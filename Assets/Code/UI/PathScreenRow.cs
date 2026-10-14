using Assets.Code;

namespace Assets.Code.UI
{
  /// <summary>
  /// One row of one of the two path screens behind the play screen - the war
  /// screen (WARSCREEN_*) or the peace screen (PEACESCREEN_*) - and the whole
  /// of what this port knows about it: the install's own name for the row, the
  /// text it shows, the picture it shows it on, the screen the click would open
  /// in the shipped game, and the screen THIS port can really show for it.
  ///
  /// Both path screens are the same kind of screen (the shipped backdrop, a grid
  /// of picture buttons, one way back), so they share this row type; what
  /// differs is only the rows each one carries, which live in
  /// <see cref="WarScreenActions"/> and <see cref="PeaceScreenActions"/>. The
  /// picture rides on the row rather than in the screen's layout code, so a row
  /// cannot be shown on another row's picture - the same discipline that keeps a
  /// button from dispatching an action that is not its own.
  /// </summary>
  public readonly struct PathScreenRow
  {
    public PathScreenRow(string id, string label, string tooltip, string destination,
                         GameState opens, string texture, string buttonName)
    {
      Id = id;
      Label = label;
      Tooltip = tooltip;
      Destination = destination;
      Opens = opens;
      Texture = texture;
      ButtonName = buttonName;
    }

    /// <summary>The install's own id for this row (i18n/en-us.xml).</summary>
    public readonly string Id;

    /// <summary>The row's shipped English text - the button's label.</summary>
    public readonly string Label;

    /// <summary>
    /// The shipped tooltip CONTENT where the table has one (the six war rows and
    /// the three peace rows describe themselves; the two ways back do not). This
    /// port shows the label on the button and keeps the content on the row - the
    /// play screen's decision, taken again here.
    /// </summary>
    public readonly string Tooltip;

    /// <summary>
    /// What this row would open in the shipped game, named from the install's own
    /// table, so the sentence a click logs is the shipped screen's name and not a
    /// name this port invented.
    /// </summary>
    public readonly string Destination;

    /// <summary>
    /// The screen this action opens in THIS port, or GameState.NONE while the
    /// port has not built it. <see cref="Built"/> is derived from this, not
    /// toggled beside it, so a row is built exactly when it names a screen the
    /// flow can really show.
    /// </summary>
    public readonly GameState Opens;

    /// <summary>
    /// The install's own picture for this row: a 366x367 jpg with its frame and
    /// G medallion for the picture rows, and the frontend's fancy button for the
    /// two ways back (the table names those rows, not their picture).
    /// </summary>
    public readonly string Texture;

    /// <summary>The name the row's button carries in the scene ("CampaignButton").</summary>
    public readonly string ButtonName;

    /// <summary>True when this row's destination exists in the port.</summary>
    public bool Built => Opens != GameState.NONE;

    /// <summary>
    /// The one wording of what a click did - the destination when the port has
    /// built it, otherwise the same fact said plainly, named. Shared by the log
    /// line and the UAT.
    /// </summary>
    public string Outcome =>
      Built ? Destination : $"no destination yet: {Destination} is not built";
  }
}
