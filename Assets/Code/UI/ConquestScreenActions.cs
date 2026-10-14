using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// The Conquest Trail screen's own actions, and its ONE entry point.
  ///
  /// Where they come from: the same shipped translation table as every other
  /// frontend surface here (i18n/en-us.xml). The trail's family in that table is
  /// small and complete - the twelve rows the trail is made of
  /// (CONQUESTTRAIL_1..12: one mission brief per castle the campaign crosses,
  /// and the last row the campaign's completion message), its two paging rows
  /// with their own disabled states (CONQUEST_PREV_CASTLE / CONQUEST_NEXT_CASTLE
  /// and _NOPREV_CASTLE / _NONEXT_CASTLE), and its two start rows (CONQUEST
  /// START MISSION 'Start Mission' and CONQUEST START AGAIN, whose shipped text
  /// is the restart question). The screen's own exit row is NOT in the table -
  /// there is no CONQUEST*EXIT - so the way back wears the war screen's own
  /// WARSCREENSTATICTOOLTIP_EXIT 'Previous menu', the row the frontend's exits
  /// all carry the same words of.
  ///
  /// The trail's progress is the PORT's own reading, and it is deliberately
  /// small: this port has no save/progress data, so the trail opens where the
  /// shipped campaign starts - at its first castle - and the player may page
  /// along all twelve rows to read them. The install's own two disabled wordings
  /// are worn at the trail's own ends ('You are at the start of the trail.' at
  /// position 1, and - where there is no further castle - 'You are currently at
  /// your furthest point along the trail.', which the shipped game ties to the
  /// player's furthest reached castle and this port has no progress record for).
  /// Both are the install's words, checked against the table at run time.
  /// </summary>
  public static class ConquestScreenActions
  {
    /// <summary>How many rows the trail is made of (CONQUESTTRAIL_1..12).</summary>
    public const int TrailCount = 12;

    /// <summary>How many castles the trail's texts name (ten - Wartburg is named twice).</summary>
    public const int CastleCount = 10;

    /// <summary>
    /// The trail's twelve rows, in the trail's own order. Each one is the
    /// install's row of that id, and each one's text names its castle where the
    /// campaign has one - the castle's own facts (its painting and its
    /// historical map) ride along, measured from the install, so the trail can
    /// say what a mission would load even though this port does not load it yet.
    /// </summary>
    public static readonly ConquestTrailRow[] Trail =
    {
      new(1, "CONQUESTTRAIL_1", "Your first mission is to defend Guadamur castle in Spain.",
          "Guadamur", "guadamur", UIAssets.CastlePaintingTexture("guadamur"), "maps/guadamur.s2c"),
      new(2, "CONQUESTTRAIL_2", "Your next mission is to Siege Javier in the mountains of the Pyrenees.",
          "Javier", "javier", UIAssets.CastlePaintingTexture("javier"), "maps/javier.s2c"),
      new(3, "CONQUESTTRAIL_3", "Your next mission is to protect the ancient wooden fortress of 'The Heuneburg'.",
          "The Heuneburg", "heuneburg", UIAssets.CastlePaintingTexture("heuneburg"), "maps/heuneburg.s2c"),
      new(4, "CONQUESTTRAIL_4", "Your next mission is to defend the cliff top castle of Wartburg against heavy attack.",
          "Wartburg", "wartburg", UIAssets.CastlePaintingTexture("wartburg"), "maps/wartburg.s2c"),
      new(5, "CONQUESTTRAIL_5", "Next you must cross to France to seize the disputed castle of Haut-Koenigsburg.",
          "Haut-Koenigsburg", "hautkoenigsbourg", UIAssets.CastlePaintingTexture("hautkoenigsbourg"),
          "maps/haut-koenigsbourg.s2c"),
      new(6, "CONQUESTTRAIL_6", "Your next target is Fougères to the west.",
          "Fougères", "fougeres", UIAssets.CastlePaintingTexture("fougeres"), "maps/fougères.s2c"),
      new(7, "CONQUESTTRAIL_7", "Next you must cross to England and take the royal castle of Windsor.",
          "Windsor", "windsor", UIAssets.CastlePaintingTexture("windsor"), "maps/windsor.s2c"),
      new(8, "CONQUESTTRAIL_8", "England is ours but the Welsh are rebelling, hurry to the aid of Harlech.",
          "Harlech", "harlech", UIAssets.CastlePaintingTexture("harlech"), "maps/harlech.s2c"),
      new(9, "CONQUESTTRAIL_9", "To secure Wales we must prevent the rebels from taking Criccieth castle further up the coast.",
          "Criccieth", "criccieth", UIAssets.CastlePaintingTexture("criccieth"), "maps/criccieth.s2c"),
      new(10, "CONQUESTTRAIL_10", "The rebels are launching one final campaign to defeat us in Scotland.  Hold Edinburgh castle.",
          "Edinburgh", "edinburgh", UIAssets.CastlePaintingTexture("edinburgh"), "maps/edinburgh.s2c"),
      new(11, "CONQUESTTRAIL_11", "Just as we thought peace was within our grasp Wartburg castle has been taken.  Return to Germany and retake it.",
          "Wartburg", "wartburg", UIAssets.CastlePaintingTexture("wartburg"), "maps/wartburg.s2c"),
      new(12, "CONQUESTTRAIL_12", "Congratulations!  Your enemies have been defeated, peace has been restored and Europe is once again united under your royal standard.",
          "", "", "", ""),
    };

    /// <summary>The row for one position of the trail (1-based; 1..12).</summary>
    public static ConquestTrailRow TrailRow(int position) => Trail[position - 1];

    /// <summary>
    /// One castle back along the trail (CONQUEST_PREV_CASTLE 'Go Back one Castle
    /// along the trail.'), drawn with the install's own left-arrow tile. At the
    /// trail's start its shipped disabled row says so ('You are at the start of
    /// the trail.').
    /// </summary>
    public static readonly ConquestScreenAction PreviousCastle = new(
      ConquestScreenActionKind.PreviousCastle, "CONQUEST_PREV_CASTLE",
      "Go Back one Castle along the trail.", "Go Back one Castle along the trail.",
      "CONQUEST_NOPREV_CASTLE", "You are at the start of the trail.",
      "the trail's previous castle (its own CONQUESTTRAIL_N row is the one shown)",
      GameState.NONE, UIAssets.ConquestTrailArrowBackTexture, "PreviousCastleButton");

    /// <summary>
    /// One castle on along the trail (CONQUEST_NEXT_CASTLE 'Go Forward one Castle
    /// along the trail.'), drawn with the install's own right-arrow tile. Where
    /// there is no further castle its shipped disabled row says so ('You are
    /// currently at your furthest point along the trail.').
    /// </summary>
    public static readonly ConquestScreenAction NextCastle = new(
      ConquestScreenActionKind.NextCastle, "CONQUEST_NEXT_CASTLE",
      "Go Forward one Castle along the trail.", "Go Forward one Castle along the trail.",
      "CONQUEST_NONEXT_CASTLE", "You are currently at your furthest point along the trail.",
      "the trail's next castle (its own CONQUESTTRAIL_N row is the one shown)",
      GameState.NONE, UIAssets.ConquestTrailArrowForwardTexture, "NextCastleButton");

    /// <summary>
    /// The start row the trail wears at a position where a mission waits: the
    /// shipped CONQUEST START MISSION 'Start Mission'. Its click names the
    /// selected castle's own mission - the castle's historical map - and that
    /// this port does not load it yet, rather than doing nothing silently.
    /// </summary>
    public static readonly ConquestScreenAction StartMission = new(
      ConquestScreenActionKind.StartMission, "CONQUEST START MISSION", "Start Mission", "",
      "", "",
      "the selected trail mission (its own castle's historical map, maps/<castle>.s2c)",
      GameState.NONE, UIAssets.ConquestStartMissionTexture, "StartMissionButton");

    /// <summary>
    /// The start row the trail wears at its END (position 12, the completion
    /// message): the shipped CONQUEST START AGAIN, whose text is the restart
    /// question the shipped game asks a player who has finished the trail -
    /// 'Do you wish to take up the challenge again?'. This port has no progress
    /// to start again, so the row is carried and its click says so by name.
    /// </summary>
    public static readonly ConquestScreenAction StartAgain = new(
      ConquestScreenActionKind.StartAgain, "CONQUEST START AGAIN",
      "Do you wish to take up the challenge again?", "", "", "",
      "the shipped restart prompt (the trail is finished)",
      GameState.NONE, UIAssets.ConquestStartMissionTexture, "StartMissionButton");

    /// <summary>
    /// The way back ('Previous menu') - the only row on this screen with a
    /// screen behind it, and it is the WAR SCREEN the trail was opened from: the
    /// frontend is a tree, and the trail is a branch of the war screen exactly as
    /// the campaign screen is. The trail ships no exit row of its own, so this
    /// row is the war screen's own shipped exit - the same words every frontend
    /// way back carries.
    /// </summary>
    public static readonly ConquestScreenAction Back = new(
      ConquestScreenActionKind.Back, "WARSCREENSTATICTOOLTIP_EXIT", "Previous menu", "", "", "",
      "the war screen (the screen the Conquest Trail was opened from, which the port has)",
      GameState.WAR_SCREEN, UIAssets.FancyButtonTexture, "BackButton");

    /// <summary>The three buttons every position of the trail carries, in the screen's own order.</summary>
    public static readonly ConquestScreenAction[] FixedButtons = { PreviousCastle, NextCastle, Back };

    /// <summary>
    /// The start row a position wears: the mission row everywhere a castle waits,
    /// and the shipped restart row at the trail's end (position <see cref="TrailCount"/>).
    /// </summary>
    public static ConquestScreenAction StartRowFor(int position) =>
      position >= TrailCount ? StartAgain : StartMission;

    /// <summary>
    /// The screen's four buttons at one position, in the screen's own order: the
    /// two paging arrows, the start row (which position 12 wears as the restart
    /// row), and the way back.
    /// </summary>
    public static ConquestScreenAction[] RowsFor(int position) =>
      new[] { PreviousCastle, NextCastle, StartRowFor(position), Back };

    /// <summary>
    /// Where the trail stands, 1-based (1..12). The screen opens at the first
    /// castle - the port has no progress, so it starts where the shipped campaign
    /// starts - and the paging buttons move it. Both the UI and the UAT read it;
    /// only <see cref="Dispatch"/> writes it.
    /// </summary>
    public static int Position { get; private set; } = 1;

    /// <summary>The trail row the screen is showing.</summary>
    public static ConquestTrailRow Selected => Trail[Position - 1];

    /// <summary>
    /// What the trail's paging state is at one position, in the install's own
    /// words: the two ends name their shipped disabled rows, every position
    /// between them is free to page both ways.
    /// </summary>
    public static string PagingNote(int position) =>
      position <= 1 ? PreviousCastle.DisabledId + " '" + PreviousCastle.DisabledText + "'"
      : position >= TrailCount ? NextCastle.DisabledId + " '" + NextCastle.DisabledText + "'"
      : "both ways along the trail";

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static ConquestScreenAction LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over, and the trail with it: ConquestScreenUI builds
    /// its buttons once per showing, so the record always describes the screen
    /// that is on it - and the trail opens at its first castle every time (the
    /// port has no progress to open it anywhere else).
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
      Position = 1;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a trail action. Names the row, records it, reports
    /// what it does - and then does it: a paging click moves the trail (unless
    /// the position is already at the end it points past, in which case the click
    /// names the install's own disabled wording), the way back asks the flow for
    /// the war screen (Game.RequestFrontendScreen, the move every frontend click
    /// makes), and the start row does not start anything yet - its click has said
    /// so by name.
    /// </summary>
    public static void Dispatch(ConquestScreenAction row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;

      string outcome = OutcomeFor(row, Position);
      Debug.Log($"[ConquestScreenActions] click: {row.Id} '{row.Label}' -> {outcome}");

      switch (row.Kind)
      {
        case ConquestScreenActionKind.PreviousCastle:
          if (Position > 1)
          {
            Position--;
            RefreshScreen();
          }
          return;

        case ConquestScreenActionKind.NextCastle:
          if (Position < TrailCount)
          {
            Position++;
            RefreshScreen();
          }
          return;

        case ConquestScreenActionKind.Back:
          Game.GetInstance()?.RequestFrontendScreen(row.Opens);
          return;

        default:
          // Start Mission / Start Again: the flow loads the campaign's .s2m
          // chapters, and a trail mission is a .s2c historical castle map - a
          // later item. The click has named what it would load.
          return;
      }
    }

    /// <summary>
    /// What one click reports from one position: a paging click that can move
    /// names where it goes, a paging click at the trail's own end names the
    /// install's disabled row for that end, the start row names the selected
    /// castle's own mission, the trail's end names the shipped restart prompt, and
    /// the way back names its screen. The log line and the UAT read the same
    /// wording.
    /// </summary>
    public static string OutcomeFor(ConquestScreenAction row, int position) => row.Kind switch
    {
      ConquestScreenActionKind.PreviousCastle =>
        position > 1
          ? $"the trail's previous castle (CONQUESTTRAIL_{position - 1} is what the screen shows next)"
          : $"{row.DisabledId} '{row.DisabledText}' (the trail is at its start)",
      ConquestScreenActionKind.NextCastle =>
        position < TrailCount
          ? $"the trail's next castle (CONQUESTTRAIL_{position + 1} is what the screen shows next)"
          : $"{row.DisabledId} '{row.DisabledText}' (there is no further castle along the trail)",
      ConquestScreenActionKind.StartMission =>
        $"no destination yet: {Trail[position - 1].Castle}'s own mission "
        + $"({Trail[position - 1].MapPath}, the install's historical castle map) is not built",
      ConquestScreenActionKind.StartAgain =>
        $"no destination yet: the shipped {row.Id} prompt is not built - the port has no trail "
        + "progress to start again",
      _ => row.Outcome,
    };

    /// <summary>The screen's own refresh, where one is up (a paging click's next step).</summary>
    private static void RefreshScreen() =>
      Game.GetInstance()?.conquestScreenUI?.Refresh();
  }
}
