using System.Collections.Generic;
using Assets.Code;
using Assets.Code.Stronghold2.Missions;
using Assets.Code.Stronghold2.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The briefing screen (GameState.BRIEFING) - the mission-start screen the
  /// clicked chapter's map puts up once it stands, and the first screen of the
  /// GAME (the frontend is gone by then).
  ///
  /// It is made of the install's own per-chapter art and the install's own
  /// rows, and nothing else: the backdrop is the chapter's picture
  /// (ui/chapter_introductions/chapterNN_intro.jpg, 1024x768 - the same shape as
  /// the content rect), the title is the chapter's own heading
  /// (WAR_CAMPAIGN_HEADINGnn) drawn on the ribbon the picture paints EMPTY
  /// across its top (measured: the red band is y 16..126 of 768 in all twelve
  /// pictures), the three tabs are the shipped screen's own buttons with their
  /// shipped labels and off/on art, the line under them is the shipped
  /// hint_bar.tga, the two medallions that page the hints are the shipped
  /// hint_closed.tga, and the way on is the shipped 'To the Game' row drawn
  /// with the frontend's fancy button.
  ///
  /// WHAT IT SHOWS, and what it says it cannot (S2MChapterBriefing carries the
  /// measured evidence for both):
  /// - Objective (the shipped screen's first tab): the chapter's title, which is
  ///   the one per-chapter line the install ships. It ships NO objective prose
  ///   for the twelve war chapters (only the demo's DEMO_BRIEFING_1..3), so the
  ///   report names that gap instead of the screen inventing a line.
  /// - Hints and Tips: the chapter's hints one at a time on the hint bar, paged
  ///   by the two medallions - read from WARHINT&lt;chapter-1&gt;_0..7 BY THE
  ///   ROWS' OWN NAMES, which the install's data does not confirm (two measured
  ///   anchors disagree; the record and the report say so).
  /// - Tutorial: nothing is drawn; the install ships tutorial text only as the
  ///   tutorial's speech rows (Tutorial_1..86.wav), never per mission, and the
  ///   report names that too.
  ///
  /// The layout is the port's reading of the art, not a decoded composition: the
  /// picture fills the 4:3 rect, the title sits in the ribbon the art leaves for
  /// it, and the tab row, the hint bar and the play button share the picture's
  /// bottom band (below the character, where the art's own frame is quietest).
  /// FORMATS.md's i18n section marks that choice as the port's.
  ///
  /// The screen owns its own canvas (above every frontend screen's) and its own
  /// aspect-ratio bars, like the rest.
  ///
  /// IT HAS TWO SHOWINGS, and which one it is says which of the shipped screen's
  /// two play rows it wears: the START briefing (the one a loaded chapter's map
  /// puts up) wears 'To the Game' (MISSIONOBJECTIVESSCREENSTATICTOOLTIP_PLAY),
  /// and the IN-GAME REVIEW - which the main screen's Objectives control
  /// (MAINSCREENROLLOVERS086) opens, Game.RequestViewObjectives - wears 'Return
  /// to the Game' (_RETURN_TO_GAME). Everything else is the same screen: the
  /// same chapter content, the same three tabs, the same hints; both play rows
  /// leave through the same ask (Game.RequestLeaveBriefing), so the review is
  /// not a second way into the mission - it is the way back to it.
  /// </summary>
  class BriefingScreenUI
  {
    public bool IsShown { get; private set; }

    /// <summary>
    /// Which showing this is: false = the mission-start briefing, true = the
    /// in-game review the main screen's Objectives control opened. Read from the
    /// flow when the screen is built (Game.BriefingIsReview).
    /// </summary>
    public bool IsReview { get; private set; }

    /// <summary>The play row this showing wears ('To the Game', or 'Return to the Game').</summary>
    public BriefingAction LeaveRow { get; private set; }

    /// <summary>The chapter's briefing content, resolved when the screen was built.</summary>
    public S2MChapterBriefing Chapter { get; private set; }

    /// <summary>Which of the three shipped tabs is on (0 = Objective, the shipped screen's first).</summary>
    public int ActiveTabIndex { get; private set; }

    /// <summary>Which of the chapter's hints the bar carries (0 when it has none).</summary>
    public int HintIndex { get; private set; }

    /// <summary>The tab row that is on.</summary>
    public BriefingAction ActiveTab => BriefingScreenActions.Tabs[
      Mathf.Clamp(ActiveTabIndex, 0, BriefingScreenActions.Tabs.Length - 1)];

    /// <summary>The hint the bar carries right now, or null when the chapter has none.</summary>
    public S2MBriefingHint? ActiveHint =>
      Chapter != null && HintIndex >= 0 && HintIndex < Chapter.Hints.Count
        ? Chapter.Hints[HintIndex] : null;

    /// <summary>The text the shipped hint bar carries (what the UAT reads back).</summary>
    public string BarText { get; private set; } = "";

    /// <summary>The chapter's title as drawn on the picture's ribbon ('' when unshipped).</summary>
    public string TitleText { get; private set; } = "";

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private Text titleLabel;
    private Text barLabel;
    private RectTransform barRect;
    private RectTransform backPageRect;
    private RectTransform nextPageRect;
    private RectTransform leaveRect;
    private readonly List<RectTransform> tabRects = new();
    private readonly List<Image> tabImages = new();
    private readonly List<float> tabCentres = new();

    // The art's own geometry, in fractions of the 4:3 content rect the picture
    // fills (up-positive, centre origin). The content rect has 1024x768's own
    // shape, so a fraction of the art's width and a fraction of its height keep
    // every piece's aspect exactly.
    private const float TitleCentreY = 0.4076f;   // the painted ribbon's centre, 71 px of 768 from the top
    private const float TitleWidth = 0.8800f;     // the ribbon's inner span, 60..960 px of 1024
    private const float TitleHeight = 0.0900f;

    private const float BarCentreY = -0.3300f;
    private const float BarWidth = 0.8350f;       // hint_bar.tga, 855 px of 1024
    private const float BarHeight = 0.0560f;      // ...and 43 px of 768
    private const float PageWidth = 0.0322f;      // hint_closed.tga, 33 px of 1024
    private const float PageHeight = 0.0430f;     // ...and 33 px of 768
    private const float PageCentreX = 0.4475f;    // just outside the bar's own ends

    private const float ControlCentreY = -0.4450f; // the bottom band's control row, 726 px of 768
    private const float TabButtonHeight = 0.0750f; // 58 px of 768, the three tabs' common height
    private const float ObjectivesCentreX = -0.4539f;
    private const float ObjectivesWidth = 0.0470f; // 81x97 px of 1024x768 at that height
    private const float HintsCentreX = -0.3907f;
    private const float HintsWidth = 0.0483f;      // 85x99
    private const float TutorialCentreX = -0.3016f;
    private const float TutorialWidth = 0.0918f;   // 149x91

    private const float LeaveCentreX = 0.3000f;   // the control row's right-hand end, inside the frame
    private const float LeaveWidth = 0.2400f;
    private const float LeaveHeight = 0.0760f;

    /// <summary>
    /// Creates the screen's UI for the chapter the flow is playing: one root,
    /// its own canvas, the chapter's own picture (or, for a map that is not a
    /// campaign chapter, the install's own briefing backdrop), its title on the
    /// picture's ribbon, the shipped hint bar with the three tabs, the two
    /// medallions that page the hints, and 'To the Game'.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      BriefingScreenActions.ResetRecord();
      ActiveTabIndex = 0;  // the shipped screen's own first tab
      HintIndex = 0;
      tabRects.Clear();
      tabImages.Clear();
      tabCentres.Clear();

      Game game = Game.GetInstance();
      IsReview = game != null && game.BriefingIsReview;
      LeaveRow = BriefingScreenActions.LeaveFor(IsReview);
      Chapter = S2MChapterBriefing.Resolve(
        Constants.GamePath, game == null ? null : game.CurrentMapPath,
        S2MTextTable.ForInstall(Constants.GamePath));
      TitleText = Chapter.Title ?? "";

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackdrop();
      CreateTitle();
      CreateBar();
      CreatePageButtons();
      CreateTabButtons();
      CreateLeaveButton();
      UpdatePanel();   // which also lays the screen out

      IsShown = true;
      Debug.Log($"Briefing screen: shown for '{Chapter.MapStem}'"
                + (Chapter.HasChapter ? $" (chapter {Chapter.Chapter})" : "")
                + (IsReview ? " as the in-game review of the mission" : " as the mission-start briefing")
                + $"; title '{TitleText}', {Chapter.Hints.Count} hint(s) "
                + $"({Chapter.HintsFamily}), {BriefingScreenActions.All.Length} actions: "
                + string.Join(", ", System.Array.ConvertAll(
                    BriefingScreenActions.RowsFor(IsReview), row => row.Label))
                + ".");
      if (!string.IsNullOrEmpty(Chapter.Note))
      {
        Debug.Log($"Briefing screen: {Chapter.MapStem}: {Chapter.Note}.");
      }
      Debug.Log($"Briefing screen: hints: {S2MChapterBriefing.HintAttributionNote}.");
      Debug.Log($"Briefing screen: objective text: {S2MChapterBriefing.ObjectiveTextNote}.");
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
        titleLabel = null;
        barLabel = null;
        barRect = null;
        backPageRect = null;
        nextPageRect = null;
        leaveRect = null;
        tabRects.Clear();
        tabImages.Clear();
        tabCentres.Clear();
      }

      IsShown = false;
    }

    /// <summary>
    /// The flow's own call: this screen is up exactly when the flow's state
    /// names it (GameState.BRIEFING), so a tick that finds it should be up shows
    /// it and lays it out, and a tick that does not hides it - which takes its
    /// aspect-ratio bars down with its root.
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
    /// What the screen's own one entry point does with a click that stays on
    /// this screen: a tab switches the panel, a page walks the chapter's hints.
    /// The record and the log line are BriefingScreenActions' business; this is
    /// only the screen's own state and its redraw.
    /// </summary>
    public void Apply(BriefingAction action)
    {
      if (action.Kind == BriefingActionKind.Tab)
      {
        int index = System.Array.IndexOf(BriefingScreenActions.Tabs, action);
        if (index >= 0) ActiveTabIndex = index;
      }
      else if (action.Kind == BriefingActionKind.PageBack)
      {
        HintIndex = Mathf.Max(0, HintIndex - 1);
      }
      else if (action.Kind == BriefingActionKind.PageNext)
      {
        int last = Mathf.Max(0, (Chapter?.Hints.Count ?? 1) - 1);
        HintIndex = Mathf.Min(last, HintIndex + 1);
      }

      UpdatePanel();
    }

    /// <summary>
    /// The panel's own content, from the tab that is on: the Objective tab draws
    /// the chapter's title (the install's one per-chapter line), the Hints tab
    /// draws one of the chapter's shipped hints on the hint bar, and the
    /// Tutorial tab draws nothing (the install ships no per-mission tutorial
    /// text - the report names it). The three tab buttons wear their own off/on
    /// art, so which one is on is the art's answer, not a tint.
    /// </summary>
    private void UpdatePanel()
    {
      for (int i = 0; i < tabImages.Count && i < BriefingScreenActions.Tabs.Length; i++)
      {
        BriefingAction tab = BriefingScreenActions.Tabs[i];
        tabImages[i].sprite = SpriteLoader.LoadSprite(
          i == ActiveTabIndex ? tab.HoverTexture : tab.Texture);
      }

      switch (ActiveTabIndex)
      {
        case 1:  // Hints and Tips - the chapter's own hints, one to a page
          BarText = ActiveHint?.Text ?? "";
          break;
        case 2:  // Tutorial - the install ships no per-mission tutorial text
          BarText = "";
          break;
        default:  // Objective - the chapter's title, the install's one per-chapter line
          BarText = TitleText;
          break;
      }

      // The medallions page the hints, so they are only on the screen where
      // there are hints to page.
      bool paging = ActiveTabIndex == 1 && (Chapter?.Hints.Count ?? 0) > 1;
      SetActive(backPageRect, paging);
      SetActive(nextPageRect, paging);

      if (barLabel != null) barLabel.text = BarText;

      UpdateLayout();
    }

    private static void SetActive(RectTransform rect, bool active)
    {
      if (rect != null && rect.gameObject.activeSelf != active)
      {
        rect.gameObject.SetActive(active);
      }
    }

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("BriefingScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1500;  // above the campaign screen's 1400: the game's own screen

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("BriefingScreenUIContentRoot");
      contentObject.transform.SetParent(uiRoot.transform, false);
      contentRoot = contentObject.AddComponent<RectTransform>();
      contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
      contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
      contentRoot.pivot = new Vector2(0.5f, 0.5f);
      contentRoot.sizeDelta = new Vector2(Screen.height * (4f / 3f), Screen.height);
      contentRoot.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// The chapter's own picture fills the 4:3 content rect (1024x768 is exactly
    /// its shape, so this distorts nothing). A map that is not a campaign
    /// chapter has no picture of its own; rather than leave the screen bare it
    /// wears the install's own briefing backdrop, which the screen's own folder
    /// ships for exactly this screen.
    /// </summary>
    private void CreateBackdrop()
    {
      GameObject backdropObject = new("BriefingScreenUIBackdrop");
      backdropObject.transform.SetParent(contentRoot, false);

      Image backdropImage = backdropObject.AddComponent<Image>();
      backdropImage.sprite = SpriteLoader.LoadSprite(
        string.IsNullOrEmpty(Chapter.PicturePath) ? UIAssets.BriefingBackgroundTexture
                                                  : Chapter.PicturePath);
      backdropImage.type = Image.Type.Simple;
      backdropImage.preserveAspect = false;
      backdropImage.raycastTarget = true;  // nothing behind this screen may be clicked

      RectTransform backdropRect = backdropObject.GetComponent<RectTransform>();
      backdropRect.anchorMin = Vector2.zero;
      backdropRect.anchorMax = Vector2.one;
      backdropRect.offsetMin = Vector2.zero;
      backdropRect.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// The chapter's title, on the ribbon the picture paints for it. It is drawn
    /// in the parchment colour the frontend's own labels use, which reads on the
    /// band's deep red.
    /// </summary>
    private void CreateTitle()
    {
      GameObject titleObject = new("BriefingScreenUITitle");
      titleObject.transform.SetParent(contentRoot, false);

      titleLabel = titleObject.AddComponent<Text>();
      titleLabel.text = TitleText;
      titleLabel.alignment = TextAnchor.MiddleCenter;
      titleLabel.color = new Color32(255, 236, 196, 255);
      titleLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      titleLabel.raycastTarget = false;
      // A title is one shipped line and the ribbon is a fixed band: the longest
      // of the twelve ('Olaf Grimtooth, Your Time Has Come') has to fit inside
      // it, so the label shrinks itself rather than running over the picture.
      titleLabel.resizeTextForBestFit = true;
      titleLabel.resizeTextMinSize = 8;
    }

    /// <summary>The shipped hint bar: the line the active tab's text is written on.</summary>
    private void CreateBar()
    {
      GameObject barObject = new("BriefingScreenUIHintBar");
      barObject.transform.SetParent(contentRoot, false);

      Image barImage = barObject.AddComponent<Image>();
      barImage.sprite = SpriteLoader.LoadSprite(UIAssets.BriefingHintBarTexture);
      barImage.type = Image.Type.Simple;
      barImage.preserveAspect = false;  // the bar fills its rect at the art's own size
      barImage.raycastTarget = false;

      barRect = barObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(barObject.transform, false);
      barLabel = textObject.AddComponent<Text>();
      barLabel.text = "";
      barLabel.alignment = TextAnchor.MiddleCenter;
      barLabel.color = new Color32(255, 244, 214, 255);
      barLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      barLabel.raycastTarget = false;
      // The shipped hints are whole sentences and the bar is one line at a fixed
      // size: the text fits the bar rather than running past its frame.
      barLabel.resizeTextForBestFit = true;
      barLabel.resizeTextMinSize = 8;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = new Vector2(10f, 0f);
      textRect.offsetMax = new Vector2(-10f, 0f);
    }

    /// <summary>
    /// The two medallions that page the chapter's hints, either side of the bar.
    /// The install ships one rolled-scroll medallion (hint_closed) beside its
    /// open twin and a highlight, and names them by the hint bar they belong to;
    /// the port's reading is that they are this screen's paging controls, so both
    /// pages wear the rolled one. They have no row of their own in the table, so
    /// their ids are empty and the log says so.
    /// </summary>
    private void CreatePageButtons()
    {
      backPageRect = CreatePageButton(BriefingScreenActions.PageBack, -PageCentreX);
      nextPageRect = CreatePageButton(BriefingScreenActions.PageNext, PageCentreX);
    }

    private RectTransform CreatePageButton(BriefingAction action, float centreFractionX)
    {
      GameObject buttonObject = new(action.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(action.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => BriefingScreenActions.Dispatch(action));

      RectTransform rect = buttonObject.GetComponent<RectTransform>();
      rect.anchoredPosition = new Vector2(0f, BarCentreY);
      rect.sizeDelta = new Vector2(PageWidth, PageHeight);
      pageCentreFractions.Add(centreFractionX);
      pageRects.Add(rect);
      return rect;
    }

    private readonly List<RectTransform> pageRects = new();
    private readonly List<float> pageCentreFractions = new();

    /// <summary>The three shipped tabs, in their own order, each wearing its own picture.</summary>
    private void CreateTabButtons()
    {
      float[] centres = { ObjectivesCentreX, HintsCentreX, TutorialCentreX };
      float[] widths = { ObjectivesWidth, HintsWidth, TutorialWidth };

      for (int i = 0; i < BriefingScreenActions.Tabs.Length; i++)
      {
        BriefingAction tab = BriefingScreenActions.Tabs[i];
        GameObject buttonObject = new(tab.ButtonName);
        buttonObject.transform.SetParent(contentRoot, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = SpriteLoader.LoadSprite(tab.Texture);
        buttonImage.type = Image.Type.Simple;
        buttonImage.preserveAspect = false;  // the rect is the art's own aspect

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => BriefingScreenActions.Dispatch(tab));

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, ControlCentreY);
        rect.sizeDelta = new Vector2(widths[i], TabButtonHeight);

        tabRects.Add(rect);
        tabImages.Add(buttonImage);
        tabCentres.Add(centres[i]);

        GameObject textObject = new("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        label.text = tab.Label;
        label.alignment = TextAnchor.LowerCenter;
        label.color = new Color32(255, 244, 214, 255);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        // The tab's caption sits ABOVE its icon: below it would be off the
        // picture's bottom edge on a 16:9 screen, where the 4:3 rect runs out.
        textRect.anchorMin = new Vector2(0f, 1.06f);
        textRect.anchorMax = new Vector2(1f, 1.36f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
      }
    }

    /// <summary>
    /// The one row that leaves the screen - 'To the Game' at mission start,
    /// 'Return to the Game' in the in-game review - drawn with the frontend's
    /// fancy button and labelled with the shipped row's own words.
    /// </summary>
    private void CreateLeaveButton()
    {
      GameObject buttonObject = new(LeaveRow.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(LeaveRow.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => BriefingScreenActions.Dispatch(LeaveRow));

      leaveRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = LeaveRow.Label;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = new Color32(255, 244, 214, 255);
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = Vector2.zero;
      textRect.offsetMax = Vector2.zero;
    }

    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      Place(titleLabel == null ? null : titleLabel.GetComponent<RectTransform>(),
            new Vector2(0f, height * TitleCentreY),
            new Vector2(width * TitleWidth, height * TitleHeight));
      if (titleLabel != null) titleLabel.fontSize = Mathf.Max(12, Mathf.RoundToInt(height * 0.048f));

      Place(barRect, new Vector2(0f, height * BarCentreY),
            new Vector2(width * BarWidth, height * BarHeight));
      if (barLabel != null) barLabel.fontSize = Mathf.Max(10, Mathf.RoundToInt(height * 0.028f));

      for (int i = 0; i < pageRects.Count && i < pageCentreFractions.Count; i++)
      {
        Place(pageRects[i], new Vector2(width * pageCentreFractions[i], height * BarCentreY),
              new Vector2(width * PageWidth, height * PageHeight));
      }

      for (int i = 0; i < tabRects.Count; i++)
      {
        float centreX = i < tabCentres.Count ? tabCentres[i] : 0f;
        Place(tabRects[i], new Vector2(width * centreX, height * ControlCentreY),
              new Vector2(width * tabWidthFractions[i], height * TabButtonHeight));
        Text label = tabRects[i].GetComponentInChildren<Text>();
        if (label != null) label.fontSize = Mathf.Max(9, Mathf.RoundToInt(height * 0.022f));
      }

      Place(leaveRect, new Vector2(width * LeaveCentreX, height * ControlCentreY),
            new Vector2(width * LeaveWidth, height * LeaveHeight));
      if (leaveRect != null)
      {
        Text label = leaveRect.GetComponentInChildren<Text>();
        if (label != null) label.fontSize = Mathf.Max(12, Mathf.RoundToInt(height / 32f));
      }
    }

    private static readonly float[] tabWidthFractions =
      { ObjectivesWidth, HintsWidth, TutorialWidth };

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
