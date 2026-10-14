using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// The game's own MUSIC playlists, read from the two files the install ships
  /// in <c>fx/music/</c> and resolved against the music on disk:
  ///
  ///   sh2_PlayLists.csv        90 playlists, 478 items. A play list opens on a
  ///                            row with an id (PL_DEFAULT, PL_MAINMENU, ...)
  ///                            and continues on rows whose first cell is
  ///                            empty, one item per row, in playback order. An
  ///                            item is a track file name (223 cells: 128
  ///                            spelled .wav, 95 .mp3), a *silence(N) gap of N
  ///                            seconds (165 cells; 10000 is the one-shot
  ///                            idiom, 51 of them, that holds a stinger as the
  ///                            last thing a playlist plays), or *end() (90,
  ///                            exactly one per playlist, always last).
  ///   Music volume levels.txt  54 rows "Name,Volume", the files' stems. The
  ///                            scale is NOT the event sheets' 0..100: 100 on
  ///                            most rows, then 110, 115, 85 and 127, and 127
  ///                            reads as full scale (the two 127 rows are the
  ///                            loudest two tracks). <see cref="Gain"/> maps
  ///                            volume/127 - the PORT'S reading of an
  ///                            undecoded engine, marked as such; the values
  ///                            are the file's own.
  ///
  /// The resolution is <see cref="S2MPathResolver"/>'s: the four rules the
  /// event sheets use, plus the two habits only this sheet has (separators used
  /// interchangeably, and one one-letter misspelling). It mirrors
  /// <c>sh2-port/tools/audio_music.py</c> rule for rule, so the manifest that
  /// tool writes (<c>manifests/audio_music.csv</c>) and this reader agree -
  /// S2MMusicRun cross-checks them.
  ///
  /// Measured on the shipped data: 220 of the 223 track cells resolve (95 as
  /// written, 62 by extension, 54 case-blind, 4 by separator, 5 by the one
  /// spelling), 222 carry a volume-list row, and the three that do not resolve
  /// are two spellings - "null.wav" (PL_NULL, the null play list) and
  /// "Chantloop1.wav" (named by two playlists and by the volume list, shipped
  /// nowhere). A missing track is named, never guessed at.
  /// </summary>
  public class S2MMusicCatalogue
  {
    /// <summary>One playlist item: a track, a silence directive, or the end.</summary>
    public class Item
    {
      /// <summary>"track", "silence" or "end" - the sheet's own three kinds.</summary>
      public string Kind;
      /// <summary>The cell as written ("The Smith.wav", "*silence(180)").</summary>
      public string File;
      /// <summary>Music-relative path when the track resolved, else null.</summary>
      public string Path;
      /// <summary>
      /// The rule that resolved it - exact / ext / case / pad / sep / spell -
      /// or null for a silence, an end, and a track that resolved to nothing.
      /// </summary>
      public string Rule;
      /// <summary>The sheet's own seconds, silence items only.</summary>
      public float Seconds;
      /// <summary>The volume list's value, tracks only; 0 when it has no row.</summary>
      public int Volume;
      /// <summary>The volume list's own spelling of the track, when it has a row.</summary>
      public string VolumeName;
      public bool IsTrack => Kind == "track";
      public bool Resolved => Path != null;
    }

    /// <summary>One play list, in playback order, with its own counts.</summary>
    public class Playlist
    {
      public string Id;
      public readonly List<Item> Items = new List<Item>();
      public int TrackCount;
      public int ResolvedCount;
      public int MissingCount;
      public int SilenceCount;
      public int EndCount;
      /// <summary>Sum of the *silence() gaps, seconds (track time needs a decode).</summary>
      public float SilenceSeconds;
    }

    private readonly List<Playlist> playlists = new List<Playlist>();
    private readonly Dictionary<string, Playlist> byId =
      new Dictionary<string, Playlist>(StringComparer.OrdinalIgnoreCase);
    private readonly List<KeyValuePair<string, int>> volumes =
      new List<KeyValuePair<string, int>>();
    private readonly Dictionary<string, string> volumeByFold =
      new Dictionary<string, string>();
    private readonly Dictionary<string, int> volumeValue =
      new Dictionary<string, int>();
    private readonly Dictionary<string, AudioClip> clips =
      new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> clipFailures =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private S2MPathResolver resolver;
    private string musicRoot;

    /// <summary>The music-relative path of the clip the last TryGetClip loaded.</summary>
    public string LastResolvedPath { get; private set; }

    /// <summary>Play lists read (90 as shipped).</summary>
    public int PlaylistCount => playlists.Count;
    /// <summary>Items over every playlist (478 as shipped).</summary>
    public int ItemCount { get; private set; }
    public int TrackCount { get; private set; }
    public int ResolvedCount { get; private set; }
    public int MissingCount => TrackCount - ResolvedCount;
    public int SilenceCount { get; private set; }
    public int EndCount { get; private set; }
    /// <summary>Volume-list rows read (54 as shipped).</summary>
    public int VolumeRowCount => volumes.Count;
    /// <summary>Volume-list rows no playlist track names - the stinger tracks.</summary>
    public readonly List<string> UnmatchedVolumeNames = new List<string>();
    /// <summary>Sheet cells this reader could not make sense of, named with their line.</summary>
    public readonly List<string> ParseWarnings = new List<string>();

    public IReadOnlyList<Playlist> Playlists => playlists;

    /// <summary>
    /// Reads the playlist sheet and the volume list under
    /// <paramref name="gameInstallPath"/>/fx/music. Resolves every track cell
    /// here, once (the paths are fixed for the install's lifetime).
    /// </summary>
    /// <returns>The catalogue, or null with a reason when a shipped file is missing.</returns>
    public static S2MMusicCatalogue Load(string gameInstallPath, out string reason)
    {
      reason = null;
      string music = Path.Combine(gameInstallPath, "fx", "music");
      string sheet = Path.Combine(music, "sh2_PlayLists.csv");
      string volumeList = Path.Combine(music, "Music volume levels.txt");
      if (!Directory.Exists(music))
      {
        reason = $"no fx/music directory under '{gameInstallPath}'";
        return null;
      }
      if (!File.Exists(sheet))
      {
        reason = $"missing playlist sheet '{sheet}'";
        return null;
      }
      if (!File.Exists(volumeList))
      {
        reason = $"missing volume list '{volumeList}'";
        return null;
      }

      var catalogue = new S2MMusicCatalogue { musicRoot = music };
      catalogue.resolver = new S2MPathResolver(music, gameInstallPath);
      catalogue.ReadVolumes(volumeList);
      catalogue.ReadPlaylists(sheet);
      return catalogue;
    }

    /// <summary>
    /// The volume list: "Name,Volume" per CRLF row, the last row without a
    /// newline. Read in file order, and the name is the row's own spelling.
    /// </summary>
    private void ReadVolumes(string path)
    {
      // ISO-8859-1, like the event sheets: every cell this reader uses is
      // ASCII, and a stray high byte must not throw.
      foreach (string raw in File.ReadAllLines(path, Encoding.GetEncoding(28591)))
      {
        string line = raw.Trim();
        if (line.Length == 0) continue;
        int comma = line.LastIndexOf(',');
        if (comma <= 0) continue;
        string name = line.Substring(0, comma).Trim();
        if (!int.TryParse(line.Substring(comma + 1).Trim(), out int value)) continue;
        volumes.Add(new KeyValuePair<string, int>(name, value));
        string fold = S2MPathResolver.Fold(name);
        if (!volumeByFold.ContainsKey(fold)) volumeByFold[fold] = name;
        if (!volumeValue.ContainsKey(name)) volumeValue[name] = value;
      }
    }

    /// <summary>
    /// The playlist sheet. A row with a non-empty first cell opens a playlist;
    /// a row whose first cell is empty continues it. The sheet's cells hold no
    /// commas and no quotes (checked: 479 rows, two columns), so a plain split
    /// is exact - and a row with a surprise third cell is REPORTED rather than
    /// silently truncated.
    /// </summary>
    private void ReadPlaylists(string path)
    {
      Playlist open = null;
      int line = 0;
      foreach (string raw in File.ReadAllLines(path, Encoding.GetEncoding(28591)))
      {
        line++;
        if (raw.Length == 0) continue;
        string[] cells = raw.Split(',');
        if (cells.Length > 2 && cells.Skip(2).Any(cell => cell.Trim().Length > 0))
        {
          ParseWarnings.Add($"{Path.GetFileName(path)}:{line}: unexpected third cell in '{raw}'");
          continue;
        }
        string id = cells[0].Trim();
        string item = cells.Length > 1 ? cells[1].Trim() : string.Empty;
        if (id.StartsWith("Play List")) continue;
        if (id.Length > 0)
        {
          open = new Playlist { Id = id };
          playlists.Add(open);
          byId[id] = open;
        }
        if (item.Length == 0) continue;
        if (open == null)
        {
          ParseWarnings.Add($"{Path.GetFileName(path)}:{line}: item before any playlist id");
          continue;
        }
        open.Items.Add(ReadItem(item));
      }

      foreach (Playlist playlist in playlists)
      {
        foreach (Item item in playlist.Items)
        {
          ItemCount++;
          if (item.Kind == "track")
          {
            TrackCount++;
            if (item.Resolved) ResolvedCount++;
          }
          else if (item.Kind == "silence")
          {
            SilenceCount++;
            playlist.SilenceCount++;
            playlist.SilenceSeconds += item.Seconds;
          }
          else
          {
            EndCount++;
            playlist.EndCount++;
          }
        }
        playlist.TrackCount = playlist.Items.Count(item => item.Kind == "track");
        playlist.ResolvedCount = playlist.Items.Count(item => item.IsTrack && item.Resolved);
        playlist.MissingCount = playlist.TrackCount - playlist.ResolvedCount;
      }
      // The volume list covers more than the playlists do: the four rows no
      // track names are the stinger tracks the game's own events play
      // (bigwin3, Smallwin1, Stinger_lose_01, Victory is Ours as shipped).
      foreach (KeyValuePair<string, int> row in volumes)
      {
        bool used = playlists.Any(playlist => playlist.Items.Any(item =>
          item.IsTrack && string.Equals(item.VolumeName, row.Key,
            StringComparison.OrdinalIgnoreCase)));
        if (!used) UnmatchedVolumeNames.Add(row.Key);
      }
    }

    /// <summary>One item cell: its kind, its resolution, its volume row.</summary>
    private Item ReadItem(string cell)
    {
      var item = new Item { File = cell };
      if (cell.StartsWith("*silence"))
      {
        item.Kind = "silence";
        item.Seconds = ParseSeconds(cell);
        return item;
      }
      if (cell.StartsWith("*end"))
      {
        item.Kind = "end";
        return item;
      }

      item.Kind = "track";
      // The four shared rules first, then this sheet's two habits - the same
      // order tools/audio_music.py applies them in.
      item.Path = resolver.Resolve(cell, out string rule);
      if (item.Path == null) item.Path = resolver.ResolveSeparator(cell, out rule);
      if (item.Path == null) item.Path = resolver.ResolveSpelling(cell, out rule);
      item.Rule = item.Path == null ? null : rule;
      LookupVolume(item);
      return item;
    }

    /// <summary>
    /// The volume-list row for a track, or none. The join runs on the FILE's
    /// own stem when the track resolved and on the sheet's spelling when it did
    /// not, folding case and separators, and retrying the one-edit fold: the
    /// volume list spells a track "Honourmix_01" where the sheet's other cell
    /// spells it "honormix_01.mp3", the way the file does.
    /// </summary>
    private void LookupVolume(Item item)
    {
      string rel = item.Path ?? item.File.Replace('\\', '/').TrimStart('/');
      string stem = Path.GetFileNameWithoutExtension(rel);
      string key = S2MPathResolver.Fold(stem);
      if (volumeByFold.TryGetValue(key, out string name))
      {
        item.VolumeName = name;
        item.Volume = volumeValue[name];
        return;
      }
      string near = null;
      int found = 0;
      foreach (KeyValuePair<string, int> row in volumes)
      {
        if (!S2MPathResolver.WithinOneEdit(key, S2MPathResolver.Fold(row.Key))) continue;
        found++;
        near = row.Key;
        if (found > 1) return;
      }
      if (found != 1 || !volumeValue.TryGetValue(near, out int value)) return;
      item.VolumeName = near;
      item.Volume = value;
    }

    /// <summary>
    /// The N of *silence(N), seconds ("*silence(06)" is six). A directive with
    /// no number is reported as a zero gap rather than throwing, so one bad
    /// cell cannot cost a caller the whole playlist.
    /// </summary>
    private static float ParseSeconds(string cell)
    {
      int open = cell.IndexOf('(');
      int close = cell.IndexOf(')');
      if (open < 0 || close <= open) return 0f;
      return float.TryParse(cell.Substring(open + 1, close - open - 1),
        out float seconds) ? seconds : 0f;
    }

    /// <summary>
    /// The gain an AudioSource should carry for a volume-list value. The list's
    /// own scale tops out at 127 on the two loudest rows, so 127 is full scale:
    /// a 127 row plays at 1.00 and the common 100 row at 0.787. (The engine's
    /// real curve is undecoded; this is the port's linear reading of the file's
    /// own numbers, marked as such in FORMATS.md.)
    /// </summary>
    public static float Gain(int volume) => volume <= 0 ? 1f : volume / 127f;

    /// <summary>The playlist for an id, or false with a named reason.</summary>
    public bool TryGetPlaylist(string id, out Playlist playlist, out string reason)
    {
      if (string.IsNullOrEmpty(id))
      {
        playlist = null;
        reason = "no playlist id was asked for";
        return false;
      }
      if (byId.TryGetValue(id.Trim(), out playlist)) { reason = null; return true; }
      reason = $"'{id}' is not in the shipped playlist sheet "
               + $"({PlaylistCount} play list(s) read)";
      return false;
    }

    /// <summary>
    /// The clip for a track item, decoded on first use and cached by path.
    /// False with a reason when the item is not a track, resolved to no file,
    /// or did not decode.
    /// </summary>
    public bool TryGetClip(Item item, out AudioClip clip, out string reason)
    {
      LastResolvedPath = null;
      clip = null;
      if (item == null || !item.IsTrack)
      {
        reason = "the item is not a track";
        return false;
      }
      if (!item.Resolved)
      {
        reason = $"'{item.File}' resolves to no file"
                 + (string.IsNullOrEmpty(Locate(item.File))
                   ? " (no such file anywhere in the install)"
                   : $" (shipped as {Locate(item.File)})");
        return false;
      }
      if (clips.TryGetValue(item.Path, out clip) && clip != null)
      {
        LastResolvedPath = item.Path;
        reason = null;
        return true;
      }
      if (clipFailures.TryGetValue(item.Path, out string failed))
      {
        reason = failed;
        return false;
      }
      clip = S2MAudioClip.Load(Path.Combine(musicRoot, item.Path), out reason);
      if (clip == null)
      {
        reason = $"'{item.Path}' did not decode: {reason}";
        clipFailures[item.Path] = reason;
        return false;
      }
      clip.name = Path.GetFileName(item.Path);
      clips[item.Path] = clip;
      LastResolvedPath = item.Path;
      return true;
    }

    /// <summary>The install-relative file an unresolved cell likely meant, or "".</summary>
    public string Locate(string cell) => resolver.Locate(cell);
  }
}
