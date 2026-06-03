# Dossier Contract UI Copy Deck

Status: production-facing draft pending native localization.

Provide concrete short UI copy for contract, dossier, route warning and ending record surfaces.

## Packets

- `P266_DOSSIER_START_SCREEN_COPY` - Dossier Start Screen Copy: Dossier Start Screen Copy explains how HECTON-8 uses memory as pressure instead of roguelite power.
- `P267_CONTRACT_CARD_FIELD_LABELS` - Contract Card Field Labels: Contract Card Field Labels describe HECTON-8 contract UI as a planning instrument.
- `P268_RUMOR_FAMILY_UI_COPY` - Rumor Family Ui Copy: Rumor Family UI Copy explains how HECTON-8 replay hints preserve uncertainty.
- `P269_ROUTE_WARNING_UI_COPY` - Route Warning Ui Copy: Route Warning UI Copy describes HECTON-8 warnings as physical decision text.
- `P270_ENDING_RECORD_UI_COPY` - Ending Record Ui Copy: Ending Record UI Copy explains why HECTON-8 endings are records of tradeoffs rather than victory screens.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
