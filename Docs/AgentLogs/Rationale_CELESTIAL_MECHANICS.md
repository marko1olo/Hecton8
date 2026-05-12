# CELESTIAL_MECHANICS Rationale

Status: PENDING VERIFICATION

## Decision 0: Authority And Scope

Problem: Celestial light work can easily mutate weather, player, or AUP systems directly.
Solution: Confine new implementation to Atmosphere/Celestial-facing code and consume external state through existing registry/contracts/signals where source confirms them.
Rejected Alternatives: Direct player/weather concrete dependencies are rejected because parallel agents may rewrite those owners; raw scene searches in runtime are rejected by zero-GC and registry mandates.
Scalability potential: Low uses locked light angle and shader color shifts; Middle uses SlowTick orbit; High adds richer ambient/eclipsed color response; Ultra can spend saved CPU on more expressive shader globals without dynamic shadow spam.
Hardware Impact: i3/MX350 avoids per-frame shadow invalidation and volumetric eclipse. Expected saving target is tens to hundreds of microseconds depending on previous light rotation cadence; measured proof absent.

## Decision 1: Mandate Selection

Problem: The task mixes rendering, weather, AUP, Burst jobs, and telemetry.
Solution: Loaded 8 mandates: visual fake first, zero-GC, native/job discipline, AUP precision, blackbox telemetry, GlobalRegistry, weather fake, and URP hot-path rendering.
Rejected Alternatives: Loading all 60+ mandates is rejected as context pollution; loading only rendering is rejected because the prompt explicitly requires job, registry, AUP, weather, and telemetry behavior.
Scalability potential: Mandates enforce Low/Middle/High/Ultra branches instead of a fixed "balanced" path.
Hardware Impact: Prevents unbounded dynamic light/shader/global work on low-end silicon; estimated guard value 20-80 us per SlowTick by avoiding unnecessary branches and scene lookups.

## Decision 2: Deferred Burst Orbit Snapshot

Problem: Analytical orbit math was deterministic but managed and could be pulled into the SlowTick critical path.
Solution: Added `CelestialOrbitMathJob` with persistent `NativeArray<CelestialOrbitJobOutput>` and late-frame completion through `DispatcherJobSwap`.
Rejected Alternatives: Forced same-frame `Complete()` was rejected because it can stall; per-frame managed orbit evaluation was rejected because the prompt requires SlowTick/Burst ownership.
Scalability potential: Low skips dynamic orbit/light rotation and uses fallback; Middle schedules deferred snapshots; High/Ultra can spend saved CPU on richer moon/gas giant shader response.
Hardware Impact: i3/MX350 avoids sync stalls and per-frame celestial math; expected savings 25-70 us on snapshot ticks.

## Decision 3: Eclipse As Shader/Light Scalar

Problem: Aegir eclipse needs strong visual response without volumetric shadows.
Solution: Kept angular penumbra scalar and pushed light intensity, night blend, ambient probe, and `_HectonAtmosphereColor` from the same eclipse state.
Rejected Alternatives: Volumetric eclipse, dynamic probe bake, and extra fill lights were rejected as too expensive and less controllable.
Scalability potential: Low shows color/ambient fake only; Middle adds stepped shadow/light response; High/Ultra can deepen shader response and biolum multiplier during eclipse.
Hardware Impact: Avoids 500-2000 us volumetric/light volume cost and keeps the work on scalar globals.

## Decision 4: Build Wall Classification

Problem: Full project build failed before clean verification due to missing `SuitStats`/`SuitUpgrades` in `SuitUpgradeManager`.
Solution: Classified Compile Check A as blocked by external dependency; no celestial compiler diagnostics appeared before the wall.
Rejected Alternatives: Editing gameplay suit dependencies is outside ATMOSPHERE & CELESTIAL domain and would be architectural sabotage.
Scalability potential: No runtime impact; preserves domain boundary.
Hardware Impact: None. Verification risk remains until the unrelated gameplay compile wall is cleared.

## Decision 5: Weather And Lightning As Scalar Events

Problem: Storm and lightning visuals can create uncontrolled light churn if treated as real lights.
Solution: Consumed storm snapshots through `WeatherEvents`/`GlobalRegistry`, added additive `WeatherEvents.Lightning`, and drove `_HectonStormCloudDensity` plus `_HectonLightningFlash` as shader scalars with `math.lerp` decay.
Rejected Alternatives: Extra point lights, direct weather director references, and per-strike scene objects were rejected because they cost CPU, allocate, and couple parallel domains.
Scalability potential: Low uses shader-only flashes and storm dimming; Middle adds stronger color response; High/Ultra can amplify shader overkill without adding light sources.
Hardware Impact: i3/MX350 avoids 100-500 us of dynamic light/scene overhead during lightning bursts.

## Decision 6: Abyss And Low-Tier Cull Before Orbit Work

Problem: Surface celestial CPU is wasted below abyssal depth and on weak devices where dynamic shadow changes are visible as spikes.
Solution: Gate abyssal depth before the celestial timeline, and lock low-tier sun rotation at 45 degrees with sky/ambient fakery.
Rejected Alternatives: Always scheduling Burst orbit math and always rotating the Directional Light were rejected because the abyss cannot see the sun and MX350-class hardware cannot afford shadow churn.
Scalability potential: Low skips/locks; Middle runs deferred SlowTick orbit; High/Ultra keep richer eclipse, ambient, and shader response.
Hardware Impact: i3/MX350 saves 50-300 us per affected SlowTick, depending on depth and light update path.

## Decision 7: Gradient Sampling Without Gradient.Evaluate

Problem: The prompt forbids `Gradient.Evaluate`; even a cold cache path would fail static review.
Solution: Packed atmosphere gradients into persistent `NativeArray<float4>` samples via manual key interpolation, then runtime paths use manual lerp only.
Rejected Alternatives: Leaving cold `Gradient.Evaluate` calls was rejected because the directive is literal; sampling every material directly was rejected as hotter and more coupled.
Scalability potential: Low uses 8 packed samples; High/Ultra can increase shader richness while CPU sampling stays flat.
Hardware Impact: Runtime cost remains allocation-free and branch-light; expected gain is 5-20 us versus repeated gradient sampling during LUT/global updates.

## Decision 8: Fixed Blackbox Instead Of Logs

Problem: Celestial NaNs or eclipse timing bugs need post-mortem evidence without string allocation or spam.
Solution: Added a 300-frame `NativeArray<CelestialBlackBoxEntry>` ring with TimeOfDay, EclipseState, flags, depth, storm, and lightning, dumping binary only on invalid numeric state.
Rejected Alternatives: `Debug.Log` per tick and managed history queues were rejected for GC and frame-time instability.
Scalability potential: Low and Ultra share the same fixed memory footprint; richer Ultra visuals do not increase blackbox cost.
Hardware Impact: Constant 300-entry native ring; avoids unbounded logging and keeps crash evidence deterministic.

## Decision 9: Verification Boundary

Problem: The direct core project compiled once, but subsequent verification hit unrelated parallel-agent errors in `World/AcousticOcclusionUtility.cs` and `UI/PDAMapTab.cs`; broader assembly verification also hits unrelated PDA map helpers.
Solution: Recorded the successful direct compile and the later dependency wall; kept CELESTIAL files unchanged except for assigned implementation.
Rejected Alternatives: Editing audio occlusion or PDA/map UI helpers is outside CELESTIAL_MECHANICS authority and would hide another agent's integration issue.
Scalability potential: No runtime impact; preserves domain boundary and keeps compile evidence honest.
Hardware Impact: None.

## OMEGA POLISH CHANGES

Dear Lie Audit:
- Replaced honest celestial simulation pressure with a Burst-friendly circular/cinematic orbit fake, precomputed reciprocals, and deferred late-frame commit.
- Eclipse stays a scalar: angular penumbra drives light intensity, sky blend, ambient probe, water tint, and biolum multiplier. No volumetric eclipse.
- Storm and lightning are shader globals: `_HectonStormCloudDensity` and `_HectonLightningFlash`; no lightning light source.
- Low tier locks sun at 45 degrees and simulates time through sky/ambient color. Abyss below -200m skips celestial CPU.
- Atmosphere gradients are packed to `NativeArray<float4>` and manually lerped; static check found no `Gradient.Evaluate`.

Scalability Matrix:
- Low: locked sun, abyss cull, 8-sample gradient LUT, shader-only weather, fixed blackbox.
- Middle: SlowTick celestial timeline, deferred Burst orbit snapshot, scalar eclipse.
- High: richer ambient/eclipsed color and celestial/moon shader response.
- Ultra: saved CPU buys visual overkill through shader globals, biolum multiplier, and sky occluder response without dynamic light churn.

Omega Static Results:
- `CelestialOrbitMathJob` contains no `Vector3`, `math.sqrt`, `math.normalize`, or direct division matches.
- Touched celestial/weather paths contain no `foreach`, `string.Format`, or `.ToString(` matches under the Omega scan.
- Cross-domain edit justification: `Environment/WeatherEvents.cs` gained an additive `Lightning` event and `RaiseLightning()` so celestial can listen through the existing event bus instead of depending on `HectonSurfaceWeatherDirector`. `HectonSurfaceWeatherDirector.cs` only raises that scalar event at the existing lightning strike point.

Final Git Diff:
- `Assets/_Project/Scripts/HectonCelestialEngine.cs`: SlowTick/Burst orbit fake, LOD gates, abyss cull, weather/lightning globals, packed gradients, ambient SH push, blackbox, biolum eclipse boost.
- `Assets/_Project/Scripts/Environment/WeatherEvents.cs`: additive `WeatherEventType.Lightning`, `RaiseLightning()`, shared enqueue helper.
- `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs`: scalar lightning event raise. Note: file also contains unrelated pre-existing local thunder acoustic changes; CELESTIAL_MECHANICS did not own those.
- `Docs/Tasks/Status_CELESTIAL_MECHANICS.md`, `Docs/AgentLogs/Rationale_CELESTIAL_MECHANICS.md`, `Docs/AgentLogs/RECON_CELESTIAL_MECHANICS.md`, `Docs/AgentLogs/LOG_CELESTIAL_MECHANICS.md`: evidence and reporting.

Diff Stat:
Latest tracked code diff: `Assets/_Project/Scripts/HectonCelestialEngine.cs`, `Assets/_Project/Scripts/Environment/WeatherEvents.cs`, and `Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs` => 3 files changed, 1226 insertions(+), 131 deletions(-).

Verification Status:
- Earlier dependency-wall entries are preserved below as historical checkpoints, not current compile state.
- Latest post-hardening dotnet checks succeeded: `Hecton8.Core.csproj` with 53 non-celestial warnings and 0 errors; `Assembly-CSharp.csproj` with 11 third-party/editor warnings and 0 errors.
- Status remains PENDING only because Unity Console, Play Mode, visual, and profiler evidence are unavailable while MCP console reads fail.

## QUALITY PASS 2026-05-12

Problem: Follow-up audit found two quality risks: lazy `NativeArray` allocation could occur on the first celestial SlowTick/blackbox write, and `_HectonLightningFlash` had two writers (`HectonSurfaceWeatherDirector` and `HectonCelestialEngine`) when celestial was registered.
Solution: Prewarm celestial orbit output, blackbox, and packed atmosphere samples during play-mode `OnEnable`; reset lightning scalar at owner startup. Surface weather now raises `WeatherEvents.Lightning` from the job-driven lightning path and only writes `_HectonLightningFlash` as a fallback when no celestial owner is registered.
Rejected Alternatives: Leaving first-use allocation was rejected as a stealth hitch; letting both systems write the same shader global was rejected as a race. A direct dependency from celestial into surface weather was rejected in favor of the existing event bus.
Scalability potential: Low avoids first SlowTick allocation and keeps shader-only flashes; Middle/High/Ultra get deterministic scalar ownership with richer shader response available without adding lights.
Hardware Impact: i3/MX350 avoids a first-use native allocation hitch and prevents flash-state jitter. Estimated protected cost remains 20-100 us for allocation avoidance and 100-500 us per lightning burst versus dynamic light paths.
Verification: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` succeeded after this pass with 3 unrelated warnings and 0 errors. Static scans found no `Gradient.Evaluate`, no `Vector3`/`math.sqrt`/`math.normalize`/direct division in `CelestialOrbitMathJob`, and no `foreach`/`string.Format`/`.ToString(` in the touched celestial/weather files.

## Decision 10: Verification Upgrade

Problem: Earlier verification was blocked by unrelated parallel-agent compile drift; after the quality pass the project needed a fresh compile proof.
Solution: Re-ran direct core and broader `Assembly-CSharp.csproj` builds. Both compile; broader build reports warnings only. Unity Editor MCP refresh timed out and console read had no active Unity session, so runtime/editor verification is still not claimed.
Rejected Alternatives: Marking VERIFIED from dotnet alone was rejected by AGENTS; Unity Console, Play Mode, profiler, and visual captures are separate evidence.
Scalability potential: No runtime impact; improves evidence quality and removes stale dependency-block status.
Hardware Impact: None.

## Decision 11: Abyss And Fallback Visual Consistency

Problem: When abyss culling returns before the timeline, `_HectonLightningFlash` could remain stale because celestial owns that shader scalar. The fallback/low-tier snapshot also missed the eclipse biolum multiplier applied by the Burst path.
Solution: Abyss cull now clears `_HectonLightningFlash` with one scalar write before returning. Fallback snapshots apply the same eclipse biolum multiplier as the main orbit path.
Rejected Alternatives: Running the full timeline in abyss was rejected because it violates the abyssal decoupling mandate. Leaving the stale flash was rejected as a visual artifact.
Scalability potential: Low/abyss stays cheap and artifact-free; High/Ultra preserve eclipse biolum overkill even when using fallback snapshots.
Hardware Impact: One scalar clear in abyss replaces stale state; expected cost is under 5 us and avoids visible flash residue.
Verification: Final `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` succeeded with 0 warnings and 0 errors. `git diff --check` reported no whitespace errors, only CRLF normalization warnings.

## Decision 12: Listener Cleanup And Active Lightning Cadence

Problem: Destroy-path cleanup had to be explicit for both celestial event lanes, and lightning decay tied only to coarse global update cadence could leave a flash visually sticky after a strike.
Solution: `OnDestroy` now unregisters from `BiomeMatrixEvents` and `WeatherEvents`. Lightning decay runs from `LateFrameTick` only while `_lightningFlash01` is active, and stops shader writes once the scalar reaches epsilon.
Rejected Alternatives: Leaving listener cleanup to disable-only paths was rejected because scene teardown/prefab destruction paths must be deterministic. A real lightning light, per-frame `Update`, or unconditional shader upload was rejected as unnecessary CPU/GPU churn.
Scalability potential: Low pays zero idle cost and only one scalar update while a flash is active. Middle/High/Ultra get sharper cinematic flash decay without dynamic light sources.
Hardware Impact: Prevents retained event listener callbacks and keeps active flash work to one scalar lerp/upload for a short burst. The rejected dynamic-light path remains 100-500 us more expensive per storm flash on MX350-class hardware.
Verification: Static scan found no new hot allocations or string formatting in the touched files. Current dotnet builds are blocked by unrelated voxel helper errors, with no celestial diagnostics emitted.

## Decision 13: Manual Atmosphere Density Curve Sampling

Problem: Strict static review found three remaining `.Evaluate(...)` calls in celestial atmosphere density. They were `AnimationCurve.Evaluate`, not `Gradient.Evaluate`, but still undermine the zero-GC/manual-sampling argument.
Solution: Replaced those calls with manual keyframe Hermite interpolation using `AnimationCurve.length` and the keyframe indexer. This avoids `AnimationCurve.Evaluate` and avoids pulling `curve.keys` copies in the hot path.
Rejected Alternatives: Keeping Unity's curve evaluator was rejected because strict review sees `.Evaluate(` and because the implementation is opaque. Linear-only interpolation was rejected because it visibly flattens authored density curves.
Scalability potential: Low keeps the same cheap 8-sample atmosphere response with deterministic scalar density. High/Ultra preserve smoother authored density shapes while still leaving performance budget for shader-side visual overkill.
Hardware Impact: Expected 5-20 us protected during atmosphere LUT/global refresh pressure, with lower allocation risk from avoiding Unity curve helper internals.
Verification: `rg "\.Evaluate\("` now returns no matches in `HectonCelestialEngine.cs`, `WeatherEvents.cs`, or `HectonSurfaceWeatherDirector.cs`. The `CelestialOrbitMathJob` slice still has no `Vector3`, `math.sqrt`, `math.normalize`, or direct division matches.

## Decision 14: Superseded Compile Wall Classification

Problem: A previous core and broad assembly build pass failed in `Assets/_Project/Scripts/HectonVoxelEngine.cs`, not in celestial code.
Solution: Classified the new build state as a dependency wall. The failing symbols are `EnsureVoxelSurfaceMeshAvailableAsync` and `EnsureVoxelPhysicsBakeMeshAvailableAsync`, which belong to voxel/mesh ownership outside ATMOSPHERE & CELESTIAL.
Rejected Alternatives: Editing voxel async mesh helpers from CELESTIAL_MECHANICS was rejected as domain sabotage. Reverting the celestial hardening pass was rejected because the compiler emitted no celestial diagnostics and the static scans improved.
Scalability potential: No direct runtime impact in celestial; preserves domain boundaries while leaving the integration failure visible to the owning agent.
Hardware Impact: None from the compile wall. Runtime estimates for celestial remain static estimates until Unity Console/Play Mode/profiler evidence is available.
Verification: This compile wall was later cleared by the working tree. Decision 15 records the current green dotnet build state. Unity MCP `validate_script` still timed out in its regex validator on the large celestial file, and `read_console` could not run because no Unity session was available.

## Decision 15: Lightning Payload Fidelity And Upload Cache

Problem: `WeatherEvents.RaiseLightning(float)` carried authored flash intensity, but celestial consumed every lightning payload as a full-strength `1.0` flash. Blackbox telemetry also read `_HectonLightningFlash` back from the shader system.
Solution: Celestial now saturates `payload.WeatherIntensity`, keeps the stronger of current and incoming flash intensity, and writes through `UploadLightningFlashShaderGlobal()`. The last uploaded flash value is cached in `_lastUploadedLightningFlash01`, so telemetry reads owned scalar state instead of shader global state.
Rejected Alternatives: Always forcing full white flash was rejected because it discards weather-profile grading and makes cheap/violent storms look identical. Shader readback was rejected because owned CPU state is already available and cheaper. Adding real lightning lights remains rejected under the visual-fake mandate.
Scalability potential: Low keeps one scalar and no dynamic light. Middle/High/Ultra preserve authored strike intensity so stronger profiles can look harsher without extra scene objects or shadow maps.
Hardware Impact: Removes one shader-global read from each blackbox write and skips redundant `_HectonLightningFlash` uploads inside an epsilon band. Dynamic-light cost remains avoided at the previously estimated 100-500 us per strike burst on MX350-class hardware.
Verification: Static scan shows the only direct `_HectonLightningFlash` `Shader.SetGlobalFloat` call is inside `UploadLightningFlashShaderGlobal`, and no shader readback remains for that scalar. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` and `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` both succeeded with 0 warnings and 0 errors. Unity Console/Play Mode/profiler verification remains unavailable because MCP reports `no_unity_session`.

## Decision 16: Storm Cloud Density Upload Gate

Problem: Storm density could be written from both weather events and the SlowTick global shader refresh even when the scalar had not changed.
Solution: Added `_lastUploadedStormCloudDensity01` and `UploadStormCloudDensityShaderGlobal()` with finite clamping, saturate, epsilon skip, and force-clear during runtime reset.
Rejected Alternatives: Continuing unconditional `Shader.SetGlobalFloat` calls was rejected because shader-global traffic is still driver-facing work. Moving storm ownership back to surface weather was rejected because celestial already owns the sky/weather scalar bridge.
Scalability potential: Low/MX350 avoids repeated uploads during steady storm state. High/Ultra keep the same scalar hook available for richer shader response without adding lights, volumetrics, or per-material writes.
Hardware Impact: Estimated low single-digit microseconds protected on steady SlowTick/event refreshes; bigger gain is avoiding unnecessary driver work under storm spam while preserving the previous 15-50 us weather-transition fake budget.
Verification: Static scan shows the only direct `_HectonStormCloudDensity` `Shader.SetGlobalFloat` call is inside `UploadStormCloudDensityShaderGlobal`, with no shader readback. `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1` succeeded with 53 warnings and 0 errors; `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal -m:1` succeeded with 11 warnings and 0 errors. Warnings are outside celestial. Unity Console/Play Mode/profiler verification remains unavailable because MCP console read failed with ping not answered.

## Decision 17: Evidence Hygiene Correction

Problem: The Omega verification paragraph still described a superseded voxel dependency wall even though later build checks cleared it.
Solution: Rewrote that paragraph to separate historical blocked checkpoints from the current dotnet state and keep the remaining PENDING reason limited to missing Unity runtime/profiler evidence.
Rejected Alternatives: Leaving contradictory evidence was rejected because the CTO-facing rationale must be machine-checkable. Deleting the historical blocked entries was rejected because those checkpoints explain prior status transitions.
Scalability potential: No runtime impact; improves integration accountability and prevents false compile-wall blame on celestial.
Hardware Impact: None. The latest runtime estimates remain unchanged until Unity profiler evidence exists.
Verification: Documentation-only correction; no code path changed.

## Decision 18: Single Lightning Decay Owner

Problem: `UpdateGlobalShaderData()` still called `UpdateLightningFlashShaderGlobal()`, so a SlowTick shader refresh could decay `_HectonLightningFlash` in addition to the active `LateFrameTick` decay path.
Solution: SlowTick/global shader refresh now calls `UploadLightningFlashShaderGlobal(_lightningFlash01, false)` only. `LateFrameTick` remains the only path that advances the scalar toward zero.
Rejected Alternatives: Leaving two decay call sites was rejected because it makes flash duration cadence-dependent. Adding another weather-side timer was rejected because celestial already owns the scalar after `WeatherEvents.Lightning`.
Scalability potential: Low keeps one shader scalar and no dynamic light. Middle/High/Ultra get consistent authored lightning duration while preserving shader-only overkill for stronger storms.
Hardware Impact: Prevents one redundant scalar lerp path on SlowTick frames and preserves the 100-500 us dynamic-light avoidance budget. Direct runtime measurement remains absent.
Verification: Static scan shows `UpdateLightningFlashShaderGlobal()` is only called from `LateFrameTick`; the only direct `_HectonLightningFlash` shader write remains inside `UploadLightningFlashShaderGlobal`. No `dotnet build` was run after the user's explicit no-build instruction.

## Decision 19: Strict Packed Gradient Sampling And Render Push Cadence

Problem: Static recheck found editor-only `Gradient.Evaluate` calls in atmosphere LUT color sampling, and LUT rebuild/publish could still push LUT shader globals and `RenderSettings` in the same celestial step that later pushed the final sky state.
Solution: Removed direct gradient evaluator calls and made editor/runtime LUT sampling use the packed `NativeArray<float4>` gradient samples. `OnValidate` marks samples dirty before the forced bake. LUT rebuild now carries an explicit `publishOnRebuild` flag so timeline and manual rebake paths let `UpdateGlobalShaderData` publish the LUT once, then push render settings once after global shader data is current.
Rejected Alternatives: Keeping editor-only `Gradient.Evaluate` was rejected because the task mandate was strict and static evidence matters. Leaving LUT publish as a hidden render-state writer was rejected because it makes the SlowTick cadence harder to audit.
Scalability potential: Low/MX350 keeps one packed 8-sample gradient lane and avoids duplicate ambient/fog writes during celestial refresh. Middle/High/Ultra preserve authored atmosphere colors while retaining saved CPU/driver budget for stronger eclipse, storm, and gas-giant shader response.
Hardware Impact: Expected low single-digit microseconds protected on LUT-present refreshes; larger impact is deterministic shader/render-state cadence and no stale editor gradient samples after authoring changes.
Verification: `rg "\.Evaluate\("` returns no matches in touched celestial/weather files. `PublishCelestialAtmosphereLut`, `UpdateDynamicCelestialAtmosphere`, and `EnsureCelestialAtmosphereLutReady` call sites now expose explicit publish/render-settings intent. No `dotnet build` was run due the user's no-build instruction.
