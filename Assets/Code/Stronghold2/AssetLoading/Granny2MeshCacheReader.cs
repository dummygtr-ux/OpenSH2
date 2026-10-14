using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Assets.Code.Stronghold2.AssetLoading
{
  public static class Granny2MeshCacheReader
  {
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OSH2GR2\0");

    public static Granny2ModelData Load(string cachePath)
    {
      using var stream = File.OpenRead(cachePath);
      using var reader = new BinaryReader(stream, Encoding.UTF8);
      byte[] magic = reader.ReadBytes(Magic.Length);
      if (magic.Length != Magic.Length || !Equal(magic, Magic))
        throw new InvalidDataException("This is not an OpenSH2 Granny mesh cache.");
      int version = reader.ReadInt32();
      // v1 = mesh only (buildings/vegetation), v2 = the same + a skeleton section
      // and per-vertex bone indices/weights (the skinned unit models).
      if (version != 1 && version != 2)
        throw new InvalidDataException($"Unsupported Granny mesh cache version {version}.");

      string sourcePath = reader.ReadString();
      string grannyVersion = reader.ReadString();

      var bones = Array.Empty<Granny2BoneData>();
      if (version >= 2)
      {
        int skeletonCount = ReadCount(reader, "skeleton", 16);
        if (skeletonCount > 1)
          throw new InvalidDataException("A Granny mesh cache carries at most one skeleton.");
        if (skeletonCount == 1)
        {
          string skeletonName = reader.ReadString();
          int boneCount = ReadCount(reader, "bone", 4096);
          bones = new Granny2BoneData[boneCount];
          for (int i = 0; i < boneCount; i++) bones[i] = ReadBone(reader);
        }
      }

      int meshCount = ReadCount(reader, "mesh", 100000);
      var meshes = new List<Granny2MeshData>(meshCount);
      for (int meshIndex = 0; meshIndex < meshCount; meshIndex++) meshes.Add(ReadMesh(reader, version));
      if (stream.Position != stream.Length) throw new InvalidDataException("The Granny mesh cache contains trailing data.");

      return new Granny2ModelData
      {
        SourcePath = sourcePath,
        GrannyVersion = grannyVersion,
        Meshes = meshes,
        Bones = bones
      };
    }

    private static Granny2BoneData ReadBone(BinaryReader reader)
    {
      string name = reader.ReadString();
      int parent = reader.ReadInt32();
      var translation = ReadFloats(reader, 3);
      var rotation = ReadFloats(reader, 4);
      var scaleShear = ReadFloats(reader, 9);
      var bindpose = ReadFloats(reader, 16);
      return new Granny2BoneData
      {
        Name = name,
        Parent = parent,
        Translation = translation,
        Rotation = rotation,
        ScaleShear = scaleShear,
        Bindpose = bindpose
      };
    }

    private static Granny2MeshData ReadMesh(BinaryReader reader, int version)
    {
      string name = reader.ReadString();
      int vertexCount = ReadCount(reader, "vertex", 10000000);
      var vertexData = new float[checked(vertexCount * 8)];
      for (int i = 0; i < vertexData.Length; i++) vertexData[i] = reader.ReadSingle();

      int indexCount = ReadCount(reader, "index", 30000000);
      var indices = new int[indexCount];
      for (int i = 0; i < indices.Length; i++)
      {
        indices[i] = reader.ReadInt32();
        if (indices[i] < 0 || indices[i] >= vertexCount)
          throw new InvalidDataException($"Mesh '{name}' contains an out-of-range vertex index.");
      }

      int materialCount = ReadCount(reader, "material", 10000);
      var materials = new List<Granny2MaterialData>(materialCount);
      for (int i = 0; i < materialCount; i++) materials.Add(new Granny2MaterialData { Name = reader.ReadString(), TexturePath = reader.ReadString() });

      int groupCount = ReadCount(reader, "triangle group", 100000);
      var groups = new List<Granny2TriangleGroup>(groupCount);
      for (int i = 0; i < groupCount; i++)
      {
        int materialIndex = reader.ReadInt32();
        int triangleFirst = reader.ReadInt32();
        int triangleCount = reader.ReadInt32();
        if (materialIndex < 0 || materialIndex >= Math.Max(1, materialCount)
          || triangleFirst < 0 || triangleCount < 0
          || checked((triangleFirst + triangleCount) * 3) > indexCount)
          throw new InvalidDataException($"Mesh '{name}' contains an invalid triangle group.");
        groups.Add(new Granny2TriangleGroup(materialIndex, triangleFirst, triangleCount));
      }

      var mesh = new Granny2MeshData
      {
        Name = name,
        VertexData = vertexData,
        Indices = indices,
        Materials = materials,
        TriangleGroups = groups
      };
      if (version < 2) return mesh;

      int boneBindingCount = ReadCount(reader, "bone binding", 4096);
      if (boneBindingCount > 0)
      {
        var bindings = new Granny2BoneBinding[boneBindingCount];
        for (int i = 0; i < boneBindingCount; i++)
        {
          bindings[i] = new Granny2BoneBinding
          {
            BoneName = reader.ReadString(),
            ObbMin = ReadFloats(reader, 3),
            ObbMax = ReadFloats(reader, 3)
          };
        }
        mesh.BoneBindings = bindings;
      }

      bool hasSkin = reader.ReadInt32() != 0;
      if (hasSkin)
      {
        int expected = checked(vertexCount * 4);
        var vertexBoneIndices = new int[expected];
        for (int i = 0; i < expected; i++)
        {
          vertexBoneIndices[i] = reader.ReadInt32();
          if (vertexBoneIndices[i] < 0 || vertexBoneIndices[i] >= boneBindingCount)
            throw new InvalidDataException($"Mesh '{name}' references bone binding {vertexBoneIndices[i]} outside its {boneBindingCount} bindings.");
        }
        var vertexBoneWeights = new float[expected];
        for (int i = 0; i < expected; i++) vertexBoneWeights[i] = reader.ReadSingle();
        mesh.VertexBoneIndices = vertexBoneIndices;
        mesh.VertexBoneWeights = vertexBoneWeights;
      }
      return mesh;
    }

    private static float[] ReadFloats(BinaryReader reader, int count)
    {
      var values = new float[count];
      for (int i = 0; i < count; i++) values[i] = reader.ReadSingle();
      return values;
    }

    private static int ReadCount(BinaryReader reader, string description, int maximum)
    {
      int value = reader.ReadInt32();
      if (value < 0 || value > maximum) throw new InvalidDataException($"Invalid {description} count {value}.");
      return value;
    }

    private static bool Equal(byte[] left, byte[] right)
    {
      for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
      return true;
    }
  }
}
