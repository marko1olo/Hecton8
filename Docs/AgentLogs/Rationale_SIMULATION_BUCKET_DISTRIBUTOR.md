# Rationale_SIMULATION_BUCKET_DISTRIBUTOR

## Initial Mandate Selection
Problem: Master modulo orchestration touches frame phase ownership, registry injection, signal emission, zero-GC scheduling, native memory, frame budgets, and blackbox telemetry.
Solution: Use ARCH_Execution_Phases, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Signal_Lane_Segregation, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, OPT_Performance_Budgets_FrameTime_VRAM_Limits, and DBG_Telemetry_Crash_Reporting_PostMortem as the active mandate set.
Rejected Alternatives: Reading unrelated physics/render mandates would pollute the scheduling-domain implementation.
Scalability potential: Low uses static bucket distribution; Middle/High/Ultra can spend saved frame stability on richer visual sync and denser presentation while scheduling remains bounded.
Hardware Impact: i3/MX350 target is reduced jitter by avoiding per-system self-modulo spikes; expected gain is frame pacing stability, not raw throughput.

## Decision: Scheduling-Domain Ownership
Problem: The prompt asks for global enforcement, but 20+ agents are editing adjacent domains.
Solution: Implement the orchestrator in Assets/_Project/Scripts/Core/Scheduling and expose contracts/signals instead of concrete dependencies.
Rejected Alternatives: Editing each gameplay system directly would create cross-domain conflicts and new compile walls.
Scalability potential: A central bucketer supports static low-tier distribution and dynamic high-tier rebalance from the same API.
Hardware Impact: MX350/i3 avoids N independent modulo decisions accumulating on one frame.

## Decision: Native SoA Bucket State
Problem: Bucket assignment must be globally visible without per-frame allocation or object graphs.
Solution: Store front/work entity bucket tables plus cost/load EWMA arrays in NativeArray-backed buffers, resolving through GlobalDataVault only; the bucketer now keeps handles and scalar state, not persistent private arrays.
Rejected Alternatives: Managed dictionaries, class records, per-system local masks, or H8Memory fallback ownership were rejected for GC pressure and authority drift.
Scalability potential: Low uses static tables and 128 slow buckets; Middle/High/Ultra can reuse the same buffers for denser entity costs and more aggressive presentation sync.
Hardware Impact: i3/MX350 gains predictable linear cache access and avoids managed allocation stalls; expected gain is <0.1 ms stability, not raw FPS claims.

## Decision: Burst Greedy Load Balancer
Problem: The orchestrator needs balanced expected bucket cost without sorting or managed containers.
Solution: Burst IJob clears scratch loads, scans entity cost EWMA, and assigns each active entity to the currently lightest bucket. Mutation versioning discards stale results if entities changed while the job was pending.
Rejected Alternatives: LINQ/order-by, managed priority queues, or full optimal bin packing were rejected as slower, allocation-prone, and unnecessary for frame pacing.
Scalability potential: Low skips the job; Middle/High/Ultra run rebalance every 60 frames and spend recovered spikes on visual overkill.
Hardware Impact: MX350/i3 avoids continuous rebalance cost; high-end hardware gets flatter bucket loads without main-thread sort spikes.

## Decision: Frame Pacing Warning and Homeostasis Command
Problem: If bucket math cannot keep predicted work under 16.667 ms, the failure must be observable and must shed load immediately.
Solution: SystemDispatcher emits FramePacingWarningSignal once per frame and directly calls ApplyHomeostasisKillSwitch with VFX, particle, distant-fauna, slow-tick, and time-dilation bits.
Rejected Alternatives: Debug.Log, polling by external systems, or adding a new concrete AGENT_HOMEOSTASIS_BRAIN dependency were rejected because typed lanes and existing homeostasis controls already exist.
Scalability potential: Low-tier sheds non-critical visual cost; High/Ultra can keep visual overkill until the mathematical budget is breached.
Hardware Impact: i3/MX350 receives immediate VFX/fauna relief instead of compounding slow frames; expected gain is bounded recovery from >16.667 ms spikes.

## Decision: Consumer Debt Cleanup
Problem: SargassumMicroFaunaBoids and HectonFluidEngine were still assigning local bucket uniforms from private masks.
Solution: Both systems now read active bucket index/mask/interpolation from ISimulationBucketer and only fall back to SimulationBucketConstants when the registry service is absent.
Rejected Alternatives: Leaving `%16`/`%8` equivalents in consumers was rejected because it keeps frame spikes uncoordinated.
Scalability potential: Low receives longer staggered updates; High/Ultra uses the same authoritative frame phase with smoother interpolation.
Hardware Impact: MX350/i3 avoids same-frame clustering from independent counters; estimated benefit is lower worst-frame variance, not higher average throughput.

## Decision: Validation Compile Wall
Problem: dotnet build cannot reach the scheduler code because unrelated dependency errors stop Hecton8.Core compilation.
Solution: Ran three no-restore build attempts, then a final post-polish pass; attempts 2 and 3 were captured to Docs/AgentLogs and show external errors in FloraInteractionManager, GlobalRegistry docking service, VehicleDockingModule, and EcosystemDirector contracts. Attempt 4 timed out behind external errors and had no errors in touched files when filtered.
Rejected Alternatives: Editing VFX wakes, construction docking, or world ecosystem contracts was rejected as out-of-domain sabotage.
Scalability potential: Scheduler implementation remains isolated; once external contracts compile, the same validation command can verify the bucket path.
Hardware Impact: No runtime impact from the compile wall. The implemented scheduler path remains designed for i3/MX350 static behavior and high-end dynamic rebalance.

## Decision: Omega Polish Rebalance Liveness
Problem: Frequent TryReportEntityCostMs calls could increment the mutation version and cause pending high-tier rebalance jobs to be discarded forever.
Solution: Only register/unregister changes invalidate pending rebalance output; cost EWMA reports update native cost state without changing structural version.
Rejected Alternatives: Treating every cost sample as a structural mutation was rejected because it can starve the 60-frame rebalance path under real telemetry.
Scalability potential: Low remains static; Middle/High/Ultra keep accepting periodic rebalance results even while costs stream.
Hardware Impact: i3/MX350 unaffected; high-end avoids wasted rebalance jobs and preserves frame-pacing corrections.

## Decision: H-Phi Data Eviction Rewrite
Problem: The first implementation still allowed scheduler fallback ownership outside the vault, which violated the Data Sovereignty pass.
Solution: Reworked ModuloSimulationBucketer to store only VaultBufferHandle<T> handles plus scalar state. Entity buckets, work buckets, EWMA costs, bucket loads, rebalance scratch, rebalance result, frame state, and black-box ring are all GlobalDataVault buffers.
Rejected Alternatives: Keeping H8Memory.Allocate fallback was rejected because fallback ownership becomes a private scheduler island during bootstrap and Quest failure analysis.
Scalability potential: Low tier resolves the same static 128-bucket table from the vault; Middle/High/Ultra can rebalance and publish visual-budget flags without moving storage ownership back into the system.
Hardware Impact: i3/MX350 keeps predictable linear memory access; Quest/Android avoids a second allocator authority for scheduler state.

## Decision: ARM64 Packing and Signal Layout
Problem: ARM64/Quest builds punish implicit padding and signal structs that depend on runtime layout luck.
Solution: Fixed scheduler contracts at Pack=1 explicit sizes: SimulationBucketFrameState 64 bytes, SimulationBucketRebalanceResult 20 bytes, SimulationBucketBlackBoxEntry 64 bytes, SimulationBucketSyncSignal 32 bytes, and FramePacingWarningSignal 64 bytes.
Rejected Alternatives: Standard Sequential layout without explicit size was rejected because platform packing drift is invisible until native/job/shader boundary faults.
Scalability potential: Same binary telemetry contract feeds toaster diagnostics and high-end pacing overlays.
Hardware Impact: Quest/Android risk reduction; no claimed frame-time gain.

## Decision: Fault-Only Black Box Dump
Problem: The scheduler needed last-300-frame postmortem state without Steam Deck MicroSD stutter or managed per-frame allocation.
Solution: Added BufferID.SimulationBucketBlackBox and a 300-entry DataVault ring. The normal path writes one packed entry and overwrites same-frame pre/post samples; binary dump to Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin occurs only after non-finite cost detection.
Rejected Alternatives: Per-frame file logging, Debug.Log, or managed queues were rejected for I/O pressure, GC pressure, and poor crash recovery.
Scalability potential: Low/Middle use the same ring for crash proof; High/Ultra can correlate visual overkill admission with frame-pacing state through the hash/flags.
Hardware Impact: i3/MX350 and Steam Deck receive zero normal-path disk writes; expected runtime cost stays under 5 us/frame.

## Decision: Visual Overkill Budget Signal
Problem: High-tier systems need a clean way to spend saved scheduling budget without reading private bucketer internals.
Solution: Added SimulationBucketPacingFlags.VisualOverkillBudgetAvailable when non-low-tier expected frame cost is under half the 16.667 ms target, no rebalance is pending, and no non-finite fault is latched.
Rejected Alternatives: Direct VFX/render edits from the scheduler domain were rejected as cross-domain ownership drift.
Scalability potential: Low stays a Dear Lie with static buckets; Middle/High/Ultra can gate salt crystals, silt, particles, POM, or other overkill from a typed frame-state flag.
Hardware Impact: i3/MX350 avoids the flag; high-end gets an allocation-free budget signal.

## Decision: Multiplatform Compute Audit
Problem: User requested ARM64/Quest/Metal/Steam Deck review even though scheduler code is CPU-side.
Solution: Scanned touched compute consumers and shader thread-group defines. Sargassum, AbyssalFlow, and FluidAdvection paths use 64-thread 1D groups or 4x4x4/8x8x8 groups, below Metal's 1024-thread-group limit. No scheduler shader mutation was required.
Rejected Alternatives: Editing shaders without a failing thread-group or DirectX-only scheduler dependency was rejected.
Scalability potential: Toaster and high-end share valid compute dispatch bounds; visual overkill remains downstream and optional.
Hardware Impact: Metal/Mac portability risk reduced by evidence; no runtime delta.

## Decision: Compile Wall After H-Phi Pass
Problem: attempt5 exposed a missing helper in SargassumMicroFaunaBoids before the build reached the known construction-domain errors.
Solution: Replaced the incomplete handle helper calls with existing DataVault-backed EnsureNativeArrayCapacity calls for boid sensory threat and black-box buffers, then reran build.
Rejected Alternatives: Adding a dead helper that only updates handles was rejected because the live native arrays would still remain unresolved.
Scalability potential: Sargassum sensory data remains vault-backed and can still participate in bucketed scheduling cleanup.
Hardware Impact: No new allocation path; DataVault buffer resolution stays cold-path.

## Decision: External Construction Compile Wall
Problem: attempt6 now fails only in VehicleDockingModule with missing construction-domain methods such as ResetDockingRuntimeCaches, ResolveSystemStress01, and docking wake helpers.
Solution: Stopped at the domain boundary and recorded the wall after filtering touched scheduler files to zero build errors.
Rejected Alternatives: Patching construction docking behavior from the scheduling agent was rejected as architectural sabotage.
Scalability potential: Scheduler remains ready to validate once construction dependencies are restored.
Hardware Impact: No runtime impact from the external compile wall.
