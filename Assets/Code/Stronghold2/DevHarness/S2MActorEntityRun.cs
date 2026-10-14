using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode check of the ENTITY path with animation on, i.e.
  /// <see cref="EntityPlacementLoader"/> as the map loader actually calls it:
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MActorEntityRun.Main -quit
  ///
  /// The skinned actors moved from the static renderer (frozen at bind pose, no
  /// bones) to <see cref="SkinnedGranny2Renderer"/> per instance, so the counts
  /// that were verified back then have to still hold: every entity placed, no
  /// model skipped, the decoded position/rot applied. On top of that each actor
  /// must now play the clip its OWN type's animation list resolves to, so the
  /// report lists the distinct (clip name, duration) pairs actually playing:
  /// several different clips with several different durations is the proof that
  /// the actors are not all sharing one global clip.
  /// </summary>
  public static class S2MActorEntityRun
  {
    public static string GameRoot => Constants.GamePath;

    private static readonly StringBuilder Report = new();
    private static int failures;

    public static void Main()
    {
      string log = Path.Combine(Path.GetTempPath(), "sh2_actor_entity_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MActorEntityRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MActorEntityRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      foreach (string[] map in new[] {
          new[] { Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter1.json"), "166" },
          new[] { Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter8.json"), "340" },
          new[] { Path.Combine(Constants.PortRoot, "artifacts", "entities_fenis.json"), "613" } })
      {
        RunMap(map[0], int.Parse(map[1]));
      }
      RunStates();
      RunCatalogue();
    }

    /// <summary>
    /// The negative controls for the state path: without them, "the actor plays
    /// the clip its catalogue names" could just mean the lookup always returns
    /// the clip it already had.
    ///
    /// 1. an empty table must leave an actor clip-less rather than inventing one;
    /// 2. a table row naming a cache that does not exist must fail loudly;
    /// 3. clipping an actor with NO bones must not throw.
    /// </summary>
    private static void RunStateNegatives()
    {
      Line("--- state negative controls ---");
      // 1. a type the table has never heard of: no row, so no clip.
      Check(!ActorStateTable.TryLoad("NoSuchType", ActorState.Run, GameRoot,
        out Granny2AnimData anim, out string reason),
        $"an unknown type has no run clip ({reason})");
      Check(anim == null, "the unknown-type lookup returns no clip data");

      // 2. a table row naming a cache the host never built: the loader must say
      //    so instead of silently playing the previous clip.
      var missing = new[]
      {
        new ActorStateClipRow { type = "Archer", state = "run", anim_name = "GHOST", clip = "meshes/units/animations/soldier/no_such_clip.GR2" }
      };
      ActorStateTable.Reset(missing);
      Check(!ActorStateTable.TryLoad("Archer", ActorState.Run, GameRoot,
        out Granny2AnimData ghost, out string ghostReason),
        $"a clip whose cache is missing reports why ({ghostReason})");
      Check(ghost == null, "the missing-cache lookup returns no clip data");

      // 3. the actor with no bones at all (the non-skinned path): AttachState
      //    must return null, not throw.
      var bare = new GameObject("Bare Actor");
      S2MActorAnimation.Attached attached = S2MActorAnimation.AttachState(bare,
        Granny2AnimData.Empty(GameRoot));
      Check(attached.Animation == null, "an actor with no bones gets no Animation component");
      UnityEngine.Object.DestroyImmediate(bare);
      Line("");
    }

    /// <summary>
    /// The per-state clip selection: an actor switched to WALK/RUN/ATTACK plays
    /// the clip ITS OWN catalogue row names, and a type with no row for a state
    /// keeps the clip it had (idle, else its rest pose).
    ///
    /// Done on the same map the placement counts are verified on
    /// (war_chapter1), switching every placed actor in turn, so the check covers
    /// the real rig mix of a map - 7 models over 166 actors - and not one
    /// hand-picked actor. The estate id is NOT used as the state: a saved map
    /// writes every actor at rest, so the state is set here the way the game
    /// would set it when it gives orders.
    /// </summary>
    private static void RunStates()
    {
      // The negative controls first: they reset the table, and the real map load
      // rebuilds it from its own JSON.
      RunStateNegatives();

      string json = Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter1.json");
      Line("--- per-state clip selection (war_chapter1) ---");
      Line($"entities     : {Path.GetFileName(json)}");
      S2MPlacementHarness.DestroyAll();
      GameObject root = S2MPlacementHarness.LoadEntities(json, GameRoot);
      var actors = new List<Transform>();
      foreach (Transform group in root.transform) actors.AddRange(group.Cast<Transform>().ToList());
      Line($"placed       : {actors.Count} actors");

      var states = new[] { ActorState.Walk, ActorState.Run, ActorState.Attack };
      var clipped = new Dictionary<ActorState, int>();
      var keptIdle = new Dictionary<ActorState, int>();
      var noRow = new Dictionary<ActorState, int>();
      var switching = new Dictionary<ActorState, List<string>>();
      foreach (ActorState state in states)
      {
        clipped[state] = 0;
        keptIdle[state] = 0;
        noRow[state] = 0;
        switching[state] = new List<string>();
      }

      foreach (Transform actor in actors)
      {
        string type = actor.name.Split(' ')[0];
        Animation animation = actor.GetComponent<Animation>();
        foreach (ActorState state in states)
        {
          if (!ActorStateTable.TryLoad(type, state, GameRoot, out Granny2AnimData anim, out string reason))
          {
            // No clip for this state on this type: the actor must keep whatever
            // it was playing (its idle clip, or nothing at all).
            noRow[state]++;
            if (animation != null) keptIdle[state]++;
            continue;
          }
          S2MActorAnimation.Attached attached =
            S2MActorAnimation.AttachState(actor.gameObject, anim);
          if (attached.Animation == null)
          {
            Line($"FAIL         : {actor.name} could not bind its {state} clip "
                 + $"'{Path.GetFileNameWithoutExtension(anim.SourcePath)}' "
                 + $"({attached.AppliedTracks} tracks applied)");
            failures++;
            continue;
          }
          clipped[state]++;
          string name = S2MActorAnimation.StateName(attached.Animation);
          if (switching[state].Count < 6)
            switching[state].Add($"{type}:{name} ({attached.Animation[name].clip.length:0.##} s, "
                                 + $"{attached.AppliedTracks} curves)");
        }
      }

      foreach (ActorState state in states)
      {
        Line($"{state,-11}: {clipped[state]} of {actors.Count} actors re-clipped to their "
             + $"{state} clip; {noRow[state]} actors' types have no {state} clip "
             + $"({keptIdle[state]} of them kept the clip they had)");
        foreach (string entry in switching[state]) Line($"              {entry}");
      }
      Check(clipped[ActorState.Walk] > 0, $"at least one actor walks ({clipped[ActorState.Walk]})");
      Check(clipped[ActorState.Run] > 0, $"at least one actor runs ({clipped[ActorState.Run]})");
      Check(clipped[ActorState.Attack] > 0, $"at least one actor attacks ({clipped[ActorState.Attack]})");
      Check(noRow[ActorState.Walk] > 0,
        "some types have no walk clip (the archer's list names none) and fall back");

      // A moving actor must play a DIFFERENT clip from an idle one: same clip =
      // the state lookup silently returned the idle row. The loop above left
      // every actor in its last switchable state, so the archer is put back to
      // Idle before the comparison - otherwise it would compare against
      // whatever the loop switched it to last.
      Transform archer = actors.Find(a => a.name.StartsWith("Archer ", StringComparison.Ordinal));
      if (archer != null)
      {
        Animation animation = archer.GetComponent<Animation>();
        string idle = null;
        if (ActorStateTable.TryLoad("Archer", ActorState.Idle, GameRoot, out Granny2AnimData idleAnim, out _))
        {
          S2MActorAnimation.Attached back = S2MActorAnimation.AttachState(archer.gameObject, idleAnim);
          idle = S2MActorAnimation.StateName(back.Animation);
        }
        if (ActorStateTable.TryLoad("Archer", ActorState.Run, GameRoot, out Granny2AnimData run, out _))
        {
          S2MActorAnimation.Attached attached = S2MActorAnimation.AttachState(archer.gameObject, run);
          string runName = S2MActorAnimation.StateName(attached.Animation);
          Check(idle != null && idle != runName,
            $"the archer's run clip differs from its idle clip ('{idle}' -> '{runName}')");
          Line($"archer       : idle '{idle}' {animation[idle].clip.length:0.##} s vs run '{runName}' "
               + $"{animation[runName].clip.length:0.##} s, "
               + $"{(animation[idle].clip.length - animation[runName].clip.length):+0.##;-#.##} s");
        }
        else
        {
          Check(false, "the archer has a run clip in its catalogue");
        }
      }
      // The per-type coverage table: which clip each type on the map plays in
      // each state, straight off the catalogue. The gaps are shipped data (the
      // archer's list names no WALK; the four RUN SWORDSMAN types' run file is
      // a 1-track "Cylinder26" clip) and an actor with a gap keeps the clip it
      // had - its rest pose when it had none.
      var types = new SortedSet<string>(StringComparer.Ordinal);
      foreach (Transform actor in actors) types.Add(actor.name.Split(' ')[0]);
      var typeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (Transform actor in actors)
      {
        string t = actor.name.Split(' ')[0];
        typeCounts[t] = typeCounts.TryGetValue(t, out int c) ? c + 1 : 1;
      }
      Line("state table  : per type on this map (- = no clip for that state)");
      foreach (string t in types)
      {
        IReadOnlyDictionary<ActorState, S2MActorAnimation.StateClip> perType = ActorStateTable.ClipsFor(t);
        var cells = new List<string>();
        foreach (ActorState state in new[] { ActorState.Idle, ActorState.Walk, ActorState.Run, ActorState.Attack })
        {
          string clip = perType.TryGetValue(state, out S2MActorAnimation.StateClip sc)
            ? Path.GetFileNameWithoutExtension(sc.ClipPath) ?? "-"
            : "-";
          cells.Add($"{state.ToString().ToLowerInvariant()}={clip}");
        }
        Line($"              {t,-14} {typeCounts[t],4}  {string.Join("  ", cells)}");
      }
      Line("");
    }

    /// <summary>
    /// One actor per catalogue type (entity_parse.py --types-json): the
    /// done-criterion is that EVERY type that owns a clip plays it, and no
    /// single shipped map places all 47 actor types, so the catalogue gets its
    /// own synthetic map.
    /// </summary>
    private static void RunCatalogue()
    {
      string json = Path.Combine(Constants.PortRoot, "artifacts", "entities_all_types.json");
      Line($"entities     : {Path.GetFileName(json)} (one actor per catalogue type)");
      S2MPlacementHarness.DestroyAll();
      GameObject root = S2MPlacementHarness.LoadEntities(json, GameRoot);
      var actors = new List<Transform>();
      foreach (Transform group in root.transform) actors.AddRange(group.Cast<Transform>().ToList());
      int withClip = ReadClipRecords(json).Count;
      var animated = actors.FindAll(a => a.GetComponent<Animation>() != null);
      Line($"placed       : {actors.Count} actor types, {withClip} own a clip, "
           + $"{animated.Count} animate");
      Check(animated.Count == withClip,
        $"every type with a clip in the catalogue animates ({animated.Count}/{withClip})");

      // each actor must play the clip the catalogue names for ITS type. The
      // Unity state name is the clip FILE's stem (one clip file backs several
      // catalogue names), so the check is against the file the record names.
      var wrong = new List<string>();
      foreach (string[] rec in ReadClipRecords(json))
      {
        Transform actor = actors.Find(a => a.name.StartsWith(rec[0] + " ", StringComparison.Ordinal));
        if (actor == null) continue;
        Animation animation = actor.GetComponent<Animation>();
        string got = animation != null ? S2MActorAnimation.StateName(animation) : null;
        if (got != rec[1]) wrong.Add($"{rec[0]}: expected '{rec[1]}', got '{got}'");
      }
      Check(wrong.Count == 0, "each type plays the clip file named for it"
        + (wrong.Count == 0 ? "" : " (" + string.Join("; ", wrong) + ")"));
      Line("");
    }

    /// <summary>[type, clip-file stem] per record that names a clip.</summary>
    private static List<string[]> ReadClipRecords(string json)
    {
      var outRecs = new List<string[]>();
      string type = null, clip = null;
      foreach (string line in File.ReadAllLines(json))
      {
        string t = line.Trim();
        if (t.StartsWith("\"type\"", StringComparison.Ordinal))
        {
          if (type != null && clip != null) outRecs.Add(new[] { type, clip });
          int colon = t.IndexOf(':');
          type = t.Substring(colon + 1).Trim().Trim(',', '"');
          clip = null;
        }
        else if (t.StartsWith("\"idle_clip\"", StringComparison.Ordinal))
        {
          int colon = t.IndexOf(':');
          string value = t.Substring(colon + 1).Trim().Trim(',', '"');
          clip = value.Length > 0 ? Path.GetFileNameWithoutExtension(value) : null;
        }
      }
      if (type != null && clip != null) outRecs.Add(new[] { type, clip });
      return outRecs;
    }

    private static void RunMap(string json, int expected)
    {
      Line($"entities     : {Path.GetFileName(json)}");

      S2MPlacementHarness.DestroyAll();
      GameObject root = S2MPlacementHarness.LoadEntities(json, GameRoot);
      var actors = new List<Transform>();
      foreach (Transform group in root.transform) actors.AddRange(group.Cast<Transform>().ToList());
      Line($"placed       : {actors.Count} of {expected} entity instances");

      int placed = actors.Count;
      Check(placed == expected, $"{placed} instances created (expected {expected})");

      var skinned = actors.FindAll(a => a.GetComponentInChildren<SkinnedMeshRenderer>() != null);
      Line($"skinned      : {skinned.Count} of {placed} instances render through a SkinnedMeshRenderer");
      Check(skinned.Count > 0, "at least one actor renders skinned");

      var animated = actors.FindAll(a => a.GetComponent<Animation>() != null);
      Line($"animated     : {animated.Count} instances carry a playing clip");

      // The catalogue report: which types animate, with which clip. Distinct
      // (clip file, duration) pairs are what prove the actors are not all
      // sharing one global clip - the archer's 18.04 s is the reference.
      var byClip = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
      foreach (Transform actor in animated)
      {
        Animation animation = actor.GetComponent<Animation>();
        string state = S2MActorAnimation.StateName(animation);
        string key = $"{state} ({animation[state].clip.length:F2} s)";
        if (!byClip.TryGetValue(key, out var list)) byClip[key] = list = new List<Transform>();
        list.Add(actor);
      }
      Line($"clips        : {byClip.Count} distinct (clip file, duration) pairs");
      foreach (var pair in byClip.OrderByDescending(p => p.Value.Count))
      {
        var types = new SortedSet<string>(pair.Value.Select(a => a.name.Split(' ')[0]),
                                          StringComparer.Ordinal);
        Line($"                {pair.Value.Count,4} x {pair.Key,-36} types: {string.Join(", ", types)}");
      }
      Check(byClip.Count >= 3,
        "at least 3 distinct clips are playing (the actor->clip catalogue, "
        + "not one global clip)");

      foreach (Transform actor in animated.Take(2))
      {
        Animation instance = actor.GetComponent<Animation>();
        string clip = S2MActorAnimation.StateName(instance);
        Line($"              '{actor.name}' -> '{clip}' "
             + $"({instance.GetClipCount()} curve set(s), {instance[clip].clip.length:F3} s)");
      }

      // The archer cluster: 67 instances on war_chapter1, every one with a clip.
      var archers = animated.FindAll(a => a.name.StartsWith("Archer ", StringComparison.Ordinal));
      Line($"archers      : {archers.Count} animated Archer instances");
      Check(archers.Count > 0, "the archers of war_chapter1 animate");

      if (archers.Count > 0)
      {
        Transform archer = archers[0];
        Animation animation = archer.GetComponent<Animation>();
        // NOT animation.clip: it is the "clip Play() with no argument would
        // use" field and stays null for a clip added under a state name.
        string clipName = S2MActorAnimation.StateName(animation);
        float length = animation[clipName].clip.length;
        Line($"playing      : '{clipName}' on {archer.name}, {length:F3} s, "
             + $"{animation[clipName].clip.length:F3} s wrap {animation.wrapMode}");
        Sample(animation, clipName, 0f);
        Vector3 first = archer.Find("Bip01")?.localPosition ?? Vector3.zero;
        Sample(animation, clipName, length * 0.5f);
        Vector3 half = archer.Find("Bip01")?.localPosition ?? Vector3.zero;
        Sample(animation, clipName, length * 0.95f);
        Vector3 last = archer.Find("Bip01")?.localPosition ?? Vector3.zero;
        Line($"root bone    : t=0 {first}, t={length * 0.5f:F2}s {half}, t={length * 0.95f:F2}s {last}");
        double travelled = (first - last).magnitude;
        Check(travelled > 0.01f,
          $"the placed archer's root bone moves between two clip times ({travelled:F4} m)");

        // The rotation must be the whole Y Euler of the placed instance, plus
        // whatever the clip does to the bones beneath it.
        Line($"placed       : pos {archer.localPosition} rotY {archer.localEulerAngles.y:F2}");
      }

      // No actor may have been dropped by the skinned switch (a model that
      // fails to convert is skipped with a warning, which would show up here
      // as a shortfall against the JSON).
      int expectedModels = DistinctModels(json);
      int actualModels = root.transform.Cast<Transform>().Count(t => t.childCount > 0);
      Line($"model groups : {actualModels} distinct models rendered, {expectedModels} referenced");
      Check(actualModels == expectedModels, "every referenced model rendered");
      Line("");
    }

    private static int DistinctModels(string json)
    {
      var models = new HashSet<string>(StringComparer.Ordinal);
      foreach (string line in File.ReadAllLines(json))
      {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith("\"model\"", StringComparison.Ordinal)) continue;
        int colon = trimmed.IndexOf(':');
        if (colon < 0) continue;
        string value = trimmed.Substring(colon + 1).Trim().Trim(',', '"');
        if (value.Length > 0) models.Add(value.Replace('\\', '/'));
      }
      return models.Count;
    }

    private static void Sample(Animation animation, string stateName, float time)
    {
      AnimationState state = animation[stateName];
      if (state == null) return;
      state.time = time;
      animation.Sample();
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
