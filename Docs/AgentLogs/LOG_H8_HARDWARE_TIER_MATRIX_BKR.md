# LOG - H8_HARDWARE_TIER_MATRIX_BKR

## 2026-05-14 - Hardware Profile Bake

Status: PROFILES BAKED / COMPILE TOOLCHAIN BLOCKED

What was wrong:
- `Data/System/Hardware_Profiles.json` did not exist.
- The batch required explicit target rows for `PC_High`, `SteamDeck_Mid`, `Quest2_Low`, and `Quest3_LowPlus`.
- SHI/SystemStress vasoconstriction thresholds were not encoded in the requested path.
- Quest2 had no local 4GB total system RAM self-audit in the requested profile file.

What was done:
- Created `Data/System/Hardware_Profiles.json`.
- Set file status to `PROFILES BAKED`.
- Added four profile rows with exact required override keys: `VramLimit`, `CpuLaneTokenRate`, `RenderScale`, `TextureMipBias`.
- Added `systemHealthIndex` metadata mapping `SystemStress` to `SystemHealthIndexSignal` semantics.
- Added vasoconstriction levels with sacrifice masks and sacrificed systems.
- Added monotonic per-profile `VasoconstrictSystemStressByLevel` thresholds.
- Added Quest2 self-audit: `SystemRamLimit` 4096 MB, `SystemRamBudget` 3840 MB, `SystemRamSafetyReserve` 256 MB, `TotalCommittedPlusReserve` 4096 MB, `DoesNotExceed4Gb` true.
- Created and updated `Docs/Tasks/Status_H8_HARDWARE_TIER_MATRIX_BKR.md`.
- Created and updated `Docs/AgentLogs/Rationale_H8_HARDWARE_TIER_MATRIX_BKR.md`.

Cinematic cheats used:
- No physical simulation added.
- Vasoconstriction sacrifices presentation and optional simulation systems first: caustics, particle advection, high-res fog, distant fauna steering, procedural sway, IK bracing, SSR, boid brain, non-critical VFX, foveated simulation tier, slow tick cadence, time dilation.
- Saved runtime budget is reserved for higher-tier visual overkill through later VISUAL_SYNC consumers, not through deeper simulation.

Exact microseconds saved:
- Measured savings: 0 microseconds. No profiler artifact exists.
- Claimed savings: 0 microseconds. This was a cold-data bake, not a hot-path optimization.
- Added hot-path cost: 0 microseconds/frame. No C# runtime path was added or modified.

Verification:
- JSON parse: PASS via `ConvertFrom-Json`.
- Required profiles: PASS.
- Required override keys: PASS.
- RenderScale range > 0 and <= 1: PASS.
- TextureMipBias non-negative: PASS.
- SHI threshold monotonicity: PASS.
- Release hysteresis minimum 0.08: PASS.
- Quest2 4GB cap: PASS, total committed plus reserve = 4096 MB.
- Trailing whitespace in `Data/System/Hardware_Profiles.json`: PASS.
- Compile: BLOCKED. `dotnet` command is unavailable, no `.sln`/`.csproj` exists, Unity 6000.4.1f1 executable was not found in common Hub paths.
- Unity import/Play Mode/profiler/GCMonitor: NOT VERIFIED.

REGRESSION MODEL:
- CPU: no runtime code changed; no hot-path execution added.
- GC: no Tick/Update allocation path added.
- Memory: new cold JSON file only; runtime memory unchanged until a future consumer loads it.
- Cadence: no dispatcher phase or tick cadence changed.
- Correctness: JSON is syntactically valid and contains required targets, override keys, SHI thresholds, and Quest2 cap audit.

HOT PATH IMPACT:
- None from this edit. Any future consumer must parse/select this profile during bootstrap P0 only.

FAILURE MODES:
- A future parser may expect columnar arrays instead of profile objects.
- A future runtime consumer may require exact integer render-scale milli values instead of float fractions.
- `CpuLaneTokenRate` has no existing source contract; unit is declared in JSON as 50 microseconds per token.
- Compile and Unity import remain unverified because the local toolchain is unavailable.

WHY KEPT / REJECTED:
- Kept cold JSON because the prompt requested a bake, and cold data avoids cross-agent runtime coupling.
- Rejected C# API changes because they would mutate existing hardware catalog ownership.
- Rejected overwriting `Data/Hardware/Profiles.json` because that existing generated file owns different profiles and reference devices.
- Rejected invented polish behavior; `<POLISH_MANDATE>` was absent from the batch file, so the manual anti-bloat pass was recorded instead.

Mandates followed:
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Execution_Phases.txt`
- `REND_VRS_MX350_Reality_Check.txt`

## 2026-05-14 - Escalation Hardening Pass

Status: PROFILES BAKED / STATIC PARITY VERIFIED / COMPILE TOOLCHAIN BLOCKED

What was wrong:
- The first file was syntactically valid but relied primarily on object-row profile data.
- Existing hardware profile data in the project uses flatter, columnar arrays; a future boot consumer could otherwise need object-shape parsing glue.
- `Quest3_LowPlus` used 6144 MB as `SystemRamLimit`, which was conservative but semantically weak because it looked like a physical/device-class limit rather than a budget.

What was done:
- Added root `profileCount`.
- Added `guardThresholds` for VRAM warning, VRAM critical, frame throttle, main thread cap, and suspicious hot-system budget.
- Added SHI `stressModel` weights with total weight = 1.0.
- Added `profileTable` columnar arrays mirroring all four profile rows.
- Added row-major arrays for vasoconstriction thresholds and stress actions.
- Corrected `Quest3_LowPlus.SystemRamLimit` to 8192 MB while retaining conservative budget/reserve split: 5120 MB budget + 3072 MB reserve.
- Re-ran strict parity audit: rows match table, hashes match exact IDs, stress rows are monotonic, stress weights sum to 1, Quest2 cap remains 4096 MB.

Cinematic cheats used:
- Same as first pass: threshold-driven sacrifice of presentation/optional systems before gameplay truth.
- No new simulation was added.

Exact microseconds saved:
- Measured savings: 0 microseconds. No profiler artifact exists.
- Added hot-path cost: 0 microseconds/frame. Still data-only.
- Potential future boot/runtime improvement: columnar lookup removes parser ambiguity, but no runtime savings are claimed without profiler evidence.

Verification:
- JSON parse: PASS.
- Columnar parity audit: PASS.
- Exact FNV1A32 hash audit: PASS.
- Stress weight sum: PASS, `1.0`.
- Quest2 cap: PASS, `4096 MB`.
- Compile: BLOCKED by missing local toolchain (`dotnet` missing, no `.sln`/`.csproj`, Unity Hub editor roots missing).

REGRESSION MODEL:
- CPU: unchanged at runtime.
- GC: unchanged at runtime.
- Memory: cold JSON is larger; runtime memory unchanged unless future boot code loads it.
- Cadence: unchanged.
- Correctness: improved by table/row parity and corrected Quest3 limit semantics.

HOT PATH IMPACT:
- None.

FAILURE MODES:
- Future loader must choose row objects or `profileTable` as source of truth. Rationale: `profileTable` is intended for boot/runtime, row objects for review.
- Compile/Unity import is still not verified because the editor/toolchain is absent.

WHY KEPT / REJECTED:
- Kept both row objects and columnar arrays to preserve human review and machine-friendly loading.
- Rejected runtime code generation; it would exceed a profile-bake task and require compile verification that this machine cannot perform.

## 2026-05-15 - Static Guard Tooling Pass

Status: PROFILES BAKED / STATIC TOOLING VERIFIED / COMPILE TOOLCHAIN BLOCKED

What was wrong:
- `Data/System/Hardware_Profiles.json` had embedded self-audit data but no reusable fail-closed guard.
- Future edits could silently break `profileTable` parity, FNV hashes, SHI threshold ordering, release hysteresis, stress weights, or the Quest2 4GB cap.
- `Docs/Tasks/CURRENT_BATCH.md` no longer contains the H8 prompt tag or a polish mandate, so the active batch file cannot be used as the current H8 task source.

What was done:
- Added `Tools/Hardware/ValidateSystemHardwareProfiles.py`.
- Added `Tools/Hardware/test_validate_system_hardware_profiles.py`.
- Generated deterministic audit report `Docs/AgentLogs/Hardware_Profile_Audit_H8_HARDWARE_TIER_MATRIX_BKR.json`.
- The guard validates required profile order, required override keys, exact FNV1A32 hashes, row/object to columnar table parity, row-major stress/action lengths, Quest2 RAM cap, SHI monotonicity, release hysteresis, stress model weight sum, and HECTON frame/VRAM guard thresholds.
- Recorded active batch drift in the H8 status/rationale instead of importing unrelated neighboring prompts.

Cinematic cheats used:
- No physical simulation was added.
- The baked response still sacrifices visual/presentation systems first: caustics, particle advection, high-res fog, distant fauna steering, procedural sway, IK bracing, SSR, boid brain, non-critical VFX, foveated simulation tier, slow tick cadence, and time dilation.
- Saved budget remains reserved for higher-tier VISUAL_SYNC overkill, not for deeper simulation truth.

Exact microseconds saved:
- Measured savings: 0 microseconds. No Unity profiler artifact exists.
- Claimed savings: 0 microseconds. This pass adds offline tooling only.
- Added hot-path cost: 0 microseconds/frame. No Unity runtime code was touched.

Verification:
- `python --version`: PASS, Python 3.14.0.
- `python -B Tools/Hardware/ValidateSystemHardwareProfiles.py --write-report`: PASS, `SYSTEM_HARDWARE_PROFILE_GUARD=PASS profiles=4 quest2_total_mb=4096 stress_weight_sum=1.000000 hot_path_us=0`.
- `python -B Tools/Hardware/ValidateSystemHardwareProfiles.py --check-report`: PASS.
- `python -B Tools/Hardware/test_validate_system_hardware_profiles.py -v`: PASS, 6 tests.
- `python -B -m py_compile Tools/Hardware/ValidateSystemHardwareProfiles.py Tools/Hardware/test_validate_system_hardware_profiles.py`: PASS.
- `python -B -m json.tool Data/System/Hardware_Profiles.json`: PASS.
- `python -B -m json.tool Docs/AgentLogs/Hardware_Profile_Audit_H8_HARDWARE_TIER_MATRIX_BKR.json`: PASS.
- Active batch tag audit: DRIFT DETECTED. H8 prompt tag and polish tag are absent from current `Docs/Tasks/CURRENT_BATCH.md`.
- Compile/Unity import/Play Mode/profiler/GCMonitor: NOT VERIFIED. Local `dotnet`/Unity toolchain remains unavailable from prior check.

REGRESSION MODEL:
- CPU: no runtime CPU path changed.
- GC: no runtime allocation path changed.
- Memory: one cold report JSON plus two Python tooling files; runtime memory unchanged.
- Cadence: no dispatcher phase or tick cadence changed.
- Correctness: improved by executable validation and negative tests.

HOT PATH IMPACT:
- None. Tooling runs outside Unity and the profile JSON remains cold data.

FAILURE MODES:
- If future legitimate profile values change, the validator must be updated in the same patch with rationale. Silent drift now fails.
- The guard cannot prove Unity import, scene wiring, profiler budgets, or GCMonitor output.
- The report is deterministic; `--check-report` will fail if profile data changes without regenerating the report.

WHY KEPT / REJECTED:
- Kept offline Python tooling because this is a data bake and can be verified without touching C# runtime ownership.
- Rejected runtime parser/codegen because it would be a broader integration task and cannot be compiled here.
- Rejected active-batch reinterpretation because the current batch file does not contain the H8 XML tag.

Mandates followed:
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Execution_Phases.txt`
- `REND_VRS_MX350_Reality_Check.txt`

## 2026-05-15 - Combined Hardware Guard Entry Point

Status: PROFILES BAKED / STATIC TOOLING VERIFIED / COMPILE TOOLCHAIN BLOCKED

What was wrong:
- `ValidateHardwareProfileCatalog.py` and `ValidateSystemHardwareProfiles.py` passed independently, but there was no single command for the Integrator to validate all hardware profile artifacts.
- Separate commands increase the chance that the generated runtime catalog is checked while the H8 system profile bake is skipped.

What was done:
- Added `Tools/Hardware/ValidateAllHardwareProfiles.py`.
- The combined guard imports the existing runtime catalog guard and the H8 system profile guard.
- It fails if either guard fails, and it checks the deterministic H8 audit report for drift.

Cinematic cheats used:
- No simulation was added.
- This remains a static hardware-profile safeguard; the profile data still prioritizes load-shedding presentation and optional systems before gameplay truth.

Exact microseconds saved:
- Measured savings: 0 microseconds. No Unity profiler artifact exists.
- Claimed savings: 0 microseconds.
- Added hot-path cost: 0 microseconds/frame. Offline Python only.

Verification:
- `python -B Tools/Hardware/ValidateAllHardwareProfiles.py`: PASS, runtime catalog profiles = 2, system profiles = 4, Quest2 committed+reserve = 4096 MB, hot path = 0 microseconds.
- `python -B Tools/Hardware/ValidateHardwareProfileCatalog.py`: PASS.
- `python -B Tools/Hardware/ValidateSystemHardwareProfiles.py --check-report`: PASS.
- `python -B Tools/Hardware/test_validate_system_hardware_profiles.py -v`: PASS, 6 tests.
- `python -B -m py_compile Tools/Hardware/ValidateAllHardwareProfiles.py Tools/Hardware/ValidateSystemHardwareProfiles.py Tools/Hardware/test_validate_system_hardware_profiles.py`: PASS.
- Compile/Unity import/Play Mode/profiler/GCMonitor: NOT VERIFIED. Local toolchain remains unavailable.

REGRESSION MODEL:
- CPU: no runtime code changed.
- GC: no runtime allocation path changed.
- Memory: one additional offline Python script; runtime memory unchanged.
- Cadence: no dispatcher phase or tick cadence changed.
- Correctness: improved by one-command static validation coverage.

HOT PATH IMPACT:
- None.

FAILURE MODES:
- The combined guard cannot prove Unity import, scene wiring, profiler budgets, or GCMonitor output.
- It is intentionally static; runtime hardware detection must still be verified in Unity on target devices.

WHY KEPT / REJECTED:
- Kept a separate aggregate script to avoid overloading the existing generated catalog guard.
- Rejected shell wrapper because Python gives portable structured output and direct access to both validators.
