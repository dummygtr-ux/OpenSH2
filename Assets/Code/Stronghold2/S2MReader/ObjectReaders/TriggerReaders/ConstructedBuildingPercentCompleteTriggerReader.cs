using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class ConstructedBuildingPercentCompleteTriggerReader : TriggerReader
  {
    public ConstructedBuildingPercentCompleteTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      ConstructedBuildingPercentCompleteTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // 0 in all 5 shipped instances: read and dropped, the same unidentified
      // leading int the complete sibling carries.
      reader.ReadInt32();

      obj.Percentage = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
