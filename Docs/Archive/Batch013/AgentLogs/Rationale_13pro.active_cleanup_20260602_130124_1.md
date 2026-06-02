# Rationale_13pro

Date: 2026-05-27
Agent: 13pro
Domain: Prologue orbital flight, Aegir approach, Hecton orbit, capsule descent.
Status: STATIC PATCH COMPLETE / COMPILE BLOCKED BY CPU AFTER OUT-OF-DOMAIN WALL

## Decision 0 - Prompt Authority

Problem: `Docs/Tasks/CURRENT_BATCH.md` has no `<AGENT_PROMPT id="13pro">`, while the user supplied an explicit `13pro` prologue domain directive in chat.
Solution: Treat the direct user assignment as the primary task source and record XML task count as 0. Continue with disk-backed status/rationale.
Rejected Alternatives: Applying neighboring `13xx` batch prompts would contaminate domain decisions and violate strict parsing.
Scalability potential: Keeps work confined to prologue/celestial/presentation so low, middle, high, and ultra device behavior is not polluted by unrelated domain changes.
Hardware Impact: No runtime impact. Prevents wrong-file churn and avoidable compile churn on i3/MX350.

## Decision 1 - Mandate Set

Problem: Prologue needs cinematic orbital/descent presentation without turning into expensive real orbital physics or cross-domain global authority pollution.
Solution: Use execution/bootstrap, GlobalRegistry, AUP, visual-fake-first, zero-GC, URP/HLOD, and Addressables mandates before coding. Use `TASTE.md` for noir identity instead of widening mandate count.
Rejected Alternatives: Real Kepler/orbital mechanics, per-particle atmospheric reentry, direct scene polling, and binary low/high quality switches.
Scalability potential: Low uses deterministic camera rails, LUTs, impostors, and cadence throttles. Middle adds richer fog/silt/cockpit response. High adds denser window VFX and longer LOD. Ultra spends saved cycles on overkill material/light/audio presentation, not new gameplay truth.
Hardware Impact: Target is avoiding >0.1 ms hot systems on i3/MX350; expected gain is from fake-first orbital visuals and no per-frame allocations rather than measured proof yet.

## Decision 2 - Prologue Runtime Patch

Problem: The current prologue presentation had three concrete rule violations: orbital sequence waited for atmospheric heat before playing orbital silence, `OrbitalRelativityDirector` changed Math LOD immediately on threshold crossings, and the reentry haptic path published both `SignalBus<HapticRequest>` and a direct `ToolHapticsRuntime` command.
Solution: Start sequence with a non-pausing orbital silence beat before atmospheric wait, add a 3-frame Math LOD hysteresis gate, compute locked-capsule leading-edge orientation from cached rotation, and keep haptics on the canonical SignalBus route consumed by `InputDispatcher`.
Rejected Alternatives: A real Kepler/reentry solver, direct tool haptic coupling, same-frame LOD threshold switches, early look/input freeze during orbital sightseeing, and Transform-driven celestial ownership.
Scalability potential: Low devices hold impostor/mesh decisions stable and avoid duplicated haptic queue work. Middle/high devices can keep richer mesh/VFX modes without flicker. Ultra spends the continuous quality budget on shader/VFX presentation rather than another truth owner.
Hardware Impact: Estimated hot-path saving is small but real: one direct haptic enqueue avoided every `hapticIntervalSeconds` during reentry, one per-frame TransformDirection avoided when capsule lock is active, and LOD renderer toggles are debounced to avoid spikes on i3/MX350.

## Decision 3 - Prototype Planet Rotation

Problem: `_PROLOGUE_CONTENT/Scripts/PlanetRotation.cs` used `Update()` and `Transform.Rotate()` as a planet spin owner, directly conflicting with the celestial route card forbidding transform-driven celestial truth.
Solution: Disable the obsolete prototype component on enable and leave the actual orbital/planet presentation to shader globals and the active prologue/celestial systems.
Rejected Alternatives: Keeping the transform spinner for visual motion, adding an unreferenced custom asmdef to pull `GlobalQualityWeight` into prototype content, or editing prefab/scene YAML to remove components while the workspace is dirty.
Scalability potential: Low/middle/high/ultra now share one authority route; visual richness should be bought through material/shader phase and `OrbitalRelativityDirector` quality weighting, not duplicate object rotation.
Hardware Impact: Removes a per-frame MonoBehaviour `Update()` and transform write from any prefab still carrying the prototype script; expected gain is microsecond-level but prevents a deterministic authority conflict on weak hardware.

## Decision 4 - Verification Gate

Problem: The project requires compile verification, but AGENTS forbids launching `dotnet build` while CPU load is above 50 percent or another `dotnet`/`csc` is running.
Solution: Ran scoped static checks and CPU/process gate. `dotnet` and `csc` were absent, but CPU load was 76 percent then 72 percent, so compile was not launched.
Rejected Alternatives: Starting a build under forbidden CPU pressure, editing unrelated generated project files to create a fake prologue csproj, or claiming runtime proof from static diff.
Scalability potential: Protects shared multi-agent machine from build contention; low/middle/high/ultra runtime claims remain static until Unity import/profiler can run cleanly.
Hardware Impact: No runtime impact. Verification debt remains: Unity import/Console and GC/profiler proof are still required when CPU/build gate is clear.

## Decision 5 - Prologue VFX/Audio Second Pass

Problem: `OrbitalDropReentryVfxController` sampled `GlobalQualityWeight` only on enable, so thermal/quality pressure during the 10-15 minute descent could not downshift or upshift presentation. It also published VFX state and wrote Vault telemetry every LateFrame even before any reentry signal. Prologue VFX/audio late-frame registration was not runtime-gated.
Solution: Refresh quality once per frame, skip VFX state signal and black-box write while the controller is idle/settled, add DataVault allocation guards, and gate late-frame registration behind play mode plus dispatcher availability for VFX and audio.
Rejected Alternatives: Per-frame idle telemetry forever, editor-time GlobalRegistry writes, raw scene disablement, and quality policy sampled only during scene load.
Scalability potential: Low devices avoid idle lane pressure and Vault locks. Middle/high/ultra keep continuous quality response during active plasma/whiteout/hydrated fade without changing DTO layout or authority route.
Hardware Impact: Saves one `ReentryVfxStateSignal` publish and one Vault write-lock/write/unlock per idle frame per enabled VFX controller. On i3/MX350 this is microsecond-level but removes avoidable hot-path pressure before the prologue actually enters reentry.

## Decision 6 - Prologue Duration And Orbit Presentation

Problem: `OrbitalRelativityDirector` default pacing was not a 10-15 minute prologue. `startDistanceMeters=12000` at `320 m/s` produces a seconds-scale approach, so the narrative sequence waits almost immediately for reentry instead of selling Aegir/Hecton orbit.
Solution: Move default approach to `260000 m` at `300 m/s`, set reentry envelope to `70000 m`, whiteout to `5000 m`, and add a triangle-wave diamond orbit offset in presentation only. The orbit fake scales continuously from survival to overkill through `GlobalQualityWeight`; gameplay truth remains the existing distance/velocity snapshot.
Rejected Alternatives: Real Kepler/orbital gravity, spinning scene transforms, per-particle atmosphere, a second celestial authority, and binary low/high cinematic branches.
Scalability potential: Low uses small diamond parallax and impostor continuity. Middle holds stable mesh/cloud handoff. High adds richer orbit travel through larger presentation offsets. Ultra increases apparent orbital sweep without changing DTO layout or authority route.
Hardware Impact: Replaces a likely real-orbit temptation with two triangle waves and scalar lerps during presentation. Expected cost is sub-10 us on i3/MX350 and buys minutes of visible orbital travel without simulation debt.

## Decision 7 - 01_ORBIT Wiring Without Product Route Mutation

Problem: `01_ORBIT.unity` contained prototype visual objects and `PlanetRotation`, but no references to `OrbitalRelativityDirector`, `AwaitableDropSequenceDirector`, `PrologueSequenceRegistryBridge`, or `OrbitalDropReentryVfxController`. Existing prologue code could not start from that scene.
Solution: Add `PrologueOrbitSceneBootstrap` to the `01_ORBIT` Main Camera. At play-time, only when that exact scene is loaded, it creates a scene-local inactive runtime root, adds the four prologue runtime components, binds Main Camera/Hecton surface/Aegir/cloud renderers, then activates the root so OnEnable runs after bindings.
Rejected Alternatives: Changing `EditorBuildSettings` to insert `01_ORBIT`, changing main menu target scene, broad raw scene YAML authoring of every component field, runtime `FindObject` hot polling, and hidden global self-spawn in non-orbit scenes.
Scalability potential: Low devices get cold scene composition with existing impostors and stable Math LOD. Middle/high/ultra can author richer overlay/material references later while keeping the same bootstrap contract and continuous quality scaling.
Hardware Impact: Cold-only scene root traversal and four component adds. Runtime hot impact is only the intended prologue systems; no per-frame scene search is introduced. This removes the first-20-minutes blocker class "scene/asset wiring blocker" without changing the current product handoff.

## Decision 8 - Static Contract Compatibility

Problem: The audio quality-refresh rename made `AdvancedAcousticsSmokeTester` extract a deleted `RefreshQualityPolicyCold()` method, creating a stale editor test failure unrelated to gameplay.
Solution: Update the test extraction to `RefreshQualityPolicy()` and keep assertions focused on continuous quality byte refresh with no binary low-memory registry profile.
Rejected Alternatives: Restoring misleading cold-only naming, disabling the smoke test, or leaving an expected static failure for the integrator.
Scalability potential: Low/middle/high/ultra keep a continuous quality scalar in audio presentation. Test language now matches live behavior.
Hardware Impact: Editor-only change. Runtime impact is unchanged from Decision 5.

## Decision 9 - Verification Boundary

Problem: Compile verification is mandatory, but the machine was under load and other `dotnet` processes were active.
Solution: Re-ran scoped static scans and `git diff --check`; did not launch build while CPU was 84-100 percent and multiple `dotnet` processes existed.
Rejected Alternatives: Breaking AGENTS build gate or claiming Unity import/Console proof from source scans.
Scalability potential: Prevents shared build contention. Runtime/platform proof remains pending until Unity can import and profile.
Hardware Impact: No runtime impact. Verification debt remains: Unity import, Console, Play Mode through `01_ORBIT`, profiler/GC, and frame capture are still required.

## Decision 10 - Orbit Scene Visibility Contract

Problem: `01_ORBIT` Main Camera used prototype identity rotation and `100000` far clip, while `OrbitalRelativityDirector` places Hecton/Aegir on the `-Y` presentation axis and starts the approach at `260000 m`. That can render the expensive prologue as an empty window. Aegir was also at risk of being treated like a mutually exclusive Hecton impostor instead of a separate gas giant backdrop.
Solution: `PrologueOrbitSceneBootstrap` now cold-aligns the orbit camera to look down world `-Y`, moves it to the presentation origin, and raises far clip to `360000 m`. `OrbitalRelativityDirector` now has a dedicated Aegir backdrop binding and keeps Hecton mesh rendering when no actual Hecton impostor exists.
Rejected Alternatives: Shortening the prologue distance to fit the old camera clip, rotating the authored scene by hand in raw YAML, using Aegir as a Hecton LOD impostor, real celestial orbit simulation, or changing production BuildSettings/main-menu routing.
Scalability potential: Low devices get a visible cheap presentation with stable mesh and far clip. Middle devices keep cloud/mesh continuity. High devices get larger orbital parallax and separate Aegir/Hecton composition. Ultra devices can add richer material/light overkill on the same camera axis without changing authority routes.
Hardware Impact: Camera alignment and far clip writes are cold-only scene composition. Runtime cost is 0 recurring us. The fix prevents wasted VFX/audio/orbit runtime work on i3/MX350 where the player would otherwise see nothing useful.

## Decision 11 - Post-Patch Verification Boundary

Problem: The scene visibility patch needs Unity import and Play Mode proof, but the build gate remains closed.
Solution: Ran scoped static verification only. `git diff --check` reports no whitespace errors, only CRLF normalization warnings. CPU was 100 percent and a `dotnet` process was active, so no build was launched.
Rejected Alternatives: Claiming runtime proof from static scans, starting another build under a running `dotnet`, or hiding verification debt in chat only.
Scalability potential: Keeps the multi-agent machine stable. Low/middle/high/ultra runtime proof remains blocked until Unity can import and frame-capture the scene.
Hardware Impact: No runtime impact. Proof debt remains: Unity Console, `01_ORBIT` Play Mode, GC/profiler, and capture from the orbit window camera.

## Decision 12 - Production Prologue Route

Problem: The production route still bypassed the prologue. `MainMenuController` loaded `02_HECTON_WORLD` for new games, `01_ORBIT` was absent from BuildSettings, and no owner converted `PrologueCompleteSignal` into the guarded world transition.
Solution: Add `01_ORBIT` to BuildSettings between menu and world. Add `newGameTargetSceneName = 01_ORBIT` while leaving load-game `targetSceneName = 02_HECTON_WORLD`. Add `PrologueWorldHandoffSceneLoader` to the orbit runtime root; it consumes the typed `SignalBus<PrologueCompleteSignal>` ocean handoff and calls `ISceneService.LoadScene("02_HECTON_WORLD")` after a short whiteout buffer.
Rejected Alternatives: Forcing all load-game saves through prologue, raw `SceneManager.LoadScene`, global `SceneRuntimeService` route mutation, self-`Update()` polling, hot `GlobalRegistry.Scene` polling every frame, or using legacy `GlobalSignals` queues.
Scalability potential: Low devices get deterministic scene routing with no simulation overhead. Middle keeps normal save/load ergonomics. High/ultra can spend prologue time on richer orbit/reentry presentation while the same typed handoff drives world activation.
Hardware Impact: The handoff loader costs one late-frame signal snapshot while `01_ORBIT` is active, then unregisters after load request and is destroyed with the scene. Estimated active cost is 1-5 us/frame on i3/MX350 during prologue; 0 recurring us in world.

## Decision 13 - Authored Camera Consistency

Problem: Runtime bootstrap corrected `01_ORBIT` camera direction/far clip, but the scene asset itself still showed the obsolete `+Z` camera and `100000` far clip, creating misleading editor previews and fragile manual validation.
Solution: Store the orbit-window camera direction and clip range in `01_ORBIT.unity`: rotation `x=0.7071068,w=0.7071068`, position origin, far clip `360000`.
Rejected Alternatives: Runtime-only correction, manual Unity-only authoring without text proof, or shortening the orbital approach distance to fit the old clip plane.
Scalability potential: Low/middle/high/ultra all validate against the same camera axis before runtime systems start. Visual scale can still increase through `GlobalQualityWeight`, not scene-specific camera hacks.
Hardware Impact: Authored scene data has 0 runtime hot cost. It reduces validation waste by making editor screenshots match the runtime orbit window.

## Decision 14 - Route Verification Boundary

Problem: Route changes touch menu, BuildSettings, and prologue scene code, so compile/Unity import proof is mandatory, but the build gate is still closed.
Solution: Ran scoped static route checks and `git diff --check`. CPU measured 56-58 percent with no `dotnet`/`csc`; AGENTS still forbids build because CPU is above 50 percent.
Rejected Alternatives: Claiming route proof without Unity import, launching build under CPU violation, or hiding the fact that `01_ORBIT -> 02_HECTON_WORLD` still needs Play Mode confirmation.
Scalability potential: Static route is now correct; runtime proof remains pending for all quality tiers.
Hardware Impact: No runtime impact from verification. Pending proof: scene import, main-menu new game, prologue completion, world handoff, GC/profiler, screenshot capture.

## Decision 15 - Compile Attempt Boundary

Problem: After route work, compile proof became mandatory. CPU later dropped below the project gate and no `dotnet`/`csc` process was active.
Solution: Ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`. The build failed before reaching domain code on generated Unity project-reference cycles: `Unity.RenderPipelines.Core.Editor.csproj` and `Unity.ShaderGraph.Editor.csproj` both reported `MSB4006` circular dependency in `ResolveProjectReferences`.
Rejected Alternatives: Editing Unity package/generated project graphs from the prologue domain, claiming the route compiles, or launching a second build attempt after CPU rose to 86 percent and a `dotnet` process became active.
Scalability potential: No runtime route claim changes. Compile proof remains blocked by external project infrastructure, not by a known prologue diagnostic.
Hardware Impact: No runtime impact. Build attempt consumed wall time only; runtime microsecond estimates remain static until Unity import/profiler can run.

## Decision 16 - Canonical Handoff Split

Problem: `OrbitalRelativityDirector` was publishing `PrologueCompleteSignal.PhaseOceanHandoff` as soon as cloud whiteout reached the threshold. With the new world handoff loader, that could be interpreted as final prologue completion before `AwaitableDropSequenceDirector` had executed manual release, impact sync, hydration wait, velocity zero, and authoritative ocean handoff.
Solution: Make orbital completion a whiteout-only signal: `PhaseWhiteout` from `OrbitalRelativityDirector`. `PrologueSequenceRegistryBridge` consumes that only as a standalone `01_ORBIT` manual-release fallback, using a cached scene-mode bool set in `OnEnable`. `PrologueWorldHandoffSceneLoader` now accepts only finite, non-zero `SequenceDirector` `PhaseOceanHandoff`. Early orbital splashdown fluid impulse was removed; `HectonFluidEngine` remains the final splashdown fluid owner through sequence handoff.
Rejected Alternatives: Loading world directly on orbital whiteout, keeping early `FluidImpulseSignal`, using `SceneManager.GetActiveScene()` from the bridge read path, or treating any force-whiteout as final ocean truth.
Scalability potential: Low devices avoid duplicate fluid splash and premature scene load. Middle/high/ultra keep orbital whiteout, manual beat, impact, VFX hydration, and world load separated by source hash rather than timing guesses.
Hardware Impact: Removes one premature fluid impulse path. Adds cached bool/source/hash checks only; expected cost is under 1 us/frame on i3/MX350.

## Decision 17 - Scripted Reentry Burn

Problem: The 10-15 minute orbit defaults moved passive approach to `300 m/s`, but the sequence still required `Mach10` before manual release. Without player thrust, `RunReentryBurnAsync` could stall forever and never consume the orbital whiteout complete signal.
Solution: Add deterministic scripted reentry burn inside `OrbitalRelativityDirector`: acceleration contribution is `scriptedReentryBurnAccelerationMetersPerSecondSq * (1 - distance / reentryStartDistanceMeters)`, clamped continuously. Default `260 m/s^2` was modeled at 60 Hz: `Mach10=684.7s`, whiteout range `687.1s`, whiteout emit `688.3s`, total `11.47 min`.
Rejected Alternatives: Lowering or deleting the Mach10 contract, requiring player input in the intro, tying completion speed to `GlobalQualityWeight`, real gravity/reentry simulation, or adding a second sequence owner.
Scalability potential: Low/middle/high/ultra share the same story truth. Visual richness still scales through presentation offsets, material/VFX cadence, debris quantity, and audio granularity, not through different completion logic.
Hardware Impact: One saturate/rcp/scalar burn term per orbital tick. Expected under 3 us on i3/MX350; saved integration risk compared with real orbital physics is orders of magnitude larger.

## Decision 18 - Whiteout Source Contract

Problem: VFX/audio whiteout-only handling accepted `PrologueCompleteSignal.PhaseWhiteout` from any source and also accepted non-sequence `PhaseOceanHandoff` with force-whiteout. That widened the route and could let unrelated or malformed prologue complete signals alter screen/audio state.
Solution: Restrict whiteout-only complete handling in `PrologueAcousticOrchestrator` and `OrbitalDropReentryVfxController` to `OrbitalRelativityDirector` `PhaseWhiteout` with non-zero sequence and force-whiteout. `SequenceDirector` `PhaseOceanHandoff` remains the only path for splashdown/hydrated fade/world load.
Rejected Alternatives: Keeping permissive "any force-whiteout" handling, duplicating splashdown VFX on orbital whiteout, or adding another global event route.
Scalability potential: Low devices avoid spurious effect transitions. High/ultra can still overdrive the visual/audio finish, but only after the proper source emits the proper phase.
Hardware Impact: Extra branch/hash checks only; no allocation and no meaningful frame cost.

## Decision 19 - Current Verification Wall

Problem: Static checks pass, but full compile proof remains required.
Solution: Ran `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` when CPU was `25.5%` and no `dotnet/csc` was active. Build failed outside prologue at `Assets/Candice AI for Games/Scripts/Libs/Candice Save System/Overrides/CandiceSQLiteProvider.cs`: missing `Mono.Data` namespace and `SqliteDataReader`. The build also reports broad MapMagic type-conflict warnings. A second narrow error-only compile was not launched because CPU later measured `90.2%`.
Rejected Alternatives: Editing vendor save-system references from the prologue domain, using compile noise as proof of prologue failure, or violating the CPU gate for a second build.
Scalability potential: No runtime behavior changes. Unity import/Console/Play Mode/profiler proof remains pending after the external vendor reference wall is resolved.
Hardware Impact: No runtime impact. Verification debt is infrastructure-level, not a measured prologue hot-path regression.

## Decision 20 - Route Evidence Hygiene

Problem: After adding `01_ORBIT` to the new-game route, active proof surfaces still had stale expectations: an editor smoke tester expected `LoadScene(targetSceneName)`, active route docs described direct menu-to-world flow, and the scene initially carried placeholder script GUIDs before generated Unity meta GUIDs existed.
Solution: Update the editor smoke test to assert `ResolveStartSceneName(isNewGame)`, `newGameTargetSceneName = "01_ORBIT"`, runtime scene service use, and `sceneService.LoadScene(sceneName)`. Update active first-20-minutes/modding route docs to `00_BOOTSTRAP -> 01_MAIN_MENU -> 01_ORBIT -> 02_HECTON_WORLD` for new game while preserving load-game direct-to-world semantics. Keep generated `.meta` GUIDs and bind `01_ORBIT.unity` to the actual bootstrap GUID. YAML alignment verified: `m_RootGameObject` is absent because these are scene files, but `m_Roots`, `GameObject`, Main Camera FileID `1823536641`, bootstrap component FileID `1823536646`, script GUID `444968fbfc5a4eb291617d5798a39dce`, and `newGameTargetSceneName: 01_ORBIT` are present and aligned.
Rejected Alternatives: Rewriting historical log entries, leaving active docs/tests stale, changing `SceneRuntimeService` only to satisfy prose, or using hand-invented GUIDs in a Unity scene.
Scalability potential: Low/middle/high/ultra all test the same route surface. Load-game remains a cheap resume path; new-game earns the full orbital visual budget.
Hardware Impact: Editor/docs/GUID hygiene only. Runtime cost remains unchanged: cold scene bootstrap plus prologue-only signal handoff.

## Decision 21 - Final Static Boundary

Problem: Whole-repo `git diff --check` reports trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md`, which is outside the `13pro` prompt and not caused by this pass. CPU is also above the AGENTS compile threshold.
Solution: Treat whole-repo whitespace as external proof noise, run scoped checks for touched 13pro files instead, and do not launch another build while CPU is `57.6-99.8%`.
Rejected Alternatives: Editing `CURRENT_BATCH.md` from this domain, rewriting other agents' old route logs, or launching `dotnet build` under the >50 percent CPU ban.
Scalability potential: Keeps route evidence accurate without contaminating other domains or shared build resources.
Hardware Impact: No runtime impact. Verification remains PENDING: external Candice SQLite compile wall and Unity Play Mode/profiler proof are still unresolved.

## Decision 22 - Orbit Is Not Gameplay World Activation

Problem: `GameBootstrapper.RequiresGameplaySceneActivation(Scene scene)` excluded only bootstrap and main menu. After routing new game into `01_ORBIT`, that made the orbit prologue look like `02_HECTON_WORLD` to bootstrap activation, risking premature player disable, world generation, save/load setup, world-ready gates, and cleanup before capsule handoff.
Solution: Add `OrbitSceneName = "01_ORBIT"`, `IsOrbitScene(Scene scene)`, and exclude it from `RequiresGameplaySceneActivation`. The orbit scene remains a prologue scene; the real gameplay activation still starts when the guarded world handoff loads `02_HECTON_WORLD`.
Rejected Alternatives: Letting world runtime wake during the 10-15 minute orbit, adding a per-frame scene poller, special-casing the prologue inside world systems, or bypassing `ISceneService`.
Scalability potential: Low devices avoid running world bootstrap load while showing orbital visuals. Middle/high/ultra spend prologue frame budget on Aegir/Hecton/reentry presentation, not hidden world activation. World truth ownership stays in the world scene.
Hardware Impact: Adds one cold string comparison in the scene activation guard. Prevents a high-ms world activation class from overlapping the prologue on i3/MX350.

## Decision 23 - Final Handoff Context Refresh

Problem: `GameStartContextHolder` persists handoff snapshots for 45 seconds, but the modeled prologue reaches whiteout at about 688.3 seconds. Normal static memory survives scene load, but the persisted recovery snapshot can be stale by the time `01_ORBIT` loads `02_HECTON_WORLD`.
Solution: `PrologueWorldHandoffSceneLoader` now calls `GameStartContextHolder.TryGetCurrentOrRestore` and re-`SetCurrent` once, immediately before `ISceneService.LoadScene`. This refreshes the cold handoff timestamp only at final transition.
Rejected Alternatives: Raising the global PlayerPrefs TTL to 15-20 minutes, writing PlayerPrefs every frame or every few seconds during prologue, or introducing a second session context owner.
Scalability potential: Low/middle/high/ultra keep the same session truth route. Prologue duration can remain cinematic without stale handoff recovery at the actual world transition.
Hardware Impact: One cold PlayerPrefs write/save at final whiteout handoff. No per-frame cost and no DTO/layout change.

## Decision 24 - Verification Boundary After Seventh Pass

Problem: The activation/context fixes need compile and Play Mode proof, but the shared machine is still over the explicit build gate and has active compiler processes.
Solution: Ran scoped static checks only: `git diff --check` over touched 13pro files, trailing-whitespace scan for untracked new files, route string scan, and process/CPU gate. CPU was `70.2%` and `dotnet/csc` count was `7`, so no build was launched.
Rejected Alternatives: Starting another compile under load, editing external vendor save-system references from the prologue domain, or claiming runtime proof without Unity.
Scalability potential: Static route/activation contracts are now coherent for low, middle, high, and ultra. Runtime/perf proof remains pending, not invented.
Hardware Impact: No runtime impact from verification. Compile/Play Mode/profiler proof remains blocked by shared-machine state and existing non-13pro compile walls.

## Decision 25 - Camera-Local Reentry Overlay

Problem: `01_ORBIT` had a material and shader for orbital reentry plasma, but no authored or runtime-bound renderer surface for the `OrbitalDropReentryVfxController`. The controller was added by bootstrap with `null` overlay/window/material bindings, so plasma/whiteout could update state, audio, ambient, and globals while never drawing a visible full-screen effect.
Solution: Serialize `MAT_OrbitalDropReentryPlasma` on `PrologueOrbitSceneBootstrap`, cold-create a camera-local quad named `__HECTON_REENTRY_PLASMA_OVERLAY`, bind it to `OrbitalDropReentryVfxController`, scale it from camera FOV/aspect during active presentation, and upload plasma uniforms through one reusable `MaterialPropertyBlock` instead of mutating the shared material asset.
Rejected Alternatives: `Resources.Load` or raw runtime Addressables for the material, adding particle simulation, making Hecton/Aegir renderers double as plasma windows, broad scene YAML authoring of a permanent overlay object, or cloning materials to hide shared asset mutation.
Scalability potential: Low uses one transparent quad and material fake. Middle keeps aspect-safe whiteout without particles. High can spend quality on richer shader noise and longer burn visuals. Ultra can overdrive shader complexity/material response on the same overlay route without changing gameplay truth or DTO layout.
Hardware Impact: Cold setup adds one GameObject, Mesh, MeshFilter, MeshRenderer, and one MPB. Active hot cost is FOV/aspect scalar math plus property-block upload only when values change, estimated under 2 us/frame on i3/MX350 during active reentry and 0 us while idle due existing presentation-state gate.

## Decision 26 - Remove Unowned 3D Noise Dependency

Problem: `Hecton_OrbitalDropReentryPlasma.shader` sampled `_HectonPrebakedVectorNoise3D`, but repository search found no runtime owner publishing that global texture. The new visible overlay could therefore depend on undefined texture binding behavior in URP/player builds.
Solution: Remove the Texture3D declaration/sample and replace it with deterministic procedural `Hash21` cell noise inside the fragment path. The shader keeps the same material contract and still blends with `_SharedNoiseTex`, Voronoi cells, altitude scatter, and continuous quality pressure.
Rejected Alternatives: Adding a runtime Texture3D publisher, serializing a new noise asset, using `Resources.Load`, or keeping a missing global texture because it might work in Editor.
Scalability potential: Low avoids an extra texture fetch and missing-resource risk. Middle keeps stable plasma grain. High/Ultra can spend quality on shader math or authored 2D noise without adding a global 3D resource owner.
Hardware Impact: Removes one 3D texture sample from every active overlay fragment and replaces it with simple hash math. Expected gain depends on GPU, but low-end/mobile-class hardware benefits from dropping an unbound 3D sampler and a texture-cache path.

## Decision 27 - Final Verification Boundary

Problem: The final patch set still requires Unity import, shader import, and Play Mode proof, but the current shared-machine gate forbids compilation.
Solution: Run scoped static proof only: targeted `rg`, scoped `git diff --check`, scoped `git status`, and CPU/process gate. Latest gate is `100%` CPU with `9` `dotnet/csc` processes.
Rejected Alternatives: Launching `dotnet build` under explicit AGENTS violation, editing unrelated vendor/project-reference compile walls, or claiming runtime visibility without Unity.
Scalability potential: Code paths are prepared for low, middle, high, and ultra by continuous quality and fake-first rendering, but runtime claims stay pending until import/profiler evidence exists.
Hardware Impact: No runtime impact from verification. The build machine is saturated; proof remains static.

## Decision 28 - Camera-Safe Overlay Distance And No MPB On Standard Geometry

Problem: The camera-local plasma quad was initialized at `0.08m`, while the `01_ORBIT` Main Camera serializes near clip `0.3m`. The overlay could therefore be correctly created and still be clipped before rasterization. The same pass exposed a rule violation in the previous overlay upload path: `MaterialPropertyBlock` on a MeshRenderer standard geometry surface conflicts with the active URP/SRP Batcher mandate.
Solution: Raise the default overlay distance to `0.35m`, clamp the runtime overlay distance against `camera.nearClipPlane + 0.03m`, and record the effective distance in VFX telemetry. Remove MPB usage from `OrbitalDropReentryVfxController`; dynamic plasma state now uses two dirty-gated global shader vectors `_HectonReentryPlasmaState0/1`, while the material keeps only static authored color/noise controls.
Rejected Alternatives: Lowering the camera near clip and risking depth precision, keeping `0.08m`, cloning a runtime material, retaining MPB because the overlay is only one quad, or adding a full URP renderer feature for a single prologue whiteout fake.
Scalability potential: Low uses one near-plane-safe quad with no per-renderer property block. Middle keeps aspect-safe whiteout. High adds richer shader math through the same global vector state. Ultra can overdrive shader complexity without new gameplay truth or per-renderer material state.
Hardware Impact: Removes MPB allocation/storage and property-block upload from the active path. Adds two `Shader.SetGlobalVector` calls only when state changes, plus scalar near-clip distance math. Expected cost remains microsecond-level on i3/MX350, with less SRP Batcher risk.

## Decision 29 - Verification Boundary After Overlay Correction

Problem: The correction touches C# and shader code and needs Unity import, shader import, Play Mode, and profiler proof.
Solution: Static proof only: `rg` shows no `MaterialPropertyBlock`, `SetPropertyBlock`, `renderer.material`, runtime `Resources.Load`, 3D sampler, or `_HectonPrebakedVectorNoise3D` in the patched overlay path. Scoped `git diff --check` passes with CRLF normalization warnings only. Build gate measured CPU `30.5%` but one active `dotnet` process, so no build was launched.
Rejected Alternatives: Running `dotnet build` while another dotnet process is active, claiming shader import success without Unity, or editing unrelated compile walls from the prologue domain.
Scalability potential: Static contracts now align for low, middle, high, and ultra. Runtime quality/perf claims remain PENDING VERIFICATION until Unity/Profiler evidence exists.
Hardware Impact: No runtime impact from verification. Proof debt remains external to this static pass.

## Decision 30 - Scene-Owned Runtime Root Must Not Use HideFlags.DontSave

Problem: `PrologueOrbitSceneBootstrap` created `__HECTON_PROLOGUE_ORBIT_RUNTIME` with `HideFlags.DontSave`. Unity's `HideFlags.DontSave` documentation states that such an object is not destroyed when a new scene is loaded. In this route, that can leave `OrbitalRelativityDirector`, `PrologueSequenceRegistryBridge`, VFX, and world handoff logic alive after `01_ORBIT -> 02_HECTON_WORLD`.
Solution: Set the scene-owned runtime root to `HideFlags.None` for both newly-created and already-found roots. Keep only the tiny shared mesh as `DontSaveInEditor | DontSaveInBuild`, which avoids asset persistence without creating a scene-load survivor.
Rejected Alternatives: Keeping the hidden persistent root, using `DontDestroyOnLoad`, relying on `DontSave` to mean only "not serialized", adding cleanup polling in world, or broad-fixing unrelated hide-flag patterns outside the 13pro domain.
Scalability potential: Low devices avoid duplicate prologue late-frame work competing with world systems. Middle/high/ultra keep the prologue overkill budget isolated to `01_ORBIT`; world presentation and gameplay authority remain separate after handoff.
Hardware Impact: Prevents a potential duplicate active prologue stack in the world scene. Patch adds no hot-path cost; it removes a possible multi-system leak after scene load.

## Decision 31 - Verification Boundary After Lifetime Correction

Problem: The lifetime correction requires Unity Play Mode proof to confirm scene unload behavior and no survivor root, but the compiler gate is closed.
Solution: Static proof only: scoped scans show no `HideFlags.DontSave`, `HideAndDontSave`, `DontDestroyOnLoad`, MPB, runtime `Resources.Load`, or unowned 3D texture dependency in the prologue overlay/bootstrap path. Scoped `git diff --check` passes with CRLF normalization warnings only. CPU measured `45.7%`, but two active `dotnet` processes were running, so `dotnet build` remains forbidden.
Rejected Alternatives: Launching compile while other `dotnet` processes are active, claiming Unity unload proof from source scans, or editing unrelated hide-flag hits in other domains.
Scalability potential: Static route now better separates low, middle, high, and ultra prologue presentation from world runtime. Runtime verification remains PENDING until Unity import, Play Mode, and profiler evidence exist.
Hardware Impact: No runtime impact from verification. Remaining proof debt: Unity import, Console, Play Mode through `01_ORBIT`, no survivor root after world load, and GC/profiler capture.

## Decision 32 - Manual Release Must Be Real Before Watchdog Fallback

Problem: `01_ORBIT` contains no authored `OpenXRManualOverrideLever`, while `PrologueSequenceRegistryBridge` accepted `OrbitalRelativityDirector` `PhaseWhiteout` as an immediate manual-complete signal. That avoids a hard hang but collapses the manual capsule release beat into an automatic whiteout continuation.
Solution: Keep direct `ManualOverrideLever` complete support, but add a standalone orbit input path inside the bridge: discrete `PlayerInputSignal` commands `Interact`, `PrimaryAction`, or `SecondaryAction` synthesize the manual release completion with a `0.4s` whiteout hold. Orbital whiteout is now captured and released only after a 180-frame watchdog delay.
Rejected Alternatives: Adding a direct `Hecton8.UI.VR` dependency to the prologue bridge, reflection-based lever creation, removing the fallback and risking an infinite prologue, or leaving the manual stage as instant auto-complete.
Scalability potential: Low devices use the same one-button/manual-release contract and a cheap watchdog. Middle/high/ultra can author richer VR lever visuals later without changing the prologue sequence contract.
Hardware Impact: No cost outside the manual stage. During manual stage the bridge scans existing frame snapshots only; expected cost is under 2 us/frame on i3/MX350 for the short release window.

## Decision 33 - Verification Boundary After Manual Release Patch

Problem: Manual-release routing touches a core prologue bridge and needs compile/Play Mode proof, but the build gate is closed.
Solution: Static proof only: scoped `rg` confirms the new input commands and the 180-frame fallback; scoped `git diff --check` passes with CRLF normalization warnings only. CPU measured `62.7%` and no `dotnet/csc` processes were active, so `dotnet build` remains forbidden by the >50% CPU rule.
Rejected Alternatives: Running a compile under the explicit CPU ban, claiming input proof without Play Mode, or editing the unrelated UI/VR lever haptic implementation from the 13pro prologue domain.
Scalability potential: The manual-release path is now contract-safe for low, middle, high, and ultra tiers; richer physical lever work can layer on top through the existing signal contract.
Hardware Impact: Verification adds no runtime cost. Runtime proof remains PENDING: Unity import, Play Mode input test through `01_ORBIT`, and profiler/GC capture.

## Decision 34 - Impact Handoff Must Wait For Near-Surface State

Problem: After the manual-release path became real, `AwaitableDropSequenceDirector.RunImpactSyncAsync` still waited exactly one frame before hydration/ocean handoff. With current modeled defaults, Mach10 happens at about `684.65s` and `13893.5m`, so immediate player release could load the world while the capsule is still high above Hecton.
Solution: Replace one-frame impact sync with a bounded near-surface wait. The sequence now samples the existing orbital snapshot and atmospheric reentry signals, waits until nearest distance/altitude is <= `120m` after a `0.65s` minimum hold, and uses an `8s` watchdog only if the distance route is missing.
Rejected Alternatives: Blocking manual input until orbital whiteout, adding a scene collision/raycast dependency, using real physics impact, deleting the watchdog, or leaving a teleport-to-water seam.
Scalability potential: Low devices pay only scalar snapshot reads during the short post-release window. Middle/high/ultra can add richer capsule shake/plasma visuals while the same distance gate owns handoff truth.
Hardware Impact: Added cost is one orbital snapshot read plus optional atmospheric signal scan per impact-sync frame for about `3.6s` in the current model; expected under 3 us/frame on i3/MX350. It prevents a visible kilometers-high scene-load seam.

## Decision 35 - Verification Boundary After Impact Sync Patch

Problem: The sequence patch needs compile and Play Mode proof through manual release and world handoff, but the build gate is closed.
Solution: Static and numeric proof only. Scoped `git diff --check` passes with CRLF normalization warnings only. PowerShell 60 Hz model with current defaults gives `Mach10=684.65s`, `Mach10Distance=13893.5m`, `Whiteout/ImpactReady=688.25s`, `ImpactDistance=81.1m`, and `ManualToImpactHold=3.6s`. CPU measured `100%` with `8` active `dotnet` processes, so no build was launched.
Rejected Alternatives: Compiling under active dotnet contention, claiming Unity Play Mode proof from a scalar model, or broad-editing world/ocean systems from the prologue domain.
Scalability potential: The timing contract is now deterministic and independent of quality tier; low, middle, high, and ultra alter presentation only.
Hardware Impact: No runtime impact from verification. Runtime proof remains PENDING: Unity import, `01_ORBIT` manual release, near-surface impact wait, ocean handoff, no survivor root, and profiler/GC capture.

## Decision 36 - Impact Sync Telemetry Must Be Frame-State, Not Event Spam

Problem: The near-surface impact-sync loop sampled orbital and atmospheric data, then recorded separate black-box entries for each sample plus a generic wait entry in the same frame. That reduces the diagnostic value of the fixed 300-entry black box during the exact seam where distance, whiteout, and world handoff can fail.
Solution: `TryResolveImpactRangeReached` now returns a single state hash and flag byte for the frame. `RunImpactSyncAsync` records one `ImpactSync` entry after both distance routes are sampled. Atmospheric data wins the frame hash when present because it is the closest descent-state signal; orbital data remains the fallback.
Rejected Alternatives: Expanding the telemetry DTO, changing `IPrologueSequenceRuntime`, adding a second black-box ring, or accepting duplicate DataVault writes during the short impact window.
Scalability potential: Low keeps the cheapest scalar gate and one diagnostic write per frame. Middle/high/ultra can still add richer VFX/audio/camera presentation around the same handoff without changing truth ownership or telemetry layout.
Hardware Impact: Removes up to two DataVault write-lock/write/unlock cycles per impact-sync frame. Expected savings are 2-6 us/frame on i3/MX350 for the modeled post-release window, plus cleaner crash evidence.

## Decision 37 - Verification Boundary After Impact Telemetry Coalescing

Problem: The telemetry patch touches active prologue sequence code and needs compile/Unity proof, but the shared machine is above the explicit build gate.
Solution: Static proof only. `rg` confirms a single `RecordStage(PrologueStage.ImpactSync, impactStateHash, impactFlags)` path in `RunImpactSyncAsync`, `TryResolveImpactRangeReached` no longer records normal impact entries, and `OnValidate` clamps the impact sync inspector fields. Scoped `git diff --check` passes with CRLF normalization warning only. CPU measured `92.1%` with no compiler process, so build remains forbidden by the >50% rule.
Rejected Alternatives: Running `dotnet build` under CPU violation, claiming Unity import proof from static scans, or widening the patch outside the 13pro prologue domain.
Scalability potential: Diagnostics now scale cleanly across low, middle, high, and ultra because quality affects presentation only; the handoff proof route remains one owner and one frame-state record.
Hardware Impact: No runtime impact from verification. Runtime proof remains PENDING: Unity import, Play Mode through manual release/impact/handoff, GC/profiler capture, and no survivor runtime root after world load.

## Decision 38 - Plasma Material Must Match Shader Contract

Problem: `MAT_OrbitalDropReentryPlasma` still serialized `_PlasmaLowTier: 1`, but `Hecton_OrbitalDropReentryPlasma.shader` no longer declares that property. The same material did not serialize `_PlasmaQualityPressure`, so Unity would use the shader default `1` as authored baseline, biasing high-end preview toward survival-pressure flattening before runtime globals dominate.
Solution: Replace the stale `_PlasmaLowTier` float with `_PlasmaQualityPressure: 0` in the material. This keeps the material high-fidelity by default while runtime global shader vectors still drive continuous quality pressure during actual descent.
Rejected Alternatives: Runtime material cloning, shader-side compatibility for a dead property, `Resources.Load`, or leaving stale YAML because it is not a compiler error.
Scalability potential: Low still receives runtime quality pressure from `_HectonReentryPlasmaState1`. Middle/high/ultra start from a visual-overkill material baseline and scale down continuously through runtime state instead of being flattened by a stale default.
Hardware Impact: No hot-path cost. Removes one stale serialized float and avoids unnecessary high-tier color flattening in material preview/import state.

## Decision 39 - Verification Boundary After Plasma Material Patch

Problem: The material YAML patch needs Unity material/shader import proof, but the build gate is closed.
Solution: Static proof only. `rg` confirms `_PlasmaLowTier` is absent and `_PlasmaQualityPressure` exists in both shader and material. `Select-String` confirms material name, shader GUID `ed0a893349281084383651b4a57938cd`, and `_PlasmaQualityPressure: 0`. Scoped `git diff --check` passes. CPU measured `100%` with `2` compiler processes, so build remains forbidden.
Rejected Alternatives: Running build under CPU/process violation, broad asset reauthoring, or claiming Unity import proof without the Editor.
Scalability potential: Material state now matches low, middle, high, and ultra runtime quality scaling: one authored high-fidelity baseline, continuous pressure applied by runtime.
Hardware Impact: No runtime impact from verification. Runtime proof remains PENDING: Unity shader/material import, scene render, and profiler/GC capture.

## Decision 40 - Orbital Black Box Requires Vault Writer Lock

Problem: `OrbitalRelativityDirector.RecordTelemetry()` resolved the orbital telemetry ring with `TryResolveHandle` and wrote into it directly. The active memory doctrine requires mutable vault resolutions to be fenced, especially for a black-box crash system that must survive compaction/relocation pressure.
Solution: Write the orbital telemetry ring through `TryAcquireWriteLock(in _telemetryRingHandle, OwnerSystemId, out NativeArray<OrbitalTelemetryEntry>)` and release in a `finally`. Dump reads now use `TryReadOnlyHandle`, not a mutable alias.
Rejected Alternatives: Keeping unlocked `TryResolveHandle` because telemetry is small, moving the ring into a persistent `NativeArray` field on the MonoBehaviour, adding a second local ring, or fixing unrelated vault users outside the 13pro domain.
Scalability potential: Low devices get correct crash proof under memory pressure. Middle/high/ultra can produce richer orbital visuals while the same black-box route stays deterministic and relocation-safe.
Hardware Impact: Adds one owner writer-fence acquire/release around the existing orbital telemetry write. Expected cost is microsecond-level per tick; the trade is accepted because an unlocked crash ring is false evidence.

## Decision 41 - Verification Boundary After Orbital Telemetry Lock Patch

Problem: The telemetry lock patch touches active orbital runtime code and needs compile/Play Mode proof, but the build gate is closed.
Solution: Static proof only. `rg` confirms `RecordTelemetry` uses `TryAcquireWriteLock` and `ReleaseWriteLock`, and `DumpTelemetry` uses `TryReadOnlyHandle`. `TryResolveTelemetryRing` is gone. Scoped `git diff --check` passes with CRLF warning only. CPU measured `37.7%`, but one active `dotnet` process exists, so `dotnet build` remains forbidden.
Rejected Alternatives: Running compile while another dotnet process is active, claiming vault-lock proof from prose, or widening the pass into unrelated vault aliases.
Scalability potential: The telemetry route is now safer across low, middle, high, and ultra because memory pressure does not change how diagnostics own mutable buffers.
Hardware Impact: No runtime proof yet. Static expected delta is one lock acquire/release per orbital telemetry write; correctness gain is crash-dump integrity.

## Decision 42 - Final Verification Boundary

Problem: The current pass changed runtime C#, material YAML, and logs. Unity import, Console, Play Mode, and profiler proof are required, but the project build gate still blocks a local compile launch.
Solution: Final scoped `git diff --check` over touched 13pro files passes with CRLF normalization warnings only. `rg` confirms `TryResolveTelemetryRing` is absent from `OrbitalRelativityDirector`. Build was not launched because CPU measured `51.6%` with `0` compiler processes; the explicit rule forbids builds above 50% CPU.
Rejected Alternatives: Launching `dotnet build` at 51.6% CPU, editing unrelated compiler/vendor walls, or claiming runtime proof from static checks.
Scalability potential: Static code now supports the prologue route across low, middle, high, and ultra via visual fakes, continuous quality pressure, locked diagnostics, and deterministic handoff gates. Runtime proof remains separate and pending.
Hardware Impact: No measured runtime impact. Proof debt remains: Unity import/Console, Play Mode `01_ORBIT -> 02_HECTON_WORLD`, no survivor prologue root, dump validation, and profiler/GC captures.

## Decision 43 - Standalone Orbit Handoff Proxy Is Not Low Tier

Problem: `AwaitOceanHydrationAsync` used `_runtime.IsLowTier` as the only permission to accept an ocean proxy. In standalone `01_ORBIT`, `02_HECTON_WORLD` is intentionally not loaded yet, so a high-quality run with `GlobalQualityWeight=1` could wait forever for high-resolution ocean residency that cannot exist before the handoff signal.
Solution: Add explicit contract surface `IsStandaloneOrbitHandoffProxyAllowed` and hydration mode `StandaloneOrbitHandoffProxy`. The sequence now checks high-resolution ocean readiness first, then permits the standalone proxy only when the bridge confirms it is running in `01_ORBIT`. Quality pressure still controls survival proxy admission, not handoff truth ownership.
Rejected Alternatives: Treating all standalone proxy use as `LowTierProxySurface`, loading the world before the sequence-owned handoff, unconditional proxy acceptance in every scene, or tying scene progression to `GlobalQualityWeight`.
Scalability potential: Low and middle can use the proxy to hide streaming without changing player truth. High and ultra still prefer high-resolution readiness when it exists, but standalone orbit is allowed to finish because the proxy is a scene bridge, not a quality downgrade.
Hardware Impact: Adds one branch and one property read in the hydration wait loop. Expected cost under 1 us/frame on i3/MX350; prevents an infinite route stall.

## Decision 44 - Verification Boundary After Standalone Proxy Patch

Problem: The proxy contract patch changes core contracts and narrative sequence code and needs compiler/Unity proof.
Solution: Static proof only. `rg` confirms `StandaloneOrbitHandoffProxy`, `IsStandaloneOrbitHandoffProxyAllowed`, and explicit `IsOceanSurfaceReady(allowProxy: false/true)` calls. Scoped `git diff --check` passes with CRLF normalization warnings only. Build was not launched because CPU measured `100%` and `8` active `dotnet` processes were present.
Rejected Alternatives: Running compile under explicit host contention, claiming Play Mode proof from source, or changing unrelated world streaming ownership.
Scalability potential: The route now scales across low, middle, high, and ultra because quality may alter proxy admission only for memory pressure; the standalone scene bridge remains scene-owned.
Hardware Impact: No runtime measurement. Proof debt remains: Unity compile/import and Play Mode through high-quality `01_ORBIT` hydration.

## Decision 45 - Reentry VFX Black Box Must Follow DataVault Replacement

Problem: `OrbitalDropReentryVfxController` registered for registry hot-swap events but only handled Dispatcher replacement. If `DataVault` is replaced, the VFX black-box handle can remain bound to the old vault, producing stale or invalid telemetry during the reentry/handoff seam.
Solution: Handle `GlobalRegistryServiceSlot.DataVault` by releasing the old telemetry buffer, updating `_dataVault`, resetting the cursor, and reacquiring telemetry storage from the new vault. `OnDestroy` now uses the same release helper.
Rejected Alternatives: Assuming scene unload always precedes DataVault replacement, keeping a stale handle until destroy, or duplicating a local NativeArray outside Vault ownership.
Scalability potential: Low devices under memory pressure and high/ultra routes under richer VFX now keep one telemetry owner route. The visual overkill path does not weaken crash evidence.
Hardware Impact: No normal-frame cost. Replacement-only path adds one release and one cold reacquire.

## Decision 46 - VFX Telemetry Writer Fence Must Release On Every Exit

Problem: The VFX telemetry path acquired a Vault write lock, then had a manual early release for invalid arrays and a narrower `finally` only around the final write. Future edits or unexpected exceptions between acquire and write could leave a writer fence unreleased.
Solution: Wrap the entire post-acquire telemetry path in a single `try/finally` and release through `vault.ReleaseWriteLock` for all exits.
Rejected Alternatives: Leaving the existing manual release because current code was small, moving telemetry out of the Vault, or accepting a stuck writer fence in a crash diagnostic system.
Scalability potential: Low, middle, high, and ultra all retain deterministic black-box behavior. Higher VFX richness does not increase lock-leak risk.
Hardware Impact: Same writer fence count as before; no meaningful hot-path cost change. Correctness gain is fenced telemetry integrity under failure.

## Decision 47 - Route Wiring Static Scan Is Evidence, Not Runtime Proof

Problem: The prologue route can fail even when isolated systems compile if BuildSettings, main-menu target, orbit bootstrap, or gameplay activation filters disagree.
Solution: Performed a scoped static route scan. `ProjectSettings/EditorBuildSettings.asset` includes `00_BOOTSTRAP`, `01_MAIN_MENU`, `01_ORBIT`, and `02_HECTON_WORLD`; `01_MAIN_MENU.unity` serializes `newGameTargetSceneName: 01_ORBIT`; `01_ORBIT.unity` serializes `PrologueOrbitSceneBootstrap` with matching script GUID; `GameBootstrapper.RequiresGameplaySceneActivation` excludes `01_ORBIT` from world gameplay activation.
Rejected Alternatives: Rewriting scene flow after no static route contradiction was found, or claiming Play Mode proof from YAML/source scans.
Scalability potential: Route ownership remains stable for low, middle, high, and ultra; quality affects presentation and proxy readiness, not the new-game scene path.
Hardware Impact: No runtime impact. Remaining proof debt is Play Mode route execution and profiler capture.

## Decision 48 - Compile Wall Is Outside 13pro Scope

Problem: The build gate opened after the standalone-proxy and VFX telemetry patches, so a compiler proof attempt was required. `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` failed before producing scoped 13pro proof.
Solution: Treat the compile result as an external wall, not a successful verification. The errors are project-wide and out of the prologue domain: unresolved Odin attributes/namespaces, duplicate `SoaInventoryQueryEngine.OffsetOf`, and missing `BufferID` members across vegetation, wreck, nav, and world contract files. The 13pro changes remain statically verified only.
Rejected Alternatives: Editing unrelated vendor/package, inventory, vegetation, wreck, nav, or world-contract code from the prologue lane; rerunning the same build without a scoped fix; or claiming compile success from `git diff --check`.
Scalability potential: No scalability claim changes. Low, middle, high, and ultra prologue behavior remains a static contract until Unity import, Play Mode, and profiler evidence exist.
Hardware Impact: No runtime measurement. Build proof is blocked by out-of-domain compilation failures; the exact 13pro frame cost remains estimated, not measured.

## Decision 49 - Dispatcher Replacement Must Rebind Prologue Presentation Lanes

Problem: `PrologueAcousticOrchestrator` and `OrbitalDropReentryVfxController` handled Dispatcher service replacement without clearing their late-frame registration flags. After a Dispatcher replacement, they could believe they were still registered while the new dispatcher lane did not contain them.
Solution: Rebind on Dispatcher replacement by clearing the registration flag and registering against the current dispatcher only when active. `PrologueAcousticOrchestrator` implements both rebound and replaced callbacks, so the rebound callback now only caches the dispatcher reference; the replaced callback owns the actual rebind. Orbital and handoff loaders gained a same-service guard so an idempotent rebound does not corrupt registration flags.
Rejected Alternatives: Polling `GlobalRegistry.Dispatcher` every frame, leaving stale registration flags, or assuming Dispatcher replacement cannot happen during the prologue.
Scalability potential: Low, middle, high, and ultra keep audio/VFX/orbital presentation tied to the dispatcher lane instead of silently losing VISUAL_SYNC work after service replacement.
Hardware Impact: No normal-frame cost. Replacement-only work is one flag reset plus one bounded registration call per active owner.

## Decision 50 - Rebound And Replacement Are Two Calls, Not One

Problem: `GlobalRegistry.NotifyHotSwapListeners` invokes `OnGlobalRegistryServiceRebound` and then `OnGlobalRegistryServiceReplaced` for ref listeners. A naive rebind in both callbacks can attempt duplicate registration, causing `RegistryBucket.TryRegister` to reject the second call and leave the owner's local "registered" flag false while the object remains in the lane.
Solution: `PrologueAcousticOrchestrator.OnGlobalRegistryServiceRebound` now only caches the dispatcher. `OnGlobalRegistryServiceReplaced` performs the rebind and skips work when previous and current service references are identical.
Rejected Alternatives: Relying on `RegistryBucket.Contains` duplicate rejection as lifecycle control, adding unregister calls against the current registry for a previous dispatcher, or disabling ref-listener support.
Scalability potential: Stable dispatcher registration prevents presentation systems from degrading unpredictably on all hardware tiers.
Hardware Impact: Prevents stale-lane leaks and no-op `LateFrameTick` calls after dispatcher churn. Runtime overhead remains unchanged outside replacement events.

## Decision 51 - Production Orbit Route Requires A Route Card

Problem: The new-game scene path now includes `01_ORBIT`, while the older authority text in `AGENTS.md` still describes direct `01_MAIN_MENU -> 02_HECTON_WORLD`. Without a route card, the change is evidence-poor and can be rejected as an undocumented global route mutation.
Solution: Added `Docs/ARCHITECTURE/PROLOGUE_ORBIT_HANDOFF_ROUTE_CARD_13PRO.md` and linked it in `GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`. The route card states owner, instrument, producer/consumer phase, cadence, payload shape, failure mode, telemetry, black-box route, shutdown, scene unload behavior, and proof required before GREEN. Review disposition is explicitly `YELLOW / STATIC_SOURCE_ONLY`.
Rejected Alternatives: Treating chat/status logs as route approval, editing `AGENTS.md` from a domain agent, or claiming GREEN without Unity import, Play Mode, Game View, profiler/GC, and no-survivor-root proof.
Scalability potential: Route ownership is now auditable across low, middle, high, and ultra. Quality still scales presentation only, not scene identity or handoff truth.
Hardware Impact: Documentation-only; 0 runtime cost. It prevents future agents from adding a second route owner or deleting `01_ORBIT` as "unapproved" without seeing the boundary.

## Decision 52 - Verification Boundary After Dispatcher And Route-Card Patch

Problem: Runtime patches and architecture docs need compile/import proof, but the current host violates the build gate.
Solution: Static verification only. Scoped `git diff --check` over audio, VFX, orbital, handoff, route card, and migration ledger passes with CRLF normalization warnings only. Route-card field scan confirms required owner/instrument/phase/cadence/failure/shutdown/proof fields. Build was not launched because CPU measured `100%` and one `dotnet` process was active.
Rejected Alternatives: Launching `dotnet build` under explicit CPU/process ban, marking the route GREEN from static docs, or widening fixes into unrelated compile-wall domains.
Scalability potential: The patch removes hot-swap fragility without adding recurring frame work. Runtime scalability proof remains pending.
Hardware Impact: No measured runtime impact. Expected normal-frame delta is 0 us; replacement-only rebind cost is microsecond-level.

## Decision 53 - Final Gate Remains Static

Problem: After recording the route-card and dispatcher rebind patches, another compile gate check was required so the report does not cite stale host state.
Solution: Rechecked the gate. CPU measured `80%` with `dotnet/csc=0`; the explicit build ban still applies because CPU is above 50%. No second build was launched after the earlier out-of-domain `Hecton8.Core.csproj` wall.
Rejected Alternatives: Running `dotnet build` under CPU violation, or hiding the fact that the final verification state is static-only.
Scalability potential: No new scalability claim. Runtime proof remains pending for all hardware tiers.
Hardware Impact: No runtime measurement. Static expected deltas remain: 0 us normal-frame for dispatcher rebind and 0 us runtime for route-card documentation.

## Decision 54 - Post-Compaction Evidence Refresh

Problem: Context compaction can leave the final chat report citing stale verification state.
Solution: Re-read `Status_13pro.md`, `Rationale_13pro.md`, and the Unity MCP skill, then re-ran scoped `git status`, scoped `git diff --check`, and the CPU/compiler gate. Static diff check still passes with CRLF normalization warnings only. CPU is currently `100%` with `dotnet/csc=0`, so compile remains forbidden by the explicit CPU rule.
Rejected Alternatives: Reporting from memory, launching `dotnet build` at 100% CPU, or claiming runtime/Unity proof from static scans.
Scalability potential: No route or runtime behavior changed. The low/middle/high/ultra scalability claims remain static until Unity import, Play Mode, Game View capture, and profiler proof are collected.
Hardware Impact: No runtime measurement. Verification work is 0 runtime cost; build proof remains blocked.

## Decision 55 - Standalone Orbit Proxy Must Obey `allowProxy`

Problem: `PrologueSequenceRegistryBridge.IsOceanSurfaceReady(allowProxy: false)` returned true for the standalone `01_ORBIT` handoff proxy. `AwaitOceanHydrationAsync` intentionally checks `allowProxy:false` first to prove real high-resolution ocean readiness; the bug collapsed the later `StandaloneOrbitHandoffProxy` path into a fake high-res result.
Solution: Gate the standalone orbit hydration proxy behind `allowProxy && allowStandaloneOrbitHydrationProxy && _standaloneOrbitSceneActive`. High-resolution readiness now remains high-resolution; the standalone proxy is consumed only by the explicit proxy branch.
Rejected Alternatives: Keeping the proxy as a silent high-res substitute, removing the standalone proxy and risking a route stall, or renaming the public `Is*` contract during a narrow defect pass.
Scalability potential: Low and middle still use the proxy to hide world streaming; high and ultra no longer mislabel proxy readiness as high-resolution surface truth.
Hardware Impact: One branch in the hydration wait path, expected under 1 us/frame on i3/MX350. The gain is correctness: no false high-res telemetry and no hidden route-contract ambiguity.

## Decision 56 - Sequence Black Box Must Follow DataVault Replacement Gates

Problem: `AwaitableDropSequenceDirector` owned the sequence black-box ring but did not listen for `DataVault` replacement and did not respect compaction/allocation lock before reacquiring telemetry storage. A stale handle or allocation during a fence would make the crash ring unreliable at the descent handoff seam.
Solution: Register the sequence director as an `IGlobalRegistryHotSwapListener`, release the old black-box handle on DataVault replacement, skip idempotent same-service notifications, reacquire only from the current vault, validate existing handles through read-only access, and refuse allocation when compaction or allocation locks are active.
Rejected Alternatives: Keeping a stale handle until destroy, using a local `NativeArray` outside vault ownership, allocating through compaction pressure, or broad-fixing unrelated black-box owners.
Scalability potential: Low devices under memory pressure and high/ultra cinematic routes share the same deterministic black-box ownership. Visual overkill cannot weaken diagnostic ownership.
Hardware Impact: No normal-frame cost outside the existing black-box write path. Replacement-only cost is one release and optional cold reacquire. Prevents false postmortem evidence under service churn.

## Decision 57 - Verification Boundary After Hydration And Sequence Black-Box Patch

Problem: The latest patch touches core sequence readiness and narrative sequence telemetry, so compiler and Unity import proof are required.
Solution: Static proof only. Scoped `git diff --check` passes with CRLF normalization warnings only. Static scans confirm the `allowProxy` gate, `IGlobalRegistryHotSwapListener`, same-service DataVault guard, explicit `TryGetGenerationHandle<PrologueSequenceTelemetryEntry>`, compaction fence, and allocation lock checks. Build was not launched because CPU measured `100%` and `dotnet` process `48912` is active.
Rejected Alternatives: Launching `dotnet build` under the explicit CPU/process ban, claiming Play Mode proof from source scans, or editing unrelated compile-wall domains.
Scalability potential: The route remains scalable across low, middle, high, and ultra because quality controls presentation/proxy admission only; scene handoff truth and black-box ownership are stable.
Hardware Impact: No runtime measurement. Expected delta is under 1 us/frame in hydration wait and 0 us normal-frame for DataVault hot-swap hardening. Runtime proof remains PENDING.

## Decision 58 - Compile Attempt Remains Blocked Outside 13pro

Problem: After the latest static patch, the compile gate opened and compiler proof was required.
Solution: Ran exactly one permitted compile: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` with CPU at `40%` and no active `dotnet/csc`. The build failed on existing out-of-domain project walls: unresolved Odin/Sirenix attributes/namespaces, duplicate `SoaInventoryQueryEngine.OffsetOf`, duplicate `HectonMapMagicVegetationBridge.InvalidateChunksForNewPermanentEchoes`, missing `BufferID` members in world/vegetation/wreck/nav/sargassum contracts, duplicate source include warnings, and assembly version conflicts.
Rejected Alternatives: Editing vendor/Odin references, inventory query engine, vegetation persistence, world BufferID ownership, wreck generation, nav grid, sargassum, or generated project include state from the prologue lane; rerunning the same compile without a scoped fix.
Scalability potential: No runtime scalability claim changes. Low, middle, high, and ultra behavior for 13pro remains static-verified but not compiler/Unity-proven.
Hardware Impact: No runtime measurement. Compile wall prevents profiler/GC proof; latest code deltas remain estimated: under 1 us/frame in hydration wait and 0 us normal-frame for DataVault hot-swap hardening.
