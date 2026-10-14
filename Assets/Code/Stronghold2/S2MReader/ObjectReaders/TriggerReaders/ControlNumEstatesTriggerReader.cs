using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class ControlNumEstatesTriggerReader : TriggerReader
  {
    public ControlNumEstatesTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      ControlNumEstatesTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.NumEstates = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
