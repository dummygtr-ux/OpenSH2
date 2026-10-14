using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// Attaches an ".osh2anim" clip to an actor already rendered by
  /// <see cref="SkinnedGranny2Renderer"/>.
  ///
  /// Playback uses the legacy <see cref="Animation"/> component, not Animator,
  /// because the clip is authored with <see cref="Granny2AnimationClip"/> as a
  /// set of Transform curves keyed by bone path - the same shape the game's own
  /// track groups have (a flat list of named transform tracks), with no blend
  /// tree, root-motion or avatar stage behind it.
  ///
  /// WHICH clip an actor plays is a lookup in its type's catalogue, built in
  /// sh2-port: actor type -> the type's own .uni/.aml animation-name list ->
  /// AnimList.anl (or the animal's own .aml clip) -> the .gr2 file. The catalogue
  /// reaches the engine as the entity JSON's "state_clips" array - one row per
  /// (type, state) - and <see cref="EntityPlacementLoader"/> hands the rows for a
  /// type's model to this class's lookup. An actor loads in
  /// <see cref="ActorState.Idle"/> because a saved map stores every actor at rest
  /// (see <see cref="ActorState"/>); the game switches states with
  /// <see cref="AttachState"/>. <see cref="DefaultClipFor"/> is kept only as the
  /// fallback for callers that have no record at all.
  /// </summary>
  /// <summary>
  /// The actor's animation STATES, and which clip each one plays.
  ///
  /// The names are the engine's own vocabulary spelled inside the animation
  /// names (WALK/RUN/IDLE/ATK - see STATE_TOKENS in sh2-port/tools/uni_info.py),
  /// so a state is a name match over the actor's own animation list, not a
  /// position: the lists are in FILE order and a type's state order varies.
  ///
  /// WHAT DRIVES A STATE: nothing in a saved map does. A .s2m writes every actor
  /// at rest (all troops store rot 0, and the payload has no state field - the
  /// header's +61 estate id is the OWNING ESTATE's id, not an animation state),
  /// so an actor loads in <see cref="Idle"/> and the game switches states as it
  /// gives orders. The catalogue is what tells it WHICH CLIP each state plays.
  /// </summary>
  public enum ActorState
  {
    Idle = 0,
    Walk = 1,
    Run = 2,
    Attack = 3
  }

  public static class S2MActorAnimation
  {
    /// <summary>What <see cref="Attach"/> produced for one actor.</summary>
    public readonly struct Attached
    {
      public readonly Animation Animation;
      public readonly int AppliedTracks;
      public readonly int IgnoredTracks;
      public readonly string ClipPath;
      public Attached(Animation animation, int appliedTracks, int ignoredTracks,
        string clipPath)
      {
        Animation = animation;
        AppliedTracks = appliedTracks;
        IgnoredTracks = ignoredTracks;
        ClipPath = clipPath;
      }
    }

    /// <summary>
    /// One row of the per-type state catalogue from the entity JSON's
    /// "state_clips" array: the game-relative .gr2 clip the type plays in
    /// <see cref="state"/>, and the animation name it resolved from (for logs
    /// and audits - the Unity state name is the clip FILE's stem, because one
    /// clip file backs several catalogue names).
    /// </summary>
    public readonly struct StateClip
    {
      public readonly ActorState State;
      public readonly string ClipPath;
      public readonly string AnimationName;
      public StateClip(ActorState state, string clipPath, string animationName)
      {
        State = state;
        ClipPath = clipPath;
        AnimationName = animationName;
      }
    }

    /// <summary>
    /// The fallback clip for a model, as a game-relative .gr2 path (the
    /// ".osh2anim" cache name is derived from it). Only used by callers that
    /// have no entity record; the real catalogue is the record's "state_clips"
    /// (see the class comment).
    /// </summary>
    public static string DefaultClipFor(string modelRelativePath)
    {
      if (modelRelativePath == null) return null;
      return modelRelativePath.Replace('\\', '/').ToLowerInvariant() switch
      {
        "meshes/units/models/archer.gr2" =>
          "meshes/units/animations/soldier/archer_idle.gr2",
        _ => null
      };
    }

    /// <summary>Parses the JSON "state_clips" array's state names.</summary>
    public static bool TryParseState(string name, out ActorState state)
    {
      switch (name)
      {
        case "idle": state = ActorState.Idle; return true;
        case "walk": state = ActorState.Walk; return true;
        case "run": state = ActorState.Run; return true;
        case "attack": state = ActorState.Attack; return true;
        default: state = default; return false;
      }
    }

    /// <summary>
    /// The clip path a type plays in <paramref name="state"/>, or null when its
    /// catalogue has no entry for it - which is a shipped-data fact (the archer's
    /// list names no WALK, the bear's no RUN), not a missing build step.
    /// </summary>
    public static string ClipFor(IReadOnlyDictionary<ActorState, StateClip> catalogue,
      ActorState state)
    {
      if (catalogue != null && catalogue.TryGetValue(state, out StateClip clip)
        && !string.IsNullOrWhiteSpace(clip.ClipPath))
        return clip.ClipPath;
      return null;
    }

    /// <summary>
    /// Re-clips an actor to <paramref name="anim"/> (the clip its NEW state
    /// resolves to) and starts it, replacing whatever it was playing.
    ///
    /// The clip data comes from the caller, not from here: the point of the
    /// catalogue is that one group of same-type actors shares its clips, so the
    /// caller loads a type's handful of clips once and hands them out. A state
    /// change therefore costs a curve rebuild per actor, not a disk read.
    ///
    /// A state switch RE-SAMPLES from the new clip rather than blending: the
    /// legacy <see cref="Animation"/> component has no blend tree and the cache
    /// carries no cross-fade data, so the switch is a cut - which is what the
    /// game's own playback looks like for these flat transform tracks.
    /// </summary>
    public static Attached AttachState(GameObject actorRoot, Granny2AnimData anim,
      Granny2ModelRenderSettings settings = null, bool play = true)
    {
      if (actorRoot == null) throw new ArgumentNullException(nameof(actorRoot));
      return Attach(actorRoot, BonesFor(actorRoot), anim, settings, play);
    }

    /// <summary>
    /// The state name an actor's Animation is PLAYING, or null when it has no
    /// clip playing. NOT the first state in the list: an actor that has been
    /// switched from idle to run holds both clips, and the first-added one is
    /// still the enumerator's first. And NOT <see cref="Animation.clip"/>:
    /// that field is what Play() with no argument would use and stays null for
    /// a clip added under a state name.
    /// </summary>
    public static string StateName(Animation animation)
    {
      if (animation == null) return null;
      foreach (AnimationState state in animation)
        if (animation.IsPlaying(state.name)) return state.name;
      foreach (AnimationState state in animation) return state.name;
      return null;
    }

    private static IReadOnlyList<Transform> BonesFor(GameObject actorRoot)
    {
      SkinnedMeshRenderer renderer = actorRoot.GetComponentInChildren<SkinnedMeshRenderer>();
      return renderer != null && renderer.bones.Length > 0 ? renderer.bones
        : System.Array.Empty<Transform>();
    }

    /// <summary>
    /// Builds the clip for <paramref name="anim"/> against the actor's bones and
    /// starts it on <paramref name="actorRoot"/>. The bones come from the
    /// <see cref="SkinnedMeshRenderer"/> the actor's meshes already share, in
    /// skeleton order - the order the clip's bone paths are built against.
    /// <see cref="Attached.Animation"/> is null when the clip has nothing to
    /// play on this skeleton.
    /// </summary>
    public static Attached Attach(GameObject actorRoot, Granny2AnimData anim,
      Granny2ModelRenderSettings settings = null, bool play = true)
    {
      if (actorRoot == null) throw new ArgumentNullException(nameof(actorRoot));
      SkinnedMeshRenderer renderer = actorRoot.GetComponentInChildren<SkinnedMeshRenderer>();
      if (renderer == null || renderer.bones.Length == 0)
        return new Attached(null, 0, anim.Tracks.Count, anim.SourcePath);
      return Attach(actorRoot, renderer.bones, anim, settings, play);
    }

    /// <summary>
    /// Builds the clip for <paramref name="anim"/> against
    /// <paramref name="bones"/> and starts it on <paramref name="actorRoot"/>.
    /// Returns the Animation component so a caller can sample or stop it.
    ///
    /// The clip is SHARED when the actor sits under a group root carrying an
    /// <see cref="S2MActorClipCache"/> (the entity loader's arrangement): the
    /// curve set belongs to the clip file, not to the actor, so every actor of a
    /// type plays one clip with its own phase. Without such a group root - a
    /// harness rendering a bare rig - the clip is built here, as it always was.
    /// </summary>
    public static Attached Attach(GameObject actorRoot, IReadOnlyList<Transform> bones,
      Granny2AnimData anim, Granny2ModelRenderSettings settings = null, bool play = true)
    {
      if (actorRoot == null) throw new ArgumentNullException(nameof(actorRoot));
      S2MActorClipCache cache = S2MActorClipCache.FindFor(actorRoot);
      Granny2AnimationClip.Report report = cache != null
        ? cache.Get(anim, actorRoot.transform, bones, settings)
        : Granny2AnimationClip.Build(anim, actorRoot.transform, bones, settings);
      if (report.AppliedTracks == 0) return new Attached(null, 0, report.IgnoredTracks, anim.SourcePath);
      var animation = actorRoot.GetComponent<Animation>();
      // NOT "GetComponent<Animation>() ?? AddComponent<Animation>()": Unity's
      // fake-null makes the ?? see a non-null reference, so the component is
      // never added and the first AddClip throws MissingComponentException.
      if (animation == null) animation = actorRoot.AddComponent<Animation>();
      var state = AddClip(animation, anim, report.Clip);
      if (!play)
      {
        state.enabled = false;
        return new Attached(animation, report.AppliedTracks, report.IgnoredTracks,
          anim.SourcePath);
      }
      state.enabled = true;
      animation.wrapMode = WrapMode.Loop;
      animation.Play(state.name);
      return new Attached(animation, report.AppliedTracks, report.IgnoredTracks,
          anim.SourcePath);
    }

    /// <summary>Adds the built clip under a unique name; re-adding the same name throws.</summary>
    private static AnimationState AddClip(Animation animation, Granny2AnimData anim, AnimationClip clip)
    {
      string name = string.IsNullOrWhiteSpace(anim.ClipName) ? Path.GetFileNameWithoutExtension(anim.SourcePath) ?? "clip" : anim.ClipName;
      if (animation.GetClip(name) == null) animation.AddClip(clip, name);
      return animation[name];
    }

    /// <summary>
    /// Reads the default clip for a model, or null when the catalogue has no
    /// entry (or its cache was not built on the extraction host).
    /// </summary>
    public static Granny2AnimData LoadDefault(string gameInstallPath, string modelRelativePath,
      out string clipRelativePath)
    {
      clipRelativePath = DefaultClipFor(modelRelativePath);
      if (clipRelativePath == null) return null;
      return Granny2AnimLoader.Load(gameInstallPath, clipRelativePath);
    }
  }
}
