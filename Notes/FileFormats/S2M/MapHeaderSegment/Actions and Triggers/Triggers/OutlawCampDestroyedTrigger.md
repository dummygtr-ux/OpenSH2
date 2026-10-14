This trigger is strange as it seems to have some sort of configuration settings; but none that can be modified in the map editor.

- +4 bytes = unknown (Map editor always sets it as 00 00 00 00)

**Variant note (2026-10-08, verified on all 67 shipped maps):** [VariantPayloads](./VariantPayloads.md) in `Actions and Triggers/` records the byte counts and variants the shipped maps actually use.

The 4 bytes below are the action/trigger registration index (not always 0: 15, 19 and 42 observed), and it is the object's last field — the trailer follows immediately.
