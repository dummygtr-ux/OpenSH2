using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// One row of the entity JSON's "state_clips" array: an actor TYPE's clip for
  /// one STATE (sh2-port/tools/entity_parse.py, from
  /// manifests/actor_state_clips.csv).
  ///
  /// Flat by necessity - Unity's JsonUtility reads a list of flat objects and
  /// cannot deserialize a dictionary, which is why the tool writes the table as
  /// rows instead of nesting it per type.
  /// </summary>
  [Serializable]
  public class ActorStateClipRow
  {
    public string type;
    public string state;
    /// <summary>The animation name the clip resolved from (audit trail; the
    /// Unity state name is the clip FILE's stem, because one clip file backs
    /// several catalogue names).</summary>
    public string anim_name;
    /// <summary>Game-relative .gr2 path; the ".osh2anim" cache name derives
    /// from it.</summary>
    public string clip;
  }

  /// <summary>
  /// The per-type state -> clip catalogue: which clip an actor type plays in
  /// each <see cref="ActorState"/>, with the clips loaded once and shared by
  /// every actor of that type.
  ///
  /// Built from the entity JSON's "state_clips" array, so a map's actors can
  /// switch state without any C#-side table of their own. A type that has no
  /// row for a state simply has no clip for it (the archer's own animation list
  /// names no WALK, the bear's no RUN) - the actor then stays in the pose it is
  /// in, which for a freshly placed actor is its rest pose.
  ///
  /// Nothing in a saved map drives a state (see <see cref="ActorState"/>): a
  /// .s2m stores every actor at rest, so the game sets states as it gives
  /// orders. That is why the loader places every actor in Idle and the harness
  /// switches states explicitly to prove the rest of the table plays.
  /// </summary>
  public static class ActorStateTable
  {
    private static readonly Dictionary<string, Granny2AnimData> Loaded =
      new Dictionary<string, Granny2AnimData>(StringComparer.Ordinal);

    /// <summary>type -> state -> the clip. Ranked by nothing: a type has at most
    /// one row per state.</summary>
    private static readonly Dictionary<string, Dictionary<ActorState, S2MActorAnimation.StateClip>> Rows =
      new Dictionary<string, Dictionary<ActorState, S2MActorAnimation.StateClip>>(StringComparer.Ordinal);

    /// <summary>
    /// Parses the JSON rows. Called once per entity file by
    /// <see cref="EntityPlacementLoader.Load"/>; later rows for the same
    /// (type, state) overwrite earlier ones, which cannot happen in the tool's
    /// output (it writes one row per pair) but keeps the table deterministic.
    /// </summary>
    public static void Reset(IEnumerable<ActorStateClipRow> rows)
    {
      Rows.Clear();
      if (rows == null) return;
      foreach (ActorStateClipRow row in rows)
      {
        if (row == null || string.IsNullOrWhiteSpace(row.type)
          || string.IsNullOrWhiteSpace(row.clip)) continue;
        if (!S2MActorAnimation.TryParseState(row.state, out ActorState state)) continue;
        if (!Rows.TryGetValue(row.type, out Dictionary<ActorState, S2MActorAnimation.StateClip> perType))
          Rows[row.type] = perType = new Dictionary<ActorState, S2MActorAnimation.StateClip>();
        perType[state] = new S2MActorAnimation.StateClip(state,
          row.clip.Replace('\\', '/'), row.anim_name ?? "");
      }
    }

    /// <summary>The clip paths a type plays, per state (empty when the type has
    /// no catalogue rows at all).</summary>
    public static IReadOnlyDictionary<ActorState, S2MActorAnimation.StateClip> ClipsFor(string type)
    {
      if (type != null && Rows.TryGetValue(type,
        out Dictionary<ActorState, S2MActorAnimation.StateClip> perType))
        return perType;
      return new Dictionary<ActorState, S2MActorAnimation.StateClip>();
    }

    /// <summary>The clip path a type plays in <paramref name="state"/>, or null.</summary>
    public static string ClipPathFor(string type, ActorState state) =>
      ClipsFor(type).TryGetValue(state, out S2MActorAnimation.StateClip clip) ? clip.ClipPath : null;

    /// <summary>
    /// The clip data for a state, loaded from the ".osh2anim" cache and CACHED
    /// per clip path - one actor group per type means a type's handful of clips
    /// is read once, not once per actor.
    ///
    /// Returns false, with a reason, when the type has no clip for the state or
    /// its cache is not built on the extraction host. Neither is a decode
    /// failure: the first is the shipped data, the second a missing build step,
    /// and both leave the actor in its rest pose rather than skipping it.
    /// </summary>
    public static bool TryLoad(string type, ActorState state, string gameInstallPath,
      out Granny2AnimData anim, out string reason)
    {
      anim = null;
      string path = ClipPathFor(type, state);
      if (path == null)
      {
        reason = $"{type} has no {state} clip in its catalogue";
        return false;
      }
      if (Loaded.TryGetValue(path, out anim)) { reason = null; return true; }
      try
      {
        anim = Granny2AnimLoader.Load(gameInstallPath, path);
      }
      catch (Exception exception)
      {
        reason = $"cache for {Path.GetFileNameWithoutExtension(path)} unavailable: {exception.Message}";
        anim = null;
        return false;
      }
      Loaded[path] = anim;
      reason = null;
      return true;
    }
  }
}
