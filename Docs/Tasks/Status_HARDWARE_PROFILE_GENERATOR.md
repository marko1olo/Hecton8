# Status_HARDWARE_PROFILE_GENERATOR

PROMPT IDENTIFIED: HARDWARE_PROFILE_GENERATOR
DOMAIN: Auxiliary Node (Data/Research)
TASK COUNT: 15 declared in original extracted XML; 7 numbered objectives present.
STATUS: HARDWARE PROFILED | COMPILE PENDING VERIFICATION

## Churn Notice
- [x] Workspace churn detected | DOD: `Docs/Tasks/CURRENT_BATCH.md` was replaced and no longer contains `HARDWARE_PROFILE_GENERATOR`; previous hardware status/log/profile artifacts disappeared from disk. | Alternatives Rejected: silently switching to another agent prompt. | Estimate: 0 us runtime.

## Mandates Loaded
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- ARCH_Execution_Phases.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- GPU_Compute_Warp_Sizing_Mobile.txt
- REND_VRS_MX350_Reality_Check.txt
- REND_Foveated_Simulation_LOD.txt
- CTRL_Device_Abstraction_Haptics.txt

## Core Checklist
- [x] Prompt extraction | DOD: CLI block extraction from `Docs/Tasks/CURRENT_BATCH.md` was completed before batch replacement; `CURRENT_BATCH_OSHINO.md` was absent. | Alternatives Rejected: archive batch reads, MCP-style partial reads. | Estimate: 40 us cold operator time.
- [x] Hygiene check | DOD: original status/rationale files were absent at session start; after external deletion they were recreated. | Alternatives Rejected: appending to another agent log. | Estimate: 10 us.
- [x] 1. SPECS RESEARCH | DOD: Quest 3, Steam Deck LCD, Quest 2 reference, and MX350 reference memory facts recorded in flat source arrays; derived bandwidth values are explicitly labeled. | Alternatives Rejected: false discrete VRAM for UMA hardware and unlabeled "exact" derived bandwidth. | Estimate: 80 us parser lookup after cold load.
- [x] 2. PROFILE JSON | DOD: created `Data/Hardware/Profiles.json` as flat columnar arrays. | Alternatives Rejected: nested profile objects, runtime C# API edits. | Estimate: 35 us cold parse per profile row after parser exists.
- [x] 3. TIER 0 TOASTER | DOD: Tier0 row defines 2 GB physical graphics memory, 1536 MB project graphics budget, 4 physical cores/4 hardware threads, low LOD/fog/shadow settings, Level1 sacrifice mask. | Alternatives Rejected: balanced middle profile. | Estimate: 1 us indexed lookup.
- [x] 4. TIER 3 ULTRA | DOD: Tier3 row defines 16 GB physical graphics memory, 12288 MB project graphics budget, 12 physical cores, visual overkill fields, tight 144 FPS phase thresholds. | Alternatives Rejected: bloated CPU simulation as "overkill". | Estimate: 1 us indexed lookup.
- [x] 5. BITMASK ASSIGNMENT | DOD: mapped masks to existing `HomeostasisBrain.SystemBit` values with stable bit indexes 4,5,6,7,8,9,10,12,20,21,22,23. | Alternatives Rejected: new single-use EventIDs or new public contract bits. | Estimate: <1 us bitwise mask test.
- [x] 6. WATCHDOG THRESHOLDS | DOD: `phaseCount=4`; profile and tier phase arrays validate as row-major `count * phaseCount` values. | Alternatives Rejected: single global phase budget. | Estimate: <1 us indexed lookup.
- [x] 7. DATA SOVEREIGNTY | DOD: syntax check passed; top-level values are scalars or arrays, no nested profile objects. | Alternatives Rejected: nested JSON object graph. | Estimate: 35 us cold parse per profile row after parser exists.

## Iterative Loop Log
- [x] Loop 1 - JSON syntax validation passed.
- [x] Loop 2 - Flat shape/count validation passed: profiles=2, phases=4, tiers=4, killBits=12.
- [x] Loop 3 - Mask arithmetic validation passed: `0x70`, `0x2007F0`, `0xF017F0`.
- [x] Loop 4 - Compile/build feasibility check: no `.sln`/`.csproj` found and `dotnet` is not installed in PATH; Unity executable also not discoverable from standard Hub path. Compile status remains PENDING VERIFICATION, not green.
- [x] Loop 5 - Polish mandate and final anti-bloat review: original active batch had no applicable `<POLISH_MANDATE>` for this data task; current batch replacement is a different prompt set.
- [x] Loop 6 - Professional self-review remediation: split physical graphics memory from graphics budget, added bandwidth derivation kinds/formulas, added Quest 2/MX350 source coverage, and validated every declared array count. Final validation: `FINAL_STRICT_PROFILE_VALIDATION_OK`.
- [x] Loop 7 - Source hygiene review: removed process-noise wording from source notes, revalidated source/profile/tier/reference array counts. Result: `FINAL_JSON_ARRAY_VALIDATION_OK`.
- [x] Loop 8 - Encoding and mask review: all hardware profile artifacts are ASCII-only; pressure masks and bit indexes print as expected. Result: `ASCII_OK`, masks `0x0/0x70/0x2007F0/0xF017F0`.
- [x] Loop 9 - Runtime key review: added FNV-1a `StableHash32` arrays for phases, profiles, tiers, and reference devices so C# consumers do not need string keys. Result: `HASHED_PROFILE_VALIDATION_OK`.
