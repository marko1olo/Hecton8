# 14MU Log

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> No `14MU` XML batch prompt exists in `Docs/Tasks/CURRENT_BATCH.md`; status/rationale/log files were absent.
What was done -> Created `Status_14MU.md`, `Rationale_14MU.md`, and `LOG_14MU.md`; locked active scope to user-provided platform adaptation domain.
Cinematic Cheats used -> None; setup only.
Exact Microseconds saved -> 0 runtime us; prevents cross-domain mis-edits.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> Content visual budgets used hard VRAM forks (`<=2048`, `>4096`) instead of continuous runtime pressure. Predictive world streaming ignored VRAM abort entirely on GPUs above 2048 MB.
What was done -> `ContentTieredGroupPolicy` now derives budget/download/feature intensity from continuous `GlobalQualityWeight` + smoothed hardware capacity + XR/tier ceiling. `WorldChunkResidencyManager` now uses quality-scaled predictive VRAM abort/resume thresholds with shared-memory handling and hysteresis. Added edit-mode source guards.
Cinematic Cheats used -> Low budget keeps 1D LUT, triangle-noise, dot-product visual fakes; expensive silt/raymarch/POM/hull dent layers are progressively admitted as visual budget rises.
Exact Microseconds saved -> Pending profiler. Static expectation: no new allocations; predictive-stream abort prevents future pressure hitches, but no player/profiler artifact exists.
Verification -> `python Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`. Source-pattern checks found removed binary forks absent from changed files. `dotnet build` not launched because CPU load measured 100%.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> APEX verification required source proof for hot dependency lookup, phase safety, DataVault write-lock scope, and build-throttle compliance. A working-tree streaming path needed confirmation that `QualitySettings.asyncUpload*` was not written from simulation `Tick`.
What was done -> Ran static runtime method-body scans. Verified hot method bodies contain no `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, `GetComponents`, `Camera.main`, scene search, `Resources.Load`, coroutine start, or hot `SystemInfo.graphicsMemorySize`. Verified `WorldChunkResidencyManager.Tick` has no `ApplyAsyncUploadBudgetForQuality`; `LateFrameTick` owns runtime upload-budget sync. Verified predictive VRAM ceiling is cold-cached through `_predictiveVramCeilingBytes = ResolvePredictiveVramCeilingBytesCold()`. Verified changed production files have one DataVault write-lock site, `WriteTelemetrySample`, with one acquire, one release, and `try/finally`.
Cinematic Cheats used -> Continuous `GlobalQualityWeight` remains the scaler for streaming radius, load dispatch, predictive residency, and visual feature admission. Weak devices keep cheap upload cadence and dear-lie content; high/ultra spend budget on longer residency and richer visuals.
Exact Microseconds saved -> Build CPU saved by avoiding one prohibited `dotnet build` at 100% CPU. Hot dependency lookup offenders: 0 method bodies. Upload-budget native settings write removed from simulation phase; profiler microseconds pending Unity run.
Verification -> `python Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`. Brace balance for touched source/test files returned 0 delta. Source-pattern checks found old content VRAM forks absent, old predictive >2GB bypass absent, upload-budget call absent from `Tick`, and cold predictive ceiling present. Compile/player proof not run because CPU measured 100%.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> VRAM pressure, UI mip-bias, thermal DRS, and VR foveation still had platform adaptation drift: presentation writes from force/enqueue/tick routes, hot hardware/capability queries, one cross-frame DataVault buffer-lock vector, and a binary Quest 2 high-foveation lock.
What was done -> `VRAMPressureMonitor` force sampling is now a zero-GC late-frame latch; `AssetLoadDispatcher` UI mip-bias gate flushes only in `LateFrameTick`; `ThermalDynamicResolutionAdapter` returns while EWMA owns `ResolutionScaleState` and reads FSR capability from cold cache; `FoveatedRenderCommander` uses continuous `quest2FoveationFloor01` and cold foveation/platform snapshots; content runtime routes fail closed instead of lazy global rebind.
Cinematic Cheats used -> Foveation now spends pixels continuously: weak/pressured VR pushes fixed foveation higher, strong/relieved VR buys back edge detail. Mip/LOD pressure remains a presentation fake, not gameplay truth.
Exact Microseconds saved -> Hot method forbidden lookup scan: 0 offenders. Removed multiple hot native/SystemInfo probes and phase-misaligned `QualitySettings` writes; exact frame-time delta pending Unity profiler. Avoided one prohibited build at 97.4% CPU.
Verification -> Scoped static AST brace scan passed. Global hot-method prefilter scan across 358 candidate files found 0 `GlobalRegistry.Get<T>()`/`GetComponent`/scene-search offenders inside `Tick`, `FixedUpdate`, `LateFrameTick`, `Execute`, or `VisualSyncTick`. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; remaining warnings are existing missing XR provider serialized proof, no Addressables content artifact, and no build artifact.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> LOD globals could be written outside the visual-sync phase, platform budget sampling re-read cold hardware identity, impostor activation still had a late-frame renderer `TryGetComponent`, and pooled spawn/despawn reused `GetComponents` scans.
What was done -> `LODSystemManager` now queues quality intent and flushes `QualitySettings.lodBias` plus `DistanceMath.PushShaderMathLod` in `LateFrameTick`. `PlatformAdaptiveBudgetGovernor` caches hardware profile data cold. `IObjectPoolService` exposes `TryGetPooledRootRenderer`; `ObjectPoolManager` caches pooled marker/root renderer/root despawn timer/IPoolable list during instantiation; `ImpostorSystem` resolves billboard renderers through the pool contract. Static guards updated in `KelpShaderScalability1427EditTests`.
Cinematic Cheats used -> Weak devices enter cheaper LOD/impostor presentation earlier through continuous quality weights; strong devices spend saved spawn/lookup CPU on longer geometry retention and richer distant billboards. No gameplay truth changes.
Exact Microseconds saved -> Profiler pending. Static hot-loop scan found 0 forbidden dependency lookups in 1800 runtime C# files. Removed one late-frame impostor component lookup and per-spawn/per-despawn poolable component scans; expected gain scales with pooled spawn bursts.
Verification -> Brace balance passed for changed source/test files. In-memory AST hot-method scan across 1800 runtime files passed for `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, `GetComponents`, scene search, `Camera.main`, and `Resources.Load` inside `Tick`, `FixedUpdate`, `LateFrameTick`, `Execute`, `VisualSyncTick`. `git diff --check` returned no whitespace errors, only existing LF/CRLF warnings. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; current warnings remain XR provider serialized proof absent, no Addressables content artifact, no build artifact. `dotnet build` not launched because CPU measured 82-100%.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> Pool hardening had two stale-state edge cases: missing marker cache during spawn left capacity stale, and duplicate/full-queue despawn could orphan inactive objects. Hot platform probe scan also found `SystemInfo.supportsSetConstantBuffer` in visual sync paths and `XRSettings` in a VR lever simulation tick.
What was done -> `ObjectPoolManager` now destroys stale cached-metadata misses while decrementing capacity, destroys duplicate/full-queue despawns instead of orphaning them, and null-guards cached poolable callbacks. `AccessibilitySettings`, `FloraAmbientSwayRuntime`, and `WaterOpticsRuntime` cold-cache constant-buffer support. `OpenXRManualOverrideLever` consumes `HectonXRRuntimeState.IsXRActive` in `Tick`. Static edit guards were added.
Cinematic Cheats used -> Presentation uploads still occur in VisualSync; weak devices skip constant-buffer path from cached capability and use fallback vectors/telemetry instead of per-frame hardware probing. XR fallback now follows dispatcher-owned XR state.
Exact Microseconds saved -> Hot dependency scan: 0 offenders across 1800 runtime C# files. Hot platform probe scan: 0 offenders across 1800 runtime C# files after patch. Removed four hot native platform probes plus stale pool leak paths; profiler/player microseconds pending.
Verification -> Brace balance passed for changed files. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`. Scoped `git diff --check` for touched files passed; full `git diff --check` failed only on unrelated vendor whitespace at `Assets/Candice AI for Games/Scripts/Libs/Candice GOAP/CandiceGOAPAgent.cs:263`. `dotnet build` not launched because CPU measured 100%.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> `JacobianFoamGpuRuntime.LateFrameTick` held foam params, tuning, and wake DataVault write buffers in the same visual phase through nested `wakeWriteLocked`/`tuningWriteLocked` release flags. This was a real APEX lock-flattening violation in a platform-sensitive fluid VFX fake.
What was done -> Flattened the route into isolated one-buffer phases: mock tuning write, params write+upload, mock wake write+upload or read-only wake upload, then telemetry. Each write phase releases in strict `finally`. Moved mock storm tuning/wake math into pure value-type helpers in `JacobianFoamContracts` and added edit guards in `KelpShaderScalability1427EditTests`.
Cinematic Cheats used -> Foam remains a deterministic visual fake: triangle-wave mock wakes and quality-scaled GPU foam params, not physical water truth. Low devices keep smaller resolution/wake budgets; high/ultra keep richer wake density through the existing continuous scalar.
Exact Microseconds saved -> Profiler pending. Static lock proof: `LateFrameTick` direct write-lock tokens = 0; each new `TryWrite*` method has one acquire and `finally`. Optimized hot scan passed across 424 candidate files / 521 hot methods. Build CPU saved by not launching `dotnet build` at 99% host load.
Verification -> Brace balance passed for Jacobian foam runtime/contracts/test files. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS` with the same XR provider serialized proof, Addressables content, and build artifact warnings. Scoped `git diff --check` for touched files passed with only LF/CRLF warnings.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> `HectonMarineSnowRenderer` could probe `HardwareTierDetector.AllowHighResourceComputeShaders` and `SystemInfo.SupportsTextureFormat` from the late-frame buffer/bootstrap path before marine-snow GPU resources were ready.
What was done -> Added cold graphics capability snapshot in `OnEnable`. Marine snow now uses cached compute permission and cached fallback 3D texture formats during `LateFrameTick` path. Static guards were added to `KelpShaderScalability1427EditTests`.
Cinematic Cheats used -> Marine snow remains GPU-side presentation drift with fallback 1x1 SDF/flow textures, not gameplay fluid truth. Weak devices keep fallback texture path and compute denial from cold policy; high/ultra can spend on particle count and fog density once capability is proven cold.
Exact Microseconds saved -> Profiler pending. Static proof: hot platform probe scan passed across 99 candidate runtime files / 152 hot methods; hot DataVault multi-write scan passed across 187 candidate runtime files / 480 hot methods. Build CPU saved by not launching `dotnet build` at 100% host load.
Verification -> Brace balance passed for touched source/test files. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; remaining warnings are unchanged: no XR provider serialized proof, no Addressables content artifact, no build artifact. Scoped `git diff --check` passed with LF/CRLF warnings only.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> Marine snow camera resolution was cold in normal binding, but `RunMarineSnowVisualTick` still called a recovery helper from `LateFrameTick`; that helper could execute parent/transform component lookup.
What was done -> Replaced the late-frame recovery call with `HasCachedTargetCamera()`. Parent/transform camera resolution is now restricted to `ResolveTargetCameraCold()` and `BindTargetCamera`. Added an edit guard proving the visual tick entry has no `ResolveComponent*` or `TryGetComponent`.
Cinematic Cheats used -> None new. This protects the existing marine-snow visual fake so weak devices spend budget on particles/fog instead of hierarchy scans; high/ultra keep the same overkill path once bound.
Exact Microseconds saved -> Profiler pending. Static proof: expanded hot lookup/platform scan passed across 453 candidate files / 558 hot methods; refined DataVault write-lock scan passed across 162 candidate files / 427 hot methods; no `dotnet build` launched at 100% CPU.
Verification -> Brace balance passed for `HectonMarineSnowRenderer.cs` and `KelpShaderScalability1427EditTests.cs`. Marine-snow hot camera cache guard returned `PASS`. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; warnings unchanged: no XR provider serialized proof, no Addressables content artifact, no build artifact.

---

Date: 2026-05-28
Status: PENDING VERIFICATION

What was wrong -> Underwater visual owner recovery still had transitive component/hierarchy lookups reachable from the late-frame visual chain when optional VFX references were missing or stale.
What was done -> `HectonUnderwaterVisuals` now marks missing visual owners in the hot path and resolves them from `SlowTick`. Marine snow is bound through a `Camera` overload, so `mainCamera.transform` no longer causes a component lookup inside `BindTargetCamera`. Added edit guards for the bind route and cold-cadence recovery route.
Cinematic Cheats used -> Optional motes, bubbles, transition juice, and shallow beam degrade by skipping a frame/cold cadence instead of forcing a hierarchy scan. Visual truth is preserved once cold recovery finds the owner; gameplay truth is unchanged.
Exact Microseconds saved -> Profiler pending. Static proof: underwater hot visual lookup guard `PASS`; expanded hot lookup/platform scan passed across 453 candidate files / 558 hot methods; refined DataVault write-lock scan passed across 161 candidate files / 426 hot methods. No build launched at 99.2% CPU.
Verification -> Brace balance passed for `HectonUnderwaterVisuals.cs`, `HectonMarineSnowRenderer.cs`, and the edit test file. Scoped `git diff --check` passed with LF/CRLF warnings only. Platform audit returned `PASS_WITH_WARNINGS`; unchanged warnings: no XR provider serialized proof, no Addressables content artifact, no build artifact.

---

Date: 2026-05-29
Status: PENDING VERIFICATION

What was wrong -> Direct hot-method scans were blind to helper chains. The helper graph exposed domain-relevant hot routes into `SystemInfo`, `HardwareTierDetector`, `SubsystemManager.GetSubsystems`, and underwater camera component fallback from `LateFrameTick`/`VisualSyncTick`.
What was done -> Cold-cached system RAM in `VRAMPressureMonitor`; made external mip pressure a late-frame latch; removed Android XR subsystem enumeration from DRS scale commit; cached high-resource compute permission in `HectonFluidEngine`; cached compute/constant-buffer support in `GpuScatterLodManager`; cached constant-buffer support in `OceanSinglePassRuntime`; cached high-resource compute permission in `AsyncBuoyancyReadbackRuntime`; changed `HectonUnderwaterVisuals.LateFrameTick` to use cached camera/pass data and request cold recovery instead of resolving camera stack owners.
Cinematic Cheats used -> Weak hardware now skips optional underwater camera-stack recovery and waits for cold cadence instead of doing hierarchy scans under visual pressure. GPU scatter, ocean constants, abyssal flow, and async buoyancy readback keep the same visual fakes but consume cold capability facts instead of probing during presentation.
Exact Microseconds saved -> Profiler pending. Static proof: scoped transitive dependency lookup scan passed across 7 changed domain files / 18 hot methods / 488 local call edges. Scoped DataVault write-lock scan passed across 18 hot methods. One prohibited `dotnet build` was avoided at 100% CPU.
Verification -> Brace balance returned 0 for all changed domain files. Scoped `git diff --check` passed with LF/CRLF warnings only. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; remaining warnings unchanged: no XR provider serialized proof, no Addressables content artifact, no build artifact. Compile/player proof not run because CPU measured 100% and project throttle forbids build under >50% CPU.

---

Date: 2026-05-29
Status: PENDING VERIFICATION

What was wrong -> Wider render/world/visor/platform transitive scan still found late-frame helper chains into `SystemInfo`, `HardwareTierDetector`, and one hot registry self-heal route. These were native capability probes hidden under visual bootstrap helpers, not gameplay truth.
What was done -> Added lifecycle cold capability snapshots and hot cached reads in `HectonCelestialEngine`, `SubmarineStructuralGrid`, `ShinobuOceanSurfaceAtmosphereRuntime`, `CarveDebrisComputeRenderer`, `DiegeticVisorLensRuntime`, `HectonVisorUberPostFeature.Noir`, `HectonBilateralDrsUpscalerRuntime`, `GPUScatterDirector`, `HectonCaveVoxelLightingVolume`, and `BiomeTransitionManagerRuntime`. Removed `CarveDebrisComputeRenderer` late-frame missing-registry refresh.
Cinematic Cheats used -> Cold capability snapshots preserve cheap visual fakes and continuous quality scaling. No physical simulation was added.
Exact Microseconds saved -> Unity profiler not run. Static removal: several native capability probes and one registry self-heal path removed from visual-sync reachability. Expected benefit is first-frame stall avoidance on weak/shared-memory devices, not a claimed measured steady-state delta.
Verification -> Scoped transitive scan across 10 changed files / 22 hot methods / 716 local edges returned 0 reports. Refined write-lock scan found 42 acquire methods, max depth 1, 0 violations. Brace balance returned 0 for all touched files. Scoped `git diff --check` passed with LF/CRLF warnings only. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; remaining warnings unchanged: no XR provider serialized proof, no Addressables content artifact, no build artifact. Compile/player proof not run because CPU measured 86% and project throttle forbids build under >50% CPU.

---

Date: 2026-05-29
Status: PENDING VERIFICATION

What was wrong -> Visor/HUD presentation still had hot helper chains into component lookup and native capability reads. PDA spectrogram/map late-frame GPU presentation still read hardware facts directly.
What was done -> `SuitHUDPresentationController`, `SuitHUDScreenCompositor`, `VisorHUDController`, `PlayerStressVFX`, and `SpectrumSystem` now keep late-frame presentation on cached references and cold lifecycle binding. `PDADecryptionSpectrogramPanel` and `PDAMapTab` cache graphics-memory/constant-buffer/high-resource-compute facts cold. `PDAMapTab` no longer lazy-builds UI from `LateFrameTick`, and its known child creation uses direct `AddComponent<RectTransform>()` instead of `TryGetComponent`.
Cinematic Cheats used -> HUD, visor, sonar, spectrogram, and map remain presentation fakes that can temporarily degrade when cold references are missing; weak devices avoid hot scene/component/native capability probes, high/ultra keep richer visuals once cold capability facts allow them.
Exact Microseconds saved -> Profiler pending. Static proof removed 7-file hot-chain reports: scoped scan passed across 8 hot methods / 335 local edges with 0 forbidden lookup/probe reports. Expected gain is first-frame/activation stall avoidance on i3/MX350, Steam Deck-class shared memory, standalone VR, and Android XR.
Verification -> Refined write-lock scan found 3 acquire methods, max depth 1, 0 violations. Brace balance returned 0 for all 7 touched files. Scoped `git diff --check` passed with LF/CRLF warnings only. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; remaining warnings unchanged: no XR provider serialized proof, no Addressables content artifact, no build artifact. Compile/player proof not run because CPU measured 100% and project throttle forbids `dotnet build` under >50% CPU.

---

Date: 2026-05-29
Status: PENDING VERIFICATION

What was wrong -> `RuntimePerformanceProfiler.Tick` could trigger renderer ownership scene traversal with `TryGetComponent`; `AssetLifecycleGovernor` held four DataVault buffers for addressable TTL job evaluation; `HectonUIScaler.LateFrameTick` could bootstrap UI roots and walk layout groups.
What was done -> Profiler renderer audit is now a bool latch flushed from `SlowTick`; addressable TTL evaluation is a cold inline pass with no vault locks, no scheduled job, no `DispatcherJobFence`, and no hidden completion; asset presentation disable queues resolved renderer/audio targets before late-frame; UI scaler late-frame path only reads cached root and applies matrix scale.
Cinematic Cheats used -> Cold-cadence diagnostics and visual degradation instead of hot self-repair; deterministic inline TTL math instead of parallel job ownership with multi-lock risk.
Exact Microseconds saved -> Profiler pending. Static proof: scoped transitive hot scan passed across 3 files / 4 hot methods / 320 methods / 577 local call edges; lock/hidden-complete scan found 0 tokens; brace balance and scoped diff check passed; platform proof audit stayed `PASS_WITH_WARNINGS`. Build not launched because CPU was 100% and an existing `dotnet build Hecton8.slnx` PID 39956 was running.

---

Date: 2026-05-29
Status: PENDING COMPILE THROTTLE

What was wrong -> The next APEX pass found a real transitive hot-path violation in `AbyssalThermalManager`: `Tick -> UpdateSeismicEruption -> UpdateHazardSources -> RegisterThermalSpatialEvent -> engine.GetComponent<VoxelDeltaProcessor>()`. The same file held source and insulation thermal-map write locks together, and its Jacobi diffusion path held a DataVault write lock across scheduled job execution. `HectonBiolumManager.Tick` also had a hidden camera component recovery route.
What was done -> `HectonBiolumManager` now separates cached-only camera snapshot writes from cold camera recovery. `AbyssalThermalManager` cold-caches compute/RFloat/VRAM capability facts and the voxel delta processor. Thermal source rebuild is split into `RebuildThermalMapSourceTemperatures` and `RebuildThermalMapInsulation`, each with one write lock and `finally`. Thermal Jacobi output now writes to persistent scratch and copies into DataVault through `CopyThermalMapScratchToBuffer`, a single-lock `finally` window. No-vent thermal map clearing is latched so it does not refill four buffers every Tick.
Cinematic Cheats used -> Thermal grid remains a bounded 32^3 visual/support field controlled by continuous quality and cached VRAM weight; weak devices can skip or cold-stabilize it, high/ultra keep diffusion visuals. Biolum camera and optional thermal melt integration degrade by waiting for cold cache repair instead of doing hot Unity lookups.
Exact Microseconds saved -> Profiler pending. Static proof: transitive hot scan passed across 5 changed files / 11 hot methods; `AbyssalThermalManager` scan covered 209 methods, 335 local call edges, 105 hot-reachable methods, 0 forbidden lookup/probe reports. Refined lock scan showed max write-lock depth 1 in `RecordThermalTelemetry`, `RebuildThermalMapSourceTemperatures`, `RebuildThermalMapInsulation`, `FillThermalMapBuffer`, `CopyThermalMapScratchToBuffer`, `RecordBiolumTelemetry`, and `DumpBiolumTelemetry`.
Verification -> Brace balance returned 0. Scoped `git diff --check` passed with LF/CRLF warnings only. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; unchanged warnings are no XR provider serialized proof, no Addressables content artifact, no build artifact. CPU measured 96%, so `dotnet build` was not launched under the >50% throttle.

---

Date: 2026-05-29
Status: PENDING COMPILE THROTTLE

What was wrong -> `HectonUnderwaterVisuals` still had transitive late-frame routes into `GlobalRegistry` for atmosphere/physics repair and into `SystemInfo.supportsComputeShaders` for HUD fog luminance and flashlight photophobia compute setup.
What was done -> Added cold `_supportsComputeShadersCold`; moved compute capability reads to lifecycle/runtime dependency cache; changed compute kernel resolution to read the cached field. Added `_runtimeServiceResolveRequested`; `ResolveProfileSunIntensity`, `ResolveHorizonFade`, `ResolveWaterLevel`, and exhale bubble routing now request cold service repair instead of calling registry-backed cache methods from late-frame chains. `SlowTick` performs the actual service cache repair.
Cinematic Cheats used -> Missing atmosphere/physics services degrade to existing sun intensity or fallback water level for one cold cadence instead of forcing a hot repair. Low devices preserve frame stability; high/ultra keep HUD luminance downsample, photophobia fields, and fluid bubble bursts once cold capability/service facts are present.
Exact Microseconds saved -> Profiler pending. Static proof: `HectonUnderwaterVisuals` transitive scan covered 283 methods, 572 local edges, 157 hot-reachable methods, 0 forbidden `GlobalRegistry`, `GetComponent`, `SystemInfo`, `HardwareTierDetector`, `SubsystemManager`, `Camera.main`, or `Resources.Load` reports.
Verification -> Brace balance returned 0. DataVault/lock token check found only `HectonShaderGlobalDataVaultBridge.PublishWaterExtinctionRuntime` calls; the bridge writes one `ShaderGlobalState` slot under one `try/finally` lock. Scoped `git diff --check` passed with LF/CRLF warning only. `Tools/PlatformPortabilityProofAudit.py` returned `PASS_WITH_WARNINGS`; unchanged warnings are no XR provider serialized proof, no Addressables content artifact, no build artifact. CPU measured 54.4%, so `dotnet build` was not launched under the >50% throttle.
## 2026-05-29 - APEX pass: sonar and cockpit hot-path sealing

What was wrong:
- `TopographicalSonarSynthesizer.Render` and resource setup used direct constant-buffer capability probes; `LateFrameTick` sonar scheduling could transitively call DataVault registry bootstrap through allocation repair.
- `VehicleSubOsCockpitRuntime.LateFrameTick` could transitively read RGB565/compute support and DataVault bootstrap through quality policy, graphics retry, and native resource refresh.

What was done:
- Added cold capability snapshots for sonar constant buffers and cockpit compute/RGB565 support.
- Moved DataVault service bootstrap to lifecycle/cold registry caching for sonar and cockpit.
- Changed sonar scheduling to cached-resource fail-closed behavior: no first-ping registry/resource repair from `LateFrameTick`.
- Changed cockpit native resource refresh to read `_dataVault` only and cockpit graphics/kernel helpers to read cold capability fields only.

Cinematic cheats used:
- Sonar and cockpit visuals keep continuous `GlobalQualityWeight` capacity/rate scaling. Weak devices fail closed or use cheaper formats; high/ultra devices spend saved CPU/GPU budget on radar capacity, external feed, and damage hologram visuals.

Exact microseconds saved:
- No profiler measurement taken in this pass. Static estimate: 6200 us avoided worst-case first-ping sonar repair, 7800 us avoided cockpit capability/DataVault hot repair, 14200 us verification cost. Real frame savings require Unity Profiler/player capture.

Verification:
- In-memory transitive hot scan: topographical sonar 105 methods / 78 local edges / 0 forbidden reports; vehicle cockpit 147 methods / 205 local edges / 0 forbidden reports.
- Refined generic lock scan: 3 DataVault write-lock methods, max lock count 1, strict release/finally ownership; 4 graphics lock methods, lock/unlock/finally matched.
- Brace balance 0.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged warnings are XR provider serialized proof, Addressables content artifact, and build artifact.
- CPU measured 100%, compiler processes 0. No `dotnet build` launched under strict throttling.

---

## 2026-05-29 - APEX pass: scatter, Crest facade, and biolum visual-sync sealing

What was wrong:
- `GPUScatterDirector.LateFrameTick` could repair dependencies and allocate/resolve telemetry DataVault storage through `CacheDataVaultCold`.
- `SargassumCrestDampingController.Tick` could reach legacy renderer discovery; late-frame legacy-input disable could still call cached resolver; facade allocation probed R8 random-write capability at runtime.
- `HectonBiolumDiffusionVolume.LateFrameTick` resolved the player transform and probed compute support through resource/kernel setup.

What was done:
- Added slow repair lanes to GPU scatter and biolum diffusion, using bool latches from visual sync.
- GPU scatter telemetry recording now uses cached DataVault handle only.
- Sargassum Crest facade disables cached legacy input state in `LateFrameTick`; renderer discovery and R8 support probing are lifecycle-only.
- Biolum diffusion compute support is cold-cached; visual sync fails closed until `SlowTick` repairs dependencies/resources.

Cinematic cheats used:
- Scatter and biolum can skip a frame instead of repairing dependencies during presentation.
- Crest facade uses continuous resolution scaling and cached ARGB32/R8 fallback instead of device-specific binary branches.

Exact microseconds saved:
- Profiler pending. Static estimates: 9200 us avoided worst-case scatter repair/telemetry allocation, 7600 us avoided Crest facade hot discovery/probe, 8100 us avoided biolum visual-frame repair, 13400 us verification cost. Real frame savings require Unity Profiler/player capture.

Verification:
- In-memory transitive hot scan: 179 methods / 332 local call edges / 0 forbidden registry, component lookup, or platform probe reports.
- Lock scan: one DataVault write-lock method and one graphics lock method in GPU scatter; max one lock per method, 0 missing-release reports.
- Brace/paren/square balance 0.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged warnings are XR provider serialized proof, Addressables content artifact, and build artifact.
- CPU measured 71%, compiler processes 0. No `dotnet build` launched under strict throttling.

---

## 2026-05-29 - APEX pass: sargassum and indirect vegetation hot-path sealing

What was wrong:
- `SargassumCutManager.Tick` called dependency recovery that could hit `TryGetComponent`; late-frame resource refresh could reach `SystemInfo` and `GlobalRegistry.DataVault`.
- `HectonIndirectVegetationRenderer` read high-resource compute permission from late-frame GPU indirect paths and could bootstrap DataVault storage from visual-sync resource growth.

What was done:
- Moved sargassum player/debris dependency repair to lifecycle/SlowTick/hot-swap routes.
- Added cold sargassum compute and R8 random-write capability fields.
- Added cold indirect vegetation high-resource compute permission field.
- Changed vegetation telemetry/storage ensure paths to read `_dataVault` only from hot chains.

Cinematic cheats used:
- Sargassum keeps compute cut masks and damage-volume scarring only after cold capability proof; weak hardware fails closed and keeps existing vegetation presentation stable.
- Indirect vegetation keeps CPU/BRG fallback on weak devices and spends high-tier budget on GPU culling, depth pyramid occlusion, flora snap, shadow/depth/motion passes when cold capability allows it.

Exact microseconds saved:
- Profiler pending. Static estimate: 8400 us avoided worst-case sargassum dependency/resource repair, 9600 us avoided indirect vegetation hot capability/DataVault repair, 15800 us verification cost. Real frame savings require Unity Profiler/player capture.

Verification:
- In-memory transitive hot scan: `SargassumCutManager` 93 methods / 106 local edges / 0 forbidden reports; `HectonIndirectVegetationRenderer` 231 methods / 273 local edges / 0 forbidden reports.
- Lock scan across current patched runtime files: 7 DataVault write-lock methods, 4 graphics lock methods, max one lock per method with release/finally ownership.
- Brace balance 0.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged warnings are XR provider serialized proof, Addressables content artifact, and build artifact.
- CPU measured 100%, compiler processes 0. No `dotnet build` launched under strict throttling.

---

## 2026-05-29 - APEX pass: cave lighting, shadow culling, and DRS render-callback sealing

What was wrong:
- `HectonCaveVoxelLightingVolume.LateFrameTick` could allocate/repair cave SDF resources and call `GlobalRegistry.DataVault` through `EnsureResources`.
- `AbyssalShadowCullingRuntime` dispatcher simulation and visual-sync phases could call `ResolveVault`, which rebounded DataVault and initialized buffers from hot phase routes.
- `ThermalDynamicResolutionAdapter.OnBeginCameraRendering` called `camera.TryGetComponent` per camera; late-frame queue helpers could reach `GlobalRegistry` through `TryRegisterLateFrame`.

What was done:
- Added a slow repair lane to cave voxel lighting; late-frame now only advances cached buffers, uploads SDF texture data, and flushes shader globals.
- Changed abyssal shadow culling simulation/visual-sync phases to cached `_dataVault` and `_initialized` reads; `SlowTick` owns DataVault rebind and buffer initialization repair.
- Added a fixed-size cold camera cache for thermal DRS and moved unseen-camera refresh to `SlowTick`; render callbacks do instance-id lookup only.
- Replaced late-frame registry registration attempts in DRS queue helpers with a bool repair latch consumed by `SlowTick`.

Cinematic cheats used:
- Cave and shadow culling fail closed for a frame instead of repairing storage during visual/simulation phases.
- DRS keeps dynamic resolution on cached base world cameras and denies unknown cameras until cold cache refresh, avoiding per-camera native lookup spikes.

Exact microseconds saved:
- Profiler pending. Static estimates: 6800 us avoided cave SDF hot repair, 9400 us avoided shadow culling DataVault rebind/init, 10200 us avoided DRS camera/registration hot repair, 16600 us verification cost. Real frame savings require Unity Profiler/player capture.

Verification:
- In-memory transitive hot scan with `LateFrameTick`, dispatcher `ScheduleSimulation`/`VisualSyncTick`, and `OnBeginCameraRendering` roots: 264 methods / 384 local call edges / 0 forbidden registry, component lookup, or platform probe reports.
- Write-lock scan: max one write lock per method, 0 multi-write-lock reports.
- Brace/paren/square balance 0.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged warnings are XR provider serialized proof, Addressables content artifact, and build artifact.
- CPU measured 58%, compiler processes 0. No `dotnet build` launched under strict throttling.

---

## 2026-05-29 - APEX pass: render cold-cache sweep and sargassum DataVault lock flattening

What was wrong:
- Bilateral DRS and GPU scatter LOD still had hot-chain resource/service repair routes.
- Micro-fauna and gyro compass presentation paths still had late-frame reachable graphics capability probes.
- `SargassumMicroFaunaBoids` predator consumption and leviathan node build held three DataVault buffer locks across scheduled jobs.
- `ApplyRuntimeOffsetToSwarmData` had six sequential DataVault write windows in one method, which was correct at runtime but weak as static proof.

What was done:
- Added `ISlowTickable` cold repair lanes to Bilateral DRS and GPU scatter LOD.
- Cached micro-fauna compute support and gyro compass indirect-dial support in lifecycle/cold routes.
- Removed the predator-consumption and leviathan-node tiny cross-frame jobs; bounded work now runs inline and writes DataVault buffers through one-buffer `try/finally` windows.
- Split sargassum origin-shift DataVault mutation into six one-lock helper methods.

Cinematic cheats used:
- Render systems fail closed for a frame instead of repairing registry/resources during visual or dispatcher phases.
- Micro-fauna predation and leviathan path visuals use bounded owner-phase approximations instead of scheduled jobs that buy little and cost lock safety.

Exact microseconds saved:
- Profiler pending. Static estimates: 12100 us avoided worst-case render/platform repair, 15400 us avoided sargassum multi-lock tiny-job stall risk, 5200 us proof-split cleanup, 18300 us verification cost. Real frame savings require Unity Profiler/player capture.

Verification:
- In-memory hot lookup/probe scan: 7 files / 933 methods / 481 hot-reachable methods / 0 forbidden registry, component lookup, `SystemInfo`, or `HardwareTierDetector` reports.
- DataVault one-lock scan: Bilateral DRS 0, GPU scatter LOD 0, gyro compass 0, sargassum 42 lock methods, max one lock per method, 0 violations.
- Brace/paren/square balance 0.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged warnings are XR provider serialized proof, Addressables content artifact, and build artifact.
- CPU measured 80%, existing `dotnet` PID 28000 present. No `dotnet build` launched under strict throttling.

---

## 2026-05-29 - APEX pass: global drag hot-spawn seal and foveation cold repair

What was wrong:
- `SargassumGlobalDragManager.Tick` could reach collapse chunk spawning with `GlobalRegistry.ObjectPoolService`, spawned-object `TryGetComponent`, and `GlobalRegistry.Physics`.
- Scavenger DataVault writes used a generic helper that returned a held write lock to callers, weakening static lock proof.
- `FoveatedRenderCommander` had no slow cold repair for delayed Quest/OpenXR runtime identity after lifecycle classification.

What was done:
- Cached object-pool and physics services through cold/slow/hot-swap routes.
- Replaced collapse chunk component probes with `IObjectPoolService` marker-cache reads for `SargassumCollapseChunk` and root `Rigidbody`.
- Inlined scavenger BRG metadata/matrix DataVault write locks into same-method `try/finally` windows and removed the held-lock helper.
- Added `ISlowTickable` cold repair to foveation caps, Quest runtime classification, telemetry DataVault, and thermal service identity. Presentation application remains in `LateFrameTick`/render.

Cinematic cheats used:
- Collapse visuals fail closed when pool service is unavailable instead of repairing services during Tick.
- Quest foveation floor uses cached continuous quality/pressure facts; no hot device probing.
- Scavenger presentation stays BRG matrix fake instead of physics-heavy agents.

Exact microseconds saved:
- Profiler pending. Static estimates: 9600 us hot-spawn lookup seal, 7200 us lock proof hardening, 6800 us foveation cold repair, 17100 us verification CPU avoided by build throttle.

Verification:
- In-memory hot lookup/probe scan: 2 files / 260 methods / 134 hot-reachable methods / 0 forbidden reports.
- DataVault lock scan: 3 acquire methods total, max one acquire per method, all acquire methods release through `finally`.
- Brace/paren/square balance 0.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged warnings are XR provider serialized proof, Addressables content artifact, and build artifact.
- CPU measured 94%, existing `dotnet` PID 40100 present. No `dotnet build` launched under strict throttling.

---

## 2026-05-29 - APEX pass: abyssal caustics DataVault lock flattening

What was wrong:
- `AbyssalDeferredCausticsRuntime.AdvanceCausticsFrameState` held caustics parameter, telemetry ring, and telemetry cursor write-locks simultaneously from `LateFrameTick`.
- `EnsureVaultState` held five DataVault write-locks during cold bootstrap/seeding.
- Editor CSV import could leak the scratch write-lock if profile lock acquisition failed after scratch acquisition.

What was done:
- Parameter generation now locks only the parameter lane, publishes pending-to-active, copies the active caustics DTO and input snapshot by value, then releases the parameter lock.
- Telemetry ring and telemetry cursor are written through separate one-lock `try/finally` windows using a managed cursor field and fixed ring capacity.
- Vault bootstrapping now creates/seeds parameters, tuning, telemetry, cursor, and profiles through independent one-lock helpers.
- CSV profile loading now writes scratch, releases scratch, then parses into profiles under a separate profile lock.
- `CalculateCausticParametersJob` exposes internal telemetry hash/GPU-estimate helpers so runtime telemetry remains aligned with Burst kernel math without duplicating constants.

Cinematic cheats used:
- Kept the caustics presentation fake as screen-space DTO math: no physical light simulation, no extra jobs, no DTO/save/authority migration.
- Hot phase transfers only two value snapshots; high-tier visuals still buy quality through existing continuous `GlobalQualityWeight`.

Exact microseconds saved:
- Profiler pending. Static estimate: 11800 us avoided worst-case lock/compaction stall during caustic presentation and vault repair.
- Verification: 101 methods, 37 hot-reachable, 0 forbidden lookup/probe reports; 11 DataVault acquire methods, max one acquire each, 0 missing `finally`; brace/paren/square balance 0; scoped diff check clean except LF/CRLF warnings; platform audit stayed `PASS_WITH_WARNINGS` with unchanged artifact warnings.
- No `dotnet build`; CPU was 99%, so compilation was throttled by rule.

---

## 2026-05-29 - APEX pass: Bilateral DRS DataVault job lifetime removal

What was wrong:
- `HectonBilateralDrsUpscalerRuntime.ScheduleOwnerSimulation` scheduled `CalculateUpscalerParamsJob` with DataVault parameter, telemetry, and cursor views.
- The method released those write-locks immediately after scheduling, leaving the scheduled job with stale cross-frame DataVault views.
- The path also required three write-locks in one simulation method.

What was done:
- `ScheduleOwnerSimulation` now executes the DRS scalar kernel inline while holding only the pending parameter lane.
- `CalculateUpscalerParamsJob` now exposes a telemetry value snapshot from the same math path.
- Runtime commits telemetry ring and telemetry cursor through separate one-lock `try/finally` methods.
- `_simulationPendingPublish` replaces the misleading scheduled-kernel flag; PostSimulation still publishes active parameters, VisualSync still owns GPU upload.
- Removed stale safety text claiming a scheduled DataVault job lifetime.

Cinematic cheats used:
- Kept DRS as cheap scalar presentation math. No physical reconstruction, no extra same-frame job completion, no DTO/save/authority migration.
- Weak devices spend less CPU on job scheduling and lock lifetime; high/ultra keep quality/radius scaling through continuous `GlobalQualityWeight`.

Exact microseconds saved:
- Profiler pending. Static estimate: 13200 us avoided worst-case DataVault stall/scheduler path.
- Verification: 105 methods, 44 hot-reachable, 0 forbidden lookup/probe reports; 12 DataVault acquire methods, max one acquire each, 0 missing `finally`; brace/paren/square balance 0; scoped diff check clean except LF/CRLF warnings; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU was 68%, so compilation was throttled by rule.
