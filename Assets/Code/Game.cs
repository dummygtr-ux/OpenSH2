using Assets.Code.Caching;
using Assets.Code.UI;
using Assets.Code.Video;
using System;

namespace Assets.Code
{
  class Game
  {
    private static Game Instance = null;

    public static Game Initialize()
    {
      if (Instance != null)
      {
        throw new Exception("Already initialized Game instance.");
      }

      Instance = new Game();
      return Instance;
    }

    public static Game GetInstance()
    {
      return Instance!;
    }

    public GameState State { get; private set; } = GameState.NONE;
    public CacheManager cacheManager;
    public VideoManager videoManager;
    public MainMenuUI mainMenuUI;
    public PlayScreenUI playScreenUI;
    public WarScreenUI warScreenUI;
    public PeaceScreenUI peaceScreenUI;
    public CampaignScreenUI campaignScreenUI;

    /// <summary>
    /// The Conquest Trail screen (GameState.CONQUEST_TRAIL_SCREEN) - the trail's
    /// castle map, which the war screen's Conquest Trail row opens. The war
    /// screen's own second branch, beside the campaign screen.
    /// </summary>
    public ConquestScreenUI conquestScreenUI;

    /// <summary>
    /// The siege selection screen (GameState.SIEGE_SCREEN) - the install's own
    /// castle list (HISTORICCASTLE00..43), which the war screen's Siege row
    /// opens. The war screen's own third branch, beside the campaign screen and
    /// the Conquest Trail.
    /// </summary>
    public SiegeScreenUI siegeScreenUI;

    /// <summary>
    /// The map request screen (GameState.MAP_REQUEST_SCREEN) - the install's own
    /// map list (ui/map_select/), which the war screen's Custom row opens. The
    /// war screen's own fourth branch.
    /// </summary>
    public MapRequestScreenUI mapRequestScreenUI;

    /// <summary>
    /// The load screen (GameState.LOAD_SCREEN) - the engine's own
    /// LoadScenarioScreen (screen name 'load scenario'), which the war screen's
    /// Load row opens: its shipped header, its Name/Date/P columns and its
    /// Load/Cancel rows over the install's own picture, with the player's saved
    /// games list - which this port ships none of. The war screen's own fifth
    /// branch.
    /// </summary>
    public LoadScreenUI loadScreenUI;

    /// <summary>
    /// The scenario lobby screen (GameState.SCENARIO_LOBBY_SCREEN) - the engine's
    /// own `ScenarioLobbyScreen` (screen name 'scenario lobby screen'), which the
    /// war screen's Kingmaker row opens: the install's own multiplayer lobby
    /// (ui/multiplayer/multiplayer_background.jpg) with its four game-type
    /// buttons on the medallions the picture paints - Kingmaker is game type 1
    /// of them, the type the row came in by - and the lobby's own rows
    /// LOBBYSCREEN_START 'Start' / LOBBYSCREEN_BACK 'Back'. The war screen's own
    /// sixth branch, and the last of its six.
    /// </summary>
    public ScenarioLobbyUI scenarioLobbyUI;

    /// <summary>
    /// The mission's briefing screen (GameState.BRIEFING) - the game's own
    /// screen, not the frontend's: a clicked chapter's map puts it up, and its
    /// 'To the Game' row is what leaves it for the mission. It has two
    /// showings: the mission-start briefing, and the in-game review the main
    /// screen's Objectives row opens (Game.BriefingIsReview).
    /// </summary>
    public BriefingScreenUI briefingScreenUI;

    /// <summary>
    /// The in-game main screen (GameState.GAMEPLAY) - the game's own surface
    /// the briefing hands the map to: the install's own status bar with the
    /// loaded mission's own decoded numbers, and the Objectives row.
    /// </summary>
    public MainScreenUI mainScreenUI;

    public Game()
    {
      cacheManager = new();
      videoManager = new();
      mainMenuUI = new();
      playScreenUI = new();
      warScreenUI = new();
      peaceScreenUI = new();
      campaignScreenUI = new();
      conquestScreenUI = new();
      siegeScreenUI = new();
      mapRequestScreenUI = new();
      loadScreenUI = new();
      scenarioLobbyUI = new();
      briefingScreenUI = new();
      mainScreenUI = new();
    }

    /// <summary>
    /// Which showing the briefing screen is in: false while it is the
    /// mission-start briefing (a loaded chapter's map puts it up), true while
    /// it is the in-game review (the main screen's Objectives row opens it -
    /// Game.RequestViewObjectives). The screen reads it when it is built, and
    /// the showings differ only in which of the shipped screen's two play rows
    /// they wear ('To the Game' vs 'Return to the Game'). Both are the
    /// BRIEFING state - the same screen - and both leave it through
    /// Game.RequestLeaveBriefing, so the review is the way BACK to the
    /// mission, never a second way into it.
    /// </summary>
    public bool BriefingIsReview { get; private set; }

    /// <summary>
    /// THE one transition: every state change goes through here and says so on
    /// the console - the boot's own three (Main.TickGameFlow) and the
    /// frontend's screen changes alike, in one wording. The UAT reads these
    /// lines.
    /// </summary>
    public void SetState(GameState next)
    {
      UnityEngine.Debug.Log($"Game flow: {State} -> {next}.");
      State = next;
    }

    /// <summary>
    /// A frontend click asking for the screen it opens. Only the screens the
    /// frontend owns can be asked for, and only from one of them - so a click
    /// can never skip or re-enter the boot (LOADING_ASSETS / INTRO_VIDEOS stay
    /// the flow's own). False with a reason when the request is refused; the
    /// caller's click has still been recorded by its own action surface.
    /// </summary>
    public bool RequestFrontendScreen(GameState next)
    {
      if (!IsFrontendScreen(State) || !IsFrontendScreen(next))
      {
        UnityEngine.Debug.LogWarning(
          $"Frontend: refusing a screen change {State} -> {next}; "
          + "only the frontend's own screens can be opened by a click.");
        return false;
      }

      SetState(next);
      return true;
    }

    /// <summary>
    /// A campaign screen chapter row asking to start its chapter: the map file
    /// the row names is loaded by the game's own flow (MAP_LOADING -> MAP_LOADED,
    /// Main.TickGameFlow's map branch) - the one click that LEAVES the
    /// frontend's tree for the game itself. Only the campaign screen's own
    /// chapter rows may ask, and only with a map path, so no other screen can
    /// start a chapter and a click can never load an arbitrary file. False with
    /// a reason when the request is refused.
    /// </summary>
    public bool RequestStartChapter(string mapPath)
    {
      if (State != GameState.CAMPAIGN_SCREEN || string.IsNullOrEmpty(mapPath))
      {
        UnityEngine.Debug.LogWarning(
          $"Frontend: refusing a chapter start ('{mapPath}') from {State}; "
          + "only the campaign screen's own chapter rows can start a chapter.");
        return false;
      }

      CurrentMapPath = mapPath;
      // The briefing this map leads to is the mission-start one, never a
      // review left over from the chapter before it.
      BriefingIsReview = false;
      SetState(GameState.MAP_LOADING);
      return true;
    }

    /// <summary>
    /// The map currently being played - the chapter row's own file
    /// (maps/war_chapterN.s2m), kept by RequestStartChapter until a later
    /// chapter start replaces it. The map load, the briefing screen's content
    /// and the UAT all read it; nothing else writes it.
    /// </summary>
    public string CurrentMapPath { get; private set; }

    /// <summary>
    /// The briefing screen's 'To the Game' row asking to start the mission: the
    /// briefing is done and the map becomes the game's (GameState.GAMEPLAY). Only
    /// the briefing screen's own play row may ask, so no click can skip the
    /// briefing or start a mission from anywhere else. False with a reason when
    /// the request is refused.
    /// </summary>
    public bool RequestLeaveBriefing()
    {
      if (State != GameState.BRIEFING)
      {
        UnityEngine.Debug.LogWarning(
          $"Mission: refusing to leave the briefing from {State}; "
          + "only the briefing screen's own play row ('To the Game' / 'Return to the Game') "
          + "can start the mission or come back to it.");
        return false;
      }

      BriefingIsReview = false;
      SetState(GameState.GAMEPLAY);
      return true;
    }

    /// <summary>
    /// The main screen's Objectives row (MAINSCREENROLLOVERS086) asking for the
    /// mission objectives: the briefing screen comes back up as the IN-GAME
    /// REVIEW (GameState.BRIEFING with BriefingIsReview set), wearing the
    /// shipped screen's other play row - 'Return to the Game'
    /// (MISSIONOBJECTIVESSCREENSTATICTOOLTIP_RETURN_TO_GAME) - which leaves it
    /// again through RequestLeaveBriefing. Only the main screen's own Objectives
    /// row may ask, and only while the mission is the game's screen, so no
    /// click can re-open the briefing mid-load or from a frontend screen.
    /// False with a reason when the request is refused.
    /// </summary>
    public bool RequestViewObjectives()
    {
      if (State != GameState.GAMEPLAY)
      {
        UnityEngine.Debug.LogWarning(
          $"Mission: refusing to open the mission objectives from {State}; "
          + "only the main screen's own Objectives row can open them.");
        return false;
      }

      BriefingIsReview = true;
      SetState(GameState.BRIEFING);
      return true;
    }

    /// <summary>
    /// The screens a click may move between (the boot's states are not among
    /// them). The menu is the hub; the play screen is the menu's first
    /// destination; the two path screens are branches of the play screen, so a
    /// path screen's own way back is the play screen (never the menu, which the
    /// play screen's own way back reaches); and the campaign screen, the Conquest
    /// Trail, the siege selection, the map request screen and the load screen are
    /// branches of the WAR screen, so their own ways back are the war screen.
    /// </summary>
    public static bool IsFrontendScreen(GameState state) =>
      state == GameState.MAIN_MENU
      || state == GameState.PLAY_SCREEN
      || state == GameState.WAR_SCREEN
      || state == GameState.PEACE_SCREEN
      || state == GameState.CAMPAIGN_SCREEN
      || state == GameState.CONQUEST_TRAIL_SCREEN
      || state == GameState.SIEGE_SCREEN
      || state == GameState.MAP_REQUEST_SCREEN
      || state == GameState.LOAD_SCREEN
      || state == GameState.SCENARIO_LOBBY_SCREEN;
  }
}
