using UnityEngine;
using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// The campaign screen's own actions, in the shipped screen's own order: the
  /// twelve chapter rows of the 'Lost King' campaign, and the way back to the
  /// war screen.
  ///
  /// Where these come from: the same shipped translation table as every other
  /// frontend surface here (i18n/en-us.xml - the shipped help file documents the
  /// game, not the frontend). The screen is the campaign's chapter list, which
  /// the war screen's WARSCREEN_CAMPAIGN row opens, and the table names its rows
  /// twice over: the chapters themselves (WARCAMPAIGN_MISSION1..12 'Chapter 1'..
  /// 'Chapter 12' - the button labels) and each button's static tooltip
  /// (CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12 'Start Chapter 1'..'Start Chapter
  /// 12'), plus its own way back (CAMPAIGNSCREENSTATICTOOLTIP_EXIT 'Previous
  /// Menu'). The maps are shipped too: maps/war_chapter1..12.s2m, one per
  /// chapter row.
  ///
  /// The art is the install's, from ui/campaign_screen/: the twelve per-chapter
  /// pictures (the port opens on chapter 1's - see UIAssets) and the one button
  /// the screen reuses, campaign_button.bmp.
  ///
  /// The chapter rows are where the frontend ends and the game begins: a
  /// chapter's click starts its own map (the row's MapPath), loaded by the
  /// game's own flow - so this screen's action surface carries map-starting rows
  /// as well as screen-opening ones.
  /// </summary>
  public static class CampaignScreenActions
  {
    /// <summary>The campaign's twelve chapters - the screen's own rows.</summary>
    public const int ChapterCount = 12;

    /// <summary>
    /// The screen's twelve chapter buttons, in the shipped screen's own order.
    /// Every one of them STARTS its chapter now: the click names the row's own
    /// maps/war_chapterN.s2m and asks the flow for it
    /// (Game.RequestStartChapter), which loads that map through the game's own
    /// flow (MAP_LOADING -> MAP_LOADED) - the one frontend click that leaves the
    /// frontend for the game.
    /// </summary>
    public static readonly CampaignScreenRow[] Chapters = BuildChapters();

    /// <summary>
    /// The way back (CAMPAIGNSCREENSTATICTOOLTIP_EXIT 'Previous Menu') - the
    /// screen row on this screen with a frontend screen behind it, and it is the
    /// WAR SCREEN the campaign screen was opened from: the frontend is a tree,
    /// and this screen is a branch of the war screen exactly as the war screen is
    /// a branch of the play screen.
    /// </summary>
    public static readonly CampaignScreenRow Back = new(
        "CAMPAIGNSCREENSTATICTOOLTIP_EXIT", "", "Previous Menu", "",
        "the war screen (the screen the campaign screen was opened from, which the port has)",
        null, GameState.WAR_SCREEN, UIAssets.FancyButtonTexture, "BackButton");

    /// <summary>Every row this screen dispatches: the twelve chapters, then the way back.</summary>
    public static readonly CampaignScreenRow[] All = AppendBack();

    /// <summary>The row for one chapter (0-based; chapter 1 is index 0).</summary>
    public static CampaignScreenRow Chapter(int index) => Chapters[index];

    private static CampaignScreenRow[] BuildChapters()
    {
      var chapters = new CampaignScreenRow[ChapterCount];
      for (int index = 0; index < ChapterCount; index++)
      {
        int chapter = index + 1;
        chapters[index] = new(
            $"CAMPAIGNSCREENSTATICTOOLTIP_PLAY_{chapter}",
            $"WARCAMPAIGN_MISSION{chapter}",
            $"Chapter {chapter}",
            $"Start Chapter {chapter}",
            $"maps/war_chapter{chapter}.s2m (the chapter's map)",
            $"maps/war_chapter{chapter}.s2m",
            GameState.NONE,
            UIAssets.CampaignButtonTexture,
            $"Chapter{chapter}Button");
      }
      return chapters;
    }

    private static CampaignScreenRow[] AppendBack()
    {
      var all = new CampaignScreenRow[Chapters.Length + 1];
      Chapters.CopyTo(all, 0);
      all[Chapters.Length] = Back;
      return all;
    }

    /// <summary>How many clicks have been dispatched since this screen was built.</summary>
    public static int DispatchCount { get; private set; }

    /// <summary>The last dispatched row (only meaningful after a dispatch).</summary>
    public static CampaignScreenRow LastDispatch { get; private set; }

    /// <summary>True once any click has been dispatched on this screen.</summary>
    public static bool HasDispatch { get; private set; }

    /// <summary>
    /// Starts the record over. CampaignScreenUI builds its buttons once per
    /// showing, so the record always describes the screen that is on it.
    /// </summary>
    public static void ResetRecord()
    {
      DispatchCount = 0;
      HasDispatch = false;
      LastDispatch = default;
    }

    /// <summary>
    /// THE entry point for this screen: every one of its buttons lands here and
    /// nothing else dispatches a campaign-screen action. Names the row, records
    /// it, reports its destination, and asks the flow for what the row carries -
    /// the way back's screen (Game.RequestFrontendScreen, the move every frontend
    /// click makes) or, for a chapter row, the map its click STARTS
    /// (Game.RequestStartChapter, the one ask that leaves the frontend).
    /// </summary>
    public static void Dispatch(CampaignScreenRow row)
    {
      DispatchCount++;
      LastDispatch = row;
      HasDispatch = true;
      Debug.Log($"[CampaignScreenActions] click: {row.Id} '{row.Label}' -> {row.Outcome}");
      if (row.Opens != GameState.NONE)
      {
        Game.GetInstance()?.RequestFrontendScreen(row.Opens);
      }
      else if (!string.IsNullOrEmpty(row.MapPath))
      {
        Game.GetInstance()?.RequestStartChapter(row.MapPath);
      }
    }
  }
}
