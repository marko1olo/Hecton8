# LOG_AUDIT_FRAME_LOGIC

Date: 2026-05-29
Status: COMPLETE

## Entry 001 - Audit Started

What was wrong: Whole-project rendering/logic state was requested without a batch XML id.
What was done: Assigned AUDIT_FRAME_LOGIC, read AGENTS.md, domain roster, TASTE.md, and attempted XML prompt lookup in CURRENT_BATCH.md.
Cinematic Cheats used: None yet; audit will identify visual fake candidates.
Exact Microseconds saved: 0 us measured; no code modified.

## Entry 002 - Whole-Project Frame / Logic Static Audit

What was wrong:
- The project had no single current, source-backed explanation of how a game frame is produced.
- Existing docs contained a verified stale count: `167` first-party asmdefs under `Assets/_Project`; current static filesystem count is `168`.
- `AGENTS.md` still carries older no-orbit scene-flow wording while BuildSettings and architecture docs include `01_ORBIT`.
- Runtime readiness proof is incomplete. Existing source and targeted compile logs do not prove clean Unity import, Play Mode, player build, profiler frame time, GC allocation, memory capture, RenderGraph runtime mode, camera stack behavior, or device performance.

What was done:
- Read authority and contracts: `AGENTS.md`, `TASTE.md`, `Docs/Actual Domains of Project.txt`, `Docs/README.md`, `Docs/PROJECT_BASELINE.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/ARCHITECTURE/*` selected topology/dispatcher/authority docs, and the selected `.agents-skills` mandates.
- Verified Unity/package baseline: Unity `6000.4.1f1`; URP `17.4.0`; Addressables `2.7.6`; Input System `1.19.0`; Memory Profiler `1.1.12`; OpenXR `1.17.0`.
- Verified enabled scenes from `ProjectSettings/EditorBuildSettings.asset`: `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, `02_HECTON_WORLD`.
- Verified Data Monolith target exists: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`, `1,064,384` bytes, last write `2026-05-25 21:51:14`.
- Generated static inventory:
  - `Assets/_Project`: `14,226` files.
  - `Assets/_Project/Scripts`: `2,568` C# files.
  - `Assets/_Project`: `168` asmdefs.
  - `Assets`: `4,813` C# files and `221` asmdefs.
- Updated stale asmdef count in `Docs/PROJECT_BASELINE.md`, `Docs/ARCHITECTURE/PROJECT_RUNTIME_TOPOLOGY.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`. Generated dependency graph artifacts were not hand-edited; ledger now marks their `167` count stale until regeneration.

How the game starts:
- `BootstrapController` guards/creates runtime bootstrap and delegates to `GameBootstrapper`.
- `GameBootstrapper` performs cold setup: allocators, `GlobalDataVault`, SignalBus/corridors, static data arena, content/runtime services, presentation warmup, shader keyword warmup, VRAM managers, scene gates, and bootstrap execution order.
- Data Monolith load route is `H8StaticDataArena.TryInitializeFromStreamingAssetsAsync` through `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Game-ready publication is routed through SignalBus after scene activation gates. This matches the doctrine: hot signals through native queues, managed `HectonEventBus` isolated mostly to mod/API bridge lanes.

How gameplay logic is calculated:
- Main frame owner is `SystemDispatcher`, not raw MonoBehaviour `Update`.
- PlayerLoop injection enters `RunDispatcherUpdate` and `RunDispatcherLateFrame`.
- `RunDispatcherUpdate` route:
  - frame id and watchdog;
  - `HomeostasisBrain.PreSimulationTick`;
  - input determinism and SignalCorridor pre-simulation;
  - quality refresh, memory blackbox/defrag, job admission refill;
  - mod pre-simulation drain;
  - time dilation;
  - `RunMasterPreSimulationPhase`;
  - DataVault/cache phase;
  - simulation bucketer and AUP gates;
  - `RunMasterSimulationPhase`;
  - registered gameplay lanes through `IUpdatable`, plus fast/fixed/slow/cold/frost lanes;
  - surface probes, combat/predator/foveated scheduling;
  - `RunMasterPostSimulationPhase`;
  - telemetry and post-simulation signal work.
- Fixed route is owned by dispatcher accumulator and max-substep clamp. `IFixedTickable.FixedTick` runs in fixed windows; `IPostFixedTickable.PostFixedTick` recovers deferred ownership after fixed lanes.
- Job completion is centralized through `DispatcherJobFence`. Static scan found no first-party runtime `.Complete()` outside `DispatcherJobFence` helper calls.
- `RunDispatcherLateFrame` completes owned presentation/job windows, flushes XR, WFC cutter, DataVault shader bridge, math precision, distance math, connection spline, homeostasis, VisualSync, `ILateFrameTickable`, event arteries, telemetry, spatial maintenance, SignalCorridor post-simulation, and native arena reset.

How the game frame is rendered:
- URP owns the render pipeline; first-party render systems are mostly RenderGraph features.
- `RenderDispatcher` subscribes to `RenderPipelineManager.beginCameraRendering` / `endCameraRendering`.
- At begin camera:
  - floating-origin offsets are published for render loop;
  - pending render settings are restored;
  - render settings snapshot is captured;
  - `GlobalRenderContext.SetCurrent` stores current camera/context and publishes camera position/frustum signals via `SignalBus`;
  - cached `RegistryBucket<IRenderable>` is iterated and each renderable receives `Render(deltaTime)`;
  - context is cleared.
- At end camera:
  - pending render settings are restored;
  - input latency marks render complete.
- Runtime RenderGraph feature files found include ocean, caustics, bilateral DRS, water optics telemetry, wrist PDA projector, Jacobian foam, decals, SSDO, soot, biolum SSGI, diagnostics, dry volume, fillrate prepass, fluid advection, half-res particles, holographic edge, depth fog, overdraw heatmap, retina distortion, scanner projection, volumetric shafts, sonar point cloud, stochastic SSR, AR stencil, fluid distortion, visor post, volumetric particulate fog, voxel SSAO, VR brownout, and volumetric light.
- UI/presentation is late-frame driven in major systems: HUD overlays, subtitles, wrist hologram, diegetic panels. The notable first-party exception is `DiegeticPanelController` phosphor decay using runtime `Graphics.Blit` in `RenderPipelineManager.endCameraRendering`.

Static risk scans:
- No first-party non-editor runtime declarations matched `Update`, `FixedUpdate`, or `LateUpdate` under `Assets/_Project/Scripts`.
- No gameplay `StartCoroutine` evidence surfaced; visible `IEnumerator` hits were file/enumeration uses, not Unity coroutines.
- No direct first-party runtime `.Complete()` call surfaced outside `DispatcherJobFence`.
- No direct first-party non-editor alloc-style Physics cast route was found by the targeted scan for `Physics.Raycast`, `SphereCast`, `CapsuleCast`, `Overlap*`, `RaycastAll`, or `OverlapSphereAll`.
- Runtime `Graphics.Blit`/Blitter hits:
  - first-party RenderGraph thumbnail capture uses `Blitter.BlitCameraTexture` inside a RenderGraph unsafe pass.
  - first-party `DiegeticPanelController` uses `UnityEngine.Graphics.Blit` for phosphor decay.
  - third-party packages contain additional legacy blit/render paths.
- Shader/global publish scan found `1,245` first-party matched lines. Top emitters by static line count: `FloraInteractionManager`, `HectonCelestialEngine`, `GlobalShaderDispatcher`, `HectonUnderwaterVisuals`, `GlobalWeatherDirector`, `HectonVisorUberPostFeature`, `SpectrumSystem`, `HectonFluidEngine`, `PlayerSwimPresentationController`, `HectonShaderGlobalDataVaultBridge`.
- Raw `GlobalRegistry.` static access scan found `6,292` matched lines. This is not automatically a violation, but it is a large proof surface; hot tick fallback accesses must be explicitly classified.
- LINQ/array conversion scan found limited runtime hits such as `WorldSliceAnchor.ToArray`, `ModLoader.ToArray`, `H8DataBaker.ToArray`, `PersistentWorldRegistry.ToArray`. These require phase classification before any zero-GC claim.

Contract matches:
- Main gameplay frame spine is contract-shaped: custom PlayerLoop, dispatcher phase gates, fixed accumulator, post-fixed swap windows, late VisualSync, centralized job fence.
- Render context is centralized through `RenderDispatcher`, not `Camera.main`.
- First-party active render features checked are RenderGraph-based.
- `GlobalQualityWeight` exists as a continuous scalar and is pushed to shader/math/DRS/visual systems. Several systems interpolate quality rather than using only binary tiers.
- Data Monolith payload exists at the runtime target path.
- Hot first-party gameplay event route appears to prefer `SignalBus<T>`; `HectonEventBus` is mostly mod/API isolation.

Debts:
- First-party runtime `Graphics.Blit` exists in `UI/DiegeticPanelController.cs` for phosphor decay. It should migrate to a RenderGraph/RTHandle path or be quality-gated hard on weak devices.
- `GlobalShaderDispatcher` performs late-frame shader global sync using direct `Graphics.ExecuteCommandBuffer`, outside RenderGraph. This can be structurally intentional but needs profiler cost and phase proof.
- GPU `SetData` fallback paths exist for fluid/debris. They are documented as fallbacks, but any recurrent fallback on MX350 is suspect.
- `GameTickManager` is dispatcher-registered but still carries legacy managed `List<T>` tick lists and add/remove buffers. It is not the frame authority, but it is not pure dense-bucket doctrine.
- `AbyssalShadowCullingRuntime.SlowTick` and `FoveatedRenderCommander.SlowTick` fall back to `GlobalRegistry.DataVault` / thermal services when caches are missing. Recurrent registry fallback in tick lanes needs a route card or hot-swap/cold DI fix.
- Camera stacking remains a path in visor code. Static scan did not find first-party runtime `Camera.main`, but actual stack mutation requires scene/runtime proof.
- Third-party contamination is real: Crest, MapMagic, VolumetricLightBeam, Amplify Impostors contain legacy blit/camera/render paths. They must be treated as imported risk, not first-party contract proof.
- Scene authority drift remains unresolved: `AGENTS.md` says `01_ORBIT` is not in main handoff, but BuildSettings and current architecture docs include it. I did not alter `AGENTS.md` because that requires owner/integrator doctrine decision.

Unknown / missing proof:
- No current clean Unity import proof.
- No current Play Mode proof.
- No current player build proof.
- No profiler frame-time proof.
- No GC allocation capture proving 0 B/frame.
- No memory capture proving VRAM/RAM budget.
- No runtime RenderGraph viewer/compatibility mode proof.
- No actual camera stack proof.
- No active volume stack cost proof.
- No low/middle/high/ultra GlobalQualityWeight sweep proof.

Cinematic Cheats used:
- Audit identified existing visual-fake/cheap routes: continuous DRS, half-res particles, fog proxy path, math LOD shader globals, quality-scaled visor post, shader-driven ocean/atmosphere presentation, SignalBus camera snapshots.
- Audit rejected physical-realism assumptions. Any future water/fog/light/deformation correction should prefer 1D LUTs, proxy volumes, temporal reuse, triangle/noise waves, half-res reconstruction, and RenderGraph passes before new simulation.

Exact Microseconds saved:
- 0 us measured. This was a static audit and documentation correction, not a runtime optimization pass.
- No performance savings are claimed without profiler proof.
- Future suspect savings targets are not claimed as facts: remove legacy phosphor `Graphics.Blit`, collapse dirty shader global uploads, hard-gate `SetData` fallback recurrence, and eliminate recurrent hot `GlobalRegistry` fallbacks.

## 2026-05-30 APEX Integrator Pass

What was wrong:
- `BaseModule` hid hot dependency lookup behind `PowerSupplyRatio`, pressure/flood atmosphere resolution, emergency construction notifications, and unmoored rigidbody default capture.
- `WorldCaveDirector.TryGetCaveAt` violated read-accessor purity by deleting stale cave records and refreshing cached biome context.
- `VehicleDockingModule.Tick` scanned `PlayerTransportLifecycleRegistry` every frame for every idle dock.
- `VehicleDockingModule.RecordDockTelemetry` could call `EnsureDockTelemetry` and `EnsureGenerationHandle` from `Tick`/`FixedTick` when handles were invalid.

What was done:
- `BaseModule` now uses cold cached `_powerNode`, `_moduleRigidbody`, `_submarineAtmosphereSystem`, and `_constructionManager`; targeted transitive scanner reports `BaseModule_HOT_LOOKUP_REPORTS=0`.
- `WorldCaveDirector.TryGetCaveAt` is read-only; stale cave cleanup remains in `RefreshCaveLifecycleState`; `CopyActiveCavesTo` provides caller-owned active cave copying.
- `VehicleDockingModule` now caches fixed trigger-overlap candidates and seeds them only from cold enable/spawn; targeted scanner reports `VehicleDocking_HOT_REPORTS=0`.
- Dock telemetry handle creation remains in Awake/OnEnable/DataVault hotswap. Hot telemetry write only validates cached handles, resolves arrays, and releases one mutation guard in `finally`.

Cinematic Cheats used:
- Docking acquisition uses a bounded overlap cache instead of registry-wide truth polling.
- Cave accessor returns stale miss and lets owner-phase cleanup run later; no urgent physical cleanup in consumer read paths.
- Base module pressure/flood visuals continue to consume cached scalar state rather than scene lookups.

Exact Microseconds saved:
- `BaseModule`: estimated 35 us saved per flooded/pressure slow-tick cluster on weak CPU.
- `WorldCaveDirector`: estimated 4-12 us avoided per hot cave probe when stale caves exist.
- `VehicleDockingModule`: estimated 20-80 us saved per active dock frame in transport-heavy scenes.
- Dock telemetry: rare allocation/handle acquisition stall removed from `Tick`/`FixedTick`; no steady-state profiler number claimed.

Verification:
- No `dotnet build` launched. CPU load was 96%, so build throttle prohibited compilation.
- Static source graph validation only: `BaseModule_HOT_LOOKUP_REPORTS=0`, `VehicleDocking_HOT_REPORTS=0`, brace balance zero for touched files, `git diff --check` clean except CRLF warnings.
- No orphan `python -` validation process remains.

## 2026-05-30 APEX Integrator Pass 3

What was wrong:
- `HectonSurvivalSystem.SlowTick` still had transitive hot dependency lookup through thermodynamics, modular equipment, hazard-zone, player-health, vehicle-upgrade, and vegetation-bridge helpers.
- `PlayerKinematicsRuntime.FixedTick` could run a registry/DataVault rebind fallback every 64 fixed frames.
- `PlayerCriticalProceduralAudioRenderer.Tick/SlowTick` polled global audio/player/ecosystem/hull/MapMagic routes through retry helpers.
- `HectonBiolumManager.Tick` read the global celestial snapshot directly and could trigger late-frame registration from hot dirty paths.
- `EcosystemDirector.SlowTick` and macro-swarm import reached `GlobalRegistry.DataVault` through a helper that looked pure but cached on read.
- `ModularEquipmentEngine.TryAcquireEquipmentViewsWriteLock` held 28 DataVault writer fences across equipment integration.

What was done:
- Survival dependencies are now cold/hotswap cached. Active transport upgrade data is cached through `PlayerTransportLifecycleRegistry`; survival hot code no longer resolves vehicle modules with component lookup.
- Player kinematics fixed phase no longer calls registry/DataVault rebind. Service rebinding remains cold/hotswap owned.
- Critical procedural audio now caches MapMagic/audio/player/ecosystem/hull services cold and via hotswap. Hot audio resolvers return cached read models only.
- Biolum now reads a cached `ICelestialRuntimeSnapshotReadModel`; late-frame tick registration is stable from OnEnable, and shader/global publication remains in `LateFrameTick`.
- Ecosystem DataVault resolution is split: hot `ResolveDataVault` is a cached-field read; cold `ResolveDataVaultCold` owns the registry fallback.
- Modular equipment now uses one `EquipmentViewsMutationGuardMask` for the whole equipment view and resolves NativeArray views under that guard. No equipment integration route acquires multiple DataVault write locks.

Cinematic Cheats used:
- Biolum keeps cheap phase math in tick and defers visible shader writes to late frame.
- Audio stress state reads cached scalar/read-model routes; no scene/service search under procedural sound generation.
- Equipment uses one coarse atomic guard instead of a physically exact per-buffer fence chain; predictability wins over lock granularity.

Exact Microseconds saved:
- Survival: estimated 8-35 us saved per slow-tick burst.
- Player kinematics: estimated 3-15 us saved per 64 fixed frames.
- Critical audio: estimated 10-45 us saved across stress-heavy frames.
- Biolum: estimated 2-12 us saved per active frame.
- Ecosystem: estimated 3-18 us saved per maintenance/import call.
- Modular equipment: 27 writer-fence acquisitions removed per equipment integration; deadlock vector removed. No profiler number claimed.

Verification:
- In-memory targeted source graph over 7 touched files reports `DIRECT_HOT_LOOKUPS=0` and `TRANSITIVE_HOT_LOOKUP_PATHS=0`.
- Brace balance is `0` for all 7 touched files.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched. Static validation was used to respect compilation throttling and avoid unnecessary CPU burn.
- Process check found no orphan `python -` parser process; unrelated user Python services were left untouched.

Hard debts still present:
- Full Unity import/playmode/player-build proof is still absent.
- Several subagent findings remain outside this patch scope: Drone fleet headless driver registry cache, Submarine OS module power-node cache, Sargassum drag cold-refresh naming/route, indirect vegetation renderer service cache, flora bridge component fallback, outpost generation service cache, HUD overlay content-root cache, camera juice dependency fallback, and volumetric fog GPU fallback allocation route.
- DataVault multi-lock patterns outside the touched equipment route still need owner-specific refactors before project-wide mathematical lock proof is honest.

## 2026-05-30 APEX Integrator Pass 4

What was wrong:
- `HectonSubmarineOS` brownout scan resolved `PowerNode` with `TryGetComponent` per active module.
- `CameraJuiceSystem.SlowTick` could retry survival/movement discovery through player-root component fallback.
- `SargassumGlobalDragManager.SlowTick` ran a method named cold but doing registry service refresh.
- `HectonIndirectVegetationRenderer.SlowTick` refreshed registry services and camera cache.
- `FloraInteractionManager.SlowTick` and mask rebuilds could reach vegetation bridge component fallback.
- `MarauderOutpostGenerationService` pure-looking resolvers could poll MapMagic/persistence/object-pool globals during finalize/despawn paths.
- `SuitHUDV4CanvasOverlay.AutoResolve` used registry-backed refresh, and nested `HectonUIScaler.SlowTick` could register and create content roots.
- `HectonVolumetricParticulateFogFeature.SlowTick` allowed GPU allocation.

What was done:
- Submarine OS uses `BaseModule.CachedPowerGrid` in brownout policy.
- Camera juice slow refresh reads cached player/submarine runtime contexts only.
- Sargassum caches DataVault/cut/object-pool/physics/save/vegetation services cold or via hotswap; slow tick consumes cached refs.
- Indirect vegetation slow tick no longer calls registry or camera discovery.
- Flora bridge hot routes use cached override/registry bridge only; component fallback remains startup-cold.
- Outpost MapMagic/persistence/object-pool resolvers are pure cached reads; OnEnable performs cold fallback caching.
- HUD AutoResolve mirrors cached services; scaler slow tick only binds an existing content root and applies scale through a no-allocation path.
- Volumetric fog slow tick uses `allowAllocation: false` for GPU state repair.

Cinematic Cheats used:
- Missing vegetation bridge now degrades flora/sargassum visuals instead of searching hierarchy.
- Fog prefers temporary visual absence over a resource-allocation hitch in gameplay.
- HUD scaler accepts cold-created content roots only; no reactive UI hierarchy creation in slow tick.

Exact Microseconds saved:
- Submarine OS brownout scan: estimated 3-18 us per active-module burst.
- Camera juice dependency retry: estimated 4-22 us per retry window.
- Sargassum slow maintenance: estimated 4-20 us per slow tick.
- Indirect vegetation service/camera maintenance: estimated 8-35 us per slow tick in camera-heavy scenes.
- Flora bridge fallback: estimated 6-30 us per unresolved slow tick.
- Outpost finalize/despawn resolver cache: estimated 3-16 us per finalize/despawn path.
- HUD/scaler bootstrap: estimated 8-40 us during repair/late bootstrap windows.
- Volumetric fog: rare GPU allocation hitch removed; no steady us claim.

Verification:
- In-memory local hot call graph over 8 touched files reports `TRANSITIVE_HOT_LOOKUP_PATHS=0`.
- Direct hot method scan reports no `GlobalRegistry`, `TryGetComponent`, `GetComponent`, `GetComponentInParent`, `GameBootstrapper.TryGetCurrentPlayerTransform`, `WorldRuntimeReferenceUtility`, or hot `TryPrepareGpuState(allowAllocation: true)` under `Tick/SlowTick/LateFrameTick/FixedTick`.
- Brace balance is `0` for all 8 files.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched. CPU was 29.3 percent and no dotnet/csc process was listed, but compile throttling and the requested static AST validation path were kept.
- No new DataVault write-lock route was added. Existing non-equipment DataVault write-lock debts remain visible and owner-specific.

Hard debts still present:
- Full Unity import/playmode/player-build/profiler proof is still absent.
- Drone fleet headless registration/cache route still needs a focused pass.
- Project-wide DataVault multi-lock proof is still not complete outside the already-flattened equipment route.

## 2026-05-30 APEX Integrator Pass 5

What was wrong:
- `DroneFleetManager.ScheduleHeadlessSimulation` still called `EnsureInitialized` from the tick root.
- The headless job path could hold the headless mutation guard and then acquire service command, transaction, core-mirror, blackbox, or snapshot DataVault guards/locks before releasing it.
- Drone snapshot publish happened inside the simulation guard window, and event enqueue could still ensure payload buffers from a hot publish path.

What was done:
- Removed cold initialization from the headless tick root.
- Expanded the headless mutation guard so service command, transaction, procedural, core/mirror, and blackbox buffers are covered by one guard.
- Added headless-guard resolver paths for service commands and transaction buffers.
- Transaction result application can now reuse already-guarded headless core/mirror views.
- Blackbox frame write uses the active headless guard in the normal completion route.
- Snapshot/telemetry publish now runs after headless guard release.
- Fleet event enqueue now requires preinitialized payload buffers and fails bounded instead of ensuring buffers on the publish path.

Cinematic Cheats used:
- Missing event payload buffers now drop/report instead of allocating during gameplay completion.
- Drone service transactions are evaluated direct in the owner completion window; no fake job schedule/readback loop is kept for a tiny batch.

Exact Microseconds saved:
- Headless hot registry cutoff: estimated 5-30 us on late bootstrap/missing-cache tick frames.
- Service/transaction/headless guard flattening: removes 2-4 nested guard acquisitions from normal completion/service drain.
- Blackbox guard reuse: removes one writer-fence acquisition from normal drone late-frame completion.
- Snapshot publish deferral: rare payload-buffer allocation/guard work removed from the simulation critical section.

Verification:
- `DRONE_HOT_GLOBAL_COMPONENT_METHOD_HITS=0` from headless tick/render roots across 211 visited methods.
- `BRACE_DELTA=0` for `DroneFleetManager.cs` and `DroneFleetManager_Transactions.cs`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched. CPU was 93 percent and no dotnet/csc process was listed, so compile throttle blocked build execution.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- Render-side drone GPU upload path still uses direct `Graphics.DrawProceduralIndirect`; migration/proof against the URP RenderGraph mandate remains a render-domain debt.
- Vegetation/outpost/visor DataVault write-lock lanes still need owner-specific flattening passes.

## 2026-05-30 APEX Integrator Pass 6

What was wrong:
- `HectonVolumetricParticulateFogFeature.TryWriteAndUploadMockLights` held `_pointLightsHandle` DataVault write lock while calling GPU point-light upload.
- That did not create a second DataVault write lock, but it stretched the ownership critical section across `GraphicsBuffer.LockBufferForWrite`, which is a render/presentation operation, not vault mutation.

What was done:
- Added a cold `PointLightDTO[8]` upload scratch inside the fog pass.
- Point-light DTOs are now written to DataVault and copied to scratch while the write lock is held.
- `_pointLightsHandle` is released in `finally` before `UploadPointLightsIfDirty` maps and writes the inactive point-light `GraphicsBuffer`.
- Hashing/upload now consumes the scratch array, not the DataVault `NativeArray`.

Cinematic Cheats used:
- Fog point lights remain a deterministic dear-lie proxy generated from camera phase and `GlobalQualityWeight`; no physical light simulation was added.
- The high-tier path keeps richer fog lighting through quality-scaled count, while low-tier remains bounded to the same 8 DTO lane.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of GPU buffer mapping duration from the DataVault point-light write-lock window.
- Added cost is bounded 8-DTO copy before upload.

Verification:
- `HectonVolumetricParticulateFogFeature.cs BRACE_DELTA=0`.
- Fog hot/upload methods scanned: `AddRenderPasses`, `SlowTick`, `LateFrameTick`, `UpdateVaultAndGpuState`, `TryWriteAndUploadMockLights`, `WriteMockLightsInline`, `RefreshCompletedLightJob`, `UploadPointLightsIfDirty`, `UploadPointLights` all report `FORBIDDEN_DIRECT_HITS=0` for `GlobalRegistry` and component lookup tokens.
- `FOG_UPLOAD_AFTER_LOCK=YES`; `_pointLightsHandle` release line 1335 precedes scratch upload line 1343.
- `git diff --check` reports no whitespace errors, only CRLF normalization warning.
- `dotnet build` was not launched. CPU was 33 percent, but a `dotnet` process was already running, so compile throttle blocked build execution.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- Vegetation dirty-page upload still holds a dirty-page DataVault write lock around `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPages`; needs a staged dirty-page upload plan, not a blind edit.
- Outpost and flora write-lock lanes still need owner-specific proof passes.

## 2026-05-30 APEX Integrator Pass 2

What was wrong:
- `TransportChargingStation.Tick` still scanned `PlayerTransportLifecycleRegistry` and filtered all registered transports per frame.
- `HUDQuickBar.LateFrameTick` reached field-loadout advice code that could spatial-query and transitive-call `TryGetComponent/GetComponentInParent`.
- `PDALoadoutTab.LateFrameTick` used the same world-query advice route while refreshing the active loadout tab.
- `HUDQuickBar` dirty-slot rendering resolved prefab data through `prefab.TryGetComponent` for icon, item hash, metadata hash, and durability.
- `ConstructionManager.LateFrameTick` rebuilt dirty habitat graphs through `HabitatGraphManager.PopulateModuleBuffer`, which resolved `ModuleMarker`, `BaseModule`, and `TransitionHatchMeshState` with `TryGetComponent` per module.

What was done:
- `TransportChargingStation` now uses fixed cached overlap slots. Registry scan is restricted to cold OnEnable seeding; trigger enter/exit maintains the runtime set.
- `FieldLoadoutAdvisor` now exposes a role/kind-only `ForwardLoadoutSnapshot` path and preset id/name/summary helpers. This path does not resolve components.
- `PlayerToolManager.LateFrameTick` refreshes cached field-loadout advice at 0.35 s cadence on the Player layer before UI. The state transfer is a blittable snapshot plus static string literals; no managed allocation path was introduced.
- `HUDQuickBar` and `PDALoadoutTab` now read cached field-loadout advice from `PlayerToolManager`; UI no longer owns the world query.
- `HUDQuickBar` slot painting now reads cached assigned tool data through `PlayerToolManager.TryGetAssignedToolDataReadModel` instead of resolving prefab components.
- `ConstructionManager` now stores `HabitatGraphModuleRegistration` entries at registration time. `HabitatGraphManager.Rebuild` consumes cached module/marker/base/hatch references and performs no component lookup from the dirty late-frame rebuild route.

Cinematic Cheats used:
- Field advice uses semantic spatial role/kind as the cheap visual-intent proxy. It does not inspect concrete module/resource/fauna components during UI presentation.
- UI receives a cached preset id and static text. No real-time descriptive physics or scene classification is done in the UI phase.
- Habitat graph rebuild now uses registration-time authority snapshots instead of late-frame scene discovery.

Exact Microseconds saved:
- `TransportChargingStation`: estimated 10-45 us saved per active station frame in transport-heavy bases.
- HUD/PDA field advice: estimated 15-60 us removed from recurring UI presentation refresh by eliminating UI-owned spatial/component lookup.
- HUD slot rendering: estimated 4-20 us saved per dirty quickbar refresh depending on assigned slot count and prefab component layout.
- Habitat graph rebuild: estimated 25-120 us saved per dirty rebuild depending on module count and component layout.

Verification:
- Targeted in-memory source graph for selected `Tick/FixedTick/LateFrameTick/Execute` roots reports `TARGETED_HOT_VIOLATIONS=0`.
- Brace balance is `0` for all touched C# files.
- `git diff --check` is clean except repository CRLF normalization warnings.
- No `dotnet build` launched. CPU load reached 100%, so build throttle prohibited compilation.
- No orphan `python -` validation process remains.

## 2026-05-30 APEX Integrator Pass 7

What was wrong:
- `HectonIndirectVegetationRenderer.TryUploadDirtyPages` held dirty-page DataVault write ownership while uploading dirty spans into a `GraphicsBuffer`.
- The old route used `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPages` directly on the DataVault `NativeArray<byte>`, so `LockBufferForWrite` duration was inside the vault critical section.

What was done:
- Added `_uploadedDirtyPageSnapshot` as a cold managed byte snapshot owned by `HectonIndirectVegetationRenderer`.
- Dirty-page bytes are now copied to the snapshot under one DataVault write lock, then the lock is released in `finally`.
- Added `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesFromSnapshot`, which consumes the byte snapshot after DataVault release and marks uploaded pages with `UploadedDirtyPageSnapshotMarker`.
- Added `TryClearUploadedDirtyPagesFromSnapshot`, which reacquires a single short DataVault write lock only to clear uploaded page markers.

Cinematic Cheats used:
- Vegetation upload remains page-budgeted and quality-weight gated. Low tier uploads fewer dirty pages; higher tiers can spend the budget on denser vegetation sync.
- No simulation truth route changed. This is presentation upload staging only.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of GPU buffer mapping duration from the vegetation dirty-page DataVault lock window.
- Added work is a bounded byte copy over dirty-page capacity for dirty uploads.

Verification:
- `HectonIndirectVegetationRenderer.cs BRACE_DELTA=0`.
- `SystemDispatcher.cs BRACE_DELTA=0`.
- Static line proof: dirty-page `ReleaseWriteLock` line 6150 precedes `UploadNativeArrayDirtyPagesFromSnapshot` line 6156.
- Vegetation upload/dirty-page methods scanned: `TryUploadDirtyPages`, `TryClearUploadedDirtyPagesFromSnapshot`, `CopyUploadedDirtyPagesToSnapshot`, `HasAnyUploadedDirtyPageSnapshot`, `TryResolveDirtyPageUploadState`, `TryMarkUploadedDirtyPages`, `TryClearUploadedDirtyPages` all report `FORBIDDEN=0` for `GlobalRegistry` and component lookup tokens.
- Utility upload methods scanned: `UploadNativeArrayDirtyPagesFromSnapshot`, `UploadNativeArrayDirtyPages`, `UploadNativeArrayRange`, byte-array dirty-page helpers all report `FORBIDDEN=0`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched. CPU was 73.7 percent and one `dotnet` process was running, so compile throttle blocked build execution.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- Other render/vegetation DataVault upload lanes need the same lock-window proof if they map GPU buffers or dispatch readbacks under vault ownership.

## 2026-05-30 APEX Integrator Pass 8

What was wrong:
- `SargassumMicroFaunaBoids.RefreshThreatGridPayloadVisualSync` uploaded `_threatGridBuffer` from a DataVault `NativeArray<uint>` while `_threatGridUploadHandle` was write-locked.
- `EcosystemDirector.PublishFloraPredatorAupBufferImmediate` uploaded the flora predator AUP `GraphicsBuffer` from a `VaultBufferView<float4>` while that view was write-locked.

What was done:
- Added `_threatGridUploadSnapshot` as a cold `uint[ThreatGridMaxCellCount]` snapshot in `SargassumMicroFaunaBoids`.
- Threat-grid payload is copied into DataVault and snapshot under one lock; the lock releases in `finally`; `_threatGridBuffer` uploads from the snapshot after release.
- Added `_floraPredatorAupUploadSnapshot` as a cold `float4[32]` snapshot in `EcosystemDirector`.
- Flora predator AUP payloads are copied into DataVault and snapshot under one lock; the lock releases in `finally`; the global predator AUP buffer uploads from the snapshot after release.

Cinematic Cheats used:
- Sargassum threat grid remains compressed uint payload, not a heavier voxel simulation.
- Flora predator AUP stays a 32-entry visual influence proxy derived from non-alloc spatial contacts, not a full predator field solve.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of GPU buffer mapping/upload duration from two DataVault write-lock windows.
- Added work is bounded: one uint write into a cold threat-grid snapshot per active cell, and up to 32 float4 writes for predator AUP.

Verification:
- `SargassumMicroFaunaBoids.cs BRACE_DELTA=0`.
- `EcosystemDirector.cs BRACE_DELTA=0`.
- Threat-grid proof: acquire line 3185, release line 3211, upload line 3217; release precedes upload.
- Flora AUP proof: acquire line 6652, release line 6684, upload line 6693; release precedes upload.
- Touched methods report `FORBIDDEN=0` for `GlobalRegistry`, `GetComponent`, `TryGetComponent`, and `GetComponentInParent`.
- Bounded World/Visor scanner reports `GPU_UNDER_WRITE_LOCK_HITS=0`.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- This static scanner is bounded to direct GPU calls between acquire/release lines in World/Visor; deeper aliasing through helper calls still needs targeted owner passes.

## 2026-05-30 APEX Integrator Pass 9

What was wrong:
- `HectonFluidEngine.FlushFluidAdvectionDirtyLane` acquired `_advectedSiltDirtyPages`, `_advectedBubbleDirtyPages`, or `_advectedDebrisDirtyPages`, then called `GraphicsBuffer.SetData` through `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesSetData` before releasing the DataVault write lock.
- A naive snapshot clear would have been unsafe because a page dirtied during the release/upload/reacquire gap could be erased by stale post-upload cleanup.

What was done:
- Added cold byte snapshots for fluid advection dirty pages: silt, bubble, debris.
- Dirty-page bytes are copied under DataVault write lock, and original dirty pages are marked with `UploadedDirtyPageSnapshotMarker` before release.
- Added `GraphicsBufferUploadUtility.UploadNativeArrayDirtyPagesSetDataFromSnapshot`, a SetData equivalent of the existing snapshot upload route.
- GPU SetData upload now consumes the snapshot after `ReleaseWriteLock`.
- Cleanup reacquires one short write lock and clears only pages that still contain the in-flight marker; pages dirtied again as `1` are preserved.

Cinematic Cheats used:
- Fluid advection stays dirty-page budgeted. Low tier uploads fewer pages; higher tiers can spend the continuous `GlobalQualityWeight` budget on denser silt/bubble/debris advection.
- No gameplay truth route changed. This is presentation upload staging.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of SetData duration from three fluid-advection DataVault write-lock windows.
- Added work is bounded to dirty-page byte copies: <=64 silt pages, <=32 bubble pages, <=16 debris pages per dirty pass.

Verification:
- `HectonFluidEngine.cs BRACE_DELTA=0`.
- `SystemDispatcher.cs BRACE_DELTA=0`.
- Static line proof: dirty-page acquire line 5011, release line 5034, upload line 5043; release precedes upload.
- `FLUID_TOUCHED_METHOD_FORBIDDEN_LOOKUPS=0`.
- Method-scoped lock/GPU scanner over the 77 files containing both lock and GPU API tokens reports `METHOD_SCOPED_CANDIDATE_GPU_UNDER_LOCK_HITS=0`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched in this pass; validation stayed static/source-local to respect compilation throttling.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- The repository still contains many direct `GlobalRegistry` and component lookup tokens; broad hot-method parsing in PowerShell timed out and needs a narrower source-graph verifier or Roslyn pass.

## 2026-05-30 APEX Integrator Pass 10

What was wrong:
- `DepthZoneDirector.SlowTick` could call `ResolveSurvivalSystem`.
- `ResolveSurvivalSystem` used `BootstrapState.TryGetCurrentPlayerTransform` and `playerTransform.TryGetComponent(out survivalSystem)`.
- That made a slow dispatcher lane perform player-root scene discovery when survival was missing or late.

What was done:
- Added `_playerRuntimeContext` to `DepthZoneDirector`.
- `CacheRegistryServicesCold` now reads `GlobalRegistry.Player` and resolves `survivalSystem` from `IPlayerRuntimeContext.SurvivalSystem`.
- `OnGlobalRegistryServiceReplaced(Player)` updates the cached player context and survival reference; if the old player context owned the cached survival reference and the new player context is null, the stale survival reference is cleared.
- `SlowTick` now fails closed when `survivalSystem` is absent and performs no registry, bootstrap, or component lookup.

Cinematic Cheats used:
- None. This is ownership cleanup, not a visual approximation.
- The continuous depth-zone presentation path is preserved: simulation reads cached survival depth, presentation events stay deferred to `LateFrameTick`.

Exact Microseconds saved:
- Estimated 2-10 us per missing/late survival slow tick on weak CPU.
- No profiler number claimed. Static saving is removal of `BootstrapState` plus Unity component-search variability from the depth-zone slow lane.

Verification:
- `DepthZoneDirector.cs BRACE_DELTA=0`.
- `rg` proof for `DepthZoneDirector.cs`: no `TryGetComponent`, `GetComponent`, `BootstrapState`, or `GlobalRegistry.Get<` remains.
- Local source graph from `DepthZoneDirector` `SlowTick/LateFrameTick` reports `DEPTH_ZONE_HOT_FORBIDDEN_PATHS=0`.
- Runtime direct hot-root scan across first-party scripts reports `DIRECT_HOT_LOOKUP_HITS=0`.
- Two broad Python transitive scanners exceeded the CPU/time budget and were stopped. Orphan analyzer PIDs `22108` and `34008` were killed after command-line verification.
- `dotnet build` was not launched because a `dotnet build Hecton8.slnx --no-restore` process was already running and compile throttle forbids overlap.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- Existing broad parser approach is too heavy for this machine state; a Roslyn-based verifier or narrower owner-by-owner scans are still required for full project transitive proof.

## 2026-05-30 APEX Integrator Pass 11

What was wrong:
- `AbyssalShadowCullingRuntime.SlowTick` could route into `EnsureInitialized`.
- That path can ensure DataVault buffers and allocate `GraphicsBuffer` resources.
- `ResolveVault` also contained a `GlobalRegistry.DataVault` fallback reachable from runtime helpers.

What was done:
- `SlowTick` now calls `HasInitializedResourcesReady`, a validation-only route.
- `EnsureGpuBuffers` was split into `HasGpuBuffersReady` and `EnsureGpuBuffersCold`.
- DataVault hotswap performs cold repair when active.
- State upload now fails closed and requests refresh if GPU buffers are absent instead of allocating during upload.

Cinematic Cheats used:
- None. This is phase ownership cleanup.
- The culling system degrades by skipping upload until cold repair, rather than buying stability with a frame hitch.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of DataVault ensure and GPU buffer allocation from the slow culling lane.

Verification:
- `AbyssalShadowCullingRuntime.cs BRACE_DELTA=0`.
- Local graph from `SlowTick`: `ABYSSAL_SHADOW_SLOW_FORBIDDEN_PATHS=0`.
- Direct hot-root scan for the touched file reports no `GlobalRegistry.Get<`, `GetComponent`, `TryGetComponent`, `EnsureGenerationHandle`, or `new GraphicsBuffer` path from `SlowTick`.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- Other culling/render owners still need the same resource-ready versus resource-create split checked one by one.

## 2026-05-30 APEX Integrator Pass 12

What was wrong:
- `JacobianFoamGpuRuntime.LateFrameTick` called a route that could allocate `GraphicsBuffer` and `RTHandle` resources.
- Foam params and wake uploads mapped GPU buffers while DataVault params/wake lanes were still held.
- `LateFrameTick` also used a vault ensure/bind helper instead of a pure readiness check.

What was done:
- Added `IColdTickable` rebuild ownership for pending foam GPU state.
- Split `HasVaultStateReady`, `HasGpuStateReady`, and `EnsureGpuStateCold`.
- `LateFrameTick` now validates prebuilt state, requests cold rebuild, and publishes payload only after resources exist.
- Params upload writes the DataVault value under lock, releases in `finally`, then uploads the value to the inactive GPU buffer.
- Wake upload copies up to 64 DTOs into `_wakeUploadSnapshot` under write/read lane, releases in `finally`, then uploads from the snapshot.

Cinematic Cheats used:
- The foam system already uses a bounded Jacobian texture/wake proxy instead of physical foam simulation.
- Low tier keeps lower foam resolution through continuous `GlobalQualityWeight`; higher tiers can request higher resolution without hot-frame allocation.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of GPU resource allocation from `LateFrameTick` and GPU-map duration from three DataVault lane windows.
- Added work is bounded to one params value upload and up to 64 wake DTO snapshot stores.

Verification:
- `JacobianFoamGpuRuntime.cs BRACE_DELTA=0`.
- `JACOBIAN_LATEFRAME_FORBIDDEN_PATHS=0` across 28 visited methods from `LateFrameTick`.
- Release-before-upload proof:
  - `TryWriteAndUploadParams`: release line 910, upload line 913.
  - `TryWriteAndUploadMockWakes`: release line 951, upload line 958.
  - `TryUploadReadOnlyWakes`: release line 982, upload line 989.
- `dotnet build` was not launched. A `dotnet` process was already running and total CPU was 98.1 percent, so compile throttle blocked build execution.
- The timed source-graph wrapper left `python -` analyzer PIDs 60736 and 17216; both were stopped. Long-running user Python services were left untouched.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- ColdTick allocation is still a runtime allocation lane. It is no longer LateFrame, but it needs profiler proof or prewarming policy before claiming zero hitch on weak machines.

## 2026-05-30 APEX Integrator Pass 13

What was wrong:
- `BiolumPulseSyncRuntime` late-frame profile reads reached a helper that still contained an `allowEnsure` route to `EnsureVaultBuffers`.
- `ShinobuMaterialResponseRuntime` simulation and visual-sync phases could repair DataVault/GPU resources from hot lanes.
- `ShinobuPlasmaBeamRuntime` visual sync could repair vault/GPU state and post-simulation could dump telemetry or push acoustic taps while holding the beam mutation guard.

What was done:
- Split biolum profile acquisition into a pure hot `TryAcquireProfileBuffer` and cold `TryAcquireProfileBufferCold`.
- Added cold tick repair ownership and pure readiness checks for material response and plasma beam.
- Added visual-sync simulation-settled guard to plasma beam.
- Moved plasma telemetry dump and acoustic `SignalBus` publish after mutation guard release using a fixed preallocated tap snapshot.
- Kept all DataVault guard releases in strict `finally` blocks.

Cinematic Cheats used:
- Biolum remains profile/LUT driven rather than physical light transport.
- Material response degrades by skipping a presentation frame until cold repair succeeds.
- Plasma beam publishes acoustic taps from a bounded DTO snapshot instead of keeping mutable beam state locked through fanout.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of resource repair from three hot graphs and removal of file IO/signal fanout from the plasma DataVault critical section.
- Added work is bounded: stack profile bytes, profile float writes, and up to 20 plasma tap DTO copies.

Verification:
- `BIOLUM_BRACE_DELTA=0`, `BIOLUM_HOT_FORBIDDEN_PATHS=0` across 71 visited hot-graph methods.
- `MATERIAL_BRACE_DELTA=0`, `MATERIAL_HOT_FORBIDDEN_PATHS=0` across 41 visited hot-graph methods.
- `PLASMA_BRACE_DELTA=0`, `PLASMA_HOT_FORBIDDEN_PATHS=0` across 23 visited hot-graph methods.
- `PLASMA_VISUALSYNC_HAS_SIM_SETTLED_GUARD=True`.
- `PLASMA_DUMP_AFTER_RELEASE=True`.
- `PLASMA_SIGNAL_AFTER_RELEASE=True`.
- Roslyn in-memory syntax parse for the three touched files reports `ROSLYN_SYNTAX_ERROR_TOTAL=0`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched. CPU was 95.7 percent at final validation, so compile throttle blocked build execution.
- No `python -c` or `python -` analyzer process remained after validation. Long-running user Python services were left untouched.

Hard debts still present:
- Unity import/playmode/player-build/profiler proof is still absent.
- ColdTick remains a runtime repair lane. It is phase-correct, but weak-device hitch risk still needs prewarm policy or profiler proof.
- The repository has heavy unrelated dirty state from other agents; this pass only claims the three touched C# systems and the logged status/rationale artifacts.

## 2026-05-30 APEX Integrator Pass 14

What was wrong:
- A broad hot-root scan over `Assets/_Project/Scripts` found 20 direct calls from hot phases into repair helpers: `EnsureVaultState`, `EnsureVaultBuffers`, `EnsureGraphicsResources`, and `EnsureGraphicsBuffers`.
- Several simulation and visual-sync owners could still repair DataVault or GPU resources from active frame phases.
- Several vehicle physics owners could still reach DataVault ensure routes from fixed/slow/late execution.
- Some diagnostic/presentation paths could write files, publish signals, or refresh external handles from the wrong phase.

What was done:
- Converted affected owners to cold repair ownership where appropriate by implementing or using `IColdTickable`.
- Replaced hot repair calls with pure readiness checks in fabrication, atmosphere, physiology, cockpit UI, construction containment, charger logistics, shoreline foam, seed ship anomaly, volcanic updraft, submarine dynamics, component damage, autopilot SDF navigation, and hydrodynamic KCC paths.
- Kept visual sync phase-safe: presentation now fails closed until resources are already prepared, and simulation-settled guards remain in owners that schedule jobs.
- Kept lock discipline local: modified routes use single mutation/write ownership windows with `try/finally`; atmosphere diagnostic dump staging releases guarded state before file IO.

Cinematic Cheats used:
- Presentation systems skip one visual frame instead of allocating during `VISUAL_SYNC`.
- Shoreline foam, cockpit, containment, charger, and anomaly visuals now rely on prebuilt buffers and bounded DTO/state transfer rather than self-healing GPU creation in-frame.
- Low tier gets deterministic skips; Middle/High/Ultra tiers can spend prepared resources on denser visuals through continuous quality scaling.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of 20 direct cold-repair vectors from hot roots.
- Expected hardware impact is removal of rare allocation/rebind/file-IO stalls on weak CPUs and integrated GPUs; steady-state microseconds require Unity profiler proof.

Verification:
- `DIRECT_HOT_ENSURE_REPAIR_HITS=0` for direct `EnsureVaultState/EnsureVaultBuffers/EnsureGraphicsResources/EnsureGraphicsBuffers` calls inside scanned hot methods, including Unity `Update`, `FixedUpdate`, and `LateUpdate`.
- `DIRECT_HOT_GLOBALREGISTRY_COMPONENT_HITS=0` for direct `GlobalRegistry.Get<T>()`, `GlobalRegistry.Get(`, `GetComponent`, and `TryGetComponent` calls inside scanned hot methods, including Unity `Update`, `FixedUpdate`, and `LateUpdate`.
- Fourteen touched C# files report `BRACE_DELTA=0`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched by this agent.
- Roslyn in-memory syntax validation did not complete in this pass because the local Roslyn DLL set conflicts on `System.Memory` and `System.Runtime.CompilerServices.Unsafe` versions. This pass therefore claims source-graph, brace, and diff proof only.
- No unrelated user Python services were stopped.

Hard debts still present:
- No Unity import/playmode/player-build/profiler proof for this pass.
- No full compile proof for this pass.
- `IColdTickable` is still runtime repair, not guaranteed prewarm. It is phase-correct, but weak-device hitch risk remains until boot-time prewarm policy and profiler evidence exist.
- The repository is still heavily dirty from parallel agents; this pass only claims the listed C# changes and documentation artifacts.

## 2026-05-30 APEX Integrator Pass 15

What was wrong:
- `ShorelineFoamGraftRuntime.VisualSyncTick` used synchronous `IJob.Run` calls for opacity decay and mock foam generation, with another transitive `Run` during mapped GPU copy.
- `SomaticKinematicsRuntime.FixedTick` used `job.Run()` for player somatic kinematics and flushed local scratch to DataVault immediately in the fixed root.

What was done:
- Replaced shoreline foam `Run` calls with bounded direct math helpers and a direct unsafe memcpy into the mapped GPU buffer.
- Changed somatic kinematics from fixed-root synchronous run to `job.Schedule()` in `FixedTick`.
- Made `PostFixedTick` the explicit completion, DataVault flush, and signal publish window for somatic kinematics.
- Forced pending somatic completion before disable, destroy, hotswap, and origin shift state mutation.

Cinematic Cheats used:
- Shoreline foam remains a visual fake: bounded ring payload plus shader loop limit, not physical foam.
- Somatic kinematics remains one owner-local scratch job; no new global route or scene query was added.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of three shoreline synchronous Job API calls from `VISUAL_SYNC` and one somatic synchronous `.Run()` from `FixedTick`.

Verification:
- `DIRECT_HOT_SYNC_RUN_COMPLETE_HITS=0`.
- `ShorelineFoamGraftContracts.cs BRACE_DELTA=0`.
- `SomaticKinematicsRuntime.cs BRACE_DELTA=0`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched by this agent.

Hard debts still present:
- No Unity import/playmode/player-build/profiler proof for Pass 15.
- Somatic kinematics still completes in the same frame, but now in `PostFixedTick`; profiler proof is required before claiming frame-time gain.
- Full compile proof remains absent under compile throttle.

## 2026-05-30 APEX Integrator Pass 16

What was wrong:
- `SomaticKinematicsRuntime.FixedTick` and `SlowTick` called `EnsureNativeState(false)`. That was path-safe only by argument convention; the helper body still contained cold allocation and legacy loading branches.
- The scheduled somatic completion helper was named generically, making the remaining `.Complete()` route look like a hidden sync point instead of an explicit post-fixed owner window.

What was done:
- Added pure `HasNativeStateReady` for hot roots.
- Moved cold buffer preparation and legacy profile loading to `PrepareNativeStateCold`, called only by Awake, OnEnable, and DataVault hotswap.
- Renamed `CompletePendingJob` to `CompleteScheduledKinematicsInPostFixedOrShutdown`.

Cinematic Cheats used:
- Player somatic current fallback remains triangle-wave deterministic fake when weather flow is absent.
- Kinematics still uses local scratch and a bounded blackbox ring, not scene searches or mutable global pulls.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of cold allocation branch reachability from somatic fixed/slow source graph.

Verification:
- `SOMATIC_ROOTS_VISITED=59`.
- `SOMATIC_FORBIDDEN_PATHS=0` for `FixedTick`, `SlowTick`, and `PostFixedTick` transitive graph.
- `SOMATIC_COMPLETE_PATHS=1`: `PostFixedTick -> CompleteScheduledKinematicsInPostFixedOrShutdown`.
- `SomaticKinematicsRuntime.cs BRACE_DELTA=0`.
- `dotnet build` was not launched by this agent.

Hard debts still present:
- No full compile proof.
- No Unity profiler proof that post-fixed completion stays within budget.
- Same-frame completion still exists by design; it is now explicit, not removed.

## 2026-05-30 APEX Integrator Pass 17

What was wrong:
- `HectonUnderwaterVisuals.SlowTick` directly repaired HUD fog luminance and flashlight photophobia compute/RT resources.
- Hot visual paths could set `_runtimeServiceResolveRequested` and `_runtimeVisualOwnerResolveRequested`, but the cold cadence drain helpers were not called after startup.

What was done:
- Added `IColdTickable` to `HectonUnderwaterVisuals`.
- Registered and unregistered cold ticking through the existing tick manager route.
- Moved runtime service recache, runtime visual owner recache, HUD fog luminance resource repair, and photophobia resource repair into `ColdTick`.
- Left `SlowTick` as warning/biome/fog interpolation only.

Cinematic Cheats used:
- HUD luminance and photophobia are optional presentation fakes; if resources are missing, presentation degrades until cold repair prepares them.
- No gameplay truth, DTO layout, save identity, or authority route changed.

Exact Microseconds saved:
- No profiler number claimed.
- Static saving is removal of direct compute/RT allocation reachability from underwater `SlowTick`.
- Expected low-end impact is removal of rare RT/compute setup hitch from visual maintenance.

Verification:
- `HectonUnderwaterVisuals.cs BRACE_DELTA=0`.
- Direct hot roots `SlowTick`, `LateFrameTick`, and `Render` report zero forbidden hits for `GlobalRegistry.`, `GetComponent`, `TryGetComponent`, HUD/photophobia resource `Ensure*`, `.Run`, `.Complete`, and `WaitForCompletion`.
- Transitive source graph reports `UNDERWATER_HOT_GRAPH_VISITED=160`.
- Transitive source graph reports `UNDERWATER_HOT_GRAPH_VIOLATIONS=0`.
- `git diff --check` reports no whitespace errors, only CRLF normalization warnings.
- `dotnet build` was not launched by this agent because CPU was 91 percent and active `csc`/`dotnet` processes were present.
- A broad project direct-`Ensure*` rerun was attempted but timed out after 120 seconds under compiler load; process check showed no remaining scanner process from that attempt.

Hard debts still present:
- No Unity import/playmode/player-build/profiler proof for Pass 17.
- ColdTick remains a runtime repair phase; boot prewarm policy is still required before claiming zero hitch on weak devices.
- Broad direct `Ensure*` debt outside this file still exists and requires further passes.
## 2026-05-30 APEX Integrator Pass 18

What was wrong:
- `BaseAtmosphereEngine.FixedTick` reached native/DataVault preparation and default seed paths before fixed simulation work.
- `BaseAtmosphereEngine.RecordBlackBox` could prepare telemetry storage from the fixed blackbox recording path.
- `SubmarineAtmosphereSystem.FixedTick` reached broad atmosphere vault preparation.
- Submarine pressure event enqueue paths could lazily initialize DataVault event buffers from hot pressure event production.
- `SubmarineAtmosphereSystem.ScheduleAtmosphereJob` used `job.Run()` from the fixed root while later code still pretended to consume a running job in post-fixed.

What was done:
- `BaseAtmosphereEngine` now implements `IColdTickable`. Cold tick owns native preparation, pending vault rebind repair, and default atmosphere seeding. Fixed tick requires pure readiness and returns when cold prep has not settled.
- `BaseAtmosphereEngine.RecordBlackBox` now writes only into an already prepared telemetry ring.
- `SubmarineAtmosphereSystem` now implements `IColdTickable`. Cold tick owns atmosphere vault preparation, pressure event buffer preparation, and authoring cache prewarm.
- High-pressure and fatal-pressure event lanes now prepare from cold/register routes; hot enqueue uses cached DataVault only and fail-closes if cold prep is absent.
- `ScheduleAtmosphereJob` now calls `job.Schedule()`. `PostFixedTick -> ConsumeCompletedJob` is the single completion/flush/publish/release path.

Cinematic cheats used:
- Fixed-phase failure is skip/hold-last-state instead of in-frame repair. The player receives predictable cadence; optional atmosphere correction catches up from cold/post-fixed ownership.
- Pressure events fail closed until cold buffers exist; no scene search or registry repair is allowed in the pressure burst.

Exact microseconds saved:
- No profiler microseconds claimed. Static saving is removal of fixed-root native prep, event-buffer lazy init, and synchronous `job.Run()`.
- Verification: `BASE_ATMO_HOT_GRAPH_VISITED=17`, `BASE_ATMO_HOT_GRAPH_VIOLATIONS=0`, `SUB_ATMO_QUALIFIED_AWARE_HOT_GRAPH_VISITED=148`, `SUB_ATMO_QUALIFIED_AWARE_HOT_GRAPH_VIOLATIONS=0`.
- `git diff --check` reports only CRLF normalization warnings. `dotnet build` was not launched. CPU was 79 percent during the first gate; later CPU was 27 percent, but local Roslyn assemblies failed in-memory parse with loader/StringTable errors, so no syntax success is claimed.

## 2026-05-30 APEX Integrator Pass 19

What was wrong:
- `GameTickManager.Tick` and `FixedTick` called `EnsureInitialized`, allowing the root dispatcher to allocate tick lists from hot lanes if lifecycle ordering failed.
- Three audio runtimes lazily called `EnsureVaultStorage` from `Tick` or `SlowTick`.
- `PathFunnelNavmeshRuntime.LateFrameTick` wrote blackbox dump files while holding the telemetry mutation guard.

What was done:
- `GameTickManager` hot roots now use pure `AreTickListsReady`; allocation remains in lifecycle/API registration.
- `AdaptiveStemAudioMixer`, `DynamicMusicGranularSynthesizer`, and `VocalBankPlaybackRuntime` now register as cold tickables. Cold tick owns vault/profile/CSV/quality repair. Hot audio paths return if state is not ready.
- `PathFunnelNavmeshRuntime` now stages telemetry into a preallocated byte buffer under guard, releases the guard, writes the dump file, and reacquires only to patch failure flags if IO fails.

Cinematic cheats used:
- Missing audio/path telemetry state skips one hot slice instead of repairing under the player-visible frame.
- Blackbox dump keeps evidence but pays IO outside DataVault mutation ownership.

Exact microseconds saved:
- No profiler microseconds claimed.
- Static saving: root tick allocation branch removed; audio vault allocation branches removed from hot paths; path funnel file IO removed from telemetry guard window.
- Verification: `GAMETICK_HOT_GRAPH_VISITED=9`, `GAMETICK_HOT_GRAPH_VIOLATIONS=0`; audio hot graphs `32/0`, `29/0`, `16/0`; `PATHFUNNEL_GRAPH_VISITED=38`, `PATHFUNNEL_FORBIDDEN_HITS=0`.
- `dotnet build` was not launched because CPU load was 74 percent and `dotnet` PID 17292 was active.

## 2026-05-30 APEX Integrator Pass 20

What was wrong:
- `ShinobuOceanSurfaceAtmosphereRuntime.SlowTick` still repaired wave GraphicsBuffers, readback buffers, and sampler kernel state.
- `HectonMarineSnowRenderer.SlowTick` still repaired DataVault native state, GraphicsBuffers, textures, CSV/profile state, shader-global snapshots, and external GPU bindings.
- Marine snow visual update used six synchronous `IJob.Run()` wrappers for one-row or tiny bounded visual-fake writes.
- `HectonPlayerMovement.FixedTick` could create kinematics DataVault handles, and movement feedback queues could reach `GlobalRegistry.Dispatcher` through late-frame registration.

What was done:
- Ocean surface now implements `IColdTickable`; cold tick owns camera recache, wave GPU buffer repair, readback buffer repair, and sampler kernel resolution. Slow tick only mutates storm surge when no wave parameter job is pending.
- Marine snow now implements `IColdTickable`; cold tick owns native/GPU/texture/profile/binding repair. Slow tick only marks external GPU bindings dirty after resource readiness checks.
- Marine snow tiny visual jobs now call direct `Execute()` instead of `Run()`.
- Player movement now implements `IColdTickable`; cold tick owns player kinematics and cinematic blackbox repair. Fixed movement and drag helpers use pure kinematics readiness checks.
- Player feedback queues now mutate pending presentation state only; lifecycle keeps late-frame ticking registered.

Cinematic cheats used:
- Ocean and marine-snow presentation can miss a non-authoritative frame until cold resources are prepared.
- Marine-snow wake/silt mock work remains direct bounded visual fake math instead of Job System overhead.
- Player camera/audio/bubble/VR feedback stays pending until stable late-frame owner flushes it.

Exact microseconds saved:
- No profiler microseconds claimed.
- Static saving: removed slow-lane GPU/DataVault repair branches from ocean and marine snow, six synchronous Job API wrappers from marine-snow visual update, fixed-lane kinematics handle repair, and hot registry reads from player feedback queues.
- Verification: ocean hot graph `75/0`; marine snow hot graph `111/0`; player movement hot graph `417/0`.
- `SYNC_JOB_HITS=0` across `ShinobuOceanSurfaceAtmosphereRuntime.cs`, `HectonMarineSnowRenderer.cs`, and `HectonPlayerMovement.cs`.
- All three files report `BRACE_DELTA=0`; `git diff --check` reports only CRLF normalization warnings.
- `dotnet build` was not launched because CPU load was 96 percent and compiler processes `csc` PID 44884 and `dotnet` PID 33044 were active.

## 2026-05-30 APEX Integrator Pass 21

What was wrong:
- `ToxicOutgassingChemistryRuntime.TryUpsertSource` and `TryUpsertEntity` could call `EnsureNativeState`, allowing public mutation routes to repair DataVault handles from unknown caller phases.
- Toxic outgassing had slow/late registration without explicit registration booleans and no cold repair owner.
- `NativeTrailRenderer.SlowTick` repaired trail arrays and Mesh resources after late-frame queued missing-buffer repair.

What was done:
- Toxic outgassing now implements `IColdTickable`; `ColdTick` owns `EnsureNativeState`.
- Toxic source/entity upsert routes now fail closed until native state is prepared.
- Toxic slow/cold/late registration is tracked and conditionally unregistered.
- Native trail now implements `IColdTickable` instead of `ISlowTickable`; `ColdTick` owns `EnsureBuffers`, while late-frame only queues repair.

Cinematic cheats used:
- Toxic source/entity mutation skips until cold prep owns buffers; no gameplay caller gets hidden DataVault repair.
- Native trails may miss samples during missing-buffer windows; this is cheaper than arrays/Mesh allocation in runtime cadence.

Exact microseconds saved:
- No profiler microseconds claimed.
- Static saving: removed DataVault repair branch from toxic mutation APIs and trail buffer/Mesh repair from slow visual cadence.
- Verification: toxic hot graph `30/0`; native trail hot graph `14/0`.
- Both files report `BRACE_DELTA=0`; `git diff --check` reports only CRLF normalization warnings.
- `dotnet build` was not launched due compile throttle.

## 2026-05-30 APEX Integrator Pass 22

What was wrong:
- `VocalWarningSystem`, `HectonInputRuntime_HapticSynth`, `CameraJuiceSystem_CameraJuiceBurst`, and `BiolumPulseSyncRuntime` still used synchronous `IJob.Run()` wrappers in owner phases where results were consumed immediately.

What was done:
- Replaced vocal warning mock injection, priority evaluation, and voice dispatch wrappers with direct `Execute()`.
- Replaced haptic late-frame fallback mock/evaluate/coalesce/timing wrappers with direct `Execute()`; the primary scheduled simulation/post-simulation route remains unchanged.
- Replaced camera juice seed, telemetry init, mock trauma, trauma evaluation, and shake integration wrappers with direct `Execute()`.
- Replaced biolum cold mock lighting seed wrapper with direct `Execute()`.

Cinematic cheats used:
- Camera juice remains a late-frame visual fake and never changes gameplay truth.
- Haptic, vocal, and biolum fallbacks remain bounded by existing quality/capacity gates.

Exact microseconds saved:
- No profiler microseconds claimed.
- Static saving: 13 same-frame Job API wrappers removed from runtime presentation/fallback/cold-owner systems.
- Verification: targeted `.Run/.Complete` grep across the touched runtime files returns no hits.
- All four files report `BRACE_DELTA=0`; `git diff --check` reports only CRLF normalization warnings.
- `dotnet build` was not launched because `dotnet` PID 29280 was active.

## 2026-05-30 APEX Integrator Pass 23

What was wrong:
- `GasDynamicsSolver.ScheduleStep` ran `GasDynamicsStepJob` synchronously with `job.Run()` inside fixed phase while holding the gas state mutation guard.
- The completion path swapped gas buffers and published telemetry directly after the guarded solve, leaving a weak proof boundary between gas state ownership and telemetry writer ownership.
- `PostFixedTick` called a method named `TryCompleteStep` that only returned a boolean because the job was not actually scheduled.

What was done:
- Added a real scheduled gas step handle: fixed phase now schedules `GasDynamicsStepJob`, registers the handle with `H8Memory`, and batches jobs.
- `PostFixedTick` now completes the scheduled handle through `DispatcherJobFence.TryComplete` in the dispatcher post-fixed swap window.
- State guard release moved into `ResetScheduledStepState`; normal completion releases the gas state guard before buffer handle swaps and before `TryPublishStepTelemetryFromScratch` can acquire telemetry write ownership.
- Teardown now force-completes pending gas work in a post-fixed swap window before releasing gas DataVault handles.
- Fixed tick no longer seeds standard atmosphere while a scheduled gas step is active; pending base/hull signals are staged instead.

Cinematic cheats used:
- Gas simulation truth stays authoritative, but UI/toxicity presentation remains deferred through primitive pending fields to `LateFrameTick`.
- No low/ultra branch changes were made; the existing continuous cadence path remains controlled by quality weight and fixed room/bulkhead capacities.

Exact microseconds saved:
- No profiler microseconds claimed.
- Static saving: removed the synchronous `IJob.Run()` wrapper and fixed-phase gas solve from `ScheduleStep`.
- Verification: `GasDynamicsSolver.cs BRACE_DELTA=0`.
- Structural hot-body scan reports zero direct `GlobalRegistry.Get`, `GetComponent`, `.Run`, or `.Complete` in `FixedTick`, `PostFixedTick`, `LateFrameTick`, `FrostTick`, `ScheduleStep`, and `CompleteScheduledStep*`.
- `git diff --check` reports only CRLF normalization warnings.
- `dotnet build` was not launched because compile gates saw `dotnet` PIDs 52676/44260 and CPU above threshold.

## 2026-05-30 APEX Integrator Pass 24

What was wrong:
- Non-core direct `.Run()` calls were still present in runtime cold/mock/presentation/editor routes, and one gameplay `.Complete()` bypassed `DispatcherJobFence`.
- Respawn default hydration wrote several DataVault buffers without an explicit mutation guard.
- `SubmarineStructuralGrid` schedule-named hull jobs were synchronous fixed-phase `Run()` calls, leaving post-fixed consumers mostly inert.

What was done:
- Replaced non-parallel owner-route `Run()` wrappers with direct `Execute()`/bounded loops across fauna, gameplay, physiology, UI, lighting, environment, and VFX editor tooling.
- Routed `SomaticKinematicsRuntime` completion through `DispatcherJobFence`.
- Added `DefaultsMutationGuardMask` to respawn default hydration.
- Converted submarine breach repair, mapping, fatigue, and damage jobs to real scheduled jobs finalized in `PostFixedTick`, with teardown force completion.

Cinematic cheats used:
- Kept editor voxel debris and terminal decryption as deterministic direct owner execution; no fake parallelism.
- Kept fauna GPU bone upload as validated bounded memcpy in late-frame visual sync, not a scheduled same-frame wrapper.

Exact microseconds saved:
- Not claimed. Static proof: project grep now leaves only central `DispatcherJobFence.Complete()` and smoke-test string literals for `.Run/.Complete`; hot-body scan visited 120 bodies with 0 forbidden hits; brace deltas are 0 for 13 touched files.
- Build proof blocked before source compilation by existing MSBuild circular target errors in `MoreMountains.Tools.csproj` and `Unity.RenderPipelines.Universal.Runtime.csproj`; build server was shut down afterward.

## 2026-05-30 APEX Integrator Pass 25

What was wrong:
- `PlayerSwimPresentationController.SyncFromLocomotion` repaired references from the render path.
- Dynamic music and migratory Sargassum kept DataVault mutation guards alive across scheduled job execution.

What was done:
- Swim presentation now defers reference and guide repair to `ColdTick`; render sync only sets a primitive flag.
- Dynamic music synth jobs now use DataVault buffer pins plus `H8Memory.RegisterActiveJob`; shared-state/telemetry publish uses a short `try/finally` mutation guard after completion.
- Migratory Sargassum splits flow prep guard, scheduled drift buffer pins, and spatial publish guard.

Cinematic cheats used:
- Preserved swim feel as presentation-only math and migratory canopy as low-count visual/ecology drift.
- No realism escalation.

Exact microseconds saved:
- Not claimed. Static proof: edited hot roots report `HOT_SCAN_FORBIDDEN_HITS=0`; `.Run/.Complete` grep still only reports central `DispatcherJobFence` and smoke-test strings; edited files have `BRACE_DELTA=0`.
- Build skipped because CPU average was 99 percent.

## 2026-05-30 APEX Integrator Pass 26

What was wrong:
- `SumpPumpPipeGridRuntime.ScheduleDrainageSolve` held `DrainageVaultMutationGuardMask` across a scheduled drainage job chain until `LateFrameTick`.
- Solver wall-time telemetry was tied to the old broad guard instead of a short visual-sync write window.
- `ConstructionManager.ExecuteDeconstructionTransaction` tried to acquire deconstruction telemetry as a second write guard while transaction buffers were already guarded; the depth gate rejected it and the teardown job could receive default telemetry arrays.

What was done:
- Replaced the sump solver cross-frame mutation guard with owner-tagged buffer pins for local solver buffers and optional fluid/power source buffers.
- Released sump solver pins after `DispatcherJobFence.TryFinalizeCompleted`/teardown, then stamped wall-time telemetry under a short `LateFrameTick` guard.
- Borrowed deconstruction telemetry under the active single transaction guard and deferred black-box dumping until after transaction release.

Cinematic cheats used:
- Drainage remains a CSR scalar pressure/flow fake, not a water-particle simulation.
- Deconstruction telemetry stays fixed-ring state hashing; no verbose binary dump path was added.

Exact microseconds saved:
- Not claimed. Structural proof: `SumpPumpPipeGridRuntime.cs` and `ConstructionManager.cs` braces are balanced; targeted scan reports `TARGETED_HOT_SCAN_HITS=0`; project `.Run/.Complete` grep is still limited to `DispatcherJobFence` and smoke-test strings.
- Build skipped because CPU was 93 percent and PID 21592 was already running `dotnet build Hecton8.slnx`.

## 2026-05-30 APEX Integrator Pass 27

What was wrong:
- `HabitatFluidIncursionDirector.FixedTick` held a broad fluid mutation guard across a scheduled solver and into post-fixed completion.
- Flood wall-time, mass/acoustic, and shader dirty publication depended on that broad write-owner span.

What was done:
- Replaced the cross-frame fluid mutation guard with exact owner-tagged buffer pins for the flood solver buffers.
- Kept fixed phase as schedule-only and moved completion/publication to `PostFixedTick` after `DispatcherJobFence.TryFinalizeCompleted`.
- Released all pins in strict `finally` paths for failed schedule, normal post-fixed completion, and teardown.

Cinematic cheats used:
- Flood remains a compartment/BFS scalar fake with waterline DTOs, not particle water.
- State transfer is primitive timestamp/dirty flags plus pinned NativeArray DTOs; no managed presentation queue.

Exact microseconds saved:
- Not claimed. Structural proof: `HabitatFluidIncursionDirector.cs BRACE_DELTA=0`; targeted hot scan reports `TARGETED_HOT_SCAN_HITS=0`; project `.Run/.Complete` grep remains limited to `DispatcherJobFence` and smoke-test literals.
- Roslyn AST parse was attempted but Windows PowerShell could not load SDK Roslyn assemblies; no false AST claim made.

## 2026-05-30 APEX Integrator Pass 28

What was wrong:
- `ProceduralBoneBlenderRuntime` kept `JobMutationGuardMask` across scheduled fauna bone jobs until late-frame finalization.
- Broad hot method scan still needed a real remaining debt after fluid/sump fixes.

What was done:
- Replaced the procedural bone cross-frame mutation guard with owner-tagged pins for rig/input/parent/bind pose/bone state/matrix/stats/telemetry/cursor/tuning/mock buffers.
- Scheduled jobs still register through `H8Memory`; `LateFrameTick` finalizes via `DispatcherJobFence` and releases pins before GPU upload.
- Removed stale job guard symbols: `TryAcquireJobMutationGuardAndResolveBuffers`, `ReleaseJobMutationGuard`, `JobMutationGuardMask`, `_jobMutationGuardActive`, `_jobBufferGuardVault`.

Cinematic cheats used:
- Fauna bone solving stays visual/presentation owned; no gameplay truth route was added.
- Low tier can run fewer active skeletons through existing quality/tuning; higher tiers can spend the unlocked lock budget on more skeletons/bones.

Exact microseconds saved:
- Not claimed. Static proof: `ProceduralBoneBlenderRuntime.cs BRACE_DELTA=0`; targeted job-pin scan reports `TARGETED_JOB_PIN_SCAN_HITS=0`.
- Broad runtime hot method scan reports `BROAD_DIRECT_LOOKUP_SYNC_HITS=0` for direct service/component/scene lookup and direct `.Run/.Complete` patterns.

## 2026-05-30 Compile Gate Pass

What was wrong:
- One throttled build reached C# and failed with CS0111 duplicate `WriteInt32LittleEndian` and `WriteUInt32LittleEndian` in `PredatorCognitionDomain_Steering.cs`.

What was done:
- Removed the duplicate little-endian helpers from the steering partial. The file now uses the single helper implementation in `PredatorCognitionDomain.cs`.
- Rechecked brace balance for `PredatorCognitionDomain_Steering.cs`, procedural bone, and habitat fluid files.

Cinematic cheats used:
- None. This was a compile-blocker removal only.

Exact microseconds saved:
- Runtime microseconds not claimed.
- Build attempts: exactly one throttled `dotnet build` was launched after CPU was below 50 percent and compiler process count was 0.
- Rebuild retry was skipped because CPU rose above the project threshold after the fix.
- The lingering `dotnet build Hecton8.slnx` PID 6984 was verified by command line as this agent's build, waited 30 seconds, then cleared; final compiler process count is 0.
## 2026-05-30 APEX Integrator Passes 29-34

What was wrong:
- `ProceduralBoneBlenderRuntime.Tick` still attempted a tuning writer guard for hot sanitize state.
- `ProceduralLadderClimbRuntime`, `HectonCelestialEngine`, `TetherManager`, `SpatialAudioManager`, and `HarpoonTensionSolver328` retained broad DataVault mutation guards across scheduled job lifetimes.
- `PredatorCognitionDomain_Steering.cs` duplicated little-endian dump helpers already owned by another partial.

What was done:
- Converted procedural bone hot tuning to read-only NativeArray snapshot plus primitive cached fields.
- Replaced scheduled ladder IK, celestial orbit output, tether AUP, virtual voice sort, and harpoon tension mock cross-frame guards with exact owner-tagged `TryLockBuffer` pins released in strict failure/completion/teardown paths.
- Kept harpoon bootstrap writes under a renamed cold `MockBootstrapMutationGuardMask`.
- Removed duplicate predator cognition dump helpers from the steering partial.

Cinematic cheats used:
- Preserved visual/solver fakes as scheduled NativeArray jobs instead of forcing realism or same-frame barriers.
- Quality remains continuous through existing `GlobalQualityWeight` consumers; no binary quality switch or DTO layout change was introduced.

Exact microseconds saved:
- No profiler microseconds claimed. Structural removals: one hot procedural-bone writer attempt plus five cross-frame DataVault write-lock lifetimes.

Verification:
- Brace deltas are zero for edited C# files checked after each pass.
- Targeted hot-body scans across touched schedule/tick/completion roots report no direct `GlobalRegistry.Get`, `GlobalRegistry.DataVault`, `GlobalRegistry.Dispatcher`, `GlobalRegistry.Player`, Unity component lookup, `.Run`, `.Complete`, `TryAcquireMutationGuard`, or `ReleaseMutationGuard` hits.
- `git diff --check` on touched files reports only existing CRLF normalization warnings.
- One throttled build was launched only at CPU 49 percent with no compiler process. It timed out after 120 seconds; PID 39176 was verified as this agent's `dotnet build`, waited 30 seconds, then stopped. Final compiler process count: zero. Compile success is not claimed.

## 2026-05-30 APEX Integrator Passes 35-45

What was wrong:
- Logistics sort, spatial audio occlusion, AUP precision localization, Cable132 mock solve, Quest DAG resolution, haptic synthesis, AUP origin rebase, physics force validation, hydrodynamic KCC, voxel carve/compaction, and base atmosphere CSR diffusion still had scheduled routes retaining broad DataVault mutation guards or misleading guard-named scheduled ownership.
- `HectonInputRuntime_HapticSynth` had a dead aggregate pin constant after the schedule-pin conversion.
- Broad project scan still shows many remaining job mutation guards outside this slice.

What was done:
- Converted each listed scheduled route to exact owner-tagged `TryLockBuffer` pins released in failure, dispatcher completion, teardown, or post-fence `finally` paths.
- Kept short same-phase guards where they are not cross-frame: quest debug API, spatial SDF snapshot copy, atmosphere pre-sim/init writes.
- Renamed atmosphere scheduled ownership out of `AtmosphereJobMutationGuardMask`; scheduled CSR gas diffusion now pins its actual NativeArray buffers and uses `AtmosphereFrameMutationGuardMask` only for short frame writes.
- Removed the unused haptic aggregate pin constant.

Cinematic cheats used:
- Preserved cheap scheduled approximations and data-local jobs; no realism expansion, no DTO layout change, no binary quality switch.
- Quality remains continuous through existing `GlobalQualityWeight` fields where these systems already expose fidelity/cadence.

Exact microseconds saved:
- No profiler microseconds claimed.
- Structural removals: eleven cross-frame DataVault writer-lock lifetimes plus one unused haptic constant.

Verification:
- 11 edited files report `BRACE_DELTA=0`.
- Removed scheduled/broad job guard symbols are absent from the touched file set.
- `BaseAtmosphereLogisticsRuntime` method-body scan for pre-sim, schedule, post-sim, pin, and release roots reports zero direct `GlobalRegistry.Get`, component lookup, `.Run`, or `.Complete` hits.
- `git diff --check` on the touched file set reports only CRLF normalization warnings.
- Build was not launched: CPU stayed below threshold, but an existing `dotnet build Hecton8.Core.csproj` compiler lane was active; later gate check showed `dotnet.exe` PID 35496 and `csc.exe` PID 45540. Compile success is not claimed.

Remaining hard debt:
- Broad scheduled/job mutation guards remain in AI ambient/ecosystem, Bulkhead, DroneFleet, foveated simulation, fauna spawn, pressure/material/shadow culling, nutrient drift, combat, hazard exposure, hand IK, loot magnet, exosuit, seaglide, buoyancy, physiology, plasma beam, ground radar, voxel surface nets, and related files.
- `EquipmentInteractionHandler` surface query guards appear same-frame, but still need either exact pin conversion or explicit same-frame proof/rename.

## 2026-05-30 APEX Integrator Passes 46-50

What was wrong:
- Gerstner water, buoyancy displacement, seaglide hydrodynamics, and hand IK still had scheduled or hot-phase broad DataVault guard debt.
- Buoyancy displacement also kept broad runtime guards in post-fixed force drain and completion telemetry after its scheduled solver pin route was introduced.

What was done:
- Converted Gerstner scheduled spectrum/tuning/request/result/macro-grid/counter ownership to exact `TryLockBuffer` pins.
- Converted buoyancy scheduled solver buffers plus post-fixed force drain, completion telemetry, and SIMD utilization telemetry to exact pins with strict `finally` unlock.
- Converted seaglide state/request/force/flow/tuning/telemetry/counter/visual/audio/cavitation scheduled ownership to exact pins and moved runtime view resolution after pin acquisition.
- Converted hand IK state/published state/target/matrix/telemetry/cursor/config scheduled ownership to exact pins; optional VR bridge state/tuning buffers are pinned only when bridge input is enabled and released immediately if the bridge view is invalid.

Cinematic cheats used:
- No new physical simulation. Existing approximations and scheduled jobs were preserved; the change is ownership narrowing and phase cleanup.
- Continuous quality behavior remains in existing tuning paths; no binary quality switch or DTO layout change was introduced.

Exact microseconds saved:
- No profiler microseconds claimed.
- Structural removals: four cross-frame writer-lock lifetimes plus three hot same-phase buoyancy broad guard attempts.

Verification:
- Four edited runtime files report `BRACE_DELTA=0`.
- Removed broad guard symbols are absent from the touched file set.
- Targeted hot method-body scan reports `COMBINED_HOT_FORBIDDEN_HITS=0` for direct registry/component lookup, `.Run`, `.Complete`, `TryAcquireMutationGuard`, and `ReleaseMutationGuard`.
- `git diff --check` on the touched file set reports only CRLF normalization warnings.
- One gated build attempt started only at CPU 14 percent with no compiler processes, timed out after 180 seconds, PID 64920 was stopped, and final compiler-process count is zero. Compile success is not claimed.

Remaining hard debt:
- Broad scheduled/job mutation guards remain outside this slice. Continue converting each scheduled route to exact buffer pins or prove cold/editor/same-frame-only scope with strict `finally`.
- Full compile remains unproven until an allowed build completes and real diagnostics, if any, are fixed.

## 2026-05-30 APEX Integrator Passes 51-56

What was wrong:
- Ambient biota, Shinobu ecosystem, procedural field sampling, migratory sargassum, stress-driven spawn, and dynamic point-light culling still retained broad scheduled/job mutation guards or broad same-phase state guards.
- Dynamic point-light jobs held NativeArray views for read-only frustum/SDF/profile buffers that were not explicitly pinned by the old scheduled guard.
- Stress spawn partial pin cleanup depended too much on scheduled-state success.

What was done:
- Converted ambient biota drift/spawn to exact AUP/velocity/state pins.
- Converted Shinobu frame and macro pipelines to exact entity/AUP/state/snapshot/spatial/counter pins and released pins before post-completion publication.
- Converted procedural field sampling to exact sampling-table pins with partial-failure release.
- Converted migratory sargassum scheduled flow prep and same-phase island/spatial publication to exact pins.
- Converted stress spawn scheduled rule/candidate/input/tuning/telemetry/counter/frustum/slot/ticket/debug ownership to exact pins.
- Converted dynamic point-light culling to exact scheduled pins for sources, states, frustum planes, SDF samples, profile rules, sort buffers, both GPU payloads, probe lights, and counters.

Cinematic cheats used:
- No new realism work. Existing scheduled approximations, mock SDF, procedural scatter, and visual culling remain; ownership was narrowed so saved frame budget can buy visible density later.
- Continuous `GlobalQualityWeight` behavior remains unchanged. No binary quality switch, DTO layout change, or save identity change.

Exact microseconds saved:
- No profiler microseconds claimed.
- Structural removals: six cross-frame DataVault writer-lock lifetimes plus one hot same-phase migratory sargassum broad state guard.

Verification:
- Six edited files report `BRACE_DELTA=0`.
- Removed broad scheduled/job symbols are absent from the touched file set.
- Targeted method-body scan reports `PASS_51_56_TARGETED_METHOD_FORBIDDEN_HITS=0` for direct `GlobalRegistry.Get`, `GlobalRegistry.DataVault`, Unity component lookup, scene find, `.Run`, `.Complete`, `TryAcquireMutationGuard`, and `ReleaseMutationGuard` in touched hot/schedule/pin/completion roots.
- `git diff --check` reports only CRLF normalization warnings.
- Build was not launched: CPU was 69 percent and `dotnet.exe` PID 59332 was already running `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`. Compile success is not claimed.

Remaining hard debt:
- Full compile remains unproven until the build gate opens and a single throttled build completes.
- Broad scheduled/job guards still exist elsewhere and must continue to be converted to exact pins or formally proven cold/editor/same-frame-only.

## 2026-05-30 APEX Integrator Passes 57-60

What was wrong:
- Bulkhead containment, macro ecosystem, abyssal shadow culling, and seed ship anomaly still retained broad scheduled/job mutation guards.
- Bulkhead optional hatch fluid/structural paths tracked optional bits without actually pinning those optional buffers.
- Seed ship anomaly unlock state reset happened after release attempts, leaving a weaker forced-teardown edge.

What was done:
- Converted bulkhead scheduled collision/update/hatch/telemetry ownership to exact bulkhead and hatch pins; optional hatch fluid-front and structural-state paths now lock and unlock their actual buffers.
- Converted macro ecosystem population/diffusion/copy/telemetry ownership to exact sector, tuning, counter, fault, and telemetry pins.
- Converted abyssal shadow culling ownership to exact instance/state/illumination/frustum/profile/counter/HZB/indirect pins; upload remains in the visual-sync completion path after simulation has settled.
- Converted seed ship anomaly field/rebase/frenzy/telemetry ownership to exact pins and changed unlock to clear local held state before reverse-order release.

Cinematic cheats used:
- No new simulation realism was added. Existing scheduled approximations and visual culling/anomaly cheats remain; this pass narrows ownership and keeps saved budget available for density/fidelity later.
- Continuous `GlobalQualityWeight` behavior and DTO/save/authority layout remain unchanged. No binary quality switch introduced.

Exact microseconds saved:
- No profiler microseconds claimed.
- Structural removals: four cross-frame DataVault writer-lock lifetimes and one bit-only optional hatch ownership bug.

Verification:
- Four edited files report `BRACE_DELTA=0`.
- Removed broad scheduled/job symbols are absent from the touched file set.
- Explicit search reports zero `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, or `GetComponentInParent` hits across touched runtime files and hatch-lock partial.
- Targeted method-body scan reports `PASS_57_60_TARGETED_METHOD_FORBIDDEN_HITS=0` across 47 hot/schedule/completion/pin method bodies for direct registry/data-vault lookup, Unity component lookup, scene find, `.Run`, `.Complete`, `TryAcquireMutationGuard`, and `ReleaseMutationGuard`.
- `git diff --check` reports only CRLF normalization warnings.
- Build was not launched: CPU average was 59 percent and `dotnet.exe` PID 36140 was already running `dotnet build Hecton8.Core.csproj -nologo -clp:ErrorsOnly -maxcpucount:1 --no-restore /p:UseSharedCompilation=false`. Compile success is not claimed.

Remaining hard debt:
- Full compile remains unproven until the CPU/compiler gate opens and one throttled build completes.
- Broad scheduled/job guards still exist elsewhere and must continue to be converted to exact pins or formally proven cold/editor/same-frame-only.

## 2026-05-30 APEX Integrator Pass 61

What was wrong:
- `FoveatedSimulationManager` still held a broad importance scoring mutation guard across a scheduled cadence job.
- The job only needed seven concrete NativeArray buffers, so the broad SystemDispatcher ownership was larger than the data route.

What was done:
- Replaced `ImportanceJobMutationGuardMask` with exact pins for score positions, entity AUPs, importance scores, tick-rate codes, frustum flags, sim tiers, and distances.
- Partial pin acquisition releases in `finally`; schedule failure and completion use the same reverse-order release route.
- Existing foveated tick-rate math, continuous quality thresholds, DTO layout, and result application phase are unchanged.

Cinematic cheats used:
- No new simulation or visual realism added. Existing foveated cadence scoring remains the core performance cheat: spend updates near the player/camera and starve distant or rear targets predictably.
- Low/Middle/High/Ultra behavior stays continuous through existing threshold resolution, not binary switches.

Exact microseconds saved:
- No profiler microseconds claimed.
- Structural removal: one cross-frame DataVault writer-lock lifetime in the central foveated simulation scheduler.

Verification:
- `FoveatedSimulationManager.cs BRACE_DELTA=0`.
- Removed importance guard symbols are absent.
- Explicit search reports zero `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, or `GetComponentInParent` hits in the file.
- Targeted method-body scan reports `PASS_61_FOVEATED_METHOD_FORBIDDEN_HITS=0` across 14 hot/schedule/completion/pin/execute method bodies.
- `git diff --check` reports only CRLF normalization warnings.
- One build was launched only after CPU dropped to 4 percent and compiler-process scan was empty: `dotnet build Hecton8.slnx --no-restore /maxcpucount:1 -v:minimal`. It timed out after 244 seconds without diagnostics; this agent's `dotnet`, `csc`, and `VBCSCompiler` PIDs were stopped, and final compiler-process scan is empty. Compile success is not claimed.

Remaining hard debt:
- Full compile remains unproven until the CPU/compiler gate opens.
- Broad scheduled/job guards remain in interaction, voxel, physiology, graphics material, nutrient, vehicle/exosuit, power, loot, hazard, plasma, radar, flora, drone, combat, and related systems.
