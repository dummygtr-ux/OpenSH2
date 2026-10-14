using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The shipped play screen - PLAYSCREEN 'Choose Your Path' - which the main
  /// menu's Play button opens and whose own "Previous Menu" button brings the
  /// flow back.
  ///
  /// What it is made of is the install's, not this port's invention: the
  /// background is ui/choose_path/choose_path_background.jpg (1024x768, the
  /// two sides of the picture are the two paths), the two path buttons are
  /// ui/war_peace_menu/war_off.jpg + war_on.jpg and peace_off.jpg + peace_on.jpg
  /// (the folder that also holds the war/peace screens' own pictures), the
  /// tutorial is the medallion tutorial_button_off.tga + _on.tga, and every
  /// word on the screen comes from the shipped translation table through
  /// <see cref="PlayScreenActions"/> (the title 'Choose Your Path', the two
  /// path titles, 'Play the Tutorial', 'Previous Menu').
  ///
  /// Layout is this port's own: the shipped screen's coordinates are not in
  /// anything we can read, so the pictures are placed as the background's own
  /// composition suggests - the king (the gentler path) on the left, the
  /// armoured figure (war) on the right - with the title above both and the
  /// tutorial medallion centred at the bottom. Everything scales with the 4:3
  /// content rect, the same one the menu uses.
  /// </summary>
  class PlayScreenUI
  {
    public bool IsShown = false;
    private GameObject uiRoot;
    private RectTransform contentRoot;
    private RectTransform titleRect;
    private Text titleText;
    private AspectRatioBars aspectRatioBars;
    private readonly List<RectTransform> buttonRects = new();
    private readonly List<RectTransform> captionRects = new();

    // Fractions of the 4:3 content rect, so one set of numbers lays the screen
    // out at any Game view size (the menu's content root is the same rect).
    private const float PathButtonWidth = 0.300f;   // of the content width
    private const float PathButtonHeight = 0.450f;  // of the content height
    private const float PathButtonCentreX = 0.235f; // of the content width, each side
    private const float PathButtonCentreY = -0.030f; // of the content height
    private const float TutorialWidth = 0.120f;
    private const float TutorialHeight = 0.179f;
    private const float TutorialCentreY = -0.360f;
    private const float BackWidth = 0.220f;
    private const float BackHeight = 0.080f;
    private const float TitleCentreY = 0.400f;

    /// <summary>
    /// Creates the screen's UI. One root, its own canvas above the menu's
    /// (the menu's is 1100), a full-screen raycast-blocking background - the
    /// menu may still be in the scene for the frame the flow switches screens
    /// on, and a click must never land through this screen onto it - and its
    /// own aspect-ratio bars: this screen is 4:3 like the menu's, and the menu
    /// takes ITS bars down when it is hidden, so without its own the widescreen
    /// gutters would show whatever the scene camera renders (measured
    /// 2026-10-12: the first capture had the sample scene's skybox in them).
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      PlayScreenActions.ResetRecord();
      buttonRects.Clear();
      captionRects.Clear();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreateTitle();
      CreatePathButtons();
      CreateTutorialButton();
      CreateBackButton();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Play screen: shown ({PlayScreenActions.All.Length} actions: "
                + string.Join(", ", System.Array.ConvertAll(PlayScreenActions.All, info => info.Label))
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
        titleRect = null;
        titleText = null;
        buttonRects.Clear();
        captionRects.Clear();
      }

      IsShown = false;
    }

    /// <summary>
    /// The flow's own call: this screen is up exactly when the flow's state
    /// names it, so a tick that finds it should be up shows it (and lays it out),
    /// and a tick that does not hides it - which takes its aspect-ratio bars down
    /// with its root. The screens the play screen opens (the two path screens)
    /// use the same call, so exactly one frontend screen is ever on.
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
      uiRoot = new GameObject("PlayScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1200;  // above the main menu's 1100

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("PlayScreenContentRoot");
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
      GameObject backgroundObject = new("PlayScreenBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.ChoosePathBackgroundTexture);
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

    private void CreateTitle()
    {
      GameObject titleObject = new("PlayScreenTitle");
      titleObject.transform.SetParent(contentRoot, false);

      titleText = titleObject.AddComponent<Text>();
      // The shipped title of this screen (PLAYSCREEN_CHOOSE_YOUR_PATH), and the
      // one string on the screen that no button's row owns. The UAT reads the
      // same row out of the install and compares the two.
      titleText.text = ShippedTitleText;
      titleText.alignment = TextAnchor.MiddleCenter;
      titleText.color = new Color32(255, 244, 214, 255);
      titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      titleText.raycastTarget = false;

      titleRect = titleObject.GetComponent<RectTransform>();
      titleRect.anchorMin = new Vector2(0f, 0.5f);
      titleRect.anchorMax = new Vector2(1f, 0.5f);
      titleRect.pivot = new Vector2(0.5f, 0.5f);
      titleRect.sizeDelta = new Vector2(0f, 0f);
    }

    /// <summary>
    /// The screen's own title, as the shipped table has it: the row
    /// PLAYSCREEN_CHOOSE_YOUR_PATH, 'Choose Your Path'. Public because the UAT
    /// checks the rendered text against the same row read out of the install.
    /// </summary>
    public const string ShippedTitleId = "PLAYSCREEN_CHOOSE_YOUR_PATH";
    public const string ShippedTitleText = "Choose Your Path";

    private void CreatePathButtons()
    {
      CreatePictureButton("WarButton", PlayScreenAction.War,
                          UIAssets.WarButtonTexture, UIAssets.WarButtonHighlightTexture);
      CreatePictureButton("PeaceButton", PlayScreenAction.Peace,
                          UIAssets.PeaceButtonTexture, UIAssets.PeaceButtonHighlightTexture);
    }

    /// <summary>
    /// One of the screen's picture buttons: the shipped picture, its hover
    /// state from the same folder, its shipped label as a caption under it, and
    /// its click wired to the screen's ONE entry point
    /// (<see cref="PlayScreenActions.Dispatch"/>) with its own action captured.
    /// </summary>
    private void CreatePictureButton(string name, PlayScreenAction action,
                                     string texture, string highlightTexture)
    {
      PlayScreenActionInfo info = PlayScreenActions.Info(action);

      GameObject buttonObject = new(name);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => PlayScreenActions.Dispatch(info.Action));

      RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
      buttonRects.Add(buttonRect);

      HoverSpriteSwap hover = buttonObject.AddComponent<HoverSpriteSwap>();
      hover.SetSprites(buttonImage, SpriteLoader.LoadSprite(highlightTexture));

      CreateCaption(info.Label + "Caption", info.Label, captionRects);
    }

    /// <summary>
    /// The tutorial medallion: a round button with its own picture, labelled
    /// with its shipped text.
    /// </summary>
    private void CreateTutorialButton()
    {
      PlayScreenActionInfo info = PlayScreenActions.Info(PlayScreenAction.Tutorial);

      GameObject buttonObject = new("TutorialButton");
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(UIAssets.TutorialButtonTexture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => PlayScreenActions.Dispatch(info.Action));

      // The medallion is the one button that is not a picture in a frame but a
      // round icon, so its caption sits above it rather than under it.
      RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
      buttonRects.Add(buttonRect);
      CreateCaption(info.Label + "Caption", info.Label, captionRects);

      HoverSpriteSwap hover = buttonObject.AddComponent<HoverSpriteSwap>();
      hover.SetSprites(buttonImage, SpriteLoader.LoadSprite(UIAssets.TutorialButtonHighlightTexture));
    }

    /// <summary>
    /// The way back - the only action on this screen with a screen behind it.
    /// The shipped button art for it is the frontend's own fancy button (the
    /// table names the action, not its picture), which the menu already uses.
    /// </summary>
    private void CreateBackButton()
    {
      PlayScreenActionInfo info = PlayScreenActions.Info(PlayScreenAction.Back);

      GameObject buttonObject = new("BackButton");
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(UIAssets.FancyButtonTexture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => PlayScreenActions.Dispatch(info.Action));

      RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
      buttonRects.Add(buttonRect);

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = info.Label;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = new Color32(255, 244, 214, 255);
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = Vector2.zero;
      textRect.offsetMax = Vector2.zero;

      HoverSpriteSwap hover = buttonObject.AddComponent<HoverSpriteSwap>();
      hover.SetSprites(buttonImage, SpriteLoader.LoadSprite(UIAssets.FancyButtonHighlightTexture));
    }

    /// <summary>A shipped label under (or above) a picture button, tracked so UpdateLayout moves it.</summary>
    private void CreateCaption(string name, string text, List<RectTransform> tracked)
    {
      GameObject captionObject = new(name);
      captionObject.transform.SetParent(contentRoot, false);

      Text caption = captionObject.AddComponent<Text>();
      caption.text = text;
      caption.alignment = TextAnchor.MiddleCenter;
      caption.color = new Color32(255, 244, 214, 255);
      caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      caption.raycastTarget = false;

      RectTransform captionRect = captionObject.GetComponent<RectTransform>();
      captionRect.anchorMin = new Vector2(0.5f, 0.5f);
      captionRect.anchorMax = new Vector2(0.5f, 0.5f);
      captionRect.pivot = new Vector2(0.5f, 0.5f);
      captionRect.sizeDelta = new Vector2(0f, 0f);
      tracked.Add(captionRect);
    }

    /// <summary>
    /// Places every element from the content rect's size, in the order the
    /// buttons are created (war, peace, tutorial, back), so the caption list
    /// follows the button list.
    /// </summary>
    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      RectTransform war = buttonRects.Count > 0 ? buttonRects[0] : null;
      RectTransform peace = buttonRects.Count > 1 ? buttonRects[1] : null;
      RectTransform tutorial = buttonRects.Count > 2 ? buttonRects[2] : null;
      RectTransform back = buttonRects.Count > 3 ? buttonRects[3] : null;

      float pathWidth = width * PathButtonWidth;
      float pathHeight = height * PathButtonHeight;
      // The background puts the king (peace) on the left and the armoured
      // figure (war) on the right.
      Place(peace, new Vector2(-width * PathButtonCentreX, height * PathButtonCentreY),
            new Vector2(pathWidth, pathHeight));
      Place(war, new Vector2(width * PathButtonCentreX, height * PathButtonCentreY),
            new Vector2(pathWidth, pathHeight));

      float tutorialWidth = width * TutorialWidth;
      float tutorialHeight = height * TutorialHeight;
      Place(tutorial, new Vector2(0f, height * TutorialCentreY),
            new Vector2(tutorialWidth, tutorialHeight));

      Place(back, new Vector2(-width * (0.5f - 0.030f - BackWidth / 2f),
                              -height * (0.5f - 0.040f - BackHeight / 2f)),
            new Vector2(width * BackWidth, height * BackHeight));

      if (titleRect != null)
      {
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 0.5f);
        titleRect.sizeDelta = new Vector2(0f, height * 0.10f);
        titleRect.anchoredPosition = new Vector2(0f, height * TitleCentreY);
      }

      if (titleText != null)
      {
        titleText.fontSize = Mathf.Max(12, Mathf.RoundToInt(height / 24f));
      }

      // Captions: war and peace under their picture, the tutorial above its
      // medallion. The buttons were created in that order.
      float captionHeight = height * 0.045f;
      for (int i = 0; i < captionRects.Count && i < buttonRects.Count; i++)
      {
        RectTransform buttonRect = buttonRects[i];
        bool above = i == 2;  // the tutorial medallion's caption sits above it
        float offset = buttonRect.sizeDelta.y / 2f + captionHeight * 0.65f;
        Place(captionRects[i],
              buttonRect.anchoredPosition + new Vector2(0f, above ? offset : -offset),
              new Vector2(buttonRect.sizeDelta.x * 1.6f, captionHeight));
        Text caption = captionRects[i].GetComponent<Text>();
        if (caption != null) caption.fontSize = Mathf.Max(10, Mathf.RoundToInt(height / 40f));
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

    /// <summary>
    /// The shipped art's own hover convention: every picture button ships an
    /// "on" state next to its "off" one (war/peace/tutorial), so hovering swaps
    /// the sprite rather than tinting or overlaying anything.
    /// </summary>
    private class HoverSpriteSwap : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
      private Image target;
      private Sprite offSprite;
      private Sprite onSprite;

      public void SetSprites(Image image, Sprite on)
      {
        target = image;
        offSprite = image != null ? image.sprite : null;
        onSprite = on;
      }

      public void OnPointerEnter(PointerEventData eventData)
      {
        if (target != null && onSprite != null) target.sprite = onSprite;
      }

      public void OnPointerExit(PointerEventData eventData)
      {
        if (target != null && offSprite != null) target.sprite = offSprite;
      }
    }
  }
}
