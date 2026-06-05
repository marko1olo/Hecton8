# Rationale 3202

Decision: did not create a P461/P462 source-only patch.

Reason:
- The correct first insertion point is a canonical packet JSON bundle, not generated CSV and not route cards.
- `Tools/AppliedLoreImporter.py` reads manifest `packet_sources`; it ignores RS093 `canonical_importer_sources` and `authoring_packet_sources`.
- P461/P462 Markdown locale rows are not a complete importer-ready field matrix for every locale. Manual conversion would decide missing `terminal`, `audio`, `in_game_wiki`, and `external_site` rows for some locales.
- Current source-only audit is already red on an existing publication frontmatter mismatch outside 3202 scope.

Consequence:
- `RS093_route_cards.csv` must remain absent.
- `static_data.h8bin` must not be edited or rebaked for RS093 until canonical packet JSON, generated source CSV/hash/page exports, and source-only audit are accepted.
