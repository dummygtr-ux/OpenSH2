namespace Assets.Code.UI
{
  /// <summary>What one of the map request screen's own buttons does.</summary>
  public enum MapRequestScreenActionKind
  {
    /// <summary>
    /// The screen's own row, MAPREQUESTSCREEN_PLAY 'Play' - the way on from the
    /// selected map into the custom game, and the one row the shipped table
    /// ships for this screen.
    /// </summary>
    Play,

    /// <summary>
    /// One map of the list the screen shows (read from the install's own maps/
    /// folder): the click SELECTS that map, and the screen's title strip and
    /// its list highlight follow it.
    /// </summary>
    SelectMap,

    /// <summary>
    /// One page back in the map list, on the screen's own left painted arrow.
    /// The install ships NO row and no wording for the two arrows - the art
    /// paints them and nothing else - so this one is the port's own naming of
    /// what the arrow does.
    /// </summary>
    PreviousPage,

    /// <summary>One page on in the map list, on the screen's own right painted arrow.</summary>
    NextPage,

    /// <summary>The way back to the war screen the screen was opened from.</summary>
    Back,
  }

  /// <summary>
  /// One map of the install's own list, as this port reads it at run time: the
  /// file's own name (the install's own spelling - some of the shipped files
  /// are all-lowercase, like 'hawk rescue.s2m', and the port keeps what the
  /// file keeps), the stem the list row shows, and the full path a future load
  /// would name.
  ///
  /// Which files are in the list is MEASURED, not guessed (2026-10-13):
  /// the shipped engine's own map request screen
  /// filters its list by the name prefixes 'war_chapter', 'peace_chapter' and
  /// 'tutorial' (the three literals its code compares against, read out of
  /// stronghold2.exe), so the port lists the install's *.s2m scenarios with
  /// exactly those three families left out - the campaign's chapters belong to
  /// the campaign screen and the tutorial to the play screen's medallion. The
  /// install's 19 historical *.s2c maps are NOT in this list either: they are
  /// the siege selection's own scenarios, which that screen already carries.
  /// The mirror repeats many names in two spellings (the case-corrected
  /// symlinks), so the reader groups by the lowercased name and prefers the
  /// spelling that carries capitals - the install's own file - which is what
  /// turns 81 *.s2m directory entries into 34 maps.
  /// </summary>
  public readonly struct MapRequestMap
  {
    public MapRequestMap(string fileName, string stem, string path)
    {
      FileName = fileName;
      Stem = stem;
      Path = path;
    }

    /// <summary>The file's own name, the install's own spelling ('Arena of Kings.s2m').</summary>
    public readonly string FileName;

    /// <summary>The file's stem - what the list row shows ('Arena of Kings').</summary>
    public readonly string Stem;

    /// <summary>The full path of the file, for a future load and for the logs.</summary>
    public readonly string Path;
  }

  /// <summary>
  /// One of the map request screen's own buttons - the screen's whole action
  /// surface - and the whole of what this port knows about it: the install's
  /// row where the table ships one (Play, and the way back's own words), the
  /// port's plain name where it does not (the two painted arrows), the picture
  /// it wears, the button's scene name, and what its click does or would do.
  /// </summary>
  public readonly struct MapRequestScreenAction
  {
    public MapRequestScreenAction(MapRequestScreenActionKind kind, string id, string label,
                                  string outcome, string texture, string buttonName,
                                  int mapIndex)
    {
      Kind = kind;
      Id = id;
      Label = label;
      Outcome = outcome;
      Texture = texture;
      ButtonName = buttonName;
      MapIndex = mapIndex;
    }

    /// <summary>What this button does.</summary>
    public readonly MapRequestScreenActionKind Kind;

    /// <summary>
    /// The install's own id for this row (i18n/en-us.xml), or "" where the
    /// install ships NO row - which is the two painted arrows: the art paints
    /// them and the table says nothing about them, so the port names them
    /// rather than inventing an id.
    /// </summary>
    public readonly string Id;

    /// <summary>
    /// The row's shipped text where the table ships one ('Play'), "" for the
    /// two arrows (their picture is the install's own painted arrow, and the
    /// port draws no words the install does not ship).
    /// </summary>
    public readonly string Label;

    /// <summary>
    /// What this click does or would do, in one sentence - the log line and
    /// the UAT read the same wording.
    /// </summary>
    public readonly string Outcome;

    /// <summary>The install's own picture for this button, or "" where the screen's folder ships none.</summary>
    public readonly string Texture;

    /// <summary>The name the row's button carries in the scene ("PlayButton").</summary>
    public readonly string ButtonName;

    /// <summary>Which map of the list this row is about (-1 for the non-map rows).</summary>
    public readonly int MapIndex;

    /// <summary>
    /// How this row names itself in the log and the checks: the install's own
    /// id and text where it ships them, the list row's own stem where the row is
    /// a map of the install's list, and the port's plain name of the painted
    /// arrow where the install ships no row at all.
    /// </summary>
    public string Describe =>
      Id.Length > 0 ? $"{Id} '{Label}'"
      : Kind == MapRequestScreenActionKind.SelectMap ? $"the list row '{Label}'"
      : Kind == MapRequestScreenActionKind.PreviousPage
        ? "the screen's own left painted arrow (the install ships no row for it)"
        : "the screen's own right painted arrow (the install ships no row for it)";
  }
}
