namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Current Quest Status' (the table row is spelled 'TRIGGER_COMPLETE QUEST').
  /// Three one-byte flags, one per quest slot: the byte at position i answers for
  /// quest i (the game's UI labels the slots Quest 1..3,
  /// MISSIONOBJECTIVESSCREEN_QUEST_1..3). All seven non-zero combinations appear
  /// across the shipped instances and the all-zero one does not; war_chapter6's
  /// win condition is the all-set pattern, and the invasion schedules of
  /// war_chapter4/6 gate pairs of quests at +2 months and single quests at +3 -
  /// evidence that a set flag means 'this quest is complete'. The runtime's exact
  /// AND semantics are the game's own, not the authoring data's.
  /// </summary>
  internal class QuestCompleteTrigger : Trigger
  {
    public bool Quest0Complete { get; set; }
    public bool Quest1Complete { get; set; }
    public bool Quest2Complete { get; set; }
  }
}
