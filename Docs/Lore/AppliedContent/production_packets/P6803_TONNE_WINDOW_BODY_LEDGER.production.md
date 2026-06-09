# P6803 TONNE WINDOW BODY LEDGER Production Source

Release set: `RS285_TONNE_WINDOW_BODY_LEDGER`
Packet: `P6803_TONNE_WINDOW_BODY_LEDGER`
Hash: `0xEBF9C2E3` / `3959014115`

## Purpose

Make the tonne-window rule player-visible as behavior, not lore trivia. `P6803` explains why Black Keel can count a living Marauder, proof core, sample canister, salvage crate and witness tag as competing recovery mass lines.

## Runtime/Data Path

Authoring source lives in:
- `Docs/Lore/AppliedContent/packets/RS285_TONNE_WINDOW_BODY_LEDGER.packets.json`
- `Docs/Lore/AppliedContent/articles/RS285_TONNE_WINDOW_BODY_LEDGER/*_external_site.md`

Generated targets:
- `Docs/Lore/AppliedContent/external_site/<locale>/P6803_TONNE_WINDOW_BODY_LEDGER.md`
- `Docs/Lore/AppliedContent/in_game_wiki/<locale>/P6803_TONNE_WINDOW_BODY_LEDGER.md`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`

Runtime owner remains the baked DataMonolith/packet hash bridge. Runtime must not read JSON or markdown.

## World Binding Targets

- `poi.keelmark_body_ledger`
- `poi.bent_ascent_ring_mass_stencil`
- `poi.blank_quarantine_berth_receipt`
- `poi.operator_to_payload_suit_tag`
- `poi.black_keel_window_timer`

## Failure Cases To Preserve

- A packet receipt must not imply body recovery.
- A living body must not silently override proof, sample, salvage or quarantine payload rules.
- Missing receiver or quarantine berth should remain visible as a blocked recovery line, not generic refusal.
- Repeated unlock should be idempotent by packet hash; duplicate world objects should point to the same packet id.

