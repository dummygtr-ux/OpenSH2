using Assets.Code.Stronghold2.Audio;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// The music UAT, headless: the shipped play lists, the shipped volume list,
  /// and a real play list played through the runtime player.
  ///
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MMusicRun.Main -quit
  ///
  /// Compute-only: nothing is rendered and nothing is clicked, so this runs
  /// under the plain run-batchmode-check.sh recipe (-nographics).
  ///
  /// There is NO SPEAKER in batchmode - a null audio driver - so nothing here
  /// asserts that anything was audible, exactly as the audio UAT does not. What
  /// it asserts is everything up to the speaker:
  ///
  ///   the sheet      the runtime reader and the python tool's manifest agree
  ///                  on all 478 items: same 90 play lists, same item order,
  ///                  same kinds, same resolved paths, same silence seconds,
  ///                  same volume-list values, same rule counts;
  ///   the clips      each of the UAT play list's tracks, its channels, sample
  ///                  rate and sample count compared against the FILE'S OWN
  ///                  MPEG frames - the decoder is not its own witness - plus a
  ///                  sample of the shipped music spanning its shortest and
  ///                  longest tracks, which is what measures how far Unity's
  ///                  decoder may sit above a file's own frames;
  ///   the play       PL_CS_WAR_M15_01 played on the WALL CLOCK, in order:
  ///                  Stinger_drama_01.mp3, a six-second *silence(06) gap, then
  ///                  Stinger_danger_01.mp3, each transition measured in real
  ///                  seconds against the clip's own length and the sheet's own
  ///                  gap. The clock is fed into S2MMusicPlayer.Tick by hand
  ///                  because -executeMethod has no player loop - the same
  ///                  method Play mode feeds from Update - and the order of
  ///                  events, not the tick rate, is what the checks rest on;
  ///   the sentinel   the 10,000 s *silence(10000) is spent by feeding the
  ///                  clock 10,000 s (waiting 2 h 46 m is not a test), after
  ///                  which the play list reaches *end() and says so;
  ///   the silences   a play list id that does not exist plays nothing and
  ///                  names itself; PL_NULL, which ships as a play list whose
  ///                  only track is an unresolvable "null.wav", plays nothing
  ///                  and names the file; and a track that resolves to no file
  ///                  (Chantloop1.wav in PL_MED_POP_EARLY) is SKIPPED and named
  ///                  while its neighbours still play;
  ///   the gains      the volume list's own values reach the source: 127 as 1.00
  ///                  full scale, and the UAT's two tracks at 85 and 100.
  /// </summary>
  public static class S2MMusicRun
  {
    public static string GameRoot => Constants.GamePath;

    /// <summary>The manifest the python tool wrote - the cross-check's other side.</summary>
    private static string Manifest =>
      Path.Combine(Constants.PortRoot, "manifests", "audio_music.csv");

    /// <summary>
    /// The play list the real-time run drives, chosen because it is the shipped
    /// sheet's SHORTEST complete track -> gap -> track -> end sequence: two
    /// stinger tracks (14.4 s and 7.8 s) around a six-second gap.
    /// </summary>
    private const string UatPlaylist = "PL_CS_WAR_M15_01";

    /// <summary>A play list whose second track the install does not ship.</summary>
    private const string SkipPlaylist = "PL_MED_POP_EARLY";

    /// <summary>The play list whose only track resolves to nothing.</summary>
    private const string NullPlaylist = "PL_NULL";

    /// <summary>The shipped counts, as measured by tools/audio_music.py.</summary>
    private const int ShippedPlaylists = 90;
    private const int ShippedItems = 478;
    private const int ShippedTracks = 223;
    private const int ShippedResolved = 220;
    private const int ShippedSilences = 165;
    private const int ShippedEnds = 90;
    private const int ShippedVolumeRows = 54;
    private const int ShippedUnmatchedVolumes = 4;

    /// <summary>How far a measured item duration may sit from its own seconds.</summary>
    private const float WallClockTolerance = 0.4f;

    private const int Mp3FrameSamples = 1152;

    /// <summary>
    /// The decoder's excess over a file's own bitstream: a flat
    /// <see cref="Mp3PrimingFrames"/>-frame delay plus a per-frame fraction
    /// (<see cref="Mp3PrimingFraction"/>), the shape measured across the music
    /// (see <see cref="CheckPrimingSample"/>). Both live here rather than at the
    /// call sites so every length check bounds the same thing.
    /// </summary>
    private const double Mp3PrimingFrames = 3.0;
    private const double Mp3PrimingFraction = 0.008;

    /// <summary>How many distinct music files the priming sample decodes.</summary>
    private const int HeaderSampleCount = 8;

    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;

    public static void Main()
    {
      // The same report-path contract the other harnesses use, so one run
      // script can drive them all.
      string log = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(log))
        log = Path.Combine(Path.GetTempPath(), "sh2_music_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MMusicRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MMusicRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      Line($"game root    : {GameRoot}");
      Line($"audio device : rate={AudioSettings.outputSampleRate} "
           + $"speakerMode={AudioSettings.speakerMode} "
           + $"driverCaps={AudioSettings.driverCapabilities}");

      S2MMusicCatalogue catalogue = S2MMusicCatalogue.Load(GameRoot, out string reason);
      Check(catalogue != null, $"the shipped playlist sheet and volume list load ({reason})");
      if (catalogue == null) return;

      Line($"sheet        : {catalogue.PlaylistCount} play list(s), {catalogue.ItemCount} item(s), "
           + $"{catalogue.TrackCount} track(s), {catalogue.SilenceCount} *silence() gap(s), "
           + $"{catalogue.EndCount} *end()");
      Line($"             : {catalogue.ResolvedCount} track(s) resolve, "
           + $"{catalogue.MissingCount} do not; volume list {catalogue.VolumeRowCount} row(s)");
      Check(catalogue.PlaylistCount == ShippedPlaylists
            && catalogue.ItemCount == ShippedItems
            && catalogue.TrackCount == ShippedTracks
            && catalogue.SilenceCount == ShippedSilences
            && catalogue.EndCount == ShippedEnds,
        $"the runtime reads the shipped counts ({ShippedPlaylists} play list(s), "
        + $"{ShippedItems} item(s), {ShippedTracks} track(s), {ShippedSilences} gap(s), "
        + $"{ShippedEnds} *end())");
      Check(catalogue.ResolvedCount == ShippedResolved
            && catalogue.MissingCount == ShippedTracks - ShippedResolved,
        $"and resolves the shipped number of tracks ({catalogue.ResolvedCount}/{ShippedTracks})");
      Check(catalogue.VolumeRowCount == ShippedVolumeRows
            && catalogue.UnmatchedVolumeNames.Count == ShippedUnmatchedVolumes,
        $"and reads the volume list's {ShippedVolumeRows} rows, "
        + $"{ShippedUnmatchedVolumes} of them named by no track "
        + $"({string.Join(", ", catalogue.UnmatchedVolumeNames)})");
      Check(catalogue.ParseWarnings.Count == 0,
        $"and the sheet parses with no warnings ({catalogue.ParseWarnings.Count})");
      foreach (string warning in catalogue.ParseWarnings) Line($"  warning    : {warning}");

      CrossCheckManifest(catalogue);
      CheckClips(catalogue);
      CheckPrimingSample(catalogue);
      PlayThroughRealTime(catalogue);
      CheckSkipAndNull(catalogue);
      CheckGains(catalogue);
    }

    /// <summary>
    /// The runtime reader against the python tool's manifest, item by item:
    /// two independent readers of one sheet that disagree would be worse
    /// evidence than either one alone.
    /// </summary>
    private static void CrossCheckManifest(S2MMusicCatalogue catalogue)
    {
      if (!File.Exists(Manifest))
      {
        Check(false, $"the manifest to cross-check exists ({Manifest})");
        return;
      }

      // The manifest carries no quoted cells and no cell holds a comma (the
      // file names are the sheet's, and the sheet's cells are comma-free), so
      // a plain split is exact here.
      var rows = new List<string[]>();
      foreach (string line in File.ReadAllLines(Manifest))
      {
        if (line.Length == 0 || line.StartsWith("playlist,")) continue;
        string[] cells = line.Split(',');
        if (cells.Length >= 22) rows.Add(cells);
      }

      int mismatches = 0;
      string firstMismatch = null;
      var manifestPlaylists = new Dictionary<string, int>(StringComparer.Ordinal);
      var manifestRules = new Dictionary<string, int>(StringComparer.Ordinal);
      foreach (string[] cells in rows)
      {
        string id = cells[0];
        if (!manifestPlaylists.ContainsKey(id))
        {
          manifestPlaylists[id] = 0;
          foreach (string column in new[] { "n_exact", "n_case", "n_ext", "n_pad",
            "n_sep", "n_spell" })
          {
            manifestRules.TryGetValue(column, out int seen);
            manifestRules[column] = seen
              + int.Parse(cells[Array.IndexOf(ManifestColumns, column)],
                CultureInfo.InvariantCulture);
          }
        }
        manifestPlaylists[id]++;

        string kind = cells[2];
        int index = int.Parse(cells[1], CultureInfo.InvariantCulture);
        if (!catalogue.TryGetPlaylist(id, out S2MMusicCatalogue.Playlist playlist, out _)
          || index >= playlist.Items.Count)
        {
          mismatches++;
          firstMismatch ??= $"{id}[{index}] is not in the runtime reader";
          continue;
        }
        S2MMusicCatalogue.Item item = playlist.Items[index];
        string path = cells[4];
        int seconds = cells[6].Length == 0 ? 0 : int.Parse(cells[6], CultureInfo.InvariantCulture);
        int volume = cells[7].Length == 0 ? 0 : int.Parse(cells[7], CultureInfo.InvariantCulture);
        bool agrees = kind == item.Kind
          && cells[3].Replace("\\", "/").TrimStart('/')
             == item.File.Replace("\\", "/").TrimStart('/')
          && path == (item.Path ?? string.Empty)
          && (kind != "silence" || seconds == (int)item.Seconds)
          && (kind != "track" || volume == item.Volume);
        if (agrees) continue;
        mismatches++;
        firstMismatch ??= $"{id}[{index}] manifest {kind}/'{cells[3]}'/'{path}'/{seconds}s/"
                          + $"{volume} vs runtime {item.Kind}/'{item.File}'/"
                          + $"'{item.Path}'/{item.Seconds:F0}s/{item.Volume}";
      }

      Line($"manifest     : {Manifest}");
      Line($"             : {rows.Count} item row(s), {manifestPlaylists.Count} play list(s)");
      Check(rows.Count == catalogue.ItemCount,
        $"both readers see the same {rows.Count} item(s) (runtime: {catalogue.ItemCount})");
      Check(manifestPlaylists.Count == catalogue.PlaylistCount,
        $"and the same {manifestPlaylists.Count} play list(s) "
        + $"(runtime: {catalogue.PlaylistCount})");
      Check(mismatches == 0,
        $"and agree on every item: kind, file, resolved path, silence seconds and "
        + $"volume ({mismatches} mismatch(es)"
        + (firstMismatch == null ? "" : $", first: {firstMismatch}") + ")");

      // The rule census, from the manifest's own summary columns: the shipped
      // resolution story, counted the same way in both readers.
      string rules = string.Join(", ", manifestRules.Select(pair => $"{pair.Key[2..]} {pair.Value}"));
      Line($"rules        : {rules}");
      int resolvedByRules = manifestRules.Values.Sum();
      Check(resolvedByRules == catalogue.ResolvedCount,
        $"the manifest's rule counts sum to the resolved tracks "
        + $"({resolvedByRules} vs {catalogue.ResolvedCount})");

      int silentPlaylists = 0;
      foreach (S2MMusicCatalogue.Playlist playlist in catalogue.Playlists)
        if (playlist.ResolvedCount == 0) silentPlaylists++;
      string silentIds = string.Join(", ", catalogue.Playlists
        .Where(p => p.ResolvedCount == 0).Select(p => p.Id));
      Line($"             : {silentPlaylists} play list(s) with no resolvable track ({silentIds})");
    }

    private static readonly string[] ManifestColumns =
    {
      "playlist", "index", "kind", "file", "resolved_path", "rule", "seconds",
      "volume", "volume_name", "n_items", "n_tracks", "n_resolved", "n_missing",
      "n_exact", "n_case", "n_ext", "n_pad", "n_sep", "n_spell", "n_silence",
      "n_case_raw", "missing_list",
    };

    /// <summary>
    /// The UAT play list's two tracks, against their own MPEG frames: channels,
    /// sample rate and sample count. The length check carries the same
    /// three-frame tolerance the audio UAT measured for Unity's decoder (its
    /// own priming/padding adds two frames to the bitstream's count).
    /// </summary>
    private static void CheckClips(S2MMusicCatalogue catalogue)
    {
      Line("--- clips ---");
      catalogue.TryGetPlaylist(UatPlaylist, out S2MMusicCatalogue.Playlist playlist, out _);
      foreach (S2MMusicCatalogue.Item item in playlist.Items.Where(entry => entry.IsTrack))
      {
        bool loaded = catalogue.TryGetClip(item, out AudioClip clip, out string reason);
        Check(loaded, $"'{item.File}' decodes a clip ({reason})");
        if (!loaded) continue;
        string path = Path.Combine(GameRoot, "fx/music", item.Path);
        S2MAudioFileHeader.Mp3 header = S2MAudioFileHeader.ReadMp3(path, out string why);
        Check(header != null, $"  its file header parses ({why})");
        if (header == null) continue;
        Line($"item {item.File}");
        Line($"  file       : fx/music/{item.Path}");
        Line($"  clip       : '{clip.name}' {clip.length:F4} s, {clip.channels} ch, "
             + $"{clip.frequency} Hz, {clip.samples} samples");
        Line($"  header     : {header.FileBytes} bytes, {header.BitrateKbps} kbps, "
             + $"{header.SampleRate} Hz, {header.Channels} ch, {header.Frames} frames "
             + $"= {header.Samples} samples (header length {header.Length:F4} s)");
        Check(clip.frequency == header.SampleRate,
          $"  sample rate agrees ({clip.frequency} vs {header.SampleRate})");
        Check(clip.channels == header.Channels,
          $"  channels agree ({clip.channels} vs {header.Channels})");
        long excess = clip.samples - header.Samples;
        long frames = header.Samples / Mp3FrameSamples;
        double bound = Mp3PrimingFrames + Mp3PrimingFraction * frames;
        Check(clip.frequency == header.SampleRate && clip.channels == header.Channels
              && excess >= 0 && excess / (double)Mp3FrameSamples <= bound,
          $"  the sample count agrees (excess {excess / (double)Mp3FrameSamples:F1} "
          + $"frame(s) of the {bound:F1} the decoder is allowed, "
          + $"{clip.samples} vs {header.Samples})");
        Check(Math.Abs(clip.length - header.Length) <= WallClockTolerance,
          $"  and so does the clip's length ({clip.length:F4} s vs the header's "
          + $"{header.Length:F4} s)");
      }
    }

    /// <summary>
    /// The decoder's priming, measured rather than assumed: a sample spanning
    /// the SHORTEST and the LONGEST track the sheet names, each decoded and
    /// compared with its own MPEG frames.
    ///
    /// The excess samples are not a constant - measured on this install, Unity's
    /// decoder reports about 2 frames more than the bitstream plus another 0.47 %
    /// of the frame count (9 files from 7.8 s to 217 s fit it to within a frame;
    /// the audio UAT's 0.9 s speech file fits it too, at +2 frames). The header
    /// walk is the side that agrees with the files: its length matches the byte
    /// count at the declared 192 kbps CBR to 5 ms, and ffprobe agrees with it.
    /// So the checks bound the excess by that model, with room to spare, rather
    /// than by one frame count that would only fit one file length.
    /// </summary>
    private static void CheckPrimingSample(S2MMusicCatalogue catalogue)
    {
      Line("--- the decoder's priming, sampled shortest to longest ---");
      var items = catalogue.Playlists
        .SelectMany(playlist => playlist.Items)
        .Where(item => item.IsTrack && item.Resolved)
        .GroupBy(item => item.Path, StringComparer.Ordinal)
        .Select(group => group.First())
        .ToList();
      // Read every header first (bytes only, no decode) so the sample can span
      // the length range instead of whatever order the sheet happens to be in.
      var measured = new List<(S2MMusicCatalogue.Item Item, S2MAudioFileHeader.Mp3 Header)>();
      foreach (S2MMusicCatalogue.Item item in items)
      {
        S2MAudioFileHeader.Mp3 header = S2MAudioFileHeader.ReadMp3(
          Path.Combine(GameRoot, "fx/music", item.Path), out _);
        if (header != null) measured.Add((item, header));
      }
      measured.Sort((left, right) => left.Header.Length.CompareTo(right.Header.Length));
      Line($"files        : {measured.Count} distinct resolved track(s) the sheet names, "
           + $"headers read without decoding; the sample takes "
           + $"{HeaderSampleCount} spanning the range");
      if (measured.Count == 0)
      {
        Check(false, "the music files have readable MPEG headers");
        return;
      }
      Line($"range        : shortest {measured[0].Item.Path} "
           + $"{measured[0].Header.Length:F3} s, longest "
           + $"{measured[^1].Item.Path} {measured[^1].Header.Length:F3} s");

      var sample = new List<(S2MMusicCatalogue.Item Item, S2MAudioFileHeader.Mp3 Header)>();
      int step = Math.Max(1, measured.Count / HeaderSampleCount);
      for (int at = 0; at < measured.Count && sample.Count < HeaderSampleCount; at += step)
        sample.Add(measured[at]);
      if (!sample.Contains(measured[^1])) sample[^1] = measured[^1];

      int worstFrames = 0, shortestFrames = int.MaxValue, negative = 0;
      double worstRatio = 0;
      foreach ((S2MMusicCatalogue.Item item, S2MAudioFileHeader.Mp3 header) in sample)
      {
        if (!catalogue.TryGetClip(item, out AudioClip clip, out string why))
        {
          Check(false, $"'{item.Path}' decodes ({why})");
          continue;
        }
        long frames = header.Samples / Mp3FrameSamples;
        long excess = clip.samples - header.Samples;
        double bound = Mp3PrimingFrames + Mp3PrimingFraction * frames;
        bool agrees = clip.frequency == header.SampleRate && clip.channels == header.Channels
                      && excess >= 0 && excess / (double)Mp3FrameSamples <= bound;
        Check(agrees, $"  {item.Path,-30} {clip.frequency} Hz {clip.channels} ch, "
                      + $"{header.Length:F3} s of {frames} frame(s) -> clip "
                      + $"{clip.length:F3} s, excess "
                      + $"{excess / (double)Mp3FrameSamples:F1} frame(s) of the "
                      + $"{bound:F1} allowed");
        if (excess < 0) negative++;
        worstFrames = Math.Max(worstFrames, (int)(excess / Mp3FrameSamples));
        shortestFrames = Math.Min(shortestFrames, (int)(excess / Mp3FrameSamples));
        worstRatio = Math.Max(worstRatio, clip.length / header.Length);
      }
      Line($"priming      : the decoder reports {shortestFrames} to {worstFrames} MPEG "
           + $"frame(s) more than the bitstream across the sample (worst length ratio "
           + $"{worstRatio:F4}); the checks allow {Mp3PrimingFrames} frame(s) + "
           + $"{Mp3PrimingFraction * 100:F1} % of the frames");
      Check(negative == 0,
        $"the decoder never reports fewer samples than the bitstream ({negative} file(s) did)");
    }

    /// <summary>
    /// The play list on the wall clock: track, gap, track, gap, end - with each
    /// transition timed in real seconds, and the source asked which track it is
    /// on while it runs. The sentinel gap is then spent by feeding the clock,
    /// which is what a 10,000 s directive cannot be tested with otherwise.
    /// </summary>
    private static void PlayThroughRealTime(S2MMusicCatalogue catalogue)
    {
      Line($"--- {UatPlaylist} on the wall clock ---");
      var host = new GameObject("S2M Music Player");
      // A listener, like the game's scene always has: a listener-less batchmode
      // scene would make any non-2D source inaudible and could hide a real bug.
      host.AddComponent<AudioListener>();
      var player = host.AddComponent<S2MMusicPlayer>();
      // No player loop in -executeMethod: the harness IS the clock. Play mode
      // feeds the same Tick from Update.
      player.TickInUpdate = false;
      player.UseCatalogue(catalogue);

      bool started = player.Play(UatPlaylist, out string reason);
      Check(started, $"'{UatPlaylist}' starts ({reason})");
      if (!started) { UnityEngine.Object.DestroyImmediate(host); return; }
      S2MMusicCatalogue.Playlist playlist = player.Current;
      Line($"play list    : {playlist.Id}, {playlist.Items.Count} item(s), "
           + $"{playlist.TrackCount} track(s) ({playlist.ResolvedCount} resolve), "
           + $"{playlist.SilenceCount} gap(s) totalling {playlist.SilenceSeconds:F0} s");
      for (int index = 0; index < playlist.Items.Count; index++)
      {
        S2MMusicCatalogue.Item item = playlist.Items[index];
        Line($"  item {index}     : {item.Kind,-8} '{item.File}'"
             + (item.IsTrack
               ? $" -> {item.Path ?? "(unresolved)"}"
                 + $" [{item.Rule}] volume {item.Volume}"
                 + (item.VolumeName == null ? "" : $" from '{item.VolumeName}'")
               : "")
             + (item.Kind == "silence" ? $" = {item.Seconds:F0} s" : ""));
      }

      // Expected: item 0 is a track, 1 a gap, 2 a track, 3 a gap, 4 the end.
      var kinds = playlist.Items.Select(item => item.Kind).ToArray();
      Check(kinds.SequenceEqual(new[] { "track", "silence", "track", "silence", "end" }),
        $"the play list is track -> gap -> track -> gap -> end ({string.Join(", ", kinds)})");
      Check(playlist.Items[1].Seconds == 6f && playlist.Items[3].Seconds == 10000f,
        $"the gaps are the sheet's own (item 1 = {playlist.Items[1].Seconds:F0} s, "
        + $"item 3 = {playlist.Items[3].Seconds:F0} s)");
      Check(playlist.Items[0].Volume == 85 && playlist.Items[2].Volume == 100,
        $"and the volume list's own values (item 0 = {playlist.Items[0].Volume}, "
        + $"item 2 = {playlist.Items[2].Volume})");

      var transitions = new List<(int Index, double At)>();
      var seenClips = new List<string>();
      var seenGains = new List<float>();
      int lastIndex = player.Index;
      // The first track's length is the header-checked clip length, so the
      // expected wall-clock duration of each item is the game's own number.
      float firstLength = player.CurrentClip != null ? player.CurrentClip.length : 0f;
      Line($"clock        : item 0 clip length {firstLength:F4} s - the run waits it out");
      var watch = Stopwatch.StartNew();
      double last = 0;
      // A budget generous enough for the two tracks and the gap with room for
      // a slow decode, and short enough that a stuck run fails the check. The
      // walk stops once the sentinel gap is reached - waiting out 10,000 s of
      // silence is not a test, so the sentinel is fed below instead.
      while (!player.IsFinished && player.Index < 3
             && watch.Elapsed.TotalSeconds < firstLength + 7.0 + 30.0)
      {
        Thread.Sleep(5);
        double now = watch.Elapsed.TotalSeconds;
        player.Tick((float)(now - last));
        last = now;
        if (player.Index == lastIndex) continue;
        lastIndex = player.Index;
        transitions.Add((player.Index, watch.Elapsed.TotalSeconds));
        seenClips.Add(player.Source.clip == null ? "(none)" : player.Source.clip.name);
        seenGains.Add(player.Source.volume);
        Line($"  t={watch.Elapsed.TotalSeconds,8:F3} s -> item {player.Index} "
             + $"{player.CurrentItem.Kind} '{player.CurrentItem.File}'"
             + (player.CurrentItem.IsTrack
               ? $", source '{seenClips[^1]}' gain {seenGains[^1]:F3}" : ""));
        // Which track it is on, asked while it is on it.
        if (player.CurrentItem.IsTrack)
          Check(player.Source.clip != null && player.Source.isPlaying
                && player.Source.clip.name == Path.GetFileName(player.CurrentItem.Path),
            $"  item {player.Index} is on the source and playing "
            + $"('{seenClips[^1]}')");
      }
      double elapsed = watch.Elapsed.TotalSeconds;
      watch.Stop();

      // The transitions are the evidence: each item lasted its own duration.
      Check(transitions.Count == 3,
        $"three transitions ran inside the budget ({transitions.Count})");
      if (transitions.Count == 3)
      {
        float secondLength = playlist.Items[2].Resolved
          ? LoadLength(catalogue, playlist.Items[2]) : 0f;
        double firstRun = transitions[0].At;
        double gapRun = transitions[1].At - transitions[0].At;
        double secondRun = transitions[2].At - transitions[1].At;
        Check(Math.Abs(firstRun - firstLength) <= WallClockTolerance,
          $"the first track ran its own length in real seconds "
          + $"({firstRun:F3} s vs {firstLength:F4} s)");
        Check(Math.Abs(gapRun - 6.0) <= WallClockTolerance,
          $"the *silence(06) gap ran six real seconds ({gapRun:F3} s)");
        Check(Math.Abs(secondRun - secondLength) <= WallClockTolerance,
          $"the second track ran its own length ({secondRun:F3} s vs {secondLength:F4} s)");
      }

      // The sentinel: 10,000 s of silence is spent by feeding the clock. What
      // is checked is that the machine reports the directive's own remainder and
      // then reaches *end() - not that anybody waited.
      Check(player.CurrentItem.Kind == "silence" && player.CurrentItem.Seconds == 10000f,
        $"the run stopped inside the *silence(10000) sentinel "
        + $"(item {player.Index}, {player.SilenceRemaining:F3} s left)");
      Check(!player.IsFinished, "and the play list is NOT finished while the sentinel runs");
      float before = player.SilenceRemaining;
      player.Tick(before - 0.5f);
      Check(player.CurrentItem != null && player.CurrentItem.Kind == "silence"
            && Math.Abs(player.SilenceRemaining - 0.5f) < 0.001f,
        $"half a second left of the sentinel is reported as such "
        + $"({player.SilenceRemaining:F3} s)");
      player.Tick(0.5f);
      Check(player.IsFinished, "and the sentinel's end reaches *end()");
      Check(player.LastReason != null && player.LastReason.Contains("*end()"),
        $"the finish names *end() ('{player.LastReason}')");
      Check(player.TracksPlayed == 2 && player.GapsPlayed == 2 && player.ItemsSkipped == 0,
        $"the counters agree: {player.TracksPlayed} track(s), {player.GapsPlayed} gap(s), "
        + $"{player.ItemsSkipped} skipped");
      Line($"totals       : {elapsed:F3} s of real time for the unwound part, "
           + $"{player.TotalElapsed:F3} s of play list time in total");

      // The audio thread runs even with no device: the DSP clock moves on while
      // a clip plays. (AudioSource.time does NOT: no player loop pumps it in
      // -executeMethod. Measured by the audio UAT, printed here, not asserted.)
      double dspBefore = AudioSettings.dspTime;
      player.Play(UatPlaylist, out _);
      Thread.Sleep(250);
      double dspAfter = AudioSettings.dspTime;
      Line($"dsp clock    : {dspBefore:F4} -> {dspAfter:F4} (+{dspAfter - dspBefore:F4} s "
           + $"while a track plays), source.time {player.Source.time:F4} (frozen: no player loop)");
      Check(dspAfter > dspBefore, "the audio clock runs on while the music plays");
      player.Stop("the run is done with it");
      UnityEngine.Object.DestroyImmediate(host);
    }

    /// <summary>The header length of a resolved track, in seconds.</summary>
    private static float LoadLength(S2MMusicCatalogue catalogue, S2MMusicCatalogue.Item item)
    {
      return catalogue.TryGetClip(item, out AudioClip clip, out _) && clip != null
        ? clip.length : 0f;
    }

    /// <summary>
    /// The refusals, each named: a play list id that is not in the sheet, the
    /// null play list (which ships a track the install does not have), and a
    /// miss in the middle of a real play list - which is SKIPPED while its
    /// neighbours still play.
    /// </summary>
    private static void CheckSkipAndNull(S2MMusicCatalogue catalogue)
    {
      Line("--- silences and refusals ---");
      var host = new GameObject("S2M Music Silences");
      var player = host.AddComponent<S2MMusicPlayer>();
      player.TickInUpdate = false;
      player.UseCatalogue(catalogue);

      bool known = player.Play("PL_NO_SUCH_LIST", out string why);
      Check(!known && player.Current == null,
        $"a play list id that does not exist plays nothing ('{why}')");
      Check(why != null && why.Contains("PL_NO_SUCH_LIST"), "and the refusal names the id");
      Check(player.Source.clip == null && !player.Source.isPlaying,
        "and nothing is on the source");

      bool nullStarted = player.Play(NullPlaylist, out string nullWhy);
      Check(nullStarted, $"'{NullPlaylist}' exists in the sheet and starts ({nullWhy})");
      Check(player.IsFinished && player.TracksPlayed == 0,
        $"but plays nothing ({player.TracksPlayed} track(s), finished {player.IsFinished})");
      Check(player.ItemsSkipped == 1 && player.SkippedFiles.Contains("null.wav"),
        $"and names the track it skipped ({string.Join(", ", player.SkippedFiles)})");
      Line($"null play list: '{player.LastReason}'");

      bool skipStarted = player.Play(SkipPlaylist, out string skipWhy);
      Check(skipStarted, $"'{SkipPlaylist}' starts ({skipWhy})");
      if (!skipStarted) { UnityEngine.Object.DestroyImmediate(host); return; }
      S2MMusicCatalogue.Playlist playlist = player.Current;
      // Feed the whole play list through in one step: it is minutes of music
      // and the point here is the SKIP, not the timing (which the wall-clock
      // section above already measured).
      player.Tick(100000f);
      Check(player.IsFinished, $"the whole play list runs to *end() in one fed step "
                               + $"('{player.LastReason}')");
      Check(player.ItemsSkipped == 1 && player.SkippedFiles.Contains("Chantloop1.wav"),
        $"the one track the install does not ship is skipped and named "
        + $"({string.Join(", ", player.SkippedFiles)})");
      Check(player.TracksPlayed == playlist.ResolvedCount && playlist.ResolvedCount > 0,
        $"and every other track played ({player.TracksPlayed} of "
        + $"{playlist.TrackCount}, {playlist.ResolvedCount} resolve)");
      string missingFiles = string.Join(", ", playlist.Items
        .Where(i => i.IsTrack && !i.Resolved).Select(i => i.File));
      Line($"skip case    : {playlist.Id} has {playlist.TrackCount} track(s), "
           + $"{playlist.MissingCount} missing ({missingFiles})");
      UnityEngine.Object.DestroyImmediate(host);
    }

    /// <summary>
    /// The volume list's own numbers on the source: 127 as full scale, and the
    /// two the UAT play list carries (85 and 100).
    /// </summary>
    private static void CheckGains(S2MMusicCatalogue catalogue)
    {
      Line("--- gains (the volume list's own scale) ---");
      Line($"gain         : 127 -> {S2MMusicCatalogue.Gain(127):F4}, "
           + $"115 -> {S2MMusicCatalogue.Gain(115):F4}, 100 -> {S2MMusicCatalogue.Gain(100):F4}, "
           + $"85 -> {S2MMusicCatalogue.Gain(85):F4}");
      Check(Math.Abs(S2MMusicCatalogue.Gain(127) - 1f) < 0.0001f,
        "a 127 row (the loudest two tracks) plays at full scale");
      Check(Math.Abs(S2MMusicCatalogue.Gain(100) - 100f / 127f) < 0.0001f,
        "a 100 row plays at 100/127 of full scale");
      Check(Math.Abs(S2MMusicCatalogue.Gain(100) - 1f) > 0.2f,
        "so the music list's 100 is NOT treated as the event sheets' full-scale 100");

      var host = new GameObject("S2M Music Gains");
      var player = host.AddComponent<S2MMusicPlayer>();
      player.TickInUpdate = false;
      player.UseCatalogue(catalogue);
      player.Play(UatPlaylist, out _);
      Check(Math.Abs(player.Source.volume - 85f / 127f) < 0.0001f,
        $"the first track plays at its own row's gain ({player.Source.volume:F4} = 85/127)");
      player.Tick(player.CurrentClip.length);
      Check(player.CurrentItem.Kind == "silence"
            && Math.Abs(player.Source.volume - 85f / 127f) < 0.0001f,
        "and a gap leaves the source's gain alone");
      player.Tick(6f);
      Check(player.CurrentItem.IsTrack
            && Math.Abs(player.Source.volume - 100f / 127f) < 0.0001f,
        $"the second track takes its own gain ({player.Source.volume:F4} = 100/127)");
      UnityEngine.Object.DestroyImmediate(host);
    }

    private static void Check(bool ok, string what)
    {
      if (!ok) failures++;
      Line($"{(ok ? "ok  " : "FAIL")} {what}");
    }

    private static void Line(string text) => Report.AppendLine(text);
  }
}
