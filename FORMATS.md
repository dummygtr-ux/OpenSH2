# FORMATS.md — Stronghold 2 file-format index

One entry per format the pipeline touches. **Handler** = code that reads/writes it
(sh2-unity C# unless prefixed `sh2-port/`). **Detail notes** hold the full evidence;
this file is the index. Completeness: DONE / PARTIAL / UNKNOWN. Guesses marked
"(unverified)". Per-format TODOs and open questions live inline in the
entries below.

Repos: `sh2-unity/` = Unity project (readers/renderers), `sh2-port/` = extraction lab
(tools, artifacts, manifests). Never open game assets / Library/ / artifacts binaries
while working; numbers live in `sh2-port/manifests/`.

## Scene units (runtime convention) — DONE (decided 2026-10-08)

**1 S2M map cell = 2 metres = 1 Unity world unit.** 1024 raw game units per cell
horizontally AND vertically, so cells = raw/1024 and **metres = raw/512** for
heights (before 2026-10-08 the renderers used raw/1024 as world units, which put
every model at 2.36× its footprint and made terrain/walls read 2× too flat).
The forest payload stores x/z at **2048 raw per cell** instead. GR2 models are
centimetres loaded at `UnitScale 0.01` (cm→m) and now land 1:1 on the terrain.

Authority: `Assets/Code/Stronghold2/S2MWorldScale.cs` — every renderer settings
class takes its defaults from it. Decisive reference (the game's own art, not by
eye): model **ground** footprints fill their BuildingLayer blobs at 2.00 m/cell
(tower_square_closed base 6.15×6.26 m in a 3×3 blob, gatehouse_small 6.15×6.15 in
3×3, statue_lord 2.00×2.00 in 1×1) and the decoded deck offsets match the meshes
only at 512 raw/m (TowerSquare +7,168 raw = 14.0 m vs 14.37 m measured).
**Refined 2026-10-08:** those "6.15 m" numbers are *mesh grid*
units, and the grid is **205 units per cell**, not 200 — the game's own `.bdg`
definitions declare the same models in true centimetres at 200/cell (tower
6.00 m, torture/1-cell props exactly 200, monastery 2411 for 12 cells). So the
art is drawn **2.5% larger than its declared size** (models deliberately
overhang their footprints); the rendered metre space (mesh unit = 1 cm) is
self-consistent and must NOT be rescaled by 0.9756 to "fix" this. Measurements +
post-change engine checks: `sh2-port/manifests/scene_scale.json` (`tools/model_surfaces.py`
reproduces the model numbers; `.bdg` numbers: `manifests/bdg_fields.csv`).
**Captures written before 2026-10-08 are pre-rescale and stale.**

## .s2m — map container — PARTIAL (core DONE, many payloads unknown)

- Handlers: `S2MReader/S2MReader.cs` + `ObjectReaders/*`; independent: `sh2-port/tools/s2m_parse.py`
  (validated 48/48 maps), `s2m_extract.py`, `building_parse.py`, `walls_parse.py`,
  `deck_analyze.py` (plane-2 taxonomy + unit cross-check), `bdg_info.py`,
  `bdg_fields.py` (bdg numeric fields: footprint/anchor/bbox).
- Detail notes: `Notes/FileFormats/S2M/` (Overview, FileHeader, Object, per-segment).
- **Header property block + the `author` key (2026-10-09, third-party
  corroboration).** A community map unlocker (`others/sh2-unofficial-patch/
  tools/win/s2mEditable.cpp` + `tools/README.md`) parses the plain header as
  `u32 strCount` + `{u32 keyLen, ascii key, u32 charCount, UTF-16LE value}`
  pairs, then an int block, then the zlib body starting `78 9C` — the same shape
  our `S2MReader`/`s2m_parse.py` already handle. New fact: the game's map
  browser **hides a map from the editor's load list purely on the presence of
  an `author` key in that block — the value is never compared** (their tool
  deletes it to make official maps editable; re-saving in the in-game editor
  re-adds it from a copy serialized inside the compressed body). Useful as an
  authoring-mode marker; nothing to implement, noted for provenance.
- **In-engine reader coverage (2026-10-08, second sweep over all 67 maps,
  `manifests/reader_decode_fails.csv`):** **all 67 files load with zero
  DECODE-FAIL lines** — 48 `.s2m` + the 19 `.s2c` historical castle maps. The
  first sweep of the same day found 19 `.s2c` maps aborting `Load` (header map
  type `historical` rejected) and 74 DECODE-FAIL warnings across 22 `.s2m` maps
  (each one an object falling back to a raw payload); the manifest now carries both the per-map
  before-counts and the after-counts (all 0).
- **Action/trigger payload variants — DONE (2026-10-08),** full evidence in
  `Notes/FileFormats/S2M/MapHeaderSegment/Actions and Triggers/VariantPayloads.md`.
  Four facts the old readers got wrong, all verified against the bytes of every
  shipped object: (a) the payload marker's `{i32 unknown, i32 byte length}` pair
  is written only on data-carrying objects, so presence is decided from the bytes
  remaining before the trailer, not from the type (`FireAction` has data —
  4 bytes = `NumFires`; `OutlawCampDestroyedTrigger` is empty-shaped);
  (b) `InvasionAction`'s declared 136 bytes are only its invasion-point section,
  the target section following as `{i32 1}{i32 18|19}` — the 19th byte
  (`AreInvasionWarningsEnabled`) is absent in the 18-byte form used by 20 of 76
  instances; (c) `Scenario`'s "30 unknown bytes" are
  `{i32 n}{n zero bytes}{3, 84, 0}` with n = 8/9/14 per map; (d)
  `GoodsAcquiredTrigger`'s `Goods` table was never initialised (NullReference on
  all 18 instances).
- Layout: uncompressed file header → consecutive zlib members (MapHeader, RadarMap,
  S2Game). Stop inflating when next bytes fail the zlib header check (some maps have
  trailing non-zlib data; war_chapter8 +11 KB).
- File header = two option tables, count-prefixed: strings (`author`, `type`:
  war/peace campaign, kingmaker, freebuild, **`historical`** = the 19 `.s2c`
  castle maps), ints (`balanced`, `lastsave`, `mapsize`,
  `maxplayers`, `version`). `mapsize` optional; NOT the serialized grid size (always 256×256).
- Object stream (per segment): skip 8-byte prefix; `{i32 id, i32 typeIndex}`; new
  typeIndex → `{i32 nameLen, ascii name}`; then i32 parentTypeIndex (0 = none;
  == typeIndex ⇒ one optional i32 may precede trailer — the empty-`Walls` edge case).
  Type registry is segment-local. First object in MapHeader parses id+enum twice.
- Markers are SIGNED i32: object trailer `-57681` (AF 1E FF FF), segment end `-8531`
  (AD DE FF FF), nested data payload `-15963` (A5 C1 FF FF). Unsigned compare breaks.
- MapHeader segment: MapHeader/Scenario/Mission/estate markers + actions/triggers —
  ~60 action readers, ~35 trigger readers exist with per-type notes under
  `Notes/FileFormats/S2M/MapHeaderSegment/`; the payload variants the shipped maps
  use (and the four reader fixes for them) are in that folder's
  `Actions and Triggers/VariantPayloads.md`. RadarMap segment: 2 objects —
  `RadarMap` = the game's own minimap, 256×256 BGRA image (i32 pixel-byte
  count then 262,144 B; structures/walls black, wooden walls brown OR black
  OR plain land depending on the map, sea blue; the **outer 1-cell ring is
  painted black where the map is unplayable** — ch1 right column + bottom
  row, ch8 top-right corner rows/cols too; exclude the ring when scoring
  against it) + 12 B tail
  {1, 4, ptr}; `EstateLayer` = 40 B header + 65,536 u8 (ch1: constant 1 —
  not an estate-id partition there). Useful as ground truth for structure
  extraction (walls_parse) — but corroboration only: parts of real walls are
  not inked (see Walls section).

## HeightLayer payload — DONE (read; plane semantics decoded 2026-10-08)

- Handlers: `ObjectReaders/HeightLayerReader.cs`; independent:
  `sh2-port/tools/walls_parse.py` (wall/water/deck extraction from planes).
  Notes: `S2GameSegment/HeightLayer.md`.
- Layout: `{tag=3, headerLength, id, 0, recordCount}` header, where
  **headerLength = 12 + 16·recordCount** and the pre-marker records
  (headerLength − 12 = 16·recordCount bytes, contents UNKNOWN — 4 i32 each
  with byte-swapped-looking u16 pairs; no water/coord correlation found)
  follow the header; `A5 C1 FF FF` marker at 20 + 16·recordCount, then
  `{1, 262148, 65536}`; 65,536 f32 primary (row-major z*256+x);
  `{tag=2, blen}`; blen/(4+65536*16) = 3 corner planes × {i32 cellCount,
  65,536×4 f32 corners}. Rule verified on ALL 67 shipped .s2m/.s2c files
  (32 carry records: ch1 0, Lookout Mountain 1, ch8 6, World_Europe 28,
  Pembroke 20) — per-map table in `manifests/heightlayer_headers.csv`.
  Corner order per cell (probe-verified, HeightLayer.md): slot 0=(x,z+1),
  1=(x+1,z+1), 2=(x,z), 3=(x+1,z); cell order row-major (z*256+x), same as
  the primary plane (walls_parse's radar match + the in-engine 100 % render
  match on ch1 AND ch8 both pin this). The X-major (x*256+z) grid is the
  **Landscape tag-19** feature grid, not these planes.
- Heights −640..10240 (−256 sea shoreline ring, −640 water depression).
  **Units: 1024 raw = 1 cell = 2 m** (see Scene units), so the 3,328-raw stone
  wall raise is 6.5 m and the terrain relief peaks at 20 m.
  **Water cells serialize the BED in plane 0**, not the shore: ch8's river
  and moats read (plane 0, plane 1) = (−640, 0) — bed at the depression,
  surface at the water line — so a bed-to-surface "raise" of +640 is water,
  not a wall.
  **Plane semantics (decoded 2026-10-08; the three planes are the three surfaces
  a unit can stand on, proven cross-segment on ch1 + ch8):**
  - **plane 0** = true ground corners (moat beds hug it; ≈ primary when flat).
    Units on plain terrain store exactly this height.
  - **plane 1** = FIRST SURFACE ABOVE GROUND per cell: **wall top** on wall
    cells (ch1 stone 13,568/13,594 = +3.3 m; ch8 stone 3,328, wooden 2,048) or
    **water surface** on water cells (river/moat at/below ground; the old
    "plane 1 = water" label from The Four Woods is the same field — that map
    just has no walls). Units standing on a wall-walk store exactly plane 1.
  - **plane 2** = SECOND SURFACE = the **structure deck level**, i.e. the
    height a unit stands at when the cell carries a castle structure. Proof is
    a cross-segment join, not correlation: every scenario unit stores its
    standing height as `f32@33` of its payload (x@29, y@33, z@37, raw/1024 =
    cells), and that y equals **plane 2** on structure decks, **plane 1** on
    wall-walks and **plane 0** on terrain — 487 units on ch1+ch8, 483 within
    0.3 m of a plane and none further (tools/deck_analyze.py `unit_crosscheck`;
    e.g. 4 ch8 Archers stand at y=7628.8 on the TowerLookout cell whose
    plane 2 is 7628.8). Values are per-structure constants: `p2 - ground` is
    fixed per building type across BOTH maps (TowerSquare +7168, TowerGreat
    +8832, TowerRoundTall +8110.1, TowerRoundShort +5350.4, MediumGateHouse
    +6144, LargeGateHouse +7168, keep terraces 0/+5324.8/+8806.4/+10444.8 with
    the inner court at −1638.4, village pads 0). Per-cell taxonomy with counts
    (`tools/deck_analyze.py`, `manifests/deck_taxonomy.json`), 100 % of
    plane-2 cells classified — ch1 1,022 / ch8 418 cells:
    `building_platform_elevated | _ground | _lowered` (inside the footprint of
    a building the engine renders as a model — the model IS the geometry, so
    the wall renderer must not add a deck), `unmodelled_deck_elevated`
    (footprint with no model in the engine; genuinely raised decks the wall
    renderer draws), `unmodelled_pad_ground`, `terrace_ground` (ward/battlement
    paving and village pads, plane 2 == ground → nothing to draw),
    `water_submerged_surface` (sea cells, ch8 river), `water_side_degenerate`
    (sub-0.1 m coast values). Most plane-2 cells are flat by design: they mark
    structure surface, not height.

## Walls / castle fortifications — DONE (decoded 2026-10-08; in-engine render 2026-10-08)

- Walls are **not serialized as objects anywhere**: the `Walls` container has
  an empty payload in ALL 48 maps (the known empty-object edge case), there
  are no wall-piece objects, no wall coordinates in any segment (verified by
  exhaustive u16/u32-packed/f32 sweeps), and the game tree has NO live
  wall-piece GR2 models (only `*_destroy` props + assassin-climb anims) —
  the engine extrudes wall geometry procedurally from the HeightLayer
  surface planes (above) + `castle_wall*.dds` textures.
- Handlers: `sh2-port/tools/walls_parse.py` → `artifacts/walls_<map>.json`
  (per-cell {x, z, ground, top, kind, drawn} + radar-overlay preview PNG);
  **in-engine: `TerrainRendering/S2MWallRenderer.cs`** (classifier = exact port
  of walls_parse; extrudes top quads from the surface-plane corners and side
  quads down to plane-0 corners, 4 textured submeshes). Classification: deck
  when plane 2 stands > 0.1 m over its ground; else stone when plane 1 − plane 0
  > 2500 and wooden when > 500, only when plane 1 > 0 (a wall top must stand
  above the water line — ch8's river/moats put the bed at −640 with the surface
  at 0, which used to classify 2,262 water cells as wooden palisade). Flat
  decks (paving/pads, plane 2 == ground) draw nothing.
- **Footprint rule (2026-10-08):** only BuildingLayer instances a *rendered
  placement* claims are skipped (`RenderedBuildingCells` in `S2MWallSettings`;
  a placement cell inside the instance bbox + 1). The window is deliberately
  loose — the placement cell is the object's origin, which lands inside the
  registered footprint cells for most placements and one cell outside for the
  rest. Coverage after the full type extension (all-maps sweep): **ch1 90/90
  instances, ch8 188/206** (recounted 2026-10-08 with the 90/192-placement
  JSONs); the remainder is 1-2 cell bridge/platform props and
  InvasionMarkers-adjacent cells (see Placed buildings).
  Unclaimed instances are still classified as walls/decks so unmapped props
  leave no holes — 1 ch1 cell + 18 ch8 cells, **100 % radar-black**.
- Verified (recounted 2026-10-08 with the extended placements): ch1 919 stone /
  128 wooden / **410** deck (all 410 flat → 1,047 drawn, **0 raised decks**),
  ch8 715 stone / 479 wooden / 151 deck (**all 151 flat → 0 raised decks
  drawn**; the previous 8 raised decks were Keep2's 4 walk cells at
  ground+8243.2 and TowerLookout's 4 at ground+7628.8, now covered by their
  models). Engine counts == walls_parse exactly on both maps; walls-only
  top-down render = **100 % precision / 100 % recall cell-for-cell, zero
  strays** (ch1 1,047/1,047, ch8 1,194/1,194;
  artifacts/sh2_walls_ch1_engine_topdown.png, sh2_walls_ch8_engine_topdown.png).
  **Re-verified after the 2026-10-08 scene-unit rescale** (captures re-rendered
  at 1 cell = 2 m): the same 1,047/1,047 and 1,194/1,194 cells at 100 %
  precision / recall, so the classifier is cell-neutral to the scale change.
  Building coverage measured alongside: ch8's 45 instances with ≥ 16 cells all
  have model pixels above them (94.4 % of their footprint cells covered;
  artifacts/sh2_buildings_ch8_topdown.png).
  **Cross-map in-engine sample (2026-10-08):** Kill Barclay (war)
  377 stone / 508 wooden / 215 deck, all flat → 0 raised; Australia (kingmaker)
  0/0/7 all flat; Coastal County (freebuild) 0/0/4 all flat; war_chapter5
  (incl. the 12x12 ConstructingMonastery) 0 stone / 241 wooden / 144 deck all
  flat — 0 raised decks and 0 DECODE-FAILs on the three sample maps.
- RadarMap scoring (minimap black = stone structures + buildings): exclude
  footprint cells of modelled buildings, sea, and the **outer 1-cell ring**
  (the game paints the unplayable edge black); score stone vs black, report
  wooden inked-ness and the black ink no surface explains. ch1: stone 889/1,016
  (recall 87.5 %, precision 96.7 %), 0 unexplained black. ch8: stone 530/745
  (71.1 % / 74.1 %), 1 unexplained black; **structure recall (all kinds)
  100.0 % ch1 / 99.9 % ch8**; unmodelled-instance cells are 184/184 radar-black.
  ch8's stone residual is radar ink, not classification: 185 stone cells
  (22 compact wall runs at exactly +3,328, ch1's stone height) are not inked
  at all, and only 225 of 480 wooden cells are — treat black-based
  precision/recall as corroboration, not ground truth.
- Wall pieces have NO rotation (per-cell grid) —
  the building rotation-sign question can NOT be pinned against walls;
  pin it against in-game screenshots/gate-orientation instead.
- Wall-related objects that DO exist: `GateTeleport`/`SpiralStairTeleport`/
  `Teleport`/`LevelTeleport` (pathing; positions packed z<<16|x<<8|sub; e.g.
  SpiralStairTeleport cells sit inside tower footprints), `Cluster`
  (wall-walk path steps, packed cell pairs stepping +65,537), gatehouse payload
  `GateHouse` block = count + runtime object-id links (dead pointers in the
  file; only its GateTeleport link resolves).
- Open: UV/tiling calibration of wall textures vs the original game is
  eyeball-unchecked; the 16 B pre-marker HeightLayer records (unknown) may yet
  explain wall/water edge cases on the maps that have up to 34 of them; keep
  blobs are rectangular while keep models are cross/T-shaped, so 1–2 cells at a
  keep blob's outer edge now show terrain where stone placeholders used to be
  (see Placed buildings).

## Landscape payload — PARTIAL (heights/material/tint/flow decoded; most tags unknown)

- Handlers: `ObjectReaders/LandscapeReader.cs`. Notes: `S2GameSegment/LandscapeWater.md`,
  `TerrainTextures.md`.
- Optional 12 B prefix (present if payload[12]==4; absent in war_chapter1), then
  `{i32 tag, i32 len, bytes}` blocks. Grids serialized X-major (`x*256+z`), reader
  transposes to row-major. Decoded tags:
  - **4** f32 height grid; **27** surface material IDs (byte/cell; `0x08`+ = index in
    `terrain/landtex.txt` GROUND1: 0x08 mountain … 0x1F forest_floor; tail 167,936 B
    undecoded, likely blending); **7** flow direction (5-bit, 11.25°/step, visible
    flow OPPOSITE the raw vector); **22** tint (first 262,144 B = RGB per cell, white
    = untinted; rest unknown); **19** feature categories — EXACT match only:
    1 river, 3 sea, 5 pitch/swamp, 7 moat, 0x21 tree-types-1/2, 0x28 pitch trap,
    0x26 building (33/35/37 etc. in campaign = unknown, do NOT bitmask);
  - **30** texture rotation: absent from ALL standard maps (only editor probes) —
    reader treats as optional (was a hard throw).
  - Tags 2/5/16/18/20/25/26/28: UNKNOWN.

## Forest payload (vegetation) — DONE

- Handlers: `ObjectReaders/ForestReader.cs`, `AssetLoading/S2MVegetation*`,
  `ModelRendering/S2MVegetationRenderer.cs`. Notes: `S2GameSegment/Vegetation.md`.
- i32 declaredLength, i32 count, then count × 65-B records:
  `10×f32` (x,y,z raw; yaw°; scaleX/Y; appearanceScale; tint RGB), i32 seed,
  u16 cellX, cellZ, family, variant, 13 B tail (zero in probes).
  +32,772 B trailing block UNKNOWN.
- **x/z are raw at 2048 units per cell** — cells = raw/2048, landing on the
  record's own cellX/cellZ + a 0..1 fraction jittered around the cell centre
  (|raw/2048 − cellX| ≤ 0.75, mean 0.50 on war_chapter1/The Four Woods; mean
  0.48 on Fenis). **raw/1024 doubles every tree's distance from the origin** —
  the engine did exactly that until 2026-10-08, when `ForestInstance.MapX/MapZ`
  became cells and the renderer applies the shared metres-per-cell factor.
- **The record's 2nd float is NOT a height.** Verified against the HeightLayer
  plane-0 corners on all 712 war_chapter1 records: 0 lie inside their cell's
  corner range (3 of the 237 that store exactly 0), 237 store 0 while standing
  on 10–20 m ground, and all 40 records whose cell is sea store the map maximum
  10240. `S2MVegetationRenderer.GetTerrainSurfaceHeight` therefore seats each
  tree on the plane-0 **bilinear corner surface** — the same surface
  `S2MTerrainRenderer` builds its mesh from (post-change check: 535/535 ch1
  trees match their record position to the millimetre, 0 height deviations
  > 1 cm). Still unexplained: what that float means, and whether the ~40
  sea-cell records render in the original game at all (they now stand on the
  seabed surface, which is data-faithful but unverified against the exe).
- Family→model (visually confirmed): 0→forest_tree_1..4, 1→winter_tree_1..4,
  2→tree_1/1b/1c/1d; variant indexes the set (255 = engine-random).

## Scenery containers (NaturalStone/Rocks/Shrubs/Grass/Waves/Bridges/Rubble) — DONE (NaturalStone + Rubble)

- **One singleton object per map holding a record list** (not per-instance map
  objects): `{i32 payload_len - 4}{i32 count}` + `count x <stride>` records;
  empty = `{4, 0}` (8 bytes). Handler: `sh2-port/tools/stage_fields.py`
  (`--scan` per-family survey, `--rocks` per-record table, `--tables` the exe
  model lists, `--emit-stages` writes the render JSON). Numbers:
  `manifests/scenery_containers.csv` + `manifests/natural_stone_variants.csv`.
  Decoded 2026-10-08.

  | type | objects (empty) | stride | records | cell-frame check | selector | exe model list |
  |---|---|---|---|---|---|---|
  | NaturalStone | 67 (26) | 23 | 20,766 | **20,766/20,766** | byte 12: 0..11 | 12 `rock_natural_stone_*` |
  | Rocks | 67 (12) | 31 | 25,525 | 25,229 + 296 off-by-1 | bytes 28/29: 49 combos | 49 (`rock_iron_*` / `rock_flat_*` / `rock_N`) |
  | Shrubs | 67 (29) | 27 | 5,453 | 5,453/5,453 | not pinned | 9 `shrub_1..9` |
  | Grass | 67 (15) | 27 | 37,867 | 37,866 + 1 off-by-1 | not pinned | textures (decals) |
  | Waves | 67 (40) | 24 | 365 | no cell pair in the frame | not pinned | 3 `wave_*.tga` |
  | Bridges | 67 (31) | 26 | 119 | ids only | not pinned | — |
  | Rubble | 67 (**67 = all empty**) | (23?) | 0 | — | — | 12 `ruins_*` |

- **Record frame (verified cell-for-cell — this is what proves the stride):** the
  record opens with the position floats and ends with the cell written twice,
  then a flag byte. NaturalStone (23 B):
  `{f32 x}{f32 z}{f32 rot}{u8 variant}{u8 0}{u8 0}{u8 cellX}{u8 cellZ}{u8 0}
  {u16 cellX}{u16 cellZ}{u8 1|0}` — the u8 cell pair at 15/16 and the u16 pair at
  18/20 are the same cell and equal `trunc(position/1024)` on **all 20,766
  records of all 41 non-empty maps** (Rocks: y sits between x and z, cells as u16
  at 24/26; Shrubs/Grass: cells at 20/22). The 296 Rocks "off-by-one" records are
  the `trunc` vs `round` boundary (the float sits within 1/1000 of a cell edge);
  no record is further off. The flag byte is 1 on 17,036/20,766 rocks (0.82).
- **The variant byte is the staged-model index** (the hovel pattern, one family
  earlier): 0..11 over all 20,766 records, near-uniform
  (1,724/1,791/1,769/1,718/1,695/1,674/1,730/1,748/1,653/1,724/1,793/1,747),
  uncorrelated with position — and the value space *equals the exe table size*
  in both pinned families (12/12 NaturalStone, **49/49 Rocks** — the Rocks
  selector is the byte *pair* 28/29 and its combination counts reproduce the
  table's structure exactly: 28=8 -> 12 variants = the iron rocks, 28=4..7 -> 6
  each = the four flat sizes, 28=0..3 -> 5+3+2+3 = 13 = the individual rocks).
- **Model order:** `Stronghold2.exe` stores each type's models as contiguous
  NUL-terminated names *before* the type-name string, written in **descending**
  index order; the engine's 0-based index is the reverse (the hovel `.bdg` table
  loads hovel1..hovel6 in that reversed order, and the reversal is what yields the
  growth order for hops `stage_1..7` and apple `sapling..apples`). So NaturalStone
  `0..11` = `rock_natural_stone_{A1,A2,B1,B2,C1,C2,D1,D2,E1,E2,F1,F2}` and Rubble
  `0..11` = `ruins_{stone_1x1..stone_8x8, wood_1x1..wood_8x8}`. The models are
  1.41..3.10 m (12 distinct sizes) and sit on the terrain surface (the record
  carries no height — the renderer samples the plane-0 corner surface).
  The third float is the rock's rotation: uniform in ±45° (mean |22.5°|) and
  **uncorrelated with the local terrain slope** (r = +0.05/+0.03/-0.03 on Rocky
  Island / Three Valleys / Arena of Kings) — a random look, not a slope tilt.
- **In-engine verified (2026-10-08):** `stage_fields.py --emit-stages` writes a
  `scenery` array into the map's placements JSON; `BuildingPlacementLoader`
  renders it into an `S2M Scenery` root (own root, excluded from
  ReadPlacementCells like props; models resolve as `meshes/landscape/*.gr2`).
  Rocky Island: **152/152 instances match the manifest within 1 mm** (model,
  cell, terrain-seated y) — capture `artifacts/sh2_rocks_rocky_island_editmode.png`
  (two natural-stone fields, 12 models, visibly mixed sizes).
- **Rubble is runtime-only**: the list is empty on **all 67 shipped maps**, so
  the ruins are written when a building is destroyed. The engine's table names
  give the rule — size class (1x1..8x8 = the destroyed footprint in cells) x
  material (wood/stone): `ruins_wood_8x8` measures 18.4x16.6 m with its origin at
  the min corner = 8x8 cells at 2 m/cell. **The 7 `ruin_0*.bdg`/`.gr2` pairs are
  orphans**: not in the exe's 104-entry `.bdg` table, no `.bdg`/`.uni`/`.txt`/
  `.exe` literal references `ruin_0*` anywhere in the install (only the files
  themselves) — leftovers from an older rubble system, not engine input.

## Crops (AppleTree / Hops / Wheat) — DONE as a NEGATIVE result (no stage field)

- The hovel pattern does **not** extend to the crops: an exhaustive per-byte
  survey of all 1,437 crop objects (1,019 AppleTree + 392 Wheat + 36 Hops over
  all 67 maps) finds **no field with 5 (apple) or 7 (hops) values** — there is no
  per-instance stage selector to find. Handler/evidence:
  `stage_fields.py --crops` -> `manifests/crop_fields.csv`.
- What the objects *do* carry (payload = entity header + 3 nested A5C1FFFF
  blocks, ids 57 `Element` / 58 `ResourceElement` / a third list entry):
  block0 = `{i32 17}{i32 1}{i32 1}{i32 0}{u8 1}{u16 idA}{u16 idB}` where the two
  ids link the crop to its **farm's slot list** — an AppleFarm's last block lists
  its 8 orchard trees' `(idB,idA)` pairs, a HopFarm's lists its 12 tiles'
  (`war_chapter11` 3 farms = 12 pairs each, values 41..52/53..64/15..26 matching
  the tiles' own fields 1:1). So the crops are farm-slot elements, not staged
  plants.
- The one small per-instance field, payload **byte 61**, is the **estate id** —
  the same field every placed object carries (see "Entities/troops" below):
  an AppleTree/AppleFarm's `+61` equals the EstateLayer value at its own cell
  (ch8: 121/121 trees, 7/7 farms; the survey's "per-farm value" reading is
  this id — the farm and its trees genuinely share the estate), while **every
  Wheat/Hop crop stores 0 = no estate**, which is what that survey saw as
  "0 for every crop". It is not a model selector — the crop negative (no
  5-value apple / 7-value hops stage field exists anywhere) stands unchanged.
- The crop look comes from **atlas frames / textures, not geometry**:
  `hops_stage_1..7` are the *same* 16-vertex billboard (1.60x1.55x3.42 m) with 7
  different UV rects; `apple_tree`/`_apples`/`_blossom`/`_bare` share one
  250-vertex mesh (only the texture differs) and `apple_tree_sapling` is a
  separate 8-vertex billboard; `Wheat`'s exe entry is the **texture**
  `meshes/buildings/farm_plants.transparent.dds` with no model list at all. So
  the crop's appearance = (season/frame) x (farm state) — which is why no
  per-instance selector exists in the map data.
- **Engine index order of these tables** (see the Scenery section for the
  rule): AppleTree `0..4` = sapling, bare, blossom, apple_tree, apples; Hops
  `0..6` = stage_1..stage_7; Grapes `0..5` = stage_1..stage_6 (the `Grapes`
  type is not placed in any shipped map).



## Placed buildings (S2Building/Tower/Keep/Hovel… payloads) — DONE (transform + type coverage + Hovel variants)

- **HOVEL VARIANTS DECODED (2026-10-08) — the placed object itself carries the
  variant index.** The Hovel is the game's ONLY type with six `.bdg`s, and the
  map object says which one it uses: the index sits at +20 inside the object's
  THIRD nested `A5C1FFFF` block (= payload byte 248 on the 272-byte payload,
  260 on the 12-byte-longer layout, where the extra bytes are inserted inside
  the object's first block). Value 0..5 → `hovel1..hovel6_closed` in the exe's
  table order (the engine's own building table in `Stronghold2.exe` lists the
  six paths and loads them hovel1..hovel6 into one 6-entry table; the renderer
  indexes it with the object's variant field). Evidence: all 146 placements on
  the 67 maps give values 0..5, near-uniform (25/26/23/29/19/24); uncorrelated
  with position/rotation/id (|r| ≤ 0.05); on the 9 long-layout objects the
  field moves with the +12-byte insertion (both readings agree 146/146); and a
  Play-mode pass on war_chapter1 renders its four hovels as
  hovel4/hovel3/hovel5/hovel6 whose in-engine bounds are 9.66/6.93/4.89/4.79 m
  wide = each variant's own bdg bbox × 1.025 (the art-grid factor) —
  `artifacts/sh2_hovels_ch1_{playmode,topdown}.png`. **No `.bdg` field selects
  the variant** and the maps carry no other variant data. Tooling:
  `building_parse.py --hovels <maps> --csv manifests/hovel_variants.csv` and
  `bdg_fields.py --variants` (per-variant model sizes + the "is there a
  selector in the file?" scan).
- Handlers: raw via `ObjectReaders/RawObjectReader.cs` in-engine; decode pipeline
  `sh2-port/tools/building_parse.py` → `artifacts/buildings_*.json` → rendered by
  `Buildings/BuildingPlacementLoader.cs`. Model authority reader:
  `sh2-port/tools/bdg_info.py`. Notes: `S2GameSegment/Buildings/`.
  **All 67 shipped maps extracted (2026-10-08): 1,852 placements + 1,122 props
  over 94 distinct types; `tools/verify_placements.py --all <maps>` PASSes all
  67 (`manifests/placement_verification.csv`).**
- Payload = nested blocks; final block holds foundation
  corner coords in raw units (/2048 = cells). Transform floats in the first block:
  x@140, y(height)@144, z@148 (world = raw/1024), **rotation@152 = f32 degrees**
  (decoded 2026-10-08; values observed exactly {0,90,180,270} across 100
  buildings on war_chapter1 + war_chapter8; Unity applies as Y Euler).
  **Block schema CORRECTED 2026-10-08:** a nested block is
  `A5 C1 FF FF` + `{i32 classId}` + `[ {i32 nameLen}{name} — written ONLY the
  first time that class is used in the segment ]` + `{i32 A}{i32 B}` + fields
  (`classId` is a segment type-registry index, so it moves between maps —
  `Building`/`S2Building` are 58/59 on war_chapter1, 60/61 on Warwick.s2c; A/B
  are per-class constants: 1/52 for Building, 2/172 for Actor). The old note's
  "first block named 'Building' (not the type digit)" read one half right and
  one half wrong: the string IS "Building", but it appears because that object
  is the **first building of the segment** (the name is 4+8 = the documented
  **12-byte "preamble"** — that variant is now explained, not mysterious), and
  the "type digit" was a misparse of the `{A}{B}` pair (A=1 read as nameLen,
  0x34 = '4' as the name). Measured over all 67 maps: exactly one building per
  map carries the name, always at the first building object in the stream.
  `building_parse.extract_transform`'s detection (rotation plausibility at
  @140, else @152) still finds the shifted layout, so no result changes; the
  full rule + evidence live in `S2GameSegment/Entities/Entity.md` §2.
  Correlation evidence: same-type instances that
  visibly face differently (4 Hovel… all-0° set, 3 SmallGateHouse, gatehouse
  pairs) differ at @152 and nowhere else.
- **Position is stored TWICE** (2026-10-08, all-maps sweep): the entity triple
  x@29, y@33, z@37 (no yaw) holds the same values as the transform block for
  small buildings; for large footprints they are opposite corners of the
  footprint (Keep4 12x18: transform (161,48) vs entity (149,30), delta = the
  footprint dims). The **placement cell = the footprint's MIN corner and the
  model's local origin is its corner** — proven by rendering the monastery
  (`ConstructingMonastery`, blob 12x12 org (73,106)): the rendered mesh bounds
  are centre (85.3, 118.3) size 24.69x24.60 m = position + size/2, i.e. the
  model covers the blob exactly. So the transform block is the right anchor for
  buildings; props (no footprint) use the entity triple (x@29, y@33 = standing
  surface, z@37).
- Extracted: ch1 = 90 placements + 21 props, ch8 = 192 + 28 (JSONs carry
  `{type, model, x, z, y, rot}` and a separate `props` array).
  **Coordinates are CELLS, not metres** (2026-10-08): joining placements
  against BuildingLayer footprint blobs gives exactly one claimed instance per
  placement at ≤ 1.41 cells; the ×2 "metres" reading misses by 20–130 cells.
  So `raw/1024` in the building transform = cell index. **Footprints are now
  exact, not eyeballed** (`.bdg` decode, 2026-10-08, see the `.bdg` section):
  the placement footprint = the bdg's u16[4],[5] cells (= 2.00 m/cell), and the
  placement cell is the footprint corner that carries the model's local origin
  (rotation-dependent). The GR2 meshes are authored on a 205-units-per-cell
  grid, i.e. 2.5% larger than the size the bdg declares, so models overhang
  their blobs by design (tower_square_closed 6.00 m base in a 6 m/3×3 blob,
  keep_small 15.02×18.69 m model on a 14×14 footprint). `BuildingPlacementLoader`
  multiplies x/y/z by `S2MWorldScale.MetresPerCell` into metre world space
  (the y field is cells of height too: raw@144/1024).
- **Type → model coverage DONE (2026-10-08): 89 types** (was 21, then 71). The
  model name comes from the game's own building definition
  `meshes/buildings/<type>.bdg`: its FIRST entry is the default (closed-state)
  model, and that file exists for every mapped type. Read it with
  `sh2-port/tools/bdg_info.py` (entry 0 kept verbatim — beacon.bdg's first entry
  reads `beacon_construction20.gr2` while the shipped file is
  `beacon_construction_20.gr2`; later pool fragments are dropped by file
  existence). State/damage pieces (`keep_*_fl/fr/bl/br` = destroyed-corner
  debris, `_open`, `_construction`, `_collision`, `_table`, `flag_pole`) are not
  separate map objects — the placed object renders the single `_closed` model
  (verified: keep_large_closed's bbox spans the whole footprint incl. the corner
  towers). Type→bdg aliases resolved by the localization table
  `i18n/en-us.xml` `BUILD_*` tags (Solar = "Bedchamber" → `queens_solar_closed`,
  FishPond = "Eel Pond" → `fishpond.gr2`, GongPit → `gong_farm.gr2`,
  TortureRack → `torture_streching`, GardenersHut = "Vegetable Garden").
  **Keep tiers are forced by footprint fit** (blob 2 m/cell vs model bbox):
  Keep1 4×9 cells = 8×18 m vs `keep_wooden_closed` 9.4×20.5 m; Keep2 7×7 = 14×14
  vs `keep_small_closed` 15.4×15.9; Keep3 8×9 = 18×16 vs `keep_medium_closed`
  17.2×19.3; Keep4 12×18 = 24×36 vs `keep_large_closed` 24.9×39.5 (already
  mapped) — each model overhangs its footprint by 1–3.5 m and no other pairing
  fits. Payload sizes back the order (Keep1 389 B < Keep2 437 < Keep3 445 <
  Keep4 453). RadarMap ink does NOT discriminate: every keep's cells are black,
  wooden keeps included. Rendered proof: ch8 Keep1 = the dark wooden palisade
  keep with a staked fence (`artifacts/sh2_buildings_ch8_keep1_village.png`),
  Keep3 = the stone multi-tower castle (`…_castle_keep3.png`), Keep2 +
  TowerLookout at the eastern castle (`…_keep2_lookout.png`).
  **The 18 types the all-maps sweep added (2026-10-08)** — each = bdg entry 0
  plus its blob-vs-model fit: ConstructingMonastery → monastery (12x12 = 24x24 m
  vs 24.69x24.60; proven by rendered bounds, not a capture - the
  `sh2_monastery_warch5.png` this entry used to cite does not exist on disk),
  OutlawCamp → bandit_camp (7x6 = 14x12 vs 15.51x14.03), Fairground →
  travelling_fair (5x3 = 10x6 vs 9.67x6.76), CarterPost → cart_post (2x4 = 4x8
  vs 4.71x8.46), WeaverWorkshop → workshop_weaver (2x3 vs 4.37x6.27), IronMine →
  iron_mine (2x2 vs 5.00x5.36), WaterPot → water_pot (2x2 vs 3.90x3.66),
  SheepFarm → farm_sheep (2x2 vs 4.60x4.57), Brewery → brewery_closed (2x2 vs
  4.70x4.73), ManTrap → man_trap (1x1 vs 2.05x2.05, 271 objects), WarHoundCage →
  wardog_cage (1x1 vs 2.07x2.07), Signpost → signpost, Well → well, Dovecote →
  dovecote (1x1 vs a slim 1.09x1.19x3.49), ConstructingBeacon →
  beacon_construction_20 (name quirk above; 1.87x2.21), Statue → statue_lord
  (exactly 2.00x2.00 m = the 1x1 blob; statue_horse is 2.00x3.39 and fits no
  instance), ConstructingBridge → bridge_end (2x2 vs 4.47x4.61; bridge_wooden is
  a 46 m span and its _construction_02 piece 4.82x12.64 — neither fits a 2x2
  registration).
- **Mapped types that stay deliberately unmapped** (checked against the whole
  game tree 2026-10-08; each owns NO footprint ≥ 16 cells, so no unmodelled
  structure is left on any map — see `manifests/placed_types.csv`):
  `Smelter` (BUILD_SMELTER = "Oil Smelter" — no smelter*/kiln*/furnace* model
  exists in the install and no `*.bdg`; its one Fenis instance is 2x2 cells),
  `WoodenPlatform` (51 objects, 1x1/1x2 blobs, no model name matches),
  `BurningLog` (the 3 rolling logs per tipper, stacked ~0.70 m above the wall
  top — its standing surface is not one of the three planes),
  `Wheat`/`Hops`/`AppleTree` (crops, 0 BuildingLayer cells; see the **Crops**
  section — no per-instance stage field exists, the look is atlas/texture +
  farm state), `InvasionMarkers`
  (one per map, mission markers). `ConstructingBridge` and `Statue` used to be
  on this list — both are mapped above. The **scenery containers**
  (`NaturalStone`/`Rocks`/`Shrubs`/`Grass`/`Rubble`) are not placements either —
  one singleton record list per map, now decoded in the Scenery section.
- **Mounted props (ch1 21 + ch8 28 braziers/log tippers/stone tippers)** own NO
  BuildingLayer instance — they stand ON walls, tower decks and keep terraces,
  and their payload uses the ENTITY layout (x@29, y@33 = the standing surface,
  z@37, raw/1024; x/z at cell centres). Proof: with `tools/verify_placements.py`,
  all 34 braziers + 92 BurningLogTipper + 19 StoneTipper objects land within
  0.3 m of the cell's **plane 1** (wall top) or **plane 2** (tower/keep deck) —
  e.g. ch1 braziers at y=13568 sit on stone-wall cells whose plane 1 is 13568,
  ch8 y=8110.1 is the TowerRoundTall deck offset. Emitted into the JSON's
  `props` array, which the loader renders but `ReadPlacementCells` excludes (the
  wall renderer's model-covered rule is about footprint-owning structures only).
  One documented exception: Koblenz's brazier at (73.5,44.5) stores
  ground+7628.8 (the ch8 TowerLookout deck offset) over flat land with no
  structure nearby — a `PROP_OUTLIERS` entry in `verify_placements.py`.
- `BuildingLayer` object = 40 B header + {108, 0, 131076, 65536} + 65,536
  u16 grid (row-major z*256+x): value = building INSTANCE index (1..91 on
  ch1; each value forms exactly one contiguous footprint blob — towers 3×3/5×5,
  hovels 2×3, braziers/statues 2×2, killing pits 1-cell). Includes props
  (braziers, statues, torture items) beyond the mapped set.
  **Decoded in-engine (2026-10-08):** `ObjectReaders/BuildingLayerReader.cs`
  → `S2MFile.BuildingLayer` (ch1: 90 instances, 1,369 footprint cells);
  consumed by S2MWallRenderer for footprint exclusion.
  **Coverage after the full type extension (2026-10-08, all 67 maps):** ch1
  87/90 instances, ch8 187/206; every instance ≥ 16 cells is claimed on every
  map (`tools/verify_placements.py --all`, `manifests/placement_verification.csv`
  — 1,852 placements, 0 raised plane-2 cells in unclaimed instances anywhere).
  Remaining unclaimed instances are 1-2 cell bridge/platform props and
  `InvasionMarkers`-adjacent cells. **Documented exception class:** 9 of the 12
  ServantsQuarters objects across peace_chapter1, war_chapter7, war_chapter10,
  war_chapter8 and Miskolc-Diosgyor carry a normal payload but have NO
  BuildingLayer registration at all (neither the transform cell nor the entity
  cell is inside an instance or its bbox+1) — an `OUTLIERS` entry in
  verify_placements.py; the other 3 own blobs and pass strictly.
  ch8 in-engine top-down proof (earlier): all 45 instances ≥ 16 cells
  have rendered model pixels above them, 94.4 % of their footprint cells covered.
  Known cosmetic gap: the keep blob is a full rectangle while the keep model is
  a cross/T, so 1–2 cells at the blob's outer edge show terrain instead of
  stone (they used to be stone placeholders). Refine with per-type blob masks or
  model-footprint projection later.
- TODO: rotation sign convention (CW/CCW) — walls are unrotated grid cells
  (Walls section), so pin it against in-game screenshots or gate orientation;
  the scenery containers' *other* selectors (Rocks' 49-class mapping, Shrubs'
  and Grass' bytes) are catalogued in `manifests/scenery_containers.csv` but the
  per-family index→model assignment is only pinned for NaturalStone/Rubble.
- In-engine coverage of the pipeline on the maps the sweep was run against
  (2026-10-08): Kill Barclay (war) 377 placements / 393 rendered models, 377
  stone + 508 wooden + 215 deck cells all flat (0 raised decks drawn), 0
  DECODE-FAILs; Australia (kingmaker) 9 placements, 0 raised; Coastal County
  (freebuild) 3, 0 raised; war_chapter5 (bonus) 5 placements incl. the 12x12
  ConstructingMonastery (144 deck cells, all flat → 0 raised). **The 19 `.s2c`
  castle maps now load too** (2026-10-08):
  Play-mode Fenis.s2c = MapType Historical, 256×256 heightmap + Landscape +
  BuildingLayer + 229 forest trees, walls extruded (72×11.2×95 bounds) and
  **36 building renderers == the 36 placements in `buildings_fenis.json`**,
  0 DECODE-FAILs (also when the file is re-read inside the playing domain);
  the engine's render matches the Python-extracted terrain (84.7 % of the grid
  sits at height 0 with the castle in the low-coordinate corner — checked in
  `terrain.json`, so the "flat plain" in the capture is map data, not a bug).

## Entities/troops (Archer, Oxen, HorseCart, Lord…) — DONE (payload + state field + facing; 2026-10-08, facing 2026-10-09)

- Handlers: `sh2-port/tools/entity_parse.py` (**new 2026-10-08**); raw in-engine
  via `RawObjectReader.cs`. Detail note: `S2GameSegment/Entities/Entity.md`
  (+ per-type `Archer.md` / `HorseCart.md` / `Oxen.md`).
- **The actor payload is decoded** and swept over all 67 maps: `payload = 100 B
  actor header + nested A5C1FFFF class blocks`.
  - Header: `+0` 0, `+4` 59 (placed-object marker), `+8` **the game's own type
    id** (Archer 19, Crossbowman 52, Pikeman 51, Spearman 28, Swordsman 27,
    Maceman 50, Ballista 70, Knight 71, Ladderman 29, HorseCart 2, Oxen 14 …),
    `+12` 2, `+16` world id, `+20` 1, **`+21` the standing-surface class**
    (0 ground/water level, 1 raised wall-walk, 2 structure deck), `+22/+23`
    u8 cell pair + `+24` 0 + `+25/+27` u16 cell pair (the cell written twice —
    the NaturalStone convention), **`+29` x, `+33` y, `+37` z** (the position
    triple; y = the raw height of the surface the unit stands on), `+41` 0,
    `+45/+49` = **HIT POINTS** (one per-(map,type) value written twice — a
    current/max pair; the same pair is written again in the Actor block),
    `+53` 0, `+57` 0,
    `+61` **THE ESTATE ID** (the state field), **`+65` a per-object random**
    (uniform 0..32767 = MSVC `rand()`'s range — NOT a fraction: it exceeds
    10000 on 7,000+ actors), `+67` 10000, `+69` 0, `+71` 25 (three
    constants, identical on all 12,256 actors), **`+75` a per-type u32
    classification** (3 troops & most animals, 1 siege vehicles, 18 Heron,
    28 Dolphin, 173 Ladderman, 179 Engineer, 255 HorseCart), `+83` 1.0,
    `+91` 1, `+92` a per-(map,type) f32 stat (always a 1/150 multiple, 0 on
    all wildlife); the first `A5 C1 FF FF` block marker sits at `+100` on
    **every** actor of every map. **The constants' names are structural
    readings** — the install ships no unit-stat table (the values appear
    nowhere else, not even in the exe), so only the exe phase can name
    them; the pinned value sets per type live in
    `manifests/entity_types.csv` + `Entity.md` §1b. **None of the header's
    constants is the rotation** — `+41` is 0 on all 12,256 actors; the
    facing lives in the Actor block (see below).
  - **ROTATION (decoded 2026-10-09)**: an f32 in **degrees** at
    **Actor-block data+44**, right after a 1.0 scale and after the actor's
    (x, z) position written twice — `{i32 0}{x}{z}{x}{z}{0,0,0}{0,0}{1.0}{ROT}`
    — **written a second time at data+83** (12,256/12,256 agree, `--check`
    asserts it). Same degree convention as the buildings' `rot@152`, so the
    engine applies it as a Y Euler. **Read it BLOCK-RELATIVELY**: the class name
    is emitted only at a class's first use in the segment, shifting the block's
    data start by `4 + nameLen` on 55 of 12,256 actors — a fixed `payload+160`
    read there returns 0 (it silently lost 2 Wolf / 1 Cat / 1 FireBallista
    facings). Value set over all 67 maps: **exactly 180°** on Catapult,
    Trebuchet, Mantlet, BurningCart, FireBallista, Cat; **{0, 90, 180, 270}** on
    HorseCart/CarterCart; packs of 14 share one of 45/100/160/340° on WarHound;
    19 free angles on Wolf; **0 on every troop type in every shipped map**
    (Archer 4,137 … Ballista 31) — the shipped maps rotate only props, carts and
    stationary siege equipment, so a wall garrison inherits the editor's 0°
    (ch1's 82 wall-walk units and ch8's 149 all store 0 — a real property of the
    data, not a decode gap). Evidence it is the yaw: the quarter-turn/free-angle
    split, no other field correlating with it (r > 0.9 scan over every offset of
    all 12,256 payloads), and the geometry — 897 local same-facing clusters show
    37 % columns / 18 % ranks along-vs-across the facing (random ≈ 11 % each),
    Fenis's 9 mantlets at 180° form a rank, and an in-engine rot-0/rot-180 A/B
    on one mantlet model flips its asymmetric +0.41 m mesh offset to −0.41 m.
  - Nested blocks: `A5C1FFFF` + `{i32 classId}` + `[ {i32 nameLen}{name} — only
    at the class's FIRST use in the segment ]` + `{i32 A}{i32 B}` + fields.
    The class id is a **segment type-registry index and therefore differs per
    map** (Actor 43 on 51 maps / 44 on four; Soldier 45 or 46; Archer 41 on
    war_chapter1, 51 on Warwick.s2c) — the **names** are stable, and since the
    first use carries the name a map-local `classId → name` table is rebuilt
    from the payloads (`class_names`). The chain is base-first, the object's
    own class last and unnamed (it was registered by the object itself in the
    top-level stream): `Actor > S2Actor > Soldier > Archer` (12,119 payloads),
    `Actor > S2Actor > SiegeEngine > Ballista`, `Actor > S2Actor > Lord`,
    `Actor > S2Actor > HorseCart/Oxen/CarterCart` (no Soldier level),
    `Building > S2Building > <type>` for buildings. **Actor detection = the
    payload's first nested block is the map's Actor class** — data-driven, no
    type list (`A`/`B` — 2/172 on Actor — are still unverified).
- **STATE FIELD = the estate id (`+61`)**, decoded by cross-segment join
  (`tools/entity_parse.py --estates` → `manifests/estate_join.csv`): the
  `EstateLayer` object is `40 B + 65,536 u8` = one estate id per cell, and over
  all 67 maps **every one of the 1,895 structures/props with a non-zero `+61`
  stands on land whose grid value equals its own id — 0 exceptions**; objects
  with `+61 == 0` are the unowned ones (the wildlife: Rabbit/Deer/WarHound/
  Dolphin/Wolf/Bear/Whale/Heron/Fox … **and the crops**, i.e. the crop survey's
  "payload byte 61 is 0 for every Wheat/Hop" negative is this same field, now
  explained). Units that do not match their cell are standing on someone
  else's land = invaders (8,161 of 11,870 units — the campaign maps are
  invasions; war_chapter9's 217 id-3 units all stand on the constant-1 grid).
  The id is written twice (also inside the S2Actor block, payload +341) and
  clusters spatially (ch8 id 9 → x 53..105, id 7 → x 198..217). The 31
  `S2Estate` objects per map (`+8` = index 0..30) are the matching estate
  records.
- **OWNERSHIP, FACTIONS AND THE ABSENT PLAYER (measured 2026-10-10, the
  R007 data step)**: the maps carry an ESTATE-ownership model and NO
  faction/player model — no attack-order logic can be derived from the shipped
  data. Four measurements:
  1. **No map object names a player.** The 31 `S2Estate` objects per map
     (2,666 B each) differ only at `+8` (the estate index) and three data
     fields (236–238, 420–422, 786) — there is no owner/player field and no
     estate→player table anywhere in the file. The file header's `maxplayers`
     is a count of HUMAN players (ch1 = 1, all 19 `.s2c` siege maps = 0, skirmish
     maps 1–8), not a roster of who is on the map. The only faction relation the
     file holds is the scenario SCRIPT: `SetAlliesAction` (present on 1 of 67
     maps, ch1: a nested 10-class block with a 44-byte data section) and the
     enemy-named triggers (`SpecificEnemyLordDiesTrigger`,
     `EnemyNearMarkerTrigger`, `PercentTroopsKilledTrigger`,
     `KillAllLordsTroopsAction`) — none of which names an actor set.
  2. **The estate id is NOT a faction identity.** On `war_chapter1` the
     player's castle (estate 1: Keep4, Granary, Church, 9 TowerSquares, the whole
     town) is manned by its OWN 161-strong garrison carrying estate 3, and 126 of
     those troops stand ON that castle's wall-walk/structure-deck surfaces
     (`+21` = 1/2: 82 wall-walk, 44 deck) — a garrison, not an army outside the
     walls. So "different estate id = enemy" would brand the chapter's own wall
     garrison as the enemy. The earlier "the invading stacks are the
     actors standing off their own estate's land" reading therefore does NOT hold
     on `war_chapter1`: its `EstateLayer` grid is constant 1 over all 65,536
     cells, so the join degenerates to "+61 != 1" and cannot separate the map's
     actors. (It does separate them where the grid is partitioned, e.g. Kill
     Barclay with 13 estate ids over 1,953 actors, and on the historical maps.)
  3. **On the 19 `.s2c` historical siege maps the split happens to be right**, and
     that is what the "invading stacks" note was really seeing: exactly three
     estate ids among the actors — `{1}` = the castle + its own garrison (on
     Fenis: 208 actors, 137 of them on the walls/decks) and `{5, 6}` = two
     invasion armies (Fenis 315 + 127 actors, 0 of either on a wall-walk or deck,
     encamped at the map's invasion edge, far from the castle). It is a layout
     accident of those maps, not a faction field.
  4. **`InvasionAction` adds no faction.** Its payload is one nested block
     (class 18): a 136-byte invasion-point section (ch1: 3 points, the first at
     x=23 z=30 raw-ish cells) then the `{i32 1}{i32 18|19}` target section. The
     `InvasionMarkers` layer (260 B: `{i32 256}` then 6 f32 positions) is where
     the invasion armies spawn. Nothing carries a faction id.
- **Standing plane re-derived per entity** (independent of the flag): `+33`
  equals the HeightLayer corner plane at the unit's cell (plane 0 terrain —
  sampled bilinearly over the cell's corners — plane 1 wall-walk, plane 2
  structure deck): **12,206/12,256 entities (99.6 %) within 0.3 m**; the 50
  residuals are mid-slope positions 0.3–0.9 m off the sampled surface (historic
  castle maps). The header's own `+21` surface class agrees with that join on
  **12,245/12,256** (the 41 coastal cells where plane 1 holds the water surface
  are mapped to ground on both sides).
- **Numbers (all 67 maps, `entity_parse.py --check` PASSes)**: 12,256 actor
  objects in 47 types; **cell frame 12,256/12,256** (u8 pair == u16 pair ==
  `trunc(position/1024)`); **resolved to {type, x, z, rot, standing plane,
  estate} 12,256/12,256**; **rotation == its second copy at data+83:
  12,256/12,256**. war_chapter1 166 entities in 8 types, war_chapter8 340 in 14
  (the two per-map dumps are `artifacts/entities_war_chapter{1,8}.json`).
  Manifests: `manifests/entity_types.csv` (per type: type id, objects, maps,
  payload lengths, surface classes, estate distribution, standing planes,
  on-own-estate, cell frame, class chain, **hit points / random range /
  +75 classification / +92 stat — the decoded header constants — and the
  per-field check counts**),
  `manifests/entity_estates.csv` (per map, with its rotation column),
  `manifests/estate_join.csv` (the estate evidence per map),
  `manifests/clan_fields.csv` (per clan/cluster/formation object, with the
  BoxFormation join).
- **IN-ENGINE RENDER DONE (2026-10-08)** — the actors
  render statically. `entity_parse.py --json` joins
  `manifests/actor_models.csv` (tools/uni_info.py) so every record carries its
  resolved mesh path (`meshes/units/models/archer.GR2`); the join is in Python
  because the manifest's notes column contains commas and quotes.
  `EntityPlacementLoader.cs` (mirrors `BuildingPlacementLoader`) reads that and
  calls `Granny2AssetConverter.LoadOrConvert` + `Granny2ModelRenderer
  .RenderInstances`, one instance per actor. **Coordinate rule**: x/z are already
  cells → `* S2MWorldScale.MetresPerCell`; `y_raw` is NOT a cell count but raw
  height at 512/metre → `* S2MWorldScale.HeightUnitScale` (the building JSON's
  `y` is raw@144/1024 = cells and scales by MetresPerCell instead — the two
  layouts differ). Unit meshes are Z-up centimetres with the origin at the feet
  and X centred (archer X ±95.6 cm, Z 0..218), so no anchor-corner offset like
  the building footprints. Verified: ch1 **166/166** and ch8 **340/340** placed,
  0 skipped models, positions equal the JSON to ≤1e-4 m (float32), Play-mode
  capture `artifacts/sh2_entities_ch1_playmode.png` (archers on the ch1 castle's
  stone walkway). Meshes cached via `build_mesh_cache.py --all-actors` (46/46).
- **FACING APPLIED (2026-10-09)** — `rot` (Actor block data+44,
  degrees) is in the JSON and applied as a Y Euler in
  `EntityPlacementLoader`. Re-verified in Play mode: ch1 166/166 with positions
  5.2e-5 m off the JSON and rotY exact (165 at 0°, the FireBallista at 180° at
  cell (158.5, 48.5)); Fenis 613/613 with 15 rotated actors (9 mantlets in a
  3×3 rank, 3 trebuchets, 3 fire ballistas at 180°) and the rot-0/rot-180 A/B
  proving the geometry flips; captures
  `artifacts/sh2_entities_fenis_playmode.png` (mantlet rank + formations) and
  the re-rendered ch1 capture, 0 console errors on both.
- **NOT yet done**: no actor *animations* in-engine — the animation data itself
  is **decoded** (2026-10-10) and the interchange decision is **extend `.osh2mesh`** (a v2 skeleton +
  skin-weight section) plus a new `.osh2anim` clip cache — glTF is rejected for
  the reasons in §8.3. What remains is the conversion work: extend `gr2dump.c`
  (skeleton bones + rest transforms, per-vertex bone indices/weights via the
  DLL's `PWNG…` vertex types, per-clip sampled curves), the two cache formats
  and the Unity `SkinnedMeshRenderer`/`AnimationClip` side. Also still open: the
  *names* of the +75 classification and the +92 stat stay unverified because the
  install ships no unit-stat table and the exe no field-name metadata, so only
  the exe phase (§7.5) can name them.
- **CLANS/FORMATIONS DECODED (2026-10-09)** —
  `entity_parse.py --clans MAP` / `--clans-sweep MAPDIR --csv DIR`, evidence
  `Entity.md` §7, manifests `clan_census.csv` + `clan_fields.csv`:
  - CLAN objects (1,143): `TroopClan` 715 + the wildlife `DeerClan` 66 /
    `RabbitClan` 71 / `SeaGullClan` 66 / `WolfClan` 56 / `SheepClan` 4 /
    `CrowClan` 38 / `PheasantClan` 33 + `OutlawCampAggressiveClan` 36 /
    `OutlawCampDefensiveClan` 46 + 12 whose stream type name the walk garbles
    (war_chapter11). One shared payload: header {0, class marker, 0, index,
    +14, +16, cluster count, cluster id #1/#2} + nested blocks (the `S2Clan`
    class block A=0/B=5 with a 5-byte datum, plus a second block A=0 whose B =
    its data length: 11 troops / 28 wildlife / 37-45 outlaw). Detection keys
    on the S2Clan class id — the shape test (A=0, B=5) also matches emitters
    and mounts.
  - CLUSTER objects (1,104): `{declaredLen}{memberCount}{member records}{u32 A}
    {A × 4 B waypoints}{u32 B}{B × 8 B waypoints (each point written twice: x at
    raw/4, then both axes in cells)}{owner clan id}{59 B parameter tail with
    the cluster's own f32 x/z, 256.0 and 1.0}`; 870 end exactly at
    79 + 4m + 4A + 8B bytes, 178 carry a 13 B sentinel waypoint at the map edge
    (x 255, z −1) and 56 one extra 8 B waypoint.
  - **THE MEMBER JOIN**: every actor carries its cluster in its Actor block at
    **data+111** (block-relative) — grouping on it makes each cluster's
    memberCount match on **1,101/1,104** and the owner field resolve to a real
    clan on 1,101/1,104; 3,673 of the 12,256 actors are in a formation.
    `entity_parse.py --json` now emits `cluster` per unit. Documented outliers
    (`CLAN_OUTLIERS`): Kill Barclay (3 clusters + 5 actors pointing at
    InterruptActions — so data+111 is a generic "linked object" pointer),
    Three Valleys (2 orphans with owner 0), war_chapter11 (1 dangling owner id).
  - `ActorGroup` = an 8-byte empty container, exactly 11 per map on all 67.
  - **`BoxFormation` (decoded 2026-10-09):** 18 objects
    over 5 maps (Fenis 1, Javier 5, Windsor 8, hawk rescue 2, Koblenz 2) —
    a 45-byte payload holding ONE nested block of the *shape* class
    (`GridFormation` on all 18; the segment's registry also knows
    `ColumnFormation`) whose 4-byte datum is the **grid width = members per
    row**. A TroopClan payload carries the BoxFormation's object id (payload
    +52/+56 depending on the clan variant, located by value), and the width
    is pinned by the member layout: 18/18 formations fit `width*width` and
    fill a compact ~1-cell block (Fenis 9 members on width 3, Javier 36 on
    width 6, Windsor 11 on width 4, hawk rescue 12 on width 4). Evidence
    `Entity.md` §7.5.
  - STILL UNVERIFIED: the member records' two u16s (engine registry handles —
    the second tracks the actor's 1-based ordinal on ~51 % of clusters but a
    pair→position fit fails outright), the waypoints' fine-copy unit and the
    tail's non-position fields, the S2Clan block's 5-byte datum (1,106
    distinct values over 1,143 clans = a per-object engine handle) and the
    second block's parameter data (a per-type-sized block, mostly zeros:
    19/11 B on troop clans, 16..36 B on the wildlife clans, 19..45 B on the
    outlaw camps).

## .gr2 — Granny 2 model — PARTIAL (static mesh path DONE; skinned skeleton + weights CONVERTED and rendered 2026-10-10; the clip curves still need the sampler)

- Handlers: `Tools/Granny2MeshConverter` (upstream, Windows x86, loads the game's own
  `granny2.dll`); Linux bridge `sh2-port/tools/gr2dump.exe` (mingw32, wine32) → JSON
  → `json_to_osh2mesh.py`. Unity: `AssetLoading/Granny2AssetConverter.cs`.
  **New 2026-10-10: `sh2-port/tools/gr2skel.exe`** — the
  skeleton/animation probe behind everything in §8 of `Entities/Entity.md` — and
  `sh2-port/tools/anim_probe.py` / `animlist_parse.py` for the sweeps.
  Notes: `Notes/FileFormats/GR2/Overview.md` +
  `Notes/FileFormats/S2M/S2GameSegment/Entities/Entity.md` §8.
- Magic `B8 67 B0 CA F8 6D B1 0F 84 72 8C 7E 5E 19 00 1E`, format v6, Oodle
  section compression type 2 — ALWAYS decode through granny2.dll (x86 only), never
  parse raw. Extracts: meshes, PNT332 verts, indices, triangle groups, one texture
  ref per material (embedded Windows paths `Z:\...` → resolve basename beside the GR2).
  Assets are Z-up, centimetres.
- **Model files also carry a skeleton + bone bindings; animation files carry
  neither (and no mesh) — see "Actor animations" below.** The static dumper still
  ignores them (PNT332 carries no bone indices/weights), so buildings/vegetation
  are unaffected. Not yet converted: skin weights, morph targets, UV2+, COLOR0
  (foliage shader gets neutral white vertex color).

  **The skinned half is now converted and rendered (2026-10-10):** `gr2dump.exe`
  emits the skeleton and, for a mesh whose own type definition lists
  BoneWeights + BoneIndices, the per-vertex indices/weights plus the mesh's bone
  bindings. The vertex layout is derived from that type definition, never a
  hard-coded stride — the 8-word records are `{typeId, char* name, ?, int count}`
  where BoneWeights/BoneIndices are 4 **u8** each and every other member is
  `count` f32s (the member-name prefix match must be `strncmp(name,
  "TextureCoordinate", 17)`; 18 misses "TextureCoordinates**0**" and drops 8
  bytes off every stride). An unknown member name ends the parse rather than
  guessing its size. The archer is 40 B/vertex, the Lord's low_lord09 64 B
  (it carries Tangent+Binormal between Normal and TextureCoordinates0).

## Actor animations (meshes/units/animations/ + AnimList.anl + the .uni tails) — DONE (decoded 2026-10-10; **the actor->clip catalogue joined and played 2026-10-10**)

- Handlers: `sh2-port/tools/gr2skel.exe` (the DLL probe), `tools/anim_probe.py`
  (model/clip sweeps → `manifests/actor_rigs.csv` + `anim_clips.csv`),
  `tools/animlist_parse.py` (the `.anl` catalogue →
  `manifests/animlist_anl.csv`, `--resolve` against the real files). Evidence:
  `Notes/FileFormats/S2M/S2GameSegment/Entities/Entity.md` §8 (full byte-level
  detail: struct layouts, strides, the oracle check, all numbers).
- **1,008 unique animation files** under `meshes/units/animations/{soldier,worker,
  hero,teenager,child}/`, named `<profile>_<action>.gr2`; each is one track group
  + one animation with **no mesh and no skeleton**. Sweep over a 75-clip sample:
  durations 0.07–80 s, timesteps 1/60, 1/50 and 1/30, 1–40 transform tracks.
- **`meshes/units/AnimList.anl`** (202,896 B) = the catalogue the engine reads
  (the exe carries the literal `meshes\units\AnimList.anl`): a 20-byte header
  `{4,5,600,24,47}`, then **1,127 fixed 180-byte records** `{animation .gr2 file
  name}{animation name}` + an `0xff` numeric tail, then 16 pad bytes.
  **1,125/1,127 records resolve to a real file**; the 2 that don't are
  front-coded (prefix-compressed) names — completing the prefix resolves 6
  (`hornblower_idle`, `hornblower_walk`, `cs_sword_to_throat_loop`, `hero_walk`,
  `cs_pikeman_idle`, `cs_pikeman_standdown`) and the remaining 2
  (`fanfare.GR2` — stored `anfare.GR2` — and `soldier_taunt_2.GR2`) have no
  shipped file. Only one shipped animation file has no record
  (`soldier_charge.gr2`). **The record's own two names are separated from the
  pool fragments by extension** (corrected 2026-10-10): each
  record is `{0xff lead, 4/8/12/16 B}{file name, NUL-terminated, then pool
  fragments}{animation name, NUL-terminated, then more fragments}{i32 numbers}`,
  so the record's own file name is the **first run ending in ".GR2"** and its
  own animation name the first later run that does **not** (the old
  `strs[0]/strs[1]` read put a fragment into the anim name on 40+ records —
  `PICT IDLE`, `LORD IDLE`, `LC WALK`, `CROSSBOW RUN`, `HA CANTER ATK`,
  `RUN SWORDSMAN` … all live behind a .GR2 run). 6 records are additionally
  front-coded in BOTH names, and the missing character comes from a
  NEIGHBOURING record: `s_sword_to_throat_loop.GR2` sits between `run_burning`
  and `cs_sword_to_throat_loop`, `night_walk`/`anfare.GR2` next to `hero_*` /
  `fill_moat`, and completing with the neighbour's prefix also fixes the anim
  name (`NIGHT WALK` + `K` from `KNIGHT WIELD` -> `KNIGHT WALK`).
- **`AnimList.txt`** (install root, 5,258 B) = the 411-line animation-NAME
  vocabulary (349 distinct), a subset of the catalogue's 942 names. A `.uni` def
  file's tail is that actor's own animation-name list (archer: ARCHER RUN,
  SHOOT, SHOOT END, ATK DAGGER, ARCHER IDLE ATK …) and 10/10 archer names are
  catalogue names. The exe lists NO animation file names, so the engine composes
  `animations\<profile>\<...>` itself.
- **THE ANIMATION-NAME TAIL OF EVERY DEF FILE (2026-10-10)**
  — two different layouts, both detected (stride + first record) by which one
  resolves the most vocabulary names:
  * `.uni` (34 soldier/civilian types): 24-byte records `{i32 tag}{char anim
    name[NUL]}` — 32 B on `horse_archer.uni`. The name field is a string pool
    (the same suffix sharing as the .bdg/.uni model pools), so a raw scan returns
    fragments (`DLE WOOD`, `ACKAWAYBOW`, `WOD TOWE`) and a slot's name can be the
    SECOND NUL-terminated string of the field. Names only: the clip file comes
    from the .anl. The 4-byte tag is NOT an .anl index (the same tag maps to
    unrelated names across files: 25 = ARCHER RUN, CRIM WALK, CS IDLE,
    AXEMAN ATTACK) and stays unidentified.
  * `.aml` (animals): 112-byte entries `{i32 -1}{char clip file[NUL]}` (64 B)
    then `{i32 0}{char anim name[NUL]}` (48 B) — the actor's OWN clips, which
    `AnimList.anl` does **not** catalogue at all (they sit next to the models in
    `meshes/units/animals/`). A bear's `WALK` is `bear_walk.GR2`, not the child
    Bip01's `child_walk.GR2` the shared .anl record would give.
  Completion rule: the stored text is the name missing its leading 1-2
  characters, and the vocabulary (the .anl's animation names + AnimList.txt) is
  the only source of prefixes — with a 3-character minimum so a padding hole
  cannot complete `ALK` to `WALK`. Where the catalogue ships the same name for
  several rigs (`WALK`/`RUN`/`IDLE TAP` exist for the soldier, worker, teenager,
  child and hero Bip01s) the candidate is picked by the profile the type's own
  unambiguous clips already use. Manifests: `actor_animations.csv` (458 rows over
  the 34 types with a def file — the .aml grid fix below added 41 animal rows) and
  `actor_idle_clips.csv` (47 rows, the plain IDLE name per type that resolves);
  `uni_info.py --anims`, `--anims-csv`, `--anims-check`, `--idle-csv`,
  `--states-csv`, and `build_anim_cache.py` for the caches. 13 types (siege
  engines, carts, the cat) have no def file, so no animations; 51 rows name an
  animation with no shipped clip (the animals' gaps
  plus the Lord's `TABLE IDLE`, a vocabulary name with no .anl record).
- **THE `.aml` ENTRY GRID — CORRECTED 2026-10-10.** The
  name-count heuristic that picked the start put it a few bytes off, and the
  clip-file field then read a string-pool fragment as the name: the animals'
  clips resolved to **nothing** (deer_bambi, sheep, rabbit, oxen, heron,
  horse_cart) and the bear lost `ATTACK`. The grid is **tagged**, which is a
  detector, not a guess: 112-byte entries whose tag field reads `{i32 60}` on
  the first entry and `{i32 -1}` on every later one — **unique over all 34
  .aml files** (scanned every 4-byte start of every file). The six short .aml
  (crow/dolphin/eel/rat/seagull/whale, 1,452–2,320 B) have no such run and keep
  the fallback; their field alignment is a documented negative (the Dolphin's
  only clip, `hin_trad_water.GR2`, completes to `dolphin_trad_water.GR2` on disk
  but its entry does not resolve).
- **THE `.aml` CLIP-FILE FIELD IS FRONT-CODED** (same encoding as the .anl's):
  a name whose head an earlier string already carries stores only its tail, and
  the field then starts with an **empty string** (visible in a byte dump: the
  0xff tag, a 0x00, then `olf_run.GR2`). Completion borrows the leading 1–3
  characters from the file's own stem / its neighbouring entries and keeps the
  completion that exists on disk (`hind_legs.GR2` → `rat_hind_legs.GR2`,
  `orse_ha_idle_sneeze.GR2` → `horse_ha_idle_sneeze.GR2`). A fragment whose only
  disk completion is a **different animal's** clip — sheep's `olf_run.GR2`
  wants `wolf_run.GR2`, deer_bambi's `ear_die_2.GR2` wants `bear_die_2.GR2` —
  stays **unresolved**: the pool head that would prove it is not in the file,
  and guessing across animals would put a wolf's clip in a sheep's slot.
- **A tagged `.aml` ENTRY NAMES ITS OWN ANIMATION.** The .anl vocabulary is
  only a subset of the animal name space — `PROWL`, `JUMP`, `DANCE`, `SLEEP 2`,
  the landing/diving clips are animal-only — so gating the .aml names on it
  dropped `ATTACK`/`PROWL`/`JUMP`/`DANCE` rows the .aml names AND ships. The
  `.uni` path keeps the vocabulary gate (its names must resolve in the .anl).
- **23 OF THE 293 CATALOGUE CLIPS ARE 1-TRACK PROP ANIMATIONS** (2026-10-10): their single track is a weapon or prop name — `sword02` (8
  soldier attack/idle clips), `horse_LC`, `Object01/02`, `Dummy01/02`,
  `Cylinder26`, `GeoSphere01` — **not a Bip01 bone**. They are the
  attached-object animations (the sword's own swing, the horse's head, the oil
  pot); joined to a skinned actor they bind nothing. `run_swordsman.GR2` is
  confirmed broken by a second, independent read: `gr2dump.exe` reports **0
  skeletons and 0 meshes** for it. Two of them were picked for a STATE, so a
  pick must drive the type's OWN bones: `build_anim_cache.py --tracks-csv`
  writes `manifests/anim_clip_tracks.csv` (one row per extracted clip with its
  track names) and `uni_info.py` `state_clip(rows, state, bones)` skips a pick
  whose tracks do not bind (≥3 shared names and ≥half the clip's tracks; a real
  clip shares 20–30 because a clip rig is a superset of any model's).
  Result: the LightCavalry's walk falls `lc_walk.GR2` → `lc_walk_attack.GR2`,
  and Swordsman/Maceman/ArmedPeasant/WarriorMonk get **no run clip** rather than
  one that animates nothing — their own lists name no other run.
- **PER-STATE CLIP SELECTION (2026-10-10).** `uni_info.py
  --states-csv` → `manifests/actor_state_clips.csv`: 188 rows (47 types × 4
  states), **103 with a clip, 82 distinct clip files, all on disk**, 0 that fail
  the rig check. The state→name rule is a TOKEN match over the type's own list
  (`STATE_TOKENS` = idle `IDLE`, walk `WALK`, run `RUN`, attack `ATK`/`AT1`/`ATT`
  — ATK has three spellings because the catalogue truncates names), in two
  passes: the **plain state name** first (the .anl's shared `IDLE`/`WALK`/`RUN`,
  which every soldier type spells qualified — `ARCHER IDLE`, `MACEMAN IDLE` —
  and so falls to pass 2), then the first qualified name that mentions the token.
  The plain-name pass matters because the lists are the actor's own animation
  SET, not an ordered state machine: `hunters_dog.aml` lists `IDLE LAY` before
  `IDLE`, and a dog standing around would lie DOWN in the file-order pick.
  7 of 47 idle picks move (all from the .aml grid fix; the engineer's
  `IDLE LOOK 2` → `IDLE`, and the dog keeps `hunters_dog_idle`).
  **NOT a per-actor state.** A saved .s2m writes every actor at rest — all
  troops store rot 0 and the payload has no per-actor state field. The header's
  +61 estate id, which an earlier plan called "the state field", is
  the **owning estate's** id (war_chapter1 = {1:4, 3:161, 9:1} — land ownership,
  decoded by the EstateLayer join; war_chapter9's 217 invaders with id 3 all
  stand on the constant-1 grid). So the catalogue says which clip each state
  PLAYS and the engine drives the state at runtime: `entity_parse.py --json`
  writes a `state_clips` array (one flat row per type/state) beside the actors,
  Unity's `ActorStateTable`/`S2MActorAnimation.AttachState` re-clips an actor
  on a state change (a cut, not a blend — the legacy `Animation` has no blend
  tree and the cache carries no cross-fade data), and every actor loads in
  Idle. Verified in batchmode on ch1/ch8/fenis: ch1 166/166, ch8 340/340, fenis
  613/613, all 31 clip-owning types animate, 95 ch1 actors walk / 142 run / 162
  attack on the clip their own catalogue row names, and the types with a gap
  (archer has no WALK; the 4 RUN-SWORDSMAN types no run; Ballista/FireBallista
  have no clip at all) keep the clip they had.
- **The join is by bone NAME.** A model file holds the skeleton (`archer.GR2`:
  "Bip01", 23 bones, 21 bone bindings; `granny_skeleton {Name, BoneCount, Bones*}`,
  bones = an inline array of 152-byte structs); a clip holds 30 tracks for the
  same rig (fingers/nubs included) — so a clip's track set is a *superset* of the
  model's bone set. Rig families: every humanoid is a `Bip01` biped (23–38 bones
  by type; the Lord's own mesh is bound to 1 bone and its weapons are separate
  attached meshes), animals have their own root (`BEAR` 24, `rabbit bip` 33,
  `FOX` 38, `stag` 24, Wolf 46), siege engines/carts are rigid 1-bone boxes.
- **Interchange decision: extend the `.osh2mesh` cache, not glTF.** Clips are a
  shared library keyed by bone name, so a
  per-model glTF would duplicate ~1,000 clips and glTF needs an importer
  dependency; the `.osh2mesh` path is proven for all 46 actor models. Clips get a
  sibling `.osh2anim` cache; skin weights come from the DLL's `PWNG…` vertex
  types + `GrannyCopyMeshVertices`; curves are read via `GrannyCurveGetKnotCount` /
  `GrannyEvaluateCurveAtT` (they are `DaK32fC32f` — never hand-decoded) and the
  rest pose via `GrannyBuildRestWorldPose`.

  **Corrections to §8.2 established while converting it (2026-10-10):**
  - Per-vertex bone indices/weights do **not** need a `PWNG…` destination type.
    They are read straight out of `PrimaryVertexData->Vertices` at the offsets
    the file's own type definition gives, and the DLL's `PWNG*` conversions leave
    the weight slots uninitialised on this build (`tools/gr2vert.c` is the probe
    that established this). A bone binding is a 36-B record `{char* BoneName;
    real32 OBBMin[3]; real32 OBBMax[3]; 0; 0}`, and the vertex bone indices index
    THAT array (verified: max index 20 < 21 bindings on the archer, every
    vertex's weights summing to 1.0 across all 47 models).
  - The rest/bind pose does **not** need `GrannyBuildRestWorldPose`: the bone's
    inverse-WORLD 4x4 is already in the file at bone-struct words 19..34, and it
    equals the inverse of the world matrix computed from the local transforms
    along the parent chain (worst 7e-5 cm on the archer). It is written to the
    cache as the bindpose.
  - A TRACK RECORD is 7 words with the three curve objects at **+4, +12, +20**
    (8 B each), not at the interleaved +2/+4/+6 the raw words suggest. Pinned by
    `GrannyGetTrackSamplerFor`'s own `lea 0x4(%edi)` / `lea 0xc(%edi)` /
    `add $0x14,%edi`. With that, `GrannyCurveGetDegree/GetDimension/
    GetKnotCount` work: archer_idle = Duration 18.04 s, TimeStep 0.02 s, track
    "Bip01" position knots=200 (dim 3) / orientation knots=514 (dim 4), track
    "Bip01 Head" position knots=1.
  - **`GrannyEvaluateCurveAtT@36` — DO NOT GO BACK TO IT (2026-10-10).** The earlier note here pointed at the wrong address. The export
    really is at **0x50050740** (and `GrannyEvaluateCurveAtKnotIndex@40` at
    0x50050780); both are pure 1:1 forwarders into an internal core at
    **0x50017d20**, which a whole-`.text` disassembly shows is called from
    nowhere but those two exports. The core `sub $0x128,%esp` and reads stack
    args up to **arg17**, i.e. it is a *multi-curve batch* evaluator
    (curve count + array + scratch), not a one-curve evaluate. With the curve at
    arg4, arg1 = float count, arg7 = a knot index and arg9 = an output buffer,
    the call writes exactly arg1 floats to arg9 — but the values it produces
    drift freely (up to 139 cm on a root that should hold near rest) and match
    the raw controls under **no** consistent index offset. So the signature is
    not pin-able without the SDK headers. `tools/gr2dumpcurve.exe` prints the
    raw arrays beside the DLL's evaluated values — that is the evidence.
    **The keyframes do not need it:** see the `.osh2anim` section below.
  - **The curve data header, and what the stored values ARE (2026-10-11):** curve object = 8-byte slot `{type descriptor*, CurveHeader*}` at
    record+4 / +12 / +20, where a NULL header means that channel is the identity
    for the whole track (scaleShear on every probed actor clip; orientation on the
    nub tracks). The slot's first word is the pointer to the type descriptor —
    on this build `0x5005fca0`, whose own name field reads
    `CurveDataHeader_DaK32fC32f` (read straight out of the DLL's .data). The
    header is `{u32 fmt (byte 0 = 1 = DaK32fC32f, byte 1 = DEGREE), u32 knotCount,
    f32* knotTimes, u32 controlFloatCount, f32* controlValues}` with
    `controlFloatCount == knotCount * dim` on every probed clip: ONE value vector
    per knot and **no tangents**. `GrannyCurveGetDegree` (the DLL's own oracle)
    reads **degree 2 (quadratic) on every multi-key channel** of every clip
    checked (archer_idle, soldier_walk_weapon_long, wolf_walk, rabbit_hop);
    a 1-knot channel reports degree 0 (a constant) and scaleShear is always the
    identity (`dim 0`), so over the 82 state clips there are 449 position (dim 3)
    and 1664 orientation (dim 4) curves and **zero scaleShear curves**
    (`manifests/anim_curve_layout.csv`).
  - **THE VALUES ARE SPLINE CONTROL POINTS, NOT KEYFRAMES — the curve does not
    pass through them.** Measured with the DLL's own exported degree-2 kernels
    (`GrannySampleBSpline2x3@16` = impl 0x50016470, `2x4@16` = 0x50016520,
    `2x9@16`; args are `(knotPtr, controlPtr, float t, float *out)` and the
    kernel blends THREE consecutive control vectors with weights that sum to 1):
    at a knot time the evaluation returns the MIDPOINT of the two neighbouring
    controls, e.g. archer_idle's root position at knot 2 gives
    `0.5*(3.1828,-0.59656) = 1.29312` on x, and the same relation holds for
    orientation on three rig families. The DLL's own locator (0x50017810) hands
    the kernel `(knots+i, ctrl+i*dim)` with the SAME index i, and for a time
    inside a segment only one anchor keeps the kernel's parameter
    `u=(t-knots[i-1])/(knots[i]-knots[i-1])` inside [0,1] — the first knot >= t —
    which is the rule `tools/gr2sample.exe` uses. Adjacent anchors give the
    IDENTICAL value at knot times (0 deg difference, all clips tested) and differ
    only inside a segment (that is why the anchor rule matters at all).
  - **The knot times are artist times, not the declared timestep**: over the 82
    state clips the median fraction of consecutive gaps that equal the clip's own
    timestep is **0.000** (max 0.933) — the curves are non-uniform keyframe-time
    data (`manifests/anim_curve_layout.csv` has per-clip/per-channel min/median/max
    spacing). Two channels are exceptions to the degree-2 rule: archer_idle's and
    soldier_walk_weapon_long's `Bip01 Spine1` POSITION curves carry only 3 knots,
    below the kernel's 4-knot window; they oscillate by ~0.01 cm and the dump
    falls back to linear interpolation for them (counted in its stderr summary).
  - **What is still open (do not overclaim either way):** the exact internal
    calling convention of the game's OWN track sampler (`GrannySampleTrackUUULocal@16`
    could not be driven from outside — its 4th/context argument stayed opaque and
    the output froze at every time-slot candidate), so the exported kernels are
    the oracle, not the internal sampler; and the DLL's end-of-curve rule outside
    `[knots[1], knots[n-2]]` (the kernel has no in-range anchor there) — the dump
    marks those samples `interior: 0` and the bake keeps the file's own first/last
    control values as the clip's boundary keys instead.

- **Locomotion vs idle, measured on the clip data (2026-10-10)** — two facts that decide how a MOVING actor must be built, both
  computed off every clip's own keyframes over the 103-clip state catalogue
  (`manifests/actor_state_clips.csv`; the metric is
  `S2MActorClipMetrics.RootDriftPerLoop` / `LegAngularSpeedPerSecond`). The
  catalogue-wide figures below were measured with an ad-hoc Python pass over the
  cached `.osh2anim` files (not kept - the per-clip numbers are now reproducible
  in-engine through `S2MActorClipMetrics` + `DevHarness/S2MActorStateRun`):
  - **No clip carries root motion.** The skeleton root's NET drift over one loop
    is 0.000–0.045 m for the walk/run clips and 0.000–0.052 m for the idles
    (archer_idle 0.000 over 18.04 s, archer_run_with_bow 0.045 over 1.00 s,
    soldier_walk_weapon_long 0.008 over 1.00 s, crossbow_idle 0.052 over 12 s).
    So the actor's travel through the world is the GAME's, not the clip's: a
    driver translates the transform while an in-place locomotion cycle plays.
    (The root does PATH-travel a little — the walk clips' roots sweep
    0.24–0.35 m per 1.00 s loop as the hips bob — but that returns to its own
    start, so it is not translation.)
  - **A walk clip swings legs; an idle does not.** Mean angle per second swept
    by the leg bones (Thigh/Calf/Foot, the tokens every rig family shares —
    humanoid `Bip01 L Calf`, animal `WOLF L Calf`): the 31 walk clips read
    **48–204 deg/s** (humanoid soldier walks 76–95, hero_walk 130, wolf 204,
    rabbit hop 597) and the 31 idles **6–22 deg/s** (the highest being
    `soldier_idle_crouch` at 22). Two documented exceptions: the horse archer's
    `ha_walk` reads 15 (the rider's legs stay still while the horse mesh moves)
    and the rabbit's `rabbit_hop` has no leg swing at all (it reads 597 as a
    whole-body hop). Walk/run durations are short cycles (0.43–3.20 s) against
    the idles' 9–29 s.
  - **Consequence for the engine:** `S2MActorStateDriver` translates, clips once
    per state change, and never re-clips per frame; a state whose type has no
    clip (the archer's list names no WALK) still moves and keeps the clip it
    had, with the reason recorded.
  - **The unit meshes' world bounds (measured in-engine via Renderer.bounds,
    2026-10-10):** the Pikeman reads 2.15 x 2.20 x 1.07 m (x/y/z,
    unit meshes put their origin at the feet with x centred, so ~1.1 m deep and
    2.2 m tall with its weapon) and its rect narrows to 1.28 x 2.20 x 2.14 once
    the actor is turned to face a new heading. Useful because the projected-
    bounds pick (S2MActorCommander) sizes its click rectangle from exactly this
    number: at the camera's ~43 m a unit is a ~40x50 px rectangle, and on a
    crowded map the 13 of 166 war_chapter1 actors whose own centre is covered by
    a NEARER actor's rectangle are won by that actor - which is the pick's
    documented nearest-depth rule, not a defect.

## .osh2mesh — engine mesh cache — DONE (v1 static; **v2 skinned, read and rendered 2026-10-10**)

- Handlers: writer `Tools/Granny2MeshConverter` + `sh2-port/tools/json_to_osh2mesh.py`;
  reader `AssetLoading/Granny2MeshCacheReader.cs`.
- "OSH2GR2" v1, .NET LEB128 strings, `SourcePath` = real GR2 path (drives texture
  resolution). Name `{basename}-{sha256(relpath.lower())[0:16]}.osh2mesh` (16 hex
  chars). Cache dir: `persistentDataPath/OpenSH2/Cache/Meshes`. Mtime gotcha:
  keep cache NEWER than sources; on Linux never let the converter
  path be reached (it's a Windows exe).
- **v2 (2026-10-10):** written whenever the model carries a
  skeleton, so the static buildings/vegetation caches are unchanged. Layout after
  the v1 header: `int32 skeletonCount` {per skeleton: `string name`,
  `int32 boneCount`, per bone `{string name, int32 parent, f32 translation[3],
  f32 rotation[4], f32 scaleShear[9], f32 bindpose[16]}`}} then per mesh the v1
  body plus `int32 boneBindingCount` {per binding `{string boneName, f32
  obbMin[3], f32 obbMax[3]}`}, `int32 hasSkinData` and, if 1, `{int32
  vertexBoneIndices[vc*4], f32 vertexBoneWeights[vc*4]}`. No trailing data.
  The reader accepts v1 and v2 and rejects a v2 mesh whose vertex bone index is
  outside its own bindings.
- **The vertex bone indices address the mesh's own BONE BINDINGS** (the file's
  alphabetical list), never the skeleton directly — each binding NAMES a skeleton
  bone, which is the join. The Unity side remaps both to the skeleton's order so
  `Mesh.bindposes`, `Mesh.boneWeights` and `SkinnedMeshRenderer.bones` all agree.
- Coordinates: the bindpose stores the bone's **inverse WORLD matrix in bind
  pose, COLUMN-major** (Unity's `Matrix4x4` ctor takes *columns*). Under the
  Z-up-cm → Y-up-Unity map `A = unitScale · sigma` (sigma = (x,y,z)→(x,z,y)):
  `bindpose_unity = A · bindpose_granny · A⁻¹`, whose linear part is just the
  sigma swap and whose **translation column picks up unitScale** (both endpoints
  are already in Unity units, so the linear part does not). The bone's Unity
  local transform is `A · localTransform · A⁻¹`: position `(x,z,y) · unitScale`,
  rotation quaternion `(qx, qz, qy, -qw)` — sigma is a *reflection* (det −1), so
  sigma·R·sigma is the rotation of the quaternion with its VECTOR part negated —
  and diagonal scale-shear `(sx, sz, sy)`. Meshes with no per-vertex data (the
  Lord's weapon meshes, the 1-bone siege boxes) get weight 1 on their single
  binding instead of being dropped.

## .osh2anim — engine animation cache — **READ, WRITTEN AND PLAYED IN-ENGINE (2026-10-10)**

- Writer: `sh2-port/tools/anim_to_cache.py` (from `gr2clip.exe` JSON).
  Reader: `AssetLoading/Granny2AnimCacheReader.cs`, path resolution in
  `AssetLoading/Granny2AnimLoader.cs`, curve building in
  `ModelRendering/Granny2AnimationClip.cs`, playback in
  `ModelRendering/S2MActorAnimation.cs`. Harnesses:
  `DevHarness/S2MActorAnimationRun.cs` (one archer, one clip) and
  `DevHarness/S2MActorEntityRun.cs` (a full entity map).
  **ONE CACHE, ONE BUILT CLIP (2026-10-12):** a decoded
  `.osh2anim` was always loaded once per clip path (`ActorStateTable`'s cache),
  but the built `AnimationClip` used to be built once per ACTOR — 328 of them
  on war_chapter8, 6073 MB of native memory for clips whose data is ~18 MB a
  piece. `ModelRendering/S2MActorClipCache.cs` is a component on the entity
  loader's group root that builds each clip file once and hands the SAME
  `AnimationClip` to every actor under it; the curves are addressed by bone
  PATH relative to the `Animation` component's own GameObject, and every actor
  of a type has the same hierarchy, so one clip asset plays on all of them with
  its own per-actor phase (`AnimationState.time`). The same change also shares the
  meshes/materials/textures per model
  (`SkinnedGranny2Renderer.SkinnedGranny2SharedModel`); the numbers are in
  `sh2-port/artifacts/sh2_entity_memory_*`.
- "OSH2AN" magic, int32 version (1), .NET LEB128 strings (the same convention
  `json_to_osh2mesh.py` uses), all little-endian:
  `string clipName`, `string sourcePath` (the game-relative .gr2 path),
  `f32 duration`, `f32 timestep`, `int32 trackCount`, then per track
  `{string boneName, int32 positionKeyCount, per key {f32 time, f32 x, f32 y, f32 z},
    int32 orientationKeyCount, per key {f32 time, f32 x, f32 y, f32 z, f32 w},
    int32 scaleShearKeyCount, per key {f32 time, f32 m00..m22}}`.
  No trailing data. Round-tripped exactly through an independent Python
  re-reader: 25 clips / 696 tracks / 75,933 keyframes, every byte consumed.
- **THE PER-KEY LAYOUT, and a writer bug it exposed (2026-10-10):** the channels are INTERLEAVED per key — `{time, values}` — as this
  section and the writer's own docstring always said. `anim_to_cache.py`'s
  first implementation wrote all the knot times first and all the value vectors
  after them, and nothing caught it because the round-trip re-reader was
  written against the writer rather than against the spec. The C# reader
  followed the documented layout and immediately failed with "keys out of
  order" (it was reading value floats as times). The writer was fixed to match
  the documented form and the caches were regenerated; the re-reader now reads
  per-key. Any re-reader written from the code instead of the doc will disagree.
- Naming rule (same as `.osh2mesh`): `{basename}-{sha256(relpath.lower())[0:16]}.osh2anim`,
  16 hex chars, in `persistentDataPath/OpenSH2/Cache/Anims`. `anim_to_cache.py`
  takes `--relative <game-relative .gr2 path>` (plus optional `--name`,
  `--cache-dir`) to apply the rule; with no `--relative` it writes the literal
  path it was given, which is only right for ad-hoc round-trip checks. The
  hash is over the GAME-RELATIVE path, never an absolute one.
- There is deliberately no converter fallback in the Unity loader. The mesh
  path can call a Windows .NET converter; the clips come from `gr2clip.exe`, a
  wine32 build that only runs on the Linux extraction host, so a missing cache
  throws a message naming the exact command to run.
- The join to a model is BY BONE NAME, but not as a bare name: Unity's legacy
  Animation addresses a bone by its PATH from the Animation component's
  GameObject, so `Granny2AnimationClip.Build` walks each bone's parents and
  emits `Bip01 Pelvis/Bip01 Spine/.../Bip01 L Calf`. A clip-only track
  (fingers, nubs) simply has no bone on this skeleton and is dropped: the
  archer binds 23 of `archer_idle`'s 30 tracks.
- **A channel with NO keys emits NO curve, which leaves the bone's REST value
  in place — it is not forced to identity.** The `.gr2` writes a NULL curve
  there, and the difference is real: archer_idle's `Bip01 Neck` carries one
  position key and no orientation curve, and its rest pitch is ~11 deg
  (quaternion (0, 0, 0.0988, 0.9951)). Forcing identity would snap the neck
  upright for the whole idle. (The "0-count = identity" phrasing in the spec
  describes the granny curve being NULL; it does not dictate that Unity must
  WRITE an identity curve.) All 23 archer bones have position keys, so no bone
  ever snaps to the origin.
- **Values stay in the granny2 convention** (centimetres, quaternion x,y,z,w).
  The Unity side applies the same `Swap`/unitScale map the rest pose uses
  (`SkinnedGranny2Renderer.BuildSkeleton`): position `(x, z, y) · unitScale`,
  rotation `(qx, qz, qy, -qw)`, diagonal scale-shear `(sx, sz, sy)`. The
  `.osh2mesh` v2 caches are the reference for what a rest-pose bone looks
  like after that map.
- **The keyframe-vs-spline decision is SETTLED (2026-10-11) — the cache now carries the SOURCE SPLINE's samples, not the raw
  controls.** The curves are degree-2 splines over control points (see "Actor
  animations"): Unity's legacy clip interpolates between whatever values it is
  given, so a cache of raw controls made the engine play a different curve, and
  the difference is not subtle — measured in-engine on the driver path
  (`DevHarness/S2MActorInterpRun`, reference = the game's own granny2.dll
  through `tools/gr2sample.exe --cand X`, sampled BETWEEN the cache keys):
  max rotation deviation **5.40 deg (archer idle), 9.75 (pikeman walk),
  38.28 (wolf walk), 46.25 (rabbit hop)**, max position 6.5 cm, family walk
  46.2 deg — every clip above the 0.5 deg / 5 mm visible bar
  (`manifests/anim_interp_deviation.csv`, pre-fix column).
  The bake: `build_anim_cache.py --manifest manifests/actor_state_clips.csv
  --resample` runs `gr2sample.exe --cand X` per clip and
  `anim_to_cache.py --resample <dump>` writes the cache from it. Keys = the
  DLL's samples inside `(knots[1], knots[n-2])` plus the file's own first/last
  control values as boundary keys; the bake step is `1/300` s (`--bake-step`),
  because the source is quadratic per segment and the interpolation error falls
  as the square of the key spacing: at the clip's own timestep the same
  measurement read 0.29 / 1.86 / 10.82 / 19.18 deg, at 1/300 s it reads
  **0.000 / 0.000 / 0.000 / 0.186 deg** (worst sample sets: the reference grid
  is deliberately NOT a multiple of the bake grid, or 16.7 ms would land exactly
  on a key and the comparison would pass by construction). All 82 state clips
  are baked (103 cache rows, 0 failures).
- Orientation quaternions in the cache are NOT exactly unit (up to 0.07 off) —
  as raw controls they are spline control points (the bake's samples are much
  closer to unit but still not normalized). The Unity side normalizes each key
  individually when it builds the curves, or Unity's lerp of the four
  quaternion components would shorten the bone between keys.
- **In-engine status (2026-10-10):** ONE `AnimationClip`
  per cache, `legacy = true` (set BEFORE `SetCurve`, not after),
  `wrapMode = Loop`, `frameRate = 1/timestep`, one curve per component on
  `m_LocalPosition.{x,y,z}` / `m_LocalRotation.{x,y,z,w}` /
  `m_LocalScale.{x,y,z}`. `AnimationClip.SetCurve` is present in the PLAYER
  runtime assemblies too (checked in `LinuxStandaloneSupport/Variations/mono`
  and `WebGLSupport/Variations/il2cpp` `UnityEngine.AnimationModule.dll`), so
  this is not an editor-only path.
- **The actor -> clip catalogue is BUILT and per-state (2026-10-10):** `manifests/actor_state_clips.csv` (188 rows: 47 types ×
  4 states, 103 with a clip) joined into every entity JSON as a `state_clips`
  array, read by `ModelRendering/ActorStateTable.cs`. Clips are loaded once per
  clip path and shared by the type's actors; `S2MActorAnimation.AttachState`
  re-clips on a state change (a cut, not a blend) and `StateName` reports the
  PLAYING state via `Animation.IsPlaying` — the first state in the enumerator is
  the first one added, and a switched actor holds several. Every actor loads in
  Idle because a saved map writes them at rest; 82 distinct clip files are
  cached. Verification lives in `S2MActorEntityRun.RunStates` + its 5 negative
  controls (ch1 166/166, ch8 340/340, fenis 613/613, 95 walk / 142 run /
  162 attack on ch1).

## .dds — textures — DONE (top mip only)

- Handler: `ModelRendering/DdsTextureLoader.cs` (DXT1/DXT3/DXT5 + BGRA32 decode,
  first mip; files contain mips which are ignored).
- Terrain textures under `terrain/` (material ID table above), model textures beside
  GR2s. Naming convention: `.alphatest` suffix = genuine cutout mask; other DDS alpha
  is NOT a mask (light/spec data) — render opaque (bug fixed 2026-10-08).

## .tga — UI textures — PARTIAL

- Handler: `TGALoader/TGALoader.cs`; runtime UI per README. Alignment/scaling WIP.

## .bik — Bink video — DONE (transcode pipeline)

- Handlers: `Video/Converter.cs` (native ffmpeg on Linux), `VideoFilePaths.cs`,
  `Video/VideoManager.cs`, `Caching/CacheManager.cs` (the transcode on first
  use), `UI/VideoPlayerUI.cs` (playback; `Video/VideoManager.cs` has the
  click-to-skip).
- **A cached video's file name comes from ONE lookup**:
  `VideoFilePaths.GetCachedVideoOutputFormat(path)` — WebM for EVERY video on
  Linux (the Linux VideoPlayer rejects H.264 MP4: "format 0 not supported"), the
  per-file table only on Windows. The writer (`CacheManager`) and every reader
  (`GetCachedVideoPath`) must both use it: until 2026-10-12 the writer used the
  dictionary's own value instead (MP4 for the intro/menu videos), so it cached
  `.mp4` files while every player asked for `.webm`, and the intro videos were
  silently "Video file not found" behind it. Which videos: the
  frontend ones (`ui/frontend/{firefly_logo,Stronghold2 FMV,background}.bik` and
  the two curtains — the flow's intro and menu) and the water surfaces
  (`terrain/river/{sea,river_full_,moat}.bik`; **VP8 alpha renders as a red
  filter on Unity Linux, so the water encodes are opaque**).
  Static marsh uses `terrain/marsh.DDS`, no video.
- **Transcode cost, measured 2026-10-12:** the port's own VP8 settings
  (`-deadline good -cpu-used 0 -crf 10`, chosen for the alpha water) run at
  **~0.046x realtime** — the 125 s intro FMV alone is ~45 min of CPU. A boot on
  a cold cache therefore sits in LOADING_ASSETS for minutes, which is exactly
  what PL_LOADING is the music for; the flow UAT pre-caches the long videos with
  a faster (`deadline realtime -cpu-used 8`) encode and leaves one short video
  to convert in-run.
- **Playback, measured IN PLAY MODE 2026-10-12:** the
  non-looping player's **`loopPointReached` → `onFinished` → `Destroy` path
  works on Linux** — this is `VideoPlayerUI`'s only "finished" mechanism and
  both users of it (`VideoManager.PlayFullscreenBinkVideo` for the intro chain,
  `MainMenuVideoUiManager` for the curtain) are wired to it. The curtain's
  hand-off was the first completion in the port that nothing SKIPS (the intro
  videos are skipped by a click / by the flow UAT's auto-skip), and it fired
  naturally: `curtain_animation` (ui/frontend, 340 frames at 380x768) played to
  frame 338+ and its player was destroyed, then `curtain_idle` took over with
  `isLooping=True` and its clock wrapped 239 -> 0 at 7.967 s (report:
  `sh2-port/artifacts/sh2_game_flow_report_curtain_2026-10-12.txt`).
- **Checking a video from a harness — the player's own numbers and their
  traps:** drive checks off `frame`/`time`/`frameCount`, never off `isPlaying`
  (measured False on Linux while frames advance). A player that is
  still PREPARING reports **`frame == -1` and `frameCount == 0`** — a check that
  latches its "first frame" at first sighting records -1/0 and poisons its own
  numbers, so latch the first non-negative frame and update the count while it
  is 0. A looping player is proven by its clock coming back DOWN (frame or
  time), not by the `isLooping` property, which is only the setting.
- **Destroying a player that is still PREPARING wedges the video module
  (measured 2026-10-12).** `VideoPlayerUI.Stop` destroys the
  GameObject, and when that lands between `Prepare()` and `prepareCompleted` the
  NEXT players' `Play()` calls do not start for **~85 s**: the flow UAT run that
  did it (its auto-skip fired before the second intro video had started) sat
  with the menu's two players at `frame 0/340` and `frame 0/289` from t=37 s to
  t=123 s and then failed its curtain checks on the old 90 s budget, while the
  four runs whose second intro had started before the skip began those players
  immediately. It is a RUNNER-caused state, not a game bug - a player who
  watches the intro never destroys a preparing player - so the flow UAT now
  skips only a video that has STARTED (`SkipStartedIntroVideo`, judged on the
  report's own "Trying?" / "Video playback started." lines) and its curtain
  budget is 240 s, so the same 85 s can never be misread as a failed hand-off.
- **The intro clips' own budget (2026-10-13):**
  `Main.introVideoSeconds` = how long each intro clip plays before the port skips
  it through the very call a left click makes (`VideoManager.SkipCurrentIntroVideo`).
  **Default 1 s per clip**, and the three settings are: `0` = the `INTRO_VIDEOS`
  state is not entered at all (the flow logs `LOADING_ASSETS -> MAIN_MENU` and no
  clip is played), a positive number = that many seconds per clip, a negative
  number = no limit (the shipped behaviour: each clip plays to its end, a click
  still skips). `/tmp/sh2-intro.txt` overrides the field per run — line 1 =
  `off`, `full`, or a number of seconds. **The timer starts from the moment the
  PLAYER reports the clip started (`VideoPlayerUI.HasStarted`, its own `started`
  event), never from when the clip was asked to play** — the ~85 s wedge above is
  exactly what a mid-prepare stop costs, so a clip that is still preparing is left
  alone (the flow UAT's 12 s auto-skip stays as the backstop for one that never
  starts). Measured on war_chapter4's boot: both clips prepared and started, the
  port skipped each after 1.0 s, and the flow left `INTRO_VIDEOS` after **2.2 s**
  of the runner's 12 s window — so the video path stays verified (2 "Video
  playback started." lines) without the 130 s of playback.

## Audio (fx/) — DONE (2026-10-11): shipped files + the game's own two catalogues

**No decoding is needed: the install ships playable audio and its own event→file
mapping.** Everything below is read from the shipped data; nothing is guessed
except the two port choices marked as such.

**The audio tree (5,703 files under `fx/`, measured).**

| Location | Audio | What it is |
|---|---|---|
| `fx/*.wav` | 1,017 | effects, named per event (`sword_hvy_01.wav`, `staff_01.wav`) |
| `fx/fx2/` | 510 | more effects — what a sheet's `fx2\...` path names |
| `fx/deathfx/` | 277 | death/injury cries (`dth_083.wav`) |
| `fx/Environmentals/` | 12 | ambience loops (`ocean_01.wav`, `birdsbusy_01.wav`) |
| `fx/campaign sfx/` | 14 | campaign stingers |
| `fx/military speech/` | 1,193 | unit voices, `Arch_*`, `Peasant_*`, … |
| `fx/speech/` | 1,845 | narrative/system speech |
| `fx/villager speech/` | 504 · `fx/peasant speech/` 69 · `fx/tutorial speech/` 86 · `fx/multiplayer speech/` 113 | the other voice banks |
| `fx/music/` | 59 | music, plus `sh2_PlayLists.csv` + `Music volume levels.txt` — see the **Music** section below |

**WAV effects are mostly ADPCM, not PCM** (measured over all 1,816): **1,711 are
IMA ADPCM (RIFF format tag 17)** with `blockAlign` 2,048 or 512, and only 105 are
PCM (tag 1). Sample rates: **22,050 Hz on 1,798** and 44,100 on 18 — a mixed set
(11 mono ADPCM, 6 stereo ADPCM, 1 mono PCM). Channels: **1,573 mono / 243
stereo**, so mono dominates in both codecs. Unity's decoder reads both codecs
correctly (verified against each file's own header, see below) — the port
therefore needs no converter.

**MP3 speech is uniform** (measured on the files the run plays): MPEG-1 layer III,
**128 kbps CBR, 44.1 kHz, mono**, with a 1,024-byte ID3v2 tag in front and no
ID3v1 trailer; frame walking the bitstream gives the exact sample count.

**`fx/sh2_sounds.csv` — 457 effect ids** (`FX_*`). Header:
`Sound ID,Volume,Delay (1/40th secs),Random,Comment,Variants (Unlimited?)`.
Volume is 0–100; **`Delay` is in 1/40 s ticks** (a real field — `FX_ARMOURER_B15`
carries 880 = 22 s); **`Random=yes`** on the combat/variation ids means "pick a
variant per play". `Comment` is free text ("test", unit names) and is the only
column with prose. Variants are paths relative to `fx/`, Windows-spelled.

**`fx/sh2_speech.csv` — 704 speech rows** (`FXT_*`), 702 distinct ids — the sheet
**repeats `FXT_HONOUR_NEEDED` and `FXT_CARTERPOST_15`, with different variant
lists**, so a reader must merge or keep both rows or it loses variants (the
runtime reader merges; the python tool keeps both rows in the manifest). Its third
column is headed **`unused` and is empty on every row**; `Volume` is 100
everywhere; `random`/`comment` are empty on all but the header. 8 rows are
prefixed **`XXX`** (`XXXFXT_POPULARITY_FALLING` …) — disabled ids, still carrying
resolvable files; and `FXT_ASSASSIN_DARING` is listed with **no variants at all**.

**Resolution: the sheets do not name the files consistently, and four rules are
needed** (`sh2-port/tools/audio_catalogue.py`, mirrored by `S2MAudioCatalogue.cs`).
Of **4,060 variant cells, 3,991 resolve**:

| Rule | Cells | Why it is needed |
|---|---|---|
| sheet spelling | 2,229 + 10 | the path is already right |
| extension retry (`stem+.wav`/`.mp3`, also when an extension is present) | 393 + 1,269 | 400 sounds cells carry none (`axebasic_01`), one is misspelled `blank.wavv`, one doubled (`Engineer_Line.wav.wav`), and **the speech sheet writes `.wav` on 1,399 of its 1,401 cells while the install ships that speech as `.mp3`** — the speech extension is simply stale |
| case-blind retry | 72 (mirror) | no case convention exists (`sheathout_02.wav` vs `Arch_m1.wav`); **the port reads a symlink mirror whose lowercase aliases make it case-insensitive, so against the RAW install 60 resolved cells need a case correction** (`arm_hit_01.wav` is shipped as `Arm_hit_01.wav`). The manifest's `n_case_raw` column is that count; the runtime reader never notices because the mirror answers both spellings |
| zero-padding retry (digit runs re-read without leading zeros; unique match in the sheet's own directory only) | 3 + 15 | `deathfx\dth_0083` is shipped as `dth_083.wav` |

**69 cells do not resolve**, and both the manifest and the run name them:
**28 have a same-named file in another directory** — the 12 `Environmentals`
(this is why `FX_ENVIRONMENT_*` "misses"), `injury_070..073` (shipped in `fx2/`),
and the 16 `miltary speech\...` cells (a **typo for the shipped `military
speech`**); **41 exist nowhere in the install** — `sword_med_01b..16b` (the "2"
variant set of `FX_COMBAT_SWORDMEDIUM2`), `Arch_Attack_Pitch1..6`,
`Knight_Ladder3`, `Outlaw MCover1..4`, `Engineer_Line.wav.wav`, `blank.wavv`,
`dth_174`, `temp_01.wav`. **26 ids have no resolvable variant at all** — a loader
can only answer those with silence, and the manifest says so per id
(`n_resolved` 0) instead of hiding it.

**Unit voices: the sheet's 20 stems are not the roster's type names.** Each voice
comes as a `SELECT`/`MOVE` pair (`FXT_ARCHER_SELECT`, `FXT_PIKEMEN_MOVE`, …) — the
two verbs a player's click grammar actually issues — and the alias table
(`Audio/S2MActorSpeech.cs`) covers **18 of the 47 roster types**:
Archer→ARCHER, ArmedPeasant→PEASANT, AxeThrower→AXETHROWER, Berserker→BERSERKER,
BoatWarrior→BOAT, Crossbowman→CROSSBOWMEN, Engineer→ENGINEER,
HorseArcher→**HORSEARCHER** (words swapped), Knight→KNIGHT, Ladderman→**LADDER**,
LightCavalry→**CAVALRY**, Maceman→MACEMEN, Monk→MONK, Outlaw→OUTLAW,
Pikeman→PIKEMEN, Spearman→SPEARMEN, Swordsman→SWORDSMEN,
WarriorMonk→WARRIOR_MONK. **`FXT_THIEF_*` and `FXT_ASSASSIN_*` have no roster
type at all** (the 47 shipped types hold no Thief and no Assassin): those 4 ids
resolve but are unreachable from map data. Everything else on the roster — siege
engines, carts, animals — has no voice in the sheet, and the commander says so by
name rather than playing a neighbour's.

**Runtime read path** (`Audio/`), all verified in batchmode by `S2MAudioRun`:
`S2MAudioCatalogue` reads both sheets and resolves/caches clips
(`UnityWebRequestMultimedia`; the loaded clip's own name is empty, so the loader
names it after the file), `S2MAudioFileHeader` reads the file's own bytes as the
independent oracle, and `S2MActorCommander.EnableActorSpeech` wires the two verbs
(opt-in: with no catalogue the commander is byte-for-byte the verified old one).
Measured: PCM clip 64,720 frames == the header's 64,720 exactly; ADPCM 9,281 vs
9,282 frames (tail block); **the mp3 decodes to 41,472 samples where the bitstream
holds 39,168 — exactly 2 MPEG frames, Unity's own priming/padding**, so the clip
check carries a 3-frame tolerance. `AudioSource.Play()` reports `isPlaying` true
and `AudioSettings.dspTime` advances (the audio thread runs with no device),
**but `AudioSource.time` stays 0: `-executeMethod` runs with no player loop, so
nothing updates it** — the harness prints that instead of asserting it.

**Port choices (not facts from the data, and marked as such in code):** a squad
order plays ONE voice (the first selected actor's) because the shipped rule for
several units at once is not decoded; and a non-`Random` multi-variant id always
takes its FIRST variant, since the sheet does not say what the other variants are
for.

**Still open:** the `Delay` column is parsed but nothing schedules on it; the
effects catalogue's event ids are not yet wired to game events (only the two
unit-voice verbs are); `FX_*` ambience needs the map's terrain/water zones to
trigger. (The music side of the audio phase is read and played — see the
**Music** section below.)

## Music (fx/music/) — DONE (2026-10-12): the play lists, the volume list, and an in-engine player

**The game ships its own play lists, and they are a SEQUENCE, not an id → file
table.** `fx/music/` holds **59 audio files** — all `.mp3`, MPEG-1 layer III,
**192 kbps CBR, 44.1 kHz stereo** (so the music bank differs from the speech
bank's 128 kbps mono) — plus two data files that name them:

| File | What it is |
|---|---|
| `sh2_PlayLists.csv` | 478 rows under the header `Play List ID,Filename / *silence() / *end()`. A play list opens on a row with an id (`PL_DEFAULT`, `PL_MAINMENU`, `PL_CS_WAR_M15_01`, …) and CONTINUES on rows whose first cell is empty, one item per row, in playback order. Cells hold no commas and no quotes, so a plain split is exact. |
| `Music volume levels.txt` | 54 rows `Name,Volume`, CRLF, **the last row has no newline**, names are the files' STEMS ("The Smith", "the lamb_lp", "Fiddle solo"). |

**The sheet as measured:** **90 play lists, 478 items = 223 track cells + 165
`*silence(N)` + 90 `*end()`**. Every play list has **exactly one `*end()`, always
the last item**, so running off the end of a list is a parse failure and not a
normal end. **128 of the track cells are spelled `.wav` and 95 `.mp3` — and not
one of the 59 shipped files is a `.wav`**: the extension retry the event sheets
needed for speech is this sheet's *normal* case, not an exception. The silences
have **32 distinct second values**; `*silence(10000)` appears **51 times** — the
one-shot idiom that parks a stinger as the last thing a list plays (the largest
real gap is 360 s) — and two cells carry a leading zero (`*silence(06)`,
`*silence(08)`, i.e. 6 s and 8 s).

**Resolution: the four rules of the event sheets, plus two habits only this
sheet has.** The event reader's rules (the sheet's own spelling → the extension
retried → case-blind → zero-padding on a unique match) are **imported from
`audio_catalogue.py` by `audio_music.py` and shared at runtime through
`S2MPathResolver`**, so the two readers cannot drift. On top of them:
**SEPARATOR** (spaces and underscores are interchangeable — `Fiddle_solo.wav` is
the shipped `fiddle solo.mp3`, and the volume list spells it "Fiddle solo", 4
cells) and **SPELLING** (one one-letter misspelling — `Honourmix_01.wav` is the
shipped `honormix_01.mp3`; the sheet's *own other cell* for that track,
`PL_CS_WAR_M22_02`, spells it the u-less way the file does, 5 cells). Both are
uniqueness-guarded like the padding rule and counted separately. **220 of the 223
track cells resolve: exact 95, extension 62, case 54, separator 4, spelling 5**
(`manifests/audio_music.csv`, one row per ITEM in playback order, 478 rows).
**The 3 misses are two spellings, both named**: `null.wav` (PL_NULL — the sheet's
own null play list) and `Chantloop1.wav` (named by `PL_MED_POP_EARLY` and
`PL_LARGE_POP_EARLY` **and given a volume row**, shipped nowhere).
**Against the raw install, 0 of the 220 are case corrections** (`n_case_raw`):
unlike the event sheets, the music sheet's spelling is off as often against the
disk as against the mirror, so the case rule is doing real work here (the shipped
files are largely lowercase — `harpy_01.mp3`, `castlejam.mp3` — while the sheet
writes `Harpy_01.wav`, `Castlejam.wav`).

**The volume list is on a DIFFERENT SCALE from the event sheets.** Values are
**100 (most rows), 110, 115, 85 and 127** — and **127 on the two loudest rows
(The Smith_war, Victory is Ours) reads as full scale**, so the runtime gain is
**`volume / 127`** (a 100 row plays at 0.787, an 85 row at 0.669). That mapping
is **the port's reading of an undecoded engine, marked as such in code**: the
file's numbers are the data, the curve is not (a `volume/100` reading would clip
127). **222 of the 223 track cells join a volume row** (the join folds case,
separators and the one-edit spelling — the volume list itself spells a track
`Honourmix_01` where the sheet's other cell spells it the way the file does);
**4 rows no play list names** — `bigwin3`, `Smallwin1`, `Stinger_lose_01`,
`Victory is Ours` — the stingers the game's own events play rather than a list.

**Runtime read path** (`Assets/Code/Stronghold2/Audio/`): `S2MMusicCatalogue`
reads both files and resolves every track (mirroring `audio_music.py` rule for
rule), `S2MMusicPlayer` plays a list in order on an `AudioSource` — a track for
the CLIP's own length, its `*silence(N)` gap as real time, `*end()` stopping it
with the reason recorded — `S2MPathResolver` now holds the shared rules for both
sheet readers, and `S2MAudioClip` is the single decode path all three catalogues
use (`S2MAudioCatalogue` keeps its public surface and its UAT still passes
unchanged). **The player's clock is explicit (`Tick(delta)`), which is a measured
decision, not a style**: in batchmode `AudioSource.time` stays 0.0000 forever
(no player loop), so nothing about the source can report that a track ended —
Play mode feeds `Tick` from `Update`, the harness feeds it by hand, one code path
both ways. A track that resolves to no file is **skipped and named, never guessed
at**, and the list carries on.

**The UAT** (`S2MMusicRun`, batchmode, compute-only — **`-nographics` is fine
here**, it clicks nothing) **PASSes 0 failures**: the runtime reader and the
python manifest agree on all **478 items** (kind, file, resolved path, silence
seconds, volume — 0 mismatches); **PL_CS_WAR_M15_01 plays on the WALL CLOCK** —
`Stinger_drama_01.mp3` for its own 14.497 s, then the sheet's six-second gap
(measured 6.002 s), then `Stinger_danger_01.mp3` for 7.915 s, each transition
timed in real seconds and the source asked which clip it is on while it plays;
the `*silence(10000)` sentinel is then FED (waiting 2 h 46 m is not a test), and
the list reaches `*end()` and names it; an id that does not exist plays nothing
and names itself; **PL_NULL plays nothing and names `null.wav`**;
`Chantloop1.wav` is skipped mid-list in `PL_MED_POP_EARLY` while its six
resolvable neighbours play; and the gains reach the source (85/127, 100/127).

**One measured decoder fact the length checks rest on:** Unity's
mp3 decoder reports MORE samples than a file's own bitstream by **≈2 frames plus
≈0.47 % of its frames** — fitted across 8 files spanning **4.7 s to 309 s**
(+3 frames on the 4.7 s file, +58 on the 309 s one, worst length ratio 1.0165).
The **header walk is the side that agrees with the files**: its length matches
the declared 192 kbps CBR byte count to 5 ms, and `ffprobe` agrees with it, so
the excess is the decoder's. The checks therefore bound that model
(`3 frames + 0.8 %`), not one frame count. This generalises the Audio section's
older "exactly 2 extra frames" note: that file was 0.9 s and fits the same model.

**Still open:** the engine's real volume curve (the file's numbers are read; the
linear `/127` map is the port's); which of the game's own events starts each of
the 90 play lists — only `PL_LOADING` and `PL_MAINMENU` have a moment in the
port's game flow, and **that flow is still bypassed** (`Main.Update` returns), so
the two calls are in place but unexercised while the player itself is what the
UAT verifies; the 62 `PL_CS_*` chapter lists, the peace/war/economy lists and the
fighting mixes are read and resolvable but nothing triggers them; and the exact
role of `*silence(10000)` beyond "a hold" (a hold is what the port implements).

## .aic / .bdg / .uni — .bdg DONE, .uni DONE (model catalogue), .aic unparsed

- `.aic` = `castles/*.aic` AI castle layouts — still unparsed.
  FireFly's official "SH2 AI Village Editor.doc" (in `others/`) documents the
  `.aic` editing workflow only (no binary layout) — checked 2026-10-08.
- **`.uni` (`meshes/units/<type>.uni`) + `.aml` (`meshes/units/animals/<type>.aml`)
  = the unit's model definition — CATALOGUED (2026-10-08).**
  Reader: `tools/uni_info.py`, table: `manifests/actor_models.csv` (47/47 entity
  types → 46 distinct models). **Entry 0 is the actor's model** (offset 8; offset 4
  on the crow/dolphin/rat/seagull `.aml`, which carry a different prologue); the
  names after it are attachments/tools, and the pool's shared-suffix fragments
  resolve by file existence — the same rule as a `.bdg`. The 13 types with no def
  file (siege engines, carts) get their models from `Stronghold2.exe`'s per-type
  literal block ("string adjacency": the record's model list, then its `.uni`/
  `.aml`, then the type name, emitted together in `.rdata`), and each exe-only
  model must be referenced from `.text` (`--check` verifies both directions; a
  deliberately corrupted attestation was tested and does fire). Naming does not
  always fold out of the type name: AxeThrower→`axeman.uni`,
  BoatWarrior→`pictish_warrior.uni` (entry 0 `pict.GR2`),
  Monk→`fighting_monk.uni`, Deer→`deer_bambi.aml`, Oxen→`oxen_stone.aml`,
  Engineer→`engineer_oil.uni`. Siege engines: the engine's own hull is
  `<stem>.gr2` else `<stem>_body.gr2`; an `engineer_<engine>.uni` inside an
  engine's block is the CREW's def, never the engine's model. Ballista and
  FireBallista share one mesh set (identical literals pool once, in the
  FireBallista slot). Models dumped via `dump_models.py --list
  artifacts/actor_models_types.txt` (46 unique, 3 s) and cached as `.osh2mesh`
  with `build_mesh_cache.py --all-actors`.
- **`.bdg` (`meshes/buildings/<type>.bdg`) = the game's own building definition —
  numeric fields DECODED (2026-10-08).** Reader: `tools/bdg_fields.py`
  (**121/121 files parse, 0 failures**); numbers: `manifests/bdg_fields.csv`
  (per file) + `manifests/bdg_footprint_check.csv` (per file joined with every
  map's BuildingLayer instances). Fields, with the independent checks:

  | offset | field | verified against |
  |---|---|---|
  | 0x00 u16[0] | format id: 13 (117 files), 6 (siege_camp*/siege_tent), 7 (catapult) | — |
  | 0x08 u16[4],[5] | **placement footprint in CELLS** | every BuildingLayer instance bbox on all 67 maps (up to 90° rotation); the manifest's max_instance_cells is an upper bound (instances merge with walls) and equal on the clean types (keep_small 49, keep_large 216, monastery 144, hovel 6, farm_dairy 6, statue 1) |
  | 0x10 u16[8],[9] | the same footprint in 1/256-cell fixed point | == 256×u16[4],[5] on 96/121 files (the exceptions are the big models, see unknown fields) |
  | 0x20.. | model-name list, entry 0 = default/closed model, file order | `bdg_info.py` (string pool stores shared suffixes → fragments dropped by file existence) |
  | model block | `02 00 00 00` + 2 small i16 + **6 i16 = default model's bounding box** (lo_x,hi_x, lo_z,hi_z, lo_y,hi_y) in **centimetres**, low corner = the model's local origin | dumped GR2 meshes ×200/205: median abs error 8.5 cm over 93 types |
  | after bbox | the **anchor section** (decoded below): `{3 bytes flags}{f32 x,z,y}`… — the `{u8 1}{u16 1}{f32 x,y,z}` block seen on 18 files (keeps, apothecary, inn, workshops…) is just its first point with flags `01 00 01` | the point sits on/in the model; flags `00 00 00` mean "no anchor" |
  | +15/+27/+36 | `{u16 p,q,r}` (180,100,100 on most files) + `{u16 n}` = number of anchor records, then n × 14 B `{f32 x,z,y}{u16 v}` + a closing u16 | structural: ends exactly at EOF on 78/121 files (`bdg_fields.anchors()`). The per-record `u16 v` (2026-10-08): 99..800 over 199 records in 71 files, **mode 200 = 65/199**, mixed *within* files (no file has a single v) and only weakly correlated with the point's distance from the model anchor (r = +0.40). In bdg length units (200/cm per cell) that is 0.5..4 cells with 1 cell dominant — reads as a per-point radius/clearance, the same unit reading the neighbouring `{u16 p,q,r}` = (180,100,100) supports (0.9/0.5/0.5 cells); UNVERIFIED (no independent check available) |
  | per-entry | 4–8 bytes of numbers before each name (e.g. `{i32 6}{i32 3}` before tower_square_hoardings, `{f32 10.0}{i32 1\|3}` before the corner/candle/table pieces) | UNKNOWN |

  **Units (independent check of the scene convention):** the bbox is in true
  centimetres *at 1 cell = 2.00 m* — the 1-cell props carry exactly 200 units
  (torture_*), 2-cell killing_pits 391 (~400), 12-cell monastery 2411 (~2400),
  i.e. **200 bdg units = 1 cell**. The GR2 meshes are authored on a **205
  units-per-cell** grid (torture 205.0, killing_pits 410.2, tower_square base
  615, gatehouse_small 615), so `bdg = mesh × 200/205 = 0.97561` — measured
  median 0.97562 on 192 samples, i.e. an exact unit conversion. Consequences:
  the art is drawn 2.5% larger than the size the bdg declares (models overhang
  their footprints by design — keeps by 1–3.5 m), and the "1 cell = 2 m" reading
  is the one that makes the bdg numbers round in cm (the alternative, mesh unit
  = 1 cm ⇒ cell = 2.05 m, would need the bdg unit to be 1.025 cm).
  **Placement anchor rule (decoded):** the stored placement cell is the
  footprint corner that carries the model's local origin, rotated with the
  building — rot 0 → min corner, 90 → (min x, far z), 180 → far corner,
  270 → (far x, min z). Verified on every map: 1789/1838 placements own
  *exactly* that rectangle (97.3%), 35 own a superset (blob = footprint + annex
  cells: Kitchen 12, ServantsQuarters 12, SawPit 8, …), 6 a clipped subset
  (StockPile ×5, EngineersGuild ×1), 8 none (the ServantsQuarters
  no-registration class). `tools/bdg_fields.py expected_footprint()` +
  `verify_placements.py`'s A2 check implement this, **replacing the old loose
  "placement cell inside instance bbox+1" window**.
  The 6 representative types (`manifests/bdg_footprint_check.csv`):
  keep_small 7×7 = 14×14 m (max inst 49 = exact; model bbox 15.02×18.69 m — the
  cross-shaped keep overhangs), tower_square 3×3 = 6×6 m (116 exact placements;
  model 6.89 m wide with hoardings), gatehouse_small 3×3 = 6×6 m (model
  6.00×6.67 m), hovel1 2×3 = 4×6 m (146 exact), farm_dairy 3×2 = 6×4 m
  (model 6.00×3.70 m), tower_lookout 2×2 = 4×4 m (the wall-mounted lookout;
  model 4.47×17.02 m — a 17 m tower on a 4 m base).
  Still UNKNOWN: header u16[2],[3],[6],[7],[10..15] (u16[15] is 0..16 and does
  NOT track the entry count or the mesh count; the "big model" files carry
  smaller u16[8],[9] than 256×footprint, e.g. inn 512 vs 1280 — a candidate
  "model's own cell footprint"), the per-entry numbers, the keep extra bbox,
  the property/pricing tail blocks, and the `_open`/`_construction` stage
  *selector* (beacon lists ONE name for its 20 construction stages, so the
  stage count comes from elsewhere; the engine derives `<model>_construction*`
  by name — `hovel_small_construction.gr2` exists but is not listed in
  hovel_small.bdg).
- **Multi-model families and the hovel1..6 variant — RESOLVED 2026-10-08.**
  A `.bdg` never selects among siblings: **hovel1 and hovel2 are byte-identical
  outside their model name, their own model bbox and the anchor points**
  (`bdg_fields.py --variants` re-verifies this: of all byte-level fields whose
  values differ across the six, everything sits inside the model block
  (bbox + anchors) or inside the model-name strings — 0 elsewhere). The game's
  own building table is in `Stronghold2.exe` as 104 literal
  `meshes\buildings\*.bdg` paths; the Hovel is the only type with several, and
  the exe loads hovel1..hovel6 into a 6-entry table in that order. **The
  variant therefore lives in the placed object** — payload byte 248 (260 in the
  12-byte-longer layout) = index 0..5 → hovel1..hovel6; see "Placed buildings"
  above for the evidence and `manifests/hovel_variants.csv` for the numbers.
  **Model anchors (new):** the model block ends with the anchor section
  `{3 bytes}{f32 x,z,y}{u16 p,q,r}{3 bytes}{u16 n}` + n × `{f32 x,z,y}{u16 v}`
  + a closing u16 — positions in cm in the same axis order as the bbox
  (`bdg_fields.anchors()`; on 78 of 121 files the section accounts for the whole
  file, ending exactly at EOF; 102/117 keep every point inside the model's own
  bbox ±25 cm, the rest sit on the sibling state models). The `u16 p,q,r` triple
  is 180,100,100 on most files (180,192,198 / 180,213,253 on the keeps) —
  semantics UNKNOWN, as are the per-record `u16 v` values (see the field table
  above: 99..800, mode 200 = 1 cell — a per-point radius/clearance by unit reading,
  unverified).
- **Orphan `.bdg`s (2026-10-08):** `ruin_01..07.bdg` are referenced by **nothing**
  — not by the exe's 104-entry `.bdg` table, not by any other file in the install
  (only their own `.gr2`/lightmap files mention them). The engine's rubble models
  are the 12 `ruins_{wood,stone}_{1x1..8x8}.gr2` (see the Scenery section), so
  the `ruin_0*.bdg`/`.gr2` pairs are leftovers from an older rubble system.
- Both decode paths and the bboxes are regenerable with
  `sh2-port/tools/dump_models.py` (gr2dump.exe under wine32 → JSON + bbox) and
  `sh2-port/tools/build_mesh_cache.py` (placements JSON → `.osh2mesh` cache,
  writing a real `SourcePath` so material DDS resolve — a cache written without
  it renders untextured).

## help/stronghold_2_help.hlp — the shipped INPUT GRAMMAR (read 2026-10-11) — DONE as a source

The shipped WinHelp file is the game's own statement of how units are commanded,
and it is why R010/R011/R012 read the way they do. It is NOT a binary format to
parse: `strings -n 6 "$SH2_GAME_ROOT/help/stronghold_2_help.hlp"` prints
the sections in order with their `<!-- comment -->` / `<section name=...>` markers
intact, so grep the output (do not open the .hlp in a viewer; it is a compiled
WinHelp container).

**What it settles** (verbatim quotes from the game's own help):

- **Selection**: left click selects one unit; press-and-hold then drag draws a box,
  and everything inside is selected on release. That is the whole select grammar —
  the drag box is the shipped default, not an invention.
- **Movement**: with a unit selected the pointer becomes the movement cursor (green
  = reachable, red cross = inaccessible) and a left click moves the unit there. One
  destination per order — no waypoint chaining, no double-click, no modifier.
- **Attack**: select a unit, then click the enemy when the red attack pointer
  appears. The pointer is enemy-specific, i.e. it needs the faction model the maps
  do not carry (R007).
- **Stop**: `Stop - Pressing the stop causes units or siege equipment to stop what
  they are doing. For example, Siege equipment will stop firing stone.` — listed in
  the "Unit Commands" section together with Attack Here / Patrol / Disband /
  Mount Horse / Dismount Horse / Return the Lord to Civilian Duties, i.e. **all of
  them command-box buttons**, not keys and not mouse verbs.
- **No keyboard bindings for unit commands at all** (R011). The ONE documented
  keyboard use in the unit grammar is holding SHIFT to add multiple points to a
  Patrol, which is command-box functionality and is why plain movement stays
  exactly one destination (R012). **There is no right-click verb anywhere in the
  unit grammar.**
- **Formations** live in the same box (Open Combat / Column / Defensive Knot /
  Line) — recorded for a future formation-command slice, unbuilt.
- **The box itself is a GRID OF ICON BUTTONS, and its screen position is NOT in
  the shipped data**: every `<section>` carries a `loadpic="<name>.bmp"` and
  `help/gfx/` ships 142 `.bmp` screenshots — `unit commands.bmp` shows the box as
  a 4x4 grid of icons (crossed swords, shield, lord portrait, patrol arrow, hand,
  …) cropped to the box, so it proves the button/icon reading and says nothing
  about where the box sits on the screen. Not decoded anywhere.

**Consequences in-engine (2026-10-11):** `S2MActorCommander` draws a
minimal command box (one Stop button) while a selection exists and hit-tests it as
the first test of a click; the button's rectangle is a HARNESS placement chosen
because no shipped UI layout is decoded — a future HUD replaces it.

## i18n/en-us.xml — the shipped STRING TABLE (frontend names read 2026-10-12) — DONE as a source

The install's own translation table, and the authority for the frontend's text.
`i18n/lang.txt` says `en-us`; `i18n/en-us.xml` is a SpreadsheetML (Excel-XML)
workbook — `<Worksheet ss:Name="Translation en-us">`, one `<Row>` per string with
four cells (id, English text, translated text, changed) — **5,593 rows, 5,592 of
them with a non-empty id**. Read it as text and match the rows (the
`<Data ss:Type="String">` cells, XML-unescaped); there is no in-engine handler
yet, and nothing else in the pipeline reads it (the one earlier mention is
`sh2-port/tools/building_parse.py` using its `BUILD_*` tags).

**The shipped help file does NOT cover the frontend.** `help/stronghold_2_help.hlp`
(its own section above) is the input grammar for the GAME: all 195 of its
sections are in-game — buildings, units, formations, unit commands, ranks, the 36
tutorials — and its only "menu" mentions are the in-game menu bar and the
catapult's own menu. So the six frontend buttons' names and destinations come
from the string table instead:

| id | text | the shipped screen the button opens |
| --- | --- | --- |
| `MAINMENU_PLAY` | Play | `PLAYSCREEN` 'Choose Your Path' (then `WARSCREEN_*` Campaign / Conquest Trail / Kingmaker / Siege / Custom / Load, and `PEACESCREEN_*` Sim Campaign / Free Build / Custom Maps) |
| `MAINMENU_MULTIPLAYER` | Multiplayer | `MULTIPLAYERSETUPSCREEN` (internet / local network / host / join) and `LOBBYSCREEN_NETWORK_LOBBY` 'Network Lobby' |
| `MAINMENU_EDITOR` | Map Editor | `MAINSCENARIOSCREEN` ('Create a New Scenario' / 'Load Scenario'), then `SCENARIOEDITORSCREEN` / `EDITORMENU_*` |
| `MAINMENU_SETTINGS` | Options | the options screen — its rows are `AUDIOSETTINGS_*` (Music/Sfx/Speech Volume), `VIDEOSETTINGS_RESOLUTION`, `MENU_VIDEO_WINDOWED_MODE` |
| `MAINMENU_LOAD` | Load | `LOADSCENARIOSCREEN` (header 'Load'/'Save', columns Name/Date/P) |
| `MAINMENU_EXIT` | Exit | the exit confirmation — the shipped voice line `Confirm1.wav` 'Exit Stronghold 2??' and `ui/frontend/exitcancel.jpg` (so the button does not quit outright) |

**Consequences in-engine (2026-10-12):** `UI/MainMenuActions.cs`
carries those six ids and texts as the port's own action rows, and `MainMenuUI`
builds each button from its row — the labels the port had hardcoded turned out to
be the shipped ones. A row's `Built` flag is **derived, not toggled**: the row
carries the screen it opens (`GameState`) and `Built` is `Opens != NONE`, so a
button can never claim a destination the flow cannot show. The flow UAT asserts
the ids/texts against this file at run time (6/6 rows agree, 5,592 strings read;
`S2MGameFlowRun.ReadShippedFrontendStrings` is the row matcher). The imagery the
same folder ships is the corresponding art (`ui/frontend/`:
`frontend_button_fancy.tga` + `_highlight`, `frontend_logo.tga`,
`opponent_selection.png`, `scenario_editor_screen.jpg`, `exitcancel.jpg`,
`loadscreen_*.jpg`), and the port already loads the logo and the button sprites
(`UI/UIAssets.cs`).

**THE PLAY SCREEN — `MAINMENU_PLAY`'s destination, the port's first built
frontend screen (2026-10-12).** The screen's own rows are
the table's, read the same way:

| id | text | what it is on the screen |
| --- | --- | --- |
| `PLAYSCREEN_CHOOSE_YOUR_PATH` | Choose Your Path | the screen's title (the one string no button's row owns) |
| `PLAYSCREEN_WAR_TOOLTIP_TITLE` | The Path of War | the war button; tooltip content `PLAYSCREEN_WAR_TOOLTIP_CONTENT` 'Combat intensive styles of play.' |
| `PLAYSCREEN_PEACE_TOOLTIP_TITLE` | The Path of Peace | the peace button; tooltip content `PLAYSCREEN_PEACE_TOOLTIP_CONTENT` 'Gentler game modes, focusing mainly on castle life.' |
| `PLAYSCREENSTATICTOOLTIP_TUTORIAL` | Play the Tutorial | the tutorial medallion |
| `PLAYSCREENSTATICTOOLTIP_PREVIOUS_MENU` | Previous Menu | the way back to the main menu (the one action of the four the port has built) |

**The art is shipped in two folders, and the folder names are the screen's
structure.** `ui/choose_path/choose_path_background.jpg` (1024x768 = the 4:3 the
menu's content rect already uses) is the screen's background: a king with a
purple cloak on the left and an armoured figure with a green cloak on the right,
i.e. the two paths as pictures. The buttons are in `ui/war_peace_menu/`, each
with its own "on" state beside its "off" one:
`war_off.jpg`/`war_on.jpg` and `peace_off.jpg`/`peace_on.jpg` (363x407 each) and
the round medallion `tutorial_button_off.tga`/`tutorial_button_on.tga`
(137x153). **The same folder holds the WAR and PEACE screens' own pictures**,
which is what those screens will be built from:
`war_campaign.jpg`, `war_kingmaker.jpg`, `war_siege.jpg`, `warpeace_custom.jpg`,
`warpeace_load.jpg`, `war_multiplayer.jpg`, `peace_simcampaign.jpg`,
`peace_freebuild.jpg`, plus `war_peace_menu_back.jpg` (1024x768, the sub-menus'
own background) and `background_lion_side.jpg`.

**In-engine:** `UI/PlayScreenActions.cs` carries the four rows and THE one entry
point (`PlayScreenActions.Dispatch`, the same discipline as the menu's — action
named on the console, click recorded, destination reported honestly), and
`UI/PlayScreenUI.cs` builds the screen from the art above. Two facts are the
port's own and are marked as such, because the shipped data does not carry them:
(1) **the layout** — the art's own composition is read as peace-left (the king)
and war-right (the armoured figure), with the title above both, the tutorial
medallion centred at the bottom and the fancy frontend button as the way back;
(2) **the two path titles and 'Play the Tutorial' are drawn as captions** (the
shipped buttons are pictures whose text is their hover tooltip, and this port
has no hover panel — the tooltip CONTENTS ride on the rows and are printed by
the UAT instead). The flow UAT checks the rendered title and every label against
this file at run time (5/5 PLAYSCREEN* rows agree) and clicks all four buttons
through their own pointer click. **Music:** the shipped sheet's frontend play
lists are exactly `PL_LOADING` and `PL_MAINMENU` (see the Music section), so the
play screen has NO cue of its own: the frontend cue starts once and is not
restarted by the round trip (`Main.TickGameFlow` only plays `PL_MAINMENU` when
it is not already the current list), and the UAT checks the play-list clock kept
advancing across it.

**THE TWO PATH SCREENS — the play screen's War and Peace destinations
(2026-10-12).** The war screen and the peace screen are the
screens behind the play screen's two path buttons, and their rows are the
table's, read the same way:

| id | text | what it is on the screen |
| --- | --- | --- |
| `WARSCREEN_CAMPAIGN` | Campaign | the war screen's first picture button; tooltip content `WARSCREENSTATICTOOLTIP_CAMPAIGN` 'Play the 'Lost King' campaign' |
| `WARSCREEN_CONQUEST` | Conquest Trail | the Gold Edition's addition (art below); tooltip `WARSCREENSTATICTOOLTIP_CONQUEST` 'Seige and defend the Strongholds of Europe as you conquer the continent.' (the shipped spelling) |
| `WARSCREEN_KINGMAKER` | Kingmaker | tooltip `WARSCREENSTATICTOOLTIP_KINGMAKER` 'Skirmish for the crown, in a Kingmaker challenge' |
| `WARSCREEN_SIEGE` | Siege | tooltip `WARSCREENSTATICTOOLTIP_SIEGE` 'Relive history. Siege a real castle!' |
| `WARSCREEN_CUSTOM` | Custom | tooltip `WARSCREENSTATICTOOLTIP_CUSTOM` 'Load a user created scenario' |
| `WARSCREEN_LOAD` | Load | tooltip `WARSCREENSTATICTOOLTIP_LOAD` 'Load a previously saved game' |
| `WARSCREENSTATICTOOLTIP_EXIT` | Previous menu | the war screen's way back — to the PLAY SCREEN, not to the menu |
| `PEACESCREEN_SIM_CAMPAIGN` | Sim Campaign | tooltip `PEACESCREENSTATICTOOLTIP_SIM_CAMPAIGN` 'Help the King rebuild the county, in this gentler Sim campaign' |
| `PEACESCREEN_FREE_BUILD` | Free Build | tooltip `PEACESCREENSTATICTOOLTIP_FREE_BUILD` 'Build peacefully in a setting of your own choosing' |
| `PEACESCREEN_CUSTOM_MAPS` | Custom Maps | tooltip `PEACESCREENSTATICTOOLTIP_CUSTOM_MAPS` 'Load a user created sim mission' |
| `PEACECREENSTATICTOOLTIP_EXIT` | Previous menu | the peace screen's way back — **the shipped typo (one S) is the only row there is**: no correctly spelled twin exists (checked 2026-10-12), so the port's row id carries the typo |

**The screens behind those nine buttons, named by the same table** (each click
reports its own, so a button says the shipped screen's own name): the campaign's
chapters (`CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12` 'Start Chapter 1..12'), the
Conquest Trail's castle map (`CONQUESTTRAIL_1..12`, `ui/gold_edition/
conquest_trail.png`, with `CONQUEST_PREV_CASTLE` 'Go Back one Castle along the
trail.' and its siblings), the Kingmaker setup
(`SCENARIOEDITORSCREEN_MAKE_SKIRMISH` 'Kingmaker', `SKIRMISH_LORD_*`), the siege
selection and setup (`SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME` 'Select this
Siege', `SIEGESETUPSCREENSTATICTOOLTIP_ATTACK` 'Command the assault of the
castle' / `_DEFEND` 'Take charge of the castle garrison'), the custom scenario
list (`MAINSCENARIOSCREEN_HELP_CREATE_NEW_SCENARIO` 'Create a New Scenario',
`MAINSCENARIOSCREEN_LOAD_SCENARIO` 'Load Scenario'), the save/load list
(`LOADSCENARIOSCREEN_*`), the Sim campaign's chapters
(`SIM_CAMPAIGN_HEADING00..11` 'The Kings Beacon' .. 'The Kings Visit'), the free
build map selection (`SCENARIOEDITORSCREEN_MAKE_FREEBUILD` 'Free Build') and the
custom map list (`MAINSCENARIOSCREEN`). Two of them are built now — the campaign
screen (2026-10-12) and the Conquest Trail (2026-10-13) — and those rows say
where they go; the other seven still report "no destination yet: <shipped
screen> is not built", each by its own name.

**The art: one backdrop, nine pictures, and the Conquest Trail's own folder.**
`ui/war_peace_menu/war_peace_menu_back.jpg` (1024x768) is both screens' backdrop
— a stone wall with a picture frame either side and an empty description bar
along the bottom (the frames are centred at +0.10 of the height, the bar spans
-0.26 to -0.42) — and each picture row's button is the 366x367 jpg named after
it: `war_campaign.jpg`, `war_kingmaker.jpg`, `war_siege.jpg`,
`warpeace_custom.jpg`, `warpeace_load.jpg`, `peace_simcampaign.jpg`,
`peace_freebuild.jpg`. Two facts the folder itself carries:
`warpeace_custom.jpg` is SHARED (the war screen's Custom and the peace screen's
Custom Maps are one picture, which is what the file's name says), and
`war_multiplayer.jpg` is NOT one of the nine — it is named for the multiplayer
screens the menu's own `MAINMENU_MULTIPLAYER` row opens. **The Conquest Trail's
picture is elsewhere:** Conquest Trail came with the Gold Edition, which ships
its own folder — `ui/gold_edition/war_conquest.jpg` (366x367, a sword over the
map of Europe with the trail's red arrows) for the button,
`conquest_trail.png` + `war_conquest_arrows01..11.jpg` +
`conquest_trail_winscreen.png` for the screen behind it, and
`conquest_trail_arrow{back,forward}{,_on}.tga` for its own navigation. **No
hover art:** the install ships "on" states beside the play screen's two path
buttons only (`war_on`/`peace_on`), not beside these nine, so the port's
path-screen buttons do not swap their picture.

**In-engine (2026-10-12):** `UI/PathScreenRow.cs` is the
row both screens share (id, label, tooltip content, destination, picture, button
name; `Built` derived from the screen it opens, so a row cannot claim a screen
the flow cannot show), `UI/WarScreenActions.cs` and `UI/PeaceScreenActions.cs`
are the two screens' own action surfaces (each with THE one entry point — the
menu's and the play screen's discipline), and `UI/PathScreenUI.cs` builds either
screen, with `UI/WarScreenUI.cs` / `UI/PeaceScreenUI.cs` declaring only their
rows and the room those rows need. Four facts are this port's own, marked as
such because the shipped data does not carry them: (1) **the layout** — the
picture buttons stand in the columns the backdrop's own painted frames stand in,
three to a row (the war screen's six make two rows straddling the frames' centre;
the peace screen's three make one row there); (2) **the row labels are drawn as
captions** under their pictures (the shipped screen shows them as hover tooltips
and this port builds no hover panel — the contents ride on the rows and are
printed by the UAT); (3) **the way back sits in the backdrop's empty description
bar** (with no tooltip panel built, that bar is the screen's own bottom action's
place); (4) **the painted frames keep their own pictures** — the buttons are laid
over the wall, not into the frames, because the folder ships one picture per ROW
and two painted frames cannot be six buttons. The flow UAT checks every id and
label against this file at run time (7/7 `WARSCREEN*` rows and 4/4
`PEACESCREEN*`/`PEACECREEN*` rows agree), checks every button's sprite is its own
row's picture by file name, and clicks all nine through their own pointer click.

**THE CAMPAIGN SCREEN — the war screen's Campaign destination, the 'Lost King'
campaign's twelve-chapter list (2026-10-12).** The
screen's own rows are the table's, and it is the first frontend screen the table
names TWICE per button — the chapter, and the button's own static tooltip:

| id | text | what it is on the screen |
| --- | --- | --- |
| `WARCAMPAIGN_MISSION1..12` | Chapter 1 .. Chapter 12 | the twelve chapter rows' LABELS (1-indexed, matching both the tooltips and `maps/war_chapter1..12.s2m`) |
| `CAMPAIGNSCREENSTATICTOOLTIP_PLAY_1..12` | Start Chapter 1 .. Start Chapter 12 | each chapter button's own row: the static tooltip content |
| `CAMPAIGNSCREENSTATICTOOLTIP_EXIT` | Previous Menu | the way back — to the WAR SCREEN, not to the play screen and not to the menu |

**The install names the campaign's chapters a second way, and this screen does
not draw it.** `WAR_CAMPAIGN_HEADING00..11` are the twelve titles — 'The Search
for the King', 'First Command', "Edwin's Rescue", 'The Bull in the Borderlands',
'Return to the Monastery', 'Olaf Grimtooth, Your Time Has Come', 'Defence of the
Abbey' (the shipped spelling), 'Hawks Nest', "Lord Barclay's Castle", 'Siege of
the Abbey', 'Treachery', 'To Capture a King' (0-indexed; the Sim campaign has its
own set, `SIM_CAMPAIGN_HEADING00..11` 'The Kings Beacon' .. 'The Kings Visit') —
and no `CAMPAIGNSCREEN*` row names them. The chapter BRIEFINGS are a different
screen with their own art: `ui/chapter_introductions/chapter01..12_intro.jpg`
(1024x768 each — a moonlit castle, knights in red cloaks, an EMPTY red ribbon
across the top and an empty oval for the text). So `campaign_chapterN.jpg` is
this screen's own map, one per chapter, and the headings (which read like a
briefing's title, and pair with the `C1MissionN_*` narration rows) are the
briefing screen's business — not built here, named.

**The art is one folder, `ui/campaign_screen/`, and it paints the screen's own
composition.** Twelve 1024x768 pictures, one per chapter — the same desk, candle,
map of Britain and parchment scroll column in every one, with the campaign's
route drawn a little further along in each (chapter 1 is a single red marker in
Wales; by chapter 12 the map carries the whole route, in red, blue and green) —
so the picture IS a per-chapter progress map. The scroll column on the right is
twelve EMPTY parchment slots (215x43 px, grouped six / three / three, separated by
two ornamental dividers, pitch ~50 px) — the chapter list, its buttons left to the
game — and a gold-framed description bar sits at the bottom left (186..521 x
661..746 of the 1024x768 picture). The one button the screen reuses is
`campaign_button.bmp` (251x44, blank parchment), and `campaign_button_highlight.tga`
(251x44) is its hover art — **the only frontend button the install ships hover art
for, and it is an OVERLAY, not a replacement face**: alpha ~91 of 255 at its
centre, fading to ~10 at its edges, so drawing it alone would make the button
vanish. The six `symbol_{good,bad,red}_{complete,current}.tga` (32x32) are the
campaign's progressive-state markers (a completed chapter vs the current one, in
three states).

**In-engine (2026-10-12):** `GameState.CAMPAIGN_SCREEN`
(=27) is the war screen's branch — `WARSCREEN_CAMPAIGN`'s `Opens` —
`UI/CampaignScreenUI.cs` builds the screen (canvas 1400, above the war screen's
1300, with its own aspect-ratio bars), `UI/CampaignScreenActions.cs` carries its
rows and THE one entry point, and `UI/CampaignScreenRow.cs` is its row type: the
PAIR of ids above (`Id` = the button's own tooltip row, `LabelId` = the label's
row), which is why it is not the path screens' `PathScreenRow` (that row carries
one id). **The one frontend layout the INSTALL dictates rather than the port reading
a composition is the buttons' own place:** the twelve buttons stand ON the art's
own painted slots (centre x +0.3545 of the 4:3 rect, 0.2100 x 0.0560 each, the
twelve centre-y values in `CampaignScreenUI`), and the button art is 251 px wide
against the slot's 215, so a button fills its slot at 0.86x horizontally rather
than overhanging it by 18 px a side. Four facts are this port's own, marked
because the shipped data does not carry them: (1) **the backdrop is chapter 1's
picture** (`campaign_chapter1.jpg`) — the port has no save/progress data, so it
opens where the shipped campaign starts, and it does not change the picture;
(2) **the labels are drawn inside the buttons**, dark ink on the
parchment (the shipped button is blank art and the shipped screen shows the text
on hover); (3) **the way back sits in the art's own description bar** — not a
tooltip panel, because this port builds no hover panel (the path screens' own
decision, taken again; the shipped tooltip CONTENT rides on the rows, printed by
the UAT instead); (4) **no title** (there is no `CAMPAIGNSCREEN` title row) and
**no progress markers** (there is no save system — the six `symbol_*` and the
other eleven chapter pictures are named above for whoever builds one).
**The install's BMP art needed its own reader for this screen:** Unity's
`ImageConversion.LoadImage` does not read BMP, and `campaign_button.bmp` is one —
`UI/BMPLoader.cs` reads the shapes the install actually ships (280 of its 283
`.bmp` files are 24-bit, 3 are 32-bit, all uncompressed BI_RGB; the 32-bit ones
carry ALPHA in the byte the spec calls reserved — over this button it is 255
across the body and ramps to ~0 along the left edge, measured). The flow UAT
checks the twelve pairs of id/text against this file at run time (25/25
`WARCAMPAIGN_MISSION*` + `CAMPAIGNSCREENSTATICTOOLTIP_*` rows agree, and the
twelve `WAR_CAMPAIGN_HEADING*` titles are read and reported as not drawn), checks
each button's sprite is the install's own button by file name, reads
`campaign_chapter1.jpg` back from the install and checks every button's middle is
brighter than the gap half a slot above it (12/12 on parchment, one straight
column, x spread 0 px), and walks the tree one branch deeper:
menu -> play -> war -> campaign -> war -> play -> peace -> play -> menu.

**The chapter click STARTED (2026-10-12):** a chapter row
carries its own map file (`CampaignScreenRow.MapPath` = `maps/war_chapterN.s2m`,
`Built` derived from it exactly as a screen row's is from `Opens`), and its click
is the ONE frontend click that leaves the frontend tree: it asks
`Game.RequestStartChapter` (accepted only from the campaign screen, only with a
map path), which sets `Game.CurrentMapPath` and moves the flow
`CAMPAIGN_SCREEN -> MAP_LOADING -> MAP_LOADED` through the one `Game.SetState`.
`MAP_LOADING` builds the clicked chapter's whole world through
`Stronghold2/MapLoading/S2MMapWorldLoader` (the SAME loader the dev-harness boot
uses - terrain + textures, water, walls, vegetation, buildings, actors, the actor
commander), whose per-map data files are named after the map's stem
(`artifacts/buildings_<stem>.json`, `entities_<stem>.json`; all 12 war chapters
and peace_chapter1 ship/generate them), and it takes the frontend's screens down
on the way (`Main.TickGameFlow` calls `TickFrontend` with no frontend state
naming a screen). `MAP_LOADED` is where the game's own next screen belongs - the
briefing/objectives screen, built 2026-10-13 (below).
The UAT's chapter-start walk clicks 'Chapter 4' and checks the loaded world
against the port's manifests: the map's scenario census (1 mission / 71 events ==
`scenario_missions.csv` / `scenario_events.csv`), its 191 actors ==
`entity_estates.csv`, its 18 placements (12 + 6 props) and 134 scenery == the
buildings JSON + `placement_verification.csv`, and its walls 469 stone / 69
wooden / 0 deck == `walls_war_chapter4.json` (`walls_parse.py`'s ground truth).

**The mission-start briefing screen is built (2026-10-13):
`GameState.BRIEFING`, and the text it can and cannot show.** The loaded map hands
the flow `MAP_LOADED -> BRIEFING` on the next tick (`MAP_LOADED` is now one tick
long, and is where the load's own result is read) and the game's own
mission-start screen goes up: `UI/BriefingScreenUI.cs` builds the chapter's own
picture (`ui/chapter_introductions/chapterNN_intro.jpg`, 1024x768 - the chapter's
number read from the map file the flow is playing, `maps/war_chapterN.s2m`), the
chapter's title on the picture's own painted ribbon (measured: the red band is y
16..126 of 768 in all twelve, x 8..972 - art that exists to carry a title, and
ships empty), the shipped `hint_bar.tga` line, the shipped screen's three tab
buttons with their off/on art and their own rows, the two shipped medallions that
page the hints (`hint_closed.tga`; the folder also ships `hint_open.tga` and
`hint_highlight.tga`), and the shipped 'To the Game' row - the one that leaves the
screen, for `GameState.GAMEPLAY`, through `Game.RequestLeaveBriefing` (the
frontend's one-entry-point discipline on the game's own screen; the tab and page
rows share that one surface, `BriefingScreenActions.Dispatch`). Everything the
screen shows is read from the install at RUN TIME: `Stronghold2/Text/
S2MTextTable.cs` reads `i18n/en-us.xml` and `Stronghold2/Missions/
S2MChapterBriefing.cs` resolves the chapter's title
(`WAR_CAMPAIGN_HEADING{chapter-1}`, two-digit), its hints
(`WARHINT{chapter-1}_0..7`, empty slots dropped) and its picture - while the UAT
keeps its OWN parse of the same file, so the two readers still check each other
(the authority check the frontend's screens get, same shape).

What that measurement found, and what the screen therefore does NOT draw - the
answer to the open question "where is the briefing's text":

- **No objective/briefing PROSE is shipped for the war campaign's twelve
  chapters.** The only briefing paragraphs in the table are `DEMO_BRIEFING_1..3`
  (the demo campaign's three missions - "By now you should have arrived. Tom
  Simpkins, my trusted scribe will contact you shortly to aid you further...");
  the maps author no start-of-mission action (`EnterBriefingAction`'s two shipped
  instances are mid-mission in war_chapter6, and the action carries NO payload -
  its reader reads a header, a payload marker and a trailer, so it names no
  text); the 98 `cutscenes/*.csc` files are binary (zero ASCII runs >= 6 - camera
  data; the per-mission narration is the separate `C{n}Mission{n}_{n}.wav` subtitle
  rows); and the shipped help file documents the game, not the frontend. So the
  Objective tab carries the chapter's title - the one per-chapter line that IS
  shipped - and the report says so rather than the port inventing a line.
- **The hint rows' chapter pairing is NOT pinned by the install's data, and the
  screen says which reading it used.** The table ships 14 war families
  (`WARHINT00..13`, 2-6 non-empty slots each) for 12 chapters whose maps carry 19
  missions, and nothing in the install names a family for a chapter (no id
  carries a chapter number, no map references a hint, `ENTER_BRIEFING` has no
  payload). **TWO MEASURED ANCHORS DISAGREE:** `WARHINT00` describes
  war_chapter1.s2m (that map's own `CustomChapter1Trigger` row is 'Fight through
  the guards and reach the boat', and the hint says 'Direct Sir William and
  Matthew (the two horsemen) to a position near the boat'), while war_chapter8.s2m's
  win - `NoMessagesPlayingTrigger + CustomTriggerWilliamReachKeep -> WinAction`
  plus a `ControlLordsAIAction[IsPlayerControlled=true]` that hands the player a
  lord - is `WARHINT09`'s text verbatim ('Once he is rescued you will be able to
  control William and his troops'). Two anchors, two different offsets, and no
  constant offset fits. The port therefore draws the hints BY THE ROWS' OWN NAMES
  (`WARHINT<chapter-1>_0..7`) - the only attribution the install's own row names
  offer, marked in code and on the report by
  `S2MChapterBriefing.HintAttributionNote` - and this is an OPEN QUESTION that needs
  the game in front of it: flipping it is one method
  (`S2MChapterBriefing.Resolve`). The same chapter-numbering question shows in the
  install's own art: `castles/AIVChap4_Bull.aic` sits beside
  `chapter04_intro.jpg`'s bull warrior and `WAR_CAMPAIGN_HEADING03` 'The Bull in
  the Borderlands' (all chapter 4: the pictures, the headings and the .aic files
  agree on 1-based chapters), while `AIVChap0_*.aic` names a chapter 0 (LordGrey,
  SirEdwin, SirWilliam) and the hint families outnumber the chapters.
- **The Tutorial tab carries nothing,** because the install ships tutorial text
  only as the tutorial's own speech rows (`Tutorial_1..86.wav`, subtitles of
  voiced lines) - never per mission. Named on the report, not filled with prose
  the install does not have.

The port's own choices on this screen, marked as such: the layout (the picture
fills the 4:3 rect; the title in the art's ribbon; the tab row, the hint bar with
its medallions and the play button sharing the picture's bottom band - no shipped
composition is decoded, and the art's own `briefing_screen/briefing_background.jpg`
with its empty frame is used only as the fallback backdrop for a map that is not
a campaign chapter); the play button's art (the table names the row, not a
picture, so it wears the frontend's fancy button like every other table-named row
here); the reading of the tutorial's own speech line "return to the game by
clicking on the right arrow at the bottom of the briefing screen" onto that
bottom-row button; and the tab captions drawn above the tabs' icons.

**The game's own MAIN SCREEN is built (2026-10-13):
`GameState.GAMEPLAY`, the first surface after the briefing.** The briefing's own
play row hands the map to the game, and the game's own first surface is its main
screen: the string table's own family `MAINSCREENROLLOVERS079..091` names it - six
status fields (`_079` Current Rank, `_080` Date, `_081` Gold, `_082` Honour,
`_083` Peasants / Maximum Peasants, `_084` Popularity) and seven controls (`_085`
Delete, `_086` Objectives, `_087` Castle Views, `_088` Estate Ownership, `_089`
Zoom, `_090` Reports, `_091` Radar Map (left click to move); the castle-view
family continues at `CASTLEVIEWROLLOVERS092..099`). **Its status bar is the one
fully composed in-game surface piece the install ships:** `ui/ingameui.tga`
(1024x1024, 32bpp, non-RLE) paints the whole bar across its top rows - measured
2026-10-13, in x px of 1024: the left end cap, the Date inset 11..137, the
Peasants inset 144..405, the stone Current Rank plaque 407..610 (taller than the
insets - its banner dips), Gold 611..708 with the gold-coin medallion at
613..650, Honour 711..823 with the crown at 723..744, Popularity 824..937 with
the face at 843..852, and a seventh slot 938..1013 that NO rollover row names.
The bar's painted body ends at row ~28 (measured per column: the left and right
insets' frames both, their red areas ending ~row 24), the stone plaque's banner
the one piece that dips lower (~row 36); the port's strip is the top 45 rows of
the atlas - the bar's paint plus the gap above the next atlas row. The in-game
menus' own small button is `ui/ingame_menus/button_fancy_small.tga` (192x46).

Where the three numbers it can carry come from - all decoded by the port's own
readers already: **Date** = `Scenario.StartingYear` + `Scenario.StartingMonth`
with the install's own `MONTH_JANUARY..DECEMBER` rows ('Jan'..'Dec') for the
month's name, and **the month is 0-BASED - the manual pins it**: the shipped
manual's chapter editor says "The default date is January 1066", and
`war_chapter2.s2m` / `war_chapter10.s2m`, scenarios left at that default, store
`1066-00` (the scenario report's own line). `war_chapter4.s2m` stores `1084-00`,
so the bar reads 'Jan 1084'. **Gold** = `Mission.StartingGold` (war_chapter4:
1000 - the same number `scenario_missions.csv` carries, an independent read) and
**Popularity** = `Mission.StartingPopularity` (90), both resolved by
`Stronghold2/Missions/S2MMissionState.cs` from the map file the flow loaded.

**Three of the bar's six fields stay EMPTY, named rather than invented:** Current
Rank, Honour and Peasants / Maximum Peasants are the RUNTIME's own state - the
mission's data carries no honour, peasants or rank, and this port runs no
simulation yet, so those slots are left as the install paints them
(`S2MMissionState.RuntimeFieldsNote`). **And only ONE of the seven controls is
drawn:** the install pins no art for the others (the atlas's icon buttons carry
no labels, so which icon belongs to Delete and which to Reports is NOT in the
shipped data), so the port draws no button that could claim an unpinned pairing -
the six are carried as rows and named on the report, while the Objectives row
(the one whose own text names its destination - the mission objectives screen the
port already carries) wears the in-game menus' own small fancy button with the
row's shipped text and opens the briefing screen's IN-GAME REVIEW: the shipped
screen's other play row, `MISSIONOBJECTIVESSCREENSTATICTOOLTIP_RETURN_TO_GAME`
'Return to the Game', now has an opener (`Game.RequestViewObjectives`, accepted
only from GAMEPLAY), and it leaves back to GAMEPLAY through the same ask the
start briefing's 'To the Game' uses (`Game.RequestLeaveBriefing`) - so the two
showings of the briefing screen differ in exactly which of the shipped screen's
two play rows they wear.

The port's own readings on this surface, marked as such (the UAT report
says each): **the bar's slot attribution** - the install names the six fields and
the seven controls but not their positions, so the port reads them in the
rollover table's own order and leaves the seventh painted slot empty
(`S2MMissionState.SlotAttributionNote`); **the bar's anchoring** - the exe's own
is not in the shipped data, so the bar spans the screen width at the strip's own
1024:45 aspect with its top edge at the top, and every other piece (the values,
the button) is anchored relative to the bar's own rect so the whole surface
scales together at any resolution; each value is drawn in its slot's red area,
right of the slot's medallion; the Objectives button hangs below the bar's left
end (the install pins no position for it either); the date's word order ('Jan
1084', shipped month text then the map's own year); and the label font
(Unity's built-in, like every label in this port) and its parchment colour.

**THE CONQUEST TRAIL SCREEN — the war screen's Conquest Trail destination, the
Gold Edition's castle map (2026-10-13).** Its whole row family
is small, and the table names every one of it:

| id | text | what it is on the screen |
| --- | --- | --- |
| `CONQUESTTRAIL_1..12` | 'Your first mission is to defend Guadamur castle in Spain.' .. 'Congratulations!  Your enemies have been defeated, ...' | the trail's twelve rows, one per castle the campaign crosses plus the completion message - the screen shows ONE at a time, in the art's own panel |
| `CONQUEST_PREV_CASTLE` | Go Back one Castle along the trail. | the left paging button's tooltip content |
| `CONQUEST_NEXT_CASTLE` | Go Forward one Castle along the trail. | the right paging button's tooltip content |
| `CONQUEST_NOPREV_CASTLE` | You are at the start of the trail. | the left button's DISABLED wording |
| `CONQUEST_NONEXT_CASTLE` | You are currently at your furthest point along the trail. | the right button's DISABLED wording |
| `CONQUEST START MISSION` | Start Mission | the row that starts the selected castle's mission |
| `CONQUEST START AGAIN` | Do you wish to take up the challenge again? | the start row's own text once the trail is finished (the shipped restart question) |

**There is NO `CONQUEST*` EXIT row.** The trail screen ships no way-back row of
its own (the campaign screen's `CAMPAIGNSCREENSTATICTOOLTIP_EXIT` has no
counterpart here - checked 2026-10-13), so the port's way back wears the war
screen's own `WARSCREENSTATICTOOLTIP_EXIT` 'Previous menu', the same row the
parent screen itself uses. **The eleven castles are named in the texts, not in a
table:** Guadamur, Javier, The Heuneburg, Wartburg, Haut-Koenigsburg, Fougères,
Windsor, Harlech, Criccieth, Edinburgh, and Wartburg again for the retake (row
11) - ten distinct castles over eleven mission rows - and row 12 names none (it
is the congratulation).

**The art is the Gold Edition's own folder, `ui/gold_edition/`, and it is one
COMPOSED screen plus the trail's own progress pictures.** `conquest_trail.png`
(1024x768) is the whole screen as the shipped game draws it - the framed map of
Europe with the trail's route and its ten castle labels painted on it, the ivy
and the candle, the two paging-arrow tiles at the map's bottom right (measured:
the install's own 52x52 `conquest_trail_arrowback/forward.tga` art stands at
x 732 and x 782, y 507, with `_on.tga` twins for the pressed state), and an EMPTY
gold-framed panel along the bottom (measured: its inner rect is x 174..839,
y 588..709, between the gold lines at x 171..173 / 840..842 and y 585..587 /
710..712) - so the screen's text and its buttons have exactly one place the art
leaves for them. The trail's own progress is `war_conquest_arrows01..11.jpg`
(558x357 each): the same picture of the map with the trail drawn one castle
further along - arrows01 has only Guadamur lit, arrows11 the whole route - drawn
at the rect the shipped screen draws it in, measured by subimage match against
the backdrop: **x 209, y 114** (the match is pixel-exact: mean absolute channel
difference 0.0076 of 1.0 for arrows11, 0.06 for a different progress state).
`conquest_trail_winscreen.png` (660x533, a crown on a purple field) is the
trail's COMPLETION art - progressive-state art this port does not draw (it has no
progress data), like the campaign screen's six `symbol_*` markers. There are
**22** `castle_<name>_painting.jpg` (425x313, each with its own location inset)
and 22 `layout_<name>.jpg` in `ui/siege_menu/`, and all ten of the trail's
castles have both (the folder serves the SIEGE selection screen, which the trail
shares its castles with); the trail SCREEN pins no place for a painting - its own
art draws the castles as map labels - so the port carries the
text -> castle -> painting pairing as row data and draws none of them.

**What a trail mission would LOAD (measured, 2026-10-13, not assumed):** each
castle's own scenario is `maps/<Castle>.s2c` - the install's 19 `.s2c`
"historical castle" maps (`MapType.Historical`, `S2MReader`'s own file-header
type), and all ten of the trail's castles are among them (`Fougères.s2c` keeps
its accent; the mirror carries `maps/fougères.s2c`). The port's per-map
artifacts exist for **buildings** (`artifacts/buildings_<stem>.json`, 10/10) but
**not for entities** (`entities_<stem>.json`, 0/10 - the per-map entity dump has
never been run over the historical maps; 1 of the 19 `.s2c` maps has one,
`Fenis`), and the port's map loader places actors from that file - so starting a
trail mission is a later item, named rather than assumed.

**In-engine (2026-10-13):** `GameState.CONQUEST_TRAIL_SCREEN`,
`UI/ConquestScreenRow.cs` (the twelve trail rows and the four action rows),
`UI/ConquestScreenActions.cs` (THE one entry point) and `UI/ConquestScreenUI.cs`
(the screen: backdrop, the position's own progress picture at the measured rect,
the row's text in the art's own panel, the install's two arrow tiles on the tiles
the backdrop paints, the start row and the way back). The war screen's
`WARSCREEN_CONQUEST` row carries the screen (`Opens`), so its `Built` is derived
and that screen's unbuilt-row aggregate flips 5 -> 4 with it. The trail opens at
its FIRST castle (the port has no progress), pages all twelve rows, and wears the
install's own disabled rows at its two ends.

The port's own readings on this surface, marked as such (the UAT report
says each): **the start row's picture** - the trail's folder ships NO button art
(only the map, its eleven progress pictures and the two paging tiles), so the
start row wears the install's own castle-scenario play button
(`ui/siege_menu/button_play.bmp`, 266x57, from the folder the castles' art comes
from); **the start row's and the way back's place** - the art pins no slot for
either, so both stand in the panel's right-hand side, which the shipped picture
leaves empty (the trail's text takes the panel's left, 374 px of it); and **the
disabled arrows' look** - the install ships no disabled art, so an arrow at an
end of the trail still wears its own tile and its click names the shipped
disabled row rather than moving (this port records and names every click).

**THE SIEGE SELECTION SCREEN — the war screen's Siege destination, the install's
own castle list (2026-10-13).** Its table family is the screen's
four rows and, more importantly, THE CASTLE LIST:

| id | text | what it is on the screen |
| --- | --- | --- |
| `SIEGESELECTIONSCREEN_SETUP` | Setup | the row that continues to the siege setup behind the selected castle |
| `SIEGESELECTIONSCREENSTATICTOOLTIP_SETUP` | Continue to the Siege | that row's own tooltip content |
| `SIEGESELECTIONSCREENSTATICTOOLTIP_SIEGENAME` | Select this Siege | the tooltip a castle in the list shows |
| `SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT` | Previous Menu | the screen's OWN way back (see below) |
| `HISTORICCASTLE00..43` | 'Leeds', 'Spread across three islands...' .. 'Castell y Bere', 'Built on a steep rocky spur...' | the CASTLE LIST: an EVEN-indexed NAME row and its odd-indexed DESCRIPTION prose, 22 castles |

**This screen ships its OWN exit row and its own exit art** (unlike the Conquest
Trail, which has neither and wears the war screen's row and the frontend's fancy
button): `SIEGESELECTIONSCREENSTATICTOOLTIP_EXIT` 'Previous Menu' - note the
capital M, where the war screen's own `WARSCREENSTATICTOOLTIP_EXIT` says
'Previous menu' - and `ui/siege_menu/button_exit.tga` (58x58, a compass-rose
medallion), with `button_exit_highlight.tga` beside it.

**The 22 castles, in the table's own order** (read from the file, not assumed):
Leeds, Harlech, Koblenz-Stolzenfels, Haut-Koenigsbourg, Edinburgh, Javier,
Guadamur, Wartburg, Tower of London, Warwick, Criccieth, Windsor, Hastings,
Heuneburg, Caerphilly, Marksburg, Meersburg, Fougères, Fenis, Bodiam, Pembroke,
Castell y Bere. **The table's spelling and the FOLDER's spelling differ for four
of them**, so the port carries the file key it measured rather than deriving it:
Koblenz-Stolzenfels -> `castle_stolzenfels_painting.jpg` (the file is named for
the castle, not the town), Tower of London -> `castle_toweroflondon_painting.jpg`,
Fougères -> `castle_fougeres_painting.jpg` (the file drops the accent), Castell y
Bere -> `castle_castellybere_painting.jpg`. A mechanical rule (lowercase, drop
accents, drop everything that is not a letter or digit) reproduces THREE of those
four and misses only Koblenz-Stolzenfels; the harness re-runs the rule and says
so.

**The art is a COMPLETE folder, `ui/siege_menu/`** (2026-10-13, all sizes
measured): `siege_menu_background.{png,jpg,tga}` (the .png and its .jpg twin are
1024x768 - mean channel difference 1.6, JPEG noise - and the .tga is 939x657 with
alpha ~128-159 over 92 % of it, a semi-transparent variant this port does not
draw), `castle_<key>_painting.jpg` **22x, 425x313** (each with its country's own
location inset baked into its bottom-left corner), `layout_<key>.jpg` **22x,
146x163**, `siege_painting.bmp` (425x313) and `siege_layout.bmp` (144x161) - the
BLANK PLATES at the two picture slots' own sizes - plus `siege_map.bmp` (144x144,
a compass rose), `siege_location.bmp` (82x90, a map inset), `siege_textbox.tga`
(363x167), `ivy.tga` (248x238), `button_play.bmp` (266x57, the screen's one
button picture, with `button_play_highlight.bmp`) and the four pieces of the
screen BEHIND it: `siege_menu_choices_preview_build.jpg` (1024x768, the setup
screen as a composed picture - two scene insets over a bar) and
`choice_attack_{off,on}.jpg` / `choice_defend_{off,on}.jpg` (363x407 each). The
table names that screen's rows too:
`SIEGESETUPSCREENSTATICTOOLTIP_ATTACK` 'Command the assault of the castle' /
`_DEFEND` 'Take charge of the castle garrison' / `_EXIT` 'Previous Menu'.

**The backdrop is the screen's own composed picture and it PINS FOUR RECTS**,
measured off the gold frame lines it paints (all four carried in `UIAssets`):
the painting's frame is drawn at rows 58 and 373 (x 63..490) and columns 63 and
490 (y 58..373), so its opening is **x 64..489, y 59..372 = 426x314** - one pixel
more than the paintings in each direction; the layout's frame is at columns
498/643 (y 58..221), so its opening is **x 499..642, y 59..220 = 144x162** (the
146x163 `layout_*.jpg` pictures overhang it by one pixel each way, and the blank
plate `siege_layout.bmp` is 144x161); the description panel's opening is
**x 70..609, y 385..639** (its frame rows 382..384 / 640..642, its inner right
gold line at column 610); and the castle list's panel opening is
**x 708..947, y 62..639** (frame rows 59..61 / 640..642, inner gold lines at
columns 707 and 947) - 240x578, which holds the 22 name rows at ~26 px pitch.

**What a siege would LOAD (measured, not assumed):** each castle's own scenario
is `maps/<Castle>.s2c` - the install's 19 `.s2c` historical castle maps - and
**18 of the 22** castles have one: the four without are **Leeds, Caerphilly,
Meersburg, Bodiam** (all four are listed and painted by the screen's own data, so
the port says the scenario is missing rather than inventing one), and the 19th
map, `maps/Miskolc-Diosgyor.s2c`, has no castle row and no painting. The map
spelling is the install's own: `Fougères.s2c` keeps its accent, `Castell Y
Bere.s2c` capitalises the article the table's name row spells 'Castell y Bere',
`Koblenz - Stolzenfels.s2c` keeps the spaces round its hyphen. The port's
per-map artifacts: `buildings_<stem>.json` exists for the historical maps, the
`entities_<stem>.json` the port's loader needs for actors does not (1 of 19,
`Fenis`) - so starting a siege is a later item, exactly as starting a trail
mission is.

**In-engine (2026-10-13):** `GameState.SIEGE_SCREEN`,
`UI/SiegeScreenRow.cs` (the castle row and the action row),
`UI/SiegeScreenActions.cs` (THE one entry point, and the 22-castle list) and
`UI/SiegeScreenUI.cs` (the screen: backdrop, the selected castle's painting and
layout picture in the two slots it leaves, its name and description in the panel
along the bottom, the 22 name rows down the right-hand panel, and the two
buttons). The war screen's `WARSCREEN_SIEGE` row carries the screen (`Opens`), so
its `Built` is derived and that screen's unbuilt-row aggregate flips 4 -> 3 with
it. The screen opens at the table's FIRST castle (the port has no progress and no
save data).

The port's own readings on this surface, marked as such (the UAT report
says each): **the two buttons' PLACE** - the backdrop paints no slot for either -
so both stand in the free brown band below the description panel (the shipped
picture leaves it empty), `button_play.bmp` (266x57) at its left and the
compass-rose medallion (58x58) at its right; **the way back's WORDS** - the
shipped row is four words and the medallion is 58 px across, so the label stands
UNDER the medallion; and **which of the folder's plates this screen draws** - the
backdrop paints two picture slots and two panels, so the folder's other blank
plates (`siege_map.bmp`, `siege_location.bmp`, `siege_textbox.tga`,
`ivy.tga`) are carried and named rather than placed anywhere the install does not
pin.

**THE MAP REQUEST SCREEN — the war screen's Custom destination (2026-10-13).
This one CORRECTED the port's earlier reading, and the correction is
exe-measured.** An earlier reading had `WARSCREEN_CUSTOM` as
opening the scenario list (`MAINSCENARIOSCREEN_HELP_CREATE_NEW_SCENARIO` /
`_LOAD_SCENARIO`); the shipped engine says otherwise, and it can be read out of
`Stronghold2.exe` (the method: the exe's string pool holds the screens' own NAME
strings - 'map request screen', 'siege selection screen', 'main scenario
editor', 'load scenario', ... - and each screen class's code pushes the name of
the screen its buttons open; the war screen's own handler carries the Custom
row's picture `warpeace_custom.jpg` into the screen it then opens,
'map request screen'). The `MAINSCENARIOSCREEN*` / `MAINSCENARIOEDITOR_SCREEN*`
rows live in the engine's `MainScenarioEditorScreen` ('main scenario editor') -
the MAP EDITOR's own list (behind the main menu's unbuilt `MAINMENU_EDITOR` row),
whose own code scans `*.s2m`/`*.s2c` for it - not behind the war screen's Custom.

**What the engine class `MapRequestScreen` is made of** (its screen name is the
string 'map request screen'; its code loads both backgrounds as
`ui/map request bg` -> `ui/map_select/mapselect_background.jpg` and
`ui/map request war bg` -> `ui/map_select/mapselect_background_war.jpg` - the
peace variant has the monk and the woman, the war variant the barbarian and the
knight, and the war one is the war screen's Custom path): the two 1024x768
pictures paint the big empty frame at the top left (opening x 244..492,
y 42..308 - the engine's map view), the frame beside it (x 505..790, y 33..317)
and the panel along the bottom (outer frame x 237..790, y 320..727) with its
brown title strip (y 329..355), its list area and its scrollbar groove, plus the
two green arrow medallions the art itself paints (left x 53..99, right
x 925..971, both y 672..719). **The folder's own `_big`/`_small` variants measure
the map windows inside the right-hand frame**: `mapselect_background_big.png` is
transparent at x 525..780, y 47..302 (256x256) and `..._small.png` at x 526..651,
y 176..300 (126x125) - the war jpg paints the small one black - and
`mapselect_small_map_mask.png` (257x257, its lower-left 130x129 cut) is the mask
the engine clips a small view with. **The three 256x256 bmps beside them are the
screen's three MODES** - `mapselect_custom.bmp` (a castle at night),
`mapselect_freebuild.bmp` (a jouster) and `mapselect_load.bmp` (a peasant
carrying stones), registered by the exe as `texture/ui/custom`, `/freebuild`,
`/load` - so the screen the war path opens is the Custom one, and 256x256 is
exactly the `_big` window a mode picture fills.

**Its list is the install's own maps, and the FILTER is measured**: the screen's
code compares candidate names against the three literals `war_chapter`,
`peace_chapter` and `tutorial` (read out of the exe), so the campaign's chapters
and the tutorial maps are not in it; **34 maps remain** of the install's *.s2m
(`maps/` has 81 directory entries, 48 distinct names once the mirror's
case-alias twins are grouped - the port keeps the spelling that carries capitals,
the install's own file). The table's one row for this screen is
`MAPREQUESTSCREEN_PLAY` 'Play'; the shipped game's own list would also include
the player's own maps (the user's `\Stronghold 2\Maps\` folder, the exe's own
template), which this port does not have.

**The port's own readings on this surface** (each named on the UAT report):
the two painted arrows' WORDS and meaning - the table ships NO row for either, so
the port draws no words (the painted arrow is the picture) and its left/right
arrows page the list one page back/on, with the ends said in the port's own
plain wording; the **Play row's PICTURE and PLACE** - `ui/map_select/` ships no
button art at all, so it wears the install's own castle-scenario play button
`ui/siege_menu/button_play.bmp` (266x57) in the free band between the medallions;
the **way back's row and place** - the screen ships no exit row, so it wears the
war screen's own `WARSCREENSTATICTOOLTIP_EXIT` 'Previous menu' on the frontend's
fancy button (the trail's own deal), beside the Play row; and **the big black
frame at the top left** (the engine's map view) plus the small window in the
right-hand frame are carried as the art paints them - this port has no
scenario-view renderer, so it fills the right-hand frame with the mode's own
picture (the size the `_big` variant pins) and leaves the map view empty, both
named.

**What a custom game would LOAD is measured too**: 34/34 of these maps have the
port's buildings artifact, **0/34** their entities one (the same gap the trail's
missions and the sieges have), so the Play row's click names the selected map and
the gap rather than starting anything.

**THE LOAD SCREEN — the war screen's Load destination (2026-10-13).** The
engine class is `LoadScenarioScreen` (its screen
name string is 'load scenario') and its table family is `LOADSCENARIOSCREEN_*`:

| id | text | what it is on the screen |
| --- | --- | --- |
| `LOADSCENARIOSCREEN_HEADER_LOAD` | Load | the screen's header in the LOAD mode - the mode the war screen's row opens |
| `LOADSCENARIOSCREEN_HEADER_SAVE` | Save | the same header in the SAVE mode (the in-game menu's own entry), which this port names and does not build |
| `LOADSCENARIOSCREEN_NAME` / `_DATE` / `_PLAYERS` | Name / Date / P | the list's three column headers - each one's click the sort the table names beside it |
| `LOADSCENARIOSCREEN_LOAD` / `_CANCEL` | Load / Cancel | the screen's two rows: the way on and the way back |
| `LOADSCENARIO_STATICTOOLTIP_FILENAMESORT` / `_DATESORT` / `_PLAYERNUMBERSORT` | Sort by Name / Sort by Date / Sort by Number of Players | the three sorts the column headers apply |
| `LOADSCENARIO_STATICTOOLTIP_CANCEL` | Cancel | the Cancel row's own tooltip content |

**Its backdrop is pinned by the class's own code**: `texture/loadscenario
background` -> `ui/ingame_menus/scenario_loadscreen.jpg` (1024x768 - the
barbarian and the knight flanking a big framed panel whose brown title strip
carries the header, with a scrollbar groove along the right and two dark-red
button plates along the bottom; the same class serves the in-game menu's
save/load, which is why its picture lives in the ingame_menus folder). Measured
off its own gold lines: the panel's frame is x 214..801, y 105..662, the title
strip y 113..139, the list area y 140..658 (scrollbar groove around x 758..790),
the two plates x 221..463 and x 553..791, both y 686..728.

**The three `loadscreen_*.jpg` in `ui/frontend/` are NOT this screen's, and that
was measured too**: `loadscreen_2_name.jpg` is the PLAYER NAME screen's
background (the engine's `PlayerNameScreen` - its code builds 'Enter Your Name'
over that picture's empty frame, whose default text is the shipped
'Sir Stronghold'), `loadscreen_3.jpg` is referenced by the options/in-game
screens' code, and `loadscreen_1.jpg` is referenced by NOTHING in the exe at
all. The install's `ui/frontend/` art is the frontend's own, but a screen's art
is whatever its class's code loads - not what its filename suggests - which is
exactly the trap the earlier hand-off warned about.

**The list is the player's own saved games** - the shipped game keeps them in
the user's Documents folder (`\Stronghold 2\Saves\`, the folder template read out
of the exe) - and **this port ships none and has no save reader, so the screen is
built complete and its list is EMPTY**. The table ships no empty-state row
(measured: no `LOADSCENARIOSCREEN` row describes one), so the port draws none and
says the state on the console and in the report instead; the port's own readings
are the column headers' place (the list area's top row) and the two rows' plates
(the art's own painted plates, with transparent buttons and the shipped labels
over them). Its Cancel row is the way back to the WAR SCREEN.

## The scenario model the game's own editor authors — triggers, actions, missions, markers (2026-10-12)

**Where the model comes from.** The shipped maps were authored in the game's own
editor, and the shipped manual documents it: `extras/manual/Stronghold 2 Manual
English.pdf` **§8 "THE CAMPAIGN EDITOR" (pp. 53-56)** — the *Scenario Editor*
makes "Campaign or Skirmish" scenarios, "a campaign map allows you to create a
series of missions, which are linked to create a campaign chapter", events are
created "in the events section of the Scenario Editor", and the *Map Editor*
("the main window consists of the date box, mini-map, mission panel and the
player placement icons") terraforms, places buildings/walls/troops/military
units, marks out estates and carries **Keep Placement**, **Village Placement**
and **Invasion Marker** — "a marker where scheduled invasions (**created in the
events section of the Scenario Editor**) enter the map". "Scenarios are saved to
the maps folder in your Documents\Stronghold 2 folder. The save maps ends with
the extension **.s2m**." The editor's own screens are in the string table
(`MAINSCENARIOSCREEN_HELP_CREATE_NEW_SCENARIO` 'Create a New Scenario',
`SCENARIOEDITORSCREEN_ADD_NEW_MISSION` 'Add New Mission' / `_MAKE_SKIRMISH`
'Kingmaker' / `_MAKE_FREEBUILD` 'Free Build', `MISSIONEDITORSCREEN_BUILDING_
AVAILABILITY` / `_START_GOODS` / `_TRADER`) and its art in `ui/scenario_editor/`
(`background_scenario_editor.jpg`, `background_mission_editor.jpg`, the two
button pairs) with `ui/frontend/scenario_editor_screen.jpg` and
`ui/ineditorui.tga`. The shipped help file and the manual's reference sections
document the GAME, not the editor - the manual's chapter 8 is the only place the
authoring model is written down - and the string table's `ACTION_*` (81) and
`TRIGGER_*` (102) rows are that model's vocabulary.

**The model, as it is serialized** (every shipped .s2m; the port reads it with
`ScenarioReader` / `MissionReader` / `ScenarioEventReader`):

    MapHeader segment
      Scenario          starting year+month, 16 estate owners, 16 estate
                        production/population entries, 16 AI-placement bytes
        Mission x N     the editor's mission panels: Building Availability
                        (per building: Disabled / Enabled / Requires 1..3
                        QUESTS), Trade Availability (per good), Starting Gold,
                        Starting Popularity, Starting Resources
          ScenarioEvent x M
                        delay, "+ Months" length, action repeat count/delay
            TriggerObjectIds  (0..n triggers, ANDed)
            ActionObjectId    (exactly 1 action - or 0 for "no action")

**What the maps say** (instrument: `DevHarness/S2MScenarioRun`; manifests:
`manifests/scenario_missions.csv`, `scenario_events.csv`, `scenario_coverage.csv`,
`scenario_vocabulary.csv`; report `artifacts/sh2_scenario_report_2026-10-12.txt`):

- The mirror the port resolves through holds **81 .s2m files for 48 distinct
  maps** - 33 shipped mixed-case names sit beside a lowercase alias symlink each -
  and the install has **51**, of which three (`modKill Barclay.s2m`,
  `modpeace_chapter1.s2m`, `modwar_chapter8.s2m`, written Oct 10-11, months after
  the shipped files) are NOT mirrored; the harness names them rather than
  pretending the mirror is the whole shipped set.
- **81 Scenarios / 101 Missions / 760 ScenarioEvents** (668 events over 67
  missions once the case-duplicated names are folded) - and the census agrees with
  the independent python parse (`tools/s2m_parse.py`), which is the harness's own
  cross-check.
- **Every map carries the editor's model**, skirmish maps included: the one-event
  skirmishes (`gold 500-2000, popularity 80, 105/105 buildings, 29/29 trade
  goods`), `Kill Barclay` (23 events), `Viking Island` (22), `Hawk Rescue` (15),
  `Swords for the King` (11), `tutorial` (2), and the two campaigns:
  `war_chapter1..12` (11/43/8/71/40/75/20/76/17/10/25/18 events, 484 triggers and
  404 actions in all) and `peace_chapter1` (**12 missions, 145 events** - the Sim
  campaign's ladder: the missions' own Building Availability grows 5/105 → 9 → 13
  → 22 → 34 → 35 → 38 → 46 → 51 → 65 → 90 enabled, with
  `SingleThicknessWall:Requires3Quests` the first quest-gated entry).
- **The wiring is legible per event**, e.g. war_chapter8's mission 560123112:
  `LordDiesTrigger -> LoseAction`, `SpecificEnemyLordDiesTrigger[Lord=TheKing] ->
  LoseAction`, `AlwaysTrigger -> SetAlliesAction[Olaf=Friend, LordBarclay=Enemy,
  ...]`, `AlwaysTrigger -> InvasionAction[InvasionPointFlagColor=Green,
  InvasionPointFlagNumber=0, TargetPointFlagColor=Green, TargetPointFlagNumber=1,
  Lord=LordBarclay, TargetLord=SirWilliam, ArmyType=1]`,
  `NoMessagesPlayingTrigger + CustomTriggerWilliamReachKeep -> WinAction` (the
  win condition), `PercentTroopsKilledTrigger[Percentage=40/50] ->
  OverlordMessageAction[Lord=Olaf, SoundFileId=311..318]` (the narration voices),
  `RescueLordTrigger[Lord=SirWilliam] -> ...`, `AlwaysTrigger ->
  MaxOutPeasasntsAction[Lord=...]`. **The marker join is live:** an
  `InvasionAction` names its entry point and target as estate-flag colour+number,
  which is the editor's Invasion Marker placed in the Map Editor's Estates panel
  (the port's `EstateMarkers` layer, and the "InvasionMarkers" census leftover -
  9 instances - is that same marker family as a BuildingLayer type).
- **16 events carry no trigger at all** (an unconditional action) and **4 carry no
  action** (id 0) - both counted by the harness, neither a failure.

**The editor's own words for the data** (`tools/scenario_vocabulary.py`): the
table's 183 `TRIGGER_*`/`ACTION_*` rows are joined to the 96 object types the maps
use - **89 pair up** (78 by spelling, 3 by WORD SET, 8 curated by hand) and 7 have
no row the tool could pair (5 bespoke `Custom*` objects, `SomeEnemiesCloseToKeep`
/ `NoMessagesPlayingTrigger`). Three shipped quirks the join documents:
**ids with a space inside** (`TRIGGER_COMPLETE QUEST` 'Current Quest Status',
`TRIGGER_SINGLE_COMPLETE QUEST`, `TRIGGER_NOT_COMPLETE QUEST`), **ids whose words
are in another order** than the class name (`TRIGGER_PERCENT_TROOP_KILLED` for
`PercentTroopsKilledTrigger`), and the class-side typo `MaxOutPeasasntsAction`
(the row is `ACTION_MAX_PEASANTS` 'Give Lord Full Peasants').

**The decode gap (updated 2026-10-12).** The port's
readers now decode **40 of 44 trigger types (841 of 855 instances) and all 52
action types (756 of 756)**. **Four trigger types - 14 instances - remain
`RawS2Object`**, each with its payload measured but its field meanings not
established by the shipped data:

| type | instances | the editor's row | measured payload | why it stays raw |
| --- | --- | --- | --- | --- |
| `EnemyNearMarkerTrigger` | 4 | `TRIGGER_ENEMYNEARMARKER` 'Enemies are near a marker' (family row `_RADIUS` 'Radius') | three ints: `(15,3,3)` (war_ch1), `(5,3,0)` (war_ch12) | the family names a Radius parameter but not which int carries it; the numbers match no flag on either map (their flags are 0..1 and 0..3), and while a position-plus-radius reading fits the sea-corner cells both payloads point at, two maps cannot settle the field order |
| `SomeEnemiesCloseToKeepTrigger` | 7 | - (no row the join could pair) | one int: 1 (war_ch5), 4 (war_ch4), 6 (war_ch6), 7 (war_ch12) | one value per map with no label; count and radius readings both fit |
| `LiftSiegeTrigger` | 2 | `TRIGGER_LIFTSIEGE` 'Lift Siege' (family row `_express` 'Rescue Lords Castle') | one int: 9 in both instances (war_ch4) | two identical instances in one map - nothing varies to measure against |
| `EnemyTroopsOnWallsTrigger` | 1 | `TRIGGER_ENEMYTROOPSONWALLS` 'Get Troops onto Enemies Walls' (family rows `_express`, `_express_enemy` 'Enemy') | one int: 5 (war_ch3) | a single shipped instance |

**The ranked work list it was** (the 2026-10-12 scenario dump ranked the gap
below; every type in it except the four above was decoded in the follow-up pass
- the layouts are the subsection after this table):

| type | instances | the editor's row | text |
| --- | --- | --- | --- |
| `AfterBriefingTrigger` | 41 | `TRIGGER_AFTER_BRIEFING` | After Briefing |
| `QuestCompleteTrigger` | 37 | `TRIGGER_COMPLETE QUEST` | Current Quest Status |
| `SingleQuestCompleteTrigger` | 27 | `TRIGGER_SINGLE_COMPLETE QUEST` | Single Quest Complete |
| `QuestNotCompleteTrigger` | 13 | `TRIGGER_NOT_COMPLETE QUEST` | Quest Not Complete |
| `ConstructedBuildingCompleteTrigger` | 12 | `TRIGGER_CONSTRUCTED_BUILDING_COMPLETE` | Building Site Complete |
| `ConstructedBuildingPercentCompleteTrigger` | 5 | `TRIGGER_CONSTRUCTED_BUILDING_PERCENT_COMPLETE` | Building Site Percent Complete |
| `ControlNumEstatesTrigger` | 5 | `TRIGGER_CONTROL_NUM_ESTATES` | Control a number of Estates |
| `NoWolvesOnMapTrigger` | 5 | `TRIGGER_NO_WOLVES_ON_MAP` | No Wolves Left on the Map |
| `NumQuestsCompleteTrigger` | 4 | `TRIGGER_NUM_QUESTS_COMPLETE` | Number of Quests Complete |
| `EnemyNearMarkerTrigger` | 4 | `TRIGGER_ENEMYNEARMARKER` | Enemies are near a marker |
| `NoBearsOnMapTrigger` | 4 | `TRIGGER_NO_BEARS_ON_MAP` | No Bears Left on the Map |
| `SomeEnemiesCloseToKeepTrigger` | 7 | - | (no row the join could pair) |
| `LiftSiegeTrigger` | 2 | `TRIGGER_LIFTSIEGE` | Lift Siege |
| `CustomTriggerWilliamReachKeep` | 2 | - | (bespoke; the port reads this one already) |
| `CustomTriggerTakeEstate6` | 2 | - | (bespoke, per map) |
| `CustomChapter1Trigger` | 1 | `TRIGGER_CUSTOM_CHAPTER1` | Fight through the guards and reach the boat |
| `EnemyTroopsOnWallsTrigger` | 1 | `TRIGGER_ENEMYTROOPSONWALLS` | Get Troops onto Enemies Walls |
| `CustomActionPlayCutScene` | 5 | - | (bespoke; the cutscene folder's own business) |
| `GongInvasionAction` | 2 | `ACTION_GONG_INVASION` | Gong Infestation |
| `TakeEnemysCastleAction` | 1 | `ACTION_TAKE_ENEMYS_CASTLE` | Take Enemy's Castle |
| `CustomActionStopWolfMessages` | 1 | - | (bespoke, per map) |

**The fields the readers establish (2026-10-12).** Every
reader below consumes exactly its measured payload and throws rather than read
past it (the dispatcher falls back to `RawS2Object` per object). The value sets
are measured over the install's own 51 `.s2m`, the three `mod*` maps included;
the declared payload byte count is 0, 3 or a multiple of 4 as noted:

- **The six EMPTY types** - `AfterBriefingTrigger` (41 instances),
  `NoWolvesOnMapTrigger`, `NoBearsOnMapTrigger`, `CustomChapter1Trigger`,
  `CustomTriggerTakeEstate6` and the action `CustomActionStopWolfMessages` -
  carry no payload at all: the serialized form is the header, the data payload
  marker with the instance index, and the trailer (25-byte triggers, 17-byte
  actions). The two "no animals left" triggers are the editor's own rows; the
  other four are bespoke entries.
- **`QuestCompleteTrigger`** (the row is `TRIGGER_COMPLETE QUEST` 'Current Quest
  Status') - the payload is exactly THREE bytes, one flag per quest slot
  (`Quest0Complete`, `Quest1Complete`, `Quest2Complete`; the game's UI labels the
  slots Quest 1..3, `MISSIONOBJECTIVESSCREEN_QUEST_1..3`). All seven non-zero
  patterns appear across the shipped maps and the all-zero one does not;
  war_chapter6's win condition is the all-set pattern, and the invasion
  schedules of war_chapter4/6 gate pairs of quests at +2 months and single
  quests at +3 - the table reads as "the flagged quests are complete" (the
  runtime's exact AND semantics are the game's own, not the authoring data's).
- **`SingleQuestCompleteTrigger`** - `Quest`, the 0-based quest slot: 0, 1, 2.
  Cross-check: war_chapter4's three cutscene events pair quest 0/1/2 with
  `CustomActionPlayCutScene` ids 0/1/2.
- **`QuestNotCompleteTrigger`** - `Quest`: 0 and 2. war_chapter6 ANDs it with
  `SingleQuestCompleteTrigger` to fire "one quest done, another not".
- **`NumQuestsCompleteTrigger`** - `NumQuests`: 1 and 2 (the editor's 'Number of
  Quests Complete'); each instance gates a reward or a rule change.
- **`ControlNumEstatesTrigger`** - `NumEstates`: 2, 3, 6, 9 (the editor's 'Control
  a number of Estates'; family row `_buy` 'Buy a Number of Estates').
- **`ConstructedBuildingCompleteTrigger` / `ConstructedBuildingPercentCompleteTrigger`**
  - the same two-int shape. The leading int is 0 in all 24 shipped instances of
  both types; it is read and dropped rather than named (the editor's per-site
  rows - 'Build Beacon', 'Rebuild the Ruined Bridge', ... - do not show up as its
  values, so how a site is recorded is not this field). `Percentage` is the
  second: -1 in every complete-trigger instance (the complete sentinel beside the
  percent sibling's real percents) and 20 / 65 in the percent trigger.
- **`CustomActionPlayCutScene`** - `CutSceneId`: 0..6. It does NOT index the
  shipped `cutscenes/` folder by file name (war_chapter6's id 4 sits beside four
  `warMission6_*.csc` files, and war_chapter4's 0/1/2 beside one
  `warMission4_01.csc`), so which table it resolves through is an open question.
- **`GongInvasionAction`** - `EstatePointFlagColor` (Blue, Red) +
  `EstatePointFlagNumber` (2) + `NumGongPiles` (8, 3): the table's own two
  parameters, 'Estate Marked By Flag' and 'Number of Gong Piles', with the flag
  read as the port's usual color-then-number pair.
- **`TakeEnemysCastleAction`** - `Lord`: 7 = Edwin in the one shipped instance,
  cross-checked against war_chapter6's Scenario ownership (Edwin holds six of the
  map's eight estates) and read with the sibling `TakeEnemyCastleAction`'s
  numbering. The class-name difference is real: `TakeEnemyCastleAction` (no `s`)
  is a distinct type with no shipped instances.

**What the editor does NOT explain** (named so nothing later assumes it):
the RUNTIME semantics of a trigger - evaluation order, reset, and what an empty
trigger list means - are the game's own (`Stronghold2.exe`), not the
authoring data; **the mission's briefing is the game's own IN-GAME screen, not a
frontend screen and not authored by the maps** (measured 2026-10-12):
its rows are `MISSIONOBJECTIVESSCREEN*` ('Objective',
'Hints and Tips', 'Tutorial', `QUEST_1..3`, Win/Lose/Complete, and the two play
buttons that date it - `MISSIONOBJECTIVESSCREENSTATICTOOLTIP_PLAY` 'To the Game'
for the start briefing and `_RETURN_TO_GAME` 'Return to the Game' for the in-game
review the manual's "View Mission Briefing" button opens), its art is
`ui/briefing_screen/` (`briefing_background.jpg`, the hint/objectives/tutorial
buttons, 1024x768) plus the per-chapter `ui/chapter_introductions/
chapterNN_intro.jpg` (all 12 shipped, the same 1024x768), its hints are the
shipped per-mission `WARHINT<chapter-1>_0..7` / `PEACEHINT<mission>_0..7` rows
(chapter 1's name the boat, its objective), its titles are
`WAR_CAMPAIGN_HEADING00..11` ('The Search for the King' .. 'To Capture a King'),
and **the maps author no start-of-mission briefing action**: the two shipped
`EnterBriefingAction` instances are both mid-mission in war_chapter6 (each fired
by `SingleQuestCompleteTrigger+NoMessagesPlayingTrigger`, i.e. the screen
re-entered when a quest completes), while `TRIGGER_AFTER_BRIEFING`'s 41 instances
(war_chapter4/6) presuppose a briefing the maps do not author - so entering it at
mission start is the runtime's own behaviour. What the objective TEXT's source is
(the mission's own data carries none) is not yet located; and the three `mod*`
maps are **not** editor re-saves:
against their originals the file header and the SCENARIO segment are byte-identical
and only the S2Game segment differs, by exactly **17 two-byte values (34 bytes of
10.6 MB)** - a targeted edit of runtime-world data, authored by something other
than the editor's save path (nothing in this port wrote them).

## Stronghold2.exe as a data source — REFERENCE (RTTI class map + runtime object offsets), exe-derived 2026-10-09

**Everything in this section is exe-derived third-party observation, not
file-format data.** It is recorded here because the pipeline already reads the
exe (the 104 `.bdg` literals above, the exe model lists below) and because
an exe phase is a planned next step that this material de-risks. Disciplinary
rules:
label it exe-derived wherever it is cited, never paste decompiled code, and
re-derive before depending on any of it.

- **The binary (our copy, `sh2-game-root/stronghold2.exe`):** file version
  **1.5.0.0**, linker timestamp 2020-04-24, image base `0x400000`, 14,208,000
  bytes, 5 sections — `.text` RVA `0x1000` (0x4ff000), `.rdata` `0x500000`
  (0x16b000), `.data` `0x66b000` (0x614c00), `.rsrc` `0x26d1000`,
  `.reloc` `0x273c000`. **No PDB / RSDS / symbol path** — the exe is stripped,
  so all names below come from embedded RTTI.
- **Full MSVC RTTI is present — this is the engine's class map.**
  **1,612 type-descriptor strings, 955 of them plain class descriptors**:
  `Stronghold2` 775, `GlowWorm` 98, `Dragonfly` 50, plus std/GameSpy/boost
  leftovers. Finding method: scan the image for `.?AV<class>@<ns>::@` strings;
  the RTTI-complete-object-locator → vtable walk (what the community patch uses
  to place hooks) then names every virtual function's class. Consequences for
  this repo:
  - **Our `.s2m` entity block-chains are literally the engine's C++ class
    names**: `Actor > S2Actor > Soldier > Archer` = `.?AVActor@GlowWorm@@`,
    `.?AVS2Actor@Stronghold2@@`, `.?AVSoldier@Stronghold2@@`,
    `.?AVArcher@Stronghold2@@` (base-first, own class last — already our
    reading, now confirmed from the other side).
  - Every class behind an open question is named and therefore targetable:
    `TroopClan`, `S2Clan`, `BoxFormation` (+`Column`/`Line`/`Grid`/`Knot`),
    `Estate`, `EstateLayer`, `ClaimLayer`, `HeightLayer`, `BuildingLayer`,
    `StockPile`, `Lord`, `S2Governor`, `Player`, `Scenario`, `GameRules`.
  - **The Lord AI / behaviour system is named**: 159 `*Chore` classes plus
    `AIAttackActorChore`, `AIAttackAreaChore`, `AIAttackBuildingChore`,
    `AIAttackWoodenWallChore`, `AICBuilder`, `AIFillInMoatChore`,
    `AIMakeSiegeEquipmentChore`, `AIMakeWeaponChore`, `AIMoveTroopsChore`,
    `AIRetreatChore`, `AIStartSiegeChore`, `AIUpdateSiegeState`,
    `AIUpdateStance`, `OpenFieldController`, `LordsRules` and the 36 `*Clan`
    classes (`DefensiveBlockClan`, `SiegeMissileClan`, `HarassClan`, …). A
    behaviour-tree-style chore system, not a monolithic AI.
- **Byte-verified hook sites (see §6.8 for the full byte dumps):** unit spawn
  hook RVA `0x0EE3BE` = `80 B9 79 01 00 00 00` (`cmp byte ptr [ecx+0x179], 0`),
  with `EDI` = unit type ID and `ESI` = `player_base + 0x674` at the site; the
  mp-ai patch site `0x2A0F69` = two 7-byte `mov byte [esi+0xD28],0` /
  `mov byte [esi+0x1790],0`; the three unit-cap sites `0x16827/0x18768/0x189FB`
  hold `add eax, 0x12C`, i.e. **the shipped build's unit cap is 300**.
- **Unit type IDs (third source for our `entity_types.csv` decode):** the
  spawn hook's type IDs match our manifest's `type_id` **27/27 with zero
  conflicts**, and add the display names we lacked — `ArmedPeasant` = "Peasant"
  (49), `BoatWarrior` = "Pictish Boat Warrior" (76). Their two IDs absent from
  our placed-entity manifest, `Assassin` (72) and `Thief` (79), are recruitable
  only, which is why they never appear in a shipped map's placements.
- **Player-object layout (three sources agree):** see §6.8 — colour `+0x04`,
  honour `+0x1C`, gold float `+0x1010`, popularity float `+0x1028`, troops
  `+0xD8C`, siege `+0xD90`, title `+0xF58`, castle state `+0x10F8`, the 8
  honour sources at `+0x1714`, income at `+0x1554/+0x1558/+0x1628`, army
  sub-object `+0x674`, and the stockpile block at `+0xF60`. **Do not use the
  static player pointer** — the three sources give `0x6E8BD8`, `0x6E7C60` and
  `0x6E6C08` and it cannot be settled statically (all in `.data`, zeroed at
  load).
- **Sources:** `others/sh2-unofficial-patch/` (B1naryVision, Unlicense —
  `src/patches/endgameStats/gameOffsets.h`, `units.h`, `unitTracker.cpp`,
  `docs/features/endgame-stats.md`), Ald0s/Stronghold2-Trainer (weakest — its
  damage-hook RVA `0x385380` is mid-loop code, not a function entry), and a
  Cheat Engine table (`~/Downloads/Stronghold 2 V1.5 Steam -F.CT`) which
  carries the fullest resource list (kitchen goods, max beds, spawn rate).

---

Per-format TODOs are inline above.
When a format's status changes, update the entry here in the same change.
