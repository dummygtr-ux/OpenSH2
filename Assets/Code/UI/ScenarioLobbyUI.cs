using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The scenario lobby screen (GameState.SCENARIO_LOBBY_SCREEN) - the shipped
  /// game's multiplayer lobby, which the war screen's WARSCREEN_KINGMAKER row
  /// opens (see <see cref="ScenarioLobbyActions"/> for the measurement).
  ///
  /// What it is made of is the install's, not this port's invention. The
  /// backdrop is the file the screen's own class loads
  /// (ui/multiplayer/multiplayer_background.jpg, 1024x768): the map frame with
  /// the four game-type medallions below it, the player panel down the right,
  /// the chat panel along the bottom and the round exit medallion in the
  /// picture's bottom-left corner. Measured 2026-10-13 off the
  /// picture itself: the map frame is x 70..660, y 31..482 with its separator
  /// line at y 343; the four medallions are centred at (217, 375), (282, 375),
  /// (217, 444) and (282, 444), each about 65 px across - exactly the size of
  /// the four ui/multiplayer/button_gametype*.tga buttons the engine draws over
  /// them; the player panel is x 685..950, y 31..296; the chat panel is
  /// x 125..655, y 497..737 with its input line at y 698; and the exit medallion
  /// is centred at (77, 697), also about 65 px across.
  ///
  /// Into those places the screen puts the install's own data: the four game-type
  /// buttons on the medallions the backdrop paints - KINGMAKER, the type the war
  /// screen's row came in by, wearing its own highlight art
  /// (button_gametype1_kingmaker_highlight.tga), and Knight, Baron and Deathmatch
  /// beside it - the shipped lobby name row (LOBBYSCREEN_NETWORK_LOBBY 'Network
  /// Lobby') on the map frame, the screen's own way on (LOBBYSCREEN_START
  /// 'Start') on the frontend's fancy button, and its way back (LOBBYSCREEN_BACK
  /// 'Back') on the exit medallion the art paints. The lobby's PLAYER LIST IS
  /// EMPTY: its slots hold a network session's players, the port has no session
  /// and no online service, and the table ships no empty-state row - so no player
  /// line is drawn and the report says so (see ScenarioLobbyActions).
  ///
  /// The screen owns its own canvas above the war screen's and its own
  /// aspect-ratio bars, like every frontend screen here.
  /// </summary>
  class ScenarioLobbyUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private Text headerText;
    private RectTransform headerRect;
    private RectTransform startRect;
    private RectTransform backRect;
    private readonly RectTransform[] gameTypeRects = new RectTransform[ScenarioLobbyActions.GameTypes.Length];

    // The slots the backdrop itself paints, in fractions of the 4:3 content
    // rect (up-positive, centre origin - the campaign screen's own convention,
    // so a measured pixel px of the 1024-wide art is px/1024 - 0.5). Measured
    // off the picture's own painted frames and medallions (see the class note).
    private const float HeaderCentreX = -0.143555f;  // (110 + 510/2) px of 1024, from the centre
    private const float HeaderCentreY = 0.395833f;   // 0.5 - (58 + 44/2) / 768
    private const float HeaderWidth = 0.498047f;     // 510 px of 1024
    private const float HeaderHeight = 0.057292f;    // 44 px of 768

    // The four game-type medallions the backdrop paints, each the size of the
    // TGA button the screen draws over it (65x67 px).
    private const float GameTypeWidth = 0.063477f;   // 65 px of 1024
    private const float GameTypeHeight = 0.087240f;  // 67 px of 768
    private const float GameTypeLeftCentreX = -0.288086f;   // (185 + 65/2) px of 1024
    private const float GameTypeRightCentreX = -0.224609f;  // (250 + 65/2) px of 1024
    private const float GameTypeTopCentreY = 0.011719f;     // 0.5 - (342 + 67/2) / 768
    private const float GameTypeBottomCentreY = -0.078125f; // 0.5 - (411 + 67/2) / 768

    // The screen's own way on, on the frontend's fancy button (332x81 tga), in
    // the chat panel's free band - the place the shipped lobby fills with its
    // player list, which this port's lobby does not have.
    private const float StartCentreX = -0.119141f;   // (278 + 225/2) px of 1024
    private const float StartCentreY = -0.359375f;   // 0.5 - (630 + 61/2) / 768
    private const float StartWidth = 0.219727f;      // 225 px of 1024
    private const float StartHeight = 0.079427f;     // 61 px of 768

    // The screen's way back, on the round medallion the picture paints in its
    // bottom-left corner (about 65 px across).
    private const float BackCentreX = -0.424805f;    // (45 + 65/2) px of 1024
    private const float BackCentreY = -0.407552f;    // 0.5 - (665 + 65/2) / 768
    private const float BackWidth = 0.063477f;       // 65 px of 1024
    private const float BackHeight = 0.084635f;      // 65 px of 768

    private static readonly Color32 LabelColour = new(255, 244, 214, 255);

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the war screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, the install's own
    /// picture of the lobby, its shipped name row, the four game-type buttons on
    /// the medallions the picture paints, and the screen's two own rows. No
    /// player line is drawn: the lobby has no session (see the class note).
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      ScenarioLobbyActions.ResetRecord();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreateHeader();
      CreateGameTypeButtons();
      CreateRowButtons();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Scenario lobby screen: shown ({ScenarioLobbyActions.PlayerCount} player(s) - the "
                + "lobby's slots hold a network session's players, and the port has no session and no "
                + "online service; the table ships no empty-state row, so no player line is drawn). "
                + $"Game type: {ScenarioLobbyActions.Selected.Label} "
                + $"({Path.GetFileName(SelectedGameTypeTexture)}).");
    }

    public void Hide()
    {
      if (uiRoot != null)
      {
        aspectRatioBars?.Hide();
        aspectRatioBars = null;
        Object.Destroy(uiRoot);
        uiRoot = null;
        contentRoot = null;
        headerText = null;
        headerRect = null;
        startRect = null;
        backRect = null;
        for (int i = 0; i < gameTypeRects.Length; i++) gameTypeRects[i] = null;
      }

      IsShown = false;
    }

    /// <summary>
    /// The flow's own call: this screen is up exactly when the flow's state
    /// names it, so a tick that finds it should be up shows it (and lays it
    /// out), and a tick that does not hides it - which takes its aspect-ratio
    /// bars down with its root.
    /// </summary>
    public void SetShown(bool shown)
    {
      if (shown)
      {
        if (!IsShown) Show();
        UpdateLayout();
      }
      else if (IsShown)
      {
        Hide();
      }
    }

    /// <summary>
    /// The picture the selected game type wears: Kingmaker's own highlight art
    /// (button_gametype1_kingmaker_highlight.tga) for the type the screen opens
    /// on, and the plain button for the other three.
    /// </summary>
    public static string SelectedGameTypeTexture =>
      ScenarioLobbyActions.GameTypes[ScenarioLobbyActions.SelectedGameType].Texture;

    /// <summary>The picture a game-type button wears (its own art, or the highlight for the selected one).</summary>
    public static string GameTypeTextureFor(int index) =>
      index == ScenarioLobbyActions.SelectedGameType
        ? UIAssets.GameTypeKingmakerHighlightTexture
        : ScenarioLobbyActions.GameTypes[index].Texture;

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("ScenarioLobbyUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;  // the war screen's other branches' own order

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("ScenarioLobbyUIContentRoot");
      contentObject.transform.SetParent(uiRoot.transform, false);
      contentRoot = contentObject.AddComponent<RectTransform>();
      contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
      contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
      contentRoot.pivot = new Vector2(0.5f, 0.5f);
      contentRoot.sizeDelta = new Vector2(Screen.height * (4f / 3f), Screen.height);
      contentRoot.anchoredPosition = Vector2.zero;
    }

    private void CreateBackground()
    {
      GameObject backgroundObject = new("ScenarioLobbyUIBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.MultiplayerLobbyBackgroundTexture);
      // The shipped art is 1024x768 - the content rect's own 4:3 - so filling the
      // rect is filling it without distortion.
      backgroundImage.type = Image.Type.Simple;
      backgroundImage.preserveAspect = false;
      backgroundImage.raycastTarget = true;  // nothing behind this screen may be clicked

      RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
      backgroundRect.anchorMin = Vector2.zero;
      backgroundRect.anchorMax = Vector2.one;
      backgroundRect.offsetMin = Vector2.zero;
      backgroundRect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// The screen's shipped name row, LOBBYSCREEN_NETWORK_LOBBY 'Network
    /// Lobby', across the map frame the picture paints. The art itself paints no
    /// title strip, so this is the port's own place for it, named on the report
    /// (the shipped screen fills that frame with the selected map's preview).
    /// </summary>
    private void CreateHeader()
    {
      GameObject headerObject = new("ScenarioLobbyUIHeader");
      headerObject.transform.SetParent(contentRoot, false);

      headerText = headerObject.AddComponent<Text>();
      headerText.text = ScenarioLobbyActions.HeaderLabel;
      headerText.alignment = TextAnchor.MiddleCenter;
      headerText.color = LabelColour;
      headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      headerText.horizontalOverflow = HorizontalWrapMode.Wrap;
      headerText.verticalOverflow = VerticalWrapMode.Truncate;
      headerText.resizeTextForBestFit = true;
      headerText.resizeTextMinSize = 8;
      headerText.raycastTarget = false;

      headerRect = headerObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// The lobby's four game-type buttons, standing on the four medallions the
    /// backdrop paints (the engine draws the same ui/multiplayer/
    /// button_gametype*.tga art over them). The selected type - Kingmaker, the
    /// one the war screen's row came in by - wears its own highlight art. The
    /// install ships no text row for a game type: the word is painted in the
    /// button, so no label is drawn over any of the four.
    /// </summary>
    private void CreateGameTypeButtons()
    {
      for (int i = 0; i < ScenarioLobbyActions.GameTypes.Length; i++)
      {
        ScenarioLobbyAction gameType = ScenarioLobbyActions.GameTypes[i];

        GameObject buttonObject = new(gameType.ButtonName);
        buttonObject.transform.SetParent(contentRoot, false);

        Image image = buttonObject.AddComponent<Image>();
        image.sprite = SpriteLoader.LoadSprite(GameTypeTextureFor(i));
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        ScenarioLobbyAction captured = gameType;
        button.onClick.AddListener(() => ScenarioLobbyActions.Dispatch(captured));

        gameTypeRects[i] = buttonObject.GetComponent<RectTransform>();
      }
    }

    /// <summary>
    /// The screen's two own rows: LOBBYSCREEN_START 'Start' on the frontend's
    /// fancy button in the chat panel's free band, and LOBBYSCREEN_BACK 'Back'
    /// on the round exit medallion the picture paints in its bottom-left corner
    /// (a transparent button - the medallion IS the install's picture for the
    /// row - with the shipped label on it).
    /// </summary>
    private void CreateRowButtons()
    {
      startRect = CreateStartButton();
      backRect = CreateBackButton();
    }

    private RectTransform CreateStartButton()
    {
      ScenarioLobbyAction row = ScenarioLobbyActions.Start;

      GameObject buttonObject = new(row.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image image = buttonObject.AddComponent<Image>();
      image.sprite = SpriteLoader.LoadSprite(row.Texture);
      image.type = Image.Type.Simple;
      image.preserveAspect = false;
      image.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = image;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => ScenarioLobbyActions.Dispatch(row));

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = row.Label;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = LabelColour;
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.resizeTextForBestFit = true;
      label.resizeTextMinSize = 8;
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = new Vector2(6f, 2f);
      textRect.offsetMax = new Vector2(-6f, -2f);

      return buttonObject.GetComponent<RectTransform>();
    }

    private RectTransform CreateBackButton()
    {
      ScenarioLobbyAction row = ScenarioLobbyActions.Back;

      GameObject buttonObject = new(row.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image image = buttonObject.AddComponent<Image>();
      image.color = new Color(0f, 0f, 0f, 0f);
      image.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = image;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => ScenarioLobbyActions.Dispatch(row));

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = row.Label;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = LabelColour;
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.resizeTextForBestFit = true;
      label.resizeTextMinSize = 8;
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = new Vector2(2f, 2f);
      textRect.offsetMax = new Vector2(-2f, -2f);

      return buttonObject.GetComponent<RectTransform>();
    }

    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      // The shipped lobby name row across the map frame.
      if (headerText != null)
      {
        headerText.fontSize = Mathf.Max(10, Mathf.RoundToInt(height * 0.026f));
        headerText.resizeTextMaxSize = headerText.fontSize;
        Place(headerRect, new Vector2(width * HeaderCentreX, height * HeaderCentreY),
              new Vector2(width * HeaderWidth, height * HeaderHeight));
      }

      // The four game-type buttons on the medallions the picture paints.
      Place(gameTypeRects[0], new Vector2(width * GameTypeLeftCentreX, height * GameTypeTopCentreY),
            new Vector2(width * GameTypeWidth, height * GameTypeHeight));
      Place(gameTypeRects[1], new Vector2(width * GameTypeRightCentreX, height * GameTypeTopCentreY),
            new Vector2(width * GameTypeWidth, height * GameTypeHeight));
      Place(gameTypeRects[2], new Vector2(width * GameTypeLeftCentreX, height * GameTypeBottomCentreY),
            new Vector2(width * GameTypeWidth, height * GameTypeHeight));
      Place(gameTypeRects[3], new Vector2(width * GameTypeRightCentreX, height * GameTypeBottomCentreY),
            new Vector2(width * GameTypeWidth, height * GameTypeHeight));

      // The screen's two own rows.
      Place(startRect, new Vector2(width * StartCentreX, height * StartCentreY),
            new Vector2(width * StartWidth, height * StartHeight));
      Place(backRect, new Vector2(width * BackCentreX, height * BackCentreY),
            new Vector2(width * BackWidth, height * BackHeight));
    }

    private static void Place(RectTransform rect, Vector2 centre, Vector2 size)
    {
      if (rect == null) return;
      rect.anchorMin = new Vector2(0.5f, 0.5f);
      rect.anchorMax = new Vector2(0.5f, 0.5f);
      rect.pivot = new Vector2(0.5f, 0.5f);
      rect.sizeDelta = size;
      rect.anchoredPosition = centre;
    }
  }
}
