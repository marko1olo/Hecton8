# Project Baseline

Date: 2026-06-02
Status: STATIC BASELINE
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: stable engineering entry point. This file is not a work log, task board, build-status page, report index, or prompt digest.

## Authority

- Source under `Assets/_Project` wins over dated reports.
- `AGENTS.md`, `.agents-skills/`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, and `Docs/ARCHITECTURE` define operating doctrine.
- `Docs/Reports` stores evidence snapshots. It is not a contract layer.
- `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` are historical storage.
- A report fact becomes active only after it is distilled into `Docs/ARCHITECTURE` or another stable contract.

## Root and Docs Boundary

- Repository root text anchors are limited to `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `textes.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, and standing root route bibles listed under `Routes` in `PROJECT_BIBLES.md`.
- `Docs/` root is for stable maps, governance, quality gates, project contracts, and architecture entry points.
- Generated/tool-required root paths must stay short and contract-shaped; full generated bodies belong in reports, regenerated artifacts, or deprecated snapshots.
- Large generated documentation artifacts belong in `Docs/Generated`.
- CSV authoring/tuning profiles belong in `Docs/Data/Profiles`.
- `Docs/Lore` is the narrative/content corpus, not implementation proof.
- `Docs/Marketing` is the public/commercial planning corpus; public copy still obeys root `textes.md` and proof gates.
- `Docs/Modding` is the mod/API planning and audit corpus; source and runtime artifacts decide actual API behavior.
- `Docs/Design`, `Docs/Audio`, `Docs/Atmosphere`, and `Docs/AI_Texturing_Templates` are support corpora. Promote durable engineering facts into `Docs/ARCHITECTURE` before using them as contracts.
- Dated notes, prompt extracts, report chains, task status files, work logs, generated evidence, local telemetry, and temporary scan counters do not belong in repository root or `Docs/` root.
- Current proof snapshots belong in `Docs/Reports` and the concise evidence sections of `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

## Stable Runtime Doctrine

- Persistent cross-domain native memory routes through `GlobalDataVault` and generation-checked handles.
- Private persistent `NativeArray`, `NativeList`, `NativeQueue`, raw pointer, or unsafe buffer fields in managers and `MonoBehaviour` types are debt unless a specific owner contract proves lifetime, disposal, and local scratch scope.
- Read accessors are pure: no allocation, scene search, publication, sync, job completion, global mutation, or hidden dependency resolution.
- `GlobalRegistry` is cold identity and dependency injection only.
- `SignalBus<T>` is the hot first-party broadcast route.
- `GlobalSignals` direct queues are legacy bridge lanes.
- `HectonEventBus` is managed mod/API isolation.
- `HomeostasisBrain.GlobalQualityWeight` is the continuous quality scalar. It may scale fidelity, cadence, capacity, and optional telemetry, but not gameplay truth ownership, DTO layout, save identity, or authority route.
- Burst/jobs are valid for amortized data-local batch work with dispatcher-owned completion windows. Tiny jobs, same-frame schedule/readback loops, and hidden `.Complete()` require profiler proof or removal.

## Stable Data Contracts

- Data Monolith runtime payload target: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Save writer version: `0x000B`.
- Current save header size: `56` bytes.
- AUP/blit layout: `48` bytes.
- AUP distance work subtracts sector/local coordinates in double before float local handoff.

## Current Static Project Envelope

Source-backed snapshot for agent onboarding:

- Unity editor version: `6000.4.1f1`.
- Primary project root: `Assets/_Project`.
- Enabled build-spine scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, `02_HECTON_WORLD`.
- Current production handoff route: `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
- Orbit status: `01_ORBIT` is enabled in BuildSettings and source-visible, but remains a standalone/YELLOW prologue route until `PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` is GREEN and root scene-flow authority is updated.
- First-party asmdef count under `Assets/_Project`: `171` in the 2026-06-01 static filesystem check.
- First-party script directory count under `Assets/_Project/Scripts`: `56` in the 2026-06-01 static filesystem check.
- URP package: `com.unity.render-pipelines.universal` `17.4.0`.
- Data Monolith payload is present at the target path and is `7,457,664` bytes, mtime 2026-06-07, in the 2026-08-05 static filesystem check; the 2026-06-01 check recorded `1,804,864` bytes.

Detailed source-backed runtime topology lives in `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`. Real-script system ownership lives in `Docs/ARCHITECTURE/SOURCE_SYSTEMS_REALITY_MAP.md`. Domain-to-architecture coverage lives in `Docs/ARCHITECTURE/DOMAIN_ARCHITECTURE_COVERAGE_MATRIX.md`. These facts are static topology only; they do not prove import, compile, route playthrough, profiler, GC, player build, platform, or visual readiness.

## Verification Language

- Use `PENDING VERIFICATION` unless a current artifact path proves the claim.
- Static source reads do not prove runtime behavior.
- CLI compile proves only the compiled source slice named by the log.
- Runtime readiness requires explicit Unity import, Console, Play Mode or player, profiler, GC/memory, shader/render, save/load, platform, and visual artifacts.

## Deprecated Material Boundary

Root-local telemetry, old FAQ/glossary prose, marketing binary storage, and stale domain planning bundles are no longer active project docs. Manifests:

- `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/MANIFEST.md`
- `Docs/DEPRECATED/Legacy_Domain_Bundles_2026-05-26/MANIFEST.md`
- `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/MANIFEST.md`

Archived facts must not be cited as active contracts. Promote only the current technical fact, then cite the archive path as evidence.
