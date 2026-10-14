using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  [Serializable]
  public class S2MTerrainSettings
  {
    [Min(0.0001f)]
    [Tooltip("Unity world units per S2M map cell: 2 = the scene is in metres (1 cell = 2 m, see S2MWorldScale). Must match every other map renderer so the layers stay aligned.")]
    public float HorizontalCellSize = S2MWorldScale.MetresPerCell;

    [Tooltip("Unity vertical units per raw S2M height unit. Raw heights are 1024 per cell, so metres = raw/512 (see S2MWorldScale); 1/1024 would render terrain and walls at half height.")]
    [Min(0.000001f)]
    public float HeightUnitScale = S2MWorldScale.HeightUnitScale;

    [Tooltip("Reverse the serialized X axis when generating the terrain mesh.")]
    public bool FlipX;

    [Tooltip("Reverse the serialized row axis when generating Unity's Z axis.")]
    public bool FlipZ;

    [Tooltip("Use plane-0's four per-cell corner heights instead of collapsing the map to its primary grid.")]
    public bool UsePerCellCornerHeights = true;

    [Min(1)]
    [Tooltip("Number of terrain cells in each square render chunk.")]
    public int CellsPerChunk = 32;

    [Tooltip("Create a MeshCollider for every generated terrain chunk.")]
    public bool GenerateMeshColliders = true;

    [Tooltip("Optional runtime material override for generated terrain mesh chunks.")]
    public Material Material;
  }
}
