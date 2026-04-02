# DEV RUNTIME PROFILER SCENE HOOK — 2026-04-02

## What was done

- Added a disabled scene object `__DEV_RuntimePerformanceProfiler` under the `--- SYSTEMS ---` root in:
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- Attached `Hecton8.Dev.RuntimePerformanceProfiler` to it.
- Saved the scene.

## What this means in simple terms

The project now has a ready-to-use runtime profiler hook in the main world scene.

It is disabled by default, so it does not add runtime overhead until someone explicitly enables it.

## What this gives

- future optimization passes can be based on real numbers instead of code guesses only
- no gameplay behavior change right now
- no permanent profiling spam in normal scene usage

## What was verified

- Scene saved successfully.
- Unity console stayed at `0 log entries` after adding the disabled profiler hook.

## What remains open

- The profiler hook is present, but it still needs an intentional enable/run workflow for real capture sessions.
