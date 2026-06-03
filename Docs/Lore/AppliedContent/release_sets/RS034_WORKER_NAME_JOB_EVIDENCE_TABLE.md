# Worker Name Job Evidence Table

Status: production-facing draft pending native localization.

Seed-safe worker names, jobs, locker variants and localization handling.

## Packets

- `P166_WORKER_NAME_POOL_PROTOCOL` - Worker Name Pool Protocol: Worker Name Pool Protocol defines site/wiki and game rules for seeded worker evidence.
- `P167_PRESSURE_JOB_TITLE_TABLE` - Pressure Job Title Table: Pressure Job Title Table gives writers and UI a stable colony labor vocabulary.
- `P168_LOCKER_PROP_VARIANTS` - Locker Prop Variants: Locker Prop Variants defines reusable art and wiki hooks for colony evidence.
- `P169_NATIVE_LOCALIZED_NAME_HANDLING` - Native Localized Name Handling: Native Localized Name Handling defines localization-safe colony naming.
- `P170_SHIFT_CREW_STORY_SEEDS` - Shift Crew Story Seeds: Shift Crew Story Seeds turns worker evidence into replay-safe content.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
