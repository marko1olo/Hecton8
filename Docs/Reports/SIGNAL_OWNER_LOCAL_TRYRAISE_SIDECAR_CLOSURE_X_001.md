# SIGNAL OWNER-LOCAL TRYRAISE AND SIDECAR CLOSURE - X_001

Date: 2026-05-24
Status: SOURCE_ONLY / PENDING UNITY RUNTIME VERIFICATION

## Scope

This pass targeted remaining owner-local event lanes that still exposed `void Raise*` producers or managed sidecars near the typed signal corridor. The work did not create new global lanes or move ownership into Core.

Touched runtime files: 22.

- `Assets/_Project/Scripts/Gameplay/EclipseGameplaySystem.cs`
- `Assets/_Project/Scripts/Gameplay/EndingSystem.cs`
- `Assets/_Project/Scripts/Gameplay/FirstHourDirector.cs`
- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs`
- `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`
- `Assets/_Project/Scripts/Gameplay/SuitMeshUpdateEvents.cs`
- `Assets/_Project/Scripts/Gameplay/SuitUpgradeManager.cs`
- `Assets/_Project/Scripts/Gameplay/ToolEffectEvents.cs`
- `Assets/_Project/Scripts/HectonVoxelVolume.cs`
- `Assets/_Project/Scripts/LaserCutter.cs`
- `Assets/_Project/Scripts/PDA/PDALogbookManager.cs`
- `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs`
- `Assets/_Project/Scripts/PDA/PlayerExplorationTracker.cs`
- `Assets/_Project/Scripts/PlayerFlashlight.cs`
- `Assets/_Project/Scripts/PlayerPDA.cs`
- `Assets/_Project/Scripts/Power/PowerGridTelemetryEvents.cs`
- `Assets/_Project/Scripts/PowerGridManager.cs`
- `Assets/_Project/Scripts/RepairTool.cs`
- `Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs`
- `Assets/_Project/Scripts/World/DepthZoneDirector.cs`
- `Assets/_Project/Scripts/World/EmergencyServiceRelay.cs`
- `Assets/_Project/Scripts/World/EmergencyServiceRelayEvents.cs`

## What Changed

- Added producer-visible `TryRaise*` / `TryRaise` APIs to the selected owner-local lanes.
- Marked the old selected `Raise*` wrappers `[Obsolete(..., true)]` so first-party source cannot silently use the void surface again.
- Updated all selected first-party call sites, including the missed `RandomEventEvents.RaiseSeismicShockwave(...)` call in `HectonVoxelVolume`.
- Replaced `DepthZoneEvents` managed `Dictionary<uint, DepthZoneProfile>` with `ProfileSlot[32]`.
- Replaced `EmergencyServiceRelayEvents` managed `Dictionary<ulong, EmergencyServiceRelay>` with `RelaySlot[32]`.

## Capacity And Overflow Ledger

| Lane | Capacity | Admission Result | Overflow Policy |
|---|---:|---|---|
| Eclipse gameplay | 16 | `TryRaise* == false` | Drop newest before native enqueue |
| Ending | 8 | `TryRaise* == false` | Drop newest and record existing overflow telemetry |
| First hour | 16 | `TryRaiseMilestone == false` | Drop newest and record existing overflow telemetry |
| Random event started | 16 | `TryRaiseStarted == false` | Drop newest before native enqueue |
| Random event ended | 16 | `TryRaiseEnded == false` | Drop newest before native enqueue |
| Random seismic shockwave | 8 | `TryRaiseSeismicShockwave == false` | Drop newest for random-event listeners; acoustic ping route remains independently bounded |
| Depth zone | 16 events, 32 profile sidecar slots | `TryRaiseZone* == false` | Drop newest if event queue or fixed profile sidecar is full |
| Emergency relay | 16 events, 32 relay sidecar slots | `TryRaiseRelayActivated == false` | Drop newest if event queue or fixed relay sidecar is full |
| Base integrity HUD | 8 | `TryRaise* == false` | Drop newest before native enqueue |
| Flashlight | 16 SignalBus entries, low-tier frame cap 4 | `TryRaise* == false` | `SignalBus<T>.TryPush` bounded refusal |
| Laser cutter | 16 SignalBus entries | `TryRaise* == false` | Drop newest before `SignalBus<T>.TryPush` |
| PDA | 32 events, 128 native dedup keys | `TryRaise* == false` | Drop newest before native enqueue or duplicate key refusal |
| Suit mesh update | 16 | `TryRaise == false` | Drop newest before native enqueue |
| Power grid telemetry | 8 | `TryRaise == false` | Drop newest before native enqueue |
| Submarine OS | 16 | `TryRaise* == false` | Drop newest before native enqueue |
| Tool effect | 16 listener slots | `TryRaiseEffectApplied == false` | Reject invalid/no-listener producer; retained immediate owner mutation path |

## Verification

- Selected old call-site scan: `EclipseGameplayEvents.Raise|EndingEvents.Raise|FirstHourEvents.Raise|RandomEventEvents.Raise|DepthZoneEvents.Raise|EmergencyServiceRelayEvents.Raise|BaseIntegrityEvents.Raise|ToolEffectEvents.Raise|LaserCutterEvents.Raise|FlashlightEvents.Raise|PDAEvents.Raise|SuitMeshUpdateEvents.Raise|PowerGridTelemetryEvents.Raise|HectonSubmarineOsEvents.Raise` returns 0 call sites outside obsolete wrapper declarations.
- Selected managed sidecar scan for `Dictionary`, `_profilesByHash`, and `_relaysByInstanceId` in `DepthZoneDirector.cs` and `EmergencyServiceRelayEvents.cs`: 0 hits.
- Runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer` outside Core/Signals, Editor, Tests: 0 observed hits.
- `SignalBus<T>.Push`: 0 hits.
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI, Editor, Tests: 0 observed hits.
- `ThreadSafeCommandQueue.Enqueue`: 0 hits.
- Core signal DTO field scan for managed/string/native-container field declarations: 0 hits.
- Touched-file brace delta: 0.
- `git diff --check` on touched files: no errors, LF-to-CRLF warnings only.

## Build Status

Build was not launched. Latest guard reported CPU 37 percent but one active `dotnet` process (PID 42500), which violates the project rule forbidding `dotnet build` while another compiler/build process is running.

Runtime profiler, GCMonitor, Unity import, Play Mode, and player build were not run. Runtime zero-GC and microsecond claims remain unverified.
