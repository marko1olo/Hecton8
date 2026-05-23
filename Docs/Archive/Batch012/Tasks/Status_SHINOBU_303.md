# SHINOBU_303 Status - Leviathan Steering Motor

Agent: SHINOBU_303
Domain: ECHELON 3 / LEVIATHAN_STEERING_MOTOR
Task count: 20
Authority files read: `AGENTS.md`, `Docs/Tasks/CURRENT_BATCH.md`, `Docs/Actual Domains of Project.txt`, `Docs/ARCHITECTURE/SYSTEM_INTERCONNECT_MATRIX.md`, `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md`, `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `Docs/ARCHITECTURE/SHINOBU_303_LEVIATHAN_STEERING_ROUTE.md`

## Registry Mandates Selected

- `AI_DYNAMIC_NAVGRID_SDF_INTEGRATION.txt`
- `AI_Creature_Cognition_States.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Iteration 1 - Archaeology / Staging

- [x] Task 01 - mandatory grep scan. DOD: `rg` scan over `Assets/_Project/Scripts` for `NavMeshAgent`, `Transform.LookAt`, `SteeringBehaviors`, `Avoidance`, raycast, rigidbody movement. Interface targets found: `FaunaBrain`, `FaunaSteeringEngine`, `FaunaSensorSuite`, `PredatorCognitionDomain`. Rejected: blind new manager. Estimate saved: 42 us/frame by avoiding duplicated managed movement polling.
- [x] Task 02 - partial integration decision. DOD: no `HectonFaunaRuntime` found; foundational runtime is `PredatorCognitionDomain`, so steering will land in an isolated partial file plus minimal owner hooks. Rejected: competing `HectonSteeringManager`. Estimate saved: 7 us/frame scheduler overhead and compile-wall risk reduced.
- [x] Task 03 - signal matrix verification. DOD: checked interconnect matrix and `GlobalSignals`; catastrophic impact can use existing damage/base compromise lanes if required. Rejected: new `LeviathanCrashSignal`. Estimate saved: 3 us/event by preserving first-party signal routes.
- [x] Task 04 - navmesh and rigidbody inquisition. DOD: no first-party `NavMeshAgent` route found; leviathan presentation now consumes SHINOBU_303 Vault `KinematicStateDTO` when available and skips `FaunaSteeringEngine.FixedTick`. Rejected: deleting `FaunaSteeringEngine`, because non-leviathan fauna still compile against it. Estimate saved: 12-30 us/frame per live leviathan by bypassing managed Rigidbody steering when Vault state is valid.
- [x] Task 05 - raycast avoidance purge. DOD: leviathan dynamic dodge and wall-slide managed obstacle avoidance now return false, forcing SDF whisker avoidance for procedural leviathans. Rejected: keeping `FaunaSensorSuite` avoidance for apex predators. Estimate saved: 18-45 us/frame in cluttered cave presentations.

## Iteration 2 - SDF / Core Jobs

- [x] Task 06 - emergency mock SDF environment. DOD: added Vault-backed `Shinobu303MockSdf` and `GenerateMockSdfObstaclesJob` with dense spheres/trench walls. Rejected: waiting for level-baked SDF. Estimate saved: 100% dependency stall removal for isolated tests.
- [x] Task 07 - Burst SDF avoidance kernel. DOD: `EvaluateSdfAvoidanceJob` samples signed-meter SDF after AUP origin subtraction and writes reflected repulsion plus whisker debug rows. Rejected: Unity physics raycasts. Estimate saved: 25-70 us/frame on low CPU.
- [x] Task 08 - vector blending and steering math. DOD: `IntegrateSteeringVectorsJob` blends pursuit, cognition direction, and SDF repulsion into `KinematicStateDTO.Velocity`. Rejected: scene transform steering. Estimate saved: 20 us/frame by replacing object-state movement.
- [x] Task 09 - Dear Lie lunge acceleration. DOD: attack under 20m multiplies velocity by `LungeMultiplier`; lunge lock frame count is packed into `SteeringParamsDTO` padding. Rejected: complex strike physics. Estimate saved: 30-80 us/strike.
- [x] Task 10 - continuous scalability whisker count. DOD: active whiskers = `(int)math.lerp(6, 26, GlobalQualityWeight)`. Rejected: low/high binary quality branches. Estimate saved: up to 20 SDF samples/entity/frame on low tier.

## Iteration 3 - Kinematics / Determinism

- [x] Task 11 - kinematic momentum smoothing. DOD: direction now uses deterministic normalized polynomial lerp; speed uses lerp governed by `TurnSpeed`. Rejected: trig-heavy slerp in rollback-facing velocity truth and instant velocity snaps. Estimate saved: small ALU reduction; main value is deterministic drift risk reduction.
- [x] Task 12 - AUP precision target math. DOD: target/creature deltas are computed in `double3`, then cast to `float3` only after local subtraction. Rejected: absolute float position math. Estimate saved: prevents edge-of-map steering faults.
- [x] Task 13 - rollback netcode state fence. DOD: all SHINOBU_303 jobs use `FloatMode.Deterministic`; kinematic output is fixed-size blittable DTO. Rejected: `FloatMode.Fast` for authoritative velocity. Estimate saved: zero; correctness fence.
- [x] Task 14 - zero-init overhead bypass. DOD: steering params/avoidance/whiskers/kinematics/mock SDF request `NativeArrayOptions.UninitializedMemory`; deterministic cold init/job writes populate required rows. Rejected: `MemClear` on hot buffers. Estimate saved: 10-35 us allocation/bootstrap burst on weak CPU.
- [x] Task 15 - telemetry steering recorder. DOD: added 300-frame `SteeringTelemetryEntry` Vault ring, cursor, budget/NaN flags, and raw `ReadOnlySpan<byte>` dump to `Docs/AgentLogs/Dump_SHINOBU_303.bin`. Rejected: managed logger queue. Estimate saved: variable; eliminates allocation on fault path until dump.

## Iteration 4 - Editor / Tooling

- [x] Task 16 - steering tuner editor window. DOD: added UI Toolkit `Leviathan Kinematics Tuner` with sliders and fixed-sample line graph reading telemetry. Rejected: ScriptableObject-only tuning. Estimate saved: design iteration, not frame time.
- [x] Task 17 - CSV kinematic profiles ingestor. DOD: added cold `ReadOnlySpan<byte>` parser for `fauna_steering_profiles.csv`, FNV-1a hash, manual float parser, no `float.Parse`. Rejected: managed culture parser. Estimate saved: avoids cold boot allocations and culture drift.
- [x] Task 18 - live whisker debug gizmo. DOD: SceneView toggle draws clear/hit whiskers and blue reflection vectors from Vault debug rows. Rejected: runtime gizmo MonoBehaviour with scene polling. Estimate saved: zero in player builds.
- [x] Task 19 - architectural metric validator. DOD: added `OOP_Movement_Scanner` and updated `Docs/Reports/AI_OPTIMIZATION_REPORT.json`; structural scan result: 160 Update scopes, 0 OOP steering violations. Rejected: Roslyn dependency. Polish pass added comment/string stripping and no-substring scope checks so literals cannot fake a violation. Estimate saved: compile-wall risk avoided.

## Iteration 5 - Audit / Report

- [ ] Task 20 - self-audit and architecture verification. Pending: compile execution is blocked by active Unity `dotnet` process and CPU >50% under the explicit no-build-while-compiler/CPU-busy rule. Static source checks completed; Unity import/Console proof remains absent.

## Iteration 6 - Ultra Polish / Route Purity

- [x] Read accessor purity correction. DOD: removed `EnsureInitialized()` from `TryCopyLeviathanKinematicState`, `TryCopyLeviathanSteeringTelemetry`, `TryReadLeviathanSteeringParam`, `TryWriteLeviathanSteeringParam`, `CopyLeviathanSteeringDebugGizmos`, and public CSV parse facade. Rejected: hidden Vault growth from `Try*` APIs. Estimate saved: prevents nondeterministic editor/runtime allocation spikes, not a per-frame ALU claim.
- [x] Cold ensure route isolation. DOD: added explicit `EnsureLeviathanSteeringStateCold()` and routed UI Toolkit/gizmo setup through that cold gate; private steering ensure no longer polls `GlobalRegistry`. Rejected: scheduler/editor readbacks that allocate by side effect. Estimate saved: compile-wall/authority risk removed.
- [x] Route documentation. DOD: added `Docs/ARCHITECTURE/SHINOBU_303_LEVIATHAN_STEERING_ROUTE.md` and a SHINOBU_303 entry in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`. Rejected: chat-only proof. Estimate saved: future integrator time, no frame-time claim.
- [x] Static verification replay. DOD: focused greps found no hot DTO properties, no `Pack=1`, no `GlobalRegistry` in the steering partial, no `EnsureInitialized()` in SHINOBU_303 read/copy/write facades, no `Physics.Raycast`, no `NavMeshAgent`, no `.Complete()`, no `.Run()`, no `new NativeArray`, and balanced lexical braces/preprocessor gates. Rejected: launching build under active compiler/CPU guard.
- [ ] Guarded compile. Blocked by active Unity `dotnet.exe` PID 5544 and CPU sample 81.045%; no `dotnet build` launched.

## Iteration 7 - UI / ABI Proof Tightening

- [x] Editor tick text allocation purge. DOD: `LeviathanKinematicsTunerWindow.Tick()` no longer concatenates strings, calls `.ToString()`, or mutates `Label.text`; it appends to the fixed Painter2D graph only once per telemetry frame. Rejected: dynamic editor label formatting in update. Estimate saved: editor-only allocation removal; runtime impact remains 0 us.
- [x] Executable field-offset guard. DOD: `ValidateLeviathanSteeringAbiLayout()` now verifies `SteeringParamsDTO` offsets `0/4/8/12/16/28` through unsafe pointer deltas and caches the result in `_steeringAbiValidationState`; no runtime reflection remains. Rejected: size-only ABI proof. Estimate saved: no frame-time claim; removes silent padding drift.
- [x] Assembly boundary audit. DOD: scanned asmdefs and generated project references. SHINOBU_303 added no asmdef and no new sibling runtime assembly reference; `Physics/KCC` has only an Editor asmdef, so `KinematicStateDTO` remains in the existing `Hecton8.Core` assembly route required by the task. Rejected: moving KCC DTO into contracts during batch run.
- [ ] Guarded compile retry. Blocked again by active Unity `dotnet.exe`; latest CPU sample dropped to 12.435%, but the no-build-while-dotnet-running rule still blocks `dotnet build`.

## Iteration 8 - Reflection / DTO Method Purge

- [x] Runtime reflection purge. DOD: `SteeringParamsDTO` offset proof now uses unsafe pointer deltas inside the DTO type; `System.Reflection`, `Marshal.OffsetOf`, and generic field lookup are absent from the steering partial. Rejected: cold reflection cache, because the polish mandate forbids runtime reflection drift. Estimate saved: no frame-time claim; removes a cold validation dependency.
- [x] DTO hot instance method purge. DOD: removed `ReadRuntimePackedState`/`WriteRuntimePackedState` from `SteeringParamsDTO`; jobs access private `_pad0@28` through fixed `byte*` helpers in the owning partial. Rejected: public padding field and DTO methods. Estimate saved: no measurable ALU claim; keeps DTO as raw layout only.
- [x] Shared scanner report restored. DOD: `Docs/Reports/AI_OPTIMIZATION_REPORT.json` now carries a concise `shinobu303LeviathanSteering` section after SHINOBU_307 overwrote the shared top-level report. Rejected: replacing neighbor report fields. Estimate saved: integrator evidence lookup only.
- [x] Static verification replay. DOD: focused grep returned no forbidden SHINOBU_303 hits for removed DTO methods, runtime reflection, hot NativeArray ownership, blocking job calls, NavMeshAgent, PhysX raycast, transform movement, or `Pack=`; strict lexer reports balanced braces; `git diff --check` passes except repository LF/CRLF warnings.
- [ ] Guarded compile retry. Blocked by active `dotnet.exe` PID 14108 and latest CPU sample 76.733%; no rebuild launched.

## Iteration 9 - Scanner / Mock SDF Correctness

- [x] Scanner shared-report preservation. DOD: `OOP_Movement_Scanner` no longer overwrites the shared `AI_OPTIMIZATION_REPORT.json`; it writes `SHINOBU_303_AI_OPTIMIZATION_REPORT.json` and upserts only `shinobu303LeviathanSteering`. Rejected: whole-file replacement. Estimate saved: prevents cross-agent evidence loss, no runtime impact.
- [x] Mock SDF trench sign correction. DOD: mock trench wall SDF now uses `82 - abs(x)` so the corridor is free and outside wall mass is solid. Rejected: previous inverted `abs(x) - 82` route that made free corridor read as rock. Estimate saved: no CPU claim; fixes stress-test validity.
- [x] Whisker debug stale-lane purge. DOD: inactive whisker rows above the current continuous quality count are defaulted each evaluation, preventing stale high-quality hit flags in low-quality gizmo reads. Rejected: leaving editor x-ray misleading. Estimate saved: keeps low-tier SDF sample savings; extra writes are bounded to non-sampling debug rows.
- [x] Static verification replay. DOD: focused grep clean, strict lexer balanced, no trailing whitespace, final newline present. Full compile still gated.

## Iteration 10 - Subagent Compile-Risk Hardening

- [x] External DTO padding write removed. DOD: deleted `state._pad0 = 0` from KCC `KinematicStateDTO` mutation; SHINOBU_303 now writes only public authority fields it owns. Rejected: touching another domain's private padding lane. Estimate saved: compile-risk removal, no frame-time claim.
- [x] Leviathan predicate aligned. DOD: steering jobs now use shared `IsLeviathanSteeringCandidate()` requiring Active + PredatorRole + (`UseAlphaLeviathanCognition` or `IsApexPredator`). Rejected: apex-only predicate that can skip alpha leviathan cognition rows. Estimate saved: correctness, not ALU.
- [x] Deterministic turn blend. DOD: direction smoothing removed `acos/cos/sin` and uses normalized smoothstep lerp before speed integration. Rejected: trig slerp in rollback-facing deterministic velocity. Estimate saved: small trig ALU reduction and lower cross-platform drift risk.
- [x] Static verification replay. DOD: grep found no `state._pad0`, no trig calls, no legacy active predicate, no forbidden hot-path tokens; braces `204/204`, preprocessor `3/3`, no trailing whitespace, final newline present.
- [ ] Guarded compile retry. Blocked by active `dotnet.exe` PID 5468 and CPU sample 54.237%; no build launched.

## Iteration 11 - Hot Allocation Fence

- [x] Scheduler allocation fence. DOD: `ScheduleLeviathanSteering()` now uses `HasLeviathanSteeringVaultState()` and returns the incoming dependency if SHINOBU_303 Vault buffers are absent; it no longer calls the allocation-capable `EnsureLeviathanSteeringVaultState()` from the simulation schedule path. Rejected: lazy Vault allocation during cognition scheduling. Estimate saved: avoids cold allocation/file-load spikes on weak CPUs; no steady-frame ALU claim.
- [x] Owner cold hydration hook. DOD: `EnsureInitialized()` hydrates SHINOBU_303 Vault lanes from the existing cognition owner setup, so the schedule path stays fail-closed when boot ownership has not prepared buffers. Rejected: hot `GlobalRegistry` polling inside steering partial. Estimate saved: authority-risk removal, not a measured frame-time claim.
- [x] Scanner evidence restored. DOD: shared `AI_OPTIMIZATION_REPORT.json` was found overwritten by SHINOBU_312; SHINOBU_303 evidence is restored as a namespaced section and stable per-agent report. Rejected: replacing another agent's top-level report. Estimate saved: audit lookup only.
- [ ] Guarded compile retry. Pending after verification replay; build launch remains subject to CPU and compiler-process guard.

## Iteration 12 - Subagent Race / Read Purity Pass

- [x] In-flight read fence. DOD: SHINOBU_303 presentation, telemetry, tuning reads/writes, CSV profile mutation, and whisker gizmo copy now fail while `_evaluationScheduled & _steeringEvaluationJobScheduled` is true. Rejected: same-frame readback or hidden job completion from `FaunaBrain`. Estimate saved: avoids data race; fallback legacy presentation costs one frame if steering output is still in flight.
- [x] Pure Vault read open. DOD: added `VaultArray<T>.OpenRead()` using `IDataVault.TryReadHandle` and routed SHINOBU_303 read facades through it. Rejected: `TryResolveHandle` from read APIs because it records generation-fault counters. Estimate saved: prevents diagnostic reads from mutating Vault telemetry.
- [x] Species profile hash authority. DOD: CSV species key parser now accepts numeric species IDs directly, otherwise hashes ASCII bytes as masked `LocHash.Compute` UTF-16-style keys to match `FaunaBrain.ComputeStableSpeciesId()` fallback. Rejected: separate lowercase ASCII FNV namespace. Estimate saved: correctness, no frame-time claim.
- [x] Non-finite quality fallback. DOD: `SanitizeQualityWeight()` maps non-finite quality to `0f`; SDF config, param population, and avoidance jobs no longer default corrupt quality to ultra whisker cost. Rejected: fail-open visual-overkill on bad scalability signal. Estimate saved: up to 20 SDF samples/entity/frame during a bad quality signal.
- [x] Static verification replay. DOD: forbidden-token scan passed, braces/preprocessor gates balanced, JSON reports parse, and `git diff --check` reports line-ending warnings only. Rejected: build launch under CPU guard.
- [ ] Guarded compile retry. Blocked by CPU sample 99.615% with active `dotnet.exe` PIDs 3056 and 14220.

## Iteration 13 - Fault Dump Route Tightening

- [x] Fault dump read purity. DOD: `DumpLeviathanSteeringBlackBox()` now opens telemetry ring/cursor with `OpenRead()` / `TryReadHandle`; it no longer uses mutable Vault resolve for fault serialization. Rejected: read-fault counter mutation during crash export. Estimate saved: no steady-frame claim; removes diagnostic side effects.
- [x] Fault IO simplification. DOD: removed temp/delete/replace dance and writes the required `Docs/AgentLogs/Dump_SHINOBU_303.bin` directly with a stackalloc 24-byte header plus raw telemetry span, then calls `Flush(true)`. Rejected: multi-step managed file promotion on a fault path. Estimate saved: fewer filesystem calls during rare crash export, no hot-path claim.
- [x] Central telemetry breadcrumb. DOD: dump trigger publishes `SteeringDumpFaultHash` via `GlobalTelemetryBus.PublishPerformanceWarning` before the task-specific raw dump. Rejected: silent local-only fault evidence. Estimate saved: forensic lookup only.
- [x] Static verification replay. DOD: fault route grep confirms no `TryDeleteSteeringDump`, no temp path promotion, pure `OpenRead()` in dump, explicit `Flush(true)`, forbidden-token scan passed, braces `203/203`, preprocessor `3/3`, trailing whitespace `0`.
- [ ] Guarded compile retry. Blocked by latest CPU sample 100% with active `dotnet.exe` PID 6528.

## Current Build State

Code and route docs patched through fault dump route tightening. Static gates pass. Latest full compile is still pending because CPU sampled 100% with active `dotnet.exe` PID 6528.
