using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class FireActionReader : ActionReader
  {
    public FireActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      FireAction obj = new();

      ReadActionHeader(reader);
      // FireAction carries data (the payload byte count is 4), so the payload
      // marker is followed by the {unknown, byte count} pair — unlike the
      // empty-payload actions such as WinAction.
      int payloadByteCount = ReadDataPayloadMarker(reader, false);
      if (payloadByteCount >= sizeof(int))
      {
        obj.NumFires = reader.ReadInt32();
        reader.ReadBytes(payloadByteCount - sizeof(int));
      }

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
