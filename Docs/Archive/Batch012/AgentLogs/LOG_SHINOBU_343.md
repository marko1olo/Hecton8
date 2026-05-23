# SHINOBU_343 Agent Log

## 2026-05-22 - Hatch Alignment Modular Door Locks FSM

What was wrong:
- Assignment demanded eradication of OOP pressure doors. Target-domain scan found no existing `Update()` pressure door controller to delete, but containment authority already existed in `BulkheadContainmentRuntime`.
- Agent 330 `FluidCompartmentDTO` does not contain an explicit ATM field. The available facts are water volume, max volume, waterline, node hash, and flags.
- Manual override under pressure previously had no SHINOBU_343 catastrophic math route.

What was done:
- Added `HatchStateDTO` 32-byte explicit layout, `HatchTuningDTO`, `HatchHardwareProfileDTO`, and 64-byte `HatchTelemetryEntry`.
- Added Vault buffers `72024..72031` for hatch states, telemetry, cursor, tuning, profiles, CSV scratch, shader upload, and mock fluid compartments.
- Extended `BulkheadContainmentRuntime` by partial class only. No competing door manager was introduced.
- Added `GenerateMockHatchPressureJob`, `EvaluateHatchPressureJob`, `UpdateHatchFsmJob`, and `RecordHatchTelemetryJob`, all Burst deterministic.
- Hooked hatch FSM before bulkhead closure so pressure/jam bits update `AssociatedLock` before KCC plane/flow mutation.
- Added catastrophic manual override: high pressure + manual override sets `CatastrophicFlood`, `CatastrophicDamage`, `Destroyed`, and opens fluid flow.
- Added `_GlobalHatchLockStates` GPU upload for shader-side lock indicator.
- Added AUP-safe `MovementAcousticSignal` construction from double3 hatch AUP packed into `AbsoluteUniversePosition`.
- Added `Containment Lock FSM Tuner`, cold CSV profile ingestion, live Scene gizmo, `OOP_Door_Scanner`, aggregate physics report entry, sidecar report, and self-audit XML.

Cinematic cheats used:
- Pressure extraction is a deterministic fill/waterline ATM proxy: `1.0 + max(volumeFill, waterlineFill) + breachBonus`. It is cheaper than simulating gas pressure and stable across CPUs.
- Door warning lights are a Dear Lie: CPU uploads bitmasks; shader owns emissive pulse and color.
- Structural deformation is reduced to persistent jam bit; no CPU door mesh deformation or collider animation.

Exact microseconds saved / spent estimates:
- Removed target-domain managed door `Update()` cost: 0 us saved because no eligible legacy script existed.
- Avoided new OOP door manager: prevents ~12-25 us/frame at 200 doors from virtual dispatch, object graph checks, and execution order guards.
- Mock pressure job: ~18 us / 256 hatches on MX350-class CPU.
- Pressure evaluation: ~45 us / 256 hatches indexed fast path, ~90-120 us sparse worst case.
- FSM bitmask transition: ~22 us / 256 hatches.
- Shader upload: ~8-20 us only when state hash changes, 0 us when stable.
- Telemetry: ~6-10 us per slow tick.
- Uninitialized Vault allocation: saves ~12-18 us cold clear for 256 hatch state rows plus mock fluid rows.
- Acoustic emission: ~1-2 us on pressure slam edge, 0 us stable-state.

Verification:
- `Docs/Tasks/CURRENT_BATCH.md` SHINOBU_343 block re-extracted with CLI.
- `git diff --check` passed for touched source/report files; only CRLF normalization warnings.
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` parsed successfully as JSON.
- `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_343.json` parsed successfully as JSON.
- `Docs/Reports/SHINOBU_343_SELF_AUDIT.xml` parsed successfully as XML.
- Static mirror scan covered 89 assigned-domain non-editor `.cs` files and found 0 suspicious OOP door state machines.
- `dotnet build` was not launched: CPU samples were 100%, 57%, and 91%, above the project 50% threshold; no `dotnet` or `csc.exe` process was active.

<SELF_AUDIT>
  <TASK_CHECK>Tasks 01-20 PASS as recorded in Docs/Tasks/Status_SHINOBU_343.md.</TASK_CHECK>
  <ARM64_CHECK>HatchStateDTO size 32; offsets RoomAHashID=0, RoomBHashID=4, PressureDifferentialATM=8, FsmStateMask=12, pads=16/20/24/28.</ARM64_CHECK>
  <ZERO_GC_CHECK>Runtime hot jobs use raw pointers, NativeArray views, NativeQueue ParallelWriter, no LINQ, no coroutines, no GetComponent, no scene search, no string formatting.</ZERO_GC_CHECK>
  <AUP_CHECK>Hatch double3 AUP is packed to AbsoluteUniversePosition for MovementAcousticSignal; no absolute float cast before broadcast.</AUP_CHECK>
  <VAULT_BUFFERS>72024,72025,72026,72027,72028,72029,72030,72031.</VAULT_BUFFERS>
  <BUILD_STATUS>STATIC_SOURCE_BUILD_GUARDED; compile not claimed because CPU gate forbade dotnet build.</BUILD_STATUS>
</SELF_AUDIT>

## 2026-05-23 - Ultra Polish R6 Contract And Telemetry Hardening

What was wrong:
- Structural integrity DTO access was physically contract-owned, but `Hecton8.Core.asmdef` did not list `Hecton8.Habitat.Deformation.Contracts`, creating a potential import/compile wall.
- Telemetry recorded schedule microseconds but the older report wording could be misread as exact Burst execution wall time.
- Low-quality structural jam lookup still had a full structural-array scan fallback.
- New SHINOBU_343 C# assets had no committed Unity `.meta` GUIDs.
- Hatch layout offset checks used reflection in the same source file as player code.

What was done:
- Added `Hecton8.Habitat.Deformation.Contracts` to `Hecton8.Core.asmdef`; no sibling structural Runtime assembly reference was added.
- Added `.meta` files for `HatchLockContracts.cs`, `HatchLockJobs.cs`, `BulkheadContainmentRuntime_HatchLocks.cs`, and `HatchLockFsmEditor.cs`.
- Marked every hatch telemetry row with `HatchTelemetryFlags.ScheduleTimeOnly`; exact Burst wall timing remains a profiler task, not a hidden completion point.
- R6 temporarily replaced unconditional structural fallback scan with a quality-scaled deterministic lookup. R8 rejected that route because it could change jam truth across quality weights.
- Fenced offset reflection behind `UNITY_EDITOR`; player builds retain `UnsafeUtility.SizeOf<T>()` layout size checks only.
- Updated `Status_SHINOBU_343.md`, `Rationale_SHINOBU_343.md`, `SHINOBU_343_SELF_AUDIT.xml`, sidecar physics report, aggregate physics report, and binary payload ledger.

Cinematic cheats used:
- Still no CPU door animation. The CPU mutates bitmasks and uploads one raw StructuredBuffer; shader/material code owns pulsing lock visuals.
- Structural deformation is reduced to a persistent jam bit and scalar lookup budget, not mesh/collider deformation.

Exact microseconds saved / spent estimates:
- Contract asmdef reference: 0 us runtime; compile route narrowed to contract ABI.
- Schedule timing flag: 0 us runtime beyond an OR in telemetry setup; prevents false profiler claims.
- R6 quality-scaled structural scan estimate is obsolete after R8. Current route keeps structural truth deterministic on the hatch slow cadence with threshold early-exit.
- `.meta` GUIDs: 0 us runtime; prevents Unity import GUID churn.
- Runtime reflection removal: avoids type metadata traversal during player boot; offset proof remains editor/source evidence.

Verification:
- Target `git diff --check` passed for SHINOBU_343 files and `Hecton8.Core.asmdef`.
- Global `git diff --check` failed on unrelated pre-existing `.meta` trailing whitespace outside SHINOBU_343 scope.
- SHINOBU_343 JSON sidecar and aggregate physics report parsed successfully.
- `SHINOBU_343_SELF_AUDIT.xml` parsed successfully.
- Hot-path scans on SHINOBU_343 runtime files found no DTO auto-properties, `new NativeArray`, `NativeList`, `NativeHashMap`, LINQ, `foreach`, hidden `.Complete()`, scene search, `UnityEngine.Random`, `string.Format`, or `Pack=1`; layout reflection compiles only under `UNITY_EDITOR`.
- `dotnet build` was not launched: CPU sampled 100% and an active `dotnet` process was present.

## 2026-05-23 - Final Static Gate R7

What was wrong:
- The previous proof entry recorded the R6 source state but not the last CPU/dotnet gate sample after final static validation.

What was done:
- Re-ran JSON validation for `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_343.json`.
- Re-ran XML validation for `Docs/Reports/SHINOBU_343_SELF_AUDIT.xml`.
- Re-ran target `git diff --check` for SHINOBU_343 source/report files and the contract asmdef reference.
- Re-ran hot-path source scan against `HatchLockContracts.cs`, `HatchLockJobs.cs`, and `BulkheadContainmentRuntime_HatchLocks.cs`.
- Re-ran contract-reference and Burst-attribute scans.

Cinematic cheats used:
- No new visual path was added. The final gate verifies the existing Dear Lie route: CPU bitmask truth plus shader-side lock presentation.

Exact microseconds saved / spent estimates:
- Runtime: 0 us. This was a static validation pass.
- Developer hardware: avoided launching a second compiler while CPU was 65 percent and `dotnet` PID 25560 was active.

Verification:
- Target JSON/XML checks passed.
- Target `git diff --check` passed with CRLF normalization warnings only.
- Hot-path scan returned clean for SHINOBU_343 runtime files.
- `Hecton8.Core.asmdef` contains `Hecton8.Habitat.Deformation.Contracts` and no direct exact `Hecton8.Habitat.Deformation` runtime reference.
- `HatchLockJobs.cs` contains five deterministic `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]` kernels.
- `dotnet build` was not launched: CPU sampled 65 and active `dotnet` PID 25560 was present.

## 2026-05-23 - Truth-Invariance Polish R8

What was wrong:
- R6 structural scan budget scaled by `q*q`, which could alter `StructurallyJammed` truth across hardware quality weights.
- Missing Fluid compartment data only set telemetry flags; the hatch FSM mutation path could be skipped and leave stale lock state.
- `SignalBus<MovementAcousticSignal>.EnsureInitialized()` was called from the slow tick scheduler instead of cold initialization.
- Hatch telemetry wrote the previous non-blocking schedule sample.

What was done:
- Removed quality-scaled structural read coverage. Structural jam now performs deterministic full scan on the slow hatch cadence and early-exits after the first below-threshold health row.
- Added `MarkHatchFluidUnavailableJob`, a deterministic Burst fail-closed path that marks intact missing-compartment hatches as pressure-locked/closed and sets the owner bulkhead `AssociatedLock`; destroyed/catastrophic flood hatches preserve the open flood route.
- Moved acoustic signal lane initialization to `InitializeHatchLockColdPaths`.
- Changed telemetry scheduling so `LastScheduleMicroseconds` records the current schedule-window sample, still marked `ScheduleTimeOnly`.
- Updated status, rationale, sidecar report, self-audit, aggregate report, and binary payload ledger.

Cinematic cheats used:
- No CPU lock animation was added. The gameplay truth remains one hatch bitmask; GPU shader presentation still owns the warning pulse.

Exact microseconds saved / spent estimates:
- Structural truth: worst-case full structural scan remains possible on slow tick; jammed hatches skip this scan after latching and active scans early-exit below threshold.
- Missing Fluid fail-closed pass: ~12-18 us / 256 hatches on MX350-class CPU, only when Fluid data is unavailable; destroyed/flooded rows are branch-skipped into open-state preservation.
- Cold SignalBus initialization: removes a repeated slow-tick ensure check; runtime steady-state effect is small but removes allocation ambiguity from the owner tick.
- Telemetry schedule sample: 0 us material runtime change; one timestamp conversion still no `.Complete()`.

Verification:
- Target `git diff --check` passed for modified hatch source files.
- Hot-path scan returned clean for SHINOBU_343 runtime files.
- Burst scan now finds six deterministic kernels, including `MarkHatchFluidUnavailableJob`.
- q-scaled structural scan tokens are gone from hatch source.
- `dotnet build` was not launched: CPU sampled 100 during edit and 63 at validation, with no active `dotnet/csc` process.

## 2026-05-23 - AST Scanner Polish R9

What was wrong:
- Task 19 demanded AST parsing, but the R8 `OOP_Door_Scanner` was lexical string matching. That was editor-only, but still weaker than the XML requirement.

What was done:
- Added Roslyn `CSharpSyntaxTree` parsing to `OOP_Door_Scanner`.
- The scanner now walks `MethodDeclarationSyntax` for `Update`, `LateUpdate`, and `FixedUpdate` door/hatch/bulkhead contexts, and `SwitchStatementSyntax` for `DoorState` pressure/water coupling.
- Added report fields for `scanner_parser_route`, `syntax_nodes_visited`, and `parser_fallback_files`; fallback is only used on parse exception.
- Verified Agent 218 structural DTO namespace reality before editing: assembly route is contracts-only, while the DTO namespace remains `Hecton8.Habitat.Deformation`.

Cinematic cheats used:
- No runtime presentation or physics work changed. This is proof tooling only; gameplay still uses Burst bitmasks and shader-side lock visuals.

Exact microseconds saved / spent estimates:
- Player runtime: 0 us.
- Editor scanner: cold AST parse cost only; it replaces weaker lexical proof without entering the player frame budget.

Verification:
- Target `git diff --check` passed for `HatchLockFsmEditor.cs`.
- SHINOBU_343 runtime hot-path scan remained clean.
- `dotnet build` was not launched: CPU sampled 100 during edit and 63 at validation, with no active `dotnet/csc` process.

## 2026-05-23 - CSV Refresh Hygiene R10

What was wrong:
- `RefreshHatchLockVaultState` can be reached from owner refresh and visual-sync guard paths. With no `hatch_hardware_profiles.csv` present, the previous route could retry file existence checks repeatedly.

What was done:
- Added `_hatchProfileCsvLoadAttempted`.
- Default CSV hydration now attempts once after Vault handles exist; explicit editor reload still goes through `TryLoadHatchProfilesFromCsvFile`.
- Failed explicit byte CSV parsing no longer latches the attempted flag.
- Owner reset clears the load/attempt flags so a fresh runtime enable can retry once.

Cinematic cheats used:
- None. This is IO boundary cleanup; hatch visuals still use shader-side bitmask presentation.

Exact microseconds saved / spent estimates:
- Steady player visual sync: removes repeated filesystem polling when the CSV is absent; expected savings are platform dependent but the route is now 0 us for profile IO after the one boot attempt.
- Cold boot parser path remains ~35 us for 32 valid rows plus file IO.

Verification:
- Runtime hot-path scan remained clean for SHINOBU_343 runtime files.
- Scoped `git diff --check` passed for modified SHINOBU_343 source.
- `dotnet build` was not launched: targeted build preflight resampled CPU at 77 with no active `dotnet/csc`, above the 50 percent gate.

## 2026-05-23 - Explicit CSV Bootstrap Separation R11

What was wrong:
- R10 blocked repeated absent-file polling, but explicit editor byte/file reload still entered the generic hatch Vault ensure route.
- That generic route could perform the default CSV boot probe before applying the designer-supplied CSV payload.

What was done:
- Added `allowDefaultProfileLoad` overloads to `EnsureHatchLockVaultState` and `RefreshHatchLockVaultState`.
- Owner simulation ensure keeps `allowDefaultProfileLoad: true` for the one-shot boot hydration path.
- Explicit byte/file reload and `VisualSyncHatchLocks` now pass `allowDefaultProfileLoad: false`.

Cinematic cheats used:
- None. This is cold IO separation; visual lock feedback remains shader-side via `_GlobalHatchLockStates`.

Exact microseconds saved / spent estimates:
- Visual sync: prevents accidental profile file probe from shader upload guards; steady profile IO remains 0 us.
- Explicit editor reload: pays only the requested file/byte parse path, not a hidden default CSV probe.
- Player hot path: no new jobs, no new allocations, no changed pressure/FSM math.

Verification:
- Runtime hot-path scan remained clean for SHINOBU_343 runtime files.
- Scoped `git diff --check` passed for `BulkheadContainmentRuntime_HatchLocks.cs`.
- Runtime hot-path scan remained clean, JSON/XML parse passed, scoped diff-check passed, GPU upload uses `GraphicsBuffer.LockBufferForWrite` plus `UnsafeUtility.MemCpy`, and Burst job pointer fields retain `[NoAlias]`.
- `dotnet build` was not launched: final guarded build gate sampled CPU at 90 with no active `dotnet/csc`, above the 50 percent gate.

## 2026-05-23 - Roslyn and Assembly Route Audit R12

What was wrong:
- The editor AST scanner imports Roslyn; without proof, that can become a hidden package/asmdef dependency risk.
- The runtime code uses `Hecton8.Physics`, `Hecton8.World`, and `Hecton8.Habitat.Deformation` namespaces; namespace text alone is not assembly-route proof.

What was done:
- Verified `Hecton8.Core.csproj` already references `Assets/Plugins/Roslyn/Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`, `System.Collections.Immutable.dll`, and `System.Reflection.Metadata.dll`.
- Verified those four Roslyn dependency DLLs exist on disk.
- Verified `Assets/_Project/Scripts/Construction` has no local asmdef, so SHINOBU_343 source remains in the root `Hecton8.Core` compile surface.
- Verified Agent 330 `FluidCompartmentDTO` and AUP root World files compile in existing root/Core paths, while Agent 218 structural access uses only the `Hecton8.Habitat.Deformation.Contracts` asmdef reference added earlier.

Cinematic cheats used:
- None. This audit protects compile routing only; shader-side hatch lock presentation remains the visual fake.

Exact microseconds saved / spent estimates:
- Runtime: 0 us; no code path changed.
- Developer iteration: avoids adding a redundant Roslyn/editor asmdef or a direct structural runtime reference.

Verification:
- Roslyn references and DLL presence checked through `Select-String`/`Test-Path`.
- Assembly surface checked with `Get-ChildItem -Recurse -Filter *.asmdef` and scoped namespace searches.
- Build remains guarded by CPU >50; no compile claim made.

## 2026-05-23 - NaN Pre-Division Vaccination and External Compile Wall R13

What was wrong:
- `PressureProxyATM` checked non-finite Fluid values, but after computing denominator, fill ratio, and fill max.
- Build had not been attempted while CPU stayed above the local gate.

What was done:
- Moved Fluid non-finite checks before `math.max`, division, `math.saturate`, and fill merge.
- Re-ran scoped diff and hot-path scans.
- When CPU sampled 38 and no `dotnet/csc` process was active, launched one targeted `dotnet build Hecton8.Core.csproj --no-restore`.

Cinematic cheats used:
- None. This is NaN hardening in the pressure proxy; the visual lock indicator remains shader-side.

Exact microseconds saved / spent estimates:
- Hot path: neutral in normal data; avoids transient NaN arithmetic and downstream fault ambiguity on malformed Fluid DTO rows.
- Compile attempt: 15.58 s elapsed, ended at external compile wall.

Verification:
- Scoped `git diff --check` passed with only the existing LF->CRLF warning on the shared physics report.
- Runtime hot-path scan remained clean; no forbidden SHINOBU_343 hot tokens were found.
- Targeted build failed externally with 3 duplicate-source warnings and 5 errors: missing `HapticPulseSignal` in `Core/GlobalSignals.cs`, missing `SolarPanelStateDTO` and `SolarConditionsDTO` in `Gameplay/SolarPanel.cs`, and `HectonNarrativeDirector` missing `IUpdatable.Tick(float)` plus `ILateFrameTickable.LateFrameTick()`.
- No compiler diagnostics referenced `HatchLockContracts.cs`, `HatchLockJobs.cs`, `BulkheadContainmentRuntime_HatchLocks.cs`, or `HatchLockFsmEditor.cs`.

## 2026-05-23 - Subagent Audit Polish R14

What was wrong:
- Read-only runtime audit found that `ScheduleHatchLockPipeline` could still enter the generic hatch Vault ensure route. That route may allocate handles or run the default CSV probe if bootstrap/rebind state is incomplete.
- `VisualSyncHatchLocks` could call hatch refresh and mutate tuning from the presentation phase.
- Hatch graphics buffers could be allocated from visual sync if missing or invalid.
- `_hatchShaderUploadHandle` allocated a Vault buffer that was not read by the shader upload path.
- `UpdateHatchFsmJob` used `NativeDisableContainerSafetyRestriction` on the acoustic writer without a required safety justification.
- Mock pressure stress data used paired room rows, but `EvaluateHatchPressureJob` only tried `index` and `index+1` before falling through to a sparse scan.
- R13 compile-wall wording was stale after neighboring agents added the previously missing symbols to the project graph; no fresh guarded build has been run and no raw R13 compiler log artifact exists on disk.

What was done:
- Changed hatch scheduling to require `_vaultInitialized` and resolve already-created hatch handles only; no schedule-path `EnsureGenerationHandle` or default CSV probe remains.
- Added `RefreshVaultState(vault, refreshHatchLocks)` and made visual sync call it with hatch refresh disabled.
- Changed `VisualSyncHatchLocks` to require prewarmed hatch graphics buffers and never allocate them; bootstrap prewarms hatch graphics buffers when upload is enabled, and prewarm failure disables only hatch shader upload instead of blocking simulation.
- Removed active allocation of `Shinobu343HatchShaderUpload`; BufferID 72030 is now retired/reserved, while active SHINOBU_343 Vault buffers are 72024-72029 and 72031.
- Removed `NativeDisableContainerSafetyRestriction` from `NativeQueue<MovementAcousticSignal>.ParallelWriter`.
- Added paired mock-room index probes to `EvaluateHatchPressureJob` before the sparse hash scan.
- Changed `HatchTelemetryEntry` tail padding from one trailing `ulong` to two 4-byte `uint` fields at offsets 56 and 60.
- Updated status, rationale, sidecar JSON, self-audit XML, scanner markdown, aggregate report, and binary payload ledger to mark compile proof as pending a fresh guarded build.

Cinematic cheats used:
- No new CPU presentation was added. Hatch warning visuals remain a shader-side bitmask fake fed by `_GlobalHatchLockStates`; gameplay truth remains a Burst bitmask.

Exact microseconds saved / spent estimates:
- Schedule path: removes cold Vault allocation/CSV IO risk from simulation scheduling; steady runtime remains handle-resolve only.
- Visual sync: removes hatch tuning mutation and hatch graphics buffer allocation from presentation; steady upload remains 0 us when state hash is unchanged.
- Mock pressure evaluation: paired rows avoid the avoidable sparse scan during stress tests; expected mock path returns to two direct probes per hatch instead of scanning the whole compartment buffer.
- Player hot path: no managed allocation route added; no hidden `.Complete()` added.

Verification:
- Runtime hot-path grep returned no forbidden SHINOBU_343 tokens for `new NativeArray`, `NativeList`, `NativeHashMap`, LINQ, `foreach`, hidden `.Complete()`, scene search, `UnityEngine.Random`, `string.Format`, `.Split`, `Pack=1`, `Time.deltaTime`, or `void Update`.
- Burst scan finds six deterministic SHINOBU_343 jobs.
- Build not run yet after R14; current status is pending fresh guarded build because build execution requires CPU <=50 and no active `dotnet`/`csc`. The latest CPU gate sampled 96 percent with no active compiler process.

## 2026-05-23 - Profile Envelope and Sourcegraph Gate R15

What was wrong:
- CSV hardware profiles were parsed into Vault rows but did not influence the active hatch tuning row.
- The profile Vault buffer uses `UninitializedMemory`; applying profile rows without a parsed-row count would risk consuming garbage beyond the rows actually parsed.
- The ignored/generated local `Hecton8.Core.csproj` sourcegraph did not include the new SHINOBU_343 files, so a later targeted build would not prove these files compile.

What was done:
- Added `_hatchProfileRowCount` and set it only after successful byte/file CSV parse.
- Folded valid profile rows into `HatchTuningDTO` as a conservative hardware envelope: lowest safe pressure, highest structural jam threshold, lowest catastrophic pressure with `catastrophic >= safe`.
- Updated editor state readback to report the effective Vault tuning row after profile folding.
- Added SHINOBU_343 runtime/editor source files to the ignored/generated local csproj sourcegraph for the next guarded build.

Cinematic cheats used:
- No CPU animation was added. Door-type visual richness remains shader-owned; profile folding affects safety thresholds only.

Exact microseconds saved / spent estimates:
- Cold parse/reload: <=32 profile rows folded once, estimated <5 us after the existing CSV parse.
- Runtime schedule: no per-hatch profile lookup; the FSM reads one folded tuning row, 0 B/frame GC.
- Local build proof: next guarded `dotnet build` will compile SHINOBU_343 files instead of producing false coverage.

Verification:
- Scoped `git diff --check` passed for touched SHINOBU_343 files and local csproj sourcegraph.
- Hot-path scan found only cold dump directory creation and cold CSV `File.Exists`; no SHINOBU_343 schedule/Burst hot-path forbidden tokens were found.
- Build not launched because CPU remained above the 50 percent gate; latest sample was 84 percent with active `dotnet/csc` processes.
