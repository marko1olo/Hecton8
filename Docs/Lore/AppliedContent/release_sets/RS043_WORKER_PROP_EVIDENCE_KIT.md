# Worker Prop Evidence Kit

Status: production-facing draft pending native localization.

Lock prop-level evidence variants for lockers, ledgers, route stamps, Marauder corrections and audio fragments.

## Packets

- `P211_LOCKER_PROP_VARIANT_MATRIX` - Locker Prop Variant Matrix: Locker Prop Variant Matrix defines publication and asset rules for HECTON-8 worker lockers.
- `P212_TRIAGE_LEDGER_PROP_VARIANTS` - Triage Ledger Prop Variants: Triage Ledger Prop Variants define how HECTON-8 presents medical evidence.
- `P213_ROUTE_PERMISSION_STAMP_SET` - Route Permission Stamp Set: Route Permission Stamp Set provides visual vocabulary for HECTON-8 access props.
- `P214_MARAUDER_CORRECTION_MARK_RULES` - Marauder Correction Mark Rules: Marauder Correction Mark Rules defines HECTON-8's salvage-note visual language.
- `P215_AUDIO_FRAGMENT_PROP_RULES` - Audio Fragment Prop Rules: Audio Fragment Prop Rules defines how HECTON-8 uses voice without lore-dump fatigue.

## Runtime Rule

Authoring/export source only. Runtime consumes baked static-data rows, packet hashes and string-pool offsets.
No runtime markdown parsing, JSON parsing, live translation or scene search.
