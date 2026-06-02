# Documentation Governance

Date: 2026-05-26
Status: STATIC POLICY
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: keep active docs small, source-backed, and free of work-log noise.

## Authority Order

1. `../AGENTS.md`
2. `../TASTE.md`
3. `../textes.md` for public copy only
4. `.agents-skills/README.md`
5. task-relevant `.agents-skills/*` mandates
6. current source under `Assets/_Project`
7. `Docs/PROJECT_BASELINE.md`
8. `Docs/ARCHITECTURE/README.md`
9. `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
10. active architecture contracts
11. fresh verification artifacts
12. dated reports and archives

## Placement Rules

Root may contain only:

- `AGENTS.md`
- `TASTE.md`
- `textes.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`

Active docs belong in `Docs/` or `Docs/ARCHITECTURE/`.

Evidence snapshots belong in `Docs/Reports/`.

Historical material belongs in `Docs/DEPRECATED/`, `Docs/_Archive/`, or `Docs/Archive/`.

Stable distilled facts belong in `Docs/PROJECT_BASELINE.md` or `Docs/ARCHITECTURE` when they are durable. Do not turn report, task, or log folders into the project brain.

## Update Rules

- Do not use dated reports as active contracts unless a current stable doc imports the fact.
- Do not claim Unity import, Console, Play Mode, profiler, GCMonitor, player build, save/load, scene wiring, shader import, or visual proof without a fresh artifact path.
- Keep status updates concise and tied to source, command, artifact, or grep proof.
- Do not inflate docs with audit prose.
- Record durable facts, blockers, proof boundaries, and owner routes. Drop narrative.
- Root docs must not carry current build status, scanner counters, prompt summaries, or task-progress notes.

## Report Rules

- Read `Docs/PROJECT_BASELINE.md` before scanning report/log/status piles.
- Use `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` for concise current proof snapshots.
- Read status/log files only when a report artifact still needs ownership context.
- Do not bulk-archive or delete latest proof artifacts while they are still cited by active evidence.
- A scoped green report is not a global green build.
- A stale compile artifact remains useful history, not current readiness proof after newer source edits.
- Put superseded process residue in `Docs/DEPRECATED` or `Docs/_Archive` only after preserving a manifest and not breaking active citations.
