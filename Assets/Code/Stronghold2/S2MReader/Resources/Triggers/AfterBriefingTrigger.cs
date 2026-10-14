namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'After Briefing' in the editor (TRIGGER_AFTER_BRIEFING). Fires once the
  /// mission's briefing has been shown. Carries no data payload: the serialized
  /// form is the trigger header, the data payload marker with the instance index,
  /// and the trailer (25 bytes total) - the same shape in all 41 shipped
  /// instances (war_chapter4, war_chapter6).
  /// </summary>
  internal class AfterBriefingTrigger : Trigger
  {
  }
}
