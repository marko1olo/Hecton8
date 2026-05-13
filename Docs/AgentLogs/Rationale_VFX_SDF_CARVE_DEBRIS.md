# Rationale - VFX_SDF_CARVE_DEBRIS

Status: PENDING VERIFICATION

## Decision 0 - Domain and Mandate Boundary

Problem: SDF carve feedback needs GPU debris visuals without introducing gameplay physics, Unity `ParticleSystem`, or direct dependencies on unfinished systems owned by other agents.
Solution: Treat this as presentation VFX. Use GPU-resident particle buffers, explicit event ingestion, and capability probing for optional voxel/flow/global-vault integrations. The VFX path must fail closed when dependencies are absent.
Rejected Alternatives: Unity `ParticleSystem` was rejected by prompt. CPU GameObject debris was rejected because it allocates, adds transform overhead, and violates GPU residency. Direct hard links to unknown voxel classes were rejected because 20+ agents are editing in parallel.
Scalability potential: Low = 16 injected particles, no SDF texture collision. Middle = 32 particles and flow drag. High = 64 particles with SDF collision. Ultra = 64+ visual richness through shader/mesh variation, not a larger base cost.
Hardware Impact: MX350/i3 gain is avoiding ParticleSystem/GameObject spawn churn and keeping event bursts in fixed buffers; estimated savings PENDING PROFILER, regression model 150-400 microseconds on burst frames versus GameObject debris.

## Decision 1 - Carve Signal Bridge

Problem: The voxel owner queued validated `VoxelCarveEvent` packets but only published a derived debris signal, losing exact carve radius and direct SDF intent.
Solution: Make `VoxelCarveEvent` implement `ISignal` and push the sanitized queued packet into `SignalBus<VoxelCarveEvent>` after volume-id assignment. This keeps VFX read-only and lets the existing voxel queue remain authoritative.
Rejected Alternatives: Polling `VoxelDeltaProcessor` internals was rejected because it hard-couples VFX to voxel queue ownership. Consuming only `DebrisSpawnSignal` was rejected because it is already a secondary gameplay/ecosystem feedback packet, not the requested carve packet.
Scalability potential: Low/Middle/High/Ultra all receive the same packet; visual scale is selected downstream by Math LOD, not by modifying voxel logic.
Hardware Impact: One typed signal enqueue per carve is estimated at 4-8 us on i3/MX350 and replaces any GameObject or ParticleSystem spawn path.

## Decision 2 - SOA DataVault Lane

Problem: Carve debris needs fixed hot-path storage with no per-frame allocations and no CPU readback.
Solution: Add `BufferID.CarveDebris` and `BufferID.CarveDebrisVelocity`, request both from `GlobalDataVault`, and mirror them into persistent `GraphicsBuffer` ping-pong lanes. Position/lifetime stays `float4`; velocity is a separate `float4` lane.
Rejected Alternatives: A managed `NativeArray` owned only by the renderer was rejected because prompt requires H-PHI/DataVault sovereignty. An AoS debris struct was rejected because the render shader only needs position/lifetime and should not fetch velocity/flags.
Scalability potential: Low = same 4096 cap but only 16 injected per carve and no SDF texture reads. Middle = 32-64 injection depending authored setting. High/Ultra = 64 injection, flow drag, SDF dissolve, shader variation.
Hardware Impact: SOA avoids unused velocity reads in the draw pass; estimated savings 10-20 us per full 4096 instance render on MX350-class hardware.

## Decision 3 - Dead-Slot Injection Job

Problem: Burst carve events can arrive while previous debris is still alive, and recycling must avoid allocations and CPU `GetData`.
Solution: A Burst `IJob` scans for `w <= 0`, uses `Unity.Mathematics.Random` seeded from frame + absolute carve coordinates, writes only dead slots, and uploads the dirty range to both GPU ping-pong buffers with `LockBufferForWrite`.
Rejected Alternatives: A CPU free-list was rejected because GPU collision can kill particles earlier than the CPU mirror knows, making the free list stale without readback. A full-buffer upload every frame was rejected because it burns bandwidth for dead/static slots.
Scalability potential: Low tier injects 16. High/Ultra keep 64 and spend saved GameObject cost on denser chips and shader variation.
Hardware Impact: Dirty-range upload after injection is bounded to injected slot span; expected burst-frame saving is 80-180 us versus full-buffer upload plus managed ParticleSystem emission on i3/MX350.

## Decision 4 - Compute Advection Reuse

Problem: Carve debris must use abyssal flow and SDF collision without creating a parallel fluid compute architecture.
Solution: Extend `Hecton_FluidAdvection.compute` with `AdvectCarveDebris`, `ClearCarveDebrisIndirectArgs`, and `CullCarveDebrisForRender`. The new kernel reuses existing flow/SDF sampler functions, applies gravity, and writes indirect args on GPU.
Rejected Alternatives: A separate compute shader was rejected because it would duplicate SDF/flow binding logic and drift from the fluid owner. CPU advection was rejected because it violates GPU residency and cannot hit 0.1 ms during carve bursts.
Scalability potential: Low skips SDF sampling and uses the same cheap gravity/flow-off path. Middle uses flow texture when available. High/Ultra use flow drag plus SDF dissolve and spend saved CPU on richer mesh/shader variation.
Hardware Impact: Low tier avoids one 3D SDF texture sample per live particle. Full 4096-slot dispatch is estimated at 20-45 us on MX350; CPU transform debris would cost several hundred microseconds under burst load.

## Decision 5 - Indirect CoreLit Rock Chips

Problem: Visual feedback must look like solid chips, not billboard dust, and cannot instantiate renderers.
Solution: Render a low-poly octahedron through `Graphics.RenderMeshIndirect`, GPU cull into an indirect args buffer, and shade with a small URP shader that includes `Hecton_CoreLit.hlsl` for cave ambient, caustic scatter, fog, and dither fade.
Rejected Alternatives: Unity `ParticleSystem` mesh mode was rejected by prompt and has managed emission overhead. VFX Graph was rejected because the prompt explicitly targets a compute buffer in the existing advection shader.
Scalability potential: Low gets fewer chips but same silhouette. Middle/High/Ultra increase perceived richness through shader chip-edge tint and flow/SDF behavior, not through unbounded particle count.
Hardware Impact: One indirect draw replaces dozens of renderer submissions; expected burst-frame saving is 150-400 us on i3/MX350 compared with spawned mesh renderers.

## Decision 6 - AUP Shift on GPU

Problem: Floating origin shifts can leave existing debris visually offset if particle positions are not rebased.
Solution: Accumulate negative `AupShiftSignal.ShiftMeters` and add it in `AdvectCarveDebris` before velocity integration. The CPU mirror is only for lifetime/free-slot selection, so it does not need a full rebase upload.
Rejected Alternatives: CPU rebasing every `float4` position was rejected because it writes the full 4096 buffer after every origin shift. Ignoring rebase was rejected because it creates spatial lies during camera-origin movement.
Scalability potential: All tiers share the same constant-time shift uniform. Ultra can keep long-lived particles without rebase cost growth.
Hardware Impact: Saves up to 4096 CPU writes plus two GPU buffer uploads per origin shift; estimated 50-120 us avoided on low-end hardware during shift frames.

## Decision 7 - H-PHI and Math LOD

Problem: The renderer needs persistent storage without owning an independent memory island, and low-end hardware must not pay high-tier SDF costs.
Solution: Request position/lifetime and velocity lanes from `GlobalRegistry.DataVault`, then select particle count and SDF sampling from `GlobalRegistry.ScalabilityTier`, `ScalabilityTierProfileByte`, and `H8_LOW_MEMORY_PROFILE`.
Rejected Alternatives: Private `NativeArray` ownership was rejected because DataVault is the project memory authority. A single balanced middle path was rejected because HECTON-8 requires Low/Middle/High/Ultra split behavior.
Scalability potential: Low = 16 particles/carve, no SDF sample. Middle = flow drag with bounded 4096 storage. High = 64 particles and SDF dissolve. Ultra = same cap but stronger shader/lighting impression, not unbounded count.
Hardware Impact: MX350/i3 avoids 48 injection writes per carve and removes one 3D SDF texture fetch per live particle; estimated 30-90 us saved on dense carve frames.

## Decision 8 - Black Box and Compile Wall

Problem: GPU-only debris cannot be debugged through CPU readback, and Unity compile access is currently blocked by unavailable MCP transport.
Solution: Write a 300-frame native telemetry ring with active count, carve count, injected count, flags, hash, and AUP shift. Dump it to `Docs/AgentLogs/Dump_VFX_SDF_CARVE_DEBRIS.bin` on invalid state. Static indirect-args verification is recorded; Unity compile is blocked by tool/session availability, not ignored.
Rejected Alternatives: `ComputeBuffer.GetData` was rejected by prompt and would stall the GPU. Console-only diagnostics were rejected because they do not preserve the last 300 frames.
Scalability potential: Low through Ultra share the same fixed telemetry cost. High-end visual overkill does not increase telemetry size.
Hardware Impact: One native ring write per frame is negligible; avoiding GPU readback prevents millisecond-scale stalls on i3/MX350 during burst frames.
