using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// Builds a legacy <see cref="AnimationClip"/> from one ".osh2anim" cache.
  ///
  /// The clip is joined to a skeleton by BONE NAME: the cache's track set is a
  /// superset of any one model's rig (fingers, nubs), so tracks the skeleton
  /// lacks are dropped. A channel with no keys (a NULL curve in the .gr2)
  /// emits NO curve at all, which leaves that bone's rest-pose value in place
  /// - NOT identity. The distinction is visible on the archer: "Bip01 Neck"
  /// carries one position key and no orientation curve, and its rest pitch is
  /// ~11 deg (quaternion (0, 0, 0.0988, 0.9951)); forcing identity there would
  /// snap the head upright through the whole idle, which the game's own artist
  /// keyframes plainly do not do.
  ///
  /// Coordinate derivation (same contract as
  /// <see cref="SkinnedGranny2Renderer.BuildSkeleton"/>): granny is Z-up
  /// centimetres, the bone local transform under Swap = (x, y, z) -> (x, z, y)
  /// becomes position (x, z, y), quaternion (qx, qz, qy, -qw) and diagonal
  /// scale-shear (sx, sz, sy); positions additionally pick up unitScale.
  ///
  /// The keyframe values are spline CONTROL POINTS, so an orientation
  /// quaternion is up to 0.07 off unit length. Each key is normalized
  /// individually; otherwise Unity's lerp of the four components would scale
  /// the bone's length down between keys.
  /// </summary>
  public static class Granny2AnimationClip
  {
    /// <summary>Result of <see cref="Build"/>.</summary>
    public readonly struct Report
    {
      public readonly AnimationClip Clip;
      /// <summary>Tracks that produced at least one curve on this skeleton.</summary>
      public readonly int AppliedTracks;
      /// <summary>Tracks the skeleton has no bone for (finger/nub tracks).</summary>
      public readonly int IgnoredTracks;

      public Report(AnimationClip clip, int appliedTracks, int ignoredTracks)
      {
        Clip = clip;
        AppliedTracks = appliedTracks;
        IgnoredTracks = ignoredTracks;
      }
    }

    /// <summary>The three Transform channels Unity animates for a bone.</summary>
    private const string PositionProperty = "m_LocalPosition";
    private const string RotationProperty = "m_LocalRotation";
    private const string ScaleProperty = "m_LocalScale";

    /// <summary>
    /// Builds the clip. When <paramref name="bones"/> is supplied the curves are
    /// keyed by each bone's path relative to <paramref name="root"/> (the
    /// GameObject the <see cref="Animation"/> component is added to), because
    /// Unity's legacy animation addresses a bone by its hierarchy path, not by
    /// its name - a nested bone like "Bip01 L Calf" is "Bip01 Pelvis/Bip01 Spine/
    /// .../Bip01 L Calf" from the model root.
    /// </summary>
    public static Report Build(Granny2AnimData anim, Transform root = null,
      IReadOnlyList<Transform> bones = null, Granny2ModelRenderSettings settings = null)
    {
      if (anim == null) throw new ArgumentNullException(nameof(anim));
      settings ??= new Granny2ModelRenderSettings();
      float unitScale = settings.UnitScale;
      if (unitScale <= 0f) throw new ArgumentOutOfRangeException(nameof(settings.UnitScale));

      // legacy first: setting it after the curves are added leaves the earlier
      // ones outside the legacy animation path.
      var clip = new AnimationClip { name = anim.ClipName, legacy = true };
      if (anim.Timestep > 0f) clip.frameRate = 1f / anim.Timestep;
      clip.wrapMode = WrapMode.Loop;

      if (bones == null || bones.Count == 0)
      {
        // No skeleton given: address every track by its bare bone name. Only
        // correct when the Animation component sits ON the bone's parent node.
        foreach (Granny2AnimTrackData track in anim.Tracks)
          for (int channel = 0; channel < 3; channel++)
            AddChannel(clip, track.BoneName, track, channel, unitScale);
        return new Report(clip, anim.Tracks.Count, 0);
      }

      var applied = new HashSet<string>(StringComparer.Ordinal);
      int ignored = 0;
      foreach (Granny2AnimTrackData track in anim.Tracks)
      {
        string path = null;
        foreach (Transform bone in bones)
        {
          if (!string.Equals(bone.name, track.BoneName, StringComparison.Ordinal)) continue;
          path = PathRelativeTo(bone, root);
          break;
        }
        if (path == null)
        {
          ignored++;
          continue;
        }
        bool emitted = false;
        for (int channel = 0; channel < 3; channel++)
          emitted |= AddChannel(clip, path, track, channel, unitScale);
        if (emitted) applied.Add(path);
      }
      return new Report(clip, applied.Count, ignored);
    }

    /// <summary>Adds one channel's curves; returns false when it is empty.</summary>
    private static bool AddChannel(AnimationClip clip, string path, Granny2AnimTrackData track,
      int channel, float unitScale)
    {
      IReadOnlyList<Granny2AnimKey> keys;
      string property;
      float scale;
      switch (channel)
      {
        case 0:
          keys = track.PositionKeys; property = PositionProperty; scale = unitScale; break;
        case 1:
          keys = track.OrientationKeys; property = RotationProperty; scale = 1f; break;
        default:
          keys = track.ScaleShearKeys; property = ScaleProperty; scale = 1f; break;
      }
      if (keys.Count == 0) return false;

      int componentCount = ComponentCount(channel);
      var curves = new float[componentCount][];
      for (int c = 0; c < componentCount; c++) curves[c] = new float[keys.Count];
      for (int k = 0; k < keys.Count; k++)
      {
        Granny2AnimKey key = keys[k];
        float[] values = new float[componentCount];
        Convert(key.Values, channel, unitScale, values);
        for (int c = 0; c < componentCount; c++) curves[c][k] = values[c];
      }
      for (int c = 0; c < componentCount; c++)
        clip.SetCurve(path, typeof(Transform), $"{property}.{Suffix(channel, c)}",
          new AnimationCurve(Keys(keys, curves[c])));
      return true;
    }

    private static Keyframe[] Keys(IReadOnlyList<Granny2AnimKey> keys, float[] values)
    {
      var result = new Keyframe[keys.Count];
      for (int k = 0; k < result.Length; k++)
        result[k] = new Keyframe(keys[k].Time, values[k]);
      return result;
    }

    private static int ComponentCount(int channel) => channel == 0 ? 3 : channel == 1 ? 4 : 3;

    private static string Suffix(int channel, int component) =>
      channel == 1 ? "xyzw"[component].ToString(CultureInfo.InvariantCulture)
      : "xyz"[component].ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// granny convention -> Unity convention for one key of one channel.
    /// Position (x, y, z) -> (x, z, y) * unitScale; orientation quaternion
    /// xyzw -> (qx, qz, qy, -qw) normalized (sigma is a reflection, so the vector
    /// part is negated); scaleShear takes its DIAGONAL (m00, m44, m88) as
    /// (sx, sz, sy), exactly as the rest pose does.
    /// </summary>
    private static void Convert(float[] raw, int channel, float unitScale, float[] into)
    {
      switch (channel)
      {
        case 0:
          into[0] = raw[0] * unitScale;
          into[1] = raw[2] * unitScale;
          into[2] = raw[1] * unitScale;
          break;
        case 1:
          var quaternion = new Quaternion(raw[0], raw[2], raw[1], -raw[3]);
          // A spline control point need not be unit: normalize per key so Unity's
          // linear component lerp cannot shorten the bone between keys.
          if (SqrMagnitude(ref quaternion) > 0f) quaternion.Normalize();
          into[0] = quaternion.x;
          into[1] = quaternion.y;
          into[2] = quaternion.z;
          into[3] = quaternion.w;
          break;
        default:
          into[0] = raw[0];
          into[1] = raw[8];
          into[2] = raw[4];
          break;
      }
    }

    // UnityEngine.Quaternion carries no sqrMagnitude (that is Vector3 only).
    private static float SqrMagnitude(ref Quaternion q) =>
      q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;

    /// <summary>
    /// The bone's path from <paramref name="root"/> - Unity's legacy Animation
    /// addresses a transformed bone by hierarchy path. A bone that is not a
    /// descendant of <paramref name="root"/> gets the empty path, which makes
    /// the curves bind to the Animation component's own GameObject.
    /// </summary>
    public static string PathRelativeTo(Transform bone, Transform root)
    {
      if (bone == null) throw new ArgumentNullException(nameof(bone));
      var chain = new List<string>();
      Transform current = bone;
      while (current != null && current != root)
      {
        chain.Add(current.name);
        current = current.parent;
      }
      // current == root means the bone is a descendant of the animation's
      // GameObject (or is it); current == null means it is not under it at all,
      // and binding an absolute name there would silently attach the curves to
      // whatever object happens to carry that name at the model root.
      if (current == null) return null;
      chain.Reverse();
      return string.Join("/", chain);
    }

    /// <summary>Human-readable summary of what a clip would animate, for logs.</summary>
    public static string Describe(Report report, Granny2AnimData anim)
    {
      var sb = new StringBuilder();
      sb.AppendLine($"{anim.ClipName}: {anim.Duration:F3} s, step {anim.Timestep:F4} s, "
                    + $"{report.AppliedTracks} of {anim.Tracks.Count} tracks applied, "
                    + $"{report.IgnoredTracks} ignored (not on this skeleton)");
      return sb.ToString().TrimEnd();
    }
  }
}
