# Global Authority Boundaries

Date: 2026-05-24
Status: PENDING VERIFICATION
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Evidence class: STATIC_DOC / STATIC_SOURCE / CLI_COMPILE where artifact cited

Scope: allowed routes for global authority surfaces.

## Surfaces

| Surface | Allowed use | Rejected use |
|---|---|---|
| `GlobalRegistry` | cold bootstrap identity, service registration, dependency injection, stable owner lookup | hot polling, mutable gameplay state, event bus, scene search replacement |
| `SignalBus<T>` | first-party hot broadcast with unmanaged payloads, bounded capacity, deterministic overflow, telemetry | request/response, one-private-caller events, managed payloads, unbounded queues |
| `GlobalSignals` direct queues | legacy bridge lanes and low-level queue infrastructure during migration | new gameplay traffic, catch-all queue expansion, undocumented lane growth |
| `HectonEventBus` | mod/API/cold managed isolation and watchdog-protected extension events | first-party hot gameplay traffic, Burst/job data flow |
| `GlobalDataVault` / `IDataVault` | cross-domain native ownership, generation-checked handles, persistent shared snapshots, relocation/defrag ownership | global heap, private scratch replacement, unowned persistent allocations |

## Current Static Source Counters

| Counter | Value |
|---|---:|
| `SignalBusRegistry.LaneCapacity` | 512 |
| `ClearPostSimulation` hits under `Core/Signals` | 141 |
| `NativeQueue<T>` hits in `Core/Signals/GlobalSignals*.cs` | 35 |
| mod/API public signal-denial count | 160 |

The `160` value is not the total active signal count. It is a mod/API boundary fact.

## Route Rules

1. Pick one route before coding: cold lookup, hot broadcast, persistent shared memory, mod event, telemetry, or debug.
2. Cache registry-resolved interfaces outside hot paths.
3. Publish runtime facts once from the owning phase.
4. Consumers read immutable snapshots, generation-checked handles, or typed signal payloads.
5. Read accessors named `Get*`, `TryGet*`, `Resolve*`, or `Read*` must be pure.
6. Read accessors must not publish, allocate, grow buffers, search scenes, complete jobs, or mutate global state.
7. New signal lanes require owner, producer phase, consumer phase, capacity, overflow policy, retention policy, payload layout, duplicate-name scan, and telemetry route.
8. New DataVault buffers require `BufferID`, `SystemID`, length/capacity, generation handling, release behavior, stale-handle behavior, and dump behavior.

## 2026-05-24 External Codex Notes

Hot-swap callbacks:

- Valid use: cache refresh for service replacement.
- Rejected use: gameplay event transport, per-frame retry loops, scene-search fallback.
- Service-replacement callbacks must consume `currentService` or cached owner state.

Continuous quality route:

- Required source: `HomeostasisBrain.GlobalQualityWeight`.
- Rejected sources: `GlobalRegistry.ScalabilityTier`, `ScalabilityChangedEvent`.
- Affected presentation/generation domains:
  - contextual physical IK; procedural wrecks; diegetic UI; indirect vegetation;
  - loot; flora/organic; trade; active sonar; seismic shader-shake; GPU scatter;
  - toxic chemistry; thermodynamics; somatic CCD; GI relay fallback; memory sentinel;
  - tether visual/solver; voxel carve-drain budgets; submarine sonar LOD; scanner quality;
  - gyro compass presentation; interior GI resolution; player brine/focus presentation;
  - bootstrap vault/math LOD; DRS policy; player kinematics; submarine fluid; hydro KCC.
- Removed dead listeners: drill/lockstep, player motor/movement, ballast, scanner, gyro, interior-GI, player kinematics, submarine-fluid, hydro-KCC, DRS no-op/dead binary scalability listeners.
- Loop59 grep: non-editor/non-Core bridge runtime hits for `GlobalRegistry.ScalabilityTier*` and `ScalabilityEvents.Register/Unregister` are empty.

Registry/DataVault route fixes:

- Beacon static action helpers and `GetOrCreate`: use active runtime pointer; registry writes only in owner lifecycle/recovery.
- Construction blueprint scans: may use cached `IQuestSystem` overloads.
- SDF/Terrain probe helpers: read cached owner fields; reject hot `?? GlobalRegistry.VoxelSonarSdf/Terrain` fallback.
- `ConstructionManager`: deconstruction/load/clear/save-catalog/telemetry paths read cached ObjectPool/PlayerInventory/DataVault refs with hot-swap refresh.
- Structural integrity init: register DataVault hot-swap before init; reject retrying `GlobalRegistry.DataVault` inside `TryInitialize`.
- Organic/hull/voxel diagnostic DataVault release/dump/bootstrap paths: use cached owner vaults.
- Signal producers: publish through their payload owner lane.

Recent loop additions:

- Loop69 `ScannerDataMiningRouter`: instance DataVault access uses cached owner vault only. `GlobalRegistry.DataVault` is allowed only for cold cache/static settings helpers. Runtime replacement flows through hot-swap rebind after query/completion buffers unlock.
- Loop70 `HectonFloatingOrigin`: AUP tuner/static facades must not fall back from live owner cache to `GlobalRegistry.DataVault`. Registry fallback is allowed only when no floating-origin owner exists.
- Loop71 combat runtime: ballistics, status effects, and armor penetration consume the combat-owned DataVault cache/hot-swap state. `?? GlobalRegistry.DataVault` is rejected in combat init/rebind paths.
- Loop72 runtime fallback cleanup: MathGuard, static data stores, Babel dictionary, and SignalWarden crash dump must not use `?? GlobalRegistry` fallback routes.
- Loop73 `AsynchronousTelemetryExporter`/bootstrap: analytics storage acquisition uses cached DataVault only with worker-safe rebind; bootstrap fallback must use explicit cold null checks instead of `?? GlobalRegistry` patterns.
- Loop150 owners: core, bootstrap, QA, world, narrative, visor, voxel.
- Service-lane callbacks rebind Dispatcher lanes from `currentService`.
- DataVault replacements release or clear old native handles before reacquire.
- Static voxel deferred drivers use one hot-swap bridge instead of per-frame Dispatcher polling.
- File-level residuals are partial-class false positives only.
- Loop74 suit/loot/vehicle: suit resolver/telemetry, loot magnet dependency snapshots, and vehicle vault helpers read cached owner state only after cold cache or hot-swap rebind.
- Loop75 `ProceduralLadderClimbRuntime`: climb-start DataVault/player/movement dependencies read cold cached owner state; DataVault/player/movement replacement flows through hot-swap rebind.
- Loop76 player/VR: player kinematics and VR somatic DataVault resolvers read cached owner state only; registry access is cold cache before hot-swap registration.
- Loop77 debris: debris vault allocation must not retry DataVault registry after hot-swap registration; DataVault replacement releases old handles before new-vault rebind.
- Loop78 somatic kinematics: weather/VR service rebind must not also poll DataVault; DataVault moves only through cold cache or the DataVault hot-swap slot.
- Loop79 chemical/flora: runtime vault resolvers read cached owner vaults only.
- Flora `OnEnable` refreshes DataVault before resolver calls and clears generation handles on vault change.
- Queued wake-trail globals must publish instead of self-queue.
- Loop80 hazard/reactor/habitat: EnvironmentalHazard and BioReactor player reads use cached player context; HabitatIntegrityManager FluidDecals/Atmosphere/Terrain reads are cold cached and hot-swap refreshed before slow/action use.
- Loop81 vehicle wake-silt: VehicleMotor wake silt emission reads cached `AbyssalFluidDecalManager`; direct `GlobalRegistry.AbyssalFluidDecals` is cold cache only.
- Loop83 HazardZoneManager: player exposure fallback reads cached `IPlayerRuntimeContext`; `GlobalRegistry.Player` is cold cache only.
- Loop84 settings UI: graphics camera/volume binding reads cached player context only; Player replacement invalidates player-owned camera/volume cache through hot-swap.
- Loop85 VR somatic: player camera fallback reads cached `IPlayerRuntimeContext`; Player replacement refreshes camera owner through hot-swap.
- Loop86 player kinematics: Fluid/Voxel/Gas/PlayerMotor/Player camera recovery reads are cold cache only; hot-swap replacement uses `currentService` and cached player context.
- Loop87 RepairTool: hull-dent/black-box DataVault handles rebind through PlayerTool DataVault hot-swap; direct DataVault registry read is cold cache only.
- Loop88 EnvironmentalHazard: damage interrupt uses cached `IPlayerActionInterruptSink`; direct PlayerAction registry read is cold cache only.
- Loop89 PlayerActionController: inventory removal and completion/cancel audio read cached `IPlayerInventoryService`/`IAudioService`; direct PlayerInventory/Audio registry reads are cold cache only.
- Loop90 FloraInteractionManager: player tool/AUP/toxic-spore, parasite day-length, and fungal spread helpers read cached Player/Atmosphere/Construction owners; direct registry reads are cold cache only.
- Loop91 ConsumableItem: use-sound playback receives caller-owned `IAudioService`; static consumable utility must not read `GlobalRegistry.Audio`.
- Loop92 ClimbableLadder: climb audio and interact text localization read cached Audio/Localization owners; direct registry reads are cold cache only.
- Loop93 ecosystem save owners: FaunaGenetics/EcosystemHealth/EnvironmentalStrain register via cached `ISaveService` and rebind on Save hot-swap; direct lifecycle Save registry calls are rejected.
- Loop94 StorageCrate: open/close audio and interact text localization read cached Audio/Localization owners; direct registry reads are cold cache only.
- Loop95 SargassumGlobalDragManager: save registration/unregistration routes through cached `ISaveService`; Save replacement unregisters the previous owner before current-owner bind.
- Loop96 OxygenBubble: collection audio and pool despawn read cached Audio/ObjectPool owners; direct registry reads are cold cache only.
- Loop97 Floater: pickup/attach audio and interact text localization read cached Audio/Localization owners; direct registry reads are cold cache only.
- Loop98 WorldState/WorldProcedural/FaunaDirector/AtlasSignal: save registration/unregistration routes through cached `ISaveService`; Save replacement unregisters the previous owner before current-owner bind.
- Loop99 HectonPlayerHealth: survival heartbeat audio and radiation advisory AudioLog routing read cached Audio/AudioLog owners; direct registry reads are cold cache only.
- Loop100 MessageTerminal: access/new-message audio and localized prompts read cached Audio/Localization owners; WFC datapad state uses `SignalBus<WfcOutpostStateChangedSignal>` and status-light MPB writes flush in late frame.
- Loop101 TraumaDispatcher: parasite-room acoustic load and EMP PDA corrosion read cached Audio/Localization owners; direct registry reads are cold cache only.
- Loop102 HectonNarrativeDirector/SuitUpgradeManager/PDAExchangeSystem/PlayerInventory: save registration/unregistration routes through cached `ISaveService`; Save replacement unregisters the previous owner before current-owner bind.
- Loop103 FirstHourDirector: save registration/unregistration routes through cached `ISaveService`; Save replacement unregisters the previous owner before current-owner bind.
- Loop104 DataArchaeologyRuntime: save registration/unregistration routes through cached `ISaveService`; Save replacement unregisters the previous owner before current-owner bind.
- Loop105 CorporateOrderSystem/ProceduralLoreDirector/MetaCampaignService: save registration routes through cached `ISaveService`; ProceduralLore caches PlayerExploration/AudioLog/ObjectPool owners and despawns lore placements through their owning pool.
- Loop106 RunModifierController/ModWorldPersistenceManager/PlayerExpressionManager: save registration routes through cached `ISaveService`; RunModifier keeps cached concrete `SaveManager` only for slot delete/name API.
- Loop107 GlobalProfileManager/DynamicDifficultyDirector: run-time Save and Discovery reads use cached owners plus Save/DiscoveryRuntime hot-swap; cold cache helpers are the only remaining registry reads.
- Loop108 FaunaBrain: compile-wall fix imports `Hecton8.Physics` for the existing deterministic KCC velocity facade.
- Loop109 HectonDiscoveryManager/PlayerExplorationTracker/PDAMarkerRegistry/PDALogbookManager: save registration routes through cached `ISaveService` and Save hot-swap.
- Loop110 PlayerAchievementRegistry/PDAContextualAdvisorySystem: progression/advisory save registration routes through cached `ISaveService`; PDA advisory hot-swap unregisters through the cached owner.
- Loop111 Runtime SaveRuntime interface tails: audio-log/beacon/scarcity/pause/save-station/PDA clock/ending/crash telemetry/world residency use save interfaces where no concrete SaveManager API is required.
- Loop112 MainMenuController/SaveSlotHoverPreview: menu save metadata binds concrete `SaveManager` through `GlobalRegistry.Save` plus Save hot-swap, not `SaveRuntime`.
- Loop113 Bootstrap/diagnostics: GameBootstrapper and save smoke/verifier code bind concrete `SaveManager` from `GlobalRegistry.Save`; `SaveRuntime` remains only compatibility/self-check surface.
- Loop114 ScanLogSystem/RadioisotopeThermalGenerator: save participant registration/unregistration uses cached `ISaveService` plus Save hot-swap.
- Loop115 UI/crafting/scavenging: refresh/action owner reads use cold cached owners plus hot-swap; PlayerInventoryManager getters stay pure.
- Loop116 Construction/Lore/Seam/LOD/DynamicResolution: save participant registration uses cached `ISaveService`; missing Save hot-swap and disabled-owner unregister gaps are closed.
- Loop117 CaveBioRootsGenerator: cave-root spline submit/remove uses cached `IConnectionSplineBatchRendererService`; renderer replacement removes old links before rebinding.
- Loop118 BuoyancyObject: FluidRuntime registration uses cached `IBuoyancyObjectRegistry` plus FluidRuntime hot-swap, not enable-path `GlobalRegistry.TryGet`.
- Loop119 dispatcher/save lifecycle: 20 UI/world/visor/AI/runtime owners rebind tick/late-frame/slow lanes on Dispatcher replacement; Survival/AudioLog Save participants use bound `ISaveService`; DataVault swaps release old handles where needed.

Evidence boundary:

- Last zero-warning compile PASS: `Docs/AgentLogs/Build_EXTERNAL_CODEX_hotpath_cleanup122_tick_registration.log`.
- Loops66-148 source fixes:
  - warning causes, owner-cache leaks, interaction scene scan.
  - Atlas read-model/DataVault read, Dispatcher rebind, duplicate-source target.
  - slow/updatable/fixed/late-frame registration probe causes.
  - static-driver/renderable residues, info-only release log callsites.
  - loop139: 71 additional selected info-only runtime logs compile out through `H8Debug.Log`.
  - loop140: Environment/Ocean context getters are pure cached reads; RaycastBatch late-frame rebinds after Dispatcher replacement.
  - loop141: 63 executable smoke/diagnostic/runtime-support info logs plus 2 comments compile out through `H8Debug.Log`.
  - loop142: non-editor raw info `Debug.Log` is zero outside `H8Debug.cs`; 8 root editor proof tools also use `H8Debug.Log`.
  - loops143-148: forty-seven additional cadence/context/tool/pipe/replay/highlight/transport/interaction/render/physics/geology owners rebind after Dispatcher/DataVault/service replacement.
  - HectonVoxelVolume sonar DataVault polls.
- PerformanceBudgetController and cadence/context rebind losses covered through loop151.
- Persistent-world Save/Player/Inventory owner-cache ownership covered through loop153.
- UI/audio/construction Dispatcher unregister/re-register or lane-reset tails covered through loop156.
- UI/Construction runtime `PlayerRuntimeContextService.ActiveRuntimeContext` and `LocalizationManager.ActiveRuntimeInstance` tails removed in loop157.
- Targeted greps pass; broad scans retain known false positives.
- Loop130: old non-editor runtime register/probe grep zeroed.
- Loop131: one duplicate include target tail removed.
- Loop132: editor DLL output reached with one environment/cache warning.
- Loop136: source-only under CPU/compiler guard.
- Runtime, profiler, and GC proof remain absent.

## Compile-Wall Guards

Runtime domain assemblies must not reference sibling runtime assemblies for gameplay data flow. Use one of these routes:

- a contract interface in `Hecton8.*.Contracts`;
- a typed unmanaged `SignalBus<T>` payload;
- a generation-checked `GlobalDataVault` handle;
- a cold `GlobalRegistry` lookup cached during boot.

Rejected:

- arrays of interfaces in hot paths;
- registry lookup inside `Update`, `FixedUpdate`, job execution, culling, or solver loops;
- hidden same-frame `JobHandle.Complete()` readbacks;
- read accessors that allocate, publish, search scenes, sync transforms, grow native buffers, or mutate global state.

Static source scan on 2026-05-21 found:

- six interface-array declarations;
- sixty files containing both `GlobalRegistry` and frame-loop method names.

These are triage hits, not proof of hot-path misuse. Each owner must review method scope before claiming compliance.

## Required Playbook

Use these files before changing global routes:

- `GLOBAL_AUTHORITY_SETUP_PLAYBOOK.md`
- `GLOBAL_AUTHORITY_ROUTE_CARD_TEMPLATE.md`
- `GLOBAL_AUTHORITY_REVIEW_CHECKLIST.md`
- `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

## Non-Claims

Evidence limit: runtime lane wiring, scene setup, overflow behavior, job safety, GC state, and profiler cost remain unproven.
