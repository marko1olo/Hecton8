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
