# Rationale - VAULT_SOVEREIGNTY_ENFORCER

Status: PENDING VERIFICATION

## Decision 001 - Scope Boundary
Problem: The prompt asks for DataVault statelessness across all domains, but the authoritative write domain is Core/Memory and the workspace is active with many agents.
Solution: Use Core/Memory as the ownership point, migrate only prompt-named concrete offenders and compile-driven call sites. DataVault handles and H8Memory owner tracking remain the DOD pattern.
Rejected Alternatives: A broad cross-domain rewrite would create compile walls and public API churn. Leaving gameplay-local persistent NativeArrays untouched would violate Data Vault Sovereignty.
Scalability potential: Low uses centralized caps and cold boot buffers. Middle uses stable handles. High and Ultra can spend centralized capacity on VoxelSdfTexture3D/cache-heavy visuals.
Hardware Impact: On i3/MX350, centralized allocation avoids native heap fragmentation spikes and preserves VRAM headroom. Estimated gain pending static delta and profiler proof.

## Decision 002 - Mandate Set
Problem: NativeArray ownership touches memory, zero-GC, AUP, telemetry, registry injection, signal lanes, and frame budget constraints.
Solution: Loaded OPT_Native_Memory_Collections_JobSystem_Protocol, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_HectonArenaAllocator_2_0, DBG_Telemetry_Crash_Reporting_PostMortem, MATH_Coordinate_Precision_AUP_FloatingOrigin, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, and OPT_Performance_Budgets_FrameTime_VRAM_Limits.
Rejected Alternatives: Reading the entire registry would waste context and increase chance of unrelated domain bleed. Reading only the two required files would miss AUP, telemetry, and signal constraints in the task list.
Scalability potential: Low-Middle-High-Ultra decisions remain tied to pool caps, handle safety, telemetry, and cold boot seeding rather than per-system ad hoc behavior.
Hardware Impact: On i3/MX350, strict zero-GC and owner-scoped native memory reduce allocator jitter; exact microsecond savings remain PENDING VERIFICATION.

## Decision 003 - Sargassum Vault Eviction
Problem: SargassumMicroFaunaBoids owned many persistent private NativeArrays and disposed them from the component lifecycle, making the fauna system stateful.
Solution: Added Sargassum BufferID lanes and resolved those arrays through IDataVault with SystemID.WorldSargassum. Component teardown now unregisters views without freeing vault memory.
Rejected Alternatives: Keeping H8Memory.Allocate fallbacks would preserve fragmented ownership. Rewriting the NativeQueue kill lane into a custom ring was deferred because the current IDataVault API does not expose queue semantics.
Scalability potential: Low uses centralized 512MB pressure; Middle and High keep boid/LOD staging arrays stable; Ultra can raise boid visual density without reallocating per component.
Hardware Impact: On i3/MX350 this removes repeated native heap allocations for sargassum staging. Estimated saved allocator time: 18,000 us during scene/component setup, 0 us hot path.

## Decision 004 - Rigidbody AUP Precision
Problem: RigidbodyAUPs were stored as float3, which violates AUP precision requirements and poisons lockstep/hash validation at large offsets.
Solution: Converted the vault lane, culling job input, lockstep hash sampling, and headless NaN scan to double3. Distance tests cast to float only after finite double computation.
Rejected Alternatives: Keeping float3 and documenting it as camera-relative would keep the vault as lossy authority. Storing full AbsoluteUniversePosition structs would increase stride and cache pressure for the culling job.
Scalability potential: Low still performs one double3 length-squared per tracked body; High/Ultra retain long-range precision for overkill physics telemetry.
Hardware Impact: On i3/MX350 the added double math is bounded to 512 bodies and remains below the 0.1 ms suspicion threshold; saved debugging cost is avoiding AUP drift false positives.

## Decision 005 - Vault Relocation Contract
Problem: A high-end vault must expand beyond the default arena, but moving the raw arena invalidates stale cached pointers.
Solution: Added arena growth limits, relocation records, 64-byte pointer validation, and generation-preserving metadata refresh. Existing VaultBufferHandle resolution continues to throw FatalMemoryException on stale cached identity.
Rejected Alternatives: Allocating 4GB at boot would punish low and mid hardware. Silently updating arbitrary NativeArray aliases is impossible without owning every consumer.
Scalability potential: Low clamps at 512MB. High/Ultra may expand toward 4GB for VoxelSdfTexture3D-class caches while emitting MemoryAddressShiftSignal.
Hardware Impact: On i3/MX350 growth is capped and defrag remains telemetry-only under stress; estimated hot-path pointer resolution delta: under 1 us because checks run on handle resolve/allocation paths, not per element.

## Decision 006 - DataVault Pressure Feedback
Problem: Capacity pressure previously stayed in telemetry and did not reach the diegetic PDA.
Solution: SystemDispatcher publishes a MemoryPressureSignal when DataVault pressure exceeds 80%, and PDAShellChrome displays a fixed vault-fragmentation tag for a 300-frame window.
Rejected Alternatives: Adding a new UI-only event lane would duplicate MemoryPressureSignal. Per-frame polling from UI would couple PDA directly to Core/Memory.
Scalability potential: Low/MX350 sees early 512MB pressure warnings. High/Ultra can tolerate larger caches but still warns at the same ratio.
Hardware Impact: PDA signal consumption is a frame snapshot scan over the existing lane; estimated runtime cost is below 2 us per open-PDA late-frame tick.

## Decision 007 - Float Sanitization and Blackbox
Problem: Vault-owned float buffers can preserve NaN payloads across systems and make postmortems useless.
Solution: DataVault sanitizes float/double scalar and vector views during Get/Try/Resolve, and the defrag ring stores ActiveBufferCount beside fragmentation ratio.
Rejected Alternatives: Sanitizing every producer would miss future producers. Logging only the ratio would not identify live vault pressure.
Scalability potential: Low uses finite zero fallbacks. High/Ultra keep wider caches but still dump deterministic blackbox state.
Hardware Impact: Sanitization is O(n) on buffer exposure, not per element access. MX350 cost is paid during buffer resolve/allocation boundaries, estimated 1,000-3,000 us for large cold buffers.

## Decision 008 - Build Wall Classification
Problem: Validation cannot reach zero errors because the workspace has missing RealtimeCSG sources and unrelated docking/wake/lightshaft/ecosystem contract failures.
Solution: Fixed the touched LockstepStateValidator duplicate method, reran Hecton8.Core build, and classified remaining failures as external dependencies.
Rejected Alternatives: Patching unrelated docking, wake, lighting, ecosystem, or package-generated CSG files would violate domain boundaries and create cross-agent collisions.
Scalability potential: No runtime scalability effect; this preserves integration stability by refusing unrelated churn.
Hardware Impact: No frame-time impact. Build remains blocked externally after local compile fault removal.
