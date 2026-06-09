# P6805 RECEIVER COUNTERSTAMP ROOM NUMBER Production Source

Release set: `RS287_RECEIVER_COUNTERSTAMP_ROOM_NUMBER`
Packet: `P6805_RECEIVER_COUNTERSTAMP_ROOM_NUMBER`
Hash: `0x034E3FC6` / `55459782`

## Purpose

Show the first actionable way out of a blank quarantine berth state. `P6805` is the receiver counterstamp that writes a room number onto the previously blank berth receipt after proof pressure, braking debt and quarantine custody align enough to make somebody own the door.

## Runtime/Data Path

Authoring source lives in:
- `Docs/Lore/AppliedContent/packets/RS287_RECEIVER_COUNTERSTAMP_ROOM_NUMBER.packets.json`
- `Docs/Lore/AppliedContent/articles/RS287_RECEIVER_COUNTERSTAMP_ROOM_NUMBER/*_external_site.md`

Generated targets:
- `Docs/Lore/AppliedContent/external_site/<locale>/P6805_RECEIVER_COUNTERSTAMP_ROOM_NUMBER.md`
- `Docs/Lore/AppliedContent/in_game_wiki/<locale>/P6805_RECEIVER_COUNTERSTAMP_ROOM_NUMBER.md`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_packets.csv`
- `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`

Runtime owner remains the baked DataMonolith/packet hash bridge. Runtime must not read JSON or markdown.

## World Binding Targets

- `poi.receiver_counterstamp_room_number`
- `poi.quarantine_room_number_plate`
- `poi.medical_owner_counterseal`
- `poi.public_ledger_receiver_strip`
- `poi.black_keel_lift_allocation_switch`

## Failure Cases To Preserve

- A room number must not imply safe freedom; it only creates receiver custody.
- A room stamped for samples must not satisfy body recovery.
- A stale room number must remain suspicious if its timestamp predates the body ledger or blank berth receipt.
- Duplicate world props should resolve to one packet hash and one unlock.
- Corporate receiver counterstamps should remain capture-risk outcomes, not neutral rescue language.
- Save/load or scene reload must preserve whether the state is `berth blank` or `room named`; do not collapse both into generic carrier acknowledgement.

