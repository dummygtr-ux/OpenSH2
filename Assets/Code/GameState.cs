using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Code
{
  public enum GameState
  {
    NONE = 0,
    LOADING_ASSETS = 1,
    INTRO_VIDEOS = 10,
    MAIN_MENU = 15,

    /// <summary>
    /// The shipped play screen - PLAYSCREEN 'Choose Your Path' - which the
    /// MAINMENU_PLAY row opens. One of the frontend's own screens: reachable
    /// by a click (Game.RequestFrontendScreen), never by the boot.
    /// </summary>
    PLAY_SCREEN = 20,

    /// <summary>
    /// The shipped war screen (WARSCREEN_* Campaign / Conquest Trail /
    /// Kingmaker / Siege / Custom / Load), which the play screen's War row
    /// opens. A frontend screen like the play screen: a click moves the flow.
    /// </summary>
    WAR_SCREEN = 25,

    /// <summary>
    /// The shipped peace screen (PEACESCREEN_* Sim Campaign / Free Build /
    /// Custom Maps), which the play screen's Peace row opens.
    /// </summary>
    PEACE_SCREEN = 26,

    /// <summary>
    /// The shipped campaign screen (CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12
    /// 'Start Chapter 1..12' - the 'Lost King' campaign's chapter list), which
    /// the war screen's Campaign row opens. A frontend screen like the rest:
    /// a click moves the flow, and its own way back is the WAR SCREEN it was
    /// opened from.
    /// </summary>
    CAMPAIGN_SCREEN = 27,

    /// <summary>
    /// The shipped Conquest Trail screen (CONQUESTTRAIL_1..12 - the trail's
    /// castle map with its twelve mission briefs, its two paging rows and
    /// CONQUEST START MISSION), which the war screen's Conquest Trail row opens.
    /// A frontend screen like the rest: a click moves the flow, and its own way
    /// back is the WAR SCREEN it was opened from.
    /// </summary>
    CONQUEST_TRAIL_SCREEN = 28,

    /// <summary>
    /// The shipped siege selection screen (SIEGESELECTIONSCREEN_SETUP 'Setup' /
    /// SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME 'Select this Siege' - the
    /// install's own castle list, HISTORICCASTLE00..43, with the selected
    /// castle's painting and layout picture and its own description prose),
    /// which the war screen's Siege row opens. A frontend screen like the rest:
    /// a click moves the flow, and its own way back
    /// (SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT 'Previous Menu') is the WAR SCREEN
    /// it was opened from.
    /// </summary>
    SIEGE_SCREEN = 29,

    /// <summary>
    /// The map request screen (MAPREQUESTSCREEN_PLAY 'Play' - the engine's own
    /// screen, `MapRequestScreen`), which the war screen's Custom row opens:
    /// ui/map_select/'s own two backgrounds (the war variant is this path's),
    /// the mode's own 256x256 picture (mapselect_custom.bmp) and the screen's
    /// own map list - the install's scenarios. A frontend screen like the rest:
    /// a click moves the flow, and its own way back is the WAR SCREEN it was
    /// opened from.
    /// </summary>
    MAP_REQUEST_SCREEN = 34,

    /// <summary>
    /// The load screen (LOADSCENARIOSCREEN_HEADER_LOAD 'Load' - the engine's
    /// own `LoadScenarioScreen`, screen name 'load scenario'), which the war
    /// screen's Load row opens: ui/ingame_menus/scenario_loadscreen.jpg with
    /// the screen's own header, its Name/Date/P columns and its Load/Cancel
    /// rows. It lists the player's saved games - the port ships none. A
    /// frontend screen like the rest: a click moves the flow, and its own
    /// Cancel row is the WAR SCREEN it was opened from.
    /// </summary>
    LOAD_SCREEN = 35,

    /// <summary>
    /// The scenario lobby screen (the engine's own `ScenarioLobbyScreen`, screen
    /// name 'scenario lobby screen'), which the war screen's Kingmaker row opens.
    /// Measured 2026-10-13: the row's control is the one the war
    /// screen labels WARSCREEN_KINGMAKER, and its action id's branch is the one
    /// that pushes this screen's name - so Kingmaker is a GAME TYPE inside the
    /// shared multiplayer lobby, not a screen of its own, and the lobby's own art
    /// is ui/multiplayer/'s (multiplayer_background.jpg with the four
    /// button_gametype*.tga buttons). A frontend screen like the rest: a click
    /// moves the flow, and its own Back row is the WAR SCREEN it was opened from.
    /// </summary>
    SCENARIO_LOBBY_SCREEN = 36,

    /// <summary>
    /// A chapter's map is being loaded: the campaign screen's chapter row asked
    /// for it (Game.RequestStartChapter names the row's own maps/war_chapterN.s2m)
    /// and Main.TickGameFlow is building the map world - terrain, water, walls,
    /// vegetation, buildings and actors through the SAME loaders the dev-harness
    /// path uses (S2MMapWorldLoader). The first state this port enters that
    /// belongs to the game rather than to the frontend.
    /// </summary>
    MAP_LOADING = 30,

    /// <summary>
    /// The clicked chapter's map world stands. One tick long: the mission's
    /// briefing screen is the next thing the flow shows, so this is the state
    /// the load's own result is read in (§10) and BRIEFING follows it.
    /// </summary>
    MAP_LOADED = 31,

    /// <summary>
    /// The mission's briefing screen is up: the chapter's own picture, its title
    /// on the picture's ribbon, its hints, the shipped screen's three tabs and
    /// one of its two play rows - the game's own IN-GAME screen
    /// (MISSIONOBJECTIVESSCREEN*, art ui/briefing_screen/ plus the per-chapter
    /// ui/chapter_introductions/chapterNN_intro.jpg). It has two showings and
    /// they differ only in which play row they wear (Game.BriefingIsReview):
    /// the mission-START briefing, which a loaded chapter's map puts up and
    /// whose 'To the Game' row leaves for GAMEPLAY; and the in-game REVIEW,
    /// which the main screen's Objectives row opens and whose 'Return to the
    /// Game' row comes back to GAMEPLAY. Both go through
    /// Game.RequestLeaveBriefing.
    /// </summary>
    BRIEFING = 32,

    /// <summary>
    /// The mission is the game's: the main screen is up (UI/MainScreenUI.cs) -
    /// the install's own status bar with the loaded mission's own decoded
    /// numbers (date, gold, popularity) in its slots, and the Objectives row
    /// that opens the briefing's in-game review. The gameplay state machine
    /// proper (its simulation, the HUD's other surfaces) is still to come; what
    /// is up here is the mission's own state, not a simulation.
    /// </summary>
    GAMEPLAY = 33,
  }
}
