namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Building Site Percent Complete' (TRIGGER_CONSTRUCTED_BUILDING_PERCENT_COMPLETE).
  /// The trigger's second int is the completion percentage the site must reach;
  /// values 20 and 65 across the shipped instances (war_chapter2, war_chapter5).
  /// </summary>
  internal class ConstructedBuildingPercentCompleteTrigger : Trigger
  {
    public int Percentage { get; set; }
  }
}
