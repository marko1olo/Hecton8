# Rs090 Unity Placement Scene Briefs

Status: production-facing draft pending native localization and runtime placement.
Runtime rule: source content only; no runtime JSON, markdown or live translation.

Purpose: Unity placement briefs for first-hour, mid-depth, ecology, final descent and terminal-promotion backlog rows.

## Packets

- `P446_FIRST_HOUR_PLACEMENT_BRIEF` - First Hour Placement Brief.
- `P447_MID_DEPTH_ROUTE_PLACEMENT_BRIEF` - Mid Depth Route Placement Brief.
- `P448_ECOLOGY_SCAN_PLACEMENT_BRIEF` - Ecology Scan Placement Brief.
- `P449_FINAL_DESCENT_PLACEMENT_BRIEF` - Final Descent Placement Brief.
- `P450_TERMINAL_SLOT_PROMOTION_BRIEF` - Terminal Slot Promotion Brief.

## Use

- In-game: scanner, terminal, PDA/codex, dossier or audio transcript source rows after DataMonolith bake.
- Site/wiki: external article modules generated from the same packet IDs.
- Authoring: route cards, evidence graph, binding maps, image briefs and placement backlog.

## Boundary

This release set does not claim Unity scene placement, runtime UI/audio implementation, final native localization, final numeric balancing or `static_data.h8bin` bake.
