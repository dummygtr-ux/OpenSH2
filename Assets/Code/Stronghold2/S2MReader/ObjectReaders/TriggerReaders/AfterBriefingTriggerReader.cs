using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class AfterBriefingTriggerReader : TriggerReader
  {
    public AfterBriefingTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      AfterBriefingTrigger obj = new();

      ReadTriggerHeader(reader);
      // No data: the payload marker and instance index are the payload's whole
      // body (25 bytes serialized, measured over all 41 shipped instances).
      ReadDataPayloadMarker(reader, true);
      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
