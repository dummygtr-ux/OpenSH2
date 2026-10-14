using Assets.Code;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.Missions;
using Assets.Code.Stronghold2.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The in-game MAIN SCREEN (GameState.GAMEPLAY) - the game's own surface the
  /// mission's briefing hands the map to, and the first thing a mission shows
  /// once its briefing is done.
  ///
  /// It is made of the install's own art and the install's own rows, and nothing
  /// else: the status bar is the strip the install paints across the TOP 45
  /// rows of ui/ingameui.tga (1024x1024, measured 2026-10-13 - the one fully
  /// composed in-game surface piece the install ships, with the Date and
  /// Peasants insets, the stone Current Rank plaque, and the four right-hand
  /// insets whose medallions are a gold coin, a crown and a face), and the three
  /// values written into it are the loaded mission's own decoded numbers
  /// (S2MMissionState: the Scenario's starting year + month as the install's own
  /// MONTH_* row reads it, Mission.StartingGold, Mission.StartingPopularity).
  /// Its one control is the Objectives row (MAINSCREENROLLOVERS086), wearing the
  /// in-game menus' own small fancy button
  /// (ui/ingame_menus/button_fancy_small.tga) with the row's shipped text.
  ///
  /// WHAT IT DOES NOT DRAW, and why (the report says all of it):
  /// - Three of the bar's six fields (Current Rank, Honour, Peasants / Maximum
  ///   Peasants) are the RUNTIME's own state. The mission's data carries none of
  ///   them and this port runs no simulation, so those slots are left as the
  ///   install paints them - empty - rather than filled with invented numbers
  ///   (S2MMissionState.RuntimeFieldsNote).
  /// - The other six controls (Delete, Castle Views, Estate Ownership, Zoom,
  ///   Reports, Radar Map) are carried as rows and named, not drawn: the install
  ///   pins no art for them (the atlas's icon buttons carry no labels, so which
  ///   icon belongs to which row is NOT in the shipped data), so the port draws
  ///   no button that could claim a pairing the install does not make.
  ///
  /// The layout is the port's reading, marked: the bar spans the whole screen
  /// width at the strip's own 1024:45 aspect, anchored to the very top (the
  /// shipped game's own anchoring is not in its data - the atlas paints the
  /// strip and says nothing about where the screen puts it), the three values
  /// are centred in the painted red areas of their slots (right of each slot's
  /// medallion), the Objectives button hangs below the bar's left end, and the
  /// bar's seventh painted slot is named by no row in the install's table, so
  /// it stays empty. The slot attribution itself is
  /// S2MMissionState.SlotAttributionNote.
  /// </summary>
  class MainScreenUI
  {
    public bool IsShown { get; private set; }

    /// <summary>The loaded mission's own state, resolved when the screen was built.</summary>
    public S2MMissionState Mission { get; private set; }

    /// <summary>What the bar's Date slot carries ('Jan 1084'), or ''.</summary>
    public string DateLabelText { get; private set; } = "";

    /// <summary>What the bar's Gold slot carries ('1000'), or ''.</summary>
    public string GoldLabelText { get; private set; } = "";

    /// <summary>What the bar's Popularity slot carries ('90'), or ''.</summary>
    public string PopularityLabelText { get; private set; } = "";

    private GameObject uiRoot;
    private RectTransform barRect;
    private Image barImage;
    private Text dateLabel;
    private Text goldLabel;
    private Text popularityLabel;
    private RectTransform objectivesRect;

    // The install's own strip, in its measures (ui/ingameui.tga): the bar is its
    // top 45 rows of 1024, and its painted slots run, in x px of 1024 - Date
    // 11..137, Peasants 144..405, the stone Rank plaque 407..610, Gold 611..708
    // (coin medallion at 613..650), Honour 711..823 (crown), Popularity 824..937
    // (face) and a seventh slot 938..1013 no row names. The value text sits in
    // each slot's red area, right of its medallion.
    public const int StripWidthPx = 1024;
    public const int StripHeightPx = 45;
    public const int DateSlotLeftPx = 11, DateSlotRightPx = 137;
    public const int GoldSlotLeftPx = 611, GoldSlotRightPx = 708;
    public const int PopularitySlotLeftPx = 824, PopularitySlotRightPx = 937;
    private const float DateTextLeft = 16f, DateTextRight = 132f;
    private const float GoldTextLeft = 656f, GoldTextRight = 706f;
    private const float PopularityTextLeft = 876f, PopularityTextRight = 935f;

    // The Objectives button (ui/ingame_menus/button_fancy_small.tga, 192x46),
    // hung below the bar's left end at the bar's own scale - the port's reading
    // of where the shipped screen's controls stand (not in its data).
    private const float ButtonLeftPx = 10f, ButtonTopGapPx = 8f;
    private const float ButtonWidthPx = 192f, ButtonHeightPx = 46f;

    /// <summary>
    /// Creates the screen's UI for the mission the flow has loaded: one root,
    /// its own canvas, the install's own status bar with the mission's own
    /// three numbers in their slots, and the Objectives row.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      MainScreenActions.ResetRecord();

      Game game = Game.GetInstance();
      Mission = S2MMissionState.Resolve(
        game == null ? null : game.CurrentMapPath,
        S2MMapWorldLoader.Last?.MapFile,
        S2MTextTable.ForInstall(Constants.GamePath));

      DateLabelText = Mission.HasMission ? Mission.DateText : "";
      GoldLabelText = Mission.HasMission ? Mission.StartingGold.ToString() : "";
      PopularityLabelText = Mission.HasMission ? Mission.StartingPopularity.ToString() : "";

      CreateUiRoot();
      CreateBar();
      CreateValueLabels();
      CreateObjectivesButton();
      UpdateLayout();

      IsShown = true;
      Debug.Log($"Main screen: shown for '{Mission.MapStem}'"
                + (Mission.HasMission ? "" : " (its mission did not resolve)")
                + $"; date '{DateLabelText}', gold '{GoldLabelText}', "
                + $"popularity '{PopularityLabelText}'.");
      Debug.Log($"Main screen: {S2MMissionState.SlotAttributionNote}.");
      Debug.Log($"Main screen: {S2MMissionState.RuntimeFieldsNote}.");
      if (!string.IsNullOrEmpty(Mission.Note))
      {
        Debug.Log($"Main screen: {Mission.MapStem}: {Mission.Note}.");
      }
      foreach (MainScreenRow row in MainScreenActions.All)
      {
        Debug.Log($"Main screen: row {row.Id} '{row.Label}' -> {MainScreenActions.Outcome(row)}.");
      }
      Debug.Log($"Main screen: its one drawn control is '{MainScreenActions.Objectives.Label}' "
                + $"({MainScreenActions.ObjectivesButtonName}); the other "
                + $"{MainScreenActions.Controls.Length - 1} controls are named, not drawn - "
                + "the install pins no art for them.");
    }

    public void Hide()
    {
      if (uiRoot != null)
      {
        Object.Destroy(uiRoot);
        uiRoot = null;
        barRect = null;
        barImage = null;
        dateLabel = null;
        goldLabel = null;
        popularityLabel = null;
        objectivesRect = null;
      }

      IsShown = false;
    }

    /// <summary>
    /// The flow's own call: this screen is up exactly when the flow's state
    /// names it (GameState.GAMEPLAY), so a tick that finds it should be up shows
    /// it and lays it out, and a tick that does not hides it.
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
      uiRoot = new GameObject("MainScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1450;  // above every frontend screen (1400 and below), below the briefing's 1500

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// The install's own status bar: the strip across ui/ingameui.tga's top 45
    /// rows, drawn full-width at that strip's own aspect. The image does not
    /// swallow clicks - the world under the bar is the game's - so only the
    /// buttons are raycast targets.
    /// </summary>
    private void CreateBar()
    {
      GameObject barObject = new("MainScreenUIBar");
      barObject.transform.SetParent(uiRoot.transform, false);

      barImage = barObject.AddComponent<Image>();
      Sprite whole = SpriteLoader.LoadSprite(UIAssets.IngameUiTexture);
      if (whole != null && whole.texture != null)
      {
        // The strip is the atlas's TOP 45 rows: Unity's own bottom-up texture
        // space puts them at y = height - 45 (TGALoader writes the file's
        // bottom-origin rows straight into that space, so the image is upright).
        barImage.sprite = Sprite.Create(
          whole.texture,
          new Rect(0f, whole.texture.height - StripHeightPx, StripWidthPx, StripHeightPx),
          new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        barImage.sprite.name = "ingameui bar strip";
      }
      barImage.type = Image.Type.Simple;
      barImage.preserveAspect = false;
      barImage.raycastTarget = false;

      barRect = barObject.GetComponent<RectTransform>();
      barRect.anchorMin = new Vector2(0f, 1f);
      barRect.anchorMax = new Vector2(1f, 1f);
      barRect.pivot = new Vector2(0.5f, 1f);
      barRect.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// The mission's own three numbers, each written into its slot's red area
    /// (the part of the painted inset right of the slot's medallion). Each label
    /// is a CHILD OF THE BAR with fraction anchors, so a value sits in its
    /// painted slot at every resolution by construction - no screen-size
    /// arithmetic stands between the strip's own pixels and the label's rect.
    /// The other three slots show nothing because the install's mission data
    /// carries nothing for them - the report names that.
    /// </summary>
    private void CreateValueLabels()
    {
      dateLabel = CreateValueLabel("MainScreenUIDateLabel", MainScreenSlot.Date, DateTextLeft, DateTextRight);
      goldLabel = CreateValueLabel("MainScreenUIGoldLabel", MainScreenSlot.Gold, GoldTextLeft, GoldTextRight);
      popularityLabel = CreateValueLabel("MainScreenUIPopularityLabel", MainScreenSlot.Popularity,
                                          PopularityTextLeft, PopularityTextRight);
    }

    private Text CreateValueLabel(string name, MainScreenSlot slot, float leftPx, float rightPx)
    {
      GameObject labelObject = new(name);
      labelObject.transform.SetParent(barRect, false);

      // The Text component first: a fresh GameObject carries a plain Transform,
      // and it is adding the UI component that gives it the RectTransform the
      // anchors below need (GetComponent before that returns null).
      Text label = labelObject.AddComponent<Text>();

      RectTransform rect = labelObject.GetComponent<RectTransform>();
      rect.anchorMin = new Vector2(leftPx / StripWidthPx, 0f);
      rect.anchorMax = new Vector2(rightPx / StripWidthPx, 1f);
      rect.offsetMin = Vector2.zero;
      rect.offsetMax = Vector2.zero;

      label.text = slot switch
      {
        MainScreenSlot.Date => DateLabelText,
        MainScreenSlot.Gold => GoldLabelText,
        _ => PopularityLabelText,
      };
      label.alignment = TextAnchor.MiddleCenter;
      label.color = new Color32(255, 236, 196, 255);  // the frontend labels' parchment
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.raycastTarget = false;
      // A slot is a fixed painted inset: a wide number shrinks rather than
      // running over the bar's frame.
      label.resizeTextForBestFit = true;
      label.resizeTextMinSize = 8;
      return label;
    }

    /// <summary>
    /// The one control the port draws: the Objectives row, wearing the in-game
    /// menus' own small fancy button with the row's shipped text, wired to the
    /// screen's one entry point.
    /// </summary>
    private void CreateObjectivesButton()
    {
      GameObject buttonObject = new(MainScreenActions.ObjectivesButtonName);
      buttonObject.transform.SetParent(uiRoot.transform, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(UIAssets.IngameSmallButtonTexture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = false;  // the rect is the art's own 192:46 aspect

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => MainScreenActions.Dispatch(MainScreenActions.Objectives));

      // The button hangs off the BAR's own frame (its left end, its bottom
      // edge), so its place survives any resolution the same way the values'
      // slots do: the bar's rect is the strip's own coordinate space.
      objectivesRect = buttonObject.GetComponent<RectTransform>();
      objectivesRect.SetParent(barRect, false);
      objectivesRect.anchorMin = new Vector2(ButtonLeftPx / StripWidthPx, 0f);
      objectivesRect.anchorMax = new Vector2(ButtonLeftPx / StripWidthPx, 0f);
      objectivesRect.pivot = new Vector2(0f, 1f);

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = MainScreenActions.Objectives.Label;
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
      if (uiRoot == null || barRect == null) return;

      // The bar spans the screen width (its own anchors) at the strip's own
      // 1024:45 aspect, and everything else is measured off the bar's OWN laid
      // out rect - not off Screen or the canvas - so the painted slots, the
      // values in them and the button below them all scale together whatever
      // the canvas's own unit happens to be.
      float barWidthPx = barRect.rect.width;
      if (barWidthPx <= 1f) barWidthPx = Screen.width;
      float scale = barWidthPx / StripWidthPx;
      float barHeight = StripHeightPx * scale;
      barRect.sizeDelta = new Vector2(0f, barHeight);

      SetLabelFontSize(dateLabel, barHeight);
      SetLabelFontSize(goldLabel, barHeight);
      SetLabelFontSize(popularityLabel, barHeight);

      if (objectivesRect != null)
      {
        objectivesRect.anchoredPosition = new Vector2(0f, -ButtonTopGapPx * scale);
        objectivesRect.sizeDelta = new Vector2(ButtonWidthPx * scale, ButtonHeightPx * scale);
        Text label = objectivesRect.GetComponentInChildren<Text>();
        if (label != null) label.fontSize = Mathf.Max(10, Mathf.RoundToInt(ButtonHeightPx * scale * 0.42f));
      }
    }

    private static void SetLabelFontSize(Text label, float barHeight)
    {
      if (label != null) label.fontSize = Mathf.Max(10, Mathf.RoundToInt(barHeight * 0.46f));
    }
  }
}
