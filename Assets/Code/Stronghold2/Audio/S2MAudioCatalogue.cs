using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// The game's own event -> audio mapping, read from the two sheets the
  /// install ships beside the audio they name (<c>fx/sh2_sounds.csv</c>,
  /// <c>fx/sh2_speech.csv</c>) and resolved against the files on disk.
  ///
  /// The sheets are the authority for the MAPPING and for the per-id volume /
  /// random flags; nothing about them is guessed here. What this class adds is
  /// the path resolution, because the sheets were written for Windows and are
  /// not internally consistent (see the rule list in <see cref="Resolve"/>),
  /// and the clip loading, because a resolved path is not yet an AudioClip.
  ///
  /// The resolution mirrors <c>sh2-port/tools/audio_catalogue.py</c> rule for
  /// rule, so the manifest that tool writes (<c>manifests/audio_catalogue.csv</c>)
  /// and this runtime resolver agree - S2MAudioRun cross-checks them on the
  /// ids it uses. Two readers of the same sheet that disagree would be worse
  /// than either one alone.
  ///
  /// Clips load through <c>UnityWebRequestMultimedia</c>, which decodes both
  /// shipped formats on this platform: the effects are .wav (mostly IMA ADPCM,
  /// format tag 17, 22.05 kHz mono - see FORMATS.md) and the speech is .mp3
  /// (MPEG-1 layer III, 128 kbps, 44.1 kHz mono). Loading BLOCKS the calling
  /// thread until the request finishes (a few ms per file, and every clip is
  /// cached by id), because the harnesses run with no player loop to yield to;
  /// a Play-mode caller pays that once per id, on the frame it first plays it.
  /// </summary>
  public class S2MAudioCatalogue
  {
    /// <summary>One sheet row: the id, its flags, and its variant paths.</summary>
    public class Entry
    {
      /// <summary>The sheet's own spelling - two ids carry stray whitespace.</summary>
      public string Id;
      /// <summary>"sound" (fx/sh2_sounds.csv) or "speech" (fx/sh2_speech.csv).</summary>
      public string Kind;
      /// <summary>The sheet's own id name, trimmed - the key callers use.</summary>
      public string Key;
      /// <summary>0..100 from the sheet; 100 on every speech row.</summary>
      public int Volume = 100;
      /// <summary>Sounds only: the sheet's Delay column, in 1/40 s ticks.</summary>
      public int Delay;
      /// <summary>The sheet's Random column: "yes" means pick a variant at random.</summary>
      public bool Random;
      public string Comment;
      /// <summary>Every variant cell, in sheet order, as written.</summary>
      public readonly List<string> Variants = new List<string>();
      /// <summary>The variants that resolve, install-relative to fx/. One per Variant, order kept.</summary>
      public readonly List<string> Paths = new List<string>();
      /// <summary>The variants that do not resolve, as written.</summary>
      public readonly List<string> Missing = new List<string>();

      public bool AnyResolved => Paths.Count > 0;

      /// <summary>"3 of 4 variants resolve" - the one-line reason a caller logs.</summary>
      public string Resolution => $"{Paths.Count} of {Variants.Count} variant(s) resolve";
    }

    private readonly Dictionary<string, Entry> byKey =
      new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AudioClip> clips =
      new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    /// <summary>The fx-relative file each cached clip came from, for reports.</summary>
    private readonly Dictionary<string, string> clipPaths =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> clipFailures =
      new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Lowercased path -> on-disk path, and the other sheet tables.</summary>
    private S2MPathResolver resolver;

    private string fxRoot;
    private System.Random random = new System.Random(20261011);

    /// <summary>The id the last <see cref="TryGetClip"/> call actually loaded, for reports.</summary>
    public string LastResolvedPath { get; private set; }

    /// <summary>Sheet rows read, including the two ids the speech sheet repeats.</summary>
    public int RowCount { get; private set; }

    /// <summary>Rows of sh2_sounds.csv / sh2_speech.csv (457 / 704 as shipped).</summary>
    public int SoundCount { get; private set; }
    public int SpeechCount { get; private set; }

    /// <summary>Distinct ids the lookup holds (1,159 - two speech ids repeat).</summary>
    public int IdCount => byKey.Count;
    public int VariantCount { get; private set; }
    public int ResolvedCount { get; private set; }
    public int UnresolvedCount => VariantCount - ResolvedCount;

    /// <summary>
    /// Reads both sheets under <paramref name="gameInstallPath"/>/fx and
    /// resolves every variant cell. The indexes are built once here (an fx
    /// tree walk, ~5,700 files), so a later lookup is a dictionary hit.
    /// </summary>
    /// <returns>The catalogue, or null with a reason when a sheet is missing.</returns>
    public static S2MAudioCatalogue Load(string gameInstallPath, out string reason)
    {
      reason = null;
      string fx = Path.Combine(gameInstallPath, "fx");
      if (!Directory.Exists(fx))
      {
        reason = $"no fx directory under '{gameInstallPath}'";
        return null;
      }

      var catalogue = new S2MAudioCatalogue { fxRoot = fx };
      catalogue.resolver = new S2MPathResolver(fx, gameInstallPath);

      foreach ((string kind, string sheet) in new[]
      {
        ("sound", "sh2_sounds.csv"),
        ("speech", "sh2_speech.csv"),
      })
      {
        string path = Path.Combine(fx, sheet);
        if (!File.Exists(path))
        {
          reason = $"missing catalogue sheet '{path}'";
          return null;
        }
        int count = catalogue.ReadSheet(path, kind);
        if (kind == "sound") catalogue.SoundCount = count; else catalogue.SpeechCount = count;      }
      return catalogue;
    }

    /// <summary>The definition ROWS read from one sheet - not the distinct ids.</summary>
    private int ReadSheet(string path, string kind)
    {
      int rows = 0;
      // ISO-8859-1: the sheets are ASCII in every cell this reader uses, and
      // decoding them as UTF-8 would throw on a stray high byte in a comment.
      foreach (string line in File.ReadAllLines(path, Encoding.GetEncoding(28591)))
      {
        if (line.Length == 0 || line[0] == ',') continue;
        string[] cells = line.Split(',');
        if (cells[0].Trim().Length == 0 || cells[0].StartsWith("Sound ID")
          || cells[0].StartsWith("Speech ID")) continue;

        var entry = new Entry
        {
          Id = cells[0],
          Key = cells[0].Trim(),
          Kind = kind,
          Comment = cells.Length > 4 ? cells[4].Trim() : string.Empty,
        };
        if (cells.Length > 1 && int.TryParse(cells[1].Trim(), out int volume)) entry.Volume = volume;
        // The speech sheet's third column is headed `unused` and is empty on
        // every row, so only a sound row can carry a delay.
        if (kind == "sound" && cells.Length > 2
          && int.TryParse(cells[2].Trim(), out int delay)) entry.Delay = delay;
        if (kind == "sound" && cells.Length > 3)
          entry.Random = cells[3].Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);

        for (int cell = 5; cell < cells.Length; cell++)
        {
          string variant = cells[cell].Trim();
          if (variant.Length == 0) continue;
          entry.Variants.Add(variant);
          string resolved = Resolve(variant);
          if (resolved != null) entry.Paths.Add(resolved);
          else entry.Missing.Add(variant);
        }

        VariantCount += entry.Variants.Count;
        ResolvedCount += entry.Paths.Count;
        RowCount++;
        rows++;
        // A repeated id (the speech sheet repeats two) keeps BOTH rows: the
        // sheets are the authority, and hiding a row would hide its variants.
        byKey.TryGetValue(entry.Key, out Entry seen);
        if (seen == null)
        {
          byKey[entry.Key] = entry;
        }
        else
        {
          seen.Variants.AddRange(entry.Variants);
          seen.Paths.AddRange(entry.Paths);
          seen.Missing.AddRange(entry.Missing);
        }
      }
      return rows;
    }

    /// <summary>
    /// Resolves one sheet variant to an fx-relative path, or null.
    ///
    /// The four rules the shipped event sheets need are <see cref="S2MPathResolver"/>'s
    /// (the sheet's own spelling; the extension retried; the same case-blind;
    /// and the zero-padding correction on a unique match) - they are shared
    /// with the music reader rather than written twice, so the runtime cannot
    /// resolve one sheet by rules the other does not have.
    /// </summary>
    private string Resolve(string variant) => resolver.Resolve(variant, out _);

    /// <summary>The file an unresolved variant most likely meant, or "".</summary>
    public string Locate(string variant) => resolver.Locate(variant);

    private string Relative(string file, string root = null)
    {
      string prefix = (root ?? fxRoot).TrimEnd(Path.DirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
      return file.StartsWith(prefix, StringComparison.Ordinal)
        ? file.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/')
        : file.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>The sheet row for an id, or false with a named reason.</summary>
    public bool TryResolve(string id, out Entry entry, out string reason)
    {
      if (string.IsNullOrEmpty(id))
      {
        entry = null;
        reason = "no id was asked for";
        return false;
      }
      if (byKey.TryGetValue(id.Trim(), out entry)) { reason = null; return true; }
      reason = $"'{id}' is not in the shipped catalogues "
               + $"({SoundCount} sound + {SpeechCount} speech id(s) read)";
      return false;
    }

    /// <summary>
    /// The clip for an id, loaded on first use and cached. False with a reason
    /// when the id is unknown, resolves to no file, or the decode fails - the
    /// three cases a caller has to tell apart, and none of them throws.
    /// </summary>
    public bool TryGetClip(string id, out AudioClip clip, out string reason)
    {
      LastResolvedPath = null;
      if (!TryResolve(id, out Entry entry, out reason)) { clip = null; return false; }
      // A cached clip answers with the path it was loaded from: the caller
      // reads LastResolvedPath to name the FILE behind the clip, and a cache
      // hit that left it null would make every later report lie (found by the
      // second format section reading a null path).
      if (clips.TryGetValue(entry.Key, out clip) && clip != null)
      {
        clipPaths.TryGetValue(entry.Key, out string cached);
        LastResolvedPath = cached;
        reason = null;
        return true;
      }
      if (clipFailures.TryGetValue(entry.Key, out string failed))
      {
        clip = null;
        reason = failed;
        return false;
      }
      if (!entry.AnyResolved)
      {
        clip = null;
        reason = $"'{entry.Key}' has no resolvable variant ({entry.Resolution}"
                 + (entry.Missing.Count > 0
                   ? $"; missing {string.Join(", ", entry.Missing)}" : "") + ")";
        clipFailures[entry.Key] = reason;
        return false;
      }

      string relative = NextVariant(entry);
      clip = S2MAudioClip.Load(Path.Combine(fxRoot, relative), out reason);
      if (clip == null)
      {
        reason = $"'{entry.Key}' resolved to '{relative}' but it did not decode: {reason}";
        clipFailures[entry.Key] = reason;
        return false;
      }
      clip.name = Path.GetFileName(relative);
      clips[entry.Key] = clip;
      clipPaths[entry.Key] = relative;
      LastResolvedPath = relative;
      return true;
    }

    /// <summary>
    /// Which variant of an entry to play. The sheet's own Random column
    /// decides: "yes" picks one at random per call, anything else takes the
    /// first resolved variant. (The sheet only marks effects random - no
    /// speech row says "yes" - and it does not document what a non-random
    /// multi-variant id does with its extra files, so a sequential policy was
    /// not invented here.)
    /// </summary>
    public string NextVariant(Entry entry)
    {
      if (entry.Paths.Count == 0) return null;
      if (!entry.Random) return entry.Paths[0];
      return entry.Paths[random.Next(entry.Paths.Count)];
    }
  }
}
