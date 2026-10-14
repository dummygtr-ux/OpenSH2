using Assets.Code.Stronghold2.Audio;
using Assets.Code.Stronghold2.Buildings;
using Assets.Code.Stronghold2.ModelRendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// The audio UAT, headless: the shipped catalogues, the shipped files, and
  /// the unit voices firing off the commander's own click grammar.
  ///
  ///   Unity -batchmode -projectPath <proj>
  ///         -executeMethod Assets.Code.Stronghold2.DevHarness.S2MAudioRun.Main -quit
  ///
  /// NOTE the missing -nographics: the clicks this run drives are in SCREEN
  /// pixels, so the screen has to be a real one (under -nographics Screen is
  /// 1x1 and every projected rectangle is meaningless). The run prints the
  /// screen size and checks it. Nothing is rendered - no camera.Render(), no
  /// capture - so this is a compute-only harness that merely needs a display.
  ///
  /// There is NO SPEAKER in batchmode - a null audio driver - so nothing here
  /// asserts that anything was audible. What it asserts is everything up to
  /// the speaker:
  ///
  ///   the catalogue  both shipped sheets read and resolved by the runtime
  ///                  reader, cross-checked against the manifest the python
  ///                  tool wrote (same id count, same cell count, same paths
  ///                  for the ids this run uses);
  ///   the clips      one PCM wav, one IMA-ADPCM wav and one speech mp3 per id
  ///                  file, each clip's channels, sample rate and length
  ///                  compared against the FILE'S OWN HEADER - the decoder is
  ///                  not its own witness;
  ///   playback       an AudioSource on a placed actor reports playing while
  ///                  the DSP clock runs (the audio thread is alive even with
  ///                  no device). Note what is NOT assertable here: with no
  ///                  player loop in -executeMethod, AudioSource.time stays at
  ///                  0 because the main thread never updates it - measured,
  ///                  printed, and deliberately not turned into a check;
  ///   the voices     a click on a real Archer plays FXT_ARCHER_SELECT and an
  ///                  order plays FXT_ARCHER_MOVE, through HandleClick and
  ///                  OrderSelectionTo - the commander's own entry points;
  ///   the silences   an unknown id, a type the sheet gives no voice (Ballista
  ///                  on the real roster), an id with no variants at all
  ///                  (FXT_ASSASSIN_DARING), and a click that orders nothing
  ///                  each play NOTHING, and each says why in one line.
  /// </summary>
  public static class S2MAudioRun
  {
    public static string GameRoot => Constants.GamePath;

    private static string MapJson =>
      Path.Combine(Constants.PortRoot, "artifacts", "entities_war_chapter1.json");

    /// <summary>The manifest the python tool wrote - the cross-check's other side.</summary>
    private static string Manifest =>
      Path.Combine(Constants.PortRoot, "manifests", "audio_catalogue.csv");

    /// <summary>A PCM effect: 1 channel, 22050 Hz, 16-bit (the rarer shipped codec).</summary>
    private const string PcmSoundId = "FX_BLACKSMITH_BELLOW";

    /// <summary>An IMA-ADPCM effect the sheet marks Random=yes (7 variants).</summary>
    private const string AdpcmSoundId = "FX_COMBAT_STAFF";

    /// <summary>The Archer's two voices, and the ids this run expects to hear.</summary>
    private const string ArcherSelectId = "FXT_ARCHER_SELECT";
    private const string ArcherMoveId = "FXT_ARCHER_MOVE";

    /// <summary>An id the sheet lists with no variants at all.</summary>
    private const string EmptyId = "FXT_ASSASSIN_DARING";

    /// <summary>
    /// How far a decoded clip's sample count may sit from the file header's own
    /// count. PCM is exact; an ADPCM tail block and Unity's MP3 decoder (which
    /// reports two more frames of priming/padding than the bitstream holds -
    /// measured below) are not, so the tolerance is 0.5 % for wav and three
    /// MPEG frames for mp3.
    /// </summary>
    private const float WavFrameTolerance = 0.005f;
    private const int Mp3FrameTolerance = 3;
    private const int Mp3FrameSamples = 1152;

    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;

    public static void Main()
    {
      // The same report-path contract the other harnesses use, so one run
      // script can drive them all.
      string log = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(log))
        log = Path.Combine(Path.GetTempPath(), "sh2_audio_report.txt");
      try
      {
        Run();
      }
      catch (Exception exception)
      {
        failures++;
        Report.AppendLine($"FATAL {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}");
        Debug.LogError($"[S2MAudioRun] {exception}");
      }
      Report.AppendLine($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");
      File.WriteAllText(log, Report.ToString());
      Debug.Log("[S2MAudioRun]\n" + Report.ToString().TrimEnd());
    }

    private static void Run()
    {
      Line($"game root    : {GameRoot}");
      Line($"audio device : rate={AudioSettings.outputSampleRate} "
           + $"speakerMode={AudioSettings.speakerMode} "
           + $"driverCaps={AudioSettings.driverCapabilities}");

      S2MAudioCatalogue catalogue = S2MAudioCatalogue.Load(GameRoot, out string reason);
      Check(catalogue != null, $"the shipped catalogues load ({reason})");
      if (catalogue == null) return;

      Line($"catalogue    : {catalogue.SoundCount} sound + {catalogue.SpeechCount} speech "
           + $"row(s), {catalogue.IdCount} distinct id(s) "
           + $"(the speech sheet repeats two)");
      Line($"             : {catalogue.VariantCount} variant cell(s), "
           + $"{catalogue.ResolvedCount} resolved, "
           + $"{catalogue.VariantCount - catalogue.ResolvedCount} unresolved");
      CrossCheckManifest(catalogue);

      CheckClips(catalogue);

      // The actor path: the real roster of the real map, driven through the
      // commander's own click entry points (D005 - the same methods a player's
      // mouse reaches).
      var host = new GameObject("S2M Audio Commander");
      // The game's scene always has a listener; a listener-less batchmode scene
      // would make every 3D source inaudible and could hide a real bug.
      host.AddComponent<AudioListener>();
      var commander = host.AddComponent<S2MActorCommander>();
      GameObject entitiesRoot = S2MPlacementHarness.LoadEntities(MapJson, GameRoot);
      Check(entitiesRoot != null, $"the real map's actors loaded ({MapJson})");
      if (entitiesRoot == null) return;

      // A camera where a player's would be. The harness clicks in SCREEN
      // pixels, so the screen has to be real: this run must NOT pass
      // -nographics (see the class comment), and the size is printed as the
      // proof that the projected rectangles were measured on a real screen.
      var cameraHost = new GameObject("S2M Audio Camera");
      var camera = cameraHost.AddComponent<Camera>();
      camera.fieldOfView = 55f;
      camera.nearClipPlane = 0.3f;
      camera.farClipPlane = 2000f;
      camera.clearFlags = CameraClearFlags.SolidColor;
      Line($"screen       : {Screen.width}x{Screen.height}, camera "
           + $"{camera.pixelWidth}x{camera.pixelHeight}");
      Check(Screen.width > 1 && Screen.height > 1,
        "the screen the clicks are computed on is a real one (no -nographics)");

      commander.Initialize(entitiesRoot, GameRoot, camera);
      commander.EnableActorSpeech(catalogue);
      Line($"roster       : {commander.ActorCount} placed actor(s), "
           + $"{S2MActorSpeech.MappedTypeCount} type(s) with voices");

      CheckVoices(commander, catalogue, camera);
      CheckSilences(commander, catalogue);
      CheckFormats(catalogue);

      S2MPlacementHarness.DestroyAll();
    }

    /// <summary>
    /// The runtime reader against the python tool's manifest: the same cells
    /// counted the same way, and the same resolved paths for the ids this run
    /// plays. Two independent readers of one sheet that disagree would be
    /// worse evidence than either alone.
    /// </summary>
    private static void CrossCheckManifest(S2MAudioCatalogue catalogue)
    {
      if (!File.Exists(Manifest))
      {
        Check(false, $"the manifest to cross-check exists ({Manifest})");
        return;
      }

      int rows = 0, variants = 0, resolved = 0;
      var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (string line in File.ReadAllLines(Manifest))
      {
        if (line.Length == 0 || line.StartsWith("id,")) continue;
        // The sheets carry no quoted cells and no cell contains a comma, so a
        // plain split is exact here (checked: 1,161 rows, 0 commas inside a
        // cell).
        string[] cells = line.Split(',');
        if (cells.Length < 15) continue;
        rows++;
        variants += int.Parse(cells[6], CultureInfo.InvariantCulture);
        resolved += int.Parse(cells[7], CultureInfo.InvariantCulture);
        string first = cells[14].Split('|')[0];
        paths[cells[0].Trim()] = first.Length == 0 ? string.Empty
          : first.Split(new[] { " (" }, StringSplitOptions.None)[0];
      }

      Line($"manifest     : {Manifest}");
      Line($"             : {rows} row(s), {variants} cell(s), {resolved} resolved");
      Check(rows == catalogue.RowCount,
        $"both readers see the same {rows} sheet row(s) (runtime: {catalogue.RowCount})");
      Check(variants == catalogue.VariantCount && resolved == catalogue.ResolvedCount,
        $"and resolve the same cells ({variants}/{resolved} manifest vs "
        + $"{catalogue.VariantCount}/{catalogue.ResolvedCount} runtime)");

      foreach (string id in new[] { PcmSoundId, AdpcmSoundId, ArcherSelectId, ArcherMoveId })
      {
        bool known = catalogue.TryResolve(id, out S2MAudioCatalogue.Entry entry, out _);
        string runtimePath = known && entry.Paths.Count > 0 ? entry.Paths[0] : string.Empty;
        paths.TryGetValue(id, out string manifestPath);
        Check(known && runtimePath == manifestPath,
          $"'{id}' resolves to the same file in both readers "
          + $"('{runtimePath}' vs manifest '{manifestPath}')");
      }
    }

    /// <summary>
    /// The clips, against their own files: one PCM wav, one ADPCM wav and the
    /// speech mp3, each decoded through the catalogue and compared field by
    /// field with the header reader.
    /// </summary>
    private static void CheckClips(S2MAudioCatalogue catalogue)
    {
      Line("--- clips ---");
      foreach (string id in new[] { PcmSoundId, AdpcmSoundId, ArcherSelectId })
      {
        bool loaded = catalogue.TryGetClip(id, out AudioClip clip, out string reason);
        Check(loaded, $"'{id}' decodes a clip ({reason})");
        if (!loaded) continue;

        string path = Path.Combine(GameRoot, "fx", catalogue.LastResolvedPath);
        Line($"id {id}");
        Line($"  file       : {catalogue.LastResolvedPath}");
        Line($"  clip       : '{clip.name}' {clip.length:F4} s, {clip.channels} ch, "
             + $"{clip.frequency} Hz, {clip.samples} samples, state {clip.loadState}");
        Check(clip.length > 0.02f,
          $"  the clip has a real length ({clip.length:F4} s)");

        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
          S2MAudioFileHeader.Wav header = S2MAudioFileHeader.ReadWav(path, out string why);
          Check(header != null, $"  the file header parses ({why})");
          if (header == null) continue;
          Line($"  header     : {header.FileBytes} bytes, formatTag {header.FormatTag} "
               + $"({(header.IsAdpcm ? "IMA ADPCM" : "PCM")}), {header.Channels} ch, "
               + $"{header.SampleRate} Hz, {header.Frames} frames "
               + $"(header length {header.Length:F4} s)");
          Check(clip.channels == header.Channels,
            $"  channels agree with the header ({clip.channels} vs {header.Channels})");
          Check(clip.frequency == header.SampleRate,
            $"  sample rate agrees ({clip.frequency} vs {header.SampleRate})");
          float drift = Math.Abs(clip.samples - header.Frames) / (float)header.Frames;
          Check(drift <= WavFrameTolerance,
            $"  the sample count agrees within {WavFrameTolerance * 100f:F1} % "
            + $"({clip.samples} vs {header.Frames}, {drift * 100f:F3} %)");
        }
        else
        {
          S2MAudioFileHeader.Mp3 header = S2MAudioFileHeader.ReadMp3(path, out string why);
          Check(header != null, $"  the mp3 header parses ({why})");
          if (header == null) continue;
          Line($"  header     : {header.FileBytes} bytes, {header.BitrateKbps} kbps, "
               + $"{header.SampleRate} Hz, {header.Channels} ch, {header.Frames} frames "
               + $"= {header.Samples} samples (header length {header.Length:F4} s), "
               + $"ID3v2 {header.Id3v2Bytes} B, ID3v1 {header.HasId3v1}, "
               + $"{header.StrayBytes} stray byte(s)");
          Check(clip.frequency == header.SampleRate,
            $"  sample rate agrees ({clip.frequency} vs {header.SampleRate})");
          Check(clip.channels == header.Channels,
            $"  channels agree ({clip.channels} vs {header.Channels})");
          long delta = Math.Abs(clip.samples - header.Samples);
          Check(delta <= Mp3FrameTolerance * Mp3FrameSamples,
            $"  the sample count agrees within {Mp3FrameTolerance} MPEG frames "
            + $"({clip.samples} vs {header.Samples}, delta {delta} = "
            + $"{delta / (float)Mp3FrameSamples:F1} frame(s))");
          Line($"  note       : Unity's decoder reports {delta / (float)Mp3FrameSamples:F1} "
               + $"frame(s) more than the bitstream holds - its own priming/padding");
        }
      }
    }

    /// <summary>
    /// The voices on the real roster: select an Archer with a click, order it,
    /// and check which id fired - plus the AudioSource state behind it.
    /// </summary>
    private static void CheckVoices(S2MActorCommander commander, S2MAudioCatalogue catalogue,
      Camera camera)
    {
      Line("--- voices on the real roster ---");
      S2MActorCommander.ActorHandle archer = commander.Actors
        .FirstOrDefault(handle => handle.Type == "Archer");
      Check(archer != null, "an Archer stands on the roster");
      if (archer == null) return;

      Frame(camera, archer.Actor.transform.position);
      Vector2 onArcher = ScreenOf(camera, archer.Renderer.bounds.center);

      double dspBefore = AudioSettings.dspTime;
      commander.HandleClick(onArcher);
      Check(commander.SelectionCount == 1 && commander.selection[0].Actor == archer.Actor,
        $"clicking the Archer selects it ({commander.SelectionCount} selected)");
      Check(commander.SpeechPlayCount == 1 && commander.LastSpeechId == ArcherSelectId,
        $"and plays its select voice '{ArcherSelectId}' "
        + $"(played {commander.SpeechPlayCount}, last '{commander.LastSpeechId}')");

      var source = archer.Actor.GetComponent<AudioSource>();
      Check(source != null && source.clip != null && source.isPlaying,
        $"the voice is playing on the actor's own AudioSource "
        + $"(clip '{(source?.clip == null ? "(none)" : source.clip.name)}', "
        + $"isPlaying {source?.isPlaying})");
      if (source != null && source.clip != null)
      {
        Line($"source       : on {archer.Actor.name}, '{source.clip.name}' "
             + $"{source.clip.length:F4} s {source.clip.channels} ch "
             + $"{source.clip.frequency} Hz, volume {source.volume:F2}, "
             + $"spatialBlend {source.spatialBlend:F2}");
        // The sheet's own volume column reaches the source (100 -> 1.00).
        Check(catalogue.TryResolve(ArcherSelectId, out S2MAudioCatalogue.Entry entry, out _)
              && Mathf.Abs(source.volume - entry.Volume / 100f) < 0.001f,
          $"and carries the sheet's own volume ({source.volume:F2})");
      }

      // The audio thread runs even with no device: the DSP clock moves on
      // while the clip plays. (AudioSource.time does NOT: no player loop pumps
      // in -executeMethod. Printed, not asserted.)
      Thread.Sleep(250);
      double dspAfter = AudioSettings.dspTime;
      Line($"dsp clock    : {dspBefore:F4} -> {dspAfter:F4} "
           + $"(+{dspAfter - dspBefore:F4} s while playing), source.time "
           + $"{(source == null ? 0f : source.time):F4} (frozen: no player loop)");
      Check(dspAfter > dspBefore, "the audio clock runs on while the voice plays");

      // The order path: the same actor, the MOVE verb.
      Vector3 destination = archer.Actor.transform.position
        + new Vector3(6f, 0f, 6f);
      commander.OrderSelectionTo(destination);
      Check(archer.Driver.IsMoving,
        "the order starts the Archer walking (the driver's own state)");
      Check(commander.SpeechPlayCount == 2 && commander.LastSpeechId == ArcherMoveId,
        $"and plays its move voice '{ArcherMoveId}' "
        + $"(played {commander.SpeechPlayCount}, last '{commander.LastSpeechId}')");

      if (source != null && source.clip != null)
      {
        string expected = Path.GetFileName(catalogue.LastResolvedPath);
        Check(source.clip.name == expected,
          $"the move voice is the move clip ('{source.clip.name}' == '{expected}')");
        Check(catalogue.LastResolvedPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase),
          $"and it is the shipped speech format ({catalogue.LastResolvedPath})");
      }
    }

    /// <summary>
    /// What must stay silent, and why: an unknown id, a type with no voice, an
    /// id with no variants, and a click that orders nothing. Each is a NAMED
    /// refusal, not a fallback to some other sound.
    /// </summary>
    private static void CheckSilences(S2MActorCommander commander, S2MAudioCatalogue catalogue)
    {
      Line("--- silences ---");

      bool known = catalogue.TryGetClip("FXT_NO_SUCH_VOICE", out AudioClip clip, out string why);
      Check(!known && clip == null, $"an id that does not exist plays nothing ('{why}')");

      bool empty = catalogue.TryGetClip(EmptyId, out AudioClip none, out string emptyWhy);
      Check(!empty && none == null,
        $"'{EmptyId}' (listed with no variants) plays nothing ('{emptyWhy}')");

      // A roster type the sheet gives no voice: the click SELECTS it, and the
      // command path says why it stays silent instead of playing a neighbour's
      // voice. The candidate has to be one a click can actually land on, so the
      // most ISOLATED silent actor is tried first (a click goes to the nearest
      // actor whose projected rectangle contains the point, D001, so a
      // neighbour's rectangle can steal the point), from a camera looking
      // straight down at it - the same isolation trick S2MActorClickRun uses
      // for its stop subject.
      var silentCandidates = commander.Actors
        .Where(handle => S2MActorSpeech.SelectId(handle.Type) == null)
        .OrderByDescending(handle => NearestNeighbourGap(commander, handle))
        .ToList();
      Check(silentCandidates.Count > 0,
        $"the roster has {silentCandidates.Count} actor(s) whose type the sheet "
        + $"gives no voice (e.g. {silentCandidates.FirstOrDefault()?.Type})");

      S2MActorCommander.ActorHandle silent = null;
      int before = commander.SpeechPlayCount;
      foreach (S2MActorCommander.ActorHandle candidate in silentCandidates.Take(6))
      {
        var camera = commander.ViewCamera;
        camera.transform.position = candidate.Actor.transform.position + new Vector3(0f, 12f, -0.5f);
        camera.transform.LookAt(candidate.Actor.transform.position);
        before = commander.SpeechPlayCount;
        commander.HandleClick(ScreenOf(camera, candidate.Renderer.bounds.center));
        if (commander.SelectionCount == 1 && commander.selection[0] == candidate)
        {
          silent = candidate;
          break;
        }
      }
      Check(silent != null,
        $"a click selects one of them ({(silent == null ? "none landed" : silent.Actor.name)})");
      if (silent != null)
      {
        Line($"silent case  : {silent.Actor.name} (type '{silent.Type}'), "
             + $"nearest neighbour {NearestNeighbourGap(commander, silent):F2} m away");
        Check(commander.SpeechPlayCount == before,
          $"and plays nothing ({commander.SpeechPlayCount} vs {before}) - "
          + $"'{commander.LastSpeechReason}'");
        Check(!string.IsNullOrEmpty(commander.LastSpeechReason)
              && commander.LastSpeechReason.Contains(silent.Type),
          "the silence names the reason");
      }

      // The unspeakable ids the sheet lists with no roster type are recorded,
      // not silently dropped: each still resolves by id, it is simply never
      // reached by a click.
      foreach (string id in S2MActorSpeech.UnreachableIds)
      {
        bool resolvable = catalogue.TryResolve(id, out S2MAudioCatalogue.Entry entry, out _)
                          && entry.Paths.Count > 0;
        Check(resolvable, $"'{id}' has no roster type but still resolves "
                          + $"({S2MActorSpeech.UnreachableIds.Length} such ids)");
      }

      // A click that orders nothing: no selection, a click on empty sky.
      commander.selection.Clear();
      int quiet = commander.SpeechPlayCount;
      commander.HandleClick(new Vector2(4f, 4f));
      Check(commander.SelectionCount == 0,
        "a click on empty space with no selection selects nothing");
      Check(commander.SpeechPlayCount == quiet,
        $"and plays nothing ({commander.SpeechPlayCount} vs {quiet})");
    }

    /// <summary>
    /// The two shipped formats on the actor path: the PCM wav and the speech
    /// mp3, each played from a placed actor's own AudioSource.
    /// </summary>
    private static void CheckFormats(S2MAudioCatalogue catalogue)
    {
      Line("--- formats on the actor path ---");
      // Reached through the same catalogue the voices come from: the point of
      // this section is the FILE FORMATS, which the voice section cannot cover
      // (every actor voice is speech, i.e. mp3, and every effect is wav).
      var host = new GameObject("S2M Audio Formats");
      var source = host.AddComponent<AudioSource>();

      foreach ((string id, string format) in new[]
      {
        (PcmSoundId, "wav/PCM"),
        (AdpcmSoundId, "wav/ADPCM, Random=yes"),
        (ArcherSelectId, "mp3 speech"),
      })
      {
        bool loaded = catalogue.TryGetClip(id, out AudioClip clip, out string why);
        Check(loaded, $"{format}: '{id}' decodes ({why})");
        if (!loaded) continue;
        source.clip = clip;
        source.spatialBlend = 0f;
        source.Play();
        Check(source.isPlaying && source.clip == clip,
          $"  it plays on an AudioSource (isPlaying {source.isPlaying})");
        // The path is named, not the file's byte count: the port reads a
        // symlink mirror, where FileInfo.Length reports the length of the link
        // path (a 2.9 s PCM file measures "75 bytes"). The sizes the readers
        // measured are in the clips section above, where the bytes were read.
        Line($"  {format,-24} '{id}' -> {catalogue.LastResolvedPath} => "
             + $"'{clip.name}' {clip.length:F4} s, {clip.channels} ch, "
             + $"{clip.frequency} Hz, {clip.samples} samples");
      }

      // The Random flag: an id the sheet marks random must be able to answer
      // with different variants, and one it does not must always take its first.
      catalogue.TryResolve(AdpcmSoundId, out S2MAudioCatalogue.Entry random, out _);
      var seen = new HashSet<string>();
      for (int draw = 0; draw < 40; draw++) seen.Add(catalogue.NextVariant(random));
      Check(seen.Count > 1 && seen.Count <= random.Paths.Count,
        $"'{AdpcmSoundId}' (Random=yes) draws from its {random.Paths.Count} variants "
        + $"({seen.Count} distinct in 40 draws)");
      catalogue.TryResolve(PcmSoundId, out S2MAudioCatalogue.Entry fixedEntry, out _);
      var always = new HashSet<string>();
      for (int draw = 0; draw < 10; draw++) always.Add(catalogue.NextVariant(fixedEntry));
      Check(always.Count == 1, $"'{PcmSoundId}' (no Random flag) always takes "
                               + $"'{always.FirstOrDefault()}'");
      UnityEngine.Object.DestroyImmediate(host);
    }

    /// <summary>How far the nearest other actor stands from this one, metres.</summary>
    private static float NearestNeighbourGap(S2MActorCommander commander,
      S2MActorCommander.ActorHandle handle)
    {
      Vector3 self = handle.Actor.transform.position;
      float nearest = float.MaxValue;
      foreach (S2MActorCommander.ActorHandle other in commander.Actors)
      {
        if (other == handle) continue;
        float gap = Vector3.Distance(self, other.Actor.transform.position);
        if (gap < nearest) nearest = gap;
      }
      return nearest == float.MaxValue ? 0f : nearest;
    }

    /// <summary>
    /// Puts the camera where a player's would be, looking down at an actor from
    /// off and above - the same viewpoint the click run uses, so the actor's
    /// projected rectangle is on screen and the pick can find it.
    /// </summary>
    private static void Frame(Camera camera, Vector3 subject)
    {
      camera.transform.position = subject + new Vector3(-22f, 30f, -22f);
      camera.transform.LookAt(subject);
    }

    private static Vector2 ScreenOf(Camera camera, Vector3 world)
    {
      Vector3 point = camera.WorldToScreenPoint(world);
      return new Vector2(point.x, point.y);
    }

    private static void Check(bool ok, string what)
    {
      if (!ok) failures++;
      Line($"{(ok ? "ok  " : "FAIL")} {what}");
    }

    private static void Line(string text) => Report.AppendLine(text);
  }
}
