# RS093 Lore System Integration Bridge

Status: production-facing draft pending native localization, route-card authoring export, string-pool bake, and runtime placement.

Runtime rule: source content only. Runtime must not parse Markdown, live-translate text, scan article headings, or treat this release set as DataMonolith readiness.

Purpose: bridge lore packets into future site/wiki/in-game surfaces and static-data bake contracts without inventing runtime proof.

## Packets

- `P461_PACKET_CUSTODY_BRIDGE` - Packet custody as a physical evidence route for Black Keel, website/wiki, scanner, PDA/codex, terminal, audio, and future bake records.
- `P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE` - PressureSeal first repair bridge for the bright P-63 shelf, FiberKelp/FiberMesh/PressureSeal route teaching, scanner/codex/terminal/audio/field-note surfaces, and future bake records.
- `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE` - Public/wiki spoiler-gate bridge explaining which facts can live on external pages and which stronger claims require recovered in-game evidence.
- `P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE` - Black Keel claim-window bridge explaining why signal receipt, custody pricing, tonne-window allocation, and physical recovery are separate steps.

## Use

- In-game: early codex/scanner/terminal source row after packet seal or Black Keel custody-tag discovery.
- Site/wiki: public spoiler-safe article module explaining why evidence needs custody and why Black Keel contact is not rescue.
- Authoring: route cards, evidence graph, binding maps, placement backlog, localization/native-review queue, and future static-data bake input.
- Runtime/binary: PENDING. Requires LocID hashes, packet header table, surface enum table, unlock route IDs, relation records, and string-pool rows baked into validated static data.
- Packet source: STATIC_DOC only. `P461`, `P462`, `P463`, and `P464` are production Markdown packets, not canonical AppliedLore `.packets.json`, source CSV, generated hash, or h8bin rows.
- Route card: CANDIDATE only. Do not add `RS093_route_cards.csv` until `P461_PACKET_CUSTODY_BRIDGE`, `P462_PRESSURE_SEAL_FIRST_REPAIR_BRIDGE`, `P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE`, and `P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE` exist in the AppliedLore source CSV/generated packet export; authoritative route-card CSVs are validator-owned bake inputs.

## Boundary

This release set does not claim Unity scene placement, runtime UI/audio implementation, final native localization, final numeric balancing, authoritative route-card export, `static_data.h8bin` bake, h8bin generation, or DataMonolith boot validation.

Evidence class: STATIC_DOC.
