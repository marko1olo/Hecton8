# Status_UNASSIGNED_AUDIT

Task: objective audit and data-only improvement of cheap cinematic lighting/fog/reflection/postprocess techniques in the current project.
Domain: Graphics/Rendering Audit.
State: PENDING UNITY IMPORT/PROFILER VERIFICATION - static scan plus data-only rendering profile edits. No build. No C# edits. No project setting mutation.

- [x] Read authority rules and rendering mandates.
  DOD practice: AGENTS.md, domain map, and rendering/optimization mandates were checked before classification.
  Rejected alternative: broad refactor or scene edits; the user asked for inventory and judgement.
  Microsecond estimate: 0 us saved by this audit step; no runtime code changed.

- [x] Inspect project files for lighting, probes, volumes, fog, postprocess, VFX fakes, and AO evidence.
  DOD practice: file-backed proof only; renderer assets, URP assets, profiles, shaders, scripts, scene artifacts, and project settings were scanned with CLI.
  Rejected alternative: assuming feature state from names or old docs.
  Microsecond estimate: 0 us saved by this audit step; potential savings are documented in rationale and require profiler proof.

- [x] Inspect Unity/rendering state if MCP read-only tools are available.
  DOD practice: read-only Unity MCP queried active scene, URP pipeline, renderer features, skybox/fog/reflection, and console.
  Rejected alternative: loading/changing scenes or launching dotnet build while other agents may be active.
  Microsecond estimate: 0 us saved; validation is limited because the active Unity scene is not 02_HECTON_WORLD and compile errors exist.

- [x] Write concise recommendation: exists / missing / needed / rejected.
  DOD practice: final judgement separates proven project state from missing/unproven routes and forbidden expensive techniques.
  Rejected alternative: blanket "use all cinematic tricks"; HECTON-8 requires continuous GlobalQualityWeight and proof per route.
  Microsecond estimate: exact saved time is 0 us because this was an audit. Expected future impact: avoid millisecond-class realtime GI/SSR/volumetric mistakes on MX350-class hardware.

- [x] Expand noir grading coverage without touching compile lane.
  DOD practice: HectonVisorUberPostFeature.Noir CSV contract was preserved: 13 columns, numeric values, depth/stress ranges expanded from 3 sparse profiles to 10 continuous profile rows.
  Rejected alternative: disabling postprocess or editing shader/C# while another agent owns compilation.
  Microsecond estimate: 0 us runtime cost added; no new samples, render passes, allocations, or code paths.

- [x] Add missing fallback noir aesthetic reconstruction data.
  DOD practice: Added `Assets/_Project/Data/noir_aesthetic_profiles.csv` plus `.meta` matching the existing parser expectation for 12-column fallback profiles.
  Rejected alternative: relying on hardcoded fallback reconstruction values when the runtime has `loadAestheticCsv` style data hooks.
  Microsecond estimate: 0 us runtime hot-path cost added; cold CSV data only.

- [x] Clean caustic lighting profile dead row.
  DOD practice: Verified parser maps `Hurricane` to `WeatherState.Storm`; first matching `Storm` profile wins, making the Hurricane row unreachable. Consolidated Storm values into the real active row.
  Rejected alternative: adding a new weather enum/parser route in C# during another agent's compile work.
  Microsecond estimate: <0.01 us theoretical CPU reduction for later profile matches; measured proof absent.

- [x] Add missing ocean/shoreline visual profile files.
  DOD practice: Verified runtime paths and parser schemas, then added `ocean_aesthetic_profiles.csv` and `shoreline_foam_profiles.csv` with Unity `.meta` files. Existing `GlobalQualityWeight` code remains the scaler.
  Rejected alternative: adding new water simulation or a new render feature.
  Microsecond estimate: 0 us new GPU cost; existing code consumes the data and scales effect intensity/falloff/normal perturbation.

- [x] Static-validate edited data files.
  DOD practice: CSV column counts and numeric parse passed for noir grading, noir aesthetic, caustics, ocean aesthetic, and shoreline foam profiles. `git diff --check` passed except line-ending warnings on two pre-existing tracked CSVs.
  Rejected alternative: launching dotnet/Unity build while `dotnet` PID 17540 is active.
  Microsecond estimate: 0 us; validation only.

- [x] Fix DRS upscaler profile overlap.
  DOD practice: Verified first-match range selection, then replaced overlapping bands with six monotonic continuous scale bands from survival to ultra.
  Rejected alternative: C# interpolation or a new upscaler path while the compile lane is occupied.
  Microsecond estimate: 0 us new runtime cost; same parser and profile count class. Visual correctness improves by preventing cheap profiles from winning mid/high ranges.

- [x] Fix camera trauma weak-tier radius authority.
  DOD practice: Verified loader uses the first profile radius as low-tier radius and the last profile radius as ultra-tier radius. Reduced first row radius from 72m to 32m while preserving amplitude caps and ultra radius.
  Rejected alternative: disabling shake/juice or reducing high-tier cinematic amplitude.
  Microsecond estimate: 0 us new runtime cost; fewer low-tier receivers may reduce event work, but measured proof is absent.

- [x] Static-validate expanded rendering data batch.
  DOD practice: Column counts, numeric parsing, mapped caustic keys, and upscaler range monotonicity passed for the edited CSV batch.
  Rejected alternative: dotnet/Unity build; `dotnet` PID 17540 remains active.
  Microsecond estimate: 0 us; static validation only.

- [x] Add missing marine snow silt source profile.
  DOD practice: Verified `HectonMarineSnowRenderer` resolves `Assets/_SourceData/VFX/Propwash/vfx_silt_profiles.csv` and `VolumetricSiltCsvParser` consumes key,value rows. Added all six supported keys with bounded numeric values plus meta.
  Rejected alternative: new volumetric simulation, shader edits, or lowering particle richness.
  Microsecond estimate: 0 us new hot-path code; existing background CSV route already polls the path. Runtime impact requires profiler proof.

- [x] Add missing Visor AR HUD source profiles.
  DOD practice: Verified `HectonVisorARStencilRendererFeature` cold-loads `Assets/_SourceData/Visor/visor_hud_profiles.csv` into existing Vault profile DTOs. Added four profile rows plus folder/file meta.
  Rejected alternative: editing render feature C# or disabling HUD fog/curvature.
  Microsecond estimate: 0 us new hot-path code; cold source-data hydration only.

- [x] Static-validate new source-data profiles.
  DOD practice: Parsed marine-snow six-key contract and Visor HUD 8-column rows; `git diff --check` passed for the new source files.
  Rejected alternative: Unity import/build while `dotnet` PID 17540 remains active.
  Microsecond estimate: 0 us; static validation only.

- [x] Add bioluminescence pulse override data.
  DOD practice: Verified `BiolumPulseSyncRuntime` resolves root `biolum_pulse_profiles.csv` before legacy fallback and parses species rows plus `group0..group3` pulse rows. Added four species and four group rows.
  Rejected alternative: new particle/light simulation or shader edit.
  Microsecond estimate: 0 us new hot-path code; existing editor watcher route consumes the data.

- [x] Run scoped final static hygiene checks.
  DOD practice: Scoped `git diff --check` passed for all files touched by `UNASSIGNED_AUDIT`; full-repo check is blocked by unrelated `Docs/Tasks/CURRENT_BATCH.md` trailing whitespace from other agents.
  Rejected alternative: editing unrelated batch content or launching build during active `dotnet` PID 17540.
  Microsecond estimate: 0 us; static hygiene only.

- [x] Add missing diegetic visor lens properties.
  DOD practice: Verified `DiegeticVisorLensRuntime` resolves root `visor_properties.csv` and parses key,value rows. Added nine bounded lens/fog/droplet/crack/dirt tuning keys.
  Rejected alternative: changing shader/C# lens logic or disabling lens grime/refraction.
  Microsecond estimate: 0 us new hot-path code; existing cold CSV route consumes the data.

- [x] Add missing lighting fake-source and dynamic light culling data.
  DOD practice: Verified `InteriorGIProbeVolumeRuntime` reads `Docs/lighting_fixtures.csv` and `DynamicPointLightCullingDirector` reads `Docs/Data/light_culling_profiles.csv`. Added parser-compatible rows only.
  Rejected alternative: adding shadow-casting point lights, realtime GI, or a new light simulation.
  Microsecond estimate: 0 us new code/GPU passes. Low-tier benefit is authored fake GI/culling data; profiler proof pending.

- [x] Add missing editor VFX debris tuning source.
  DOD practice: Verified `ShinobuVoxelSculptorWindow` expects `Assets/_Project/Data/VFX/ShinobuDeltaCrusherTuning.csv` with gravity,bounce,maxDebris,massUnits. Added CSV plus meta.
  Rejected alternative: adding runtime debris simulation or reducing debris presentation globally.
  Microsecond estimate: 0 us runtime hot-path code; editor tuning/bake source only.

- [x] Reject unsafe blind CSV overrides.
  DOD practice: Inspected routes for `shader_globals_override.csv`, `font_metrics_override.csv`, and TerminalOS layout/decryption CSVs. Deferred them because they need scene/atlas/hash-specific authority.
  Rejected alternative: creating global shader/font/terminal overrides with guessed values.
  Microsecond estimate: 0 us; avoiding a bad global override prevents visual regressions rather than saving measured time.

- [x] Static-validate late profile batch.
  DOD practice: Parsed visor lens keys, lighting fixture rows, light culling profile rows, and VFX debris tuning row; scoped `git diff --check` passed for the late files.
  Rejected alternative: Unity import/build while `dotnet` PID 17540 is active.
  Microsecond estimate: 0 us; static validation only.

- [x] Remove plasma beam CSV quality override.
  DOD practice: Verified `ShinobuPlasmaBeamRuntime` resolves `beam_visuals.csv`; parser accepts `quality_weight`, while `ApplyQualityAndEditorTuning` already owns `HomeostasisBrain.GlobalQualityWeight`. Removed the data key and left beam count/radius/noise/energy intact.
  Rejected alternative: C# parser change or reducing beam visuals during active compile work.
  Microsecond estimate: 0 us runtime code added; avoids a CSV hot-reload frame stomping continuous quality, exact profiler gain not measured.

- [x] Align VFX compute budget proof JSON with runtime constants.
  DOD practice: Compared `REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json` against `VfxComputeParticleBudgetCatalog`; updated low/high/ultra particle counts, dispatch group audits, and VRAM half-cut model to match code.
  Rejected alternative: leaving stale proof claiming high quality is MX350 soft-cap safe.
  Microsecond estimate: 0 us runtime impact; this is proof/data correction only.

- [x] Static-validate VFX beam and budget data.
  DOD practice: Parsed `beam_visuals.csv` key/value rows, asserted no `quality_weight` override remains, parsed JSON via `ConvertFrom-Json`, checked tier constants, and ran scoped `git diff --check`.
  Rejected alternative: Unity import/build while `dotnet` PID 17540 is active.
  Microsecond estimate: 0 us; static validation only.

- [x] Reject out-of-domain character rig quality edit.
  DOD practice: Found `character_rig_constraints.csv` has `global_quality_weight,1.0`, then traced it to `KineticCharacterAnimator` editor/runtime parser under Animation, not current rendering/VFX ownership.
  Rejected alternative: editing animation rig data from the rendering audit lane.
  Microsecond estimate: 0 us; no runtime change.

- [x] Add missing TBDR editor GPU budget override.
  DOD practice: Verified `TBDRPipelineSurgeonRuntime` resolves `Data/Rendering/gpu_budgets.csv` and `TBDRGpuBudgetCsvIngestor` consumes the first numeric line as `maxVisibleVertices,transparentQuadLimit,frustumSqueezeDegrees`. Added a conservative one-line editor override.
  Rejected alternative: changing runtime C# constants or running the tuner/import lane while Unity `dotnet` is active.
  Microsecond estimate: 0 us runtime hot-path code added; editor cold CSV route only. Any vertex/quad saving requires tuner/profiler proof after compile clears.

- [x] Add missing abyssal shadow profile rules.
  DOD practice: Verified `AbyssalShadowCullingRuntime` resolves `Docs/Tasks/shadow_culling_profiles.csv` and validates every non-comment row. Added five parser-valid shadow rules from weak-device practicals to ultra silhouettes.
  Rejected alternative: disabling shadows, lowering all shadow distance globally, or adding a new shadow render feature.
  Microsecond estimate: 0 us new render passes. Potential savings come from existing culling job rejecting tiny/low-priority casters earlier; measured proof is pending.

- [x] Reject unsafe blind material texture index CSV.
  DOD practice: Verified `ShinobuMaterialResponseRuntime` parses `Data/Visuals/texture_set_indices.csv` into texture-set hashes and texture array slice indices, then remaps material states modulo row count.
  Rejected alternative: guessing texture array slice indices without the actual texture array owner/bake artifact.
  Microsecond estimate: 0 us; no change. This avoids a likely material regression.

- [x] Static-validate TBDR/shadow data batch.
  DOD practice: Parsed `gpu_budgets.csv` first numeric line, parsed five `shadow_culling_profiles.csv` rows, and ran scoped `git diff --check` for both files.
  Rejected alternative: Unity import/build while Unity `dotnet` PID 17360 is active.
  Microsecond estimate: 0 us; static validation only.

- [x] Add missing ocean surface weather source profile.
  DOD practice: Verified `ShinobuOceanSurfaceAtmosphereRuntime` resolves `Assets/_SourceData/Atmosphere/weather_profiles.csv` and `OceanWeatherCsvParser` accepts key,value plus key,index,value rows. Added wind, storm, fog/scatter, surge, and four wave-lane parameter rows plus Unity meta.
  Rejected alternative: authoring `beaufort_scale_profiles.csv`; its parser hashes state names into profile slots while runtime wave fill reads the magic `QSTP` slot, so blind CSV rows are likely dead or collision-dependent.
  Microsecond estimate: 0 us new hot-path code; existing cold source-data route only.

- [x] Add missing toxic outgassing chemistry tuning source.
  DOD practice: Verified `ToxicOutgassingChemistryRuntime` resolves `Data/Tuning/chemical_properties.csv` and maps ten FNV-keyed key,value rows before sanitizing constants. Added all supported keys with bounded values.
  Rejected alternative: new chemical simulation, extra gas particles, or changing C# while compile lane is occupied.
  Microsecond estimate: 0 us new hot-path code; existing cold CSV route only.

- [x] Static-validate atmosphere and chemistry data batch.
  DOD practice: Parsed 36 weather rows, 10 chemistry rows, verified all keys against parser hash contracts, verified optional wave indices, and ran scoped `git diff --check`.
  Rejected alternative: Unity import/build while Unity `dotnet` PID 17360 is active.
  Microsecond estimate: 0 us; static validation only.

- [x] Integrate read-only rendering sub-agent findings without code interference.
  DOD practice: Accepted evidence that parasite swarm, marine snow, and plasma beam use direct GPU submission paths outside the RenderGraph-owned renderer feature route; accepted shader quality cliff and variant-budget risks as code/shader backlog.
  Rejected alternative: editing C#/shader files while Unity `dotnet` PID 17360 is active, or pretending these can be fixed through CSV data.
  Microsecond estimate: 0 us; documentation/risk capture only. Expected savings require later code ownership, RenderGraph bridge proof, and profiler capture.

- [x] Add packaged-player water extinction LUT copy.
  DOD practice: Verified `LutArrayResolver` searches `Assets/StreamingAssets/Data/Visuals/Water_Extinction_Matrix.bin` for player packaging and only falls back to root `Data/Visuals` in editor/current project contexts. Copied the existing 393216-byte R16 LUT into StreamingAssets and added Unity meta files.
  Rejected alternative: changing `LutArrayResolver` C# or forcing low-memory devices to use the LUT; existing code still uses analytical fallback on low-memory/portable targets.
  Microsecond estimate: 0 us runtime code added. High/mid player builds avoid analytical fallback where the LUT is allowed; exact visual/frame impact requires Unity import/player proof.

- [x] Integrate shader quality-cliff scout findings.
  DOD practice: Accepted static evidence for excessive shader variants and hard quality cliffs in TerrainMaster, UberNoir, AbyssalVoxelRock, SonarRaymarch, Sargassum boids, LeviathanOrganic, VisorUberPost, and trauma decal shaders.
  Rejected alternative: editing `.shader`/`.compute` files while Unity `dotnet` PID 17360 remains active and shader import can collide with the compile lane.
  Microsecond estimate: 0 us in this pass; future savings require shader import, Frame Debugger/RenderGraph/Profiler proof.

- [x] Add packaged-player bioluminescence profile copy.
  DOD practice: Verified `BiolumPulseSyncRuntime.BuildColdProfilePath()` searches `Application.streamingAssetsPath` in player builds and uses root `Data/Visuals` only inside `UNITY_EDITOR`. Copied the existing `Biolum_Profiles.bin` into `Assets/StreamingAssets` and added Unity meta.
  Rejected alternative: changing VFX C# profile resolver while Unity `dotnet` PID 17360 is active; copying `gerstner_wave_weather.bin` because its legacy load path is editor-only.
  Microsecond estimate: 0 us runtime code added. Mid/high packaged builds can hydrate authored biolum profile floats instead of falling back to seeded defaults; exact frame/visual proof pending.

- [x] Reject additional blind StreamingAssets binary copies.
  DOD practice: Searched for existing candidate binaries named by rendering/visor/world fallback probes: `global_shader_constants.h8bin`, `lighting_palettes_007.bin`, `mobile_vertex_limits.h8bin`, `texture_streaming_budgets.bin`, `visor_materials_006.h8bin`, `surface_nets_lut.h8bin`, `marching_cubes_edge_tables.bin`, `volcanic_vent_locations.h8bin`, `seed_ship_emission_rates.h8bin`, `glitch_zones_007.bin`, and flora genetics binaries. No source artifact was found outside already handled water/biolum payloads.
  Rejected alternative: fabricating placeholder binaries or copying unrelated root CSVs into StreamingAssets without resolver support.
  Microsecond estimate: 0 us; no runtime change. Avoided package bloat and invalid binary probe false positives.

- [x] Align biome lighting data with active biome authority.
  DOD practice: Verified `HectonLightingRuntime_DayNightRelay` resolves `Docs/Data/lighting_gradient_profiles.csv` by biome hash first and only falls back by index, and `InteriorGIProbeVolumeRuntime` resolves `Docs/Data/Profiles/ambient_lighting_profiles.csv` by biome hash/ProfileId. Mirrored the active biome names from `Assets/_Project/Data/World/biome_atmosphere_rules.csv`: `safe_shallows`, `kelp_forest`, `deep_abyss`, `sulfur_vents`.
  Rejected alternative: C# hash/fallback changes or shader edits while Unity `dotnet` PID 19384 is active; leaving old profile names because that can route current biomes through index fallback with wrong colors.
  Microsecond estimate: 0 us runtime code cost. Cold CSV parse size increased by four ambient rows only; visual correctness improves by avoiding wrong-zone lighting fallback. Runtime/profiler proof pending.

- [x] Restore reachable volumetric water extinction profiles.
  DOD practice: Verified `HectonVolumetricParticulateFogFeature.ApplyExtinctionProfileFromVault()` scans `WaterExtinctionProfileDTO` rows in order and returns on the first depth match. Replaced overlapping catch-all profile ranges in `Docs/Data/Profiles/water_extinction_profiles.csv` with monotonic ranges so silted wreck, vent blackwater, and brine noir profiles are reachable.
  Rejected alternative: C# resolver priority changes, shader changes, or deleting rich water looks while Unity `dotnet` PID 17936 is active and CPU load is 93%.
  Microsecond estimate: 0 us runtime code cost. Visual gain is correct cheap fog/extinction profile selection; profiler/import proof pending.

- [x] Close DRS upscaler scale-band gaps.
  DOD practice: Verified `BilateralDrsUpscalerContracts` skips profiles when render scale is outside inclusive `MinScale01..MaxScale01`. Replaced 0.01 gaps between authored CSV bands with shared inclusive boundaries.
  Rejected alternative: C# interpolation or parser changes while Unity `dotnet` PID 17936 remains active.
  Microsecond estimate: 0 us runtime code cost. Same six profiles, same parser, no new pass; avoids fallback/no-profile frames for intermediate render scales. Unity import/profiler proof pending.

- [x] Fix active noir aesthetic resolver shadowing.
  DOD practice: Verified `HectonVisorUberPostFeature.ResolveAestheticCsvPath()` resolves `Data/Visuals/noir_aesthetic_profiles.csv` before `Assets/_Project/Data/noir_aesthetic_profiles.csv`. Mirrored the 10-row active reconstruction profile set into the primary resolver path.
  Rejected alternative: C# resolver order changes while Unity `dotnet` PID 17936 remains active; leaving the old 4-row source to shadow richer profiles.
  Microsecond estimate: 0 us runtime code cost. Cold CSV data only; restores authored non-unified visor reconstruction coverage. Unity import/profiler proof pending.

- [x] Audit water optics, ocean single-pass, shoreline foam, and screen-space shafts without new edits.
  DOD practice: Verified active CSV/parser contracts and renderer feature activation from source and serialized renderer assets. Water optics, ocean aesthetic, shoreline foam, and Quest shaft stripping are currently contract-valid.
  Rejected alternative: adding duplicate profile rows, enabling Quest shafts, or fabricating source registrations while Unity `dotnet` PID 17936 remains active.
  Microsecond estimate: 0 us; no runtime change. This pass prevents false-positive edits rather than claiming measured savings.

- [x] Reject blind light/shadow profile-hash data expansion.
  DOD practice: Verified dynamic point-light and abyssal shadow profile CSV rows match numeric `ProfileHash` producers, not human-readable intent labels. Existing arbitrary rule names can parse, but may not bind without producer-owned hashes.
  Rejected alternative: adding more named rules or hex-looking rows that the parser would hash as text and likely never match.
  Microsecond estimate: 0 us; no runtime change. Correct fix is a compile-safe producer/hash contract or tooling report, not more guessed data.

- [x] Audit URP postprocess profile state without reducing beauty.
  DOD practice: Read active low/medium/high VolumeProfile assets. Bloom, tonemapping, shadows/midtones/highlights, vignette, and white balance are already authored; motion blur remains inactive.
  Rejected alternative: disabling Bloom/vignette for speed or adding chromatic aberration/motion blur as default low-tier cost.
  Microsecond estimate: 0 us; no data edit. Current profiles already buy cinematic grading through existing passes.

- [x] Audit baked lighting, reflection probes, fog variants, and AO state without unsafe imports.
  DOD practice: Verified project/scene YAML and packaged assets only. Build scenes keep baked lightmap flags enabled and realtime lightmaps disabled; `02_HECTON_WORLD` has `LightingData.asset` and `ReflectionProbe-0.exr`; all quality tiers keep realtime reflection probes off. Text scenes have AO bake extraction disabled and null lighting data, and GraphicsSettings keeps all lightmap/fog variants.
  Rejected alternative: flipping AO/lightmap/fog stripping flags or editing scene lighting settings while Unity `dotnet` PID 17936 is active and CPU was 85-100%; those changes need Unity bake/import/profiler proof and can silently change look or strip required variants.
  Microsecond estimate: 0 us; no runtime change. First-20-minutes visual route risk is documented for the next bake/import-safe pass.
