# VFX DataVault Sovereignty Repair Anchor Map

Status: `STATIC_ONLY_REVIEW_PENDING`
Evidence class: `STATIC_SOURCE_READBACK_DOC`

Mandates followed:
- .agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- .agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- .agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt
- telemetry.md
- systems.md
- performance.md

## Exact Source Anchors Table
| id | file | line | classification | required_action | verification_state |
|----|------|------|----------------|-----------------|--------------------|
| BIOLUM_319 | BiolumPulseSyncRuntime.cs | 319 | BLACK_BOX_DUMP_SNAPSHOT | Keep owner-local telemetry exception from source decision lines 311-315; prove compile Unity GC/profiler and dump artifact | SOURCE_DECISION_FIELDS_PRESENT_PENDING_COMPILE_UNITY_DUMP_PROOF |
| BIOLUM_336 | BiolumPulseSyncRuntime.cs | 336 | BLACK_BOX_DUMP_SNAPSHOT | Keep owner-local telemetry exception from source decision lines 311-315; prove compile Unity GC/profiler and dump artifact | SOURCE_DECISION_FIELDS_PRESENT_PENDING_COMPILE_UNITY_DUMP_PROOF |
| BIOLUM_384 | BiolumPulseSyncRuntime.cs | 384 | BLACK_BOX_DUMP_SNAPSHOT | Keep owner-local telemetry exception from source decision lines 311-315; prove compile Unity GC/profiler and dump artifact | SOURCE_DECISION_FIELDS_PRESENT_PENDING_COMPILE_UNITY_DUMP_PROOF |
| BIOLUM_3018 | BiolumPulseSyncRuntime.cs | 3018 | EDITOR_OFFLINE_SCRATCH | Move under an Editor-only surface or document editor/offline owner route | STATIC_ONLY_EDITOR_OFFLINE_ROUTE_PENDING |
| BIOLUM_3993 | BiolumPulseSyncRuntime.cs | 3993 | BLACK_BOX_DUMP_SNAPSHOT | Keep owner-local telemetry exception from source decision lines 311-315; prove compile Unity GC/profiler and dump artifact | SOURCE_DECISION_FIELDS_PRESENT_PENDING_COMPILE_UNITY_DUMP_PROOF |
| MARINE_673 | HectonMarineSnowRenderer.cs | 673 | RUNTIME_DIAGNOSTIC_MIRROR | Resolve as DataVault-owned or bounded generation-checked DataVault views | PENDING_SOURCE_REPAIR_AND_UNITY_PROOF |
| MARINE_674 | HectonMarineSnowRenderer.cs | 674 | RUNTIME_DIAGNOSTIC_MIRROR | Resolve as DataVault-owned or bounded generation-checked DataVault views | PENDING_SOURCE_REPAIR_AND_UNITY_PROOF |
| MARINE_712 | HectonMarineSnowRenderer.cs | 712 | EDITOR_OFFLINE_SCRATCH | Move under an Editor-only surface or document editor/offline owner route | STATIC_ONLY_EDITOR_OFFLINE_ROUTE_PENDING |
| MARINE_1347 | HectonMarineSnowRenderer.cs | 1347 | RUNTIME_DIAGNOSTIC_MIRROR | Resolve as DataVault-owned or bounded generation-checked DataVault views | PENDING_SOURCE_REPAIR_AND_UNITY_PROOF |
| MARINE_2005 | HectonMarineSnowRenderer.cs | 2005 | EDITOR_OFFLINE_SCRATCH | Move under an Editor-only surface or document editor/offline owner route | STATIC_ONLY_EDITOR_OFFLINE_ROUTE_PENDING |
| PLASMA_1483 | ShinobuPlasmaBeamRuntime.cs | 1483 | BLACK_BOX_DUMP_SNAPSHOT | Use bounded buffers without unmanaged scratch in gameplay hot paths | STATIC_ONLY_REVIEW_PENDING |

## Per-file Repair Interpretation
- Biolum black-box mirror route: Current source decision lines 311-315 keep the snapshot/write mirrors owner-local diagnostic scratch with Session lifetime, owner disposal, no gameplay authority, no cross-domain snapshot contract, and no blind DataVault migration. Remaining blockers are compile, Unity, GC/profiler, scanner recheck, and deterministic dump artifact proof.
- MarineSnow wake/profile scratch route: Resolve mock wake and propwash scratch as DataVault-owned or bounded generation-checked views. Ensure editor/offline parse scratch is isolated and not counted as runtime debt.
- PlasmaBeam dump serialization route: Ensure fault export uses bounded buffers and does not allocate unmanaged scratch without an approved owner path.

## Audit / Source Alignment
- `HectonMarineSnowRenderer.cs:712` is currently inside a local `#if UNITY_EDITOR` field block. It remains an anchor because the audit artifact records it as a forbidden persistent declaration, but current source readback supports editor/offline handling rather than runtime DataVault migration.
- `HectonMarineSnowRenderer.cs:1347` is runtime scratch through `EnsureRuntimeScratchBuffers()` and is used by `_mockWakeScratch` / `_propwashEventScratch` vault and GPU upload paths.
- `HectonMarineSnowRenderer.cs:2005` is inside the `#if UNITY_EDITOR` CSV reader region that starts at line `1952` and closes at line `2280`. The audit JSON already records `1347` as Runtime and `2005` as Editor.
- Do not perform runtime source repair against `MARINE_2005`; route it as editor/offline owner debt.

## Low / Middle / High / Ultra Consequences
- Low: preserve route readability and no hot allocation; no binary quality switch.
- Middle: same gameplay truth with conservative VFX staging.
- High: richer VFX only after DataVault/telemetry proof.
- Ultra: extra density/debug evidence only through continuous `GlobalQualityWeight`, without changing authority, DTO layout, save identity, or public contracts.

## Explicit Non-Claims
- no source repair performed
- no compile proof
- no Unity proof
- no profiler/GC proof
- no runtime dump proof
