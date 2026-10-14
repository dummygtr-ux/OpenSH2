namespace Assets.Code.Stronghold2
{
  /// <summary>
  /// The scene unit convention, in one place: ONE S2M map cell = 2 metres =
  /// 1 Unity world unit, used by terrain, water, walls, vegetation and placed
  /// buildings together. Every renderer settings class takes its defaults from
  /// here.
  ///
  /// Raw game units (the f32 fields in the map files) are 1024 per cell
  /// horizontally AND vertically - the building/entity coordinate joins against
  /// the BuildingLayer grid pin 1024 raw = 1 cell, and unit standing heights
  /// (payload f32@33) equal the HeightLayer surface values raw for raw. So
  /// raw/1024 = cells, 1 metre = 512 raw height units, and heights convert with
  /// 1/512 (not 1/1024: that would make terrain and walls 2x flatter than the
  /// models standing on them).
  ///
  /// Evidence for 2 m/cell, from the game's own art (GR2 assets are Z-up
  /// centimetres; the models' GROUND footprints fill their BuildingLayer blobs
  /// at 2.00 m/cell - the roofs/eaves overhang by design):
  ///   tower_square_closed  base 6.15 x 6.26 m in a 3x3-cell blob (= 6 m)
  ///   gatehouse_small      base 6.15 x 6.15 m in a 3x3-cell blob
  ///   statue_lord          exactly 2.00 x 2.00 m in a 1x1-cell blob
  /// and the decoded structure-deck offsets are exact cell counts whose model
  /// heights agree only at 512 raw/m: TowerSquare deck +7,168 raw = 7 cells
  /// = 14.0 m vs 14.37 m measured on tower_square_closed (the 1024 raw/m
  /// reading would be 7.17 m, refuted by the mesh).
  /// Measurements: sh2-port/tools/model_surfaces.py, artifacts/models/*.json.
  ///
  /// Unit note (2026-10-08): those "6.15 m" figures are MESH GRID
  /// units and the grid is 205 units per cell. The game's own .bdg definitions
  /// declare the same models in true centimetres at 200 units/cell (tower base
  /// 6.00 m, the 1-cell props exactly 200, the 12-cell monastery 2411), i.e.
  /// the art is drawn 2.5% larger than the size the bdg declares - deliberate
  /// overhang, NOT an error. The rendered metre space (mesh unit = 1 cm) is
  /// self-consistent: do not rescale by 0.9756. Numbers:
  /// sh2-port/manifests/bdg_fields.csv (tools/bdg_fields.py).
  /// </summary>
  public static class S2MWorldScale
  {
    /// <summary>Metres per S2M map cell - also the Unity world units per cell.</summary>
    public const float MetresPerCell = 2f;

    /// <summary>Raw game units per map cell for building/entity/height data.</summary>
    public const float RawUnitsPerCell = 1024f;

    /// <summary>
    /// The forest payload stores x/z at twice that resolution: its raw
    /// coordinates are 2048 per cell (verified on war_chapter1, The Four Woods
    /// and Fenis - raw/2048 lands on the record's own cellX/cellZ + a 0..1
    /// fraction; raw/1024 would double every tree's distance from the origin).
    /// </summary>
    public const float ForestRawUnitsPerCell = 2048f;

    /// <summary>Raw height units per metre (1024 per cell / 2 m per cell).</summary>
    public const float RawUnitsPerMetre = RawUnitsPerCell / MetresPerCell;

    /// <summary>Metres per raw height unit, for every renderer settings default.</summary>
    public const float HeightUnitScale = 1f / RawUnitsPerMetre;
  }
}
