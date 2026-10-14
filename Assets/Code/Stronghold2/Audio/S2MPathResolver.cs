using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// The shipped sheets' path resolution, in ONE place, because the game spells
  /// the same file several ways and two readers that disagree would be worse
  /// than either alone.
  ///
  /// The root this is built on is the directory the sheet's cells are RELATIVE
  /// TO - <c>fx/</c> for the event sheets (whose cells carry a subdirectory
  /// when they want one) and <c>fx/music/</c> for the playlists (whose cells
  /// are bare file names). Every rule runs against that root, so both
  /// catalogues resolve by the same code:
  ///
  ///   Resolve           the four rules every shipped sheet needs, in order:
  ///                     1. the sheet's own spelling;
  ///                     2. the extension retried (.wav then .mp3 - 400 sounds
  ///                        cells carry none, one is misspelled ".wavv", one
  ///                        doubles it, and the speech sheet writes ".wav" for
  ///                        files the install ships as ".mp3");
  ///                     3. the same two, case-blind, against an index of the
  ///                        whole root ("arm_hit_01.wav" for the shipped
  ///                        "Arm_hit_01.wav");
  ///                     4. the numbered name re-read without leading zeros
  ///                        ("deathfx\dth_0083" for "deathfx/dth_083.wav"),
  ///                        only on a UNIQUE match in the sheet's own
  ///                        directory.
  ///   ResolveSeparator  the music sheet's first extra habit: spaces and
  ///                     underscores are used interchangeably ("Fiddle_solo.wav"
  ///                     for the shipped "fiddle solo.mp3"). Unique-match
  ///                     guarded, like the padding rule.
  ///   ResolveSpelling   the music sheet's second: a one-letter misspelling of
  ///                     the shipped name ("Honourmix_01.wav" for the shipped
  ///                     "honormix_01.mp3" - and the sheet's own other cell for
  ///                     that track spells it the u-less way the file does).
  ///                     Also unique-match guarded.
  ///   Locate            the last-resort basename sweep for a miss, so a caller
  ///                     can name the file the sheet probably meant.
  ///
  /// The separator and spelling rules are only ever reached by the MUSIC
  /// reader; they live here because they are the same kind of thing as the
  /// four - a documented habit of the game's own authors, not a guess - and
  /// because the event catalogues must not start using them silently.
  ///
  /// This mirrors <c>sh2-port/tools/audio_catalogue.py</c> rule for rule and
  /// <c>tools/audio_music.py</c> for the two extra rules; the manifests those
  /// tools write are what the harnesses cross-check against.
  /// </summary>
  public class S2MPathResolver
  {
    private static readonly string[] AudioExtensions = { ".wav", ".mp3" };

    private readonly string root;
    private readonly string installRoot;
    private readonly Dictionary<string, string> caseIndex;
    private readonly Dictionary<string, string> padIndex;
    private readonly Dictionary<string, string> basenameIndex;
    // The two music-only tables are built on first use: the fx tree holds
    // ~5,700 files and only the music sheet ever asks for them.
    private Dictionary<string, string> foldedIndex;
    private Dictionary<string, string> spellStems;

    /// <summary>The directory the resolved paths are relative to.</summary>
    public string Root => root;

    /// <summary>
    /// Builds the indexes for one sheet root. <paramref name="installRoot"/>
    /// is where the basename sweep looks - the whole install, because a sheet
    /// path can name the wrong directory.
    /// </summary>
    public S2MPathResolver(string root, string installRoot)
    {
      this.root = root;
      this.installRoot = installRoot;
      caseIndex = BuildCaseIndex();
      padIndex = BuildPadIndex();
      basenameIndex = BuildBasenameIndex();
    }

    /// <summary>
    /// Resolves one sheet variant to a root-relative path by the four shared
    /// rules, or null. <paramref name="rule"/> names the rule that hit
    /// (exact / ext / case / pad) for a caller that reports how it resolved.
    /// </summary>
    public string Resolve(string variant, out string rule)
    {
      string rel = variant.Replace('\\', '/').TrimStart('/');
      string stem = Path.GetFileNameWithoutExtension(rel);
      string directory = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;
      var tries = new List<string> { rel };
      foreach (string extension in AudioExtensions)
      {
        string retry = stem + extension;
        if (directory.Length > 0) retry = directory + "/" + retry;
        if (!tries.Contains(retry)) tries.Add(retry);
      }
      // A stem that itself ends in an audio extension (the doubled
      // "Engineer_Line.wav.wav" cell) is tried as that stem.
      if (AudioExtensions.Any(e => stem.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
      {
        string trimmed = directory.Length > 0 ? directory + "/" + stem : stem;
        if (!tries.Contains(trimmed)) tries.Add(trimmed);
      }

      foreach (string candidate in tries)
      {
        if (File.Exists(Path.Combine(root, candidate)))
        {
          rule = candidate == rel ? "exact" : "ext";
          return candidate;
        }
      }
      foreach (string candidate in tries)
      {
        if (caseIndex.TryGetValue(candidate.ToLowerInvariant(), out string hit))
        {
          rule = "case";
          return hit;
        }
      }
      string key = (directory.Length > 0 ? directory + "/" + stem : stem).ToLowerInvariant();
      if (padIndex.TryGetValue(ZeroNormalised(key), out string pad) && pad != null)
      {
        rule = "pad";
        return pad;
      }
      rule = null;
      return null;
    }

    /// <summary>
    /// The music sheet's separator habit: the name with its spaces and
    /// underscores folded away, matched against the shipped names folded the
    /// same way. Only a UNIQUE candidate resolves, so the rule can never pick
    /// between two real files.
    /// </summary>
    public string ResolveSeparator(string variant, out string rule)
    {
      rule = null;
      if (foldedIndex == null) foldedIndex = BuildFoldedIndex();
      string rel = variant.Replace('\\', '/').TrimStart('/');
      string stem = Path.GetFileNameWithoutExtension(rel);
      string directory = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;
      string key = Fold(directory.Length > 0 ? directory + "/" + stem : stem);
      if (!foldedIndex.TryGetValue(key, out string hit) || hit == null) return null;
      rule = "sep";
      return hit;
    }

    /// <summary>
    /// The music sheet's spelling habit: the folded name with exactly one
    /// character inserted, deleted or substituted against a shipped name.
    /// A lookup rather than an index - the query is a name the install does
    /// not have - and unique-match guarded, so exactly one neighbour is
    /// required and two leave the cell unresolved.
    /// </summary>
    public string ResolveSpelling(string variant, out string rule)
    {
      rule = null;
      if (spellStems == null) spellStems = BuildSpellStems();
      string rel = variant.Replace('\\', '/').TrimStart('/');
      string stem = Path.GetFileNameWithoutExtension(rel);
      string directory = Path.GetDirectoryName(rel)?.Replace('\\', '/') ?? string.Empty;
      string key = Fold(directory.Length > 0 ? directory + "/" + stem : stem);
      string near = null;
      int found = 0;
      foreach (KeyValuePair<string, string> entry in spellStems)
      {
        if (entry.Key == key || !WithinOneEdit(key, entry.Key)) continue;
        found++;
        near = entry.Value;
        if (found > 1) return null;
      }
      if (found != 1) return null;
      rule = "spell";
      return near;
    }

    /// <summary>The install-relative file an unresolved variant likely meant, or "".</summary>
    public string Locate(string variant)
    {
      string rel = variant.Replace('\\', '/').TrimStart('/');
      string stem = Path.GetFileNameWithoutExtension(rel);
      string extension = Path.GetExtension(rel);
      foreach (string name in new[] { stem + extension, stem + ".wav", stem + ".mp3" })
      {
        if (basenameIndex.TryGetValue(name.ToLowerInvariant(), out string hit)) return hit;
      }
      return string.Empty;
    }

    /// <summary>
    /// Case and separators dropped: "Fiddle_solo", "fiddle solo" and
    /// "FIDDLE SOLO" all fold to "fiddlesolo". The sheet, the install and the
    /// volume list are three spellings of the same names, and this is the key
    /// the two music rules and the volume join compare on.
    /// </summary>
    public static string Fold(string name)
    {
      var builder = new StringBuilder(name.Length);
      foreach (char letter in name.ToLowerInvariant())
      {
        if (letter == ' ' || letter == '_' || letter == '\t') continue;
        builder.Append(letter);
      }
      return builder.ToString();
    }

    /// <summary>True when a and b differ by at most one insert, delete or substitute.</summary>
    public static bool WithinOneEdit(string a, string b)
    {
      if (Math.Abs(a.Length - b.Length) > 1) return false;
      if (a.Length > b.Length) { string swap = a; a = b; b = swap; }
      if (a.Length == b.Length)
      {
        int differences = 0;
        for (int index = 0; index < a.Length; index++)
          if (a[index] != b[index] && ++differences > 1) return false;
        return differences <= 1;
      }
      // b is one longer: exactly one character of b must be skippable.
      int at = 0;
      while (at < a.Length && a[at] == b[at]) at++;
      for (int index = at; index < a.Length; index++)
        if (a[index] != b[at + 1 + index - at]) return false;
      return true;
    }

    /// <summary>Every digit run re-read without leading zeros.</summary>
    private static string ZeroNormalised(string text)
    {
      var builder = new StringBuilder(text.Length);
      int index = 0;
      while (index < text.Length)
      {
        if (!char.IsDigit(text[index])) { builder.Append(text[index++]); continue; }
        int start = index;
        while (index < text.Length && char.IsDigit(text[index])) index++;
        string digits = text.Substring(start, index - start).TrimStart('0');
        builder.Append(digits.Length > 0 ? digits : "0");
      }
      return builder.ToString();
    }

    /// <summary>lowercased root-relative path -> the on-disk root-relative path.</summary>
    private Dictionary<string, string> BuildCaseIndex()
    {
      var index = new Dictionary<string, string>();
      foreach (string file in Files())
      {
        string rel = Relative(file, root);
        if (!index.ContainsKey(rel.ToLowerInvariant())) index[rel.ToLowerInvariant()] = rel;
      }
      return index;
    }

    /// <summary>Zero-normalised lowercased stem -> path, None when ambiguous.</summary>
    private Dictionary<string, string> BuildPadIndex()
    {
      var index = new Dictionary<string, string>();
      foreach (string file in Files())
      {
        string rel = Relative(file, root);
        string stem = rel.Substring(0, rel.Length - Path.GetExtension(rel).Length);
        string key = ZeroNormalised(stem.ToLowerInvariant());
        index[key] = index.ContainsKey(key) ? null : rel;
      }
      return index;
    }

    /// <summary>
    /// Folded stem -> path, or None when two files fold together. Built over
    /// the root only, and by FOLDED STEM, which is what makes the separator
    /// rule safe: a key two files share is dropped rather than guessed.
    /// </summary>
    private Dictionary<string, string> BuildFoldedIndex()
    {
      var index = new Dictionary<string, string>();
      foreach (string file in Files())
      {
        string rel = Relative(file, root);
        string stem = rel.Substring(0, rel.Length - Path.GetExtension(rel).Length);
        string key = Fold(stem);
        index[key] = index.ContainsKey(key) ? null : rel;
      }
      return index;
    }

    /// <summary>
    /// Folded stem -> path, one entry per folded stem, the canonical path
    /// taken in sorted order so the table does not depend on directory order
    /// (the mirror carries case aliases - two paths, one folded name).
    /// </summary>
    private Dictionary<string, string> BuildSpellStems()
    {
      var stems = new Dictionary<string, string>();
      foreach (string rel in Files().Select(file => Relative(file, root)).OrderBy(
        name => name, StringComparer.Ordinal))
      {
        string stem = Path.GetFileNameWithoutExtension(rel);
        string key = Fold(stem);
        if (!stems.ContainsKey(key)) stems[key] = rel;
      }
      return stems;
    }

    /// <summary>lowercased basename -> install-relative path, for the miss report.</summary>
    private Dictionary<string, string> BuildBasenameIndex()
    {
      var index = new Dictionary<string, string>();
      foreach (string file in Directory.EnumerateFiles(installRoot, "*",
        SearchOption.AllDirectories))
      {
        string name = Path.GetFileName(file).ToLowerInvariant();
        if (!index.ContainsKey(name)) index[name] = Relative(file, installRoot);
      }
      return index;
    }

    /// <summary>
    /// The files under the root. Sorted, because two of the tables above pick a
    /// canonical entry and an unsorted walk would make that pick depend on the
    /// filesystem.
    /// </summary>
    private IEnumerable<string> Files()
    {
      return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static string Relative(string file, string from)
    {
      string prefix = from.TrimEnd(Path.DirectorySeparatorChar)
        + Path.DirectorySeparatorChar;
      return file.StartsWith(prefix, StringComparison.Ordinal)
        ? file.Substring(prefix.Length).Replace(Path.DirectorySeparatorChar, '/')
        : file.Replace(Path.DirectorySeparatorChar, '/');
    }
  }
}
