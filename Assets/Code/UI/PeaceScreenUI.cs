namespace Assets.Code.UI
{
  /// <summary>
  /// The peace screen: the screen the play screen's Peace button opens
  /// (GameState.PEACE_SCREEN), carrying the three shipped PEACESCREEN_* rows
  /// (<see cref="PeaceScreenActions"/>) on the backdrop the two path screens
  /// share - see <see cref="PathScreenUI"/>, which builds it.
  ///
  /// Its three pictures make one grid row, and that row stands exactly where the
  /// war screen's first row stands (the same columns, the same centre-y): the
  /// backdrop's own painted frames are centred at +0.10 of the content height,
  /// which is where a single row belongs. None of the screen's rows reaches the
  /// description bar at the bottom, where the way back sits.
  /// </summary>
  class PeaceScreenUI : PathScreenUI
  {
    protected override string DisplayName => "Peace screen";
    protected override string RootName => "PeaceScreenUI";
    protected override int CanvasOrder => 1300;  // above the play screen's 1200
    protected override PathScreenRow[] Pictures => PeaceScreenActions.Pictures;
    protected override PathScreenRow Back => PeaceScreenActions.Back;

    /// <summary>The one row stands where the war screen's first row stands.</summary>
    private static readonly float[] RowCentreValues = { 0.100f };
    protected override float[] RowCentres => RowCentreValues;

    protected override void ResetDispatchRecord() => PeaceScreenActions.ResetRecord();
    protected override void Dispatch(PathScreenRow row) => PeaceScreenActions.Dispatch(row);
  }
}
