using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code.UI
{
  class UIAssets
  {
    public static string StoneSidebarTexture = Constants.GamePath + "/ui/frontend/stoneSidebar.png";
    public static string StoneSidebar2KTexture = Constants.GamePath + "/ui/frontend/stoneSidebar2k.png";
    public static string StoneSidebar4KTexture = Constants.GamePath + "/ui/frontend/stoneSidebar4k.png";
    public static string LogoTexture = Constants.GamePath + "/ui/frontend/frontend_logo.tga";
    public static string FancyButtonTexture = Constants.GamePath + "/ui/frontend/frontend_button_fancy.tga";
    public static string FancyButtonHighlightTexture = Constants.GamePath + "/ui/frontend/frontend_button_fancy_highlight.tga";
    public static string FancyButtonHighlightTexture2 = Constants.GamePath + "/ui/frontend/frontend_button_fancy_highlight_green.tga";

    // The play screen (PLAYSCREEN 'Choose Your Path'), which MAINMENU_PLAY
    // opens: the install ships the screen's own art in two folders - the
    // background in ui/choose_path/ and the three buttons' pictures (each with
    // its hover state) in ui/war_peace_menu/, next to the war/peace screens'
    // own button pictures that those screens will use.
    public static string ChoosePathBackgroundTexture = Constants.GamePath + "/ui/choose_path/choose_path_background.jpg";
    public static string WarButtonTexture = Constants.GamePath + "/ui/war_peace_menu/war_off.jpg";
    public static string WarButtonHighlightTexture = Constants.GamePath + "/ui/war_peace_menu/war_on.jpg";
    public static string PeaceButtonTexture = Constants.GamePath + "/ui/war_peace_menu/peace_off.jpg";
    public static string PeaceButtonHighlightTexture = Constants.GamePath + "/ui/war_peace_menu/peace_on.jpg";
    public static string TutorialButtonTexture = Constants.GamePath + "/ui/war_peace_menu/tutorial_button_off.tga";
    public static string TutorialButtonHighlightTexture = Constants.GamePath + "/ui/war_peace_menu/tutorial_button_on.tga";

    // The two path screens behind the play screen (the war screen and the peace
    // screen), which the play screen's two path buttons open. They share the
    // install's own backdrop - ui/war_peace_menu/war_peace_menu_back.jpg, the
    // stone wall with a picture frame either side and the empty description bar
    // along the bottom - and each row's picture is the install's own 366x367 jpg
    // from the same folder, except the Conquest Trail's, which the Gold Edition
    // shipped in its own folder with the trail's map. The install ships no hover
    // state beside these pictures (only the play screen's two path buttons have
    // war_on/peace_on twins), so these buttons do not swap their picture.
    public static string WarPeaceMenuBackgroundTexture = Constants.GamePath + "/ui/war_peace_menu/war_peace_menu_back.jpg";
    public static string WarCampaignTexture = Constants.GamePath + "/ui/war_peace_menu/war_campaign.jpg";
    public static string WarConquestTexture = Constants.GamePath + "/ui/gold_edition/war_conquest.jpg";
    public static string WarKingmakerTexture = Constants.GamePath + "/ui/war_peace_menu/war_kingmaker.jpg";
    public static string WarSiegeTexture = Constants.GamePath + "/ui/war_peace_menu/war_siege.jpg";
    public static string WarCustomTexture = Constants.GamePath + "/ui/war_peace_menu/warpeace_custom.jpg";
    public static string WarLoadTexture = Constants.GamePath + "/ui/war_peace_menu/warpeace_load.jpg";
    public static string PeaceSimCampaignTexture = Constants.GamePath + "/ui/war_peace_menu/peace_simcampaign.jpg";
    public static string PeaceFreeBuildTexture = Constants.GamePath + "/ui/war_peace_menu/peace_freebuild.jpg";
    public static string PeaceCustomMapsTexture = Constants.GamePath + "/ui/war_peace_menu/warpeace_custom.jpg";

    // The campaign screen (GameState.CAMPAIGN_SCREEN), which the war screen's
    // Campaign row opens: the install ships the screen's own folder,
    // ui/campaign_screen/. Twelve full-screen 1024x768 pictures, one per
    // chapter - the same desk, map of Britain and parchment scroll column, with
    // the campaign's own route drawn a little further along in each (chapter 1
    // is a single red marker in Wales, chapter 12 the whole route) - and the one
    // button the screen reuses (campaign_button.bmp, 251x44), with
    // campaign_button_highlight.tga (a soft glow, alpha ~91 of 255 at its
    // centre, fading to ~10 at its edges) as its hover overlay. The folder also
    // holds the six progressive-state markers
    // symbol_{good,bad,red}_{complete,current}.tga (32x32), which mark where a
    // completed or current chapter stands - art this port has no save/progress
    // data for and does not draw.
    //
    // The screen's own backdrop is chapter 1's picture: the port has no
    // progress, so it opens where the shipped campaign starts. The other eleven
    // are the install's own art for the chapters the port cannot reach yet;
    // which picture the shipped screen shows for which chapter is its progress
    // system's business, not this port's invention.
    public static readonly string[] CampaignChapterTextures =
    {
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter1.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter2.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter3.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter4.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter5.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter6.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter7.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter8.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter9.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter10.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter11.jpg",
      Constants.GamePath + "/ui/campaign_screen/campaign_chapter12.jpg",
    };
    public static string CampaignBackgroundTexture => CampaignChapterTextures[0];
    public static string CampaignButtonTexture = Constants.GamePath + "/ui/campaign_screen/campaign_button.bmp";
    public static string CampaignButtonHighlightTexture = Constants.GamePath + "/ui/campaign_screen/campaign_button_highlight.tga";

    // The briefing screen (GameState.BRIEFING) - the mission-start screen a
    // clicked chapter's map puts up - and the per-chapter introductions it is
    // made of. The install ships the screen's own folder, ui/briefing_screen/:
    // its backdrop (briefing_background.jpg, 1024x768 - a big empty framed panel
    // over a gold plaque), the three tab buttons with their off/on states
    // (button_objectives/hints/tutorial_{off,on}.tga - a scroll, a scroll with
    // an arrow, and an open book), and the hint line's art (hint_bar.tga,
    // 855x43, with the medallions hint_open/hint_closed/hint_highlight.tga,
    // 33x33). There is no play-button art in the folder: the shipped screen's
    // 'To the Game' row has a table row and no picture, so it wears the
    // frontend's own fancy button like every other table-named row here.
    public static string BriefingBackgroundTexture = Constants.GamePath + "/ui/briefing_screen/briefing_background.jpg";
    public static string BriefingObjectivesTexture = Constants.GamePath + "/ui/briefing_screen/button_objectives_off.tga";
    public static string BriefingObjectivesOnTexture = Constants.GamePath + "/ui/briefing_screen/button_objectives_on.tga";
    public static string BriefingHintsTexture = Constants.GamePath + "/ui/briefing_screen/button_hints_off.tga";
    public static string BriefingHintsOnTexture = Constants.GamePath + "/ui/briefing_screen/button_hints_on.tga";
    public static string BriefingTutorialTexture = Constants.GamePath + "/ui/briefing_screen/button_tutorial_off.tga";
    public static string BriefingTutorialOnTexture = Constants.GamePath + "/ui/briefing_screen/button_tutorial_on.tga";
    public static string BriefingHintBarTexture = Constants.GamePath + "/ui/briefing_screen/hint_bar.tga";
    public static string BriefingPageMedallionTexture = Constants.GamePath + "/ui/briefing_screen/hint_closed.tga";
    public static string BriefingPageMedallionOnTexture = Constants.GamePath + "/ui/briefing_screen/hint_open.tga";
    public static string BriefingPageMedallionHighlightTexture = Constants.GamePath + "/ui/briefing_screen/hint_highlight.tga";

    // The in-game MAIN SCREEN (GameState.GAMEPLAY) - the game's own surface the
    // mission's briefing hands the map to. Its status bar is the install's own
    // composed strip: ui/ingameui.tga (1024x1024, 32bpp) paints the whole bar
    // across its top rows (its paint ends ~row 28 per column; the strip the port
    // cuts is the top 45) - the left end cap, the Date and Peasants /
    // Maximum Peasants insets (127 and 262 px), the stone Current Rank plaque
    // (206 px, its banner dipping to ~row 36), then the four right-hand insets
    // with their own medallions - a gold coin (Gold), a crown (Honour), a face
    // (Popularity) and one the install's rollover table does not name (61 px).
    // Measured 2026-10-13; the strip is the one fully composed in-game surface
    // piece the install ships.
    // The button the main screen's Objectives row wears is the in-game menus'
    // own small fancy button (ui/ingame_menus/button_fancy_small.tga, 192x46);
    // the install pins no art for the other six controls' rows, so the port
    // draws none of them (see MainScreenActions).
    public static string IngameUiTexture = Constants.GamePath + "/ui/ingameui.tga";
    public static string IngameSmallButtonTexture = Constants.GamePath + "/ui/ingame_menus/button_fancy_small.tga";

    // The Conquest Trail screen (GameState.CONQUEST_TRAIL_SCREEN), which the war
    // screen's WARSCREEN_CONQUEST row opens. The Gold Edition shipped the whole
    // screen in its own folder, ui/gold_edition/, and its backdrop is the one
    // fully composed picture in it: conquest_trail.png (1024x768) - the framed
    // map of Europe with the trail's route and its ten castles painted on it,
    // the two paging-arrow tiles at the map's bottom right (measured: their
    // 52x52 art stands at x 732 and x 782, y 507, so the port draws the install's
    // own arrow buttons on them) and the empty gold-framed panel along the
    // bottom where the screen's text goes (measured: its inner rect is
    // x 174..841, y 588..709 of the 1024x768 picture).
    //
    // The trail's own progress art is the eleven war_conquest_arrowsNN.jpg
    // pictures (558x357): each is the map's central rect (measured by subimage
    // match: drawn at x 209, y 114 - the same rect in the backdrop) with the
    // trail drawn one castle further along, arrows01 carrying only Guadamur lit
    // and arrows11 the whole route. The screen shows the position's own picture
    // over that rect; the trail's twelve rows are one step past the eleven
    // pictures only in that the last row (CONQUESTTRAIL_12, the congratulation)
    // stands on the completed trail, which is arrows11's picture.
    //
    // conquest_trail_winscreen.png (660x533 - a crown on a purple field) is the
    // trail's completion art. This port draws none of it: it is progressive-state
    // art like the campaign screen's six markers, and the port has no progress
    // data (named on the report, not invented).
    public static string ConquestTrailBackgroundTexture =
      Constants.GamePath + "/ui/gold_edition/conquest_trail.png";
    public static string ConquestTrailArrowBackTexture =
      Constants.GamePath + "/ui/gold_edition/conquest_trail_arrowback.tga";
    public static string ConquestTrailArrowBackOnTexture =
      Constants.GamePath + "/ui/gold_edition/conquest_trail_arrowback_on.tga";
    public static string ConquestTrailArrowForwardTexture =
      Constants.GamePath + "/ui/gold_edition/conquest_trail_arrowforward.tga";
    public static string ConquestTrailArrowForwardOnTexture =
      Constants.GamePath + "/ui/gold_edition/conquest_trail_arrowforward_on.tga";
    public static string ConquestTrailWinScreenTexture =
      Constants.GamePath + "/ui/gold_edition/conquest_trail_winscreen.png";

    /// <summary>
    /// The trail's own progress picture for one position: war_conquest_arrowsNN
    /// (NN = the position, 01..11). The trail's last row (12) stands on the
    /// completed trail, which is arrows11's picture - the install ships eleven
    /// pictures for the twelve rows, so the last row wears the eleventh.
    /// </summary>
    public static string ConquestTrailOverlayTexture(int position) =>
      Constants.GamePath + $"/ui/gold_edition/war_conquest_arrows{Math.Min(Math.Max(position, 1), 11):D2}.jpg";

    /// <summary>
    /// The 'Start Mission' (CONQUEST START MISSION) button's art: the install's
    /// own castle-scenario play button (ui/siege_menu/button_play.bmp, 266x57, a
    /// blank gold-framed bar) - the button the castle art the trail's castles
    /// come from ships beside them. The trail's own folder ships no button art
    /// (only the map, its arrows and the two paging buttons), so which picture
    /// the shipped screen dresses this row in is NOT in the shipped data; this is
    /// the port's reading, named on the report.
    /// </summary>
    public static string ConquestStartMissionTexture =
      Constants.GamePath + "/ui/siege_menu/button_play.bmp";

  /// <summary>
  /// The install's own painting for one castle (ui/siege_menu/castle_&lt;key&gt;_painting.jpg,
  /// 425x313, each with the castle's own location inset baked into its
  /// bottom-left corner). The siege selection screen, whose folder this is,
  /// draws the selected castle's painting in the slot its own backdrop leaves;
  /// the Conquest Trail carries the same pairing as row data without drawing it
  /// (the trail's own art draws its castles as labels on the map).
  /// </summary>
  public static string CastlePaintingTexture(string castleKey) =>
    Constants.GamePath + $"/ui/siege_menu/castle_{castleKey}_painting.jpg";

  // The SIEGE SELECTION screen (GameState.SIEGE_SCREEN), which the war screen's
  // WARSCREEN_SIEGE row opens. The install ships the screen's whole folder,
  // ui/siege_menu/, and its backdrop is the composed picture of the screen
  // itself - siege_menu_background.png (1024x768): the empty gold frame at the
  // top left where the selected castle's painting goes, the smaller frame beside
  // it for the castle's layout picture, the empty leather panel along the bottom
  // left (the description's place), the tall empty leather panel down the right
  // (the castle list's place), and the stripped brown border. The install ships
  // the same picture three times - the .png, its .jpg twin (mean channel
  // difference 1.6, JPEG noise), and a 939x657 .tga whose interior is
  // semi-transparent (alpha ~128-159 over 92 % of it, with a bright frame): this
  // port draws the lossless .png and names the other two rather than guessing
  // what the .tga's transparency is for.
  //
  // Measured from the .png (2026-10-13), the backdrop's own frames:
  // the painting's frame is drawn at rows 58 and 373 (x 63..490) and columns 63
  // and 490 (y 58..373), so its opening is x 64..489, y 59..372 = 426x314 - one
  // pixel more than the 425x313 paintings in each direction; the layout's frame
  // is at columns 498/643 (y 58..221), so its opening is x 499..642, y 59..220 =
  // 144x162 - and the layout_*.jpg pictures are 146x163 (one pixel more each
  // way), while the empty plate the folder ships beside them (siege_layout.bmp)
  // is 144x161. The two leather panels' openings are measured off the same gold
  // lines: the description's is x 70..609, y 385..639 (its frame rows 382..384 /
  // 640..642, its inner right gold line at column 610) and the castle list's is
  // x 708..947, y 62..639 (frame rows 59..61 / 640..642, inner gold lines at
  // columns 707 and 947).
  //
  // The folder's other pieces are CARRIED, not drawn, and named on the report:
  // siege_painting.bmp (425x313) and siege_layout.bmp (144x161) are the blank
  // plates at the two slots' own sizes, siege_map.bmp (144x144, a compass rose)
  // and siege_location.bmp (82x90, a map inset) are the same kind of plate for
  // the slots this screen's backdrop does NOT paint, siege_textbox.tga (363x167)
  // a framed box, ivy.tga (248x238) foliage, and the last four choices
  // (siege_menu_choices_preview_build.jpg 1024x768, choice_attack_{off,on}.jpg and
  // choice_defend_{off,on}.jpg, 363x407) belong to the SIEGE SETUP screen behind
  // this one - whose own rows the table names
  // (SIEGESETUPSCREENSTATICTOOLTIP_ATTACK/DEFEND/EXIT).
  public static string SiegeMenuBackgroundTexture =
    Constants.GamePath + "/ui/siege_menu/siege_menu_background.png";
  public static string SiegeMenuBackgroundJpgTexture =
    Constants.GamePath + "/ui/siege_menu/siege_menu_background.jpg";
  public static string SiegeMenuBackgroundTgaTexture =
    Constants.GamePath + "/ui/siege_menu/siege_menu_background.tga";

  /// <summary>
  /// The install's own layout picture for one castle
  /// (ui/siege_menu/layout_&lt;key&gt;.jpg, 146x163): the castle's plan drawing,
  /// drawn in the backdrop's smaller frame - the slot the empty
  /// siege_layout.bmp (144x161) is the same picture's blank plate for.
  /// </summary>
  public static string CastleLayoutTexture(string castleKey) =>
    Constants.GamePath + $"/ui/siege_menu/layout_{castleKey}.jpg";

  /// <summary>
  /// The siege selection screen's own 'Setup' button art: button_play.bmp
  /// (266x57, an empty gold-framed gold bar, with button_play_highlight.bmp
  /// beside it) - the one button picture the folder ships for this screen.
  /// </summary>
  public static string SiegeSetupButtonTexture =
    Constants.GamePath + "/ui/siege_menu/button_play.bmp";

  /// <summary>
  /// The screen's own way-back art: button_exit.tga (58x58 - the install's own
  /// round compass-rose medallion, with button_exit_highlight.tga beside it).
  /// It is the picture the folder ships for the screen's own exit row,
  /// SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT 'Previous Menu'.
  /// </summary>
  public static string SiegeExitButtonTexture =
    Constants.GamePath + "/ui/siege_menu/button_exit.tga";

  /// <summary>The blank plates and decorative pieces this screen carries but does not draw.</summary>
  public static string SiegePaintingPlateTexture =
    Constants.GamePath + "/ui/siege_menu/siege_painting.bmp";
  public static string SiegeLayoutPlateTexture =
    Constants.GamePath + "/ui/siege_menu/siege_layout.bmp";
  public static string SiegeMapPlateTexture =
    Constants.GamePath + "/ui/siege_menu/siege_map.bmp";
  public static string SiegeLocationPlateTexture =
    Constants.GamePath + "/ui/siege_menu/siege_location.bmp";
  public static string SiegeTextboxTexture =
    Constants.GamePath + "/ui/siege_menu/siege_textbox.tga";
  public static string SiegeIvyTexture =
    Constants.GamePath + "/ui/siege_menu/ivy.tga";

  /// <summary>The setup screen's own art, which the folder carries beside this screen's.</summary>
  public static string SiegeSetupPreviewTexture =
    Constants.GamePath + "/ui/siege_menu/siege_menu_choices_preview_build.jpg";
  public static string SiegeAttackChoiceTexture =
    Constants.GamePath + "/ui/siege_menu/choice_attack_off.jpg";
  public static string SiegeAttackChoiceOnTexture =
    Constants.GamePath + "/ui/siege_menu/choice_attack_on.jpg";
  public static string SiegeDefendChoiceTexture =
    Constants.GamePath + "/ui/siege_menu/choice_defend_off.jpg";
  public static string SiegeDefendChoiceOnTexture =
    Constants.GamePath + "/ui/siege_menu/choice_defend_on.jpg";

    /// <summary>
    /// The MAP REQUEST screen (GameState.MAP_REQUEST_SCREEN), which the war
    /// screen's WARSCREEN_CUSTOM row opens - the engine's `MapRequestScreen`,
    /// screen name 'map request screen'. The install ships the screen's whole
    /// folder, ui/map_select/, and its code loads both backgrounds under the
    /// ids the folder's names say: 'ui/map request bg' -> mapselect_background.jpg
    /// (the monk and the woman - the PEACE path's characters) and 'ui/map
    /// request war bg' -> mapselect_background_war.jpg (the barbarian and the
    /// knight - the WAR path's, the one this row wears). Both 1024x768, and both
    /// paint the same structure, measured 2026-10-13:
    /// the big empty frame at the top left (opening x 244..492, y 42..308), the
    /// frame beside it (x 505..790, y 33..317) that the folder's own _big/_small
    /// variants mark with their transparent windows - 256x256 at x 525..780,
    /// y 47..302 (background_big) or 126x125 at x 526..651, y 176..300
    /// (background_small, and the war jpg paints that window black) - the panel
    /// along the bottom (outer frame x 237..790, y 320..727) with its brown
    /// title strip (y 329..355), its list area and its scrollbar groove, and the
    /// two green arrow medallions the art itself paints (measured: the left at
    /// x 53..99, y 672..719, the right at x 925..971, y 672..719).
    ///
    /// The three 256x256 pictures beside the backgrounds are the screen's own
    /// MODE pictures - mapselect_custom.bmp (a castle at night),
    /// mapselect_freebuild.bmp (a jouster) and mapselect_load.bmp (a peasant
    /// carrying stones) - and the exe registers all three as texture ids
    /// ('texture/ui/custom', '/freebuild', '/load', measured 2026-10-13), so
    /// the screen has three modes and this row is the Custom one. The war jpg
    /// and the _big variant agree on where a 256x256 picture stands - the
    /// _big variant's window is exactly 256x256 - so the mode picture goes
    /// there; the port draws it and names the reading (the shipped engine would
    /// draw the selected map's own view in it).
    ///
    /// mapselect_small_map_mask.png (257x257, its lower-left 130x129 transparent)
    /// is the mask the shipped engine clips a small map view with; this port
    /// carries it, not drawing it (named on the report, like the siege's
    /// plates).
    /// </summary>
    public static string MapSelectBackgroundWarTexture =
      Constants.GamePath + "/ui/map_select/mapselect_background_war.jpg";
    public static string MapSelectBackgroundTexture =
      Constants.GamePath + "/ui/map_select/mapselect_background.jpg";
    public static string MapSelectCustomModeTexture =
      Constants.GamePath + "/ui/map_select/mapselect_custom.bmp";
    public static string MapSelectFreeBuildModeTexture =
      Constants.GamePath + "/ui/map_select/mapselect_freebuild.bmp";
    public static string MapSelectLoadModeTexture =
      Constants.GamePath + "/ui/map_select/mapselect_load.bmp";
    public static string MapSelectSmallMapMaskTexture =
      Constants.GamePath + "/ui/map_select/mapselect_small_map_mask.png";

    /// <summary>
    /// The map request screen's 'Play' row (MAPREQUESTSCREEN_PLAY) wears the
    /// install's own castle-scenario play button - ui/siege_menu/button_play.bmp
    /// (266x57, a blank gold-framed bar) - because ui/map_select/ ships NO
    /// button art at all: only the two backgrounds, the three mode pictures and
    /// the mask. Which picture the shipped screen dresses this row in is
    /// therefore NOT in the shipped data, and this is the port's reading (the
    /// same button the trail's start row and the siege's Setup row wear), named
    /// on the report.
    /// </summary>
    public static string MapRequestPlayButtonTexture =
      Constants.GamePath + "/ui/siege_menu/button_play.bmp";

    /// <summary>
    /// The LOAD screen (GameState.LOAD_SCREEN), which the war screen's
    /// WARSCREEN_LOAD row opens - the engine's `LoadScenarioScreen`, screen name
    /// 'load scenario', table family LOADSCENARIOSCREEN_*. Its backdrop is the
    /// file the class's own code loads as 'texture/loadscenario background':
    /// ui/ingame_menus/scenario_loadscreen.jpg (1024x768) - the barbarian and
    /// the knight flanking a big framed panel whose brown title strip carries
    /// the screen's header (LOADSCENARIOSCREEN_HEADER_LOAD 'Load' /
    /// _HEADER_SAVE 'Save', its two modes), with a scrollbar groove along the
    /// panel's right and two button plates along the bottom. Measured
    /// 2026-10-13: the panel's frame is x 214..801,
    /// y 105..662, its title strip y 113..139, its list area y 140..658 (the
    /// scrollbar groove around x 758..790); the two plates are x 221..463 and
    /// x 553..791, both y 686..728. The install ships three more
    /// loadscreen_*.jpg in ui/frontend/ and NONE of them is this screen's:
    /// loadscreen_2_name.jpg is the PLAYER NAME screen's background (the
    /// engine's PlayerNameScreen - its own code builds 'Enter Your Name' over
    /// that picture's empty frame) and loadscreen_3.jpg is referenced by the
    /// options/in-game screens' code, while loadscreen_1.jpg is referenced by
    /// nothing at all - all three measured in the exe, 2026-10-13.
    /// </summary>
    public static string LoadScenarioBackgroundTexture =
      Constants.GamePath + "/ui/ingame_menus/scenario_loadscreen.jpg";

    /// <summary>
    /// The SCENARIO LOBBY screen (GameState.SCENARIO_LOBBY_SCREEN), which the
    /// war screen's WARSCREEN_KINGMAKER row opens - the engine's own
    /// `ScenarioLobbyScreen`, screen name 'scenario lobby screen'. Its backdrop
    /// is the file that class's own code loads (measured 2026-10-13:
    /// 'ui\multiplayer\multiplayer_background.jpg', pushed at VA
    /// 0x6a8873 inside the class's own code region): the 1024x768 multiplayer
    /// lobby - the map frame with the four game-type medallions below it, the
    /// player panel on the right, the chat panel along the bottom and the round
    /// exit medallion in the picture's bottom-left corner.
    /// </summary>
    public static string MultiplayerLobbyBackgroundTexture =
      Constants.GamePath + "/ui/multiplayer/multiplayer_background.jpg";

    /// <summary>
    /// The lobby's four GAME-TYPE buttons, which the same class loads
    /// (button_gametype1_kingmaker.tga at VA 0x6a8c15; the four are the
    /// ui/multiplayer/ files the exe registers as 'texture/ui/lobby/gametype/1'
    /// .. '/4'). The first is the KINGMAKER one - the game type the war screen's
    /// Kingmaker row is about - and it ships its own highlight variant beside it,
    /// which is the one the screen wears for the selected type. The install ships
    /// no text row for any of the four: the mode's word is painted in the art,
    /// and the file names are where the four words come from.
    /// </summary>
    public static string GameTypeKingmakerTexture =
      Constants.GamePath + "/ui/multiplayer/button_gametype1_kingmaker.tga";
    public static string GameTypeKingmakerHighlightTexture =
      Constants.GamePath + "/ui/multiplayer/button_gametype1_kingmaker_highlight.tga";
    public static string GameTypeKnightTexture =
      Constants.GamePath + "/ui/multiplayer/button_gametype2_knight.tga";
    public static string GameTypeBaronTexture =
      Constants.GamePath + "/ui/multiplayer/button_gametype3_baron.tga";
    public static string GameTypeDeathmatchTexture =
      Constants.GamePath + "/ui/multiplayer/button_gametype4_deathmatch.tga";

    // The chapter introductions: one 1024x768 picture per campaign chapter, the
    // same shape as the briefing backdrop, each with the chapter's painted -
    // and EMPTY - title ribbon across its top (measured: the ribbon's red band
    // is y 16..126 of 768 in all twelve, x 8..972 wide, so the chapter's title
    // is the text the art leaves room for). The pictures are the install's own
    // per-chapter art and are named by the chapter's number, one-based:
    // chapter01_intro.jpg is chapter 1's.
    public static string ChapterIntroTexture(int chapter) =>
      Constants.GamePath + $"/ui/chapter_introductions/chapter{chapter:D2}_intro.jpg";
  }
}
