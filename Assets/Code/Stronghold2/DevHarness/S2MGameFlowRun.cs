using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Assets.Code;
using Assets.Code.Stronghold2.Audio;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.Missions;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.TerrainRendering;
using Assets.Code.UI;
using Assets.Code.Video;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// The game-flow UAT, in PLAY MODE - the one check the batchmode harnesses
  /// cannot make, because the whole point of the flow is that a real player
  /// loop drives it (Main.Update -> Main.TickGameFlow) and batchmode's
  /// -executeMethod never gets one.
  ///
  ///   Unity -projectPath &lt;proj&gt; -logFile /tmp/sh2-gui-flow.log \
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MGameFlowRun.Main
  ///
  /// MEMORY: this is the one harness that needs a real editor, and an editor
  /// holding this project sits at several GB - on a 15 GB machine that is
  /// enough to have the desktop (and a browser) swapped or killed (measured
  /// 2026-10-12: a run in flight alongside the editor tripped the OOM
  /// killer). Close the editor the moment the report says
  /// SUMMARY - normally the runner does it itself with EditorApplication.Exit,
  /// and after an aborted run: `pkill -f Editor/Unity`.
  ///
  /// NOT -batchmode: the menu capture is what a window renders, and a
  /// batchmode Game view does not exist. The runner drives the editor itself -
  /// entering Play mode and reading the console, scripted - and closes it
  /// when the report is written.
  ///
  /// Two play-mode runs in one editor session, following the switch in Main.cs:
  ///
  ///   run 1  /tmp/sh2-flow.txt = "game": the scene boots the game's own flow.
  ///          The report carries the console's state transitions and both
  ///          music cues, the menu's AudioSource sampled TWICE (the track, its
  ///          volume-list gain, and AudioSource.time ADVANCING in real time -
  ///          the number that stays 0.0000 under batchmode), a capture of the
  ///          menu, and the menu's own sprites being non-null (the UI assets
  ///          resolving is what a real menu needs). The menu phase also watches
  ///          the CURTAIN hand over - curtain_animation once, then curtain_idle
  ///          looping (MainMenuVideoUiManager) - on the players' own
  ///          frame/time/isLooping, since that hand-off is the first video
  ///          completion in the port that nothing skips. It then drives the
  ///          menu's six buttons through their own pointer click: the five with
  ///          no destination first, then the PLAY click, which opens the port's
  ///          first frontend destination - the play screen (PLAYSCREEN 'Choose
  ///          Your Path'). The play screen is judged on the screen itself (its
  ///          canvas, its ui/choose_path/ background, its shipped title and
  ///          four action buttons), its unbuilt action is clicked too (it
  ///          reports its shipped screen as not built), and then the round trip
  ///          walks the frontend's whole tree and returns: the play screen's War
  ///          click opens the war screen, that screen's Campaign click walks one
  ///          branch deeper - the campaign screen (the 'Lost King' campaign's
  ///          twelve-chapter list, whose own Previous Menu comes back to the WAR
  ///          screen), the war screen's Conquest Trail click walks its SECOND
  ///          branch (the trail's castle map, its twelve mission rows, its own
  ///          paging and its start row, whose own Previous menu comes back to the
  ///          WAR screen as well), the war screen's Siege click walks its THIRD
  ///          branch (the install's own castle list - twenty-two HISTORICCASTLE
  ///          name/description pairs - with the selected castle's painting, its
  ///          layout picture and its own description prose, all twenty-two rows
  ///          clicked, whose own Previous Menu comes back to the WAR screen too),
  ///          the war screen's Custom click walks its FOURTH branch (the map
  ///          request screen - ui/map_select/'s own war-path backdrop, the
  ///          custom mode's own 256x256 picture, and the install's own maps
  ///          read at run time, twelve to a page with the screen's two painted
  ///          arrows paging them, whose own way back comes back to the WAR
  ///          screen), the war screen's Load click walks its FIFTH branch (the
  ///          load screen - the engine's own LoadScenarioScreen, screen name
  ///          'load scenario', on the picture the class itself loads,
  ///          ui/ingame_menus/scenario_loadscreen.jpg, with its shipped header,
  ///          its Name/Date/P columns - which are its three sort rows - and its
  ///          Load/Cancel plates; its list is EMPTY because the port ships no
  ///          saved games, and that is judged too), and then the war screen's
  ///          own Previous menu
  ///          comes back to the PLAY screen, its Peace click opens the peace
  ///          screen, that screen's Previous menu comes back again, and the play
  ///          screen's own Previous Menu is the last click - back at MAIN_MENU,
  ///          where the menu must be the six-button menu again, its dispatch
  ///          record fresh, and the frontend's cue NOT restarted. All seven
  ///          destination screens are judged on themselves the same way (their
  ///          canvas, their own art, their own bars, their own pictures and
  ///          shipped labels, their destination-less rows clicked through their
  ///          own one entry point; the campaign screen's buttons are additionally
  ///          judged against the slots the installed art paints for them, the
  ///          trail's against the install's own paging tiles, its twelve rows, its
  ///          castle paintings and historical maps and its own progressive
  ///          pictures, the siege selection's against the install's castle
  ///          list, its paintings and layout pictures and its own historical
  ///          castle maps, the map request screen's against the install's own
  ///          maps folder read a second time and against its own per-map
  ///          artifacts, and the load screen's against its empty list - the
  ///          install's own truth on a fresh machine) and all seven are
  ///          captured. The
  ///          trip done and the menu back, the runner walks the first three
  ///          screens once more (Play -> War -> Campaign) and clicks ONE chapter
  ///          row - 'Chapter 4', not the dev harness's own chapter - the one
  ///          frontend click that LEAVES the frontend: the flow logs
  ///          CAMPAIGN_SCREEN -> MAP_LOADING -> MAP_LOADED, the chapter's own
  ///          map (maps/war_chapter4.s2m) is built by the game's flow through
  ///          the same loader the dev harness uses, and the loaded world is
  ///          judged against the port's manifests (its scenario census against
  ///          scenario_missions/events.csv, its 191 actors against
  ///          entity_estates.csv, its placements against
  ///          placement_verification.csv, its wall cells against
  ///          walls_war_chapter4.json) plus a capture;
  ///   run 2  /tmp/sh2-flow.txt = "dev": the same scene boots the dev-harness
  ///          map render instead - the path every earlier check used - and
  ///          the report checks it still loads war_chapter8 whole (map world,
  ///          220 building + prop instances, 176 scenery, 340 actors) and that
  ///          NO music played.
  ///
  /// What has to survive the domain reload each Play-mode entry performs lives
  /// in SessionState (the phase, its start, its deadline); everything else is
  /// re-derived from the report file, which is written line by line as the run
  /// goes, so a reload - or a crash - never loses what was already proven.
  /// </summary>
  public static class S2MGameFlowRun
  {
    /// <summary>The install the flow's assets and music come from.</summary>
    private static string GameRoot => Constants.GamePath;

    /// <summary>The switch file Main.cs reads: line 1 = "game" or "dev".</summary>
    private const string FlowOverridePath = "/tmp/sh2-flow.txt";

    /// <summary>The dev harness's own map override, moved aside for run 2.</summary>
    private const string TestMapOverridePath = "/tmp/sh2-testmap.txt";

    /// <summary>
    /// The intro switch's own override (line 1 = off / full / seconds), moved
    /// aside for the run so the serialized field decides what the run measures.
    /// </summary>
    private const string IntroBudgetOverridePath = "/tmp/sh2-intro.txt";

    private const string DefaultReportPath = "/tmp/sh2_game_flow_report.txt";

    /// <summary>What the Game view render is captured to (run 1).</summary>
    private const string MenuCapturePath = "/tmp/sh2_game_flow_menu.png";

    /// <summary>The fallback capture: the menu canvas drawn through a camera.</summary>
    private const string MenuCanvasCapturePath = "/tmp/sh2_game_flow_menu_canvas.png";

    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // What the shipped sheets say the flow's two moments must play (the python
    // manifest, sh2-port/manifests/audio_music.csv). The expected names are the
    // resolved FILES: Unity names a decoded clip after the file, extension
    // included ("the lamb_lp.mp3", not the sheet's cell "the lamb_lp.mp3").
    private const string LoadingPlaylist = "PL_LOADING";
    private const string LoadingFirstTrack = "the lamb_lp.mp3";
    private const float LoadingGain = 110f / 127f;
    private const string MenuPlaylist = "PL_MAINMENU";
    private const string MenuFirstTrack = "The Smith.mp3";
    private const float MenuGain = 115f / 127f;

    // The dev harness's own map (war_chapter8) as its JSONs describe it:
    // 192 buildings + 28 props under "S2M Buildings", 176 rocks under
    // "S2M Scenery", 340 actors under "S2M Entities".
    private const int Ch8BuildingInstances = 220;
    private const int Ch8SceneryInstances = 176;
    private const int Ch8EntityInstances = 340;

    // Budgets in seconds. The game-flow budget covers what the boot really
    // costs here: the CacheManager converts whatever is not cached yet with
    // the port's VP8 settings (measured at ~0.05x realtime - which is why the
    // 125 s intro FMV is pre-cached), then the intro videos
    // play in real time (5 s + 125 s).
    private const double GameFlowBudgetSeconds = 40 * 60;
    private const double MenuObservationSeconds = 3.5;
    private const double CaptureWaitSeconds = 2.5;

    /// <summary>
    /// How long the menu phase waits for the curtain's two clips to hand over
    /// and for the idle clip to wrap: the open clip is 11.3 s and the idle 8 s
    /// of 30 fps art, and both are decoded as fast as the machine can (the
    /// players run Freerun), so this is slack for a slow machine, not the
    /// expected wait - the hand-off itself takes ~20 s.
    ///
    /// It is generous because a WEDGED video module has been measured under
    /// this phase: a player destroyed mid-prepare (see SkipStartedIntroVideo)
    /// held the menu's two players at frame 0 for ~85 s before they started.
    /// That is a runner-caused state and is fixed at its cause, but a budget
    /// that could be exhausted by the same 85 s would report it as a failed
    /// hand-off - the one thing this phase must never do.
    /// </summary>
    private const double CurtainBudgetSeconds = 240;
    private const double DevHarnessBudgetSeconds = 10 * 60;
    private const double ExitPlaymodeSeconds = 30;

    /// <summary>
    /// How long the play-screen phase waits for the screen the Play click asked
    /// for: the flow shows it on its own next Update (Main.TickGameFlow), so a
    /// couple of frames is the expected wait and this only bounds a hang - the
    /// same slack the menu's phase carries, for the same reason (a click and a
    /// screen change land on different ticks, so the runner must not judge on
    /// the tick it clicked from).
    /// </summary>
    private const double PlayScreenBudgetSeconds = 30;
    private const double PlaymodeStartSeconds = 10;
    private const int MaxPlaymodeAttempts = 6;
    /// <summary>How long the intro is left alone before the runner skips it.</summary>
    private const double IntroAutoSkipSeconds = 12;

    /// <summary>
    /// How long a single intro video may still be PREPARING before the runner
    /// skips it anyway - the only way the intro phase can outlast its window,
    /// and only for a video that never starts.
    /// </summary>
    private const double IntroPrepareGraceSeconds = 30;

    private enum Phase
    {
      Idle = 0,
      GameFlow = 1,    // play run 1: the game boots through its own flow
      Menu = 2,        // the menu is up: sample the music a second time
      Capture = 3,     // read the menu capture back, then ask for the play screen
      ExitPlaymode = 4,// run 1 is over: wait for the editor to leave play mode
      DevHarness = 5,  // play run 2: the switch's other path
      Finish = 6,      // write the summary and close the editor
      PlayScreen = 7,  // the play screen is up: judge it, then capture it
      PlayScreenCapture = 8, // read the play-screen capture back, then click War
      MenuBack = 9,    // the menu is back: judge the round trip, then hand over
      WarScreen = 10,  // the war screen is up: judge it, capture it, then Previous Menu
      BackToPlayFromWar = 11,  // the play screen is back: judge it, then click Peace
      PeaceScreen = 12,        // the peace screen is up: judge it, capture it, then Previous Menu
      BackToPlayFromPeace = 13, // the play screen is back again: judge it, then click Back
      CampaignScreen = 14,      // the campaign screen is up: judge it, capture it, then Previous Menu
      BackToWarFromCampaign = 15, // the war screen is back: judge it, then its own Previous Menu
      ChapterWalk = 16,         // back at the menu: walk to the campaign screen again and click a chapter
      ChapterMap = 17,          // the chapter click left the frontend: judge the load and the map world
      Briefing = 18,            // the mission's briefing screen is up: judge it, click its tabs, then 'To the Game'
      AfterBriefing = 19,       // 'To the Game' clicked: the flow leaves the briefing for the mission
      MainScreen = 20,          // the mission's main screen is up: judge it, capture it
      MainScreenCapture = 21,   // read the main screen's capture back, then click its Objectives row
      ObjectivesReview = 22,    // the mission objectives are up as the in-game review: judge it, click 'Return to the Game'
      BackFromReview = 23,      // the main screen is back: judge the round trip, then hand over to run 2
      ConquestScreen = 24,      // the Conquest Trail screen is up: judge it, capture it, then Previous menu
      BackToWarFromConquest = 25, // the war screen is back: judge it, then the Siege row
      SiegeScreen = 26,         // the siege selection is up: judge it, walk its castle list, capture it, then Previous Menu
      BackToWarFromSiege = 27,  // the war screen is back a third time: judge it, then its own Custom row
      MapRequestScreen = 28,    // the map request screen is up: judge it, walk its map list and pages, capture it, then its way back
      BackToWarFromCustom = 29, // the war screen is back a fourth time: judge it, then its own Load row
      LoadScreen = 30,          // the load screen is up: judge it (empty list and all), walk its sorts and rows, capture it, then Cancel
      BackToWarFromLoad = 31,   // the war screen is back a fifth time: judge it, then its own Kingmaker row
      ScenarioLobby = 32,       // the scenario lobby is up: judge it (empty lobby and all), walk its four game types and its Start row, capture it, then its own Back row
      BackToWarFromLobby = 33,  // the war screen is back a sixth time: judge it, then its own Previous Menu
    }

    private const string PhaseKey = "sh2.S2MGameFlowRun.Phase";
    private const string StartKey = "sh2.S2MGameFlowRun.StartTicks";
    private const string DeadlineKey = "sh2.S2MGameFlowRun.DeadlineTicks";
    private const string ReportKey = "sh2.S2MGameFlowRun.Report";
    private const string AttemptsKey = "sh2.S2MGameFlowRun.PlaymodeAttempts";
    private const string LastAttemptKey = "sh2.S2MGameFlowRun.LastAttemptTime";

    private static readonly object ReportLock = new object();

    // Per-run state. A domain reload clears these, which is exactly what the
    // two runs want: each run's bookkeeping starts empty.
    private static string reportPath;
    private static bool bootChecked;
    private static bool introLogged;
    private static bool introBudgetChecked;
    private static bool menuChecked;
    private static bool menuSampledA;
    private static bool captureChecked;
    private static bool devBootChecked;
    private static bool devCounted;
    private static bool gameMusicSampled;
    private static string stateOrder = "";
    private static double reachedMenuAt;
    private static double introAt;
    private static double introPreparingSince = -1;
    private static float menuTimeA;
    private static float menuElapsedA;
    private static GameState lastState;
    private static bool menuSampledB;
    private static bool curtainChecked;

    // ------------------------------------------------------------------
    // the play screen (MAINMENU_PLAY's destination), through the round trip:
    // the Play click, the screen it opens, the three actions it reports as
    // unbuilt, and the Previous Menu click that comes back.
    // ------------------------------------------------------------------

    /// <summary>Where the play-screen capture is written (run 1).</summary>
    private const string PlayScreenCapturePath = "/tmp/sh2_game_flow_playscreen.png";

    /// <summary>The play screen's fallback capture, when the Game view will not render.</summary>
    private const string PlayScreenCanvasCapturePath = "/tmp/sh2_game_flow_playscreen_canvas.png";

    /// <summary>The menu's dispatch count when the six clicks began (Play is clicked later than the other five).</summary>
    private static int menuBatchStart;
    /// <summary>The menu actions dispatched during that batch, for the distinct-action count.</summary>
    private static readonly List<MainMenuAction> menuBatchActions = new();
    /// <summary>The frontend's play-list clock when Play was clicked (continuity across the round trip).</summary>
    private static float musicTotalElapsedBeforePlay = -1;
    private static string musicIdBeforePlay;
    private static bool playScreenChecked;
    private static bool playScreenCaptureChecked;
    private static bool menuBackChecked;

    // ------------------------------------------------------------------
    // run 1, part three: the two path screens behind the play screen (the war
    // screen and the peace screen), the campaign screen behind the war screen's
    // Campaign row, and the round trip through all of them.
    //
    // The trip the runner drives is the frontend's own tree, walked the way a
    // player walks it: MainMenuActions (Play) -> the play screen ->
    // PlayScreenActions (War) -> the war screen -> WarScreenActions (Campaign)
    // -> the campaign screen -> CampaignScreenActions (Previous Menu) -> the war
    // screen -> WarScreenActions (Previous menu) -> the play screen ->
    // PlayScreenActions (Peace) -> the peace screen -> PeaceScreenActions
    // (Previous menu) -> the play screen -> PlayScreenActions (Previous Menu) ->
    // the menu. Every click is the button's own pointer click, and every
    // screen's rows are judged against the install's own table.
    // ------------------------------------------------------------------

    /// <summary>Where the war screen's capture is written (run 1).</summary>
    private const string WarScreenCapturePath = "/tmp/sh2_game_flow_warscreen.png";

    /// <summary>The war screen's fallback capture, when the Game view will not render.</summary>
    private const string WarScreenCanvasCapturePath = "/tmp/sh2_game_flow_warscreen_canvas.png";

    /// <summary>Where the peace screen's capture is written (run 1).</summary>
    private const string PeaceScreenCapturePath = "/tmp/sh2_game_flow_peacescreen.png";

    /// <summary>The peace screen's fallback capture, when the Game view will not render.</summary>
    private const string PeaceScreenCanvasCapturePath = "/tmp/sh2_game_flow_peacescreen_canvas.png";

    /// <summary>Where the campaign screen's capture is written (run 1).</summary>
    private const string CampaignScreenCapturePath = "/tmp/sh2_game_flow_campaignscreen.png";

    /// <summary>The campaign screen's fallback capture, when the Game view will not render.</summary>
    private const string CampaignScreenCanvasCapturePath = "/tmp/sh2_game_flow_campaignscreen_canvas.png";

    /// <summary>Where the Conquest Trail screen's capture is written (run 1).</summary>
    private const string ConquestScreenCapturePath = "/tmp/sh2_game_flow_conquestscreen.png";

    /// <summary>The Conquest Trail screen's fallback capture, when the Game view will not render.</summary>
    private const string ConquestScreenCanvasCapturePath = "/tmp/sh2_game_flow_conquestscreen_canvas.png";

    /// <summary>Where the siege selection screen's capture is written (run 1).</summary>
    private const string SiegeScreenCapturePath = "/tmp/sh2_game_flow_siegescreen.png";

    /// <summary>The siege selection screen's fallback capture, when the Game view will not render.</summary>
    private const string SiegeScreenCanvasCapturePath = "/tmp/sh2_game_flow_siegescreen_canvas.png";

    /// <summary>Where the map request screen's capture is written (run 1).</summary>
    private const string MapRequestScreenCapturePath = "/tmp/sh2_game_flow_maprequestscreen.png";

    /// <summary>The map request screen's fallback capture, when the Game view will not render.</summary>
    private const string MapRequestScreenCanvasCapturePath = "/tmp/sh2_game_flow_maprequestscreen_canvas.png";

    /// <summary>Where the load screen's capture is written (run 1).</summary>
    private const string LoadScreenCapturePath = "/tmp/sh2_game_flow_loadscreen.png";

    /// <summary>The load screen's fallback capture, when the Game view will not render.</summary>
    private const string LoadScreenCanvasCapturePath = "/tmp/sh2_game_flow_loadscreen_canvas.png";

    /// <summary>Where the scenario lobby screen's capture is written (run 1).</summary>
    private const string ScenarioLobbyCapturePath = "/tmp/sh2_game_flow_scenariolobby.png";

    /// <summary>The scenario lobby's fallback capture, when the Game view will not render.</summary>
    private const string ScenarioLobbyCanvasCapturePath = "/tmp/sh2_game_flow_scenariolobby_canvas.png";

    private static bool warScreenChecked;
    private static bool warCaptureChecked;
    private static bool peaceScreenChecked;
    private static bool peaceCaptureChecked;
    private static bool playFromWarChecked;
    private static bool playFromPeaceChecked;
    private static bool campaignScreenChecked;
    private static bool campaignCaptureChecked;
    private static bool campaignSlotChecked;
    private static bool warFromCampaignChecked;
    private static bool conquestScreenChecked;
    private static bool conquestCaptureChecked;
    private static bool warFromConquestChecked;
    private static bool siegeScreenChecked;
    private static bool siegeCaptureChecked;
    private static bool warFromSiegeChecked;
    private static bool mapRequestScreenChecked;
    private static bool mapRequestCaptureChecked;
    private static bool warFromCustomChecked;
    private static bool loadScreenChecked;
    private static bool loadScreenCaptureChecked;
    private static bool warFromLoadChecked;
    private static bool scenarioLobbyChecked;
    private static bool scenarioLobbyCaptureChecked;
    private static bool warFromLobbyChecked;

    // ------------------------------------------------------------------
    // run 1, part four: the chapter start. After the round trip is judged and
    // the menu is back, the runner walks the frontend's first three screens once
    // more (Play -> War -> Campaign) and clicks ONE chapter row - the one click
    // that leaves the frontend for the game: CAMPAIGN_SCREEN -> MAP_LOADING ->
    // MAP_LOADED, the clicked chapter's own map built by the game's flow, and
    // the world judged against the port's manifests (the map's scenario counts,
    // its actors, its walls). The walk is a second visit, so the round trip's
    // own sequence check (made before it) is untouched.
    // ------------------------------------------------------------------

    /// <summary>The chapter the runner starts: index 3 = 'Chapter 4'. Not the dev harness's own map (chapter 8), so the load proves the CLICK chose the map.</summary>
    private const int ChapterStartIndex = 3;

    /// <summary>Where the loaded chapter's capture is written (run 1).</summary>
    private const string ChapterCapturePath = "/tmp/sh2_game_flow_chapter.png";

    /// <summary>The manifests the loaded chapter's world is judged against (the python tools' own output).</summary>
    private static string PortManifestsPath => Path.Combine(Constants.PortRoot, "manifests");

    private static string PortArtifactsPath => Path.Combine(Constants.PortRoot, "artifacts");

    /// <summary>The chapter-4 map file its row names, and the stem its per-map data files carry.</summary>
    private const string ChapterStartMapPath = "maps/war_chapter4.s2m";

    /// <summary>
    /// How long the chapter's map may take to load: the click enters MAP_LOADING
    /// on the button's own listener and the load runs on the next tick (its four
    /// renderers take a few seconds on war_chapter4), so this only bounds a hang.
    /// </summary>
    private const double ChapterMapBudgetSeconds = 90;

    /// <summary>Chapter 4's buildings JSON split (buildings_war_chapter4.json): 12 buildings + 6 props.</summary>
    private const int ChapterPropInstances = 6;

    /// <summary>Chapter 4's scenery count (buildings_war_chapter4.json's scenery list).</summary>
    private const int ChapterSceneryInstances = 134;

    private const string ChapterWalkStepKey = "sh2.S2MGameFlowRun.ChapterWalkStep";
    private static bool chapterMapChecked;
    private static bool chapterCaptureChecked;

    // ------------------------------------------------------------------
    // run 1, part five: the mission's briefing screen. The map the click loaded
    // is followed by the game's own mission-start screen (GameState.BRIEFING) -
    // the chapter's picture with its title on the picture's ribbon, its hints,
    // the shipped screen's three tabs and its 'To the Game' row. Judged here:
    // the screen and its content against the INSTALL (the picture by file name
    // and by pixels, the rows against i18n/en-us.xml read at run time), its tabs
    // and pages as a real input surface, and the one row that leaves it for the
    // mission - plus the gaps measured so far, said on the report.
    // ------------------------------------------------------------------

    /// <summary>Where the briefing screen's capture is written (run 1).</summary>
    private const string BriefingCapturePath = "/tmp/sh2_game_flow_briefing.png";

    /// <summary>
    /// How long the briefing screen may take to come up after the map stood: the
    /// flow enters BRIEFING on the tick after MAP_LOADED and builds the screen on
    /// the tick after that (its text table is read once), so this only bounds a
    /// hang.
    /// </summary>
    private const double BriefingBudgetSeconds = 30;

    /// <summary>Chapter 4's heading row and its hint family - what the screen must carry.</summary>
    private const string ChapterTitleRow = "WAR_CAMPAIGN_HEADING03";
    private const string ChapterHintFamily = "WARHINT03";

    private const string BriefingStepKey = "sh2.S2MGameFlowRun.BriefingStep";
    private static bool briefingChecked;
    private static bool briefingTabsChecked;
    private static bool briefingPagesChecked;
    private static bool briefingLeaveChecked;
    private static bool briefingCaptureChecked;
    private static int briefingHintAtFirstPage = -1;

    // ------------------------------------------------------------------
    // ------------------------------------------------------------------
    // run 1, part six: the mission's MAIN SCREEN (GameState.GAMEPLAY). 'To the
    // Game' hands the map to the game itself, and the game's own first surface
    // is the main screen: the install's own status bar (the strip across the
    // top 45 rows of ui/ingameui.tga) with the loaded mission's own decoded
    // numbers in its slots - Date, Gold, Popularity - and the Objectives row
    // (MAINSCREENROLLOVERS086), whose click opens the mission objectives as the
    // briefing screen's IN-GAME REVIEW ('Return to the Game', the shipped
    // screen's other play row). Judged here: the bar against the install's own
    // file by name AND by pixels, its slot layout against the strip's measured
    // spans, the three values against the manifests and the map itself, the
    // thirteen rows against i18n/en-us.xml read at run time, the one drawn
    // control as a real input surface - and the round trip GAMEPLAY -> BRIEFING
    // -> GAMEPLAY through Game's own asks, back on the map the briefing left.
    // ------------------------------------------------------------------

    /// <summary>Where the main screen's capture is written (run 1).</summary>
    private const string MainScreenCapturePath = "/tmp/sh2_game_flow_mainscreen.png";

    /// <summary>Chapter 4's own scenario start - war_chapter4.s2m stores 1084-00 (0-based month; the manual pins January as 00).</summary>
    private const int ChapterStartYear = 1084;
    private const int ChapterStartMonthIndex = 0;
    private const string ChapterStartMonthRow = "MONTH_JANUARY";

    // The install's own strip, measured 2026-10-13 (ui/ingameui.tga's top 45
    // rows): the bar is 1024x45 and its painted slots run, in x px of 1024 -
    // Date 11..137, Peasants 144..405, the stone Rank plaque 407..610, Gold
    // 611..708, Honour 711..823, Popularity 824..937, and a seventh slot
    // 938..1013 the rollover table does not name. These are the runner's OWN
    // copy of the measurement, so the screen's layout is checked against the
    // numbers the strip was measured with, not against the screen's constants.
    private const int StripWidthPx = 1024;
    private const int StripHeightPx = 45;
    private const int DateSlotLeftPx = 11, DateSlotRightPx = 137;
    private const int GoldSlotLeftPx = 611, GoldSlotRightPx = 708;
    private const int PopularitySlotLeftPx = 824, PopularitySlotRightPx = 937;

    private const string MainScreenObjectivesCountKey = "sh2.S2MGameFlowRun.MainScreenObjectivesCount";
    private const string BriefingReturnCountKey = "sh2.S2MGameFlowRun.BriefingReturnCount";
    private static bool mainScreenChecked;
    private static bool mainScreenCaptureChecked;
    private static bool objectivesReviewChecked;
    private static bool backFromReviewChecked;

    /// <summary>
    /// The frontend states the runner has observed, in order and one entry per
    /// change - the round trip's own record. It is read at the end of the trip
    /// (TickMenuBack), so a click that opened the wrong screen, or a way back
    /// that went to the menu instead of the play screen, shows up as a sequence
    /// that is not the frontend's tree.
    /// </summary>
    private static readonly List<GameState> frontendTrip = new();

    // ------------------------------------------------------------------
    // the curtain hand-off, as the PLAYERS report it. Every field is filled
    // by polling the two curtain VideoPlayers' own url/frame/time/isLooping
    // once per tick from the frame the menu appears - never by a clock the
    // runner keeps - so what the checks compare is the players' behaviour.
    // ------------------------------------------------------------------

    /// <summary>The cached clip files the two curtain players must be running.</summary>
    private static readonly string CurtainOpenFile =
      Path.GetFileName(VideoFilePaths.GetCachedVideoPath(VideoFilePaths.MainMenuCurtainOpen));
    private static readonly string CurtainIdleFile =
      Path.GetFileName(VideoFilePaths.GetCachedVideoPath(VideoFilePaths.MainMenuCurtainIdle));

    private static double curtainLastLogAt = -99;
    private static bool curtainSawOpen;
    private static bool curtainOpenAdvanced;
    private static bool curtainOpenGone;
    private static long curtainOpenFirstFrame = -1;
    private static long curtainOpenLastFrame = -1;
    private static long curtainOpenFrameCount = -1;
    private static bool curtainIdleSeen;
    private static bool curtainIdleAdvanced;
    private static bool curtainIdleWrapped;
    private static bool curtainIdleIsLooping;
    private static long curtainIdleFirstFrame = -1;
    private static long curtainIdleMaxFrame = -1;
    private static long curtainIdleLastFrame = -1;
    private static long curtainIdleFrameCount = -1;
    private static double curtainIdleFirstTime = -1;
    private static double curtainIdleMaxTime = -1;
    private static double curtainIdleLastTime = -1;
    private static long curtainWrapFromFrame = -1;
    private static long curtainWrapToFrame = -1;
    private static double curtainWrapFromTime = -1;
    private static double curtainWrapToTime = -1;

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
      // Entering Play mode reloads the domain and wipes the delegate the run
      // was driven by. Re-arm it on every reload while a run is in flight.
      if ((Phase)SessionState.GetInt(PhaseKey, 0) == Phase.Idle) return;
      reportPath = SessionState.GetString(ReportKey, DefaultReportPath);
      Application.logMessageReceived -= OnLog;
      Application.logMessageReceived += OnLog;
      EditorApplication.update -= Tick;
      EditorApplication.update += Tick;
    }

    public static void Main()
    {
      reportPath = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(reportPath)) reportPath = DefaultReportPath;
      SessionState.SetString(ReportKey, reportPath);
      File.WriteAllText(reportPath, "");

      Line($"unity        : {Application.unityVersion}, {Application.platform}, "
           + $"batchmode={Application.isBatchMode}, screen={Screen.width}x{Screen.height}");
      Line($"report       : {reportPath}");
      Line($"capture      : {MenuCapturePath} (fallback {MenuCanvasCapturePath} "
           + "if the Game view is not rendering)");
      Line($"capture      : {PlayScreenCapturePath} (fallback {PlayScreenCanvasCapturePath})");
      Line($"capture      : {WarScreenCapturePath} (fallback {WarScreenCanvasCapturePath})");
      Line($"capture      : {PeaceScreenCapturePath} (fallback {PeaceScreenCanvasCapturePath})");
      Line($"capture      : {CampaignScreenCapturePath} (fallback {CampaignScreenCanvasCapturePath})");
      Line($"capture      : {ConquestScreenCapturePath} (fallback {ConquestScreenCanvasCapturePath})");
      Line($"capture      : {SiegeScreenCapturePath} (fallback {SiegeScreenCanvasCapturePath})");
      Line($"capture      : {MapRequestScreenCapturePath} (fallback {MapRequestScreenCanvasCapturePath})");
      Line($"capture      : {LoadScreenCapturePath} (fallback {LoadScreenCanvasCapturePath})");

      // The run sets up its own preconditions: the scene the game ships in,
      // the dev harness's map override out of the way (so war_chapter8 and its
      // known counts are what run 2 loads), and the switch on the game flow.
      if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
      {
        Line($"scene        : opening {ScenePath}");
        EditorSceneManager.OpenScene(ScenePath);
      }
      if (File.Exists(TestMapOverridePath))
      {
        MoveFile(TestMapOverridePath, TestMapOverridePath + ".sh2flow-backup");
        Line($"map override : {TestMapOverridePath} moved aside for this run "
             + "(restored when it finishes)");
      }
      // The intro switch's own override, the same deal: out of the way, so the
      // serialized field decides and the run measures what the SCENE is set to.
      if (File.Exists(IntroBudgetOverridePath))
      {
        MoveFile(IntroBudgetOverridePath, IntroBudgetOverridePath + ".sh2flow-backup");
        Line($"intro switch : {IntroBudgetOverridePath} moved aside for this run "
             + "(restored when it finishes)");
      }
      WriteSwitch("game");

      Line("--- phase 1: the game's own flow (switch = game)");
      BeginPhase(Phase.GameFlow, GameFlowBudgetSeconds);
      Application.logMessageReceived += OnLog;
      EditorApplication.update += Tick;
      SessionState.SetInt(AttemptsKey, 1);
      SessionState.SetFloat(LastAttemptKey, (float)EditorApplication.timeSinceStartup);
      EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
      try
      {
        TickCore();
      }
      catch (Exception error)
      {
        Fail($"the runner threw: {error.GetType().Name}: {error.Message}");
        Finish();
      }
    }

    private static void TickCore()
    {
      switch ((Phase)SessionState.GetInt(PhaseKey, 0))
      {
        case Phase.GameFlow: TickGameFlow(); break;
        case Phase.Menu: TickMenu(); break;
        case Phase.Capture: TickCapture(); break;
        case Phase.PlayScreen: TickPlayScreen(); break;
        case Phase.PlayScreenCapture: TickPlayScreenCapture(); break;
        case Phase.WarScreen: TickWarScreen(); break;
        case Phase.BackToPlayFromWar: TickBackToPlayFromWar(); break;
        case Phase.PeaceScreen: TickPeaceScreen(); break;
        case Phase.BackToPlayFromPeace: TickBackToPlayFromPeace(); break;
        case Phase.CampaignScreen: TickCampaignScreen(); break;
        case Phase.BackToWarFromCampaign: TickBackToWarFromCampaign(); break;
        case Phase.ConquestScreen: TickConquestScreen(); break;
        case Phase.BackToWarFromConquest: TickBackToWarFromConquest(); break;
        case Phase.SiegeScreen: TickSiegeScreen(); break;
        case Phase.BackToWarFromSiege: TickBackToWarFromSiege(); break;
        case Phase.MapRequestScreen: TickMapRequestScreen(); break;
        case Phase.BackToWarFromCustom: TickBackToWarFromCustom(); break;
        case Phase.LoadScreen: TickLoadScreen(); break;
        case Phase.BackToWarFromLoad: TickBackToWarFromLoad(); break;
        case Phase.ScenarioLobby: TickScenarioLobby(); break;
        case Phase.BackToWarFromLobby: TickBackToWarFromLobby(); break;
        case Phase.MenuBack: TickMenuBack(); break;
        case Phase.ChapterWalk: TickChapterWalk(); break;
        case Phase.ChapterMap: TickChapterMap(); break;
        case Phase.Briefing: TickBriefing(); break;
        case Phase.AfterBriefing: TickAfterBriefing(); break;
        case Phase.MainScreen: TickMainScreen(); break;
        case Phase.MainScreenCapture: TickMainScreenCapture(); break;
        case Phase.ObjectivesReview: TickObjectivesReview(); break;
        case Phase.BackFromReview: TickBackFromReview(); break;
        case Phase.ExitPlaymode: TickExitPlaymode(); break;
        case Phase.DevHarness: TickDevHarness(); break;
        case Phase.Finish: Finish(); break;
        default: EditorApplication.update -= Tick; break;
      }
    }

    // ------------------------------------------------------------------
    // run 1: the game's own flow
    // ------------------------------------------------------------------

    private static void TickGameFlow()
    {
      if (Expired(GameFlowBudgetSeconds, "the flow did not reach MAIN_MENU inside its budget"))
        return;
      if (WaitForOrRetryPlaymode("the game flow")) return;

      Main main = UnityEngine.Object.FindAnyObjectByType<Main>();
      if (main == null) return;  // the scene is still coming up

      if (!bootChecked)
      {
        bootChecked = true;
        Check(main.GameFlowActive && main.GameFlowSource == FlowOverridePath + " = game",
              $"the switch booted the game's own flow ({main.GameFlowSource})");
        Check(GameObject.Find("S2M Map World") == null,
              "and the dev-harness map render did not run in this boot");

        // The intro videos' own switch: the port plays each clip for its budget
        // (introVideoSeconds, from the scene - the override file is moved aside
        // for the run) and then skips it itself, so this boot does not wait the
        // shipped length out. Both settings that turn the intro down are named
        // here, and the disabled one (0) is the console's own line if the scene
        // sets it: LOADING_ASSETS -> MAIN_MENU with no INTRO_VIDEOS at all.
        Check(main.IntroBudgetSeconds > 0f && main.IntroBudgetSeconds < IntroAutoSkipSeconds
              && main.IntroBudgetSource.Contains("Main.introVideoSeconds"),
              $"the intro clips play the port's own short budget "
              + $"({main.IntroBudgetSeconds:F1} s per clip, switch: {main.IntroBudgetSource})");
      }

      Game game = Game.GetInstance();
      if (game == null) return;  // Main.Start has not run yet

      if (game.State != lastState)
      {
        stateOrder += (stateOrder.Length == 0 ? "" : " -> ") + game.State;
        Line($"observed     : the state is {game.State} at +{Elapsed():F1} s");
        lastState = game.State;
      }

      // The first play list, sampled while it is still the current one.
      if (game.State == GameState.LOADING_ASSETS && !gameMusicSampled)
      {
        S2MMusicPlayer music = main.MusicPlayer;
        if (music.Current != null)
        {
          gameMusicSampled = true;
          AudioSource source = music.Source;
          Check(music.Current.Id == LoadingPlaylist,
                $"PL_LOADING starts while the assets convert (playing '{music.Current.Id}')");
          Check(source.clip != null && source.clip.name == LoadingFirstTrack,
                $"and its first track is on the source ('{source.clip?.name}')");
          Check(Mathf.Abs(source.volume - LoadingGain) < 0.001f,
                $"at the volume list's own gain ({source.volume:F3} = 110/127)");
          Line($"music        : {music.Status}");
        }
      }

      // The intro videos. The claim that they play "when the bypass is removed"
      // (recorded 2026-10-08) could not have been true on this machine: the
      // install path and the cache format both missed. These
      // are VideoPlayerUI's own console lines, and they are what says it plays.
      //
      // They are also 130 s of 1080p VP8, which decodes slower than realtime
      // here, so the PORT's own budget skips each clip once it has started and
      // played its few seconds (Main.TickIntroBudget, through the very method a
      // left click calls) - the runner's own window below is only the backstop
      // for a clip that never starts at all. But only a video that has STARTED
      // is skipped, by either of them: see SkipStartedIntroVideo for what
      // stopping one mid-prepare costs.
      if (game.State == GameState.INTRO_VIDEOS)
      {
        if (!introLogged)
        {
          introLogged = true;
          introAt = Elapsed();
          Line($"intro        : entered at +{introAt:F1} s");
        }
        else if (Elapsed() - introAt > IntroAutoSkipSeconds)
        {
          SkipStartedIntroVideo(game);
        }
      }

      // The intro budget's own evidence, read once the flow has moved on: the
      // port skipped the clips (its own count and its own console lines) and the
      // runner's 12 s backstop was NOT what did it (the state was left long
      // before it could fire).
      if (game.State == GameState.MAIN_MENU && !introBudgetChecked && introLogged)
      {
        introBudgetChecked = true;
        double dwell = reachedMenuAt > 0 ? reachedMenuAt - introAt : Elapsed() - introAt;
        Check(main.IntroSkipCount >= 1
              && CountReportLines("skipping it through the same call a click makes") >= 1,
              $"the port's own intro budget skipped the clips it played "
              + $"({main.IntroSkipCount} skip(s), {CountReportLines("has started; it plays")} "
              + "clip(s) seen to start)");
        Check(dwell < IntroAutoSkipSeconds,
              $"and it did it well inside the runner's own backstop (the intro state was left "
              + $"after {dwell:F1} s of its {IntroAutoSkipSeconds:F0} s window - the runner's "
              + "auto-skip never had to fire)");
      }

      if (game.State == GameState.MAIN_MENU && !menuChecked)
      {
        menuChecked = true;
        reachedMenuAt = Elapsed();
        Line($"menu         : reached at +{reachedMenuAt:F1} s "
             + $"({CountReportLines("Video prepared.")} video(s) prepared, "
             + $"{CountReportLines("Video playback started.")} started)");

        // NONE is left in the flow's very FIRST frame - Main.Update runs before
        // the runner's first tick - so the runner's own list typically starts at
        // LOADING_ASSETS. Assert the order it did see is an ordered subsequence
        // of the scaffolded one and ends at the menu; the console lines below
        // are what carry the transitions themselves.
        string[] scaffolded = { "NONE", "LOADING_ASSETS", "INTRO_VIDEOS", "MAIN_MENU", "PLAY_SCREEN" };
        bool inOrder = true;
        int cursor = 0;
        foreach (string state in stateOrder.Split(new[] { " -> " }, StringSplitOptions.None))
        {
          int found = Array.IndexOf(scaffolded, state, cursor);
          if (found < 0) { inOrder = false; break; }
          cursor = found + 1;
        }
        Check(inOrder && stateOrder.EndsWith("MAIN_MENU"),
              $"the states the runner saw are the scaffolded ones, in order ({stateOrder})");

        // The criterion's own evidence, from the console: the three transition
        // lines, all of them the game's own Debug.Log.
        Check(ReportHas("Game flow: NONE -> LOADING_ASSETS."),
              "the console logged NONE -> LOADING_ASSETS");
        Check(ReportHas("Game flow: LOADING_ASSETS -> INTRO_VIDEOS."),
              "the console logged LOADING_ASSETS -> INTRO_VIDEOS");
        Check(ReportHas("Game flow: INTRO_VIDEOS -> MAIN_MENU."),
              "the console logged INTRO_VIDEOS -> MAIN_MENU");
        Check(ReportHas($"Music: {LoadingPlaylist}[1/"),
              $"the console logged the PL_LOADING cue ({LoadingPlaylist})");
        Check(CountReportLines("Video playback started.") >= 1,
              $"the intro videos decoded and started "
              + $"({CountReportLines("Video playback started.")} 'Video playback started.' line(s))");

        // The menu itself - its UI, its own music cue and the first sample of
        // its AudioSource - is one frame away from here: Main.Update shows the
        // menu and calls Play on the frame AFTER the state flips, and the
        // runner's tick lands in between. So the Menu phase does all of that.
        BeginPhase(Phase.Menu, MenuObservationSeconds);
      }
    }

    private static void TickMenu()
    {
      // The curtain is observed on EVERY tick of the menu phase, including the
      // ones the two music windows still have to wait out: the hand-off is the
      // players' own doing and can happen at any moment.
      ObserveCurtain();

      if (Elapsed() < MenuObservationSeconds) return;

      Main main = UnityEngine.Object.FindAnyObjectByType<Main>();
      S2MMusicPlayer music = main.MusicPlayer;
      AudioSource source = music.Source;

      if (!menuSampledA)
      {
        // Step A: the menu has been up for a few seconds now, so it exists,
        // its cue has been logged, and its own play list is the current one.
        menuSampledA = true;

        if (!gameMusicSampled)
        {
          Line("music        : PL_LOADING's own sample was not taken - its state was already "
               + "past when the runner's first tick landed (a warm cache); the console cue "
               + "above is the record of it");
        }

        Check(ReportHas($"Music: {MenuPlaylist}[1/"),
              $"the console logged the PL_MAINMENU cue ({MenuPlaylist})");
        CheckMenuContent();
        CheckMenuButtons();
        CheckMenuStringsAgainstInstall();
        Check(music.Current != null && music.Current.Id == MenuPlaylist,
              $"the menu's play list is the current one ('{music.Current?.Id}')");
        Check(source.clip != null && source.clip.name == MenuFirstTrack,
              $"PL_MAINMENU's first track is on the menu's AudioSource "
              + $"('{source.clip?.name}' = {MenuFirstTrack})");
        Check(Mathf.Abs(source.volume - MenuGain) < 0.001f,
              $"at the volume list's own gain ({source.volume:F3} = 115/127)");
        Check(source.isPlaying, "the track is playing (not a stalled source)");

        menuTimeA = source.time;
        menuElapsedA = music.ItemElapsed;
        Line($"music A      : clip='{source.clip?.name}' time={menuTimeA:F3} s "
             + $"volume={source.volume:F3} isPlaying={source.isPlaying}");
        BeginPhase(Phase.Menu, MenuObservationSeconds);
        return;
      }

      if (!menuSampledB)
      {
        // Step B: the same source, a few seconds later. This is the measurement
        // batchmode cannot make: AudioSource.time advances only with a player
        // loop, and it is the source's own number, not the runner's clock.
        menuSampledB = true;
        Check(source.time > menuTimeA + 1.0f,
              $"AudioSource.time ADVANCES in real time ({menuTimeA:F3} s -> {source.time:F3} s) "
              + "- the number that stays 0.0000 under batchmode");
        Check(music.ItemElapsed > menuElapsedA + 1.0f,
              $"and the player's own clock agrees ({menuElapsedA:F3} s -> {music.ItemElapsed:F3} s, "
              + $"play list {music.TotalElapsed:F1} s)");
        Line($"music B      : {music.Status}");

        // Step C is the curtain: the open clip is 11.3 s at 30 fps, so the
        // hand-off lands well after the two music windows, and the idle loop
        // needs another 8 s to wrap. The phase's third window waits it out -
        // the capture below happens only once the menu has SETTLED, so the
        // capture is of the menu a player actually sits in front of.
        BeginPhase(Phase.Menu, CurtainBudgetSeconds);
        return;
      }

      // Step C: the hand-off must have completed. ObserveCurtain has been
      // recording the players since the menu appeared; this only waits for the
      // idle clip to wrap (the last thing there is to see) and then judges.
      if (!curtainIdleWrapped)
      {
        if (Elapsed() > CurtainBudgetSeconds)
        {
          CheckCurtainHandoff();
          Fail($"the curtain never finished its hand-off inside "
               + $"{CurtainBudgetSeconds:F0} s (open clip seen={curtainSawOpen}, "
               + $"gone={curtainOpenGone}; idle seen={curtainIdleSeen}, "
               + $"wrapped={curtainIdleWrapped})");
          Finish();
        }
        return;
      }

      CheckCurtainHandoff();

      FocusGameView();
      ScreenCapture.CaptureScreenshot(MenuCapturePath);
      Line($"capture      : asked for the Game view render -> {MenuCapturePath}");
      BeginPhase(Phase.Capture, CaptureWaitSeconds);
    }

    private static void TickCapture()
    {
      if (captureChecked || Elapsed() < CaptureWaitSeconds) return;
      captureChecked = true;

      int screenColours = CheckCapture(MenuCapturePath, "the Game view capture");
      if (screenColours < 8)
      {
        // An unfocused Game view can stop rendering. The menu is a UI canvas,
        // so it needs no window to be drawn: render it through a temporary
        // camera and judge that instead - the report says which one it judged.
        Line("capture      : the Game view render looks blank; drawing the menu canvas "
             + "through a camera instead");
        int canvasColours = CaptureFrontendCanvas("MainMenuUI", MenuCanvasCapturePath,
                                                  "the menu canvas capture");
        Check(canvasColours >= 8,
              $"the menu's own canvas renders non-blank ({canvasColours} distinct colours, "
              + $"{MenuCanvasCapturePath})");
      }

      // The menu's settled, the capture is judged - NOW the first destination is
      // driven: the Play button's own click, the same path the other five took.
      // It is deliberately last of the six: the click MOVES the flow off the
      // menu (the play screen replaces it), so the curtain hand-off, the music
      // samples and the capture above all had to be done with the menu up.
      ClickMenuPlayButton();

      // The frontend's cue, as the play list measures it, the moment before the
      // round trip: S2MMusicPlayer.Play always restarts at the list's first
      // item, so this number not going backwards across the trip is what proves
      // the return did not restart it.
      Main main = UnityEngine.Object.FindAnyObjectByType<Main>();
      musicTotalElapsedBeforePlay = main != null ? main.MusicPlayer.TotalElapsed : -1f;
      musicIdBeforePlay = main != null && main.MusicPlayer.Current != null
        ? main.MusicPlayer.Current.Id : null;

      BeginPhase(Phase.PlayScreen, PlayScreenBudgetSeconds);
    }

    // ------------------------------------------------------------------
    // run 1, part two: the play screen, and the way back
    // ------------------------------------------------------------------

    /// <summary>
    /// The runner's intro skip, but only for a video that has STARTED.
    /// VideoManager.SkipCurrentIntroVideo destroys the player it stops, and a
    /// VideoPlayer destroyed while it is still PREPARING leaves Unity's video
    /// module wedged: the NEXT players' Play() calls then do not start for ~85 s
    /// (measured 2026-10-12 - the run that killed the second intro mid-prepare
    /// sat with the menu's two players at frame 0 from t=37 s to t=123 s, and
    /// the curtain budget expired under it, while all four earlier runs, whose
    /// second intro had started before the skip, began the menu's players
    /// immediately). The runner's own record says which is which: "Trying?" is
    /// logged once per PlayFullscreenBinkVideo call and "Video playback started."
    /// once per player that really starts, so the skip waits for the two to
    /// agree. The grace below only bounds a video that never starts at all.
    /// </summary>
    private static void SkipStartedIntroVideo(Game game)
    {
      int played = CountReportLines("Trying?");
      int started = CountReportLines("Video playback started.");
      if (started < played)
      {
        if (introPreparingSince < 0)
        {
          introPreparingSince = Elapsed();
          Line($"intro        : the current video is still preparing ({started}/{played} started); "
               + "not skipping it mid-prepare (that is what wedges the video module)");
        }
        else if (Elapsed() - introPreparingSince > IntroPrepareGraceSeconds)
        {
          Line($"intro        : the current video has been preparing for "
               + $"{Elapsed() - introPreparingSince:F0} s; skipping it anyway");
          game.videoManager.SkipCurrentIntroVideo();
        }
        return;
      }

      introPreparingSince = -1;
      game.videoManager.SkipCurrentIntroVideo();
    }

    /// <summary>
    /// The Play button's click, and the dispatch record read after it - the
    /// sixth of the menu's six clicks, made through the button's own pointer
    /// click like the other five (see CheckMenuButtons, which makes those and
    /// deliberately leaves this one to here).
    /// </summary>
    private static void ClickMenuPlayButton()
    {
      MainMenuActionInfo info = MainMenuActions.Info(MainMenuAction.Play);
      Transform column = FindMenuColumn();
      Transform child = column != null ? column.Find(info.Label + "Button") : null;
      UnityEngine.UI.Button button = child != null ? child.GetComponent<UnityEngine.UI.Button>() : null;
      if (button == null)
      {
        Fail($"the {info.Label} button ({info.Label}Button) is not in the menu, so its click "
             + "could not be made");
        return;
      }

      int before = MainMenuActions.DispatchCount;
      PressButton(button);
      MainMenuActionInfo dispatched = MainMenuActions.LastDispatch;
      bool ok = MainMenuActions.DispatchCount == before + 1
                && MainMenuActions.HasDispatch
                && dispatched.Action == info.Action
                && dispatched.Id == info.Id;
      if (ok) menuBatchActions.Add(dispatched.Action);

      Check(ok,
            $"the {info.Label} button's click dispatched {info.Id} '{info.Label}' "
            + $"through the one entry point (MainMenuActions.Dispatch) - "
            + $"{MainMenuActions.Outcome(info)}");
    }

    private static void TickPlayScreen()
    {
      if (Expired(PlayScreenBudgetSeconds,
                  "the play screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      GameObject screen = GameObject.Find("PlayScreenUI");
      // The click only ASKS for the screen (Game.RequestFrontendScreen); the
      // flow shows it on its own next Update - the same one-frame hand-over the
      // menu has, so the runner waits for the screen rather than judging the
      // click's own tick.
      if (game.State != GameState.PLAY_SCREEN || screen == null) return;

      NoteFrontendState(game.State);
      if (playScreenChecked) return;
      playScreenChecked = true;

      Check(ReportHas("Game flow: MAIN_MENU -> PLAY_SCREEN."),
            $"the Play click moved the flow to the play screen "
            + $"(the console logged MAIN_MENU -> PLAY_SCREEN, state={game.State})");
      CheckPlayScreenContent();
      CheckPlayScreenStringsAgainstInstall();
      CheckPlayScreenButtons();
      CheckThePlayScreensPathRows();
      CheckTheSixMenuClicks();

      FocusGameView();
      ScreenCapture.CaptureScreenshot(PlayScreenCapturePath);
      Line($"capture      : asked for the Game view render -> {PlayScreenCapturePath}");
      BeginPhase(Phase.PlayScreenCapture, CaptureWaitSeconds);
    }

    private static void TickPlayScreenCapture()
    {
      if (playScreenCaptureChecked || Elapsed() < CaptureWaitSeconds) return;
      playScreenCaptureChecked = true;

      int colours = CheckCapture(PlayScreenCapturePath, "the play screen's Game view capture");
      if (colours < 8)
      {
        // The same fallback the menu's capture uses: an unfocused Game view can
        // stop rendering, and a UI canvas needs no window.
        Line("capture      : the Game view render looks blank; drawing the play screen's "
             + "canvas through a camera instead");
        int canvasColours = CaptureFrontendCanvas("PlayScreenUI", PlayScreenCanvasCapturePath,
                                                  "the play screen canvas capture");
        Check(canvasColours >= 8,
              $"the play screen's own canvas renders non-blank ({canvasColours} distinct "
              + $"colours, {PlayScreenCanvasCapturePath})");
      }
      else
      {
        Check(true,
              $"the play screen renders its own art ({colours} distinct colours - "
              + "the shipped background and its picture buttons)");
      }

      // And the way into the first path screen, through its own button, like
      // every other click here. The play screen's own way back is NOT clicked
      // now: the frontend is a tree - the play screen's "Previous Menu" is the
      // last click of the round trip (TickBackToPlayFromPeace), after both path
      // screens have been walked and the play screen has been reached again.
      ClickPlayScreenRow(PlayScreenAction.War, Phase.WarScreen);
    }

    /// <summary>
    /// One of the play screen's own rows, clicked through its button's own
    /// pointer click - the same call the input module makes on release - with
    /// the dispatch record read after it: the count must have advanced by
    /// exactly one and the row must be that button's own action.
    /// </summary>
    private static void ClickPlayScreenRow(PlayScreenAction action, Phase phase)
    {
      PlayScreenActionInfo info = PlayScreenActions.Info(action);
      GameObject buttonObject = GameObject.Find(action + "Button");
      UnityEngine.UI.Button button = buttonObject != null
        ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null;
      if (button == null)
      {
        Fail($"the {info.Label} button ({action}Button) is not on the play screen, "
             + "so its click could not be made");
        BeginPhase(phase, PlayScreenBudgetSeconds);
        return;
      }

      int before = PlayScreenActions.DispatchCount;
      PressButton(button);
      PlayScreenActionInfo dispatched = PlayScreenActions.LastDispatch;
      Check(PlayScreenActions.DispatchCount == before + 1
            && PlayScreenActions.HasDispatch
            && dispatched.Action == info.Action
            && dispatched.Id == info.Id,
            $"the {info.Label} button's click dispatched {info.Id} '{info.Label}' through "
            + $"the play screen's one entry point (PlayScreenActions.Dispatch) - "
            + $"{PlayScreenActions.Outcome(info)}");

      BeginPhase(phase, PlayScreenBudgetSeconds);
    }

    // ------------------------------------------------------------------
    // the two path screens behind the play screen, and the round trip
    // ------------------------------------------------------------------

    /// <summary>
    /// One entry per state CHANGE, in the order the runner observed them - the
    /// frontend's own transitions, one per click: the click happens inside the
    /// runner's own tick (Game.RequestFrontendScreen sets the state from the
    /// button's listener), so no state can pass between two ticks unseen, and a
    /// way back that went somewhere other than the play screen cannot hide here.
    /// </summary>
    private static void NoteFrontendState(GameState state)
    {
      if (frontendTrip.Count == 0 || frontendTrip[frontendTrip.Count - 1] != state)
      {
        frontendTrip.Add(state);
      }
    }

    /// <summary>
    /// The war screen, once the play screen's War click has opened it: its own
    /// content (its own canvas above the play screen's, the backdrop the two path
    /// screens share, its own aspect-ratio bars, its six pictures with the
    /// shipped labels), its own rows against the install's table, and its four
    /// destination-less rows clicked through its own one entry point. Then the
    /// capture, and then the campaign row - the one row of the six that has a
    /// screen behind it (GameState.CAMPAIGN_SCREEN) - which walks the trip one
    /// branch deeper. Its own Previous menu, the click that goes back to the PLAY
    /// SCREEN, is made after the campaign visit (TickBackToWarFromCampaign).
    /// </summary>
    private static void TickWarScreen()
    {
      if (Expired(PlayScreenBudgetSeconds, "the war screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      // The click only ASKS for the screen; the flow shows it on its own next
      // Update - the same one-frame hand-over every screen here has.
      if (game.State != GameState.WAR_SCREEN || GameObject.Find("WarScreenUI") == null) return;
      NoteFrontendState(game.State);

      if (!warScreenChecked)
      {
        warScreenChecked = true;

        Check(ReportHas("Game flow: PLAY_SCREEN -> WAR_SCREEN."),
              $"the War click moved the flow to the war screen "
              + $"(the console logged PLAY_SCREEN -> WAR_SCREEN, state={game.State})");
        Check(GameObject.Find("PlayScreenUI") == null,
              "and the play screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckPathScreenContent("war", "WarScreenUI", WarScreenActions.Pictures);
        CheckPathScreenBackButton("war", WarScreenActions.Back);
        CheckPathScreenRowsAgainstInstall("war", "WARSCREEN", WarScreenActions.All);
        CheckPathScreenButtons("war", "WarScreenActions", WarScreenActions.All,
                               () => WarScreenActions.DispatchCount,
                               () => WarScreenActions.LastDispatch);

        FocusGameView();
        ScreenCapture.CaptureScreenshot(WarScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {WarScreenCapturePath}");
        return;
      }

      if (!warCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        warCaptureChecked = true;
        CheckPathScreenCapture(WarScreenCapturePath, WarScreenCanvasCapturePath,
                               "WarScreenUI", "war");
      }

      // The war screen's two built rows are its two branches: the Campaign row's
      // click opens the campaign screen, and - after that branch has been walked
      // and come back here (TickBackToWarFromCampaign) - the Conquest Trail row's
      // click opens the trail. The screen's own way back is the last click of the
      // visit, after both branches.
      CheckTheWarScreensBuiltRows();
      PathScreenRow campaignRow = WarScreenActions.Picture(0);
      ClickDepartingRow("war", "WarScreenActions", campaignRow.ButtonName, campaignRow.Id,
                        campaignRow.Label, campaignRow.Outcome, "Campaign",
                        () => WarScreenActions.DispatchCount,
                        () => WarScreenActions.LastDispatch.Id, Phase.CampaignScreen);
    }

    /// <summary>
    /// The play screen, back from the war screen: the war screen's own Previous
    /// menu must have brought the flow here - not to the menu, which only the
    /// play screen's own way back reaches - the play screen must be rebuilt with
    /// its content and a fresh dispatch record, and then the second path's own
    /// button is clicked, the same click the first path took.
    /// </summary>
    private static void TickBackToPlayFromWar()
    {
      if (playFromWarChecked) return;

      Game game = Game.GetInstance();
      GameObject play = GameObject.Find("PlayScreenUI");
      if (game == null || game.State != GameState.PLAY_SCREEN || play == null
          || GameObject.Find("WarScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the play screen never came back after the war screen's Previous menu");
        return;
      }

      NoteFrontendState(game.State);
      playFromWarChecked = true;

      Check(ReportHas("Game flow: WAR_SCREEN -> PLAY_SCREEN."),
            "the war screen's Previous menu brought the flow back to the PLAY SCREEN, not to the "
            + "menu (the console logged WAR_SCREEN -> PLAY_SCREEN, the WarScreenUI root is "
            + "destroyed, and the play screen's root is back)");
      CheckPlayScreenCameBack("war");

      ClickPlayScreenRow(PlayScreenAction.Peace, Phase.PeaceScreen);
    }

    /// <summary>
    /// The peace screen, once the play screen's Peace click has opened it - the
    /// same walk the war screen got: its own content, its own rows against the
    /// install, its three destination-less rows clicked through its own one entry
    /// point, a capture, and then its own Previous menu, back to the play screen.
    /// </summary>
    private static void TickPeaceScreen()
    {
      if (Expired(PlayScreenBudgetSeconds, "the peace screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      if (game.State != GameState.PEACE_SCREEN || GameObject.Find("PeaceScreenUI") == null) return;
      NoteFrontendState(game.State);

      if (!peaceScreenChecked)
      {
        peaceScreenChecked = true;

        Check(ReportHas("Game flow: PLAY_SCREEN -> PEACE_SCREEN."),
              $"the Peace click moved the flow to the peace screen "
              + $"(the console logged PLAY_SCREEN -> PEACE_SCREEN, state={game.State})");
        Check(GameObject.Find("PlayScreenUI") == null,
              "and the play screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckPathScreenContent("peace", "PeaceScreenUI", PeaceScreenActions.Pictures);
        CheckPathScreenBackButton("peace", PeaceScreenActions.Back);
        CheckPathScreenRowsAgainstInstall("peace", "PEACESCREEN / PEACECREEN", PeaceScreenActions.All);
        CheckPathScreenButtons("peace", "PeaceScreenActions", PeaceScreenActions.All,
                               () => PeaceScreenActions.DispatchCount,
                               () => PeaceScreenActions.LastDispatch);

        FocusGameView();
        ScreenCapture.CaptureScreenshot(PeaceScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {PeaceScreenCapturePath}");
        return;
      }

      if (!peaceCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        peaceCaptureChecked = true;
        CheckPathScreenCapture(PeaceScreenCapturePath, PeaceScreenCanvasCapturePath,
                               "PeaceScreenUI", "peace");
      }

      ClickPathScreenBack("peace", "PeaceScreenActions", PeaceScreenActions.Back,
                          () => PeaceScreenActions.DispatchCount,
                          () => PeaceScreenActions.LastDispatch.Id,
                          Phase.BackToPlayFromPeace);
    }

    /// <summary>
    /// The play screen again, back from the peace screen: the same judgement the
    /// war screen's return got, and then the trip's last click - the play
    /// screen's own Previous Menu, which is the one that reaches the MENU.
    /// </summary>
    private static void TickBackToPlayFromPeace()
    {
      if (playFromPeaceChecked) return;

      Game game = Game.GetInstance();
      GameObject play = GameObject.Find("PlayScreenUI");
      if (game == null || game.State != GameState.PLAY_SCREEN || play == null
          || GameObject.Find("PeaceScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the play screen never came back after the peace screen's Previous menu");
        return;
      }

      NoteFrontendState(game.State);
      playFromPeaceChecked = true;

      Check(ReportHas("Game flow: PEACE_SCREEN -> PLAY_SCREEN."),
            "the peace screen's Previous menu brought the flow back to the PLAY SCREEN, not to the "
            + "menu (the console logged PEACE_SCREEN -> PLAY_SCREEN, the PeaceScreenUI root is "
            + "destroyed, and the play screen's root is back)");
      CheckPlayScreenCameBack("peace");

      ClickPlayScreenRow(PlayScreenAction.Back, Phase.MenuBack);
    }

    // ------------------------------------------------------------------
    // the campaign screen, behind the war screen's Campaign row - one branch
    // deeper into the frontend's tree (the war screen opened it, and the war
    // screen is where its own way back goes)
    // ------------------------------------------------------------------

    /// <summary>
    /// The campaign screen, once the war screen's Campaign click has opened it -
    /// the same walk the two path screens got: its own content (its own canvas
    /// above the war screen's, the install's own per-chapter picture as the
    /// backdrop, its own aspect-ratio bars, its twelve chapter buttons with the
    /// shipped labels and the install's own button art), its own rows against the
    /// install's table, its twelve destination-less chapter rows clicked through
    /// its own one entry point, and the buttons judged against the art itself
    /// (each one stands on a slot the installed picture paints). Then the
    /// capture, and then its own Previous Menu - which goes back to the WAR
    /// SCREEN it was opened from, never to the play screen and never to the menu.
    /// </summary>
    private static void TickCampaignScreen()
    {
      if (Expired(PlayScreenBudgetSeconds, "the campaign screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      // The click only ASKS for the screen; the flow shows it on its own next
      // Update - the same one-frame hand-over every screen here has.
      if (game.State != GameState.CAMPAIGN_SCREEN || GameObject.Find("CampaignScreenUI") == null) return;
      NoteFrontendState(game.State);

      if (!campaignScreenChecked)
      {
        campaignScreenChecked = true;

        Check(ReportHas("Game flow: WAR_SCREEN -> CAMPAIGN_SCREEN."),
              $"the war screen's Campaign click moved the flow to the campaign screen "
              + $"(the console logged WAR_SCREEN -> CAMPAIGN_SCREEN, state={game.State})");
        Check(GameObject.Find("WarScreenUI") == null,
              "and the war screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckCampaignScreenContent();
        CheckCampaignStringsAgainstInstall();
        CheckCampaignButtons();
        CheckCampaignSlots();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(CampaignScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {CampaignScreenCapturePath}");
        return;
      }

      if (!campaignCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        campaignCaptureChecked = true;
        CheckPathScreenCapture(CampaignScreenCapturePath, CampaignScreenCanvasCapturePath,
                               "CampaignScreenUI", "campaign");
      }

      ClickDepartingRow("campaign", "CampaignScreenActions",
                        CampaignScreenActions.Back.ButtonName, CampaignScreenActions.Back.Id,
                        CampaignScreenActions.Back.Label, CampaignScreenActions.Back.Outcome,
                        "way back", () => CampaignScreenActions.DispatchCount,
                        () => CampaignScreenActions.LastDispatch.Id, Phase.BackToWarFromCampaign);
    }

    /// <summary>
    /// The war screen, back from the campaign screen: the campaign screen's own
    /// Previous Menu must have brought the flow here - the campaign screen is a
    /// branch of the war screen, so not to the play screen and not to the menu -
    /// and the war screen must be REBUILT with its six pictures and a fresh
    /// dispatch record (the destination-less clicks and the Campaign click it
    /// took before belong to the showing that is gone). Then the screen's SECOND
    /// branch: the Conquest Trail row's own click, which opens the trail screen.
    /// The war screen's own Previous menu, the click that reaches the PLAY
    /// SCREEN, is made after the trail visit (TickBackToWarFromConquest).
    /// </summary>
    private static void TickBackToWarFromCampaign()
    {
      if (warFromCampaignChecked) return;

      Game game = Game.GetInstance();
      GameObject war = GameObject.Find("WarScreenUI");
      if (game == null || game.State != GameState.WAR_SCREEN || war == null
          || GameObject.Find("CampaignScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the war screen never came back after the campaign screen's Previous Menu");
        return;
      }

      NoteFrontendState(game.State);
      warFromCampaignChecked = true;

      Check(ReportHas("Game flow: CAMPAIGN_SCREEN -> WAR_SCREEN."),
            "the campaign screen's Previous Menu brought the flow back to the WAR SCREEN, not to the "
            + "play screen and not to the menu (the console logged CAMPAIGN_SCREEN -> WAR_SCREEN, the "
            + "CampaignScreenUI root is destroyed, and the war screen's root is back)");
      CheckWarScreenCameBack("campaign");

      PathScreenRow conquestRow = WarScreenActions.Picture(1);
      ClickDepartingRow("war", "WarScreenActions", conquestRow.ButtonName, conquestRow.Id,
                        conquestRow.Label, conquestRow.Outcome, "Conquest Trail",
                        () => WarScreenActions.DispatchCount,
                        () => WarScreenActions.LastDispatch.Id, Phase.ConquestScreen);
    }

    /// <summary>
    /// The war screen, back from the Conquest Trail: the trail screen's own
    /// Previous menu must have brought the flow here - the trail is a branch of
    /// the war screen like the campaign screen - and the war screen must be
    /// REBUILT again, with its six pictures and a fresh dispatch record. Then the
    /// screen's THIRD branch: the Siege row's own click, which opens the siege
    /// selection. The war screen's own Previous menu, the click that reaches the
    /// PLAY SCREEN, is made after the siege visit (TickBackToWarFromSiege).
    /// </summary>
    private static void TickBackToWarFromConquest()
    {
      if (warFromConquestChecked) return;

      Game game = Game.GetInstance();
      GameObject war = GameObject.Find("WarScreenUI");
      if (game == null || game.State != GameState.WAR_SCREEN || war == null
          || GameObject.Find("ConquestScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the war screen never came back after the Conquest Trail screen's Previous menu");
        return;
      }

      NoteFrontendState(game.State);
      warFromConquestChecked = true;

      Check(ReportHas("Game flow: CONQUEST_TRAIL_SCREEN -> WAR_SCREEN."),
            "the Conquest Trail screen's Previous menu brought the flow back to the WAR SCREEN, not to "
            + "the play screen and not to the menu (the console logged CONQUEST_TRAIL_SCREEN -> "
            + "WAR_SCREEN, the ConquestScreenUI root is destroyed, and the war screen's root is back)");
      CheckWarScreenCameBack("Conquest Trail");

      PathScreenRow siegeRow = WarScreenActions.Picture(3);
      ClickDepartingRow("war", "WarScreenActions", siegeRow.ButtonName, siegeRow.Id,
                        siegeRow.Label, siegeRow.Outcome, "Siege",
                        () => WarScreenActions.DispatchCount,
                        () => WarScreenActions.LastDispatch.Id, Phase.SiegeScreen);
    }

    /// <summary>
    /// The war screen, back from the siege selection: the siege screen's own
    /// Previous Menu must have brought the flow here - the siege selection is a
    /// branch of the war screen like the campaign screen and the trail - and the
    /// war screen must be REBUILT a third time, with its six pictures and a fresh
    /// dispatch record. Then the screen's FOURTH branch: the Custom row's own
    /// click, which opens the map request screen. The war screen's own Previous
    /// menu, the click that reaches the PLAY SCREEN, is made after the Custom
    /// and Load visits (TickBackToWarFromLoad).
    /// </summary>
    private static void TickBackToWarFromSiege()
    {
      if (warFromSiegeChecked) return;

      Game game = Game.GetInstance();
      GameObject war = GameObject.Find("WarScreenUI");
      if (game == null || game.State != GameState.WAR_SCREEN || war == null
          || GameObject.Find("SiegeScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the war screen never came back after the siege selection screen's Previous Menu");
        return;
      }

      NoteFrontendState(game.State);
      warFromSiegeChecked = true;

      Check(ReportHas("Game flow: SIEGE_SCREEN -> WAR_SCREEN."),
            "the siege selection screen's Previous Menu brought the flow back to the WAR SCREEN, not to "
            + "the play screen and not to the menu (the console logged SIEGE_SCREEN -> WAR_SCREEN, the "
            + "SiegeScreenUI root is destroyed, and the war screen's root is back)");
      CheckWarScreenCameBack("siege selection");

      PathScreenRow customRow = WarScreenActions.Picture(4);
      ClickDepartingRow("war", "WarScreenActions", customRow.ButtonName, customRow.Id,
                        customRow.Label, customRow.Outcome, "Custom",
                        () => WarScreenActions.DispatchCount,
                        () => WarScreenActions.LastDispatch.Id, Phase.MapRequestScreen);
    }

    /// <summary>
    /// The war screen, back from the map request screen: the screen's own way
    /// back must have brought the flow here - the map request screen is a branch
    /// of the war screen like the campaign screen, the trail and the siege
    /// selection - and the war screen must be REBUILT a fourth time, with its
    /// six pictures and a fresh dispatch record. Then the screen's FIFTH branch:
    /// the Load row's own click, which opens the load screen.
    /// </summary>
    private static void TickBackToWarFromCustom()
    {
      if (warFromCustomChecked) return;

      Game game = Game.GetInstance();
      GameObject war = GameObject.Find("WarScreenUI");
      if (game == null || game.State != GameState.WAR_SCREEN || war == null
          || GameObject.Find("MapRequestScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the war screen never came back after the map request screen's way back");
        return;
      }

      NoteFrontendState(game.State);
      warFromCustomChecked = true;

      Check(ReportHas("Game flow: MAP_REQUEST_SCREEN -> WAR_SCREEN."),
            "the map request screen's way back brought the flow back to the WAR SCREEN, not to the "
            + "play screen and not to the menu (the console logged MAP_REQUEST_SCREEN -> WAR_SCREEN, "
            + "the MapRequestScreenUI root is destroyed, and the war screen's root is back)");
      CheckWarScreenCameBack("map request");

      PathScreenRow loadRow = WarScreenActions.Picture(5);
      ClickDepartingRow("war", "WarScreenActions", loadRow.ButtonName, loadRow.Id,
                        loadRow.Label, loadRow.Outcome, "Load",
                        () => WarScreenActions.DispatchCount,
                        () => WarScreenActions.LastDispatch.Id, Phase.LoadScreen);
    }

    /// <summary>
    /// The war screen, back from the load screen: the load screen's own Cancel
    /// row must have brought the flow here - the load screen is a branch of the
    /// war screen like the other four - and the war screen must be REBUILT a
    /// fifth time, with its six pictures and a fresh dispatch record. Then the
    /// screen's SIXTH and LAST branch: the Kingmaker row's own click, which opens
    /// the scenario lobby screen.
    /// </summary>
    private static void TickBackToWarFromLoad()
    {
      if (warFromLoadChecked) return;

      Game game = Game.GetInstance();
      GameObject war = GameObject.Find("WarScreenUI");
      if (game == null || game.State != GameState.WAR_SCREEN || war == null
          || GameObject.Find("LoadScreenUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the war screen never came back after the load screen's Cancel row");
        return;
      }

      NoteFrontendState(game.State);
      warFromLoadChecked = true;

      Check(ReportHas("Game flow: LOAD_SCREEN -> WAR_SCREEN."),
            "the load screen's Cancel row brought the flow back to the WAR SCREEN, not to the play "
            + "screen and not to the menu (the console logged LOAD_SCREEN -> WAR_SCREEN, the "
            + "LoadScreenUI root is destroyed, and the war screen's root is back)");
      CheckWarScreenCameBack("load");

      PathScreenRow kingmakerRow = WarScreenActions.Picture(2);
      ClickDepartingRow("war", "WarScreenActions", kingmakerRow.ButtonName, kingmakerRow.Id,
                        kingmakerRow.Label, kingmakerRow.Outcome, "Kingmaker",
                        () => WarScreenActions.DispatchCount,
                        () => WarScreenActions.LastDispatch.Id, Phase.ScenarioLobby);
    }

    /// <summary>
    /// The war screen, back from the scenario lobby: the lobby's own Back row must
    /// have brought the flow here - the lobby is a branch of the war screen like
    /// the other five - and the war screen must be REBUILT a sixth time, with its
    /// six pictures and a fresh dispatch record. This is the last return to this
    /// screen, so its own Previous menu - the click that reaches the PLAY SCREEN -
    /// is what comes next.
    /// </summary>
    private static void TickBackToWarFromLobby()
    {
      if (warFromLobbyChecked) return;

      Game game = Game.GetInstance();
      GameObject war = GameObject.Find("WarScreenUI");
      if (game == null || game.State != GameState.WAR_SCREEN || war == null
          || GameObject.Find("ScenarioLobbyUI") != null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the war screen never came back after the scenario lobby's Back row");
        return;
      }

      NoteFrontendState(game.State);
      warFromLobbyChecked = true;

      Check(ReportHas("Game flow: SCENARIO_LOBBY_SCREEN -> WAR_SCREEN."),
            "the scenario lobby's Back row brought the flow back to the WAR SCREEN, not to the play "
            + "screen and not to the menu (the console logged SCENARIO_LOBBY_SCREEN -> WAR_SCREEN, the "
            + "ScenarioLobbyUI root is destroyed, and the war screen's root is back)");
      CheckWarScreenCameBack("scenario lobby");

      ClickPathScreenBack("war", "WarScreenActions", WarScreenActions.Back,
                          () => WarScreenActions.DispatchCount,
                          () => WarScreenActions.LastDispatch.Id,
                          Phase.BackToPlayFromWar);
    }

    /// <summary>
    /// The play screen as it comes back from one of its path screens: it is
    /// REBUILT (PlayScreenUI.Show) rather than kept underneath the screen that
    /// replaced it, so its own content is there again and its dispatch record
    /// starts over - which is what makes "the record describes the screen that is
    /// on" true.
    /// </summary>
    private static void CheckPlayScreenCameBack(string from)
    {
      int buttons = 0;
      int withSprites = 0;
      foreach (PlayScreenActionInfo info in PlayScreenActions.All)
      {
        GameObject buttonObject = GameObject.Find(info.Action + "Button");
        UnityEngine.UI.Image image = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (image == null) continue;
        buttons++;
        if (image.sprite != null) withSprites++;
      }

      Check(buttons == PlayScreenActions.All.Length && withSprites == PlayScreenActions.All.Length,
            $"and the play screen, back from the {from} screen, is the four-button screen again "
            + $"({withSprites}/{buttons} buttons with their shipped sprites)");
      Check(PlayScreenActions.DispatchCount == 0 && !PlayScreenActions.HasDispatch,
            $"and it starts its own dispatch record over "
            + $"({PlayScreenActions.DispatchCount} dispatch(es) since it was rebuilt)");
    }

    /// <summary>
    /// The war screen as it comes back from one of its own branches (the campaign
    /// screen or the Conquest Trail): it is REBUILT (WarScreenUI.SetShown -> Show)
    /// rather than kept underneath the screen that replaced it, so its six
    /// pictures are there again and its dispatch record starts over - the clicks
    /// of the showing that is gone (its destination-less rows and the branch's own
    /// row) are not in it. <paramref name="from"/> names the branch in the
    /// check's own wording.
    /// </summary>
    private static void CheckWarScreenCameBack(string from)
    {
      int buttons = 0;
      int withPictures = 0;
      foreach (PathScreenRow row in WarScreenActions.Pictures)
      {
        GameObject buttonObject = GameObject.Find(row.ButtonName);
        UnityEngine.UI.Image image = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (image == null) continue;
        buttons++;
        if (image.sprite != null) withPictures++;
      }

      Check(buttons == WarScreenActions.Pictures.Length
            && withPictures == WarScreenActions.Pictures.Length,
            $"and the war screen, back from the {from} screen, is the six-picture screen again "
            + $"({withPictures}/{buttons} buttons with their shipped pictures)");
      Check(WarScreenActions.DispatchCount == 0 && !WarScreenActions.HasDispatch,
            $"and it starts its own dispatch record over "
            + $"({WarScreenActions.DispatchCount} dispatch(es) since it was rebuilt)");
    }

    /// <summary>
    /// The war screen's six rows now that three of its branches exist: the
    /// Campaign, Conquest Trail and Siege rows carry the screens they open (Built
    /// is derived from that, so a row cannot claim a screen the flow cannot show)
    /// and the other three still name their shipped screens as not built - the
    /// honest wording, both directions.
    /// </summary>
    private static void CheckTheWarScreensBuiltRows()
    {
      PathScreenRow campaign = WarScreenActions.Picture(0);
      Check(campaign.Built && campaign.Opens == GameState.CAMPAIGN_SCREEN
            && campaign.Outcome == campaign.Destination,
            $"the war screen's {campaign.Label} row now says where it goes "
            + $"(\"{campaign.Outcome}\")");

      PathScreenRow conquest = WarScreenActions.Picture(1);
      Check(conquest.Built && conquest.Opens == GameState.CONQUEST_TRAIL_SCREEN
            && conquest.Outcome == conquest.Destination,
            $"and its {conquest.Label} row the same, the screen behind it being the trail the port "
            + $"built earlier (\"{conquest.Outcome}\")");

      PathScreenRow siege = WarScreenActions.Picture(3);
      Check(siege.Built && siege.Opens == GameState.SIEGE_SCREEN
            && siege.Outcome == siege.Destination,
            $"and its {siege.Label} row the same, the screen behind it being the siege selection the "
            + $"port built just before it (\"{siege.Outcome}\")");

      PathScreenRow custom = WarScreenActions.Picture(4);
      Check(custom.Built && custom.Opens == GameState.MAP_REQUEST_SCREEN
            && custom.Outcome == custom.Destination,
            $"and its {custom.Label} row the same, the screen behind it being the map request screen "
            + $"the port built now - the shipped engine's own Custom branch hands this row's "
            + $"own picture to that screen and opens it (\"{custom.Outcome}\")");

      PathScreenRow load = WarScreenActions.Picture(5);
      Check(load.Built && load.Opens == GameState.LOAD_SCREEN
            && load.Outcome == load.Destination,
            $"and its {load.Label} row the same, the screen behind it being the load screen the port "
            + $"built now - the engine's own LoadScenarioScreen (\"{load.Outcome}\")");

      PathScreenRow kingmaker = WarScreenActions.Picture(2);
      Check(kingmaker.Built && kingmaker.Opens == GameState.SCENARIO_LOBBY_SCREEN
            && kingmaker.Outcome == kingmaker.Destination,
            $"and its {kingmaker.Label} row the same, the screen behind it being the scenario lobby "
            + "the port built now - MEASURED out of the shipped engine, which is where the "
            + "earlier reading was corrected: the war screen's own handler gives this row's control the "
            + $"action id whose branch pushes 'scenario lobby screen' (\"{kingmaker.Outcome}\")");

      int unbuilt = 0;
      var stillUnbuilt = new List<string>();
      int built = 0;
      foreach (PathScreenRow row in WarScreenActions.Pictures)
      {
        if (row.Built)
        {
          built++;
          continue;
        }

        if (row.Outcome.StartsWith("no destination yet: ", StringComparison.Ordinal)
            && row.Outcome.EndsWith(" is not built", StringComparison.Ordinal))
        {
          unbuilt++;
          stillUnbuilt.Add(row.Id);
        }
      }
      Check(built == WarScreenActions.Pictures.Length && unbuilt == 0,
            $"so ALL {WarScreenActions.Pictures.Length} of the war screen's picture rows now open a "
            + $"screen the flow can really show, and the aggregate of rows naming their shipped screen "
            + $"as not built is {unbuilt} (was 1 before this change - the Kingmaker row was the last; "
            + $"{string.Join(", ", stillUnbuilt)})");
    }

    /// <summary>
    /// The round trip, as the runner's own record of the states it observed: the
    /// frontend's tree, walked from the hub to its leaves and back - menu, play
    /// screen, war screen, campaign screen (the war screen's first branch), war
    /// screen again, the Conquest Trail (the war screen's second branch), war
    /// screen again, the siege selection (the war screen's third branch), war
    /// screen again, the map request screen (the war screen's fourth branch),
    /// war screen again, the load screen (the war screen's fifth branch), war
    /// screen again, play screen, peace screen, play screen, menu. Each screen's
    /// root was judged where it stood (every phase above asserts its own root is
    /// up and the others are gone), so this is the ORDER the flow moved in; the
    /// record starts at the play screen because the menu -> play step is the Play
    /// click, judged where the menu stood.
    /// </summary>
    private static void CheckTheFrontendRoundTrip()
    {
      var expected = new[]
      {
        GameState.PLAY_SCREEN, GameState.WAR_SCREEN, GameState.CAMPAIGN_SCREEN,
        GameState.WAR_SCREEN, GameState.CONQUEST_TRAIL_SCREEN, GameState.WAR_SCREEN,
        GameState.SIEGE_SCREEN, GameState.WAR_SCREEN,
        GameState.MAP_REQUEST_SCREEN, GameState.WAR_SCREEN,
        GameState.LOAD_SCREEN, GameState.WAR_SCREEN,
        GameState.SCENARIO_LOBBY_SCREEN, GameState.WAR_SCREEN,
        GameState.PLAY_SCREEN, GameState.PEACE_SCREEN, GameState.PLAY_SCREEN, GameState.MAIN_MENU,
      };

      bool same = frontendTrip.Count == expected.Length;
      for (int i = 0; same && i < expected.Length; i++)
      {
        if (frontendTrip[i] != expected[i]) same = false;
      }

      Check(same,
            "the frontend round trip is menu -> play -> war -> campaign -> war -> conquest trail -> war "
            + "-> siege -> war -> map request -> war -> load -> war -> scenario lobby -> war -> play -> "
            + "peace -> play -> menu "
            + "(main menu -> " + string.Join(" -> ", frontendTrip) + ")");
    }

    /// <summary>
    /// What one of the two path screens carries, read off the screen itself: its
    /// own canvas above the play screen's, the install's own backdrop (the one
    /// picture the two path screens share), its own aspect-ratio bars, and one
    /// button per picture row - each with the install's own picture FOR THAT ROW
    /// and the row's shipped label under it. The buttons are found by the row's
    /// own ButtonName, never by position.
    /// </summary>
    private static void CheckPathScreenContent(string screenLabel, string rootName, PathScreenRow[] pictures)
    {
      GameObject screen = GameObject.Find(rootName);
      Check(screen != null, $"the {screenLabel} screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1200,
            $"it is its own canvas above the play screen's "
            + $"(sorting order {canvas?.sortingOrder}, the play screen's is 1200)");

      GameObject background = GameObject.Find(rootName + "Background");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.WarPeaceMenuBackgroundTexture),
            $"its background is the install's own backdrop for the two path screens "
            + $"('{backgroundTexture?.name}' = "
            + $"{Path.GetFileName(UIAssets.WarPeaceMenuBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height})");

      // The screen is 4:3 like the menu's and the play screen's, so it carries
      // its own bars: whichever screen is on owns the widescreen gutters.
      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the "
            + $"shipped stone and not the scene behind the screen "
            + $"({barSprites}/2 bars with their stone sprite)");

      int buttons = 0;
      int withPictures = 0;
      int labelled = 0;
      var detail = new List<string>();
      foreach (PathScreenRow row in pictures)
      {
        GameObject buttonObject = GameObject.Find(row.ButtonName);
        UnityEngine.UI.Image image = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (image != null)
        {
          buttons++;
          // The row's OWN picture, by the install's file name: a row cannot be
          // built on another row's art.
          if (image.sprite != null && image.sprite.texture != null
              && image.sprite.texture.name == Path.GetFileName(row.Texture))
          {
            withPictures++;
          }
          else
          {
            detail.Add($"{row.Label}: picture='{image.sprite?.texture?.name ?? "<none>"}' "
                       + $"expected='{Path.GetFileName(row.Texture)}'");
          }
        }

        GameObject captionObject = GameObject.Find(row.Label + "Caption");
        UnityEngine.UI.Text caption = captionObject != null
          ? captionObject.GetComponent<UnityEngine.UI.Text>() : null;
        if (caption != null && caption.text == row.Label) labelled++;
      }

      Check(buttons == pictures.Length && withPictures == pictures.Length && labelled == pictures.Length,
            $"its {pictures.Length} picture buttons carry the install's own picture for their row "
            + $"and the shipped labels ({withPictures}/{pictures.Length} pictures, "
            + $"{labelled}/{pictures.Length} labels"
            + (detail.Count > 0 ? "; " + string.Join("; ", detail) : "") + ")");

      Line($"{screenLabel} screen  : buttons "
           + string.Join(", ", Array.ConvertAll(pictures, row => $"{row.Label}='{Path.GetFileName(row.Texture)}'")));
    }

    /// <summary>
    /// One path screen's way back: the frontend's own fancy button with the row's
    /// shipped label inside it (the table names this row, not its picture - the
    /// same button the menu's rows and the play screen's own way back use).
    /// </summary>
    private static void CheckPathScreenBackButton(string screenLabel, PathScreenRow back) =>
      CheckBackButton(screenLabel, back.ButtonName, back.Label, back.Id);

    /// <summary>
    /// A screen's way back, read off the button itself: the frontend's own fancy
    /// button with the shipped label inside it - the campaign screen's own way back
    /// is the same kind of button and gets the same judgement.
    /// </summary>
    private static void CheckBackButton(string screenLabel, string buttonName, string label, string id)
    {
      GameObject buttonObject = GameObject.Find(buttonName);
      UnityEngine.UI.Image image = buttonObject != null
        ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
      Transform labelTransform = buttonObject != null ? buttonObject.transform.Find("Label") : null;
      UnityEngine.UI.Text text = labelTransform != null
        ? labelTransform.GetComponent<UnityEngine.UI.Text>() : null;
      Check(image != null && image.sprite != null && text != null && text.text == label,
            $"its way back is the frontend's own button with the shipped label inside it "
            + $"('{text?.text}' = {id})");
    }

    /// <summary>
    /// One path screen's own rows against the install: every row's id and text
    /// must BE the shipped table's row of that id, read at run time - the same
    /// authority check the menu's six and the play screen's four get. The rows'
    /// tooltip CONTENT (the shipped hover text) rides on them as well and is
    /// printed for the report.
    /// </summary>
    private static void CheckPathScreenRowsAgainstInstall(string screenLabel, string prefix, PathScreenRow[] all)
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      foreach (PathScreenRow row in all)
      {
        if (shipped.TryGetValue(row.Id, out string english) && english == row.Label) continue;
        mismatches.Add($"{row.Id} shipped="
                       + $"{(shipped.TryGetValue(row.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{row.Label}'");
      }

      Check(mismatches.Count == 0 && shipped.Count > 0,
            $"the {screenLabel} screen's {all.Length} action ids are the shipped table's own rows, "
            + $"with the shipped text ({all.Length - mismatches.Count}/{all.Length} {prefix} rows "
            + $"agree with i18n/en-us.xml, {shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");

      foreach (PathScreenRow row in all)
      {
        if (row.Tooltip.Length > 0)
        {
          Line($"{screenLabel} screen  : {row.Id} tooltip content '{row.Tooltip}'");
        }
      }
    }

    /// <summary>
    /// One path screen's rows that have no destination yet, clicked the same way
    /// the menu's five and the play screen's unbuilt rows were - the button's own
    /// pointer click - with the dispatch record read after each, so each one has
    /// to be its own row on the screen's ONE entry point. The rows that leave the
    /// screen are deliberately not among them - the ways back, and the war
    /// screen's Campaign row - because they are the round trip's own clicks
    /// (<see cref="ClickPathScreenRow"/>).
    /// </summary>
    private static void CheckPathScreenButtons(string screenLabel, string surfaceName, PathScreenRow[] all,
                                               Func<int> dispatchCount, Func<PathScreenRow> lastDispatch)
    {
      var seen = new HashSet<string>();
      int clicked = 0;
      int batchStart = dispatchCount();
      foreach (PathScreenRow row in all)
      {
        if (row.Built) continue;  // the way back: the round trip's own click

        GameObject buttonObject = GameObject.Find(row.ButtonName);
        UnityEngine.UI.Button button = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null;
        if (button == null)
        {
          Fail($"the {screenLabel} screen's {row.Label} button ({row.ButtonName}) is not on the "
               + "screen, so its click could not be made");
          continue;
        }

        int before = dispatchCount();
        PressButton(button);
        PathScreenRow dispatched = lastDispatch();
        clicked++;
        bool ok = dispatchCount() == before + 1 && dispatched.Id == row.Id;
        if (ok) seen.Add(dispatched.Id);

        Check(ok,
              $"the {screenLabel} screen's {row.Label} button's click dispatched {row.Id} "
              + $"'{row.Label}' through its one entry point ({surfaceName}.Dispatch) - "
              + $"{row.Outcome}");
      }

      int unbuilt = 0;
      string lastUnbuilt = "";
      foreach (PathScreenRow row in all)
      {
        if (row.Built) continue;
        unbuilt++;
        lastUnbuilt = row.Label;
      }

      Check(clicked == unbuilt && dispatchCount() - batchStart == clicked && seen.Count == clicked,
            $"and its {unbuilt} destination-less action(s) each dispatched once through that one "
            + $"entry point ({dispatchCount() - batchStart} dispatch(es) for {clicked} click(s), "
            + $"{seen.Count} distinct action(s); the last, {lastUnbuilt}, is clicked last)");
    }

    /// <summary>
    /// One of a frontend screen's rows whose click LEAVES the screen, clicked
    /// through its own button: the ways back, which reach the screen the screen
    /// was opened from (the play screen's and the war screen's, never the menu,
    /// which only the play screen's own way back reaches), and the war screen's
    /// Campaign row, which reaches the campaign screen - a branch of the war
    /// screen. The row's own facts are passed rather than a typed row, because
    /// the campaign screen's rows are not the path screens' kind.
    /// <paramref name="what"/> names the row in the check's own wording.
    /// </summary>
    private static void ClickDepartingRow(string screenLabel, string surfaceName,
                                          string buttonName, string rowId, string rowLabel,
                                          string outcome, string what,
                                          Func<int> dispatchCount, Func<string> lastDispatchId,
                                          Phase next)
    {
      GameObject buttonObject = GameObject.Find(buttonName);
      UnityEngine.UI.Button button = buttonObject != null
        ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null;
      if (button == null)
      {
        Fail($"the {screenLabel} screen's {rowLabel} button ({buttonName}) is not on the "
             + "screen, so its click could not be made");
        BeginPhase(next, PlayScreenBudgetSeconds);
        return;
      }

      int before = dispatchCount();
      PressButton(button);
      string dispatchedId = lastDispatchId();
      Check(dispatchCount() == before + 1 && dispatchedId == rowId,
            $"the {screenLabel} screen's {rowLabel} button's click dispatched {rowId} "
            + $"'{rowLabel}' through its one entry point ({surfaceName}.Dispatch) - "
            + $"the {what}: {outcome}");

      BeginPhase(next, PlayScreenBudgetSeconds);
    }

    /// <summary>
    /// One path screen's way back, clicked through its own button - the same
    /// leaving click, with the row's own facts read off the path row.
    /// </summary>
    private static void ClickPathScreenBack(string screenLabel, string surfaceName, PathScreenRow back,
                                            Func<int> dispatchCount, Func<string> lastDispatchId,
                                            Phase next) =>
      ClickDepartingRow(screenLabel, surfaceName, back.ButtonName, back.Id, back.Label, back.Outcome,
                        "way back", dispatchCount, lastDispatchId, next);

    /// <summary>
    /// One path screen's capture: the Game view render, judged by its distinct
    /// colours, with the same canvas fallback the menu and the play screen use
    /// when an unfocused Game view stops rendering.
    /// </summary>
    private static void CheckPathScreenCapture(string capturePath, string canvasCapturePath,
                                               string rootName, string screenLabel)
    {
      int colours = CheckCapture(capturePath, $"the {screenLabel} screen's Game view capture");
      if (colours >= 8)
      {
        Check(true,
              $"the {screenLabel} screen renders its own art ({colours} distinct colours - "
              + "the shipped backdrop and its picture buttons)");
        return;
      }

      Line($"capture      : the Game view render looks blank; drawing the {screenLabel} screen's "
           + "canvas through a camera instead");
      int canvasColours = CaptureFrontendCanvas(rootName, canvasCapturePath,
                                                $"the {screenLabel} screen canvas capture");
      Check(canvasColours >= 8,
            $"the {screenLabel} screen's own canvas renders non-blank ({canvasColours} distinct "
            + $"colours, {canvasCapturePath})");
    }

    // ------------------------------------------------------------------
    // the campaign screen, judged on itself and against the install's own art
    // ------------------------------------------------------------------

    /// <summary>
    /// What the campaign screen carries, read off the screen itself: its own
    /// canvas above the war screen's, the install's own art for this screen as the
    /// backdrop (chapter 1's picture - see UIAssets for why that one), its own
    /// aspect-ratio bars, NO title (the shipped table names no CAMPAIGNSCREEN title
    /// row, so the port invents none), its twelve chapter buttons with the
    /// install's own button picture and the shipped chapter labels, and its own way
    /// back.
    /// </summary>
    private static void CheckCampaignScreenContent()
    {
      GameObject screen = GameObject.Find("CampaignScreenUI");
      Check(screen != null, "the campaign screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1300,
            $"it is its own canvas above the war screen's "
            + $"(sorting order {canvas?.sortingOrder}, the war screen's is 1300)");

      GameObject background = GameObject.Find("CampaignScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.CampaignBackgroundTexture),
            $"its backdrop is the install's own art for this screen "
            + $"('{backgroundTexture?.name}' = {Path.GetFileName(UIAssets.CampaignBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height} - the campaign's own first "
            + "chapter picture: the port has no progress data, so it opens where the shipped campaign "
            + "starts)");

      Check(GameObject.Find("CampaignScreenTitle") == null,
            "and it draws no title: the shipped table has no CAMPAIGNSCREEN title row "
            + "(the screen's own content is the chapter list)");

      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the "
            + $"shipped stone and not the scene behind the screen "
            + $"({barSprites}/2 bars with their stone sprite)");

      int buttons = 0;
      int withTheButtonArt = 0;
      int labelled = 0;
      var detail = new List<string>();
      foreach (CampaignScreenRow row in CampaignScreenActions.Chapters)
      {
        GameObject buttonObject = GameObject.Find(row.ButtonName);
        UnityEngine.UI.Image image = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (image != null)
        {
          buttons++;
          // The install's own button for this screen, by file name.
          if (image.sprite != null && image.sprite.texture != null
              && image.sprite.texture.name == Path.GetFileName(row.Texture))
          {
            withTheButtonArt++;
          }
          else
          {
            detail.Add($"{row.Label}: picture='{image.sprite?.texture?.name ?? "<none>"}' "
                       + $"expected='{Path.GetFileName(row.Texture)}'");
          }
        }

        Transform label = buttonObject != null ? buttonObject.transform.Find("Label") : null;
        UnityEngine.UI.Text text = label != null ? label.GetComponent<UnityEngine.UI.Text>() : null;
        if (text != null && text.text == row.Label) labelled++;
        else detail.Add($"{row.Label}: label='{text?.text ?? "<none>"}'");
      }

      Check(buttons == CampaignScreenActions.ChapterCount
            && withTheButtonArt == CampaignScreenActions.ChapterCount
            && labelled == CampaignScreenActions.ChapterCount,
            $"its {CampaignScreenActions.ChapterCount} chapter buttons carry the install's own button "
            + $"picture and the shipped chapter labels ({withTheButtonArt}/"
            + $"{CampaignScreenActions.ChapterCount} pictures, {labelled}/"
            + $"{CampaignScreenActions.ChapterCount} labels"
            + (detail.Count > 0 ? "; " + string.Join("; ", detail) : "") + ")");

      CheckBackButton("campaign", CampaignScreenActions.Back.ButtonName,
                      CampaignScreenActions.Back.Label, CampaignScreenActions.Back.Id);

      Line("campaign scr : " + string.Join(", ", Array.ConvertAll(
             CampaignScreenActions.Chapters, row => $"{row.Label} -> {row.Destination}")));
      Line("campaign scr : the install's six progress markers "
           + "(ui/campaign_screen/symbol_{good,bad,red}_{complete,current}.tga) are NOT drawn: "
           + "they are progressive-state art and this port has no save/progress data (named for a "
           + "later work). The install's other eleven chapter pictures "
           + "(campaign_chapter2..12.jpg) are not shown either - the port has no progress, so it "
           + "opens on chapter 1's picture and never changes it.");
    }

    /// <summary>
    /// The authority for the campaign screen's rows, checked against the install:
    /// every chapter button's LABEL is the shipped campaign row it names
    /// (WARCAMPAIGN_MISSION1..12 'Chapter 1'..'Chapter 12') and its tooltip content
    /// is the shipped static tooltip of its own row
    /// (CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12 'Start Chapter 1'..'Start Chapter
    /// 12') - the two rows the shipped table ships per button; the way back is
    /// CAMPAIGNSCREENSTATICTOOLTIP_EXIT 'Previous Menu'. The campaign's other names
    /// for these chapters - WAR_CAMPAIGN_HEADING00..11, the twelve titles - are
    /// read and reported too, and named as not drawn by this screen.
    /// </summary>
    private static void CheckCampaignStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      int pairs = 0;

      foreach (CampaignScreenRow row in CampaignScreenActions.Chapters)
      {
        pairs += 2;
        if (!(shipped.TryGetValue(row.LabelId, out string label) && label == row.Label))
        {
          mismatches.Add($"{row.LabelId} shipped="
                         + $"{(shipped.TryGetValue(row.LabelId, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port-label='{row.Label}'");
        }
        if (!(shipped.TryGetValue(row.Id, out string tooltip) && tooltip == row.Tooltip))
        {
          mismatches.Add($"{row.Id} shipped="
                         + $"{(shipped.TryGetValue(row.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port-tooltip='{row.Tooltip}'");
        }
      }

      pairs++;
      if (!(shipped.TryGetValue(CampaignScreenActions.Back.Id, out string exit)
            && exit == CampaignScreenActions.Back.Label))
      {
        mismatches.Add($"{CampaignScreenActions.Back.Id} shipped="
                       + $"{(shipped.TryGetValue(CampaignScreenActions.Back.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{CampaignScreenActions.Back.Label}'");
      }

      Check(mismatches.Count == 0 && shipped.Count > 0,
            $"the campaign screen's twelve chapter rows are the shipped table's own rows, label and "
            + $"tooltip both ({pairs - mismatches.Count}/{pairs} WARCAMPAIGN_MISSION*/"
            + $"CAMPAIGNSCREENSTATICTOOLTIP_* rows agree with i18n/en-us.xml, {shipped.Count} strings "
            + "read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");

      foreach (CampaignScreenRow row in CampaignScreenActions.Chapters)
      {
        Line($"campaign scr : {row.LabelId} label '{row.Label}' / {row.Id} tooltip content '{row.Tooltip}'");
      }

      int headings = 0;
      string firstHeading = "<none>";
      string lastHeading = "<none>";
      for (int i = 0; i < CampaignScreenActions.ChapterCount; i++)
      {
        if (shipped.TryGetValue($"WAR_CAMPAIGN_HEADING{i:00}", out string heading) && heading.Length > 0)
        {
          headings++;
          if (i == 0) firstHeading = heading;
          if (i == CampaignScreenActions.ChapterCount - 1) lastHeading = heading;
        }
      }
      Check(headings == CampaignScreenActions.ChapterCount,
            $"and the install's own chapter titles are all there for whoever builds the "
            + $"briefing screens ({headings}/{CampaignScreenActions.ChapterCount} WAR_CAMPAIGN_HEADING* "
            + $"rows read: '{firstHeading}' .. '{lastHeading}') - this screen draws none of them, "
            + "because no CAMPAIGNSCREEN* row names them");
    }

    /// <summary>
    /// The campaign screen's twelve chapter rows, clicked the same way every other
    /// destination-less row here is - the button's own pointer click - with the
    /// dispatch record read after each: each one has to be its own row on the
    /// screen's ONE entry point, and each one's click has to report its OWN
    /// chapter's map as not built, by name. The way back is not among them: its
    /// click leaves the screen and is the round trip's own.
    /// </summary>
    private static void CheckCampaignButtons()
    {
      int onScreen = 0;
      int withSprites = 0;
      int namedTheirMaps = 0;
      var wrong = new List<string>();

      for (int index = 0; index < CampaignScreenActions.ChapterCount; index++)
      {
        CampaignScreenRow row = CampaignScreenActions.Chapter(index);
        int chapter = index + 1;

        GameObject buttonObject = GameObject.Find(row.ButtonName);
        UnityEngine.UI.Button button = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null;
        UnityEngine.UI.Image image = button != null
          ? button.GetComponent<UnityEngine.UI.Image>() : null;
        if (button != null) onScreen++;
        if (image != null && image.sprite != null) withSprites++;

        string expectedMap = $"maps/war_chapter{chapter}.s2m";
        bool namesItsMap = row.Built
                           && row.MapPath == expectedMap
                           && row.Opens == GameState.NONE
                           && row.Destination.StartsWith(expectedMap + " ", StringComparison.Ordinal)
                           && row.Outcome == row.Destination;
        if (namesItsMap) namedTheirMaps++;
        else wrong.Add(row.Id);
      }

      Check(namedTheirMaps == CampaignScreenActions.ChapterCount,
            $"every one of the campaign screen's {CampaignScreenActions.ChapterCount} chapter rows "
            + "carries its own chapter's map and is built by it (Built derived from the row's own "
            + $"map file, its Outcome naming that map: {namedTheirMaps}/"
            + $"{CampaignScreenActions.ChapterCount} rows"
            + (wrong.Count > 0 ? $"; wrong: {string.Join(", ", wrong)}" : "") + ")");
      Check(onScreen == CampaignScreenActions.ChapterCount
            && withSprites == CampaignScreenActions.ChapterCount,
            $"and all {CampaignScreenActions.ChapterCount} of them stand on the screen with the "
            + $"install's own button picture ({withSprites}/{onScreen} buttons with their sprite). "
            + "They are not clicked here: a chapter click leaves the frontend for the game, so the "
            + "clicks belong to the chapter-start walk, which clicks exactly one");
    }

    /// <summary>
    /// The campaign screen's buttons against the ART they stand on. The install's
    /// own picture paints twelve parchment slots down its right-hand scroll column,
    /// and every chapter button must stand on one of them - so the runner reads the
    /// picture itself (the same file the screen loads) and, for each button,
    /// compares the luminance of the button's own middle with the luminance of the
    /// gap half a slot above it: in the art the slot parchment is much brighter
    /// than the dark gaps between the slots (and than the ornamental dividers two
    /// of those gaps carry). It also checks the twelve buttons make one straight
    /// column, top to bottom, so no two rows share a slot and none is off the
    /// column.
    /// </summary>
    private static void CheckCampaignSlots()
    {
      if (campaignSlotChecked) return;
      campaignSlotChecked = true;

      string artPath = UIAssets.CampaignBackgroundTexture;
      Texture2D art = null;
      try
      {
        if (File.Exists(artPath))
        {
          art = new Texture2D(2, 2, TextureFormat.RGBA32, false);
          if (!ImageConversion.LoadImage(art, File.ReadAllBytes(artPath))) art = null;
        }
      }
      catch (Exception error)
      {
        Line($"campaign art : {artPath} unreadable ({error.Message})");
      }

      if (art == null)
      {
        Fail($"the campaign screen's own art could not be read back ({artPath}), so its buttons "
             + "could not be judged against the slots it paints");
        return;
      }

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      int onParchment = 0;
      int centres = 0;
      int straight = 1;
      bool ordered = true;
      float minX = float.MaxValue;
      float maxX = float.MinValue;
      float previousY = float.MinValue;
      var offSlot = new List<string>();

      for (int index = 0; index < CampaignScreenActions.ChapterCount; index++)
      {
        GameObject buttonObject = GameObject.Find(CampaignScreenActions.Chapter(index).ButtonName);
        RectTransform rect = buttonObject != null
          ? buttonObject.GetComponent<RectTransform>() : null;
        if (rect == null)
        {
          Fail($"the campaign screen's chapter {index + 1} button is not on the screen, so its "
               + "place on the art's own slots could not be judged");
          continue;
        }

        // The button's centre in the art's own pixels (the art is the content
        // rect's own 4:3, so the fractions map straight onto it; the picture's y
        // runs down from its top, the texture's rows up from its bottom).
        float fractionX = 0.5f + rect.anchoredPosition.x / width;
        float fractionY = 0.5f - rect.anchoredPosition.y / height;
        int pixelX = Mathf.Clamp(Mathf.RoundToInt(fractionX * art.width), 0, art.width - 1);
        int pixelYFromTop = Mathf.Clamp(Mathf.RoundToInt(fractionY * art.height), 0, art.height - 1);
        int rowInTexture = art.height - 1 - pixelYFromTop;
        float slotHeightPixels = rect.sizeDelta.y / height * art.height;
        int halfWidth = Mathf.Max(2, Mathf.RoundToInt(rect.sizeDelta.x / width * art.width / 6f));
        int halfHeight = 2;
        int gapOffset = Mathf.RoundToInt(slotHeightPixels * 0.6f);  // half a slot's pitch

        float onSlot = PatchLuminance(art, pixelX, rowInTexture, halfWidth, halfHeight);
        float inGap = PatchLuminance(art, pixelX, rowInTexture + gapOffset, halfWidth, halfHeight);
        if (onSlot > inGap) onParchment++;
        else offSlot.Add($"chapter {index + 1}: on-slot {onSlot:F0} vs gap {inGap:F0}");

        centres++;
        minX = Mathf.Min(minX, pixelX);
        maxX = Mathf.Max(maxX, pixelX);
        if (pixelYFromTop <= previousY) ordered = false;
        previousY = pixelYFromTop;
      }

      if (centres > 1 && maxX - minX > 4f) straight = 0;

      Check(centres == CampaignScreenActions.ChapterCount && onParchment == centres,
            $"its {CampaignScreenActions.ChapterCount} chapter buttons stand on the slots the "
            + $"install's own art paints for them (each button's middle is brighter than the gap half "
            + $"a slot above it in {Path.GetFileName(artPath)}: {onParchment}/{centres} on parchment"
            + (offSlot.Count > 0 ? "; " + string.Join("; ", offSlot) : "") + ")");
      Check(straight == 1 && ordered,
            $"and they are one straight column, top to bottom - chapter 1 above chapter 12, one slot "
            + $"each ({centres} centres, x spread {maxX - minX:F0} px of the {art.width}-wide art, y "
            + "strictly increasing)");

      Line($"campaign art : {Path.GetFileName(artPath)} read back from the install for the slot check "
           + $"({art.width}x{art.height})");
      UnityEngine.Object.Destroy(art);
    }

    /// <summary>
    /// The mean luminance of a small patch of a texture, clamped to its edges - the
    /// unit the campaign screen's art checks are made of.
    /// </summary>
    private static float PatchLuminance(Texture2D texture, int centreX, int centreY,
                                        int halfWidth, int halfHeight)
    {
      Color32[] pixels = texture.GetPixels32();
      float sum = 0f;
      int count = 0;
      for (int y = centreY - halfHeight; y <= centreY + halfHeight; y++)
      {
        if (y < 0 || y >= texture.height) continue;
        for (int x = centreX - halfWidth; x <= centreX + halfWidth; x++)
        {
          if (x < 0 || x >= texture.width) continue;
          Color32 pixel = pixels[y * texture.width + x];
          sum += (pixel.r + pixel.g + pixel.b) / 3f;
          count++;
        }
      }
      return count > 0 ? sum / count : 0f;
    }

    // ------------------------------------------------------------------
    // the Conquest Trail screen, behind the war screen's Conquest Trail row -
    // the war screen's SECOND branch (the campaign screen is the first), judged
    // on itself and against the install's own art
    // ------------------------------------------------------------------

    /// <summary>The install's own trail-screen pictures, and what the runner expects to read off them.</summary>
    private const int ConquestArtWidth = 1024;
    private const int ConquestArtHeight = 768;
    private const int ConquestOverlayWidth = 558;
    private const int ConquestOverlayHeight = 357;

    // The siege selection screen's own art sizes, measured off the install
    // (ui/siege_menu/): the composed backdrop, the paintings in its larger frame
    // and the layout pictures in its smaller one.
    private const int SiegeArtWidth = 1024;
    private const int SiegeArtHeight = 768;
    private const int SiegePaintingWidth = 425;
    private const int SiegePaintingHeight = 313;
    private const int SiegeLayoutWidth = 146;
    private const int SiegeLayoutHeight = 163;

    /// <summary>
    /// The Conquest Trail screen, once the war screen's Conquest Trail click has
    /// opened it - the war screen's second branch, walked the way the campaign
    /// screen was: its own content (its own canvas, the Gold Edition's own
    /// composed trail picture as the backdrop, its own aspect-ratio bars, the
    /// trail's own progress picture over the map's central rect, the selected
    /// row's own shipped text in the art's own panel, the install's own paging
    /// arrows on the tiles the backdrop paints, the start row and the way back),
    /// its twelve rows against the install's table, the ten castles its texts
    /// name against the install's own files, the art by name AND by pixels, and
    /// then the paging walk itself - the trail's real input surface, which clicks
    /// along all twelve positions and back, reading each position's own text and
    /// picture off the screen and letting the ends name the install's own
    /// disabled rows. Then the capture, and then its own Previous menu - which
    /// goes back to the WAR SCREEN it was opened from, never to the play screen
    /// and never to the menu.
    /// </summary>
    private static void TickConquestScreen()
    {
      if (Expired(PlayScreenBudgetSeconds, "the Conquest Trail screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      // The click only ASKS for the screen; the flow shows it on its own next
      // Update - the same one-frame hand-over every screen here has.
      if (game.State != GameState.CONQUEST_TRAIL_SCREEN || GameObject.Find("ConquestScreenUI") == null)
        return;
      NoteFrontendState(game.State);

      if (!conquestScreenChecked)
      {
        conquestScreenChecked = true;

        Check(ReportHas("Game flow: WAR_SCREEN -> CONQUEST_TRAIL_SCREEN."),
              $"the war screen's Conquest Trail click moved the flow to the trail screen "
              + $"(the console logged WAR_SCREEN -> CONQUEST_TRAIL_SCREEN, state={game.State})");
        Check(GameObject.Find("WarScreenUI") == null,
              "and the war screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckConquestScreenContent();
        CheckConquestStringsAgainstInstall();
        CheckConquestCastlePairing();
        CheckConquestScreenArt();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(ConquestScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {ConquestScreenCapturePath}");
        return;
      }

      if (!conquestCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        conquestCaptureChecked = true;
        CheckPathScreenCapture(ConquestScreenCapturePath, ConquestScreenCanvasCapturePath,
                               "ConquestScreenUI", "Conquest Trail");

        // The paging walk comes AFTER the capture, so what the capture shows is
        // the screen as the shipped game opens it (its first castle) rather than
        // the state the last click left behind.
        CheckConquestPaging();
      }

      ClickDepartingRow("trail", "ConquestScreenActions",
                        ConquestScreenActions.Back.ButtonName, ConquestScreenActions.Back.Id,
                        ConquestScreenActions.Back.Label, ConquestScreenActions.Back.Outcome,
                        "way back", () => ConquestScreenActions.DispatchCount,
                        () => ConquestScreenActions.LastDispatch.Id, Phase.BackToWarFromConquest);
    }

    /// <summary>
    /// What the trail screen carries, read off the screen itself: its own canvas
    /// above the war screen's, the Gold Edition's own composed picture of the
    /// screen as the backdrop (ui/gold_edition/conquest_trail.png, 1024x768 - the
    /// map, its route, the two paging tiles and the empty gold panel), the
    /// trail's own progress picture for the position it opens on over the map's
    /// central rect (war_conquest_arrowsNN.jpg, 558x357), the selected row's own
    /// shipped text in the art's own panel, NO title (the shipped table names no
    /// CONQUEST* title row, so the port invents none), its own aspect-ratio bars,
    /// and its four buttons - each with the install's own picture for its row.
    /// </summary>
    private static void CheckConquestScreenContent()
    {
      GameObject screen = GameObject.Find("ConquestScreenUI");
      Check(screen != null, "the Conquest Trail screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1300,
            "it is its own canvas above the war screen's "
            + $"(sorting order {canvas?.sortingOrder}, the war screen's is 1300)");

      GameObject background = GameObject.Find("ConquestScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.ConquestTrailBackgroundTexture)
            && backgroundTexture.width == ConquestArtWidth
            && backgroundTexture.height == ConquestArtHeight,
            $"its backdrop is the install's own composed picture of this screen "
            + $"('{backgroundTexture?.name}' = {Path.GetFileName(UIAssets.ConquestTrailBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height} - the trail's map, its own paging "
            + "tiles and the empty panel, as the Gold Edition shipped them)");

      GameObject overlay = GameObject.Find("ConquestScreenUIOverlay");
      UnityEngine.UI.Image overlayImage = overlay != null
        ? overlay.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D overlayTexture = overlayImage != null && overlayImage.sprite != null
        ? overlayImage.sprite.texture : null;
      string openOverlay = Path.GetFileName(UIAssets.ConquestTrailOverlayTexture(1));
      Check(overlayTexture != null && overlayTexture.name == openOverlay
            && overlayTexture.width == ConquestOverlayWidth
            && overlayTexture.height == ConquestOverlayHeight,
            $"and over the map's central rect it carries the trail's OWN progress picture for the "
            + $"position it opens on ('{overlayTexture?.name}' = {openOverlay}, "
            + $"{overlayTexture?.width}x{overlayTexture?.height} - the install's own picture of the trail "
            + "one castle in, drawn where the shipped screen draws it)");

      ConquestTrailRow first = ConquestScreenActions.TrailRow(1);
      string shownText = LabelTextOf("ConquestScreenUIText");
      Check(shownText == first.Text,
            "and the art's own empty panel carries the trail's first row, whose text is the install's "
            + $"({first.Id}: '{Shorten(shownText)}')");

      Check(GameObject.Find("ConquestScreenUITitle") == null,
            "and it draws no title: the shipped table has no CONQUEST* title row "
            + "(the screen's own content is the trail's map and its rows)");

      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the shipped stone "
            + $"and not the scene behind the screen ({barSprites}/2 bars with their stone sprite)");

      int onScreen = 0;
      int withTheirOwnArt = 0;
      var wrongArt = new List<string>();
      foreach (ConquestScreenAction row in ConquestScreenActions.RowsFor(ConquestScreenActions.Position))
      {
        GameObject buttonObject = GameObject.Find(row.ButtonName);
        UnityEngine.UI.Image image = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (image != null) onScreen++;
        if (image != null && image.sprite != null && image.sprite.texture != null
            && image.sprite.texture.name == Path.GetFileName(row.Texture))
        {
          withTheirOwnArt++;
        }
        else
        {
          wrongArt.Add($"{row.Id}: picture='{image?.sprite?.texture?.name ?? "<none>"}' "
                       + $"expected='{Path.GetFileName(row.Texture)}'");
        }
      }
      Check(onScreen == 4 && withTheirOwnArt == 4,
            "and all four of its buttons stand on the screen, each with the install's own picture for "
            + $"its own row ({withTheirOwnArt}/4" + (wrongArt.Count > 0 ? "; " + string.Join("; ", wrongArt) : "")
            + ")");

      string startLabel = LabelTextOf(ConquestScreenActions.StartMission.ButtonName);
      Check(startLabel == ConquestScreenActions.StartMission.Label,
            $"its start row carries the shipped {ConquestScreenActions.StartMission.Id} text "
            + $"('{startLabel}' = '{ConquestScreenActions.StartMission.Label}') - the row a position with a "
            + "castle to take wears");
      CheckBackButton("Conquest Trail", ConquestScreenActions.Back.ButtonName,
                      ConquestScreenActions.Back.Label, ConquestScreenActions.Back.Id);

      Line("trail screen : the two things the install does not pin, said rather than faked: the trail's "
           + "folder ships NO button art, so the start row wears the install's own castle-scenario play "
           + $"button ({Path.GetFileName(UIAssets.ConquestStartMissionTexture)}) from the folder the castle "
           + "art comes from; and the art pins no place for it or for the way back, so both stand in the "
           + "panel's right-hand side, which the shipped picture leaves empty (the trail's text takes the "
           + "panel's left). The install's trail-completion art "
           + $"({Path.GetFileName(UIAssets.ConquestTrailWinScreenTexture)}, a crown on a purple field) is "
           + "NOT drawn: it is progressive-state art and this port has no trail progress (named, like the "
           + "campaign screen's six markers).");
    }

    /// <summary>
    /// The trail screen's rows against the install, read at run time: the twelve
    /// CONQUESTTRAIL_* rows (the trail's own texts), the two paging rows with the
    /// two disabled rows they ship beside them (CONQUEST_PREV_CASTLE /
    /// CONQUEST_NOPREV_CASTLE, CONQUEST_NEXT_CASTLE / CONQUEST_NONEXT_CASTLE),
    /// the two start rows (CONQUEST START MISSION and CONQUEST START AGAIN) and
    /// the way back's own row (WARSCREENSTATICTOOLTIP_EXIT - the trail ships no
    /// exit row of its own, so its way back wears the parent screen's own).
    /// </summary>
    private static void CheckConquestStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      int pairs = 0;

      foreach (ConquestTrailRow row in ConquestScreenActions.Trail)
      {
        pairs++;
        if (!(shipped.TryGetValue(row.Id, out string text) && text == row.Text))
        {
          mismatches.Add($"{row.Id} shipped="
                         + $"{(shipped.TryGetValue(row.Id, out string found) ? $"'{Shorten(found)}'" : "<no such row>")}"
                         + $" port='{Shorten(row.Text)}'");
        }
      }

      foreach (ConquestScreenAction row in new[] { ConquestScreenActions.PreviousCastle, ConquestScreenActions.NextCastle })
      {
        pairs += 2;
        if (!(shipped.TryGetValue(row.Id, out string tooltip) && tooltip == row.Tooltip))
        {
          mismatches.Add($"{row.Id} shipped="
                         + $"{(shipped.TryGetValue(row.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port-tooltip='{row.Tooltip}'");
        }
        if (!(shipped.TryGetValue(row.DisabledId, out string disabled) && disabled == row.DisabledText))
        {
          mismatches.Add($"{row.DisabledId} shipped="
                         + $"{(shipped.TryGetValue(row.DisabledId, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port='{row.DisabledText}'");
        }
      }

      foreach (ConquestScreenAction row in new[] { ConquestScreenActions.StartMission, ConquestScreenActions.StartAgain })
      {
        pairs++;
        if (!(shipped.TryGetValue(row.Id, out string text) && text == row.Label))
        {
          mismatches.Add($"{row.Id} shipped="
                         + $"{(shipped.TryGetValue(row.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port='{row.Label}'");
        }
      }

      pairs++;
      if (!(shipped.TryGetValue(ConquestScreenActions.Back.Id, out string exit)
            && exit == ConquestScreenActions.Back.Label))
      {
        mismatches.Add($"{ConquestScreenActions.Back.Id} shipped="
                       + $"{(shipped.TryGetValue(ConquestScreenActions.Back.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{ConquestScreenActions.Back.Label}'");
      }

      Check(mismatches.Count == 0 && shipped.Count > 0,
            $"the trail screen's {pairs} rows are the shipped table's own rows "
            + $"({pairs - mismatches.Count}/{pairs} CONQUEST*/WARSCREENSTATICTOOLTIP_EXIT rows agree with "
            + $"i18n/en-us.xml, {shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");

      foreach (ConquestTrailRow row in ConquestScreenActions.Trail)
      {
        Line($"trail screen : {row.Id} '{Shorten(row.Text)}'"
             + (row.NamesACastle ? $" -> {row.Castle} ({row.MapPath})" : " -> the trail's own completion row"));
      }
      Line($"trail screen : {ConquestScreenActions.PreviousCastle.Id} tooltip content "
           + $"'{ConquestScreenActions.PreviousCastle.Tooltip}' / disabled "
           + $"{ConquestScreenActions.PreviousCastle.DisabledId} "
           + $"'{ConquestScreenActions.PreviousCastle.DisabledText}'");
      Line($"trail screen : {ConquestScreenActions.NextCastle.Id} tooltip content "
           + $"'{ConquestScreenActions.NextCastle.Tooltip}' / disabled "
           + $"{ConquestScreenActions.NextCastle.DisabledId} "
           + $"'{ConquestScreenActions.NextCastle.DisabledText}'");
      Line($"trail screen : {ConquestScreenActions.StartMission.Id} "
           + $"'{ConquestScreenActions.StartMission.Label}' / {ConquestScreenActions.StartAgain.Id} "
           + $"'{ConquestScreenActions.StartAgain.Label}' / way back {ConquestScreenActions.Back.Id} "
           + $"'{ConquestScreenActions.Back.Label}'");
    }

    /// <summary>
    /// The castles the trail's own texts name, against the install's own files:
    /// every row that names a castle must name it IN ITS TEXT, and that castle
    /// must have the install's painting (ui/siege_menu/castle_&lt;key&gt;_painting.jpg)
    /// and its own historical castle map (maps/&lt;castle&gt;.s2c). This is the
    /// text->castle pairing measured rather than assumed: the install's file key
    /// is not always the text's spelling ('Haut-Koenigsburg' in the prose,
    /// hautkoenigsbourg in the file), so both are carried and both are checked.
    /// Then what a trail mission would LOAD is reported: the map and the buildings
    /// artifact are there for every one of them; the entity artifact - which the
    /// port's map loader needs to place actors - is not (the per-map entity dump
    /// has never been run over the historical castle maps), which is why starting
    /// a trail mission is a later item.
    /// </summary>
    private static void CheckConquestCastlePairing()
    {
      int named = 0;
      var castles = new List<string>();
      var castlesAgain = new List<string>();
      var keys = new List<string>();
      var missing = new List<string>();

      foreach (ConquestTrailRow row in ConquestScreenActions.Trail)
      {
        if (!row.NamesACastle) continue;
        named++;

        if (!row.Text.Contains(row.Castle, StringComparison.Ordinal))
        {
          missing.Add($"{row.Id}: its own text does not name '{row.Castle}'");
        }

        if (!castles.Contains(row.Castle)) castles.Add(row.Castle);
        else if (!castlesAgain.Contains(row.Castle)) castlesAgain.Add(row.Castle);
        if (!keys.Contains(row.CastleKey)) keys.Add(row.CastleKey);
      }

      // The install's own files, per DISTINCT castle: the painting, the castle's
      // historical map, and - what a mission's load would need - the port's
      // per-map artifacts.
      int paintings = 0;
      int maps = 0;
      int buildings = 0;
      int entities = 0;
      var counted = new List<string>();
      foreach (ConquestTrailRow row in ConquestScreenActions.Trail)
      {
        if (!row.NamesACastle || counted.Contains(row.CastleKey)) continue;
        counted.Add(row.CastleKey);

        if (File.Exists(row.PaintingPath)) paintings++;
        else missing.Add($"{row.Castle}: no painting at {row.PaintingPath}");

        if (File.Exists(Path.Combine(GameRoot, row.MapPath))) maps++;
        else missing.Add($"{row.Castle}: no map at {row.MapPath}");

        string stem = Path.GetFileNameWithoutExtension(row.MapPath).ToLowerInvariant().Replace(' ', '_');
        if (File.Exists(Path.Combine(PortArtifactsPath, $"buildings_{stem}.json"))) buildings++;
        if (File.Exists(Path.Combine(PortArtifactsPath, $"entities_{stem}.json"))) entities++;
      }

      Check(named == ConquestScreenActions.TrailCount - 1
            && castles.Count == ConquestScreenActions.CastleCount
            && castles.Count == keys.Count,
            $"the trail's eleven mission rows name their castles in their own texts "
            + $"({named} rows, {castles.Count} distinct: {string.Join(", ", castles)}"
            + (castlesAgain.Count > 0 ? " - " + string.Join(", ", castlesAgain) + " is named twice" : "")
            + ")");

      Check(paintings == castles.Count && maps == castles.Count,
            $"and every one of them has the install's own painting and its own castle map "
            + $"({paintings}/{castles.Count} ui/siege_menu/castle_*_painting.jpg, "
            + $"{maps}/{castles.Count} maps/<castle>.s2c - the install's 19 historical castle maps, all "
            + "ten of the trail's among them"
            + (missing.Count > 0 ? "; " + string.Join("; ", missing) : "") + ")");

      Line("trail screen : what a trail mission would LOAD, measured rather than assumed: the castle's "
           + $"own map (maps/<castle>.s2c - {maps}/{castles.Count} present) and its buildings artifact "
           + $"({buildings}/{castles.Count} present under sh2-port/artifacts). The ENTITY artifact is the "
           + $"gap: {entities}/{castles.Count} of the ten has one (the port's per-map entity dump has "
           + "never been run over the historical castle maps - 1 of the install's 19 .s2c maps has one, "
           + "Fenis), and the port's map loader places actors from it - so a trail mission's load is a "
           + "later item, named here.");
    }

    /// <summary>
    /// The trail screen's art, against the install's own files by PIXELS as well
    /// as by name: the composed backdrop, the position's own progress picture,
    /// the rect the picture is drawn in (which must be the rect the backdrop
    /// itself carries a copy of it at - measured by subimage match at x 209,
    /// y 114 of the 1024x768 art), and the two paging buttons, which must stand
    /// on the tiles the backdrop paints for them.
    /// </summary>
    private static void CheckConquestScreenArt()
    {
      CheckSpriteMatchesInstallFile("ConquestScreenUIBackground", UIAssets.ConquestTrailBackgroundTexture,
                                    "the trail screen's backdrop");
      CheckSpriteMatchesInstallFile("ConquestScreenUIOverlay",
                                    UIAssets.ConquestTrailOverlayTexture(ConquestScreenActions.Position),
                                    "the trail's own progress picture");
      CheckOverlayStandsOnTheBackdropsOwnCopy();
      CheckArrowsStandOnThePaintedTiles();
    }

    /// <summary>
    /// One of the trail screen's images against the install's own file: the
    /// sprite's texture is that file by name and by size, and - over a 4x4 grid
    /// of 9x9-block means - by pixels, with the briefing screen's own bar (a
    /// blank or other picture misses it by orders of magnitude; JPEG and the
    /// GPU's own filtering move a channel by a little).
    /// </summary>
    private static void CheckSpriteMatchesInstallFile(string objectName, string filePath, string what)
    {
      GameObject found = GameObject.Find(objectName);
      UnityEngine.UI.Image image = found != null ? found.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D drawn = image != null && image.sprite != null ? image.sprite.texture : null;
      if (drawn == null)
      {
        Fail($"{what} has no sprite on the screen to compare with the install's file "
             + $"({filePath})");
        return;
      }
      if (!File.Exists(filePath))
      {
        Fail($"the install ships no file at {filePath}");
        return;
      }

      // Unity's ImageConversion.LoadImage does NOT read BMP (the campaign
      // screen's own trap), and the map request screen's mode picture IS one
      // (mapselect_custom.bmp) - so .bmp files go through the project's own
      // reader, the same dispatch SpriteLoader makes. TGA is the same trap met
      // from the other side (Unity's loader does not read it either, and the
      // scenario lobby's game-type buttons ARE .tga), so .tga goes through the
      // project's own TGA reader - again the dispatch SpriteLoader makes.
      Texture2D reference;
      bool loaded;
      if (filePath.EndsWith(".bmp"))
      {
        try
        {
          reference = BMPLoader.LoadBMP(filePath);
          loaded = reference != null;
        }
        catch (Exception error)
        {
          reference = null;
          loaded = false;
          Line($"note         : the install's own .bmp reader failed on {filePath}: {error.Message}");
        }
      }
      else if (filePath.EndsWith(".tga"))
      {
        try
        {
          reference = TGALoader.LoadTGA(filePath);
          loaded = reference != null;
        }
        catch (Exception error)
        {
          reference = null;
          loaded = false;
          Line($"note         : the install's own .tga reader failed on {filePath}: {error.Message}");
        }
      }
      else
      {
        reference = new Texture2D(2, 2, TextureFormat.ARGB32, false);
        loaded = ImageConversion.LoadImage(reference, File.ReadAllBytes(filePath));
      }
      Check(loaded, $"{what}: the install's own file loads ({Path.GetFileName(filePath)}, "
                    + $"{reference?.width}x{reference?.height})");
      if (!loaded) return;

      Check(drawn.name == Path.GetFileName(filePath) && drawn.width == reference.width
            && drawn.height == reference.height,
            $"{what} IS that file on the screen, by name and by size ('{drawn.name}', "
            + $"{drawn.width}x{drawn.height})");

      double total = 0;
      int samples = 0;
      for (int gy = 0; gy < 4; gy++)
      {
        for (int gx = 0; gx < 4; gx++)
        {
          total += MeanBlockDifference(drawn, reference, (gx + 0.5f) / 4f, (gy + 0.5f) / 4f);
          samples++;
        }
      }
      double mean = samples == 0 ? 1 : total / samples;
      Check(mean < 0.08,
            $"and its pixels on the screen are that picture's ({samples} grid samples, mean absolute "
            + $"channel difference {mean:F4} of 1.0)");

      reference.name = what + " reference";
      UnityEngine.Object.Destroy(reference);
    }

    /// <summary>
    /// The rect the trail's progress picture is drawn in: the install's own
    /// backdrop carries a copy of that picture at the rect the shipped screen
    /// draws it in (558x357 at x 209, y 114 of the 1024x768 art - measured by
    /// subimage match), so the runner reads the backdrop's art back and compares
    /// the screen's rect, in the art's own pixels, with the picture's own. Drawn
    /// anywhere else, what lies under the rect is the map's plain parchment and
    /// this misses by far.
    /// </summary>
    private static void CheckOverlayStandsOnTheBackdropsOwnCopy()
    {
      GameObject overlayObject = GameObject.Find("ConquestScreenUIOverlay");
      RectTransform rect = overlayObject != null ? overlayObject.GetComponent<RectTransform>() : null;
      GameObject backgroundObject = GameObject.Find("ConquestScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = backgroundObject != null
        ? backgroundObject.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D art = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      string overlayPath = UIAssets.ConquestTrailOverlayTexture(ConquestScreenActions.Position);

      if (rect == null || art == null || !File.Exists(overlayPath))
      {
        Fail("the trail screen has no overlay rect or no backdrop art to judge the picture's place "
             + $"against ({overlayPath})");
        return;
      }

      var reference = new Texture2D(2, 2, TextureFormat.ARGB32, false);
      bool loaded = ImageConversion.LoadImage(reference, File.ReadAllBytes(overlayPath));
      if (!loaded)
      {
        Fail($"the install's own progress picture does not read back ({overlayPath})");
        return;
      }

      ArtRectOf(rect, art, out int left, out int top, out int widthPx, out int heightPx);
      double total = 0;
      int samples = 0;
      for (int gy = 0; gy < 4; gy++)
      {
        for (int gx = 0; gx < 4; gx++)
        {
          float u = (gx + 0.5f) / 4f;
          float v = (gy + 0.5f) / 4f;
          int ax = Mathf.Clamp(left + Mathf.RoundToInt(u * widthPx), 0, art.width - 1);
          int ayFromTop = Mathf.Clamp(top + Mathf.RoundToInt(v * heightPx), 0, art.height - 1);
          // Both textures read bottom-up: the art's rect is measured from the
          // picture's top, the pixels' own origin is its bottom.
          Color artColour = art.GetPixel(ax, art.height - 1 - ayFromTop);
          int rx = Mathf.Clamp(Mathf.RoundToInt(u * reference.width), 0, reference.width - 1);
          int ry = Mathf.Clamp(Mathf.RoundToInt(v * reference.height), 0, reference.height - 1);
          Color referenceColour = reference.GetPixel(rx, ry);
          total += Math.Abs(artColour.r - referenceColour.r)
                 + Math.Abs(artColour.g - referenceColour.g)
                 + Math.Abs(artColour.b - referenceColour.b);
          samples += 3;
        }
      }

      double mean = samples == 0 ? 1 : total / samples;
      Check(mean < 0.08,
            "and the rect it is drawn in is the rect the INSTALL's own backdrop carries it at "
            + $"(x {left}, y {top}, {widthPx}x{heightPx} of the {art.width}x{art.height} picture, "
            + $"{samples / 3} grid samples, mean absolute channel difference {mean:F4} of 1.0 - the "
            + "shipped screen's own place for it)");

      UnityEngine.Object.Destroy(reference);
    }

    /// <summary>
    /// The two paging buttons against the tiles the backdrop PAINTS for them: the
    /// install's own picture carries the very same 52x52 arrow art at x 732 and
    /// x 782, y 507 (template-matched), and those tiles' interiors are the only
    /// green in that part of the picture - the map around them is brown leather.
    /// Each button's rect must therefore hold the tiles' green, against the same
    /// rect shifted away from the tile, which is what fails if a button is drawn
    /// anywhere but on its own tile.
    /// </summary>
    private static void CheckArrowsStandOnThePaintedTiles()
    {
      GameObject backgroundObject = GameObject.Find("ConquestScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = backgroundObject != null
        ? backgroundObject.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D art = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      if (art == null)
      {
        Fail("the trail screen's backdrop art is not on the screen, so its painted paging tiles "
             + "cannot be judged");
        return;
      }

      var rows = new[] { ConquestScreenActions.PreviousCastle, ConquestScreenActions.NextCastle };
      var shifts = new[] { -60, 60 };
      var offTile = new List<string>();
      int onTheirTile = 0;

      for (int index = 0; index < rows.Length; index++)
      {
        GameObject buttonObject = GameObject.Find(rows[index].ButtonName);
        RectTransform rect = buttonObject != null ? buttonObject.GetComponent<RectTransform>() : null;
        if (rect == null)
        {
          Fail($"the trail screen's {rows[index].ButtonName} is not on the screen, so its place on "
               + "the art's painted paging tile could not be judged");
          continue;
        }

        ArtRectOf(rect, art, out int left, out int top, out int widthPx, out int heightPx);
        int onTile = CountGreenPixels(art, left, top, widthPx, heightPx);
        int beside = CountGreenPixels(art, left + shifts[index], top, widthPx, heightPx);
        if (onTile > 4 * Math.Max(beside, 20))
        {
          onTheirTile++;
        }
        else
        {
          offTile.Add($"{rows[index].ButtonName}: on-tile {onTile} vs beside {beside} green px");
        }
      }

      Check(onTheirTile == rows.Length,
            "and its two paging buttons stand on the tiles the INSTALL's own backdrop paints for "
            + $"them - the only green in that part of the picture ({onTheirTile}/{rows.Length} buttons "
            + "on their own tile's green"
            + (offTile.Count > 0 ? "; " + string.Join("; ", offTile) : "") + ")");
    }

    /// <summary>The green-dominant pixels of one rect of a picture (the painted paging tiles' interiors).</summary>
    private static int CountGreenPixels(Texture2D art, int left, int topFromTop, int width, int height)
    {
      int green = 0;
      for (int y = topFromTop; y < topFromTop + height; y++)
      {
        if (y < 0 || y >= art.height) continue;
        for (int x = left; x < left + width; x++)
        {
          if (x < 0 || x >= art.width) continue;
          Color pixel = art.GetPixel(x, art.height - 1 - y);
          if (pixel.g > pixel.r + 0.02f && pixel.g > pixel.b + 0.04f) green++;
        }
      }
      return green;
    }

    /// <summary>
    /// A scene rect's own place in one of the trail screen's 1024x768 pictures:
    /// the content rect is the art's own 4:3, so the fractions map straight onto
    /// it (the picture's y runs down from its top, the rect's up from its
    /// centre).
    /// </summary>
    private static void ArtRectOf(RectTransform rect, Texture2D art,
                                  out int left, out int topFromTop, out int widthPx, out int heightPx)
    {
      float contentWidth = Screen.height * (4f / 3f);
      float contentHeight = Screen.height;
      float centreX = 0.5f + rect.anchoredPosition.x / contentWidth;
      float centreY = 0.5f - rect.anchoredPosition.y / contentHeight;
      widthPx = Mathf.RoundToInt(rect.sizeDelta.x / contentWidth * art.width);
      heightPx = Mathf.RoundToInt(rect.sizeDelta.y / contentHeight * art.height);
      left = Mathf.RoundToInt(centreX * art.width) - widthPx / 2;
      topFromTop = Mathf.RoundToInt(centreY * art.height) - heightPx / 2;
    }

    /// <summary>
    /// The trail's real input surface, walked with the buttons' own pointer
    /// clicks. Every position is reached by paging (eleven Next clicks take the
    /// trail from its first castle to its end), each click is the screen's ONE
    /// entry point recording its own row, and each position's own shipped text
    /// and its own progress picture are read off the screen when it is reached.
    /// Then the two ends: at the trail's end a Next click dispatches its row,
    /// names the install's own disabled wording for that end and moves nothing;
    /// at the trail's start a Previous click does the same with the install's own
    /// 'You are at the start of the trail.', while every Previous click between
    /// them walks the trail back. The start row is clicked at both ends too: where
    /// a castle waits its click names that castle's own historical map as not
    /// built, and at the trail's end it wears the shipped restart row.
    /// </summary>
    private static void CheckConquestPaging()
    {
      int startPosition = ConquestScreenActions.Position;
      int startDispatches = ConquestScreenActions.DispatchCount;
      int clicks = 0;
      int dispatchedTheirOwnRow = 0;
      var wrongState = new List<string>();

      // Position 1 as the screen opens: its own row's text and picture are the
      // first of the twelve states the walk judges.
      ConquestTrailRow firstRow = ConquestScreenActions.TrailRow(1);
      if (!(ConquestScreenActions.Position == 1 && ShownTrailText() == firstRow.Text
            && OverlayTextureName() == Path.GetFileName(UIAssets.ConquestTrailOverlayTexture(1))))
      {
        wrongState.Add($"position 1: text or picture is not {firstRow.Id} "
                       + $"(picture '{OverlayTextureName()}')");
      }

      // Eleven Next clicks: the trail walks its whole length, and each position
      // it lands on shows its own row and its own picture.
      for (int position = 1; position < ConquestScreenActions.TrailCount; position++)
      {
        int before = ConquestScreenActions.DispatchCount;
        PressConquestButton(ConquestScreenActions.NextCastle);
        clicks++;
        if (ConquestScreenActions.DispatchCount == before + 1
            && ConquestScreenActions.LastDispatch.Id == ConquestScreenActions.NextCastle.Id)
        {
          dispatchedTheirOwnRow++;
        }

        int reached = position + 1;
        ConquestTrailRow row = ConquestScreenActions.TrailRow(reached);
        if (!(ConquestScreenActions.Position == reached
              && ShownTrailText() == row.Text
              && OverlayTextureName() == Path.GetFileName(UIAssets.ConquestTrailOverlayTexture(reached))))
        {
          wrongState.Add($"position {reached}: state={ConquestScreenActions.Position}, "
                         + $"text matches {ShownTrailText() == row.Text}, "
                         + $"picture '{OverlayTextureName()}'");
        }
      }

      Check(clicks == ConquestScreenActions.TrailCount - 1
            && dispatchedTheirOwnRow == clicks
            && ConquestScreenActions.Position == ConquestScreenActions.TrailCount,
            $"the trail's eleven Next clicks walked all {ConquestScreenActions.TrailCount} positions, each "
            + $"click dispatched through the screen's one entry point "
            + $"({dispatchedTheirOwnRow}/{clicks} click(s) dispatched {ConquestScreenActions.NextCastle.Id}, "
            + $"the trail now at position {ConquestScreenActions.Position})");
      Check(wrongState.Count == 0,
            "and every position it reached showed its own row of the install's texts and its own "
            + $"progress picture ({ConquestScreenActions.TrailCount - wrongState.Count}/"
            + $"{ConquestScreenActions.TrailCount} positions"
            + (wrongState.Count > 0 ? "; " + string.Join("; ", wrongState) : "") + ")");

      // The trail's END: the start row wears the shipped restart row, and the
      // arrow that points past the last castle names the install's own wording.
      ConquestScreenAction startAtEnd = ConquestScreenActions.StartRowFor(ConquestScreenActions.Position);
      Check(startAtEnd.Id == ConquestScreenActions.StartAgain.Id
            && LabelTextOf(ConquestScreenActions.StartMission.ButtonName) == startAtEnd.Label,
            $"at the trail's end the start row wears the shipped {ConquestScreenActions.StartAgain.Id} "
            + $"('{Shorten(startAtEnd.Label)}')");

      int atEnd = ConquestScreenActions.DispatchCount;
      PressConquestButton(ConquestScreenActions.StartAgain);
      Check(ConquestScreenActions.DispatchCount == atEnd + 1
            && ConquestScreenActions.LastDispatch.Id == ConquestScreenActions.StartAgain.Id
            && ReportHas($"{ConquestScreenActions.StartAgain.Id} "
                         + $"'{ConquestScreenActions.StartAgain.Label}'")
            && ConquestScreenActions.Position == ConquestScreenActions.TrailCount,
            "and its click records the shipped restart row through the one entry point and starts "
            + $"nothing, because the port has no trail progress to start again (still at position "
            + $"{ConquestScreenActions.Position})");

      int endDispatches = ConquestScreenActions.DispatchCount;
      PressConquestButton(ConquestScreenActions.NextCastle);
      Check(ConquestScreenActions.DispatchCount == endDispatches + 1
            && ConquestScreenActions.LastDispatch.Id == ConquestScreenActions.NextCastle.Id
            && ReportHas($"{ConquestScreenActions.NextCastle.DisabledId} "
                         + $"'{ConquestScreenActions.NextCastle.DisabledText}'")
            && ConquestScreenActions.Position == ConquestScreenActions.TrailCount,
            $"where there is no further castle along the trail the Next click names the install's own "
            + $"{ConquestScreenActions.NextCastle.DisabledId} "
            + $"('{ConquestScreenActions.NextCastle.DisabledText}') and moves nothing");

      // The walk back: eleven Previous clicks take the trail to its start, each
      // one its own row, and the last one names the install's own wording for the
      // trail's start instead of moving.
      int backClicks = 0;
      int backDispatched = 0;
      for (int position = ConquestScreenActions.TrailCount; position > 1; position--)
      {
        int before = ConquestScreenActions.DispatchCount;
        PressConquestButton(ConquestScreenActions.PreviousCastle);
        backClicks++;
        if (ConquestScreenActions.DispatchCount == before + 1
            && ConquestScreenActions.LastDispatch.Id == ConquestScreenActions.PreviousCastle.Id)
        {
          backDispatched++;
        }
      }

      ConquestTrailRow walkedBackTo = ConquestScreenActions.TrailRow(1);
      Check(backClicks == ConquestScreenActions.TrailCount - 1
            && backDispatched == backClicks
            && ConquestScreenActions.Position == 1
            && ShownTrailText() == walkedBackTo.Text
            && OverlayTextureName() == Path.GetFileName(UIAssets.ConquestTrailOverlayTexture(1)),
            $"and its eleven Previous clicks walked the trail back to its start, each one dispatched "
            + $"({backDispatched}/{backClicks}), the screen showing {walkedBackTo.Id} and its own picture again");

      int atStart = ConquestScreenActions.DispatchCount;
      PressConquestButton(ConquestScreenActions.PreviousCastle);
      Check(ConquestScreenActions.DispatchCount == atStart + 1
            && ConquestScreenActions.LastDispatch.Id == ConquestScreenActions.PreviousCastle.Id
            && ReportHas($"{ConquestScreenActions.PreviousCastle.DisabledId} "
                         + $"'{ConquestScreenActions.PreviousCastle.DisabledText}'")
            && ConquestScreenActions.Position == 1,
            $"at the trail's start the Previous click names the install's own "
            + $"{ConquestScreenActions.PreviousCastle.DisabledId} "
            + $"('{ConquestScreenActions.PreviousCastle.DisabledText}') and moves nothing");

      // The start row where a castle waits: its click names the selected castle's
      // own historical map - what a mission would load - and starts nothing.
      int atStartRow = ConquestScreenActions.DispatchCount;
      ConquestTrailRow selected = ConquestScreenActions.Selected;
      PressConquestButton(ConquestScreenActions.StartMission);
      Check(ConquestScreenActions.DispatchCount == atStartRow + 1
            && ConquestScreenActions.LastDispatch.Id == ConquestScreenActions.StartMission.Id
            && ReportHas($"{selected.Castle}'s own mission ({selected.MapPath}")
            && ReportHas("is not built")
            && ConquestScreenActions.Position == 1,
            $"and the start row's own click names what it would load - {selected.Castle}'s own mission, "
            + $"{selected.MapPath}, the install's historical castle map - and that the port does not load "
            + "it yet, rather than doing nothing");

      Line($"trail screen : the paging walk: {ConquestScreenActions.DispatchCount - startDispatches} "
           + $"clicks through the screen's one entry point, from position {startPosition} along all "
           + $"{ConquestScreenActions.TrailCount} and back to {ConquestScreenActions.Position}; the trail "
           + "opens at its first castle because the port has no progress, and the shipped forward "
           + "disabled row ('furthest point along the trail') is worn at the trail's own end.");
    }

    /// <summary>One trail button's pointer click (the same call the input module makes on release).</summary>
    private static void PressConquestButton(ConquestScreenAction row)
    {
      GameObject buttonObject = GameObject.Find(row.ButtonName);
      Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
      if (button == null)
      {
        Fail($"the trail screen's {row.Label} button ({row.ButtonName}) is not on the screen, so its "
             + "click could not be made");
        return;
      }
      PressButton(button);
    }

    /// <summary>The trail text the screen is showing (the panel's own label).</summary>
    private static string ShownTrailText() => LabelTextOf("ConquestScreenUIText");

    /// <summary>The progress picture the screen is showing, by its own file name.</summary>
    private static string OverlayTextureName()
    {
      GameObject overlay = GameObject.Find("ConquestScreenUIOverlay");
      UnityEngine.UI.Image image = overlay != null ? overlay.GetComponent<UnityEngine.UI.Image>() : null;
      return image != null && image.sprite != null && image.sprite.texture != null
        ? image.sprite.texture.name : "<none>";
    }

    // ------------------------------------------------------------------
    // the siege selection screen (the war screen's third branch), judged on
    // itself, against the install's own castle list, and against its art
    // ------------------------------------------------------------------

    /// <summary>
    /// The siege selection screen, once the war screen's Siege click has opened
    /// it: its own content (its own canvas above the war screen's, the install's
    /// own composed backdrop, its own aspect-ratio bars, the selected castle's
    /// painting and layout picture in the two slots the backdrop paints, its name
    /// and description in the panel along the bottom, and its twenty-two castle
    /// rows down the right-hand panel), its rows against the install's table, the
    /// castle list's file pairing measured rather than assumed, the art by pixels,
    /// the capture, and then the screen's own selection walk: all twenty-two rows
    /// clicked through the screen's one entry point, each judged to show its own
    /// castle's painting, layout and description, and the 'Setup' row's click
    /// naming what it would open. Its own Previous Menu, the click that goes back
    /// to the WAR SCREEN, is made after the walk.
    /// </summary>
    private static void TickSiegeScreen()
    {
      if (Expired(PlayScreenBudgetSeconds, "the siege selection screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      // The click only ASKS for the screen; the flow shows it on its own next
      // Update - the same one-frame hand-over every screen here has.
      if (game.State != GameState.SIEGE_SCREEN || GameObject.Find("SiegeScreenUI") == null) return;
      NoteFrontendState(game.State);

      if (!siegeScreenChecked)
      {
        siegeScreenChecked = true;

        Check(ReportHas("Game flow: WAR_SCREEN -> SIEGE_SCREEN."),
              $"the war screen's Siege click moved the flow to the siege selection screen "
              + $"(the console logged WAR_SCREEN -> SIEGE_SCREEN, state={game.State})");
        Check(GameObject.Find("WarScreenUI") == null,
              "and the war screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckSiegeScreenContent();
        CheckSiegeStringsAgainstInstall();
        CheckSiegeCastlePairing();
        CheckSiegeScreenArt();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(SiegeScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {SiegeScreenCapturePath}");
        return;
      }

      if (!siegeCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        siegeCaptureChecked = true;
        CheckPathScreenCapture(SiegeScreenCapturePath, SiegeScreenCanvasCapturePath,
                               "SiegeScreenUI", "siege selection");

        // The selection walk comes AFTER the capture, so what the capture shows is
        // the screen as the shipped game opens it (its first castle of the table's
        // own list) rather than the state the last click left behind.
        CheckSiegeSelection();
      }

      ClickDepartingRow("siege", "SiegeScreenActions",
                        SiegeScreenActions.Back.ButtonName, SiegeScreenActions.Back.Id,
                        SiegeScreenActions.Back.Label, SiegeScreenActions.Back.Outcome,
                        "way back", () => SiegeScreenActions.DispatchCount,
                        () => SiegeScreenActions.LastDispatch.Id, Phase.BackToWarFromSiege);
    }

    /// <summary>
    /// What the siege selection screen carries, read off the screen itself: its
    /// own canvas above the war screen's, the install's own composed picture of
    /// the screen as the backdrop (ui/siege_menu/siege_menu_background.png,
    /// 1024x768 - the two empty picture frames, the two empty leather panels and
    /// the stripped border), the selected castle's own painting and layout picture
    /// in the two frames the backdrop paints for them, that castle's own shipped
    /// name row and description prose in the panel along the bottom, NO title (the
    /// shipped table names no SIEGESELECTIONSCREEN title row, so the port invents
    /// none), its own aspect-ratio bars, and its twenty-four buttons - the
    /// twenty-two castle rows of the table's own list, the 'Setup' row with the
    /// install's own button art, and the way back with the screen's own exit
    /// medallion.
    /// </summary>
    private static void CheckSiegeScreenContent()
    {
      GameObject screen = GameObject.Find("SiegeScreenUI");
      Check(screen != null, "the siege selection screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1300,
            "it is its own canvas above the war screen's "
            + $"(sorting order {canvas?.sortingOrder}, the war screen's is 1300)");

      GameObject background = GameObject.Find("SiegeScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.SiegeMenuBackgroundTexture)
            && backgroundTexture.width == SiegeArtWidth
            && backgroundTexture.height == SiegeArtHeight,
            $"its backdrop is the install's own composed picture of this screen "
            + $"('{backgroundTexture?.name}' = {Path.GetFileName(UIAssets.SiegeMenuBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height} - the two empty frames the "
            + "selected castle's pictures go in, the two empty panels, and the border, as the "
            + "install ships them)");

      SiegeCastleRow opening = SiegeScreenActions.Selected;
      GameObject openingPainting = GameObject.Find("SiegeScreenUIPainting");
      UnityEngine.UI.Image openingPaintingImage = openingPainting != null
        ? openingPainting.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D openingPaintingTexture = openingPaintingImage != null && openingPaintingImage.sprite != null
        ? openingPaintingImage.sprite.texture : null;
      GameObject openingLayout = GameObject.Find("SiegeScreenUILayout");
      UnityEngine.UI.Image openingLayoutImage = openingLayout != null
        ? openingLayout.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D openingLayoutTexture = openingLayoutImage != null && openingLayoutImage.sprite != null
        ? openingLayoutImage.sprite.texture : null;
      Check(ShownSiegePaintingName() == Path.GetFileName(opening.PaintingPath)
            && openingPaintingTexture != null
            && openingPaintingTexture.width == SiegePaintingWidth
            && openingPaintingTexture.height == SiegePaintingHeight
            && ShownSiegeLayoutName() == Path.GetFileName(opening.LayoutPath)
            && openingLayoutTexture != null
            && openingLayoutTexture.width == SiegeLayoutWidth
            && openingLayoutTexture.height == SiegeLayoutHeight,
            $"and it opens showing the table's FIRST castle's own pictures "
            + $"({opening.Name}: '{ShownSiegePaintingName()}' "
            + $"{openingPaintingTexture?.width}x{openingPaintingTexture?.height} in the painting's frame, "
            + $"'{ShownSiegeLayoutName()}' {openingLayoutTexture?.width}x{openingLayoutTexture?.height} in "
            + "the layout's)");
      Check(ShownSiegeName() == opening.Name && ShownSiegeDescription() == opening.Description,
            "and the panel along the bottom carries that castle's own shipped name row and description "
            + $"({opening.NameId} '{opening.Name}' and {opening.DescriptionId}, {opening.Description.Length} "
            + "characters of the install's prose)");

      Check(GameObject.Find("SiegeScreenUITitle") == null,
            "and it draws no title: the shipped table has no SIEGESELECTIONSCREEN title row "
            + "(the screen's own content is the install's castle list)");

      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the shipped stone "
            + $"and not the scene behind the screen ({barSprites}/2 bars with their stone sprite)");

      int onScreen = 0;
      var wrong = new List<string>();
      for (int i = 0; i < SiegeScreenActions.CastleCount; i++)
      {
        SiegeScreenAction row = SiegeScreenActions.SelectRow(i);
        GameObject buttonObject = GameObject.Find(row.ButtonName);
        Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
        string label = buttonObject != null ? LabelTextOf(row.ButtonName) : null;
        if (button != null) onScreen++;
        if (button == null || label != row.Label)
        {
          wrong.Add($"{row.ButtonName} label='{label ?? "<none>"}' expected='{row.Label}'");
        }
      }
      Check(onScreen == SiegeScreenActions.CastleCount && wrong.Count == 0,
            $"and the install's own castle list stands down its right-hand panel, one button per "
            + $"castle, each wearing that castle's own shipped name row "
            + $"({onScreen - wrong.Count}/{SiegeScreenActions.CastleCount} buttons with the table's own "
            + "names" + (wrong.Count > 0 ? "; " + string.Join("; ", wrong) : "") + ")");

      GameObject setupObject = GameObject.Find(SiegeScreenActions.Setup.ButtonName);
      UnityEngine.UI.Image setupImage = setupObject != null
        ? setupObject.GetComponent<UnityEngine.UI.Image>() : null;
      Check(setupImage != null && setupImage.sprite != null && setupImage.sprite.texture != null
            && setupImage.sprite.texture.name == Path.GetFileName(UIAssets.SiegeSetupButtonTexture)
            && LabelTextOf(SiegeScreenActions.Setup.ButtonName) == SiegeScreenActions.Setup.Label,
            $"its 'Setup' row wears the install's own button picture from this screen's folder "
            + $"('{setupImage?.sprite?.texture?.name}', {SiegeScreenActions.Setup.Id} "
            + $"'{SiegeScreenActions.Setup.Label}')");
      CheckBackButton("siege selection", SiegeScreenActions.Back.ButtonName,
                      SiegeScreenActions.Back.Label, SiegeScreenActions.Back.Id);
      GameObject backObject = GameObject.Find(SiegeScreenActions.Back.ButtonName);
      UnityEngine.UI.Image backImage = backObject != null
        ? backObject.GetComponent<UnityEngine.UI.Image>() : null;
      Check(backImage != null && backImage.sprite != null && backImage.sprite.texture != null
            && backImage.sprite.texture.name == Path.GetFileName(UIAssets.SiegeExitButtonTexture),
            "and its way back wears THIS screen's own exit art - the install's compass-rose medallion "
            + $"from ui/siege_menu/ ('{backImage?.sprite?.texture?.name}' = "
            + $"{Path.GetFileName(UIAssets.SiegeExitButtonTexture)}) - where the trail had no exit art "
            + "of its own and wore the frontend's fancy button");

      Line("siege screen : the three things the install does not pin, said rather than faked: the "
           + "backdrop paints NO slot for the two buttons, so both stand in the free brown band below "
           + "the description panel which the shipped picture leaves empty "
           + $"({Path.GetFileName(UIAssets.SiegeSetupButtonTexture)}, 266x57, and "
           + $"{Path.GetFileName(UIAssets.SiegeExitButtonTexture)}, 58x58); the way back's four shipped "
           + "words stand UNDER its 58 px medallion, where they can be read; and the folder's other "
           + $"pieces are carried, not drawn - the blank plates at the two slots' own sizes "
           + $"({Path.GetFileName(UIAssets.SiegePaintingPlateTexture)} 425x313, "
           + $"{Path.GetFileName(UIAssets.SiegeLayoutPlateTexture)} 144x161), the two plates for slots "
           + $"this backdrop does not paint ({Path.GetFileName(UIAssets.SiegeMapPlateTexture)} 144x144, "
           + $"{Path.GetFileName(UIAssets.SiegeLocationPlateTexture)} 82x90), a framed box "
           + $"({Path.GetFileName(UIAssets.SiegeTextboxTexture)} 363x167), foliage "
           + $"({Path.GetFileName(UIAssets.SiegeIvyTexture)} 248x238), and the SIEGE SETUP screen's own "
           + $"four choice pictures ({Path.GetFileName(UIAssets.SiegeSetupPreviewTexture)} 1024x768, "
           + "choice_attack_off/on.jpg, choice_defend_off/on.jpg 363x407) - the screen behind this "
           + "one, whose rows the table names (SIEGESETUPSCREENSTATICTOOLTIP_ATTACK/DEFEND/EXIT).");
    }

    /// <summary>
    /// The siege selection screen's rows against the install, read at run time:
    /// the screen's own four rows (SIEGESELECTIONSCREEN_SETUP 'Setup' with the
    /// tooltip the table ships beside it, SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP
    /// 'Continue to the Siege'; the tooltip a castle in the list shows,
    /// SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME 'Select this Siege'; and the
    /// screen's own way back, SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT 'Previous
    /// Menu') and the castle list itself - the table's HISTORICCASTLE00..43, an
    /// even-indexed NAME row and the odd-indexed DESCRIPTION prose beside it, so
    /// forty-four pairings for the twenty-two castles.
    /// </summary>
    private static void CheckSiegeStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      int pairs = 0;

      foreach (SiegeCastleRow castle in SiegeScreenActions.Castles)
      {
        pairs += 2;
        if (!(shipped.TryGetValue(castle.NameId, out string name) && name == castle.Name))
        {
          mismatches.Add($"{castle.NameId} shipped="
                         + $"{(shipped.TryGetValue(castle.NameId, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port='{castle.Name}'");
        }
        if (!(shipped.TryGetValue(castle.DescriptionId, out string description)
              && description == castle.Description))
        {
          mismatches.Add($"{castle.DescriptionId} shipped="
                         + $"{(shipped.TryGetValue(castle.DescriptionId, out string found) ? $"'{Shorten(found)}'" : "<no such row>")}"
                         + $" port='{Shorten(castle.Description)}'");
        }
      }

      pairs += 3;
      if (!(shipped.TryGetValue(SiegeScreenActions.Setup.Id, out string setup)
            && setup == SiegeScreenActions.Setup.Label))
      {
        mismatches.Add($"{SiegeScreenActions.Setup.Id} shipped="
                       + $"{(shipped.TryGetValue(SiegeScreenActions.Setup.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{SiegeScreenActions.Setup.Label}'");
      }
      if (!(shipped.TryGetValue("SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP", out string setupTooltip)
            && setupTooltip == SiegeScreenActions.Setup.Tooltip))
      {
        mismatches.Add("SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP shipped="
                       + $"{(shipped.TryGetValue("SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP", out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port-tooltip='{SiegeScreenActions.Setup.Tooltip}'");
      }
      if (!(shipped.TryGetValue(SiegeScreenActions.SiegeNameId, out string siegeName)
            && siegeName == SiegeScreenActions.SiegeNameTooltip))
      {
        mismatches.Add($"{SiegeScreenActions.SiegeNameId} shipped="
                       + $"{(shipped.TryGetValue(SiegeScreenActions.SiegeNameId, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port-tooltip='{SiegeScreenActions.SiegeNameTooltip}'");
      }

      pairs++;
      if (!(shipped.TryGetValue(SiegeScreenActions.Back.Id, out string exit)
            && exit == SiegeScreenActions.Back.Label))
      {
        mismatches.Add($"{SiegeScreenActions.Back.Id} shipped="
                       + $"{(shipped.TryGetValue(SiegeScreenActions.Back.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{SiegeScreenActions.Back.Label}'");
      }

      Check(SiegeScreenActions.CastleCount == 22 && mismatches.Count == 0 && shipped.Count > 0,
            $"the siege selection screen's castle list and its own {pairs - SiegeScreenActions.CastleCount * 2} "
            + $"rows are the shipped table's own "
            + $"({pairs - mismatches.Count}/{pairs} HISTORICCASTLE*/SIEGESELECTIONSCREEN* rows agree with "
            + $"i18n/en-us.xml, {shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");

      Line($"siege screen : the screen's own rows: {SiegeScreenActions.Setup.Id} "
           + $"'{SiegeScreenActions.Setup.Label}' (tooltip SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP "
           + $"'{SiegeScreenActions.Setup.Tooltip}'), {SiegeScreenActions.SiegeNameId} "
           + $"'{SiegeScreenActions.SiegeNameTooltip}' (what a castle in the list shows), and the way "
           + $"back {SiegeScreenActions.Back.Id} '{SiegeScreenActions.Back.Label}' - the screen ships its "
           + "own exit row AND its own exit art, unlike the trail.");
      Line($"siege screen : the castle list, in the table's own order ({SiegeScreenActions.CastleCount} "
           + $"castles): {string.Join(", ", CastleNames())}");
    }

    /// <summary>Every castle's name, in the table's own order (for the report's own line).</summary>
    private static string[] CastleNames()
    {
      var names = new string[SiegeScreenActions.CastleCount];
      for (int i = 0; i < names.Length; i++) names[i] = SiegeScreenActions.Castle(i).Name;
      return names;
    }

    /// <summary>
    /// The castle list's pairing against the install's own files - measured, not
    /// assumed. Every one of the twenty-two castles must have the install's own
    /// painting and layout picture, whose file NAME is not the table's own
    /// spelling in four cases (Koblenz-Stolzenfels's files are named for the
    /// castle's own name, stolzenfels; Tower of London and Castell y Bere run the
    /// words together; Fougères's files drop the accent) - so the port carries the
    /// key it measured, and this check re-runs the mechanical rule (lowercase,
    /// drop accents, drop everything that is not a letter or digit) to say exactly
    /// where the rule would have been wrong. Then what a siege would LOAD: the
    /// install's own historical scenario maps/<castle>.s2c, which eighteen of the
    /// twenty-two have and four do not (Leeds, Caerphilly, Meersburg, Bodiam) -
    /// and the port's per-map artifacts, which are the gap that keeps starting a
    /// siege a later item.
    /// </summary>
    private static void CheckSiegeCastlePairing()
    {
      var missing = new List<string>();
      var noMap = new List<string>();
      var ruleMissed = new List<string>();
      int paintings = 0;
      int layouts = 0;
      int maps = 0;
      int buildings = 0;
      int entities = 0;

      foreach (SiegeCastleRow castle in SiegeScreenActions.Castles)
      {
        if (File.Exists(castle.PaintingPath)) paintings++;
        else missing.Add($"{castle.Name}: no painting at {castle.PaintingPath}");

        if (File.Exists(castle.LayoutPath)) layouts++;
        else missing.Add($"{castle.Name}: no layout at {castle.LayoutPath}");

        string mechanical = MechanicalCastleKey(castle.Name);
        if (mechanical != castle.FileKey)
        {
          ruleMissed.Add($"{castle.Name} -> the rule says '{mechanical}', the install's files say "
                         + $"'{castle.FileKey}'");
        }

        if (!castle.HasMap)
        {
          noMap.Add(castle.Name);
          continue;
        }

        maps++;
        if (!File.Exists(Path.Combine(GameRoot, castle.MapPath)))
        {
          missing.Add($"{castle.Name}: no map at {castle.MapPath}");
        }

        string stem = Path.GetFileNameWithoutExtension(castle.MapPath).ToLowerInvariant().Replace(' ', '_');
        if (File.Exists(Path.Combine(PortArtifactsPath, $"buildings_{stem}.json"))) buildings++;
        if (File.Exists(Path.Combine(PortArtifactsPath, $"entities_{stem}.json"))) entities++;
      }

      Check(paintings == SiegeScreenActions.CastleCount && layouts == SiegeScreenActions.CastleCount,
            $"every one of the screen's {SiegeScreenActions.CastleCount} castles has the install's own "
            + $"painting and its own layout picture, under the file key the port measured "
            + $"({paintings}/{SiegeScreenActions.CastleCount} ui/siege_menu/castle_*_painting.jpg, "
            + $"{layouts}/{SiegeScreenActions.CastleCount} ui/siege_menu/layout_*.jpg"
            + (missing.Count > 0 ? "; " + string.Join("; ", missing) : "") + ")");

      Check(ruleMissed.Count == 1,
            "and the mechanical key (lowercase, accents dropped, everything else dropped) would have "
            + $"been right for {SiegeScreenActions.CastleCount - ruleMissed.Count} of them and wrong for "
            + $"{ruleMissed.Count} - which is why the key is carried rather than derived "
            + $"({string.Join("; ", ruleMissed)})");

      Check(maps == 18 && noMap.Count == 4,
            $"and {maps} of the {SiegeScreenActions.CastleCount} have the install's own historical "
            + $"scenario (maps/<castle>.s2c, in the install's own spelling - the accent kept in "
            + "maps/Fougères.s2c and the article capitalised in maps/Castell Y Bere.s2c); the four the "
            + $"install ships none for are named rather than invented ({string.Join(", ", noMap)})");

      Line($"siege screen : what a siege would LOAD, measured rather than assumed: the castle's own "
           + $"scenario ({maps}/18 present) and its buildings artifact ({buildings}/18 present under "
           + $"sh2-port/artifacts). The ENTITY artifact is the gap, as it is for the trail: "
           + $"{entities}/18 of them have one (the port's per-map entity dump has never been run over "
           + "the historical castle maps - 1 of the install's 19 .s2c maps has one, Fenis), and the "
           + "port's map loader places actors from it - so starting a siege is a later item, named here. "
           + "The install also ships a nineteenth historical map with NO castle row and NO painting "
           + "(maps/Miskolc-Diosgyor.s2c): the shipped table's list and the folder's art agree on 22, "
           + "and this one is neither.");
    }

    /// <summary>
    /// The screen's art, against the install's own files by PIXELS as well as by
    /// name: the composed backdrop, and the two pictures the screen opens with in
    /// the frames the backdrop paints for them (the table's first castle's
    /// painting and its layout). A screen that drew the wrong castle's picture, or
    /// drew the blank plates the folder ships beside them, would miss by far.
    /// </summary>
    private static void CheckSiegeScreenArt()
    {
      CheckSpriteMatchesInstallFile("SiegeScreenUIBackground", UIAssets.SiegeMenuBackgroundTexture,
                                    "the siege selection screen's backdrop");
      CheckSpriteMatchesInstallFile("SiegeScreenUIPainting", SiegeScreenActions.Selected.PaintingPath,
                                    "the selected castle's painting");
      CheckSpriteMatchesInstallFile("SiegeScreenUILayout", SiegeScreenActions.Selected.LayoutPath,
                                    "the selected castle's layout picture");
    }

    /// <summary>
    /// The screen's real input surface: all TWENTY-TWO castle rows clicked
    /// through the screen's one entry point - each row its own button, each click
    /// dispatching its OWN row (the twenty-two share the table's one
    /// SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME row, so the record's own castle
    /// index is what says which row was clicked) - and after each one the screen
    /// must be showing that castle's own painting, its own layout picture and its
    /// own name and description. Then the selected row clicked again (it dispatches
    /// and changes nothing), and the 'Setup' row, whose click names the siege setup
    /// screen as unbuilt and leaves the selection where it was.
    /// </summary>
    private static void CheckSiegeSelection()
    {
      int startDispatches = SiegeScreenActions.DispatchCount;
      int clicks = 0;
      int dispatchedTheirOwnRow = 0;
      var wrongState = new List<string>();

      // The state the screen opens on, before any click: the table's first castle.
      SiegeCastleRow first = SiegeScreenActions.Castle(0);
      if (!(SiegeScreenActions.SelectedIndex == 0 && ShownSiegeIs(first)))
      {
        wrongState.Add($"on opening: the screen shows {ShownSiegeName()}, expected {first.Name}");
      }

      for (int i = 0; i < SiegeScreenActions.CastleCount; i++)
      {
        SiegeScreenAction row = SiegeScreenActions.SelectRow(i);
        int before = SiegeScreenActions.DispatchCount;
        PressSiegeButton(row);
        clicks++;
        if (SiegeScreenActions.DispatchCount == before + 1
            && SiegeScreenActions.LastDispatch.CastleIndex == i
            && SiegeScreenActions.LastDispatch.Id == SiegeScreenActions.SiegeNameId)
        {
          dispatchedTheirOwnRow++;
        }

        SiegeCastleRow castle = SiegeScreenActions.Castle(i);
        if (!(SiegeScreenActions.SelectedIndex == i && ShownSiegeIs(castle)))
        {
          wrongState.Add($"{castle.Name}: selection={SiegeScreenActions.SelectedIndex}, "
                         + $"painting '{ShownSiegePaintingName()}', layout '{ShownSiegeLayoutName()}', "
                         + $"name '{ShownSiegeName()}'");
        }
      }

      Check(clicks == SiegeScreenActions.CastleCount
            && dispatchedTheirOwnRow == clicks
            && SiegeScreenActions.SelectedIndex == SiegeScreenActions.CastleCount - 1,
            $"the screen's {clicks} castle rows are {clicks} real buttons: every one of them was clicked "
            + "through the screen's one entry point and each dispatched its OWN row "
            + $"({dispatchedTheirOwnRow}/{clicks}, the record's castle index read after each - the "
            + "twenty-two share the table's one SIEGENAME row, so the index is what tells them apart)");
      Check(wrongState.Count == 0,
            "and every one of them left the screen showing that castle's own painting, its own layout "
            + "picture and its own shipped name and description "
            + $"({SiegeScreenActions.CastleCount - wrongState.Count}/{SiegeScreenActions.CastleCount} "
            + "castles" + (wrongState.Count > 0 ? "; " + string.Join("; ", wrongState) : "") + ")");

      // The selected row again: it is still its own button, and a click on the
      // castle already selected dispatches and changes nothing.
      SiegeScreenAction selectedRow = SiegeScreenActions.SelectRow(SiegeScreenActions.SelectedIndex);
      int atSelected = SiegeScreenActions.DispatchCount;
      PressSiegeButton(selectedRow);
      Check(SiegeScreenActions.DispatchCount == atSelected + 1
            && SiegeScreenActions.LastDispatch.CastleIndex == selectedRow.CastleIndex
            && SiegeScreenActions.SelectedIndex == selectedRow.CastleIndex,
            $"and a second click on the castle already selected dispatches through the same one entry "
            + $"point and changes nothing (still {SiegeScreenActions.Selected.Name})");

      // The 'Setup' row: the one click that would take the selected castle into
      // the siege setup - named as unbuilt, the selection untouched.
      int atSetup = SiegeScreenActions.DispatchCount;
      PressSiegeButton(SiegeScreenActions.Setup);
      Check(SiegeScreenActions.DispatchCount == atSetup + 1
            && SiegeScreenActions.LastDispatch.Id == SiegeScreenActions.Setup.Id
            && ReportHas($"{SiegeScreenActions.Setup.Id} '{SiegeScreenActions.Setup.Label}'")
            && ReportHas("is not built")
            && SiegeScreenActions.SelectedIndex == selectedRow.CastleIndex,
            $"and the 'Setup' row's own click names what it would open - "
            + $"{SiegeScreenActions.Setup.Destination} - and that the port does not open it yet, "
            + "rather than doing nothing");

      Line($"siege screen : the selection walk: {SiegeScreenActions.DispatchCount - startDispatches} "
           + $"clicks through the screen's one entry point - all {SiegeScreenActions.CastleCount} castle "
           + "rows, one of them twice, and the 'Setup' row - the screen opening on the table's first "
           + $"castle (the port has no progress) and now standing on "
           + $"{SiegeScreenActions.Selected.NameId} '{SiegeScreenActions.Selected.Name}'.");
    }

    /// <summary>
    /// The mechanical castle key this port chose NOT to rely on: the table's own
    /// name, lowercased, its accents dropped and everything that is not a letter
    /// or a digit dropped with them. It builds the install's file key for
    /// twenty-one of the twenty-two castles - and for Koblenz-Stolzenfels it
    /// builds 'koblenzstolzenfels' where the install's files say 'stolzenfels',
    /// which is exactly why the key rides on the row instead. The accent stripping
    /// is what makes Fougères's key the rule's own work rather than a second
    /// exception.
    /// </summary>
    private static string MechanicalCastleKey(string name)
    {
      string plain = StripAccents(name).ToLowerInvariant();
      var builder = new System.Text.StringBuilder(plain.Length);
      foreach (char c in plain)
      {
        if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) builder.Append(c);
      }
      return builder.ToString();
    }

    /// <summary>One string with its accents dropped (Fougères -> Fougeres).</summary>
    private static string StripAccents(string text)
    {
      string decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
      var builder = new System.Text.StringBuilder(decomposed.Length);
      foreach (char c in decomposed)
      {
        if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
            != System.Globalization.UnicodeCategory.NonSpacingMark)
        {
          builder.Append(c);
        }
      }
      return builder.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>One castle row's pointer click (the same call the input module makes on release).</summary>
    private static void PressSiegeButton(SiegeScreenAction row)
    {
      GameObject buttonObject = GameObject.Find(row.ButtonName);
      Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
      if (button == null)
      {
        Fail($"the siege selection screen's {row.Label} button ({row.ButtonName}) is not on the screen, "
             + "so its click could not be made");
        return;
      }
      PressButton(button);
    }

    /// <summary>True when everything the screen shows is that castle's own (painting, layout, name, description).</summary>
    private static bool ShownSiegeIs(SiegeCastleRow castle) =>
      ShownSiegePaintingName() == Path.GetFileName(castle.PaintingPath)
      && ShownSiegeLayoutName() == Path.GetFileName(castle.LayoutPath)
      && ShownSiegeName() == castle.Name
      && ShownSiegeDescription() == castle.Description;

    /// <summary>The painting the screen is showing, by its own file name.</summary>
    private static string ShownSiegePaintingName() => ShownSpriteName("SiegeScreenUIPainting");

    /// <summary>The layout picture the screen is showing, by its own file name.</summary>
    private static string ShownSiegeLayoutName() => ShownSpriteName("SiegeScreenUILayout");

    /// <summary>One of the screen's images' sprite texture name, or "&lt;none&gt;".</summary>
    private static string ShownSpriteName(string objectName)
    {
      GameObject found = GameObject.Find(objectName);
      UnityEngine.UI.Image image = found != null ? found.GetComponent<UnityEngine.UI.Image>() : null;
      return image != null && image.sprite != null && image.sprite.texture != null
        ? image.sprite.texture.name : "<none>";
    }

    /// <summary>The castle name the screen's panel is showing.</summary>
    private static string ShownSiegeName() => LabelTextOf("SiegeScreenUICastleName");

    /// <summary>The description the screen's panel is showing.</summary>
    private static string ShownSiegeDescription() => LabelTextOf("SiegeScreenUIDescription");

    // ------------------------------------------------------------------
    // the map request screen (the war screen's FOURTH branch: Custom) and the
    // load screen (its FIFTH: Load), judged on themselves, against the
    // install's own files and - for the map list - against the run's own read
    // of the install's maps folder
    // ------------------------------------------------------------------

    /// <summary>
    /// The map request screen, once the war screen's Custom click has opened it:
    /// its own content (its own canvas above the war screen's, the install's own
    /// war-path backdrop from ui/map_select/, the custom mode's own 256x256
    /// picture in the window the art's variants measure, the install's own maps
    /// down the leather panel twelve to a page, the selected map's stem in the
    /// title strip, the two painted arrows, the Play row and the way back), its
    /// rows against the install's table, its map list against the run's own
    /// independent read of the install's maps folder, the art by pixels, the
    /// capture, and then the walk: rows and pages clicked through the screen's
    /// one entry point, each judged by what it moved, and the Play row's click
    /// naming the selected map and the artifact gap. Its own way back, the click
    /// that goes back to the WAR SCREEN, is made after the walk.
    /// </summary>
    private static void TickMapRequestScreen()
    {
      if (Expired(PlayScreenBudgetSeconds,
                  "the map request screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      // The click only ASKS for the screen; the flow shows it on its own next
      // Update - the same one-frame hand-over every screen here has.
      if (game.State != GameState.MAP_REQUEST_SCREEN
          || GameObject.Find("MapRequestScreenUI") == null) return;
      NoteFrontendState(game.State);

      if (!mapRequestScreenChecked)
      {
        mapRequestScreenChecked = true;

        Check(ReportHas("Game flow: WAR_SCREEN -> MAP_REQUEST_SCREEN."),
              "the war screen's Custom click moved the flow to the map request screen (the console "
              + $"logged WAR_SCREEN -> MAP_REQUEST_SCREEN, state={game.State})");
        Check(GameObject.Find("WarScreenUI") == null,
              "and the war screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckMapRequestScreenContent();
        CheckMapRequestScreenStringsAgainstInstall();
        CheckMapRequestMapListAgainstTheInstall();
        CheckMapRequestScreenArt();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(MapRequestScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {MapRequestScreenCapturePath}");
        return;
      }

      if (!mapRequestCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        mapRequestCaptureChecked = true;
        CheckPathScreenCapture(MapRequestScreenCapturePath, MapRequestScreenCanvasCapturePath,
                               "MapRequestScreenUI", "map request screen");

        // The list walk comes AFTER the capture, so what the capture shows is
        // the screen as the shipped game opens it (the install's list's first
        // page, its first map selected) rather than the state the last click
        // left behind.
        CheckMapRequestWalk();
      }

      ClickDepartingRow("map request", "MapRequestScreenActions",
                        MapRequestScreenActions.Back.ButtonName, MapRequestScreenActions.Back.Id,
                        MapRequestScreenActions.Back.Label, MapRequestScreenActions.Back.Outcome,
                        "way back", () => MapRequestScreenActions.DispatchCount,
                        () => MapRequestScreenActions.LastDispatch.Id, Phase.BackToWarFromCustom);
    }

    /// <summary>
    /// What the map request screen carries, read off the screen itself: its own
    /// canvas above the war screen's, the install's own picture of the screen
    /// for the WAR path (ui/map_select/mapselect_background_war.jpg, 1024x768 -
    /// the barbarian and the knight; the folder's other background is the peace
    /// path's), the custom mode's own 256x256 picture in the big window the
    /// folder's _big variant measures (x 525..780, y 47..302 - the picture's
    /// exact size), the selected map's own stem in the title strip, the
    /// install's own maps down the leather panel (twelve rows on the first
    /// page), its own aspect-ratio bars, and its four fixed buttons - the two
    /// painted arrows, the Play row on the install's castle-scenario play button
    /// and the way back.
    /// </summary>
    private static void CheckMapRequestScreenContent()
    {
      GameObject screen = GameObject.Find("MapRequestScreenUI");
      Check(screen != null, "the map request screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1300,
            "it is its own canvas above the war screen's "
            + $"(sorting order {canvas?.sortingOrder}, the war screen's is 1300)");

      GameObject background = GameObject.Find("MapRequestScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.MapSelectBackgroundWarTexture)
            && backgroundTexture.width == 1024 && backgroundTexture.height == 768,
            $"its backdrop is the install's own picture of this screen for the WAR path "
            + $"('{backgroundTexture?.name}' = {Path.GetFileName(UIAssets.MapSelectBackgroundWarTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height} - the barbarian and the knight; "
            + $"ui/map_select/{Path.GetFileName(UIAssets.MapSelectBackgroundTexture)} is the same screen "
            + "with the peace path's characters)");

      GameObject mode = GameObject.Find("MapRequestScreenUIMode");
      UnityEngine.UI.Image modeImage = mode != null ? mode.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D modeTexture = modeImage != null && modeImage.sprite != null
        ? modeImage.sprite.texture : null;
      Check(modeTexture != null
            && modeTexture.name == Path.GetFileName(UIAssets.MapSelectCustomModeTexture)
            && modeTexture.width == 256 && modeTexture.height == 256,
            $"and the custom mode's own 256x256 picture stands in the big window the folder's own "
            + $"_big variant measures for a map picture (x 525..780, y 47..302) "
            + $"('{modeTexture?.name}' = {Path.GetFileName(UIAssets.MapSelectCustomModeTexture)}, "
            + $"{modeTexture?.width}x{modeTexture?.height})");

      GameObject titleObject = GameObject.Find("MapRequestScreenUITitle");
      UnityEngine.UI.Text titleText = titleObject != null
        ? titleObject.GetComponent<UnityEngine.UI.Text>() : null;
      Check(titleText != null && titleText.text == MapRequestScreenActions.Selected.Stem,
            "and the title strip along the list panel's top carries the selected map's own file stem "
            + $"({MapRequestScreenActions.Selected.FileName} -> '{titleText?.text}')");

      int rows = 0;
      int labelled = 0;
      int active = 0;
      var wrong = new List<string>();
      for (int i = 0; i < MapRequestScreenActions.PageSize; i++)
      {
        MapRequestScreenAction row = MapRequestScreenActions.SelectRow(i);
        GameObject rowObject = GameObject.Find(row.ButtonName);
        Button button = rowObject != null ? rowObject.GetComponent<Button>() : null;
        if (button != null) rows++;
        if (rowObject != null && rowObject.activeSelf) active++;
        string label = rowObject != null && rowObject.activeSelf ? LabelTextOf(row.ButtonName) : null;
        if (label == row.Label) labelled++;
        else if (rowObject != null && rowObject.activeSelf)
        {
          wrong.Add($"{row.ButtonName} label='{label ?? "<none>"}' expected='{row.Label}'");
        }
      }
      Check(rows == MapRequestScreenActions.PageSize
            && active == MapRequestScreenActions.RowsOnPage
            && labelled == MapRequestScreenActions.RowsOnPage,
            $"and the install's own map list stands down the leather panel, one row per map of the "
            + $"page, each wearing its own file stem ({labelled}/{MapRequestScreenActions.RowsOnPage} "
            + $"rows of page 1 labelled, {active} on screen; the list is "
            + $"{MapRequestScreenActions.Maps.Length} maps over {MapRequestScreenActions.PageCount} "
            + "page(s)"
            + (wrong.Count > 0 ? "; " + string.Join("; ", wrong) : "") + ")");

      GameObject playObject = GameObject.Find(MapRequestScreenActions.Play.ButtonName);
      UnityEngine.UI.Image playImage = playObject != null
        ? playObject.GetComponent<UnityEngine.UI.Image>() : null;
      Check(playImage != null && playImage.sprite != null && playImage.sprite.texture != null
            && playImage.sprite.texture.name == Path.GetFileName(UIAssets.MapRequestPlayButtonTexture)
            && LabelTextOf(MapRequestScreenActions.Play.ButtonName) == MapRequestScreenActions.Play.Label,
            $"its Play row wears the install's own castle-scenario play button "
            + $"('{playImage?.sprite?.texture?.name}' = "
            + $"{Path.GetFileName(UIAssets.MapRequestPlayButtonTexture)}, the folder ships no button "
            + $"art of its own) with the shipped label ({MapRequestScreenActions.PlayId} "
            + $"'{MapRequestScreenActions.Play.Label}')");

      foreach (MapRequestScreenAction arrow in new[]
               { MapRequestScreenActions.PreviousPage, MapRequestScreenActions.NextPage })
      {
        GameObject arrowObject = GameObject.Find(arrow.ButtonName);
        Button arrowButton = arrowObject != null ? arrowObject.GetComponent<Button>() : null;
        UnityEngine.UI.Image arrowImage = arrowObject != null
          ? arrowObject.GetComponent<UnityEngine.UI.Image>() : null;
        Check(arrowButton != null && arrowImage != null
              && arrowObject.transform.Find("Label") == null,
              $"and its {(arrow.Kind == MapRequestScreenActionKind.PreviousPage ? "left" : "right")} "
              + $"painted arrow is a real button with no words drawn on it "
              + $"({arrow.ButtonName}; the install ships no row for either arrow)");
      }

      CheckBackButton("map request", MapRequestScreenActions.Back.ButtonName,
                      MapRequestScreenActions.Back.Label, MapRequestScreenActions.Back.Id);

      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the shipped stone "
            + $"and not the scene behind the screen ({barSprites}/2 bars with their stone sprite)");

      Line("map request  : the install's own structure, said rather than faked: the big black frame at "
           + "the top left is the engine's map view (carried, not drawn - this port has no "
           + "scenario-view renderer); the list's scrollbar groove is painted by the art and this "
           + "port's list pages with the two arrows instead; and the arrows themselves, the Play row's "
           + "place and picture, and the way back's row are the port's readings, named here.");
    }

    /// <summary>
    /// The map request screen's rows against the install, read at run time: the
    /// screen's one shipped row (MAPREQUESTSCREEN_PLAY 'Play') and the war
    /// screen's own exit row the way back wears (WARSCREENSTATICTOOLTIP_EXIT
    /// 'Previous menu') - the two painted arrows have NO shipped row (measured:
    /// the table's only MAPREQUEST* row is PLAY), which is said rather than
    /// papered over.
    /// </summary>
    private static void CheckMapRequestScreenStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();

      foreach ((string id, string label) in new[]
               {
                 (MapRequestScreenActions.PlayId, MapRequestScreenActions.PlayLabel),
                 (MapRequestScreenActions.Back.Id, MapRequestScreenActions.Back.Label),
               })
      {
        if (!(shipped.TryGetValue(id, out string english) && english == label))
        {
          mismatches.Add($"{id} shipped="
                         + $"{(shipped.TryGetValue(id, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port='{label}'");
        }
      }

      Check(mismatches.Count == 0
            && !shipped.ContainsKey("MAPREQUESTPREVIOUS")
            && !shipped.ContainsKey("MAPREQUESTNEXT"),
            "the map request screen's shipped rows are the table's own, with the shipped text "
            + "(2/2 MAPREQUESTSCREEN_PLAY and WARSCREENSTATICTOOLTIP_EXIT agree with "
            + $"i18n/en-us.xml, {shipped.Count} strings read; the two painted arrows have no row - the "
            + "port names them rather than inventing ids"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");
      Line($"map request  : {MapRequestScreenActions.PlayId} '{MapRequestScreenActions.PlayLabel}' - the "
           + "one row the shipped table ships for this screen; the install's own name for the screen is "
           + $"'{MapRequestScreenActions.ScreenName}' (the engine's MapRequestScreen).");
    }

    /// <summary>
    /// The screen's map list against the install, read by the RUNNER itself -
    /// the same independent-read discipline the i18n checks use: the runner
    /// lists the install's maps/*.s2m files, applies the filter the shipped
    /// screen's own code carries (the three literals measured in the exe:
    /// 'war_chapter', 'peace_chapter', 'tutorial'), groups the mirror's
    /// case-alias twins preferring the spelling that carries capitals, sorts by
    /// the stem, and compares the whole list - count, names and order - with the
    /// screen's. Then what a Play would LOAD: the port's per-map artifacts for
    /// these maps (buildings and entities under sh2-port/artifacts).
    /// </summary>
    private static void CheckMapRequestMapListAgainstTheInstall()
    {
      string mapsFolder = Path.Combine(GameRoot, "maps");
      var byLowerName = new Dictionary<string, string>(StringComparer.Ordinal);
      if (Directory.Exists(mapsFolder))
      {
        foreach (string path in Directory.GetFiles(mapsFolder, "*.s2m"))
        {
          string fileName = Path.GetFileName(path);
          string lower = fileName.ToLowerInvariant();
          if (lower.StartsWith("war_chapter") || lower.StartsWith("peace_chapter")
              || lower.StartsWith("tutorial")) continue;
          if (!byLowerName.TryGetValue(lower, out string kept)) byLowerName[lower] = fileName;
          else if (HasCapital(fileName) && !HasCapital(kept)) byLowerName[lower] = fileName;
        }
      }

      var stems = new List<string>();
      foreach (string fileName in byLowerName.Values)
      {
        stems.Add(Path.GetFileNameWithoutExtension(fileName));
      }
      stems.Sort((a, b) =>
      {
        int byStem = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return byStem != 0 ? byStem : string.CompareOrdinal(a, b);
      });

      bool same = stems.Count == MapRequestScreenActions.Maps.Length;
      var differences = new List<string>();
      for (int i = 0; same && i < stems.Count; i++)
      {
        if (stems[i] != MapRequestScreenActions.Maps[i].Stem)
        {
          same = false;
          differences.Add($"index {i}: runner='{stems[i]}' screen='{MapRequestScreenActions.Maps[i].Stem}'");
        }
      }

      Check(same && stems.Count > 0,
            "and its map list IS the install's own, as the runner's independent read of "
            + $"<install>/maps says: every *.s2m with the shipped screen's own three filters "
            + $"('{string.Join("', '", new[] { "war_chapter", "peace_chapter", "tutorial" })}*' left out, "
            + "the mirror's case-alias twins grouped to one and the install's capitalised spelling "
            + $"kept) - {stems.Count} map(s), same names and same order "
            + (differences.Count > 0 ? "; " + string.Join("; ", differences) : "") + ")");

      int buildings = 0;
      int entities = 0;
      foreach (MapRequestMap map in MapRequestScreenActions.Maps)
      {
        string stem = map.Stem.ToLowerInvariant().Replace(' ', '_');
        if (File.Exists(Path.Combine(PortArtifactsPath, $"buildings_{stem}.json"))) buildings++;
        if (File.Exists(Path.Combine(PortArtifactsPath, $"entities_{stem}.json"))) entities++;
      }

      Line($"map request  : what a custom game would LOAD, measured rather than assumed: the map's own "
           + $"file and its buildings artifact ({buildings}/{MapRequestScreenActions.Maps.Length} present "
           + $"under sh2-port/artifacts). The ENTITY artifact is the gap: "
           + $"{entities}/{MapRequestScreenActions.Maps.Length} of these maps has one, and the port's "
           + "map loader places actors from it - so starting a custom game is a later item, named here "
           + "by the Play row's own click.");
    }

    /// <summary>
    /// The map request screen's art, against the install's own files by PIXELS
    /// as well as by name: the backdrop and the mode picture, and the two
    /// painted arrows - which must stand ON the medallions the backdrop itself
    /// paints (the only green in that part of the picture), the same check the
    /// trail's paging tiles get.
    /// </summary>
    private static void CheckMapRequestScreenArt()
    {
      CheckSpriteMatchesInstallFile("MapRequestScreenUIBackground", UIAssets.MapSelectBackgroundWarTexture,
                                    "the map request screen's war-path backdrop");
      CheckSpriteMatchesInstallFile("MapRequestScreenUIMode", UIAssets.MapSelectCustomModeTexture,
                                    "the map request screen's custom-mode picture");

      GameObject backgroundObject = GameObject.Find("MapRequestScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = backgroundObject != null
        ? backgroundObject.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D art = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      if (art == null)
      {
        Fail("the map request screen's backdrop art is not on the screen, so its painted arrows "
             + "cannot be judged");
        return;
      }

      var arrows = new[] { MapRequestScreenActions.PreviousPage, MapRequestScreenActions.NextPage };
      var shifts = new[] { -70, 70 };
      int onTheirArrow = 0;
      var offArrow = new List<string>();
      for (int index = 0; index < arrows.Length; index++)
      {
        GameObject arrowObject = GameObject.Find(arrows[index].ButtonName);
        RectTransform rect = arrowObject != null ? arrowObject.GetComponent<RectTransform>() : null;
        if (rect == null)
        {
          Fail($"the map request screen's {arrows[index].ButtonName} is not on the screen, so its "
               + "place on the art's painted arrow could not be judged");
          continue;
        }

        ArtRectOf(rect, art, out int left, out int top, out int widthPx, out int heightPx);
        int onArrow = CountGreenPixels(art, left, top, widthPx, heightPx);
        int beside = CountGreenPixels(art, left + shifts[index], top, widthPx, heightPx);
        if (onArrow > 4 * Math.Max(beside, 20)) onTheirArrow++;
        else offArrow.Add($"{arrows[index].ButtonName}: on-arrow {onArrow} vs beside {beside} green px");
      }

      Check(onTheirArrow == arrows.Length,
            "and the screen's two painted arrows are real buttons standing ON the medallions the "
            + $"INSTALL's own art paints for them - the only green in that part of the picture "
            + $"({onTheirArrow}/{arrows.Length} buttons on their own medallion's green"
            + (offArrow.Count > 0 ? "; " + string.Join("; ", offArrow) : "") + ")");
    }

    /// <summary>
    /// The map request screen's real input surface, walked with the buttons' own
    /// pointer clicks: two list rows (a different map selected each time, with
    /// the title strip and the highlight read off the screen after each), the
    /// list's end walked with Next and Previous (page 3's ten rows, a Next at
    /// the last page moving nothing and naming it, a Previous back to page 1),
    /// and the Play row - whose click must name the selected map and the
    /// artifact gap. Every click is the screen's ONE entry point recording its
    /// own row.
    /// </summary>
    private static void CheckMapRequestWalk()
    {
      int startDispatches = MapRequestScreenActions.DispatchCount;
      int clicks = 0;
      int dispatchedTheirOwnRow = 0;
      var wrongState = new List<string>();

      // The state the screen opens on, before any click: page 1, the list's
      // first map.
      if (!(MapRequestScreenActions.Page == 0 && MapRequestScreenActions.SelectedIndex == 0
            && ShownMapRequestTitle() == MapRequestScreenActions.Maps[0].Stem))
      {
        wrongState.Add($"on opening: page={MapRequestScreenActions.Page}, "
                       + $"selection={MapRequestScreenActions.SelectedIndex}, "
                       + $"title='{ShownMapRequestTitle()}'");
      }

      // One list row on the first page: the selection, the title strip and the
      // highlight must all follow the click.
      MapRequestScreenAction row = MapRequestScreenActions.SelectRow(7);
      ClickMapRequestButton(row, ref clicks, ref dispatchedTheirOwnRow);
      if (!(MapRequestScreenActions.SelectedIndex == 7
            && ShownMapRequestTitle() == MapRequestScreenActions.Maps[7].Stem
            && ShownMapRequestRowIsSelected(7)))
      {
        wrongState.Add($"row '{row.Label}': selection={MapRequestScreenActions.SelectedIndex} "
                       + $"(expected 7), title='{ShownMapRequestTitle()}'");
      }

      // The pages, walked with the right painted arrow: each Next moves one page
      // and its first map becomes the selection.
      for (int page = 1; page < MapRequestScreenActions.PageCount; page++)
      {
        ClickMapRequestButton(MapRequestScreenActions.NextPage, ref clicks, ref dispatchedTheirOwnRow);
        int firstOnPage = page * MapRequestScreenActions.PageSize;
        if (!(MapRequestScreenActions.Page == page
              && MapRequestScreenActions.SelectedIndex == firstOnPage
              && ShownMapRequestTitle() == MapRequestScreenActions.Maps[firstOnPage].Stem))
        {
          wrongState.Add($"page {page + 1}: page={MapRequestScreenActions.Page}, "
                         + $"selection={MapRequestScreenActions.SelectedIndex} "
                         + $"(expected {firstOnPage}), title='{ShownMapRequestTitle()}'");
        }
      }
      Check(MapRequestScreenActions.Page == MapRequestScreenActions.PageCount - 1
            && MapRequestScreenActions.RowsOnPage
               == MapRequestScreenActions.Maps.Length - (MapRequestScreenActions.PageCount - 1)
                  * MapRequestScreenActions.PageSize,
            $"and the screen's {MapRequestScreenActions.PageCount} pages are all reachable with the "
            + $"right painted arrow (walked to page {MapRequestScreenActions.Page + 1} of "
            + $"{MapRequestScreenActions.PageCount}, {MapRequestScreenActions.RowsOnPage} map(s) of "
            + $"the install's {MapRequestScreenActions.Maps.Length} on it; the rows past the list's "
            + "end are off the screen)");

      ClickMapRequestButton(MapRequestScreenActions.NextPage, ref clicks, ref dispatchedTheirOwnRow);
      Check(MapRequestScreenActions.Page == MapRequestScreenActions.PageCount - 1
            && ReportHas("already on its last page"),
            "and a Next click on the last page dispatches its row, names the end and moves nothing "
            + "(the port's own wording: the install ships no row for the arrows)");

      // One list row on the last page, then the page range walked back.
      MapRequestScreenAction lastPageRow = MapRequestScreenActions.SelectRow(2);
      int lastPageIndex = (MapRequestScreenActions.PageCount - 1) * MapRequestScreenActions.PageSize + 2;
      ClickMapRequestButton(lastPageRow, ref clicks, ref dispatchedTheirOwnRow);
      if (!(MapRequestScreenActions.SelectedIndex == lastPageIndex
            && ShownMapRequestTitle() == MapRequestScreenActions.Maps[lastPageIndex].Stem
            && ShownMapRequestRowIsSelected(2)))
      {
        wrongState.Add($"row '{lastPageRow.Label}': selection={MapRequestScreenActions.SelectedIndex} "
                       + $"(expected {lastPageIndex}), title='{ShownMapRequestTitle()}'");
      }

      for (int page = MapRequestScreenActions.PageCount - 2; page >= 0; page--)
      {
        ClickMapRequestButton(MapRequestScreenActions.PreviousPage, ref clicks, ref dispatchedTheirOwnRow);
        int firstOnPage = page * MapRequestScreenActions.PageSize;
        if (!(MapRequestScreenActions.Page == page
              && MapRequestScreenActions.SelectedIndex == firstOnPage
              && ShownMapRequestTitle() == MapRequestScreenActions.Maps[firstOnPage].Stem))
        {
          wrongState.Add($"page {page + 1} (previous): page={MapRequestScreenActions.Page}, "
                         + $"selection={MapRequestScreenActions.SelectedIndex} "
                         + $"(expected {firstOnPage}), title='{ShownMapRequestTitle()}'");
        }
      }
      ClickMapRequestButton(MapRequestScreenActions.PreviousPage, ref clicks, ref dispatchedTheirOwnRow);
      Check(MapRequestScreenActions.Page == 0 && ReportHas("already on its first page"),
            "and a Previous click on the first page does the same at the other end "
            + "(the list is back on its first page with its first map selected)");

      Check(clicks > 0 && dispatchedTheirOwnRow == clicks,
            $"the screen's rows and arrows are real buttons: all {clicks} clicks of the walk dispatched "
            + "their OWN row through the screen's one entry point "
            + $"({dispatchedTheirOwnRow}/{clicks}; the twelve list buttons are rebuilt content, so the "
            + "record's map index is what tells them apart)");
      Check(wrongState.Count == 0,
            "and every selection and paging click left the screen showing what it chose "
            + $"({MapRequestScreenActions.SelectedIndex} selected on page "
            + $"{MapRequestScreenActions.Page + 1}"
            + (wrongState.Count > 0 ? "; " + string.Join("; ", wrongState) : "") + ")");

      // The Play row: the one click that would start the custom game - named as
      // unbuilt, the selection untouched.
      MapRequestMap selected = MapRequestScreenActions.Selected;
      ClickMapRequestButton(MapRequestScreenActions.Play, ref clicks, ref dispatchedTheirOwnRow);
      Check(MapRequestScreenActions.LastDispatch.Kind == MapRequestScreenActionKind.Play
            && ReportHas($"{MapRequestScreenActions.PlayId} '{MapRequestScreenActions.Play.Label}'")
            && ReportHas(selected.FileName)
            && ReportHas("is not built")
            && MapRequestScreenActions.SelectedIndex == 0,
            "and the Play row's own click names the selected map and that starting a custom game on "
            + $"it is a later item - the port's flow loads the campaign's chapters, and these maps' "
            + $"per-map entity dumps are not there ('{selected.FileName}') - rather than doing nothing");

      Line($"map request  : the list walk: {MapRequestScreenActions.DispatchCount - startDispatches} "
           + $"clicks through the screen's one entry point - two list rows on different pages, the "
           + "pages walked both ways, the two ends named, and the Play row - the screen opening on the "
           + $"install's list's first map and now standing on '{MapRequestScreenActions.Selected.Stem}'.");
    }

    /// <summary>One map request row's pointer click, counted into the walk's totals.</summary>
    private static void ClickMapRequestButton(MapRequestScreenAction row, ref int clicks,
                                              ref int dispatchedTheirOwnRow)
    {
      GameObject buttonObject = GameObject.Find(row.ButtonName);
      Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
      if (button == null || (buttonObject != null && !buttonObject.activeSelf))
      {
        Fail($"the map request screen's {(row.Label.Length > 0 ? row.Label : row.ButtonName)} button "
             + $"({row.ButtonName}) is not clickable on the screen, so its click could not be made");
        return;
      }

      int before = MapRequestScreenActions.DispatchCount;
      PressButton(button);
      clicks++;
      MapRequestScreenAction dispatched = MapRequestScreenActions.LastDispatch;
      if (MapRequestScreenActions.DispatchCount == before + 1
          && dispatched.Kind == row.Kind
          && (row.Kind != MapRequestScreenActionKind.SelectMap || dispatched.MapIndex == row.MapIndex))
      {
        dispatchedTheirOwnRow++;
      }
    }

    /// <summary>The map request screen's title strip text.</summary>
    private static string ShownMapRequestTitle() => LabelTextOf("MapRequestScreenUITitle");

    /// <summary>True when one visible list row wears the screen's selected colour.</summary>
    private static bool ShownMapRequestRowIsSelected(int pageIndex)
    {
      GameObject rowObject = GameObject.Find(MapRequestScreenActions.MapRowButtonName(pageIndex));
      Transform labelTransform = rowObject != null ? rowObject.transform.Find("Label") : null;
      UnityEngine.UI.Text label = labelTransform != null
        ? labelTransform.GetComponent<UnityEngine.UI.Text>() : null;
      return label != null && label.color.r > 0.9f && label.color.g > 0.85f;
    }

    /// <summary>True when a name carries a capital letter (the install's own spelling).</summary>
    private static bool HasCapital(string name)
    {
      foreach (char c in name)
      {
        if (char.IsUpper(c)) return true;
      }
      return false;
    }

    /// <summary>
    /// The load screen, once the war screen's Load click has opened it: its own
    /// content (its own canvas above the war screen's, the install's own picture
    /// the screen's class loads, its shipped header, its three column headers -
    /// which are its sort rows - and its Load/Cancel rows on the plates the art
    /// paints), its rows against the install's table, the EMPTY list (the port
    /// ships no saved games, and the table ships no empty-state row), the art by
    /// pixels, the capture, and then the walk: the three sorts and the Load row
    /// clicked through the screen's one entry point, each naming what it would
    /// do. Its own Cancel row, the click that goes back to the WAR SCREEN, is
    /// made after the walk.
    /// </summary>
    private static void TickLoadScreen()
    {
      if (Expired(PlayScreenBudgetSeconds, "the load screen never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      if (game.State != GameState.LOAD_SCREEN || GameObject.Find("LoadScreenUI") == null) return;
      NoteFrontendState(game.State);

      if (!loadScreenChecked)
      {
        loadScreenChecked = true;

        Check(ReportHas("Game flow: WAR_SCREEN -> LOAD_SCREEN."),
              "the war screen's Load click moved the flow to the load screen (the console logged "
              + $"WAR_SCREEN -> LOAD_SCREEN, state={game.State})");
        Check(GameObject.Find("WarScreenUI") == null,
              "and the war screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckLoadScreenContent();
        CheckLoadScreenStringsAgainstInstall();
        CheckLoadScreenArt();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(LoadScreenCapturePath);
        Line($"capture      : asked for the Game view render -> {LoadScreenCapturePath}");
        return;
      }

      if (!loadScreenCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        loadScreenCaptureChecked = true;
        CheckPathScreenCapture(LoadScreenCapturePath, LoadScreenCanvasCapturePath,
                               "LoadScreenUI", "load screen");
        CheckLoadScreenWalk();
      }

      ClickDepartingRow("load", "LoadScreenActions",
                        LoadScreenActions.Cancel.ButtonName, LoadScreenActions.Cancel.Id,
                        LoadScreenActions.Cancel.Label, LoadScreenActions.Cancel.Outcome,
                        "way back (its own Cancel row)", () => LoadScreenActions.DispatchCount,
                        () => LoadScreenActions.LastDispatch.Id, Phase.BackToWarFromLoad);
    }

    /// <summary>
    /// What the load screen carries, read off the screen itself: its own canvas
    /// above the war screen's, the install's own picture the screen's class
    /// loads (ui/ingame_menus/scenario_loadscreen.jpg, 1024x768 - its code
    /// loads it as 'texture/loadscenario background'), the shipped header
    /// (LOADSCENARIOSCREEN_HEADER_LOAD 'Load') in the title strip, the three
    /// column headers (LOADSCENARIOSCREEN_NAME 'Name' / _DATE 'Date' / _PLAYERS
    /// 'P') at the top of the list area, the two rows (LOAD 'Load' / CANCEL
    /// 'Cancel') on the plates the art paints, its own aspect-ratio bars, NO
    /// list row at all (the list is empty - the port ships no saved games - and
    /// the table ships no empty-state row, so none is drawn), and only the five
    /// buttons the screen has.
    /// </summary>
    private static void CheckLoadScreenContent()
    {
      GameObject screen = GameObject.Find("LoadScreenUI");
      Check(screen != null, "the load screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1300,
            "it is its own canvas above the war screen's "
            + $"(sorting order {canvas?.sortingOrder}, the war screen's is 1300)");

      GameObject background = GameObject.Find("LoadScreenUIBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.LoadScenarioBackgroundTexture)
            && backgroundTexture.width == 1024 && backgroundTexture.height == 768,
            $"its backdrop is the install's own picture of this screen - the file the engine's own "
            + $"LoadScenarioScreen loads ('{backgroundTexture?.name}' = "
            + $"{Path.GetFileName(UIAssets.LoadScenarioBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height})");

      Check(LabelTextOf("LoadScreenUIHeader") == LoadScreenActions.HeaderLabel,
            "and its shipped header stands in the title strip the art paints "
            + $"({LoadScreenActions.HeaderId} '{LoadScreenActions.HeaderLabel}' - the screen's LOAD "
            + $"mode; {LoadScreenActions.SaveHeaderId} '{LoadScreenActions.SaveHeaderLabel}' is the "
            + "in-game menu's SAVE mode, which this port names and does not build yet)");

      int sorts = 0;
      int sortsLabelled = 0;
      foreach (LoadScreenAction sort in LoadScreenActions.Sorts)
      {
        GameObject sortObject = GameObject.Find(sort.ButtonName);
        Button sortButton = sortObject != null ? sortObject.GetComponent<Button>() : null;
        if (sortButton != null) sorts++;
        if (LabelTextOf(sort.ButtonName) == sort.Label) sortsLabelled++;
      }
      Check(sorts == LoadScreenActions.Sorts.Length && sortsLabelled == sorts,
            "and the list's three column headers stand at the top of the panel - the screen's own "
            + $"sort rows - each wearing its shipped label ({sortsLabelled}/{sorts}: "
            + string.Join(", ", Array.ConvertAll(LoadScreenActions.Sorts, s => $"{s.Id} '{s.Label}'"))
            + ")");

      int rows = 0;
      int rowsLabelled = 0;
      foreach (LoadScreenAction row in LoadScreenActions.Rows)
      {
        GameObject rowObject = GameObject.Find(row.ButtonName);
        Button rowButton = rowObject != null ? rowObject.GetComponent<Button>() : null;
        if (rowButton != null) rows++;
        if (LabelTextOf(row.ButtonName) == row.Label) rowsLabelled++;
      }
      Check(rows == LoadScreenActions.Rows.Length && rowsLabelled == rows,
            "and its two rows stand on the two plates the art paints along the bottom, each wearing "
            + $"its shipped label ({rowsLabelled}/{rows}: "
            + string.Join(", ", Array.ConvertAll(LoadScreenActions.Rows, r => $"{r.Id} '{r.Label}'"))
            + ")");

      int allButtons = screen.GetComponentsInChildren<Button>(true).Length;
      Check(allButtons == LoadScreenActions.Sorts.Length + LoadScreenActions.Rows.Length,
            $"and the list itself is EMPTY - no row is drawn, because the port ships no saved games "
            + $"(the shipped game lists the player's own saves from the user's Documents folder, "
            + "\"\\Stronghold 2\\Saves\\\") and the table ships no empty-state row to draw instead "
            + $"({allButtons} buttons on the screen: {LoadScreenActions.Sorts.Length} sorts and "
            + $"{LoadScreenActions.Rows.Length} rows)");

      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the shipped stone "
            + $"and not the scene behind the screen ({barSprites}/2 bars with their stone sprite)");

      Line("load screen  : the three things the install does not pin, said rather than faked: the two "
           + "rows' buttons are transparent over the plates the art paints (the plate IS the install's "
           + "picture); the column headers' place is the port's reading of the list area's top row; and "
           + "the list is empty - the install's own truth on a fresh machine - so no row, and no "
           + "invented empty-state line either (the table ships none).");
    }

    /// <summary>
    /// The load screen's rows against the install, read at run time: its header
    /// (LOADSCENARIOSCREEN_HEADER_LOAD 'Load'), its three column headers
    /// (LOADSCENARIOSCREEN_NAME 'Name' / _DATE 'Date' / _PLAYERS 'P'), its two
    /// rows (LOAD 'Load' / CANCEL 'Cancel') and the three sort tooltips
    /// (LOADSCENARIO_STATICTOOLTIP_{FILENAMESORT,DATESORT,PLAYERNUMBERSORT}) the
    /// columns' clicks name - plus the SAVE header row, which the screen's other
    /// mode wears and this port does not build yet.
    /// </summary>
    private static void CheckLoadScreenStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      var pairs = new List<(string Id, string Text)>
      {
        (LoadScreenActions.HeaderId, LoadScreenActions.HeaderLabel),
        (LoadScreenActions.SaveHeaderId, LoadScreenActions.SaveHeaderLabel),
        (LoadScreenActions.Load.Id, LoadScreenActions.Load.Label),
        (LoadScreenActions.Cancel.Id, LoadScreenActions.Cancel.Label),
        ("LOADSCENARIO_STATICTOOLTIP_CANCEL", LoadScreenActions.Cancel.Tooltip),
      };
      foreach (LoadScreenAction sort in LoadScreenActions.Sorts)
      {
        pairs.Add((sort.Id, sort.Label));
      }
      pairs.Add(("LOADSCENARIO_STATICTOOLTIP_FILENAMESORT", LoadScreenActions.SortName.Tooltip));
      pairs.Add(("LOADSCENARIO_STATICTOOLTIP_DATESORT", LoadScreenActions.SortDate.Tooltip));
      pairs.Add(("LOADSCENARIO_STATICTOOLTIP_PLAYERNUMBERSORT", LoadScreenActions.SortPlayers.Tooltip));

      int agreed = 0;
      foreach ((string id, string text) in pairs)
      {
        if (shipped.TryGetValue(id, out string english) && english == text) agreed++;
        else
        {
          mismatches.Add($"{id} shipped="
                         + $"{(shipped.TryGetValue(id, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port='{text}'");
        }
      }

      Check(agreed == pairs.Count,
            $"the load screen's {pairs.Count} rows are the shipped table's own, with the shipped text "
            + $"({agreed}/{pairs.Count} LOADSCENARIOSCREEN*/LOADSCENARIO_STATICTOOLTIP_* rows agree "
            + $"with i18n/en-us.xml, {shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");
      Line($"load screen  : the install's own name for the screen is "
           + $"'{LoadScreenActions.ScreenName}' (the engine's LoadScenarioScreen), and the one thing "
           + "it says about an empty list is nothing: no LOADSCENARIOSCREEN row describes one "
           + "(measured), so the port draws none.");
    }

    /// <summary>
    /// The load screen's art, against the install's own file by name, size AND
    /// pixels - and the two things the screen must NOT have drawn: any list row
    /// and any invented empty-state line (the empty list is judged in
    /// CheckLoadScreenContent by the button count).
    /// </summary>
    private static void CheckLoadScreenArt()
    {
      CheckSpriteMatchesInstallFile("LoadScreenUIBackground", UIAssets.LoadScenarioBackgroundTexture,
                                    "the load screen's backdrop");
      Check(GameObject.Find("LoadScreenUIListRow") == null,
            "and it draws no list row and no empty-state line - the list is empty and the table ships "
            + "no words for that state, so the port invents none");
    }

    /// <summary>
    /// The load screen's real input surface, walked with the buttons' own
    /// pointer clicks: the three column headers (each click the shipped sort it
    /// names) and the Load row (its click naming that nothing is selected) -
    /// every one through the screen's one entry point recording its own row.
    /// The Cancel row, the click that leaves for the war screen, is made after
    /// the walk.
    /// </summary>
    private static void CheckLoadScreenWalk()
    {
      int startDispatches = LoadScreenActions.DispatchCount;
      int clicks = 0;
      int dispatchedTheirOwnRow = 0;

      foreach (LoadScreenAction sort in LoadScreenActions.Sorts)
      {
        ClickLoadScreenButton(sort, ref clicks, ref dispatchedTheirOwnRow);
        Check(ReportHas($"{sort.Id} '{sort.Label}'") && ReportHas(sort.Tooltip),
              $"the load screen's {sort.Label} column header's click dispatched {sort.Id} "
              + $"'{sort.Label}' through its one entry point and named the shipped sort it would "
              + $"apply ({sort.Tooltip})");
      }

      ClickLoadScreenButton(LoadScreenActions.Load, ref clicks, ref dispatchedTheirOwnRow);
      Check(ReportHas($"{LoadScreenActions.Load.Id} '{LoadScreenActions.Load.Label}'")
            && ReportHas("no saved game is selected"),
            "and its Load row's click names that no saved game is selected - the port ships no saved "
            + "games and has no save reader - rather than doing nothing");

      Check(clicks > 0 && dispatchedTheirOwnRow == clicks,
            $"the screen's {clicks} clicks (three sorts and the Load row) each dispatched their OWN "
            + $"row through the screen's one entry point ({dispatchedTheirOwnRow}/{clicks})");

      Line($"load screen  : the row walk: {LoadScreenActions.DispatchCount - startDispatches} clicks "
           + "through the screen's one entry point - the three sorts and the Load row, each naming "
           + "what it would do to a list that is empty; the Cancel row, the way back, is next.");
    }

    /// <summary>One load-screen row's pointer click, counted into the walk's totals.</summary>
    private static void ClickLoadScreenButton(LoadScreenAction row, ref int clicks,
                                              ref int dispatchedTheirOwnRow)
    {
      GameObject buttonObject = GameObject.Find(row.ButtonName);
      Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
      if (button == null)
      {
        Fail($"the load screen's {row.Label} button ({row.ButtonName}) is not on the screen, so its "
             + "click could not be made");
        return;
      }

      int before = LoadScreenActions.DispatchCount;
      PressButton(button);
      clicks++;
      if (LoadScreenActions.DispatchCount == before + 1
          && LoadScreenActions.LastDispatch.Id == row.Id)
      {
        dispatchedTheirOwnRow++;
      }
    }

    // ------------------------------------------------------------------
    // the scenario lobby screen (GameState.SCENARIO_LOBBY_SCREEN), the war
    // screen's Kingmaker destination - its SIXTH and last branch
    // ------------------------------------------------------------------

    /// <summary>
    /// The scenario lobby screen, judged on itself and against the install's own
    /// art, then walked. It is the screen the war screen's WARSCREEN_KINGMAKER
    /// row opens - MEASURED out of the shipped engine (see
    /// <see cref="ScenarioLobbyActions"/>): the row's control's action id's branch
    /// pushes the screen-name string 'scenario lobby screen', the engine's
    /// `ScenarioLobbyScreen` - the multiplayer lobby, whose game-type strip is
    /// where Kingmaker is one of four modes. The phase: one wait for the screen to
    /// come up and be judged (its content, its rows against i18n/en-us.xml, its
    /// art by name/size/pixels), a second for its capture, and then the walk of
    /// its four game types and its Start row through the screen's one entry point.
    /// Its own Back row, the click that goes back to the WAR SCREEN, comes after
    /// the walk.
    /// </summary>
    private static void TickScenarioLobby()
    {
      if (Expired(PlayScreenBudgetSeconds, "the scenario lobby never came up after its button's click"))
        return;

      Game game = Game.GetInstance();
      if (game == null) return;

      if (game.State != GameState.SCENARIO_LOBBY_SCREEN || GameObject.Find("ScenarioLobbyUI") == null)
        return;
      NoteFrontendState(game.State);

      if (!scenarioLobbyChecked)
      {
        scenarioLobbyChecked = true;

        Check(ReportHas("Game flow: WAR_SCREEN -> SCENARIO_LOBBY_SCREEN."),
              "the war screen's Kingmaker click moved the flow to the scenario lobby screen (the console "
              + $"logged WAR_SCREEN -> SCENARIO_LOBBY_SCREEN, state={game.State})");
        Check(GameObject.Find("WarScreenUI") == null,
              "and the war screen it was opened from is gone while it is up "
              + "(one frontend screen is on at a time)");
        CheckScenarioLobbyContent();
        CheckScenarioLobbyStringsAgainstInstall();
        CheckScenarioLobbyArt();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(ScenarioLobbyCapturePath);
        Line($"capture      : asked for the Game view render -> {ScenarioLobbyCapturePath}");
        return;
      }

      if (!scenarioLobbyCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        scenarioLobbyCaptureChecked = true;
        CheckPathScreenCapture(ScenarioLobbyCapturePath, ScenarioLobbyCanvasCapturePath,
                               "ScenarioLobbyUI", "scenario lobby");
        CheckScenarioLobbyWalk();
      }

      ClickDepartingRow("scenario lobby", "ScenarioLobbyActions",
                        ScenarioLobbyActions.Back.ButtonName, ScenarioLobbyActions.Back.Id,
                        ScenarioLobbyActions.Back.Label, ScenarioLobbyActions.Back.Outcome,
                        "way back (its own Back row)", () => ScenarioLobbyActions.DispatchCount,
                        () => ScenarioLobbyActions.LastDispatch.Id, Phase.BackToWarFromLobby);
    }

    /// <summary>
    /// What the scenario lobby carries, read off the screen itself: its own canvas
    /// above the war screen's, the install's own picture of the lobby
    /// (ui/multiplayer/multiplayer_background.jpg, 1024x768 - the file the
    /// engine's own ScenarioLobbyScreen loads), the shipped lobby name row
    /// (LOBBYSCREEN_NETWORK_LOBBY 'Network Lobby'), the four game-type buttons on
    /// the medallions the picture paints (each the install's own ui/multiplayer/
    /// button_gametype*.tga art, the selected Kingmaker one wearing its own
    /// highlight file), the screen's own Start and Back rows, its own
    /// aspect-ratio bars, NO player line at all (the lobby's slots hold a network
    /// session's players, the port has no session, and the table ships no
    /// empty-state row, so none is drawn), and only the six buttons the screen
    /// has.
    /// </summary>
    private static void CheckScenarioLobbyContent()
    {
      GameObject screen = GameObject.Find("ScenarioLobbyUI");
      Check(screen != null, "the scenario lobby's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1300,
            "it is its own canvas above the war screen's "
            + $"(sorting order {canvas?.sortingOrder}, the war screen's is 1300)");

      GameObject background = GameObject.Find("ScenarioLobbyUIBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.MultiplayerLobbyBackgroundTexture)
            && backgroundTexture.width == 1024 && backgroundTexture.height == 768,
            "its backdrop is the install's own picture of this screen - the file the engine's own "
            + $"ScenarioLobbyScreen loads ('{backgroundTexture?.name}' = "
            + $"{Path.GetFileName(UIAssets.MultiplayerLobbyBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height})");

      Check(LabelTextOf("ScenarioLobbyUIHeader") == ScenarioLobbyActions.HeaderLabel,
            "and the shipped lobby name row stands across the map frame the picture paints "
            + $"({ScenarioLobbyActions.HeaderId} '{ScenarioLobbyActions.HeaderLabel}' - the place is "
            + "this port's reading: the art paints no title strip, and the shipped screen fills that "
            + "frame with the selected map's preview)");

      int gameTypes = 0;
      int gameTypesWithSprites = 0;
      foreach (ScenarioLobbyAction gameType in ScenarioLobbyActions.GameTypes)
      {
        GameObject gameTypeObject = GameObject.Find(gameType.ButtonName);
        Button gameTypeButton = gameTypeObject != null ? gameTypeObject.GetComponent<Button>() : null;
        UnityEngine.UI.Image gameTypeImage = gameTypeObject != null
          ? gameTypeObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (gameTypeButton != null) gameTypes++;
        if (gameTypeImage != null && gameTypeImage.sprite != null) gameTypesWithSprites++;
      }
      Check(gameTypes == ScenarioLobbyActions.GameTypes.Length
            && gameTypesWithSprites == ScenarioLobbyActions.GameTypes.Length,
            "and the lobby's four game-type buttons stand on the four medallions the picture paints, "
            + $"each wearing the install's own button art ({gameTypesWithSprites}/{gameTypes}: "
            + string.Join(", ", Array.ConvertAll(ScenarioLobbyActions.GameTypes, g => g.Label))
            + " - the words are painted in the art, so none is drawn over them)");

      Check(LabelTextOf(ScenarioLobbyActions.Start.ButtonName) == ScenarioLobbyActions.Start.Label
            && LabelTextOf(ScenarioLobbyActions.Back.ButtonName) == ScenarioLobbyActions.Back.Label,
            "and its own two rows wear their shipped labels "
            + $"({ScenarioLobbyActions.Start.Id} '{ScenarioLobbyActions.Start.Label}' on the frontend's "
            + $"button, {ScenarioLobbyActions.Back.Id} '{ScenarioLobbyActions.Back.Label}' on the round "
            + "exit medallion)");

      int allButtons = screen.GetComponentsInChildren<Button>(true).Length;
      Check(allButtons == ScenarioLobbyActions.GameTypes.Length + ScenarioLobbyActions.Rows.Length,
            "and the lobby itself is EMPTY - no player line is drawn, because its slots hold a network "
            + "session's players, this port has no session and no online service, and the table ships "
            + $"no empty-state row to draw instead ({allButtons} buttons on the screen: "
            + $"{ScenarioLobbyActions.GameTypes.Length} game types and "
            + $"{ScenarioLobbyActions.Rows.Length} rows)");

      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            "and its own aspect-ratio bars are up, so the widescreen gutters are the shipped stone "
            + $"and not the scene behind the screen ({barSprites}/2 bars with their stone sprite)");

      Line("scenario lobby: the three things the install does not pin, said rather than faked: the four "
           + "game-type buttons are the install's own art but the engine's exact pixel offsets are not "
           + "in the shipped data, so the port stands them on the medallions the backdrop itself paints "
           + "(measured off the picture); the name row's place is the port's reading of the map frame; "
           + "and the lobby is empty - no session, no players - so no player line and no invented "
           + "empty-state line either (the table ships none).");
    }

    /// <summary>
    /// The scenario lobby's rows against the install, read at run time: its own
    /// name row (LOBBYSCREEN_NETWORK_LOBBY 'Network Lobby') and its two action
    /// rows (LOBBYSCREEN_START 'Start' / LOBBYSCREEN_BACK 'Back'). The four game
    /// types are NOT in this check, and that is the measurement: the install ships
    /// no i18n row for a game type at all - the mode's word is painted in the
    /// button's own art, and in the shipped data it exists only as the button's
    /// file name (button_gametype1_kingmaker.tga and its three siblings) - so the
    /// port draws no words over the four and carries the exe's own registration
    /// ids ('texture/ui/lobby/gametype/1..4') on the rows instead.
    /// </summary>
    private static void CheckScenarioLobbyStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      var pairs = new List<(string Id, string Text)>
      {
        (ScenarioLobbyActions.HeaderId, ScenarioLobbyActions.HeaderLabel),
        (ScenarioLobbyActions.Start.Id, ScenarioLobbyActions.Start.Label),
        (ScenarioLobbyActions.Back.Id, ScenarioLobbyActions.Back.Label),
      };

      int agreed = 0;
      foreach ((string id, string text) in pairs)
      {
        if (shipped.TryGetValue(id, out string english) && english == text) agreed++;
        else
        {
          mismatches.Add($"{id} shipped="
                         + $"{(shipped.TryGetValue(id, out string found) ? $"'{found}'" : "<no such row>")}"
                         + $" port='{text}'");
        }
      }

      Check(agreed == pairs.Count,
            $"the scenario lobby's {pairs.Count} text rows are the shipped table's own, with the "
            + $"shipped text ({agreed}/{pairs.Count} LOBBYSCREEN_* rows agree with i18n/en-us.xml, "
            + $"{shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");

      int gameTypeRows = 0;
      foreach (ScenarioLobbyAction gameType in ScenarioLobbyActions.GameTypes)
      {
        if (shipped.ContainsKey(gameType.Id)) gameTypeRows++;
      }
      Check(gameTypeRows == 0,
            "and the four game types have no shipped text row to agree with - measured: the install "
            + "ships no i18n row for a game type, so the mode's word lives in the button's own art "
            + "(the file names are the four words) and the rows carry the exe's own registration ids "
            + $"({string.Join(", ", Array.ConvertAll(ScenarioLobbyActions.GameTypes, g => g.Id))})");

      Line($"scenario lobby: the install's own name for the screen is '{ScenarioLobbyActions.ScreenName}' "
           + "(the engine's ScenarioLobbyScreen), and what it is made of is measured too: its class's own "
           + "code loads ui\\multiplayer\\multiplayer_background.jpg as the backdrop and "
           + "ui\\multiplayer\\button_gametype1_kingmaker.tga among the four game-type buttons, with the "
           + "texture/ui/lobby/ family (teams, ready flags, difficulty scales, the lords' icons) beside "
           + "them - the multiplayer lobby, whose own machinery is the online service's.");
    }

    /// <summary>
    /// The scenario lobby's art, against the install's own files by name, size AND
    /// pixels: the backdrop, the four game-type buttons (the selected one wearing
    /// its own highlight file) and the Start row's button - plus the two things the
    /// screen must NOT have drawn: any player line, and any sprite over the exit
    /// medallion the backdrop paints (that row's button is transparent; the
    /// medallion IS the install's picture). The medallions themselves are checked
    /// against the backdrop's own pixels, so a button off its painted medallion
    /// fails rather than passing on the art's name alone.
    /// </summary>
    private static void CheckScenarioLobbyArt()
    {
      CheckSpriteMatchesInstallFile("ScenarioLobbyUIBackground",
                                    UIAssets.MultiplayerLobbyBackgroundTexture,
                                    "the scenario lobby's backdrop");

      for (int i = 0; i < ScenarioLobbyActions.GameTypes.Length; i++)
      {
        ScenarioLobbyAction gameType = ScenarioLobbyActions.GameTypes[i];
        CheckSpriteMatchesInstallFile(gameType.ButtonName, ScenarioLobbyUI.GameTypeTextureFor(i),
                                      $"the lobby's {gameType.Label} game-type button");
      }

      CheckSpriteMatchesInstallFile(ScenarioLobbyActions.Start.ButtonName,
                                    UIAssets.FancyButtonTexture,
                                    "the lobby's Start row's button");
      Check(SelectedGameTypeIsHighlighted(),
            "and the selected game type is Kingmaker - the one the war screen's row came in by - and it "
            + "is the button wearing the highlight art the install ships beside it "
            + $"({Path.GetFileName(UIAssets.GameTypeKingmakerHighlightTexture)})");

      GameObject backObject = GameObject.Find(ScenarioLobbyActions.Back.ButtonName);
      UnityEngine.UI.Image backImage = backObject != null
        ? backObject.GetComponent<UnityEngine.UI.Image>() : null;
      Check(backImage != null && backImage.sprite == null,
            "and its Back row's button is transparent over the round exit medallion the picture "
            + "paints in its bottom-left corner - the medallion IS the install's picture for the row");

      GameObject playerLine = GameObject.Find("ScenarioLobbyUIPlayerLine");
      Check(playerLine == null,
            "and it draws no player line and no invented empty-state line - the lobby has no session "
            + "and the table ships no words for that state, so the port invents none");

      CheckBackdropPaintsMedallion("the lobby's Kingmaker game-type button", 217f, 375f);
      CheckBackdropPaintsMedallion("the lobby's Knight game-type button", 282f, 375f);
      CheckBackdropPaintsMedallion("the lobby's Baron game-type button", 217f, 444f);
      CheckBackdropPaintsMedallion("the lobby's Deathmatch game-type button", 282f, 444f);
      CheckBackdropPaintsMedallion("the lobby's Back row", 77f, 697f);
    }

    /// <summary>True when the button drawn for the selected game type is the highlight file's sprite.</summary>
    private static bool SelectedGameTypeIsHighlighted()
    {
      ScenarioLobbyAction selected = ScenarioLobbyActions.Selected;
      GameObject buttonObject = GameObject.Find(selected.ButtonName);
      UnityEngine.UI.Image image = buttonObject != null
        ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
      return image != null && image.sprite != null && image.sprite.texture != null
             && image.sprite.texture.name
                == Path.GetFileName(UIAssets.GameTypeKingmakerHighlightTexture);
    }

    /// <summary>
    /// One slot the lobby's backdrop paints (a measured pixel of the 1024x768
    /// art), judged against the backdrop's OWN pixels: the medallion's centre must
    /// differ from the wall beside it by a wide margin, so a row the port stood
    /// off its painted medallion fails here rather than passing on the art's name
    /// alone.
    /// </summary>
    private static void CheckBackdropPaintsMedallion(string what, float px, float py)
    {
      GameObject background = GameObject.Find("ScenarioLobbyUIBackground");
      UnityEngine.UI.Image image = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backdrop = image != null && image.sprite != null ? image.sprite.texture : null;
      if (backdrop == null)
      {
        Fail($"{what} could not be checked against the backdrop: the backdrop's texture is not loaded");
        return;
      }

      // The art is read as it is drawn (the sprite is not flipped), so a measured
      // pixel of the picture is the same pixel of the texture.
      Color centre = MeanColour(backdrop, px / 1024f, 1f - (py / 768f), 4);
      Color beside = MeanColour(backdrop, (px + 80f) / 1024f, 1f - (py / 768f), 4);
      double delta = Math.Abs(centre.r - beside.r) + Math.Abs(centre.g - beside.g)
                     + Math.Abs(centre.b - beside.b);
      Check(delta > 0.06,
            $"{what} stands on a medallion the backdrop's own picture paints "
            + $"({px},{py} of 1024x768: the patch there reads {centre.r:F2},{centre.g:F2},{centre.b:F2} "
            + $"against {beside.r:F2},{beside.g:F2},{beside.b:F2} of the wall beside it, a difference of "
            + $"{delta:F2})");
    }

    /// <summary>The mean colour of a small square patch of a texture, in UV space.</summary>
    private static Color MeanColour(Texture2D texture, float u, float v, int radius)
    {
      int cx = Mathf.Clamp(Mathf.RoundToInt(u * (texture.width - 1)), 0, texture.width - 1);
      int cy = Mathf.Clamp(Mathf.RoundToInt(v * (texture.height - 1)), 0, texture.height - 1);
      double r = 0, g = 0, b = 0;
      int n = 0;
      for (int y = -radius; y <= radius; y++)
      {
        for (int x = -radius; x <= radius; x++)
        {
          int sx = Mathf.Clamp(cx + x, 0, texture.width - 1);
          int sy = Mathf.Clamp(cy + y, 0, texture.height - 1);
          Color c = texture.GetPixel(sx, sy);
          r += c.r; g += c.g; b += c.b; n++;
        }
      }
      return new Color((float)(r / n), (float)(g / n), (float)(b / n), 1f);
    }

    /// <summary>
    /// The scenario lobby's real input surface, walked with the buttons' own
    /// pointer clicks: the four game-type buttons (each click naming the shipped
    /// mode it is and the session this port does not have) and the Start row (its
    /// click naming that there is no session to start) - every one through the
    /// screen's one entry point recording its own row. The Back row, the click
    /// that leaves for the war screen, is made after the walk.
    /// </summary>
    private static void CheckScenarioLobbyWalk()
    {
      int startDispatches = ScenarioLobbyActions.DispatchCount;
      int clicks = 0;
      int dispatchedTheirOwnRow = 0;

      foreach (ScenarioLobbyAction gameType in ScenarioLobbyActions.GameTypes)
      {
        ClickScenarioLobbyButton(gameType, ref clicks, ref dispatchedTheirOwnRow);
        Check(ReportHas($"{gameType.Id} '{gameType.Label}'"),
              $"the lobby's {gameType.Label} game-type button's click dispatched {gameType.Id} "
              + $"'{gameType.Label}' through its one entry point, naming the shipped mode it is");
      }

      ClickScenarioLobbyButton(ScenarioLobbyActions.Start, ref clicks, ref dispatchedTheirOwnRow);
      Check(ReportHas($"{ScenarioLobbyActions.Start.Id} '{ScenarioLobbyActions.Start.Label}'")
            && ReportHas("no session to start"),
            "and its Start row's click names that the lobby has no session to start - the port has no "
            + "online service and runs no lobby - rather than doing nothing");

      Check(clicks > 0 && dispatchedTheirOwnRow == clicks,
            $"the screen's {clicks} clicks (four game types and the Start row) each dispatched their "
            + $"OWN row through the screen's one entry point ({dispatchedTheirOwnRow}/{clicks})");

      Line($"scenario lobby: the row walk: {ScenarioLobbyActions.DispatchCount - startDispatches} clicks "
           + "through the screen's one entry point - the four game types and the Start row, each naming "
           + "what it is and the session this port does not have; the Back row, the way back, is next.");
    }

    /// <summary>One lobby row's pointer click, counted into the walk's totals.</summary>
    private static void ClickScenarioLobbyButton(ScenarioLobbyAction row, ref int clicks,
                                                 ref int dispatchedTheirOwnRow)
    {
      GameObject buttonObject = GameObject.Find(row.ButtonName);
      Button button = buttonObject != null ? buttonObject.GetComponent<Button>() : null;
      if (button == null)
      {
        Fail($"the scenario lobby's {row.Label} button ({row.ButtonName}) is not on the screen, so its "
             + "click could not be made");
        return;
      }

      int before = ScenarioLobbyActions.DispatchCount;
      PressButton(button);
      clicks++;
      if (ScenarioLobbyActions.DispatchCount == before + 1
          && ScenarioLobbyActions.LastDispatch.Id == row.Id)
      {
        dispatchedTheirOwnRow++;
      }
    }

    private static void TickMenuBack()
    {
      if (menuBackChecked) return;

      Game game = Game.GetInstance();
      GameObject menu = GameObject.Find("MainMenuUI");
      GameObject play = GameObject.Find("PlayScreenUI");

      // The click asks; the flow rebuilds the menu on its next Update.
      if (game == null || game.State != GameState.MAIN_MENU || menu == null || play != null)
      {
        Expired(PlayScreenBudgetSeconds, "the menu never came back after Previous Menu");
        return;
      }

      menuBackChecked = true;
      NoteFrontendState(game.State);  // the trip's last state: the click reached the menu

      Check(ReportHas("Game flow: PLAY_SCREEN -> MAIN_MENU."),
            "the Previous Menu click brought the flow back to MAIN_MENU and the play screen "
            + "is gone (the console logged PLAY_SCREEN -> MAIN_MENU, the PlayScreenUI root "
            + "is destroyed, and the menu root is back)");

      int buttons = 0;
      int buttonsWithSprites = 0;
      Transform column = FindMenuColumn();
      if (column != null)
      {
        foreach (Transform child in column)
        {
          if (!child.name.EndsWith("Button")) continue;
          buttons++;
          UnityEngine.UI.Image image = child.GetComponent<UnityEngine.UI.Image>();
          if (image != null && image.sprite != null) buttonsWithSprites++;
        }
      }
      Check(buttons == 6 && buttonsWithSprites == 6,
            $"and the menu that came back is the six-button menu again "
            + $"({buttonsWithSprites}/{buttons} buttons with their shipped sprites)");

      // The record describes the menu that is ON SCREEN (MainMenuUI.Show calls
      // MainMenuActions.ResetRecord), so the rebuilt menu starts a fresh one -
      // which is also why the six-click checks above were made before this
      // click, while the first menu was still up.
      Check(MainMenuActions.DispatchCount == 0 && !MainMenuActions.HasDispatch,
            $"and the rebuilt menu starts its own dispatch record "
            + $"({MainMenuActions.DispatchCount} dispatch(es) since the menu was rebuilt)");

      CheckTheFrontendRoundTrip();
      CheckTheFrontendCueSurvivedTheRoundTrip();

      // The round trip is judged; NOW the chapter start - the one frontend
      // click that leaves the frontend's tree. The menu is freshly rebuilt and
      // this is its Play click again: the walk below drives Play -> War ->
      // Campaign and clicks 'Chapter 4', and run 2 (the dev harness, the
      // switch's other path) begins after the loaded chapter's world has been
      // judged (TickChapterMap) - so a failure there is this run's failure.
      Line("--- the chapter start: the campaign screen's 'Chapter 4' clicked for real");
      SessionState.SetInt(ChapterWalkStepKey, 0);
      ClickMenuPlayButton();
      BeginPhase(Phase.ChapterWalk, PlayScreenBudgetSeconds);
    }

    /// <summary>
    /// The chapter-start walk, back at the menu: the same three clicks the round
    /// trip made (the menu's Play - already made by TickMenuBack - then the war
    /// screen's Campaign row), and then a CHAPTER row - the one click in this
    /// frontend that leaves it. The step lives in SessionState so each wait gets
    /// its own budget and a domain reload cannot lose the place; every click is
    /// the button's own pointer click through the screen's one entry point,
    /// exactly like the trip's.
    /// </summary>
    private static void TickChapterWalk()
    {
      Game game = Game.GetInstance();
      if (game == null) return;

      int step = SessionState.GetInt(ChapterWalkStepKey, 0);
      if (step == 0)
      {
        if (game.State != GameState.PLAY_SCREEN || GameObject.Find("PlayScreenUI") == null)
        {
          Expired(PlayScreenBudgetSeconds,
                  "the play screen never came up for the chapter-start walk");
          return;
        }

        Line("chapter walk : the play screen is up again; clicking War");
        SessionState.SetInt(ChapterWalkStepKey, 1);
        ClickPlayScreenRow(PlayScreenAction.War, Phase.ChapterWalk);
        return;
      }

      if (step == 1)
      {
        if (game.State != GameState.WAR_SCREEN || GameObject.Find("WarScreenUI") == null)
        {
          Expired(PlayScreenBudgetSeconds,
                  "the war screen never came up for the chapter-start walk");
          return;
        }

        Line("chapter walk : the war screen is up again; clicking Campaign");

        PathScreenRow campaignRow = WarScreenActions.Picture(0);
        SessionState.SetInt(ChapterWalkStepKey, 2);
        ClickDepartingRow("war", "WarScreenActions", campaignRow.ButtonName, campaignRow.Id,
                          campaignRow.Label, campaignRow.Outcome, "Campaign",
                          () => WarScreenActions.DispatchCount,
                          () => WarScreenActions.LastDispatch.Id, Phase.ChapterWalk);
        return;
      }

      if (game.State != GameState.CAMPAIGN_SCREEN || GameObject.Find("CampaignScreenUI") == null)
      {
        Expired(PlayScreenBudgetSeconds,
                "the campaign screen never came up for the chapter-start walk");
        return;
      }

      CampaignScreenRow chapter = CampaignScreenActions.Chapter(ChapterStartIndex);
      Line($"chapter walk : the campaign screen is up again; clicking '{chapter.Label}' - "
           + "the click that leaves the frontend");
      SessionState.SetInt(ChapterWalkStepKey, 3);
      ClickDepartingRow("campaign", "CampaignScreenActions", chapter.ButtonName, chapter.Id,
                        chapter.Label, chapter.Outcome, "chapter start",
                        () => CampaignScreenActions.DispatchCount,
                        () => CampaignScreenActions.LastDispatch.Id, Phase.ChapterMap);
    }

    /// <summary>
    /// The clicked chapter's map, once the click has left the frontend. The
    /// click's own dispatch was judged by ClickDepartingRow; this phase judges
    /// the FLOW and the WORLD: the two logged transitions
    /// (CAMPAIGN_SCREEN -> MAP_LOADING -> MAP_LOADED), the map the flow says it
    /// loaded (the clicked row's own file), and then the world the load built -
    /// read through S2MMapWorldLoader.Last and the scene's own roots - against
    /// the port's MANIFESTS: the map's scenario census (scenario_missions.csv /
    /// scenario_events.csv), its actor count (entity_estates.csv), its building
    /// placements (placement_verification.csv) and its wall classification
    /// (walls_war_chapter4.json, walls_parse.py's ground truth). Then the Game
    /// view capture, and the hand-over to run 2.
    /// </summary>
    private static void TickChapterMap()
    {
      if (!chapterMapChecked)
      {
        if (Expired(ChapterMapBudgetSeconds,
                    "the chapter's map never loaded after its row's click"))
          return;

        Game game = Game.GetInstance();
        // MAP_LOADED is one tick long now - the mission's briefing screen follows
        // it - so the world is judged on whichever of the two the runner lands
        // on (BRIEFING means it missed the single MAP_LOADED frame, which the
        // console's own transition lines cover either way).
        if (game == null
            || (game.State != GameState.MAP_LOADED && game.State != GameState.BRIEFING)) return;
        if (GameObject.Find("S2M Map World") == null) return;

        chapterMapChecked = true;

        Check(ReportHas("Game flow: CAMPAIGN_SCREEN -> MAP_LOADING.")
              && ReportHas("Game flow: MAP_LOADING -> MAP_LOADED."),
              "the chapter row's click moved the flow off the frontend and through the load "
              + "(the console logged CAMPAIGN_SCREEN -> MAP_LOADING -> MAP_LOADED, "
              + $"state={game.State})");
        Check(GameObject.Find("CampaignScreenUI") == null
              && GameObject.Find("WarScreenUI") == null
              && GameObject.Find("MainMenuUI") == null,
              "and every frontend screen is gone while the map is what is on "
              + "(the campaign, war and menu roots are all destroyed)");
        Check(game.CurrentMapPath == ChapterStartMapPath,
              $"the flow loaded the map the clicked row names ('{game.CurrentMapPath}')");

        CheckTheChapterWorld();

        FocusGameView();
        ScreenCapture.CaptureScreenshot(ChapterCapturePath);
        Line($"capture      : asked for the Game view render -> {ChapterCapturePath}");
        return;
      }

      if (!chapterCaptureChecked)
      {
        if (Elapsed() < CaptureWaitSeconds) return;
        chapterCaptureChecked = true;

        int colours = CheckCapture(ChapterCapturePath, "the loaded chapter's Game view capture");
        Check(colours >= 8,
              $"the loaded chapter renders its own map ({colours} distinct colours - terrain, "
              + "water and the chapter's placements; the capture is taken with the mission's "
              + $"briefing screen over it), captured to {ChapterCapturePath}");

        MemoryLine("after run 1's chapter map load");

        // The mission's briefing screen - the game's own screen at mission start,
        // and the step the shipped game takes between the map and the player's
        // first order. Run 2 (the switch's other path) follows ITS own checks.
        Line("--- the mission's briefing screen (the chapter's own picture, title and hints)");
        SessionState.SetInt(BriefingStepKey, 0);
        BeginPhase(Phase.Briefing, BriefingBudgetSeconds);
      }
    }

    /// <summary>
    /// The mission's briefing screen (GameState.BRIEFING), the game's own screen
    /// the clicked chapter's map puts up: the chapter's picture, its title on the
    /// picture's ribbon, its hints, the shipped screen's three tabs and the
    /// 'To the Game' row that leaves it for the mission.
    ///
    /// It is judged the way the frontend's screens are, against the INSTALL: the
    /// chapter's content (picture, title, hints) against the install's own files
    /// and i18n/en-us.xml, the picture by file name AND by pixels (read back with
    /// ImageConversion.LoadImage, the campaign screen's own technique), the rows
    /// against the table read at run time, and the tabs/paging as a real input
    /// surface driven through each button's own pointer click. The gaps the
    /// checks found - no objective prose for the war chapters, and the hint
    /// family read by the rows' own names - are said on the report, not invented
    /// away.
    ///
    /// The step lives in SessionState so each wait gets its own budget and a
    /// domain reload cannot lose the place: 0 = judge the screen (which clicks
    /// every tab and both medallions itself and asks for the capture), 1 = read
    /// the capture back, then click 'To the Game'.
    /// </summary>
    private static void TickBriefing()
    {
      Game game = Game.GetInstance();
      if (game == null) return;

      int step = SessionState.GetInt(BriefingStepKey, 0);

      if (step == 0)
      {
        if (Expired(BriefingBudgetSeconds,
                    "the mission's briefing screen never came up after the map loaded"))
          return;
        if (game.State != GameState.BRIEFING) return;
        BriefingScreenUI briefing = game.briefingScreenUI;
        if (briefing == null || !briefing.IsShown
            || GameObject.Find("BriefingScreenUI") == null) return;

        SessionState.SetInt(BriefingStepKey, 1);
        CheckBriefingScreen(game, briefing);

        // Everything the screen's clicks changed is in place; the capture is of
        // the screen a player sits in front of, so it is asked for last.
        FocusGameView();
        ScreenCapture.CaptureScreenshot(BriefingCapturePath);
        Line($"capture      : asked for the briefing screen's Game view render -> {BriefingCapturePath}");
        BeginPhase(Phase.Briefing, CaptureWaitSeconds + 5);
        return;
      }

      if (briefingCaptureChecked) return;
      if (Elapsed() < CaptureWaitSeconds) return;
      briefingCaptureChecked = true;

      int colours = CheckCapture(BriefingCapturePath, "the briefing screen's Game view capture");
      Check(colours >= 8,
            $"the mission's briefing screen renders ({colours} distinct colours - the "
            + $"chapter's own picture and the shipped screen's own controls), captured to "
            + $"{BriefingCapturePath}");

      Line("briefing     : clicking 'To the Game' - the one row that leaves the screen");
      SessionState.SetInt(BriefingLeaveCountKey, BriefingScreenActions.DispatchCount);
      ClickBriefingButton(BriefingScreenActions.Leave.ButtonName);
      BeginPhase(Phase.AfterBriefing, PlayScreenBudgetSeconds);
    }

    /// <summary>
    /// The flow after 'To the Game': the briefing is gone and the map is the
    /// game's - the main screen comes up on the next tick (the next phase), and
    /// run 2 - the switch's other path, the one every earlier check used -
    /// waits until the main screen's own round trip is judged.
    /// </summary>
    private static void TickAfterBriefing()
    {
      if (briefingLeaveChecked) return;

      Game game = Game.GetInstance();
      if (game == null) return;

      if (!ReportHas("Game flow: BRIEFING -> GAMEPLAY."))
      {
        Expired(PlayScreenBudgetSeconds,
                "the briefing's 'To the Game' row never moved the flow off it");
        return;
      }

      briefingLeaveChecked = true;
      int countAtClick = SessionState.GetInt(BriefingLeaveCountKey, -1);
      Check(BriefingScreenActions.DispatchCount == countAtClick + 1
            && BriefingScreenActions.LastDispatch.Kind == BriefingActionKind.Leave
            && BriefingScreenActions.LastDispatch.Id == BriefingScreenActions.Leave.Id,
            $"'To the Game' came through the screen's one entry point and named its own row "
            + $"({BriefingScreenActions.DispatchCount} dispatch(es) on the screen: "
            + $"'{BriefingScreenActions.LastDispatch.Label}')");
      Check(game.State == GameState.GAMEPLAY,
            "and it left the briefing through the one state path "
            + $"(the console logged BRIEFING -> GAMEPLAY, state={game.State})");
      Check(GameObject.Find("BriefingScreenUI") == null,
            "the briefing screen is gone (its root is destroyed - the map is what is on)");
      Check(GameObject.Find("S2M Map World") != null,
            "and the chapter's map world still stands behind it");

      MemoryLine("after run 1's briefing screen");

      // The game's own first surface comes up on the next tick: the mission's
      // main screen, judged next - run 2 waits until its round trip is done.
      BeginPhase(Phase.MainScreen, PlayScreenBudgetSeconds);
    }

    private const string BriefingLeaveCountKey = "sh2.S2MGameFlowRun.BriefingLeaveCount";

    /// <summary>
    /// The briefing screen itself - the chapter it resolved, what it is made of,
    /// and the three things it must not be: another screen, a blank picture, or
    /// text the install does not ship.
    /// </summary>
    private static void CheckBriefingScreen(Game game, BriefingScreenUI briefing)
    {
      briefingChecked = true;

      Check(ReportHas("Game flow: MAP_LOADED -> BRIEFING."),
            "the loaded map handed the flow to the mission's briefing "
            + "(the console logged MAP_LOADED -> BRIEFING, state=" + game.State + ")");
      Check(GameObject.Find("MainMenuUI") == null
            && GameObject.Find("WarScreenUI") == null
            && GameObject.Find("CampaignScreenUI") == null
            && GameObject.Find("PlayScreenUI") == null,
            "and the frontend is still down under it (no menu, war, campaign or play screen root)");
      Check(GameObject.Find("S2M Map World") != null,
            "while the chapter's map world stands behind the briefing");

      // The chapter the flow is playing, and the content the install ships for it.
      S2MChapterBriefing chapter = briefing.Chapter;
      Check(chapter != null && chapter.Chapter == ChapterStartIndex + 1,
            $"the briefing is the clicked chapter's (chapter {chapter?.Chapter} of "
            + $"'{chapter?.MapStem}', the flow's own '{game.CurrentMapPath}')");
      Check(chapter != null && chapter.MapPath == ChapterStartMapPath,
            $"and it was resolved from the map the clicked row named "
            + $"('{chapter?.MapPath}' = {ChapterStartMapPath})");

      Dictionary<string, string> shipped = ReadShippedFrontendStrings();

      // The title: the install's own row, read at run time - the one per-chapter
      // line the install ships.
      string shippedTitle = shipped.TryGetValue(ChapterTitleRow, out string title) ? title : null;
      Check(shippedTitle != null && briefing.TitleText == shippedTitle
            && chapter.Title == shippedTitle,
            $"the title on the picture's ribbon is the install's own {ChapterTitleRow} row "
            + $"('{briefing.TitleText}' = '{shippedTitle}')");

      // The picture: the chapter's own file, named AND drawn - the sprite's own
      // name, then the pixels themselves against the install's file read back.
      string picturePath = Path.Combine(GameRoot,
        $"ui/chapter_introductions/chapter{ChapterStartIndex + 1:D2}_intro.jpg");
      Check(chapter.PicturePath == picturePath,
            $"the backdrop is the chapter's own picture ('{chapter.PicturePath}')");
      CheckBriefingPictureIsTheInstalls(picturePath);

      // The hints: every non-empty WARHINT03_* row, in slot order, with the
      // install's own text - and the family the screen says it read them by.
      var expectedHints = new List<string>();
      for (int slot = 0; slot < S2MChapterBriefing.HintSlots; slot++)
      {
        if (shipped.TryGetValue($"{ChapterHintFamily}_{slot}", out string hint)
            && !string.IsNullOrEmpty(hint))
        {
          expectedHints.Add(hint);
        }
      }

      var mismatches = new List<string>();
      for (int i = 0; i < Mathf.Max(expectedHints.Count, chapter.Hints.Count); i++)
      {
        string expected = i < expectedHints.Count ? expectedHints[i] : "<none>";
        string found = i < chapter.Hints.Count ? chapter.Hints[i].Text : "<none>";
        if (expected != found) mismatches.Add($"[{i}] shipped='{expected}' screen='{found}'");
      }
      Check(mismatches.Count == 0 && expectedHints.Count > 0,
            $"the chapter's hints are the install's own {ChapterHintFamily}_0..7 rows "
            + $"({chapter.Hints.Count}/{expectedHints.Count} agree, in slot order"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");
      Check(chapter.HintsFamily == $"{ChapterHintFamily}_0..{S2MChapterBriefing.HintSlots - 1}",
            $"and the screen names the family it read them by ('{chapter.HintsFamily}')");

      // The screen's own rows, against the table at run time - the same authority
      // check the frontend's screens get.
      string[] tabRows = { "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_OBJECTIVE",
                           "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_HINTS",
                           "MISSIONOBJECTIVESSCREENSTATICTOOLTIP_TUTORIAL" };
      var rowMismatches = new List<string>();
      for (int i = 0; i < tabRows.Length; i++)
      {
        string expected = shipped.TryGetValue(tabRows[i], out string text) ? text : "<no such row>";
        string found = BriefingScreenActions.Tabs[i].Label;
        if (expected != found)
          rowMismatches.Add($"{tabRows[i]} shipped='{expected}' port='{found}'");
      }
      string leaveId = BriefingScreenActions.Leave.Id;
      string leaveExpected = shipped.TryGetValue(leaveId, out string leaveText)
        ? leaveText : "<no such row>";
      if (leaveExpected != BriefingScreenActions.Leave.Label)
        rowMismatches.Add($"{leaveId} shipped='{leaveExpected}' port='{BriefingScreenActions.Leave.Label}'");
      Check(rowMismatches.Count == 0,
            $"the screen's four named rows are the install's own "
            + $"(3 MISSIONOBJECTIVESSCREENSTATICTOOLTIP_* tabs + '{leaveId}' "
            + $"'{BriefingScreenActions.Leave.Label}' agree with i18n/en-us.xml"
            + (rowMismatches.Count > 0 ? "; " + string.Join("; ", rowMismatches) : "") + ")");

      // Every row of the screen is a button in the scene, under its own name.
      // The search includes INACTIVE objects: the two medallions are only on
      // screen while the hints tab is (they page it), which the tab walk below
      // checks where it means something.
      var buttonNames = new List<string>();
      GameObject briefingRoot = GameObject.Find("BriefingScreenUI");
      if (briefingRoot != null)
      {
        foreach (Button sceneButton in briefingRoot.GetComponentsInChildren<Button>(true))
        {
          buttonNames.Add(sceneButton.gameObject.name);
        }
      }
      var missing = new List<string>();
      foreach (BriefingAction action in BriefingScreenActions.All)
      {
        if (!buttonNames.Contains(action.ButtonName)) missing.Add(action.ButtonName);
      }
      Check(missing.Count == 0 && buttonNames.Count >= BriefingScreenActions.All.Length,
            $"all {BriefingScreenActions.All.Length} of its rows are buttons in the scene, each "
            + $"under its own name ({buttonNames.Count} button(s) under the screen's root; "
            + $"missing: {(missing.Count == 0 ? "none" : string.Join(", ", missing))})");

      // What the screen cannot fill, said rather than invented: the objective
      // prose the war chapters do not ship, and the hint family's attribution.
      Line($"briefing     : objective text: {S2MChapterBriefing.ObjectiveTextNote}");
      Line($"briefing     : hints: {S2MChapterBriefing.HintAttributionNote}");
      Line($"briefing     : the screen's own record: '{briefing.ActiveTab.Label}' tab, bar "
           + $"'{briefing.BarText}', {chapter.Hints.Count} hint(s)");
      if (!string.IsNullOrEmpty(chapter.Note))
      {
        Line($"briefing     : what the install did not name: {chapter.Note}");
      }

      CheckBriefingTabs(briefing);
    }

    /// <summary>
    /// The chapter's picture, drawn on the screen, against the install's own file
    /// for that chapter: the backdrop sprite's own name, then the pixels - the
    /// install's jpg read back with ImageConversion.LoadImage (the campaign
    /// screen's technique) and sampled on a grid against the screen's texture.
    /// JPEG and the GPU's own filtering move a channel by a little, so the bar is
    /// a mean absolute difference over the sampled points, not equality; a
    /// different picture (or a blank one) misses it by orders of magnitude.
    /// </summary>
    private static void CheckBriefingPictureIsTheInstalls(string picturePath)
    {
      if (!File.Exists(picturePath))
      {
        Fail($"the install ships no chapter picture at {picturePath}");
        return;
      }

      GameObject contentRoot = GameObject.Find("BriefingScreenUIContentRoot");
      Image backdrop = null;
      if (contentRoot != null)
      {
        Transform found = contentRoot.transform.Find("BriefingScreenUIBackdrop");
        if (found != null) backdrop = found.GetComponent<Image>();
      }
      if (backdrop == null || backdrop.sprite == null || backdrop.sprite.texture == null)
      {
        Fail("the briefing screen has no backdrop sprite to compare with the install's picture");
        return;
      }

      var reference = new Texture2D(2, 2, TextureFormat.ARGB32, false);
      bool loaded = ImageConversion.LoadImage(reference, File.ReadAllBytes(picturePath));
      Check(loaded,
            $"the install's own chapter picture loads ({Path.GetFileName(picturePath)}, "
            + $"{reference.width}x{reference.height})");
      if (!loaded) return;

      Texture2D drawn = backdrop.sprite.texture;
      // The loader names the TEXTURE after the file it read (SpriteLoader), and
      // the sprite it builds from it is what the screen draws - so the name is
      // read off the texture, which is the object that came from the file.
      Check(drawn != null && drawn.name == Path.GetFileName(picturePath),
            $"and the screen's backdrop texture IS that file by name ('{drawn?.name}')");
      Check(drawn != null && drawn.width == reference.width && drawn.height == reference.height,
            $"and by size (screen {drawn?.width}x{drawn?.height}, install "
            + $"{reference.width}x{reference.height})");
      if (drawn == null) return;

      // A 4x4 grid of samples, each the mean of a 9x9 block at the same relative
      // position in both textures (the screen's own y is bottom-up, like Unity's).
      double totalDifference = 0;
      int samples = 0;
      for (int gy = 0; gy < 4; gy++)
      {
        for (int gx = 0; gx < 4; gx++)
        {
          float u = (gx + 0.5f) / 4f;
          float v = (gy + 0.5f) / 4f;
          totalDifference += MeanBlockDifference(drawn, reference, u, v);
          samples++;
        }
      }

      double meanDifference = samples == 0 ? 1 : totalDifference / samples;
      Check(meanDifference < 0.08,
            $"and the pixels on the screen are that picture's ({samples} grid samples, mean "
            + $"absolute channel difference {meanDifference:F4} of 1.0 - a blank or other "
            + "picture misses this bar by orders of magnitude)");

      reference.name = "briefing picture reference";
      UnityEngine.Object.Destroy(reference);
    }

    /// <summary>The mean absolute channel difference of a 9x9 block, 0..1, at one relative position.</summary>
    private static double MeanBlockDifference(Texture2D drawn, Texture2D reference, float u, float v)
    {
      double total = 0;
      int count = 0;
      for (int dy = -4; dy <= 4; dy++)
      {
        for (int dx = -4; dx <= 4; dx++)
        {
          int rx = Mathf.Clamp(Mathf.RoundToInt(u * reference.width) + dx, 0, reference.width - 1);
          int ryBottomUp = Mathf.Clamp(Mathf.RoundToInt(v * reference.height) + dy, 0, reference.height - 1);
          Color referenceColour = reference.GetPixel(rx, ryBottomUp);

          // The reference is read with its own origin at the bottom-left (this
          // sampling is in the same space), so the screen's texture uses the
          // same coordinates - no flip: both are the file, bottom-up.
          int sx = Mathf.Clamp(Mathf.RoundToInt(u * drawn.width) + dx, 0, drawn.width - 1);
          int sy = Mathf.Clamp(Mathf.RoundToInt(v * drawn.height) + dy, 0, drawn.height - 1);
          Color drawnColour = drawn.GetPixel(sx, sy);

          total += Math.Abs(referenceColour.r - drawnColour.r)
                 + Math.Abs(referenceColour.g - drawnColour.g)
                 + Math.Abs(referenceColour.b - drawnColour.b);
          count += 3;
        }
      }
      return count == 0 ? 1 : total / count;
    }

    /// <summary>Whether a briefing-screen button is on the screen (its object active).</summary>
    private static bool IsBriefingButtonActive(string buttonName)
    {
      GameObject root = GameObject.Find("BriefingScreenUI");
      if (root == null) return false;
      foreach (Button sceneButton in root.GetComponentsInChildren<Button>(true))
      {
        if (sceneButton.gameObject.name == buttonName) return sceneButton.gameObject.activeInHierarchy;
      }
      return false;
    }

    /// <summary>Clicks a briefing-screen button by the name its row carries in the scene.</summary>
    private static void ClickBriefingButton(string buttonName)
    {
      GameObject buttonObject = GameObject.Find(buttonName);
      if (buttonObject == null)
      {
        Fail($"the briefing screen has no '{buttonName}' button to click");
        return;
      }

      Button button = buttonObject.GetComponent<Button>();
      if (button == null)
      {
        Fail($"'{buttonName}' is in the scene but is not a button");
        return;
      }

      // The same call the input module makes on release, so Selectable's
      // interactable gate, Button.Press() and the button's own UnityEvent are all
      // the game's own code (the frontend screens' clicks are driven the same way).
      ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current),
                            ExecuteEvents.pointerClickHandler);
    }

    /// <summary>
    /// The three shipped tabs as a real input surface: each one is clicked
    /// through its own button and must (a) be the row the screen's one entry
    /// point recorded - so a button wired to another row fails here - and (b)
    /// switch the panel to its own content, which for this chapter is the title
    /// (Objective), the first shipped hint (Hints and Tips) and nothing
    /// (Tutorial, whose text the install does not ship per mission).
    /// </summary>
    private static void CheckBriefingTabs(BriefingScreenUI briefing)
    {
      if (briefingTabsChecked) return;
      briefingTabsChecked = true;

      // The shipped screen's own first tab is the one the screen opens on; its
      // button is clicked anyway, because a tab that is on by default must still
      // be a real control (the click must reach the one entry point and name its
      // own row).
      Check(briefing.ActiveTabIndex == 0 && briefing.BarText == briefing.TitleText,
            "the screen opens on the shipped screen's own first tab, the Objective panel, "
            + $"carrying the chapter's title ('{Shorten(briefing.BarText)}')");
      Check(!IsBriefingButtonActive(BriefingScreenActions.PageBack.ButtonName)
            && !IsBriefingButtonActive(BriefingScreenActions.PageNext.ButtonName),
            "and the two medallions are off the screen while there are no hints to page "
            + "(they are the hints tab's own controls)");

      var before = BriefingScreenActions.DispatchCount;
      ClickBriefingButton(BriefingScreenActions.Objective.ButtonName);
      Check(BriefingScreenActions.DispatchCount == before + 1
            && BriefingScreenActions.LastDispatch.Id == BriefingScreenActions.Objective.Id
            && briefing.ActiveTabIndex == 0,
            "and its own button clicks through the screen's one entry point and names its row "
            + $"({BriefingScreenActions.DispatchCount} dispatch(es) so far: "
            + $"'{BriefingScreenActions.LastDispatch.Label}')");

      // Hints and Tips.
      before = BriefingScreenActions.DispatchCount;
      ClickBriefingButton(BriefingScreenActions.Hints.ButtonName);
      string expectedHint = briefing.Chapter.Hints.Count > 0 ? briefing.Chapter.Hints[0].Text : "";
      Check(BriefingScreenActions.DispatchCount == before + 1
            && BriefingScreenActions.LastDispatch.Id == BriefingScreenActions.Hints.Id
            && briefing.ActiveTabIndex == 1,
            "the Hints and Tips click is its own row and switches the panel to it "
            + $"({BriefingScreenActions.DispatchCount} dispatch(es), tab "
            + $"'{briefing.ActiveTab.Label}')");
      Check(briefing.BarText == expectedHint && briefing.BarText.Length > 0,
            $"and the shipped hint bar carries the chapter's own first hint "
            + $"('{Shorten(briefing.BarText)}')");
      Check(IsBriefingButtonActive(BriefingScreenActions.PageBack.ButtonName)
            && IsBriefingButtonActive(BriefingScreenActions.PageNext.ButtonName),
            "with its two medallions on the screen now that there are, and a real button "
            + "either side of the bar");

      // Tutorial - the one tab the install ships no per-mission text for.
      before = BriefingScreenActions.DispatchCount;
      ClickBriefingButton(BriefingScreenActions.Tutorial.ButtonName);
      Check(BriefingScreenActions.DispatchCount == before + 1
            && BriefingScreenActions.LastDispatch.Id == BriefingScreenActions.Tutorial.Id
            && briefing.ActiveTabIndex == 2,
            "the Tutorial click is its own row and switches the panel to it "
            + $"({BriefingScreenActions.DispatchCount} dispatch(es), tab "
            + $"'{briefing.ActiveTab.Label}')");
      Check(briefing.BarText.Length == 0,
            "and its panel carries no text - which is what the install ships for it (its "
            + "tutorial lines are speech rows, Tutorial_1..86.wav, never per-mission text), "
            + "named on the report rather than invented");

      // Back to the hints for the paging step, which is where the screen is left.
      ClickBriefingButton(BriefingScreenActions.Hints.ButtonName);
      CheckBriefingPages(briefing);
    }

    /// <summary>
    /// The two medallions as a real input surface: on the hints tab, 'Next' walks
    /// on through the chapter's shipped hints and 'Back' walks back - the bar's
    /// text is what says which hint is on, so the check is the install's own
    /// sentences, not a counter.
    /// </summary>
    private static void CheckBriefingPages(BriefingScreenUI briefing)
    {
      if (briefingPagesChecked) return;
      briefingPagesChecked = true;

      int hintCount = briefing.Chapter.Hints.Count;
      Check(hintCount >= 2,
            $"the chapter ships more than one hint to page ({hintCount} of "
            + $"{S2MChapterBriefing.HintSlots} slots)");

      briefingHintAtFirstPage = briefing.HintIndex;
      int before = BriefingScreenActions.DispatchCount;
      ClickBriefingButton(BriefingScreenActions.PageNext.ButtonName);
      Check(BriefingScreenActions.DispatchCount == before + 1
            && BriefingScreenActions.LastDispatch.Kind == BriefingActionKind.PageNext,
            "the medallion beside the bar pages on through the chapter's own hints "
            + $"({BriefingScreenActions.DispatchCount} dispatch(es))");
      Check(briefing.HintIndex == briefingHintAtFirstPage + 1
            && briefing.BarText == briefing.Chapter.Hints[briefing.HintIndex].Text,
            $"and the bar moves to the next shipped hint ('{Shorten(briefing.BarText)}')");

      before = BriefingScreenActions.DispatchCount;
      ClickBriefingButton(BriefingScreenActions.PageBack.ButtonName);
      Check(BriefingScreenActions.DispatchCount == before + 1
            && BriefingScreenActions.LastDispatch.Kind == BriefingActionKind.PageBack,
            "and the medallion on its other side pages back "
            + $"({BriefingScreenActions.DispatchCount} dispatch(es))");
      Check(briefing.HintIndex == briefingHintAtFirstPage
            && briefing.BarText == briefing.Chapter.Hints[briefingHintAtFirstPage].Text,
            $"to the hint it was on ('{Shorten(briefing.BarText)}')");
    }

    private static string Shorten(string text) =>
      text == null ? "" : (text.Length <= 60 ? text : text.Substring(0, 57) + "...");

    // ------------------------------------------------------------------
    // run 1, part six: the mission's MAIN SCREEN (GameState.GAMEPLAY) - the
    // game's own first surface - and the briefing's in-game review its one
    // control opens. 'To the Game' leaves the briefing (part five); this part
    // judges what the game itself shows in its place: the install's own status
    // bar (the strip across ui/ingameui.tga's top 45 rows) with the loaded
    // mission's own decoded numbers in its slots, its thirteen
    // MAINSCREENROLLOVERS rows against the install's table read at run time,
    // its one drawn control as a real input surface, the three slots the port
    // leaves as the install paints them (named, not invented) - and the round
    // trip GAMEPLAY -> BRIEFING -> GAMEPLAY through the Objectives row and the
    // review's 'Return to the Game'. Run 2 (the dev-harness path) starts only
    // after the round trip lands back on the main screen.
    // ------------------------------------------------------------------

    private static void TickMainScreen()
    {
      if (mainScreenChecked) return;

      Game game = Game.GetInstance();
      if (game == null) return;

      if (game.State != GameState.GAMEPLAY)
      {
        Expired(PlayScreenBudgetSeconds, "the flow never reached GAMEPLAY after the briefing left");
        return;
      }

      MainScreenUI screen = game.mainScreenUI;
      if (screen == null || !screen.IsShown || GameObject.Find("MainScreenUI") == null)
      {
        Expired(PlayScreenBudgetSeconds, "the mission's main screen never came up after the briefing");
        return;
      }

      mainScreenChecked = true;
      CheckMainScreen(game, screen);

      FocusGameView();
      ScreenCapture.CaptureScreenshot(MainScreenCapturePath);
      Line($"capture      : asked for the main screen's Game view render -> {MainScreenCapturePath}");
      BeginPhase(Phase.MainScreenCapture, CaptureWaitSeconds + 5);
    }

    /// <summary>
    /// The mission's main screen itself - the mission it resolved, what it is
    /// made of, and the three things it must not be: another screen, a bar the
    /// install does not ship, or numbers the mission's data does not carry.
    /// </summary>
    private static void CheckMainScreen(Game game, MainScreenUI screen)
    {
      Check(ReportHas("Game flow: BRIEFING -> GAMEPLAY."),
            "the briefing's own play row put the mission on the game's main screen "
            + $"(the console logged BRIEFING -> GAMEPLAY, state={game.State})");
      Check(GameObject.Find("MainMenuUI") == null
            && GameObject.Find("PlayScreenUI") == null
            && GameObject.Find("WarScreenUI") == null
            && GameObject.Find("PeaceScreenUI") == null
            && GameObject.Find("CampaignScreenUI") == null
            && GameObject.Find("BriefingScreenUI") == null,
            "and the frontend and the briefing are down under it (no menu, play, war, peace, "
            + "campaign or briefing root)");
      Check(GameObject.Find("S2M Map World") != null,
            "while the chapter's map world stands behind it");

      S2MMissionState mission = screen.Mission;
      Check(mission != null && mission.HasMission && mission.MapStem == "war_chapter4"
            && mission.MapPath == ChapterStartMapPath,
            $"the screen resolved the loaded mission's own state (chapter 4's first mission of "
            + $"'{mission?.MapStem}', the flow's own '{game.CurrentMapPath}')");

      // The screen's thirteen rows, against the table at run time - the same
      // authority check the frontend's screens and the briefing get.
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var rowMismatches = new List<string>();
      for (int i = 0; i < MainScreenActions.All.Length; i++)
      {
        string rowId = $"MAINSCREENROLLOVERS{79 + i:000}";
        string expected = shipped.TryGetValue(rowId, out string rowText) ? rowText : "<no such row>";
        string found = MainScreenActions.All[i].Label;
        if (expected != found) rowMismatches.Add($"{rowId} shipped='{expected}' port='{found}'");
      }
      Check(rowMismatches.Count == 0,
            $"its {MainScreenActions.All.Length} rows (six status fields, seven controls) are the "
            + $"install's own MAINSCREENROLLOVERS079..091 rows "
            + $"({MainScreenActions.All.Length - rowMismatches.Count}/{MainScreenActions.All.Length} agree"
            + (rowMismatches.Count > 0 ? "; " + string.Join("; ", rowMismatches) : "") + ")");

      CheckMainScreenStripIsTheInstalls();
      CheckMainScreenValues(screen, shipped);
      CheckMainScreenSlots();

      // The one drawn control, and the six the install's data cannot pin: the
      // atlas's icon buttons carry no labels, so which icon belongs to Delete
      // and which to Reports is NOT in the shipped data - those rows are named
      // on the report and left undrawn rather than given an invented pairing.
      string objectivesExpected = shipped.TryGetValue(MainScreenActions.Objectives.Id, out string objectivesText)
        ? objectivesText : "<no such row>";
      GameObject objectivesButton = GameObject.Find(MainScreenActions.ObjectivesButtonName);
      Check(objectivesButton != null && objectivesButton.GetComponent<Button>() != null
            && MainScreenActions.Objectives.Label == objectivesExpected
            && LabelTextOf(MainScreenActions.ObjectivesButtonName) == objectivesExpected,
            $"its one drawn control is the install's own {MainScreenActions.Objectives.Id} row "
            + $"('{objectivesExpected}'), a real button under its own name");
      var buttonNames = new List<string>();
      GameObject root = GameObject.Find("MainScreenUI");
      if (root != null)
      {
        foreach (Button sceneButton in root.GetComponentsInChildren<Button>(true))
        {
          buttonNames.Add(sceneButton.gameObject.name);
        }
      }
      Check(buttonNames.Count == 1 && buttonNames.Contains(MainScreenActions.ObjectivesButtonName),
            $"and it draws no button for its other {MainScreenActions.Controls.Length - 1} controls "
            + $"(the install pins no art for them), named instead: "
            + $"{buttonNames.Count} button(s) under the screen's root");
      foreach (MainScreenRow row in MainScreenActions.Controls)
      {
        if (row.Id != MainScreenActions.Objectives.Id)
        {
          Line($"main screen  : {row.Id} '{row.Label}' -> {MainScreenActions.Outcome(row)}");
        }
      }

      // What the screen cannot fill, said rather than invented.
      Line($"main screen  : {S2MMissionState.SlotAttributionNote}");
      Line($"main screen  : {S2MMissionState.RuntimeFieldsNote}");
      if (!string.IsNullOrEmpty(mission?.Note))
      {
        Line($"main screen  : {mission.MapStem}: {mission.Note}");
      }
    }

    /// <summary>
    /// The status bar, against the install's own file: the sprite's name, the
    /// strip it is cut from (ui/ingameui.tga's top 45 rows - the measurement is
    /// the runner's own copy, above), then the pixels - the atlas read back with
    /// the project's TGA reader and sampled on a grid across the strip in both
    /// textures. The same file read twice differs by nothing; the wrong region
    /// (or a blank bar) misses the bar by orders of magnitude.
    /// </summary>
    private static void CheckMainScreenStripIsTheInstalls()
    {
      string atlasPath = Path.Combine(GameRoot, "ui/ingameui.tga");
      if (!File.Exists(atlasPath))
      {
        Fail($"the install ships no status-bar atlas at {atlasPath}");
        return;
      }

      GameObject barObject = GameObject.Find("MainScreenUIBar");
      Image bar = barObject != null ? barObject.GetComponent<Image>() : null;
      if (bar == null || bar.sprite == null || bar.sprite.texture == null)
      {
        Fail("the main screen has no status-bar sprite to compare with the install's strip");
        return;
      }

      Texture2D drawn = bar.sprite.texture;
      Check(drawn.name == "ingameui.tga",
            $"the screen's status bar IS the install's own atlas by name ('{drawn.name}')");

      Rect strip = bar.sprite.textureRect;
      Check(Mathf.RoundToInt(strip.width) == StripWidthPx
            && Mathf.RoundToInt(strip.height) == StripHeightPx
            && Mathf.RoundToInt(strip.y) == drawn.height - StripHeightPx,
            $"and its sprite is the strip across the atlas's top {StripHeightPx} rows "
            + $"(rect {strip.width}x{strip.height} at y {strip.y} of {drawn.height})");

      Texture2D reference = TGALoader.LoadTGA(atlasPath);
      if (reference == null || reference.width != StripWidthPx)
      {
        Fail($"the install's atlas does not read back ({atlasPath})");
        return;
      }

      double totalDifference = 0;
      int samples = 0;
      for (int gy = 0; gy < 3; gy++)
      {
        for (int gx = 0; gx < 4; gx++)
        {
          float u = (gx + 0.5f) / 4f;
          float v = (gy + 0.5f) / 3f;
          int sx = Mathf.Clamp(Mathf.RoundToInt(strip.x + u * strip.width), 0, drawn.width - 1);
          int sy = Mathf.Clamp(Mathf.RoundToInt(strip.y + v * strip.height), 0, drawn.height - 1);
          int rx = Mathf.Clamp(Mathf.RoundToInt(u * StripWidthPx), 0, StripWidthPx - 1);
          int ry = Mathf.Clamp(Mathf.RoundToInt((reference.height - StripHeightPx) + v * StripHeightPx),
                               0, reference.height - 1);
          Color drawnColour = drawn.GetPixel(sx, sy);
          Color referenceColour = reference.GetPixel(rx, ry);
          totalDifference += Math.Abs(drawnColour.r - referenceColour.r)
                           + Math.Abs(drawnColour.g - referenceColour.g)
                           + Math.Abs(drawnColour.b - referenceColour.b);
          samples += 3;
        }
      }

      double meanDifference = samples == 0 ? 1 : totalDifference / samples;
      Check(meanDifference < 0.02,
            $"and the pixels on the screen are that strip's ({samples / 3} grid samples, mean "
            + $"absolute channel difference {meanDifference:F4} of 1.0 - a blank bar or the wrong "
            + "atlas region misses this bar by orders of magnitude)");

      UnityEngine.Object.Destroy(reference);
    }

    /// <summary>
    /// The three numbers the bar carries, each against a source that is not the
    /// screen: the Date's month is the install's own MONTH_JANUARY row and its
    /// year is the scenario's own - read a SECOND time, by the runner itself,
    /// straight from the map file the flow loaded - and Gold and Popularity are
    /// the scenario_missions.csv cells (the python toolchain's parse of the same
    /// map, an independent read the chapter checks already use).
    /// </summary>
    private static void CheckMainScreenValues(MainScreenUI screen, Dictionary<string, string> shipped)
    {
      string monthText = shipped.TryGetValue(ChapterStartMonthRow, out string month) ? month : null;
      string expectedDate = monthText == null ? null : $"{monthText} {ChapterStartYear}";

      string mapPath = Path.Combine(GameRoot, ChapterStartMapPath);
      int mapYear = -1, mapMonth = -1;
      try
      {
        S2MFile mapFile = S2MFileLoader.Load(mapPath);
        foreach (S2Object obj in mapFile.MapHeaderObjects)
        {
          if (obj is Scenario scenario)
          {
            mapYear = scenario.StartingYear;
            mapMonth = scenario.StartingMonth;
            break;
          }
        }
      }
      catch (Exception error)
      {
        Line($"main screen  : the runner's own read of {ChapterStartMapPath} failed ({error.Message})");
      }

      Check(expectedDate != null && screen.DateLabelText == expectedDate
            && LabelTextOf("MainScreenUIDateLabel") == expectedDate,
            $"the bar's Date slot carries the install's own {ChapterStartMonthRow} row and the map's "
            + $"own year ('{screen.DateLabelText}' = '{expectedDate}')");
      Check(mapYear == ChapterStartYear && mapMonth == ChapterStartMonthIndex,
            $"and the runner's own read of the map agrees the scenario starts "
            + $"{mapYear}-{mapMonth:00} ({ChapterStartYear}-{ChapterStartMonthIndex:00}; the shipped "
            + "manual's editor says the default date is January 1066 and the default maps store 1066-00, "
            + "which pins month 00 as January)");

      int manifestGold = ReadManifestInt("scenario_missions.csv", ChapterStartMapPath, "starting_gold");
      int manifestPopularity = ReadManifestInt("scenario_missions.csv", ChapterStartMapPath, "starting_popularity");
      Check(manifestGold >= 0 && screen.GoldLabelText == manifestGold.ToString()
            && LabelTextOf("MainScreenUIGoldLabel") == manifestGold.ToString(),
            $"the bar's Gold slot carries the mission's own starting gold from scenario_missions.csv "
            + $"('{screen.GoldLabelText}' = {manifestGold})");
      Check(manifestPopularity >= 0 && screen.PopularityLabelText == manifestPopularity.ToString()
            && LabelTextOf("MainScreenUIPopularityLabel") == manifestPopularity.ToString(),
            $"and its Popularity slot the mission's own starting popularity "
            + $"('{screen.PopularityLabelText}' = {manifestPopularity}), the same manifest");
    }

    /// <summary>
    /// The bar's slot layout: each value must sit inside the slot the strip
    /// paints for it (the runner's own measured spans, x px of 1024, above), and
    /// the other three painted slots must carry no value at all - the runtime's
    /// own state, which the mission's data does not hold. The measured frame is
    /// the BAR's own rect (a Screen Space Overlay canvas's world space IS screen
    /// pixels), so the check cannot be fooled by a canvas whose unit differs
    /// from the screen's.
    /// </summary>
    private static void CheckMainScreenSlots()
    {
      GameObject barObject = GameObject.Find("MainScreenUIBar");
      RectTransform barRect = barObject != null ? barObject.GetComponent<RectTransform>() : null;
      if (barRect == null)
      {
        Fail("the main screen has no status bar to measure its slots against");
        return;
      }

      var barCorners = new Vector3[4];
      barRect.GetWorldCorners(barCorners);
      float barLeft = barCorners[0].x;
      float barWidth = barCorners[3].x - barCorners[0].x;
      Canvas canvas = GameObject.Find("MainScreenUI")?.GetComponent<Canvas>();
      Line($"main screen  : {Screen.width}x{Screen.height} screen, canvas scale "
           + $"{canvas?.scaleFactor:F3}, the bar's own rect {barWidth:F0}x"
           + $"{barCorners[1].y - barCorners[0].y:F0} px");

      CheckSlot(barLeft, barWidth, "MainScreenUIDateLabel", DateSlotLeftPx, DateSlotRightPx, "Date");
      CheckSlot(barLeft, barWidth, "MainScreenUIGoldLabel", GoldSlotLeftPx, GoldSlotRightPx, "Gold");
      CheckSlot(barLeft, barWidth, "MainScreenUIPopularityLabel", PopularitySlotLeftPx, PopularitySlotRightPx, "Popularity");

      var valueLabels = new List<string>();
      GameObject root = GameObject.Find("MainScreenUI");
      if (root != null)
      {
        // UnityEngine.UI.Text qualified: inside Assets.Code.Stronghold2.* the
        // bare name Text is the sibling Stronghold2.Text namespace.
        foreach (UnityEngine.UI.Text label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
        {
          if (label.gameObject.name.StartsWith("MainScreenUI", StringComparison.Ordinal))
          {
            valueLabels.Add(label.gameObject.name);
          }
        }
      }
      Check(valueLabels.Count == 3
            && valueLabels.Contains("MainScreenUIDateLabel")
            && valueLabels.Contains("MainScreenUIGoldLabel")
            && valueLabels.Contains("MainScreenUIPopularityLabel"),
            "and the bar's other three slots (Current Rank, Honour, Peasants / Maximum Peasants) "
            + "are left as the install paints them - the mission's data carries no such state and "
            + "this port runs no simulation yet (named on the report, not filled)");
    }

    /// <summary>One value label, inside the painted span of the slot it belongs to (fractions of the bar's own width).</summary>
    private static void CheckSlot(float barLeft, float barWidth, string labelName,
                                  int leftPx, int rightPx, string what)
    {
      GameObject labelObject = GameObject.Find(labelName);
      RectTransform rect = labelObject != null ? labelObject.GetComponent<RectTransform>() : null;
      if (rect == null)
      {
        Fail($"the main screen's {what} slot has no label in the scene");
        return;
      }

      var corners = new Vector3[4];
      rect.GetWorldCorners(corners);
      float left = (corners[0].x - barLeft) / barWidth;
      float right = (corners[3].x - barLeft) / barWidth;
      Check(left >= (float)leftPx / StripWidthPx - 0.001f
            && right <= (float)rightPx / StripWidthPx + 0.001f,
            $"its {what} value sits inside the slot the strip paints for it "
            + $"(x {leftPx}..{rightPx} of {StripWidthPx}; the label spans "
            + $"{left:F4}..{right:F4} of the bar)");
    }

    /// <summary>The text one of the main screen's labels carries in the scene.</summary>
    private static string LabelTextOf(string objectName)
    {
      GameObject found = GameObject.Find(objectName);
      if (found == null) return null;
      // Qualified for the same reason as above: Text alone is the sibling namespace here.
      UnityEngine.UI.Text label = found.GetComponentInChildren<UnityEngine.UI.Text>(true);
      return label == null ? null : label.text;
    }

    /// <summary>Clicks a main-screen button by the name its row carries in the scene.</summary>
    private static void ClickMainScreenButton(string buttonName)
    {
      GameObject buttonObject = GameObject.Find(buttonName);
      if (buttonObject == null)
      {
        Fail($"the main screen has no '{buttonName}' button to click");
        return;
      }

      Button button = buttonObject.GetComponent<Button>();
      if (button == null)
      {
        Fail($"'{buttonName}' is in the scene but is not a button");
        return;
      }

      ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current),
                            ExecuteEvents.pointerClickHandler);
    }

    private static void TickMainScreenCapture()
    {
      if (mainScreenCaptureChecked) return;
      if (Elapsed() < CaptureWaitSeconds) return;
      mainScreenCaptureChecked = true;

      int colours = CheckCapture(MainScreenCapturePath, "the main screen's Game view capture");
      Check(colours >= 8,
            $"the mission's main screen renders (the install's own status bar over the loaded "
            + $"chapter's world, captured to {MainScreenCapturePath})");

      Line("main screen  : clicking its Objectives row - the one control it draws");
      SessionState.SetInt(MainScreenObjectivesCountKey, MainScreenActions.DispatchCount);
      ClickMainScreenButton(MainScreenActions.ObjectivesButtonName);
      BeginPhase(Phase.ObjectivesReview, PlayScreenBudgetSeconds);
    }

    /// <summary>
    /// The briefing screen's in-game review, which the Objectives click opened:
    /// the same chapter screen in its other showing, wearing the shipped
    /// screen's other play row.
    /// </summary>
    private static void TickObjectivesReview()
    {
      if (objectivesReviewChecked) return;

      Game game = Game.GetInstance();
      if (game == null) return;

      if (!ReportHas("Game flow: GAMEPLAY -> BRIEFING."))
      {
        Expired(PlayScreenBudgetSeconds, "the main screen's Objectives row never opened the mission objectives");
        return;
      }

      BriefingScreenUI briefing = game.briefingScreenUI;
      if (briefing == null || !briefing.IsShown
          || GameObject.Find("BriefingScreenUI") == null)
      {
        Expired(PlayScreenBudgetSeconds, "the mission objectives never came up as the in-game review");
        return;
      }

      objectivesReviewChecked = true;

      int countAtClick = SessionState.GetInt(MainScreenObjectivesCountKey, -1);
      Check(MainScreenActions.DispatchCount == countAtClick + 1
            && MainScreenActions.LastDispatch.Id == MainScreenActions.Objectives.Id
            && MainScreenActions.LastDispatch.Kind == MainScreenRowKind.Control,
            "the Objectives row came through the main screen's one entry point and named its own row "
            + $"({MainScreenActions.DispatchCount} dispatch(es) on the main screen: "
            + $"'{MainScreenActions.LastDispatch.Label}')");
      Check(game.State == GameState.BRIEFING && briefing.IsReview,
            "and it opened the mission objectives as the briefing screen's in-game review "
            + $"(the console logged GAMEPLAY -> BRIEFING, state={game.State}, review={briefing.IsReview})");
      Check(GameObject.Find("MainScreenUI") == null,
            "with the main screen taken down (the review is what is on)");
      Check(GameObject.Find("S2M Map World") != null,
            "while the chapter's map world still stands behind it");

      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      string returnExpected = shipped.TryGetValue(BriefingScreenActions.ReturnToGame.Id, out string returnText)
        ? returnText : "<no such row>";
      Check(GameObject.Find(BriefingScreenActions.ReturnToGame.ButtonName) != null
            && BriefingScreenActions.ReturnToGame.Label == returnExpected
            && LabelTextOf(BriefingScreenActions.ReturnToGame.ButtonName) == returnExpected,
            $"and the review wears the shipped screen's OTHER play row, "
            + $"{BriefingScreenActions.ReturnToGame.Id} ('{returnExpected}')");
      Check(GameObject.Find(BriefingScreenActions.Leave.ButtonName) == null,
            $"while the start briefing's own '{BriefingScreenActions.Leave.Label}' row is off the "
            + "screen - the two showings differ exactly in which play row they wear");
      Check(briefing.Chapter != null && briefing.Chapter.MapPath == ChapterStartMapPath
            && briefing.TitleText == (shipped.TryGetValue(ChapterTitleRow, out string title) ? title : null),
            $"and the review is the same chapter screen the briefing was ('{briefing.TitleText}', "
            + $"{briefing.Chapter?.MapPath})");

      Line("review       : clicking 'Return to the Game' - the shipped screen's other play row");
      SessionState.SetInt(BriefingReturnCountKey, BriefingScreenActions.DispatchCount);
      ClickBriefingButton(BriefingScreenActions.ReturnToGame.ButtonName);
      BeginPhase(Phase.BackFromReview, PlayScreenBudgetSeconds);
    }

    /// <summary>
    /// The round trip's landing: the review's own play row put the main screen
    /// back on the same map, and then run 2 - the switch's other path, the one
    /// every earlier check used.
    /// </summary>
    private static void TickBackFromReview()
    {
      if (backFromReviewChecked) return;

      Game game = Game.GetInstance();
      if (game == null) return;

      // The wait is the state itself (the phase's deadline is the budget): the
      // report already carries ONE 'BRIEFING -> GAMEPLAY' from the start
      // briefing's 'To the Game', so only the state and the rebuilt screen can
      // say the review's own return has landed.
      MainScreenUI screen = game.mainScreenUI;
      if (game.State != GameState.GAMEPLAY || screen == null || !screen.IsShown
          || GameObject.Find("MainScreenUI") == null)
      {
        Expired(PlayScreenBudgetSeconds, "the review's 'Return to the Game' row never brought the flow back");
        return;
      }

      backFromReviewChecked = true;

      Check(CountReportLines("Game flow: BRIEFING -> GAMEPLAY.") >= 2,
            "the console logged the review's own BRIEFING -> GAMEPLAY - the run's second, "
            + $"beside the start briefing's ({CountReportLines("Game flow: BRIEFING -> GAMEPLAY.")} in all)");

      int returnCountAtClick = SessionState.GetInt(BriefingReturnCountKey, -1);
      Check(BriefingScreenActions.DispatchCount == returnCountAtClick + 1
            && BriefingScreenActions.LastDispatch.Kind == BriefingActionKind.Leave
            && BriefingScreenActions.LastDispatch.Id == BriefingScreenActions.ReturnToGame.Id,
            "'Return to the Game' came through the briefing screen's one entry point and named its "
            + $"own row ({BriefingScreenActions.DispatchCount} dispatch(es) on the screen: "
            + $"'{BriefingScreenActions.LastDispatch.Label}')");
      Check(GameObject.Find("BriefingScreenUI") == null,
            "and the review is gone (its root is destroyed)");
      Check(screen.IsShown && MainScreenActions.DispatchCount == 0 && !MainScreenActions.HasDispatch,
            "while the main screen came back up with a fresh record - built again, exactly as a "
            + "screen the flow returns to is (its dispatch count is 0)");
      Check(GameObject.Find("S2M Map World") != null,
            "and the chapter's map world never left (the round trip was the game's own screen pair)");

      MemoryLine("after run 1's main screen and the briefing's in-game review");

      // Run 2 - the switch's other path, the one every earlier check used.
      WriteSwitch("dev");
      Line("--- phase 2: the dev-harness map path (switch = dev)");
      BeginPhase(Phase.ExitPlaymode, ExitPlaymodeSeconds);
      EditorApplication.ExitPlaymode();
    }

    /// <summary>
    /// The loaded chapter's world against the port's manifests - the load's own
    /// result (S2MMapWorldLoader.Last, the map file and the renderers' tallies)
    /// and the scene's roots (the same grandchildren counting run 2 uses). What
    /// each check is worth: the scenario census is the map's IDENTITY (chapter 4's
    /// own mission id and event count, python-parsed), the actor count and the
    /// placement count are the python parse of the same map, and the wall counts
    /// are walls_parse.py's classification of the same cells - so each is a
    /// cross-tool check of the port's load, not the port agreeing with itself.
    /// </summary>
    private static void CheckTheChapterWorld()
    {
      S2MMapWorldLoader.LoadedMap loaded = S2MMapWorldLoader.Last;
      if (loaded?.MapFile == null)
      {
        Fail("the chapter's map world exists but the loader kept no result to judge");
        return;
      }

      // The map's identity: its Scenario's own missions and events, counted off
      // the loaded objects and compared with the manifests' rows for this map.
      int missions = 0;
      int events = 0;
      foreach (S2Object obj in loaded.MapFile.MapHeaderObjects)
      {
        if (obj.Type == "Mission") missions++;
        else if (obj.Type == "ScenarioEvent") events++;
      }

      int manifestMissions = CountManifestRows("scenario_missions.csv", ChapterStartMapPath);
      int manifestEvents = CountManifestRows("scenario_events.csv", ChapterStartMapPath);
      Check(missions == manifestMissions && events == manifestEvents,
            $"the loaded map is the clicked chapter's own ({missions} mission(s) and {events} "
            + $"ScenarioEvent(s) in its MapHeader segment; scenario_missions.csv and "
            + $"scenario_events.csv carry {manifestMissions} and {manifestEvents} rows for "
            + $"{ChapterStartMapPath})");

      // Its actors: the scene's own count vs entity_estates.csv's actor_objects.
      int manifestActors = ReadManifestInt("entity_estates.csv", ChapterStartMapPath,
                                           "actor_objects");
      int sceneActors = Grandchildren("S2M Entities");
      Check(manifestActors >= 0 && sceneActors == manifestActors,
            $"and its actors are the manifest's own count ({sceneActors} under \"S2M Entities\", "
            + $"entity_estates.csv says {manifestActors} actor_objects for {ChapterStartMapPath})");

      // Its buildings and scenery: the scene's counts vs the two python parses
      // (placement_verification.csv's placements = the JSON's buildings; the
      // props and the scenery are the buildings JSON's own split, as run 2's
      // chapter-8 check documents).
      int manifestPlacements = ReadManifestInt("placement_verification.csv", ChapterStartMapPath,
                                               "placements");
      int sceneBuildings = Grandchildren("S2M Buildings");
      int sceneScenery = Grandchildren("S2M Scenery");
      Check(manifestPlacements >= 0 && sceneBuildings == manifestPlacements + ChapterPropInstances
            && sceneScenery == ChapterSceneryInstances,
            $"and its placements stand as the manifests describe them ({sceneBuildings} under "
            + $"\"S2M Buildings\" = {manifestPlacements} placements + {ChapterPropInstances} props, "
            + $"{sceneScenery} under \"S2M Scenery\" = the buildings JSON's scenery)");

      // Its walls: the renderer's own classification vs walls_parse.py's.
      WallsManifest walls = ReadWallsManifest("walls_war_chapter4.json");
      S2MWallRenderer.S2MWallCounts counts = loaded.Walls;
      if (walls == null)
      {
        Fail("walls_war_chapter4.json could not be read, so the wall classification was not judged");
      }
      else
      {
        Check(counts.Stone == walls.stats.by_kind.stone_wall
              && counts.Wooden == walls.stats.by_kind.wooden_wall
              && counts.Deck == walls.stats.by_kind.deck,
              $"and its wall cells classify as walls_parse.py classifies them "
              + $"({counts.Stone} stone / {counts.Wooden} wooden / {counts.Deck} deck in the engine, "
              + $"{walls.stats.by_kind.stone_wall} / {walls.stats.by_kind.wooden_wall} / "
              + $"{walls.stats.by_kind.deck} in walls_war_chapter4.json)");
      }
    }

    /// <summary>How many manifest rows (beyond the header) name this map in their first column.</summary>
    private static int CountManifestRows(string manifestName, string mapPath)
    {
      try
      {
        string mapFile = Path.GetFileName(mapPath);
        int count = 0;
        foreach (string line in File.ReadAllLines(Path.Combine(PortManifestsPath, manifestName)))
        {
          if (line.StartsWith(mapFile + ",", StringComparison.Ordinal)) count++;
        }
        return count;
      }
      catch (Exception error)
      {
        Line($"manifest     : {manifestName} unreadable ({error.Message})");
        return -1;
      }
    }

    /// <summary>One int cell of a manifest row (by header column name); -1 when unreadable.</summary>
    private static int ReadManifestInt(string manifestName, string mapPath, string column)
    {
      try
      {
        string mapFile = Path.GetFileName(mapPath);
        string[] lines = File.ReadAllLines(Path.Combine(PortManifestsPath, manifestName));
        if (lines.Length == 0) return -1;
        string[] header = lines[0].Split(',');
        int columnIndex = Array.IndexOf(header, column);
        if (columnIndex < 0) return -1;
        foreach (string line in lines)
        {
          if (!line.StartsWith(mapFile + ",", StringComparison.Ordinal)) continue;
          string[] cells = line.Split(',');
          return columnIndex < cells.Length && int.TryParse(cells[columnIndex], out int value)
            ? value : -1;
        }
        return -1;
      }
      catch (Exception error)
      {
        Line($"manifest     : {manifestName} unreadable ({error.Message})");
        return -1;
      }
    }

    /// <summary>The walls manifest of the started chapter, parsed with Unity's own JSON reader.</summary>
    private static WallsManifest ReadWallsManifest(string manifestName)
    {
      try
      {
        return JsonUtility.FromJson<WallsManifest>(
          File.ReadAllText(Path.Combine(PortArtifactsPath, manifestName)));
      }
      catch (Exception error)
      {
        Line($"manifest     : {manifestName} unreadable ({error.Message})");
        return null;
      }
    }

    /// <summary>walls_parse.py's per-map JSON, as far as this check reads it.</summary>
    [Serializable]
    private class WallsManifest
    {
      public WallsStats stats;
    }

    [Serializable]
    private class WallsStats
    {
      public int wall_cells;
      public WallsByKind by_kind;
    }

    [Serializable]
    private class WallsByKind
    {
      public int stone_wall;
      public int wooden_wall;
      public int deck;
    }

    /// <summary>
    /// The six menu clicks, judged once all six have happened: the five that
    /// report no destination were clicked with the menu up (CheckMenuButtons)
    /// and Play last, right before the screen it opens - so the batch count
    /// spans both, and this runs in the play-screen phase, where the sixth
    /// click has just been made.
    /// </summary>
    private static void CheckTheSixMenuClicks()
    {
      int dispatched = MainMenuActions.DispatchCount - menuBatchStart;
      int distinct = new HashSet<MainMenuAction>(menuBatchActions).Count;
      Check(dispatched == MainMenuActions.All.Length
            && distinct == MainMenuActions.All.Length,
            $"all six clicks arrived through the one entry point, one action each "
            + $"({dispatched} dispatch(es) for {menuBatchActions.Count} click(s), "
            + $"{distinct} distinct action(s))");

      int named = 0;
      foreach (MainMenuActionInfo info in MainMenuActions.All)
      {
        if (ReportHas($"click: {info.Id} '{info.Label}'")) named++;
      }
      Check(named == MainMenuActions.All.Length,
            $"the report names every action that was dispatched "
            + $"({named}/{MainMenuActions.All.Length} of the six named on a 'click:' line)");

      // The honest wording, both directions: the row whose screen this port
      // really opens says so, and every row whose screen it does not have still
      // names the shipped screen and says it is not built.
      MainMenuActionInfo play = MainMenuActions.Info(MainMenuAction.Play);
      Check(play.Built && play.Opens == GameState.PLAY_SCREEN
            && MainMenuActions.Outcome(play) == play.Destination,
            $"MAINMENU_PLAY is the one row with a destination, and it is this screen "
            + $"(\"{MainMenuActions.Outcome(play)}\")");

      int unbuilt = 0;
      var stillUnbuilt = new List<string>();
      foreach (MainMenuActionInfo info in MainMenuActions.All)
      {
        if (info.Built) continue;
        string outcome = MainMenuActions.Outcome(info);
        if (outcome.StartsWith("no destination yet: ", StringComparison.Ordinal)
            && outcome.EndsWith(" is not built", StringComparison.Ordinal))
        {
          unbuilt++;
          stillUnbuilt.Add(info.Id);
        }
      }
      Check(unbuilt == MainMenuActions.All.Length - 1,
            $"and the other five still name their shipped screen as not built "
            + $"({unbuilt}/5: {string.Join(", ", stillUnbuilt)})");
    }

    /// <summary>
    /// The frontend's cue across the round trip. The shipped sheet has exactly
    /// two frontend play lists (PL_LOADING for the boot, PL_MAINMENU for the
    /// frontend), so the play screen has no cue of its own and the menu's list
    /// must keep running - S2MMusicPlayer.Play always restarts at the list's
    /// first item, and the play-list clock (TotalElapsed) not going backwards
    /// across the trip is what says no restart happened.
    /// </summary>
    private static void CheckTheFrontendCueSurvivedTheRoundTrip()
    {
      Main main = UnityEngine.Object.FindAnyObjectByType<Main>();
      S2MMusicPlayer music = main != null ? main.MusicPlayer : null;
      if (music == null)
      {
        Fail("the music player is gone, so the frontend cue could not be judged");
        return;
      }

      string id = music.Current != null ? music.Current.Id : null;
      float elapsed = music.TotalElapsed;
      Check(id == MenuPlaylist && musicTotalElapsedBeforePlay >= 0f
            && elapsed > musicTotalElapsedBeforePlay,
            $"the frontend's cue was NOT restarted by the round trip "
            + $"({musicIdBeforePlay} {musicTotalElapsedBeforePlay:F1} s -> {id} {elapsed:F1} s "
            + $"of play list time, source time {music.Source?.time:F1} s)");
    }

    // ------------------------------------------------------------------
    // run 2: the dev harness behind the same switch
    // ------------------------------------------------------------------

    private static void TickExitPlaymode()
    {
      if (EditorApplication.isPlaying)
      {
        if (Expired(ExitPlaymodeSeconds, "the editor did not leave Play mode"))
          return;
        return;
      }

      BeginPhase(Phase.DevHarness, DevHarnessBudgetSeconds);
      SessionState.SetInt(AttemptsKey, 1);
      SessionState.SetFloat(LastAttemptKey, (float)EditorApplication.timeSinceStartup);
      EditorApplication.EnterPlaymode();
    }

    private static void TickDevHarness()
    {
      if (Expired(DevHarnessBudgetSeconds, "the dev harness did not load its map inside its budget"))
        return;
      if (WaitForOrRetryPlaymode("the dev harness")) return;

      Main main = UnityEngine.Object.FindAnyObjectByType<Main>();
      if (main == null) return;

      if (!devBootChecked)
      {
        devBootChecked = true;
        Check(!main.GameFlowActive && main.GameFlowSource == FlowOverridePath + " = dev",
              $"the switch booted the dev harness ({main.GameFlowSource})");
      }

      GameObject world = GameObject.Find("S2M Map World");
      if (world == null) return;  // RunTests is still loading the map
      if (devCounted) return;
      devCounted = true;

      Check(ReportHas("Read S2M header:"),
            "the harness read the map header (war_chapter8)");
      Check(Grandchildren("S2M Buildings") == Ch8BuildingInstances,
            $"the buildings root holds war_chapter8's {Ch8BuildingInstances} placement "
            + $"instances (found {Grandchildren("S2M Buildings")})");
      Check(Grandchildren("S2M Scenery") == Ch8SceneryInstances,
            $"the scenery root holds its {Ch8SceneryInstances} rock instances "
            + $"(found {Grandchildren("S2M Scenery")})");
      Check(Grandchildren("S2M Entities") == Ch8EntityInstances,
            $"the entities root holds its {Ch8EntityInstances} actors "
            + $"(found {Grandchildren("S2M Entities")})");
      Check(main.MusicPlayer.Current == null && main.MusicPlayer.TracksPlayed == 0,
            "and the dev-harness path played no music at all");

      // The map load's own cost, as the kernel reports it: this is the number
      // the OOM killer acted on five times on 2026-10-12 (anon-rss 4.94 GB and
      // 7.69 GB at this very load). The per-actor mesh/clip
      // duplication behind those kills is fixed (§7.7); this line is what says
      // the fix held in the real editor, and the check is the regression gate.
      MemoryLine("after run 2's map load");
      long anon = AnonRssBytes();
      Check(anon > 0 && anon < AnonBudgetBytes,
            $"the editor's own memory fits the machine's budget at the end of run 2 "
            + $"({anon / 1073741824.0:F2} GB anon-rss, budget {AnonBudgetBytes / 1073741824.0:F1} GB "
            + "on a 15 GB machine)");

      BeginPhase(Phase.Finish, 30);
    }

    private static void Finish()
    {
      EditorApplication.update -= Tick;
      Application.logMessageReceived -= OnLog;

      MemoryLine("at the end of the run");
      int failures = CountFailures();
      Line($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      SessionState.EraseInt(PhaseKey);
      SessionState.EraseString(StartKey);
      SessionState.EraseString(DeadlineKey);

      // Leave the machine as the run found it: the switch file goes away (the
      // scene's serialized field decides next time) and a map override the
      // check had put in place comes back.
      try { File.Delete(FlowOverridePath); } catch (Exception) { /* best effort */ }
      try
      {
        if (File.Exists(TestMapOverridePath + ".sh2flow-backup"))
        {
          MoveFile(TestMapOverridePath + ".sh2flow-backup", TestMapOverridePath);
        }
      }
      catch (Exception) { /* best effort */ }
      try
      {
        if (File.Exists(IntroBudgetOverridePath + ".sh2flow-backup"))
        {
          MoveFile(IntroBudgetOverridePath + ".sh2flow-backup", IntroBudgetOverridePath);
        }
      }
      catch (Exception) { /* best effort */ }

      EditorApplication.Exit(failures == 0 ? 0 : 1);
    }

    /// <summary>File.Move without the overwrite overload (not in Unity's profile).</summary>
    private static void MoveFile(string from, string to)
    {
      if (File.Exists(to)) File.Delete(to);
      File.Move(from, to);
    }

    // ------------------------------------------------------------------
    // the menu's content, the captures, and the plumbing
    // ------------------------------------------------------------------

    private static void CheckMenuContent()
    {
      GameObject menu = GameObject.Find("MainMenuUI");
      Check(menu != null, "the menu's UI root exists in the scene");
      if (menu == null) return;

      GameObject logo = GameObject.Find("MainMenuLogo");
      UnityEngine.UI.Image logoImage = logo != null ? logo.GetComponent<UnityEngine.UI.Image>() : null;
      Check(logoImage != null && logoImage.sprite != null,
            "the menu's logo sprite loaded from the install (Constants.GamePath resolves)");

      // The column sits two levels down (MainMenuUI -> MainMenuContentRoot ->
      // MainMenuColumn -> button), so it has to be found anywhere under the
      // root, not as a direct child.
      Transform column = FindMenuColumn();
      Check(column != null, "the menu's button column exists under the UI root");

      int buttons = 0;
      int buttonsWithSprites = 0;
      if (column != null)
      {
        foreach (Transform child in column)
        {
          // The column carries the logo as well (CreateLogo parents it here),
          // so the buttons are the children CreateMenuButton named "<text>Button".
          if (!child.name.EndsWith("Button")) continue;
          buttons++;
          UnityEngine.UI.Image image = child.GetComponent<UnityEngine.UI.Image>();
          if (image != null && image.sprite != null) buttonsWithSprites++;
        }
      }
      Check(buttons == 6 && buttonsWithSprites == 6,
            $"the menu's 6 buttons are built with their shipped sprites "
            + $"({buttonsWithSprites}/{buttons} buttons, {column?.childCount ?? 0} children in the column)");

      // Not a criterion of this slice - just recorded, because the layered menu
      // videos are the next menu item on the roadmap and the
      // background player appearing is the first sign of them.
      Line($"menu         : background/curtain video players present="
           + $"{GameObject.Find("MainMenuBackgroundPlayer") != null}");
    }

    /// <summary>
    /// The menu's button column. It sits two levels down (MainMenuUI ->
    /// MainMenuContentRoot -> MainMenuColumn -> button), so it has to be found
    /// anywhere under the root, not as a direct child. Its first child is the
    /// logo, not a button; the buttons are the children CreateMenuButton named
    /// "&lt;label&gt;Button".
    /// </summary>
    private static Transform FindMenuColumn()
    {
      GameObject menu = GameObject.Find("MainMenuUI");
      if (menu == null) return null;
      foreach (Transform candidate in menu.GetComponentsInChildren<Transform>(true))
      {
        if (candidate.name == "MainMenuColumn") return candidate;
      }
      return null;
    }

    // ------------------------------------------------------------------
    // the six buttons as an input surface (MainMenuActions)
    // ------------------------------------------------------------------

    /// <summary>
    /// The five menu buttons that have no destination yet, clicked the way a
    /// player's click reaches them - the pointer click event the input module
    /// dispatches to the button's own GameObject - so the interactable gate,
    /// Button.Press() and the button's UnityEvent are all the real path, and the
    /// listener at its end is MainMenuActions.Dispatch, the ONE entry point.
    ///
    /// After each click the runner reads the dispatch record: the count must
    /// have advanced by exactly one and the row must be that button's own
    /// action, so a button wired to another button's action (or to nothing)
    /// fails here rather than passing on the previous click's record. The
    /// outcome - the shipped destination the action would open, or the fact
    /// that the port has not built it - is written to the report for every one
    /// of them. Nothing here asserts that a destination screen exists: that is
    /// the honest state of the five that have none.
    ///
    /// PLAY IS DELIBERATELY NOT AMONG THEM. Its click opens the play screen,
    /// which replaces the menu - so clicking it here would destroy the very
    /// column the rest of this batch walks, and the curtain hand-off and the
    /// settled-menu capture (both still to come with the menu up) would have
    /// nothing to observe. It is the sixth click, made by ClickMenuPlayButton
    /// after the menu capture, and the batch's aggregate is judged once both
    /// halves have happened (CheckTheSixMenuClicks).
    /// </summary>
    private static void CheckMenuButtons()
    {
      Transform column = FindMenuColumn();
      if (column == null)
      {
        Fail("the menu's button column was not found, so none of the six clicks could be made");
        return;
      }

      int batchStart = MainMenuActions.DispatchCount;
      menuBatchStart = batchStart;
      menuBatchActions.Clear();
      foreach (MainMenuActionInfo info in MainMenuActions.All)
      {
        if (info.Built) continue;  // Play: the sixth click, made later (see above)

        Transform child = column.Find(info.Label + "Button");
        UnityEngine.UI.Button button = child != null
          ? child.GetComponent<UnityEngine.UI.Button>() : null;
        if (button == null)
        {
          Fail($"the {info.Label} button ({info.Label}Button) is not in the menu, "
               + "so its click could not be made");
          continue;
        }

        int before = MainMenuActions.DispatchCount;
        PressButton(button);
        MainMenuActionInfo dispatched = MainMenuActions.LastDispatch;
        bool ok = MainMenuActions.DispatchCount == before + 1
                  && MainMenuActions.HasDispatch
                  && dispatched.Action == info.Action
                  && dispatched.Id == info.Id;
        if (ok) menuBatchActions.Add(dispatched.Action);

        Check(ok,
              $"the {info.Label} button's click dispatched {info.Id} '{info.Label}' "
              + $"through the one entry point (MainMenuActions.Dispatch) - "
              + $"{MainMenuActions.Outcome(info)}");
      }
    }

    /// <summary>
    /// One button press, driven the way the input module drives it: the pointer
    /// click event is dispatched to the button's GameObject
    /// (ExecuteEvents.pointerClickHandler - the exact call StandaloneInputModule
    /// makes when a release lands on a button), so everything downstream of it
    /// is untouched game code: Selectable's interactable gate, Button.Press(),
    /// the button's own UnityEvent, and its listener.
    /// </summary>
    private static void PressButton(UnityEngine.UI.Button button)
    {
      UnityEngine.EventSystems.EventSystem eventSystem =
        UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
      UnityEngine.EventSystems.PointerEventData data =
        new(eventSystem) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
      UnityEngine.EventSystems.ExecuteEvents.Execute(
        button.gameObject, data, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    }

    // ------------------------------------------------------------------
    // the play screen (MAINMENU_PLAY's destination)
    // ------------------------------------------------------------------

    /// <summary>
    /// What the play screen carries, read off the screen itself: its own
    /// canvas, its background from ui/choose_path/, its title from the shipped
    /// row, and its four action buttons with the install's own pictures and the
    /// shipped labels. The screen's GameObjects are named after the action
    /// ("WarButton", "PeaceButton", "TutorialButton", "BackButton"), which is
    /// how a button is found here - never by position or by the label alone.
    /// </summary>
    private static void CheckPlayScreenContent()
    {
      GameObject screen = GameObject.Find("PlayScreenUI");
      Check(screen != null, "the play screen's UI root exists in the scene");
      if (screen == null) return;

      Canvas canvas = screen.GetComponent<Canvas>();
      Check(canvas != null && canvas.sortingOrder > 1100,
            $"it is its own canvas above the menu's "
            + $"(sorting order {canvas?.sortingOrder}, the menu's is 1100)");

      GameObject background = GameObject.Find("PlayScreenBackground");
      UnityEngine.UI.Image backgroundImage = background != null
        ? background.GetComponent<UnityEngine.UI.Image>() : null;
      Texture2D backgroundTexture = backgroundImage != null && backgroundImage.sprite != null
        ? backgroundImage.sprite.texture : null;
      Check(backgroundTexture != null
            && backgroundTexture.name == Path.GetFileName(UIAssets.ChoosePathBackgroundTexture),
            $"its background is the install's own art for this screen "
            + $"('{backgroundTexture?.name}' = {Path.GetFileName(UIAssets.ChoosePathBackgroundTexture)}, "
            + $"{backgroundTexture?.width}x{backgroundTexture?.height})");

      GameObject title = GameObject.Find("PlayScreenTitle");
      UnityEngine.UI.Text titleText = title != null ? title.GetComponent<UnityEngine.UI.Text>() : null;
      Check(titleText != null && titleText.text == PlayScreenUI.ShippedTitleText,
            $"its title is the shipped row's own text ('{titleText?.text}' "
            + $"= {PlayScreenUI.ShippedTitleId})");

      // The screen is 4:3 like the menu's, so it needs the stone bars the menu
      // has - the menu takes ITS bars down when this screen replaces it, and
      // without this screen's own the widescreen gutters show the scene camera
      // (the first capture of this screen had the sample scene's skybox in
      // them). Judged on the bars' own sprites, like every other picture here.
      GameObject bars = GameObject.Find("AspectRatioBars");
      int barSprites = 0;
      if (bars != null)
      {
        foreach (Transform child in bars.transform)
        {
          UnityEngine.UI.Image barImage = child.GetComponent<UnityEngine.UI.Image>();
          if (barImage != null && barImage.sprite != null) barSprites++;
        }
      }
      Check(barSprites == 2,
            $"and its own aspect-ratio bars are up, so the widescreen gutters are the "
            + $"shipped stone and not the scene behind the screen "
            + $"({barSprites}/2 bars with their stone sprite)");

      int buttons = 0;
      int withSprites = 0;
      int labelled = 0;
      var detail = new List<string>();
      foreach (PlayScreenActionInfo info in PlayScreenActions.All)
      {
        GameObject buttonObject = GameObject.Find(info.Action + "Button");
        UnityEngine.UI.Image image = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Image>() : null;
        if (image != null)
        {
          buttons++;
          if (image.sprite != null && image.sprite.texture != null) withSprites++;
        }

        // The picture buttons carry the shipped label as a caption under them
        // (the shipped buttons are pictures with tooltips, so the text has to
        // live somewhere on this port's screen); the fancy Back button carries
        // it inside, the way the menu's buttons do.
        string shown;
        if (info.Action == PlayScreenAction.Back)
        {
          Transform label = buttonObject != null ? buttonObject.transform.Find("Label") : null;
          UnityEngine.UI.Text text = label != null ? label.GetComponent<UnityEngine.UI.Text>() : null;
          shown = text != null ? text.text : null;
        }
        else
        {
          GameObject captionObject = GameObject.Find(info.Label + "Caption");
          UnityEngine.UI.Text caption = captionObject != null
            ? captionObject.GetComponent<UnityEngine.UI.Text>() : null;
          shown = caption != null ? caption.text : null;
        }
        if (shown == info.Label) labelled++;
        else detail.Add($"{info.Action}: shown='{shown ?? "<none>"}' expected='{info.Label}'");
      }

      Check(buttons == PlayScreenActions.All.Length
            && withSprites == PlayScreenActions.All.Length
            && labelled == PlayScreenActions.All.Length,
            $"its four actions are built with the install's own pictures and the shipped labels "
            + $"({withSprites}/{PlayScreenActions.All.Length} sprites, "
            + $"{labelled}/{PlayScreenActions.All.Length} labels"
            + (detail.Count > 0 ? "; " + string.Join("; ", detail) : "") + ")");

      Line($"play screen  : title '{titleText?.text}', buttons "
           + string.Join(", ", Array.ConvertAll(PlayScreenActions.All, i => $"{i.Action}='{i.Label}'")));
    }

    /// <summary>
    /// The play screen's own rows against the install: its title and its four
    /// actions' ids and text must BE the shipped PLAYSCREEN* rows of
    /// i18n/en-us.xml, read at run time - the same authority check the menu's
    /// six rows get. The two paths' tooltip CONTENT (the shipped hover text)
    /// rides on the rows as well and is printed for the report, but nothing on
    /// this port's screen promises a hover panel.
    /// </summary>
    private static void CheckPlayScreenStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();

      if (shipped.TryGetValue(PlayScreenUI.ShippedTitleId, out string title)
          && title == PlayScreenUI.ShippedTitleText)
      {
        // the title is the shipped row's
      }
      else
      {
        mismatches.Add($"{PlayScreenUI.ShippedTitleId} shipped="
                       + $"{(shipped.TryGetValue(PlayScreenUI.ShippedTitleId, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{PlayScreenUI.ShippedTitleText}'");
      }

      foreach (PlayScreenActionInfo info in PlayScreenActions.All)
      {
        if (shipped.TryGetValue(info.Id, out string english) && english == info.Label) continue;
        mismatches.Add($"{info.Id} shipped="
                       + $"{(shipped.TryGetValue(info.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{info.Label}'");
      }

      int rows = PlayScreenActions.All.Length + 1;
      Check(mismatches.Count == 0 && shipped.Count > 0,
            $"the play screen's title and four action ids are the shipped table's own rows, "
            + $"with the shipped text ({rows - mismatches.Count}/{rows} PLAYSCREEN* rows agree "
            + $"with i18n/en-us.xml, {shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");

      foreach (PlayScreenActionInfo info in PlayScreenActions.All)
      {
        if (info.Tooltip.Length > 0)
        {
          Line($"play screen  : {info.Id} tooltip content '{info.Tooltip}'");
        }
      }
    }

    /// <summary>
    /// The play screen's actions with no destination yet, clicked the same way
    /// the menu's five were - the button's own pointer click - with the dispatch
    /// record read after each, so each one has to be its own row on the screen's
    /// ONE entry point (PlayScreenActions.Dispatch). As of the path-screens
    /// work, that is the tutorial alone: the two paths open the war and peace
    /// screens, so their clicks leave this screen and are the round trip's own
    /// (TickPlayScreenCapture and the phases after it), and so is the way back.
    /// </summary>
    private static void CheckPlayScreenButtons()
    {
      var seen = new List<PlayScreenAction>();
      int clicked = 0;
      foreach (PlayScreenActionInfo info in PlayScreenActions.All)
      {
        if (info.Built) continue;  // the round trip's own clicks, made after the capture

        GameObject buttonObject = GameObject.Find(info.Action + "Button");
        UnityEngine.UI.Button button = buttonObject != null
          ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null;
        if (button == null)
        {
          Fail($"the {info.Label} button ({info.Action}Button) is not on the play screen, "
               + "so its click could not be made");
          continue;
        }

        int before = PlayScreenActions.DispatchCount;
        PressButton(button);
        PlayScreenActionInfo dispatched = PlayScreenActions.LastDispatch;
        clicked++;
        bool ok = PlayScreenActions.DispatchCount == before + 1
                  && PlayScreenActions.HasDispatch
                  && dispatched.Action == info.Action
                  && dispatched.Id == info.Id;
        if (ok) seen.Add(dispatched.Action);

        Check(ok,
              $"the play screen's {info.Label} button's click dispatched {info.Id} "
              + $"'{info.Label}' through its one entry point (PlayScreenActions.Dispatch) - "
              + $"{PlayScreenActions.Outcome(info)}");
      }

      int unbuilt = 0;
      var theTrips = new List<string>();
      foreach (PlayScreenActionInfo info in PlayScreenActions.All)
      {
        if (info.Built) theTrips.Add(info.Label);
        else unbuilt++;
      }

      int distinct = new HashSet<PlayScreenAction>(seen).Count;
      Check(clicked == unbuilt
            && PlayScreenActions.DispatchCount == clicked
            && distinct == clicked,
            $"and its {unbuilt} destination-less action(s) each dispatched once through that one "
            + $"entry point ({PlayScreenActions.DispatchCount} dispatch(es) for {clicked} click(s), "
            + $"{distinct} distinct action(s); the built ones - {string.Join(", ", theTrips)} - "
            + "are the round trip's own clicks)");
    }

    /// <summary>
    /// The play screen's two path rows, now that their screens exist: each row
    /// carries the screen it opens (Built is derived from that, so a row cannot
    /// claim a screen the flow cannot show) and the tutorial's row still names its
    /// shipped screen as not built - the honest wording, both directions.
    /// </summary>
    private static void CheckThePlayScreensPathRows()
    {
      PlayScreenActionInfo war = PlayScreenActions.Info(PlayScreenAction.War);
      PlayScreenActionInfo peace = PlayScreenActions.Info(PlayScreenAction.Peace);
      PlayScreenActionInfo tutorial = PlayScreenActions.Info(PlayScreenAction.Tutorial);

      Check(war.Built && war.Opens == GameState.WAR_SCREEN
            && PlayScreenActions.Outcome(war) == war.Destination,
            $"the play screen's {war.Label} row now says where it goes "
            + $"(\"{PlayScreenActions.Outcome(war)}\")");
      Check(peace.Built && peace.Opens == GameState.PEACE_SCREEN
            && PlayScreenActions.Outcome(peace) == peace.Destination,
            $"and its {peace.Label} row says where it goes "
            + $"(\"{PlayScreenActions.Outcome(peace)}\")");
      Check(!tutorial.Built
            && PlayScreenActions.Outcome(tutorial).StartsWith("no destination yet: ", StringComparison.Ordinal)
            && PlayScreenActions.Outcome(tutorial).EndsWith(" is not built", StringComparison.Ordinal),
            $"while the tutorial still reports its shipped screen as not built "
            + $"(\"{PlayScreenActions.Outcome(tutorial)}\")");
    }

    /// <summary>
    /// The authority for the six action ids and their text, checked against the
    /// install: the shipped help file documents the game, not the frontend, so
    /// the frontend's own names live in the shipped translation table
    /// (i18n/en-us.xml - an Excel-XML workbook, one Row per string: id, English
    /// text, translated text). This asserts the port's six rows ARE the shipped
    /// MAINMENU_* rows, id and text - not names this port invented.
    /// </summary>
    private static void CheckMenuStringsAgainstInstall()
    {
      Dictionary<string, string> shipped = ReadShippedFrontendStrings();
      var mismatches = new List<string>();
      foreach (MainMenuActionInfo info in MainMenuActions.All)
      {
        if (shipped.TryGetValue(info.Id, out string english) && english == info.Label) continue;
        mismatches.Add($"{info.Id} shipped="
                       + $"{(shipped.TryGetValue(info.Id, out string found) ? $"'{found}'" : "<no such row>")}"
                       + $" port='{info.Label}'");
      }

      Check(mismatches.Count == 0 && shipped.Count > 0,
            $"the six action ids are the shipped frontend's own ids, with the shipped text "
            + $"({MainMenuActions.All.Length - mismatches.Count}/{MainMenuActions.All.Length} "
            + $"MAINMENU_* rows agree with i18n/en-us.xml, {shipped.Count} strings read"
            + (mismatches.Count > 0 ? "; " + string.Join("; ", mismatches) : "") + ")");
    }

    /// <summary>
    /// Every id/text pair in the install's translation table. The table is 2 MB
    /// of SpreadsheetML, so the rows are matched rather than DOM-loaded: one
    /// &lt;Row&gt; per string, its first two string cells the id and the English
    /// text.
    /// </summary>
    private static Dictionary<string, string> ReadShippedFrontendStrings()
    {
      var strings = new Dictionary<string, string>(StringComparer.Ordinal);
      string path = Path.Combine(GameRoot, "i18n/en-us.xml");
      try
      {
        string text = File.ReadAllText(path);
        foreach (Match row in Regex.Matches(text, "<Row>(.*?)</Row>", RegexOptions.Singleline))
        {
          MatchCollection cells = Regex.Matches(row.Groups[1].Value,
            "<Data ss:Type=\"String\">(.*?)</Data>", RegexOptions.Singleline);
          if (cells.Count < 2) continue;
          string id = System.Net.WebUtility.HtmlDecode(cells[0].Groups[1].Value).Trim();
          string english = System.Net.WebUtility.HtmlDecode(cells[1].Groups[1].Value).Trim();
          if (id.Length > 0) strings[id] = english;
        }
      }
      catch (Exception error)
      {
        Line($"menu strings : {path} unreadable ({error.Message})");
      }
      return strings;
    }

    // ------------------------------------------------------------------
    // the curtain's hand-off (curtain_animation once -> curtain_idle looping)
    // ------------------------------------------------------------------

    /// <summary>
    /// One tick of the curtain's record, taken from the players themselves:
    /// which of the two clips is running (matched by the cached file its url
    /// points at), how far it has played (frame/time) and whether it loops.
    /// Called on every tick of the menu phase - the open clip's end and the
    /// idle clip's wrap can land on any frame, and both are the evidence the
    /// checks below are made of.
    /// </summary>
    private static void ObserveCurtain()
    {
      VideoPlayer open = FindCurtainPlayer(CurtainOpenFile);
      if (open != null)
      {
        if (!curtainSawOpen)
        {
          curtainSawOpen = true;
          Line($"curtain      : the open clip's player is up (frame {open.frame}, "
               + $"frameCount {open.frameCount})");
        }
        // A player that is still PREPARING reports frame -1 and frameCount 0,
        // so the first real frame and the real count are taken as they appear -
        // otherwise the first sighting poisons every number in the checks.
        if (curtainOpenFirstFrame < 0 && open.frame >= 0) curtainOpenFirstFrame = open.frame;
        if (open.frameCount > 0) curtainOpenFrameCount = (long)open.frameCount;
        if (curtainOpenFirstFrame >= 0 && open.frame > curtainOpenFirstFrame) curtainOpenAdvanced = true;
        if (open.frame > curtainOpenLastFrame) curtainOpenLastFrame = open.frame;
      }
      else if (curtainSawOpen && !curtainOpenGone)
      {
        curtainOpenGone = true;
        Line($"curtain      : the open clip's player is gone - last frame seen "
             + $"{curtainOpenLastFrame}/{curtainOpenFrameCount}");
      }

      VideoPlayer idle = FindCurtainPlayer(CurtainIdleFile);
      if (idle != null)
      {
        if (!curtainIdleSeen)
        {
          curtainIdleSeen = true;
          curtainIdleIsLooping = idle.isLooping;
          Line($"curtain      : curtain_idle's player took over (frame {idle.frame}, "
               + $"frameCount {idle.frameCount}, isLooping={idle.isLooping}, "
               + $"isPlaying={idle.isPlaying})");
        }

        if (idle.frameCount > 0) curtainIdleFrameCount = (long)idle.frameCount;
        if (curtainIdleFirstFrame < 0 && idle.frame >= 0)
        {
          curtainIdleFirstFrame = idle.frame;
          curtainIdleLastFrame = idle.frame;
          curtainIdleFirstTime = idle.time;
          curtainIdleLastTime = idle.time;
        }

        if (idle.frame >= 0)
        {
          if (idle.frame > curtainIdleMaxFrame) curtainIdleMaxFrame = idle.frame;
          if (idle.time > curtainIdleMaxTime) curtainIdleMaxTime = idle.time;
          if (idle.frame > curtainIdleFirstFrame
              || idle.time > curtainIdleFirstTime + 0.001) curtainIdleAdvanced = true;

          // The loop itself, on the player's own numbers: a looping player's
          // clock must come back down at the end of its clip. Frame first, time
          // as the fallback - an earlier measurement found that isPlaying can report
          // False on Linux while frames advance, so the numbers decide.
          if (!curtainIdleWrapped
              && (idle.frame < curtainIdleLastFrame - 1 || idle.time < curtainIdleLastTime - 0.25))
          {
            curtainIdleWrapped = true;
            curtainWrapFromFrame = curtainIdleLastFrame;
            curtainWrapToFrame = idle.frame;
            curtainWrapFromTime = curtainIdleLastTime;
            curtainWrapToTime = idle.time;
            Line($"curtain      : the idle clip WRAPPED (frame {curtainWrapFromFrame} -> "
                 + $"{curtainWrapToFrame}, time {curtainWrapFromTime:F3} -> {curtainWrapToTime:F3} s)");
          }

          curtainIdleLastFrame = idle.frame;
          curtainIdleLastTime = idle.time;
        }
      }

      // A trajectory line every couple of seconds, so the report of a run that
      // FAILED still carries what the two players did and when.
      double now = EditorApplication.timeSinceStartup;
      if (now - curtainLastLogAt > 2.0)
      {
        curtainLastLogAt = now;
        string openState = open == null
          ? "open player gone"
          : open.frame < 0
            ? "open player preparing"
            : $"open frame {open.frame}/{open.frameCount}";
        string idleState = idle == null
          ? "no idle player yet"
          : idle.frame < 0
            ? "idle player preparing"
            : $"idle frame {idle.frame}/{idle.frameCount}";
        Line($"curtain      : t={now:F1} s - {openState}, {idleState}");
      }
    }

    /// <summary>
    /// The VideoPlayer running a given cached clip, found by the file its url
    /// points at - the player's own state, not a GameObject name: the two
    /// curtain players are created and destroyed as the hand-off runs, and the
    /// pair share a default name.
    /// </summary>
    private static VideoPlayer FindCurtainPlayer(string clipFileName)
    {
      foreach (VideoPlayer player in UnityEngine.Object.FindObjectsByType<VideoPlayer>(
                 FindObjectsInactive.Exclude, FindObjectsSortMode.None))
      {
        if (player == null) continue;
        string url = player.url ?? "";
        if (url.EndsWith(clipFileName, StringComparison.OrdinalIgnoreCase)) return player;
      }
      return null;
    }

    /// <summary>
    /// The hand-off, judged from the observation above, in the shipped order:
    /// the open clip plays first and finishes (its player destroys itself), the
    /// idle clip takes over and wraps. Every number here is the player's own -
    /// frame, time, isLooping - never a clock the runner kept.
    /// </summary>
    private static void CheckCurtainHandoff()
    {
      if (curtainChecked) return;
      curtainChecked = true;

      Check(curtainSawOpen && (curtainOpenAdvanced || curtainOpenFirstFrame > 0),
            $"the curtain's open clip plays first (curtain_animation, first frame seen "
            + $"{curtainOpenFirstFrame} -> last {curtainOpenLastFrame} of {curtainOpenFrameCount})");
      Check(curtainOpenGone && !ReportHas("Video playback failed"),
            "and it finishes on its own: its player is gone "
            + $"(last frame seen {curtainOpenLastFrame}/{curtainOpenFrameCount}) and no "
            + "playback error was logged");
      Check(curtainIdleSeen && curtainIdleIsLooping,
            $"then curtain_idle takes over (frame {curtainIdleFirstFrame}/{curtainIdleFrameCount}, "
            + $"isLooping={curtainIdleIsLooping})");
      Check(curtainIdleAdvanced,
            $"and that clip is the one playing (frame {curtainIdleFirstFrame} -> "
            + $"{curtainIdleMaxFrame}, time {curtainIdleFirstTime:F3} -> {curtainIdleMaxTime:F3} s)");
      Check(curtainIdleWrapped,
            $"and it LOOPS: the player's own clock came back down (frame {curtainWrapFromFrame} -> "
            + $"{curtainWrapToFrame}, time {curtainWrapFromTime:F3} -> {curtainWrapToTime:F3} s)");
    }

    /// <summary>
    /// Reads a PNG back and counts what is IN it: distinct 5-bit colours (the
    /// quantiser caveat - a channel is SHIFTED, never clamped)
    /// and the fraction of pixels that are not black. Returns the colour count.
    /// </summary>
    private static int CheckCapture(string path, string what)
    {
      if (!File.Exists(path))
      {
        Fail($"{what} was not written ({path})");
        return 0;
      }
      byte[] bytes = File.ReadAllBytes(path);
      var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
      if (!ImageConversion.LoadImage(texture, bytes))
      {
        Fail($"{what} does not decode as an image ({bytes.Length} bytes)");
        return 0;
      }
      Color32[] pixels = texture.GetPixels32();
      var seen = new HashSet<int>();
      int notBlack = 0;
      foreach (Color32 pixel in pixels)
      {
        seen.Add((pixel.r >> 3 << 10) | (pixel.g >> 3 << 5) | (pixel.b >> 3));
        if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8) notBlack++;
      }
      float notBlackFraction = (float)notBlack / pixels.Length;
      Line($"capture      : {Path.GetFileName(path)} {texture.width}x{texture.height}, "
           + $"{bytes.Length} bytes, {seen.Count} distinct colours, "
           + $"{notBlackFraction * 100f:F1} % of pixels not black");
      return seen.Count;
    }

    /// <summary>
    /// The fallback for either frontend screen: the named canvas drawn by a
    /// temporary camera into a RenderTexture. A UI canvas needs no window, only
    /// the canvas itself; the canvas is put back exactly as it was afterwards.
    /// </summary>
    private static int CaptureFrontendCanvas(string rootName, string capturePath, string what)
    {
      GameObject root = GameObject.Find(rootName);
      Canvas canvas = root != null ? root.GetComponent<Canvas>() : null;
      if (canvas == null)
      {
        Fail($"{what}: the {rootName} canvas is gone; nothing to render");
        return 0;
      }

      int width = Mathf.Max(Screen.width, 640);
      int height = Mathf.Max(Screen.height, 480);
      var cameraObject = new GameObject("S2M Flow Capture Camera");
      Camera camera = cameraObject.AddComponent<Camera>();
      camera.orthographic = true;
      camera.orthographicSize = height / 2f;
      camera.cullingMask = 0;  // the canvas only: this scene has no world geometry
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = Color.black;
      camera.transform.position = new Vector3(0f, 0f, -100f);

      var target = new RenderTexture(width, height, 24);
      camera.targetTexture = target;

      RenderMode previousMode = canvas.renderMode;
      Camera previousCamera = canvas.worldCamera;
      canvas.renderMode = RenderMode.ScreenSpaceCamera;
      canvas.worldCamera = camera;
      canvas.planeDistance = 50f;
      Canvas.ForceUpdateCanvases();
      camera.Render();

      RenderTexture previousActive = RenderTexture.active;
      RenderTexture.active = target;
      var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
      texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
      texture.Apply();
      RenderTexture.active = previousActive;
      File.WriteAllBytes(capturePath, texture.EncodeToPNG());

      canvas.renderMode = previousMode;
      canvas.worldCamera = previousCamera;
      camera.targetTexture = null;
      UnityEngine.Object.Destroy(cameraObject);
      target.Release();

      return CheckCapture(capturePath, what);
    }

    private static void FocusGameView()
    {
      try
      {
        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null) return;
        EditorWindow window = EditorWindow.GetWindow(gameViewType);
        window?.Focus();
      }
      catch (Exception)
      {
        // Not fatal: the canvas fallback covers a Game view that will not render.
      }
    }

    private static int Grandchildren(string rootName)
    {
      GameObject root = GameObject.Find(rootName);
      if (root == null) return -1;
      int count = 0;
      foreach (Transform child in root.transform) count += child.childCount;
      return count;
    }

    /// <summary>Writes the switch file Main.cs reads, and says so in the report.</summary>
    private static void WriteSwitch(string choice)
    {
      File.WriteAllText(FlowOverridePath, choice + "\n");
      Line($"switch       : {FlowOverridePath} = '{choice}'");
    }

    /// <summary>
    /// The memory budget this run gates on, as the kernel measures it: anon-rss
    /// (the process's own allocations - the figure the five OOM kills of
    /// 2026-10-12 were logged at). The machine has 15 GB and the editor shares
    /// it with a desktop and a browser, so ~6 GB is the ceiling
    /// that leaves everyone alive; before the per-actor mesh/clip sharing
    /// run 2's map load alone crossed it.
    /// </summary>
    private const long AnonBudgetBytes = 6L * 1024 * 1024 * 1024;

    /// <summary>
    /// The editor's own memory, as the kernel reports it - the numbers the OOM
    /// killer acts on, read from /proc/self/status: VmRSS (everything the
    /// process holds) and RssAnon (its own allocations).
    /// </summary>
    private static void MemoryLine(string when)
    {
      Line($"memory       : {when} - Unity allocated "
           + $"{Profiler.GetTotalAllocatedMemoryLong() / 1073741824.0:F2} GB, reserved "
           + $"{Profiler.GetTotalReservedMemoryLong() / 1073741824.0:F2} GB, process RSS "
           + $"{VmRssBytes() / 1073741824.0:F2} GB (anon {AnonRssBytes() / 1073741824.0:F2} GB)");
    }

    /// <summary>RssAnon from /proc/self/status, in bytes (0 where unavailable).</summary>
    private static long AnonRssBytes() => ProcStatusBytes("RssAnon:");

    /// <summary>VmRSS from /proc/self/status, in bytes (0 where unavailable).</summary>
    private static long VmRssBytes() => ProcStatusBytes("VmRSS:");

    private static long ProcStatusBytes(string field)
    {
      try
      {
        foreach (string line in File.ReadAllLines("/proc/self/status"))
        {
          if (!line.StartsWith(field, StringComparison.Ordinal)) continue;
          string[] parts = line.Substring(field.Length).Trim().Split(' ');
          if (parts.Length > 0 && long.TryParse(parts[0], out long kilobytes))
            return kilobytes * 1024;
        }
      }
      catch (Exception)
      {
        // Not Linux, or /proc unreadable: the Unity numbers still print.
      }
      return 0;
    }

    private static void BeginPhase(Phase phase, double budgetSeconds)
    {
      DateTime now = DateTime.UtcNow;
      SessionState.SetInt(PhaseKey, (int)phase);
      SessionState.SetString(StartKey, now.Ticks.ToString());
      SessionState.SetString(DeadlineKey, now.AddSeconds(budgetSeconds).Ticks.ToString());
      SessionState.SetInt(AttemptsKey, 0);
      SessionState.SetFloat(LastAttemptKey, 0f);
    }

    /// <summary>Seconds since the current phase began (reload-proof: SessionState).</summary>
    private static double Elapsed()
    {
      string start = SessionState.GetString(StartKey, "");
      if (start.Length == 0) return 0;
      return (DateTime.UtcNow - new DateTime(long.Parse(start))).TotalSeconds;
    }

    private static bool Expired(double budget, string message)
    {
      string deadline = SessionState.GetString(DeadlineKey, "");
      if (deadline.Length == 0) return false;
      if (DateTime.UtcNow.Ticks <= long.Parse(deadline)) return false;
      Fail($"{message} (budget {budget:F0} s)");
      Finish();
      return true;
    }

    /// <summary>
    /// True while Play mode is not up yet (the caller should wait). Asks for it
    /// again when the previous request was cancelled - an unfocused editor
    /// cancels EnterPlaymode, a measured gotcha - and gives up after
    /// <see cref="MaxPlaymodeAttempts"/>.
    /// </summary>
    private static bool WaitForOrRetryPlaymode(string what)
    {
      if (EditorApplication.isPlaying) return false;

      int attempts = SessionState.GetInt(AttemptsKey, 0);
      float last = SessionState.GetFloat(LastAttemptKey, 0f);
      float now = (float)EditorApplication.timeSinceStartup;
      bool waiting = last > 0f && now - last < (float)PlaymodeStartSeconds;

      if (waiting) return true;
      if (attempts >= MaxPlaymodeAttempts)
      {
        Fail($"{what}: Play mode never started after {attempts} attempts "
             + "(an unfocused editor cancels EnterPlaymode)");
        Finish();
        return true;
      }

      SessionState.SetInt(AttemptsKey, attempts + 1);
      SessionState.SetFloat(LastAttemptKey, now);
      Line($"play mode    : entering for {what} (attempt {attempts + 1}, "
           + $"compiling={EditorApplication.isCompiling})");
      EditorApplication.EnterPlaymode();
      return true;
    }

    // ------------------------------------------------------------------
    // the report file: written as the run goes, read back for the checks
    // ------------------------------------------------------------------

    private static void Line(string text)
    {
      lock (ReportLock)
      {
        File.AppendAllText(reportPath, text + "\n");
      }
      Debug.Log($"[S2MGameFlowRun] {text}");
    }

    private static void Check(bool ok, string what) => Line($"check        : {(ok ? "ok  " : "FAIL")} {what}");

    private static void Fail(string what) => Line($"check        : FAIL {what}");

    private static void OnLog(string message, string stackTrace, LogType type)
    {
      if (message.StartsWith("[S2MGameFlowRun]")) return;
      string line = type == LogType.Log ? message : $"{type}: {message}";
      if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
      {
        int newline = stackTrace.IndexOf('\n');
        line += " | " + (newline > 0 ? stackTrace.Substring(0, newline) : stackTrace);
      }
      lock (ReportLock)
      {
        File.AppendAllText(reportPath, "console      : " + line + "\n");
      }
    }

    private static string ReadReport()
    {
      lock (ReportLock)
      {
        try { return File.ReadAllText(reportPath); }
        catch (IOException) { return ""; }
      }
    }

    private static bool ReportHas(string needle) => ReadReport().Contains(needle);

    private static int CountReportLines(string needle)
    {
      int count = 0;
      foreach (string line in ReadReport().Split('\n'))
      {
        if (line.Contains(needle)) count++;
      }
      return count;
    }

    private static int CountFailures()
    {
      int failures = 0;
      foreach (string line in ReadReport().Split('\n'))
      {
        if (line.StartsWith("check        : FAIL")) failures++;
      }
      return failures;
    }
  }
}
