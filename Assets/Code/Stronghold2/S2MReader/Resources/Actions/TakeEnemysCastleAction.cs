using Assets.Code.Stronghold2.S2MReader.Enums;

namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// 'Take Enemy's Castle' (ACTION_TAKE_ENEMYS_CASTLE - the class name's extra 's'
  /// is the shipped typo). The single int is the lord whose castle is taken, in
  /// the class family's lord numbering (2 = Olaf .. 10 = SirGrey, the same switch
  /// the port's TakeEnemyCastleAction reader uses). The one shipped instance, in
  /// war_chapter6, carries 7 = Edwin - and Edwin owns six of that map's eight
  /// placed estates per the Scenario's ownership table, the map's antagonist.
  /// </summary>
  internal class TakeEnemysCastleAction : Action
  {
    public S2MLords Lord { get; set; }
  }
}
