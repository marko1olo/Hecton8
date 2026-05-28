# Unity Homeostasis DataVault Rebind Pass - UNKNOWN - 2026-05-28

Status: PENDING VERIFICATION.

## Scope

- Domain: Core & Memory Infrastructure.
- Files changed: `Assets/_Project/Scripts/Core/HomeostasisBrain.cs`.
- Read-only dirty neighbor: `Assets/_Project/Scripts/Core/HomeostasisBrain.ScalabilityDictator.cs`.
- First-20-minutes blocker removed: global stability telemetry no longer reopens core Homeostasis buffers from the next pre-simulation frame after a DataVault replacement.

## What Was Wrong

- `RebindRegistryDependency(DataVault)` released Homeostasis and ScalabilityDictator Vault handles, assigned the new `_dataVault`, and left all runtime handles as `default`.
- The next `PreSimulationTick()` called `TryResolveRuntimeBuffers()`.
- Before this pass, `TryResolveRuntimeBuffers()` could call `TryResolveOrAcquire()` and then `vault.EnsureGenerationHandle<T>()`.
- That made the pre-simulation frame phase a hidden DataVault open/allocation fallback after a service replacement.

## What Was Done

- Added `ReopenRuntimeBuffersAfterDataVaultRebindCold()` and call it after DataVault rebind.
- Renamed the mutating runtime-buffer route to `OpenOrAcquireRuntimeBuffers()`.
- `InitializeRuntime()` and DataVault hot-swap use `OpenOrAcquireRuntimeBuffers()`.
- `PreSimulationTick()` now keeps using `TryResolveRuntimeBuffers()`, but that method is now resolve-only and returns false if handles are missing or invalid.
- Removed the unused mutating `TryResolveHardwareMetrics()` helper.
- Left the dirty ScalabilityDictator partial untouched; its existing `TryResolveRuntimeBuffers()` call sites now consume the pure resolver.

## Rejected Alternatives

- Touching `HomeostasisBrain.ScalabilityDictator.cs` was rejected because another agent already has active edits there.
- Keeping `TryResolveRuntimeBuffers()` mutating was rejected because read/resolve helpers must not allocate or open native buffers.
- Reopening only the three Homeostasis buffers was rejected because `ResetScalabilityDictatorVaultHandles()` also clears MathLOD/scalability handles; the owner hot-swap route must reopen the dependent runtime set before frame code resumes.
- Running a full build was rejected because CPU was `100%`, `dotnet.exe` PID `41344` was active, and the user assigned current compile-wall repair to another agent.

## Proof

- `PreSimulationTick()` calls `TryResolveRuntimeBuffers()`.
- `TryResolveRuntimeBuffers()` now only checks existing handles and calls `vault.TryResolveHandle(...)`.
- `OpenOrAcquireRuntimeBuffers()` is the only Homeostasis runtime-buffer route in this file that reaches `TryResolveOrAcquire()` and `EnsureGenerationHandle<T>()`.
- `OpenOrAcquireRuntimeBuffers()` active references: `3` (`InitializeRuntime`, cold rebind reopen, method declaration).
- `TryResolveRuntimeBuffers()` active references: `4` (`PreSimulationTick`, two ScalabilityDictator read/dump fallback call sites, method declaration).
- `TryResolveHardwareMetrics` active references: `0`.
- Touched source brace delta: `0`.
- Scoped `git diff --check` exited `0`; line-ending warning only.

## Residuals

- `HomeostasisBrain.ScalabilityDictator.cs` remains dirty from another agent and still has other private `Ensure*` helpers. They were not edited in this pass to avoid interfering.
- Full solution build was not run.
- No Unity Editor import, Play Mode, profiler, GCMonitor, player build, device run, or DataVault hot-swap runtime test was performed.
- Runtime microseconds saved claimed: `0`. This is frame-phase ownership/lifecycle correctness, not a measured speedup.
