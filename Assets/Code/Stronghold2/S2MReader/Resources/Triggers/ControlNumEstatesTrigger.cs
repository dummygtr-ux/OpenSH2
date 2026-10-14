namespace Assets.Code.Stronghold2.S2MReader.Resources.Triggers
{
  /// <summary>
  /// 'Control a number of Estates' (TRIGGER_CONTROL_NUM_ESTATES; the table also
  /// ships TRIGGER_CONTROL_NUM_ESTATES_buy 'Buy a Number of Estates'). The number
  /// of estates the player must control; values 2, 3, 6 and 9 across the shipped
  /// instances (Kill Barclay, hawk rescue, tutorial, war_chapter6).
  /// </summary>
  internal class ControlNumEstatesTrigger : Trigger
  {
    public int NumEstates { get; set; }
  }
}
