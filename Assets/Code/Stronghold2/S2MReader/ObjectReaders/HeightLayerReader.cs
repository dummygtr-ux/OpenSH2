using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class HeightLayerReader : ObjectReader
  {
    // Payload header: {tag=3, headerLength, id, unused, recordCount} with
    // headerLength = 12 + 16 * recordCount. When recordCount > 0 the 20-byte
    // header is followed by that many 16-byte pre-marker records (contents
    // unknown) and only then by the A5 C1 FF FF data payload marker — reading
    // the marker straight after the header fails on those maps.
    // Verified on all 67 shipped .s2m/.s2c files: marker offset = 20 +
    // headerLength - 12 = 20 + 16 * recordCount (war_chapter1 12/0;
    // war_chapter8 108/6; up to 34 records on World_Europe).
    private const int HeightLayerHeaderLength = 12;
    private const int PreMarkerRecordSize = 16;

    public HeightLayerReader(S2Object obj) : base(obj) { }

    public override S2Object Read(BinaryReader reader)
    {
      reader.ReadInt32();
      int headerLength = reader.ReadInt32();
      reader.ReadInt32();
      reader.ReadInt32();
      int recordCount = reader.ReadInt32();
      int preMarkerBytes = headerLength - HeightLayerHeaderLength;
      if (preMarkerBytes < 0 || preMarkerBytes != recordCount * PreMarkerRecordSize)
        throw new InvalidDataException(
          $"HeightLayer header length {headerLength} with {recordCount} pre-marker records does not match the 12 + 16 * recordCount layout.");
      for (int i = 0; i < preMarkerBytes; i++) reader.ReadByte();

      ReadDataPayloadMarker(reader, isEmpty: false);

      int valueCount = reader.ReadInt32();
      if (valueCount <= 0 || valueCount > 16 * 1024 * 1024)
        throw new InvalidDataException($"Invalid HeightLayer primary-plane value count {valueCount}.");

      var heights = new float[valueCount];
      for (int i = 0; i < heights.Length; i++) heights[i] = reader.ReadSingle();

      int additionalBlockTag = reader.ReadInt32();
      int additionalBlockLength = reader.ReadInt32();
      if (additionalBlockTag != 2 || additionalBlockLength <= 0)
        throw new InvalidDataException(
          $"Invalid HeightLayer corner-height block (tag {additionalBlockTag}, length {additionalBlockLength}).");

      int valuesPerPlane = checked(valueCount * HeightLayer.CornersPerCell);
      int bytesPerPlane = checked(sizeof(int) + valuesPerPlane * sizeof(float));
      if (additionalBlockLength % bytesPerPlane != 0)
        throw new InvalidDataException("HeightLayer corner-height block is not a whole number of counted four-corner planes.");

      int planeCount = additionalBlockLength / bytesPerPlane;
      var cornerHeightPlanes = new float[planeCount][];
      for (int plane = 0; plane < planeCount; plane++)
      {
        int cellCount = reader.ReadInt32();
        if (cellCount != valueCount)
          throw new InvalidDataException(
            $"HeightLayer corner-height plane {plane} has {cellCount} cells; expected {valueCount}.");

        var cornerHeights = new float[valuesPerPlane];
        for (int i = 0; i < cornerHeights.Length; i++) cornerHeights[i] = reader.ReadSingle();
        cornerHeightPlanes[plane] = cornerHeights;
      }

      int side = (int)Math.Sqrt(valueCount);
      ReadObjectTrailerMarker(reader);
      return new HeightLayer
      {
        Width = side * side == valueCount ? side : 0,
        Height = side * side == valueCount ? side : 0,
        Heights = heights,
        CornerHeightPlanes = cornerHeightPlanes
      };
    }
  }
}
