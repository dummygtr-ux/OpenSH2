using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>What one of the trail screen's own buttons does.</summary>
  public enum ConquestScreenActionKind
  {
    /// <summary>One castle back along the trail (CONQUEST_PREV_CASTLE).</summary>
    PreviousCastle,

    /// <summary>One castle on along the trail (CONQUEST_NEXT_CASTLE).</summary>
    NextCastle,

    /// <summary>
    /// The row that STARTS the selected castle's mission (CONQUEST START MISSION).
    /// The port's flow does not load the trail's maps yet, so this click names
    /// what it would load rather than doing it.
    /// </summary>
    StartMission,

    /// <summary>
    /// The row the trail wears once it is FINISHED (CONQUEST START AGAIN, whose
    /// shipped text is the restart question). Same kind of click as the start row:
    /// the port has no trail progress to start again, so it names that.
    /// </summary>
    StartAgain,

    /// <summary>The way back to the war screen the trail was opened from.</summary>
    Back,
  }

  /// <summary>
  /// One of the trail's twelve shipped text rows (CONQUESTTRAIL_1..12): the
  /// install's own id, the install's own English text ('Your first mission is to
  /// defend Guadamur castle in Spain.', ... 'Congratulations! ...'), and - where
  /// the text names a castle - the castle's own facts, measured from the install
  /// rather than guessed.
  ///
  /// The castle a row names is the row's own text's business: every one of the
  /// first eleven rows names its castle in prose (Guadamur, Javier, The
  /// Heuneburg, Wartburg, Haut-Koenigsburg, Fougères, Windsor, Harlech,
  /// Criccieth, Edinburgh, and Wartburg again for the retake), and the twelfth
  /// row is the campaign's completion message and names none. The port carries
  /// the name as the text writes it (<see cref="Castle"/>, checked against the
  /// text at run time), the install's OWN file key for that castle
  /// (<see cref="CastleKey"/> - the spelling its files use, which differs from
  /// the prose: 'Haut-Koenigsburg' in the text, hautkoenigsbourg in the file),
  /// the castle's painting (ui/siege_menu/castle_&lt;key&gt;_painting.jpg) and
  /// the castle's own scenario map (maps/&lt;castle&gt;.s2c - the install's
  /// historical castle maps, 19 of them, and all ten of the trail's castles are
  /// among them).
  ///
  /// The painting is CARRIED, not drawn: the trail screen's own art paints its
  /// castles as labels on the map and pins no place for a painting (the
  /// paintings belong to the siege selection screen, which ships in the same
  /// folder). The map is carried for the same reason - what a trail mission
  /// would LOAD is named on the report, and loading it is a later item.
  /// </summary>
  public readonly struct ConquestTrailRow
  {
    public ConquestTrailRow(int position, string id, string text, string castle,
                            string castleKey, string paintingPath, string mapPath)
    {
      Position = position;
      Id = id;
      Text = text;
      Castle = castle;
      CastleKey = castleKey;
      PaintingPath = paintingPath;
      MapPath = mapPath;
    }

    /// <summary>Where this row stands along the trail, 1-based (1..12).</summary>
    public readonly int Position;

    /// <summary>The row's shipped id (CONQUESTTRAIL_1..12).</summary>
    public readonly string Id;

    /// <summary>The row's shipped English text - the trail screen's own line for this position.</summary>
    public readonly string Text;

    /// <summary>
    /// The castle this row's text names, as the text spells it ("" for the last
    /// row, which names none - it is the trail's completion message).
    /// </summary>
    public readonly string Castle;

    /// <summary>The install's own file key for the castle ("" where no castle is named).</summary>
    public readonly string CastleKey;

    /// <summary>The castle's painting in the install ("" where no castle is named).</summary>
    public readonly string PaintingPath;

    /// <summary>
    /// The castle's own scenario map, install-relative (maps/&lt;castle&gt;.s2c) -
    /// what a click on 'Start Mission' at this position would load. "" where no
    /// castle is named. The path's spelling is the install's own (Fougères keeps
    /// its accent: maps/fougères.s2c).
    /// </summary>
    public readonly string MapPath;

    /// <summary>True when this row names a castle (the first eleven do; the last does not).</summary>
    public bool NamesACastle => Castle.Length > 0;
  }

  /// <summary>
  /// One of the trail screen's own buttons - the screen's whole action surface.
  /// Every one of them carries the install's own table row: the two paging
  /// buttons are CONQUEST_PREV_CASTLE / CONQUEST_NEXT_CASTLE ('Go Back one
  /// Castle along the trail.' / 'Go Forward one Castle along the trail.') with
  /// their own disabled wordings (CONQUEST_NOPREV_CASTLE 'You are at the start
  /// of the trail.' / CONQUEST_NONEXT_CASTLE 'You are currently at your furthest
  /// point along the trail.'), the start button is CONQUEST START MISSION
  /// ('Start Mission') or, at the trail's end, CONQUEST START AGAIN ('Do you
  /// wish to take up the challenge again?'), and the way back is the frontend's
  /// own exit row (WARSCREENSTATICTOOLTIP_EXIT 'Previous menu') - the trail
  /// screen ships NO exit row of its own, so its way back wears the parent
  /// screen's, the same wording every frontend way back carries.
  ///
  /// The paging buttons' art is the install's own too
  /// (ui/gold_edition/conquest_trail_arrowback/forward(.tga), 52x52, opaque
  /// 41x41 at +6) - and the trail screen's own backdrop paints the very same
  /// tiles where the shipped screen draws them (measured: the 52x52 art stands
  /// at x 732 and x 782, y 507 of the 1024x768 picture), so the buttons stand on
  /// the painted tiles rather than beside them.
  /// </summary>
  public readonly struct ConquestScreenAction
  {
    public ConquestScreenAction(ConquestScreenActionKind kind, string id, string label,
                                string tooltip, string disabledId, string disabledText,
                                string destination, GameState opens, string texture, string buttonName)
    {
      Kind = kind;
      Id = id;
      Label = label;
      Tooltip = tooltip;
      DisabledId = disabledId;
      DisabledText = disabledText;
      Destination = destination;
      Opens = opens;
      Texture = texture;
      ButtonName = buttonName;
    }

    /// <summary>What the click does.</summary>
    public readonly ConquestScreenActionKind Kind;

    /// <summary>The row's shipped id (i18n/en-us.xml).</summary>
    public readonly string Id;

    /// <summary>The row's shipped English text - 'Start Mission', 'Previous menu', or a tooltip for the arrows.</summary>
    public readonly string Label;

    /// <summary>
    /// The row's shipped static tooltip CONTENT where the table ships one beside
    /// the row (the two paging arrows' own rows are their tooltips: 'Go Forward
    /// one Castle along the trail.'). This port keeps the content on the row -
    /// the arrows draw the install's own arrow art, which needs no label on it.
    /// </summary>
    public readonly string Tooltip;

    /// <summary>The shipped row for this button's DISABLED state (the two arrows' ends), or "".</summary>
    public readonly string DisabledId;

    /// <summary>The disabled row's shipped English - the wording a click at the trail's end names.</summary>
    public readonly string DisabledText;

    /// <summary>What a click reaches, or what it would reach, in words.</summary>
    public readonly string Destination;

    /// <summary>The screen this button opens in THIS port, or GameState.NONE.</summary>
    public readonly GameState Opens;

    /// <summary>The install's own picture for the button.</summary>
    public readonly string Texture;

    /// <summary>The name the button carries in the scene ("StartMissionButton").</summary>
    public readonly string ButtonName;

    /// <summary>
    /// True when the port really does what this row says: the two paging
    /// buttons move the trail (the port owns the position), and the way back
    /// opens the war screen. The start button is NOT built - the port's flow
    /// loads the campaign's .s2m chapters and the trail's missions are .s2c
    /// historical maps, a later item - so its click names what it would load
    /// rather than doing it.
    /// </summary>
    public bool Built => Kind switch
    {
      ConquestScreenActionKind.PreviousCastle => true,
      ConquestScreenActionKind.NextCastle => true,
      ConquestScreenActionKind.Back => Opens == GameState.WAR_SCREEN,
      _ => false,
    };

    /// <summary>
    /// The one wording of what a click reports, shared by the log line and the
    /// UAT: the destination when the port has built it (the two paging arrows and
    /// the way back), and otherwise the same fact said plainly, named. The two
    /// start rows say more than that - they name the SELECTED castle's own
    /// mission - so their wording is composed with the trail's position by the
    /// screen's own entry point (ConquestScreenActions.OutcomeFor), which is what
    /// the log line and the UAT read.
    /// </summary>
    public string Outcome => Built ? Destination : $"no destination yet: {Destination} is not built";
  }
}
