namespace Assets.Code.Stronghold2.S2MReader.Resources.Actions
{
  /// <summary>
  /// A per-map bespoke action (no table row the vocabulary join could pair): plays
  /// a cut scene. The single int is the cut scene's id within the campaign's own
  /// numbering; values 0..6 across the shipped instances (war_chapter4 uses 0, 1
  /// and 2 - one per quest - and war_chapter6 uses 4 and 6). The id does NOT
  /// index the shipped cutscenes folder by file name (war_chapter6's id 4 sits
  /// beside four warMission6_*.csc files), so what table it resolves through is an
  /// open question.
  /// </summary>
  internal class CustomActionPlayCutScene : Action
  {
    public int CutSceneId { get; set; }
  }
}
