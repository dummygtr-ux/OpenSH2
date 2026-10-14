using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public sealed class Granny2ModelData
  {
    public string SourcePath { get; internal set; }
    public string GrannyVersion { get; internal set; }
    public IReadOnlyList<Granny2MeshData> Meshes { get; internal set; } = Array.Empty<Granny2MeshData>();
    /// <summary>
    /// Skeleton in bind order, or empty for the static models (buildings,
    /// vegetation) whose GR2 files carry no skeleton. The bone at index i is the
    /// bone every mesh's vertex bone index i refers to.
    /// </summary>
    public IReadOnlyList<Granny2BoneData> Bones { get; internal set; } = Array.Empty<Granny2BoneData>();
    public bool IsSkinned => Bones.Count > 0;
  }

  public sealed class Granny2MeshData
  {
    public string Name { get; internal set; }
    /// <summary>Eight floats per vertex: position XYZ, normal XYZ, UV.</summary>
    public float[] VertexData { get; internal set; }
    public int[] Indices { get; internal set; }
    public IReadOnlyList<Granny2MaterialData> Materials { get; internal set; } = Array.Empty<Granny2MaterialData>();
    public IReadOnlyList<Granny2TriangleGroup> TriangleGroups { get; internal set; } = Array.Empty<Granny2TriangleGroup>();
    /// <summary>
    /// The mesh's bone bindings in the file's own order (alphabetical on the
    /// archer), or empty. <see cref="VertexBoneIndices"/> indexes THIS array,
    /// never the skeleton directly - each binding names a skeleton bone, which
    /// is how the join is made (the clip rigs are a superset of the model rig).
    /// </summary>
    public IReadOnlyList<Granny2BoneBinding> BoneBindings { get; internal set; } = Array.Empty<Granny2BoneBinding>();
    /// <summary>Four bone-bone indices per vertex, or empty when unskinned.</summary>
    public int[] VertexBoneIndices { get; internal set; } = Array.Empty<int>();
    /// <summary>Four weights per vertex matching <see cref="VertexBoneIndices"/>.</summary>
    public float[] VertexBoneWeights { get; internal set; } = Array.Empty<float>();
    public bool IsSkinned => VertexBoneIndices.Length > 0;
  }

  public sealed class Granny2BoneData
  {
    public string Name { get; internal set; }
    public int Parent { get; internal set; }
    public float[] Translation { get; internal set; } = { 0f, 0f, 0f };
    /// <summary>Quaternion xyzw, as Granny stores it.</summary>
    public float[] Rotation { get; internal set; } = { 0f, 0f, 0f, 1f };
    public float[] ScaleShear { get; internal set; } = { 1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f };
    /// <summary>
    /// The bone's inverse WORLD matrix in bind pose, COLUMN-major (Granny's
    /// native order), i.e. exactly what Unity's Mesh.bindposes wants.
    /// </summary>
    public float[] Bindpose { get; internal set; } = { 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f };
  }

  public sealed class Granny2BoneBinding
  {
    public string BoneName { get; internal set; }
    public float[] ObbMin { get; internal set; } = { 0f, 0f, 0f };
    public float[] ObbMax { get; internal set; } = { 0f, 0f, 0f };
  }

  public sealed class Granny2MaterialData
  {
    public string Name { get; internal set; }
    /// <summary>Original exporter texture path retained inside the GR2 file.</summary>
    public string TexturePath { get; internal set; }
  }

  public readonly struct Granny2TriangleGroup
  {
    public int MaterialIndex { get; }
    public int TriangleFirst { get; }
    public int TriangleCount { get; }

    public Granny2TriangleGroup(int materialIndex, int triangleFirst, int triangleCount)
    {
      MaterialIndex = materialIndex;
      TriangleFirst = triangleFirst;
      TriangleCount = triangleCount;
    }
  }
}
