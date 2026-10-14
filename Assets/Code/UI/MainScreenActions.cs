using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// The bar's painted slots, in the order the install's own strip paints them
  /// (ui/ingameui.tga, its top 45 rows - measured 2026-10-13, x px of 1024:
  /// Date 11..137, Peasants 144..405, the stone Rank plaque 407..610, Gold
  /// 611..708 with the coin medallion at its left, Honour 711..823 with the
  /// crown, Popularity 824..937 with the face, and a seventh slot 938..1013
  /// that no rollover row names).
  /// </summary>
  public enum MainScreenSlot
  {
    Date = 0,
    Peasants = 1,
    Rank = 2,
    Gold = 3,
    Honour = 4,
    Popularity = 5,

    /// <summary>The right-hand slot the install's rollover table does not name - left as painted.</summary>
    Unnamed = 6,
  }

  /// <summary>What one row of the main screen does: a status field, or a control.</summary>
  public enum MainScreenRowKind
  {
    /// <summary>One of the bar's six status fields (MAINSCREENROLLOVERS079..084).</summary>
    Stat,

    /// <summary>One of the main screen's seven controls (MAINSCREENROLLOVERS085..091).</summary>
    Control,
  }

  /// <summary>
  /// One row of the in-game main screen (GameState.GAMEPLAY): the install's own
  /// id and English text (i18n/en-us.xml's MAINSCREENROLLOVERS079..091 - the
  /// six status fields and the seven controls the shipped screen names), the bar
  /// slot a status field is drawn in, and - for a control - the shipped
  /// destination and the screen THIS port can really open for it.
  ///
  /// Only ONE control has a destination the port has built: Objectives
  /// (MAINSCREENROLLOVERS086), whose own row's text names the mission
  /// objectives screen the port already carries - the briefing screen in its
  /// in-game review (MainScreenActions.Objectives). The other six controls are
  /// carried so they are named rather than absent; the install pins no art
  /// for them (the atlas's icon buttons carry no labels, so which icon is
  /// Delete and which is Reports is NOT in the shipped data), which is why the
  /// port draws no button for them.
  /// </summary>
  public readonly struct MainScreenRow
  {
    public MainScreenRow(MainScreenRowKind kind, string id, string label,
                         MainScreenSlot slot, string note, GameState opens)
    {
      Kind = kind;
      Id = id;
      Label = label;
      Slot = slot;
      Note = note;
      Opens = opens;
    }

    /// <summary>Whether this row is a status field or a control.</summary>
    public readonly MainScreenRowKind Kind;

    /// <summary>The row's shipped id (MAINSCREENROLLOVERS079..091).</summary>
    public readonly string Id;

    /// <summary>The row's shipped English text ('Date', 'Objectives', ...).</summary>
    public readonly string Label;

    /// <summary>The bar slot a status field is drawn in (Unnamed for controls).</summary>
    public readonly MainScreenSlot Slot;

    /// <summary>A stat's own value source, or a control's shipped destination - in words.</summary>
    public readonly string Note;

    /// <summary>
    /// The screen this row opens in THIS port, or GameState.NONE while the port
    /// has not built it. Built is derived from this, not toggled beside it.
    /// </summary>
    public readonly GameState Opens;

    /// <summary>True when this row's destination exists in the port.</summary>
    public bool Built => Opens != GameState.NONE;
  }

  /// <summary>
  /// The main screen's own rows, and its ONE entry point.
  ///
  /// The rows are the install's MAINSCREENROLLOVERS079..091, in their own
  /// order: the six status fields the bar carries (Current Rank, Date, Gold,
  /// Honour, Peasants / Maximum Peasants, Popularity) and the seven controls
  /// (Delete, Objectives, Castle Views, Estate Ownership, Zoom, Reports, Radar
  /// Map). The port's reading of which painted slot carries which field is
  /// S2MMissionState.SlotAttributionNote, said on the report.
  ///
  /// A DATA row (a stat) is not clickable and is drawn into its slot by
  /// MainScreenUI. A CONTROL row is a button where the port has art for it -
  /// today only Objectives, which wears the in-game menus' own small fancy
  /// button (ui/ingame_menus/button_fancy_small.tga) with this row's shipped
  /// text, and opens the mission objectives screen in its in-game review
  /// (Game.RequestViewObjectives) - the shipped screen's second play row,
  /// 'Return to the Game' (MISSIONOBJECTIVESSCREENSTATICTOOLTIP_RETURN_TO_GAME),
  /// is what leaves it again.
  /// </summary>
  public static class MainScreenActions
  {
    /// <summary>The six status fields, in the install's own row order (079..084).</summary>
    public static readonly MainScreenRow[] Stats =
    {
      new(MainScreenRowKind.Stat, "MAINSCREENROLLOVERS079", "Current Rank", MainScreenSlot.Rank,
          "the runtime's own (the crest the stone plaque holds) - this port runs no simulation yet",
          GameState.NONE),
      new(MainScreenRowKind.Stat, "MAINSCREENROLLOVERS080", "Date", MainScreenSlot.Date,
          "the loaded mission's own decoded data (the Scenario's starting year + 0-based month, "
          + "with the install's MONTH_* row for the month's name)",
          GameState.NONE),
      new(MainScreenRowKind.Stat, "MAINSCREENROLLOVERS081", "Gold", MainScreenSlot.Gold,
          "the loaded mission's own decoded data (Mission.StartingGold)",
          GameState.NONE),
      new(MainScreenRowKind.Stat, "MAINSCREENROLLOVERS082", "Honour", MainScreenSlot.Honour,
          "the runtime's own - the mission's data carries no honour and this port runs no simulation yet",
          GameState.NONE),
      new(MainScreenRowKind.Stat, "MAINSCREENROLLOVERS083", "Peasants / Maximum Peasants", MainScreenSlot.Peasants,
          "the runtime's own - the mission's data carries no peasants and this port runs no simulation yet",
          GameState.NONE),
      new(MainScreenRowKind.Stat, "MAINSCREENROLLOVERS084", "Popularity", MainScreenSlot.Popularity,
          "the loaded mission's own decoded data (Mission.StartingPopularity)",
          GameState.NONE),
    };

    /// <summary>The seven controls, in the install's own row order (085..091).</summary>
    public static readonly MainScreenRow[] Controls =
    {
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS085", "Delete", MainScreenSlot.Unnamed,
          "the demolish tool (the shipped screen's own mode, no screen of its own)",
          GameState.NONE),
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS086", "Objectives", MainScreenSlot.Unnamed,
          "the mission objectives (the briefing screen in its in-game review, 'Return to the Game')",
          GameState.BRIEFING),
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS087", "Castle Views", MainScreenSlot.Unnamed,
          "the castle-view row (CASTLEVIEWROLLOVERS092..099: Architects View, Lord's Eye View, "
          + "Free View, ...)",
          GameState.NONE),
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS088", "Estate Ownership", MainScreenSlot.Unnamed,
          "the estate-ownership overlay (the maps' own estates)",
          GameState.NONE),
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS089", "Zoom", MainScreenSlot.Unnamed,
          "the in-game camera's zoom",
          GameState.NONE),
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS090", "Reports", MainScreenSlot.Unnamed,
          "the report screens (the install's ui/reports/ pictures)",
          GameState.NONE),
      new(MainScreenRowKind.Control, "MAINSCREENROLLOVERS091", "Radar Map (left click to move)", MainScreenSlot.Unnamed,
          "the radar map (the map's own decoded RadarMap layer)",
          GameState.NONE),
    };

    /// <summary>Every row the main screen carries, in the install's own order (079..091).</summary>
    public static readonly MainScreenRow[] All = BuildAll();

    /// <summary>The one control the port has built: MAINSCREENROLLOVERS086.</summary>
    public static MainScreenRow Objectives => Controls[1];

    /// <summary>The name the Objectives row's button carries in the scene.</summary>
    public const string ObjectivesButtonName = "ObjectivesButton";

    /// <summary>
    /// The one wording of what a row is, shared by the log line and the UAT:
    /// a stat says where its value comes from, and a control names its
    /// destination when the port has built it - and otherwise the same fact said
    /// plainly, no destination yet, named - so no row can claim a place that
    /// does not exist.
    /// </summary>
    public static string Outcome(MainScreenRow row) =>
      row.Kind == MainScreenRowKind.Stat
        ? row.Note
        : row.Built ? row.Note : $"no destination yet: {row.Note} is not built";

    /// <summary>How many clicks have been dispatched since the screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static MainScreenRow LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. MainScreenUI builds its buttons once per showing,
    /// so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: the Objectives button's click lands here
    /// (only the Objectives row has a button - the install pins no art for the
    /// other six controls, so the port draws none of them). Names the row,
    /// records it, reports what it does - and then does it: the mission
    /// objectives ask the flow for the briefing screen in its in-game review
    /// (Game.RequestViewObjectives, the one ask a main-screen click makes).
    /// </summary>
    public static void Dispatch(MainScreenRow row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;
      Debug.Log($"[MainScreenActions] click: {row.Id} '{row.Label}' -> {Outcome(row)}");

      if (row.Opens == GameState.BRIEFING)
      {
        Game.GetInstance()?.RequestViewObjectives();
      }
    }

    private static MainScreenRow[] BuildAll()
    {
      var all = new MainScreenRow[Stats.Length + Controls.Length];
      Stats.CopyTo(all, 0);
      Controls.CopyTo(all, Stats.Length);
      return all;
    }
  }
}
