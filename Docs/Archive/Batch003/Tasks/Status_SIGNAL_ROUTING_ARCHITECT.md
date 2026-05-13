# Status_SIGNAL_ROUTING_ARCHITECT

Authority: ANOTHER_BATCH.md / SIGNAL_ROUTING_ARCHITECT
Domain: CORE & MEMORY INFRASTRUCTURE
Status: PENDING VERIFICATION - global compile blocked by unrelated assembly dependency holes.

## Mandates Read Before Coding

- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt

## Phase 1: The Great Purge

- [x] Task 1 - SINGLETON ERADICATION: `EventBus.Instance` audit found no remaining direct singleton access. | Justification: rg proof over Assets/_Project/Scripts; DOD used direct evidence search before deletion. | Alternatives Rejected: blind deletion of unknown event classes. | Estimate: 0.0 us current, prevents future singleton dispatch stalls.
- [x] Task 2 - SIGNAL MIGRATION: `rg GlobalSignals.Push` now returns no project call sites; resource producers call `SignalBus<T>.Push(in signal)` directly. | Justification: typed unmanaged lane is the storage path; compatibility wrappers remain only as cold API guardrails. | Alternatives Rejected: mass world-domain refactor beyond the three proven call sites. | Estimate: 3-12 us saved per hot publish under bursty resource events.
- [ ] Task 3 - ASMDEF ISOLATION: [BLOCKED BY DEPENDENCY] `Hecton8.Core.Signals` shares legacy AUP signal structs using `Hecton8.World.AbsoluteUniversePosition`. | Justification: new lane contract is project-domain clean but existing namespace is not separable without moving 60+ structs and consumers. | Alternatives Rejected: fake asmdef with unresolved AUP references. | Estimate: 0.0 us until integrator splits contracts.
- [ ] Task 4 - DEAD CODE HUNT: [BLOCKED BY DEPENDENCY] broad input/rebinding/bootstrap delegates remain outside signal-routing scope. | Justification: rg found `Action`/`delegate` surfaces in input and bootstrap contracts; deleting them here would break unrelated domains. | Alternatives Rejected: cross-domain rewrite without owner coordination. | Estimate: not claimed.

## Phase 2: Signal Lanes

- [x] Task 5 - LANE GENERICS: implemented `SignalBus<T> where T : unmanaged, ISignal`. | Justification: generic static storage creates one lane per signal type. | Alternatives Rejected: monolithic tagged union queue. | Estimate: 8-40 us saved when unrelated signal types spike.
- [x] Task 6 - NATIVE QUEUE BACKING: each lane owns `NativeQueue<T>` and `NativeList<T>` snapshot registered with `NativeMemorySentinel`. | Justification: NativeCollections preserve contiguous unmanaged storage and lifecycle tracking. | Alternatives Rejected: managed `Queue<T>` and `List<T>`. | Estimate: 5-25 us saved plus 0 GC under load.
- [x] Task 7 - CATEGORY LANES: added `CombatDamageSignal`, `WeatherChangedSignal`, `SystemPauseSignal`. | Justification: category payloads are fixed-size unmanaged structs with dedicated generic lanes. | Alternatives Rejected: enum-channel inside one generic signal. | Estimate: 4-18 us saved during combat/weather/pause bursts.
- [x] Task 8 - THE FLUSH: dispatcher calls `GlobalSignals.FlushPreSimulation()` before simulation-pause drain. | Justification: readers consume stable frame snapshots after PRE_SIMULATION. | Alternatives Rejected: lazy reader-side dequeue. | Estimate: 6-30 us saved by batch drain and cache locality.

## Phase 3: Contract Pinning & Consumption

- [x] Task 9 - IINITIALIZABLE PROTOCOL: added `IInitializable.OnRegister()` and `OnDependencyInject()`. | Justification: two-stage registry contract pins dependency order without singleton lookups. | Alternatives Rejected: constructor DI over Unity lifetime objects. | Estimate: 0.0 us hot path; boot-order risk reduction.
- [x] Task 10 - DECOUPLED READING: added `SignalBus<T>.GetFrameSnapshot()` and `GetFrameSnapshotArray()`. | Justification: consumers can read snapshots without owning producers. | Alternatives Rejected: destructive TryDequeue-only API. | Estimate: 5-20 us saved for multi-reader frames.
- [x] Task 11 - BATCH PROCESSING: snapshots are same-type contiguous `NativeList<T>` storage. | Justification: Burst jobs can use `NativeArray<T>.ReadOnly` from each lane. | Alternatives Rejected: mixed object payloads. | Estimate: 10-60 us saved on 100+ signal batches.
- [x] Task 12 - CLEARING: dispatcher clears snapshots in late-frame finally before arena reset. | Justification: POST_SIMULATION cleanup runs even under late-frame exceptions. | Alternatives Rejected: producer-side clear. | Estimate: 2-10 us saved by deterministic bounded clear.

## Phase 4: Safety & LOD

- [x] Task 13 - OVERFLOW PROTECTION: per-lane cap is 10000; oldest overflow is dropped; storm warning is dev-gated. | Justification: memory exhaustion prevented before snapshot copy. | Alternatives Rejected: unbounded NativeQueue growth. | Estimate: catastrophic frame avoided; normal overhead under 5 us.
- [x] Task 14 - AUP SHIFT SAFETY: AUP shift snapshot offsets combat coordinate snapshots before readers consume them. | Justification: transform pass runs immediately after PRE_SIMULATION flush. | Alternatives Rejected: reader-specific rebase logic. | Estimate: 4-15 us saved by one shared pass.
- [x] Task 15 - MATH LOD: low tier caps lane processing at 1000 signals/frame. | Justification: `SignalBusRegistry.LowTierMode` drives flush limit from `GlobalRegistry.ScalabilityTierProfileByte`. | Alternatives Rejected: single balanced cap. | Estimate: up to 90% routing work saved on MX350 storm frames.
- [x] Task 16 - ZERO-GC: hot push/read paths use unmanaged generics, NativeQueue, NativeList, and `ReadOnlySpan<T>`. | Justification: no boxing, no managed payload allocation in signal flow. | Alternatives Rejected: interface payload queue. | Estimate: prevents GC spikes; 0 B/frame intended.
- [x] Task 17 - BLACKBOX DUMP: signal lane counts are written to `CrashTelemetryBuffer` ring. | Justification: lane hash, queued count, snapshot count, and dropped count enter fixed telemetry. | Alternatives Rejected: string log-only storm reports. | Estimate: 1-5 us per active lane sample.
- [x] Task 18 - CROSS-DOMAIN AUDIT: `PlayerRuntimeContext` pushes integrity loss into `SignalBus<CombatDamageSignal>`. | Justification: player damage leaves runtime context as an unmanaged signal, not a direct dependency. | Alternatives Rejected: callback into combat runtime. | Estimate: 2-8 us saved versus managed event path.
- [ ] Task 19 - OMEGA COMPILE CHECK: [BLOCKED BY DEPENDENCY] `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` fails before clean validation due unrelated missing `Core.Memory`, `Physics.Determinism`, `IDataVault/SystemID`, and Cartography contracts. | Justification: two build attempts showed no signal-routing compile errors before the external wall. | Alternatives Rejected: stubbing other agents' dependencies. | Estimate: not claimed.

## Iteration Log

- Loop 1: Prompt extracted from ANOTHER_BATCH.md; mandates and domain read; status/rationale created.
- Loop 2: Existing GlobalSignals inspected; typed `SignalBus<T>` and registry added without deleting compatibility readers.
- Loop 3: Prompt re-extracted after task group; category lanes, dispatcher PRE_SIMULATION flush, and POST_SIMULATION clear wired.
- Loop 4: Safety audit found missing capacity growth and legacy Push alias route; both corrected.
- Loop 5: Compile attempted twice; global dependency wall documented; broad Action/delegate and asmdef isolation blockers recorded instead of cross-domain sabotage.
- Loop 6: No-build static polish pass. Expanded lane registry headroom, removed read-side cold allocation, rate-limited lane telemetry, moved unscaled delta before PRE_SIMULATION telemetry, and migrated remaining `GlobalSignals.Push` call sites.
- Loop 7: No-build correctness pass. Added lane registry overflow evidence, explicit combat-signal provenance flags, preserved mirrored integrity delta in its own byte, and re-verified `rg GlobalSignals.Push`/`Push(ISignal)`/`EventBus.Instance` returns no matches.
- Loop 8: Final no-build evidence repair. Static scan exposed three stale resource producer wrapper calls still on disk; patched them to `SignalBus<T>.Push(in signal)`, reran `rg GlobalSignals.Push|Push(ISignal)|EventBus.Instance` with no matches, and reran `git diff --check` with no whitespace errors.
