# Orphan And Crosslink Findings - 1776

Evidence class: STATIC_SOURCE from JSON/CSV parse.

## Summary
- `.packets.json` files parsed: 91.
- Bundle packets parsed: 451.
- Single-packet JSON files outside bundle pattern: 9.
- Surface index unique packet IDs: 460.
- Cluster index unique cluster packet IDs: 5.

## Surface Packet IDs Not In `.packets.json` Bundle Scope
- P001_CRASH_SHELF: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P001_CRASH_SHELF.json`.
- P002_BLACK_KEEL_CONTACT: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P002_BLACK_KEEL_CONTACT.json`.
- P003_BARNARD_MARK: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P003_BARNARD_MARK.json`.
- P004_BLUE_DEBT: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P004_BLUE_DEBT.json`.
- P005_REPAIR_SCAR: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P005_REPAIR_SCAR.json`.
- P007_BRINE_STAIR: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P007_BRINE_STAIR.json`.
- P008_EVACUATION_HOLD: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P008_EVACUATION_HOLD.json`.
- P009_BOTTOM_FACTORY: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P009_BOTTOM_FACTORY.json`.
- P010_PAYLOAD_WINDOW: surface index references packet; bundle file absent; single-packet evidence: `Docs/Lore/AppliedContent/packets/P010_PAYLOAD_WINDOW.json`.

## Bundle Packet IDs Not In Surface Index
- None.

## Dead Cluster References
- None. All cluster packet/preq/next references resolve to packet evidence in bundle or single-packet scope.

## Duplicate IDs
- Bundle duplicate packet IDs: none.
- Bundle duplicate article IDs: none.
- Surface duplicate rows by `(surface, locale, packet_id)`: none.
- Cluster duplicate rows by `(surface, locale, cluster_packet_id)`: none.

## Clusters With Unclear Reader/Player Purpose
- None. Each current cluster has `truth_payload` and `player_question` fields.
