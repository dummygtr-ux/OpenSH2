using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// The siege selection screen's own actions, and its ONE entry point.
  ///
  /// Where they come from: the same shipped translation table as every other
  /// frontend surface here (i18n/en-us.xml). This screen's family in that table
  /// is two things at once - the screen's own rows, and the CASTLE LIST:
  ///
  ///   * the screen's rows: SIEGESELECTIONSCREEN_SETUP 'Setup' with its own
  ///     tooltip SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP 'Continue to the Siege',
  ///     the tooltip a castle in the list shows,
  ///     SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME 'Select this Siege', and the
  ///     screen's own way back SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT
  ///     'Previous Menu' (this screen ships its OWN exit row and its own exit
  ///     art - button_exit.tga - where the Conquest Trail had neither and wore
  ///     the war screen's);
  ///
  ///   * the castle list: HISTORICCASTLE00..43, an EVEN-indexed NAME row and the
  ///     odd-indexed DESCRIPTION prose beside it - twenty-two castles, in the
  ///     table's own order (Leeds, Harlech, Koblenz-Stolzenfels, ... Castell y
  ///     Bere). The table pins the names and the prose; the ART's file names are
  ///     the install folder's business and the two spellings differ for four of
  ///     the twenty-two (<see cref="SiegeCastleRow"/>), so each castle carries the
  ///     install's own file key and the install's own map spelling - both measured
  ///     against the install, not derived by a rule that happens to hold for the
  ///     other eighteen.
  ///
  /// The mechanical rule (lowercase the name, drop accents, drop everything that
  /// is not a letter or digit) builds castle_&lt;key&gt;_painting.jpg for
  /// TWENTY-ONE of the twenty-two; the one it misses is carried by hand and named
  /// here: Koblenz-Stolzenfels's key is 'stolzenfels' (the file is named for the
  /// castle, not the town the table's own name row prefixes), where the rule would
  /// say 'koblenzstolzenfels'. Fougères's key 'fougeres' IS the rule's own work
  /// once the accent is dropped, so the four name-vs-file differences are three
  /// rules and one carried key. The map's own spelling keeps what the install's
  /// files keep: maps/Fougères.s2c carries its accent, maps/Castell Y Bere.s2c
  /// capitalises the article the table's own name row spells 'Castell y Bere', and
  /// maps/Koblenz - Stolzenfels.s2c keeps the spaces round its hyphen.
  ///
  /// WHAT A CLICK WOULD LOAD is measured, not assumed: eighteen of the
  /// twenty-two castles have the install's own historical scenario
  /// (maps/&lt;castle&gt;.s2c, the 19 .s2c maps the install ships - Miskolc-Diosgyor
  /// among them has no row in the list and no painting), and FOUR do not (Leeds,
  /// Caerphilly, Meersburg, Bodiam). The shipped screen lists all twenty-two the
  /// same; this port says which of them the install ships no scenario for rather
  /// than inventing one. Starting a siege is a later item in any case - the
  /// port's flow loads the campaign's .s2m chapters, and a siege is a .s2c
  /// historical map whose per-map entity artifact the port has not dumped
  /// (the same gap the Conquest Trail's own start row names).
  /// </summary>
  public static class SiegeScreenActions
  {
    /// <summary>How many castles the screen's list carries (HISTORICCASTLE00..43 - 22 name/description pairs).</summary>
    public const int CastleCount = 22;

    /// <summary>
    /// The screen's castle list, in the table's own order, each with the
    /// install's own name row and description row, the install's own file key for
    /// its art, and the install's own historical map spelling ("" for the four
    /// castles the install ships no map for).
    /// </summary>
    public static readonly SiegeCastleRow[] Castles =
    {
      new(0, "HISTORICCASTLE00", "HISTORICCASTLE01",
          "Leeds",
          "Spread across three islands in the middle of an artificial lake Leeds castle was originally a simple manor house owned by the Saxon royal family.  Norman invaders built a strong stone fortress in 1119 to protect themselves from the hostile Saxons.  In 1321, the Royal Steward refused Queen Isabella entry to the castle.  King Edward II was furious after hearing this and successfully laid siege to the castle.  Medieval folk referred to Leeds castle as Lady's Castle after being the royal residence of six English queens.",
          "leeds",
          ""),
      new(1, "HISTORICCASTLE02", "HISTORICCASTLE03",
          "Harlech",
          "Perched on a rock overlooking the mountains of Snowdonia, Harlech is part of an iron ring of castles built by the English king, Edward I to subdue the Welsh.  Edward's masons began work on the castle in 1283, during his second campaign in North Wales.  The Welsh leader Owain Glyndwr captured the castle during his famous revolt of 1404 and held it successfully for over 4 years before it was retaken by the English.",
          "harlech",
          "maps/Harlech.s2c"),
      new(2, "HISTORICCASTLE04", "HISTORICCASTLE05",
          "Koblenz-Stolzenfels",
          "In the uplands, overlooking both the Rhine and the Moselle rivers sits the famous German castle of Koblenz-Stolzenfels.  The Archbishops of Trier built this typical 13th century castle as a customs fortress to impose illegal Rhine Tolls.  The French razed it to the ground during a siege in 1689 and King Frederick William IV of Prussia rebuilt the castle in the 19th century, using it as his summer residence from 1836 to 1842.",
          "stolzenfels",
          "maps/Koblenz - Stolzenfels.s2c"),
      new(3, "HISTORICCASTLE06", "HISTORICCASTLE07",
          "Haut-Koenigsbourg",
          "Built on a rocky outcrop in the middle of a forest, surrounded by mountains, Frederick II began building Haut-Koenigsbourg in 1147 to control the nearby roads.  The Swedish army besieged the castle in 1633 and despite it being in a rundown state, the garrison held out for fifty-two days.  When the Swedes finally took the castle, they found the garrison had disappeared.  Shortly after this, fire broke out totally destroying the castle.  The castle ruins were given to Kaiser Wilhelm II in 1899 and the architect Bodo Ebhardt finished the rebuilding of the castle in 1908.",
          "hautkoenigsbourg",
          "maps/Haut-Koenigsbourg.s2c"),
      new(4, "HISTORICCASTLE08", "HISTORICCASTLE09",
          "Edinburgh",
          "Sitting on top of an extinct volcano, a local Celtic tribe called the Votadini built a fort around 3000 years ago and named it Din Eidyn.  Come the medieval period Edinburgh Castle had grown from a fort into a stronghold, becoming the residence of Scotland's royalty.  In 1296, Edward I of England claimed the castle after three days of siege.  The Scots reclaimed it in 1314 by scaling the cliff face at night but by 1335, the English had it back in their possession.",
          "edinburgh",
          "maps/Edinburgh.s2c"),
      new(5, "HISTORICCASTLE10", "HISTORICCASTLE11",
          "Javier",
          "Starting life as a simple signal tower in the foothills of the Pyrenees, Arabian forces built Javier Castle into a fortress at the beginning of the 10th century.  Spanish military architects have long since replaced or adapted the original fortifications in their own style.  The patron saint of Navarre and the missionary church, St Francis Xavier was born here in 1506.  The King of Aragon, Fernando V conquered Navarra and occupied the castle at the beginning of the 16th century.",
          "javier",
          "maps/Javier.s2c"),
      new(6, "HISTORICCASTLE12", "HISTORICCASTLE13",
          "Guadamur",
          "On a hill overlooking the town, Don Pedro Lopez de Ayala built Guadamur Castle in the latter half of the 15th century.  The castle was often at the centre of Spanish Royal life.  Charles V came here to mourn the passing of his wife, Empress Isabella and after their wedding, Princess Joanna and Philip the Fair came here on honeymoon.  During the Napoleonic Wars, the French invaders burned the castle down, although the Count of Asalto rebuilt the castle from ruin in 1887, his design remaining true to the original 15th century castle.",
          "guadamur",
          "maps/Guadamur.s2c"),
      new(7, "HISTORICCASTLE14", "HISTORICCASTLE15",
          "Wartburg",
          "In the middle of a forest on top of a steep and sheer hill overlooking Eisenach, the people of Thuringia built Wartburg castle sometime after 1170.  The castle was a place of culture throughout the middle ages and became the venue for a Minstrel's Contest called the Sngerkrieg around 1207.  Between 1521 and 1522, Martin Luther famously translated the New Testament into German at the castle.  Under the name of Junker Jrg (the Knight George), he was hiding here to avoid persecution during the Reformation.",
          "wartburg",
          "maps/Wartburg.s2c"),
      new(8, "HISTORICCASTLE16", "HISTORICCASTLE17",
          "Tower of London",
          "The imposing White Tower built by Gundulf, the Bishop of Rochester, must have put fear into the hearts of the Anglo-Saxons who were still adjusting to the death of Harold II at the hands of their new Norman king,  William I, The Conqueror.  The castle developed over the years into an impenetrable royal fortress and became infamous as a place of imprisonment and execution, the most famous prisoners being Princess Elizabeth and her half sister Princess Mary.",
          "toweroflondon",
          "maps/Tower of London.s2c"),
      new(9, "HISTORICCASTLE18", "HISTORICCASTLE19",
          "Warwick",
          "Warwick castle is an excellent example of a well preserved English medieval castle.  Any attacker determined enough to get through the first set of gates would find themselves trapped in the infamous Killing Ground where boiling oil and deadly rocks would rain down on them.  Founded in 1086 by William the Conqueror on his move northwards from London, the wooden fort of Warwick controlled the roads and the river crossing  down to London.  In 1260 stone mining commenced and the castle we see today began to take shape.",
          "warwick",
          "maps/Warwick.s2c"),
      new(10, "HISTORICCASTLE20", "HISTORICCASTLE21",
          "Criccieth",
          "Perched on a rocky headland in Tremadog bay with distant views across to its near neighbour Harlech Castle, Criccieth castle began life at the start of the 13th century.  It  is unclear who first built this castle, but after King Edward I's second Welsh campaign, the English had control of it and made extensive improvements to it.  A focal point is the Large Engine Tower, which was exceptionally strong and sturdy enough to support a siege engine like a ballista or mangonel.  During the last Welsh rebellion, Owain Glyndwr's men ripped down the walls and set the castle ablaze.",
          "criccieth",
          "maps/Criccieth.s2c"),
      new(11, "HISTORICCASTLE22", "HISTORICCASTLE23",
          "Windsor",
          "Built near a Saxon hunting ground on a steep hill overlooking the River Thames the central Round Tower of Windsor Castle can be seen from almost anywhere in Windsor.  William the conqueror chose the site to guard the western approach to the capital.  It is roughly a days march from London.  According to legend, King Arthur visited Windsor's Grand Hall with his Knights of the Round Table.  Windsor is the world's largest castle and is still used as the official residence of the Queen of England.",
          "windsor",
          "maps/Windsor.s2c"),
      new(12, "HISTORICCASTLE24", "HISTORICCASTLE25",
          "Hastings",
          "On a cliff edge overlooking the sea, Hastings was a brilliant example of a motte and bailey fort.  Upon landing in 1066, William the Conqueror erected two prefabricated motte and bailey castles he had brought with him in his ships.  This artists impression shows how it may have looked before William upgraded the castle to stone in 1070.  Violent storms ravaged the south coast in 1287 eroding the soft sandstone cliffs beneath the castle and parts of it were lost to the sea.  Hastings was never attacked by military force but over the years has seen its fair share of peasant uprisings.",
          "hastings",
          "maps/Hastings.s2c"),
      new(13, "HISTORICCASTLE26", "HISTORICCASTLE27",
          "Heuneburg",
          "Located on top of a hill in the plains of central Europe, this wooden hill fort dates back to 600BC when its natural defensive position made it attractive to Iron age tribes wishing to control the surrounding area.  Later its proximity to the Danube River and location on some of Europes busiest trade routes made sure it was often a target for rulers wishing to control and tax commerce.  It is known to have been completely demolished and rebuilt at least 3 times.",
          "heuneburg",
          "maps/Heuneburg.s2c"),
      new(14, "HISTORICCASTLE28", "HISTORICCASTLE29",
          "Caerphilly",
          "Built in the middle of a flooded valley on a stretch of man made islands, Caerphilly was the first concentric castle ever built in Britain.  Its revolutionary concept of walls within walls made it a masterpiece of late 13th century military engineering and its system of artificial lakes and moats made it practically impregnable.  Several minor Welsh sieges were raised against Caerphilly's walls but none made any impact.  By 1283 however, Edward I had all but crushed the Welsh resistance, effectively making Caerphilly redundant.",
          "caerphilly",
          ""),
      new(15, "HISTORICCASTLE30", "HISTORICCASTLE31",
          "Marksburg",
          "Situated high above the town of Braubach on the east bank of the Rhine, Marksburg castle was built by the Freemen of the town in the 12th century to enforce tolls on the river Rhine.  It never faced a serious attack and is the only hilltop castle on the Rhine never to be destroyed and has been continuously inhabited for more than seven hundred years.",
          "marksburg",
          "maps/Marksburg.s2c"),
      new(16, "HISTORICCASTLE32", "HISTORICCASTLE33",
          "Meersburg",
          "Strategically positioned on the north shore of Lake Constance in the province of Bodensee, Meersburg castle is one of Germany's oldest castles.  It was built in the 7th century and  its 28 rooms contained one of the most extensive collections of medieval literature ever known.  The castle was rebuilt in 1508 to give it its present appearance.",
          "meersburg",
          ""),
      new(17, "HISTORICCASTLE34", "HISTORICCASTLE35",
          "Fougères",
          "Now sitting on a hill above the River Nancon dominating the skyline with its huge round towers, Fougères Castle started life with a wooden keep on a small island at the bottom of the valley between the marshes.  As well as arrow slits the castle walls are lined with musket loops and there is even a gun platform for a cannon.  Henry II of England destroyed the wooden castle in 1166 but it was rebuilt in stone shortly afterwards by Raoul II.",
          "fougeres",
          "maps/Fougères.s2c"),
      new(18, "HISTORICCASTLE36", "HISTORICCASTLE37",
          "Fenis",
          "Located in the picturesque region of Val d'Aosta, this compact castle was built by Prince Challant in 1242 to control the valley.  It consists of a square donjon, two outer walls and a central tower with an armoury.  The castle was converted into stables after 1716 and reduced to nothing more than a small farm.  Alfred d'Andrade bought the castle in 1895 and restored it to its former glory.  No one ever attacked Fenis castle and it has survived to this day to become a museum.",
          "fenis",
          "maps/Fenis.s2c"),
      new(19, "HISTORICCASTLE38", "HISTORICCASTLE39",
          "Bodiam",
          "At the centre of an artificial lake, Bodiam castle has a round tower on each corner and a square tower in the middle of each wall giving it a symmetrical appearance.  A Norman family called the Bodehams took control of the area and built a motte and bailey castle.  To protect the Rother valley, Edward Dalyngrigge gained a royal license to strengthen and crenellate.  He used this to build a totally different castle on a brand new site.  Sir William Waller gutted the interior of the castle during the English Civil War but left the outside virtually intact.",
          "bodiam",
          ""),
      new(20, "HISTORICCASTLE40", "HISTORICCASTLE41",
          "Pembroke",
          "Located high on a rocky peninsula and surrounded by the tidal waters of the River Cleddau, Pembroke Castle started life as a wooden fortress and was the first Norman settlement in South West Wales.  The Earl of Pembroke, William Marshall, oversaw the construction of the stone castle and held it until his death in 1219.  The castle was regularly attacked by the Welsh but thanks to the constant improvements made to the fortress they failed to make an impact.",
          "pembroke",
          "maps/Pembroke.s2c"),
      new(21, "HISTORICCASTLE42", "HISTORICCASTLE43",
          "Castell y Bere",
          "Built on a steep rocky spur high above the Dysynni valley, Castell y Bere is a good example of a castle made by the Welsh Princes.  It features low curtain walls and two elongated D shaped towers both typical of the Welsh style.  The steep rocky inclines add to the castles defensive properties.  The castle eventually fell to the English during King Edward's conquest of Wales in 1283.",
          "castellybere",
          "maps/Castell Y Bere.s2c"),
    };

    /// <summary>The row for one castle of the list (0-based; 0..21).</summary>
    public static SiegeCastleRow Castle(int index) => Castles[index];

    /// <summary>The castle the screen is showing (the port's selection, see <see cref="SelectedIndex"/>).</summary>
    public static SiegeCastleRow Selected => Castles[SelectedIndex];

    /// <summary>The name the button of one castle's list row carries in the scene ("CastleRow00Button").</summary>
    public static string CastleRowButtonName(int index) => $"CastleRow{index:D2}Button";

    /// <summary>The shipped row a castle row's own click wears: the table's one 'Select this Siege' tooltip row.</summary>
    public const string SiegeNameId = "SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME";

    /// <summary>The tooltip content that row ships: 'Select this Siege'.</summary>
    public const string SiegeNameTooltip = "Select this Siege";

    /// <summary>
    /// One castle of the list, as the screen's own action: its label is the
    /// castle's own shipped name row (what the button shows), its click wears the
    /// table's one SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME row and its
    /// content, and the port really does select that castle - the screen shows
    /// the castle's painting, its layout picture and its own description.
    /// </summary>
    public static SiegeScreenAction SelectRow(int index)
    {
      SiegeCastleRow castle = Castles[index];
      string destination = castle.HasMap
        ? $"the siege of {castle.Name} (its own historical scenario, {castle.MapPath})"
        : $"the siege of {castle.Name} (the install ships no historical scenario for this castle)";
      return new SiegeScreenAction(SiegeScreenActionKind.SelectCastle, SiegeNameId,
                                   castle.Name, SiegeNameTooltip, destination, GameState.NONE,
                                   "", CastleRowButtonName(index), index);
    }

    /// <summary>
    /// The screen's own 'Setup' row (SIEGESELECTIONSCREEN_SETUP 'Setup', whose
    /// tooltip the table ships as SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP
    /// 'Continue to the Siege'): the way on from the selected castle, into the
    /// SIEGE SETUP screen the table names beside it
    /// (SIEGESETUPSCREENSTATICTOOLTIP_ATTACK / _DEFEND). That screen is a later
    /// item, so this click names what it would open rather than doing it. Its
    /// picture is the install's own button_play.bmp - the one button picture the
    /// screen's folder ships.
    /// </summary>
    public static readonly SiegeScreenAction Setup = new(
      SiegeScreenActionKind.Setup, "SIEGESELECTIONSCREEN_SETUP", "Setup", "Continue to the Siege",
      "the siege setup screen (SIEGESETUPSCREENSTATICTOOLTIP_ATTACK 'Command the assault of the "
      + "castle' / _DEFEND 'Take charge of the castle garrison', ui/siege_menu/choice_attack_*.jpg, "
      + "choice_defend_*.jpg)",
      GameState.NONE, UIAssets.SiegeSetupButtonTexture, "SetupButton", -1);

    /// <summary>
    /// The way back ('Previous Menu') - the only row on this screen with a screen
    /// behind it, and it is the WAR SCREEN the screen was opened from: the
    /// frontend is a tree, and the siege selection is a branch of the war screen
    /// exactly as the campaign screen and the Conquest Trail are. This screen
    /// ships its OWN exit row and its own exit art (button_exit.tga, the install's
    /// compass-rose medallion) - unlike the trail, which wore the war screen's row
    /// and the frontend's own fancy button.
    /// </summary>
    public static readonly SiegeScreenAction Back = new(
      SiegeScreenActionKind.Back, "SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT", "Previous Menu", "",
      "the war screen (the screen the siege selection was opened from, which the port has)",
      GameState.WAR_SCREEN, UIAssets.SiegeExitButtonTexture, "BackButton", -1);

    /// <summary>The two rows every showing of the screen carries, in the screen's own order.</summary>
    public static readonly SiegeScreenAction[] FixedRows = { Setup, Back };

    /// <summary>
    /// The screen's whole action surface at one selection, in the screen's own
    /// order: the twenty-two castle rows (the table's own list), then 'Setup' and
    /// the way back.
    /// </summary>
    public static SiegeScreenAction[] RowsFor(int selectedIndex)
    {
      var rows = new SiegeScreenAction[CastleCount + FixedRows.Length];
      for (int i = 0; i < CastleCount; i++) rows[i] = SelectRow(i);
      FixedRows.CopyTo(rows, CastleCount);
      return rows;
    }

    /// <summary>
    /// Where the screen stands: the castle it is showing, 0-based (0..21). The
    /// screen opens at the table's FIRST castle (the port has no progress and no
    /// save data, so it opens where the shipped list opens) and a click on a list
    /// row moves it. Both the UI and the UAT read it; only <see cref="Dispatch"/>
    /// writes it.
    /// </summary>
    public static int SelectedIndex { get; private set; }

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static SiegeScreenAction LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over, and the selection with it: SiegeScreenUI builds its
    /// buttons once per showing, so the record always describes the screen that is
    /// on it - and the screen opens at the table's first castle every time.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
      SelectedIndex = 0;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a siege-screen action. Names the row, records it,
    /// reports what it does - and then does it: a castle row's click selects that
    /// castle (the screen repaints its painting, its layout and its description),
    /// the way back asks the flow for the war screen (Game.RequestFrontendScreen,
    /// the move every frontend click makes), and the 'Setup' row does not open
    /// anything yet - its click has said so by name.
    /// </summary>
    public static void Dispatch(SiegeScreenAction row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;

      string outcome = OutcomeFor(row, SelectedIndex);
      Debug.Log($"[SiegeScreenActions] click: {row.Id} '{row.Label}' -> {outcome}");

      switch (row.Kind)
      {
        case SiegeScreenActionKind.SelectCastle:
          if (row.CastleIndex != SelectedIndex)
          {
            SelectedIndex = row.CastleIndex;
            RefreshScreen();
          }
          return;

        case SiegeScreenActionKind.Back:
          Game.GetInstance()?.RequestFrontendScreen(row.Opens);
          return;

        default:
          // Setup: the screen behind it is the siege setup - a later item. The
          // click has named what it would open.
          return;
      }
    }

    /// <summary>
    /// What one click reports from one selection: a castle row names the castle
    /// the screen now shows (and what a siege there would load, measured), the
    /// way back names its screen, and the 'Setup' row names the setup screen the
    /// port has not built.
    /// </summary>
    public static string OutcomeFor(SiegeScreenAction row, int selectedIndex) => row.Kind switch
    {
      SiegeScreenActionKind.SelectCastle =>
        $"the siege of {Castles[row.CastleIndex].Name} is the screen's selection "
        + (Castles[row.CastleIndex].HasMap
           ? $"(its own historical scenario {Castles[row.CastleIndex].MapPath} is what a siege there "
             + "would load; starting one is a later item)"
           : "(the install ships no historical scenario for this castle)"),
      _ => row.Outcome,
    };

    /// <summary>The screen's own refresh, where one is up (a castle row's next step).</summary>
    private static void RefreshScreen() =>
      Game.GetInstance()?.siegeScreenUI?.Refresh();
  }
}
