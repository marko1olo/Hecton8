# HECTON8_XR Log

## Entry 1

Status: PENDING VERIFICATION

What was wrong:
- The repo already separated PC and VR through XR-active guards and GlobalRegistry null providers, but the tunnel threshold was below the requested 15 m/s.
- The existing VR comfort vectors were not consumed by the VR brownout fullscreen pass.
- Snap-turn yaw was atomic, but there was no explicit short blackout envelope.

What was done:
- Started Loop 1 for tasks 1-3 with renderer/shader follow-through.
- Created status and rationale files for persistent agent memory.

Cinematic Cheats used:
- Dithered peripheral black mask instead of physical optical-flow simulation.
- Yaw-only platform basis instead of physically inheriting submarine pitch/roll into the headset.

Exact Microseconds saved:
- Not profiler-measured. Static estimate: avoids always-on blur and coroutine/UI overlay cost; runtime proof pending.

## Entry 2

Status: PENDING VERIFICATION

What was wrong:
- VR movement published `_HectonVrComfortSignals`, but the fullscreen brownout feature only reacted to BIOS brownout, focus blur, and near-collision.
- High-speed tunnel reference was `5.25 m/s`, below the requested VR gate.
- Platform camera composition still inherited submarine pitch/roll through the platform basis while VR horizon lock was active.
- Snap turn had an atomic yaw swap but no explicit 0.1 second blackout envelope.

What was done:
- Added yaw-only platform basis compensation for VR horizon lock: platform yaw is preserved, platform tilt is removed.
- Raised `vrComfortHighSpeedMetersPerSecond` default to `15 m/s` and validation max to `25 m/s`.
- Added `vrSnapTurnFadeSeconds = 0.1` and `_vrSnapTurnFadeTimer`; snap turn now publishes blackout through the existing comfort vector, no coroutine.
- Extended `HectonVRBrownoutFeature` to pull sanitized `_HectonVrComfortSignals` and `_HectonVrComfortMotion` globals only when XR is active.
- Extended `Hidden_Hecton_VRBrownout.shader` with dithered black peripheral tunnel and full blackout envelope.

Cinematic Cheats used:
- Dithered black peripheral mask instead of simulating optical-flow suppression physically.
- Yaw-only quaternion basis instead of a physical cockpit counter-rotation rig.
- Scalar blackout timer in the existing shader signal instead of an overlay canvas.

Exact Microseconds saved:
- Not profiler-measured. Static estimate remains <10 us CPU for camera basis + shader vector path and <5 us CPU for snap timer. GPU cost is one existing fullscreen pass only when XR comfort/brownout/near-focus signals are active.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` succeeded: 0 errors, 0 warnings.
- `git diff --check` returned only line-ending warnings for existing touched files.
- Unity Editor console, headset runtime, shader import, and profiler proof are still pending.
