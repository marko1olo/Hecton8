# Rationale_AUDIT_FRAME_LOGIC

Date: 2026-05-29
Status: COMPLETE

## Decision 001 - Audit Scope Without Provided Batch ID

Problem: User requested whole-project frame/render/logic audit but did not provide an AGENT_PROMPT id from CURRENT_BATCH.md.
Solution: Use explicit local ID AUDIT_FRAME_LOGIC, verify no matching XML tag exists via Select-String, and proceed as an ad-hoc audit with disk artifacts.
Rejected Alternatives: Blocking for an ID would waste the session; using another agent's XML block would contaminate domain boundaries.
Scalability potential: Audit must classify systems by continuous GlobalQualityWeight path from minimum survival to visual overkill.
Hardware Impact: No runtime gain yet. Expected output is route/risk map for later MX350/i3 fixes.

## Decision 002 - Selected Mandates

Problem: The request crosses rendering, frame phases, logic dispatch, global authority, memory, and GPU upload paths.
Solution: Select ARCH_Execution_Phases, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Project_Bootstrap_Sequence_Init_Safety, ARCH_Signal_Lane_Segregation, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Performance_Budgets_FrameTime_VRAM_Limits, REND_URP_Graphics_HotPath_Optimization_HLOD, GPU_Compute_Kernels_Kernels_Optimization_MX350.
Rejected Alternatives: Reading all registry files first would be bureaucracy and delay factual source scanning.
Scalability potential: The selected mandates cover Low/Middle/High/Ultra progression through cadence, capacity, HLOD, GPU dispatch, and visual sync.
Hardware Impact: No runtime gain yet. Audit target is identifying hot work that can be pushed out of simulation and into gated VISUAL_SYNC.

## Decision 003 - Static Proof Boundary

Problem: User requested a full project state audit, but no Unity import, Play Mode, profiler, GC capture, memory capture, device run, or player build was executed in this turn.
Solution: Map source routes and existing proof artifacts, then classify runtime-only claims as UNKNOWN instead of reporting readiness.
Rejected Alternatives: Claiming runtime green from static source; launching a dotnet rebuild despite the task being architectural and the project policy warning against unnecessary builds.
Scalability potential: Runtime proof must later measure Low, Middle, High, and Ultra paths separately because GlobalQualityWeight scales cadence, capacity, render scale, and effect density.
Hardware Impact: 0 us measured. Prevents false optimization claims on i3/MX350 without profiler data.

## Decision 004 - Frame Owner Classification

Problem: The project has many gameplay systems, but the authoritative execution route must be identified before judging performance or determinism.
Solution: Treat `SystemDispatcher` as gameplay frame owner and `RenderDispatcher`/URP RenderGraph features as presentation/render owner. `GameTickManager` is classified as legacy dispatcher-registered support, not the frame authority.
Rejected Alternatives: Auditing by Unity MonoBehaviour naming alone; assuming every system owns its own `Update` loop.
Scalability potential: Low tier can reduce cadence and capacities through dispatcher/quality weights; Middle/High/Ultra can buy visual overkill in render-side systems without changing gameplay truth.
Hardware Impact: 0 us measured. Route map targets future removals of hidden work from simulation frames.

## Decision 005 - Documentation Drift Correction

Problem: Current filesystem count is `168` first-party asmdefs under `Assets/_Project`; several docs still recorded `167`.
Solution: Update only the verified count in `PROJECT_BASELINE.md`, `PROJECT_RUNTIME_TOPOLOGY.md`, and `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`; leave generated dependency graph artifacts untouched and mark their count stale until regeneration.
Rejected Alternatives: Rewriting generated graph by hand; changing `AGENTS.md` scene doctrine without owner/integrator decision.
Scalability potential: Accurate topology prevents stale dependency assumptions when splitting low/mid/high/ultra ownership across assemblies.
Hardware Impact: 0 us measured. Documentation integrity only.

## Decision 006 - Render Debt Classification

Problem: The first-party render spine is mostly RenderGraph/SRP-based, but exceptions exist that can still hit visible-frame cost.
Solution: Classify `DiegeticPanelController` runtime `Graphics.Blit`, `GlobalShaderDispatcher` direct `Graphics.ExecuteCommandBuffer`, documented GPU `SetData` fallbacks, MPB/material mutation, camera stacking, and third-party legacy render paths as debt requiring profiler proof or migration.
Rejected Alternatives: Treating all shader global writes as defects; many are intended VisualSync bridges and require phase/frequency proof before changes.
Scalability potential: Low tier should disable or reduce legacy per-frame presentation work; Middle/High/Ultra can retain richer visor/fog/ocean effects when RenderGraph and upload cost are proven.
Hardware Impact: 0 us measured. Potential MX350 savings not claimed until runtime timings exist.

## Decision 007 - Logic Debt Classification

Problem: Dispatcher contract is strong, but recurrent cold-registry fallback and legacy tick list structures still exist.
Solution: Classify `GameTickManager` managed list buffering, `AbyssalShadowCullingRuntime.SlowTick` DataVault fallback, `FoveatedRenderCommander.SlowTick` DataVault/Thermal fallback, and mod managed fanout as debt or proof gaps, not immediate compile defects.
Rejected Alternatives: Removing fallback paths without explicit route cards; blocking on full rewrite of legacy tick support.
Scalability potential: Low tier needs fewer hot registry/cache misses and tighter tick cadence; Ultra can spend saved time on visual density rather than lookup overhead.
Hardware Impact: 0 us measured. Future target is sub-0.1 ms recurrent logic overhead per suspect lane.

## Decision 008 - BaseModule Hot Lookup Removal

Problem: `BaseModule` slow/fixed tick routes reached `TryGetComponent`, parent service lookup, and `GlobalRegistry.ConstructionRuntime` through `PowerSupplyRatio`, `TryResolveSubmarineAtmosphereSystem`, emergency notifications, and unmoored rigidbody capture.
Solution: Keep `_powerNode`, `_moduleRigidbody`, `_submarineAtmosphereSystem`, and `_constructionManager` as cold cached dependencies. Hot accessors now only read cached fields and skip when unavailable.
Rejected Alternatives: Lazy scene lookup from read properties; registry polling when emergency state flips; parent-chain service walk during flood/depth resolution.
Scalability potential: Low tier avoids repeated managed/Unity lookup branches; Middle/High/Ultra spend the saved budget on pressure/flood presentation rather than dependency search.
Hardware Impact: Estimated 35 us saved in clustered flooded/pressure slow ticks on i3/MX350 class CPU; larger impact when many modules flood simultaneously.

## Decision 009 - WorldCave Read Purity

Problem: `WorldCaveDirector.TryGetCaveAt` mutated cave dictionaries by removing stale entries and also refreshed cached biome context while serving a read accessor.
Solution: Use a pure cave-key calculation in `TryGetCaveAt`; return false for stale volumes; keep removal in `RefreshCaveLifecycleState` under `SlowTick`. Added `CopyActiveCavesTo` for caller-owned enumeration.
Rejected Alternatives: Returning `_caveInstances.Values` as the runtime path; deleting stale records from unknown consumer phases.
Scalability potential: Low tier avoids hidden dictionary churn from probes; Ultra can query cave state more often without changing ownership.
Hardware Impact: Estimated 4-12 us avoided per hot cave probe when stale entries exist; prevents unpredictable mutation stalls.

## Decision 010 - Vehicle Docking Candidate Ownership

Problem: `VehicleDockingModule.Tick` scanned every transport registry slot every frame and tested Unity object active state before any real overlap was known.
Solution: Add fixed overlap candidate caches populated by trigger enter/exit and seeded cold on enable/spawn. Runtime tick checks only the bounded overlap list. `PlayerTransportLifecycleRegistry.TryGetRegistered` exposes cached transport references for cold/event capture.
Rejected Alternatives: Polling all 64 slots forever; adding a managed event fanout; using a dynamic List for overlap candidates.
Scalability potential: Low tier pays O(overlapping transports) instead of O(all transports*docks); higher tiers can afford tighter docking gates and better presentation without registry polling.
Hardware Impact: Estimated 20-80 us saved per active dock frame on i3/MX350 class CPU in transport-heavy scenes.

## Decision 011 - Dock Telemetry Hot Allocation Removal

Problem: `RecordDockTelemetry` could fall through `TryAcquireDockTelemetryWrite` into `EnsureDockTelemetry` and DataVault `EnsureGenerationHandle` from `Tick`/`FixedTick`.
Solution: Keep telemetry handle creation in Awake/OnEnable/DataVault hotswap. Hot write now only validates cached handles, resolves arrays, and releases the single mutation guard in strict `finally`.
Rejected Alternatives: Allocating vault buffers from telemetry write; clearing descriptors repeatedly from hot failure branches.
Scalability potential: Low tier avoids rare hitch on invalid handles; Ultra keeps 300-frame black-box telemetry without frame-time spikes.
Hardware Impact: Removes a rare allocation/handle-acquisition stall; steady telemetry write remains one guarded ring write.

## Decision 012 - Transport Charging Station Candidate Ownership

Problem: `TransportChargingStation.Tick` scanned the whole transport lifecycle registry and filtered Unity behaviours every frame.
Solution: Seed tracked transports cold on enable, then maintain a fixed trigger-overlap cache through enter/exit events. Runtime `Tick` only validates cached slots.
Rejected Alternatives: Full registry scan per active charger; dynamic `List<T>` candidate storage; resolving transport components from the tick loop.
Scalability potential: Low tier pays O(active overlaps) per station; Middle/High/Ultra can raise station count without multiplying registry scans.
Hardware Impact: Estimated 10-45 us saved per active station frame on i3/MX350 class CPU in transport-heavy bases.

## Decision 013 - Field Loadout Advice Phase Split

Problem: `HUDQuickBar.LateFrameTick` and `PDALoadoutTab.LateFrameTick` reached `FieldLoadoutAdvisor.TryBuildForward*`, which can query the spatial grid and then call `TryGetComponent/GetComponentInParent` through source classification.
Solution: Add a role/kind-only `ForwardLoadoutSnapshot` route. `PlayerToolManager.LateFrameTick` refreshes it at 0.35 s cadence on the Player layer before UI, using static scratch only. HUD and PDA read cached preset/advice strings from the manager.
Rejected Alternatives: Keeping UI as the world-query owner; moving the query into `Tick`; widening `ToolLoadoutChangedSignal` with string or managed payload.
Scalability potential: Low tier keeps UI refresh deterministic and lookup-free; High/Ultra can still increase advice cadence or richer presentation without changing UI ownership.
Hardware Impact: Estimated 15-60 us removed from recurring UI presentation refresh; eliminated transitive component lookup from UI late-frame roots.

## Decision 014 - HUD Prefab Lookup Removal

Problem: `HUDQuickBar` dirty-slot rendering resolved prefab tool interfaces via `prefab.TryGetComponent` for icon, item hash, metadata hash, and durability.
Solution: Reuse `PlayerToolManager` assigned-prefab read-model cache through `TryGetAssignedToolDataReadModel`; quickbar renders from cached interfaces only.
Rejected Alternatives: Repeating prefab component lookup per slot paint; adding a second HUD-owned prefab cache.
Scalability potential: Low tier avoids repeated Unity component resolution during dirty UI bursts; higher tiers can spend the saved work on richer slot feedback.
Hardware Impact: Estimated 4-20 us saved per dirty quickbar refresh depending on slot count and prefab component layout.

## Decision 015 - Habitat Graph Registration Cache

Problem: `ConstructionManager.LateFrameTick` rebuilt dirty habitat graphs through `HabitatGraphManager.Rebuild`, and `PopulateModuleBuffer` resolved `ModuleMarker`, `BaseModule`, and `TransitionHatchMeshState` with `TryGetComponent` for each registered module.
Solution: Resolve those references once in cold `RegisterModule` and store `HabitatGraphModuleRegistration` beside the placed-module registry. `HabitatGraphManager.Rebuild` now consumes cached references.
Rejected Alternatives: Accepting dirty late-frame graph rebuild as "not every frame"; adding lazy resolution inside `ModuleRecord`; moving graph rebuild to another hot dispatcher phase.
Scalability potential: Low tier can rebuild small bases without Unity component lookup spikes; Middle/High/Ultra can tolerate larger habitat graph rebuilds and spend visual budget on stress/flood presentation.
Hardware Impact: Estimated 25-120 us saved per dirty habitat graph rebuild depending on module count and component layout.

## Decision 016 - Survival Hot Dependency Cache

Problem: `HectonSurvivalSystem.SlowTick` reached thermodynamics, modular equipment, hazard zones, player health, vehicle upgrade modules, and vegetation bridge through lazy `GlobalRegistry` or component fallback routes.
Solution: Cache registry services in cold/hotswap callbacks, consume cached `PlayerTransportLifecycleRegistry` vehicle upgrade refs, and make toxicity/oxygen/thermal/cold-pocket branches read cached references only.
Rejected Alternatives: Lazy service retry from survival damage code; `TryGetComponent` on active transport during oxygen and thermal resistance resolution.
Scalability potential: Low tier gets deterministic survival slow ticks; Middle/High/Ultra can spend the saved budget on richer physiological audio/visor presentation without changing health truth ownership.
Hardware Impact: Estimated 8-35 us removed from survival slow-tick bursts on i3/MX350 class CPU.

## Decision 017 - Fixed Physics Rebind Removal

Problem: `PlayerKinematicsRuntime.FixedTick` called `RebindColdIfMissing`, which could reach registry/DataVault rebinding from the physics phase every 64 frames.
Solution: Keep service/DataVault rebinding in cold setup and hotswap callbacks. Fixed tick now only refreshes camera transform from the cached player context.
Rejected Alternatives: Registry retry inside physics because it is infrequent; hidden fixed-phase dependency search is still phase impurity.
Scalability potential: Low tier avoids fixed-frame spikes; Middle/High/Ultra can maintain tighter motor smoothing without lookup stalls.
Hardware Impact: Estimated 3-15 us saved per 64 fixed frames and lower phase jitter.

## Decision 018 - Critical Audio Hot Lookup Cache

Problem: `PlayerCriticalProceduralAudioRenderer.Tick/SlowTick` polled `GlobalRegistry` for MapMagic, audio, player, ecosystem, and submarine hull read models through retry functions.
Solution: Bind services once in cold init and update them via `IGlobalRegistryHotSwapListener`; hot audio resolvers now return cached read models only.
Rejected Alternatives: Keeping frame-index throttled registry retry in audio because it is not every frame; audio stress frames must not search global services.
Scalability potential: Low tier keeps critical audio deterministic; Middle/High/Ultra can spend the saved time on granular/binaural audio density.
Hardware Impact: Estimated 10-45 us saved across stress-heavy audio frames depending on missing-service retry frequency.

## Decision 019 - Biolum Snapshot And Visual Phase Ownership

Problem: `HectonBiolumManager.Tick` read `GlobalRegistry.CelestialRuntimeSnapshot` and hot paths could register/unregister late-frame ticking while publishing shader state later.
Solution: Cache `ICelestialRuntimeSnapshotReadModel` cold, update it on celestial hotswap, keep late-frame tick registration stable, and leave shader global writes in `LateFrameTick`.
Rejected Alternatives: Per-frame global snapshot access; dynamic late-frame registration from biolum tick dirty paths.
Scalability potential: Low tier gets cheap deterministic phase math; Middle/High/Ultra can raise touch ripple and biolum density while maintaining presentation isolation.
Hardware Impact: Estimated 2-12 us saved per active biolum frame and fewer dispatcher registration branches.

## Decision 020 - Ecosystem DataVault Route Cache

Problem: `EcosystemDirector.SlowTick` and late-frame macro-swarm import reached `GlobalRegistry.DataVault` through `ResolveDataVault`.
Solution: Split cold fallback into `ResolveDataVaultCold`; hot `ResolveDataVault` is a pure cached-field read, with DataVault hotswap updating `_dataVault`.
Rejected Alternatives: Allowing a helper named `Resolve*` to mutate global dependency state from ecology phases.
Scalability potential: Low tier avoids ecology maintenance lookup stalls; Middle/High/Ultra can spend budget on larger macro-swarm visual import caps.
Hardware Impact: Estimated 3-18 us saved per ecology maintenance/import call when registry fallback would have executed.

## Decision 021 - Modular Equipment Lock Flattening

Problem: `ModularEquipmentEngine.TryAcquireEquipmentViewsWriteLock` acquired 28 DataVault write locks and carried them across equipment integration scheduling.
Solution: Replace the 28 writer fences with one `EquipmentViewsMutationGuardMask`; resolve current NativeArray views after the guard and release the single guard in existing `finally` paths.
Rejected Alternatives: Keeping many writer locks because release order was deterministic; deterministic release order does not remove multi-lock deadlock surface.
Scalability potential: Low tier avoids lock fanout cost in active tool ticks; Middle/High/Ultra can run richer upgrade matrix and flashlight telemetry without multiplying writer fences.
Hardware Impact: Removes 27 writer-fence acquisitions per equipment integration and eliminates the observed multi-lock deadlock vector.

## Decision 022 - Submarine OS Brownout Cache

Problem: `HectonSubmarineOS.ApplyAmbientLightPolicy` loop resolved `PowerNode` with `TryGetComponent` for every active module.
Solution: Reuse `BaseModule.CachedPowerGrid`, already owned and maintained by the module lifecycle.
Rejected Alternatives: Adding a parallel OS-owned power-node cache; BaseModule is the fact owner.
Scalability potential: Low tier avoids component lookup in brownout bursts; higher tiers can spend budget on richer brownout shader/audio presentation.
Hardware Impact: Estimated 3-18 us saved per active-module brownout scan burst on i3/MX350 class CPU.

## Decision 023 - Camera Juice Cached Slow Dependencies

Problem: `CameraJuiceSystem.SlowTick` retried survival and movement discovery through player root/component fallback.
Solution: Slow tick now mirrors cached `IPlayerRuntimeContext` and `ISubmarineRuntimeContext`; component fallback remains Awake/OnEnable only.
Rejected Alternatives: Keeping four-slow-tick retry because it is infrequent; the slow lane still must be deterministic.
Scalability potential: Low tier avoids UI/VFX jitter; High/Ultra can keep denser shake/FOV layers without dependency search.
Hardware Impact: Estimated 4-22 us saved per dependency retry window.

## Decision 024 - Sargassum And Indirect Vegetation Cold-Service Split

Problem: Sargassum and indirect vegetation slow ticks refreshed registry services and camera lists during visual maintenance.
Solution: OnEnable/hotswap own registry and camera binding. Slow ticks consume cached refs only.
Rejected Alternatives: Slow-tick "cold" refresh naming; a method name does not make a dispatcher phase cold.
Scalability potential: Low tier reduces vegetation maintenance spikes; Ultra can spend saved time on density, scavengers, and BRG culling.
Hardware Impact: Estimated 4-35 us saved across sargassum/indirect vegetation slow ticks depending on scene camera count.

## Decision 025 - Flora Bridge Hot Fallback Removal

Problem: Flora slow tick could enter `ResolveVegetationBridge`, which searched local/parent components when the bridge was missing or late.
Solution: Hot routes use cached override/registry bridge only; component fallback is isolated to startup cold path.
Rejected Alternatives: Parent-chain fallback during mask rebuilds; missing bridge should degrade visuals, not search scene hierarchy.
Scalability potential: Low tier keeps flora interaction cheap when streaming order is late; Middle/High/Ultra retain richer masks when bridge is available.
Hardware Impact: Estimated 6-30 us saved per slow tick when bridge is absent or unresolved.

## Decision 026 - Outpost Service Resolver Purity

Problem: Outpost generation resolvers for MapMagic, async persistence, and object pool looked pure but could poll globals from finalize/despawn paths.
Solution: OnEnable caches service fallbacks; resolver methods return cached references only.
Rejected Alternatives: Global polling during job finalization; finalize is already latency-sensitive because it uploads matrices and spawns proxies.
Scalability potential: Low tier avoids finalize spikes; Ultra can spend budget on more outpost shell/interactable density.
Hardware Impact: Estimated 3-16 us saved on finalize/despawn paths.

## Decision 027 - HUD Runtime Dependency And Scaler Split

Problem: HUD AutoResolve used registry-backed dependency refresh, and nested `HectonUIScaler.SlowTick` could register and create content roots.
Solution: HUD AutoResolve mirrors cached runtime services; scaler slow tick only latches an existing content root and applies a pure scale path.
Rejected Alternatives: Slow tick creating UI hierarchy; content bootstrap belongs to OnEnable/cold layout rebuild.
Scalability potential: Low tier avoids UI bootstrap stalls; High/Ultra can keep higher reactive HUD cadence.
Hardware Impact: Estimated 8-40 us saved during HUD repair/late bootstrap windows.

## Decision 028 - Volumetric Fog Slow Allocation Gate

Problem: Volumetric fog slow tick allowed GPU state allocation, which can become a visible hitch.
Solution: Slow tick now calls `TryPrepareGpuState(allowAllocation: false)`; allocation remains in Create/OnEnable/cold maintenance.
Rejected Alternatives: Repairing GPU resources from slow tick; visual degradation is safer than allocation hitch under gameplay.
Scalability potential: Low tier avoids render-maintenance stalls; Ultra retains volumetric overkill when cold resources are ready.
Hardware Impact: Rare allocation hitch removed. No steady microsecond saving claimed.

## Decision 029 - Drone Headless Hot Registry Cutoff

Problem: `HeadlessFleetDriver.Tick` entered `ScheduleHeadlessSimulation`, which still called `EnsureInitialized`; the guarded path could cold-cache `GlobalRegistry` services, allocate headless memory, and register dispatcher lanes if the first tick arrived during late bootstrap.
Solution: Remove `EnsureInitialized` from the tick root. Headless driver registration is already a cold prerequisite for the driver to tick, and runtime hotswap updates cached services.
Rejected Alternatives: Keeping the call because it usually returns fast; a hot root must not contain a cold bootstrap escape hatch.
Scalability potential: Low tier avoids bootstrap jitter from drone AI; Middle/High/Ultra keep drone cadence and path detail without registry discovery.
Hardware Impact: Estimated 5-30 us saved on late bootstrap/missing-cache frames; steady initialized frames mainly lose a branch chain.

## Decision 030 - Drone Headless Lock Flattening

Problem: Headless drone simulation held the headless mutation guard, then service command, transaction, core mirror, snapshot, and blackbox routes could acquire additional DataVault guards or write locks before the headless guard was released.
Solution: Expand the headless guard to cover service command, transaction, procedural, core/mirror, and blackbox buffers. Service and transaction helpers now resolve buffers under the active headless guard and fail closed instead of acquiring another guard. Blackbox writes use the active guard in the normal completion path.
Rejected Alternatives: Deterministic nested lock order; deterministic order still leaves a multi-lock deadlock surface under compaction/hotswap pressure.
Scalability potential: Low tier avoids lock fanout and completion stalls; High/Ultra can spend the saved completion budget on richer drone swarm presentation.
Hardware Impact: Removes 2-4 nested guard acquisitions from the normal headless completion/service-drain path and one blackbox writer fence.

## Decision 031 - Drone Snapshot Publish After Guard Release

Problem: `CompleteHeadlessSimulationAndApply` published snapshot and telemetry while simulation DataVault views were still guarded; snapshot enqueue could compact/write payload buffers and previously could ensure payload buffers from the hot publish route.
Solution: Defer snapshot and telemetry publish until after headless guard release. `HectonDroneFleetEvents.Enqueue` now requires preinitialized payload buffers and reports bounded overflow instead of allocating/ensuring from the publish path.
Rejected Alternatives: Publishing inside the simulation guard for convenience; presentation/event fanout does not need mutable drone state ownership.
Scalability potential: Low tier keeps late-frame completion shorter; Middle/High/Ultra can keep richer UI/telemetry listeners without holding simulation locks.
Hardware Impact: Removes rare payload-buffer allocation/guard work from the headless completion critical section. No profiler number claimed.

## Decision 032 - Volumetric Fog GPU Upload Lock Split

Problem: `HectonVolumetricParticulateFogFeature.TryWriteAndUploadMockLights` wrote fog point-light DTOs and then mapped/uploaded the GPU point-light buffer while `_pointLightsHandle` DataVault write lock was still held.
Solution: Keep only DataVault mutation and an 8-entry DTO copy inside the lock. Release `_pointLightsHandle` in `finally`, then upload from a cold `PointLightDTO[8]` scratch into the inactive double-buffered `GraphicsBuffer`.
Rejected Alternatives: Keeping the upload under the write lock; using a persistent `NativeArray` scratch field, which violates memory sovereignty for runtime managers; reading the DataVault NativeArray after releasing the lock.
Scalability potential: Low tier avoids lock hold extension during GPU map/write stalls; Middle/High/Ultra can keep richer fog point-light counts through `GlobalQualityWeight` without coupling graphics upload latency to DataVault ownership.
Hardware Impact: Removes GPU buffer mapping duration from the DataVault point-light critical section. CPU microseconds are pending profiler proof; lock risk reduction is static.

## Decision 033 - Vegetation Dirty-Page Upload Lock Split

Problem: `HectonIndirectVegetationRenderer.TryUploadDirtyPages` held a dirty-page DataVault write lock while `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPages` mapped and copied GPU buffer spans.
Solution: Copy dirty page bytes into a cold managed snapshot under the DataVault lock, release the lock in `finally`, upload from the snapshot through `UploadNativeArrayDirtyPagesFromSnapshot`, then reacquire one short write lock only to clear pages marked uploaded.
Rejected Alternatives: Keeping `LockBufferForWrite` inside the vault lock; copying unsafe upload code into the renderer; using a persistent `NativeArray` snapshot that would add another native owner route.
Scalability potential: Low tier keeps vegetation visual sync from blocking DataVault ownership during slow GPU map/write stalls; Middle/High/Ultra can raise vegetation density and dirty-page upload budget without coupling graphics upload latency to DataVault locks.
Hardware Impact: No profiler microseconds claimed. Static saving is removal of GPU buffer mapping duration from the dirty-page write-lock window; added cost is one bounded byte-copy over dirty-page capacity during dirty uploads.

## Decision 034 - Sargassum Threat Grid Upload Lock Split

Problem: `SargassumMicroFaunaBoids.RefreshThreatGridPayloadVisualSync` copied threat-grid data into `_threatGridUploadHandle` and then uploaded `_threatGridBuffer` before releasing the DataVault write lock.
Solution: Allocate a cold `uint[ThreatGridMaxCellCount]` snapshot, copy the compressed threat grid into both DataVault and the snapshot under lock, release in `finally`, then upload the snapshot after release.
Rejected Alternatives: Uploading directly from the vault `NativeArray<uint>`; allocating a fresh array during visual sync; moving threat-grid truth out of the existing DataVault handle.
Scalability potential: Low tier avoids DataVault stalls when the threat grid upload maps slowly; Middle/High/Ultra can keep higher threat-grid resolution and richer micro-fauna predator response without extending lock windows.
Hardware Impact: No profiler microseconds claimed. Static saving is removal of GPU upload duration from the threat-grid write-lock window; added work is one bounded uint copy already aligned with the payload write.

## Decision 035 - Ecology Flora Predator AUP Upload Lock Split

Problem: `EcosystemDirector.PublishFloraPredatorAupBufferImmediate` filled `_floraPredatorAupUpload` and uploaded the predator AUP graphics buffer before releasing the `VaultBufferView<float4>` write lock.
Solution: Allocate a cold `float4[32]` snapshot, mirror each accepted apex predator AUP payload into DataVault and snapshot under lock, release in `finally`, then upload the snapshot after release.
Rejected Alternatives: Uploading from the vault `NativeArray<float4>`; using a persistent native scratch for a 32-entry visual payload; moving the spatial query into the upload phase.
Scalability potential: Low tier keeps ecology visual sync lock windows bounded; Middle/High/Ultra can afford richer predator visual influence without coupling graphics upload latency to DataVault ownership.
Hardware Impact: No profiler microseconds claimed. Static saving is removal of GPU upload duration from the flora predator AUP write-lock window; added cost is up to 32 float4 stores into a cold managed snapshot.

## Decision 036 - Fluid Advection Dirty-Page Upload Lock Split

Problem: `HectonFluidEngine.FlushFluidAdvectionDirtyLane` held a fluid dirty-page DataVault write lock while `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesSetData` called `GraphicsBuffer.SetData` for both advection buffers.
Solution: Add cold byte snapshots for silt, bubble, and debris dirty pages. Copy dirty pages to the snapshot under lock, mark original dirty pages as `UploadedDirtyPageSnapshotMarker`, release in `finally`, then upload both buffers through `UploadNativeArrayDirtyPagesSetDataFromSnapshot`. Reacquire one short lock only to clear pages that still carry the in-flight marker.
Rejected Alternatives: Keeping `SetData` inside the write lock; blind post-upload clearing from a stale snapshot; allocating snapshot arrays from the visual sync path.
Scalability potential: Low tier avoids DataVault ownership stalls when SetData is slow; Middle/High/Ultra can raise advection particle density and upload budget without coupling graphics upload latency to dirty-page ownership.
Hardware Impact: No profiler microseconds claimed. Static saving is removal of SetData duration from three fluid-advection dirty-page write-lock windows; added work is bounded to <=64 byte copies for silt, <=32 for bubbles, <=16 for debris per dirty upload pass.

## Decision 037 - Depth Zone Survival Runtime Cache

Problem: `DepthZoneDirector.SlowTick` could call `ResolveSurvivalSystem`, which fell back to `BootstrapState.TryGetCurrentPlayerTransform` and `playerTransform.TryGetComponent(out survivalSystem)` when the serialized survival reference was missing.
Solution: Add cached `IPlayerRuntimeContext` ownership. `CacheRegistryServicesCold` reads `GlobalRegistry.Player` once and resolves `HectonSurvivalSystem` from `IPlayerRuntimeContext.SurvivalSystem`; `OnGlobalRegistryServiceReplaced(Player)` updates the cached context and survival reference. `SlowTick` now returns if survival is absent and never searches the scene.
Rejected Alternatives: Keeping a lazy player-root component search because the tick is slow; slow phase is still a dispatcher runtime phase and must not perform cold discovery. Adding a second scene search fallback was rejected for the same reason.
Scalability potential: Low tier gets deterministic depth-zone updates with no missing-reference scene search. Middle/High/Ultra can spend budget on richer zone notification, PDA cartography, and hull-warning presentation without changing depth truth ownership.
Hardware Impact: Estimated 2-10 us saved per missing/late survival slow tick on weak CPU, plus removal of a variable Unity component-search spike. No profiler number claimed.

## Decision 038 - Abyssal Shadow Slow Repair Cutoff

Problem: `AbyssalShadowCullingRuntime.SlowTick` could call `EnsureInitialized`, which can ensure DataVault buffers and allocate GPU buffers. That made a slow dispatcher lane a resource repair lane.
Solution: Split readiness validation from allocation. `SlowTick` now calls `HasInitializedResourcesReady`; DataVault hotswap and cold lifecycle own `EnsureInitialized`; upload fails closed and requests refresh if GPU buffers are absent.
Rejected Alternatives: Keeping slow-tick repair because culling needs resilience; visual degradation for one refresh window is cheaper than runtime allocation under gameplay.
Scalability potential: Low tier avoids culling repair hitches. Middle/High/Ultra can keep larger culling capacity without coupling resource allocation to slow-frame maintenance.
Hardware Impact: Rare allocation hitch removed from slow lane. No profiler microseconds claimed.

## Decision 039 - Jacobian Foam GPU Allocation Phase Split

Problem: `JacobianFoamGpuRuntime.LateFrameTick` called a method that could create `GraphicsBuffer` and `RTHandle` resources when foam resolution changed or buffers were missing.
Solution: Split `HasGpuStateReady` from `EnsureGpuStateCold`. `LateFrameTick` only validates prepared resources and requests a pending rebuild; OnEnable/cold rebuild performs allocation. Presentation payload publication remains in `LateFrameTick`.
Rejected Alternatives: Allocating max-resolution textures forever; it would protect LateFrame but destroy low-tier scaling. Allocating directly in LateFrame was rejected because resolution-change stalls belong outside the visual-sync publish path.
Scalability potential: Low tier can keep lower foam resolution without surprise RTHandle recreation in the visible frame. Middle/High/Ultra can request higher foam resolution and pay allocation in the cold repair lane before presentation resumes.
Hardware Impact: Rare resolution-change allocation hitch moved out of LateFrame. No profiler microseconds claimed.

## Decision 040 - Jacobian Foam DataVault Upload Lock Split

Problem: Jacobian foam params and wake uploads mapped `GraphicsBuffer` while DataVault params/wake write or read lanes were still held.
Solution: Params upload now uses the value built before the lock; wake upload copies up to 64 DTOs into a cold managed snapshot under lock/read pin, releases in `finally`, then maps the inactive GPU buffer after release.
Rejected Alternatives: Keeping Burst copy jobs over vault `NativeArray` sources; that still requires the vault lane to stay live through GPU mapping. Allocating a fresh upload array per frame was rejected for GC.
Scalability potential: Low tier avoids DataVault lane stalls when GPU mapping is slow. Middle/High/Ultra can keep richer wake counts through continuous `GlobalQualityWeight` without extending DataVault ownership windows.
Hardware Impact: Static saving is removal of GPU-map duration from three DataVault lane windows. Added work is bounded to one 32-byte params value copy and up to 64 wake DTO stores into a cold snapshot.

## Decision 041 - Biolum Profile Hot Helper Split

Problem: `BiolumPulseSyncRuntime.LateFrameTick` reached `TryAcquireProfileBuffer`; that helper contained an `allowEnsure` branch to `EnsureVaultBuffers`, so the static hot graph still carried a cold DataVault allocation route even when runtime calls passed the default false value.
Solution: Split the route into `TryAcquireProfileBufferCold` for profile loading and a pure `TryAcquireProfileBuffer` for late-frame reads. Late-frame now has no method body containing `EnsureVaultBuffers` or `EnsureGenerationHandle` in its transitive graph.
Rejected Alternatives: Keeping the optional bool and relying on call-site convention. Convention does not satisfy a source-graph verifier.
Scalability potential: Low tier avoids accidental visual-frame repair if a future call flips the flag. Middle/High/Ultra keep richer profile-driven pulse groups without letting profile storage repair enter presentation.
Hardware Impact: Rare visual-frame DataVault ensure/file-profile path removed. No steady profiler microseconds claimed.

## Decision 042 - Biolum Cold IO And Mutation Guard Split

Problem: Profile loading mixed file IO and DataVault profile mutation in one conceptual path, making it easy to hold profile ownership while doing work that does not need vault access.
Solution: Read the profile bytes into stack memory first, then acquire `ProfileFloatsGuardMask` only for bounded NativeArray writes. Emergency mock generation also uses single mutation guards with strict `try/finally`.
Rejected Alternatives: Holding the profile guard around file reads; moving profile bytes to a managed heap buffer would violate the zero-GC direction for this route.
Scalability potential: Low tier keeps profile reload bounded. Higher tiers can ship larger profile sets only through a cold import path, not frame presentation.
Hardware Impact: Removes file IO duration from the DataVault profile critical section. Added work is stack byte read and bounded float sanitization.

## Decision 043 - Shinobu Material Response Cold Repair Split

Problem: `ShinobuMaterialResponseRuntime` simulation and visual-sync phases could call `EnsureVaultState` and `EnsureGraphicsBuffers`, allowing DataVault handle creation and `GraphicsBuffer` allocation from hot lanes.
Solution: Add cold tick repair ownership, pure `HasVaultStateReady`, and pure `AreGraphicsBuffersReady`. Visual sync returns if simulation is still scheduled and records telemetry only after GPU upload, under one short mutation guard.
Rejected Alternatives: Recreating graphics buffers from visual sync because the render result is important; skipping one presentation frame is cheaper than allocating in the visible frame.
Scalability potential: Low tier degrades by missing a material-response presentation frame until cold repair succeeds. Middle/High/Ultra keep higher wear/material density without coupling allocation to VISUAL_SYNC.
Hardware Impact: Rare GPU/DataVault repair hitch removed from material response hot phases. Static graph reports zero forbidden hot paths; no profiler microseconds claimed.

## Decision 044 - Plasma Beam Guard And Signal Split

Problem: `ShinobuPlasmaBeamRuntime` hot roots could ensure vault/GPU state, and post-simulation could dump telemetry or push acoustic taps while the plasma mutation guard was still held.
Solution: Add cold tick repair ownership, pure vault/GPU readiness checks, a simulation-scheduled guard at visual sync entry, and a fixed `PlasmaBeamAcousticEchoTap[MaxBeamCount]` publish snapshot. Telemetry dump and `SignalBus` publish now happen after `ReleasePlasmaBeamJobMutationGuard`.
Rejected Alternatives: Keeping signal push inside the guard for convenience; event fanout and file dump are not part of mutable beam state ownership.
Scalability potential: Low tier avoids lock elongation from file IO or signal listeners. Middle/High/Ultra can increase beam visual richness and acoustic listeners without extending the simulation critical section.
Hardware Impact: Removes file IO/signal fanout duration from the DataVault critical section. Added work is bounded to 20 tap DTO copies.

## Decision 045 - Direct Hot Resource Repair Cutoff

Problem: A broad pass over runtime scripts found 20 direct hot roots where `Tick`, `FixedTick`, `SlowTick`, `LateFrameTick`, `PreSimulationTick`, `ScheduleSimulation`, `PostSimulationTick`, or `VisualSyncTick` could call DataVault/GPU repair helpers such as `EnsureVaultState`, `EnsureVaultBuffers`, `EnsureGraphicsResources`, or `EnsureGraphicsBuffers`. The final direct scan also included Unity `Update`, `FixedUpdate`, and `LateUpdate` roots.
Solution: Move resource creation and handle acquisition into lifecycle and `IColdTickable` repair lanes. Hot phases now use pure readiness checks and fail closed until the owner cold lane has prepared state.
Rejected Alternatives: Trusting `allowAcquire:false` flags or naming convention as proof. A future call-site can flip the flag; a pure readiness helper has no repair branch to accidentally activate.
Scalability potential: Low tier skips one presentation/simulation slice instead of allocating in-frame. Middle, High, and Ultra tiers can run richer visuals once cold resources are ready without changing gameplay truth ownership.
Hardware Impact: Static removal of 20 direct cold-repair vectors from hot roots. No profiler microseconds claimed.

## Decision 046 - Fabrication And Atmosphere Phase Ownership

Problem: Fabrication and atmosphere runtime phases could repair vault state or stage crash dump IO from simulation/presentation flow.
Solution: Add cold repair ownership to `FabricationAssemblerRuntime` and `BaseAtmosphereLogisticsRuntime`. Simulation and visual sync now read prepared state only. Atmosphere fault dump data is staged while guarded and written only after release.
Rejected Alternatives: Keeping fault resilience inside the hot phase. Crash diagnostics are valid, but file IO and handle creation are not part of settled simulation ownership.
Scalability potential: Low tier avoids hidden repair stalls in fabrication/atmosphere lanes. Higher tiers can spend the saved frame budget on denser logistics and gas visuals without moving authority.
Hardware Impact: Rare DataVault repair and file IO are removed from gameplay/visual phase windows. No steady profiler number claimed.

## Decision 047 - Physiology Runtime Repair Split

Problem: Sensory impairment and metabolism slow lanes could perform DataVault acquisition and editor profile/CSV work from runtime cadence.
Solution: Move sensory vault/mock buffer repair, metabolism vault state preparation, and editor profile reloads into cold tick/lifecycle routes. Hot physiology lanes consume ready handles or request repair and return.
Rejected Alternatives: Treating `SlowTick` as safe for discovery. Slow cadence is still runtime phase execution and must stay predictable.
Scalability potential: Low tier receives deterministic physiology cadence with no surprise file or handle work. Middle, High, and Ultra can raise sensory/metabolic presentation richness through prepared buffers.
Hardware Impact: Rare DataVault acquisition and editor file polling removed from physiology runtime lanes. No profiler number claimed.

## Decision 048 - Presentation GPU Repair Split

Problem: Cockpit, bulkhead, charger, and shoreline foam presentation paths could allocate graphics buffers or repair vault state from visual sync or slow UI lanes.
Solution: Move GPU/vault repair into cold ticks or lifecycle setup. Visual sync and slow UI now test pure readiness (`HasGraphicsBuffersReady`, `HasVaultStateReady`) and skip presentation until resources are valid.
Rejected Alternatives: Allocating graphics resources from `VISUAL_SYNC` to keep visuals alive. A missing frame is cheaper than a visible allocation hitch.
Scalability potential: Low tier can miss non-authoritative presentation while cold repair catches up. Middle, High, and Ultra can use larger visual buffers once prepared without coupling allocation to the visible frame.
Hardware Impact: Rare graphics allocation/rebind hitches removed from cockpit, containment, charger, and foam presentation routes. No profiler number claimed.

## Decision 049 - Vehicle Physics Buffer Readiness

Problem: Vehicle physics owners could reach DataVault buffer ensure routes from `FixedTick`, `SlowTick`, or `LateFrameTick`, including submarine dynamics, component damage, autopilot SDF navigation, and hydrodynamic KCC.
Solution: Move DataVault ensure and external command/damage refresh work into cold/lifecycle routes. Fixed/slow/late lanes now consume `_buffersReady` or pure generation-handle readiness only.
Rejected Alternatives: Allowing fixed physics to self-heal by acquiring handles. Fixed phase must be deterministic and must not contain allocation/rebind branches.
Scalability potential: Low tier gets predictable vehicle simulation cadence. Middle, High, and Ultra can spend budget on richer hydro/vehicle presentation after cold prep.
Hardware Impact: Static removal of fixed-frame allocation branch vectors from vehicle physics. No profiler microseconds claimed.

## Decision 050 - Compile Throttle And Static Proof Boundary

Problem: The request requires no build spam and in-memory syntax validation, but the local Roslyn assembly set has conflicting `System.Memory` and `System.Runtime.CompilerServices.Unsafe` requirements in this session.
Solution: Do not launch `dotnet build`. Use bounded static source scans, local hot-root source graph checks, brace balance, and `git diff --check`. Record the Roslyn dependency conflict instead of claiming syntax proof that did not complete.
Rejected Alternatives: Forcing a full build despite compile throttle; killing unrelated user Python services; lying about Roslyn success.
Scalability potential: Low tier developer machines avoid compiler contention. High-end machines still need a controlled build/profiler pass when compile gate is clean.
Hardware Impact: No build CPU consumed by this agent. Static proof covers source-level route violations; it does not replace Unity import, player build, or runtime profiler proof.

## Decision 051 - Shoreline Foam Visual Sync Direct Math

Problem: `ShorelineFoamGraftRuntime.VisualSyncTick` executed `DecayShorelineFoamOpacityJob.Run`, `GenerateMockShorelineFoamDataJob.Run`, and a transitive upload copy job `Run` from the visible frame path.
Solution: Replace the synchronous jobs with direct bounded methods over pre-resolved `NativeArray` views and an unsafe memcpy into the mapped GPU buffer. The path remains zero-GC and uses already prepared buffers only.
Rejected Alternatives: Keeping `IJob.Run` because the work is small. Tiny synchronous jobs do not buy parallelism and hide scheduler/sync semantics inside `VISUAL_SYNC`.
Scalability potential: Low tier avoids per-frame job wrapper overhead on a visual fake. Middle, High, and Ultra keep the same foam quality scaling through `GlobalQualityWeight` and shader loop limits.
Hardware Impact: Static saving is removal of three synchronous Job API calls from shoreline visual sync. No profiler microseconds claimed.

## Decision 052 - Somatic Kinematics PostFixed Completion Window

Problem: `SomaticKinematicsRuntime.FixedTick` ran `SomaticKinematicsJob` synchronously with `job.Run()` and immediately flushed local scratch back to DataVault in the fixed phase.
Solution: Schedule the Burst job from `FixedTick`, then complete it in the explicit `PostFixedTick` dispatcher lane before DataVault flush and `SignalBus` publication. On disable, destroy, hotswap, and origin shift force completion before state mutation or release.
Rejected Alternatives: Direct `job.Execute()` would remove Job API text but likely lose Burst execution. Leaving `job.Run()` keeps same-frame synchronous work inside the fixed root.
Scalability potential: Low tier gets an explicit completion window instead of hidden fixed-phase sync. Middle, High, and Ultra can keep richer somatic math while preserving the owner phase boundary.
Hardware Impact: Static saving is removal of direct synchronous `.Run()` from a hot fixed root. Runtime gain is pending Unity profiler proof.

## Decision 053 - Somatic Native-State Readiness Split

Problem: `SomaticKinematicsRuntime.FixedTick` and `SlowTick` called `EnsureNativeState(false)`. The runtime branch was safe by argument value, but the helper body still contained cold `AllocateVaultBuffers` and legacy load paths, so a source-graph verifier could not prove the hot path was decoupled.
Solution: Split the helper into pure `HasNativeStateReady` for hot roots and `PrepareNativeStateCold` for Awake, OnEnable, and DataVault hotswap. Hot roots now only test prepared handle readiness.
Rejected Alternatives: Keeping the bool convention. Path-sensitive discipline is fragile and not acceptable for APEX source proof.
Scalability potential: Low tier avoids accidental cold repair under player kinematics. Middle, High, and Ultra can keep richer somatic math once buffers are prepared without changing authority.
Hardware Impact: Static removal of cold allocation branch reachability from somatic fixed/slow roots. No profiler microseconds claimed.

## Decision 054 - Somatic Completion Route Naming

Problem: The forced completion route was named `CompletePendingJob`, which looked like a hidden generic sync point even though the intended owner phase is `PostFixedTick` plus shutdown/hotswap safety.
Solution: Rename it to `CompleteScheduledKinematicsInPostFixedOrShutdown` and keep the only transitive `.Complete()` path under `PostFixedTick` or forced lifecycle mutation boundaries.
Rejected Alternatives: Leaving the name vague and documenting intent only in logs. The C# source must carry the proof.
Scalability potential: Low tier keeps player kinematics completion bounded to an explicit owner window. Higher tiers can scale kinematics work without adding hidden fixed-root completion.
Hardware Impact: No runtime microsecond claim. Static proof now reports one completion path and it is phase-named.

## Decision 055 - Underwater Visual Cold Repair Ownership

Problem: `HectonUnderwaterVisuals.SlowTick` directly called HUD fog luminance and photophobia resource repair helpers, and runtime recache request flags raised by visual paths were not serviced after startup.
Solution: Implement `IColdTickable` on `HectonUnderwaterVisuals`; register/unregister cold ticking with the existing tick manager route; move compute/RT repair and requested runtime visual/service recache into `ColdTick`. `SlowTick`, `LateFrameTick`, and `Render` remain free of direct registry/component lookup, resource repair, synchronous job run, or explicit completion tokens by source graph.
Rejected Alternatives: Calling the same helpers with `allowAllocate:false` from `SlowTick`; that would leave an `Ensure*` route in the hot source root. Keeping request flags unserviced was rejected because it creates stale camera/service drift after references are invalidated.
Scalability potential: Low tier misses optional HUD/photophobia presentation until cold repair succeeds instead of allocating in runtime slow maintenance. Middle, High, and Ultra tiers can keep compute-backed visual overkill once resources are prepared without moving lookup or allocation into presentation.
Hardware Impact: Rare RT/compute allocation hitch removed from slow visual maintenance. No steady profiler microseconds claimed; static proof reports 160 transitive hot methods visited with zero forbidden hits.

## Decision 056 - Base Atmosphere Fixed-Phase Repair Cutoff

Problem: `BaseAtmosphereEngine.FixedTick` could call native/DataVault preparation and default atmosphere seeding before simulation work. `RecordBlackBox` also reached blackbox buffer preparation from the fixed path.
Solution: Implement `IColdTickable`; move native state preparation, vault rebind repair, and default seeding into `ColdTick`; make `FixedTick` require pure `HasNativeStateReady`; make blackbox recording write only when the telemetry ring is already prepared.
Rejected Alternatives: Keeping `EnsureNativeState` in fixed with guard flags. The source still exposes allocation/rebind branches to the fixed root and cannot be proven phase-safe by static graph.
Scalability potential: Low tier skips atmosphere simulation while cold ownership repairs state instead of stalling fixed phase. Middle, High, and Ultra can increase gas compartment fidelity after prepared handles exist without changing truth ownership.
Hardware Impact: Rare fixed-frame DataVault allocation/rebind branch removed. Static graph reports 17 hot methods visited with zero forbidden paths; no profiler microseconds claimed.

## Decision 057 - Submarine Atmosphere Job And Event Phase Split

Problem: `SubmarineAtmosphereSystem.FixedTick` repaired native state, pressure event queues could initialize through `GlobalRegistry.DataVault` from event enqueue paths, and the atmosphere solve used `job.Run()` inside the fixed root.
Solution: Add cold tick ownership for atmosphere vault preparation, high-pressure/fatal-pressure event buffer preparation, and authoring cache prewarm. Hot pressure enqueue now uses cached DataVault only and fail-closes if cold prep is missing. `ScheduleAtmosphereJob` now schedules the job; `PostFixedTick -> ConsumeCompletedJob` owns completion, vault flush, signal publish, and lock release.
Rejected Alternatives: Direct `Execute` or `Run` in fixed. That removes the scheduler surface but keeps the solve synchronous inside the simulation root. Lazy event-buffer initialization from pressure enqueue was rejected because events can be produced during fixed pressure bursts.
Scalability potential: Low tier gets fixed-phase predictability and a single post-fixed completion window. Middle, High, and Ultra can spend budget on richer room atmosphere math and pressure reactions without adding hot registry or allocation routes.
Hardware Impact: Static removal of `job.Run`, native prep, and event-buffer allocation from fixed pressure paths. Qualified source graph reports 148 hot methods visited with zero forbidden paths; no profiler microseconds claimed.

## Decision 058 - Pass 18 Compile Throttle

Problem: The local machine was above the allowed compile threshold during the first verification pass, and the local Roslyn plugin assemblies fail in this PowerShell host (`Add-Type` loader failure, then `StringTable` initializer failure on direct parse).
Solution: Do not launch `dotnet build`; use source graph verification, brace balance, and `git diff --check`. Record that full compile and Roslyn syntax proof are intentionally absent for this pass.
Rejected Alternatives: Spamming a build under high CPU to produce a symbolic green check; claiming Roslyn syntax success after assembly binding failed.
Scalability potential: Low-end developer machines remain responsive under multi-agent work. High-end machines can run the same code through build/profiler once the compile gate is clean.
Hardware Impact: No build CPU consumed by this agent in Pass 18. `git diff --check` reports only CRLF normalization warnings.

## Decision 059 - Root Tick Manager Initialization Cutoff

Problem: `GameTickManager.Tick` and `FixedTick` called `EnsureInitialized`, which could allocate `TickList<T>` buffers from the dispatcher root if lifecycle ordering was wrong.
Solution: Keep `EnsureInitialized` in `Awake`, `OnEnable`, `InitializeService`, and public registration APIs. Hot tick roots now use pure `AreTickListsReady` and return if cold lifecycle did not prepare the lists.
Rejected Alternatives: Keeping defensive allocation in the root dispatcher. A central update/fixed owner must not repair itself from the same hot lanes it dispatches.
Scalability potential: Low tier avoids root-frame allocation spikes. Middle, High, and Ultra keep the same tick contract while richer systems depend on a cleaner dispatcher boundary.
Hardware Impact: Static removal of dispatcher-root cold allocation branch. Hot source graph reports 9 visited methods with zero forbidden paths.

## Decision 060 - Audio Runtime Cold Vault Ownership

Problem: `AdaptiveStemAudioMixer`, `DynamicMusicGranularSynthesizer`, and `VocalBankPlaybackRuntime` lazily called `EnsureVaultStorage` from `Tick` or `SlowTick`, allowing audio frame lanes to allocate DataVault buffers.
Solution: Add `IColdTickable` ownership and register those runtimes on cold lanes. Cold tick owns vault preparation, profile/CSV maintenance, global quality refresh, and bank/bootstrap repair. `Tick`, `SlowTick`, `LateFrameTick`, and `OnAudioFilterRead` now consume prepared state or return.
Rejected Alternatives: Keeping lazy repair because missing audio is visible. A missed or silent audio frame is cheaper than a frame-time vault allocation or audio callback stall.
Scalability potential: Low tier avoids audio allocation spikes and callback contention. Middle, High, and Ultra can keep richer granular/stem/vocal synthesis once buffers are prepared.
Hardware Impact: Static audio hot graphs report zero forbidden vault repair, registry lookup, component lookup, or synchronous run calls across 32, 29, and 16 visited methods.

## Decision 061 - Path Funnel Blackbox Dump Guard Split

Problem: `PathFunnelNavmeshRuntime.LateFrameTick` could write a blackbox dump file while holding `TelemetryMutationGuardMask`, extending a DataVault mutation guard over directory creation and `FileStream` IO.
Solution: Copy the 300-frame telemetry ring into `_telemetryDumpBytes`, a preallocated byte buffer, while the guard is held. Release the guard before writing the file. If the write fails, reacquire the guard only to patch the failure flag and telemetry entry.
Rejected Alternatives: Keeping `FileStream` inside the telemetry guard for access to `NativeArray`. The correct route is a bounded zero-GC copy under lock, then IO after release.
Scalability potential: Low tier avoids long IO critical sections during pathfinding telemetry. Higher tiers can keep blackbox evidence without stalling DataVault mutation lanes.
Hardware Impact: Removes file IO duration from the pathfinding telemetry guard. Source line proof shows guard release before `TryDumpBlackBox`; graph reports 38 visited methods with zero forbidden hot lookup/allocation paths.

## Decision 062 - Pass 19 Compile Throttle

Problem: Verification ended with CPU load at 74 percent and active `dotnet` PID 17292.
Solution: Do not launch `dotnet build`; use source graph checks, brace balance, and `git diff --check`.
Rejected Alternatives: Running a build while another compiler host is active. The throttle is a hard project rule, not a suggestion.
Scalability potential: Low-end developer machines and parallel agents avoid compiler contention.
Hardware Impact: No build CPU consumed by this agent in Pass 19.

## Decision 063 - Ocean Surface Slow Lane Resource Cutoff

Problem: `ShinobuOceanSurfaceAtmosphereRuntime.SlowTick` repaired wave GraphicsBuffers, readback buffers, and sampler kernel binding from a runtime maintenance lane.
Solution: Add `IColdTickable`; move camera recache, wave GPU buffer repair, readback buffer repair, and kernel resolution to `ColdTick`. `SlowTick` now only applies storm surge when no wave parameter job is pending.
Rejected Alternatives: Keeping buffer repair in `SlowTick` because it is only 10 Hz. Slow cadence still competes with frame ownership and can allocate/rebind graphics resources.
Scalability potential: Low tier skips optional ocean presentation until cold repair succeeds. Middle, High, and Ultra keep richer wave/readback visuals without coupling GPU repair to runtime maintenance.
Hardware Impact: Rare graphics allocation/rebind hitch removed from ocean presentation maintenance. Static hot graph reports 75 visited methods with zero forbidden paths.

## Decision 064 - Marine Snow Cold Resource Ownership

Problem: `HectonMarineSnowRenderer.SlowTick` performed DataVault native-state repair, GraphicsBuffer/texture creation, CSV refresh, shader global reads, and external GPU binding refresh.
Solution: Add `IColdTickable`; keep `SlowTick` to a readiness check and dirty flag only. `ColdTick` owns native/DataVault repair, GPU buffer and texture repair, CSV/profile maintenance, target camera recache, shader-global snapshots, and external GPU bindings.
Rejected Alternatives: Treating `SlowTick` as a cold lane. It is still dispatcher runtime cadence and must not contain repair branches.
Scalability potential: Low tier gets deterministic marine-snow runtime cadence. Middle, High, and Ultra can scale particle capacity and propwash visuals through already prepared resources.
Hardware Impact: Rare DataVault/GPU allocation spike removed from marine-snow runtime slow lane. Static hot graph reports 111 visited methods with zero forbidden paths.

## Decision 065 - Marine Snow Tiny Job Wrapper Removal

Problem: Marine-snow visual update used six synchronous `IJob.Run()` calls for one-row or tiny bounded visual-fake writes consumed in the same frame.
Solution: Replace those calls with direct `Execute()` on the same bounded job structs. The code remains deterministic and avoids the Job System wrapper/fence surface for work that is not parallel.
Rejected Alternatives: Scheduling the jobs would require same-frame readback fences. Keeping `Run()` preserves hidden synchronous Job API cost in visual sync.
Scalability potential: Low tier avoids scheduler wrapper overhead on fake wake/silt DTOs. Middle, High, and Ultra can spend the saved budget on actual particle/propwash visual density.
Hardware Impact: Six synchronous Job API wrapper calls removed from marine-snow visual update. No profiler microseconds claimed.

## Decision 066 - Player Kinematics Cold Repair And Presentation Queue Purity

Problem: `HectonPlayerMovement.FixedTick` could create player kinematics DataVault handles, and fixed/tick feedback queues could call `TryRegisterLateFrameTickable`, which reads `GlobalRegistry.Dispatcher`.
Solution: Add `IColdTickable` for player kinematics and cinematic blackbox repair. Fixed tick and drag helpers use pure `HasPlayerKinematicsNativeState`. Presentation feedback queues only mutate pending fields through `MarkLateFramePresentationDirty`; late-frame ticking remains lifecycle-registered.
Rejected Alternatives: Allocating player kinematics buffers from fixed movement; registering presentation on demand from collision, brine, sonar, VR, audio, and bubble queues.
Scalability potential: Low tier avoids fixed-frame DataVault repair and registry reads during movement feedback bursts. Middle, High, and Ultra keep richer camera/audio/presentation queues under a stable late-frame owner.
Hardware Impact: Static hot graph reports 417 visited methods with zero forbidden paths. No build/profiler runtime microseconds claimed.

## Decision 067 - Pass 20 Compile Throttle

Problem: Verification ended with CPU load at 96 percent and active compiler processes `csc` PID 44884 and `dotnet` PID 33044.
Solution: Do not launch `dotnet build`; use source graph verification, brace balance, sync-job grep, and `git diff --check`.
Rejected Alternatives: Violating the compile throttle under active compiler load; claiming compile proof without a clean gate.
Scalability potential: Parallel agents and weak developer machines avoid compiler contention.
Hardware Impact: No build CPU consumed by this agent in Pass 20.
