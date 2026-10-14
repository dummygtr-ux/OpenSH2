using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The Conquest Trail screen (GameState.CONQUEST_TRAIL_SCREEN) - the trail's
  /// castle map, which the war screen's WARSCREEN_CONQUEST row opens.
  ///
  /// What it is made of is the install's, not this port's invention. The backdrop
  /// is the Gold Edition's own composed picture of the whole screen
  /// (ui/gold_edition/conquest_trail.png, 1024x768): the framed map of Europe
  /// with the trail's route and its ten castles painted on it, the two paging
  /// tiles at the map's bottom right, and the empty gold-framed panel along the
  /// bottom. Over the map's central rect the screen draws the trail's OWN
  /// progress picture for the position it is showing
  /// (war_conquest_arrows01..11.jpg, one per step - arrows01 has only Guadamur
  /// lit, arrows11 the whole route), at the rect the shipped screen draws it in:
  /// measured by subimage match against the backdrop, the 558x357 pictures stand
  /// at x 209, y 114 of the 1024x768 screen. The trail's twelve rows stand one
  /// step past the eleven pictures only at the end: the twelfth row is the
  /// campaign's completion message, and it stands on the finished trail, which is
  /// arrows11's picture (UIAssets.ConquestTrailOverlayTexture).
  ///
  /// The panel carries the selected position's own shipped row (CONQUESTTRAIL_N)
  /// - the install's text, read from the screen's own table, in the install's own
  /// empty panel (measured: the panel's inner rect is x 174..839, y 588..709 of
  /// the picture). The two paging buttons are the install's own arrow art
  /// (conquest_trail_arrowback/forward.tga, 52x52) drawn on the very tiles the
  /// backdrop paints (measured: the art stands at x 732 and x 782, y 507), so
  /// they stand on the shipped screen's own controls rather than beside them.
  ///
  /// Two things the install does not pin, and this port says so rather than
  /// pretending otherwise: the START button's picture and the two buttons' place
  /// in the panel. The trail's folder ships no button art at all (only the map,
  /// its eleven progress pictures and the two paging tiles), so the start row
  /// wears the install's own castle-scenario play button - the one its castle art
  /// ships beside in ui/siege_menu/ - and both the start row and the way back
  /// sit in the panel's right-hand side, which the art leaves empty: the trail's
  /// text takes the panel's left, the start row stands at its right, and the way
  /// back below it. The trail's completion art
  /// (ui/gold_edition/conquest_trail_winscreen.png, a crown on a purple field) is
  /// NOT drawn - it is progressive-state art and this port has no progress data
  /// (named on the report, like the campaign screen's six markers).
  ///
  /// The screen owns its own canvas above the war screen's and its own
  /// aspect-ratio bars, like every frontend screen here.
  /// </summary>
  class ConquestScreenUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private Image overlayImage;
    private RectTransform overlayRect;
    private Text trailText;
    private Text startLabel;
    private RectTransform previousRect;
    private RectTransform nextRect;
    private RectTransform startRect;
    private RectTransform backRect;

    // The map's own central rect, in fractions of the 4:3 content rect
    // (up-positive, centre origin - the campaign screen's own convention, so a
    // measured pixel px of the 1024-wide art is px/1024 - 0.5) - measured from
    // the install's conquest_trail.png by subimage match against
    // war_conquest_arrows11.jpg: the progress picture stands at x 209..766,
    // y 114..470 of the 1024x768 art.
    private const float OverlayCentreX = -0.023437f;  // (209 + 558/2) px of 1024, from the centre
    private const float OverlayCentreY = 0.119141f;   // 0.5 - (114 + 357/2) / 768
    private const float OverlayWidth = 0.544922f;     // 558 px of 1024
    private const float OverlayHeight = 0.464844f;    // 357 px of 768

    // The art's own gold-framed panel along the bottom - the one place the
    // screen's text can go. Measured: its inner rect is x 174..839, y 588..709
    // of the 1024x768 picture, between the gold lines the art paints at
    // x 171..173 / 840..842 and y 585..587 / 710..712.
    private const float PanelCentreX = -0.005371f;    // (174 + 839) / 2 px of 1024, from the centre
    private const float PanelCentreY = -0.344401f;    // 0.5 - (588 + 709) / 2 / 768
    private const float PanelWidth = 0.650391f;       // 666 px of 1024
    private const float PanelHeight = 0.158854f;      // 122 px of 768

    // The trail's own text: the panel's left-hand side, with a margin off the
    // art's frame, ending where the two buttons begin.
    private const float TextCentreX = -0.135742f;     // (186 + 560) / 2 px of 1024, from the centre
    private const float TextWidth = 0.365234f;        // 374 px of 1024
    private const float TextHeight = 0.119792f;       // 92 px of 768

    // The two buttons the install pins no art or place for (see the class note):
    // the install's own castle-scenario play button (266x57) at the panel's right
    // end, and the frontend's own way-back banner (drawn 266x47) below it.
    private const float ButtonCentreX = 0.188477f;    // (572 + 838) / 2 px of 1024, from the centre
    private const float StartCentreY = -0.310547f;    // 0.5 - (588 + 6 + 57/2) / 768
    private const float StartWidth = 0.259766f;       // 266 px of 1024
    private const float StartHeight = 0.074219f;      // 57 px of 768
    private const float BackCentreY = -0.390104f;     // 0.5 - (660 + 47/2) / 768
    private const float BackHeight = 0.061198f;       // 47 px of 768

    // The two paging buttons, on the tiles the backdrop itself paints: the
    // install's own 52x52 arrow art stands at x 732 and x 782, y 507.
    private const float PreviousCentreX = 0.240234f;  // (732 + 26) px of 1024, from the centre
    private const float NextCentreX = 0.289063f;      // (782 + 26) px of 1024, from the centre
    private const float ArrowCentreY = -0.194010f;    // 0.5 - (507 + 26) / 768
    private const float ArrowWidth = 0.050781f;       // 52 px of 1024
    private const float ArrowHeight = 0.067708f;      // 52 px of 768

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the war screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, the install's own
    /// backdrop, the trail's own progress picture over the map's central rect,
    /// the selected row's text in the panel, the two paging buttons on the tiles
    /// the art paints, the start row and the way back.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it
      // (and the trail opens at its first castle again).
      ConquestScreenActions.ResetRecord();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreateOverlay();
      CreateTrailText();
      CreateArrows();
      CreateStartButton();
      CreateBackButton();
      Refresh();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Conquest Trail screen: shown ({ConquestScreenActions.TrailCount} rows; position "
                + $"{ConquestScreenActions.Position} - {ConquestScreenActions.Selected.Id} "
                + $"'{Shorten(ConquestScreenActions.Selected.Text)}'; paging: "
                + $"{ConquestScreenActions.PagingNote(ConquestScreenActions.Position)}).");
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
        overlayImage = null;
        trailText = null;
        startLabel = null;
        previousRect = null;
        nextRect = null;
        startRect = null;
        backRect = null;
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

    /// <summary>
    /// The screen's own refresh, after a paging click moved the trail
    /// (ConquestScreenActions.Dispatch): the position's own progress picture over
    /// the map, the position's own shipped text in the panel, and the start row
    /// the position wears (position 12's is the shipped restart row). Called by
    /// the entry point, never by a click directly, so the trail's own record and
    /// what is on the screen cannot disagree.
    /// </summary>
    public void Refresh()
    {
      int position = ConquestScreenActions.Position;
      ConquestTrailRow row = ConquestScreenActions.Selected;

      if (overlayImage != null)
      {
        overlayImage.sprite = SpriteLoader.LoadSprite(UIAssets.ConquestTrailOverlayTexture(position));
      }

      if (trailText != null)
      {
        trailText.text = row.Text;
      }

      if (startLabel != null)
      {
        // The start row's own shipped text: 'Start Mission' where a castle
        // waits, and the shipped restart question at the trail's end.
        startLabel.text = ConquestScreenActions.StartRowFor(position).Label;
      }

      Debug.Log($"Conquest Trail: position {position}/{ConquestScreenActions.TrailCount} - {row.Id} "
                + (row.NamesACastle
                   ? $"({row.Castle}; {row.MapPath})"
                   : "(the trail's own completion row)"));
    }

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("ConquestScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;  // the campaign screen's own order - both are branches of the war screen's

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("ConquestScreenUIContentRoot");
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
      GameObject backgroundObject = new("ConquestScreenUIBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.ConquestTrailBackgroundTexture);
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
    /// The trail's own progress picture, over the map's central rect. The screen
    /// opens at the trail's first castle and Refresh swaps the picture as the
    /// paging buttons move it.
    /// </summary>
    private void CreateOverlay()
    {
      GameObject overlayObject = new("ConquestScreenUIOverlay");
      overlayObject.transform.SetParent(contentRoot, false);

      overlayImage = overlayObject.AddComponent<Image>();
      overlayImage.sprite = SpriteLoader.LoadSprite(
        UIAssets.ConquestTrailOverlayTexture(ConquestScreenActions.Position));
      overlayImage.type = Image.Type.Simple;
      overlayImage.preserveAspect = false;  // the picture is the rect's own size
      overlayImage.raycastTarget = false;   // the map itself is not a control

      overlayRect = overlayObject.GetComponent<RectTransform>();
    }

    /// <summary>The panel's own line: the selected position's shipped row, as the install writes it.</summary>
    private void CreateTrailText()
    {
      GameObject textObject = new("ConquestScreenUIText");
      textObject.transform.SetParent(contentRoot, false);

      trailText = textObject.AddComponent<Text>();
      trailText.text = ConquestScreenActions.Selected.Text;
      trailText.alignment = TextAnchor.MiddleCenter;
      trailText.color = new Color32(255, 244, 214, 255);  // the frontend's own cream, on the panel's leather
      trailText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      trailText.horizontalOverflow = HorizontalWrapMode.Wrap;
      trailText.verticalOverflow = VerticalWrapMode.Overflow;
      trailText.raycastTarget = false;
    }

    /// <summary>
    /// The two paging buttons: the install's own arrow tiles, standing exactly on
    /// the tiles the backdrop paints (measured from the art - see the class note).
    /// Their click is the screen's own entry point with their own row; a click at
    /// the trail's end names the install's disabled wording and moves nothing.
    /// </summary>
    private void CreateArrows()
    {
      previousRect = CreateArrow(ConquestScreenActions.PreviousCastle);
      nextRect = CreateArrow(ConquestScreenActions.NextCastle);
    }

    private RectTransform CreateArrow(ConquestScreenAction row)
    {
      GameObject buttonObject = new(row.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(row.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = false;  // the art is the 52x52 tile, drawn at its own size

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => ConquestScreenActions.Dispatch(row));

      return buttonObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// The start row - the one click that would take the trail's mission. Its
    /// own row is resolved at CLICK time, because the trail wears two of them:
    /// the shipped CONQUEST START MISSION where a castle waits, and the shipped
    /// CONQUEST START AGAIN at the trail's end. Its picture is the install's own
    /// castle-scenario play button (the trail's folder ships no button art - see
    /// the class note).
    /// </summary>
    private void CreateStartButton()
    {
      GameObject buttonObject = new(ConquestScreenActions.StartMission.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(UIAssets.ConquestStartMissionTexture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = false;  // the button is drawn at the art's own size

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => ConquestScreenActions.Dispatch(
        ConquestScreenActions.StartRowFor(ConquestScreenActions.Position)));

      startRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      startLabel = textObject.AddComponent<Text>();
      startLabel.text = ConquestScreenActions.StartRowFor(ConquestScreenActions.Position).Label;
      startLabel.alignment = TextAnchor.MiddleCenter;
      startLabel.color = new Color32(255, 244, 214, 255);  // cream on the art's own dark bar
      startLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      startLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
      startLabel.verticalOverflow = VerticalWrapMode.Overflow;
      startLabel.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = new Vector2(6f, 2f);
      textRect.offsetMax = new Vector2(-6f, -2f);
    }

    /// <summary>
    /// The way back - the only row on this screen with a screen behind it (the
    /// war screen it was opened from). Its picture is the frontend's own fancy
    /// button, which the menu, the play screen's way back and the two path
    /// screens' ways back use; the trail ships no exit art of its own.
    /// </summary>
    private void CreateBackButton()
    {
      GameObject buttonObject = new(ConquestScreenActions.Back.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(ConquestScreenActions.Back.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = false;  // drawn at the panel's own width, like the start row

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => ConquestScreenActions.Dispatch(ConquestScreenActions.Back));

      backRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = ConquestScreenActions.Back.Label;
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

      // The trail's own progress picture, over the map's central rect.
      Place(overlayRect, new Vector2(width * OverlayCentreX, height * OverlayCentreY),
            new Vector2(width * OverlayWidth, height * OverlayHeight));

      // The selected row's text, in the panel's left-hand side.
      if (trailText != null)
      {
        trailText.fontSize = Mathf.Max(11, Mathf.RoundToInt(height * 0.021f));
        Place(trailText.rectTransform,
              new Vector2(width * TextCentreX, height * PanelCentreY),
              new Vector2(width * TextWidth, height * TextHeight));
      }

      // The install's own arrow tiles, on the tiles the backdrop paints.
      Place(previousRect, new Vector2(width * PreviousCentreX, height * ArrowCentreY),
            new Vector2(width * ArrowWidth, height * ArrowHeight));
      Place(nextRect, new Vector2(width * NextCentreX, height * ArrowCentreY),
            new Vector2(width * ArrowWidth, height * ArrowHeight));

      // The start row and the way back, in the panel's right-hand side - the
      // install pins neither (see the class note).
      Place(startRect, new Vector2(width * ButtonCentreX, height * StartCentreY),
            new Vector2(width * StartWidth, height * StartHeight));
      Place(backRect, new Vector2(width * ButtonCentreX, height * BackCentreY),
            new Vector2(width * StartWidth, height * BackHeight));

      if (startLabel != null)
      {
        // The restart question is long and the start button is 266 px wide, so
        // the label's size follows its own text (the shipped row is drawn whole).
        startLabel.fontSize = Mathf.Max(10, Mathf.RoundToInt(height * (startLabel.text.Length > 24 ? 0.016f : 0.022f)));
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

    /// <summary>One line of a shipped row's text, for the screen's own log lines.</summary>
    private static string Shorten(string text) =>
      text.Length <= 60 ? text : text.Substring(0, 57) + "...";
  }
}
