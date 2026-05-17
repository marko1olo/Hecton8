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
Solution: Carried forward the vault-handle helper path so boid sensory threat and black-box buffers resolve through VaultBufferHandle<T> instead of local persistent NativeArray fields, then reran build.
Rejected Alternatives: Reintroducing local NativeArray fields was rejected because it would violate the H-Phi data-eviction pass.
Scalability potential: Sargassum sensory data remains vault-backed and can still participate in bucketed scheduling cleanup.
Hardware Impact: No new allocation path; DataVault buffer resolution stays cold-path.

## Decision: External Construction Compile Wall
Problem: attempt6 now fails only in VehicleDockingModule with missing construction-domain methods such as ResetDockingRuntimeCaches, ResolveSystemStress01, and docking wake helpers.
Solution: Stopped at the domain boundary and recorded the wall after filtering touched scheduler files to zero build errors.
Rejected Alternatives: Patching construction docking behavior from the scheduling agent was rejected as architectural sabotage.
Scalability potential: Scheduler remains ready to validate once construction dependencies are restored.
Hardware Impact: No runtime impact from the external compile wall.

## Decision: Job Admission Data Eviction
Problem: A second H-Phi scan found BurstTokenBucketJobAdmissionService still owned persistent NativeArray fields for lane budgets, refill tables, EWMA job costs, job hashes, and black-box telemetry.
Solution: Moved all persistent admission storage to GlobalDataVault buffers using SystemID.JobAdmission and BufferID.JobAdmission* entries. The service now stores vault handles and scalar counters only.
Rejected Alternatives: Keeping the old private NativeArray fields was rejected because it created a second scheduler-owned data island beside the master bucketer.
Scalability potential: Low tier keeps the same fixed token-bucket Dear Lie; High/Ultra can admit heavier visual jobs from the same vault-owned EWMA cost table without adding managed lookup containers.
Hardware Impact: i3/MX350 receives the same fixed-size budget math with 0 B private scheduler native ownership. Runtime microsecond delta is not measured; expected frame effect is neutral because buffer lengths and loops are unchanged.

## Decision: Job Admission Black Box and Steam Deck I/O
Problem: Job admission is part of frame pacing and can deny or shed work, but its black-box ring previously lived in private native state and had no fault dump file.
Solution: Added a 300-entry Pack=1 Size=32 admission black-box in GlobalDataVault and a fault-only dump path at Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_JobAdmission.bin.
Rejected Alternatives: Per-frame binary writes or Debug.Log telemetry were rejected for MicroSD stutter and string allocation risk.
Scalability potential: Low/Middle can diagnose denied work without disk traffic; High/Ultra can correlate visual-overkill admission with scheduler debt state after a fault.
Hardware Impact: Steam Deck normal-path disk writes remain 0. Ring write is fixed-size memory only; exact microseconds not measured.

## Decision: Scheduler Build Revalidation
Problem: Previous validation stopped at attempt6 with an external construction wall after some scheduler-domain debt still existed.
Solution: Completed the job-admission DataVault pass, added the scheduling asmdef dependency on Hecton8.Core.Memory, injected GlobalRegistry.DataVault from GameBootstrapper, and reran dotnet build.
Rejected Alternatives: Reporting the external wall as final while BurstTokenBucketJobAdmissionService still owned private NativeArrays was rejected as incomplete H-Phi compliance.
Scalability potential: Scheduler and job admission now share vault ownership patterns and remain decoupled from construction-domain compile churn.
Hardware Impact: No runtime claim beyond unchanged fixed loop counts. Build evidence: Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt8_jobadmission_hphi.log reports Build succeeded with 0 Error(s).

## Decision: Job Admission Fault Dump Ordering
Problem: The admission fault path dumped the black-box ring before writing the current non-finite fault entry and skipped binary dump entirely when the optional telemetry sink was absent.
Solution: Write the fault entry first, then dump the 300-entry ring once per frame regardless of telemetry-sink availability. Telemetry lane/cost reporting remains conditional.
Rejected Alternatives: Keeping telemetry-sink-gated dumps was rejected because crash evidence must exist even when no listener is wired.
Scalability potential: Low/Middle keep zero normal-path disk I/O; High/Ultra get the same deterministic fault artifact when admission permits visual-overkill jobs.
Hardware Impact: Steam Deck/i3/MX350 normal path remains 0 disk writes. Fault path adds one cold binary dump only on non-finite admission state; exact microseconds not measured.

## Decision: Admission Default Table Eviction
Problem: A strict data-sovereignty audit found a private managed refill-budget array in the admission service after native arrays had been evicted.
Solution: Replaced the static array with a switch resolver and kept the actual mutable refill/budget state in GlobalDataVault buffers.
Rejected Alternatives: Leaving the array because it was cold-only was rejected; the user explicitly requested no private scheduler-owned data islands.
Scalability potential: Low tier keeps the same fixed token refill profile; High/Ultra can spend admission budget on downstream visual detail without adding scheduler tables.
Hardware Impact: Removes one cold managed array allocation. No runtime loop-count change and no measured frame-time claim.

## Decision: Current External Compile Wall
Problem: After the fault-dump polish, dotnet attempt9/10/11 encountered concurrent external compile walls outside CORE/SCHEDULING: transient Sargassum vault helper debt, then Ecosystem/SubmarineFluidDynamics, then AI/EcosystemPopulationBalancer.
Solution: Re-ran validation until the wall settled and filtered attempt11 for scheduler-owned paths. The current log has no errors in Core/Scheduling, Core/Bucketing, Bootstrap/GameBootstrapper, H8Memory, SimulationBucket, or JobAdmission paths.
Rejected Alternatives: Patching AI ecosystem SignalBus/entity-death/ref-return logic from the scheduler prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler remains isolated; global build can return to green after AI ecosystem dependency repair without changing bucket/admission code.
Hardware Impact: No runtime impact from the external compile wall. Current evidence: Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt11_after_external_edits.log.

## Decision: Job Admission Lane Constant Repair
Problem: Strict contract audit found `BurstTokenBucketJobAdmissionService.ResolveDefaultRefillBudgetMs` still referenced stale `JobAdmissionLanes.Lane2AI` and `Lane3Physics` names after the public lane taxonomy had become `Lane2Voxel` and `Lane3AI`.
Solution: Replaced the stale names with the current contract constants. The refill values are unchanged: voxel receives the 1.40 ms lane budget and AI receives the 0.80 ms lane budget.
Rejected Alternatives: Adding alias constants was rejected because it would preserve interface chaos and hide lane taxonomy drift.
Scalability potential: Low tier keeps deterministic fixed token budgets; High/Ultra admission remains compatible with voxel and AI job callers using the public lane enum.
Hardware Impact: Compile correctness only. No runtime microsecond gain claimed.

## Decision: Current Fauna Compile Wall
Problem: After scheduler lane constants were repaired, attempt12 fails outside CORE/SCHEDULING in `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` with missing species-target/tuning fields and helpers.
Solution: Stopped at the domain boundary and recorded attempt12 after verifying no scheduler, bucketer, bootstrap, H8Memory, SimulationBucket, or JobAdmission errors are present.
Rejected Alternatives: Patching predator cognition species targeting from the scheduler prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler stays isolated; fauna cognition can repair its own species-target DataVault contract without scheduler mutation.
Hardware Impact: No runtime impact from the external compile wall. Current evidence: Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt12_lane_constants.log.

## Decision: Job Admission Bootstrap Vault Repair
Problem: The H-Phi rewrite moved admission storage to GlobalDataVault, but `GameBootstrapper.EnsureJobAdmissionServiceRegistered` was still calling the interface-only Initialize overload, leaving the concrete admission service uninitialized and fail-open.
Solution: Rewired bootstrap to pass `GlobalRegistry.DataVault` when the registered service is `BurstTokenBucketJobAdmissionService`, and added a boxed DataVault overload to survive generated-project source/reference identity divergence.
Rejected Alternatives: Reintroducing private admission NativeArrays or requiring a public interface signature mutation was rejected. The legacy interface overload remains for non-concrete test services.
Scalability potential: Low tier now actually receives vault-backed fixed token budgets; High/Ultra admission can use vault-backed EWMA costs instead of silently allowing every job.
Hardware Impact: Cold-path correctness only. Hot-path loop counts unchanged; no measured microsecond gain claimed.

## Decision: Current Tether Compile Wall
Problem: After admission bootstrap wiring compiled, attempt15 fails outside CORE/SCHEDULING with `TetherManager`/`TetherSignals` missing `TetherFireRequest`.
Solution: Recorded the wall and filtered the attempt15 log; it has no scheduler, bucketer, bootstrap, H8Memory, SimulationBucket, or JobAdmission errors.
Rejected Alternatives: Patching tether physics signal ownership from the scheduling prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler remains isolated; tether physics can repair its signal contract independently.
Hardware Impact: No runtime impact from the external compile wall. Current evidence: Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt15_bootstrap_object_vault.log.

## Decision: Generated-Project Vault Overload Repair
Problem: The boxed job-admission overload exists in source but the generated Core project compiles against the precompiled scheduling DLL, which exposes the `IDataVault` overload and rejects boxed `object` calls.
Solution: `GameBootstrapper` now calls the compile-visible `BurstTokenBucketJobAdmissionService.Initialize(IJobAdmissionTelemetrySink, IDataVault)` overload with `GlobalRegistry.DataVault` for concrete admission services, while non-concrete services keep the interface-only path.
Rejected Alternatives: Relying on the boxed overload before Unity regenerates the scheduling assembly was rejected because attempt16 proved the generated project cannot see it. Reverting to interface-only initialization was rejected because it leaves admission fail-open after DataVault eviction.
Scalability potential: Low tier receives fixed vault-backed token budgets; High/Ultra can use the same vault EWMA cost table to deny or permit downstream visual jobs without scheduler-owned data.
Hardware Impact: Cold bootstrap correctness only. Hot-path loop counts are unchanged; no measured microsecond gain claimed.

## Decision: SystemDispatcher NativeArray Fallback Eviction
Problem: Re-inquisition found `SystemDispatcher` still owned fallback `NativeArray` storage for H8 time and deferred raycast hits if DataVault resolution failed.
Solution: Removed the private `NativeArray` fields and H8Memory fallback allocations for those two persistent SOA buffers. The dispatcher now stores DataVault handles and resolves temporary NativeArray views only when updating H8 time or scheduling/completing raycast jobs.
Rejected Alternatives: Keeping fallback H8Memory allocations with the correct SystemID was rejected because it preserves a scheduler-owned data island. Moving raycast command queues/lists into the vault was rejected in this pass because they are dispatcher request-staging lanes, not persistent frame-state SOA.
Scalability potential: Low/Middle keep the same deterministic command staging while persistent time/raycast result storage remains vault-owned. High/Ultra keep the same job path and can rely on the vault lock around raycast hit writes.
Hardware Impact: Removes two cold fallback allocation paths. Normal frame microseconds were not benchmarked; no runtime speed gain is claimed.

## Decision: Current UI Navigation and Ecosystem Compile Wall
Problem: attempt18 fails outside CORE/SCHEDULING in `DiegeticGyroCompassRuntime.cs` and `EcosystemDirector.cs`; the errors are missing UI navigation members/overloads and ecosystem native-pointer generic inference.
Solution: Recorded the wall and filtered attempt18; there are zero hits in `SystemDispatcher`, `GameBootstrapper`, Core/Scheduling, Core/Bucketing, H8Memory, SimulationBucket, or JobAdmission paths.
Rejected Alternatives: Patching compass UI black-box/visual-overkill logic or ecosystem native pointer inference from the scheduler prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler remains isolated; downstream UI/ecosystem domains can repair their contracts while the master bucketer and admission gate stay vault-backed.
Hardware Impact: No runtime impact from the external compile wall. Current evidence: Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt18_dispatcher_vault_views.log.

## Decision: Dispatcher PlayerLoop Authority
Problem: The master dispatcher still depended on Unity MonoBehaviour `Update()` and `LateUpdate()` messages, leaving the time authority hidden behind generic engine callbacks.
Solution: Moved the bodies behind `RunDispatcherUpdate` and `RunDispatcherLateFrame`, then installed explicit PlayerLoop nodes before Unity's script update and script late-update phases during dispatcher initialization.
Rejected Alternatives: Keeping standard MonoBehaviour update names was rejected because the prompt explicitly calls for SystemDispatcher as sole owner of time. Replacing the entire Unity player loop was rejected as unnecessary blast radius.
Scalability potential: Low/Middle/High/Ultra all enter the same explicit dispatcher cadence; downstream visual overkill still keys from typed bucket signals.
Hardware Impact: Lifecycle determinism repair only. No measured microsecond gain claimed.

## Decision: Dispatcher Debug Log Purge
Problem: Re-inquisition found dev-only `Debug.LogError` calls in the dispatcher heap-lock and AUP NaN paths.
Solution: Replaced console string emission with typed `ComplianceViolationSignal` and `GlobalTelemetryBus` numeric events. The heap-lock guard still throws in the fail-fast editor path after the typed signal is emitted.
Rejected Alternatives: Leaving console logs in the time authority was rejected because typed lanes and black-box telemetry already exist.
Scalability potential: Toaster builds avoid managed console-string diagnostics; High/Ultra diagnostics can consume the same typed signal lane.
Hardware Impact: Normal-path allocation remains 0 B; fault path emits typed telemetry only. Exact microseconds not measured.

## Decision: Dispatcher Raycast Command Vault Eviction
Problem: After H8 time and hit-result eviction, the deferred raycast command staging still lived in dispatcher-owned native containers.
Solution: Pending and scheduled `RaycastCommand` buffers now resolve through GlobalDataVault handles using `BufferID.SystemDispatcherRaycastPendingCommands` and `BufferID.SystemDispatcherRaycastScheduledCommands`; scheduling copies the fixed range into the scheduled vault buffer and clears the pending range.
Rejected Alternatives: Keeping `NativeQueue<RaycastCommand>` and `NativeList<RaycastCommand>` was rejected as another private scheduler data island. Per-raycast managed events were rejected for GC and scheduling jitter.
Scalability potential: Low tier keeps bounded 1024-command fixed buffers; High/Ultra can issue the same deferred query volume without command-container allocation churn.
Hardware Impact: Two private native containers removed. Frame-time delta was not benchmarked; no microsecond gain claimed.

## Decision: Current UI Navigation and Diagnostics Compile Wall
Problem: attempt22 fails outside scheduler after the PlayerLoop/vault-command pass. Current failures are missing compass DTO presentation fields and missing debug visualizer/debug-signal contracts.
Solution: Recorded the wall and filtered attempt22; there are zero hits in `SystemDispatcher`, `GameBootstrapper`, Core/Scheduling, Core/Bucketing, H8Memory, SimulationBucket, or JobAdmission paths.
Rejected Alternatives: Patching compass presentation DTOs or diagnostic visualizer contracts from the scheduling prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler remains isolated; downstream UI/diagnostics domains can repair their contracts while scheduler cadence, raycast command staging, and admission gates remain vault-backed.
Hardware Impact: No runtime impact from the external compile wall. Current evidence: Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt22_current_playerloop_vault_commands.log.

## Decision: Dispatcher Black Box Completion
Problem: Concurrent dispatcher black-box scaffolding had DataVault IDs, fields, and calls, but the actual ensure/dispose/write/dump methods were missing, breaking the scheduler compile and leaving the time authority without its own 300-frame heartbeat ring.
Solution: Implemented a DataVault-backed `DispatcherBlackBoxEntry` ring with a one-int cursor buffer, per-frame heartbeat writes from `RecordMemoryBlackBoxHeartbeat`, non-finite guards, typed compliance telemetry, and fault-only binary dump to `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin`.
Rejected Alternatives: Reverting the black-box calls was rejected because the prompt explicitly requires last-300-frame survival data. Per-frame file logging was rejected for Steam Deck MicroSD stutter.
Scalability potential: Low tier writes the same fixed heartbeat ring without disk I/O; High/Ultra can correlate PlayerLoop cadence, raycast backlog, homeostasis pressure, and time dilation when visual-overkill budgeting misbehaves.
Hardware Impact: Normal path is one fixed-size DataVault write; exact microseconds were not benchmarked. Fault path writes one cold binary dump only on non-finite dispatcher state.

## Decision: Final Build Green
Problem: attempt22 was externally blocked, but the current source required another compile after the dispatcher black-box repair.
Solution: Ran attempt25 with `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly`; it succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reporting stale blocked logs was rejected because the current artifact is build-green.
Scalability potential: Build-green scheduler artifacts are ready for platform smoke testing across low/high tiers.
Hardware Impact: Compile validation only; no runtime microsecond gain claimed.

## Decision: Current Build Revalidation
Problem: The build-green state needed a fresh proof after the final static debt and process checks.
Solution: Ran attempt26 with `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly`; it succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Relying only on attempt25 was rejected because concurrent agent work can mutate generated project state.
Scalability potential: The scheduler, bucketer, admission, dispatcher, and vault-backed black boxes are compile-ready for low-tier and high-tier platform smoke passes.
Hardware Impact: Compile validation only. No runtime benchmark was run and no microsecond savings are claimed.

## Decision: Dispatcher Tier Snapshot and Dump Path Repair
Problem: Re-inquisition found the dispatcher black-box fault path still used the stale `Dump_CORE_TICK_DILATION.bin` filename, and `SystemDispatcher` read `GlobalRegistry.ScalabilityTierProfileByte` repeatedly inside dispatcher cadence helpers.
Solution: Retargeted the dispatcher dump to `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin` and added one `_scalabilityTierProfileByte` frame snapshot refreshed at the start of PRE_SIMULATION. Time-dilation VFX, memory defrag cadence, black-box low-tier flags, job-admission refill, and bucket advancement now consume the cached byte.
Rejected Alternatives: Leaving the stale dump name was rejected because it breaks owner post-mortem traceability. Pushing a new cross-domain scalability signal was rejected in this pass because it would mutate public signaling surface during a batch; one dispatcher-owned frame snapshot is lower blast radius.
Scalability potential: Low tier keeps cold memory defrag cadence and static bucket fakes without repeated registry reads; High/Ultra consume the same cached tier for dynamic rebalancing and downstream visual-overkill budget flags.
Hardware Impact: Measured microseconds saved: 0 us. No profiler harness was run. Static impact is four repeated registry property reads replaced with one PRE_SIMULATION snapshot.

## Decision: Current Ecosystem Compile Wall
Problem: attempt27 after the dispatcher repair fails outside CORE/SCHEDULING in `Assets/_Project/Scripts/World/EcosystemDirector.cs` duplicate method definitions for `ResolveVaultIndexCapacity`, `ClearIndexEntries`, `TryUpsertIndexEntry`, and `TryFindIndexEntry`.
Solution: Stopped at the domain boundary and filtered the attempt27 log; it has zero scheduler, bucketer, SystemDispatcher, GameBootstrapper, H8Memory, SimulationBucket, or JobAdmission hits.
Rejected Alternatives: Editing ecosystem indexing from the scheduling prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler remains isolated; ecosystem can remove its duplicate index helpers without changing dispatcher cadence or bucket/admission contracts.
Hardware Impact: No runtime impact from this scheduling patch is measured. Current compile status is externally blocked, not scheduler-broken.

## Decision: Dispatcher Cached DataVault Lane
Problem: Re-inquisition found static dispatcher raycast helpers still resolved `GlobalRegistry.DataVault` during deferred raycast staging, scheduled hit resolution, vault locking, and black-box heartbeat fallback.
Solution: Added a dispatcher-owned cached DataVault lane populated during dependency refresh, cleared during static reset/shutdown, and used by the static helper paths. `QueueDispatcherRaycast` now relies on `ActiveRuntimeInstance` instead of a registry dispatcher lookup.
Rejected Alternatives: Leaving the registry reads was rejected because the helper paths are reached from dispatcher cadence and request staging. Moving raycast receiver arrays into DataVault was rejected because those hold managed interface references and are not valid NativeArray payloads.
Scalability potential: Low tier avoids repeated registry reads in raycast staging; High/Ultra keep the same 1024-command vault buffers without adding public API or cross-domain coupling.
Hardware Impact: Measured microseconds saved: 0 us. No profiler harness was run. Static impact is five helper-path registry DataVault reads removed from the warmed dispatcher path.

## Decision: Cached Vault Build Green
Problem: The cached DataVault repair needed a current compile after attempt27's external ecosystem wall.
Solution: Ran attempt28 with `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /clp:ErrorsOnly`; it succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reporting attempt27 as current was rejected because the current source is now build-green.
Scalability potential: Scheduler artifacts are compile-ready for runtime profiler verification on Low/Middle/High/Ultra tiers.
Hardware Impact: Compile validation only. No runtime microsecond gain is claimed.

## Decision: Explicit Defrag Phase and Vault Lock Ownership
Problem: The dispatcher defrag path used the shorter `FrostTickDefrag` overload and scheduled raycast vault locks did not carry the owning `SystemID`, weakening PRE_SIM proof and memory-sentinel attribution.
Solution: `RunPreSimulationMemoryDefrag` now snapshots `ActiveBurstLockMask` and calls the explicit overload with `MemoryDefragPhase.PreSimulation`. Scheduled raycast command/hit lock and unlock calls now pass `SystemID.SystemDispatcher`.
Rejected Alternatives: Keeping implicit defrag context was rejected because the vault already has a phase/lock-aware overload. Anonymous vault locks were rejected because they hide ownership from the memory sentinel.
Scalability potential: Low tier keeps the same cold defrag cadence and bounded raycast buffers; High/Ultra keep explicit burst-lock visibility when visual-overkill jobs raise memory pressure.
Hardware Impact: Measured microseconds saved: 0 us. No profiler harness was run. This is correctness and ownership evidence for Quest/Android and Steam Deck post-mortem paths.

## Decision: ARM-Safe Job Admission Bridge Publication
Problem: `JobAdmissionSchedulerBridge` is written during bootstrap and read by scheduling wrappers, but a plain static reference gives no explicit acquire/release contract on ARM64.
Solution: Publish the service through `Volatile.Write`, read it through `Volatile.Read`, and clear with `Interlocked.CompareExchange` so a stale clear cannot remove a newer bootstrap-owned service.
Rejected Alternatives: Leaving the plain static field was rejected because the bridge is exactly the kind of cross-phase publication that can fail only on weaker memory models. Adding locks was rejected as unnecessary for one reference slot.
Scalability potential: Low/Middle/High/Ultra all keep the same zero-allocation admission API; high-tier visual-overkill admission is not allowed to race against bootstrap publication.
Hardware Impact: Measured microseconds saved: 0 us. ARM64/Quest correctness only; no runtime benchmark was run.

## Decision: Defrag Assembly Visibility Recheck
Problem: attempt33 failed with `Hecton8.Core.Memory.Defrag` and `MemoryDefragPhase` unresolved, threatening the dispatcher time authority.
Solution: Inspected the source and generated project surface. `Assets/_Project/Scripts/Core/Memory/Defrag/MemoryDefragContracts.cs`, its asmdef, and `Library/ScriptAssemblies/Hecton8.Core.Memory.Defrag.dll` exist. Re-ran attempt34 after the script assembly was present; `Hecton8.Core.csproj` built successfully with 0 warnings and 0 errors.
Rejected Alternatives: Duplicating `MemoryDefragPhase` into scheduler/core source was rejected because it would create duplicate interface authority and future asmdef drift. Editing generated `.csproj` was rejected because Unity marks it generated.
Scalability potential: Scheduler remains dependent on the single memory-defrag contract; low-tier static bucketing and high-tier rebalance paths do not gain another private compatibility shim.
Hardware Impact: Compile validation only. No runtime microsecond claim.

## Decision: EWMA Poison Recovery Without Rebuild
Problem: The active-bucket load EWMA and job-admission EWMA relied on callers to prevent non-finite internal state. If an old `INF` or `NaN` survived in the EWMA field, a later finite sample could keep the history poisoned.
Solution: `ModuloSimulationBucketer.ReportActiveBucketLoadMs` now finite-checks previous load/jitter EWMA before lerp. `JobAdmissionMath.UpdateEwma` now falls back to a finite 0.025 ms default when both previous and measured values are invalid, then clamps to 1000 ms.
Rejected Alternatives: Running another full build was rejected per user instruction and because this is a private math-body edit with no signature or assembly-surface change. Leaving the old trust chain was rejected because mobile GPU/telemetry consumers cannot tolerate one persistent NaN.
Scalability potential: Low tier recovers to finite fake costs instead of poisoning static cadence; High/Ultra admission and visual-overkill gating resume from finite EWMA values after corrupted telemetry.
Hardware Impact: Measured microseconds saved: 0 us. No profiler or build run was performed for this pass; static scan and `git diff --check` only.

## Decision: Hot Path Registry Cache Pass
Problem: `SystemDispatcher` still resolved VRAM monitor, VRAM pressure, macro database, object pool, GI relay, and renderables through `GlobalRegistry` inside pressure/render paths after the earlier cache pass focused on DataVault and camera juice.
Solution: Added dispatcher-owned cached service references for VRAM pressure/macro/object-pool paths and render-dispatcher cached references for renderables and GI relay. Registry reads remain in refresh/fallback points only.
Rejected Alternatives: Leaving direct registry property reads in render callbacks was rejected because render callbacks can run per camera and should consume cached service lanes. Adding new signal types was rejected because no new broadcast contract is needed.
Scalability potential: Low tier avoids repeated service lookup during memory pressure and camera render callbacks; High/Ultra render callbacks keep the same renderable fan-out without registry churn.
Hardware Impact: Measured microseconds saved: 0 us. Static impact is fewer hot-path service property reads; no profiler harness was run.

## Decision: Cached Registry Build Revalidation
Problem: attempt45 was green before the hot-path registry cache pass, so it could not validate the current source.
Solution: Ran attempt46 with isolated `BaseIntermediateOutputPath` and `OutputPath`; `Hecton8.Core.csproj` restored and built successfully with 0 warnings, 0 errors, EXIT_CODE=0.
Rejected Alternatives: Reporting attempt45 as current was rejected because source changed afterward.
Scalability potential: Validation-only; low/high tier behavior is unchanged except cached lookup routing.
Hardware Impact: Compile validation only. No runtime microsecond claim.

## Decision: Direct Exit-Code Build Revalidation
Problem: attempt34 proved compile success but the polled PowerShell `Start-Process` wrapper left the appended `EXIT_CODE` field blank.
Solution: Ran attempt35 directly through `dotnet build Hecton8.Core.csproj --no-restore --no-incremental -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildInParallel=false /v:minimal /clp:Summary`; it returned EXIT_CODE=0 with 0 warnings and 0 errors.
Rejected Alternatives: Keeping attempt34 as the final pointer was rejected because the build output was green but the exit-code evidence was incomplete.
Scalability potential: Validation-only; no change to low-tier static bucketing or high-tier visual-overkill budget gates.
Hardware Impact: Compile validation only. No runtime microsecond claim.

## Decision: Load Balancing Job Bounds Vaccination
Problem: A strict mobile/Quest audit found `LoadBalancingJob` assumed nonzero rebalance-load storage and equal cost/work buffer lengths. A corrupted or mismatched DataVault handle could index `BucketLoadsMs[0]` or `EntityBucketsWork[entityIndex]` out of range inside Burst.
Solution: Added created/length gates, clamped entity iteration to the shorter cost/work span, emitted a safe zero-result when bucket storage is absent, and guarded result writes with `Result.IsCreated`.
Rejected Alternatives: Trusting vault capacity symmetry was rejected because the prompt explicitly targets ARM64/Quest/Android and forbids implicit crash assumptions. Adding private fallback NativeArrays was rejected by the data-sovereignty rule.
Scalability potential: Low/Middle keep static or conservative bucket distribution when vault storage is invalid; High/Ultra avoid a Burst crash and can resume dynamic rebalance once valid vault buffers exist.
Hardware Impact: Measured microseconds saved: 0 us. This is a crash-prevention guard, not a speed claim.

## Decision: Catastrophic Cost Clamp
Problem: Finite but pathological measured costs could survive `math.isfinite` and overflow persistent rebalance-load floats into INF after accumulation.
Solution: Added a 1000 ms catastrophic clamp in managed cost ingestion and in the Burst rebalance job. The clamp is far above the 16.667 ms target, so impossible-frame detection still trips while persistent DataVault floats remain finite.
Rejected Alternatives: Leaving finite overflow to later validation was rejected because one INF in a persistent scheduling buffer can poison mobile GPU/telemetry consumers. Clamping to the target frame time was rejected because it would hide impossible 60 FPS cases.
Scalability potential: Toaster mode gets deterministic finite fails instead of a crash; High/Ultra still expose visual-overkill only when expected frame cost is under half-budget.
Hardware Impact: Measured microseconds saved: 0 us. Static impact is finite-data survival; runtime benchmark absent.

## Decision: Current External Tether Compile Wall
Problem: The cost/bounds patch needed a fresh compile, but attempts36-38 fail outside CORE/SCHEDULING. attempt38 has only `Assets/_Project/Scripts/TetherManager.cs(20,92): ISlowTickable.SlowTick()` missing.
Solution: Applied the 3-strike protocol and filtered the logs. There are zero hits in `Core\\Bucketing`, `Core\\Scheduling`, `SystemDispatcher`, `ModuloSimulationBucketer`, `JobAdmission`, `SimulationBucket`, `GlobalDataVault`, or `H8Memory`.
Rejected Alternatives: Patching tether physics ownership from the master bucketer prompt was rejected as out-of-domain sabotage.
Scalability potential: Scheduler patch remains isolated; tether physics can repair its own slow-tick contract without changing bucket/admission math.
Hardware Impact: No runtime impact from the external compile wall. Current evidence: `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt38_bucket_nan_guard_retry2.log`.

## Decision: Span Hash and Null Publish Guard
Problem: The admission hash helper still exposed only a string-based FNV1a path, and `JobAdmissionSchedulerBridge.SetService` could publish null even though clearing has an owner-checked path.
Solution: Added `ComputeFnv1a(ReadOnlySpan<char>)`, routed the generic job hash through it, kept the string overload as a delegating compatibility wrapper, and made `SetService(null)` a no-op.
Rejected Alternatives: Removing the string overload was rejected as unnecessary public API churn. Letting null writes clear the bridge was rejected because `ClearService` already provides stale-clear protection.
Scalability potential: Low/Middle/High/Ultra keep the same admission API; high-tier visual-overkill job admission cannot be accidentally disabled by a null publish.
Hardware Impact: Measured microseconds saved: 0 us. This is cold-path contract hygiene and bootstrap correctness, not a benchmarked frame-time change.

## Decision: Current External Multi-Domain Compile Wall
Problem: attempt39 after the span/bridge guard still cannot validate globally because other domains are failing: `TetherManager`, `EquipmentInteractionContracts`, `HectonPlayerMovement`, plus a concurrent `csc` file-lock warning.
Solution: Filtered attempt39 for scheduler/touched paths; there are zero hits in `Core\\Bucketing`, `Core\\Scheduling`, `SystemDispatcher`, `ModuloSimulationBucketer`, `JobAdmission`, `SimulationBucket`, `GlobalDataVault`, or `H8Memory`.
Rejected Alternatives: Patching tether, equipment interaction, or player movement from the scheduler prompt was rejected as out-of-domain ownership drift.
Scalability potential: Scheduler remains isolated; external domains can repair their contracts without changing bucket/admission code.
Hardware Impact: No runtime impact from this scheduler patch is measured. Current evidence: `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt39_span_bridge_guard.log`.

## Decision: Load Balancer Negative INF and Stale Mask Guard
Problem: The Burst rebalance loop skipped `cost <= 0f` before checking `math.isfinite`, allowing `-INF` to evade the non-finite flag if a vault cost slot was corrupted. The job also remasked a selected bucket with `BucketMask`, which is redundant and can collapse valid target buckets if a mismatched vault load span is smaller than the configured mask domain.
Solution: Moved the finite/negative guard before the zero-cost skip and wrote the chosen `targetBucket` directly after the bounds-safe min-load search.
Rejected Alternatives: Trusting cost EWMA writes from managed ingestion was rejected because DataVault contents can be stale or corrupted under platform failure. Keeping the redundant mask was rejected because target bucket is already bounded by `bucketCount`.
Scalability potential: Toaster mode fails finite and static instead of crashing. High/Ultra dynamic rebalance keeps exact bucket IDs when vault span clamping is active.
Hardware Impact: Measured microseconds saved: 0 us. This is NaN/INF crash prevention and ARM/Quest bounds hygiene, not a speed claim.

## Decision: Job Admission Refill and Cost Clamp
Problem: Admission refill used `baseRefillMs[lane]` directly for refill and cap math. A corrupted base-refill value, huge finite job completion sample, or overflow EWMA could poison lane budgets and black-box telemetry with INF.
Solution: Added a 1000 ms admission cost clamp and sanitized base refill, cap, estimated cost, measured completion cost, overflow cost, EWMA output, and black-box millisecond fields before persistent writes.
Rejected Alternatives: Clamping to 16.667 ms was rejected because impossible work must remain visibly impossible. Ignoring huge finite values was rejected because one persistent INF can poison downstream scheduling diagnostics.
Scalability potential: Low tier remains conservative under corrupted data; High/Ultra visual-overkill jobs cannot buy admission from poisoned budgets.
Hardware Impact: Measured microseconds saved: 0 us. This is finite-data survival for mobile/Steam Deck/PC, not a benchmarked optimization.

## Decision: Dispatcher Blackbox Stale Mirror Purge
Problem: The dispatcher black-box rationale said the fault dump was retargeted to `Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin`, but source still wrote a stale `Dump_CORE_TICK_DILATION.bin` mirror.
Solution: Removed the stale mirror constant and second write. Dispatcher crash evidence now has one owner path under the scheduler prompt ID.
Rejected Alternatives: Keeping dual dumps was rejected because it creates ownership ambiguity and doubles fault-path disk writes on slow storage.
Scalability potential: Steam Deck/MicroSD fault handling avoids duplicate write pressure; High/Ultra diagnostics consume the same single dump.
Hardware Impact: Normal runtime gain 0 us. Fault path saves one file write; no profiler benchmark was run.

## Decision: Isolated Build After Concurrent CSC Lock
Problem: attempt41 default-output build failed because another compiler process locked `Temp\\obj\\Hecton8.Core\\Hecton8.Core.dll` and sourcelink output. attempt44 returned `EXIT_CODE=-1` after restore with no compiler diagnostics after the final cleanup.
Solution: Ran attempt45 with isolated `BaseIntermediateOutputPath` and `OutputPath`; the current `Hecton8.Core.csproj` restored and built successfully with 0 warnings, 0 errors, EXIT_CODE=0.
Rejected Alternatives: Killing `csc`/dotnet processes from other agents was rejected. Reporting attempt41 or attempt44 as source failure was rejected because attempt45 proves the current source compiles.
Scalability potential: Validation-only; no low/high tier behavior change.
Hardware Impact: Compile validation only. No runtime microsecond claim.
