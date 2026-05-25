# Global Authority Migration Ledger

Date: 2026-05-24
Status: PENDING VERIFICATION
Evidence class: STATIC_SOURCE / STATIC_DOC; CLI_COMPILE only where a build log is cited.
Authority parent: `GLOBAL_AUTHORITY_BOUNDARIES.md`

Full historical ledger snapshot: `../_Archive/Architecture_X_012_APEX_2026-05-24_FILE_CAP/ARCHITECTURE_APEX_PRE_FILE_CAP_GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`.

## Current Contract

Goal: shrink uncontrolled global authority without replacing it with hidden owner ambiguity.

Allowed global routes:

| Route | Allowed use | Stop condition |
|---|---|---|
| `GlobalRegistry` | cold identity, bootstrap, dependency injection | no hot-path live polling |
| `SignalBus<T>` | first-party hot unmanaged broadcast | owner, phase, capacity, overflow, telemetry documented |
| `GlobalSignals` | retained legacy bridge lanes only | each retained lane has owner and migration state |
| `HectonEventBus` | mod/API/cold managed isolation | no first-party hot gameplay traffic |
| `GlobalDataVault` | cross-domain native ownership | `BufferID`, `SystemID`, generation, lifetime, stale-handle behavior documented |
| dispatcher barriers | owned phase completion windows | no undocumented same-frame `.Complete()` |

## Current Static Snapshot

| Surface | Snapshot | Use |
|---|---:|---|
| raw `GlobalRegistry.` source lines | `6179` | historical HFI snapshot; rerun before gate use |
| raw event/signal publish token hits | `890` | historical HFI snapshot |
| `GlobalSignals.cs` `NativeQueue<...>` refs | `115` | historical HFI snapshot |
| `GlobalSignals.cs` configure/init hits | `271` | historical HFI snapshot |
| native collection line hits | `23375` | historical HFI snapshot |

## Active Migration Streams

| Stream | First action | Required proof |
|---|---|---|
| Registry surface | classify top hot-path registry readers | hot path uses cached owner/interface or fails closed |
| Signal lanes | classify hot `HectonEventBus` and `GlobalSignals.Publish` sites | retained lanes have route cards |
| Direct queues | inventory `GlobalSignals.cs` queues | owner/capacity/overflow/telemetry per retained queue |
| DataVault sovereignty | migrate owner-blocked native collections by domain | no lifecycle regression, stale-handle behavior named |
| Dispatcher barriers | map `.Complete()` and drains | completion window owned by dispatcher/phase |
| Evidence language | attach current proof artifacts | claims do not exceed proof class |

## 2026-05-24 External Codex Scope

- Cleanup loops55-142 summary:
  - loops55-114: hot global tails removed without new routes.
  - loop115: UI/crafting/scavenging owner-cache tails and `PlayerInventoryManager` read-accessor scene sync removed.
  - loop116: Construction/Lore/Seam/LOD/DynamicResolution Save owner tails removed.
  - loop117: `CaveBioRootsGenerator` spline-renderer owner tail removed.
  - loop118: `BuoyancyObject` FluidRuntime owner tail removed.
  - loop119: Dispatcher hot-swap rebinds, bound Save registration helpers, owner-correct DataVault swaps across 20 runtime owners.
  - loop120: automatic interaction scene scan removed; remaining runtime interactables registered explicitly.
  - loops121-129: dispatcher/DataVault/read-model tails and tick-list probes removed across PathFunnel, AmbientWaterMotion, Atlas, Core/player/UI/tool, cave voxel lighting/AO, and 40 runtime owners.
  - loop130: non-editor register/probe grep surface zeroed; `Build_EXTERNAL_CODEX_hotpath_cleanup129_registration_probe_zero.log` classified ENV/ACCESS_DENIED.
  - loop131: one duplicate include target tail removed.
  - loop132: editor DLL output reached; one environment/cache warning; no C# diagnostics.
  - loop133: non-`this` static-driver/renderable registration residues removed.
  - loop134: info-only release log callsites stripped to `H8Debug.Log`; remaining Save/Steam/Ecosystem/Foveated frost/render membership probes removed.
  - loop135: HectonVoxelVolume sonar DataVault runtime polls moved to cached owner/hot-swap.
  - loop136: PerformanceBudgetController Dispatcher replacement rebind restored.
  - loop137: EntityChangeManager, LandingImpactVFX, PlayerStressMetricsRuntime, and RenderTextureLifecycleTracker Dispatcher replacement rebind restored.
  - loop138: VoxelDynamicNavGridRuntimeLifecycle, InstanceCullingServiceRegistryBridge, HectonSuitHUDExtensions, GCMonitor, and MeteorSplashQuadVfx Dispatcher replacement rebind restored.
  - loop139: 71 additional info-only runtime `Debug.Log` callsites stripped to conditional `H8Debug.Log`.
  - loop140: Environment/Ocean runtime context getters made pure cached reads; RaycastBatch late-frame rebind restored.
  - loop141: 63 executable smoke/diagnostic/runtime-support info logs plus 2 comments stripped to conditional `H8Debug.Log`.
  - loop142: remaining non-editor raw info `Debug.Log` surface outside `H8Debug.cs` stripped to conditional `H8Debug.Log`; 8 root editor proof tools cleaned.
  - loop143: ten cadence/context owners gained Dispatcher/service hot-swap rebind; PlayerSensory getters are pure cached reads.
  - loop144: fourteen tool/pipe/replay/demo/flora/celestial owners gained Dispatcher/DataVault/service hot-swap rebind/cache refresh; no-hot-swap candidate count is 47.
  - loop145: four spline/highlight/transport owners gained Dispatcher hot-swap rebind; no-hot-swap candidate count is 43.
  - loop146: four interaction/door transient tick owners gained Dispatcher hot-swap rebind without clearing pending work; current no-hot-swap candidate count is 27.
  - loop147: delayed despawn and GI relay gained Dispatcher/DataVault/service hot-swap coverage; current no-hot-swap candidate count is 24.
  - loop148: thirteen cadence/render/UI/physics/geology owners gained Dispatcher hot-swap rebind; current no-hot-swap candidate count is 13.
  - loop149: topographical sonar, GPU Jacobian foam, and indirect vegetation gained Dispatcher/DataVault/player hot-swap coverage.
  - loop150: marauder outpost/trade, vehicle damage, submarine dynamics, and abyssal thermodynamics gained Dispatcher/DataVault/service hot-swap coverage; type-aware no-hot-swap scan leaves 4 infra/QA/tool owners.
  - loop151: `PlayerBuilder`, `RepairTool`, `HeadlessStressFractureBot`, `SteamManager`, `MantaEmergencyWreck`, `AbyssalCavitationRuntimeHost`, `TerrainChunkPagerRuntime`, and `HectonUIScaler` gained real Dispatcher/DataVault/service hot-swap coverage; targeted hot-swap greps pass in touched scopes.
  - loop152: `PersistentWorldRegistry` tombstone day resolution reads cached `ISaveService` plus Save hot-swap state instead of `GlobalRegistry.Save` from a static helper.
  - loop153: `PersistentWorldRegistry` hydration and item-catalog helpers read cached `IPlayerRuntimeContext`/`IPlayerInventoryService` plus hot-swap state instead of direct Player/PlayerInventory registry reads.
  - loop154: 12 short UI/audio/construction owners unregister/re-register on Dispatcher hot-swap instead of keeping stale registration flags; `PDADeathMemoryDump` reads cached Player context instead of direct `GlobalRegistry.Player`.
  - loop155: `FontStreamingManager`, `LocalizedTextMadnessFx`, PDA tab labels, `PDALoadoutTab`, `SubtitleManager`, and `LoadingScreenController` gained the same Dispatcher unregister/re-register path; three prior UI owners gained null-Dispatcher local reset.
  - loop156: 15 remaining UI/construction owners gained local lane reset or unregister/register rebinds before Dispatcher replacement registration.
  - loop157: UI/Construction runtime singleton tails now route through `GlobalRegistry` cold cache and existing hot-swap state.
- Scope: binary scalability event/tier tails outside Core bridge.
- Scope: beacon/construction action fanout and BeaconNetwork `GetOrCreate` registry fallback.
- Scope: SDF/Terrain `?? GlobalRegistry` fallbacks.
- Scope: `ConstructionManager` ObjectPool/PlayerInventory/DataVault action-path reads.
- Broad scope:
  - callback, physics, audio, structural integrity, organic/hull/voxel, combat, suit/loot/vehicle, ladder, player/VR.
  - DebrisManager, SomaticKinematicsRuntime, ChemicalInfluenceGrid, FloraInteractionManager, hazard/reactor/habitat.
  - VehicleMotor, HazardZoneManager, SettingsManager, PlayerActionController, Flora Player/Atmosphere/Construction.
  - ConsumableItem, ClimbableLadder, ecosystem Save, StorageCrate, Sargassum Save, OxygenBubble, Floater.
  - WorldState/WorldProcedural/FaunaDirector/AtlasSignal Save, HectonPlayerHealth, MessageTerminal, TraumaDispatcher.
  - Narrative/Suit/PDA/Inventory Save, FirstHourDirector Save, DataArchaeologyRuntime Save.
  - CorporateOrderSystem, ProceduralLoreDirector, MetaCampaignService, RunModifierController, ModWorldPersistenceManager, PlayerExpressionManager.
  - GlobalProfileManager, DynamicDifficultyDirector, HectonDiscoveryManager, PlayerExplorationTracker, PDAMarkerRegistry, PDALogbookManager.
  - PlayerAchievementRegistry/PDAContextualAdvisorySystem owner-cache cleanup.
  - Runtime SaveRuntime interface tails, CrashTelemetryBuffer save presence, concrete UI tails, bootstrap/diagnostic SaveRuntime tails.
  - Save owner tails, spline-renderer/FluidRuntime owner tails, dispatcher/save/DataVault rebind tails, interaction registry scene-scan removal.
  - FaunaBrain physics determinism compile fix.
- Last zero-warning proof: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.
- Runtime proof remains pending.

## Required Audit Commands

```powershell
rg -n "GlobalRegistry\." Assets/_Project/Scripts
rg -n "GlobalSignals\.Publish|HectonEventBus|SignalBus<.*>\.(Push|TryPush)" Assets/_Project/Scripts
rg -n "\.Complete\(" Assets/_Project/Scripts
rg -n "GlobalDataVault\.TryGetLatestCreated|TryGetLatestCreated\(" Assets/_Project/Scripts
```

## Non-Claims

This file is not Unity import proof, Play Mode proof, profiler proof, GC proof, player-build proof, or route approval.
