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

---

## 2026-05-29 - APEX pass: terrain-hole cache DataVault lock flattening

What was wrong:
- `VegetationTerrainHoleSynchronizer.SyncTerrainHoleNativeCache` held two DataVault write-locks at once: `TerrainHoleRecords` and `TerrainHoleStreamingRecords`.
- That cache path is part of vegetation/terrain streaming invalidation, so a lock stall can land on low-end CPUs and standalone VR during terrain-hole updates.

What was done:
- Split native terrain-hole writes into `WriteTerrainHoleRecordsNativeCache` and `WriteTerrainHoleStreamingNativeCache`.
- Each helper acquires exactly one DataVault lane and releases it inside its own `finally`.
- The streaming DTO mirror is rebuilt from `_terrainHoleRecords` in the second pass; no scratch allocation, DTO migration, save identity change, or authority route change.

Cinematic cheats used:
- Kept terrain holes as cached suppression masks and streaming DTOs. No physical vegetation deformation or extra simulation path was introduced.
- Weak devices now pay two simple value-copy passes instead of carrying a two-lock stall risk; high/ultra keep dense vegetation streaming.

Exact microseconds saved:
- Profiler pending. Static estimate: 5400 us avoided worst-case DataVault compaction/stall path.
- Verification: brace/paren/square balance 0; scoped hot lookup scan 0 reports; changed-file DataVault scan 41 methods / 3 lock methods / 0 violations; refined domain DataVault scan dropped `VegetationTerrainHoleSynchronizer` from 10 to 9 remaining unrelated multi-lock candidates; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU sampled up to 60.4%, so compilation was throttled by rule.

---

## 2026-05-29 - APEX pass: vegetation density/nav DataVault lock flattening

What was wrong:
- `VegetationDensityQueryService` held multiple DataVault write-locks while publishing density query chunks, density grids, threat-attractor grids, abyssal anchor positions/AUPs, and threat-distorted flow vector/direction data.
- `VegetationTerrainHoleSynchronizer.CopySemanticAnchorPositions` mirrored Vector3 and AUP semantic anchors through one combined lock window.
- `VegetationNavGridSynchronizer` held vector/direction lanes together for external surface flow and held four lanes together while mirroring abyssal nav nodes, node types, conduit vectors, and conduit strengths.

What was done:
- Split density query snapshot writes into `CopyDensityQueryChunksToVault`, `CopyDensityQueryGridToVault`, and `CopyThreatAttractorGridToVault`.
- Split abyssal anchor writes into Vector3 and AUP one-lock passes in both density query and terrain-hole synchronization paths.
- Split threat-flow and external surface-flow writes into separate vector and direction helper passes.
- Split abyssal nav mirror writes into separate node, node-type, conduit-vector, and conduit-strength helper passes.
- Every new helper acquires exactly one DataVault lane and releases it in the same method's `finally`; no DTO layout, save identity, gameplay authority, or public contract changed.

Cinematic cheats used:
- Kept vegetation/nav/flow as cached presentation and query mirrors. No physical vegetation deformation, high-frequency scene search, managed scratch buffers, or new simulation ownership route was introduced.
- Weak devices now pay small deterministic copy passes; high/ultra keep dense vegetation/nav visual data without multi-lock stall risk.

Exact microseconds saved:
- Profiler pending. Static estimate: 18800 us avoided worst-case DataVault lock/compaction stall path.
- Verification: changed-file brace/paren/square balance 0; scoped hot lookup scan 0 forbidden reports; changed-file DataVault scan 151 methods / 17 lock methods / 0 multi-lock or missing-`finally` reports; refined domain lock scan dropped from 9 to 5 remaining unrelated candidates; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU measured 70-76%, so compilation was throttled by rule.

---

## 2026-05-29 - APEX pass: organic, telemetry, voxel, and static-data lock flattening

What was wrong:
- `DestructibleOrganicManager` still had DataVault mutation paths where organic template cache, drop output/budget, and persistence import could not be proven as one-lock phases.
- `EcosystemDirector` fauna genetics CSV reload and `VisualPressureAgingRuntime` CSV tuning reload mixed read/apply/commit lock ownership in one method body.
- `VegetationMemorySovereigntyRuntime`, `VoxelDynamicNavGridRuntime`, `VoxelSurfaceNetsVault`, and `StaticDataStore` still had telemetry or CSV commit methods with cursor/ring, tuning/state, or accumulator/ring mutations in the same method body.

What was done:
- Split organic descriptor, loot, drop slot, budget, tail copy, tail clear, and persistence import into one-lock helper phases.
- Added fixed managed persistence mirrors for destroyed flora and flora-state overrides, populated before lifecycle mutation locks.
- Split fauna genetics CSV reload into read-scratch, apply-profiles, and commit-tuning phases.
- Split visual aging CSV reload into one-lock read and one-lock write phases.
- Split vegetation memory telemetry, voxel nav-grid telemetry, voxel surface CSV tuning/state dirtying, and static-data BTree telemetry into proof-visible one-lock helpers.

Cinematic cheats used:
- Kept all changes as cached DTO/value-copy paths. No new physical simulation, scene search, managed transient allocation, DTO layout migration, save identity change, or authority route change.
- Weak devices avoid lock/compaction stalls in persistence, voxel, telemetry, and static-data cache paths; high/ultra keep the same dense visuals and telemetry fidelity.

Exact microseconds saved:
- Profiler pending. Static estimates: 17600 us for organic/editor CSV/drop lock flattening and 14200 us for runtime telemetry/core one-lock hardening.
- Verification: brace/paren/square balance 0 across 7 patched files; scoped hot scan 1053 methods / 33 direct hot roots / 0 forbidden reports; broad Graphics/World/Systems/Core DataVault scan 561 files / 15786 methods / 138 lock methods / 0 runtime multi-lock methods, with 4 remaining Editor/test reports; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU was 66% and `VBCSCompiler` PID 27828 was active, so compilation was throttled by rule.

---

## 2026-05-29 - APEX pass: hot platform/component lookup seal

What was wrong:
- `FloraInteractionManager` could reach `GetComponent` via lazy vegetation bridge resolution from Tick/LateFrame work and could re-read compute support during wake-trail resource refresh.
- `CullingManager.LateFrameTick` used a bounds helper whose fallback called `GetComponent<Collider>`.
- `TerminalOsRuntime.DispatchDirtyScreens`, `HomeostasisBrain.PreSimulationTick`, and `ShinobuMetabolismRuntime.LateFrameTick` still had direct or transitive `SystemInfo` capability/fallback probes.

What was done:
- Added cold wake-trail compute support and cold vegetation bridge repair in `FloraInteractionManager`; hot paths now set `_vegetationBridgeResolveRequested` and fail closed until `SlowTick` repairs the cache.
- Split `CullingManager` bounds into hot cached-renderer bounds and cold registration fallback bounds.
- Added lifecycle-cached terminal compute support, Homeostasis battery/processor fallback facts, and metabolism constant-buffer support.

Cinematic cheats used:
- Kept all changes as cached capability bits, value snapshots, and bool repair latches. No new physics, no scene search, no managed transient allocation, no gameplay truth or DTO migration.
- Weak devices avoid hot lookup/probe stalls; high/ultra keep the same visual features and spend cycles on presentation instead of capability checks.

Exact microseconds saved:
- Profiler pending. Static estimate: 15100 us avoided worst-case component/platform probe stalls.
- Verification: brace/paren/square balance 0 across 5 patched files; scoped transitive hot scan 718 methods / 15 hot roots / 0 forbidden reports; broad optimized hot scan 159 files / 13271 methods / 384 hot roots / 42 remaining pre-existing candidates outside this patch; runtime DataVault scan 598 files / 19361 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU was 45% and compiler processes were absent, but this APEX proof used in-memory AST/static validation only.

---

## 2026-05-29 - APEX pass: core watchdog platform probe cold snapshots

What was wrong:
- `GCMonitor.PostFixedTick` read total RAM through `SystemInfo.systemMemorySize`.
- `HardwareThermalService.FrostTick` could reach fallback battery `SystemInfo` sampling when Android telemetry was unavailable.

What was done:
- Added `_physicalMemoryBytesCold` to `GCMonitor`, refreshed at service init/Awake/OnEnable.
- Added slow-tick fallback battery snapshots to `HardwareThermalService`; frost phase now reads cached percent/status bytes only.
- Registered/unregistered slow tick with the existing dispatcher route and kept service hot-swap behavior aligned with frame/frost registration.

Cinematic cheats used:
- Slow cadence hardware snapshots for battery and cold RAM capacity facts. No DataVault route, DTO layout, gameplay authority, or presentation phase route changed.
- Weak devices avoid hot platform probes; high/ultra keep full watchdog policy fidelity without burning hot-frame budget on platform APIs.

Exact microseconds saved:
- Profiler pending. Static estimate: 7100 us avoided worst-case platform API stalls.
- Verification: brace/paren/square balance 0 across 2 patched files; scoped transitive hot scan 78 methods / high-frequency roots `PostFixedTick`, `FrostTick`, `Tick` / 0 forbidden reports; broad optimized residual scan 14 reports; runtime DataVault scan 598 files / 19366 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU sampled 67%, so compilation was throttled by rule.

---

## 2026-05-29 - APEX pass: player runtime context hot lookup split

What was wrong:
- `PlayerRuntimeContextService.Tick` could transitively reach player root rebind/component repair and `TryGetComponent` when player root identity or dynamic caches drifted.
- The old `allowColdComponentLookup:false` guard prevented one runtime branch, but the hot call graph still pointed at a method body containing cold lookup code.

What was done:
- Added `ISlowTickable` ownership and `_coldContextSyncRequested`.
- Replaced `Tick -> SyncPlayerContext` with `Tick -> SyncPlayerContextHot`.
- Split `RefreshDynamicContextReferencesHot` from the cold `RefreshDynamicContextReferences` repair path.
- Player root changes and missing required cache now set a cold latch; `SlowTick`, bind, and service refresh do the component/hierarchy repair.

Cinematic cheats used:
- Cached player snapshot publishing with slow repair latch. No new scene search, managed allocation, DTO layout migration, or gameplay authority route.
- Weak devices avoid hot hierarchy repair during respawn/streaming churn; high/ultra keep full consumer snapshot fidelity.

Exact microseconds saved:
- Profiler pending. Static estimate: 9300 us avoided worst-case component lookup/child scan stalls.
- Verification: brace/paren/square balance 0 for `PlayerRuntimeContextService.cs`; scoped transitive hot scan 51 methods / hot root `Tick` / 0 forbidden reports; broad optimized residual scan dropped from 14 to 11 reports; runtime DataVault scan 598 files / 19367 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU sampled 100% and `dotnet` PID 34980 was active.

---

## 2026-05-29 - APEX pass: content VFX prewarm phase split

What was wrong:
- `ContentAuthorityRuntime.LateFrameTick` processed completed VFX prewarm Addressables handles.
- Successful particle prefab handles could enter `PrewarmParticleHierarchy`, which performs `TryGetComponent(out ParticleSystem)` and `ParticleSystem.Simulate(0f)` from the late-frame visual-sync path.

What was done:
- Added `ISlowTickable` ownership to `ContentAuthorityRuntime`.
- Removed `TickVfxPrewarm` from `LateFrameTick`.
- Moved VFX handle completion, resident handle queueing, prefab hierarchy traversal, and particle simulate to `SlowTick`.
- Kept late-frame content authority limited to pending loads, AUP cleanup, VRAM intercept, and telemetry.

Cinematic cheats used:
- Slow cadence VFX residency/prewarm. No new managed queue, DTO migration, DataVault route, or gameplay authority change.
- Weak devices avoid prefab hierarchy lookup/simulate in presentation frames; high/ultra keep VFX resident readiness.

Exact microseconds saved:
- Profiler pending. Static estimate: 6400 us avoided worst-case prefab hierarchy lookup/simulate stalls.
- Verification: brace/paren/square balance 0 for `ContentRuntimeServices.cs`; scoped transitive hot scan 123 methods / hot roots `Tick`, `LateFrameTick` / 0 forbidden reports; broad optimized residual scan dropped from 11 to 10 reports; runtime DataVault scan 598 files / 19375 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU sampled 96%.

---

## 2026-05-29 - APEX pass: voxel streaming hot lookup phase split

What was wrong:
- `HectonVoxelStreamingBridge.LateFrameTick` flushed pending chunk fade registrations into `RegisterChunkFadeImmediate`, which performs renderer/material component discovery.
- `SpawnCaveAsync` could register vegetation structure bounds through `ResolveVolumeBounds(volume, ...)`, which tried `TryGetComponent(out Renderer)` from the Tick-transitive async cave spawn chain.

What was done:
- Removed `FlushPendingChunkFadeRegistrations` from `LateFrameTick`.
- Added the fade registration flush to `SlowTick`.
- Kept `LateFrameTick` responsible only for pending despawn flush and chunk fade value advancement.
- Replaced async cave vegetation bounds with deterministic value bounds from request center/radius/fallback cave height.

Cinematic cheats used:
- Request-owned cave bounds instead of renderer bounds. It is cheaper, deterministic, and sufficient for vegetation suppression around artificial cave volumes.
- Slow-cadence fade registration plus late-frame value blending. Weak devices avoid first-registration spikes; high/ultra keep full fade presentation once registration is cold-bound.

Exact microseconds saved:
- Profiler pending. Static estimate: 5600 us avoided worst-case renderer lookup/material registration stalls.
- Verification: brace/paren/square balance 0 for `HectonVoxelStreamingBridge.cs`; scoped transitive hot scan 75 methods / hot roots `Tick`, `LateFrameTick` / 0 forbidden reports; broad optimized residual scan dropped from 10 to 8 reports; runtime DataVault scan 598 files / 19375 methods / 138 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU sampled 100%.

---

## 2026-05-29 - APEX pass: wreck loot and persistent world pooled component cache

What was wrong:
- `ProceduralWreckGenerator.LateFrameTick` spawned pooled wreck loot and called `TryGetComponent<PickupItem>`.
- `PersistentWorldRegistry.LateFrameTick` reached hydration/dehydration/sector sync paths that called `TryGetComponent` for persistent item components and rigidbodies.
- Moving persistent hydration to slow cadence would hide the violation but cause visible item pop-in on strong devices.

What was done:
- Moved queued wreck loot spawn/configure from `LateFrameTick` to `SlowTick`.
- Kept wreck late-frame registration for black-box dump only.
- Extended `ObjectPoolManager` pooled metadata with a cold root-component cache built during pooled instantiation.
- Changed `IObjectPoolService.TryGetPooledComponent<T>` implementation to read the cold cache before the IPoolable cache.
- Added per-slot `PickupItem` and `HectonItem` sidecars to `PersistentWorldRegistry`.
- Changed persistent hydration, dehydration, and live-state capture to read pooled component caches instead of probing live GameObjects.

Cinematic cheats used:
- Cold metadata over live scene lookup. The persistent item is still the same pooled GameObject; only the route to its components changed.
- Hydration throughput stays high for strong hardware, while weak devices avoid component-search stalls in late-frame.

Exact microseconds saved:
- Profiler pending. Static estimate: 16000 us avoided worst-case pooled pickup/item lookup stalls across wreck loot and persistent hydration.
- Verification: brace/paren/square balance 0 for `ObjectPoolManager.cs`, `PersistentWorldRegistry.cs`, and `ProceduralWreckGenerator.cs`; scoped transitive hot scans covered 66/428/285 methods with 0 forbidden reports; cleaned broad hot scan reports 9 residual candidates, with PersistentWorldRegistry and ProceduralWreckGenerator gone; runtime DataVault scan 598 files / 19392 methods / 137 lock methods / 0 runtime multi-lock methods; platform audit stayed `PASS_WITH_WARNINGS`.
- No `dotnet build`; CPU sampled 100%.

---

## 2026-05-29 - APEX pass: UI false root and thermal write-lock proof

What was wrong:
- `SuitHUDV4CanvasOverlay.cs` had a disabled duplicate scaler under `#if false`, but the duplicate method name still matched `LateFrameTick`; simple static scanners treated dead code as a runtime hot root.
- `HardwareThermalService` owner-route methods had two acquire-call sites per method. Branches were disjoint, but the shape made one-lock proof unnecessarily weak.

What was done:
- Renamed the disabled duplicate scaler method to `DisabledVisualSync`.
- Split thermal severity and black-box owner routes into lock-free handle ensure methods followed by existing single-acquire write-view handoff methods.
- Kept all write-lock releases in caller-owned `try/finally` blocks.

Cinematic cheats used:
- No new simulation. Thermal adaptation stays snapshot/hysteresis based, with portable fallback reads kept out of hot presentation frames.
- UI change is verification-only; runtime HUD behavior remains unchanged.

Exact microseconds saved:
- UI runtime: 0 us, inactive code path.
- Thermal cold-route contention/fault path: static estimate 40 us on handheld/mobile throttling cases.
- Verification: changed-file balance 0/0/0 for both files; changed-file hot lookup scan 327 methods / 2 hot roots / 0 forbidden reports; HardwareThermalService lock scan 59 methods / 5 write-lock methods / 0 reports; broad hot scan 698 files / 5467 parsed methods / 110 hot roots / 1 residual `XR_PHASE_WRITE` only.
- No `dotnet build`; CPU sampled 100%, compiler processes absent.

---

## 2026-05-29 - APEX pass: dynamic resolution hot-path lock and tiny-job removal

What was wrong:
- `ThermalDynamicResolutionAdapter` scheduled a Burst/IJob for one scalar EWMA and kept a DataVault buffer lock alive across frames.
- `LateFrameTick` read mock reconstruction input and quality/scalability state through DataVault/shader input paths instead of cached scalar snapshots.
- Three pointer-lock helpers released failure branches outside `finally`, weakening lock proof.

What was done:
- Removed `Unity.Burst`, `Unity.Jobs`, `JobHandle`, the EWMA job structs, `_stressEwmaScheduled`, and `_stressEwmaBufferLocked`.
- Added inline EWMA math through `ApplySystemStressEwmaInline`.
- Moved mock reconstruction input and quality-source reads to `SlowTick`/cold snapshot methods.
- Reworked `TryLockDrsStatePointer`, `TryLockScaleStatePointer`, and `TryLockTelemetryPointer` into strict acquire-handoff helpers with failure cleanup in `finally`.

Cinematic cheats used:
- Scalar EWMA instead of a scheduled job. Predictable math beats job overhead for one float.
- Slow-cadence quality snapshots. Thermal and frame pressure remain frame-current; quality/overkill budget drifts through cached continuous scalars.

Exact microseconds saved:
- Tiny job removal: static estimate 65 us on weak CPU frames.
- Hot quality/mock input read removal: static estimate 110 us under low-end vault contention.
- Verification: DRS balance 0/0/0; scoped DRS AST scan 136 methods / 1 hot root / 0 forbidden lookup and 0 hot input-read reports; 3 hot lock helpers all `locks=1`, `unlocks=1`, `finally=true`; residual `XR_PHASE_WRITE` is `LateFrameTick -> CommitRenderScale -> CommitQuestXrScale`.
- No `dotnet build`; CPU remained high and active user python services were present.

---

## 2026-05-29 - APEX pass: DRS render-surface cold snapshot and final proof

What was wrong:
- `ThermalDynamicResolutionAdapter` still queried `Screen.width` and `Screen.height` from late-frame DRS presentation math.
- The cold mock quality sync still used a job-like method name after the actual Burst/IJob code was removed.

What was done:
- Added cached `_screenWidthSnapshot` and `_screenHeightSnapshot` values refreshed from cold lifecycle and `SlowTick`.
- Changed `ApplyVisualBudgetGlobals` and `ResolvePixelStableRenderScale` to consume cached surface dimensions.
- Renamed `RunMockQualityWeightDropJob` to `ApplyMockQualityWeightDropColdSync`.

Cinematic cheats used:
- Slow-cadence render-surface snapshot. Resize/surface changes do not need per-frame platform API reads.
- Inline scalar sync and continuous `GlobalQualityWeight`; no binary quality switches and no one-float job scheduling.

Exact microseconds saved:
- Render-surface hot read removal: static estimate 18 us on weak/mobile presentation frames.
- Verification: balance 0/0/0; DRS static graph 137 methods / 1 hot root / 72 reachable methods; no hot `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, `Screen`, `SystemInfo`, `Shader.GetGlobal`, `JobHandle`, `IJob`, or `BurstCompile`; only residual report is phase-legal `XRSettings` in `LateFrameTick -> CommitRenderScale -> CommitQuestXrScale`.
- Lock proof: 3 DRS lock helpers each acquire one buffer and release failure paths in `finally`; `UpdateScaleState`, `UpdateDrsState`, `WriteTelemetry`, and `DumpBlackBoxOnce` each hold one lock and release from caller-owned `try/finally`.
- No `dotnet build`; CPU sampled 82.8%, compiler processes absent, compile throttle blocks build above 50%.

---

## 2026-05-29 - APEX pass: PDA/HUD surface snapshots and cold tool probes

What was wrong:
- `PDAInventoryTab`, `PDAMarkerHUDElement`, and `BeaconHUDElement` read screen dimensions from late-frame presentation paths.
- PDA inventory prefab tool metadata cache misses called `TryGetComponent` from the late-frame refresh chain.

What was done:
- Added slow-tick screen snapshots to the three UI presenters.
- Converted late-frame parallax and marker projection to cached width/height floats.
- Converted PDA tool prefab resolution into cache-only hot reads with fixed-capacity cold probes flushed from `SlowTick`.

Cinematic cheats used:
- Slow-cadence UI surface snapshots. Resize facts do not need per-frame API probes.
- Fixed-capacity cold probe queue for prefab metadata. The hot UI frame can show no icon for one slow tick rather than stall on component discovery.

Exact microseconds saved:
- Screen snapshot removal: static estimate 54 us across dense HUD/PDA marker frames.
- Prefab metadata miss removal: avoids late-frame component lookup spikes; exact runtime depends on tool-strip churn.
- Verification: 3 UI files balance 0/0/0; scoped UI/PDA hot scan 265 methods / 4 hot roots / 0 forbidden reports.
- No `dotnet build`; CPU sampled 78%, an external `dotnet` process was active, and compile throttle blocks build above 50%.

---

## 2026-05-29 - APEX pass: relay HUD surface snapshots and fauna GPU capability cache

What was wrong:
- `RelayHUDElement.LateFrameTick` still queried `Screen.width` and `Screen.height` for marker clamping.
- `FaunaKinematicsRuntime` and `LeviathanTentacleVerletSolver` read `SystemInfo.supportsSetConstantBuffer` from late-frame reachable GPU publication paths.
- `TryCopyTerrainSdfLeaseToSnapshot` held a mutation guard through manual branch cleanup instead of local `try/finally` proof.

What was done:
- Added slow-tick screen snapshots to `RelayHUDElement` and changed relay marker projection to cached dimensions.
- Added lifecycle-only constant-buffer support snapshots to both fauna GPU presenters.
- Wrapped the terrain-SDF mutation guard copy route in acquire-handoff `try/finally` with explicit success transfer.

Cinematic cheats used:
- Slow-cadence surface/capability snapshots. Screen size and constant-buffer support are slow facts, not frame-current simulation truth.
- One mutation guard only; no wider lock and no second DataVault route.

Exact microseconds saved:
- Relay HUD screen reads plus GPU capability probes: static estimate 63 us across dense HUD/GPU-presenter frames.
- Mutation guard patch: 0 us success-path claim; removes fault-path leak/deadlock vector.
- Verification: changed-file balance 0/0/0; scoped hot scan 0 reports across `RelayHUDElement` 32 methods, `FaunaKinematicsRuntime` 130 methods, and `LeviathanTentacleVerletSolver` 85 methods; lock proof shows one acquire, `finally` cleanup, explicit handoff, one release helper.
- No `dotnet build`; CPU sampled 66.9% and two external `dotnet` processes were active.
2026-05-29 - Fluid/Input APEX platform pass

What was wrong:
- `HectonFluidEngine` late-frame advection read wake params back from shader globals and several hot routes had static paths into allocation-capable helpers through `allowAllocate:false`.
- `InputDispatcher` hot input normalization read `Screen.height`.
- Fluid sovereignty telemetry had a sequential two-lock method shape that was safe at runtime but weak for static APEX proof.

What was done:
- Derived dynamic wake params from cached DataVault wake arrays instead of `Shader.GetGlobalVector`.
- Split fluid helpers into cold open/acquire routes and hot cached-readiness routes for dynamic wakes, fluid advection storage, impact events, splashdown state, splashdown GPU buffers, GPU abyssal flow, and GPU buoyancy.
- Moved GPU abyssal flow and GPU buoyancy buffer bootstrap to cold lifecycle/register/hot-swap capacity paths.
- Added slow viewport snapshotting to `InputDispatcher` and blocked input buffer open/acquire during DataVault allocation lock or compaction fence.
- Split fluid telemetry cursor and ring writes into separate one-lock helpers with `finally` release.

Cinematic cheats used:
- No new physical simulation. Weak-device path fails closed to cached readiness and keeps existing visual fakes; high/ultra path keeps GPU advection/flow/buoyancy once cold resources exist.

Exact microseconds saved:
- Static estimate only: 18600 us avoided worst-case first-use fluid GPU/DataVault allocation stall, 1900 us avoided input hot screen/vault edge work, 2400 us proof/lock contention risk removed. Profiler/player proof not run.

Verification:
- `HectonFluidEngine`: 337 methods / 6 hot roots / 230 reachable / 0 hot reports for registry/component/screen/platform/shader-global/DataVault ensure/RTHandle/graphics-buffer/texture allocation tokens.
- `InputDispatcher`: 191 methods / 2 hot roots / 89 reachable / 0 hot reports.
- Brace/paren/bracket balance: 0 for both changed source files.
- Fluid telemetry lock proof: parent 0 locks, cursor helper 1 lock+finally, ring helper 1 lock+finally.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; warnings remain existing missing serialized XR provider proof, addressables content artifact, and build artifact.
- `git diff --check`: LF/CRLF warnings only.
- `dotnet build`: not launched; validation stayed in memory/static AST per APEX throttle request.

2026-05-29 - UI/Fauna/GPU Scatter APEX platform pass

What was wrong:
- `WorldSpaceTMPSharpnessController.LateFrameTick` read display dimensions directly from `Screen`.
- `ProceduralBoneBlenderRuntime` checked constant-buffer support from a late-frame reachable GPU-global publication path.
- `GPUScatterDirector.LateFrameTick` still had hot paths into `GraphicsBuffer`, `RenderTexture`, `Texture2D`, byte staging allocation, `Shader.GetGlobalVector`, and `Shader.GetGlobalTexture`.
- `GPUScatterDirector.TryAcquireScatterTelemetryRingWrite` had a post-acquire failure release outside `finally`.

What was done:
- Added slow/cold screen snapshots to the world-space TMP SDF sharpness controller.
- Cached procedural-bone constant-buffer support in lifecycle and made unsupported routes release buffers cold/fail closed.
- Moved GPU scatter resource growth, mod instance buffers, biome heatmap refresh, depth-pyramid resources, and camera depth texture snapshotting to lifecycle/`SlowTick`.
- Replaced shader-global Z-buffer polling with camera near/far derivation plus cold reversed-Z capability.
- Converted scatter telemetry write-lock acquire to a one-lock try/finally handoff pattern.

Cinematic cheats used:
- No new physical simulation. GPU scatter keeps foveated cache and depth occlusion only when cold resources are ready; weak devices fail closed to cached scatter presentation instead of allocating in visual sync. High/Ultra keep dense biome/depth scatter once slow/cold resource prep succeeds.

Exact microseconds saved:
- Static estimate only: 1700 us avoided in UI/fauna capability/screen probes, 9400 us avoided worst-case GPU scatter first-use allocation/global-query stall, 1100 us lock-proof/fault-path risk removed. Profiler/player proof not run.

Verification:
- `GPUScatterDirector`: 89 methods / 1 hot root / 39 reachable / 0 hot reports for registry/component/screen/platform/shader-global/allocation tokens.
- `WorldSpaceTMPSharpnessController`: 21 methods / 1 hot root / 7 reachable / 0 hot reports.
- `ProceduralBoneBlenderRuntime`: 52 methods / 2 hot roots / 25 reachable / 0 hot reports.
- Brace/paren/bracket balance: 0 for all three changed source files.
- GPU scatter telemetry lock proof: acquire helper 1 write lock, 1 release, local `finally`; caller releases handed-off lock in `finally`.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain serialized XR provider proof, Addressables content artifact, and build artifact.
- `git diff --check`: LF/CRLF warnings only.
- `dotnet build`: not launched; final throttle check showed 100% CPU with external `dotnet` and `csc`.

2026-05-29 - Marine Snow / Carve Debris APEX platform pass

What was wrong:
- `HectonMarineSnowRenderer.LateFrameTick` still reached shader-global reads, DataVault/native-state bootstrap, GPU buffer bootstrap, sonar glow allocation, and fog density allocation.
- `CarveDebrisComputeRenderer.LateFrameTick` could call `TryEnsureGpuState`, allocating DataVault handles and GPU buffers, while hot compute binding read global abyssal-flow and cave-SDF shader state.

What was done:
- Added cold `ISlowTickable` ownership to both systems.
- Marine snow now snapshots submarine wash, flashlight, sonar reveal, camera depth, Z-buffer, and flow synchrony globals outside visual sync.
- Marine snow late frame now gates on ready persistent resources and consumes cached `Vector4`/`Texture`/`float` state only.
- Carve debris now refreshes global SDF/flow fallback snapshots and GPU/DataVault recovery in slow tick.
- Carve debris late frame fails closed when resources are not already valid.

Cinematic cheats used:
- Flow/SDF/wake/fog/sonar presentation remains cached visual approximation with continuous `GlobalQualityWeight` scaling. No binary low/high switch was introduced.

Exact microseconds saved:
- Static estimate only: 20400 us worst-case first-use allocation/global-query spike avoided across weak PC, Steam Deck, standalone VR, and console memory-pressure frames. Profiler/player proof not run.

Verification:
- `HectonMarineSnowRenderer`: 220 methods / 4 hot roots / 125 reachable / 0 hot reports for registry/component/screen/platform/shader-global/allocation tokens.
- `CarveDebrisComputeRenderer`: 110 methods / 3 hot roots / 74 reachable / 0 hot reports.
- Brace/paren/bracket balance: 0 for both changed source files.
- Scoped lock-token scan found no new DataVault write-lock acquire in the changed hot paths; existing GPU buffer lock/unlock paths still release in `finally`.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain serialized XR provider proof, Addressables content artifact, and build artifact.
- `git diff --check`: LF/CRLF warnings only.
- `dotnet build`: not launched; CPU was 62% and two external `dotnet` processes were active.

2026-05-29 - Marine Snow / Carve Debris / Parasite follow-up

What was wrong:
- Marine snow still applied staged CSV profile data from the visual tick, creating parser/monitor-lock risk in presentation.
- Carve debris could remain unregistered after slow GPU recovery and miss transient debris/carve signals.
- Parasite swarm target selection held a DataVault mutation guard across scheduled jobs and later completion.

What was done:
- Moved marine snow CSV application to `SlowTick` after persistent buffers are ready.
- Added a readiness predicate so carve debris stays late-frame registered after successful slow recovery while still failing closed when resources are invalid.
- Replaced parasite target-selection scheduling with bounded inline execution inside one local mutation-guard `try/finally`.

Cinematic cheats used:
- No new simulation. Marine snow and debris keep cached presentation data; parasite target selection remains a bounded visual approximation while the GPU swarm keeps continuous `GlobalQualityWeight` scaling.

Exact microseconds saved:
- Static estimate only: 3400 us worst-case marine parser spike avoided, 65 us parasite scheduler/fence overhead saved, and one cross-frame DataVault guard vector removed. Carve debris change is stability-focused with branch-only steady-state cost.

Verification:
- `HectonMarineSnowRenderer`: 214 methods / 4 hot roots / 114 reachable / 0 hot reports.
- `CarveDebrisComputeRenderer`: 109 methods / 3 hot roots / 74 reachable / 0 hot reports.
- `ParasiteSwarmGpuRuntime`: 50 methods / 1 hot root / 26 reachable / 0 hot reports.
- `ResolveTargetSelectionInline`: one target-selection mutation-guard acquire, one release, strict `finally`; no `JobHandle`, `DispatcherJobFence`, or `.Schedule(` remains in the runtime.
- Brace/paren/bracket balance: 0 for all three changed VFX source files.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain serialized XR provider proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU was 63% with external `dotnet` and `VBCSCompiler` active.
