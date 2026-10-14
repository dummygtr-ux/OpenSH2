using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The map request screen (GameState.MAP_REQUEST_SCREEN) - the install's own
  /// map list the war screen's WARSCREEN_CUSTOM row opens.
  ///
  /// What it is made of is the install's, not this port's invention. The
  /// backdrop is the install's own picture of the screen for the WAR path,
  /// ui/map_select/mapselect_background_war.jpg (1024x768, the barbarian and
  /// the knight; the folder's mapselect_background.jpg is the same screen with
  /// the PEACE path's monk and woman). It paints the screen's whole structure,
  /// measured 2026-10-13 off its own frames: the big empty frame at the top
  /// left (opening x 244..492, y 42..308 - the engine's map view), the frame
  /// beside it (x 505..790, y 33..317 - the folder's _big/_small variants mark
  /// their map windows inside it: 256x256 at x 525..780, y 47..302, or 126x125
  /// at x 526..651, y 176..300, which the war picture paints black), the panel
  /// along the bottom (outer frame x 237..790, y 320..727) with its brown title
  /// strip (y 329..355), its list area and its scrollbar groove, and the two
  /// green arrow medallions the art itself paints (left x 53..99, right
  /// x 925..971, both y 672..719).
  ///
  /// Into those places the screen puts the install's own data: the mode's own
  /// 256x256 picture, mapselect_custom.bmp (the castle at night - the "custom"
  /// of the three modes the exe registers: 'texture/ui/custom', '/freebuild',
  /// '/load'), drawn in the big window's exact rect, which is the size the
  /// folder's own _big variant leaves for a picture; the install's own maps
  /// down the list area, twelve to a page; the selected map's own file stem in
  /// the title strip; and the table's own row, MAPREQUESTSCREEN_PLAY 'Play'.
  ///
  /// Four things the install does not pin, and this port says so rather than
  /// pretending otherwise: the two painted arrows' WORDS (the table ships no
  /// row for either - the port draws none, the painted arrow is the picture,
  /// and the click names what it does on the console and in the report); the
  /// Play row's PLACE and PICTURE (ui/map_select/ ships no button art at all,
  /// so it wears ui/siege_menu/button_play.bmp, the install's own castle-
  /// scenario play button, and stands in the free band between the two
  /// medallions); the way back's row and place (the screen ships no exit row,
  /// so it wears the war screen's own WARSCREENSTATICTOOLTIP_EXIT 'Previous
  /// menu' on the frontend's fancy button - the trail's own deal - beside the
  /// Play row); and the two map view panes the engine renders into (the black
  /// frame at the top left stays as the art paints it, named on the report -
  /// this port has no scenario-view renderer for it).
  ///
  /// The screen owns its own canvas above the war screen's and its own
  /// aspect-ratio bars, like every frontend screen here.
  /// </summary>
  class MapRequestScreenUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private Image modeImage;
    private RectTransform modeRect;
    private Text titleText;
    private RectTransform titleRect;
    private readonly Text[] rowLabels = new Text[MapRequestScreenActions.PageSize];
    private readonly RectTransform[] rowRects = new RectTransform[MapRequestScreenActions.PageSize];
    private RectTransform playRect;
    private RectTransform previousRect;
    private RectTransform nextRect;
    private RectTransform backRect;

    // The slots the backdrop itself paints, in fractions of the 4:3 content
    // rect (up-positive, centre origin - the campaign screen's own convention,
    // so a measured pixel px of the 1024-wide art is px/1024 - 0.5). Measured
    // off the war picture's own frames (see the class note).
    private const float ModeCentreX = 0.137207f;    // (525 + 256/2) px of 1024, from the centre
    private const float ModeCentreY = 0.272786f;    // 0.5 - (47 + 256/2) / 768
    private const float ModeWidth = 0.250000f;      // 256 px of 1024
    private const float ModeHeight = 0.333333f;     // 256 px of 768

    // The title strip (x 237..790, y 329..355): the selected map's own stem.
    private const float TitleCentreX = 0.001465f;   // (237 + 790) / 2 px of 1024, from the centre
    private const float TitleCentreY = 0.054687f;   // 0.5 - (329 + 26/2) / 768
    private const float TitleWidth = 0.540039f;     // 553 px of 1024
    private const float TitleHeight = 0.033854f;    // 26 px of 768

    // The list area (x 246..742, first row's top y 358, ~26 px pitch - the
    // siege list's own reading of a leather panel's rows), twelve rows a page,
    // ending clear of the two painted medallions' band (y 672..719).
    private const float RowCentreX = -0.017578f;    // (246 + 496/2) px of 1024, from the centre
    private const float RowWidth = 0.484375f;       // 496 px of 1024
    private const float RowTopY = 0.033854f;        // 0.5 - 358 / 768  (the first row's top edge)
    private const float RowHeight = 0.033854f;      // 26 px of 768

    // The Play row and the way back, in the free band between the two painted
    // medallions (the install pins no place for either - see the class note).
    private const float PlayCentreX = -0.109375f;   // 400 px of 1024, from the centre
    private const float BackCentreX = 0.134766f;    // 650 px of 1024, from the centre
    private const float ButtonCentreY = -0.406250f; // 0.5 - (696 + 45/2) / 768
    private const float PlayWidth = 0.205078f;      // 210 px of 1024 (button_play.bmp's own 266:57 aspect)
    private const float PlayHeight = 0.058594f;     // 45 px of 768
    private const float BackWidth = 0.180664f;      // 185 px of 1024 (the fancy button's own 332:81 aspect)
    private const float BackHeight = 0.058594f;     // 45 px of 768

    // The two painted arrows' own place (measured: x 53..99 / x 925..971,
    // y 672..719 - the buttons are a touch larger than the paint).
    private const float ArrowCentreX = 0.425781f;   // 76 / 924 px of 1024, from the centre
    private const float ArrowCentreY = -0.405599f;  // 0.5 - (672 + 47/2) / 768
    private const float ArrowWidth = 0.046875f;     // 48 px of 1024
    private const float ArrowHeight = 0.062500f;    // 48 px of 768

    private static readonly Color32 SelectedRowColour = new(255, 244, 214, 255);
    private static readonly Color32 OtherRowColour = new(186, 168, 128, 255);

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the war screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, the install's
    /// own war-path backdrop, the mode's own picture in the big window the art's
    /// variants measure, the selected map's stem in the title strip, twelve list
    /// rows down the leather panel, and the four fixed buttons - the two painted
    /// arrows, the Play row and the way back.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it
      // (and the screen opens on the install's list's first page again).
      MapRequestScreenActions.ResetRecord();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreateModePicture();
      CreateTitle();
      CreateMapRows();
      CreateFixedButtons();
      Refresh();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Map request screen: shown ({MapRequestScreenActions.Maps.Length} maps, "
                + $"page 1 of {MapRequestScreenActions.PageCount}, showing "
                + $"'{MapRequestScreenActions.Selected.Stem}').");
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
        modeImage = null;
        modeRect = null;
        titleText = null;
        titleRect = null;
        playRect = null;
        previousRect = null;
        nextRect = null;
        backRect = null;
        for (int i = 0; i < rowLabels.Length; i++)
        {
          rowLabels[i] = null;
          rowRects[i] = null;
        }
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
    /// The screen's own refresh, after a list row's or a paging click moved the
    /// selection or the page (MapRequestScreenActions.Dispatch): the selected
    /// map's own stem in the title strip and its page's rows down the leather
    /// panel, with the selected map's row highlighted. Called by the entry
    /// point, never by a click directly, so the screen's own record and what is
    /// on the screen cannot disagree.
    /// </summary>
    public void Refresh()
    {
      MapRequestMap selected = MapRequestScreenActions.Selected;
      if (titleText != null)
      {
        titleText.text = selected.Stem;
      }

      for (int i = 0; i < rowRects.Length; i++)
      {
        bool onThisPage = i < MapRequestScreenActions.RowsOnPage;
        if (rowRects[i] != null && rowRects[i].gameObject.activeSelf != onThisPage)
        {
          rowRects[i].gameObject.SetActive(onThisPage);
        }
        if (!onThisPage || rowLabels[i] == null) continue;

        MapRequestMap map = MapRequestScreenActions.MapAt(i);
        rowLabels[i].text = map.Stem;
        rowLabels[i].color = MapRequestScreenActions.Page * MapRequestScreenActions.PageSize + i
                             == MapRequestScreenActions.SelectedIndex
          ? SelectedRowColour : OtherRowColour;
      }

      Debug.Log($"Map request screen: page {MapRequestScreenActions.Page + 1} of "
                + $"{MapRequestScreenActions.PageCount} is up ({MapRequestScreenActions.RowsOnPage} "
                + $"of {MapRequestScreenActions.Maps.Length} map(s)), '{selected.Stem}' selected "
                + $"({selected.FileName}).");
    }

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("MapRequestScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;  // the campaign screen's, the trail's and the siege's own order - all branches of the war screen's

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("MapRequestScreenUIContentRoot");
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
      GameObject backgroundObject = new("MapRequestScreenUIBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.MapSelectBackgroundWarTexture);
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
    /// The mode's own 256x256 picture (mapselect_custom.bmp - the "custom" of
    /// the three modes the exe registers), drawn in the big window's exact
    /// rect, which is the size the folder's own _big variant leaves for a map
    /// picture. The shipped engine would render the selected map's own view
    /// here; this port has no scenario-view renderer, so it shows the install's
    /// own picture for this screen's mode and names that reading on the report.
    /// </summary>
    private void CreateModePicture()
    {
      GameObject modeObject = new("MapRequestScreenUIMode");
      modeObject.transform.SetParent(contentRoot, false);

      modeImage = modeObject.AddComponent<Image>();
      modeImage.sprite = SpriteLoader.LoadSprite(UIAssets.MapSelectCustomModeTexture);
      modeImage.type = Image.Type.Simple;
      modeImage.preserveAspect = false;  // the picture is the window's own size
      modeImage.raycastTarget = false;   // the picture itself is not a control

      modeRect = modeObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// The title strip along the list panel's top: the selected map's own file
    /// stem. The table ships no title row for this screen, so the strip carries
    /// the selection's own name - the install's data, not a word this port
    /// invented.
    /// </summary>
    private void CreateTitle()
    {
      GameObject titleObject = new("MapRequestScreenUITitle");
      titleObject.transform.SetParent(contentRoot, false);

      titleText = titleObject.AddComponent<Text>();
      titleText.text = MapRequestScreenActions.Selected.Stem;
      titleText.alignment = TextAnchor.MiddleCenter;
      titleText.color = SelectedRowColour;
      titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
      titleText.verticalOverflow = VerticalWrapMode.Truncate;
      titleText.resizeTextForBestFit = true;
      titleText.resizeTextMinSize = 8;
      titleText.raycastTarget = false;

      titleRect = titleObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// The install's own map list: one row per map of the current page, down
    /// the panel's leather, each row showing the map's own file stem. Every
    /// row's click is the screen's own entry point with that row, and the
    /// selected map is the one the title strip shows and the Play row acts on.
    /// </summary>
    private void CreateMapRows()
    {
      for (int i = 0; i < MapRequestScreenActions.PageSize; i++)
      {
        MapRequestScreenAction row = MapRequestScreenActions.SelectRow(i);

        GameObject rowObject = new(row.ButtonName);
        rowObject.transform.SetParent(contentRoot, false);

        // The row's own picture is the leather showing through, so it carries a
        // fully transparent Image to be clickable (the screen's own decision:
        // the install ships no picture for a list row).
        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = new Color(0f, 0f, 0f, 0f);
        rowImage.raycastTarget = true;

        Button button = rowObject.AddComponent<Button>();
        button.targetGraphic = rowImage;
        button.transition = Selectable.Transition.None;
        // The buttons are built ONCE and their labels are re-filled per page, so
        // the row a click dispatches is resolved when the click happens - a
        // captured row would keep pointing at the page the screen was built on
        // (measured: the first run's page-3 row click dispatched the page-1 map
        // of the same row index).
        int pageIndex = i;
        button.onClick.AddListener(
          () => MapRequestScreenActions.Dispatch(MapRequestScreenActions.SelectRow(pageIndex)));

        rowRects[i] = rowObject.GetComponent<RectTransform>();

        GameObject textObject = new("Label");
        textObject.transform.SetParent(rowObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        label.text = row.Label;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = i == MapRequestScreenActions.SelectedIndex ? SelectedRowColour : OtherRowColour;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 6;
        label.raycastTarget = false;
        rowLabels[i] = label;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);
      }
    }

    /// <summary>
    /// The screen's four fixed buttons: the two painted arrows (transparent
    /// buttons exactly over the medallions the art paints - the arrow IS the
    /// install's picture, and the row wears no words the install does not
    /// ship), the Play row on the install's own castle-scenario play button,
    /// and the way back on the frontend's fancy button with the war screen's
    /// own 'Previous menu'.
    /// </summary>
    private void CreateFixedButtons()
    {
      CreateArrowButton(MapRequestScreenActions.PreviousPage, out previousRect);
      CreateArrowButton(MapRequestScreenActions.NextPage, out nextRect);

      GameObject playObject = new(MapRequestScreenActions.Play.ButtonName);
      playObject.transform.SetParent(contentRoot, false);
      Image playImage = playObject.AddComponent<Image>();
      playImage.sprite = SpriteLoader.LoadSprite(MapRequestScreenActions.Play.Texture);
      playImage.type = Image.Type.Simple;
      playImage.preserveAspect = false;  // drawn at the button's own measured aspect
      Button playButton = playObject.AddComponent<Button>();
      playButton.targetGraphic = playImage;
      playButton.transition = Selectable.Transition.None;
      playButton.onClick.AddListener(() => MapRequestScreenActions.Dispatch(MapRequestScreenActions.Play));
      playRect = playObject.GetComponent<RectTransform>();
      AddLabel(playObject, MapRequestScreenActions.Play.Label, new Vector2(6f, 2f), new Vector2(-6f, -2f));

      GameObject backObject = new(MapRequestScreenActions.Back.ButtonName);
      backObject.transform.SetParent(contentRoot, false);
      Image backImage = backObject.AddComponent<Image>();
      backImage.sprite = SpriteLoader.LoadSprite(MapRequestScreenActions.Back.Texture);
      backImage.type = Image.Type.Simple;
      backImage.preserveAspect = false;  // drawn at the fancy button's own aspect
      Button backButton = backObject.AddComponent<Button>();
      backButton.targetGraphic = backImage;
      backButton.transition = Selectable.Transition.None;
      backButton.onClick.AddListener(() => MapRequestScreenActions.Dispatch(MapRequestScreenActions.Back));
      backRect = backObject.GetComponent<RectTransform>();
      AddLabel(backObject, MapRequestScreenActions.Back.Label, new Vector2(6f, 2f), new Vector2(-6f, -2f));
    }

    private void CreateArrowButton(MapRequestScreenAction action, out RectTransform rect)
    {
      GameObject buttonObject = new(action.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      // Fully transparent: the install's own painted arrow shows through, and
      // the button exists so the click lands on it.
      Image image = buttonObject.AddComponent<Image>();
      image.color = new Color(0f, 0f, 0f, 0f);
      image.raycastTarget = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = image;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => MapRequestScreenActions.Dispatch(action));

      rect = buttonObject.GetComponent<RectTransform>();
    }

    private static void AddLabel(GameObject parent, string text, Vector2 offsetMin, Vector2 offsetMax)
    {
      GameObject textObject = new("Label");
      textObject.transform.SetParent(parent.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = text;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = SelectedRowColour;  // cream on the art's own dark gold bars
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.resizeTextForBestFit = true;
      label.resizeTextMinSize = 8;
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = offsetMin;
      textRect.offsetMax = offsetMax;
    }

    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      // The mode's own picture, in the big window's rect the art's variants measure.
      Place(modeRect, new Vector2(width * ModeCentreX, height * ModeCentreY),
            new Vector2(width * ModeWidth, height * ModeHeight));

      // The selected map's stem, in the title strip along the list panel's top.
      if (titleText != null)
      {
        titleText.fontSize = Mathf.Max(10, Mathf.RoundToInt(height * 0.021f));
        titleText.resizeTextMaxSize = titleText.fontSize;
        Place(titleRect, new Vector2(width * TitleCentreX, height * TitleCentreY),
              new Vector2(width * TitleWidth, height * TitleHeight));
      }

      // The list, filling the leather panel's list area top to bottom - the
      // first row's top edge is the measured RowTopY and the pitch is the row
      // height, so the twelfth row ends clear of the painted medallions.
      for (int i = 0; i < rowRects.Length; i++)
      {
        if (rowRects[i] == null) continue;
        float topY = RowTopY - i * RowHeight;   // the row's top edge, up-positive
        float centreY = topY - RowHeight / 2f;
        Place(rowRects[i], new Vector2(width * RowCentreX, height * centreY),
              new Vector2(width * RowWidth, height * RowHeight));
        if (rowLabels[i] != null)
        {
          rowLabels[i].fontSize = Mathf.Max(8, Mathf.RoundToInt(height * 0.019f));
          rowLabels[i].resizeTextMaxSize = rowLabels[i].fontSize;
        }
      }

      // The Play row and the way back, in the free band between the medallions.
      Place(playRect, new Vector2(width * PlayCentreX, height * ButtonCentreY),
            new Vector2(width * PlayWidth, height * PlayHeight));
      Place(backRect, new Vector2(width * BackCentreX, height * ButtonCentreY),
            new Vector2(width * BackWidth, height * BackHeight));

      // The two painted arrows, over the art's own medallions.
      Place(previousRect, new Vector2(width * -ArrowCentreX, height * ArrowCentreY),
            new Vector2(width * ArrowWidth, height * ArrowHeight));
      Place(nextRect, new Vector2(width * ArrowCentreX, height * ArrowCentreY),
            new Vector2(width * ArrowWidth, height * ArrowHeight));
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
