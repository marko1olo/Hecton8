# System Interconnect Matrix

Date: 2026-05-24
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE
Owner domain: architecture/system interconnect documentation
Review disposition: YELLOW / STATIC_DOC_ONLY until compile/import/runtime/profiler/player proof exists.

Full historical matrix snapshot: `../_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/ARCHITECTURE_APEX_PRE_FILE_CAP_SYSTEM_INTERCONNECT_MATRIX.md`.

## Scope

Maps lane ownership and flush order. It is not profiler proof, route-card approval, Unity import proof, or runtime acceptance.

Authority links:

- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`

## Route Classification

| Route | Current use | Boundary |
|---|---|---|
| `SignalBus<T>` | default first-party hot/cross-domain broadcast | owner, phase, capacity, overflow, telemetry required |
| `GlobalSignals` | retained Core bridge/direct queues | retained lanes require explicit owner |
| `HectonEventBus` | mod/API/cold boundary | not first-party hot gameplay bus |
| `GlobalRegistry` | cold service identity/rebind notification | not state-change bus |
| `GlobalDataVault` | shared native state | requires `BufferID`, `SystemID`, generation, lifetime, stale-handle behavior |

## Modding Boundary

- `HectonEventBus.cs`: blittable-only mod event bridge.
- `ModCommandDispatcher.cs`: sandboxed command queues, AUP rebasing, raycast proxy requests, render matrix injection, command flood throttling, spawn arbitration, heap-quota eviction.
- Mod persistent payloads: protected 16 KB indexed sectors in `SaveBinaryStorage`.
- Legacy `SaveData.CustomModData`: fallback index only.

## Static Flush Order

Source: `Assets/_Project/Scripts/Core/SystemDispatcher.cs`.

1. `CompleteDispatcherRaycasts()`
2. `ILateFrameTickable.LateFrameTick()`
3. `ThreadSafeCommandQueue.DrainToMainThread()`
4. `ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents()`
5. `ModCommandDispatcher.DrainLateFrame()`
6. mod/bootstrap/localization/narrative/interaction/crafting/scan/save/quest flush surfaces

## Active Route Notes

| Route | Owner | Key facts |
|---|---|---|
| SHINOBU_347 Day-Night GI Relay | `HectonGIRelaySystem` | `VISUAL_SYNC`; reads celestial Vault state, player AUP, biome gradients, `GlobalQualityWeight`; output excluded from rollback/Merkle/save truth |
| SHINOBU_353 Haptic Synthesis | `InputDispatcher` PAL boundary | consumes typed unmanaged signals; emits narrow `HapticPulseSignal`; telemetry/dump required |
| SHINOBU_354 Procedural Camera Shake | camera presentation owner | presentation only; trauma input bounded; no gameplay authority |

## Legacy Buckets

| Bucket | Scope |
|---|---|
| Core | bootstrap, registry, save/load, localization, telemetry, performance, mod registry |
| Env | weather, atmosphere, biome, celestial, acoustic, physics, fluid, pressure, soundscape |
| Player | interaction, crafting, scan, PDA, inventory, notifications |
| Base | modules, airlocks, integrity, submarine OS, power grid, drone telemetry |
| AI | director, quest/progression, narrative, ecosystem/fauna pressure |

Legacy buckets are navigation only. They are not route approval.

## Acceptance Gate

A route is `YELLOW` until the same section names owner, producer phase, consumer phase, cadence, capacity, overflow, shutdown, telemetry, and proof artifact tuple.
