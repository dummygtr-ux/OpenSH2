using UnityEngine;
using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// The war screen's own actions, in the shipped screen's own order: the six
  /// ways into the war game modes, and the way back to the play screen.
  ///
  /// Where these come from: the same shipped translation table as the main
  /// menu's six and the play screen's four (i18n/en-us.xml - the shipped help
  /// file documents the game, not the frontend). The war screen is the screen
  /// the play screen's WARSCREEN_WAR_TOOLTIP_TITLE row opens, and the table
  /// names its six buttons (WARSCREEN_CAMPAIGN / _CONQUEST / _KINGMAKER /
  /// _SIEGE / _CUSTOM / _LOAD) and its own way back
  /// (WARSCREENSTATICTOOLTIP_EXIT 'Previous menu') - see
  /// <see cref="All"/> for the rows, each with the tooltip content the same
  /// table ships for it.
  ///
  /// The pictures are the install's too, from the folder the play screen's two
  /// path buttons came from: ui/war_peace_menu/{war_campaign,war_kingmaker,
  /// war_siege,warpeace_custom,warpeace_load}.jpg - and the Conquest Trail's,
  /// which the Gold Edition shipped separately as
  /// ui/gold_edition/war_conquest.jpg (the trail's own castle map, the arrows
  /// and the win screen are in the same folder). The only picture in
  /// ui/war_peace_menu/ that is NOT one of these six is war_multiplayer.jpg,
  /// which is named for the multiplayer screens the main menu's own
  /// MAINMENU_MULTIPLAYER row opens.
  /// </summary>
  public static class WarScreenActions
  {
    /// <summary>
    /// The screen's six picture buttons, in the order the shipped screen shows
    /// them. As of 2026-10-13 ALL SIX have their screens in this
    /// port - the Campaign row's (GameState.CAMPAIGN_SCREEN), the Conquest
    /// Trail's (GameState.CONQUEST_TRAIL_SCREEN), the Kingmaker row's
    /// (GameState.SCENARIO_LOBBY_SCREEN), the Siege row's (GameState.SIEGE_SCREEN),
    /// the Custom row's (GameState.MAP_REQUEST_SCREEN) and the Load row's
    /// (GameState.LOAD_SCREEN) - so every row says where it goes and none is left
    /// naming a screen the port cannot show.
    ///
    /// The Kingmaker destination is MEASURED too, and it CORRECTED the port's
    /// earlier reading (2026-10-13): the row does not open the Kingmaker (skirmish) setup of the SCENARIO EDITOR's own
    /// family (SCENARIOEDITORSCREEN_MAKE_SKIRMISH 'Kingmaker', SKIRMISH_LORD_*),
    /// which is where an earlier reading had put it; the war screen's own
    /// handler gives this row's control (object offset +0x834, labelled
    /// WARSCREEN_KINGMAKER here) the action id whose branch pushes the screen
    /// name 'scenario lobby screen' - the engine's `ScenarioLobbyScreen`, the
    /// multiplayer lobby, whose game-type strip is where Kingmaker is one of four
    /// modes. See <see cref="ScenarioLobbyActions"/>.
    /// </summary>
    /// The Custom and Load destinations are MEASURED, not read off the table's
    /// nearby ids (2026-10-13): the shipped
    /// engine's own war-screen handler carries the Custom row's own picture
    /// ('warpeace_custom.jpg') into the screen it names 'map request screen'
    /// and opens that screen (the class `MapRequestScreen`, whose code loads
    /// ui/map_select/mapselect_background.jpg and _war.jpg under the ids
    /// 'ui/map request bg' / 'ui/map request war bg'), and the screen whose
    /// table family is LOADSCENARIOSCREEN_* is the engine's `LoadScenarioScreen`
    /// ('load scenario'), whose code loads ui/ingame_menus/scenario_loadscreen.jpg
    /// as 'texture/loadscenario background'. The scenario list the port's earlier
    /// reading had behind Custom (MAINSCENARIOSCREEN_HELP_CREATE_NEW_SCENARIO /
    /// _LOAD_SCENARIO) lives in the engine's `MainScenarioEditorScreen` ('main
    /// scenario editor') - the MAP EDITOR's own list, behind the main menu's
    /// unbuilt Map Editor row - and NOT behind the war screen's Custom.
    /// </summary>
    public static readonly PathScreenRow[] Pictures =
    {
      new("WARSCREEN_CAMPAIGN", "Campaign", "Play the 'Lost King' campaign",
          "the campaign screen (CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12 'Start Chapter 1..12', "
          + "maps/war_chapter1..12.s2m)",
          GameState.CAMPAIGN_SCREEN, UIAssets.WarCampaignTexture, "CampaignButton"),
      new("WARSCREEN_CONQUEST", "Conquest Trail",
          "Seige and defend the Strongholds of Europe as you conquer the continent.",
          "the Conquest Trail's castle map (CONQUESTTRAIL_1..12, ui/gold_edition/conquest_trail.png)",
          GameState.CONQUEST_TRAIL_SCREEN, UIAssets.WarConquestTexture, "ConquestButton"),
      new("WARSCREEN_KINGMAKER", "Kingmaker", "Skirmish for the crown, in a Kingmaker challenge",
          "the scenario lobby screen (the engine's own ScenarioLobbyScreen, screen name "
          + "'scenario lobby screen': ui/multiplayer/multiplayer_background.jpg with the four "
          + "game-type buttons - Kingmaker is game type 1 of them - and the lobby's own "
          + "LOBBYSCREEN_* rows)",
          GameState.SCENARIO_LOBBY_SCREEN, UIAssets.WarKingmakerTexture, "KingmakerButton"),
      new("WARSCREEN_SIEGE", "Siege", "Relive history. Siege a real castle!",
          "the siege selection (HISTORICCASTLE00..43, the install's own twenty-two castles, "
          + "ui/siege_menu/)",
          GameState.SIEGE_SCREEN, UIAssets.WarSiegeTexture, "SiegeButton"),
      new("WARSCREEN_CUSTOM", "Custom", "Load a user created scenario",
          "the map request screen (MAPREQUESTSCREEN_PLAY 'Play'; ui/map_select/mapselect_background_war.jpg "
          + "and the install's own maps - the shipped engine's Custom branch hands this row's own picture "
          + "to that screen and opens it)",
          GameState.MAP_REQUEST_SCREEN, UIAssets.WarCustomTexture, "CustomButton"),
      new("WARSCREEN_LOAD", "Load", "Load a previously saved game",
          "the load screen (LOADSCENARIOSCREEN_HEADER_LOAD 'Load', the Name/Date/P columns, "
          + "LOADSCENARIOSCREEN_LOAD 'Load' / _CANCEL 'Cancel'; the engine's own LoadScenarioScreen, "
          + "ui/ingame_menus/scenario_loadscreen.jpg)",
          GameState.LOAD_SCREEN, UIAssets.WarLoadTexture, "LoadButton"),
    };

    /// <summary>
    /// The way back (WARSCREENSTATICTOOLTIP_EXIT 'Previous menu') - the only row
    /// on this screen with a screen behind it, and it is the play screen the war
    /// screen was opened from: the frontend's two path screens are a branch of
    /// the play screen, not of each other, and the play screen's own way back is
    /// the menu's.
    /// </summary>
    public static readonly PathScreenRow Back = new(
        "WARSCREENSTATICTOOLTIP_EXIT", "Previous menu", "",
        "the play screen (the screen the war screen was opened from, which the port has)",
        GameState.PLAY_SCREEN, UIAssets.FancyButtonTexture, "BackButton");

    /// <summary>Every row this screen dispatches: the six pictures, then the way back.</summary>
    public static readonly PathScreenRow[] All = AppendBack();

    /// <summary>The row for one picture (the picture index, in the shipped screen's order).</summary>
    public static PathScreenRow Picture(int index) => Pictures[index];

    private static PathScreenRow[] AppendBack()
    {
      var all = new PathScreenRow[Pictures.Length + 1];
      Pictures.CopyTo(all, 0);
      all[Pictures.Length] = Back;
      return all;
    }

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static PathScreenRow LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. WarScreenUI builds its buttons once per showing,
    /// so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a war-screen action. Names the row, records it,
    /// reports its destination - and, for the one row the port has built (the way
    /// back), asks the flow for it through the same logged transition the menu's
    /// Play uses.
    /// </summary>
    public static void Dispatch(PathScreenRow row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;
      Debug.Log($"[WarScreenActions] click: {row.Id} '{row.Label}' -> {row.Outcome}");
      if (row.Built) Game.GetInstance()?.RequestFrontendScreen(row.Opens);
    }
  }
}
