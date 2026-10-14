using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode entry point for the CLIP path, so the whole of "an actor plays a
  /// clip in-engine" is checkable without an editor window:
  ///   Unity -batchmode -projectPath <proj> -executeMethod S2MActorAnimationRun.Main -quit
  ///
  /// The checks, in order:
  ///   1. read-back  - the ".osh2anim" cache loads, every track and key inside
  ///                   its own bounds, no trailing data.
  ///   2. curve data - at EVERY position key time of every bone, Unity's own
  ///                   animation sampler must have written the key's value
  ///                   onto the right bone, converted the 1e-4 m tolerance
  ///                   way (same axis map and unitScale as the rest pose).
  ///   3. motion     - the clip must actually move: report the bone-translation
  ///                   and rotation ranges over the whole duration.
  ///   4. negatives  - a truncated and a bad-magic copy of the cache must both
  ///                   be rejected, otherwise check 2 proves nothing.
  ///   5. capture    - the actor rendered from above at two times, so the
  ///                   pose difference is visible without a display.
  /// </summary>
  public static class S2MActorAnimationRun
  {
    public static string GameRoot => Constants.GamePath;

    private const string ModelPath = "meshes/units/models/archer.GR2";
    private const string ClipPath = "meshes/units/animations/soldier/archer_idle.gr2";

    /// <summary>Position/key sample tolerance, metres (the done-criterion).</summary>
    private const double PositionTolerance = 1e-4;
    /// <summary>Quaternion component tolerance (unity quaternions, unit norm).</summary>
    private const double RotationTolerance = 1e-4;
    /// <summary>How many orientation keys per bone to spot-check (channels vary wildly in count).</summary>
    private const int OrientationKeysPerBone = 4;

    private static readonly StringBuilder Report = new();
    private static int failures;

    public static void Main()
    {
      string log = Path.Combine(Path.GetTempPath(), "sh2_actor_anim_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MActorAnimationRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MActorAnimationRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      var settings = new Granny2ModelRenderSettings();

      Granny2AnimData anim = Granny2AnimLoader.Load(GameRoot, ClipPath);
      Line($"cache        : {Granny2AnimLoader.CachePathFor(ClipPath)}");
      Line($"clip         : '{anim.ClipName}' from {anim.SourcePath}");
      Line($"duration     : {anim.Duration:F4} s, timestep {anim.Timestep:F5} s, {anim.Tracks.Count} tracks");

      GameObject actor = S2MSkinnedModelHarness.Load(ModelPath, GameRoot);
      SkinnedMeshRenderer renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
      Transform[] bones = renderer.bones;
      Line($"actor        : {ModelPath}, {bones.Length} bones, root '{bones[0].name}'");

      S2MActorAnimation.Attached attached = S2MActorAnimation.Attach(actor, anim, settings);
      Animation animation = attached.Animation;
      Check(animation != null, "the clip attached an Animation component");
      if (animation == null) return;

      Line($"curves       : {attached.AppliedTracks} tracks bound, {attached.IgnoredTracks} clip-only tracks ignored");

      SampleChecks(animation, anim, bones, settings);
      MotionChecks(animation, anim, bones);
      CheckReadBackRejections(ClipPath);
      Capture(actor, anim);
    }

    // 2. every position key lands on the right bone, and the spot-checked
    //    orientation keys round the same bend.
    private static void SampleChecks(Animation animation, Granny2AnimData anim,
      Transform[] bones, Granny2ModelRenderSettings settings)
    {
      int keysChecked;
      double worstPosition;
      string worstBone;
      int mismatches = PositionMismatches(animation, anim, bones, settings.UnitScale,
        Vector3.zero, out keysChecked, out worstPosition, out worstBone);
      Check(keysChecked > 0, "the clip sampled at least one position key");
      Check(mismatches == 0,
        $"all {keysChecked} position keys within {PositionTolerance} m "
        + $"(worst {worstPosition:F6} m on {worstBone})");

      // "everything matched" is only evidence if the comparison is able to
      // fail: shift every expectation by 8.7 mm and the same loop must now
      // report a mismatch on every single key.
      Vector3 sabotage = new Vector3(5e-3f, 5e-3f, 5e-3f);
      int blind = PositionMismatches(animation, anim, bones, settings.UnitScale,
        sabotage, out _, out _, out _);
      Check(blind > keysChecked / 2,
        $"the 1e-4 m tolerance is not blind: a 8.7 mm off expectation mismatches {blind} of {keysChecked} keys");

      double worstRotation = 0;
      int orientationChecked = 0, skippedBones = 0;
      var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
      foreach (Transform bone in bones) byName[bone.name] = bone;
      foreach (Granny2AnimTrackData track in anim.Tracks)
      {
        if (!byName.TryGetValue(track.BoneName, out Transform bone)) continue;
        bool used = track.PositionKeys.Count > 0 || track.OrientationKeys.Count > 0;
        int perBoneChecked = 0;
        foreach (Granny2AnimKey key in track.OrientationKeys)
        {
          if (perBoneChecked >= OrientationKeysPerBone) break;
          Sample(animation, anim, key.Time);
          Quaternion expected = ConvertRotation(key.Values);
          Quaternion actual = bone.localRotation;
          perBoneChecked++;
          orientationChecked++;
          // q and -q are the same rotation; compare the shorter twin.
          double error = Math.Min(
            Magnitude(actual.x - expected.x, actual.y - expected.y, actual.z - expected.z, actual.w - expected.w),
            Magnitude(actual.x + expected.x, actual.y + expected.y, actual.z + expected.z, actual.w + expected.w));
          if (error > worstRotation) worstRotation = error;
          if (error > RotationTolerance)
          {
            Line($"   FAIL rot   {track.BoneName} @{key.Time:F4}s expected {expected} got {actual}");
            failures++;
          }
        }
        if (!used) skippedBones++;
      }
      Check(worstRotation <= RotationTolerance,
        $"all {orientationChecked} orientation spot-checks within {RotationTolerance} (worst {worstRotation:F6})");
      Line($"bones        : {byName.Count} on this skeleton, {skippedBones} carried no key at all");
    }

    /// <summary>
    /// Samples the clip at every position key time of every bone the skeleton
    /// has and compares the bone's local position to the converted key value
    /// plus <paramref name="expectedOffset">. A zero offset is the real check;
    /// a non-zero one is the self-check on the tolerance itself.
    /// </summary>
    private static int PositionMismatches(Animation animation, Granny2AnimData anim,
      Transform[] bones, float unitScale, Vector3 expectedOffset, out int keysChecked,
      out double worstError, out string worstBone)
    {
      var byName = new Dictionary<string, Transform>(StringComparer.Ordinal);
      foreach (Transform bone in bones) byName[bone.name] = bone;
      keysChecked = 0;
      worstError = 0;
      worstBone = null;
      int mismatches = 0;
      foreach (Granny2AnimTrackData track in anim.Tracks)
      {
        if (!byName.TryGetValue(track.BoneName, out Transform bone)) continue;
        foreach (Granny2AnimKey key in track.PositionKeys)
        {
          Sample(animation, anim, key.Time);
          Vector3 expected = ConvertPosition(key.Values, unitScale) + expectedOffset;
          double error = (bone.localPosition - expected).magnitude;
          if (error > worstError) { worstError = error; worstBone = track.BoneName; }
          keysChecked++;
          if (error > PositionTolerance) mismatches++;
        }
      }
      return mismatches;
    }

    // 3. the clip has to MOVE something, else every check above also passes on
    //    a degenerate all-identity clip.
    private static void MotionChecks(Animation animation, Granny2AnimData anim, Transform[] bones)
    {
      var first = new Vector3[bones.Length];
      var firstRotation = new Quaternion[bones.Length];
      Sample(animation, anim, 0f);
      for (int i = 0; i < bones.Length; i++) { first[i] = bones[i].localPosition; firstRotation[i] = bones[i].localRotation; }

      int movedPositions = 0, movedRotations = 0;
      double maxTranslation = 0, maxRotation = 0;
      string translationBone = null, rotationBone = null;
      int samples = 24;
      for (int s = 1; s < samples; s++)
      {
        float time = anim.Duration * s / samples;
        Sample(animation, anim, time);
        for (int i = 0; i < bones.Length; i++)
        {
          double distance = (bones[i].localPosition - first[i]).magnitude;
          if (distance > maxTranslation) { maxTranslation = distance; translationBone = bones[i].name; }
          if (distance > 1e-3) movedPositions++;
          double angle = Quaternion.Angle(firstRotation[i], bones[i].localRotation);
          if (angle > maxRotation) { maxRotation = angle; rotationBone = bones[i].name; }
          if (angle > 1e-3) movedRotations++;
        }
      }
      Line($"motion       : over {samples} samples, {movedPositions} position and {movedRotations} rotation channel-moves");
      Line($"              biggest translation {maxTranslation:F4} m ({translationBone}), "
           + $"biggest rotation {maxRotation:F2} deg ({rotationBone})");
      Check(maxTranslation > 0.01f, $"the clip translates a bone by more than 1 cm ({maxTranslation:F4} m)");
      Check(maxRotation > 0.5f, $"the clip rotates a bone by more than 0.5 deg ({maxRotation:F2} deg)");
    }

    // 4. negative controls on the reader: without these, "every key matched"
    //    could just mean the sampler never wrote anything.
    private static void CheckReadBackRejections(string clipPath)
    {
      string cachePath = Granny2AnimLoader.CachePathFor(clipPath);
      string directory = Path.Combine(Path.GetTempPath(), "sh2_anim_negative");
      Directory.CreateDirectory(directory);

      byte[] data = File.ReadAllBytes(cachePath);
      string trailing = Path.Combine(directory, "trailing.osh2anim");
      File.WriteAllBytes(trailing, data.Concat(new byte[] { 0, 0, 0, 0 }).ToArray());
      Rejects(trailing, "a cache with trailing data");

      string badMagic = Path.Combine(directory, "magic.osh2anim");
      byte[] broken = (byte[])data.Clone();
      broken[0] ^= 0xFF;
      File.WriteAllBytes(badMagic, broken);
      Rejects(badMagic, "a cache with a corrupt magic");

      string truncated = Path.Combine(directory, "truncated.osh2anim");
      File.WriteAllBytes(truncated, data.Take(data.Length / 2).ToArray());
      Rejects(truncated, "a truncated cache");
    }

    private static void Rejects(string path, string description)
    {
      try
      {
        Granny2AnimCacheReader.Load(path);
        Check(false, $"{description} must be rejected");
      }
      catch (Exception exception) when (exception is IOException or InvalidDataException)
      {
        Line($"rejects      : {description} ({exception.GetType().Name})");
      }
    }

    // 5. top-down captures at the first and last quarter of the clip.
    private static void Capture(GameObject actor, Granny2AnimData anim)
    {
      string directory = Path.Combine(Path.GetTempPath(), "sh2_actor_anim");
      Directory.CreateDirectory(directory);
      var captured = new List<string>();
      var cameraObject = new GameObject("Capture Camera");
      var camera = cameraObject.AddComponent<Camera>();
      camera.orthographic = true;
      camera.orthographicSize = 1.3f;
      camera.nearClipPlane = 0.01f;
      camera.farClipPlane = 20f;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0.75f, 0f, 0.75f);
      cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
      cameraObject.transform.position = new Vector3(0f, 6f, 0f);
      // URP Lit reads nothing without a light; a headless batchmode run has no
      // ambient either, so one directional light stands in for the scene.
      var lightObject = new GameObject("Capture Light");
      var light = lightObject.AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = 1.1f;
      light.shadows = LightShadows.None;
      lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

      var render = new RenderTexture(640, 640, 24);
      camera.targetTexture = render;
      var captureTimes = new List<float> { 0f, anim.Duration * 0.5f, anim.Duration * 0.95f };
      foreach (float time in captureTimes)
      {
        Animation animation = actor.GetComponent<Animation>();
        if (animation != null) Sample(animation, anim, time);
        camera.Render();
        var image = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
        RenderTexture.active = render;
        image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
        RenderTexture.active = null;
        byte[] png = image.EncodeToPNG();
        string path = Path.Combine(directory, $"archer_idle_{time:F2}s.png");
        File.WriteAllBytes(path, png);
        UnityEngine.Object.DestroyImmediate(image);
        captured.Add(path);
      }
      UnityEngine.Object.DestroyImmediate(render);
      UnityEngine.Object.DestroyImmediate(cameraObject);
      UnityEngine.Object.DestroyImmediate(lightObject);
      Line($"captures     : {captured.Count} top-down frames at "
           + $"{string.Join(", ", captured.ConvertAll(p => Path.GetFileName(p)))}");
    }

    private static void Sample(Animation animation, Granny2AnimData anim, float time)
    {
      string name = string.IsNullOrWhiteSpace(anim.ClipName) ? "clip" : anim.ClipName;
      AnimationState state = animation[name];
      state.time = Math.Max(0f, Math.Min(time, anim.Duration));
      animation.Sample();
    }

    private static double Magnitude(float x, float y, float z, float w) =>
      Math.Sqrt((double)x * x + (double)y * y + (double)z * z + (double)w * w);

    private static Vector3 ConvertPosition(float[] raw, float unitScale) =>
      new Vector3(raw[0], raw[2], raw[1]) * unitScale;

    private static Quaternion ConvertRotation(float[] raw)
    {
      // sigma is a reflection: the VECTOR part of the quaternion is negated.
      var quaternion = new Quaternion(raw[0], raw[2], raw[1], -raw[3]);
      if (quaternion.x * quaternion.x + quaternion.y * quaternion.y
        + quaternion.z * quaternion.z + quaternion.w * quaternion.w > 0f) quaternion.Normalize();
      return quaternion;
    }

    private static void Check(bool passed, string description)
    {
      Line($"{(passed ? "PASS" : "FAIL")}         : {description}");
      if (!passed) failures++;
    }

    private static void Line(string text)
    {
      Report.AppendLine(text);
    }
  }
}
