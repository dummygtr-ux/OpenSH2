using Assets.Code.Stronghold2.AssetLoading;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.ModelRendering
{
  /// <summary>
  /// Renders loaded OpenSH2 Granny model data that carries a SKELETON through a
  /// <see cref="SkinnedMeshRenderer"/> with <see cref="Mesh.bindposes"/>/
  /// <see cref="Mesh.boneWeights"/>. Static models (buildings, vegetation) keep
  /// going through <see cref="Granny2ModelRenderer"/> unchanged.
  ///
  /// Coordinate derivation (part of the contract, not incidental):
  /// Granny is Z-up centimetres; the static path converts a vertex as
  /// v_unity = unitScale * Swap * v_granny with Swap = (x, y, z) -> (x, z, y).
  /// Applying the SAME map A = unitScale * Swap to the whole model requires
  ///   bindpose_unity = Swap * bindpose_granny * Swap   (no extra scale: both
  ///   sides are already in Unity units, the factor cancels), and
  ///   boneLocal_unity = Swap * boneLocal_granny * Swap.
  /// With Swap = sigma swapping rows/cols 1 and 2, Swap * M * Swap is
  /// M'[i][j] = M[sigma(i)][sigma(j)], so a rotation becomes quaternion
  /// (x, z, y, w), a translation (x, z, y), and a diagonal scale-shear
  /// (sx, sz, sy). The column-major bindpose array from granny2.dll already
  /// has its translation in the last column; Unity's Matrix4x4 constructor
  /// takes rows, so FromColumnMajor rebuilds ROWS from the column-major array.
  /// </summary>
  public static class SkinnedGranny2Renderer
  {
    /// <summary>sigma as a 4x4 that maps (x, y, z, w) -> (x, z, y, w).</summary>
    private static readonly Matrix4x4 Swap = new(
      new Vector4(1f, 0f, 0f, 0f),
      new Vector4(0f, 0f, 1f, 0f),
      new Vector4(0f, 1f, 0f, 0f),
      new Vector4(0f, 0f, 0f, 1f));

    public static GameObject Render(
      Granny2ModelData model,
      Transform parent = null,
      Granny2ModelRenderSettings settings = null,
      string objectName = null)
    {
      if (model == null) throw new ArgumentNullException(nameof(model));
      if (!model.IsSkinned) throw new ArgumentException("The model carries no skeleton.", nameof(model));
      settings ??= new Granny2ModelRenderSettings();
      if (settings.UnitScale <= 0f) throw new ArgumentOutOfRangeException(nameof(settings.UnitScale));

      // ONE actor: its own root owns the model's shared assets (there is no
      // group root above it to own them). For many actors of one model use
      // SkinnedGranny2SharedModel directly - that is the whole point of it.
      SkinnedGranny2SharedModel shared = SkinnedGranny2SharedModel.Build(model, settings);
      var root = new GameObject(string.IsNullOrWhiteSpace(objectName)
        ? Path.GetFileNameWithoutExtension(model.SourcePath)
        : objectName);
      if (parent != null) root.transform.SetParent(parent, false);
      shared.Own(root.AddComponent<Granny2GeneratedModelResources>());
      shared.AttachTo(root);
      return root;
    }

    /// <summary>
    /// Builds the bone Transforms in the model's bone order and returns them.
    /// The hierarchy comes from each bone's Parent; a parent index outside the
    /// already-built range falls back to the root with a warning rather than
    /// throwing, because one corrupt bone would otherwise cost the whole actor.
    /// </summary>
    public static List<Transform> BuildSkeleton(Granny2ModelData model, Transform parent,
      Granny2ModelRenderSettings settings = null)
    {
      settings ??= new Granny2ModelRenderSettings();
      var bones = new List<Transform>(model.Bones.Count);
      foreach (Granny2BoneData bone in model.Bones)
      {
        var go = new GameObject(string.IsNullOrWhiteSpace(bone.Name) ? "Bone" : bone.Name);
        Transform owner = bone.Parent >= 0 && bone.Parent < bones.Count ? bones[bone.Parent] : parent;
        if (bone.Parent >= 0 && bone.Parent >= bones.Count)
        {
          Debug.LogWarning($"Granny bone '{bone.Name}' claims parent {bone.Parent}, which is not "
                           + $"built yet - attaching it to the model root instead.");
        }
        go.transform.SetParent(owner, false);
        // Swap * localTransform * Swap: translation (x, z, y), quaternion (x, z, y, w),
        // diagonal scale-shear (sx, sz, sy).
        go.transform.localPosition = new Vector3(bone.Translation[0], bone.Translation[2],
          bone.Translation[1]) * settings.UnitScale;
        // sigma is a REFLECTION (det -1), so sigma*R*sigma = R(-sigma(q_vec), w):
        // the quaternion's VECTOR part is negated, which with the y<->z swap is
        // (qx, qz, qy, -qw).
        go.transform.localRotation = new Quaternion(bone.Rotation[0], bone.Rotation[2],
          bone.Rotation[1], -bone.Rotation[3]);
        go.transform.localScale = new Vector3(bone.ScaleShear[0], bone.ScaleShear[8],
          bone.ScaleShear[4]);
        bones.Add(go.transform);
      }
      return bones;
    }

    private static Mesh BuildMesh(Granny2MeshData source, Granny2ModelData model,
      Granny2ModelRenderSettings settings, List<UnityEngine.Object> generated,
      Matrix4x4[] bindposes)
    {
      int vertexCount = source.VertexData.Length / 8;
      var positions = new Vector3[vertexCount];
      var normals = new Vector3[vertexCount];
      var uvs = new Vector2[vertexCount];
      for (int i = 0; i < vertexCount; i++)
      {
        int offset = i * 8;
        Vector3 position = new(source.VertexData[offset], source.VertexData[offset + 1], source.VertexData[offset + 2]);
        Vector3 normal = new(source.VertexData[offset + 3], source.VertexData[offset + 4], source.VertexData[offset + 5]);
        positions[i] = ConvertAxis(position, settings.ConvertZUpToUnityYUp) * settings.UnitScale;
        normals[i] = ConvertAxis(normal, settings.ConvertZUpToUnityYUp).normalized;
        uvs[i] = new Vector2(source.VertexData[offset + 6], source.VertexData[offset + 7]);
      }

      var mesh = new Mesh
      {
        name = source.Name,
        indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
      };
      mesh.vertices = positions;
      mesh.normals = normals;
      mesh.uv = uvs;
      mesh.subMeshCount = source.TriangleGroups.Count;
      for (int groupIndex = 0; groupIndex < source.TriangleGroups.Count; groupIndex++)
      {
        Granny2TriangleGroup group = source.TriangleGroups[groupIndex];
        int[] triangles = new int[group.TriangleCount * 3];
        Array.Copy(source.Indices, group.TriangleFirst * 3, triangles, 0, triangles.Length);
        if (settings.ConvertZUpToUnityYUp) ReverseWinding(triangles);
        mesh.SetTriangles(triangles, groupIndex, calculateBounds: false);
      }

      // Every mesh of a skinned model goes through the SkinnedMeshRenderer, even
      // a rigid one: the Lord's weapons are separate attached meshes that carry a
      // single bone binding, and synthesising weight 1 on that bone is the exact
      // equivalent of parenting them to it - so they follow the pose for free.
      mesh.bindposes = bindposes;
      mesh.boneWeights = BoneWeightsFor(model, source);
      mesh.RecalculateBounds();
      generated.Add(mesh);
      return mesh;
    }

    /// <summary>
    /// Bindposes over the model's whole skeleton, in SKELETON bone order - the
    /// order <see cref="BuildSkeleton"/> returns. Meshes only skin a subset of
    /// the rig, but sharing one order is what makes Mesh.bindposes,
    /// Mesh.boneWeights and SkinnedMeshRenderer.bones line up.
    /// </summary>
    private static Matrix4x4[] BindposesForSkeleton(Granny2ModelData model, float unitScale)
    {
      var result = new Matrix4x4[model.Bones.Count];
      for (int i = 0; i < result.Length; i++) result[i] = Bindpose(model.Bones[i], unitScale);
      return result;
    }

    /// <summary>
    /// Per-vertex weights with the bone indices REMAPPED from the mesh's
    /// binding order into skeleton order, so they index
    /// <see cref="SkinnedMeshRenderer.bones"/> directly.
    /// </summary>
    private static BoneWeight[] BoneWeightsFor(Granny2ModelData model, Granny2MeshData source)
    {
      if (source.VertexBoneIndices.Length == 0)
      {
        return RigidBoneWeightsFor(model, source);
      }
      return SkinWeightsFor(model, source);
    }

    /// <summary>
    /// A rigid mesh (the siege engines' 1-bone boxes, the Lord's weapon meshes)
    /// carries no per-vertex data, so every vertex is bound to its single bone
    /// binding with weight 1 - which makes it move with that bone exactly as a
    /// parented child would.
    /// </summary>
    private static BoneWeight[] RigidBoneWeightsFor(Granny2ModelData model, Granny2MeshData source)
    {
      if (source.BoneBindings.Count == 0)
      {
        // No bindings at all (the Lord's own mesh before its weapon meshes are
        // separated out). Bind it to the root so it still renders in place.
        int vertexCount = source.VertexData.Length / 8;
        var weights = new BoneWeight[vertexCount];
        int root = RootBoneIndex(model);
        for (int v = 0; v < vertexCount; v++)
        {
          weights[v].weight0 = 1f;
          weights[v].boneIndex0 = root;
        }
        return weights;
      }
      string boundBone = source.BoneBindings[0].BoneName;
      int skeletonIndex = -1;
      for (int i = 0; i < model.Bones.Count; i++)
      {
        if (string.Equals(model.Bones[i].Name, boundBone, StringComparison.Ordinal))
        {
          skeletonIndex = i;
          break;
        }
      }
      if (skeletonIndex < 0) skeletonIndex = RootBoneIndex(model);
      int count = source.VertexData.Length / 8;
      var result = new BoneWeight[count];
      for (int v = 0; v < count; v++)
      {
        result[v].weight0 = 1f;
        result[v].boneIndex0 = skeletonIndex;
      }
      return result;
    }

    private static int RootBoneIndex(Granny2ModelData model)
    {
      for (int i = 0; i < model.Bones.Count; i++)
      {
        if (model.Bones[i].Parent < 0) return i;
      }
      return 0;
    }

    private static BoneWeight[] SkinWeightsFor(Granny2ModelData model, Granny2MeshData source)
    {
      var byName = new Dictionary<string, int>(StringComparer.Ordinal);
      for (int i = 0; i < model.Bones.Count; i++)
      {
        if (!string.IsNullOrEmpty(model.Bones[i].Name)) byName[model.Bones[i].Name] = i;
      }

      var skeletonIndexForBinding = new int[source.BoneBindings.Count];
      for (int b = 0; b < skeletonIndexForBinding.Length; b++)
      {
        string name = source.BoneBindings[b].BoneName;
        if (!byName.TryGetValue(name, out int skeletonIndex))
        {
          throw new InvalidDataException(
            $"Mesh '{source.Name}' binds to bone '{name}', which its model's skeleton does not contain. "
            + "The model file changed, or the rig join is by a name this build does not ship.");
        }
        skeletonIndexForBinding[b] = skeletonIndex;
      }

      int vertexCount = source.VertexData.Length / 8;
      var weights = new BoneWeight[vertexCount];
      for (int v = 0; v < vertexCount; v++)
      {
        int offset = v * 4;
        int i0 = skeletonIndexForBinding[source.VertexBoneIndices[offset]];
        int i1 = skeletonIndexForBinding[source.VertexBoneIndices[offset + 1]];
        int i2 = skeletonIndexForBinding[source.VertexBoneIndices[offset + 2]];
        int i3 = skeletonIndexForBinding[source.VertexBoneIndices[offset + 3]];
        weights[v].boneIndex0 = i0;
        weights[v].boneIndex1 = i1;
        weights[v].boneIndex2 = i2;
        weights[v].boneIndex3 = i3;
        weights[v].weight0 = source.VertexBoneWeights[offset];
        weights[v].weight1 = source.VertexBoneWeights[offset + 1];
        weights[v].weight2 = source.VertexBoneWeights[offset + 2];
        weights[v].weight3 = source.VertexBoneWeights[offset + 3];
      }
      return weights;
    }

    private static Material[] CreateMaterials(Granny2MeshData source, string sourcePath,
      Granny2ModelRenderSettings settings, List<UnityEngine.Object> generated)
    {
      var result = new Material[source.TriangleGroups.Count];
      for (int i = 0; i < result.Length; i++)
      {
        int materialIndex = source.TriangleGroups[i].MaterialIndex;
        Granny2MaterialData data = materialIndex < source.Materials.Count ? source.Materials[materialIndex] : null;
        result[i] = CreateMaterial(data, sourcePath, settings, generated);
      }
      return result;
    }

    /// <summary>Same texture resolution as the static renderer.</summary>
    private static Material CreateMaterial(Granny2MaterialData data, string gr2Path,
      Granny2ModelRenderSettings settings, List<UnityEngine.Object> generated)
    {
      Material material;
      if (settings.FallbackMaterial != null)
      {
        material = new Material(settings.FallbackMaterial);
      }
      else
      {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No compatible model shader could be found.");
        material = new Material(shader);
      }
      material.name = string.IsNullOrWhiteSpace(data?.Name) ? "GR2 Material" : data.Name;

      string texturePath = Granny2ModelRenderer.ResolveTexturePath(gr2Path, data?.TexturePath);
      if (texturePath != null)
      {
        Texture2D texture = DdsTextureLoader.Load(texturePath);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        generated.Add(texture);
      }
      generated.Add(material);
      return material;
    }

    /// <summary>
    /// Granny's column-major 4x4 (the translation is the last column) as a Unity
    /// Matrix4x4. Unity's Matrix4x4 constructor takes COLUMNS, so the four
    /// consecutive floats of each Granny column become one constructor argument -
    /// passing them as rows silently transposes the matrix, which still looks
    /// plausible for a rotation but swaps the whole vertex set around.
    /// </summary>
    private static Matrix4x4 FromColumnMajor(float[] m)
    {
      return new Matrix4x4(
        new Vector4(m[0], m[1], m[2], m[3]),
        new Vector4(m[4], m[5], m[6], m[7]),
        new Vector4(m[8], m[9], m[10], m[11]),
        new Vector4(m[12], m[13], m[14], m[15]));
    }

    /// <summary>
    /// The bindpose under the Z-up cm -> Y-up unitSpace swap. Both the vertices
    /// and the bone-local positions already live in unitSpace, so the LINEAR part
    /// is just the axis swap, while the translation - which enters from the
    /// centimetre side - picks up unitScale. Skipping that last factor is the
    /// classic bug here: it leaves a ~1 m ghost offset on every unit model.
    /// </summary>
    private static Matrix4x4 Bindpose(Granny2BoneData bone, float unitScale)
    {
      Matrix4x4 m = Swap * FromColumnMajor(bone.Bindpose) * Swap;
      Vector4 column = m.GetColumn(3);
      m.SetColumn(3, new Vector4(column.x * unitScale, column.y * unitScale,
        column.z * unitScale, 1f));
      return m;
    }

    private static Vector3 ConvertAxis(Vector3 value, bool convert) => convert
      ? new Vector3(value.x, value.z, value.y)
      : value;

    private static void ReverseWinding(int[] triangles)
    {
      for (int i = 0; i < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
    }

    /// <summary>
    /// ONE model's render assets - the meshes (vertices, indices, bone weights,
    /// bindposes), the materials and their textures - built once and reused by
    /// every instance of that model.
    ///
    /// Why this type exists (measured by the harness
    /// S2MEntityMemoryRun): war_chapter8's 340 actors come from 13 model files,
    /// but the entity loader used to call <see cref="SkinnedGranny2Renderer
    /// .Render"/> once per ACTOR, and every call built its own Mesh, Material
    /// and Texture2D set - 348 meshes, 773 materials and 765 textures for 340
    /// actors, 6.7 GB of native memory in one map load. Nothing in that set is a
    /// property of the actor: the vertices, the images and the bindposes are the
    /// MODEL's. Sharing them is what Unity's own skinned rendering expects - the
    /// actor supplies the pose, the model supplies the geometry.
    ///
    /// What stays per actor is what a <see cref="SkinnedMeshRenderer"/> really
    /// needs of its own: the bone <see cref="Transform"/> hierarchy and its
    /// <c>bones</c> array (each actor poses independently), the renderer
    /// components themselves, and the <see cref="Animation"/> component that
    /// plays the clip (so each actor keeps its own clip phase).
    ///
    /// Lifetime: a built model owns real Unity objects, so its assets are handed
    /// to a <see cref="Granny2GeneratedModelResources"/> through
    /// <see cref="Own"/> - the group root in a map load, the actor root when a
    /// model is rendered on its own - and die with it.
    /// </summary>
    public sealed class SkinnedGranny2SharedModel
    {
      private readonly struct Part
      {
        public readonly string Name;
        public readonly Mesh Mesh;
        public readonly Material[] Materials;

        public Part(string name, Mesh mesh, Material[] materials)
        {
          Name = name;
          Mesh = mesh;
          Materials = materials;
        }
      }

      private readonly Granny2ModelData model;
      private readonly Granny2ModelRenderSettings settings;
      private readonly List<Part> parts = new();
      private readonly List<UnityEngine.Object> generated = new();

      private SkinnedGranny2SharedModel(Granny2ModelData model, Granny2ModelRenderSettings settings)
      {
        this.model = model;
        this.settings = settings;
      }

      /// <summary>The meshes this model draws (one per mesh in the file).</summary>
      public int MeshCount => parts.Count;

      /// <summary>Bytes of mesh data built here, for a report (CPU side).</summary>
      public long MeshBytes
      {
        get
        {
          long bytes = 0;
          foreach (Part part in parts)
          {
            bytes += (long)part.Mesh.vertexCount * (12 + 12 + 8 + 32);
            for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
              bytes += (long)part.Mesh.GetIndexCount(sub) * 2;
            bytes += (long)(part.Mesh.bindposes?.Length ?? 0) * 64;
          }
          return bytes;
        }
      }

      /// <summary>
      /// Builds the shared assets: one mesh per mesh in the model file (with the
      /// skeleton bindposes and the remapped bone weights), one material per
      /// triangle group and the textures they name. This is the work that used
      /// to happen once per actor.
      /// </summary>
      public static SkinnedGranny2SharedModel Build(Granny2ModelData model,
        Granny2ModelRenderSettings settings = null)
      {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (!model.IsSkinned) throw new ArgumentException("The model carries no skeleton.", nameof(model));
        settings ??= new Granny2ModelRenderSettings();
        if (settings.UnitScale <= 0f) throw new ArgumentOutOfRangeException(nameof(settings.UnitScale));

        var shared = new SkinnedGranny2SharedModel(model, settings);
        // One shared index space for the whole skinned path: the SKELETON's bone
        // order. A mesh's vertex bone indices address its own bone BINDINGS (the
        // file's alphabetical list), each naming a skeleton bone - so both the
        // bindposes and the per-vertex indices are remapped through that name join
        // here, and Mesh.bindposes, Mesh.boneWeights and
        // SkinnedMeshRenderer.bones all end up in the same order.
        Matrix4x4[] bindposes = BindposesForSkeleton(model, settings.UnitScale);
        foreach (Granny2MeshData source in model.Meshes)
        {
          Mesh mesh = BuildMesh(source, model, settings, shared.generated, bindposes);
          shared.parts.Add(new Part(
            string.IsNullOrWhiteSpace(source.Name) ? "GR2 Mesh" : source.Name,
            mesh,
            CreateMaterials(source, model.SourcePath, settings, shared.generated)));
        }
        return shared;
      }

      /// <summary>
      /// Hands the built assets to <paramref name="owner"/> so they are destroyed
      /// with it. Call this on the object that outlives the instances: the group
      /// root for a map's actor group, the actor root for a single render.
      /// </summary>
      public void Own(Granny2GeneratedModelResources owner)
      {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        foreach (UnityEngine.Object value in generated) owner.Add(value);
      }

      /// <summary>
      /// Adds one actor under <paramref name="parent"/>: the bone hierarchy, and
      /// one child per model mesh carrying a <see cref="SkinnedMeshRenderer"/>
      /// that references the SHARED mesh and materials. The renderer still gets
      /// its own <c>bones</c> array - that is the part a skinned renderer cannot
      /// share, because the pose lives in those Transforms.
      /// </summary>
      public GameObject CreateInstance(Transform parent, string name)
      {
        var root = new GameObject(string.IsNullOrWhiteSpace(name)
          ? Path.GetFileNameWithoutExtension(model.SourcePath)
          : name);
        if (parent != null) root.transform.SetParent(parent, false);
        AttachTo(root);
        return root;
      }

      /// <summary>
      /// The same as <see cref="CreateInstance"/> for a caller that already has
      /// the actor's root GameObject (the single-model render path).
      /// </summary>
      public void AttachTo(GameObject root)
      {
        if (root == null) throw new ArgumentNullException(nameof(root));
        List<Transform> bones = BuildSkeleton(model, root.transform, settings);
        foreach (Part part in parts)
        {
          var child = new GameObject(part.Name);
          child.transform.SetParent(root.transform, false);
          child.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
          var renderer = child.AddComponent<SkinnedMeshRenderer>();
          renderer.sharedMesh = part.Mesh;
          renderer.bones = bones.ToArray();
          renderer.rootBone = bones.Count > 0 ? bones[0] : root.transform;
          renderer.sharedMaterials = part.Materials;
          renderer.updateWhenOffscreen = true;
        }
      }
    }
  }
}
