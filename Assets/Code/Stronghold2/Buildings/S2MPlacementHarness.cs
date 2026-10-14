using UnityEngine;

namespace Assets.Code.Stronghold2.Buildings
{
  /// <summary>
  /// Edit-mode entry point for the placement pipeline (dev harness).
  ///
  /// Main.cs drives the same loader in Play mode; this wrapper exists so an
  /// editor-side tool (script execution / SceneView capture) can load a
  /// placements JSON and inspect the created objects WITHOUT entering
  /// Play mode - the loader is internal, so an editor assembly cannot call it
  /// directly. Written 2026-10-08 to verify that the
  /// NaturalStone rock records (the JSON's "scenery" array, written by
  /// sh2-port/tools/stage_fields.py --emit-stages) land on their map cells with
  /// the model the variant byte selects.
  ///
  /// Call <see cref="DestroyAll"/> when done so the edit scene stays clean.
  /// </summary>
  public static class S2MPlacementHarness
  {
    public static GameObject LoadPlacements(string placementsJsonPath, string gameInstallPath)
    {
      return BuildingPlacementLoader.Load(placementsJsonPath, gameInstallPath, null);
    }

    /// <summary>
    /// Loads an entity JSON (sh2-port/tools/entity_parse.py --json) and creates
    /// the actor objects without entering Play mode - same reason as
    /// <see cref="LoadPlacements"/>: the loader is internal. The created root is
    /// "S2M Entities"; count its grandchildren for the placed-actor count (one
    /// GameObject per actor - a model may carry several meshes).
    /// </summary>
    public static GameObject LoadEntities(string entitiesJsonPath, string gameInstallPath)
    {
      return EntityPlacementLoader.Load(entitiesJsonPath, gameInstallPath, null);
    }

    public static void DestroyAll()
    {
      Object.DestroyImmediate(GameObject.Find("S2M Buildings"));
      Object.DestroyImmediate(GameObject.Find("S2M Scenery"));
      Object.DestroyImmediate(GameObject.Find("S2M Entities"));
    }
  }
}
