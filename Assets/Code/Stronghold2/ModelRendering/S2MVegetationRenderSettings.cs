using System;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  [Serializable]
  public sealed class S2MVegetationRenderSettings
  {
    [Tooltip("Unity world units per S2M map cell: 2 = metres (see S2MWorldScale). Must match S2MTerrainSettings.")]
    [Min(0.0001f)] public float HorizontalCellSize = S2MWorldScale.MetresPerCell;
    [Tooltip("Unity vertical units per raw S2M height unit (metres = raw/512, see S2MWorldScale). Used for the terrain surface height a tree stands on.")]
    [Min(0.000001f)] public float HeightUnitScale = S2MWorldScale.HeightUnitScale;
    public bool FlipX;
    public bool FlipZ;
    public bool ApplyAppearanceScale = true;
    [Tooltip("Uniform multiplier for every GR2 model placed by the S2M map. Does not change map coordinates.")]
    [Min(0.000001f)] public float AssetScaleMultiplier = 1f;
    public Granny2ModelRenderSettings Model = new();
  }
}
