# HECTON-8 Conceptual System Authority Map

Date: 2026-05-11
Status: PENDING VERIFICATION
Scope: concept-level current system ownership across source and active docs

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

This file is not a file-count ledger.
It is the current conceptual map for what systems exist, which ones carry runtime authority, and which ones must be treated as transitional or target-only.

Source wins over this file.
Runtime proof is absent unless a later Unity/Profiler/MCP report says otherwise.
Current authority starts at `AGENTS.md`, `.agents-skills/README.md`, task-relevant mandates, stable `Docs/*.md` files, current source, and fresh logs.
Current data evidence starts at `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md` and `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
Current presentation doctrine starts at `AGENTS.md`, `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, `.agents-skills/README.md`, and `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`; `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md` is supporting evidence.

## 1. Conceptual Status Model

| Status | Meaning |
|---|---|
| LOAD-BEARING | Current runtime depends on this domain. Breakage here can block play, save, world, UI, or core simulation. |
| ACTIVE BUT TRANSITIONAL | Real implementation exists, but authority is split, legacy patterns remain, or ownership is too concentrated. |
| PRESENTATION / SUPPORT | Important for product feel or tools, but must not be treated as gameplay truth. |
| EXPERIMENTAL / SEAM | Source or docs exist, but this is not the production backbone yet. |
| HISTORICAL / EVIDENCE | Useful for provenance only. Re-open source before using it as current truth. |

## 2. Load-Bearing Runtime Backbone

| Domain | Current conceptual owner | Current status | Read first |
|---|---|---|---|
| Bootstrap / startup | `BootstrapController`, `GameBootstrapper`, `SceneBootstrap` | ACTIVE BUT TRANSITIONAL | `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`, `BUILD_DEPENDENCY_GRAPH.md` |
| Service registry | `GlobalRegistry`, `GlobalRegistryContracts` | LOAD-BEARING | `../02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md`, `INTERFACE_CONTRACT_TABLE.md`, `PROJECT_ATLAS.md` |
| Runtime cadence | `SystemDispatcher`, `GameTickManager` | LOAD-BEARING WITH LOCAL BARRIER RISK | `FRAME_TIMELINE.md`, `DOOMSDAY_FLAW_REPORT.md`, `02_SYSTEM_REALITY_MATRIX.md` |
| Scene loading | `SceneRuntimeService`, bootstrap scene flow | ACTIVE BUT TRANSITIONAL | `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md`, `PROJECT_ATLAS.md` |
| Object pooling | `ObjectPoolManager`, pool budget data | ACTIVE BUT NOT PROJECT-WIDE PROVEN | `AWAITABLE_MEMORY_COMPACTION_SURGERY_LOG.md`, `06_CRITICAL_ACTION_QUEUE.md` |
| Telemetry / crash evidence | `GlobalTelemetryBus`, `CrashTelemetryBuffer` | LOAD-BEARING FOR DEBUG, NOT GAMEPLAY AUTHORITY | `05_EVIDENCE_LEDGER.md`, `DBG_Telemetry_Crash_Reporting_PostMortem.txt` |

Core reality:
- The backbone is real, but not cleanly sovereign.
- `GlobalRegistry` is central, while static `Instance`, `ActiveRuntimeInstance`, and `DontDestroyOnLoad` patterns still exist across many domains.
- `SystemDispatcher` is the intended cadence gate, but multiple systems still decide local job completion timing.

## 3. Gameplay Runtime Domains

| Domain | Current conceptual owner | Current status | Critical boundary |
|---|---|---|---|
| Player movement / suit body | `HectonPlayerMovement`, player runtime context services | LOAD-BEARING BUT OVERLOADED | Movement must not become the owner for inventory, UI, save, or world mutation. |
| Survival / pressure / thermal | `HectonSurvivalSystem`, `HazardZoneManager`, `HectonHazardManager`, `AbyssalThermalManager` | LOAD-BEARING WITH PARALLEL HEALTH BRANCH | `HectonPlayerHealth` remains a separate HP/mutation branch with persistence caveats. |
| Tools / equipment | `PlayerTool`, `PlayerToolManager`, `ModularEquipmentEngine`, `EquipmentInteractionHandler` | LOAD-BEARING BUT SPLIT | Hand input, runtime tool state, and world interaction are separate authorities. |
| Inventory / item data | `PlayerInventory`, `PlayerInventoryManager`, `InventoryGrid`, `ItemTemplateRegistry` | LOAD-BEARING | Template data and runtime inventory state must remain separated. |
| Construction / base | `ConstructionManager`, `HabitatGraphManager`, `BaseModule`, `BaseAirlock`, module scripts | LOAD-BEARING BUT LARGE | Graph/topology, module state, logistics, power, and interaction are not one single system. |
| Submarine / vehicle | `SubmarineCoreDirector`, `HectonSubmarineOS`, structural/hull/power systems | ACTIVE BUT TRANSITIONAL | Runtime context, OS/UI, and physical state must not collapse into one presentation object. |

Gameplay reality:
- The project has real game systems, not just prototypes.
- The largest gameplay risk is not absence; it is mixed ownership.
- Player, tools, construction, survival, and submarine logic are tightly coupled through registry, direct references, save surfaces, and events.

## 4. World / Simulation Domains

| Domain | Current conceptual owner | Current status | Critical boundary |
|---|---|---|---|
| Persistent world state | `PersistentWorldRegistry`, `WorldSpatialHashGrid`, procedural state registries | LOAD-BEARING | Handles and spatial ownership must be deterministic and bounded. |
| Voxel / cave / geology | `HectonVoxelEngine`, `VoxelDeltaProcessor`, `WorldCaveDirector`, geology directors | LOAD-BEARING BUT STALL-PRONE | Chunk generation, collision, nav, and mesh lifecycle need unified job ownership. |
| Scatter / flora placement | `WorldProceduralScatterDirector`, scatter partials/backends | LOAD-BEARING BUT MONOLITHIC | Runtime selection/placement must stay separate from editor preview and authoring shape. |
| MapMagic / vegetation bridge | `HectonMapMagicVegetationBridge` | ACTIVE BUT HIGH COUPLING | Third-party bridge only; do not leak MapMagic policy into core gameplay. |
| Physics / force application | `PhysicsApplySystem`, `GlobalPhysicsStateManager`, `PhysicsEventBus` | Active/Transitional | Contact modification, fixed-step force queueing, and managed collision callbacks coexist; profiler/GC proof is absent. |
| Fauna / ecosystem | `FaunaBrain`, `FaunaDirector`, `EcosystemDirector`, cognition domains | LOAD-BEARING BUT HEADLESS/AUP RISK | Gameplay state must not depend on camera/Animator presentation. |
| Ocean / weather / thermal | `OceanKinematicsRuntimeService`, `GlobalWeatherDirector`, `AbyssalThermalManager`, atmosphere systems | ACTIVE BUT MIXED | Environmental truth must stay service-owned, not visual-effect-owned. |

World reality:
- World systems are materially implemented and heavy.
- They are the highest dependency-gravity area in the codebase.
- The main risk is local ownership of jobs, native memory, spatial handles, and third-party bridge assumptions.

## 5. Presentation / UI / Audio Domains

| Domain | Current conceptual owner | Current status | Boundary |
|---|---|---|---|
| HUD / visor UI | `SuitHUDV4CanvasOverlay`, `TMP_TextRegistry`, `UIStateStore`, visor systems | STRONG PRESENTATION SURFACE | UI can display truth; it must not become truth. |
| PDA / menus | `PlayerPDA`, PDA managers, menu controllers | ACTIVE SUPPORT | UI flow and persistence must stay separate from core progression truth. |
| Audio / DSP | `SpatialAudioManager`, procedural audio renderers, audio event buses | ACTIVE BUT MIXED | DSP direction is real; legacy/static audio managers remain. |
| VFX / camera juice | VFX, visor, camera feedback systems | PRESENTATION / SUPPORT | Must not drive gameplay transitions. |

Presentation reality:
- UI/HUD is comparatively mature.
- The conceptual bug class is presentation-owned gameplay: Animator events, camera availability, and visual objects deciding state.

## 6. Data / Persistence / Content Domains

| Domain | Current conceptual owner | Current status | Boundary |
|---|---|---|---|
| Slot save/load | `SaveManager`, `SaveBinaryStorage`, `SaveEvents`, save participants | LOAD-BEARING | Runtime validation is still required; implementation is not paperware. |
| Meta/profile/input persistence | `GlobalProfileManager`, `RebindingManager`, `UserOptionsPersistence` | ACTIVE SIDE-PERSISTENCE | These are outside the slot-save authority and must be documented separately. |
| Items/resources/templates | ScriptableObject templates, item/resource registries, content validators | ACTIVE DATA CONTRACT | Runtime must not mutate authoring templates directly. |
| Content ledgers | `PROJECT_CONTENT_LEDGER.md`, item GUID/hash docs | ACTIVE EVIDENCE | Useful for mapping content; not a runtime integrity proof by itself. |

Persistence reality:
- Save is a serious system, but it is not the only persistence surface.
- Treat "save/load is real" and "save/load is verified" as different claims.

## 7. Experimental / Transitional / Non-Authority Surfaces

| Surface | Current conceptual classification | Handling |
|---|---|---|
| DOTS / Entities backend | Experimental Seam | `com.unity.entities` is not in current `Packages/manifest.json`; `World/Dots` is gated placeholder scaffolding, not production ownership. |
| Networking | EXPERIMENTAL / PLACEHOLDER | Do not let network docs imply production multiplayer readiness. |
| Modding API | ACTIVE BOUNDARY / NOT CORE AUTHORITY | Useful as external-facing seam; must not drive internal engine ownership. |
| Runtime smoke testers / verifiers | QA SUPPORT | Useful for proving flows; strict coroutine usage is source-migrated, but they are still not production gameplay and still need Play Mode proof. |
| Editor tooling | SUPPORT / VALIDATION | Can enforce data rules; must not be treated as runtime behavior. |
| Deprecated / archive docs | HISTORICAL / EVIDENCE | Preserve for provenance; never read first. |

## 8. Current Conceptual Truths To Preserve

- The project is no longer a missing-feature prototype. It is a heavy, partially integrated runtime with dependency-gravity problems.
- The strongest systems are not automatically safe systems. UI, save, world, and construction all contain real implementation weight, but still need runtime verification.
- The weakest conceptual claim is "one architecture." The actual architecture is a layered hybrid: registry + dispatcher + singletons + DDOL + direct references + event buses.
- The biggest production risk is not one bug. It is cross-system authority drift: multiple owners believing they are the source of truth for state, cadence, persistence, or presentation.
- Large files are now risk multipliers. `HectonMapMagicVegetationBridge`, `WorldProceduralScatterDirector`, `HectonPlayerMovement`, HUD, save, voxel, fauna, and audio owners should be treated as Class-A review targets.
- Documentation must describe current runtime ownership, not aspirational target architecture.

## 9. Conceptual Read Path By Work Type

| Work type | Read path |
|---|---|
| Any code work | `AGENTS.md` -> relevant `.agents-skills/*` -> this file -> source owner |
| Bootstrap/service work | `PROJECT_ATLAS.md` -> `2026-04-30_BOOTSTRAP_RUNTIME_AUTHORITY_TRUTH.md` -> `../02_ACTUAL_REPORTS/INTERFACE_HEALTH_DASHBOARD.md` |
| Player/tools work | `PLAYER_GAMEPLAY_CORE_MAP.md` -> `TOOLS_INTERACTION_OPERATIONAL_SYSTEM_MAP.md` -> source |
| Construction/base work | `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` -> `HABITAT_LOGISTICS_GRAPH.md` -> source |
| Survival/hazard work | `SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md` -> survival/hazard source |
| World/scatter/flora work | `WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md` -> `DOOMSDAY_FLAW_REPORT.md` -> source |
| Save/persistence work | `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` -> `SAVE_V8_BINARY_SPEC.md` -> source |
| UI/audio work | `UI_AUDIO_PRESENTATION_SYSTEM_MAP.md` -> `AUDIO_DSP_PIPELINE.md` / `ZERO_GC_UI_PIPELINE.md` -> source |
| Cleanup/deprecation work | `DOC_AUTHORITY_CLASSIFICATION.md` -> `DOCSET_COVERAGE_MATRIX.md` -> target docs |

## 10. Regression Model

CPU: no runtime code changed.
GC: no runtime code changed.
Memory: no runtime data, native containers, scenes, or assets changed.
Cadence: no runtime cadence changed.
Correctness: improved because future work has a concept-level authority map and less pressure to infer current architecture from old audit snapshots.

## 11. Hot Path Impact

None. Markdown-only update.

## 12. Failure Modes

- This map will drift if source changes without doc updates.
- Scene and prefab wiring can still contradict class-level authority.
- Runtime verification can contradict static source interpretation.
- Historical docs may still contain true local evidence, but their global conclusions can be obsolete.

STATUS: PENDING VERIFICATION
