using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode check for <see cref="S2MActorCommander"/> - the component that
  /// gives every placed actor a movement driver and drives them from a click.
  ///
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MActorControlRun.Main -quit
  ///
  /// The roster war_chapter1 places is already known exactly (166 actors, by
  /// type). What is NOT known is whether the commander attaches one driver to
  /// every one of them with the right type - so this loads the same map the
  /// state run does, builds a commander over it, and checks the attach outcome
  /// actor by actor. Later checks (pick, order) join this same report; the run
  /// always gates on the one SUMMARY line at the end.
  /// </summary>
  public static class S2MActorControlRun
  {
    public static string GameRoot => Constants.GamePath;

    private static string MapJson =>
      Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter1.json");

    /// <summary>Documented war_chapter1 placement, the total the roster must match.</summary>
    private const int ExpectedPlaced = 166;

    /// <summary>Fixed timestep, so a driven order is repeatable frame for frame.</summary>
    private const float Step = 1f / 60f;

    /// <summary>Ceiling on an order's frame loop (4 m at 1.4 m/s is 171 frames).</summary>
    private const int MaxOrderFrames = 600;

    /// <summary>How far the ordered actors are sent, metres.</summary>
    private const float OrderDistance = 4f;

    /// <summary>
    /// Half the side of the box a real squad is drawn in, metres. war_chapter1's
    /// troops stand in loose ranks and formations, so a few metres is enough to
    /// find a compact group without pulling in half the roster.
    /// </summary>
    private const float UnitBox = 6f;

    /// <summary>
    /// Squared-distance slack that still counts as a depth tie. Two distinct
    /// actors metres apart differ by far more than this, so a tie means the two
    /// bounds centres are equidistant from the camera to within float noise.
    /// </summary>
    private const float DepthTie = 1f;

    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;

    public static void Main()
    {
      string log = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(log))
        log = Path.Combine(Path.GetTempPath(), "sh2_actor_control_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MActorControlRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MActorControlRun]\n" + Report.ToString().TrimEnd());
    }

    /// <summary>
    /// Attaches, then checks the attach. One commander, one entities root,
    /// disk loaded - the same objects a Play-mode run would attach to.
    /// </summary>
    private static void Run()
    {
      Line($"map          : {Path.GetFileName(MapJson)}");
      S2MPlacementHarness.DestroyAll();
      GameObject entitiesRoot = S2MPlacementHarness.LoadEntities(MapJson, GameRoot);
      Line($"root         : {(entitiesRoot != null ? entitiesRoot.name : "(none)")}");

      // A camera for the real roster: batchmode has no Camera.main, and the
      // pick's screen-to-viewport step needs one. The rig pick checks below
      // bring their own cameras; this one is what a Play-mode click would read.
      Camera mapCamera = BuildRosterCamera("S2M Pick Camera (map)", entitiesRoot);

      var host = new GameObject("S2M Actor Commander");
      var commander = host.AddComponent<S2MActorCommander>();
      commander.Initialize(entitiesRoot, GameRoot, mapCamera);
      Line($"camera       : {mapCamera.name} {mapCamera.pixelWidth}x{mapCamera.pixelHeight}");
      Line("");

      // 1. The roster is the placed actors, cached once.
      int placed = CountActorGrandchildren(entitiesRoot);
      Line($"placed       : {placed} actor(s) placed, {commander.ActorCount} cached");
      Check(commander.ActorCount == placed,
        $"the commander cached every placed actor ({commander.ActorCount} of {placed})");
      Check(commander.ActorCount == ExpectedPlaced,
        $"the roster is the documented {ExpectedPlaced} placements ({commander.ActorCount})");

      // 2. Every cached actor has exactly one driver with the right type.
      List<S2MActorCommander.ActorHandle> roster = commander.Actors.ToList();
      int singleDriver = 0, correctType = 0, withRenderer = 0;
      var typeCounts = new SortedDictionary<string, int>();
      foreach (S2MActorCommander.ActorHandle handle in roster)
      {
        GameObject actor = handle.Actor;
        int drivers = actor.GetComponents<S2MActorStateDriver>().Length;
        bool driverCached = handle.Driver != null;
        if (drivers == 1) singleDriver++;
        if (driverCached && handle.Driver == actor.GetComponent<S2MActorStateDriver>()
            && actor.name.StartsWith(handle.Type + " ", StringComparison.Ordinal))
          correctType++;
        if (handle.Renderer != null && handle.Renderer.transform.IsChildOf(handle.Actor.transform))
          withRenderer++;
        if (driverCached)
        {
          typeCounts.TryGetValue(handle.Type, out int seen);
          typeCounts[handle.Type] = seen + 1;
        }
      }
      Line($"drivers      : {singleDriver} with exactly one, {correctType} with a cached "
           + $"driver of the right type, {withRenderer} with a renderer");
      Check(roster.Count > 0 && singleDriver == roster.Count,
        $"every actor carries exactly one driver ({singleDriver} of {roster.Count})");
      Check(correctType == roster.Count,
        $"every handle's driver is the cached one and its type parses from the name "
        + $"({correctType} of {roster.Count})");
      Check(withRenderer == roster.Count,
        $"every handle has a renderer for picking ({withRenderer} of {roster.Count})");

      // 3. No actor carries two drivers.
      List<string> doubled = roster
        .Where(handle => handle.Actor.GetComponents<S2MActorStateDriver>().Length > 1)
        .Select(handle => handle.Actor.name)
        .ToList();
      Check(doubled.Count == 0,
        $"no actor carries two drivers ({doubled.Count}: {string.Join(", ", doubled.Take(5))})");

      // 4. The type breakdown matches what war_chapter1 places.
      string breakdown = string.Join(", ",
        typeCounts.Select(pair => $"{pair.Key}={pair.Value.ToString(CultureInfo.InvariantCulture)}"));
      Line($"types        : {breakdown}");
      Check(typeCounts.TryGetValue("Archer", out int archers) && archers == 67,
        $"67 Archers are on the roster ({archers})");
      Check(typeCounts.TryGetValue("Ballista", out int ballistae) && ballistae == 3,
        $"3 Ballistae are on the roster ({ballistae})");

      // 5. At least one Pikeman and at least one Ballista, both with a driver.
      S2MActorCommander.ActorHandle pikeman = roster.Find(
        handle => handle.Type == "Pikeman");
      S2MActorCommander.ActorHandle ballista = roster.Find(
        handle => handle.Type == "Ballista");
      Check(pikeman != null && pikeman.Driver != null
            && pikeman.Driver.ActorType == "Pikeman",
        $"a Pikeman is present with a driver ({pikeman?.Actor.name ?? "(none)"})");
      Check(ballista != null && ballista.Driver != null
            && ballista.Driver.ActorType == "Ballista",
        $"a Ballista is present with a driver ({ballista?.Actor.name ?? "(none)"})");

      Line("");
      CheckPick();
      CheckSquadSelect();
      CheckPickRealRoster(commander);
      Line("");
      CheckOrder(commander);
      CheckSquadOrder(commander);
      CheckStop(commander);
    }

    /// <summary>
    /// The pick half of select-then-command (R001, D001): the actor under the
    /// cursor is the one selected, the nearest of two overlapping actors wins,
    /// and an empty viewport point selects nothing.
    ///
    /// The picks run against small RIGS - synthetic actors at known world
    /// positions in front of a camera this method builds - rather than against
    /// the 166 placed actors, because with a real roster there is no way to know
    /// that no OTHER actor's rectangle covers the point being picked. The rig
    /// actors go through the same <see cref="S2MActorCommander.Initialize"/> and
    /// <see cref="S2MActorCommander.PickAtViewport"/> path the placed ones do;
    /// the placed ones are picked at the end of <see cref="Run"/>.
    /// </summary>
    private static void CheckPick()
    {
      Line("--- picking ---");

      // 1. Under the cursor: two actors side by side, each picked on its own
      //    projected centre.
      Camera sideCamera = BuildPickCamera("S2M Pick Camera (side)",
        new Vector3(1.5f, 2.5f, -8f), new Vector3(1.5f, 0f, 0f));
      var sideCommander = BuildCommander("S2M Actor Commander (side)",
        BuildRig("S2M Pick Rig (side)", ("Pikeman 0", new Vector3(0.5f, 0f, 0f)),
                 ("Pikeman 1", new Vector3(2.5f, 0f, 0f))), sideCamera);
      List<S2MActorCommander.ActorHandle> side = sideCommander.Actors.ToList();
      Check(side.Count == 2, $"the side rig placed two actors ({side.Count})");

      Vector2 first = ViewportPointOf(sideCamera, side[0].Renderer.bounds.center);
      Vector2 second = ViewportPointOf(sideCamera, side[1].Renderer.bounds.center);
      Line($"camera       : {sideCamera.name} {sideCamera.pixelWidth}x{sideCamera.pixelHeight}, "
           + $"'Pikeman 0' at ({first.x:F3}, {first.y:F3}), 'Pikeman 1' at ({second.x:F3}, {second.y:F3})");
      Check(OnScreen(first) && OnScreen(second),
        $"both rig actors project on screen (a 0x0 headless camera would show NaN)");
      Check(Vector2.Distance(first, second) > 0.05f,
        $"and their projected rects are far apart ({Vector2.Distance(first, second):F3})");

      GameObject picked = sideCommander.PickAtViewport(first);
      Check(picked == side[0].Actor,
        $"the pick under 'Pikeman 0' selects it ({Name(picked)})");
      Check(sideCommander.SelectionCount == 1 && sideCommander.selection[0] == side[0],
        $"and the selection holds exactly that actor ({sideCommander.SelectionCount})");
      picked = sideCommander.PickAtViewport(second);
      Check(picked == side[1].Actor,
        $"the pick under 'Pikeman 1' selects it too ({Name(picked)})");

      // 2. Nearest camera depth wins an overlap: two actors on the SAME
      //    camera-forward line project onto the same point, so the near one's
      //    rectangle contains the far one's centre.
      Camera depthCamera = BuildPickCamera("S2M Pick Camera (depth)",
        new Vector3(0f, 3f, -10f), new Vector3(0f, 0f, 0f));
      Vector3 forward = depthCamera.transform.forward;
      Vector3 eye = depthCamera.transform.position;
      var depthCommander = BuildCommander("S2M Actor Commander (depth)",
        BuildRig("S2M Pick Rig (depth)", ("Pikeman 2", eye + forward * 7f),
                 ("Pikeman 3", eye + forward * 17f)), depthCamera);
      List<S2MActorCommander.ActorHandle> depth = depthCommander.Actors.ToList();
      Check(depth.Count == 2, $"the depth rig placed two actors ({depth.Count})");
      Vector3 nearCenter = depth[0].Renderer.bounds.center;
      Vector3 farCenter = depth[1].Renderer.bounds.center;
      Vector2 nearPoint = ViewportPointOf(depthCamera, nearCenter);
      Vector2 farPoint = ViewportPointOf(depthCamera, farCenter);
      Check((nearCenter - eye).sqrMagnitude < (farCenter - eye).sqrMagnitude
            && Vector2.Distance(nearPoint, farPoint) < 0.05f,
        $"the two depth-rig actors share one projected point, the nearer at "
        + $"{Vector2.Distance(nearPoint, farPoint):F3} from it");

      picked = depthCommander.PickAtViewport(farPoint);
      Check(picked == depth[0].Actor,
        $"the nearest of the two overlapping actors wins ({Name(picked)} vs near "
        + $"{depth[0].Actor.name})");
      Check(depthCommander.SelectionCount == 1, "and the selection is that one actor");

      // 2b. An actor standing AT the camera must not steal off-axis clicks. Its
      //     corners behind the near plane project mirrored, so an actor there
      //     used to fold a rectangle over most of the viewport - the case the
      //     real roster hit (an archer beside a tracking camera took every click
      //     aimed east of the selected unit).
      Vector3 depthEye = depthCamera.transform.position;
      Vector3 depthForward = depthCamera.transform.forward;
      var nearCommander = BuildCommander("S2M Actor Commander (near)",
        BuildRig("S2M Pick Rig (near)",
          ("Pikeman 4", depthEye + depthForward * 1.5f),
          ("Pikeman 5", depthEye + depthForward * 20f + depthCamera.transform.right * 6f)),
        depthCamera);
      List<S2MActorCommander.ActorHandle> near = nearCommander.Actors.ToList();
      Check(near.Count == 2, $"the near rig placed two actors ({near.Count})");
      Vector2 atCamera = ViewportPointOf(depthCamera, near[0].Renderer.bounds.center);
      Vector2 offAxis = ViewportPointOf(depthCamera, near[1].Renderer.bounds.center);
      Check(Vector2.Distance(offAxis, atCamera) > 0.1f,
        $"the two rig actors project well apart (the actor at the camera sits at "
        + $"{atCamera}, the one off its axis at {offAxis})");
      picked = nearCommander.PickAtViewport(offAxis);
      Check(picked == near[1].Actor,
        $"a click on the off-axis actor is not stolen by the one at the camera "
        + $"({Name(picked)} vs {near[1].Actor.name})");

      // 3. An empty viewport point: a world point in front of the camera but so
      //    far off its axis that no rig actor's rectangle can reach it.
      Vector2 sky = ViewportPointOf(sideCamera, sideCamera.transform.position
        + sideCamera.transform.forward * 100f + sideCamera.transform.right * 100f);
      Check(IsReal(sky) && sky.x > 1f,
        $"the empty-sky point is off screen to the right (x={sky.x:F3})");
      sideCommander.selection.Clear();
      picked = sideCommander.PickAtViewport(sky);
      Check(picked == null, $"an empty-sky pick returns null ({Name(picked)})");
      Check(sideCommander.SelectionCount == 0,
        $"and leaves the selection empty ({sideCommander.SelectionCount})");

      // 4. The same pick on the real roster: the rigs prove the semantics, the
      //    placed actors prove them on the loaded renderers. That one check
      //    needs the map camera, so Run ends with it.
    }

    /// <summary>
    /// The real roster from above: every placed actor's own bounds centre must
    /// be pickable, and the winner of such a click must be the clicked actor or
    /// one at least as near the camera - never a far-away one. A camera above the
    /// whole roster keeps every actor in front of it, and an actor's centre is
    /// inside its own rectangle by construction, so a null here would mean the
    /// pick is broken.
    ///
    /// The "clicked actor itself" count is a MEASUREMENT, not a rule: when two
    /// actors' projected rectangles overlap (a rank seen from above), the nearer
    /// one wins the click at the farther one's centre - the D001 tie-break doing
    /// its job, exactly what a player would see. war_chapter1's archers stand in
    /// a ring, so a few of those contests are exact depth ties, which the pick
    /// gives to the first actor it meets on the roster.
    /// </summary>
    private static void CheckPickRealRoster(S2MActorCommander commander)
    {
      List<S2MActorCommander.ActorHandle> roster = commander.Actors.ToList();
      Check(commander.ViewCamera != null,
        $"the map commander has a view camera ({commander.ViewCamera?.name ?? "(none)"})");

      int pickable = 0, own = 0, nearer = 0, tied = 0, farther = 0;
      var wrong = new List<string>();
      Vector3 cameraPosition = commander.ViewCamera.transform.position;
      foreach (S2MActorCommander.ActorHandle handle in roster)
      {
        if (handle.Renderer == null) continue;
        Vector3 center = handle.Renderer.bounds.center;
        Vector2 at = ViewportPointOf(commander.ViewCamera, center);
        if (!IsReal(at)) continue;
        GameObject picked = commander.PickAtViewport(at);
        if (picked == null) continue;
        pickable++;
        if (picked == handle.Actor) { own++; continue; }

        // Some other actor won. The documented rule (D001) is nearest camera
        // depth, so it can only be one that is nearer - or exactly as near,
        // where the first actor on the roster keeps the click.
        Renderer winner = picked.GetComponentInChildren<Renderer>();
        float clicked = (center - cameraPosition).sqrMagnitude;
        float won = winner != null
          ? (winner.bounds.center - cameraPosition).sqrMagnitude : float.NaN;
        if (won < clicked - DepthTie) nearer++;
        else if (won <= clicked + DepthTie) tied++;
        else farther++;
        if (farther > 0 && farther <= 3)
          wrong.Add($"{handle.Actor.name}->{picked.name} (depth {won:F1} vs {clicked:F1})");
      }
      Line($"roster pick  : {pickable} of {roster.Count} pickable at their own centre, "
           + $"{own} of them the clicked actor itself, {nearer} won by a nearer actor "
           + $"and {tied} by an equally near one");

      Check(pickable == roster.Count,
        $"every placed actor is pickable at its own bounds centre ({pickable} of {roster.Count})");
      Check(farther == 0,
        $"and no click ever selects an actor farther than the one clicked "
        + $"({farther} did: {string.Join(", ", wrong)})");
    }

    /// <summary>
    /// The squad half (R009): a drag box selects every actor whose projected
    /// rectangle it crosses, the box REPLACES the selection, a box over nothing
    /// selects nothing, and a box that covers only part of an actor still
    /// selects it.
    ///
    /// Rigs again, for the same reason the pick uses them: a box over the real
    /// roster would have to know which other actors happen to share its space.
    /// What every check compares against is a viewport rectangle derived HERE
    /// from the projected bounds corners, not the commander's own answer, so a
    /// broken projection fails the check instead of agreeing with itself.
    /// </summary>
    private static void CheckSquadSelect()
    {
      Line("--- squad select ---");

      Camera camera = BuildPickCamera("S2M Pick Camera (squad)",
        new Vector3(0f, 4f, -12f), new Vector3(0f, 0f, 0f));
      var squadCommander = BuildCommander("S2M Actor Commander (squad)",
        BuildRig("S2M Squad Rig (squad)",
          ("Pikeman 0", new Vector3(-3f, 0f, -2f)),
          ("Pikeman 1", new Vector3(3f, 0f, -2f)),
          ("Pikeman 2", new Vector3(-3f, 0f, 2f)),
          ("Pikeman 3", new Vector3(3f, 0f, 2f))), camera);
      List<S2MActorCommander.ActorHandle> squad = squadCommander.Actors.ToList();
      Check(squad.Count == 4, $"the squad rig placed four actors ({squad.Count})");

      // The rig's projected geometry, derived here.
      var rects = new Dictionary<GameObject, ViewportRect>();
      foreach (S2MActorCommander.ActorHandle handle in squad)
      {
        ViewportRect rect = RectOf(camera, handle.Renderer.bounds);
        rects[handle.Actor] = rect;
        Check(rect.Valid && OnScreen(rect.Center),
          $"{handle.Actor.name} projects a real rectangle centred on screen "
          + $"({rect.Center.x:F3}, {rect.Center.y:F3})");
      }
      Check(Vector2.Distance(rects[squad[0].Actor].Center, rects[squad[2].Actor].Center) > 0.1f,
        $"the rig's left and right columns project well apart "
        + $"({Vector2.Distance(rects[squad[0].Actor].Center, rects[squad[2].Actor].Center):F3})");

      // 1. A box in empty sky selects nothing - and ordering an empty selection
      //    still issues nothing (R006). This is the negative control: a drag
      //    the player makes over blank ground must not hand the previous
      //    selection an order.
      Vector2 emptyMin = new Vector2(1.4f, 0.2f), emptyMax = new Vector2(1.8f, 0.8f);
      int empty = squadCommander.SelectInBox(emptyMin, emptyMax);
      Check(empty == 0, $"a box in empty sky selects nothing ({empty})");
      Check(squadCommander.SelectionCount == 0,
        $"and leaves the selection empty ({squadCommander.SelectionCount})");
      var untouched = squad.Select(handle => handle.Actor.transform.position).ToList();
      squadCommander.OrderSelectionTo(new Vector3(0f, 0f, 4f));
      bool moved = squad.Select(handle => handle.Actor.transform.position)
        .Where((position, index) => position != untouched[index]).Any();
      Check(!moved, "so an order with an empty selection issues nothing");

      // 2. A box around all four selects all four - and the selection is also
      //    the set the geometry says the box crosses, derived here.
      Vector2 fullMin, fullMax;
      BoxOf(squad.Select(handle => rects[handle.Actor]), 0.05f, out fullMin, out fullMax);
      int full = squadCommander.SelectInBox(fullMin, fullMax);
      Check(full == 4, $"a box around all four selects all four ({full})");
      List<S2MActorCommander.ActorHandle> crossed = squad
        .Where(handle => RectsCross(rects[handle.Actor], fullMin, fullMax)).ToList();
      Check(SameSet(squadCommander.selection, crossed) && crossed.Count == 4,
        "and the selection is exactly the four the geometry says the box crosses");

      // 3. A box around one column selects that column: the box replaces the
      //    selection rather than extending it, and an actor the box never
      //    touches is not in it.
      var leftColumn = new List<S2MActorCommander.ActorHandle> { squad[0], squad[2] };
      Vector2 columnMin, columnMax;
      BoxOf(leftColumn.Select(handle => rects[handle.Actor]), 0.03f, out columnMin, out columnMax);
      int column = squadCommander.SelectInBox(columnMin, columnMax);
      Check(column == 2, $"a box around the left column selects two ({column})");
      Check(SameSet(squadCommander.selection, leftColumn),
        "which are exactly the actors it was drawn around");

      // 4. A box that covers only PART of an actor still selects it, and a box
      //    that merely SHARES an edge with one does too. Both are derived from
      //    the geometry, so neither can depend on exact distances.
      ViewportRect half = rects[squad[1].Actor];
      Vector2 halfMin = new Vector2(half.Min.x, half.Center.y);
      int partial = squadCommander.SelectInBox(halfMin, half.Max);
      List<S2MActorCommander.ActorHandle> crossers = squad
        .Where(handle => RectsCross(rects[handle.Actor], halfMin, half.Max)).ToList();
      Check(partial == 1, $"a box over one actor's top half selects it anyway ({partial})");
      Check(SameSet(squadCommander.selection, crossers)
            && crossers.Count == 1 && crossers[0] == squad[1],
        "and only it: half a body is enough, containment is not required");

      Vector2 sliverMin = new Vector2(half.Max.x, half.Min.y);
      Vector2 sliverMax = new Vector2(half.Max.x + 0.01f, half.Max.y);
      int sliver = squadCommander.SelectInBox(sliverMin, sliverMax);
      crossers = squad
        .Where(handle => RectsCross(rects[handle.Actor], sliverMin, sliverMax)).ToList();
      Check(sliver == 1, $"a box starting on an actor's right edge selects it ({sliver})");
      Check(SameSet(squadCommander.selection, crossers)
            && crossers.Count == 1 && crossers[0] == squad[1],
        "because sharing an edge counts as a crossing");

      // 5. A single click still picks one actor: the press/release split did not
      //    change what a click does when it does not drag.
      squadCommander.selection.Clear();
      Vector2 point = rects[squad[3].Actor].Center;
      GameObject clicked = squadCommander.PickAtViewport(point);
      Check(clicked == squad[3].Actor,
        $"a click on an actor's own centre still selects that one ({Name(clicked)})");
      Check(squadCommander.SelectionCount == 1,
        $"and the selection is a single actor ({squadCommander.SelectionCount})");
    }

    /// <summary>
    /// A box-selected squad of REAL placed actors is ordered by one order and
    /// every one of them drives to the ordered point, while every actor outside
    /// the box stays exactly where it was. This is the whole point of the box:
    /// <see cref="OrderSelectionTo"/> already iterated the selection, so a
    /// selection of N drivers must move N actors and not one more.
    /// </summary>
    private static void CheckSquadOrder(S2MActorCommander commander)
    {
      Line("--- squad order ---");

      List<S2MActorCommander.ActorHandle> roster = commander.Actors.ToList();
      Check(commander.ViewCamera != null,
        $"the map commander has a view camera ({commander.ViewCamera?.name ?? "(none)"})");
      if (commander.ViewCamera == null || roster.Count == 0) return;

      // A squad of real actors: the densest cluster on the map, box-selected
      // through the public box path on geometry derived here.
      int picked = SelectRealSquad(commander, out List<S2MActorCommander.ActorHandle> expected,
        out Vector3 centre);
      Check(picked >= 3, $"the box over the densest cluster selects it ({picked})");
      if (picked < 3) return;
      Vector3 at0 = expected[0].Actor.transform.position;

      // One order for the whole selection, on a point the whole cluster can
      // reach: the cluster's centre plus a small offset, so each walk is short.
      Vector3 destination = centre + new Vector3(OrderDistance * 0.5f, 0f, OrderDistance * 0.5f);
      var outsiders = roster
        .Where(handle => !expected.Contains(handle))
        .Select(handle => new { handle, Start = handle.Actor.transform.position })
        .ToList();

      commander.OrderSelectionTo(destination);

      Check(expected.All(handle => handle.Driver.Destination == destination),
        "every selected driver got the same ordered destination");
      int arrived = 0;
      foreach (S2MActorCommander.ActorHandle handle in expected)
      {
        for (int frame = 0; frame < MaxOrderFrames && handle.Driver.IsMoving; frame++)
          handle.Driver.Tick(Step);
        if (!handle.Driver.IsMoving
          && Mathf.Abs(handle.Actor.transform.position.x - destination.x) < 1e-3f
          && Mathf.Abs(handle.Actor.transform.position.z - destination.z) < 1e-3f)
          arrived++;
      }
      Check(arrived == expected.Count,
        $"all {expected.Count} box-selected actors walk to the ordered point "
        + $"({at0.x:F2}, {at0.y:F2}, {at0.z:F2}) cluster; {arrived} arrived");
      int drifted = outsiders.Count(entry =>
        entry.handle.Actor.transform.position != entry.Start);
      Check(drifted == 0,
        $"and none of the {outsiders.Count} actors outside the box moved ({drifted} did)");
    }

    /// <summary>
    /// The densest cluster of REAL actors on the roster, box-selected into the
    /// commander's selection: the box is the union of the cluster's projected
    /// rectangles and the expectation is whatever that box crosses, derived
    /// HERE from the geometry - a stray actor whose rectangle happens to cross
    /// the box is expected, not a failure of the box.
    /// </summary>
    /// <returns>How many actors the box selected, or -1 when the roster or the
    /// camera cannot carry the check.</returns>
    private static int SelectRealSquad(S2MActorCommander commander,
      out List<S2MActorCommander.ActorHandle> expected, out Vector3 centre)
    {
      expected = new List<S2MActorCommander.ActorHandle>();
      centre = Vector3.zero;
      List<S2MActorCommander.ActorHandle> roster = commander.Actors.ToList();
      if (commander.ViewCamera == null || roster.Count == 0) return -1;

      // The anchor is the actor with the most neighbours inside UnitBox metres,
      // because the ordered walk is short only if the box encloses one compact
      // group.
      List<S2MActorCommander.ActorHandle> squad = DensestCluster(roster, UnitBox);
      if (squad.Count < 3) return -1;

      var rects = new Dictionary<GameObject, ViewportRect>();
      foreach (S2MActorCommander.ActorHandle handle in roster)
      {
        if (handle.Renderer == null) continue;
        rects[handle.Actor] = RectOf(commander.ViewCamera, handle.Renderer.bounds);
      }
      Vector2 min, max;
      BoxOf(squad.Select(handle => rects[handle.Actor]), 0.02f, out min, out max);

      expected = roster
        .Where(handle => rects.ContainsKey(handle.Actor)
          && RectsCross(rects[handle.Actor], min, max)).ToList();
      centre = squad.Aggregate(Vector3.zero,
        (sum, handle) => sum + handle.Actor.transform.position) / squad.Count;

      int picked = commander.SelectInBox(min, max);
      Line($"squad        : {squad.Count} actor(s) within {UnitBox} m, box "
           + $"({min.x:F3}, {min.y:F3})-({max.x:F3}, {max.y:F3}) selects {picked} of "
           + $"{expected.Count} its geometry crosses");
      Check(SameSet(commander.selection, expected),
        "and the selection is exactly the actors the geometry says the box crosses");
      return picked;
    }

    /// <summary>
    /// The densest group of actors standing within <paramref name="radius"/>
    /// metres of each other: the actor with the most neighbours is the
    /// cluster's centre, and that cluster is the squad a player would box.
    /// </summary>
    private static List<S2MActorCommander.ActorHandle> DensestCluster(
      List<S2MActorCommander.ActorHandle> roster, float radius)
    {
      List<S2MActorCommander.ActorHandle> best = new List<S2MActorCommander.ActorHandle>();
      foreach (S2MActorCommander.ActorHandle handle in roster)
      {
        Vector3 at = handle.Actor.transform.position;
        List<S2MActorCommander.ActorHandle> nearby = roster
          .Where(other => Vector3.Distance(other.Actor.transform.position, at) <= radius)
          .ToList();
        if (nearby.Count > best.Count) best = nearby;
      }
      return best;
    }

    /// <summary>
    /// The Stop command (R010): the command box's Stop button halts the WHOLE
    /// selection where it stands - every actor keeps the position and facing it
    /// had at the press, lands in Idle on its next tick, and can be re-ordered
    /// straight away.
    ///
    /// A rig of five carries the semantics: four box-selected and ordered
    /// together, a fifth ordered on its own so there is an actor the command is
    /// NOT allowed to touch. Only a rig can say which actor the stop was
    /// supposed to leave alone. The end repeats it on the real roster, where a
    /// clip-less Ballista stands in for R010's "units or siege equipment".
    /// </summary>
    private static void CheckStop(S2MActorCommander mapCommander)
    {
      Line("--- stop ---");

      Camera camera = BuildPickCamera("S2M Pick Camera (stop)",
        new Vector3(0f, 6f, -14f), new Vector3(0f, 1f, 0f));
      var commander = BuildCommander("S2M Actor Commander (stop)",
        BuildRig("S2M Stop Rig (stop)",
          ("Pikeman 0", new Vector3(-2.5f, 0.5f, 0f)),
          ("Pikeman 1", new Vector3(-0.8f, 0.5f, 0f)),
          ("Pikeman 2", new Vector3(0.8f, 0.5f, 0f)),
          ("Pikeman 3", new Vector3(2.5f, 0.5f, 0f)),
          ("Pikeman 4", new Vector3(14f, 0.5f, 0f))), camera);
      List<S2MActorCommander.ActorHandle> rig = commander.Actors.ToList();
      Check(rig.Count == 5, $"the stop rig placed five actors ({rig.Count})");
      if (rig.Count != 5) return;

      // The button a player's click would hit. The rig stands clear of it, so
      // the click below really reaches the BUTTON rather than an actor (a pick
      // would select instead of stopping).
      Rect button = commander.StopButtonRect();
      Check(button.width > 0f && button.height > 0f
            && button.xMax <= Screen.width && button.yMax <= Screen.height,
        $"the command box's Stop button is on screen ({button.x:F0}, {button.y:F0}, "
        + $"{button.width:F0}x{button.height:F0} of {Screen.width}x{Screen.height})");
      GameObject underButton = commander.PickAtViewport(
        camera.ScreenToViewportPoint(button.center));
      Check(underButton == null,
        $"and no rig actor stands under it, so a click there is a button press "
        + $"({Name(underButton)})");

      // The squad: the four left-hand actors, box-selected through the public
      // box path, then ordered together; the fifth separately, so both are
      // walking when the stop comes.
      var rects = new Dictionary<GameObject, ViewportRect>();
      foreach (S2MActorCommander.ActorHandle handle in rig)
        rects[handle.Actor] = RectOf(camera, handle.Renderer.bounds);
      List<S2MActorCommander.ActorHandle> squad = rig.GetRange(0, 4);
      Vector2 min, max;
      BoxOf(squad.Select(handle => rects[handle.Actor]), 0.02f, out min, out max);
      int boxed = commander.SelectInBox(min, max);
      Check(boxed == 4 && SameSet(commander.selection, squad),
        $"a box over the four left-hand actors selects them ({boxed})");

      var starts = rig.ToDictionary(handle => handle.Actor,
        handle => handle.Actor.transform.position);
      Vector3 squadGoal = new Vector3(0f, 0.5f, 6f);
      commander.OrderSelectionTo(squadGoal);
      commander.selection.Clear();
      commander.selection.Add(rig[4]);
      Vector3 loneGoal = new Vector3(14f, 0.5f, 6f);
      commander.OrderSelectionTo(loneGoal);
      foreach (S2MActorCommander.ActorHandle handle in rig) DrivePart(handle, 0.4f);
      Check(rig.All(handle => handle.Driver.IsMoving),
        $"all five rig actors are walking before the stop "
        + $"({rig.Count(handle => handle.Driver.IsMoving)})");

      // The stop, through the exact click a player makes: the command box's
      // Stop button, in screen pixels.
      var frozen = rig.ToDictionary(handle => handle.Actor,
        handle => new
        {
          Position = handle.Actor.transform.position,
          Rotation = handle.Actor.transform.rotation,
          Walked = handle.Driver.DistanceWalked,
          Transitions = handle.Driver.TransitionCount,
        });
      commander.selection.Clear();
      commander.selection.AddRange(squad);

      commander.HandleClick(button.center);

      Check(squad.All(handle => !handle.Driver.IsMoving),
        $"the button click halts all four selected actors "
        + $"({squad.Count(handle => !handle.Driver.IsMoving)})");
      Check(rig[4].Driver.IsMoving,
        $"and leaves the actor outside the selection walking ({rig[4].Actor.name})");
      Check(squad.All(handle => !handle.Driver.IsMoving
              && Vector3.Distance(starts[handle.Actor], handle.Actor.transform.position) > 0f),
        "each having stopped away from where it started");

      // ONE TICK: the state a halt lands in is the Idle the driver's own Stop
      // documents, and that is the clip the actor plays from then on.
      foreach (S2MActorCommander.ActorHandle handle in rig) handle.Driver.Tick(Step);
      string landed = string.Join(", ",
        squad.Select(handle => handle.Driver.State.ToString()));
      Line($"halted       : states {landed}");
      Check(squad.All(handle => handle.Driver.State == ActorState.Idle),
        $"every stopped actor has landed in Idle one tick later "
        + $"({squad.Count(handle => handle.Driver.State == ActorState.Idle)} of {squad.Count})");

      // And it STAYS stopped: two more seconds of ticks move nothing, turn
      // nothing, re-clip nothing.
      for (int frame = 0; frame < 120; frame++)
        foreach (S2MActorCommander.ActorHandle handle in rig) handle.Driver.Tick(Step);

      int drifted = 0, turned = 0, rewalked = 0, retransitioned = 0;
      foreach (S2MActorCommander.ActorHandle handle in squad)
      {
        if (handle.Actor.transform.position != frozen[handle.Actor].Position) drifted++;
        if (handle.Actor.transform.rotation != frozen[handle.Actor].Rotation) turned++;
        if (handle.Driver.DistanceWalked != frozen[handle.Actor].Walked) rewalked++;
        if (handle.Driver.TransitionCount != frozen[handle.Actor].Transitions + 1) retransitioned++;
      }
      Line($"frozen       : {drifted} moved, {turned} turned, {rewalked} walked on, "
           + $"{retransitioned} with an unexpected transition count after 120 frames");
      Check(drifted == 0, $"every stopped actor keeps its position ({drifted} moved)");
      Check(turned == 0, $"and the facing it had when stopped ({turned} turned)");
      Check(rewalked == 0 && retransitioned == 0,
        "with no further travel and exactly one transition each (the landing in Idle)");

      // The stop is confined to the selection: with nothing selected it halts
      // nothing and the actor still walking walks on.
      Vector3 loneBefore = rig[4].Actor.transform.position;
      commander.selection.Clear();
      int haltedNothing = commander.StopSelection();
      foreach (S2MActorCommander.ActorHandle handle in rig) handle.Driver.Tick(Step);
      Check(haltedNothing == 0,
        $"a stop with an empty selection halts nothing ({haltedNothing})");
      Check(rig[4].Driver.IsMoving
            && rig[4].Actor.transform.position != loneBefore,
        "and the walking actor outside the selection walks on");

      // The button is a RECTANGLE, not "any click near the corner": a click one
      // pixel outside it is a world click, and the walk continues. (This scene
      // has no terrain, so such a click resolves no ground point and orders
      // nothing - what is checked is that it does not STOP.)
      commander.selection.Clear();
      commander.selection.AddRange(squad);
      commander.OrderSelectionTo(squadGoal);
      foreach (S2MActorCommander.ActorHandle handle in squad) DrivePart(handle, 0.4f);
      Vector3 destination = squad[0].Driver.Destination;
      commander.HandleClick(new Vector2(button.xMax + 1f, button.yMax + 1f));
      Check(squad.All(handle => handle.Driver.IsMoving
              && handle.Driver.Destination == destination),
        "a click one pixel outside the button does not stop the walk");

      // A STOPPED actor is not a frozen one: the same selection takes a new
      // order, walks it and arrives. This is also the direct entry point, so
      // the count it reports is checked here.
      int halted = commander.StopSelection();
      Check(halted == squad.Count,
        $"a stop mid-walk reports the {squad.Count} actors it halted ({halted})");
      foreach (S2MActorCommander.ActorHandle handle in squad) handle.Driver.Tick(Step);
      Check(squad.All(handle => handle.Driver.State == ActorState.Idle),
        "and they land in Idle again");
      commander.OrderSelectionTo(squadGoal);
      Check(squad.All(handle => handle.Driver.IsMoving),
        "a stopped actor still takes a new order");
      foreach (S2MActorCommander.ActorHandle handle in squad) DrivePart(handle, 1f);
      int arrived = squad.Count(handle => !handle.Driver.IsMoving
        && Mathf.Abs(handle.Actor.transform.position.z - squadGoal.z) < 1e-3f);
      Check(arrived == squad.Count,
        $"and all {squad.Count} walk the new order to its end ({arrived})");
      Check(squad.All(handle => handle.Driver.State == ActorState.Idle), "idle on arrival");

      // Idempotent: stopping actors that are not moving reports nothing halted
      // and adds no transition.
      int transitions = squad.Sum(handle => handle.Driver.TransitionCount);
      int idleHalt = commander.StopSelection();
      foreach (S2MActorCommander.ActorHandle handle in squad) handle.Driver.Tick(Step);
      Check(idleHalt == 0, $"a stop on an idle selection reports nothing halted ({idleHalt})");
      Check(squad.Sum(handle => handle.Driver.TransitionCount) == transitions,
        "and adds no state transition");

      CheckStopRealRoster(mapCommander);
    }

    /// <summary>
    /// The same command on the REAL roster: the densest cluster is box-selected,
    /// ordered and stopped mid-walk through the command box's button, while the
    /// actors outside the box are checked not to move at all - and a clip-less
    /// Ballista (siege equipment, R010's other half) halts too.
    /// </summary>
    private static void CheckStopRealRoster(S2MActorCommander commander)
    {
      Line("--- stop on the real roster ---");

      List<S2MActorCommander.ActorHandle> roster = commander.Actors.ToList();
      int picked = SelectRealSquad(commander, out List<S2MActorCommander.ActorHandle> squad,
        out Vector3 centre);
      Check(picked >= 3, $"the box over the densest cluster selects it ({picked})");
      if (picked < 3) return;

      var outsiders = roster
        .Where(handle => !squad.Contains(handle))
        .Select(handle => new { handle, Start = handle.Actor.transform.position })
        .ToList();
      Vector3 goal = centre + new Vector3(OrderDistance, 0f, OrderDistance);
      commander.OrderSelectionTo(goal);
      foreach (S2MActorCommander.ActorHandle handle in squad) DrivePart(handle, 0.4f);
      Check(squad.All(handle => handle.Driver.IsMoving),
        $"every one of the {squad.Count} selected actors is mid-walk "
        + $"({squad.Count(handle => handle.Driver.IsMoving)})");

      var stops = squad.ToDictionary(handle => handle.Actor,
        handle => handle.Actor.transform.position);
      commander.HandleClick(commander.StopButtonRect().center);
      Check(squad.All(handle => !handle.Driver.IsMoving),
        $"the button click halts the whole squad "
        + $"({squad.Count(handle => !handle.Driver.IsMoving)} of {squad.Count})");
      foreach (S2MActorCommander.ActorHandle handle in roster) handle.Driver.Tick(Step);
      Check(squad.All(handle => handle.Driver.State == ActorState.Idle),
        $"and every one lands in Idle ({squad.Count(handle => handle.Driver.State == ActorState.Idle)})");

      for (int frame = 0; frame < 120; frame++)
        foreach (S2MActorCommander.ActorHandle handle in roster) handle.Driver.Tick(Step);
      int drifted = squad.Count(handle =>
        handle.Actor.transform.position != stops[handle.Actor]);
      int strayed = outsiders.Count(entry => entry.handle.Actor.transform.position != entry.Start);
      Check(drifted == 0, $"none of the stopped actors moves afterwards ({drifted})");
      Check(strayed == 0,
        $"and none of the {outsiders.Count} actors outside the box moved ({strayed})");

      // Siege equipment: a Ballista has no clip at all, and must still halt
      // (R006's clipless rule on the new verb).
      S2MActorCommander.ActorHandle ballista = roster.Find(handle => handle.Type == "Ballista");
      Check(ballista != null, "a Ballista is on the roster for the siege-equipment stop");
      if (ballista == null) return;

      commander.selection.Clear();
      commander.selection.Add(ballista);
      Vector3 siegeStart = ballista.Actor.transform.position;
      commander.OrderSelectionTo(siegeStart + new Vector3(0f, 0f, OrderDistance));
      DrivePart(ballista, 0.4f);
      Check(ballista.Driver.IsMoving, "the ordered Ballista is rolling");
      int siegeHalted = commander.StopSelection();
      ballista.Driver.Tick(Step);
      Check(siegeHalted == 1 && !ballista.Driver.IsMoving
            && ballista.Driver.State == ActorState.Idle,
        $"and the clip-less Ballista halts into Idle like the rest "
        + $"(halted {siegeHalted}, state {ballista.Driver.State})");
      Vector3 siegeStop = ballista.Actor.transform.position;
      for (int frame = 0; frame < 120; frame++) ballista.Driver.Tick(Step);
      Check(ballista.Actor.transform.position == siegeStop,
        "keeping its position, having travelled "
        + $"{Vector3.Distance(siegeStart, siegeStop):F3} m of the {OrderDistance} m ordered");
    }

    /// <summary>
    /// One actor's viewport rectangle, derived from the projected bounds
    /// corners here so a squad check can be judged against the geometry. The
    /// behind-the-plane corners are dropped exactly as the commander drops
    /// them: WorldToViewportPoint mirrors a point behind the camera, so an
    /// actor straddling the near plane would otherwise own most of the
    /// viewport rectangle.
    /// </summary>
    private struct ViewportRect
    {
      public Vector2 Min;
      public Vector2 Max;
      public bool Valid;

      public Vector2 Center => (Min + Max) * 0.5f;
    }

    /// <summary>A world bounds projected to its viewport rectangle.</summary>
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

    /// <summary>Whether two viewport rectangles overlap at all - sharing an edge
    /// counts, which is what a drag box over an actor's outer edge does.</summary>
    private static bool RectsCross(ViewportRect rect, Vector2 min, Vector2 max) =>
      rect.Valid
      && !(rect.Max.x < min.x || rect.Min.x > max.x
        || rect.Max.y < min.y || rect.Min.y > max.y);

    /// <summary>The union of the given viewport rectangles, grown by
    /// <paramref name="margin"/> viewport units on every side.</summary>
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

    /// <summary>Whether the two sets hold the same handles, by actor.</summary>
    private static bool SameSet(List<S2MActorCommander.ActorHandle> got,
      IEnumerable<S2MActorCommander.ActorHandle> expected)
    {
      List<GameObject> want = expected.Select(handle => handle.Actor).ToList();
      return got.Count == want.Count
        && !got.Any(handle => !want.Contains(handle.Actor))
        && !want.Any(actor => got.All(handle => handle.Actor != actor));
    }

    /// <summary>
    /// A camera over the whole placed roster: batchmode has no Camera.main, and
    /// the pick's screen-to-viewport step needs a pixel size, so one stands in
    /// for the game view. It sits off to the side as well as above, because a
    /// camera looking straight down the up axis has no stable LookAt rotation.
    /// </summary>
    private static Camera BuildRosterCamera(string name, GameObject root)
    {
      bool any = false;
      var bounds = new Bounds();
      foreach (Transform group in root.transform)
      {
        foreach (Transform actor in group)
        {
          Renderer renderer = actor.GetComponentInChildren<Renderer>();
          if (renderer == null) continue;
          if (!any) { bounds = renderer.bounds; any = true; }
          else bounds.Encapsulate(renderer.bounds);
        }
      }

      var host = new GameObject(name);
      var camera = host.AddComponent<Camera>();
      camera.fieldOfView = 60f;
      camera.nearClipPlane = 0.5f;
      camera.farClipPlane = 5000f;
      if (any)
      {
        float height = Mathf.Max(bounds.extents.x, bounds.extents.z) * 3f + 50f;
        host.transform.position = bounds.center + new Vector3(height * 0.2f, height, 0f);
        host.transform.LookAt(bounds.center);
      }
      // Headless batchmode reports no display, and the screen-to-viewport step
      // divides by the camera's pixel size - a render texture stands in for one.
      if (camera.pixelWidth <= 0)
        camera.targetTexture = new RenderTexture(64, 64, 0);
      return camera;
    }

    /// <summary>
    /// Ticks one driver until it has covered <paramref name="progress"/> of the
    /// way from its position to its destination, or it arrives - the walk a
    /// Play-mode Update feeds, run on the fixed timestep instead.
    /// </summary>
    private static void DrivePart(S2MActorCommander.ActorHandle handle, float progress)
    {
      Vector3 from = handle.Actor.transform.position;
      float target = Vector3.Distance(from, handle.Driver.Destination) * progress;
      float walked = 0f;
      for (int frame = 0; frame < MaxOrderFrames && handle.Driver.IsMoving
        && walked < target; frame++)
      {
        Vector3 before = handle.Actor.transform.position;
        handle.Driver.Tick(Step);
        walked += Vector3.Distance(before, handle.Actor.transform.position);
      }
    }

    /// <summary>
    /// The order half (R002/R003/R004/R006): a ground destination reaches the
    /// verified driver, the facing snaps at issue time in the command path, a
    /// re-order redirects without stopping, and every guard issues nothing and
    /// throws nothing - reaching the next check at all is the no-throw proof.
    /// </summary>
    private static void CheckOrder(S2MActorCommander commander)
    {
      Line("--- ordering ---");

      List<S2MActorCommander.ActorHandle> roster = commander.Actors.ToList();
      S2MActorCommander.ActorHandle pikeman = roster.Find(handle => handle.Type == "Pikeman");
      Check(pikeman != null, $"a Pikeman is on the roster to order ({roster.Count})");
      if (pikeman == null) return;

      // The selection is a public list precisely so a harness can seed it
      // through the same seam a pick fills.
      commander.selection.Clear();
      commander.selection.Add(pikeman);

      Vector3 start = pikeman.Actor.transform.position;
      Vector3 destination = start + new Vector3(0f, 0f, OrderDistance);
      commander.OrderSelectionTo(destination);

      Check(pikeman.Driver.IsMoving, "the order puts the driver in motion");
      Check(pikeman.Driver.Destination == destination,
        $"and the driver's destination is the ordered point ({pikeman.Driver.Destination})");
      Vector3 heading = destination - start;
      heading.y = 0f;
      Check(pikeman.Actor.transform.rotation == Quaternion.LookRotation(heading),
        "the actor faces the order direction (the snap is in the command path)");

      // A re-order while walking replaces the destination, without stopping and
      // with the snap re-run. The click entry point itself (HandleClick) is
      // exercised by S2MActorClickRun on the real map; here the order is issued
      // straight through OrderSelectionTo.
      Vector3 retarget = start + new Vector3(OrderDistance * 0.75f, 0f, -OrderDistance * 0.5f);
      commander.OrderSelectionTo(retarget);
      Check(pikeman.Driver.Destination == retarget, "a re-order replaces the destination");
      Check(pikeman.Driver.IsMoving, "without stopping");
      Vector3 newHeading = retarget - start;
      newHeading.y = 0f;
      Check(pikeman.Actor.transform.rotation == Quaternion.LookRotation(newHeading),
        "and re-snaps the facing to the new heading");

      // The driver is the movement path: one manual tick at a time, exactly as
      // the state run drives it.
      for (int frame = 0; frame < MaxOrderFrames && pikeman.Driver.IsMoving; frame++)
        pikeman.Driver.Tick(Step);
      Vector3 end = pikeman.Actor.transform.position;
      float travelled = Vector3.Distance(
        new Vector3(end.x, start.y, end.z), new Vector3(start.x, start.y, start.z));
      float asked = Vector3.Distance(
        new Vector3(retarget.x, start.y, retarget.z), new Vector3(start.x, start.y, start.z));
      Line($"drove        : from ({start.x:F3}, {start.y:F3}, {start.z:F3}) to "
           + $"({end.x:F3}, {end.y:F3}, {end.z:F3}), {travelled:F3} m of {asked:F3} m asked");
      Check(!pikeman.Driver.IsMoving, "the ordered actor arrives");
      Check(Mathf.Abs(travelled - asked) < S2MActorStateDriver.DefaultSpeed * Step * 2f,
        $"having walked the ordered {asked:F3} m ({travelled:F3} m)");
      Check(pikeman.Driver.State == ActorState.Idle, "and is back in idle on arrival");
      Check(Mathf.Abs(end.y - start.y) < 1e-4f, "with the standing height untouched");

      // Guards. An empty selection issues nothing...
      commander.selection.Clear();
      Vector3 before = pikeman.Actor.transform.position;
      commander.OrderSelectionTo(destination);
      Check(pikeman.Actor.transform.position == before && pikeman.Driver.Destination == retarget,
        "an order with an empty selection issues nothing");

      // ...and a ray that hits no terrain mesh resolves no point and throws
      // nothing.
      Check(!commander.TryGetGroundPoint(new Vector2(5000f, 5000f), out Vector3 ground),
        "a ray that hits no terrain mesh returns false");
      Check(ground == default(Vector3), $"and resolves no ground point ({ground})");

      // A type with no clip at all still translates, with the state set and the
      // reason recorded - the driver's documented rule (R006).
      S2MActorCommander.ActorHandle ballista = roster.Find(handle => handle.Type == "Ballista");
      if (ballista != null)
      {
        commander.selection.Clear();
        commander.selection.Add(ballista);
        Vector3 siegeStart = ballista.Actor.transform.position;
        Vector3 siegeTo = siegeStart + new Vector3(0f, 0f, -OrderDistance);
        commander.OrderSelectionTo(siegeTo);
        ballista.Driver.Tick(Step);
        Check(ballista.Driver.State == ActorState.Walk,
          $"the clip-less ballista's state is walking ({ballista.Driver.State})");
        Check(ballista.Driver.LastReason != null,
          $"and it records why it has no clip ({ballista.Driver.LastReason ?? "none"})");
        for (int frame = 0; frame < MaxOrderFrames && ballista.Driver.IsMoving; frame++)
          ballista.Driver.Tick(Step);
        Vector3 siegeEnd = ballista.Actor.transform.position;
        float siegeTravelled = Vector3.Distance(
          new Vector3(siegeEnd.x, siegeStart.y, siegeEnd.z),
          new Vector3(siegeStart.x, siegeStart.y, siegeStart.z));
        Check(!ballista.Driver.IsMoving && Mathf.Abs(siegeTravelled - OrderDistance) < 0.1f,
          $"yet it still translates the ordered {OrderDistance} m ({siegeTravelled:F3} m)");
      }
      else
      {
        Check(false, "a Ballista is on the roster for the clip-less control");
      }
    }

    /// <summary>A perspective camera for the pick math, with a pixel size.</summary>
    private static Camera BuildPickCamera(string name, Vector3 position, Vector3 target)
    {
      var host = new GameObject(name);
      var camera = host.AddComponent<Camera>();
      camera.fieldOfView = 60f;
      camera.nearClipPlane = 0.05f;
      camera.farClipPlane = 500f;
      host.transform.position = position;
      host.transform.LookAt(target);
      // Headless batchmode reports no display, and the screen-to-viewport step
      // divides by the camera's pixel size - a render texture stands in for one.
      if (camera.pixelWidth <= 0)
        camera.targetTexture = new RenderTexture(64, 64, 0);
      return camera;
    }

    /// <summary>
    /// One rig root in the placement pipeline's two-level shape: a group, with
    /// one small meshed actor per record under it. No collider - the only
    /// collider in the scene must stay the terrain's, or the ground-raycast
    /// guard could not be tested.
    /// </summary>
    private static GameObject BuildRig(string rootName,
      params (string name, Vector3 position)[] actors)
    {
      var root = new GameObject(rootName);
      var group = new GameObject("Pikeman");
      group.transform.SetParent(root.transform, true);
      Mesh cube = RigCube();
      foreach ((string name, Vector3 position) actor in actors)
      {
        var go = new GameObject(actor.name);
        go.transform.SetParent(group.transform, true);
        go.transform.position = actor.position;
        go.AddComponent<MeshFilter>().sharedMesh = cube;
        go.AddComponent<MeshRenderer>();
      }
      return root;
    }

    /// <summary>A 0.5 m cube mesh - the rig actors' renderer bounds.</summary>
    private static Mesh RigCube()
    {
      float h = 0.25f;
      var mesh = new Mesh
      {
        vertices = new[]
        {
          new Vector3(-h, -h, -h), new Vector3(h, -h, -h),
          new Vector3(h, h, -h), new Vector3(-h, h, -h),
          new Vector3(-h, -h, h), new Vector3(h, -h, h),
          new Vector3(h, h, h), new Vector3(-h, h, h),
        },
        triangles = new[]
        {
          0, 1, 2, 0, 2, 3, 1, 5, 6, 1, 6, 2, 5, 4, 7, 5, 7, 6,
          4, 0, 3, 4, 3, 7, 4, 5, 1, 4, 1, 0, 3, 2, 6, 3, 6, 7,
        },
      };
      mesh.RecalculateBounds();
      return mesh;
    }

    private static S2MActorCommander BuildCommander(string hostName, GameObject root,
      Camera camera)
    {
      var host = new GameObject(hostName);
      var commander = host.AddComponent<S2MActorCommander>();
      commander.Initialize(root, GameRoot, camera);
      return commander;
    }

    private static int cheapestSelection(S2MActorCommander commander) => commander.SelectionCount;

    /// <summary>WorldToViewportPoint's xy, as a Vector2 - its z is the depth.</summary>
    private static Vector2 ViewportPointOf(Camera camera, Vector3 world)
    {
      Vector3 point = camera.WorldToViewportPoint(world);
      return new Vector2(point.x, point.y);
    }

    /// <summary>A viewport point that is a real number inside the 0..1 square.</summary>
    private static bool OnScreen(Vector2 viewport) =>
      IsReal(viewport)
      && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

    /// <summary>A viewport point that is a number at all - a 0x0 headless camera
    /// yields NaN, which would silently pass every comparison.</summary>
    private static bool IsReal(Vector2 viewport) =>
      !float.IsNaN(viewport.x) && !float.IsNaN(viewport.y)
      && !float.IsInfinity(viewport.x) && !float.IsInfinity(viewport.y);

    private static string Name(GameObject actor) => actor != null ? actor.name : "(none)";

    /// <summary>
    /// The actor count is the count of grandchildren: the loader builds one
    /// GameObject per actor under one group per model, and an actor may carry
    /// several child meshes that must not be counted.
    /// </summary>
    private static int CountActorGrandchildren(GameObject root)
    {
      if (root == null) return 0;
      int count = 0;
      foreach (Transform group in root.transform)
        count += group.Cast<Transform>().Count(actor =>
          actor.gameObject.name.IndexOf(' ') >= 0);
      return count;
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
