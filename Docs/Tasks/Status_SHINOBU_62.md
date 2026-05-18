# Status_SHINOBU_62

Agent: SHINOBU_62  
Domain: OCEAN_SURFACE_AND_ATMOSPHERE_DIRECTOR  
Prompt source: `Docs/Tasks/CURRENT_BATCH.md`, second duplicate `SHINOBU_62` block, starts at line 2332.  
Task count: 20  
State: IN_PROGRESS - ocean determinism/UI Toolkit/static audit done; forced runtime/editor compile is blocked by CPU gate and unrelated Core dependency errors.  
Contamination note: this file has been repeatedly overwritten by stale flora/fauna SHINOBU_62 state. For the current user request, that first duplicate block is rejected.

## Mandates Read

- `MATH_AUP_Determinism_Sync.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DATA_Runtime_Struct_Layout_ARM64.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`

## Task Matrix

- [x] 01 Binary graveyard reconnaissance: Gerstner/weather precompute is now cold-probed; fallback mock weather hydrates waves. DOD: cold IO only. Rejected: per-frame payload probe. Est: saves 20-80 us/frame IO risk.
- [x] 02 Flat water eradication: CPU and HLSL share `WaveParametersDTO`; no Unity Plane/Skybox authority. DOD: one math truth. Rejected: visual-only waves. Est: prevents buoyancy desync.
- [x] 03 CS1612 purge: hot DTOs use public fields. DOD: no hot auto-properties. Rejected: get/set wrappers. Est: avoids NativeArray defensive copies.
- [x] 04 ARM64 padding: `WaveParametersDTO` 32B; atmosphere/weather/lod/telemetry/signal lanes 64B. DOD: explicit offsets. Rejected: `Pack=1`. Est: avoids unaligned ARM64 loads.
- [x] 05 Blind dependency mocking: 10,000 AUP mock buoyancy queries. DOD: no submarine dependency. Rejected: direct Agent 11 coupling. Est: batch path avoids managed loop.
- [x] 06 Burst evaluator: exact `EvaluateWaves(double3 AUP, float time, NativeArray<WaveParametersDTO> waves, out float height, out float3 normal)`. DOD: pure Burst math. Rejected: Unity water component. Est: bounded 4-16 waves/sample.
- [x] 07 Waterline breach: analytic camera/surface compare emits `WaterlineBreachSignal`. DOD: SignalBus. Rejected: trigger collider. Est: one wave eval replaces broadphase path.
- [x] 08 Atmospheric scattering: Rayleigh/Mie/gas giant scalars publish to shader globals. DOD: HLSL math sky. Rejected: standard Skybox. Est: zero CPU sky sim after scalar upload.
- [x] 09 Foam/whitecaps: HLSL Jacobian pinch scalar. DOD: GPU fake. Rejected: CPU foam particles. Est: avoids particle updates.
- [x] 10 Wind advection: publishes `_GlobalFlowVector` and `_H8GlobalFlow`. DOD: decoupled global vector. Rejected: sibling direct dependency. Est: one vector upload.
- [x] 11 Continuous mesh LOD: `OceanSurfaceLodDTO` lerps radial grid from `GlobalQualityWeight`. DOD: continuous q. Rejected: low/high branch. Est: low lane cuts far vertex density.
- [x] 12 Storm surge: narrative signal/mask forces 15m waves and tint. DOD: core mask/signal. Rejected: direct quest reference. Est: DTO rewrite only.
- [x] 13 AUP shader projection: double AUP wraps by wavelength before float trig; shader gets camera-local projection. DOD: 100km jitter guard. Rejected: absolute GPU coordinates. Est: correctness at 50km+.
- [x] 14 Rain disturbance: shader scalar normal ripple. DOD: Dear Lie scalar. Rejected: collision particles. Est: avoids O(rainDrops) CPU.
- [x] 15 Buoyancy broadcast: `OceanBuoyancyHeightJob` and `OceanBuoyancyAupJob`. DOD: IJobParallelFor. Rejected: per-object MonoBehaviour query. Est: 100-object batch avoids 100 managed calls.
- [x] 16 Zero-init: Vault buffers use `NativeArrayOptions.UninitializedMemory` where overwritten. DOD: handles only. Rejected: private persistent NativeArrays. Est: avoids boot clears.
- [x] 17 Telemetry: 300-frame ring and `Dump_SURFACE_SURGEON.bin`. DOD: black-box recorder. Rejected: non-forensic crash. Est: diagnostic, not frame saving.
- [x] 18 Editor tuner: `Atmosphere & Wave Tuner` writes Vault DTOs through UI Toolkit `CreateGUI`. DOD: human facade without IMGUI. Rejected: recompiling constants and `OnGUI`. Est: saves iteration time.
- [x] 19 CSV ingestor: native byte parser for `weather_profiles.csv`. DOD: no Split/LINQ/row objects. Rejected: managed CSV parser. Est: avoids heap churn.
- [x] 20 Wave profiler gizmo: SceneView `OnDrawGizmos(SceneView)` samples same CPU evaluator. DOD: visual proof hook. Rejected: separate debug math. Est: editor-only correctness audit.

## Iterative Loops

1. Tasks 01-05: prompt disambiguated to ocean; binary ledger and DTO/mocks audited.
2. Tasks 06-10: Burst evaluator, waterline SignalBus, scattering, foam, wind route.
3. Tasks 11-15: radial LOD, storm surge, AUP wrapping, rain fake, buoyancy jobs.
4. Tasks 16-20: Vault zero-init, telemetry, editor tuner, CSV parser, SceneView grid.
5. Ultra audit: exact Burst flags, `[NoAlias]`, deterministic `Unity.Mathematics.Random`, `Camera.main` removed.
6. GPU upload audit: double-buffered, hash-gated wave upload; no per-frame cold GraphicsBuffer creation.
7. State contamination audit: stale flora/fauna status/rationale rewritten back to ocean authority.
8. Determinism audit: ocean runtime no longer reads `Time.frameCount` or dispatcher delta for surface truth; `_simulationFrameCounter` advances fixed 1/60s and feeds waterline/telemetry/shader LOD.
9. Shader/UI audit: shader globals are state-hash gated, endian loader has `math.reversebytes`, and the editor facade was moved from IMGUI `OnGUI` to UI Toolkit.

## Verification

- Prompt extraction: PASS, active block is the second duplicate `SHINOBU_62` ocean prompt.
- Static forbidden scan: PASS for new ocean runtime/contracts/editor/HLSL/tests; no `Camera.main`, `FindObject`, `GameObject.Find`, `OverlapSphere`, `SphereCollider`, LINQ, `Pack=1`, `Skybox`, `UnityEngine.Random`, `Time.frameCount`, `Time.deltaTime`, `OnGUI`, `EditorGUILayout`, or `GUILayout`.
- Burst directive scan: PASS, 8 exact required Burst attributes.
- Pointer aliasing scan: PASS, all ocean job NativeArray fields use `[NoAlias]`; immutable inputs use `[ReadOnly]`.
- Determinism scan: PASS, ocean CPU evaluator and shader globals share `_timeSeconds` from quality-quantized fixed simulation time; mock RNG now mixes `Seed`, `SectorHash`, and `SimulationFrame`.
- Generated-project hygiene: PASS, temporary `.csproj` include edits were removed after CPU gate blocked build.
- Compile: BLOCKED BY DEPENDENCY. After a clean preflight (no `dotnet`/`csc`, CPU 15.23%), forced Core compile was launched with temporary ocean runtime include. It failed before ocean diagnostics on unrelated duplicate members in `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`: `EnsureNativeHandleStorage`, `DisposeNativeHandleStorage`, `EvaluateAddressableTtlAndQueueReleases`, `WriteHeapTelemetrySample`, `TryAcquireTrackedHandle`, `AllocateAddressableHandleSlot`, `TryDecrementNativeRefCount`, `SetNativeRefCount`, `ArmNativeTtlRelease`, `ClearNativeHandleSlot`, `DumpHeapTelemetry`, and `ComputeBundlePrefixHash`. Temporary `.csproj` include edits were removed.
- Compile recheck: SKIPPED BY HARDWARE GATE. Latest sampled CPU was 89.36% with no active `dotnet`/`csc`; AGENTS forbids launching build while CPU is above 50%.
