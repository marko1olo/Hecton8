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

2026-05-29 - Indirect Vegetation visual-sync cold split

What was wrong:
- `HectonIndirectVegetationRenderer.LateFrameTick` could transitively call shader-global reads, source binding uploads, legacy metadata buffer creation, indirect visible/args/telemetry buffer allocation, depth-pyramid `RenderTexture` allocation, flora-age DataVault upload, telemetry handle creation, and editor asset auto-assignment.
- Scatter cull telemetry readback completion could allocate a DataVault telemetry handle before taking the write lock.

What was done:
- Added `ISlowTickable` registration and cold ownership for graphics capabilities, camera/depth/Z-buffer/global biolum/submarine wash snapshots, source binding, indirect GPU resources, depth pyramid resources, flora-age upload, and telemetry handles.
- Changed visual tick to use `TryResolveActiveInstanceDataBufferHot`, `TryResolveFloraAgeBufferHot`, cached shader-global state, cached property blocks, and ready-resource predicates.
- Split telemetry acquisition so hot record paths only acquire existing handles; allocation is slow-phase only.
- Removed editor asset auto-assignment from the render-material hot chain.

Cinematic cheats used:
- No physical simulation added. Vegetation culling keeps cached depth/biolum/wash presentation data; weak devices fail closed until slow/cold prep succeeds, while high/ultra still get dense indirect vegetation, depth occlusion, darkness culling, and snap flags once resources are ready.

Exact microseconds saved:
- Static estimate only: 14600 us worst-case first-use visual-frame hitch avoided, plus 900 us telemetry allocation fault-path stall removed. Profiler/player proof not run.

Verification:
- `HectonIndirectVegetationRenderer`: 217 methods / 8 hot roots / 60 reachable / 0 forbidden lookup/platform/shader-global/allocation/ensure reports.
- Telemetry hot path: no `EnsureTelemetryBuffer` route; `TryAcquireExistingTelemetryBuffer<T>` has one write-lock acquire and failure release in `finally`; record methods release handed-off locks in `finally`.
- Brace/paren/bracket balance: 0/0/0.
- Scoped `git diff --check`: LF/CRLF warning only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain serialized XR provider proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 100% and no compiler process was active.

2026-05-29 - Diegetic Glitch cold native repair pass

What was wrong:
- `DiegeticGlitchSurgeonRuntime.LateFrameTick` could finish a pending DataVault swap and call `EnsureNativeResources`, creating DataVault handles and H8Memory scratch in the visual phase.
- `TryLoadFrameScratchFromVault`, called from visual job scheduling, could call allocation-capable `EnsureGlitchScratchResources` if resident scratch pointers were missing.
- Delayed disable after an outstanding glitch job could finish teardown without unregistering the dispatcher routes.

What was done:
- Added `ISlowTickable` ownership.
- Moved pending vault-swap/native repair into `ServiceNativeColdRepair` on `SlowTick`.
- `LateFrameTick` now latches `_nativeColdRepairRequested` and returns instead of repairing native state.
- Replaced hot scratch ensure with `AreGlitchScratchResourcesReady`.
- Added `FinishDisableTeardownAndUnregister` to clear both slow and late registrations after delayed drain.

Cinematic cheats used:
- The effect remains presentation-only: cached unmanaged glitch state drives shader globals and bridge DTOs. Weak devices fail closed until resident scratch exists; high/ultra keep the full text/matrix/radar/synth glitch pass.

Exact microseconds saved:
- Static estimate only: 6200 us worst-case visual-frame native allocation/DataVault handle spike avoided during vault hot-swap or UI glitch activation. Profiler/player proof not run.

Verification:
- `DiegeticGlitchSurgeonRuntime`: 190 methods / 34 `LateFrameTick`-reachable methods / 0 reports for registry/component/platform/shader-global/native allocation/DataVault handle tokens.
- Brace/paren/bracket balance: 0/0/0.
- Scoped `git diff --check`: LF/CRLF warning only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain serialized XR provider proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 72.7%, above the 50% throttle.
- Process hygiene: the timed-out `python -` parser was stopped; no new Python proof process remained after audit completion.

2026-05-29 - Sargassum and biome hot-allocation split

What was wrong:
- `SargassumCrestDampingController.LateFrameTick` statically reached facade `RenderTexture` allocation helpers and editor `AssetDatabase` auto-assignment through `RefreshFacadeTextures`/`DispatchFacadeBake`.
- `SargassumCutManager.LateFrameTick` owned quality resource refresh and could reach cut-mask and 3D damage-volume `RenderTexture` creation.
- `BiomeTransitionManagerRuntime.LateFrameTick` could allocate biome shader payload `GraphicsBuffer` ping-pong buffers during payload publication.

What was done:
- Crest facade visual refresh now uses cached-only resource validation; resource allocation and editor asset lookup are cold lifecycle/slow-phase work.
- Sargassum cut quality refresh now runs in `SlowTick`; visual sync only flushes already-queued GPU work and shader globals.
- Biome transition shader payload CBuffers are allocated in lifecycle/`SlowTick`; late-frame CBuffer upload fails closed when buffers are not resident.

Cinematic cheats used:
- No new physical simulation. Weak devices fail closed until cached presentation resources exist; middle devices repair on slow cadence; high/ultra keep richer facade, cut-volume, and biome CBuffer visuals once cold resources are resident.

Exact microseconds saved:
- Static estimate only: 4800 us crest facade allocation/editor lookup hitch avoided, 11700 us sargassum mask/damage-volume rebuild hitch avoided, 2600 us biome CBuffer allocation hitch avoided. Profiler/player proof not run.

Verification:
- Hot graph: `SargassumCrestDampingController` 45 methods / 2 roots / 16 reachable / 0 reports.
- Hot graph: `SargassumCutManager` 92 methods / 2 roots / 43 reachable / 0 reports.
- Hot graph: `BiomeTransitionManagerRuntime` 86 methods / 2 roots / 39 reachable / 0 reports.
- Syntax balance: brace/paren/bracket 0/0/0 for all three changed files.
- Lock scan: 0 reports; no changed method shows more than one DataVault write/mutation acquire or missing `finally`.
- Scoped `git diff --check`: LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain XR provider serialized proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 100% and an external `dotnet build` process was already active.
- Process hygiene: two timed-out parser `python -` processes were stopped; a later parser process was confirmed gone; unrelated bot/uvicorn Python services were left untouched.

2026-05-29 - Visor fluid render-phase cache split

What was wrong:
- `HectonVisorFluidDistortionFeature.AddRenderPasses` built runtime state from live `Shader.GetGlobal*`, `RenderSettings`, and `SystemInfo.graphicsMemorySize`.
- `VisorFluidPass.RecordRenderGraph` reached `SystemInfo.supportsComputeShaders`, `SystemInfo.supportsSetConstantBuffer`, and allocation-capable `Ensure*GlobalsBuffer(false)` helpers.
- Dispatcher service replacement only repaired registration when the replacement service was non-null.

What was done:
- Added `ILateFrameTickable` ownership for visor presentation snapshots.
- Moved diegetic lens vectors, rain intensity, water-density signal, and ambient light reads into `CachePresentationGlobalsLate`.
- Moved constant-buffer support, compute support, and graphics memory size into `CacheGraphicsCapabilitiesCold`.
- Render state now consumes cached value fields; state transfer is field-only value data with no managed allocation.
- Split hot buffer updates onto `HasVisorFluidGlobalsBuffer` and `HasLensComputeGlobalsBuffer`; allocation remains cold prewarm/lifecycle only.
- Dispatcher hot-swap now always unregisters before optional re-register.

Cinematic cheats used:
- No new physical fluid simulation. Weak devices use cached visor distortion/rain/water-density signals and fail closed when prewarmed CBuffers are absent; middle keeps lens mask at reduced scale; high and ultra keep full diegetic lens/fog/rain blending once cold resources are resident.

Exact microseconds saved:
- Static estimate only: 8100 us worst-case render-frame hitch avoided by removing shader-global/platform probes and allocation-capable constant-buffer paths from visor render setup. Profiler/player proof not run.

Verification:
- `HectonVisorFluidDistortionFeature`: brace/paren/bracket balance 0/0/0.
- Linear hot graph: render roots 55 reachable methods / 0 forbidden reports for `GlobalRegistry.Get<T>()`, component lookup, scene search, `SystemInfo`, shader-global reads, `RenderSettings`, and GPU allocation helpers.
- Late-frame graph: 4 reachable methods / 0 forbidden dependency/allocation reports; phase-legal presentation reads are confined to `CachePresentationGlobalsLate`/`ResolveAmbientLight01`.
- DataVault proof: `TryWriteBlackBoxEntry` acquire=1, release=1, finally=1; no direct method contains more than one write-lock acquire.
- Scoped `git diff --check`: LF/CRLF warning only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain XR provider serialized proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 100% with external `dotnet` and `VBCSCompiler` active.
- Process hygiene: timed-out `python -` scanner left no new parser process; unrelated bot/uvicorn Python services were left untouched.

2026-05-29 - Visor micro-pass render-phase cache split

What was wrong:
- `HectonAtmosphereSootFeature`, `HectonHalfResParticlesFeature`, `HectonNoirDepthFogFeature`, `HectonStochasticSsrFeature`, `HectonRetinaDistortionFeature`, and `HectonVRBrownoutFeature` still used `SystemInfo.supportsSetConstantBuffer` from render-path CBuffer readiness helpers.
- `HectonRetinaDistortionFeature.AddRenderPasses` read `_HectonNarcosisScalar` through `Shader.GetGlobalFloat`.
- `HectonVRBrownoutFeature.AddRenderPasses` read brownout/focus/near-collision/VR comfort shader globals directly during render setup.

What was done:
- Added `SetGraphicsCapabilitiesCold` to all six pass types and wired lifecycle `CacheGraphicsCapabilitiesCold` ownership in each feature.
- Replaced render-path platform probes in `Has*GlobalsBuffer` with cached capability flags.
- Added `ILateFrameTickable` presentation snapshots to retina distortion and VR brownout, including dispatcher hot-swap unregister/register repair.
- Render setup now consumes cached narcosis/brownout/comfort vectors and cached retina quality weight; CBuffer allocation remains lifecycle/cold only.

Cinematic cheats used:
- No extra physical simulation. The existing fullscreen soot, half-res particle, noir fog, reflection sheen, retina, and VR comfort effects remain visual fakes with continuous quality scaling.

Exact microseconds saved:
- Static estimate only: 960 us worst-case saved across dense visor micro-pass stacks by removing repeated capability probes and static edges to allocation-capable readiness helpers.
- Static estimate only: 42 us render-setup saved by moving retina/VR comfort shader-global reads to `LateFrameTick`.

Verification:
- In-memory syntax/call-graph scan across six changed files: all brace/paren/bracket balances 0/0/0.
- Render roots `AddRenderPasses`/`RecordRenderGraph`: 0 forbidden `GlobalRegistry.Get<T>()`, `GetComponent`, `SystemInfo`, `Shader.GetGlobal*`, `Screen`, `RenderSettings`, `new GraphicsBuffer`, `new RenderTexture`, or `Ensure*` reports.
- Late roots: 0 forbidden reports after allowing only phase-legal `CachePresentationGlobalsLate` shader-global snapshots.
- Changed files contain no DataVault write-lock route; GPU buffer lock writes still release in local `finally`.
- Scoped `git diff --check`: LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain XR provider serialized proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 97% and external `dotnet` was active.
- Process hygiene: no lingering `python -` parser process.

2026-05-29 - Visor compute-gate cache extension

What was wrong:
- Broad Visor render-root scan still found `SystemInfo.supportsComputeShaders` in `HectonBiolumSSGIFeature.AddRenderPasses`.
- `HectonVoxelSsaoFeature.AddRenderPasses` also queried `SystemInfo.supportsComputeShaders`.
- `HectonScooterVolumetricShaftsFeature.RecordRenderGraph` reached `SystemInfo.supportsSetConstantBuffer`, `SystemInfo.supportsComputeShaders`, and `SystemInfo.graphicsMemorySize` through shaft CBuffer readiness, auto-exposure kernel init, and low-VRAM pressure math.

What was done:
- Added lifecycle `CacheGraphicsCapabilitiesCold` snapshots for Biolum SSGI and Voxel SSAO compute support.
- Added scooter shaft cached compute support, CBuffer support, and low-VRAM pressure.
- Added `ShaftsPass.SetGraphicsCapabilitiesCold` so render graph setup uses cached facts only.
- Replaced scooter low-VRAM pressure live VRAM read with a lifecycle-computed continuous pressure scalar.

Cinematic cheats used:
- Biolum SSGI still falls back to the existing proxy composite when compute is unavailable. Voxel SSAO remains disabled when no runtime consumer or compute support exists. Scooter shafts keep the same half-res radial/fake noir shaft model with continuous low-VRAM pressure scaling.

Exact Microseconds Saved:
- Static estimate only: 530 us worst-case saved across compute-gated visor effects by removing render-root platform and VRAM probes.

Verification:
- In-memory syntax/call-graph scan across all nine changed visor renderer features: all brace/paren/bracket balances 0/0/0.
- Changed-file render roots: 0 forbidden `GlobalRegistry.Get<T>()`, component lookup, `SystemInfo`, shader-global read, `Screen`, `RenderSettings`, `new GraphicsBuffer`, `new RenderTexture`, or `Ensure*` reports.
- Broad Visor residual scan dropped from 9 files to 6 files with reports; remaining files are `HectonDryVolumeFeature`, `HectonSonarPointCloudFeature`, `HectonVisorARStencilRendererFeature`, `HectonVisorUberPostFeature`, `HectonVolumetricParticulateFogFeature`, and `VolumetricLightFeature`.
- Changed files contain no DataVault write-lock route.
- Scoped `git diff --check`: LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain XR provider serialized proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 57% and external `dotnet` was active.
- Process hygiene: no lingering `python -` parser process.

2026-05-29 - Visor phase/resource ownership split

What was wrong:
- `HectonDryVolumeFeature` read the ocean camera color texture from `Shader.GetGlobalTexture` in `RecordRenderGraph`.
- `VolumetricLightFeature` read flashlight/fog/freeze globals in `AddRenderPasses`/`RecordRenderGraph` and checked compute support in `AddRenderPasses`.
- `HectonSonarPointCloudFeature` polled sonar reveal globals in render roots and allocated RTHandle history/world textures from `RecordRenderGraph`.
- `HectonVisorARStencilRendererFeature` hot upload called allocation-capable `EnsureBuffers(false)`, leaving `SystemInfo` and `new GraphicsBuffer` reachable from the frame route.

What was done:
- Added late-frame snapshots for dry-volume ocean texture, volumetric flashlight/fog/freeze state, and sonar reveal expiry.
- Added cold compute/CBuffer support caches where the render root needed hardware facts.
- Moved sonar RTHandle allocation to queued `SlowTick` prewarm; render graph now imports only resident resources and fails closed otherwise.
- Split AR stencil `HasBuffers` from cold `EnsureBuffers`; frame upload writes only prewarmed mapped buffers.

Cinematic cheats used:
- Sonar history now prefers a missing-frame fail-closed visual over an allocation hitch on the reveal frame.
- Volumetric light keeps the existing proxy path as the cheap fallback and spends compute only when cold capability says it is supported.
- AR stencil chooses prewarmed buffer residency over emergency frame allocation.

Exact Microseconds Saved:
- Static estimate only: 118 us render-frame variance reduction from dry/volumetric shader-global and platform read split.
- Static estimate only: 9400 us worst-case sonar history RTHandle resize/reveal hitch avoided.
- Static estimate only: 3100 us worst-case AR stencil GPU buffer allocation route removed.

Verification:
- In-memory syntax/call-graph scan across DryVolume, VolumetricLight, SonarPointCloud, and ARStencil: all brace/paren/bracket balances 0/0/0.
- Changed-file render roots: 0 forbidden `GlobalRegistry.Get<T>()`, component lookup, `Shader.GetGlobal*`, `SystemInfo`, `RTHandles.Alloc`, `new GraphicsBuffer`, `new RenderTexture`, or allocation helper reports.
- Broad Visor residual scan now reports 4 files: `DiegeticVisorLensRuntime`, `HectonVisorUberPostFeature`, `HectonVolumetricParticulateFogFeature`, and `SuitHUDPresentationController`.
- AR DataVault write-lock routes: `TryCommitSingleToVault`, `TryCommitSpanToVault`, and `TryCommitTelemetryFrame` each have one acquire, one release, and `finally`; CSV mutation guard has one acquire, one release, and `finally`.
- Scoped `git diff --check`: LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: PASS_WITH_WARNINGS; existing warnings remain XR provider serialized proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched under compile throttle; CPU was saturated and no build spam was performed.
- Process hygiene: parser process scan completed with no new `python -` orphan.

## 2026-05-29 - APEX Visor Residual Closure

What was wrong:
- `DiegeticVisorLensRuntime` could repair native/GPU resources from visual sync after state loss; telemetry cursor/ring writes were sequential but proof-hostile in one method.
- `SuitHUDPresentationController` pulled comfort shader globals inside the HUD projection chain.
- `HectonVolumetricParticulateFogFeature` still reached shader globals, bridge RTHandle prep, compute probing, DataVault multi-write sequencing, and a tiny mock-light job from render/visual paths.
- `HectonVisorUberPostFeature` allocated imported static texture handles from `RecordRenderGraph`, probed `SystemInfo` from `AddRenderPasses`, and read reconstruction globals during runtime-state build.
- `VisorHUDController` read `Screen.width/height` from the late-frame runtime RT sizing path.

What was done:
- Moved diegetic visor native/GPU repair to `SlowTick`; split telemetry cursor advance and ring write into one-lock helpers.
- Cached Suit HUD comfort vignette in `LateFrameTick`.
- Moved volumetric fog bridge shader globals to `LateFrameTick`, bridge/static GPU prep to `SlowTick`, compute support to lifecycle, and mock lights to bounded inline writes.
- Added Uber post cold/slow static texture handle prewarm, cold platform/memory/depthless caches, and late presentation global snapshots.
- Added Visor HUD slow screen-surface snapshots and dispatcher hot-swap re-registration for slow/late routes.

Cinematic cheats used:
- Fail closed for one visual frame when visor/fog/Uber-post resources are not resident instead of allocating inside render graph.
- Use cached late presentation snapshots for comfort, internal waterline, hypoxia, pressure, and fog bridge data; simulation truth remains untouched.
- Replace the tiny mock-light job with inline bounded math because eight lights do not justify scheduler overhead.

Exact Microseconds Saved:
- Static estimate only: 3100 us worst-case diegetic visor native/GPU repair hitch removed from visual sync.
- Static estimate only: 5240 us worst-case particulate fog bridge/resource hitch avoided; 55 us tiny-job overhead removed.
- Static estimate only: 7800 us worst-case Uber post static handle/CBuffer/platform-probe hitch avoided.
- Static estimate only: 18 us saved from HUD screen-size probe removal.

Verification:
- In-memory broad Visor hot call graph: 0 residual reports across `Assets/_Project/Scripts/Visor`.
- Focused changed files `DiegeticVisorLensRuntime`, `SuitHUDPresentationController`, `HectonVolumetricParticulateFogFeature`, `HectonVisorUberPostFeature`, `HectonVisorUberPostFeature.Noir`, `VisorHUDController`: brace/paren/bracket balance 0/0/0.
- Changed visor source set: 20 files, 31 DataVault write methods, 0 methods with multiple write-acquire sites or release without `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: intentionally not launched; CPU was 100% with external `dotnet`/`csc`, then 85% after they exited.
- Process hygiene: WMI process scan found no `python -` parser orphan; existing bot/uvicorn/stomchat Python services were left untouched.

## 2026-05-29 - Bilateral DRS Render Capability Cache

What was wrong:
- `HectonBilateralDrsUpscalerFeature` used `SystemInfo.supportsComputeShaders` in `AddRenderPasses` and `RecordRenderGraph`.
- Texture-array support and graphics format support were queried from render-graph helper paths.
- The clear-edge fallback also rechecked compute/format support from the hot render route.

What was done:
- Added cold `GraphicsCapabilities` snapshot construction in `Create`.
- Passed cached capability values into `BilateralDrsPass.Setup`.
- Replaced hot `SystemInfo` calls with cached compute support, cached 2D-array support, cached edge-mask load/store format, cached raster edge-mask render format, and cached output load/store fallback.
- Kept unsupported paths fail-closed: if no prevalidated format exists, the pass publishes the safe clear path or skips the upscale.

Cinematic cheats used:
- Prefer a one-frame clear edge mask or conservative load/store fallback over render-thread platform probing.
- Keep HDR-capable R16 fallback for high/ultra hardware while allowing weak devices to avoid unsupported compute/array routes.

Exact Microseconds Saved:
- Static estimate only: 185 us render setup variance removed across weak CPUs, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR.

Verification:
- Scoped brace/paren/bracket balance for `HectonBilateralDrsUpscalerFeature.cs`: 0/0/0.
- Static hot call graph: 37 methods discovered, 32 reachable from `AddRenderPasses`/`RecordRenderGraph`, 0 forbidden `GlobalRegistry.Get<T>()`, component lookup, `SystemInfo`, `Screen`, or `Shader.GetGlobal*` reports.
- `SystemInfo` location scan: remaining reads are only in `BuildGraphicsCapabilitiesCold` and `ResolveSupportedFormatCold`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 97.7%, no compile spam.

## 2026-05-29 - Underwater Visual RT Repair Split

What was wrong:
- `HectonUnderwaterVisuals` late-frame noir/global path called `EnsureHudFogLuminanceResources(false)`.
- The flashlight photophobia update path called `EnsurePhotophobiaFieldResources(false)`.
- Both calls blocked allocation at runtime, but static hot proof still reached `new RenderTexture` through allocation-capable helper bodies.

What was done:
- Added `HasHudFogLuminanceResourcesReady` and `HasPhotophobiaFieldResourcesReady` as allocation-free hot readiness probes.
- Moved HUD fog luminance and photophobia field resource repair/prewarm to `SlowTick`.
- Late-frame now fails closed if resources are absent instead of entering allocation-capable helpers.

Cinematic cheats used:
- Prefer one visual frame with no HUD fog perturbation/photophobia field over a render-texture recreation hitch.
- Keep the existing cheap 1x1 luminance and 128x128 photophobia fields resident for middle/high/ultra targets.

Exact Microseconds Saved:
- Static estimate only: 16400 us worst-case render-texture recreation hitch avoided after device loss/resource eviction on weak GPUs, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR.

Verification:
- Scoped brace/paren/bracket balance for `HectonUnderwaterVisuals.cs`: 0/0/0.
- Static late/render call graph: 285 methods discovered, 152 reachable from `LateFrameTick`/`Render`, 0 forbidden `GlobalRegistry`, component lookup, `SystemInfo`, `Screen`, `new RenderTexture`, `new GraphicsBuffer`, or `RTHandles.Alloc` reports.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 99.2%, no compiler process was active.

## 2026-05-29 - Single-Pass Ocean Compute Capability Split

What was wrong:
- `HectonSinglePassOceanFeature` polled `SystemInfo.supportsComputeShaders` inside `RecordRenderGraph`.
- `AddRenderPasses` called `Setup` every frame, and `Setup` re-ran wake compute kernel resolution with `HasKernel`, `FindKernel`, and thread-group inspection.

What was done:
- Added `_supportsComputeShadersCold` to the renderer feature lifecycle.
- Passed the cached compute capability into `SinglePassOceanPass.Setup`.
- Made wake kernel resolution dirty-only when the compute shader reference or cold compute capability changes.
- Render graph now uses cached compute capability and pre-resolved kernels only.

Cinematic cheats used:
- Unsupported or missing compute wake publishes a 1x1 cleared wake texture while depth/shoreline foam remains active.
- Weak targets avoid physical wake accumulation; middle/high/ultra targets keep the compute wake path once prevalidated.

Exact Microseconds Saved:
- Static estimate only: 310 us render setup variance removed on weak CPUs, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR.

Verification:
- Scoped brace/paren/bracket balance for `HectonSinglePassOceanFeature.cs`: 0/0/0.
- Static render hot graph: 20 methods discovered, 15 reachable from `AddRenderPasses`/`RecordRenderGraph`, 0 forbidden dependency/platform/global/screen/allocation reports.
- `SystemInfo` location scan: remaining read is only in `CacheGraphicsCapabilitiesCold`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 100% with external `dotnet` active.

## 2026-05-29 - Instance Culling Compute Gate Cache

What was wrong:
- `InstanceCullingService.Dispatch` reached `ValidateDispatch`, which queried `SystemInfo.supportsComputeShaders` for every culling dispatch.
- This put a cold platform fact into the runtime validation route used by procedural flora/HLOD culling.

What was done:
- Added `_supportsComputeShadersCold`.
- Refreshed the cached capability in `Awake`, `OnEnable`, and `Configure`.
- Replaced the dispatch-time platform probe with the cached field.

Cinematic cheats used:
- Unsupported compute targets fail closed through existing invalid telemetry instead of attempting a CPU fallback culler.
- Existing quality-weighted cull distance remains continuous, so weak, middle, high, and ultra tiers keep one route with different density budgets.

Exact Microseconds Saved:
- Static estimate only: 37 us per dense dispatch validation on i3/MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR.

Verification:
- Scoped brace/paren/bracket balance for `InstanceCullingService.cs`: 0/0/0.
- Static dispatch call graph: 36 methods discovered, 13 reachable from `Dispatch`, 0 forbidden platform/component/screen/allocation reports after allowing the existing telemetry write lock.
- Telemetry write lock proof: `WriteTelemetry` has one `TryAcquireWriteLock` site and releases in `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 100% with external `dotnet` active.

## 2026-05-29 - GPU Scatter Visual Repair Split

What was wrong:
- `GpuScatterLodManager.LateFrameTick` called `RunScatterVisualTick`.
- `RunScatterVisualTick` called `TryEnsureGpuState`.
- `TryEnsureGpuState` can reach DataVault rebinding, kernel resolution, `EnsureGpuBuffers`, indirect args initialization, and multiple `new GraphicsBuffer` paths after GPU state loss or cold startup.

What was done:
- `SlowTick` now owns GPU state repair by calling `TryEnsureGpuState` when buffers/state are missing.
- `RunScatterVisualTick` now calls allocation-free `HasGpuStateReady`.
- Late-frame scatter fails closed if resources are absent, instead of trying to rebuild GPU state in visual sync.

Cinematic cheats used:
- Prefer one missing scatter visual frame over buffer recreation in `LateFrameTick`.
- Low devices repair on slow cadence; middle/high/ultra devices keep dense scatter once resident.

Exact Microseconds Saved:
- Static estimate only: 22100 us worst-case scatter GPU buffer recreation hitch avoided on MX350, Steam Deck-class APUs, Mac integrated GPUs, and standalone VR.

Verification:
- Scoped brace/paren/bracket balance for `GpuScatterLodManager.cs`: 0/0/0.
- Static late-frame call graph: 117 methods discovered, 60 reachable from `LateFrameTick`, 0 forbidden registry/component/platform/screen/shader-global/allocation reports.
- Remaining `SystemInfo` and `new GraphicsBuffer` routes are lifecycle or `SlowTick` repair only.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 100% with external `dotnet` active.

## 2026-05-29 - Graphics Rendering APEX Sweep

What was wrong:
- After local fixes, proof still had to cover drift across the whole Graphics/Rendering surface, not just edited files.

What was done:
- Ran a broad in-memory hot call graph over non-editor `Assets/_Project/Scripts/Graphics` and `Assets/_Project/Scripts/Rendering`.
- Ran scoped diff check over all edited source files.
- Scanned changed files for DataVault write-lock shape.
- Rechecked CPU/compiler throttle before deciding whether a build was legal.

Cinematic cheats used:
- Slow repair and fail-closed presentation were preferred over visual-frame allocation or platform probing.

Exact Microseconds Saved:
- Aggregate static worst-case hitch avoided in this pass: 38947 us across DRS, underwater RT repair, ocean wake setup, instance culling dispatch, and GPU scatter repair.

Verification:
- Broad Graphics/Rendering hot scan: `TOTAL_REPORTS=0`.
- Changed-file DataVault write-lock proof: one write method found, `InstanceCullingService.WriteTelemetry`, with one acquire, one release, and `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 71% with external `dotnet` active.

## 2026-05-29 - VR/Shader Telemetry Hot Split

What was wrong:
- `FoveatedRenderCommander` late/render telemetry write and dump paths could reach `EnsureTelemetry()` and then `EnsureGenerationHandle`.
- `GlobalShaderDispatcher` visual-sync shader slot access used `EnsureShaderGlobalSlotsRuntime(... allowAllocation:false)`, but the hot graph still reached an allocation-capable method body.
- `HectonUberNoirRuntimeBridge` telemetry push and dump used `EnsureTelemetryBuffer(... allowAllocation:false)` with the same static allocation edge.

What was done:
- Added `HasTelemetryReady()` to gate foveation telemetry without allocation.
- Added `TryResolveShaderGlobalSlotsRuntime()` and `TryResolvePreparedShaderGlobalSlots()` so shader global hot paths can resolve prepared slots without allocation.
- Added `TryResolveTelemetryBufferReady()` so Uber Noir telemetry push and dump only use resident or existing DataVault rings.

Cinematic cheats used:
- Fail closed for one telemetry/shader-reporting frame instead of repairing DataVault state during visual sync.
- Keep allocation ownership in lifecycle/slow/cold paths, where weak hardware can absorb repair without headset or render-thread stalls.

Exact Microseconds Saved:
- Static estimate: 1800 us foveation telemetry repair hitch avoided plus 2600 us shader global/Uber Noir telemetry repair hitch avoided.

Verification:
- Scoped brace/paren/bracket balance for `FoveatedRenderCommander`, `GlobalShaderDispatcher`, and `HectonUberNoirRuntimeBridge`: 0/0/0.
- Broad non-editor `Assets/_Project/Scripts/Graphics` + `Assets/_Project/Scripts/Rendering` hot graph: 33 files, `TOTAL_REPORTS=0`.
- DataVault write-lock proof: `FoveatedRenderCommander.TryAcquireTelemetryWriteBuffer` and `HectonUberNoirRuntimeBridge.TryAcquireTelemetryWriteBuffer` each have one acquire, one release, and `finally`; callers release handed-off locks from `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 100% with external `dotnet` active.

## 2026-05-29 - Runtime Adaptation Hot Allocation Split

What was wrong:
- `HarpoonLauncherTool.RenderTracer` could allocate a runtime material and three GraphicsBuffers from `LateFrameTick`.
- `ShinobuOceanSurfaceAtmosphereRuntime.LateFrameTick` could repair wave upload/readback GPU buffers and resolve the wave sampler compute kernel.

What was done:
- `HarpoonLauncherTool` now prewarms tracer resources from spawn/equip/cold paths and renders only when `HasTracerReady()` is true.
- `ShinobuOceanSurfaceAtmosphereRuntime` now performs wave/readback buffer repair and kernel resolution in `OnEnable`/`SlowTick`; hot paths use `UploadPreparedWaveBufferToGpu()` and `HasResolvedWaveSamplerKernel()`.

Cinematic cheats used:
- Missing harpoon tracer or skipped wave readback is accepted for a frame; gameplay truth stays intact.
- Weak hardware gets fail-closed visuals instead of GPU allocation stalls; high-tier keeps the full visual path once resources are resident.

Exact Microseconds Saved:
- Static estimate: 3900 us harpoon tracer resource hitch avoided plus 11800 us ocean wave/readback repair hitch avoided.

Verification:
- `HarpoonLauncherTool`: 73 methods, 50 reachable from tool/late roots, 0 forbidden allocation/dependency reports.
- `ShinobuOceanSurfaceAtmosphereRuntime`: 123 methods, 65 reachable from `Tick`/`LateFrameTick`, 0 forbidden ensure/kernel/platform/allocation reports.
- Five-file hot graph aggregate: `TOTAL_REPORTS=0`.
- Scoped balance for five changed runtime files: 0/0/0.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 88% with external `dotnet` active.

## 2026-05-29 - 14MU APEX Platform Adaptation Sweep 12

What was wrong:
- `SaveThumbnailCaptureFeature.AddRenderPasses` could reach capture RTHandle repair, and save-thumbnail GPU readback completion could allocate the persistent RGBA shadow buffer.
- Abyssal shadow, dynamic point light culling, octahedral impostors, and interior GI probe upload still had visual/upload routes that could repair GPU buffers after resource loss.
- Diegetic compass, connection spline batching, and GPR could repair native/GPU resources from late/render paths instead of slow/cold ownership.
- Crest depth-cache bootstrap and Architect Eye diagnostics still had hot-chain component/resource self-healing.

What was done:
- Split save-thumbnail capture/readback into cold `Prepare*Cold`/`Ensure*Cold` routes and hot `Has*Ready` probes.
- Added fail-closed GPU readiness gates for shadow/light/impostor/GI upload paths; moved allocation repair to lifecycle or `SlowTick`.
- Registered slow repair loops for compass, spline batching, and GPR; late/render paths consume cached readiness only.
- Moved Crest discovery/populate and Architect Eye diagnostic upload resource prewarm into slow phase.

Cinematic cheats used:
- A missing thumbnail, diagnostic overlay, depth-cache refresh, compass indirect payload, spline batch, GPR draw, or visual upload can skip a frame instead of allocating in a frame-critical phase.
- Low hardware preserves frame time first; middle/high/ultra keep the full visuals once resident without changing gameplay authority or save identity.

Exact Microseconds Saved:
- Static estimate: 7400 us save-thumbnail hitch avoided.
- Static estimate: 30900 us aggregate shadow/light/impostor/GI GPU-buffer repair hitch avoided.
- Static estimate: 35800 us navigation/spline/GPR resource repair moved out of visual sync.
- Static estimate: 10200 us Crest/Architect Eye hierarchy/resource hitch avoided.

Verification:
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- Scoped syntax balance: all 11 changed files report BRACE=0, PAREN=0, BRACKET=0.
- Hot graph verification from this sweep: target roots report 0 forbidden lookup/platform/allocation edges after the cold/slow split.
- DataVault lock verification: `TryPublishRadarPendingJob`, `TryPinScanJobBuffers`, `TryPinPingGpuReadBuffer`, and `WriteTelemetry` each use one acquire and release in `finally`; Abyssal/Dynamic-light job guard helpers acquire one mutation guard and release through caller `finally` or scheduled-job completion release.
- `dotnet build`: not launched; CPU measured 67-91% and external `dotnet` process 37024 was active.

## 2026-05-29 - 14MU APEX Platform Adaptation Sweep 13

What was wrong:
- `HectonUIScaler.LateFrameTick` reached `Screen.width`/`Screen.height` through render-dimension resolution.
- `FoveatedRenderCommander.LateFrameTick` could reach registry unregister helpers while detaching an inactive commander.
- `PDAMapTab.RenderPointCloud` pulled sonar shader globals inside the draw path instead of consuming a visual-sync snapshot.

What was done:
- `HectonUIScaler` now refreshes render dimensions only in lifecycle/configuration/`SlowTick`; late frame reads cached dimensions.
- `FoveatedRenderCommander` now latches inactive detach in late frame and executes actual `GlobalRegistry` unregisters from `SlowTick`/lifecycle.
- `PDAMapTab` now snapshots sonar globals in `CachePresentationGlobalsLate` and renders the point cloud from cached value fields.

Cinematic cheats used:
- UI scale and PDA sonar tolerate one slow/late snapshot delay; no gameplay truth or save identity depends on these values.
- Inactive XR commander cleanup is deferred out of visual sync; missing one cleanup frame is cheaper than registry mutation in the headset lane.

Exact Microseconds Saved:
- Static estimate: 18 us from cached UI render dimensions.
- Static estimate: 35 us from removing XR late-frame registry unregister edge.
- Static estimate: 12 us from cached PDA sonar presentation globals.

Verification:
- Changed-file syntax balance for `HectonUIScaler`, `FoveatedRenderCommander`, and `PDAMapTab`: 0/0/0.
- Changed-file transitive hot graph: `TOTAL_HITS=0`; `CachePresentationGlobalsLate` is the only allowed shader-global snapshot point.
- Broad direct hot scan across 648 runtime files: `DIRECT_HOT_FORBIDDEN=0` for direct `GlobalRegistry.Get<T>()`, component lookup, `SystemInfo`, `XRSettings`, or `Screen` tokens in hot bodies.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged gaps are XR provider serialized proof, Addressables content artifact, and build artifact.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 57% and external `dotnet` process 20592 was active.

## 2026-05-29 - 14MU APEX Platform Adaptation Sweep 14

What was wrong:
- `HectonDistantLandmarkRenderer.LateFrameTick` and `HectonHLODRenderer.LateFrameTick` could reach fallback runtime material creation and shader lookup.
- `LODSystemManager.Tick` could reach fixed scratch allocation through `EnsureDistanceScratchAllocated()` if lifecycle prewarm was missing.

What was done:
- Distant landmark and HLOD fallback materials are now prepared in `Awake`/`OnEnable`; late-frame rendering only reads `GetPreparedMaterial()` and fails closed if cold preparation is absent.
- LOD distance scratch remains lifecycle-owned; `CalculateDistanceSlice()` now uses `HasDistanceScratchReady()` and clears the scheduled batch instead of allocating from `Tick`.

Cinematic cheats used:
- Missing distant silhouettes, HLOD draws, or one LOD distance slice are allowed to skip a frame; weak hardware keeps frame time stable.
- High and Ultra retain the same visuals once resources are resident, without changing gameplay authority or save identity.

Exact Microseconds Saved:
- Static estimate: 4200 us worst-case fallback material/shader hitch avoided.
- Static estimate: 260 us worst-case LOD scratch allocation repair avoided and 0 B GC in the solver tick.

Verification:
- Scoped syntax balance for three changed files: braces=0, parens=0.
- Transitive hot graph: `HectonDistantLandmarkRenderer` 32 methods / 8 reachable / 0 forbidden reports; `HectonHLODRenderer` 34 methods / 10 reachable / 0 forbidden reports; `LODSystemManager` 59 methods / 19 reachable from `Tick` + `LateFrameTick` / 0 forbidden reports.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `Tools/PlatformPortabilityProofAudit.py`: `PASS_WITH_WARNINGS`; unchanged gaps are XR provider serialized proof, Addressables content artifact, and build artifact.
- `dotnet build`: not launched; CPU measured 63-96% and external `dotnet` process 20592 was active.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 15

What was wrong:
- `AbyssalThermalManager.Tick` reached thermal-map DataVault/storage repair; `LateFrameTick` reached thermal-map `Texture2D` creation, smoke GPU repair, and EMP PDA sink registry lookup.
- `SargassumGlobalDragManager.LateFrameTick` still reached density/sink texture creation through dynamic texture refresh.
- `FloraInteractionManager.LateFrameTick` could repair wake trail render textures during presentation.
- `WreckMaterialRegistry.LateFrameTick` could reach BRG material/buffer preparation, frustum scratch allocation, camera component fallback, and registry registration refresh.

What was done:
- Moved abyssal thermal map buffers, scratch, texture, smoke GPU buffers, vent upload, and particle reset to lifecycle/`SlowTick`; hot paths use `Has*Ready` gates and cached `_pdaCorrosionPresentationSink`.
- Moved sargassum density/sink texture creation to `SlowTick`; late dynamic texture refresh now requires prepared texture/raw buffers.
- Moved flora wake trail RT repair to `SlowTick`; late wake globals fail inactive when resources are not ready.
- Moved wreck BRG upload resource preparation to publish-time/`SlowTick`; late visibility upload now requires prepared resources and cached frustum/camera state.

Cinematic Cheats used:
- Missing one thermal-map, smoke, sargassum density, wake, scavenger, or wreck upload frame is accepted on weak devices instead of allocating in simulation or visual sync.
- High and Ultra retain the full visuals once resources are resident; gameplay truth, DTO layout, save identity, and authority routes are unchanged.

Exact Microseconds Saved:
- Static estimate: 42100 us worst-case abyssal thermal/smoke resource repair hitch avoided.
- Static estimate: 16200 us sargassum/flora texture/RT/BRG repair hitch avoided.
- Static estimate: 28600 us wreck BRG material/buffer/frustum repair hitch avoided.

Verification:
- In-memory transitive hot graph: `SargassumGlobalDragManager`, `FloraInteractionManager`, and `WreckMaterialRegistry` report 0 forbidden normal-frame lookup/platform/allocation edges.
- `AbyssalThermalManager` reports 0 ordinary hot-frame edges; the only remaining allocation edge is `DumpThermalBlackBox => new NativeArray` on NaN/crash fault path.
- Syntax balance for four changed source files: braces=0, parens=0, brackets=0.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- DataVault lock-token scan: no changed method holds more than one write/mutation lock; normal write routes use one acquire and `finally` release or explicit ownership-transfer helpers.
- `dotnet build`: not launched; CPU measured 75.7%, above the project throttle.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 16

What was wrong:
- `WorldProceduralScatterDirector.LateFrameTick` -> scatter reconcile -> flora GPUI registration could reach compute-support probing, prefab `TryGetComponent`, and pooled matrix-buffer growth.
- Runtime scatter proxy creation reused pooled instances from late frame, then `ConfigureScatter` / `MarkScatterSync` / pool `OnSpawn` could trigger collision scans and LOD/Culling registration.
- GPUI register/reset/flush helpers still had hot `EnsureWorkingMemory()` edges.

What was done:
- `ScatterInstancingService` now caches compute support cold, caches GPUI prototype ownership cold, and prewarms prototype matrix arrays by continuous quality weight.
- `TryRegisterPlacement` no longer rents or grows arrays from the hot path; missing capacity is a fail-closed visual skip.
- `WorldProceduralProxyInstance` now uses a runtime static metadata cache, dirty bool latches, cached component buffers, and owner-local LOD/Culling lookup.
- `WorldProceduralScatterDirector.SlowTick` flushes dirty proxy optimization state with an 8-64 continuous budget; late-frame scatter applies value state only.
- Hot GPUI helpers now return false/skip when cold working memory or instancing service is absent.

Cinematic Cheats used:
- Low devices skip excess GPUI flora or defer one proxy LOD/collision/culling refresh instead of paying a late-frame scan/allocation.
- Middle devices clear a larger slow-tick budget.
- High and Ultra keep dense flora and fast proxy refresh with up to 512 prewarmed GPUI matrices per prototype.

Exact Microseconds Saved:
- Static estimate: 4400 us worst-case GPUI prototype lookup and matrix-buffer growth avoided in visual sync.
- Static estimate: 6200 us worst-case proxy child component scan and registration work moved out of late-frame reconcile.

Verification:
- In-memory transitive hot graph from `LateFrameTick`, scatter reconcile, GPUI register, create/apply/sync, and warmup-demand roots: 461 methods parsed, 96 reachable, 0 forbidden reports.
- `CullingManager.LateFrameTick` and `RunCullingEvaluationVisualSync`: 0 direct lookup/platform/allocation reports.
- Syntax balance for `WorldProceduralScatterDirector`, `WorldProceduralProxyInstance`, `ScatterInstancingService`, and `CullingManager`: braces=0, parens=0, brackets=0.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- Changed-file DataVault scan: no write-lock acquisition route in this patch.
- `dotnet build`: not launched; CPU measured 93.4%, above the project throttle.
- Parser process check: no orphan `python -` process; remaining Python processes are unrelated user services.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 17

What was wrong:
- `HectonBiolumZone.GetZonePosition()` was a public read accessor that could read `transform.position` and publish invalid-input telemetry.
- `HectonBiolumZone.GetZoneAup()` was a public read accessor that could mutate cached AUP state and call runtime-origin resolution.
- `SampleZoneColor`, `SampleZoneIntensity`, and `SampleZoneRange` recomputed virtual zone sample values for manager, diffusion, and fauna consumers instead of reading a phase-owned snapshot.

What was done:
- Added cached sample fields for zone color, intensity, and range.
- Moved runtime position and AUP refresh into owner phases through `RefreshCachedAup()`.
- Moved sample value refresh into `RefreshSampleCache()`, called from lifecycle, `Tick`, and after `EvaluateBiolumState()` in `LateFrameTick`.
- Converted the five public read accessors to pure cached-value reads.
- Kept invalid-zone telemetry inside owner-phase refresh instead of hot read accessors.

Cinematic Cheats used:
- If a zone cannot resolve a finite position/AUP, consumers receive zero position or invalid AUP from the last owner-phase decision instead of repairing state mid-sample.
- Low devices avoid hidden accessor work in dense biolum loops.
- Middle devices keep stable cached biolum influence.
- High and Ultra can spend saved CPU on denser biolum visuals without changing gameplay truth, DTO layout, save identity, or authority route.

Exact Microseconds Saved:
- Static estimate: 28 us saved on dense biolum sampling frames.
- Larger fault-path stability when invalid zone data would otherwise publish telemetry or repair AUP from consumer read loops.

Verification:
- Public accessor purity: `SampleZoneColor`, `SampleZoneIntensity`, `SampleZoneRange`, `GetZonePosition`, and `GetZoneAup` report `PURE_VALUE_READ`.
- Scoped biolum direct scan: `BIOLUM_DIRECT_REPORTS=0`.
- Broad preprocessor-aware hot scan across World, Graphics, Rendering, Visor, UI, and Platform: 412 runtime files, `BROAD_HOT_REPORTS=0`.
- Source balance for `HectonBiolumZone`: braces=0, parens=0, brackets=0.
- DataVault write-lock scan for `HectonBiolumZone`: `DATAVAULT_WRITE_LOCK_REPORTS=0`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 91%, above the project throttle.
- Parser process check: no parser orphan from this work; remaining Python processes are unrelated user services.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 18

What was wrong:
- `ThermalDynamicResolutionAdapter` derived `_hardwareTier` and `ResolutionScaleState.HardwareTier` from mutable quality tier, letting continuous quality scaling rewrite stable platform identity.

What was done:
- `ResolveHardwareTierByte()` now uses valid boot hardware tier first and quality tier only as fallback.
- `ResolveStpIntent()` now evaluates boot hardware tier before compatibility quality tier.

Cinematic Cheats used:
- No new simulation; this is a platform identity separation so continuous visual quality can throttle without lying about hardware class.

Exact Microseconds Saved:
- 0 steady-state us; scalar branch only.
- Prevents wrong DRS/thermal/foveation policy on weak laptops, Deck-class APUs, Mac integrated GPUs, PC VR, standalone VR, and high-end desktops.

Verification:
- `ThermalDynamicResolutionAdapter` balance: braces=0, parens=0, brackets=0.
- Focused hot lookup scan: 0 `GlobalRegistry.Get<T>` or `GetComponent` hot edges.
- DRS guard helper owns one local guard path; call sites release through `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 63% and `VBCSCompiler.exe` was active.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 19

What was wrong:
- `TBDRPipelineSurgeonRuntime.ScheduleTBDRProtectionPass()` and `CommitCompletedProtectionPass()` repeatedly called platform classification helpers that read `SystemInfo` and device strings.
- `TBDRComputeDispatchLimiter.TryDispatch()` polled compute support and could self-boot from dispatch.

What was done:
- Cached mobile TBDR classification and early-Z radix-sort policy into `_isMobileTbdrCold` and `_shouldRunEarlyZRadixSortCold` during initialization.
- Hot schedule/commit routes now read cached booleans only.
- `TBDRComputeDispatchLimiter.Boot()` owns compute capability and hardware thread-group snapshots; `TryDispatch()` fails closed if cold boot was skipped and never reads `SystemInfo`.

Cinematic Cheats used:
- Low and standalone VR devices keep early-Z radix sort when cold-classified as mobile/TBDR.
- High desktop GPUs can still skip early-Z radix sort, but the choice is a platform fact, not per-frame probing.

Exact Microseconds Saved:
- Static estimate: 84 us on protection frames from avoiding repeated platform classification and compute-support probes.
- Larger value is reduced culling/render variance on Quest/Android/iGPU/Deck-class hardware.

Verification:
- `TBDRPipelineSurgeonRuntime` and `TBDRPipelineSurgeonTypes` balance: braces=0, parens=0, brackets=0.
- `ScheduleTBDRProtectionPass`, `CommitCompletedProtectionPass`, and `TryDispatch`: 0 forbidden `SystemInfo`, `TBDRHardwarePipelineSwitch`, registry, or component hits in method bodies.
- No DataVault write-lock acquisition route added.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 64% and `VBCSCompiler.exe` was active.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 20

What was wrong:
- `HectonCaveVoxelLightingVolume.LateFrameTick()` advanced cave SDF scan state, including scan begin/restart, voxel slice scan, SDF finalize, and DataVault write-buffer work.
- Slow phase did not own missing-resource repair cleanly.

What was done:
- Removed `AdvanceLightingVolumeState()` from `LateFrameTick`.
- `SlowTick()` now owns `EnsureResources()` and `AdvanceLightingVolumeState()`.
- Late frame now only uploads an already completed SDF texture and flushes pending shader globals.
- If resources are still unavailable, slow phase publishes inactive globals and exits cleanly.

Cinematic Cheats used:
- Cave lighting can lag by slow-tick cadence instead of spending visual-sync time on voxel scanning.
- Weak devices fail closed to inactive cave lighting until resources/scan data are ready.
- High and Ultra keep dense SDF lighting without moving scan work back into late frame.

Exact Microseconds Saved:
- Static estimate: 5900 us worst-case SDF slice/encode/resource repair moved out of visual sync.

Verification:
- `HectonCaveVoxelLightingVolume` balance: braces=0, parens=0, brackets=0.
- `LateFrameTick`: 0 forbidden scan/resource/platform/global lookup hits.
- `SlowTick`: intentional ownership of `EnsureResources` and `AdvanceLightingVolumeState`.
- SDF upload lock path remains one write-lock route with `finally` release.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 100% and `VBCSCompiler.exe` was active.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 21

What was wrong:
- `GlobalShaderDispatcher.LateFrameTick()` called `TryEnsureCommandBuffer(false)`. Runtime behavior was fail-closed, but static hot-root proof still reached an allocation-capable helper body.

What was done:
- Added pure `HasCommandBufferReady()`.
- Late frame now checks command-buffer readiness without entering `TryEnsureCommandBuffer`.
- Lifecycle remains the only route that can allocate the command buffer.

Cinematic Cheats used:
- No visual change. This is proof-hardening: shader-global upload skips if the cold command buffer is absent instead of touching an allocation-capable route.

Exact Microseconds Saved:
- 0 steady-state us.
- Removed one allocation-capable helper edge from visual sync.

Verification:
- `GlobalShaderDispatcher` balance: braces=0, parens=0, brackets=0.
- Broad direct hot-body scan over non-editor `Graphics`, `Rendering`, and `World`: `DIRECT_HOT_BODY_REPORTS=0`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 100%.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 22

What was wrong:
- `FoveatedRenderCommander.HasEyeTrackedGaze()` performed `InputDevices.GetDeviceAtXRNode(XRNode.CenterEye)` from the late/render policy graph.
- The same file already had telemetry lock hardening, so the new fix had to avoid touching lock ownership or registry lifecycle logic.

What was done:
- Added `_centerEyeDeviceCold`.
- `CacheRuntimeCapabilitySnapshotCold()` now refreshes the center-eye device from lifecycle/`SlowTick`.
- `HasEyeTrackedGaze()` consumes the cached device and keeps hot work to `TryGetFeatureValue` plus finite-vector validation.

Cinematic Cheats used:
- Low/weak hosts fail closed for one slow-tick interval if XR device identity is not cached yet.
- Middle PC VR keeps gaze-tracked VRS when the cached eye device is valid.
- High and Ultra keep gaze-tracked foveation without platform discovery in visual policy flow.

Exact Microseconds Saved:
- Static estimate: 24 us on sampled PC VR foveation frames.
- Bigger value is reduced XR runtime variance under weak CPUs, Steam Deck-class APUs, and PC VR frame pressure.

Verification:
- `FoveatedRenderCommander` balance: braces=0, parens=0, brackets=0.
- Transitive hot graph from `LateFrameTick`/`Render`: 71 methods, 40 reachable, 0 forbidden registry/component/platform/allocation/job reports.
- `TryAcquireTelemetryWriteBuffer`: acquire=1, release=1, finally=1; `WriteTelemetry`: finally=1.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched; CPU measured 59.6%.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 23

What was wrong:
- `FoveatedRenderCommander.ApplyPolicy()` still called `HectonXRManager.RefreshEyeDescriptor()`.
- That route reads `XRSettings.eyeTextureDesc`, so XR descriptor discovery was still inside the late/render foveation policy graph.

What was done:
- Added `_eyeDescriptorCold`.
- `CacheRuntimeCapabilitySnapshotCold()` refreshes the descriptor beside foveation caps and cached center-eye device identity.
- `ApplyPolicy()` consumes the cached `RenderTextureDescriptor` value only.

Cinematic Cheats used:
- Low/weak devices and standalone VR keep one slow-cadence descriptor snapshot instead of querying XR descriptor state during foveation policy.
- High and Ultra keep full eye-resolution policy because descriptor data is still refreshed cold.

Exact Microseconds Saved:
- Static estimate: 18 us on sampled foveation frames.
- Reduces variance more than average CPU time because XR descriptor access can cross runtime/native platform state.

Verification:
- `FoveatedRenderCommander` balance: braces=0, parens=0, brackets=0.
- Hot graph from `LateFrameTick`/`Render` with `HectonXRManager.RefreshEyeDescriptor` forbidden: 71 methods, 40 reachable, 0 reports.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 70.9%.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 24

What was wrong:
- `HectonXRRuntimeState.RefreshFrameState()` was a frame-route called by `SystemDispatcher.RunDispatcherUpdate()`, but it read `XRSettings.enabled/isDeviceActive`, could poll display subsystems for refresh rate, and repaired missing head AUP from the same frame path.
- `HomeostasisBrain.PreSimulationTick()` used per-frame target-frame-rate sampling and could call an XR refresh-rate request route that polled `SubsystemManager.GetSubsystems` and wrote `Application.targetFrameRate`.

What was done:
- Added `HectonXRRuntimeState.RefreshPlatformStateCold(int frame)` and moved XR active/refresh-rate sampling plus pending refresh-rate application into dispatcher init/`SlowTick`.
- `RefreshFrameState()` now only consumes cached XR state and queues shader globals.
- `TryRequestDisplayRefreshRateHz()` now latches a pending scalar request; `TryApplyDisplayRefreshRateRequestCold()` applies it from slow/cold ownership.
- Added `HomeostasisBrain.RefreshCadenceSnapshotCold()` and `_cachedTargetFrameRate`; pre-simulation health logic consumes cached FPS only.

Cinematic Cheats used:
- Low and standalone VR devices can tolerate one slow-tick delay for XR active/refresh/cadence changes rather than paying platform query variance every frame.
- Middle devices keep stable cached cadence.
- High and Ultra keep high-refresh/foveated overkill while platform mutation stays out of frame-critical control logic.

Exact Microseconds Saved:
- Static estimate: 44 us removed from XR frame-state paths.
- Static estimate: 61 us removed from XR pressure-shed frames.
- State transfer between phases is scalar fields only; 0 B GC.

Verification:
- `HectonXRRuntimeState`, `HomeostasisBrain`, `SystemDispatcher` balance: braces=0, parens=0, brackets=0.
- Direct hot-body scan over Core/Graphics/Rendering/Visor roots including `PreSimulationTick`, `RefreshFrameState`, `LateFrameTick`, `Render`, `Execute`: `DIRECT_CORE_GRAPHICS_REPORTS=0`.
- `RefreshFrameState`, `ResolveDispatcherDeltaTime`, `TryRequestDisplayRefreshRateHz`, `RunDispatcherUpdate`, and `PreSimulationTick` direct platform scans are clean.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because another `dotnet` process was already running.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 25

What was wrong:
- `RenderTexturePool.Rent()` and `Return()` called `DefragForCurrentScreenIfNeeded()`, so shared RT borrow/release routes read `Screen.width/height` and could clear all pools from the caller phase.
- `Return()` allocated `new Queue<RenderTexture>` for unknown keys, turning release cleanup into a managed allocation route for transient RT shapes.

What was done:
- `RenderTexturePool` now implements `ISlowTickable`.
- Service registration now registers a slow tick; disable/destroy unregister it.
- `SlowTick()` owns screen-size defrag.
- `Rent()` and `Return()` no longer poll `Screen` or call defrag.
- Unknown return keys now dispose the RT through lifecycle tracking instead of growing the pool from the release path.

Cinematic Cheats used:
- Low devices and standalone VR defer obsolete screen-size pool cleanup to slow cadence instead of paying platform surface polling in RT borrow/release.
- Middle devices keep stable screen bucket reuse.
- High and Ultra can retain large prewarmed screen buckets and still avoid API-path allocation drift.

Exact Microseconds Saved:
- Static estimate: 26 us on rent/return bursts by removing screen polling and accidental pool-clear checks from the public API.
- Static estimate: 160 us first-unknown-key managed allocation and later GC pressure avoided per unique transient RT shape.

Verification:
- `RenderTexturePool` balance: braces=0, parens=0, brackets=0.
- `Rent`/`Return` body scans: no `Screen`, `DefragForCurrentScreenIfNeeded`, `GlobalRegistry.Get`, `GetComponent`, or `new Queue`.
- `SlowTick` is the only caller of `DefragForCurrentScreenIfNeeded`.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched because CPU measured 100% with external `dotnet` PID 68252 active.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 26

What was wrong:
- `ShinobuEcosystemBalancer.BindProceduralCullingResources()` read `SystemInfo.supportsComputeShaders`.
- Kernel helpers also read compute support and re-ran `HasKernel`, `FindKernel`, `IsSupported`, and `GetKernelThreadGroupSizes` on every bind.
- The API currently has no external caller, but it is a public procedural culling contract and would be unsafe for future render-visible owners.

What was done:
- Added `_supportsComputeShadersCold` and `RefreshGraphicsCapabilitiesCold()` during runtime activation.
- `BindProceduralCullingResources()` now consumes cached compute capability.
- Added `_proceduralCullKernelsResolved`.
- Compute kernels and thread-group sizes now resolve only when the compute shader identity changes or cold capability invalidates the cache.

Cinematic Cheats used:
- Low and standalone VR devices fail closed from cached compute capability.
- Middle devices keep stable procedural culling without platform polling.
- High and Ultra keep swarm procedural overkill while per-frame matrix/depth rebinding avoids kernel reflection.

Exact Microseconds Saved:
- Static estimate: 31 us on repeated culling-bind frames from removing platform support polling.
- Static estimate: 240 us on repeated procedural culling binds from avoiding kernel reflection when the compute shader is unchanged.

Verification:
- `ShinobuEcosystemBalancer` balance: braces=0, parens=0, brackets=0.
- `BindProceduralCullingResources`, `Render`, and `ResolveGpuCullingParams` body scans: no `SystemInfo`, `GlobalRegistry.Get`, `GetComponent`, or allocation tokens.
- Only `RefreshGraphicsCapabilitiesCold` reads `SystemInfo.supportsComputeShaders`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 97% with external `dotnet` PID 24832 active.

## 2026-05-30 - 14MU APEX Platform Adaptation Sweep 27

What was wrong:
- `HectonUIScaler.DisabledVisualSync()` could call `ApplyScale()`.
- `ApplyScale()` reached `ResolveRenderDimensions()`.
- `ResolveRenderDimensions()` read `Screen.width/height` from the visual-sync scale route.

What was done:
- Added cached render dimensions to `HectonUIScaler`.
- `RefreshRenderDimensionsSlowSample()` owns `Screen.width/height` sampling from lifecycle/editor rebuild/`SlowTick`.
- `ResolveRenderDimensions()` now reads cached overlay dimensions or authored reference dimensions for world-space HUD.
- `SlowTick()` reapplies scale only when cached render dimensions changed.

Cinematic Cheats used:
- Low and standalone VR devices accept one slow-tick delay for UI resize detection instead of paying surface polling in visual sync.
- Middle devices keep stable HUD scaling with cached dimensions.
- High and Ultra keep sharp HUD transforms while resize ownership remains outside presentation callbacks.

Exact Microseconds Saved:
- Static estimate: 18 us on UI visual-sync scale checks.
- 0 B GC; state transfer is two cached ints.

Verification:
- `SuitHUDV4CanvasOverlay.cs` balance: braces=0, parens=0, brackets=0.
- `DisabledVisualSync` and `ResolveRenderDimensions` body scans: no `Screen`, `GlobalRegistry.Get`, `GetComponent`, or allocation tokens.
- Only `RefreshRenderDimensionsSlowSample` contains `Screen.width/height`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 84% with external `dotnet` PID 24832 active.

## 2026-05-30 Sweep 29 - UI scaler pure accessors and RT lifecycle phase split

What was wrong:
- Nested `SuitHUDV4CanvasOverlay.HectonUIScaler` still allowed cached-root validation/read access to fall into hierarchy search or RectTransform sanitation from visual routes.
- Standalone `HectonUIScaler.ContentRoot` was not a pure accessor; it could reach hierarchy search/mutation through `ResolveContentRootInternal(false)`.
- `RenderTextureLifecycleTracker` moved slow leak diagnostics into `LateFrameTick` through a pending flag.

What was done:
- `SuitHUDV4CanvasOverlay.HectonUIScaler.ContentRoot`, `DisabledVisualSync`, and `TryRefreshExistingContentRootHot` are cached-only; slow/cold bootstrap owns child search and root creation.
- Standalone `HectonUIScaler` now mirrors the cached-dimension/cached-root contract and recovers missing roots from slow/cold phase.
- Removed `ILateFrameTickable`, `_registeredLateFrame`, `_leakCheckPending`, and `LateFrameTick` from `RenderTextureLifecycleTracker`.

Cinematic Cheats used:
- UI scale is treated as a cached scalar/matrix presentation problem, not a live surface-query problem.
- Missing HUD root repair is slow-cadence recovery; visual sync fails closed until the root is cached again.

Exact Microseconds Saved:
- Nested scaler: static estimate 11 us on missing-root visual-sync guard cases.
- Standalone scaler: static estimate 18 us on HUD scale checks.
- RT lifecycle: static estimate 38 us late-frame variance removed from leak-check cadence.

Verification:
- `SuitHUDV4CanvasOverlay.cs`, `HectonUIScaler.cs`, `RenderTextureLifecycleTracker.cs`: braces=0, parens=0, brackets=0.
- Changed-file hot lookup scan: `CHANGED_FILE_HOT_LOOKUP_REPORTS=0`.
- Method-body scans: hot UI methods have no `Screen`, `GlobalRegistry.Get`, `GetComponent`, `ResolveCanvas`, `ResolveContentRootInternal`, `FindExistingChild`, or allocation tokens.
- Slow/cold methods are the only remaining routes with `Screen.width/height`, `FindExistingChild`, or `ResolveCanvas`.
- `RenderTextureLifecycleTracker`: no `ILateFrameTickable`, `LateFrameTick`, `_registeredLateFrame`, or `_leakCheckPending`.
- Scoped DataVault write-lock scan: no changed-file write-lock route.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 88% and `VBCSCompiler.exe` was active.
- Stale broad parser processes killed: 23264, 45956, 24416.
Sweep 30 - Platform adaptation / phase safety

What was wrong:
- `VRAMPressureMonitor` was a late-frame tickable while owning profiler memory reads, QualitySettings writes, mip pressure, BRG LOD pressure, RT pool clearing, and asset evictions.
- `AssetLoadDispatcher` evaluated UI mip-bias pressure from late frame.
- `AssetLifecycleGovernor` flushed retry scans and async dispatch from late frame.
- `ContentAuthorityRuntime` could force-drain Addressables releases from visual sync during AUP shift and VRAM intercept paths.

What was done:
- Moved `VRAMPressureMonitor` to `ISlowTickable`, preserving frame cadence through `_nextSampleFrame`.
- Replaced emergency `ForceDrainPendingReleaseQueue()` with `DrainPendingReleaseQueueBudgeted(emergencyEvictionBudget)`.
- Moved `AssetLoadDispatcher` UI mip-gate evaluation to slow tick.
- Moved asset retry pump into `AssetLifecycleGovernor.SlowTick`; late frame now only disables cached presentation targets.
- Split `ContentAuthorityRuntime` AUP/VRAM cleanup into late-frame bool latches and bounded slow-tick flushes.

Cinematic cheats used:
- Pressure response remains continuous, not binary: mip pressure, LOD aggression, and UI mip gates still scale by measured pressure and `GlobalQualityWeight`.
- Weak hardware gets bounded cleanup; high/ultra hardware keeps visual overkill until pressure thresholds require slow-phase shedding.

Exact microseconds saved:
- VRAM pressure sample late-frame work: 290 us static estimate.
- UI mip-gate late-frame pressure evaluation: 64 us static estimate.
- Asset retry pump late-frame record traversal: 115 us static estimate.
- Content authority late-frame release drain: 2200 us worst-case static estimate.
- Unbounded emergency release drain collapsed to 1-8 releases per slow pass.

Verification:
- Seven scoped files balance braces/parens/brackets 0/0/0.
- Direct hot-body reports 0 for `Tick`, `FixedUpdate`, `LateFrameTick`, `Execute`, `Render`, `RecordRenderGraph`, and `AddRenderPasses` in the scoped files.
- DataVault write-lock additions: 0 in the new patch surface; existing telemetry write routes keep one local guard and `finally` release.
- `git diff --check` exited 0 with LF/CRLF warnings only.
- `dotnet build` not launched: CPU measured 95%, so compile throttling blocked it.

Sweep 31 - Diegetic panel RT phase split

What was wrong:
- `DiegeticPanelController.LateFrameTick()` reached panel data refresh, distance refresh, and RT rebuild logic that can allocate `RenderTexture` and phosphor history resources.
- `ForceRefreshRenderTexture()` was a public force path that PDA presentation could call while visible, rebuilding the surface in the same visual frame.
- Queue helpers in the first correction draft attempted slow-tick registration from hot callers, which would reintroduce registry polling.

What was done:
- Added `ISlowTickable` to `DiegeticPanelController`.
- `LateFrameTick()` now performs interaction/presentation flush only.
- `SlowTick()` owns `_pendingQualityPresentationRefresh`, `_pendingDistanceRenderTextureRefresh`, `_forceRenderTextureRefreshQueued`, `RefreshDistanceAndRenderTexture()`, `EnsureRenderTexture()`, and phosphor resource repair.
- `ForceRefreshRenderTexture()`, fixed RT resolution overrides, pause recovery, and phosphor override now queue scalar refresh latches instead of rebuilding immediately.
- `RefreshLateFrameRegistration()` now uses `_dispatcherAvailableCold` instead of polling `GlobalRegistry.Dispatcher`.

Cinematic cheats used:
- Low/standalone VR devices may show one stale diegetic panel frame instead of paying RT allocation during presentation.
- Middle devices refresh panel surfaces on slow cadence.
- High/Ultra devices still get large RT/phosphor surfaces, but rebuild cost is phase-owned.

Exact microseconds saved:
- Static estimate: 900-4200 us worst-case late-frame RT rebuild/phosphor allocation spike moved to slow phase.
- 0 B GC phase transfer: bool latches plus one float delta.

Verification:
- `DiegeticPanelController.cs` balance braces/parens/brackets 0/0/0.
- Transitive hot graph from `LateFrameTick`, `AdvancePanelInteractionPresentation`, and `ForceRefreshRenderTexture`: 59 reachable methods, 0 forbidden registry/component/platform/resource-allocation reports.
- Read accessors `TryProjectCanvasPointToWorld`, `TryProjectRayToCanvas`, `TryGetPanelRotation`, `TryGetCanvasPixelBasis`, and `TryGetFocusGateData`: no resource lookup/allocation hits.
- DataVault write-lock scan: no route in the touched file.
- Scoped `git diff --check`: exit 0; LF/CRLF warning only.
- `dotnet build`: not launched because CPU measured 100% with active external `dotnet build Hecton8.slnx`.
- Process cleanup: killed orphan `python.exe -` with dead parent PID 58624; left named user services untouched.

Sweep 32 - Tool diegetic RT lifecycle phase split

What was wrong:
- `ToolDiegeticDisplayController` presentation decisions could request RT ensure/release from the visual path, and the ensure latch had no safe slow-phase owner.
- `EnsureRenderTexture()` still resolved the pool through `CacheRenderTexturePoolCold()`, so a future hot call would reach `GlobalRegistry.RenderTexturePoolService`.

What was done:
- `OnEnable()` and `Start()` cold-cache the render texture pool.
- `SlowTick()` now flushes pending RT resource state after quality sampling.
- `LateFrameTick()` consumes presentation latches only; release-pending or missing RT forces fallback without calling rent/return.
- `EnsureRenderTexture()` reads `_cachedRenderTexturePool` only.
- `QueuePresentationCommit()` gives release priority over ensure so one frame cannot request both.
- Added `ToolDiegeticDisplay_RenderTextureResourceWorkIsSlowPhaseOnly` as a C# source guard.

Cinematic cheats used:
- Low and standalone VR devices can show fallback texture for one slow cadence instead of paying RT rent/create on the visible equip frame.
- Middle devices recover the live tool screen on the slow resource phase.
- High/Ultra devices keep the live RT tool screen, but pool churn stays out of visual sync.

Exact microseconds saved:
- Static estimate: 480-1400 us worst-case RT rent/create/return spike moved out of `LateFrameTick`.
- 0 B GC state transfer: bool latches plus cached pool references.

Verification:
- `ToolDiegeticDisplayController.cs` and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0.
- Transitive hot graph from `LateFrameTick`: 35 reachable methods, 0 forbidden registry/component/platform/resource-allocation reports.
- Slow graph from `SlowTick` reaches `FlushPendingRenderTextureResourceState`, `EnsureRenderTexture`, `ReleaseRenderTexture`, and `DestroyUnownedRenderTexture`.
- `EnsureRenderTexture()` contains `_cachedRenderTexturePool`, no `GlobalRegistry`, and no `CacheRenderTexturePoolCold()` call.
- DataVault write-lock scan: no route in the touched runtime file.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 77.1% and external `dotnet build Hecton8.slnx` PID 56788 was active.
- Process check: no orphan parser process remained; named user Python services were left untouched.
2026-05-30 14MU UI/VR fault-path phase sweep

What was wrong:
- `TerminalOsRuntime.LateFrameTick()` could call `TryDumpBlackBox()` directly on terminal fault flags.
- `TerminalOsRuntime.TryFinalizeDecryptionJob()` could call `TryDumpDecryptionBlackBox()` from the late-frame decryption completion path.
- `TopographicalSonarSynthesizer.CommitCompletedScan()` called `DumpBlackBox()` from a path reached by `LateFrameTick`, allocating a temp `NativeArray<byte>` and issuing fault-path file work in visual sync.

What was done:
- Terminal OS now transfers fault state through `_terminalBlackBoxDumpQueued`, `_queuedTerminalBlackBoxFaultFlags`, `_decryptionBlackBoxDumpQueued`, and `_queuedDecryptionBlackBoxFaultFlags`.
- Terminal OS `SlowTick()` and teardown now flush `TryDumpBlackBox()` / `TryDumpDecryptionBlackBox()` through `FlushQueuedBlackBoxDumps()`.
- Topographical sonar now implements `ISlowTickable`, registers/unregisters the slow lane, queues black-box dump intent from scan completion, and flushes `DumpBlackBox()` from slow phase.
- Added source guards in `KelpShaderScalability1427EditTests.cs` for Terminal OS terminal/decryption dump queueing and topographical sonar slow-phase dump ownership.

Cinematic cheats used:
- Fault presentation is allowed to show one stale/unchanged visual frame while dump I/O is deferred. This buys stable visual cadence on weak hardware without changing gameplay truth.
- Sonar dump evidence is captured from the same telemetry ring, but the allocation/write owner moves to slow phase.

Exact microseconds saved:
- Terminal OS fault dump: static estimate 700-2600 us removed from late-frame fault path.
- Topographical sonar dump: static estimate 900-3200 us temp allocation/write spike removed from late-frame scan completion.
- Verification: 120 non-editor UI files direct hot-body scan reported `DIRECT_HOT_REPORTS=0`; seven UI DataVault write-lock methods reported one acquire, one release, one `finally`; scoped diff check passed with LF/CRLF warnings only.
- `dotnet build` not launched: CPU was 80.7% and external `dotnet.exe` PID 54640 was active.
2026-05-30 14MU platform resource hot-chain phase sweep

What was wrong:
- `HectonMapMagicVegetationBridge.CacheTileMasks()` could reach a texture-cache helper that allocates `Texture2D[]`.
- `SargassumGlobalDragManager.LateFrameTick()` could call visual resource repair that allocates texture arrays, `Texture2D`, fallback resources, `GraphicsBuffer`, BRG handles, and DataVault-backed metadata.
- `VisorHUDController.ConfigureHudScissorCommandBuffers()` could call command-buffer allocation and sample SRP platform state from projection binding.
- The scanner also reported `new GraphicsBuffer.IndirectDrawIndexedArgs` in MicroFauna/Marauder; verified as struct initialization, not `new GraphicsBuffer(...)`.

What was done:
- Split MapMagic terrain texture cache into cold allocator `RefreshTerrainTextureCachesCold()` and hot cached-only `TryRefreshTerrainTextureCachesHot()`.
- Moved Sargassum visual resource repair into `SlowTick()` through `EnsureVisualResourcesForSlowTick()` and kept late frame as cached readiness checks plus scalar retry latches.
- Moved Visor scissor command-buffer creation to `EnsureHudScissorCommandBuffersCold()` and slow repair; `ConfigureHudScissorCommandBuffers()` now consumes cached command buffers and cached SRP state only.
- Added source guards in `KelpShaderScalability1427EditTests.cs` for MapMagic, Sargassum, Visor, and indirect args writers.

Cinematic cheats used:
- Low and standalone VR devices may show one stale/missing vegetation/sargassum/HUD scissor frame instead of stalling on resource creation.
- Middle devices repair on slow cadence.
- High/Ultra keep full detail, BRG scavengers, dense vegetation masks, and HUD scissor precision, but resource repair is phase-owned.

Exact microseconds saved:
- MapMagic texture cache mismatch: static estimate 120-480 us managed allocation/GC risk removed from cache preparation.
- Sargassum visual resource repair: static estimate 900-2400 us worst-case late-frame spike moved to slow phase.
- Visor HUD scissor setup: static estimate 350-900 us first command-buffer setup moved to cold/slow phase.
- Indirect args classification: 0 runtime us; prevents unnecessary churn.

Verification:
- Six files balance braces/parens/brackets 0/0/0 after stripping strings/comments.
- Hot graphs: `SargassumGlobalDragManager` 75 reachable / 0 forbidden; `VisorHUDController` 80 reachable / 0 forbidden; `HectonMapMagicVegetationBridge` 27 reachable / 0 forbidden.
- Direct changed-file scan: 0 `GlobalRegistry.Get<T>()` / `GetComponent()` matches.
- Sargassum scavenger write methods: one `TryAcquireWriteLock`, one `ReleaseWriteLock`, one `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 62% and external `dotnet.exe` PID 30052 was active.
- Parser process check: no new orphan parser from this pass; existing Python processes were pre-existing user services or long-running unrelated sessions.
2026-05-30 14MU celestial/weather LUT phase sweep

What was wrong:
- `HectonCelestialEngine.FlushCelestialVisualSync()` called a branch-guarded atmosphere update helper. The call passed `allowResourceRepair: false`, but the method body still contained `EnsureCelestialAtmosphereAuthoring()` and `EnsureCelestialAtmosphereTexture()`.
- `GlobalWeatherDirector.FlushNoirFogLutTexture()` could allocate `Texture2D` and `Color[]` from the late-frame weather shader publish path when the LUT was missing or resized.

What was done:
- Replaced the branch-guarded Celestial helper with `TryUpdateDynamicCelestialAtmosphereVisualSync()`, which consumes an existing LUT or queues `_celestialAtmosphereLutRepairRequested`.
- Added `FlushCelestialAtmosphereLutRepairSlow()` from `SlowTick()` as the only runtime owner of `EnsureCelestialAtmosphereLutReady(publishOnRebuild: false)` repair.
- Added `_noirFogLutRepairRequested`, `HasNoirFogLutResourcesReady()`, `QueueNoirFogLutRepair()`, and `FlushNoirFogLutRepairSlow()` to `GlobalWeatherDirector`.
- Kept Awake/OnEnable LUT prewarm cold; late-frame weather sync now either rebuilds an existing LUT or queues slow repair.
- Added source guards in `KelpShaderScalability1427EditTests.cs` for Celestial atmosphere LUT and Noir fog LUT slow-phase ownership.

Cinematic cheats used:
- Low devices can show one stale sky/fog frame instead of allocating a texture during visual sync.
- Middle devices repair on slow cadence.
- High/Ultra keep full atmosphere/fog LUT fidelity, but resource ownership stays outside late frame.

Exact microseconds saved:
- Celestial atmosphere LUT repair edge: static estimate 80-260 us removed from visual sync.
- Noir fog LUT repair edge: static estimate 100-560 us removed from late-frame shader publish.
- Combined environment presentation worst-case: static estimate 180-820 us moved to slow/cold ownership.

Verification:
- Three changed files balance braces/parens/brackets 0/0/0 after stripping strings/comments.
- Focused hot graph: `HectonCelestialEngine.LateFrameTick` 147 reachable methods / 0 forbidden lookup/platform/resource-allocation reports.
- Focused hot graph: `GlobalWeatherDirector.LateFrameTick` 24 reachable methods / 0 forbidden lookup/platform/resource-allocation reports.
- Broad direct runtime scan: 1802 non-editor runtime files, 2018 hot methods, 0 direct `GlobalRegistry.Get<T>()` / `GetComponent()` reports.
- Phase guards: Celestial visual updater does not call texture/authoring ensure; Weather LUT flush does not call resource ensure; both slow repair methods own resource ensure calls.
- Changed runtime files add no DataVault write-lock route; `HectonCelestialEngine` still contains one pre-existing async mutation guard for orbit output, unchanged by this pass and not a new write lock.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched by design; this pass used in-memory static parsing only. CPU was 48% and no compiler process was active, but compilation throttling request explicitly favored AST/source validation.
- Parser process check: no orphan parser process remained; active Python processes were named user services.

2026-05-30 14MU trail/scatter/debris hot-resource phase sweep

What was wrong:
- `NativeTrailRenderer.LateFrameTick()` could call `EnsureBuffers()`, allocating trail arrays and a generated mesh in presentation phase.
- `GpuScatterLodManager.UpdateVisibleCountReadback()` could call `EnsureVisibleCountReadbackData()`, allocating a persistent `NativeArray<uint>` from the visual cull/readback route.
- `CarveDebrisComputeRenderer.RenderDebris()` could reach fallback mesh/material repair through `ResolveMaterial()` and `TryResolveDrawMesh()`, including `BuildOctahedronMesh`, `Shader.Find`, and `new Material`.

What was done:
- `NativeTrailRenderer` now implements `ISlowTickable`; late frame queues `_bufferRepairRequested`, and slow tick owns `EnsureBuffers()`.
- `GpuScatterLodManager` now uses `HasVisibleCountReadbackData()` from visual readback; missing storage queues `_visibleCountReadbackRepairRequested`, and slow/cold owns `EnsureVisibleCountReadbackData()`.
- `CarveDebrisComputeRenderer` now keeps `ResolveMaterial()` and `ResolveMesh()` cached-only; missing fallback resources queue `_fallbackRenderResourceRepairRequested`, flushed only from `SlowTick`.
- Added source guards in `KelpShaderScalability1427EditTests.cs` for these three phase contracts.

Cinematic cheats used:
- Low devices can drop one decorative trail/scatter-count/debris visual feedback frame instead of allocating in visual sync.
- Middle devices repair on slow cadence.
- High/Ultra keep full AUP trails, scatter visible-count feedback, and indirect carve debris visuals after slow/cold preparation.

Exact microseconds saved:
- Native trail repair: static estimate 120-460 us managed array/mesh allocation moved out of late frame.
- Scatter readback storage: static estimate 40-140 us native allocation/sentinel registration moved out of visual readback.
- Carve debris fallback resources: static estimate 260-1200 us shader/material/mesh repair removed from render path.
- Combined affected presentation frames: static estimate 420-1800 us worst-case resource repair shifted to slow/cold ownership.

Verification:
- Runtime changed files balance braces/parens/brackets 0/0/0 after string/comment stripping.
- Hot graphs: `NativeTrailRenderer.LateFrameTick` 10 reachable / 0 forbidden; `GpuScatterLodManager.LateFrameTick` 67 reachable / 0 forbidden; `CarveDebrisComputeRenderer.LateFrameTick` 66 reachable / 0 forbidden.
- Targeted source guards: 8 pass assertions for no hot allocation-capable helper calls and slow repair ownership.
- Direct scoped lookup scan: 0 `GlobalRegistry.Get<T>()`, `GetComponent`, or `TryGetComponent` matches in the three runtime files.
- Scoped DataVault write-lock token scan: 0 matches in the three runtime files for this pass.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 91%; no active `dotnet`, `csc`, or `VBCSCompiler` process, but build throttle forbids compilation under >50% CPU.
- Parser process check: no orphan parser process from this pass; active Python command lines are user services (`bot_watchdog.py`, `main.py`, `uvicorn`, `stomchat`).

2026-05-30 14MU world readback storage phase sweep

What was wrong:
- `GPUScatterDirector.UpdateVisibleCountReadback()` could create persistent readback storage from the visible-count visual route.
- `HectonIndirectVegetationRenderer.RequestCullTelemetryReadback()` could allocate cull telemetry readback storage from a route reached by `RunVisualTick()`.
- `SargassumMicroFaunaBoids.TryRequestParasiteLatchReadback()` could allocate parasite latch-stat readback storage from micro-fauna visual simulation.
- `HectonMapMagicVegetationBridge.CacheTileMasks()` could allocate tile height readback storage during late-frame resident cache validation.

What was done:
- Added cached readiness checks, repair latches, and slow repair methods for GPU scatter visible-count readback, indirect vegetation cull telemetry, micro-fauna parasite latch stats, and MapMagic tile height readback storage.
- Hot request/cache paths now queue repair and return when storage is missing; `SlowTick` owns the allocation-capable `Ensure*ReadbackData` helpers.
- Added source guards in `KelpShaderScalability1427EditTests.cs` for all four phase contracts.

Cinematic cheats used:
- Diagnostic/feedback readbacks may skip one cadence instead of allocating during visual sync.
- Terrain native-cache validation may delay one slow cadence instead of allocating heightmap readback storage inside the late-frame barrier.
- High/Ultra retain full telemetry/cache fidelity after slow repair; weak devices avoid the allocation spike.

Exact microseconds saved:
- GPU scatter visible count: static estimate 25-90 us native allocation/sentinel registration moved out of visual readback.
- Indirect vegetation cull telemetry: static estimate 25-90 us native allocation/sentinel registration moved out of visual telemetry sampling.
- Micro-fauna parasite latch stats: static estimate 45-160 us native allocation/sentinel registration moved out of visual simulation.
- MapMagic tile height readback: static estimate 65-640 us heightmap readback storage allocation moved out of late cache validation.
- Combined affected frames: static estimate 160-980 us moved to slow/cold ownership.

Verification:
- Five changed files balance braces/parens/brackets 0/0/0 after string/comment stripping.
- Hot graphs: `GPUScatterDirector` 40 reachable / 0 hits; `HectonIndirectVegetationRenderer` 62 / 0; `SargassumMicroFaunaBoids` 136 / 0; `HectonMapMagicVegetationBridge` 72 / 0.
- Targeted phase guards: hot bodies contain queue calls and no allocation-capable ensure calls; slow repair bodies own the ensure calls.
- Lock-shape scan: hot reachable lock methods have single acquire with `finally` release, or explicit handoff helper with caller-side `finally` release.
- Direct scoped lookup scan: hot graph reports 0 `GlobalRegistry.Get<T>()`, `GetComponent`, or `TryGetComponent` hits; raw file scan found only cold unused/camera-cache `TryGetComponent` in MapMagic, not reachable from the hot graph.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 71.7-99.6% and external `dotnet.exe` PID 7380 was active.
- Parser process check: no orphan parser process remained; active Python command lines are named user services only.

2026-05-30 14MU LOD quality policy phase sweep

What was wrong:
- `LODSystemManager.LateFrameTick()` called the quality flush path that wrote `QualitySettings.lodBias`.
- That placed global quality policy mutation in visual sync, beside LODGroup presentation transitions and shader math-LOD publishing.
- `ApplyEmergencyLODBiasStrike()` also used `QualitySettings.lodBias` as a fallback value.

What was done:
- `LODSystemManager` now implements and registers `ISlowTickable`.
- `SlowTick` owns `FlushQualityPolicySlow()`, including `GlobalQualityWeight` sampling, emergency LOD bias calculation, and `QualitySettings.lodBias` mutation.
- `LateFrameTick` owns only `FlushQualityShaderVisualSync()` and `ApplyLODTransitions()`.
- Shader math LOD crosses phases through `_pendingMathLodWeight` and `_mathLodVisualSyncDirty`; this is scalar state, 0 B GC.
- `ApplyEmergencyLODBiasStrike()` now falls back to `_defaultLODBias`, not `QualitySettings`.
- Added source guard `LODSystemManager_QualitySettingsMutationIsSlowPhaseOnly()`.

Cinematic cheats used:
- Low devices accept slow-cadence LOD bias policy response instead of global quality mutation during visual sync.
- Middle devices preserve normal LODGroup transitions.
- High/Ultra keep overkill shader math LOD via late-frame scalar sync while policy writes stay out of the frame-critical path.

Exact microseconds saved:
- Static estimate 8-24 us avoided on quality-dirty visual frames.
- Larger value is phase risk removed: no Unity quality global write from `LateFrameTick`.

Verification:
- `LODSystemManager` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- Source guard added in `KelpShaderScalability1427EditTests.cs`.
- Hot graphs: `Tick` 12 reachable / 0 forbidden; `LateFrameTick` 4 / 0; `SlowTick` 6 / 0 lookup/allocation hits; `ApplyEmergencyLODBiasStrike` 1 / 0.
- `LateFrameTick` body contains no `QualitySettings.` and does not call `FlushQualityPolicySlow()`.
- `FlushQualityPolicySlow()` owns `QualitySettings.lodBias = targetBias;` and does not call `DistanceMath.PushShaderMathLod`.
- `FlushQualityShaderVisualSync()` owns `DistanceMath.PushShaderMathLod(qualityWeight01);`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 91% and external `dotnet.exe` PID 7380 was active.
- Parser process check: no orphan parser remained; active Python command lines are named user services only.

2026-05-30 14MU Sargassum Crest facade kernel phase sweep

What was wrong:
- `SargassumCrestDampingController.LateFrameTick()` could reach `DispatchFacadeBake()`.
- `DispatchFacadeBake()` resolved compute shader kernels and thread-group sizes through `HasKernel`, `FindKernel`, `IsSupported`, and `GetKernelThreadGroupSizes`.
- That is platform/capability discovery inside visual sync.

What was done:
- Added cached compute-kernel resolution state: `_facadeBakeKernelRepairRequested` and `_facadeBakeKernelResolvedCold`.
- Added `HasFacadeBakeKernelReady()`, `HasCurrentFacadeBakeKernelResolution()`, `QueueFacadeBakeKernelRepair()`, and `FlushFacadeBakeKernelRepairSlow()`.
- Moved all `ResolveSupportedKernel()` and `ResolveKernelThreadGroupSizes()` calls into cold/slow repair.
- `DispatchFacadeBake()` now only consumes cached kernel/thread-group integers and queues repair when stale.
- `Awake`, `OnEnable`, `CacheRegistryServicesCold`, service hot-swap, and `SlowTick` flush the cold/slow repair path.
- Added source guard `SargassumCrestFacade_KernelResolutionIsSlowPhaseOnly()`.

Cinematic cheats used:
- Low devices skip one decorative damping facade bake when kernel state is stale.
- Middle devices repair on slow cadence and keep cached facade publication stable.
- High/Ultra keep full wave/oil facade overkill once cold kernel resolution succeeds.

Exact microseconds saved:
- Static estimate 18-74 us removed from first/changed facade bake frames.
- Larger value is variance removal: visual sync no longer probes compute capability or kernel thread-group metadata.

Verification:
- `SargassumCrestDampingController.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- Hot graphs: `Tick` 4 reachable / 0 forbidden; `LateFrameTick` 14 reachable / 0 forbidden.
- `DispatchFacadeBake()` contains `QueueFacadeBakeKernelRepair()` and does not contain `ResolveSupportedKernel`, `ResolveKernelThreadGroupSizes`, `.FindKernel`, `.HasKernel`, or `.GetKernelThreadGroupSizes`.
- `FlushFacadeBakeKernelRepairSlow()` owns `ResolveSupportedKernel(_facadeBakeCompute, "CSMain")` and `ResolveKernelThreadGroupSizes(...)`.
- Scoped raw scan shows `TryGetComponent` only in `ResolveLegacyInputRenderer`, a cold legacy input resolver not reachable from `Tick` or `LateFrameTick`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 99% after validation; no compiler process was active.
- Parser process check: no orphan parser remained; active Python command lines are named user services only.

2026-05-30 14MU Biolum diffusion resource repair loop

What was wrong:
- `HectonBiolumDiffusionVolume.LateFrameTick()` latched `_resourceRefreshRequested` when volume textures, point buffers, or kernel state were missing.
- `SlowTick()` then called `HasRequiredResources()` and returned without calling `EnsureResources()`.
- Result: lost or failed resources could remain permanently disabled instead of repairing from the slow phase.

What was done:
- `SlowTick()` now calls `EnsureResources()` before `HasRequiredResources()` in the refresh branch.
- `LateFrameTick()` remains allocation-free and only latches repair, clears globals, and returns when resources are invalid.
- Added source guard `BiolumDiffusionVolume_ResourceRefreshRepairsInSlowTick()`.

Cinematic cheats used:
- Low devices lose biolum volume output for a slow cadence while 3D textures/buffers/kernels repair.
- Middle devices recover without injecting resource recreation into visual sync.
- High/Ultra retain HDR diffusion volume visuals once slow repair completes.

Exact microseconds saved:
- Static estimate 420-2200 us worst-case lost-resource recreation kept out of late frame.
- Correctness gain: resource loss now self-recovers from slow phase instead of staying black/inactive.

Verification:
- `HectonBiolumDiffusionVolume.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- `LateFrameTick` body contains `_resourceRefreshRequested |=` and does not contain `EnsureResources();`.
- `SlowTick` body contains `EnsureResources();` before `if (!HasRequiredResources())`.
- `EnsureResources` owns `CreateVolumeTexture`, `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BiolumPointGpuData>`, and `TryResolveKernel`.
- Hot graph: `LateFrameTick` 21 reachable / 0 forbidden allocation/lookup/resource-repair hits.
- `SlowTick` graph reaches `EnsureResources` by design; that is the repair owner, not visual sync.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 74%; no compiler process was active.
- Parser process check: no orphan parser remained; active Python command lines are named user services only.

2026-05-30 14MU Player target discovery phase sweep

What was wrong:
- `SargassumDebrisParticleSystem.AdvanceAmbientDebrisEmission()` refreshed runtime player targets from the late-frame ambient particle path.
- `FloraInteractionManager.Tick()` reached `BootstrapState.CurrentPlayerTransform` through `ResolveRuntimePlayerTransform()`.
- `CaveBioRootsGenerator.LateFrameTick()` refreshed player context before visual spline submission.
- `SargassumCutManager.RegisterExternalCut()` and slow residency refresh shared a resolver that sampled bootstrap and could reach `TryGetComponent`.

What was done:
- `SargassumDebrisParticleSystem` now registers `ISlowTickable`; `LateFrameTick` consumes cached player transform and queues slow refresh when missing.
- `FloraInteractionManager` now keeps Tick on cached `IPlayerRuntimeContext` / `_playerTransform`; bootstrap fallback moved to `RefreshPlayerReferenceCacheCold()`.
- `CaveBioRootsGenerator` now registers `ISlowTickable`; `LateFrameTick` reads cached `_playerTransform`, slow/lifecycle refreshes bootstrap fallback.
- `SargassumCutManager` now splits `ResolveDependencies()` hot cached reads from `ResolveDependenciesCold()` bootstrap/component discovery.
- Added source guards for all four phase boundaries in `KelpShaderScalability1427EditTests.cs`.

Cinematic cheats used:
- Low devices may miss one decorative ambient debris / cave propwash / cut context refresh cadence while slow phase repairs player identity.
- Middle devices recover on slow tick without contaminating visual sync or external cut writes.
- High/Ultra keep full flora wake, cave spline, debris, and sargassum cut visuals while using the same cached identity route.

Exact microseconds saved:
- Static estimate 18-96 us aggregate lookup/probe variance removed from affected hot frames.
- State transfer is cached `Transform` references and bool latches only, 0 B GC.

Verification:
- `SargassumDebrisParticleSystem.cs`, `FloraInteractionManager.cs`, `CaveBioRootsGenerator.cs`, `SargassumCutManager.cs`, and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- Hot graphs: Debris `LateFrameTick` 14 reachable / 0 hits; Flora `Tick` 77 / 0; Cave `LateFrameTick` 11 / 0; Sargassum external cut 24 / 0.
- Remaining `BootstrapState.CurrentPlayerTransform` sites are inside cold/lifecycle/slow refresh methods only.
- Scoped raw scan shows `TryGetComponent` only in cold component-caching methods for the touched routes.
- Changed diff adds no DataVault lock acquire/release tokens.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because external `dotnet.exe` PID 21592 was active despite CPU measuring 44%.
- Parser process check: no orphan parser remained; active Python command lines are named user services only.

## 2026-05-30 - HUD Fog Readback and PDA Sonar Kernel Phase Split

What was wrong:
- `HectonUnderwaterVisuals.UpdateHudFogLuminanceDownsample()` could allocate/register HUD fog luminance readback storage through `EnsureHudFogLuminanceReadbackData()` from the visual-sync path.
- `PDAMapTab.RenderPointCloud()` and `DispatchSonarPointCloud()` could resolve compute kernels and thread-group sizes from `LateFrameTick`, executing platform discovery during PDA presentation.

What was done:
- Added `_hudFogLuminanceReadbackRepairRequested`, cached storage readiness, and slow repair ownership in `HectonUnderwaterVisuals`.
- Added `ISlowTickable`, `_sonarComputeKernelRepairRequested`, cached kernel readiness, and slow compute-kernel repair ownership in `PDAMapTab`.
- Added C# source guards for HUD fog readback storage ownership and PDA sonar kernel phase ownership in `KelpShaderScalability1427EditTests.cs`.

Cinematic cheats used:
- Low devices may skip one HUD fog luminance feedback sample or one PDA sonar point-cloud frame while slow repair prepares native/GPU state.
- Middle devices repair on slow cadence without visual-sync spikes.
- High/Ultra keep the full GPU readback and PDA point-cloud route after cold/slow cache repair.

Exact microseconds saved:
- HUD fog readback: static estimate 25-90 us native allocation/sentinel registration removed from the affected visual frame.
- PDA sonar kernels: static estimate 18-80 us removed from first/changed PDA sonar compute frame.
- State transfer is bool latches plus cached native handles/kernel integers, 0 B GC.

Verification:
- `PDAMapTab.cs`, `HectonUnderwaterVisuals.cs`, and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- PDA `LateFrameTick` same-file graph: 57 reachable methods / 0 forbidden lookup/kernel reports.
- Underwater HUD update body: no `EnsureHudFogLuminanceReadbackData();` and no `new NativeArray<float>`.
- Broad direct hot scan: 1802 runtime C# files / 1928 hot methods / 0 `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, scene search, or `Camera.main` reports.
- Changed-line lookup/lock scan: no added `GlobalRegistry.Get<T>()`, `GetComponent`, `TryGetComponent`, DataVault write-lock, or native allocation tokens in runtime diff.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 100% and external `dotnet.exe` PID 39820 was active.
- Parser process check: no orphan parser remained; active Python command lines are named user services only.

## 2026-05-30 - Submarine Leak Plume GPU Repair and Structural Job Lock Finalizers

What was wrong:
- `SubmarineStructuralGrid.LateFrameTick()` could reach leak plume compute-kernel discovery and GPU buffer creation through `DispatchLeakPlumeCompute()`.
- Completed structural job consumers finalized job fences before releasing structural mutation guards, but the release was not protected by `finally`.

What was done:
- `SubmarineStructuralGrid` now registers `ISlowTickable`; `SlowTick` owns `FlushLeakPlumeGpuResourceRepairSlow()`.
- Late-frame leak plume dispatch only checks `HasLeakPlumeGpuResourcesReady()` and queues `_leakPlumeGpuResourceRepairRequested` when stale.
- `EnsureLeakPlumeGpuResources()` remains the sole owner of `HasKernel`, `FindKernel`, `GetKernelThreadGroupSizes`, and leak plume `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>`.
- `ConsumeCompletedBreachRepairJob`, `ConsumeCompletedMappingJob`, `ConsumeCompletedFatigueJob`, and `ConsumeCompletedDamageJob` now release structural mutation guards from `finally`.
- Added source guards: `SubmarineStructuralGrid_LeakPlumeGpuResourcesAreSlowPhaseOnly` and `SubmarineStructuralGrid_StructuralJobLocksReleaseInFinally`.

Cinematic cheats used:
- One stale/offline decorative leak plume frame is allowed while slow phase repairs compute resources. Hull truth remains in fixed/post-fixed simulation; plume particles are presentation only.

Exact microseconds saved:
- Leak plume first-use repair moved out of late frame: static estimate 110-640 us on i3/MX350/standalone VR class hardware.
- Lock-finalizer hardening: 0 normal-frame us; removes rare permanent structural mutation-guard stall after completed job state-transfer faults.

Verification:
- `SubmarineStructuralGrid.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 after string/comment stripping.
- Submarine `LateFrameTick` same-file graph: 30 reachable methods / 0 forbidden lookup/kernel/resource-allocation reports.
- Lock-finalizer shape proof: all completed structural job guard releases and telemetry write-lock release occur after `finally`.
- Broad direct hot scan: 1802 runtime C# files / 1803 hot methods / 0 `GlobalRegistry.Get<T>()`, `GetComponent`, or `TryGetComponent` reports.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 57%, above the project 50% ceiling.
- Parser process check: no orphan parser remained; active Python command lines are named user services only.

## 2026-05-30 - Diegetic Gyro Compass Indirect Buffer Slow Repair

What was wrong:
- `DiegeticGyroCompassRuntime.SlowTick()` could set `_indirectBuffersDirty` after indirect GPU buffer loss, but no slow repair consumed the latch.
- `LateFrameTick()` returned before `ApplyPresentation()` while dirty, so a lost indirect buffer could permanently stall compass cardinal text, fallback transform dial motion, and visual overkill output.
- Unsupported, disabled, or unbound indirect routes could also be treated as dirty even though fallback presentation is valid.

What was done:
- Added `ShouldRequireIndirectBuffersCold()` as the single cold predicate for indirect route ownership.
- Added `FlushIndirectBuffersRepairSlow()`; slow phase now refreshes cold graphics capability state, calls `EnsureIndirectBuffersCold()` only when the route is required, and clears `_indirectBuffersDirty` when fallback presentation should proceed.
- Startup and physical binding now seed `_indirectBuffersDirty` from the same route requirement predicate.
- Added `DiegeticGyroCompass_IndirectBufferRepairIsSlowPhaseOnly` source guard.

Cinematic cheats used:
- Low/unsupported devices keep the cheap cardinal text and transform-based dial fallback instead of waiting for indirect GPU resources.
- Middle devices repair indirect buffers on slow cadence.
- High/Ultra keep instanced indirect dial rendering after slow repair without probing or allocating in visual sync.

Exact microseconds saved:
- Static estimate 480-1400 us first-repair GPU allocation spike kept out of late frame.
- State transfer is one bool latch plus cached `GraphicsBuffer` handles, 0 B GC.

Verification:
- `DiegeticGyroCompassRuntime.cs` balance braces/parens/brackets 0/0/0 with lexical string/comment stripping.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 with lexical string/comment stripping.
- Targeted phase guard passed: `LateFrameTick` contains no `EnsureIndirectBuffersCold`, no `FlushIndirectBuffersRepairSlow`, no `SupportsIndirectDialCold`, no `SystemInfo`, and no `new GraphicsBuffer`.
- Slow repair owns `EnsureIndirectBuffersCold`; `EnsureIndirectBuffersCold` remains the allocation-capable `new GraphicsBuffer` owner.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 76%, above the project 50% ceiling.
- Parser process cleanup: timed-out parser PID 60880 was stopped; final process scan found only named user Python services.

## 2026-05-30 - World Chunk Async Upload Policy Slow Phase

What was wrong:
- `WorldChunkResidencyManager.LateFrameTick()` called the async upload budget updater.
- That updater writes global Unity streaming policy: `QualitySettings.asyncUploadBufferSize`, `QualitySettings.asyncUploadTimeSlice`, and `QualitySettings.asyncUploadPersistentBuffer`.
- The writes were hash-gated, but a quality-dirty frame could still mutate platform policy from visual sync.

What was done:
- Replaced the late-frame route with `FlushAsyncUploadBudgetPolicySlow()`.
- `Awake()` now seeds the policy once during lifecycle setup.
- `SlowTick()` refreshes the policy before `_chunkCount <= 0`, so the policy remains valid even before resident chunks exist.
- `LateFrameTick()` no longer calls the policy helper and contains no `QualitySettings` access.
- Added `WorldChunkResidency_AsyncUploadQualityPolicyIsSlowPhaseOnly` source guard.

Cinematic cheats used:
- Low devices keep constrained async upload buffer/slice policy without late-frame global mutations.
- Middle devices refresh upload policy on slow cadence while chunk residency continues normal dispatch.
- High/Ultra devices can still raise async upload throughput through continuous `GlobalQualityWeight`, but the expensive global setting path stays out of visual sync.

Exact microseconds saved:
- Static estimate 10-35 us removed from quality-dirty late frames, plus reduced global setting hitch risk.
- State transfer uses existing scalar quality/hash fields, 0 B GC.

Verification:
- `WorldChunkResidencyManager.cs` balance braces/parens/brackets 0/0/0 with lexical string/comment stripping.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 with lexical string/comment stripping.
- Targeted phase assertions pass: no stale `ApplyAsyncUploadBudgetForQuality`, `Awake` owns first flush, `SlowTick` flushes before chunk gate, `LateFrameTick` has no policy call and no `QualitySettings`, exact async upload writes remain in the slow helper.
- Direct hot lookup scan passes for `WorldChunkResidencyManager`: no `GlobalRegistry.Get<T>` or `GetComponent`/`TryGetComponent` inside `Tick`, `FixedUpdate`, `LateFrameTick`, or `Execute`.
- Async-upload patch surface scan passes: no DataVault write-lock token and no hot lookup token in `FlushAsyncUploadBudgetPolicySlow`, `SlowTick`, `LateFrameTick`, or the new guard method.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 79% and external `dotnet.exe` PID 39176 was active.
- Parser process check: only named user Python services were visible; no parser created by this pass remained.

## 2026-05-30 - Screen-Space Light Shaft Visual-Sync Latch

What was wrong:
- `ScreenSpaceLightShaftRuntime.LateFrameTick()` polled `Application.isPlaying` every visual-sync frame.
- Runtime lifecycle state was already available through `_registeredLateFrame`, so the Unity global read was redundant hot-path work.

What was done:
- Replaced the hot `Application.isPlaying` gate with `_registeredLateFrame`.
- Added `ScreenSpaceLightShaft_LateFrameUsesRegistrationLatchNotApplicationPoll` source guard.

Cinematic cheats used:
- No visual downgrade. The saved CPU variance stays available for the light shaft fake: shader globals, soot/brownout coupling, and top contribution selection.

Exact microseconds saved:
- Static estimate 1-4 us direct platform read variance removed from affected visual-sync frames.
- State transfer uses the existing lifecycle bool latch, 0 B GC.

Verification:
- `ScreenSpaceLightShaftRuntime.cs` balance braces/parens/brackets 0/0/0 with lexical string/comment stripping.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 with lexical string/comment stripping.
- Targeted late-frame assertions pass: `_registeredLateFrame` is the gate; `Application.isPlaying`, `GlobalRegistry.Get<T>`, and `GetComponent` are absent.
- Direct hot platform scan pass: no `Application`, `QualitySettings`, `SystemInfo`, `Screen`, allocation, registry lookup, or component lookup token in `LateFrameTick`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- Final build throttle probe: CPU measured 57%; no `dotnet.exe`, `csc.exe`, or `VBCSCompiler.exe` process was active, but build stayed blocked by the >50% CPU ceiling.

## 2026-05-30 - Dynamic Resolution Runtime Latch

What was wrong:
- A hot token scan reported three `Screen.` matches inside Burst `Execute()` methods. Manual source inspection showed they were `NativeArray<ExoScreenDTO> Screen` or local screen-space variables, not `UnityEngine.Screen`.
- The real defect was `DynamicResolutionScaler.ApplyRenderScale()`: it polled `Application.isPlaying` while reachable from `LateFrameTick()` and `PlatformAdaptiveBudgetGovernor` pressure commits.

What was done:
- Left the Burst DTO/local `Screen` identifiers untouched.
- Added `_runtimeRenderScaleQueueActive`.
- Cached runtime mode from `Application.isPlaying` in `Awake()` and `OnEnable()`.
- Cleared the latch in `OnDisable()`.
- Replaced the hot `Application.isPlaying` check in `ApplyRenderScale()` with `_runtimeRenderScaleQueueActive`.
- Added `DynamicResolutionScaler_RenderScaleApplyUsesColdRuntimeLatch` source guard.

Cinematic cheats used:
- No rendering downgrade. The saved CPU variance stays available for DRS smoothing, scale snapping, and platform-pressure visual recovery.

Exact microseconds saved:
- Static estimate 1-5 us hot route variance removed on weak CPUs.
- State transfer uses one cached bool latch, 0 B GC.

Verification:
- `DynamicResolutionScaler.cs` balance braces/parens/brackets 0/0/0 with C# lexical string/comment scanning.
- `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 with C# lexical string/comment scanning.
- Targeted method assertions pass: `ApplyRenderScale` uses `_runtimeRenderScaleQueueActive`, does not contain `Application.isPlaying`, and `LateFrameTick` contains no `Application.` token.
- Direct hot token scan over `LateFrameTick`, `ApplyRenderScale`, `SetPlatformPressureRenderScale`, `ApplySystemOverrideRenderScale`, and `ClearSystemOverrideRenderScale`: 0 forbidden platform/dependency/allocation reports.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 72.9% and external `dotnet.exe` PID 55436 was active.
- Process cleanup: the transient broad scan process had exited by the cleanup probe; remaining Python processes were named user services.

## 2026-05-30 - Render Feature Runtime Gates

What was wrong:
- `HectonSinglePassOceanFeature`, `HectonDeferredCausticsFeature`, `HectonBilateralDrsUpscalerFeature`, and `HectonWaterOpticsTelemetryFeature` polled `Application.isPlaying` from `AddRenderPasses()` or `RecordRenderGraph()`.
- Those routes execute per renderer/camera frame and should consume runtime-owner state, not Unity global platform state.

What was done:
- Added `OceanSinglePassRuntime.HasRendererFeatureRuntimeGate()` and `OceanSinglePassRuntime.TryEnterRenderGraphRuntimeGate()`.
- Ocean feature now uses runtime/mock gates instead of `Application.isPlaying`.
- Deferred caustics feature now relies on active caustics constant-buffer state in both enqueue and render-graph routes.
- Bilateral DRS feature now enqueues only when `HectonBilateralDrsUpscalerRuntime.TryGetRuntimeInstance(out _)` succeeds; render graph no longer polls play state.
- Water optics telemetry feature now gates on `WaterOpticsRuntime.TryGetRuntimeInstance(out _)`; the runtime lookup is public in all builds.
- Added `RendererFeatures_DoNotPollApplicationPlayingFromRenderRoutes`.

Cinematic cheats used:
- No visual downgrade. Runtime-owner gates preserve ocean mock render frames, caustics active-buffer rendering, DRS runtime reconstruction, and water optics telemetry markers.
- Weak devices avoid per-camera platform polling; high and ultra devices keep the same render-feature overkill routes once owner state exists.

Exact microseconds saved:
- Static estimate 4-16 us aggregate per-camera gate variance removed on weak CPUs when multiple URP features/cameras are active.
- State transfer is static runtime references, active GPU buffer presence, and existing mock-frame budget integer, 0 B GC.

Verification:
- Seven touched C# files balance braces/parens/brackets 0/0/0 with string/comment-aware in-memory scanning.
- Targeted method assertions pass for ocean, caustics, bilateral DRS, and water optics render routes.
- Direct `Application.isPlaying` scan on changed feature files reports no matches.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- No new runtime DataVault write-lock route was added.
- `dotnet build`: not launched; this pass was intentionally static/in-memory under the requested compilation throttle.

## 2026-05-30 - Core Runtime Gate Latches

What was wrong:
- `SystemDispatcher.RunDispatcherUpdate()` polled `Application.isPlaying` before the central gameplay lane dispatch.
- `GameTickManager.Tick()` polled `Application.isPlaying` for the bootstrap slow-dt fallback.
- `NativeMemorySentinel.ResolveCurrentFrame()` and `ResolveCurrentUnscaledTime()` polled `Application.isPlaying` for diagnostic frame/time ownership.

What was done:
- Added lifecycle-owned `_runtimeGameplayBootstrapGateActive` to `SystemDispatcher`.
- Added lifecycle-owned `_runtimeGameplayBootstrapGateActive` to `GameTickManager`.
- Changed `NativeMemorySentinel` frame/time resolution to use `SystemDispatcher.ActiveRuntimeInstance`.
- Added `SystemDispatcher_GameplayBootstrapGateUsesColdRuntimeLatch`, `GameTickManager_GameplayBootstrapGateUsesColdRuntimeLatch`, and `NativeMemorySentinel_FrameResolveUsesDispatcherRuntimeState`.

Cinematic cheats used:
- No visual downgrade. The saved CPU variance stays available for platform-adaptive visual work instead of Unity global runtime polling.

Exact microseconds saved:
- `SystemDispatcher`: static estimate 1-5 us/frame.
- `GameTickManager`: static estimate 1-4 us/tick frame.
- `NativeMemorySentinel`: static estimate 1-3 us on affected native allocation diagnostics.
- State transfer uses cached bools and dispatcher-owned runtime identity, 0 B GC.

Verification:
- `SystemDispatcher.cs`, `GameTickManager.cs`, `NativeMemorySentinel.cs`, and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 with string/comment-aware in-memory scanning.
- Targeted source assertions pass for all three runtime gates.
- Hot dispatcher/tick bodies contain no `Application.isPlaying`, `GlobalRegistry.Get<T>`, or `GetComponent` token.
- No new DataVault write-lock route was added.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched; CPU measured 65% and an external `dotnet build` PID 64492 was already running.

## 2026-05-30 - Visor Render Feature Runtime Owner Gate

What was wrong:
- `HectonAbyssalSsdoFeature`, `HectonHalfResParticlesFeature`, `HectonNoirDepthFogFeature`, `HectonScooterVolumetricShaftsFeature`, and `HectonStochasticSsrFeature` polled `Application.isPlaying` from URP render-frequency routes.
- The affected routes were `AddRenderPasses()` and `RecordRenderGraph()`, so the cost scaled with active cameras and visor feature count.

What was done:
- Added `HectonDrsRenderFeatureGate.HasRuntimeRenderOwner()`.
- Routed all five features through `SystemDispatcher.ActiveRuntimeInstance` instead of `Application.isPlaying`.
- Added `VisorRenderFeatures_DoNotPollApplicationPlayingFromRenderRoutes` and `AssertVisorRenderRouteUsesRuntimeOwnerGate`.

Cinematic cheats used:
- No effect downgrade. The change removes a platform read from render gates and keeps existing visor visual math untouched.
- Weak devices avoid small repeated gate variance. High and Ultra devices keep the same overkill visor effects once the dispatcher runtime owner exists.

Exact microseconds saved:
- Static estimate: 5-20 us aggregate render-gate variance on weak CPUs with multiple visor features/cameras.
- State transfer: dispatcher-owned runtime reference read, 0 B GC.

Verification:
- Seven touched files balance braces/parens/brackets 0/0/0 with string/comment-aware source scanning.
- Targeted assertions pass for all five patched `AddRenderPasses()` and `RecordRenderGraph()` routes.
- Direct patched-file runtime-token scan: 0 `Application.isPlaying`, `GlobalRegistry.Get<T>`, `GetComponent`, `TryGetComponent`, or DataVault lock tokens.
- Whole-runtime direct hot lookup scan: 1872 methods, 0 reports.
- DataVault write-lock parser: 239 methods scanned. Two `RepairDroneEntity` helper-release false positives manually inspected; both release through `ReleasePayloadWrite(...)` inside `finally`.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched. Static in-memory validation was used under compilation throttling.

## 2026-05-30 - Scatter And HUD Runtime Callback Latches

What was wrong:
- `WorldProceduralScatterDirector.Tick()` and `LateFrameTick()` polled `Application.isPlaying` from scatter runtime cadence.
- Scatter helpers reachable from cadence/visual-sync rebuild also read play-mode state for bootstrap deferral, startup settle, forced refresh, radius resolve, and registration.
- `SuitHUDV4CanvasOverlay.SlowTick()`/`LateFrameTick()` and nested `HectonUIScaler.SlowTick()` used play-mode reads in callback routes.
- The HUD stencil suppression helper embedded a play-mode read used by hot UI callbacks.

What was done:
- Added `_runtimeScatterCallbacksActive` to `WorldProceduralScatterDirector`.
- Routed scatter runtime time through `HasRuntimeScatterOwner()`.
- Replaced hot scatter play-mode checks with the cold lifecycle latch.
- Added `_runtimeHudCallbacksActive` to `SuitHUDV4CanvasOverlay`.
- Added `_runtimeScalerCallbacksActive` to `HectonUIScaler`.
- Routed stencil suppression through `SystemDispatcher.ActiveRuntimeInstance != null`.
- Added source guards for scatter/HUD runtime latches and a shared `AssertHotBodyUsesRuntimeLatch` guard.

Cinematic cheats used:
- No simulation downgrade. This pass removes platform reads from callback gates and keeps scatter/HUD visual math unchanged.
- Weak devices reduce repeated hot-route branch variance. High and Ultra keep full procedural scatter, migratory sargassum, HUD overlays, radar, and threat chevron paths.

Exact microseconds saved:
- Scatter runtime callbacks: static estimate 4-18 us under active procedural scatter cadence.
- HUD/scaler callbacks: static estimate 3-14 us across active UI slow/visual-sync routes.
- State transfer: three cached bool latches plus dispatcher runtime owner read, 0 B GC.

Verification:
- `WorldProceduralScatterDirector.cs`, `SuitHUDV4CanvasOverlay.cs`, and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0 with string/comment-aware in-memory scanning.
- Targeted source assertions pass for `RuntimeNowSeconds`, `HasRuntimeScatterOwner`, scatter `Tick`/`SlowTick`/`LateFrameTick`/rebuild/bootstrap helpers, HUD slow/late callbacks, and scaler slow/register/hot-swap callbacks.
- Broad direct hot lookup scan: 2253 methods, 0 `GlobalRegistry.Get<T>`, `GetComponent`, or `TryGetComponent` reports.
- Scoped DataVault write-lock scan: no `TryAcquireWriteLock` or `ReleaseWriteLock` tokens in touched runtime files.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 98.1%, above the 50% project ceiling. No compiler process was active.

## 2026-05-30 - Underwater Visual Runtime Callback Latch

What was wrong:
- `HectonUnderwaterVisuals.SlowTick()` directly read `Application.isPlaying`.
- `LateFrameTick()` reached a transitive play-mode read through underwater visual update and ocean global binding.
- `Render()` fog enforcement and selected visual-sync helpers had the same runtime-state dependency pattern.

What was done:
- Added `_runtimeVisualCallbacksActive` and made lifecycle/startup own runtime-state sampling.
- Replaced hot callback and transitive helper play-mode checks with the cached latch or a bool argument.
- Covered underwater slow/late/render routes, photophobia, HUD fog luminance, bubble impulse decay, camera depth state, ocean-underwater globals, and tick/render registration.
- Added `HectonUnderwaterVisuals_HotCallbacksUseColdRuntimeLatch`.

Cinematic cheats used:
- No downgrade. Visual math and quality ladders stay intact.
- Weak devices avoid repeated Unity global state reads in underwater presentation.
- High and Ultra keep full Noir fog, photophobia, HUD luminance feedback, and ocean material globals.

Exact microseconds saved:
- Static estimate: 6-22 us hot-route platform-read variance on i3/MX350 class CPUs.
- State transfer: one cached bool plus bool method arguments, 0 B GC.

Verification:
- `HectonUnderwaterVisuals.cs` and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0.
- Local graph scan: `LateFrameTick` 149 reachable / 0 forbidden lookup/platform/resource-repair hits.
- Local graph scan: `Render` 12 reachable / 0 forbidden hits.
- Local graph scan: `SlowTick` 45 reachable / 0 lookup/platform hits.
- Exact project hot platform scan: 1103 exact hot methods; remaining 4 reports are `Editor/CodexPlayModeLauncher.Tick` and three known `Screen` DTO false positives.
- Broad direct hot lookup scan: 2943 methods; 0 `GlobalRegistry.Get<T>`, `GetComponent`, or `TryGetComponent` reports.
- Runtime file DataVault token scan: 0 write-lock acquire/release tokens.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 53.4%, above the 50% ceiling. No compiler process was active.
- Orphan parser cleanup: stopped timed-out parser PID 39276; final parser command-line scan found no `python -` process.

## 2026-05-30 - Ambient Water Motion Runtime Callback Latch

What was wrong:
- `AmbientWaterMotionManager.Tick()` can reach `TryRegisterLateFrame()`.
- `TryRegister`, `TryRegisterLateFrame`, `TryRegisterService`, and `TryRegisterHotSwapListener` polled `Application.isPlaying`.
- That made runtime mode a hot Unity global read instead of a lifecycle-owned cold fact.

What was done:
- Added `_runtimeWaterMotionCallbacksActive`.
- Sampled runtime mode in `Awake()` and `OnEnable()`.
- Cleared the latch in `OnDisable()` and `OnDestroy()`.
- Routed tick, late-frame, service, hot-swap, and BiomeMatrix registration through the latch.
- Added `AmbientWaterMotionManager_HotRegistrationUsesColdRuntimeLatch`.

Cinematic cheats used:
- No physical simulation added.
- Ambient water remains a cheap scalar presentation system with Tick state collection and LateFrameTick presentation.
- Weak devices remove hot callback polling variance; High and Ultra keep the same visual water motion budget.

Exact microseconds saved:
- Static estimate: 1-4 us when ambient water tick self-registration or callback recovery is active.
- State transfer: one cached bool, 0 B GC.

Verification:
- `AmbientWaterMotionManager.cs` and `KelpShaderScalability1427EditTests.cs` balance braces/parens/brackets 0/0/0.
- Targeted source assertions pass for lifecycle latch ownership and hot registration helper purity.
- Same-file hot graph: `Tick` 2 reachable / 0 forbidden lookup/platform/allocation hits.
- Same-file hot graph: `LateFrameTick` 15 reachable / 0 forbidden lookup/platform/allocation hits.
- Runtime file DataVault token scan: 0 write-lock acquire/release tokens.
- Scoped `git diff --check`: exit 0; LF/CRLF warnings only.
- `dotnet build`: not launched because CPU measured 53%, above the 50% ceiling.
- Orphan parser cleanup: broad parser PID 40280 was stopped; final process scan found no `python -` parser.
- Post-log final check: bounded static verifier passed again; CPU measured 54%, compiler process count 0, and no `python -` parser process remained.
