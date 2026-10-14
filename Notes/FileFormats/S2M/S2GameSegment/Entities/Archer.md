# Archer — decoded 2026-10-08 (payload layout: [Entity](./Entity.md))

The workhorse type and the one that gave the layout away.

* **Population**: war_chapter1 67, war_chapter8 170; 4,137 over 33 of the 67
  maps (the largest actor population after the melee infantry).
* **Payload**: 825 B on ch1 / 967 B on ch8 (one per map is 31 B longer = the
  first occurrence, which carries the nested class names).
* **Class chain**: `Actor > S2Actor > Soldier > Archer` (12,119 of 12,256
  actors share the Soldier level).
* **Type id** (payload +8): 19.
* **Header constants**: +45/+49 = 3000.0 / 3000.0, +92 = 4.9333 (98 of the 169
  ch8 archers; 65 store 15.65) — unverified.
* **Standing surfaces (all 67 maps)**: terrain 2,034 / wall-walk 1,080 /
  structure-deck 963 / primary grid 60 — archers are the second-most garrisoned
  type after the melee troops.
* **Estates**: 9 distinct ids (1..10); war_chapter1's 67 archers are all estate
  3, standing on estate-1 land (an invasion), which is why `on_own_estate` is
  false for them.

The named first occurrence (war_chapter1, the first archer in the stream):

```
A5C1FFFF 43 "Actor" | 44 "S2Actor" | 45 "Soldier" | 41 (= Archer)
```
