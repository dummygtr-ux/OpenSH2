using System;

namespace Assets.Code.Stronghold2.S2MReader.Resources
{
  /// <summary>
  /// Per-cell building instance grid from the S2Game BuildingLayer object.
  /// Non-zero values mark building footprints (towers, hovels, braziers,
  /// statues); a value is the building instance index and each instance forms
  /// exactly one contiguous footprint blob. Serialized row-major (z*Width+x).
  /// </summary>
  public class BuildingLayer : S2Object
  {
    public int Width { get; set; }
    public int Height { get; set; }
    /// <summary>One building instance index per map cell; zero = no building.</summary>
    public ushort[] InstanceIds { get; set; }

    public ushort GetInstanceId(int x, int z)
    {
      if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x));
      if (z < 0 || z >= Height) throw new ArgumentOutOfRangeException(nameof(z));
      return InstanceIds[z * Width + x];
    }
  }
}
