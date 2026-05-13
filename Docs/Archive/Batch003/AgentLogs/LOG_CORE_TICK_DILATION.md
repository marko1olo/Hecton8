# CORE_TICK_DILATION Log

## 2026-05-12 - Time Dilation Dispatcher Purge

What was wrong:
Unity `Time.timeScale`, scattered pause/hit-stop writes, and ad hoc tick ownership were controlling simulation, UI, physics, and audio from different places. That breaks deterministic cadence, freezes menu/HUD work during pause, and gives no blackbox state when slow-motion fails.

What was done:
Implemented `ITickDispatcher`, `H8TimeSnapshot`, `H8TimeSlot`, `IFastTickable`, `IColdTickable`, and `IUnscaledFastTickable`; added dispatcher-owned dilated/unscaled time storage, scalar pause/dilation control, 60 Hz/10 Hz/1 Hz/0.2 Hz cadence gates, preallocated raw bucket iteration, dilated fixed-step dispatch, pause-safe late-frame queue guards, and AUP one-frame pause. Routed pause menu, suit HUD, physics hit-stop, scene reset, watchdogs, audio pitch, visor bullet-time overlay, and crash telemetry through registry/signals instead of Unity global time.

Cinematic Cheats used:
Bullet-time is sold as a visor vignette/chromatic-aberration signal, not physical motion blur. Low tier disables the post effect entirely. Audio slow-motion uses a floor-limited multiply-only pitch ratio, not heavy pitch drop or DSP resimulation. Pause freezes simulation arteries but keeps menu/UI lanes draining.

Exact Microseconds saved:
Measured profiler data is unavailable because `dotnet build Hecton8.Core.csproj --no-restore` is blocked by generated-project dependency errors outside this task. Static estimates recorded in the status file: 0.8 us singleton lookup avoidance, 3.0 us pause signal containment, 2.0 us H8Time consolidation, 6.0 us raw bucket traversal at 128 subscribers, 10.0 us hitch containment, 4.0 us fixed-step decoupling, 2.5 us audio scalar refresh, 1.0 us unscaled UI lane, up to 100.0 us saved while paused by skipping simulation arteries, 0.4 us AUP pause guard, 20.0 us MX350 fill saved by low-tier visor-disable, 0.7 us telemetry ring write, 0.2 us from replacing audio `math.sqrt` with multiply-only easing. These are estimates, not profiler-confirmed.

Verification:
`Task.Delay`, `Time.deltaTime`, `Time.fixedDeltaTime`, managed `foreach`, and `.ForEach` are absent from edited timing paths. `Time.timeScale` remains only in `Core/BootstrapContracts/BootstrapStatus.cs`, the safe-halt bootstrap boundary that cannot reference Core. Build remains blocked by stale/missing generated project references for `Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, and `Hecton8.Cartography`.

## 2026-05-13 - Static Recheck and Coupling Cleanup

What was wrong:
The first polish pass still left timing debt in edge paths: AUP interpolation sampled Unity fixed-time globals, camera hit-stop held a concrete dispatcher dependency, pause audio/verifiers reasoned with time-scale-era semantics, and DataVault maintenance was resolved through the registry after the dispatcher already owned the timing lane.

What was done:
Published dispatcher-owned `CurrentFixedInterpolationAlpha` from the fixed-step accumulator and moved `HectonFloatingOrigin` to that value. Added `RequestCoreTickDilation` to `ITickDispatcher`, routed camera hit-stop through the interface, cached `IDataVault` in `SystemDispatcher`, and changed pause audio/smoke verifiers to read `ITickDispatcher.SimulationPaused` with `GlobalSignals` fallback. Replaced world scatter dev diagnostics from `Time.timeScale` to the canonical dilation scalar.

Cinematic Cheats used:
No new physical simulation. AUP presentation receives one scalar from the dispatcher; bullet-time remains a post/audio fake driven by signals. Low tier stays scalar-only; higher tiers can spend presentation cost outside the core scheduler.

Exact Microseconds saved:
Static estimates only because build/runtime profiling was not run: 0.2-0.5 us per DataVault maintenance touch from cached lookup, 1-3 us during pause/hit-stop verification paths from interface/global-signal reads, and sub-0.1 us for dispatcher alpha publication. The main gain is deterministic timing authority, not raw CPU.

Verification:
No `dotnet build` launched per user instruction. Targeted `rg` found no `Time.timeScale`, `Time.deltaTime`, `Time.fixedDeltaTime`, `Task.Delay`, `.ForEach`, or concrete `SystemDispatcher _dispatcher` in edited timing/pause/AUP/world-scatter integration files. Broad scan leaves only `BootstrapStatus` safe-halt `Time.timeScale`, dev-only `CelestialTimeLapseDebugger` fixed-delta, and comment/documentation references outside this task's edited runtime timing path. `git diff --check` passed with only checkout line-ending warnings.

## 2026-05-13 - Pause Precedence and Clock Authority Pass

What was wrong:
Frame-count hit-stop could outlive a pause request and feed nonzero dilated delta because burst scalar resolution happened after pause signal draining. Physics hit-stop duration, legacy tick fallback, and frame watchdog sampling also still read separate Unity unscaled time.

What was done:
Made pause/freeze outrank core hit-stop. `RequestSimulationPause(true)` now captures the pre-burst restore scalar, clears burst state, and holds scalar zero. `RequestCoreTickDilation` ignores paused/frozen state and uses a nonzero minimum so it cannot create a stuck zero-scalar burst. External `RequestTimeDilation` clears pending frame bursts. Physics hit-stop duration, `GameTickManager` bootstrap fallback, and `FrameTimeWatchdog` now consume dispatcher unscaled delta.

Cinematic Cheats used:
Hit-stop remains a controllable scalar fake. Pause is authoritative freeze; visuals can still sell impact on higher tiers through the visor/audio signal path without advancing simulation time.

Exact Microseconds saved:
Static estimates only: sub-0.2 us from clearing stale burst checks on pause, 0.3-0.8 us across watchdog/hit-stop/fallback sampling paths by reusing dispatcher unscaled delta. No profiler run; no build launched.

Verification:
No `dotnet build` launched. `rg TimeManager.Instance` returns no hits. Targeted timing-path audit leaves only `SystemDispatcher` as the Unity unscaled delta source in edited core/physics/pause/AUP files. `git diff --check` passed with only line-ending normalization warnings.

## 2026-05-13 - Runtime Visual Clock Cleanup

What was wrong:
Surface weather rain shedding and scene transition dissolve timing sampled Unity unscaled delta directly, creating independent visual timing decisions outside the dispatcher.

What was done:
Moved `HectonSurfaceWeatherDirector` rain load-shed and `SceneRuntimeService` cinematic transition progression to `SystemDispatcher.CurrentFrameUnscaledDeltaTime`, with a fixed fallback for async transition loops where the dispatcher has not produced a frame yet.

Cinematic Cheats used:
Screen-space rain remains a cheap visual shed, not physical precipitation simulation. Scene transition timing remains unscaled so menus/loading presentation does not freeze with simulation.

Exact Microseconds saved:
Static estimate only: 0.1-0.4 us from avoiding duplicate Unity time reads in visual timing paths. No profiler run; no build launched.

Verification:
No `dotnet build` launched. Targeted timing-path audit leaves only `SystemDispatcher` as the Unity unscaled delta source in edited files. Broad remaining Unity-time hits are safe-halt bootstrap, dev/editor/tool harnesses, `HectonUnderwaterVisuals` editor-preview tick, and comments/documentation outside this task's runtime core path.
