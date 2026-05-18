# HECTON-8 GAMEPLAY SYSTEM OWNERSHIP LEDGER

Date: 2026-05-07
Status: PENDING VERIFICATION

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

Scope: broad source-backed owner inventory for major first-party gameplay domains
Mandates followed: `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `UI_Data_Streaming_ZeroGC_Optimization.txt`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`

2026-05-01 trust note:

- Read `Docs/Reports/2026-05-18_DOCUMENTATION_REPORT_VAULT_AND_NAVIGATION_R17_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_R15_NAVIGATION_SUPERSESSION_R16_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_ENTRYPOINT_NAVIGATION_R15_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_BATCH008_BINARY_HYGIENE_R14_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_GENERIC_REPORT_BOUNDARIES_R13_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_ACTIVE_REMAINDER_R11_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_LONGTAIL_INTERIOR_R10_LOCAL.md`, `Docs/Reports/2026-05-18_DOCUMENTATION_EVIDENCE_LANGUAGE_AND_COUNTERS_R9_LOCAL.md`, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, and `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md` before any older counter, root-path, build-artifact, or pre-Batch008 binary-hygiene claim. Treat the May 4 / May 1 reports as historical/domain context only.
- Read `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` when the question is project readiness or overall risk, not narrow domain ownership.
- This ledger is a navigation aid, not a live verification artifact.
- Current cross-project risks remain mixed registry/singleton authority, local job barriers, headless/presentation coupling, and missing profiler/GC proof.

## Purpose

The active docset already has narrow truth pages for:

- player/gameplay core
- construction/runtime integration
- save/load runtime truth
- scene/prefab service-owner truth

What it still lacked was one broad owner ledger that answers:

Which large gameplay domains exist right now, and which class is the primary owner for each.

This file is that ledger.

## Proof Boundary

This ledger is based on current first-party source under `Assets/_Project/Scripts`.

It is intended to reduce navigation ambiguity.
It is not play-mode correctness proof.

It does not guarantee:

- the current owner is the right owner architecturally
- all domain edges are bug-free
- all listed owners are low-GC or low-CPU in live runtime

## Reading Rule

This file is broad.
The deeper truth files for specific domains still matter:

- player stack -> `PLAYER_GAMEPLAY_CORE_MAP.md`
- construction stack -> `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md`
- save/load stack -> `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md`
- authored-vs-runtime service truth -> `2026-04-29_SCENE_PREFAB_SERVICE_OWNER_TRUTH.md`

## Domain Ledger

### 1. Core Runtime / Dispatch Surface

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Input runtime | `InputDispatcher` | `Assets/_Project/Scripts/Core/InputDispatcher.cs:14`, `128`, `146` | direct `IInputService` publisher |
| Scene transition runtime | `SceneRuntimeService` | `Assets/_Project/Scripts/Core/SceneRuntimeService.cs:15`, `254` | direct `ISceneService` publisher |
| Save runtime | `SaveManager` | `Assets/_Project/Scripts/SaveManager.cs:32`, `273` | direct `ISaveService` publisher |
| Physics routing | `PhysicsApplySystem` | `Assets/_Project/Scripts/PhysicsApplySystem.cs:198`, `280` | direct `IPhysicsService` publisher |
| Audio routing | `SpatialAudioManager` | `Assets/_Project/Scripts/SpatialAudioManager.cs:71`, `380` | direct `IAudioService` publisher |
| UI runtime | `SuitHUDV4CanvasOverlay` | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs:31`, `849` | direct `IUIService` publisher |

Current interpretation:

- these are not helper classes
- these are the active service-facing core owners
- they form the most visible gameplay-facing runtime shell

### 2. Player Locomotion / Survival

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Movement | `HectonPlayerMovement` | `Assets/_Project/Scripts/HectonPlayerMovement.cs:44` | core locomotion owner |
| Survival / health / oxygen / persistence | `HectonSurvivalSystem` | `Assets/_Project/Scripts/HectonSurvivalSystem.cs:184`, `1536-1575` | `ISaveable`, save/load participant |
| Player runtime read model | `PlayerRuntimeContextService` | `Assets/_Project/Scripts/Core/PlayerRuntimeContextService.cs:19`, `598` | canonical player context publication |

This split matters:

- `HectonPlayerMovement` and `HectonSurvivalSystem` own actual behavior
- `PlayerRuntimeContextService` owns central access and rebinding

### 3. Player Inventory / Tools / Build Interaction

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Tool handoff and active tool switching | `PlayerToolManager` | `Assets/_Project/Scripts/PlayerToolManager.cs:44` | authored player-side tool coordinator |
| Runtime inventory service mirror | `PlayerInventoryManager` | `Assets/_Project/Scripts/Core/PlayerInventoryManager.cs:14`, `205` | exposes inventory/tooling through registry |
| Modular tool runtime compilation | `ModularEquipmentEngine` | `Assets/_Project/Scripts/ModularEquipmentEngine.cs:20`, `83-96`, `535` | native-buffer-backed runtime equipment owner |
| Build tool authority | `PlayerBuilder` | `Assets/_Project/Scripts/PlayerBuilder.cs:51` | concrete build-capable tool owner |
| Queued interaction/raycast authority | `EquipmentInteractionHandler` | `Assets/_Project/Scripts/Interaction/EquipmentInteractionHandler.cs:16`, `39`, `86`, `158-166`, `469` | deferred interaction signal and batched raycast owner |

Current interpretation:

- `PlayerToolManager` is not enough to understand tools by itself
- `ModularEquipmentEngine` is the runtime stat/compiler side
- `EquipmentInteractionHandler` is the deferred world-interaction side
- `PlayerBuilder` is only one concrete tool owner inside that wider ecosystem

### 4. PDA / Knowledge / Marker / Exchange Domain

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| PDA actor root | `PlayerPDA` | `Assets/_Project/Scripts/PlayerPDA.cs:86` | authored player-side PDA behavior root |
| Logbook persistence | `PDALogbookManager` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:55`, `93`, `96`, `206`, `248` | `ISaveable`, late UI/knowledge persistence |
| PDA item/content exchange | `PDAExchangeSystem` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:15`, `71`, `72`, `279`, `324` | `ISaveable`, exchange-state owner |
| PDA markers | `PDAMarkerRegistry` | `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:57`, `91`, `94`, `273`, `301` | `ISaveable`, marker-state owner |

Current interpretation:

- the PDA domain is not one class
- it is distributed across actor root, logbook state, exchange state, and marker state
- older docs that flatten PDA into a single owner are incomplete

### 5. Quest / Progression Domain

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Quest runtime service | `QuestManager` | `Assets/_Project/Scripts/Quest/QuestManager.cs:17`, `52`, `54`, `77-78`, `314`, `341` | `IQuestSystem` + `ISaveable` |
| Encounter pressure / dramatic pacing | `HectonDirectorAI` | `Assets/_Project/Scripts/HectonDirectorAI.cs:16`, `130` | direct `IEncounterDirectorService` publisher |

Current interpretation:

- `QuestManager` owns authored quest activation/completion state
- `HectonDirectorAI` owns encounter pressure and pacing, not authored quest data
- these systems are related in progression feel, but they are not the same owner

### 6. Environment / Weather / Thermal Domain

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Environment runtime context | `EnvironmentRuntimeContextService` | `Assets/_Project/Scripts/Core/EnvironmentRuntimeContextService.cs:12`, `209` | registry-facing environment context owner |
| Global weather | `GlobalWeatherDirector` | `Assets/_Project/Scripts/Environment/GlobalWeatherDirector.cs:14`, `282` | direct `IWeatherService` publisher |
| Thermodynamics / vent flow | `AbyssalThermalManager` | `Assets/_Project/Scripts/World/AbyssalThermalManager.cs:19`, `2317` | direct thermodynamics runtime owner |
| Underwater presentation | `HectonUnderwaterVisuals` | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs:74` | render/tick-side underwater presentation owner |

Current interpretation:

- environment is split between simulation/state services and presentation owner
- weather and thermal are not hidden subfeatures of one mono-manager

### 7. World Generation / Ecology Domain

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Procedural scatter/world generation | `WorldProceduralScatterDirector` | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs:643`, `1779` | direct `IWorldGenService` publisher, bootstrap-scene listener |
| Ecosystem population simulation | `EcosystemDirector` | `Assets/_Project/Scripts/World/EcosystemDirector.cs:28`, `937` | direct `IEcosystemDirectorService` publisher |
| Encounter pressure escalation | `HectonDirectorAI` | `Assets/_Project/Scripts/HectonDirectorAI.cs:16`, `130` | lives between AI and tension-direction domain |

Current interpretation:

- world generation, ecology, and encounter pressure are adjacent but distinct domains
- current code already separates them more than older docs suggested

### 8. Construction / Habitat / Power Domain

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Construction orchestration | `ConstructionManager` | `Assets/_Project/Scripts/ConstructionManager.cs:38`, `372-373`, `388`, `497`, `745` | logistics service + save participant |
| Authored module definition | `BaseModuleTemplate` | `Assets/_Project/Scripts/BaseModuleTemplate.cs:18`, `55-87` | immutable module family definition |
| Placed runtime module | `BaseModule` | `Assets/_Project/Scripts/BaseModule.cs:85`, `375-386`, `772+` | power/cut/pool/slowtick placed instance |
| Habitat topology compile | `HabitatGraphManager` | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs:15`, `39`, `45`, `88` | graph rebuild authority |
| Global power runtime | `PowerGridManager` | `Assets/_Project/Scripts/PowerGridManager.cs:16`, `114`, `383` | global power-grid service |

Deep dive already exists in `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md`.

### 9. Submarine Domain

| Domain slice | Current owner | Evidence | Notes |
|---|---|---|---|
| Submarine runtime root | `SubmarineCoreDirector` | `Assets/_Project/Scripts/Gameplay/SubmarineCoreDirector.cs:22`, `121` | direct `ISubmarineRuntimeContext` publisher |
| Hull-breach read model | `SubmarineStructuralGrid` | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs:37`, `273` | direct `ISubmarineHullBreachReadModel` publisher |

Current interpretation:

- submarine state is already split between runtime root and structural breach read model
- older docs that describe â€œsubmarine systemâ€ as one owner are too coarse

## Cross-Cutting Save Participants Confirmed In This Pass

Current confirmed nontrivial save participants include:

| Owner | Evidence |
|---|---|
| `HectonSurvivalSystem` | `Assets/_Project/Scripts/HectonSurvivalSystem.cs:184`, `1536-1575` |
| `QuestManager` | `Assets/_Project/Scripts/Quest/QuestManager.cs:17`, `52`, `54`, `314`, `341` |
| `ConstructionManager` | `Assets/_Project/Scripts/ConstructionManager.cs:38`, `372-373`, `388`, `497` |
| `PDALogbookManager` | `Assets/_Project/Scripts/PDA/PDALogbookManager.cs:55`, `93`, `96`, `206`, `248` |
| `PDAExchangeSystem` | `Assets/_Project/Scripts/Gameplay/PDAExchangeSystem.cs:15`, `71`, `72`, `279`, `324` |
| `PDAMarkerRegistry` | `Assets/_Project/Scripts/PDA/PDAMarkerRegistry.cs:57`, `91`, `94`, `273`, `301` |

This matters because the gameplay surface is not only split by behavior ownership.
It is also split by persistence timing and save/load order.

## What Looks Good

- major gameplay domains do have identifiable owners
- many important domains already publish through explicit `GlobalRegistry` contracts
- save participation is not hidden; several major systems expose explicit priorities and persistence methods
- player, construction, worldgen, ecology, and submarine are more structured than stale docs implied

## What Looks Merely Acceptable

- ownership is clear once traced, but discoverability cost is still high
- several domains are split correctly, but that split is spread across many files and services
- some systems use narrow mirror services, which improves access discipline but makes first-pass reading slower

## What Looks Weak

- there is still no single exhaustive owner ledger for every minor subsystem in the project
- broad navigation is now much better, but not yet equal to a full dependency export or architectural graph dump
- measured runtime proof is still absent across all these domains

## Failure Modes To Watch

- stale docs can still reappear as code moves and ledger is not refreshed
- service publication can look valid while authored scene/prefab truth drifts
- save participants can stay individually correct while integrated restore ordering regresses
- owner boundaries can erode if new logic keeps accreting onto already-large orchestrators

## Regression Model

| Dimension | Impact |
|---|---|
| CPU | None. Documentation-only pass. |
| GC | None. Documentation-only pass. |
| Memory | None. Documentation-only pass. |
| Cadence | None. Runtime code unchanged. |
| Correctness | Improves whole-project navigation by replacing vague â€œsystem areasâ€ with explicit current owners. |

## Verdict

The active docset now has a materially broader gameplay owner layer:

- core runtime shell
- player locomotion/survival
- inventory/tools/build interaction
- PDA/knowledge domain
- quest/progression domain
- environment/weather/thermal
- worldgen/ecology
- construction/habitat/power
- submarine runtime

It is still not mathematically exhaustive for every script in the repo.
But it is now much closer to a real ownership ledger than the docset had before.

STATUS: PENDING VERIFICATION
