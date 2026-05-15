# HECTON-8 WORLD / ENVIRONMENT / SUBMARINE SYSTEM MAP

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: source-backed map of current world, environment, ocean, ecology, debris, and submarine runtime owners
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

2026-05-01 trust note:

- Read `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` and `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md` before any older counter, root-path, or build-artifact claim. Then read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` as historical/domain context before using this map as current project truth.
- This file remains useful as a domain ownership map, but it is not a runtime verification report.
- Current unresolved risks for this domain include editor/Play Mode deadlock proof, local job completion ownership, scene/prefab wiring, and measured world/runtime performance.

## Purpose

The active docset already had player, construction, save/load, registry, and broad gameplay ledgers.

What still needed its own focused map was the non-player world-facing stack:

- environment context
- weather
- ocean kinematics
- thermal/vent simulation
- procedural scatter/world generation
- ecosystem simulation
- debris runtime
- submarine runtime

This file isolates that layer.

## Proof Boundary

This file is source-backed only.

Primary evidence came from:

- `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs`
- `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs`
- `Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs`
- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
- `Assets/_Project/Scripts/Gameplay/DebrisManager.cs`
- `Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs`
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs`

It does not prove:

- live underwater traversal correctness
- thermal/ocean fidelity in play mode
- ecosystem or submarine runtime performance under load

## 1. Environment Context Surface

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `EnvironmentRuntimeContextService` | `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs:12`, `209` | registry-facing environment runtime context |

Current meaning:

- this is the environment-side context publisher into `GlobalRegistry`
- it is the intended integration surface for other systems that need construction/module catalog/hazard access without scene scraping

## 2. Weather Layer

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `GlobalWeatherDirector` | `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs:14`, `282` | direct `IWeatherService` publisher |

Related supporting owners found in source:

| Owner | Evidence | Role |
|---|---|---|
| `HectonSurfaceWeatherDirector` | `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs:24` | adjacent surface-weather authority, separate from global weather contract |
| `SurfaceWeatherVfxRig` | `Assets/_Project/Scripts/Atmosphere/SurfaceWeatherVfxRig.cs:12` | weather VFX presentation side |

Current interpretation:

- `GlobalWeatherDirector` is the service-facing global weather owner
- there is also a specialized atmosphere/surface-weather branch
- older docs that flatten weather into one visual system are incomplete

## 3. Ocean Kinematics Layer

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `OceanKinematicsRuntimeService` | `Assets/_Project/Scripts/Core/OceanKinematicsRuntimeService.cs:13`, `235` | direct `IHectonOceanKinematicsService` publisher |

Supporting adapters found in source:

| Owner | Evidence | Role |
|---|---|---|
| `Crest4KinematicsAdapter` | `Assets/_Project/Scripts/Crest4KinematicsAdapter.cs:13` | Crest-backed ocean provider path |
| `Crest5KinematicsAdapter` | `Assets/_Project/Scripts/Crest5KinematicsAdapter.cs:15` | Crest-backed ocean provider path |

Current interpretation:

- the runtime service is a selector/provider surface
- Crest adapters exist underneath that surface
- gameplay systems are intended to query the service, not talk directly to raw ocean adapters

## 4. Thermal / Vent Layer

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `AbyssalThermalManager` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:19`, `2317` | direct thermodynamics runtime owner and `IThermodynamicsService` source |

Supporting thermal entities:

| Owner | Evidence | Role |
|---|---|---|
| `ThermalUpdraftVolume` | `Assets/_Project/Scripts/ThermalUpdraftVolume.cs:14` | slow-tick thermal influence volume |
| `ThermalGeyser` | `Assets/_Project/Scripts/ThermalGeyser.cs:15` | active thermal/geyser runtime actor |

Current interpretation:

- thermodynamics is not only a passive data provider
- there is a service-facing global owner plus local thermal actors/volumes

## 5. Procedural Scatter / World Generation Layer

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `WorldProceduralScatterDirector` | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:643`, `1779` | direct `IWorldGenService` publisher and scene-bootstrap listener |

Supporting world/scatter neighbors:

| Owner | Evidence | Role |
|---|---|---|
| `GPUScatterDirector` | `Assets/_Project/Scripts/World/GPUScatterDirector.cs:13` | lower-level scatter/runtime execution surface |
| `ScatterBudgetController` | `Assets/_Project/Scripts/ScatterBudgetController.cs:10` | budget/cadence control sidecar |
| `HectonScatterOutput` | `Assets/_Project/Scripts/HectonScatterOutput.cs:53` | graph/output side integration point |

Current interpretation:

- scatter/worldgen is not one file, but `WorldProceduralScatterDirector` is the service-facing owner
- additional runtime/budget helpers sit around it

## 6. Ecosystem Layer

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `EcosystemDirector` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:29`, `1053` | direct `IEcosystemDirectorService` publisher |

Related neighboring ecology owners:

| Owner | Evidence | Role |
|---|---|---|
| `EcosystemHealthDirector` | `Assets/_Project/Scripts/Ecosystem/EcosystemHealthDirector.cs:17` | save-backed ecosystem health state |
| `FaunaGeneticsManager` | `Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs:16` | save-backed genetics/evolution side owner |
| `FaunaDirector` | `Assets/_Project/Scripts/FaunaDirector.cs:68` | runtime fauna orchestration owner |

Current interpretation:

- `EcosystemDirector` is the service-facing population simulation authority
- health/genetics/fauna orchestration are related but separate owners

## 7. Debris Layer

Primary owner:

| Owner | Evidence | Role |
|---|---|---|
| `DebrisManager` | `Assets/_Project/Scripts/Gameplay/DebrisManager.cs:17`, `109` | direct `IDebrisService` publisher |

Related debris definition surface:

| Owner | Evidence | Role |
|---|---|---|
| `OrganicDebrisProfile` | `Assets/_Project/Scripts/Gameplay/DebrisManager.cs:837` | concrete `IDebrisDefinition` carrier |
| `SargassumDebrisParticleSystem` | `Assets/_Project/Scripts/World/SargassumDebrisParticleSystem.cs:12` | nearby debris/VFX specialization |

Current interpretation:

- `DebrisManager` is the burst/runtime owner
- debris profiles/definitions are separate authored/runtime payload carriers

## 8. Submarine Layer

Primary runtime owners:

| Owner | Evidence | Role |
|---|---|---|
| `SubmarineCoreDirector` | `Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs:22`, `121` | direct `ISubmarineRuntimeContext` publisher |
| `SubmarineStructuralGrid` | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:37`, `273` | direct `ISubmarineHullBreachReadModel` publisher |

Important neighboring submarine systems:

| Owner | Evidence | Role |
|---|---|---|
| `HectonSubmarineOS` | `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs:130` | updatable/renderable submarine OS presentation owner |
| `SubmarineFluidDynamics` | `Assets/_Project/Scripts/SubmarineFluidDynamics.cs:48` | submarine fluid/physics side |
| `SubmarineAtmosphereSystem` | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs:118` | atmosphere and interaction-consumer side |
| `SubmarineStationKeepingController` | `Assets/_Project/Scripts/Gameplay/SubmarineStationKeepingController.cs:15` | stabilization/control slice |
| `SubmarineElectrolysisModule` | `Assets/_Project/Scripts/SubmarineElectrolysisModule.cs:50` | power component slice |

Current interpretation:

- submarine is a multi-owner domain
- `SubmarineCoreDirector` and `SubmarineStructuralGrid` are the main registry-facing runtime anchors
- control, atmosphere, dynamics, and OS presentation are separate adjacent owners

## 9. Cross-Domain Reality

The world/environment/submarine layer is not a single hierarchy.
It is currently shaped like this:

- environment context publishes shared world-facing references
- weather and ocean are separate runtime service layers
- thermal and scatter are separate simulation domains
- ecosystem is its own long-horizon population domain
- debris is an effects/destruction runtime domain
- submarine is split into root runtime, breach read model, control, atmosphere, dynamics, and UI/presentation

This is more modular than many older docs implied.
It is also harder to navigate without a map.

## What Looks Good

- major world-facing runtime domains do have identifiable owners
- service-facing owners are explicit for weather, ocean, thermal, worldgen, ecosystem, debris, and submarine
- submarine authority is not falsely compressed into one giant root class
- ecology and scatter are already separate from simple â€œenvironmentâ€ labeling

## What Looks Merely Acceptable

- there are many adjacent owners, which is structurally healthier but harder to read quickly
- some domains still blend simulation and presentation neighbors in ways that deserve future deeper audit

## What Looks Weak

- there is still no measured runtime authority showing these domains behave well together under real traversal
- service-facing clarity exists, but folder-level discoverability remains expensive
- some adjacent owners likely deserve their own future submaps if the docset keeps expanding

## Failure Modes To Watch

- world-facing systems can drift into parallel authority if their service-facing owners and neighboring specialists stop aligning
- submarine domain can accumulate cross-links fast because it spans physics, UI, atmosphere, damage, and control
- environment and worldgen can look healthy in static code while runtime cadence or memory behavior regresses

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves readability of the non-player world-facing stack and reduces false flattening of environment/submarine domains. |

## Verdict

The non-player world-facing stack is now materially better documented.

Current service-facing anchors are:

- `EnvironmentRuntimeContextService`
- `GlobalWeatherDirector`
- `OceanKinematicsRuntimeService`
- `AbyssalThermalManager`
- `WorldProceduralScatterDirector`
- `EcosystemDirector`
- `DebrisManager`
- `SubmarineCoreDirector`
- `SubmarineStructuralGrid`

This is a stronger and more honest map than the previous generalized wording.

STATUS: PENDING VERIFICATION
