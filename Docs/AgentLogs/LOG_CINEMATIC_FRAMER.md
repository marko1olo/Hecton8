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

## 2026-05-13 Continuation Hardening

What was wrong:
Re-review found three avoidable defects: Core had an unnecessary reference to `Hecton8.Narrative.Camera`, disabled focus stopped draining `NarrativeFocusSignal` entries, and active focus direction rebuilt player AUP from rigidbody runtime position instead of using the authoritative locomotion AUP snapshot.

What was done:
Removed `Hecton8.Narrative.Camera` from `Hecton8.Core.asmdef`. Updated `DrainNarrativeFocusSignals` to drain bounded focus signals while disabled and release active audio ducking. Updated `TryResolveCinematicFocusDirection` to use `_playerState.AbsolutePosition`.

Cinematic Cheats used:
No new visual cost. The system remains one AUP delta, rsqrt direction, nlerp pull, scalar FOV bias, edge-only signals, and fixed ring telemetry.

Exact Microseconds saved:
Removed one runtime-to-AUP conversion per active focus frame. Estimate improves from 4-8 us to 3-7 us per active focus frame on i3/MX350. Queue drain remains capped at four signals per frame.

Verification:
Static scan shows no `Vector3.Distance` or `Quaternion.Slerp` in touched focus files. `CinematicMath.FastNlerp` remains the only focus rotation blend. `Hecton8.Core.asmdef` no longer references `Hecton8.Narrative.Camera`. `dotnet build Hecton8.Core.csproj` still fails on global baseline missing contracts/asmdefs; Unity MCP validation still returns `no_unity_session`.

## 2026-05-13 Hot-Path Purge

What was wrong:
The focus acceptance path still refreshed the cinematic focus tier gate through `GlobalRegistry`, and subtitle alpha still used a scalar division. Neither was catastrophic, but both were unnecessary hot-path work.

What was done:
Removed `RefreshCinematicFocusTierGateCold` from `ApplyNarrativeFocusSignal`; focus FOV permission now uses the cached gate initialized in lifecycle setup. Replaced subtitle fade division with `math.rcp(fadeSq)` reciprocal multiply.

Cinematic Cheats used:
No honest simulation added. The system stays on squared AUP distance, cached tier flags, nlerp pull, scalar FOV bias, NativeQueue events, and fixed ring telemetry.

Exact Microseconds saved:
Saved one `GlobalRegistry` read per accepted focus signal and one scalar division per active focus frame using subtitle fade. Updated estimate remains roughly 3-7 us per active focus frame on i3/MX350. Exact profiler proof is still blocked by no Unity session and global compile failures.

Verification:
`rg` confirms `RefreshCinematicFocusTierGateCold` is only called from cold lifecycle paths, `ApplyNarrativeFocusSignal` no longer calls it, and `ResolveCinematicSubtitleAlpha01` uses `math.rcp`. Final `dotnet build Hecton8.Core.csproj --no-restore` probe remains blocked by unrelated global baseline errors including missing fluids/CCD/audio/memory contracts and unrelated brine edits in `HectonPlayerMovement.cs`.
