using System;
using System.Collections.Generic;
using System.IO;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.ModelRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.Buildings
{
  /// <summary>
  /// Renders placed buildings from an extracted placements JSON produced by the
  /// sh2-port building tools. Coordinates are map CELLS (raw game units / 1024;
  /// x = raw@140, z = raw@148, ground height = raw@144) and are scaled to
  /// metres with S2MWorldScale.MetresPerCell, so the models' authored
  /// centimetre sizes (UnitScale 0.01) match the terrain and the wall renderer.
  /// Rotation = raw@152, f32 degrees (observed values 0/90/180/270). Models load
  /// through the Granny2 cache-first path (meshes/buildings/*.gr2, or
  /// meshes/*.gr2 when the model name carries a directory, e.g.
  /// "siege_equipment/brazier").
  ///
  /// The JSON also carries a "props" array: wall/floor-mounted objects that own
  /// no BuildingLayer footprint and use the ENTITY layout (x@29, y@33 = the
  /// surface they stand on, z@37) - braziers on wall tops and tower decks.
  /// Props render like buildings but are deliberately NOT part of
  /// ReadPlacementCells: the wall renderer's model-covered rule is about
  /// footprint-owning structures only.
  ///
  /// "scenery" (2026-10-08) = the NaturalStone rock
  /// records: one record per rock, {x, y, z, rot, model, variant} with y
  /// already sampled from the HeightLayer plane-0 corner surface by
  /// sh2-port/tools/stage_fields.py --emit-stages (the record itself carries no
  /// height, only x, z and a +-45 deg tilt). The variant byte (record offset 12)
  /// resolves to rock_natural_stone_A1..F2 in the exe table's order - see
  /// FORMATS.md "Scenery". Rendered like props, excluded from
  /// ReadPlacementCells (rocks own no footprint).
  /// </summary>
  internal static class BuildingPlacementLoader
  {
    [Serializable]
    private class Placement
    {
      public string type;
      public string model;
      public float x;
      public float y;
      public float z;
      public float rot;
    }

    [Serializable]
    private class PlacementFile
    {
      public string map;
      public List<Placement> buildings;
      public List<Placement> props;
      public List<Placement> scenery;
    }

    public static GameObject Load(string placementsJsonPath, string gameInstallPath, Transform parent)
    {
      var file = ReadPlacements(placementsJsonPath);
      var all = new List<Placement>(file.buildings);
      if (file.props != null) all.AddRange(file.props);
      var scenery = file.scenery ?? new List<Placement>();

      var byModel = new Dictionary<string, List<Placement>>();
      foreach (var placement in all)
      {
        if (string.IsNullOrWhiteSpace(placement.model)) continue;
        if (!byModel.TryGetValue(placement.model, out var list))
          byModel[placement.model] = list = new List<Placement>();
        list.Add(placement);
      }
      // Scenery renders through the same path but lives in its own root, so a
      // capture can hide/show the rocks independently of the buildings.
      var sceneryByModel = new Dictionary<string, List<Placement>>();
      foreach (var placement in scenery)
      {
        if (string.IsNullOrWhiteSpace(placement.model)) continue;
        if (!sceneryByModel.TryGetValue(placement.model, out var list))
          sceneryByModel[placement.model] = list = new List<Placement>();
        list.Add(placement);
      }

      var root = new GameObject("S2M Buildings");
      if (parent != null) root.transform.SetParent(parent, false);
      var sceneryRoot = new GameObject("S2M Scenery");
      if (parent != null) sceneryRoot.transform.SetParent(parent, false);

      Render(sceneryByModel, gameInstallPath, sceneryRoot.transform, "scenery");
      Render(byModel, gameInstallPath, root.transform, "building");
      Debug.Log($"Placed {all.Count} buildings/props + {scenery.Count} scenery "
        + $"from {Path.GetFileName(placementsJsonPath)}"
        + $" ({file.buildings.Count} buildings + {(file.props?.Count ?? 0)} props).");
      return root;
    }

    private static void Render(Dictionary<string, List<Placement>> byModel,
                               string gameInstallPath, Transform parent, string kind)
    {
      foreach (var pair in byModel)
      {
        string relativePath = pair.Key.Contains("/")
          ? $"meshes/{pair.Key}.gr2"
          : $"meshes/buildings/{pair.Key}.gr2";
        try
        {
          var model = Granny2AssetConverter.LoadOrConvert(gameInstallPath, relativePath);
          var instances = new List<Granny2ModelInstance>();
          int index = 0;
          foreach (var placement in pair.Value)
          {
            // JSON coordinates are cells (x/z) and cells of height (y = raw@144
            // / 1024); the scene is in metres at 2 m per cell.
            instances.Add(new Granny2ModelInstance(
              $"{pair.Key} {index++}",
              new Vector3(placement.x, placement.y, placement.z) * S2MWorldScale.MetresPerCell,
              new Vector3(0f, placement.rot, 0f),
              Vector3.one));
          }
          Granny2ModelRenderer.RenderInstances(model, instances, parent,
            new Granny2ModelRenderSettings { AlphaTestMode = Gr2AlphaTestMode.AlphaTestSuffix },
            pair.Key);
        }
        catch (Exception exception)
        {
          Debug.LogWarning($"{(kind == "scenery" ? "Scenery" : "Buildings")} "
            + $"'{pair.Key}' skipped: {exception.Message}");
        }
      }
    }

    /// <summary>
    /// Map cells (x, z) of the BUILDING placements in an extracted placements
    /// JSON - footprint-owning structures only. Callers hand these to
    /// S2MWallRenderer so it can tell model-covered BuildingLayer footprints
    /// from structures the scene does not render (props, unmapped types).
    /// </summary>
    public static Vector2Int[] ReadPlacementCells(string placementsJsonPath)
    {
      var file = ReadPlacements(placementsJsonPath);
      var cells = new Vector2Int[file.buildings.Count];
      for (int i = 0; i < file.buildings.Count; i++)
      {
        var placement = file.buildings[i];
        cells[i] = new Vector2Int(Mathf.RoundToInt(placement.x), Mathf.RoundToInt(placement.z));
      }
      return cells;
    }

    private static PlacementFile ReadPlacements(string placementsJsonPath)
    {
      var file = JsonUtility.FromJson<PlacementFile>(File.ReadAllText(placementsJsonPath));
      if (file?.buildings == null || file.buildings.Count == 0)
        throw new InvalidDataException($"No building placements found in {placementsJsonPath}.");
      return file;
    }
  }
}
