# GLOBAL REGISTRY DEPENDENCY GRAPH

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: source-backed dependency orientation for core runtime services
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`, `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## 1. Purpose

This file is a narrowed dependency orientation page.
It does not claim exhaustive compile-time graph completeness and does not claim runtime initialization was measured in Unity during this pass.

## 2. Verified Core Dependency Surface

Current code review confirmed these core service owners/interfaces in the active registry-contract layer:

- `InputDispatcher` -> `IInputService`
- `PhysicsApplySystem` -> `IPhysicsService`
- `SceneRuntimeService` -> `ISceneService`
- `SaveManager` -> `ISaveService`
- `PlayerRuntimeContextService` -> `IPlayerRuntimeContext`
- `PlayerInventoryManager` -> `IPlayerInventoryService`
- `PlayerSensoryManager` -> `IPlayerSensoryService`
- `EnvironmentRuntimeContextService` -> `IEnvironmentRuntimeContext`
- `GlobalWeatherDirector` -> `IWeatherService`
- `OceanKinematicsRuntimeService` -> `IHectonOceanKinematicsService`
- `EquipmentInteractionHandler` -> `IInteractionSignalService`
- `DebrisManager` -> `IDebrisService`
- `EcosystemDirector` -> `IEcosystemDirectorService`

Unresolved service gap still confirmed:

- `IAudioService` -> no verified implementor in current pass

## 3. Structural Interpretation

Observed dependency style is mixed, not pure:

- `GlobalRegistry` / service-locator access is present
- static event buses are present
- feature-local static buses are present
- direct component/service references are also present

This means the project is not a single clean DI graph. It is a layered runtime with several competing communication styles.

## 4. High-Risk Dependency Areas

### 4.1 Audio

- `SpatialAudioManager` exists
- `IAudioService` ownership remains unresolved
- audio runtime is therefore structurally weaker than other service surfaces

### 4.2 UI

- `IUIService` is fragmented across multiple implementors
- UI behavior also relies heavily on feature-local event surfaces such as `PDAEvents`

### 4.3 Damage / Integrity

- `IDamageReceiver` remains semantically conflicting because a shadow/nested definition was previously documented and code context remains high-risk

## 5. Dependency Narrative

Representative architectural path inferred from current code:

1. bootstrap/runtime setup establishes core services
2. feature systems query `GlobalRegistry` or subscribe to static buses
3. player/world/UI systems exchange state through mixed direct calls and event surfaces
4. mod-facing signals can also pass through `HectonEventBus`

This is sufficient for orientation, not for claiming deterministic or leak-free startup.

## 6. What Was Removed

Removed from older versions:

- `ETA VERIFIED` status language
- unsupported certainty about full graph completeness
- stale dependency claims not rechecked in the current audit pass

## 7. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: no runtime sequencing changed
Correctness: improved by replacing stale certainty with source-backed dependency orientation

## 8. Hot Path Impact

None. Markdown-only change.

## 9. Failure Modes

- hidden scene wiring may bypass the dependency picture described here
- registry ownership can drift if code changes without paired doc maintenance
- compile-time relationships outside the rechecked core surfaces are not fully enumerated here

## 10. Why This Version Was Kept

Kept because it is narrower, readable, and backed by current source inspection.
Rejected content: unsupported verification language and stale graph claims.

STATUS: PENDING VERIFICATION
