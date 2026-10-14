using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class ConstructedBuildingCompleteTriggerReader : TriggerReader
  {
    public ConstructedBuildingCompleteTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      ConstructedBuildingCompleteTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // 0 in all 19 shipped instances of this type (and in the percent-complete
      // sibling's 5): what the leading int selects is not established by the data,
      // so it is read and dropped rather than named.
      reader.ReadInt32();

      // -1 in every shipped instance; the percent sibling carries the real
      // percent in this position (20, 65), so -1 reads as the complete sentinel.
      obj.Percentage = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
