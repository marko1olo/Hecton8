# Resource Recipe Pressure Rules

Status: production-facing draft pending native localization.

Recipe tiers, pressure thresholds, blue debt quality, vent forge steps and escape components.

## Packets

- `P171_RECIPE_TIER_PRESSURE_BANDS` - Recipe Tier Pressure Bands: Recipe Tier Pressure Bands defines release-facing resource progression.
- `P172_PRESSURE_FAILURE_THRESHOLDS` - Pressure Failure Thresholds: Pressure Failure Thresholds defines containment risk for crafting and route cards.
- `P173_BLUE_DEBT_SAMPLE_QUALITY` - Blue Debt Sample Quality: Blue Debt Sample Quality defines payout and evidence classes.
- `P174_VENT_FORGE_PROCESS_STEPS` - Vent Forge Process Steps: Vent Forge Process Steps defines the site/wiki and in-game crafting fantasy.
- `P175_ESCAPE_COMPONENT_TUNING_RULES` - Escape Component Tuning Rules: Escape Component Tuning Rules gives crafting and endings a single route grammar.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
