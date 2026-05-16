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
