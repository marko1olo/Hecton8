# INPUT_DETERMINISM_BRIDGE Rationale

Status: PENDING VERIFICATION

## Bootstrap Decision

Problem: Raw input cadence differs across VR, mouse, Steam Deck, and 60 Hz simulation, creating replay and lockstep desync risk.
Solution: Build a deterministic input snapshot bridge around a fixed 60 Hz pre-simulation tick and unmanaged ring buffer, then publish snapshots through `SignalBus<InputStateSignal>`.
Rejected Alternatives: Direct `Input.GetAxis()` in consumers was rejected because hardware polling rates leak into gameplay. A Unity singleton input manager was rejected because project authority requires `GlobalRegistry` interfaces.
Scalability potential: Low tier uses the identical bit-exact path; Middle/High/Ultra spend saved CPU on presentation-only look smoothing and existing OpenXR late-latch pose paths without changing hitboxes.
Hardware Impact: Expected gain on i3/MX350 comes from removing duplicated consumer polling and queue drains; target remains below 0.1 ms, profiler proof still pending because build is blocked by unrelated asmdef/type errors.

## Decision 1 - Registry Boundary

Problem: Input determinism needed a service boundary without creating a second authoritative input owner.
Solution: `IInputDeterminismService` is exposed through `GlobalRegistry.InputDeterminism`, aliasing the registered input service and preserving bootstrap ownership.
Rejected Alternatives: A parallel singleton or new concrete `InputDeterminismManager` would split authority and create registration-order failures.
Scalability potential: Low/Middle/High/Ultra all resolve one interface; no tier branch exists in the control truth.
Hardware Impact: i3/MX350 avoids repeated concrete lookups and consumer-side raw input polling; estimate 0.2-4.0 us per consumer depending on previous path.

## Decision 2 - Packed Input ABI

Problem: Float input snapshots are not bit-stable enough for replay/lockstep and cost more bandwidth than needed.
Solution: Added `InputState` as `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]` with quantized `short` move/look/vertical axes and a button bitmask.
Rejected Alternatives: Reusing `PlayerInputState` was rejected because it is float-heavy and `Pack=4`; using `UniversalInputStateSignal` was rejected because it is not the required 24-byte ABI.
Scalability potential: Every device tier uses the same packet. Ultra devices get visual smoothing only; simulation remains identical.
Hardware Impact: MX350/i3 path stores 60 samples in 1440 bytes and a 300-frame blackbox in 9600 bytes; no per-frame GC.

## Decision 3 - SignalBus Migration

Problem: Player kinematics drained a physics-specific input queue, coupling gameplay to legacy signal transport and making same-frame pre-simulation flush unclear.
Solution: `InputDispatcher.PreSimulationInputTick()` queues `InputStateSignal` before `GlobalSignals.FlushPreSimulation()`, and `PlayerKinematicsRuntime` consumes the latest frame snapshot.
Rejected Alternatives: Keeping `PhysicsDeterminismSignals.TryDequeueInput()` as the primary consumer path was rejected because it bypasses the standardized signal lane.
Scalability potential: Low tier avoids multiple queue drains; high tier can still render smoother camera presentation from `VisualLookDelta`.
Hardware Impact: Estimated 2 us saved in player kinematics by removing up to eight legacy queue drain attempts per fixed step.

## Decision 4 - Replay and Blackbox

Problem: Input desync needs reconstructable evidence, not logs.
Solution: Maintained a 60-sample `NativeArray<InputState>` ring, a 300-frame deterministic input blackbox, `CrashTelemetryBuffer.ReportDeterministicInputFrame`, and a background MMF writer for `input_determinism_bridge.h8replay`.
Rejected Alternatives: Synchronous `FileStream.Write` on the main thread was rejected as hitch-prone. Managed replay event objects were rejected for GC.
Scalability potential: Low tier pays one fixed tiny packet path; high/ultra replay evidence remains identical and can be used for richer debugging tools.
Hardware Impact: Main thread stages 60 structs once per second; background blit avoids a visible hitch. Estimated avoided spike 0.03-0.06 ms per second on i3/MX350 storage path.

## Decision 5 - Latency and Presentation Split

Problem: Offline latency simulation must not change determinism or introduce async timing.
Solution: `InputDelayFrames` clamps to 0-2 and selects prior ring samples by deterministic frame; `VisualLookDelta` interpolates only presentation look between previous/current samples.
Rejected Alternatives: Sleeping, coroutines, async delays, or smoothing the physics vector were rejected because they mutate timing or AUP/control truth.
Scalability potential: Low/Middle/High/Ultra all execute identical input truth; higher tiers may present smoother look at render cadence.
Hardware Impact: Delay selection is O(1). Visual smoothing is one `float2` lerp per `Tick`, materially below 0.1 ms.

## Decision 6 - Compile Wall Classification

Problem: `dotnet build` reaches C# but fails with 154 missing type/namespace errors from unrelated cross-agent asmdef/type work (`Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `MacroSwarm`, `SoundEmissionSignal`, etc.).
Solution: Ran restore, full build, and touched-file filtered build diagnostics. No input bridge file surfaced in the filtered diagnostics after the final patch pass.
Rejected Alternatives: Editing unrelated asmdefs/types would cross the UX input domain and risk architectural sabotage.
Scalability potential: Not applicable to runtime tiers; this is integration risk.
Hardware Impact: No runtime gain. Integrator action required before objective runtime proof.

## OMEGA POLISH CHANGES

Problem: Polish audit found one presentation interpolation division and needed proof that no managed loop/string/sqrt bloat was introduced in the input bridge.
Solution: Replaced interpolation division with multiplication by `StandardInputTickRateHz`; re-ran targeted scans for `foreach`, `.ToString()`, `string.Format`, `math.sqrt`, and `math.normalize` across touched files.
Rejected Alternatives: Leaving the division was harmless but violated the explicit polish mandate. Adding tiered input math was rejected because input must be bit-exact, not scalable by approximation.
Scalability potential: Low/Middle/High/Ultra all use identical quantization and delay. Visual overkill is limited to render-only interpolation and existing OpenXR pose late-latching; no simulation branch changes by tier.
Hardware Impact: On i3/MX350, the division removal is sub-microsecond but free. Main material gain remains avoiding per-consumer hardware polling, avoiding legacy input queue drains, and backgrounding the replay blit.

Cinematic Cheats used: The bridge uses the "dear lie" only for camera/look presentation: interpolate visible look between fixed input ticks while keeping physical controls and hitboxes pinned to the 60 Hz quantized sample. No water/physics truth was added.

Final diff scope: `InputDispatcher.cs`, `PlayerInputState.cs`, `GlobalSignals.cs`, `GlobalRegistryContracts.cs`, `GlobalRegistry.cs`, `SystemDispatcher.cs`, `PlayerKinematicsRuntime.cs`, `CrashTelemetryBuffer.cs`, plus `Assets/_Project/Scripts/Input/Determinism/*` and the agent status/log files. Shared worktree contains unrelated edits by other agents in the same files; only the input bridge changes above are claimed here.

## Decision 7 - Lockstep Recorder and Replay Writer Hardening

Problem: Recheck found `LockstepStateValidator` still sampling `PhysicsDeterminismSignals.TryGetLatestInput`, which left one determinism recorder/vault path dependent on the legacy physics input lane. Recheck also found the replay writer could hold its gate through MMF flush and the public input-delay setter used a literal ceiling.
Solution: `LockstepStateValidator` now pulls the latest flushed `SignalBus<InputStateSignal>` snapshot for replay capture and player vault input-actions hashing. Replay snapshot copying is locked only around the NativeArray copy/MMF memcpy, with `Flush()` outside the lock. `InputDelayFrames` clamps through `MaxInputDelayFrames` and survives dispatcher reset as a configured test parameter. The new determinism asmdef keeps only the contract assembly reference.
Rejected Alternatives: Keeping the legacy latest-input query was rejected because the prompt requires physics/determinism systems to read the standardized snapshot. Holding the writer gate through disk flush was rejected because it can stall the main-thread once-per-second staging path. Hardcoded delay ceiling was rejected because it creates a second authority.
Scalability potential: Low/Middle/High/Ultra all keep identical input truth. Low tier benefits from fewer legacy lanes; Ultra spends saved time only on presentation and diagnostics, not divergent input math.
Hardware Impact: On i3/MX350, estimated 2 us path risk removed from lockstep replay/vault capture and 30-60 us once-per-second hitch risk reduced by avoiding a locked disk flush. Global profiler proof remains blocked by unrelated compile errors.

## Decision 8 - Native-Direct Fault Dump

Problem: The deterministic input blackbox dump copied the 300-frame `NativeArray` into a managed `byte[]` before writing. That does not hit the normal frame hot path, but crash evidence should avoid heap pressure and avoid losing fidelity under memory stress.
Solution: Dump now writes a `ReadOnlySpan<byte>` directly over the native blackbox memory to the `FileStream`. Replay payload sizing and ABI guard both use `InputStateSizeBytes`, and replay writer shutdown waits longer before releasing MMF resources.
Rejected Alternatives: Keeping the managed scratch copy was rejected because the telemetry mandate prefers native binary export. Repeating literal `24` was rejected because replay sizing and ABI validation need one authority.
Scalability potential: Low/Middle/High/Ultra all dump identical binary evidence. Cheap devices avoid one extra managed allocation during fault handling; high-end devices gain no divergent simulation path.
Hardware Impact: On i3/MX350, removes roughly 9.6 KB managed allocation per deterministic input dump and reduces slow-storage MMF shutdown race risk. Runtime hot path remains unchanged.

## Decision 9 - Remove Normal Legacy Input Lane

Problem: `InputDispatcher` still mirrored every standardized `InputStateSignal` into `PhysicsDeterminismSignals.PublishInput`, even after scans showed no remaining first-party runtime consumer of `TryDequeueInput` or `TryGetLatestInput`.
Solution: Removed the normal per-tick legacy publish. The authoritative runtime path is now the 60 Hz `SignalBus<InputStateSignal>` snapshot. The old physics signal remains only for ghost replay override injection, where `LockstepStateValidator.PublishInputOverride` feeds `InputDispatcher.TryConsumeLatestInputOverride` before quantization.
Rejected Alternatives: Keeping the mirror was rejected because it maintains a second queue and second "latest input" state without a consumer. Removing the override bridge was rejected because it would break `.h8replay` ghost input injection.
Scalability potential: Low/Middle/High/Ultra all use one deterministic input lane for normal play. High-end devices still get visual smoothing only; no tier changes input truth.
Hardware Impact: On i3/MX350, removes one bounded `NativeQueue<InputSignal>` write and latest-signal assignment per 60 Hz tick; estimated ~1-3 us saved depending on queue pressure.

## Decision 10 - Dump Failure Visibility

Problem: The deterministic input dump catch path swallowed file I/O failures silently, which violates the blackbox requirement because a failed dump would leave no evidence trail.
Solution: Added `CrashTelemetryBuffer.ReportBlackBoxExportFailure()` and call it from `DumpDeterministicInputBlackBox()` catch. It increments the existing blackbox export fault path without retaining managed exception text.
Rejected Alternatives: Logging the exception was rejected because crash/fault paths must avoid managed strings and log spam. Leaving the catch empty was rejected because postmortem evidence must report its own failure.
Scalability potential: Low/Middle/High/Ultra share the same fault telemetry. Cheap devices benefit most because slow or locked storage is more likely to fail dumps.
Hardware Impact: No steady-frame cost. On failure, a single counter/fault flag write replaces silent loss of evidence.

## Decision 11 - Replay Writer Failure Containment

Problem: Recheck found the deterministic input dump still carried a stale owner path and replay writer setup/flush/shutdown failures could be silent or unsafe. A background writer fault would violate the blackbox mandate by hiding evidence loss.
Solution: Corrected `InputDumpRelativePath` to `Dump_INPUT_DETERMINISM_BRIDGE.bin`, report blackbox export failures during replay writer setup, catch exceptions inside `InputReplayWriterLoop()`, and avoid releasing mapped resources when shutdown times out while the writer thread may still own them.
Rejected Alternatives: Keeping the stale owner path was rejected because evidence would be misrouted. Empty background catches were rejected because they erase postmortem cause. Releasing MMF resources after a join timeout was rejected because the writer may still be flushing.
Scalability potential: Low/Middle/High/Ultra all keep identical input truth and replay evidence. Low-end storage benefits most from guarded slow flush handling; high-end devices gain cleaner tooling evidence, not divergent simulation.
Hardware Impact: No steady-frame cost. On i3/MX350, this reduces cold-path crash and evidence-loss risk under slow or locked storage; no microsecond frame saving is claimed.

## Decision 12 - Replay Setup and Shutdown Lifetime Closure

Problem: Replay writer setup could fail after allocating the wait handle or assigning a thread object, leaving `_inputReplayThread` non-null and preventing all future setup attempts. Shutdown timeout also disposed the wait handle before proving the background writer had exited.
Solution: Setup failure now clears replay stop/write flags, nulls the thread field, disposes the wait handle, and releases MMF resources. The null-pointer path reports blackbox export failure. Shutdown now leaves signal/map resources intact when the writer does not stop within the timeout, so the live thread is not handed disposed handles.
Rejected Alternatives: Keeping the stale non-null thread field was rejected because it silently disables replay for the rest of the session. Disposing the wait handle on timeout was rejected because it converts a slow flush into a background ObjectDisposed failure.
Scalability potential: Low/Middle/High/Ultra keep the same deterministic packet. Low-end storage gets the most value because slow filesystem and MMF setup failures are more likely on weak hardware; high-end only gains cleaner capture reliability.
Hardware Impact: No steady-frame cost. On i3/MX350 this is a cold-path correctness improvement, not a frame-time saving.

## Decision 13 - Replay Setup Retry Backoff

Problem: A persistent replay MMF setup failure could retry from the pre-simulation path every frame, repeatedly allocating file/MMF resources and reporting the same export fault while storage permissions or locks remain broken.
Solution: Added `_nextInputReplayRetryFrame` and a 300-frame retry interval. Setup failures report one blackbox export fault for that window, successful writer start clears the gate, and dispatcher reset clears the gate for clean session recovery.
Rejected Alternatives: Per-frame retry was rejected because it can create an allocation/IO storm under a persistent environment fault. Permanent disable after one failure was rejected because transient file locks should recover without restarting the play session.
Scalability potential: Low/Middle/High/Ultra all keep identical 24-byte input truth and replay payloads. Low devices benefit most because weak disks and locked antivirus scans are more likely to trigger transient setup failure; high-end gets cleaner capture reliability without divergent input math.
Hardware Impact: No normal-frame cost after successful setup. On i3/MX350 failure cases, this avoids up to 60 repeated FileStream/MMF setup attempts per second and prevents repeated telemetry spam from consuming CPU/IO budget.
