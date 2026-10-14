using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>
  /// Reads the OpenSH2 actor-animation cache (".osh2anim"), written by
  /// sh2-port/tools/anim_to_cache.py.
  ///
  /// Layout (all little-endian; strings are .NET-style LEB128 length-prefixed
  /// UTF-8, the same convention the ".osh2mesh" reader uses):
  ///   "OSH2AN" magic, int32 version (1)
  ///   string clipName, string sourcePath (game-relative .gr2 path),
  ///   f32 duration, f32 timestep, int32 trackCount,
  ///   per track {string boneName,
  ///              int32 positionKeyCount    {f32 time, f32 x,y,z},
  ///              int32 orientationKeyCount {f32 time, f32 x,y,z,w},
  ///              int32 scaleShearKeyCount  {f32 time, f32 m00..m22}}
  ///   No trailing data.
  ///
  /// Values stay in the granny2 convention; the Z-up-cm -> Y-up-Unity map is
  /// applied where the clip is turned into curves
  /// (<see cref="Granny2AnimationClip"/>), exactly as the rest pose is.
  /// </summary>
  public static class Granny2AnimCacheReader
  {
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OSH2AN");

    // Channel dimensions, fixed by the format: a position is a 3-vector, an
    // orientation a quaternion and a scaleShear a 3x3.
    private const int PositionDimension = 3;
    private const int OrientationDimension = 4;
    private const int ScaleShearDimension = 9;

    private const int MaxTracks = 4096;
    private const int MaxKeysPerChannel = 1 << 20;

    public static Granny2AnimData Load(string cachePath)
    {
      using var stream = File.OpenRead(cachePath);
      using var reader = new BinaryReader(stream, Encoding.UTF8);
      byte[] magic = reader.ReadBytes(Magic.Length);
      if (magic.Length != Magic.Length || !Equal(magic, Magic))
        throw new InvalidDataException("This is not an OpenSH2 Granny animation cache.");
      int version = reader.ReadInt32();
      if (version != 1)
        throw new InvalidDataException($"Unsupported Granny animation cache version {version}.");

      var anim = new Granny2AnimData
      {
        ClipName = reader.ReadString(),
        SourcePath = reader.ReadString(),
        Duration = reader.ReadSingle(),
        Timestep = reader.ReadSingle(),
        TrackCount = ReadCount(reader, "track", MaxTracks)
      };

      // The clip rig is a superset of any single model's rig, so the same bone
      // name never occurs twice - a duplicate would make the clip's own intent
      // ambiguous rather than merely redundant.
      var seen = new HashSet<string>(StringComparer.Ordinal);
      var tracks = new List<Granny2AnimTrackData>(anim.TrackCount);
      for (int i = 0; i < anim.TrackCount; i++)
      {
        Granny2AnimTrackData track = ReadTrack(reader, anim, i);
        if (!seen.Add(track.BoneName))
          throw new InvalidDataException(
            $"Track {track.BoneName} appears more than once (from track {i} on).");
        tracks.Add(track);
      }

      anim.Tracks = tracks;
      // Anything left means the writer and this reader disagree about the
      // format, or the file was truncated and padded by something else.
      if (stream.Position != stream.Length)
        throw new InvalidDataException("The Granny animation cache contains trailing data.");
      return anim;
    }

    private static Granny2AnimTrackData ReadTrack(BinaryReader reader, Granny2AnimData anim, int index)
    {
      string boneName = reader.ReadString();
      // An unnamed track can never be joined to a skeleton bone: the join is by
      // name, so a curve with no name would silently attach to nothing (or,
      // worse, to whatever an empty-string path resolves to in Unity's legacy
      // Animation). Reject instead.
      if (string.IsNullOrWhiteSpace(boneName))
        throw new InvalidDataException($"Track {index} carries no bone name.");
      var track = new Granny2AnimTrackData
      {
        BoneName = boneName,
        PositionKeys = ReadChannel(reader, anim, boneName, PositionDimension),
        OrientationKeys = ReadChannel(reader, anim, boneName, OrientationDimension),
        ScaleShearKeys = ReadChannel(reader, anim, boneName, ScaleShearDimension)
      };
      if (track.PositionKeys.Count == 0 && track.OrientationKeys.Count == 0
        && track.ScaleShearKeys.Count == 0)
      {
        throw new InvalidDataException(
          $"Track '{boneName}' is empty on all three channels - a granny clip "
          + "keys at least one of them for every track it carries.");
      }
      return track;
    }

    /// <summary>
    /// One channel's keys, checked against the clip's own duration. An empty
    /// channel is legitimate (a NULL curve in the .gr2 means that channel is
    /// the identity); anything between 1 and the maximum is data.
    /// </summary>
    private static List<Granny2AnimKey> ReadChannel(BinaryReader reader, Granny2AnimData anim,
      string boneName, int dimension)
    {
      int count = ReadCount(reader, $"{boneName} key", MaxKeysPerChannel);
      var keys = new List<Granny2AnimKey>(count);
      float previousTime = float.NegativeInfinity;
      for (int i = 0; i < count; i++)
      {
        float time = reader.ReadSingle();
        if (!float.IsFinite(time) || time < 0f || time > anim.Duration + TimeSlack(anim.Duration))
          throw new InvalidDataException(
            $"Track '{boneName}' has a key at time {time} outside [0, {anim.Duration}].");
        // Unity's AnimationCurve demands non-decreasing times; granny guarantees
        // strictly increasing artist keyframe times.
        if (time < previousTime)
          throw new InvalidDataException(
            $"Track '{boneName}' has keys out of order at {time} after {previousTime}.");
        previousTime = time;
        var values = new float[dimension];
        for (int v = 0; v < dimension; v++) values[v] = reader.ReadSingle();
        if (dimension == OrientationDimension && Length(values) == 0f)
          throw new InvalidDataException(
            $"Track '{boneName}' carries a zero-length orientation quaternion.");
        keys.Add(new Granny2AnimKey(time, values));
      }
      return keys;
    }

    /// <summary>Float rounding slack on the "inside the clip duration" check.</summary>
    private static float TimeSlack(float duration) => 1e-3f + Math.Abs(duration) * 1e-6f;

    private static float Length(float[] values)
    {
      double sum = 0;
      for (int i = 0; i < values.Length; i++) sum += (double)values[i] * values[i];
      return (float)Math.Sqrt(sum);
    }

    private static int ReadCount(BinaryReader reader, string description, int maximum)
    {
      int value = reader.ReadInt32();
      if (value < 0 || value > maximum) throw new InvalidDataException($"Invalid {description} count {value}.");
      return value;
    }

    private static bool Equal(byte[] left, byte[] right)
    {
      for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
      return true;
    }
  }
}
