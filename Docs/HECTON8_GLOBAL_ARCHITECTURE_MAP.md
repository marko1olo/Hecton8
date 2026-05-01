# HECTON-8 Global Architecture Map

Date: 2026-05-01
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

## 2026-05-01 Current-State Boundary

- Read `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` and `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` before using this map as current project truth.
- This map is source-backed architecture orientation, not Play Mode proof, profiler proof, console certification, or scene/prefab wiring proof.
- Line counts and interface counts are orientation data only; source files and fresh verification logs win.

## 1. Scope

- Audit target: `Assets/_Project/**/*.cs`
- Current first-party `.cs` inventory under `Assets/_Project`: `1060`
- Current first-party `.cs` inventory under `Assets/_Project/Scripts`: `1020`
- Current script line count under `Assets/_Project/Scripts`: `544728`
- Average lines per script: `534.05`
- Scripts still living directly in `Assets/_Project/Scripts` root: `316`

This file replaces the older generated map whose `808`-script snapshot and some interface conclusions are no longer true.

2026-05-01 correction:
- Counts above supersede the April 30 `1038/998/444135` and earlier May 1 `1060/1020/466768` snapshots.
- Current `GlobalRegistryContracts.cs` contains `31` interfaces.
- `IGlobalRegistryHotSwapListener` currently has no direct implementor and must be treated as an empty extension seam, not as a proven live runtime service.

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
- `IPDALogbookService` -> `PDA/PDALogbookManager`
- `IPowerGridService` -> `Power/PowerGridManager`
- `IFaunaSim` -> `Fauna/FaunaSimulationEngine` plus bootstrap fallback `DemiurgeFaunaSimulationService`
- `IFluidSim` -> `Physics/FluidMathCore`
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
It also supersedes the April 30 interface count: source-level interface coverage is now `30/31`, with `IGlobalRegistryHotSwapListener` still empty.

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

Current documentation pass editor status:

- no fresh Unity console readback was obtained for this May 1 documentation update
- Play Mode was not launched
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md` is the current conceptual state anchor

Historical Unity Editor readback from the previous recheck:

- Build Settings scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`
- Loaded active scene: `02_HECTON_WORLD`
- Latest console readback: `15` errors, but all `15` are package-side MCP `ManageAsset` conversion errors, not first-party compile errors

Latest console slice observed:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message pattern: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

This is a historical editor snapshot only and was not refreshed in the 2026-05-01 docset actuality pass.
It is not proof of stable play-mode behavior, shipping build health, or zero-GC runtime.

## 6. Concentration Risks

Largest current first-party owners by line count:

- `World/HectonMapMagicVegetationBridge.cs` - `15598`
- `WorldProceduralScatterDirector.cs` - `11535`
- `HectonPlayerMovement.cs` - `9275`
- `UI/SuitHUDV4CanvasOverlay.cs` - `5659`
- `HectonUnderwaterVisuals.cs` - `5652`
- `World/PersistentWorldRegistry.cs` - `5092`
- `SaveBinaryStorage.cs` - `4871`
- `HectonVoxelEngine.cs` - `4818`
- `Audio/PlayerCriticalProceduralAudioRenderer.cs` - `4789`
- `FaunaDirector.cs` - `4698`

These are not cosmetic complaints.
They are risk multipliers because runtime ownership, visuals, simulation, and integration pressure accumulate inside very large files.

## 7. Proven Tensions

- bootstrap ownership is split between `GameBootstrapper` and `SceneBootstrap`
- event architecture is mixed between queue-backed and direct static buses
- root-folder ownership is still noisy with `316` scripts in `Assets/_Project/Scripts`
- codebase health is stronger than some old reports claimed, but file-size concentration and ownership spread remain real

## 8. Regression Model

CPU: no runtime code changed
GC: no runtime code changed
Memory: no runtime code changed
Cadence: documentation only
Correctness: improved because stale script counts and large-owner line counts were refreshed; editor state remains historical until a future Unity run

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
