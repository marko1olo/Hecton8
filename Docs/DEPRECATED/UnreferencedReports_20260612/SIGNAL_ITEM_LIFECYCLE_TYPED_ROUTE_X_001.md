# SIGNAL_ITEM_LIFECYCLE_TYPED_ROUTE_X_001

Date: 2026-05-24
Agent: X_001
Scope: first-party collected/recycled/discarded item lifecycle traffic.

## Finding

- First-party item lifecycle traffic still used managed `HectonEventBus` classes carrying `ItemData` references:
  - `HarvestableOutcrop` -> `ItemCollectedEvent`
  - `ScrapManager` / `ResourceRecyclerModule` -> `ItemRecycledEvent`
  - `PlayerInventory` -> `ItemDiscardedEvent`
- `EnvironmentalStrainManager` and `GlobalProfileManager` consumed those managed events for first-party state.

## Fix

- Added `ItemLifecycleSignal`, explicit 64-byte unmanaged DTO: item hash, category, resource family, flags, runtime position, frame, and sequence.
- Added `ItemLifecycleSignalRoute` to convert owner-local `ItemData` into hash/category/family/flag fields before publishing.
- Configured `SignalBus<ItemLifecycleSignal>` with capacity `128`, max frame `128`, low-tier frame cap `32`, direct flush/clear, and finite guard.
- Rewired first-party producers to `ItemLifecycleSignalRoute`.
- Rewired `EnvironmentalStrainManager` and `GlobalProfileManager` to read typed frame snapshots with per-consumer sequence cursors.
- Marked retired managed item event classes `[Obsolete(..., true)]`.

## Proof

- First-party item event publish/subscribe scan outside `ModdingAPI`: 0 hits.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` scan outside Editor/Tests: 0 hits.
- `ItemLifecycleSignal` DTO has no `GameObject`, `Transform`, `string`, `FixedString*`, or native container fields.
- Non-modding `HectonEventBus` traffic outside Editor/Tests/ModdingAPI dropped from 29 to 21 hits.
- Touched-file brace balance is 0.
- `git diff --check` reports only LF-to-CRLF warnings.

## Build

- Full Editor build is not claimed. A guarded build launched at CPU 43.64 percent with no active `dotnet/csc`, but exceeded the 120-second command timeout after `Hecton8.Core.dll` was emitted. Subsequent retry is blocked by CPU 100 percent and active external `dotnet` processes.
