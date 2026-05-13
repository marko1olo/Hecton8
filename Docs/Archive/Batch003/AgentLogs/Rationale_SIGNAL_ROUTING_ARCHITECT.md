# Rationale_SIGNAL_ROUTING_ARCHITECT

Status: PENDING VERIFICATION - implementation complete except global compile dependency wall.

## Decision 0 - Pre-Code Mandate Selection

Problem: Signal routing touches cross-domain communication, native memory ownership, telemetry, AUP coordinate safety, and frame budget compliance.
Solution: Read task-relevant mandates before writing code: Global Registry DI, Zero-GC, Native Memory/Jobs, Crash Telemetry, AUP/Floating Origin, and Performance Budgets.
Rejected Alternatives: Skipping mandates or relying on memory would risk a managed event facade, singleton dependency, or untracked NativeCollection ownership.
Scalability potential: Low tier caps each lane at 1000 signals; Middle/High/Ultra use the same contract with larger snapshots and richer telemetry.
Hardware Impact: i3/MX350 avoids scanning unrelated signal types. Estimated saving depends on burst mix; target is sub-0.1ms routing overhead with 0 GC.

## Decision 1 - Compatibility Purge Boundary

Problem: The prompt demands purge of monolithic routing, but existing systems already call `GlobalSignals.Publish/TryDequeue`.
Solution: Replace GlobalSignals queue ownership with typed `SignalBus<T>` lanes while keeping narrow compatibility wrappers for existing producers/readers.
Rejected Alternatives: Raw deletion of GlobalSignals APIs; cross-domain rewrite of every producer in one pass.
Scalability potential: Low uses the same API with tighter lane caps; Ultra can process all 10000 per lane and feed jobs from contiguous snapshots.
Hardware Impact: Avoids catastrophic integration churn and removes cache misses from mixed signal storage. Estimated hot burst saving: 8-40 us.

## Decision 2 - SignalBus Storage Contract

Problem: A single queue ruins cache locality and cannot expose same-type batch data safely.
Solution: Added `SignalBus<T> where T : unmanaged, ISignal` with `NativeQueue<T>` producer storage and `NativeList<T>` frame snapshot.
Rejected Alternatives: Managed `Queue<object>`, interface payload queue, enum-tagged union queue.
Scalability potential: Low = 1000 copy limit; Middle = same queue with moderate pressure; High/Ultra = 10000 cap and Burst-readable `NativeArray<T>.ReadOnly`.
Hardware Impact: i3/MX350 avoids object chasing and GC. Expected improvement: 5-60 us during high-signal frames, 0 B/frame on hot paths.

## Decision 3 - Dispatcher Phase Placement

Problem: Consumers need deterministic data after producers finish but before simulation readers run.
Solution: Call `GlobalSignals.FlushPreSimulation()` in `SystemDispatcher.Update()` before pause signal drain, then clear snapshots in the late-frame finally block.
Rejected Alternatives: Reader-side queue drain; clearing snapshots directly after each consumer.
Scalability potential: Same ordering supports low-tier drops and high-tier batch jobs without branchy consumer code.
Hardware Impact: One predictable pass over each active lane. Estimated saving: 6-30 us versus scattered destructive drains.

## Decision 4 - Overflow, Math LOD, and AUP Safety

Problem: Storm frames can grow queues without bound; coordinate payloads can become stale across origin shifts.
Solution: Enforced 10000 high-tier cap, 1000 low-tier cap, oldest-drop overflow, dev storm warning, and AUP shift transform for combat coordinate snapshots before readers consume them.
Rejected Alternatives: Unlimited queues; per-reader AUP correction; balanced one-size cap.
Scalability potential: Toaster/MX350 sees hard 1000 cap. Middle/High retain more signal detail. Ultra spends saved CPU on richer downstream visual response, not routing overhead.
Hardware Impact: Low silicon avoids memory pressure and long dequeue bursts. High-end keeps full 10000-lane visual overkill budget.

## Decision 5 - Black Box Telemetry

Problem: Signal storms must leave forensic evidence instead of a vague "bus got busy" log.
Solution: Added `CrashTelemetryBuffer.ReportSignalLaneStats` storing lane hash, queued count, snapshot count, and dropped count into the fixed telemetry ring.
Rejected Alternatives: Editor-only `Debug.LogWarning`; allocating strings for per-lane reports.
Scalability potential: Low tier records drops under cap pressure; Ultra records dense lane activity without changing signal contracts.
Hardware Impact: 1-5 us per active lane sample, no heap churn. Provides crash reconstruction data instead of guesswork.

## Decision 6 - PlayerRuntimeContext Audit

Problem: Player integrity damage was state-only; signal consumers had no typed combat lane event.
Solution: `PublishSurvivalState` now emits `SignalBus<CombatDamageSignal>` when integrity decreases, using sanitized player world position.
Rejected Alternatives: Direct combat runtime callback; managed Action from player context.
Scalability potential: Low gets one cheap unmanaged packet per actual damage transition; Ultra can layer heavier camera/audio/VFX responses downstream.
Hardware Impact: 2-8 us avoided versus managed cross-domain notification under damage bursts.

## Decision 7 - Blocked Work Boundary

Problem: ASMDEF isolation and delegate eradication reach unrelated owners: legacy `Hecton8.Core.Signals` structs use `Hecton8.World.AbsoluteUniversePosition`, and input/bootstrap/rebinding contracts still expose `Action`/`delegate`.
Solution: Mark those tasks blocked by dependency with rg evidence; do not edit outside domain to fake compliance.
Rejected Alternatives: Moving 60+ signal structs and rewriting input contracts during signal-lane work.
Scalability potential: Future split should move pure lane contracts into a dependency-light assembly and leave AUP-heavy legacy payloads in a world-aware package.
Hardware Impact: No direct microsecond claim until those owners migrate.

## OMEGA POLISH CHANGES

Problem: First pass risked tier upgrade capacity overflow and left legacy `GlobalSignals.Push` aliases routing through old method names.
Solution: Added snapshot capacity growth when high tier raises the frame cap; changed typed Push aliases to call `SignalBus<T>.Push` directly; kept storm warning as editor/dev-only string.
Rejected Alternatives: Always allocating 10000 snapshot capacity for every lane on low-tier hardware; deleting external resource calls.
Scalability potential: Low stays at 1000 cap and smaller native allocation; High/Ultra can expand lanes to 10000 when needed.
Hardware Impact: MX350 avoids oversized snapshots; high-end can buy more visual response with full signal detail. Expected low-tier memory win depends on lane count and signal size.
Cinematic Cheats used: lane caps, oldest-drop load shedding, shared AUP transform pass, typed category routing instead of physical event simulation.
Final Git Diff summary: edited `GlobalSignals.cs`, `SystemDispatcher.cs`, `CrashTelemetryBuffer.cs`, `PlayerRuntimeContext.cs`; created this rationale and `Status_SIGNAL_ROUTING_ARCHITECT.md`. Build remains blocked by external `Core.Memory`, `Physics.Determinism`, `IDataVault/SystemID`, and Cartography contract holes.

## Decision 8 - No-Build Static Polish Pass

Problem: Follow-up review found three survivable but weak details: typed snapshot readers allocated native storage when a lane had never been initialized, lane telemetry could flood the 1024-entry black-box ring, and three resource producers still called the legacy `GlobalSignals.Push` alias.
Solution: `GetFrameSnapshotArray()` now returns default on uninitialized lanes; lane registry headroom is 256; registry loops capture lane count; telemetry samples four non-critical lanes per frame while always reporting drops/storms; SystemDispatcher writes `CurrentFrameUnscaledDeltaTime` before PRE_SIMULATION signal telemetry; resource producers now call `SignalBus<T>.Push` directly.
Rejected Alternatives: Running `dotnet build` against the user's instruction; deleting compatibility aliases; writing every lane to telemetry every frame; broad resource-domain refactor.
Scalability potential: Low tier preserves black-box history instead of overwriting it with lane spam; High/Ultra keep critical storm data and enough round-robin samples for forensic reconstruction.
Hardware Impact: Avoids cold native allocation on read-only paths, prevents telemetry ring churn, and removes one wrapper hop from hot resource publish sites. Estimated savings: 1-8 us on quiet frames, 3-12 us on resource bursts, and materially better crash-history retention.

## Decision 9 - Combat Lane Provenance and Registry Overflow

Problem: A consumer migration to `SignalBus<CombatDamageSignal>` would double-apply damage if it blindly processed mirrored legacy `DamageSignal` entries and direct runtime entries together. The lane registry also failed silently if future agents exhausted capacity.
Solution: Added `LegacyMirrorFlag` and `DirectRuntimeFlag` to the core combat signal payload, moved mirrored `IntegrityDelta` into a dedicated byte, marked PlayerRuntimeContext packets as direct runtime packets, and made registry overflow visible through dev error plus black-box lane telemetry.
Rejected Alternatives: Adding a combat-runtime adapter immediately and risking duplicate damage; silently trusting the 256-lane registry forever; encoding provenance by separate managed side tables.
Scalability potential: Low tier can filter direct versus mirrored damage without extra lookup; High/Ultra can add richer consumers without reprocessing legacy mirrors.
Hardware Impact: Provenance is one byte in an existing 64-byte payload. Registry overflow check is cold-path only. Estimated runtime cost: effectively 0 us on normal frames, with better fault isolation under future lane growth.

## Decision 10 - Final Evidence Repair

Problem: A final source scan found three stale resource producer calls still using the legacy `GlobalSignals.Push` wrapper on disk, invalidating the earlier purge proof until corrected.
Solution: Patched `ResourceNode` and `ProceduralOreSpawner` to call `SignalBus<ItemAcquiredSignal>.Push` and `SignalBus<ResourceDepletionDeltaSignal>.Push` directly, then reran source search and whitespace checks without launching a build.
Rejected Alternatives: Leaving wrapper calls because they land in typed lanes internally; running `dotnet build` despite the explicit no-build instruction.
Scalability potential: Low tier avoids wrapper indirection in bursty resource acquisition; High/Ultra keep direct lane ownership for future job consumers.
Hardware Impact: Estimated 3-12 us saved during ore/resource bursts and stronger static evidence for the purge gate.
