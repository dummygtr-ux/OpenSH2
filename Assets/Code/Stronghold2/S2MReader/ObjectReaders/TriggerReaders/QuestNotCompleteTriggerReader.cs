using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class QuestNotCompleteTriggerReader : TriggerReader
  {
    public QuestNotCompleteTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      QuestNotCompleteTrigger obj = new();

      ReadTriggerHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // 0-based quest index, the same space SingleQuestCompleteTrigger uses.
      obj.Quest = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
