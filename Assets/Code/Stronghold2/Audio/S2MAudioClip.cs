using System;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// Decoding one shipped audio file into an AudioClip - the single path both
  /// catalogues use, so the effect, speech and music readers cannot drift into
  /// three different decode behaviours.
  ///
  /// <c>UnityWebRequestMultimedia</c> decodes every format the install ships on
  /// this platform: the effects are .wav (mostly IMA ADPCM, format tag 17,
  /// 22.05 kHz mono) and the speech and music are .mp3 (MPEG-1 layer III).
  /// Loading BLOCKS the calling thread until the request finishes (a few ms to
  /// a few tens of ms per file), because the harnesses run with no player loop
  /// to yield to; a Play-mode caller pays that once per file, on the frame it
  /// first plays it, and callers cache the result.
  ///
  /// Nothing here decides WHICH file to load - that is the sheets' business,
  /// and <see cref="S2MPathResolver"/>'s.
  /// </summary>
  public static class S2MAudioClip
  {
    /// <summary>The decode deadline: a 20 s stall is a failure, not a slow file.</summary>
    private const double TimeoutSeconds = 20.0;

    /// <summary>
    /// Decodes one audio file, or returns null with a one-line reason. Never
    /// throws: a missing file, a failed request and a decoder that returns
    /// nothing are all named results, because every one of them is a case a
    /// caller has to tell apart from "it played".
    /// </summary>
    public static AudioClip Load(string absolutePath, out string error)
    {
      error = null;
      if (!File.Exists(absolutePath))
      {
        error = $"no file at '{absolutePath}'";
        return null;
      }
      var type = absolutePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        ? AudioType.MPEG : AudioType.WAV;
      UnityWebRequest request =
        UnityWebRequestMultimedia.GetAudioClip(new Uri(absolutePath).AbsoluteUri, type);
      UnityWebRequestAsyncOperation operation = request.SendWebRequest();
      DateTime deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
      while (!operation.isDone && DateTime.UtcNow < deadline) Thread.Sleep(5);
      if (!operation.isDone)
      {
        request.Dispose();
        error = $"the decode request did not finish within {TimeoutSeconds:F0} s";
        return null;
      }
      if (request.result != UnityWebRequest.Result.Success)
      {
        error = $"decode failed: {request.result} '{request.error}'";
        request.Dispose();
        return null;
      }
      AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
      request.Dispose();
      if (clip == null) error = "the decoder returned no clip";
      return clip;
    }
  }
}
