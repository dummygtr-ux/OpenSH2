namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Single Quest Complete' (TRIGGER_SINGLE_COMPLETE QUEST). The quest the
  /// trigger watches, 0-based; values 0..2 across the shipped instances, matching
  /// the game's three quest slots (MISSIONOBJECTIVESSCREEN_QUEST_1..3). The index
  /// is cross-checked in war_chapter4: its three cutscene events pair quest 0/1/2
  /// with CustomActionPlayCutScene values 0/1/2.
  /// </summary>
  internal class SingleQuestCompleteTrigger : Trigger
  {
    public int Quest { get; set; }
  }
}
