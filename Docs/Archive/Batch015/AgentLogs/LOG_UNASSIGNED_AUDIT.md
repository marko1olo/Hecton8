# LOG_UNASSIGNED_AUDIT

What was wrong:
- User needed objective inventory of cheap cinematic lighting/fog/reflection/post tricks.
- Unity editor live state is not production-authoritative: active scene path is empty/rootCount=2.
- Console contains compile errors, so runtime validation is compromised.

What was done:
- Read authority docs and rendering/optimization mandates.
- Scanned URP assets, renderer assets, volume profiles, project settings, scene artifacts, shaders, scripts, and VFX/AO routes.
- Queried Unity MCP read-only for active scene, pipeline, renderer feature list, skybox/fog/reflection state, and console messages.
- No code, scene, or setting changes were made.

Cinematic cheats found:
- Baked/probe artifacts: 02_HECTON_WORLD has LightingData.asset and ReflectionProbe-0.exr.
- Realtime reflection probes disabled in QualitySettings.
- URP Low/Medium/High support probe blending, box projection, and probe atlas.
- First-party noir color grading exists via noir_color_grading_profiles.csv and HectonVisorUberPostFeature.Noir.cs.
- URP profiles have Tonemapping and ColorAdjustments active. Bloom exists but intensity is 0. MotionBlur is inactive.
- HectonUnderwaterVisuals owns RenderSettings fog and Crest/depth fog integration.
- HectonNoirDepthFogFeature, HectonScooterVolumetricShaftsFeature, HectonAbyssalSsdoFeature, HectonDeferredCausticsFeature are present in serialized renderer assets.
- Scooter shafts use renderScale 0.5 and explicitly avoid world raymarching.
- AO exists through packed/material/vertex AO, AbyssalSSDO, cave voxel AO controller, and HectonVoxelSsaoFeature. Voxel SSAO has HasRuntimeConsumer=false and therefore is currently dormant.

Missing or unproven:
- No proof of production active 02_HECTON_WORLD scene state from Unity MCP because current editor scene is not that world.
- No proof of a real Unity ColorLookup LUT texture in production profiles. Fog LUT textures exist but are 73-byte placeholders; noir grading is CSV/shader-driven.
- No proof of authored transparent sprite/card light shaft meshes as the primary route. Current route is a custom half-res screen-space/post route.
- No proof of enough reflection probe zoning beyond one 02_HECTON_WORLD probe artifact and renderer support.
- No profiler timings for Bloom, shafts, SSDO, caustics, or fog passes in current broken compile state.

Cinematic cheats rejected:
- Realtime GI, SSR on low tier, every-frame realtime reflection probes.
- URP SSAO as default; project mandates reject it.
- Full volumetric raymarch god rays for weak hardware.
- Chromatic Aberration plus Lens Distortion as a default lens stack.
- Many shadow-casting point lights as fake GI. Static bake or emissive/material/probe fake first.

Exact Microseconds saved:
- 0 us. This was an audit and did not change runtime code or assets.
- Potential savings are deferred to profiling. Avoiding realtime GI/SSR/full volumetrics is expected to save milliseconds on i3/MX350-class hardware, but no numeric claim is valid without capture.

---

What was wrong:
- The user requested real visual improvement but warned that another agent owns compilation.
- `noir_color_grading_profiles.csv` had only 3 sparse rows, leaving large depth/stress bands dependent on fallback behavior.
- `noir_aesthetic_profiles.csv` was missing.
- `caustic_lighting_profiles.csv` contained `Hurricane`, but the parser maps `Hurricane` to `WeatherState.Storm`; the first `Storm` row wins, so `Hurricane` was unreachable data.
- `Assets/_Project/Data/ocean_aesthetic_profiles.csv` and `Assets/_Project/Data/shoreline_foam_profiles.csv` were referenced by runtime paths but missing.

What was done:
- Stayed out of C# and build lanes. No dotnet build, no shader edits, no Unity settings edits.
- Expanded noir color grading to 10 continuous depth/stress profiles.
- Added fallback noir aesthetic reconstruction CSV plus Unity meta.
- Consolidated caustic Storm profile and removed the dead Hurricane row.
- Added ocean single-pass biome aesthetic CSV plus Unity meta.
- Added shoreline foam profile CSV plus Unity meta.
- Static-validated column counts and numeric parsing for all edited CSVs.

Cinematic Cheats used:
- Data-driven noir color grade, grain, glitch, chroma, and vignette scaling instead of new post passes.
- Caustic behavior tuned through existing fake projection parameters instead of real light simulation.
- Ocean/shoreline richness bought with existing GlobalQualityWeight-scaled foam, wake, reflection, falloff, and normal perturbation parameters.
- No realtime GI, SSR, realtime reflection refresh, URP SSAO, or full volumetric truth added.

Exact Microseconds saved:
- Runtime hot-path cost added: 0 us. Data-only changes; no new pass/sample/allocation.
- Caustic dead-row cleanup: theoretical CPU saving below 0.01 us per profile scan; measured proof absent.
- Build/profiler validation not run because `dotnet` PID 17540 is active and another agent owns compilation.
- STATUS: PENDING VERIFICATION until Unity import/play/profiler after compile lane clears.

---

What was wrong:
- `upscaler_quality_profiles.csv` had overlapping scale bands while the DRS profile selector is first-match. That allowed cheaper profiles to mask later middle/high/ultra profiles.
- `camera_trauma_profiles.csv` used `default` as the first row with 72m radius. The loader uses first row radius for low-tier radius and last row radius for ultra-tier radius, so weak devices inherited too broad a shake radius.

What was done:
- Replaced DRS overlap with six monotonic continuous bands: survival, compact, handheld, middle, high, ultra.
- Reduced first camera trauma radius from 72m to 32m. Kept translation/rotation gain at 1.0 and kept ultra_overkill as the last row with 120m radius.
- Static-validated edited CSVs for column counts, numeric parsing, caustic key uniqueness, and DRS range monotonicity.

Cinematic Cheats used:
- Better reconstruction profile shaping instead of new samples or a new upscaler.
- Localized low-tier camera trauma instead of deleting camera juice.
- High/ultra presentation remains stronger through wider radii and stronger profile values.

Exact Microseconds saved:
- Runtime hot-path cost added: 0 us.
- Low-tier event fanout and DRS correctness may improve, but measured microsecond proof is absent.
- Build/profiler validation not run because `dotnet` PID 17540 is active.
- STATUS: PENDING VERIFICATION.

---

What was wrong:
- `HectonMarineSnowRenderer` had a source-data route for `Assets/_SourceData/VFX/Propwash/vfx_silt_profiles.csv`, but the file was missing.
- `HectonVisorARStencilRendererFeature` had `loadCsvProfiles=true` and a route for `Assets/_SourceData/Visor/visor_hud_profiles.csv`, but the folder/file was missing.

What was done:
- Added `vfx_silt_profiles.csv` with all six parser-supported keys and Unity meta.
- Added `Assets/_SourceData/Visor` folder meta and `visor_hud_profiles.csv` with four fixed DTO rows: default, pressure, warning, abyssal.
- Static-validated silt key coverage, Visor row columns/numbers, and `git diff --check`.

Cinematic Cheats used:
- Marine snow density/curl/wake tuning through existing GPU fake, not real water particulate physics.
- Helmet HUD curvature/fog/color profiles through existing stencil/visor route, not a new pass.
- Beauty preserved: no effect disabled, no high-tier cap reduced.

Exact Microseconds saved:
- Runtime hot-path code cost added: 0 us.
- New source files may cause cold/editor hydration only. Measured frame proof is absent.
- Build/profiler validation not run because `dotnet` PID 17540 is active.
- STATUS: PENDING VERIFICATION.

---

What was wrong:
- `BiolumPulseSyncRuntime` has an existing editor override route for root `biolum_pulse_profiles.csv`, but the file was absent.

What was done:
- Added `biolum_pulse_profiles.csv` at project root with four species rows and four pulse-group rows.
- Static-validated row shapes and numeric fields.

Cinematic Cheats used:
- Synchronized emissive pulse data instead of physical light simulation.
- Project-root CSV avoids Unity asset import while the compile lane is occupied.

Exact Microseconds saved:
- Runtime hot-path code cost added: 0 us.
- Visual gain depends on existing runtime consumption; measured proof is absent.
- STATUS: PENDING VERIFICATION.

---

What was wrong:
- Full-repo `git diff --check` is polluted by unrelated `Docs/Tasks/CURRENT_BATCH.md` trailing whitespace and many other agents' dirty files.

What was done:
- Ran scoped `git diff --check` against all `UNASSIGNED_AUDIT` files. It passed.
- Did not edit unrelated batch/current agent files.

Cinematic Cheats used:
- None. Verification-only.

Exact Microseconds saved:
- 0 us. Static hygiene proof only.
- Build/profiler validation not run because `dotnet` PID 17540 is active.
- STATUS: PENDING VERIFICATION.

---

What was wrong:
- `DiegeticVisorLensRuntime` resolves root `visor_properties.csv`, but the file was missing.
- `InteriorGIProbeVolumeRuntime` resolves `Docs/lighting_fixtures.csv`, but the file was missing.
- `DynamicPointLightCullingDirector` resolves `Docs/Data/light_culling_profiles.csv`, but the file was missing.
- `ShinobuVoxelSculptorWindow` resolves `Assets/_Project/Data/VFX/ShinobuDeltaCrusherTuning.csv`, but the file was missing.

What was done:
- Added `visor_properties.csv` with nine lens/fog/droplet/crack/dirt tuning keys.
- Added five fake interior GI fixture rows for warm bounce, cyan panel, emergency red, biolume, and helmet flash refill.
- Added six dynamic point light culling profile rows without headers/comments, matching the parser contract.
- Added `ShinobuDeltaCrusherTuning.csv` plus meta for editor debris tuning.

Cinematic Cheats used:
- Fake bounce/practical light data instead of realtime GI or shadow-casting point-light spam.
- Diegetic lens grime/fog/crack tuning through existing CSV route instead of a new pass.
- Debris tuning source for authored cinematic fragments, not runtime physics expansion.

Exact Microseconds saved:
- Runtime hot-path code cost added: 0 us.
- New GPU passes added: 0.
- Expected low-end protection is avoiding realtime GI/light spam; exact measured microseconds are absent.
- Build/profiler validation not run because `dotnet` PID 17540 is active.
- STATUS: PENDING VERIFICATION.

---

What was wrong:
- `shader_globals_override.csv`, `font_metrics_override.csv`, and TerminalOS layout/decryption CSVs are real routes, but blind values would be unsafe.

What was done:
- Rejected blind authoring of those files.
- Reason: shader globals are process-wide visual state, font metrics require the actual atlas, and TerminalOS rows require scene-specific terminal hashes/layouts.

Cinematic Cheats used:
- None. This is a regression-avoidance decision.

Exact Microseconds saved:
- 0 us.
- Avoided likely visual/UI breakage from guessed global overrides.

---

What was wrong:
- Late data files needed scoped validation without touching another agent's compile lane.

What was done:
- Static-validated visor lens keys, lighting fixture rows, light culling rows, and debris tuning row.
- Scoped `git diff --check` passed for the late files.

Cinematic Cheats used:
- None. Verification-only.

Exact Microseconds saved:
- 0 us.
- Full build/import/profiler still deferred because `dotnet` PID 17540 is active.
## 2026-05-31 - UNASSIGNED_AUDIT VFX Data Pass

What was wrong:
- `Assets/_Project/Data/VFX/Beam/beam_visuals.csv` had `quality_weight,1.0`, while `ShinobuPlasmaBeamRuntime` already owns beam quality through `HomeostasisBrain.GlobalQualityWeight`. On CSV reload this could briefly stomp continuous quality.
- `Assets/_Project/Data/VFX/REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json` claimed to mirror `VfxComputeParticleBudgetCatalog`, but low/high/ultra particle counts and MX350 group audits were stale.

What was done:
- Removed the beam CSV local quality override. Kept radius, noise, heat, energy, biome extinction, requested beams, and automatic radial segment scaling.
- Updated VFX compute budget JSON to match runtime constants: Low 8512 total/8000 snow, High 104096 total/100000 snow, Ultra 105120 total/100000 snow.
- Updated dispatch group audits and marine-snow VRAM half-cut model so high/ultra are documented as visual-overkill paths, not MX350 soft-cap-safe defaults.

Cinematic Cheats used:
- Preserved authored beam fake geometry and shader-driven noise instead of replacing it with heavier simulation.
- Preserved compute-particle visual richness on high/ultra while keeping weak-device truth dependent on continuous quality and pressure compression.

Exact Microseconds saved:
- Runtime code added: 0 us.
- Beam quality override removal: no measured frame gain; prevents a hot-reload frame from forcing q=1.0.
- Budget JSON correction: 0 us runtime impact; proof artifact corrected for future tooling/agent decisions.

Verification:
- Parsed `beam_visuals.csv`; no `quality_weight` key remains.
- Parsed `REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json` with `ConvertFrom-Json`; tier constants match runtime catalog.
- Scoped `git diff --check` passed for the edited VFX files with LF-to-CRLF warnings only.
- Build/import not run because `dotnet` PID 17540 is active.
- Found `character_rig_constraints.csv` quality field, traced it to Animation/KCC tooling, and left it untouched as out-of-domain.

## 2026-05-31 - UNASSIGNED_AUDIT TBDR and shadow data pass

What was wrong:
- `TBDRPipelineSurgeonRuntime` resolves `Data/Rendering/gpu_budgets.csv`, but no project file existed.
- `AbyssalShadowCullingRuntime` resolves `Docs/Tasks/shadow_culling_profiles.csv`, but no project file existed.
- `Data/Visuals/texture_set_indices.csv` is also missing, but its contract requires real texture array slice ownership and is unsafe to guess.

What was done:
- Added `Data/Rendering/gpu_budgets.csv` with one parser-compatible budget line: `560000,3600,12.0`.
- Added `Docs/Tasks/shadow_culling_profiles.csv` with five parser-valid rules from weak-device practical shadows to ultra silhouettes.
- Rejected blind authoring of `texture_set_indices.csv`.

Cinematic Cheats used:
- DOD/TBDR culling as a visual-preserving fake: reduce submitted geometry/light-shadow pressure instead of removing fog, grime, beams, or post.
- Shadow Math LOD: weak profiles cull tiny casters early; high/ultra profiles keep smaller casters and longer silhouettes.

Exact Microseconds saved:
- 0 us measured. No code, build, import, profiler, or runtime execution was performed.
- Expected savings are only active after editor/tuner/runtime consumers load the files and must be measured later.

Verification:
- Parsed `gpu_budgets.csv` first numeric line.
- Parsed five `shadow_culling_profiles.csv` rows.
- Scoped `git diff --check` passed for both files.
- No Unity import/build: Unity `dotnet` PID 17360 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT atmosphere and chemistry source-data pass

What was wrong:
- `ShinobuOceanSurfaceAtmosphereRuntime` resolves `Assets/_SourceData/Atmosphere/weather_profiles.csv`, but the source file was missing.
- `ToxicOutgassingChemistryRuntime` resolves `Data/Tuning/chemical_properties.csv`, but the tuning file was missing.
- `beaufort_scale_profiles.csv` looks tempting, but the current parser/runtime route is unsafe for blind authoring because normal state-name hashes do not guarantee the `QSTP` profile slot used by wave fill.

What was done:
- Added `Assets/_SourceData/Atmosphere/weather_profiles.csv` plus Unity meta with parser-supported wind, storm, foam, rain, scatter, surge, and four indexed wave lanes.
- Added `Data/Tuning/chemical_properties.csv` with all ten supported chemical constants.
- Rejected blind Beaufort authoring until the owner route guarantees a deterministic profile slot or C# repair is allowed.

Cinematic Cheats used:
- Multi-lane authored wave/scatter/weather data through existing ocean runtime instead of new wave simulation.
- Bounded chemistry tuning and visual thresholds through existing field logic instead of adding particle-heavy gas truth.

Exact Microseconds saved:
- Runtime code added: 0 us.
- New render/GPU passes added: 0.
- Expected gain is visual coherence from existing systems and avoided future expensive simulation pressure; no profiler measurement taken.

Verification:
- Parsed `weather_profiles.csv`: 36 rows, supported parser keys, optional wave indices valid.
- Parsed `chemical_properties.csv`: 10 rows, supported parser keys.
- Scoped `git diff --check` passed for both new data files.
- No Unity import/build: Unity `dotnet` PID 17360 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT read-only sub-agent integration

What was wrong:
- Read-only URP/VFX audit found direct GPU submission paths outside the preferred RenderGraph-owned route:
  - `ParasiteSwarmGpuRuntime.cs`: hot visual flow executes command buffer and procedural indirect draw.
  - `HectonMarineSnowRenderer.cs`: direct `Graphics.DrawProceduralIndirect`.
  - `ShinobuPlasmaBeamRuntime.cs`: buffer upload and draw from visual sync.
- Additional code/shader risks were found: hot `GlobalRegistry` fallback in `HectonDrsRenderFeatureGate.cs`, per-frame shader-global readback in `HectonVolumetricParticulateFogFeature.cs`, hard quality thresholds in shaders, and high shader variant counts.
- Data scout confirmed the weather and chemistry gaps already closed in this pass, but Beaufort remains rejected by primary parser review.

What was done:
- No C#/shader edit was made.
- Logged the risks as code-fix-later items because Unity `dotnet` PID 17360 is active and these fixes need compile/profiler ownership.
- Kept `beaufort_scale_profiles.csv` deferred until the parser/runtime route can deterministically bind the profile slot read by wave fill.

Cinematic Cheats used:
- None in this integration step. It is risk capture only.

Exact Microseconds saved:
- 0 us measured.
- Potential future savings are only real if direct draws/hot polls are migrated to owned graph/bridge paths and profiled.

Verification:
- Sub-agent outputs were read and integrated.
- No build/import/profiler run because Unity `dotnet` PID 17360 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT packaged water extinction LUT pass

What was wrong:
- `LutArrayResolver` can load `Data/Visuals/Water_Extinction_Matrix.bin` from StreamingAssets for player builds, but that packaged path was absent.
- Root `Data/Visuals/Water_Extinction_Matrix.bin` existed and had the expected 393216-byte size, so editor/project fallback could hide the player packaging gap.

What was done:
- Copied the existing LUT to `Assets/StreamingAssets/Data/Visuals/Water_Extinction_Matrix.bin`.
- Added Unity meta files for `Assets/StreamingAssets/Data.meta`, `Assets/StreamingAssets/Data/Visuals.meta`, and `Assets/StreamingAssets/Data/Visuals/Water_Extinction_Matrix.bin.meta`.
- Integrated shader scout findings as deferred import/profiler backlog, not code changes.

Cinematic Cheats used:
- Packaged Beer-Lambert R16 LUT as an authored light/water fake for allowed tiers instead of adding runtime atmospheric simulation.
- Low-memory/portable lanes still keep the existing analytical fallback path.

Exact Microseconds saved:
- Runtime code cost added: 0 us.
- Runtime proof absent. Expected effect is preventing packaged mid/high/ultra builds from falling back to analytical water extinction when the LUT is allowed.

Verification:
- Source and StreamingAssets LUT both equal 393216 bytes.
- SHA256 for both files: `99EB8631F64C15181E62E2C1E24CDA285A490330C0F04F99FB218EFEC9BBFF89`.
- Scoped `git diff --check` passed for the new StreamingAssets path and meta files.
- No Unity import/build/profiler: Unity `dotnet` PID 17360 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT packaged biolum profile pass

What was wrong:
- `BiolumPulseSyncRuntime` searches `Application.streamingAssetsPath` for `Biolum_Profiles.bin` in player builds.
- The authored binary existed only at `Data/Visuals/Biolum_Profiles.bin`, which is editor/project-root fallback only.

What was done:
- Copied `Data/Visuals/Biolum_Profiles.bin` to `Assets/StreamingAssets/Biolum_Profiles.bin`.
- Added `Assets/StreamingAssets/Biolum_Profiles.bin.meta`.
- Rejected copying `gerstner_wave_weather.bin` because its legacy route is editor-only.

Cinematic Cheats used:
- Packaged authored biolum pulse/profile data as a cheap presentation fake: stronger color/frequency/pulse identity without new particles, lights, or simulation.

Exact Microseconds saved:
- Runtime code cost added: 0 us.
- Package size added: 25936 bytes.
- Runtime proof absent. Expected effect is preventing packaged builds from seeding generic biolum defaults when the authored profile file exists.

Verification:
- Source and StreamingAssets files both equal 25936 bytes.
- SHA256 for both files: `1C7DB3B6FD0FC24541B078BF9DEEBBCAA9EB9AAE2DCC6F4F25D749439A3B0FEB`.
- Scoped `git diff --check` passed for the new binary/meta.
- No Unity import/build/profiler: Unity `dotnet` PID 17360 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT additional binary fallback rejection

What was wrong:
- Several fallback probes name StreamingAssets or archive binaries that could be mistaken for easy packaging wins.
- No validated source payload exists in the repo for those names.

What was done:
- Searched for `global_shader_constants.h8bin`, `lighting_palettes_007.bin`, `mobile_vertex_limits.h8bin`, `texture_streaming_budgets.bin`, `visor_materials_006.h8bin`, `surface_nets_lut.h8bin`, `marching_cubes_edge_tables.bin`, `volcanic_vent_locations.h8bin`, `seed_ship_emission_rates.h8bin`, `glitch_zones_007.bin`, `flora_genetics.h8bin`, `l_system_axioms_006.h8bin`, and `botanical_traits.bin`.
- Found no validated binaries to copy.
- Confirmed adjacent CSV routes such as `vehicle_wake_profiles.csv`, water optics, visual aging/degradation, parasite behavior, gas diffusion, and flora sway already exist.

Cinematic Cheats used:
- None added. The correct visual decision was to keep existing defaults/fallbacks rather than fabricate binary readiness.

Exact Microseconds saved:
- 0 us measured.
- Avoided invalid package data, false-positive binary probe branches, and unnecessary package bloat.

Verification:
- Static file search only.
- No Unity import/build/profiler: Unity `dotnet` remains active; latest post-patch process check saw PID 11560.

## 2026-05-31 - UNASSIGNED_AUDIT biome lighting data alignment

What was wrong:
- `Docs/Data/lighting_gradient_profiles.csv` used stale profile names: `open_ocean`, `mushroom_cave`, `radioactive_trench`, `abyssal_blue`.
- Active biome authority in `Assets/_Project/Data/World/biome_atmosphere_rules.csv` uses `safe_shallows`, `kelp_forest`, `deep_abyss`, `sulfur_vents`.
- `HectonLightingRuntime_DayNightRelay` resolves by biome hash first, then falls back by index. A hash miss can assign wrong zone colors by index.
- `Docs/Data/Profiles/ambient_lighting_profiles.csv` lacked active biome-name rows for the interior fake-GI tint route.

What was done:
- Replaced day/night lighting-gradient rows with active biome names and matching noir-safe colors.
- Added active biome rows to ambient lighting profiles while keeping legacy fallback rows.
- No C#, shader, Unity import, build, or scene mutation.

Cinematic Cheats used:
- Corrected authored fog/ambient/directional color data for existing fake-GI and day/night relay paths.
- Bought visual coherence with cold CSV data instead of new lights, volumetrics, or post passes.

Exact Microseconds saved:
- 0 us measured.
- Runtime code cost added: 0 us.
- Potential benefit is visual correctness and avoiding wrong-color fallback; frame impact requires Unity/profiler proof.

Verification:
- Static parser check: `lightingRows=4 ambientRows=7 activeAmbientRows=4`.
- Scoped `git diff --check` passed with LF/CRLF warnings only.
- No Unity import/build/profiler: Unity `dotnet` PID 19384 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT water extinction profile reachability

What was wrong:
- `HectonVolumetricParticulateFogFeature.ApplyExtinctionProfileFromVault()` scans water extinction profiles in order and exits on the first depth match.
- `Docs/Data/Profiles/water_extinction_profiles.csv` started with `default_abyss,0..20000`, so `silted_wreck`, `vent_blackwater`, and `brine_noir` were dead profile rows.

What was done:
- Changed the CSV to monotonic depth bands:
  - `default_abyss`: 0..40m.
  - `silted_wreck`: 40..900m.
  - `vent_blackwater`: 900..3500m.
  - `brine_noir`: 3500..20000m.
- Added a short file-local comment documenting the first-match contract.
- No C#, shader, Unity import, build, scene, or project setting mutation.

Cinematic Cheats used:
- Restored existing cheap depth-fog/water-extinction fake selection instead of adding volumetric truth, new dynamic lights, or extra shader sampling.

Exact Microseconds saved:
- Runtime code cost added: 0 us.
- GPU pass/sample cost added: 0 us.
- Potential gain is visual correctness and avoided temptation to add heavier fog because authored depth looks now resolve. Measured frame proof absent.

Verification:
- Static parser check: `waterExtinctionRows=4 monotonic=True`.
- Scoped `git diff --check` passed with LF/CRLF warning only.
- No Unity import/build/profiler: Unity `dotnet` PID 17936 remains active; CPU load sampled at 93%.

## 2026-05-31 - UNASSIGNED_AUDIT DRS upscaler continuity repair

What was wrong:
- `Assets/_Project/Data/upscaler_quality_profiles.csv` had 0.01 gaps between adjacent render-scale bands.
- `BilateralDrsUpscalerContracts` skips a profile when `scale01 < MinScale01 || scale01 > MaxScale01`; intermediate scales could miss all authored rows and fall back to default tuning.

What was done:
- Changed adjacent boundaries to shared inclusive values: `0.45`, `0.59`, `0.70`, `0.81`, `0.92`.
- Kept the same six bands: survival, compact, handheld, middle, high, ultra.
- No C#, shader, Unity import, build, scene, or project setting mutation.

Cinematic Cheats used:
- Preserved the existing cheap bilateral DRS reconstruction profile route. This keeps image quality stable through authored reconstruction parameters instead of adding resolution, samples, or a new post pass.

Exact Microseconds saved:
- Runtime code cost added: 0 us.
- GPU pass/sample cost added: 0 us.
- Potential benefit is avoiding no-profile fallback frames during continuous render-scale movement. Measured frame proof absent.

Verification:
- Static parser check: `upscalerRows=6 contiguous=True first=0.25 last=1`.
- Scoped `git diff --check` passed with LF/CRLF warning only.
- No Unity import/build/profiler: Unity `dotnet` PID 17936 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT noir aesthetic active-source repair

What was wrong:
- `HectonVisorUberPostFeature.ResolveAestheticCsvPath()` resolves `Data/Visuals/noir_aesthetic_profiles.csv` first.
- That primary file still had an old 4-row table, while `Assets/_Project/Data/noir_aesthetic_profiles.csv` had the richer 10-row reconstruction table.
- Result: the active cold-load path could shadow the richer profile coverage.

What was done:
- Replaced `Data/Visuals/noir_aesthetic_profiles.csv` with the same 10 data rows used by the authored Assets profile.
- Added a file-local resolver-order note.
- No C#, shader, Unity import, build, scene, or project setting mutation.

Cinematic Cheats used:
- Restored authored cheap reconstruction constants for non-unified visor fallback. This buys noir readability/failure styling through existing shader params, not extra samples or passes.

Exact Microseconds saved:
- Runtime code cost added: 0 us.
- GPU pass/sample cost added: 0 us.
- Potential benefit is visual correctness and avoiding coarse fallback style. Measured frame proof absent.

Verification:
- Static parser check: root and Assets noir aesthetic profiles each parse as 10 rows, 12 columns.
- Data rows mirror the Assets profile source.
- Scoped `git diff --check` passed with LF/CRLF warning only.
- No Unity import/build/profiler: Unity `dotnet` PID 17936 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT compile-safe rendering continuation

What was wrong:
- Dynamic light and shadow profile CSVs look editable, but their first column is hashed and matched against producer-owned `ProfileHash` values. Human-readable names are not proof of runtime binding.
- It would be easy to "optimize" by disabling Bloom, shafts, or vignette, but that directly violates the visual target.

What was done:
- Read water optics, ocean single-pass, shoreline foam, screen-space shaft, dynamic light culling, abyssal shadow culling, and URP postprocess contracts.
- Confirmed water/ocean/foam data routes are currently parser-valid.
- Confirmed Quest renderer keeps scooter screen-space shafts inactive while PC/mobile keep the cheap fake shaft feature active.
- Confirmed active low/medium/high URP profiles already use cinematic grading/Bloom/vignette layers and keep MotionBlur inactive.

Cinematic Cheats used:
- No new effect added. Preserved existing cheap cheats: cold CSV water profiles, half-res screen-space shafts, Bloom/grading/vignette, and first-party noir profiles.

Exact Microseconds saved:
- 0 us measured and 0 us claimed. This pass avoided unsafe edits and dead data. Profiler proof is still pending after `dotnet` clears.

Verification:
- Static contract scan only.
- No Unity import/build/profiler: `dotnet` PID 17936 remains active.

## 2026-05-31 - UNASSIGNED_AUDIT baking/probe/fog audit under active compile

What was wrong:
- The cinematic-cheat checklist could lead to destructive "optimization" if baking/AO/probe/fog state was changed blindly.
- Static scenes show baked-lightmap flags but AO extraction is disabled in text scenes/sandboxes, and those scenes point at null lighting data.
- `GraphicsSettings.asset` keeps all lightmap and fog shader variants, which is a potential build/runtime variant pressure issue but not safe to strip without player proof.

What was done:
- Verified build-scene route still lists `00_BOOTSTRAP`, `01_MAIN_MENU`, and `02_HECTON_WORLD`.
- Verified `02_HECTON_WORLD` has packaged lighting/probe artifacts: `LightingData.asset` and `ReflectionProbe-0.exr`.
- Verified all quality tiers set `realtimeReflectionProbes: 0`.
- Verified PC/mobile/high renderer assets keep cheap fog/shaft cinematic fake routes active, while Quest keeps the scooter shaft feature stripped.
- Recorded AO/lightmap/fog stripping as bake/import-safe backlog, not as a text-patch target.

Cinematic Cheats used:
- Static baked lighting and reflection probe artifacts are present for the world route.
- Realtime reflection probes are not used.
- Fog/shaft/postprocess fake routes remain intact; no beauty pass was disabled.

Exact Microseconds saved:
- `0 us` measured in this pass. This was a compile-safe audit only. Future measurable savings require a controlled bake/import/profiler lane.
