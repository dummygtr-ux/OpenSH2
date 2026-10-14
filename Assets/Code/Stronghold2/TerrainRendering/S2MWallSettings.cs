using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public sealed class S2MWallSettings
  {
    [Min(0.0001f)]
    [Tooltip("Unity world units per S2M map cell: 2 = metres (see S2MWorldScale). Must match S2MTerrainSettings so walls align with the terrain mesh.")]
    public float HorizontalCellSize = S2MWorldScale.MetresPerCell;

    [Min(0.000001f)]
    [Tooltip("Unity vertical units per raw S2M height unit (metres = raw/512, see S2MWorldScale). Must match S2MTerrainSettings so wall tops land at the decoded plane heights.")]
    public float HeightUnitScale = S2MWorldScale.HeightUnitScale;

    [Tooltip("Reverse the serialized X axis. Must match S2MTerrainSettings.")]
    public bool FlipX;

    [Tooltip("Reverse the serialized row axis. Must match S2MTerrainSettings.")]
    public bool FlipZ;

    [Tooltip("Root of the legally owned Stronghold 2 installation. Wall textures load from it.")]
    public string GameInstallPath;

    [Tooltip("Map cells of the building placements the scene renders as models. A " +
      "BuildingLayer instance whose footprint contains one of these cells (bbox + 1) is " +
      "model-covered and its cells are skipped; instances WITHOUT a model (props, and " +
      "structure types the placement extractor does not map - war_chapter8's Keep1/2/3, " +
      "TowerLookout, watch posts, braziers) are still classified, or their wall runs would " +
      "have holes and their walk decks would be missing. Empty = skip every BuildingLayer " +
      "footprint (pre-2026-10-08 behaviour).")]
    public Vector2Int[] RenderedBuildingCells;

    [Tooltip("Stone wall side texture, relative to the game installation.")]
    public string StoneSideTexturePath = "meshes/buildings/castle_wall.dds";

    [Tooltip("Stone wall and deck walkway top texture, relative to the game installation.")]
    public string StoneTopTexturePath = "meshes/buildings/walltop.dds";

    [Tooltip("Wooden palisade side texture, relative to the game installation.")]
    public string WoodenSideTexturePath = "meshes/buildings/wood_walls.dds";

    [Tooltip("Wooden palisade top texture, relative to the game installation.")]
    public string WoodenTopTexturePath = "meshes/buildings/walltop_wood.dds";

    [Tooltip("Optional material overrides for generated wall geometry.")]
    public Material StoneSideMaterial;
    public Material StoneTopMaterial;
    public Material WoodenSideMaterial;
    public Material WoodenTopMaterial;
  }
}
