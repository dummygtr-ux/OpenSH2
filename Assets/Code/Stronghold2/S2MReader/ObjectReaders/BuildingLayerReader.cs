using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class BuildingLayerReader : ObjectReader
  {
    public BuildingLayerReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      for (int i = 0; i < 5; i++) reader.ReadInt32();
      ReadDataPayloadMarker(reader, isEmpty: false);

      int cellCount = reader.ReadInt32();
      if (cellCount <= 0 || cellCount > 16 * 1024 * 1024)
        throw new InvalidDataException($"Invalid BuildingLayer cell count {cellCount}.");

      var instanceIds = new ushort[cellCount];
      for (int i = 0; i < instanceIds.Length; i++) instanceIds[i] = reader.ReadUInt16();
      ReadObjectTrailerMarker(reader);

      int side = (int)Math.Sqrt(cellCount);
      return new BuildingLayer
      {
        Width = side * side == cellCount ? side : 0,
        Height = side * side == cellCount ? side : 0,
        InstanceIds = instanceIds
      };
    }
  }
}
