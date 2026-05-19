# LOG_HFI_AUDIT

## 2026-05-19 H-Phi Static Audit And Non-Compile Risk Review

What was wrong:
- User-requested "ash-fi" was not a literal project metric name. The current project metric is H-Phi.
- Two H-Phi tool families exist: PowerShell runtime static H-Phi and Python CalculateHPhi snapshots. Mixing their values creates false trend claims.
- Runtime proof remains absent: Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, scene wiring, and visual proof are still pending in stable docs.
- Production payloads are not populated: `_SourceData` is empty, `StreamingAssets` and root `AddressableAssetsData` are missing in the local filesystem scan.
- Forbidden third-party contamination remains imported: Easy Save 3, Master Audio, Astar, DOTween, Resources settings.
- H-Phi improved, but DataVault/native ownership backlog and content proof remain the actual project blockers.

What was done:
- Read `AGENTS.md`, domain map, task-relevant mandates, H-Phi metric contract, quality gates, proof matrix, latest H-Phi artifacts, and sub-agent findings.
- Recalculated latest static H-Phi from `Docs/AgentLogs/HPhi_SHINOBU_02_current2.json`:
  - `HPhiStaticNarrow = 0.119585803`
  - `HPhiStaticRisk = 0.009214659`
- Built trend table and ASCII graph in `Docs/Reports/2026-05-19_HFI_AUDIT_H_PHI_AND_PROJECT_RISK.md`.
- Identified non-compile risks: proof vacuum, missing payloads, vendor contamination, DataVault backlog, `.Complete()`/stall surfaces, UI/registry/hierarchy search risks, singleton drift, raw Update scripts under Assets, docs validation residue, marketing not executable.

Cinematic Cheats used:
- Audit-only. No runtime simulation, no visual feature, no physics. No cinematic cheat applied to code.
- Future direction recommends proof-first content payloads and visual fake budgets before new simulation work.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.
- Process-only savings: avoided a heavy full H-Phi scan while local CPU was observed at 100% with Unity active; avoided dotnet/build work because user asked for non-compilation problems.

Verification:
- Static artifacts parsed and formula recalculated.
- No compile, no Unity, no profiler, no GCMonitor.
- Status remains PENDING VERIFICATION for runtime readiness.

## 2026-05-19 Global Authority Boundary Documentation

What was wrong:
- The project has correct global primitives, but they can become four global god objects if boundaries are not explicit: `GlobalRegistry`, `SignalBus<T>`/`GlobalSignals`, `HectonEventBus`, and `GlobalDataVault`.
- Static source signals show danger-zone drift, not terminal failure: raw `GlobalRegistry.` hits are high, `GlobalSignals.cs` still mixes typed lanes and direct queues, `HectonEventBus` exists beside first-party buses, and DataVault migration is incomplete.

What was done:
- Created `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`.
- Linked it from `Docs/ARCHITECTURE/README.md` and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.
- Added anti-monolith guardrails to `Docs/ARCHITECTURE/GLOBAL_REGISTRY_SERVICE_LOCATOR.md`.
- Added EventBus/SignalBus boundary rules to `Docs/ARCHITECTURE/GLOBAL_SIGNAL_CORRIDOR.md`.
- Added no-regression global authority gate to `Docs/QUALITY_GATES.md`.
- Updated `.agents-skills/ARCH_Signal_Lane_Segregation.txt` with `HectonEventBus` and legacy `GlobalSignals` migration rules.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.
- Future savings are unmeasured until profiler proof exists; expected avoided costs are registry polling, managed hot event traffic, and unmanaged memory ownership leaks.

Verification:
- Static source/doc scan only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Global Authority Documentation Spine Extension

What was wrong:
- The first boundary document defined the rule, but the project still needed a migration ledger and cross-document propagation so future agents do not treat global authority as a convenience pattern.
- H-Phi documentation needed explicit anti-gaming language: adding SignalBus/DataVault references without ownership proof can improve a static score while worsening real architecture.

What was done:
- Added `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.
- Updated `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md` with the GlobalRegistry/SignalBus/GlobalSignals/HectonEventBus/DataVault split.
- Updated `Docs/ARCHITECTURE/DISPATCH_PIPELINE.md` with dispatcher-phase global authority limits.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with the global-authority anti-gaming overlay.
- Updated `Docs/SYSTEMS_CONTRACTS.md`, `Docs/README.md`, `Docs/Reports/README.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and the HFI report with stable links and boundaries.
- Updated `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt` so registry growth must satisfy the global-authority boundary and migration ledger.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.
- Future savings remain unmeasured until profiler evidence exists.

Verification:
- Static doc/link grep only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Global Authority Enforcement Mandate Propagation

What was wrong:
- Boundary docs existed, but old active text still risked promoting `HectonEventBus` into the gameplay bus and treating global surfaces as acceptable convenience infrastructure.
- Root roadmap/playtest anchors did not yet warn that H-Phi/global-authority work is static governance only, not runtime readiness.
- Mandates for native memory, QA evidence, and telemetry did not yet carry the full Registry/SignalBus/EventBus/DataVault proof split.

What was done:
- Updated `AGENTS.md` and `.codexrules/AGENTS.md` with cold-registry, first-party SignalBus, HectonEventBus mod/API/cold, and documented GlobalSignals bridge rules.
- Updated `.agents-skills/README.md`, `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `QA_Evidence_Text_Filter_Audit.txt`, and `DBG_Telemetry_Crash_Reporting_PostMortem.txt`.
- Updated `Docs/DOC_GOVERNANCE.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
- Replaced the stale `HectonEventBus` first-party-spine language in the static x-ray with the current mod/API/cold boundary.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.
- Future savings remain unmeasured until targeted runtime migrations are profiled.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Global Authority Review Checklist

What was wrong:
- Boundary, setup, operating model, and route card existed, but there was no single reviewer disposition gate for accepting, blocking, rejecting, or killing a global route.
- A route card can be complete and still choose the wrong instrument.

What was done:
- Created `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`.
- Added `GREEN`, `YELLOW`, `RED`, and `KILL` review outcomes.
- Added fast decision matrix, immediate rejection triggers, instrument-specific checks, evidence requirements, review commands, and review note template.
- Propagated the `GREEN` review requirement into AGENTS, .codexrules, mandate registry, registry/signal/native-memory/QA/telemetry mandates, quality gates, systems contracts, runtime plan, architecture/docs indexes, active reports, FAQ, project-state x-ray, root references, and HFI report.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Setup Playbook Citation Closure

What was wrong:
- The owner-local-first playbook existed, but several active entry points still routed readers only to boundary, ledger, or template docs.
- That left a procedural gap: a future agent could see what is forbidden without seeing the correct order for introducing registry, SignalBus, DataVault, or HectonEventBus routes.

What was done:
- Added direct `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md` references to `GLOBAL_AUTHORITY_BOUNDARIES.md` and `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.
- Added the setup playbook and route-card references to active H-Phi and signal reports.
- Added explicit signal-audit language: new signal routes start owner-local first, then interface, then typed lane only when fan-out/cross-domain need is proven.
- Updated `Docs/PROJECT_ATLAS.md` and telemetry mandate cross-references so assembly and crash/postmortem readers inherit the same setup order.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 AAA Global Authority Operating Model

What was wrong:
- Boundary docs and migration queues existed, but there was no single practical operating model for how to combine `GlobalRegistry`, `SystemDispatcher`, `SignalBus<T>`, `GlobalSignals`, `HectonEventBus`, `GlobalDataVault`, telemetry, and H-Phi.
- Without a route-card rule, future agents could still add global routes with vague justification.

What was done:
- Created `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md`.
- Added the core law: `one fact -> one owner -> one route -> one proof artifact`.
- Added mandatory route-card fields: owner, instrument, producer/consumer phase, cadence, payload/data shape, capacity, overflow/failure mode, telemetry, shutdown/disposal, and proof.
- Propagated the operating model into `AGENTS.md`, `.codexrules/AGENTS.md`, `.agents-skills/README.md`, registry/signal/native-memory/QA/telemetry mandates, architecture/docs indexes, runtime master plan, quality gates, systems contracts, root anchors, FAQ, project-state x-ray, actuality ledger, and the HFI report.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.
- Future savings are unmeasured and require profiler proof after targeted migration.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Global Authority Route Card Template

What was wrong:
- The route-card rule existed in the operating model, but it was not a standalone copy/paste review artifact.
- Without a concrete template, future agents could claim they followed the model while omitting cadence, capacity, telemetry, shutdown, or proof fields.

What was done:
- Created `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`.
- Added full route-card fields, approval rules, instrument-specific minimums, accepted SignalBus example, rejected registry example, and storage rules.
- Linked the template from `GLOBAL_AUTHORITY_OPERATING_MODEL.md`, `GLOBAL_AUTHORITY_BOUNDARIES.md`, `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`, architecture/docs indexes, AGENTS, .codexrules, quality gates, systems contracts, runtime plan, route-related mandates, root anchors, FAQ, project-state x-ray, actuality ledger, and HFI report.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Global Authority Setup Playbook

What was wrong:
- The project had boundaries, an operating model, and a route-card template, but the practical subsystem setup sequence was still spread across several docs.
- Without an owner-local-first playbook, future agents could jump directly to registry/signal/vault routes before proving the ownership boundary is real.

What was done:
- Created `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`.
- Documented architecture planes: identity, time, signal, bridge, mod/API, data, telemetry, and proof.
- Documented setup sequence: owner-local -> owner interface -> cold registry -> SignalBus fan-out -> GlobalSignals bridge -> DataVault shared native state -> HectonEventBus mod/API -> telemetry -> `GlobalQualityWeight` behavior -> proof ladder.
- Added scenario recipes for player state, save/native state, mod achievement events, and renderer visual response.
- Propagated the setup playbook into AGENTS, .codexrules, mandate registry, registry/signal/native-memory/QA mandates, architecture/docs indexes, quality gates, systems contracts, runtime plan, root anchors, FAQ, project-state x-ray, actuality ledger, and HFI report.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.

## 2026-05-19 Active Report Containment

What was wrong:
- Active historical H-Phi and signal reports could still be cited without the new global authority split.
- Project atlas and FAQ explained registry/signals, but did not yet point readers at the current boundary/migration docs.

What was done:
- Added global-authority anti-gaming override to `Docs/Reports/HECTON_PHI_REPORT.md`.
- Added current signal/event override to `Docs/Reports/SIGNAL_UNIFICATION_AUDIT.md`.
- Added global-authority assembly-edge rules to `Docs/PROJECT_ATLAS.md`.
- Added `HectonEventBus` boundary note to `Docs/TECHNICAL_FAQ.md`.

Cinematic Cheats used:
- None. Documentation-only architecture governance pass.

Exact Microseconds saved:
- 0 us runtime claimed. No runtime code changed.

Verification:
- Static grep/readback only.
- No compile, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, or scene wiring proof.
- Status remains PENDING VERIFICATION.
