# CINEMATIC_FRAMER Log

## 2026-05-13 Soft Look-At Build

What was wrong:
The narrative camera path had no verified first-party procedural soft look-at. The prompt explicitly banned hard camera theft, `CutsceneManager.Instance`, dialogue `CinemachineVirtualCamera` overrides, `Quaternion.Slerp`, `Vector3.Distance`, hot-path strings, and direct cross-domain ownership.

What was done:
Added/verified `NarrativeFocusSignal(AUP, Intensity)` through `GlobalSignals` and consumed it in `HectonPlayerMovement` during camera composition. Added `FocusBrokenSignal` and `MixerStateSignal(Focus)` lanes. Added `CinematicMath.FastNlerp` overloads and applied them to the player camera rotation bias. Added focus timers, input break/yield, FOV narrowing toward 75 when tier/VR allow it, squared-distance subtitle alpha, origin-shift re-evaluation, telemetry hash publication, and a fixed 300-frame `NativeArray` black-box dump at `Docs/AgentLogs/Dump_CINEMATIC_FRAMER.bin`.

Cinematic cheats used:
Soft nlerp camera pull instead of a camera track; scalar FOV bias instead of lens/camera stack override; squared AUP distance instead of sqrt distance; subtitle hash/fade telemetry instead of managed text projection; bitmask target flags instead of object graph queries; NativeQueue edge signals instead of direct audio/narrative callbacks.

Dependency blocks:
Spatial subtitles are blocked by the missing `UI_LOCALIZATION_BABEL` BRG text-quad renderer/provider. Creature head-bone targeting is blocked by the missing public fauna head AUP/matrix contract; existing leviathan head pose is private to the fauna owner and `IFaunaSim` exposes only readiness/capacity. Both blocks are recorded as checked dependency blocks in `Docs/Tasks/Status_CINEMATIC_FRAMER.md`.

Exact microseconds saved / spent:
Active focus path estimate on i3/MX350: 4-8 us per frame for one focus target. Sqrt removal saves 1 sqrt per active frame. Avoided Cinemachine/dialogue track overhead: unmeasured, but runtime path adds no new camera stack. Audio ducking is 1 NativeQueue enqueue on focus start and release. Focus break is 1 NativeQueue enqueue. Black box is 1 fixed struct write per active focus frame and 0 B/frame after cold allocation.

Verification:
Prompt re-read: `CURRENT_BATCH.md` lines 612-655. Static scans found no `Vector3.Distance` or `Quaternion.Slerp` in the focus path and found `CinematicMath.FastNlerp` at `HectonPlayerMovement.cs:7529`. `dotnet build Hecton8.Core.csproj` remains blocked by global baseline contract failures (`Hecton8.Core.Scheduling`, `Hecton8.Core.Memory.Layout`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `IInertialNavigationService`, `BinaryBlittableSafe`, `TetherFiredSignal`, acoustic contracts). Unity MCP script validation failed because no Unity session is available. Status remains PENDING due global compile/Unity validation blocks.
