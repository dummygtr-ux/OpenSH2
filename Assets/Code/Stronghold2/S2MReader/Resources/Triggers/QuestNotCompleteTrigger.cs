namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Quest Not Complete' (TRIGGER_NOT_COMPLETE QUEST). The quest the trigger
  /// watches, 0-based; values 0 and 2 across the shipped instances (war_chapter4,
  /// war_chapter6). war_chapter6 combines it with SingleQuestCompleteTrigger to
  /// fire only when one quest is done and another is not.
  /// </summary>
  internal class QuestNotCompleteTrigger : Trigger
  {
    public int Quest { get; set; }
  }
}
