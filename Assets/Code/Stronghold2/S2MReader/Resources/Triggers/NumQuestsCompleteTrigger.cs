namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Number of Quests Complete' (TRIGGER_NUM_QUESTS_COMPLETE). The count of
  /// completed quests; values 1 and 2 across the shipped instances (hawk rescue,
  /// war_chapter6). Each instance gates a reward or a rule change - e.g. war_ch6
  /// unlocks troop types once 2 quests are complete.
  /// </summary>
  internal class NumQuestsCompleteTrigger : Trigger
  {
    public int NumQuests { get; set; }
  }
}
