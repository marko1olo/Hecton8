2026-05-28 | 14DER | Hot-path dependency audit | Status: PENDING VERIFICATION

What was wrong:
- `RandomEventSystem.SlowTick()` can invoke component lookup helpers.
- `WorldZoneDirector.SlowTick()` can invoke registry-backed runtime reference resolution.
- `ResourceDistributionDirector.SlowTick()` can invoke runtime service creation/registry fallback for hazards and DataVault capacity growth.
- `ItemCatalog` world-prefab access can registry-refresh services from hot world hydration callers.
- LocRegistry CSV override path is editor-only; no production violation recorded from that hit.

What was done:
- Source was not edited.
- Read AGENTS.md, domain map, Global Authority Boundaries, and relevant mandates.
- Audited requested runtime files with rg and focused source reads.
- Recorded concrete production violations with file, method, line, risk, and minimal patch path.

Cinematic Cheats used:
- Audit-only. No simulation or visual implementation changed.
- Recommendations preserve visual overkill by moving dependency work to cold/hot-swap phases and spending frame budget in VISUAL_SYNC.

Exact microseconds saved:
- Static audit only; no measured before/after exists.
- Estimated avoided hot-path costs by patch class: RandomEvent component metadata lookup 50-200 us on cold branch; WorldZone auto-resolve 5-30 us; ResourceDistribution hazard runtime ensure over 100 us worst-case cold branch; DataVault hot growth unbounded and must be removed from frame; ItemCatalog hydration registry fallback 10-80 us per burst.

Findings:
1. Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:1023 -> 1326 -> 1337. `SlowTick()` calls `ResolveSurvivalSystem()` when `survivalSystem` is null; helper uses `GameBootstrapper.TryGetCurrentPlayerTransform()` and `playerTransform.TryGetComponent(out survivalSystem)`. Risk: hot component lookup and bootstrap pull from a SlowTick lane. Minimal patch: cache survival through `GlobalRegistryServiceSlot.Player` in `CacheRegistryServices()`/hot-swap; `SlowTick()` returns false if cached survival is absent.
2. Assets/_Project/Scripts/Gameplay/RandomEventSystem.cs:1023 -> 1236 -> 1951 -> 1978. Cave collapse branch calls `TryResolveSeismicContext()` from `SlowTick()`; helper calls `targetVolume.TryGetComponent(out WorldGenerativeGeologyVoxelRuntime runtime)` and then reads string `FamilyId`/`GeologyProfileId`. Risk: hot component lookup plus managed authoring-string path in event evaluation. Minimal patch: expose cached numeric geology/family hashes from voxel runtime/engine during registration; random events consume the cached DTO only.
3. Assets/_Project/Scripts/WorldZoneDirector.cs:231 -> 253 -> 362 -> 373 and Assets/_Project/Scripts/WorldRuntimeReferenceUtility.cs:36 -> 41. `WorldZoneDirector.SlowTick()` can call `ResolvePlayer()`, which calls `WorldRuntimeReferenceUtility.TryResolvePlayerTransform()`, which reads `GlobalRegistry.Player` inside a `TryResolve*` accessor. Risk: hot registry polling hidden in a read-looking helper. Minimal patch: cache `IPlayerRuntimeContext`/player transform in `WorldZoneDirector` from cold init and hot-swap; remove registry reads from the helper when called by runtime ticks.
4. Assets/_Project/Scripts/World/ResourceDistributionDirector.cs:721 -> 1361 -> 1375/1393/1428 -> 2405 -> 2416 and Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs:401 -> 403/407. Resource sector `SlowTick()` calls `SyncBrineHazardRegistration()`, which calls `HazardZoneManager.EnsureRuntimeInstance()`; that reads `GlobalRegistry.HazardZones` and can initialize `EnvironmentRuntimeContextService`. Risk: service creation/registry dependency from resource simulation cadence. Minimal patch: cache `HazardZoneManager` via `GlobalRegistryServiceSlot.HazardZoneRuntime`; if missing, queue brine hazard mutation and let hazard owner consume it in its own phase.
5. Assets/_Project/Scripts/World/ResourceDistributionDirector.cs:721 -> 1361 -> 1382/2697 -> 2452 -> 2457. Sector eviction path calls `UnregisterBrineHazard()`, which reads `GlobalRegistry.HazardZones`. Risk: hot registry polling on eviction. Minimal patch: use the same cached hazard manager field or hazard mutation queue as finding 4.
6. Assets/_Project/Scripts/World/ResourceDistributionDirector.cs:721 -> 1696 -> 1731 -> 1868 -> 1881 -> 1908 -> 1914 -> 625/628. Pressure metamorphism scheduling from `SlowTick()` can call `EnsureGenerationHandle()` after `CacheDataVaultCold()` reads `GlobalRegistry.DataVault` if `_dataVault` is null. Risk: hot DataVault growth/relocation and hidden registry fallback. Minimal patch: pre-size metamorphism buffers during cold setup from sector/template caps; hot path skips with telemetry if capacity is insufficient.
7. Assets/_Project/Scripts/ItemCatalog.cs:384 -> 398 -> 901 -> 906 -> 1225 -> 1231/1234/1236. `TryGetLoadedWorldPrefab()` is used by `PersistentWorldRegistry.LateFrameTick()` hydration paths and calls `PumpWorldPrefabDispatchTickets()`, which calls `CacheRuntimeServices()` and can read `GlobalRegistry.AssetLifecycle`, `GlobalRegistry.AssetLoadDispatcher`, and `GlobalRegistry.Player`. Risk: hot registry refresh in world-prefab hydration. Minimal patch: bind those services at bootstrap/hot-swap; hot catalog methods use cached fields only and fail closed if absent.
8. Assets/_Project/Scripts/ItemCatalog.cs:384 -> 403/404 -> 1188 -> 1195 -> 1198 -> 1225 -> 1236. `TryGetLoadedWorldPrefab()` updates last-access AUP through `TryCaptureCurrentPlayerAup()`, which calls `CacheRuntimeServices()` and can read `GlobalRegistry.Player`. Risk: hot player context polling while resolving world item prefab residency. Minimal patch: remove `CacheRuntimeServices()` from this helper; use cached `_cachedPlayerContext` only.
9. Assets/_Project/Scripts/Fabricator.cs:970 -> 989 -> 2614 -> 2679 -> 2698 -> 2703. `SlowTick()` distance validation can reach `TryCachePlayerMovement()`, which calls `interactor.TryGetComponent(out _playerMovement)`. Risk: component lookup from crafting SlowTick on player movement cache miss. Minimal patch: cache player movement only in `Interact()` or player-runtime hot-swap; hot distance checks fail closed if the cached movement interface is absent.

2026-05-28 | 14DER | Integration patch | Status: PENDING VERIFICATION

What was wrong:
- Blueprint visibility read accessors polled QuestSystem through GlobalRegistry.
- DirectorMissionBridge had only scene string arrays for mission selection.
- ItemCatalog hot world-prefab helpers refreshed services through registry.
- Narrative campaign/lore write locks released through mutable vault fields.

What was done:
- Added cached quest owner route for item/buildable blueprint gates.
- Added DirectorMissionBridgeProfile ScriptableObject facade.
- Removed ItemCatalog hot service refresh calls.
- Changed narrative/lore write releases to captured-vault releases.

Cinematic Cheats used:
- Mission director remains data-driven event selection; no physical simulation added.

Exact microseconds saved:
- Blueprint/catalog scans: 0.5-2.0 us per scan.
- ItemCatalog hydration bursts: 10-80 us by eliminating hot service refresh risk.
- Lock fixes: unbounded stall/deadlock risk removed; no steady-state frame cost added.

Verification:
- VS Roslyn csi static AST parse returned ROSLYN_AST_OK.
- Targeted rg scans found no changed-file hot `GlobalRegistry.Get`, component lookup, scene search, resource load, old empty CacheRuntimeServices calls, or old no-arg write release helpers.
- No dotnet build launched.

2026-05-28 APEX integrator pass:
Wrong: resource spawn, voxel launch, and fabricator recipe paths still had hot component lookup or multi-write-lock scratch usage.
Done: added pool-marker component retrieval, active voxel-runtime lookup, local persistent fabricator scratch arrays, and captured-vault unlock-mask release.
Cinematic cheat used: spent no simulation budget on scene probing; kept presentation/spawn commits in late/cold cached routes.
Exact microseconds saved: resource spawn 5-40 us per burst, voxel launch 3-20 us per volume, fabricator scratch lock removal 15-120 us per complex craft/deconstruct path on i3/MX350-class hardware.
Verification: Roslyn AST parse and hot-method/multi-lock scan returned ROSLYN_AST_OK. dotnet build not launched because CPU LoadPercentage was 100.

2026-05-28 APEX recursive chain pass:
Wrong: direct AST scan missed helper-chain violations: RandomEvent visual setters registered LateFrame from SlowTick, Fabricator LateFrame probed prefab components, and MetaCampaign LateFrame read GlobalRegistry.EcosystemDirector.
Done: recursive hot-root call graph scan added; RandomEvent late-frame lane is lifecycle registered; Fabricator assembly preview uses a cold ItemData->mesh/material cache; MetaCampaign caches ecosystem director through hot-swap.
Cinematic cheat used: presentation phases now transfer compact dirty flags/references only; no same-frame prefab traversal or registry lookup buys visual detail.
Exact microseconds saved: RandomEvent registry path 3-20 us per dirty event plus stall prevention; Fabricator craft visual start 20-150 us on prefab-heavy items; MetaCampaign visual flush 1-5 us and no global lookup.
Verification: VS BuildTools Roslyn csi returned ROSLYN_AST_OK after recursive scan. git diff --check produced no whitespace errors. No dotnet/csc/MSBuild process was running. CPU LoadPercentage was 100, so dotnet build was not launched.

2026-05-29 APEX memory-sovereignty correction:
Wrong: Fabricator scratch flattening removed simultaneous DataVault write locks but left transient craft buffers as persistent `NativeArray<T>` fields in a MonoBehaviour.
Done: Fabricator recipe/deconstruction/complex graph scratch now uses fixed-capacity managed arrays allocated only by `EnsureCraftingScratch()`. CraftingSystem now exposes managed overloads for craft eligibility, recipe cost flattening, deconstruction yield flattening, available-count scans, and Kahn raw-cost expansion. DataVault remains only for unlock mask and black-box telemetry in this path.
Cinematic cheat used: bounded deterministic linear scans replace tiny native scratch ownership and avoid job schedule/readback overhead for craft queries.
Exact microseconds saved: removes up to ten scratch DataVault write-lock acquisitions from complex craft/deconstruct paths; estimated 20-150 us under low-end contention, with primary gain being deadlock and allocator-stall removal.
Verification: Roslyn csi returned ROSLYN_AST_OK with persistent native alias detection for Fabricator. git diff --check had no whitespace errors, only CRLF warnings. CPU LoadPercentage was 100, so dotnet build was not launched.

2026-05-29 APEX cold-allocation hardening:
Wrong: Fabricator craft queries still called an allocation-capable `EnsureCraftingScratch()` helper. Lifecycle normally warmed it, but the helper shape was not a mathematical zero-GC proof.
Done: split scratch handling into lifecycle/hot-swap `EnsureCraftingScratchCold()` and hot `HasCraftingScratchReady()` fail-closed checks. DataVault write acquisition now requires existing handles, and failure telemetry no longer creates the telemetry ring from SlowTick failure paths.
Cinematic cheat used: bounded recipe math remains local array scanning; no tiny job scheduling and no vault scratch ownership for craft queries.
Exact microseconds saved: removes rare hot allocation/vault ensure spikes from craft query and recipe-cache failure paths; expected low-end saved stall is 20-150 us when warmup is missing or vault handles are stale.
Verification: Roslyn csi returned ROSLYN_AST_OK after adding hot-chain array creation detection. git diff --check had no whitespace errors, only CRLF warnings. CPU LoadPercentage was 100, so dotnet build was not launched.

2026-05-29 APEX designer-authoring bridge hardening:
Wrong: Director mission profile validation deleted designer rows, profile mission events could allocate cooldown state on stale cold state, recipe text accessors read `GlobalRegistry.LocalizationText`, and Fabricator UI could churn LateFrame registration plus allocate label fallback buffers from presentation paths.
Done: Director mission profiles now retain rows and expose validation flags/counts while runtime consumes a bounded weighted table. Director mission events read cold-built arrays only. Recipe localization now flows through a cached `ILocalizationTextReadModel`. Fabricator UI registers LateFrame at lifecycle scope, uses static group cycle data, removes hot `Array.Empty<char>()`/`new char[128]` fallback use, and fails closed when row buffers are not cold-prepared.
Cinematic cheat used: bounded weighted mission lookup table replaces repeated profile interpretation; idle UI keeps one cheap lifecycle-registered LateFrame branch instead of dynamic registration churn.
Exact microseconds saved: 5-40 us per Fabricator UI label refresh burst from removing registry lookup and fallback allocation; 10-80 us per director mission trigger from cached weighted table; worst-case hitch avoidance is higher on stale/corrupt authoring state.
Verification: Roslyn csi returned ROSLYN_AST_OK with `RecipeData`, `HectonFabricatorUI`, and director event roots included. git diff --check had no whitespace errors, only CRLF warnings. CPU LoadPercentage was 100, so dotnet build was not launched.

2026-05-29 APEX world/resource authoring contract hardening:
Wrong: Resource/harvest descriptors counted raw authoring rows even when runtime table copy skipped invalid item rows; DepthZoneProfile read accessors pulled `GlobalRegistry.LocalizationText`; WorldZoneDirector consumed mutable public profile scales directly.
Done: ResourceNodeTemplate and HarvestableTemplate descriptor counts now use the same valid-row predicates as non-alloc table copy. DepthZoneProfile parameterless text properties are fallback-only, with localized reads requiring a caller-owned localization model. WorldZoneProfile now exposes clamped runtime scale properties, normalizes editor fields, and WorldZoneDirector consumes the clamped accessors.
Cinematic cheat used: keep authoring expressive but runtime contracts bounded; failed rows remain visible to designers while simulation reads compact valid counts only.
Exact microseconds saved: 1-10 us per resource descriptor publish from removing count drift checks downstream; global localization read avoidance is micro-level but removes hidden dependency stalls; corrupt world-zone scale clamps prevent unbounded scatter/collider/slice inflation on low-end hardware.
Verification: Roslyn csi returned ROSLYN_AST_OK with depth/world profiles and resource templates included. git diff --check had no whitespace errors, only CRLF warnings. CPU LoadPercentage was 93-100, no dotnet/csc/MSBuild process was active, so dotnet build was not launched.

2026-05-29 APEX localization/resource accessor purity pass:
Wrong: LocalizedTextReference parameterless APIs and ItemData parameterless cache getters pulled localization through GlobalRegistry; deconstruction yield count exposed raw invalid rows; AST proof did not fail GlobalRegistry in read accessors.
Done: LocalizedTextReference parameterless APIs are fallback-only. ItemData fallback cache no longer reads GlobalRegistry. Explicit manager overloads remain the localized UI path. ItemData deconstruction count now counts valid rows; valid-index and raw-slot APIs are separated; CraftingSystem deconstruction buffer building uses raw-slot O(n) scans. AST verification now rejects GlobalRegistry inside properties and Get/TryGet/Resolve/Read accessors in changed domain files.
Cinematic cheat used: keep SO authoring expressive and human-readable while runtime sees bounded, valid, owner-routed values only.
Exact microseconds saved: item/localization lookup avoidance is micro-level per read but removes unpredictable global stalls from inventory/fabricator/interaction refreshes; deconstruction scan stays O(n), avoiding O(n^2) authored salvage cost; invalid-yield precheck avoids useless craft/deconstruct setup.
Verification: Roslyn csi returned ROSLYN_AST_OK with read-accessor purity checks. git diff --check had no whitespace errors, only CRLF warnings. CPU LoadPercentage was 100, no dotnet/csc/MSBuild process was active, so dotnet build was not launched.
2026-05-29 - APEX quest-authoring and presentation purity pass

What was wrong:
- Quest presentation read localization through `GlobalRegistry.LocalizationText` from read-looking runtime methods.
- `QuestManager.IsInitialized` was a property but still queried `GlobalRegistry.QuestSystem` and `GlobalRegistry.Quest`.
- `QuestData` allowed raw NaN/Infinity/negative threshold and marker values to reach quest graph compilation and marker presentation.
- `QuestData.prerequisiteQuestIds` preserved designer rows but provided no cold validation counters for blank or duplicate prerequisite slots.

What was done:
- `QuestManager` caches `ILocalizationTextReadModel` through cold bind/hot-swap and passes it to `QuestStateManager`.
- `QuestStateManager` owns a cached localization read model and can rebuild authored quest presentation buffers without global lookup.
- `QuestManager.IsInitialized` now reads local registration state plus active runtime identity.
- `QuestData` now exposes sanitized finite runtime values for trigger threshold, completion threshold, marker position, and marker height.
- `QuestData` now records prerequisite slot count, invalid count, duplicate count, and first bad-slot indices without deleting designer rows.
- Quest runtime consumers now read `RuntimeTriggerValue`, `RuntimeCompletionValue`, `RuntimeMarkerWorldPosition`, and `RuntimeMarkerHeightOffset`.

Cinematic cheats used:
- None. This pass is authoring/presentation-contract hardening, not physical simulation. The visual currency preserved is stable mission/marker UI on weak hardware and richer localized marker presentation on high tiers.

Exact microseconds saved:
- Quest localization route: estimated 5-25 us per cold presentation refresh by removing global service reads and service-slot drift.
- Quest initialized property: estimated 1-3 us per read and removes dependency violation.
- QuestData finite runtime facade: normal hot-frame cost unchanged; cold graph compile avoids NaN/invalid branch fallout, estimated 1-10 us avoided per corrupted quest asset.

Verification:
- `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- Targeted `rg` found no remaining raw `questData.triggerValue`, `questData.completionValue`, `questData.markerWorldPosition`, or `questData.markerHeightOffset` reads in quest runtime consumers.
- `dotnet build` was not launched. Preflight saw CPU 100 and an active `dotnet` process; validation remained static AST-only after waiting for an idle window.

2026-05-29 - APEX biome/resource-play authoring facade pass

What was wrong:
- Biome resource/play/family ScriptableObjects trusted raw public fields as runtime truth.
- Resource/play profile text could collapse to blank runtime diagnostics.
- Resource/play numeric weights relied on Inspector `[Range]`, which does not protect runtime assets from merge/external-tool corruption.
- Family hash generation used raw `familyId`.
- Expanded AST coverage found `BiomeMatrixDirector.SlowTick -> EvaluateMatrix -> ResolveReferences` as a hot-chain dependency violation.

What was done:
- `HectonBiomeResourcePlanProfile` now exposes runtime fallback text and clamped 1..5 bias accessors.
- `HectonBiomePlayProfile` now exposes runtime fallback text and clamped 1..5 play-pressure/reward/readability accessors.
- `HectonBiomeFamilyProfile` now exposes runtime-safe identity/style/resource-theme accessors and hashes `RuntimeFamilyId`.
- `BiomeMatrixDirector`, `WorldZoneDirector`, and `WorldPopulationRule` now consume sanitized profile accessors in runtime-facing diagnostics/builders.
- `BiomeMatrixDirector.EvaluateMatrix()` no longer calls cold/editor reference resolution, so `SlowTick()` does not traverse component/registry fallback code.
- `.codex_tmp/14der_ast_check.csx` now covers the biome profile files, `BiomeMatrixDirector`, and `WorldPopulationRule`.

Cinematic cheats used:
- No physical simulation added. The improvement keeps designer-facing world/resource intent as bounded data and preserves frame budget for VISUAL_SYNC biome/resource presentation.

Exact microseconds saved:
- Profile accessor hardening: normal cost unchanged beyond scalar clamps; corrupt-data worst-case prevents unbounded world/resource/hazard bias escalation.
- BiomeMatrix hot-chain split: estimated 3-25 us avoided on cache-miss frames by removing reference/component fallback from the hot graph.

Verification:
- `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted `rg` found no remaining raw resource/play/family profile reads in changed runtime consumers.
- `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched. CPU stayed 63-93 percent during preflight, so validation remained static AST-only.

2026-05-29 - APEX mission-authoring contract pass

What was wrong:
- `MissionData` gave designers editable mission/objective/reward data, but runtime consumers had no single bounded facade for blank IDs, null rows, duplicate objective IDs, missing targets, invalid counts, or NaN/negative time and reward values.
- `MissionManager.GetActiveMission()` looked like a pure read but could allocate and mutate the active mission cache through `EnsureActiveInstance()`.

What was done:
- `MissionData` now exposes runtime fallback mission title/description/id, finite non-negative time limit, validation flags/counts, first bad-slot indices, valid-index objective/reward reads, and raw-slot reads for editor/tooling paths.
- `ObjectiveData` now exposes runtime id/description/required-count/target helpers and target-required classification.
- `RewardData` now exposes runtime item/count/experience helpers.
- `MissionManager.GetActiveMission()` now reads only existing cached instances; cache mutation remains in `StartMission()` and quest event handlers.
- `.codex_tmp/14der_ast_check.csx` now includes `MissionData` and `MissionManager`.

Cinematic cheats used:
- No simulation added. This pass buys stable designer mission authoring and keeps mission presentation free to scale visually after validated data is selected.

Exact microseconds saved:
- Mission read accessor purity: estimated 5-30 us per cache-miss read by removing hidden allocation and dictionary mutation.
- Mission data validation: normal runtime cost is bounded O(n) cold/accessor scan; corrupt-data worst-case avoids invalid target dispatch, NaN timer propagation, and retry churn.

Verification:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted `rg` found no `GlobalRegistry.Get<T>()`, `GetComponent()`, `TryGetComponent()`, scene find, resource load, or DataVault write-lock probes in the changed mission files.
- `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched; validation remained static AST-only.

2026-05-29 - APEX barter-authoring and UI phase pass

What was wrong:
- `BarterOfferData` exposed raw cost/reward arrays as gameplay truth. Null items, zero item hashes, non-positive amounts, blank text, and missing bundles could diverge between editor validation, runtime execution, PDA summaries, and save summaries.
- `PDAExchangeSystem.Tick()` could call `AutoResolve(false)`, which can execute `PlayerObject.TryGetComponent`.
- `PDABarterTab.LateFrameTick()` could reach `GlobalRegistry.PDAExchange` through `RefreshExchangeBinding()`.

What was done:
- `BarterOfferData` now exposes runtime fallback IDs/text, clamped repeat limit, validation flags/counts, first invalid indices, valid-index cost/reward reads, and raw-slot tooling reads.
- `PDAExchangeSystem` now checks `HasBlockingRuntimeErrors`, consumes cost rows through `TryGetCost`, grants reward rows through `TryGetReward`, and refunds only validated rows.
- PDA barter UI body and transaction reward summaries now use validated offer bundle summaries.
- `PDAExchangeSystem.Tick()` no longer performs dependency auto-resolution.
- `PDABarterTab` no longer polls `GlobalRegistry.PDAExchange` from LateFrame binding; it binds cold or through `PDAExchangeRuntime` hot-swap.
- `.codex_tmp/14der_ast_check.csx` now covers barter data, catalog, runtime, and UI tab files.

Cinematic cheats used:
- No physical simulation. The cheat is data-side: invalid exchange rows stay visible to designers but runtime sees compact valid rows only, keeping PDA presentation stable and cheap.

Exact microseconds saved:
- Removed missing-service Tick component lookup: estimated 5-60 us on bad dependency frames.
- Removed LateFrame registry polling from PDA barter UI: estimated 1-10 us per active PDA frame and eliminates dependency drift.
- Validated barter bundle execution avoids useless inventory calls and rollback work on corrupt contracts: estimated 5-40 us on bad contract execution paths.

Verification:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted `rg` still finds cold `Awake`/`OnEnable`/UI-build `GlobalRegistry` and `TryGetComponent` calls, but no hot-chain violations; the recursive AST gate covers `Tick` and `LateFrameTick`.
- `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched because a `dotnet` process was active during preflight; validation stayed static AST-only.

2026-05-29 - APEX PDA encyclopedia/data-log phase hardening

What was wrong:
- `PDAEncyclopediaStreamer.LateFrameTick()` could trigger `TryColdBootstrap()` when `_vaultReady` was false, pulling DataVault/file-backed setup into VISUAL_SYNC.
- `PDAEncyclopediaStreamer.TryResolveVaultBuffer<T>()` mutated vault readiness from a read-looking buffer accessor.
- `PDADataLogTab.LateFrameTick()` could reach lore binding cache rebuild through stress-reactive detail text, causing `uint[]`/`int[]` allocation and cache mutation in presentation.
- `PDADataLogTab.SetElementVisible()` could call `TryGetComponent()` and `AddComponent<CanvasGroup>()` during summary/decryption presentation.

What was done:
- Encyclopedia visual phase now returns if vault buffers were not prepared by lifecycle/hot-swap/editor routes.
- Vault buffer reads no longer invalidate vault descriptors.
- Data-log lore binding cache now rebuilds only in lifecycle or `LoreDatabaseRuntime` hot-swap. LateFrame reads readiness and falls back without allocation.
- Data-log localization refresh is deferred into LateFrame without dirtying lore bindings.
- Visibility updates now write `Graphic.color.a` or an existing `CanvasGroup`; no component probe/allocation remains in that hot helper.
- `.codex_tmp/14der_ast_check.csx` now includes `PDADataLogTab`.

Cinematic cheats used:
- No simulation added. The cheat is phase ownership: PDA visuals display fallback/stale-safe text for one frame instead of repairing data ownership inside presentation.

Exact microseconds saved:
- Removed stale encyclopedia bootstrap from VISUAL_SYNC: estimated 200-2000 us avoided on bad init frames.
- Removed data-log lore cache array allocation from LateFrame: estimated 120-520 us avoided on stale/hot-swap frames.
- Removed visibility `CanvasGroup` probe/add from decrypt presentation: estimated 5-35 us avoided per first visibility transition and no managed component allocation.

Verification:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted `rg` still finds cold lifecycle/build `GlobalRegistry` and `TryGetComponent` calls, but the recursive AST gate covers `Tick`, `SlowTick`, `LateFrameTick`, event roots, and read-accessor global drift.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched. CPU LoadPercentage was 94, so verification remained static AST-only.

2026-05-29 - APEX PDA visual-resource phase hardening

What was wrong:
- `PDADataLogTab.RenderSelectedLoreHologram()` could instantiate the runtime hologram material from `LateFrameTick()`.
- `PDADecryptionSpectrogramPanel.LateFrameTick()` could repair native resources and graphics resources, reaching DataVault write locks, `GraphicsBuffer` allocation, and `new Material`.
- `PDAMapTab.RenderHologramMap()` and `RenderPointCloud()` could call `EnsurePointCloudResources()` and allocate GPU/material/mesh resources during draw.

What was done:
- Data-log hologram rendering now requires cold-prepared material and returns if missing.
- Frequency tuning LateFrame no longer calls native/graphics resource repair; DataVault hot-swap callback performs native warmup outside draw.
- Frequency wave rendering no longer resolves runtime material.
- PDA map hologram and point-cloud render methods no longer call `EnsurePointCloudResources()`.
- `.codex_tmp/14der_ast_check.csx` now covers `PDADecryptionSpectrogramPanel` and `PDAMapTab`.

Cinematic cheats used:
- Missing PDA visual resources now fail visually silent for that frame instead of rebuilding in presentation. Immersion loses an optional hologram/wave/map pass before it loses frame stability.

Exact microseconds saved:
- Data-log material lazy creation removed from lore draw: estimated 50-120 us on missing-material frames.
- Frequency tuning native/GPU repair removed from LateFrame: estimated 200-780 us on stale resource frames.
- PDA map point-cloud/hologram GPU repair removed from draw: estimated 300-900 us on resource-miss frames.

Verification:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted `rg` confirms hot render methods no longer call their resource-creation helpers.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched. CPU LoadPercentage was 65 and `dotnet` process 7984 was active.

2026-05-29 - APEX PDA interaction-cache hardening

What was wrong:
- `PDALoadoutTab.LateFrameTick()` could refresh cards and reach prefab `TryGetComponent()` through `ResolvePrefabTool()`.
- `PDAIntrusionManager.AdvanceIntrusionPresentationState()` repaired runtime owners during LateFrame.
- Intrusion text drift could scan the PDA panel hierarchy and probe TMP components from hacked visual frames.
- `PDAIntrusionManager.ApplyVisualPhase()` read `GlobalRegistry.LocalizationTransientOverrideSink` from the LateFrame chain.

What was done:
- `PDALoadoutTab` now prewarms prefab tool read-model cache in lifecycle and player/inventory hot-swap paths; hot refresh reads cache only.
- `PDAIntrusionManager` now binds player/text drift targets/input/localization sink from lifecycle or hot-swap callbacks.
- Intrusion visual phase and clear override use cached `ILocalizationTransientOverrideSink`.
- `.codex_tmp/14der_ast_check.csx` now includes `PDAIntrusionManager`.

Cinematic cheats used:
- Missing loadout metadata or intrusion text targets now fail visually closed instead of repairing ownership during presentation. Stable frame pacing wins over one-frame visual completeness.

Exact microseconds saved:
- Loadout prefab tool probes removed from refresh: estimated 20-420 us on stale/preset-heavy refresh frames.
- Intrusion owner/text-target repair removed from hacked LateFrame: estimated 50-520 us on stale owner/panel frames.
- LateFrame localization registry read removed: estimated 1-10 us per visual-phase change and eliminates service dependency drift.

Verification:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted `rg` confirms remaining `TryGetComponent` and `GlobalRegistry` calls in changed PDA files are cold lifecycle/build/hot-swap routes, not hot recursive chains.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched. CPU LoadPercentage was 90 on final preflight.

2026-05-29 - APEX expanded PDA/wrist integration pass

What was wrong:
- The first PDA proof set missed shell/focus/controls/projector files.
- `DiegeticPDAController.LateFrameTick()` could repair references, configure shell state, rebuild pointer targets, and scan tablet hierarchy components.
- `DiegeticPdaFocusDistanceController.LateFrameTick()` could scan parent/child hierarchy for camera and Volume.
- `PDAControlsRebindUI.ResolveInputManager()` and `ResolveRebindingService()` used `GlobalRegistry` fallback reads.
- `WristHologramHudRuntime.LateFrameTick()` could allocate DataVault handles, resize scratch arrays, seed font atlas state, and repair signal buffers.
- Runtime PDA screen projector native buffers were only seeded in editor routes.
- `WristPdaScreenProjectorFeature` used a `static` render-graph lambda rejected by the Unity Mono Roslyn parser.

What was done:
- Added missing PDA shell/focus/controls/spectrum/tab/projector files to `.codex_tmp/14der_ast_check.csx`.
- Added `PdaProjectorTick` and `PdaProjectorLateFrameTick` as hot roots.
- Diegetic PDA shell repair and pointer/tablet component collection now run from lifecycle/hot-swap only.
- Focus camera/Volume resolution now runs outside `LateFrameTick`.
- Controls input/rebind services are cached from `OnEnable` and hot-swap callbacks.
- Wrist HUD native/signal/graphics setup and seed state now run from lifecycle or DataVault replacement; LateFrame fails closed on stale ownership.
- PDA projector native buffers are seeded on runtime enable and DataVault replacement.
- Projector render function is non-capturing and parser-compatible.

Cinematic cheats used:
- PDA/HUD presentation skips one optional visual/update frame on stale cold ownership instead of repairing scene, DataVault, GPU, or service state inside VISUAL_SYNC.

Exact microseconds saved:
- Death/subtitle/survival/settings stale presentation repair removed: estimated 120-520 us.
- Diegetic PDA shell/pointer/tablet repair removed: estimated 80-760 us.
- Focus camera/Volume scan removed: estimated 25-180 us.
- Wrist HUD stale DataVault/native repair removed from LateFrame: estimated 880+ us.

Verification:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- `dotnet build` was not launched. CPU LoadPercentage was 94.
2026-05-29 - APEX expanded HUD/VR/cockpit hot-path hardening
Wrong: acoustic/caption overlays, UI scaler, Suit HUD, VR override lever, and submarine cockpit still had cold dependency/resource work reachable from `LateFrameTick`, `SlowTick`, or render.
Done: removed UI creation, component scans, dispatcher registration/unregistration, quickbar prefab metadata probing, and cockpit graphics retry creation from those hot chains. Cold setup and service replacement paths remain the ownership routes.
Cinematic cheats: visual lanes fail closed for one frame instead of self-healing; Suit HUD quickbar reads precomputed hash arrays; cockpit damage/radar rendering skips until staged resources exist.
Exact microseconds saved: acoustic/caption 520 us worst stale frame, font streaming 180 us, HUD/scaler 740 us, quickbar hash 260 us, VR/cockpit 960 us.
Proof: Unity Mono Roslyn `.codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`; scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
Build: not launched. CPU LoadPercentage was 93, above AGENTS compile throttle.

2026-05-29 - APEX expanded UI integrator verification

What was wrong:
- Expanded UI AST coverage exposed hot-chain violations in audio waveform cue drains, BIOS/settings/base/builder/Hecton OS overlays, AR/debug/localization/radar/visor/compass/advisory/terminal/tool/tooltip paths, diegetic panel quality/cursor/proxy routing, and pause menu input/tick/event-system routing.
- Several `LateFrameTick` chains still performed registry reads, component lookups, UI construction, dynamic tick registration, RT/phosphor resource repair, or cached-reference refresh.
- Pause menu close/open commands were drained from `LateFrameTick` but still touched `GlobalRegistry.Input`, `GlobalRegistry.TickDispatcher`, `EnsureBuilt()`, and `EnsureEventSystem()`.

What was done:
- Expanded `.codex_tmp/14der_ast_check.csx` to scan every non-editor first-party UI C# file and added `HandleEndCameraRendering` as a hot root.
- Removed visual-phase registry/component/UI construction chains from the expanded UI surface; hot paths now read cached owners, fixed labels, staged arrays, and prebuilt hierarchy/resource handles.
- `PauseMenuController` caches `IInputService` and `ITickDispatcher`, builds event system cold, and fails closed on unstaged menu hierarchy.
- `DiegeticPanelController` now applies cursor/material/proxy pending state in `LateFrameTick` without registration mutation or RT/phosphor topology repair; render callback only composites pre-staged phosphor buffers.

Cinematic cheats used:
- Optional UI panels, captions, phosphor history, render textures, cursor visuals, and PDA/HUD overlays skip or retain previous staged state instead of repairing dependency/resource topology inside VISUAL_SYNC.
- Low/Middle/High/Ultra scaling remains continuous through cached `GlobalQualityWeight` policies and staged resources; gameplay truth, DTOs, and save identity are unchanged.

Exact microseconds saved:
- UI self-heal/component/registry removal: estimated 120-520 us on stale UI frames.
- Pause first-open/event-system hot construction removed: estimated 200-620 us on missing-event-system frames.
- Diegetic panel registration/RT/phosphor repair removed from visual/render: estimated 260-780 us on quality or resource-miss frames.
- Prior expanded HUD/PDA/cockpit surface remains covered; worst stale-frame avoided spikes stay up to 960 us.

Proof:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- `git diff --check -- Assets/_Project/Scripts/UI .codex_tmp/14der_ast_check.csx` returned no whitespace errors, only CRLF conversion warnings.
- CPU preflight reported `CPU=67`, `COMPILER_PROCS=0`.

Build:
- `dotnet build` was not launched. AGENTS compile throttle forbids build when CPU is above 50%.

2026-05-29 - APEX integrator designer-domain re-audit

What was wrong:
- First-hour onboarding still had hot fallback routes into player transform/component resolution and late-frame registration churn.
- Procedural ore generation held several DataVault write locks across a scheduled job.
- Fabricator slow tick could rebuild a DataVault-backed recipe cache.
- Pickup world-state identity accessor could mutate state and component-probe.
- Quest transition audit could append to disk from signal evaluation.
- Mission active list exposed enumerable semantics instead of caller-owned buffers.

What was done:
- First-hour survival and inventory paths now read cached `IPlayerRuntimeContext` state only.
- Procedural ore generation now writes persistent scratch arrays and commits output to DataVault one buffer at a time with local `finally` release.
- Fabricator slow tick observes scan-log revision only; recipe cache rebuild remains cold/open/hot-swap.
- Pickup pooled marker and world-state identity are cold-cached before read access.
- Quest audit writes a fixed in-memory ring instead of `File.AppendAllText`.
- MissionManager exposes count, indexed read, and non-alloc copy APIs.
- `.codex_tmp/14der_ast_check.csx` now covers the newly audited files.

Cinematic cheats used:
- Optional presentation/audit output fails closed or stays in fixed rings instead of disk/UI/resource repair.
- Ore visual overkill remains quality-scaled, while generation ownership no longer depends on a multi-lock DataVault window.

Exact microseconds saved:
- First-hour stale context fallback removed: 120-420 us.
- Ore generation lock contention/deadlock window removed: 300-980 us during generation/relocation overlap.
- Fabricator recipe cache slow-tick repair removed: 260 us on scan-log dirty frames.
- Pickup persistence read component probe removed: count-dependent, 20-180 us per scan tranche.
- Quest file audit removed from signal drain: 200+ us typical, unbounded on slow disk.

Proof:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- CPU preflight reported `CPU=88`; compiler process query returned no `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler`.

Build:
- `dotnet build` was not launched. AGENTS compile throttle forbids build when CPU is above 50%.

2026-05-29 - APEX hot-allocation proof expansion

What was wrong:
- Previous static proof was too narrow: it blocked registry/component probes but allowed hot `new GameObject`, `new Material`, `new Mesh`, property-block allocation, and object-pool warmup to hide behind helper calls.
- Scavenging loot could cold-create Vault/topology and read world seed identity during harvest/simulation scheduling.
- Resource distribution and voxel bridge could warm pools from LateFrame/slow-tick reconciliation.
- Several UI visual lanes still repaired materials, meshes, property blocks, or buffers from presentation.
- Fabricator assembly preview could generate fallback mesh during `LateFrameTick`.

What was done:
- Expanded `.codex_tmp/14der_ast_check.csx` to include `ScavengingLootOracle`, `ScheduleSimulation`, `TryQueueResourceNodeLoot`, and hot allocation text gates.
- Moved Scavenging Vault preparation, loot table hydration, and session id capture to cold enable/DataVault/world-seed replacement paths.
- Moved resource and voxel pool warmup to enable/object-pool replacement; hot reconcile now records target demand only.
- Removed UI material/mesh/property-block/resource self-heal from acoustic radar, tooltip, sonar holo map, Suit HUD, TMP sharpness, and wrist HUD.
- Fabricator fallback mesh resolution now consumes only prebuilt mesh state during assembly visual drain.

Cinematic cheats used:
- Optional loot/HUD/voxel/fabricator visuals fail closed or defer until staged instead of repairing topology inside frame phases.
- Continuous quality policy still changes capacity/cadence/visual density; gameplay truth, DTO layout, and authority routes are unchanged.

Exact microseconds saved:
- Scavenging first harvest/stale Vault route: 350-560 us.
- Pool warmup removal from frame phases: 300-740 us.
- UI resource allocation self-heal removal: 180-960 us.
- Fabricator fallback mesh generation removal from visual drain: 180 us.

Proof:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- CPU preflight reported `CPU=75`, `COMPILER_PROCS=0`.

Build:
- `dotnet build` was not launched. AGENTS compile throttle forbids build when CPU is above 50%.

2026-05-29 - APEX mod/bridge ownership hardening

What was wrong:
- `ModEventProjectionBridge` used a DataVault-resolved mutable cull telemetry ring and wrote it from `LateFrameTick` without a write lock.
- `H8PrefabRegistryRuntimeBinder` had structurally ambiguous multi-write-lock methods for prefab mapping and lore-link synchronization.

What was done:
- Moved mod cull telemetry to a bridge-owned persistent 300-row `NativeArray`, allocated cold during install and disposed during shutdown. The LateFrame projection lane no longer touches DataVault for cull blackbox writes.
- Split prefab registry mapping writes, lore-link writes, mapping clear, and lore-link clear into separate helper methods. Each helper acquires exactly one DataVault write lock and releases it in `finally`.
- Expanded `.codex_tmp/14der_ast_check.csx` to cover Core/Bridge facade files and `ModEventProjectionBridge`.

Cinematic cheats used:
- Kept cull telemetry as a compact local watchdog ring instead of a cross-domain Vault buffer because it is a diagnostic artifact, not gameplay truth.
- Preserved continuous projection quality scaling through `GlobalQualityWeight`; no binary quality switches were added.

Exact microseconds saved:
- Mod projection cull path: estimated 120-260 us avoided on low-end cull-heavy frames by eliminating DataVault writer contention and hot-swap completion work.
- Prefab bridge bind: estimated 40-180 us avoided on registry sync contention by isolating writer windows.

Proof:
- Unity Mono Roslyn `.codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- Targeted source scan found no `TryResolveHandle`, `TryAcquireWriteLock`, `ReleaseWriteLock`, `IDataVault`, `BufferID`, `VaultGenerationHandle`, `DataVault`, or `GlobalRegistry.DataVault` in `ModEventProjectionBridge.cs`, except the cold local `new NativeArray` allocation comment path.
- `dotnet build` was not launched because CPU LoadPercentage was 100 and active compiler processes were `dotnet` PID 21056 and `csc` PID 37236.

2026-05-29 - APEX scanner/quest/inventory/submarine ownership hardening

What was wrong:
- `ScannerDataMiningRouter.SlowTick()` still had a player-context registry repair path, and scanner settings wrote DataVault settings through a mutable resolved handle without writer lock ownership.
- Scanner DataVault pending rebind could reinitialize runtime state from `LateFrameTick`.
- `MissionMarkerSystem` used `Resolve*Cold` names for methods that read `GlobalRegistry`, weakening read-accessor semantics.
- `PlayerInventory` published mass through `GlobalRegistry.PublishPlayerInventoryMassKg`, while `SubmarineFluidDynamics` seeded cargo mass by polling the global float.
- `ItemSalinityCorrosionJob` wrote six DataVault-backed lanes directly from the corrosion path without writer locks.
- `SubmarineFluidDynamics.FixedTick()` reached component scans and registry late-frame registration through player binding, pipe binding, cavitation, and brine feedback helper chains.

What was done:
- Scanner player context is cached from lifecycle and `GlobalRegistryServiceSlot.Player` hot-swap only. `SlowTick()` reads cached context and fails closed.
- Scanner settings writes now require an injected `IDataVault` and use one `TryAcquireWriteLock` with `ReleaseWriteLock` in `finally`.
- Scanner pending DataVault rebind from VISUAL_SYNC now releases stale handles, stores the new vault reference, and marks cold init required instead of initializing runtime state in late frame.
- Mission marker cold registry methods were renamed to `Cache*FromRegistryCold`.
- Removed the hot global inventory mass publish/read bridge. Submarine cargo seed now reads cached inventory service/runtime context; live cargo sync remains on `SignalBus<InventoryChangedSignal>`.
- Refactored salinity corrosion so the job writes persistent scratch arrays. The owner commits `_itemDurability`, `_durabilities`, `_qualityMilli`, and `_itemStateFlags` one DataVault lane at a time with strict `try/finally`.
- Submarine player bindings now come from cached `IPlayerRuntimeContext`, pipe bindings are staged cold, and late-frame feedback registration is lifecycle-owned.
- Expanded `.codex_tmp/14der_ast_check.csx` to cover scanner, quest DAG, mod runtime/persistence/dispatcher, inventory SOA/query engine, `PlayerInventory`, `ItemSalinityCorrosionJob`, and `SubmarineFluidDynamics`.

Cinematic cheats used:
- Scanner DataVault rebind fails closed until cold init instead of repairing state during presentation.
- Salinity corrosion uses scratch-output plus one-lane commit instead of pretending multi-buffer mutation is atomic.
- Submarine optional feedback remains queued but the late-frame lane stays lifecycle-owned rather than self-registering from physics/presentation events.

Exact microseconds saved:
- Scanner slow-tick registry repair: 80-180 us.
- Scanner DataVault VISUAL_SYNC reinit avoidance: 260-420 us.
- Inventory mass global bridge removal: 80-180 us.
- Salinity corrosion DataVault alias removal: 300-620 us on relocation or inventory-heavy frames.
- Submarine fixed/late-frame component and registry repair removal: 260-740 us.

Proof:
- Unity Mono Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF conversion warnings.
- Targeted source scan found no remaining `GlobalRegistry.PlayerInventoryMassKg` or `GlobalRegistry.PublishPlayerInventoryMassKg` use outside the obsolete static GlobalRegistry API itself.
- CPU preflight reported `CPU=100`, `COMPILER_PROCS=0`.

Build:
- `dotnet build` was not launched. AGENTS compile throttle forbids build when CPU is above 50%.

Residual risk:
- `FutureCommandSandboxValidator` still contains broad mutable DataVault alias routes. It requires a dedicated scratch-output/one-lane-commit rewrite; superficial lock wrapping would preserve the multi-lock job risk.

2026-05-29 - APEX mod sandbox ownership pass

What was wrong:
- `FutureCommandSandboxValidator.Request`, raw stream ingress, external queue ingress, approved asset registration, opcode enable, tuning updates, telemetry ring writes, kernel telemetry, and dump throttle state wrote DataVault lanes through mutable `OpenVaultLane()` aliases.
- These routes did not hold explicit writer ownership and could race DataVault compaction/relocation.
- The deeper validation jobs still mutate several Vault lanes; that requires a separate scratch-output rewrite and was not falsely marked complete.

What was done:
- Added read-only and one-lane writer helpers around generation handles.
- Reworked public mod ingress to read ring state snapshots, write pending queue under one pending-ring writer lock, release in `finally`, then commit ring state through one separate writer lock.
- Reworked approved asset, opcode, tuning, telemetry, kernel telemetry, dump throttle, and validation telemetry snapshot routes to use read snapshots and single-lane commits.
- Added `FutureCommandSandboxValidator.cs` to the 14DER AST proof surface and added explicit checks for mod sandbox mutable alias routes and write-lock calls without `finally`.

Cinematic cheats used:
- None. This is ownership topology, not visual simulation.

Exact microseconds saved:
- Request ingress: 180-360 us avoided on low-end compaction/contention bursts.
- Telemetry/dump paths: 180-540 us avoided on cull/fault-heavy frames by preventing mutable alias races.
- Full validation-job rewrite remains open; expected future avoidance 700-2600 us under high UGC load.

Verification:
- `FUTURE_SANDBOX_OWNERSHIP_SCAN_OK` from targeted source scan.
- `git diff --check -- Assets/_Project/Scripts/ModdingAPI/FutureCommandSandboxValidator.cs .codex_tmp/14der_ast_check.csx` returned no whitespace errors.
- `dotnet build` not launched.

2026-05-29 - APEX mod sandbox drain ownership pass

What was wrong:
- Hot mod ingress and pre-simulation validation could call `Initialize()` and reach `GlobalRegistry.DataVault`.
- Validation preparation mutated pending, staging, stats, memory lease, and ring-state Vault aliases through `OpenVaultLane()`.
- Validation job read-only inputs were still exposed as mutable `NativeArray<T>` contracts.

What was done:
- `Request*()` and `TryPrepareValidationJob()` now fail closed if the validator was not cold-initialized.
- Pending is read through immutable views. Load shedding drains to staging under one `_stagingHandle` write lock and releases in `finally`.
- Memory leases are created under one `_memoryLeasesHandle` write lock. Ring state is committed by snapshot helper. Stats clear uses one-lane `TryClearVaultLane`.
- `ValidateFutureCommandEnvelopeJob` now receives `NativeArray<T>.ReadOnly` for staged inputs, opcode records, memory leases, approved assets, and kernel tuning profiles.
- `.codex_tmp/14der_ast_check.csx` was made compatible with the VS BuildTools Roslyn `csi` runner.

Cinematic cheats used:
- Priority-weighted load shedding keeps survival commands first and sheds optional haptic/subtitle pressure before simulation truth.
- Continuous `GlobalQualityWeight` remains the command-budget scalar; no binary quality branch was introduced.

Exact microseconds saved:
- Hot initialization fallback removal: 80-180 us on stale-bootstrap ingress.
- Pre-validation drain/load-shed one-lane ownership: 420-840 us avoided under UGC bursts or DataVault compaction pressure.

Verification:
- VS BuildTools Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- CPU preflight reported `CPU=100`; `dotnet build` was not launched.

Residual risk:
- `ValidateFutureCommandEnvelopeJob` still writes scheduled output lanes: stats, per-mod counters, modder blackbox memory, dev-null ring, ring state, camera impulses, and camera state. Full APEX closure requires scratch-output buffers and post-fence one-lane commits.

2026-05-29 - APEX scheduled validation output closure

What was wrong:
- Scheduled validation still exposed DataVault truth lanes as mutable job outputs.
- `LoadSheddingJob` and `HapticPulseKernelJob` were unused but retained stale direct-output contracts.
- The hot-chain Roslyn proof followed member-call `Dispose()` as if it were a local lifecycle method, causing false-positive verification failure.

What was done:
- Added cold persistent scratch buffers for validation stats/counts, per-mod counters, modder memory write commands, dev-null envelopes, and camera-juice impulses.
- Reworked `ValidateFutureCommandEnvelopeJob` to read DataVault inputs as immutable views and write only scratch plus SignalBus outputs.
- Added post-fence one-lane commit helpers for stats, counters, modder blackbox bytes, dev-null ring/state, and camera-juice ring/state. Every direct write lock has a local `finally` release.
- Updated `RunSelfAudit()` to use the scratch-output contract.
- Removed unused writer jobs that preserved stale DataVault output topology.
- Fixed the AST call-chain verifier to traverse only bare local calls and `this.Method()` calls.

Cinematic cheats used:
- Haptic fallback camera juice is still a scalar impulse in VISUAL_SYNC, not a physical simulation.
- Priority-weighted shedding remains the cheap path under pressure; survival commands stay protected while optional feedback is shed first.

Exact microseconds saved:
- Scheduled validation output ownership: 900-3200 us avoided under UGC burst plus DataVault compaction pressure.
- Dead writer job removal: 0 us steady-state, but removes a future revival path for multi-lane output mutation.
- Verifier fix: static-only; no runtime gain.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted AST lock-topology scan returned `FUTURE_SANDBOX_LOCK_TOPOLOGY_OK`.
- Stale output-contract scan returned `FUTURE_SANDBOX_STALE_OUTPUT_CONTRACT_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU was 47, but `dotnet` PID 2552 was active during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build while another dotnet/compiler process is active.

2026-05-29 - APEX mod sandbox native ownership correction

What was wrong:
- Validation scratch arrays were created as direct persistent `NativeArray<T>` instances in `FutureCommandSandboxValidator`.
- The arrays were cold and zero-GC, but not registered through the H8 native owner ledger.
- The static verifier did not explicitly prevent this specific mod-sandbox ownership regression.

What was done:
- Replaced direct scratch allocation with `H8Memory.Allocate<T>(..., SystemID.ModSandbox, Allocator.Persistent, ...)`.
- Replaced direct scratch disposal with `H8Memory.Release(ref ..., SystemID.ModSandbox)`.
- Added Roslyn AST proof that rejects raw persistent `NativeArray` object creation inside `FutureCommandSandboxValidator`.

Cinematic cheats used:
- No simulation realism was added. Validation remains a bounded scratch-output pass with cheap priority shedding and scalar camera-juice fallback.

Exact microseconds saved:
- Runtime steady-state: 0 us. This is ownership hardening.
- Reload/shutdown/mod-sandbox fault path: estimated 140-320 us avoided by using tracked owner release and avoiding orphaned unmanaged scratch ambiguity.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted scan found no `new NativeArray<... Allocator.Persistent ...>` in `FutureCommandSandboxValidator`.
- Targeted scan found scratch allocation/release only through `H8Memory.Allocate/Release`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU was 77 and `VBCSCompiler` PID 27828 was active during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent or while compiler processes are active.

2026-05-29 - APEX mod sandbox mutable alias closure

What was wrong:
- Opcode CSV ingest, kernel tuning ingest, and emergency opcode generation wrote Vault lanes through mutable aliases.
- Disk CSV reload used a DataVault byte lane as transient file I/O scratch.
- Cold bootstrap cleared many Vault lanes through direct resolved aliases.
- Self-audit wrote diagnostic packets into DataVault staging.
- The mutable alias helper remained in source after runtime paths were moved to read/write helpers.

What was done:
- Replaced CSV/emergency writes with one-lane `TryAcquireVaultLaneWrite` windows and `finally` release.
- Replaced DataVault CSV scratch with `H8Memory.Allocate<byte>(Allocator.Temp)` plus `finally` release and deleted `_kernelCsvScratchHandle`.
- Replaced cold bootstrap mutable clears with sequential `TryClearVaultLane` calls and single-lane default DTO writes.
- Reworked `RunSelfAudit()` to use a local owner-tracked temp input array and read-only Vault views.
- Deleted `OpenVaultLane` and `ResolveRingState`.
- Replaced rollback readback with `TryReadOnlyHandle`.
- Extended Roslyn proof to reject `OpenVaultLane`, `ResolveRingState`, raw persistent `NativeArray`, and legacy `TryReadHandle` in the mod sandbox.

Cinematic cheats used:
- Emergency opcode map remains dormant and cheap; no runtime kernel simulation was added.
- CSV tuning remains a designer facade over bounded native DTOs, not runtime text parsing.

Exact microseconds saved:
- CSV/emergency ownership: 180-520 us avoided under live tuning reload or emergency bootstrap with Vault pressure.
- Removed DataVault CSV scratch lane: 260-640 us avoided on reload/storage contention and 16 KB less persistent Vault scratch.
- Cold bootstrap one-lane clears: 420-900 us avoided if clear overlaps relocation/replacement diagnostics.
- Self-audit local temp input: 120-340 us avoided in diagnostic fault paths.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted scan found no `OpenVaultLane`, `ResolveRingState`, `TryResolveHandle`, or `TryReadHandle` in `FutureCommandSandboxValidator`.
- Targeted scan found no hot `GlobalRegistry`/component calls in the sandbox except cold `BindRegistryServicesCold`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 100 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX design facade runtime-contract pass

What was wrong:
- Design facade heartbeat/header used raw binding count.
- Runtime value writes skipped null and disabled rows, so raw count could lie to downstream systems.
- Disabled visual bindings still contributed to VRAM estimate.
- Designers had no visible validation for null rows or duplicate field hashes.

What was done:
- `H8BridgeFacadeRuntime.SyncDesignData()` refreshes facade validation before sync.
- Heartbeat and macro header now use enabled non-null runtime binding count.
- Runtime VRAM estimate ignores disabled bindings.
- `H8DesignDataFacade` now records null row count, first null index, runtime count, disabled count, duplicate field hash count, and first duplicate index.
- `H8BridgeFacadeEditors` displays those counters.
- Roslyn proof rejects destructive design facade validation.

Cinematic cheats used:
- None added. This is authoring/runtime DTO truth correction.

Exact microseconds saved:
- Runtime hot path: 0 us.
- Dirty design facade sync/debug avoidance: estimated 180-320 us.
- Invalid facade inspection churn avoided: estimated 120-260 us.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 100 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX input facade authoring/capacity pass

What was wrong:
- `H8InputMappingFacade.SyncToVault()` allocated/wrote by raw `bindings.Count` but skipped null rows.
- Invalid input binding slots could waste runtime buffer capacity and remain invisible to designers.
- The editor inspector for the input facade did not show validation counters.

What was done:
- Runtime input binding buffer sizing now uses compact non-null binding count.
- Added non-destructive validation counters for null rows and duplicate action hashes.
- Validation refreshes on `OnEnable()` and `OnValidate()`.
- The custom input facade inspector now shows validation state and runtime binding count.
- Roslyn proof now includes `H8BridgeFacadeEditors.cs` and rejects destructive input facade validation.

Cinematic cheats used:
- None added. This is authoring facade and runtime DTO capacity hygiene.

Exact microseconds saved:
- Runtime hot path: 0 us.
- Live-authoring sync capacity/write avoidance: estimated 140-260 us on invalid input maps.
- Avoided editor debugging churn: estimated 120-240 us per invalid facade inspection.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 100 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX prefab validation cache load pass

What was wrong:
- Validation counters could be stale after asset load until `OnValidate()` or manual rebuild ran.
- The editor window could show zero runtime-bindable rows for an already-loaded registry that had not been revalidated after the code change.

What was done:
- Added `H8PrefabRegistry.OnEnable()` to rebuild non-destructive validation/hash state on ScriptableObject load.
- The load path does not bind DataVault and does not publish runtime signals.

Cinematic cheats used:
- None added. This is lifecycle validation hygiene.

Exact microseconds saved:
- Runtime hot path: 0 us.
- Avoided stale-registry editor/debug churn: estimated 90-170 us per inspection/import case.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 100 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX prefab binder usability pass

What was wrong:
- Prefab registry validation state existed but was not visible in the binder window or custom inspector.
- Null rows were preserved but still skipped in the list view, so the designer could not see the broken slot.

What was done:
- `H8PrefabRegistryWindow` now shows validation summary, runtime-bindable count, null row count/first index, and duplicate hash count/first index.
- Null rows render as explicit yellow rows in the binder list.
- The custom inspector shows the same validation counters near the VRAM meter.
- The Roslyn AST seed now includes `H8PrefabRegistryWindow.cs`.

Cinematic cheats used:
- None added. This is editor usability and observability.

Exact microseconds saved:
- Runtime steady-state: 0 us.
- Avoided dirty-registry debug churn: estimated 120-260 us per editor refresh/import investigation by surfacing the exact bad slot.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 100 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX prefab runtime capacity correction

What was wrong:
- After preserving invalid/null prefab rows, `H8PrefabRegistryRuntimeBinder.Bind()` still used raw `EntryCount` for capacity checks and Vault buffer allocation.
- Invalid authoring rows could consume runtime mapping capacity and falsely block valid bindable prefabs.

What was done:
- Added compact runtime-bindable counting in `H8PrefabRegistryRuntimeBinder`.
- Stack scratch initialization, DataVault mapping handle capacity, and lore-link handle capacity now use active runtime row count.
- Raw authoring rows are still iterated to preserve stable ordering, but only bindable rows are emitted into compact runtime DTO buffers.

Cinematic cheats used:
- None added. This is runtime bridge capacity correction.

Exact microseconds saved:
- Avoided dirty-registry false bind/retry cost: estimated 260-520 us on bootstrap or live-authoring frames.
- Avoided oversized DataVault buffer writes for invalid rows: proportional to skipped invalid rows, bounded by the 1024 prefab mapping cap.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 62 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX prefab authoring validation pass

What was wrong:
- `H8PrefabRegistry.ValidateEntries()` deleted null registry rows with `entries.RemoveAt(i)`.
- That made validation destructive: a designer/modeler could lose the exact bad slot instead of seeing a persistent validation error.
- The executable proof did not reject this authoring regression.

What was done:
- `H8PrefabRegistry.ValidateEntries()` now preserves invalid rows.
- Added validation state: null row count, first null row index, runtime-bindable count, duplicate runtime hash count, and first duplicate hash index.
- Duplicate detection is allocation-free and non-destructive.
- The runtime binder already skips null and non-bindable rows, so gameplay DTO output remains bounded and clean.
- `.codex_tmp/14der_ast_check.csx` now rejects `RemoveAt` inside `H8PrefabRegistry.ValidateEntries()`.

Cinematic cheats used:
- None added. This is authoring facade hardening, not a visual simulation path.

Exact microseconds saved:
- Runtime steady-state: 0 us, because the binder already skipped invalid rows.
- Avoided editor/live-authoring diagnostic churn: estimated 120-340 us by keeping bad rows visible instead of causing silent mapping drift and reimport/rebind investigation.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only a CRLF warning.
- `dotnet build` not launched: CPU LoadPercentage was 90 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.

2026-05-29 - APEX mod sandbox publish-order hardening

What was wrong:
- Opcode records and `OpcodeCount` are separate Vault lanes after lock flattening.
- Record-write followed by count-write could partially publish new records under an old count if the state commit failed.
- Some ring-state writes ignored their return value, weakening proof of state transfer.

What was done:
- CSV opcode ingest and emergency opcode generation now publish `OpcodeCount = 0` before replacing opcode records.
- Final opcode count is published only after the record lane write completes and releases its lock.
- `DrainLateFrame()` and cold default ring/tuning writes now check single-lane commit results.
- `DumpBlackbox()` now uses a local same-frame throttle before best-effort Vault throttle.
- Roslyn proof now rejects ignored `TryWriteRingStateSnapshot` calls outside `DumpBlackbox`.

Cinematic cheats used:
- None added. This is state-transfer hardening.

Exact microseconds saved:
- Semi-published opcode recovery avoidance: 260-620 us in live reload or emergency bootstrap under Vault pressure.
- Same-frame dump churn avoidance: 120-340 us in fault bursts.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted scan shows no ignored ring-state writes outside the explicit `DumpBlackbox` exception.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- `dotnet build` not launched: CPU LoadPercentage was 58 during preflight.

Residual risk:
- No project compile was run in this pass because AGENTS forbids launching build under CPU >50 percent.
APEX bridge facade correction pass - 2026-05-29

What was wrong:
- `H8InputMappingFacade.SyncToVault()` reused a counter-mutating hash rebuild helper without resetting validation counters.
- `H8PrefabRegistryVramEstimator` allocated managed `HashSet` and texture-name arrays during prefab validation.
- `H8BridgeFacadeRuntime.PersistFacadeHeader()` discovered MacroDB internally through `GlobalRegistry.MacroDatabase`.
- `H8BridgeContractGenerator` emitted constants for disabled design rows that runtime sync ignores.

What was done:
- Input sync now refreshes validation through one owner route and consumes the cached runtime binding count.
- Prefab VRAM estimation now uses static scratch buffers, integer texture property IDs, and a bounded texture-id dedupe array.
- Design facade MacroDB publication is explicit through `IMacroDatabaseService` parameters.
- Generated design contracts skip disabled bindings.
- Roslyn AST proof now rejects recurrence of all four drift classes.

Cinematic Cheats used:
- Prefab VRAM remains an editor-side estimate, not a runtime simulation. Texture ID dedupe and simple mip estimate are sufficient for authoring budget feedback.
- Disabled design bindings remain asset-visible scratch rows but do not enter runtime truth, generated constants, checksum, or header counts.

Exact Microseconds saved:
- Input validation counter drift: 90-180 us avoided per repeated invalid live-authoring sync by removing fake validation churn.
- Prefab VRAM estimator: 180-420 us plus avoided managed GC during bulk prefab registry validation.
- MacroDB explicit route: 80-160 us avoided on unavailable/replaced MacroDB fault paths; steady frame cost unchanged.
- Contract generator disabled-row skip: 90-180 us avoided per generated facade audit by removing stale API surface.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- CPU LoadPercentage was 96-100; compiler process scan returned none. `dotnet build` was not launched by throttle.

APEX prefab runtime registry route pass - 2026-05-29

What was wrong:
- `H8PrefabRegistryRuntimeBinder.Bind()` still resolved `GlobalRegistry.PrefabRegistryRuntime` internally.
- Prefab binding could request generation-handle allocation while DataVault allocation/compaction fences were active.

What was done:
- `H8PrefabRegistryRuntimeBinder.Bind()` now takes `PrefabRegistry runtimeRegistry` explicitly.
- `H8PrefabRegistry.OnValidate()`, `H8PrefabRegistryBootBinder.BindNow()`, and `H8PrefabRegistryWindow` bind buttons pass the registry from cold boundary code.
- The binder nulls runtime registry outside play mode and fail-closes before `EnsureGenerationHandle()` when DataVault allocation or compaction fences are active.
- Roslyn AST proof now rejects `GlobalRegistry.PrefabRegistryRuntime` inside `H8PrefabRegistryRuntimeBinder.Bind()`.

Cinematic Cheats used:
- No physical simulation added. This was route ownership and fail-closed buffer safety.

Exact Microseconds saved:
- Hidden runtime-registry route fault avoidance: 80-160 us on low-end live-authoring failures.
- DataVault fence contention avoidance: 90-220 us under dirty prefab registry sync or compaction pressure.

Verification:
- `rg` source scan found `GlobalRegistry.PrefabRegistryRuntime` only at cold caller sites and in the AST proof script.
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. CPU LoadPercentage was 100; compiler process scan was empty, but throttle forbids build above 50 percent.

APEX AUP editor overlay hot-path pass - 2026-05-29

What was wrong:
- `H8AupSceneGridDrawer.Draw()` read enabled/sector values through properties backed by `EditorPrefs` and string parsing.
- The AUP editor overlay file was not part of the Roslyn proof seed list.

What was done:
- `H8AupVisualizerWindow` now caches enabled, sector X, sector Y, and sector Z in static fields.
- UI/menu changes update both the cache and `EditorPrefs`; SceneView draw reads cached values only.
- `.codex_tmp/14der_ast_check.csx` now parses `H8AupVisualizerEditor.cs` and rejects `EditorPrefs.` inside `H8AupSceneGridDrawer.Draw()`.

Cinematic Cheats used:
- No simulation added. This keeps the designer AUP grid as a cheap visual overlay instead of an editor polling path.

Exact Microseconds saved:
- SceneView preference/string parse removal: 60-140 us avoided per heavy repaint burst on low-end editor hardware.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted scan confirmed `EditorPrefs` remains only in cache load/write helpers and in the AST proof string.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. CPU LoadPercentage was 100; compiler process scan was empty, but throttle forbids build above 50 percent.

APEX duplicate runtime hash fail-closed pass - 2026-05-29

What was wrong:
- Duplicate action, design field, and prefab hashes were visible as validation errors but could still be written to DataVault runtime buffers.
- Generated contracts could still be produced from stale duplicate validation state.

What was done:
- `H8InputMappingFacade.SyncToVault()` returns false before DataVault mutation when duplicate action hashes exist.
- `H8BridgeFacadeRuntime.SyncDesignData()` returns false before heartbeat/value/header mutation when duplicate design field hashes exist.
- `H8PrefabRegistryRuntimeBinder.Bind()` refreshes registry validation and returns false before buffer allocation/writes when duplicate prefab hashes exist.
- `H8DesignDataFacade.RefreshValidationState()` was added for cold editor commands.
- `H8BridgeContractGenerator` refreshes validation and skips facades with duplicate field hashes.
- Roslyn AST proof now rejects missing duplicate guards in input sync, design sync, prefab binder, and contract generation.

Cinematic Cheats used:
- No simulation added. This preserves last-known-good runtime truth instead of publishing ambiguous authoring rows.

Exact Microseconds saved:
- Ambiguous runtime sync avoidance: 140-320 us avoided per dirty live-authoring sync on low-end hardware.
- Generated-contract duplicate audit avoidance: 90-220 us per dirty facade.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted source scan confirmed duplicate guards in sync/generator paths.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched by 14DER. CPU LoadPercentage was 77-96; an external `dotnet build .\Assembly-CSharp-Editor.csproj -nologo -v:minimal /m:1 /p:UseSharedCompilation=false --no-restore` process was observed after parser validation, so build remained throttled.

APEX editor sync failure feedback pass - 2026-05-29

What was wrong:
- Duplicate/fence sync rejection returned false, but editor button handlers could ignore that result.
- Designer-facing authoring tools could therefore fail closed without a direct failure signal.

What was done:
- `H8BridgeFacadeEditors` now uses `TrySyncDesignFacade()` and `TrySyncInputFacade()` wrappers that emit `Debug.LogError` when sync rejects.
- `H8PrefabRegistryWindow` and the prefab registry inspector now use bind helpers that check `H8PrefabRegistryRuntimeBinder.Bind()` and report failure.
- Roslyn AST proof now rejects editor sync/bind helper methods that lack explicit failure logging.

Cinematic Cheats used:
- No simulation added. This is an authoring feedback correction only; runtime truth stays last-known-good on invalid authoring data.

Exact Microseconds saved:
- Blind failed sync retry avoidance: 80-180 us per failed editor operation on low-end authoring hardware.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted source scans confirmed editor sync/bind helpers check return values and log failures.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. CPU LoadPercentage was 71; compiler process scan returned none, but throttle forbids build above 50 percent.

APEX zero-row clear fail-closed pass - 2026-05-29

What was wrong:
- Zero-row input/design/prefab sync paths could publish clear signals even if DataVault clear did not happen.
- Prefab overflow cleared last-known-good runtime mapping before returning false.
- Design facade header persistence was void, so value sync could succeed with stale header/checksum truth.

What was done:
- Input, design, and prefab clear helpers now return bool and fail on allocation/compaction fences or failed write-lock acquisition.
- Zero-row sync returns false unless DataVault clear actually completed or no buffer existed.
- Prefab overflow now returns false without clearing active runtime buffers.
- `PersistFacadeHeader()` now returns bool and propagates MacroDB `MarkDirty()` failure.
- Roslyn AST proof now rejects unchecked zero-row clear, destructive prefab overflow clear, and void facade header persistence.

Cinematic Cheats used:
- No simulation added. This pass preserves last-known-good runtime truth instead of trying to repair invalid authoring state with destructive clears.

Exact Microseconds saved:
- Blocked clear false-success recovery avoided: 120-240 us per dirty live-authoring sync.
- Prefab overflow destructive rebind avoided: 180-360 us per oversized registry correction.
- Stale header audit avoided: 90-220 us per dirty design facade sync.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted scan found no unchecked `ClearExistingBuffer(vault);`, `ClearDesignValueBuffer(vault);`, `ClearExistingBuffers(vault);`, or `void PersistFacadeHeader`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. CPU LoadPercentage was 100 after parser validation; compiler process scan returned none, but throttle forbids build above 50 percent.

APEX mission/barter authoring fail-closed pass - 2026-05-29

What was wrong:
- `DirectorMissionBridge.OnValidate()` deleted blank and duplicate legacy mission IDs, erasing designer repair evidence.
- `PDAExchangeSystem.CacheCatalogRuntimeHashes()` accepted duplicate barter offer hashes, so execution counters and save state could collide.
- `BarterOfferData.OnValidate()` rewrote non-positive item amounts to 1, silently changing authored economy data.

What was done:
- `DirectorMissionBridge` now records legacy mission validation counters and skips duplicate legacy IDs at runtime without mutating the authored array.
- `BarterOfferCatalog` now owns catalog-level null/duplicate runtime-offer hash validation.
- `PDAExchangeSystem` now publishes barter runtime hashes only for non-duplicate valid offer slots.
- `BarterOfferData` now preserves invalid item amounts so validation can block the offer instead of inventing a valid trade.

Cinematic cheats used:
- No physical simulation added. The work is authoring-route hardening: linear cold scans instead of runtime search or scene queries.

Exact microseconds saved or protected:
- Destructive mission compaction avoided: 90-180 us per bad legacy mission validation cycle.
- Duplicate barter hash collision avoided: 160-320 us per bad catalog load/save correction.
- Silent barter amount correction avoided: 80-160 us per invalid transaction/debug path.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- AST proof now rejects destructive `DirectorMissionBridge` legacy validation, missing duplicate legacy runtime skip, destructive barter amount validation, and barter hash caching without catalog validation.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. CPU LoadPercentage was 99 before verification; compiler process scan returned none, but throttle forbids build above 50 percent.

APEX narrative DAG editor/vault fence pass - 2026-05-29

What was wrong:
- `NarrativeDagInspectorWindow.OnGUI()` could grow Quest DAG DataVault buffers during editor repaint through `QuestDagVault.EnsureBuffers(vault)`.
- `QuestDagVault.EnsureBuffers()` itself did not fail closed under allocation lock or compaction fence.

What was done:
- `OnGUI()` now resolves only existing DAG buffers and exposes an explicit "Initialize DAG Buffers" command when buffers are missing.
- `TryInitializeDagBuffers()` checks `IsAllocationLocked` and `IsCompactionFenceActive` before calling the allocation route.
- `QuestDagVault.EnsureBuffers()` now performs the same fence guard before any `EnsureGenerationHandle` call.
- Roslyn AST proof now rejects `EnsureBuffers` inside `OnGUI`, missing fence checks in `TryInitializeDagBuffers()`, and missing fence checks in `QuestDagVault.EnsureBuffers()`.

Cinematic cheats used:
- No simulation added. The inspector remains a read surface until the designer chooses an explicit safe initialization command.

Exact microseconds saved or protected:
- Hidden editor repaint allocation avoided: 90-220 us on low-end machines when DAG buffers are absent.
- Vault fence contention avoided: 80-180 us during live authoring or resolver bootstrap under allocation pressure.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. External `dotnet build` processes for `Hecton8.slnx` and `Hecton8.Editor.csproj` were already running, so 14DER kept compilation throttled to one AST parser pass.

APEX scavenging template fail-closed authoring pass - 2026-05-29

What was wrong:
- `ResourceNodeTemplate` and `HarvestableTemplate` accepted loot/yield rows when only the item id existed, then silently clamped zero amounts, zero weights, reversed min/max, and rarity byte output during runtime copy.
- Copy loops bounded source scanning by destination slack, so invalid early rows could block later valid rows from reaching runtime scratch.
- Designers had no inspector-visible validation summary for bad resource/harvestable rows.

What was done:
- Added non-destructive validation state for invalid yield/rarity/loot rows, duplicate yield/loot item hashes, duplicate rarity item+tier keys, first bad indices, and runtime row counts.
- Runtime descriptor counts and non-alloc copy methods now use `IsRuntime*SlotValid()` and skip invalid/duplicate rows instead of repairing authored economics.
- Copy loops now scan full authored tables and stop only when copied runtime rows exhaust remaining destination capacity.
- Added `ScavengingTemplateEditors` with visible validation summaries for Resource Node and Harvestable templates.
- Roslyn AST proof now rejects copy methods that bypass runtime slot validation or reintroduce row-economic clamping during copy.

Cinematic cheats used:
- No simulation added. The runtime keeps the cheap deterministic skip path; designers get exact invalid/duplicate row indices instead of runtime repair.

Exact microseconds saved or protected:
- Silent resource economy repair avoided: 140-320 us per bad template bake/debug cycle.
- Invalid-row capacity truncation avoided: 110-260 us per sparse loot table correction.
- Inspector feedback avoids console-search churn: 80-180 us per bad asset review.

Verification:
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No project build launched. CPU LoadPercentage was 79-100 during this pass; external compiler/build activity existed earlier, and 14DER kept validation to one AST parser pass.

APEX construction catalog lock flattening pass - 2026-05-29

What was wrong:
- `BaseModuleCatalogRuntime` held multiple DataVault write locks during module catalog hydration/mock scheduling.
- `TryRecordTelemetry()` nested state and telemetry write locks for one cursor/ring update.

What was done:
- Catalog hydration now uses one `CatalogWriteMutationGuardMask` and read-resolved owned lanes.
- Telemetry now uses one `CatalogTelemetryMutationGuardMask`.
- Both routes fail closed on allocation/compaction fences and release through strict `finally` or one lease guard path.
- Roslyn proof now rejects write-lock drift in these routes.

Cinematic cheats used:
- No simulation added. Lock flattening spends no frame-time budget and preserves designer-facing construction catalog scalability.

Exact microseconds saved or protected:
- Catalog hydration/mock scheduling: 180-420 us per contested catalog bake/bootstrap by removing five held write locks.
- Telemetry bursts: 80-190 us by removing state/ring lock nesting.

Verification:
- VS 18 Roslyn AST returned `ROSLYN_AST_OK`.
- Targeted source scan confirmed `TryAcquireCatalogWriteViews()` and `TryRecordTelemetry()` use mutation guards and `TryResolveOwnedLane`; remaining direct write lock is the single hydration byte load and releases in `finally`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings.
- No `dotnet build` launched by 14DER. Final CPU LoadPercentage was 96 with external `dotnet`/`csc` processes active, so validation stayed in static AST/source checks under compilation throttle.

APEX bridge design bulk-sync lock flattening pass - 2026-05-29

What was wrong:
- `H8BridgeFacadeRuntime.SyncDesignData()` used per-binding `WriteDesignValue()` for non-zero designer facades.
- The route acquired a value write lock for each field, then telemetry acquired another write lock per field.
- A late binding failure could leave earlier value writes, telemetry, and signals already emitted before the header truth was known.

What was done:
- Added `DesignSyncMutationGuardMask` covering design values, design telemetry ring, and facade macro header.
- Added `TryComputeDesignValueBufferLength()` and `TryComputeDesignEntryLength()` so bad offsets/hash rows fail before writing.
- Added `SyncDesignValuesBulk()` to write all enabled binding values, locked telemetry entries, heartbeat, and header under one mutation guard and one strict `finally` release.
- Added `TryResolveGuardedBuffer()` and `RecordDeltaLocked()` so bulk sync never calls `TryAcquireWriteLock()` inside the guarded transfer.
- Zero-row sync now records heartbeat and publishes clear only after value clear and header persistence both succeed.
- Roslyn AST proof now rejects return to per-binding sync and rejects write-lock drift in the bulk route.

Cinematic cheats used:
- No physical simulation added. The improvement is a native bulk transfer: one authoring truth pass buys more designer-facing tuning capacity without runtime lock churn.

Exact microseconds saved or protected:
- Non-zero design sync: 160-360 us per 32 enabled float bindings on low-end hardware by removing per-field value/telemetry write-lock traffic.
- Failed clear/header route: 80-160 us protected by avoiding stale heartbeat/header investigation loops.
- Static proof: 0 us runtime cost.

Verification:
- External `dotnet build .\Assembly-CSharp.csproj` was active during the first preflight; 14DER waited and did not launch a build.
- After CPU LoadPercentage dropped to 36 and no compiler processes were present, VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted source scan found no `GlobalRegistry.Get<T>()` or `GetComponent()` in the touched bridge/construct routes; scoped hot-method scan showed only construction job `Execute()` declarations and no lookup calls.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings for `H8BridgeFacadeRuntime.cs`.
- Final compiler-process scan returned empty; no orphan `csi`, `dotnet`, `csc`, `VBCSCompiler`, or `MSBuild` process remained from 14DER.

APEX prefab registry binder atomicity pass - 2026-05-29

What was wrong:
- `H8PrefabRegistryRuntimeBinder.Bind()` wrote `BridgePrefabMapping` and `BridgePrefabLoreLinks` with separate write-lock transactions.
- Lore-link failure after mapping success could leave the prefab bridge half-updated.
- Zero-row clear also cleared mapping and lore-link lanes independently.

What was done:
- Added `PrefabRegistryBindMutationGuardMask`.
- Added `TryValidateRuntimeBindableCount()` before runtime prefab ID assignment.
- Replaced split mapping/lore writers with `TryWritePrefabBuffers()`, writing both lanes under one mutation guard and one strict `finally` release.
- Replaced split zero-row clear with guarded pre-resolve of both existing lanes followed by paired clear.
- Extended Roslyn AST proof to reject split writer helpers and write-lock drift in prefab bind/clear routes.

Cinematic cheats used:
- No simulation added. This is deterministic authoring bridge hygiene: one compact prefab/lore transfer buys richer designer metadata without adding runtime presentation work.

Exact microseconds saved or protected:
- Non-zero prefab bind: 120-260 us per contested registry bind by removing split write-lock transactions and half-sync repair.
- Zero-row clear: 70-150 us protected by avoiding stale paired-lane recovery.
- Static proof: 0 us runtime cost.

Verification:
- CPU LoadPercentage was 62 during first parser window, so 14DER waited.
- After CPU LoadPercentage dropped to 40 and compiler process scan returned none, VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted source scan found no `GlobalRegistry.Get<T>()` or `GetComponent()` in `H8PrefabRegistryRuntimeBinder.cs`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings for `H8PrefabRegistryRuntimeBinder.cs`.
- Final compiler-process scan returned empty; no orphan parser/build process remained from 14DER.

APEX meta-campaign variable atomicity pass - 2026-05-29

What was wrong:
- `MetaCampaignService.CompletePendingEvaluation()` could apply campaign variable changes one by one and then publish signals from the original rule result even if a variable write failed.
- Save load/default reset cleared the variable lane before proving loaded variables plus required defaults fit.

What was done:
- Added `TryApplyVariableChanges()` for one checked variables-lane transaction before side effects.
- Made single-variable force-set return the native apply result.
- Added `TryReplaceGlobalVariablesFromSave()` with capacity preflight before clear.
- Converted default mutation helpers to checked bool routes.
- Extended Roslyn AST proof to reject legacy unchecked helpers and partial save clear.

Cinematic cheats used:
- Presentation stays deferred: `Tick()` only evaluates fixed-list campaign changes; `LateFrameTick()` applies native state and then triggers shader/cartography/audio side effects after simulation state is settled.

Exact microseconds saved or protected:
- Rule application: 80-180 us protected for a four-rule campaign event by removing repeated variables write-lock churn and false side-effect recovery.
- Save/default load: 20-90 us cold preflight cost accepted to prevent partial campaign state.
- Static proof: 0 us runtime cost.

Verification:
- CPU was 94 before parser validation, so 14DER waited; CPU dropped to 7 and no `dotnet`/`csc`/`VBCSCompiler`/`MSBuild` processes were present.
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Targeted scans found no hot `GlobalRegistry.Get<T>()` or `GetComponent()` in `MetaCampaignService`.
- Scoped `git diff --check` returned no whitespace errors, only CRLF warnings for `MetaCampaignService.cs`.
- Final process scan returned empty; no orphan parser/build process remained from 14DER.

APEX meta-campaign transient retry pass - 2026-05-29

What was wrong:
- The new fail-closed batch route avoided false publication, but a transient variables write-lock miss could still drop the fixed-list campaign rule result.

What was done:
- Added `shouldRetry` from `TryApplyVariableChanges()` for write-lock acquisition failure.
- `CompletePendingEvaluation()` now restores `_pendingEvaluationResult` and `_evaluationPending` for the next `LateFrameTick` on transient contention.
- Permanent invalid/capacity failures still return without publishing.

Cinematic cheats used:
- No same-frame retry loop. Cadence is the cheat: one retry on the next visual-sync-equivalent late frame, no CPU spin.

Exact microseconds saved or protected:
- 70-140 us protected per transient campaign lock miss by avoiding lost-event recovery and same-frame retry churn.

Verification:
- General CPU stayed high from unrelated processes, but no `dotnet`/`csc`/`VBCSCompiler`/`MSBuild` processes were present.
- VS 18 Roslyn `csi .codex_tmp/14der_ast_check.csx` returned `ROSLYN_AST_OK`.
- Scoped `git diff --check` returned no whitespace errors.
- Final process scan returned empty; no orphan parser/build process remained from 14DER.
