using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode check that something in the GAME can drive an actor's state -
  /// the piece the per-state clip table was missing:
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MActorStateRun.Main -quit
  ///
  /// The state table says WHICH clip a type plays in each state; nothing ever SET
  /// a state, because a .s2m writes every actor at rest. This run walks actors on
  /// war_chapter1 with <see cref="S2MActorStateDriver"/> and reports what the
  /// drive actually did, state change by state change:
  ///
  ///   1. the transitions    - idle -> walking -> idle, with the frame each happened
  ///                           on and the frames spent in each state (so a walk
  ///                           that switched clip every frame would show up as
  ///                           hundreds of transitions);
  ///   2. WHICH clip         - while WALKING the clip must be the file the actor's
  ///                           OWN catalogue row names, not the idle it arrived
  ///                           with;
  ///   3. clip QUALITY       - a walk clip must swing legs (mean leg angular speed
  ///                           over the clip's own keyframes) and an idle must
  ///                           not, and neither may carry root motion;
  ///   4. the world moved    - the actor's world displacement must equal the
  ///                           distance asked for, on y untouched, because the
  ///                           clips are in-place cycles;
  ///   5. the negative types - a type whose list names no WALK (the archer) still
  ///                           moves and still keeps the clip it had, and a type
  ///                           with no clip at all (the ballista) must not throw.
  /// </summary>
  public static class S2MActorStateRun
  {
    public static string GameRoot => Constants.GamePath;

    private static string MapJson =>
      Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter1.json");

    /// <summary>Fixed timestep, so a drive is repeatable frame for frame.</summary>
    private const float Step = 1f / 60f;

    /// <summary>How far the driven actors walk, metres.</summary>
    private const float WalkDistance = 6f;

    /// <summary>Ceiling on the frame loop (6 m at 1.4 m/s is 257 frames).</summary>
    private const int MaxFrames = 900;

    /// <summary>
    /// Mean leg angular speed that separates a locomotion clip from an idle, in
    /// degrees/second, measured over the whole 103-clip state catalogue: the
    /// walks read 48..204 and the idles 6..22, so a walk must clear 40 and an
    /// idle stay under 30.
    /// </summary>
    private const float LocomotionLegSpeed = 40f;
    private const float IdleLegSpeed = 30f;

    /// <summary>Largest net root drift any shipped clip shows, metres per
    /// loop (the archer's run, at 0.045).</summary>
    private const float MaxRootDrift = 0.06f;

    private static readonly StringBuilder Report = new();
    private static int failures;

    public static void Main()
    {
      string log = Path.Combine(Path.GetTempPath(), "sh2_actor_state_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MActorStateRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MActorStateRun]\n" + Report.ToString().TrimEnd());
    }

    /// <summary>The driver as it stood at one state change.</summary>
    private readonly struct Snapshot
    {
      public readonly int Frame;
      public readonly ActorState From;
      public readonly ActorState To;
      /// <summary>The clip the driver attached, or null when it attached none.</summary>
      public readonly string DriverClip;
      /// <summary>The clip the actor's Animation is playing.</summary>
      public readonly string PlayingClip;
      public readonly int Attaches;
      public readonly string Reason;

      public Snapshot(int frame, ActorState from, ActorState to, S2MActorStateDriver driver,
        Animation animation)
      {
        Frame = frame;
        From = from;
        To = to;
        DriverClip = Short(driver.ClipPath);
        PlayingClip = animation == null ? null : S2MActorAnimation.StateName(animation);
        Attaches = driver.AttachCount;
        Reason = driver.LastReason;
      }

      public override string ToString() =>
        $"{From}->{To} @frame {Frame}: clip {DriverClip ?? "-"}, playing "
        + $"'{PlayingClip ?? "-"}', {Attaches} attach(es)"
        + (Reason == null ? "" : $", reason: {Reason}");
    }

    private static void Run()
    {
      Line($"entities     : {Path.GetFileName(MapJson)} (drive: {S2MActorStateDriver.DefaultSpeed} m/s, "
           + $"{Step * 1000:F2} ms/frame, {WalkDistance} m)");
      S2MPlacementHarness.DestroyAll();
      GameObject root = S2MPlacementHarness.LoadEntities(MapJson, GameRoot);
      var actors = new List<Transform>();
      foreach (Transform group in root.transform) actors.AddRange(group.Cast<Transform>().ToList());
      Line($"placed       : {actors.Count} actors");
      Line("");

      // The positive case: a type with BOTH an idle and a walk clip, 47
      // instances on this map.
      Drive(actors, "Pikeman");
      // The negative cases on the same map:
      //  - the archer's own list names no WALK, so the driver must not invent one;
      //  - the ballista owns no clip at all, so the driver must not throw.
      Drive(actors, "Archer", expectWalkClip: false);
      Drive(actors, "Ballista", expectWalkClip: false, expectNoClip: true);
    }

    /// <summary>
    /// Drives the first actor of <paramref name="type"/> from its spawn point to
    /// a point <see cref="WalkDistance"/> metres east, one tick at a time, and
    /// checks everything the drive is supposed to prove.
    /// </summary>
    private static void Drive(List<Transform> actors, string type,
      bool expectWalkClip = true, bool expectNoClip = false)
    {
      Line($"--- driving {type} ---");
      Transform actor = actors.Find(a => a.name.StartsWith(type + " ", StringComparison.Ordinal));
      if (actor == null)
      {
        Check(false, $"a {type} is placed on this map to drive");
        Line("");
        return;
      }

      string walkStem = Short(ActorStateTable.ClipPathFor(type, ActorState.Walk));
      string idleStem = Short(ActorStateTable.ClipPathFor(type, ActorState.Idle));
      Line($"catalogue    : idle={idleStem} walk={walkStem}");

      S2MActorStateDriver driver = actor.gameObject.AddComponent<S2MActorStateDriver>();
      driver.ActorType = type;
      driver.GameInstallPath = GameRoot;
      driver.Speed = S2MActorStateDriver.DefaultSpeed;

      Vector3 start = actor.position;
      Vector3 destination = start + new Vector3(WalkDistance, 0f, 0f);
      driver.WalkTo(destination);

      // Walk it, snapshotting the driver at every state change: which clip it
      // attached, which clip the actor is PLAYING, what it attached and why it
      // might not have. Reading those after the drive would only ever see the
      // idle it arrived back into.
      var snapshots = new List<Snapshot>();
      Animation animation = actor.GetComponent<Animation>();
      ActorState previous = driver.State;
      int framesToWalk = 0;
      string midWalk = "(never recorded)";
      float midFrame = WalkDistance / S2MActorStateDriver.DefaultSpeed / Step * 0.5f;
      for (int frame = 1; frame <= MaxFrames; frame++)
      {
        driver.Tick(Step);
        framesToWalk = frame;
        if (frame == (int)midFrame)
        {
          string playing = animation == null ? "-" : S2MActorAnimation.StateName(animation);
          midWalk = $"frame {frame}, state {driver.State}, {driver.FramesInState} frames in it, "
            + $"playing '{playing}', still moving {driver.IsMoving}";
        }
        if (driver.State != previous)
        {
          snapshots.Add(new Snapshot(frame, previous, driver.State, driver, animation));
          previous = driver.State;
        }
        if (!driver.IsMoving) break;
      }
      if (driver.IsMoving)
      {
        snapshots.Add(new Snapshot(framesToWalk, previous, driver.State, driver, animation));
        Check(false, $"the driven {type} arrives at its destination");
      }

      Line($"mid-walk     : {midWalk}");
      foreach (Snapshot snapshot in snapshots) Line($"transition    : {snapshot}");
      Line($"state        : {driver.State}, {driver.TotalFrames} frames, "
           + $"{driver.TransitionCount} transitions, {driver.AttachCount} clip attaches, "
           + $"reason={(driver.LastReason ?? "none")}");

      // 1. the transitions: exactly walk then idle, in that order.
      Check(driver.TransitionCount == 2,
        $"the driven {type} makes exactly two transitions ({driver.TransitionCount})");
      Check(snapshots.Count == 2
            && snapshots[0].From == ActorState.Idle && snapshots[0].To == ActorState.Walk
            && snapshots[1].From == ActorState.Walk && snapshots[1].To == ActorState.Idle,
        $"the transitions are idle->walking then walking->idle ({snapshots.Count} recorded)");

      // The walk must last as long as the distance at the speed implies: a frame
      // loop that switched state on a timer rather than on arrival would show up
      // here as a mismatch, and the frames spent in the state are the walk's own
      // length.
      float expectedFrames = WalkDistance / S2MActorStateDriver.DefaultSpeed / Step;
      Line($"timing       : walked for {framesToWalk} frames, "
           + $"{snapshots[0].Frame} frame(s) in idle and {snapshots[1].Frame - snapshots[0].Frame} "
           + $"in walking, {expectedFrames:F0} expected at {S2MActorStateDriver.DefaultSpeed} m/s");
      Check(Math.Abs(framesToWalk - expectedFrames) <= 2f,
        $"the drive takes {expectedFrames:F0} frames, not {framesToWalk}");

      // 2. one clip attach per transition: a per-frame re-clip would rebuild an
      //    AnimationClip hundreds of times over the same walk.
      Check(driver.AttachCount <= driver.TransitionCount,
        $"the clip is attached at most once per transition ({driver.AttachCount} for "
        + $"{driver.TransitionCount})");

      // 3. WHICH clip. While WALKING the actor must play the file its own
      //    catalogue row names; on arrival it must be back on its idle.
      if (expectNoClip)
      {
        Check(snapshots.TrueForAll(s => s.DriverClip == null),
          $"a type with no clip at all never gets one ({string.Join("; ", snapshots)})");
        Check(snapshots.TrueForAll(s => s.Attaches == 0),
          $"and never attaches one ({string.Join("; ", snapshots)})");
        Check(snapshots.TrueForAll(s => s.PlayingClip == null),
          $"so nothing plays ({string.Join("; ", snapshots)})");
        Check(actor.GetComponent<Animation>() == null,
          "the clip-less actor carries no Animation component");
      }
      else if (expectWalkClip)
      {
        Check(snapshots[0].DriverClip == walkStem,
          $"the walking clip is {type}'s own walk row ({snapshots[0].DriverClip} vs {walkStem})");
        Check(snapshots[0].PlayingClip == walkStem,
          $"and that is what plays while walking ('{snapshots[0].PlayingClip}')");
        Check(snapshots[0].Reason == null,
          $"with no fallback reason ({snapshots[0].Reason ?? "none"})");
        Check(snapshots[1].DriverClip == idleStem,
          $"the arrival clip is the type's idle row ({snapshots[1].DriverClip} vs {idleStem})");
        Check(actor.GetComponent<Animation>().wrapMode == WrapMode.Loop, "the clip loops");
      }
      else
      {
        // A type whose own list names no WALK: the state still changes (the actor
        // IS walking), no clip is switched to, and the reason names the type.
        Check(snapshots[0].DriverClip == null && snapshots[0].Attaches == 0,
          $"the walk attached no clip ({snapshots[0]})");
        Check(snapshots[0].Reason != null && snapshots[0].Reason.Contains(type),
          $"and says why ({snapshots[0].Reason})");
        Check(snapshots[0].PlayingClip == idleStem,
          $"the actor keeps the clip it arrived with ('{snapshots[0].PlayingClip}')");
        Check(snapshots[1].DriverClip == idleStem,
          $"and picks its own idle row back up on arrival ({snapshots[1].DriverClip})");
      }

      // 4. the world moved, and only in x/z.
      Vector3 end = actor.position;
      float travelled = Vector3.Distance(new Vector3(end.x, start.y, end.z),
        new Vector3(start.x, start.y, start.z));
      Line($"world        : from ({start.x:F3}, {start.y:F3}, {start.z:F3}) to "
           + $"({end.x:F3}, {end.y:F3}, {end.z:F3}), {travelled:F3} m walked");
      Check(Math.Abs(travelled - WalkDistance) < S2MActorStateDriver.DefaultSpeed * Step * 2f,
        $"the actor walked the asked-for {WalkDistance} m ({travelled:F3} m)");
      Check(Math.Abs(end.y - start.y) < 1e-4f,
        $"the drive leaves the standing height alone ({end.y:F3} vs {start.y:F3})");

      // 5. clip QUALITY off the clip's own keyframes.
      if (!expectNoClip)
        ReportClipQuality(type, idleStem, walkStem, actor, expectWalkClip);

      Line("");
    }

    /// <summary>
    /// The two numbers that tell a walk clip from an idle one without playing
    /// either: leg angular speed (a walk swings legs, an idle does not) and root
    /// drift (neither carries root motion, so the drive's translation is the
    /// only thing that moves the actor through the world).
    /// </summary>
    private static void ReportClipQuality(string type, string idleStem, string walkStem,
      Transform actor, bool hasWalk)
    {
      SkinnedMeshRenderer renderer = actor.GetComponentInChildren<SkinnedMeshRenderer>();
      string rootBone = renderer != null && renderer.rootBone != null
        ? renderer.rootBone.name : null;
      if (rootBone == null)
      {
        Line($"clip quality : no skeleton root on {actor.name}");
        return;
      }

      float idleLegs = 0f, walkLegs = 0f, idleDrift = 0f, walkDrift = 0f;
      float idleLength = 0f, walkLength = 0f;
      if (ActorStateTable.TryLoad(type, ActorState.Idle, GameRoot, out Granny2AnimData idle, out _))
      {
        idleLegs = S2MActorClipMetrics.LegAngularSpeedPerSecond(idle);
        idleDrift = S2MActorClipMetrics.RootDriftPerLoop(idle, rootBone, 0.01f);
        idleLength = idle.Duration;
      }
      if (hasWalk && ActorStateTable.TryLoad(type, ActorState.Walk, GameRoot,
        out Granny2AnimData walk, out _))
      {
        walkLegs = S2MActorClipMetrics.LegAngularSpeedPerSecond(walk);
        walkDrift = S2MActorClipMetrics.RootDriftPerLoop(walk, rootBone, 0.01f);
        walkLength = walk.Duration;
      }
      Line($"clip quality : idle {idleStem} {idleLength:F2} s "
           + $"legs {idleLegs:F1} deg/s, root drift {idleDrift:F3} m/loop; "
           + $"walk {walkStem} {walkLength:F2} s "
           + $"legs {walkLegs:F1} deg/s, root drift {walkDrift:F3} m/loop");

      if (hasWalk)
      {
        Check(walkLegs > LocomotionLegSpeed,
          $"the walk clip swings legs faster than {LocomotionLegSpeed} deg/s "
          + $"({walkLegs:F1})");
        Check(walkLegs > idleLegs * 2f,
          $"and faster than the type's own idle clip ({walkLegs:F1} vs {idleLegs:F1} deg/s)");
        Check(idleLegs < IdleLegSpeed,
          $"the idle clip stays under {IdleLegSpeed} deg/s ({idleLegs:F1})");
        Check(walkDrift <= MaxRootDrift && idleDrift <= MaxRootDrift,
          $"neither clip carries root motion (walk {walkDrift:F3} m/loop, "
          + $"idle {idleDrift:F3} m/loop, cap {MaxRootDrift})");
      }
    }

    private static string Short(string path) =>
      string.IsNullOrEmpty(path) ? null : Path.GetFileNameWithoutExtension(path);

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
