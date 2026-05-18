# Rationale_SHINOBU_40

Date: 2026-05-18
Agent: SHINOBU_40
Domain: MASTER_INTEGRATOR_AND_DISPATCHER
Status: CORE TASKS COMPLETE / COMPILE BLOCKED BY EXTERNAL WAKE REQUEST DEPENDENCY

## Initial Constraints

Problem: Thread stalls from chaotic Unity Job dependencies and scattered MonoBehaviour phase ownership.
Solution: Central SystemDispatcher owns PRE_SIMULATION, SIMULATION, POST_SIMULATION, VISUAL_SYNC, combines simulation JobHandles, and completes exactly once in POST_SIMULATION.
Rejected Alternatives: Per-system Complete calls and local Update loops serialize worker threads and violate ARCH_Execution_Phases.
Scalability potential: Low uses bucketed cadence and optional visual-shed; Middle runs stable full cadence; High increases telemetry/detail density; Ultra spends saved time in VISUAL_SYNC only.
Hardware Impact: Expected benefit is stall flattening on i3/MX350 by avoiding mid-frame worker synchronization; exact runtime microseconds remain unmeasured because compile is blocked by external `WakeRequestSignal`.

## Decision 01 - Dispatcher Contract Surface

Problem: The dispatcher needed to accept work from 85 domains without hard references to those domains.
Solution: Added `IDispatcherSystem` and `IDispatcherFixedSystem` in the Core assembly and exposed registration through `GlobalRegistry.TryRegisterDispatcherSystem` / `TryRegisterDispatcherFixedSystem`.
Rejected Alternatives: Direct fields for Physics, AI, Ecosystem, or VFX systems were rejected as compile-wall coupling and sabotage under concurrent agents.
Scalability potential: Low/Middle systems can register bucketed cadence; High/Ultra can register visual-sync-only overkill without touching gameplay truth.
Hardware Impact: Expected low-tier gain is fewer main-thread dependency stalls; registration has zero per-frame allocation after boot.

## Decision 02 - Kahn Topology

Problem: Unordered job scheduling creates accidental dependency cycles and stalls.
Solution: Implemented Kahn in-degree sorting with preallocated arrays, dependency hashes, and `FatalArchitectureException` on cycles.
Rejected Alternatives: LINQ sorting, reflection over `IRequire<T>`, and runtime type scans were rejected for GC and nondeterminism.
Scalability potential: Low tier uses static mock/default topology; Middle/High/Ultra can add registered systems without changing dispatcher code.
Hardware Impact: Cold path cost only, estimated 30-80 us for 85 systems. No hot-path heap traffic.

## Decision 03 - JobHandle Dear Lie

Problem: Domains calling `.Complete()` independently serialize worker threads.
Solution: SIMULATION systems return `JobHandle`s to a Vault-backed NativeArray; dispatcher combines dependencies and calls one master `.Complete()` at POST_SIMULATION start.
Rejected Alternatives: Complete per domain, polling handles mid-update, or hand-written worker balancing.
Scalability potential: Low has one wait point and 64-bucket culling; Middle/High/Ultra can fill worker queues while the dispatcher remains the only barrier owner.
Hardware Impact: Expected i3/MX350 gain is 100-800 us in stall-heavy frames after domains adopt the contract; unmeasured in this compile-blocked pass.

## Decision 04 - DataVault Sovereignty Polish

Problem: The first patch used private persistent `NativeArray` fields for dispatcher job/telemetry buffers, violating H-Phi/DataVault ownership.
Solution: Replaced private arrays with `VaultBufferHandle<T>` fields and DataVault buffer IDs `SystemDispatcherMasterJobHandles` through `SystemDispatcherMasterMockTimeDilationSignals`.
Rejected Alternatives: H8Memory-owned private arrays were rejected after polish audit because they create local data ownership inside the dispatcher.
Scalability potential: Low keeps buffers centralized for memory pressure accounting; High/Ultra can inspect/resize through Vault policy later.
Hardware Impact: Uninitialized Vault allocations avoid boot zero-fill; expected cold-start savings are small but deterministic.

## Decision 05 - ARM64 Layout

Problem: Runtime DTOs with `Pack=1` or wrong field order create ARM64 misaligned loads.
Solution: New dispatcher DTOs use `StructLayout(Size=...)` without Pack. `DispatcherTimingDTO` is exactly 16 bytes: `FrameDelta` offset 0, `FixedDelta` offset 4, `TimeScale` offset 8, `ActiveBucketMask` offset 12. `JobDependencyDTO` is exactly 16 bytes: `JobHandlePtr` offset 0, `SystemIdHash` offset 8, `_pad0` offset 12.
Rejected Alternatives: Adding extra frame/bucket fields to `DispatcherTimingDTO` was rejected because the prompt requires the 16-byte timing contract.
Scalability potential: Same DTO travels across Low/Middle/High/Ultra with no platform-specific packing.
Hardware Impact: Prevents ARM64 unaligned traps; expected gain is correctness and avoided worst-case stalls, not a measurable micro-optimization yet.

## Decision 06 - Mock Time Dilation

Problem: Agent 32 scalability/time-dilation data may not exist during isolated integration.
Solution: Added `MockTimeDilationSignal` and `MockTimeDilationSignalJob`; emergency mock topology schedules local jobs that emit 1.0 or 0.1 scale and the dispatcher applies that scalar after POST_SIMULATION completion.
Rejected Alternatives: Waiting for external signal definitions or adding a real gameplay signal from this domain.
Scalability potential: Low can fake bullet-time by scalar multiply; High/Ultra still route real time dilation later through the proper signal corridor.
Hardware Impact: Mock path costs a tiny scheduled job; no gameplay physics simulation added.

## Decision 07 - Visual Sync Load Shedding

Problem: GPU/CPU pressure above 0.9 should not damage deterministic gameplay.
Solution: VISUAL_SYNC can be skipped for one frame from `SystemHealthIndexSignal` while SIMULATION and POST_SIMULATION still run.
Rejected Alternatives: Skipping physics/AI or silently dropping job completion.
Scalability potential: Low/Middle shed visuals first; High/Ultra spend recovered deterministic time on visual overkill when pressure is low.
Hardware Impact: Expected low-end benefit equals all registered visual-sync cost for that frame; no measured runtime artifact yet.

## Decision 08 - CSV Priority Override

Problem: Designers need execution priority tuning without recompiling C#.
Solution: Added editor/development-only `execution_priorities.csv` polling every 64 frames, parsed by a byte-level parser, then insertion-sorts registered systems before Kahn reruns.
Rejected Alternatives: Production filesystem polling and string/line parsing were rejected for Steam Deck MicroSD and GC pressure.
Scalability potential: Low can push expensive systems later/bucketed; High/Ultra can allow more visual sync systems while preserving dependency order.
Hardware Impact: Zero production cost; editor/development-only IO.

## Decision 09 - X-Ray Facade

Problem: Dispatcher phase timing and bucket load are invisible to humans.
Solution: Added an Editor-only `Execution Pipeline X-Ray` window with phase bars and a 64-cell bucket grid.
Rejected Alternatives: runtime UI overlay or Debug.Log spam.
Scalability potential: Low-tier tuning identifies clumped buckets; High/Ultra tuning can see when visual sync budget has headroom.
Hardware Impact: Editor only; no player cost.

## Compile Wall

Problem: Focused Core build fails before a clean end-to-end compile claim.
Solution: Ran `dotnet build Hecton8.Core.csproj --disable-build-servers -p:UseSharedCompilation=false /m:1 -v:minimal -clp:ErrorsOnly` twice; both failures are external `GlobalPhysicsStateManager.cs` references to missing `WakeRequestSignal` at lines 119 and 1343.
Rejected Alternatives: Defining `WakeRequestSignal` from SHINOBU_40 was rejected because SHINOBU_37 owns the physics culling/wake contract and other agents have already logged this compile wall.
Scalability potential: No runtime scalability conclusion can be claimed until that external dependency is resolved.
Hardware Impact: None from SHINOBU_40; compile verification blocked upstream.
