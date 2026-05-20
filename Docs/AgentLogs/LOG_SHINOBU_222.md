# LOG_SHINOBU_222

## 2026-05-20 - Sump Pump CSR Drainage Solver

What was wrong:
- Pump evacuation was split across object-authoritative paths: `FluidPipeGraphRuntime.ApplyPumpInputs` drained `BaseModule` water through `WaterPumpModule`, and `HabitatGraphManager.ApplyWaterPumpDrainage` traversed connected rooms through a managed graph helper.
- That design allowed duplicate water authority, recursive/BFS room drain behavior, scene object coupling, and non-Vault state mutation.
- Pipe flow visuals had no dedicated scalar payload for shader-driven fake flow from a drainage solver.

What was done:
- Added `PumpNodeDTO`, `PipeEdgeDTO`, `PipeProfileDTO`, `DrainageTuningDTO`, `DrainageTelemetryEntry`, and `DrainagePipeFlowGpuDTO` in `SumpPumpPipeGridContracts.cs`.
- Added owner-local Vault buffer IDs through `SumpPumpDrainageBufferIds` for pump, pipe, CSR, pressure, telemetry, tuning, CSV, frame summary, and GPU flow lanes.
- Added `DrainageMockNetworkJob` for deterministic 1000-node/2500-edge test topology.
- Added `BuildCsrPipeGraphJob` to convert flat pipe edges into CSR offsets, destinations, conductance, flow, and flat-edge index arrays.
- Added `PipePressureSolverJob` with deterministic Burst Jacobi relaxation and double-buffered pressure arrays.
- Added `PipeEdgeFlowJob` to calculate edge flow and write GPU-facing flow DTOs.
- Added `EvacuateWaterVolumeJob` to drain Fluid Incursion Vault buffers through quantized CAS updates, with per-pump remainder.
- Added `DrainageTelemetryRecorderJob` and a fixed 300-frame telemetry ring; non-finite output path dumps `Docs/AgentLogs/Dump_SHINOBU_222.bin`.
- Added `SumpPumpPipeGridRuntime` as the cold Vault/bootstrap adapter and dispatcher-owned scheduling facade.
- Retired object pump drain bodies in `FluidPipeGraphRuntime` and `HabitatGraphManager`; deleted the old connected-room pump drain helper.
- Added `Base Drainage Tuner` UI Toolkit window and `OnDrawGizmos` pressure/flow debug visualization.

Cinematic Cheats used:
- Dear Lie pipe flow: CPU writes only scalar edge flow. GPU shader/material systems can pan refractive normal maps from `_H8DrainagePipeEdgeFlow`; no water particles or CPU liquid meshes.
- AUP gravity cheat: downhill conductance uses one double-precision delta and a gravity dot product during CSR rebuild, not physical water simulation.
- Continuous quality cheat: `GlobalQualityWeight` maps solver steps from 1 to 8, trading pressure convergence for frame time without binary low-tier/high-tier branches.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. Compile/profiler run was blocked by the explicit CPU gate: CPU sampled at 100%, no `csc.exe`/`dotnet` active, protocol forbids launching `dotnet build` above 50%.
- Static expected savings: object pump traversal and recursive connected-room drain path removed from pump authority; Broadphase/collider pipe flow cost is zero because connectivity is CSR edge data.
- Solver wall time instrumentation exists in `DrainageTelemetryEntry.SolverWallMicroseconds`; exact values will be available after the first permitted runtime/profile pass.

Verification:
- Prompt re-read from `Docs/Tasks/CURRENT_BATCH.md` lines 1706-1769.
- Legacy recursive pump helper search: no `DrainConnectedFloodComponent` or `CanGraphFluidTraverseEdge` remains in `HabitatGraphManager`.
- Bad-pattern scan on new drainage files: no `new List<>`, LINQ, `GameObject.Find`, `GetComponentsInChildren`, `ParticleSystem`, or `WaterParticle` hits.
- Build: BLOCKED BY CPU GATE, not executed.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" />
  <DTO name="PumpNodeDTO" sizeBytes="32">
    <Field name="NodeHash" offset="0" type="uint" />
    <Field name="IngressRate" offset="4" type="float" />
    <Field name="MaxPumpRate" offset="8" type="float" />
    <Field name="CurrentEvacuationRate" offset="12" type="float" />
    <Field name="Flags" offset="16" type="uint" />
    <Field name="PowerDraw" offset="20" type="float" />
    <Field name="_pad0.._pad7" offset="24..31" type="byte" />
  </DTO>
  <DTO name="PipeEdgeDTO" sizeBytes="64" fields="SourceNodeIndex,DestinationNodeIndex,Conductance,CurrentFlow,Flags,PowerPotential,FractionalRemainderM3,DownhillScalar,EdgeHash,SourceNodeHash,DestinationNodeHash,Reserved" />
  <DTO name="DrainageTelemetryEntry" sizeBytes="64" ringEntries="300" />
  <VaultBuffers ids="95820-95842" authority="GlobalDataVault" owner="SumpPumpDrainageBufferIds" />
  <ZeroGC hotPath="true" managedCollectionsInJobs="false" linqInJobs="false" recursiveDrain="false" />
  <AUP precision="double3 source-destination before float3 cast" />
  <Scalability curve="iterations = clamp((int)lerp(1,8,GlobalQualityWeight),1,8)" />
  <MassConservation quantum="MassQuantumM3" remainder="PumpRemainderM3" atomicApply="Interlocked.CompareExchange float-bit CAS" />
  <Compile status="BLOCKED_BY_CPU_GATE" cpuPercent="100" compilerProcesses="0" />
</SELF_AUDIT>

## 2026-05-20 - Power Authority And Quantization Fatalism Pass

What was wrong:
- Missing, locked, empty, or undersized Logistics Power Vault rows previously filled drainage `PowerPotential` with `1.0`. That allowed pumps to run at full mathematical power when the Vault route had not proved power availability.
- `EvacuateWaterVolumeJob` converted requested drain units to `int` after `floor(requested / quantum)` without a clamp. A corrupted pump rate or remainder could overflow the cast before the room lock and conservation write.

What was done:
- Changed missing/invalid Logistics pressure fallback to write `0.0` power potential and keep `MissingPowerVault` telemetry. Pump evacuation now halts when power authority is absent instead of inventing power.
- Added `MaxQuantizedDrainUnitsPerPump = 1 << 24` and clamp/finite checks before the quantized unit cast.
- Non-finite requested volume now flags the pump and clears remainder; absurd clipped requests lose poisoned remainder instead of carrying it forward.
- Repatched the mock topology route to `job.Run()` after the hardening scan caught `job.Execute()` again at the same call site.

Cinematic Cheats used:
- No change to visual authority. CPU still solves scalar CSR pressure/flow; shader flow scalars carry pipe-water motion. No water particles, colliders, or CPU liquid mesh entered the path.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This pass prevents invalid math and synthetic power, not a claimed speedup.
- Expected low-end cost is one finite check plus one clamp per active pump, lower than propagating a poisoned value into telemetry and Fluid buffers.

Verification:
- Power fallback scan: no `powerPotential[i] = 1f` fallback remains in `HydratePowerPotentialFromVault`.
- Quantization scan: `MaxQuantizedDrainUnitsPerPump` is applied before integer cast in `EvacuateWaterVolumeJob`.
- Forbidden-pattern scan: PASS for no direct `.Execute()`, `.Complete()`, stale Vault handle APIs, `Interlocked.Add`, `NativeDisableParallelForRestriction`, `Time.deltaTime`, LINQ, `foreach`, or `Pack=1` in SHINOBU_222 runtime/jobs/contracts/editor files.
- Build: BLOCKED BY CPU GATE. Latest sample remains 100% CPU; no build launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" />
  <PowerAuthority missingVaultFallback="0.0" syntheticFullPowerFallback="removed" telemetryFlag="MissingPowerVault" />
  <Quantization maxUnitsPerPump="16777216" castClampedBeforeInt="true" nonFiniteRequest="flag pump and clear remainder" />
  <Conservation frontBackMutation="one locked identical delta" physicalWaterParticles="0" />
</SELF_AUDIT>

## 2026-05-20 - Static Regression Correction: Mock Job Route

What was wrong:
- The post-conservation forbidden-pattern scan found `DrainageMockNetworkJob` still invoked as `job.Execute()` in `SumpPumpPipeGridRuntime.cs`.
- That contradicted the job-route rule and the existing rationale note that mock topology generation must use the Unity job extension path.

What was done:
- Patched the cold mock topology path to invoke `job.Run()`.
- Reran the forbidden-pattern scan across SHINOBU_222 runtime/jobs/contracts/editor files. The scan returned clean for direct `.Execute()`, `.Complete()`, `JobHandle.Complete`, stale Vault handle APIs, `Interlocked.Add`, `NativeDisableParallelForRestriction`, `Time.deltaTime`, LINQ, `foreach`, and `Pack=1`.

Cinematic Cheats used:
- No change. Mock topology remains deterministic data injection; runtime water remains scalar CSR math plus shader flow scalars.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This is a route/protocol correction, not a performance claim.

Verification:
- Forbidden-pattern scan: PASS after the `job.Run()` patch.
- `git diff --check`: no whitespace errors in targeted files; CRLF normalization warning remains on the ledger file.
- Build: BLOCKED BY CPU GATE. One sample reported active `dotnet`/`csc`; the final sample reported no active compiler processes but still 100% CPU, so `dotnet build` was not launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" />
  <JobRoute directExecute="0" mockRoute="DrainageMockNetworkJob.Run()" blockingComplete="0" />
  <BuildGate cpu="100%" activeCompilerProcesses="0 in final sample" buildLaunched="false" />
  <RegressionModel cpu="no runtime claim until build/profiler" gc="static zero-GC patterns still clean" correctness="mock job entry no longer bypasses job extension path" />
</SELF_AUDIT>

## 2026-05-20 - Conservation Lock Front Back Drain Pass

What was wrong:
- `EvacuateWaterVolumeJob` drained Fluid Incursion front/back rows through independent float-bit CAS paths. Under same-room contention or existing front/back drift, one row could accept a larger delta than the other.
- The old reducer reported the mismatch after the fact, but telemetry is not conservation. The solver needed to prevent SHINOBU_222 from creating additional duplicated-buffer drift.
- A local mirror of `FluidCompartmentDTO` was investigated and rejected. `GlobalDataVault.ComputeTypeHash<T>()` includes `typeof(T).TypeHandle`, so a mirror type would fail Vault validation against the Fluid-owned rows.

What was done:
- Added owner-local Vault lane `SumpPumpDrainageBufferIds.RoomDrainLocks = 95843`.
- Added `DrainageRoomDrainLock64`, an explicit 64-byte row with `LockState` at offset 0 and padding through offset 56, so concurrent room locks do not false-share cache lines.
- Added `ClearDrainageRoomLocksJob` before the evacuation pass, chained through the existing job dependency graph.
- Reworked `EvacuateWaterVolumeJob` to acquire a bounded 64-attempt per-room lock, sanitize both Fluid rows, compute one `actualDrained = min(frontWater, backWater, quantizedRequest)`, subtract that identical delta from front and back, then release the lock.
- Updated boot validation, owner-local Vault handle lifecycle, buffer locks/unlocks, scalar clear, status, rationale, and binary payload ledger for lane `95843`.

Cinematic Cheats used:
- No physical water entities were introduced. Pump drain authority remains scalar CSR math over Vault rows.
- Pipe flow visuals remain shader-driven from flow scalars; the CPU does no liquid mesh, particle, collider, or recursive room-balancing work.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. Build/profile proof is still controlled by the CPU gate.
- This pass is a correctness and contention-bound fix, not a claimed speedup. It replaces two independent CAS mutation paths with one bounded per-room lock only for active pump-room drains.
- Expected low-end impact: lower worst-case conservation retry ambiguity and one isolated 64-byte lock row per room, avoiding false sharing when pumps target adjacent room lock indices.

Verification:
- Static scan: no `VaultBufferHandle`, `GetBufferHandle`, `.Resolve(_vault)`, `TryGetBuffer(`, `Interlocked.Add`, `NativeDisableParallelForRestriction`, `Time.deltaTime`, `new List<>`, `foreach`, LINQ, `Pack=1`, `JobHandle.Complete`, `.Complete()`, direct `.Execute()`, or `AtomicDrainVolume` remains in SHINOBU_222 runtime/jobs/contracts/editor files.
- Static scan: `DrainageRoomDrainLock64`, `ClearDrainageRoomLocksJob`, `TryAcquireRoomLock`, bounded `Interlocked.CompareExchange`, and `Interlocked.Exchange` are present.
- Burst scan: seven SHINOBU_222 jobs now carry deterministic Burst attributes.
- `git diff --check`: no whitespace errors in targeted files; only CRLF normalization warnings from the ledger file.
- Build: pending CPU gate. No `dotnet build` was launched while the workstation reported blocked load in prior samples.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" task10="ABSENT_FROM_XML" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="Object/particle water pump authority remains retired; no physical water actors are reintroduced." />
    <Task id="02" status="PASS" proof="Pipe connectivity and sealed state remain CSR edge attributes, not physics constraints." />
    <Task id="03" status="PASS" proof="Pump/pipe/lock DTOs use raw public fields and explicit layout." />
    <Task id="04" status="PASS" proof="PumpNodeDTO validation remains explicit 32-byte offset proof." />
    <Task id="05" status="PASS" proof="Mock topology remains deterministic job-routed data injection." />
    <Task id="06" status="PASS" proof="CSR build remains bounded by real row capacity and per-source offset range." />
    <Task id="07" status="PASS" proof="Jacobi solver remains double-buffered deterministic Burst." />
    <Task id="08" status="PASS" proof="Fluid evacuation now subtracts one identical bounded volume from front/back rows under a per-room lock." />
    <Task id="09" status="PASS" proof="PowerPotential still gates pump rate through scalar multiplication." />
    <Task id="10" status="N/A" proof="No Task 10 exists in the SHINOBU_222 XML block." />
    <Task id="11" status="PASS" proof="Flow visual remains GPU scalar payload; no CPU liquid geometry." />
    <Task id="12" status="PASS" proof="Solver iterations remain continuous from `GlobalQualityWeight`." />
    <Task id="13" status="PASS" proof="Quantized request and per-pump remainder remain; the final applied delta is now front/back identical." />
    <Task id="14" status="PASS" proof="AUP local-delta gravity conductance remains the only downhill calculation." />
    <Task id="15" status="PASS" proof="Lock DTO and existing state rows are blittable, explicit, Vault-owned, and deterministic-job compatible." />
    <Task id="16" status="PASS" proof="Telemetry reduction records evacuated volume, active pumps, average pressure, mass error, and non-finite dump route." />
    <Task id="17" status="PASS" proof="Editor tuner remains cold/UI Toolkit only and writes Vault-backed tuning." />
    <Task id="18" status="PASS" proof="CSV profile ingest remains cold span parser into Vault profile rows." />
    <Task id="19" status="PASS" proof="Scene gizmo remains editor-only Vault visualization." />
    <Task id="20" status="PASS_STATIC_ONLY" proof="Static audit updated; compile/profiler proof still requires CPU-gated build/runtime artifacts." />
  </TaskReconciliation>
  <StructLayout name="DrainageRoomDrainLock64" sizeBytes="64" falseSharing="isolated-row">
    <Field name="LockState" offset="0" size="4" />
    <Field name="Reserved0" offset="4" size="4" />
    <Field name="Pad0" offset="8" size="8" />
    <Field name="Pad1" offset="16" size="8" />
    <Field name="Pad2" offset="24" size="8" />
    <Field name="Pad3" offset="32" size="8" />
    <Field name="Pad4" offset="40" size="8" />
    <Field name="Pad5" offset="48" size="8" />
    <Field name="Pad6" offset="56" size="8" />
    <Math>4 + 4 + (7 * 8) = 64 bytes, one cache line.</Math>
  </StructLayout>
  <Scalability curve="iterations = clamp((int)math.lerp(1,8,GlobalQualityWeight),1,8)" below03="1-3 Jacobi iterations; same conservation lock; shader scalar visuals only" binarySwitches="0" />
  <VaultBuffers ids="95820..95843" owner="SumpPumpDrainageBufferIds" persistentPrivateArrays="0" newLane="RoomDrainLocks" />
  <DependencyGraph consumed="dispatcher dependency, owner-local drainage rows, Fluid Incursion front/back rows, PowerPotential rows" produced="clear-locks -> pressure iterations -> evacuation -> telemetry JobHandle registered through H8Memory" blockingHotComplete="0" />
  <PointerAliasing noAlias="true" lockRows="DrainageRoomDrainLock64 per room" nonOverlappingBuffers="pump, edge, pressure, flow, remainder, mass-error, telemetry, room-lock rows are separate Vault lanes" />
  <CompileGuard newAsmdefRefs="0" centralEnumAdditions="0" note="The remaining Fluid DTO namespace dependency is not mirrored because Vault type hashes include the concrete runtime type handle." />
  <DearLie before="object/particle/recursive water O(scene objects + edges + hierarchy traversal)" after="CSR math O(edges + nodes * iterations), visual water O(edge scalar upload), no physical water actors" />
</SELF_AUDIT>

## 2026-05-20 - Boot Fail-Close Vault Handle Validation Pass

What was wrong:
- `SumpPumpPipeGridRuntime.TryResolveAndInitializeBuffers()` requested every SHINOBU_222 owner-local Vault descriptor but did not prove the complete set resolved before tuning initialization.
- Under Vault compaction/starvation/type drift, a partial descriptor set could defer failure until solver scheduling, increasing the chance of noisy unlock/retry behavior.

What was done:
- Added `ValidateOwnedBuffers()` to prove all 23 owner-local `VaultGenerationHandle<T>` descriptors resolve through `IDataVault.TryResolveHandle`.
- Added `HasResolvedBuffer<T>()` to reject default descriptors, unresolved handles, and buffers shorter than the required lane length.
- Wired the failure path to `ReleaseOwnedBuffers()` before returning false, so partial acquisition resets descriptors and does not let `_buffersReady` become true.

Cinematic Cheats used:
- No new physical water truth was added. The authority remains CSR/Jacobi scalar math plus GPU flow panning.
- Saved CPU remains reserved for shader-side pipe motion richness on higher `GlobalQualityWeight`, not per-particle or collider simulation.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This is a cold-path memory-safety patch; runtime profiler proof remains pending.
- Expected hot-path delta: no meaningful per-frame savings claimed. The value is fail-close behavior before scheduling jobs over invalid Vault rows.

Verification:
- Static scan: `ValidateOwnedBuffers` and `HasResolvedBuffer` are present, cover the owner-local lane set, and call `ReleaseOwnedBuffers()` on failure before tuning init.
- Static forbidden-pattern scan: no `VaultBufferHandle`, `GetBufferHandle`, direct `TryGetBuffer`, `.Resolve(_vault)`, `Interlocked.Add`, `foreach`, LINQ, `Time.deltaTime`, `Pack=1`, direct `.Execute()`, `JobHandle.Complete`, or `.Complete()` in SHINOBU_222 files.
- `git diff --check`: no whitespace errors; only the existing CRLF normalization warning in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Build: blocked by CPU gate. Latest `Win32_Processor.LoadPercentage` is 100%; no `dotnet`/`csc` compiler process is active.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" task10="ABSENT_FROM_XML" />
  <BootValidation status="PASS_STATIC_ONLY" lanes="23" failurePath="ReleaseOwnedBuffers then false" buffersReadyOnPartialFailure="false" />
  <VaultHandles type="VaultGenerationHandle" persistentPrivateArrays="0" directTryGetBuffer="0" />
  <StructLayout name="PumpNodeDTO" sizeBytes="32" unchanged="true" />
  <Scalability curve="iterations = clamp((int)math.lerp(1,8,GlobalQualityWeight),1,8)" binarySwitches="0" />
  <DearLie authority="CSR/Jacobi scalar flow" visual="GPU panning flow buffer" particles="0" colliders="0" />
  <RuntimeProof status="PENDING_VERIFICATION" reason="CPU gate 100 percent blocks dotnet build and Unity/profiler evidence" />
</SELF_AUDIT>

## 2026-05-20 - Polish Mandate Vault Fence And CSR Safety Pass

What was wrong:
- Solver scheduling resolved owner-local Vault views before acquiring the mutation/relocation locks.
- Shared Fluid Incursion and Logistics pressure reads used direct `TryGetBuffer`, creating external-view ambiguity instead of method-local generation-handle proof.
- CSR capacity trimming capped total valid edges but did not stop a single overfull source node from writing past its capped row into a later node's range.
- Fluid volume CAS used an unbounded loop.
- Runtime teardown unlocked and released GPU buffers, but did not release SHINOBU_222 owner-local Vault generation descriptors.

What was done:
- Moved scheduling to lock-before-resolve for all SHINOBU_222 owner-local buffers.
- Added `TryResolveLockedExistingBuffer<T>` for optional Fluid front/back rows: method-local `VaultGenerationHandle<T>`, lock, resolve, schedule, unlock after fence.
- Changed Logistics pressure hydration to method-local `VaultGenerationHandle<float>` with a short lock around the scalar copy.
- Added source-row CSR bound: `slot < NodeEdgeOffsets[source + 1]` in addition to the global valid-edge bound.
- Bounded `AtomicDrainVolume` to 64 CAS attempts and sanitized delta time/current evacuation rate before quantization.
- Added `ReleaseOwnedBuffers()` and `ResetHandles()` on teardown; tuning is copied to offline fallback before descriptor release.
- Registered the final scheduled telemetry-chain handle with `H8Memory.RegisterActiveJob(OwnerSystem, _solverHandle)` after schedule.

Cinematic Cheats used:
- No change to Dear Lie visual authority: the CPU still writes scalar flow rows only; shader/connection-spline visuals carry water motion.
- The safety pass protects the mathematical fake from corrupt data without adding physical liquid actors, colliders, or scene traversal.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. Build/profile proof remains blocked by CPU gate.
- Static expected delta: CSR overwrite prevention is correctness, not speed. CAS bound caps worst-case contention; normal uncontended pump drain remains one successful compare-exchange.

Verification:
- Static scan: no direct `TryGetBuffer`, `GetBufferHandle`, `VaultBufferHandle`, `.Resolve(_vault)`, `Interlocked.Add`, direct `.Execute()`, `JobHandle.Complete`, or `.Complete()` remains in SHINOBU_222 runtime/jobs/contracts/editor files.
- Static scan: `TryLockJobBuffers()` is called before owner-local generation handle resolves in scheduled solve and mock generation.
- Static scan: `ReleaseOwnedBuffers()` releases `95820..95842` descriptors and resets every persistent generation handle.
- Static scan: scheduled solver chain publishes the final owner fence through `H8Memory.RegisterActiveJob`.
- `git diff --check`: no whitespace errors in targeted SHINOBU_222 files; only CRLF normalization warnings where broader docs already have them.
- Build: not launched because CPU gate has not opened. Latest `Win32_Processor.LoadPercentage` sample was 100% with no `dotnet`/`csc` process.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="VaultFenceAndCsrSafety" />
  <VaultStatus ownerLocalIds="95820..95842" persistentNativeArrays="0" persistentVaultBufferHandles="0" teardownRelease="ReleaseOwnedBuffers" />
  <LockOrder ownerLocal="lock-before-resolve" sharedFluid="generation-handle lock resolve job unlock-after-fence" sharedPower="generation-handle lock copy unlock-before-schedule" />
  <CSR sourceRangeBound="slot < NodeEdgeOffsets[source + 1]" globalRangeBound="slot < validEdgeCount" />
  <CAS maxAttempts="64" fallback="0m3 drained on pathological contention" />
  <CompileGuard directTryGetBufferInDomain="0" directJobCompleteInDomain="0" />
</SELF_AUDIT>

## 2026-05-20 - Polish Mandate Compile-Wall Correction

What was wrong:
- Drainage BufferID ownership was collision-free but still too centralized: the previous repair added SHINOBU_222 lanes to `H8Memory.BufferID`, a shared core enum touched by many agents.
- `GenerateMockDrainageNetwork()` called `DrainageMockNetworkJob.Execute()` directly. This was a cold path, but it bypassed Unity job extensions and weakened the Burst/job-route proof.

What was done:
- Removed SHINOBU_222 drainage IDs from central `H8Memory.BufferID`; source scan now finds no `ShinobuDrainage*` IDs in `H8Memory.cs`.
- Declared `SumpPumpDrainageBufferIds` in the drainage contract as owner-local numeric `BufferID` casts `95820..95842`.
- Rewired all runtime Vault acquisitions, locks, CSV/tuning/mock writes, solver buffers, telemetry, frame summary, GPU flow, and mass-error lanes through `SumpPumpDrainageBufferIds`.
- Changed the synthetic topology generator from direct `job.Execute()` to `job.Run()`.
- Updated the binary payload ledger to state explicitly that these IDs are owner-local casts, not central enum additions.

Cinematic Cheats used:
- No change to authority model: pipe water remains scalar math plus GPU visual panning. No particles, colliders, rigidbody constraints, liquid meshes, or recursive room balancing were reintroduced.
- Mock topology remains a deterministic data injection job, not scene object construction.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. Build/profile proof is still gated by CPU load, so no fabricated measurements are recorded.
- Runtime delta from `Execute()` -> `Run()` is not claimed as a performance win; it is a route correction for Burst/job discipline.
- Compile-wall risk is reduced because drainage-owned ID edits no longer require touching `H8Memory.cs`.

Verification:
- Prompt extraction re-run from `Docs/Tasks/CURRENT_BATCH.md`; SHINOBU_222 task count remains 19 because Task 10 is absent.
- Static scan: no `BufferID.ShinobuDrainage` or `ShinobuDrainage` remains in SHINOBU_222 files or `H8Memory.cs`.
- Static scan: no direct `.Execute()`, `JobHandle.Complete`, or `.Complete()` remains in `SumpPumpPipeGridRuntime.cs` or `SumpPumpPipeGridJobs.cs`.
- Static scan: no `VaultBufferHandle`, `GetBufferHandle`, `.Resolve(_vault)`, `Interlocked.Add`, `NativeDisableParallelForRestriction`, `Time.deltaTime`, `new List<>`, `foreach`, LINQ, `Pack=1`, or `LastMassError` remains in SHINOBU_222 runtime/contracts/jobs/editor files.
- `git diff --check`: no whitespace errors; only CRLF normalization warnings in touched files.
- Build: still blocked by CPU gate; latest `typeperf` and `Win32_Processor.LoadPercentage` samples both reported 100% total CPU with no `dotnet`/`csc` compiler process.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" task10="ABSENT_FROM_XML" />
  <TaskReconciliation note="No task was reopened by this correction; this pass tightens compile-wall and job-route proof for Tasks 05, 15, and 20." />
  <StructLayout name="PumpNodeDTO" sizeBytes="32" unchanged="true">
    <Field name="NodeHash" offset="0" size="4" />
    <Field name="IngressRate" offset="4" size="4" />
    <Field name="MaxPumpRate" offset="8" size="4" />
    <Field name="CurrentEvacuationRate" offset="12" size="4" />
    <Field name="Flags" offset="16" size="4" />
    <Field name="PowerDraw" offset="20" size="4" />
    <Field name="_pad0.._pad7" offset="24..31" size="8" />
  </StructLayout>
  <VaultBuffers ids="95820..95842" owner="SumpPumpDrainageBufferIds" centralEnumAdditions="0" persistentPrivateArrays="0" />
  <Scalability curve="iterations = clamp((int)math.lerp(1,8,GlobalQualityWeight),1,8)" binarySwitches="0" />
  <DependencyGraph consumed="dispatcher slow tick, Fluid Incursion front/back Vault rows, logistics pressure rows" produced="solver JobHandle finalized in late-frame, shader flow rows after fence" blockingHotComplete="0" />
  <PointerAliasing noAlias="true" aggregateCounterAtomics="0" />
  <CompileGuard centralH8MemoryDrainageEntries="0" newAsmdefRefs="0" />
  <DearLie before="particle/object/recursive water O(scene objects + edges)" after="CSR math O(edges + nodes * iterations), CPU visual payload O(edges)" />
</SELF_AUDIT>

## 2026-05-20 - Polish Mandate Static Audit Pass

What was wrong:
- The first drainage BufferID lane used `70820..70841`, which static grep proved was already locally cast by graphics culling, atmosphere, sonar, and wreckage systems.
- `SumpPumpPipeGridRuntime` persisted obsolete pointer-bearing `VaultBufferHandle<T>` descriptors, contrary to the current binary payload ledger.
- `BuildCsrPipeGraphJob` capped the final valid edge count after prefix construction, so malformed edge counts could leave node offsets beyond destination/flow capacity.
- Parallel pump drains wrote adjacent aggregate `int` counters through `Interlocked.Add`, creating avoidable cache-line contention.
- CSV profile writes, tuning writes, and mock topology writes were cold paths but still lacked explicit Vault write fences.

What was done:
- Moved drainage owner-local BufferIDs to `95820..95842`, adding `PumpMassError` for per-pump mass-error rows and documenting the rejected `70820..70841` range in the binary ledger.
- Replaced all persistent drainage runtime handles with `VaultGenerationHandle<T>` and method-local `IDataVault.TryResolveHandle` views.
- Bounded CSR prefix offsets to the minimum real edge capacity before any destination/conductance/flow slot is written.
- Removed parallel adjacent aggregate counter atomics. `EvacuateWaterVolumeJob` writes per-pump rate, remainder, and mass-error rows; `DrainageTelemetryRecorderJob` performs the single deterministic reduction.
- Added Vault locks for CSV profile ingestion, tuning edits, mock network generation, solver job buffers, Fluid Incursion buffers, and the new mass-error buffer.
- Preserved the exact mandated `PumpNodeDTO` 32-byte layout with `_pad0.._pad7` at offsets 24..31.
- Throttled the editor tuner readout to 4Hz and removed per-frame string concatenation from its combined telemetry label; this is editor-only and not in the simulation hot path.

Cinematic Cheats used:
- Pipe water remains a Dear Lie: the CPU solves scalar pressure/flow and uploads `DrainagePipeFlowGpuDTO`; shader panning/refractive normals carry the visual water motion.
- No liquid particles, rigidbodies, colliders, CPU liquid meshes, or recursive room balancing participate in pump authority.
- Gravity is a one-dot-product conductance bias during CSR rebuild, not a physical fluid simulation.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. Build/profile proof remains blocked by the enforced CPU gate; latest samples were 95.2-100% CPU with no `dotnet`/`csc` process, so no compile was launched.
- Static expected delta: removed multi-core `Interlocked.Add` aggregate contention from active pump drains and replaced it with one linear telemetry reduction. Expected low-end benefit is lower MESI invalidation traffic under many active pumps.
- Static expected delta: collision-free BufferIDs prevent catastrophic Vault alias corruption; this is correctness, not a microsecond claim.

Verification:
- `rg` scan: no `VaultBufferHandle`, `GetBufferHandle`, `.Resolve(_vault)`, `Interlocked.Add`, `NativeDisableParallelForRestriction`, `foreach`, LINQ, `Time.deltaTime`, or `Pack=1` in SHINOBU_222 runtime/jobs/contracts/editor files.
- Burst scan: all six jobs use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
- DTO scan: `PumpNodeDTO` is explicit 32 bytes with required offsets; `PipeEdgeDTO`, `DrainageTuningDTO`, and `DrainageTelemetryEntry` are 64 bytes; GPU flow row is 16 bytes.
- BufferID scan: `95820..95842` appear as drainage owner-local constants plus unrelated generated hash/content literals, not as other `BufferID` owners.
- `git diff --check`: no whitespace errors in targeted files; only pre-existing CRLF normalization warnings in touched files.
- Build: still blocked by CPU gate, not executed.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" taskCount="19" task10="ABSENT_FROM_XML" />
  <TaskReconciliation>
    <Task id="01" status="PASS" proof="Old object pump drain authority retired; scan showed no particle/mesh water pump path in SHINOBU_222 authority." />
    <Task id="02" status="PASS" proof="Pipe state is CSR edge conductance/sealed flags; no Rigidbody/collider connectivity check in solver." />
    <Task id="03" status="PASS" proof="Pump/pipe DTOs are raw public fields; jobs use pointer/ref memory access." />
    <Task id="04" status="PASS" proof="PumpNodeDTO explicit 32 bytes, offsets 0,4,8,12,16,20,24..31 validated cold." />
    <Task id="05" status="PASS" proof="DrainageMockNetworkJob injects deterministic 1000-node/2500-edge topology and clears stale rows." />
    <Task id="06" status="PASS" proof="BuildCsrPipeGraphJob creates bounded CSR offsets, destinations, conductance, flow, and flat-edge index rows." />
    <Task id="07" status="PASS" proof="Jacobi pressure job uses double-buffered pressure arrays and deterministic Burst." />
    <Task id="08" status="PASS" proof="Evacuation drains Fluid Incursion Vault front/back rows with float-bit CAS and quantized remainder." />
    <Task id="09" status="PASS" proof="PowerPotential hydrates from `ShinobuLogisticsPressureFront`; pump rate is scalar-multiplied." />
    <Task id="10" status="N/A" proof="No Task 10 exists inside the SHINOBU_222 XML block." />
    <Task id="11" status="PASS" proof="Flow scalars go to `DrainagePipeFlowGpuDTO` and connection spline scalar; no CPU water geometry." />
    <Task id="12" status="PASS" proof="Solver iterations use continuous `lerp(1,8,GlobalQualityWeight)`." />
    <Task id="13" status="PASS" proof="Evacuation uses `MassQuantumM3`, per-pump remainder, and CAS deltas." />
    <Task id="14" status="PASS" proof="Downhill scalar subtracts `double3` AUP endpoints before local `float3` gravity dot." />
    <Task id="15" status="PASS" proof="Jobs use deterministic Burst; DTOs are blittable explicit rows in Vault." />
    <Task id="16" status="PASS" proof="300-entry telemetry ring and `Dump_SHINOBU_222.bin` non-finite path exist." />
    <Task id="17" status="PASS" proof="UI Toolkit tuner edits Vault tuning DTOs and throttles editor readout." />
    <Task id="18" status="PASS" proof="Cold `ReadOnlySpan<byte>` CSV parser writes hashed profiles into Vault." />
    <Task id="19" status="PASS" proof="Scene gizmo renders pressure/flow from Vault AUP/edge/pressure rows." />
    <Task id="20" status="PASS_STATIC_ONLY" proof="Static self-audit complete; compile/profiler proof blocked by CPU gate." />
  </TaskReconciliation>
  <StructLayout name="PumpNodeDTO" sizeBytes="32" alignment="32">
    <Field name="NodeHash" offset="0" size="4" />
    <Field name="IngressRate" offset="4" size="4" />
    <Field name="MaxPumpRate" offset="8" size="4" />
    <Field name="CurrentEvacuationRate" offset="12" size="4" />
    <Field name="Flags" offset="16" size="4" />
    <Field name="PowerDraw" offset="20" size="4" />
    <Field name="_pad0.._pad7" offset="24..31" size="8" />
  </StructLayout>
  <VaultBuffers ids="95820..95842" handleType="VaultGenerationHandle" persistentPrivateArrays="0" />
  <DependencyGraph input="dispatcher slow tick, Fluid Incursion front/back buffers, Logistics pressure front" output="solver JobHandle finalized in late-frame tick; shader flow buffer after fence" />
  <PointerAliasing noAlias="true" parallelCounterAtomics="removed" />
  <CompileGuard newAsmdefRefs="0" note="No asmdef was edited; existing Core sibling references are pre-existing project debt." />
  <DearLie before="object/particle/recursive flow O(objects + edges + scene traversal)" after="CSR solve O(edges + nodes * iterations), visual water O(edge scalars) on CPU" />
</SELF_AUDIT>

## 2026-05-20 - Static Power Authority Correction

What was wrong:
- The runtime power hydration already failed missing Logistics rows to `0.0`, but `PipePressureSolverJob` still had a local `PowerPotential` read fallback of `1.0`.
- The quantized drain clamp prevented huge positive overflow but did not lower-bound a negative corrupted remainder before float-to-int conversion.
- Short Logistics pressure rows wrote zero for missing indices but did not mark the frame with `MissingPowerVault`.

What was done:
- Changed Jacobi pump power fallback to `0.0`, so missing, out-of-range, or non-finite local power cannot synthesize pump pressure.
- Changed Logistics pressure hydration to return `MissingPowerVault` when the source row count is shorter than the copied node range.
- Changed quantized unit clamping to `[0, MaxQuantizedDrainUnitsPerPump]` before integer conversion.

Cinematic Cheats used:
- No physical water was reintroduced. Drainage authority remains scalar CSR/Jacobi math over Vault rows; pipe visuals remain shader-side flow scalars.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This pass is correctness hardening, not a measured speed claim.
- Expected hot-path cost is one scalar fallback and one `math.clamp`; expected low-end benefit is avoiding wasted pump solve/drain work when power data is absent or poisoned.

Verification:
- Static source read: `VaultGenerationHandle<T>.BufferID` is a `uint`; existing `handle.BufferID == 0u` checks are compile-consistent with the Vault ABI.
- Static source read: `ConnectionSplineBatchRenderer.SetPipeNodeFlow(uint, float)` exists and matches the SHINOBU visual-sync call.
- Forbidden-pattern scan stayed clean for stale Vault handles, direct job execute/complete, synthetic full-power fallback, parallel aggregate atomics, LINQ, `foreach`, `Time.deltaTime`, and `Pack=1` in SHINOBU_222 files.
- `git diff --check`: no whitespace errors in targeted files; CRLF warning only in the shared binary ledger.
- Build: BLOCKED BY CPU GATE. Latest `Win32_Processor.LoadPercentage` sample was 100%; compiler process count was 0; no `dotnet build` launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="PowerAuthorityFallbackClamp" />
  <PowerAuthority source="BufferID.ShinobuLogisticsPressureFront" missingRows="0.0 power + MissingPowerVault" jacobiFallback="0.0" syntheticFullPowerFallback="0" />
  <Quantization unitsClamp="[0, MaxQuantizedDrainUnitsPerPump]" intCastBeforeClamp="0" />
  <CompileGuard directBuildLaunched="0" cpuPercent="100" compilerProcesses="0" />
</SELF_AUDIT>

## 2026-05-20 - Worktree Job Route Reconciliation

What was wrong:
- Targeted `git diff` showed the active worktree had `GenerateMockDrainageNetwork()` calling `DrainageMockNetworkJob.Execute()` directly, even though the recorded rationale and HEAD view already required `job.Run()`.

What was done:
- Patched the active worktree call site back to `job.Run()`.
- Re-added this condition to the immediate forbidden-pattern verification set before any build gate attempt.

Cinematic Cheats used:
- No visual or physics change. This is a job-route correction for the deterministic mock topology generator.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This change is compile/job discipline, not a runtime speed claim.

Verification:
- Forbidden-pattern scan rerun: zero direct `.Execute()` calls in SHINOBU_222 runtime/jobs/contracts/editor files.
- Positive source scan: `SumpPumpPipeGridRuntime.cs:283` invokes `job.Run()`.
- Build: BLOCKED BY CPU GATE. Latest `Win32_Processor.LoadPercentage` sample was 100%; compiler process count was 0; no `dotnet build` launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="WorktreeJobRouteReconciliation" />
  <JobRoute mockTopology="IJob.Run" directExecuteAllowed="0" />
  <CompileGuard directBuildLaunched="0" cpuPercent="100" compilerProcesses="0" />
</SELF_AUDIT>

## 2026-05-20 - Utilization-Scaled Pump Energy Telemetry

What was wrong:
- Pump watt telemetry reported full Vault `PowerDraw` whenever a pump moved any water, even if low power potential or low available room water reduced actual evacuation rate.

What was done:
- `DrainageTelemetryRecorderJob` now computes `utilization = saturate(CurrentEvacuationRate / MaxPumpRate)` and multiplies Vault `PowerDraw` by that scalar.
- Zero actual drain now records zero pump watts; partial drain records proportional watts.

Cinematic Cheats used:
- No new simulation. This keeps the Dear Lie: energy reporting is a scalar derived from Vault rows and actual drain output, not RPM, fluid turbulence, particles, or motor physics.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This pass intentionally adds one reciprocal/saturate in the telemetry reducer for correctness.

Verification:
- Positive source scan: `DrainageTelemetryRecorderJob` computes a saturated utilization scalar before adding pump watts.
- Forbidden-pattern scan: zero direct `.Execute()` calls, zero stale Vault handle APIs, zero synthetic full-power fallback, zero `Interlocked.Add`, zero LINQ/`foreach`, zero `Time.deltaTime`, and zero `Pack=1` in SHINOBU_222 files.
- Build: BLOCKED BY CPU GATE. Latest `Win32_Processor.LoadPercentage` sample was 100%; compiler process count was 0; no `dotnet build` launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="UtilizationScaledEnergyTelemetry" />
  <EnergyTelemetry powerDrawSource="PumpNodeDTO.PowerDraw" utilizationSource="CurrentEvacuationRate / MaxPumpRate" hiddenFullDrawFallback="0" />
  <DearLie physicalMotorSimulation="0" />
  <CompileGuard directBuildLaunched="0" cpuPercent="100" compilerProcesses="0" />
</SELF_AUDIT>

## 2026-05-20 - Editor Readout String Formatting Purge

What was wrong:
- The Base Drainage Tuner telemetry readout used formatted label strings in the editor refresh loop, conflicting with Task 17's zero-GC readout requirement.
- The counter clamp used `Mathf.Min(int.MaxValue, uintValue)`, which is a compile-risk overload route.
- The active worktree had again drifted to direct `DrainageMockNetworkJob.Execute()` in the cold mock generator.

What was done:
- Replaced telemetry label formatting with pre-created `IntegerField` and `FloatField` readout controls updated through `SetValueWithoutNotify`.
- Added explicit `ClampUIntToInt(uint)` for frame index, active pump count, and solver microseconds.
- Patched the mock topology generator back to `job.Run()`.

Cinematic Cheats used:
- No physical water, pipe mesh animation, or particle route was added. The editor reads Vault telemetry; runtime visuals remain shader-side scalar flow.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. Unity import/profiler proof remains blocked by CPU gate.
- Expected editor benefit is removal of per-refresh formatted-string churn. Runtime frame cost is unchanged because this path is editor-only.

Verification:
- Forbidden-pattern scan: zero direct `.Execute()` calls, zero stale Vault handle APIs, zero synthetic full-power fallback, zero `Interlocked.Add`, zero LINQ/`foreach`, zero `Time.deltaTime`, zero `Pack=1`, zero `StringBuilder`, zero `ToString(`, zero `CultureInfo`, and zero `Mathf.Min` in SHINOBU_222 files.
- Positive source scan: `SumpPumpPipeGridRuntime.cs:283` invokes `job.Run()`; tuner readouts use `SetValueWithoutNotify` and `ClampUIntToInt`.
- `git diff --check`: no whitespace errors in targeted files; repository LF/CRLF warnings only.
- Build: BLOCKED BY CPU GATE. Latest `Win32_Processor.LoadPercentage` sample was 88%; compiler process count was 0; no `dotnet build` launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="EditorReadoutFormattingPurge" />
  <EditorReadout formattedStrings="0" stringBuilder="0" cultureInfo="0" valueRoute="SetValueWithoutNotify" />
  <JobRoute mockTopology="IJob.Run" directExecuteAllowed="0" />
  <CompileGuard directBuildLaunched="0" cpuPercent="88" compilerProcesses="0" />
</SELF_AUDIT>

## 2026-05-20 - Assembly Boundary And Compile Gate Proof

What was wrong:
- The prior report had not recorded the exact asmdef owner for the SHINOBU_222 files after the editor/readout correction.
- The most recent compile gate sample changed from 88% CPU to 100% CPU.

What was done:
- Walked upward from `Assets/_Project/Scripts/Construction/SumpPumpPipeGridRuntime.cs`; the owning assembly is the existing parent `Assets/_Project/Scripts/Hecton8.Core.asmdef`.
- Confirmed no asmdef was edited and no new sibling runtime assembly reference was introduced by SHINOBU_222.
- Resampled CPU/compiler state before any build attempt.

Cinematic Cheats used:
- None added in this proof pass. Runtime still uses CSR scalar math plus shader flow scalars, not particles or physical pipe fluid actors.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. This pass is compile-wall evidence and build-gate discipline, not a runtime optimization.

Verification:
- Assembly boundary scan: SHINOBU_222 files resolve to existing parent `Hecton8.Core.asmdef`; no `Construction` asmdef exists under the edited folder.
- Forbidden-pattern scan: no direct `.Execute()`, no `.Complete()`, no stale Vault pointer handles, no LINQ/`foreach`, no `Time.deltaTime`, no `Pack=1`, and no editor string-formatting readout patterns in SHINOBU_222 files.
- Build: BLOCKED BY CPU GATE. Latest `Win32_Processor.LoadPercentage` sample was 100%; compiler process count was 0; no `dotnet build` launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="AssemblyBoundaryCompileGateProof" />
  <CompileGuard ownerAsmdef="Assets/_Project/Scripts/Hecton8.Core.asmdef" asmdefEdited="0" newSiblingRuntimeReference="0" />
  <BuildGate directBuildLaunched="0" cpuPercent="100" compilerProcesses="0" />
</SELF_AUDIT>

## 2026-05-20 - Final Direct Job Entry Recheck

What was wrong:
- A post-documentation forbidden scan found the active worktree again calling `DrainageMockNetworkJob.Execute()` at `SumpPumpPipeGridRuntime.cs:283`.

What was done:
- Preserved the scheduled route: `job.Schedule()`, `H8Memory.RegisterActiveJob(OwnerSystem, _mockSeedHandle)`, and later `DispatcherJobFence` finalization.
- Reran direct route and full forbidden-pattern scans.

Cinematic Cheats used:
- None added. This is job-route hygiene; drainage remains Vault CSR math plus shader-side scalar flow visuals.

Exact microseconds saved:
- Measured exact savings: NOT AVAILABLE. The correction is architectural discipline, not a measured hot-path optimization.

Verification:
- Direct route scan: `GenerateMockDrainageNetwork()` invokes `job.Schedule()` and registers `_mockSeedHandle`.
- Forbidden-pattern scan: zero `.Execute()` matches across SHINOBU_222 files.
- Build: BLOCKED BY CPU GATE. Latest gate samples stayed above threshold at 68-100%; compiler process count was 0; no `dotnet build` launched.

<SELF_AUDIT>
  <Agent id="SHINOBU_222" role="SUMP_PUMP_PIPE_GRID_SOLVER" pass="FinalDirectJobEntryRecheck" />
  <JobRoute mockTopology="IJob.Schedule+RegisteredHandle" directExecuteMatches="0" />
  <BuildGate directBuildLaunched="0" cpuPercentRange="68-100" compilerProcesses="0" />
</SELF_AUDIT>
