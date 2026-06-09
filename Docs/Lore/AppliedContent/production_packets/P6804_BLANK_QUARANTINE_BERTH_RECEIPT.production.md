# P6804 BLANK QUARANTINE BERTH RECEIPT Production Source

Release set: `RS286_BLANK_QUARANTINE_BERTH_RECEIPT`
Packet: `P6804_BLANK_QUARANTINE_BERTH_RECEIPT`
Hash: `0x7289D30C` / `1921635084`

## Purpose

Turn the missing quarantine berth into a concrete runtime-facing artifact. `P6804` follows the tonne-window body ledger: Black Keel has heard the line, priced the body and asked for receiver custody, but the quarantine berth field is still blank.

## Runtime/Data Path

Authoring source lives in:
- `Docs/Lore/AppliedContent/packets/RS286_BLANK_QUARANTINE_BERTH_RECEIPT.packets.json`
- `Docs/Lore/AppliedContent/articles/RS286_BLANK_QUARANTINE_BERTH_RECEIPT/*_external_site.md`

Generated targets:
- `Docs/Lore/AppliedContent/external_site/<locale>/P6804_BLANK_QUARANTINE_BERTH_RECEIPT.md`
- `Docs/Lore/AppliedContent/in_game_wiki/<locale>/P6804_BLANK_QUARANTINE_BERTH_RECEIPT.md`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`

Runtime owner remains the baked DataMonolith/packet hash bridge. Runtime must not read JSON or markdown.

## World Binding Targets

- `poi.blank_quarantine_berth_receipt`
- `poi.receiver_berth_null_stamp`
- `poi.keelmark_quarantine_slot_printer`
- `poi.black_keel_window_timer`
- `poi.return_rights_handshake_console`

## Failure Cases To Preserve

- A packet receipt plus live body line must not imply lift approval.
- A blank berth is a visible blocked state, not missing UI text and not silent no-data.
- Repeated scans should resolve to the same packet hash and unlock id.
- Duplicate receipt props should not create duplicate lore state; they should point to the same packet id.
- If receiver custody is missing after save/load or scene reload, the receipt should still communicate `berth blank` rather than degrading into generic refusal.
- If localization for a locale is not native-reviewed, the baked row may remain draft status, but the runtime should still have scanner, terminal, audio, wiki and site strings.

