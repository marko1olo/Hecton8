# Status_SHINOBU_309

Agent: SHINOBU_309
Role: PLANKTON_NUTRIENT_FLOW_DRIFT
Domain: Echelon 3 biota scalar-field drift; Environment/Atmosphere read-only flow consumption
Task Count: 20
Status: PENDING LOOP21 CORE COMPILE GATE / UNITY IMPORT / PLAYMODE / PROFILER PROOF; PRIOR CORE COMPILE GREEN

## Mandates Selected Before Coding

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DATA_Runtime_Struct_Layout_ARM64.txt
- MATH_AUP_Determinism_Sync.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- ARCH_Execution_Phases.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Signal_Lane_Segregation.txt

## Assignment Source

Extracted from `Docs/Tasks/CURRENT_BATCH.md` using a CLI regex bound to `<AGENT_PROMPT id="SHINOBU_309" ...>`. Re-extracted before implementation and after three-task progress checkpoints.

## Loop 0 - Intake

- [x] Prompt extraction | DOD practice: strict XML extraction from batch file with `Get-Content -Raw` and regex bound to `id="SHINOBU_309"` | Alternative rejected: reading neighboring prompts or relying on chat text | Estimate: 15 us static parse budget, runtime cost 0 us
- [x] Domain boundary read | DOD practice: read `Docs/Actual Domains of Project.txt` before source mutation | Alternative rejected: assuming Environment ownership from prompt alone | Estimate: 0 us runtime
- [x] Mandate selection | DOD practice: selected eight task-relevant registry mandates before coding | Alternative rejected: broad registry load without relevance filter | Estimate: 0 us runtime

## Loop 1 - Archaeology

- [x] Task 01 MANDATORY_CODEBASE_GREP_SCAN | DOD practice: `rg` scans over Environment, AI, Ecosystem, World, and VFX for particle/nutrient/grid/flow/Vault routes | Alternative rejected: duplicate system creation without source scan | Estimate: runtime 0 us
- [x] Task 02 PARTIAL_CLASS_INTEGRATION_MANDATE | DOD practice: no `HectonFluidDynamicsRuntime` found; created isolated `NutrientDriftRuntime` following `MacroEcosystemMathematicianRuntime`/`ChemicalInfluenceGrid` patterns | Alternative rejected: partial injection into player/atmosphere classes | Estimate: runtime 0 us
- [x] Task 03 SIGNALBUS_MATRIX_VERIFICATION | DOD practice: read interconnect docs and thermal registry routes; no new hot signal lane added | Alternative rejected: inventing `VentEruptionSignal` when `PersistentWorldRegistry` already publishes bounded vent snapshots | Estimate: avoids one queue flush/listener fan-out

## Loop 2 - Sanitation And Vault Staging

- [x] Task 04 PARTICLE_COLLISION_INQUISITION | DOD practice: targeted scanner found 0 `ParticleSystem`/collision/Rigidbody authority hits in Environment/AI/Ecosystem roots | Alternative rejected: deleting unrelated World/VFX presentation particles | Estimate: no direct deletion delta; prevents future object-particle nutrient route
- [x] Task 05 GAMEOBJECT_SPAWNER_PURGE | DOD practice: no managed plankton GameObject spawner found; runtime uses scalar grid only | Alternative rejected: ripping out VFX plankton rendering and existing sector heatmaps | Estimate: no transform churn introduced
- [x] Task 06 EMERGENCY_MOCK_FLOW_FIELD | DOD practice: `CopyAbyssalFlowVolumeToNutrientFlowJob` consumes cached `IAbyssalFlowVolumeReadModel.TryGetAbyssalFlowVolumePayload` when present; Burst `GenerateMockFlowFieldJob` writes deterministic fallback flow into Vault `70462` only when that route is unavailable | Alternative rejected: inventing a direct dependency on absent Agent 105 code, retaining a concrete World owner field in the nutrient runtime, or creating scene vectors | Estimate: 35-70 us at 16^3 mock, 120-300 us at 32^3 with trilinear flow-volume sampling on i3/MX350 class CPU

Compile gate after Tasks 1-6: blocked. CPU 30%, but `dotnet.exe` PID 6776 running Unity `VBCSCompiler.dll`; no `dotnet build` launched by rule.

## Loop 3 - Core Burst Math

- [x] Task 07 BURST_ADVECTION_SOLVER_KERNEL | DOD practice: `EvaluateNutrientAdvectionJob` performs reverse-trajectory semi-Lagrangian advection over flat pointers | Alternative rejected: particle actors, Rigidbody drift, or same-frame scene queries | Estimate: 180-450 us depending active axis and interpolation weight
- [x] Task 08 DOUBLE_BUFFERED_STATE_SWAP | DOD practice: Vault front/back buffers `70460/70461` swap only after `DispatcherJobFence.TryFinalizeCompleted` | Alternative rejected: in-place density mutation while readers sample | Estimate: 1-4 us handle swap plus fence finalization
- [x] Task 09 THE_DEAR_LIE_VISUAL_REPRESENTATION | DOD practice: density upload buffer `70469` feeds one RFloat `Texture3D` and shader globals; visual texture is not gameplay truth | Alternative rejected: visible plankton particles/colliders | Estimate: 20-200 us upload cadence, GPU cost outside this owner
- [x] Task 10 INJECTION_AND_DECAY_MATH | DOD practice: thermal vent AUP sources copied to `70464/70465`, injection computed after double precision AUP subtraction, decay is scalar clamp | Alternative rejected: buoyant fluid simulation/protons | Estimate: 60-180 us at base grid

Compile gate after Tasks 7-10: blocked by active Unity compiler server PID 6776. Static `git diff --check` reported CRLF normalization warnings only.

## Loop 4 - Scalability, Precision, And Telemetry

- [x] Task 11 CONTINUOUS_SCALABILITY_INTERPOLATION | DOD practice: `GlobalQualityWeight` continuously scales active axis, nearest/trilinear blend, and texture upload cadence | Alternative rejected: low/high binary quality switches | Estimate: saves 35-55% advection ALU when weight trends low
- [x] Task 12 AUP_PRECISION_GRID_WRAPPING | DOD practice: source injection subtracts `GridOriginAup` from vent AUP in double precision before float local-grid cast; sampling wraps toroidally | Alternative rejected: absolute float world positions | Estimate: O(1), no grid memcpy
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE | DOD practice: tuning flags mark netcode exclusion; public output is visual/ecology snapshot only, no StateRingBuffer/Merkle/save route | Alternative rejected: adding nutrient field to rollback truth | Estimate: runtime hash cost 0 us
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | DOD practice: Vault buffers use `NativeArrayOptions.UninitializedMemory`, then one cold bootstrap job deterministically seeds cells/telemetry | Alternative rejected: per-frame clear or cold full zero-fill dependency | Estimate: removes hot zero-fill entirely
- [x] Task 15 TELEMETRY_FLUID_GRID_RECORDER | DOD practice: 300-entry `FluidGridTelemetryEntry` ring in `70467`, cursor in `70468`, NaN/over-budget dump to `Docs/AgentLogs/Dump_SHINOBU_309.bin` | Alternative rejected: managed per-frame log strings | Estimate: 8-20 us telemetry write, dump only on fault

Self-read correction in Loop 4: `ReadSnapshotReady` now evaluates raw tuning flags before sanitization so an uninitialized row cannot be falsely accepted.

## Loop 5 - Presentation And Static Proof

- [x] Task 16 FLUID_ADVECTION_TUNER_WINDOW | DOD practice: UI Toolkit `NutrientDriftTunerWindow` edits Vault tuning through `TryWriteTuning` and graphs telemetry | Alternative rejected: inspector-only serialized MonoBehaviour authority | Estimate: editor-only
- [x] Task 17 CSV_NUTRIENT_PROFILES_INGESTOR | DOD practice: cold CSV parser uses fixed Vault scratch `70471`, profile table `70472`, span parser, FNV1A keys | Alternative rejected: Newtonsoft/managed row objects/LINQ | Estimate: cold reload only
- [x] Task 18 LIVE_GRID_SLICE_GIZMO | DOD practice: editor SceneView `OnDrawGizmos` draws a bounded density slice from read-only snapshots | Alternative rejected: spawning debug cubes/cell GameObjects | Estimate: editor-only
- [x] Task 19 ARCHITECTURAL_METRIC_VALIDATOR | DOD practice: `NutrientDriftParticleScanner` and shared report section prove 0 particle/Rigidbody nutrient authority hits in scoped roots | Alternative rejected: manual report without repeatable scanner | Estimate: tool-only
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD practice: `NutrientDriftSelfAudit.BuildSelfAuditXml()` validates 20 task entries, DTO size/offsets, Vault ID range, quality curve, dependency graph, compile guard, and Dear Lie route; JSON report parsed successfully | Alternative rejected: chat-only proof | Estimate: static + editor proof; compile still gated

## Verification Log

- Prompt extraction: completed with strict `SHINOBU_309` XML block.
- Static scan: no `ParticleSystem`, particle collision, collision event, trigger event, or `Rigidbody` hits in Environment/AI/Ecosystem scope.
- JSON report validation: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parsed through `ConvertFrom-Json`; SHINOBU_309 section present.
- Whitespace check: `git diff --check` returned only CRLF normalization warnings for pre-existing line-ending policy.
- Post-polish lexical brace depth: `NutrientDriftRuntime.cs=0`, `NutrientDriftTunerWindow.cs=0`, `NutrientDriftParticleScanner.cs=0`.
- Post-polish shared report parse: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` parsed through `ConvertFrom-Json`; scanner upsert route names `Fluid_Particle_Scanner`.
- AST scanner polish: existing editor assembly already contains Roslyn scanners; SHINOBU_309 scanner now uses `CSharpSyntaxTree`. Scoped fallback token proof over 64 Environment/AI/Ecosystem source files found zero forbidden `ParticleSystem`/collision/Rigidbody tokens. JSON report still parses.
- Compile: initially not run. First gate: CPU 30% with Unity `dotnet.exe` PID 6776 running `VBCSCompiler.dll`. Later gate: CPU 62% with `dotnet.exe` PID 5544 active. Final early gate: CPU 97% with `dotnet.exe` PIDs 3104 and 12624 active. After polish, CPU sampled 25.6% and no dotnet/csc/VBCSCompiler process was reported, so one narrow `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1` was run.
- Compile wall: build failed outside SHINOBU_309 with 11 missing-type diagnostics in `Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs` and `Assets/_Project/Scripts/World/EcosystemDirector.cs` for `FaunaGeneticsTuningDTO`, `FaunaGeneticsProfileDTO`, and `GeneticsTelemetryEntry`. No SHINOBU_309 diagnostic was emitted before this external wall.
- Post-wall compile proof: after Loop 7, guarded `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1` succeeded with 0 errors and 0 warnings in 2.24s.
- Loop 8 compile gate: not launched. CPU sampled 91.7% with Unity `dotnet.exe` PID 14108 active after interpolation/telemetry polish. Static gates passed; compile must wait for guard clearance.
- Loop 9 prompt re-extraction: strict `SHINOBU_309` XML block length 19115 chars; task count re-confirmed as 20.
- Loop 9 tuner graph/check: `NutrientDriftTunerWindow` no longer contains `IMGUIContainer`, `GUILayoutUtility`, `EditorGUI.DrawRect`, `Handles.BeginGUI`, `Handles.EndGUI`, `Vector3[]`, `new Vector3[`, or `DrawSolidRectangleWithOutline`; telemetry graph now uses UI Toolkit `generateVisualContent`/`Painter2D`, and SceneView slice uses disc/wire-disc handles without a private array.
- Loop 10 prompt re-extraction: strict `SHINOBU_309` XML block length 19115 chars; task count re-confirmed as 20.
- Loop 10 static gates: JSON report parsed, string-aware brace depth is 0 for runtime/tuner/scanner, `git diff --check` reports only CRLF normalization warnings for ledger/report.
- Loop 10 compile gate: not launched. CPU sampled 99.6% with Unity `dotnet.exe` PID 5468 active.
- Loop 11 static gates: JSON report parsed, runtime string-aware brace depth is 0, grid-header lock/unlock tokens present, `git diff --check` reports only CRLF normalization warnings for ledger/report.
- Loop 11 compile gate: not launched. CPU sampled 63.8% with Unity `dotnet.exe` PID 5468 active.
- Loop 15 source-injection static gates: runtime string-aware brace depth is 0; low-quality source falloff now uses squared-distance weight without `sqrt`, mid-quality blends squared/exact radial, high-quality keeps exact radial shape.
- Loop 15 compile gate: not launched. CPU sampled 100% with Unity `dotnet.exe` PIDs 3056 and 16936 active.
- Loop 16 editor asmdef static gate: local `Hecton8.Ecosystem.NutrientDrift.Editor.asmdef` parses as JSON, carries Roslyn precompiled references, and has `autoReferenced=false` to avoid broad editor compile-wall expansion.
- Loop 16 compile gate: not launched. Latest gate sampled CPU 100% with Unity `dotnet.exe` PIDs 6528 and 15572 active.
- Loop 17 prompt re-extraction: strict `SHINOBU_309` XML block length 19115 chars; task count re-confirmed as 20.
- Loop 17 mock-flow static gates: runtime string-aware brace depth is 0; `git diff --check` is clean for `NutrientDriftRuntime.cs`; fallback mock flow now uses squared-radius falloff at low quality and defers `sqrt` to middle/high quality only.
- Loop 17 compile gate: not launched. Latest gate sampled CPU 100% with no active `dotnet`/`csc`/`VBCSCompiler`; CPU rule still blocks build.
- Loop 18 prompt re-extraction: strict `SHINOBU_309` XML block length 19115 chars; task count re-confirmed as 20.
- Loop 18 contract-route static gates: `NutrientDriftRuntime` no longer contains concrete `PersistentWorldRegistry` or `HectonMapMagicVegetationBridge` type references, `_persistentWorldRegistry`, `_vegetationBridge`, `GlobalRegistry.PersistentWorldRegistry`, or `GlobalRegistry.MapMagicVegetation`; only the `GlobalRegistryServiceSlot.PersistentWorldRegistry` hot-swap enum case remains as slot identity. It caches `INutrientThermalVentReadModel` and `IAbyssalFlowVolumeReadModel` from cold registry/hot-swap only. Focused `git diff --check` reports CRLF warnings only for touched core/world files.
- Loop 18 compile gate: not launched. Latest gate sampled CPU 100% with active `csc.exe` PID 7748 and `dotnet.exe` PID 16748; CPU/compiler rules block build.
- Loop 19 prompt re-extraction: strict `SHINOBU_309` XML block length 19115 chars; task count re-confirmed as 20.
- Loop 19 evidence consistency static gates: JSON report parsed; stale concrete-route proof strings are absent from status/rationale/report; `NutrientDriftRuntime` concrete-owner scan returns only `GlobalRegistryServiceSlot.PersistentWorldRegistry` as hot-swap slot identity; focused `git diff --check` reports CRLF warnings only for touched core/world/ledger/report files.
- Loop 19 compile gate: not launched. Latest gate sampled CPU 100% with active `dotnet.exe` PID 12344; CPU/compiler rules block build.
- Loop 20 source hygiene audit: `NutrientDriftRuntime` and carrion partial show no `new NativeArray`/`NativeList`/`NativeHashMap`, no LINQ, no hot `Time.deltaTime`, no runtime particle/Rigidbody authority, and no DTO auto-properties. `.Complete()` hits are documented cold bootstrap/editor stress sync points. `NutrientDriftRuntime` has 8 Burst job attributes and all use the deterministic synchronous standard-precision form; `NoAlias` fields are present on pointer/native lanes. `using Hecton8.World` remains only for AUP/common contract types, not concrete owner fields.
- Loop 20 compile gate: not launched. Latest gate sampled CPU 26% with active `csc.exe` PID 15232 and `dotnet.exe` PID 10876; compiler-process rule blocks build.
- Loop 21 self-audit hardening: `BuildSelfAuditXml()` now emits explicit Tasks 01-20, `NutrientCellDTO` offset math `4+4+4+4=16`, secondary DTO size checks, Vault `70460..70473`, continuous quality curve notes, H-Phi ownership, NoAlias/dependency graph, compile guard, Dear Lie complexity, and netcode exclusion. Raw brace count after patch is `201/201`; focused `git diff --check` on runtime source is clean.
- Loop 21 shared report restoration: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` was externally overwritten by a SHINOBU_326 scanner and lost SHINOBU_309 again; restored `shinobu_309_plankton_nutrient_flow_drift` while preserving the current SHINOBU_326/325 objects. `ConvertFrom-Json` passed and SHINOBU_309 object is present.
- Loop 21 compile gate: not launched. Latest gate sampled CPU 100% with active Unity `dotnet.exe` PID 16552; CPU/compiler rules block build.

## Loop 6 - Polish Mandate Corrections

- [x] Hot registry repair removed | DOD practice: `TryReadAbyssalFlowPayload` now reads only the cached bridge injected during cold registry/hot-swap phases; no helper-path `GlobalRegistry.MapMagicVegetation` lookup occurs from `FrostTick` | Alternative rejected: lazy per-tick bridge self-healing | Estimate: saves O(1) lookup and removes hidden route mutation from hot cadence
- [x] Runtime CSV polling removed | DOD practice: `nutrient_drift_profiles.csv` loads only during cold Vault initialization or explicit editor reload | Alternative rejected: `File.Exists`/timestamp checks every FrostTick | Estimate: saves managed filesystem IO/string churn from gameplay cadence
- [x] Player concrete read removed | DOD practice: grid origin reads `IPlayerRuntimeContext.TryGetMovementRuntimeState` and `PredictedAup` snapshot instead of `HectonPlayerMovement.CurrentAup` | Alternative rejected: concrete player movement dependency in nutrient owner | Estimate: runtime cost unchanged, compile-wall surface reduced
- [x] Scene slice allocation removed | DOD practice: editor slice gizmo uses `Handles.DrawSolidDisc`/`DrawWireDisc` and holds no private corner array | Alternative rejected: per-cell `new Vector3[]` or a persistent private `Vector3[]` corner cache | Estimate: editor-only GC/array ownership removed from every visible slice cell
- [x] Scanner facade named to prompt | DOD practice: `Fluid_Particle_Scanner` menu facade now wraps the existing nutrient particle authority scanner and report names the prompt scanner | Alternative rejected: duplicating scanner logic | Estimate: tool-only
- [x] Shared report preservation fixed | DOD practice: scanner now upserts only the SHINOBU_309 section and preserves neighboring report objects | Alternative rejected: overwriting `RENDERING_OPTIMIZATION_REPORT.json` with a single-agent file | Estimate: tool-only
- [x] Binary payload ledger updated | DOD practice: documented BufferIDs, DTO ABI, runtime route, scalability, Dear Lie, and compile-wall evidence in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` | Alternative rejected: chat-only architecture proof | Estimate: static doc-only

## Loop 7 - AST Scanner Tightening

- [x] Roslyn AST particle scanner | DOD practice: `Fluid_Particle_Scanner` now parses C# through `CSharpSyntaxTree`, walks syntax nodes, classifies Environment particle/Rigidbody authority unless instant-impact VFX context is proven, and reports parser failures separately | Alternative rejected: source-line substring scanning that can match comments/strings and miss syntax context | Estimate: editor/tool-only; runtime 0 us
- [x] Report schema refreshed | DOD practice: SHINOBU_309 section now records `ROSLYN_AST_TARGETED`, `scannerUsesRoslynAst`, source file count, parser failure count, and node count | Alternative rejected: stale line-scan evidence after scanner upgrade | Estimate: doc/tool-only
- [x] Post-wall guarded Core compile | DOD practice: after CPU sampled 49.2% and no `dotnet`/`csc`/`VBCSCompiler` process was present, ran one narrow `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -maxcpucount:1`; result 0 errors / 0 warnings in 2.24s | Alternative rejected: broad rebuild or compile under active compiler load | Estimate: tool-only

## Loop 8 - Continuous Cost Collapse Audit

- [x] Flow-volume interpolation bypass | DOD practice: `CopyAbyssalFlowVolumeToNutrientFlowJob` now maps `GlobalQualityWeight` through `smoothstep(0.30,0.90)` and selects 1-tap nearest at the low endpoint, nearest/trilinear blend at middle weights, and pure trilinear at the high endpoint | Alternative rejected: always paying 8 flow-volume taps then lerping visually | Estimate: low endpoint saves 7 flow reads plus lerps per active cell
- [x] Density advection interpolation bypass | DOD practice: `EvaluateNutrientAdvectionJob` now skips `SampleTrilinear` at the low endpoint and skips `SampleNearest` at full-quality endpoint | Alternative rejected: computing both nearest and trilinear for every cell regardless of quality | Estimate: low endpoint saves 8 nutrient cell reads per active cell; high endpoint saves one redundant nearest read
- [x] Mock-source telemetry flag corrected | DOD practice: `RecordNutrientTelemetryJob` now reads source flags from the bounded source buffer before setting the mock-source telemetry bit and uses local const bit names instead of literals | Alternative rejected: assuming any single source is the mock fallback | Estimate: bounded 0-16 source flag reads in one telemetry job only

## Loop 9 - UI Toolkit Graph Cleanup

- [x] Tuner telemetry graph converted from IMGUI to retained UI Toolkit | DOD practice: `NutrientDriftTunerWindow` uses `VisualElement.generateVisualContent` plus `Painter2D` for the 300-frame ring graph | Alternative rejected: `IMGUIContainer`/`GUILayoutUtility`/`Handles.BeginGUI` graph path that could allocate and violates the requested UI Toolkit facade | Estimate: editor-only, removes IMGUI repaint bridge from the tuner graph
- [x] Scene slice private array removed | DOD practice: live slice now draws density cells with disc/wire-disc handles and keeps no `Vector3[]` field | Alternative rejected: retaining a private managed array just because it was editor-only | Estimate: editor-only, removes final private array ownership from the tuner facade
- [ ] Loop 9/10 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 99.6% with Unity `dotnet.exe` PID 5468 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 10 - Hot Vault Setup Audit

- [x] Hot `EnsureVaultState` reacquire path collapsed | DOD practice: initialized runtime now fast-paths on stamped generation handles instead of calling `OpenOrAcquireVaultBuffer` for 14 lanes before every FrostTick | Alternative rejected: treating Boot allocation/acquire logic as a per-tick guard | Estimate: removes 14 Vault open/acquire probes from normal FrostTick preflight
- [x] Invalid-handle recovery kept explicit | DOD practice: if scheduled job buffer opens fail after locking, runtime marks `_initialized=false` so the next tick re-enters cold reacquire instead of spinning forever on stale handles | Alternative rejected: silent no-op forever after a stale generation handle | Estimate: fault path only

## Loop 11 - Vault Write Lock Matrix

- [x] Grid header lane added to lock/unlock matrix | DOD practice: `ShinobuNutrientDriftGridHeader` is now locked with the other job/proof lanes and unlocked symmetrically after fence finalization | Alternative rejected: writing the proof artifact outside the owner lock set | Estimate: one extra lock/unlock around the scheduled solve, not per-cell
- [ ] Loop 11 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 63.8% with Unity `dotnet.exe` PID 5468 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 12 - Blackbox Ring Cursor Closure

- [x] Telemetry cursor made bounded modulo | DOD practice: `_telemetryCursor` and the Vault cursor row now stay in `0..299`; telemetry slot and next cursor derive from the physical ring capacity | Alternative rejected: unbounded `int` cursor relying on effectively unreachable overflow | Estimate: O(1), no per-cell cost
- [ ] Loop 12 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 94.8% with Unity `dotnet.exe` PID 1548 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 13 - Unity Asset Metadata Hygiene

- [x] New script `.meta` files normalized | DOD practice: runtime, tuner, and scanner script metas now include the standard `MonoImporter` block with stable GUIDs | Alternative rejected: leaving two-line metas for Unity to regenerate during import | Estimate: editor/import-only
- [ ] Loop 13 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with Unity `dotnet.exe` PID 1548 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 14 - AUP Visual Publish Correction

- [x] Shader origin cast localized | DOD practice: density texture origin now subtracts current runtime origin in double precision through `ResolveGridCenterLocal` before casting to `float3` for shader globals | Alternative rejected: publishing absolute `double3` AUP as three floats and injecting 100km jitter into the visual lie | Estimate: O(1) per texture publish
- [ ] Loop 14 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with Unity `dotnet.exe` PID 1548 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 15 - Source Injection Math LOD

- [x] Source falloff sqrt collapsed at low quality | DOD practice: `UpdateNutrientSourcesJob` maps `GlobalQualityWeight` through `smoothstep(0.35,0.90)`; low endpoint uses squared-distance falloff without `sqrt`, middle blends squared/exact radial, high endpoint keeps precise radial shape | Alternative rejected: paying a square root per source/cell on thermally constrained hardware | Estimate: low endpoint saves up to 16 `sqrt` ops per active cell
- [ ] Loop 15 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with Unity `dotnet.exe` PIDs 3056 and 16936 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 16 - Editor Assembly Isolation

- [x] Roslyn scanner isolated from parent editor assembly | DOD practice: added local editor-only asmdef with Roslyn precompiled references for `NutrientDriftParticleScanner`, leaving broad `Hecton8.Editor` asmdef untouched | Alternative rejected: adding Roslyn to the global editor asmdef and expanding compile-wall blast radius | Estimate: editor compile isolation, runtime 0 us
- [ ] Loop 16 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with Unity `dotnet.exe` PIDs 6528 and 15572 active | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 17 - Mock Flow Math LOD Closure

- [x] Mock flow radial falloff sqrt collapsed at low quality | DOD practice: `GenerateMockFlowFieldJob` maps `GlobalQualityWeight` through `smoothstep(0.30,0.90)`; low endpoint uses squared-radius falloff with no `sqrt`, middle blends squared/exact radial falloff, high endpoint keeps exact radial shape | Alternative rejected: paying precise radial distance in emergency fallback on weak hardware | Estimate: low endpoint saves one `sqrt` per active cell in mock-flow mode
- [ ] Loop 17 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with no active compiler process, so CPU rule still blocks build | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 18 - Contract Route Decoupling

- [x] Thermal vent source route moved behind a core read-model interface | DOD practice: added `NutrientThermalVentSnapshotDTO=80` and `INutrientThermalVentReadModel`; `PersistentWorldRegistry` implements the interface, while `NutrientDriftRuntime` caches only the interface | Alternative rejected: retaining concrete World owner fields in the nutrient runtime | Estimate: runtime cost unchanged, compile-wall blast radius reduced
- [x] Abyssal flow volume route moved behind a core read-model interface | DOD practice: added `IAbyssalFlowVolumeReadModel`; `HectonMapMagicVegetationBridge` implements it through the existing read-only flow payload method, while nutrient drift caches only the interface | Alternative rejected: direct `HectonMapMagicVegetationBridge` field/cast in the ecosystem owner | Estimate: runtime cost unchanged, direct sibling type dependency removed from the route owner
- [ ] Loop 18 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with active `csc.exe` PID 7748 and `dotnet.exe` PID 16748 | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 19 - Evidence Consistency Pass

- [x] Status/rationale route wording normalized | DOD practice: older task evidence now names the cached Core read-model interfaces as the runtime dependency and treats concrete World classes only as implementing owners | Alternative rejected: leaving stale proof text that contradicts Loop 18 source decoupling | Estimate: docs/tool-only
- [ ] Loop 19 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with active `dotnet.exe` PID 12344 | Alternative rejected: launching under current shared-agent load | Estimate: tool-only

## Loop 20 - Source Hygiene Audit

- [x] Hot-path allocation/concrete-owner scan rerun | DOD practice: targeted `rg` scan over nutrient runtime/carrion/editor routes found no runtime native allocations, LINQ, particle/Rigidbody authority, DTO auto-properties, or concrete World owner fields; only cold documented `.Complete()` sync points remain | Alternative rejected: changing source without a current defect | Estimate: static/tool-only
- [x] Burst/aliasing audit rerun | DOD practice: all 8 nutrient runtime Burst jobs use deterministic synchronous standard precision; `[NoAlias]` appears on pointer/native lanes; carrion partial also carries deterministic Burst attributes | Alternative rejected: assuming attributes from memory after compaction | Estimate: static/tool-only
- [ ] Loop 20 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 26% with active `csc.exe` PID 15232 and `dotnet.exe` PID 10876 | Alternative rejected: launching under active compiler processes | Estimate: tool-only

## Loop 21 - Self-Audit And Report Restoration

- [x] Runtime self-audit expanded | DOD practice: cold `BuildSelfAuditXml()` now emits the requested 20-task reconciliation, struct layout math, continuous scalability curve, Vault/H-Phi ownership, NoAlias/dependency graph, compile guard, Dear Lie complexity, zero-GC static proof, and netcode exclusion | Alternative rejected: retaining the prior short attribute-only XML | Estimate: cold audit only, runtime hot path 0 us
- [x] Shared report section restored after external overwrite | DOD practice: re-added the SHINOBU_309 object to `RENDERING_OPTIMIZATION_REPORT.json` while preserving currently present SHINOBU_326/325 report objects | Alternative rejected: overwriting the shared report from this agent | Estimate: tool/doc-only
- [ ] Loop 21 guarded Core compile | DOD practice: build may run only when CPU <=50% and no `dotnet`/`csc`/`VBCSCompiler` process is active; latest gate sampled CPU 100% with active Unity `dotnet.exe` PID 16552 | Alternative rejected: launching under saturated CPU and active compiler processes | Estimate: tool-only
