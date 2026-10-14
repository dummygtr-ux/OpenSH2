using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class GongInvasionActionReader : ActionReader
  {
    public GongInvasionActionReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      GongInvasionAction obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      // The estate's flag is the port's usual color + number pair, in the same
      // order InvasionAction reads its invasion and target points.
      obj.EstatePointFlagColor = (FlagColor)reader.ReadInt32();
      obj.EstatePointFlagNumber = reader.ReadInt32();
      obj.NumGongPiles = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
