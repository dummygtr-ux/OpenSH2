using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// One row of the campaign screen (GameState.CAMPAIGN_SCREEN) - the chapter
  /// list the war screen's Campaign row opens - and the whole of what this port
  /// knows about it.
  ///
  /// A chapter row carries TWO shipped table rows, which is why it is not a
  /// <see cref="PathScreenRow"/>: its label is the campaign's own chapter row
  /// (WARCAMPAIGN_MISSION1..12 'Chapter 1'..'Chapter 12'), and the button's own
  /// row in the screen's table is the static tooltip it shows
  /// (CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12 'Start Chapter 1'..'Start Chapter
  /// 12'). Both are the install's, checked against i18n/en-us.xml at run time.
  /// The way back is one row like the path screens' (CAMPAIGNSCREENSTATICTOOLTIP_
  /// EXIT 'Previous Menu'), and carries no second id.
  ///
  /// The install names the campaign's chapters a second way -
  /// WAR_CAMPAIGN_HEADING00..11, the twelve titles ('The Search for the King',
  /// 'First Command', ...) - and no CAMPAIGNSCREEN* row names them, so this
  /// screen does not draw them; they belong to the briefing screens
  /// (ui/chapter_introductions/chapter01..12_intro.jpg), left for later work.
  /// </summary>
  public readonly struct CampaignScreenRow
  {
    public CampaignScreenRow(string id, string labelId, string label, string tooltip,
                             string destination, string mapPath, GameState opens,
                             string texture, string buttonName)
    {
      Id = id;
      LabelId = labelId;
      Label = label;
      Tooltip = tooltip;
      Destination = destination;
      MapPath = mapPath;
      Opens = opens;
      Texture = texture;
      ButtonName = buttonName;
    }

    /// <summary>The row's own shipped id - the button's static tooltip row (i18n/en-us.xml).</summary>
    public readonly string Id;

    /// <summary>The shipped id the button's LABEL comes from, where it is not the row's own id.</summary>
    public readonly string LabelId;

    /// <summary>The row's shipped English text - the button's label ('Chapter 1'..'Chapter 12').</summary>
    public readonly string Label;

    /// <summary>
    /// The shipped static tooltip CONTENT - what the shipped screen shows for
    /// this button ('Start Chapter 1'..'Start Chapter 12'). This port shows the
    /// label on the button and keeps the content on the row; the art's own
    /// description bar carries the way back instead (the path screens' own
    /// decision, taken again here), so no hover panel is built.
    /// </summary>
    public readonly string Tooltip;

    /// <summary>
    /// What this row would open in the shipped game, named from the install's
    /// own data: the chapter's own map file, maps/war_chapter1..12.s2m (twelve
    /// files, one per chapter row - the map every harness here loads,
    /// war_chapter8.s2m, is chapter 8's).
    /// </summary>
    public readonly string Destination;

    /// <summary>
    /// The chapter's own map file, install-relative (maps/war_chapterN.s2m) -
    /// what a click STARTS (Game.RequestStartChapter). A chapter row carries
    /// this instead of a <see cref="GameState"/> because the destination is a
    /// map, not a frontend screen; the way back's row carries null and a screen
    /// exactly as the path screens' rows do.
    /// </summary>
    public readonly string MapPath;

    /// <summary>
    /// The screen this action opens in THIS port, or GameState.NONE while the
    /// port has not built it. <see cref="Built"/> is derived from this, not
    /// toggled beside it.
    /// </summary>
    public readonly GameState Opens;

    /// <summary>
    /// The install's own picture for this row: campaign_button.bmp for the
    /// twelve chapter rows (one button, reused - the install ships it once),
    /// and the frontend's fancy button for the way back (the table names that
    /// row, not its picture).
    /// </summary>
    public readonly string Texture;

    /// <summary>The name the row's button carries in the scene ("Chapter1Button").</summary>
    public readonly string ButtonName;

    /// <summary>
    /// True when this row's destination exists in the port: a screen behind it
    /// (<see cref="Opens"/>) or a map its click starts (<see cref="MapPath"/>).
    /// </summary>
    public bool Built => Opens != GameState.NONE || !string.IsNullOrEmpty(MapPath);

    /// <summary>
    /// The one wording of what a click did - the destination when the port has
    /// built it, otherwise the same fact said plainly, named. Shared by the log
    /// line and the UAT.
    /// </summary>
    public string Outcome =>
      Built ? Destination : $"no destination yet: {Destination} is not built";
  }
}
