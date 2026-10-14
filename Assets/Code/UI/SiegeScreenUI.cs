using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// The siege selection screen (GameState.SIEGE_SCREEN) - the castle list the
  /// war screen's WARSCREEN_SIEGE row opens.
  ///
  /// What it is made of is the install's, not this port's invention. The backdrop
  /// is the install's own composed picture of the screen
  /// (ui/siege_menu/siege_menu_background.png, 1024x768): the empty gold frame at
  /// the top left where the selected castle's painting goes (measured: its
  /// opening is x 64..489, y 59..372 = 426x314, one pixel more than the 425x313
  /// paintings in each direction), the smaller frame beside it for the castle's
  /// layout picture (opening x 499..642, y 59..220 = 144x162; the layout_*.jpg
  /// pictures are 146x163), the empty leather panel along the bottom left (the
  /// castle's description's place, opening x 70..609, y 385..639) and the tall
  /// empty leather panel down the right (the castle list's place, opening
  /// x 708..947, y 62..639). All four openings are measured off the gold lines
  /// the backdrop paints (2026-10-13; see UIAssets for the frame
  /// rows and columns).
  ///
  /// Into those places the screen puts the install's own data: the selected
  /// castle's painting (castle_&lt;key&gt;_painting.jpg, whose file key is
  /// measured - <see cref="SiegeCastleRow"/>), its layout picture
  /// (layout_&lt;key&gt;.jpg), and into the two panels the install's own words -
  /// the table's castle list (HISTORICCASTLE00..43, twenty-two names, in the
  /// table's own order) down the right, with the selected castle's own name row
  /// and description prose in the panel along the bottom.
  ///
  /// Three things the install does not pin, and this port says so rather than
  /// pretending otherwise: the two buttons' PLACE (the screen's own 'Setup' row
  /// wears the folder's one button picture, button_play.bmp 266x57, and the way
  /// back the folder's compass-rose medallion, button_exit.tga 58x58 - but the
  /// backdrop paints no slot for either), so both stand in the free brown band
  /// below the description panel, which the shipped picture leaves empty; the
  /// words of the way back, which stand under the medallion because the medallion
  /// is 58 px across and the shipped row is four words long; and what the
  /// scaffold of the screen looks like where the installation ships art this
  /// screen's backdrop does not paint - the folder also carries the blank plates
  /// of two further slots (siege_map.bmp, a compass rose; siege_location.bmp, a
  /// map inset) and a framed box (siege_textbox.tga), none of which this port
  /// draws (named on the report, like the trail's completion art).
  ///
  /// The screen owns its own canvas above the war screen's and its own
  /// aspect-ratio bars, like every frontend screen here.
  /// </summary>
  class SiegeScreenUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private Image paintingImage;
    private RectTransform paintingRect;
    private Image layoutImage;
    private RectTransform layoutRect;
    private Text nameText;
    private Text descriptionText;
    private RectTransform setupRect;
    private RectTransform backRect;
    private readonly Text[] castleRowLabels = new Text[SiegeScreenActions.CastleCount];
    private readonly RectTransform[] castleRowRects = new RectTransform[SiegeScreenActions.CastleCount];

    // The four slots the backdrop itself paints, in fractions of the 4:3 content
    // rect (up-positive, centre origin - the campaign screen's own convention, so
    // a measured pixel px of the 1024-wide art is px/1024 - 0.5). Measured off
    // the backdrop's own gold frame lines (see the class note).
    private const float PaintingCentreX = -0.229980f;  // (64 + 425/2) px of 1024, from the centre
    private const float PaintingCentreY = 0.219401f;   // 0.5 - (59 + 313/2) / 768
    private const float PaintingWidth = 0.415039f;     // 425 px of 1024
    private const float PaintingHeight = 0.407552f;    // 313 px of 768

    private const float LayoutCentreX = 0.057617f;     // (499 + 643) / 2 px of 1024, from the centre
    private const float LayoutCentreY = 0.317708f;     // 0.5 - (59 + 221) / 2 / 768
    private const float LayoutWidth = 0.142578f;       // 146 px of 1024
    private const float LayoutHeight = 0.212240f;      // 163 px of 768

    // The description panel's own opening (x 70..609, y 385..639), split into the
    // castle's name heading along its top and the description below it.
    private const float PanelCentreX = -0.168457f;     // (70 + 609) / 2 px of 1024, from the centre
    private const float PanelWidth = 0.527344f;        // 540 px of 1024
    private const float NameCentreY = -0.023438f;      // 0.5 - (387 + 30/2) / 768
    private const float NameHeight = 0.039063f;        // 30 px of 768
    private const float DescriptionCentreY = -0.188151f;  // 0.5 - (420 + 217/2) / 768
    private const float DescriptionHeight = 0.282552f;    // 217 px of 768

    // The castle list's panel (x 708..947, y 62..639): twenty-two rows, one per
    // castle, filling it top to bottom.
    private const float ListCentreX = 0.308105f;       // (708 + 947) / 2 px of 1024, from the centre
    private const float ListWidth = 0.234375f;         // 240 px of 1024
    private const float ListTopY = 0.419271f;          // 0.5 - 62 / 768
    private const float ListRowHeight = 0.034209f;     // (639 - 62) / 22 px of 768

    // The two buttons, in the free brown band below the description panel (the
    // install pins no place for either - see the class note).
    private const float SetupCentreX = -0.301758f;     // (70 + 266/2) px of 1024, from the centre
    private const float SetupCentreY = -0.396484f;     // 0.5 - (660 + 57/2) / 768
    private const float SetupWidth = 0.259766f;        // 266 px of 1024
    private const float SetupHeight = 0.074219f;       // 57 px of 768
    private const float BackCentreX = 0.084961f;       // (570 + 58/2) px of 1024, from the centre
    private const float BackCentreY = -0.397135f;      // 0.5 - (660 + 58/2) / 768
    private const float BackWidth = 0.056641f;         // 58 px of 1024
    private const float BackHeight = 0.075521f;        // 58 px of 768

    private static readonly Color32 SelectedRowColour = new(255, 244, 214, 255);
    private static readonly Color32 OtherRowColour = new(186, 168, 128, 255);

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the war screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, the install's own
    /// backdrop, the selected castle's painting and layout picture in the two
    /// slots it leaves, its own name and description in the panel along the
    /// bottom, the twenty-two castle rows down the right-hand panel, and the two
    /// buttons.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it
      // (and the screen opens at the table's first castle again).
      SiegeScreenActions.ResetRecord();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreatePainting();
      CreateLayout();
      CreateCastleText();
      CreateCastleRows();
      CreateSetupButton();
      CreateBackButton();
      Refresh();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"Siege selection screen: shown ({SiegeScreenActions.CastleCount} castles; "
                + $"showing {SiegeScreenActions.Selected.NameId} '{SiegeScreenActions.Selected.Name}'"
                + (SiegeScreenActions.Selected.HasMap
                   ? $" ({SiegeScreenActions.Selected.MapPath})."
                   : " (the install ships no historical scenario for this castle)."));
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
        paintingImage = null;
        paintingRect = null;
        layoutImage = null;
        layoutRect = null;
        nameText = null;
        descriptionText = null;
        setupRect = null;
        backRect = null;
        for (int i = 0; i < castleRowLabels.Length; i++)
        {
          castleRowLabels[i] = null;
          castleRowRects[i] = null;
        }
      }

      IsShown = false;
    }

    /// <summary>
    /// The flow's own call: this screen is up exactly when the flow's state names
    /// it, so a tick that finds it should be up shows it (and lays it out), and a
    /// tick that does not hides it - which takes its aspect-ratio bars down with
    /// its root.
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
    /// The screen's own refresh, after a castle row's click moved the selection
    /// (SiegeScreenActions.Dispatch): the castle's own painting in the frame the
    /// backdrop paints for it, its own layout picture beside it, and its own name
    /// row and description prose in the panel along the bottom. The list's rows
    /// repaint with it, so the selected castle is the one the screen shows. Called
    /// by the entry point, never by a click directly, so the screen's own record
    /// and what is on the screen cannot disagree.
    /// </summary>
    public void Refresh()
    {
      SiegeCastleRow castle = SiegeScreenActions.Selected;

      if (paintingImage != null)
      {
        paintingImage.sprite = SpriteLoader.LoadSprite(castle.PaintingPath);
      }

      if (layoutImage != null)
      {
        layoutImage.sprite = SpriteLoader.LoadSprite(castle.LayoutPath);
      }

      if (nameText != null)
      {
        nameText.text = castle.Name;
      }

      if (descriptionText != null)
      {
        descriptionText.text = castle.Description;
      }

      for (int i = 0; i < castleRowLabels.Length; i++)
      {
        if (castleRowLabels[i] != null)
        {
          castleRowLabels[i].color = i == SiegeScreenActions.SelectedIndex
            ? SelectedRowColour : OtherRowColour;
        }
      }

      Debug.Log($"Siege selection: {castle.NameId} '{castle.Name}' is the selection "
                + (castle.HasMap
                   ? $"({castle.PaintingPath}; {castle.LayoutPath}; a siege there would load "
                     + $"{castle.MapPath})."
                   : $"({castle.PaintingPath}; {castle.LayoutPath}; the install ships no historical "
                     + "scenario for this castle)."));
    }

    private void CreateUiRoot()
    {
      uiRoot = new GameObject("SiegeScreenUI");

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 1400;  // the campaign screen's and the trail's own order - all three are branches of the war screen's

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new("SiegeScreenUIContentRoot");
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
      GameObject backgroundObject = new("SiegeScreenUIBackground");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.SiegeMenuBackgroundTexture);
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
    /// The selected castle's own painting, in the frame the backdrop paints for
    /// it. The screen opens at the table's first castle and Refresh swaps the
    /// painting as the list's clicks move the selection.
    /// </summary>
    private void CreatePainting()
    {
      GameObject paintingObject = new("SiegeScreenUIPainting");
      paintingObject.transform.SetParent(contentRoot, false);

      paintingImage = paintingObject.AddComponent<Image>();
      paintingImage.sprite = SpriteLoader.LoadSprite(SiegeScreenActions.Selected.PaintingPath);
      paintingImage.type = Image.Type.Simple;
      paintingImage.preserveAspect = false;  // the picture is the slot's own size
      paintingImage.raycastTarget = false;   // the painting itself is not a control

      paintingRect = paintingObject.GetComponent<RectTransform>();
    }

    /// <summary>The selected castle's own layout picture, in the smaller frame the backdrop paints beside it.</summary>
    private void CreateLayout()
    {
      GameObject layoutObject = new("SiegeScreenUILayout");
      layoutObject.transform.SetParent(contentRoot, false);

      layoutImage = layoutObject.AddComponent<Image>();
      layoutImage.sprite = SpriteLoader.LoadSprite(SiegeScreenActions.Selected.LayoutPath);
      layoutImage.type = Image.Type.Simple;
      layoutImage.preserveAspect = false;
      layoutImage.raycastTarget = false;

      layoutRect = layoutObject.GetComponent<RectTransform>();
    }

    /// <summary>
    /// The panel along the bottom: the selected castle's own shipped name row as
    /// a heading, and its shipped description prose below it. Both are the
    /// install's words for the castle the list has selected.
    /// </summary>
    private void CreateCastleText()
    {
      GameObject nameObject = new("SiegeScreenUICastleName");
      nameObject.transform.SetParent(contentRoot, false);

      nameText = nameObject.AddComponent<Text>();
      nameText.text = SiegeScreenActions.Selected.Name;
      nameText.alignment = TextAnchor.MiddleCenter;
      nameText.color = SelectedRowColour;
      nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
      nameText.verticalOverflow = VerticalWrapMode.Truncate;
      // The briefings screen's own treatment of a fixed rect: the text is drawn as
      // large as its rect allows and shrinks to fit rather than spilling out of
      // the panel the backdrop paints (this screen's descriptions run to ~660
      // characters and its twenty-two rows are fixed, so the fit has to be the
      // rect's, not a chosen point size).
      nameText.resizeTextForBestFit = true;
      nameText.resizeTextMinSize = 8;
      nameText.raycastTarget = false;

      GameObject descriptionObject = new("SiegeScreenUIDescription");
      descriptionObject.transform.SetParent(contentRoot, false);

      descriptionText = descriptionObject.AddComponent<Text>();
      descriptionText.text = SiegeScreenActions.Selected.Description;
      descriptionText.alignment = TextAnchor.UpperLeft;
      descriptionText.color = SelectedRowColour;
      descriptionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
      descriptionText.verticalOverflow = VerticalWrapMode.Truncate;
      descriptionText.resizeTextForBestFit = true;
      descriptionText.resizeTextMinSize = 6;
      descriptionText.raycastTarget = false;
    }

    /// <summary>
    /// The castle list: one row per castle of the table's own list, down the
    /// panel the backdrop leaves on the right, each row showing the castle's own
    /// shipped name. Every row's click is the screen's own entry point with that
    /// row, and the selected castle is the one the screen's painting, layout and
    /// description show.
    /// </summary>
    private void CreateCastleRows()
    {
      for (int i = 0; i < SiegeScreenActions.CastleCount; i++)
      {
        SiegeScreenAction row = SiegeScreenActions.SelectRow(i);

        GameObject rowObject = new(row.ButtonName);
        rowObject.transform.SetParent(contentRoot, false);

        // The row's own picture is the background's leather showing through, so
        // it carries a fully transparent Image to be clickable (the screen's own
        // decision: the install ships no picture for a list row).
        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = new Color(0f, 0f, 0f, 0f);
        rowImage.raycastTarget = true;

        Button button = rowObject.AddComponent<Button>();
        button.targetGraphic = rowImage;
        button.transition = Selectable.Transition.None;
        SiegeScreenAction captured = row;
        button.onClick.AddListener(() => SiegeScreenActions.Dispatch(captured));

        castleRowRects[i] = rowObject.GetComponent<RectTransform>();

        GameObject textObject = new("Label");
        textObject.transform.SetParent(rowObject.transform, false);
        Text label = textObject.AddComponent<Text>();
        label.text = row.Label;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = i == SiegeScreenActions.SelectedIndex ? SelectedRowColour : OtherRowColour;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        // Twenty-two rows in one fixed panel: the row text fits its row rather than
        // running into the row below it ('Koblenz-Stolzenfels' is the longest).
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 6;
        label.raycastTarget = false;
        castleRowLabels[i] = label;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
      }
    }

    /// <summary>
    /// The screen's own 'Setup' row - the click that would take the selected
    /// castle into the siege setup - wearing the install's own button_play.bmp.
    /// Its place is the port's reading (the backdrop paints no slot for it - see
    /// the class note).
    /// </summary>
    private void CreateSetupButton()
    {
      GameObject buttonObject = new(SiegeScreenActions.Setup.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(SiegeScreenActions.Setup.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = false;  // the button is drawn at the art's own size

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => SiegeScreenActions.Dispatch(SiegeScreenActions.Setup));

      setupRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = SiegeScreenActions.Setup.Label;
      label.alignment = TextAnchor.MiddleCenter;
      label.color = SelectedRowColour;  // cream on the art's own dark gold bar
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      textRect.anchorMin = Vector2.zero;
      textRect.anchorMax = Vector2.one;
      textRect.offsetMin = new Vector2(6f, 2f);
      textRect.offsetMax = new Vector2(-6f, -2f);
    }

    /// <summary>
    /// The way back - the only row on this screen with a screen behind it (the
    /// war screen it was opened from). Its picture is this screen's OWN exit art,
    /// the install's compass-rose medallion (button_exit.tga, 58x58) - a picture
    /// the folder ships for this row and no other frontend screen uses. The
    /// shipped words are four times the medallion's width, so they stand under it
    /// (the port's reading - the install pins the medallion, not a place for the
    /// words).
    /// </summary>
    private void CreateBackButton()
    {
      GameObject buttonObject = new(SiegeScreenActions.Back.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(SiegeScreenActions.Back.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = false;  // drawn at the medallion's own size

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => SiegeScreenActions.Dispatch(SiegeScreenActions.Back));

      backRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = SiegeScreenActions.Back.Label;
      label.alignment = TextAnchor.UpperCenter;
      label.color = SelectedRowColour;
      label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
      label.raycastTarget = false;

      RectTransform textRect = textObject.GetComponent<RectTransform>();
      // The words hang under the medallion: the label's rect is anchored to the
      // button's bottom edge and reaches below it.
      textRect.anchorMin = new Vector2(0f, 0f);
      textRect.anchorMax = new Vector2(1f, 0f);
      textRect.pivot = new Vector2(0.5f, 1f);
      textRect.offsetMin = new Vector2(-80f, -2f);
      textRect.offsetMax = new Vector2(80f, -2f);
      textRect.anchoredPosition = new Vector2(0f, -2f);
    }

    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      // The selected castle's own painting and layout picture, in the two slots
      // the backdrop paints for them.
      Place(paintingRect, new Vector2(width * PaintingCentreX, height * PaintingCentreY),
            new Vector2(width * PaintingWidth, height * PaintingHeight));
      Place(layoutRect, new Vector2(width * LayoutCentreX, height * LayoutCentreY),
            new Vector2(width * LayoutWidth, height * LayoutHeight));

      // The castle's own name and description, in the panel along the bottom. The
      // sizes below are what the text may grow to on a tall screen; on a shorter
      // one the best-fit shrink takes over, so the install's prose stays inside
      // the panel the backdrop paints whatever the Game view's own size is.
      if (nameText != null)
      {
        nameText.fontSize = Mathf.Max(12, Mathf.RoundToInt(height * 0.024f));
        nameText.resizeTextMaxSize = nameText.fontSize;
        Place(nameText.rectTransform, new Vector2(width * PanelCentreX, height * NameCentreY),
              new Vector2(width * PanelWidth, height * NameHeight));
      }

      if (descriptionText != null)
      {
        descriptionText.fontSize = Mathf.Max(10, Mathf.RoundToInt(height * 0.0165f));
        descriptionText.resizeTextMaxSize = descriptionText.fontSize;
        Place(descriptionText.rectTransform,
              new Vector2(width * PanelCentreX, height * DescriptionCentreY),
              new Vector2(width * PanelWidth, height * DescriptionHeight));
      }

      // The castle list, filling its panel top to bottom - one row per castle, so
      // the twenty-second row ends where the panel does.
      for (int i = 0; i < castleRowRects.Length; i++)
      {
        if (castleRowRects[i] == null) continue;
        float centreY = ListTopY - (i + 0.5f) * ListRowHeight;
        Place(castleRowRects[i], new Vector2(width * ListCentreX, height * centreY),
              new Vector2(width * ListWidth, height * ListRowHeight));
        if (castleRowLabels[i] != null)
        {
          castleRowLabels[i].fontSize = Mathf.Max(8, Mathf.RoundToInt(height * 0.018f));
          castleRowLabels[i].resizeTextMaxSize = castleRowLabels[i].fontSize;
        }
      }

      // The two buttons, in the free band below the description panel.
      Place(setupRect, new Vector2(width * SetupCentreX, height * SetupCentreY),
            new Vector2(width * SetupWidth, height * SetupHeight));
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
