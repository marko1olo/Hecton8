# Rationale_HFI_AUDIT

Date: 2026-05-19
Status: PENDING VERIFICATION

## Decision 001 - Treat User Term As H-Phi
Problem: User requested a phonetically typed "ash-fi"; repository search found active `H-Phi`, `HECTON_PHI`, and `METRIC_PHI` reports, while exact `H-Fi/HFI` has no project metric definition.
Solution: Use the existing H-Phi corpus as the authoritative metric target and label the term mapping in the final report.
Rejected Alternatives: Inventing a new H-Fi formula was rejected because it would create fake metrics. Treating the user term as System Health Index was rejected because SHI is a runtime stress scalar, not the project-wide architecture metric corpus.
Scalability potential: Low/Middle/High/Ultra impact is process-only; metric clarity prevents wrong runtime priorities across all hardware tiers.
Hardware Impact: 0 us runtime; avoids misdirected future work that could burn MX350 budget on irrelevant score chasing.

## Decision 002 - Audit-Only, No Compile Loop
Problem: User explicitly asked to find problems besides compilation; AGENTS warns not to spam builds when other agents may be active.
Solution: Run static and documentation analysis only unless a source edit requires compile verification.
Rejected Alternatives: Launching `dotnet build` was rejected as out-of-scope and potentially disruptive in a concurrent multi-agent workspace.
Scalability potential: Low/Middle/High/Ultra impact is process-only; preserves CPU for active agents and avoids false compile-centered conclusions.
Hardware Impact: 0 us runtime; saves local machine time, not player frame time.

## Decision 003 - Use Latest Static Artifact Instead Of Fresh Full Rescan
Problem: A fresh `HectonPhiAudit.ps1` full scan could be expensive and local CPU was observed at 100% with Unity active.
Solution: Recalculate current H-Phi from the latest existing artifact `Docs/AgentLogs/HPhi_SHINOBU_02_current2.json`, using the official formula and component values.
Rejected Alternatives: Running a heavy full scan under active machine load was rejected. Using only `Docs/Reports/HECTON_PHI_SCORE_FINAL.json` was rejected because it belongs to the older Python score family, not the runtime static H-Phi family.
Scalability potential: Low/Middle/High/Ultra impact is process-only; avoids wasting local CPU while keeping metric lineage accurate.
Hardware Impact: 0 us runtime; avoids local analysis load, no player-frame claim.

## Decision 004 - Split Metric Trend From Runtime Readiness
Problem: H-Phi grew sharply, but H-Phi is static text/architecture evidence only.
Solution: Report score growth separately from proof gaps and explicitly mark Unity/profiler/player proof as pending.
Rejected Alternatives: Treating H-Phi growth as product readiness was rejected because `QUALITY_GATES.md` and the H-Phi contract forbid that inference.
Scalability potential: Low devices still need actual frame/memory proof; high/ultra visual overkill still needs content payloads and profile gates.
Hardware Impact: 0 us runtime; prevents false optimization/marketing decisions from a static metric.

## Decision 005 - Promote Global Authority Boundary
Problem: User asked whether the project is already globally failing by introducing `GlobalRegistry`, event bus, signal bus, and global storage. The risk is architectural, not compile-only: correct primitives can still become four global god objects.
Solution: Create `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`, add cross-links to the architecture index/global map, tighten `GLOBAL_REGISTRY_SERVICE_LOCATOR.md`, `GLOBAL_SIGNAL_CORRIDOR.md`, `QUALITY_GATES.md`, and update the signal-lane mandate with the `HectonEventBus`/legacy queue boundary.
Rejected Alternatives: Leaving the answer in chat was rejected because authority must live in stable docs. Editing runtime code was rejected because the user requested documentation/analysis, and no single runtime fix can solve a cross-surface governance problem.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; preventing global monolith growth protects MX350 from hidden hot-path polls and lets high/ultra spend cycles on visible systems instead of accidental bus churn.
Hardware Impact: 0 us runtime in this pass; future impact is avoided registry polling, managed event traffic, and unmanaged memory leak risk.

## Decision 006 - Add Migration Ledger Instead Of Runtime Refactor
Problem: Boundary rules alone do not tell the next agent where to start; they can still add global routes while claiming H-Phi improvement.
Solution: Add `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md` with static snapshot counters, top review targets, decision checklist, audit commands, and completion criteria; propagate the boundary into `SYSTEM_INTERCONNECT_MATRIX.md`, `DISPATCH_PIPELINE.md`, `HECTON_PHI_STATIC_METRIC.md`, `SYSTEMS_CONTRACTS.md`, `Docs/README.md`, `Docs/Reports/README.md`, `HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and registry/signal mandates.
Rejected Alternatives: Editing runtime source was rejected because this pass is governance/documentation and safe migration requires owner-specific proof. Creating another dated report only was rejected because durable policy belongs in stable docs.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; the ledger protects low-end devices from hidden global polling and protects high/ultra paths from spending cycles on accidental broadcast churn instead of visible output.
Hardware Impact: 0 us runtime in this pass; future savings remain unmeasured until profiler evidence exists.

## Decision 007 - Align Enforcement Layer With New Global Boundary
Problem: Stable architecture docs existed, but higher-order operating docs and mandates could still be read as permission to treat `GlobalRegistry`, `HectonEventBus`, direct `GlobalSignals`, or `GlobalDataVault` as convenience globals.
Solution: Propagate the boundary into `AGENTS.md`, `.codexrules/AGENTS.md`, `.agents-skills/README.md`, native memory, QA evidence, telemetry mandates, `Docs/DOC_GOVERNANCE.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, root roadmap/playtest anchors, and `Docs/ROOT_DOCS_REFERENCE.md`.
Rejected Alternatives: Leaving the conflict for future agents was rejected because AGENTS/mandates outrank most docs in day-to-day work. Runtime refactor was rejected because this pass is governance-only and safe code migration needs owner-specific profiling and integration proof.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; the rule blocks low-end frame theft from hidden global polling and keeps high/ultra quality budgets for visible output instead of unmanaged broadcast/storage churn.
Hardware Impact: 0 us runtime in this pass; future microsecond savings require profiler evidence after targeted runtime migrations.

## Decision 008 - Contain Active Historical Reports
Problem: Active metric/signal reports predate the global authority boundary and could be cited for old EventBus/DataVault/H-Phi interpretations.
Solution: Add explicit 2026-05-19 override sections to `Docs/Reports/HECTON_PHI_REPORT.md` and `Docs/Reports/SIGNAL_UNIFICATION_AUDIT.md`, plus short boundary notes in `Docs/PROJECT_ATLAS.md` and `Docs/TECHNICAL_FAQ.md`.
Rejected Alternatives: Rewriting old reports was rejected because they are provenance snapshots. Ignoring them was rejected because agents still grep and cite active reports.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; stale report containment prevents future work from converting static-score movement into real hot-path debt.
Hardware Impact: 0 us runtime in this pass; no player-frame impact claimed.

## Decision 009 - Add AAA Operating Model Instead Of More Abstract Warnings
Problem: Boundary docs say what is allowed/forbidden, but teams still need an operational pattern for combining registry, dispatcher, SignalBus, legacy queues, HectonEventBus, DataVault, telemetry, and H-Phi without creating a global monolith.
Solution: Create `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md` with the "one fact -> one owner -> one route -> one proof artifact" law, lifecycle order, route-card template, instrument setup rules, quality scaling, review gates, and migration strategy. Propagate it to AGENTS, mandates, indexes, quality gates, runtime plan, systems contracts, FAQ, and the HFI report.
Rejected Alternatives: A broad runtime refactor was rejected because it would create refactor-loop risk without owner/profiler proof. Chat-only advice was rejected because agents follow stable docs, not conversation memory.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; route cards stop low-end devices losing time to hidden global churn while allowing high/ultra VISUAL_SYNC consumers only after gameplay authority is flat.
Hardware Impact: 0 us runtime in this pass; future microsecond savings require profiler evidence after route-card-driven migrations.

## Decision 010 - Split Route Card Into Reusable Template
Problem: The operating model contained route-card fields, but agents need a copy/paste template and reviewer rejection checklist that can be used directly in status, rationale, PR notes, and domain docs.
Solution: Create `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md` with a full card, approval rules, instrument-specific minimums, accepted/rejected examples, and storage rules; propagate the template into AGENTS, `.codexrules`, mandate registry, registry/signal/native-memory/QA/telemetry mandates, architecture/doc indexes, quality gates, systems contracts, runtime plan, root anchors, FAQ, project x-ray, actuality ledger, and HFI report.
Rejected Alternatives: Leaving the template embedded in the operating model was rejected because it is easy to skip and harder to cite as a merge blocker. Runtime edits were rejected because the user requested documentation/governance and no owner-specific route is being implemented here.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; the template forces cadence, capacity, and `GlobalQualityWeight` behavior before a route can steal low-end frame budget or high-end visual budget.
Hardware Impact: 0 us runtime in this pass; no player-frame claim without profiler proof.

## Decision 011 - Add Owner-Local-First Setup Playbook
Problem: Route cards define review fields, but agents also need the implementation sequence: when to stay owner-local, when to extract an interface, when to add registry, when to add SignalBus, when to add DataVault, and when HectonEventBus is legitimate.
Solution: Create `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md` with architecture planes, ten-step subsystem setup, scenario recipes, review cadence, and static checks. Propagate it into AGENTS, `.codexrules`, mandate registry, registry/signal/native-memory/QA mandates, docs indexes, quality gates, systems contracts, runtime plan, root anchors, FAQ, project x-ray, actuality ledger, and HFI report.
Rejected Alternatives: Leaving setup order implicit in the operating model was rejected because agents under time pressure will jump straight to global routes. Runtime edits were rejected because this pass defines governance, not a specific subsystem migration.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; owner-local-first keeps low-end frame budget protected and allows high-end visual overkill only as VISUAL_SYNC consumers after authority is stable.
Hardware Impact: 0 us runtime in this pass; no player-frame claim without profiler proof.

## Decision 012 - Close Setup-Playbook Citation Gaps
Problem: The setup playbook existed, but some high-traffic entry points still linked only to boundary/ledger/template docs. A rushed agent could read the prohibition layer without reading the implementation order.
Solution: Add direct setup-playbook links to `GLOBAL_AUTHORITY_BOUNDARIES.md`, `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`, `HECTON_PHI_REPORT.md`, `SIGNAL_UNIFICATION_AUDIT.md`, `PROJECT_ATLAS.md`, and telemetry mandate cross-references. Add explicit signal-audit text that new signal routes start owner-local first.
Rejected Alternatives: Relying on docs indexes was rejected because agents usually enter through grep hits and active reports, not a single table of contents. Runtime edits were rejected because this is governance propagation only.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; direct links reduce accidental global surface growth before it creates hidden frame-budget and memory-budget costs.
Hardware Impact: 0 us runtime in this pass; no runtime code changed.

## Decision 013 - Add Review Disposition Gate
Problem: Route cards define required fields, but they do not force a reviewer to state whether a global route is accepted, blocked, wrong-instrument, or architectural poison.
Solution: Create `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md` with `GREEN`/`YELLOW`/`RED`/`KILL` outcomes, a fast decision matrix, immediate rejection triggers, instrument-specific checks, evidence requirements, and static orientation commands. Propagate `GREEN` review disposition as a merge requirement into AGENTS, `.codexrules`, mandate registry, registry/signal/native/QA/telemetry mandates, quality gates, systems contracts, runtime plan, docs indexes, reports, FAQ, x-ray, root references, and HFI report.
Rejected Alternatives: Relying on route cards alone was rejected because a complete-looking card can still select the wrong global instrument. Runtime edits were rejected because this change is governance/review discipline only.
Scalability potential: Low/Middle/High/Ultra impact is process/runtime-indirect; early rejection prevents low-end frame budget and high-end visual budget from being spent on accidental global monolith routes.
Hardware Impact: 0 us runtime in this pass; no runtime code changed.
