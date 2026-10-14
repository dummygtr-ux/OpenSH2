using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The load screen (GameState.LOAD_SCREEN) - the shipped game's save/load
  /// list the war screen's WARSCREEN_LOAD row opens.
  ///
  /// What it is made of is the install's, not this port's invention. The
  /// backdrop is the picture the screen's own class loads
  /// (ui/ingame_menus/scenario_loadscreen.jpg, 1024x768 - the engine's
  /// `LoadScenarioScreen` loads it as 'texture/loadscenario background'): the
  /// barbarian and the knight flanking a big framed panel whose brown title
  /// strip carries the screen's header, with a scrollbar groove along the
  /// panel's right and two button plates along the bottom. Measured 2026-10-13
  /// off its own gold lines: the panel's frame is x 214..801, y 105..662, its
  /// title strip y 113..139, its list area y 140..658 (the scrollbar groove
  /// around x 758..790), and the two plates are x 221..463 and x 553..791,
  /// both y 686..728.
  ///
  /// Into those places the screen puts the install's own data: the shipped
  /// header (LOADSCENARIOSCREEN_HEADER_LOAD 'Load' - the screen's LOAD mode)
  /// in the title strip, the three column headers (LOADSCENARIOSCREEN_NAME
  /// 'Name' / _DATE 'Date' / _PLAYERS 'P') at the top of the list area, and the
  /// screen's own two rows (LOADSCENARIOSCREEN_LOAD 'Load' /
  /// LOADSCENARIOSCREEN_CANCEL 'Cancel') on the two plates the art paints. The
  /// list itself is EMPTY: it shows the player's own saved games, the port
  /// ships none, and the table ships no empty-state row - so no row is drawn
  /// and the report says so (see LoadScreenActions).
  ///
  /// The screen owns its own canvas above the war screen's and its own
  /// aspect-ratio bars, like every frontend screen here.
  /// </summary>
  class LoadScreenUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private Text headerText;
    private RectTransform headerRect;
    private RectTransform loadRect;
    private RectTransform cancelRect;
    private readonly RectTransform[] sortRects = new RectTransform[LoadScreenActions.Sorts.Length];

    // The slots the backdrop itself paints, in fractions of the 4:3 content
    // rect (up-positive, centre origin - the campaign screen's own convention,
    // so a measured pixel px of the 1024-wide art is px/1024 - 0.5). Measured
    // off the picture's own gold lines (see the class note).
    private const float HeaderCentreX = -0.003418f;  // (222 + 573/2) px of 1024, from the centre
    private const float HeaderCentreY = 0.335938f;   // 0.5 - (113 + 26/2) / 768
    private const float HeaderWidth = 0.559570f;     // 573 px of 1024
    private const float HeaderHeight = 0.033854f;    // 26 px of 768

    // The list area's own header row (the three columns' headers, which are the
    // screen's sort rows): the list area is x 222..790, y 140..658, and its
    // first row stands at its top.
    private const float SortRowCentreY = 0.292969f;  // 0.5 - (145 + 28/2) / 768
    private const float SortRowHeight = 0.036458f;   // 28 px of 768
    private const float NameCentreX = -0.109375f;    // (240 + 320/2) px of 1024, from the centre
    private const float NameWidth = 0.312500f;       // 320 px of 1024
    private const float DateCentreX = 0.115234f;     // (560 + 140/2) px of 1024, from the centre
    private const float DateWidth = 0.136719f;       // 140 px of 1024
    private const float PlayersCentreX = 0.220215f;  // (700 + 75/2) px of 1024, from the centre
    private const float PlayersWidth = 0.073242f;    // 75 px of 1024

    // The two plates the art paints along the bottom, which the screen's two
    // rows stand on (the buttons are transparent; the plate is the picture).
    private const float LoadCentreX = -0.166016f;    // (221 + 242/2) px of 1024, from the centre
    private const float LoadCentreY = -0.420573f;    // 0.5 - (686 + 42/2) / 768
    private const float LoadWidth = 0.236328f;       // 242 px of 1024
    private const float CancelCentreX = 0.156250f;   // (553 + 238/2) px of 1024, from the centre
    private const float CancelCentreY = -0.418620f;  // 0.5 - (686 + 40/2) / 768
    private const float CancelWidth = 0.232422f;     // 238 px of 1024
    private const float PlateHeight = 0.054688f;     // 42 px of 768

    private static readonly Color32 LabelColour = new(255, 244, 214, 255);

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the war screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, the install's
    /// own picture of the screen, its shipped header, the three column headers
    /// (the sort rows) and the two rows on the plates the art paints. No list
    /// row is drawn: the list is empty (see the class note).
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      LoadScreenActions.ResetRecord();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreateHeader();
      CreateSortRows();
      CreateRowButtons();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Load screen: shown ({LoadScreenActions.SavedGameCount} saved game(s) - the port "
                + "ships none (the shipped game lists the player's own saves from the user's "
                + "Documents folder, \"\\Stronghold 2\\Saves\\\"); the list is empty and the table "
                + "ships no empty-state row, so no row is drawn).");
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
        loadRect = null;
        cancelRect = null;
        for (int i = 0; i < sortRects.Length; i++) sortRects[i] = null;
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

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("LoadScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;  // the war screen's other branches' own order

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("LoadScreenUIContentRoot");
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
      GameObject backgroundObject = new("LoadScreenUIBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.LoadScenarioBackgroundTexture);
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
    /// The screen's shipped header in the title strip the art paints for it:
    /// LOADSCENARIOSCREEN_HEADER_LOAD 'Load' - the LOAD mode, which is the mode
    /// the war screen's Load row opens (the SAVE mode is the in-game menu's).
    /// </summary>
    private void CreateHeader()
    {
      GameObject headerObject = new("LoadScreenUIHeader");
      headerObject.transform.SetParent(contentRoot, false);

      headerText = headerObject.AddComponent<Text>();
      headerText.text = LoadScreenActions.HeaderLabel;
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
    /// The list's three column headers at the top of the panel - 'Name',
    /// 'Date', 'P' - which are also the screen's three sort rows: each one's
    /// click is the shipped sort the table names beside it (Sort by Name /
    /// Sort by Date / Sort by Number of Players). They are transparent buttons
    /// with the shipped labels drawn on them; below them the list is empty and
    /// no row is drawn.
    /// </summary>
    private void CreateSortRows()
    {
      for (int i = 0; i < LoadScreenActions.Sorts.Length; i++)
      {
        LoadScreenAction sort = LoadScreenActions.Sorts[i];

        GameObject buttonObject = new(sort.ButtonName);
        buttonObject.transform.SetParent(contentRoot, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        LoadScreenAction captured = sort;
        button.onClick.AddListener(() => LoadScreenActions.Dispatch(captured));

        sortRects[i] = buttonObject.GetComponent<RectTransform>();

        GameObject textObject = new("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        label.text = sort.Label;
        label.alignment = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
        label.color = LabelColour;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 8;
        label.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);
      }
    }

    /// <summary>
    /// The screen's two own rows on the two plates the art paints along the
    /// bottom: LOADSCENARIOSCREEN_LOAD 'Load' and LOADSCENARIOSCREEN_CANCEL
    /// 'Cancel'. The buttons are transparent - the plate IS the install's
    /// picture for the row - and each carries the shipped label.
    /// </summary>
    private void CreateRowButtons()
    {
      loadRect = CreatePlateButton(LoadScreenActions.Load);
      cancelRect = CreatePlateButton(LoadScreenActions.Cancel);
    }

    private RectTransform CreatePlateButton(LoadScreenAction row)
    {
      GameObject buttonObject = new(row.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image image = buttonObject.AddComponent<Image>();
      image.color = new Color(0f, 0f, 0f, 0f);
      image.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = image;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => LoadScreenActions.Dispatch(row));

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

    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      // The shipped header, in the title strip along the panel's top.
      if (headerText != null)
      {
        headerText.fontSize = Mathf.Max(10, Mathf.RoundToInt(height * 0.021f));
        headerText.resizeTextMaxSize = headerText.fontSize;
        Place(headerRect, new Vector2(width * HeaderCentreX, height * HeaderCentreY),
              new Vector2(width * HeaderWidth, height * HeaderHeight));
      }

      // The three column headers at the top of the list area.
      Place(sortRects[0], new Vector2(width * NameCentreX, height * SortRowCentreY),
            new Vector2(width * NameWidth, height * SortRowHeight));
      Place(sortRects[1], new Vector2(width * DateCentreX, height * SortRowCentreY),
            new Vector2(width * DateWidth, height * SortRowHeight));
      Place(sortRects[2], new Vector2(width * PlayersCentreX, height * SortRowCentreY),
            new Vector2(width * PlayersWidth, height * SortRowHeight));

      // The screen's two rows, on the plates the art paints.
      Place(loadRect, new Vector2(width * LoadCentreX, height * LoadCentreY),
            new Vector2(width * LoadWidth, height * PlateHeight));
      Place(cancelRect, new Vector2(width * CancelCentreX, height * CancelCentreY),
            new Vector2(width * CancelWidth, height * PlateHeight));
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
