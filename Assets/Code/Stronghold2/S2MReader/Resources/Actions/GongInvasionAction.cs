namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// 'Gong Infestation' (ACTION_GONG_INVASION). The table names its two
  /// parameters: 'Estate Marked By Flag' (GONGINVASIONACTION_ESTATE_POINT) and
  /// 'Number of Gong Piles' (GONGINVASIONACTION_TITLE). The flag is the port's
  /// usual color + number pair (read in that order, as InvasionAction reads its
  /// points); values across the shipped instances: (Blue, 2, 8) and (Red, 2, 3),
  /// both in peace_chapter1.
  /// </summary>
  internal class GongInvasionAction : Action
  {
    public FlagColor EstatePointFlagColor { get; set; }
    public int EstatePointFlagNumber { get; set; }
    public int NumGongPiles { get; set; }
  }
}
