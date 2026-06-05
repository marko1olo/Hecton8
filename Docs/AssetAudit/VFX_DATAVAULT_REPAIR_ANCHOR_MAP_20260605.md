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
| MARINE_DWAKE_HANDLE | HectonMarineSnowRenderer.cs | 429;2560 | DATAVAULT_REWRITE_PRESENT | Preserve DataVault dynamic-wake handle and write-lock path; prove scanner compile Unity GC/profiler | SOURCE_REWRITE_PRESENT_PENDING_SCANNER_COMPILE_UNITY_PROOF |
| MARINE_PROPWASH_HANDLE | HectonMarineSnowRenderer.cs | 432;2763 | DATAVAULT_REWRITE_PRESENT | Preserve DataVault propwash event handle and write-lock path; prove scanner compile Unity GC/profiler | SOURCE_REWRITE_PRESENT_PENDING_SCANNER_COMPILE_UNITY_PROOF |
| MARINE_WAKE_SOURCE_BRIDGE | HectonMarineSnowRenderer.cs | 431;2984-3021 | DATAVAULT_REWRITE_PRESENT | Preserve WakeSource bridge through DataVault read/write paths; prove scanner compile Unity GC/profiler | SOURCE_REWRITE_PRESENT_PENDING_SCANNER_COMPILE_UNITY_PROOF |
| MARINE_710 | HectonMarineSnowRenderer.cs | 710 | EDITOR_OFFLINE_SCRATCH | Move under an Editor-only surface or document editor/offline owner route | STATIC_ONLY_EDITOR_OFFLINE_ROUTE_PENDING |
| MARINE_1948 | HectonMarineSnowRenderer.cs | 1948 | EDITOR_OFFLINE_SCRATCH | Move under an Editor-only surface or document editor/offline owner route | STATIC_ONLY_EDITOR_OFFLINE_ROUTE_PENDING |
| PLASMA_1483 | ShinobuPlasmaBeamRuntime.cs | 1483 | BLACK_BOX_DUMP_SNAPSHOT | Use bounded buffers without unmanaged scratch in gameplay hot paths | STATIC_ONLY_REVIEW_PENDING |

## Per-file Repair Interpretation
- Biolum black-box mirror route: Current source decision lines 311-315 keep the snapshot/write mirrors owner-local diagnostic scratch with Session lifetime, owner disposal, no gameplay authority, no cross-domain snapshot contract, and no blind DataVault migration. Remaining blockers are compile, Unity, GC/profiler, scanner recheck, and deterministic dump artifact proof.
- MarineSnow wake/profile route: Current source already uses DataVault handles/write-lock paths for mock wake, propwash, and wake-source bridge. Preserve that rewrite, prove it with scanner/compile/Unity/GC/profiler evidence, and keep editor/offline parse scratch isolated.
- PlasmaBeam dump serialization route: Ensure fault export uses bounded buffers and does not allocate unmanaged scratch without an approved owner path.

## Audit / Source Alignment
- Older audit JSON records historical MarineSnow `1347`/`2005` anchors, but current disk source has no `_mockWakeScratch`, no `_propwashEventScratch`, and no `EnsureRuntimeScratchBuffers()`.
- Current source readback shows DataVault handles at lines `429`, `432`, and `436`; mock wake write-lock path at `2560`; propwash write-lock path at `2763`; wake-source bridge at `2984-3021`.
- `HectonMarineSnowRenderer.cs:710` and `:1948` are editor/offline wake-profile scratch anchors.
- Do not perform runtime source repair against `MARINE_1948`; route it as editor/offline owner debt.

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
