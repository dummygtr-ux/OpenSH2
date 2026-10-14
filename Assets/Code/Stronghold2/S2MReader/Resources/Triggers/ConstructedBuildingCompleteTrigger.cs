namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Building Site Complete' (TRIGGER_CONSTRUCTED_BUILDING_COMPLETE; the table
  /// also ships per-site rows - 'Build Beacon', 'Rebuild the Ruined Bridge',
  /// 'Rebuild Ruined Keep', 'Rebuild Ruined Monastery', 'Rebuild Wooden Bridge').
  /// The trigger's second int is the site's completion percentage: -1 in every
  /// shipped instance, which reads as the complete sentinel beside the
  /// percent-complete sibling's real percents (20, 65).
  /// </summary>
  internal class ConstructedBuildingCompleteTrigger : Trigger
  {
    public int Percentage { get; set; }
  }
}
