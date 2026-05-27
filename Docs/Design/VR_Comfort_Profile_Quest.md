# VR Comfort Profile - Quest 2/3

Date: 2026-05-14
Owner: SOMATIC_COMFORT_ANALYST
Status: STATIC COMFORT PROFILE DEFINED / PENDING RUNTIME VERIFICATION

## Authority Boundary

Static documentation only. Current source, active architecture contracts, fresh proof artifacts, and official platform rules override dated claims in this file. No runtime, profiler, memory, render, platform, public-page, or ship-readiness proof is implied by this file alone.

Domain: VR Somatic Comfort / Haptic Feedback Director

Evidence read:
- `Docs/Archive/Batch003/AgentLogs/LOG_VR_SOMATIC_ENGINEER.md`
- `Docs/Archive/Batch003/AgentLogs/Rationale_VR_SOMATIC_ENGINEER.md`
- `Docs/Archive/Batch003/AgentLogs/LOG_VR_COMFORT_VANGUARD.md`
- `Assets/_Project/Scripts/Gameplay/VRSomaticProvider.cs`
- `Assets/_Project/Scripts/Tools/ToolHapticsRuntime.cs`

Mandates followed:
- `CTRL_Device_Abstraction_Haptics.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`

## Calibration Boundary

This profile defines calibration data and audit rules. It does not create a new runtime manager.

Machine-readable companion: `Docs/Design/VR_Comfort_Profile_Quest.json`.

Runtime owners already exist:
- HMD/root stabilization and somatic vignette: `VRSomaticProvider`.
- Movement/FPS comfort scalar: movement comfort path writing `_VRComfortVignette01`.
- Haptic command payload: `ToolHapticsRuntime.HapticCommand`.

Comfort remains a deterministic presentation fake: shader edge tunneling, HUD/PDA scale reduction, and bounded haptic pulses. Camera projection FOV mutation is rejected for XR.

## Runtime Integration Handoff

Profile loading policy:
- Load JSON only during bootstrap, build-time baking, or editor tooling.
- Do not parse JSON in `Tick`, `LateUpdate`, `FixedUpdate`, Burst jobs, or haptic dispatch.
- Runtime data must be copied into fixed fields or preallocated arrays before the comfort path runs.

Required owner bindings:

| Runtime owner | Field / limit | Profile source |
|---|---|---|
| `VRSomaticProvider` | `rotationJerkLimitRadiansPerSecondCubed` | `jerk.fullEventRadS3` |
| `VRSomaticProvider` | `MaxSomaticHeadAngularJerkRadiansPerSecondCubed` | `jerk.hardCapRadS3` |
| `VRSomaticProvider` | `JerkEventDebounceSeconds` | `jerk.eventDebounceSeconds` |
| `VRSomaticProvider` | `rotationJerkVignetteContribution` | `jerk.maxVignetteContribution` |
| `VRSomaticProvider` | `rootRotationSmoothingSharpness` | `stabilization.modes.middle.sharpness` |
| `VRSomaticProvider` | `comfortVignetteMaximum` | `devices.Quest3_90Hz.opacityMax` |
| `VRSomaticProvider` | `comfortAccelerationSoftTunnelStartRadS2` | `devices.Quest3_90Hz.accelSoftTunnelStartRadS2` |
| `VRSomaticProvider` | `comfortAccelerationEmergencyClampRadS2` | `devices.Quest3_90Hz.accelEmergencyClampRadS2` |
| `VRSomaticProvider` | `comfortAccelerationReleaseBelowRadS2` | `devices.Quest3_90Hz.releaseBelowRadS2` |
| `VRSomaticProvider` | `comfortAccelerationReleaseHysteresisSeconds` | `devices.Quest3_90Hz.releaseHysteresisSeconds` |
| `VRSomaticProvider` | `comfortVignetteAttackSlewPerFrame` | `devices.Quest3_90Hz.attackSlewPerFrame` |
| `VRSomaticProvider` | `comfortVignetteReleaseSlewPerFrame` | `devices.Quest3_90Hz.releaseSlewPerFrame` |
| `ToolHapticsRuntime` | `BufferCapacity` | `haptic.limits.bufferCapacity` |
| `ToolHapticsRuntime` | `MaxCommandDurationSeconds` | `haptic.limits.durationMaxSeconds` |
| `ToolHapticsRuntime` | `MaxCommandFrequencyHz` | `haptic.limits.frequencyMaxHz` |

Execution lane:
- Somatic visual application stays in `VISUAL_SYNC` or the existing late-frame VR rig sync lane.
- Haptic patterns must enter through `ToolHapticsRuntime`; no direct OpenXR rumble call from gameplay code.
- Profile combine semantics remain `max`. `VRSomaticProvider` must max-combine angular-speed and angular-acceleration tunnel scalars before publishing `_VRComfortVignette`; a later runtime integration that sums tunnel contributors is a comfort regression.

## Jerk And Acceleration Thresholds

Units:
- Angular velocity: radians/second.
- Angular acceleration: radians/second squared.
- Angular jerk: radians/second cubed.
- Sampling cadence: Quest 2 at 72 Hz, Quest 3 at 90 Hz.

Maximum angular acceleration allowed before `FOV_Tunneling` starts:

| Device | Untunneled Max | Soft Tunnel Start | Strong Tunnel | Emergency Clamp | Release Hysteresis |
|---|---:|---:|---:|---:|---:|
| Quest 2 | 42.0 rad/s2 | 42.0 rad/s2 | 96.0 rad/s2 | 150.0 rad/s2 | below 24.0 rad/s2 for 0.25 s |
| Quest 3 | 50.0 rad/s2 | 50.0 rad/s2 | 112.0 rad/s2 | 180.0 rad/s2 | below 30.0 rad/s2 for 0.22 s |

Tunnel opacity contribution from angular acceleration:

| Accel01 | Meaning | Quest 2 Opacity | Quest 3 Opacity |
|---:|---|---:|---:|
| 0.00 | below soft start | 0.00 | 0.00 |
| 0.25 | early discomfort | 0.16 | 0.13 |
| 0.50 | strong comfort guard | 0.30 | 0.26 |
| 0.75 | near emergency | 0.43 | 0.38 |
| 1.00 | emergency clamp | 0.52 | 0.46 |

Angular jerk culling parameters:

| Parameter | Value | Reason |
|---|---:|---|
| Soft jerk warning | 180 rad/s3 | starts visor/HUD damping without forcing a full tunnel |
| Full jerk event threshold | 320 rad/s3 | matches current `rotationJerkLimitRadiansPerSecondCubed` default |
| Hard jerk cap | 1440 rad/s3 | matches current `MaxSomaticHeadAngularJerkRadiansPerSecondCubed` clamp |
| Jerk event debounce | 0.20 s | matches existing somatic debounce; prevents haptic/telemetry spam |
| Max jerk vignette contribution | 0.28 | matches current serialized default |

Runtime combine rule:

`finalTunnel = max(speedLutOpacity, angularAccelerationOpacity, jerkOpacity, frameRateSafetyOpacity)`

Do not sum these values. Summing punishes the player during combined motion and creates sudden darkness. The shader/HUD consumers already max-combine movement and somatic scalars.

Slew limits:
- Attack: max +0.055 opacity per frame at 72 Hz; max +0.050 at 90 Hz.
- Release: max -0.025 opacity per frame at 72 Hz; max -0.022 at 90 Hz.
- Any single-frame opacity jump above 0.10 is classified as `VISUAL_TELEPORT_SHOCK`.

## Movement-Speed Vignette LUT

Use horizontal player speed in meters/second after finite checks. Vertical bob and head micro-motion do not enter this LUT.

Quest 3 baseline LUT:

| Speed m/s | Opacity |
|---:|---:|
| 0.0 | 0.000 |
| 0.5 | 0.000 |
| 1.0 | 0.010 |
| 1.5 | 0.035 |
| 2.0 | 0.070 |
| 2.5 | 0.105 |
| 3.0 | 0.145 |
| 3.5 | 0.185 |
| 4.0 | 0.225 |
| 4.5 | 0.260 |
| 5.0 | 0.295 |
| 6.0 | 0.350 |
| 7.0 | 0.395 |
| 8.0 | 0.425 |
| 10.0 | 0.450 |
| 12.0 | 0.460 |

Quest 2 multiplier:
- Apply `opacity * 1.13`.
- Clamp to `0.52`.
- Keep the same speed breakpoints.

Frame-rate safety override:
- Quest 2: if real XR frame delta exceeds 16.67 ms for 2 consecutive frames, force minimum tunnel opacity `0.12`.
- Quest 3: if real XR frame delta exceeds 13.89 ms for 2 consecutive frames, force minimum tunnel opacity `0.10`.
- Release only after 12 stable frames. This is a safety tunnel, not a quality setting.

## Cockpit Stabilization FastNlerp Values

Existing job blend form:

`alpha = (sharpness * dt) / (1 + sharpness * dt)`

Use these alpha targets for the horizon-locked VR rig:

| Mode | Sharpness | Quest 2 alpha at 72 Hz | Quest 3 alpha at 90 Hz | Use |
|---|---:|---:|---:|---|
| Low / weak device | 10 | 0.122 | 0.100 | cheap smoothing, less visual noise |
| Middle / default | 14 | 0.163 | 0.135 | matches current `rootRotationSmoothingSharpness` default |
| High | 19 | 0.209 | 0.174 | tighter cockpit lock after profiling |
| Ultra | 24 | 0.250 | 0.211 | visual-overkill stability on strong hardware |
| Jerk clamp transient | 34 | 0.321 | 0.274 | max 0.18 s when jerk event fires |
| Settle after snap | 6 | 0.077 | 0.062 | first 0.10 s after snap-turn completion |

Guardrails:
- Do not exceed alpha `0.33` in XR. Above that, the root feels glued to the jerk instead of damping it.
- Jerk clamp transient must decay through hysteresis, not immediate flipping.
- Stabilization must stay in `VISUAL_SYNC` or the existing late-frame root sync lane. It must not mutate gameplay truth.

## Scalability

Low:
- Quest 2 style thresholds.
- LUT + acceleration opacity only.
- No extra haptic layers beyond critical comfort pulses.
- Baked/hard edge vignette path allowed.

Middle:
- Quest 3 baseline thresholds.
- Jerk scalar contributes up to `0.28`.
- Haptics use priority/debounce through `ToolHapticsRuntime`.

High:
- Same gameplay truth.
- Higher cockpit stabilization alpha.
- Richer visor edge response and smoother decay.

Ultra:
- Same gameplay truth.
- More polished visual edge texture or procedural edge detail.
- Optional device-specific trigger resistance only through the platform abstraction, not tool code.

## Failure Modes

- Non-finite angular velocity, acceleration, jerk, speed, or opacity: write fallback `0`, keep last valid root state, and emit telemetry hash.
- Haptic buffer full: obey existing priority replacement; comfort patterns must not bypass `ToolHapticsRuntime`.
- Frame drop: force safety tunnel; do not change camera projection.
- AUP shift: clear acceleration/jerk history for at least one frame.
- Device disconnect: skip haptic dispatch; keep visual comfort active.

## Verification Boundary

Static source and documentation evidence only:
- Current code already owns somatic root, jerk telemetry, and haptic command queue.
- No Unity Play Mode, GCMonitor, Quest device run, or profiler capture was executed in this pass.
- Runtime comfort remains `PENDING VERIFICATION` until a Quest 2/3 headset test captures frame time, GC, and user comfort notes.
