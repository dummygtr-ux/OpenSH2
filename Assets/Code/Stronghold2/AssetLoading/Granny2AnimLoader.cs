using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.AssetLoading
{
  /// <summary>
  /// Resolves an actor clip's game-relative path to its ".osh2anim" cache and
  /// reads it, mirroring <see cref="Granny2AssetConverter"/>'s cache lookup.
  ///
  /// Naming rule (identical to ".osh2mesh", so one lookup table covers both):
  ///   {basename}-{sha256(relpath.lower())[0:16]}.osh2anim
  /// in <c>Application.persistentDataPath/OpenSH2/Cache/Anims</c>. The hash is
  /// over the game-relative path, never over an absolute one - the extractor
  /// and this side run on different machines with different mount points.
  ///
  /// There is deliberately NO converter fallback here. The mesh path can call a
  /// Windows .NET exe because the game ships a Granny2 DLL the exe p/invokes;
  /// the clips are extracted by tools/gr2clip.exe, a wine32 build that only
  /// runs on the Linux extraction host. A missing cache is therefore a missing
  /// build step and says so, instead of trying to convert.
  /// </summary>
  public static class Granny2AnimLoader
  {
    public static string DefaultCacheDirectory => Path.Combine(
      Application.persistentDataPath, "OpenSH2", "Cache", "Anims");

    /// <summary>The cache file a clip's game-relative path maps to.</summary>
    public static string CachePathFor(string clipRelativePath, string cacheDirectory = null)
    {
      if (string.IsNullOrWhiteSpace(clipRelativePath))
        throw new ArgumentException("A game-relative .gr2 path is required.", nameof(clipRelativePath));
      cacheDirectory ??= DefaultCacheDirectory;
      return Path.Combine(cacheDirectory, CreateCacheName(clipRelativePath));
    }

    public static Granny2AnimData Load(string gameInstallPath, string clipRelativePath,
      string cacheDirectory = null)
    {
      ValidateInstallation(gameInstallPath);
      string cachePath = CachePathFor(clipRelativePath, cacheDirectory);
      if (!File.Exists(cachePath))
      {
        throw new FileNotFoundException(
          $"No animation cache for {clipRelativePath}. The clip caches are built on the "
          + "Linux extraction host with: WINEPREFIX=~/.wine-sh2-32 wine32 sh2-port/tools/gr2clip.exe "
          + "\"<game>/granny2.dll\" \"<clip>.gr2\" > clip.json && python3 sh2-port/tools/anim_to_cache.py "
          + $"clip.json --relative {clipRelativePath}", cachePath);
      }
      return Granny2AnimCacheReader.Load(cachePath);
    }

    public static bool HasCache(string clipRelativePath, string cacheDirectory = null) =>
      File.Exists(CachePathFor(clipRelativePath, cacheDirectory));

    private static void ValidateInstallation(string gameInstallPath)
    {
      string root = Path.GetFullPath(gameInstallPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      if (!File.Exists(Path.Combine(root, "Stronghold2.exe")) || !File.Exists(Path.Combine(root, "granny2.dll")))
        throw new DirectoryNotFoundException("A valid Stronghold 2 installation containing Stronghold2.exe and granny2.dll is required.");
    }

    private static string CreateCacheName(string relativePath)
    {
      using var sha = SHA256.Create();
      byte[] hash = sha.ComputeHash(
        Encoding.UTF8.GetBytes(relativePath.Replace('\\', '/').ToLowerInvariant()));
      string shortHash = BitConverter.ToString(hash, 0, 8).Replace("-", string.Empty).ToLowerInvariant();
      return $"{Path.GetFileNameWithoutExtension(relativePath)}-{shortHash}.osh2anim";
    }
  }
}
