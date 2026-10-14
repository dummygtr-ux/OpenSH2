using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.TerrainRendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// The click-to-move UAT, headless: a real click on the real map, driven
  /// through the commander's own click entry point.
  ///
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MActorClickRun.Main -quit
  ///
  /// S2MActorControlRun proves the pick and order halves separately (rigs for
  /// the semantics, the placed roster for the integration). What it cannot prove
  /// is the Play-mode GRAMMAR - one click selecting an actor, the next click
  /// ordering it across the ground - because its scene carries no terrain, so a
  /// ground click can never resolve a point there.
  ///
  /// This run loads the map scene (terrain chunks with their real MeshColliders,
  /// plus the placed actors), puts a camera over a Pikeman where a player's
  /// would be, and drives <see cref="S2MActorCommander.HandleClick"/> with
  /// screen positions computed from the camera - the same method the commander's
  /// own update shell calls for a real mouse click, so nothing about the path is
  /// simulated:
  ///
  ///   click 1 on the Pikeman's screen point  -> it is selected, nothing ordered;
  ///   click 2 on a ground point's screen point -> it walks there, and the
  ///                                            destination carries the
  ///                                            terrain height the collider
  ///                                            raycast returned;
  ///   a capture mid-walk                     -> the actor is between its start
  ///                                            and the click, state Walk;
  ///   click 3 on another ground point       -> the destination is replaced and
  ///                                            the facing re-snapped;
  ///   a capture, then the walk completes     -> it arrives and idles.
  ///   a drag box over the densest squad     -> the whole squad is selected, one
  ///                                            ground click walks all of it,
  ///                                            a capture mid-walk shows the
  ///                                            whole squad in frame;
  ///   the command box's Stop button         -> the ordered actor halts, lands
  ///                                            in Idle, and a frame pair 90
  ///                                            ticks apart is pixel-identical
  ///                                            where the walking pair was not.
  ///
  /// The terrain is the only collider in the scene, so the raycast inside
  /// TryGetGroundPoint hits exactly what a player's click would hit.
  /// </summary>
  public static class S2MActorClickRun
  {
    public static string GameRoot => Constants.GamePath;

    private static string Map => Path.Combine(GameRoot, "maps", "war_chapter1.s2m");

    private static string MapJson =>
      Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter1.json");

    /// <summary>Fixed timestep, one frame of a 60 fps Play-mode run.</summary>
    private const float Step = 1f / 60f;

    /// <summary>
    /// Where the camera sits, relative to the actor it watches: steeply off and
    /// above it, so the view looks over the terrain between camera and actor
    /// instead of straight at a hillside that would hide it.
    /// </summary>
    private static readonly Vector3 CameraOffset = new Vector3(-22f, 30f, -22f);

    /// <summary>How far the first order sends the actor, metres.</summary>
    private const float FirstOrder = 8f;

    /// <summary>How far straight east the re-order sends it, metres.</summary>
    private const float SecondOrderEast = 9f;

    /// <summary>How far north the re-order sends it, metres.</summary>
    private const float SecondOrderNorth = 4f;

    /// <summary>
    /// How far the destination a click resolves may sit from the ground point
    /// that was aimed at. The aim point is itself resolved on the terrain by a
    /// downward raycast, so what separates the two is the ground ray's own
    /// grazing angle - a metre or so at these distances.
    /// </summary>
    private const float AimTolerance = 3f;

    /// <summary>
    /// Distinct quantised colours a capture needs before it counts as a picture.
    /// A frame that rendered nothing holds one or two - the clear colour and
    /// perhaps its antialiased edge - so this only has to beat a flat frame.
    /// </summary>
    private const int MinimumCaptureColours = 8;

    /// <summary>
    /// How many of the subject's own pixels a frame must hold for the capture to
    /// count as showing it. A humanoid at ~45 m is a few dozen pixels across,
    /// so this floor only rules an actor out of the picture entirely.
    /// </summary>
    private const int MinimumSubjectPixels = 30;

    /// <summary>
    /// Half the side of the box the squad UAT draws, metres: who stands near the
    /// ordered Pikeman when the box comes down.
    /// </summary>
    private const float UnitBox = 6f;

    /// <summary>Where the squad's order is aimed from the squad's own centre.</summary>
    private const float SquadOrderEast = 6f;

    /// <summary>Ditto, north.</summary>
    private const float SquadOrderNorth = 6f;

    /// <summary>
    /// How many driver ticks apart the two frames of a motion measurement are:
    /// 90 frames is 1.5 s of Play mode and 2.1 m of travel at the driver's
    /// 1.4 m/s, far enough that a walking actor's pixels cannot stay put.
    /// </summary>
    private const int MotionTicks = 90;

    /// <summary>
    /// Changed pixels that count as "the picture shows movement". A humanoid
    /// walking 2.1 m at this camera distance changes thousands of pixels (the
    /// measured walks below are in the thousands), so this floor only rules out
    /// a measurement that came back as nothing at all.
    /// </summary>
    private const int MinimumMotionPixels = 50;

    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;

    public static void Main()
    {
      // The same report-path contract S2MActorControlRun uses, so one run script
      // can drive both.
      string log = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(log))
        log = Path.Combine(Path.GetTempPath(), "sh2_actor_click_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MActorClickRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MActorClickRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      Line($"map          : {Path.GetFileName(Map)}");
      S2MPlacementHarness.DestroyAll();

      var mapFile = S2MFileLoader.Load(Map);
      var world = new GameObject("S2M Map World");
      GameObject terrain = S2MTerrainRenderer.Render(mapFile, world.transform,
        new S2MTerrainSettings { UsePerCellCornerHeights = true });
      GameObject entitiesRoot = S2MPlacementHarness.LoadEntities(MapJson, GameRoot);
      Check(terrain != null && entitiesRoot != null,
        $"the map scene built (terrain {(terrain == null ? "missing" : "built")}, "
        + $"entities {(entitiesRoot == null ? "missing" : "built")})");
      if (terrain == null || entitiesRoot == null) return;

      // A camera where a player's would be, and the light the headless scene
      // otherwise lacks (URP draws nothing unlit - see S2MActorAnimationRun).
      var cameraHost = new GameObject("S2M Click Camera");
      var camera = cameraHost.AddComponent<Camera>();
      camera.fieldOfView = 55f;
      camera.nearClipPlane = 0.3f;
      camera.farClipPlane = 2000f;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
      // Lit from the camera's side, along the same direction the camera looks:
      // a fixed world rotation left the ordered actor standing in its own
      // shadow a few pixels into the frame.
      var light = new GameObject("S2M Click Light").AddComponent<Light>();
      light.type = LightType.Directional;
      light.intensity = 1.3f;
      light.shadows = LightShadows.None;

      // One throwaway frame first: the first render a process makes initialises
      // the pipeline and can come back blank, which would turn every later
      // with/without comparison into a measurement of that blank frame.
      WarmUp(camera);

      var host = new GameObject("S2M Actor Commander");
      var commander = host.AddComponent<S2MActorCommander>();
      commander.Initialize(entitiesRoot, GameRoot, camera);
      Line($"roster       : {commander.ActorCount} actors, "
           + $"{CountColliders(terrain)} terrain chunk collider(s)");

      // The victim: a Pikeman - the type with both an idle and a walk clip, so
      // the ordered walk is the positive case the state run also drives.
      S2MActorCommander.ActorHandle pikeman = commander.Actors.ToList()
        .Find(handle => handle.Type == "Pikeman");
      Check(pikeman != null, $"a Pikeman is on the roster ({commander.ActorCount} actors)");
      if (pikeman == null) return;

      Vector3 start = pikeman.Actor.transform.position;
      FrameCamera(camera, start);
      light.transform.position = start + CameraOffset;
      light.transform.LookAt(start);
      Line($"camera       : {camera.name} {camera.pixelWidth}x{camera.pixelHeight} over "
           + $"{pikeman.Actor.name} at ({start.x:F2}, {start.y:F2}, {start.z:F2})");

      // CLICK 1: on the actor's own screen point. Pick first, order nothing - the
      // grammar's first half, and proof that selecting does not move anything.
      Vector2 onActor = ScreenOf(camera, pikeman.Renderer.bounds.center);
      commander.HandleClick(onActor);
      Check(commander.SelectionCount == 1
            && commander.selection[0].Actor == pikeman.Actor,
        $"clicking the Pikeman selects it ({commander.SelectionCount} selected)");
      Check(!pikeman.Driver.IsMoving && pikeman.Actor.transform.position == start,
        "and the select click issues no order");

      // CLICK 2: on a ground point. The aim point is resolved ON the terrain by a
      // downward raycast, so the click's own ground ray starts from the surface
      // rather than from the air above it.
      Vector3 firstGoal = GroundAt(start.x + FirstOrder * 0.25f, start.z + FirstOrder);
      Vector2 onFirstGround = ScreenOf(camera, firstGoal);
      commander.HandleClick(onFirstGround);
      Check(commander.SelectionCount == 1, "the ground click keeps the selection");
      Check(pikeman.Driver.IsMoving, "and orders the selected Pikeman to walk");
      Vector3 firstDestination = pikeman.Driver.Destination;
      Line($"order        : click aimed at ({firstGoal.x:F2}, {firstGoal.y:F2}, "
           + $"{firstGoal.z:F2}), resolved destination "
           + $"({firstDestination.x:F2}, {firstDestination.y:F2}, {firstDestination.z:F2})");
      Check(Vector3.Distance(firstDestination, firstGoal) < AimTolerance,
        $"the walk goes to the ground point that was clicked (aimed "
        + $"{Vector3.Distance(firstDestination, firstGoal):F2} m away)");
      Check(firstDestination.y < start.y + 0.5f && firstDestination.y > start.y - 12f,
        $"and the destination stands on the terrain (y {firstDestination.y:F2} against "
        + $"the actor's {start.y:F2})");

      // Walk it half way and capture: the actor must be between its start and
      // the click, in the Walk state.
      Vector3 midWalk = DriveTo(pikeman, progress: 0.5f);
      float asked = FlatDistance(start, firstDestination);
      float gone = FlatDistance(start, midWalk);
      Line($"mid-walk     : at ({midWalk.x:F3}, {midWalk.y:F3}, {midWalk.z:F3}), "
           + $"{gone:F3} m of a {asked:F3} m order, state {pikeman.Driver.State}");
      Check(pikeman.Driver.State == ActorState.Walk, "it is walking at the halfway point");
      Check(gone > asked * 0.3f && gone < asked * 0.7f,
        $"it is between its start and the click ({gone:F3} of {asked:F3} m)");
      ReportCapture(Capture(camera, "sh2_click_walk_ch1", pikeman.Actor),
        "the mid-walk capture shows the actor at that point");

      // CLICK 3: mid-walk, on another ground point. The order must replace the
      // destination without stopping, and re-run the facing snap.
      FrameCamera(camera, pikeman.Actor.transform.position);
      Vector3 secondGoal = ClearGround(camera, commander, start, SecondOrderEast,
        SecondOrderNorth);
      Vector2 onSecondGround = ScreenOf(camera, secondGoal);
      Vector3 beforeReOrder = pikeman.Actor.transform.position;
      commander.HandleClick(onSecondGround);
      Check(pikeman.Driver.IsMoving, "a second click mid-walk keeps it moving");
      Vector3 secondDestination = pikeman.Driver.Destination;
      Check(Vector3.Distance(secondDestination, secondGoal) < AimTolerance,
        $"and orders it to the newly clicked ground point (aimed "
        + $"{Vector3.Distance(secondDestination, secondGoal):F2} m away)");
      Check(secondDestination != firstDestination, "replacing the first destination");
      Check(pikeman.Actor.transform.rotation == Quaternion.LookRotation(new Vector3(
              secondDestination.x - beforeReOrder.x, 0f,
              secondDestination.z - beforeReOrder.z)),
        "with the facing re-snapped onto the new heading");

      Vector3 midSecond = DriveTo(pikeman, progress: 0.6f);
      Check(pikeman.Driver.State == ActorState.Walk, "still walking after the re-order");
      FrameCamera(camera, pikeman.Actor.transform.position);
      ReportCapture(Capture(camera, "sh2_click_redirect_ch1", pikeman.Actor),
        "the redirect capture shows it heading the new way");
      Check(midSecond != midWalk, "and it has moved on from where the first order left it");

      // And it arrives on its own: the driver is still the only movement path.
      DriveTo(pikeman, progress: 1f);
      Check(!pikeman.Driver.IsMoving && pikeman.Driver.State == ActorState.Idle,
        "and it arrives and idles at the new destination");
      FrameCamera(camera, pikeman.Actor.transform.position);
      ReportCapture(Capture(camera, "sh2_click_arrived_ch1", pikeman.Actor), "the arrival capture shows it there");

      CheckSquad(camera, light, commander);
      CheckStop(camera, light, commander);
    }

    /// <summary>
    /// The Stop command on the real map (R010), with the capture that proves it
    /// in the PICTURE: the Pikeman is ordered to walk, the command box's Stop
    /// button is clicked through <see cref="S2MActorCommander.HandleClick"/>
    /// (the same entry point a player's click takes), and the frame is measured
    /// twice, <see cref="MotionTicks"/> driver ticks apart - while it walks the
    /// two frames differ by the actor's own motion, and after the stop the same
    /// two frames are pixel-identical. Nothing is ticked between the two grabs
    /// of a pair but the driver, so the diff is the walk itself.
    /// </summary>
    private static void CheckStop(Camera camera, Light light, S2MActorCommander commander)
    {
      Line("--- stop ---");

      // The subject: the Pikeman standing furthest from every other actor. The
      // click that selects it must not be stolen by a neighbour's rectangle
      // (a pick goes to the nearest actor whose rectangle contains the point,
      // D001), and a frame with one actor in it measures that actor.
      S2MActorCommander.ActorHandle pikeman = null;
      float bestGap = -1f;
      foreach (S2MActorCommander.ActorHandle handle in commander.Actors)
      {
        if (handle.Type != "Pikeman") continue;
        float gap = float.MaxValue;
        foreach (S2MActorCommander.ActorHandle other in commander.Actors)
          if (other != handle)
            gap = Mathf.Min(gap, FlatDistance(other.Actor.transform.position,
              handle.Actor.transform.position));
        if (gap > bestGap) { bestGap = gap; pikeman = handle; }
      }
      Check(pikeman != null,
        $"a Pikeman stands clear of the roster to stop ({bestGap:F2} m to its nearest "
        + $"neighbour, {(pikeman != null ? pikeman.Actor.name : "(none)")})");
      if (pikeman == null) return;

      // Order it through the click grammar: a click on the actor selects it, a
      // click on the ground walks it.
      Vector3 start = pikeman.Actor.transform.position;
      FrameCamera(camera, start);
      light.transform.position = start + CameraOffset;
      light.transform.LookAt(start);
      commander.HandleClick(ScreenOf(camera, pikeman.Renderer.bounds.center));
      Check(commander.SelectionCount == 1 && commander.selection[0].Actor == pikeman.Actor,
        $"clicking the Pikeman selects it for the stop ({commander.SelectionCount} selected)");
      if (commander.SelectionCount != 1 || commander.selection[0].Actor != pikeman.Actor) return;
      Vector3 goal = ClearGround(camera, commander, start, SecondOrderEast, SecondOrderNorth);
      commander.HandleClick(ScreenOf(camera, goal));
      Check(pikeman.Driver.IsMoving,
        $"and the ground click orders it to walk ({FlatDistance(start, goal):F2} m)");

      Vector3 walking = DriveTo(pikeman, progress: 0.4f);
      Check(pikeman.Driver.State == ActorState.Walk && pikeman.Driver.IsMoving,
        $"it is walking 40% of the way there ({FlatDistance(start, walking):F2} m)");
      FrameCamera(camera, pikeman.Actor.transform.position);
      light.transform.position = pikeman.Actor.transform.position + CameraOffset;
      light.transform.LookAt(pikeman.Actor.transform.position);

      // The walking control: what the picture says about a unit that is moving.
      var render = new RenderTexture(1280, 720, 24);
      int walkingMotion = MotionPixels(camera, render,
        () => Ticks(pikeman, MotionTicks));
      Check(walkingMotion >= MinimumMotionPixels,
        $"over {MotionTicks} frames of walking the frame changes by {walkingMotion} pixels");

      // The stop, through the command box's button: the button's own rectangle
      // in mouse-pixel space, which is the space HandleClick reads.
      Rect button = commander.StopButtonRect();
      commander.HandleClick(button.center);
      Check(!pikeman.Driver.IsMoving,
        $"the command box's Stop button halts it ({button.center.x:F0}, {button.center.y:F0})");
      Ticks(pikeman, 1);
      Check(pikeman.Driver.State == ActorState.Idle,
        $"and it lands in Idle one tick later ({pikeman.Driver.State})");
      Vector3 stopped = pikeman.Actor.transform.position;
      Check(FlatDistance(start, stopped) < FlatDistance(start, goal) - 0.5f,
        $"it stopped short of the destination ({FlatDistance(start, stopped):F3} m of "
        + $"{FlatDistance(start, goal):F3} m)");

      // The stopped control: the same measurement, the same number of ticks.
      FrameCamera(camera, stopped);
      light.transform.position = stopped + CameraOffset;
      light.transform.LookAt(stopped);
      int stoppedMotion = MotionPixels(camera, render, () => Ticks(pikeman, MotionTicks));
      Line($"stopped      : position ({stopped.x:F3}, {stopped.y:F3}, {stopped.z:F3}), "
           + $"{walkingMotion} pixels of motion while walking, {stoppedMotion} after the stop");
      Check(stoppedMotion == 0,
        $"{MotionTicks} frames later the stopped frame is pixel-identical ({stoppedMotion} changed)");
      Check(pikeman.Actor.transform.position == stopped,
        "and the actor is still exactly where the stop left it");
      Check(pikeman.Driver.State == ActorState.Idle, "idle, not merely motionless");

      UnityEngine.Object.DestroyImmediate(render);

      // The capture of the stopped actor: the lit scene and the actor's own
      // pixels, exactly as the walk captures are measured.
      ReportCapture(Capture(camera, "sh2_click_stopped_ch1", pikeman.Actor),
        "the stop capture shows the actor standing where it halted");

      // A stopped actor is not a frozen one: the grammar takes a new order and
      // it walks the new one to its end.
      Vector3 reGoal = ClearGround(camera, commander, stopped, FirstOrder, -SecondOrderNorth);
      commander.HandleClick(ScreenOf(camera, reGoal));
      Check(pikeman.Driver.IsMoving, "a stopped actor still takes a new order");
      DriveTo(pikeman, progress: 1f);
      Check(!pikeman.Driver.IsMoving && pikeman.Driver.State == ActorState.Idle
            && FlatDistance(stopped, pikeman.Actor.transform.position) > 0.5f,
        $"and walks it to the end, {FlatDistance(stopped, pikeman.Actor.transform.position):F2} m on");
    }

    /// <summary>
    /// <paramref name="frames"/> fixed-timestep driver ticks - the walk a
    /// Play-mode Update would have fed it.
    ///
    /// Ticked UNCONDITIONALLY, including while the driver is not moving: a
    /// halted driver owes its landing in Idle to the first tick after the
    /// <see cref="S2MActorStateDriver.Stop"/>, so a helper that stopped ticking
    /// when IsMoving went false left a stopped actor in Walk and failed the
    /// stop checks that way. The real Update ticks every frame regardless.
    /// </summary>
    private static void Ticks(S2MActorCommander.ActorHandle handle, int frames)
    {
      for (int frame = 0; frame < frames; frame++) handle.Driver.Tick(Step);
    }

    /// <summary>
    /// Renders the frame, runs <paramref name="between"/>, renders again, and
    /// counts the pixels that differ: what the picture says moved between the
    /// two frames. The camera and the light are fixed and nothing else in the
    /// scene animates, so the count is the actor's own motion.
    /// </summary>
    private static int MotionPixels(Camera camera, RenderTexture render, Action between)
    {
      camera.aspect = (float)render.width / render.height;
      camera.targetTexture = render;
      Texture2D before = Grab(camera, render);
      between();
      Texture2D after = Grab(camera, render);
      int moved = ChangedPixels(before, after);
      UnityEngine.Object.DestroyImmediate(before);
      UnityEngine.Object.DestroyImmediate(after);
      return moved;
    }

    /// <summary>
    /// The squad half of the click grammar (R009) on the real map: a drag box
    /// selects the actors it is drawn over, one ground click orders the whole
    /// selection, and a capture shows them all walking. The entry points are the
    /// ones the single-actor clicks above already drove - only the size of the
    /// selection changes, which is the point of the box.
    ///
    /// The drag is played through the same conversions Play mode uses: the two
    /// box corners are computed as screen points from the actors' projected
    /// rectangles, turned back into viewport space by the camera's own
    /// ScreenToViewportPoint, and handed to SelectInBox exactly as the Update
    /// shell hands them over.
    /// </summary>
    private static void CheckSquad(Camera camera, Light light, S2MActorCommander commander)
    {
      Line("--- squad select ---");

      List<S2MActorCommander.ActorHandle> squad = DensestSquad(commander, UnitBox);
      Check(squad.Count >= 3,
        $"a squad of at least three stands together somewhere on the map ({squad.Count})");
      if (squad.Count < 3) return;
      Vector3 squadCentre = Centre(squad);
      Line($"squad        : {squad.Count} actor(s) within {UnitBox} m of each other, "
           + $"centred at ({squadCentre.x:F2}, {squadCentre.y:F2}, {squadCentre.z:F2})");

      FrameCamera(camera, squadCentre);
      light.transform.position = squadCentre + CameraOffset;
      light.transform.LookAt(squadCentre);

      // The drag box: the union of the squad's projected rectangles. Built from
      // each actor's own bounds again, not from the commander's answer.
      var rects = new Dictionary<GameObject, ViewportRect>();
      foreach (S2MActorCommander.ActorHandle handle in squad)
      {
        if (handle.Renderer == null) continue;
        rects[handle.Actor] = RectOf(camera, handle.Renderer.bounds);
      }
      Check(squad.Count(handle => rects.ContainsKey(handle.Actor)) == squad.Count,
        $"every squad member projects a rectangle ({squad.Count})");
      Vector2 min, max;
      BoxOf(rects.Values, 0.02f, out min, out max);

      // A drag over empty ground first - the negative control: a box the player
      // drags over nothing must leave nothing selected, and a click after it
      // must order nothing. The box is put where the harness's own projection
      // says no actor's rectangle reaches.
      Vector2 anywhere = EmptyBox(camera, commander);
      int empty = commander.SelectInBox(anywhere, anywhere + new Vector2(0.02f, 0.25f));
      Check(empty == 0, $"a drag box over empty ground selects nothing ({empty})");
      Check(commander.SelectionCount == 0,
        $"and leaves the selection empty ({commander.SelectionCount})");
      var stillStill = squad.Select(handle => handle.Actor.transform.position).ToList();
      commander.HandleClick(ScreenOf(camera, squadCentre + new Vector3(0f, 0f, 6f)));
      bool drifted = squad.Select(handle => handle.Actor.transform.position)
        .Where((position, index) => position != stillStill[index]).Any();
      Check(!drifted, "so the click after it orders nothing");

      // Now the real drag: press point and release point, in screen pixels,
      // converted back through the camera exactly as the input shell does.
      Vector2 press = ViewportToScreen(camera, min);
      Vector2 release = ViewportToScreen(camera, max);
      int selected = commander.SelectInBox(
        ViewportOfScreen(camera, press), ViewportOfScreen(camera, release));
      Line($"drag         : press ({press.x:F0}, {press.y:F0}) -> release "
           + $"({release.x:F0}, {release.y:F0}), {selected} selected");
      Check(selected == squad.Count,
        $"the drag box selects the whole squad ({selected} of {squad.Count})");
      Check(squad.All(handle => commander.selection.Contains(handle)),
        "and every squad member is in the selection");

      // One ground click for the whole squad. ClearGround finds a point no
      // actor's rectangle covers, so the click orders instead of selecting.
      Vector3 goal = ClearGround(camera, commander, squadCentre, SquadOrderEast,
        SquadOrderNorth);
      commander.HandleClick(ScreenOf(camera, goal));
      Check(commander.SelectionCount == squad.Count,
        "the ground click keeps the whole selection");
      List<S2MActorCommander.ActorHandle> ordered = commander.selection.ToList();
      Check(ordered.Count(handle => handle.Driver.IsMoving) == squad.Count,
        $"and orders every selected actor to walk ({ordered.Count} selected)");
      Check(ordered.All(handle =>
              Vector3.Distance(handle.Driver.Destination, goal) < AimTolerance),
        $"each to the clicked ground point (aimed at "
        + $"({goal.x:F2}, {goal.y:F2}, {goal.z:F2}))");

      // Half way: every selected actor is walking, and the frame holds the
      // squad's own pixels - the with/without measurement over ALL of them.
      foreach (S2MActorCommander.ActorHandle handle in ordered)
        DriveTo(handle, progress: 0.5f);
      Line($"mid-walk     : {ordered.Count(handle => handle.Driver.State == ActorState.Walk)} "
           + $"in Walk, {ordered.Count} selected");
      Check(ordered.Count(handle => handle.Driver.State == ActorState.Walk) == squad.Count,
        "every selected actor is in Walk at the halfway point");
      FrameCamera(camera, Centre(ordered));
      light.transform.position = Centre(ordered) + CameraOffset;
      light.transform.LookAt(Centre(ordered));
      ReportCapture(Capture(camera, "sh2_squad_walk_ch1",
                     ordered.Select(handle => handle.Actor).ToArray()),
        "the mid-walk capture shows the whole ordered squad in frame");

      // And they all arrive: one order, one walk, N idle actors.
      foreach (S2MActorCommander.ActorHandle handle in ordered)
        DriveTo(handle, progress: 1f);
      Check(ordered.Count(handle => !handle.Driver.IsMoving) == squad.Count,
        "every selected actor arrives");
      Check(ordered.Count(handle => handle.Driver.State == ActorState.Idle) == squad.Count,
        "and every one of them is idle on arrival");
    }

    /// <summary>
    /// The densest group of actors standing within <paramref name="radius"/>
    /// metres of each other. The squad UAT drags a box over a real squad, and
    /// the densest place on the map is the one a player would box-select: the
    /// actor with the most neighbours is the cluster's centre.
    /// </summary>
    private static List<S2MActorCommander.ActorHandle> DensestSquad(
      S2MActorCommander commander, float radius)
    {
      List<S2MActorCommander.ActorHandle> best = null;
      foreach (S2MActorCommander.ActorHandle handle in commander.Actors)
      {
        Vector3 at = handle.Actor.transform.position;
        List<S2MActorCommander.ActorHandle> squad = commander.Actors.ToList()
          .FindAll(other => FlatDistance(other.Actor.transform.position, at) <= radius);
        if (best == null || squad.Count > best.Count) best = squad;
      }
      return best ?? new List<S2MActorCommander.ActorHandle>();
    }

    /// <summary>The centre of a squad, for framing the camera over it.</summary>
    private static Vector3 Centre(List<S2MActorCommander.ActorHandle> squad) =>
      squad.Aggregate(Vector3.zero, (sum, handle) => sum + handle.Actor.transform.position)
      / squad.Count;

    /// <summary>
    /// Ticks the driver forward until <paramref name="progress"/> of the way
    /// from the actor's position to its destination is covered, or it arrives,
    /// and returns the position it stopped at. A player's Update feeds the same
    /// Tick, so this is the walk itself and not a mock of it.
    /// </summary>
    private static Vector3 DriveTo(S2MActorCommander.ActorHandle handle, float progress)
    {
      Vector3 from = handle.Actor.transform.position;
      Vector3 to = handle.Driver.Destination;
      float target = FlatDistance(from, to) * progress;
      float walked = 0f;
      while (handle.Driver.IsMoving && walked < target)
      {
        Vector3 before = handle.Actor.transform.position;
        handle.Driver.Tick(Step);
        walked += FlatDistance(before, handle.Actor.transform.position);
      }
      return handle.Actor.transform.position;
    }

    private static float FlatDistance(Vector3 a, Vector3 b) =>
      Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    /// <summary>
    /// The terrain surface at an x/z, from a downward raycast against the same
    /// chunk colliders a ground click resolves through - so an aim point sits on
    /// the ground instead of floating over it, which would make the click's own
    /// ray land past it.
    /// </summary>
    private static Vector3 GroundAt(float x, float z)
    {
      Ray down = new Ray(new Vector3(x, 500f, z), Vector3.down);
      if (Physics.Raycast(down, out RaycastHit hit, 1000f))
        return hit.point;
      return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Puts the camera where a player's hovers over the unit being ordered: a
    /// fixed offset off and above the actor, looking at it, so what the camera
    /// sees is the ordered actor rather than whatever else happened to be in
    /// frame. Re-framed between orders and captures, exactly as a game camera
    /// would track the unit under command.
    /// </summary>
    private static void FrameCamera(Camera camera, Vector3 subject)
    {
      camera.transform.position = subject + CameraOffset;
      camera.transform.LookAt(subject);
    }

    /// <summary>
    /// A ground point along the requested bearing that no actor's rectangle
    /// covers, so a click there really does order a move instead of selecting a
    /// neighbour - a click on a unit selects it (R001), which is the grammar
    /// working, not a failure. The search walks out along the bearing and a
    /// little either side of it, because the roster is packed: on war_chapter1
    /// the archers' rectangles cover most of the ground east of any unit.
    ///
    /// The pick itself is the test, asked first, and its side effect - a probe
    /// over a unit selects that unit - is undone before returning, so the search
    /// leaves the selection exactly as it found it. The whole selection is
    /// restored, not one actor: a squad search must not shrink a squad to its
    /// first member.
    /// </summary>
    private static Vector3 ClearGround(Camera camera, S2MActorCommander commander,
      Vector3 start, float east, float north)
    {
      List<S2MActorCommander.ActorHandle> selected = commander.selection.ToList();
      foreach (float reach in new[] { 0f, 2f, 4f, 6f })
      {
        foreach (float drift in new[] { 0f, 3f, -3f, 6f, -6f })
        {
          Vector3 ground = GroundAt(start.x + east + reach, start.z + north + drift);
          if (commander.PickAtViewport(ViewportOf(camera, ground)) == null)
          {
            Reselect(commander, selected);
            return ground;
          }
        }
      }
      Reselect(commander, selected);
      return GroundAt(start.x + east, start.z + north);
    }

    private static void Reselect(S2MActorCommander commander,
      List<S2MActorCommander.ActorHandle> selected)
    {
      commander.selection.Clear();
      commander.selection.AddRange(selected);
    }

    private static Vector2 ViewportOf(Camera camera, Vector3 world)
    {
      Vector3 point = camera.WorldToViewportPoint(world);
      return new Vector2(point.x, point.y);
    }

    /// <summary>A viewport point in the screen pixels Input.mousePosition uses.</summary>
    private static Vector2 ViewportToScreen(Camera camera, Vector2 viewport) =>
      (Vector2)camera.ViewportToScreenPoint(viewport);

    /// <summary>A screen point back in viewport space - the input shell's own
    /// conversion, so a drag played here follows the same path.</summary>
    private static Vector2 ViewportOfScreen(Camera camera, Vector2 screen)
    {
      Vector3 point = camera.ScreenToViewportPoint(screen);
      return new Vector2(point.x, point.y);
    }

    /// <summary>
    /// A viewport box no actor's rectangle reaches, derived from the projected
    /// geometry: past the top-right corner of the union of ALL of them. Used for
    /// the negative control, because "off screen" is not a safe guess - an
    /// actor's projected rectangle routinely extends past the frame's edge, and
    /// an actor the camera cannot even see still owns a rectangle.
    /// </summary>
    private static Vector2 EmptyBox(Camera camera, S2MActorCommander commander)
    {
      var rects = new List<ViewportRect>();
      foreach (S2MActorCommander.ActorHandle handle in commander.Actors)
        if (handle.Renderer != null)
          rects.Add(RectOf(camera, handle.Renderer.bounds));
      Vector2 min, max;
      BoxOf(rects, 0f, out min, out max);
      return new Vector2(max.x + 0.05f, max.y + 0.05f);
    }

    /// <summary>
    /// One actor's viewport rectangle, projected here so a box check is judged
    /// against the geometry and not against the commander's own answer. The
    /// behind-the-plane corners are dropped, exactly as the commander drops
    /// them - WorldToViewportPoint mirrors such a point and the rectangle would
    /// otherwise fold over the whole viewport.
    /// </summary>
    private struct ViewportRect
    {
      public Vector2 Min;
      public Vector2 Max;
      public bool Valid;
    }

    private static ViewportRect RectOf(Camera camera, Bounds bounds)
    {
      var rect = new ViewportRect
      {
        Min = new Vector2(float.MaxValue, float.MaxValue),
        Max = new Vector2(float.MinValue, float.MinValue),
      };
      if (camera.WorldToViewportPoint(bounds.center).z <= 0f) return rect;

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
        rect.Min = Vector2.Min(rect.Min, new Vector2(point.x, point.y));
        rect.Max = Vector2.Max(rect.Max, new Vector2(point.x, point.y));
      }
      rect.Valid = anyInFront;
      return rect;
    }

    /// <summary>The union of the given rectangles, grown by <paramref name="margin"/>.</summary>
    private static void BoxOf(IEnumerable<ViewportRect> rects, float margin,
      out Vector2 min, out Vector2 max)
    {
      min = new Vector2(float.MaxValue, float.MaxValue);
      max = new Vector2(float.MinValue, float.MinValue);
      bool any = false;
      foreach (ViewportRect rect in rects)
      {
        if (!rect.Valid) continue;
        any = true;
        min = Vector2.Min(min, rect.Min);
        max = Vector2.Max(max, rect.Max);
      }
      if (!any)
      {
        min = max = Vector2.zero;
        return;
      }
      min -= new Vector2(margin, margin);
      max += new Vector2(margin, margin);
    }

    private static Vector2 ScreenOf(Camera camera, Vector3 world)
    {
      Vector3 point = camera.WorldToScreenPoint(world);
      return new Vector2(point.x, point.y);
    }

    private static int CountColliders(GameObject terrain)
    {
      if (terrain == null) return 0;
      return terrain.transform.Cast<Transform>().Count(
        chunk => chunk.GetComponentInChildren<Collider>() != null);
    }

    /// <summary>
    /// Renders the camera into a PNG and returns what came back. The frame is
    /// rendered TWICE - once with the subjects' renderers enabled and once with
    /// them hidden - and the pixels that change between the two are exactly the
    /// subjects' own pixels in the picture. A render texture stands in for the
    /// game view, exactly as S2MActorAnimationRun's captures do: a headless
    /// batchmode run has no display to render into.
    ///
    /// A squad capture hides ALL of the squad's renderers, so the measured
    /// pixels are the whole squad's - one wrong frame and the count collapses.
    /// </summary>
    private static Frame Capture(Camera camera, string name, GameObject subject) =>
      Capture(camera, name, new[] { subject });

    private static Frame Capture(Camera camera, string name, GameObject[] subjects)
    {
      var render = new RenderTexture(1280, 720, 24);
      // Pinned to the target's own aspect, so the frame that gets rendered and
      // the projection measured against it always agree.
      camera.aspect = (float)render.width / render.height;
      camera.targetTexture = render;

      var with = Grab(camera, render);
      var renderers = new List<Renderer>();
      foreach (GameObject subject in subjects)
        renderers.AddRange(subject.GetComponentsInChildren<Renderer>());
      foreach (Renderer renderer in renderers) renderer.enabled = false;
      var without = Grab(camera, render);
      foreach (Renderer renderer in renderers) renderer.enabled = true;

      Rect rect = SubjectRect(camera, UnionBounds(subjects), image: with);
      var result = new Frame(
        Path.Combine(Path.GetTempPath(), $"{name}.png"),
        DistinctColours(with),
        ChangedPixels(with, without),
        $"{subjects.Length} subject(s) {SizeOf(UnionBounds(subjects))} -> "
        + $"{rect.width:F0}x{rect.height:F0} px, {ContentPixels(with, rect)} of them content");
      camera.targetTexture = null;

      File.WriteAllBytes(result.Path, with.EncodeToPNG());
      UnityEngine.Object.DestroyImmediate(with);
      UnityEngine.Object.DestroyImmediate(without);
      UnityEngine.Object.DestroyImmediate(render);
      return result;
    }

    /// <summary>One bounds' size, the way a report line reads it.</summary>
    private static string SizeOf(Bounds bounds) =>
      $"({bounds.size.x:F2}, {bounds.size.y:F2}, {bounds.size.z:F2})";

    /// <summary>The bounds of every renderer under the given subjects.</summary>
    private static Bounds UnionBounds(GameObject[] subjects)
    {
      var bounds = new Bounds();
      bool any = false;
      foreach (GameObject subject in subjects)
        foreach (Renderer renderer in subject.GetComponentsInChildren<Renderer>())
        {
          if (!any) { bounds = renderer.bounds; any = true; }
          else bounds.Encapsulate(renderer.bounds);
        }
      return bounds;
    }

    /// <summary>Renders the camera into its target, then reads the frame back.</summary>
    private static Texture2D Grab(Camera camera, RenderTexture render)
    {
      camera.Render();
      var image = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
      RenderTexture.active = render;
      image.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
      RenderTexture.active = null;
      return image;
    }

    private static void WarmUp(Camera camera)
    {
      var render = new RenderTexture(64, 64, 0);
      camera.aspect = 1f;
      camera.targetTexture = render;
      var image = Grab(camera, render);
      camera.targetTexture = null;
      UnityEngine.Object.DestroyImmediate(image);
      UnityEngine.Object.DestroyImmediate(render);
    }

    /// <summary>What one rendered capture measured.</summary>
    private readonly struct Frame
    {
      public readonly string Path;
      /// <summary>Distinct quantised colours - a flat frame proves nothing.</summary>
      public readonly int Colours;
      /// <summary>Pixels the subject's own renderers put in the frame.</summary>
      public readonly int SubjectPixels;
      /// <summary>What the frame measured the subject to be.</summary>
      public readonly string Detail;

      public Frame(string path, int colours, int subjectPixels, string detail)
      {
        Path = path;
        Colours = colours;
        SubjectPixels = subjectPixels;
        Detail = detail;
      }
    }

    /// <summary>
    /// A capture that renders a flat colour proves nothing: the lit scene has to
    /// be in the picture, AND the subject has to be in it - measured by the
    /// pixels that appear only when its own renderers are enabled.
    /// </summary>
    private static void ReportCapture(Frame frame, string description)
    {
      Line($"capture      : {Path.GetFileName(frame.Path)} "
           + $"{new FileInfo(frame.Path).Length / 1024} kB, {frame.Colours} distinct "
           + $"colours, {frame.SubjectPixels} of the subject's own pixels in frame "
           + $"({frame.Detail})");
      Check(frame.Colours >= MinimumCaptureColours,
        $"the capture shows the lit scene ({frame.Colours} distinct colours)");
      Check(frame.SubjectPixels >= MinimumSubjectPixels, description);
    }

    /// <summary>
    /// The pixels that differ between two frames, by exact colour. Used both
    /// ways round: with and without the subject's renderers it counts the
    /// subject and nothing else, and between two frames of the SAME scene it
    /// counts what moved - the stop UAT's own proof, where the walking pair
    /// differs and the stopped pair is identical. No background, projection or
    /// dominant-colour guesswork can inflate this.
    /// </summary>
    private static int ChangedPixels(Texture2D with, Texture2D without)
    {
      Color[] on = with.GetPixels(), off = without.GetPixels();
      int changed = 0;
      for (int pixel = 0; pixel < on.Length; pixel++)
        if (Rgb(on[pixel]) != Rgb(off[pixel])) changed++;
      return changed;
    }

    /// <summary>
    /// Pixels inside a rectangle that are not the frame's dominant colour, which
    /// is how much stuff is drawn there at all - the subject's own renderers
    /// hidden, the terrain still is.
    /// </summary>
    private static int ContentPixels(Texture2D image, Rect rect)
    {
      int background = DominantBucket(image);
      int found = 0;
      for (int x = (int)rect.xMin; x < (int)rect.xMax; x++)
      {
        for (int y = (int)rect.yMin; y < (int)rect.yMax; y++)
        {
          if (x < 0 || y < 0 || x >= image.width || y >= image.height) continue;
          if (Bucket(image.GetPixel(x, y)) != background) found++;
        }
      }
      return found;
    }

    /// <summary>The frame's background: its most common quantised colour.</summary>
    private static int DominantBucket(Texture2D image)
    {
      Color[] pixels = image.GetPixels();
      var counts = new int[32768];
      foreach (Color pixel in pixels) counts[Bucket(pixel)]++;
      int dominant = 0;
      for (int bucket = 1; bucket < counts.Length; bucket++)
        if (counts[bucket] > counts[dominant]) dominant = bucket;
      return dominant;
    }

    /// <summary>The subject's projected rectangle, clamped into the frame.</summary>
    private static Rect SubjectRect(Camera camera, Bounds bounds, Texture2D image)
    {
      Vector3 center = bounds.center, extents = bounds.extents;
      float minX = float.MaxValue, minY = float.MaxValue;
      float maxX = float.MinValue, maxY = float.MinValue;
      for (int corner = 0; corner < 8; corner++)
      {
        Vector3 world = center + new Vector3(
          (corner & 1) == 0 ? -extents.x : extents.x,
          (corner & 2) == 0 ? -extents.y : extents.y,
          (corner & 4) == 0 ? -extents.z : extents.z);
        Vector3 view = camera.worldToCameraMatrix.MultiplyPoint(world);
        if (view.z > -camera.nearClipPlane) continue;
        Vector4 clip = camera.projectionMatrix * new Vector4(view.x, view.y, view.z, 1f);
        float ndcX = (clip.x / clip.w + 1f) * 0.5f, ndcY = (clip.y / clip.w + 1f) * 0.5f;
        minX = Mathf.Min(minX, ndcX); minY = Mathf.Min(minY, ndcY);
        maxX = Mathf.Max(maxX, ndcX); maxY = Mathf.Max(maxY, ndcY);
      }
      return Rect.MinMaxRect(
        Mathf.Clamp(minX * image.width, 0f, image.width),
        Mathf.Clamp(minY * image.height, 0f, image.height),
        Mathf.Clamp(maxX * image.width, 0f, image.width),
        Mathf.Clamp(maxY * image.height, 0f, image.height));
    }

    /// <summary>Distinct colours, quantised to 5 bits per channel.</summary>
    private static int DistinctColours(Texture2D image)
    {
      Color[] pixels = image.GetPixels();
      var seen = new HashSet<int>();
      foreach (Color pixel in pixels) seen.Add(Bucket(pixel));
      return seen.Count;
    }

    /// <summary>One pixel's exact 24-bit colour, so a change means a change and
    /// not a change big enough to cross a quantisation boundary.</summary>
    private static int Rgb(Color colour) =>
      ((int)(colour.r * 255f) << 16) | ((int)(colour.g * 255f) << 8)
      | (int)(colour.b * 255f);

    /// <summary>One pixel's 5-bits-per-channel colour key.</summary>
    private static int Bucket(Color colour) =>
      (Quantise(colour.r) << 10) | (Quantise(colour.g) << 5) | Quantise(colour.b);

    /// <summary>
    /// A 0..255 channel value into its 5-bit bucket. The clamp is against 255,
    /// NOT against 31: clamping early and shifting late folded every channel
    /// above 31 into the top bucket, so a whole frame of lit terrain counted as
    /// one or two colours and the "the capture shows the lit scene" check was
    /// measuring almost nothing. The squad capture found it: a frame holding
    /// 1,539 of the squad's own pixels came back as 6 distinct colours, while a
    /// real 5-bit count of the same PNG gives 570.
    /// </summary>
    private static int Quantise(float channel)
    {
      int value = (int)(channel * 255f);
      value = value < 0 ? 0 : value > 255 ? 255 : value;
      return value >> 3;
    }

    private static void Check(bool passed, string description)
    {
      Line($"{(passed ? "PASS" : "FAIL")}         : {description}");
      if (!passed) failures++;
    }

    private static void Line(string text)
    {
      Report.AppendLine(text);
    }
  }
}
