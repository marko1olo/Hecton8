# Rationale_UNASSIGNED_AUDIT

Problem: Need objective answer on cinematic fake lighting stack without inventing project state.
Solution: Static scan first, Unity MCP read-only where available. Compare findings against AGENTS.md and rendering mandates.
Rejected Alternatives: No code changes, no dotnet build, no Unity setting mutation. Build/profiler not required for an inventory audit and may contend with other agents.
Scalability potential: Minimum uses baked AO, fog/LUT, reflection probes, dither and no Bloom. Middle adds controlled post/fake shafts. High/Ultra buys stronger volumetric/fog/SSDO only behind proof.
Hardware Impact: On i3/MX350, replacing runtime light/volumetric/reflection truth with baked/probe/sprite/fog fakes is expected to save GPU milliseconds, but exact microseconds require profiler capture.

Problem: User asked which cinematic lighting cheats already exist, which are absent, and what should be rejected.
Solution: Classify only from objective artifacts:
- URP active quality in Unity MCP: Surface (Medium), pipeline asset Assets/_Project/Data/URP_Medium (PC_RPAsset).asset.
- Active Unity scene reported empty path/rootCount=2, so live scene state is not authoritative for 02_HECTON_WORLD.
- Console has compile errors in HectonVisorUberPostFeature.Noir.cs, HectonVisorUberPostFeature.cs, and PDAInventoryTab.cs. Runtime feature validation is therefore not clean.
Rejected Alternatives: Do not run dotnet build or mutate scenes. Do not trust Unity MCP feature_list alone because custom renderer features are present in serialized renderer assets but MCP only reported Shapes, Decal, and ScreenSpaceShadows for the current editor state.
Scalability potential: Use serialized asset state for inventory, then require profiler before promoting any active custom feature to low-tier truth.
Hardware Impact: Audit changed 0 us. Preventing one mistaken SSR/realtime GI/full volumetric route on MX350-class devices can save milliseconds, not microseconds, but exact values were not measured.

Problem: Determine whether baking/reflection probe/post/fog/AO routes exist.
Solution:
- Baking/probes: 02_HECTON_WORLD has LightingData.asset and ReflectionProbe-0.exr. QualitySettings has realtimeReflectionProbes: 0 across quality entries. URP Low/Medium/High enable reflection probe blending, box projection, and probe atlas.
- Post: URP Low/Medium use SampleSceneProfile.asset; High uses SampleSceneProfile_High.asset. Both have Tonemapping and ColorAdjustments active, Bloom active with intensity 0, Vignette active, MotionBlur inactive. Main Camera Profile only has Vignette active; DoF and ChromaticAberration inactive.
- Noir grading: noir_color_grading_profiles.csv exists and HectonVisorUberPostFeature.Noir.cs cold-loads it into Vault-backed NoirColorProfileDTO rows. This is not a Unity LUT texture route; it is first-party shader parameter grading.
- Fog: HectonUnderwaterVisuals owns RenderSettings fog writes, per-camera enforcement, depth fog density, Crest material depth fog, HUD fog readback, and background color binding. Renderer assets also include HectonNoirDepthFogFeature active.
- Light shafts: HectonScooterVolumetricShaftsFeature exists and is active in serialized Mobile/PC/High renderer assets. It declares zero world raymarch steps and fixed 2D radial taps at renderScale 0.5.
- AO: First-party HectonAbyssalSsdoFeature is active in serialized Mobile/PC/High renderer assets at renderScale 0.5. HectonVoxelSsaoFeature exists but HasRuntimeConsumer=false, so it is disabled by design. Multiple shaders/materials use packed mask/vertex/baked AO.
Rejected Alternatives: Do not recommend Unity URP SSAO as default; AGENTS forbids it. Do not treat ColorLookup in DefaultVolumeProfile as active production LUT because actual URP profile does not use DefaultVolumeProfile and Default ColorLookup has no texture/contribution.
Scalability potential: Low/compact = baked/material AO + fog + grading + probe reflections. Middle = half-res SSDO and controlled shafts. High/Ultra = higher shadows, screen-space shadows only where proven, stronger custom post.
Hardware Impact: Current audit saves 0 us. Serialized low/medium choices already avoid realtime reflection probes and screen-space lens flare, expected to protect low-end frame time.

Problem: Decide what is needed and what is useless.
Solution:
- Needed: keep baked static lighting/probe route; make probe zoning explicit per logical zone; keep one fog owner; keep first-party noir grading; verify Bloom/intensity-zero cost; keep custom cheap shafts only if profiler stays below budget; use material/vertex AO first and half-res SSDO only behind quality.
- Useless/harmful: realtime GI, SSR as low-tier default, every-frame realtime reflection probes, URP SSAO, full volumetric raymarch fog/god-rays on low hardware, ChromaticAberration plus LensDistortion as a default stack, many shadow-casting point lights for fake bounce.
Rejected Alternatives: Do not add a new system until existing HectonUnderwaterVisuals, HectonVisorUberPostFeature.Noir, HectonNoirDepthFogFeature, HectonScooterVolumetricShaftsFeature, and HectonAbyssalSsdoFeature are profiled.
Scalability potential: Minimum Survival uses authored/baked data and fog/color only. Middle adds half-res screen effects. High/Ultra increases shadow/probe/fog/SSDO quality continuously via GlobalQualityWeight.
Hardware Impact: Exact gain not measured. Expected largest low-end protection is avoiding realtime GI/SSR/full volumetrics; those are millisecond-class hazards on i3/MX350.

Problem: User requested actual improvement but warned not to interfere with another agent compiling.
Solution: Stay out of C# and build lanes. Modify only cold rendering data:
- `noir_color_grading_profiles.csv`: 3 sparse depth/stress rows -> 10 continuous rows.
- `noir_aesthetic_profiles.csv`: added missing fallback reconstruction profiles.
- `caustic_lighting_profiles.csv`: removed unreachable Hurricane row because parser resolves it to Storm and the first Storm row wins.
- `ocean_aesthetic_profiles.csv`: added editor-loaded ocean single-pass biome profiles for safe_shallows, kelp_forest, deep_abyss, sulfur_vents.
- `shoreline_foam_profiles.csv`: added first active shoreline foam profile.
Rejected Alternatives: No shader/C# edits, no new render feature, no disabling beauty effects, no dotnet build. Active `dotnet` PID 17540 means compile lane is occupied.
Scalability potential: Low/compact keeps the same passes and lets existing GlobalQualityWeight reduce intensity, falloff, active lanes, and perturbation. Middle gets stronger coherent fog/grading/caustic response. High/Ultra buys stronger noir pressure, reflection mix, foam/wake, grain/chroma/vignette without new simulation.
Hardware Impact: Runtime hot-path cost added is 0 us by construction: no code, no new render pass, no new shader sample. Caustic profile cleanup removes one dead profile row; theoretical CPU saving is below 0.01 us per profile scan and not measured. Visual gain is data coverage and reduced fallback/default reliance. STATUS: PENDING VERIFICATION until Unity import/play/profiler after compile lane clears.

Problem: Rendering data contracts could silently fall back to defaults.
Solution: Static-validated edited CSVs with exact column counts and invariant-culture numeric parsing:
- noir_color_grading_profiles.csv: 10 rows, 13 columns.
- noir_aesthetic_profiles.csv: 10 rows, 12 columns.
- caustic_lighting_profiles.csv: 5 rows, 7 columns.
- ocean_aesthetic_profiles.csv: 4 rows, 9 columns.
- shoreline_foam_profiles.csv: 1 row, 6 columns.
Rejected Alternatives: Unity import validation is deferred; forcing import/build now can interfere with the compile agent.
Scalability potential: Data rows cover low/middle/high/ultra behavior through existing continuous weights, not binary quality switches.
Hardware Impact: Static validation cost only. No runtime measurement taken. `git diff --check` passed with LF-to-CRLF warnings on two tracked CSVs only.

Problem: DRS upscaler profile ranges overlapped, and the parser selects the first profile whose min/max scale contains the current scale. Cheap profiles could therefore win across middle/high scale regions.
Solution: Replace four overlapping profiles with six monotonic contiguous bands: survival, compact, handheld, middle, high, ultra. Keep the existing CSV contract and parser. No new render pass, sample, allocation, or C# edit.
Rejected Alternatives: C# interpolation was rejected because another agent owns compilation. Heavy temporal reconstruction was rejected because it would add runtime cost before proof. Disabling reconstruction quality was rejected because the user explicitly wants beauty preserved.
Scalability potential: Survival uses tighter radius/lower weights for compact devices. Compact/handheld get controlled recovery without mid-tier jumps. Middle/high/ultra gain progressively larger reconstruction radii and weights through data, not binary switches.
Hardware Impact: Runtime hot-path cost added is 0 us. Low-end benefit is correct cheap profile selection; measured frame-time proof is absent. High-end keeps visual overkill through ultra radius 3.35 and higher weights.

Problem: Camera trauma loader uses the first CSV row radius as `LowTierRadiusMeters` and the last row radius as `UltraRadiusMeters`. The first `default` row had 72m radius, so weak devices could receive broad shake propagation before low_survival was even considered for the low-tier boundary.
Solution: Reduce first row radius to 32m while preserving translation/rotation gains at 1.0 and preserving last ultra row radius at 120m. This trims weak-tier propagation without deleting cinematic shake.
Rejected Alternatives: Removing camera trauma was rejected; it would cheapen presentation. Reducing all profile amplitudes was rejected because high/ultra should spend saved cycles on stronger cinema. Reordering rows was rejected because it would also alter first/last boundary authority.
Scalability potential: Low tier gets local shake. Middle gets 56m deck response. High gets 96m noir response. Ultra keeps 120m overkill propagation.
Hardware Impact: Runtime hot-path cost added is 0 us. Low-end event fanout may decrease if the runtime uses the low-tier radius for filtering; measured proof is absent.

Problem: `HectonMarineSnowRenderer` resolves `Assets/_SourceData/VFX/Propwash/vfx_silt_profiles.csv`, but the source file was missing. The renderer already has a background CSV route, so missing data left marine snow/silt on defaults instead of authored noir water volume tuning.
Solution: Add `vfx_silt_profiles.csv` with all six parser-supported keys: particle_count, curl_noise_strength, wake_influence, gravity_sinking_speed, ambient_silt_size, density_scale. Keep particle_count at the existing overkill cap and tune motion/density for deeper water believability.
Rejected Alternatives: New volumetric truth simulation rejected. Reducing particles rejected. C# route change rejected while compile lane is occupied.
Scalability potential: Existing renderer still applies GlobalQualityWeight, VRAM pressure, render scale, homeostasis pressure, and kill-switch budgets. Low devices keep reduced active counts through existing logic. High/Ultra keep 100000 allocation cap and stronger silt/wake response.
Hardware Impact: Runtime code cost added is 0 us. Existing background poll route may perform one extra file read when the file appears or changes; steady-state still gates by timestamp. Measured frame proof absent.

Problem: `HectonVisorARStencilRendererFeature` has `loadCsvProfiles=true` and resolves `Assets/_SourceData/Visor/visor_hud_profiles.csv`, but the folder/file was missing. The profile buffer stayed dependent on defaults/zeroed rows.
Solution: Add source-data Visor HUD profiles for default, pressure, warning, and abyssal states. Values drive existing font scale, curvature, fog edge, and primary color DTO fields only.
Rejected Alternatives: Shader/C# edit rejected. Disabling HUD fog/curvature rejected because that removes premium helmet presentation. Adding complex new HUD layout data rejected because parser supports only the existing fixed DTO subset.
Scalability potential: Low tier retains the same pass and can use the same profile rows. Middle/high/ultra get stronger curvature/fog/color identity without new passes. Further scaling remains owned by existing quality/time shader params.
Hardware Impact: Runtime hot-path code cost added is 0 us. Cold source hydration only. GPU cost is unchanged by file creation; visual effect depends on existing shader consumption and needs Unity/profiler proof.

Problem: `BiolumPulseSyncRuntime` resolves root `biolum_pulse_profiles.csv` for editor override data, but no current file existed. Existing biolum pulse/group sync therefore had no authored CSV override path.
Solution: Add root `biolum_pulse_profiles.csv` with four species color/frequency/wave rows and four `group0..group3` pulse phase/frequency/amplitude/spatial rows. This uses the existing parser and watcher.
Rejected Alternatives: New dynamic lights, new particle system, or C# shader edits rejected. Importing this under Assets rejected because the runtime path is project-root and a root CSV avoids Unity asset import.
Scalability potential: Low devices can keep existing runtime capacity/cadence while receiving coherent pulse data. Middle/high/ultra can display stronger synchronized glow with no additional simulation owner.
Hardware Impact: Runtime hot-path code cost added is 0 us. Existing editor watcher may load the file on creation/change. Measured GPU/CPU proof absent.

Problem: Final repository-wide diff hygiene cannot be claimed because unrelated agents modified `Docs/Tasks/CURRENT_BATCH.md` with trailing whitespace and many unrelated files are dirty.
Solution: Run scoped `git diff --check` against every file touched by `UNASSIGNED_AUDIT`. It passed. Do not mutate unrelated current batch or other agents' files.
Rejected Alternatives: Fixing unrelated whitespace rejected as cross-agent interference. Full build rejected because `dotnet` PID 17540 remains active.
Scalability potential: No runtime effect.
Hardware Impact: 0 us; proof scope is static file hygiene only.

Problem: `DiegeticVisorLensRuntime` resolves root `visor_properties.csv`, but the file was absent. That left lens fog, droplets, crack pressure, dirt, wipe strength, and low-refraction cutoff on code defaults only.
Solution: Add root `visor_properties.csv` with nine key,value rows matching the existing parser. Values preserve premium helmet grime/refraction while keeping low cutoff explicit.
Rejected Alternatives: Shader/C# lens edits rejected while compile is occupied. Disabling lens grime/refraction rejected because the user explicitly objected to removing beauty. Moving the file under Assets rejected because runtime resolves project root.
Scalability potential: Low uses stronger cutoff/clearing route already in code. Middle keeps readable fog/droplets. High/Ultra can keep stronger lens dirt/crack/refraction without adding a pass.
Hardware Impact: Runtime hot-path code cost added is 0 us. Existing cold CSV read path may hydrate once; measured frame proof absent.

Problem: `InteriorGIProbeVolumeRuntime` resolves `Docs/lighting_fixtures.csv`, but the file was missing. The project therefore had a route for cheap fake bounce/practical lights but no authored fixture source.
Solution: Add five parser-compatible fixture rows: warm corridor bounce, cyan service panel, red emergency strip, flora biolume pool, and helmet flash refill. These are no-shadow data sources for the existing interior GI probe volume, not Unity Light objects.
Rejected Alternatives: Realtime GI, shadow-casting point-light spam, and full volumetric bounce were rejected as low-end hazards. C# source registration changes rejected while other agents compile.
Scalability potential: Minimum/low can use smaller local fake sources and existing quality/cadence gates. Middle gets authored noir bounce. High/Ultra can spend saved light cost on stronger post/fog/SSDO while fixture data stays stable.
Hardware Impact: Runtime code/GPU pass cost added is 0 us. If CSV polling is enabled, existing cold IO loads five rows. Expected alternative avoided is millisecond-class dynamic GI/light cost on MX350-class hardware, but no profiler measurement was taken.

Problem: `DynamicPointLightCullingDirector` resolves `Docs/Data/light_culling_profiles.csv`, but the file was absent. The culling profile rule buffer had no authored data path.
Solution: Add six rows with parser-valid `name,priority,fade,intensity,sdfBias,flags` values: flare, emergency, biolume, practical, far_soft, ultra_glint. No headers/comments because the parser does not skip them.
Rejected Alternatives: A new profile interpolation system or C# hash route was rejected. Guessing scene-specific source profile hashes was rejected; rows are named rules for existing/consumer-owned matching.
Scalability potential: Low can bind far_soft/practical for tighter fade/intensity. Middle/high can bind flare/emergency/biolume. Ultra can bind ultra_glint for overkill practical highlights.
Hardware Impact: Runtime hot-path code cost added is 0 us. Matching rules may reduce submitted light work where source profile hashes are owned by producers; measured proof absent.

Problem: `ShinobuVoxelSculptorWindow` expects `Assets/_Project/Data/VFX/ShinobuDeltaCrusherTuning.csv`, but the editor source file was absent. The debris bake/tuning path lacked a stable source artifact.
Solution: Add one schema-valid row with gravity_y=-6.80, bounce=0.34, max_debris=384, mass_units_per_particle=3, plus Unity meta.
Rejected Alternatives: Runtime debris simulation changes rejected. Reducing debris globally rejected because high-tier presentation should stay rich. Binary bake not run because compile/import lanes are occupied.
Scalability potential: Low can bake/use lower active debris caps through existing quality constants. Middle keeps controlled bounce. High/Ultra can raise baked debris through the editor route when compile lane is clear.
Hardware Impact: Runtime hot-path code cost added is 0 us. This is editor source data only until a bake/import is run.

Problem: Several apparent CSV gaps are unsafe to author blindly.
Solution: Defer `shader_globals_override.csv` because it globally overrides shader fog/caustic state; defer `font_metrics_override.csv` because real atlas metrics are required; defer TerminalOS layout/decryption CSVs because terminal hashes/layouts are scene-specific.
Rejected Alternatives: Creating guessed global shader/font/terminal overrides rejected as likely visual or interaction regression.
Scalability potential: Correct future work should bind these through scene/atlas owner artifacts, not global guesses.
Hardware Impact: 0 us. This is a risk rejection, not a performance change.

Problem: Late data additions needed static proof without entering the compile lane.
Solution: Validated `visor_properties.csv` key,value rows, `Docs/lighting_fixtures.csv` 10-column rows, `Docs/Data/light_culling_profiles.csv` 6-column rows, and `ShinobuDeltaCrusherTuning.csv` 4-column row. Scoped `git diff --check` passed for late files.
Rejected Alternatives: Full build/import rejected because `dotnet` PID 17540 is active.
Scalability potential: Data rows preserve continuous quality routes and do not add binary quality switches.
Hardware Impact: 0 us static validation cost only. Runtime/profiler verification is still pending.

Problem: `Assets/_Project/Data/VFX/Beam/beam_visuals.csv` contained `quality_weight,1.0`. `ShinobuPlasmaBeamRuntime` already writes `GlobalQualityWeight` from `HomeostasisBrain`; the CSV parser can still write the same DTO field on hot-reload after the owner phase, creating a local one-frame quality stomp.
Solution: Remove the `quality_weight` row and add a CSV comment that beam quality is owned by `HomeostasisBrain.GlobalQualityWeight`. Radius, noise, heat, energy, biome extinction, requested beam count, and automatic radial segment scaling are preserved.
Rejected Alternatives: C# parser change rejected while another compile lane is active. Reducing beam count/radius rejected because the user explicitly warned not to remove beauty.
Scalability potential: Low keeps automatic radial/length segment scaling from global quality. Middle/high/ultra keep richer beam tubes without a local data file forcing ultra.
Hardware Impact: 0 us runtime code added. Avoided risk is a hot-reload frame forcing q=1.0; no profiler measurement taken.

Problem: `REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json` claimed to mirror `VfxComputeParticleBudgetCatalog`, but low/high/ultra particle counts and MX350 audit text were stale. The JSON falsely implied high quality was at the 512-group MX350 soft cap, while the runtime constants now allow 100000 marine-snow particles on high/ultra.
Solution: Update JSON tier rows to match runtime constants: Low 8512 total/8000 snow, High 104096 total/100000 snow, Ultra 105120 total/100000 snow. Update dispatch group audits and VRAM half-cut model from the same counts.
Rejected Alternatives: Leaving stale proof rejected because it can mislead future agents/tools into authoring unsafe high-tier defaults for weak hardware. Changing runtime constants rejected because it is C# compile-lane work and needs profiler proof.
Scalability potential: Low and middle remain MX350-safe authored targets. High and ultra are explicitly documented as visual-overkill paths that require non-MX350 hardware or continuous quality/pressure compression.
Hardware Impact: 0 us runtime impact. Proof accuracy improved; measured frame-time gain absent.

Problem: New VFX data edits needed validation without Unity import.
Solution: Parsed `beam_visuals.csv` as key,value rows and asserted no `quality_weight` remains. Parsed `REND_DYNAMIC_RESOLUTION_ADAPTER_compute_particle_budgets.json` with `ConvertFrom-Json` and checked tier constants. Scoped `git diff --check` passed with line-ending warnings only.
Rejected Alternatives: Unity import/build rejected because `dotnet` PID 17540 remains active.
Scalability potential: Continuous quality ownership remains intact; proof artifact now separates MX350-safe tiers from high/ultra overkill tiers.
Hardware Impact: 0 us static validation only.

Problem: Data scan found `Assets/_Project/Data/character_rig_constraints.csv` with `global_quality_weight,1.0`.
Solution: Traced the route to `KineticCharacterAnimator` parser/editor tooling. It is Animation/KCC presentation data, not the current rendering/VFX audit domain.
Rejected Alternatives: Editing it from this lane rejected as cross-domain interference. It may be legitimate rig authoring input and needs the animation owner to decide whether the quality field is override, seed, or editor preview value.
Scalability potential: No rendering decision made. If animation owner changes it later, it must preserve continuous quality and weak/middle/high/ultra iteration scaling.
Hardware Impact: 0 us; no change.

Problem: `TBDRPipelineSurgeonRuntime` had a default editor CSV path `Data/Rendering/gpu_budgets.csv`, but the file was missing inside the project. The editor/tuner route could not apply a cold-path protective budget artifact.
Solution: Add `Data/Rendering/gpu_budgets.csv` with one parser-compatible data line: 560000 visible vertices, 3600 transparent quads, 12.0 frustum squeeze degrees. This preserves beauty by keeping the existing rendering path and lets `GlobalQualityWeight` and tile pressure control squeeze strength.
Rejected Alternatives: Runtime C# constant edit rejected because Unity `dotnet` PID 17360 is active. A very low cap was rejected because it would harm presentation. Multiple quality rows were rejected because the ingestor consumes only the first numeric line.
Scalability potential: Low/weak devices get a conservative editor safety ceiling. Middle keeps enough geometry/transparency for noir composition. High/Ultra still use existing continuous quality and pressure routes; a future hardware-specific artifact should replace this one-line editor override if profiler data proves it.
Hardware Impact: Runtime hot-path code cost added is 0 us. Potential saved work is fewer submitted vertices/quads when the editor route is applied; exact microseconds require tuner/profiler proof.

Problem: `AbyssalShadowCullingRuntime` resolves `Docs/Tasks/shadow_culling_profiles.csv`, but the file was absent. The first-party shadow culling route had no authored profile rules for cheap-device vs ultra silhouette behavior.
Solution: Add five parser-valid rows: toaster_practical, low_noir, middle_noir, high_noir, ultra_overkill. Weak profiles use larger caster-radius thresholds and shorter distance scales. High/ultra profiles keep smaller casters and longer noir silhouettes. No shadow feature was added or disabled.
Rejected Alternatives: Disabling shadows rejected because the user explicitly said not to remove beauty. Global distance reduction rejected because it would flatten high-tier presentation. New shadow render feature rejected because it adds GPU work and compile risk.
Scalability potential: Low trims tiny casters and practical-light shadow churn. Middle keeps baseline silhouettes. High/Ultra spend saved work on long silhouettes and wider fade bands through existing profile rules and `GlobalQualityWeight`.
Hardware Impact: Runtime hot-path code cost added is 0 us. Expected low-end benefit is fewer shadow states uploaded/evaluated once profiles bind; measured proof pending.

Problem: `ShinobuMaterialResponseRuntime` also expects `Data/Visuals/texture_set_indices.csv`, but that CSV remaps texture-set hashes to texture array slice indices and then rewrites material states modulo row count.
Solution: Do not author this file blindly. It requires the actual texture-array bake/slice owner. Existing defaults already generate seeded slice indices, so a guessed CSV can make materials point at wrong slices.
Rejected Alternatives: Creating plausible names like rust/salt/biomass with guessed slice values rejected as visual regression risk. C# parser change rejected during active compile lane.
Scalability potential: Correct future work should come from the texture array bake artifact and support weak/middle/high/ultra content density without changing truth ownership.
Hardware Impact: 0 us; no change. Avoided risk is material corruption, not a measured performance gain.

Problem: New TBDR/shadow data needed proof without Unity import.
Solution: Parsed `gpu_budgets.csv` first numeric line, parsed five shadow profile rows with invariant-culture floats, and ran scoped `git diff --check` for both files.
Rejected Alternatives: Unity import/build rejected because Unity `dotnet` PID 17360 remains active.
Scalability potential: Data-only routes preserve continuous quality ownership and avoid binary quality switches.
Hardware Impact: 0 us static validation only.

Problem: `ShinobuOceanSurfaceAtmosphereRuntime` resolves `Assets/_SourceData/Atmosphere/weather_profiles.csv`, but the project did not contain that source profile. Ocean atmosphere, wave-lane, storm, foam, scatter, and surge values could silently fall back to defaults or the weaker Beaufort route.
Solution: Add `Assets/_SourceData/Atmosphere/weather_profiles.csv` plus meta. Use only parser-supported rows: key,value for global weather/scatter/surge values and key,index,value for four wave lanes. The data increases cinematic ocean coherence through existing water/fog/scatter controls instead of adding simulation.
Rejected Alternatives: `beaufort_scale_profiles.csv` was rejected because `TryApplyBeaufort` hashes state names into profile slots, while `FillWaveParameters` reads the magic `QSTP` profile slot. Without a guaranteed magic slot route, a guessed Beaufort CSV is likely dead data or collision-dependent. C# repair was rejected because Unity `dotnet` PID 17360 owns the compile lane.
Scalability potential: Low uses authored wind/foam/scatter with existing quality and pressure clamps. Middle gets stable multi-lane waves and controlled storm/rain. High/Ultra can preserve stronger gas-giant glow, scatter, and long-wave interference without extra passes.
Hardware Impact: Runtime hot-path code cost added is 0 us. GPU pass count added is 0. Benefit is fewer default/fallback states and more believable water from existing shader/runtime routes; measured frame proof pending.

Problem: `ToxicOutgassingChemistryRuntime` resolves `Data/Tuning/chemical_properties.csv`, but the file was absent. Chemical diffusion, advection, corrosion, flora absorption, density decay, source radius, and visual thresholds had no authored tuning artifact.
Solution: Add `Data/Tuning/chemical_properties.csv` with all ten parser-supported FNV-keyed rows. Values are bounded and still pass through `SanitizeConstants`, preserving the runtime authority contract.
Rejected Alternatives: New toxic gas simulation, extra per-particle chemistry, or scene changes rejected. This should remain a cheap field/tuning route, not a particle-heavy truth system.
Scalability potential: Low can keep small source radius and decay-heavy readability through existing clamps. Middle gets credible acid/biolum thresholds. High/Ultra can make gas zones visually richer through existing thresholds without changing gameplay truth ownership.
Hardware Impact: Runtime hot-path code cost added is 0 us. Existing cold CSV load only. Avoids future pressure to represent chemistry with expensive particles; exact microseconds saved are not measured.

Problem: New atmosphere and chemistry source data needed proof without Unity import.
Solution: Static-validated 36 weather rows and 10 chemistry rows against parser-supported keys, optional wave indices, and invariant-culture numeric parsing. Ran scoped `git diff --check`.
Rejected Alternatives: Unity import/build rejected because Unity `dotnet` PID 17360 remains active.
Scalability potential: Both files feed existing continuous-quality routes; no binary low/ultra switch was introduced.
Hardware Impact: 0 us static validation only. Runtime/profiler verification remains pending.

Problem: Read-only URP/VFX audit found renderer hot-path architecture risks that are not safe data-only fixes.
Solution: Record the risks for the next compile-safe pass:
- `ParasiteSwarmGpuRuntime.cs` executes a command buffer and procedural indirect draw from the hot visual flow.
- `HectonMarineSnowRenderer.cs` renders via direct `Graphics.DrawProceduralIndirect`.
- `ShinobuPlasmaBeamRuntime.cs` uploads buffers and draws from visual sync.
- `HectonDrsRenderFeatureGate.cs` can poll `GlobalRegistry` on hot cache miss.
- `HectonVolumetricParticulateFogFeature.cs` reads shader global state every late-frame tick.
- Several shaders contain hard quality thresholds and high multi_compile counts.
Rejected Alternatives: Do not patch C#/shader files while Unity `dotnet` PID 17360 is active. Do not hide the issue by lowering visual quality or adding CSV toggles. The correct fix is graph-owned submission or a formal documented bridge with profiler proof.
Scalability potential: Low should use graph-owned/pressure-owned submission with predictable cadence and no hot registry search. Middle/high/ultra should spend saved CPU/GPU on richer particles, shafts, beams, and silhouettes through continuous quality, not direct unowned draws.
Hardware Impact: 0 us measured in this pass. Potential low-end gain is millisecond-class only if direct submissions and hot polls are migrated later and proven in profiler.

Problem: Rendering data scout marked `beaufort_scale_profiles.csv` as cold-data safe, but primary parser review found a route mismatch.
Solution: Keep the primary rejection. The weather CSV was added because `OceanWeatherCsvParser.TryApply` directly writes supported keys and indexed wave lanes. Beaufort CSV remains deferred because its state-name hash slots are not guaranteed to satisfy the runtime `QSTP` profile check.
Rejected Alternatives: Creating a plausible Beaufort table would look productive but may not affect runtime wave fill. C# repair is compile-lane work.
Scalability potential: Current weather file gives low/middle/high/ultra water tuning through existing continuous routes. A future Beaufort fix should bind deterministic named weather states without changing gameplay authority.
Hardware Impact: 0 us; this is correctness risk avoidance.

Problem: `LutArrayResolver` has a player-safe lookup for `Assets/StreamingAssets/Data/Visuals/Water_Extinction_Matrix.bin`, but only root `Data/Visuals/Water_Extinction_Matrix.bin` existed. Editor/current-project contexts could load the LUT, while player builds could fall back to analytical Beer-Lambert despite the 393216-byte matrix being available.
Solution: Copy the existing `Data/Visuals/Water_Extinction_Matrix.bin` into `Assets/StreamingAssets/Data/Visuals/Water_Extinction_Matrix.bin` and add Unity meta files for `Data`, `Data/Visuals`, and the binary asset. Verified both files are 393216 bytes and share SHA256 `99EB8631F64C15181E62E2C1E24CDA285A490330C0F04F99FB218EFEC9BBFF89`.
Rejected Alternatives: C# resolver rewrite rejected because Unity `dotnet` PID 17360 is active. Forcing LUT on low-memory or portable targets rejected because current code intentionally uses analytical fallback at <=2048 MB graphics memory, Steam Deck-like, Android, and visionOS lanes.
Scalability potential: Low/portable lanes keep the cheap analytical fallback by existing code. Middle/high/ultra packaged builds can use the authored R16 extinction matrix for richer water absorption/scattering without new shader code or extra simulation.
Hardware Impact: Runtime code cost added is 0 us. VRAM cost for allowed tiers is the existing 768x256 RHalf texture path, not a new pass. Exact frame/visual impact remains pending Unity import/player verification.

Problem: Shader scout found hard quality cliffs and variant pressure in rendering shaders that cannot be safely solved through data files.
Solution: Keep them as compile/import backlog:
- `TerrainMaster.shader`: 15 variant pragma lines, above mandate cap.
- `Core/Hecton8_UberNoir.shader`: 12 variant pragma lines and repeated instancing variants.
- `Hecton_AbyssalVoxelRock.shader`: 10 variant pragma lines.
- `Hecton_SonarRaymarch.compute`: `_GlobalQualityWeight < 0.3` switches SDF sampling abruptly.
- `SargassumMicroFaunaBoids.compute`: binary FULL/SIMPLIFIED/SLEEP tiers with no quality weight in file.
- `Hecton_LeviathanOrganic.shader`: `bodyFxQuality > 0.5h` gates hero effects.
- `HectonVisorUberPost.shader`: unrolled 16-tap loop pays full sample cost while masking contribution.
- Deferred decal/visor trauma/wounds shaders can scan 128 decals per pixel without quality-capped loop count.
Rejected Alternatives: Shader edits rejected during active Unity `dotnet` because `.shader` and `.compute` changes can trigger import/variant churn and need Frame Debugger/profiler proof. Disabling effects rejected because it would violate the visual-currency requirement.
Scalability potential: Future fix should use smooth effect weights, continuous sample caps, dithered fades, nearest/trilinear blend ramps, and variant stripping without binary low/ultra cliffs. Low gets real cost reduction; middle/high/ultra keep visual overkill through richer samples/effects.
Hardware Impact: 0 us measured in this pass. Potential future low-end wins are shader import/profiler-dependent and must be measured.

Problem: `BiolumPulseSyncRuntime.BuildColdProfilePath()` loads `Biolum_Profiles.bin` from `Application.streamingAssetsPath` in player builds, but the project only had root `Data/Visuals/Biolum_Profiles.bin`. Editor could hydrate authored profile floats; packaged builds would return null and seed default profiles.
Solution: Copy `Data/Visuals/Biolum_Profiles.bin` to `Assets/StreamingAssets/Biolum_Profiles.bin` and add a Unity meta file. Verified source and target are 25936 bytes and share SHA256 `1C7DB3B6FD0FC24541B078BF9DEEBBCAA9EB9AAE2DCC6F4F25D749439A3B0FEB`.
Rejected Alternatives: C# resolver rewrite rejected while Unity `dotnet` PID 17360 is active. Copying `gerstner_wave_weather.bin` was rejected because its legacy load route is wrapped in `#if UNITY_EDITOR`; player copy would be dead data. Copying gas toxicity binaries to StreamingAssets was rejected because the current probe route does not read StreamingAssets and needs C# ownership.
Scalability potential: Low/compact builds still scale particle/glow work through existing `GlobalQualityWeight`; packaged mid/high/ultra builds retain authored biolum colors/frequency/pulse profiles instead of generic seeded defaults. No new draw or simulation pass.
Hardware Impact: Runtime code cost added is 0 us. StreamingAssets file increases package size by 25936 bytes. Runtime frame impact is pending Unity/player verification.

Problem: Additional rendering/visor/world fallback probes name several binary artifacts, but no validated source binary exists in the project tree.
Solution: Search for candidate payload names from source probes before copying anything: `global_shader_constants.h8bin`, `lighting_palettes_007.bin`, `mobile_vertex_limits.h8bin`, `texture_streaming_budgets.bin`, `visor_materials_006.h8bin`, `surface_nets_lut.h8bin`, `marching_cubes_edge_tables.bin`, `volcanic_vent_locations.h8bin`, `seed_ship_emission_rates.h8bin`, `glitch_zones_007.bin`, `flora_genetics.h8bin`, `l_system_axioms_006.h8bin`, and `botanical_traits.bin`. Search returned no valid source payloads. Existing `vehicle_wake_profiles.csv`, water optics, visual aging, degradation, parasite, atmosphere, and flora sway profile files already exist and are not missing.
Rejected Alternatives: Do not fabricate binary files to satisfy probes. Do not copy root/project CSVs into StreamingAssets unless the runtime resolver actually reads `Application.streamingAssetsPath` for that file. Do not mutate shader/C# resolver paths while Unity `dotnet` is active; latest post-patch process check saw PID 11560.
Scalability potential: Correct future binary work must come from the owning bake/import tool and preserve low/middle/high/ultra continuous `GlobalQualityWeight` behavior. Placeholders would only create false readiness.
Hardware Impact: 0 us runtime change. This prevents package bloat and invalid "found binary" branches that could disable mock/default fallback with unusable payloads.

Problem: Active biome ids in `Assets/_Project/Data/World/biome_atmosphere_rules.csv` did not match the authored day/night and interior fake-GI lighting profile names.
Solution: Replace `Docs/Data/lighting_gradient_profiles.csv` rows with `safe_shallows`, `kelp_forest`, `deep_abyss`, and `sulfur_vents`, preserving the existing 6-column parser contract. Add matching rows to `Docs/Data/Profiles/ambient_lighting_profiles.csv` while retaining legacy fallback rows. This lets `HectonLightingRuntime_DayNightRelay.ResolveOneProfile()` and `InteriorGIProbeVolumeRuntime.ResolveProfileTint()` resolve by hash/ProfileId instead of stale index fallback.
Rejected Alternatives: C# resolver changes, shader changes, and Unity import/build were rejected because Unity `dotnet` PID 19384 is active. Leaving `open_ocean`, `mushroom_cave`, `radioactive_trench`, and `abyssal_blue` as the only gradient rows was rejected because current nonzero biome hashes can miss and then return profiles by index, e.g. deep abyss could inherit a red trench color.
Scalability potential: Low tier gets correct cheap fog/ambient color without extra samples or lights. Middle gets coherent biome fake-GI tint. High and Ultra retain stronger zone identity through existing gradient/ambient profile buffers and continuous `GlobalQualityWeight`; no binary quality switch was introduced.
Hardware Impact: Runtime code cost added is 0 us. Cold data rows only. Static validation parsed 4 active lighting-gradient rows, 7 ambient rows, and 4 active ambient biome rows. Scoped `git diff --check` passed with LF/CRLF warnings only. Runtime/profiler verification remains pending.

Problem: `Docs/Data/Profiles/water_extinction_profiles.csv` put `default_abyss,0..20000` before the authored silt, vent, and brine profiles. `HectonVolumetricParticulateFogFeature.ApplyExtinctionProfileFromVault()` returns on the first depth match, so every nonzero specific water extinction look was effectively unreachable after CSV load.
Solution: Convert the water extinction ranges to monotonic, non-overlapping depth bands: default 0..40m, silted wreck 40..900m, vent blackwater 900..3500m, brine noir 3500..20000m. This keeps the existing cheap depth-fog/extinction fake and makes the authored expensive-looking water looks actually selectable without adding raymarch steps, lights, textures, or shader samples.
Rejected Alternatives: C# priority resolver changes and shader changes were rejected because Unity `dotnet` PID 17936 is active and CPU load is 93%. Keeping the overlap was rejected because it silently collapses visual variety to one broad profile. Deleting silt/vent/brine was rejected because the user explicitly forbade making the picture worse.
Scalability potential: Low gets the same one-row first-match cost and correct depth color for free. Middle gets reachable silt/vent transitions. High and Ultra keep stronger brine/vent mood through the existing profile buffer and can spend saved simulation cost on richer post/fog elsewhere.
Hardware Impact: Runtime code cost added is 0 us. Static validation parsed 4 rows with monotonic ranges. Scoped `git diff --check` passed with LF/CRLF warning only. Runtime/import/profiler verification remains pending.

Problem: `Assets/_Project/Data/upscaler_quality_profiles.csv` still had 0.01-wide gaps between the six DRS profile bands, while `BilateralDrsUpscalerContracts` selects a row only when render scale is inside inclusive `MinScale01..MaxScale01`. A render scale like 0.445, 0.585, 0.695, 0.805, or 0.915 could miss every authored profile and fall back to default reconstruction tuning.
Solution: Move adjacent band boundaries to shared inclusive values: 0.45, 0.59, 0.70, 0.81, and 0.92. This keeps the same six profiles and preserves survival/compact/handheld/middle/high/ultra visual progression without adding runtime work.
Rejected Alternatives: C# interpolation or fuzzy range matching was rejected because Unity `dotnet` PID 17936 remains active. Widening only low-tier bands was rejected because it would let cheap settings steal mid/high render scales. Leaving gaps was rejected because it violates the claimed continuous scalability curve.
Scalability potential: Low remains cheap at 0.25..0.45. Middle/high get deterministic reconstruction profile coverage instead of accidental default fallback. Ultra keeps 0.92..1.0 overkill radius/weights. The curve stays continuous enough for hardware breathing without binary quality switches.
Hardware Impact: Runtime code cost added is 0 us. Static validation parsed 6 rows, 8 columns each, contiguous first=0.25 last=1.00. `git diff --check` passed with LF/CRLF warning only. Runtime/import/profiler verification remains pending.

Problem: `HectonVisorUberPostFeature.ResolveAestheticCsvPath()` checks `Data/Visuals/noir_aesthetic_profiles.csv` before `Assets/_Project/Data/noir_aesthetic_profiles.csv`. The root Data/Visuals file still had the old 4-row reconstruction table, so the newer 10-row fallback profile set in `Assets/_Project/Data` could be shadowed and never loaded.
Solution: Replace `Data/Visuals/noir_aesthetic_profiles.csv` with the same 10 data rows as the authored Assets profile set, plus a resolver-order note. This fixes the active cold-load source without changing code, shaders, or render passes.
Rejected Alternatives: C# resolver order changes were rejected because Unity `dotnet` PID 17936 remains active. Deleting the root file was rejected because the resolver explicitly prefers it and deletion could fall back differently depending on working directory. Keeping the old 4 rows was rejected because it removes midwater/failure/blackbox reconstruction coverage.
Scalability potential: Low keeps cheap reconstruction constants but now gets deterministic surface/mid/abyss bands. Middle gets pressure/failure rows instead of coarse thermocline fallback. High/Ultra retain blackbox/overkill reconstruction styling through the same existing shader params and `GlobalQualityWeight` route.
Hardware Impact: Runtime code cost added is 0 us. Static validation parsed 10 rows, 12 columns each, and data rows mirror `Assets/_Project/Data/noir_aesthetic_profiles.csv`. `git diff --check` passed with LF/CRLF warning only. Runtime/import/profiler verification remains pending.

Problem: Follow-up scan needed to avoid accidental visual regression while another agent still owns compilation.
Solution: Read water optics, ocean single-pass, shoreline foam, screen-space shaft, dynamic light culling, abyssal shadow culling, and URP postprocess contracts. Do not edit valid routes. Quest renderer already has `HectonScooterVolumetricShaftsFeature` inactive, which matches the Quest stripping validator. PC/mobile renderer assets keep the cheap half-res fake shaft feature active.
Rejected Alternatives: Enabling Quest shafts was rejected because the validator requires them stripped. Disabling PC/mobile shafts, Bloom, or vignette was rejected because it removes beauty. Adding guessed water/foam rows was rejected because current parser contracts already have valid rows.
Scalability potential: Low and VR keep forbidden shafts stripped where required. Mobile/PC keep cheap fake shafts and half-res visual tricks. High/Ultra keep stronger post/noir/water profile coverage through existing continuous weights.
Hardware Impact: 0 us runtime change. The value is risk avoidance: no new render pass, no shader import, no compile collision.

Problem: Dynamic point-light and abyssal shadow CSV rows are profile-hash contracts, but the apparent human-readable rule names do not prove binding to real producers.
Solution: Inspect parsers and jobs. Dynamic light rules hash the first CSV token as text and compare it to `DynamicPointLightSourceDTO.ProfileHash`. Mock sources generate `ProfileHash = Hash32(seed ^ 0x0C15E551)`, so arbitrary names such as `flare` or `practical` parse but do not necessarily bind. Abyssal shadow rules similarly compare `ShadowCullInstanceDTO.ProfileHash`.
Rejected Alternatives: More named rows, hex-looking text rows, or broad wildcard data were rejected. The parser does not parse the first token as a numeric hash and there is no wildcard route. Correct repair needs a compile-safe hash publication contract or an editor report that emits the real producer hashes.
Scalability potential: Low/middle/high/ultra shadow and light budgets should bind to producer-owned hashes deterministically. Until then, defaults remain safer than fake profile coverage.
Hardware Impact: 0 us runtime change. Avoids dead data and false confidence; future measured savings require profile-hash owner integration and profiler proof.

Problem: Postprocess could be "optimized" destructively if treated as generic cost instead of visual currency.
Solution: Inspect active URP VolumeProfiles. `SampleSceneProfile` and `SampleSceneProfile_High` already carry Bloom, Tonemapping, ShadowsMidtonesHighlights, WhiteBalance, Vignette, and inactive MotionBlur. No edit was made because the current profile state already preserves cinematic cheat layers without adding new systems.
Rejected Alternatives: Disabling Bloom/vignette was rejected because the user explicitly forbade making visuals worse. Enabling ChromaticAberration/MotionBlur as default was rejected because it is a low-tier cost and nausea/clarity risk without profiler and UX proof.
Scalability potential: Low/middle keep controlled Bloom/vignette/grading. High/Ultra can overdrive noir through first-party profiles and high profile assets rather than defaulting to expensive lens defects.
Hardware Impact: 0 us runtime change. No build/profiler run because `dotnet` PID 17936 remains active.

Problem: Baking, AO, fog, and reflection items from the cinematic-cheat checklist needed objective classification before touching scenes or ProjectSettings.
Solution: Static-inspect scene YAML, `QualitySettings.asset`, `GraphicsSettings.asset`, URP renderer assets, and packaged lighting artifacts. Build scenes keep baked lightmaps enabled and realtime lightmaps disabled. `02_HECTON_WORLD` has `Assets/_Project/Scenes/02_HECTON_WORLD/LightingData.asset` and `ReflectionProbe-0.exr`, so world-level baked/reflection data exists. All quality tiers set `realtimeReflectionProbes: 0`. PC/mobile/high renderer assets keep cheap volumetric particulate fog and scooter shaft fake features active where allowed; Quest keeps the scooter shaft feature inactive. Text scenes and sandboxes have `m_AO: 0`, `m_ExtractAmbientOcclusion: 0`, and null `m_LightingDataAsset`; `GraphicsSettings.asset` keeps all lightmap/fog variants.
Rejected Alternatives: Do not flip AO flags by text edit; Unity must rebake lighting or the setting is a false proof artifact. Do not change `m_LightmapStripping`/`m_FogStripping` while compile/import is busy; variant stripping can remove required fog/lightmap modes and needs build proof. Do not enable realtime reflection probes; probes are already correctly cheap/faked.
Scalability potential: Low keeps baked/static lighting and no realtime probe cost. Middle keeps authored baked world probe and fog fake routes. High/Ultra can spend visual budget on richer authored bakes, post, shafts, and fog profiles after a controlled bake/import pass. No low-vs-ultra binary switch was introduced.
Hardware Impact: 0 us runtime change in this pass. Avoided high-risk import churn while CPU was 85-100% and Unity `dotnet` PID 17936 was active. Future low-end gain from variant stripping or AO rebake requires Unity import, Frame Debugger, and profiler proof.
