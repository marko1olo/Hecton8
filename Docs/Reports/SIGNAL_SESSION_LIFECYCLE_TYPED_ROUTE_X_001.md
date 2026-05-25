# SIGNAL_SESSION_LIFECYCLE_TYPED_ROUTE_X_001

Agent: X_001
Date: 2026-05-24

## Scope

Cut the last first-party `GameLoadedEvent` / `PlayerSpawnedEvent` consumers from managed `HectonEventBus` and keep managed lifecycle events inside `ModdingAPI` only.

## What Changed

- Added `SessionLifecycleSignal`, a 64-byte unmanaged DTO with `Kind`, `Sequence`, `Frame`, `SlotHash`, `PlayerEntityId`, and `PlayerPosition`.
- Added `SessionLifecycleSignalRoute` as the only first-party producer for load/spawn lifecycle notifications.
- Configured `SignalBus<SessionLifecycleSignal>` with capacity 16, max-frame 16, low-tier frame cap 8, direct flush/clear wiring, direct-lane registration, finite guard, and lane contract id 134.
- `ModLoader` now publishes typed lifecycle signals before the mod-only envelope gate. Managed `GameLoadedEvent` / `PlayerSpawnedEvent` publishes remain behind the mod/API gate.
- Rewired `RunModifierController`, `GlobalProfileManager`, `DynamicDifficultyDirector`, `PlayerAchievementRegistry`, `PDALogbookManager`, `PDAContextualAdvisorySystem`, and `HectonOSBootManager` to consume `SignalBus<SessionLifecycleSignal>` snapshots with local sequence cursors.
- Removed the final non-modding unmanaged `HectonEventBus.Publish(in InventoryPhysicalDropRequestPayload)` in `PlayerInventory`; the real first-party discard route is already `ItemLifecycleSignalRoute.PublishDiscarded`.

## Capacity And Overflow

- Capacity: 16 native entries.
- Max frame signals: 16.
- Low-tier frame cap: 8.
- Overflow strategy: deterministic `SignalBus<T>` native shedding/drop policy, no managed queue growth.
- Coalescing: none. Lifecycle events are sparse control-plane facts; duplicate same-frame spam is bounded by the 16/8 caps and sequence-ordered snapshot drain.
- Managed allocation: DTO has no `GameObject`, `Transform`, `string`, `FixedString`, or native container fields. `SlotHash` is FNV-derived at the route boundary.

## Proof

- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside `ModdingAPI`, `Editor`, and `Tests`: 0 hits.
- `GameLoadedEvent` / `PlayerSpawnedEvent` outside `ModdingAPI`: 0 hits.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` outside Editor/Tests: 0 hits.
- Core signal DTO banned-field scan: 0 hits.
- Touched-file brace balance: 0 deltas.
- `git diff --check`: LF/CRLF warnings only.

## Build Status

`dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false -v:minimal -clp:ErrorsOnly` was run twice under the CPU/process guard.

- First attempt exposed two local fallout errors: missing `Hecton8.Core.Contracts.Signals` import in `RunModifierController` and missing `RefreshDiscoveryBindingCold` in `PlayerAchievementRegistry`.
- Both were fixed.
- Second attempt has no X_001 session-route errors. It still fails on 14 unrelated existing compile walls in `MainMenuController`, `HectonDirectorAI`, `ModSettingsRegistry`, `GameBootstrapper`, and `MesofaunaBehavioralStateMachine`.

Runtime profiler/GCMonitor was not run; no measured microsecond saving is claimed.
