# Rationale 3221

Decision:
- Assigned P464 to `RC498_P464_BLACK_KEEL_CLAIM_WINDOW_BRIDGE` instead of the duplicate `RC497` candidate in packet Markdown.

Reason:
- P463 already owns `RC497_P463_PUBLIC_WIKI_SPOILER_GATE_BRIDGE`.
- Route-card exporter rejects duplicate route IDs and packet ownership collisions.
- Task authority explicitly required P464 as `RC498`.

Evidence:
- Source row exists in `Docs/Lore/AppliedContent/route_cards/RS093_route_cards.csv`.
- Exported row exists in `Assets/_SourceData/DataMonolith/Narrative/applied_lore_route_cards.csv`.
- Source-only audit passed.

