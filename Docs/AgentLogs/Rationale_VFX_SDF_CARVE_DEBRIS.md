# Rationale - VFX_SDF_CARVE_DEBRIS

Status: PENDING VERIFICATION / STATIC CHECKS ONLY / UNITY COMPILE BLOCKED (NO DOTNET REBUILD RUN)

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
Solution: A Burst `IJob` scans for `w <= 0`, uses `Unity.Mathematics.Random` seeded from frame + absolute carve coordinates, writes only dead slots, and uploads the dirty range to both GPU ping-pong buffers with `LockBufferForWrite`. Later H-Phi pass batches up to 32 carve requests into one persistent native request buffer and one synchronous Burst run.
Rejected Alternatives: A CPU free-list was rejected because GPU collision can kill particles earlier than the CPU mirror knows, making the free list stale without readback. A full-buffer upload every frame was rejected because it burns bandwidth for dead/static slots. Per-carve `Schedule()+Complete()` was rejected because it adds one job-system fence per carve burst.
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
Solution: Request position/lifetime and velocity lanes from `GlobalRegistry.DataVault`, then select particle count and SDF sampling from `GlobalRegistry.ScalabilityTier`, `ScalabilityTierProfileByte`, and `H8_LOW_MEMORY_PROFILE`. Later pass caches the sampled tier for 30 frames and requires a 120-frame confirmation window before changing the low/high active capacity.
Rejected Alternatives: Private `NativeArray` ownership was rejected because DataVault is the project memory authority. A single balanced middle path was rejected because HECTON-8 requires Low/Middle/High/Ultra split behavior. Immediate tier flipping was rejected because capacity changes should not churn GPU upload ranges during transient hardware scaler changes.
Scalability potential: Low = 16 particles/carve, no SDF sample. Middle = flow drag with bounded 4096 storage. High = 64 particles and SDF dissolve. Ultra = same cap but stronger shader/lighting impression, not unbounded count.
Hardware Impact: MX350/i3 avoids 48 injection writes per carve and removes one 3D SDF texture fetch per live particle; estimated 30-90 us saved on dense carve frames.

## Decision 8 - Black Box and Compile Wall

Problem: GPU-only debris cannot be debugged through CPU readback, and Unity compile access is currently blocked by unavailable MCP transport.
Solution: Write a 300-frame native telemetry ring with active count, carve count, injected count, flags, hash, and AUP shift. Dump it to `Docs/AgentLogs/Dump_VFX_SDF_CARVE_DEBRIS.bin` on invalid state. Static indirect-args verification is recorded; Unity compile is blocked by tool/session availability, not ignored.
Rejected Alternatives: `ComputeBuffer.GetData` was rejected by prompt and would stall the GPU. Console-only diagnostics were rejected because they do not preserve the last 300 frames.
Scalability potential: Low through Ultra share the same fixed telemetry cost. High-end visual overkill does not increase telemetry size.
Hardware Impact: One native ring write per frame is negligible; avoiding GPU readback prevents millisecond-scale stalls on i3/MX350 during burst frames.

## Decision 9 - OMEGA Polish Changes

Problem: The final anti-bloat pass found lifetime normalization using scalar float division and one per-frame dispatch-group expression that made the hot path harder to audit. Unity validation is also blocked by MCP transport failure and an active project lock.
Solution: Replace lifetime divisions with `math.rcp` reciprocal multiplies, cache dispatch-group count after kernel thread-size discovery, and keep visible-count validation on GPU indirect args instead of CPU `GetData`. Static scans were rerun against the renderer, compute shader, and debris material shader.
Rejected Alternatives: Leaving float divisions in place was rejected because the polish mandate explicitly targets hot math. CPU `GetData` verification was rejected because it stalls the GPU and violates the prompt. Starting a second Unity batchmode compile while `Temp/UnityLockfile` is present was rejected because the live editor owns the project.
Scalability potential: Low = 16 chips, no SDF sample, same indirect material silhouette. Middle = bounded flow drag, no new renderer count. High = SDF dissolve plus dynamic wake advection. Ultra = more perceived richness through CoreLit caustics, edge tint, and dither fade without raising the fixed 4096 storage cap.
Hardware Impact: On i3/MX350 the direct savings from reciprocal replacement are sub-microsecond, but the critical savings remain avoiding GPU readback stalls and renderer/GameObject debris: 150-400 us on carve bursts, 30-90 us from low-tier SDF bypass, 50-120 us on AUP shift frames, and 10-20 us from SOA render reads.

## Decision 10 - Second-Pass Scalability and Binding Hardening

Problem: The first implementation met the prompt architecture, but the audit found four weak edges: low tier still retained too much of the 4096-slot cost envelope, fallback flow binding could be misread as active flow, dynamic wake buffers needed explicit zero-slot binding for kernels that share the fluid shader, and fast debris could tunnel across thin SDF surfaces without substeps.
Solution: Add a 1024 active-capacity path for low tier while preserving 4096 persistent storage for high/ultra; bind `HectonFluidEngine` published buffer/texture payloads through its public GPU contract; bind empty wake buffers with `_DynamicWakeParams.x = 0`; move fallback render resources to cold lifecycle hooks; add a compute-side velocity/step clamp; preserve blackbox invalid flags through the CPU mirror aging job.
Rejected Alternatives: A uniform 4096-slot path was rejected because it wastes low-end dispatch groups. CPU readback was rejected because the prompt forbids it and it would stall. Direct `HectonCaveVoxelLightingVolume.ActiveRuntimeInstance` access was rejected because its SDF publication API is internal and this renderer is intentionally isolated by asmdef. ParticleSystem fallback was rejected by prompt and by zero-GC policy. Substep SDF collision was rejected because a hard visual clamp is cheaper and predictable.
Scalability potential: Low = 1024 active slots, 16 injected chips, no SDF sample, zero dynamic wake overkill. Middle = 4096 storage with normal flow drag and authored texture override when available. High = 4096 active slots, 64 injected chips, SDF dissolve, published fluid texture/buffer binding. Ultra = same storage cap but stronger perceived richness from CoreLit caustics, edge tint, dynamic wake billow/shear, and longer-lived visual debris rather than unbounded particles.
Hardware Impact: MX350/i3 low tier drops carve debris dispatch from 64 to 16 thread groups, estimated 25-35 us GPU saved on dense frames. Idle frames skip the CPU mirror job when no debris is alive, estimated 10-25 us CPU saved. Velocity clamping prevents SDF miss-through without extra texture samples or substeps. Flow binding now avoids sampling fake fallback flow and uses the real published GPU payload when present.

## Decision 11 - Disk Reconciliation and Compile Wall Classification

Problem: A strict reread found that the status overstated several protections that were not present in the current renderer file, and Unity compile retries exposed unrelated project blockers before a live VFX import could be proven.
Solution: Patch the renderer to match the claimed contract: global SDF shader cache with a four-frame refresh cadence, explicit serialized camera use instead of `Camera.main`, `GlobalRegistry.Fluid` binding instead of `HectonFluidEngine.Instance`, subtract-only carve debris emission, shape/operation validation, box/blend radius fallback, full-packet deterministic seed hashing, AUP shift duplicate and NaN guards, mesh index-count draw guards, active-only telemetry warning publish, and SDF cache release cleanup. A single cross-domain `VoxelDeltaProcessor` fix was kept to the carve/decal interface: convert the `double3` AUP cave-in dust point back to `Vector3` for the existing decal API while preserving `double3` for runtime debris spawn conversion.
Rejected Alternatives: Leaving stale status was rejected because disk proof is the authority. Editing `SaveMasterHashV10`, `BinaryLayoutManifest`, hardware profile, homeostasis, or physics allocation errors was rejected because those are Echelon 1/5/core blockers outside this VFX/SDF carve boundary. CPU readback validation was rejected because it violates the prompt and creates GPU stalls.
Scalability potential: Low = explicit 1024-slot cap, no SDF sample, no false flow/SDF activation, and no idle telemetry publish. Middle = authored or published flow texture/buffer only when valid. High = global cave SDF fallback if no serialized texture is assigned. Ultra = same fixed storage with richer SDF/flow-driven chip motion and CoreLit response, not unbounded particle counts.
Hardware Impact: MX350/i3 avoids false high-tier SDF disable when the cave system publishes globals, avoids one telemetry warning publish every 30 idle frames, and prevents invalid AUP shifts from contaminating the compute constants. Estimated frame saving remains dominated by 25-35 us low-tier dispatch reduction, 30-90 us SDF/injection LOD reduction, and 150-400 us versus spawned mesh debris. Verification remains PENDING because Unity r5 stopped on license/headless entitlement before script compile, while `dotnet build Hecton8.Core.csproj` is blocked by unrelated Core/Save/Hardware/Physics errors and does not compile the VFX debris asmdef.

## Decision 12 - Batched Injection and Flow Payload Validation

Problem: The renderer still had per-carve same-frame job fences and accepted flow texture metadata before local validation. Under burst carving, that multiplies scheduler overhead. With bad texture metadata, buffer sampling could inherit the wrong center even when the texture path stayed inactive.
Solution: Add a persistent `NativeArray<CarveDebrisRequest>` sized to `MaxCarveSignalsPerFrame`, fill it from the signal snapshot, run one `CarveDebrisInjectBatchJob.Run()` per frame, and convert mirror aging to `AgeCarveDebrisMirrorJob.Run()`. Add local validation for flow buffers, grid resolution, center, spacing, texture presence, and texture parameter derivation before setting active flags or overwriting shared flow uniforms.
Rejected Alternatives: Keeping one `Schedule()+Complete()` per carve was rejected because it burns burst-frame CPU on scheduler fences. Direct managed lists or a CPU free-list were rejected because they add allocation/sync risk and diverge from GPU collision truth. Trusting `HectonFluidEngine` blindly was rejected because VFX consumers must fail closed at their own boundary.
Scalability potential: Low = one 1024-slot batch pass for up to 32 carve events and no SDF sample. Middle = one 4096-slot batch pass with validated flow binding. High = SDF plus validated published flow texture/buffer. Ultra = same fixed storage with richer flow-driven motion and CoreLit response, not unbounded particle count.
Hardware Impact: MX350/i3 saves an estimated 20-70 us on 2-32 carve burst frames by replacing per-carve scheduler fences with one synchronous Burst run. Active mirror frames save an estimated 5-20 us by avoiding job scheduler overhead. Flow validation prevents false active sampling and wrong-center buffer lookup; frame-time saving is scene-dependent, correctness impact is higher than raw CPU gain.

## Decision 13 - Tier Cache and Capacity Hysteresis

Problem: Low/high tier selection was sampled from `GlobalRegistry` every frame and changed active capacity immediately. That violates the project hysteresis rule and can cause repeated capacity shed/upload churn if the scalability dictator oscillates during frame-time spikes.
Solution: Cache the low-tier decision for 30 frames, initialize from the first sampled value, and require 120 frames of consistent opposite-tier samples before switching. Reset the cache on GPU state release. Low remains the fail-closed default before first sample.
Rejected Alternatives: Per-frame registry reads were rejected because they waste hot-path work and couple VFX cadence to scaler noise. Instant low/high active-capacity changes were rejected because they can repeatedly clear/upload tail ranges and change dispatch group counts during transient spikes.
Scalability potential: Low = stable 1024 active slots after confirmed downgrade. Middle/High/Ultra = stable 4096 active slots after confirmed upgrade. Ultra can still get visual overkill, but only after the device is consistently classified above low tier.
Hardware Impact: Removes 4-5 registry property reads per frame from this renderer and prevents repeated 1024/4096 capacity churn. CPU gain is sub-microsecond per steady frame; avoided churn can save 10-40 us during transient tier oscillation frames.

## Decision 14 - Monotonic Batch Injection Scan

Problem: The batched injection job removed scheduler fences, but each request could still begin its dead-slot search from index zero. In a mostly active buffer, 32 carve requests would repeatedly scan the same occupied prefix before finding free slots.
Solution: Carry one `scanStart` cursor across all requests in `CarveDebrisInjectBatchJob`. Each request resumes from the previous scan position, writes only `w <= 0` slots, and stops when the active capacity is exhausted. Invalid generated positions fail closed by skipping that slot and preserving the telemetry invalid-state flag.
Rejected Alternatives: A managed free-list was rejected because GPU-side SDF death can make CPU ownership stale without readback. A native free-list was rejected for this pass because it adds a second synchronization model and more mutation surface. Independent full-capacity scans were rejected because they are deterministic but wasteful during dense carve bursts.
Scalability potential: Low = at most 1024 slots walked once for up to 32 requests. Middle/High/Ultra = at most 4096 slots walked once, spending the saved CPU on denser visual response and CoreLit/flow richness rather than scheduler or prefix-scan waste.
Hardware Impact: On i3/MX350-class CPUs this removes repeated occupied-prefix reads during burst frames. Estimated saving is 15-60 us when several carve events arrive while the buffer is dense; idle and sparse frames remain effectively unchanged.

## Decision 15 - Flow Payload Center Compatibility

Problem: `Hecton_FluidAdvection.compute` has one `_AbyssalFlowCenter` uniform shared by the 3D texture path and structured-buffer fallback. If a valid texture override or texture publication uses a different center than the buffer payload, fallback buffer sampling can fetch the wrong grid cells.
Solution: When a texture path validates, compare its center against the active buffer center. If centers differ beyond a 1 cm squared tolerance, disable the structured-buffer fallback for that bind and use the zero buffer metadata. Same-origin published buffer+texture payloads still keep the buffer fallback.
Rejected Alternatives: Adding a second shader center uniform was rejected for this pass because the shared advection shader also serves silt/bubble/debris paths and that change would widen the contract. Blindly trusting authored texture overrides was rejected because VFX consumers must fail closed at the boundary.
Scalability potential: Low = flow disabled, no impact. Middle/High = valid texture gets correct center without wrong fallback fetches. Ultra = same-source fluid texture+buffer can still keep fallback richness when centers match.
Hardware Impact: CPU cost is a few scalar comparisons only on flow bind. Correctness impact is higher: it prevents wrong-cell buffer sampling that can produce visually false debris drift with no readback or extra GPU branch.

## Decision 16 - Render Basis Hot-Math and Draw Metadata Cache

Problem: The indirect debris shader built every chip basis with `sincos` in the vertex path, and the C# renderer queried mesh index count/start/base vertex in both dispatch and render paths during the same active frame.
Solution: Replace trigonometric yaw with a hash-vector basis that preserves deterministic chip orientation and uses existing safe normalization. Cache mesh draw metadata once per frame in `TryResolveDrawMesh`, then feed the cached draw args into compute and render with the same mesh validation result.
Rejected Alternatives: Per-particle CPU rotation upload was rejected because it adds bandwidth and persistent state. Keeping `sincos` was rejected because rock chips do not need exact angular truth. Permanent mesh metadata caching was rejected because authored mesh data can change in Editor or at runtime; once-per-frame cache is safer.
Scalability potential: Low/MX350 = cheaper vertex ALU and one mesh metadata query per active frame. Middle/High/Ultra = saved ALU buys denser visible chips and stronger CoreLit/caustic shading inside the same fixed storage cap.
Hardware Impact: CPU saving is estimated sub-10 us on active frames from avoiding duplicate mesh index queries. GPU saving depends on visible count; replacing one `sincos` per debris vertex removes expensive transcendental ALU on MX350 while preserving deterministic per-chip variation.

## Decision 17 - Velocity-Driven High-Tier Fresh Edge Response

Problem: After removing vertex trig, the high/ultra path had saved ALU but no new visible payoff. Fast SDF chips should read as fresh fracture impact without adding CPU color uploads or new particle state.
Solution: Bind the existing `CarveDebrisVelocity` ping-pong lane to the indirect material and set `_CarveDebrisMaterialParams.w` as a cached non-low-tier visual flag. The shader reads velocity only on non-low tiers and blends more fresh-edge tint for fast, still-alive chips.
Rejected Alternatives: Per-particle CPU color upload was rejected because it adds bandwidth and duplicates GPU state. Always reading velocity on Low/MX350 was rejected because the low path should spend saved cycles on stability, not extra shading. Adding a new material variant was rejected because it increases variant pressure for a tiny branch.
Scalability potential: Low/MX350 = branch disabled, same cheap rock silhouette. Middle/High/Ultra = impact-speed edge highlight gives stronger carve readability while staying inside fixed 4096 storage.
Hardware Impact: Low-tier cost is unchanged except one buffer bind on the CPU render path. High/ultra cost is one velocity buffer read per visible vertex and a few scalar ops; no GC, no readback, no extra CPU upload.

## Decision 18 - No-Dotnet Static Verification Closeout

Problem: The user explicitly prohibited dotnet rebuilds, Unity MCP tools are unavailable in this session, and the live project cannot be truthfully reported as Unity-compiled from static file access alone.
Solution: Treat static evidence as the closeout boundary: run `git diff --check`, scan the touched VFX lane for forbidden managed/runtime patterns, scan debris/flow shaders for hot expensive math regressions, confirm velocity binding and draw-metadata cache call sites, and record the missing current batch prompt tag count. Keep compile status blocked instead of inventing a pass.
Rejected Alternatives: Running `dotnet build` or `dotnet rebuild` was rejected by direct user instruction. CPU `GetData` validation was rejected because it stalls GPU work and violates the debris architecture. Claiming Unity compile success without editor/MCP evidence was rejected because it is a false report.
Scalability potential: Low/MX350 retains the 1024 active slot cap, no SDF sample, no shader velocity branch, and no hot scheduler fences. Middle/High/Ultra retain 4096 active capacity, validated flow/SDF binding, hash-vector chip orientation, CoreLit response, and velocity fresh-edge impact readability without new CPU uploads.
Hardware Impact: Verification itself saves 0 us at runtime. Preserved runtime estimates remain 25-35 us low-tier dispatch reduction, 20-70 us burst scheduler-fence removal, 15-60 us dense-batch scan reduction, sub-10 us duplicate mesh metadata avoidance, and visible-count-dependent shader ALU savings from removing per-vertex `sincos`.

## Decision 19 - Startup Retry, Bounded Signal Fairness, and No-Wake Fast Path

Problem: Three residual weaknesses remained after the last pass: an `OnEnable()` that runs before `GlobalRegistry`/DataVault readiness can leave the renderer unregistered forever, a raw 32-signal cap can let invalid/non-subtract carve packets starve valid subtract packets later in the same frame, and `ApplyDynamicWakes` still enters an eight-slot unrolled loop even when no wake payload is active. The debris shader also recomputed a hash already available during basis construction and normalized an up vector derived from two unit perpendicular vectors.
Solution: Retry registration/GPU readiness once from `Start()` without adding an Update polling loop; scan up to 64 raw carve signals while keeping the 32 valid-request emission cap; early-return from `ApplyDynamicWakes` when slot count is zero; reuse the orientation z-hash for edge jitter and assign `upWS = cross(forwardWS, rightWS)` directly.
Rejected Alternatives: Runtime polling was rejected because it adds a permanent hot-path branch. Unbounded signal scans were rejected because burst events must stay predictable. Binding real dynamic wake buffers by widening `HectonFluidEngine` public contracts was rejected for this pass because the existing publisher does not expose a narrow read-only wake payload. CPU rotation/color uploads were rejected because shader fakes are cheaper.
Scalability potential: Low/MX350 benefits most: no wake slot loop when wake slots are zero, 1024 active capacity stays intact, and startup retry prevents missing the whole VFX system after boot order jitter. Middle/High/Ultra retain richer flow/SDF behavior and velocity edge response, with lower vertex ALU per visible chip.
Hardware Impact: Startup retry has no steady-frame cost. The signal scan remains capped at 64 raw packets and 32 valid emissions. The no-wake fast path removes 8 wake-slot branch iterations per live particle on frames with `_DynamicWakeParams.x == 0`, which is the current carve debris bind. Shader basis change saves one hash and one safe-normalize/rsqrt per visible chip vertex.
