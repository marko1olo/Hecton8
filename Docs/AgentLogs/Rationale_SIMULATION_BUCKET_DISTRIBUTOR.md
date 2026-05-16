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
Solution: Store front/work entity bucket tables plus cost/load EWMA arrays in NativeArray-backed buffers, resolving through GlobalDataVault when available and H8Memory only as fallback.
Rejected Alternatives: Managed dictionaries, class records, or per-system local masks were rejected for GC pressure and authority drift.
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
