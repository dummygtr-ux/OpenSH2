using Assets.Code.Stronghold2.S2MReader.Resources;
using System;
using System.Collections.Generic;
using System.IO;

namespace Assets.Code.Stronghold2.S2MReader.ObjectReaders
{
  internal class ObjectReader
  {
    protected readonly S2Object Object;

    public ObjectReader(S2Object obj)
    {
      Object = obj;
    }

    /// <summary>
    /// True when at least <paramref name="count"/> payload bytes remain before the
    /// object's trailer marker. Action and trigger objects vary between map files:
    /// some writers omit optional trailing fields (InvasionAction's warning flag),
    /// so a reader must check before reading a field that may be absent.
    /// </summary>
    protected static bool HasPayloadBytes(BinaryReader reader, int count)
    {
      // The trailer is always the payload's last 4 bytes, so it is not available
      // to field readers.
      return reader.BaseStream.Length - reader.BaseStream.Position - sizeof(int) >= count;
    }

    /// <summary>
    /// Reads the marker that starts an action's or trigger's data payload:
    /// {marker}{i32 instance index} and, only when the object carries data,
    /// {i32 unknown}{i32 payload byte count}. An object with an empty payload puts
    /// its trailer straight after the instance index, so presence is decided from
    /// the bytes that remain rather than from the caller's expectation.
    /// </summary>
    /// <returns>The declared data payload byte count, 0 when the object has no data.</returns>
    protected int ReadDataPayloadMarker(BinaryReader reader, bool isEmpty)
    {
      // Data payload marker
      var payloadMarker = reader.ReadInt32();
      if (payloadMarker != S2MReaderUtils.DataPayloadMarker)
      {
        throw new InvalidDataException($"Expected data payload marker {S2MReaderUtils.DataPayloadMarker}, but got {payloadMarker}");
      }

      // Instance index of the action/trigger within its map.
      reader.ReadInt32();

      // The {unknown, payload byte count} pair exists only on data objects; both
      // ints must fit before the trailer for it to be present.
      if (!HasPayloadBytes(reader, 2 * sizeof(int))) return 0;

      reader.ReadInt32();
      // This is the length of bytes in the data payload, which readers use as the
      // authoritative size when their own field list is shorter than the data.
      return reader.ReadInt32();
    }

    public void ReadObjectTrailerMarker(BinaryReader reader)
    {
      // Read 4 object-terminator bytes (AF1EFFFF)
      var terminator = reader.ReadInt32();
      if (terminator != S2MReaderUtils.TrailerMarker)
      {
        throw new InvalidDataException($"Invalid object terminator. Got {terminator:X8}");
      }
    }

    protected byte[] ReadPayloadToTrailer(BinaryReader reader)
    {
      var payload = new List<byte>();
      var window = new byte[4];
      int windowCount = 0;

      while (reader.BaseStream.Position < reader.BaseStream.Length)
      {
        byte value = reader.ReadByte();
        payload.Add(value);
        if (windowCount < 4) window[windowCount++] = value;
        else
        {
          window[0] = window[1];
          window[1] = window[2];
          window[2] = window[3];
          window[3] = value;
        }

        if (windowCount == 4 && BitConverter.ToInt32(window, 0) == S2MReaderUtils.TrailerMarker)
        {
          payload.RemoveRange(payload.Count - 4, 4);
          return payload.ToArray();
        }
      }

      throw new EndOfStreamException($"Object '{Object.Type}' ended without an AF 1E FF FF trailer.");
    }

    public virtual S2Object Read(BinaryReader reader)
    {
      throw new NotImplementedException();
    }
  }
}
