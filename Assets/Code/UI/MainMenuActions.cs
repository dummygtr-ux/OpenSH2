using UnityEngine;
using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// The six main-menu buttons, in the shipped frontend's own order.
  ///
  /// Where these come from: the shipped help file (help/stronghold_2_help.hlp)
  /// does NOT document the frontend - all 195 of its sections are in-game
  /// (buildings, units, formations, unit commands, the 36 tutorials), and its
  /// only "menu" mentions are the in-game menu bar and the catapult's own menu.
  /// The frontend's own strings are in the shipped translation table
  /// i18n/en-us.xml, and that table names all six buttons and the screens they
  /// open - see <see cref="MainMenuActions.All"/> for the rows.
  /// </summary>
  public enum MainMenuAction
  {
    Play = 0,
    Multiplayer = 1,
    MapEditor = 2,
    Options = 3,
    Load = 4,
    Exit = 5,
  }

  /// <summary>
  /// One action's shipped identity and where it leads: the i18n id the shipped
  /// frontend names the button with, its shipped English text, the shipped
  /// screen the button opens, and whether THIS PORT has built that screen yet.
  /// </summary>
  public readonly struct MainMenuActionInfo
  {
    public MainMenuActionInfo(MainMenuAction action, string id, string label,
                              string destination, GameState opens)
    {
      Action = action;
      Id = id;
      Label = label;
      Destination = destination;
      Opens = opens;
    }

    public readonly MainMenuAction Action;
    public readonly string Id;
    public readonly string Label;
    public readonly string Destination;

    /// <summary>
    /// The screen this action opens in THIS port, or GameState.NONE while the
    /// port has not built it. <see cref="Built"/> is derived from this, not
    /// toggled beside it: a row is built exactly when it names a screen the
    /// flow can really show, so the two can never disagree.
    /// </summary>
    public readonly GameState Opens;

    /// <summary>True when this action's destination exists in the port.</summary>
    public bool Built => Opens != GameState.NONE;
  }

  /// <summary>
  /// The menu's action surface: the six rows, and THE one entry point a click
  /// arrives through. MainMenuUI wires every button's own UnityEvent to
  /// <see cref="Dispatch"/>, and a Play-mode harness fires the buttons' pointer
  /// click - the call the input module itself makes - so the path a harness
  /// drives is exactly the path a player's click drives (the D005 discipline
  /// <c>S2MActorCommander.HandleClick</c> follows for the unit commands).
  /// </summary>
  public static class MainMenuActions
  {
    /// <summary>
    /// The six actions' shipped rows, in the order the menu shows them. The ids
    /// and the English text are the install's own (i18n/en-us.xml); each
    /// destination names the shipped screen by its own string ids, taken from
    /// the same table (PLAYSCREEN 'Choose Your Path', MULTIPLAYERSETUPSCREEN,
    /// LOBBYSCREEN 'Network Lobby', MAINSCENARIOSCREEN, SCENARIOEDITORSCREEN,
    /// the options screen's AUDIOSETTINGS_*/VIDEOSETTINGS_* rows,
    /// LOADSCENARIOSCREEN, and the exit confirmation the shipped voice line
    /// Confirm1.wav 'Exit Stronghold 2??' and ui/frontend/exitcancel.jpg are
    /// for). The last argument is the screen the port can really open for that
    /// row - GameState.NONE where it has none yet, so the row says so on every
    /// click - and Play is the first one with a screen behind it
    /// (GameState.PLAY_SCREEN, built 2026-10-12).
    /// </summary>
    public static readonly MainMenuActionInfo[] All =
    {
      new(MainMenuAction.Play, "MAINMENU_PLAY", "Play",
          "the play screen (PLAYSCREEN 'Choose Your Path')", GameState.PLAY_SCREEN),
      new(MainMenuAction.Multiplayer, "MAINMENU_MULTIPLAYER", "Multiplayer",
          "the multiplayer setup and lobby (MULTIPLAYERSETUPSCREEN, LOBBYSCREEN 'Network Lobby')", GameState.NONE),
      new(MainMenuAction.MapEditor, "MAINMENU_EDITOR", "Map Editor",
          "the scenario editor (MAINSCENARIOSCREEN, SCENARIOEDITORSCREEN)", GameState.NONE),
      new(MainMenuAction.Options, "MAINMENU_SETTINGS", "Options",
          "the options screen (AUDIOSETTINGS_* / VIDEOSETTINGS_* / MENU_VIDEO_* rows)", GameState.NONE),
      new(MainMenuAction.Load, "MAINMENU_LOAD", "Load",
          "the save/load list (LOADSCENARIOSCREEN)", GameState.NONE),
      new(MainMenuAction.Exit, "MAINMENU_EXIT", "Exit",
          "the exit confirmation ('Exit Stronghold 2??', ui/frontend/exitcancel.jpg)", GameState.NONE),
    };

    /// <summary>The row for one action (the enum values index <see cref="All"/>).</summary>
    public static MainMenuActionInfo Info(MainMenuAction action) => All[(int)action];

    /// <summary>
    /// The one wording of what a click did, shared by the log line and the UAT:
    /// the destination when the port has built it, and otherwise the same fact
    /// said plainly - no destination yet, named - so a button can never claim a
    /// place that does not exist.
    /// </summary>
    public static string Outcome(MainMenuActionInfo info) =>
      info.Built ? info.Destination : $"no destination yet: {info.Destination} is not built";

    /// <summary>How many clicks have been dispatched since the surface was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched action's row (only meaningful after a dispatch).</summary>
    public static MainMenuActionInfo LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. MainMenuUI builds the six buttons once, so the
    /// record always describes the menu that is on screen.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point: every button's click lands here, and nothing else
    /// dispatches a menu action. It names the action it dispatched, records it
    /// for the UAT to read, and says out loud where the action would go and
    /// whether this port has built that place yet - so no click is a silent
    /// no-op, and the console is the menu's own record of what a player pressed.
    /// A row with a screen behind it (Built) then asks the flow for it, through
    /// Game.RequestFrontendScreen - the same logged transition the boot uses, so
    /// the click is what moves the flow.
    /// </summary>
    public static void Dispatch(MainMenuAction action)
    {
      MainMenuActionInfo info = Info(action);
      DispatchCount++;
      LastDispatch = info;
      HasDispatch = true;
      Debug.Log($"[MainMenuActions] click: {info.Id} '{info.Label}' -> {Outcome(info)}");
      if (info.Built) Game.GetInstance()?.RequestFrontendScreen(info.Opens);
    }
  }
}
