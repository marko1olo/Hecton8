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

## 2026-05-13 Edge-Only Focus Hardening

What was wrong:
Focus refreshes could enqueue `MixerStateSignal(Focus)` repeatedly while audio was already ducked. Clearing focus released audio but left stale focus hash, subtitle hash, target AUP, and subtitle alpha in inactive fields. The binary dump wrote the black-box ring in raw storage order, which weakens postmortem reconstruction.

What was done:
Added an audio-duck edge gate in `ApplyNarrativeFocusSignal`, scrubbed inactive focus metadata in `ClearCinematicFocus`, added `_cinematicFocusBlackBoxCount`, and changed dump export to write populated entries oldest-to-newest.

Cinematic Cheats used:
No new simulation. The framing still uses one AUP delta, nlerp pull, scalar FOV bias, squared-distance subtitle alpha, edge-only mixer/focus signals, and fixed ring telemetry.

Exact Microseconds saved:
Repeated focus refreshes now save one NativeQueue mixer enqueue each after the initial duck. Active telemetry adds only one bounded integer increment per active frame. Dump ordering is fault-only and has 0 normal-frame cost. Low-end estimate remains roughly 3-7 us per active focus frame.

Verification:
`git diff --check` passed. Static scan found no `Vector3.Distance`, `Quaternion.Slerp`, hot-path string formatting, runtime `Find`, `Camera.main`, coroutine, or new managed container in the touched focus path. Unity MCP validation still returns `no_unity_session`. `dotnet build Hecton8.Core.csproj --no-restore` still fails on global missing contracts/namespaces before focus code can be compiler-proven.

## 2026-05-13 Focus Telemetry Gate

What was wrong:
Active focus telemetry still published on every accepted refresh, even after mixer ducking was edge-gated. The black-box dump also caught only `IOException`, so a path/security/export fault could escape the diagnostic path.

What was done:
Added a `focusChanged` gate before `GlobalTelemetryBus.PublishPerformanceWarning(_cinematicFocusTelemetryHash, ...)` and broadened the cold dump catch to `System.Exception`.

Cinematic Cheats used:
No new simulation. This is signal hygiene only: lifecycle telemetry, edge-only ducking, fixed ring dump, and no extra camera math.

Exact Microseconds saved:
Saves one telemetry enqueue for each duplicate refresh of the same focus hash. Fault catch broadening has 0 normal-frame cost. Runtime estimate remains roughly 3-7 us per active focus frame pending profiler proof.

Verification:
Static scan found `focusChanged` gating in `ApplyNarrativeFocusSignal`, broad dump catch, no `Vector3.Distance`, and no `Quaternion.Slerp` in `HectonPlayerMovement.cs`. `git diff --check` passed for the script. Unity MCP validation still returns `no_unity_session`. `dotnet build Hecton8.Core.csproj --no-restore` timed out after 120s on the same global missing `Fluids`, `Scheduling`, `Memory.Layout`, `Physics.CCD`, `Audio.Propagation`, and `IGroundRadarService` failures before focus code could be compiler-proven.

## 2026-05-13 Input Yield Reciprocal

What was wrong:
The active player-resistance path still used one scalar division for focus pull suppression when mouse delta was above the yield band but below the break threshold.

What was done:
Changed suppression from `deltaSq / thresholdSq` to `deltaSq * math.rcp(thresholdSq)` inside `ApplyCinematicFocusInputOverride`. Re-extracted the CINEMATIC_FRAMER prompt from `CURRENT_BATCH.md`; it remains 19 tasks and `PENDING VERIFICATION`.

Cinematic Cheats used:
No new simulation. This is a cheaper scalar fake for the same player-yield behavior: squared mouse delta, reciprocal multiply, nlerp camera pull, edge-only signals, and fixed telemetry.

Exact Microseconds saved:
Saves one scalar division on active focus frames where player input exceeds the yield band. Estimate remains roughly 3-7 us per active focus frame on i3/MX350 pending profiler proof.

Verification:
Assembly recheck shows `Hecton8.Narrative.Camera.asmdef` references `Hecton8.Core.Contracts`, while `Hecton8.Core.asmdef` has no narrative-camera reference and no `using Hecton8.Narrative.Camera` exists under `Assets`. Scoped `git diff --check` passed for `HectonPlayerMovement.cs` plus CINEMATIC_FRAMER docs. Banned scan found no `deltaSq / thresholdSq`, `Vector3.Distance`, `Quaternion.Slerp`, hot string formatting, runtime `Find`, `Camera.main`, or coroutine in `HectonPlayerMovement.cs`. Unity MCP validation still returns `no_unity_session`; `dotnet build Hecton8.Core.csproj --no-restore` timed out after 120s. The current workspace has unrelated brine shader-global throttling in `HectonPlayerMovement.cs` and many unrelated dirty files; global `git diff --check` fails on unrelated `.meta` trailing whitespace outside this domain.
