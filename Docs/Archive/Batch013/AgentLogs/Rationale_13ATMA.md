# Rationale_13ATMA

Date: 2026-05-27
Status: BUILD FAILED - EXTERNAL VENDOR/WORKSPACE DEPENDENCY ERRORS

## Decision 001 - Missing Batch XML

Problem: User ID `13ATMA` has no `<AGENT_PROMPT id="13ATMA">` block in `Docs/Tasks/CURRENT_BATCH.md`. Neighboring prompts 1323/1324 are submarine gas-memory tasks, not sky/celestial beauty.
Solution: Treat the direct user message as the active one-task directive and keep all proof artifacts under `13ATMA`.
Rejected Alternatives: Inheriting 1323/1324 would violate strict parsing and cross-domain ownership. Waiting for a wipe is not required because `Status_13ATMA.md` was missing, not stale.
Scalability potential: Keeps audit bounded to Echelon 7/presentation atmosphere so low/middle/high/ultra decisions stay about visual sky/weather cost, not unrelated gas solver memory.
Hardware Impact: Avoids touching submarine gas systems and triggering compile risk on i3/MX350 with no domain gain.

## Decision 002 - Mandate Set

Problem: Atmosphere/celestial work can drift into expensive simulation, global polling, and pretty-but-useless visuals.
Solution: Use eight mandates: `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `CORE_Weather_Abyssal_FlowField_Currents`, `REND_Abyssal_Lighting_Voxel_Occlusion_Shadows`, `REND_Shader_Noir_Aesthetics_Dithering_Fog`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits`, `MATH_AUP_Determinism_Sync`, `ARCH_Global_Registry_ServiceLocator_DI_Init`.
Rejected Alternatives: Scientific orbit/weather simulation by default; binary low/high quality switches; hot registry polling; clean decorative skybox.
Scalability potential: Low uses LUT/dither/depth fog and cheap celestial wave math; middle adds denser fog/light detail; high adds richer raymarch/shafts; ultra buys overkill presentation without changing gameplay truth.
Hardware Impact: MX350 path targets 0 B hot-path GC and sub-0.1 ms suspicious-system budget; high-tier saved cycles buy more fog, shafts, and celestial presentation.

## Decision 003 - Edit-Mode Sky Preview Was Dead

Problem: `ObserverRelativeCelestialBody` and `SkySystemFollowCamera` are `[ExecuteAlways]`, but their editor code paths returned when `!Application.isPlaying`. That made authored sky placement, Scene View sky follow, sunrise/sunset/orbit preview, and `OnValidate` feedback unreachable in edit mode.
Solution: Removed the play-mode rejection from the editor compile guards while preserving the `EditorApplication.isCompiling` early-out. Added editor tests that prove `SkySystemFollowCamera` applies Scene View follow from `OnEnable` and `ObserverRelativeCelestialBody` captures parent-local sky direction from `OnEnable`.
Rejected Alternatives: Leaving preview as play-mode-only; adding a separate editor tool; forcing designers to call private methods or enter play mode. Those options hide broken authored orbit/sky layout until runtime.
Scalability potential: Low/middle/high/ultra all benefit because authored sky geometry is validated before runtime. Low devices avoid wasted runtime correction; high/ultra devices get reliable cinematic composition for moons, horizons, and orbital silhouettes.
Hardware Impact: Estimated runtime gain is indirect but real: fewer runtime fallback solves and no editor-authored bad placement shipping to i3/MX350. Scene preview cost remains editor-only.

## Decision 004 - Hot Registry Reads In Observer Placement

Problem: `ObserverRelativeCelestialBody` resolved `GlobalRegistry.Player` while finding the observer camera and resolved `GlobalRegistry.Atmosphere` from `ResolveTimeSeconds()`. That violates the local rule that hot runtime context owners publish once and consumers read cached interfaces/snapshots.
Solution: Added cold cached `IPlayerRuntimeContext` and atmosphere runtime binding, refreshed by `IGlobalRegistryHotSwapListener`. `ResolveTimeSeconds()` now only reads the cached atmosphere reference or presentation clock. `CurrentDirection` calls a non-mutating solve path.
Rejected Alternatives: Keeping repeated registry reads; using `Camera.main`; doing scene search from the getter. Standard Unity convenience routes are too opaque and too costly for a sky placement path.
Scalability potential: Low tier uses cached camera/time routes with no scene scan unless fallback is missing. Middle/high/ultra can spend saved budget on richer fog/shaft/celestial visuals without changing truth ownership.
Hardware Impact: Removes repeated global lookups from celestial late-frame placement. Estimated savings on i3/MX350: 2-8 us/frame in bad fallback scenes, plus removes nondeterministic lookup order risk.

## Decision 005 - Public Celestial Direction Getter Needed To Be Pure

Problem: `CurrentDirection` could call `ResolveParentDirection()`, which cached `_parentObserverRelativeBody`, and could pass through observer resolution. Read accessors must not mutate global/local state or perform hidden component caching.
Solution: Added an `allowReferenceCaching` solve parameter. Owned placement paths keep caching allowed; public `CurrentDirection` passes `false`. Added a regression test proving `_parentObserverRelativeBody` remains null after reading `CurrentDirection`.
Rejected Alternatives: Documenting the getter as impure; returning stale direction only; duplicating the whole orbit solver. Those either violate doctrine or add maintenance risk.
Scalability potential: Pure read route lets other systems sample celestial direction safely at any quality tier without causing hidden hierarchy work. Ultra visuals can query more often without accidental cache mutation.
Hardware Impact: Prevents surprise `TryGetComponent` work from read consumers. Estimated gain on MX350 scenes with multiple orbiting bodies: 1-5 us per sampling burst and lower frame-time variance.

## Decision 006 - Weather Biome Depth Route Was A Hot Singleton Poll

Problem: `GlobalWeatherDirector.ResolveCurrentBiomeDepthMeters()` called `BiomeMatrixDirector.ActiveRuntimeInstance` during weather tick/LUT state updates. That is a hot singleton lookup in a presentation-weather path.
Solution: Cached `BiomeMatrixDirector` from `GlobalRegistry.BiomeMatrix` during dependency resolution and refreshed it on `GlobalRegistryServiceSlot.BiomeMatrixRuntime` hot swap.
Rejected Alternatives: Keeping `ActiveRuntimeInstance`; querying scene objects; moving biome depth ownership into weather. The biome matrix is already the fact owner; weather only needs a cached read.
Scalability potential: Low tier gets cheap fog LUT/depth blend. Middle/high/ultra can increase LUT/fog richness without accumulating global lookup cost.
Hardware Impact: Estimated MX350 gain: 1-3 us per weather update path and cleaner dependency proof for frame spikes.

## Decision 007 - Build Gate Blocked By Machine Load

Problem: Local rules forbid launching `dotnet build` when another dotnet/csc build is running or CPU is above 50 percent. The first legal build window opened, so `dotnet build Hecton8.slnx --no-restore` was executed. It failed after warning-heavy Unity/package output, and the actual error lines were not isolated before transcript truncation. The build left idle MSBuild node-reuse workers; after `dotnet build-server shutdown`, CPU returned to 57-99 percent, blocking an errors-only rerun.
Solution: Static proof was completed with `git diff --check` and targeted `rg` scans. The build failure is recorded as unresolved workspace compile failure, not a green pass. No second compile was launched while CPU was above threshold.
Rejected Alternatives: Starting another build despite explicit prohibition; reporting compile success without running it; killing unknown active build jobs; pretending warning noise was the failure cause without the actual error lines.
Scalability potential: Avoids creating false contention in the 20+ agent workspace. Build queue remains available for the integrator when the machine is not saturated.
Hardware Impact: Prevents avoidable CPU and IO contention on low-end/loaded hardware. Runtime microsecond claims remain estimates until a clean Unity compile/test pass is available.

## Decision 008 - Firmament Bake Used Binary VRAM Buckets

Problem: `HectonCelestialEngine` selected firmament cubemap resolution through hard MX350/mid/high VRAM buckets. The same resolver also published telemetry and mutated `_firmamentResolutionWarningPublished`, violating the continuous `GlobalQualityWeight` rule and read/resolve purity doctrine.
Solution: Replaced the binary caps with `ComputeFirmamentCubemapResolution(int requested)`: continuous quality curve from `HomeostasisBrain.GlobalQualityWeight`, continuous graphics-memory budget from survival to overkill memory, hardware texture cap, and a power-of-two floor snap so the cubemap never exceeds the resolved budget. Warning publication now happens in `PublishFirmamentResolutionClampWarningIfNeeded()`, outside the pure compute path.
Rejected Alternatives: Keeping fixed `<=MX350`, `<=mid`, `high` buckets; forcing 8K on all hardware; adding a physical star simulation; leaving telemetry publication hidden in a `Resolve*` method. Each option either breaks the systemic quality mandate or spends frame/VRAM currency without improving player-facing sky belief.
Scalability potential: Low uses survival-scale 2K-oriented firmament and can fall lower when `GlobalQualityWeight` is near 0. Middle grows smoothly instead of stepping. High and Ultra can reach 8K only when both quality and memory budgets allow it, preserving visual-overkill headroom without changing celestial truth ownership.
Hardware Impact: MX350/i3 avoids accidental 4K/8K cubemap allocation and dispatch pressure; estimated startup/render-memory protection is multiple milliseconds of avoided bake work and tens to hundreds of MB of VRAM depending on requested resolution. Runtime hot-path GC remains 0 B by static inspection; measured profiler proof is still absent.

## Decision 009 - Solution Compile Blocked Outside 13ATMA Scope

Problem: Legal errors-only build ran after CPU/build gate passed, but solution compile still failed with 364 errors in third-party/vendor and workspace hygiene areas unrelated to the atmosphere/celestial patch.
Solution: Treat compile as red, record visible blocker classes, and avoid out-of-domain vendor surgery. Ran `dotnet build-server shutdown` after `VBCSCompiler` remained resident.
Rejected Alternatives: Editing Astar/MapMagic/MeshBaker/Technie/Candice/ShaderGraph from a sky-domain task; hiding the failed compile; launching repeated builds after one full errors-only failure. Those choices would either violate domain boundary or waste the shared machine.
Scalability potential: Keeping the sky patch isolated avoids compounding global package contamination. The firmament fix remains static-proofed and can be validated once vendor references are repaired by the owning integrator/package hygiene agent.
Hardware Impact: Build cleanup removes resident compiler server pressure from low-end shared hardware. No new runtime overhead was introduced by the compile attempt; measured runtime impact remains pending Unity/profiler proof.

## Decision 010 - Surface Weather Editor Contract And Read Purity

Problem: `HectonSurfaceWeatherDirector.Reset()` and `OnValidate()` rejected edit mode, so authored surface weather defaults, child `SurfaceWeatherVfxRig` binding, and serialized depth clamps did not run when designers edited the component. The same file also used mutating `Resolve*` dependency binders, and late-frame ocean visual application could refresh the ocean binding through a read-looking method.
Solution: Removed the `!Application.isPlaying` editor rejection while preserving the compile guard. Renamed mutating dependency paths to `RefreshPlayerMovementReference`, `RefreshOceanKinematicsBinding`, `RefreshSceneOwnedReferences`, and `RefreshOwnedWeatherVfxRig`. Added `ReadCachedOceanKinematics()` and made ocean default/cache/apply/restore paths consume the cached provider only. Added edit-mode regression probes for owned VFX rig binding and suppression-depth clamp.
Rejected Alternatives: Keeping surface weather authoring as play-mode-only; calling `GlobalRegistry.OceanKinematics` or a service resolver during late-frame visual binding; leaving mutating dependency work behind `Resolve*` names. Those options hide editor defects and break the read/resolve doctrine.
Scalability potential: Low hardware gets authored weather bounds and VFX references corrected before runtime, avoiding fallback churn. Middle tier keeps stable storm/ocean visual coupling. High and Ultra can spend the saved budget on richer spray, lightning, rain, and fog presentation without changing weather truth ownership.
Hardware Impact: Removes a hidden service refresh path from late-frame ocean application and restoration. Estimated MX350/i3 gain is small but useful: 1-4 us/frame variance reduction in scenes where ocean provider rebinding would otherwise run from visual apply/restore paths. Edit-mode validation cost is 0 runtime us.

## Decision 011 - Seismic AUP Float Cast Could Create False Local Shake

Problem: `SeismicWaveMath.CalculateSeismicDisplacement()` and the seismic event job subtracted AUP positions in `double3`, then cast the finite delta to `float3` before proving it was inside the active wavefront. A huge but finite AUP separation can become float infinity; the old fallback treated non-finite distance as 1 meter, producing false local camera shake, turbidity, seismic direction, and downstream sky/weather presentation noise.
Solution: Added shared seismic distance constants and bounded influence in double precision before any float conversion. Public displacement returns `float3.zero` for non-finite or out-of-wave AUP deltas. The job path now converts to float only inside the bounded influence radius; far or overflowing deltas resolve to zero local falloff instead of fake 1 m proximity. Regression tests cover far finite AUP rejection and near-wave finite displacement after AUP subtraction.
Rejected Alternatives: Clamping an infinite `float3` to a large vector, keeping the old 1 m fallback, running a physical seismic propagation solver, or editing unrelated 13KRA volumetric/light dump routes. Clamping keeps a lie in the math; a physical solver violates the cinematic cheat mandate and frame budget; 13KRA files have a different documented owner.
Scalability potential: Low tier skips impossible far-wave noise and avoids false shake. Middle tier keeps stable local seismic/sky coupling. High and Ultra can spend quality-controlled wave/noise detail only when the receiver is actually near the active front, without changing truth ownership or DTO layout.
Hardware Impact: Avoids `noise.snoise`, arrival/falloff math, and camera/turbidity accumulation for impossible far AUP deltas. Estimated i3/MX350 gain is 0.5-2 us per far seismic event consumer and, more importantly, removes a deterministic false-positive shake path. Near-wave cost is unchanged.

## Decision 012 - Storm Runtime Claimed Authority In Edit Mode

Problem: `ShinobuStormPropagationRuntime.OnEnable()` executed runtime claim, hot-swap/origin-shift registration, and DataVault setup even when the component was enabled outside play mode. The component is not an editor preview tool; claiming `s_runtimeClaimed` in edit mode makes scene authoring able to contaminate runtime singleton state and can block the intended after-scene-load runtime from becoming the sole owner until subsystem reset.
Solution: Added an early `!Application.isPlaying` guard before runtime claim/registry/listener/vault work. Added `StormPropagationRuntimeDoesNotClaimRuntimeInEditMode` to prove edit-mode `AddComponent` leaves the static claim at zero.
Rejected Alternatives: Claiming then unregistering, relying on `SubsystemRegistration` to clean up later, or converting the runtime into `[ExecuteAlways]`. Those preserve the wrong lifecycle boundary and create editor-state side effects for a pure runtime weather propagation owner.
Scalability potential: Low/middle/high/ultra all keep one storm propagation owner and one route. Cheap devices avoid editor-created stale services; high/ultra can safely run richer storm attenuation only from the valid runtime owner.
Hardware Impact: Runtime frame cost unchanged. Authoring/runtime contamination risk removed; avoids cold DataVault/listener setup from editor component activation and prevents duplicate-owner failure modes.

## Decision 013 - Atmosphere Editor Preview Was Still Play-Mode Gated

Problem: `HectonAtmosphereManager.OnEnable()`, `EditorTick()`, and `OnValidate()` rejected `!Application.isPlaying`. That made the edit-mode branch that marks preview dirty and registers `EditorApplication.update` unreachable, and made authoring clamps/RenderSettings sun resolution unreachable. Scene View sun, sunrise, sunset, and sky-cycle preview could only be trusted after entering play mode.
Solution: Changed editor gates to reject compiling, and changed `OnValidate()` to reject play mode instead of edit mode. Runtime registration still stays inside `Application.isPlaying`. Added edit-mode tests for `OnEnable` preview dirtying and `OnValidate` cycle-duration clamp/preview invalidation.
Rejected Alternatives: Leaving direct `SyncEditorPreviewFromSunTransform()` as the only testable path, adding a separate editor window, or forcing designers to enter play mode to validate sky composition. Those hide broken authored atmosphere state and waste runtime iteration time.
Scalability potential: Low tier benefits from validated authored sky/weather state before runtime. Middle/high/ultra get reliable cinematic sunrise/sunset/eclipsed-sky composition without adding runtime simulation or changing authority routes.
Hardware Impact: Runtime hot path unchanged. Editor-only work prevents bad atmosphere authoring from shipping; estimated runtime savings are indirect, but it removes play-mode-only correction loops and preview guesswork.

## Decision 014 - Surface Thunder Authoring Was Ignored

Problem: `SurfaceWeatherProfile` exposed `lightningFlashDuration`, `thunderDelayMin`, `thunderDelayMax`, and `thunderPropagationDistanceScale`, and those values were copied into `SurfaceWeatherMathState`, but both `SurfaceWeatherMathJob.TriggerLightning()` and the direct `HectonSurfaceWeatherDirector.TriggerLightning()` fallback ignored them. Runtime used a hard-coded 0.1 s flash and raw distance / air sound speed, so authored storm profiles could not buy slow cinematic thunder roll-in or shorter/longer flashes.
Solution: Added shared scalar `SurfaceThunderMath` and used it in both runtime branches. Lightning flash duration is now authored and clamped to the profile range. Thunder delay now scales strike distance with `thunderPropagationDistanceScale` and clamps to `thunderDelayMin/Max`. Added editor regression probes through reflection so runtime public API is not widened for tests.
Rejected Alternatives: Adding physical lightning/thunder propagation, leaving raw sound-speed timing because it is "realistic", or duplicating divergent math in job and non-job paths. Real physics is unnecessary for player belief; raw sound speed made the authored profile fields dead; duplicate formulas would drift.
Scalability potential: Low tier keeps the cheapest deterministic scalar fake: one multiply, divide, clamp. Middle tier gets authored storm pacing. High and Ultra can exaggerate cinematic thunder timing, flash length, rain density, and lightning width through profiles without adding simulation truth or changing DTO layout.
Hardware Impact: Runtime cost is effectively unchanged; the helper replaces one divide with multiply/divide/clamp. Estimated delta on i3/MX350 is <0.1 us per lightning event and 0 B GC by static inspection. Visual/audio value increases because authored weather profiles now control perceived storm scale.

## Decision 015 - Weather Event Queue Could Allocate On First Hot Publish

Problem: `WeatherEvents.TryRaiseSnapshotUpdated()` and `TryRaiseLightning()` call `EnsureInitialized()`. If no listener had registered first, the first weather publish from `GlobalWeatherDirector.Tick()` or a surface-lightning event could create persistent `NativeQueue` lanes from a hot producer path.
Solution: Added explicit `WeatherEvents.PrepareCold()` and called it from `GlobalWeatherDirector.InitializeRuntimeStateIfNeeded()` and `HectonSurfaceWeatherDirector.InitializeRuntimeStateIfNeeded()`, both reached from Awake/OnEnable before tick publishing. Added an editor test that resets static state and proves both native queues are created by the cold warmup call.
Rejected Alternatives: Relying on `HectonCelestialEngine` or GI relay listener registration to warm the lane, moving queue allocation into dispatcher flush, or replacing the lane with managed events. Listener order is not an ownership proof; dispatcher allocation would still be hot; managed events violate zero-GC broadcast policy.
Scalability potential: Low tier avoids a first-storm/first-weather snapshot allocation hitch. Middle/high/ultra keep the same event capacity and can spend weather budget on richer fog/rain/lightning presentation without introducing a new authority route.
Hardware Impact: Prevents two persistent NativeQueue allocations from landing on a gameplay frame. Estimated i3/MX350 first-event hitch avoided: tens to hundreds of microseconds depending on allocator state; steady-state runtime cost unchanged and 0 B GC by static inspection.

## Decision 016 - Global Weather Runtime Owner Contaminated Edit Mode

Problem: `GlobalWeatherDirector` is a runtime weather owner, not an editor preview component, but `Awake()` and `OnEnable()` initialized runtime state, registered `GlobalRegistry.Weather`, published shader globals, raised weather events, and could allocate the runtime noir fog LUT when the component was added/enabled outside play mode.
Solution: Added play-mode gates to `Awake()` and `OnEnable()`. Added `ClearEditorRuntimeResidue()` for edit-mode disable/destroy to unregister only this stale weather owner, clear flags, and release any existing LUT resource. Added an editor regression test proving edit-mode `AddComponent<GlobalWeatherDirector>()` leaves `GlobalRegistry.Weather` null, `IsInitialized` false, and `_noirFogLutTexture` null.
Rejected Alternatives: Converting the component to `[ExecuteAlways]`, keeping editor registration for convenience, or adding a separate editor weather preview route. Those options blur runtime authority and create duplicate weather-service ownership risk.
Scalability potential: Low tier avoids editor-created weather snapshots, shader state, and LUT resources entering runtime assumptions. Middle/high/ultra keep a single runtime weather authority and can spend budget on richer fog/rain/god-ray presentation only after the valid owner phase starts.
Hardware Impact: Runtime hot path unchanged. Edit-mode side effects removed; avoids one cold `Texture2D` plus `Color[]` LUT allocation and weather event lane work from scene authoring. Estimated i3/MX350 runtime stability gain is preventing duplicate-owner and first-frame contamination, not a steady-state CPU reduction.

## Decision 017 - Surface Weather Runtime Started From Edit Mode

Problem: `HectonSurfaceWeatherDirector` performed runtime work from edit-mode lifecycle: DataVault cache, player/context service reads, service/origin listener registration, dependency refresh, weather-event warmup, runtime buffer acquisition, and a cold weather math seed. The component has edit authoring through `Reset`/`OnValidate`, but its runtime owner route must not start in edit mode.
Solution: Kept fallback profile/editor default authoring in `Awake()`, then returned outside play mode before runtime setup. Added play-mode gate to `OnEnable()`. Added edit-mode cleanup for stale self-owned residue on disable/destroy. Added an editor regression test proving no `GlobalRegistry.SurfaceWeather` claim, no `HectonFloatingOrigin` listener registration, no runtime state init, and no DataVault cache from edit-mode `AddComponent`.
Rejected Alternatives: Leaving `HectonFloatingOrigin.RegisterListener(this)` active in edit mode, converting the component to `[ExecuteAlways]`, or treating DataVault buffer acquisition as harmless authoring setup. Those choices create a second authority route before gameplay exists.
Scalability potential: Low tier avoids scene-authoring cold jobs and DataVault pressure. Middle/high/ultra keep the surface weather budget available for actual runtime rain, lightning, ocean coupling, and sky luminance instead of editor contamination.
Hardware Impact: Runtime hot path unchanged. Edit-mode startup no longer performs DataVault handle work or cold weather math. Estimated avoided authoring hitch on i3/MX350 is tens to hundreds of microseconds plus removal of stale origin listener/service state risk.

## Decision 018 - Atmosphere VFX/Ocean Runtime Claimed Runtime Work In Edit Mode

Problem: `SurfaceWeatherVfxRig.OnEnable()` registered a floating-origin listener outside play mode, and `ShinobuOceanSurfaceAtmosphereRuntime.OnEnable()` executed runtime startup from edit mode: player/DataVault registry reads, vault hydration, GPU buffer creation, wave upload, ocean provider registration through `OceanKinematicsRuntimeService`, dispatcher registration, and shader-global publication. The ocean runtime also defaulted `_readbackDispatchEnabled` to true before any runtime owner phase.
Solution: Added play-mode gates before runtime listener/provider/DataVault/GPU/dispatcher work. Set ocean readback dispatch to default-off and enable it only from runtime `OnEnable()`. Added editor tests proving the VFX rig does not register with `HectonFloatingOrigin` and the ocean atmosphere runtime does not create `OceanKinematicsRuntimeService`, register lanes, hydrate vault buffers, arm readback dispatch, or allocate wave/readback `GraphicsBuffer`s in edit mode.
Rejected Alternatives: Converting these components into editor previews, leaving listener/provider claims to be cleaned by disable/destroy, or touching core `OceanKinematicsRuntimeService` ownership from a sky/weather domain task. Editor preview would add simulation cost without authored sky value; cleanup-after-claim still pollutes global state; core service surgery exceeds 13ATMA ownership.
Scalability potential: Low tier avoids scene-authoring GPU/DataVault pressure and stale global service state. Middle tier keeps ocean/weather coupling deterministic. High and Ultra can spend runtime budget on richer wave, rain, lightning, and sky reflection presentation only after the valid play-mode owner starts.
Hardware Impact: Runtime steady-state path unchanged except readback dispatch is no longer armed before runtime. Avoided edit-mode work includes two wave upload `GraphicsBuffer`s, six readback/query/result buffers, DataVault handle hydration, ocean provider service creation risk, dispatcher lane registration attempts, and shader global publication. Estimated avoided authoring hitch on i3/MX350: hundreds of microseconds to several milliseconds depending on graphics driver and DataVault state; runtime GC remains 0 B by static inspection.

## Decision 019 - Surface Weather Job Teardown Masked Pending Work

Problem: `HectonSurfaceWeatherDirector.DisposeWeatherMathBuffers()` used non-forced `DispatcherJobSwap.TryComplete`. If `_weatherJobScheduled` was true and the job was not complete, the method returned before releasing the DataVault output handle. `ClearEditorRuntimeResidue()` then set `_weatherJobScheduled` and `_weatherJobPrimed` false anyway, masking a pending job and stale output handle state. Runtime `OnDisable()` also skipped weather job disposal entirely.
Solution: Made disposal explicit with `DisposeWeatherMathBuffers(bool forceCompletePendingJob)`. Teardown paths (`OnDisable`, `OnDestroy`, editor stale cleanup) pass `true`; the normal frame path still uses non-forced `TryCompleteWeatherMathJob()`. Removed the editor cleanup's manual scheduled/primed flag wipe so disposal owns those facts. Added an editor source-contract test for forced teardown call sites and the old bad pattern.
Rejected Alternatives: Forcing job completion in the normal weather tick, keeping parameterless disposal, or clearing flags after a failed non-forced completion. Forced normal tick would violate dispatcher swap discipline; parameterless disposal hides lifecycle intent; flag clearing after failure creates a false proof artifact.
Scalability potential: Low tier avoids stale weather output handles and hidden job lifetime bugs when weather owners are disabled under load. Middle/high/ultra keep the same scheduled weather math cadence and can spend quality-controlled rain/lightning/ocean coupling budget without changing weather truth ownership.
Hardware Impact: Steady-state runtime frame cost unchanged. Teardown may block once to finish a pending weather job, which is correct owner-phase cleanup. Estimated avoided i3/MX350 failure cost is not a frame micro-optimization; it prevents stale job/output state that could otherwise force later recovery work or undefined readback. Normal frame path remains 0 B GC by static inspection.

## Decision 020 - Ocean Surface Runtime Bypassed Continuous Quality

Problem: `ShinobuOceanSurfaceAtmosphereRuntime.Tick()` sampled `_globalQualityWeight`, but several downstream routes still used `OceanSurfaceAtmosphereConstants.AuthoritativeQualityWeight` or a local `authorityQuality`: wave evaluation time quantization, GPU readback sample budget, readback active wave count and AUP phase bases, readback shader quality upload, telemetry active wave count, and telemetry state hash. Low-tier devices therefore paid max-quality cadence/readback work while the shader path only partially scaled.
Solution: Routed `_globalQualityWeight` through wave evaluation cadence, readback sample budget, readback LOD/phase/quality uniforms, telemetry active wave count, and state hash. Added an editor source-contract regression to reject the old authoritative-quality bypasses. Preserved unrelated pre-existing wave/readback safety edits in the same dirty runtime file.
Rejected Alternatives: Keeping max-quality readback for "authority", adding a low/high boolean quality switch, or changing DTO layout. This is presentation sampling, not gameplay truth; binary switches violate project quality law; DTO layout churn would be unnecessary cross-system risk.
Scalability potential: Low/MX350 now uses coarser time cadence and fewer readback samples while retaining believable surface motion. Middle scales smoothly. High and Ultra keep dense wave/readback work and spend saved low-tier budget on stronger sky reflection, foam, rain disturbance, and ocean-atmosphere coupling without changing authority ownership.
Hardware Impact: Runtime steady-state cost drops on low quality by reducing GPU readback sample budget from max toward the 4-sample floor and by snapping wave evaluation to lower cadence. Estimated i3/MX350 gain depends on readback traffic: tens to hundreds of microseconds on readback frames plus lower GPU/driver pressure. GC remains 0 B by static inspection.

## Decision 021 - Celestial Snapshot Clear Published From Edit Mode

Problem: `HectonCelestialEngine.ClearCelestialRuntimeSnapshot()` published an empty `CelestialRuntimeSnapshot` through `GlobalRegistry.PublishCelestialRuntimeSnapshot()` unconditionally. Because the method is reached by edit-mode disable/destroy cleanup, scene authoring could erase the last runtime celestial snapshot sequence outside play mode, violating runtime authority ownership.
Solution: Keep shader/global visual cleanup unchanged, but gate the global snapshot publication behind `Application.isPlaying`. Added an editor regression that seeds the private global celestial snapshot state, invokes the private clear route in edit mode, and proves the sequence/snapshot remain intact.
Rejected Alternatives: Removing clear entirely, adding a separate editor snapshot owner, or accepting edit-mode publication as harmless. Removing clear would leave runtime residue; an editor owner creates a second fact route; edit-mode global publish violates one-owner runtime truth.
Scalability potential: Low/MX350 keeps runtime sky consumers from losing cached celestial state because of editor lifecycle noise. Middle/high/ultra preserve stable sky, eclipse, orbit, and lighting consumers without changing DTO layout or quality routing.
Hardware Impact: Runtime hot path unchanged. Edit-mode global mutation removed. Estimated i3/MX350 runtime savings are indirect: avoids false first-frame celestial rebind/recovery work and removes a non-play global state hazard; 0 B GC by static inspection.

## Decision 022 - Sky Follow Runtime Camera Route Still Scene-Scanned

Problem: `SkySystemFollowCamera.ResolveTargetCamera()` still fell back to `Camera.GetAllCameras()` and a `CompareTag("MainCamera")` scan at runtime. That violates no scene search/hot polling doctrine and can allocate/fill a camera buffer every follow tick when explicit camera and cached context are missing.
Solution: Removed the runtime camera scan path and its buffer. Runtime now consumes only an explicit `runtimeCamera`, a cached resolved camera, or the cached `IPlayerRuntimeContext.PlayerCamera`; sea-level owner discovery also uses cached `IPlayerRuntimeContext.PlayerMovement` before explicit cold camera fallback. Edit-mode Scene View fallback remains editor-only.
Rejected Alternatives: Using `Camera.main`, keeping `Camera.GetAllCameras()` as a rare fallback, or introducing a new direct dependency on a not-yet-existing camera service. Unity camera discovery hides scene search and tag order; the project already has `IPlayerRuntimeContext` as the correct decoupled route.
Scalability potential: Low tier avoids camera scene scans in sky follow. Middle/high/ultra keep deterministic sky-relative placement and spend saved frame budget on more clouds, shafts, horizon detail, and orbital silhouettes without changing authority.
Hardware Impact: Removes worst-case O(camera count) scan/fill from runtime sky follow. Estimated i3/MX350 gain in multi-camera scenes: 5-40 us on bad fallback frames, plus lower variance and no runtime camera-buffer dependency; GC remains 0 B by static inspection.

## Decision 023 - Ocean Read Accessors Mutated GPU/Job State

Problem: `ShinobuOceanSurfaceAtmosphereRuntime` public read paths queued wave-height readbacks and `TryGetSurfaceWeatherState()` tried to complete the wave-parameter job. Read accessors therefore changed runtime queues or job fences, violating pure read doctrine and adding hidden GPU/CPU synchronization risk to buoyancy/sky/ocean consumers.
Solution: Removed read-path `QueueWaveHeightSample` and `TryCompleteWaveParameterKernel()` calls. Added `TryEvaluateWaveKinematicsSnapshot()`: it reads an already-completed sample first, otherwise evaluates the current `WaveParametersDTO` snapshot on CPU with `HectonOceanSurfaceMath.EvaluateWavesDetailed`, and fails closed while the wave-parameter job is scheduled. `GetWaterHeight`, `GetWaveNormal`, and `TrySampleWaveKinematics` now use that route.
Rejected Alternatives: Calling `.Complete()` from reads, leaving async readback queues in getters, returning the last sea level as if wave detail existed, or adding a physical ocean solver. Hidden completion breaks frame determinism; readback enqueue from getters mutates state; invented data is worse than a false return; physical ocean simulation violates cinematic cheat and frame budget.
Scalability potential: Low/MX350 uses completed samples or cheap CPU snapshot math only when data-local and safe. Middle keeps stable wave queries without GPU queue churn. High and Ultra can keep richer wave/readback cadence through owner phases while read consumers remain predictable.
Hardware Impact: Removes per-query GPU readback enqueue pressure and read-side job completion risk. Estimated i3/MX350 gain in dense buoyancy/query scenes: 5-30 us on frames that previously queued extra samples, plus avoidance of millisecond-scale stalls if a read tried to drain a pending wave job. Runtime GC remains 0 B by static inspection.

## Decision 024 - Weather Bridge Hot-Polled Celestial Snapshot

Problem: `GlobalWeatherDirector.PublishAtmosphericBridgeShaderState()` ran from the weather late-frame path and read `GlobalRegistry.CelestialRuntimeSnapshot` directly. That made atmospheric god rays, moon phase, radiation storm, and biolum sky/ocean coupling depend on a hot global snapshot poll instead of the cached celestial owner route.
Solution: Cached `HectonCelestialEngine` through `ResolveDependencies()` and `GlobalRegistryServiceSlot.CelestialEngineRuntime` hot-swap updates. The atmospheric bridge now calls `ReadCachedCelestialRuntimeSnapshot()` and consumes the owner `RuntimeSnapshot` property. Added a source-contract test that rejects the global snapshot poll from the bridge region.
Rejected Alternatives: `FindObjectOfType`, `GlobalRegistry.TryGetLatestCreated`, keeping the direct snapshot poll because it is "just shader state", or adding a new weather-celestial singleton. Scene search and latest-created routes violate ownership doctrine; shader writes still run in frame hot paths; a singleton creates a second authority route.
Scalability potential: Low tier avoids global snapshot polling variance in weather shader publication. Middle keeps deterministic celestial/weather coupling. High and Ultra can spend visual budget on stronger moonlit clouds, god rays, radiation storm tint, and biolum response without changing celestial truth ownership.
Hardware Impact: Removes one hot global snapshot access from every atmospheric bridge publish and keeps dependency updates on cold/hot-swap paths. Estimated i3/MX350 gain is small but deterministic: 1-3 us/frame variance reduction in weather bridge frames, with more important reduction of ownership risk. GC remains 0 B by static inspection.

## Decision 025 - Atmosphere Procedural Biome Route Used Lazy Active-Instance Fallback

Problem: `HectonAtmosphereManager.RefreshProceduralBiomeInfluenceSnapshotIfNeeded()` ran from the atmosphere `SlowTick` timeline and lazily called `WorldRuntimeReferenceUtility.TryResolveWorldProceduralFieldSampler()` when `_proceduralFieldSampler` was null. The biome matrix route also used `WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector()`. Those helpers fall through to `ActiveRuntimeInstance`, creating a hot fallback route outside the documented `GlobalRegistry` owner path.
Solution: Cache `GlobalRegistry.BiomeMatrix` and `GlobalRegistry.ProceduralFieldSampler` from `CacheRegistryRuntimeReferences()`. Add `GlobalRegistryServiceSlot.BiomeMatrixRuntime` and `GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime` hot-swap handling. Remove the slow-tick lazy sampler resolve. When sampler authority disappears, clear procedural biome influence state and hysteresis instead of holding stale fog/color blending.
Rejected Alternatives: Keeping 0.35 s active-instance fallback because it is not every frame, using scene search, using `GlobalRegistry.TryGetLatestCreated`, or moving ownership into the atmosphere manager. Cadence does not make a hot fallback correct; scene/latest-created routes are forbidden; world owns procedural field sampling, atmosphere only consumes cached presentation influence.
Scalability potential: Low/MX350 avoids lazy dependency churn in the sky/fog timeline and fails closed to base atmosphere profiles when procedural field sampling is absent. Middle keeps stable biome fog blending through hot-swap updates. High and Ultra can spend visual budget on richer biome haze, sky tint, and underwater surface composition without changing world/biome authority.
Hardware Impact: Removes active-instance fallback from atmosphere biome refresh. Estimated i3/MX350 gain is small per refresh, 2-8 us in scenes where the sampler was temporarily null, but the larger gain is deterministic ownership and no stale procedural fog influence after sampler teardown. First-20 route blocker removed: early-world surface/underwater visibility no longer depends on lazy world singleton recovery during the atmosphere tick.
