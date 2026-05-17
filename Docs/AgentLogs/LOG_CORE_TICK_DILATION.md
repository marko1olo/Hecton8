# CORE_TICK_DILATION Log

## 2026-05-16

What was wrong:
- `CORE_TICK_DILATION` was not present in active `Docs/Tasks/CURRENT_BATCH.md`; assignment was recovered from deprecated prompt dump under explicit user override and recorded as a batch mismatch.
- `SystemDispatcher` already contained most dispatcher requirements, but typed signal lanes still flushed simulation snapshots during pause and `AupPreShiftSignal` did not directly request the dispatcher AUP one-frame pause.
- `BootstrapStatus.TryTriggerSafeHalt()` still set `Time.timeScale = 0f`, creating a second clock authority outside the dilated dispatcher.
- Full `Hecton8.Core.csproj` compile is blocked by out-of-domain syntax errors in `Assets/_Project/Scripts/SubmarineFluidDynamics.cs`.

What was done:
- Added `SignalLanePolicyCache<T>.FlushDuringSimulationPause` and `ISignalLane.FlushDuringSimulationPause`.
- Updated `SignalBusRegistry.FlushPreSimulation()` to skip non-immune typed lanes while `GlobalSignals.SimulationPaused` is true. UI/system/AUP/telemetry lanes still flush.
- Updated `GlobalSignals.Publish(in AupPreShiftSignal)` to call `SystemDispatcher.ActiveRuntimeInstance.RequestAupPreShiftPause(...)`.
- Changed safe halt `Time.timeScale` from `0f` to `1f`; scripted physics halt remains.
- Created `Docs/Tasks/Status_CORE_TICK_DILATION.md`, `Docs/AgentLogs/Rationale_CORE_TICK_DILATION.md`, and `Docs/Tasks/RECON_CORE_TICK_DILATION.md`.
- Ran OMEGA polish: no owned `foreach`, `string.Format`, interpolated strings, or `.ToString()` offenders found in modified owned code.

Cinematic Cheats used:
- Bullet time remains a cheap post-process scalar, not a second simulation path.
- Pause freezes simulation lane snapshots instead of solving or draining gameplay queues.
- Safe halt freezes scripted physics while Unity global time stays neutral at 1.0.

Exact microseconds saved:
- Avoided duplicate dispatcher layer: estimated 0.05-0.15 us/frame on i3/MX350.
- Pause lane gate: estimated +0.005 us per active lane while unpaused; during pause it avoids gameplay snapshot copies, negative net cost under signal load.
- AUP pre-shift hook: 0 us/frame; one null-check only when pre-shift fires.
- Safe halt timeScale correction: 0 us; removes hidden clock desync.

Verification:
- `dotnet build Hecton8.Bootstrap.Contracts.csproj --no-restore`: PASS, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj`: BLOCKED BY DEPENDENCY, 187 syntax errors in `SubmarineFluidDynamics.cs:2067-2095` and `:4926` before owned scheduling validation can complete.
- Delay scan: no `Task.Delay` under `ITickable.cs`, `SystemDispatcher.cs`, or `GlobalSignals.cs`.
- Recon scan logged remaining out-of-domain `Time.fixedDeltaTime` reads for physics/fauna owners.

Final status:
- PENDING due global compile dependency. Owned scheduling patch complete and documented.

## 2026-05-16 - Multiplatform/Data Sovereignty Pass

What was wrong:
- Dispatcher native payloads needed explicit binary layout for ARM64/Quest and IL2CPP.
- Dispatcher deferred raycast staging still used local persistent native containers, violating DataVault sovereignty.
- Dispatcher had memory/vault heartbeats but no owned 300-frame cadence blackbox with pause/AUP/raycast flags.
- `H8Time` accepted raw Unity/XR delta values; a non-finite delta could poison downstream physics/rendering.

What was done:
- Added Pack=1 fixed-size layout to `H8TimeSnapshot`, `CriticalMemoryPressureEvent`, and the 64-byte dispatcher blackbox entry.
- Added DataVault IDs `SystemDispatcherRaycastPendingCommands`, `SystemDispatcherRaycastScheduledCommands`, `SystemDispatcherBlackBox`, and `SystemDispatcherBlackBoxCursor`.
- Moved dispatcher raycast pending/scheduled command storage to DataVault `NativeArray` handles and locked scheduled command/hit buffers while a scheduled batch is in flight.
- Added dispatcher blackbox ring export to `Docs/AgentLogs/Dump_CORE_TICK_DILATION.bin` on non-finite detection.
- Sanitized unscaled/dilated dt before writing `H8Time`.

Cinematic Cheats used:
- No physical slow-motion simulation added. Bullet time stays as signal-driven post/VFX currency.
- Toaster path keeps bounded arrays, no post override on low tier, no scheduler-side visual truth.
- God-mode path remains free to spend saved scheduler cost on VISUAL_SYNC systems such as volumetric silt/salt/visor effects; dispatcher stays deterministic.

Exact microseconds saved:
- DataVault raycast arrays remove NativeQueue dequeue overhead under dispatcher raycast pressure: estimated 0.01-0.03 us/frame when pending raycasts exist.
- Normal no-raycast frames: 0 us change for raycast staging.
- H8Time finite guard: estimated +0.004 us/frame.
- Dispatcher blackbox heartbeat: estimated +0.02 us/frame, 19.2 KB DataVault memory, 0 B/frame managed allocation.

Verification:
- `dotnet build Hecton8.Bootstrap.Contracts.csproj`: PASS after restore, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj`: BLOCKED BY DEPENDENCY after restore by missing out-of-domain symbols `HectonEcologyContract`, `ScalabilityContract`, and `HectonPhysicsContract`.
- `dotnet build Assembly-CSharp.csproj`: BLOCKED BY DEPENDENCY / timed out after vendor RealtimeCSG missing source file errors.
- Static scan over `SystemDispatcher.cs` and `ITickable.cs`: no `NativeQueue<`, `NativeList<`, `new NativeArray`, `Task.Delay`, `string.Format`, or `.ToString(` offenders found.

Final status:
- PENDING VERIFICATION. Owned CORE/SCHEDULING static checks pass; full compile waits on AI/Physics/vendor dependency owners.

## 2026-05-16 - Recursive Adrenaline Pass

What was wrong:
- The recursive prompt item for Adrenaline dilation was still not implemented.
- Direct player-health polling would couple CORE/SCHEDULING to gameplay physiology and duplicate the existing health/stress lane.

What was done:
- `SystemDispatcher` now consumes the existing `SystemHealthIndexSignal` typed lane via `ReadOnlySpan`.
- When `Health01 <= 0.1` or `FlagAdrenaline` is present, the dispatcher ramps `TimeDilationScalar` toward 0.5 over 1 unscaled second.
- When the signal pressure clears, it restores the previous scalar over 1 unscaled second.
- Pause, headless/manual time dilation, and core hit-stop remain higher authority; adrenaline clears or yields instead of fighting them.

Cinematic Cheats used:
- Adrenaline is a scalar-driven time fake, not a second simulation or physiology solver.
- Bullet-time visual overkill still rides the existing `BulletTimeVisualSignal`.

Exact microseconds saved:
- Reused `SystemHealthIndexSignal`: avoids a new lane and duplicate queue allocation.
- Idle cost: estimated +0.003 us/frame for span access and length check.
- Worst 16-signal scan: estimated +0.02 us/frame.
- Active ramp: rare scalar publish once per frame for roughly 60 frames, 0 B/frame.

Verification:
- Owned scan: no `Update`, `FixedUpdate`, `LateUpdate`, `foreach`, `string.Format`, `.ToString(`, `Task.Delay`, local `NativeQueue`/`NativeList`, `new NativeArray`, `math.sqrt`, or `math.normalize` offenders in scheduler files/diff.
- `dotnet build Hecton8.Core.csproj --no-restore`: BLOCKED BY DEPENDENCY / timed out after 120s with missing out-of-domain `HectonPlatformContract`, `HectonDataSovereigntyContract`, and `HectonVisualOverkillContract` errors in `HectonContractValidator`.

Final status:
- PENDING VERIFICATION. New adrenaline scheduler code is statically clean; full compile remains blocked by external contract files.

## 2026-05-17 - Reinquisition Hot-Path / Blackbox Pass

What was wrong:
- Dispatcher blackbox export path had drifted to `Dump_SIMULATION_BUCKET_DISTRIBUTOR_Dispatcher.bin`, contradicting CORE_TICK_DILATION's required `Dump_[AgentID].bin`.
- `RunDispatcherUpdate` still read `GlobalRegistry.InputDeterminism` and `GlobalRegistry.ScalabilityTierProfileByte`; late-frame pause DOF still read `GlobalRegistry.CameraJuice`.
- `ScalabilityEvents` had only 16 listener slots while the source tree contains 27 files implementing `IScalabilityChangedEventListener`.

What was done:
- Primary dispatcher blackbox dump restored to `Docs/AgentLogs/Dump_CORE_TICK_DILATION.bin`; SIM_BUCKET mirror retained for neighboring scheduler evidence.
- Added binary dump header: `HECTON8\0` magic, version, entry count, 64-byte entry size, cursor, then 300 entries.
- Cached `IInputDeterminismService` and `ICameraJuiceSystem`; absent CameraJuice lookup retries once per 30 frames.
- Dispatcher now seeds scalability tier once and receives later changes through existing `ScalabilityEvents`; frame loop reads `_scalabilityTierProfileByte`.
- Widened `ScalabilityEvents` cold listener/deferred arrays from 16 to 32.

Cinematic cheats used:
- No new physical simulation. Scheduler remains cadence authority only.
- Slow motion and adrenaline still sell through existing bullet-time visual signals; high-tier visual overkill remains in VISUAL/VFX systems.

Exact microseconds saved:
- Measured proof absent. Estimated savings are below profiler resolution: <0.002 us/frame per removed registry property read.
- CameraJuice absent path drops from 60 registry reads/s to 2 reads/s.
- Blackbox dump change costs 0 us normal frames; fault path writes fixed 19.2 KB ring plus 20-byte header.

Verification:
- `rg` scan on `SystemDispatcher.cs` and `ITickable.cs`: no raw `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, `.ToString(`, `foreach`, `Task.Delay`, `new NativeArray`, local `NativeQueue`/`NativeList`, `math.sqrt`, `math.normalize`, or `GlobalRegistry.Get`.
- `git diff --check` on touched scheduler/core files reports only LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore` timed out after 126s; no-reuse/shared-compilation-disabled retry timed out after 304s. Both logs were 0 bytes. Build servers were shut down and lingering dotnet processes were killed.

STATUS: PENDING VERIFICATION - static scans pass; Core build command currently hangs before diagnostics in this workspace; Unity import/play/build proof absent.

## 2026-05-17 - Typed Scalability SignalBus Bridge

What was wrong:
- Dispatcher scalability state was still updated through `IScalabilityChangedEventListener`. That is decoupled, but it is not the strict typed-lane/`ReadOnlySpan<T>` pattern requested for the scheduler.
- The existing platform event payload was packed but was not an `ISignal`, so using it through `SignalBus<T>` was impossible.
- The source still had a stale SIM-bucket-only blackbox dump path in the dispatcher constant.

What was done:
- `ScalabilityChangedEvent` now implements `ISignal` while keeping `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 2)]`.
- `ScalabilityEvents.Raise` now pushes the same payload into `SignalBus<ScalabilityChangedEvent>` with a configured 4-event lane and `SCLT` lane hash.
- `SystemDispatcher` no longer implements/registers/unregisters `IScalabilityChangedEventListener`.
- Dispatcher drains `SignalBus<ScalabilityChangedEvent>.GetFrameSnapshot()` after `GlobalSignals.FlushPreSimulation()` and uses the resulting cached tier for job admission and simulation bucketing.
- `ScalabilityChangedEvent` flushes during simulation pause, so platform tier changes are not blocked by `TimeDilationScalar == 0`.
- Dispatcher blackbox primary path is again `Docs/AgentLogs/Dump_CORE_TICK_DILATION.bin`, with the SIM bucket mirror retained.

Cinematic cheats used:
- No physical simulation added. Scheduler still only moves cadence, pause, and tier state.
- Low-tier path keeps the bullet-time post override disabled; high/ultra presentation remains the place to spend saved cycles on heavier visuals.

Exact microseconds saved:
- No measured profiler proof. Expected idle overhead is below 0.002 us/frame for the empty `ReadOnlySpan` check.
- Event path is rare and capped at 4 scalability payloads.
- Blackbox mirror costs 0 us on normal frames; fault path writes the fixed binary ring twice.

Verification:
- No dotnet rebuild was run in this pass per user instruction.
- `rg` confirmed `SystemDispatcher` no longer references `IScalabilityChangedEventListener` or `_scalabilityEventsRegistered`.
- `rg` confirmed `ScalabilityChangedEvent : ISignal`, `SignalBus<ScalabilityChangedEvent>.Push`, `SignalBus<ScalabilityChangedEvent>.Configure`, and dispatcher `GetFrameSnapshot()` consumption.
- `git diff --check` on touched files reports only LF-to-CRLF warnings.

STATUS: PENDING VERIFICATION - static code evidence only; Unity import/play/build proof absent.
