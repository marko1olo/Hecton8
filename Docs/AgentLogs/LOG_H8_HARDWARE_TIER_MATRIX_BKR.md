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
