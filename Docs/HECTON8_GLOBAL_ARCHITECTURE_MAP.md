# HECTON-8 Global Architecture Map

Date: 2026-05-08
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

## 2026-05-08 Current-State Boundary

- Read `Docs/Reports/2026-05-08_DOCUMENTATION_CONTINUATION_SYNC.md`, `Docs/Reports/2026-05-08_ACTIVE_DOCUMENTATION_MANIFEST.json`, `Docs/Reports/2026-05-07_MAIN_DOCUMENTATION_CURRENT_STATE_REFRESH.md`, `Docs/Reports/2026-05-07_FINAL_INQUISITION_NATIVE_SCANNER.md`, `Docs/Reports/2026-05-07_BRUTAL_SYNCHRONIZATION_REPORT.md`, `Docs/Reports/2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md`, `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`, and `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md` before using this map as current project truth.
- This map is source-backed architecture orientation, not Play Mode proof, profiler proof, console certification, or scene/prefab wiring proof.
- Line counts and interface counts are orientation data only; source files and fresh verification logs win.

## 1. Scope

- Audit target: `Assets/_Project/**/*.cs`
- Current first-party `.cs` inventory under `Assets/_Project`: `1247`
- Current first-party `.cs` inventory under `Assets/_Project/Scripts`: `1206`
- Current script physical line count under `Assets/_Project/Scripts` by `System.IO.StreamReader.ReadLine()`: `675775`
- Current script line count under `Assets/_Project/Scripts` by `Measure-Object -Line`: `580769`
- Average lines per script by `Measure-Object -Line`: `481.57`
- Scripts still living directly in `Assets/_Project/Scripts` root: `337`

This file replaces the older generated map whose `808`-script snapshot and some interface conclusions are no longer true.

2026-05-08 correction:
- Counts above supersede the April 30 `1038/998/444135`, earlier May 1 `1060/1020/466768`, May 1 `1020/544728`, earlier May 2 `489893`, May 2 `1087/1047/571562/317`, May 6 `651121/552119`, and earlier May 7 `651253/559502`, `651810/560025`, `652238/560372`, `652787/560848`, `655363/563210`, and `667771` snapshots.
- Current `GlobalRegistryContracts.cs` contains `40` direct public interfaces.
- Older interface coverage ratios in this document are stale orientation only; re-open `GlobalRegistryContracts.cs` and implementors before making interface-completion claims.
- Current completed full Core dependency build succeeded with `48 Warning(s)` and `0 Error(s)` in `CodexArtifacts/2026-05-08_DOC_SYNC_CORE_BUILD3.log`; later source writes were observed, so this is latest completed build evidence, not stable-source proof.
- Current Unity MCP editor-console readback returned `0` error/warning entries on active scene `00_BOOTSTRAP`.
- This architecture map is not a Play Mode, profiler, GCMonitor, memory-retention, or player-build certificate.

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

Current `GlobalRegistry` service ownership confirmed by source scan:

- `IInputService` -> `InputDispatcher`
- `IPhysicsService` -> `PhysicsApplySystem`
- `IAudioService` -> `SpatialAudioManager`
- `ISceneService` -> `SceneRuntimeService`
- `ISaveService` -> `SaveManager`
- `IUIService` -> `UI/SuitHUDV4CanvasOverlay`
- `IPlayerRuntimeContext` -> `PlayerRuntimeContextService`
- `IPlayerInventoryService` -> `PlayerInventoryManager`
- `IModularEquipmentService` -> `ModularEquipmentEngine`
- `IPlayerSensoryService` -> `PlayerSensoryManager`
- `IEnvironmentRuntimeContext` -> `EnvironmentRuntimeContextService`
- `IWeatherService` -> `GlobalWeatherDirector`
- `IThermodynamicsService` -> `World/AbyssalThermalManager`
- `ILogisticsService` -> `Construction/ConstructionManager`
- `IWorldGenService` -> `WorldProceduralScatterDirector`
- `IEncounterDirectorService` -> `HectonDirectorAI`
- `IQuestSystem` -> `Quest/QuestManager`
- `IHectonOceanKinematicsService` -> `OceanKinematicsRuntimeService`
- `IInteractionSignalService` -> `EquipmentInteractionHandler`
- `IDebrisService` -> `Gameplay/DebrisManager`
- `IEcosystemDirectorService` -> `World/EcosystemDirector`

Additional registry-backed contracts outside that 21-owner service list:

- `IPDALogbookService` -> `PDA/PDALogbookManager`
- `IPowerGridService` -> `PowerGridManager`
- `IFaunaSim` -> `Fauna/FaunaSimulationEngine` plus bootstrap fallback `DemiurgeFaunaSimulationService`
- `IFluidSim` -> `Physics/FluidMathCore`
- `ISubmarineRuntimeContext` -> `SubmarineCoreDirector`
- `ISubmarineHullBreachReadModel` -> `SubmarineStructuralGrid`

Current direct rendering contract owners:

- `IRenderable` -> `HectonUnderwaterVisuals`
- `IRenderable` -> `Gameplay/HectonSubmarineOS`
- `IRenderable` -> `Quest/MissionMarkerSystem`

Current direct damage contract owner confirmed by source scan:

- `Hecton8.Core.IDamageReceiver` -> `Gameplay/HabitatIntegrityManager`

This is materially different from older documents that claimed `IAudioService` was ghost, `IUIService` was directly multi-owned, or `IDamageReceiver` had two current implementers.
It also supersedes older interface-count claims. Current direct public interface count is `39`; coverage must be rechecked from source before being used as proof.

## 4. Event Architecture

Current project documentation authority uses the five-artery Mega-Bus classification:

- Core
- Env
- Player
- Base
- AI

Cross-domain authority is documented in `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/EVENT_BUS_MAP.md` and `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/EVENT_FLOW_MAP.md`.

Current rule:

- Direct `Action`, `Func`, C# `event`, `delegate`, or `UnityEvent` chains are legacy debt when they cross domain boundaries.
- Local owner callbacks and async completion callbacks do not become event architecture authority by appearing in code.
- Source scan found one static `Action`-typed GPU readback callback bridge in `SaveThumbnailSystem.cs`; it is not a first-party cross-domain bus lane.

## 5. Current Editor Readback

Current documentation pass status:

- `Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md` is the latest broad documentation synchronization, active header-debt closure, official Unity/package source-check, and Unity MCP editor-readback addendum
- `Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` is the latest documentation sorting and authority map, amended by the May 6 synchronization pass
- `Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` is the latest broad documentation/build/guard actuality sweep before the May 6 sync layer
- `Reports/2026-05-04_WARNING_CLEANUP.md` is the latest first-party warning cleanup and post-refresh console-readback addendum
- `Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` is the latest foundation guard repair addendum
- `Reports/2026-05-01_CURRENT_PROJECT_STATE.md` remains the stable conceptual state anchor
- May 4 local Core and Editor builds returned `0 Warning(s)` / `0 Error(s)`
- latest May 4 DOTS and PlayModeTests builds returned `0 Warning(s)` / `0 Error(s)`
- May 4 post-repair foundation guard scan exits `0`; `.Run(` remains `0`, raw `UnsafeUtility.MemCpy` outside guard is `0`, and unauthorized Unity loop methods are `0`
- earlier May 4 MCP readback found active scene `01_MAIN_MENU`, editor in Play Mode transition, console errors `0`, and console warnings present; latest May 6 MCP readback found active scene `00_BOOTSTRAP`, Play Mode off, compiling false, ready for tools, console error/warning entries `0`, render textures `37`, render texture bytes `56,320,492`, and renderer `PC_Renderer`

Historical Unity Editor readback from older rechecks:

- Build Settings scenes: `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`
- Loaded active scene: `02_HECTON_WORLD`
- Older console readback: `15` errors, but all `15` were package-side MCP `ManageAsset` conversion errors, not first-party compile errors

Latest console slice observed:

- tool/package source: `./Library/PackageCache/com.coplaydev.unity-mcp.../Editor/Tools/ManageAsset.cs`
- repeated message pattern: `Failed to convert -1 to a unsigned 32 bit int`
- affected assets: `Assets/_Project/Data/Scavenging/ResourceNodes/ResourceNodeTemplate_*`

This older package-side MCP error slice is historical and was not reproduced by the May 4 or May 6 console error queries.
It is not proof of stable play-mode behavior, shipping build health, or zero-GC runtime.

## 6. Concentration Risks

Largest current first-party owners by line count:

- `WorldProceduralScatterDirector.cs` - `11778`
- `HectonPlayerMovement.cs` - `10571`
- `HectonVoxelEngine.cs` - `6751`
- `World/HectonMapMagicVegetationBridge.cs` - `6640`
- `HectonUnderwaterVisuals.cs` - `6553`
- `UI/SuitHUDV4CanvasOverlay.cs` - `6220`
- `Audio/PlayerCriticalProceduralAudioRenderer.cs` - `6047`
- `SaveBinaryStorage.cs` - `5743`
- `World/PersistentWorldRegistry.cs` - `5646`
- `WorldProceduralFieldSampler.cs` - `5100`

These are not cosmetic complaints.
They are risk multipliers because runtime ownership, visuals, simulation, and integration pressure accumulate inside very large files.

## 7. Proven Tensions

- bootstrap ownership is split between `GameBootstrapper` and `SceneBootstrap`
- event architecture is mixed between queue-backed and direct static buses
- root-folder ownership is still noisy with `337` scripts in `Assets/_Project/Scripts`
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
