using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>
  /// One actor animation clip as the sh2-port pipeline writes it
  /// (".osh2anim", written by tools/anim_to_cache.py from tools/gr2clip.exe JSON).
  ///
  /// A clip is a set of named transform tracks. Each track carries three
  /// channels - position (3 floats per key), orientation (4) and scaleShear
  /// (9) - and each channel has its OWN keyframe times, so the three counts on
  /// a track are unrelated. A count of 0 means that channel is the identity
  /// for the whole track (granny writes a NULL curve object there).
  ///
  /// The join to a model is by BONE NAME: a clip's track set is a SUPERSET of
  /// a model's bone set (fingers/nubs included), so tracks the model lacks are
  /// simply ignored when the clip is attached.
  /// </summary>
  public sealed class Granny2AnimData
  {
    public string ClipName { get; internal set; }
    /// <summary>Game-relative .GR2 path the clip was extracted from.</summary>
    public string SourcePath { get; internal set; }
    public float Duration { get; internal set; }
    public float Timestep { get; internal set; }
    /// <summary>Tracks the clip carries, as written by the extractor.</summary>
    public int TrackCount { get; internal set; }
    public IReadOnlyList<Granny2AnimTrackData> Tracks { get; internal set; } = Array.Empty<Granny2AnimTrackData>();

    /// <summary>The track for <paramref name="boneName"/>, or null (ordinal match).</summary>
    public Granny2AnimTrackData Track(string boneName)
    {
      if (boneName == null) throw new ArgumentNullException(nameof(boneName));
      foreach (Granny2AnimTrackData track in Tracks)
        if (string.Equals(track.BoneName, boneName, StringComparison.Ordinal)) return track;
      return null;
    }

    /// <summary>
    /// A track-less clip, for callers that only need a valid (empty) clip to
    /// exercise a code path - the dev harness's "an actor with no bones" control.
    /// Built against anything it yields zero applied tracks, which is exactly
    /// what that control asserts.
    /// </summary>
    public static Granny2AnimData Empty(string sourcePath) => new Granny2AnimData
    {
      ClipName = "empty",
      SourcePath = sourcePath ?? "",
      Duration = 0f,
      Timestep = 0f,
      TrackCount = 0,
      Tracks = Array.Empty<Granny2AnimTrackData>()
    };
  }

  public sealed class Granny2AnimTrackData
  {
    /// <summary>
    /// The animated bone's name, or empty. The NAME is the join: an unnamed
    /// track can never be attached to a skeleton bone, so the reader rejects it
    /// rather than let a curve land on the model root by accident.
    /// </summary>
    public string BoneName { get; internal set; }
    /// <summary>{time, x, y, z} keys in ascending time; empty = identity.</summary>
    public IReadOnlyList<Granny2AnimKey> PositionKeys { get; internal set; } = Array.Empty<Granny2AnimKey>();
    /// <summary>{time, x, y, z, w} quaternion keys in ascending time; empty = identity.</summary>
    public IReadOnlyList<Granny2AnimKey> OrientationKeys { get; internal set; } = Array.Empty<Granny2AnimKey>();
    /// <summary>{time, m00..m22} keys in ascending time; empty = identity.</summary>
    public IReadOnlyList<Granny2AnimKey> ScaleShearKeys { get; internal set; } = Array.Empty<Granny2AnimKey>();
  }

  /// <summary>
  /// One keyframe: a time and that channel's raw values in the granny2
  /// convention (centimetres, quaternion xyzw, row-major scale-shear). The
  /// values are spline CONTROL POINTS, not samples - the orientation
  /// quaternions are up to 0.07 off unit length - so the consumer normalizes.
  /// </summary>
  public readonly struct Granny2AnimKey
  {
    public readonly float Time;
    public readonly float[] Values;

    public Granny2AnimKey(float time, float[] values)
    {
      Time = time;
      Values = values;
    }
  }
}
