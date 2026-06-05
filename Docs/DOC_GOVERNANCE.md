# Documentation Governance

Date: 2026-05-26
Status: STATIC POLICY
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: keep active docs small, source-backed, and free of work-log noise.

## Authority Order

1. `../AGENTS.md`
2. `Docs/AGENT_AUTHORITY_ROUTING.md` for non-trivial task intake and no-loss rule routing
3. `../PROJECT_BIBLES.md`
4. `../VISION_LOCKS.md` for product vision or ambiguity
5. `../TASTE.md` and the matching standing root route bible
6. `../.agents-skills/README.md`
7. task-relevant `../.agents-skills/*` mandates
8. `Docs/AGENTS_RULE_DETAIL_LEDGER.md` only for no-loss conflict resolution or migration provenance
9. `../textes.md` for public copy only
10. current source under `Assets/_Project`
11. `Docs/PROJECT_BASELINE.md`
12. `Docs/ARCHITECTURE/README.md`
13. `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
14. active architecture contracts
15. fresh verification artifacts
16. dated reports and archives

## Placement Rules

Root may contain these active text anchors and standing route bibles:

- `AGENTS.md`
- `GEMINI.md` as Gemini/Antigravity shim only
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `textes.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`
- standing root route bibles listed under `Routes` in `PROJECT_BIBLES.md`

Active docs belong in `Docs/` or `Docs/ARCHITECTURE/`.

Evidence snapshots belong in `Docs/Reports/`.

Root reports, prompts, status files, work logs, generated evidence, task-progress prose, and temporary scan counters are forbidden as root doctrine.

Historical material belongs in `Docs/DEPRECATED/`, `Docs/_Archive/`, or `Docs/Archive/`.

Stable distilled facts belong in `Docs/PROJECT_BASELINE.md` or `Docs/ARCHITECTURE` when they are durable. Do not turn report, task, or log folders into the project brain.

## Update Rules

- Do not use dated reports as active contracts unless a current stable doc imports the fact.
- Do not hand-edit generated snapshots such as `Docs/PROJECT_ROOT_BIBLES_COMBINED.md`; update the live source file and rerun the generator.
- Keep `C:\Users\danat\.codex\AGENTS.md` as a global router, not a duplicate HECTON-8 law copy.
- Keep `C:\Users\danat\.gemini\GEMINI.md` as a global Gemini/Antigravity router, not a duplicate HECTON-8 law copy.
- Keep project `GEMINI.md` as a tool shim that routes to root `AGENTS.md` and `Docs/AGENT_AUTHORITY_ROUTING.md`.
- Keep `Docs/AGENTS_RULE_DETAIL_LEDGER.md` as no-loss detail/provenance, not as an always-on substitute for `PROJECT_BIBLES.md`, root route bibles, or `.agents-skills` mandates.
- Keep `.agent/rules/*.md` as short historical stubs; previous generic Unity bodies belong under `Docs/DEPRECATED/AgentRulesHistorical_20260605/`.
- Keep local AGENTS derivatives delegated to or synchronized with root `AGENTS.md`; they are not independent law sources.
- Use `Docs/AGENT_AUTHORITY_ROUTING.md` for rule-surface routing and the no-loss split protocol before shortening, splitting, or mirroring rule files.
- Run `python -B Tools/Docs/TestAgentRuleRouting.py` after agent rule-surface edits.
- Preserve unrelated dirty files and report real evidence conflicts instead of rewriting history or generated artifacts.
- Do not claim Unity import, Console, Play Mode, profiler, GCMonitor, player build, save/load, scene wiring, shader import, or visual proof without a fresh artifact path.
- Keep status updates concise and tied to source, command, artifact, or grep proof.
- Do not inflate docs with audit prose.
- Do not create repeat checks over unchanged source, assets, or proof. Once a report records `PENDING VERIFICATION`, the next artifact must be a proof run, source/asset fix, or concrete blocker note.
- Record durable facts, blockers, proof boundaries, and owner routes. Drop narrative.
- Root docs must not carry current build status, scanner counters, prompt summaries, or task-progress notes.

## Report Rules

- Read `Docs/PROJECT_BASELINE.md` before scanning report/log/status piles.
- Use `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` for concise current proof snapshots.
- Read status/log files only when a report artifact still needs ownership context.
- Do not bulk-archive or delete latest proof artifacts while they are still cited by active evidence.
- A scoped green report is not a global green build.
- A stale compile artifact remains useful history, not current readiness proof after newer source edits.
- A third static report for the same unchanged state is rejected unless the user/controller explicitly asks for it or new evidence changed the decision.
- Put superseded process residue in `Docs/DEPRECATED` or `Docs/_Archive` only after preserving a manifest and not breaking active citations.
