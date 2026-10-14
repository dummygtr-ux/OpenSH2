using UnityEngine;
using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// The peace screen's own actions, in the shipped screen's own order: the
  /// three ways into the peace game modes, and the way back to the play screen.
  ///
  /// Where these come from: the same shipped translation table as the main
  /// menu's six and the play screen's four (i18n/en-us.xml). The peace screen is
  /// the screen the play screen's PLAYSCREEN_PEACE_TOOLTIP_TITLE row opens, and
  /// the table names its three buttons (PEACESCREEN_SIM_CAMPAIGN /
  /// PEACESCREEN_FREE_BUILD / PEACESCREEN_CUSTOM_MAPS) and its own way back
  /// (PEACECREENSTATICTOOLTIP_EXIT 'Previous menu' - the shipped table's own
  /// spelling, one S, and the only row it ships for this screen's way back:
  /// there is no correctly spelled twin, checked 2026-10-12).
  ///
  /// The pictures are the install's, from the folder the play screen's two path
  /// buttons came from: ui/war_peace_menu/peace_simcampaign.jpg and
  /// peace_freebuild.jpg, plus warpeace_custom.jpg for Custom Maps - the one
  /// picture the folder shares between this screen's custom maps and the war
  /// screen's custom scenarios, which is what its name says.
  /// </summary>
  public static class PeaceScreenActions
  {
    /// <summary>
    /// The screen's three picture buttons, in the order the shipped screen shows
    /// them. None of them has a destination in this port yet: the Sim campaign's
    /// chapters (the table's own SIM_CAMPAIGN_HEADING00..11), the free build map
    /// selection and the custom map list are left for later work, and
    /// each click says so by name rather than doing nothing.
    /// </summary>
    public static readonly PathScreenRow[] Pictures =
    {
      new("PEACESCREEN_SIM_CAMPAIGN", "Sim Campaign",
          "Help the King rebuild the county, in this gentler Sim campaign",
          "the Sim campaign's chapter list (SIM_CAMPAIGN_HEADING00..11 'The Kings Beacon'..'The Kings Visit')",
          GameState.NONE, UIAssets.PeaceSimCampaignTexture, "SimCampaignButton"),
      new("PEACESCREEN_FREE_BUILD", "Free Build",
          "Build peacefully in a setting of your own choosing",
          "the free build map selection (SCENARIOEDITORSCREEN_MAKE_FREEBUILD 'Free Build', "
          + "ui/map_select/mapselect_freebuild.bmp)",
          GameState.NONE, UIAssets.PeaceFreeBuildTexture, "FreeBuildButton"),
      new("PEACESCREEN_CUSTOM_MAPS", "Custom Maps", "Load a user created sim mission",
          "the custom map list (MAINSCENARIOSCREEN_LOAD_SCENARIO 'Load Scenario', "
          + "ui/map_select/mapselect_custom.bmp)",
          GameState.NONE, UIAssets.PeaceCustomMapsTexture, "CustomMapsButton"),
    };

    /// <summary>
    /// The way back (PEACECREENSTATICTOOLTIP_EXIT 'Previous menu') - the only row
    /// on this screen with a screen behind it, and it is the play screen the
    /// peace screen was opened from: the frontend's two path screens are a branch
    /// of the play screen, not of each other, and the play screen's own way back
    /// is the menu's.
    /// </summary>
    public static readonly PathScreenRow Back = new(
        "PEACECREENSTATICTOOLTIP_EXIT", "Previous menu", "",
        "the play screen (the screen the peace screen was opened from, which the port has)",
        GameState.PLAY_SCREEN, UIAssets.FancyButtonTexture, "BackButton");

    /// <summary>Every row this screen dispatches: the three pictures, then the way back.</summary>
    public static readonly PathScreenRow[] All = AppendBack();

    /// <summary>The row for one picture (the picture index, in the shipped screen's order).</summary>
    public static PathScreenRow Picture(int index) => Pictures[index];

    private static PathScreenRow[] AppendBack()
    {
      var all = new PathScreenRow[Pictures.Length + 1];
      Pictures.CopyTo(all, 0);
      all[Pictures.Length] = Back;
      return all;
    }

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static PathScreenRow LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. PeaceScreenUI builds its buttons once per showing,
    /// so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a peace-screen action. Names the row, records it,
    /// reports its destination - and, for the one row the port has built (the way
    /// back), asks the flow for it through the same logged transition the menu's
    /// Play uses.
    /// </summary>
    public static void Dispatch(PathScreenRow row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;
      Debug.Log($"[PeaceScreenActions] click: {row.Id} '{row.Label}' -> {row.Outcome}");
      if (row.Built) Game.GetInstance()?.RequestFrontendScreen(row.Opens);
    }
  }
}
