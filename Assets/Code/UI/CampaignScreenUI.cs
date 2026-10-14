using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The campaign screen (GameState.CAMPAIGN_SCREEN) - the 'Lost King'
  /// campaign's chapter list, which the war screen's Campaign row opens.
  ///
  /// What it is made of is the install's, not this port's invention: the
  /// backdrop is the install's own screen art (ui/campaign_screen/
  /// campaign_chapter1..12.jpg - one 1024x768 picture per chapter, the same
  /// desk, map and parchment scroll column with the campaign's route drawn a
  /// little further along in each), every one of the twelve chapter buttons is
  /// the install's own button (campaign_button.bmp) labelled with the install's
  /// own chapter text (WARCAMPAIGN_MISSION1..12 'Chapter 1'..'Chapter 12'), and
  /// the way back is the frontend's own fancy button labelled from the screen's
  /// own row (CAMPAIGNSCREENSTATICTOOLTIP_EXIT 'Previous Menu').
  ///
  /// The screen shows chapter 1's picture. The port has no save/progress data,
  /// so it opens where the shipped campaign starts; the install's other eleven
  /// pictures and its six progress markers
  /// (symbol_{good,bad,red}_{complete,current}.tga) are progressive-state art
  /// this port cannot fill in yet, and it draws none of them rather than
  /// inventing completion state.
  ///
  /// There is no title: the shipped table names a title row for the play screen
  /// (PLAYSCREEN_CHOOSE_YOUR_PATH) and none for this one, so this port invents
  /// none.
  ///
  /// Layout is the install's own: every chapter button stands exactly on one of
  /// the twelve parchment slots the art paints down its right-hand scroll column
  /// (measured off the art - the slots are 215x43 px of a 1024x768 picture, six,
  /// then three, then three, at the pitch below), and the way back sits in the
  /// description bar the art ships empty (the same place the two path screens'
  /// way back takes in the shared backdrop). The button art is 251x44, so a
  /// button fills its slot at 0.86x horizontally; the alternative - the art's
  /// own size - would overhang the painted slots by 18 px a side.
  ///
  /// The screen owns its own canvas above the war screen's and its own
  /// aspect-ratio bars, like every frontend screen here.
  /// </summary>
  class CampaignScreenUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private RectTransform backRect;
    private readonly List<RectTransform> chapterRects = new();
    private readonly List<Text> chapterLabels = new();

    // The art's own scroll column, in fractions of the 4:3 content rect
    // (up-positive, centre origin) - measured from the install's
    // campaign_chapter1.jpg (1024x768; the column is identical in all twelve).
    private const float SlotCentreX = 0.3545f;   // the painted slots' centre, 875 px of 1024
    private const float SlotWidth = 0.2100f;     // 215 px of 1024
    private const float SlotHeight = 0.0560f;    // 43 px of 768
    /// <summary>The twelve painted slots' centre-y, top to bottom (6, then 3, then 3).</summary>
    private static readonly float[] SlotCentreYs =
    {
       0.4277f,  0.3633f,  0.2995f,  0.2344f,  0.1680f,  0.1029f,
      -0.0260f, -0.0924f, -0.1562f,
      -0.2878f, -0.3529f, -0.4154f,
    };

    // The art's own description bar (the gold-framed panel at the bottom left,
    // 186..521 x 661..746 of the 1024x768 picture), where the way back sits.
    private const float BackCentreX = -0.1548f;
    private const float BackCentreY = -0.4160f;
    private const float BackWidth = 0.2400f;
    private const float BackHeight = 0.0760f;

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the war screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, the twelve
    /// chapter buttons on the art's own slots, and the way back.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      CampaignScreenActions.ResetRecord();
      chapterRects.Clear();
      chapterLabels.Clear();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreateChapterButtons();
      CreateBackButton();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Campaign screen: shown ({CampaignScreenActions.All.Length} actions: "
                + string.Join(", ", System.Array.ConvertAll(
                    CampaignScreenActions.All, row => row.Label))
                + ").");
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
        backRect = null;
        chapterRects.Clear();
        chapterLabels.Clear();
      }

      IsShown = false;
    }

    /// <summary>
    /// The flow's own call: this screen is up exactly when the flow's state
    /// names it, so a tick that finds it should be up shows it (and lays it out),
    /// and a tick that does not hides it - which takes its aspect-ratio bars down
    /// with its root.
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
      uiRoot = new GameObject("CampaignScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;  // above the war screen's 1300

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("CampaignScreenUI" + "ContentRoot");
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
      GameObject backgroundObject = new("CampaignScreenUIBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.CampaignBackgroundTexture);
      // The shipped art is 1024x768 - the content rect's own 4:3 - so filling
      // the rect is filling it without distortion.
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
    /// One button per chapter, in the chapters' own order, each on its own slot
    /// of the art's scroll column and labelled with its shipped text inside the
    /// button (the shipped button is a blank parchment bar, so the text has to
    /// live somewhere on this port's screen), its click wired to this screen's
    /// ONE entry point with its own row captured.
    /// </summary>
    private void CreateChapterButtons()
    {
      foreach (CampaignScreenRow row in CampaignScreenActions.Chapters)
      {
        GameObject buttonObject = new(row.ButtonName);
        buttonObject.transform.SetParent(contentRoot, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = SpriteLoader.LoadSprite(row.Texture);
        buttonImage.type = Image.Type.Simple;
        buttonImage.preserveAspect = false;  // the button fills the art's own slot

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => CampaignScreenActions.Dispatch(row));

        chapterRects.Add(buttonObject.GetComponent<RectTransform>());

        GameObject textObject = new("Label");
        textObject.transform.SetParent(buttonObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        label.text = row.Label;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color32(60, 44, 20, 255);  // dark ink on the parchment bar
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.raycastTarget = false;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        chapterLabels.Add(label);
      }
    }

    /// <summary>
    /// The way back - the only row on this screen with a screen behind it
    /// (the war screen it was opened from). Its picture is the frontend's own
    /// fancy button, which the menu, the play screen's way back and the two path
    /// screens' ways back use.
    /// </summary>
    private void CreateBackButton()
    {
      GameObject buttonObject = new(CampaignScreenActions.Back.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(CampaignScreenActions.Back.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => CampaignScreenActions.Dispatch(CampaignScreenActions.Back));

      backRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = CampaignScreenActions.Back.Label;
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

      float slotWidth = width * SlotWidth;
      float slotHeight = height * SlotHeight;
      int labelFontSize = Mathf.Max(10, Mathf.RoundToInt(slotHeight * 0.55f));

      // The chapter buttons, one per painted slot, top to bottom.
      for (int i = 0; i < chapterRects.Count; i++)
      {
        float centreY = (i < SlotCentreYs.Length ? SlotCentreYs[i]
                                                 : SlotCentreYs[SlotCentreYs.Length - 1]) * height;
        Place(chapterRects[i], new Vector2(width * SlotCentreX, centreY),
              new Vector2(slotWidth, slotHeight));
        if (i < chapterLabels.Count && chapterLabels[i] != null)
        {
          chapterLabels[i].fontSize = labelFontSize;
        }
      }

      // The way back, centred in the art's own empty description bar.
      Place(backRect, new Vector2(width * BackCentreX, height * BackCentreY),
            new Vector2(width * BackWidth, height * BackHeight));

      if (backRect != null)
      {
        Text label = backRect.GetComponentInChildren<Text>();
        if (label != null) label.fontSize = Mathf.Max(12, Mathf.RoundToInt(height / 32f));
      }
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
