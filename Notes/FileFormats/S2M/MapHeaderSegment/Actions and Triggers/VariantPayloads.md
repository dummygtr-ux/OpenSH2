# Action/trigger payload variants, and the .s2c "historical" map type

Verified 2026-10-08 against the real bytes of all 67 shipped map files
(48 `.s2m` + 19 `.s2c`) by an independent Python walk of every object stream, and
then in-engine by an `S2MFileLoader.Load` sweep over the same 67 files: **0 decode
failures**, where before the fix 19 maps aborted `Load` and 74 objects across 22
maps fell back to raw payloads. Before/after table:
`sh2-port/manifests/reader_decode_fails.csv`.

## The payload header (both actions and triggers)

`[Action](./Actions/Action.md)` / `[Trigger](./Triggers/Trigger.md)` describe the
marker and the optional `{unknown, byte-length}` pair correctly. Two details that
the maps enforce and the old readers got wrong:

- The **byte-length is authoritative** and is written only on objects that carry
  data. An empty-payload object puts its trailer straight after the 4-byte
  registration index. So presence must be decided from the bytes that remain
  (`Length - Position - 4 >= 8`), not from the type: `FireAction` carries data
  (`byte-length = 4`) while `OutlawCampDestroyedTrigger` is empty-shaped, and the
  old code assumed the opposite for both.
- For `InvasionAction` the byte-length covers only the *invasion point* section
  (136 bytes: flag colour, flag number, owning lord, unknown, 30 unit counts).
  The target section that follows is a second, separately sized block:
  `{i32 1}{i32 targetSectionBytes}` where `targetSectionBytes` is **19** on
  campaign maps and **18** on the older builder — the 19th byte is
  `AreInvasionWarningsEnabled`, absent when the section is 18 bytes. 20 of the 76
  shipped `InvasionAction` instances use the 18-byte form.

Per-type payload shapes of all shipped action/trigger objects (empty vs data,
and the declared byte length) were tabulated during the analysis; the only type
whose declared length is *not* the whole data section is `InvasionAction`, as
above. The four readers that were wrong are now driven by these byte counts.

## `Scenario` unknown block

`[Scenario.md](../Scenario.md)` records "+30 bytes = unknown and absolutely no
clue". Those 30 bytes are actually `{i32 n}{n zero bytes}{i32 3}{i32 84}{i32 0}`
— the reference map that produced the note stores `n = 14`, which is why the
fixed 30-byte skip fit it. The shipped maps use **n = 8, 9 or 14**:
`8`: Edinburgh, Harlech, hawk rescue, Mountain Hideaway, River Delta, Three
Valleys, Tropical Retreat, tutorial, Viking Island, Volcano, war_chapter1/2/12.
`9`: Bear Necessities, Swords for the King, The Four Woods, Tower of London,
war_chapter3/5/7/9/10/11, Warwick. `14`: everything else. A fixed skip therefore
ran past the payload on 24 maps and threw `EndOfStreamException`; the remaining
fields of the object (16 estate lords, 16 production types, 16 AI-placement
bytes, all readable as sane values) sit right after the block.

## `.s2c` maps: the `historical` file-header type

The 19 `.s2c` "historical castle" maps are ordinary 256×256 S2M files whose
file-header `type` string is **`historical`** (FileHeader.md lists only the other
four). Their MapHeader object also carries the kind as an int, and across all 67
files the two agree exactly:

| `type` string | MapHeader int | file count |
|---|---|---|
| kingmaker | 0 | 21 |
| historical | 1 | 19 |
| warcampaign | 2 | 15 |
| peacecampaign | 3 | 4 |
| freebuild | 4 | 8 |

`MapHeader.md` lists `04 = free build` and omits `01`; its `03 = peace custom`
and the missing freebuild entry are corrected by this table (the old
`MapHeaderReader` had a dead `mapType == 3` branch for FreeBuild).
