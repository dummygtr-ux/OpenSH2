namespace Assets.Code.UI
{
  /// <summary>What one of the load screen's own buttons does.</summary>
  public enum LoadScreenActionKind
  {
    /// <summary>
    /// The screen's own row, LOADSCENARIOSCREEN_LOAD 'Load' - the way on from
    /// the selected saved game into the game it holds. The port ships no saved
    /// games and has no save reader, so this click names the state rather than
    /// doing nothing silently.
    /// </summary>
    Load,

    /// <summary>
    /// The screen's own way back, LOADSCENARIOSCREEN_CANCEL 'Cancel' (with its
    /// own tooltip row, LOADSCENARIO_STATICTOOLTIP_CANCEL 'Cancel'): the war
    /// screen the screen was opened from.
    /// </summary>
    Cancel,

    /// <summary>
    /// One of the list's three column headers, whose click is the shipped sort
    /// the table names beside it: LOADSCENARIOSCREEN_NAME 'Name' with
    /// LOADSCENARIO_STATICTOOLTIP_FILENAMESORT 'Sort by Name'.
    /// </summary>
    SortName,

    /// <summary>LOADSCENARIOSCREEN_DATE 'Date' with LOADSCENARIO_STATICTOOLTIP_DATESORT 'Sort by Date'.</summary>
    SortDate,

    /// <summary>LOADSCENARIOSCREEN_PLAYERS 'P' with LOADSCENARIO_STATICTOOLTIP_PLAYERNUMBERSORT 'Sort by Number of Players'.</summary>
    SortPlayers,
  }

  /// <summary>
  /// One of the load screen's own rows - the screen's whole action surface -
  /// and the whole of what this port knows about it: the install's own id and
  /// text, the shipped tooltip CONTENT where the table ships one (the three
  /// sorts describe themselves; the screen's own rows do not), the button's
  /// scene name, and what its click does or would do.
  ///
  /// The screen is the engine's own `LoadScenarioScreen` (screen name 'load
  /// scenario'), whose table family is LOADSCENARIOSCREEN_*: the header row
  /// (HEADER_LOAD 'Load' / HEADER_SAVE 'Save' - the screen has two modes, and
  /// the war screen's row opens the LOAD one), the three column headers
  /// (NAME 'Name' / DATE 'Date' / PLAYERS 'P'), the two rows (LOAD 'Load' /
  /// CANCEL 'Cancel') and the three sorts. This port carries the LOAD mode and
  /// names the SAVE mode as the in-game menu's (the shipped game's own Save
  /// entry), which this screen's state machine does not have yet.
  /// </summary>
  public readonly struct LoadScreenAction
  {
    public LoadScreenAction(LoadScreenActionKind kind, string id, string label, string tooltip,
                            string outcome, string buttonName)
    {
      Kind = kind;
      Id = id;
      Label = label;
      Tooltip = tooltip;
      Outcome = outcome;
      ButtonName = buttonName;
    }

    /// <summary>What this button does.</summary>
    public readonly LoadScreenActionKind Kind;

    /// <summary>The install's own id for this row (i18n/en-us.xml).</summary>
    public readonly string Id;

    /// <summary>The row's shipped text - the label the button shows.</summary>
    public readonly string Label;

    /// <summary>
    /// The shipped tooltip content where the table has one (the three sorts);
    /// the screen's own rows ship theirs as separate tooltip rows, which the
    /// report prints. Empty where the table ships none.
    /// </summary>
    public readonly string Tooltip;

    /// <summary>What this click does or would do, in one sentence.</summary>
    public readonly string Outcome;

    /// <summary>The name the row's button carries in the scene ("LoadButton").</summary>
    public readonly string ButtonName;
  }
}
