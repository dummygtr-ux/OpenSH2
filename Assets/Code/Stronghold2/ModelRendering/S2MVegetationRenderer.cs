using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>Renders already loaded vegetation data without reading map or GR2 files.</summary>
  public static class S2MVegetationRenderer
  {
    public static GameObject Render(
      S2MVegetationData vegetation,
      Transform parent = null,
      S2MVegetationRenderSettings settings = null,
      string objectName = "S2M Vegetation")
    {
      if (vegetation == null) throw new ArgumentNullException(nameof(vegetation));
      settings ??= new S2MVegetationRenderSettings();
      if (settings.AssetScaleMultiplier <= 0f)
      {
        throw new ArgumentOutOfRangeException(
          nameof(settings),
          "AssetScaleMultiplier must be greater than zero.");
      }
      var root = new GameObject(objectName);
      if (parent != null) root.transform.SetParent(parent, false);

      foreach (S2MVegetationBatch batch in vegetation.Batches)
      {
        var transforms = new List<Granny2ModelInstance>(batch.Instances.Count);
        for (int i = 0; i < batch.Instances.Count; i++)
        {
          ForestInstance source = batch.Instances[i];
          transforms.Add(ToUnityInstance(source, vegetation.Map, settings, i));
        }
        Granny2ModelRenderer.RenderInstances(
          batch.Model,
          transforms,
          root.transform,
          settings.Model,
          batch.AssetRelativePath.Replace('\\', '/'));
      }
      return root;
    }

    private static Granny2ModelInstance ToUnityInstance(
      ForestInstance source,
      S2MFile map,
      S2MVegetationRenderSettings settings,
      int index)
    {
      float x = source.MapX;
      float z = source.MapZ;
      float y = GetTerrainSurfaceHeight(map, x, z, settings);
      float yaw = source.RotationDegrees;
      int width = map?.HeightLayer?.Width ?? map?.MapSize ?? 0;
      int height = map?.HeightLayer?.Height ?? map?.MapSize ?? 0;
      if (settings.FlipX && width > 0) { x = width - x; yaw = 180f - yaw; }
      if (settings.FlipZ && height > 0) { z = height - z; yaw = -yaw; }

      float appearanceScale = settings.ApplyAppearanceScale && source.AppearanceScale > 0f
        ? source.AppearanceScale
        : 1f;
      var scale = new Vector3(source.ScaleX, source.ScaleY, source.ScaleX)
        * appearanceScale
        * settings.AssetScaleMultiplier;
      return new Granny2ModelInstance(
        $"Tree Family {source.Family} Variant {source.Variant} ({index})",
        new Vector3(x * settings.HorizontalCellSize, y, z * settings.HorizontalCellSize),
        new Vector3(0f, yaw, 0f),
        scale);
    }

    /// <summary>
    /// World height of the rendered terrain surface at a fractional map cell -
    /// the bilinear interpolation of the HeightLayer plane-0 corner heights,
    /// i.e. exactly the surface S2MTerrainRenderer builds its meshes from, so a
    /// tree trunk meets the ground it appears to stand on. The forest record's
    /// own "y" float is NOT a height (ForestInstance.RawY) and is not used.
    /// </summary>
    private static float GetTerrainSurfaceHeight(
      S2MFile map,
      float cellX,
      float cellZ,
      S2MVegetationRenderSettings settings)
    {
      HeightLayer heightLayer = map?.HeightLayer;
      if (heightLayer == null || settings.HeightUnitScale <= 0f) return 0f;
      int x = Mathf.Clamp(Mathf.FloorToInt(cellX), 0, heightLayer.Width - 1);
      int z = Mathf.Clamp(Mathf.FloorToInt(cellZ), 0, heightLayer.Height - 1);
      float fractionX = Mathf.Clamp01(cellX - x);
      float fractionZ = Mathf.Clamp01(cellZ - z);
      int cellIndex = z * heightLayer.Width + x;

      float cornerSouthWest, cornerSouthEast, cornerNorthWest, cornerNorthEast;
      if (heightLayer.CornerHeightPlanes?.Length > 0
        && heightLayer.CornerHeightPlanes[0]?.Length
          == heightLayer.Width * heightLayer.Height * HeightLayer.CornersPerCell)
      {
        // Serialized corner order: slot 2 = (x, z), 3 = (x+1, z), 0 = (x, z+1),
        // 1 = (x+1, z+1) - see HeightLayer.GetCornerHeight.
        cornerSouthWest = heightLayer.GetCornerHeight(0, cellIndex, 2);
        cornerSouthEast = heightLayer.GetCornerHeight(0, cellIndex, 3);
        cornerNorthWest = heightLayer.GetCornerHeight(0, cellIndex, 0);
        cornerNorthEast = heightLayer.GetCornerHeight(0, cellIndex, 1);
      }
      else
      {
        float primary = heightLayer.Heights != null && cellIndex < heightLayer.Heights.Length
          ? heightLayer.Heights[cellIndex]
          : 0f;
        cornerSouthWest = cornerSouthEast = cornerNorthWest = cornerNorthEast = primary;
      }

      float south = Mathf.Lerp(cornerSouthWest, cornerSouthEast, fractionX);
      float north = Mathf.Lerp(cornerNorthWest, cornerNorthEast, fractionX);
      return Mathf.Lerp(south, north, fractionZ) * settings.HeightUnitScale;
    }
  }
}
