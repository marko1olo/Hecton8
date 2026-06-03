# Colony Roster Authoring Pool

Status: production-facing draft pending native localization.

Lock the colony roster scale, crew archetypes and reusable identity pool for prop, wiki and mission writing.

## Packets

- `P206_WORKER_ROSTER_SIZE_RULE` - Worker Roster Size Rule: Worker Roster Size Rule defines the HECTON-8 colony name pool for wiki, game and prop writing.
- `P207_PRESSURE_CREW_ARCHETYPE_TABLE` - Pressure Crew Archetype Table: Pressure Crew Archetype Table gives HECTON-8 colony writing a practical labor structure.
- `P208_ANCHOR_WORKER_NAME_SET_A` - Anchor Worker Name Set A: Anchor Worker Name Set A provides publication-ready early colony names for HECTON-8.
- `P209_ANCHOR_WORKER_NAME_SET_B` - Anchor Worker Name Set B: Anchor Worker Name Set B gives HECTON-8 deeper worker identities for spoiler-gated pages.
- `P210_SEED_ROLE_NAME_GRAMMAR` - Seed Role Name Grammar: Seed Role Name Grammar defines how HECTON-8 handles replay-safe colony names.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
