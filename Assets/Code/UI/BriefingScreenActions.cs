using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>What a briefing-screen click does - the screen's own three kinds of row.</summary>
  public enum BriefingActionKind
  {
    /// <summary>One of the shipped screen's three tabs (Objective / Hints and Tips / Tutorial).</summary>
    Tab,

    /// <summary>One hint back along the chapter's hints.</summary>
    PageBack,

    /// <summary>One hint on along the chapter's hints.</summary>
    PageNext,

    /// <summary>The one row that LEAVES the briefing: 'To the Game', which starts the mission.</summary>
    Leave,

    /// <summary>
    /// The briefing screen's other play row, 'Return to the Game' - the row the
    /// IN-GAME REVIEW wears (the main screen's Objectives row opens it). Same
    /// kind of click: it leaves the screen for the mission.
    /// </summary>
    Return,
  }

  /// <summary>
  /// One row of the briefing screen (GameState.BRIEFING) - the mission-start
  /// screen the clicked chapter's map puts up. A row carries the install's own
  /// id and English text where the table ships a row for it, the install's own
  /// picture, and the name its button carries in the scene.
  ///
  /// The three TAB rows are the shipped screen's own
  /// (MISSIONOBJECTIVESSCREENSTATICTOOLTIP_OBJECTIVE / _HINTS / _TUTORIAL), each
  /// with the install's off/on art pair from ui/briefing_screen/. The two play
  /// rows are the shipped screen's own too, and which one is drawn says which
  /// showing this is: the START briefing wears _PLAY 'To the Game' (the LEAVE
  /// row, reached at mission start after the map loads), and the IN-GAME REVIEW
  /// wears _RETURN_TO_GAME 'Return to the Game' (the RETURN row, reached from
  /// the main screen's Objectives control, MAINSCREENROLLOVERS086). The two PAGING rows have no
  /// shipped row of their own: the install ships the medallions' art
  /// (hint_open / hint_closed / hint_highlight) beside the hint_bar, and the
  /// port uses the rolled one for the two pages and names that reading here.
  /// </summary>
  public readonly struct BriefingAction
  {
    public BriefingAction(string id, string label, BriefingActionKind kind,
                          string texture, string hoverTexture, string buttonName,
                          string outcome)
    {
      Id = id;
      Label = label;
      Kind = kind;
      Texture = texture;
      HoverTexture = hoverTexture;
      ButtonName = buttonName;
      Outcome = outcome;
    }

    /// <summary>The row's shipped id, or "" for the two paging rows (the table ships none).</summary>
    public readonly string Id;

    /// <summary>The row's shipped English text (a tab's label, 'To the Game').</summary>
    public readonly string Label;

    /// <summary>What the click does.</summary>
    public readonly BriefingActionKind Kind;

    /// <summary>The install's own picture for the row (its off state, where it has states).</summary>
    public readonly string Texture;

    /// <summary>The install's own picture for the row's on/active state, or null.</summary>
    public readonly string HoverTexture;

    /// <summary>The name the row's button carries in the scene ("ToTheGameButton").</summary>
    public readonly string ButtonName;

    /// <summary>What a click reports: where it goes, or what it does to the screen.</summary>
    public readonly string Outcome;
  }

  /// <summary>
  /// The briefing screen's own actions, and its ONE entry point.
  ///
  /// Every row is the install's: the three tabs are its own screen rows with its
  /// own button art, and 'To the Game' is its own play row. The screen is the
  /// mission's, not the frontend's: its click does not move between frontend
  /// screens, it either switches the panel on this screen (a tab, a page) or
  /// asks the flow to leave the briefing for the mission
  /// (Game.RequestLeaveBriefing) - the game's own screen wearing the frontend's
  /// discipline.
  /// </summary>
  public static class BriefingScreenActions
  {
    /// <summary>The shipped 'Objective' tab - the subscript's scroll.</summary>
    public static readonly BriefingAction Objective = new(
      "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_OBJECTIVE", "Objective", BriefingActionKind.Tab,
      UIAssets.BriefingObjectivesTexture, UIAssets.BriefingObjectivesOnTexture,
      "ObjectiveTabButton", "the Objective panel");

    /// <summary>The shipped 'Hints and Tips' tab - the scroll with the arrow.</summary>
    public static readonly BriefingAction Hints = new(
      "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_HINTS", "Hints and Tips", BriefingActionKind.Tab,
      UIAssets.BriefingHintsTexture, UIAssets.BriefingHintsOnTexture,
      "HintsTabButton", "the chapter's hints");

    /// <summary>The shipped 'Tutorial' tab - the open book.</summary>
    public static readonly BriefingAction Tutorial = new(
      "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_TUTORIAL", "Tutorial", BriefingActionKind.Tab,
      UIAssets.BriefingTutorialTexture, UIAssets.BriefingTutorialOnTexture,
      "TutorialTabButton", "the Tutorial panel");

    /// <summary>The three tabs, in the shipped screen's own order (Objective first).</summary>
    public static readonly BriefingAction[] Tabs = { Objective, Hints, Tutorial };

    /// <summary>Back one hint - the install's rolled-scroll medallion (its only row-less control).</summary>
    public static readonly BriefingAction PageBack = new(
      "", "Previous hint", BriefingActionKind.PageBack,
      UIAssets.BriefingPageMedallionTexture, null, "HintBackButton", "one hint back");

    /// <summary>On one hint - the same medallion; the install ships one rolled-scroll picture.</summary>
    public static readonly BriefingAction PageNext = new(
      "", "Next hint", BriefingActionKind.PageNext,
      UIAssets.BriefingPageMedallionTexture, null, "HintNextButton", "one hint on");

    /// <summary>
    /// The one row that leaves the screen: the shipped start-briefing play row,
    /// drawn with the frontend's own fancy button (the table names the row, not
    /// its picture, and this is the button the port labels from the table
    /// everywhere else).
    /// </summary>
    public static readonly BriefingAction Leave = new(
      "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_PLAY", "To the Game", BriefingActionKind.Leave,
      UIAssets.FancyButtonTexture, UIAssets.FancyButtonHighlightTexture,
      "ToTheGameButton", "the mission (the briefing is done)");

    /// <summary>
    /// The in-game review's own way back: the shipped
    /// MISSIONOBJECTIVESSCREENSTATICTOOLTIP_RETURN_TO_GAME 'Return to the Game'
    /// row, which the main screen's Objectives control opens the screen with.
    /// Its click reaches the same ask the start briefing's does
    /// (Game.RequestLeaveBriefing), so the review cannot become a third route
    /// into the mission.
    /// </summary>
    public static readonly BriefingAction ReturnToGame = new(
      "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_RETURN_TO_GAME", "Return to the Game", BriefingActionKind.Leave,
      UIAssets.FancyButtonTexture, UIAssets.FancyButtonHighlightTexture,
      "ReturnToGameButton", "the mission (the review is done)");

    /// <summary>
    /// The play row one showing of the screen wears: the start briefing's 'To
    /// the Game', or - for the in-game review the main screen's Objectives
    /// control opens - 'Return to the Game'.
    /// </summary>
    public static BriefingAction LeaveFor(bool review) => review ? ReturnToGame : Leave;

    /// <summary>
    /// The START briefing's own six buttons: the three tabs, the two pages, and
    /// 'To the Game'. The in-game review wears the same six with
    /// <see cref="ReturnToGame"/> in place of <see cref="Leave"/> - see
    /// <see cref="RowsFor"/>.
    /// </summary>
    public static readonly BriefingAction[] All = BuildAll();

    /// <summary>The six rows one showing of the screen carries (the review swaps the play row).</summary>
    public static BriefingAction[] RowsFor(bool review)
    {
      if (!review) return All;
      var rows = (BriefingAction[])All.Clone();
      rows[rows.Length - 1] = ReturnToGame;
      return rows;
    }

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static BriefingAction LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. BriefingScreenUI builds its buttons once per
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
    /// nothing else dispatches a briefing action. Names the row, records it,
    /// reports what it does - and then does it: a tab or a page is applied to
    /// the screen that is up (the panel switches), and 'To the Game' asks the
    /// flow to leave the briefing (Game.RequestLeaveBriefing, the one ask that
    /// starts the mission).
    /// </summary>
    public static void Dispatch(BriefingAction action)
    {
      DispatchCount++;
      LastDispatch = action;
      HasDispatch = true;

      string id = string.IsNullOrEmpty(action.Id) ? "(no shipped row)" : action.Id;
      Debug.Log($"[BriefingScreenActions] click: {id} '{action.Label}' -> {action.Outcome}");

      if (action.Kind == BriefingActionKind.Leave)
      {
        Game.GetInstance()?.RequestLeaveBriefing();
        return;
      }

      Game.GetInstance()?.briefingScreenUI?.Apply(action);
    }

    private static BriefingAction[] BuildAll()
    {
      var all = new BriefingAction[Tabs.Length + 3];
      Tabs.CopyTo(all, 0);
      all[Tabs.Length] = PageBack;
      all[Tabs.Length + 1] = PageNext;
      all[Tabs.Length + 2] = Leave;
      return all;
    }
  }
}
