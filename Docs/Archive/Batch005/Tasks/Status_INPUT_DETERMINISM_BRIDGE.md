# INPUT_DETERMINISM_BRIDGE Status

Batch prompt: `INPUT_DETERMINISM_BRIDGE`
Domain: `UX_ENGINEER`
Status: PENDING VERIFICATION

## Mandates Read

- [x] `OPT_Zero_GC_Policy_AllocFree_Mandate` | DOD: hot-path allocation bans applied before design | Rejected: managed per-frame event objects | Estimate: 0.00 us saved baseline.
- [x] `ARCH_Global_Registry_ServiceLocator_DI_Init` | DOD: service boundary must be registry/interface based | Rejected: singleton input manager | Estimate: 0.20 us saved per lookup.
- [x] `CTRL_Device_Abstraction_Haptics` | DOD: raw device input stays behind provider boundary | Rejected: gameplay `UnityEngine.Input` polling | Estimate: 4.00 us saved per consumer.
- [x] `UI_Data_Streaming_ZeroGC_Optimization` | DOD: presentation reads cached snapshots | Rejected: formatted per-frame diagnostics | Estimate: 1.00 us saved per visible update.
- [x] `DBG_Telemetry_Crash_Reporting_PostMortem` | DOD: fixed blackbox telemetry retained | Rejected: log-only diagnosis | Estimate: evidence gain, not frame time.
- [x] `OPT_Performance_Budgets_FrameTime_VRAM_Limits` | DOD: input bridge remains below suspicious 0.1 ms budget by design | Rejected: per-consumer hardware polling | Estimate: 10.00 us saved at 60 Hz.

## Loop 0 - Bootstrap

- [x] Extract XML prompt from `CURRENT_BATCH.md` using CLI | DOD: exact tag from line-bounded PowerShell extraction | Rejected: neighboring agent prompt context | Estimate: 0.00 us.
- [x] Inspect current input and tick architecture | DOD: read `InputDispatcher`, `SystemDispatcher`, `GlobalSignals`, `PlayerKinematicsRuntime` | Rejected: invented signatures | Estimate: 0.00 us.
- [x] Baseline restore/build | DOD: `dotnet restore` succeeded, `dotnet build --no-restore` reached C# | Rejected: claiming Unity readiness from static scan | Estimate: blocked by unrelated missing assembly references.

## Loop 1 - Tasks 1-5

- [x] 1. Register deterministic input service path | DOD: `IInputDeterminismService` exposed via `GlobalRegistry.InputDeterminism` alias to authoritative input service | Rejected: `InputManager.Instance` singleton | Estimate: 0.20 us lookup cost avoided.
- [x] 2. Migrate standardized snapshots to `SignalBus` | DOD: added `InputStateSignal` lane and publisher | Rejected: direct `PhysicsDeterminismSignals` drain as primary path | Estimate: 2.00 us saved per consumer.
- [x] 3. Add asmdef isolation | DOD: created `Hecton8.Input.Determinism` referencing `Hecton8.Core.Contracts` | Rejected: adding contracts to gameplay assembly | Estimate: 0.00 us runtime.
- [x] 4. Dead code hunt | DOD: `rg` found no `Input.GetAxis`, `InputManager.Instance`, or gameplay `UnityEngine.Input` access; remaining Input System hits are Core/Input/UI/Editor | Rejected: touching UI/editor input routes outside domain | Estimate: 4.00 us saved per gameplay consumer.
- [x] 5. Native 60-slot ring | DOD: `NativeArray<InputState>[60]` in `InputDispatcher` | Rejected: managed `InputState[]` ring | Estimate: 0 B/frame GC.
- [x] Compile attempt after tasks 1-5 | DOD: build run after restore | Rejected: hiding red build | Estimate: blocked by unrelated missing refs (`Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, etc.).

## Loop 2 - Tasks 6-10

- [x] 6. Pre-simulation polling | DOD: `SystemDispatcher.Update` calls `GlobalRegistry.InputDeterminism?.PreSimulationInputTick()` immediately before `GlobalSignals.FlushPreSimulation()` | Rejected: applying physics in `Update` | Estimate: deterministic cadence, no frame-time claim.
- [x] 7. Snapshot publication | DOD: quantized sample written to ring, delayed sample published as `InputStateSignal` | Rejected: raw float hardware payload | Estimate: replay bit consistency.
- [x] 8. Kinematic consumption | DOD: `PlayerKinematicsRuntime.SnapshotInputs()` reads `SignalBus<InputStateSignal>.GetFrameSnapshot()` only for fresh input | Rejected: queue-draining `PhysicsDeterminismSignals` in player kinematics | Estimate: 2.00 us queue drain saved.
- [x] 9. Visual smoothing | DOD: `VisualLookDelta` interpolates `PreviousInputState` to `CurrentInputState`; simulation state remains quantized | Rejected: smoothing physical AUP/control vector | Estimate: visual-only.
- [x] 10. VR late-latch boundary | DOD: existing XR pose path remains internal through `HectonXRRuntimeState`/XR buffers; deterministic hitbox input stays 60 Hz | Rejected: feeding high-frequency head pose into physics | Estimate: comfort/latency separation.
- [x] Compile attempt after tasks 6-10 | DOD: touched-file filter on build output returned no new diagnostics for input bridge files | Rejected: fixing unrelated cross-agent asmdef graph | Estimate: blocked by external dependency graph.

## Loop 3 - Tasks 11-15

- [x] 11. Replay recording | DOD: every 60 standardized samples stages `NativeArray<InputState>[60]` and background thread blits it into `input_determinism_bridge.h8replay` MMF | Rejected: synchronous main-thread file write | Estimate: 0.03-0.06 ms hitch avoided per second.
- [x] 12. AUP shift safety | DOD: `InputState` stores local move/look/vertical axes only | Rejected: world/AUP position in input packet | Estimate: no rebase work.
- [x] 13. Math LOD | DOD: no quality tier branch in input math | Rejected: low-tier approximation that could desync | Estimate: bit-exact across tiers.
- [x] 14. Zero-GC hot path | DOD: NativeArrays, fixed events, no per-frame managed collections; fault dump allocation only on NaN/sanitization path | Rejected: managed replay event objects | Estimate: 0 B/frame target.
- [x] 15. Blackbox dump | DOD: 300-frame `NativeArray<DeterministicInputTelemetryEntry>`, per-frame `CrashTelemetryBuffer.ReportDeterministicInputFrame`, dump path `Docs/AgentLogs/Dump_INPUT_DETERMINISM_BRIDGE.bin` | Rejected: log-only input evidence | Estimate: evidence gain, not frame time.
- [x] Compile attempt after tasks 11-15 | DOD: `dotnet build` still blocked before isolated validation by unrelated missing assemblies; no input-file diagnostics in filtered pass | Rejected: false green report | Estimate: blocked.

## Loop 4 - Tasks 16-18

- [x] 16. Latency compensation | DOD: `InputDelayFrames` clamped 0-2; delayed frame marked with `InputStateFlags.DelayApplied` | Rejected: wall-clock sleep or async delay | Estimate: deterministic offline network simulation.
- [x] 17. Reconnaissance | DOD: `rg` scan for `InputSystem.actions` in `Update` found no production gameplay usage | Rejected: broad UI/editor rewrite | Estimate: 0.00 us.
- [x] 18. ABI compile check | DOD: `InputState` is `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]` and runtime editor/development guard checks 24 bytes | Rejected: `Pack=4` float snapshot for replay | Estimate: 24-byte packet vs larger float payload.

## Loop 5 - Recursive Re-Verification

- [x] Re-extracted prompt after task groups | DOD: CLI extraction repeated from `CURRENT_BATCH.md` | Rejected: memory-only task list | Estimate: 0.00 us.
- [x] Verified no physics application in `Update` | DOD: `InputDispatcher.Tick` only visual interpolation/haptics; `PreSimulationInputTick` performs capture before signal flush | Rejected: `Update`-time physics mutation | Estimate: correctness.
- [x] Verified `PlayerKinematicsRuntime` no longer drains `PhysicsDeterminismSignals` for input | DOD: targeted `rg` returned no `TryDequeueInput`, `TryGetLatestInput`, `_lastInputSignal` | Rejected: stale queue fallback | Estimate: deterministic consumer path.
- [x] Verified raw-input purge scan | DOD: no `Input.GetAxis`, `InputManager.Instance`, or gameplay `UnityEngine.Input` access in first-party scripts | Rejected: scanning third-party package noise | Estimate: 4.00 us per gameplay consumer.
- [x] Build status | DOD: restore succeeded; build failed with 154 unrelated missing type/namespace errors from current shared worktree | Rejected: altering other agents' asmdef/dependency work | Estimate: blocked by dependency graph, not input bridge evidence.

## Omega Polish

- [x] Read `<POLISH_MANDATE>` after core task completion | DOD: CLI extraction from `CURRENT_BATCH.md` | Rejected: pre-reading polish before task closure | Estimate: 0.00 us.
- [x] Anti-bloat scan | DOD: targeted scan for `foreach`, `.ToString()`, `string.Format`, `math.sqrt`, `math.normalize` in touched runtime files returned no hits | Rejected: broad third-party/editor churn | Estimate: 0.00 us.
- [x] Division purge | DOD: visual interpolation now multiplies by `StandardInputTickRateHz` instead of dividing by tick interval | Rejected: leaving mandated polish nit | Estimate: sub-1.00 us.
- [x] Final build state | DOD: touched-file filtered build diagnostics show no input bridge file errors; full build still red on unrelated dependency graph | Rejected: claiming VERIFIED without global compile | Estimate: PENDING VERIFICATION.

## Loop 6 - Recheck Upgrade

- [x] Re-extracted XML prompt with CLI after further edits | DOD: line-bounded PowerShell extraction of `INPUT_DETERMINISM_BRIDGE` from `CURRENT_BATCH.md` | Rejected: stale memory after compaction | Estimate: 0.00 us.
- [x] Unity metadata sanity | DOD: `Assets/_Project/Scripts/Input/Determinism` contains `.meta` files matching existing project import formats | Rejected: manual GUID churn when Unity already generated valid metadata | Estimate: integration hygiene.
- [x] Asmdef dependency trim | DOD: `Hecton8.Input.Determinism` now references only `Hecton8.Core.Contracts` | Rejected: unused `Unity.Mathematics` dependency in contract-only assembly | Estimate: import graph hygiene.
- [x] Lockstep replay/vault input source migrated | DOD: `LockstepStateValidator` now mirrors latest `SignalBus<InputStateSignal>` snapshot instead of `PhysicsDeterminismSignals.TryGetLatestInput` | Rejected: legacy latest-input queue dependency in determinism recorder | Estimate: 2.00 us queue lookup/path risk removed.
- [x] Replay writer lock scope tightened | DOD: main-thread snapshot copy is guarded; background MMF flush occurs outside the gate | Rejected: holding a shared lock through disk flush | Estimate: 30-60 us hitch risk reduced once/sec on weak storage.
- [x] Delay configuration hardened | DOD: `InputDelayFrames` is serialized, clamped through `MaxInputDelayFrames`, and reset preserves configured delay | Rejected: runtime-only literal clamp and reset-to-zero behavior | Estimate: correctness, no frame-time claim.
- [x] Re-ran deterministic input scans | DOD: no `Input.GetAxis`, `InputManager.Instance`, `InputSystem.actions`, `_lastInputSignal`, `PhysicsDeterminismSignals.TryDequeueInput`, or `TryGetLatestInput` in first-party script scan | Rejected: leaving a hidden legacy consumer | Estimate: 4.00 us per avoided gameplay poll/consumer path.
- [x] Re-ran focused build filter | DOD: full build still fails globally with 154 unrelated missing type/namespace errors; filtered diagnostics show no input bridge or lockstep-validator lines | Rejected: claiming VERIFIED while global compile is red | Estimate: PENDING VERIFICATION.
- [x] Unity console check attempted | DOD: MCP `read_console` requested error console; Unity session returned not ready/ping timeout | Rejected: claiming editor compile evidence without a responsive Unity session | Estimate: blocked by tool/session readiness.

## Loop 7 - Fault Evidence Hardening

- [x] Re-read task prompt with CLI after continued hardening | DOD: `CURRENT_BATCH.md` extraction confirmed unchanged `INPUT_DETERMINISM_BRIDGE` directive | Rejected: continuing from memory only | Estimate: 0.00 us.
- [x] Re-read relevant mandates | DOD: Zero-GC, registry, device abstraction, crash telemetry, performance budget, and physics determinism mandates checked before patching | Rejected: undocumented hot-path exception | Estimate: process integrity.
- [x] Input blackbox dump made native-direct | DOD: removed managed `byte[]` scratch; dump writes `NativeArray` memory through `ReadOnlySpan<byte>` | Rejected: fault-path heap copy before binary dump | Estimate: ~9.6 KB managed allocation removed per dump.
- [x] Replay shutdown window hardened | DOD: replay writer join window raised from 250 ms to 2000 ms before releasing MMF resources | Rejected: releasing mapped memory while a slow flush may still be active | Estimate: crash-risk reduction on slow storage, cold path only.
- [x] ABI byte-size single authority | DOD: `InputStateSizeBytes` drives replay payload size and runtime ABI guard | Rejected: repeated literal `24` in replay math | Estimate: maintenance correctness.
- [x] Re-ran scans/build after fault-path patch | DOD: no managed scratch/literal ABI/join regression hits; focused build filter still clean; full build now fails with 153 unrelated errors | Rejected: declaring VERIFIED under global compile wall | Estimate: PENDING VERIFICATION.
- [x] Final local audit | DOD: `git diff --check` returned no whitespace errors; raw input API scan hits only editor/UI diagnostics, not gameplay simulation | Rejected: expanding into UI/editor hotkey ownership outside task domain | Estimate: scope containment.

## Loop 8 - Parallel Lane Removal

- [x] Re-extracted XML prompt before further edit | DOD: CLI extraction confirmed `SignalBus<InputStateSignal>` remains the required publication path | Rejected: maintaining compatibility queue without a consumer | Estimate: 0.00 us.
- [x] Removed normal legacy input publish | DOD: `InputDispatcher` no longer calls `PhysicsDeterminismSignals.PublishInput` for every standardized sample | Rejected: dual-authority `NativeQueue<InputSignal>` mirror | Estimate: ~1-3 us and one bounded queue write removed per 60 Hz input tick.
- [x] Preserved ghost replay override bridge | DOD: `TryConsumeLatestInputOverride` and `PublishInputOverride` remain for replay injection before quantization | Rejected: breaking existing `.h8replay` ghost override path | Estimate: correctness.
- [x] Re-ran legacy lane scan | DOD: no first-party normal `PublishInput`, `TryDequeueInput`, or `TryGetLatestInput` usage remains outside the legacy API definition | Rejected: hidden physics consumer | Estimate: deterministic lane singularity.
- [x] Re-ran focused build filter | DOD: no input bridge or lockstep validator diagnostics surfaced; global build remains blocked by unrelated dependency graph | Rejected: claiming VERIFIED under red global compile | Estimate: PENDING VERIFICATION.

## Loop 9 - Export Failure Telemetry

- [x] Added blackbox export fault reporter | DOD: `CrashTelemetryBuffer.ReportBlackBoxExportFailure()` records failure count without retaining exception strings | Rejected: silent catch or managed exception text capture | Estimate: evidence gain, 0 B retained.
- [x] Wired deterministic input dump failure path | DOD: `DumpDeterministicInputBlackBox()` catch now reports blackbox export failure | Rejected: invisible dump failure during NaN/sanitization incident | Estimate: postmortem reliability.
- [x] Re-ran focused build and allocation scan | DOD: no input bridge diagnostics; no managed blackbox scratch remains; remaining `byte[]` hits are cold pre-owned replay/crash export scratch buffers | Rejected: false hot-path allocation report | Estimate: PENDING VERIFICATION.
- [x] Unity console rechecked | DOD: MCP returned six editor compile errors in UI/ecosystem, none in input bridge files | Rejected: treating unrelated editor errors as input regression | Estimate: PENDING VERIFICATION.

## Loop 10 - Replay Writer Failure Hardening

- [x] Corrected owner dump path | DOD: `InputDumpRelativePath` now targets `Docs/AgentLogs/Dump_INPUT_DETERMINISM_BRIDGE.bin` | Rejected: stale `UNIVERSAL_INPUT_ORCHESTRATOR` owner path | Estimate: evidence routing correctness.
- [x] Replay setup failure telemetry | DOD: `EnsureInputReplayWriter()` catch reports `CrashTelemetryBuffer.ReportBlackBoxExportFailure()` before releasing replay map resources | Rejected: silent replay setup failure | Estimate: postmortem reliability, cold path.
- [x] Replay writer exception guard | DOD: `InputReplayWriterLoop()` catches background exceptions, reports blackbox export failure, and requests writer stop | Rejected: uncaught background thread failure | Estimate: process stability, cold path.
- [x] Replay shutdown timeout safety | DOD: stop timeout reports export failure and avoids releasing MMF resources under a still-live writer thread | Rejected: disposing mapped resources while the writer may still flush | Estimate: use-after-release risk reduction.
- [x] Re-ran focused scans/build | DOD: no stale owner path, no 250 ms replay join, no first-party normal legacy input publish/read path, and touched-file build filter reports no input bridge diagnostics | Rejected: claiming global verification while full build still has 93 unrelated errors | Estimate: PENDING VERIFICATION.
- [x] Unity console rechecked | DOD: MCP console returned errors in MCP regex handling, `HectonUnderwaterVisuals`, and entry-point discovery; no input bridge file errors | Rejected: treating unrelated console state as input regression | Estimate: PENDING VERIFICATION.

## Loop 11 - Replay Writer Lifecycle Closure

- [x] Hardened setup null-pointer path | DOD: null MMF pointer now reports `ReportBlackBoxExportFailure()` before releasing map resources | Rejected: silent disabled replay capture | Estimate: evidence reliability, cold path.
- [x] Closed partial setup leak | DOD: setup exception resets stop/write flags, clears `_inputReplayThread`, disposes `_inputReplaySignal`, then releases map resources | Rejected: leaving a non-null unstarted thread that blocks future replay setup | Estimate: session recovery correctness.
- [x] Protected live writer wait handle | DOD: `StopInputReplayWriter()` now disposes signal/map only after the writer has stopped | Rejected: disposing wait handle or MMF resources while a slow writer may still own them | Estimate: use-after-dispose risk reduction.
- [x] Re-ran focused build filter | DOD: no diagnostics for input bridge files; full build remains blocked by 92 unrelated errors | Rejected: global green claim | Estimate: PENDING VERIFICATION.

## Loop 12 - Replay Setup Retry Backoff

- [x] Added replay setup retry gate | DOD: `_nextInputReplayRetryFrame` delays failed MMF setup retries by 300 frames | Rejected: per-frame file/MMF retry storm from `PRE_SIMULATION` | Estimate: avoids up to 60 cold setup retries/sec while storage/permission failure persists.
- [x] Reset retry state on recovery/reset | DOD: successful writer start and dispatcher frame-state clear reset `_nextInputReplayRetryFrame` to zero | Rejected: permanent disable after one transient replay setup failure | Estimate: session recovery correctness, no steady-frame cost.
- [x] Re-ran focused validation after retry patch | DOD: touched-file build filter reports no input bridge diagnostics; static scan finds no stale owner path, no 250 ms join, no legacy normal input publish/read, and no raw gameplay input APIs in touched bridge files; full build remains red with 91 unrelated errors | Rejected: claiming VERIFIED while full build remains globally red | Estimate: PENDING VERIFICATION.
- [x] Unity console retry attempted | DOD: `read_console` retried twice and Unity session did not answer ping | Rejected: claiming editor compile evidence without a responsive session | Estimate: editor verification blocked.
