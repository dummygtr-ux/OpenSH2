using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode measurement of what ONE MAP LOAD of the entity path costs - the
  /// instrument behind the memory-scaling fix.
  ///
  ///   Unity -batchmode -nographics -projectPath &lt;proj&gt; \
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MEntityMemoryRun.Main -quit
  ///
  /// Why it exists: the flow UAT's run 2 (the dev-harness map render) was
  /// OOM-killed five times on 2026-10-12 at war_chapter8's map load, the last
  /// time at 7.69 GB anon-rss with four actor types already animating. The
  /// suspected cause is duplication rather than size: 340 actors are drawn from
  /// 13 model files, but <see cref="SkinnedGranny2Renderer"/> is called once per
  /// ACTOR and builds a fresh <see cref="Mesh"/>, material and texture set each
  /// time. This run puts numbers on that claim (and on the fix's drop).
  ///
  /// WHAT IT MEASURES, all attributable to the load itself (a baseline is taken
  /// after the roots are destroyed and the unused assets collected, so every
  /// number is a delta):
  ///
  ///   * the SHARING RATIO - how many Mesh objects the actors reference in
  ///     total versus how many DISTINCT ones exist. "340 references over 340
  ///     meshes" is the duplication; "340 references over 13" is the fix.
  ///     The same pair for Material and Texture2D;
  ///   * the per-group table - one row per model group (the loader's own
  ///     grouping), with its actor count, mesh/material/texture counts, vertex
  ///     total and a CPU-side byte estimate of its mesh data;
  ///   * the process totals - Unity's own allocated/reserved memory, the mono
  ///     heap, and the load's wall-clock time;
  ///   * the invariants the fix must NOT change: every JSON actor placed, its
  ///     own type's count, a live skinned renderer with a mesh, and the decoded
  ///     position/facing/height applied (per type, first actor).
  ///
  /// The sharing check is a real gate: it FAILS on the per-actor build (the
  /// measured before-state, kept as evidence) and must pass after the fix.
  /// </summary>
  public static class S2MEntityMemoryRun
  {
    /// <summary>The install the meshes and clips come from.</summary>
    private static string GameRoot => Constants.GamePath;

    /// <summary>The dev harness's own map - the one run 2 of the flow UAT loads.</summary>
    private static string EntitiesJson =>
      Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter8.json");

    /// <summary>war_chapter8's documented placement count.</summary>
    private const int ExpectedActors = 340;

    /// <summary>Position/facing tolerance: the loader's own placements are exact
    /// to well under this (the JSON carries the tool's already-decoded numbers).</summary>
    private const float Tolerance = 1e-4f;

    private static readonly StringBuilder Report = new();
    private static int failures;

    public static void Main()
    {
      string log = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(log))
        log = Path.Combine(Path.GetTempPath(), "sh2_entity_memory_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        UnityEngine.Debug.LogError($"[S2MEntityMemoryRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      UnityEngine.Debug.Log("[S2MEntityMemoryRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      Line($"unity        : {Application.unityVersion}, {Application.platform}, "
           + $"batchmode={Application.isBatchMode}");
      Line($"entities     : {Path.GetFileName(EntitiesJson)}");
      Line("");

      // A clean floor: the harnesses share a process, and an earlier load's
      // objects would otherwise be counted as this load's.
      S2MPlacementHarness.DestroyAll();
      GC.Collect();
      GC.WaitForPendingFinalizers();
      GC.Collect();
      Resources.UnloadUnusedAssets();

      var before = TakeSnapshot();

      var stopwatch = Stopwatch.StartNew();
      GameObject root = S2MPlacementHarness.LoadEntities(EntitiesJson, GameRoot);
      stopwatch.Stop();

      var after = TakeSnapshot();

      Line($"load time    : {stopwatch.ElapsedMilliseconds} ms");
      Line("");
      Line("--- process totals (delta of this load) ---");
      Line($"memory       : allocated {Mb(after.Allocated - before.Allocated)} "
           + $"(total {Mb(after.Allocated)}), reserved {Mb(after.Reserved - before.Reserved)} "
           + $"(total {Mb(after.Reserved)})");
      Line($"heap         : mono {Mb(after.Mono)} (delta {Mb(after.Mono - before.Mono)}), "
           + $"gc {Mb(after.Gc)} (delta {Mb(after.Gc - before.Gc)})");
      Line($"objects      : Mesh {after.Meshes - before.Meshes}, "
           + $"Material {after.Materials - before.Materials}, "
           + $"Texture2D {after.Textures - before.Textures}, "
           + $"AnimationClip {after.Clips - before.Clips}, "
           + $"Animation {after.Animations - before.Animations}, "
           + $"SkinnedMeshRenderer {after.SkinnedRenderers - before.SkinnedRenderers}, "
           + $"GameObject {after.GameObjects - before.GameObjects}");
      Line("");

      // ---- the per-group table and the sharing ratio -------------------------
      var groups = new List<GroupCounts>();
      int actorCount = 0;
      var meshRefs = new Dictionary<Mesh, int>();
      var materialRefs = new Dictionary<Material, int>();
      var textureRefs = new Dictionary<Texture, int>();
      var perType = new SortedDictionary<string, int>(StringComparer.Ordinal);
      var firstOfType = new Dictionary<string, Transform>();

      if (root == null)
      {
        Check(false, "the entity loader returned a root");
        return;
      }

      foreach (Transform group in root.transform)
      {
        var counts = new GroupCounts { Name = group.name };
        var groupMeshes = new HashSet<Mesh>();
        var groupMaterials = new HashSet<Material>();
        var groupTextures = new HashSet<Texture>();
        foreach (Transform actor in group)
        {
          counts.Actors++;
          actorCount++;
          string type = TypeOf(actor.name);
          perType[type] = perType.TryGetValue(type, out int seen) ? seen + 1 : 1;
          if (!firstOfType.ContainsKey(type)) firstOfType[type] = actor;

          foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
          {
            Material[] materials = renderer.sharedMaterials;
            counts.MaterialRefs += materials.Length;
            foreach (Material material in materials)
            {
              if (material == null) continue;
              counts.MaterialIds.Add(material);
              groupMaterials.Add(material);
              Touch(materialRefs, material);
              Texture texture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap") : material.mainTexture;
              if (texture == null) continue;
              counts.TextureIds.Add(texture);
              groupTextures.Add(texture);
              Touch(textureRefs, texture);
            }

            Mesh mesh = renderer is SkinnedMeshRenderer skinned
              ? skinned.sharedMesh
              : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            counts.MeshRefs++;
            counts.Vertices += mesh.vertexCount;
            counts.MeshBytes += MeshBytes(mesh);
            counts.MeshIds.Add(mesh);
            groupMeshes.Add(mesh);
            Touch(meshRefs, mesh);
          }
        }
        counts.DistinctMeshes = groupMeshes.Count;
        counts.DistinctMaterials = groupMaterials.Count;
        counts.DistinctTextures = groupTextures.Count;
        groups.Add(counts);
      }

      Line("--- per model group (the loader's own grouping) ---");
      Line($"{"group",-34} {"actors",7} {"meshes",8} {"verts",8} {"mesh MB",8} "
           + $"{"mats",5} {"texs",5} {"skinned",8}");
      foreach (GroupCounts counts in groups)
      {
        Line($"{Trim(counts.Name, 34),-34} {counts.Actors,7} "
             + $"{counts.MeshRefs}/{counts.DistinctMeshes,-5} {counts.Vertices,8} "
             + $"{counts.MeshBytes / 1048576.0,8:F2} {counts.DistinctMaterials,5} "
             + $"{counts.DistinctTextures,5} {counts.Actors,8}");
      }
      Line($"{"TOTAL",-34} {actorCount,7} "
           + $"{Count(meshRefs):0}/{meshRefs.Count} {Total(groups, g => g.Vertices),8} "
           + $"{Total(groups, g => g.MeshBytes) / 1048576.0,8:F2} {materialRefs.Count,5} "
           + $"{textureRefs.Count,5} {actorCount,8}");
      Line("");
      Line("--- the sharing question (references vs distinct objects) ---");
      Line($"meshes       : {Count(meshRefs)} references over {meshRefs.Count} distinct "
           + $"({Ratio(meshRefs)}x) - {groups.Count} model groups");
      Line($"materials    : {Count(materialRefs)} references over {materialRefs.Count} distinct "
           + $"({Ratio(materialRefs)}x)");
      Line($"textures     : {Count(textureRefs)} references over {textureRefs.Count} distinct "
           + $"({Ratio(textureRefs)}x)");
      Line($"mesh data    : {Total(groups, g => g.MeshBytes) / 1048576.0:F2} MB on the CPU side "
           + $"(vertices + normals + uv + bone weights + indices + bindposes)");
      Line("");

      // WHERE the bytes are, by Unity's own per-object sizes over the DISTINCT
      // objects (which is the sharing metric: one image is charged once).
      Line("--- where the bytes are (Unity's own object sizes, distinct objects) ---");
      Line($"textures     : {Bytes(textureRefs.Keys)} MB over {textureRefs.Count} object(s)");
      Line($"meshes       : {Bytes(meshRefs.Keys)} MB over {meshRefs.Count} object(s)");
      Line($"materials    : {Bytes(materialRefs.Keys)} MB over {materialRefs.Count} object(s)");
      var clipSet = new HashSet<AnimationClip>();
      foreach (Animation animation in Resources.FindObjectsOfTypeAll<Animation>())
        foreach (AnimationState state in animation)
          if (state.clip != null) clipSet.Add(state.clip);
      Line($"clips        : {Bytes(clipSet)} MB over {clipSet.Count} AnimationClip object(s)");
      long pixelBytes = 0;
      int biggest = 0;
      string biggestName = "-";
      foreach (Texture texture in textureRefs.Keys)
      {
        if (texture is Texture2D image)
        {
          pixelBytes += (long)image.width * image.height * 4;
          if ((long)image.width * image.height > biggest)
          {
            biggest = image.width * image.height;
            biggestName = $"{image.name} {image.width}x{image.height}";
          }
        }
      }
      Line($"texture px   : {pixelBytes / 1048576.0:F1} MB of pixels decoded "
           + $"(largest: {biggestName})");
      Line("");

      // ---- the invariants the fix must not change ---------------------------
      Line("--- invariants (must hold before AND after the fix) ---");
      Check(actorCount == ExpectedActors,
        $"war_chapter8 places its {ExpectedActors} actors (found {actorCount})");

      var expected = ReadExpected();
      foreach (var pair in expected.Counts)
      {
        Check(perType.TryGetValue(pair.Key, out int placed) && placed == pair.Value,
          $"'{pair.Key}' places its {pair.Value} actor(s) "
          + $"(found {(perType.TryGetValue(pair.Key, out int got) ? got : 0)})");
      }

      int withoutMesh = 0;
      foreach (Transform group in root.transform)
        foreach (Transform actor in group)
          if (actor.GetComponentInChildren<SkinnedMeshRenderer>(true) == null
              || actor.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh == null)
            withoutMesh++;
      Check(withoutMesh == 0,
        $"every actor carries a skinned renderer with a mesh ({withoutMesh} without)");

      var bad = new List<string>();
      foreach (var pair in expected.First)
      {
        if (!firstOfType.TryGetValue(pair.Key, out Transform actor))
        {
          bad.Add($"{pair.Key}: not placed");
          continue;
        }
        var want = new Vector3(pair.Value.x * S2MWorldScale.MetresPerCell,
                               pair.Value.y_raw * S2MWorldScale.HeightUnitScale,
                               pair.Value.z * S2MWorldScale.MetresPerCell);
        Vector3 got = actor.localPosition;
        if ((got - want).magnitude > Tolerance)
          bad.Add($"{pair.Key}: ({got.x:F4}, {got.y:F4}, {got.z:F4}) vs "
                  + $"({want.x:F4}, {want.y:F4}, {want.z:F4})");
        float yaw = (actor.localRotation.eulerAngles.y - pair.Value.rot) % 360f;
        if (Mathf.Abs(Mathf.DeltaAngle(actor.localRotation.eulerAngles.y, pair.Value.rot)) > 0.01f)
          bad.Add($"{pair.Key}: yaw {yaw:F2} vs {pair.Value.rot:F2}");
      }
      Check(bad.Count == 0,
        $"each type's first actor sits at its decoded position and facing "
        + $"({bad.Count} off{First(bad)})");

      // The gate: one Mesh object per model-mesh, not one per actor. The
      // threshold is derived from the data, not hardcoded - a model file that
      // carries several sub-meshes (the Lord has its weapon meshes) is allowed
      // for, an actor's own copy is not.
      int modelFiles = groups.Count;
      Check(meshRefs.Count <= modelFiles * 8,
        $"the actors SHARE their mesh data: {meshRefs.Count} distinct Mesh object(s) "
        + $"for {actorCount} actors from {modelFiles} model files "
        + $"({Count(meshRefs)} references, {Ratio(meshRefs)}x duplication)");
      Check(materialRefs.Count <= modelFiles * 8,
        $"and their materials: {materialRefs.Count} distinct Material object(s) "
        + $"({Count(materialRefs)} references, {Ratio(materialRefs)}x)");
      Check(textureRefs.Count <= modelFiles * 8,
        $"and their textures: {textureRefs.Count} distinct Texture2D object(s) "
        + $"({Count(textureRefs)} references, {Ratio(textureRefs)}x)");
      Line("");
    }

    /// <summary>One model group's counts, for the table.</summary>
    private class GroupCounts
    {
      public string Name;
      public int Actors;
      public int MeshRefs;
      public int DistinctMeshes;
      public int Vertices;
      public long MeshBytes;
      public int MaterialRefs;
      public int DistinctMaterials;
      public int DistinctTextures;
      public readonly HashSet<Mesh> MeshIds = new();
      public readonly HashSet<Material> MaterialIds = new();
      public readonly HashSet<Texture> TextureIds = new();
    }

    /// <summary>CPU-side size of one mesh, in bytes: the arrays a per-actor build
    /// keeps alive (positions, normals, uv, bone weights, indices, bindposes).
    /// GPU copies are not visible from here - the process totals cover those.</summary>
    private static long MeshBytes(Mesh mesh)
    {
      long bytes = (long)mesh.vertexCount * (12 + 12 + 8 + 32);
      for (int sub = 0; sub < mesh.subMeshCount; sub++)
        bytes += (long)mesh.GetIndexCount(sub) * (mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32 ? 4 : 2);
      bytes += (long)(mesh.bindposes?.Length ?? 0) * 64;
      return bytes;
    }

    private readonly struct Snapshot
    {
      public readonly long Allocated;
      public readonly long Reserved;
      public readonly long Mono;
      public readonly long Gc;
      public readonly int Meshes;
      public readonly int Materials;
      public readonly int Textures;
      public readonly int Clips;
      public readonly int Animations;
      public readonly int SkinnedRenderers;
      public readonly int GameObjects;

      public Snapshot(long allocated, long reserved, long mono, long gc, int meshes,
        int materials, int textures, int clips, int animations, int skinned, int gameObjects)
      {
        Allocated = allocated; Reserved = reserved; Mono = mono; Gc = gc;
        Meshes = meshes; Materials = materials; Textures = textures; Clips = clips;
        Animations = animations; SkinnedRenderers = skinned; GameObjects = gameObjects;
      }
    }

    private static Snapshot TakeSnapshot() => new(
      Profiler.GetTotalAllocatedMemoryLong(),
      Profiler.GetTotalReservedMemoryLong(),
      Profiler.GetMonoHeapSizeLong(),
      GC.GetTotalMemory(false),
      Resources.FindObjectsOfTypeAll<Mesh>().Length,
      Resources.FindObjectsOfTypeAll<Material>().Length,
      Resources.FindObjectsOfTypeAll<Texture2D>().Length,
      Resources.FindObjectsOfTypeAll<AnimationClip>().Length,
      Resources.FindObjectsOfTypeAll<Animation>().Length,
      Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>().Length,
      Resources.FindObjectsOfTypeAll<GameObject>().Length);

    // ---- the JSON's own view of the map, for the invariant checks ----------

    [Serializable]
    private class Record
    {
      public string type;
      public float x;
      public float y_raw;
      public float z;
      public float rot;
    }

    [Serializable]
    private class EntityFile
    {
      public List<Record> entities;
    }

    private readonly struct Expected
    {
      public readonly SortedDictionary<string, int> Counts;
      public readonly SortedDictionary<string, Record> First;
      public Expected(SortedDictionary<string, int> counts, SortedDictionary<string, Record> first)
      { Counts = counts; First = first; }
    }

    private static Expected ReadExpected()
    {
      var file = JsonUtility.FromJson<EntityFile>(System.IO.File.ReadAllText(EntitiesJson));
      var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
      var first = new SortedDictionary<string, Record>(StringComparer.Ordinal);
      foreach (Record record in file.entities)
      {
        counts[record.type] = counts.TryGetValue(record.type, out int seen) ? seen + 1 : 1;
        if (!first.ContainsKey(record.type)) first[record.type] = record;
      }
      return new Expected(counts, first);
    }

    // ---- small helpers ----------------------------------------------------

    /// <summary>The actor's type: the loader names it "&lt;type&gt; &lt;index&gt;".</summary>
    private static string TypeOf(string name)
    {
      int space = name.LastIndexOf(' ');
      return space > 0 && int.TryParse(name.Substring(space + 1), out _)
        ? name.Substring(0, space) : name;
    }

    private static void Touch<T>(Dictionary<T, int> counts, T id) where T : UnityEngine.Object =>
      counts[id] = counts.TryGetValue(id, out int seen) ? seen + 1 : 1;

    private static int Count<T>(Dictionary<T, int> counts) where T : UnityEngine.Object
    {
      int total = 0;
      foreach (int value in counts.Values) total += value;
      return total;
    }

    private static string Ratio<T>(Dictionary<T, int> counts) where T : UnityEngine.Object =>
      counts.Count == 0 ? "-" : (Count(counts) / (double)counts.Count).ToString("F1");

    private static double Total(List<GroupCounts> groups, Func<GroupCounts, long> pick)
    {
      long total = 0;
      foreach (GroupCounts group in groups) total += pick(group);
      return total;
    }

    private static string Mb(long bytes) => $"{bytes / 1048576.0:F1} MB";

    /// <summary>Unity's own size for a set of objects, in MB.</summary>
    private static string Bytes(IEnumerable<UnityEngine.Object> objects)
    {
      long total = 0;
      foreach (UnityEngine.Object value in objects)
        total += Profiler.GetRuntimeMemorySizeLong(value);
      return $"{total / 1048576.0:F1}";
    }

    private static string Trim(string value, int max) =>
      value.Length <= max ? value : value.Substring(0, max);

    private static string First(List<string> items) => items.Count == 0 ? "" : $": {items[0]}";

    private static void Check(bool passed, string description)
    {
      Line($"{(passed ? "PASS" : "FAIL")}         : {description}");
      if (!passed) failures++;
    }

    private static void Line(string text) => Report.AppendLine(text);
  }
}
