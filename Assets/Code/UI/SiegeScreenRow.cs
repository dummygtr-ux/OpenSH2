using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>What one of the siege selection screen's own buttons does.</summary>
  public enum SiegeScreenActionKind
  {
    /// <summary>
    /// One castle of the list (the install's own HISTORICCASTLE name row): the
    /// click SELECTS that siege, and the screen shows the castle's own painting,
    /// its layout picture and its own description prose.
    /// </summary>
    SelectCastle,

    /// <summary>
    /// The screen's own 'Setup' row (SIEGESELECTIONSCREEN_SETUP), which the
    /// shipped screen puts behind the selected castle: the SIEGE SETUP screen
    /// (SIEGESETUPSCREENSTATICTOOLTIP_ATTACK 'Command the assault of the castle'
    /// / _DEFEND 'Take charge of the castle garrison'). That screen is a later
    /// item, so this click names what it would open rather than doing it.
    /// </summary>
    Setup,

    /// <summary>The way back to the war screen the screen was opened from.</summary>
    Back,
  }

  /// <summary>
  /// One of the siege selection screen's twenty-two castles - the screen's whole
  /// list - and the whole of what this port knows about it: the install's own
  /// name row and description prose, the install's OWN file key for that castle,
  /// the castle's painting and layout picture, and the historical castle map a
  /// siege at it would load.
  ///
  /// Where this comes from: the shipped translation table's own castle list,
  /// HISTORICCASTLE00..43 - an even-indexed NAME row and the odd-indexed
  /// DESCRIPTION prose beside it, twenty-two castles in the table's own order
  /// (Leeds, Harlech, Koblenz-Stolzenfels, Haut-Koenigsbourg, Edinburgh, Javier,
  /// Guadamur, Wartburg, Tower of London, Warwick, Criccieth, Windsor, Hastings,
  /// Heuneburg, Caerphilly, Marksburg, Meersburg, Fougères, Fenis, Bodiam,
  /// Pembroke, Castell y Bere). The table pins the list; what the table does NOT
  /// pin is the file each castle's art lives in, and the two spellings differ for
  /// four of the twenty-two:
  ///
  ///   Koblenz-Stolzenfels -> castle_stolzenfels_painting.jpg (the file keeps the
  ///                          castle's own name, not the town beside it)
  ///   Tower of London     -> castle_toweroflondon_painting.jpg (run together)
  ///   Fougères            -> castle_fougeres_painting.jpg (the file drops the accent)
  ///   Castell y Bere      -> castle_castellybere_painting.jpg (run together)
  ///
  /// Three of those four are a mechanical rule's own work (lowercase, drop the
  /// accents, drop everything that is not a letter or a digit) and ONE is not, so
  /// <see cref="FileKey"/> is CARRIED - measured against the install's own folder
  /// - rather than derived. The one the rule misses is Koblenz-Stolzenfels, whose
  /// key 'stolzenfels' has no 'koblenz' in it at all; the harness re-runs the rule
  /// and says so. The map's spelling is the install's own too, and it keeps what
  /// the file keeps: maps/Fougères.s2c has its accent, maps/Castell Y Bere.s2c
  /// spells the article with a capital the table's own name row does not
  /// ('Castell y Bere'), and maps/Koblenz - Stolzenfels.s2c keeps the spaces the
  /// .s2c file names carry round the hyphen.
  /// </summary>
  public readonly struct SiegeCastleRow
  {
    public SiegeCastleRow(int index, string nameId, string descriptionId, string name,
                          string description, string fileKey, string mapPath)
    {
      Index = index;
      NameId = nameId;
      DescriptionId = descriptionId;
      Name = name;
      Description = description;
      FileKey = fileKey;
      MapPath = mapPath;
    }

    /// <summary>Where this castle stands in the table's own list, 0-based (0..21).</summary>
    public readonly int Index;

    /// <summary>The castle's shipped name row (HISTORICCASTLE00, 02, ... 42 - the even ids).</summary>
    public readonly string NameId;

    /// <summary>The castle's shipped description row (HISTORICCASTLE01, 03, ... 43 - the odd ids).</summary>
    public readonly string DescriptionId;

    /// <summary>The castle's shipped English name, as the table spells it ('Castell y Bere').</summary>
    public readonly string Name;

    /// <summary>The castle's shipped English description - the prose the shipped screen shows for it.</summary>
    public readonly string Description;

    /// <summary>
    /// The install's own file key for this castle's art (castle_&lt;key&gt;_painting.jpg,
    /// layout_&lt;key&gt;.jpg) - the spelling the install's files use, which differs
    /// from the table's spelling for four of the twenty-two.
    /// </summary>
    public readonly string FileKey;

    /// <summary>
    /// The castle's own historical scenario map, install-relative, in the
    /// install's own spelling - what a click on 'Setup' at this castle would load
    /// (maps/Fougères.s2c, maps/Castell Y Bere.s2c, maps/Koblenz - Stolzenfels.s2c).
    /// "" for the four castles the install ships no historical map for (Leeds,
    /// Caerphilly, Meersburg, Bodiam): the shipped screen lists them all the same,
    /// and this port says the map is missing rather than inventing one.
    /// </summary>
    public readonly string MapPath;

    /// <summary>The castle's painting in the install (ui/siege_menu/castle_&lt;key&gt;_painting.jpg).</summary>
    public string PaintingPath => UIAssets.CastlePaintingTexture(FileKey);

    /// <summary>The castle's layout picture in the install (ui/siege_menu/layout_&lt;key&gt;.jpg).</summary>
    public string LayoutPath => UIAssets.CastleLayoutTexture(FileKey);

    /// <summary>True when the install ships a historical scenario map for this castle.</summary>
    public bool HasMap => MapPath.Length > 0;
  }

  /// <summary>
  /// One of the siege selection screen's own buttons - the screen's whole action
  /// surface. Twenty-two of them are the castle list's rows (the install's name
  /// rows: Leeds, Harlech, ...), one is the screen's own 'Setup' row
  /// (SIEGESELECTIONSCREEN_SETUP, with the table's tooltip for it beside it,
  /// SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP 'Continue to the Siege'), and one is
  /// the way back (SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT 'Previous Menu' - this
  /// screen ships its OWN exit row, unlike the trail, which wears the war
  /// screen's).
  ///
  /// A castle row's own click is the shipped tooltip's business: the table ships
  /// SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME 'Select this Siege' as the
  /// content a castle in the list shows, and the row's label is the castle's own
  /// name from the HISTORICCASTLE list. Both are the install's words; neither is
  /// invented here.
  /// </summary>
  public readonly struct SiegeScreenAction
  {
    public SiegeScreenAction(SiegeScreenActionKind kind, string id, string label, string tooltip,
                             string destination, GameState opens, string texture, string buttonName,
                             int castleIndex)
    {
      Kind = kind;
      Id = id;
      Label = label;
      Tooltip = tooltip;
      Destination = destination;
      Opens = opens;
      Texture = texture;
      ButtonName = buttonName;
      CastleIndex = castleIndex;
    }

    /// <summary>What the click does.</summary>
    public readonly SiegeScreenActionKind Kind;

    /// <summary>
    /// The shipped row this action's own click wears (i18n/en-us.xml): the
    /// castle rows all wear the one SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME
    /// row the table ships for selecting a siege, 'Setup' wears
    /// SIEGESELECTIONSCREEN_SETUP, and the way back wears
    /// SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT.
    /// </summary>
    public readonly string Id;

    /// <summary>The row's shipped English text - the castle's own name, 'Setup', or 'Previous Menu'.</summary>
    public readonly string Label;

    /// <summary>
    /// The shipped tooltip CONTENT the table ships beside the row ('Continue to
    /// the Siege' for Setup, 'Select this Siege' for a castle, "" for the way
    /// back, whose own row is its tooltip). This port keeps the content on the
    /// row, the play screen's own decision taken again here.
    /// </summary>
    public readonly string Tooltip;

    /// <summary>What a click reaches, or what it would reach, in words.</summary>
    public readonly string Destination;

    /// <summary>The screen this button opens in THIS port, or GameState.NONE.</summary>
    public readonly GameState Opens;

    /// <summary>
    /// The install's own picture for the button: button_play.bmp for the screen's
    /// one 'Setup' row and button_exit.tga for its way back. "" for the castle
    /// list's rows - the install ships no picture for a list row, whose picture
    /// is its own name in the panel the backdrop leaves for it.
    /// </summary>
    public readonly string Texture;

    /// <summary>The name the button carries in the scene ("CastleRow00Button", "SetupButton").</summary>
    public readonly string ButtonName;

    /// <summary>The castle this action selects (0..21), or -1 for the two fixed rows.</summary>
    public readonly int CastleIndex;

    /// <summary>
    /// True when the port really does what this row says: selecting a castle IS
    /// the port's own (it owns the selection, and the screen shows that castle's
    /// painting, layout and description), and the way back opens the war screen.
    /// The 'Setup' row is NOT built - the screen behind it is the siege setup, a
    /// later item - so its click names what it would open rather than doing it.
    /// </summary>
    public bool Built => Kind switch
    {
      SiegeScreenActionKind.SelectCastle => true,
      SiegeScreenActionKind.Back => Opens == GameState.WAR_SCREEN,
      _ => false,
    };

    /// <summary>
    /// The one wording of what a click reports, shared by the log line and the
    /// UAT: the destination when the port has built it (the castle rows and the
    /// way back), and otherwise the same fact said plainly, named. The castle
    /// rows say more than that - they name the castle the screen now shows - so
    /// their wording is composed with the screen's own selection by
    /// <see cref="SiegeScreenActions.OutcomeFor"/>, which is what the log line
    /// and the UAT read.
    /// </summary>
    public string Outcome => Built ? Destination : $"no destination yet: {Destination} is not built";
  }
}
