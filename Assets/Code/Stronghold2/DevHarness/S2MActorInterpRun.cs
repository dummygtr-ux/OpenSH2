using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode check of the BETWEEN-KEY SHAPE: Unity's legacy clip interpolates
  /// the cached key values with its own curve; the shipped curve is a degree-2
  /// (quadratic) spline over CONTROL POINTS, so the two are not the same curve.
  /// This run measures the difference on the driver path:
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MActorInterpRun.Main -quit
  ///
  /// The reference is the game's own granny2.dll: sh2-port/tools/gr2sample.exe
  /// evaluates each track/channel with the DLL's exported degree-2 kernels
  /// (GrannySampleBSpline2x3/2x4, impls 0x50016470 / 0x50016520) at the clip's
  /// declared timestep, offset half a step so the samples fall BETWEEN the cache
  /// keys - a reference at the key times would be passed by construction.
  ///
  /// Deviations are reported in Unity units (metres, degrees) after the same
  /// axis/unit map the rest pose uses, per clip family (idle vs walk) and per
  /// channel, with the worst bone and time, plus a sabotage run proving the
  /// comparison can fail. The visible bar - 0.5 deg of rotation, 5 mm of
  /// position - is what the bake (anim_to_cache.py --resample) must bring the
  /// numbers under; the artist-key caches miss it.
  /// </summary>
  public static class S2MActorInterpRun
  {
    public static string GameRoot => Constants.GamePath;

    private static string RefDir =>
      Path.Combine(Constants.PortRoot, "artifacts", "interp");

    /// <summary>The visible bar: under these, the shape difference cannot be seen.</summary>
    private const double RotationBar = 0.5;      // degrees
    private const double PositionBar = 0.005;    // metres

    private readonly struct ClipSpec
    {
      public readonly string Label;
      public readonly string Family;
      public readonly string Model;
      public readonly string Clip;

      public ClipSpec(string label, string family, string model, string clip)
      {
        Label = label;
        Family = family;
        Model = model;
        Clip = clip;
      }
    }

    private static readonly ClipSpec[] Clips =
    {
      new ClipSpec("archer_idle", "idle", "meshes/units/models/archer.GR2",
        "meshes/units/animations/soldier/archer_idle.GR2"),
      new ClipSpec("pikeman_walk", "walk", "meshes/units/models/pikeman.GR2",
        "meshes/units/animations/soldier/soldier_walk_weapon_long.GR2"),
      new ClipSpec("wolf_walk", "walk", "meshes/units/animals/wolf.GR2",
        "meshes/units/animals/wolf_walk.GR2"),
      new ClipSpec("rabbit_hop", "walk", "meshes/units/animals/rabbit.GR2",
        "meshes/units/animals/rabbit_hop.GR2"),
    };

    [Serializable]
    private class RefClip
    {
      public string clip;
      public float duration;
      public float step;
      public int samples;
      public float[] times;
      public int[] interior;
      public RefTrack[] tracks;
    }

    [Serializable]
    private class RefTrack
    {
      public string name;
      public int positionDim;
      public float[] position;
      public int orientationDim;
      public float[] orientation;
    }

    private static readonly StringBuilder Report = new();
    private static int failures;

    public static void Main()
    {
      string log = Environment.GetEnvironmentVariable("SH2_ACTOR_INTERP_REPORT");
      if (string.IsNullOrEmpty(log))
        log = Path.Combine(Path.GetTempPath(), "sh2_actor_interp_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MActorInterpRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MActorInterpRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      var settings = new Granny2ModelRenderSettings();
      var byFamily = new Dictionary<string, (double Rot, double Pos)>(StringComparer.Ordinal);
      int overBar = 0;

      foreach (ClipSpec spec in Clips)
      {
        string reference = Path.Combine(RefDir, spec.Label + "_off.json");
        if (!File.Exists(reference))
        {
          Check(false, $"{spec.Label}: the DLL reference dump exists ({reference})");
          continue;
        }
        RefClip refClip = JsonUtility.FromJson<RefClip>(File.ReadAllText(reference));
        int maxKeys = 0;
        Granny2AnimData anim = Granny2AnimLoader.Load(GameRoot, spec.Clip);
        foreach (Granny2AnimTrackData track in anim.Tracks)
        {
          maxKeys = Math.Max(maxKeys, track.PositionKeys.Count);
          maxKeys = Math.Max(maxKeys, track.OrientationKeys.Count);
        }
        Line($"--- {spec.Label} ({spec.Family}) ---");
        Line($"  clip       : {spec.Clip}");

        GameObject actor = S2MSkinnedModelHarness.Load(spec.Model, GameRoot);
        SkinnedMeshRenderer renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
        Transform[] bones = renderer.bones;
        S2MActorAnimation.Attached attached = S2MActorAnimation.Attach(actor, anim, settings);
        Animation animation = attached.Animation;
        if (animation == null)
        {
          Check(false, $"{spec.Label}: the clip attached an Animation component");
          UnityEngine.Object.DestroyImmediate(actor);
          continue;
        }

        var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (Transform bone in bones) byName[bone.name] = bone;

        int matched = 0, clipOnly = 0, used = 0, sabotaged = 0;
        double maxRot = 0, sumRot = 0, maxPos = 0, sumPos = 0;
        long rotCount = 0, posCount = 0;
        string worstRot = "-", worstPos = "-";
        float worstRotTime = 0, worstPosTime = 0;

        foreach (RefTrack track in refClip.tracks)
        {
          if (!byName.TryGetValue(track.name, out Transform bone))
          {
            clipOnly++;
            continue;
          }
          matched++;
          for (int i = 0; i < refClip.samples; i++)
          {
            if (refClip.interior == null || refClip.interior[i] == 0) continue;
            float time = refClip.times[i];
            Sample(animation, anim, time);
            used++;
            if (track.orientation != null && track.orientationDim == 4)
            {
              Quaternion expected = ConvertRotation(track.orientation, i * 4);
              Quaternion actual = bone.localRotation;
              double angle = Quaternion.Angle(actual, expected);
              rotCount++;
              sumRot += angle;
              if (angle > maxRot) { maxRot = angle; worstRot = track.name; worstRotTime = time; }
              Quaternion shifted = expected * Quaternion.Euler(5f, 5f, 5f);
              if (Quaternion.Angle(actual, shifted) > RotationBar) sabotaged++;
            }
            if (track.position != null && track.positionDim == 3)
            {
              Vector3 expected = ConvertPosition(track.position, i * 3, settings.UnitScale);
              double distance = (bone.localPosition - expected).magnitude;
              posCount++;
              sumPos += distance;
              if (distance > maxPos) { maxPos = distance; worstPos = track.name; worstPosTime = time; }
            }
          }
        }

        Line($"  cache keys : max {maxKeys} keys per channel over {anim.Tracks.Count} tracks "
             + $"({anim.Duration:F3} s at {anim.Timestep:F4} s)");
        Line($"  reference  : {Path.GetFileName(reference)}, {refClip.samples} samples, t in "
             + $"[{refClip.times[0]:F4}, {refClip.times[refClip.samples - 1]:F4}] s "
             + $"(grid {refClip.step:F4} s, deliberately NOT a multiple of the bake grid), "
             + $"{used} track-samples used, "
             + $"{refClip.samples - (refClip.interior == null ? 0 : CountInterior(refClip))} end-span skipped");
        Line($"  join       : {matched} bones matched, {clipOnly} clip-only tracks ignored");
        Line($"  position   : max {maxPos:F5} m  mean {(posCount == 0 ? 0 : sumPos / posCount):F5} m  "
             + $"worst {worstPos} @ {worstPosTime:F2} s  ({posCount} samples)");
        Line($"  rotation   : max {maxRot:F4} deg  mean {(rotCount == 0 ? 0 : sumRot / rotCount):F4} deg  "
             + $"worst {worstRot} @ {worstRotTime:F2} s  ({rotCount} samples)");
        Line($"  sabotage   : a 5 deg shifted expectation is flagged on {sabotaged} of {rotCount} "
             + $"rotation samples");

        Check(matched > 0, $"{spec.Label}: at least one ref track joins a bone ({matched})");
        Check(used > 0, $"{spec.Label}: at least one reference sample was compared ({used})");
        Check(rotCount == 0 || sabotaged > rotCount / 2,
          $"{spec.Label}: the comparison is not blind ({sabotaged}/{rotCount} flagged on a 5 deg shift)");

        if (!byFamily.TryGetValue(spec.Family, out (double Rot, double Pos) family))
          family = (0, 0);
        byFamily[spec.Family] = (Math.Max(family.Rot, maxRot), Math.Max(family.Pos, maxPos));

        bool within = maxRot <= RotationBar && maxPos <= PositionBar;
        if (!within) overBar++;
        Check(within,
          $"{spec.Label}: within the visible bar ({RotationBar} deg / {PositionBar} m) - "
          + $"measured {maxRot:F3} deg / {maxPos:F4} m");

        UnityEngine.Object.DestroyImmediate(actor);
        Line("");
      }

      foreach (KeyValuePair<string, (double Rot, double Pos)> family in byFamily)
        Line($"family {family.Key,-5}: max rotation {family.Value.Rot:F3} deg, "
             + $"max position {family.Value.Pos:F4} m");

      if (overBar > 0)
        Line($"VERDICT: {overBar} of {Clips.Length} clips exceed the visible bar - the artist-key "
             + "caches are NOT the source curve; bake with anim_to_cache.py --resample");
      else
        Line($"VERDICT: all {Clips.Length} clips within the visible bar");
    }

    private static int CountInterior(RefClip clip)
    {
      int n = 0;
      foreach (int v in clip.interior) if (v != 0) n++;
      return n;
    }

    private static void Sample(Animation animation, Granny2AnimData anim, float time)
    {
      string name = string.IsNullOrWhiteSpace(anim.ClipName) ? "clip" : anim.ClipName;
      AnimationState state = animation[name];
      state.time = Math.Max(0f, Math.Min(time, anim.Duration));
      animation.Sample();
    }

    private static Vector3 ConvertPosition(float[] raw, int index, float unitScale) =>
      new Vector3(raw[index], raw[index + 2], raw[index + 1]) * unitScale;

    private static Quaternion ConvertRotation(float[] raw, int index)
    {
      var quaternion = new Quaternion(raw[index], raw[index + 2], raw[index + 1], -raw[index + 3]);
      if (quaternion.x * quaternion.x + quaternion.y * quaternion.y
        + quaternion.z * quaternion.z + quaternion.w * quaternion.w > 0f) quaternion.Normalize();
      return quaternion;
    }

    private static void Check(bool passed, string description)
    {
      Line($"{(passed ? "PASS" : "FAIL")}       : {description}");
      if (!passed) failures++;
    }

    private static void Line(string text)
    {
      Report.AppendLine(text);
    }
  }
}
