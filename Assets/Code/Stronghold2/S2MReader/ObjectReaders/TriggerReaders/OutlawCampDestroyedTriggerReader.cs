using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class OutlawCampDestroyedTriggerReader : TriggerReader
  {
    public OutlawCampDestroyedTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      OutlawCampDestroyedTrigger obj = new();

      ReadTriggerHeader(reader);
      // All 4 shipped instances use the empty-payload shape: the marker is
      // followed by just the instance index and then the trailer (29 bytes total).
      // The old reader also consumed an int after the marker block, which read the
      // trailer and threw "Invalid object terminator".
      int payloadByteCount = ReadDataPayloadMarker(reader, false);
      reader.ReadBytes(payloadByteCount);

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
