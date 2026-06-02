# LOG_13pro

## 2026-05-27 - Prologue Orbital Flight Audit / Patch

What was wrong:
- `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="13pro">`; direct user assignment is the only scoped authority.
- Celestial authority is already owned by `HectonSeismicTideDirector` / SHINOBU_345 Vault route. Prologue must not create a second Aegir/Hecton orbital truth owner.
- `AwaitableDropSequenceDirector` started by waiting for atmospheric reentry before the orbital silence beat, so the cinematic sequence could begin only after heat/plasma existed.
- `OrbitalRelativityDirector` changed Math LOD immediately at thresholds, causing possible renderer toggle thrash.
- `OrbitalRelativityDirector` published `SignalBus<HapticRequest>` and also directly enqueued `ToolHapticsRuntime` haptics, duplicating the hot feedback route.
- `_PROLOGUE_CONTENT/Scripts/PlanetRotation.cs` used `Update()` and `Transform.Rotate()` as a prototype celestial owner.

What was done:
- `AwaitableDropSequenceDirector` now starts with a non-pausing orbital silence beat before waiting for atmospheric reentry. It does not lock look/flight input during the orbital sightseeing window.
- `OrbitalRelativityDirector` now resolves Math LOD through a 3-frame hysteresis gate while still consuming continuous `HomeostasisBrain.GlobalQualityWeight`.
- Locked-capsule leading-edge math now uses cached `_capsuleLockedRotation` when capsule lock is active, avoiding hot transform direction reads on the normal prologue path.
- Reentry haptics now publish only the canonical `SignalBus<HapticRequest>` route consumed by `InputDispatcher`.
- `PlanetRotation` is disabled on enable so prototype transform rotation cannot compete with the celestial/prologue shader authority route.

Cinematic Cheats used:
- Relativity fake remains the correct prologue approach: capsule stays near origin, universe/planet presentation moves around it.
- Orbital view and descent stay shader/presentation driven rather than real Kepler, rigidbody orbit, physical light rotation, or atmospheric particle simulation.
- Low: stable impostor/mesh LOD and no duplicate haptic enqueue.
- Middle: mesh presentation with throttled feedback.
- High: richer renderer/VFX modes without LOD flicker.
- Ultra: spend continuous quality weight on shader/VFX overkill, not duplicate gameplay truth.

Exact Microseconds saved:
- Direct haptic enqueue removed from reentry path: estimated 3-15 us every `hapticIntervalSeconds` on low-end CPU, plus no duplicate queue pressure.
- Prototype `Update()` + `Transform.Rotate()` removed where attached: estimated 1-5 us per frame per attached component and zero transform authority conflict.
- Cached capsule orientation path: estimated sub-1 us per frame, but removes a hot TransformDirection dependency.
- Math LOD hysteresis: no steady-state cost beyond two fields and integer compares; prevents renderer toggle spikes at threshold crossings.

Verification:
- Re-read changed code after patch.
- Scoped `rg` found no remaining `Transform.Rotate`, `Update()`, `ToolHapticsRuntime`, `Resources.Load`, object find, or `new GameObject` hits in the prologue scope checked.
- `git diff --check` reported only CRLF normalization warnings on touched C# files.
- Compile was not launched: CPU load gate was 76 percent then 72 percent, above the project ban for `dotnet build`; no `dotnet` or `csc` process was running.

Remaining proof debt:
- Unity import/Console clean.
- GCMonitor 0 B/frame in prologue orbit/reentry path.
- Profiler proof that prologue presentation systems stay under 0.1 ms on i3/MX350 class hardware.

## 2026-05-27 - Second Pass / Scene Wiring And Duration

What was wrong:
- `01_ORBIT.unity` was still a prototype scene: Main Camera, Directional Light, GasGiant_Aegir prefab, Hecton8 surface/cloud meshes, and legacy `PlanetRotation`; no scene/prefab reference existed for orbital, sequence, bridge, or VFX runtime owners.
- `OrbitalRelativityDirector` default pacing was seconds-scale (`12000 m / 320 m/s`), not the requested 10-15 minute orbital approach and descent.
- Prologue VFX sampled quality only on enable and could publish idle VFX state plus Vault telemetry every LateFrame before any reentry signal.
- Prologue audio/VFX late-frame registration was not fully play-mode/dispatcher gated.
- `AdvancedAcousticsSmokeTester` still extracted `RefreshQualityPolicyCold()` after the runtime quality refresh moved to `RefreshQualityPolicy()`.

What was done:
- Added `PrologueOrbitSceneBootstrap` and wired it to `01_ORBIT` Main Camera. It only runs in exact scene `01_ORBIT`, creates a cold scene-local runtime root, adds `OrbitalRelativityDirector`, `AwaitableDropSequenceDirector`, `PrologueSequenceRegistryBridge`, and `OrbitalDropReentryVfxController`, then binds Main Camera/Hecton/Aegir/cloud objects before activation.
- Added explicit scene-binding APIs to `OrbitalRelativityDirector` and `OrbitalDropReentryVfxController`.
- Updated `Hecton8.Prologue.Space.asmdef` to reference the prologue Narrative and VFX assemblies required by the scene bootstrap.
- Changed orbital defaults to `260000 m` start, `300 m/s` passive approach, `70000 m` reentry envelope, `5000 m` whiteout envelope, and added a continuous-quality triangle-wave diamond orbit presentation fake.
- VFX now refreshes quality during active LateFrame, skips idle signal/telemetry output, and respects DataVault compaction/allocation guards.
- Audio/VFX registration is play-mode gated; audio quality refresh test now targets the live method.

Cinematic Cheats used:
- Diamond-orbit window motion uses two triangle waves and scalar lerps, not orbital physics.
- Capsule remains camera-local; Aegir/Hecton visual travel is presentation-space.
- Reentry heat, cloud whiteout, audio pressure, haptics, splash, and hydration handoff remain typed signals and shader/audio state, not particle/fluid simulation.
- Low: smaller orbit offset, impostor continuity, idle VFX lane silent.
- Middle: stable mesh/cloud handoff and continuous audio filter sweep.
- High: larger orbital parallax and richer VFX response without authority changes.
- Ultra: more visible orbital sweep and shader/audio overkill from the same `GlobalQualityWeight` scalar.

Exact Microseconds saved:
- Idle VFX state publish avoided: estimated 2-8 us per enabled idle controller per frame.
- Idle VFX Vault write-lock/write/unlock avoided: estimated 4-20 us per enabled idle controller per frame.
- Runtime scene wiring cost is cold only: one root traversal plus four component adds when `01_ORBIT` loads; no hot scene search.
- Triangle-wave orbital presentation fake: estimated sub-10 us per presentation apply on i3/MX350, replacing any real orbit solver temptation.

Verification:
- `rg` confirmed `01_ORBIT.unity` now carries `PrologueOrbitSceneBootstrap`.
- Scoped scans confirm no direct `ToolHapticsRuntime` prologue path remains.
- `git diff --check` passes on touched files; only CRLF normalization warnings remain on C# files.
- Compile was not launched: CPU load was 84-100 percent and multiple `dotnet` processes were already running, so the AGENTS build gate forbids another build.

Remaining proof debt:
- Unity import/Console after asmdef reference change.
- Play Mode proof that `01_ORBIT` bootstrap composes the runtime root and sequence starts after bootstrap services are present.
- Profiler/GC proof for the 10-15 minute prologue path.
- Product-route decision remains separate: AGENTS still says production flow is `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`; this pass did not insert `01_ORBIT` into BuildSettings or main menu handoff.

## 2026-05-27 - Third Pass / Orbit Window Visibility Contract

What was wrong:
- `01_ORBIT` Main Camera had identity rotation, looking down `+Z`, while `OrbitalRelativityDirector` places Hecton and Aegir on the `-Y` presentation axis.
- The scene camera far clip was `100000`, but the prologue now starts at `260000 m`; Hecton/Aegir could be clipped before the player sees the intended orbital approach.
- Aegir needed to remain a separate gas giant backdrop. Treating it like the Hecton impostor would make Aegir and Hecton mutually exclusive under Math LOD.

What was done:
- `PrologueOrbitSceneBootstrap` now cold-configures the orbit window camera: world position at the presentation origin, rotation `Quaternion.LookRotation(Vector3.down, Vector3.forward)`, far clip at least `360000 m`.
- `OrbitalRelativityDirector` now exposes a dedicated Aegir backdrop binding, scales it separately, positions it as a side/depth backdrop, and keeps it enabled independently from Hecton mesh/impostor LOD.
- `PrologueOrbitSceneBootstrap` binds Aegir through `ConfigureAegirBackdrop(...)` and passes `null` for Hecton impostor when no real impostor exists.

Cinematic Cheats used:
- Camera-space orbit window axis is fixed cold; no runtime camera search or real celestial transform authority.
- Aegir backdrop is presentation-space composition, not a second celestial truth owner.
- Low: visible Hecton mesh with cheap backdrop placement.
- Middle: stable Hecton/cloud handoff with Aegir still present.
- High: stronger orbital side parallax through continuous quality.
- Ultra: larger visual sweep/material overkill remains possible without changing DTOs or route authority.

Exact Microseconds saved:
- Runtime cost of the camera fix: 0 recurring us. All writes are cold scene composition in bootstrap.
- Aegir/Hecton separation avoids renderer LOD churn from a false impostor relationship; estimated spike prevention is scene-dependent, but steady-state extra cost is only one renderer enable check during presentation apply.

Verification:
- `git diff --check` passes on scoped touched files; only CRLF normalization warnings remain.
- `rg` confirms `01_ORBIT` script GUID `444968fbfc5a4eb291617d5798a39dce`, camera configuration method, Aegir backdrop API, and scene-local root move.
- Compile was not launched: CPU load was `100%` and `dotnet` PID `21804` was already running.

Remaining proof debt:
- Unity import/Console after the new scene bootstrap and asmdef references.
- Play Mode proof that `01_ORBIT` camera sees Hecton/Aegir and that the sequence starts.
- Profiler/GC/frame capture proof for low-end and high-end quality weights.
- Production route still unchanged: this does not insert `01_ORBIT` into BuildSettings or main-menu flow.

## 2026-05-27 - Fourth Pass / Production Prologue Route

What was wrong:
- New Game still loaded `02_HECTON_WORLD` directly from `MainMenuController`, so the orbit/descent prologue remained unreachable in the production player path.
- `01_ORBIT.unity` was absent from `EditorBuildSettings.asset`.
- No owner converted the typed prologue ocean handoff into the guarded world scene transition.
- The authored `01_ORBIT` camera still stored obsolete `+Z` orientation and `100000` far clip, even though runtime bootstrap corrected it.

What was done:
- Added `newGameTargetSceneName = "01_ORBIT"` to `MainMenuController`; load-game saves still use `targetSceneName = "02_HECTON_WORLD"`.
- Updated `01_MAIN_MENU.unity` serialized controller data with `newGameTargetSceneName: 01_ORBIT`.
- Inserted `Assets/_Project/Scenes/01_ORBIT.unity` into `ProjectSettings/EditorBuildSettings.asset` between main menu and world.
- Added `PrologueWorldHandoffSceneLoader`; it registers through the dispatcher late-frame lane, consumes `SignalBus<PrologueCompleteSignal>`, waits two whiteout frames, then calls `ISceneService.LoadScene("02_HECTON_WORLD")`.
- `PrologueOrbitSceneBootstrap` now adds the handoff loader to the cold runtime root.
- Stored the corrected orbit-window camera orientation and far clip directly in `01_ORBIT.unity`.

Cinematic Cheats used:
- Handoff is typed signal choreography, not a raw scene load hidden in VFX or input code.
- New Game route buys 10-15 minutes of authored orbit/reentry presentation without changing save-load semantics.
- Low: cheap deterministic route and whiteout buffer.
- Middle: normal world residency gate remains owned by `SceneRuntimeService`.
- High: more prologue visual density still uses the same handoff.
- Ultra: visual overkill can scale in orbit/reentry without changing route authority.

Exact Microseconds saved:
- Avoided raw `SceneManager` path and hot registry polling: estimated 0 recurring us outside `01_ORBIT`.
- Handoff loader active cost during prologue: estimated 1-5 us/frame for one signal snapshot and branch path on i3/MX350.
- After load request/unload: 0 recurring us.

Verification:
- `git diff --check` passes on route/handoff files; only CRLF normalization warning remains in `MainMenuController.cs`.
- `rg` confirms `newGameTargetSceneName`, BuildSettings `01_ORBIT`, `PrologueWorldHandoffSceneLoader`, and removal of direct `LoadScene(targetSceneName)` call.
- Compile gate later opened at 46 percent CPU with no `dotnet`/`csc`, so `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` was attempted.
- Compile failed before domain code on generated Unity project-reference cycles: `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj` reported `MSB4006` circular dependency in `ResolveProjectReferences`.
- A second compile attempt was not launched because CPU rose to 86 percent and a `dotnet` process was active.

Remaining proof debt:
- Unity import/Console after adding the new handoff script and BuildSettings scene.
- Play Mode proof: New Game -> `01_ORBIT`, visible Aegir/Hecton orbit, prologue completion -> `02_HECTON_WORLD`.
- Profiler/GC proof for handoff loader and active orbit path.
- External compile wall: generated Unity project-reference cycle outside prologue domain.

## 2026-05-27 - Fifth Pass / Handoff Truth And Autonomous Descent

What was wrong:
- Orbital whiteout was still too authoritative: `OrbitalRelativityDirector` emitted `PhaseOceanHandoff`, so the new world loader could load `02_HECTON_WORLD` before manual release, impact sync, hydration wait, and sequence-owned splashdown.
- The passive 10-15 minute orbit path moved at `300 m/s`, but `AwaitableDropSequenceDirector` requires `Mach10` before manual release. Without player thrust, the prologue could stall in `ReentryBurn` forever.
- VFX/audio accepted whiteout-only complete signals too widely. Any force-whiteout could alter prologue presentation state instead of only the orbital owner doing that.

What was done:
- `OrbitalRelativityDirector` now publishes `PrologueCompleteSignal.PhaseWhiteout` only. It no longer emits the final splashdown fluid impulse.
- `PrologueSequenceRegistryBridge` accepts the orbital whiteout only as a standalone `01_ORBIT` manual fallback, using a cached scene-mode bool set in `OnEnable`; no `SceneManager.GetActiveScene()` is used in the consume/read path.
- `PrologueWorldHandoffSceneLoader` now accepts only finite, non-zero `SequenceDirector` `PhaseOceanHandoff`.
- Added deterministic scripted reentry burn: `scriptedReentryBurnAccelerationMetersPerSecondSq=260`, applied by `1 - distance / reentryStartDistanceMeters`.
- VFX/audio whiteout-only complete handling now requires `OrbitalRelativityDirector` source, non-zero sequence, `PhaseWhiteout`, and force-whiteout flag.

Cinematic Cheats used:
- Scripted deorbit burn is a scalar rail, not real orbital physics.
- Orbital whiteout is presentation/manual-gate only; ocean splash remains owned by sequence/world fluid systems.
- Low: cheap scalar burn, source-filtered signals, no duplicate splash impulse.
- Middle: stable manual-release and hydration order.
- High: fast visual descent can still carry richer VFX/audio through continuous quality.
- Ultra: overkill particles/audio/debris may attach to sequence handoff without changing truth routes.

Exact Microseconds saved:
- Removed premature orbital fluid impulse route: avoids one false splashdown event and any downstream fluid/debris work it could trigger.
- Scripted burn hot cost: one saturate/rcp/scalar term per orbital tick, estimated under 3 us on i3/MX350.
- Source hardening cost: branch/hash checks only, estimated under 1 us/frame.

Verification:
- PowerShell 60 Hz model with defaults: `Mach10=684.7s`, whiteout range `687.1s`, whiteout emit `688.3s`; total `11.47 min`.
- `git diff --check` passes on all touched domain files; only CRLF normalization warnings remain.
- Scoped `rg` confirms no direct `ToolHapticsRuntime`, no early `FluidImpulseSignal`/`SplashdownFluidImpulse`, and no `SceneManager.GetActiveScene` in `PrologueSequenceRegistryBridge`.
- `CURRENT_BATCH.md` still has no `<AGENT_PROMPT id="13pro">`; direct chat assignment remains authoritative with task count `0`.
- Compile was attempted when CPU was `25.5%` and `dotnet/csc=0`. It failed outside prologue at `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs`: missing `Mono.Data` and `SqliteDataReader`. Broad MapMagic duplicate-type warnings are also present.
- A second compile probe was not launched because CPU later measured `90.2%`.

Remaining proof debt:
- Unity import/Console after external Candice SQLite reference wall is fixed.
- Play Mode proof: New Game -> `01_ORBIT`, orbital scene runtime root, visible Aegir/Hecton, autonomous burn reaches manual release, sequence handoff loads `02_HECTON_WORLD`.
- Profiler/GC capture across low, middle, high, and ultra quality weights.

## 2026-05-27 - Sixth Pass / Route Evidence Hygiene

What was wrong:
- Active tests/docs lagged behind the production route change: new-game route is no longer direct menu-to-world.
- Historical `LOG_13pro.md` entries still mention the old route as a prior-pass constraint. They are retained as history, but superseded by the fourth/fifth/sixth pass records.
- Whole-repo `git diff --check` is polluted by trailing whitespace already present in `Docs/Tasks/CURRENT_BATCH.md`, outside the `13pro` prompt.

What was done:
- Updated `PersistenceUxSmokeTester` expectations to the new start-scene resolver and `sceneService.LoadScene(sceneName)` call.
- Updated active first-20-minutes and modding route docs to `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD` for new game, while preserving direct load-game resume to `02_HECTON_WORLD`.
- Verified `CURRENT_BATCH.md` still has no `<AGENT_PROMPT id="13pro">`.
- Verified route-stale direct-flow strings remain only in historical agent logs, not active architecture/modding docs.

Cinematic Cheats used:
- No new physical simulation. This pass keeps evidence aligned with the authored orbital cinematic route.
- Low: load-game remains direct to world when resuming saves.
- Middle: new-game prologue route is explicit for normal validation.
- High/Ultra: richer orbit/reentry presentation can scale on the same route contract.

Exact Microseconds saved:
- Runtime change: 0 us. Editor/docs/test hygiene only.
- Avoided broad `SceneRuntimeService` mutation: 0 recurring hot cost added.

Verification:
- `rg` confirms no `<AGENT_PROMPT id="13pro">` in `CURRENT_BATCH.md`.
- `rg` confirms stale direct `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD` text in this scope is historical log-only.
- YAML sanity: mandatory `m_RootGameObject` check is false because `.unity` scenes use `m_Roots`, not prefab root fields. Follow-up scene checks confirm `m_Roots`, `GameObject`, Main Camera FileID `1823536641`, bootstrap component FileID `1823536646`, script GUID `444968fbfc5a4eb291617d5798a39dce`, camera transform/far clip fields, and menu `newGameTargetSceneName: 01_ORBIT`.
- Whole-repo `git diff --check` is not clean because `Docs/Tasks/CURRENT_BATCH.md` has trailing whitespace outside this domain. Scoped 13pro diff check must be used for this pass.
- Compile not relaunched: CPU measured `57.6-99.8%`, above the AGENTS `dotnet build` limit.

Remaining proof debt:
- Unity import/Console after external Candice SQLite reference wall is fixed.
- Play Mode proof for `01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
- Profiler/GC capture across low, middle, high, and ultra quality weights.

## 2026-05-27 - Seventh Pass / Orbit Activation And Handoff Context

What was wrong:
- `GameBootstrapper.RequiresGameplaySceneActivation` treated every loaded scene except `00_BOOTSTRAP` and `01_MAIN_MENU` as gameplay. With the new production route, `01_ORBIT` could trigger full world activation during the prologue instead of waiting for the final capsule/ocean handoff.
- `GameStartContextHolder` persisted handoff recovery expires after 45 seconds, while the modeled prologue reaches whiteout at about `688.3s`. The in-memory context is normally valid, but the persisted recovery timestamp was not refreshed for the final world transition.

What was done:
- Added `OrbitSceneName = "01_ORBIT"` and `IsOrbitScene` to `GameBootstrapper`.
- Excluded `01_ORBIT` from gameplay-scene activation so world systems activate only after `02_HECTON_WORLD` is loaded.
- Added a one-shot `RefreshGameStartContextHandoff()` call in `PrologueWorldHandoffSceneLoader` immediately before `ISceneService.LoadScene`.
- Kept the route on `ISceneService`; no raw `SceneManager.LoadScene` or hot GlobalRegistry polling was added.

Cinematic Cheats used:
- No real physics added. The pass protects the existing visual-fake orbital prologue so the frame budget is not stolen by hidden world bootstrap.
- Low: orbit scene runs prologue-only systems; no world activation overlap.
- Middle: final handoff keeps context recovery fresh without long global TTL.
- High/Ultra: saved activation headroom remains available for overkill orbital/reentry presentation.

Exact Microseconds saved:
- Orbit activation guard: one cold string compare added; prevents a potential multi-ms world activation overlap during `01_ORBIT`.
- Handoff refresh: one cold PlayerPrefs persistence refresh at final whiteout; 0 recurring hot cost.

Verification:
- `rg` confirms `OrbitSceneName`, `IsOrbitScene`, and the exclusion inside `RequiresGameplaySceneActivation`.
- `git diff --check -- Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs Assets/_Project/Scripts/Prologue/Space/PrologueWorldHandoffSceneLoader.cs` passes with only CRLF normalization warning on `GameBootstrapper.cs`.
- `rg` confirms `RefreshGameStartContextHandoff` is called immediately before `sceneService.LoadScene`.
- Untracked 13pro files have no trailing whitespace.
- Build not launched after this pass: CPU `70.2%`, active `dotnet/csc` count `7`.

Remaining proof debt:
- Compile not claimed until CPU/process gate allows it and external vendor/project-reference issues are cleared.
- Unity Play Mode still required for New Game -> `01_ORBIT` -> final handoff -> `02_HECTON_WORLD`.

## 2026-05-27 - Eighth Pass / Visible Reentry Overlay Contract

What was wrong:
- `01_ORBIT` did not contain any plasma/window/overlay object, and `PrologueOrbitSceneBootstrap` configured `OrbitalDropReentryVfxController` with `null` overlay, `null` window renderer, and `null` material.
- `MAT_OrbitalDropReentryPlasma` existed, but it was not connected to the runtime prologue scene. The result could be a correctly timed descent with audio/ambient/global shader state but no visible plasma or whiteout surface.
- Writing VFX floats directly to a shared material asset is a bad Play Mode contract when a renderer exists.

What was done:
- Added serialized `orbitPlasmaMaterial` to `PrologueOrbitSceneBootstrap` and bound `01_ORBIT.unity` to `MAT_OrbitalDropReentryPlasma` GUID `75c99b06202152b46af43611e7c9cad9`.
- Bootstrap now cold-creates a camera-local quad named `__HECTON_REENTRY_PLASMA_OVERLAY`, assigns the existing plasma material, disables shadows/probes/motion vectors, and passes its transform/renderer/material into `OrbitalDropReentryVfxController`.
- `OrbitalDropReentryVfxController` now scales the overlay from camera FOV/aspect with finite guards, so the whiteout covers desktop/mobile aspect ratios without per-device authoring.
- VFX uniforms now go through one reusable `MaterialPropertyBlock` when a window or overlay renderer exists. Material mutation remains only as fallback when no renderer is bound.

Cinematic Cheats used:
- Full-screen plasma and cloud whiteout are one camera-local transparent quad, not particles, fluids, or simulated atmosphere.
- Low: one quad, one material, MPB writes only on value changes.
- Middle: aspect-safe full-frame whiteout with existing shader noise.
- High: richer shader response can ride continuous `GlobalQualityWeight`.
- Ultra: visual overkill can be added inside the shader/material route without new gameplay truth.

Exact Microseconds saved:
- Avoided particle/volume reentry simulation: expected savings are multi-ms versus a naive particle stack on i3/MX350.
- Added active hot cost: FOV/aspect scalar math plus MPB upload on changed values, estimated under 2 us/frame during active reentry.
- Idle cost remains 0 us for overlay updates because `HasActivePresentationState()` exits before `MaintainCameraLocalOverlay()`.

Verification:
- `rg` confirms `orbitPlasmaMaterial` serialized in `01_ORBIT.unity`, plasma material GUID binding, overlay name, `MaterialPropertyBlock`, and no `Resources.Load` or runtime `AssetDatabase.LoadAssetAtPath` in prologue runtime.
- `git diff --check -- Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs Assets/_Project/Scenes/01_ORBIT.unity` passes with only CRLF normalization warning on `OrbitalDropReentryVfxController.cs`.
- Compile was not launched: CPU `63.3%`, `dotnet/csc=0`; AGENTS forbids build above 50% CPU.

Remaining proof debt:
- Unity import/Console for new serialized field and camera-local mesh path.
- Play Mode frame capture: verify visible Aegir/Hecton orbit, plasma overlay, whiteout, hydration fade, and final `02_HECTON_WORLD` handoff.
- Profiler/GC proof across low, middle, high, and ultra quality weights.

## 2026-05-27 - Ninth Pass / Plasma Shader Dependency Removal

What was wrong:
- `Hecton_OrbitalDropReentryPlasma.shader` sampled `_HectonPrebakedVectorNoise3D`.
- Project search found no owner publishing `_HectonPrebakedVectorNoise3D`; the only hit was the shader itself.
- That makes the new visible overlay dependent on undefined texture binding behavior and costs a 3D texture sample on the fragment path.

What was done:
- Removed `TEXTURE3D(_HectonPrebakedVectorNoise3D)` and its sampler from the plasma shader.
- Replaced the 3D texture fetch with deterministic procedural `Hash21` cell noise derived from UV, speed, and `_VoronoiScale`.
- Kept the existing material contract intact: no new serialized asset, no runtime loader, no new global route.

Cinematic Cheats used:
- Plasma micro-variation is now procedural hash noise plus 2D shared noise and Voronoi, not a real 3D volume.
- Low: no 3D sampler dependency.
- Middle: stable grain and cloud/plasma breakup.
- High: shader math can be overdriven by current quality/material values.
- Ultra: future overkill can be authored in the same shader without a new texture owner.

Exact Microseconds saved:
- Per-fragment 3D texture sample removed from the active overlay.
- Added cost is one `Hash21` call on a floored UV cell; expected cheaper and more deterministic than an unowned 3D texture fetch on low-end GPUs.

Verification:
- `rg` confirms no `HectonPrebakedVectorNoise3D`, `TEXTURE3D`, or `SAMPLE_TEXTURE3D` remains in `Hecton_OrbitalDropReentryPlasma.shader`.
- `git diff --check -- Assets/_Project/Art/Shaders/Hecton_OrbitalDropReentryPlasma.shader Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs Assets/_Project/Scenes/01_ORBIT.unity` passes with CRLF normalization warnings only.
- Compile was not launched: CPU `66%`, `dotnet/csc=0`; AGENTS forbids build above 50% CPU.

Remaining proof debt:
- Unity shader import proof for the modified URP shader.
- Play Mode frame capture for overlay visibility and handoff.
- Profiler/GC proof across quality weights.

Final scoped sweep:
- Expected touched surfaces in this pass: `Hecton_OrbitalDropReentryPlasma.shader`, `01_ORBIT.unity`, `OrbitalDropReentryVfxController.cs`, new `PrologueOrbitSceneBootstrap.cs`, and `13pro` status/rationale/log files.
- Scoped `git diff --check` passes with CRLF normalization warnings only.
- Final build gate: CPU `100%`, compiler process count `9`; build not launched by rule.

## 2026-05-27 - Tenth Pass / Overlay Near Clip And SRP Batcher Correction

What was wrong:
- The camera-local plasma overlay was initialized at `0.08m`, but `01_ORBIT` Main Camera uses near clip `0.3m`. A correctly created overlay could be clipped and invisible.
- The previous upload route used `MaterialPropertyBlock` on a MeshRenderer quad. Current URP mandate forbids MPB on standard geometry because it breaks SRP Batcher expectations.

What was done:
- Raised the bootstrap overlay default distance to `0.35m`.
- `OrbitalDropReentryVfxController` now clamps effective overlay distance to `max(configuredDistance, camera.nearClipPlane + 0.03m)` and stores that effective value in telemetry.
- Removed MPB and `SetPropertyBlock` from the prologue VFX controller.
- Plasma runtime state now uploads through two dirty-gated global shader vectors: `_HectonReentryPlasmaState0` and `_HectonReentryPlasmaState1`.
- `Hecton_OrbitalDropReentryPlasma.shader` reads those runtime globals for heat, opacity, velocity, altitude, and quality pressure while keeping static material controls for authored color/noise defaults.

Cinematic Cheats used:
- Reentry/whiteout remains a single camera-local transparent quad, not particle atmosphere or volumetric simulation.
- Low: one near-plane-safe quad, no MPB, no 3D sampler.
- Middle: FOV/aspect-safe full-frame whiteout.
- High: richer shader noise and color response through global runtime vectors.
- Ultra: more shader overkill can be added inside the same fake without changing gameplay truth.

Exact Microseconds saved:
- Removed MPB allocation/storage and per-renderer property-block upload from the active overlay path.
- Added cost: two `Shader.SetGlobalVector` calls only when runtime state changes, plus scalar near-clip/FOV/aspect math.
- Correctness gain: prevents an entire plasma/whiteout pass from being raster-clipped by near plane.

Verification:
- `rg` confirms no `MaterialPropertyBlock`, `SetPropertyBlock`, `renderer.material`, `.materials`, runtime `Resources.Load`, runtime `AssetDatabase.LoadAssetAtPath`, `TEXTURE3D`, `SAMPLE_TEXTURE3D`, or `_HectonPrebakedVectorNoise3D` remains in the patched overlay path.
- `git diff --check -- Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs Assets/_Project/Art/Shaders/Hecton_OrbitalDropReentryPlasma.shader` passes with CRLF normalization warnings only.
- Build not launched: CPU average `30.5%`, but `1` active `dotnet` process violates the AGENTS compiler gate.

Remaining proof debt:
- Unity import/Console for C# and shader changes.
- Play Mode frame capture for visible overlay after near-clip correction.
- Profiler/GC proof across low, middle, high, and ultra quality weights.

## 2026-05-27 - Eleventh Pass / Orbit Runtime Root Lifetime Correction

What was wrong:
- `PrologueOrbitSceneBootstrap` created `__HECTON_PROLOGUE_ORBIT_RUNTIME` with `HideFlags.DontSave`.
- Unity's official `HideFlags.DontSave` documentation states that object is not destroyed when a new scene is loaded.
- On the production route `01_ORBIT -> 02_HECTON_WORLD`, that could leak orbital director, sequence bridge, VFX, and handoff loader into the world scene.

What was done:
- Runtime root now uses `HideFlags.None` when created.
- Any already-found runtime root in `01_ORBIT` is normalized back to `HideFlags.None`.
- The tiny shared overlay mesh keeps only `DontSaveInEditor | DontSaveInBuild`, avoiding asset persistence without creating a scene-load survivor.
- Cold allocation comments in the bootstrap were normalized to the project-required `COLD ALLOC: Type[capacity] - reason - owner` format.

Cinematic Cheats used:
- No cleanup polling, no persistent prologue manager, no second scene authority.
- Low: prologue systems die with the orbit scene, preserving world frame budget.
- Middle: clean handoff keeps world activation isolated from orbit presentation.
- High: richer orbit/plasma work remains scoped to `01_ORBIT`.
- Ultra: visual overkill still lives in shader/presentation, not in leaked persistent systems.

Exact Microseconds saved:
- Hot patch cost: 0 us.
- Prevented cost: duplicate prologue late-frame stack after world load. Exact runtime saving requires Play Mode/profiler proof, but the failure class is removed by lifetime ownership.

Verification:
- Scoped `rg` confirms no `HideFlags.DontSave`, `HideAndDontSave`, `DontDestroyOnLoad`, MPB, runtime `Resources.Load`, or unowned 3D texture dependency remains in the prologue overlay/bootstrap path.
- `git diff --check -- Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs Assets/_Project/Scripts/Prologue/VFX/OrbitalDropReentryVfxController.cs Assets/_Project/Art/Shaders/Hecton_OrbitalDropReentryPlasma.shader` passes with CRLF normalization warnings only.
- Build not launched: CPU `45.7%`, but `2` active `dotnet` processes violate the AGENTS compiler gate.

Remaining proof debt:
- Unity import/Console.
- Play Mode check that no `__HECTON_PROLOGUE_ORBIT_RUNTIME` survives after `02_HECTON_WORLD` loads.
- Profiler/GC proof across low, middle, high, and ultra quality weights.

## 2026-05-27 - Twelfth Pass / Manual Release Watchdog Correction

What was wrong:
- `01_ORBIT` has no authored `OpenXRManualOverrideLever`.
- The bridge treated orbital `PhaseWhiteout` as an immediate manual-complete fallback, so the "manual release" beat could auto-complete without player action.
- Removing that fallback entirely would risk an infinite prologue if no lever/input path exists.

What was done:
- `PrologueSequenceRegistryBridge` now accepts standalone orbit manual release from existing first-party `PlayerInputSignal` commands: `Interact`, `PrimaryAction`, or `SecondaryAction`.
- Direct VR/manual lever completion remains supported through `ManualOverrideLever` source hash.
- Orbital whiteout is now captured as a pending watchdog and only completes the manual stage after 180 frames.
- The synthetic input release uses the same `0.4s` whiteout hold as `OpenXRManualOverrideLever`.

Cinematic Cheats used:
- No new VR object, no reflection, no direct UI/VR dependency, no real physics interlock.
- Low: one button release plus watchdog.
- Middle: same input route with stable diegetic HUD prompt.
- High: authored physical lever can later publish the same signal.
- Ultra: overkill lever animation/haptics can layer on top without changing sequence truth.

Exact Microseconds saved:
- No cost outside the manual stage.
- Added cost during manual stage: bounded scan of existing `PlayerInputSignal` frame snapshot plus one pending whiteout snapshot check; estimated under 2 us/frame on i3/MX350.
- Prevented cost: no direct UI/VR assembly dependency or hot lever scene search.

Verification:
- `rg` confirms `TryConsumeManualReleaseInput`, `StandaloneOrbitWhiteoutFallbackFrames = 180`, and `PlayerInputSignalCommands.Interact/PrimaryAction/SecondaryAction` in `PrologueSequenceRegistryBridge`.
- Scoped `git diff --check -- Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs` passes with CRLF normalization warnings only.
- Build not launched: CPU `62.7%`, `dotnet/csc=0`; AGENTS forbids build above 50% CPU.

Remaining proof debt:
- Unity import/Console for the bridge patch.
- Play Mode test: reach manual stage in `01_ORBIT`, press Interact/Primary/Secondary, verify impact/hydration/ocean handoff continues.
- Profiler/GC proof for the manual stage frame-snapshot scans.

## 2026-05-27 - Thirteenth Pass / Near-Surface Impact Sync

What was wrong:
- After manual release, `AwaitableDropSequenceDirector.RunImpactSyncAsync` waited one frame and then allowed hydration/ocean handoff.
- With current orbital defaults, Mach10 happens around `684.65s` while the capsule is still `13893.5m` above Hecton.
- A real player release at that moment could turn capsule descent into a world-load seam instead of a near-surface impact.

What was done:
- Added `impactSyncDistanceMeters = 120m`, `impactSyncMinimumHoldSeconds = 0.65s`, and `impactSyncWatchdogSeconds = 8s`.
- `RunImpactSyncAsync` now waits for orbital/atmospheric distance to reach the near-surface window before `PublishMassiveImpact()` and `PublishOceanHandoff()`.
- If the distance route is absent, the watchdog prevents an infinite prologue stall.
- Fault handling stays strict: non-finite orbital/atmospheric data dumps black boxes and stops the sequence.

Cinematic Cheats used:
- No collision solver, no raycast-to-terrain, no capsule rigidbody simulation.
- Low: scalar distance gate with whiteout cover.
- Middle: short deterministic descent hold after manual release.
- High: stronger VFX/audio/camera shake can play during the same gate.
- Ultra: overkill reentry presentation remains visual-only; handoff truth stays distance-based.

Exact Microseconds saved:
- Rejected real physics/collision impact, expected to avoid broadphase/raycast and scene dependency cost entirely.
- Added hot cost only during impact sync: one orbital snapshot read plus optional atmospheric signal scan per frame, estimated under 3 us/frame on i3/MX350 for the modeled `3.6s` release-to-impact window.

Verification:
- PowerShell 60 Hz model: `Mach10=684.65s`, `Mach10Distance=13893.5m`, `WhiteoutSignal=688.25s`, `ImpactReady=688.25s`, `ImpactDistance=81.1m`, `ManualToImpactHold=3.6s`.
- `rg` confirms `impactSyncDistanceMeters`, `TryResolveImpactRangeReached`, and no remaining one-frame `ImpactSync` return pattern.
- Scoped `git diff --check -- Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs Assets/_Project/Scripts/Prologue/Space/PrologueOrbitSceneBootstrap.cs` passes with CRLF normalization warnings only.
- Build not launched: CPU `100%`, `dotnet/csc=8`; AGENTS forbids build.

Remaining proof debt:
- Unity import/Console for sequence, bridge, bootstrap, shader.
- Play Mode: new game route into `01_ORBIT`, manual release, near-surface impact wait, hydrated/ocean handoff into `02_HECTON_WORLD`.
- Profiler/GC proof for impact-sync snapshot reads across quality weights.

## 2026-05-27 - Fourteenth Pass / Impact Black-Box Coalescing

What was wrong:
- The new impact-sync wait fixed the kilometers-high handoff, but it still wrote multiple black-box entries in one frame: orbital sample, atmospheric sample, and generic wait.
- The requirement is a fixed-size last-300-frames diagnostic ring. Event spam during impact compresses useful history and makes a crash dump less trustworthy.

What was done:
- `TryResolveImpactRangeReached` now only resolves range plus one hash/flag pair for the frame.
- `RunImpactSyncAsync` records exactly one `ImpactSync` telemetry entry per wait frame.
- Added inspector tooltips for impact sync distance/minimum/watchdog fields.
- Added `OnValidate` clamps so bad inspector values cannot make watchdog shorter than the minimum hold.

Cinematic Cheats used:
- No physics collision, no terrain raycast, no extra DTO, no second telemetry buffer.
- Low: one scalar frame-state write.
- Middle: same distance gate with cleaner crash proof.
- High: richer VFX/audio can still play over the same deterministic wait.
- Ultra: overkill presentation remains visual-only; telemetry stays one truth route.

Exact Microseconds saved:
- Removed up to two extra DataVault write-lock/write/unlock cycles per impact-sync frame.
- Estimated saving: 2-6 us/frame on i3/MX350 during the modeled `3.6s` post-release window.
- No added hot allocation; added validation is editor/cold inspector path only.

Verification:
- `rg` confirms the normal impact-sync path has one `RecordStage(PrologueStage.ImpactSync, impactStateHash, impactFlags)` call.
- Scoped `git diff --check -- Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs` passes with CRLF normalization warning only.
- Build not launched: CPU `92.1%`, `dotnet/csc=0`; AGENTS forbids build above 50% CPU.

Remaining proof debt:
- Unity import/Console for the sequence patch.
- Play Mode: manual release, near-surface wait, hydration, ocean handoff.
- Profiler/GC proof for the coalesced impact-sync wait.

## 2026-05-27 - Fifteenth Pass / Plasma Material Shader Contract

What was wrong:
- `MAT_OrbitalDropReentryPlasma` serialized `_PlasmaLowTier: 1`.
- `Hecton_OrbitalDropReentryPlasma.shader` no longer declares `_PlasmaLowTier`.
- The material did not serialize `_PlasmaQualityPressure`, so authored import state used shader default pressure instead of a high-fidelity baseline.

What was done:
- Removed `_PlasmaLowTier`.
- Added `_PlasmaQualityPressure: 0`.
- Kept the material bound to shader GUID `ed0a893349281084383651b4a57938cd`.

Cinematic Cheats used:
- No runtime material clone.
- No extra texture.
- No shader compatibility branch for a dead property.
- Low/middle/high/ultra all use one material; runtime global vectors apply continuous pressure.

Exact Microseconds saved:
- Runtime hot-path delta: 0 us.
- Avoided future runtime workaround cost and stale material import confusion.
- High-tier starts from visual-overkill baseline instead of survival-pressure material default.

Verification:
- `rg` confirms `_PlasmaLowTier` is absent from the prologue material/shader path.
- `rg` confirms `_PlasmaQualityPressure` exists in shader and material.
- `Select-String` confirms material name, shader GUID, and `_PlasmaQualityPressure: 0`.
- Scoped `git diff --check -- Assets/_Project/Art/Materials/VFX/MAT_OrbitalDropReentryPlasma.mat` passes.
- Build not launched: CPU `100%`, `dotnet/csc=2`; AGENTS forbids build under both conditions.

Remaining proof debt:
- Unity material/shader import.
- `01_ORBIT` Game View capture at low/middle/high/ultra quality weights.
- Profiler/GC proof for the prologue VFX path.

## 2026-05-27 - Sixteenth Pass / Orbital Black-Box Vault Lock

What was wrong:
- `OrbitalRelativityDirector.RecordTelemetry()` wrote the orbital black-box ring through `TryResolveHandle`.
- That is an unlocked mutable `GlobalDataVault` alias in a runtime tick path.
- For a crash diagnostic ring, unlocked writes are worse than no telemetry: they can produce false evidence under relocation or compaction pressure.

What was done:
- `RecordTelemetry()` now acquires the vault writer fence with `TryAcquireWriteLock`.
- Release is guaranteed through `finally`.
- `DumpTelemetry()` now reads through `TryReadOnlyHandle`.
- Removed the old mutable `TryResolveTelemetryRing` helper.

Cinematic Cheats used:
- None. This is memory ownership correctness, not presentation.
- Low: black-box survives memory pressure.
- Middle/high/ultra: richer orbital visuals keep the same locked diagnostic route.

Exact Microseconds saved:
- No speed claim. This adds one lock acquire/release around an existing telemetry write.
- Accepted cost: microsecond-level per orbital tick.
- Saved failure cost: prevents corrupted crash evidence from unlocked vault mutation.

Verification:
- `rg` confirms `TryAcquireWriteLock(in _telemetryRingHandle`, `ReleaseWriteLock(in _telemetryRingHandle`, and `TryReadOnlyHandle(in _telemetryRingHandle`.
- `rg` confirms `TryResolveTelemetryRing` is absent.
- Scoped `git diff --check -- Assets/_Project/Scripts/Prologue/Space/OrbitalRelativityDirector.cs` passes with CRLF normalization warning only.
- Build not launched: CPU `37.7%`, `dotnet/csc=1`; AGENTS forbids build while another dotnet process is active.

Remaining proof debt:
- Unity import/Console for orbital director.
- Play Mode descent through `01_ORBIT`.
- Dump file validation after forced NaN/crash path.
- Profiler/GC proof for the added vault writer fence.

Final verification addendum:
- Final scoped `git diff --check` over touched 13pro C#/material/log files passes with CRLF normalization warnings only.
- `rg` confirms `TryResolveTelemetryRing` is absent from `OrbitalRelativityDirector`.
- Build not launched: latest gate CPU `51.6%`, `dotnet/csc=0`; AGENTS forbids build above 50% CPU.
- Status remains PENDING VERIFICATION until Unity import/Console, Play Mode, Game View capture, and profiler/GC evidence exist.

## 2026-05-27 - Seventeenth Pass / Standalone Orbit Hydration Contract

What was wrong:
- The sequence treated proxy ocean readiness as `IsLowTier`.
- In standalone `01_ORBIT`, high-quality runs can have `GlobalQualityWeight=1`, while `02_HECTON_WORLD` is not loaded and cannot publish high-resolution ocean residency before the sequence-owned handoff.
- Result: `AwaitOceanHydration` could wait indefinitely after impact sync on high-quality devices.

What was done:
- Added `PrologueHydrationMode.StandaloneOrbitHandoffProxy`.
- Added `IPrologueSequenceRuntime.IsStandaloneOrbitHandoffProxyAllowed`.
- `PrologueSequenceRegistryBridge` exposes that property only when `allowStandaloneOrbitHydrationProxy` is true and the active scene is `01_ORBIT`.
- `AwaitableDropSequenceDirector` now checks high-resolution ocean readiness first, then accepts proxy readiness only for survival pressure or standalone handoff proxy.

Cinematic Cheats used:
- Handoff proxy is a scene-transition concealment fake, not gameplay ocean truth.
- High/ultra still prefer high-resolution surface readiness when available.

Exact Microseconds saved:
- Route stall risk changes from unbounded to zero for high-quality standalone orbit handoff.
- Added cost: one high-res readiness check plus one proxy policy branch per hydration wait frame.
- Expected cost: under 1 us/frame on i3/MX350.

Verification:
- `rg` confirms `StandaloneOrbitHandoffProxy`, `IsStandaloneOrbitHandoffProxyAllowed`, and explicit `allowProxy` calls.
- Scoped `git diff --check` passed with CRLF normalization warnings only.
- Build not launched: CPU `100%`, active `dotnet` processes `8`.
- Status remains PENDING VERIFICATION.

## 2026-05-27 - Eighteenth Pass / Reentry VFX DataVault Hot-Swap

What was wrong:
- `OrbitalDropReentryVfxController` listened for registry service replacement but only handled Dispatcher.
- A DataVault replacement could leave `_telemetryHandle` tied to the old vault.
- VFX black-box evidence during reentry/handoff could become stale or invalid.

What was done:
- Added DataVault hot-swap handling: release old telemetry buffer, replace `_dataVault`, reset cursor, reacquire telemetry storage.
- `OnDestroy` now uses the same release helper.
- Wrapped VFX telemetry post-acquire path in a full `try/finally` so `ReleaseWriteLock` runs on every exit.

Cinematic Cheats used:
- No new simulation. Existing camera-local plasma fake remains shader-driven.

Exact Microseconds saved:
- Normal frame cost: no new work.
- Hot-swap path: one release plus one cold reacquire.
- Telemetry write lock count unchanged; correctness improves without measurable expected cost.

Verification:
- `rg` confirms DataVault hot-swap handling and full `TryAcquireWriteLock`/`ReleaseWriteLock` path.
- Scoped `git diff --check` passed with CRLF normalization warnings only.
- Build not launched: CPU `100%`, active `dotnet` processes `8`.
- Status remains PENDING VERIFICATION.

Route wiring static addendum:
- `ProjectSettings/EditorBuildSettings.asset` contains `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD`.
- `01_MAIN_MENU.unity` serializes `newGameTargetSceneName: 01_ORBIT` and load-game target remains `02_HECTON_WORLD`.
- `01_ORBIT.unity` serializes `PrologueOrbitSceneBootstrap`; script GUID `444968fbfc5a4eb291617d5798a39dce` matches `PrologueOrbitSceneBootstrap.cs.meta`.
- `GameBootstrapper.RequiresGameplaySceneActivation` excludes `01_ORBIT`, so orbit is not treated as the hydrated gameplay world.
- No new route edit made in this addendum.
- Status remains PENDING VERIFICATION until Play Mode proves `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.

Compile wall addendum:
- Build gate opened after the latest static patches: CPU was about `20%`, `dotnet/csc=0`.
- Ran exactly one compile attempt: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`.
- Result: failed before scoped 13pro proof.
- Blocking errors are out of 13pro scope: unresolved Odin attribute/namespaces, duplicate `Inventory/SoaInventoryQueryEngine.OffsetOf`, and missing `BufferID` members across vegetation, wreck, nav, and world contract files.
- There were also project-wide duplicate source warnings from `.csproj` include state.
- Rejected fix: touching vendor/Odin, inventory, world vegetation, wreck, nav, or broad generated project ownership from the prologue lane.
- Status remains STATIC PATCH COMPLETE / COMPILE FAILED ON OUT-OF-DOMAIN PROJECT WALL.

## 2026-05-27 - Nineteenth Pass / Dispatcher Rebind And Route Authority

What was wrong:
- `PrologueAcousticOrchestrator` and `OrbitalDropReentryVfxController` updated Dispatcher references on service replacement but kept their late-frame registration flags.
- After Dispatcher replacement, they could skip registration into the new lane while still believing they were active.
- `PrologueAcousticOrchestrator` is also a ref-listener, so rebound and replaced callbacks happen in the same registry event. Rebinding from both callbacks can duplicate-register then leave the local flag false after duplicate rejection.
- The production new-game route now includes `01_ORBIT`, but no dedicated architecture route card existed for the changed global route.

What was done:
- Audio: Dispatcher rebound now only caches the dispatcher; Dispatcher replacement owns rebind.
- Audio/VFX: Dispatcher replacement clears the late-frame registration flag and registers only when active.
- Audio/VFX/orbital/handoff: same-service replacement guard prevents idempotent rebound from corrupting registration state.
- Added `Docs/ARCHITECTURE/PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md`.
- Linked the route in `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md` as `YELLOW / STATIC_SOURCE_ONLY`.

Cinematic Cheats used:
- No new physical simulation. Existing orbital motion, plasma, ocean hydration proxy, and whiteout remain deterministic visual/audio fakes.
- Route card explicitly keeps `GlobalQualityWeight` as presentation/cadence pressure only, not scene truth.

Exact Microseconds saved:
- Normal frame delta: 0 us expected.
- Replacement-only path: one flag reset and one bounded registration per active owner.
- Prevented failure cost: complete loss of late-frame prologue audio/VFX/orbital presentation after Dispatcher replacement.

Verification:
- Scoped `git diff --check` passed with CRLF normalization warnings only.
- `rg` confirms dispatcher same-service guards and rebind paths in audio, VFX, orbital, and handoff owners.
- Route-card field scan confirms owner, instrument, producer/consumer phase, cadence, overflow/failure, shutdown/disposal, proof required before GREEN, and review disposition.
- Build not launched: latest gate CPU `100%`, active `dotnet` process `1`.
- Final gate recheck: CPU `80%`, `dotnet/csc=0`; build still forbidden by the >50% CPU rule.

Remaining proof debt:
- Unity import and Console.
- Play Mode route: `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
- Game View capture for Aegir/Hecton window and plasma whiteout.
- Profiler/GC proof for prologue audio/VFX/orbital late-frame lanes.
- No-survivor-root proof after `01_ORBIT` unload.

Final evidence refresh:
- Re-read disk state after compaction risk: `Status_13pro.md`, `Rationale_13pro.md`, Unity MCP skill.
- Scoped `git status` shows only expected 13pro touched/untracked files in this pass.
- Scoped `git diff --check` still passes with CRLF normalization warnings only.
- Current build gate: CPU `100%`, `dotnet/csc=0`; compile remains forbidden by CPU rule.
- No new runtime claim added. Status remains STATIC PATCH COMPLETE / PENDING UNITY RUNTIME PROOF.
## Pass 20 - Hydration Proxy And Sequence Black Box

What was wrong:
- `PrologueSequenceRegistryBridge.IsOceanSurfaceReady(allowProxy: false)` accepted the standalone `01_ORBIT` hydration proxy. The sequence director's high-resolution ocean check could therefore complete with proxy truth and record the wrong hydration mode.
- `AwaitableDropSequenceDirector` owned a 300-frame sequence black-box ring but did not handle DataVault replacement, stale handle validation, compaction fence, or allocation lock before reacquiring the buffer.

What was done:
- `Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs`: standalone orbit hydration proxy now requires `allowProxy`.
- `Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs`: sequence director now implements `IGlobalRegistryHotSwapListener`, releases/reacquires its black-box ring on DataVault replacement, skips same-service replacement notifications, validates handles through read-only access, uses explicit `TryGetGenerationHandle<PrologueSequenceTelemetryEntry>`, and refuses allocation during compaction/allocation locks.

Cinematic Cheats used:
- Kept standalone hydration proxy as a route bridge for `01_ORBIT`, not a physical ocean proof. This preserves the fake-first cinematic descent while avoiding false high-resolution state.
- No real ocean load, no collision solver, no scene raycast, no physical splashdown simulation added.

Exact Microseconds saved:
- Hydration readiness: one extra branch, estimated under 1 us/frame while waiting for ocean handoff.
- Sequence black-box hot-swap: 0 us normal-frame delta; replacement-only release/reacquire.
- Avoided stale-vault diagnostic corruption rather than saving frame time; crash evidence correctness is the value.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Core/PrologueSequenceRegistryBridge.cs Assets/_Project/Scripts/Narrative/Prologue/AwaitableDropSequenceDirector.cs` passes with CRLF normalization warnings only.
- Static scans confirm `allowProxy && allowStandaloneOrbitHydrationProxy`, sequence `IGlobalRegistryHotSwapListener`, same-service DataVault guard, explicit generation handle lookup, compaction fence, and allocation lock checks.
- Initial compile gate was closed: CPU `100%`, active `dotnet` process `48912`.
- Later compile gate opened: CPU `40%`, no active `dotnet/csc`, so `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` was run once.
- Compile result: failed outside 13pro on unresolved Odin/Sirenix attributes/namespaces, duplicate `SoaInventoryQueryEngine.OffsetOf`, duplicate `HectonMapMagicVegetationBridge.InvalidateChunksForNewPermanentEchoes`, missing `BufferID` members in world/vegetation/wreck/nav/sargassum contracts, duplicate source include warnings, and assembly version conflicts.
- Rejected fix: touching vendor/Odin, inventory, vegetation, world BufferID, wreck, nav, sargassum, or generated project include ownership from this prologue lane.

Remaining proof debt:
- Unity import/Console.
- Play Mode route: `01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD`.
- Game View capture of Aegir/Hecton orbit, manual release, plasma whiteout, near-surface impact wait, and ocean handoff.
- Profiler/GC capture and black-box dump validation.
