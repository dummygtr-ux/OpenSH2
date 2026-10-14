using Assets.Code.Video;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Code.Caching
{
  /// <summary>
  /// Responsible for running conversion processes on original Stronghold 2 assets
  /// and caching them for use in the OpenSH2 engine.
  /// </summary>
  class CacheManager
  {
    public Task assetLoadingTask = null;

    /// <summary>
    /// Runs conversion and caching on any video assets that are needed.
    /// 
    /// Yields when a conversion begins and when that asset is finished.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<CacheManagerAssetStatus> ConvertAndCacheVideoAssets()
    {
      foreach (var asset in VideoFilePaths.CachedVideoOutputFormats)
      {
        string assetFilePath = asset.Key;
        // The format comes from the SAME lookup the players resolve their cache
        // path with. The dictionary's own per-file value is upstream's Windows
        // table, and on Linux every video is WebM (the Linux VideoPlayer
        // rejects H.264 MP4) - so writing asset.Value here cached files no
        // player ever asked for, and the intro/menu videos were silently
        // "not found" behind it.
        string outputFormat = VideoFilePaths.GetCachedVideoOutputFormat(assetFilePath);
        string basename = System.IO.Path.GetFileNameWithoutExtension(assetFilePath);
        string cachedVideoPath = Constants.VideoCachePath + "/" + basename + "." + outputFormat;

        if (System.IO.File.Exists(cachedVideoPath))
        {
          Debug.Log($"Asset {assetFilePath} already cached, skipping conversion.");
          continue;
        }

        yield return new CacheManagerAssetStatus
        {
          AssetPath = assetFilePath,
          IsFinished = false
        };

        try
        {
          Converter.Convert(assetFilePath, cachedVideoPath, outputFormat);
          Debug.Log($"Finished converting and caching asset {assetFilePath} ({outputFormat}).");
        }
        catch (Exception ex)
        {
          Debug.LogError($"Error converting asset {assetFilePath}: {ex}");
          continue;
        }

        yield return new CacheManagerAssetStatus
        {
          AssetPath = assetFilePath,
          IsFinished = true
        };
      }
    }
  }

  struct CacheManagerAssetStatus
  {
    public string AssetPath { get; set; }
    public bool IsFinished { get; set; }
  }
}
