# HECTON-8 Mod Signal Audit Matrix

Date: 2026-05-19
Status: ENVELOPE-ONLY STATIC SOURCE AUDIT / STATIC VALIDATOR PASSING / PENDING RUNTIME VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Owner prompt: MODDING_API_SCHEMA_BUILDER  
Source file: `Assets/_Project/Scripts/Core/GlobalSignals.cs`  
Companion schema: `Docs/Modding/Signal_Schema.json`

## 2026-05-19 Envelope-Only Override

The projection rows below are historical/source-audit context. Current public UGC runtime does not subscribe to first-party `SignalBus<T>` lanes through managed callbacks. If the SDK needs event-like behavior, it should provide authoring-time graph triggers, sampled/redacted fixtures, or future engine-owned unmanaged projections that still resolve to bounded envelope behavior. No `SignalBus<T>` snapshot is exposed to mods.

## Extraction Evidence

Command used:

```powershell
rg -o "public struct [A-Za-z0-9_]+ : ISignal" Assets/_Project/Scripts/Core/GlobalSignals.cs
```

Result: 162 unique `ISignal` structs in `GlobalSignals.cs`.

Projection bridge source check: `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs` consumes only `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>` for `ModEventDto` projection.

## Mod Visibility Rule

Only signals listed in `Signal_Schema.json.allowedSignalBuses` are public to mods. Every other `ISignal` struct is internal first-party infrastructure and denied by default, even if the struct is public in C# source.

| Source signal | Mod surface | Status | Reason |
|---|---|---|---|
| `CombatDamageSignal` | `ModEventDto` kind `CombatDamage` | ALLOWED_READ_ONLY_PROJECTION | Projected by `ModEventProjectionBridge`; no Unity objects or mutable damage receiver references exposed. |
| `WeatherChangedSignal` | `ModEventDto` kind `WeatherChanged` | ALLOWED_READ_ONLY_PROJECTION | Projected by `ModEventProjectionBridge`; no weather authority mutation exposed. |

## Denied-By-Default Inventory

The following 160 current `ISignal` structs are not public mod subscriptions. Any future exposure requires schema update, projection/copy wrapper, cap, telemetry, finite guards, runtime profiling, and Integrator approval.

```text
AcousticPingSignal
AnomalyProximitySignal
AnomalySignal
AtmosphericReentrySignal
AudioEvent
AupPreShiftSignal
AupShiftSignal
BaseModuleCompromisedSignal
BatteryLevelSignal
BiomeChangedSignal
BiomeGradientSignal
BlueprintUnlockedSignal
BrownoutSignal
BubbleSpawnSignal
CameraFrustumSignal
CameraJuiceImpactSignal
CameraPositionSignal
ChunkDehydratedSignal
CombatDamageSignalAupShiftTransformer
CompassCalibratedSignal
ComplianceViolationSignal
ControlSignal
CpuStarvationSignal
CraftingCompletedSignal
CraftingStartedSignal
CrashTelemetrySignal
CrushWarningSignal
DataReloadSignal
DataVaultUpdateSignal
DebrisSpawnSignal
DebugSignal
DeconstructRequestSignal
DeconstructResultSignal
DeferredSubmarineImpactSignal
DeflectSignal
DesyncDetectedSignal
DiegeticHudSignal
DockingCompleteSignal
DockingFailedSignal
DockingRequestSignal
DropPodLandedSignal
EntityDeathSignal
EntitySpawnSignal
FaunaStateChangedSignal
FluidDensityChangedSignal
FluidImpulseSignal
FluidIncursionSignal
FocusBrokenSignal
FramePacingWarningSignal
GlobalWorldStateSignal
HabitatConstructionSignal
HabitatFloodAcousticMuffleSignal
HapticRequest
HighSpeedImpactSignal
HUDNotificationSignal
HullDeformedSignal
HullRepairedSignal
HypoxiaSignal
ImpactSignal
InputSignal
InputStateSignal
InteractionUiSignal
InventoryChangedSignal
InventoryCommandSignal
ItemAcquiredSignal
ItemDecaySignal
ItemDurabilityChangedSignal
KccVelocitySignal
LaserCutterEventPayload
LightLevelSignal
LockstepSnapshotSignal
LoreFragmentScannedSignal
MacroDatabaseSectorHydrationSignal
ManualOverridePulledSignal
MemoryAddressShiftSignal
MemoryPressureSignal
MixerStateSignal
ModuleDeconstructSignal
MovementAcousticSignal
NarrativeFocusSignal
NarrativeHudWaypointSignal
NarrativePoiStateSignal
OxygenCriticalSignal
PdaExchangeStateChangedSignal
PhysicsEventPayload
PhysiologyStateSignal
PipeRuptureSignal
PlayerActionCancelledSignal
PlayerActionCompletedSignal
PlayerActionProgressSignal
PlayerBaseEnterSignal
PlayerBaseExitSignal
PlayerInputSignal
PlayerLookTargetSignal
PlayerStateSignal
PlayerStressSignal
PowerDrainSignal
PrefabAcousticSignatureSignal
PrefabLoreLinkSignal
ProgressionEventSignal
PrologueCompleteSignal
RadiationDoseSignal
RadiationSourceSignal
RebaseSignal
ReconDataSignal
ReentryVfxStateSignal
ResolutionChangedSignal
ResourceDepletionDeltaSignal
RigidbodySleepSignal
SaveLifecycleSignal
SaveMetadataReadySignal
ScanCompleteSignal
ScanLogChangedSignal
ScannerToolActiveSignal
SectorDehydratedSignal
SectorResidencyHydratedSignal
SeismicSignal
SimulationBucketSyncSignal
SolarFlareSignal
SonarPingSignal
SoundscapeProfileSignal
SpectrumScanSignal
SplashEvent
StateCorrectionSignal
StorageDebtSignal
StreamingTurbulenceSignal
SubmarineFloodStateSignal
SubmarineLightsChangedSignal
SubtitleSignal
SurvivalVitalsChangedSignal
SwarmDispersedSignal
SyncFenceSignal
SystemGlitchSignal
SystemHealthIndexSignal
SystemHealthSignal
SystemPauseSignal
TelemetryAnomalySignal
TemperatureChangedSignal
TetherFiredSignal
TetherSnappedSignal
TetherTensionSignal
ThermalSourceSignal
ThermalStateChangedSignal
ToolAcousticSignal
ToolLoadoutChangedSignal
ToolStateChangedSignal
ToolTriggerSignal
TraumaSignal
UIRescaleRequestSignal
VehicleUpgradesChangedSignal
VisorDropletSignal
VisualFlareSignal
VitalWarningSignal
VocalWarningSignal
VoxelCarveEvent
WakeGeneratedSignal
WeatherStrengthSignal
WfcOutpostDoorPowerSignal
WfcOutpostGeneratedSignal
WfcOutpostStateChangedSignal
```

## High-Risk Groups

| Group | Signals | Crash/corruption risk | Required wrapper |
|---|---|---|---|
| AUP/origin shift | `AupPreShiftSignal`, `AupShiftSignal`, `RebaseSignal`, `MemoryAddressShiftSignal` | Stale coordinate handles, bad rebases, native buffer desync. | Read-only rebased DTO or command response only. |
| DataVault/streaming/save | `MemoryPressureSignal`, `StorageDebtSignal`, `SectorHydratedSignal`, `SectorResidencyHydratedSignal`, `SectorDehydratedSignal`, `ChunkDehydratedSignal`, `MacroDatabaseSectorHydrationSignal`, `ScanLogChangedSignal`, `SaveRequestSignal`, `SaveCompletedSignal`, `SaveLifecycleSignal`, `SaveStatusSignal`, `SaveMetadataReadySignal`, WFC signals | Can expose lifecycle state that must stay engine-owned. | Redacted hashes/status only; no file offsets, handles, mutable sector ids, or save authority. |
| Player/survival/input | `InputStateSignal`, `PlayerInputSignal`, `PlayerLookTargetSignal`, `PlayerStateSignal`, `PhysiologyStateSignal`, `SurvivalVitalsChangedSignal`, `HypoxiaSignal`, `OxygenCriticalSignal`, `PlayerStressSignal`, `PlayerBaseEnterSignal`, `PlayerBaseExitSignal`, `ToolLoadoutChangedSignal`, `InventoryCommandSignal`, `InventoryChangedSignal` | Input spoofing, inventory duplication, survival corruption. | Read-only redacted DTO plus validated engine-owned command kernels. |
| High-volume sim/render | `WakeGeneratedSignal`, `FluidImpulseSignal`, `FluidIncursionSignal`, `RigidbodySleepSignal`, `CameraPositionSignal`, `CameraFrustumSignal`, `CullingOverloadSignal`, `FaunaStateChangedSignal` | Callback storm and presentation-state leakage. | Sampled projection with `GlobalQualityWeight`-derived caps and overflow telemetry. |
| UI/audio/presentation | `HUDNotificationSignal`, `SubtitleSignal`, `VocalWarningSignal`, `HapticRequest`, `SoundscapeProfileSignal`, `MixerStateSignal`, `SubmarineLightsChangedSignal`, `ThermalSourceSignal` | Mods could fake authoritative status, environmental hazard sources, or flood managed presentation callbacks. | Presentation-only facade APIs; no direct first-party signal subscription. |

## Consistency Gate

If the source inventory count changes from 162, the mod signal schema and this audit must be updated before the mod API can be marked runtime verified. A new `SignalBus<T>` exposure is not valid until `Signal_Schema.json.allowedSignalBuses`, `Mod_API_Specification.md`, and this audit matrix all name it explicitly.

Run the static drift gate after any signal or mod bridge edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
```
