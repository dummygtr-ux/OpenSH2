# HorseCart — decoded 2026-10-08 (payload layout: [Entity](./Entity.md))

* **Population**: war_chapter8 9, 60 objects over 9 of the 67 maps.
* **Payload**: 636 B (ch8).
* **Class chain**: `Actor > S2Actor > <HorseCart>` — **no Soldier level**, which
  is how the chain distinguishes the carts from troops. (Oxen and the two
  builder carts, `CarterCart`/`BailiffCart`, share that shorter chain.)
* **Type id** (payload +8): 2.
* **Standing surface**: terrain (57 of 60).
* Estate ids are ordinary (1..16 as on the rest of the map) — carts belong to
  the estate that owns them.
