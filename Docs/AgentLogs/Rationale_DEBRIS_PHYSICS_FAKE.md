# Rationale_DEBRIS_PHYSICS_FAKE

## Decision 1 - Missing Batch Prompt

Problem: The requested prompt ID `DEBRIS_PHYSICS_FAKE` has no `<AGENT_PROMPT>` block in `Docs/Tasks/CURRENT_BATCH.md`, so there is no authoritative task list, no task count, and no XML-scoped domain boundary.

Solution: Treat the task as blocked before implementation. The mandatory batch extraction was executed with a CLI regex, then cross-checked against `Docs/Tasks/CURRENT_BATCH_AUDIT_20260516.md`, which explicitly lists `DEBRIS_PHYSICS_FAKE` as missing from the active batch.

Rejected Alternatives: Implementing from the launcher list alone was rejected because it would synthesize a missing prompt. Reading archived batch logs was rejected because active hygiene rules forbid using previous batch material unless explicitly ordered.

Scalability potential: No runtime debris implementation was created. Low, Middle, High, and Ultra tiers remain undefined until the authoritative prompt exists.

Hardware Impact: 0 us runtime change on i3/MX350 because no gameplay/render code was changed.

## Decision 2 - No Source Edits

Problem: GPU-only debris chips would touch VFX/rendering code and likely signal ingestion. Without the XML task list, the write boundary and required integration points are undefined.

Solution: Leave source unchanged and record the blocker in `Status_DEBRIS_PHYSICS_FAKE.md` and this rationale file.

Rejected Alternatives: Editing existing `DebrisManager`, `HectonFluidEngine`, shader, or signal code based only on a one-line task was rejected because it risks colliding with active agents and violates the current batch prompt protocol.

Scalability potential: The desired solution should eventually be GPU-resident, signal-driven, and tiered from cheap billboard/triangle chips on low hardware to denser shader-lit shard fields on high hardware.

Hardware Impact: 0 us runtime change. No bandwidth, GC, CPU, or VRAM delta introduced.

## Decision 3 - Phase 1 CPU Debris Purge

Problem: Voxel carve aftermath still had a CPU debris path that registered dropped items per carve and a legacy laser path that called `IDebrisService.SpawnBurst`. This violates the XML rule: zero GameObjects and all debris rendered by indirect instancing.

Solution: Removed the dropped-item debris loop and the legacy transient SpawnBurst path from `VoxelDeltaProcessor`. Voxel carve, mining outcrops, drills, player CCD impacts, and vehicle CCD impacts now publish `DebrisSpawnSignal` with `FlagComputeShard`. The existing `CarveDebrisComputeRenderer` consumes the non-destructive signal snapshot and injects requests into the DataVault-backed compute path.

Rejected Alternatives: Pooling dropped-item GameObjects was rejected because it still burns transform, registry, and lifecycle CPU. Keeping `IDebrisService` as a fallback was rejected because it preserves a second debris authority and makes scene cleanup ambiguous.

Scalability potential: Low = capped signal scan and existing low-tier renderer capacity. Middle = GPU shards without SDF collision. High = denser compute shards with shader tumble. Ultra = expanded particle capacity and heavier material response without producer code changes.

Hardware Impact: Expected i3/MX350 gain is the removed per-carve dropped-item registration loop plus removed legacy SpawnBurst work. Runtime measurement is pending compile/runtime profiling; static estimate is tens to hundreds of microseconds saved on burst-heavy mining frames, depending on the previous `carveDebrisMaxCount` path.

## Decision 4 - Debris Service Registry Boundary

Problem: Bootstrap still treated `DebrisManager` as the debris runtime service, which can instantiate or preserve a GameObject-based service during startup even when GPU debris is the target authority.

Solution: Added `IDebrisComputeService` and `GlobalRegistryServiceSlot.DebrisComputeRuntime`. `CarveDebrisComputeRenderer` registers itself as the compute debris service. Bootstrap readiness and scene cleanup now target `GlobalRegistry.DebrisCompute` instead of creating or depending on the legacy `DebrisManager`.

Rejected Alternatives: Directly referencing the VFX renderer from bootstrap was rejected because it would hard-wire a scene component into core startup. Reusing `IDebrisService` for GPU debris was rejected because the old service contract exposes CPU debris semantics such as `ClearActiveDebris` and SpawnBurst-era ownership.

Scalability potential: Low = no bootstrap-created debris GameObject on weak devices. Middle = renderer self-registers only when present. High = future GPU debris variants can implement the same registry contract. Ultra = multiple visual implementations can be scene-selected without changing mining producers.

Hardware Impact: Expected i3/MX350 gain is removal of startup/lifecycle work for the legacy debris runtime and less scene teardown churn. Static estimate is small per frame but important during mining spikes because producers now publish one unmanaged signal instead of touching a service object.

## Decision 5 - Non-Destructive Signal Ingestion

Problem: `GlobalSignals.TryDequeueDebrisSpawn` is a destructive queue. If the GPU renderer consumed it directly, it could starve other systems or depend on execution order.

Solution: Mirrored `DebrisSpawnSignal` into `SignalBus<DebrisSpawnSignal>` during publish. `CarveDebrisComputeRenderer` reads the frame snapshot and only handles signals tagged with `FlagComputeShard`.

Rejected Alternatives: Draining the existing queue in VFX was rejected because it introduces order bugs. Adding direct calls from mining scripts to the renderer was rejected because it creates cross-domain coupling and breaks the multi-agent decoupling rule.

Scalability potential: Low = bounded scan of 64 debris spawn signals per frame. Middle = multiple producers can publish unmanaged signals without allocations. High = renderer-side capacity controls density. Ultra = high-tier renderer can read the same signal lane and expand injection quality.

Hardware Impact: Expected i3/MX350 gain is indirect: signal fanout avoids per-producer service lookups and avoids CPU object handoff. The added snapshot push is unmanaged and fixed-capacity; microsecond cost is expected below the old CPU debris branch and must be validated in runtime profiling.

## Decision 6 - Compile Blocker Boundary

Problem: `dotnet build Hecton8.Core.csproj` is blocked by external domain dependency drift. First compile stopped in `Assets/_Project/Scripts/Fauna/FaunaKinematicsRuntime.cs` on unresolved bite IK types. After concurrent worktree movement, the next compile stopped on docking autopilot, VFX wake, and ecosystem macro swarm contracts: unresolved `IDockingAutopilotService`, `ActiveSplineData`, `WakeSource`, `WakeTelemetryEntry`, plus new `IEcosystemDirectorService` members not implemented by `EcosystemDirector`.

Solution: Do not edit fauna code under the debris prompt. Record the compile blocker and leave the Phase 1 debris purge intact. The targeted debris static scan found no remaining legacy CPU debris calls in the modified purge path.

Rejected Alternatives: Patching fauna, docking, wake, or ecosystem types from the debris task was rejected because it would cross into other agent domains without an interface-level debris reason. Reverting those changes was rejected because they were not authored by this agent and may be active work.

Scalability potential: Low/Middle/High/Ultra debris scalability remains unaffected by this external build break; runtime profiling is blocked until the fauna compile wall is resolved by its owner or integrator.

Hardware Impact: 0 us direct debris runtime impact from this decision. Compile validation is blocked externally, so measured microsecond proof for Phase 1 must wait.
