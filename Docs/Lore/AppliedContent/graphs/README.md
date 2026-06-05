# AppliedContent Evidence Graphs

Status: authoring graph / runtime input candidate. Currentness requires a timestamped inventory or audit artifact.
Purpose: connect baked content packets to evidence chains, POI sequencing, depth bands, player decisions, and future quest flags.

This is not runtime prose. Runtime should consume baked packet IDs, route flags, phase gates, and localized string-pool slices.

## Current Graphs

This README is not the current graph inventory. Treat local graph counts as static snapshots unless a timestamped command output or audit artifact says otherwise.

- `RS001_RS003_evidence_graph.csv`: historical first-wave graph for the first 15 packets.
- `RS###*_evidence_graph.csv`: release-set graph CSV pattern for later waves. The directory currently extends beyond RS001-RS003; use the folder inventory and scoped audit output instead of this README for current coverage.

## Rules

- Every scoped AppliedLore packet intended for graph export must appear once.
- `prereq_packet_ids` and `next_packet_ids` must reference known packet IDs.
- Graph truth cannot change per seed. Seed variation may alter location, order pressure, route context, and optional fragments only.
- `route_moment` must describe a player-facing action or evidence beat, not an abstract lore chapter.
- `Tools/AppliedLoreRuntimeAudit.py --root .` can validate graph coverage and references against the active static-data/source scope for that run. The command result, timestamp, and artifact path are the proof, not this README.
