# SIGNAL DTO Contract And Direct TryPush Closure - X_001

Date: 2026-05-25
Agent: X_001
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor

## Scope

This pass closed two remaining signal-corridor weaknesses:

1. Multi-owner lanes that could be initialized before their local owner `Configure(...)` call now have DTO-owned capacity/hash contracts plus `SignalBus<T>` cold default policy entries.
2. External runtime bool-return `SignalBus<T>.TryPush(...)` edges were converted to `TryPushTracked(...)`, so refusal is counted at the lane direct-edge counter even when the wrapper returns the bool to its caller.

Runtime/code files touched in this closure: 54.

## DTO-Owned Contract Closure

Added `ExpectedCapacity`, `MaxFrameSignals`, `LowTierFrameSignals`, and stable lane hash constants to 41 unmanaged DTO lanes:

- Audio/music: `DynamicMusicScalarSignal`.
- Construction: `ConstructionPreviewSignal`, `FloraExclusionSignal`.
- Seaglide: `SeaglidePropulsionRequestSignal`.
- Core health/runtime/presentation: `SystemHealthSignal`, `FrameTimeSignal`, `KillSwitchSignal`, `SystemKillSwitchBitsSignal`, `ReentryVfxStateSignal`, `VisorDropletSignal`, `VisualFlareSignal`, `PlayerFootstepSignal`, `PlayerWaterSplashSignal`, `WaterTransitionSignal`, `PlayerExhaleSignal`, `PlayerSprintStateSignal`, `PlayerFatalPressureSignal`, `PlayerTransportBailoutSignal`.
- Seismic/environment: `SeismicSignal`, `MockNarrativeTriggerSignal`, `DebrisAvalancheSignal`, `AcousticShockwaveSignal`, `GlobalPanicSignal`, `SeismicShockwaveSignal`, `EclipseGameplayEventPayload`.
- Inventory/economy: `MockItemAcquiredSignal`, `MockCraftingRequestSignal`, `MockConsumeSignal`, `MockToolUsedSignal`, `ToolBrokenSignal`, `EncumbranceSignal`, `EquipItemSignal`, `MockHotbarSelectSignal`, `DebrisDestroyedSignal`.
- Tool kinematics: `MockTriggerPullSignal`, `ToolHeatSignal`, `VfxSparkRequestSignal`, `MockCarveRequestSignal`.
- Terminal OS: `TerminalClickSignal`, `TerminalCommandSignal`, `TerminalUnlockedSignal`.

`SignalLanePolicyCache<T>` now resolves these contracts before first native lane initialization. This prevents an early producer from creating the native queue with generic defaults and then rejecting the real owner `Configure(...)` as a late reconfigure.

All selected owner configure call sites were changed to use DTO constants instead of local magic numbers or ad-hoc lane hashes.

## Direct TryPush Closure

`SignalBus<T>` now exposes:

- `DirectTrackedDropTotal`: per-lane `int` counter for direct/bool-return tracked edges.
- `TryPushTracked(in T signal)`: no-ref overload that records refusal on `DirectTrackedDropTotal`.
- Existing `TryPushTracked(in T signal, ref int ownerDroppedSignalCount)` remains the owner-local black-box route.

Mechanical conversion:

- Files converted from direct external `TryPush(...)`: 35.
- External runtime replacements: 66.
- Total project `TryPushTracked(...)` call sites after closure: 487.
- No-ref direct tracked call sites after closure: 65.
- External runtime direct `SignalBus<T>.TryPush(...)` call sites after closure: 0.

Existing call sites that already incremented owner counters on `false` still do so. The new no-ref tracked path adds lane-level direct-edge refusal telemetry without managed logs, dictionaries, delegates, or heap sidecars.

## Capacity And Overflow Strategy

For every touched lane, admission is bounded before or during native enqueue:

- Main-thread producers: `SignalBus<T>.TryPush(...)` rejects before enqueue when `_queue.Count >= _expectedCapacity`.
- Tracked producers: `TryPushTracked(...)` increments either caller-owned refusal telemetry or `DirectTrackedDropTotal` on rejection.
- Job producers: existing `TryEnqueueBounded(...)` claims a lane-owned `NativeArray<int>` budget before `NativeQueue<T>.ParallelWriter.Enqueue(...)`.
- Non-critical VFX lanes can shed when `SystemStressMilli` exceeds the policy threshold.
- Finite guards sanitize signal payloads before enqueue and reject corrupt/non-finite payloads.
- Flush phase applies `MaxFrameSignals`/`LowTierFrameSignals` and lane policy coalescing where present.

Under a 5000-signal burst, the lane accepts at most the configured native budget before pre-enqueue rejection or frame-snapshot shedding. Storm lanes with coalescing policy mutate existing native snapshot entries; non-coalescing lanes drop deterministically. No managed field, string carrier, managed dictionary, or scene reference is introduced into the DTO path.

## Verification

Commands and results:

- External runtime hot-route scan:
  - Pattern: `GlobalSignals.Publish/Push/TryDequeue/*Writer`, `SignalBus<T>.Push`, `SignalBus<T>.TryPush`, first-party `HectonEventBus.Publish/Subscribe/Unsubscribe`, `ThreadSafeCommandQueue.Enqueue`.
  - Result: `HOT_ROUTE_HITS=0`.
- DTO field scan:
  - Scanned runtime `ISignal` structs for `GameObject`, `Transform`, `string`, `FixedString*`, `NativeArray<>`, `NativeQueue<>`, `NativeList<>`, `NativeHashMap<>` fields.
  - Result: `DTO_BANNED_FIELD_HITS=0`.
- Selected contract configure scan:
  - Checked the 41 touched DTO lanes for numeric or hex magic `Configure(...)` starts.
  - Result: `SELECTED_MAGIC_CONFIGURE_HITS=0`.
- Touched-file brace scan:
  - Result: `TOUCHED_FILE_COUNT=54`, `BRACE_DELTA_HITS=0`.
- `git diff --check` over touched files:
  - Result: no whitespace errors; LF-to-CRLF warnings only.
- Build guard:
  - Final result: `FINAL_BUILD_GUARD cpu=41.9 compiler_count=2`.
  - Active compiler/build processes: `csc` PID 50660, `dotnet` PID 54776.
  - Per `AGENTS.md`, `dotnet build` was not launched while another compiler/build process was active.

## Runtime Claims

Verified runtime microsecond saving: 0us. No Unity profiler or GCMonitor capture was run in this pass.

Static proof:

- The touched DTOs remain unmanaged and hash/id based.
- The tracked direct-edge counter is a static `int` per closed generic lane.
- No new managed allocation path was added to hot producers.
- 5000-signal overload enters fixed native admission, deterministic coalescing, or deterministic drop paths.
