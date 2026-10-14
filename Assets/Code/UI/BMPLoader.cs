using System;
using System.IO;
using UnityEngine;

namespace Assets.Code.UI
{
  /// <summary>
  /// A reader for the install's own BMP art. Unity's
  /// <c>ImageConversion.LoadImage</c> does not read BMP, and the install ships
  /// some frontend art that way - the campaign screen's one button
  /// (ui/campaign_screen/campaign_button.bmp, 251x44) among them - so
  /// <see cref="SpriteLoader"/> routes .bmp files here.
  ///
  /// What the install actually ships, measured over all 283 .bmp files in the
  /// game root: 280 are 24-bit and 3 are 32-bit, every one of them uncompressed
  /// (BI_RGB, compression 0). So this reader supports exactly those two shapes
  /// and says so for anything else rather than guessing.
  ///
  /// Two facts about the 32-bit ones: the fourth byte is nominally "reserved" in
  /// a BI_RGB bitmap, but the install's art uses it as ALPHA - over
  /// campaign_button.bmp it is 255 across the body and ~0 on the outer rim (30
  /// of 11,044 pixels) - so it is read as alpha. And a BMP stores its rows
  /// bottom-up when its height is positive, which is the orientation Unity's
  /// Texture2D uses too, so the common case needs no flip.
  /// </summary>
  public static class BMPLoader
  {
    public static Texture2D LoadBMP(string fileName)
    {
      using var stream = File.OpenRead(fileName);
      return LoadBMP(stream);
    }

    public static Texture2D LoadBMP(Stream bmpStream)
    {
      using BinaryReader reader = new(bmpStream);

      if (reader.ReadByte() != 'B' || reader.ReadByte() != 'M')
      {
        throw new Exception("BMP texture: not a BM file.");
      }

      reader.BaseStream.Seek(8, SeekOrigin.Current);  // file size + the two reserved words
      int dataOffset = reader.ReadInt32();
      int headerSize = reader.ReadInt32();
      if (headerSize < 40)
      {
        throw new Exception($"BMP texture: unsupported header size ({headerSize} bytes).");
      }

      int width = reader.ReadInt32();
      int height = reader.ReadInt32();
      reader.BaseStream.Seek(2, SeekOrigin.Current);  // planes
      int bitDepth = reader.ReadInt16();
      int compression = reader.ReadInt32();

      if (compression != 0)
      {
        throw new Exception($"BMP texture: unsupported compression ({compression}); "
                            + "the install ships uncompressed BMPs only.");
      }
      if (bitDepth != 24 && bitDepth != 32)
      {
        throw new Exception($"BMP texture: unsupported bit depth ({bitDepth}); "
                            + "the install ships 24- and 32-bit BMPs only.");
      }
      if (width <= 0 || height == 0)
      {
        throw new Exception($"BMP texture: bad dimensions ({width}x{height}).");
      }

      // A positive height means the rows are stored bottom-up, which is the
      // orientation Unity's own pixel rows use; a negative height is top-down.
      bool bottomUp = height > 0;
      int rows = Math.Abs(height);
      int bytesPerPixel = bitDepth / 8;
      int stride = ((width * bytesPerPixel + 3) / 4) * 4;  // rows are padded to 4 bytes

      var texture = new Texture2D(width, rows, TextureFormat.RGBA32, false);
      var pixels = new Color32[width * rows];
      reader.BaseStream.Seek(dataOffset, SeekOrigin.Begin);

      var line = new byte[stride];
      for (int row = 0; row < rows; row++)
      {
        int read = reader.Read(line, 0, stride);
        if (read < stride)
        {
          throw new Exception($"BMP texture: truncated at row {row} of {rows}.");
        }

        int y = bottomUp ? row : rows - 1 - row;
        int destination = y * width;
        for (int x = 0; x < width; x++)
        {
          int source = x * bytesPerPixel;
          byte blue = line[source];
          byte green = line[source + 1];
          byte red = line[source + 2];
          byte alpha = bytesPerPixel == 4 ? line[source + 3] : (byte)255;
          pixels[destination + x] = new Color32(red, green, blue, alpha);
        }
      }

      texture.SetPixels32(pixels);
      texture.Apply();
      return texture;
    }
  }
}
