using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.MapLoading
{
  /// <summary>
  /// Builds one map's whole renderable world - terrain and its textures, water,
  /// walls, vegetation, buildings and placed actors - from a .s2m file plus the
  /// port's two per-map data files (`buildings_&lt;stem&gt;.json` and
  /// `entities_&lt;stem&gt;.json` under sh2-port/artifacts, written by the python
  /// tools). It is THE map-world load: the dev-harness boot
  /// (Main.RunTests) and the game flow's chapter start (Main.TickGameFlow,
  /// GameState.MAP_LOADING) both call it, so a chapter the player clicks loads
  /// through exactly the loaders the extraction work verified.
  ///
  /// The loader logs what it built and keeps the last load's result
  /// (<see cref="Last"/>), so a harness can read the loaded map's own numbers
  /// without loading the file a second time. Exceptions propagate - a load that
  /// cannot finish is the caller's to report (Main.RunTests catches and logs it;
  /// the flow's state lets the run's budget fail loudly).
  /// </summary>
  internal static class S2MMapWorldLoader
  {
    /// <summary>One load's result: the loaded file, its world root, and what the renderers report.</summary>
    public sealed class LoadedMap
    {
      public S2MFile MapFile;
      public GameObject Root;
      public int VegetationInstances;
      public int UnsupportedVegetationRecords;
      public S2MWallRenderer.S2MWallCounts Walls;
    }

    /// <summary>The last load's result - what Main's flow and the UAT read.</summary>
    public static LoadedMap Last { get; private set; }

    /// <summary>
    /// Loads the map and builds its world under <paramref name="parent"/> (scaled
    /// by <paramref name="gameScale"/>, the scene's own uniform scale). The two
    /// data files must exist: the port's placement data is not yet read from the
    /// map file itself (still remaining work), so a missing JSON throws
    /// where the python tools' naming is resolved.
    /// </summary>
    public static LoadedMap Load(string mapPath, string gameInstallPath,
                                 string placementsPath, string entitiesPath,
                                 Transform parent, float gameScale,
                                 S2MVegetationRenderSettings vegetationSettings)
    {
      var mapFile = S2MFileLoader.Load(mapPath);

      S2MTerrainTextureData terrainTextures = null;
      try
      {
        terrainTextures = new S2MTerrainTextureAssetLoader().Load(mapFile, gameInstallPath);
      }
      catch (System.Exception textureError)
      {
        // Some maps (e.g. The Four Woods kingmaker) lack decodable Landscape
        // surface-material IDs. Render untextured rather than aborting the map.
        Debug.LogWarning($"Terrain textures unavailable, rendering untextured: {textureError.Message}");
      }

      if (gameScale <= 0f) throw new System.InvalidOperationException("Game scale must be positive.");
      var mapWorld = new GameObject("S2M Map World");
      mapWorld.transform.SetParent(parent, worldPositionStays: false);
      mapWorld.transform.localScale = Vector3.one * gameScale;

      GameObject terrain = S2MTerrainRenderer.Render(
        mapFile,
        mapWorld.transform,
        new()
        {
          UsePerCellCornerHeights = true,
        }
        );
      if (terrainTextures != null)
      {
        S2MTerrainTextureRenderer.Apply(terrain, terrainTextures, new()
        {
          AdjacentMaterialBlendWidth = 0.5f
        });
      }

      GameObject water = S2MWaterRenderer.Render(mapFile, mapWorld.transform, new()
      {
        GameInstallPath = gameInstallPath
      });
      // Buildings load after the walls, but the wall classifier must know which
      // BuildingLayer footprints a model will cover (see S2MWallSettings).
      S2MWallRenderer.Render(mapFile, mapWorld.transform, new()
      {
        GameInstallPath = gameInstallPath,
        RenderedBuildingCells = BuildingPlacementLoader.ReadPlacementCells(placementsPath)
      });
      S2MVegetationData vegetationData = new S2MVegetationAssetLoader().Load(
        mapFile,
        gameInstallPath);
      GameObject vegetation = S2MVegetationRenderer.Render(
        vegetationData,
        mapWorld.transform,
        vegetationSettings);

      BuildingPlacementLoader.Load(placementsPath, gameInstallPath, mapWorld.transform);

      GameObject entitiesRoot = EntityPlacementLoader.Load(
        entitiesPath,
        gameInstallPath, mapWorld.transform);

      // Every placed actor gets a movement driver, so the whole roster is
      // orderable through the verified S2MActorStateDriver path.
      var actorCommander = new GameObject("S2M Actor Commander");
      actorCommander.AddComponent<S2MActorCommander>().Initialize(entitiesRoot, gameInstallPath);

      Debug.Log($"Read S2M header: author={mapFile.Author}, type={mapFile.MapType}, balanced={mapFile.Balanced}, maxPlayers={mapFile.MaxPlayers}, version={mapFile.Version}; decoded {mapFile.Forest?.Instances.Count ?? 0} Forest records, rendered {vegetationData.SupportedInstanceCount} trees ({vegetationData.UnsupportedInstanceCount} unsupported records).");

      Last = new LoadedMap
      {
        MapFile = mapFile,
        Root = mapWorld,
        VegetationInstances = vegetationData.SupportedInstanceCount,
        UnsupportedVegetationRecords = vegetationData.UnsupportedInstanceCount,
        Walls = S2MWallRenderer.LastCounts,
      };
      return Last;
    }
  }
}
