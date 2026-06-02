# AppliedContent Evidence Graphs

Status: authoring graph / runtime input candidate.
Purpose: connect baked content packets to evidence chains, POI sequencing, depth bands, player decisions, and future quest flags.

This is not runtime prose. Runtime should consume baked packet IDs, route flags, phase gates, and localized string-pool slices.

## Current Graphs

- `RS001_RS003_evidence_graph.csv`: first 15 baked packets, connected into survival, personal motive, resource truth, Atlas truth, human-space context, orbital pressure, salvage economy, and ending-payload arcs.

## Rules

- Every baked AppliedLore packet must appear once.
- `prereq_packet_ids` and `next_packet_ids` must reference known packet IDs.
- Graph truth cannot change per seed. Seed variation may alter location, order pressure, route context, and optional fragments only.
- `route_moment` must describe a player-facing action or evidence beat, not an abstract lore chapter.
- `Tools/AppliedLoreRuntimeAudit.py --root .` validates graph coverage and references against the currently baked AppliedLore packet set.
