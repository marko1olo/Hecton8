# Premium Approximation Rename Triage - 2026-06-05

Status: `STATIC TRIAGE / CURRENT REFERENCES PATCHED`.
Evidence class: `STATIC_TEXT_SCAN`.

CSV: `Docs/Reports/AssetSystem_20260605/PREMIUM_APPROXIMATION_RENAME_TRIAGE_20260605.csv`.

## What Was Wrong

Root authority now uses `Premium Approximation Protocol`. The old mandate path `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt` and old ledger `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` are deleted in the working tree.

Several current packets still reference the deleted old mandate or old terminology. That does not relax the rule, but it can break future packet execution because agents may try to load a missing mandate.

## Current Authority

- `.agents-skills/OPT_Premium_Approximation_Protocol.txt`
- `Docs/ARCHITECTURE/PREMIUM_APPROXIMATION_LEDGER.md`
- `AGENTS.md` section `AUTHORITY SPINE + PREMIUM APPROXIMATION`

Meaning retained and strengthened:

- Prefer deterministic premium authored/shader/audio/haptic/UI/proxy approximation before physical simulation.
- Approximation-first is not cheapness-first.
- Flat water, muddy sky, weak terrain, crayon texture, empty fog, low-detail hero assets, and route camouflage are rejected even if fast.

## Scan Scope

Scanned only current active scopes:

- `taskslocal/asset_system_20260605`
- `taskslocal/world_system_20260605`
- `Docs/Reports/AssetSystem_20260605`
- `Docs/Reports/RuntimeSystem_20260605`
- `Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md`

Deprecated archives and old batch dumps were not treated as current blockers.

## Patch Result

After this triage, current non-triage references in the scanned active scopes were patched to the live premium approximation mandate/ledger. The CSV is retained as the audit trail of what was stale before the patch.

## Current Blockers

- P0 rows in the CSV identify packet/report references that were stale before this pass.
- P1 rows in the CSV identify terminology-only cleanup where the old term still described the same premium approximation concept.
- No row authorizes cheap flat presentation, physics overkill, or visual floor reduction.

## Recommended Replacement Text

Use:

`- .agents-skills/OPT_Premium_Approximation_Protocol.txt`

For architecture ledger references, use:

`- Docs/ARCHITECTURE/PREMIUM_APPROXIMATION_LEDGER.md`

For prose, replace `visual fake-first` with:

`premium deterministic approximation first; physical simulation only when gameplay truth requires it`

## Regression Model

- CPU: static text triage only.
- GC: no runtime code changed.
- Memory: no asset residency changed.
- Cadence: no runtime cadence changed.
- Correctness: future owners get a live mandate path instead of a deleted one.
- Visual: rule rename does not lower the visual floor.

Final status: `CURRENT REFERENCES PATCHED / PENDING VERIFICATION`.
