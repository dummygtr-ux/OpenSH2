using Assets.Code.Stronghold2.Audio;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// The single MonoBehaviour that makes the placed actor roster orderable.
  ///
  /// <see cref="S2MActorStateDriver"/> already knows how to walk an actor to a
  /// point (verified in batchmode by <c>S2MActorStateRun</c>), and
  /// <c>EntityPlacementLoader</c> already places every actor of a map - but
  /// nothing ever gave the placed actors a driver, so the movement path existed
  /// with nothing wired to it. This component is that wiring: once per load it
  /// walks the "S2M Entities" root and gives every placed actor a driver plus
  /// the ActorType the driver needs to look its clips up.
  ///
  /// Nothing in the loaders or the driver changes here. This class only
  /// ATTACHES drivers and later drives them through the driver's own public
  /// API, so the batchmode verification of the driver stays valid.
  ///
  /// It is also the command surface: an update shell reads the left mouse
  /// button (legacy Input, D003) and routes a press/release through the same
  /// public pick and order methods a headless harness calls, so Play-mode
  /// input and batchmode verification drive one code path (D005) - select the
  /// actor under the cursor, drag a box over several, order the selection to
  /// the ground point, and stop it from the command box.
  ///
  /// The command box is the shipped game's own surface for Stop (R010): the
  /// help's "Unit Commands" section lists Attack Here / Patrol / Stop /
  /// Disband / Mount Horse as the unit command icons, and its "Formations"
  /// section names the "unit command box" that appears with units selected.
  /// Stop is therefore a BUTTON over the world, not a keyboard binding (the
  /// help documents none, R011) and not a right-click verb, so this component
  /// draws a box with one Stop button while the selection is non-empty and
  /// hit-tests it as the FIRST test of a click - the same click path a world
  /// click takes, which is what keeps the harness honest.
  /// </summary>
  public class S2MActorCommander : MonoBehaviour
  {
    /// <summary>
    /// One placed actor and the movement machinery this commander gave it.
    /// Carried by reference so a selection is a list of handles, not a second
    /// copy of the roster.
    /// </summary>
    public class ActorHandle
    {
      public GameObject Actor;
      public S2MActorStateDriver Driver;
      public Renderer Renderer;
      /// <summary>The actor's type - its name up to the LAST space.</summary>
      public string Type;
    }

    private readonly List<ActorHandle> actors = new List<ActorHandle>();

    /// <summary>
    /// The current selection - a list from day one so squad selection can
    /// become additive later without reshaping every caller.
    /// </summary>
    public readonly List<ActorHandle> selection = new List<ActorHandle>();

    /// <summary>The camera clicks are read through; never null after Initialize.</summary>
    public Camera ViewCamera { get; private set; }

    /// <summary>
    /// How far the left button must travel, in pixels, before a press-release
    /// counts as a DRAG rather than a click. A drag selects a box; a click
    /// picks the actor under the cursor. Eight pixels is below any deliberate
    /// drag and above the jitter a click normally produces.
    /// </summary>
    public const float DragThresholdPixels = 8f;

    public int ActorCount => actors.Count;

    /// <summary>The whole roster, cached once by <see cref="Initialize"/>.</summary>
    public IReadOnlyList<ActorHandle> Actors => actors;

    /// <summary>How many selected actors there are.</summary>
    public int SelectionCount => selection.Count;

    /// <summary>
    /// Gives every placed actor under <paramref name="entitiesRoot"/> a driver
    /// and an ActorType, then caches the roster.
    /// </summary>
    /// <param name="entitiesRoot">
    /// The "S2M Entities" root. Pass null to resolve it by name.
    /// </param>
    /// <param name="gameInstallPath">The game install root the driver reads clips from.</param>
    /// <param name="viewCamera">Camera for picking; defaults to Camera.main.</param>
    public void Initialize(GameObject entitiesRoot, string gameInstallPath,
      Camera viewCamera = null)
    {
      ViewCamera = viewCamera != null ? viewCamera : Camera.main;
      actors.Clear();
      selection.Clear();

      if (entitiesRoot == null) entitiesRoot = GameObject.Find("S2M Entities");

      if (entitiesRoot != null)
      {
        // Two levels, exactly as the placement pipeline builds them: the root,
        // one group per model, the actors under that group. An actor is one
        // GameObject even when its model carries several meshes, so the renderer
        // for picking comes from GetComponentInChildren.
        foreach (Transform group in entitiesRoot.transform)
        {
          foreach (Transform child in group)
          {
            GameObject actor = child.gameObject;
            string type = TypeOf(actor.name);

            // Safe attach: never assume the component is absent.
            S2MActorStateDriver driver = actor.GetComponent<S2MActorStateDriver>();
            if (driver == null) driver = actor.AddComponent<S2MActorStateDriver>();
            driver.ActorType = type;
            driver.GameInstallPath = gameInstallPath;

            actors.Add(new ActorHandle
            {
              Actor = actor,
              Driver = driver,
              Renderer = actor.GetComponentInChildren<Renderer>(),
              Type = type,
            });
          }
        }
      }

      string rootName = entitiesRoot != null ? entitiesRoot.name : "(missing)";
      string cameraName = ViewCamera != null ? ViewCamera.name : "(none)";

      Debug.Log($"[S2MActorCommander] attached drivers to {actors.Count} actor(s) under "
                + $"'{rootName}', root '{gameInstallPath}', view camera '{cameraName}'; "
                + $"by type: {Breakdown()}");
    }

    /// <summary>
    /// Picks the actor under a VIEWPORT point (0..1 on both axes, the space
    /// <c>Camera.WorldToViewportPoint</c> returns).
    ///
    /// The pick is by PROJECTED BOUNDS, not by collider: each cached actor's
    /// renderer bounds are projected to a screen rectangle, the rectangles
    /// containing the point are the candidates, and the candidate nearest the
    /// camera wins - the ordering the eye reads when two actors overlap (D001).
    /// Actors own no collider, so picking through one would force the ground
    /// raycast to filter them out again.
    ///
    /// A MISS CHANGES NOTHING: the selection is left exactly as it was. A ground
    /// click is a pick miss by definition (the terrain is not an actor), and the
    /// select-then-command grammar needs that click to still find a selection to
    /// order (R002).
    /// </summary>
    /// <returns>The picked actor, or null when no actor's rect contains the point.</returns>
    public GameObject PickAtViewport(Vector2 viewport)
    {
      if (ViewCamera == null)
      {
        Debug.LogWarning("[S2MActorCommander] cannot pick with no view camera - call Initialize first");
        return null;
      }

      ActorHandle picked = null;
      float pickedDepth = 0f;
      int candidates = 0;
      Vector3 cameraPosition = ViewCamera.transform.position;

      foreach (ActorHandle handle in actors)
      {
        if (handle.Renderer == null) continue;
        if (!ViewportRectOf(handle.Renderer.bounds, ViewCamera, viewport, out _, out _)) continue;
        candidates++;
        float depth = (handle.Renderer.bounds.center - cameraPosition).sqrMagnitude;
        if (picked == null || depth < pickedDepth)
        {
          picked = handle;
          pickedDepth = depth;
        }
      }

      if (picked == null)
      {
        Debug.Log($"[S2MActorCommander] pick at ({viewport.x:F3}, {viewport.y:F3}) found "
                  + $"no actor of {actors.Count} - selection left as it was");
        return null;
      }

      // One click selects one actor (R001), and the selection is REPLACED, not
      // extended, until squad select exists (D004 / R009).
      selection.Clear();
      selection.Add(picked);
      Debug.Log($"[S2MActorCommander] picked {picked.Actor.name} at ({viewport.x:F3}, "
                + $"{viewport.y:F3}), nearest of {candidates} candidate(s)");
      // Selecting a unit is the SELECT verb of the shipped command grammar, so
      // it is the select voice that answers the click.
      PlaySpeech(picked, S2MActorSpeech.SelectId(picked.Type), "select");
      return picked.Actor;
    }

    /// <summary>
    /// Picks at a SCREEN point (pixels, Input.mousePosition space). The screen
    /// to viewport step is the camera's own, so a harness call and a real click
    /// reach one code path (D005).
    /// </summary>
    public GameObject PickAtScreenPoint(Vector2 screenPosition)
    {
      if (ViewCamera == null)
      {
        Debug.LogWarning("[S2MActorCommander] cannot pick with no view camera - call Initialize first");
        return null;
      }
      return PickAtViewport(ViewCamera.ScreenToViewportPoint(screenPosition));
    }

    /// <summary>
    /// Selects every cached actor whose projected rectangle CROSSES the box
    /// dragged between two viewport positions (0..1 on both axes, the space
    /// <c>Camera.WorldToViewportPoint</c> returns). The order of the two
    /// corners is immaterial - a drag up and to the left is normalised here.
    ///
    /// This is R009's squad select, and it is the same rectangle the single
    /// pick uses, so one projection code serves both: <see cref="TryViewportRect"/>
    /// per actor, and the actor is in the box when the two rectangles overlap.
    /// A box CROSSING an actor selects it; a box touching only its edge counts
    /// as crossing too, because that is what a player dragging a box over a
    /// rank of units sees. Requiring full containment instead would be a
    /// one-line change (test min/max against the box inside the loop), but it
    /// would force a player to enclose each body exactly.
    ///
    /// The selection is REPLACED, not extended: the box is drawn from empty
    /// space, so a drag that encloses nothing leaves the selection empty and a
    /// following ground click orders nothing (R006).
    /// </summary>
    /// <returns>How many actors are now selected.</returns>
    public int SelectInBox(Vector2 viewportA, Vector2 viewportB)
    {
      if (ViewCamera == null)
      {
        Debug.LogWarning("[S2MActorCommander] cannot select a box with no view camera - call Initialize first");
        return 0;
      }

      Vector2 min = Vector2.Min(viewportA, viewportB);
      Vector2 max = Vector2.Max(viewportA, viewportB);
      var inBox = new List<ActorHandle>();
      int candidates = 0;

      foreach (ActorHandle handle in actors)
      {
        if (handle.Renderer == null) continue;
        if (!TryViewportRect(handle.Renderer.bounds, ViewCamera, out Vector2 boxMin,
          out Vector2 boxMax)) continue;
        candidates++;
        if (boxMax.x < min.x || boxMin.x > max.x
          || boxMax.y < min.y || boxMin.y > max.y) continue;
        inBox.Add(handle);
      }

      selection.Clear();
      selection.AddRange(inBox);
      Debug.Log($"[S2MActorCommander] box ({min.x:F3}, {min.y:F3})-({max.x:F3}, {max.y:F3}) "
                + $"selected {selection.Count} of {candidates} candidate(s): "
                + $"{string.Join(", ", selection.Select(handle => handle.Actor.name))}");
      // A box over a squad is still one SELECT verb, so one voice answers it -
      // the first man in the box (the shipped game's own rule for several
      // voices at once is not decoded; this is the port's choice, not a fact).
      if (selection.Count > 0)
        PlaySpeech(selection[0], S2MActorSpeech.SelectId(selection[0].Type), "select");
      return selection.Count;
    }

    /// <summary>
    /// The ground point a screen position looks at, from the terrain's own
    /// per-chunk mesh colliders - the only colliders in the scene, so the ray
    /// needs no layer mask.
    /// </summary>
    /// <returns>
    /// True when the ray hit a mesh collider. False, with one named log line,
    /// when it hit nothing (empty sky / off terrain): a miss issues no order and
    /// throws nothing (R006).
    /// </returns>
    public bool TryGetGroundPoint(Vector2 screenPosition, out Vector3 world)
    {
      world = default;
      if (ViewCamera == null)
      {
        Debug.LogWarning("[S2MActorCommander] cannot raycast with no view camera - call Initialize first");
        return false;
      }

      Ray ray = ViewCamera.ScreenPointToRay(screenPosition);
      if (Physics.Raycast(ray, out RaycastHit hit)
        && (hit.collider is MeshCollider
            || hit.collider.GetComponentInChildren<MeshCollider>() != null))
      {
        world = hit.point;
        return true;
      }

      Debug.Log($"[S2MActorCommander] ground miss at screen ({screenPosition.x:F0}, "
                + $"{screenPosition.y:F0}) - the ray hit no terrain mesh, no order issued");
      return false;
    }

    /// <summary>
    /// Orders the whole selection to walk to <paramref name="world"/>.
    ///
    /// The facing snap lives HERE, in the command path (D002): the verified
    /// driver stays position-only, so every order - a first one and a redirect
    /// alike - runs the same snap. <see cref="S2MActorStateDriver.WalkTo"/>
    /// overwrites the destination, so ordering while an actor walks replaces it
    /// without stopping (R004).
    /// </summary>
    public void OrderSelectionTo(Vector3 world)
    {
      if (selection.Count == 0)
      {
        // R006: nothing selected issues nothing.
        Debug.Log("[S2MActorCommander] order with an empty selection - nothing issued");
        return;
      }

      foreach (ActorHandle handle in selection)
      {
        Transform actor = handle.Actor.transform;
        Vector3 heading = world - actor.position;
        heading.y = 0f;
        // A destination on the actor's own spot has no heading to snap to.
        if (heading.sqrMagnitude > 1e-6f)
          actor.rotation = Quaternion.LookRotation(heading);
        handle.Driver.WalkTo(world);
      }

      Debug.Log($"[S2MActorCommander] ordered {selection.Count} actor(s) to "
                + $"({world.x:F3}, {world.y:F3}, {world.z:F3})");
      // The move order is the MOVE verb, and it answers with one voice: the
      // first actor of the selection, not one per actor (see SelectInBox).
      PlaySpeech(selection[0], S2MActorSpeech.MoveId(selection[0].Type), "move");
    }

    /// <summary>
    /// Stops the whole selection where it stands - the command box's Stop
    /// button (R010).
    ///
    /// The shipped Stop "causes units or siege equipment to stop what they are
    /// doing" (help: Unit Commands), so it applies to the SELECTION exactly as
    /// <see cref="OrderSelectionTo"/> does - a squad halts, not one actor - and
    /// each actor keeps the position it had at the press:
    /// <see cref="S2MActorStateDriver.Stop"/> drops the outstanding destination,
    /// and the driver lands back in <see cref="ActorState.Idle"/> on its next
    /// tick, which is the state the clip follows. The selection SURVIVES, so a
    /// stopped unit is still selected and can be re-ordered straight away.
    ///
    /// The guards are the order path's own (R006): an empty selection stops
    /// nothing and throws nothing.
    /// </summary>
    /// <returns>How many of the selected actors were actually moving.</returns>
    public int StopSelection()
    {
      if (selection.Count == 0)
      {
        Debug.Log("[S2MActorCommander] stop with an empty selection - nothing issued");
        return 0;
      }

      int halted = 0;
      foreach (ActorHandle handle in selection)
      {
        if (handle.Driver.IsMoving) halted++;
        handle.Driver.Stop();
      }

      Debug.Log($"[S2MActorCommander] stopped {halted} moving of {selection.Count} selected "
                + $"actor(s); each lands in {ActorState.Idle} on its next tick, position kept");
      return halted;
    }

    /// <summary>
    /// The unit voices the command grammar plays, or null - the default.
    ///
    /// Audio is OPT-IN so the verification runs that predate it keep the
    /// behaviour they proved: with no catalogue every one of these methods
    /// does exactly what it did before, and a run that never calls
    /// <see cref="EnableActorSpeech"/> cannot be affected by the audio path.
    /// </summary>
    private S2MAudioCatalogue speech;

    /// <summary>How many voices the command path has played (the harness's count).</summary>
    public int SpeechPlayCount { get; private set; }

    /// <summary>The last voice id the command path played, or null.</summary>
    public string LastSpeechId { get; private set; }

    /// <summary>Why nothing played, when nothing did - one named line.</summary>
    public string LastSpeechReason { get; private set; }

    /// <summary>The voice that played last and the actor it came from, for reports.</summary>
    public string LastSpeechActor { get; private set; }

    /// <summary>
    /// Turns the unit voices on, reading them from the shipped catalogue. The
    /// speech the game's own sheet pairs with these two verbs is played on the
    /// actor's own AudioSource - SELECT when a click selects it
    /// (<see cref="PickAtViewport"/>, <see cref="SelectInBox"/>) and MOVE when
    /// an order sends it off (<see cref="OrderSelectionTo"/>) - because those
    /// are the two events the shipped help documents for unit command, and the
    /// only two the click grammar produces.
    /// </summary>
    public void EnableActorSpeech(S2MAudioCatalogue catalogue)
    {
      speech = catalogue;
      Debug.Log(catalogue == null
        ? "[S2MActorCommander] unit speech off"
        : $"[S2MActorCommander] unit speech on: {catalogue.IdCount} id(s) "
          + $"({S2MActorSpeech.MappedTypeCount} of {actors.Count} actor type(s) have voices)");
    }

    /// <summary>
    /// Plays one voice id on an actor's own AudioSource, at the sheet's own
    /// volume, and records what happened either way - a silent click is a
    /// result too, and the caller must be able to say why. Guarded everywhere:
    /// no catalogue, no actor, no component, no id, no clip are all named
    /// reasons, none of them an exception (the same contract as the order path).
    /// </summary>
    private void PlaySpeech(ActorHandle handle, string id, string verb)
    {
      if (speech == null) return;
      if (handle == null || handle.Actor == null)
      {
        LastSpeechReason = $"no actor to {verb}";
        return;
      }
      LastSpeechActor = handle.Actor.name;
      if (id == null)
      {
        LastSpeechReason = S2MActorSpeech.WhySilent(handle.Type);
        Debug.Log($"[S2MActorCommander] {verb}: {LastSpeechReason}");
        return;
      }

      if (!speech.TryGetClip(id, out AudioClip clip, out string reason))
      {
        LastSpeechReason = reason;
        Debug.Log($"[S2MActorCommander] {verb}: '{id}' plays nothing - {reason}");
        return;
      }

      // Safe attach: never assume the component is absent.
      AudioSource source = handle.Actor.GetComponent<AudioSource>();
      if (source == null) source = handle.Actor.AddComponent<AudioSource>();
      source.playOnAwake = false;
      // The voice belongs to the body standing in the world, so it is a 3D
      // source at the actor's own position; the sheet's volume column is the
      // clip's own level (100 on every speech row, 50..100 on the effects).
      source.spatialBlend = 1f;
      source.rolloffMode = AudioRolloffMode.Linear;
      source.minDistance = 5f;
      source.maxDistance = 80f;
      if (speech.TryResolve(id, out S2MAudioCatalogue.Entry entry, out _))
        source.volume = Mathf.Clamp01(entry.Volume / 100f);

      source.clip = clip;
      source.Play();
      SpeechPlayCount++;
      LastSpeechId = id;
      LastSpeechReason = null;
      Debug.Log($"[S2MActorCommander] {verb}: '{id}' -> '{clip.name}' "
                + $"({clip.length:F2} s, {clip.channels} ch, {clip.frequency} Hz) on "
                + $"{handle.Actor.name} at volume {source.volume:F2}");
    }

    /// <summary>
    /// The command box's Stop button, in pixels in the space
    /// <see cref="HandleClick"/> reads (<c>Input.mousePosition</c>: origin
    /// bottom-left). This is the ONE rectangle: <see cref="OnGUI"/> draws it
    /// and <see cref="HandleClick"/> hit-tests it, so the button a player sees
    /// cannot drift from the button a click finds.
    ///
    /// The POSITION is a harness choice, not a decoded fact. The help's own
    /// screenshot of the box (`help/gfx/unit commands.bmp`) is cropped to the
    /// box: it shows a 4x4 grid of icon buttons and no screen position, and no
    /// UI layout is decoded, so the box is put in the bottom-left corner as a
    /// placeholder a future HUD replaces.
    /// </summary>
    public Rect StopButtonRect()
    {
      const float margin = 8f, width = 96f, height = 30f;
      return new Rect(margin, margin, width, height);
    }

    /// <summary>
    /// One left click, in pixels (Input.mousePosition space): the command box
    /// first, then the world - pick an actor, and only order when the click
    /// picked no actor. This is the whole Play-mode grammar, so a headless
    /// harness drives exactly this and no second path exists.
    /// </summary>
    public void HandleClick(Vector2 screenPosition)
    {
      // The command box floats over the world, so it gets the click first -
      // but only while there IS a command box: with nothing selected the
      // shipped game shows none, and the same point is a world click.
      if (selection.Count > 0 && StopButtonRect().Contains(screenPosition))
      {
        StopSelection();
        return;
      }

      // Clicking a unit SELECTS it; it does not also command it.
      if (PickAtScreenPoint(screenPosition) != null) return;

      // Clicking the ground with a selection commands it. An empty selection, or
      // a click that resolved no ground point, does nothing - and the miss has
      // already said so once (R006).
      if (selection.Count > 0 && TryGetGroundPoint(screenPosition, out Vector3 ground))
        OrderSelectionTo(ground);
    }

    /// <summary>
    /// The Play-mode input shell (D003): the legacy Input API, the left mouse
    /// button, a drag box and the command box - the grammar itself lives in
    /// <see cref="HandleClick"/> (one click, command box included) and
    /// <see cref="SelectInBox"/> (a drag), so a harness driving either of those
    /// follows exactly this path.
    ///
    /// The button is tracked PRESS-TO-RELEASE rather than acted on at the
    /// press: a drag box cannot be known anywhere else, and a click is simply
    /// a press whose release lands within <see cref="DragThresholdPixels"/>.
    /// A press that lands on the command box is the one exception - a UI press
    /// cannot start a drag box, and its release goes to
    /// <see cref="HandleClick"/>, whose first test is the button itself.
    /// </summary>
    private bool dragging;
    private bool pressOnCommandBox;
    private Vector2 pressPoint;

    private void Update()
    {
      if (ViewCamera == null) return;

      if (Input.GetMouseButtonDown(0))
      {
        pressPoint = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        pressOnCommandBox = selection.Count > 0 && StopButtonRect().Contains(pressPoint);
        dragging = !pressOnCommandBox;
        return;
      }

      if (!Input.GetMouseButtonUp(0)) return;
      dragging = false;
      Vector2 released = new Vector2(Input.mousePosition.x, Input.mousePosition.y);

      if (!pressOnCommandBox
        && Vector2.Distance(pressPoint, released) > DragThresholdPixels)
      {
        // A drag: the box between the press point and the release point, in
        // the viewport space SelectInBox works in.
        SelectInBox(ViewportOf(pressPoint), ViewportOf(released));
        return;
      }

      // A click: the command box's Stop button, the actor under the cursor, or
      // - with a selection out and no actor under it - the ground.
      HandleClick(pressPoint);
    }

    /// <summary>
    /// The two pieces of UI this component owns: the command box (a selection
    /// exists) and the drag box (the button is down). Both are drawn from the
    /// same rectangles the clicks are tested against, so neither can drift.
    /// </summary>
    private void OnGUI()
    {
      if (ViewCamera == null || Event.current == null) return;
      DrawCommandBox();
      DrawDragBox();
    }

    /// <summary>
    /// The command box with its Stop button, drawn while something is selected.
    /// The rectangle on screen is <see cref="StopButtonRect"/> - mouse space,
    /// origin bottom-left - flipped for GUI space, whose origin is top-left.
    /// </summary>
    private void DrawCommandBox()
    {
      if (selection.Count == 0) return;

      const float padding = 4f;
      Rect button = StopButtonRect();
      var screen = new Rect(button.x, Screen.height - button.yMax, button.width, button.height);
      var panel = new Rect(screen.x - padding, screen.y - padding,
        screen.width + padding * 2f, screen.height + padding * 2f);

      Color before = GUI.color;
      GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.72f);
      GUI.DrawTexture(panel, Texture2D.whiteTexture);
      GUI.color = new Color(0.30f, 0.32f, 0.36f, 0.95f);
      GUI.DrawTexture(screen, Texture2D.whiteTexture);
      GUI.color = new Color(0.92f, 0.93f, 0.95f, 0.95f);
      GUI.Label(screen, "Stop", CentredLabel());
      GUI.color = before;
    }

    /// <summary>A centred label style, created once: allocating a GUIStyle per
    /// OnGUI call would allocate one per frame.</summary>
    private static GUIStyle centredLabel;

    private static GUIStyle CentredLabel() =>
      centredLabel ?? (centredLabel = new GUIStyle(GUI.skin.label)
      {
        alignment = TextAnchor.MiddleCenter,
      });

    /// <summary>The drag box itself, while the button is down: the shipped game
    /// draws its box, so a box the player cannot see is not a drag box.</summary>
    private void DrawDragBox()
    {
      if (!dragging || Event.current == null) return;

      // OnGUI's origin is top-left and the mouse's is bottom-left, so the
      // screen y values are flipped here.
      float top = Screen.height - Mathf.Max(pressPoint.y, Event.current.mousePosition.y);
      float bottom = Screen.height - Mathf.Min(pressPoint.y, Event.current.mousePosition.y);
      float left = Mathf.Min(pressPoint.x, Event.current.mousePosition.x);
      float right = Mathf.Max(pressPoint.x, Event.current.mousePosition.x);
      if (right - left < 1f || bottom - top < 1f) return;

      Color before = GUI.color;
      GUI.color = new Color(0.35f, 0.65f, 1f, 0.16f);
      GUI.DrawTexture(new Rect(left, top, right - left, bottom - top), Texture2D.whiteTexture);
      GUI.color = new Color(0.35f, 0.65f, 1f, 0.85f);
      const float edge = 1f;
      GUI.DrawTexture(new Rect(left, top, right - left, edge), Texture2D.whiteTexture);
      GUI.DrawTexture(new Rect(left, bottom - edge, right - left, edge), Texture2D.whiteTexture);
      GUI.DrawTexture(new Rect(left, top, edge, bottom - top), Texture2D.whiteTexture);
      GUI.DrawTexture(new Rect(right - edge, top, edge, bottom - top), Texture2D.whiteTexture);
      GUI.color = before;
    }

    /// <summary>A screen position in pixels, as the viewport point the pick and
    /// the box both take.</summary>
    private Vector2 ViewportOf(Vector2 screenPosition)
    {
      Vector3 point = ViewCamera.ScreenToViewportPoint(screenPosition);
      return new Vector2(point.x, point.y);
    }

    /// <summary>
    /// Projects one world-space bounds to its viewport rectangle. Returns
    /// false for an actor behind the camera, whose projected rectangle folds
    /// over the viewport and would otherwise steal clicks that belong to
    /// actors in front of it.
    /// </summary>
    private static bool ViewportRectOf(Bounds bounds, Camera camera, Vector2 viewport,
      out Vector2 min, out Vector2 max)
    {
      if (!TryViewportRect(bounds, camera, out min, out max)) return false;
      return viewport.x >= min.x && viewport.x <= max.x
        && viewport.y >= min.y && viewport.y <= max.y;
    }

    /// <summary>
    /// The viewport rectangle of one world-space bounds, or false when no
    /// honest rectangle exists.
    ///
    /// The rectangle is the eight corners' axis-aligned bounds. A bounds is
    /// oblong and the projection is not affine, so a rectangle is the honest
    /// test: it is the region that reads as "on the actor" from this camera.
    ///
    /// Behind-the-plane corners are DROPPED, not projected: WorldToViewportPoint
    /// mirrors a point behind the camera, so an actor standing at (or near) the
    /// camera - the one the player is looking from, which is the case that
    /// matters - would otherwise fold its far corners across the whole viewport
    /// and take clicks aimed at every other unit. Found by running the click
    /// path over the real roster, where an archer near the camera did exactly
    /// that until this guard existed.
    /// </summary>
    private static bool TryViewportRect(Bounds bounds, Camera camera,
      out Vector2 min, out Vector2 max)
    {
      min = new Vector2(float.MaxValue, float.MaxValue);
      max = new Vector2(float.MinValue, float.MinValue);

      Vector3 centerProjected = camera.WorldToViewportPoint(bounds.center);
      if (centerProjected.z <= 0f) return false;

      Vector3 center = bounds.center, extents = bounds.extents;
      bool anyInFront = false;
      for (int corner = 0; corner < 8; corner++)
      {
        Vector3 world = center + new Vector3(
          (corner & 1) == 0 ? -extents.x : extents.x,
          (corner & 2) == 0 ? -extents.y : extents.y,
          (corner & 4) == 0 ? -extents.z : extents.z);
        Vector3 point = camera.WorldToViewportPoint(world);
        if (point.z <= 0f) continue;
        anyInFront = true;
        min = Vector2.Min(min, new Vector2(point.x, point.y));
        max = Vector2.Max(max, new Vector2(point.x, point.y));
      }
      // Every corner behind the camera means the actor straddles it: no honest
      // rectangle exists.
      return anyInFront;
    }

    /// <summary>
    /// The type is the actor name up to its LAST space: the loader names every
    /// placed actor "{<type>} {index}" (EntityPlacementLoader), and a type name
    /// with no space (e.g. a renamed FOE row) must come back whole rather than
    /// throwing on a -1 index.
    /// </summary>
    private static string TypeOf(string name)
    {
      int cut = name.LastIndexOf(' ');
      return cut < 0 ? name : name.Substring(0, cut);
    }

    /// <summary>"Pikeman 47, Archer 67, ..." - the one-load roster log.</summary>
    private string Breakdown()
    {
      var counts = new SortedDictionary<string, int>();
      foreach (ActorHandle handle in actors)
      {
        counts.TryGetValue(handle.Type, out int seen);
        counts[handle.Type] = seen + 1;
      }
      return counts.Count == 0
        ? "(none)"
        : string.Join(", ", counts.Select(pair =>
          $"{pair.Key.ToString(CultureInfo.InvariantCulture)} "
          + $"{pair.Value.ToString(CultureInfo.InvariantCulture)}"));
    }
  }
}
