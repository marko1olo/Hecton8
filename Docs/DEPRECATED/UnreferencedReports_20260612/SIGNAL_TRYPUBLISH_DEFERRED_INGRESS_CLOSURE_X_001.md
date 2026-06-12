# SIGNAL_TRYPUBLISH_DEFERRED_INGRESS_CLOSURE_X_001

Agent: X_001  
Domain: Echelon 1 Core Infrastructure / Typed SignalBus Corridor  
Date: 2026-05-25  
Evidence class: SOURCE_STATIC + CLI_COMPILE

## Scope

Patched 24 runtime files to remove selected residual `void Raise/Publish/Notify` ingress surfaces beside the typed signal corridor. The pass did not add new global authority. It made bounded refusal visible at producer edges and moved MapMagic terrain-tile callbacks from immediate listener execution to a deferred fixed-slot native lane.

## Changed Lanes

| Lane | Capacity | Overflow policy | Managed payload status |
|---|---:|---|---|
| `PerformanceEvents` | 16 | Drop newest, increment `DroppedEventCount` | `PerformanceEventPayload` is 32-byte explicit unmanaged |
| `LocalizationEvents` | 128 | Drop newest, increment `DroppedEventCount`, one telemetry warning while saturated | `LocalizationEventPayload` is 16-byte explicit unmanaged |
| `MapMagicBiomeEvents` | 8 | Drop newest, increment `DroppedCount` | Native `int` biome id only |
| `MapMagicTerrainTileEvents` | 16 queue / 16 sidecar | Drop newest or refuse snapshot slot, increment drop counters | Native 8-byte payload holds event type + fixed snapshot slot; managed `Terrain`/provider refs stay in fixed sidecar only until dispatch |
| `ModuleStatusEvents` | 128 queue / 128 sidecar | Drop newest and release sidecar; slot exhaustion increments `DroppedReferenceSlotCount` | `ModuleStatusEventPayload` is 64-byte explicit unmanaged |
| `PlayerExpressionEvents` | 8 queue / 8 sidecar | Drop newest and release sidecar; slot exhaustion increments `DroppedReferenceSlotCount` | `PlayerExpressionEventPayload` is now 8-byte explicit unmanaged |
| `ObjectPoolDiagnostics` | 4 | `TryPublishDataBusDepth` returns false when saturated | `PoolDiagnosticsEventPayload` is 16-byte explicit unmanaged |
| `FluidFeedbackEvents` | `SplashEvent` SignalBus 64 / low-tier 32 | `SignalBus<SplashEvent>.TryPush` refusal returned to producer | `SplashEvent` extracted Core signal payload remains unmanaged |
| `TetherSignals` | tension 128, snap 64, fire 16 / low-tier fire 8 | `SignalBus<T>.TryPush` refusal returned to producer | Tether DTOs remain unmanaged Core contracts |
| `WorldGenerativeGeologyTelemetry` | `GlobalTelemetryBus` ring 1024 | Rate-gated telemetry returns false when suppressed | Hash/scalar telemetry only |
| `HectonUnderwaterVisuals` HUD luminance | Owner-local scalar | `TryPublishHudAverageLuminance` returns false when no active owner exists | No event DTO; direct owner scalar write |

## Files Touched

- `Assets/_Project/Scripts/PerformanceMonitor.cs`
- `Assets/_Project/Scripts/Core/FrameTimeWatchdog.cs`
- `Assets/_Project/Scripts/Core/RuntimeWatchdog.cs`
- `Assets/_Project/Scripts/LocalizationEvents.cs`
- `Assets/_Project/Scripts/LocalizationManager.cs`
- `Assets/_Project/Scripts/MapMagicBridge.cs`
- `Assets/_Project/Scripts/Plugins/MapMagic/MapMagicRuntimeBridge.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs`
- `Assets/_Project/Scripts/ObjectPoolDiagnostics.cs`
- `Assets/_Project/Scripts/ModuleStatusEvents.cs`
- `Assets/_Project/Scripts/BaseModule.cs`
- `Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs`
- `Assets/_Project/Scripts/Physics/FluidFeedbackListener.cs`
- `Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs`
- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/Physics/TetherSignals.cs`
- `Assets/_Project/Scripts/TetherInstance.cs`
- `Assets/_Project/Scripts/Gameplay/HeavyTowWinch.cs`
- `Assets/_Project/Scripts/World/WorldGenerativeGeologyTelemetry.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyVoxelBridgeDirector.cs`
- `Assets/_Project/Scripts/WorldGenerativeGeologyTerrainSeamApplier.cs`
- `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs`
- `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs`

## Verification

- Selected old producer calls: `0`
- External runtime `GlobalSignals.Publish/Push/TryDequeue/*Writer`: `0`
- `SignalBus<T>.Push` source hits: `0`
- First-party `HectonEventBus.Publish/Subscribe/Unsubscribe` outside ModdingAPI/Editor/Tests: `0`
- Core signal payload banned field scan: `0`
- `git diff --check`: no errors; LF-to-CRLF warnings only
- `dotnet restore Hecton8.Editor.csproj -m:1 /nr:false`: pass
- `dotnet build Hecton8.Editor.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors, 63.15s

## Runtime Claim Boundary

No Unity Play Mode, profiler, GCMonitor, Memory Profiler, or device capture was run. Runtime microsecond savings remain `0us verified`. Static effect is visible producer-side refusal, fixed native capacity, fixed managed sidecars where scene references are unavoidable, and removal of immediate MapMagic tile listener dispatch.
