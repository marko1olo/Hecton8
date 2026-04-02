# WORLD GEOLOGY BINDING REGISTRY HARDENING — 2026-04-02

## What was wrong

- `WorldGenerativeGeologyIntegrationDirector` rebuilt seam plans by scanning the whole scene for `WorldGenerativeGeologyBinding`.
- The serialized switch `includeInactiveBindings` existed, but the hot path still always used the expensive full scene scan.
- Runtime smoke lookup for geology bindings also repeated the same scene-wide search.

## What was done

- Added an active-binding registry directly inside `WorldGenerativeGeologyBinding`.
- `WorldGenerativeGeologyIntegrationDirector` now uses that registry when `includeInactiveBindings` is off.
- Preserved the old fallback path for cases where inactive bindings really must be included.
- `WorldGenerativeGeologyRuntimeSmokeTester` now checks the active registry first before falling back to a scene scan.

## What this means in simple terms

- The geology runtime no longer walks the whole scene every refresh when only active nearby bindings matter.
- The existing inspector switch finally does what it says.
- Smoke checks now follow the same fast runtime path instead of always using the slow one.

## What was verified

- Unity compiles without `Error`.
- Short `play -> stop` smoke completed with an empty console.
- No new first-party warnings or errors were introduced by this pass.
