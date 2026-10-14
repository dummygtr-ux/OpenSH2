using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Triggers;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.TriggerReaders
{
  internal class QuestCompleteTriggerReader : TriggerReader
  {
    public QuestCompleteTriggerReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      QuestCompleteTrigger obj = new();

      ReadTriggerHeader(reader);
      // The data payload this type declares is exactly three bytes - one flag per
      // quest slot - unlike its sibling triggers, whose payloads are int-aligned.
      ReadDataPayloadMarker(reader, false);

      obj.Quest0Complete = reader.ReadBoolean();
      obj.Quest1Complete = reader.ReadBoolean();
      obj.Quest2Complete = reader.ReadBoolean();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
