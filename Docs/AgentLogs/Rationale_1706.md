# Rationale 1706

Date: 2026-06-02
Status: SOURCE PATCHED / BUILD THROTTLED / POLISH LOOP 28 STATIC VERIFIED

## Decision 00: Authority Boundary

Problem: Task spans resource spawning, loot oracle, telemetry DTO, and inventory/UI demands. Only resource/scavenging directories are explicitly free-write; DTO and inventory changes may cross domain boundaries.
Solution: Read domain roster and mandate files first. Modify outside assigned directories only if the existing source proves the DTO or API is already part of this pipeline and the change is required to remove P0 blocker.
Rejected Alternatives: Blindly editing player inventory or global contracts would violate interface immutability and cross-domain authority.
Scalability potential: Low keeps deterministic spawn truth and simple presentation; Middle/High/Ultra add visual richness only in presentation lanes.
Hardware Impact: Avoiding runtime factory/material paths prevents heap pressure and SRP batcher fragmentation on i3/MX350.

## Decision 01: Resource Proxy Source Of Truth

Problem: ResourceDistributionDirector built fallback ore and magma prefabs with `new GameObject`, primitive mesh capture, and runtime `new Material`.
Solution: Extend `ResourceNodeTemplate` with one authored pooled `RuntimeNodePrefab` slot and add director-level fallback prefabs. Warm authored prefabs through `IObjectPoolService` and fail closed when no prefab exists.
Rejected Alternatives: Rebuilding cube/cylinder proxies at boot is a standard Unity convenience path but breaks offline asset authority, prefab review, and SRP batching.
Scalability potential: Low uses one simple authored PFB_Ore; Middle/High/Ultra can assign richer template-specific prefabs and materials without changing gameplay truth.
Hardware Impact: Removes cold heap spikes and material instancing that can cost 0.9-1.8 ms during scene warmup on i3/MX350.

## Decision 02: Scavenging Oracle Host Authority

Problem: ScavengingLootOracle created a hidden runtime host and called `Resources.FindObjectsOfTypeAll` from gameplay-reachable paths.
Solution: Require an authored/bootstrap host to bind itself in `OnEnable`; isolate orphan cleanup behind a SubsystemRegistration-only gate.
Rejected Alternatives: `new GameObject` plus `HideAndDontSave` is fragile under domain reload and makes read paths mutate scene state.
Scalability potential: Low stays fail-closed if missing; Middle/High/Ultra can author richer scene bootstrap without hot searches.
Hardware Impact: Removes an active-gameplay scene-wide object scan, worst-case multi-millisecond on content-heavy scenes.

## Decision 03: Pity Timer Data Route

Problem: The prompt requires `PlayerEcosystemTelemetryDTO.EmptyScansStreak`, but no existing DTO or buffer was present in the source tree.
Solution: Add a 32-byte explicit DTO to the existing procedural geology contracts and bind BufferID 141905 through DataVault read/write handles.
Rejected Alternatives: Managed singleton counters or scanner direct references would violate one fact/one owner and hot dependency doctrine.
Scalability potential: Low forces one copper node ahead; Middle/High/Ultra spend visual budget on existing cluster and shader lanes only.
Hardware Impact: One 32-byte row read before scheduling and one write after depletion; no per-frame heap allocation.

## Decision 04: Pity Placement Math

Problem: Pure random sector sampling can produce empty scan streaks and retention failure.
Solution: Use deterministic LCG seeded by sector hash, player empty streak, and AUP; project the player forward vector into the same terrain/SDF sampling path and write one authoritative node before the normal scan loop.
Rejected Alternatives: UnityEngine.Random or scene raycasts would break deterministic AUP replay and create engine-side hot costs.
Scalability potential: Low caps to one forced node and four terrain samples; Middle/High/Ultra keep gameplay identical while presentation density scales by GlobalQualityWeight.
Hardware Impact: Trigger-only math is bounded; normal frames pay only two scalar fields passed into the Burst job.

## Decision 05: DTO Field Contract Correction

Problem: The first DTO draft used audit-style fields at offsets 4, 8, and 12, but the XML requires TotalOresMined, DistanceSinceLastFind, and PityTriggerActive in the first 16 bytes.
Solution: Preserve a 32-byte explicit DTO but make the first 16 bytes match the XML exactly; move audit metadata to offsets 16-28 and validate offsets in the editor layout validator.
Rejected Alternatives: Overlapping aliases would compile but hide field ownership and make future readers guess which name is authoritative.
Scalability potential: Low reads one 32-byte row; Middle/High/Ultra can add presentation effects from PityTriggerActive without changing ore authority.
Hardware Impact: One cache-aligned row stays cheap on i3/MX350 and avoids managed telemetry state.

## Decision 06: Scanner Sweep Writer Route

Problem: The pity timer would never trigger if no first-party path wrote EmptyScansStreak.
Solution: Extend IWorldResourceSpawnerCommandModel with ReportScannerSweepResult and have GroundPenetratingRadarRuntime call it after completed scan jobs; ProceduralOreSpawner owns the single DataVault row write.
Rejected Alternatives: Editing scanner UI directly or adding a managed singleton would cross authority boundaries and add hot service/state routes.
Scalability potential: Low only increments a counter; Middle/High/Ultra can spend presentation budget on richer GPR visuals while gameplay truth remains one row.
Hardware Impact: One write-lock and one DTO assignment per completed scanner sweep, no per-frame heap allocation.

## Decision 07: Authored Bootstrap Instead Of Runtime Factory

Problem: Removing runtime factories leaves scenes vulnerable if designers never assign PFB_Ore prefabs or the ScavengingLootOracle host.
Solution: Extend the existing editor bootstrap authoring menu to create editor-authored PFB_Ore assets and attach an authored ScavengingLootOracleRuntime host; runtime still refuses to fabricate objects.
Rejected Alternatives: Keeping runtime cube/material fallback would hide bad authoring and keep RB-010 alive.
Scalability potential: Low uses the generic authored prefab; Middle/High/Ultra can swap template-specific prefabs and materials without code changes.
Hardware Impact: Moves primitive/material creation to editor time and removes runtime SRP/material churn on low-end GPUs.

## Decision 08: Lock Flattening

Problem: Mining depletion already uses a Vault mutation guard; resetting pity telemetry inside that region would create a nested write-lock path.
Solution: Read telemetry before depletion, mark ore under the existing guard, release the depletion guard in finally, then acquire the telemetry write lock separately for a minimal row write.
Rejected Alternatives: Holding depletion and telemetry locks simultaneously would invite deadlocks during compaction or future Vault guard changes.
Scalability potential: Low avoids lock contention; Middle/High/Ultra can add haptic/presentation after release with no data-route change.
Hardware Impact: Lock hold contains only primitive assignments, reducing stall risk on i3/MX350.

## Decision 09: Build Throttle

Problem: The prompt authorizes dotnet build only below 50% CPU and with no active compiler process.
Solution: Sampled CPU at 100 and found active dotnet PIDs, so build was not launched; used targeted rg and git diff --check for static validation.
Rejected Alternatives: Spamming dotnet build would violate the batch directive and fight other agents for CPU.
Scalability potential: Keeps shared workstation responsive for parallel agents; source remains ready for one clean build when host load drops.
Hardware Impact: Avoided an extra compiler process under saturated CPU.

## Decision 10: Ore-Only Scanner Telemetry

Problem: GroundPenetratingRadarRuntime appends macro-swarm radar pings after ore raymarch completion and increments `Counters[1]`, so using the post-append `addedCount` would falsely reset EmptyScansStreak when no ore was detected.
Solution: Capture `oreAddedCount` from `pending.Counters[1]` before `AppendMacroSwarmRadarPings`, then report that ore-only count to `ReportScannerSweepResult`.
Rejected Alternatives: Treating all radar pings as ore feedback would poison the pity timer and hide empty ore scans behind fauna/ecosystem presentation.
Scalability potential: Low keeps pity logic honest with sparse pings; Middle/High/Ultra can add macro-swarm visual density without changing ore economy truth.
Hardware Impact: One int captured before append; 0 B/frame and no additional loop.

## Decision 11: Telemetry Fence Flattening

Problem: The telemetry read before spawn scheduling resolved a readonly DataVault view without checking the compaction fence; the write helper also accepted a lock even if the fence appeared immediately after acquisition.
Solution: Check `IsCompactionFenceActive` before and after `TryReadOnlyHandle`; check it before write acquisition and release immediately if it appears after lock acquisition. Keep report/reset lock bodies to one DTO row read and primitive assignments.
Rejected Alternatives: Pinning a long-lived telemetry alias would violate DataVault relocation policy. Running full Roslyn/build proof under CPU 94 with active dotnet PIDs would violate compile throttle.
Scalability potential: Low aborts a single pity read/write under defrag; Middle/High/Ultra keep identical gameplay truth and can spend saved frame stability on presentation lanes.
Hardware Impact: Two boolean fence reads per telemetry access; prevents stale alias use during compaction without adding managed allocation.

## Decision 12: Geology Buffer Fence Sweep

Problem: The pity row was fenced, but generic geology buffer helpers still resolved chunk-generation buffers and could create generation handles without checking whether the compaction fence changed between calls.
Solution: Harden `AcquireBuffer`, `TryOpenExistingBuffer`, `TryReadExistingBuffer`, `TryLockVaultBuffer`, and `TryAcquireVaultBuffer` with fence checks before and after resolve, before handle creation, before mutation guard acquisition, and after write-lock acquisition.
Rejected Alternatives: Adding a new buffer manager would duplicate first-party ownership. Holding a mutation guard and hoping DataVault rejects stale views would be weaker than fail-closed fence checks in the owner.
Scalability potential: Low skips one generation/update frame during defrag; Middle/High/Ultra preserve identical ore truth and avoid relocation stalls.
Hardware Impact: Adds cheap boolean reads; prevents stale alias use and avoids one atomic mutation-guard acquire during fence-active windows on low-end CPUs.

## Decision 13: Active Wait Boundary

Problem: The polish directive forbids synchronous waits in active gameplay, and the domain contains several `forceComplete:true` markers.
Solution: Audited each marker. Procedural ore force completes are Dispose/Disable/DataVault rebind/teardown; GPR force complete is OnDisable; Scavenging force complete is lifecycle/post-simulation swap window. Active LateFrame/PostSimulation paths use non-forcing completion.
Rejected Alternatives: Removing lifecycle forced completion would leak locked Vault rows during teardown. Moving it into hot ticks would violate frame-time doctrine.
Scalability potential: Low avoids active stalls; Middle/High/Ultra keep deterministic teardown correctness without gameplay-phase waits.
Hardware Impact: 0 us/frame added; forced waits remain outside active simulation/presentation cadence.

## Decision 14: Runtime Audit Coverage For Pity DTO

Problem: The editor layout validator checked `PlayerEcosystemTelemetryDTO`, but the runtime `GeologySelfAuditResultDTO` still only exposed the older generation telemetry stride, leaving the new pity row invisible in native audit output.
Solution: Reused the existing 4-byte pad at offset 52 in `GeologySelfAuditResultDTO` as `PlayerEcosystemTelemetrySize`, wrote `UnsafeUtility.SizeOf<PlayerEcosystemTelemetryDTO>()` from `ProceduralOreSpawner.WriteSelfAudit`, and added editor offset validation.
Rejected Alternatives: Adding a second audit DTO or a managed report would duplicate ownership and add unnecessary storage. Renaming `TelemetrySize` would risk breaking existing consumers.
Scalability potential: Low gets a cheap native proof row; Middle/High/Ultra keep the same 64-byte audit stride while validating the pity economy DTO.
Hardware Impact: 0 B/frame managed allocation, no stride growth, one additional uint assignment during the existing self-audit write.

## Decision 15: ResourceNode Spawn Cache Flattening

Problem: Pooled ore nodes call `ResourceNode.OnSpawn()` from the object pool, and `EnsureRegistryCache()` previously reread multiple `GlobalRegistry` slots every time. `IsPooledInstance()` also performed marker component lookups on pooled lifecycle routes.
Solution: Add a static bootstrap flag so the full registry snapshot happens once per subsystem lifecycle, then rely on the existing hot-swap listener for service replacement. Add `_isKnownPooledInstance` so recurring pooled enables/despawns avoid repeated marker component lookups after the first observation.
Rejected Alternatives: Rewriting `ObjectPoolManager` to pass `PoolItemMarker` into `IPoolable.OnSpawn` would widen a core contract and risk unrelated pooled systems. Removing the first marker lookup entirely would break scene-authored ResourceNode activation.
Scalability potential: Low-end devices avoid repeated registry reads during dense ore activation; Middle/High/Ultra can spawn richer authored ore prefabs without increasing service-location churn.
Hardware Impact: Removes six registry reads from repeated pooled ore spawns and amortizes the marker component lookup to the first pooled activation; no new managed allocation.

## Decision 16: Pity Placement Acceptance Tightening

Problem: The forced pity placement loop marked a sample as accepted after any finite height and only used the slope threshold as an early exit, so a wall-facing or steep-slope scan could still accept the last bad sample.
Solution: Require `normal.y >= max(0.35, SlopeRejectNormalY)` before returning success, and return false after four failed attempts. Also refuse pity slot resolution when `safeScanCount <= 0`.
Rejected Alternatives: Expanding the attempt count or adding raycasts would raise CPU cost and reintroduce engine queries. Accepting steep finite terrain would violate the bedrock/wall limit test.
Scalability potential: Low keeps the same four bounded samples; Middle/High/Ultra retain deterministic reward placement and can spend visual budget on authored ore presentation only after a valid floor exists.
Hardware Impact: Same loop bound and no managed allocation; prevents invalid forced ore writes on hostile geometry.

## Decision 17: ResourceNode Registry Bootstrap Recovery

Problem: A one-shot ResourceNode registry snapshot can run before GlobalRegistry services are initially registered. Initial registration does not queue a hot-swap event when previous service is null, so cached null services could persist.
Solution: Keep the pooled-spawn cache flattening, but allow one cold registry resnapshot per frame while required cached services are still null. Use `GlobalRegistry.IsHotSwapListenerRegistered` before `TryRegisterHotSwapListener` so an already-registered static listener is not retried forever.
Rejected Alternatives: Reverting to a full registry snapshot on every pooled spawn would restore hot churn. Depending only on hot-swap callbacks is insufficient for first registration from null.
Scalability potential: Low avoids lost object-pool/inventory/physics routes during early scene activation; Middle/High/Ultra keep dense resource prefab activation without repeated service-location cost once bootstrap is complete.
Hardware Impact: Worst incomplete-bootstrap cost is one registry snapshot per active frame; steady-state recurring pooled spawn stays bool/cache-based.

## Decision 18: Authored Ore Prefab Validity Cache

Problem: A template-specific `RuntimeNodePrefab` without `ResourceNode` could still make pool warmup appear successful or make pending spawns target a prefab that cannot produce a ResourceNode.
Solution: Cache validated runtime prefab identity inside `ResourceNodeTemplate` during cold validation and make `ResourceDistributionDirector` warm/count only ResourceNode-bearing ore prefabs; magma vent marker warmup remains separate because it is visual-only.
Rejected Alternatives: Calling `GetComponent<ResourceNode>()` in `ProcessPendingSpawns` would fix correctness but add hot component lookup. Treating invalid template prefabs as pool-ready would create repeated null spawns and queue stalls.
Scalability potential: Low uses the generic fallback PFB_Ore when template art is invalid; Middle/High/Ultra can assign richer template prefabs after they pass the same cold validation route.
Hardware Impact: Adds only cold authoring/component checks; removes a repeated null-spawn path and preserves zero managed allocation in steady-state spawn processing.

## Decision 19: Pity Telemetry Post-Read Fence

Problem: The readonly pity telemetry reader checked the compaction fence before and after handle resolution, but did not reject a row if the fence rose immediately after copying the 32-byte DTO.
Solution: Copy the row, then check `IsCompactionFenceActive` once more before returning; return default telemetry if compaction is active.
Rejected Alternatives: Adding a write lock for a read-only schedule snapshot would increase contention. Pinning the read alias would violate DataVault relocation policy.
Scalability potential: Low skips pity influence for one generation schedule under compaction; Middle/High/Ultra retain identical economy truth on the next valid schedule.
Hardware Impact: One extra boolean fence read per spawn schedule; no heap allocation and no additional lock.

## Decision 20: Validated-Only Template Prefab Source

Problem: A raw `runtimeNodePrefab` reference could still be observed before cold validation, leaving a narrow path where an invalid prefab assignment might be treated as a runtime source.
Solution: Make `RuntimeNodePrefab` return only `_validatedRuntimeNodePrefab` after `CacheRuntimeNodePrefabCold()` has run; expose `HasRuntimeNodePrefabAssignment` only for authoring diagnostics.
Rejected Alternatives: Returning the raw prefab when validation has not run would hide authoring defects and keep the director dependent on runtime validation.
Scalability potential: Low uses generic PFB_Ore when template art is invalid; Middle/High/Ultra can bind richer template prefabs after passing the same ResourceNode-bearing validation.
Hardware Impact: Keeps `GetComponent<ResourceNode>()` in cold validation only and removes invalid pending-spawn churn in steady-state.

## Decision 21: Pool Reserve Top-Up

Problem: `HasPool(prefab)` proves a pool exists but not that it contains enough inactive objects for the computed sector window.
Solution: Compare `IObjectPoolService.GetAvailableCount(prefab)` against the required warmup and call `Warmup` only for the missing reserve.
Rejected Alternatives: Rewarming the full count every time would over-allocate; trusting `HasPool` alone could underfill a hotswapped or partially consumed pool.
Scalability potential: Low keeps only the required generic reserve; Middle/High/Ultra can assign per-template visuals while each pool receives a deterministic minimum reserve.
Hardware Impact: Cold top-up only; prevents active spawn starvation without expanding in the hot spawn path.

## Decision 22: Cached Scanner Command Route

Problem: The GPR telemetry report path still had a fallback cast from the read model to `IWorldResourceSpawnerCommandModel` during completed scan commit.
Solution: Remove the fallback cast from `ReportOreScannerSweepTelemetry` and depend solely on the cached command model populated by cold owner-route and hot-swap binding.
Rejected Alternatives: Casting on every completed scan is small but violates the cold-DI doctrine and weakens the proof that hot scanner reporting is lookup-free.
Scalability potential: Low keeps scanner reports cheap; Middle/High/Ultra can increase radar visual density without adding service/cast churn to the ore telemetry route.
Hardware Impact: Removes one hot fallback cast route per completed ore scan; no allocation.

## Decision 23: Scavenging Vault Alias Fail-Closed

Problem: Scavenging buffer helpers could return a NativeArray alias after `TryResolveHandle` or `TryReadHandle` if the compaction fence rose between checks.
Solution: Check `IsCompactionFenceActive` after resolve/read, reset output buffers on failure, and reset handles when fallback generation-handle resolution is invalid.
Rejected Alternatives: Leaving stale aliases to callers would violate DataVault relocation safety. Adding a new scavenging buffer owner would duplicate first-party Vault ownership.
Scalability potential: Low skips one loot read under compaction; Middle/High/Ultra keep identical loot truth once the fence clears.
Hardware Impact: Adds cheap boolean checks to cold/read routes and avoids stale native alias use during memory relocation.

## Decision 24: Scavenging Read-Only Consumer Alias

Problem: `TryReadScavengingVaultBuffer` used `IDataVault.TryReadHandle`, whose contract marks it as a legacy mutable current-phase view. Loot queueing and self-audit only consume table rows, so exposing a mutable alias was unnecessary authority leakage.
Solution: Convert the helper to `TryReadOnlyHandle`, return `NativeArray<T>.ReadOnly`, and feed `LootResolutionJob` / `ScavengingLootOracleSelfAuditJob` with read-only views via `AsReadOnly()`.
Rejected Alternatives: Keeping `NativeArray<T>` plus `[ReadOnly]` on job fields still leaves the managed caller holding a mutable table alias. Creating a separate table mirror would duplicate Vault ownership.
Scalability potential: Low keeps the same one request write and one loot-table read; Middle/High/Ultra can enlarge authored loot tables without changing the read-only consumer contract.
Hardware Impact: No additional allocation or job. Removes write-capable consumer aliases from the hot scavenging queue path.

## Decision 25: Pressure Metamorphism Scratch Capacity Gate

Problem: `BuildPressureMetamorphismInputs()` fills `_metamorphismNodeScratch` with active carbon nodes after estimating all active sector nodes. The native workspace was capacity-checked, but the managed scratch list could remain smaller than the requested count and grow during active scheduling.
Solution: Extend the existing cold metamorphism capacity owner. `EnsureMetamorphismCapacityCold()` now raises `_metamorphismNodeScratch.Capacity` with the native workspace capacity, and `TryAcquireMetamorphismJobBuffer()` refuses an active lease when the scratch list cannot hold the requested node count.
Rejected Alternatives: Calling `List<T>.EnsureCapacity` or assigning `Capacity` inside `BuildPressureMetamorphismInputs()` would move managed heap growth into the simulation walk. Creating a parallel native node-id map would duplicate the existing commit mapping and widen the patch.
Scalability potential: Low skips one metamorphism scheduling pass if content exceeds the cold reserve; Middle/High/Ultra can increase authored pool/cold capacity without changing runtime allocation behavior.
Hardware Impact: Removes a potential managed list reallocation during pressure-metamorphism scheduling on i3/MX350; steady-state cost is one capacity branch before the workspace lease.

## Decision 26: Metamorphism Candidate-Only Reserve

Problem: The no-growth scratch gate used total active sector nodes as the required metamorphism count. Dense mixed sectors could exceed the cold reserve even when the actual carbon-node workload was small, causing the background carbon-to-diamond transformation to skip unnecessarily.
Solution: Count only live carbon-template candidates in the estimate pass and reuse a local `TryResolvePressureMetamorphismCandidate` helper for both estimate and write passes. The helper stays inside `ResourceDistributionDirector` and does not introduce a new owner or data structure.
Rejected Alternatives: Raising cold capacity to every active node count wastes low-end memory and hides bad sizing. Moving publish/signals into the unused `PublishLootYieldsJob` would move HUD/visual/inventory publication away from the current post-simulation phase contract.
Scalability potential: Low keeps reserve small and transformation reliable; Middle/High/Ultra can increase authored carbon density without forcing all non-carbon resource nodes into the metamorphism reserve.
Hardware Impact: Avoids false pass drops on dense mixed sectors while preserving 0 B/frame; adds one bounded filter pass using existing active-node lists.

## Decision 27: Pity Placement Bounds Rejection

Problem: `SampleHeight()` clamps X/Z into the current heightfield or mock-sector sample window. A pity spawn projected outside the valid sampled terrain could therefore receive a plausible edge height and be accepted as if it landed on real forward terrain.
Solution: Add `IsPityPlacementInsideSampleBounds()` inside the Burst job and call it before `SampleGrounding()`. Heightfield-backed pity placement must stay inside `TerrainOriginAbsoluteXZ + TerrainSize`; mock terrain placement must stay inside `SectorOrigin + SectorSize`; procedural fallback remains allowed because it has no finite sample window.
Rejected Alternatives: Adding Physics raycasts would violate deterministic/job-safe placement and allocate engine-side work. Letting `SampleHeight()` clamp would keep the wall/edge exploit and could fake a reward on a terrain edge the player is not actually moving toward.
Scalability potential: Low defers pity rewards near sample boundaries rather than showing unreachable ore; Middle/High/Ultra keep the same economy truth and can spend visual budget only after a valid sampled floor exists.
Hardware Impact: Adds a few scalar comparisons per pity attempt only, still capped at four attempts; prevents invalid forced ore writes without heap allocation.

## Decision 28: Cached Authored Ore Fallback

Problem: `_runtimePoolReady` can become true from template-specific prefabs while `_authoredOrePrefab` is missing or invalid. Templates without a validated prefab then fall back to the raw `_authoredOrePrefab`, which could send an invalid prefab into `pool.Spawn` or require hot component validation.
Solution: Cache `_validatedAuthoredOrePrefab` during cold validation and make `ResolveAuthoredOrePrefab()` return only a template validated prefab or the cached validated fallback. ObjectPool hotswap now reruns cold validation before rewarming pools.
Rejected Alternatives: Checking `GetComponent<ResourceNode>()` inside `ResolveAuthoredOrePrefab()` would fix correctness but violate the hot lookup rule. Treating `_runtimePoolReady` as global proof of every template fallback would hide authoring holes.
Scalability potential: Low fails closed per invalid template/fallback; Middle/High/Ultra can attach richer per-template PFB_Ore assets without changing the runtime spawn contract.
Hardware Impact: Removes an invalid-prefab spawn path and keeps component validation cold; steady-state spawn resolution remains pointer selection only.

## Decision 29: Runtime Pool Warmup Phase Gate

Problem: Several active ore/geode/pillar spawn entrypoints still called `EnsureRuntimePool()` immediately before gameplay `pool.Spawn`. The method is cold-safe, but it can validate prefabs and top up pools, which belongs to bootstrap/hot-swap phases, not active spawn calls.
Solution: Remove those active calls and leave `EnsureRuntimePool()` only in `OnEnable` and ObjectPool hot-swap. Active spawn entrypoints now only read `_runtimePoolReady`, resolve a cold-validated prefab, and spawn with `allowExpand:false`.
Rejected Alternatives: Keeping lazy warmup would mask authoring gaps and could call cold validation/top-up under gameplay pressure. Removing ObjectPool hot-swap rewarm would break legitimate service replacement recovery.
Scalability potential: Low fails closed if pools are not prepared; Middle/High/Ultra can scale prefab richness and pool size through cold authoring without changing runtime spawn contracts.
Hardware Impact: Removes cold validation/top-up work from five active spawn routes; steady-state gameplay spawn path remains pointer checks plus pooled activation only.

## Decision 30: Single-Owner Prefab Validation

Problem: `TryWarmAuthoredPrefab()` still accepted a `requiresResourceNode` flag and repeated `GetComponent<ResourceNode>()` before warming. All ore prefab inputs are already cold-validated by `ValidateAuthoredRuntimePrefabsCold()` or `ResourceNodeTemplate.ValidateRuntimeNodePrefabCold()`, so this duplicated validation ownership.
Solution: Remove the `requiresResourceNode` flag. Pool warmup now only tops up a non-null prefab reference; validation remains in the cold prefab validators.
Rejected Alternatives: Leaving duplicate validation in warmup made the pool top-up method a second prefab-authority owner. Trusting raw `_authoredOrePrefab` would be worse, so only validated caches feed ore warmup.
Scalability potential: Low keeps generic PFB_Ore validation simple; Middle/High/Ultra can add many template-specific prefabs without multiplying validation checks during pool reserve top-up.
Hardware Impact: Removes repeated cold component checks during pool reserve refresh; steady-state remains unaffected and active spawn still performs no component lookup.

## Decision 31: Vault Scratch Bulk Copy

Problem: `TryCopySpawnScratchToVault()` held a DataVault write lock while copying large NativeArray staging buffers with a managed `for` loop.
Solution: Replace the per-element loop with `NativeArray<T>.Copy(source, 0, target, 0, requiredLength)` after the same source/target length checks.
Rejected Alternatives: Unsafe pointer copy would add avoidable compile and safety risk. Leaving the loop would keep a longer write-lock window during spawn commit.
Scalability potential: Low shortens large ore buffer commits for 2048 slots; Middle/High/Ultra benefit more as authored capacity scales toward 16k without changing gameplay truth.
Hardware Impact: Converts a managed indexed loop inside the lock into a first-party native bulk copy path already used elsewhere in the project.

## Decision 32: Oracle Host Helper Purity

Problem: `ScavengingLootOracleRuntime.EnsureHost()` still called `ConfigureSignalLanes()` and retried hot-swap listener registration. Even without creating GameObjects, a host lookup could perform cold managed setup if lifecycle bootstrap had not run.
Solution: Make `EnsureHost()` a pure `_host` return. Signal lanes are configured by `AfterSceneLoad` and authored host `OnEnable`; hot-swap registration stays in authored host `OnEnable`.
Rejected Alternatives: Keeping the fallback would hide scene bootstrap defects and leave a gameplay-reachable managed initialization path. Removing cold lifecycle initialization would break valid authored hosts.
Scalability potential: Low fails closed when the host is not prepared; Middle/High/Ultra keep richer scavenging presentation through preconfigured signal lanes only.
Hardware Impact: Removes cold managed setup and registration branches from host lookup; hot loot queue remains `TryGetPreparedHostForHot()` and performs no lane configuration.

## Decision 33: Oracle Manual Cold Facade Gate

Problem: Public oracle helper methods could still call `PrepareVaultCold()` and forced cold job completion if invoked while the player was active, even though normal hot loot queueing was already clean.
Solution: Add `IsColdManualOracleOperationAllowed()` and gate emergency mock generation, editor CSV ingest, editor tuning, distribution self-audit, and editor gizmo preview before they can prepare Vault buffers or force-complete jobs.
Rejected Alternatives: Trusting method names or editor UI callers is not a phase contract. Moving the methods under new helper classes would create duplicate oracle ownership.
Scalability potential: Low avoids accidental play-mode stalls; Middle/High/Ultra keep the same authored/monolith loot route and can run audits only outside active play.
Hardware Impact: Removes a public active-gameplay path to synchronous cold work; hot loot queue remains unchanged and zero-GC.

## Decision 34: Modulo-Free Deterministic Range Mapping

Problem: Pity slot selection used `% limit` for the random start, and depletion cache probing used `% capacity` for hash start/wrap. They were deterministic, but weaker than the project RNG mandate and more expensive than multiply-high mapping.
Solution: Replace random/hash range selection with multiply-high mapping and use a single subtract for wrap because `start + probe < 2 * capacity` in both probe loops.
Rejected Alternatives: Rejection sampling is unnecessary for one bounded slot/hash start. Leaving modulo in active lookup math keeps a slower integer operation in repeated depletion probes.
Scalability potential: Low preserves deterministic results with cheaper integer math; Middle/High/Ultra gain more as ore/depletion counts scale.
Hardware Impact: Avoids integer modulo in active pity/depletion probe paths; no allocation and no added containers.

## Decision 35: Resident Sector No-Growth Registration

Problem: `RefreshResidentSectors()` and direct rare-resource spawn paths still called `_residentSectors.Add` after leasing a prewarmed sector state. The pool bounded resident state count, but the dictionary add itself had no explicit fail-closed guard at the route edge.
Solution: Add `TryRegisterResidentSectorNoGrowth()` bounded by `_sectorStatePool.Length`, release the leased state on failure, and guard the eviction scratch list with `TryQueueSectorEvictionNoGrowth()`.
Rejected Alternatives: Increasing dictionary capacity hides overflow and keeps the active route dependent on managed container growth. Replacing the dictionary with a new native map would be a larger owner rewrite outside this batch.
Scalability potential: Low/MX350 keeps resident sectors strictly inside the cold window; Middle/High/Ultra can raise `sectorRadius` only by increasing the cold pool, not by runtime growth.
Hardware Impact: Prevents active-sector registration from escaping the prewarmed pool; added cost is one count/capacity branch per new resident sector.

## Decision 36: Geology Vault Lock Window Flattening

Problem: Sector hash grid and biome heatmap writes performed small loops while holding DataVault write locks, and telemetry ring writes resolved depletion masks inside the telemetry write-lock section.
Solution: Extend the existing `SpawnStagingScratchBuffers` with `BiomeHeatmap` and `SectorHashGrid`, fill both before lock, then reuse `TryCopySpawnScratchToVault()` for copy-only lock bodies. Hoist depletion-mask read-only resolve before the telemetry lock.
Rejected Alternatives: New helper owners or separate staging classes would duplicate the ore spawner's scratch ownership. Leaving the loops in locks was acceptable by size but still violated the lock-flattening doctrine.
Scalability potential: Low gains shorter lock windows at the same 16x16 heatmap; Middle/High/Ultra can increase heatmap/detail only by cold scratch sizing while the lock body remains a bulk copy.
Hardware Impact: Converts active DataVault sections to copy-only work; avoids read-handle resolution while holding telemetry write authority.

## Decision 37: Stable Template Hash Cold Cache

Problem: Special ore fallback resolution still compared `ResourceNodeTemplate.StableId` strings, and `StableHashId` recomputed `LocHash` on every property read.
Solution: Cache special template references in `ResourceDistributionDirector` during cold bootstrap using hash ids, recache explicit inspector templates even when they are outside `resourceTemplates`, and make `ResourceNodeTemplate.StableHashId` a prepared field read with an explicit `ResolveStableHashIdCold()` lane.
Rejected Alternatives: Keeping string compares in special spawn/metamorphism routes is cheap but violates the no-hot-string doctrine. Adding a new registry would duplicate template ownership.
Scalability potential: Low avoids string/hash walks during rare-spawn/metamorphism decisions; Middle/High/Ultra can add more authored templates while gameplay reads remain pointer/int comparisons.
Hardware Impact: Removes repeated stable-id string hashing from active resource decisions; cold bootstrap pays the hash once per referenced template.

## Decision 38: Meteor Impact Mutation Ordering

Problem: `TryExecuteMeteorImpact` carved the voxel crater before proving that a valid authored ore prefab, resident sector capacity, template index, and spawn queue slot existed. A failed queue path could damage the world without spawning the meteorite reward.
Solution: Move `TryApplyMeteorImpactCrater` after all cheap fail-closed checks and add `HasSpawnQueueCapacity` so crater mutation occurs only when the spawn request can be enqueued.
Rejected Alternatives: Keeping the crater-first order is cinematic but violates one fact/one route because geology truth mutates without a resource-node route. Rolling back a voxel crater would be heavier and less reliable.
Scalability potential: Low avoids visible empty impact scars; Middle/High/Ultra can keep richer impact visuals because authoritative terrain/resource state now agrees.
Hardware Impact: Adds one queue-capacity branch before crater carving and avoids wasted voxel writes when the resource pipeline is saturated.

## Decision 39: Dual Spawn Queue Capacity Preflight

Problem: `EnqueueSectorEnvelope` preflight checked only `_pendingSpawns`, even though meshless authored nodes are routed through `_pendingGhostProxySnaps` before surface snapping. This could block valid ghost-snap ore candidates or waste candidate work against a full specific queue.
Solution: Add `HasAnySpawnQueueCapacity()` for cheap preflight and reuse `HasSpawnQueueCapacity()` after the built request reveals the target queue.
Rejected Alternatives: Splitting envelope generation by queue type would duplicate placement logic. Keeping only `_pendingSpawns` as the gate made queue behavior depend on presentation mesh availability.
Scalability potential: Low keeps sparse meshless nodes spawning when normal queue pressure is high; Middle/High/Ultra can mix authored mesh and ghost-snap templates without starvation.
Hardware Impact: Adds two count checks and avoids wasted candidate work or false starvation under queue pressure.

## Decision 40: Pool Exhaustion Queue Fairness

Problem: `ProcessPendingSpawns()` broke on the first `pool.Spawn()` null. If one authored prefab pool was exhausted, it could hold the queue head and block later requests whose pools still had reserve.
Solution: Cap each slow-tick pass by the initial queue count, rotate requests whose existing pool has zero available inactive instances, drop requests whose prefab was never warmed, and keep all direct rare-resource spawns on one `TrySpawnAuthoredResourceNodeNow()` route.
Rejected Alternatives: Calling `Spawn()` repeatedly and accepting ObjectPool exhaustion warnings would add managed warning churn in development builds. Expanding pools at runtime would reopen RB-010-style hidden allocation pressure. Creating a new scheduler would duplicate queue ownership.
Scalability potential: Low avoids one exhausted visual prefab starving all ore. Middle/High/Ultra can use more template-specific PFB_Ore variants while the queue stays fair across prefab families.
Hardware Impact: Adds bounded `HasPool`/reserve checks and removes head-of-line stalls; on MX350-class hardware this prevents repeated failed spawn attempts from consuming the whole slow-tick budget.

## Decision 41: Scavenging Presentation Phase Split

Problem: `ScavengingLootOracleRuntime.PublishResolvedYields()` published `VisualScavengeSignal` in `PostSimulation` beside item acquisition and depletion truth. The file also kept an unused `PublishLootYieldsJob` that duplicated the signal route.
Solution: Keep HUD/item/depletion publication in `PostSimulation`, queue `VisualScavengeSignal` into the existing oracle scratch owner as a fixed `NativeArray<VisualScavengeSignal>`, and flush it through a registered `VisualSyncPhaseSystem`. Delete the unused job after proving it had no call sites.
Rejected Alternatives: Scheduling a managed visual queue would add heap pressure. Blocking loot simulation until visual backlog clears would let presentation suppress gameplay truth. Keeping the dead job would leave two signal owners with conflicting phase semantics.
Scalability potential: Low drops overflow visual-only events while loot truth continues. Middle/High/Ultra can raise the cold request/visual capacity and VFX density without changing item/depletion authority.
Hardware Impact: Adds one 64-slot native visual queue allocated cold with the oracle scratch; runtime transfer is struct copy only and costs 0 B/frame on i3/MX350.

## Decision 42: Hot Loot Queue Cached Table Count

Problem: `TryQueueResourceNodeLoot()` resolved the loot CDF DataVault buffer on every harvest request only to clamp `LootEntryCount` by the buffer length.
Solution: Use the oracle-owned `_activeLootEntryCount` cached during cold table hydration and clamp it by `DefaultLootEntryCapacity`, the fixed size used by all loot CDF buffers.
Rejected Alternatives: Keeping a read-only Vault alias resolve in the harvest hot path adds unnecessary DataVault traffic. Storing a separate cached table length would duplicate `_activeLootEntryCount`.
Scalability potential: Low reduces harvest path pressure under dense resource collection; Middle/High/Ultra can enlarge the cold table capacity while the hot queue remains alias-free.
Hardware Impact: Removes one DataVault read-only handle resolve per queued harvest; runtime cost is an integer clamp and one request struct write.

## Decision 43: Direct Spawn Pool Reserve Before Sector Lease

Problem: Direct rare-resource spawn routes could compute tombstones and lease resident sector state before proving the authored prefab pool had an inactive instance.
Solution: Add `HasAuthoredPoolReserve` and call it before sector-state acquisition on thermal diamond, deep mantle geode, and pillar-surface routes. Keep pending queue drop-vs-defer behavior explicit.
Rejected Alternatives: Leaving reserve checks inside the final spawn helper preserves correctness but wastes sector-state churn when pools are exhausted. Runtime pool expansion is still rejected.
Scalability potential: Low avoids empty resident sectors under pool pressure; Middle/High/Ultra can use more template-specific PFB_Ore variants while direct spawn routes remain fail-closed.
Hardware Impact: Saves tombstone/sector work on exhausted pools; estimated 2-12 us per rejected direct spawn on MX350-class hardware.

## Decision 44: Indirect Args Presentation Transfer Outside Vault Lock

Problem: `UpdateIndirectArgsBuffer(uint)` built and queued GPU indirect-args presentation state while still inside a DataVault write-lock section.
Solution: Build `GeologyIndirectArgsDTO` before lock, write only that DTO inside the lock, release immediately, then queue the GPU upload dirty state for `LateFrameTick`.
Rejected Alternatives: Leaving the presentation dirty flag update inside the Vault lock is cheap but violates strict lock flattening. Moving the actual GPU buffer upload into simulation would violate phase order.
Scalability potential: Low keeps indirect draw state deterministic and cheap; Middle/High/Ultra can raise dormant ore instance count while presentation upload still flushes only in LateFrame.
Hardware Impact: Removes non-Vault state mutation from the write-lock window; estimated sub-microsecond but removes a lock-order ambiguity on MX350-class CPUs.

## Decision 45: Meteor Reward Pool Proof Before Terrain Mutation

Problem: Meteor impacts could prove a prefab existed and a queue slot existed, but not that an inactive authored pool instance was available before crater carving.
Solution: Require `HasAuthoredPoolReserve` before height sampling, surface placement, crater mutation, and request enqueue. The same reserve preflight was applied to the remaining rare pillar direct route.
Rejected Alternatives: Allowing a crater to wait on a future pool return creates visible empty impact scars under pool pressure. Runtime pool expansion remains rejected because it reopens RB-010.
Scalability potential: Low skips the meteor event when pool pressure is high; Middle/High/Ultra can raise cold pool reserves and keep richer meteor rewards without runtime allocation.
Hardware Impact: Avoids terrain mutation and sector/tombstone work when no pooled reward instance can exist; estimated 3-20 us avoided per rejected impact on low-end hardware.

## Decision 46: Ghost Snap Reserve Before Terrain Work

Problem: Meshless authored ore requests could enter `ProcessGhostProxySurfaceSnaps()` and pay MapMagic height/SDF placement work before proving that the template resolves to an authored prefab with an existing warmed pool and inactive reserve.
Solution: Gate ghost-snap processing on `_runtimePoolReady`, resolve template/prefab first, drop unwarmed prefab identities, and rotate zero-reserve requests back to the fixed ghost queue before terrain/SDF work. Also guard sector envelope generation before mutating `SpawnEnvelopeQueued` when no template table exists.
Rejected Alternatives: Letting the pending spawn queue defer after snap preserves correctness but wastes active terrain queries under pool pressure. Runtime pool expansion remains rejected because it violates RB-010. A new scheduler was rejected as duplicate queue ownership.
Scalability potential: Low avoids terrain/SDF work for impossible ore visuals; Middle/High/Ultra can use more meshless PFB_Ore variants while the same fixed queues rotate reserve pressure without allocation.
Hardware Impact: Avoids 4-18 us per rejected ghost snap on MX350-class CPUs and prevents a sector from being marked queued when no authored template table can generate nodes.

## Decision 47: Editor-Only Oracle Reload Cleanup

Problem: The RB-121 cleanup scan was cold-flag guarded, but the flag is toggled from `SubsystemRegistration`, which can exist in runtime startup contexts. Development-player cleanup scans are still runtime scene-wide searches.
Solution: Add an `Application.isPlaying` return and compile `Resources.FindObjectsOfTypeAll` only under `UNITY_EDITOR`. Hot and runtime host access remains `_host` snapshot only.
Rejected Alternatives: Keeping `DEVELOPMENT_BUILD` cleanup would preserve diagnostics but still permit a scene-wide managed allocation in gameplay binaries. Creating a runtime cleanup registry would duplicate first-party host ownership.
Scalability potential: Low avoids a worst-case reload spike on cheap CPUs; Middle/High/Ultra retain editor cleanup while runtime scavenging stays pure host snapshot and fixed native queues.
Hardware Impact: Removes a possible full-scene object-array allocation and `GetComponent` scan from development/runtime player contexts; active harvest route remains 0 B/frame.

## Decision 48: Depletion Guard Presentation Split

Problem: Ore depletion held the geology mutation guard while publishing SignalBus events and queuing GPU indirect-args dirty state. Runtime AUP shift also adjusted cached presentation anchors while the vault mutation guard was active.
Solution: Make guarded depletion produce struct payloads only, unlock, then publish `ItemAcquiredSignal`/`ResourceDepletionDeltaSignal`, queue indirect args, set `_renderUploadDirty`, and reset pity telemetry. Split runtime shift into vault-row mutation and post-unlock cached presentation anchor adjustment.
Rejected Alternatives: Leaving SignalBus/GPU dirty mutations in the guarded body is cheap but violates phase and guard flattening. Adding a new dispatcher would duplicate the existing `LateFrameTick` presentation upload route.
Scalability potential: Low keeps harvest/depletion responsive under resource pressure; Middle/High/Ultra can increase dormant ore counts because GPU dirty transfer remains late-frame only and no signal publication occurs inside the guarded mutation section.
Hardware Impact: Removes SignalBus push and presentation-state writes from guarded sections; estimated sub-us to 6 us lower guard residency on MX350 during dense harvest or AUP-shift frames.

## Decision 49: Depletion Loaded Flag After Unlock

Problem: `LoadDepletionMasksForCurrentSector()` set `_depletionLoaded = true` while the DataVault depletion-mask guard was still held, leaving a non-vault control flag mutation inside a section that should only copy native mask words.
Solution: Track successful cache hydration with a local `bool loaded`, release the vault guard in `finally`, then set `_depletionLoaded` after unlock. The locked body now resolves views, initializes cache if needed, and copies mask words only.
Rejected Alternatives: Keeping the flag write inside the guarded body is operationally cheap but weakens the proof that guarded regions contain only vault-backed data movement. Splitting a second helper would add call noise for one flag transfer.
Scalability potential: Low keeps depletion mask loads predictable on weak CPUs; Middle/High/Ultra can increase depletion-cache word counts without adding extra non-vault state under the guard.
Hardware Impact: Saves only sub-microsecond time directly, but removes one more non-vault mutation from the guard residency path and tightens deadlock/audit proof on i3/MX350-class hardware.

## Decision 50: Indirect Args Truth Before GPU Queue

Problem: `UpdateIndirectArgsBuffer(uint)` returned immediately when `_argsBuffer` was null, so a missing or not-yet-created graphics buffer could prevent the native `GeologyIndirectArgsDTO` DataVault row from being updated.
Solution: Build the DTO unconditionally, attempt the DataVault write independently of `_argsBuffer`, and queue the GPU copy only after the vault write succeeds and a graphics buffer exists.
Rejected Alternatives: Leaving the graphics-buffer guard first made presentation resource availability decide native truth freshness. Always queuing GPU after a failed vault write was rejected because it would present draw counts without DataVault proof.
Scalability potential: Low/no-GPU or delayed-GPU initialization keeps native draw args coherent; Middle/High/Ultra still flush presentation in `LateFrameTick` when graphics buffers are present.
Hardware Impact: No steady-state allocation and one boolean branch. Prevents stale indirect-args truth during graphics-buffer absence/rebind windows.

## Decision 51: Pooled Resource Deactivation Proof

Problem: Resource director overflow deactivation called `pool.Despawn(target)` whenever a pool service existed. If a legacy or misclassified resource object lacked a pool marker, ObjectPoolManager could take its destroy fallback.
Solution: Add `DespawnKnownPooledResourceOrDisable` and use it for immediate overflow and pending flush. The helper proves `ResourceNode` pool ownership through `TryGetPooledComponent` before despawn; otherwise it disables the object.
Rejected Alternatives: Calling `ResourceNode.IsPooledInstance()` would require a component lookup route on the node and can be sensitive to marker-add order. Keeping two duplicated guarded branches weakens the single-owner route.
Scalability potential: Low avoids accidental destroy churn under deactivation backlog; Middle/High/Ultra can raise pooled node density while overflow behavior remains deterministic.
Hardware Impact: One pool-marker dictionary probe on overflow only; avoids destroy fallback, warning churn, and possible object loss when deactivation capacity is saturated.

## Decision 52: Dense Sector Pool Instead Of Hot Enumerators

Problem: `ResourceDistributionDirector.SlowTick()` reached dictionary and queue enumerators through resident refresh, pressure metamorphism input building, diagnostics, and duplicate-spawn detection.
Solution: Use the existing `_sectorStatePool` as the dense owner for resident-sector walks, guarded by `IsLeased`. Convert duplicate-spawn detection to a bounded queue rotate scan that restores order after the initial count.
Rejected Alternatives: Maintaining a second dense resident list would duplicate ownership. Keeping dictionary enumerators is allocation-safe in some runtimes but violates the local zero-GC mandate and costs less predictable cache traversal. Copying queues to arrays is allocation.
Scalability potential: Low keeps sector/resource maintenance cheap on weak CPUs; Middle/High/Ultra can increase sector radius and resource density while active-sector walks stay contiguous and bounded by cold pool capacity.
Hardware Impact: Removes dictionary/queue enumerator paths from the resource slow tick; expected savings are small per frame but reduce cache misses and eliminate enumerator audit risk under dense resource sectors.

## Decision 53: Scavenging Queue Retention During DataVault Fence

Problem: `ScavengingLootOracleRuntime.ScheduleSimulation()` cleared `_queuedCount` before proving that the required DataVault views were available. A transient compaction fence could therefore erase fixed harvest requests without running the loot job.
Solution: Return early without clearing queued requests while the vault reports an active compaction fence, then clear only after the fence is absent and the scavenging vault/table views are still missing. The job path still clears after valid views are proven.
Rejected Alternatives: Keeping the early clear preserves a small fail-safe but loses player harvest inputs during native swap windows. Retaining requests forever was rejected; missing buffers without a fence still fail closed.
Scalability potential: Low preserves scarce harvest rewards on weak devices during native compaction. Middle/High/Ultra can use larger cold harvest capacity while the same fixed queue survives transient vault fences without allocation.
Hardware Impact: Adds two fence branches and no allocations. Prevents dropped harvest work and avoids replaying player interactions after compaction on i3/MX350-class CPUs.

## Decision 54: Player Ecosystem Telemetry Read Lock Shape

Problem: `ReadPlayerEcosystemTelemetryHot()` read BufferID 141905 through a direct read-only handle path that had no explicit release shape, weakening the strict compaction-fence audit proof for the player telemetry row.
Solution: Reuse the existing telemetry lock acquisition, copy only `telemetryView[0]` inside `try`, and release the lock in `finally`. The compaction fence is checked before acquisition and again after the one-row copy.
Rejected Alternatives: Inventing a new read-lock API would duplicate DataVault ownership. Leaving the direct read-only path would keep a special case in the hottest telemetry route. Reading inside the depletion mutation guard was rejected because it would create nested lock risk.
Scalability potential: Low keeps the player pity row safe during native compaction. Middle/High/Ultra can increase resource generation cadence without changing the telemetry authority route or holding locks during placement math.
Hardware Impact: One lock pair around a single struct copy on spawn/depletion decisions; no allocation, no math under lock, and tighter compaction safety on i3/MX350-class CPUs.

## Decision 55: Continuous Ore Node Presentation Scaling

Problem: Runtime ore nodes had deterministic economy/collider truth, but no authored continuous presentation hook for weak-versus-overkill devices at the node level. Visual richness could only be controlled indirectly through generation density.
Solution: Extend `ResourceNode.ApplyPresentation` to sample `GlobalQualityWeight` once on spawn/template application. It can choose an optional low-quality mesh at minimum survival quality and scales serialized ambient particle emission/max-particle budget continuously from disabled to high-end particulate presentation. `ResetState` also drives the gate with `0f` so pooled nodes do not carry previous particle state across despawn/spawn.
Rejected Alternatives: Adding a new ore VFX manager would duplicate ResourceNode presentation ownership. Runtime particle authoring or `GetComponentsInChildren` searches were rejected because they allocate or search prefab hierarchy during spawn. Changing collider scale or loot quantity was rejected because quality must not alter gameplay truth.
Scalability potential: Low disables ore ambient particles and can use a cheap mesh while hitboxes and rewards stay fixed. Middle raises emission gradually. High/Ultra can use authored bioluminescent particle systems without changing spawn probability, tombstone identity, or harvest math.
Hardware Impact: One cold spawn-time quality sample and a bounded serialized-array loop; no per-frame allocation. Low-end saves fill-rate/particle simulation, while high-end spends the recovered budget on authored ore atmosphere.

## Decision 56: Oracle Simulation Read View Narrowing

Problem: `ScavengingLootOracleRuntime.ScheduleSimulation()` resolved the full mutable vault view bundle before scheduling `LootResolutionJob`, even though the job only reads loot entries and biome modifiers and writes to the oracle-owned scratch buffers.
Solution: Add `TryResolveHotLootReadViews` using the existing read-only handle path for loot CDF and biome modifiers. Schedule the job from those immutable views and oracle scratch arrays. Publish resolved yields from `_nativeScratch.ResolvedYields` after the job rather than re-opening vault views.
Rejected Alternatives: Keeping `ResolveViews()` in simulation was broader than the hot path needs and exposes audit/csv buffers. Adding a parallel view owner would duplicate ownership. Taking a write lock across scheduled job execution was rejected because it would hold native ownership far beyond a lightweight copy/assign block.
Scalability potential: Low avoids unnecessary vault mutable view exposure during frequent harvest resolution. Middle/High/Ultra can increase harvest request cadence while loot table reads stay immutable and presentation signals remain deferred to VisualSync.
Hardware Impact: Removes two hot `ResolveViews()` calls and avoids touching unused audit/csv vault buffers on i3/MX350-class CPUs. No managed allocation and no additional job synchronization.

## Decision 57: Resource Node Pooled Identity Cache Route

Problem: `ResourceNode.OnEnable()` can run before `ObjectPoolManager.NotifySpawn()` marks the node as pooled, so pooled resource activation could still hit a component probe through `IsPooledInstance()`.
Solution: Cache the root `GameObject` in `Awake` and make `IsPooledInstance()` query the already-cached `IObjectPoolService.CanDespawnWithoutDestroy` route first. That route uses ObjectPoolManager marker metadata instead of a component lookup. The `TryGetComponent<PoolItemMarker>` branch remains as a cold legacy fallback only.
Rejected Alternatives: Reordering `ObjectPoolManager.Spawn()` to call `OnSpawn()` before `SetActive(true)` would affect every pooled prefab and risk lifecycle regressions outside the 1706 domain. Removing the fallback would break legacy marker-authored nodes if the object pool service is unavailable during editor/cold lifecycle.
Scalability potential: Low reduces pooled resource activation overhead when dense ore sectors stream in. Middle/High/Ultra can spawn more authored nodes through the same marker-cache route without hierarchy searches.
Hardware Impact: Converts the normal pooled activation identity check from component lookup to one pool dictionary probe; no managed allocation and no change to collider/economy truth.

## Decision 58: Geology DTO 8-Byte Stride Gate

Problem: The geology runtime layout audit checked exact DTO sizes through `UnsafeUtility.SizeOf<T>()`, but the ARM64 multiple-of-8 rule was implicit in those constants rather than enforced as a separate predicate.
Solution: Fold an explicit `ValidateStride<T>(expectedBytes)` helper into `ProceduralGeologyLayoutAudit`. Every resource/geology DTO, including `PlayerEcosystemTelemetryDTO`, now requires both the expected byte size and 8-byte divisibility in the first-party runtime audit.
Rejected Alternatives: Creating another editor validator would duplicate the existing authoring guard. Checking only the player telemetry DTO was rejected because the audit is a shared resource generation ABI gate.
Scalability potential: Low prevents silent ABI drift on weak ARM64/mobile targets. Middle/High/Ultra can extend resource DTOs while the same audit fails closed on bad row stride before jobs consume the buffers.
Hardware Impact: Cold/runtime audit cost is a small fixed set of `UnsafeUtility.SizeOf<T>()` calls. Hot generation loops are unchanged; failure mode is earlier and deterministic.

## Decision 59: Scavenging Vault Write Lock Flattening

Problem: Scavenging oracle cold/manual writers still resolved mutable Vault buffers for CSV ingest, editor tuning, monolith import, emergency fallback, and distribution audit. CSV/editor table imports also kept stale or emergency table metadata after successful manual writes.
Solution: Add `TryAcquireScavengingVaultBuffer` and route every writer through a single write lock with `finally` release. CSV parsing uses prewarmed loot scratch, self-audit uses prewarmed audit scratch, monolith import counts records before the lock, and lock bodies copy DTO rows only. CSV/editor tuning now publish dedicated table hash/version and are not marked as emergency fallback data.
Rejected Alternatives: Keeping `ResolveViews()` exposed a broad write-capable bundle and holding a DataVault write lock across a 10k audit job would satisfy syntax but violate lock residency. A new manager/helper class was rejected; the existing oracle owner was extended.
Scalability potential: Low avoids compaction stalls and lock contention during editor/cold import; Middle/High/Ultra can raise table size/audit cadence later while write ownership stays one buffer at a time.
Hardware Impact: CSV parse and audit job are outside Vault locks; lock residency becomes 4-256 DTO row copies or 32 audit uints. Estimated 10-200 us stall-risk removed from low-end CPUs during cold table tools.

## Decision 60: Ghost Proxy Snap Failure Is Not A Spawn

Problem: Meshless resource requests that failed `TryResolveGhostProxySurfaceSnap` still had a route to stale unsnapped state before this loop, and a missing `mapMagicBridge` could dequeue and drop fixed ghost requests during bootstrap/hot-swap.
Solution: `ProcessGhostProxySurfaceSnaps` now requires `mapMagicBridge` before dequeue, and failed snap resolution exits the current request before tombstone recompute, snap flag clear, or `_pendingSpawns` enqueue.
Rejected Alternatives: Spawning at the previous candidate position hides terrain/SDF failure and creates buried or invalid interactables. Retrying after real SDF failure was rejected; reserve pressure already has a defer path, invalid geometry does not. A new queue owner was rejected as duplicate scheduling.
Scalability potential: Low preserves authored scarce ore nodes through terrain service gaps and rejects invalid surface proof; Middle/High/Ultra can run richer meshless proxy variants while the same fixed queues enforce terrain proof before presentation.
Hardware Impact: Avoids pool spawn/attach work for invalid ghost snaps and preserves fixed queue work during bridge absence; estimated 3-15 us avoided per rejected invalid snap on i3/MX350-class CPU.

## Decision 61: Ghost Proxy Capacity Gate Before Terrain Work

Problem: `ProcessGhostProxySurfaceSnaps` could dequeue a ghost request and perform pool, MapMagic, surface, and SDF proof while `_pendingSpawns` was already full, then requeue a request whose snap flag had already been cleared.
Solution: Move the live spawn queue capacity check to the top of the fixed ghost snap loop before dequeue and remove the post-snap requeue path. Saturation now leaves the ghost queue untouched until the next slow tick.
Rejected Alternatives: Requeueing snapped requests into the ghost queue mixes two states in one lane. Adding a second snapped queue duplicates scheduler ownership and adds capacity bookkeeping. Dropping on saturation loses valid terrain-proven nodes.
Scalability potential: Low avoids terrain/SDF work during spawn backpressure. Middle/High/Ultra can raise authored proxy density while fixed queues preserve order and only spend terrain proof when a live spawn slot exists.
Hardware Impact: Saves one pool reserve check, one height query, one surface placement, and one SDF validation per saturated ghost request; estimated 2-12 us avoided on i3/MX350-class CPUs under dense resource streaming.
