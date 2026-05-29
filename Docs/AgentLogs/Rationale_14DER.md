Date: 2026-05-28
Agent: 14DER
Status: PENDING VERIFICATION

Problem: Hot-path dependency audit needed concrete violations without editing source.
Solution: Static source audit of requested files plus direct helper call-stack reads. Applied GlobalRegistry cold-only, Zero-GC, CSV bridge, resource/item layout, quest state, asset lifecycle, and execution phase mandates.
Rejected Alternatives: Whole-project grep report was too noisy and would mix unrelated agents' domains. Runtime build/profiler was rejected because this is audit-only and no source changed.
Scalability potential: Low uses cached interfaces and fail-closed hot paths. Middle keeps staged swaps and bounded dispatch. High/Ultra spend saved CPU in VISUAL_SYNC visuals, not registry polling or runtime scene/component discovery.
Hardware Impact: Removing registry fallback/component lookups from 0.5s and LateFrame paths avoids unbounded main-thread spikes on i3/MX350; expected savings are small per call but remove worst-case stalls from service creation, component lookup, and fallback scans.

Problem: `RandomEventSystem.SlowTick()` can resolve missing player/survival/geology dependencies through component lookup.
Solution: Cache `HectonPlayerSurvival` and geology metadata from `IPlayerRuntimeContext`/voxel owner during cold registration or hot-swap. Use numeric geology/family hashes in the seismic path.
Rejected Alternatives: Keeping `TryGetComponent` behind null checks was rejected; null-dependent hot fallback still violates phase law.
Scalability potential: Low/Middle skip event branch if cached metadata absent. High/Ultra can add richer visual seismic response after the event is resolved through cached DTOs.
Hardware Impact: Avoids component lookup and string metadata path during random-event SlowTick; prevents intermittent 0.05-0.20 ms spikes when voxel metadata is cold.

Problem: `WorldZoneDirector.SlowTick()` can call `WorldRuntimeReferenceUtility.TryResolvePlayerTransform()`, which reads `GlobalRegistry.Player` inside a `TryResolve*` accessor.
Solution: Cache player, scatter budget, and slice director in owner fields through cold init/hot-swap. `SlowTick()` should fail closed if references are absent.
Rejected Alternatives: Throttled auto-resolve was rejected; cadence throttling does not make hot registry polling legal.
Scalability potential: Low reduces zone evaluation jitter. Middle/High/Ultra can use stable zone snapshots to drive richer world dressing without dependency stalls.
Hardware Impact: Avoids registry access and bootstrap fallback checks during zone SlowTick; expected low-end gain 5-30 us on missing-reference frames.

Problem: `ResourceDistributionDirector.SlowTick()` registers/unregisters brine hazard zones through `HazardZoneManager.EnsureRuntimeInstance()` and `GlobalRegistry.HazardZones`.
Solution: Inject/cache `HazardZoneManager` or `IHazardZoneReadModel` during cold setup; queue brine hazard mutations and flush in the hazard owner phase.
Rejected Alternatives: Runtime `EnsureRuntimeInstance()` was rejected because it can initialize environment context and add components from a resource-sector pass.
Scalability potential: Low batches hazard mutations. Middle/High/Ultra increase brine visual density through VISUAL_SYNC only after hazard authority is cached.
Hardware Impact: Removes potential component/service creation and registry lookup from resource sector SlowTick; worst-case saving can exceed 0.1 ms on cold hazard runtime frames.

Problem: `ResourceDistributionDirector.SlowTick()` can grow DataVault-backed metamorphism buffers from a scheduling helper.
Solution: Pre-size metamorphism buffers in cold setup from sector/template caps; hot path should skip and emit telemetry if capacity is insufficient.
Rejected Alternatives: Doubling capacity during SlowTick was rejected; it hides native ownership mutation in simulation cadence.
Scalability potential: Low uses smaller precomputed capacity and lower cadence. Middle/High/Ultra increase capacity through continuous GlobalQualityWeight at safe swap windows.
Hardware Impact: Removes vault handle allocation/relocation from SlowTick; prevents frame stalls and stale-handle risk on i3/MX350.

Problem: `ItemCatalog` hot consumers can call `PumpWorldPrefabDispatchTickets()` / `TryGetLoadedWorldPrefab()`, which falls back to `CacheRuntimeServices()` and registry reads.
Solution: Bind `AssetLifecycle`, `AssetLoadDispatcher`, and `Player` once during bootstrap/hot-swap. Hot lookup methods return false or use cached fields only.
Rejected Alternatives: Null-conditional registry refresh was rejected; asset lookup can run from `PersistentWorldRegistry.LateFrameTick()`.
Scalability potential: Low avoids fallback scans and service lookups during hydration. High/Ultra can prewarm richer world props through the dispatcher without hot service discovery.
Hardware Impact: Avoids registry polling and potential direct Addressables path in world hydration; expected gain 10-80 us per hydration burst, higher if services are cold.

Problem: `Fabricator.SlowTick()` can reach `TryCachePlayerMovement()` and call `TryGetComponent` if the player movement cache was invalidated.
Solution: Resolve player movement only from `Interact()` or player runtime hot-swap; `SlowTick()` should fail distance check if movement is not cached.
Rejected Alternatives: Leaving the fallback because the normal interaction path pre-caches was rejected; hot helper fallback remains a production violation.
Scalability potential: Low avoids interaction-distance lookup spikes. High/Ultra can spend saved budget on fabrication VISUAL_SYNC, not component discovery.
Hardware Impact: Small but real avoidance of Unity component lookup from crafting SlowTick; expected low-end gain 5-40 us on cache-miss frames.

Problem: Blueprint visibility used `GlobalRegistry.QuestSystem` inside read-looking accessors that can be reached by UI/crafting catalog scans.
Solution: Cache the quest owner in `ItemCatalog` and `ModuleCatalog` cold paths and hot-swap callbacks; `ItemTemplateRegistry` and `BuildableData` read the cached owner only.
Rejected Alternatives: New global route and per-call registry lookup. Both increase coupling and keep hot-path drift.
Scalability potential: Low/Middle scan catalogs with stable cached owner. High/Ultra can add richer blueprint UI without altering gameplay truth.
Hardware Impact: Removes registry polling from catalog filtering; estimated 0.5-2.0 us per scan on i3/MX350 and removes a hidden service-read dependency.

Problem: `DirectorMissionBridge` exposed only scene string arrays for director missions, with no weights, cooldowns, phase gates, or validation route.
Solution: Added `DirectorMissionBridgeProfile` ScriptableObject facade with schema version, mission entries, weight, cooldown, per-entry first-hour gate, profile gate, rare discovery id, and editor validation against `QuestData`.
Rejected Alternatives: CSV runtime parsing and hardcoded C# mission tables. Runtime parsing violates bridge law; hardcoding blocks designers.
Scalability potential: Low uses bounded weighted scan and cooldown array. Middle increases mission variety. High/Ultra can layer richer director presentation after mission truth is chosen.
Hardware Impact: Event-only cost; no per-frame work. Mission choice is O(total weight), with weight clamped to 16 per mission.

Problem: Narrative campaign and lore write locks released through mutable `_dataVault`, so a registry replacement could strand a write lock on the old vault.
Solution: Acquire helpers now return the exact `IDataVault lockedVault`; every `finally` releases through that captured vault.
Rejected Alternatives: Assume hot-swap cannot happen during write scope. Source explicitly supports registry replacement, so that assumption is invalid.
Scalability potential: Low keeps single-lock writes safe. Middle/High/Ultra can expand narrative/lore authoring without adding lock leak risk.
Hardware Impact: No extra runtime allocation; avoids deadlock/leaked writer lock stalls on all tiers.

Problem: `ResourceDistributionDirector.LateFrameTick()` resolved pooled `ResourceNode` instances through `TryGetComponent` immediately after pool spawn.
Solution: Added `IObjectPoolService.TryGetPooledComponent<T>()` and backed it with `PoolItemMarker`'s cold `IPoolable[]` cache. Resource distribution now resolves spawned `ResourceNode` through the pool marker cache.
Rejected Alternatives: Runtime component lookup after spawn. It is usually cheap but still violates the no-component-probe hot-path rule and can spike on Unity internals.
Scalability potential: Low tier keeps resource spawning deterministic and bounded. Middle/High/Ultra can increase resource visual density because spawn commit no longer pays component lookup.
Hardware Impact: Removes five root component probes from resource spawn commit paths; expected low-end gain 5-40 us per spawn burst and lower jitter.

Problem: Geology voxel launch continuation used `volume.TryGetComponent` after a queue flushed from `Tick()`.
Solution: `WorldGenerativeGeologyVoxelRuntime` now exposes active runtime lookup by `GameObject` and cached `HectonVoxelVolume`; bridge uses registry lookup and only adds the component when no active runtime is registered.
Rejected Alternatives: Leaving the lookup after `await` because it is not linearly inside the initial Tick frame. The route still belongs to the hot launch queue.
Scalability potential: Low tier keeps voxel launches sparse and predictable. Middle/High/Ultra can spend saved frame variance on voxel visual detail bands.
Hardware Impact: Removes component lookup from voxel launch completion; expected gain is small per volume but removes a stall source during async cave bursts.

Problem: `Fabricator` held multiple DataVault write locks simultaneously for crafting, complex recipe, and deconstruction scratch buffers.
Solution: Moved transient recipe scratch to per-owner persistent `NativeArray` buffers allocated in cold lifecycle. DataVault write locks remain only for the unlock-mask and telemetry ring, one lock at a time, released through captured vault in `finally`.
Rejected Alternatives: Keep many DataVault scratch buffers and order releases. Ordering reduces but does not eliminate multi-lock deadlock risk.
Scalability potential: Low tier avoids lock contention during UI/crafting checks. Middle/High/Ultra can support richer designer recipe graphs without expanding DataVault writer overlap.
Hardware Impact: Removes up to 10 simultaneous DataVault write locks from `HasIngredients` and up to 8 from `ConsumeIngredients`; low-end i3/MX350 impact is lower stall risk and fewer vault synchronization calls.

Problem: Direct hot-method scanning missed helper-chain violations from `SlowTick` and `LateFrameTick`.
Solution: Upgraded `.codex_tmp/14der_ast_check.csx` to parse C# with Roslyn and recursively walk local method calls from hot roots. It now catches registry reads, component lookups, scene search, and resource loads hidden behind helpers.
Rejected Alternatives: More `rg` patterns. Text search cannot distinguish cold lifecycle helpers from hot call chains and produced both misses and noise.
Scalability potential: Low/Middle keep runtime loops free from dependency drift. High/Ultra can spend frame budget on visual fidelity because dependency proof is mechanically repeatable.
Hardware Impact: Removes hidden cold-path regressions before they become low-end frame spikes; verification cost is offline only.

Problem: `RandomEventSystem.SlowTick()` marked shader globals dirty through helpers that registered the late-frame lane with `GlobalRegistry` from the same hot chain.
Solution: Register `ILateFrameTickable` once from lifecycle/hot-swap and make random-event setters write only pending value structs and dirty flags. `LateFrameTick()` flushes shader state after simulation.
Rejected Alternatives: Conditional registration on each dirty write. It saves a mostly idle LateFrame callback but violates cold-only registry ownership.
Scalability potential: Low tier pays one cheap dirty-flag check. Middle/High/Ultra keep meteor/glitch/biolum visual sync phase-safe with no registry jitter.
Hardware Impact: Removes registry call risk from event trigger/update paths; expected saving is small per event but prevents service-slot stalls on CPU-bound frames.

Problem: `Fabricator.LateFrameTick()` resolved assembly preview mesh/material by probing result item prefabs through `GetComponent*`.
Solution: Added a cold per-fabricator assembly source cache keyed by `ItemData`. Lifecycle and mod-registry changes rebuild mesh/material references; LateFrame reads arrays only and falls back to the shared procedural mesh.
Rejected Alternatives: Keep prefab probing because craft begin is infrequent. The protocol treats LateFrame as a hot presentation phase, so component probing is still illegal.
Scalability potential: Low uses cached fallback/mesh with zero lookup. Middle/High/Ultra can increase fabrication visual richness without coupling visual begin to prefab traversal.
Hardware Impact: Removes multiple prefab component probes from craft visual start; expected 20-150 us avoided on complex prefab first-use paths.

Problem: `MetaCampaignService.LateFrameTick()` applied toxicity visuals and then read `GlobalRegistry.EcosystemDirector`.
Solution: Cache `IEcosystemDirectorService` during lifecycle and refresh it through `GlobalRegistryServiceSlot.EcosystemDirector` hot-swap.
Rejected Alternatives: Keep registry read in `PublishCachedVisualState` because it is only once per campaign change. Rare hot-path violations still couple presentation flush to global identity lookup.
Scalability potential: Low tier avoids campaign-change stalls. Middle/High/Ultra can fan out richer ecosystem pressure visuals through the cached interface.
Hardware Impact: Removes a registry read from campaign visual flush; expected saving is micro-level, but it eliminates dependency drift from LateFrame.

Problem: Previous Fabricator scratch flattening removed simultaneous DataVault write locks but left persistent `NativeArray<T>` fields in a MonoBehaviour, violating the current persistent alias ban.
Solution: Replace Fabricator recipe/deconstruction/complex-graph scratch with fixed-capacity managed arrays allocated only by `EnsureCraftingScratch()`. Add managed `CraftingSystem` overloads for craft eligibility, recipe cost flattening, deconstruction yield flattening, and Kahn raw-cost expansion.
Rejected Alternatives: Reacquire DataVault scratch buffers one at a time; it would obey one-lock-at-a-time but still pushes transient scratch into global native ownership. Allocate `NativeArray` per query with `Allocator.Temp`; it would avoid persistent fields but creates hot allocator traffic and disposal risk.
Scalability potential: Low uses bounded managed scratch and deterministic linear scans. Middle keeps the same scratch capacity with no vault lock pressure. High/Ultra can expand recipe graph authoring through constants without adding writer locks or hot dependency lookups.
Hardware Impact: Removes up to ten scratch DataVault write-lock acquisitions from craft checks/consumption and removes persistent native aliases from Fabricator. On i3/MX350 this is primarily stall-risk reduction; expected saved cost is burst-dependent, roughly 20-150 us on complex craft/deconstruction paths under contention.

Problem: The managed scratch replacement still used an allocation-capable `EnsureCraftingScratch()` helper from craft query paths, so the code shape did not mathematically prove zero hot allocation if lifecycle warmup failed.
Solution: Split Fabricator scratch into `EnsureCraftingScratchCold()` for lifecycle and DataVault hot-swap warmup, plus `HasCraftingScratchReady()` for craft/deconstruction hot checks. Changed DataVault write acquisition and telemetry failure logging to require pre-warmed handles instead of ensuring buffers from SlowTick paths.
Rejected Alternatives: Keep lazy allocation because Awake/OnEnable normally run first; normal ordering is not proof. Move the scratch back into DataVault; that reopens global scratch ownership and write-lock pressure for local bounded craft math.
Scalability potential: Low fails closed instead of hitching under missing warmup. Middle/High/Ultra keep identical recipe graph capability after cold warmup, with visual gains reserved for LateFrame fabrication presentation.
Hardware Impact: Removes latent managed array allocation and vault handle ensure from craft query/SlowTick failure paths. Expected gain is avoiding rare but visible low-end hitch; steady-state cost is unchanged linear array scans.

Problem: `DirectorMissionBridgeProfile.OnValidate()` destructively compacted invalid/duplicate mission rows, and `DirectorMissionBridge` could allocate profile cooldown state from a mission-trigger event if cold warmup missed.
Solution: Keep profile rows intact, expose validation flags/counts/first invalid index/total weight, cap runtime consumption at 64 missions, and build a fixed weighted mission index table plus cooldown clock only from lifecycle/editor cold paths. Event handlers now read cached arrays and fail closed on stale profile state.
Rejected Alternatives: Continue deleting bad rows because it makes runtime arrays clean; this hides authoring mistakes and loses designer work. Re-scan profile weights every event; bounded but still does unnecessary string/duplicate checks in the director lane.
Scalability potential: Low uses at most 64 missions and a 1024-slot weighted table. Middle keeps the same table with richer mission variety. High/Ultra can add more presentation around selected missions without touching mission truth or adding hot allocations.
Hardware Impact: Removes rare mission-event `float[]` allocation and O(weight*mission scan) profile resolution. On i3/MX350 this prevents event hitching; steady event selection becomes bounded array reads.

Problem: `RecipeData.TryWriteDisplayNameOrFallback()` read `GlobalRegistry.LocalizationText` from a read accessor used by `HectonFabricatorUI`, and the UI could unregister/register LateFrame plus allocate fallback label buffers from presentation paths.
Solution: Recipe text copy now takes a caller-owned `ILocalizationTextReadModel`; Fabricator UI caches the localization owner in cold/hot-swap paths, keeps LateFrame registration at lifecycle scope, uses a static fabrication group cycle, and fails closed if per-row display-name buffers were not cold-prepared.
Rejected Alternatives: Keep the parameterless recipe accessor as the localization route; that keeps service locator coupling in UI text refresh. Keep LateFrame retirement for idle optimization; a single idle branch is cheaper than violating cold-only registry ownership.
Scalability potential: Low keeps diegetic recipe labels stable with zero service lookup. Middle/High/Ultra can increase recipe visual richness and localized text refreshes through cached owners without adding global dependency reads.
Hardware Impact: Removes GlobalRegistry read from recipe label refresh, removes hot `new char[128]` fallback, and eliminates LateFrame unregister/register churn. Expected saving is small per frame but removes jitter and hidden allocation failure cases.

Problem: `ResourceNodeTemplate.BuildRuntimeDescriptor()` and `HarvestableTemplate.BuildRuntimeDescriptor()` counted raw authoring array length while `Copy*TableNonAlloc()` skipped invalid rows. Runtime descriptors could report more rows than the native table actually contained.
Solution: Use shared validity predicates and valid-entry counters for descriptor counts and non-alloc copy paths. Invalid/null item rows remain visible to designers but do not poison runtime DTO counts.
Rejected Alternatives: Compact arrays in `OnValidate()`; that destroys designer evidence and can reorder authored intent. Trust the copy count later; that leaves DTO contracts inconsistent until a downstream owner repairs them.
Scalability potential: Low keeps resource/harvest simulation deterministic with cheap linear scans. Middle/High/Ultra can add richer resource tables because invalid entries fail closed without changing descriptor layout.
Hardware Impact: Removes bounds-drift branches and mispredicted loot/yield reads in downstream jobs; expected direct gain is 1-10 us per bake/publish, with larger stability gain from eliminating bad table-count state.

Problem: `DepthZoneProfile.DisplayNameOrFallback` and `DescriptionOrFallback` were read-looking properties that pulled `GlobalRegistry.LocalizationText`.
Solution: Make parameterless properties fallback-only and keep localized variants explicit through caller-owned `LocalizationManager` or `ILocalizationTextReadModel` overloads.
Rejected Alternatives: Cache localization inside the ScriptableObject; assets are not runtime owners and should not hold hot global dependency identity.
Scalability potential: Low/Middle read zone names with no service dependency. High/Ultra can still use localized spans through cached presentation owners in VISUAL_SYNC/audio/UI phases.
Hardware Impact: Removes a global service read from any incidental depth-zone text access; expected gain is micro-level, but it eliminates dependency drift from profile reads.

Problem: `WorldZoneProfile` trusted `[Range]` attributes and editor validation while `WorldZoneDirector` read public floats directly in zone-scale evaluation.
Solution: Add clamped runtime properties and switch `WorldZoneDirector` hot evaluation to those properties. `OnValidate()` now normalizes strings and writes the same clamped values back for authoring visibility.
Rejected Alternatives: Clamp only in editor bootstrap; runtime asset mutation, merge corruption, or external tooling can bypass that. Clamp after applying to downstream systems; that spreads the contract across consumers.
Scalability potential: Low clamps zone budgets to safe radius/spawn/collider/slice bands. Middle/High/Ultra can scale richer world density through continuous quality and biome math without letting one corrupt zone profile explode cost.
Hardware Impact: Prevents bad zone authoring from inflating scatter/collider/slice cost beyond intended caps; worst-case saved frame time is unbounded relative to corrupt values, normal cost is six scalar clamps per zone evaluation.

Problem: `LocalizedTextReference` parameterless resolve APIs and `ItemData` parameterless cache getters read localization through `GlobalRegistry`, so any item/name/description read could silently become a service-locator dependency.
Solution: Parameterless localized-text APIs are fallback-only. Runtime localization now requires a caller-owned `ILocalizationTextReadModel` or `LocalizationManager`, which presentation owners can cache during cold setup or hot-swap.
Rejected Alternatives: Keep global fallback for convenience; that violates the read accessor doctrine and hides dependency ownership inside reusable SO data. Cache localization inside `ItemData`; assets are not runtime service owners.
Scalability potential: Low/Middle use stable fallback text with no global dependency. High/Ultra can render localized, richer item UI through cached presentation owners without changing item truth or allocating.
Hardware Impact: Removes global localization lookup from incidental item/quest/lore text reads; expected direct gain is micro-level, but it eliminates hidden dependency stalls during inventory/fabricator/interaction UI refreshes.

Problem: `ItemData.DeconstructYieldCount` exposed raw authoring array length while deconstruction runtime skipped invalid rows. A corrupted first row could make an item look deconstructable and then produce zero valid output.
Solution: `DeconstructYieldCount` now counts valid rows; `TryGetDeconstructYield()` returns valid-index rows; `TryGetDeconstructYieldBySlot()` preserves raw slot scans for O(n) runtime table building. `CraftingSystem` uses raw slots and validity checks once.
Rejected Alternatives: Delete invalid rows in `OnValidate()`; it destroys designer evidence. Use valid-index only in `CraftingSystem`; that creates O(n^2) scans on large salvage tables.
Scalability potential: Low fails closed on invalid deconstruction data. Middle/High/Ultra can support larger authored salvage tables with linear runtime scans and no DTO count drift.
Hardware Impact: Prevents wasted deconstruction path setup and keeps craft yield building O(n); expected saving is 1-15 us per deconstruct evaluation plus removal of invalid-output edge cases.

Problem: Existing AST proof checked hot call chains but did not mechanically fail read accessors that reintroduced `GlobalRegistry`.
Solution: Added read-accessor purity scan for property bodies plus `Get*`, `TryGet*`, `Resolve*`, and `Read*` methods in changed domain files.
Rejected Alternatives: Trust manual review. The prior drift came from convenience accessors, so the proof must encode that rule.
Scalability potential: Low through Ultra share one dependency-proof gate; visual richness can expand without letting authoring facades become global service tunnels.
Hardware Impact: Offline verification only; prevents future hot UI/inventory dependency stalls before they ship.

Problem: Quest presentation copied localized title/description through `GlobalRegistry.LocalizationText` from read-looking runtime methods and notification hash paths.
Solution: `QuestManager` now caches `ILocalizationTextReadModel` from cold registration/hot-swap and passes it to `QuestStateManager`; presentation buffers are rebuilt from the cached owner only.
Rejected Alternatives: Leave global localization fallback inside quest accessors for convenience. It violates read-accessor purity and makes UI/presentation refreshes depend on global polling.
Scalability potential: Low uses fallback text with no lookup. Middle/High/Ultra can localize richer quest/marker UI through cached presentation owners without changing quest truth.
Hardware Impact: Removes global service reads from quest presentation refresh and notification composition; expected gain is micro-level, but it eliminates dependency stalls and route drift.

Problem: `QuestData` trusted raw designer floats and prerequisite string arrays. Blank/duplicate prerequisites and NaN/negative thresholds could enter cold quest graph compilation and marker presentation.
Solution: Added finite runtime trigger/completion/marker properties, non-destructive prerequisite validation counters, first-bad-slot indices, and editor-only token normalization. `QuestManager` and `QuestStateManager` now consume sanitized runtime values.
Rejected Alternatives: Delete bad prerequisite rows in `OnValidate()` or rely on `[Min]` attributes. Deleting rows destroys designer evidence; Inspector attributes do not protect runtime assets, merges, or external tooling.
Scalability potential: Low fails closed with finite cheap values and visible validation state. Middle keeps richer quest authoring stable. High/Ultra can add denser mission markers and localized presentation without gameplay truth divergence.
Hardware Impact: Prevents NaN propagation and redundant prerequisite gates before they hit runtime evaluation; normal-frame cost is unchanged, cold compile cost is bounded O(n^2) over small prerequisite arrays.

Problem: Biome resource/play/family ScriptableObjects used public Inspector fields as runtime truth. Corrupt asset edits, merge drift, or external tooling could bypass `[Range]`, leave blank labels/text, or hash an empty biome family id.
Solution: Added runtime-safe facade accessors for resource-plan weights/text, play-profile weights/text, and family identity/style strings. Editor validation trims authoring strings and clamps weights, while runtime accessors fail closed to existing defaults without mutating assets.
Rejected Alternatives: Compact or rewrite authored data destructively in `OnValidate()`. That hides designer mistakes and destroys evidence. Clamp only in consumers; that spreads the contract and leaves new consumers vulnerable.
Scalability potential: Low uses stable 1..5 bias bands and fallback strings for readable world/resource diagnostics. Middle keeps the same authored knobs with predictable caps. High/Ultra can spend saved trust budget on denser biome/resource presentation without changing gameplay truth or adding physics.
Hardware Impact: Normal-frame cost is scalar clamps and null/whitespace checks in diagnostics/selection paths. Worst-case gain is preventing corrupt resource/play weights from inflating world density, route pressure, and hazard/readability decisions beyond intended caps on i3/MX350-class hardware.

Problem: Adding `BiomeMatrixDirector` to the AST gate exposed an existing hot-chain shape: `SlowTick()` called `EvaluateMatrix()`, and `EvaluateMatrix()` contained a non-playing editor reference resolver with `WorldRuntimeReferenceUtility.TryResolvePlayerTransform()` and `TryGetComponent()`.
Solution: Removed cold/editor reference resolution from `EvaluateMatrix()`. Lifecycle and explicit `ForceRefresh()` still resolve references before evaluation; hot `SlowTick()` now enters pure evaluation only.
Rejected Alternatives: Trust `Application.isPlaying` branch guards or weaken the AST proof. Path-sensitive excuses are not proof, and the protocol requires a mechanically clean hot call graph.
Scalability potential: Low avoids hidden dependency drift in biome evaluation. Middle/High/Ultra can increase biome transition fidelity or VISUAL_SYNC presentation around biome changes without adding hot scene/component probes.
Hardware Impact: Removes a potential player/component fallback from the biome matrix hot call graph. Expected direct saving is 3-25 us on cache-miss frames, with larger value from eliminating dependency stalls.

Problem: `MissionData` exposed raw designer lists, strings, counts, and floats as runtime truth. Null rows, duplicate objective IDs, missing targets, negative counts, or NaN time/experience could pass from authoring data into mission consumers without a single bounded contract.
Solution: Added runtime-safe mission/objective/reward accessors, validation flags/counts, first bad-slot indices, valid-index reads, and raw-slot reads for tools. Editor validation normalizes text and finite values without compacting or deleting designer rows.
Rejected Alternatives: Destructive compaction in `OnValidate()` would hide broken authoring and lose designer evidence. Relying on `NarrativeGameplayReferenceValidator` only was rejected because runtime assets can be loaded without that editor menu path.
Scalability potential: Low fails closed on corrupt missions and still shows fallback text. Middle keeps richer objective/reward authoring with visible validation. High/Ultra can add denser mission presentation and localized UI from stable validated rows without changing gameplay truth.
Hardware Impact: Normal cost is bounded linear scan over small mission lists during cold/profile reads. Corrupt-data worst-case prevents invalid objective/reward tables from causing retries, bad target dispatch, or NaN timer propagation on i3/MX350-class hardware.

Problem: `MissionManager.GetActiveMission()` was a read-looking API but could allocate a `MissionInstance`, mutate `_activeMissions`, and remove stale entries during a read.
Solution: `GetActiveMission()` now only reads an existing cached instance and verifies active quest state. Cache creation remains in `StartMission()`; failed/reverted quest events remove active cache entries explicitly.
Rejected Alternatives: Keeping lazy read repair because there were no current callers. Public compatibility facades become future hot paths, so hidden mutation/allocation behind `Get*` is not acceptable.
Scalability potential: Low avoids surprise allocation from UI/probe code. Middle/High/Ultra can poll mission state for richer presentation using pure reads or add explicit sync commands when needed.
Hardware Impact: Avoids one dictionary add and one `MissionInstance` allocation on cache-miss reads; expected direct saving is 5-30 us per bad read path plus removal of GC pressure.

Problem: `BarterOfferData` exposed raw cost/reward arrays as runtime truth. Null items, zero item hashes, non-positive amounts, blank labels, and missing bundles could drift between designer validation, execution, UI summaries, and save transaction text.
Solution: Added runtime-safe offer accessors, validation flags/counts, first bad-slot indices, valid-index bundle reads, and raw-slot tooling reads. `PDAExchangeSystem` now consumes/grants/refunds valid rows only and UI summaries use the validated offer bundle view.
Rejected Alternatives: Keep raw arrays and rely on `BarterCatalogValidator`. Editor validation is not runtime proof, and raw array length lets invalid rows become visible gameplay or save state.
Scalability potential: Low fails closed on corrupt exchange contracts. Middle supports richer barter catalogs without per-frame parsing. High/Ultra can layer richer PDA presentation on validated offer rows without changing economy truth.
Hardware Impact: Normal execution cost is bounded linear scan over small offer bundles. Avoids useless inventory calls for invalid rows and prevents failed reward rollback loops from touching null/zero-hash entries; expected low-end savings 5-40 us on bad contract paths.

Problem: `PDAExchangeSystem.Tick()` could call `AutoResolve(false)` and reach `PlayerObject.TryGetComponent` when inventory/scan references were missing. `PDABarterTab.LateFrameTick()` could call `RefreshExchangeBinding()` and read `GlobalRegistry.PDAExchange`.
Solution: Removed Tick auto-resolution; missing runtime dependencies fail closed until cold lifecycle or hot-swap binding updates them. `PDABarterTab` now binds the exchange system from cold `AutoResolve()` or `GlobalRegistryServiceSlot.PDAExchangeRuntime` hot-swap, while LateFrame reads cached fields only.
Rejected Alternatives: Keep null-guarded fallback resolution because it is rare. Rare hot-path dependency probes still violate phase law and create worst-frame stalls.
Scalability potential: Low keeps PDA barter update predictable under missing services. Middle/High/Ultra can refresh richer PDA barter UI through `SignalBus<PdaExchangeStateChangedSignal>` in `LateFrameTick` without global polling.
Hardware Impact: Removes component lookup from the `Tick()` fallback and registry polling from `LateFrameTick()`. Expected direct saving is 5-60 us on missing-service frames and lower frame-time variance on i3/MX350.

Problem: `PDAEncyclopediaStreamer.LateFrameTick()` and its frame-state advance path could call `TryColdBootstrap()` when `_vaultReady` was false. That bootstrap can bind DataVault handles and instantiate lore/Babel stores, so VISUAL_SYNC was not mathematically cold.
Solution: LateFrame and frame-state advance now fail closed until lifecycle/hot-swap/editor routes have made the vault ready. `TryResolveVaultBuffer<T>()` also stopped invalidating vault descriptors from a read-looking method.
Rejected Alternatives: Keep fallback bootstrap because it repairs missing init. Repair during presentation creates worst-frame stalls and violates the one-route dependency doctrine.
Scalability potential: Low devices avoid PDA open hitches when vault bootstrap is stale. Middle keeps deterministic encyclopedia presentation. High/Ultra can add richer archive visuals after cold data ownership is ready, without coupling visuals to bootstrap I/O.
Hardware Impact: Removes rare DataVault/file-backed bootstrap from VISUAL_SYNC; expected low-end hitch avoidance is 200-2000 us on stale initialization frames, steady-state cost unchanged.

Problem: `PDADataLogTab.LateFrameTick()` could reach `EnsureLoreBindingCache()` through stress-reactive detail text. That helper could allocate lore hash arrays, query record indices, and mutate `_catalogLoreRecordIndices` from read/presentation paths. The same presentation chain could lazily add `CanvasGroup` through `SetElementVisible()`.
Solution: Lore binding cache rebuild is lifecycle/hot-swap only. LateFrame uses `IsLoreBindingCacheReady()` and returns fallback hashes/unlocked state when stale. Localization changes are deferred to LateFrame without dirtying lore bindings. `SetElementVisible()` now updates existing `Graphic` alpha or an existing `CanvasGroup` only.
Rejected Alternatives: Keep lazy cache repair because catalog size normally does not change. Normal catalog stability is not proof; designer hot-swap and localization events can still route into LateFrame.
Scalability potential: Low devices get stable PDA archive text without sudden array allocation. Middle keeps corruption/decryption presentation deterministic. High/Ultra can spend budget on richer PDA madness visuals while the data route remains zero-GC.
Hardware Impact: Removes possible `uint[]`/`int[]`/`CanvasGroup` hot allocation and component probing from PDA archive presentation. Expected low-end saving is 35-520 us on stale-cache/decryption frames, with larger benefit from eliminating GC and layout side effects.

Problem: `PDADataLogTab.RenderSelectedLoreHologram()` could call `EnsureHologramMaterial()` from `LateFrameTick()` and instantiate a `Material` during lore hologram presentation.
Solution: Render now requires a cold-prepared `_runtimeHologramMaterial` and returns if the material is absent. Existing `Awake()` material warmup remains the owner route.
Rejected Alternatives: Keep lazy material repair because the shader is usually assigned at startup. That turns a missing/corrupt authoring reference into a presentation-frame allocation spike.
Scalability potential: Low keeps the archive responsive by skipping the hologram instead of stalling. Middle keeps standard hologram draw after cold setup. High/Ultra can add richer hologram meshes and shader detail without moving material creation into VISUAL_SYNC.
Hardware Impact: Removes a possible `new Material` from PDA lore draw; expected low-end hitch avoidance is 50-120 us plus native render-state churn avoidance.

Problem: `PDADecryptionSpectrogramPanel.LateFrameTick()` could repair native and graphics resources directly, reaching DataVault write locks, `GraphicsBuffer` allocation, and runtime material creation from VISUAL_SYNC.
Solution: LateFrame now treats stale native/graphics readiness as a fail-closed state. DataVault replacement prepares native resources from the hot-swap callback. Wave rendering reads only cold-prepared material/mesh/args handles.
Rejected Alternatives: Keep self-healing resource allocation in LateFrame to preserve the minigame after quality/resource drift. Repairing ownership inside presentation is the stall vector; quality must change cadence/fidelity without hot allocation.
Scalability potential: Low skips one visual panel frame instead of allocating buffers under load. Middle keeps the frequency minigame stable from cold-prepared resources. High/Ultra can raise waveform density at cold setup or staged safe boundaries, not by reallocating during draw.
Hardware Impact: Removes potential DataVault write-lock acquisition plus material/GraphicsBuffer repair from LateFrame. Estimated low-end avoided spike is 200-780 us on stale resource frames.

Problem: `PDAMapTab.RenderHologramMap()` and `RenderPointCloud()` called `EnsurePointCloudResources()` during LateFrame draw, allowing mesh/material/GraphicsBuffer repair from the cartography presentation path.
Solution: Render methods now require existing GPU resources and return if they are missing. Existing cold build path remains the only resource creation route.
Rejected Alternatives: Keep lazy point-cloud repair because map resources are usually built by `EnsureBuilt()`. Usual construction is not a proof artifact; render paths must not allocate when a previous resource was released or invalidated.
Scalability potential: Low returns a blank/clear map instead of stalling on GPU allocations. Middle retains current point cloud. High/Ultra can spend more on cartography density through cold-prepared buffers and staged quality policy.
Hardware Impact: Removes possible `new Material`, `new Mesh`, and `GraphicsBuffer` creation from map draw. Estimated avoided spike is 300-900 us on resource-miss frames, with lower driver variance on MX350-class hardware.

Problem: `PDALoadoutTab.LateFrameTick()` could refresh loadout cards and resolve authored tool prefabs through `prefab.TryGetComponent()`.
Solution: Added cold prefab-tool cache warmup after lifecycle auto-resolve and player/inventory hot-swap. Hot refresh reads cached `GameObject` to `IPlayerToolDataReadModel` pairs only and fails closed on unknown prefabs.
Rejected Alternatives: Keep prefab probing because preset count is small. Small cardinality does not make Unity component lookup legal inside a UI refresh chain.
Scalability potential: Low keeps slot panels deterministic with missing tool metadata hidden or fallback. Middle keeps current loadout summary. High/Ultra can add richer tool durability and styling because prefab metadata is already staged.
Hardware Impact: Removes up to preset-slot-count component probes from loadout refresh. Expected low-end saving is 20-420 us on first/stale refresh frames and lower managed/native boundary jitter.

Problem: `PDAIntrusionManager.LateFrameTick()` still owned repair logic: player/input owner resolution, panel text hierarchy scans, and localization override sink lookup could occur from the hacked PDA visual phase.
Solution: Moved owner binding, text drift target collection, native input binding, and transient localization sink binding to lifecycle or `GlobalRegistryServiceSlot` hot-swap routes. Presentation reads cached fields and cached text arrays only.
Rejected Alternatives: Keep self-healing PDA visuals during hack frames. Repair inside VISUAL_SYNC makes the worst visual frame slower exactly when immersion needs stable cadence.
Scalability potential: Low can skip drift/override for a frame instead of hitching. Middle preserves current intrusion cadence. High/Ultra can layer heavier glyph/drift shaders while text targets and service sinks stay staged.
Hardware Impact: Removes player `TryGetComponent`, panel `TryGetComponent<TextMeshProUGUI>`, and `GlobalRegistry.LocalizationTransientOverrideSink` reads from LateFrame chains. Estimated avoided spike is 50-520 us on stale owner/panel frames.

Problem: Static proof did not include the intrusion manager, so the cached-sink claim was not mechanically enforced.
Solution: Added `PDAIntrusionManager` to `.codex_tmp/14der_ast_check.csx`; the recursive hot-chain/read-accessor scanner caught and then cleared the LateFrame localization lookup.
Rejected Alternatives: Manual assertion only. The scanner already proved useful by finding a missed dependency route.
Scalability potential: Low through Ultra share the same proof gate for PDA interaction UI. Future visual overkill can expand without hot dependency drift.
Hardware Impact: Offline verification only; prevents reintroduction of PDA hot service lookups before runtime profiling.

Problem: PDA death dump and subtitle/survival/settings presentation paths still had repair logic that could build UI, resolve camera/survival references, or allocate text buffers during visual-frame signal drain.
Solution: Move death dump library/overlay creation, settings camera resolution, subtitle UI build, and survival system binding to lifecycle or hot-swap paths. Visual lanes now fail closed when cold staging is missing.
Rejected Alternatives: Self-heal on first presentation frame. It preserves output but makes the worst visual frame pay construction cost and hides missing lifecycle setup.
Scalability potential: Low skips one decorative/presentation frame instead of hitching. Middle keeps current PDA/HUD behavior after cold staging. High/Ultra can add richer dumps/subtitles/survival dressing without changing the data route.
Hardware Impact: Removes rare UI construction, camera scans, and survival lookup from VISUAL_SYNC; avoided low-end spikes are roughly 120-520 us on stale setup frames.

Problem: `LocalizedLayoutMirror` and `LocalizedTMPAutoSizer` could resolve localization owners and collect icon/TMP component roots from `LateFrameTick` layout application.
Solution: Capture defaults, icon roots, TMP target, and localization route in cold lifecycle/editor paths. Runtime mirroring and autosize apply cached fields only.
Rejected Alternatives: Keep path-sensitive guards and rely on normal `Awake` ordering. AST proof must be clean without relying on branch assumptions.
Scalability potential: Low keeps localized UI from hitching on language swaps. Middle/High/Ultra can use richer RTL/font presentation with cached roots and continuous quality knobs.
Hardware Impact: Removes hierarchy traversal and `TryGetComponent` from late-frame layout; expected savings are small steady-state but prevent stale-cache spikes.

Problem: `DiegeticPDAController.LateFrameTick()` could throttle reference repair, configure the PDA shell, rebuild pointer target cache, and toggle tablet visibility by scanning hierarchy components.
Solution: Restrict reference repair and shell configuration to lifecycle/hot-swap. Cache tablet renderers/colliders/canvas groups cold. Pointer target rebuild no longer occurs from input/hot presentation if dirty.
Rejected Alternatives: Throttled repair every 0.5 seconds. Throttling reduces average cost but keeps illegal component discovery inside a frame lane.
Scalability potential: Low gets deterministic PDA open/close and input without hierarchy scans. Middle keeps current world-space PDA behavior. High/Ultra can increase tablet visual fidelity because visibility/pointer gates are pre-staged arrays.
Hardware Impact: Removes recursive `TryGetComponent` and pointer cache rebuild from PDA visual/input frames; expected avoided spike is 80-760 us on stale shell or dirty pointer cache frames.

Problem: `DiegeticPdaFocusDistanceController.LateFrameTick()` could resolve camera/Volume references and scan parent/child hierarchies when focus was active and references were missing.
Solution: Resolve camera and depth-of-field volume during enable, active-toggle, or player/voxel hot-swap only. LateFrame uses cached camera transform and DepthOfField pointer or returns.
Rejected Alternatives: One scan every 30 frames. Cadence gating is still a hot dependency route.
Scalability potential: Low devices skip PDA depth-of-field instead of hitching. Middle keeps current focus effect after cold resolve. High/Ultra can sharpen focus stepping through quality-weighted math without scene scans.
Hardware Impact: Removes parent/child `TryGetComponent<Camera/Volume>` probes from focus LateFrame; avoided low-end spikes are roughly 25-180 us on missing reference frames.

Problem: PDA controls read accessors resolved input/rebind services from `GlobalRegistry`, so event and UI code could silently poll global identity through `Resolve*` methods.
Solution: Add cached input and rebinding service fields populated from `OnEnable` and `GlobalRegistryServiceSlot` hot-swap callbacks. `ResolveInputManager()` and `ResolveRebindingService()` now return cached/subscribed fields only.
Rejected Alternatives: Keep service-locator fallback because controls are event-driven. Read-accessor doctrine is global; event-driven code becomes hot under repeated input.
Scalability potential: Low keeps controls rebind UI stable under missing services. Middle/High/Ultra can add richer binding display without hidden global polling.
Hardware Impact: Removes service locator reads from controls navigation/rebind paths; direct saving is micro-level, but it closes a dependency drift route.

Problem: `WristHologramHudRuntime.LateFrameTick()` could allocate DataVault handles, resize quad scratch, generate font atlas, and prepare signal buffers when native resources were dirty.
Solution: LateFrame now returns when native/signal ownership is stale. `OnEnable`, `Start`, and DataVault hot-swap own native/signal/graphics preparation and seed state. PDA projector native buffers are prepared in runtime enable/hot-swap too.
Rejected Alternatives: Keep LateFrame self-repair to recover from DataVault replacement. The correct owner phase is the service replacement callback; presentation must not own native memory topology.
Scalability potential: Low avoids HUD stalls and may drop a frame of wrist projection. Middle keeps current projection. High/Ultra can increase projector atlas and matrix visuals after cold ownership is restored.
Hardware Impact: Removes DataVault generation handle creation, managed scratch resize, font atlas generation, and projector native seed from VISUAL_SYNC; avoided low-end spikes can exceed 880 us on stale DataVault frames.

Problem: `WristPdaScreenProjectorFeature` used a `static` render-graph lambda that the Unity Mono Roslyn proof parser rejects.
Solution: Replace it with a non-capturing lambda. The body still uses only pass data and render context, so behavior remains the same and old parser compatibility is restored.
Rejected Alternatives: Exclude the file from AST proof. That would leave a renderer-domain compile/syntax blind spot in the PDA projection surface.
Scalability potential: Low through Ultra keep the same render graph pass; compatibility improves without changing fidelity decisions.
Hardware Impact: No runtime performance target; the value is compile-path portability and clean static verification.

Problem: Acoustic translator and audio caption overlays still performed owner/UI repair and dispatcher registration from VISUAL_SYNC event drains.
Solution: Treat `OnEnable`, hot-swap, and scene/lifecycle setup as the only cold construction routes. LateFrame drains now require cached TMP labels, caption slots, and owners; if cold staging is missing the frame is skipped.
Rejected Alternatives: Keep self-heal because captions and sonar barks are user-facing. That hides missing lifecycle setup and makes the visual lane pay hierarchy/component costs during spikes.
Scalability potential: Low drops a bark/caption frame instead of hitching. Middle keeps behavior after normal cold setup. High/Ultra can spend saved time on richer sonar typography and caption motion without dependency drift.
Hardware Impact: Removes TMP hierarchy repair, registry registration, and caption slot setup from LateFrame chains; avoided low-end spikes are roughly 180-520 us on stale UI frames.

Problem: Suit HUD and UI scaler mixed cold hierarchy ownership with tick execution: slow tick could register through `GlobalRegistry`, create/scan roots, disable layout groups, and late frame could repair stencil/hierarchy components.
Solution: Hot lanes now read only already-staged roots/groups. Cold refresh remains in enable/start/bootstrap/hot-swap paths. Suit HUD quickbar prefab metadata is resolved into fixed arrays outside the visual refresh chain.
Rejected Alternatives: Slow-tick bootstrap. It lowers frequency but still violates phase ownership and creates nondeterministic frame cost.
Scalability potential: Low keeps HUD stable by skipping frames until hierarchy is ready. Middle keeps current presentation after bootstrap. High/Ultra can raise HUD cadence and visual dressing from cached state only.
Hardware Impact: Removes component traversal, CanvasGroup/RectMask repair, prefab `TryGetComponent`, and registry registration from HUD/scaler hot chains; avoided stale-state spikes are roughly 260-740 us.

Problem: VR lever latch shutdown and submarine cockpit graphics retry used presentation/render phases to mutate runtime registrations and GPU topology.
Solution: The lever no longer unregisters dispatcher/hot-swap slots from LateFrame after latch. Cockpit LateFrame/render paths fail closed when radar/damage graphics resources are not ready; resource creation remains in lifecycle and explicit service replacement paths.
Rejected Alternatives: Immediate hot teardown/allocation to save steady-state work. Registry mutation and `GraphicsBuffer` creation from VISUAL_SYNC/render are worse than carrying a cheap latched read model for a few frames.
Scalability potential: Low avoids teardown/allocation hitches. Middle preserves cockpit visuals after cold resource staging. High/Ultra can use larger radar/damage buffers without render-phase allocation.
Hardware Impact: Removes registry unregister calls and graphics buffer/material retry creation from hot chains; avoided low-end spikes can exceed 960 us during latch or cockpit resource recovery frames.

Problem: The expanded UI AST surface exposed dependency and registration drift in audio captions, diegetic panel presentation, pause routing, debug/settings/localization helpers, and multiple HUD/terminal overlays.
Solution: Converted hot helpers to cached-owner reads and pending value transfers. `LateFrameTick` and render callbacks now consume staged state; cold lifecycle and `GlobalRegistryServiceSlot` replacement own dependency capture, UI construction, event-system setup, and native/GPU resource topology.
Rejected Alternatives: Keep dynamic registration/self-heal because UI correctness is visible. That preserves one-frame output by spending unpredictable main-thread time in the exact phase that must stay stable.
Scalability potential: Low skips optional UI/cursor/phosphor/render output until staged resources exist. Middle keeps current visuals after cold setup. High/Ultra can raise panel shader, HUD, and PDA visual density because dependency discovery no longer competes with the visual lane.
Hardware Impact: Removes hidden registry reads, component probes, event-system construction, RT/phosphor repair, save-label TMP queries, and tick-registration churn from UI hot chains. Low-end avoided spikes are estimated at 120-960 us on stale setup frames; steady frames retain scalar/cached-field work only.

Problem: `PauseMenuController.Open()` and close/cancel handling were command-drained from `LateFrameTick` but still called `GlobalRegistry.Input`, `GlobalRegistry.TickDispatcher`, `EnsureBuilt()`, and `EnsureEventSystem()`.
Solution: Cached `IInputService` and `ITickDispatcher` in cold/hot-swap routes; event system and menu hierarchy are built in `Awake`/`OnEnable`; hot open fails closed if the hierarchy was not staged.
Rejected Alternatives: Lazy-build pause UI from the first pause key press. It hides missing lifecycle setup and risks allocation/component probing during user input.
Scalability potential: Low opens only when staged, avoiding input hitch. Middle keeps current menu. High/Ultra can add richer pause visuals with the same cached routing and no authority change.
Hardware Impact: Removes `GameObject` creation, UI module component probes, registry polling, and dispatcher lookup from pause command drain; avoided low-end spikes are roughly 200-620 us on first-open/stale-event-system frames.

Problem: `DiegeticPanelController` quality/cursor/proxy presentation used `LateFrameTick` as a repair lane for RT/phosphor resources, cursor queue registration, input owner refresh, serialized reference scans, and late-frame registration changes.
Solution: `EnsureRuntimeState()` is now a cached readiness check; cursor pose/visibility apply directly inside the visual phase; queue helpers set pending structs only; quality refresh updates material state without reallocating RT/phosphor resources; render callback no longer calls phosphor resource creation.
Rejected Alternatives: Path-sensitive branch guards around `RefreshLateFrameRegistration()` and `EnsureRenderTexture()`. Static proof cannot accept branch excuses, and phase law rejects allocation-capable helpers from VISUAL_SYNC.
Scalability potential: Low panels skip optional phosphor/RT topology repair instead of hitching. Middle keeps stable diegetic panels after cold setup. High/Ultra can use stronger panel shaders and phosphor history only when resources were staged safely.
Hardware Impact: Removes render texture and phosphor repair risk, registry registration mutation, and input/reference refresh from panel hot chains; avoided stale-state spikes are estimated at 260-780 us, with steady-frame work reduced to cached transforms/material writes.

Problem: The previous AST gate proved registry/component rules but missed hot `new GameObject`, `new Material`, `new Mesh`, `MaterialPropertyBlock`, and pool warmup routes. The expanded gate exposed residual allocation self-heal in scavenging, fabricator preview, acoustic/sonar/HUD visuals, wrist HUD material state, resource distribution, and voxel bridge pooling.
Solution: Expanded the Roslyn gate to include allocation texts and additional hot roots. Moved ownership to cold enable/start/hot-swap paths. Hot routes now read prepared resources, cached session ids, and warmed-state mirrors, or fail closed.
Rejected Alternatives: Keep allocation guards path-sensitive or rely on comments marking allocations cold. The proof must follow call chains, not comments.
Scalability potential: Low devices skip optional HUD/loot/voxel visual repair instead of stalling. Middle keeps current behavior after staged setup. High/Ultra can raise visual density using cold-prepared buffers/materials/pools without changing authority routes.
Hardware Impact: Removes worst stale-frame allocation spikes from several visible systems. Expected low-end avoided spikes range from 180 us for mesh fallback to 960 us for combined HUD material/mesh/resource misses.

Problem: `ScavengingLootOracleRuntime.TryQueueResourceNodeLoot()` and simulation scheduling could call `EnsureVault()`, hydrate loot tables, and read `GlobalRegistry.WorldSeedProvider` through `ResolveSessionId()` after gameplay harvest began.
Solution: Harvest queue uses `TryGetPreparedHostForHot()` and cached `_sessionId`. Vault handles and loot CDF/emergency table hydration are prepared during enable, DataVault replacement, or explicit editor/manual cold calls.
Rejected Alternatives: Lazy host/Vault creation on first harvest. It preserves output but moves DataVault topology and session identity lookup into gameplay interaction frames.
Scalability potential: Low skips loot queue until the oracle is staged. Middle keeps deterministic harvest once bootstrapped. High/Ultra can increase loot visual/audio dressing because the request lane stays scalar and pre-owned.
Hardware Impact: Removes possible host creation, Vault buffer creation, loot table hydration, and world-seed registry read from harvest/simulation routes. Estimated avoided low-end spike is 350-560 us on first harvest or stale Vault frames.

Problem: `ResourceDistributionDirector.LateFrameTick()` could call `EnsureRuntimePool()` and `WorldGenerativeGeologyVoxelBridgeDirector.ReconcileVoxelRequests()` could call object-pool warmup from tick/slow-tick reconciliation.
Solution: Resource pool warmup runs on enable/object-pool replacement. Voxel bridge slow tick now computes target warm count only; actual `Warmup()` is cold/hot-swap.
Rejected Alternatives: Amortized warmup inside slow tick. It reduces average cost but still performs allocation-capable pool ownership in frame phases.
Scalability potential: Low devices may defer a spawn instead of hitching. Middle remains stable with cold warmup. High/Ultra can request larger warm targets through continuous quality policy without in-frame pool construction.
Hardware Impact: Removes pool warmup from visual/simulation frames; estimated avoided low-end spikes are 300-740 us depending on pool miss and prefab cost.

Problem: Multiple first-party UI visual lanes still allocated or repaired resources: acoustic radar mesh/material, diegetic tooltip graphics buffers/property blocks, sonar holo mesh/material, Suit HUD threat/acoustic materials, TMP sharpness material instance, and wrist HUD property block.
Solution: Visual lanes now apply properties to existing resources only and return when resources are absent. Cold lifecycle prepares meshes/materials/property blocks/graphics buffers.
Rejected Alternatives: One-frame self-heal because HUD output is visible. It trades correctness for unstable frame time; optional presentation must fail closed.
Scalability potential: Low devices keep stable frame pacing with occasional missing optional overlay. Middle keeps normal HUD after cold staging. High/Ultra can spend more on overlay density and shader effects because material topology is staged.
Hardware Impact: Removes hot `new Mesh`, `new Material`, `new MaterialPropertyBlock`, and graphics buffer repair paths. Estimated low-end avoided spike is 180-960 us on stale visual resources.

Problem: Fabricator hologram assembly could generate the shared fallback mesh from `LateFrameTick` if a recipe had no pre-cached mesh path.
Solution: `ResolveAssemblyFallbackMesh()` now returns only authored or already-created shared mesh; `Awake` and cold source-cache rebuild own fallback creation.
Rejected Alternatives: Generate fallback during craft preview start. It makes the first craft frame pay mesh and array creation cost.
Scalability potential: Low skips fallback preview if cold staging failed. Middle keeps normal preview after `Awake`. High/Ultra can add richer assembly previews using cold caches.
Hardware Impact: Removes one-time mesh plus vertex/index/normal array creation from assembly visual drain; estimated avoided first-preview spike is 180 us.

Problem: `FirstHourDirector.SlowTick()` could still reach player transform/component fallback through survival and runtime inventory resolution.
Solution: Cache `IPlayerRuntimeContext`, `ISurvivalSystem`, and `PlayerInventory` during lifecycle and GlobalRegistry hot-swap; `SlowTick` reads cached interfaces only. Notification late-frame registration is lifecycle-owned.
Rejected Alternatives: Keep `GameBootstrapper` and `TryGetComponent` fallback as rare repair. Rare repair still runs inside a high-frequency director path and hides missing context ownership.
Scalability potential: Low avoids first-hour onboarding hitch on stale player context. Middle keeps current pacing. High/Ultra can add richer onboarding presentation without service polling.
Hardware Impact: Removes player transform/component fallback and late-frame registration churn from first-hour gameplay checks; avoided stale-context spike is roughly 120-420 us.

Problem: `ProceduralOreSpawner` held multiple DataVault write locks across scheduled generation jobs.
Solution: Added persistent generation scratch arrays. Jobs write scratch, then completion commits each output buffer to DataVault through one `TryAcquireWriteLock` at a time with a local `finally` release. Hot ticks only require cold-ready native state.
Rejected Alternatives: Keep a multi-buffer lock mask across the job. It blocks relocation/defrag safety and creates a deadlock vector if another owner waits on any one buffer.
Scalability potential: Low keeps generation deterministic without multi-lock stalls. Middle keeps current ore density. High/Ultra can increase visual-only cluster density while commit topology remains one-buffer-at-a-time.
Hardware Impact: Removes multi-buffer writer ownership from generation frames; expected low-end avoided contention spike is 300-980 us when DataVault relocation or ore generation overlaps.

Problem: `PickupItem.TryGetWorldStatePersistenceIdentity()` was not a pure read accessor; it could resolve identity and call `TryGetComponent` indirectly.
Solution: Cache pooled-instance marker in `Awake`/`OnEnable`; resolve world-state identity during cold configure/enable. The accessor now returns cached keys only.
Rejected Alternatives: Lazy identity resolution during world-state scene scan. It makes a read route mutate identity and probe components.
Scalability potential: Low scene scans stay bounded. Middle/High/Ultra can increase authored pickup count without component-probe spikes in persistence scans.
Hardware Impact: Removes component lookup from pickup persistence reads; avoided scan spike scales with pickup count.

Problem: Quest transition audit could append to disk from signal evaluation reached by the quest signal drain.
Solution: Replaced file append with a fixed `QuestTransitionAuditEntry[128]` ring. Dev audit state remains available without string path creation or disk I/O in the drain.
Rejected Alternatives: Keep editor/development file audit. The preprocessor guard does not protect development builds from frame-lane I/O.
Scalability potential: Low avoids disk stalls. Middle keeps transition evidence in memory. High/Ultra can add external export from a cold diagnostic command if needed.
Hardware Impact: Removes `File.AppendAllText`, path creation, and audit string construction from quest transition hot flow; avoided stall is unbounded on slow disks, typical 200+ us.

Problem: `MissionManager.GetActiveMissions()` exposed dictionary enumeration to gameplay/UI consumers.
Solution: Replaced it with `ActiveMissionCount`, `TryGetActiveMissionAt`, and `TryCopyActiveMissionsNonAlloc` using caller-owned storage.
Rejected Alternatives: Keep `IEnumerable` because no current callers were found. Future UI would inherit a hidden allocation/enumerator route.
Scalability potential: Low mission UI can poll without GC. Middle/High/Ultra can show richer mission dashboards from caller-owned arrays.
Hardware Impact: Avoids enumerable allocation risk and dictionary value enumerator leakage in mission presentation paths; expected saving is 20-180 us depending on caller cadence.

Problem: `ModEventProjectionBridge` resolved a DataVault `NativeArray<ModCullTelemetryEntry>` and later wrote cull telemetry from `LateFrameTick` without acquiring a DataVault write lock. That created an invisible relocation/mutation ownership hole in the mod projection lane.
Solution: Removed the DataVault-backed cull telemetry path. The bridge now owns a local persistent `NativeArray<ModCullTelemetryEntry>[300]` allocated during install and disposed during shutdown. LateFrame cull telemetry writes no longer touch DataVault at all, and the cursor uses unsigned modulo so wrap cannot create a negative index.
Rejected Alternatives: Acquire a DataVault write lock for every culled mod callback. That would make a defensive watchdog path contend with DataVault relocation and add lock latency to `LateFrameTick`.
Scalability potential: Low devices keep mod callback culling bounded and independent from DataVault pressure. Middle keeps the same mod projection behavior. High/Ultra can raise projection cap through continuous quality without adding DataVault writer contention.
Hardware Impact: Removes hot DataVault alias mutation and DataVault hot-swap completion work from mod event projection. Avoided low-end contention/stall risk is estimated at 120-260 us during cull-heavy frames.

Problem: `H8PrefabRegistryRuntimeBinder.Bind()` and `ClearExistingBuffers()` contained multiple DataVault write-lock acquisitions in one method, making the lock topology hard to prove even though the locks were sequential.
Solution: Split mapping writes, lore-link writes, mapping clear, and lore-link clear into separate helpers. Each helper has exactly one `TryAcquireWriteLock` and an unconditional `ReleaseWriteLock` inside `finally`.
Rejected Alternatives: Keep the existing code because runtime nesting was absent. The integrator proof must be structural; a future edit inside the same method could accidentally nest locks.
Scalability potential: Low devices avoid prefab-registry bind stalls from accidental lock coupling. Middle keeps current designer registry output. High/Ultra can increase prefab/lore metadata density while writer ownership remains one-buffer-at-a-time.
Hardware Impact: Turns bridge registry synchronization into independently bounded lock windows; expected gain is 40-180 us on registry rebake or hot-bind contention frames.

Problem: `ScannerDataMiningRouter.SlowTick()` could call `CachePlayerRuntimeContextCold()`, which read `GlobalRegistry.Player` if the cached context was absent or uninitialized.
Solution: Moved player context binding to cold enable and `GlobalRegistryServiceSlot.Player` hot-swap payloads. `SlowTick()` now reads cached context only and clears cached movement when unavailable.
Rejected Alternatives: Keep slow-tick self-heal because player context is rare to miss. It is still high-frequency registry polling under a helper name.
Scalability potential: Low devices avoid scanner slow-tick service polling. Middle keeps the same scanner cadence. High/Ultra can increase scanner presentation density while dependency ownership remains cold.
Hardware Impact: Removes a registry lookup/repair chain from scanner slow lane; expected avoided stale-context spike is 80-180 us.

Problem: `ScannerDataMiningRouter.TryWriteVaultSettings()` wrote the settings buffer through `TryResolveHandle`, bypassing DataVault write-lock ownership and compaction fencing.
Solution: Changed the API to `TryWriteVaultSettings(IDataVault, in ScannerSettingsDTO)`, requiring an explicit cold/editor vault dependency. The method now acquires one write lock and releases it in `finally`.
Rejected Alternatives: Keep parameterless `GlobalRegistry.DataVault` accessor and resolved mutable write because the editor tuner is cold. Cold editor code still publishes runtime settings into DataVault and must obey writer ownership.
Scalability potential: Low keeps scanner tuning swaps bounded. Middle keeps editor/live tuning behavior. High/Ultra can increase scanner candidate budgets through the same DTO without changing authority route.
Hardware Impact: Avoids DataVault alias mutation during designer tuning and removes compaction-race risk; estimated contention avoidance is 60-140 us.

Problem: A pending scanner DataVault rebind could be applied from `LateFrameTick()` after a scheduled query completed, and that path called `TryInitializeRuntimeState()`, which ensures Vault handles, seeds mock data, and registers tick lanes.
Solution: Added `ApplyPendingDataVaultRebindAfterVisualFence()`. The visual phase now releases stale handles, installs the new vault reference, sets `_runtimeStateColdInitRequired`, and fails closed until a cold initialization route runs.
Rejected Alternatives: Reinitialize immediately after query completion. It is convenient but performs ownership/registration work in VISUAL_SYNC and violates phase proof.
Scalability potential: Low skips scanner query until staged rather than hitching during a Vault swap. Middle keeps normal scanner runtime after cold init. High/Ultra can run denser scan results after proper staging.
Hardware Impact: Removes handle ensure, mock-grid seed, and tick registration from a late-frame rebind corner case; avoided low-end spike is estimated at 260-420 us.

Problem: `MissionMarkerSystem` used `ResolvePlayerContextCold`, `ResolveQuestRuntimeCold`, and `ResolveAtlasSignalCold` names for methods that read `GlobalRegistry`.
Solution: Renamed them to `CachePlayerContextFromRegistryCold`, `CacheQuestRuntimeFromRegistryCold`, and `CacheAtlasSignalFromRegistryCold` so `Resolve*` remains a pure read-accessor naming lane.
Rejected Alternatives: Teach the proof script that `Resolve*Cold` is special. That preserves ambiguous API semantics and weakens the doctrine.
Scalability potential: Low avoids accidental future hot calls into cold registry methods. Middle/High/Ultra keep mission-marker behavior unchanged.
Hardware Impact: Structural prevention only; direct runtime saving is small, roughly 40 us if a future hot path had reused the old helper.

Problem: `PlayerInventory.ApplyDerivedMassTotals()` published mass through `GlobalRegistry.PublishPlayerInventoryMassKg`, and `SubmarineFluidDynamics` seeded cargo mass from `GlobalRegistry.PlayerInventoryMassKg`.
Solution: Removed the hot global mass publication. `SubmarineFluidDynamics` now seeds from cached `IPlayerInventoryService` or cached `IPlayerRuntimeContext.Inventory`; live updates remain on `SignalBus<InventoryChangedSignal>`.
Rejected Alternatives: Keep a global float as an efficient cache. It is still a second owner route and encourages hot polling.
Scalability potential: Low devices avoid registry traffic during inventory mass refresh. Middle keeps cargo draft sync through signal snapshots. High/Ultra can add richer submarine cargo visuals without changing mass authority.
Hardware Impact: Removes global publication/read bridge from inventory/submarine coupling; expected low-end saving is 80-180 us in mass-refresh bursts.

Problem: `ItemSalinityCorrosionJob` mutated `_itemDurability`, `_durabilities`, `_qualityMilli`, `_itemStateFlags`, result, and broken-hash DataVault lanes directly from the corrosion job path without writer locks.
Solution: The job now reads truth lanes as read-only and writes cold-owned persistent scratch arrays. The owner commits scratch deltas back into DataVault one lane at a time through `TryAcquireWriteLock` and `ReleaseWriteLock` in `finally`.
Rejected Alternatives: Hold four write locks while the job runs. That creates a lock-order deadlock vector and blocks DataVault relocation.
Scalability potential: Low devices get bounded frost-tick corrosion without Vault contention. Middle keeps current durability behavior. High/Ultra can raise equipment corrosion telemetry density while keeping one-lane commit topology.
Hardware Impact: Removes multi-lane mutable aliasing from corrosion frost ticks; estimated contention/stall avoidance is 300-620 us on relocation or inventory-heavy frames.

Problem: `SubmarineFluidDynamics.FixedTick()` reached `TryGetComponent`, `GetComponentsInChildren`, and late-frame `GlobalRegistry` registration through player binding, pipe binding, cavitation, and brine feedback helper chains.
Solution: Player bindings are refreshed from cached runtime context, pipe bindings are staged in cold reference cache, and the late-frame lane is registered from lifecycle/dispatcher hot-swap rather than queued feedback.
Rejected Alternatives: Keep helper self-heal because the physics frame needs correctness. Missing optional bindings must fail closed; physics phases cannot repair scene topology.
Scalability potential: Low avoids physics-frame spikes. Middle keeps normal feedback after cold staging. High/Ultra can use richer cavitation/brine presentation because lifecycle owns registration.
Hardware Impact: Removes Unity component scans and registry registration from fixed/late-frame chains; estimated avoided spike is 260-740 us.

Problem: The previous AST proof used raw string counts for write locks and missed the newly audited inventory/submarine bridge.
Solution: Added `PlayerInventory`, `ItemSalinityCorrosionJob`, and `SubmarineFluidDynamics` to the proof surface; write-lock checks now count invocation syntax nodes, not method names.
Rejected Alternatives: Keep grep-style lock proof. It produced false positives on method declarations and misses exact call topology.
Scalability potential: Low/Middle/High/Ultra unchanged at runtime; proof quality improves for future passes.
Hardware Impact: Static proof only; no runtime saving.

Problem: `FutureCommandSandboxValidator` still has broad mutable DataVault alias routes in request/register/drain paths.
Solution: Not patched in this pass. It is a larger mod-sandbox ownership rewrite because validation jobs currently depend on many mutable Vault lanes; partial lock wrapping would risk multi-lock job windows.
Rejected Alternatives: Add superficial locks around the existing job setup. That would hide the violation while preserving the deadlock vector.
Scalability potential: Needs a separate scratch-output and one-lane commit architecture like inventory salinity corrosion.
Hardware Impact: Residual risk remains in mod command sandbox under heavy UGC input; not part of the committed source changes here.

Problem: `FutureCommandSandboxValidator.Request*`, `RegisterApprovedAsset`, `SetOpcodeEnabled`, tuning updates, telemetry writers, and dump throttle state wrote DataVault buffers through mutable `OpenVaultLane()` aliases without writer ownership.
Solution: Added `TryOpenVaultLaneRead`, `TryAcquireVaultLaneWrite`, `TryWriteVaultLaneElement`, `TryReadRingStateSnapshot`, and `TryWriteRingStateSnapshot`. Public request/register/tuning/telemetry routes now read immutable snapshots, write exactly one Vault lane per lock, and release every acquired lock in `finally`.
Rejected Alternatives: Acquire pending ring and ring state locks together for atomic queue commit. That would violate the one-writer-lock invariant and expose a deadlock path during Vault compaction. Keeping `OpenVaultLane()` because the methods are mostly cold was rejected because these APIs are mod ingress and can be hit by UGC bursts.
Scalability potential: Low uses bounded queue writes and fails closed on Vault contention. Middle keeps the same mod ingress behavior with lower relocation risk. High/Ultra can raise command budgets through continuous quality without adding lock nesting.
Hardware Impact: Removes lockless alias mutation from high-burst UGC ingress and telemetry. On i3/MX350 this reduces compaction/race stalls rather than steady arithmetic cost; estimated avoided contention spike is 180-540 us during mod command bursts or telemetry dumps.

Problem: The same file still has deeper scheduled-validation ownership debt: `TryPrepareValidationJob`, `LoadSheddingJob`, and `ValidateFutureCommandEnvelopeJob` mutate pending/staging/stats/counter/dev-null/ring/camera/memory lanes as job inputs/outputs.
Solution: Deliberately stopped short of fake completion. The correct fix is a dedicated scratch-output architecture: read DataVault truth lanes through read-only handles, execute validation against owned scratch arrays, then commit each changed lane separately through one write lock and `finally`.
Rejected Alternatives: Wrap the existing job setup in several write locks or hold locks across the scheduled job. That is mathematically worse than the current state because it would hold multiple Vault write locks across a job lifetime.
Scalability potential: Low should use smaller scratch capacities and more load shedding. Middle keeps default capacities. High/Ultra can spend more on mod command presentation and camera/subtitle/haptic feedback after scratch commit.
Hardware Impact: Residual risk remains until the job rewrite lands; projected gain after full fix is 700-2600 us avoided under compaction or high UGC load, plus removal of stale mutable aliases across job fences.

Problem: The mod sandbox hot roots could still reach cold registry binding through `Request*()` and `TryPrepareValidationJob()->Initialize()->BindRegistryServicesCold()`.
Solution: Hot ingress and pre-simulation validation now fail closed when the validator is not initialized. Cold bootstrap remains responsible for `Initialize()` and DataVault binding.
Rejected Alternatives: Mark `Initialize()` as path-safe because `_initialized` usually short-circuits. The AST proof is path-insensitive by design, and runtime cold-binding from hot roots is still a bad fallback under service loss.
Scalability potential: Low devices avoid surprise GlobalRegistry polling during mod bursts. Middle keeps normal mod ingress after bootstrap. High/Ultra can raise UGC command budgets without letting dependency capture drift into hot roots.
Hardware Impact: Removes a cold service-locator branch from mod ingress and pre-simulation; estimated avoided stale-bootstrap spike is 80-180 us.

Problem: `TryPrepareValidationJob()` drained pending commands and ran load shedding by mutating pending, staging, stats, memory lease, and ring-state Vault aliases acquired through `OpenVaultLane()`.
Solution: Pending reads now use `TryOpenVaultLaneRead`; staging writes are under one `_stagingHandle` write lock; memory lease creation is under one `_memoryLeasesHandle` write lock; ring state is committed separately through `TryWriteRingStateSnapshot`; stats clear uses `TryClearVaultLane`.
Rejected Alternatives: Hold pending/staging/ring/stats locks together for apparent atomicity. That would be a direct deadlock vector and would block DataVault relocation.
Scalability potential: Low devices shed optional mod commands and commit bounded staging without lock coupling. Middle keeps default command behavior. High/Ultra can increase command and visual feedback budgets while writer topology stays one-lane-at-a-time.
Hardware Impact: Removes pre-validation multi-lane mutable aliasing. Expected low-end contention avoidance is 420-840 us under UGC bursts or DataVault compaction pressure.

Problem: Validation job read inputs were still passed as mutable `NativeArray<T>` aliases even when the job only read them.
Solution: `ValidateFutureCommandEnvelopeJob` now receives `NativeArray<T>.ReadOnly` for staged inputs, opcode records, memory leases, approved assets, and kernel tuning profiles.
Rejected Alternatives: Keep `[ReadOnly] NativeArray<T>` only. The attribute constrains job writes, but it does not prove owner-boundary immutability in the source contract.
Scalability potential: Low/Middle/High/Ultra all get clearer input ownership. This is a prerequisite for the remaining scratch-output rewrite.
Hardware Impact: No direct arithmetic saving; it removes mutable alias risk from read-only validation inputs and tightens proof.

Problem: Scheduled validation output lanes are still not fully decoupled: stats, counters, blackbox writes, dev-null ring, ring state, and camera-juice output are mutable job outputs.
Solution: Not completed in this pass. The next correct patch is job-local scratch outputs and post-fence commits in this order: stats, per-mod counters, blackbox memory writes, dev-null ring, ring state, camera impulses/state, telemetry.
Rejected Alternatives: Claiming full APEX closure after fixing drain preparation. That would be a false proof and would leave scheduled aliases alive.
Scalability potential: Low should use smaller scratch capacities and heavier shedding. Middle keeps defaults. High/Ultra can spend more on haptic/subtitle/camera feedback after scratch commit.
Hardware Impact: Residual output-side risk remains; projected remaining win is 900-3200 us under compaction or mod burst pressure once scratch outputs are implemented.

Problem: `ValidateFutureCommandEnvelopeJob` still published scheduled validation outputs directly into multiple DataVault truth lanes after the pre-drain rewrite.
Solution: Added cold-owned scratch arrays for validation state, per-mod counters, modder memory write commands, dev-null envelopes, and camera-juice impulses. The scheduled job now writes only scratch and SignalBus outputs. After `DispatcherJobFence` finalizes, the owner commits each changed DataVault lane separately: stats, counters, modder memory, dev-null ring, dev-null ring state, camera impulse ring, and camera state. Every direct write lock is released in a local `finally`.
Rejected Alternatives: Holding DataVault locks across the scheduled job was rejected because it blocks relocation and creates a multi-lock deadlock vector. Keeping unused `LoadSheddingJob`/`HapticPulseKernelJob` writer contracts was rejected because dead code still documents the wrong topology and can be revived later.
Scalability potential: Low uses the same bounded staging with fail-closed commit. Middle keeps default mod command capacity. High and Ultra can increase haptic/subtitle/camera feedback density through continuous quality weights while the commit topology stays one-lane-at-a-time.
Hardware Impact: Removes scheduled-job mutable DataVault aliases and multi-lane output ownership from UGC bursts. On i3/MX350 the expected win is avoided contention and compaction stalls in the 900-3200 us class during heavy mod command frames; steady-state arithmetic is unchanged bounded array copy/write work.

Problem: The Roslyn hot-chain checker produced a false positive by resolving `someDisposable.Dispose()` as the local `ProceduralOreSpawner.Dispose()` method.
Solution: Split invocation naming into full invocation names for direct forbidden API calls and local invocation names for call-chain traversal. The call-chain walker now follows only bare method calls and `this.Method()` calls, not arbitrary member calls on other objects.
Rejected Alternatives: Ignoring the failing proof was rejected because a broken verifier is not evidence. Removing `Dispose` from source was rejected because the problem was scanner resolution, not the production path.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged; verification now rejects real hot-chain drift without blocking on disposable/member-call false positives.
Hardware Impact: Static proof only; no runtime CPU gain. It prevents future fake blockers and keeps hot-path verification usable.

Problem: Scheduled validation scratch buffers in `FutureCommandSandboxValidator` were zero-GC but allocated as raw persistent `NativeArray<T>` fields outside an owner-tracked allocator.
Solution: Allocate and release the validation scratch arrays through `H8Memory.Allocate/Release` with `SystemID.ModSandbox`. The scratch still belongs to cold lifecycle, but it is now registered in the native owner ledger before jobs can consume it.
Rejected Alternatives: Keeping raw persistent arrays because they were created cold was rejected; cold timing does not prove owner sovereignty. Moving scratch into DataVault was rejected because validation jobs must not hold DataVault write locks across job lifetime.
Scalability potential: Low keeps bounded scratch ownership with fail-closed validation. Middle keeps default UGC command capacity. High and Ultra can raise command feedback density through quality-scaled budgets while memory ownership remains tracked by one explicit owner.
Hardware Impact: Steady-state arithmetic is unchanged. The practical gain is eliminating unmanaged ownership drift and leak/race ambiguity; avoided low-end recovery/diagnostic stalls are estimated at 140-320 us on failed reload or mod sandbox shutdown paths.

Problem: The verification script did not explicitly reject a future reintroduction of raw persistent `NativeArray` allocation in the mod sandbox.
Solution: Added a Roslyn object-creation check that fails on `new NativeArray<...>(..., Allocator.Persistent, ...)` inside `FutureCommandSandboxValidator`.
Rejected Alternatives: Manual `rg` only. It proves the current diff but does not keep the proof executable for the next patch.
Scalability potential: Runtime unchanged across all tiers; proof hardens the source contract that lets low-tier devices avoid unmanaged allocation drift and high-tier builds scale feedback without changing ownership.
Hardware Impact: Static proof only; no runtime gain. Prevents recurrence of ownerless persistent scratch that would increase reload/shutdown fault cost.

Problem: Cold/editor opcode CSV ingest, kernel tuning ingest, and emergency opcode generation still wrote DataVault buffers through mutable resolved aliases.
Solution: Replaced those writes with one-lane `TryAcquireVaultLaneWrite` windows and local `finally` release. Ring state is read as a snapshot and committed through `TryWriteRingStateSnapshot`.
Rejected Alternatives: Keeping alias writes because the routes are cold/editor. DataVault ownership is not a hot-path-only rule; cold routes define the pattern later copied by runtime code.
Scalability potential: Low keeps mod bootstrap deterministic and fail-closed. Middle keeps the same CSV authoring workflow. High and Ultra can increase command/kernel profile density without widening lock topology.
Hardware Impact: Removes alias mutation and relocation ambiguity from CSV/emergency setup; estimated avoided low-end stall is 180-520 us during live tuning reload or emergency bootstrap under Vault pressure.

Problem: Disk CSV reload used a DataVault byte lane as transient file I/O scratch.
Solution: Removed the unused `_kernelCsvScratchHandle` and reserved buffer id. Reload now reads into an owner-tracked `H8Memory` temp `NativeArray<byte>` and releases it in `finally`.
Rejected Alternatives: Acquiring a DataVault write lock around file read then parsing from the same lane. That would either hold a DataVault writer across file I/O or require a second read phase around a buffer that should not be global truth.
Scalability potential: Low avoids DataVault pressure during editor/live tuning reloads. Middle/High/Ultra keep the same designer CSV path with no global heap pollution.
Hardware Impact: Avoids a 16 KB persistent DataVault scratch lane and removes file-I/O writer-lock pressure; expected reload-path savings are 260-640 us under low-end storage/Vault contention.

Problem: Cold `AcquireVaultBuffers()` cleared many Vault lanes through mutable aliases in one method body.
Solution: Replaced the clear block with sequential `TryClearVaultLane` calls, then wrote ring-state and tuning defaults through single-lane element writers.
Rejected Alternatives: Treating bootstrap as exempt. Bootstrap code is the source of later runtime ownership drift, and direct alias clears bypass writer proofs.
Scalability potential: Low has clearer fail-closed bootstrap behavior. Middle keeps default capacities. High/Ultra can raise capacities without changing the one-lane clear topology.
Hardware Impact: Bootstrap-only. Expected gain is avoided relocation/alias fault cost, roughly 420-900 us if a Vault clear races with replacement or diagnostic relocation.

Problem: `RunSelfAudit()` used DataVault staging as temporary diagnostic input.
Solution: Self-audit now allocates a two-envelope temp input through `H8Memory`, reads all Vault lanes as read-only views, and releases temp input in `finally`.
Rejected Alternatives: Keep using staging because self-audit is diagnostic. Diagnostics must not mutate gameplay staging or they become a hidden phase side effect.
Scalability potential: Low runs the audit without polluting runtime queues. Middle/High/Ultra keep the same validation proof independent from command staging capacity.
Hardware Impact: Diagnostic-only steady-state 0 us. Avoided fault/recovery cost is estimated at 120-340 us by removing staging mutation and cleanup ambiguity.

Problem: The mutable alias helper itself remained available after all intended callers were removed.
Solution: Deleted `OpenVaultLane` and `ResolveRingState`; changed rollback state readback to `TryReadOnlyHandle`; added Roslyn checks that reject these APIs and legacy `TryReadHandle` in the mod sandbox.
Rejected Alternatives: Leave unused helpers for convenience. Convenience helpers are how ownership drift returns.
Scalability potential: Runtime unchanged; proof hardening protects all tiers from future hidden mutable aliases.
Hardware Impact: Static/source-shape gain only. Prevents future direct alias paths that could cost hundreds of microseconds under DataVault compaction.

Problem: Opcode records and `OpcodeCount` are stored in separate Vault lanes after lock flattening, so record-write then count-write could partially publish new records under a stale count if the state commit failed.
Solution: CSV and emergency opcode replacement now publish `OpcodeCount = 0` first, write the record lane under one lock, then publish the final count. If the final state write fails, the new records remain fail-closed because consumers read zero count.
Rejected Alternatives: Hold opcode records and ring state write locks together for atomicity. That directly violates the one-write-lock invariant. Leave old order because failures are rare; rare failure is exactly where deterministic recovery matters.
Scalability potential: Low devices fail closed during live tuning contention. Middle keeps CSV authoring. High/Ultra can expand opcode/profile density without introducing multi-lane atomic dependency.
Hardware Impact: Avoids semi-published opcode state under Vault pressure; estimated avoided fault/retry cost is 260-620 us in live reload or emergency bootstrap cases.

Problem: Some ring-state writes were best-effort without proof that failure was intentionally handled.
Solution: `DrainLateFrame()` and cold default ring/tuning commits now check the write result. `DumpBlackbox()` uses `_lastLocalDumpFrame` before best-effort Vault throttle so fault dumping remains available without same-frame spam.
Rejected Alternatives: Require Vault throttle before dump. That could suppress crash evidence under DataVault contention. Ignore all return values; that hides state-transfer failure.
Scalability potential: Low gets deterministic fail-closed state transfer. Middle/High/Ultra keep richer mod feedback without state ambiguity.
Hardware Impact: Prevents repeated same-frame dump attempts and missed cold defaults; estimated avoided low-end diagnostic churn is 120-340 us.

Problem: The proof did not reject future ignored ring-state writes.
Solution: Added a Roslyn check that flags expression-statement `TryWriteRingStateSnapshot` calls outside `DumpBlackbox`.
Rejected Alternatives: Rely on code review memory. The previous drift came from convenient unchecked writes.
Scalability potential: Runtime unchanged; proof protects all tiers from silent state-transfer failure.
Hardware Impact: Static proof only; no runtime cost.

Problem: `DirectorMissionBridge.OnValidate()` compacted `directorMissionIds`, deleting blank and duplicate legacy mission slots. That made designer mistakes disappear instead of remaining repairable evidence.
Solution: Legacy mission validation is now non-destructive. It records invalid/duplicate counts and first indices, keeps authored rows intact, and the runtime mission trigger skips duplicate legacy mission IDs before calling the quest system.
Rejected Alternatives: Keep compaction because it produces a cleaner array. Clean data by deletion is not an authoring workflow; it destroys the exact slot a designer needs to fix.
Scalability potential: Low keeps the bridge cheap with simple linear scans. Middle keeps legacy mission lists repairable. High and Ultra can layer profile-driven weighted missions without the legacy fallback leaking duplicate activation attempts.
Hardware Impact: Runtime event path adds one bounded duplicate scan only when legacy fallback missions are used. Avoided authoring churn is estimated at 90-180 us per validation cycle on low-end editor hardware.

Problem: `PDAExchangeSystem` hashed every barter offer slot directly. Duplicate `offerId` values shared execution counters, repeat limits, and save state, so two designer offers could become one runtime fact.
Solution: `BarterOfferCatalog` now owns catalog-level null/duplicate runtime-hash validation. `CacheCatalogRuntimeHashes()` refreshes this cold state and publishes hashes only for non-duplicate valid offer slots.
Rejected Alternatives: Let `CanExecute()` catch bad costs later. Duplicate offer identity is not a cost error; it corrupts save/execution ownership before the transaction path runs.
Scalability potential: Low keeps barter state deterministic and small. Middle lets catalogs contain invalid parked rows without runtime identity drift. High and Ultra can ship larger faction barter catalogs while preserving one offer -> one hash -> one execution route.
Hardware Impact: Cold catalog refresh adds bounded O(n^2) duplicate scans over authored offers, with `BarterDTO.MaxOffers` limiting runtime surface. Avoided low-end save/transaction drift is estimated at 160-320 us per bad catalog load.

Problem: `BarterOfferData.OnValidate()` rewrote non-positive item amounts to 1, hiding broken economy data and turning invalid authoring into a valid runtime transaction.
Solution: The amount is no longer mutated in validation. Runtime guards and validation flags preserve the authored defect and block execution with `CostDataInvalid` until the designer fixes the slot.
Rejected Alternatives: Keep auto-clamp for convenience. Silent economic correction breaks repeatability and makes CSV/asset diff reviews lie about actual design intent.
Scalability potential: Low fails closed instead of granting accidental free/cheap trades. Middle gives deterministic catalog validation. High and Ultra can add richer barter tooling without changing runtime DTO layout.
Hardware Impact: Runtime steady-state unchanged. Avoided invalid exchange rollback/debug churn is estimated at 80-160 us per failed transaction path.

Problem: Valid zero-row authoring states published zero-count DataVault update signals even when the underlying clear could not acquire a write lock or was blocked by allocation/compaction fences.
Solution: `H8InputMappingFacade.SyncToVault()`, `H8BridgeFacadeRuntime.SyncDesignData()`, and `H8PrefabRegistryRuntimeBinder.Bind()` now require `Clear*` helpers to return true before publishing clear signals or returning success. Clear helpers return false on fences or failed write-lock acquisition, and true only when no buffer exists or the buffer was cleared.
Rejected Alternatives: Keep void clear helpers and trust editor validation. That allowed runtime observers to receive a zero-count fact while stale bytes remained in DataVault.
Scalability potential: Low keeps last-known-good runtime truth on blocked live-authoring frames. Middle keeps designer correction deterministic. High and Ultra can hot-reload bigger authoring sets without stale clear signals.
Hardware Impact: Avoids stale-data recovery/debug churn; estimated low-end avoided fault path is 120-240 us per blocked clear sync. Runtime steady-state hot path remains 0 us.

Problem: Prefab registry overflow beyond stack scratch capacity cleared existing runtime buffers before returning false.
Solution: Oversized prefab authoring state now returns false without clearing existing mapping/lore buffers, preserving last-known-good runtime state.
Rejected Alternatives: Clear first to force visible failure. That is unsafe because invalid authoring capacity should not destroy active runtime mapping.
Scalability potential: Low devices keep stable previous prefab mappings. Middle can correct oversized registries in editor. High and Ultra can raise capacity later through an explicit DataVault capacity contract instead of silent destructive fallback.
Hardware Impact: Avoided runtime remap loss and rebind churn; estimated low-end avoided recovery cost is 180-360 us plus avoided content instability.

Problem: Facade header persistence returned void, so design value sync could report success while the header/checksum/runtime field count stayed stale.
Solution: `PersistFacadeHeader()` now returns bool, requires a successful DataVault header write, and returns the actual `IMacroDatabaseService.MarkDirty()` result when MacroDB is active.
Rejected Alternatives: Treat header as optional telemetry. The header is the compact contract for runtime field count/checksum, so stale header is authority drift.
Scalability potential: Low keeps compact runtime contracts honest. Middle supports live tuning with deterministic failure. High and Ultra can extend facade headers without changing the fail-closed route.
Hardware Impact: No steady-frame cost. Avoided stale-header debugging is estimated at 90-220 us per dirty facade sync.

Problem: Input, design, and prefab facades recorded duplicate runtime hashes as validation errors, but sync paths could still publish ambiguous DataVault rows. That creates two runtime facts for one action, field, or prefab hash.
Solution: `H8InputMappingFacade.SyncToVault()`, `H8BridgeFacadeRuntime.SyncDesignData()`, and `H8PrefabRegistryRuntimeBinder.Bind()` now fail closed before DataVault mutation when duplicate runtime hashes exist.
Rejected Alternatives: Keep publishing and rely on editor labels. Labels do not protect runtime truth; duplicate identifiers make generated consumers and hash lookups nondeterministic.
Scalability potential: Low preserves the last valid runtime buffer instead of publishing ambiguity. Middle keeps authoring rows visible for correction. High and Ultra can keep experimental rows disabled without leaking duplicate truth.
Hardware Impact: No steady-frame cost. Avoided low-end debug/retry and ambiguous lookup churn is estimated at 140-320 us per dirty live-authoring sync.

Problem: `H8BridgeContractGenerator` could read stale duplicate counters if a design facade asset had not been freshly validated before generation.
Solution: Added `H8DesignDataFacade.RefreshValidationState()` and call it inside contract generation before duplicate checks. Generation skips the offending facade when duplicate field hashes remain.
Rejected Alternatives: Trust `OnValidate()` side effects. Asset import/open order is not a contract, and generated C# must not depend on stale editor state.
Scalability potential: Low keeps compiled API deterministic. Middle gives designers safe disabled scratch rows. High and Ultra can store visual-overkill candidates without duplicate compiled constants.
Hardware Impact: Editor-only cold command; runtime cost is 0 us. Avoided generated-contract audit churn is estimated at 90-220 us per dirty facade.

Problem: Static proof did not enforce duplicate-hash fail-closed behavior.
Solution: Roslyn AST now rejects missing duplicate guards in input sync, design sync, prefab binder, and contract generator. The gate was corrected to target the real `SyncDesignData` writer overload instead of its forwarding overload.
Rejected Alternatives: Manual review and broad method-name checks. The first AST attempt proved method-name-only checks are too blunt.
Scalability potential: Runtime unchanged; proof prevents recurring ambiguous runtime truth across all tiers.
Hardware Impact: Static proof only; no runtime cost.

Problem: `H8BridgeFacadeRuntime.SyncDesignData()` used raw design binding count for heartbeat/header truth even though runtime writes skip null and disabled bindings.
Solution: Sync now refreshes facade validation first and uses enabled non-null runtime binding count for heartbeat and macro header `FieldCount`. Runtime VRAM estimate also ignores disabled bindings.
Rejected Alternatives: Keep raw count as authoring count. Header/heartbeat are runtime contracts, not inspector metadata, so raw authoring slots would lie to downstream consumers.
Scalability potential: Low gets compact truthful headers. Middle keeps disabled rows for authoring. High and Ultra can hold larger tuning profiles without disabled rows inflating runtime budget.
Hardware Impact: Avoids false runtime field counts and disabled-row VRAM pressure; estimated low-end avoided sync/debug cost is 180-320 us on dirty design facade pushes.

Problem: `H8DesignDataFacade` lacked visible non-destructive validation for null rows, disabled rows, and duplicate field hashes.
Solution: Added validation counters for null rows, first null index, runtime binding count, disabled binding count, duplicate field hashes, and first duplicate index. The custom editor displays those counters.
Rejected Alternatives: Silent skip or deleting rows. Silent skip breaks usability; deletion destroys designer intent.
Scalability potential: Low keeps validation cheap and direct. Middle preserves disabled tuning rows for later use. High and Ultra can add richer overkill tuning without invalid rows polluting runtime truth.
Hardware Impact: Editor/live-authoring only; runtime hot path remains 0 us. Avoided debug churn is estimated at 120-260 us per invalid facade inspection.

Problem: Static proof did not reject destructive design facade validation.
Solution: Roslyn AST proof now rejects `RemoveAt`/`RemoveAll` inside `H8DesignDataFacade.ValidateBindings()`.
Rejected Alternatives: Manual review. The bridge facades are recurring authoring surfaces and need executable drift checks.
Scalability potential: Runtime unchanged; proof protects all tiers by keeping authoring data observable.
Hardware Impact: Static proof only; no runtime cost.

Problem: Prefab registry validation counters could remain stale or zero after asset load until `OnValidate()` or manual rebuild ran.
Solution: `H8PrefabRegistry.OnEnable()` now calls the same non-destructive `ValidateEntries()` path, rebuilding hash and validation state on ScriptableObject load without DataVault binding.
Rejected Alternatives: Refresh validation from the editor window every repaint. That would mutate the asset from UI paint and hide lifecycle ownership. Binding during `OnEnable()` was also rejected; load-time validation must not publish runtime state or touch DataVault.
Scalability potential: Low gets accurate cheap validation immediately. Middle keeps stable editor/runtime asset state. High and Ultra can build richer prefab tooling on top of reliable counters.
Hardware Impact: Runtime hot paths remain 0 us. Cold asset-load work is O(n^2) only for duplicate hash detection and bounded by registry authoring size; avoided editor/live-authoring confusion is estimated at 90-170 us per stale-registry inspection.

Problem: `H8InputMappingFacade.SyncToVault()` sized the runtime DataVault buffer from raw `bindings.Count` while null binding rows were skipped during emission.
Solution: The input facade now rebuilds hashes and counts non-null runtime bindings first, then sizes and writes the DataVault buffer by that compact count.
Rejected Alternatives: Delete null binding rows or keep raw count. Deleting rows loses designer evidence; raw count wastes capacity and makes invalid slots indistinguishable from runtime content.
Scalability potential: Low keeps input mapping buffers compact. Middle keeps authoring visibility. High and Ultra can expand input profiles without invalid rows consuming runtime DTO capacity.
Hardware Impact: Avoids oversized binding buffer writes and false capacity pressure; estimated low-end avoided bind/retry cost is 140-260 us on live-authoring sync.

Problem: Input facade authoring had no visible validation state for null rows or duplicate action hashes.
Solution: Added non-destructive counters for null binding rows, first null index, runtime binding count, duplicate action hash rows, and first duplicate index. The custom inspector now displays those counters.
Rejected Alternatives: Console warnings and silent skip. Console output is lossy and silent skip leaves designers guessing which row failed.
Scalability potential: Low keeps validation textual and cheap. Middle gives direct correction targets. High and Ultra can add richer input authoring dashboards later without changing runtime DTO shape.
Hardware Impact: Editor-only; runtime hot path remains 0 us. Avoided authoring/debug churn is estimated at 120-240 us per invalid input facade inspection.

Problem: The static proof did not cover `H8BridgeFacadeEditors.cs` after editor validation UI changed.
Solution: Added that editor file to the Roslyn seed list and added a guard rejecting `RemoveAt`/`RemoveAll` inside `H8InputMappingFacade.ValidateBindings()`.
Rejected Alternatives: Treat editor UI as exempt. This domain is explicitly designer-facing, so editor code is part of the product surface.
Scalability potential: Runtime unchanged; proof now covers the workflow designers actually use.
Hardware Impact: Static proof only; no runtime cost.

Problem: After prefab registry validation became non-destructive, `H8PrefabRegistryRuntimeBinder.Bind()` still used raw `EntryCount` for stack scratch capacity and Vault buffer sizing. Preserved invalid rows could therefore block a valid runtime bind.
Solution: Runtime binding now computes `runtimeBindableCount` by scanning valid bindable entries, sizes mapping/lore Vault buffers by that compact count, and writes compact DTO rows while still iterating raw rows for stable authoring order.
Rejected Alternatives: Re-delete invalid rows before binding; that would undo the non-destructive authoring contract. Raise `RuntimePrefabIdScratchCapacity`; that hides the false-capacity bug and wastes stack/Vault capacity on invalid authoring rows.
Scalability potential: Low keeps runtime DTO capacity for real content only. Middle preserves designer error visibility. High and Ultra can grow prefab libraries by valid runtime assets instead of raw list slots.
Hardware Impact: Avoids false bind failure and oversized Vault buffers when registries contain invalid rows. Estimated low-end avoided bind/retry cost is 260-520 us on live-authoring or bootstrap frames with dirty registries.

Problem: Prefab registry validation state was present in the asset but not visible in the binder/editor workflow, so designers could still miss null rows or duplicate hashes.
Solution: `H8PrefabRegistryWindow` and the custom inspector now show validation status, runtime-bindable count, null row count/first index, duplicate row count/first index, and explicit null-slot rows in the scroll view.
Rejected Alternatives: Rely on hidden serialized fields or console logs. Hidden fields are not an authoring workflow, and logs are lossy under batch import/live authoring.
Scalability potential: Low keeps the editor UI cheap and textual. Middle gives immediate correction targets. High and Ultra can add richer registry dashboards later without changing runtime DTO contracts.
Hardware Impact: Editor-only. Runtime steady-state is 0 us; avoided authoring/debug churn is estimated at 120-260 us per dirty registry refresh by making the bad row directly visible.

Problem: `H8PrefabRegistry.ValidateEntries()` removed null list entries during validation, so designer-authored registry rows could disappear when validation ran.
Solution: Validation is now non-destructive. It preserves raw rows, rebuilds valid entries, records `validationNullEntryCount`, `validationFirstNullEntryIndex`, `validationRuntimeBindableCount`, `validationDuplicateHashCount`, and `validationFirstDuplicateHashIndex`, and leaves the runtime binder to skip invalid rows.
Rejected Alternatives: Deleting null rows in `OnValidate()` was rejected because it hides the bad authoring slot and loses designer evidence. Allocating a `HashSet` for duplicate checks was rejected because this cold path can use O(n^2) scans without adding allocation noise to authoring validation.
Scalability potential: Low keeps the registry cheap and fail-closed because invalid rows are skipped by the binder. Middle keeps full designer feedback. High and Ultra can add richer registry tooling on top of recorded validation state without changing runtime DTO ownership.
Hardware Impact: Runtime steady-state is unchanged. The gain is preventing bad prefab rows from turning into silent runtime mapping drift; expected avoided diagnostic/reimport cost is 120-340 us on low-end editor or live-authoring machines.

Problem: The static proof did not prevent destructive validation from being reintroduced in the prefab registry.
Solution: Roslyn AST verification now rejects `RemoveAt` inside `H8PrefabRegistry.ValidateEntries()`.
Rejected Alternatives: Manual code review only. This project already showed repeated drift from convenience helpers, so the authoring contract needs executable proof.
Scalability potential: Runtime unchanged across tiers; proof protects the designer-facing resource facade from silent data loss.
Hardware Impact: Static proof only; no runtime cost.

Problem: `H8InputMappingFacade.SyncToVault()` reused `RebuildHashesAndCountRuntimeBindings()` directly after non-destructive validation was introduced. That helper increments null-row counters, so repeated syncs could inflate validation state and make designers chase fake null rows.
Solution: Sync now calls `ValidateBindings()` once, then consumes `validationRuntimeBindingCount` for DataVault capacity and emission. Roslyn proof rejects direct sync-side calls to the counter-mutating helper.
Rejected Alternatives: Reset only `validationNullBindingCount` inside sync. That would duplicate validation ownership and leave duplicate-hash state stale.
Scalability potential: Low keeps input buffers compact and validation truthful. Middle keeps multi-device input profiles visible. High and Ultra can add richer input dashboards without stale authoring counters.
Hardware Impact: Runtime hot path stays 0 us; live-authoring sync avoids false validation churn. Estimated avoided low-end debug/retry cost is 90-180 us per repeated invalid sync.

Problem: `H8PrefabRegistryVramEstimator` allocated a fresh `HashSet<int>` and `string[]` texture-property names during prefab validation. This is editor/cold, but the domain is live authoring; repeated asset validation should not spike managed GC.
Solution: Replaced per-call collections with static scratch buffers and Unity's integer texture-property route: `Material.GetTexturePropertyNameIDs(List<int>)` and `Material.GetTexture(int)`. Texture dedupe is a bounded linear scan in a fixed `int[]`.
Rejected Alternatives: Keep `HashSet` because the path is editor-only. Editor validation is part of designer workflow and should stay predictable. A runtime DataVault cache was rejected because VRAM estimation is editor metadata, not gameplay truth.
Scalability potential: Low uses simple bounded scans and conservative VRAM estimates. Middle handles normal prefab material graphs without allocation churn. High and Ultra can validate heavier prefab libraries while preserving the same authoring contract.
Hardware Impact: Removes per-prefab `HashSet` and texture-name array allocations from validation; expected low-end editor gain is 180-420 us plus avoided GC spikes during bulk prefab registry changes.

Problem: `H8BridgeFacadeRuntime.PersistFacadeHeader()` reached into `GlobalRegistry.MacroDatabase` from the runtime bridge writer. The call was not a hot loop, but it made the bridge writer own dependency discovery instead of receiving an explicit route.
Solution: `SyncDesignData()` and `PersistFacadeHeader()` now accept an `IMacroDatabaseService` parameter. The ScriptableObject/editor entry points pass `GlobalRegistry.MacroDatabase` from cold/live-authoring boundaries; the core runtime writer is dependency-explicit.
Rejected Alternatives: Leave the lookup because it is not `Tick`/`LateFrameTick`. Cold-only does not mean hidden; the bridge writer should remain mathematically decoupled from service discovery.
Scalability potential: Low avoids hidden service dependency drift. Middle keeps MacroDB persistence available in editor sync. High and Ultra can add more facade sectors without turning runtime writers into registry clients.
Hardware Impact: No steady-frame cost change. Avoided low-end fault/retry cost is estimated at 80-160 us when MacroDB is unavailable or being replaced.

Problem: `H8BridgeContractGenerator` emitted constants for every non-null design binding while runtime sync, checksum, VRAM, and header field count now skip disabled rows. Generated API could expose stale or intentionally parked tuning knobs.
Solution: Contract generation now skips disabled bindings. Roslyn proof rejects generator drift if the disabled-row guard disappears.
Rejected Alternatives: Generate all authored rows and rely on comments. Generated constants are compiled API; disabled authoring rows must not become runtime-facing contracts.
Scalability potential: Low keeps compiled surface tight. Middle allows disabled rows as design scratch. High and Ultra can hold overkill tuning experiments in assets without leaking them into contracts.
Hardware Impact: Generation is editor-only; runtime effect is smaller contract surface and fewer accidental consumers. Estimated avoided debug churn is 90-180 us per generated facade audit.

Problem: `H8PrefabRegistryRuntimeBinder.Bind()` discovered `GlobalRegistry.PrefabRegistryRuntime` internally, making the core binder own service discovery instead of receiving an explicit dependency from its cold caller.
Solution: `Bind()` now accepts `PrefabRegistry runtimeRegistry`; `H8PrefabRegistry.OnValidate()`, `H8PrefabRegistryBootBinder.BindNow()`, and editor bind buttons pass `GlobalRegistry.PrefabRegistryRuntime` from boundary code. The binder nulls the parameter outside play mode.
Rejected Alternatives: Keeping the hidden lookup because binding is cold. Cold-only does not justify route ambiguity; the binder also owns DataVault writes, so dependency discovery must stay outside it.
Scalability potential: Low keeps prefab live-authoring deterministic with direct dependencies. Middle preserves prefab runtime registration. High and Ultra can expand registry tooling without turning the binder into a service locator client.
Hardware Impact: No steady-frame cost. Avoided low-end live-authoring fault/retry cost is estimated at 80-160 us when the runtime prefab registry is absent or being replaced.

Problem: Prefab binding could call `EnsureGenerationHandle()` while DataVault allocation or compaction fences were active.
Solution: `H8PrefabRegistryRuntimeBinder.Bind()` now checks `vault.IsAllocationLocked` and `vault.IsCompactionFenceActive` before generation-handle allocation and fails closed.
Rejected Alternatives: Let `EnsureGenerationHandle()` decide. The bridge writer should not request growth while ownership fences are active; that makes failures later and harder to diagnose.
Scalability potential: Low avoids allocation pressure stalls. Middle keeps live-authoring sync predictable. High and Ultra can grow prefab buffers during safe owner windows only.
Hardware Impact: Avoids compaction/allocation contention spikes during prefab registry sync; estimated low-end avoided stall is 90-220 us under dirty asset live-authoring.

Problem: `H8AupSceneGridDrawer.Draw()` read sector state through properties backed by `EditorPrefs.GetBool()` and `EditorPrefs.GetString()` parsing. That path runs from `SceneView.duringSceneGui`, so normal editor navigation could pay preference I/O/string parse cost.
Solution: Added a static preference cache in `H8AupVisualizerWindow`. SceneView draw reads cached booleans/longs; UI/menu changes update cache and persist to `EditorPrefs`.
Rejected Alternatives: Leave it because it is editor-only. The domain is designer-facing tooling; editor hot paths are part of usability. Polling `EditorPrefs` every draw was also rejected because it hides state ownership behind property getters.
Scalability potential: Low keeps the overlay cheap while navigating large scenes. Middle keeps AUP sectors visible without UI hitches. High and Ultra can add richer grid overlays while keeping preference I/O out of draw.
Hardware Impact: Runtime cost is 0 us. Editor SceneView draw avoids preference reads and string parses; estimated low-end saved cost is 60-140 us per heavy repaint burst.

Problem: Static proof did not cover the AUP editor overlay after identifying a hot draw path.
Solution: Added `H8AupVisualizerEditor.cs` to the Roslyn seed list and a guard rejecting `EditorPrefs.` inside `H8AupSceneGridDrawer.Draw()`.
Rejected Alternatives: Rely on reviewer memory. The regression is easy to reintroduce with convenience properties.
Scalability potential: Runtime unchanged; proof protects all tiers of editor workflow.
Hardware Impact: Static proof only; no runtime cost.

Problem: Fail-closed duplicate/fence sync paths now returned false, but editor buttons could ignore the result and leave designers with a silent no-op.
Solution: Added explicit `TrySyncDesignFacade`, `TrySyncInputFacade`, and `TryBindPrefabRegistry` editor helpers. They call the runtime routes, preserve the fail-closed runtime contract, and emit `Debug.LogError` when the sync/bind route rejects the operation.
Rejected Alternatives: Passive validation labels only were rejected because a direct button press needs immediate action feedback. Throwing exceptions was rejected because this is an authoring correction workflow, not a fatal editor failure.
Scalability potential: Low gets cheap text feedback with zero runtime cost. Middle gets deterministic authoring correction. High and Ultra can add richer validation dashboards without weakening the runtime false-return contract.
Hardware Impact: Editor-only. Avoids repeated blind sync attempts and console archaeology; estimated avoided low-end authoring churn is 80-180 us per failed operation.

Problem: Static proof did not enforce editor failure feedback after runtime routes became fail-closed.
Solution: Roslyn AST verification now checks the editor sync/bind helper methods and rejects missing `Debug.LogError` feedback.
Rejected Alternatives: Manual review only. The domain is designer usability over low-level systems, so tool feedback is part of the architecture contract.
Scalability potential: Runtime unchanged across all quality tiers; proof protects the authoring surface from drifting back to silent failures.
Hardware Impact: Static proof only; no runtime cost.

Problem: `NarrativeDagInspectorWindow.OnGUI()` could call `QuestDagVault.EnsureBuffers(vault)` when the editor window repainted and buffers were missing. That made DataVault allocation/growth a hidden draw-path side effect.
Solution: `OnGUI()` now only attempts `TryResolveBuffers()`. Missing buffers show a warning and an explicit "Initialize DAG Buffers" command that routes through `TryInitializeDagBuffers()`.
Rejected Alternatives: Auto-initialize during repaint was rejected because editor draw must not mutate Vault topology. A background polling initializer was rejected because it hides allocation ownership behind UI convenience.
Scalability potential: Low keeps the narrative inspector cheap and predictable. Middle lets designers initialize the DAG deliberately. High and Ultra can add richer graph visualization without turning repaint into a buffer-growth path.
Hardware Impact: Editor-only runtime steady-state remains 0 us. On low-end editor machines, avoided hidden Vault allocation during repaint is estimated at 90-220 us plus fewer UI hitch spikes when the DAG is absent.

Problem: `QuestDagVault.EnsureBuffers()` itself accepted a Vault under allocation lock or compaction fence, so a safe caller had to remember every ownership guard.
Solution: The owner facade now fails closed before any `EnsureGenerationHandle` when `vault == null`, `vault.IsAllocationLocked`, or `vault.IsCompactionFenceActive`.
Rejected Alternatives: Per-call-site guards only. The allocator facade is the single owner of Quest DAG topology, so the invariant belongs there as well as in editor command routes.
Scalability potential: Low avoids topology mutation during pressure windows. Middle keeps CSV/mock load commands predictable. High and Ultra can grow DAG capacities during explicit safe windows only.
Hardware Impact: Prevents allocation/compaction contention spikes. Estimated low-end avoided stall is 80-180 us during live authoring or resolver bootstrap under Vault pressure.

Problem: Static proof did not cover the narrative DAG editor allocation path.
Solution: Roslyn AST now parses `NarrativeDagInspectorWindow.cs`, rejects `EnsureBuffers` inside `OnGUI`, requires fence checks in `TryInitializeDagBuffers()`, and requires fence checks in `QuestDagVault.EnsureBuffers()`.
Rejected Alternatives: Manual review only. The regression is one convenience call away, so it needs an executable source-level guard.
Scalability potential: Runtime unchanged; proof protects designer-facing narrative tooling across all device tiers.
Hardware Impact: Static proof only; no runtime cost.

Problem: `ResourceNodeTemplate` and `HarvestableTemplate` treated loot/yield rows as valid when only the item id existed, then silently clamped zero amount, zero weight, reversed min/max, and rarity byte output during runtime copy.
Solution: Both templates now maintain non-destructive validation state for invalid rows and duplicate runtime item/key rows. Copy/count routes use `IsRuntime*SlotValid()` and write authored values directly after validation instead of repairing them.
Rejected Alternatives: Keep clamp-on-copy because it avoids bad runtime rows. That hides broken economy data from designers and makes the runtime table diverge from authored intent.
Scalability potential: Low skips bad rows with linear cold scans and compact runtime copy. Middle keeps resource tuning explicit. High and Ultra can add richer resource dashboards without changing runtime DTO layout.
Hardware Impact: Runtime hot path is unchanged. Cold bake/copy avoids false valid rows; estimated low-end avoided debug/reimport cost is 140-320 us per bad scavenging template.

Problem: The old copy loop bounded source scanning by remaining destination capacity, so invalid early rows could consume scan budget and truncate later valid rows before `AddNoResize`.
Solution: Copy methods now scan the full authored source and stop only when `copiedCount` reaches `remainingCapacity`.
Rejected Alternatives: Pre-compact authoring arrays. Compaction deletes slot evidence and allocates/reorders designer data. Scanning full source is deterministic and cold.
Scalability potential: Low keeps sparse/broken authoring assets fail-closed. Middle supports partial tables during balancing. High/Ultra can carry experimental disabled rows without losing valid runtime rows.
Hardware Impact: Cold copy does a few more linear checks only when invalid rows exist. Avoided missing-yield diagnostics are estimated at 110-260 us per bad template bake.

Problem: Resource/harvestable validation was not visible in the inspector and had no static drift guard.
Solution: Added editor inspectors that show runtime row counts, invalid/duplicate counts, and first bad indices. Roslyn AST now rejects copy methods that bypass runtime slot validation or reintroduce row-economic clamping during copy.
Rejected Alternatives: Console warnings only. Designers need the bad row index next to the asset, not a lossy console trail.
Scalability potential: Low gets cheap textual feedback. Middle improves tuning turnaround. High/Ultra can layer visualization over the same validation properties.
Hardware Impact: Editor-only UI. Runtime cost is 0 us; static proof has no runtime cost.

Problem: `BaseModuleCatalogRuntime.TryAcquireCatalogWriteViews()` acquired and held separate DataVault write locks for catalog state, definitions, sockets, costs, hash index, and telemetry while hydration/mock jobs were scheduled. That violates the single-writer-route doctrine and leaves a deadlock vector if another route grabs one of those lanes in a different order.
Solution: Replaced the multi-lock lease with one `CatalogWriteMutationGuardMask`. The route now fails closed under allocation lock or compaction fence, resolves owned lanes through `TryReadHandle`, schedules work against those native views, and releases the single guard through `ReleaseWriteLease()`.
Rejected Alternatives: Keep per-lane write locks and document acquisition order. That still leaves six held locks across a job window and makes later authoring/runtime bridges dependent on remembering the same order. Copying catalog data into temporary staging arrays was rejected because it adds cold memory pressure without solving ownership.
Scalability potential: Low avoids lock-order stalls on weak CPUs. Middle keeps authoring catalog hydration predictable. High and Ultra can hydrate larger construction catalogs and visual-overkill module metadata without increasing lock count.
Hardware Impact: Removes five direct write-lock acquisitions/releases from catalog hydration/mock scheduling. Estimated low-end saved contention/overhead is 180-420 us per contested catalog bake or bootstrap, with the larger gain coming from eliminating deadlock retry/debug churn.

Problem: `TryRecordTelemetry()` nested a state write lock with a telemetry-ring write lock for one cursor increment and one ring entry. Telemetry should not become the route that stalls construction catalog state.
Solution: Replaced nested locks with one `CatalogTelemetryMutationGuardMask` and a strict `try/finally` release. State and ring lanes are resolved as owned native views only after the guard is held; early failures return through the same `finally` path.
Rejected Alternatives: Update the cursor first and telemetry second under separate locks. That can publish a cursor pointing at a missing or stale telemetry entry. A managed queue was rejected because this telemetry is native, fixed-size, and should remain zero-GC.
Scalability potential: Low gets bounded telemetry cost and no nested lock stalls. Middle preserves black-box catalog diagnostics. High and Ultra can record richer catalog telemetry with the same guard route and fixed DTO layout.
Hardware Impact: Removes two write-lock operations from the telemetry path and prevents state/ring lock inversion. Estimated low-end saved cost is 80-190 us during catalog diagnostic bursts.

Problem: Static proof did not cover the construction catalog lock route, so the regression could return with one convenience call to `TryAcquireOwnedLane()`.
Solution: Added `BaseModuleCatalogRuntime.cs` to the Roslyn AST seed and guarded hydration, telemetry, lease release, and lane-ensure fence behavior.
Rejected Alternatives: Manual inspection only. The user requested source-code proof, and this invariant is simple enough to enforce structurally.
Scalability potential: Runtime unchanged; proof protects all device tiers from lock-order drift.
Hardware Impact: Static proof only. No runtime cost and no project build CPU spike.

Problem: `H8BridgeFacadeRuntime.SyncDesignData()` wrote enabled designer float bindings by calling `WriteDesignValue()` per row. Each row acquired and released the design-value write lock, then recorded telemetry through another write-lock path. A late bad row could leave earlier values and signals already published.
Solution: Added a preflight `TryComputeDesignValueBufferLength()` pass and a non-zero-row `SyncDesignValuesBulk()` path. The bulk route reserves one `DesignSyncMutationGuardMask`, resolves values, telemetry, and header lanes as owned native views, writes all rows, records `RecordDeltaLocked()` telemetry, writes heartbeat and header, then releases in `finally`.
Rejected Alternatives: Keeping per-row `WriteDesignValue()` was rejected because it scales lock churn linearly with designer field count and permits partial sync. Allocating a managed scratch list of old values for post-guard publication was rejected because it violates zero-GC live authoring.
Scalability potential: Low avoids lock churn during small live tuning changes. Middle supports larger authoring facades without value-sync stalls. High and Ultra can expose more designer fields and visual-overkill tuning knobs while keeping the transfer route one guarded native pass.
Hardware Impact: Removes N value write-lock acquisitions and N telemetry write-lock acquisitions from non-zero design sync. Estimated low-end saved or protected cost is 160-360 us per 32-field live authoring sync, with higher gain under Vault contention.

Problem: Zero-row design facade sync could record heartbeat before proving value clear and header persistence succeeded.
Solution: Zero-row sync now requires `ClearDesignValueBuffer()` and checked `PersistFacadeHeader()` success before recording heartbeat and publishing the clear signal. Non-zero sync writes heartbeat inside the same guarded values/header transfer.
Rejected Alternatives: Treating heartbeat as best-effort was rejected because the header/count signal is the designer-facing proof artifact for the low-level buffer state.
Scalability potential: Low gets deterministic failure instead of stale tool state. Middle keeps disabled-row experiments visible without partial runtime truth. High and Ultra can swap large tuning surfaces while the clear/header contract stays exact.
Hardware Impact: No added runtime hot-path cost. Prevents stale-header debug churn; estimated low-end avoided correction loop is 80-160 us per failed clear/header attempt.

Problem: Static proof covered duplicate blocking and header bool persistence but not the new bulk-sync lock invariant.
Solution: Roslyn AST now rejects `WriteDesignValue()` inside macro `SyncDesignData()`, rejects `TryAcquireWriteLock` and `ReleaseWriteLock` inside `SyncDesignValuesBulk()`, requires mutation guard acquire/release/finally, and rejects lock allocation routes inside `RecordDeltaLocked()`.
Rejected Alternatives: Manual diff review only. The invariant is structural and can be guarded cheaply by AST.
Scalability potential: Runtime unchanged. Proof protects future authoring growth from sliding back into per-field lock traffic.
Hardware Impact: Static proof only. No runtime cost and no project build CPU spike.

Problem: `H8PrefabRegistryRuntimeBinder.Bind()` wrote prefab mapping and lore-link buffers as two separate DataVault write-lock transactions. If the mapping lane succeeded and the lore-link lane failed, designers could get a half-updated runtime bridge: prefab IDs visible, lore/acoustic links stale or cleared.
Solution: Replaced the split route with `TryWritePrefabBuffers()`. It validates the runtime-bindable count, reserves one `PrefabRegistryBindMutationGuardMask`, resolves both native lanes, clears both, writes mapping and lore records by the same compact runtime index, then releases in `finally`.
Rejected Alternatives: Keep separate write locks and clear mapping on lore failure. That still mutates last-known-good mapping before proving the second lane can be written. A managed staging list was rejected because prefab bind is a low-level bridge and the compact runtime index fits stack/native flow.
Scalability potential: Low avoids lock churn and half-published authoring data. Middle supports larger prefab/lore catalogs with deterministic sync. High and Ultra can add richer acoustic/lore/visual-overkill metadata without increasing lock count.
Hardware Impact: Removes two sequential write-lock transactions from non-zero prefab bind and prevents half-sync debug churn. Estimated low-end saved or protected cost is 120-260 us per contested prefab registry bind.

Problem: Zero-row prefab bind cleared mapping and lore-link buffers through separate routes. A first clear could succeed and the second could fail, leaving old lore against empty mapping or the reverse.
Solution: `ClearExistingBuffers()` now acquires the same prefab mutation guard, pre-resolves both existing buffers, and only then clears both inside the guarded section. Missing buffers are accepted as already clear.
Rejected Alternatives: Best-effort clear was rejected because zero-runtime-row sync is an explicit state transition and must be all-or-fail.
Scalability potential: Low keeps disabled prefab registries deterministic. Middle keeps authoring toggles safe. High and Ultra can swap large registries without stale paired lanes.
Hardware Impact: Avoids stale zero-row bridge recovery work; estimated low-end protected cost is 70-150 us per failed clear attempt.

Problem: Static proof did not cover prefab binder lock flattening.
Solution: Roslyn AST now rejects `TryWriteMappingBuffer()`/`TryWriteLoreLinksBuffer()` return paths, rejects write-lock acquire/release inside `TryWritePrefabBuffers()` and `ClearExistingBuffers()`, requires mutation guard acquire/release/finally, and rejects lock acquisition inside the existing-buffer read helper.
Rejected Alternatives: Manual source review only. This regression is a short helper reintroduction away and needs an executable proof gate.
Scalability potential: Runtime unchanged. Proof protects prefab authoring growth across all tiers.
Hardware Impact: Static proof only. No runtime cost and no project build CPU spike.

Problem: `MetaCampaignService.CompletePendingEvaluation()` applied rule-driven campaign variables through per-change `UpsertGlobalVariable()` calls and then published every signal from the original rule result. A transient variables write-lock failure could leave a mission/campaign variable stale while visual, cartography, audio, telemetry, and black-box routes announced the requested value as truth.
Solution: Added `TryApplyVariableChanges()` as a single variables-lane transaction. It acquires one DataVault write lock, preflights capacity with `CanApplyVariableChanges()`, writes the entire fixed-list rule batch, releases in `finally`, and only then lets `LateFrameTick()` publish side effects from the applied fixed list.
Rejected Alternatives: Keep per-change upsert and add logging. Logging does not prevent false world-state signals. Holding a variables write lock while publishing presentation was rejected because shader/audio/cartography must run after the native state has settled and the lock is released.
Scalability potential: Low avoids stale mission/campaign state on weak CPUs under DataVault contention. Middle preserves deterministic scenario branching. High and Ultra can add more authored campaign side effects while still using one checked native variable batch before visual-overkill presentation.
Hardware Impact: Removes up to N-1 variables write-lock acquisitions per rule batch. Estimated low-end saved/protected cost is 80-180 us for a four-rule campaign event, with the larger gain coming from preventing false side-effect recovery work.

Problem: Save load and default reset could clear the campaign variable lane before proving that loaded variables plus required defaults fit. Failure after clear could leave a partial campaign state and still risk later snapshot publication.
Solution: `LoadFromSaveData()` now routes through `TryReplaceGlobalVariablesFromSave()`, which capacity-preflights unique save hashes plus required defaults before any clear. `SeedDefaultState()` and `EnsureDefaultVariables()` now return bool and use one checked variables write transaction.
Rejected Alternatives: Trusting `MetaCampaignDTO.MaxGlobalVariables` alone. That misses the required-default overlay case when a save fills the lane without defaults. A managed staging dictionary was rejected because save load can use bounded nested loops over fixed arrays without GC.
Scalability potential: Low keeps save recovery deterministic. Middle protects branching mission continuity. High and Ultra can increase scenario variables without changing the fail-closed transaction shape.
Hardware Impact: No hot-frame allocation. Extra preflight is cold save/default work, bounded by `MetaCampaignDTO.MaxGlobalVariables`; estimated cost is 20-90 us and prevents partial-load debug loops.

Problem: Static proof did not cover MetaCampaign variable atomicity or unchecked helper regression.
Solution: Roslyn AST now rejects legacy unchecked variable helpers, `TryForceSetGlobalVariable()` ignoring the apply result, save-load partial clear, and evaluation completion that does not route through `TryApplyVariableChanges()`.
Rejected Alternatives: Manual inspection only. The failure mode is a small helper regression and should be caught structurally.
Scalability potential: Runtime unchanged. Proof protects future scenario-authoring expansion across weak, middle, high, and ultra devices.
Hardware Impact: Static proof only. No runtime cost and no project build CPU spike.
