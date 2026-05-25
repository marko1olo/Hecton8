# SIGNAL TRY SURFACE AND HASH SAVE CLOSURE X_001

Date: 2026-05-24

## Scope

Closed another batch of first-party hot-adjacent event facades that still used silent `Raise*`/`Publish*` naming or string/FixString payload ingress beside the typed `SignalBus<T>` corridor.

Runtime/editor files changed in this closure: 30 targeted files.

## Code Changes

- `SaveEventPayload` is now hash-only: `Type`, `TimestampTicks`, `SlotHash`, `MessageHash`, `MessageSlot`.
- `SaveEvents` now exposes `TryRaise*` methods that require precomputed slot/message hashes. Legacy string `Raise*` methods are `[Obsolete(..., true)]`.
- Save failure/load failure text is no longer copied into a queued DTO. Runtime keeps a fixed `MessageSlot[16]` sidecar and releases the slot after listener dispatch/drain.
- `BaseAirlockEvents` now exposes `TryRaise*` methods and returns `false` on null airlock, reference-slot exhaustion, or queue cap overflow.
- `PlayerSignalEvents`, `DirectorAIEvents`, `SpectrumEvents`, `SoundscapeEvents`, `CelestialEvents`, `BiomeMatrixEvents`, `AtmosphereEvents`, `AcousticZoneEvents`, and `PhysicsEvents` now expose explicit `Try*` producer/notify methods for the touched lanes.
- Old selected `Raise*`/`Publish*` wrappers are compile-time banned with `[Obsolete(..., true)]` and all first-party call sites in the touched surface were moved to `Try*`.

## Capacity And Overflow

| Lane | Capacity | Overflow strategy |
| --- | ---: | --- |
| `SaveEvents` | 16 front + 16 next-frame events, 16 message sidecar slots | `TryRaise*` returns `false`; message sidecar refusal does not enqueue DTO. |
| `BaseAirlockEvents` | 32 front + 32 next-frame events, 32 reference slots | `TryRaise*` returns `false`; reserved sidecar slot is released on queue overflow. |
| `PlayerSignalEvents` | 16 trauma, 16 interaction, 16 tool-depleted per front/next queue | `TryRaise*` returns `false`; no managed fallback. |
| `DirectorAIEvents` | 24 fixed array entries per front/next buffer | `TryRaise*` returns whether music `SignalBus` or listener event accepted the fact. |
| `SpectrumEvents` | 8 mode, 8 pulse, 24 ping, 8 snapshot, 8 echo, 16 ping-return per front/next queue | `TryRaise*` returns `false` at listener absence or queue cap. |
| `SoundscapeEvents` | 16 front + 16 next-frame events | `TryRaiseTierChanged` returns `false` at cap/listener absence. |
| `CelestialEvents` | 8 front + 8 next-frame events | Sun-angle and planet-phase coalesce by latest scalar; `TryRaise*` returns explicit status. |
| `BiomeMatrixEvents` | 32 front + 32 next-frame events, 128 profile sidecar slots | `TryRaise*` returns `false` at queue cap or profile slot exhaustion. |
| `AtmosphereEvents` | 8 front + 8 next-frame states | `TryRaiseStateChanged` returns `false` at queue cap/listener absence. |
| `AcousticZoneEvents` | typed `SignalBus<AcousticZoneChangedEvent>` and typed flood-muffle lane | `TryRaise*` returns `SignalBus<T>.TryPush` result. |
| `PhysicsEvents` impact listener fanout | 16 listeners, no queue | `TryNotifyImpact` returns `false` when there is no listener. |

## Verification

- Selected old call-site scan for `BaseAirlockEvents.Raise`, `SaveEvents.Raise`, `PlayerSignalEvents.Raise`, `DirectorAIEvents.Raise`, `SpectrumEvents.Raise`, `SoundscapeEvents.Raise`, `CelestialEvents.Raise`, `BiomeMatrixEvents.Raise`, `AtmosphereEvents.Raise`, `AcousticZoneEvents.Raise`, `PhysicsEvents.RaiseImpact`, plus previously closed Atlas/Narrative/Scan/Weather/Audio/Crafting/Quest/Interaction/bridge/determinism wrappers: 0 runtime hits outside wrapper declarations.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer/latest-helper` outside `Core/Signals`, Editor, and Tests: 0 hits.
- `SignalBus<T>.Push` source hits: 0.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: 0 hits.
- `ThreadSafeCommandQueue.Enqueue` first-party runtime callers: 0 hits.
- Core signal DTO banned-field scan over `Core/Signals` and `Core/Contracts/Signals`: 0 hits.
- `SaveEventPayload` no longer contains `FixedString64Bytes`, `FixedString128Bytes`, `string`, `GameObject`, or `Transform` fields.
- `git diff --check` on touched runtime/editor files: no errors, LF-to-CRLF warnings only.

## Build

Build was not launched in this closure. Guards reported CPU 52.9 percent with 0 compiler processes, then CPU 50.2 percent with eight active compiler processes (`dotnet` plus `VBCSCompiler`). Both states violate the project build guard.

Unity profiler and GCMonitor were not run; no runtime microsecond saving is claimed.
