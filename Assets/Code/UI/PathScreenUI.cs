using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Code.UI
{
  /// <summary>
  /// One of the two path screens behind the play screen: the war screen
  /// (WARSCREEN_*, which the play screen's War button opens) and the peace
  /// screen (PEACESCREEN_*, which its Peace button opens). They are the same
  /// kind of screen - the install ships them one backdrop, one set of picture
  /// buttons and their own way back - so the screen itself lives here once, and
  /// each one declares only the rows it carries (<see cref="WarScreenUI"/> the
  /// six war rows, <see cref="PeaceScreenUI"/> the three peace rows) and the
  /// room they need.
  ///
  /// What it is made of is the install's, not this port's invention: the
  /// backdrop is ui/war_peace_menu/war_peace_menu_back.jpg (1024x768 - the stone
  /// wall with a picture frame either side and an empty description bar along
  /// the bottom), and every button is the install's own picture for its row
  /// (<see cref="PathScreenRow.Texture"/>) labelled with that row's shipped text.
  /// There is no title: the shipped table names a title row for the play screen
  /// (PLAYSCREEN_CHOOSE_YOUR_PATH) and none for these two, so this port invents
  /// none.
  ///
  /// Layout is this port's own, read off the backdrop's own composition: the
  /// picture buttons stand in the columns the painted frames stand in, three per
  /// row - so the war screen's six make two rows, the second on the plain stone
  /// between the frames and the description bar - and the way back sits in the
  /// description bar the backdrop ships empty (with no hover panel built, that
  /// bar is the screen's own bottom action's place; the shipped tooltip CONTENT
  /// rides on the rows unshown, the play screen's own decision).
  ///
  /// The screen owns its own canvas above the play screen's and its own
  /// aspect-ratio bars, like every frontend screen here: a 4:3 screen that does
  /// not carry its bars leaves the widescreen gutters to whatever the scene
  /// camera renders (measured on the play screen, 2026-10-12).
  /// </summary>
  abstract class PathScreenUI
  {
    public bool IsShown { get; private set; }

    private GameObject uiRoot;
    private RectTransform contentRoot;
    private AspectRatioBars aspectRatioBars;
    private RectTransform backRect;
    private readonly List<RectTransform> pictureRects = new();
    private readonly List<RectTransform> captionRects = new();

    // Fractions of the 4:3 content rect, so one set of numbers lays a path
    // screen out at any Game view size. The installed pictures are 366x367
    // (square), so a button's width and height are one size in pixels: 0.195 of
    // a 4:3 content's width is 0.26 of its height.
    private const int Columns = 3;
    private const float ButtonWidth = 0.195f;    // of the content width
    private const float ButtonHeight = 0.260f;   // of the content height
    private const float ColumnSpacing = 0.290f;  // centre to centre, of the content width
    private const float CaptionHeight = 0.045f;  // of the content height
    private const float BackWidth = 0.220f;      // of the content width
    private const float BackHeight = 0.080f;     // of the content height
    private const float BackCentreY = -0.340f;   // the backdrop's own description bar

    /// <summary>What the screen calls itself on the console ("War screen").</summary>
    protected abstract string DisplayName { get; }

    /// <summary>The screen's UI root's name in the scene ("WarScreenUI").</summary>
    protected abstract string RootName { get; }

    /// <summary>
    /// This screen's sorting order: above the play screen's 1200, because this
    /// screen replaces it. The two path screens never stand together - the flow's
    /// state names the one that is up - so they share one order.
    /// </summary>
    protected abstract int CanvasOrder { get; }

    /// <summary>The rows this screen shows as picture buttons, in the shipped screen's own order.</summary>
    protected abstract PathScreenRow[] Pictures { get; }

    /// <summary>The way back (the screen's own Previous menu row).</summary>
    protected abstract PathScreenRow Back { get; }

    /// <summary>
    /// The centre-y of each grid row, as a fraction of the content height and
    /// up-positive, in the order the picture rows are laid out (three per row).
    /// The two numbers a screen passes are its reading of the backdrop: the
    /// background's own picture frames are centred a little above the middle
    /// (+0.10), so one row sits there and two straddle it.
    /// </summary>
    protected abstract float[] RowCentres { get; }

    /// <summary>Starts the screen's own dispatch record over - it describes the screen that is on.</summary>
    protected abstract void ResetDispatchRecord();

    /// <summary>The screen's ONE entry point; the base wires every button's click to it.</summary>
    protected abstract void Dispatch(PathScreenRow row);

    /// <summary>
    /// Creates the screen's UI: one root, its own canvas, a full-screen
    /// raycast-blocking background (the play screen may still be in the scene for
    /// the frame the flow switches screens on, and a click must never land
    /// through this screen onto it), its own aspect-ratio bars, one picture
    /// button per row, and the way back.
    /// </summary>
    public void Show()
    {
      if (uiRoot != null)
      {
        return;
      }

      // Built once per showing, so the record always describes the screen on it.
      ResetDispatchRecord();
      pictureRects.Clear();
      captionRects.Clear();

      aspectRatioBars = new();
      aspectRatioBars.Show();
      CreateUiRoot();
      CreateBackground();
      CreatePictureButtons();
      CreateBackButton();
      UpdateLayout();
      IsShown = true;
      Debug.Log($"{DisplayName}: shown ({Pictures.Length + 1} actions: "
                + string.Join(", ", System.Array.ConvertAll(AllRows(), row => row.Label))
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
        pictureRects.Clear();
        captionRects.Clear();
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

    private PathScreenRow[] AllRows()
    {
      var all = new PathScreenRow[Pictures.Length + 1];
      Pictures.CopyTo(all, 0);
      all[Pictures.Length] = Back;
      return all;
    }

    private void CreateUiRoot()
    {
      uiRoot = new GameObject(RootName);

      Canvas canvas = uiRoot.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = CanvasOrder;

      uiRoot.AddComponent<CanvasScaler>();
      uiRoot.AddComponent<GraphicRaycaster>();

      GameObject contentObject = new(RootName + "ContentRoot");
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
      GameObject backgroundObject = new(RootName + "Background");
      backgroundObject.transform.SetParent(contentRoot, false);

      Image backgroundImage = backgroundObject.AddComponent<Image>();
      backgroundImage.sprite = SpriteLoader.LoadSprite(UIAssets.WarPeaceMenuBackgroundTexture);
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
    /// One picture button per row, in the rows' own order, each with its shipped
    /// label as a caption under it and its click wired to this screen's ONE entry
    /// point with its own row captured.
    /// </summary>
    private void CreatePictureButtons()
    {
      foreach (PathScreenRow row in Pictures)
      {
        GameObject buttonObject = new(row.ButtonName);
        buttonObject.transform.SetParent(contentRoot, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.sprite = SpriteLoader.LoadSprite(row.Texture);
        buttonImage.type = Image.Type.Simple;
        buttonImage.preserveAspect = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = buttonImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Dispatch(row));

        pictureRects.Add(buttonObject.GetComponent<RectTransform>());

        // The shipped pictures carry no text of their own (the shipped screen
        // shows them with tooltips), so the row's shipped label is drawn under
        // its picture - the play screen's own arrangement.
        CreateCaption(row.Label + "Caption", row.Label);
      }
    }

    /// <summary>
    /// The way back - the only row on either path screen with a screen behind it.
    /// Its picture is the frontend's own fancy button (the table names the row,
    /// not its picture), which the menu and the play screen's own way back use.
    /// </summary>
    private void CreateBackButton()
    {
      GameObject buttonObject = new(Back.ButtonName);
      buttonObject.transform.SetParent(contentRoot, false);

      Image buttonImage = buttonObject.AddComponent<Image>();
      buttonImage.sprite = SpriteLoader.LoadSprite(Back.Texture);
      buttonImage.type = Image.Type.Simple;
      buttonImage.preserveAspect = true;

      Button button = buttonObject.AddComponent<Button>();
      button.targetGraphic = buttonImage;
      button.transition = Selectable.Transition.None;
      button.onClick.AddListener(() => Dispatch(Back));

      backRect = buttonObject.GetComponent<RectTransform>();

      GameObject textObject = new("Label");
      textObject.transform.SetParent(buttonObject.transform, false);
      Text label = textObject.AddComponent<Text>();
      label.text = Back.Label;
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

    /// <summary>A shipped label under its picture button, tracked so UpdateLayout moves it.</summary>
    private void CreateCaption(string name, string text)
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
      captionRects.Add(captionRect);
    }

    public void UpdateLayout()
    {
      if (contentRoot == null) return;

      float width = Screen.height * (4f / 3f);
      float height = Screen.height;
      contentRoot.sizeDelta = new Vector2(width, height);
      aspectRatioBars?.UpdateLayout();

      float buttonWidth = width * ButtonWidth;
      float buttonHeight = height * ButtonHeight;
      float captionWidth = buttonWidth * 1.6f;
      float captionHeight = height * CaptionHeight;
      int captionFontSize = Mathf.Max(10, Mathf.RoundToInt(height / 40f));

      // The picture buttons, row-major in the order the rows were declared:
      // three per row, centred on the content, at the rows' own centre-y.
      float[] rowCentres = RowCentres;
      for (int i = 0; i < pictureRects.Count; i++)
      {
        int row = i / Columns;
        int column = i % Columns;
        float centreX = (column - (Columns - 1) * 0.5f) * width * ColumnSpacing;
        float centreY = (row < rowCentres.Length ? rowCentres[row] : rowCentres[rowCentres.Length - 1]) * height;

        Place(pictureRects[i], new Vector2(centreX, centreY), new Vector2(buttonWidth, buttonHeight));
        Place(captionRects[i],
              new Vector2(centreX, centreY - (buttonHeight * 0.5f + captionHeight * 0.65f)),
              new Vector2(captionWidth, captionHeight));

        Text caption = captionRects[i].GetComponent<Text>();
        if (caption != null) caption.fontSize = captionFontSize;
      }

      // The way back, centred in the backdrop's own empty description bar.
      Place(backRect, new Vector2(0f, height * BackCentreY),
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
