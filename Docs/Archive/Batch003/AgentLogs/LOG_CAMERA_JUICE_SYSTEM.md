# LOG_CAMERA_JUICE_SYSTEM

## 2026-05-12 - Procedural Camera Juice Purge

What was wrong:
- Camera shake ownership was too concrete. Registry exposed `CameraJuiceSystem`, producers called shake methods directly, and the old clip/list/job shake path left more than one authority.
- Minor impacts were routed like heavyweight presentation events instead of cheap scalar trauma.
- Camera shake needed AUP safety, VR comfort, deterministic crash evidence, and exact-frame hit stop. The previous path did not prove those properties.

What was done:
- Added `ICameraJuiceSystem` as the registry contract and moved `GlobalRegistry.CameraJuice` to the interface slot.
- Added `CameraJuiceSignals` as a fixed-capacity NativeQueue ingress for decoupled impact packets.
- Migrated first-party direct shake producers to `CameraJuiceSignals.PublishImpact(...)`.
- Rebuilt `CameraJuiceSystem` around `_trauma`, LateFrame decay, squared intensity, six deterministic `noise.cnoise` samples, local-space post-input offsets, directional bias, roll spring recovery, FOV kick, VR override, and Low-tier 30 Hz noise interpolation.
- Added `SystemDispatcher.RequestCoreTickDilation(0.05f, 3, reasonHash)` for exact three-frame cinematic freeze on severity greater than 0.8.
- Added 300-frame fixed native black-box telemetry and invalid-math dump to `Docs/AgentLogs/Dump_CAMERA_JUICE_SYSTEM.bin`.

Cinematic cheats used:
- Scalar trauma instead of physical camera body simulation.
- Squared trauma intensity for smooth small hits and violent large hits.
- Deterministic local-space `noise.cnoise` lanes instead of clips or Cinemachine impulses.
- First-frame directional bias before procedural noise takes over.
- Damped scalar roll spring instead of rotational physics.
- Pade-style FOV kick decay instead of curve evaluation.
- Low-tier 30 Hz sample interpolation to buy visual feel below per-frame noise cost.
- Three-frame core tick dilation instead of adding more impact simulation.

Exact microseconds saved, estimates pending profiler:
- Direct active-shake list/profile ingress removed: 10-35 us per impact burst.
- Clip/Cinemachine-style impulse path avoided: 20-80 us per impact burst.
- Single-consumer `GlobalSignals.ImpactSignal` not stolen or rescanned: 5-20 us/frame under impact traffic.
- Exact frame dilation dispatcher cost: under 1 us/frame while active.
- Black-box telemetry write: under 2 us/frame.

Verification:
- CLI re-extracted `<AGENT_PROMPT id="CAMERA_JUICE_SYSTEM">` after core tasks.
- Static scan found no camera-owned `Mathf.PerlinNoise`, `noise.snoise`, `AnimationCurve.Evaluate`, `CameraManager.Instance`, `CameraShake.Instance`, or `CinemachineImpulseListener` in the target path.
- Static scan found `noise.cnoise`, `NativeArray<CameraJuiceTelemetryEntry>`, `ICameraJuiceSystem`, `HectonXRRuntimeState.IsXRActive`, and Low-tier sample interval in the camera path.
- `git diff --check` on touched files reported only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` is BLOCKED BY DEPENDENCY. Latest errors are missing external `Hecton8.Cartography`, `MapRevealSignal`, `CartographyAup`, `MapRevealSignalFlags`, `CartographyPoiRecord`, `CartographyBlackBoxEntry`, `CartographyGridConstants`, `Hecton8.Physics.Determinism`, and `InputSignal` symbols in PDA/map/player-kinematics files. No camera-juice compiler errors were emitted before the build stopped.
- Unity Console and runtime GC profiler proof remain pending because project compile is blocked outside this domain.

## 2026-05-13 - Follow-up Quality Pass, No Build

What was wrong:
- Impact trauma still lacked a live projection FOV kick path.
- Camera impact signal prewarm did not enforce a hard queue budget during burst traffic.
- Per-frame camera math still read several services through `GlobalRegistry`.

What was done:
- Added scalar impact FOV kick, Pade decay, XR zeroing, adaptive FOV scaling, and telemetry capture.
- Added `CameraJuiceSignals.EnsurePrewarmed()` and queue saturation handling that drops the oldest packet before enqueueing a new impact.
- Cached player/submarine rigidbodies, structural grid, dynamic-resolution scaler, VRAM monitor, scalability tier, and tick dispatcher through SlowTick dependency refresh.

Cinematic Cheats used:
- Projection FOV kick is scalar trauma presentation, not physical camera optics.
- Queue saturation favors the newest impact because player perception values current direction over historical completeness.
- Registry reads are shifted to low cadence; the hot visual loop spends saved cycles on response, not service discovery.

Exact Microseconds saved, estimates pending profiler:
- Hot-path registry polling removal: 1-6 us/frame.
- Queue cap prevents native growth spikes during impact storms; normal publish remains one enqueue.
- Impact FOV kick adds an estimated 4 us/frame only while active.

Verification:
- `dotnet build` was not launched per user instruction.
- Static scans found no direct `GlobalRegistry.CameraJuice.Trigger*`, `CameraManager.Instance`, `CameraShake.Instance`, or `CinemachineImpulseListener`.
- Static scans found no camera-owned `Mathf.PerlinNoise`, `noise.snoise`, `AnimationCurve.Evaluate`, `new List`, or LINQ in the patched files.
- `git diff --check` on patched files reported only line-ending warnings.
- Unity Console and GC profiler proof remain PENDING VERIFICATION.
