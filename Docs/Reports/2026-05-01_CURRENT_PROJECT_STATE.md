# HECTON-8 Current Project State

Date: `2026-05-01`
Status: `PENDING VERIFICATION`
Scope: conceptual source-backed project state, not a runtime certification report

## Mandates Followed

- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `STRM_World_Streaming_Residency_Chunk_Management.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`

## What This File Is

This file is the current conceptual entry point for agents.
It reconciles active docs with current source ownership at a system level.

This file is not proof of:

- clean Play Mode
- fixed editor deadlock
- zero GC
- stable memory retention
- shipping readiness

No Play Mode was launched for this update.
No MCP Unity console refresh was obtained in the latest console-stabilization pass.
Local `Editor.log` evidence exists in `Docs/Reports/2026-05-01_EDITOR_LOG_CONSOLE_STABILIZATION.md`.

## Current Runtime Shape

HECTON-8 is a real, large Unity runtime with several mature subsystems and several authority conflicts.
The codebase is not paperware, but it is not architecturally clean.

Current source-backed shape:

- Bootstrap is split across `BootstrapController`, `GameBootstrapper`, `SceneBootstrap`, scene roots, and service self-registration paths.
- `GlobalRegistry` is the dominant service locator, but not the only authority because singleton and `DontDestroyOnLoad` residues still exist.
- `SystemDispatcher` is the central cadence owner for tick, fixed tick, late-frame tick, event flushing, and slow tick, but many systems still own local job completion decisions.
- World runtime is real and heavy: `PersistentWorldRegistry`, `HectonWorldGenerator`, `HectonVoxelEngine`, `VoxelDeltaProcessor`, `WorldProceduralScatterDirector`, MapMagic vegetation bridge, geology directors, voxel nav, ocean, weather, fauna, and spatial hash all exist as substantial code.
- Save runtime is real: `SaveManager` and `SaveBinaryStorage` implement a serious binary/native/integrity-oriented stack, but runtime save/load proof is still required.
- Narrative/lore/progression ownership is distributed and real: `HectonNarrativeDirector`, `LoreDatabaseManager`, `AudioLogSystem`, Atlas systems, `QuestManager`, scan/PDA/archive systems, and scripted arc directors are separate owners. Latest source recheck hardened `LoreDatabaseManager` hash lookup completion and found no duplicate hardcoded lore hashes in the 50 fixed industrial lore seeds; Play Mode proof is still absent.
- Input rebinding is currently a compile-stabilized bridge: the live `RebindingManager` implementation is under the Core assembly path while preserving the old MonoScript GUID, and the old Input path is a tombstone with a new GUID for stale Unity/Bee source-list safety. This is source-compile evidence, not proof that Core asmdef purity is solved.
- UI/HUD runtime is one of the stronger production surfaces: `SuitHUDV4CanvasOverlay`, `TMP_TextRegistry`, `UIStateStore`, char-buffer formatting, and menu/PDA systems exist. Presentation-owned gameplay state still exists elsewhere and must not be ignored.
- Construction/base runtime is real: `ConstructionManager`, `HabitatGraphManager`, `BaseModule`, `BaseAirlock`, logistics, integrity, life support, and module graph code exist. Latest source recheck added the missing pressure-buckling stress path and structural stress trigger bridge; runtime gameplay proof is still absent.
- Audio runtime is real: `SpatialAudioManager` owns `IAudioService`, procedural audio systems exist, and audio events are partially queue-backed. Latest source recheck added the structural stress procedural audio payload/event bridge; legacy/static residue remains.
- Abyssal thermal/base coupling is now source-present: `AbyssalThermalManager` contains habitat thermal infiltration into active `BaseModule` room temperature on `SlowTick`. This is compile-checked only, not balanced or profiled.
- Event topology is mixed: several first-party lanes are `NativeQueue`-backed and flushed by `SystemDispatcher.LateUpdate()`, while direct static delegate buses and the managed mod-facing `HectonEventBus` still exist. Source recheck shows dispatcher budget and `HectonEventBus.MaxDispatchDepth` are present; NativeQueue generation split remains unproven.
- Object pooling has hardened exhaustion behavior by current source review, but project-wide "no runtime Instantiate" is not proven.

## Current System Authority Map

Concept-level companion:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`

Use that file when the question is not "which exact class owns this line" but "is this domain load-bearing, transitional, presentation-only, experimental, or historical."

| Domain | Current Primary Owners | Current State |
|---|---|---|
| Bootstrap | `BootstrapController`, `GameBootstrapper`, `SceneBootstrap` | Split authority; not one sovereign startup path. |
| Registry | `GlobalRegistry`, `GlobalRegistryContracts` | Central and broad; singleton residue remains. |
| Cadence | `SystemDispatcher`, `GameTickManager` | Real dispatcher model; local job barriers still exist. |
| Scene loading | `SceneRuntimeService`, bootstrap route guards | Real async service surface; activation/warmup proof absent. |
| Pooling | `ObjectPoolManager`, `PoolBudgetProfile` | Hardened source direction; runtime purity unverified. |
| Save | `SaveManager`, `SaveBinaryStorage`, `SaveEvents` | Serious implementation; runtime validation pending. |
| World persistence | `PersistentWorldRegistry`, `WorldSpatialHashGrid` | Real native/state ownership; handle and deferred maintenance risks remain. |
| World generation | `HectonWorldGenerator`, `HectonVoxelEngine`, `VoxelDeltaProcessor` | Real and heavy; deadlock/stall risk remains in job lifecycle boundaries. |
| Scatter/vegetation | `WorldProceduralScatterDirector`, `HectonMapMagicVegetationBridge`, scatter backends | Real but oversized; editor/gizmo and job ownership risks remain. |
| Geology | `WorldGenerativeGeology*` family | Integration layer exists; dispatcher/registry guards were recently hardened by source review only. |
| Construction/base | `ConstructionManager`, `HabitatGraphManager`, `BaseModule`, `BaseAirlock` | Real gameplay domain; native lifecycle and authority boundaries require continued review. |
| Player/tools | `HectonPlayerMovement`, `PlayerTool`, `ModularEquipmentEngine`, `EquipmentInteractionHandler` | Real but overloaded; tool action and persistence ownership are split. |
| UI/HUD | `SuitHUDV4CanvasOverlay`, `TMP_TextRegistry`, `UIStateStore`, PDA/menu controllers | Comparatively mature; gameplay must not depend on UI/Animator events. |
| Audio | `SpatialAudioManager`, procedural audio renderers, audio event lanes | Real DSP direction; legacy service patterns remain. |
| Fauna/ecosystem | `FaunaBrain`, `FaunaDirector`, `EcosystemDirector`, fauna registries | Real but integration-heavy; headless/AUP risks remain. |
| Submarine | `HectonSubmarineOS`, `SubmarineCoreDirector`, `SubmarineElectrolysisModule`, structural systems | Real domain; mixed service/tick ownership is being purged gradually. |

## Current Critical Truths

These are the current high-level truths to preserve across future docs:

- Do not claim the Play Mode/editor deadlock is fixed. Static patches reduced candidate classes; runtime proof is absent.
- Do not claim global zero-GC. Some systems are designed for zero-GC, but project-wide profiler proof is absent.
- Do not claim DOTS is production architecture. DOTS/Entities is currently a seam/stub direction, not the live backbone.
- Do not claim Entities is installed. Current `Packages/manifest.json` does not declare `com.unity.entities`; `Assets/_Project/Scripts/World/Dots` is a gated placeholder lane.
- Do not claim Core asmdef isolation is complete. The active enforcement report explicitly rejected blind removal.
- Do not claim event architecture is fully queue-backed or generation-split. The project uses both queue-backed lanes and direct/static managed buses; event budget/depth guards exist, but same-frame NativeQueue reenqueue remains a design risk.
- Do not use older scatter manifesto teardown examples as current native-memory law. Current `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt` and source ownership win.
- Do not claim all docs are current. Active entry docs are current enough to guide work; older dated docs must be treated as historical unless linked by current indexes.
- Do not treat presentation, QA harnesses, DOTS seams, networking placeholders, or modding boundaries as load-bearing gameplay just because source files exist.

## Current P0 Risks

1. Editor/Play Mode deadlock truth is still fragmented.
2. Runtime job completion ownership is still too local in several cadence-sensitive systems.
3. Headless gameplay state still depends on presentation in known places such as fauna look logic. `StorageCrate` no longer relies on an Animator event for the `Opening -> Open` transition at source level, but Play Mode proof is absent.
4. Broad physics masks and default layer fallbacks are partially reduced. Remaining source-level `~0` query masks are concentrated in `AutonomousExtractorSystem`, `WorldCaveDirector`, and `WorldProceduralFieldSampler`; scene-layer validation is required before narrowing those.
5. Service authority remains mixed between registry-owned services and singleton/DDOL owners.
6. Large world/scatter/fauna/player files remain reliability risks because ownership concentration is too high.

## Documentation Authority

Use this read order for current state:

1. `../AGENTS.md`
2. task-relevant files under `.agents-skills/`
3. `Docs/README.md`
4. `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
5. `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md`
6. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
7. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
8. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/02_SYSTEM_REALITY_MATRIX.md`
9. `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`
10. system-specific docs and source files

`Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` is the current blunt project-level verdict.
It does not replace this file as the system-shape entry point; it summarizes the overall risk/readiness conclusion.

If an older document disagrees with this file and current source, treat the older document as historical until rechecked.

## Current System-Specific Maps

These maps are still useful for domain navigation, but none of them is runtime proof:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PLAYER_GAMEPLAY_CORE_MAP.md` - player-facing gameplay ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` - construction, habitat graph, modules, logistics, and power.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` - tools, interaction routing, scanner/cutter/repair/beacon branches.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md` - survival, damage, pressure, thermal, hazards, and stress consequences.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md` - narrative, scan, lore, Atlas, quest, and progression ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` - UI, HUD, visor, PDA, and audio presentation ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` - world, environment, ocean, debris, ecology, and submarine ownership.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md` - broad owner ledger for major gameplay domains.
- `Docs/PROCEDURAL_ASSET_PIPELINE.md` - procedural asset production contract.
- `Docs/PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md` - procedural category/runtime architecture contract.
- `Docs/QUALITY_GATES.md` - validation gates; checklist only, not proof by itself.
- `Docs/ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md` - readable geology/seismic ownership reference.
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md` - audio DSP architecture reference.
- `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md` - GPU abyssal flow/weather architecture reference; verify shader/console before runtime claims.
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md` - UI zero-GC contract and pattern reference.
- `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md` and `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md` - save container and paging contracts.
- `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md` - habitat graph/logistics contract.
- `Docs/ARCHITECTURE/HEADLESS_ECOSYSTEM_SIMULATION.md` - headless fauna/ecosystem contract.
- `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md` and `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md` - AUP/floating-origin/kinematics standards.
- `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md` - headless drone fleet/native-slot architecture reference.
- `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md` - equipment/tool SOA contract; not prefab/save/UI proof.
- `Docs/ARCHITECTURE/KINETIC_ENTANGLEMENT.md`, `Docs/ARCHITECTURE/ORGANIC_ENTROPY_MATH.md`, and `Docs/ARCHITECTURE/MIGRATORY_FLORA_SYSTEM.md` - organic/flora interaction contracts; re-open source/assets before surgery.
- `Docs/ARCHITECTURE/PROJECT_CONTENT_LEDGER.md` - resource/hash coordination ledger; authored data reference, not scene validation.
- `Docs/ARCHITECTURE/QUEST_DAG_PROTOCOL.md`, `Docs/ARCHITECTURE/REACTIVE_ECONOMY_SYSTEM.md`, and `Docs/ARCHITECTURE/ZERO_GC_FABRICATION.md` - quest, economy, and fabrication contracts.
- `Docs/ARCHITECTURE/SCANNER_DATA_MINING.md`, `Docs/ARCHITECTURE/SUBMARINE_OS_MANUAL.md`, and `Docs/ARCHITECTURE/TRAUMA_GLITCH_SYSTEM.md` - scanner/PDA, submarine OS, and trauma/presentation contracts.
- `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md` and `Docs/ARCHITECTURE/URP_SCREENSHOT_PIPELINE.md` - third-party boundary and save-thumbnail rendering contracts.
- `Docs/AI_Fauna/README.md` - fauna concept and coverage reference; not runtime spawn or prefab proof.
- `Docs/Flora_Pipeline/README.md` - active flora execution bundle; not final asset/import/runtime validation proof.
- `Docs/Legacy_World_Reference/README.md` and `Docs/Legacy_Backlog/README.md` - historical reference only, not current authority.

Line-number evidence inside older maps may drift after source edits. Treat class/file ownership as the useful layer and re-open source before surgery.

## Verification State

- Play Mode: not launched.
- Unity console: MCP unavailable. Local `Editor.log` reached an additional `Tundra build success` after the input asmdef cleanup and then `Mono: successfully reloaded assembly` at line `136460`; scan after that reload found no `error CS*`, `warning CS*`, `CS2001`, `Tundra build failed`, `Resource ID out of range`, or duplicate-GUID markers. Post-reload noise still includes MCP WebSocket/TLS transport lines from `com.coplaydev.unity-mcp`, so this is editor compile evidence only, not a globally clean console.
- MCP: not used for editor mutation or scene operation in this pass.
- Runtime GC: not measured.
- Memory retention: not measured.
- Code compilation: latest local `Editor.log` reached `Tundra build success` and `Mono: successfully reloaded assembly`; this is editor compile evidence, not Play Mode proof.

## Regression Model

CPU: runtime source changed in input rebinding placement, habitat graph stress evaluation, procedural audio event payloads, abyssal thermal habitat coupling, and lore lookup hardening; no profiler numbers were captured.
GC: no new hot-path managed containers or LINQ were added by the reviewed changes; measured 0 B/frame proof is absent.
Memory: no scenes/assets/native container capacities were changed in this pass; runtime retention is unmeasured.
Cadence: `AbyssalThermalManager` now applies habitat thermal infiltration during `SlowTick`; frame-time impact is unmeasured.
Correctness: editor compile blockers were cleared by source review and local `Editor.log`; gameplay behavior remains unverified.

## Hot Path Impact

Non-zero. Runtime source changed, but the intended cadence is cold/lifecycle or `SlowTick`, not per-frame `Tick`. Hot-path profiler proof is absent.

## Failure Modes

- This file will drift if future agents patch systems without updating active docs.
- Runtime verification can contradict static source review.
- Scene/prefab wiring can still contradict code ownership maps.
- Dirty worktree changes by other agents can make this snapshot incomplete without a fresh re-scan.

STATUS: PENDING VERIFICATION
