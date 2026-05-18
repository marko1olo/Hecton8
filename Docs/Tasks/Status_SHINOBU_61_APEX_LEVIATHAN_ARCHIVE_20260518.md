# Status_SHINOBU_61

Date: 2026-05-19
Agent: SHINOBU_61
Domain: ECHELON 3 FLORA, FAUNA & BIOTA / Predator Cognition and Steering
Evidence status: LOOP 17 SOURCE HARDENED; ROSLYN RECHECK BLOCKED BY CPU GUARD; UNITY PLAY MODE/PROFILER PENDING

## Mandates Read Before Coding

- AI_Creature_Cognition_States.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Batch Prompt Boundary

Extracted from `Docs/Tasks/CURRENT_BATCH.md` with `id="SHINOBU_61"`.
Task count: 20.

## State Machine Checklist

- [x] Task 01 BINARY_GRAVEYARD_RECONNAISSANCE | DOD practice: archive/ledger evidence showed no active `apex_predator_curves.h8bin`, so `ApexBrainDefaults.BuildEmergencyMockStats()` supplies 16-byte aligned `float4` fallback rows during vault hydration | Alternative rejected: inventing a binary layout or wiring stale `Data/AI/Navigation_Tuning.h8bin` as apex truth | Estimate: 12 us cold boot fallback, 0 us hot path
- [x] Task 02 STATE_MACHINE_ERADICATION_PASS | DOD practice: `ApexBrainJob` evaluates float utility scores and emits phase bytes, with no C# state classes | Alternative rejected: OOP state machine and virtual transitions | Estimate: 6 us saved per active leviathan versus managed state dispatch
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD practice: all new hot DTOs expose public fields and `ApexBrainVault.GetStateAsRef()` returns a ref for direct L1 mutation | Alternative rejected: `{ get; private set; }` properties and defensive struct copies | Estimate: 3 us saved per 10-row pass
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD practice: `ApexStateDTO` is explicit 64B, influence nodes/signals/telemetry are 64B/128B aligned, no runtime `Pack=1` | Alternative rejected: packed structs and 52B unaligned DTOs | Estimate: 8-20 us saved per active batch on ARM64 cache-line fetches
- [x] Task 05 BLIND_DEPENDENCY_MOCKING | DOD practice: `partial struct MockPlayerAUP` plus `MockPlayerAupAdvanceJob` drives blind AUP target movement | Alternative rejected: direct dependency on Player Kinematics/Agent 06 | Estimate: 15 us saved by avoiding cross-domain player polling
- [x] Task 06 BURST_PREDICTIVE_INTERCEPT_KERNEL | DOD practice: `ApexBrainJob` computes `targetLocal + TargetVelocity * (Distance / LeviathanSpeed)` in AUP-local `float3` space | Alternative rejected: aim-at-current-position chase | Estimate: 25 us saved versus short-horizon path retargeting
- [x] Task 07 ACOUSTIC_MEMORY_BANK_ROUTER | DOD practice: `AcousticEchoTap` scan selects decayed loudest local echo and writes `AcousticMemoryHash` | Alternative rejected: direct Audio runtime dependency or managed event list | Estimate: 10-18 us saved by fixed tap scan and no allocation
- [x] Task 08 SDF_SLITHER_STEERING | DOD practice: analytic `MockWorldSampler` SDF samples head/mid/tail and produces potential-field repulsion | Alternative rejected: raycast/body-fit cave checks | Estimate: 80-140 us saved per leviathan versus 8-16 physics rays
- [x] Task 09 THE_DEAR_LIE_OCCLUSION_STALKING | DOD practice: sweet-lie LOS uses player forward dot product, distance visibility, SDF shadow, and spatial hash canyon bias | Alternative rejected: full-body linecast/raycast occlusion | Estimate: 60-120 us saved per leviathan
- [x] Task 10 AGGRO_BUILDUP_AND_TERROR_RADIUS | DOD practice: `AggressionLevel` integrates with dt/noise/acoustic/biome scalars and writes `ApexProximitySignal` | Alternative rejected: instant attack trigger and concrete Audio/HUD calls | Estimate: 8 us saved plus decoupled signal routing
- [x] Task 11 CONTINUOUS_SCALABILITY_NODE_EVAL | DOD practice: `GlobalQualityWeight` drives node count 2-16 and smooth mid/tail SDF weights | Alternative rejected: binary low/high switches | Estimate: 45-90 us saved at low quality
- [x] Task 12 LEVIATHAN_BREACH_SYNERGY | DOD practice: strike phase writes `MockCombatDamageSignal` with AUP target, direction, magnitude, hashes | Alternative rejected: local base physics/deformation calculation | Estimate: 100+ us saved by delegating WFC physics
- [x] Task 13 AUP_LOCALIZED_IK_TARGETS | DOD practice: output `IK_BiteTarget` is local `float3` from intercept and head offset | Alternative rejected: absolute double3 animation rig target | Estimate: 4 us saved plus no 100km jitter amplification
- [x] Task 14 BIOME_TERRITORIAL_BIAS | DOD practice: Abyssal Trench biome hash/flag multiplies aggro buildup by `BiomeAggressionMultiplier` | Alternative rejected: hard trigger volumes | Estimate: 6 us saved versus trigger/scene lookups
- [x] Task 15 FAUNA_SCATTER_BROADCAST | DOD practice: strike phase writes `GlobalPanicSignal` for ecosystem repulsion | Alternative rejected: direct ecosystem domain call | Estimate: 10 us saved and no sibling assembly dependency
- [x] Task 16 ZERO_INIT_OVERHEAD_BYPASS | DOD practice: vault allocates max 10 apex rows and scratch buffers with `NativeArrayOptions.UninitializedMemory`; spawn reset uses `UnsafeUtility.MemClear` | Alternative rejected: managed lists or per-spawn `new NativeArray` | Estimate: 20-35 us saved per spawn
- [x] Task 17 TELEMETRY_CORTEX_RECORDER | DOD practice: 300-frame `ApexTelemetryEntry` ring writes aggression/node/quality/fault state; cold dump writes `Dump_SHINOBU_61.bin` and `Dump_LEVIATHAN_CORTEX.bin` | Alternative rejected: managed logs/string telemetry in hot path | Estimate: 40 us saved per frame versus managed logging; dump is cold only
- [x] Task 18 APEX_TUNER_EDITOR_WINDOW | DOD practice: `Leviathan Cortex Tuner` EditorWindow reads/writes unmanaged vault tuning in Play Mode | Alternative rejected: recompilation for tuning changes | Estimate: designer iteration saved, 0 us gameplay hot path
- [x] Task 19 CSV_OVERRIDE_INGESTOR | DOD practice: `TryLoadCsvOverrides` reads `apex_predator_stats.csv` into vault scratch bytes and hashes keys into unmanaged tuning | Alternative rejected: JSON/ScriptableObject parser in runtime path | Estimate: cold 0.2-1.0 ms load, 0 us hot path
- [x] Task 20 GIZMO_INTERCEPT_VISUALIZER | DOD practice: Editor scene hook draws red intercept sphere and yellow acoustic rings from vault outputs | Alternative rejected: runtime debug GameObjects | Estimate: 0 us player hot path; editor-only draw cost

## Iteration Log

### Loop 0 - Initialization

- Status/Rationale files created.
- Runtime code not touched yet.
- Compile verification pending.

### Loop 1 - Tasks 01-05

- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with `id="SHINOBU_61"`.
- Runtime compile check: `dotnet csc @Hecton8.AI.Cognition.rsp` plus SHINOBU sources passed after CS8332 fix.
- Editor compile check: filtered `Hecton8.Editor.rsp` plus `LeviathanCortexTunerWindow.cs` passed; analyzer emitted USG0001 info only.
- Correction applied: removed `in ApexBrainVaultBuffers` from methods that mutate NativeArray views.

### Loop 2 - Tasks 06-10

- Evidence scan found predictive intercept, acoustic memory, SDF sampling, sweet-lie LOS, and proximity signal symbols in SHINOBU sources.
- Runtime compile check repeated after loop 2 and passed.
- `rg` scan found no `NavMeshAgent` or `Physics.Raycast` in the new SHINOBU runtime/editor files.

### Loop 3 - Tasks 11-15

- Prompt re-extracted for Tasks 11-15 from `CURRENT_BATCH.md`.
- Evidence scan found `GlobalQualityWeight`, SDF sample weights, `MockCombatDamageSignal`, `IK_BiteTarget`, Abyssal Trench bias, and `GlobalPanicSignal`.
- Runtime compile check repeated after loop 3 and passed.

### Loop 4 - Tasks 16-20

- Prompt re-extracted for Tasks 16-20 from `CURRENT_BATCH.md`.
- Evidence scan found uninitialized vault allocation, `UnsafeUtility.MemClear`, 300-frame telemetry/dump paths, CSV parser, editor sliders, and gizmo draw hook.
- Added computed NaN/finiteness guard after SDF/LOS math so fault telemetry can trigger `TryDumpBlackBoxOnFrameFault`.
- Runtime compile check repeated after loop 4 and passed.
- Editor compile check repeated after loop 4 and passed; Roslyn analyzer emitted USG0001 info only.

### Loop 5 - Strict Self-Audit

- Forbidden API scan over SHINOBU files returned no matches for `NavMeshAgent`, `UnityEngine.AI`, `Physics.Raycast`, `Linecast`, cast sweeps, `{ get; }`, `{ set; }`, `Pack=1`, `IsLowEnd`, or runtime `new NativeArray`/`NativeList`/`NativeHashMap`.
- Runtime asmdef references only `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity Burst/Collections/Jobs/Mathematics packages. No sibling runtime domain reference.
- Runtime compile check passed after final audit patch.
- Editor compile check passed after final audit patch; Roslyn analyzer emitted USG0001 info only.
- `git diff --check` over touched tracked paths reported no whitespace errors; Git warned only that existing asmdef line endings may normalize to CRLF.

### Loop 6 - Titanium Hardening Pass

- Re-read `CURRENT_BATCH.md`, `Rationale_SHINOBU_61.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` before code changes.
- Added exact vault `NativeArray<float3>` ambush scratchpad at BufferID `70629`, because the XML explicitly requested a preallocated `NativeArray<float3>` scratchpad and the previous rich `ApexInfluenceNode` rows were not literal enough.
- Added `math.step` quality gates for low-quality collapse and mid/tail SDF sample activation; retained polynomial smoothing and `math.lerp` node interpolation.
- Replaced zero telemetry compute time with deterministic `InterceptComputeTimeMs` estimate from node count, acoustic tap cap, SDF gates, and quality.
- Added telemetry heartbeat overload that can dump the black box immediately after a completed fault frame.
- Added dump endian marker `0x01020304` so binary forensics can reject swapped payloads explicitly.
- Runtime compile check passed after hardening. Editor compile check passed; Roslyn analyzer emitted USG0001 info only.
- Expanded forbidden API scan also checked `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, LINQ, and `foreach`; no SHINOBU matches.

### Loop 7 - SignalBus Dependency Trap Fix

- Attempted direct runtime `SignalBus<T>` bridge was rejected after Roslyn exposed duplicate `ISignal` identity between `Hecton8.Core` and `Hecton8.Core.Contracts` references.
- Removed direct `Hecton8.Core` runtime reference to preserve compile-wall isolation.
- Added optional `NativeQueue<T>.ParallelWriter` fields to `ApexBrainJob` and `ApexBrainVault.AttachSignalWriters(...)` so a Core/SignalBus owner can attach writers without AI.Cognition referencing Core.
- Burst job now writes vault signal rows and, when explicitly enabled by caller, enqueues proximity/combat/panic signals through NativeQueue writers.
- Runtime compile check passed after the trap fix. Editor compile check passed; Roslyn analyzer emitted USG0001 info only.

### Loop 8 - Fault Noise Reduction

- Found that inactive mock targets could set `FaultCode`, causing black-box dumps on empty/non-hydrated scenes.
- Changed fault semantics: inactive target still produces Dormant/zero authority output, but `ApexBrainFlags.Fault` and `FaultCode` are reserved for non-finite input or non-finite SDF/LOS math.
- Runtime compile check passed after fault semantics repair. Editor compile check passed; Roslyn analyzer emitted USG0001 info only.

### Loop 9 - Signal Integration Surface and Architecture Ledger

- Re-read `CURRENT_BATCH.md`, `Rationale_SHINOBU_61.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` before this pass.
- Removed the `new ApexBrainJob` initializer from the cold vault bridge and assigned struct fields directly after `default`, keeping the scheduling surface explicit and allocation-free.
- Added `ApexBrainVault.TryScheduleWithSignalWriters(...)` so a Core/SignalBus owner can attach `NativeQueue<T>.ParallelWriter` lanes without AI.Cognition referencing `Hecton8.Core`.
- Added `Docs/ARCHITECTURE/SHINOBU_61_APEX_COGNITION.md` with buffer IDs, compile-wall boundary, sweet-lie LOS summary, and dump header facts.
- Runtime compile check passed after this pass.
- Editor compile check passed after this pass; Roslyn analyzer emitted USG0001 info only.
- Duplicate GUID scan over six new Unity meta GUIDs returned one match each.
- Forbidden API scan remained clean for NavMesh/Physics casts/properties/Pack=1/binary hardware switches/UnityEngine.Random/Time.deltaTime/JobHandle.Complete/LINQ/foreach.
- Unity import, Play Mode, Burst Inspector, and profiler proof remain pending.

### Loop 10 - Ultra-Think Polish Reconciliation

- Re-read the active predictive `SHINOBU_61` XML block from `CURRENT_BATCH.md`; the later duplicate voxel-surface `SHINOBU_61` block was ignored as outside this assignment.
- Re-read `Rationale_SHINOBU_61.md` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` before editing.
- Found a real cold-boot risk: `UninitializedMemory` rows could contain random finite bits before target hydration. Added first-hydration `UnsafeUtility.MemClear` across states, mock targets, acoustic taps, outputs, signal rows, influence rows, ambush scratch, telemetry, cursor, and CSV scratch before emergency mock tuning is installed.
- Hardened the locked-DataVault path: `TryResolve` now validates and hydrates existing handles even when allocation is already locked, without requesting new buffers.
- Added inactive-target early-out in `ApexBrainJob`; dormant rows now clear output/signal state and write cheap telemetry without SDF, acoustic pursuit, or ambush-node evaluation.
- Corrected telemetry truth: `ActiveLeviathans` no longer stores schedule capacity; each row now writes `1` for an active authority slot and `0` for Dormant.
- Replaced per-node `math.sincos` ambush placement with deterministic 16-lane octant lattice plus spatial-hash radial jitter; this keeps 2..16 node interpolation but removes trig from the node loop.
- Replaced two direct square-root distances with `x * rsqrt(x)` guarded by `ApexBrainConstants.Epsilon`.
- Static forbidden API scan remained clean for `math.sincos`, NavMesh/Physics casts/properties/Pack=1/binary hardware switches/UnityEngine.Random/Time.deltaTime/JobHandle.Complete.
- Roslyn compile was intentionally not launched yet: CPU load checks returned 68%, 80%, 100%, 67%, and 66%, above the AGENTS `>50%` prohibition. One external `csc.exe`/`dotnet` compile appeared and finished; no second compile was launched.

### Loop 9.5 - Optional NativeQueue Safety Hardening

- Found a Unity Jobs integration risk: default scheduling carries optional `NativeQueue<T>.ParallelWriter` fields even when `EnableSignalQueueWrites` is zero.
- Added `NativeDisableContainerSafetyRestriction` to the three optional signal writer fields so the no-writer schedule path is not blocked by container safety validation on default writer structs.
- Queue access remains gated by `EnableSignalQueueWrites`; only `TryScheduleWithSignalWriters(...)` and `AttachSignalWriters(...)` enable writer use.
- Historical runtime compile check passed after this safety hardening before the later cold-boot/octant edits.
- Historical editor compile check passed after this safety hardening; Roslyn analyzer emitted USG0001 info only.
- Superseded by later source edits; do not use this historical pass as current compile proof.

### Loop 11 - Continuous Scheduler Frequency Gate and Recheck

- Added `ApexBrainVault.ShouldEvaluateFrame(...)` as a deterministic frame-stride gate derived from `GlobalQualityWeight`.
- `TrySchedule(...)` and `TryScheduleWithSignalWriters(...)` now skip scheduling on non-evaluation frames while preserving the caller's input dependency.
- Update frequency is computed with `math.lerp(5f, 60f, Smooth01(...))`; the gate now evaluates `round(updateHz)` frames per 60-frame window using a deterministic frame mask. Quality 0.1 resolves to 5 evaluations per 60 frames; quality 1.0 resolves to 60 per 60.
- Superseded by Loop 12 targeted Roslyn recheck after cadence-mask, deterministic Burst, and scratch-clearing fixes.

### Loop 12 - Rollback Determinism and Scratch Hygiene

- Re-read the predictive Apex XML block, apex archived status/rationale, and binary ledger after the duplicate-ID voxel collision.
- Changed `MockPlayerAupAdvanceJob` and `ApexBrainJob` Burst attributes from `FloatMode.Fast` to `FloatMode.Deterministic` because this authority state is rollback-relevant.
- Added `ClearAmbushRows(...)` and call sites for Dormant and faulted rows so stale `AmbushNodeScratch` / `ApexInfluenceNode` data cannot leak into gizmos or downstream consumers after a target deactivates or faults.
- Faulted output now zeroes non-authority utility vectors/scalars and resets stamina to `1f`, while preserving fault flags and state hash for telemetry.
- Static forbidden API scan remained clean for `math.sincos`, NavMesh/Physics casts/properties/Pack=1/binary hardware switches/UnityEngine.Random/Time.deltaTime/JobHandle.Complete/LINQ/foreach.
- Targeted runtime Roslyn/Bee compile passed: `Temp/SHINOBU_61_CognitionCheck.dll`, timestamp 2026-05-18 23:21:15.
- Targeted editor Roslyn compile passed: `Temp/SHINOBU_61_EditorCheck.dll`, timestamp 2026-05-18 23:21:29; analyzer emitted USG0001 info only.

### Loop 13 - Duplicate-ID Audit Trail Closure

- Re-read active `Status_SHINOBU_61.md` and `Rationale_SHINOBU_61.md`; at that historical point both belonged to the later duplicate voxel Surface Nets prompt, so they were not overwritten in Loop 13.
- Added a pointer section to `Docs/AgentLogs/LOG_SHINOBU_61.md` that directed Apex reviewers to the preserved `*_APEX_LEVIATHAN_ARCHIVE_20260518` files. This was superseded by Loop 15 after the user explicitly rebound the active prompt to Apex.
- Re-ran static forbidden API scans over Apex runtime/editor files; no matches for NavMesh, Physics casts, `Update()`, `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, LINQ, `foreach`, hot DTO properties, `Pack=1`, binary hardware switches, `math.sincos`, or `FloatMode.Fast`.
- Re-ran `git diff --check` over touched Apex and audit files; no whitespace errors. Git still warns only that the existing AI.Cognition asmdef may normalize LF to CRLF.
- No runtime source changed after the Loop 12 targeted Roslyn compile; Unity Play Mode, Burst Inspector, profiler, GCMonitor, and console proof remain pending.

### Loop 14 - Acoustic Bank Continuum Hardening

- Re-read the predictive Apex XML block, active duplicate-ID status/rationale, archived Apex status/rationale, and binary payload ledger before editing.
- Found a real scalability gap: `GlobalQualityWeight` controlled ambush nodes, SDF samples, and scheduler cadence, but active frames still scanned the full 32-row acoustic tap bank at low quality.
- Added `ResolveAcousticTapLimit(...)`: acoustic memory now lerps from 4 taps at survival quality to 32 taps at full quality through the same polynomial quality curve.
- `ResolveAcousticMemory(...)` now receives the resolved tap limit, and telemetry `InterceptComputeTimeMs` now estimates the actually scanned acoustic tap count instead of always assuming the full bank.
- Static forbidden API scans remained clean for NavMesh, Physics casts, `Update()`, `UnityEngine.Random`, `Time.deltaTime`, `JobHandle.Complete`, LINQ, `foreach`, hot DTO properties, `Pack=1`, binary hardware switches, `math.sincos`, `FloatMode.Fast`, and runtime private native allocation.
- GUID scan for new Unity `.meta` files returned one match per GUID.
- Targeted Roslyn recheck is blocked by AGENTS hardware guard: a 24-sample guard-aware wait never reached `CPU <= 50%` with zero compiler processes; observed CPU range was 72-100% and compiler count was often 1-2. The script exited `ROSLYN_RECHECK_SKIPPED_CPU_GUARD` without launching `dotnet`.
- The Loop 12 compile proof is now superseded by this source edit.

### Loop 15 - Sweet Lie LOS Polish and Legacy Cognition Hygiene

- Re-read active Apex status/rationale and the duplicate `SHINOBU_61` prompt evidence. Latest user instruction explicitly binds this turn to `PREDICTIVE_APEX_AGGRESSION_DIRECTOR`; Voxel evidence remains preserved in `_VOXEL_SURFACE_NETS_ARCHIVE_20260518`.
- Hardened sweet-lie LOS: high quality adds one midpoint SDF line sample through `math.step`/smooth quality weighting, while low quality keeps dot product + center SDF + canyon hash bias.
- Cleared stale high-quality `AmbushNodeScratch` and `ApexInfluenceNode` rows when `GlobalQualityWeight` lowers evaluated node count, preventing old 16-node intent from leaking into low-quality gizmos or animation bridges.
- Added timestamp-gated CSV polling metadata to `ApexBrainTuning` without changing its 128B layout: `LastCsvHash`, `CsvReloadVersion`, and `LastCsvWriteTicks` reuse existing explicit padding.
- Hardened adjacent legacy AI Cognition files in the same runtime assembly: removed all remaining `Pack=1` declarations, converted hot legacy DTOs to explicit layouts, removed struct `IsCreated` properties from vault wrapper structs, and changed `LeviathanStalkJob` to deterministic Burst plus `[NoAlias]` fields. Loop 16 later padded the parallel-written legacy rows to cache-line multiples.
- Static scans after source edits found no `Pack=1`, `Sequential`, NavMesh, physics raycasts, managed state machine, hot DTO `{ get; }` properties, `UnityEngine.Random`, `Time.deltaTime`, `foreach`, LINQ, `JobHandle.Complete`, or sibling runtime domain references in `Assets/_Project/Scripts/AI/Cognition`.
- `git diff --check` passed for AI Cognition and SHINOBU docs; Git warned only about existing LF-to-CRLF normalization on several files.
- Compiler proof is still intentionally blocked by guard until CPU is <=50% and no `dotnet`/`csc.exe` is active. No `dotnet build` was launched in this pass.

### Loop 16 - NaN Quarantine and False-Sharing Padding

- Re-read active Apex status/rationale, extracted the `PREDICTIVE_APEX_AGGRESSION_DIRECTOR` XML block from `CURRENT_BATCH.md` line 1118..1173, and re-read the binary payload ledger before editing.
- Added required three-paragraph `NativeDisableContainerSafetyRestriction` justifications for the optional proximity/combat/panic `NativeQueue<T>.ParallelWriter` fields.
- Fixed NaN propagation risk: non-finite state/target AUP or velocity now writes a fault telemetry row and returns before `DowncastAupDelta`, SDF sampling, dot-product LOS, or `HashSpatial` can touch NaN.
- Padded parallel-written rows to 64-byte multiples to prevent adjacent job indices sharing cache lines: `MockPlayerAUP` 96B -> 128B, `ApexBrainOutputDTO` 160B -> 192B, legacy `AlphaLeviathanCognitionState` 144B -> 192B, legacy `AlphaLeviathanSteeringOutput` 88B -> 128B.
- Updated `ApexBrainVault.ValidateLayouts()` to the new false-sharing-safe sizes.
- Static scans remained clean for `Pack=1`, `Sequential`, NavMesh, physics raycasts, managed state machine, hot DTO `{ get; }` properties, `UnityEngine.Random`, `Time.deltaTime`, `foreach`, LINQ, `JobHandle.Complete`, `FloatMode.Fast`, `math.sincos`, and sibling runtime domain references.
- `git diff --check` passed for AI Cognition and SHINOBU docs; Git warned only about existing LF-to-CRLF normalization.
- Compiler proof remains blocked by hardware guard: latest sampled CPU was 100% with active compiler processes. No `dotnet build` was launched.

### Loop 17 - Computed Fault Early-Out

- Re-read active Apex status/rationale, extracted `CURRENT_BATCH.md` lines 1118..1173 for the predictive Apex prompt, and re-read the binary payload ledger before editing.
- Found a second quarantine gap: after `computedFinite == false`, the job still continued into aggression, node selection, signal writing, telemetry construction, and `HashSpatial(interceptLocal)`.
- Changed computed SDF/LOS faults to call `WriteFaultRow(..., 0x53484E4Eu)` and return immediately before biome, aggro, ambush node, signal, telemetry, or spatial-hash work.
- Removed the now-dead `faulted` hot-path selects and the active-path `faultCode` carrier; all normal rows now execute the direct finite path, and all fault rows use the single fault writer.
- Static forbidden scan over AI Cognition stayed clean for `Pack=1`, `Sequential`, `FloatMode.Fast`, `math.sincos`, NavMesh, physics casts, `Update()`, `Time.deltaTime`, `UnityEngine.Random`, runtime native allocations, hot DTO properties, LINQ/`foreach`, and `JobHandle.Complete`.
- Runtime compile guard is still blocking: latest sampled CPU was 100% with no compiler process. No `dotnet build` or targeted Roslyn compile was launched.
