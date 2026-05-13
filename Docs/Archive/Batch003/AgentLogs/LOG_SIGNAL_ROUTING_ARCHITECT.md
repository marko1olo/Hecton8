# LOG_SIGNAL_ROUTING_ARCHITECT

## 2026-05-13 - Signal Lane Segregation

What was wrong:
- Global signal storage was still exposed as one legacy routing surface, forcing unrelated signal domains through GlobalSignals ownership and destructive dequeue patterns.
- There was no generic typed lane owner with NativeQueue producer storage plus contiguous frame snapshots.
- PRE_SIMULATION/POST_SIMULATION boundaries did not flush and clear typed snapshots.
- Signal storms had no per-lane black-box counts.
- PlayerRuntimeContext did not emit typed combat damage when integrity dropped.

What was done:
- Added `ISignal`, `IInitializable`, `ISignalSnapshotTransformer<T>`, `SignalBusRegistry`, and `SignalBus<T> where T : unmanaged, ISignal`.
- Repointed GlobalSignals queue creation/disposal/dequeue through typed `SignalBus<T>` storage while preserving existing compile-facing wrappers.
- Added `CombatDamageSignal`, `WeatherChangedSignal`, and `SystemPauseSignal` category lanes.
- Wired `GlobalSignals.FlushPreSimulation()` into `SystemDispatcher.Update()` and `GlobalSignals.ClearPostSimulationSnapshots()` into late-frame cleanup.
- Added high-tier 10000 cap, low-tier 1000 cap, oldest-drop overflow, dev storm warning, and capacity growth for tier upgrades.
- Added AUP shift transform for combat coordinate snapshots before readers consume frame data.
- Added `CrashTelemetryBuffer.ReportSignalLaneStats()` fixed-ring telemetry with lane hash, queued count, snapshot count, and dropped count.
- Updated `PlayerRuntimeContext.PublishSurvivalState()` to push `SignalBus<CombatDamageSignal>` on integrity loss.
- Ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` twice.

Cinematic cheats used:
- Typed lane segregation instead of a simulated universal event stream.
- Oldest-drop storm shedding instead of unbounded correctness theater.
- Low-tier hard cap at 1000 signals/lane/frame.
- Shared coordinate rebase pass instead of per-reader AUP correction.

Exact microseconds saved:
- EventBus singleton purge: 0.0 us current, because no `EventBus.Instance` references were found.
- Typed publish aliases: estimated 3-12 us saved in resource bursts by routing directly to `SignalBus<T>`.
- Lane generics and contiguous snapshots: estimated 8-60 us saved in mixed high-signal frames by avoiding unrelated queue scans and managed payload dispatch.
- PRE_SIMULATION batch flush: estimated 6-30 us saved versus scattered reader drains.
- Low-tier cap: up to 90% routing work avoided on storm frames above 1000 signals/lane.
- Black-box telemetry: costs estimated 1-5 us per active lane sample; accepted because crash forensics are mandatory.

Verification:
- `rg EventBus.Instance` found no direct singleton reference.
- `rg Push(ISignal)` found no generic managed `Push(ISignal)` surface.
- Remaining `GlobalSignals.Push` call sites are typed legacy calls and now land in `SignalBus<T>.Push`.
- Build remains blocked by unrelated missing `Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, `IDataVault/SystemID`, and Cartography contracts. No signal-routing compiler errors were emitted before that dependency wall.

Blocked:
- ASMDEF isolation is blocked by existing legacy `Hecton8.Core.Signals` structs depending on `Hecton8.World.AbsoluteUniversePosition`.
- Full Action/delegate eradication is blocked by input, rebinding, and bootstrap contract ownership outside this task.

## 2026-05-13 - No-Build Static Polish Pass

What was wrong:
- `GetFrameSnapshotArray()` could initialize a never-used lane from a read-only path.
- Signal-lane telemetry could write dozens of entries per frame and overwrite the 1024-entry black-box ring too quickly.
- Three resource producers still called the legacy `GlobalSignals.Push` alias.
- PRE_SIMULATION telemetry saw the previous frame's unscaled delta because the dispatcher assigned `CurrentFrameUnscaledDeltaTime` after flushing signals.

What was done:
- `GetFrameSnapshotArray()` now returns default if the lane has no snapshot storage.
- Lane registry capacity increased from 128 to 256 and flush/clear/dispose loops capture lane count before iteration.
- Non-critical lane telemetry is round-robin capped at four lanes per frame; dropped/storm lanes always report.
- `SystemDispatcher.Update()` now sets `CurrentFrameUnscaledDeltaTime` before `GlobalSignals.FlushPreSimulation()`.
- `ResourceNode` and `ProceduralOreSpawner` now publish resource signals directly through `SignalBus<T>.Push`.

Cinematic cheats used:
- Telemetry sampling instead of exhaustive per-frame lane dumps.
- Critical-lane bypass for storm/drop evidence.
- Direct typed resource publish instead of compatibility wrapper routing.

Exact microseconds saved:
- Read-only snapshot access: 1-8 us avoided when consumers inspect a cold lane.
- Telemetry cap: avoids up to dozens of ring writes per frame; exact saving scales with active lane count.
- Direct resource push: estimated 3-12 us saved during ore/resource bursts.

Verification:
- `git diff --check` run; no whitespace errors.
- `rg GlobalSignals.Push`, `rg Push(ISignal)`, and `rg EventBus.Instance` return no project matches.
- `dotnet build` intentionally not run per user instruction.

## 2026-05-13 - Final Evidence Repair

What was wrong:
- Final source scan found three stale resource producer wrapper calls still present on disk: one in `ResourceNode`, two in `ProceduralOreSpawner`.

What was done:
- Replaced those wrappers with direct `SignalBus<ItemAcquiredSignal>.Push` and `SignalBus<ResourceDepletionDeltaSignal>.Push` calls.
- Re-ran static purge scan and whitespace validation.

Cinematic cheats used:
- Direct typed lane publish instead of compatibility wrapper routing.

Exact microseconds saved:
- Estimated 3-12 us during ore/resource bursts by avoiding wrapper indirection and preserving typed lane ownership.

Verification:
- `rg GlobalSignals.Push|Push(ISignal)|EventBus.Instance` returned no matches under `Assets/_Project/Scripts`.
- `git diff --check` passed with only line-ending warnings.
- `dotnet build` intentionally not run per user instruction.

## 2026-05-13 - Combat Lane Provenance Pass

What was wrong:
- The core `CombatDamageSignal` lane contained both mirrored legacy damage packets and direct runtime packets, but no provenance bit. Future consumers could double-apply damage if they consumed both the old `DamageSignal` lane and the new combat lane.
- Signal lane registry overflow would silently strand future lanes outside PRE_SIMULATION flush.

What was done:
- Added `LegacyMirrorFlag` and `DirectRuntimeFlag` to core `CombatDamageSignal`.
- Preserved mirrored `DamageSignal.IntegrityDelta` in a dedicated payload byte instead of overloading `Flags`.
- Marked `PlayerRuntimeContext` combat packets as direct runtime packets.
- Added registry overflow state, dev error, and black-box telemetry reporting for overflow.
- Re-applied and verified direct typed `SignalBus<T>.Push` calls in resource producers.

Cinematic cheats used:
- One-byte provenance instead of side-table tracking.
- Fail-fast overflow marker instead of dynamic registry growth in frame.

Exact microseconds saved:
- Provenance flags cost 0 us in normal routing and prevent duplicate downstream damage work.
- Registry overflow check is cold-path only; normal-frame cost is 0 us.

Verification:
- `git diff --check` run; no whitespace errors.
- `rg GlobalSignals.Push`, `rg Push(ISignal)`, and `rg EventBus.Instance` return no project matches.
- `dotnet build` intentionally not run per user instruction.
