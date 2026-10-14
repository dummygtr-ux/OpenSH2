using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Assets.Code.Stronghold2.Text
{
  /// <summary>
  /// The install's own translation table, `i18n/en-us.xml` - the strings every
  /// screen in the shipped game is labelled from. It is SpreadsheetML: one
  /// &lt;Row&gt; per string, its cells &lt;Cell&gt;&lt;Data ss:Type="String"&gt;,
  /// and the first two non-empty string cells are the row's ID and its English
  /// text (a third, translated column follows in the localised files).
  ///
  /// The frontend screens here carry their handful of strings as constants
  /// measured from this table when the screens were built, and the flow UAT
  /// reads the table back to check them. The briefing screens cannot do that:
  /// a chapter's title and its hints are per-chapter rows (12 titles, 8 hint
  /// slots each), so they are READ from the table at run time instead of being
  /// copied into code - and the UAT keeps its own independent parse of the same
  /// file, so the two readers still check each other.
  ///
  /// The file is ~2 MB, so it is scanned rather than DOM-loaded: the rows are
  /// found by their own tags and the cell texts are unescaped with the same
  /// HTML decoder the names in them need (&amp;amp;, &amp;quot;, ...).
  /// </summary>
  public sealed class S2MTextTable
  {
    private readonly Dictionary<string, string> english =
      new(StringComparer.Ordinal);

    /// <summary>The table's own path, for the log lines.</summary>
    public string SourcePath { get; private set; }

    /// <summary>How many rows the parse accepted (the UAT compares this to its own read).</summary>
    public int Count => english.Count;

    /// <summary>
    /// Reads the table from an install root (the folder that holds `i18n/`).
    /// A table that cannot be read comes back EMPTY rather than throwing: every
    /// caller here has a named-gap path for "the install did not say", and a
    /// missing file must not take a screen down.
    /// </summary>
    public static S2MTextTable Load(string installRoot)
    {
      var table = new S2MTextTable
      {
        SourcePath = string.IsNullOrEmpty(installRoot)
          ? "i18n/en-us.xml"
          : Path.Combine(installRoot, "i18n/en-us.xml"),
      };

      string text;
      try
      {
        text = File.ReadAllText(table.SourcePath);
      }
      catch (Exception error)
      {
        UnityEngine.Debug.LogWarning($"Text table: {table.SourcePath} unreadable ({error.Message}); "
                                     + "every string it should carry will be reported as not shipped.");
        return table;
      }

      int cursor = 0;
      const string cellOpen = "<Data ss:Type=\"String\">";
      const string cellClose = "</Data>";
      while (true)
      {
        int rowStart = text.IndexOf("<Row>", cursor, StringComparison.Ordinal);
        if (rowStart < 0) break;
        int rowEnd = text.IndexOf("</Row>", rowStart, StringComparison.Ordinal);
        if (rowEnd < 0) break;
        cursor = rowEnd + "</Row>".Length;

        string id = null;
        string englishText = null;
        int cellCursor = rowStart;
        while (true)
        {
          int cellStart = text.IndexOf(cellOpen, cellCursor, StringComparison.Ordinal);
          if (cellStart < 0 || cellStart > rowEnd) break;
          cellCursor = cellStart + cellOpen.Length;
          int cellEnd = text.IndexOf(cellClose, cellCursor, StringComparison.Ordinal);
          if (cellEnd < 0 || cellEnd > rowEnd) break;

          string value = WebUtility.HtmlDecode(text.Substring(cellCursor, cellEnd - cellCursor)).Trim();
          cellCursor = cellEnd + cellClose.Length;
          if (id == null) { id = value; continue; }
          if (englishText == null) { englishText = value; break; }
        }

        if (!string.IsNullOrEmpty(id) && englishText != null && !table.english.ContainsKey(id))
        {
          table.english[id] = englishText;
        }
      }

      UnityEngine.Debug.Log($"Text table: read {table.english.Count} strings from {table.SourcePath}.");
      return table;
    }

    /// <summary>The row's English text, or null when the table ships no such row.</summary>
    public string Get(string id) =>
      id != null && english.TryGetValue(id, out string text) ? text : null;

    /// <summary>True when the table ships this row (with any text, empty included).</summary>
    public bool Has(string id) => id != null && english.ContainsKey(id);

    private static readonly Dictionary<string, S2MTextTable> shared =
      new(StringComparer.Ordinal);

    /// <summary>
    /// The table for one install, parsed once per run: a screen that is rebuilt
    /// (the briefing is built again on every chapter start) would otherwise
    /// re-scan the file's 2 MB each time.
    /// </summary>
    public static S2MTextTable ForInstall(string installRoot)
    {
      string key = installRoot ?? "";
      if (!shared.TryGetValue(key, out S2MTextTable table))
      {
        table = Load(installRoot);
        shared[key] = table;
      }
      return table;
    }
  }
}
