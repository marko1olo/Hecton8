# Handoff 1776 - Lore Facts / Crosslinks / Player Notes

Evidence class: STATIC_DOC / STATIC_SOURCE.

## Writer Follow-Up

- `P001_CRASH_SHELF`, `P002_BLACK_KEEL_CONTACT`, `P003_BARNARD_MARK`, `P004_BLUE_DEBT`, `P005_REPAIR_SCAR`, `P007_BRINE_STAIR`, `P008_EVACUATION_HOLD`, `P009_BOTTOM_FACTORY`, and `P010_PAYLOAD_WINDOW` exist as single-packet `H8.APPLIED_CONTENT_PACKET.V0` JSON, but not as `.packets.json` bundles. Decide whether to bundle them or keep the legacy single-packet schema documented.
- Packet-level related/crosslink arrays do not exist in the current `.packets.json` schema. Current relationships are only recoverable from publication/cluster indexes.

## Localization Follow-Up

- Several non-English rows in packet JSON and generated indexes display mojibake/draft-hold text in static console reads. Treat all non-English publication rows as draft unless native review, font/layout proof, and string-pool bake evidence exist.
- Player-note labels/templates need LocID approval before localization work. Proposed label IDs are in `production_audits/1776/player_note_templates.md`.

## Runtime/Data Follow-Up

- Do not consume audit CSVs as runtime truth. They are authoring audit outputs.
- Packet-level spoiler fields are absent in `.packets.json`. `crosslink_inventory.csv` marks most packet rows as `UNSPECIFIED_IN_PACKET_OR_SURFACE_INDEX`; schema/exporter owners need to decide whether spoiler tier belongs in packet bundles, generated indexes, or a separate relationship table.
- Player notes must be implemented through existing packet/article/unlock IDs. No runtime UI fields were invented in this pass.

## Reader/Publication Follow-Up

- `Publication_Cluster_Index.csv` currently covers five site/wiki navigation clusters. It separates start-here, system/ships, colony/workers, resources/ecology, and spoiler endings hubs with player questions and truth payloads.
- Public/wiki spoiler separation appears structurally correct in the cluster index: ending cluster is tier 2. Packet-level spoiler coverage remains a source gap outside the cluster rows.

## Exact Audit Artifacts

- `Docs/Lore/AppliedContent/production_audits/1776/crosslink_inventory.csv`
- `Docs/Lore/AppliedContent/production_audits/1776/orphan_crosslink_findings.md`
- `Docs/Lore/AppliedContent/production_audits/1776/fact_taxonomy.md`
- `Docs/Lore/AppliedContent/production_audits/1776/fact_owner_matrix.csv`
- `Docs/Lore/AppliedContent/production_audits/1776/player_note_templates.md`
- `Docs/Lore/AppliedContent/production_audits/1776/player_note_candidates.csv`
- `Docs/Lore/AppliedContent/production_audits/1776/cluster_surface_purpose_audit.csv`
- `Docs/Lore/AppliedContent/production_audits/1776/spoiler_leak_audit.md`
- `Docs/Lore/AppliedContent/production_audits/1776/surface_brightness_conflict_audit.md`
- `Docs/Lore/AppliedContent/production_audits/1776/fact_id_naming_convention.md`
- `Docs/Lore/AppliedContent/production_audits/1776/validation_output.txt`

