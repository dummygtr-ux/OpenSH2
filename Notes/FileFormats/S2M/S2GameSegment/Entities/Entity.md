# Entities (Actor hierarchy) — DECODED 2026-10-08

Every unit/creature/cart/boat on a map is an **actor object**: its payload is a
100-byte actor header followed by nested `A5 C1 FF FF` blocks that spell the
class hierarchy. Decoded on war_chapter1 + war_chapter8, then swept over all 67
shipped maps with `sh2-port/tools/entity_parse.py`.

```
entity_parse.py MAP [OUT.json]         every actor on one map + the checks
entity_parse.py --check MAPDIR         the resolve-every-entity criterion
entity_parse.py --sweep MAPDIR --csv DIR
                                       -> manifests/entity_types.csv +
                                          manifests/entity_estates.csv
entity_parse.py --estates MAPDIR --csv FILE
                                       the estate-id evidence (-> manifests/estate_join.csv)
```

## 1. Actor header (payload offsets; `HEADER_LEN = 100`)

| off | type | meaning | evidence |
|---|---|---|---|
| +0 | i32 | 0 | constant on every actor |
| +4 | i32 | 59 — the "placed object" class marker | 8 = S2Estate, 22 = ScriptAction/GateTeleport, 67 = TroopClan |
| +8 | i32 | **the game's own type id** | Archer 19, Crossbowman 52, Pikeman 51, Spearman 28, Swordsman 27, Maceman 50, Ballista 70, Knight 71, Ladderman 29, HorseCart 2, Oxen 14, Builder's cart (CarterCart) 86 … (47 actor types, `entity_types.csv`) |
| +12 | i32 | 2 | constant on every actor |
| +16 | i32 | world id | per object (ch1 1040834, ch8 930125 …) |
| +20 | u8 | 1 | constant |
| +21 | u8 | **standing-surface class**: 0 ground/water level, 1 raised wall-walk, 2 structure deck | agrees with the HeightLayer plane join on **12,245/12,256** entities — see §3 |
| +22 | u8 | cellX | |
| +23 | u8 | cellZ | |
| +24 | u8 | 0 | |
| +25 | u16 | cellX | the cell is written **twice** (u8 pair + u16 pair), the same convention the NaturalStone scenery records use |
| +27 | u16 | cellZ | |
| +29 | f32 | **x** (raw; /1024 = cells) | the position triple; 1 cell = 2 m |
| +33 | f32 | **y** = the raw height of the surface the unit stands on | decoded 2026-10-08, re-verified here per entity |
| +37 | f32 | **z** (raw; /1024 = cells) | |
| +41 | f32 | 0 | 0 on all 12,256 actors — not a facing (the yaw is §1a) |
| +45/+49 | f32 | **HIT POINTS** (current/max) | one per-(map,type) value written twice — the pair is written a third and fourth time in the Actor block, §1b |
| +53, +57 | i32 | 0 | |
| **+61** | i32 | **ESTATE ID** — the state field, see §4 | |
| +65 | u16 | a per-object RANDOM | inside MSVC rand()'s 0..32767 on all 12,256 (uniform, mean 16464) — **not** a fraction, §1b |
| +67 | u16 | 10000 | constant on all 12,256 entities |
| +69 | u16 | 0 | constant on all 12,256 entities |
| +71 | u16 | 25 | constant on all 12,256 entities — a default, not a per-type stat |
| +75 | u32 | per-type CLASSIFICATION | 3 troops/most animals, 1 siege vehicles, 18 Heron, 28 Dolphin, 173 Ladderman, 179 Engineer, 255 HorseCart (Trebuchet 1/4), §1b |
| +83 | f32 | 1.0 | a scale |
| +91 | u8 | 1 | |
| +92 | f32 | per-(map,type) STAT | every observed value an integer multiple of 1/150; 0 on all wildlife and mounts, §1b |
| +100 | — | first `A5 C1 FF FF` block marker | on EVERY actor of every map |

The grid is identical for troops, siege engines, carts, oxen, boats, animals
and lords — the class difference lives only in the blocks below.

## 1a. THE ROTATION (decoded 2026-10-09)

The yaw is an **f32 in degrees** that lives in the **first nested (Actor)
block's data**, 44 bytes in:

```
data+ 0   i32   0
data+ 4   f32   x          \  the actor's own position, written twice
data+ 8   f32   z          /  (the format's usual duplication)
data+12   f32   x
data+16   f32   z
data+20..32  f32 0, 0, 0, 0, 0
data+40   f32   1.0        (a scale)
data+44   f32   ROT  <-- the facing, degrees (the buildings' rot convention)
…         zeros …
data+83   f32   ROT  <-- the same value again, 39 bytes later
```

Two things make this easy to get wrong:

1. **The field must be read BLOCK-RELATIVE.** The class name is written only
   at a class's *first use* in the segment, which shifts the block's data start
   (and everything after it) by `4 + nameLen` bytes. 55 of the 12,256 actors
   carry a name; on those, a fixed `payload+160` read returns 0 and silently
   loses the facing (it happened to 2 Wolf / 1 Cat / 1 FireBallista instances —
   a FireBallista with a real 180° read back as 0).
2. **A whole-payload variance scan finds nothing first.** The header is 100
   bytes and the blocks are mostly zeros, so the facing only shows up once the
   payloads are compared *per type, per block-signature group* (block lengths
   vary by map: ch8's archers carry 287/300-byte middle blocks vs ch1's
   162/162).

Values over all 67 maps (12,256 actors) — the same degree convention the
buildings use at `+152`:

| value set | who |
|---|---|
| exactly 180° | Catapult 57/57, Trebuchet 8/8, Mantlet 49/49, BurningCart 13/13, FireBallista 61/61, Cat 9/9 |
| {0, 90, 180, 270} | HorseCart (0:47, 90:4, 180:1, 270:8), CarterCart 270:1 |
| 45/100/160/340 (packs of 14) | WarHound 56 |
| arbitrary angles (19 distinct) | Wolf 39 |
| 0 everywhere | **every troop type in every shipped map** — Archer 4,137, Spearman 2,698, Maceman 974, Swordsman 892, Pikeman 757, Ladderman 473, Crossbowman 510, Ballista 31, BatteringRam 15, Mangonel 11, Oxen 27, Lord 30, boats, all other wildlife |

So the shipped maps only rotate **props, carts and stationary siege
equipment**; troops are placed at the editor's default 0° (the game orients
them at runtime when they get orders). The wall-garrison cross-check therefore
reads: ch1's 82 wall-walk units and ch8's 149 all store 0 — the castles'
garrisons do NOT carry wall-facing in the map data, they inherit the default.
That is the data's answer, not a decode miss: a field that is 0 on all 12,256
records cannot be a per-unit facing.

Evidence that it IS the yaw (not a scale/speed):

* quarter-turn exactly on the axis-aligned props, free angles only on the
  wandering wildlife — the same split the buildings' rot shows;
* the two copies at data+44 and data+83 agree on **12,256/12,256** actors, and
  a correlation scan over every block-relative offset of all 12,256 payloads
  finds **no other field** that tracks the value (nothing even at r > 0.9);
* formations line up with it: of 897 local same-type clusters that share one
  facing, 37 % run *along* the facing (a column) and 18 % across it (a rank),
  against ~11 % expected for random orientation — e.g. Fenis's 9 mantlets all
  at 180° in a 3×3 block whose longer axis is 90° (a rank), Pembroke's 4
  mantlets at 180° in a column, ch8's fire-ballista pairs 3 cells apart on the
  same line;
* in-engine A/B: one mantlet model placed at rot 0 and at rot 180 shows its
  asymmetric mesh offset (+0.41 m on local z) flipping to −0.41 m — the
  rotation reaches the geometry, not just a transform field.

## 1b. THE HEADER CONSTANTS (decoded 2026-10-09)

Swept over all 67 maps / 12,256 actors, **per type AND per block-signature
group** (the same method the facing took: a whole-payload scan finds
nothing because the interesting fields are per-type constants).

| off | type | value set (all 12,256 actors) | reading |
|---|---|---|---|
| +45/+49 | f32 | per-(map,type); 100 (Rabbit, Heron) … 25000 (Lord) | **HIT POINTS** — one value written twice (a current/max pair, always equal here) |
| +65 | u16 | uniform 0..32767, mean 16464, 7,288 distinct | a per-object **random** (MSVC `rand()`'s range) |
| +67 | u16 | 10000 on every actor | a constant |
| +69 | u16 | 0 on every actor | a constant |
| +71 | u16 | 25 on every actor | a constant — a default, not a per-type stat |
| +75 | u32 | {1, 3, 4, 18, 28, 173, 179, 255} | a per-type **classification** |
| +83 | f32 | 1.0 | a scale |
| +91 | u8 | 1 | |
| +92 | f32 | per-(map,type); always a multiple of 1/150; 0 on wildlife | a per-type **stat** |

### The hit points (+45/+49)

* The two copies are equal on **12,256/12,256** actors — the format's usual
  "value written twice" habit (the same as the cell, the position, the
  estate id and the rotation), so +45/+49 is ONE field.
* That field is **real actor state, not a coincidence**: the Actor
  base-class block repeats the pair. The offset inside the block is not
  fixed — **data+127/+131** on the usual 184/196-byte blocks (11,948
  actors), **+247/+251** on the four 304-byte blocks and **+367/+371** on
  the single 424-byte one (the Kill Barclay actors whose data+111 points at
  an InterruptAction) — so the check locates the pair *by value* (two
  adjacent f32 copies of the header's value). Present on **11,953/11,953**
  actors whose value is non-zero; the 303 zero ones (Whale, Dolphin,
  HorseArcherHorse, LightCavalryHorse, CarterCart) carry zeros in the
  pair's slot, so the pair is either written as 0 or skipped — the bytes
  cannot tell.
* Per-type ranking and the five per-map splits:

  | value | types |
  |---|---|
  | 25000 | Lord |
  | 20000 | Knight |
  | 15000 | Bear |
  | 10000 | Pikeman, Swordsman, BatteringRam (13/15), SiegeTowerLarge/Small |
  | 8000 | Berserker, WarriorMonk (31/40) |
  | 6000 | Maceman, Crossbowman, Monk, Cat (7/9) |
  | 5000 | BatteringRam (2/15), Cat (2/9) |
  | 4000 | HorseArcher, Outlaw, LightCavalry, Ladderman (105/473), WarriorMonk (9/40) |
  | 3000 | Archer, Spearman, AxeThrower, BoatWarrior, Ladderman (368/473), Mantlet |
  | 2000 | Engineer |
  | 1200 | Wolf |
  | 1000 | siege engines, WarHound, ArmedPeasant (57/163), BailiffCart |
  | 500 | ArmedPeasant (106/163), HorseCart, Oxen, BurningCart |
  | 200 | Deer, Dog, Fox, Sheep |
  | 100 | Rabbit, Heron |
  | 0 | Whale, Dolphin, HorseArcherHorse, LightCavalryHorse, CarterCart |

  The five split types get their value **per map** (ArmedPeasant 500 on the
  castle maps, 1000 on hawk rescue + war_chapter3; WarriorMonk 4000 on
  Edinburgh, 8000 elsewhere; …) with the same value on every instance of a
  map — not per instance, not per estate, not per side, and not per
  subtype (the header's type id and the class chain are identical on both
  sides of every split). Unresolved: what the map author scaled.

### +65 is a random, not a fraction

The earlier reading ("a per-instance fraction scaled by 10⁴, with +67 =
10000") is wrong: +65 exceeds 10000 on 7,000+ actors and its distribution
over 0..32767 is uniform (min 0, max 32761, mean 16464, 7,288 distinct over
12,256) — exactly **MSVC `rand()`'s range**, the same convention the
forest records' `i32 random` field uses. +67 = 10000 is its own constant
field next to it.

### +75, the classification

Per-type u32, constant inside every (map, type) group — the only
within-type split in the whole corpus is Trebuchet (1 on 4 instances, 4 on
the other 4, exactly matching the +92 split of those instances):

| +75 | types |
|---|---|
| 3 | every troop type, most animals (Bear, Deer, Dog, Fox, Sheep, Rabbit, WarHound, Wolf, Whale), Ballista, Mangonel, carts, mounts, BailiffCart |
| 1 | BatteringRam, BurningCart, Catapult, FireBallista, Mantlet, SiegeTowerLarge/Small, Cat, Trebuchet (4/8) |
| 4 | Trebuchet (4/8) |
| 18 | Heron |
| 28 | Dolphin |
| 173 | Ladderman |
| 179 | Engineer |
| 255 | HorseCart |

The families are capability classes (siege vehicle / climbing / carrying /
cart / flyer / swimmer) but the *numbering* is not derivable from the
files; the table above is the pinned value set.

### +92, the per-type stat

Per-(map,type) f32 — uniform inside a (map, type) group on 431/491 groups
and inside a (cluster, type) formation on 849/853 (the 4 mixed clusters
are documented outliers). Every observed value is an integer multiple of
**1/150**, and all wildlife plus all mounts store 0. The dominant value per
type (Spearman/Pikeman/Outlaw 23.2, Archer/Swordsman/Crossbowman/
BoatWarrior/Engineer/WarriorMonk 4.9333, AxeThrower/Heron 6.6667,
Knight 17.5, LightCavalry 0.6333, siege 1.0 …) does not match any speed,
armour or range ordering available to us, so the *name* stays open.

### What is NOT verifiable from the shipped data

The names of the +75 classification and the +92 stat — and the
current/max reading of the hit-point pair — are structural readings. The
install ships **no unit-stat table**: the values (3000.0, 4.9333, 25, 3 …)
appear nowhere else, not as float bytes, not as adjacent integer pairs and
not as instructions in `Stronghold2.exe`, not in `gamerules.dat` (two
objects: GameRules, LordsRules), not in the `.uni`/`.aml` defs, the
`.lodsettings.xml` files or the manual PDF, and the exe carries no
serializer field-name metadata (the RTTI strings are class names only).
Recorded so no one re-hunts for one: the exe phase
(§7.5) is the only way to name these fields.

## 2. The nested blocks and the class hierarchy

```
marker A5C1FFFF + {i32 classId} + [ {i32 nameLen}{name} — first use of the class ] + {i32 A}{i32 B} + fields
```

The class id is a **segment type-registry index**, so it is *not stable across
maps*: `Actor` is 43 on 51 maps and 44 on four; `Soldier` is 45 or 46; `Archer`
is 41 on war_chapter1 but 51 on Warwick.s2c. The **names are stable**, and
because the segment writes a class's name at its first use, a map-local
`classId -> name` table can be rebuilt from the payloads alone
(`entity_parse.class_names`). That is what the tool keys on — no hard-coded
type list, no hard-coded id.

The named first occurrence of each map is a full example of the mechanism: the
first Archer reads

```
A5C1FFFF 43 "Actor"   | 44 "S2Actor" | 45 "Soldier" | 41 (= Archer)
```

and the first TowerSquare (a building) reads

```
A5C1FFFF 58 "Building" | 59 "S2Building" | 60 (= Tower)
```

Base classes come first, the object's own class last — and the own class is
**never named in the nested stream** (the object itself already registered it
in the top-level stream), which is why the chain's last element is filled in
from the object's type name.

Chains actually observed (`entity_types.csv` `block_chain`):

| chain | for |
|---|---|
| `Actor > S2Actor > Soldier > <type>` | Archer, Crossbowman, Pikeman, Spearman, Swordsman, Maceman, Ladderman, Knight, ArmedPeasant, … (12,119 payloads) |
| `Actor > S2Actor > SiegeEngine > Ballista / FireBallista` | Ballista, FireBallista |
| `Actor > S2Actor > Lord` | Lord |
| `Actor > S2Actor > <type>` | HorseCart, Oxen, CarterCart, BailiffCart (137 payloads — no Soldier level) |
| `Building > S2Building > <type>` | every building (CourtHouse 82, Dungeon 83, Hovel 95 …) |

**Actor detection** = the payload's first nested block is the map's `Actor`
class. `A`/`B` are the two i32s between the id and the fields (A = 2 on Actor,
1 on Building, 0 otherwise; B looks like a declared data length — 172/162/28 on
the Archer's chain) — **semantics unverified**, nothing in this decode depends
on them.

## 3. Standing plane (independent re-derivation)

`+33` is the height of the surface the unit stands on (decoded 2026-10-08 as the
cross-segment proof that plane 2 = structure deck, plane 1 = wall-walk, plane 0
= terrain). The tool re-derives it per entity from the same map's HeightLayer
corners (corner slot 2 = the cell's own (x, z) corner; plane 0 is sampled
bilinearly over the cell's four corners because a unit at the cell centre on a
slope is up to half the cell's rise off the corner value).

Measured over all 67 maps / 12,256 entities:

* **12,206/12,256 (99.6 %)** stand within 0.3 m of one of the three planes or
  the primary grid;
* the 50 residuals are units 0.3–0.9 m off the sampled surface (mid-slope
  positions on the historic castle maps; listed in the sweep output);
* **the +21 flag agrees with the plane join on 12,245/12,256**, where the
  comparison maps "plane 1 at or below the water line" to ground on both sides
  (41 coastal cells where plane 1 holds the water surface, not a wall top):
  `flag 2 ↔ plane 2` 1,247/1,247; `flag 1 ↔ plane 1` 1,630/1,671; `flag 0 ↔
  plane 0 or the primary grid` 9,098/9,098 + the primary-grid matches.

Two independent fields (a u8 in the header and the HeightLayer planes) agree on
99.9 % of 12k entities — the surface classes are real game state, not our
sampling.

## 4. STATE FIELD: the estate id (+61)

`i32 @ payload+61` is the **estate that owns the object**, an id in the same
space as the EstateLayer's per-cell grid. Evidence:

1. **The join.** The `EstateLayer` object is `40 B header + 65,536 u8` — one
   estate id per cell (war_chapter1: constant 1; war_chapter8: 11 distinct;
   Kill Barclay: 12 distinct incl. 15 and 16). Joining every placed object's
   +61 against the grid at its own cell (`entity_parse.py --estates`, all 67
   maps, 14,160 objects): **every one of the 1,895 structures/props whose +61
   is non-zero stands on land its own id names — 0 exceptions.** A unit
   matching = standing on its own estate's land; a unit not matching = standing
   on someone else's, i.e. an invader (8,161 of 11,870 units — the campaign
   maps are invasions).
2. **The "no estate" class.** Objects with +61 == 0 are exactly the ones that
   are not owned: the wildlife (Rabbit 71, Deer 66, WarHound 56, Dolphin 49,
   Wolf 39, Bear 31, Whale 28, Heron 23, Fox 23 …) and the **crops** — the crop
   survey's own negative ("the only small field is payload byte 61; every
   Wheat/Hop crop has 0") is the same field, now explained.
3. **It is written twice.** On war_chapter8 the value also appears inside the
   S2Actor block (payload +341 = that block's data +25); both readings agree.
4. **It clusters spatially.** Units of one id form one region (ch8: id 9 at
   x 53..105 / z 162..204, id 7 at x 198..217 / z 24..50, id 4 at x 25..80 /
   z 22..57), and the estate's own buildings carry the same id as the troops
   standing there (CourtHouse/Dungeon/TowerLookout of a site share it).
5. **The id space matches the EstateLayer's** exactly (1..16 across the maps),
   and the `S2Estate` object (31 per map, `+8` = its index 0..30) is the
   matching per-estate record.

## 5. Populations and checks (all 67 maps)

* 12,256 actor objects in **47 types** over 67 maps (7 maps — freebuild
  starters — have no actors at all).
* Cell frame: **12,256/12,256** — the u8 cell pair, the u16 cell pair and
  `trunc(position/1024)` agree everywhere.
* Resolved to {type, x, z, standing plane, estate}: **12,256/12,256**.
* War-chapter numbers: ch1 166 entities in 8 types (67 Archer, 47 Pikeman,
  20 Swordsman, 20 Crossbowman, 6 Spearman, 3 Ballista, 1 FireBallista, 2 Lord);
  ch8 340 in 14 types (170 Archer, 62 Pikeman, 25 Swordsman, 25 Crossbowman,
  24 Spearman, 9 HorseCart, 5 Ballista, 5 FireBallista, 3 Oxen, 2 Mangonel,
  2 Lord, 3 Maceman, 2 Sheep, 3 Dog).
* ch1's 161 units all carry estate 3 (the attack force) while the map's land is
  all estate 1 → `on_own_estate` false, which is the point of the field.
* Largest per-type populations: Archer 4,137 (33 maps), Spearman 2,698,
  Maceman 974, Swordsman 892, Pikeman 757, Ladderman 473, Crossbowman 510.
* Manifests: `manifests/entity_types.csv` (per type: type id, objects, maps,
  payload lengths, surface classes, estate distribution, standing planes,
  on-own-estate count, cell-frame count, class chain),
  `manifests/entity_estates.csv` (per map), `manifests/estate_join.csv`
  (the §4 evidence per map).

## 6. Open

* `A`/`B` of each block header (2/172 on Actor), and the block's trailing
  fixed fields — the declared `B` is smaller than the block's actual extent
  on the Actor and S2Actor blocks (12 and 150 bytes respectively on ch1
  archers), so `B` is either a partial length (as with the action payloads)
  or a field count. Nothing depends on it yet.
* Header offsets +41/+45/+49 (0 / 3000.0 / 3000.0), +65 (the fraction),
  +71/+75, +91/+92: per-type constants that look like gameplay values
  (health/armour/speed?), all unverified. **+41 is NOT the rotation** — it is 0
  on all 12,256 actors, and +92 is a per-type constant (archer 4.93, ballista
  29.27) that varies within a type on 2-3 values.
* ~~Header offsets +41/+45/+49 (0 / 3000.0 / 3000.0), +65 (the fraction),
  +71/+75, +91/+92~~ → **DECODED 2026-10-09 (§1b)**:
  +45/+49 are the hit points (one value written twice, repeated in the
  Actor block), +65 is a per-object `rand()`-range random (NOT a fraction —
  it exceeds 10000 on 7,000+ actors), +67/+69/+71 are the constants
  10000/0/25, +75 is a per-type classification (3 troops, 1 siege, 18
  Heron, 28 Dolphin, 173 Ladderman, 179 Engineer, 255 HorseCart) and +92 a
  per-(map,type) stat that is always a 1/150 multiple. The *names* of +75
  and +92 stay unverified — the install ships no stat table and the exe no
  field-name metadata, so only the exe phase can name them (§1b).
* The Actor block's per-instance region at data+60..79 (two sequential
  ordinals at +67/+69 plus a 4-byte value at +76..79 that tracks the
  instance) is undecoded; it is engine-side registration data, like the
  cluster member records' handles below.
* No actor *animation* is modelled yet: the render path (`.osh2mesh`, static
  meshes) is the next step, then audio. **The facing itself is DECODED
  (§1a) and applied in `EntityPlacementLoader`.**

# 7. THE CLAN FAMILY — TroopClan / *Clan, Cluster, ActorGroup (decoded 2026-10-09)

The formations are three cooperating object types. Swept over all 67 maps with
`entity_parse.py --clans` / `--clans-sweep`; manifests
`sh2-port/manifests/clan_census.csv` (per map) and `clan_fields.csv`
(per clan/cluster object).

## 7.1 Clan objects — 1,143 over 67 maps

Stream types seen: **TroopClan 715**, RabbitClan 71, DeerClan 66, SeaGullClan
66, WolfClan 56, OutlawCampDefensiveClan 46, CrowClan 38,
OutlawCampAggressiveClan 36, PheasantClan 33, SheepClan 4, plus **12 objects
whose stream type name the object walk garbles** (war_chapter11 — the known
walker-mismatch area). All of them share ONE payload layout,
so the decoder keys on the nested **S2Clan class id resolved from the segment's
own name table** (`class_names`), never on the type name — the naive
"any nested block with A=0, B=5" shape test also matches RippleEmitter /
KillingPit / SmokeEmitter / HorseArcherHorse payloads, because (0, 5) is just
another class's header pair.

```
clan payload (payload-relative):
  +0  i32 0
  +4  i32 class marker       63 on 643/715 TroopClans; 67/71/72/79/87/99/103
                              on the rest -- per-object, not per-class
  +8  i32 0
  +12 u16 index              1..N, sequential per segment
  +14 u16                    == +12 on the wildlife clans
  +16 i32                    == index+1 on the wildlife clans
  +20 i32 CLUSTER COUNT      1 or 2 (0 on the outlaw-camp clans)
  +24 i32 cluster object id #1
  +28 i32 cluster id #2      (0 when +20 == 1; a small non-id value on some
                              wildlife clans, e.g. 262148 = (4,4))
  +32.. per-type f32s        (-1.0 / 1.0 on the wildlife clans)
  then the nested A5C1FFFF blocks -- the same class-chain mechanism an actor
  uses (first use in the segment carries the name):
      S2Clan   A=0  B=5   with a 5-byte datum (00 + 4 varying bytes)
      <2nd>    A=0  B=<its data length>: 11 on troop clans, 28 on the
               wildlife clans, 37-45 on the outlaw-camp clans; data mostly 0
```

## 7.2 Cluster objects — 1,104 over 67 maps

```
cluster payload (payload-relative):
  +0  i32 declaredLen            == len - 4 on every cluster
  +4  u16 memberCount, u16 0
  +8  memberCount x {u16 a, u16 b}         member records, 4 B each
  +8+4m      u32 A               count of 4-byte waypoint records
  +12+4m     A x {u16 x, u16 z}
  +12+4m+4A  u32 B               count of 8-byte waypoint records
  +16+4m+4A  B x {u16 x_8.8, u16 z, u16 x, u16 z}
              -- each point written TWICE: x at 1/256-cell (raw/4)
                 resolution, then both axes as cells; b == d always and
                 a == c*256 +- 1 on 2,962/3,017 records
  +...       i32 OWNER           the object id of the owning clan
  +...       59-byte parameter tail: f32 x, f32 z (the cluster's own position
              in raw units), f32 256.0 (the map size in cells), f32 1.0, then
              zeros and the x/z pair again.  Verified on ch1's single-member
              cluster, where the tail's x/z are exactly its member's position.
  ...        178 clusters carry 13 trailing bytes
              (`00 ff fe ff ff ff ff ff 00 00 00 00 00` = a sentinel waypoint
              at x = 255, the map edge, with z = -1: "no target") and 56 carry
              one extra 8-byte waypoint record.  The remaining 870 end exactly
              at 79 + 4m + 4A + 8B bytes.

The two record sections are "the path written coarsely / finely": the troops
use the 8-byte form (B > 0, A = 0) and the wildlife clans the 4-byte form
(A > 0, B = 0) on the 133/3,017 split counted.
```

## 7.3 ActorGroup — 737 objects, an empty container

Exactly **11 per map on all 67 maps**, each an 8-byte payload
`{i32 4, i32 0}`. Nothing else lives there (checked 2026-10-09): the clusters
and clans are the real objects, the ActorGroups are placeholders.

## 7.4 THE MEMBER JOIN (the checkable claim)

Every ACTOR carries its formation in its own Actor (base-class) block, at
**data+111** — an i32 object id of a Cluster:

```
Actor-block data+111  ->  the Cluster object id the unit belongs to
                          (0 / absent = placed standalone)
```

It must be read **block-relatively** (the class name at a class's first use
shifts the data start — the same trap as the facing at data+44). Grouping the
actors by that value makes

* each cluster's `memberCount` field match its group size on **1,101/1,104**
  clusters, and
* each cluster's OWNER field resolve to a real clan payload on **1,101/1,104**
  (the 2 Three Valleys orphans have owner 0; 1 war_chapter11 reference dangles
  at 492298800, an id no object carries).

3,673 of the 12,256 actors belong to a formation (ch1 37, ch8 158, hawk rescue
289, Tower of London 277...). Three documented map-data outliers, listed in
`entity_parse.CLAN_OUTLIERS` (Kill Barclay: 3 clusters whose members carry no
back-reference and 5 actors whose data+111 points at an **InterruptAction**
object instead of a Cluster — so the field is a generic "linked object"
pointer; Three Valleys: 2 orphan clusters).

The join is independent of the member records' values, which stay
**UNVERIFIED**: they are engine-side registry handles, not positions — the
least-squares pair→(x, z) fit fails outright, and while the second u16 equals
the actor's 1-based ordinal in the map's actor list on ~51 % of clusters
(ch8 144/158 members at ordinal+1, Castell Y Bere 110/112), ch1's groups sit
at +45..+115 because the registry counts something the file does not show.

## 7.5 THE FORMATION SHAPE — BoxFormation (decoded 2026-10-09)

The formation family's missing object. A **BoxFormation** stream type does
exist in the shipped maps: **18 objects over 5 maps** (Fenis 1, Javier 5,
Windsor 8, hawk rescue 2, Koblenz 2) — found by scanning the segments for
the length-prefixed class name, not from the type census, because a map
that has formations does not necessarily have one of these.

```
BoxFormation payload (45 B):
  a5c1ffff {i32 classId = the SHAPE class} [{name} on first use]
           {i32 A = 0} {i32 B = 4} {i32 WIDTH}
  a5c1ffff {i32 classId = the object's own class}      <- empty
```

The shape class is **GridFormation** on all 18 (the segment's type registry
also knows `ColumnFormation`, unused in the shipped maps), and the 4-byte
datum is the **GRID WIDTH = members per row**. Pinned by the member layout,
not by name:

| map | width | members | fit |
|---|---|---|---|
| Fenis | 3 | 9 | 3×3 |
| Javier | 5 / 5 / 6 / 6 / 6 | 19 / 24 / 36 / 34 / 36 | ≤ width² |
| Windsor | 5 / 5 / 4 / 4 / 5 / 3 / 4 / 5 | 20 / 20 / 11 / … / 9 | ≤ width² |
| hawk rescue | 4 / 4 | 12 / 10 | 4×3 |
| Koblenz | 5 / 5 | 0 / 0 | editor leftovers |

all 18 formations fit `width*width` and their members fill a compact
~1-cell-spaced block rather than a line (Fenis's 9 sit in a 1.5×2.5-cell
box; Javier's 36 in 6.9×5.6 cells).

**The join:** a TroopClan payload carries the BoxFormation's **object id**
(at payload +52 or +56 depending on the clan's payload variant — located by
value, the ids are unique in the segment), so each formation is claimed by
exactly one clan. `entity_parse.py --clans` / `--clans-sweep` decode the
shapes and assert the grid fit (18/18 PASS); the join is in
`manifests/clan_fields.csv`.

The 12 war_chapter11 objects whose stream type name the walk garbles are
clans (they carry the S2Clan class block), and the wildlife clans
(DeerClan/RabbitClan/…) reference no shape — the 1,125 clans without a
BoxFormation are on the 62 maps that carry no formation object at all.

## 8. ACTOR ANIMATION (decoded 2026-10-10)

The maps carry no animation — it all lives in the install's unit mesh tree, and
it is reachable end to end **through the game's own `granny2.dll`** (2.6.0.14,
via the new `sh2-port/tools/gr2skel.exe` bridge). Three sources, one join:

```
meshes/units/<type>.uni          the actor's model + its animation-name list
meshes/units/AnimList.anl        the animation catalogue: name -> animation file
meshes/units/animations/<profile>/<file>.gr2    the clip (no mesh, one animation)
meshes/units/models/<type>.GR2   the model: mesh + SKELETON + bone bindings
```

### 8.1 The files

- **1,008 unique animation files** under `meshes/units/animations/` in five
  profiles: `soldier/` 259, `worker/` 473, `hero/` 168, `teenager/` 61,
  `child/` 47 (2,015 files on the case-aliased mirror; 970 distinct basenames).
- Names are `<profile>_<action>.gr2` — `archer_idle`, `soldier_attack_dagger`,
  `cs_pikeman_idle`, `mock chicken` (a file name with a SPACE really exists).
- **`meshes/units/AnimList.anl`** (202,896 B) = the catalogue. A 20-byte header
  `{4, 5, 600, 24, 47}` then **1,127 fixed 180-byte records** (then 16 `0xff`
  pad bytes), each = `{animation .gr2 file name}{animation name}` plus an `0xff`
  -filled numeric tail with `0 / -1 / -256` values. Parsed by
  `tools/animlist_parse.py` (`--csv`, `--resolve`); **1,119/1,127 file names
  resolve to a real animation file** and only one shipped file has no record
  (`soldier_charge.gr2`).
  The 8 unresolved records are **front-coded** (prefix-compressed) names whose
  shared prefix belongs to the previous record — completing them by hand gives
  `hornblower_idle`, `hornblower_walk`, `hero_walk`, `cs_pikeman_idle`,
  `cs_pikeman_standdown`, `cs_sword_to_throat_loop` (all real files); the
  eighth, `soldier_taunt_2.GR2`, is a catalogue entry with no shipped file.
- **`AnimList.txt`** at the install root (5,258 B, 411 lines / 349 distinct) is
  the engine's animation-**name vocabulary** — a subset of the catalogue's 896
  distinct names (321 shared, 28 names only in the .txt, 575 only in the .anl).
  The install root holds no `animlist.txt`; the mirror's lowercase one is an
  alias. The exe carries the literal `meshes\units\AnimList.anl` and the
  directory literals `meshes\units\animations\{hero,soldier,worker}\` — but **no
  animation file name at all**, so the engine builds the paths by convention
  (profile prefix + the catalogue's name), it never reads a file list.
- Each **`.uni` def file ends with its actor's animation-name list**
  (after the model + attachment names): `archer.uni` = ARCHER RUN, ARCHER IDLE,
  ARCHER IDLE 2, ARCHER WOOD TOWER, ARCHER CLIMB LADDER, SHOOT, SHOOT END,
  ATK DAGGER, ARCHER IDLE ATK — **10/10 of them are names in the catalogue**,
  and they resolve to `archer_run_with_bow`, `archer_idle`, `soldier_attack_dagger`,
  `archer_idle_attack_bow`, … (the `.uni` pool stores shared suffixes, so a raw
  scan of the tail returns fragments like "WOD TOWE" — complete them before use).

### 8.2 What the .gr2 files actually hold (granny2.dll, `gr2skel.exe`)

`GrannyFileInfo` counts (the same struct `gr2dump.c` uses, read past Meshes):

| file | meshes | skeletons | trackGroups | animations |
|---|---|---|---|---|
| `meshes/units/models/archer.GR2` | 1 | **1** | 0 | 0 |
| `meshes/units/models/archer_bow.GR2` (an attachment) | 1 | **1** | 0 | 0 |
| `meshes/units/animations/soldier/archer_shoot_arrow.gr2` | **0** | **0** | **1** | **1** |

So **the model file carries the skeleton and the clip file carries the animation**
— they are joined by bone NAME, never by index:

- **Model skeleton** — `granny_skeleton {char* Name; int BoneCount; granny_bone*
  Bones}`; the bones are an **inline array of 152-byte structs** (`{char* Name;
  int ParentIndex; …}`, the rest is the rest-pose transform + LOD data, still
  unread). The stride is *discovered* by `gr2skel.c` (a scan for a repeating
  `{string}{parent}` pattern), then every name is validated by asking the DLL:
  `GrannyFindBoneByName` returns a bone for **23/23** names and NULL for a bogus
  one (the negative control runs on all 47 probed models). archer = "Bip01",
  23 bones, a textbook 3ds Max biped (root → Pelvis → Spine → Spine1 → Neck →
  Head, L/R Clavicle→UpperArm→Forearm→Hand→Finger0, L/R Thigh→Calf→Foot→Toe0,
  plus Bip01 Prop1).
- **The mesh is skinned**: the mesh struct's `BoneBindingCount` is 21 on the
  archer (bones−2; the Prop/Nub bones carry no vertices) — see
  `manifests/actor_rigs.csv` for all 47 actor models. Per-vertex weights are NOT
  in the `PNT332` vertex type our static-mesh dumper uses; the DLL ships the
  `PWNG…` vertex types (bone indices + weights) and `GrannyCopyMeshVertices`, so
  the weights are reachable — that is the one part still to be implemented.
- **Rig families** (`actor_rigs.csv`): every humanoid is a `Bip01` biped, 23–38
  bones by type (Archer/Knight/Spearman 23, ArmedPeasant/Berserker/HorseArcher/
  Monk/Outlaw 25, Ladderman/Maceman/Pikeman/Swordsman/WarriorMonk 30,
  BoatWarrior/Engineer 31, Lord 27 — the Lord's own mesh declares a single bone
  binding, its weapons are separate attached meshes); animals use their own root
  (`BEAR` 24, `rabbit bip` 33, `FOX` 38, Wolf 46, `stag` 24, SHEEP 21, `bone01`
  Dolphin 8) and the siege engines/carts are rigid 1-bone `Box…`/`base`/`Bone…`
  trees.
- **Clip** — `granny_track_group {char* Name; …; int TrackCount (word 3);
  granny_transform_track* TransformTracks (word 4)}`, tracks in a **7-word
  (28-byte) array with the name at +0**; `granny_animation` = `{char* Name;
  real32 Duration; real32 TimeStep; …}` where **Name is the 3ds Max source path**
  (`Z:\Stronghold2\art\source\characters\GENERIC_ANIMS\attacks\shoot_bow.max`).
  On `archer_shoot_arrow.gr2`: **duration 1.6 s, timestep 1/60 s, 30 tracks** —
  the full biped rig (fingers + nubs) as 30 alphabetically-sorted names.
  Across the 75-clip sample (`manifests/anim_clips.csv`): durations 0.07–80 s,
  timesteps 1/60, 1/50 and 1/30, 1–40 tracks, and the track sets are all the
  shared `Bip01` vocabulary. **The clip rig is a SUPERSET of the model rig**
  (30 tracks vs the archer's 23 bones), which is why the join must be by name.
- **Curves**: each track's three curves are Granny `DaK32fC32f` ("Knots",
  "Controls", "Padding" — read from the curve-data header). Do not decode them by
  hand: the DLL exports `GrannyCurveGetKnotCount`, `GrannyEvaluateCurveAtT` /
  `GrannyEvaluateCurveAtKnotIndex`, and the sampler trio
  (`GrannyGetTrackSamplerFor` + `GetOrientation/Position/ScaleShearSamples`), and
  for the rest pose `GrannyBuildRestWorldPose` + `GrannyGetWorldPose4x4Array` +
  `GrannyNewLocalPose`. Sampling at the clip's own timestep is the plan.

### 8.3 THE FORMAT DECISION (§8 rules) — **extend the custom cache, not glTF**

Evidence for the choice, not preference:

1. The data is two kinds of file (model-with-skeleton, clip-without-mesh) and the
   clips are a **shared library** keyed by bone name — the same `.max` source
   (`bows_and_waves.max`, `child.max`, …) produces many `.gr2` files that drive
   every actor whose rig matches. glTF models this badly: an animation-only glTF
   needs a fake node hierarchy, and a per-model glTF would duplicate ~1,000 clips.
2. §8's runtime rule ("no prerendered assets in the editor, everything derives at
   runtime from the install") is already satisfied by the `.osh2mesh` cache, which
   is outside the repo and regenerable; a glTF step would add a second derived
   format plus an importer dependency (Unity ships no glTF importer).
3. The existing path is proven and upstream-compatible (`Granny2AssetConverter` +
   `Granny2MeshCacheReader`): all 46 actor models already render through it, so
   extending it is a version bump, not a rewrite.
4. Everything needed is already exposed by the game's own DLL (§8.2) — no format
   risk, only C-bridge work.

Plan (not implemented yet):

- `.osh2mesh` **v2**: keep the mesh, add a `SKELETON` section (bones: name,
  parent, rest transform) and per-vertex bone indices/weights (via a `PWNG…`
  vertex type), plus `bindposes`.
- new sibling cache kind **`.osh2anim`** (same `{basename}-{sha256(relpath.
  lower())[0:16]}` naming rule): duration, timestep, and per-bone sampled
  position/orientation/scale-shear curves.
- Unity side: `SkinnedMeshRenderer` + `Mesh.bindposes`/`boneWeights` and one
  `AnimationClip` per `.osh2anim` via `AnimationClip.SetCurve` on bone names,
  matching by name so a clip's extra bones (fingers/nubs) are simply ignored.
- `tools/gr2_to_gltf.py` stays the lab's *preview* tool (extend it later to emit
  skeletons so an extracted clip can be eyeballed in Blender — it is not the
  runtime path).
