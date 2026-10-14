using System;
using System.IO;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// What a shipped audio FILE says about itself, read from its own bytes.
  ///
  /// This exists to be the independent side of the clip check: a decoded
  /// AudioClip is only trustworthy if its sample rate, channel count and
  /// length agree with the file the decoder was handed, and that agreement has
  /// to be measured against something the decoder did not produce. Nothing
  /// here plays or decodes anything - it reads headers.
  ///
  /// Two formats, both measured on the install rather than assumed:
  ///
  ///   WAV  RIFF/WAVE. 105 of the 1,816 shipped effects are PCM (format tag 1)
  ///        and the other 1,711 are IMA ADPCM (tag 17) - so the frame count
  ///        has to be derived per codec: PCM is dataSize / blockAlign, and
  ///        ADPCM is a per-block count (a block holds 4*channels header bytes
  ///        and 4 bits per sample per channel, so
  ///        ((blockAlign - 4*channels) / (4*channels)) * 8 + 1 frames).
  ///
  ///   MP3  MPEG-1/2 layer III. The file is walked FRAME by FRAME: the ID3v2
  ///        tag at the front and an ID3v1 trailer at the back are skipped, and
  ///        every frame header gives the sample rate, channel mode, bitrate
  ///        and its own byte length, so the sample total is exact. (Unity's
  ///        decoder reports two more frames of priming/padding than the
  ///        bitstream holds - measured, see S2MAudioRun - which is why the
  ///        length check carries a tolerance in frames, not a percentage.)
  /// </summary>
  public static class S2MAudioFileHeader
  {
    /// <summary>What a WAV header says: format, rate, channels, frames, bytes.</summary>
    public class Wav
    {
      public int FormatTag;
      public int Channels;
      public int SampleRate;
      public int BitsPerSample;
      public int BlockAlign;
      public long DataBytes;
      public long Frames;
      /// <summary>
      /// The size of the file that was READ - the target's size, not a
      /// symlink's: the port reads a symlink mirror, where FileInfo.Length
      /// reports the length of the link path itself (measured: 75 "bytes" for
      /// a 2.9 s PCM file). Reading the bytes is the only honest size.
      /// </summary>
      public long FileBytes;
      public bool IsAdpcm => FormatTag == 17;
      public float Length => SampleRate > 0 ? (float)Frames / SampleRate : 0f;
    }

    /// <summary>What an MP3 header says: first frame, rate, channels, frames, samples.</summary>
    public class Mp3
    {
      public int SampleRate;
      public int Channels;
      public int BitrateKbps;
      public int Frames;
      public long Samples;
      public long Id3v2Bytes;
      public bool HasId3v1;
      /// <summary>Bytes of the file that are not inside a parsed frame.</summary>
      public long StrayBytes;
      /// <summary>The size of the file that was read (see <see cref="Wav.FileBytes"/>).</summary>
      public long FileBytes;
      public float Length => SampleRate > 0 ? (float)Samples / SampleRate : 0f;
    }

    private static readonly int[] BitrateV1 =
      { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, -1 };
    private static readonly int[] BitrateV2 =
      { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, -1 };
    private static readonly int[] RateV1 = { 44100, 48000, 32000, -1 };
    private static readonly int[] RateV2 = { 22050, 24000, 16000, -1 };
    private static readonly int[] RateV25 = { 11025, 12000, 8000, -1 };

    /// <summary>Reads a RIFF/WAVE header, or null with a reason.</summary>
    public static Wav ReadWav(string path, out string reason)
    {
      reason = null;
      byte[] data = File.ReadAllBytes(path);
      if (data.Length < 44 || Tag(data, 0) != "RIFF" || Tag(data, 8) != "WAVE")
      {
        reason = "not a RIFF/WAVE file";
        return null;
      }

      var wav = new Wav { FileBytes = data.Length };
      int at = 12;
      while (at + 8 <= data.Length)
      {
        string id = Tag(data, at);
        long size = BitConverter.ToUInt32(data, at + 4);
        if (id == "fmt " && at + 8 + 16 <= data.Length)
        {
          wav.FormatTag = BitConverter.ToUInt16(data, at + 8);
          wav.Channels = BitConverter.ToUInt16(data, at + 10);
          wav.SampleRate = (int)BitConverter.ToUInt32(data, at + 12);
          wav.BlockAlign = BitConverter.ToUInt16(data, at + 20);
          wav.BitsPerSample = BitConverter.ToUInt16(data, at + 22);
        }
        else if (id == "data")
        {
          wav.DataBytes = Math.Min(size, data.Length - (at + 8));
          break;
        }
        at += 8 + (int)size + ((int)size & 1);
      }

      if (wav.SampleRate == 0 || wav.Channels == 0)
      {
        reason = "no fmt chunk";
        return null;
      }
      wav.Frames = wav.IsAdpcm ? AdpcmFrames(wav) : wav.DataBytes / Math.Max(1, wav.BlockAlign);
      return wav;
    }

    /// <summary>
    /// IMA ADPCM frames in a data chunk: whole blocks plus a partial tail.
    /// Each block holds 4*channels header bytes and then 4 bits per sample per
    /// channel, so a full block is ((blockAlign - 4*channels) / (4*channels))
    /// * 8 + 1 frames.
    /// </summary>
    private static long AdpcmFrames(Wav wav)
    {
      int block = wav.BlockAlign > 0 ? wav.BlockAlign : 4 * wav.Channels;
      long perBlock = ((block - 4L * wav.Channels) / (4L * wav.Channels)) * 8 + 1;
      long blocks = wav.DataBytes / block;
      long leftover = wav.DataBytes - blocks * block;
      long tail = leftover > 4L * wav.Channels
        ? ((leftover - 4L * wav.Channels) / (4L * wav.Channels)) * 8 + 1
        : 0;
      return blocks * perBlock + tail;
    }

    /// <summary>Reads an MP3 frame header and walks the frames, or null with a reason.</summary>
    public static Mp3 ReadMp3(string path, out string reason)
    {
      reason = null;
      byte[] data = File.ReadAllBytes(path);
      long start = 0;
      if (data.Length > 10 && Tag(data, 0) == "ID3")
      {
        // The ID3v2 size is four 7-bit bytes, high bit clear.
        start = 10L + ((data[6] & 0x7F) << 21 | (data[7] & 0x7F) << 14
                       | (data[8] & 0x7F) << 7 | (data[9] & 0x7F));
      }
      long end = data.Length;
      bool id3v1 = end >= 128 && Tag(data, (int)(end - 128)) == "TAG";
      if (id3v1) end -= 128;
      if (start >= end)
      {
        reason = "no audio after the ID3 tag";
        return null;
      }

      var mp3 = new Mp3 { Id3v2Bytes = start, HasId3v1 = id3v1, FileBytes = data.Length };
      long at = start;
      long consumed = 0;
      while (at + 4 <= end)
      {
        if (!(data[at] == 0xFF && (data[at + 1] & 0xE0) == 0xE0))
        {
          at++;
          continue;
        }
        int version = (data[at + 1] >> 3) & 3;   // 3 = MPEG1, 2 = MPEG2, 0 = MPEG2.5
        int layer = (data[at + 1] >> 1) & 3;     // 1 = layer III
        int bitrateIndex = data[at + 2] >> 4;
        int rateIndex = (data[at + 3] >> 2) & 3;
        int padding = (data[at + 2] >> 1) & 1;
        if (version == 1 || layer != 1 || bitrateIndex == 0 || bitrateIndex == 15
          || rateIndex == 3)
        {
          at++;
          continue;
        }

        int rate, bitrate, samplesPerFrame;
        if (version == 3) { rate = RateV1[rateIndex]; bitrate = BitrateV1[bitrateIndex]; samplesPerFrame = 1152; }
        else if (version == 2) { rate = RateV2[rateIndex]; bitrate = BitrateV2[bitrateIndex]; samplesPerFrame = 1152; }
        else { rate = RateV25[rateIndex]; bitrate = BitrateV2[bitrateIndex]; samplesPerFrame = 576; }
        long frameBytes = (long)(samplesPerFrame / 8) * bitrate * 1000 / rate + padding;
        if (frameBytes < 4 || at + frameBytes > end)
        {
          at++;
          continue;
        }

        if (mp3.Frames == 0)
        {
          mp3.SampleRate = rate;
          // The channel mode's top two bits: 3 is single-channel (mono).
          mp3.Channels = (data[at + 3] >> 6) == 3 ? 1 : 2;
          mp3.BitrateKbps = bitrate;
        }
        mp3.Frames++;
        mp3.Samples += samplesPerFrame;
        consumed += frameBytes;
        at += frameBytes;
      }

      if (mp3.Frames == 0)
      {
        reason = "no MPEG audio frame found";
        return null;
      }
      mp3.StrayBytes = end - start - consumed;
      return mp3;
    }

    /// <summary>Four ASCII bytes at an offset, as a string ("" when out of range).</summary>
    private static string Tag(byte[] data, int at)
    {
      if (at < 0 || at + 4 > data.Length) return string.Empty;
      return new string(new[] { (char)data[at], (char)data[at + 1],
        (char)data[at + 2], (char)data[at + 3] });
    }
  }
}
