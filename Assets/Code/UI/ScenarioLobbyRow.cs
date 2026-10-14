namespace Assets.Code.UI
{
  /// <summary>What one of the scenario lobby screen's own buttons does.</summary>
  public enum ScenarioLobbyActionKind
  {
    /// <summary>
    /// One of the lobby's four game-type buttons - the mode the lobby is set up
    /// for. The four are the install's own (ui/multiplayer/button_gametype1_
    /// kingmaker.tga .. 4_deathmatch.tga, registered by the exe as
    /// 'texture/ui/lobby/gametype/1..4'), and the first is Kingmaker: the game
    /// type the war screen's WARSCREEN_KINGMAKER row is about, and the one the
    /// screen opens on.
    /// </summary>
    GameType,

    /// <summary>
    /// The screen's own way on, LOBBYSCREEN_START 'Start': it would start the
    /// game the lobby is set up for. This port runs no lobby session and has no
    /// online service, so the click names that state rather than doing nothing
    /// silently.
    /// </summary>
    Start,

    /// <summary>
    /// The screen's own way back, LOBBYSCREEN_BACK 'Back': the war screen the
    /// screen was opened from.
    /// </summary>
    Back,
  }

  /// <summary>
  /// One of the scenario lobby screen's own rows - the screen's whole action
  /// surface - and the whole of what this port knows about it: the install's own
  /// id, the word the install paints in the row's own art, what is written
  /// beside it where the table ships a tooltip, the button's picture, its scene
  /// name, and what its click does or would do.
  ///
  /// The screen is the engine's own `ScenarioLobbyScreen` (the screen-name
  /// string 'scenario lobby screen' at VA 0x905a80, class `ScenarioLobbyScreen`),
  /// which the war screen's Kingmaker row opens - measured 2026-10-13 out of
  /// `Stronghold2.exe`: the war screen's own handler gives that
  /// row's control (the one labelled WARSCREEN_KINGMAKER 'Kingmaker') the action
  /// id whose branch pushes this screen's name. Its text family is the shipped
  /// lobby's - LOBBYSCREEN_BACK 'Back' / _NETWORK_LOBBY 'Network Lobby' /
  /// _START 'Start' - and its art is the multiplayer lobby's (ui/multiplayer/):
  /// the backdrop, the four game-type buttons and the three hundred-odd files of
  /// team/ready/difficulty/chat art beside them.
  /// </summary>
  public readonly struct ScenarioLobbyAction
  {
    public ScenarioLobbyAction(ScenarioLobbyActionKind kind, string id, string label, string tooltip,
                               string outcome, string texture, string buttonName)
    {
      Kind = kind;
      Id = id;
      Label = label;
      Tooltip = tooltip;
      Outcome = outcome;
      Texture = texture;
      ButtonName = buttonName;
    }

    /// <summary>What this button does.</summary>
    public readonly ScenarioLobbyActionKind Kind;

    /// <summary>
    /// The install's own id for this row. The two action rows carry their i18n
    /// ids (LOBBYSCREEN_START / LOBBYSCREEN_BACK); the four game-type buttons
    /// carry the exe's own registration ids ('texture/ui/lobby/gametype/1..4'),
    /// because the install ships no i18n row for a game type at all - the word
    /// is painted in the button's own art.
    /// </summary>
    public readonly string Id;

    /// <summary>
    /// The word this row wears. For the two action rows it is the shipped text
    /// (LOBBYSCREEN_START 'Start', LOBBYSCREEN_BACK 'Back'). For the four game
    /// types it is the install's own word for the mode - painted in the button's
    /// art, and in the shipped data only as the button's file name
    /// (button_gametype1_kingmaker.tga and its three siblings), so the screen
    /// draws no words over those four: the art IS the label.
    /// </summary>
    public readonly string Label;

    /// <summary>
    /// The shipped tooltip content where the table has one. The lobby's three
    /// rows ship none (measured: LOBBYSCREEN_BACK / _NETWORK_LOBBY / _START have
    /// no STATICTOOLTIP siblings), and a game type has no row to carry one.
    /// </summary>
    public readonly string Tooltip;

    /// <summary>What this click does or would do, in one sentence.</summary>
    public readonly string Outcome;

    /// <summary>The row's own picture: the install's own button art.</summary>
    public readonly string Texture;

    /// <summary>The name the row's button carries in the scene ("StartButton").</summary>
    public readonly string ButtonName;
  }
}
