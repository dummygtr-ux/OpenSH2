using System;
using System.Collections.Generic;
using System.IO;
using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// The map request screen's own actions, and its ONE entry point. This is the
  /// screen the war screen's WARSCREEN_CUSTOM row opens (the engine's own
  /// `MapRequestScreen`, screen name 'map request screen'), and the port's
  /// reading of that row is MEASURED (2026-10-13): the
  /// shipped engine's war-screen handler loads the Custom row's own picture
  /// ('warpeace_custom.jpg') into that screen and opens it, the screen's own
  /// code loads ui/map_select/mapselect_background.jpg and
  /// mapselect_background_war.jpg as its two backgrounds ('ui/map request bg' /
  /// 'ui/map request war bg'), and the table's one row for it is
  /// MAPREQUESTSCREEN_PLAY 'Play'.
  ///
  /// The LIST is the install's own maps, read at run time
  /// (<see cref="ReadInstallMaps"/>): every *.s2m scenario in the install's
  /// maps/ folder with the three families the screen's own code filters out -
  /// 'war_chapter', 'peace_chapter', 'tutorial' (the literals measured in the
  /// exe) - left out. 34 maps after the mirror's case-alias duplicates are
  /// grouped (the run's own reading of that folder is what the UAT checks this
  /// against), shown twelve to a page; the screen's two painted arrows are its
  /// paging, and this port names them because the install ships no rows and no
  /// words for either (the art paints them and the table is silent).
  ///
  /// What a click would LOAD is measured, not assumed: the port's per-map
  /// artifacts for these 34 maps are buildings 34/34 but entities 0/34 (the
  /// same gap the trail's missions and the sieges have - the port's flow loads
  /// the campaign's chapters, whose per-map dumps are complete), so starting a
  /// custom game is a later item and the Play row's click says so by name.
  /// </summary>
  public static class MapRequestScreenActions
  {
    /// <summary>The install's own name for this screen (the exe's own string).</summary>
    public const string ScreenName = "map request screen";

    /// <summary>The shipped row this screen's way on wears: MAPREQUESTSCREEN_PLAY 'Play'.</summary>
    public const string PlayId = "MAPREQUESTSCREEN_PLAY";
    public const string PlayLabel = "Play";

    /// <summary>
    /// How many maps one page of the list shows: the leather panel's list area
    /// is y 356..719 of the 1024x768 art (measured off the backdrop's own
    /// frames) at the siege list's own ~26 px pitch, and the painted medallions
    /// stand in the band below it - twelve rows keep the list clear of them.
    /// </summary>
    public const int PageSize = 12;

    /// <summary>
    /// The three families the shipped screen's own code filters its list by
    /// (the literals measured in stronghold2.exe): the campaign chapters and
    /// the tutorial are not custom maps, so the port leaves them out exactly
    /// as the shipped screen does.
    /// </summary>
    private static readonly string[] ExcludedPrefixes = { "war_chapter", "peace_chapter", "tutorial" };

    /// <summary>
    /// The install's own map list, in the order the screen shows it. Read once
    /// per showing (ResetRecord), because the screen is rebuilt every time it
    /// is opened and the folder's content is the install's, not the port's.
    /// </summary>
    public static MapRequestMap[] Maps { get; private set; } = Array.Empty<MapRequestMap>();

    /// <summary>How many pages the list makes.</summary>
    public static int PageCount => Maps.Length == 0 ? 1 : (Maps.Length + PageSize - 1) / PageSize;

    /// <summary>The page the screen is showing, 0-based. The screen opens at the list's first page.</summary>
    public static int Page { get; private set; }

    /// <summary>
    /// Which map the screen has selected, 0-based over the WHOLE list. The
    /// screen opens at the list's first map - the port has no progress and no
    /// preference to open anywhere else - and a list row's or a paging click's
    /// selection moves it. Both the UI and the UAT read it; only
    /// <see cref="Dispatch"/> writes it.
    /// </summary>
    public static int SelectedIndex { get; private set; }

    /// <summary>The map the screen is showing.</summary>
    public static MapRequestMap Selected => SelectedIndex >= 0 && SelectedIndex < Maps.Length
      ? Maps[SelectedIndex]
      : new MapRequestMap("", "", "");

    /// <summary>The map one visible list row shows (0-based within the page), where the row has one.</summary>
    public static MapRequestMap MapAt(int pageIndex)
    {
      int index = Page * PageSize + pageIndex;
      return index >= 0 && index < Maps.Length ? Maps[index] : new MapRequestMap("", "", "");
    }

    /// <summary>How many rows the current page carries (the last page may be short).</summary>
    public static int RowsOnPage => Math.Max(0, Math.Min(PageSize, Maps.Length - Page * PageSize));

    /// <summary>The name one list row's button carries in the scene ("MapRow00Button").</summary>
    public static string MapRowButtonName(int pageIndex) => $"MapRow{pageIndex:D2}Button";

    /// <summary>
    /// The screen's own row, MAPREQUESTSCREEN_PLAY 'Play': the way on from the
    /// selected map into the custom game. Starting one is a later item - the
    /// port's flow loads the campaign's .s2m chapters, and these maps' per-map
    /// entity dumps are not there (0/34) - so the click names the selected map
    /// and the gap rather than doing nothing. Its picture is the install's own
    /// castle-scenario play button (ui/siege_menu/button_play.bmp): the
    /// ui/map_select/ folder ships no button art, so the picture is the port's
    /// reading, named on the report.
    /// </summary>
    public static readonly MapRequestScreenAction Play = new(
      MapRequestScreenActionKind.Play, PlayId, PlayLabel,
      "no destination yet: a custom game on the selected map is not built",
      UIAssets.MapRequestPlayButtonTexture, "PlayButton", -1);

    /// <summary>
    /// One page back, on the screen's own left painted arrow (measured: the
    /// medallion is the art's own pixels at x 53..99, y 672..719 of the
    /// 1024x768 picture). The button is transparent and wears no words - the
    /// painted arrow IS the install's picture for it - and the install ships
    /// no row, so the port names it in the records rather than inventing one.
    /// </summary>
    public static readonly MapRequestScreenAction PreviousPage = new(
      MapRequestScreenActionKind.PreviousPage, "", "",
      "the list's previous page", "", "PreviousPageButton", -1);

    /// <summary>One page on, on the screen's own right painted arrow (x 925..971, y 672..719).</summary>
    public static readonly MapRequestScreenAction NextPage = new(
      MapRequestScreenActionKind.NextPage, "", "",
      "the list's next page", "", "NextPageButton", -1);

    /// <summary>
    /// The way back ('Previous menu') - the only row on this screen with a
    /// screen behind it, and it is the WAR SCREEN the screen was opened from:
    /// the frontend is a tree, and the map request screen is a branch of the
    /// war screen exactly as the campaign screen, the trail and the siege
    /// selection are. The screen ships no exit row of its own (the table's only
    /// MAPREQUEST* row is PLAY), so this row is the war screen's own shipped
    /// exit - the same words every frontend way back carries - on the
    /// frontend's own fancy button, the deal the trail's way back made.
    /// </summary>
    public static readonly MapRequestScreenAction Back = new(
      MapRequestScreenActionKind.Back, "WARSCREENSTATICTOOLTIP_EXIT", "Previous menu",
      "the war screen (the screen the map request screen was opened from, which the port has)",
      UIAssets.FancyButtonTexture, "BackButton", -1);

    /// <summary>
    /// The two fixed rows beside the list, in the screen's own layout order:
    /// the two painted arrows flank the list and the Play row leads on.
    /// </summary>
    public static readonly MapRequestScreenAction[] FixedRows = { PreviousPage, NextPage, Play };

    /// <summary>
    /// The row one visible list row is: its label is the map's own stem, and
    /// the click SELECTS that map - the screen's title strip and its list
    /// highlight follow it, and the Play row acts on it.
    /// </summary>
    public static MapRequestScreenAction SelectRow(int pageIndex)
    {
      MapRequestMap map = MapAt(pageIndex);
      return new MapRequestScreenAction(
        MapRequestScreenActionKind.SelectMap, "", map.Stem,
        $"the map request screen shows '{map.Stem}' as its selection ({map.FileName})",
        "", MapRowButtonName(pageIndex), Page * PageSize + pageIndex);
    }

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static MapRequestScreenAction LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over, the list with it, and the screen's place in it:
    /// MapRequestScreenUI builds its buttons once per showing, so the record
    /// always describes the screen that is on it - and the screen opens on the
    /// install's own list's first page with its first map selected, every time
    /// (the port has no progress to open it anywhere else).
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
      Page = 0;
      SelectedIndex = 0;
      Maps = ReadInstallMaps(Path.Combine(Constants.GamePath, "maps"));
      Debug.Log($"Map request screen: the install's own map list is {Maps.Length} map(s) "
                + $"({PageCount} page(s) of up to {PageSize}), the shipped screen's own filter "
                + $"(no {string.Join(", ", ExcludedPrefixes)}*) and no *.s2c maps (the siege "
                + "selection's own scenarios) applied.");
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a map-request action. Names the row, records it,
    /// reports what it does - and then does it: a list row's click selects that
    /// map, a paging click moves the page and the selection with it (unless the
    /// list is already at the end it points past, in which case the click names
    /// that state), the way back asks the flow for the war screen
    /// (Game.RequestFrontendScreen, the move every frontend click makes), and
    /// the Play row does not start anything yet - its click has said so by name.
    /// </summary>
    public static void Dispatch(MapRequestScreenAction row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;

      string outcome = OutcomeFor(row);
      Debug.Log($"[MapRequestScreenActions] click: {row.Describe} -> {outcome}");

      switch (row.Kind)
      {
        case MapRequestScreenActionKind.SelectMap:
          if (row.MapIndex != SelectedIndex)
          {
            SelectedIndex = row.MapIndex;
            RefreshScreen();
          }
          return;

        case MapRequestScreenActionKind.PreviousPage:
          if (Page > 0)
          {
            Page--;
            SelectedIndex = Page * PageSize;
            RefreshScreen();
          }
          return;

        case MapRequestScreenActionKind.NextPage:
          if (Page < PageCount - 1)
          {
            Page++;
            SelectedIndex = Page * PageSize;
            RefreshScreen();
          }
          return;

        case MapRequestScreenActionKind.Back:
          Game.GetInstance()?.RequestFrontendScreen(GameState.WAR_SCREEN);
          return;

        default:
          // Play: the custom game behind it is a later item. The click has
          // named the selected map and why.
          return;
      }
    }

    /// <summary>
    /// What one click reports: a list row names the map the screen now shows,
    /// a paging click names where it goes or the end it is already at (the
    /// port's own wording - the install ships no row for the two arrows), the
    /// Play row names the selected map and the artifact gap that keeps the
    /// custom game from starting, and the way back names its screen.
    /// </summary>
    public static string OutcomeFor(MapRequestScreenAction row) => row.Kind switch
    {
      MapRequestScreenActionKind.Play =>
        $"no destination yet: a custom game on '{Selected.Stem}' ({Selected.FileName}) is not "
        + "built - the port's flow loads the campaign's chapters, and these maps' per-map entity "
        + "dumps are not there (the gap the trail's missions and the sieges share)",
      MapRequestScreenActionKind.PreviousPage =>
        Page > 0
          ? $"the list's previous page (page {Page} of {PageCount} is what the screen shows next)"
          : "the list is already on its first page (the port's own wording: the install ships no "
            + "row for the screen's painted arrows)",
      MapRequestScreenActionKind.NextPage =>
        Page < PageCount - 1
          ? $"the list's next page (page {Page + 2} of {PageCount} is what the screen shows next)"
          : "the list is already on its last page (the port's own wording: the install ships no "
            + "row for the screen's painted arrows)",
      _ => row.Outcome,
    };

    /// <summary>
    /// The install's own map list as this port reads it: every *.s2m file in
    /// the folder, the three families the shipped screen's own code filters out
    /// ('war_chapter*', 'peace_chapter*', 'tutorial*', matched on the lowercased
    /// name) left out, the mirror's case-alias twins grouped to ONE map and the
    /// spelling that carries capitals kept - the install's own file - then
    /// sorted by the stem the list shows. A folder that cannot be read comes
    /// back EMPTY with a warning rather than taking the screen down: the screen
    /// says how many maps it has either way.
    /// </summary>
    public static MapRequestMap[] ReadInstallMaps(string mapsFolder)
    {
      var byLowerName = new Dictionary<string, string>(StringComparer.Ordinal);
      string[] files;
      try
      {
        files = Directory.GetFiles(mapsFolder, "*.s2m");
      }
      catch (Exception error)
      {
        Debug.LogWarning($"Map request screen: {mapsFolder} is unreadable ({error.Message}); "
                         + "the screen shows an empty list.");
        return Array.Empty<MapRequestMap>();
      }

      foreach (string path in files)
      {
        string fileName = Path.GetFileName(path);
        string lower = fileName.ToLowerInvariant();
        bool excluded = false;
        foreach (string prefix in ExcludedPrefixes)
        {
          if (lower.StartsWith(prefix, StringComparison.Ordinal)) { excluded = true; break; }
        }
        if (excluded) continue;

        if (!byLowerName.TryGetValue(lower, out string kept)) byLowerName[lower] = fileName;
        else if (PrefersOver(fileName, kept)) byLowerName[lower] = fileName;
      }

      var maps = new List<MapRequestMap>(byLowerName.Count);
      foreach (KeyValuePair<string, string> pair in byLowerName)
      {
        maps.Add(new MapRequestMap(pair.Value, Path.GetFileNameWithoutExtension(pair.Value),
                                   Path.Combine(mapsFolder, pair.Value)));
      }
      maps.Sort((a, b) =>
      {
        int byStem = string.Compare(a.Stem, b.Stem, StringComparison.OrdinalIgnoreCase);
        return byStem != 0 ? byStem : string.CompareOrdinal(a.Stem, b.Stem);
      });
      return maps.ToArray();
    }

    /// <summary>
    /// Which spelling of one map's name the list keeps: the one that carries
    /// capitals, because the mirror's lowercase entries are the case-corrected
    /// ALIASES the port added beside the install's own files (many shipped names
    /// carry capitals - 'Arena of Kings.s2m' - and the alias is the all-lowercase
    /// twin); when both or neither carry capitals, the ordinal-first name is
    /// kept, so the choice is deterministic.
    /// </summary>
    private static bool PrefersOver(string candidate, string kept)
    {
      bool candidateHasCapital = HasCapital(candidate);
      bool keptHasCapital = HasCapital(kept);
      if (candidateHasCapital != keptHasCapital) return candidateHasCapital;
      return string.CompareOrdinal(candidate, kept) < 0;
    }

    private static bool HasCapital(string name)
    {
      foreach (char c in name)
      {
        if (char.IsUpper(c)) return true;
      }
      return false;
    }

    /// <summary>The screen's own refresh, where one is up (a selection or a paging click's next step).</summary>
    private static void RefreshScreen() =>
      Game.GetInstance()?.mapRequestScreenUI?.Refresh();
  }
}
