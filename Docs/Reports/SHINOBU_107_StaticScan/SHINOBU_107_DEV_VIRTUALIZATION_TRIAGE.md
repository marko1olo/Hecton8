# SHINOBU_107 Dev Virtualization Triage

Source report: `Docs/Reports/SHINOBU_107_StaticScan/SHINOBU_140_Dev_Virtualization.json`

## Summary

- Critical findings: `0`
- Warning findings: `24`
- Scanner rule: `INTERFACE_CONTAINER_DEVIRTUALIZATION_RISK`
- Patch decision: no C# source patch in this loop.

Reason: every finding is severity `1`, not a Burst/job critical. None of the 24 findings are `NativeArray<IMyInterface>`, `NativeList<IMyInterface>`, Burst job fields, or `IJob` input containers. Replacing them with wrapper structs would silence the scanner while keeping managed virtual dispatch, which is not a real devirtualization fix.

## Finding Buckets

| Bucket | Count | Files | Classification |
| --- | ---: | --- | --- |
| Power graph managed component surface | 13 | `PowerGrid.cs`, `PowerNode.cs` | Real owner-domain migration debt. The grid still traverses `IPowerComponent` at slow logistics cadence. Correct fix is a Power-owned scalar snapshot/callback route, not a SHINOBU_107 wrapper. |
| Managed damage listener caches | 4 | `CombatDamageRuntime.cs`, `HabitatIntegrityManager.cs`, `MantaScooter.cs`, `MountablePlayerTransport.cs` | Fixed-capacity managed callback fanout. Not Burst input. Event ownership remains callback-based per damage mandate. |
| Transport docking/charging caches | 2 | `VehicleDockingModule.cs`, `TransportChargingStation.cs` | Trigger/lifecycle owner caches. Not a Burst or per-entity job container. |
| Mod/API cold managed isolation | 2 | `HectonEventBus.cs`, `ModCommandDispatcher.cs` | Legacy/cold managed mod surface. Global doctrine explicitly isolates `HectonEventBus` as mod/API/cold. |
| Tool/save/pool registries | 3 | `LaserCutter.cs`, `ObjectPoolManager.cs`, `SaveManager.cs` | Fixed-capacity or preallocated lifecycle registries. No Burst vectorization surface. |

## Exact Rows

- `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs:154`
- `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs:333`
- `Assets/_Project/Scripts/Gameplay/HabitatIntegrityManager.cs:182`
- `Assets/_Project/Scripts/Gameplay/MantaScooter.cs:247`
- `Assets/_Project/Scripts/Gameplay/MountablePlayerTransport.cs:259`
- `Assets/_Project/Scripts/Gameplay/TransportChargingStation.cs:52`
- `Assets/_Project/Scripts/LaserCutter.cs:400`
- `Assets/_Project/Scripts/ModdingAPI/HectonEventBus.cs:121`
- `Assets/_Project/Scripts/ModdingAPI/ModCommandDispatcher.cs:266`
- `Assets/_Project/Scripts/ObjectPoolManager.cs:35`
- `Assets/_Project/Scripts/PowerGrid.cs:167`
- `Assets/_Project/Scripts/PowerGrid.cs:298`
- `Assets/_Project/Scripts/PowerGrid.cs:717`
- `Assets/_Project/Scripts/PowerGrid.cs:762`
- `Assets/_Project/Scripts/PowerGrid.cs:1242`
- `Assets/_Project/Scripts/PowerGrid.cs:1397`
- `Assets/_Project/Scripts/PowerGrid.cs:1423`
- `Assets/_Project/Scripts/PowerGrid.cs:1468`
- `Assets/_Project/Scripts/PowerGrid.cs:1502`
- `Assets/_Project/Scripts/PowerNode.cs:85`
- `Assets/_Project/Scripts/PowerNode.cs:120`
- `Assets/_Project/Scripts/PowerNode.cs:227`
- `Assets/_Project/Scripts/PowerNode.cs:254`
- `Assets/_Project/Scripts/SaveManager.cs:141`

## Required Owner Follow-Up

Power owner should replace the `PowerNode.Components` / `PowerGrid._consumerRefs` `IPowerComponent` traversal with a Power-owned runtime snapshot:

- `PowerConsumerRuntimeRecord`: scalar demand/generation, priority, flags, stable node id.
- owner-owned callback bridge for status changes, invoked only after logistics solve.
- CSR/native solve remains scalar-only; managed callbacks stay outside Burst jobs.

Expected effect: Power slow-tick evaluation stops repeatedly reading interface properties inside graph assembly. This is an `O(N * C)` managed virtual call surface today, where `N` is power nodes and `C` is components per node. The target route is `O(N + C)` scalar copy during topology invalidation plus `O(C_changed)` callback fanout after solve.

## Rejected Changes

- Rejected replacing interface arrays/lists with wrapper structs containing the same interface reference. That hides the warning but keeps virtual dispatch.
- Rejected converting event listener caches to concrete receiver arrays. Receiver ownership crosses gameplay, HUD, damage, transport, and mod surfaces.
- Rejected editing `HectonEventBus` into native first-party signal lanes. Doctrine keeps `HectonEventBus` as cold managed mod/API isolation; first-party hot routes use `SignalBus<T>`.
- Rejected Power-domain surgery under SHINOBU_107 without a Power route card and owner migration proof.
