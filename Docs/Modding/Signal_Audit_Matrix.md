# HECTON-8 Mod Signal Audit Matrix

Status: STATIC SOURCE AUDIT / PENDING RUNTIME VERIFICATION  
Owner prompt: MODDING_API_SCHEMA_BUILDER  
Source file: `Assets/_Project/Scripts/Core/GlobalSignals.cs`  
Companion schema: `Docs/Modding/Signal_Schema.json`

## Extraction Evidence

Command used:

```powershell
rg -o "public struct [A-Za-z0-9_]+ : ISignal" Assets/_Project/Scripts/Core/GlobalSignals.cs
```

Result: 129 unique `ISignal` structs in `GlobalSignals.cs`.

Projection bridge source check: `Assets/_Project/Scripts/ModdingAPI/ModEventProjectionBridge.cs` consumes only `SignalBus<CombatDamageSignal>` and `SignalBus<WeatherChangedSignal>` for `ModEventDto` projection.

## Mod Visibility Rule

Only signals listed in `Signal_Schema.json.allowedSignalBuses` are public to mods. Every other `ISignal` struct is internal first-party infrastructure and denied by default, even if the struct is public in C# source.

| Source signal | Mod surface | Status | Reason |
|---|---|---|---|
| `CombatDamageSignal` | `ModEventDto` kind `CombatDamage` | ALLOWED_READ_ONLY_PROJECTION | Projected by `ModEventProjectionBridge`; no Unity objects or mutable damage receiver references exposed. |
| `WeatherChangedSignal` | `ModEventDto` kind `WeatherChanged` | ALLOWED_READ_ONLY_PROJECTION | Projected by `ModEventProjectionBridge`; no weather authority mutation exposed. |

## Denied-By-Default Inventory

The following 127 current `ISignal` structs are not public mod subscriptions. Any future exposure requires schema update, projection/copy wrapper, cap, telemetry, finite guards, runtime profiling, and Integrator approval.

```text
AcousticPingSignal
AnomalySignal
AtmosphericReentrySignal
AupPreShiftSignal
AupShiftSignal
BaseModuleCompromisedSignal
BatteryLevelSignal
BiomeChangedSignal
BiomeGradientSignal
BlueprintUnlockedSignal
BrownoutSignal
BulletTimeVisualSignal
CameraFrustumSignal
CameraPositionSignal
ChunkDehydratedSignal
CombatDamageSignalAupShiftTransformer
ComplianceViolationSignal
ControlSignal
CpuStarvationSignal
CraftingCompletedSignal
CraftingStartedSignal
CrashTelemetrySignal
CrushWarningSignal
CullingOverloadSignal
DamageSignal
DataReloadSignal
DebrisSpawnSignal
DeconstructRequestSignal
DeconstructResultSignal
DeflectSignal
DiegeticHudSignal
DropPodLandedSignal
EntityDeathSignal
FaunaStateChangedSignal
FluidDensityChangedSignal
FluidImpulseSignal
FluidIncursionSignal
FocusBrokenSignal
GlobalTimeSyncSignal
GlobalWorldStateSignal
HabitatConstructionSignal
HapticRequest
HighSpeedImpactSignal
HUDNotificationSignal
HullDeformedSignal
HypoxiaSignal
ImpactSignal
InputStateSignal
InteractionUiSignal
InventoryChangedSignal
InventoryCommandSignal
ItemAcquiredSignal
ItemDecaySignal
ItemDurabilityChangedSignal
LightLevelSignal
LoreFragmentScannedSignal
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
PhysiologyStateSignal
PipeRuptureSignal
PlayerActionCancelledSignal
PlayerActionCompletedSignal
PlayerActionProgressSignal
PlayerInputSignal
PlayerLookTargetSignal
PlayerStateSignal
PlayerStressSignal
PowerDrainSignal
ProgressionEventSignal
PrologueCompleteSignal
RadiationDoseSignal
RadiationSourceSignal
RebaseSignal
ReconDataSignal
ResolutionChangedSignal
ResourceDepletionDeltaSignal
RigidbodySleepSignal
SaveCompletedSignal
SaveLifecycleSignal
SaveMetadataReadySignal
SaveRequestSignal
SaveStatusSignal
ScanCompleteSignal
ScannerToolActiveSignal
SectorDehydratedSignal
SectorHydratedSignal
SectorResidencyHydratedSignal
SeismicSignal
SimulationPauseSignal
SolarFlareSignal
SonarPingSignal
SoundscapeProfileSignal
SpectrumScanSignal
StorageDebtSignal
StreamingTurbulenceSignal
SubmarineFloodStateSignal
SubmarineLightsChangedSignal
SubtitleSignal
SwarmDispersedSignal
SystemHealthIndexSignal
SystemPauseSignal
TelemetryAnomalySignal
TemperatureChangedSignal
ThermalStateChangedSignal
TimeDilationSignal
ToolAcousticSignal
ToolStateChangedSignal
ToolTriggerSignal
TraumaSignal
UIRescaleRequestSignal
VehicleUpgradesChangedSignal
VitalWarningSignal
VocalWarningSignal
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
| DataVault/streaming/save | `MemoryPressureSignal`, `StorageDebtSignal`, `SectorHydratedSignal`, `SectorResidencyHydratedSignal`, `SectorDehydratedSignal`, `ChunkDehydratedSignal`, `SaveRequestSignal`, `SaveCompletedSignal`, `SaveLifecycleSignal`, `SaveStatusSignal`, `SaveMetadataReadySignal`, WFC signals | Can expose lifecycle state that must stay engine-owned. | Redacted hashes/status only; no file offsets, handles, mutable sector ids, or save authority. |
| Player/survival/input | `InputStateSignal`, `PlayerInputSignal`, `PlayerLookTargetSignal`, `PlayerStateSignal`, `PhysiologyStateSignal`, `HypoxiaSignal`, `OxygenCriticalSignal`, `PlayerStressSignal`, `InventoryCommandSignal`, `InventoryChangedSignal` | Input spoofing, inventory duplication, survival corruption. | Read-only redacted DTO plus validated engine-owned command kernels. |
| High-volume sim/render | `WakeGeneratedSignal`, `FluidImpulseSignal`, `FluidIncursionSignal`, `RigidbodySleepSignal`, `CameraPositionSignal`, `CameraFrustumSignal`, `CullingOverloadSignal`, `FaunaStateChangedSignal` | Callback storm and presentation-state leakage. | Sampled projection with low/high caps and overflow telemetry. |
| UI/audio/presentation | `HUDNotificationSignal`, `SubtitleSignal`, `VocalWarningSignal`, `HapticRequest`, `SoundscapeProfileSignal`, `MixerStateSignal`, `SubmarineLightsChangedSignal` | Mods could fake authoritative status or flood managed presentation callbacks. | Presentation-only facade APIs; no direct first-party signal subscription. |

## Consistency Gate

If the source inventory count changes from 129, the mod signal schema and this audit must be updated before the mod API can be marked runtime verified. A new `SignalBus<T>` exposure is not valid until `Signal_Schema.json.allowedSignalBuses`, `Mod_API_Specification.md`, and this audit matrix all name it explicitly.

Run the static drift gate after any signal or mod bridge edit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Docs/Modding/Validate_Mod_API_Static.ps1
```
