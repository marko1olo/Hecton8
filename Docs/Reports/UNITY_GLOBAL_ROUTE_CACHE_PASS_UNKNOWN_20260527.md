# Unity Global Route Cache Pass UNKNOWN - 2026-05-27

Scope: Core and mod bridge runtime dependency routes. Full project compile wall was not touched by explicit user instruction.

## What Was Wrong

- `ConnectionSplineBatchRenderer` still read `GlobalRegistry.Dispatcher` from helpers called by live pipe/relay submit and remove routes.
- `ThreadSafeCommandQueue` could fall back to `GlobalRegistry.ObjectPoolService` during late-frame command drain when the cached pool was null.
- `SystemDispatcher` read `GlobalRegistry.Physics` during late-frame environment artery pending-count and flush.
- `ModWorldPersistenceManager` read `GlobalRegistry.ObjectPoolService` in mod spawn, despawn, and save restore routes.
- `SceneRuntimeService` used registry wrappers for core tick lane registration and read registry services from transition presentation routes.

## What Changed

- `ConnectionSplineBatchRenderer` now keeps dispatcher availability as a cold/hot-swap cache and registers late-frame work through `SystemDispatcher.Register`.
- `ThreadSafeCommandQueue` now uses a cached object-pool service only. `SystemDispatcher` binds that cache during cold dependency refresh and object-pool hot-swap.
- `SystemDispatcher` now caches `IPhysicsService`; late-frame physics pending-count and flush use `ResolveCachedPhysicsService()`.
- `ModWorldPersistenceManager` now caches `IObjectPoolService`; object-pool hot-swap updates the cached field.
- `SceneRuntimeService` now uses direct dispatcher lane registration, cached terminal boot service handles, cached world-drone audio bridge, cached tick dispatcher, and cached camera-juice service.

## Proof

- `git diff --check` passed for the touched source files.
- Brace balance was `0` for:
  - `ConnectionSplineBatchRenderer.cs`
  - `ThreadSafeCommandQueue.cs`
  - `SystemDispatcher.cs`
  - `ModWorldPersistenceManager.cs`
  - `SceneRuntimeService.cs`
- `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_GLOBAL_ROUTE_CACHE_RECHECK.json`: `files=2442`, `shaders=71`, `errors=0`, `confirmedErrors=0`, `warnings=145`, `infos=1172`.

## Residuals

- Remaining direct registry reads in touched files are cold bootstrap, hotswap, shutdown, diagnostics, or scene-transition clear routes.
- `ModSettingsRegistry` and `ModRuntimeState.ResolveActiveCatalog()` still read registry services, but this pass did not prove a per-frame cadence. They should be fixed only with mod API cadence proof.
- Full `Hecton8.slnx` build was not run and no global compile errors were chased.
