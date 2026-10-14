using Assets.Code.Stronghold2.ModelRendering;
using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.Code.Stronghold2.TerrainRendering
{
  /// <summary>
  /// Extrudes castle fortifications from the HeightLayer surface planes. Walls
  /// are not serialized as objects or GR2 models anywhere in the game files
  /// (the Walls container is empty in every shipped map): plane 1 stores the
  /// first surface above the ground per cell - wall tops on wall cells, water
  /// surfaces on water cells - and plane 2 stores the structure deck level:
  /// the height a unit stands at on that cell when it carries a castle
  /// structure (tower floors, gatehouse wall-walks, keep terraces, flat
  /// ward/pad cells). Cross-segment proof: scenario units store their standing
  /// height as f32@33 (x@29, y@33, z@37, raw/1024 = cells); y equals plane 2 on
  /// decks, plane 1 on wall-walks, plane 0 on terrain (war_chapter1 163 units,
  /// war_chapter8 324). Classification mirrors sh2-port tools/walls_parse.py:
  /// corner slot 2 per cell, deck when plane 2 stands more than 0.1 m over its
  /// ground, stone wall when plane 1 - plane 0 exceeds 2500 raw units, wooden
  /// palisade above 500 - and only when plane 1 stands ABOVE the water line
  /// (0). On water cells plane 0 serialises the bed, not the shore:
  /// war_chapter8's river and moats carry the bed at the -640 water depression
  /// and the surface at 0, so a +640 raise that is not a wall (2,262 cells)
  /// would otherwise extrude as wooden geometry. Sea cells are excluded.
  /// Building footprints: only instances a rendered model claims (a placement
  /// cell inside the instance bbox + 1, see RenderedBuildingCells) are skipped;
  /// an instance without a model is classified like plain structure, otherwise
  /// it would punch holes through wall runs. Model coverage as of 2026-10-08:
  /// war_chapter1 87/90 instances, war_chapter8 187/206 - the remainder are
  /// 1-cell bridge/platform props (WoodenPlatform, ConstructingBridge), the
  /// three ch1 Statue cells and one ch8 ServantsQuarters whose cell carries no
  /// BuildingLayer entry at all (sh2-port tools/verify_placements.py holds the
  /// documented outlier). Keep and lookout cells are no longer classified as
  /// walls: those types render as models now. Side faces
  /// between neighbouring structure cells whose tops meet within one raw unit
  /// are skipped so a wall run renders as one continuous block.
  /// </summary>
  public static class S2MWallRenderer
  {
    private const int GroundPlane = 0;
    private const int FirstSurfacePlane = 1;
    private const int DeckPlane = 2;
    // Corner slot (x, z): the cell's own minimum corner, matching the slot
    // walls_parse.py classifies with.
    private const int ClassifyCornerSlot = 2;
    private const float StoneWallMinimumRaise = 2500f;
    private const float WoodenWallMinimumRaise = 500f;
    // A wall top must stand above the water line: on water cells plane 0 is the
    // bed (-640 depression on war_chapter8), so the bed-to-surface column reads
    // as a raise but is water, not a palisade.
    private const float WaterLineHeight = 0f;
    private const float TopSeamEpsilon = 1f;
    // Decks at or below the ground draw nothing: the terrain mesh already
    // renders that surface and a coplanar quad would z-fight. 102.4 raw units
    // = 0.1 m: below that a "deck" is paving/quantisation noise, not a
    // structure (war_chapter1's 3 coast cells at +16.3/+0.4 raw).
    private const float DeckMinimumRaise = 102.4f;

    private enum CellKind : byte { None, Stone, Wooden, Deck }

    /// <summary>
    /// The cell tallies of the last <see cref="Render"/>, for callers that check
    /// the render against the python ground truth (`walls_parse.py`'s per-map
    /// manifest classifies the same cells; its `by_kind` stone_wall / wooden_wall
    /// / deck are these three counts). Set before the geometry is built, so a map
    /// with no wall cells at all (Render returns null) still reports zeros.
    /// </summary>
    public static S2MWallCounts LastCounts { get; private set; }

    /// <summary>One wall render's classified cell counts, as the renderer counted them.</summary>
    public readonly struct S2MWallCounts
    {
      public S2MWallCounts(int stone, int wooden, int deck, int flatDecks,
                           int modelCoveredInstances)
      {
        Stone = stone;
        Wooden = wooden;
        Deck = deck;
        FlatDecks = flatDecks;
        ModelCoveredInstances = modelCoveredInstances;
      }

      /// <summary>Cells classified as stone wall (the stone wall-top raise).</summary>
      public readonly int Stone;
      /// <summary>Cells classified as wooden wall (the palisade raise).</summary>
      public readonly int Wooden;
      /// <summary>Cells carrying a structure deck (plane 2).</summary>
      public readonly int Deck;
      /// <summary>Decks at or below the ground - they draw no geometry.</summary>
      public readonly int FlatDecks;
      /// <summary>BuildingLayer instances a rendered model covers (skipped cells).</summary>
      public readonly int ModelCoveredInstances;

      /// <summary>All classified structure cells: stone + wooden + deck.</summary>
      public int Structures => Stone + Wooden + Deck;
    }

    // Submesh order doubles as the material-slot order on the renderer.
    private const int StoneSidesSubmesh = 0;
    private const int StoneTopsSubmesh = 1;
    private const int WoodenSidesSubmesh = 2;
    private const int WoodenTopsSubmesh = 3;

    public static GameObject Render(S2MFile map, Transform parent = null, S2MWallSettings settings = null)
    {
      if (map == null) throw new ArgumentNullException(nameof(map));
      HeightLayer heightLayer = map.HeightLayer;
      if (heightLayer == null)
        throw new InvalidDataException("The loaded S2M map does not contain a decodable HeightLayer.");
      if (heightLayer.CornerHeightPlanes == null || heightLayer.CornerHeightPlanes.Length <= FirstSurfacePlane)
        throw new InvalidDataException("HeightLayer does not contain the corner planes needed to classify walls.");

      settings ??= new S2MWallSettings();
      int width = heightLayer.Width;
      int height = heightLayer.Height;
      bool hasDeckPlane = heightLayer.CornerHeightPlanes.Length > DeckPlane;
      bool hasBuildingLayer = map.BuildingLayer != null
        && map.BuildingLayer.InstanceIds != null
        && map.BuildingLayer.InstanceIds.Length >= width * height;
      if (!hasBuildingLayer)
        Debug.LogWarning("S2M walls: no decodable BuildingLayer; building footprints are not excluded from wall classification.");
      byte[] featureValues = map.WaterLayer?.LandscapeFeatureValues;
      bool hasFeatureValues = featureValues != null && featureValues.Length >= width * height;
      bool hasRenderedCells = settings.RenderedBuildingCells != null
        && settings.RenderedBuildingCells.Length > 0;
      HashSet<int> modelCoveredInstances = hasBuildingLayer && hasRenderedCells
        ? FindModelCoveredInstances(map.BuildingLayer.InstanceIds, width, height,
            settings.RenderedBuildingCells)
        : null;
      if (hasBuildingLayer && !hasRenderedCells)
      {
        Debug.LogWarning("S2M walls: no RenderedBuildingCells given; every BuildingLayer "
          + "footprint is skipped, so structures without a model (unmapped types, props) "
          + "will leave holes and their raised decks will not be drawn.");
      }

      var kinds = new CellKind[width * height];
      var tops = new float[width * height];
      int stoneCount = 0;
      int woodenCount = 0;
      int deckCount = 0;
      int flatDeckCount = 0;
      for (int z = 0; z < height; z++)
      {
        for (int x = 0; x < width; x++)
        {
          int cellIndex = z * width + x;
          if (hasBuildingLayer)
          {
            int instance = map.BuildingLayer.InstanceIds[cellIndex];
            if (instance != 0 && (modelCoveredInstances == null
                || modelCoveredInstances.Contains(instance))) continue;
          }
          if (hasFeatureValues && map.WaterLayer.GetWaterType(x, z) == S2MWaterType.Sea) continue;

          float ground = heightLayer.GetCornerHeight(GroundPlane, cellIndex, ClassifyCornerSlot);
          float firstSurface = heightLayer.GetCornerHeight(FirstSurfacePlane, cellIndex, ClassifyCornerSlot);
          float deckSurface = hasDeckPlane
            ? heightLayer.GetCornerHeight(DeckPlane, cellIndex, ClassifyCornerSlot)
            : 0f;
          if (deckSurface != 0f)
          {
            kinds[cellIndex] = CellKind.Deck;
            tops[cellIndex] = deckSurface;
            deckCount++;
            if (deckSurface <= ground + DeckMinimumRaise) flatDeckCount++;
          }
          else if (firstSurface > WaterLineHeight && firstSurface - ground > StoneWallMinimumRaise)
          {
            kinds[cellIndex] = CellKind.Stone;
            tops[cellIndex] = firstSurface;
            stoneCount++;
          }
          else if (firstSurface > WaterLineHeight && firstSurface - ground > WoodenWallMinimumRaise)
          {
            kinds[cellIndex] = CellKind.Wooden;
            tops[cellIndex] = firstSurface;
            woodenCount++;
          }
        }
      }

      Debug.Log($"S2M walls: {stoneCount} stone, {woodenCount} wooden, {deckCount} deck cells"
        + $" ({flatDeckCount} decks at or below ground draw no geometry;"
        + $" {modelCoveredInstances?.Count ?? 0} model-covered building instances skipped).");

      LastCounts = new S2MWallCounts(stoneCount, woodenCount, deckCount, flatDeckCount,
                                     modelCoveredInstances?.Count ?? 0);

      int structureCount = stoneCount + woodenCount + deckCount;
      if (structureCount == 0) return null;

      var root = new GameObject("S2M Walls");
      if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);

      var submeshes = new[]
      {
        new SurfaceBuffer(), new SurfaceBuffer(), new SurfaceBuffer(), new SurfaceBuffer()
      };
      for (int z = 0; z < height; z++)
      {
        for (int x = 0; x < width; x++)
        {
          int cellIndex = z * width + x;
          CellKind kind = kinds[cellIndex];
          if (kind == CellKind.None) continue;
          AddCell(submeshes, heightLayer, settings, width, height, kinds, tops, cellIndex, x, z, kind);
        }
      }

      var mesh = new Mesh { name = "S2M Walls" };
      mesh.subMeshCount = 4;
      var materials = new Material[4];
      materials[StoneSidesSubmesh] = settings.StoneSideMaterial
        ?? CreateMaterial(settings, settings.StoneSideTexturePath, "S2M Wall Stone Side", new Color(0.58f, 0.55f, 0.50f));
      materials[StoneTopsSubmesh] = settings.StoneTopMaterial
        ?? CreateMaterial(settings, settings.StoneTopTexturePath, "S2M Wall Stone Top", new Color(0.50f, 0.48f, 0.45f));
      materials[WoodenSidesSubmesh] = settings.WoodenSideMaterial
        ?? CreateMaterial(settings, settings.WoodenSideTexturePath, "S2M Wall Wooden Side", new Color(0.45f, 0.32f, 0.18f));
      materials[WoodenTopsSubmesh] = settings.WoodenTopMaterial
        ?? CreateMaterial(settings, settings.WoodenTopTexturePath, "S2M Wall Wooden Top", new Color(0.42f, 0.30f, 0.16f));

      int vertexCount = 0;
      for (int i = 0; i < submeshes.Length; i++) vertexCount += submeshes[i].Vertices.Count;
      mesh.indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;

      var vertices = new List<Vector3>(vertexCount);
      var normals = new List<Vector3>(vertexCount);
      var uvs = new List<Vector2>(vertexCount);
      var baseVertices = new int[submeshes.Length];
      for (int i = 0; i < submeshes.Length; i++)
      {
        baseVertices[i] = vertices.Count;
        vertices.AddRange(submeshes[i].Vertices);
        normals.AddRange(submeshes[i].Normals);
        uvs.AddRange(submeshes[i].UVs);
      }
      mesh.SetVertices(vertices);
      mesh.SetNormals(normals);
      mesh.SetUVs(0, uvs);
      for (int i = 0; i < submeshes.Length; i++)
      {
        var triangles = new List<int>(submeshes[i].Triangles.Count);
        for (int t = 0; t < submeshes[i].Triangles.Count; t++) triangles.Add(submeshes[i].Triangles[t] + baseVertices[i]);
        mesh.SetTriangles(triangles, i, calculateBounds: i == submeshes.Length - 1);
      }

      root.AddComponent<MeshFilter>().sharedMesh = mesh;
      root.AddComponent<MeshRenderer>().sharedMaterials = materials;
      return root;
    }

    /// <summary>
    /// BuildingLayer instances a rendered model claims: an instance counts as
    /// model-covered when one of the placement cells falls inside its footprint
    /// bounding box expanded by one cell. The window is deliberately loose: the
    /// placement cell is the object's origin, which lies inside the registered
    /// footprint cells for 69/86 (war_chapter1) and 148/191 (war_chapter8)
    /// placements and one cell outside for the rest. Coverage measured
    /// 2026-10-08: ch1 87/90 instances, ch8 187/206 (sh2-port
    /// tools/verify_placements.py reproduces both and lists the remainder:
    /// 1-cell bridge/platform props, ch1 statues, one ch8 object whose cell
    /// carries no BuildingLayer entry). The only model-less instance the loose
    /// window skips is one flat-deck ch1 Statue cell, which draws nothing
    /// anyway - every other unclaimed instance stays classified as wall/deck
    /// geometry, so unmapped props leave no holes.
    /// </summary>
    private static HashSet<int> FindModelCoveredInstances(
      ushort[] instanceIds,
      int width,
      int height,
      Vector2Int[] placementCells)
    {
      var minX = new Dictionary<int, int>();
      var minZ = new Dictionary<int, int>();
      var maxX = new Dictionary<int, int>();
      var maxZ = new Dictionary<int, int>();
      for (int z = 0; z < height; z++)
      {
        for (int x = 0; x < width; x++)
        {
          int instance = instanceIds[z * width + x];
          if (instance == 0) continue;
          if (!minX.TryGetValue(instance, out int x0))
          {
            minX[instance] = maxX[instance] = x;
            minZ[instance] = maxZ[instance] = z;
            continue;
          }
          if (x < x0) minX[instance] = x;
          if (x > maxX[instance]) maxX[instance] = x;
          if (z < minZ[instance]) minZ[instance] = z;
          if (z > maxZ[instance]) maxZ[instance] = z;
        }
      }
      var covered = new HashSet<int>();
      foreach (var pair in minX)
      {
        int instance = pair.Key;
        foreach (Vector2Int cell in placementCells)
        {
          if (cell.x >= minX[instance] - 1 && cell.x <= maxX[instance] + 1
            && cell.y >= minZ[instance] - 1 && cell.y <= maxZ[instance] + 1)
          {
            covered.Add(instance);
            break;
          }
        }
      }
      return covered;
    }

    private static void AddCell(
      SurfaceBuffer[] submeshes,
      HeightLayer heightLayer,
      S2MWallSettings settings,
      int width,
      int height,
      CellKind[] kinds,
      float[] tops,
      int cellIndex,
      int x,
      int z,
      CellKind kind)
    {
      bool stone = kind != CellKind.Wooden;
      SurfaceBuffer sides = submeshes[stone ? StoneSidesSubmesh : WoodenSidesSubmesh];
      SurfaceBuffer topsBuffer = submeshes[stone ? StoneTopsSubmesh : WoodenTopsSubmesh];
      int surfacePlane = kind == CellKind.Deck ? DeckPlane : FirstSurfacePlane;

      // Corner slot order: 0=(x,z+1) 1=(x+1,z+1) 2=(x,z) 3=(x+1,z) - probe-verified
      // in Notes/FileFormats/S2M/S2GameSegment/HeightLayer.md.
      float deckRaise = 0f;
      if (kind == CellKind.Deck)
      {
        float ground = heightLayer.GetCornerHeight(GroundPlane, cellIndex, ClassifyCornerSlot);
        deckRaise = heightLayer.GetCornerHeight(DeckPlane, cellIndex, ClassifyCornerSlot) - ground;
        if (deckRaise <= DeckMinimumRaise) return;
      }

      var surfaceCorners = new Vector3[4];
      var groundCorners = new Vector3[4];
      var gridCorners = new[]
      {
        new Vector2(x, z + 1), new Vector2(x + 1, z + 1), new Vector2(x, z), new Vector2(x + 1, z)
      };
      for (int slot = 0; slot < 4; slot++)
      {
        surfaceCorners[slot] = ToWorld(
          settings, width, height, gridCorners[slot],
          heightLayer.GetCornerHeight(surfacePlane, cellIndex, slot));
        groundCorners[slot] = ToWorld(
          settings, width, height, gridCorners[slot],
          heightLayer.GetCornerHeight(GroundPlane, cellIndex, slot));
      }

      AddTopQuad(topsBuffer, settings, surfaceCorners, gridCorners);
      AddSideQuad(sides, settings, kinds, tops, width, height, x, z, cellIndex,
        surfaceCorners, groundCorners, gridCorners, FaceDirection.West);
      AddSideQuad(sides, settings, kinds, tops, width, height, x, z, cellIndex,
        surfaceCorners, groundCorners, gridCorners, FaceDirection.East);
      AddSideQuad(sides, settings, kinds, tops, width, height, x, z, cellIndex,
        surfaceCorners, groundCorners, gridCorners, FaceDirection.South);
      AddSideQuad(sides, settings, kinds, tops, width, height, x, z, cellIndex,
        surfaceCorners, groundCorners, gridCorners, FaceDirection.North);
    }

    private enum FaceDirection { West, East, South, North }

    private static void AddTopQuad(
      SurfaceBuffer buffer,
      S2MWallSettings settings,
      Vector3[] corners,
      Vector2[] gridCorners)
    {
      // Same vertex order and winding as S2MTerrainRenderer's cell quads:
      // slots 2, 3, 0, 1 with triangles (0,2,1) and (1,2,3).
      bool reverseWinding = settings.FlipX ^ settings.FlipZ;
      Vector3 normal = Vector3.Cross(corners[0] - corners[2], corners[3] - corners[2]).normalized;
      if (reverseWinding) normal = -normal;

      int start = buffer.Vertices.Count;
      buffer.Vertices.Add(corners[2]);
      buffer.Vertices.Add(corners[3]);
      buffer.Vertices.Add(corners[0]);
      buffer.Vertices.Add(corners[1]);
      for (int i = 0; i < 4; i++) buffer.Normals.Add(normal);
      buffer.UVs.Add(gridCorners[2]);
      buffer.UVs.Add(gridCorners[3]);
      buffer.UVs.Add(gridCorners[0]);
      buffer.UVs.Add(gridCorners[1]);
      if (!reverseWinding)
      {
        buffer.Triangles.Add(start);
        buffer.Triangles.Add(start + 2);
        buffer.Triangles.Add(start + 1);
        buffer.Triangles.Add(start + 1);
        buffer.Triangles.Add(start + 2);
        buffer.Triangles.Add(start + 3);
      }
      else
      {
        buffer.Triangles.Add(start);
        buffer.Triangles.Add(start + 1);
        buffer.Triangles.Add(start + 2);
        buffer.Triangles.Add(start + 1);
        buffer.Triangles.Add(start + 3);
        buffer.Triangles.Add(start + 2);
      }
    }

    private static void AddSideQuad(
      SurfaceBuffer buffer,
      S2MWallSettings settings,
      CellKind[] kinds,
      float[] tops,
      int width,
      int height,
      int x,
      int z,
      int cellIndex,
      Vector3[] surfaceCorners,
      Vector3[] groundCorners,
      Vector2[] gridCorners,
      FaceDirection direction)
    {
      int neighbourX = x;
      int neighbourZ = z;
      int firstSlot;
      int secondSlot;
      switch (direction)
      {
        case FaceDirection.West:
          neighbourX--;
          firstSlot = 2;
          secondSlot = 0;
          break;
        case FaceDirection.East:
          neighbourX++;
          firstSlot = 1;
          secondSlot = 3;
          break;
        case FaceDirection.South:
          neighbourZ--;
          firstSlot = 3;
          secondSlot = 2;
          break;
        case FaceDirection.North:
          neighbourZ++;
          firstSlot = 0;
          secondSlot = 1;
          break;
        default:
          return;
      }
      if (HidesFace(kinds, tops, width, height, neighbourX, neighbourZ, tops[cellIndex])) return;

      // Quads are wound so the face normal points away from the cell. Like the
      // terrain cliff walls, U follows the grid axis the face runs along so the
      // texture stays continuous across a wall run, and V is the world height.
      Vector3 groundFirst = groundCorners[firstSlot];
      Vector3 groundSecond = groundCorners[secondSlot];
      Vector3 surfaceFirst = surfaceCorners[firstSlot];
      Vector3 surfaceSecond = surfaceCorners[secondSlot];
      float firstU = direction == FaceDirection.West || direction == FaceDirection.East
        ? gridCorners[firstSlot].y
        : gridCorners[firstSlot].x;
      float secondU = direction == FaceDirection.West || direction == FaceDirection.East
        ? gridCorners[secondSlot].y
        : gridCorners[secondSlot].x;

      bool reverseWinding = settings.FlipX ^ settings.FlipZ;
      Vector3 normal = Vector3.Cross(groundSecond - groundFirst, surfaceSecond - groundFirst).normalized;
      if (reverseWinding) normal = -normal;

      // V follows the cliff-wall convention of S2MTerrainRenderer: world height
      // divided by the horizontal cell size.
      float verticalScale = settings.HorizontalCellSize > 0f ? 1f / settings.HorizontalCellSize : 0f;
      var quadVertices = new[] { groundFirst, groundSecond, surfaceSecond, surfaceFirst };
      var quadUVs = new[]
      {
        new Vector2(firstU, groundFirst.y * verticalScale),
        new Vector2(secondU, groundSecond.y * verticalScale),
        new Vector2(secondU, surfaceSecond.y * verticalScale),
        new Vector2(firstU, surfaceFirst.y * verticalScale)
      };
      int start = buffer.Vertices.Count;
      for (int i = 0; i < 4; i++)
      {
        buffer.Vertices.Add(quadVertices[i]);
        buffer.Normals.Add(normal);
        buffer.UVs.Add(quadUVs[i]);
      }
      if (!reverseWinding)
      {
        buffer.Triangles.Add(start);
        buffer.Triangles.Add(start + 1);
        buffer.Triangles.Add(start + 2);
        buffer.Triangles.Add(start);
        buffer.Triangles.Add(start + 2);
        buffer.Triangles.Add(start + 3);
      }
      else
      {
        buffer.Triangles.Add(start);
        buffer.Triangles.Add(start + 2);
        buffer.Triangles.Add(start + 1);
        buffer.Triangles.Add(start);
        buffer.Triangles.Add(start + 3);
        buffer.Triangles.Add(start + 2);
      }
    }

    private static bool HidesFace(
      CellKind[] kinds,
      float[] tops,
      int width,
      int height,
      int neighbourX,
      int neighbourZ,
      float ownTop)
    {
      if (neighbourX < 0 || neighbourX >= width || neighbourZ < 0 || neighbourZ >= height) return false;
      int neighbour = neighbourZ * width + neighbourX;
      return kinds[neighbour] != CellKind.None && tops[neighbour] >= ownTop - TopSeamEpsilon;
    }

    private static Vector3 ToWorld(
      S2MWallSettings settings,
      int width,
      int height,
      Vector2 gridCorner,
      float rawHeight)
    {
      float x = settings.FlipX ? width - gridCorner.x : gridCorner.x;
      float z = settings.FlipZ ? height - gridCorner.y : gridCorner.y;
      return new Vector3(
        x * settings.HorizontalCellSize,
        rawHeight * settings.HeightUnitScale,
        z * settings.HorizontalCellSize);
    }

    private static Material CreateMaterial(S2MWallSettings settings, string relativeTexturePath, string name, Color fallbackColor)
    {
      Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
      if (shader == null) throw new InvalidOperationException("No fallback wall shader is available.");
      var material = new Material(shader) { name = name };
      material.color = fallbackColor;
      if (string.IsNullOrWhiteSpace(settings.GameInstallPath)
        || string.IsNullOrWhiteSpace(relativeTexturePath))
      {
        return material;
      }
      try
      {
        string texturePath = Path.Combine(settings.GameInstallPath, relativeTexturePath);
        Texture2D texture = DdsTextureLoader.Load(texturePath);
        // Building-texture DDS alpha is light/spec data, not a cutout mask;
        // walls render fully opaque.
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
      }
      catch (Exception exception)
      {
        Debug.LogWarning($"Wall texture '{relativeTexturePath}' unavailable ({exception.Message}); using a flat colour instead.");
      }
      return material;
    }

    private sealed class SurfaceBuffer
    {
      public readonly List<Vector3> Vertices = new();
      public readonly List<Vector3> Normals = new();
      public readonly List<Vector2> UVs = new();
      public readonly List<int> Triangles = new();
    }
  }
}
