using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.Audio
{
  /// <summary>
  /// Plays one of the game's own play lists, in order, on an AudioSource:
  /// track, then its *silence(N) gap as real time, then the next track, until
  /// *end() stops it and says where it stopped.
  ///
  /// The machine is driven by an explicit CLOCK rather than by the source's own
  /// state, and that is a deliberate, measured decision: in batchmode
  /// (-executeMethod) there is no player loop, so AudioSource.time stays 0.0000
  /// forever and nothing about the source can tell a caller that a track ended.
  /// <see cref="Tick"/> therefore takes the elapsed seconds and does all the
  /// bookkeeping itself - Play mode feeds it Time.unscaledDeltaTime from
  /// <see cref="Update"/>, and a harness with no player loop feeds it by hand
  /// (S2MMusicRun drives a whole play list that way). One code path, both modes.
  ///
  /// What a caller can ask while it runs: the play list it is on, the item's
  /// index and kind, the item's own file name and resolved path, the clip that
  /// is on the source, how much of the current gap is left, and how much time
  /// the play list has consumed. A track that resolves to no file is SKIPPED
  /// and named (never guessed at), and the play list carries on - a caller that
  /// wants to know says so via <see cref="LastReason"/> and
  /// <see cref="ItemsSkipped"/>.
  ///
  /// The volume comes from the shipped volume list, mapped by
  /// <see cref="S2MMusicCatalogue.Gain"/> (the file's own 0..127 scale); a track
  /// the list has no row for plays at full scale, which no shipped play list
  /// actually needs - the one track the list misses is the null play list's
  /// unresolvable "null.wav".
  /// </summary>
  public class S2MMusicPlayer : MonoBehaviour
  {
    /// <summary>
    /// Play mode feeds the clock from Update. A caller that drives
    /// <see cref="Tick"/> itself (a batchmode harness, which has no player
    /// loop) sets this false, so the same object cannot be ticked twice.
    /// </summary>
    public bool TickInUpdate = true;

    private S2MMusicCatalogue catalogue;
    private AudioSource source;
    private string installPath;
    private AudioClip currentClip;

    /// <summary>The catalogue the player reads; null until Configure or UseCatalogue.</summary>
    public S2MMusicCatalogue Catalogue => catalogue;

    /// <summary>The AudioSource music plays on (created on first use).</summary>
    public AudioSource Source
    {
      get
      {
        if (source == null)
        {
          source = GetComponent<AudioSource>();
          if (source == null) source = gameObject.AddComponent<AudioSource>();
          source.playOnAwake = false;
          source.loop = false;
          source.spatialBlend = 0f;   // music is not positional
        }
        return source;
      }
    }

    /// <summary>The play list being played, or null.</summary>
    public S2MMusicCatalogue.Playlist Current { get; private set; }
    /// <summary>The current item's index into the play list, -1 before the first.</summary>
    public int Index { get; private set; } = -1;
    /// <summary>The current item (a track, a gap or the end), or null.</summary>
    public S2MMusicCatalogue.Item CurrentItem { get; private set; }
    /// <summary>The clip on the source, or null while a gap runs.</summary>
    public AudioClip CurrentClip => currentClip;
    /// <summary>True once *end() (or the end of the item list) has been reached.</summary>
    public bool IsFinished { get; private set; }
    /// <summary>True while a play list is running and has not finished.</summary>
    public bool IsPlaying => Current != null && !IsFinished;
    /// <summary>Seconds left of the current *silence() gap (0 while a track plays).</summary>
    public float SilenceRemaining { get; private set; }
    /// <summary>Seconds the current item has run.</summary>
    public float ItemElapsed { get; private set; }
    /// <summary>Seconds this play list has consumed since Play, gaps included.</summary>
    public float TotalElapsed { get; private set; }
    /// <summary>Tracks that reached the source.</summary>
    public int TracksPlayed { get; private set; }
    /// <summary>Silence gaps entered.</summary>
    public int GapsPlayed { get; private set; }
    /// <summary>Tracks stepped over because they resolve to no file.</summary>
    public int ItemsSkipped { get; private set; }
    /// <summary>The cells of those skipped tracks, in the order they were met.</summary>
    public readonly List<string> SkippedFiles = new List<string>();
    /// <summary>Why the play list finished, or why the last item was skipped.</summary>
    public string LastReason { get; private set; }

    /// <summary>One line: which play list, which item, which file, what gain.</summary>
    public string Status
    {
      get
      {
        if (Current == null) return "no play list";
        string where = $"{Current.Id}[{Index + 1}/{Current.Items.Count}]";
        if (IsFinished) return $"{where} finished: {LastReason}";
        if (CurrentItem == null) return $"{where} idle";
        if (CurrentItem.Kind == "silence")
          return $"{where} silence {SilenceRemaining:F3} s of {CurrentItem.Seconds:F0} s left";
        return $"{where} track '{CurrentItem.File}' -> {CurrentItem.Path} "
               + $"({ItemElapsed:F3} s of {currentClip?.length:F3} s, "
               + $"gain {(source == null ? 0f : source.volume):F3})";
      }
    }

    /// <summary>The install root the catalogue is read from on first Play.</summary>
    public void Configure(string gameInstallPath)
    {
      installPath = gameInstallPath;
      catalogue = null;
    }

    /// <summary>Play through a catalogue the caller already loaded (and checked).</summary>
    public void UseCatalogue(S2MMusicCatalogue loaded)
    {
      catalogue = loaded;
      installPath = null;
    }

    /// <summary>
    /// Starts a play list by id from its first item. False with a reason when
    /// the id is not in the shipped sheet, or when no catalogue could be read -
    /// the two cases a caller has to tell apart, and neither throws. A play
    /// list whose every track is unresolvable still STARTS: it skips, reaches
    /// *end() and finishes, naming what it skipped (PL_NULL, as shipped).
    /// </summary>
    public bool Play(string playlistId, out string reason)
    {
      if (catalogue == null)
      {
        if (string.IsNullOrEmpty(installPath))
        {
          reason = "no catalogue: Configure(install path) or UseCatalogue first";
          return false;
        }
        catalogue = S2MMusicCatalogue.Load(installPath, out reason);
        if (catalogue == null) return false;
      }
      if (!catalogue.TryGetPlaylist(playlistId, out S2MMusicCatalogue.Playlist playlist,
        out reason))
      {
        Current = null;
        CurrentItem = null;
        return false;
      }

      Current = playlist;
      Index = -1;
      CurrentItem = null;
      IsFinished = false;
      SilenceRemaining = 0f;
      ItemElapsed = 0f;
      TotalElapsed = 0f;
      TracksPlayed = 0;
      GapsPlayed = 0;
      ItemsSkipped = 0;
      SkippedFiles.Clear();
      LastReason = null;
      currentClip = null;
      Source.Stop();
      Advance();
      return true;
    }

    /// <summary>Stops where it is; the reason a caller gives is what Status reports.</summary>
    public void Stop(string reason = null)
    {
      IsFinished = true;
      if (!string.IsNullOrEmpty(reason)) LastReason = reason;
      if (source != null) source.Stop();
      SilenceRemaining = 0f;
    }

    /// <summary>
    /// Advances the machine by <paramref name="deltaSeconds"/> of play list
    /// time. Time left over at an item's boundary carries into the next item,
    /// so one big step is the same as many small ones (which is how a harness
    /// spends a 10,000 s gap without waiting 10,000 s) - the machine is exact
    /// about the durations the sheet gave it, and the caller decides how finely
    /// it samples real time.
    /// </summary>
    public void Tick(float deltaSeconds)
    {
      if (Current == null || IsFinished || deltaSeconds <= 0f) return;
      float remaining = deltaSeconds;
      TotalElapsed += deltaSeconds;
      // Bounded by the item count: every pass either spends time or moves to
      // the next item, and *end() (or the last item) ends the walk.
      int guard = Current.Items.Count + 2;
      while (remaining > 0f && !IsFinished && guard-- > 0)
      {
        if (CurrentItem == null || CurrentItem.Kind == "end")
        {
          Finish($"*end() after {Index + 1} item(s)");
          return;
        }
        if (CurrentItem.Kind == "silence")
        {
          float step = Mathf.Min(remaining, SilenceRemaining);
          SilenceRemaining -= step;
          ItemElapsed += step;
          remaining -= step;
          if (SilenceRemaining <= 0.0001f) Advance();
          continue;
        }
        float length = currentClip != null ? currentClip.length : 0f;
        float left = length - ItemElapsed;
        float spent = Mathf.Min(remaining, left);
        ItemElapsed += spent;
        remaining -= spent;
        if (ItemElapsed >= length - 0.0001f) Advance();
      }
    }

    private void Update()
    {
      if (TickInUpdate) Tick(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Moves to the next item and, for a track, puts its clip on the source -
    /// skipping (and naming) a track that resolves to no file. Called by Play
    /// and by Tick at an item's boundary.
    /// </summary>
    private void Advance()
    {
      while (true)
      {
        Index++;
        ItemElapsed = 0f;
        SilenceRemaining = 0f;
        if (Current == null || Index >= Current.Items.Count)
        {
          Finish($"the play list ran out of items at {Index}");
          return;
        }
        S2MMusicCatalogue.Item item = Current.Items[Index];
        CurrentItem = item;
        if (item.Kind == "end")
        {
          Finish($"{Current.Id}: *end() after {Index} item(s)");
          return;
        }
        if (item.Kind == "silence")
        {
          SilenceRemaining = item.Seconds;
          GapsPlayed++;
          return;
        }
        if (!item.Resolved)
        {
          ItemsSkipped++;
          SkippedFiles.Add(item.File);
          LastReason = $"{Current.Id}: '{item.File}' resolves to no file"
                       + (string.IsNullOrEmpty(catalogue.Locate(item.File))
                         ? " (no such file anywhere in the install)" : "");
          continue;
        }
        if (!catalogue.TryGetClip(item, out AudioClip clip, out string why))
        {
          ItemsSkipped++;
          SkippedFiles.Add(item.File);
          LastReason = $"{Current.Id}: {why}";
          continue;
        }
        currentClip = clip;
        Source.clip = clip;
        Source.volume = S2MMusicCatalogue.Gain(item.Volume);
        Source.Play();
        TracksPlayed++;
        return;
      }
    }

    private void Finish(string reason)
    {
      IsFinished = true;
      LastReason = reason;
      if (source != null) source.Stop();
      SilenceRemaining = 0f;
    }
  }
}
