# Status_HARDWARE_PROFILE_GENERATOR

PROMPT IDENTIFIED: HARDWARE_PROFILE_GENERATOR
DOMAIN: Auxiliary Node (Data/Research)
TASK COUNT: 15 declared in original extracted XML; 7 numbered objectives present.
STATUS: PER-DEVICE JSONS VALIDATED | COMPILE PENDING VERIFICATION

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
- [x] 8. RUNTIME CATALOG INTEGRATION | DOD: added `HardwareProfileCatalog` constants/methods and wired `HardwareTierDetector` UMA budget resolution to explicit Quest 3 / Steam Deck signatures with legacy fallback for unknown UMA. | Alternatives Rejected: runtime JSON parsing, managed arrays, existing interface mutation, treating every UMA device as Quest 3. | Estimate: <1 us cold switch lookup; 0 us/frame.
- [x] 9. TEXTURE CLAMP UPGRADE | DOD: `VRAMEnforcer` now resolves shared-memory mip clamps from profile texture budget: Steam Deck keeps mip limit 1, Quest 3/unknown UMA keep mip limit 2. | Alternatives Rejected: one-size shared-memory mip limit, Steam Deck quarter-res textures despite 2048 MB texture profile. | Estimate: <1 us cold bootstrap branch; 0 us/frame.
- [x] 10. RENDER SCALE SPLIT | DOD: `profileBaselineRenderScaleMilli` is in the JSON/catalog and `PlatformAdaptiveBudgetGovernor` routes shared-memory baseline render scale by profile: Quest 3 gets 0.85, Steam Deck/unknown UMA keeps 0.78, severe pressure clamps still override. | Alternatives Rejected: treating Quest 3 as Deck render scale by default, raising unknown UMA above conservative clamp, hardcoded governor-only scale constants. | Estimate: <1 us low-cadence branch; 0 us/frame hot path.
- [x] 11. TARGET FPS ROUTING | DOD: `GameBootstrapper.ResolveTargetFrameRate` now routes Quest 3 and Steam Deck through catalog target FPS constants. | Alternatives Rejected: fixed 60 FPS for Quest 3 despite the 72 FPS profile target; using max refresh as target. | Estimate: <1 us bootstrap branch; 0 us/frame.
- [x] 12. JOB WORKER BUDGET ROUTING | DOD: `GameBootstrapper.ConfigureJobWorkerThreads` now resolves Quest 3 and Steam Deck worker counts from `HardwareProfileCatalog` before clamping to `JobsUtility.JobWorkerMaximumCount`. | Alternatives Rejected: generic `processorCount - 1` for ARM XR and x86 SMT hardware alike. | Estimate: <1 us bootstrap branch; 0 us/frame.
- [x] 13. STREAMING MIP BUDGET ROUTING | DOD: `GameBootstrapper.ResolveStreamingMipBudgetMb` now resolves Quest 3 and Steam Deck texture streaming budgets from `HardwareProfileCatalog` before quality-tier fallback. | Alternatives Rejected: generic low/MX350 streaming budgets for profiled UMA devices. | Estimate: <1 us bootstrap branch; 0 us/frame.
- [x] 14. PROFILE VRAM THRESHOLDS | DOD: `VRAMBudgetThresholds.RuntimeDefault` now maps Quest 3 and Steam Deck graphics/texture/RT budgets from `HardwareProfileCatalog`; `VRAMMonitor` preserves custom serialized budgets and only replaces untouched MX350 defaults; `VRAMPressureMonitor` caches runtime thresholds and derives pressure bytes from budget fractions. | Alternatives Rejected: hardcoded MX350 absolute pressure bytes on profiled UMA hardware. | Estimate: slow-tick scalar math only; 0 us/frame hot path.
- [x] 15. SELF-REVIEW AND VERIFICATION | DOD: reran catalog guard, Python AST syntax parse, whitespace diff check, fixed duplicate using and stale tooltip text, confirmed old fixed VRAM pressure byte constants are absent, and attempted `dotnet build`. | Alternatives Rejected: chat-only report and false compile success. | Estimate: 0 us runtime.

## Hardening Checklist
- [x] PER-DEVICE JSON EXPORTS | DOD: added `Data/Hardware/HARDWARE_TIER_QUEST_3.json` and `Data/Hardware/HARDWARE_TIER_STEAM_DECK_LCD.json` as flat per-device mirrors of the aggregate catalog; validator checks field, phase, and sacrifice-threshold parity. | Alternatives Rejected: relying only on one aggregate file for a prompt that requested JSONs plural. | Estimate: data-only; 0 us/runtime.
- [x] PROFILE FRAME-PRESSURE BUDGET | DOD: `PlatformAdaptiveBudgetGovernor` now derives frame-pressure target time from catalog target FPS for Quest 3 and Steam Deck, and seeds the first frame-time sample to avoid startup false positives. | Alternatives Rejected: generic 16.67 ms pressure target for Quest 3's 72 FPS profile. | Estimate: low-cadence scalar math only; 0 us/frame hot path.
- [x] UNSET VRAM BUDGET RECOVERY | DOD: `VRAMBudgetThresholds.ResolveRuntimeBudget` now treats all-zero/unset serialized structs as runtime defaults while preserving deliberate non-default custom budgets. | Alternatives Rejected: preserving invalid zero budgets that would break pressure utilization. | Estimate: cold Awake branch only; 0 us/frame.
- [x] STATIC STRUCTURAL REVIEW | DOD: changed C# files passed local brace/string/comment structural scan and duplicate-using scan. | Alternatives Rejected: relying only on Python catalog guard after C# edits. | Estimate: offline tooling only; 0 us/runtime.
- [x] VALIDATOR FAILURE HYGIENE | DOD: split JSON parity guard now reports missing profile rows/fields as validation errors instead of Python tracebacks. | Alternatives Rejected: brittle guard failure output. | Estimate: offline tooling only; 0 us/runtime.

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
- [x] Loop 10 - Final contract validation: no nested objects, declared counts match, artifacts exist and are ASCII-only. Result: `FINAL_PROFILE_CONTRACT_VALIDATION_OK`; `ARTIFACT_ASCII_AND_EXISTS_OK`.
- [x] Loop 11 - Hash metadata review: declared `stableHashAlgorithm=FNV1A32_ASCII` and corrected `generatedUtc` from placeholder midnight to actual UTC generation time. Result: `HASH_ALGORITHM_DECLARED_OK`.
- [x] Loop 12 - Refresh-rate ambiguity review: added `profileTargetFpsKind`, `profileRefreshHzNominal`, and `profileRefreshHzMax` so Quest 3 project target is not confused with hardware maximum. Result: `REFRESH_METADATA_VALIDATION_OK`.
- [x] Loop 13 - Full field-count validation: every profile/tier/reference/source array matches declared row counts; refresh target bounds validated. Result: `FULL_PROFILE_FIELD_VALIDATION_OK`; ASCII final check `ASCII_FINAL_OK`.
- [x] Loop 14 - Source authority review: added flat `sourceAuthorityRank` values so official vendor sources and secondary spec tables are distinguishable without nested metadata. Result: `SOURCE_AUTHORITY_RANK_VALIDATION_OK`.
- [x] Loop 15 - Exhaustive declared-array audit: every phase/profile/tier/pressure/kill/reference/source array count matches its declared count; kill masks reconstruct exactly; `git diff --check` clean. Result: `EVERY_DECLARED_ARRAY_COUNT_OK`; `KILL_MASK_RECONSTRUCTION_OK`.
- [x] Loop 16 - CPU core semantics review: added `profileCpuCoreCountKind` so Quest 3's Qualcomm 4+2 performance-core count is not confused with Steam Deck x86 physical cores. Result: `CPU_CORE_KIND_VALIDATION_OK`.
- [x] Loop 17 - CPU thread semantics review: added `profileCpuHardwareThreadKind` so Quest 3's no-SMT performance core count is not confused with Deck SMT hardware threads. Result: `CPU_THREAD_KIND_VALIDATION_OK`.
- [x] Loop 18 - Post-CPU-semantics validation: profile arrays revalidated with new CPU kind fields; `git diff --check` clean. Result: `POST_CPU_SEMANTICS_FULL_VALIDATION_OK`.
- [x] Loop 19 - Source rank legend review: added `sourceAuthorityRankLegend` while preserving flat layout. Result: `SOURCE_AUTHORITY_LEGEND_VALIDATION_OK`.
- [x] Loop 20 - Semantic metadata final check: CPU kind fields and source rank legend validated together. Result: `SEMANTIC_METADATA_FINAL_OK`.
- [x] Loop 21 - Runtime implementation pass: created `Assets/_Project/Scripts/Core/HardwareProfileCatalog.cs` and `.meta`; catalog mirrors profile hashes, UMA budgets, texture/RT budgets, worker budgets, phase budgets, and pressure masks without arrays or heap parsing.
- [x] Loop 22 - Runtime integration validation: `HardwareTierDetector.RecommendedVramBudgetMegabytes` now resolves Steam Deck-like UMA to 4096 MB, Quest 3-like UMA to 1536 MB, and unknown UMA to the prior 960 MB fallback. Result: `PROFILE_AWARE_CATALOG_PARITY_OK`; hot-path scan found no `new`, collections, LINQ, Update/Tick, coroutine, Debug.Log, or Unity search calls.
- [x] Loop 23 - Accuracy correction: added explicit `IsQuest3Like` detection and rejected broad non-Deck UMA equals Quest 3 logic. Result: unknown UMA remains conservative.
- [x] Loop 24 - Texture clamp implementation: `HardwareProfileCatalog.ResolveSharedMemoryTextureBudgetMegabytes` is profile-aware; `VRAMEnforcer.ResolveMinimumTextureMipLimit` uses the catalog to keep Steam Deck at half-res clamp and Quest/unknown UMA at shared-memory clamp. Result: `PROFILE_TEXTURE_CLAMP_WIRING_OK`.
- [x] Loop 25 - Texture clamp review: call-site search confirms only the new signature is used; hot-path scan remains clean for the patched files. Compile remains PENDING VERIFICATION due absent local toolchain.
- [x] Loop 26 - Persistent catalog parity guard: added `Tools/Hardware/ValidateHardwareProfileCatalog.py` so JSON/C# drift is caught by a tracked command instead of a one-off inline script. Result before render-scale constants: `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=19`.
- [x] Loop 27 - Render-scale split: `profileBaselineRenderScaleMilli=[850,780]` is now JSON-backed and catalog-backed; `PlatformAdaptiveBudgetGovernor` consumes `HardwareProfileCatalog.Quest3BaselineRenderScaleMilli` and `SteamDeckLcdBaselineRenderScaleMilli`. Guard script checks this consumer.
- [x] Loop 28 - Guard output correction: validator no longer prints a hardcoded constant count. Result: `HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21`.
- [x] Loop 29 - Target FPS routing: `GameBootstrapper` now returns `HardwareProfileCatalog.Quest3TargetFps` for Quest 3-like hardware and `SteamDeckLcdTargetFps` for Steam Deck-like hardware. Guard script checks both routes. Result: `BOOT_TARGET_FPS_CATALOG_ROUTE_OK`.
- [x] Loop 30 - Job worker routing: `GameBootstrapper` now resolves `Quest3JobWorkerBudget=4` and `SteamDeckLcdJobWorkerBudget=6` from the catalog, then clamps against Unity's maximum worker count. Guard script checks both routes. Result: `BOOT_JOB_WORKER_CATALOG_ROUTE_OK`.
- [x] Loop 31 - Streaming mip budget routing: `GameBootstrapper` now resolves Quest 3 streaming mips from `Quest3TextureBudgetMegabytes=768` and Steam Deck from `SteamDeckLcdTextureBudgetMegabytes=2048`; quality-tier fallback remains for unprofiled hardware. Result: `BOOT_STREAMING_MIP_CATALOG_ROUTE_OK`.
- [x] Loop 32 - Profile VRAM thresholds: `VRAMBudgetThresholds`, `VRAMMonitor`, and `VRAMPressureMonitor` now use catalog-backed profile budgets for known hardware; pressure thresholds scale from runtime budget fractions instead of fixed MX350 bytes. Result: `PROFILE_VRAM_THRESHOLD_ROUTE_OK`.
- [x] Loop 33 - Final implementation review: duplicate `using System;` and stale MX350-only tooltip text were corrected; profile guard and AST parse still pass; `dotnet build` remains blocked by missing `dotnet`. Result: `SELF_REVIEW_COMPLETE_COMPILE_PENDING`.
- [x] Loop 34 - Frame-pressure hardening: `PlatformAdaptiveBudgetGovernor.ResolveTargetFrameTimeMs` now derives Quest 3 and Steam Deck pressure targets from catalog FPS constants; first frame-time sample seeding prevents false pressure after static reset. Result: `PROFILE_FRAME_PRESSURE_ROUTE_OK`.
- [x] Loop 35 - Unset budget hardening: `VRAMBudgetThresholds.ResolveRuntimeBudget` now recovers all-zero serialized budget structs to runtime defaults and the guard checks that path. Result: `UNSET_VRAM_BUDGET_RECOVERY_OK`.
- [x] Loop 36 - Static structural review: changed C# files passed brace/string/comment structural scan and duplicate-using scan; Unity/.NET toolchain remains absent. Result: `STATIC_CSHARP_STRUCTURE_OK_COMPILE_PENDING`.
- [x] Loop 37 - Per-device JSON export: added Quest 3 and Steam Deck LCD split JSON files and extended the validator to compare them against `Profiles.json`. Result: `SPLIT_HARDWARE_JSON_PARITY_OK`.
- [x] Loop 38 - Validator failure hygiene: missing split profile fields now produce guard errors instead of Python exceptions. Result: `SPLIT_JSON_VALIDATOR_FAIL_FAST_OK`.
- [x] Loop 39 - Sequential final verification: reran catalog guard, Python AST/JSON syntax, and `git diff --check` sequentially after parallel tool timeouts. Result: `FINAL_STATIC_VERIFICATION_OK_COMPILE_PENDING`.

## Latest Verification

- Hardware catalog guard: `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` -> PASS (`HARDWARE_PROFILE_CATALOG_GUARD=PASS profiles=2 phases=4 masks=4 constants=21 split_jsons=2`).
- Python syntax: AST parse with `python -B -c` -> PASS. `python -m py_compile` was not used as evidence in this pass because Windows denied writing the existing `Tools\Hardware\__pycache__` bytecode file.
- Render-scale consumer guard: validator checks `PlatformAdaptiveBudgetGovernor` for catalog-backed Quest 3 / Steam Deck render-scale constants and `HardwareTierDetector.IsQuest3Like` route.
- Target-FPS consumer guard: validator checks `GameBootstrapper` for catalog-backed Quest 3 / Steam Deck target FPS constants.
- Job-worker consumer guard: validator checks `GameBootstrapper` for catalog-backed Quest 3 / Steam Deck job worker budget constants.
- Streaming-mip consumer guard: validator checks `GameBootstrapper` for catalog-backed Quest 3 / Steam Deck texture streaming budget constants.
- VRAM-threshold consumer guard: validator checks `VRAMBudgetThresholds`, `VRAMMonitor`, and `VRAMPressureMonitor` for profile-aware runtime thresholds.
- Frame-pressure consumer guard: validator checks `PlatformAdaptiveBudgetGovernor` for catalog-backed Quest 3 / Steam Deck frame target derivation.
- Unset-budget guard: validator checks `VRAMBudgetThresholds` recovers all-zero serialized thresholds to runtime defaults.
- Split JSON guard: validator checks both per-device JSON files for flat layout and parity with `Profiles.json`.
- Static C# scan: brace/string/comment structural scan PASS for 8 changed runtime C# files; duplicate-using scan PASS for the same files.
- Final static verification: sequential `python -B Tools\Hardware\ValidateHardwareProfileCatalog.py` PASS, AST/JSON syntax PASS, and `git diff --check` reports CRLF normalization warnings only.
- Unity/C# compile: PENDING VERIFICATION; `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` failed because `dotnet` is not recognized in this workspace shell.
