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
