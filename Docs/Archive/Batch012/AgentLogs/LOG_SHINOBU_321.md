# LOG_SHINOBU_321

## Read-Only Subagent Audit: Vault Signal Burst Pattern Auditor

What was wrong:
- SHINOBU_321 integration risk was unknown: DataVault, SignalBus, CombatDamageSignal, Burst jobs, telemetry dumps, editor UI/gizmo, and CSV parser contracts needed source proof before any decompression simulator changes.
- `CURRENT_BATCH.md` was absent at `C:\hades\Hecton8`; assignment source available in this session was the inline `<SUB_AGENT_PROMPT>` plus existing `Status_SHINOBU_321.md`.

What was done:
- Read active authority files: `AGENTS.md`, `Docs/Actual Domains of Project.txt`, `Docs/Tasks/Status_SHINOBU_321.md`, and `Docs/AgentLogs/Rationale_SHINOBU_321.md`.
- Loaded relevant registry mandates: zero-GC, native memory/jobs, signal lane segregation, telemetry/postmortem, ARM64 struct layout, and CSV/binary bridge.
- Inspected first-party source contracts for `GlobalDataVault`, `SignalBus<T>`, `CombatDamageSignal`, physiology Burst jobs, physiology telemetry dump, editor tuner/gizmo surfaces, CSV parsers, and asmdefs.

Cinematic Cheats used:
- Audit only. No runtime visual fake implemented. Existing physiology route already uses scalar presentation outputs (`PhysiologyScalarsDTO`, `PhysiologyStateSignal`) that can drive non-authoritative barotrauma/visor/audio fakes without changing gameplay truth.

Exact Microseconds saved:
- 0 us measured. No code executed in Unity and no profiler run. Static audit prevents duplicate subsystem creation and direct health mutation risk; measured runtime savings remain PENDING VERIFICATION.

Safest integration route:
- Extend the existing physiology owner under `Assets/_Project/Scripts/Physiology`, not a new manager.
- Use existing `IDataVault.GetGenerationHandle<T>`, `TryResolveHandle<T>`, `TryReadHandle<T>`, and `ReleaseBuffer<T>` patterns with `SystemID.GameplayPlayer`.
- Publish first-party trauma through existing `SignalBus<CombatDamageSignal>` only after owner-phase simulation finalization, or use physiology-local mock/staging rows for internal job output before bridge publication.
- Use existing `PhysiologyTelemetryEntry[300]` dump pattern for black-box data.
- Use `Hecton8.Physiology.Editor` UI Toolkit windows as editor-only tuner precedent.

Absent or risky contracts:
- No public, reusable decompression-specific service interface was found. Existing decompression is embedded in `ShinobuPhysiologyRuntime` and `ShinobuPhysiologyJobs`.
- `SignalBus<T>.ParallelWriter` exists but is explicitly described as compatibility/legacy MPSC bridge. Safer producer route for new work is owner-local staging plus `SignalBus<T>.TryPush` from the owning phase, unless a job-writer route is already accepted.
- `GlobalDataVault.TryGetLatestCreated()` exists but is editor/diagnostic/bootstrap only, not a runtime integration route.

Verification:
- Static source inspection only. No build, Unity import, Play Mode, profiler, or GCMonitor proof was run.

---

## Primary Implementation Report

What was wrong:
- Existing decompression state was 80 bytes and used legacy field names instead of the required 128-byte fixed-buffer ABI.
- Existing Haldane integration was scalar and ratio-based; no Buhlmann `a/b` ceiling was written to the coefficient DTO.
- `HectonSurvivalSystem` still had a velocity-based immediate decompression signal path that could compete with the physiology owner.
- Dump path still pointed at `Dump_SHINOBU_272.bin`.
- No SHINOBU_321 OOP bends scanner/report or Buhlmann CSV seed existed.

What was done:
- `DecompressionStateDTO` is now explicit 128 bytes: `TissueTensionsN2[16]` at offset 0, `CurrentAmbientPressure` at 64, `GradientAdvantage` at 68, `BubbleFlags` at 72, padding to 128.
- `HaldaneTissueCoefficientDTO` is now 32 bytes and carries half-time, `K`, Buhlmann `a/b`, ratio fallback, gas fraction, key hash, and flags.
- `IntegrateBloodGasTensionsJob` now uses deterministic Burst, four-wide `v128`/`float4` lanes, Schreiner source-rate integration, Buhlmann ceiling evaluation, negative gradient tracking, bubble flags, physiology signal output, and `CombatDamageSignal` barotrauma output.
- Continuous `GlobalQualityWeight` now maps 4..16 active compartments; low endpoint mirrors four averaged tissue groups inside the fixed 16-slot DTO.
- `ShinobuPhysiologyRuntime` is partial and has decompression-specific editor/read accessors in `ShinobuPhysiologyRuntime_Decompression.cs`.
- Decompression Vault buffer acquisition uses `NativeArrayOptions.UninitializedMemory`; boot initialization overwrites active rows.
- CSV ingest targets `buhlmann_zh16_profiles.csv` and parses half-time/a/b columns with the existing span parser path.
- Added `HaldaneanDecompressionTunerWindow`, Scene View tension gizmo, `OOP_Bends_Scanner`, sidecar report, shared report section, and architecture note.
- Disabled the legacy immediate velocity-based decompression signal path in `HectonSurvivalSystem`.

Cinematic Cheats used:
- Barotrauma presentation is not simulated with CPU particles. The Burst job emits `PhysiologyStateSignal.CauseDecompression`; `GlobalShaderDispatcher` already maps supersaturation/narcosis/pressure to the decompression shader payload.
- Audio/DSP remains decoupled through existing physiology/stress signal routes; no direct audio object spawn was added.

Exact Microseconds saved:
- Measured proof absent. Static estimates only:
- Legacy rapid-ascent publish bypass: ~0.2-0.6 us during ascent stress, plus removal of duplicate side effects.
- Fixed 128-byte DTO and fixed-buffer tissue storage: 0 B/frame hot path; avoids managed tissue array dereferences.
- SIMD tissue solve: four vector groups for 16 tissues, target under 2 us for one player row; profiler proof pending.
- Uninitialized decompression buffer acquisition: saves full DTO buffer clear at allocation time.

Verification:
- `rg` found no `float.Parse`, `double.Parse`, `string.Split`, CsvHelper, `new float[16]`, LINQ, or tissue `List<>` in the owned runtime decompression files.
- `rg` found zero `AscentTimer`, `CheckAscent`, `TheBends`, `DepthDamage`, `depthDamage`, or `yield return new WaitForSeconds` patterns in the scanned runtime bends scope.
- `ConvertFrom-Json` passed for `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json` and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_321.json`.
- `git diff --check` passed for touched files; only existing line-ending warnings were reported.
- Build was not launched: CPU sampled at 100%, with active `csc.exe` and two `dotnet.exe` processes. Project rule forbids launching another build under that state.

<SELF_AUDIT>
20_TASK_CHECK:
01 PASS, 02 PASS, 03 PASS, 04 PASS, 05 PASS, 06 PASS, 07 PASS, 08 PASS, 09 PASS, 10 PASS, 11 PASS, 12 PASS, 13 PASS, 14 PASS, 15 PASS, 16 PASS, 17 PASS, 18 PASS, 19 PASS, 20 FAIL_BUILD_GATE.

ARM64_CHECK:
DecompressionStateDTO size 128. Offsets: TissueTensionsN2[16] = 0..63, CurrentAmbientPressure = 64, GradientAdvantage = 68, BubbleFlags = 72, _pad12 = 124.
HaldaneTissueCoefficientDTO size 32. Offsets: HalfTimeSeconds = 0, K = 4, BuhlmannA = 8, BuhlmannB = 12, MValueRatio = 16, NitrogenFraction = 20, CompartmentHash = 24, Flags = 28.

ZERO_GC_CHECK:
Hot decompression path is Burst `IJobParallelFor` over Vault `NativeArray` buffers and fixed buffers. No managed tissue arrays, LINQ, CsvHelper, `float.Parse`, runtime UI, coroutine, or direct scene search added to the decompression runtime path.

AUP_CHECK:
Depth remains derived from cached player AUP by subtracting sea-level `double3` from player `double3` before float conversion in `ResolveDepthMetersFromAup`.

VAULT_BUFFERS:
70221 DecompressionStateDTO, 70222 HaldaneTissueCoefficientDTO, 70223 environment, 70224 physiology scalars, 70226 telemetry ring, 70235 tissue rows, 70239 gas physiology state.
</SELF_AUDIT>

---

## Polish Loop 7 - Read Fences, Editor Locks, Telemetry Facade, Build Wall

What was wrong:
- Public physiology `TryGet*` accessors were fail-closed but still used the owner resolve helper. That made the audit story weaker than the Global Systems Doctrine requirement for pure read accessors.
- Editor tuning and breathing-gas override methods wrote Vault rows without explicit writer locks.
- The Haldanean tuner displayed decompression state bars but did not show the latest ambient pressure telemetry marker or black-box fault state.
- Data Monolith readiness could not be claimed: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.

What was done:
- Added `TryReadPhysiologyVaultArray` and moved public `TryGetTuning`, `TryGetGasTuning`, `TryGetTissueTension`, `TryGetVitalsExport`, `TryGetLatestTelemetry`, `TryGetGasPhysiologyState`, `TryGetDecompressionState`, and `TryGetHaldaneCoefficient` onto `GlobalDataVault.TryReadHandle`.
- Routed `SetEditorTuning`, `SetEditorGasTuning`, and `SetEditorBreathingGasNitrogenFraction` through `TryAcquireWriteLock` and `ReleaseWriteLock` in `finally`.
- Extended `HaldaneanDecompressionTunerWindow` with per-row ambient pressure marker lines and telemetry fault status from `PhysiologyTelemetryEntry`.
- Fixed the debug M-value accessor fallback so absent coefficient rows use emergency ZH-L16 `a/b` values instead of a default zero coefficient clamped to `b=0.1`.
- Updated architecture/report/status/rationale artifacts with read-fence, editor-lock, telemetry-facade, Data Monolith, and build-wall evidence.

Cinematic Cheats used:
- No CPU VFX, GameObject symptom actors, or particle spawning were added. The tissue simulator still emits scalar physiology/decompression state; shader/audio presentation remains the Dear Lie route.

Exact Microseconds saved:
- Player hot path: 0 us measured change from editor/read-accessor polish; public reads are editor/debug copy paths gated by `_jobScheduled`.
- Editor: removed shadow-state pressure interpretation by reading the existing telemetry ring; no extra runtime allocations in player builds.
- Build budget: one serialized compile attempt only, `-maxcpucount:1`, after CPU gate opened.

Verification:
- Forbidden runtime pattern scan found no `float.Parse`, `double.Parse`, `string.Split`, CsvHelper, `new float[16]`, LINQ, old decompression field names, old dump path, string format, coroutine wait, or runtime `GetComponent` in the owned SHINOBU_321 files. The only hit was editor-cold throttled `FindFirstObjectByType` in `HaldaneanDecompressionTunerWindow.ResolveRuntime`.
- `$"` fixed-string scan found no interpolated strings in owned SHINOBU_321 files.
- JSON validation passed for both `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_321.json` and `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`.
- `git diff --check` passed for loop 7 touched files with line-ending warnings only.
- CPU sampled 44% and no `dotnet`, `csc`, `MSBuild`, `VBCSCompiler`, or `Unity` process was visible through `Get-Process`; one build was launched.
- Build failed on external compile wall: `RadiationHazardGrid.cs` missing `RadiationStateDTO`; `VRSomaticProvider.Comfort.cs` missing `VRSomaticKinematicStateMirrorDTO` and `VRSomaticComfortDTO`. No SHINOBU_321 error appeared in the `ErrorsOnly` output before those blockers.
- After the final debug M-value fallback patch, CPU sampled 20% but `VBCSCompiler` PID 2036 was active. Repeat build was suppressed by the compile-wall rule.

<SELF_AUDIT>
20_TASK_RECONCILIATION:
01 PASS - archaeology re-run against SHINOBU_321 prompt, Global Authority, interconnect, and binary ledger slices.
02 PASS - decompression remains a partial extension of `ShinobuPhysiologyRuntime`.
03 PASS - combat injury route remains typed `SignalBus<CombatDamageSignal>`.
04 PASS - legacy velocity-based decompression damage remains disabled.
05 PASS - hot decompression state uses fixed `TissueTensionsN2[16]`; no managed tissue array in owned runtime path.
06 PASS - mock dive profile Burst job remains available for synthetic pressure stress.
07 PASS - deterministic Burst Haldane integrator applies Schreiner source-rate equation.
08 PASS - 16 tissues process in four `v128`/`float4` groups.
09 PASS - Buhlmann `a/b` M-value ceiling drives `GradientAdvantage` and `BubbleFlags`.
10 PASS - Dear Lie route is scalar physiology/decompression signal, not CPU symptom object spawning.
11 PASS - continuous `GlobalQualityWeight` maps representative compartments 4..16.
12 PASS - AUP depth path subtracts `double3` sea-level/player coordinates before float pressure math.
13 PASS - jobs use deterministic Burst and guarded finite math.
14 PASS - decompression Vault buffer uses uninitialized acquisition with explicit initialization.
15 PASS - 300-frame telemetry ring dumps current row on fatal/invalid/non-finite/overbudget conditions.
16 PASS - UI Toolkit tuner now reads authority DTO plus latest telemetry; editor writes use Vault locks.
17 PASS - Buhlmann CSV parser remains cold span parser; no managed CSV parser found.
18 PASS - SceneView gizmo reads the authority row.
19 PASS - OOP bends scanner/report still records zero target OOP ascent patterns.
20 FAIL_EXTERNAL_COMPILE_WALL - self-audit and build attempt done; compile proof blocked by unrelated missing DTOs, and repeat build after final static patch suppressed by active `VBCSCompiler`.

STRUCT_LAYOUT_VERIFICATION:
DecompressionStateDTO = 128 bytes. Offsets: `TissueTensionsN2[16]` 0..63 size 64; `CurrentAmbientPressure` 64 size 4; `GradientAdvantage` 68 size 4; `BubbleFlags` 72 size 4; `_pad0.._pad12` 76..124 size 52. Total 64 + 4 + 4 + 4 + 52 = 128 bytes = two 64B cache lines.
HaldaneTissueCoefficientDTO = 32 bytes. Offsets: `HalfTimeSeconds` 0, `K` 4, `BuhlmannA` 8, `BuhlmannB` 12, `MValueRatio` 16, `NitrogenFraction` 20, `CompartmentHash` 24, `Flags` 28. Total 32 bytes.

SCALABILITY_CURVE_EXPLANATION:
The math LOD is continuous: `ResolveActiveCompartmentCount` saturates `GlobalQualityWeight`, applies smoothstep, and lerps 4..16 representative compartments. Below 0.3 the job evaluates the cheapest representative tissue set while keeping the 16-slot DTO, Buhlmann coefficients, damage authority, and save identity unchanged. Middle weights expand the active compartment count smoothly; high/ultra evaluates all 16 and leaves saved budget for shader/audio presentation.

H_PHI_VAULT_STATUS:
No private persistent decompression `NativeArray`, `NativeList`, or `NativeHashMap` owns truth. Runtime stores Vault generation handles only. Buffers: 70221 decompression state, 70222 Haldane coefficients, 70223 environment, 70224 physiology scalars, 70226 telemetry ring, 70235 tissue rows, 70239 gas physiology state. Public reads use `TryReadHandle`; editor writes use write locks.

POINTER_ALIASING_AND_DEPENDENCY_GRAPH:
`IntegrateBloodGasTensionsJob` marks NativeArray/NativeQueue fields `[NoAlias]` and read lanes `[ReadOnly]`. The runtime consumes upstream dispatcher dependency, schedules the Burst job, stores the returned handle in `_activeJobHandle`, and finalizes only from owner completion/teardown fences. No hot `GlobalRegistry` polling or arbitrary mid-frame `Complete()` was added.

COMPILE_GUARD:
No direct sibling runtime assembly reference was added. Owned changes remain in physiology runtime/editor/report files plus the legacy survival bypass already documented. Build attempt failed on radiation/VR somatic DTO dependencies outside SHINOBU_321.

DEAR_LIE_CONFIRMATION:
Before: a naive bends implementation would spawn CPU particles/audio/pain objects per symptom, O(symptoms + spawned objects). After: tissue truth is O(active compartments) and presentation is one scalar signal payload; shader/audio consumers synthesize the experience without CPU object ownership.
</SELF_AUDIT>

---

## Polish Loop 6 - Blackbox Cursor And Legacy Editor Purge

What was wrong:
- The telemetry execution time was patched into the current ring cursor, but the dump check read the previous cursor row. This failed the exact "dump on >0.2 ms" forensic requirement for the just-finished decompression frame.
- The old `DcsPhysiologyTunerWindow` still existed as a duplicate decompression editor facade with managed `float[]` tissue arrays and interpolated status strings.
- `HaldaneanDecompressionTunerWindow` still formatted tissue labels/status each refresh and searched the scene from both the window and SceneView path.

What was done:
- Added `TelemetryDumpBudgetMicroseconds = 200f`.
- `TryDumpAutopsyIfFatal` now reads `_telemetryCursor % telemetry.Length` after `PatchLatestTelemetryExecutionTime` and dumps on fatal flags, invalid math, non-finite time, or `ExecutionMicroseconds >= 200`.
- The legacy DCS editor window was reduced to a compatibility menu shim that opens the active Haldanean tuner.
- The Haldanean tuner now uses a cached/throttled runtime resolver, fixed tissue label literals, named callbacks, and constant status strings. Its refresh loop no longer formats per-tissue numeric strings.
- Updated architecture and report artifacts to state the current-row dump and legacy editor shim.

Cinematic Cheats used:
- No CPU particle, audio object, or post-process object mutation was introduced. Decompression pain remains a scalar signal route for shader/audio consumers.

Exact Microseconds saved:
- Runtime gameplay: 0 us measured, no profiler available.
- Forensics: one current telemetry row read during owner completion; cost is sub-microsecond and outside the Burst tissue kernel.
- Editor: removed duplicate legacy chart allocations and per-refresh numeric string formatting; player build cost is 0 us.

Verification:
- Static owned-runtime scan found no `float.Parse`, `double.Parse`, `string.Split`, CsvHelper, managed tissue arrays, LINQ, or old decompression field names in SHINOBU_321 runtime files.
- `rg` confirmed `TelemetryDumpBudgetMicroseconds`, current telemetry cursor check, `>= 200 us` dump condition, and `Dump_SHINOBU_321.bin`.
- `git diff --check` passed for the loop-6 touched files with line-ending warnings only.
- Build was not launched. CPU sampled at 100%, then 78.3%; the project rule forbids build launch above 50% CPU even though no compiler process was active at the final sample.

<SELF_AUDIT>
20_TASK_RECONCILIATION:
01 PASS - archaeology performed over Physiology/Player/survival overlap.
02 PASS - reused partial `ShinobuPhysiologyRuntime`; no standalone decompression manager.
03 PASS - barotrauma routes through `SignalBus<CombatDamageSignal>`.
04 PASS - legacy immediate velocity-based decompression signal path disabled.
05 PASS - runtime tissue truth is fixed `TissueTensionsN2[16]`; legacy DCS editor array facade removed.
06 PASS - `GenerateMockDiveProfileJob` exists for synthetic square-wave stress samples.
07 PASS - deterministic Burst tissue integration applies Schreiner source-rate math.
08 PASS - 16 tissues process in four `v128`/`float4` groups.
09 PASS - Buhlmann `AllowedPressure = (Tension - a) * b` drives negative `GradientAdvantage` and bubble mask.
10 PASS - pain/supersaturation is a scalar signal for shader/audio consumers; no CPU VFX spawn.
11 PASS - `GlobalQualityWeight` maps continuously from 4 to 16 active compartments.
12 PASS - depth source subtracts sea-level AUP from player AUP in double before float pressure math.
13 PASS - decompression jobs use deterministic Burst and finite/denominator guards.
14 PASS - decompression state Vault buffer uses `NativeArrayOptions.UninitializedMemory` and boot overwrite.
15 PASS - 300-frame ring dumps to `Dump_SHINOBU_321.bin` on fatal/invalid/non-finite or >=200us current row.
16 PASS - UI Toolkit Haldanean tuner exists; legacy DCS menu shims into it.
17 PASS - `buhlmann_zh16_profiles.csv` cold parser uses `ReadOnlySpan<byte>` and manual float parse.
18 PASS - SceneView tension gizmo reads the Vault row and colors by gradient.
19 PASS - `OOP_Bends_Scanner` report artifact exists and records zero target patterns.
20 FAIL_BUILD_GATE - source self-audit done; compile/profiler proof blocked by CPU gate.

STRUCT_LAYOUT_VERIFICATION:
DecompressionStateDTO = 128 bytes. `TissueTensionsN2[16]` offset 0 size 64; `CurrentAmbientPressure` offset 64 size 4; `GradientAdvantage` offset 68 size 4; `BubbleFlags` offset 72 size 4; `_pad0.._pad12` offsets 76..124 size 52. Total 128 = two 64B cache lines.
HaldaneTissueCoefficientDTO = 32 bytes. `HalfTimeSeconds` 0, `K` 4, `BuhlmannA` 8, `BuhlmannB` 12, `MValueRatio` 16, `NitrogenFraction` 20, `CompartmentHash` 24, `Flags` 28.

SCALABILITY_CURVE:
`ResolveActiveCompartmentCount` saturates `GlobalQualityWeight`, applies smoothstep, and rounds `lerp(4, 16, smooth)`. Low pressure collapses representative tissue groups; middle expands representatives; high/ultra evaluates all 16. DTO layout, damage authority, save identity, and signal routes do not change.

H_PHI_VAULT_STATUS:
No private persistent `NativeArray` owns decompression truth. Runtime stores Vault generation handles for buffers `70221`, `70222`, `70223`, `70224`, `70226`, `70235`, and `70239`; jobs receive phase-local `NativeArray` views.

POINTER_ALIASING_AND_DEPENDENCY_GRAPH:
`IntegrateBloodGasTensionsJob` fields are marked `[NoAlias]`; read-only lanes are also `[ReadOnly]`. It consumes the prior dispatcher `JobHandle`, returns the scheduled handle to `_activeJobHandle`, and finalizes only through the owner completion fence or teardown.

COMPILE_GUARD:
`Hecton8.Physiology.asmdef` references Core/Core.Contracts/Core.Memory plus Unity packages. No direct sibling runtime domain reference was added. Build remains blocked by CPU gate, not by a known SHINOBU_321 compile error.

DEAR_LIE_CONFIRMATION:
Before: possible CPU particle/audio/health mutation route O(active symptoms + spawned objects). After: O(1) scalar signal payload; shader/audio consumers synthesize red aberration/tinnitus without CPU object spawning.
</SELF_AUDIT>
