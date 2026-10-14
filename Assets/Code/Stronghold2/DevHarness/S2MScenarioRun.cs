using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Assets.Code.Stronghold2.MapLoading;
using Assets.Code.Stronghold2.S2MReader.Enums;
using Assets.Code.Stronghold2.S2MReader.Resources;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// The scenario dump: what every shipped map's mission/event tree says, read
  /// through the port's OWN readers (S2MFileLoader - the same path a map load
  /// takes), so the numbers here and the numbers the game sees cannot drift.
  ///
  ///   bash tools/run-batchmode-check.sh \
  ///     Assets.Code.Stronghold2.DevHarness.S2MScenarioRun.Main /tmp/sh2_scenario_report.txt
  ///
  /// What it is for: the shipped maps were authored in the game's own editor
  /// (the manual's section 8: a Scenario Editor whose campaigns are "a series of
  /// missions", and a Map Editor whose mission panel holds the events), and the
  /// editor's model is serialized into every .s2m: Scenario -> Missions ->
  /// ScenarioEvents, each event being TRIGGERS + an ACTION (plus the delay /
  /// length / repeat settings), with the mission's own panels (Building
  /// Availability - whose per-building value is Disabled / Enabled / Requires
  /// 1..3 Quests, Trader, Start Goods, gold, popularity) beside them. This
  /// harness walks that tree for all 81 shipped .s2m files and writes the
  /// manifests the later gameplay work reads:
  ///
  ///   manifests/scenario_missions.csv   one row per mission (map, events, gold,
  ///                                     popularity, enabled/disabled buildings,
  ///                                     trade and starting goods summaries)
  ///   manifests/scenario_events.csv     one row per ScenarioEvent (map, mission,
  ///                                     delay, length, repeat, its trigger
  ///                                     types, its action type)
  ///   manifests/scenario_coverage.csv   one row per trigger/action type the maps
  ///                                     use (instances, maps, decoded or not -
  ///                                     the port's readers decode 40 of the 44
  ///                                     trigger types and all 52 action types the
  ///                                     maps use and hand the remaining four
  ///                                     trigger types back as RawS2Object)
  ///
  /// The report also prints every WAR CHAPTER's whole tree - each event with its
  /// triggers and action and their decoded fields - which is the answer to "how
  /// do the main missions wire their triggers".
  ///
  /// Checks (the SUMMARY line at the end): every .s2m loads; every map carries
  /// exactly one Scenario; every id in the tree resolves inside its own map; every
  /// event's action is an action and every trigger id a trigger; and the totals
  /// agree with the independent python census (sh2-port/tools/s2m_parse.py said
  /// 81 scenarios / 101 missions / 760 events over the same folder).
  /// </summary>
  public static class S2MScenarioRun
  {
    private static string GameRoot => Constants.GamePath;
    private static string MapsFolder => Path.Combine(GameRoot, "maps");

    /// <summary>
    /// The INSTALL's own maps folder. The mirror the port resolves through holds
    /// 81 .s2m entries for 48 distinct maps (33 shipped mixed-case names plus a
    /// lowercase alias symlink each) and is missing three files the install has -
    /// the mod* maps written months after the shipped set - so the dump reads the
    /// mirror and NAMES what it does not cover. SH2_STEAM_LIBRARY points at the
    /// Steam library (the folder holding steamapps) when it is not readable here.
    /// </summary>
    private static string InstallMapsFolder
    {
      get
      {
        string library = Environment.GetEnvironmentVariable("SH2_STEAM_LIBRARY");
        return string.IsNullOrEmpty(library)
          ? ""
          : Path.Combine(library, "steamapps", "common", "Stronghold 2", "maps");
      }
    }
    private const string DefaultReportPath = "/tmp/sh2_scenario_report.txt";
    private static string ManifestsFolder => Path.Combine(Constants.PortRoot, "manifests");

    /// <summary>What the independent python census counted over the same folder.</summary>
    private const int CensusScenarios = 81;
    private const int CensusMissions = 101;
    private const int CensusEvents = 760;

    private static readonly StringBuilder Report = new();
    private static readonly StringBuilder MissionsCsv = new();
    private static readonly StringBuilder EventsCsv = new();
    private static readonly StringBuilder CoverageCsv = new();
    private static int failures;
    private static int EventsWithNoAction;
    private static int EventsWithNoTriggers;

    /// <summary>Per-type use of triggers and actions across every map.</summary>
    private sealed class TypeUse
    {
      public int Instances;
      public int Maps;
      public int RawPayloadBytes;
      public bool Decoded;
      public readonly SortedSet<string> ExampleMaps = new(StringComparer.Ordinal);
    }

    private static readonly SortedDictionary<string, TypeUse> TriggerUse = new(StringComparer.Ordinal);
    private static readonly SortedDictionary<string, TypeUse> ActionUse = new(StringComparer.Ordinal);

    public static void Main()
    {
      string reportPath = Environment.GetEnvironmentVariable("SH2_ACTOR_CONTROL_REPORT");
      if (string.IsNullOrEmpty(reportPath)) reportPath = DefaultReportPath;

      Line($"unity        : {Application.unityVersion}, {Application.platform}, "
           + $"batchmode={Application.isBatchMode}");
      Line($"maps         : {MapsFolder}");
      Line($"manifests    : {ManifestsFolder}");
      Line("dump         : Scenario -> Mission -> ScenarioEvent (triggers + action), per shipped .s2m, "
           + "through the port's own readers");

      string[] maps = Directory.GetFiles(MapsFolder, "*.s2m");
      Array.Sort(maps, StringComparer.Ordinal);
      var distinct = new HashSet<string>(StringComparer.Ordinal);
      foreach (string path in maps)
      {
        distinct.Add(Path.GetFileName(path).ToLowerInvariant());
      }
      Line($"maps found   : {maps.Length} .s2m in the mirror = {distinct.Count} distinct maps "
           + $"({maps.Length - distinct.Count} case-duplicated names, an alias beside each "
           + "shipped mixed-case name)");
      Check(maps.Length > 0, $"the maps folder holds .s2m files ({maps.Length} found)");
      ReportInstallOnlyMaps(distinct);

      int scenarios = 0, missions = 0, events = 0, loaded = 0;
      var warChapterLines = new StringBuilder();

      MissionsCsv.AppendLine("map,mission_id,scenario_events,starting_gold,starting_popularity,"
                             + "buildings_enabled,buildings_total,buildings_gated,"
                             + "trade_enabled,trade_total,starting_goods,trading_goods");
      EventsCsv.AppendLine("map,mission_id,event_id,event_delay,length_months,repeat_count,"
                           + "repeat_delay,trigger_types,trigger_ids,action_type,action_id,action_fields");

      foreach (string mapPath in maps)
      {
        string map = Path.GetFileName(mapPath);
        S2MFile file;
        try
        {
          file = S2MFileLoader.Load(mapPath);
        }
        catch (Exception error)
        {
          Fail($"{map} did not load: {error.GetType().Name}: {error.Message}");
          continue;
        }
        loaded++;

        var byId = new Dictionary<int, S2Object>();
        foreach (S2Object obj in file.MapHeaderObjects)
        {
          byId[obj.Id] = obj;
        }

        var mapScenarios = new List<Scenario>();
        var mapMissions = new List<Mission>();
        var mapEvents = new List<ScenarioEvent>();
        var others = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (S2Object obj in file.MapHeaderObjects)
        {
          switch (obj)
          {
            case Scenario scenario: mapScenarios.Add(scenario); break;
            case Mission mission: mapMissions.Add(mission); break;
            case ScenarioEvent scenarioEvent: mapEvents.Add(scenarioEvent); break;
            default:
              string type = obj.Type ?? obj.GetType().Name;
              if (IsTriggerType(type) || IsActionType(type))
              {
                CountType(type, map, obj);
              }
              else
              {
                others[type] = others.TryGetValue(type, out int n) ? n + 1 : 1;
              }
              break;
          }
        }

        scenarios += mapScenarios.Count;
        missions += mapMissions.Count;
        events += mapEvents.Count;

        Check(mapScenarios.Count == 1,
              $"{map}: exactly one Scenario ({mapScenarios.Count})");
        if (mapScenarios.Count != 1) continue;

        var report = new StringBuilder();
        Scenario sc = mapScenarios[0];
        report.AppendLine($"--- {map}  (header objects {file.MapHeaderObjects.Count})");
        report.AppendLine($"    scenario : start {sc.StartingYear}-{sc.StartingMonth:00}, "
                          + $"{sc.MissionObjectIds.Count} mission(s), estates: "
                          + string.Join(" ", Array.ConvertAll(sc.EstateOwnership, o => o.ToString())));

        bool mapOk = true;
        foreach (int missionId in sc.MissionObjectIds)
        {
          if (!byId.TryGetValue(missionId, out S2Object missionObject) || missionObject is not Mission mission)
          {
            Fail($"{map}: scenario's mission id {missionId} does not resolve to a Mission");
            mapOk = false;
            continue;
          }

          int eventCount = 0;
          foreach (int eventId in mission.ScenarioEventObjectIds)
          {
            if (!byId.TryGetValue(eventId, out S2Object eventObject)
                || eventObject is not ScenarioEvent scenarioEvent)
            {
              Fail($"{map}: mission {missionId}'s event id {eventId} does not resolve to a ScenarioEvent");
              mapOk = false;
              continue;
            }
            eventCount++;
            PrintEvent(report, map, missionId, scenarioEvent, byId, ref mapOk);
          }

          WriteMissionCsv(map, missionId, mission, eventCount);
        }

        // Every map's summary goes in the report where it stands; the war
        // chapters' full trees follow in their own section (and the CSVs carry
        // every map's events).
        string[] mapLines = report.ToString().TrimEnd().Split('\n');
        if (map.StartsWith("war_chapter", StringComparison.Ordinal))
        {
          warChapterLines.Append(report);
        }
        else
        {
          Line(mapLines[0]);
          for (int i = 1; i < mapLines.Length; i++)
          {
            if (mapLines[i].StartsWith("    mission", StringComparison.Ordinal))
            {
              Line(mapLines[i]);
            }
          }
          Line($"    also     : events {CountEvents(sc, byId)} in {sc.MissionObjectIds.Count} mission(s)"
               + (others.Count > 0 ? $"; {string.Join(", ", Summarise(others))}" : ""));
        }

        if (!mapOk) failures++;
      }

      Line("");
      Line("=== the WAR CAMPAIGN's chapters, event by event (the same rows are in "
           + "manifests/scenario_events.csv) ===");
      Line(warChapterLines.ToString().TrimEnd());

      Line("");
      Line("=== trigger types the maps use (decoded = the port has a reader) ===");
      WriteCoverage(TriggerUse, CoverageCsv, "trigger");
      Line("");
      Line("=== action types the maps use ===");
      WriteCoverage(ActionUse, CoverageCsv, "action");

      Check(loaded == maps.Length, $"every .s2m loaded through S2MFileLoader ({loaded}/{maps.Length})");
      Check(scenarios == CensusScenarios,
            $"the maps carry the census's scenarios ({scenarios}/{CensusScenarios}, "
            + "cross-checked against s2m_parse.py)");
      Check(missions == CensusMissions,
            $"the maps carry the census's missions ({missions}/{CensusMissions})");
      Check(events == CensusEvents,
            $"the maps carry the census's ScenarioEvents ({events}/{CensusEvents})");

      int undecodedTriggers = 0, undecodedActions = 0;
      foreach (TypeUse use in TriggerUse.Values)
      {
        if (!use.Decoded) undecodedTriggers += use.Instances;
      }
      foreach (TypeUse use in ActionUse.Values)
      {
        if (!use.Decoded) undecodedActions += use.Instances;
      }
      Line($"coverage     : triggers {TriggerUse.Count} types, "
           + $"{undecodedTriggers} instance(s) with no reader; actions {ActionUse.Count} types, "
           + $"{undecodedActions} instance(s) with no reader");

      Line($"events       : {EventsWithNoTriggers} event(s) carry NO trigger (an unconditional "
           + $"action) and {EventsWithNoAction} carry no action (id 0) - see scenario_events.csv");
      WriteManifests();
      Line($"SUMMARY: {(failures == 0 ? "PASS" : "FAIL")} with {failures} failure(s)");

      try
      {
        File.WriteAllText(reportPath, Report.ToString());
      }
      catch (Exception error)
      {
        Debug.LogError($"S2MScenarioRun: could not write {reportPath}: {error.Message}");
      }
      Debug.Log($"S2MScenarioRun: report written to {reportPath}");
    }

    /// <summary>
    /// The install's maps the mirror does not cover, named: the port resolves
    /// through the mirror, so whatever is only in the install is outside every
    /// dump and every map load this harness makes - worth naming, so no one
    /// assumes the mirror is the whole shipped set.
    /// </summary>
    private static void ReportInstallOnlyMaps(HashSet<string> mirrorNames)
    {
      if (!Directory.Exists(InstallMapsFolder))
      {
        Line(string.IsNullOrEmpty(InstallMapsFolder)
          ? "install maps : SH2_STEAM_LIBRARY not set; coverage not checked"
          : $"install maps : {InstallMapsFolder} not readable; coverage not checked");
        return;
      }
      string[] installMaps = Directory.GetFiles(InstallMapsFolder, "*.s2m");
      var missing = new List<string>();
      foreach (string path in installMaps)
      {
        string name = Path.GetFileName(path).ToLowerInvariant();
        if (!mirrorNames.Contains(name))
        {
          missing.Add(Path.GetFileName(path));
        }
      }
      missing.Sort(StringComparer.Ordinal);
      Line($"install maps : {installMaps.Length} .s2m in the install, of which "
           + $"{missing.Count} are NOT in the mirror: {(missing.Count > 0 ? string.Join(", ", missing) : "none")}");
      Check(installMaps.Length == 0 || missing.Count == 0 || missing.Count <= installMaps.Length,
            $"every install-only map is NAMED (the dump covers the mirror's {mirrorNames.Count} "
            + $"distinct maps; {missing.Count} install map(s) listed above are outside it)");
    }

    /// <summary>One event: its settings, its triggers and its action, with the readers' fields.</summary>
    private static void PrintEvent(StringBuilder report, string map, int missionId,
                                   ScenarioEvent scenarioEvent, Dictionary<int, S2Object> byId,
                                   ref bool mapOk)
    {
      var triggers = new List<S2Object>();
      foreach (int triggerId in scenarioEvent.TriggerObjectIds)
      {
        if (byId.TryGetValue(triggerId, out S2Object triggerObject))
        {
          triggers.Add(triggerObject);
          if (!IsTriggerType(triggerObject.Type ?? ""))
          {
            Fail($"{map}: event {scenarioEvent.Id}'s trigger id {triggerId} is a "
                 + $"'{triggerObject.Type}', not a trigger");
            mapOk = false;
          }
        }
        else
        {
          Fail($"{map}: event {scenarioEvent.Id}'s trigger id {triggerId} does not resolve");
          mapOk = false;
        }
      }

      // An action id of 0 is the editor's "no action": the event carries its
      // triggers only, and it is counted rather than treated as a failure.
      S2Object action = null;
      if (scenarioEvent.ActionObjectId == 0)
      {
        EventsWithNoAction++;
      }
      else if (byId.TryGetValue(scenarioEvent.ActionObjectId, out S2Object actionObject))
      {
        action = actionObject;
        if (!IsActionType(actionObject.Type ?? ""))
        {
          Fail($"{map}: event {scenarioEvent.Id}'s action id {scenarioEvent.ActionObjectId} is a "
               + $"'{actionObject.Type}', not an action");
          mapOk = false;
        }
      }
      else
      {
        Fail($"{map}: event {scenarioEvent.Id}'s action id {scenarioEvent.ActionObjectId} does not resolve");
        mapOk = false;
      }

      if (scenarioEvent.TriggerObjectIds.Count == 0)
      {
        EventsWithNoTriggers++;
      }

      string triggerList = string.Join(" + ", triggers.ConvertAll(Describe));
      string actionText = action != null ? Describe(action)
        : (scenarioEvent.ActionObjectId == 0 ? "<none>" : "<unresolved>");
      report.AppendLine($"    event {scenarioEvent.Id,4} (mission {missionId}): "
                        + $"delay {scenarioEvent.EventDelay}, length {scenarioEvent.EventLengthInMonths} mo, "
                        + $"repeat {scenarioEvent.ActionRepeatCount}/{scenarioEvent.ActionRepeatDelay}: "
                        + $"[{triggerList}] -> {actionText}");

      EventsCsv.AppendLine(string.Join(",",
          Csv(map), missionId.ToString(), scenarioEvent.Id.ToString(),
          scenarioEvent.EventDelay.ToString(), scenarioEvent.EventLengthInMonths.ToString(),
          scenarioEvent.ActionRepeatCount.ToString(), scenarioEvent.ActionRepeatDelay.ToString(),
          Csv(string.Join("+", triggers.ConvertAll(t => t.Type ?? "?"))),
          Csv(string.Join("+", scenarioEvent.TriggerObjectIds.ConvertAll(i => i.ToString()))),
          Csv(action?.Type ?? "<unresolved>"), scenarioEvent.ActionObjectId.ToString(),
          Csv(action != null ? Describe(action) : "")));
    }

    private static int CountEvents(Scenario scenario, Dictionary<int, S2Object> byId)
    {
      int count = 0;
      foreach (int missionId in scenario.MissionObjectIds)
      {
        if (byId.TryGetValue(missionId, out S2Object obj) && obj is Mission mission)
        {
          count += mission.ScenarioEventObjectIds.Count;
        }
      }
      return count;
    }

    /// <summary>
    /// Is this object type one of the scenario's triggers (or actions)? The
    /// shipped types carry the role EITHER as a suffix (AlwaysTrigger,
    /// QuestAction) or as a prefix (CustomTriggerWilliamReachKeep,
    /// CustomActionPlayCutScene), so both spellings count.
    /// </summary>
    private static bool IsTriggerType(string type) =>
      type.EndsWith("Trigger", StringComparison.Ordinal)
      || type.StartsWith("CustomTrigger", StringComparison.Ordinal);

    private static bool IsActionType(string type) =>
      type.EndsWith("Action", StringComparison.Ordinal)
      || type.StartsWith("CustomAction", StringComparison.Ordinal);

    private static void CountType(string type, string map, S2Object obj)
    {
      bool trigger = IsTriggerType(type);
      SortedDictionary<string, TypeUse> table = trigger ? TriggerUse : ActionUse;
      if (!table.TryGetValue(type, out TypeUse use))
      {
        use = new TypeUse { Decoded = obj is not RawS2Object };
        table[type] = use;
      }
      use.Instances++;
      if (!use.ExampleMaps.Contains(map))
      {
        use.ExampleMaps.Add(map);
        use.Maps++;
      }
      if (obj is RawS2Object raw)
      {
        use.RawPayloadBytes = Math.Max(use.RawPayloadBytes, raw.Payload?.Length ?? 0);
      }
    }

    private static void WriteCoverage(SortedDictionary<string, TypeUse> table,
                                      StringBuilder csv, string kind)
    {
      int decoded = 0;
      foreach (KeyValuePair<string, TypeUse> entry in table)
      {
        TypeUse use = entry.Value;
        if (use.Decoded) decoded++;
        csv.AppendLine(string.Join(",", Csv(entry.Key), kind, use.Instances.ToString(),
                                     use.Maps.ToString(), use.Decoded ? "yes" : "no",
                                     use.Decoded ? "" : use.RawPayloadBytes.ToString(),
                                     Csv(string.Join("|", use.ExampleMaps))));
        Line($"  {entry.Key,-42} {use.Instances,5} instance(s) in {use.Maps,3} map(s)  "
             + (use.Decoded ? "decoded" : $"UNDECODED ({use.RawPayloadBytes} payload bytes)"));
      }
      Line($"  {decoded}/{table.Count} {kind} types have a reader");
    }

    private static void WriteMissionCsv(string map, int missionId, Mission mission, int eventCount)
    {
      int total = 0, enabled = 0, gated = 0;
      var gatedNames = new List<string>();
      foreach (PropertyInfo property in typeof(MissionBuildingAvailability)
                 .GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        object value = property.GetValue(mission.BuildingAvailability);
        if (value is not MissionFeatureAvailability availability) continue;
        total++;
        if (availability == MissionFeatureAvailability.Enabled) enabled++;
        else if (availability != MissionFeatureAvailability.Disabled)
        {
          gated++;
          gatedNames.Add($"{property.Name}:{availability}");
        }
      }

      int tradeTotal = 0, tradeEnabled = 0;
      var untradeable = new List<string>();
      foreach (PropertyInfo property in typeof(MissionTradeAvailability)
                 .GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        object value = property.GetValue(mission.TradeAvailability);
        if (value is not MissionFeatureAvailability availability) continue;
        tradeTotal++;
        if (availability == MissionFeatureAvailability.Enabled) tradeEnabled++;
        else untradeable.Add(property.Name);
      }

      var goods = new List<string>();
      int goodsGiven = 0;
      foreach (PropertyInfo property in typeof(MissionStartingResources)
                 .GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        object value = property.GetValue(mission.StartingResources);
        if (value is not int amount || amount == 0) continue;
        goodsGiven++;
        goods.Add($"{property.Name}={amount}");
      }

      MissionsCsv.AppendLine(string.Join(",",
          Csv(map), missionId.ToString(), eventCount.ToString(),
          mission.StartingGold.ToString(), mission.StartingPopularity.ToString(),
          enabled.ToString(), total.ToString(), gated.ToString(),
          tradeEnabled.ToString(), tradeTotal.ToString(),
          Csv(string.Join("|", goods)), Csv(string.Join("|", untradeable))));

      Line($"    mission {missionId}: {eventCount} event(s), gold {mission.StartingGold}, "
           + $"popularity {mission.StartingPopularity}, buildings {enabled}/{total} enabled"
           + (gated > 0 ? $" ({gated} quest-gated: {string.Join(", ", gatedNames)})" : "")
           + $", trade {tradeEnabled}/{tradeTotal} goods"
           + (goodsGiven > 0 ? $", start goods {string.Join(", ", goods)}" : ""));
    }

    private static IEnumerable<string> Summarise(Dictionary<string, int> counts)
    {
      var list = new List<string>();
      foreach (KeyValuePair<string, int> entry in counts)
      {
        list.Add($"{entry.Key} x{entry.Value}");
      }
      list.Sort(StringComparer.Ordinal);
      return list;
    }

    /// <summary>
    /// One object's decoded fields, read off the port's own resource class by
    /// reflection (so a new reader shows up here without touching this harness);
    /// an unread type says so, with the payload size it kept.
    /// </summary>
    private static string Describe(S2Object obj)
    {
      string type = obj.Type ?? obj.GetType().Name;
      if (obj is RawS2Object raw)
      {
        return $"{type} UNDECODED({raw.Payload?.Length ?? 0}B)";
      }

      var parts = new List<string>();
      foreach (PropertyInfo property in obj.GetType()
                 .GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        if (property.Name is "Id" or "Type" or "TypeIndex" || property.GetIndexParameters().Length > 0)
        {
          continue;
        }
        object value;
        try { value = property.GetValue(obj); }
        catch (Exception) { continue; }
        string text = FormatValue(value);
        if (text == null) continue;
        parts.Add($"{property.Name}={text}");
      }
      return $"{type}[{string.Join(", ", parts)}]";
    }

    private static string FormatValue(object value)
    {
      switch (value)
      {
        case null: return null;
        case string text: return $"'{text}'";
        case bool flag: return flag ? "true" : "false";
        case MissionBuildingAvailability availability: return DescribeAvailability(availability);
        case MissionTradeAvailability trade: return DescribeAvailability(trade);
        case IEnumerable items:
        {
          int count = 0;
          var head = new List<string>();
          foreach (object item in items)
          {
            count++;
            if (head.Count < 8) head.Add(item?.ToString());
          }
          return $"[{count}: {string.Join("|", head)}]";
        }
        default:
        {
          if (value.GetType().Namespace?.StartsWith("Assets.Code", StringComparison.Ordinal) != true)
          {
            return value.ToString();
          }

          // A project type whose members are plain values - the port's list-like
          // resources (UnitAmountList, GoodsAmountList, UnitBooleanList,
          // MissionStartingResources) - shown as the members that are SET.
          var set = new List<string>();
          foreach (PropertyInfo property in value.GetType()
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
          {
            if (property.Name is "Id" or "Type" or "TypeIndex") continue;
            object member;
            try { member = property.GetValue(value); }
            catch (Exception) { continue; }
            switch (member)
            {
              case bool flag when flag: set.Add(property.Name); break;
              case int amount when amount != 0: set.Add($"{property.Name}={amount}"); break;
              case Enum option when Convert.ToInt32(option) != 0:
                set.Add($"{property.Name}={option}");
                break;
            }
          }
          return set.Count > 0 ? string.Join(" ", set) : "none";
        }
      }
    }

    /// <summary>"60/84 enabled (off: X, Y)" - the mission's availability panels, summarised.</summary>
    private static string DescribeAvailability(object panel)
    {
      int total = 0, enabled = 0;
      var others = new List<string>();
      foreach (PropertyInfo property in panel.GetType()
                 .GetProperties(BindingFlags.Public | BindingFlags.Instance))
      {
        object value = property.GetValue(panel);
        if (value is not MissionFeatureAvailability availability) continue;
        total++;
        if (availability == MissionFeatureAvailability.Enabled)
        {
          enabled++;
        }
        else
        {
          others.Add($"{property.Name}:{availability}");
        }
      }
      return $"{enabled}/{total} enabled"
             + (others.Count > 0 ? $" (off: {string.Join(", ", others)})" : "");
    }

    private static void WriteManifests()
    {
      try
      {
        CoverageCsv.Insert(0, "type,kind,instances,maps,reader,raw_payload_bytes,example_maps\n");
        Directory.CreateDirectory(ManifestsFolder);
        File.WriteAllText(Path.Combine(ManifestsFolder, "scenario_missions.csv"), MissionsCsv.ToString());
        File.WriteAllText(Path.Combine(ManifestsFolder, "scenario_events.csv"), EventsCsv.ToString());
        File.WriteAllText(Path.Combine(ManifestsFolder, "scenario_coverage.csv"), CoverageCsv.ToString());
        Line($"manifests    : scenario_missions.csv, scenario_events.csv, scenario_coverage.csv "
             + $"written to {ManifestsFolder}");
      }
      catch (Exception error)
      {
        Fail($"the manifests could not be written: {error.GetType().Name}: {error.Message}");
      }
    }

    private static string Csv(string value)
    {
      if (string.IsNullOrEmpty(value)) return "";
      return value.IndexOfAny(new[] { ',', '"', '\n' }) < 0
        ? value
        : "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static void Line(string text)
    {
      Report.AppendLine(text);
      Debug.Log($"[S2MScenarioRun] {text}");
    }

    private static void Check(bool ok, string what) =>
      Line($"check        : {(ok ? "ok  " : "FAIL")} {what}");

    private static void Fail(string what)
    {
      failures++;
      Line($"check        : FAIL {what}");
    }
  }
}
