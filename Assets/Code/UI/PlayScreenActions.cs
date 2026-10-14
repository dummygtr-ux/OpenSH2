using UnityEngine;
using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// The play screen's own actions, in the shipped screen's own order: the two
  /// paths, the tutorial, and the way back.
  ///
  /// Where these come from: the same shipped translation table as the main
  /// menu's six (i18n/en-us.xml - the shipped help file documents the game, not
  /// the frontend, so it has nothing to say about this screen either). The
  /// screen itself is PLAYSCREEN 'Choose Your Path' - the row MAINMENU_PLAY
  /// points at - and its own rows name the title, the two path tooltips, the
  /// tutorial and the way back to the menu.
  /// </summary>
  public enum PlayScreenAction
  {
    War = 0,
    Peace = 1,
    Tutorial = 2,
    Back = 3,
  }

  /// <summary>
  /// One action's shipped identity and where it leads. <see cref="Label"/> is
  /// the shipped English text of the row the button's picture belongs to, and
  /// <see cref="Tooltip"/> carries the shipped tooltip CONTENT where the table
  /// has one (the two paths describe themselves on hover: 'Combat intensive
  /// styles of play.' / 'Gentler game modes, focusing mainly on castle life.')
  /// - this port shows the label on the button and keeps the content on the row
  /// rather than building a hover panel. <see cref="Opens"/> is the screen the
  /// port can really show, GameState.NONE where it has none, so Built is
  /// derived and can never disagree with it.
  /// </summary>
  public readonly struct PlayScreenActionInfo
  {
    public PlayScreenActionInfo(PlayScreenAction action, string id, string label,
                               string tooltip, string destination, GameState opens)
    {
      Action = action;
      Id = id;
      Label = label;
      Tooltip = tooltip;
      Destination = destination;
      Opens = opens;
    }

    public readonly PlayScreenAction Action;
    public readonly string Id;
    public readonly string Label;
    public readonly string Tooltip;
    public readonly string Destination;
    public readonly GameState Opens;

    /// <summary>True when this action's destination exists in the port.</summary>
    public bool Built => Opens != GameState.NONE;
  }

  /// <summary>
  /// The play screen's action surface: its four rows, and THE one entry point a
  /// click arrives through - the same discipline <see cref="MainMenuActions"/>
  /// follows for the menu (one entry point, the action named on the console,
  /// the click recorded, and the destination reported honestly whether the port
  /// has built it or not).
  /// </summary>
  public static class PlayScreenActions
  {
    /// <summary>
    /// The four rows' shipped identities. The two paths open the screens the
    /// same table names - the war screen's WARSCREEN_* rows (Campaign / Conquest
    /// Trail / Kingmaker / Siege / Custom / Load) and the peace screen's
    /// PEACESCREEN_* rows (Sim Campaign / Free Build / Custom Maps), whose button
    /// pictures the install ships in ui/war_peace_menu/ - and as of 2026-10-12
    /// both of those screens exist in this port
    /// (GameState.WAR_SCREEN / GameState.PEACE_SCREEN), so these two rows carry
    /// them. The tutorial does not: its screen is not built. The way back is the
    /// one this port built first: PLAYSCREENSTATICTOOLTIP_PREVIOUS_MENU
    /// 'Previous Menu' returns to MAINMENU_PLAY's screen, which the port has.
    /// </summary>
    public static readonly PlayScreenActionInfo[] All =
    {
      new(PlayScreenAction.War, "PLAYSCREEN_WAR_TOOLTIP_TITLE", "The Path of War",
          "Combat intensive styles of play.",
          "the war screen (WARSCREEN_* Campaign / Conquest Trail / Kingmaker / Siege / Custom / Load)",
          GameState.WAR_SCREEN),
      new(PlayScreenAction.Peace, "PLAYSCREEN_PEACE_TOOLTIP_TITLE", "The Path of Peace",
          "Gentler game modes, focusing mainly on castle life.",
          "the peace screen (PEACESCREEN_* Sim Campaign / Free Build / Custom Maps)",
          GameState.PEACE_SCREEN),
      new(PlayScreenAction.Tutorial, "PLAYSCREENSTATICTOOLTIP_TUTORIAL", "Play the Tutorial",
          "",
          "the tutorial ('Play the Tutorial')",
          GameState.NONE),
      new(PlayScreenAction.Back, "PLAYSCREENSTATICTOOLTIP_PREVIOUS_MENU", "Previous Menu",
          "",
          "the main menu (the screen MAINMENU_PLAY came from, which the port has)",
          GameState.MAIN_MENU),
    };

    /// <summary>The row for one action (the enum values index <see cref="All"/>).</summary>
    public static PlayScreenActionInfo Info(PlayScreenAction action) => All[(int)action];

    /// <summary>
    /// The one wording of what a click did - the destination when the port has
    /// built it, otherwise the same fact said plainly, named.
    /// </summary>
    public static string Outcome(PlayScreenActionInfo info) =>
      info.Built ? info.Destination : $"no destination yet: {info.Destination} is not built";

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched action's row (only meaningful after a dispatch).</summary>
    public static PlayScreenActionInfo LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. PlayScreenUI builds its four buttons once per
    /// showing, so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a play-screen action. Names the action, records
    /// it, reports its destination - and, for the one row the port has built
    /// (Back), asks the flow for it through the same logged transition the
    /// menu's Play uses.
    /// </summary>
    public static void Dispatch(PlayScreenAction action)
    {
      PlayScreenActionInfo info = Info(action);
      DispatchCount++;
      LastDispatch = info;
      HasDispatch = true;
      Debug.Log($"[PlayScreenActions] click: {info.Id} '{info.Label}' -> {Outcome(info)}");
      if (info.Built) Game.GetInstance()?.RequestFrontendScreen(info.Opens);
    }
  }
}
