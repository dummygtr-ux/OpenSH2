using Assets.Code.Stronghold2.AssetLoading;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Assets.Code.Stronghold2.Buildings
{
  /// <summary>
  /// Edit-mode entry point for the SKINNED-model path (dev harness).
  ///
  /// The actor meshes are skinned: their GR2 carries a skeleton and their
  /// vertices carry bone indices/weights, so they render through
  /// <see cref="SkinnedGranny2Renderer"/> rather than the static
  /// <see cref="Granny2ModelRenderer"/>. This wrapper exists so an editor
  /// script can load ONE model outside Play mode and inspect the skeleton
  /// (bindpose count + bone names) without driving a whole map.
  ///
  /// Usage from an editor script:
  ///   S2MSkinnedModelHarness.Report("meshes/units/models/archer.GR2", gameRoot)
  ///   S2MSkinnedModelHarness.Load(...); S2MSkinnedModelHarness.DestroyAll()
  /// </summary>
  public static class S2MSkinnedModelHarness
  {
    /// <summary>
    /// Loads one skinned model from the Granny2 cache and prints what the runtime
    /// sees: the mesh bindposes, the bone names, and a rest-pose skinning check
    /// that recomputes the mesh position from the bone world matrices.
    /// </summary>
    public static GameObject Report(string assetRelativePath, string gameInstallPath, Transform parent = null)
    {
      GameObject root = Load(assetRelativePath, gameInstallPath, parent);
      Granny2ModelData model = LoadModel(assetRelativePath, gameInstallPath);
      var sb = new StringBuilder();
      sb.AppendLine($"== {Path.GetFileName(assetRelativePath)}");
      sb.AppendLine($"   bones        : {model.Bones.Count}");
      sb.AppendLine($"   bone names   : {string.Join(", ", FirstOf(model.Bones, 12))}");
      for (int i = 0; i < model.Meshes.Count; i++)
      {
        Granny2MeshData mesh = model.Meshes[i];
        sb.AppendLine($"   mesh[{i}] '{mesh.Name}': {mesh.VertexData.Length / 8} verts, "
                      + $"{mesh.BoneBindings.Count} bindings, skin={(mesh.IsSkinned ? "yes" : "NO")}");
        if (mesh.IsSkinned)
        {
          int bad = 0;
          for (int v = 0; v < mesh.VertexData.Length / 8; v++)
          {
            float sum = mesh.VertexBoneWeights[v * 4] + mesh.VertexBoneWeights[v * 4 + 1]
                       + mesh.VertexBoneWeights[v * 4 + 2] + mesh.VertexBoneWeights[v * 4 + 3];
            if (Mathf.Abs(sum - 1f) > 1e-3f) bad++;
          }
          sb.AppendLine($"   weights sum!=1.0 on {bad} of {mesh.VertexData.Length / 8} vertices");
          var bindingNames = new List<string>();
          for (int b = 0; b < mesh.BoneBindings.Count; b++) bindingNames.Add(mesh.BoneBindings[b].BoneName);
          if (bindingNames.Count > 6) bindingNames.Add($"... (+{mesh.BoneBindings.Count - 6} more)");
          sb.AppendLine($"   bindings     : {string.Join(", ", bindingNames)}");
        }
      }
      var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
      if (renderer != null)
      {
        sb.AppendLine($"   renderer     : {renderer.bones.Length} bones, {renderer.sharedMesh.bindposes.Length} bindposes, "
                      + $"rootBone='{renderer.rootBone.name}'");
        sb.AppendLine($"   bindpose[0]  : {renderer.sharedMesh.bindposes[0].ToString("F5")}");
        // Unity's Mesh.RecalculateBounds on a SKINNED mesh is bind-pose aware
        // and reports a conservative box, so also show the raw vertex extent -
        // that is the one that can be compared against the file.
        Mesh checkMesh = renderer.sharedMesh;
        Vector3[] vp = checkMesh.vertices;
        var lo = vp[0]; var hi = vp[0];
        for (int i = 1; i < vp.Length; i++) { lo = Vector3.Min(lo, vp[i]); hi = Vector3.Max(hi, vp[i]); }
        sb.AppendLine($"   bounds(mesh) : {renderer.localBounds}");
        sb.AppendLine($"   bounds(raw)  : lo {lo.ToString("F3")} hi {hi.ToString("F3")} "
                      + $"size {(hi - lo).ToString("F3")}");
        // The acceptance check for the skinned-mesh path: at REST the bones sit on
        // their bind pose, so sum_i w_i * worldMatrix_i * bindpose_i * v must
        // reproduce the mesh's own vertices. Any mis-swapped axis, missing unit
        // scale or wrong bindpose order shows up here as a visible offset.
        RoundTrip check = RestPoseRoundTrip(renderer);
        sb.AppendLine($"   rest-pose rt : worst vertex error {check.error:F6} m "
                      + $"({check.failures} of {check.total} vertices off by > 1 mm) "
                      + $"=> {(check.failures == 0 ? "PASS" : "FAIL")}");
      }
      Debug.Log(sb.ToString().TrimEnd());
      return root;
    }

    private struct RoundTrip
    {
      public double error;
      public int failures;
      public int total;
    }

    /// <summary>
    /// Skins every vertex from the bones' CURRENT world matrices and compares the
    /// result to the mesh's stored position. On a freshly built hierarchy the
    /// bones are still at their bind pose, so this must come back at zero.
    /// </summary>
    private static RoundTrip RestPoseRoundTrip(SkinnedMeshRenderer renderer)
    {
      var result = new RoundTrip();
      Mesh mesh = renderer.sharedMesh;
      Matrix4x4[] bindposes = mesh.bindposes;
      Vector3[] vertices = mesh.vertices;
      BoneWeight[] weights = mesh.boneWeights;
      Transform[] bones = renderer.bones;
      // localToWorldMatrix is scene space; the renderer's own transform converts
      // back into the space the bindposes and vertices live in.
      Matrix4x4 toLocal = renderer.transform.worldToLocalMatrix;
      result.total = vertices.Length;
      for (int v = 0; v < vertices.Length; v++)
      {
        BoneWeight w = weights[v];
        int[] boneIndex = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
        float[] weight = { w.weight0, w.weight1, w.weight2, w.weight3 };
        Vector3 skin = Vector3.zero;
        for (int k = 0; k < 4; k++)
        {
          if (weight[k] == 0f || boneIndex[k] < 0 || boneIndex[k] >= bones.Length) continue;
          Matrix4x4 world = toLocal * bones[boneIndex[k]].localToWorldMatrix;
          skin += weight[k] * world.MultiplyPoint3x4(
            bindposes[boneIndex[k]].MultiplyPoint3x4(vertices[v]));
        }
        double error = (skin - vertices[v]).magnitude;
        if (error > result.error) result.error = error;
        if (error > 1e-3) result.failures++;
      }
      return result;
    }

    public static GameObject Load(string assetRelativePath, string gameInstallPath, Transform parent = null)
    {
      Granny2ModelData model = LoadModel(assetRelativePath, gameInstallPath);
      if (!model.IsSkinned)
      {
        Debug.LogWarning($"{Path.GetFileName(assetRelativePath)} carries no skeleton - "
                         + "use the static Granny2ModelRenderer instead of the skinned path.");
        return Granny2ModelRenderer.Render(model, parent, new Granny2ModelRenderSettings(),
          Path.GetFileNameWithoutExtension(assetRelativePath));
      }
      return SkinnedGranny2Renderer.Render(model, parent, new Granny2ModelRenderSettings(),
        Path.GetFileNameWithoutExtension(assetRelativePath));
    }

    public static void DestroyAll()
    {
      UnityEngine.Object.DestroyImmediate(GameObject.Find("S2M Skinned Models"));
    }

    private static Granny2ModelData LoadModel(string assetRelativePath, string gameInstallPath)
    {
      return Granny2AssetConverter.LoadOrConvert(gameInstallPath, assetRelativePath);
    }

    private static List<string> FirstOf(IReadOnlyList<Granny2BoneData> bones, int count)
    {
      var names = new List<string>();
      for (int i = 0; i < bones.Count && i < count; i++) names.Add(bones[i].Name);
      if (bones.Count > count) names.Add($"... (+{bones.Count - count} more)");
      return names;
    }
  }
}
