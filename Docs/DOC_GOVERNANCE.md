# Documentation Governance

Date: 2026-06-09
Status: STATIC POLICY
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: keep active docs small, source-backed, and free of work-log noise.

## Authority Order

1. `../AGENTS.md`
2. `../COMMON_SENSE.md` for non-trivial work
3. `Docs/AGENT_AUTHORITY_ROUTING.md` for non-trivial task intake and no-loss rule routing
4. `../PROJECT_BIBLES.md`
5. `Docs/SYSTEMS_CONTRACTS.md` for non-asset runtime systems, architecture, signals, data vaults, or core memory
6. `../VISION_LOCKS.md` for product vision or ambiguity
7. `../TASTE.md` and the matching standing root route bible
8. `../.agents-skills/README.md`
9. task-relevant `../.agents-skills/*` mandates
10. `Docs/QUALITY_GATES.md` before claiming `VERIFIED` or `COMPLETE`
11. `Docs/AGENTS_RULE_DETAIL_LEDGER.md` only for no-loss conflict resolution or migration provenance
12. `../textes.md` for public copy only
13. current source under `Assets/_Project`
14. `Docs/PROJECT_BASELINE.md`
15. `Docs/ARCHITECTURE/README.md`
16. `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
17. active architecture contracts
18. fresh verification artifacts
19. dated reports and archives

## Placement Rules

Root may contain these active text anchors and standing route bibles:

- `AGENTS.md`
- `CLAUDE.md` as the Claude Code shim only, not divergent project law
- `COMMON_SENSE.md` as common HECTON-8 engineering law routed by the authority spine
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `textes.md`
- `MASTER_RELEASE_WORK_PLAN.md`
- `BUILD_PLAYTEST_ISSUES.md`
- standing root route bibles listed under `Routes` in `PROJECT_BIBLES.md`
- `GEMINI.md` as a third-party Gemini/Antigravity shim only, not project law
- `HECTON8_ORCHESTRATOR.md` for explicit standalone batch/controller/orchestration work
- `HECTON8_AUTONOMOUS_CODEX_ORCHESTRATOR.md` for explicit local VS Code Codex GUI control work

Active docs belong in `Docs/` or `Docs/ARCHITECTURE/`.

Evidence snapshots belong in `Docs/Reports/`.

Explicit tool, validator, dump, and telemetry outputs belong in `Docs/AgentLogs/` only when a current tool, report chain, or explicit task mode owns that path. `Docs/AgentLogs/` is not authority and must not become a work diary. Move only verified orphan one-off outputs to `Docs/DEPRECATED/` after exact filename/path searches.

Explicit task status records belong in `Docs/Tasks/` only when a current explicit-mode task/controller workflow owns the path or a report/source cites the exact ID. `Docs/Tasks/` is not authority and must not be bulk-read as current context. Move stale status records only after exact path/name/ID searches prove they are no longer active inputs.

Standalone dispatch packets belong in `taskslocal/` only for explicit local-agent batch work. `taskslocal/` is not authority and must not be bulk-read as current context. Do not archive old batch folders merely because they are historical; move a batch only after exact path/name/batch searches prove no active source, validator, report, status, or controller workflow still cites it. New or materially rewritten serious batches must pass the strict lane gate before distribution.

Root reports, prompts, status files, work logs, generated evidence, task-progress prose, and temporary scan counters are forbidden as root doctrine.

Historical material belongs in `Docs/DEPRECATED/`, `Docs/_Archive/`, or `Docs/Archive/`.

Stable distilled facts belong in `Docs/PROJECT_BASELINE.md` or `Docs/ARCHITECTURE` when they are durable. Do not turn report, task, or log folders into the project brain.

## Update Rules

- Do not use dated reports as active contracts unless a current stable doc imports the fact.
- Do not hand-edit generated snapshots such as `Docs/PROJECT_ROOT_BIBLES_COMBINED.md`; update the live source file and rerun the generator.
- Keep `C:\Users\Admin\.codex\AGENTS.md` as a global router, not a duplicate HECTON-8 law copy.
- Keep `Docs/AGENTS_RULE_DETAIL_LEDGER.md` as no-loss detail/provenance, not as an always-on substitute for `PROJECT_BIBLES.md`, root route bibles, or `.agents-skills` mandates.
- Keep `.agent/rules/*.md` as historical/reference guidance with a strong HECTON-8 override header; they are not independent authority.
- Keep local AGENTS derivatives delegated to or synchronized with root `AGENTS.md`; they are not independent law sources.
- Use `Docs/AGENT_AUTHORITY_ROUTING.md` for rule-surface routing and the no-loss split protocol before shortening, splitting, or mirroring rule files.
- Run `python -B Tools/Docs/TestAgentRuleRouting.py` after agent rule-surface edits.
- Run `python -B Tools/Docs/TestMandateRegistry.py` after `.agents-skills` mandate or registry edits.
- Run `python -B Tools/Docs/TestTaskLocalLaneContracts.py taskslocal/<batch_name> --strict` before dispatching any new or materially rewritten serious `taskslocal` batch; use `--allow-legacy` only for old-batch inspection.
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
