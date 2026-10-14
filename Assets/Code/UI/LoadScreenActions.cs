using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// The load screen's own actions, and its ONE entry point. This is the screen
  /// the war screen's WARSCREEN_LOAD row opens (the engine's own
  /// `LoadScenarioScreen`, screen name 'load scenario', table family
  /// LOADSCENARIOSCREEN_*), and the pairing is MEASURED (2026-10-13):
  /// the class's own code loads
  /// ui/ingame_menus/scenario_loadscreen.jpg as 'texture/loadscenario
  /// background', builds the header row (HEADER_LOAD 'Load' / HEADER_SAVE
  /// 'Save'), the Cancel row and the list's own date-column font, and scans
  /// for scenario files; the war screen's Load row's shipped tooltip is 'Load a
  /// previously saved game'.
  ///
  /// The LIST shows the player's own saved games - the shipped game keeps them
  /// in the user's Documents folder ('\Stronghold 2\Saves\', the folder
  /// template measured in the exe) - and this port ships NONE and has no save
  /// reader. So the screen is built complete and its list is EMPTY, and that is
  /// the install's own truth on a fresh machine: the three column headers
  /// (Name / Date / P) stand at the top of the panel and no row is drawn. The
  /// table ships no empty-state row (measured: no LOADSCENARIOSCREEN row says
  /// anything about an empty list), so this port draws none - it says the state
  /// on the console and in the report instead.
  ///
  /// The three column headers are the screen's sort rows: their shipped
  /// tooltips name the sort each applies (Sort by Name / Sort by Date / Sort by
  /// Number of Players), and each click reports the sort it would apply and
  /// that the list is empty. The Load row reports that no saved game is
  /// selected for the same reason, and the Cancel row is the way back to the
  /// WAR SCREEN the screen was opened from.
  /// </summary>
  public static class LoadScreenActions
  {
    /// <summary>The install's own name for this screen (the exe's own string).</summary>
    public const string ScreenName = "load scenario";

    /// <summary>The shipped header the screen wears in the LOAD mode: LOADSCENARIOSCREEN_HEADER_LOAD 'Load'.</summary>
    public const string HeaderId = "LOADSCENARIOSCREEN_HEADER_LOAD";
    public const string HeaderLabel = "Load";

    /// <summary>
    /// The shipped header of the screen's OTHER mode (LOADSCENARIOSCREEN_HEADER_SAVE
    /// 'Save'), which the in-game menu's Save entry opens in the shipped game.
    /// This port carries the name so the report can say which mode is which; it
    /// does not build the Save mode yet.
    /// </summary>
    public const string SaveHeaderId = "LOADSCENARIOSCREEN_HEADER_SAVE";
    public const string SaveHeaderLabel = "Save";

    /// <summary>
    /// The screen's own load row: LOADSCENARIOSCREEN_LOAD 'Load'. Its click
    /// names the state - the port ships no saved games, so nothing is selected
    /// and nothing can be loaded - rather than doing nothing silently.
    /// </summary>
    public static readonly LoadScreenAction Load = new(
      LoadScreenActionKind.Load, "LOADSCENARIOSCREEN_LOAD", "Load", "",
      "no destination yet: no saved game is selected - the port ships no saved games "
      + "(the shipped game lists the player's own saves from the user's Documents folder) and has "
      + "no save reader, so there is nothing here to load",
      "LoadButton");

    /// <summary>
    /// The screen's way back: LOADSCENARIOSCREEN_CANCEL 'Cancel', with the
    /// tooltip row the table ships beside it (LOADSCENARIO_STATICTOOLTIP_CANCEL
    /// 'Cancel'). In the shipped game Cancel leaves the screen for wherever it
    /// was opened from; here that is the WAR SCREEN, the frontend being a tree.
    /// </summary>
    public static readonly LoadScreenAction Cancel = new(
      LoadScreenActionKind.Cancel, "LOADSCENARIOSCREEN_CANCEL", "Cancel", "Cancel",
      "the war screen (the screen the load screen was opened from, which the port has)",
      "CancelButton");

    /// <summary>
    /// The list's three column headers, in the screen's own column order, each
    /// wearing the shipped sort row the table names beside it. Their clicks are
    /// the sorts the shipped screen applies to the list.
    /// </summary>
    public static readonly LoadScreenAction SortName = new(
      LoadScreenActionKind.SortName, "LOADSCENARIOSCREEN_NAME", "Name",
      "Sort by Name", "the list would be ordered by name", "SortNameButton");

    public static readonly LoadScreenAction SortDate = new(
      LoadScreenActionKind.SortDate, "LOADSCENARIOSCREEN_DATE", "Date",
      "Sort by Date", "the list would be ordered by date", "SortDateButton");

    public static readonly LoadScreenAction SortPlayers = new(
      LoadScreenActionKind.SortPlayers, "LOADSCENARIOSCREEN_PLAYERS", "P",
      "Sort by Number of Players", "the list would be ordered by number of players",
      "SortPlayersButton");

    /// <summary>The three sort rows, in the columns' own order.</summary>
    public static readonly LoadScreenAction[] Sorts = { SortName, SortDate, SortPlayers };

    /// <summary>The screen's two own rows, in the plates' own order (left first).</summary>
    public static readonly LoadScreenAction[] Rows = { Load, Cancel };

    /// <summary>How many saved games the list shows: none - the port ships no saves and has no save reader.</summary>
    public const int SavedGameCount = 0;

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static LoadScreenAction LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. LoadScreenUI builds its buttons once per showing,
    /// so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a load-screen action. Names the row, records it,
    /// reports what it does - and then does it: the Cancel row asks the flow
    /// for the war screen (Game.RequestFrontendScreen, the move every frontend
    /// click makes), and the Load row and the three sorts do not change
    /// anything - their clicks have named why (the list is empty).
    /// </summary>
    public static void Dispatch(LoadScreenAction row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;

      string outcome = OutcomeFor(row);
      Debug.Log($"[LoadScreenActions] click: {row.Id} '{row.Label}' -> {outcome}");

      switch (row.Kind)
      {
        case LoadScreenActionKind.Cancel:
          Game.GetInstance()?.RequestFrontendScreen(GameState.WAR_SCREEN);
          return;

        default:
          // Load and the three sorts: the list is empty (no saved games are
          // shipped), so there is nothing to load or to move - and a sort on an
          // empty list is the same list. The click has said so by name.
          return;
      }
    }

    /// <summary>
    /// What one click reports: the Load row names the state that keeps it from
    /// loading, each sort names the shipped sort it would apply and the empty
    /// list it would apply it to, and the Cancel row names its screen.
    /// </summary>
    public static string OutcomeFor(LoadScreenAction row) => row.Kind switch
    {
      LoadScreenActionKind.Load =>
        "no destination yet: no saved game is selected - the port ships no saved games and has no "
        + "save reader, so the list is empty and there is nothing to load",
      LoadScreenActionKind.SortName => SortOutcome(row, "name"),
      LoadScreenActionKind.SortDate => SortOutcome(row, "date"),
      LoadScreenActionKind.SortPlayers => SortOutcome(row, "number of players"),
      _ => row.Outcome,
    };

    private static string SortOutcome(LoadScreenAction row, string what) =>
      $"{row.Tooltip} is what the shipped screen applies to its list; the port's list is empty "
      + $"(no saved games are shipped), so ordering by {what} moves nothing";
  }
}
