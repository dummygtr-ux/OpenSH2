using Assets.Code;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// The scenario lobby screen's own actions, and its ONE entry point. This is
  /// the screen the war screen's WARSCREEN_KINGMAKER row opens, and the pairing
  /// is MEASURED (2026-10-13): the war screen's own handler (the
  /// function at the head of the WarScreen class's vtable, 0x9e0204) maps the
  /// Kingmaker control's action id (0x144d, the control the war screen's own
  /// constructor builds at object offset +0x834 and its label block writes
  /// WARSCREEN_KINGMAKER 'Kingmaker' / WARSCREENSTATICTOOLTIP_KINGMAKER onto) to
  /// the screen-name string 'scenario lobby screen' (VA 0x905a80) - the engine's
  /// `ScenarioLobbyScreen`.
  ///
  /// WHAT THAT SCREEN IS, also measured: the multiplayer lobby. Its own code
  /// loads ui\multiplayer\multiplayer_background.jpg as its backdrop and
  /// ui\multiplayer\button_gametype1_kingmaker.tga among the four game-type
  /// buttons ('texture/ui/lobby/gametype/1..4'), beside the whole
  /// texture/ui/lobby/ family - teams, ready flags, AI difficulty scales, the
  /// sort rows and the lords' own icons (king, queen, bishop, hammer, olaf,
  /// hawk, bull, edwin, grey, seren, william) - and the player lines the shipped
  /// game writes into it (mp/info/playerjoined, mp/info/playerleft 'Has left the
  /// game.', ui/lobby/opponent/human 'Human'). It is a NETWORK surface: the exe's
  /// own classes behind it are the online service's (GameSpy::GameLobby,
  /// Online::OnlineLobby, "To play a multiplayer game please login to Steam and
  /// restart Stronghold 2").
  ///
  /// THE DEFERRAL THIS SETTLES: an earlier reading had this row as opening
  /// "the Kingmaker (skirmish) setup (SCENARIOEDITORSCREEN_MAKE_SKIRMISH
  /// 'Kingmaker', SKIRMISH_LORD_*)" and deferred it because that setup ships no
  /// art folder of its own. The row opens no Kingmaker-specific screen at all -
  /// Kingmaker is a game TYPE inside the shared lobby, and its button is
  /// `button_gametype1_kingmaker.tga` there. The SCENARIOEDITORSCREEN_MAKE_SKIRMISH
  /// / SKIRMISH_LORD_* rows belong to the SCENARIO EDITOR's own family (the map
  /// editor's skirmish setup), which is not behind the war screen.
  ///
  /// WHAT THIS PORT CAN SHOW: the screen, built from the install's own pieces -
  /// the backdrop, the four game-type buttons on the medallions the backdrop
  /// paints (Kingmaker's own highlight art on the one the row came in by), the
  /// shipped 'Network Lobby' name row, and its own two rows Start and Back. Its
  /// PLAYER LIST IS EMPTY: the lobby's slots hold the players of a network
  /// session, the port has neither a session nor an online service, and the
  /// table ships no empty-state row - so no player line is drawn and the state
  /// is said on the console and in the report instead (the load screen's own
  /// honest move with its empty save list).
  /// </summary>
  public static class ScenarioLobbyActions
  {
    /// <summary>The install's own name for this screen (the exe's own string).</summary>
    public const string ScreenName = "scenario lobby screen";

    /// <summary>
    /// The screen's own name row, LOBBYSCREEN_NETWORK_LOBBY 'Network Lobby' -
    /// the one row of this screen the port draws as a heading.
    /// </summary>
    public const string HeaderId = "LOBBYSCREEN_NETWORK_LOBBY";
    public const string HeaderLabel = "Network Lobby";

    /// <summary>
    /// The four game-type buttons, in the exe's own registration order
    /// ('texture/ui/lobby/gametype/1..4'): Kingmaker first - the type this
    /// screen is opened for, because the war screen's row is the Kingmaker one -
    /// then Knight, Baron and Deathmatch, the other three the install ships
    /// (ui/multiplayer/button_gametype{2_knight,3_baron,4_deathmatch}.tga).
    /// </summary>
    public static readonly ScenarioLobbyAction Kingmaker = new(
      ScenarioLobbyActionKind.GameType, "texture/ui/lobby/gametype/1", "Kingmaker",
      "",
      "the Kingmaker game type - the mode the war screen's WARSCREEN_KINGMAKER row came in by, "
      + "and the one this screen opens on ('Kingmaker', the button's own art)",
      UIAssets.GameTypeKingmakerTexture, "KingmakerGameTypeButton");

    public static readonly ScenarioLobbyAction Knight = new(
      ScenarioLobbyActionKind.GameType, "texture/ui/lobby/gametype/2", "Knight",
      "",
      "the Knight game type (button_gametype2_knight.tga; the war screen has no row for it - "
      + "the lobby is where a game type is chosen)",
      UIAssets.GameTypeKnightTexture, "KnightGameTypeButton");

    public static readonly ScenarioLobbyAction Baron = new(
      ScenarioLobbyActionKind.GameType, "texture/ui/lobby/gametype/3", "Baron",
      "",
      "the Baron game type (button_gametype3_baron.tga; the war screen has no row for it)",
      UIAssets.GameTypeBaronTexture, "BaronGameTypeButton");

    public static readonly ScenarioLobbyAction Deathmatch = new(
      ScenarioLobbyActionKind.GameType, "texture/ui/lobby/gametype/4", "Deathmatch",
      "",
      "the Deathmatch game type (button_gametype4_deathmatch.tga; the war screen has no row for it)",
      UIAssets.GameTypeDeathmatchTexture, "DeathmatchGameTypeButton");

    /// <summary>
    /// The screen's own way on, LOBBYSCREEN_START 'Start'. In the shipped game
    /// it starts the game the lobby has been set up for; this port runs no lobby
    /// session and has no online service, so the click names that state.
    /// </summary>
    public static readonly ScenarioLobbyAction Start = new(
      ScenarioLobbyActionKind.Start, "LOBBYSCREEN_START", "Start", "",
      "no destination yet: the lobby has no session to start - this port has no online service and "
      + "runs no lobby, so there are no players to gather and no game to begin",
      UIAssets.FancyButtonTexture, "StartButton");

    /// <summary>
    /// The screen's way back, LOBBYSCREEN_BACK 'Back' - the WAR SCREEN it was
    /// opened from, the frontend being a tree.
    /// </summary>
    public static readonly ScenarioLobbyAction Back = new(
      ScenarioLobbyActionKind.Back, "LOBBYSCREEN_BACK", "Back", "",
      "the war screen (the screen the lobby was opened from, which the port has)",
      "", "BackButton");

    /// <summary>The four game-type buttons, in the install's own order.</summary>
    public static readonly ScenarioLobbyAction[] GameTypes = { Kingmaker, Knight, Baron, Deathmatch };

    /// <summary>
    /// The game type this screen opens on: Kingmaker, the type the war screen's
    /// Kingmaker row is about. It is the row that wears the highlight art
    /// (button_gametype1_kingmaker_highlight.tga), and the port names it rather
    /// than deriving it from a session, which it has none of.
    /// </summary>
    public const int SelectedGameType = 0;

    /// <summary>The screen's two own rows, in the order the art's slots run.</summary>
    public static readonly ScenarioLobbyAction[] Rows = { Start, Back };

    /// <summary>
    /// How many players the lobby holds: none - its slots are a network
    /// session's, and this port has no session and no online service. The
    /// shipped screen writes its player lines from the session
    /// (mp/info/playerjoined, mp/info/playerleft); the table ships no
    /// empty-state row, so the port draws none and says the state instead.
    /// </summary>
    public const int PlayerCount = 0;

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static ScenarioLobbyAction LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. ScenarioLobbyUI builds its buttons once per
    /// showing, so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a lobby action. Names the row, records it,
    /// reports what it does - and then does it: the Back row asks the flow for
    /// the war screen (Game.RequestFrontendScreen, the move every frontend click
    /// makes), and the Start row and the four game types change nothing - their
    /// clicks have named why.
    /// </summary>
    public static void Dispatch(ScenarioLobbyAction row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;

      string outcome = OutcomeFor(row);
      Debug.Log($"[ScenarioLobbyActions] click: {row.Id} '{row.Label}' -> {outcome}");

      switch (row.Kind)
      {
        case ScenarioLobbyActionKind.Back:
          Game.GetInstance()?.RequestFrontendScreen(GameState.WAR_SCREEN);
          return;

        default:
          // Start and the four game types: no session exists (no online
          // service, no lobby), so the game cannot be started - and a game type
          // is only the mode such a session would be set up for. The click has
          // said so by name.
          return;
      }
    }

    /// <summary>
    /// What one click reports: a game-type row names the mode it is and the
    /// session this port does not have, the Start row names the same missing
    /// session, and the Back row names its screen.
    /// </summary>
    public static string OutcomeFor(ScenarioLobbyAction row) => row.Kind switch
    {
      ScenarioLobbyActionKind.GameType =>
        $"{row.Outcome}; this port runs no lobby and has no online service, so nothing is set up",
      ScenarioLobbyActionKind.Start =>
        "no destination yet: the lobby has no session to start - this port has no online service and "
        + "runs no lobby, so there are no players to gather and no game to begin",
      _ => row.Outcome,
    };

    /// <summary>The selected game type's row (the one wearing the highlight art).</summary>
    public static ScenarioLobbyAction Selected => GameTypes[SelectedGameType];
  }
}
