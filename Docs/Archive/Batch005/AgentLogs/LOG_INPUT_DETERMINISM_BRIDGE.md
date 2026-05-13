# INPUT_DETERMINISM_BRIDGE Log

## Session Start

What was wrong: Input assignment requires deterministic 60 Hz snapshots; current codebase not yet inspected.
What was done: XML prompt extracted; status and rationale files initialized.
Cinematic Cheats used: deterministic quantization and presentation-only interpolation planned; no physical input simulation.
Exact Microseconds saved: pending measurement; initial static estimate only.

## Standardized Input Tick Implementation

What was wrong: Input hardware cadence was not standardized for simulation. VR/mouse/gamepad rates could leak into gameplay through frame-cached floats or legacy physics input queues, which breaks replay and lockstep assumptions.

What was done: Added a packed 24-byte `InputState`, `InputStateSignal`, `IInputDeterminismService` access through `GlobalRegistry.InputDeterminism`, a pre-simulation 60 Hz capture call before `GlobalSignals.FlushPreSimulation()`, a 60-slot `NativeArray<InputState>` ring, 0-2 frame deterministic delay, `PlayerKinematicsRuntime` consumption from `SignalBus<InputStateSignal>`, visual-only look interpolation, 300-frame input blackbox telemetry, and an MMF replay writer for `input_determinism_bridge.h8replay`. Added `Hecton8.Input.Determinism` asmdef pointing at contracts.

Cinematic Cheats used: Presentation look uses "dear lie" interpolation between fixed input samples. Physics, hitboxes, replay, and lockstep truth remain quantized to the 60 Hz input tick. Existing OpenXR pose handling stays outside the physical input packet for late-latch presentation.

Exact Microseconds saved: Estimated 2.00 us per player fixed step from removing legacy input queue drain in `PlayerKinematicsRuntime`; estimated 4.00 us per gameplay consumer by keeping raw hardware polling behind the input provider; estimated 0.03-0.06 ms spike avoided once per second by moving replay blit to background MMF writer. Profiler proof pending because full build is blocked by unrelated shared-worktree dependency errors.

Verification: `dotnet restore Hecton8.Core.csproj --ignore-failed-sources` succeeded. `dotnet build Hecton8.Core.csproj --no-restore` failed with 154 unrelated missing namespace/type errors (`Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `MacroSwarm`, `SoundEmissionSignal`, etc.). Touched-file filtered build diagnostics returned no input bridge file errors after final patch. Static scans found no `Input.GetAxis`, `InputManager.Instance`, gameplay `UnityEngine.Input`, `InputSystem.actions` in `Update`, or stale `PhysicsDeterminismSignals` input drain in `PlayerKinematicsRuntime`.

Status: PENDING VERIFICATION due global compile dependency wall.

## Replay Setup Retry Backoff Pass

What was wrong: Replay writer setup is called from the input service lifecycle and pre-simulation path. If MMF setup failed persistently, the bridge could retry every frame, repeatedly allocating FileStream/MMF objects and emitting redundant export-failure telemetry.

What was done: Added a 300-frame `_nextInputReplayRetryFrame` gate. Setup failure reports blackbox export failure once for the retry window. Successful writer startup and dispatcher frame-state reset clear the retry gate so transient storage faults can recover.

Cinematic Cheats used: None. This is cold-path replay evidence hardening; simulation remains the fixed 60 Hz packed input packet and visual smoothing remains presentation-only.

Exact Microseconds saved: No steady-frame claim. During persistent setup failure this avoids up to 60 cold FileStream/MMF setup retries per second, preventing repeated IO/allocation spikes on low-end storage.

Verification: Focused touched-file build filter reports no input bridge diagnostics. Static scans show no stale owner path, no 250 ms replay join, no normal legacy input queue publish/read path, and no raw gameplay input API in the input bridge files. `dotnet build Hecton8.Core.csproj --no-restore` remains globally red with 91 unrelated shared-worktree errors. Unity console verification was retried twice and remained blocked because the Unity session did not answer ping.

Status: PENDING VERIFICATION due global compile dependency wall.

## Replay Writer Lifecycle Closure Pass

What was wrong: Replay writer setup could leave `_inputReplayThread` non-null after partial failure, blocking future setup. Shutdown timeout also disposed the wait handle before the writer was confirmed stopped.

What was done: Setup failure now reports blackbox export failure, clears stop/write state, nulls the thread field, disposes the wait handle, and releases MMF resources. Shutdown only disposes the wait handle and map after the writer has stopped; timeout keeps ownership intact and reports the export fault.

Cinematic Cheats used: None. This is evidence-path lifetime hardening; deterministic input truth and presentation smoothing are unchanged.

Exact Microseconds saved: No frame-time claim. This removes a cold-path replay loss and use-after-dispose risk on weak storage devices.

Verification: Focused `dotnet build` filter reports no input bridge diagnostics. Full `Hecton8.Core.csproj` build still fails with 92 unrelated shared-worktree errors. Static scan still finds no stale legacy normal input lane or old owner dump path in touched input files.

Status: PENDING VERIFICATION due global compile dependency wall.

## Recheck Upgrade Pass

What was wrong: A broader first-party scan found `LockstepStateValidator` still reading `PhysicsDeterminismSignals.TryGetLatestInput` for replay capture and player vault hashing. The replay writer also held its gate through the MMF write window, and `InputDelayFrames` had one literal clamp instead of using the single max constant.

What was done: `LockstepStateValidator` now reads the latest flushed `SignalBus<InputStateSignal>` snapshot and stores the standardized buttons, quantized move/look/vertical axes, current scheme hash, flags, and sequence into replay/vault state. `InputDispatcher` now locks only around snapshot copy/MMF memcpy, flushes outside the gate, serializes `InputDelayFrames`, preserves it on reset, and clamps it through `MaxInputDelayFrames`. `Hecton8.Input.Determinism` was trimmed to the single `Hecton8.Core.Contracts` asmdef reference.

Cinematic Cheats used: No new simulation cheat. The only cheat remains visual look interpolation between fixed 60 Hz input samples; replay, physics, vault hashing, and blackbox telemetry stay tied to quantized input truth.

Exact Microseconds saved: Estimated 2.00 us removed from the remaining legacy lockstep input path; estimated 30-60 us once-per-second hitch risk reduced by not holding the replay gate across disk flush. No claim of verified frame time until global compile is green and Unity profiling can run.

Verification: Re-extracted the XML prompt with CLI. Static scan now returns no first-party `Input.GetAxis`, `InputManager.Instance`, `InputSystem.actions`, `_lastInputSignal`, `PhysicsDeterminismSignals.TryDequeueInput`, or `PhysicsDeterminismSignals.TryGetLatestInput`. Focused build filter returns no diagnostics for `InputDispatcher`, `PlayerInputState`, `InputStateSignal`, `PlayerKinematicsRuntime`, `CrashTelemetryBuffer`, `DeterministicInputContracts`, `Hecton8.Input.Determinism`, or `LockstepStateValidator`. Full build still fails with 154 unrelated missing type/namespace errors in audio/fluid/tether/shared dependency work. Unity MCP console check was attempted and failed because the Unity session did not answer ping.

Status: PENDING VERIFICATION due global compile dependency wall.

## Fault Evidence Hardening Pass

What was wrong: The input blackbox dump used a managed `byte[]` staging copy before writing the 300-frame native telemetry buffer. The replay payload size and ABI guard also repeated the 24-byte `InputState` assumption in separate places.

What was done: `InputDispatcher` now writes the deterministic input blackbox directly from native memory through `ReadOnlySpan<byte>`, uses `InputStateSizeBytes` as the single ABI byte-size constant, and gives the replay writer a longer cold shutdown join window before releasing MMF resources.

Cinematic Cheats used: None added. The simulation truth remains the same 60 Hz quantized input packet; only fault evidence export and shutdown safety changed.

Exact Microseconds saved: No hot-frame claim. Fault dump removes roughly 9.6 KB of managed allocation per dump. Replay shutdown hardening targets correctness on slow storage, not frame time.

Verification: Re-extracted prompt with CLI. Focused build filter still returns no diagnostics for the input bridge or lockstep validator. Static scans found no managed blackbox scratch, repeated literal replay payload size, legacy input query, `Input.GetAxis`, `InputManager.Instance`, or `InputSystem.actions`. Wider raw input API scan found only editor/UI diagnostic hotkeys, not gameplay simulation. `git diff --check` returned no whitespace errors. Full build is still red on unrelated audio/fluid/tether/shared dependency errors, now reporting 153 errors in this shared worktree.

Status: PENDING VERIFICATION due global compile dependency wall.

## Parallel Lane Removal Pass

What was wrong: Normal input samples were being published twice: once as the authoritative `SignalBus<InputStateSignal>` snapshot and again into the legacy `PhysicsDeterminismSignals` input queue. No first-party runtime consumer remained for the normal legacy queue.

What was done: Removed the per-tick `PhysicsDeterminismSignals.PublishInput` mirror from `InputDispatcher`. Kept the ghost replay override bridge intact: `LockstepStateValidator` can still publish an override and `InputDispatcher` can consume it before building the standardized input packet.

Cinematic Cheats used: None added. This is transport cleanup only; presentation smoothing remains the only deliberate visual lie.

Exact Microseconds saved: Estimated ~1-3 us per 60 Hz input tick by removing one bounded native queue write and latest-input assignment. Main value is eliminating a second input authority.

Verification: CLI prompt re-extracted. Static scan reports no first-party normal `PhysicsDeterminismSignals.PublishInput`, `TryDequeueInput`, or `TryGetLatestInput` usage outside the legacy API definition. Focused build filter reports no input bridge or lockstep validator diagnostics. Full build remains blocked by unrelated global dependency errors.

Status: PENDING VERIFICATION due global compile dependency wall.

## Export Failure Telemetry Pass

What was wrong: The deterministic input blackbox dump catch path swallowed file-write failures. During a NaN/sanitization incident that could erase the only evidence that the dump failed.

What was done: Added `CrashTelemetryBuffer.ReportBlackBoxExportFailure()` and wired `DumpDeterministicInputBlackBox()` to call it on export exceptions. The path records the existing blackbox export fault counter without retaining exception strings.

Cinematic Cheats used: None. Fault reporting only.

Exact Microseconds saved: No frame-time claim. This is reliability hardening; it replaces silent failure with one counter/fault-flag update.

Verification: Focused build filter reports no input bridge diagnostics. Static scan confirms no normal legacy input queue publish/read path remains and no managed deterministic input blackbox scratch remains. Unity console errors are unrelated UI/ecosystem compile errors.

Status: PENDING VERIFICATION due global compile dependency wall.

## Replay Writer Failure Hardening Pass

What was wrong: The deterministic input dump path still referenced a stale owner identity, and replay writer setup/thread/shutdown failures could lose evidence or dispose mapped resources while a slow writer was still active.

What was done: Corrected the dump path to `Docs/AgentLogs/Dump_INPUT_DETERMINISM_BRIDGE.bin`, added blackbox export failure reporting to replay writer setup and loop exceptions, and changed shutdown timeout behavior so a still-live writer is reported instead of racing resource disposal.

Cinematic Cheats used: None. This pass only hardens evidence export; simulation remains the fixed 60 Hz quantized input packet and presentation smoothing remains visual-only.

Exact Microseconds saved: No frame-time claim. This is cold-path reliability hardening; expected gain is fewer silent evidence failures on slow i3/MX350 storage paths.

Verification: Static scan confirms no stale `UNIVERSAL_INPUT_ORCHESTRATOR` path remains in the input bridge, no 250 ms replay join remains, and no first-party normal `PhysicsDeterminismSignals.PublishInput`, `TryDequeueInput`, or `TryGetLatestInput` usage remains outside the legacy API definition. Focused build filter reports no input bridge diagnostics; global compile is still blocked by 93 unrelated shared-worktree errors. Unity console reports unrelated MCP regex, `HectonUnderwaterVisuals`, and entry-point discovery errors, with no input bridge file errors.

Status: PENDING VERIFICATION due global compile dependency wall.
