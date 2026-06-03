# RS032_COLONY_HUMAN_EVIDENCE_LAYER

Status: production-facing draft, pending native localization pass.
Runtime contract: authoring source only; runtime must consume baked static data/string-pool rows.

Purpose: locks the colony-as-people evidence layer through shifts, job cards, lockers, triage ledgers and Marauder correction notes.

## Packets

- `P156_SHIFT_CREWS_NOT_HEROES` - Shift Crews Not Heroes: Shift Crews Not Heroes is the writing rule for humanizing the drowned colony.
- `P157_WORKER_JOB_CARDS` - Worker Job Cards: Worker Job Cards turn names into evidence objects.
- `P158_LOCKER_NAME_PROTOCOL` - Locker Name Protocol: Locker Name Protocol is personal evidence without melodrama.
- `P159_MEDICAL_TRIAGE_LEDGER` - Medical Triage Ledger: Medical Triage Ledger is the human cost of delayed evacuation.
- `P160_MARAUDER_CORRECTION_LAYER` - Marauder Correction Layer: Marauder Correction Layer is the tone bridge between Deep Reach procedure and player agency.

## Production Use

- Scanner and terminal snippets are short enough for diegetic UI.
- In-game wiki and external-site fields are generated from the packet bundle.
- Route cards connect packet IDs to depth windows, replay axes and ending pressure.
- Binding maps provide future Unity/DataMonolith placement targets without runtime markdown parsing.
