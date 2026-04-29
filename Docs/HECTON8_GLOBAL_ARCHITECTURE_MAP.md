# HECTON-8 Global Architecture Map

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: source-backed architecture map only, not a profiler capture and not a build-certification report

Mandates followed:
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `AGENTS.md`

## 1. Scope

- Audit target: `Assets/_Project/**/*.cs`
- Current first-party `.cs` inventory under `Assets/_Project`: `1010`
- Current first-party `.cs` inventory under `Assets/_Project/Scripts`: `970`
- Current script line count under `Assets/_Project/Scripts`: `420468`
- Average lines per script: `433.47`
- Scripts still living directly in `Assets/_Project/Scripts` root: `312`

This file replaces the older generated map whose `808`-script snapshot and some interface conclusions are no longer true.

## 2. Runtime Topology

Representative structure visible in current source:

1. `Bootstrap/GameBootstrapper.cs` initializes registry-owned services and dependency edges.
2. `SceneBootstrap.cs` separately owns scene warmup, readiness gates, and bootstrap event flow.
3. `Core/GlobalRegistry.cs` exposes shared service slots.
4. `Core/SystemDispatcher.cs` and `GameTickManager.cs` drive central runtime cadence.
5. feature owners in gameplay, world, UI, audio, quest, and submarine layers consume those services through mixed direct references, interfaces, and event lanes.

Meaning:

- the project is not raw Unity chaos
- the project also does not have one uncontested startup sovereign

## 3. Confirmed Contract Owners

Current direct interface ownership confirmed by source scan:

- `IAudioService` -> `SpatialAudioManager`
- `IUIService` -> `UI/SuitHUDV4CanvasOverlay`
- `IModularEquipmentService` -> `ModularEquipmentEngine`
- `IPowerGridService` -> `Power/PowerGridManager`
- `IThermodynamicsService` -> `World/AbyssalThermalManager`
- `ILogisticsService` -> `Construction/ConstructionManager`
- `IWorldGenService` -> `WorldProceduralScatterDirector`
- `IEncounterDirectorService` -> `HectonDirectorAI`
- `IQuestSystem` -> `Quest/QuestManager`
- `ISubmarineRuntimeContext` -> `SubmarineCoreDirector`
- `ISubmarineHullBreachReadModel` -> `SubmarineStructuralGrid`

Current direct rendering contract owners:

- `IRenderable` -> `HectonUnderwaterVisuals`
- `IRenderable` -> `Gameplay/HectonSubmarineOS`
- `IRenderable` -> `Quest/MissionMarkerSystem`

Current direct damage contract owner confirmed by source scan:

- `Hecton8.Core.IDamageReceiver` -> `Gameplay/HabitatIntegrityManager`

This is materially different from older documents that claimed `IAudioService` was ghost, `IUIService` was directly multi-owned, or `IDamageReceiver` had two current implementers.

## 4. Event Architecture

Current project uses mixed event styles.

Queue-backed deferred buses confirmed by source:

- `SaveEvents`
- `QuestEvents`
- `ScanEvents`
- `NarrativeEvents`
- `AudioLogEvents`

Direct static delegate buses still present:

- `InteractionEvents`
- `CraftingEvents`
- `PDAEvents`
- `FlashlightEvents`
- `RandomEventEvents`
- `HectonSubmarineOsEvents`

Meaning:

- event architecture is not uniform
- migration toward deferred `NativeQueue` buses is real, but incomplete

## 5. Current Editor Readback

Current reachable Unity Editor state during the latest recheck:

- Build Settings scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`
- Loaded active scene: `02_HECTON_WORLD`
- Latest console readback: `15` errors, but all `15` are package-side MCP `ManageAsset` conversion errors, not first-party compile errors

Latest console slice observed:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message pattern: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

This is a live editor snapshot only.
It is not proof of stable play-mode behavior, shipping build health, or zero-GC runtime.

## 6. Concentration Risks

Largest current first-party owners by line count:

- `World/HectonMapMagicVegetationBridge.cs` - `13279`
- `WorldProceduralScatterDirector.cs` - `10333`
- `HectonPlayerMovement.cs` - `7851`
- `HectonUnderwaterVisuals.cs` - `4826`
- `UI/SuitHUDV4CanvasOverlay.cs` - `4608`
- `Audio/PlayerCriticalProceduralAudioRenderer.cs` - `4200`
- `HectonVoxelEngine.cs` - `4138`
- `World/SargassumMicroFaunaBoids.cs` - `3921`

These are not cosmetic complaints.
They are risk multipliers because runtime ownership, visuals, simulation, and integration pressure accumulate inside very large files.

## 7. Proven Tensions

- bootstrap ownership is split between `GameBootstrapper` and `SceneBootstrap`
- event architecture is mixed between queue-backed and direct static buses
- root-folder ownership is still noisy with `312` scripts in `Assets/_Project/Scripts`
- codebase health is stronger than some old reports claimed, but file-size concentration and ownership spread remain real

## 8. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: documentation only
Correctness: improved because stale script counts, stale interface ownership claims, and stale compile-state assumptions were removed

## 9. Hot Path Impact

None. Markdown-only pass.

## 10. Failure Modes

- source ownership can drift again without synchronized doc maintenance
- current console cleanliness can change between editor sessions
- large-owner files can hide internal contradictions even when top-level maps remain accurate

## 11. Why This Version Was Kept

Kept because the previous version had become numerically and structurally false.
This version is shorter, but it is anchored to current code and current reachable editor state.

STATUS: PENDING VERIFICATION
