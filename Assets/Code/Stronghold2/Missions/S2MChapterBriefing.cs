using System;
using System.Collections.Generic;
using System.IO;
using Assets.Code.Stronghold2.Text;

namespace Assets.Code.Stronghold2.Missions
{
  /// <summary>
  /// One hint of a chapter's briefing, as the install's table carries it.
  /// </summary>
  public readonly struct S2MBriefingHint
  {
    public S2MBriefingHint(string id, string text)
    {
      Id = id;
      Text = text;
    }

    /// <summary>The shipped row id (WARHINT03_0, ...) - what was read, named.</summary>
    public readonly string Id;

    /// <summary>The shipped English text of that row.</summary>
    public readonly string Text;
  }

  /// <summary>
  /// The briefing content of ONE campaign chapter, resolved from the install
  /// alone: the chapter's own picture (ui/chapter_introductions/
  /// chapterNN_intro.jpg), its title (WAR_CAMPAIGN_HEADINGnn) and its hints
  /// (WARHINTnn_0..7), all read out of i18n/en-us.xml through
  /// <see cref="S2MTextTable"/> - the rows' own two-digit index is the
  /// chapter's, taken from the map file the flow is playing
  /// (maps/war_chapterN.s2m).
  ///
  /// WHAT THE INSTALL DOES NOT SAY, measured 2026-10-13 (do not re-derive):
  /// - No objective/briefing PROSE is shipped for the twelve war chapters. The
  ///   only briefing paragraphs in the table are DEMO_BRIEFING_1..3 (the demo
  ///   campaign's three missions); the maps author no start-of-mission briefing
  ///   action at all (the two EnterBriefingActions are mid-mission in
  ///   war_chapter6, and the action carries no payload - its reader reads a
  ///   header, a payload marker and a trailer), the .csc cutscene files carry no
  ///   text, and the help file documents the game rather than the frontend. So
  ///   the Objective panel shows the chapter's title - the one per-chapter line
  ///   that IS shipped - and the report names the gap.
  /// - The hint rows' chapter attribution is the ROWS' OWN NAMES' reading
  ///   (WARHINT&lt;chapter-1&gt;_0..7) and nothing in the install's data
  ///   confirms it: WARHINT00's text matches war_chapter1.s2m (the map's own
  ///   custom trigger is 'Fight through the guards and reach the boat' and the
  ///   hint says 'Direct Sir William and Matthew (the two horsemen) to a
  ///   position near the boat'), while war_chapter8.s2m's win - the measured
  ///   CustomTriggerWilliamReachKeep plus a ControlLordsAIAction that hands the
  ///   player a lord - matches WARHINT09's text verbatim ('Once he is rescued
  ///   you will be able to control William and his troops'). Two anchors, two
  ///   different offsets, and 14 families (WARHINT00..13) for 12 chapters
  ///   carrying 19 missions, so no single offset fits. FORMATS.md carries the
  ///   evidence as an open question; the screen's record says which reading it
  ///   used, so a later change can flip one method when the pairing is pinned.
  /// </summary>
  public sealed class S2MChapterBriefing
  {
    /// <summary>The hint slots the install ships per family (WARHINTnn_0..7).</summary>
    public const int HintSlots = 8;

    /// <summary>What the port read the hints by - printed on the report, per the note above.</summary>
    public const string HintAttributionNote =
      "the chapter's hints are WARHINT<chapter-1>_0..7 READ BY THE ROWS' OWN NAMES; the install's "
      + "data does not confirm the pairing (two measured anchors disagree - see FORMATS.md)";

    /// <summary>What the port can say about the objective text - printed on the report.</summary>
    public const string ObjectiveTextNote =
      "the install ships no objective/briefing prose for the war campaign's chapters (the only "
      + "briefing paragraphs in its table are DEMO_BRIEFING_1..3, the demo's), so the Objective "
      + "panel carries the chapter's own title; the maps author no start briefing action";

    /// <summary>The chapter's number (1-based), or 0 when the map is not a war chapter.</summary>
    public int Chapter { get; private set; }

    /// <summary>The flow's own map path (maps/war_chapterN.s2m), as the click named it.</summary>
    public string MapPath { get; private set; }

    /// <summary>The map file's stem (war_chapter4) - what the resolution matched on.</summary>
    public string MapStem { get; private set; }

    /// <summary>The shipped id of the chapter's title row, or null when there is none.</summary>
    public string TitleId { get; private set; }

    /// <summary>The chapter's title ('The Bull in the Borderlands'), or null.</summary>
    public string Title { get; private set; }

    /// <summary>The chapter's own picture (the install's chapterNN_intro.jpg), or null.</summary>
    public string PicturePath { get; private set; }

    /// <summary>The hint family read (e.g. "WARHINT03_0..7"), or null when there is none.</summary>
    public string HintsFamily { get; private set; }

    /// <summary>The hints the family's slots carry text for, in slot order.</summary>
    public IReadOnlyList<S2MBriefingHint> Hints => hints;

    /// <summary>True when the map named a chapter AND its per-chapter rows resolved.</summary>
    public bool HasChapter => Chapter > 0;

    /// <summary>What could not be resolved for this chapter, named - null when all of it did.</summary>
    public string Note { get; private set; }

    private readonly List<S2MBriefingHint> hints = new();

    /// <summary>
    /// Resolves the briefing content of the chapter the flow's map path names.
    /// A map that is not a war chapter resolves to no chapter and a Note that
    /// names it - the caller shows the screen without per-chapter text rather
    /// than inventing any.
    /// </summary>
    public static S2MChapterBriefing Resolve(string installRoot, string mapPath, S2MTextTable table)
    {
      var briefing = new S2MChapterBriefing { MapPath = mapPath };
      string stem = string.IsNullOrEmpty(mapPath) ? "" : Path.GetFileNameWithoutExtension(mapPath);
      briefing.MapStem = stem;

      if (string.IsNullOrEmpty(stem))
      {
        briefing.Note = "the flow named no map, so there is no chapter to brief";
        return briefing;
      }

      // maps/war_chapterN.s2m -> N. The campaign screen's chapter rows name
      // exactly these files, and the install's own per-chapter art confirms the
      // numbering (AIVChap4_Bull.aic beside chapter04_intro.jpg's bull and
      // WAR_CAMPAIGN_HEADING03 'The Bull in the Borderlands' are all chapter 4).
      const string prefix = "war_chapter";
      if (!stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
          || !int.TryParse(stem.Substring(prefix.Length), out int chapter)
          || chapter <= 0)
      {
        briefing.Note = $"'{stem}' is not a war campaign chapter (maps/war_chapterN.s2m), "
                        + "so the install names no picture, title or hints for it";
        return briefing;
      }

      briefing.Chapter = chapter;
      int index = chapter - 1;

      briefing.TitleId = $"WAR_CAMPAIGN_HEADING{index:D2}";
      briefing.Title = table.Get(briefing.TitleId);
      if (briefing.Title == null)
      {
        briefing.Note = $"the install ships no {briefing.TitleId} row";
      }

      string picture = Path.Combine(installRoot ?? "",
        $"ui/chapter_introductions/chapter{chapter:D2}_intro.jpg");
      if (File.Exists(picture))
      {
        briefing.PicturePath = picture;
      }
      else
      {
        briefing.Note = Append(briefing.Note, $"the install ships no {picture}");
      }

      // The hint family the rows' own names read as the chapter's, slot 0..7;
      // an empty slot is a hint the install did not write, not a hint with no
      // text, so it is left out.
      string family = $"WARHINT{index:D2}";
      briefing.HintsFamily = $"{family}_0..{HintSlots - 1}";
      for (int slot = 0; slot < HintSlots; slot++)
      {
        string id = $"{family}_{slot}";
        string text = table.Get(id);
        if (!string.IsNullOrEmpty(text))
        {
          briefing.hints.Add(new S2MBriefingHint(id, text));
        }
      }

      if (briefing.hints.Count == 0)
      {
        briefing.Note = Append(briefing.Note, $"the install ships no non-empty {family}_0..7 row");
      }

      return briefing;
    }

    private static string Append(string current, string line) =>
      string.IsNullOrEmpty(current) ? line : current + "; " + line;
  }
}
