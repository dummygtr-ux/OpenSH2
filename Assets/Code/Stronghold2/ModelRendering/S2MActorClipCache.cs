using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// The built clips ONE GROUP of same-type actors shares.
  ///
  /// A component on the group root (the loader's "&lt;model&gt; Instances"
  /// object): every actor under it plays the SAME <see cref="AnimationClip"/>
  /// through its own <see cref="Animation"/> component, so the clip is built
  /// once per clip file instead of once per actor.
  ///
  /// Why (measured by the harness S2MEntityMemoryRun): war_chapter8's
  /// 340 actors carried 328 AnimationClips that held 6.1 GB between them - the
  /// single largest allocation of the whole map load, ~18 MB per clip, while the
  /// mesh data for the same actors was 10 MB. The curve set is not a property of
  /// an actor: it is the CLIP's, addressed by bone PATH relative to the
  /// Animation component's own GameObject. Every actor of a type has the same
  /// hierarchy, so every one of them resolves the same paths - which is exactly
  /// what lets Unity play one clip asset on many objects.
  ///
  /// The phase stays per actor: time lives in each Animation component's own
  /// <see cref="AnimationState"/>, not in the clip. Actors that must desync are
  /// a one-liner at the call site (`animation[name].time = offset`); today every
  /// actor starts at 0, which is what the per-actor builds did too.
  ///
  /// Lifetime: clips built here are registered with the group's
  /// <see cref="Granny2GeneratedModelResources"/> (<see cref="Own"/>), so a
  /// re-load of the map destroys them instead of leaking them - a runtime
  /// AnimationClip is not garbage collected on its own.
  /// </summary>
  public sealed class S2MActorClipCache : MonoBehaviour
  {
    /// <summary>One entry per clip FILE: the built clip and what it bound.</summary>
    private readonly Dictionary<string, Granny2AnimationClip.Report> built =
      new Dictionary<string, Granny2AnimationClip.Report>(StringComparer.Ordinal);

    private Granny2GeneratedModelResources resources;

    /// <summary>How many distinct clips this group has built (for a report).</summary>
    public int Count => built.Count;

    /// <summary>The clips built here die with <paramref name="owner"/>.</summary>
    public void Own(Granny2GeneratedModelResources owner) => resources = owner;

    /// <summary>
    /// The cache an actor belongs to, or null when it is not under one - a
    /// harness that renders a bare rig gets the per-call build, unchanged.
    /// </summary>
    public static S2MActorClipCache FindFor(GameObject actorRoot) =>
      actorRoot == null ? null : actorRoot.GetComponentInParent<S2MActorClipCache>();

    /// <summary>
    /// The clip for <paramref name="anim"/> on this group's skeleton: built on
    /// the first call, the same clip (and the same track counts) afterwards.
    ///
    /// The FIRST actor's root and bones are what the paths are derived from -
    /// valid for the whole group, because a group is one model file and every
    /// actor under it is built from the same skeleton with the same bone names.
    /// </summary>
    public Granny2AnimationClip.Report Get(Granny2AnimData anim, Transform root,
      IReadOnlyList<Transform> bones, Granny2ModelRenderSettings settings = null)
    {
      if (anim == null) throw new ArgumentNullException(nameof(anim));
      string key = Key(anim);
      if (built.TryGetValue(key, out Granny2AnimationClip.Report cached)) return cached;
      Granny2AnimationClip.Report report = Granny2AnimationClip.Build(anim, root, bones, settings);
      built[key] = report;
      if (resources != null) resources.Add(report.Clip);
      return report;
    }

    /// <summary>
    /// A clip's identity: its cache path (one .osh2anim per clip file), falling
    /// back to the clip name for data that came from somewhere else.
    /// </summary>
    private static string Key(Granny2AnimData anim) =>
      string.IsNullOrEmpty(anim.SourcePath) ? anim.ClipName ?? "" : anim.SourcePath;
  }
}
