using System;
using System.Collections.Generic;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>Placed landscape objects stored by the S2M Forest object.</summary>
  public sealed class Forest : S2Object
  {
    public IReadOnlyList<ForestInstance> Instances { get; internal set; } = Array.Empty<ForestInstance>();
    public byte[] TrailingData { get; internal set; } = Array.Empty<byte>();
    public byte[] RawPayload { get; internal set; } = Array.Empty<byte>();
  }

  /// <summary>A decoded 65-byte Forest placement record.</summary>
  public sealed class ForestInstance
  {
    public float RawX { get; internal set; }
    /// <summary>
    /// Raw 2nd float of the record. NOT a height: no tree's value matches the
    /// terrain surface under it (war_chapter1: 0 of 712 within their cell's
    /// corner range, 237 are exactly 0 on 10-20 m ground; the 40 sea-cell trees
    /// all store the map maximum 10240). The renderer grounds trees on the
    /// HeightLayer surface instead - see S2MVegetationRenderer.
    /// </summary>
    public float RawY { get; internal set; }
    public float RawZ { get; internal set; }
    public float RotationDegrees { get; internal set; }
    public float ScaleX { get; internal set; }
    public float ScaleY { get; internal set; }
    public float AppearanceScale { get; internal set; }
    public float TintRed { get; internal set; }
    public float TintGreen { get; internal set; }
    public float TintBlue { get; internal set; }
    public int RandomValue { get; internal set; }
    public ushort CellX { get; internal set; }
    public ushort CellZ { get; internal set; }
    public ushort Family { get; internal set; }
    public ushort Variant { get; internal set; }
    public byte[] UnknownTail { get; internal set; } = Array.Empty<byte>();

    /// <summary>
    /// Map cell of the tree (the record's cellX/cellZ plus a 0..1 fraction,
    /// jittered around the cell centre). The forest payload stores x/z at 2048
    /// raw units per cell - TWICE the building/height resolution - so raw/1024
    /// would place every tree at twice its distance from the map origin.
    /// Verified against the record's own cellX/cellZ on war_chapter1,
    /// The Four Woods and Fenis (|raw/2048 - cellX| <= 0.75, mean 0.50).
    /// </summary>
    public float MapX => RawX / S2MWorldScale.ForestRawUnitsPerCell;
    public float MapZ => RawZ / S2MWorldScale.ForestRawUnitsPerCell;
    public bool IsKnownTree => Family <= 2;
  }
}
