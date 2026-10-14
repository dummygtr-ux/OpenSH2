using System;
using System.IO;
using UnityEngine;

public class Constants
{
  /// <summary>
  /// Upstream's literal: the maintainer's Windows Steam install. Kept as the
  /// fallback so an upstream checkout resolves exactly as it always has.
  /// </summary>
  public const string DefaultGamePath = "C:/Steam/steamapps/common/Stronghold 2";

  /// <summary>
  /// The conventional Linux mirror of the Steam install: a
  /// case-corrected symlink tree at ~/sh2-game-root, resolved against the
  /// current user's home so the default works on any machine. SH2_GAME_ROOT
  /// overrides it entirely.
  /// </summary>
  private static string LinuxMirrorPath =>
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "sh2-game-root");

  private static string gamePath;

  /// <summary>
  /// Where the original game's files are read from: SH2_GAME_ROOT if set, else
  /// the upstream literal when it exists, else this fork's Linux mirror. The
  /// upstream code names the Windows path everywhere (UIAssets, VideoFilePaths)
  /// and a nonexistent one makes every frontend texture and video silently
  /// unresolvable on Linux - which is exactly what a boot into the main menu
  /// needs (the menu's own art and the intro videos).
  /// </summary>
  public static string GamePath => gamePath ??= ResolveGamePath();

  public static string VideoCachePath = Application.dataPath + "/Cache/Video";

  private static string ResolveGamePath()
  {
    string fromEnvironment = Environment.GetEnvironmentVariable("SH2_GAME_ROOT");
    if (!string.IsNullOrEmpty(fromEnvironment)) return fromEnvironment;
    if (Directory.Exists(DefaultGamePath)) return DefaultGamePath;
    if (Directory.Exists(LinuxMirrorPath)) return LinuxMirrorPath;
    return DefaultGamePath;
  }

  /// <summary>
  /// The extraction lab (sh2-port) beside this project: SH2_PORT_ROOT if set,
  /// else the sibling directory of this Unity project (the layout both repos
  /// live in - ".../sh2-port" next to ".../sh2-unity"). The dev harnesses read
  /// the lab's per-map JSONs and manifests from here.
  /// </summary>
  public static string PortRoot => portRoot ??= ResolvePortRoot();

  private static string portRoot;

  private static string ResolvePortRoot()
  {
    string fromEnvironment = Environment.GetEnvironmentVariable("SH2_PORT_ROOT");
    if (!string.IsNullOrEmpty(fromEnvironment)) return fromEnvironment;
    return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "sh2-port"));
  }
}
