using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// Measurements taken off a clip's OWN keyframes ("Granny2AnimData"), used to
  /// tell a locomotion clip from an idle one without playing it.
  ///
  /// WHY THIS EXISTS: the per-state clip table maps a state to a clip FILE, so
  /// "the actor is now walking" is only as good as the file it was handed. A
  /// rig's clip that animates nothing (the 23 one-track PROP clips -
  /// sword02/horse_LC/Object01/Cylinder26 - bind no bone on a skinned actor)
  /// would still look like a successful state switch, because the lookup and
  /// the attach both succeed. These two numbers are the observable difference:
  ///
  ///  - ROOT DRIFT: how far the skeleton root's own position travels NET over
  ///    one loop. Measured over the whole catalogue the answer is ~0: the game's
  ///    walk/run cycles are authored IN PLACE (net 0.000..0.05 m per loop) and
  ///    so are the idles. A clip therefore carries no root motion, and the actor's
  ///    travel through the world is the game's job (<see cref="S2MActorStateDriver"/>),
  ///    not the clip's. A large drift would mean a root-motion clip, which this
  ///    pipeline has never produced.
  ///
  ///  - LEG ANGULAR SPEED: the mean angle per second the leg bones (Thigh/Calf/
  ///    Foot, the tokens most rig families share) sweep between consecutive keys.
  ///    That is what separates a walk from an idle: over the 103-row state
  ///    catalogue the locomotion clips measure 48..204 deg/s and the idles
  ///    6..22 deg/s (a lone walk clip - the horse archer's ha_walk, whose rider
  ///    legs stay still while the horse mesh moves - sits at 15, and the rabbit's
  ///    hop has no leg swing at all and reads 597). The pair, not one number, is
  ///    the evidence.
  ///
  /// Both are computed on the artist keyframes the cache stores, i.e. at key
  /// resolution, not at Unity's interpolated frame rate - a mean over the
  /// clip's whole duration is unaffected by either, which is why the second
  /// metric is a mean and not a peak.
  /// </summary>
  public static class S2MActorClipMetrics
  {
    /// <summary>Bone-name fragments that mark a leg bone across the rig
    /// families (humanoid "Bip01 L Calf", animal "WOLF L Calf",
    /// "BEAR R Foot"). The root's own name is never one of these.</summary>
    public static readonly string[] LegBoneTokens = { "Calf", "Foot", "Thigh" };

    /// <summary>
    /// Net distance the skeleton root travels between the clip's first and last
    /// position key, in metres. 0 when the clip keys no root position (a clip
    /// whose root channel is identity - it animates the limbs only).
    /// </summary>
    public static float RootDriftPerLoop(Granny2AnimData anim, string rootBoneName,
      float unitScale)
    {
      if (anim == null) throw new ArgumentNullException(nameof(anim));
      if (string.IsNullOrEmpty(rootBoneName) || unitScale <= 0f) return 0f;
      Granny2AnimTrackData track = anim.Track(rootBoneName);
      if (track == null || track.PositionKeys.Count < 2) return 0f;
      // The same Z-up-cm -> Y-up-metre map Granny2AnimationClip applies, so the
      // number is comparable with what the renderer puts on screen.
      Vector3 first = Position(track.PositionKeys[0], unitScale);
      Vector3 last = Position(track.PositionKeys[^1], unitScale);
      return Vector3.Distance(first, last);
    }

    /// <summary>
    /// Mean angle per second (degrees) the leg bones sweep, averaged over every
    /// leg track's every consecutive key pair. 0 when no bone matches a leg token
    /// (a rigid siege engine, a 1-track PROP clip).
    /// </summary>
    public static float LegAngularSpeedPerSecond(Granny2AnimData anim)
    {
      if (anim == null) throw new ArgumentNullException(nameof(anim));
      double total = 0;
      int pairs = 0;
      foreach (Granny2AnimTrackData track in anim.Tracks)
      {
        if (!IsLegBone(track.BoneName)) continue;
        IReadOnlyList<Granny2AnimKey> keys = track.OrientationKeys;
        for (int k = 0; k + 1 < keys.Count; k++)
        {
          float dt = keys[k + 1].Time - keys[k].Time;
          if (dt <= 0f) continue;
          total += AngleBetween(keys[k].Values, keys[k + 1].Values) / dt;
          pairs++;
        }
      }
      return pairs == 0 ? 0f : (float)(total / pairs);
    }

    /// <summary>Whether a bone name marks a leg bone on any rig family.</summary>
    public static bool IsLegBone(string boneName)
    {
      if (string.IsNullOrEmpty(boneName)) return false;
      foreach (string token in LegBoneTokens)
        if (boneName.IndexOf(token, StringComparison.Ordinal) >= 0) return true;
      return false;
    }

    /// <summary>
    /// granny centimetres, Z up -> Unity metres, Y up: (x, y, z) -> (x, z, y).
    /// Same conversion as <see cref="Granny2AnimationClip"/>'s position channel.
    /// </summary>
    private static Vector3 Position(Granny2AnimKey key, float unitScale) =>
      new Vector3(key.Values[0] * unitScale, key.Values[2] * unitScale,
        key.Values[1] * unitScale);

    /// <summary>
    /// Angle between two quaternions in the cache's xyzw layout. The values are
    /// spline control points (up to 0.07 off unit length), so both are scaled by
    /// their length rather than assumed unit; the shorter of q/-q is taken
    /// because a quaternion and its negation are the same rotation.
    /// </summary>
    private static float AngleBetween(float[] left, float[] right)
    {
      float dot = Dot(left, right);
      float norm = Length(left) * Length(right);
      if (norm <= 0f) return 0f;
      float cosine = Mathf.Clamp(Mathf.Abs(dot) / norm, 0f, 1f);
      return 2f * Mathf.Acos(cosine) * Mathf.Rad2Deg;
    }

    private static float Dot(float[] a, float[] b)
    {
      double sum = 0;
      for (int i = 0; i < 4; i++) sum += (double)a[i] * b[i];
      return (float)sum;
    }

    private static float Length(float[] a)
    {
      double sum = 0;
      for (int i = 0; i < 4; i++) sum += (double)a[i] * a[i];
      return (float)Math.Sqrt(sum);
    }
  }
}
