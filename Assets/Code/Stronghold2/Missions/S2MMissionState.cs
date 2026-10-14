using System;
using System.Collections.Generic;
using Assets.Code.Stronghold2.S2MReader.Resources;
using Assets.Code.Stronghold2.Text;

namespace Assets.Code.Stronghold2.Missions
{
  /// <summary>
  /// The loaded mission's own starting state, as the game's in-game MAIN SCREEN
  /// shows it (i18n/en-us.xml's MAINSCREENROLLOVERS079..084 name its six status
  /// fields: Current Rank, Date, Gold, Honour, Peasants / Maximum Peasants,
  /// Popularity). Resolved from the map file the flow has loaded and the
  /// install's own string table - nothing else.
  ///
  /// WHAT THE MISSION'S OWN DATA CARRIES, measured 2026-10-13 (the port's
  /// readers already decoded all of it - MissionReader / ScenarioReader):
  /// - the Date: Scenario.StartingYear + Scenario.StartingMonth. The month is
  ///   0-BASED, and the install pins that: the shipped manual's chapter editor
  ///   says "The default date is January 1066", and war_chapter2.s2m and
  ///   war_chapter10.s2m - maps whose scenarios were left at that default -
  ///   store 1066-00. war_chapter4.s2m stores 1084-00, i.e. January 1084.
  /// - Gold: Mission.StartingGold (war_chapter4: 1000 - the same number
  ///   scenario_missions.csv carries, an independent read).
  /// - Popularity: Mission.StartingPopularity (war_chapter4: 90).
  /// The month's NAME comes from the install's own table (MONTH_JANUARY..DECEMBER,
  /// 'Jan'..'Dec'), so the date row reads e.g. "Jan 1084".
  ///
  /// WHAT IT DOES NOT CARRY, named so nothing later assumes it (see
  /// <see cref="RuntimeFieldsNote"/> and <see cref="SlotAttributionNote"/>): the
  /// mission's data holds no honour, no peasants and no rank - those three of the
  /// six fields are the RUNTIME's own state, which this port does not simulate
  /// yet - and the install's data does not pin which painted bar slot carries
  /// which field (the rollovers name the six but not their positions), so the
  /// port reads them in the rollover table's own order and says so.
  /// </summary>
  public sealed class S2MMissionState
  {
    /// <summary>What the port can say about the bar's slot attribution - printed on the report.</summary>
    public const string SlotAttributionNote =
      "the bar's painted slots are attributed to MAINSCREENROLLOVERS079..091 BY THE ROWS' OWN ORDER "
      + "(the install names the six fields and the seven controls but not their positions), and the "
      + "strip's seventh slot is named by no row at all - the port's reading, marked";

    /// <summary>Which of the bar's six fields the mission's own data can fill - printed on the report.</summary>
    public const string RuntimeFieldsNote =
      "three of the bar's six fields are the RUNTIME's own state (Current Rank, Honour, "
      + "Peasants / Maximum Peasants) - the mission's data carries no honour, peasants or rank and "
      + "this port runs no simulation yet, so those slots are left as the install paints them; "
      + "Date, Gold and Popularity ARE the mission's own decoded data";

    /// <summary>The install's own month rows, in the calendar's order (StartingMonth is 0-based here).</summary>
    public static readonly string[] MonthRows =
    {
      "MONTH_JANUARY", "MONTH_FEBRUARY", "MONTH_MARCH", "MONTH_APRIL",
      "MONTH_MAY", "MONTH_JUNE", "MONTH_JULY", "MONTH_AUGUST",
      "MONTH_SEPTEMBER", "MONTH_OCTOBER", "MONTH_NOVEMBER", "MONTH_DECEMBER",
    };

    /// <summary>The flow's own map path (maps/war_chapterN.s2m), as the click named it.</summary>
    public string MapPath { get; private set; }

    /// <summary>The map file's stem (war_chapter4).</summary>
    public string MapStem { get; private set; }

    /// <summary>True when the map's Scenario resolved to its first Mission.</summary>
    public bool HasMission { get; private set; }

    /// <summary>The scenario's starting year (war_chapter4: 1084).</summary>
    public int StartingYear { get; private set; }

    /// <summary>The scenario's starting month, 0-based (war_chapter4: 0 = January; the manual pins the reading).</summary>
    public int StartingMonthIndex { get; private set; }

    /// <summary>The install's own row for that month (MONTH_JANUARY), or null.</summary>
    public string MonthRow { get; private set; }

    /// <summary>The shipped month text ('Jan'), or null when the table has no row.</summary>
    public string MonthText { get; private set; }

    /// <summary>The date as the bar carries it: the shipped month text + the year ('Jan 1084'), or '' .</summary>
    public string DateText { get; private set; } = "";

    /// <summary>The mission's own starting gold (war_chapter4: 1000).</summary>
    public int StartingGold { get; private set; }

    /// <summary>The mission's own starting popularity (war_chapter4: 90).</summary>
    public int StartingPopularity { get; private set; }

    /// <summary>How many missions the scenario carries (the bar shows the first; war chapters have one).</summary>
    public int MissionCount { get; private set; }

    /// <summary>What could not be resolved, named - null when all of it did.</summary>
    public string Note { get; private set; }

    /// <summary>
    /// Resolves the mission's own starting state from the map file the flow has
    /// loaded. A map whose Scenario or first Mission does not resolve keeps
    /// HasMission false and a Note that names it - the main screen then shows no
    /// numbers rather than inventing any.
    /// </summary>
    public static S2MMissionState Resolve(string mapPath, S2MFile mapFile, S2MTextTable table)
    {
      var state = new S2MMissionState { MapPath = mapPath };
      state.MapStem = string.IsNullOrEmpty(mapPath)
        ? "" : System.IO.Path.GetFileNameWithoutExtension(mapPath);

      if (mapFile == null || table == null)
      {
        state.Note = "no loaded map (or no string table) to resolve the mission's own state from";
        return state;
      }

      var byId = new Dictionary<int, S2Object>();
      foreach (S2Object obj in mapFile.MapHeaderObjects)
      {
        byId[obj.Id] = obj;
      }

      Scenario scenario = null;
      foreach (S2Object obj in mapFile.MapHeaderObjects)
      {
        if (obj is Scenario found)
        {
          scenario = found;
          break;
        }
      }

      if (scenario == null)
      {
        state.Note = "the map's MapHeader carries no Scenario, so it holds no mission state";
        return state;
      }

      // The date: the scenario's own year and 0-based month, with the month's
      // name from the install's table (the table ships 'Jan'..'Dec' - the
      // abbreviations are the install's, and the year is the map's own number).
      state.StartingYear = scenario.StartingYear;
      state.StartingMonthIndex = scenario.StartingMonth;
      int month = ((scenario.StartingMonth % 12) + 12) % 12;
      state.MonthRow = MonthRows[month];
      state.MonthText = table.Get(state.MonthRow);
      if (string.IsNullOrEmpty(state.MonthText))
      {
        state.Note = $"the install ships no {state.MonthRow} row";
        state.DateText = state.StartingYear.ToString();
      }
      else
      {
        state.DateText = $"{state.MonthText} {state.StartingYear}";
      }

      // The mission the scenario starts with (the war chapters carry exactly
      // one; the Sim campaign's ladder carries more, and the bar shows the
      // first - named here rather than silently picked).
      state.MissionCount = scenario.MissionObjectIds.Count;
      if (state.MissionCount > 0
          && byId.TryGetValue(scenario.MissionObjectIds[0], out S2Object missionObject)
          && missionObject is Mission mission)
      {
        state.HasMission = true;
        state.StartingGold = mission.StartingGold;
        state.StartingPopularity = mission.StartingPopularity;
        if (state.MissionCount > 1)
        {
          state.Note = $"the scenario carries {state.MissionCount} missions; the main screen shows the first";
        }
      }
      else
      {
        state.Note = "the scenario's first mission id does not resolve to a Mission, so its "
                     + "gold and popularity are not in the loaded map";
      }

      return state;
    }
  }
}
