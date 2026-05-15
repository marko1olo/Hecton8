# HABITAT_O2_SCRUBBER_LOD Log

## 2026-05-15 - Dalton Base Hibernation Pass
STATUS: PENDING VERIFICATION

What was wrong:
- Existing Dalton gas solver advanced every room and diffusion edge regardless of base distance/occupancy.
- Power graph evaluation had no native awake-mask hook, so outpost power could keep solving while the base was uninspectable.
- No typed player-base transition lane existed for atmosphere hibernation.
- Base awake state was not exposed through H-Phi/DataVault.

What was done:
- Added typed unmanaged `PlayerBaseEnterSignal` and `PlayerBaseExitSignal` lanes with finite AUP guards.
- Expanded `IGasDynamicsSolver` with base hibernation snapshots/configuration and read-only `BaseAwakeState`.
- Added `BaseAwakeState`, room-to-base mapping, base center AUP, player-inside flags, hibernation timestamps, battery watt-seconds, idle draw, leak rate, and ambient O2 lanes.
- Stored `BaseAwakeState` through `GlobalDataVault` via `BufferID.HabitatBaseAwakeState` and `SystemID.HabitatAtmosphere`, with local fallback only if the vault is unavailable.
- Registered the solver as `IFrostTickable`; FrostTick drains base signals, applies AUP distance hibernation with hysteresis, records dispatcher unscaled time, and wakes on inside/near events.
- Gas Burst job skips metabolism and bulkhead diffusion for rooms whose base awake byte is 0; telemetry records sleeping rooms in the fixed ring.
- Added Burst `BaseHibernationWakeCatchUpJob`: `alpha = 1 - exp(-elapsed * leakRate)`, battery drain = `idleDrawWatts * elapsedSeconds`, battery clamp to 0, and O2 forced to 0 on depleted battery.
- Added power graph read-only awake-mask binding and WFC outpost power boot binding to gas base 0 when available.

Cinematic Cheats used:
- Analytical O2 decay replaces replaying every missed gas diffusion tick.
- FrostTick distance hibernation replaces per-frame distance checks.
- Power graph hibernation returns stable summary state instead of simulating invisible Jacobi relaxation.

Exact microseconds saved:
- Measured proof absent. Static model: 20-80us per sleeping 64-room gas base per gas cadence, plus avoided bound power Jacobi slices. Signal batch cost model: transition packets avoid managed observer traversal; empty frames cost snapshot-count checks. No Unity profiler or Burst console was run because the user explicitly disallowed dotnet rebuilds and no Unity MCP compiler is available.

Blocked items:
- ASMDEF isolation is blocked by existing `GlobalRegistry` concrete references to `HectonSurfaceWeatherDirector` and `HectonAtmosphereManager`; a real split requires integrator interface work first.
- Burst `math.exp` compile verification is blocked by the no-rebuild/no-Unity-tool constraint. Code path is present in a `[BurstCompile]` job, but verification remains pending.

## 2026-05-15 - Static Recheck Addendum
STATUS: PENDING VERIFICATION

What was rechecked:
- Re-read the modified gas solver hibernation state, signal drain, wake catch-up job, native disposal, and telemetry gates.
- Re-read the power graph awake-mask binding and WFC gas binding.
- Re-read GlobalSignals payload sizing and H-Phi buffer identifiers.

What was found:
- No new code patch required from this pass.
- `ReadOnlySpan<T>` signal snapshots and `NativeArray<T>.ReadOnly` job fields are already established in the project.
- `AbsoluteUniversePosition` is explicitly 48 bytes, so the 64-byte base enter/exit signal layout is correctly bounded.
- Touched-file `git diff --check` exits 0 with CRLF warnings only.

Exact microseconds saved:
- No additional runtime saving claimed; this pass was risk reduction. Dotnet rebuild was not run.

## 2026-05-15 - H-Phi Hygiene Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Gas solver cold-initialized base transition signal lanes even though `GlobalSignals` owns lane capacity/hash configuration.
- `TryConfigureBase` could leave stale room-to-base mappings when a nonzero base was reconfigured.
- A newly configured base could inherit a sleeping byte and old hibernation timestamp, causing first wake to charge against full session uptime.
- Black-box dump filename was generic instead of agent-specific.

What was done:
- Removed consumer-side signal lane initialization; empty snapshot reads remain safe until owner initialization.
- Kept `HabitatBaseAwakeState` on append-only `BufferID` 63.
- Cleared stale nonzero base room mappings before remap and initialized new bases awake at dispatcher time.
- Changed gas black-box output to `Docs/AgentLogs/Dump_HABITAT_O2_SCRUBBER_LOD.bin`.

Cinematic Cheats used:
- None new. This was H-Phi and cold-path correctness work.

Exact microseconds saved:
- Frame-time saving not claimed. Cold allocation pressure is lower because the solver no longer prewarms two signal lanes in scenes without base transition traffic. No dotnet rebuild was run.

## 2026-05-15 - Signal Producer Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Atmosphere hibernation consumed base transition signals, but no runtime module producer emitted them.

What was done:
- `BaseModule` now publishes `PlayerBaseEnterSignal` when the player is confirmed inside a dry module trigger.
- `BaseModule` now publishes `PlayerBaseExitSignal` when the tracked player leaves the module interior.
- Payloads use AUP center from the module interior probe, current frame, resolvable room id, and base id 0 as the current single-base bridge.

Cinematic Cheats used:
- None new. This is a decoupled event bridge.

Exact microseconds saved:
- No frame-time claim. This avoids gas polling gameplay modules and keeps wake override event-driven. No dotnet rebuild was run.

## 2026-05-15 - Same-Frame Handoff Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Enter and exit signals were drained in an order that could let a same-frame module exit override a same-frame module enter.

What was done:
- Gas drains exit packets first, then enter packets. Enter wins for module-to-module trigger handoffs.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No runtime saving claimed; packet count is unchanged. No dotnet rebuild was run.

## 2026-05-15 - Native Inside Count Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- A base-level boolean could still be wrong if enter and exit signals for overlapping modules arrived on adjacent frames.

What was done:
- Added `_basePlayerInsideCount` as a native per-base SOA lane.
- Enter signals increment the count; exit signals decrement it with a zero clamp.
- `_basePlayerInside` is now derived from count for signal traffic, while direct API setters remain explicit authority.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No frame-time saving claimed. Cost is one int lane per base and one integer update per transition packet. No dotnet rebuild was run.

## 2026-05-15 - H-Phi Audit Addendum
STATUS: PENDING VERIFICATION

What was checked:
- Ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary`; first two-minute run timed out, longer retry completed.

Result:
- Evidence class: STATIC_SOURCE.
- RuntimeHPhiRisk: 0.000573574.
- DataSovereignty: 0.021057035.
- DataVaultRefs: 151.
- NativeArrayRefs: 7020.

Decision:
- Kept DataVault ownership scoped to `BaseAwakeState`. Moving the front/back Dalton gas arrays now would require generation-handle and job-swap redesign outside this prompt.

Exact microseconds saved:
- None claimed. This was audit evidence, not runtime work. No dotnet rebuild was run.

## 2026-05-15 - Signal Drain Reliability Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Base transition signals lived in frame snapshots, but gas consumed them only on FrostTick. The dispatcher clears snapshots every frame.

What was done:
- `GasDynamicsSolver` now registers as `IUpdatable`.
- Per-frame `Tick` drains base transition snapshots destructively with `SignalBus.TryReadFrame`.
- Wake catch-up runs only when the gas step is not active; otherwise inside state is retained and wake occurs after the step completes.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No saving claimed. Empty-case cost is two snapshot-count checks per frame; this buys deterministic signal capture. No dotnet rebuild was run.

## 2026-05-15 - Deferred Re-enable Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- A solver re-enable during deferred disposal could reach tick-created native state without fresh cached dependencies.

What was done:
- Tick, FixedTick, and FrostTick refresh cold dependencies before native state creation when `IsInitialized` is false.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No frame saving claimed. This protects H-Phi `BaseAwakeState` allocation from falling back locally after a deferred disposal edge case. No dotnet rebuild was run.

## 2026-05-15 - Cold Registry Publication Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Tick or FrostTick could be the first lane to recreate native gas state after deferred disposal, but `IGasDynamicsSolver` publication still waited for FixedTick.

What was done:
- Tick and FrostTick now seed standard atmosphere and call `TryRegisterRegistry` immediately after native state creation.
- This keeps GlobalRegistry visibility aligned with OnEnable and FixedTick even under pause/time-dilation edges.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No frame saving claimed. This is lifecycle correctness and H-Phi visibility hygiene; no dotnet rebuild was run.

## 2026-05-15 - Wake Catch-Up Guard Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- The wake catch-up job assumed all base metadata and gas front/back arrays stayed capacity-aligned.

What was done:
- `ApplyBaseWakeCatchUp` now verifies every base metadata lane used by the Burst job.
- `BaseHibernationWakeCatchUpJob` clamps its base limit and room limit across all arrays before indexing.

Cinematic Cheats used:
- Same analytical wake catch-up lie; no new simulation truth.

Exact microseconds saved:
- None claimed. This adds cold-path guard arithmetic only and reduces future H-Phi migration crash risk. No dotnet rebuild was run.

## 2026-05-15 - Main Step SOA Guard Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- The main Dalton job used a single room-lane length and a single bulkhead-lane length as authority for multiple SOA buffers.

What was done:
- `GasDynamicsStepJob` now clamps room work across all required room lanes before indexing.
- Bulkhead diffusion is clamped across both endpoint lanes and sealed-state lane.
- Telemetry write now checks the ring exists and the scheduled write index is in range.

Cinematic Cheats used:
- None new. This preserves the same hibernation LOD and Dalton approximation.

Exact microseconds saved:
- None claimed. This is one-time per-step guard arithmetic to prevent capacity-skew faults during future H-Phi/native ownership migration. No dotnet rebuild was run.

## 2026-05-15 - Telemetry Cursor Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- The gas scheduler advanced `_telemetryWriteIndex` modulo the constant `TelemetryCapacity` even though the job now validates against the live native ring length.

What was done:
- `ScheduleStep` now derives the write index and next cursor from `_telemetryRing.Length` when the ring exists.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This preserves black-box evidence under future ring-size changes; no dotnet rebuild was run.

## 2026-05-15 - Toxicity Queue Guard Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- `ScheduleStep` could build `GasDynamicsStepJob` from `RoomO2` alone even though the job requires a valid toxicity signal queue writer.

What was done:
- Added `_toxicitySignals.IsCreated` to the scheduler gate before creating the job and parallel writer.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This is a native writer safety guard after partial disposal or future ownership migration; no dotnet rebuild was run.

## 2026-05-15 - H-Phi Audit Ownership Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Native memory audit counted `BaseAwakeState` as local gas memory even when GlobalDataVault owned that buffer.

What was done:
- `TryGetNativeMemoryAudit` now skips `BaseAwakeState` local byte accounting when `_baseAwakeVaultOwned` is true.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This is cold audit accuracy for H-Phi evidence; no dotnet rebuild was run.

## 2026-05-15 - Base Lane Readiness Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Several base hibernation methods checked `BaseAwakeState` but then indexed other base SOA lanes that are separate native buffers.

What was done:
- Added length-aware `AreBaseStateLanesReady`.
- Base snapshots, configuration, signal drain, player-inside writes, hibernation scans, and wake/sleep transitions now fail closed unless the full base lane set exists.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This is partial-native-state crash prevention for future H-Phi migration; no dotnet rebuild was run.

## 2026-05-15 - Room API Readiness Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Public room/bulkhead APIs could write multiple SOA lanes after checking only one lane.

What was done:
- Added length-aware `AreRoomStateLanesReady` and `AreBulkheadLanesReady`.
- Room snapshots, room configuration, player-room state, CO2 pressure injection, room flags, submerged fraction, ambient pressure, scrubber power, temperature, and bulkhead setup now fail closed on capacity skew.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This is API-bound crash prevention for H-Phi/native lane migration; no dotnet rebuild was run.

## 2026-05-15 - Depth Stress Guard Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- `ResolveEffectiveDepthStress01` trusted `_roomCount` without checking the live `RoomPressure` length.

What was done:
- Added a direct `RoomPressure.Length` bounds guard before pressure relief math.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This prevents stale pressure-lane indexing; no dotnet rebuild was run.

## 2026-05-15 - Redundant Guard Trim Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Some `IsCreated` branches remained after the new length-aware readiness helpers had already proven those lanes.

What was done:
- Removed redundant checks in room config, room flags, CO2 pressure injection, and base transition drains.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- No measurable claim. This removes branch noise on API/transition paths; no dotnet rebuild was run.

## 2026-05-15 - Base Room Mapping Guard Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Base remap code still trusted logical `_roomCount` for `_roomBaseIndex` writes.
- Signal-created base slots could map a room id that was valid logically but outside a shrunken/native-migrated mapping lane.

What was done:
- `TryConfigureBase` now requires live room-lane readiness before remapping and clamps ranges to `_roomBaseIndex.Length`.
- Transition signal mapping writes now require `roomId` to be inside both `_roomCount` and `_roomBaseIndex.Length`.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This is H-Phi capacity-skew crash containment; no dotnet rebuild was run.

## 2026-05-15 - Bulkhead And Telemetry Capacity Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Bulkhead endpoints could be accepted while current room lanes were not capacity-coherent.
- Black-box fault detection still used `TelemetryCapacity` to index the ring even after scheduling switched to live telemetry length.

What was done:
- Added room-lane readiness to `TrySetBulkhead`.
- Switched telemetry fault index math and dump entry-count metadata to `_telemetryRing.Length`.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This prevents capacity-skew faults and preserves truthful crash evidence; no dotnet rebuild was run.

## 2026-05-15 - H-Phi Audit Recheck Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- Fresh source-level H-Phi evidence was needed after the latest native-capacity hardening.

What was done:
- Re-ran `Tools/Architecture/HectonPhiAudit.ps1 -Summary`.
- The script emitted STATIC_SOURCE metrics before timing out at 240s, so this is recorded as partial evidence, not a clean pass.
- Captured metrics: RuntimeHPhiRisk 0.000591666; DataSovereignty 0.021395609; DataVaultRefs 153; NativeArrayRefs 6998; AupPrecisionRisk 0.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None. This was an audit-only source pass; no dotnet rebuild was run.

## 2026-05-15 - Initialization Invariant Addendum
STATUS: PENDING VERIFICATION

What was wrong:
- The solver could report initialized when only `RoomO2` and the toxicity queue existed.
- A Dalton step could be scheduled without proving room, base, bulkhead, and telemetry lanes were coherent.

What was done:
- Strengthened `IsInitialized` to require length-aware room/base/bulkhead readiness plus telemetry and toxicity creation.
- Routed `ScheduleStep` through the stronger initialization invariant.

Cinematic Cheats used:
- None new.

Exact microseconds saved:
- None claimed. This is invalid-job prevention for H-Phi/native lane migration; no dotnet rebuild was run.
