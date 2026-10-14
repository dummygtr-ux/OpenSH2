using System;
using System.Collections.Generic;
using System.IO;
using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.ModelRendering;
using UnityEngine;

namespace Assets.Code.Stronghold2.Buildings
{
  /// <summary>
  /// Renders the map's actors (units, wildlife, siege engines) from an entity
  /// JSON produced by sh2-port/tools/entity_parse.py --json.
  ///
  /// Records mirror the buildings' placements but carry the entity layout
  /// instead of a transform: x@29/z@37 are cells (raw/1024, already divided by
  /// the tool) and y_raw@33 is the standing height in RAW units (512 per metre,
  /// i.e. 1024 per cell like every other height in the map). Each record also
  /// carries its resolved mesh path ("meshes/units/models/archer.GR2"), joined
  /// in Python from manifests/actor_models.csv (tools/uni_info.py) - the model
  /// catalogue's own spelling, which is what must exist on disk on Linux.
  ///
  /// Unit meshes are authored Z-up centimetres with their local origin at the
  /// feet and X centred (archer lo=-95.6,hi=95.6 cm on X; Z 0..218 cm), so a
  /// model sits directly at the decoded position - no anchor-corner offset like
  /// the building footprints' rotated origin.
  ///
  /// ROT (2026-10-09): the actor's yaw in DEGREES, read by the
  /// tool from the first nested Actor block's data (data+44, f32; the same
  /// value is stored again at data+83 and the tool asserts the pair agrees on
  /// all 12,256 actors). Same convention as the buildings' rot@152, so it is
  /// applied the same way: a Y Euler. The block-relative read matters because
  /// the class name is written only at a class's first use in the segment,
  /// which shifts every later field by 4+nameLen bytes on 55 of 12,256 actors
  /// - reading a fixed payload+160 there returns 0 and silently loses the
  /// facing (it happened to 2 Wolf / 1 Cat / 1 FireBallista instances).
  /// </summary>
  internal static class EntityPlacementLoader
  {
    [Serializable]
    private class Entity
    {
      public string type;
      public string model;
      public float x;
      public float y_raw;
      public float z;
      public float rot;
      /// <summary>The clip this actor idles into (game-relative .gr2 path), or
      /// empty when its type has none - the actor then stays in its rest
      /// pose. Joined in Python from manifests/actor_idle_clips.csv.</summary>
      public string idle_clip;
      public string idle_clip_name;
    }

    [Serializable]
    private class EntityFile
    {
      public string map;
      public List<Entity> entities;
      /// <summary>The per-type state -> clip catalogue (one row per type/state),
      /// written by sh2-port/tools/entity_parse.py. Absent in an older JSON, in
      /// which case every actor falls back to its record's idle clip.</summary>
      public List<ActorStateClipRow> state_clips;
    }

    /// <summary>
    /// Renders the skinned actor models, one actor per entity, on SHARED
    /// per-model assets.
    ///
    /// The static path (<see cref="Granny2ModelRenderer.RenderInstances"/>)
    /// cannot animate: it builds no bone hierarchy, so the mesh is frozen in
    /// whatever pose the file's bind pose is. An actor therefore needs a real
    /// <see cref="SkinnedMeshRenderer"/> per instance - but only its BONES are
    /// per-instance. The meshes, materials and textures are built once per model
    /// (<see cref="SkinnedGranny2Renderer.SkinnedGranny2SharedModel"/>) and the
    /// clip once per clip file (<see cref="S2MActorClipCache"/>), both owned by
    /// this group's root. Before that split, war_chapter8's 340 actors held 348
    /// meshes, 773 materials, 765 textures and 328 AnimationClips - 6.7 GB of
    /// native memory for one map load (harness S2MEntityMemoryRun).
    /// The positions/euler are applied exactly as the static path applies the
    /// instance transforms, so the decoded facing and standing height are
    /// unchanged.
    ///
    /// The clip comes from the type's state catalogue via
    /// <see cref="ActorStateTable"/>: the IDLE row, because a saved map stores
    /// every actor at rest. The record's idle_clip is the fallback for a JSON
    /// with no "state_clips" array.
    /// </summary>
    private static int RenderSkinnedActor(Granny2ModelData model, List<Entity> entities,
      Transform parent, string gameInstallPath, int indexStart)
    {
      var settings = new Granny2ModelRenderSettings
      {
        AlphaTestMode = Gr2AlphaTestMode.AlphaTestSuffix
      };
      // The catalogue decides which clip this actor has (type -> .uni/.aml
      // animation-name list -> AnimList.anl / the animal's own .aml clip ->
      // the .gr2 file, joined in Python from manifests/actor_state_clips.csv);
      // a missing cache is a missing build step on the extraction host, not a
      // decode failure, so a missing clip animates nothing rather than
      // skipping the actor.
      string type = entities[0].type;
      IReadOnlyDictionary<ActorState, S2MActorAnimation.StateClip> catalogue =
        ActorStateTable.ClipsFor(type);
      Granny2AnimData anim = null;
      string clipPath = null;
      string clipName = entities[0].idle_clip_name;
      bool fromCatalogue = ActorStateTable.TryLoad(type, ActorState.Idle, gameInstallPath,
        out anim, out string reason);
      if (fromCatalogue)
      {
        clipPath = ActorStateTable.ClipPathFor(type, ActorState.Idle);
        if (catalogue.TryGetValue(ActorState.Idle, out S2MActorAnimation.StateClip idleClip))
          clipName = idleClip.AnimationName;
      }
      else
      {
        clipPath = entities[0].idle_clip;
        clipName = entities[0].idle_clip_name;
        try
        {
          if (!string.IsNullOrWhiteSpace(clipPath))
            anim = Granny2AnimLoader.Load(gameInstallPath, clipPath);
        }
        catch (Exception exception)
        {
          Debug.LogWarning($"Clip '{clipPath}' for '{model.SourcePath}' unavailable "
                           + $"({exception.Message}); the actor renders at its rest pose.");
          anim = null;
        }
      }
      if (anim == null && reason != null)
        Debug.Log($"No clip for '{type}' ({reason}); the actor renders at its rest pose.");

      // The group root OWNS everything the actors share: the model's meshes,
      // materials and textures, and the clips the actors play. An actor root
      // carries only its own bones and renderers, so destroying a group frees
      // the shared set exactly once.
      var groupRoot = new GameObject($"{Path.GetFileNameWithoutExtension(model.SourcePath)} Instances");
      groupRoot.transform.SetParent(parent, false);
      var resources = groupRoot.AddComponent<Granny2GeneratedModelResources>();
      SkinnedGranny2Renderer.SkinnedGranny2SharedModel shared =
        SkinnedGranny2Renderer.SkinnedGranny2SharedModel.Build(model, settings);
      shared.Own(resources);
      groupRoot.AddComponent<S2MActorClipCache>().Own(resources);

      int index = indexStart;
      int animated = 0;
      foreach (var entity in entities)
      {
        string name = $"{entity.type} {index++}";
        GameObject actor = shared.CreateInstance(groupRoot.transform, name);
        // x/z are cells (2 m/cell); y_raw is raw height (512/metre) - the same
        // reads and the same factors the static instance path uses.
        actor.transform.localPosition = new Vector3(
          entity.x * S2MWorldScale.MetresPerCell,
          entity.y_raw * S2MWorldScale.HeightUnitScale,
          entity.z * S2MWorldScale.MetresPerCell);
        actor.transform.localRotation = Quaternion.Euler(0f, entity.rot, 0f);
        if (anim == null) continue;
        // A clip that fails to bind must not cost the actor: the actor already
        // renders correctly at its rest pose, and one bad cache should not
        // silently drop a whole type from the map.
        try
        {
          S2MActorAnimation.Attached attached = S2MActorAnimation.Attach(actor, anim, settings);
          if (attached.Animation != null) animated++;
        }
        catch (Exception exception)
        {
          Debug.LogWarning($"Clip for '{name}' ({anim.ClipName}) not applied: {exception.Message}");
        }
      }
      if (anim != null)
      {
        int clipCount = groupRoot.GetComponent<S2MActorClipCache>().Count;
        Debug.Log($"Animating {animated}/{entities.Count} '{entities[0].type}' actors with "
                  + $"'{clipName}' ({clipPath}, duration {anim.Duration:0.##} s, "
                  + $"{shared.MeshCount} shared mesh(es), {clipCount} shared clip(s))");
      }
      return entities.Count;
    }

    public static GameObject Load(string entitiesJsonPath, string gameInstallPath, Transform parent)
    {
      var file = JsonUtility.FromJson<EntityFile>(File.ReadAllText(entitiesJsonPath));
      if (file?.entities == null || file.entities.Count == 0)
        throw new InvalidDataException($"No entities found in {entitiesJsonPath}.");

      // One group per (model, idle clip): two types can share a mesh (Ballista /
      // FireBallista share ballista_body.GR2) while idling into different clips,
      // and a clip is loaded once per group anyway. The state catalogue is
      // looked up per TYPE inside the group, so a shared mesh with different
      // types still gets each type's own state clips.
      var byModel = new Dictionary<string, List<Entity>>();
      foreach (var entity in file.entities)
      {
        if (string.IsNullOrWhiteSpace(entity.model))
        {
          Debug.LogWarning($"Entity '{entity.type}' has no model; skipped.");
          continue;
        }
        string key = entity.model + "|" + (entity.idle_clip ?? "");
        if (!byModel.TryGetValue(key, out var list))
          byModel[key] = list = new List<Entity>();
        list.Add(entity);
      }

      // The per-type state -> clip catalogue, once per entity file.
      ActorStateTable.Reset(file.state_clips);

      var root = new GameObject("S2M Entities");
      if (parent != null) root.transform.SetParent(parent, false);

      int placed = 0;
      foreach (var pair in byModel)
      {
        string modelPath = pair.Key.Split('|')[0];
        try
        {
          var model = Granny2AssetConverter.LoadOrConvert(gameInstallPath, modelPath);
          if (model.IsSkinned)
          {
            placed += RenderSkinnedActor(model, pair.Value, root.transform,
              gameInstallPath, 0);
            continue;
          }
          var instances = new List<Granny2ModelInstance>();
          int index = 0;
          foreach (var entity in pair.Value)
          {
            // x/z are already cells (the tool divided by 1024) -> * 2 m/cell.
            // y_raw is NOT a cell count: it is raw height at 512 raw/metre, so
            // it scales by HeightUnitScale (1/512), exactly like the terrain
            // and wall renderers. (Building JSON y is raw@144/1024 = cells and
            // is scaled by MetresPerCell instead - the two layouts differ.)
            // rot is the decoded facing in degrees (Actor block data+44,
            // block-relative on the tool side); Y Euler, as for buildings.
            instances.Add(new Granny2ModelInstance(
              $"{entity.type} {index++}",
              new Vector3(entity.x * S2MWorldScale.MetresPerCell,
                          entity.y_raw * S2MWorldScale.HeightUnitScale,
                          entity.z * S2MWorldScale.MetresPerCell),
              new Vector3(0f, entity.rot, 0f),
              Vector3.one));
          }
          Granny2ModelRenderer.RenderInstances(model, instances, root.transform,
            new Granny2ModelRenderSettings { AlphaTestMode = Gr2AlphaTestMode.AlphaTestSuffix },
            modelPath);
          placed += instances.Count;
        }
        catch (Exception exception)
        {
          Debug.LogWarning($"Entities '{modelPath}' skipped ({pair.Value.Count} actors): "
            + exception.Message);
        }
      }

      Debug.Log($"Placed {placed}/{file.entities.Count} entities from "
        + $"{Path.GetFileName(entitiesJsonPath)} ({byModel.Count} models).");
      return root;
    }
  }
}
