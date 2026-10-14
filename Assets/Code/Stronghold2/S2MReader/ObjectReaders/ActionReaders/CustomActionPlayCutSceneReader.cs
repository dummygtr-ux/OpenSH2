using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.S2MReader.Resources.Actions;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders.ActionReaders
{
  internal class CustomActionPlayCutSceneReader : ActionReader
  {
    public CustomActionPlayCutSceneReader(S2Object obj) : base(obj)
    {

    }

    public override S2Object Read(BinaryReader reader)
    {
      CustomActionPlayCutScene obj = new();

      ReadActionHeader(reader);
      ReadDataPayloadMarker(reader, false);

      obj.CutSceneId = reader.ReadInt32();

      ReadObjectTrailerMarker(reader);

      return obj;
    }
  }
}
