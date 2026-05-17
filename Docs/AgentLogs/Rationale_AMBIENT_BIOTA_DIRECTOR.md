# Rationale_AMBIENT_BIOTA_DIRECTOR

## Decision 1: Stop Before Code Because Prompt Is Missing

Problem: The launcher assigned `AMBIENT_BIOTA_DIRECTOR`, but `Docs/Tasks/CURRENT_BATCH.md` has no matching XML tag. Without the tag, there is no authoritative task count, phase list, domain boundary, or polish mandate.

Solution: Treat this as `[BLOCKED BY DEPENDENCY]`. Use CLI evidence from the batch file and the existing audit, then record status instead of fabricating work.

Rejected Alternatives: Implement a generic background biota pool from the one-line launcher text. That violates the batch protocol and risks cross-domain architecture damage. Reusing `ECOSYSTEM_POPULATION_BALANCER` or `ECOSYSTEM_MIGRATION_LINK` would also be wrong because those prompts have different owners and task lists.

Scalability potential: No runtime code was added. Intended future shape, once prompt exists: Low = fixed pooled ambient slots and deterministic spawn rings; Middle = spatial hash residency and modest species variation; High = richer behavior buckets; Ultra = visual overkill through GPU/animation variation without changing gameplay authority.

Hardware Impact: 0 us saved or spent at runtime because no system was modified. For i3/MX350, avoiding unauthorized spawn logic prevents unbudgeted pool growth, GC pressure, and unmanaged memory drift.

## Decision 2: Mandate Selection For Future Background Biota Work

Problem: The requested theme, "background biota spawning and pooling," touches AI scheduling, pool residency, deterministic spawn selection, signal transport, and telemetry.

Solution: Read the relevant mandates before any implementation: AI Director, Boids/Spatial Hash, Global Registry, Signal Lane Segregation, Deterministic RNG, Zero GC, Crash Telemetry, and Persistent Object Registry.

Rejected Alternatives: Treat ambient biota as decorative MonoBehaviours with `Instantiate`, `Destroy`, `Random.Range`, scene searches, or per-frame registry polling. These are explicitly forbidden and would fail MX350 frame budget requirements.

Scalability potential: Low = prewarmed slots and Math LOD spawn culling; Middle = bounded active set with deterministic weighted species tables; High = richer sensor reactions; Ultra = additional visual animation and density where frame budget allows.

Hardware Impact: The selected mandate set targets 0 B/frame GC, O(1) pool lookup, deterministic table selection, and fixed-size telemetry. Estimated future gain versus naive Unity instantiation is spike elimination rather than a stable per-frame microsecond number.

## Decision 3: Replace Per-Entity Ambient Life With SOA Service

Problem: Phase 1 requires eliminating ambient life instantiation loops and moving background biota toward a GPU-resident stream without inventing dependencies on other active agents.

Solution: Added `IAmbientBiotaService` to `GlobalRegistryContracts`, registered `GlobalRegistry.AmbientBiota`, and implemented `AmbientBiotaDirector` under `Assets/_Project/Scripts/AI/Ambient/`. The service exposes read-only AUP, velocity, and state arrays for later render/VFX consumers.

Rejected Alternatives: Editing `World/SargassumMicroFaunaBoids` was rejected because the prompt write domain is `AI/Ambient` and that file belongs to World. `ObjectPoolManager` was rejected because pooled GameObjects still require transform/component churn and do not provide a GPU-friendly SOA stream.

Scalability potential: Low = 2048 billboard-class slots, 1/16 drift buckets, 64 spawn attempts per slow tick. Middle = same math with larger active biomass target. High = 8192 slots with richer species variation. Ultra = future indirect draw and overkill visual variation fed from the same arrays.

Hardware Impact: Estimated i3/MX350 gain versus naive spawn bursts is 2000-8000 us avoided during 64-object bursts and 0 B/frame managed allocation. Steady-state drift budget target is 8-30 us/frame on low tier before renderer integration.

## Decision 4: DataVault Ownership For Ambient Biota

Problem: Ambient biota must be visible to GPU/VFX consumers and survivable across context compression without private unmanaged memory drifting outside central accounting.

Solution: Reserved `SystemID.AmbientBiota` plus `BufferID.BiotaAUPs`, `BufferID.BiotaVelocities`, and `BufferID.BiotaStates`; `AmbientBiotaDirector` requests all three buffers from `GlobalDataVault`.

Rejected Alternatives: Allocating `NativeArray<T>` directly in the director was rejected because it bypasses existing vault ownership, aliasing, and leak telemetry. Managed `List<T>`/arrays were rejected due GC and non-Burst access.

Scalability potential: Low/Middle/High/Ultra all share the same SOA contract; only capacity and later render path change. Cheap devices keep a small active set, high-end devices spend saved CPU on visual overkill.

Hardware Impact: Estimated 50-150 us cold-path resize/allocation bursts avoided after first allocation. Runtime managed allocation target remains 0 B/frame.

## Decision 5: Deterministic Spawn And Drift Jobs

Problem: Background life needs motion and replenishment without per-entity MonoBehaviours, random sources, or frame-wide scans.

Solution: Implemented Burst `AmbientBiotaSpawnJob` for dead-slot activation and `AmbientBiotaDriftJob` for bucketed Brownian plus abyssal-flow advection. Hash-based deterministic noise replaces `Random.Range`; slow tick handles biomass targeting; late-frame completion keeps job sync out of the main tick body.

Rejected Alternatives: Physics bodies, steering components, and coroutine spawners were rejected. They are visually unnecessary for background biota and cost too much on low-end silicon.

Scalability potential: Low = billboard flag and tight radius under stress. Middle = larger target active count. High = 8192-slot drift. Ultra = future richer flow/noise inputs and reactive VFX without changing service ownership.

Hardware Impact: Estimated drift cost is 8-30 us/frame on low tier and 40-90 us/frame on high tier before GPU draw. Object-based movement would likely cost 300-1200 us/frame for comparable visible density.

## Decision 6: Compile Wall Classification

Problem: Compile verification is mandatory after tasks 1-5, but `Hecton8.Core.csproj` currently fails on unrelated dependency and generated-project breakage outside `AI/Ambient`.

Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` and classified the result as `[BLOCKED BY DEPENDENCY]`. Representative errors: missing `JobAdmissionLane` assembly references, missing `HectonShaderGlobalDataVaultBridge`, missing signal types in `GlobalSignals`, missing voxel-debris constants, and stale generated project contents. The new ambient asmdef was checked for direct references and now names `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory`.

Rejected Alternatives: Editing generated `.csproj` files or foreign systems to force a local build was rejected. That would cross task boundaries and risk overwriting other agents' active work.

Scalability potential: No runtime scalability change. Build health must be restored by the owning integrator/agents before later phases can be objectively validated.

Hardware Impact: 0 us runtime effect. The risk is validation debt, not frame time.

## Decision 7: Replace Director-Owned NativeArrays With Vault Handles

Problem: The director still had private `NativeArray` fields and a local macro-hydration counter allocation. That violates Data Sovereignty and hides ambient storage from central alias/leak telemetry.

Solution: Replaced private arrays with `VaultBufferHandle<T>` for AUPs, velocities, states, macro hydration counters, telemetry ring, and telemetry cursor. The director resolves transient NativeArray views only when scheduling Burst jobs or publishing read-only aliases.

Rejected Alternatives: Keeping `H8Memory.Allocate<int>` for four macro counters was rejected because even tiny private persistent allocations bypass the vault ownership model. Managed arrays and `List<T>` were rejected due GC and non-Burst access.

Scalability potential: Low = 2048 vault slots with ring spawn and triangle noise. Middle = same SOA contract with higher active scalar. High = 8192 slots with headlight avoidance and richer emission. Ultra = doubled high-tier capacity ceiling and wider visual bubble when stress is low.

Hardware Impact: Estimated 50-150 us cold allocation churn remains avoided after vault residency. Runtime managed allocation target remains 0 B/frame. Vault handle resolution adds a small guarded pointer check but centralizes leak and stale-handle failure.

## Decision 8: AUP-First Drift And NaN Vaccination

Problem: Background biota at depth cannot use float-only distance gates. NaN velocity or rsqrt failure would also poison the GPU presentation stream on mobile.

Solution: Converted spawn/drift/dehydrate boundary math to `double3` AUP deltas and squared distance checks. Guarded dt, velocities, target velocities, AUP offsets, age, `math.rsqrt`, and distance-square tests. Sanitized faults are marked in `AmbientBiotaState.Reserved`.

Rejected Alternatives: `Vector3.Distance`, `math.length`, raw `normalize`, and unguarded velocity integration were rejected. They are either slower, less deterministic, or unsafe under corrupted payloads.

Scalability potential: Low = triangle-noise fake and skip on invalid dt. Middle = deterministic Brownian plus flow. High = safe headlight flee vector. Ultra = larger reactive bubble without changing data layout.

Hardware Impact: Estimated 1-5 us/frame guard overhead on the active bucket. This buys fault containment and prevents mobile GPU pipeline collapse from one invalid float.

## Decision 9: Reuse Existing Typed Signal Lanes

Problem: Reactive ambient death and macro hydration needed downstream notifications without inventing duplicate signal types.

Solution: Reused existing `DebrisSpawnSignal` for organic scrap and existing `EntitySpawnSignal` for hydrated macro swarms. Both are published through `GlobalSignals.Publish(in signal)` and therefore through typed `SignalBus<T>` lanes.

Rejected Alternatives: A new `AmbientBiotaExpiredSignal` was rejected because it duplicates debris/VFX semantics and dilutes signal density. GameObject particle instantiation was rejected because it reintroduces the spawn churn Phase 1 removed.

Scalability potential: Low = bounded 16 debris signals per late frame with small quantities. High/Ultra = higher organic shard quantity and emission without widening the CPU dispatch surface.

Hardware Impact: Bounded signal drain avoids unbounded late-frame spikes. Expected cost remains signal-lane enqueue level, not per-object VFX ownership.

## Decision 10: Fixed Blackbox Ring In GlobalDataVault

Problem: Ambient biota had no last-300-frame diagnostic state and would produce "unknown crash" failures if NaNs or stale handles appeared.

Solution: Added vault-owned `BiotaTelemetryRing` and `BiotaTelemetryCursor` usage. Each late frame writes center AUP, frame index, active count, cull count, capacity, state hash, and flags. On sanitized-fault detection, the ring dumps to `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR.bin`.

Rejected Alternatives: Managed log append per frame and exception-only reporting were rejected. Logs allocate and are not reliable after crash; exceptions only report the final symptom.

Scalability potential: Low/Middle/High/Ultra share the same 64 B telemetry entry and 300-entry ring. No tier gets a blind crash path.

Hardware Impact: One fixed 64 B write per late frame, estimated under 2 us. Fault dump is cold path only.

## Decision 11: Compile Wall After Loop 3

Problem: Validation after tasks 6-15 is blocked by current project-wide errors outside `AI/Ambient`.

Solution: Ran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /p:OutputPath=.codexbuild\ambient_validation_core\` and recorded the output to `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`. Representative blockers are `World/SargassumMicroFaunaBoids.cs` missing `ResolveVaultBuffer` and `_leviathanNode*Native`, plus `RepairTool.cs` unassigned `localPoint`.

Rejected Alternatives: Editing World/Sargassum or RepairTool was rejected because those files are outside the authoritative `AI/Ambient` domain and are active cross-agent territory. Faking green validation was rejected.

Scalability potential: No runtime scalability change. Remaining ambient tasks 16 and 17 depend on render/biome binding, not on these foreign compile errors.

Hardware Impact: 0 us runtime effect. The impact is validation blockage until owning domains repair their compile errors.

## Decision 12: Indirect Draw Without CPU Matrix Construction

Problem: Ambient biota needs a GPU presentation path. CPU-side matrix building for thousands of quads would erase the benefit of SOA spawning and drift.

Solution: Added an optional `Graphics.RenderMeshIndirect` path that uploads the vault AUP, velocity, and state buffers into persistent `GraphicsBuffer` lanes and renders one indirect quad mesh. Velocity is bound as the motion-vector source; inactive slots remain state-gated for shader-side discard. The path is dormant when no material is assigned.

Rejected Alternatives: Per-slot `Matrix4x4` construction, `Graphics.DrawMeshInstanced` batches, pooled GameObject quads, or transform hierarchies were rejected because they move work back to the CPU.

Scalability potential: Low = simple billboard material reading state/velocity. Middle = richer species material variants from state. High = shader-side biolume panic and organic density. Ultra = material can add volumetric silt/salt/surface effects from the same buffers without changing CPU ownership.

Hardware Impact: Expected to replace per-instance matrix setup with three bulk buffer uploads plus one indirect draw. Profiler numbers are blocked by foreign compile errors, so no microsecond claim beyond engineering estimate is recorded.

## Decision 13: Biome Sync Through Existing Typed Lane

Problem: Task 17 asks for biome-dependent visual identity, but the vault currently has no clear authoritative current-biome `BufferID` for ambient biota to read.

Solution: Used existing `SignalBus<BiomeChangedSignal>.GetFrameSnapshot()` as a `ReadOnlySpan<BiomeChangedSignal>` slow-path bridge. The latest current biome hash is folded into spawn hash, species selection, and emission bias. No duplicate ambient biome signal was created.

Rejected Alternatives: Directly depending on `BiomeMatrixDirector.ActiveRuntimeInstance` was rejected because it couples ambient AI to a world owner. Inventing `AmbientBiomeChangedSignal` was rejected as duplicate signal debt. Adding a new vault buffer without a producer was rejected as fake data sovereignty.

Scalability potential: Low = deterministic family tint/emission from biome hash. High/Ultra = material can interpret the same hash for colorful shallow plankton versus pale abyssal jellyfish without more CPU state.

Hardware Impact: Reads a typed signal snapshot only on slow-path/biome refresh. Expected cost is sub-1 us when no transition signals exist; no managed allocation is introduced.

## Decision 14: Reserved Field Must Stay Flags-Only

Problem: Macro hydration stored `swarm.HashId` in `AmbientBiotaState.Reserved` while later code used `Reserved` for pending debris and sanitized fault flags. Random swarm hash bits could trigger false debris/fault behavior.

Solution: Stopped storing arbitrary hashes in `Reserved`; active state now writes `Reserved = 0u` and keeps identity in `StableHash`/`SpeciesId`.

Rejected Alternatives: Bit-packing swarm hash and flags into one 32-bit field was rejected because task pressure is stability, not extra debug metadata. A new state field would break the fixed 32 B ABI.

Scalability potential: All tiers benefit from deterministic flags and unchanged state size.

Hardware Impact: 0 us runtime cost. Prevents false VFX emission and false blackbox fault dumps.

## Decision 15: Omega Branchless Advection Kernel

Problem: The Omega mandate required removing `if` branches from the advection kernel and using `math.select`. The previous drift job was safe, but it still used early returns and conditional branches for activity, bucket, NaN, high-tier light avoidance, and retirement.

Solution: Reworked `AmbientBiotaDriftJob.Execute` into a mask-driven kernel. Active/bucket/delta-time eligibility now forms `shouldSimulate`; velocity, target velocity, light avoidance, expiry, outside-radius cull, NaN sanitation, AUP selection, and state writes are selected with `math.select` plus fixed `SelectAup`, `SelectState`, and `SelectDouble3` helpers. `ClampMagnitude` and `SafeNormalize` were also changed to branchless finite guards.

Rejected Alternatives: Keeping early returns was faster for inactive slots but violates the explicit Omega polish text. Splitting the job into compacted active queues was rejected for this pass because it would add another write surface and require producer/consumer validation outside the ambient domain.

Scalability potential: Low = same 1/16 modulo bucket and triangle-noise billboard path, but branch prediction noise is removed. Middle = stable deterministic flow advection. High = light avoidance stays visible through the same mask path. Ultra = richer material response can trust finite state/velocity payloads without CPU branching changes.

Hardware Impact: Exact measured microseconds are unavailable because Unity Profiler/GCMonitor was not run. Engineering tradeoff: inactive slots now execute simple masked arithmetic instead of returning early, but branch divergence in the Burst lane is reduced and the 1/16 bucket still limits meaningful state mutation.

## Decision 16: Final Compile Green, Runtime Proof Still Pending

Problem: Task 18 required `dotnet build` exit 0. Earlier attempts were blocked by non-ambient compile errors in World and RepairTool.

Solution: After the Omega kernel pass, reran `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`. The build succeeded with 0 warnings and 0 errors; output was recorded to `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.

Rejected Alternatives: Claiming Unity runtime readiness from `dotnet build` was rejected. AGENTS.md explicitly requires Unity import/Console/Play Mode/profiler/GCMonitor/player proof for runtime readiness.

Scalability potential: Low/Middle/High/Ultra code paths now compile through the C# gate. Runtime tier behavior still needs Unity-side visual and GPU profiling before "shipping ready" can be asserted.

Hardware Impact: Compile success has 0 us direct runtime effect. It removes the validation wall; measured CPU/GPU/GC deltas remain pending until Unity profiling is executed.

## Decision 17: Double-Buffered Locked GPU Uploads

Problem: The indirect draw path used `GraphicsBuffer.SetData` for AUPs, velocities, states, and args. That compiled, but it violated the project bandwidth rule requiring `LockBufferForWrite`, and it pushed full-capacity uploads even when the SOA payload had not changed.

Solution: Replaced the single GPU payload lanes with double-buffered A/B GraphicsBuffers. Payload upload now writes to the non-current buffer with `LockBufferForWrite`, copies through `UnsafeMemoryCopyGuard`, and swaps the read index only after all three SOA streams copy successfully. The indirect args buffer now uses `LockBufferForWrite` and is refreshed only when mesh or capacity changes. The ambient asmdef now allows unsafe code because the copy path uses Unity's native pointer APIs.

Rejected Alternatives: Keeping `SetData` was rejected because it directly violates the current bandwidth mandate. Uploading only active compacted slots was rejected for this pass because the shader/state contract is capacity-indexed and compaction would require another indirection buffer and new shader agreement.

Scalability potential: Low = avoids unnecessary PCIe/upload churn when no job changed the payload. Middle = keeps stable double-buffered presentation. High/Ultra = retains dense indirect draw and richer material interpretation without CPU matrix generation.

Hardware Impact: Exact microseconds remain unmeasured. Expected gain is lower upload overhead and reduced GPU/CPU synchronization risk versus full `SetData` every late frame; no file I/O is added, so Steam Deck microSD pressure remains 0 B/frame in this domain.

## Decision 18: Current Compile Warning Is Ecosystem Integration

Problem: After the bandwidth pass, an initial `dotnet build` failed in a foreign ecosystem namespace. A later Loop 6 run exited 0, but still reported one non-ambient warning.

Solution: Reran the required build during Loop 6 and recorded output to `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`. That result succeeded with 1 warning and 0 errors. The warning was `CS2002`: `Assets/_Project/Scripts/AI/Ecosystem/EcosystemPopulationBalancer.cs` is specified multiple times. Loop 7 supersedes this with the current foreign dependency wall.

Rejected Alternatives: Editing `Directory.Build.targets`, `Hecton8.Core.csproj`, or ecosystem files from the ambient agent was rejected because that crosses the authoritative `Assets/_Project/Scripts/AI/Ambient/` domain. Claiming 0 warnings was rejected because the build log has one foreign warning.

Scalability potential: No runtime scalability change. This is an integration/build graph ownership issue.

Hardware Impact: 0 us runtime effect. This was a historical Loop 6 build result; Unity runtime/profiler proof remains pending.

## Decision 19: Treat Unity Bee Compile As The Real Boundary

Problem: The generated `Hecton8.Core.csproj` can report a different result than the Unity asmdef graph. The ambient source now uses unsafe locked GPU uploads, so the actual `Hecton8.AI.Ambient` Bee response file must be checked instead of relying on a stale green core-project build.

Solution: Ran direct Roslyn validation against `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Ambient.rsp`. The first blocker is a missing `Hecton8.Core.ref.dll`. Attempted to rebuild the Unity dependency chain; `Hecton8.Core.Bucketing`, `Hecton8.Core.Scheduling`, and `Hecton8.Audio.Virtualization` fail outside the ambient domain. A surrogate ambient compile was also run with the fresh generated `Temp/bin/Debug/Hecton8.Core.dll`; that harness only exposes the expected missing `ISimulationBucketer` after contract refs are stripped to avoid duplicate generated-project types.

Rejected Alternatives: Claiming the ambient asmdef compiles from `dotnet build Hecton8.Core.csproj` was rejected because the project file is not the Unity assembly boundary. Editing Bucketing, Scheduling, Audio, World, or Diagnostics from this agent was rejected because those are outside `Assets/_Project/Scripts/AI/Ambient/` and the task's 3-strike protocol requires dependency-wall reporting instead of cross-domain damage.

Scalability potential: No runtime scalability change from validation itself. The ambient source remains tiered: Low = 2048 billboard-style slots, triangle noise, 30 m stress radius; Middle = broader AUP drift with same SOA contract; High = headlight avoidance and panic emission; Ultra = larger capacity and richer material interpretation fed by the same GPU buffers.

Hardware Impact: 0 us measured because validation does not run gameplay. The compile-wall evidence prevents false microsecond claims; Unity Editor profiling, GCMonitor, and GPU capture are still required for real CPU/GPU timings.

## Decision 20: Double Buffer Indirect Args Too

Problem: The payload GPU lanes were double-buffered, but indirect draw args were still a single locked buffer. The args payload is tiny, but it is still GPU data and should not be read by `RenderMeshIndirect` while a write mapping is being refreshed.

Solution: Replaced the single indirect-args buffer with A/B `GraphicsBuffer` lanes. `UploadIndirectArgs` now writes to the non-current args buffer with `LockBufferForWrite`, swaps the read index only after the struct is written, and passes the resolved read buffer into `Graphics.RenderMeshIndirect`.

Rejected Alternatives: Keeping the single args buffer was rejected because it leaves a synchronization edge inconsistent with the AUP/velocity/state upload path. Rebuilding CPU matrices was rejected again because it violates the indirect draw objective.

Scalability potential: Low = one locked args write only when mesh/capacity changes; Middle/High/Ultra = same draw contract with denser GPU interpretation and no CPU matrix path. High-tier visual overkill stays shader/material-side rather than adding new CPU object ownership.

Hardware Impact: Exact microseconds remain unmeasured. Expected runtime gain is synchronization-risk reduction rather than frame-time magnitude; args writes are cold path on mesh/capacity changes and add 0 B/frame managed allocation.

## Decision 21: Remove Registry Polling From Per-Frame Ambient Tick

Problem: `TryCapturePlayerPose`, biomass refresh, and abyssal-flow refresh had fallback `GlobalRegistry` lookups when cached dependencies were null. If a dependency was missing during boot, the ambient `Tick` path could poll the registry every frame, violating the Signal Lane Segregation mandate against frame polling for state changes.

Solution: Added `RefreshRegistryDependencies()` and moved missing-dependency recovery into cold/slow execution. `Tick(float deltaTime)` now uses cached service references only; a static slice audit confirms no `GlobalRegistry.` access inside the tick block.

Rejected Alternatives: Keeping lazy per-call lookups was rejected because it hides boot-order problems in the hot path. Adding direct hard dependencies to foreign systems was rejected because the ambient director must remain decoupled and use registry/service contracts only.

Scalability potential: Low/MX350 avoids repeated registry probes when player/ecology/flow services are late. Middle/High/Ultra keep the same service contract while allowing richer material behavior from cached tier/flow state.

Hardware Impact: Exact microseconds are unmeasured. Expected gain is small but deterministic: removes possible per-frame service-locator probes under missing-dependency conditions; no managed allocation is introduced.

## Decision 22: Keep GPU Stream And Blackbox Honest After Non-Job Mutations

Problem: Macro hydration/dehydration jobs mutate the DataVault SOA buffers synchronously, but the renderer dirty flag was not set afterward. Also, blackbox telemetry used the simulation seed frame index instead of a heartbeat index, and `CullRatePerSecond` stored raw cull count rather than a per-second rate.

Solution: Set `_gpuPayloadDirty = true` after successful macro hydrate/dehydrate recounts. Added a dedicated heartbeat frame counter for telemetry and computed cull rate from finite elapsed unscaled time between recounts. High-tier panic state now clears/reacts branchlessly instead of leaving permanent `FlagHighTierReactive` and permanent emission. Added material parameters for quality profile, stress, flow vector, and overkill mode so shader-side visual scaling can respond without CPU matrices.

Rejected Alternatives: Waiting for the next drift/spawn job to refresh GPU buffers was rejected because it can draw stale macro swarm state. Leaving panic as a permanent flag was rejected because it turns a momentary light reaction into polluted persistent state. Recording raw cull count as a rate was rejected because it lies to the blackbox.

Scalability potential: Low = dirty uploads only when data changes and shader can stay in billboard fake mode. Middle = accurate blackbox and stable emission decay. High/Ultra = shader gets flow/stress/overkill knobs for richer silt and biolume without extra CPU ownership.

Hardware Impact: Exact microseconds remain unmeasured. Dirty-flag repair prevents stale GPU data rather than saving time; heartbeat/cull-rate math is scalar and expected below 1 us per recount. Shader parameter writes add a few scalar/vector material updates only on the existing render path.

## Decision 23: Do Not Rebind Vault Handles From Hot Resolve Paths

Problem: `TryResolveBiotaBuffers`, `TryResolveMacroCounters`, and `TryResolveTelemetryBuffers` called `EnsureVaultBuffers()`. That meant per-frame `Tick`/`LateFrameTick` could request or rebind DataVault handles if capacity or handle state drifted, turning a resolve helper into a hidden structural allocation path.

Solution: Removed `EnsureVaultBuffers()` from every resolve helper. Resolve helpers now require `_vault` and existing handles, then return false if the cold/slow setup did not prepare them. Structural creation remains limited to `OnEnable` and `SlowTick`, and `SlowTick` returns before `EnsureVaultBuffers()` while `_jobPending` is true.

Rejected Alternatives: Leaving lazy hot-path ensure was rejected because it hides boot-order and capacity-change work inside frame cadence. Calling `CompleteActiveJob()` before rebinding was rejected because mid-frame synchronization is the Native Memory Jobs failure mode. Allocating local fallback `NativeArray` storage was rejected because it violates DataVault sovereignty.

Scalability potential: Low = no surprise vault work during frame cadence on i3/MX350. Middle = stable handles while gameplay runs. High = capacity can still expand on cold/slow cadence. Ultra = larger visual density remains possible without job-handle rebinding under a live writer.

Hardware Impact: Exact microseconds are unmeasured. Expected gain is removal of rare but severe hidden structural stalls from hot resolve paths; normal-frame cost remains simple handle validation and vault view resolution.

## Decision 24: Cache System Stress For Ambient Runtime Cadence

Problem: Radius resolution and indirect material parameters read `GlobalSignals.SystemStress01` from frame cadence. The value is a global scalar, but the ambient director already has a slow quality-policy refresh point; polling the global each frame is unnecessary for a slow homeostasis control.

Solution: Added `_cachedSystemStress01`, finite-clamped in `RefreshQualityPolicy()`. `ResolveSimulationRadiusMeters()` and indirect material writes use the cached value. A static slice audit now confirms `Tick(float deltaTime)` has no `GlobalRegistry.`, no `EnsureVaultBuffers()`, and no `GlobalSignals.SystemStress01`.

Rejected Alternatives: Keeping direct frame reads was rejected because stress homeostasis does not require per-frame precision. Creating a new stress signal was rejected because `GlobalSignals.SystemStress01` already exists and no duplicate lane should be invented. Reading stress inside the Burst jobs was rejected because it would expand job payload churn for a scalar already captured by policy.

Scalability potential: Low = stress radius clamp updates on slow cadence without frame polling. Middle = stable radius between policy updates. High/Ultra = low-stress overkill radius still expands through the same cached policy, leaving shader-side visual overkill available without more CPU simulation truth.

Hardware Impact: Exact microseconds remain unmeasured. Expected gain is tiny in steady state, but it removes another frame-cadence global read and makes the stress/radius policy deterministic for a tick group.

## Decision 25: Current Compile Wall Is Physics Domain

Problem: After Loop 9, validation is blocked by a fresh non-ambient compile wall in `Assets/_Project/Scripts/PhysicsApplySystem.cs`: missing force-packet queue fields/helpers and missing `BufferID.PhysicsForce*` entries.

Solution: Recorded the failure in `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt` and did not patch the physics domain. Direct ambient Bee validation is also still blocked before source compile by missing `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll`.

Rejected Alternatives: Editing `PhysicsApplySystem.cs`, `H8Memory.cs`, or shared physics buffer IDs from the ambient biota agent was rejected because it crosses the authoritative `Assets/_Project/Scripts/AI/Ambient/` domain. Reporting green compile was rejected because objective logs say otherwise.

Scalability potential: No runtime scalability change. Low/Middle/High/Ultra ambient paths remain statically clean; Unity runtime and GPU visual proof remain pending until foreign compile walls are repaired.

Hardware Impact: 0 us measured. Compile blockers do not change runtime cost; they block profiler, GCMonitor, Frame Debugger, and player-build evidence.

## Decision 26: Shader ABI Must Not Read Raw AUP

Problem: The indirect renderer uploaded `AbsoluteUniversePosition` directly to GPU. That struct is correct CPU authority, but it contains 64-bit grid fields and no shader consumer existed. Reading that raw layout in HLSL would be poor Metal/Quest/Android ABI practice and would force every shader to know AUP internals.

Solution: Added a packed 64 B `AmbientBiotaGpuInstance` render payload. The CPU derives camera-local `float3` meters from DataVault AUP truth, copies finite velocity/emission/state flags into a float/uint GPU struct, and binds one `_HectonBiotaInstances` buffer. AUP, velocity, and state remain DataVault-owned authority arrays; the packed buffer is presentation upload only.

Rejected Alternatives: Keeping raw AUP buffers was rejected because shader-side int64/grid handling is not portable enough for Metal/mobile and had no material consumer. Adding another DataVault render buffer was rejected because this is a transient GPU upload mirror, not simulation truth. CPU matrix building was rejected again because it violates the indirect draw objective.

Scalability potential: Low = shader reads one compact packet and draws cheap billboards with triangle fakery. Middle = stable motion vectors/emission from the same packet. High/Ultra = same packet drives SSS, procedural parallax, salt glints, and silt without additional CPU object ownership.

Hardware Impact: Exact microseconds remain unmeasured. Expected bandwidth reduction is from 96 B/slot raw multi-stream upload to one 64 B/slot packed render stream, plus fewer `SetBuffer` bindings. Runtime file I/O remains 0 B/frame.

## Decision 27: Domain-Local Ambient Biota Shader

Problem: C# exposed `_HectonBiota*` buffers and knobs, but the project had no shader that consumed them. That made `INDIRECT_DRAW_CALL` structurally present but visually incomplete.

Solution: Added `Assets/_Project/Scripts/AI/Ambient/Hecton_AmbientBiotaIndirect.shader` and its `.meta`. The shader consumes `_HectonBiotaInstances`, builds camera-facing quads, discards inactive slots, uses cheap low-tier triangle/pulse math, and gates high-tier 16-step procedural parallax, SSS rim light, volumetric-silt fake, and salt-glint fake behind `_HectonBiotaOverkill01`.

Rejected Alternatives: Editing global art shaders outside the ambient domain was rejected because ownership belongs to rendering/art domains. Runtime material creation was rejected because project rules forbid hidden material clones; the shader exists for an assigned material asset/scene binding. Compute simulation was rejected because the current objective is indirect presentation and the shader has no compute thread-group risk.

Scalability potential: Low = no texture samples and no collision; simple translucent billboards. Middle = biome tint and emission. High = reactive biolume and SSS. Ultra = procedural parallax/salt/silt overkill without changing the CPU simulation layout.

Hardware Impact: Exact microseconds are unmeasured. Static shader audit shows no int64, no RW buffers, no group barriers, no wave intrinsics, no texture samples, and no `numthreads`; Unity shader compiler validation is still pending because no Unity MCP/compiler endpoint is available.

## Decision 28: Ambient Must Not Claim Foreign SDF Truth

Problem: `AmbientBiotaDirector` directly called `HectonVoxelVolume.GetSDFDensity` and marked macro-hydrated biota/spawn signals with SDF emergence flags. That crossed from `AI/Ambient` into cave/voxel ownership and claimed an SDF proof that ambient does not own through a registry-facing service contract.

Solution: Removed the `Hecton8.Caves` import, removed the static voxel-volume density query, and replaced the guard with `ResolveMacroVisualQualityTier()`, which only clamps presentation quality from finite AUP and stress inputs already present at the ambient boundary. Also removed `FlagSdfEmergence` writes from ambient macro state and spawn signal payloads.

Rejected Alternatives: Adding a new ambient SDF interface or editing Core/World contracts was rejected because this loop is inside the ambient domain and the batch forbids public interface churn without a critical dependency justification. Keeping the direct static cave call was rejected because it couples ambient biota to a foreign owner and can rot under asmdef splits. Leaving the SDF flags while not querying SDF was rejected because it is a false telemetry/rendering semantic.

Scalability potential: Low = macro hydration still collapses to billboard visuals under high stress and finite checks. Middle = deterministic macro biota remains DataVault-owned without cave-query stalls. High/Ultra = visual overkill continues through shader overkill, larger scale, emission, flow, silt, salt glints, and parallax; actual SDF emergence can be restored later only through an owned typed service.

Hardware Impact: Exact microseconds are unmeasured. Expected low-end gain is removal of a cold macro-hydration cave-volume scan/query from the ambient service path and reduced cross-domain dependency risk. Runtime `Tick`/`LateFrameTick` cost is unchanged; Unity profiling remains pending.

## Decision 29: Current Compile Wall Is Contract Graph, Not Ambient

Problem: After the domain-boundary repair, validation still cannot prove a green Unity/Bee compile. The direct ambient response-file compile fails before source analysis because `Hecton8.Core.ref.dll` is absent. The generated `Hecton8.Core.csproj` fails in non-ambient contract references.

Solution: Recorded both logs. `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_ASMDEF_BUILD.txt` shows missing `Hecton8.Core.ref.dll`. `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt` shows 49 errors outside `AI/Ambient`: missing `HectonEcologyContract`, `ScalabilityContract`, `HectonPhysicsContract`, and `HectonSurvivalContract` references in ecosystem/core/physics/power/audio/PDA/world/modding files. No `AmbientBiotaDirector` or `AI/Ambient` error appears in the global build log.

Rejected Alternatives: Editing ecosystem, homeostasis, physics, power, audio, PDA, world, or modding contract references from the ambient agent was rejected because it crosses the authoritative domain. Reporting compile success was rejected because both objective logs exit 1. Claiming shader runtime success was rejected because Unity shader import/compiler proof is unavailable.

Scalability potential: No runtime scalability change. Low/Middle/High/Ultra ambient code paths remain statically clean and domain-contained; profiling and visual validation are blocked until the foreign contract graph is repaired.

Hardware Impact: 0 us measured. This is validation state, not runtime behavior. It blocks GCMonitor, Frame Debugger, RenderDoc, and player-build evidence.

## Decision 30: Shader-Side Normalize Must Be Explicitly Vaccinated

Problem: The ambient indirect shader had CPU-side finite packing, but shader-side flow parallax, camera axes, billboard axes, normal generation, and view-vector math still used raw `normalize()`. On mobile/Metal/Quest, a zero-length vector in a transparent indirect draw can turn into NaN payload and poison the visible GPU pipeline.

Solution: Added domain-local `SafeNormalize2` and `SafeNormalize3` helpers in `Hecton_AmbientBiotaIndirect.shader`. Replaced raw normalization for flow, camera right/up, drift right, billboard right, normal, and view direction with finite fallback vectors. Static audit now reports no `normalize(` call in the ambient shader.

Rejected Alternatives: Relying on CPU-side `BuildGpuInstance()` sanitation was rejected because camera matrices, flow constants, and view vectors are shader-local state. Removing high-tier parallax/SSS was rejected because it would downgrade the PC/Ultra path instead of making the math safe. Editing global shader includes was rejected because this fix belongs to the ambient shader domain.

Scalability potential: Low = safer billboard soup with no texture reads and no NaN collapse when flow/camera vectors degenerate. Middle = stable translucent biota under ordinary flow. High = keeps SSS, silt, salt glints, and panic biolume. Ultra = retains 16-step procedural parallax overkill without adding CPU simulation truth.

Hardware Impact: Exact microseconds are unmeasured. Expected low-end effect is crash/NaN risk reduction, not a claimed frame-time gain. The extra dot/rsqrt guards are shader ALU; no runtime file I/O, managed allocation, or CPU matrix work is added. Unity shader compiler/profiler proof remains pending because no Unity MCP/compiler endpoint is exposed.

## Decision 31: Biome Sync Cannot Pretend A Vault Buffer Exists

Problem: The assignment asks to read `BiomeID` from the vault, but a fresh search found no authoritative current-biome DataVault `BufferID`/service contract in the reachable Core/World/AI sources. World biome systems keep private native maps and scatter-local hashes, while Core exposes an existing typed `BiomeChangedSignal` lane.

Solution: Kept ambient on `SignalBus<BiomeChangedSignal>.GetFrameSnapshot()` and folded `CurrentBiomeHash` into species/emission selection. No duplicate signal, direct `GPUScatterDirector`, direct `BiomeBoundarySdfRuntime`, or private native map dependency was introduced.

Rejected Alternatives: Adding a new `BufferID.CurrentBiome` or new registry interface from the ambient agent was rejected because it crosses shared contract/world ownership and would require integration coordination. Reading world biome private arrays was rejected because it violates Data Sovereignty and the domain boundary. Claiming vault biome read success was rejected because the objective source scan does not support it.

Scalability potential: Low = O(signal count) slow/cold biome update and cheap tint/species bias. Middle = stable biome hash between transitions. High/Ultra = shader still uses biome hash to bias rich biolume/parallax/silt color without extra CPU spatial queries.

Hardware Impact: Exact microseconds are unmeasured. Expected low-end cost remains typically sub-1 us when no biome transition signals are present; no MicroSD I/O and no persistent local NativeArray ownership are added.

## Decision 32: Close Editor Metadata Debt And Shader Rsqrt Edge Cases

Problem: The ambient runtime was statically clean, but the owning MonoBehaviour still had serialized fields without explicit inspector tooltips/headers, and the shader safe-normalize helpers still passed raw `lengthSq` into `rsqrt`. HLSL ternaries/selects are not a hard guarantee that the unused expression will never be evaluated on every backend, so zero-length vectors needed a max-epsilon guard at the `rsqrt` argument itself.

Solution: Added explicit `Tooltip` metadata and capacity/presentation headers to every serialized field in `AmbientBiotaDirector`. Renamed the stale macro-hydration `sdfEmergenceBias` local to `verticalBias` so the code no longer implies foreign cave/SDF truth after that dependency was purged. Updated `SafeNormalize2` and `SafeNormalize3` to compute `rsqrt(max(lengthSq, 1e-8))`, routed drift direction through `SafeNormalize3`, and removed unused shader defines.

Rejected Alternatives: Leaving editor metadata absent was rejected because `AGENTS.md` requires tooltips on serialized fields and this is cheap cold-path clarity. Leaving direct `velocity * rsqrt(velocityLenSq)` was rejected because it leaves backend-dependent NaN risk even when a ternary fallback is present. Removing high-tier parallax/SSS/silt/salt visuals was rejected because the correct fix is safe math, not flattening the Ultra path.

Scalability potential: Low = same no-texture billboard soup with stronger NaN resistance and no extra CPU work. Middle = stable translucent biota presentation from the same 64 B packet. High = keeps panic biolume, SSS, flow silt, and procedural depth. Ultra = retains 16-step parallax and salt glints without adding CPU simulation truth or runtime file I/O.

Hardware Impact: Exact microseconds remain unmeasured; no Unity profiler run was available. Expected low-end effect is stability, not a frame-time claim. CPU hot-path cost is unchanged. GPU impact is a couple of scalar `max` operations inside shader normalize helpers, with no managed allocation and no MicroSD I/O.

## Decision 33: Remove Unity Frame Count From Macro Spawn Signal

Problem: `TryHydrateMacroSwarms` published `EntitySpawnSignal.Frame` with `Time.frameCount`. That signal is not a gameplay seed, but it is still a cross-domain metadata field emitted by the ambient director. Letting Unity's global frame counter leak into this path weakens deterministic cadence and makes replay/blackbox comparison noisier than necessary.

Solution: Replaced `Time.frameCount` with the director-owned `_frameIndex`, which is already advanced by ambient spawn/drift/macro hydration cadence. This preserves the existing `EntitySpawnSignal` contract and avoids any public API change.

Rejected Alternatives: Leaving `Time.frameCount` was rejected because the ambient director already has a local deterministic frame counter. Adding a new signal field was rejected because interface churn and duplicate signal expansion are forbidden during the batch. Replacing the signal lane was rejected because the existing typed `GlobalSignals.Publish(in EntitySpawnSignal)` path is already the established cross-domain broadcast.

Scalability potential: Low = identical visual behavior and no extra CPU work. Middle = cleaner replay/telemetry cadence. High = same macro hydration and biolume overkill path with less Unity-frame coupling. Ultra = deterministic metadata remains compatible with dense shader-side visual overkill without creating new CPU truth.

Hardware Impact: Exact microseconds remain unmeasured. Expected runtime frame-time change is effectively 0 us; this is a determinism/telemetry hygiene repair. It adds no managed allocation, no NativeArray ownership, no file I/O, and no shader work.

## Decision 34: Current Build Wall Is Gameplay/Audio Signal Contract, Not Ambient

Problem: After Loop 14, global `dotnet build` no longer remains green. The fresh compile wall is outside the ambient domain and appears in player kinematics, audio, and acoustic zone code.

Solution: Recorded the current failure in `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt` and did not patch foreign domains. Direct ambient Bee validation still fails before source analysis because `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Core.ref.dll` is missing.

Rejected Alternatives: Editing `PlayerKinematicsRuntime.cs`, `HectonMusicDirector.cs`, `AcousticZoneController.cs`, or shared signal contracts from the ambient agent was rejected because it crosses the authoritative `Assets/_Project/Scripts/AI/Ambient/` domain. Reporting the build as green was rejected because the current objective log exits 1.

Scalability potential: No runtime scalability change. Ambient Low/Middle/High/Ultra paths remain statically clean; Unity runtime, GCMonitor, and GPU proof remain blocked until foreign compile walls and Unity editor access are repaired.

Hardware Impact: 0 us measured. This is validation state, not runtime behavior. It blocks reliable profiler numbers and runtime visual validation.

## Decision 35: Remove Unity Time From Ambient Telemetry Cadence

Problem: `RecountActiveBiota` still used `Time.unscaledTime` to compute `CullRatePerSecond`. That was not a gameplay seed, but it left blackbox telemetry dependent on Unity wall-clock state while every simulation tick already receives an explicit dispatcher delta.

Solution: Added a director-owned `_telemetryClockSeconds` and `_lastRecountClockSeconds`. `Tick(float deltaTime)` finite-clamps dispatcher delta and advances the telemetry clock before the `_jobPending` early return. `RecountActiveBiota` now computes elapsed time from that clock instead of Unity `Time`. The C# ambient domain now has no `Time.` references.

Rejected Alternatives: Keeping `Time.unscaledTime` was rejected because blackbox cadence should follow the same dispatcher contract as simulation cadence. Passing a delta into `LateFrameTick` was rejected because it would require interface churn outside ambient ownership. Using shader `_Time` as a source was rejected because it is presentation-only and unavailable to CPU telemetry.

Scalability potential: Low = cull-rate telemetry stays tied to cheap dispatcher scalar time with no new buffer, allocation, or registry dependency. Middle = cleaner replay/blackbox comparison across frame pacing. High = same light-reactive and high-density path without Unity wall-clock coupling. Ultra = shader-side overkill remains visual-only while CPU telemetry stays deterministic.

Hardware Impact: Exact microseconds remain unmeasured. Expected frame-time change is effectively 0 us; this is determinism and diagnostic hygiene. It adds two scalar fields and one finite-clamped scalar accumulation per tick, with no managed allocation, no NativeArray ownership, no file I/O, and no shader work.

## Decision 36: Drive Ambient Shader Motion From Dispatcher Time

Problem: The C# director no longer used Unity `Time`, but the ambient indirect shader still used `_Time.y` for billboard pulse and silt shimmer. It was presentation-only, not authority, but it still made visual replay comparison depend on Unity's global shader time instead of the director's owned cadence.

Solution: Added `_HectonBiotaVisualTime` to the ambient shader CBUFFER and a cached `BiotaVisualTimeShaderId` in the director. `RenderIndirectBiota` writes the existing `_telemetryClockSeconds` into the material parameter path, and the shader uses that value for pulse and silt phase.

Rejected Alternatives: Keeping `_Time.y` was rejected because the director already owns a finite-clamped dispatcher clock. Adding a new GPU buffer was rejected because one scalar material parameter is enough and avoids bandwidth waste. Creating a MaterialPropertyBlock was rejected because project rules forbid MPB for standard geometry/SRP batching. Moving pulse/silt to CPU was rejected because the visual fake belongs in the shader and should not create CPU truth.

Scalability potential: Low = same cheap triangle-wave billboard pulse with deterministic dispatcher cadence and no texture samples. Middle = stable biome tint and translucent drift. High = pulse/silt remain synchronized with panic biolume, SSS, salt glints, and 16-step parallax. Ultra = visual overkill remains shader-side while CPU simulation stays a sparse modulo bucket.

Hardware Impact: Exact microseconds remain unmeasured. Expected CPU delta is one scalar material write on the existing indirect render path; GPU instruction count is effectively unchanged because `_Time.y` was replaced by a CBUFFER scalar. No managed allocation, no NativeArray ownership, no MicroSD I/O, and no extra draw call were added.

## Decision 37: Repair Stale Telemetry Clock Identifier

Problem: `ResetCapacityDependentRuntimeState()` still referenced `_lastRecountTimeSeconds` after the telemetry clock was renamed to `_lastRecountClockSeconds`. That stale identifier would block compilation of the ambient director once Unity/Bee reaches this source.

Solution: Replaced the stale reset target with `_lastRecountClockSeconds`. This keeps capacity resets aligned with the dispatcher-owned telemetry clock and avoids introducing another Unity `Time` dependency.

Rejected Alternatives: Reintroducing `_lastRecountTimeSeconds` was rejected because it would preserve duplicate telemetry state and weaken the earlier time-decoupling repair. Running another full `dotnet build` was rejected for this loop because the user explicitly instructed not to rebuild every time; static identifier and forbidden-pattern scans were used instead.

Scalability potential: Low = unchanged cheap dispatcher-clock telemetry with no extra math. Middle = stable cull-rate accounting after capacity changes. High = the same high-density light-reactive path without duplicate time fields. Ultra = shader-side visual overkill continues from `_HectonBiotaVisualTime` while CPU telemetry stays on one clock.

Hardware Impact: Exact microseconds remain unmeasured. Runtime delta is expected to be 0 us because this is a compile-hygiene correction in a cold reset path. No managed allocation, NativeArray ownership, file I/O, shader instruction, or draw-call change was added.

## Decision 38: Prevent AUP Grid Delta Integer Overflow

Problem: `DeltaMeters()` subtracted `long` AUP grid coordinates before multiplying by the sector size. If two AUPs were ever separated by an extreme sector delta, the signed integer subtraction could overflow before conversion to `double`, corrupting cull, retire, and render-local distance checks.

Solution: Cast both grid coordinates to `double` before subtraction on X/Y/Z. The helper still returns a `double3` and all consumers keep squared-distance checks and existing finite guards.

Rejected Alternatives: Leaving the integer subtraction was rejected because AUP is the simulation-scale authority and overflow before finite checks is silent corruption. Adding BigInteger or checked exceptions was rejected because Burst/hot-path AUP math needs deterministic scalar arithmetic, not managed arbitrary precision or gameplay exceptions.

Scalability potential: Low = same cheap squared-distance cull with safer sector math. Middle = stable bucket drift and macro pack/dehydrate checks. High = light-reactive high-density biota keep correct AUP-relative flee/cull vectors. Ultra = dense visual overkill remains driven by correct camera-local deltas, not overflowed sector math.

Hardware Impact: Exact microseconds remain unmeasured. Expected runtime delta is negligible: three double casts before existing double arithmetic in the distance helper. No allocation, file I/O, NativeArray ownership, shader instruction, draw-call change, or public API change was added.

## Decision 39: Runtime Pose Must Be Finite Before Feeding Ecology, Flow, And Draw Bounds

Problem: `Tick` and `SlowTick` trusted `pose.RuntimePosition` directly. A non-finite player runtime position from an upstream owner could reach `RefreshEcologyInputs`, `RefreshAbyssalFlow`, indirect draw bounds, and `_HectonBiotaOriginWS`. The Burst biota AUP path had guards, but the bridge-facing runtime `Vector3` path could still propagate NaN into foreign ecology/flow samplers or render culling.

Solution: Added `SanitizeRuntimePosition(float3 position, float3 fallback)` and routed both `Tick` and `SlowTick` player-pose caching through it. `RefreshEcologyInputs` now keeps deterministic default biomass/capacity when the runtime position is non-finite. `RefreshAbyssalFlow` now falls back to the deterministic default flow vector before calling the vegetation bridge with invalid coordinates. `UploadIndirectArgs` also rejects meshes with no submesh before reading submesh 0. Ambient macro spawn and organic scrap notifications now push directly through `SignalBus<EntitySpawnSignal>` and `SignalBus<DebrisSpawnSignal>`.

Rejected Alternatives: Letting foreign ecology/flow services handle NaN was rejected because ambient owns the data it passes across the boundary. Resetting runtime position to zero was rejected because it can teleport render bounds and flow sampling; last valid position is the safer presentation fallback. Keeping ambient on the `GlobalSignals.Publish` wrapper was rejected for this loop because the active mandate asks for typed lanes directly. Editing player, ecology, vegetation, or mesh-authoring domains was rejected because this loop is constrained to `Assets/_Project/Scripts/AI/Ambient/` and those files are outside current ownership.

Scalability potential: Low = cheap default biomass/flow fallback on bad pose, no extra sampling, and no object churn. Middle = stable ecology and flow sampling after transient upstream pose faults. High = light-reactive biota and richer shader motion continue from the last finite origin. Ultra = dense visual overkill remains shader-side while invalid upstream pose data is quarantined before render bounds and material origin.

Hardware Impact: Exact microseconds are unmeasured. Expected low-end effect is stability and less wrapper fan-out, not a frame-time claim. Added work is scalar finite checks in existing Tick/SlowTick/flow cold paths and one `mesh.subMeshCount` branch before indirect args upload. No managed allocation, NativeArray ownership, file I/O, shader instruction, draw-call change, or public API change was added.

## Decision 40: OffsetAup Must Fail Safe Before Integer Overflow And Render Fallback Must Not Allocate Mid-Frame

Problem: `OffsetAup` normalized local-space deltas into sector shifts, but only downstream local float finiteness was checked. A hostile or corrupt AUP/delta could create a non-representable grid shift or overflow signed `long` grid addition before a later finite check. Separately, `TryResolveDrawMesh` lazily created the fallback quad mesh from the indirect render path, which could allocate a `Mesh` and managed vertex/index arrays on the first active biota draw.

Solution: `OffsetAup` now rejects non-finite local deltas, non-representable grid shifts, and unsafe grid additions via `CanAddGridOffset`, returning the origin as the deterministic fallback. The fallback indirect quad is now prepared in cold `OnEnable` through `EnsureFallbackDrawMeshReady`, and `TryResolveDrawMesh` only returns existing meshes. `UploadIndirectArgs` also rejects zero-index meshes and caches submesh index values before locking the indirect args buffer.

Rejected Alternatives: Relying on downstream `IsFiniteAup` was rejected because it cannot detect signed grid wrap. Using checked arithmetic or exceptions was rejected because Burst/hot paths must not throw gameplay exceptions. Leaving lazy fallback mesh allocation in render was rejected because first active draw is a gameplay frame. Forcing every scene to assign a mesh asset was rejected because the existing component contract explicitly supports an optional fallback mesh.

Scalability potential: Low = no first-draw fallback mesh allocation hitch on MX350 and safer AUP drift under corrupt inputs. Middle = stable cull/render-local math from deterministic origin fallback. High = dense light-reactive biota continues without grid wrap corrupting local positions. Ultra = shader overkill keeps receiving stable local coordinates and does not inherit impossible sector shifts.

Hardware Impact: Exact microseconds are unmeasured. Expected low-end effect is removal of a potential first-draw managed allocation burst and prevention of integer-overflow corruption. Added work is scalar finite/range checks in AUP offset helper and one zero-index mesh guard in the cold indirect-args upload path. No NativeArray ownership, file I/O, shader instruction, draw-call change, or public API change was added.

## Decision 41: Typed Signal Lanes Must Be Warm Before Ambient Gameplay Ticks

Problem: Loop 19 moved ambient macro spawn and organic debris notifications directly to `SignalBus<EntitySpawnSignal>` and `SignalBus<DebrisSpawnSignal>`, while biome sync already read `SignalBus<BiomeChangedSignal>.GetFrameSnapshot()`. Direct typed lanes are correct, but a lane that is not already initialized by bootstrap can allocate its native queue/snapshot on first push or first explicit initialization. That is acceptable during cold setup, not during a gameplay tick or late-frame debris drain.

Solution: Added `EnsureSignalLanesReady()` and call it from cold `OnEnable` before runtime registration. The method configures and initializes the three ambient-used lanes with the same capacities and FNV lane hashes used by the core signal bootstrap: biome changed = 64, entity spawn = 128, debris spawn = 128 with low-tier debris frame cap = 16. Runtime `Tick`, `SlowTick`, macro hydration, and `LateFrameTick` now operate against prewarmed typed lanes.

Rejected Alternatives: Reverting to `GlobalSignals.Publish` was rejected because the active mandate requires direct typed lanes and `ReadOnlySpan<T>` snapshots. Leaving first-use `SignalBus<T>.Push` to initialize lazily was rejected because cold native allocation can then land on a gameplay frame. Editing `GlobalSignals` to expose a public ambient bootstrap hook was rejected because it would modify shared core ownership from the ambient domain.

Scalability potential: Low = no first ambient debris/macro event lane initialization hitch on MX350 and the debris lane remains capped to the existing 16-frame low-tier organic signal budget. Middle = stable biome signal reads with no extra polling or duplicate lane. High = macro hydration and panic-biota debris can publish through richer typed telemetry without wrapper fan-out. Ultra = dense shader overkill continues to consume the same saved CPU budget while signal traffic stays bounded and cache-segregated.

Hardware Impact: Exact microseconds are unmeasured. Expected low-end effect is moving possible native queue/list cold allocation out of gameplay ticks and late-frame drains. Added gameplay-frame cost is zero after initialization; `OnEnable` pays only cold `SignalBus<T>.Configure`/`EnsureInitialized` calls. No local NativeArray ownership, no file I/O, no shader instruction, no draw-call change, no public API change, and no MicroSD pressure were added.
