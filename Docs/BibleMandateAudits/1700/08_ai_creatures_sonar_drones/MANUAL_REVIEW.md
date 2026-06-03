# AI / Creatures / Sonar / Drones Manual Review

Status: STATIC REVIEW - NO AI/CREATURE PROFILER PROOF
Date: 2026-06-02

## Reviewed Files

- `Assets/_Project/Scripts/Fauna/FaunaBrain.cs`
- `Assets/_Project/Scripts/Ecosystem/EcosystemRuntimeInstaller.cs`
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`
- `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`
- `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs`
- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`

## What Exists

- AI/creature/sonar/drones bibles exist and require state cadence, sensory truth, black-box telemetry, and bounded quality scaling.
- `NativeAudioFrameRingBuffer` has a strong fixed-storage SPSC shape and does not show managed hot allocation in the reviewed write route.
- `TopographicalSonarSynthesizer` schedules scan/fade work with job fences and uses graphics buffers for point rendering.
- `EcosystemRuntimeInstaller` can ensure ecosystem systems exist during scene bootstrap.
- `SargassumMicroFaunaBoids` owns GPU buffers/jobs/readback routes for microfauna presentation and has a continuous-density scaling obligation under ecosystem/creatures/rendering bibles.

## What Is Missing / Not Proven

- `FaunaBrain` clones runtime materials per fauna presentation route. That is a batching/material ownership risk for crowds unless the actor count is low or MPB/GPU instance data is used.
- `EcosystemRuntimeInstaller` dynamically creates a root and adds components. Production scenes should prefer an authored bootstrap prefab and leave installer behavior as recovery.
- Sonar proof still needs confidence/staleness gameplay proof, ping cadence proof, and GPU upload budget proof.
- `SargassumMicroFaunaBoids` clones an owner-local boid material and uses async readback; this needs SRP/material and cadence proof before crowd/ecology acceptance.
- `DroneFleetManager` contains mock repair/mining signal routes, mock SDF grid routes, fallback chassis specs, and a procedural drone material route. This is not acceptable production truth without strict disabling/proof.

## Current Classification

- `FaunaBrain.cs`: `YELLOW_BATCHING_RISK`.
- `EcosystemRuntimeInstaller.cs`: `YELLOW_BOOTSTRAP_PREFAB_ROUTE_REQUIRED`.
- `NativeAudioFrameRingBuffer.cs`: `GREEN_STATIC_RING_BUFFER_SHAPE`.
- `TopographicalSonarSynthesizer.cs`: `YELLOW_UI_SENSOR_PROOF_REQUIRED`.
- `SargassumMicroFaunaBoids.cs`: `YELLOW_GPU_BOID_MATERIAL_READBACK_PROOF_REQUIRED`.
- `DroneFleetManager.cs`: `P0_DRONE_MOCK_TRUTH_AND_PROCEDURAL_MATERIAL_ROUTE`.

## Required Next Proof

- AI black-box last-300-frame rings under predator/prey/path stress.
- Fauna material/SRP batcher proof or conversion to shared materials plus MPB/GPU data.
- Sonar confidence/staleness and ping spam proof.
- Microfauna GPU/material/readback proof and production drone truth proof; mock drone routes must be editor/test/headless-only or disabled for release gameplay.

## Pass 10 Addendum - Fauna Bootstrap And Ecosystem Installer Detail

- `FaunaBrain.Awake()` calls `CacheBiolumPresentationLights()`, `EnsureFaunaPresentationMaterials()`, and `CacheLogicalLodComponents()`. The hierarchy scans are cold startup cache routes, not proven per-frame scene search.
- `EnsureFaunaPresentationMaterials()` clones `new Material(sourceMaterial)` per fauna presentation slot and assigns the clone back to the renderer. This remains `YELLOW_FAUNA_BOOTSTRAP_CACHE_OK_MATERIAL_CLONE_PROOF_REQUIRED`, because crowd fauna needs shared materials plus MPB/GPU instance data or measured proof that actor/material count is hero-only and bounded.
- `ValidatePrimitiveColliderRig()` rejects `MeshCollider` and requires `CapsuleCollider` or `SphereCollider`. This is aligned with the collider-proxy law. `ApplyLogicalLodPresentationState()` still toggles cached colliders, so fauna LOD transitions need PhysX/telemetry proof under crowd stress.
- `GameBootstrapper.PublishPlayerRuntimeReference()` calls `EcosystemRuntimeInstaller.EnsureRuntimeSystems()`, which creates `__HECTON_ECOSYSTEM_RUNTIME` and adds ecosystem managers if missing. This is acceptable as bootstrap recovery only; production acceptance requires an authored ecosystem runtime prefab or deterministic boot manifest.

## Pass 11 Addendum - Sonar Truth, Ping Cadence, And GPU Upload Detail

- `TopographicalSonarSynthesizer` uses persistent H8Memory-owned buffers for scan points, hit masks, counters, mock SDF data, mock material ids, and material color LUT. The normal late-frame path finalizes scheduled work through `DispatcherJobFence.TryFinalizeCompleted`, so this is not classified as a naive forced-complete loop from static review alone.
- The release blocker is information truth: `ScheduleSonarScan()` falls back to `GenerateMockSdfJob` when no published SDF snapshot is available and marks `UsedMockSdfFlag`. This can be legal as an editor/development/diagnostic fallback, but it cannot ship as believable sonar/cartography truth.
- Point rendering uploads completed scan data into graphics buffers. That path needs ping-spam proof: max points, upload bytes, cadence, buffer growth count, async/fence latency, and compact/high GPU captures.
- Current classification is `YELLOW_BUFFERED_SONAR_ROUTE_WITH_MOCK_SDF_AND_GPU_UPLOAD_PROOF_REQUIRED`.

## Pass 12 Addendum - Ambient Biota Indirect Draw Fallbacks

- `AmbientBiotaDirector` uses dispatcher interfaces, indirect draw buffers, dirty GPU payload uploads, and `GlobalQualityWeight` in its presentation data. That is the correct direction for distant/ambient biota.
- Static review still found fallback risk: graphics buffers are created at runtime, the director clones an owner-local material, and it creates a fallback quad mesh when no authored mesh is assigned.
- Current classification is `YELLOW_AMBIENT_BIOTA_INDIRECT_DRAW_FALLBACK_PROOF_REQUIRED`.
- Required closure: authored mesh/material assignment or explicit recovery-only fallback policy, SRP/material instance count proof, no post-bootstrap buffer growth, readback/upload cadence proof, and compact/high ambient-biota captures.

## Pass 20 Addendum - AI Group Line-Level Runtime Suspect Closure

- Added `LINE_LEVEL_CLASSIFICATION.md` for all 70 static runtime suspect lines in the AI/creatures/sonar/drones audit group.
- Classification count: 45 `LEGAL_EDITOR_OR_DEV_GUARDED`, 25 `LEGAL_COLD_PATH`, 0 new `RUNTIME_VIOLATION`, 0 `FALSE_POSITIVE`.
- Important correction: `H8Debug` is a compile-stripped facade via `[Conditional("UNITY_EDITOR")]` and `[Conditional("DEVELOPMENT_BUILD")]`; those lines are not equivalent to direct release logging.
- Important non-closure: static line classification does not prove fauna crowd material cost, ecosystem authored bootstrap, sonar truth, drone production data, native audio bridge, managed callback removal, or 300-frame black-box/profiler behavior.
- Current group verdict is `YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`.
