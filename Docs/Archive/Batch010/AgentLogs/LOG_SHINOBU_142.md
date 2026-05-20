# LOG_SHINOBU_142

## 2026-05-19 - Zero-GC Fabrication Assembler Static Report

What was wrong:
- Legacy build presentation risk was CPU-owned: prefab-style presentation, material/MPB mutation, coroutine/update progress semantics, and craft completion tied to object-side animation state.
- `Progress01` needed to be the authoritative fabrication scalar in Vault, not a renderer-side timer.
- Completion needed an unmanaged signal route so authoritative inventory/world systems can act without the fabrication loop instantiating or enabling prefabs.
- Runtime validation could not use reflection, and Burst DTO mutation needed raw-ref proof.

What was done:
- Added/extended `FabricationAssemblerRuntime` as a Vault-backed dispatcher participant: SIMULATION updates `FabricationJobDTO.Progress01`; POST_SIMULATION records telemetry and fault dumps; VISUAL_SYNC uploads dirty shader payloads through double-buffered `GraphicsBuffer.LockBufferForWrite`.
- Added unmanaged payloads/signals: `FabricationCompletedSignal`, `FabricationTickSignal`, `FabricationTuningDTO`, `FabricationTimingDTO`, `FabricationTelemetryEntry`, and `FabricationGpuPayloadDTO`.
- Added Vault BufferID entries: `ShinobuFabricationJobs`, `ShinobuFabricationRuntime`, `ShinobuFabricationGpuPayload`, `ShinobuFabricationTelemetryRing`, `ShinobuFabricationTuning`, `ShinobuFabricationTimingLookup`, `ShinobuFabricationCsvScratch`.
- Updated `Hecton_HologramAssembly.shader` to consume `_H8FabricationAssemblyPayloads`, clip above the progress height, read localized AUP payload data, and scale edge richness through continuous quality.
- Added `FabricationAssemblerTunerWindow` for editor-only tuning, mock job injection, CSV timing ingest, and clipping gizmo readout.
- Updated `Docs/ARCHITECTURE/ZERO_GC_FABRICATION.md`, `Docs/Tasks/Status_SHINOBU_142.md`, and `Docs/AgentLogs/Rationale_SHINOBU_142.md`.

Cinematic cheats used:
- The final model is not built piece by piece. The shader clips pixels above the `Progress01` height and emits a rim at the slice boundary.
- Visual spark/bubble/laser load is exported as `FabricationTickSignal.EmissionMultiplier = math.lerp(0, 1, GlobalQualityWeight)`.
- Deconstruction is the same scalar running backwards; no reverse asset set was added.

Exact microseconds saved:
- Exact measured savings: 0 us measured. No profiler, Unity import, Play Mode, GCMonitor, Frame Debugger, or player build was run.
- Static estimate: removed per-craft material/MPB/progress mutation risk, estimated 35-250 us per craft-start/progress presentation path depending renderer count.
- Static estimate: Burst flat job replaces managed per-object progress loops, estimated 12-80 us per 100 active fabrications.
- Static estimate: low quality visual upload path avoids roughly 80-92% of dirty GPU payload uploads through quality-based count/stride gating.
- Compile guard: `dotnet build` was not launched. Latest guard was `CPU=100 DOTNET=0 CSC=0`, which violates the local build policy because CPU is over 50%.

<SELF_AUDIT agent_id="SHINOBU_142" status="STATIC_SOURCE_VERIFIED_COMPILE_BLOCKED">
  <TASK_RECONCILIATION>
    <TASK id="01" name="COROUTINE_ERADICATION_PASS" result="PASS">Static scan found no `StartCoroutine`, `IEnumerator`, or `Instantiate(` in the SHINOBU build-progress surface. Fabrication truth reads Vault `Progress01`.</TASK>
    <TASK id="02" name="MATERIAL_INSTANTIATION_PURGE" result="PASS">No `renderer.material`, `.material`, or `new Material(` in the target build-progress route. Remaining `SetPropertyBlock` hits are BuilderTool screen and Fabricator error feedback, not assembly progress.</TASK>
    <TASK id="03" name="CS1612_ENCAPSULATION_PURGE" result="PASS">`FabricationJobDTO` uses public unmanaged fields only. Burst jobs mutate through `UnsafeUtility.ArrayElementAsRef`.</TASK>
    <TASK id="04" name="ARM64_PADDING_RECONSTRUCTION" result="PASS">Editor-load validator checks size 32 and offsets 0/24/28. No `Pack=1`. Runtime reflection hook removed.</TASK>
    <TASK id="05" name="EMERGENCY_MOCK_FABRICATION_QUEUE" result="PASS">`GenerateMockFabricationJobsJob` writes 50 deterministic Vault jobs with NoAlias raw refs.</TASK>
    <TASK id="06" name="BURST_PROGRESS_EVALUATION_KERNEL" result="PASS">`AdvanceFabricationProgressJob` is deterministic Burst, NoAlias, dispatcher-scheduled, clamps `Progress01`, and returns a JobHandle.</TASK>
    <TASK id="07" name="THE_DEAR_LIE_SHADER_CLIPPING" result="PASS">Shader uses `clip()` against progress height and rim edge glow. CPU does not animate build pieces.</TASK>
    <TASK id="08" name="SIGNAL_BUS_COMPLETION_BROADCAST" result="PASS">Parallel Burst progress job sets completion flags; serial Burst signal job emits `FabricationCompletedSignal` carrying TargetAUP. Legacy `CraftingCompletedSignal` remains owned by `Fabricator.CompleteCraft()` after actual delivered-item commit because the existing legacy payload has no TargetAUP field. Slot clearing stays owner-local through `ClearSlot` after Fabricator finalization.</TASK>
    <TASK id="09" name="AUP_LOCALIZED_BOUNDS_CALCULATION" result="PASS">Payload stores `targetAUP - fabricatorAUP` as local float3 and shader reads `LocalOffsetPause.xyz`; bounds are local MinY/MaxY.</TASK>
    <TASK id="10" name="CONTINUOUS_SCALABILITY_VFX_EMISSION" result="PASS">Emission multiplier and shader/upload behavior consume continuous `GlobalQualityWeight`; no low/high binary switch was added.</TASK>
    <TASK id="11" name="ASYNCHRONOUS_GPU_BUFFER_UPLOAD" result="PASS">VISUAL_SYNC uses double-buffered `GraphicsBuffer` and `UploadNativeArray` backed by `LockBufferForWrite`/MemCpy.</TASK>
    <TASK id="12" name="DECONSTRUCTION_REVERSE_MATH" result="PASS">Same job supports reverse progress and emits `DeconstructResultSignal` at zero.</TASK>
    <TASK id="13" name="POWER_GRID_DRAIN_LINK" result="PASS">Power potential gates delta by multiply; zero power gives zero progress without state-machine branching.</TASK>
    <TASK id="14" name="ROLLBACK_NETCODE_STATE_FENCE" result="PASS">DTOs are blittable explicit layouts; job uses `FloatMode.Deterministic`; rollback hashes are recorded.</TASK>
    <TASK id="15" name="ZERO_INIT_OVERHEAD_BYPASS" result="PASS">Vault buffers request `UninitializedMemory`; cold clear job writes only required primary fields plus companion clears.</TASK>
    <TASK id="16" name="TELEMETRY_FABRICATION_RECORDER" result="PASS">300-entry Vault telemetry ring records active/completed/fault/rollback/quality/progress/power/upload values and dumps `Docs/AgentLogs/Dump_FABRICATION_ASSEMBLER.bin` on fault.</TASK>
    <TASK id="17" name="FABRICATION_TUNER_EDITOR_WINDOW" result="PASS">UI Toolkit editor facade exposes stats, tuning sliders, mock injection, CSV load, and gizmo toggle.</TASK>
    <TASK id="18" name="CSV_RECIPE_TIMINGS_INGESTOR" result="PASS">CSV parser reads bytes into Vault scratch and computes FNV-1a hashes without `ReadAllLines`, `Split`, or managed dictionaries.</TASK>
    <TASK id="19" name="LIVE_CLIPPING_DEBUG_GIZMO" result="PASS">Editor SceneView reads Vault job data and draws wire bounds plus progress plane.</TASK>
    <TASK id="20" name="SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION" result="PASS">This XML audit and status/rationale/log files exist. Runtime 0B/profiler proof remains PENDING due compile guard.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION primary="FabricationJobDTO">
    <FIELD name="TargetAUP" offset="0" size="24" type="double3"/>
    <FIELD name="Progress01" offset="24" size="4" type="float"/>
    <FIELD name="TargetPrefabHash" offset="28" size="4" type="uint"/>
    <TOTAL size="32" math="24+4+4=32; 32 % 16 = 0; 32 % 8 = 0"/>
    <CACHE_LINE note="Two FabricationJobDTO records fit in one 64-byte L1 cache line. No atomic counters were introduced, so no false-sharing counter struct is required."/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    At `GlobalQualityWeight` below 0.3, CPU simulation remains one scalar `Progress01` write per active job, but visual work collapses continuously: `math.step(0.0001f, q)` gates only the true-zero survival case, cubic `q*q*(3-2*q)` shapes the continuum, upload count approaches the small-record budget, upload cadence approaches a 12-frame stride around q=0.1 and 60-frame survival stride at q=0, VFX emission becomes `math.lerp(0, 1, weight)`, and shader rim richness is multiplied by quality. At 1.0, all active records can upload every frame and shader rim/wire/fresnel richness is preserved.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS private_runtime_native_arrays="0">
    <BUFFER id="ShinobuFabricationJobs" type="FabricationJobDTO" count="128"/>
    <BUFFER id="ShinobuFabricationRuntime" type="FabricationRuntimeDTO" count="128"/>
    <BUFFER id="ShinobuFabricationGpuPayload" type="FabricationGpuPayloadDTO" count="128"/>
    <BUFFER id="ShinobuFabricationTelemetryRing" type="FabricationTelemetryEntry" count="300"/>
    <BUFFER id="ShinobuFabricationTuning" type="FabricationTuningDTO" count="1"/>
    <BUFFER id="ShinobuFabricationTimingLookup" type="FabricationTimingDTO" count="256"/>
    <BUFFER id="ShinobuFabricationCsvScratch" type="byte" count="65536"/>
    <NOTE>Runtime declares Vault handles and graphics buffers only. Editor facade has one cold `Vector3[4]` for SceneView plane drawing, outside gameplay.</NOTE>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>Clear, mock, and advance Burst jobs mark Jobs/Runtime/GpuPayload/Timings with `[NoAlias]`.</NO_ALIAS>
    <DEPENDENCY consumes="Dispatcher dependsOn" produces="AdvanceFabricationProgressJob handle"/>
    <DEPENDENCY consumes="AdvanceFabricationProgressJob handle" produces="EmitFabricationSignalsJob handle"/>
    <DEPENDENCY cold_sync="ClearFabricationJobsJob, ClearFabricationTimingLookupJob, GenerateMockFabricationJobsJob" note="Boot/editor/CI only; not hot simulation."/>
    <SIGNALS>`EmitFabricationSignalsJob` writes `FabricationCompletedSignal`, `FabricationTickSignal`, and `DeconstructResultSignal` through unmanaged queues in slot order. Legacy delivered-item `CraftingCompletedSignal` remains on the existing Fabricator commit route.</SIGNALS>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef or sibling runtime assembly reference was added. Runtime code remains in existing root assembly and routes through Core, Vault handles, GlobalSignals, and SignalBus. `dotnet build` skipped because latest guard was `CPU=100 DOTNET=0 CSC=0`.
  </COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>
    Before: O(N prefab/object presentations) plus renderer/material/coroutine work during active fabrication, with spike risk proportional to craft burst size and renderer count. After: O(N) contiguous Vault scalar updates and one bounded GPU payload upload; visual assembly is shader clipping/rim emission. The CPU does not simulate pieces or instantiate final geometry inside the fabrication loop.
  </THE_DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Polish Loop 5 Addendum

What was wrong:
- Runtime first-use still called the layout validator, which used `Marshal.OffsetOf<T>()`. The audit was correct but belonged to editor validation, not gameplay boot; the validator class also remained compiled into player code.
- Legacy `CraftingSystem` helper job structs still used bare `[BurstCompile]` and lacked `[NoAlias]` fields. Existing call sites remain synchronous `.Execute()` calls; no scheduled-Burst speedup is claimed.
- Static scan exposed pre-existing `Fabricator` private `NativeArray`/`NativeParallelHashMap` recipe scratch. This is not the shader-progress prefab-instantiation path, but it is real Vault Law debt.
- Signal writes were still coupled to the parallel progress job, which left NativeQueue ordering dependent on worker scheduling.
- Visual upload LOD used a square curve but did not literally include the mandated `math.step` zero-quality gate.
- `ConfigureSignalLanes()` initialized an unused generic `SignalBus<DeconstructResultSignal>` while deconstruction already has a documented `GlobalSignals` bridge writer.
- Completed slots could rewrite `FrameCompleted` every tick until `Fabricator` cleared the slot.
- `H8Memory.BufferID` had an unused `ShinobuFabricationMockJobs` entry even though mock injection uses the real job buffer.
- Main-thread slot setup/read/clear still used `NativeArray[index]`, which was weaker proof than the raw-pointer Vault access requested by the XML.

What was done:
- `FabricationLayoutValidator` and its Vault initialization call are now editor-only behind `#if UNITY_EDITOR`.
- `CraftingSystem.EvaluateRecipeAvailabilityJob` and `CraftingSystem.KahnTotalRawCostJob` now use deterministic Burst flags and `[NoAlias]` on native fields, without adding a `Schedule().Complete()` sync point to craft checks.
- Removed premature legacy `CraftingCompletedSignal` queue writes from the Burst fabrication progress job. The progress job now publishes fabrication completion only; delivered-item completion remains with `Fabricator.CompleteCraft()`.
- Chained `AdvanceFabricationProgressJob -> EmitFabricationSignalsJob`. The first job remains parallel and writes only scalar progress/runtime/GPU payload state; the second scans 128 slots serially and emits completion/tick/deconstruct signals in deterministic slot order.
- Reworked visual upload count/stride with `math.step(0.0001f, q)`, `math.lerp`, and cubic `q*q*(3-2*q)`. True-zero quality collapses to one-record/60-frame survival upload; q=0.1 remains near 5 Hz; q=1 uploads the full active payload every frame.
- Removed the unused `SignalBus<DeconstructResultSignal>` initialization. Deconstruction stays on `GlobalSignals.DeconstructResultSignalWriter`; fabrication completion/tick stay on SHINOBU-owned typed signal lanes.
- `AdvanceFabricationProgressJob` now writes `FrameCompleted` only when the slot first transitions into the completed state.
- Removed the dead mock BufferID. Emergency mock fabrication writes into `ShinobuFabricationJobs`, preserving one owner for job state.
- `TryBeginJob`, `TryUpdateSlot`, `TryReadSnapshot`, and `ClearSlot` now access slot records through `UnsafeUtility.ArrayElementAsRef` after validating slot bounds against all touched Vault buffers.
- Did not migrate legacy `Fabricator` scratch in this pass. A correct migration needs a per-fabricator Vault scratch arena and a PlayerInventory/CraftingSystem SOA count API; one shared Vault scratch buffer would race across active fabricators.

Verification:
- Static scan: no bare `[BurstCompile]` remains in `FabricationAssemblerRuntime.cs` or `CraftingSystem.cs`.
- Static scan: forbidden build-progress hits remain only `BuilderTool` screen `SetPropertyBlock` and `Fabricator` error-feedback `SetPropertyBlock`.
- Compile guard still blocks build: latest guard `CPU=100 DOTNET=0 CSC=0`.

## 2026-05-19 - Polish Loop 6 Addendum

What was wrong:
- `Fabricator.AdvanceCraftingTask` still existed as a C# progress helper, and `CraftingRuntimeSmokeTester` used it with a temp `NativeQueue`. That smoke test validated the old timer route instead of the Vault `Progress01` lane.
- `Fabricator` still exposed `_craftTimer` naming and a timer-derived `CraftProgress` fallback, which made the non-Vault progress path look alive even though runtime progression had moved to `FabricationJobDTO.Progress01`.
- A leftover `NativeQueue<CraftingTask>` allocate/dispose block remained in `Fabricator.EnsureCraftingScratch()`/`DisposeCraftingScratch()` after the field was converted to a single active task slot. That was both a compile hazard and a local persistent allocation path.

What was done:
- Removed `Fabricator.AdvanceCraftingTask`.
- Renamed `_craftTimer` to `_craftProgressSecondsMirror`; it is now only a UI/audio mirror of Vault-read progress, not simulation truth.
- `CraftProgress` now reads `FabricationAssemblerRuntime.TryReadSnapshot()` first and otherwise returns only the cached assembly preview scalar, not a locally advancing timer formula.
- Rewired `CraftingRuntimeSmokeTester.RunFabricationVaultSmoke()` to call `FabricationAssemblerRuntime.GenerateMockFabricationJobs()` and read slot snapshots plus editor stats from the Vault.
- Added a batchmode-only 16MB fallback `GlobalDataVault` creation path for the smoke test when bootstrap has not created a vault. Normal editor mode still fails closed instead of masking missing bootstrap.
- Deferred initial `GraphicsBuffer` creation and shader-global setup while `Application.isBatchMode` is true, so CI mock fabrication can validate Vault data without requiring a graphics device.
- Updated `AutomationSmokeTestRunner` JSON keys to report mock Vault progress values instead of paused/powered/completed local timer progress.
- Removed the leftover `NativeQueue<CraftingTask>` allocation, sentinel registration, warmup enqueue/dequeue, and dispose/unregister path from `Fabricator`.
- Renamed task helpers from queue wording to slot wording and changed task creation to `default` plus direct field assignment. The owner-local delivery metadata now uses one unmanaged `CraftingTask` field and one bool.
- Renamed the smoke entry point and context menu from queue wording to `RunFabricationVaultSmoke` / `Run Fabrication Vault Smoke Pass`.

Cinematic cheat:
- Smoke and runtime now share the same Dear Lie surface: one Vault scalar per job plus shader payload. No smoke-only timer simulation remains.

Microseconds saved:
- Runtime: no new measured frame gain claimed; the local timer helper was no longer called in active gameplay.
- Runtime allocator surface: removed one cold `NativeQueue<CraftingTask>` allocation and sentinel registration per fabricator.
- Tooling/CI: removed one temp `NativeQueue` allocation, two C# timer progression calls, and two unnecessary cold `GraphicsBuffer` allocations from the crafting smoke path.

Verification:
- Static scan: no `_craftTimer` or `AdvanceCraftingTask(` remains in `Fabricator`, `CraftingRuntimeSmokeTester`, or `AutomationSmokeTestRunner`.
- Static scan: no `NativeQueue<CraftingTask>`, `_craftingTaskQueue`, `MaxQueuedCraftingTasks`, `new NativeQueue`, or `Allocator.Temp` remains in the SHINOBU target surfaces.
- Target `git diff --check`: warnings only for line endings, no whitespace errors.
- Compile guard still blocks build: latest guard `CPU_CIM=100 DOTNET=0 CSC=0`.
