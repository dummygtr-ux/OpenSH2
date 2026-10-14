using Assets.Code.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Code.Video
{
  class VideoManager
  {
    public Task introVideosTask = null;
    private VideoPlayerUI activeIntroVideoPlayer;

    /// <summary>
    /// Which clip the active player came from, as the caller named it
    /// (VideoFilePaths.FireflyLogo / Intro) - the READABLE name, since the
    /// player itself only knows the cached file it plays.
    /// </summary>
    private string activeIntroVideoSourcePath;

    public VideoManager()
    {
    }

    /// <summary>
    /// Plays a video at full screen. This function will assume the provided video file path
    /// is a .bik video and has already been cached to its configured output format.
    /// </summary>
    /// <param name="videoFilePath"></param>
    /// <returns></returns>
    public Task PlayFullscreenBinkVideo(string videoFilePath, int fadeOutMs = 0)
    {
      // Fetch the video from the videos cache
      string cachedVideoPath = VideoFilePaths.GetCachedVideoPath(videoFilePath);

      var tcs = new TaskCompletionSource<object>(
        TaskCreationOptions.RunContinuationsAsynchronously);

      Debug.Log("Trying?");
      activeIntroVideoSourcePath = videoFilePath;
      activeIntroVideoPlayer = VideoPlayerUI.Play(cachedVideoPath, delegate ()
      {
        activeIntroVideoPlayer = null;
        activeIntroVideoSourcePath = null;
        tcs.TrySetResult(null);
      }, fadeOutMs);

      if (activeIntroVideoPlayer == null)
      {
        activeIntroVideoSourcePath = null;
        tcs.TrySetResult(null);
      }

      return tcs.Task;
    }

    public void SkipCurrentIntroVideo()
    {
      if (activeIntroVideoPlayer == null)
      {
        return;
      }

      activeIntroVideoPlayer.Stop(invokeFinishedCallback: true);
    }

    /// <summary>
    /// Whether the clip that is playing right now has STARTED - the fact a
    /// caller has to read before it skips one (see VideoPlayerUI.HasStarted for
    /// why a mid-prepare stop is not allowed), and false between two clips
    /// while the next player is preparing.
    /// </summary>
    public bool ActiveIntroVideoStarted =>
      activeIntroVideoPlayer != null && activeIntroVideoPlayer.HasStarted;

    /// <summary>
    /// The active clip's readable name (the file the caller asked for, e.g.
    /// "FireflyLogo.bik"), or null while no clip is playing - for the log lines
    /// that say which clip a budget skipped.
    /// </summary>
    public string ActiveIntroVideoName =>
      activeIntroVideoSourcePath == null ? null : Path.GetFileName(activeIntroVideoSourcePath);
  }
}
