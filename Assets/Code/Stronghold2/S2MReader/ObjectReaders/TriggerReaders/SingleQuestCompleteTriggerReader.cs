using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class SingleQuestCompleteTriggerReader : TriggerReader
  {
    public SingleQuestCompleteTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      SingleQuestCompleteTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // 0-based quest index; war_chapter4 pairs quest 0/1/2 with the cut scene
      // ids 0/1/2 of its three cutscene events.
      obj.Quest = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
