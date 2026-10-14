namespace Assets.Code.UI
{
  /// <summary>
  /// The war screen: the screen the play screen's War button opens
  /// (GameState.WAR_SCREEN), carrying the six shipped WARSCREEN_* rows
  /// (<see cref="WarScreenActions"/>) on the backdrop the two path screens share
  /// - see <see cref="PathScreenUI"/>, which builds it.
  ///
  /// Its six pictures make two grid rows. The backdrop's own painted frames are
  /// centred at +0.10 of the content height, so the two rows straddle that: the
  /// first where the frames are, the second on the plain stone between them and
  /// the empty description bar, with the rows' captions clear of the bar's top
  /// edge (-0.26).
  /// </summary>
  class WarScreenUI : PathScreenUI
  {
    protected override string DisplayName => "War screen";
    protected override string RootName => "WarScreenUI";
    protected override int CanvasOrder => 1300;  // above the play screen's 1200
    protected override PathScreenRow[] Pictures => WarScreenActions.Pictures;
    protected override PathScreenRow Back => WarScreenActions.Back;

    /// <summary>The two rows straddle the backdrop's own painted frames (+0.10).</summary>
    private static readonly float[] RowCentreValues = { 0.280f, -0.050f };
    protected override float[] RowCentres => RowCentreValues;

    protected override void ResetDispatchRecord() => WarScreenActions.ResetRecord();
    protected override void Dispatch(PathScreenRow row) => WarScreenActions.Dispatch(row);
  }
}
