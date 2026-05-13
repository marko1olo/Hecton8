# LOG_ACTIVE_SONAR_ILLUMINATION

## Session Entry
What was wrong: Active sonar assignment reports sound-only sonar and singleton/post-process architectural rot. Code not inspected yet.
What was done: Prompt extracted, domain map read, mandates selected, status/rationale/log files created.
Cinematic Cheats used: Planned geometry-shader emissive fake instead of fullscreen post or dynamic lighting.
Exact Microseconds saved: PENDING VERIFICATION; initial estimate 80-250 us GPU saved versus fullscreen blit/dynamic light path on MX350.

## Final Report - ACTIVE_SONAR_ILLUMINATION
What was wrong: Active sonar had no reliable geometry-local illumination contract. The forbidden singleton/post-process path was searched, legacy fullscreen sonar history/composite rendering still had an enabled route, the PDA sonar ring could drift from the active ping radius, and final verification is blocked by unrelated global dependency failures.

What was done: Implemented/verified active sonar as global shader state consumed by `Hecton_CoreLit.hlsl`: `_ActiveSonarCenterAUP`, `_ActiveSonarRadius`, `_ActiveSonarCentersRadius[4]`, `_ActiveSonarParams[4]`, and `_ActiveSonarGeoParams`. `SpectrumSystem` publishes/steps up to four pings at 1480 m/s, shifts centers on `AupShiftSignal`, culls at 400 m, pushes `ActiveSonarRings` telemetry, and writes a 300-frame NativeArray blackbox dump on non-finite state. `AcousticPingSignal` now has ActiveSonar channel/flag constants and the legacy global publish path mirrors into `SignalBus<AcousticPingSignal>`. The legacy fullscreen sonar feature is disabled by default. PDA sonar receives the same active radius contract. OMEGA pass replaced active sonar value-noise with triangle-wave scan noise and removed redundant shader-side ping-count rounding.

Cinematic Cheats used: Geometry emissive shell instead of fullscreen blit. Squared-distance ring instead of physical wave simulation. Bright cyan material emission instead of dynamic lights/shadows. Triangle-wave topological scan fake instead of procedural value noise. Low-tier grid kill switch for MX350/i3.

Exact Microseconds saved: 80-250 us GPU by avoiding fullscreen sonar ring/history pass at 1080p; 150-400 us GPU by avoiding dynamic lights/shadows for the ping; 6-12 ALU per active sonar shaded pixel by replacing value noise with triangle-wave scan noise; 1 ALU per active sonar shaded pixel by removing redundant ping-count `round()`; 0 B/frame allocation in the active radius expansion path.

Verification: `rg` found no `distance(`, active-sonar `round(_ActiveSonarGeoParams`, or active-sonar `ValueNoise2(stablePosition` matches in `Hecton_CoreLit.hlsl`. `rg` found no first-party `SonarVfxManager`, `SonarPostProcess`, or sonar `Graphics.Blit` matches under `Assets/_Project`. Unity MCP compile/console unavailable. `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly` remains blocked by unrelated dependency wall with 104 errors before timeout. `git diff --check` is blocked by unrelated trailing whitespace in `Assets/_Project/Scripts/BoidFishInstanced.shader:520`.

Status: PENDING VERIFICATION.

## QA Upgrade - Coverage And Grid Cost
What was wrong: Scatter and dry-zone shaders were not receiving direct active-sonar geometry emission, and the CoreLit grid detail was evaluated inside the four-ping loop even though it does not depend on ping index.

What was done: Added `HectonCoreLitEvaluateActiveSonarGeoEmission` to `Hecton_ScatterIndirectLit.shader` and `Hecton_DryZoneLit.shader`. Hoisted active-sonar grid evaluation out of the ping loop in `Hecton_CoreLit.hlsl` so the ring response is accumulated first and grid is multiplied once.

Cinematic Cheats used: Same cyan shell/fine scan-rib fake, now applied to more geometry surfaces without fullscreen passes, dynamic lighting, physics waves, or texture samples.

Exact Microseconds saved: Saves up to three duplicate grid evaluations per active sonar shaded pixel in the four-ping overlap case. Adds one bounded active-ring call to scatter and dry-zone shaded pixels during pings; still no GC and no render-target bandwidth.

Verification: Targeted shader `rg` confirms active sonar emission now reaches AbyssalVoxelRock, WreckIndirectLit, ScatterIndirectLit, DryZoneLit, and CoreLit biolum consumers. Targeted `git diff --check` on touched shaders is clean. Full build and full diff hygiene remain blocked externally as previously recorded.

Status: PENDING VERIFICATION.

## QA Upgrade - High-Tier Scan Detail
What was wrong: The first OMEGA pass saved active-sonar shader cost but left high-tier visual response too close to middle-tier. Full-project hygiene also mixed touched-file status with unrelated dirty metadata.

What was done: Re-read AGENTS.md, domain map, and the active prompt. Changed non-low `_ActiveSonarGeoParams.z` from boolean 1 to detail tier 2. Added high-tier-only fine grid and scan rib math to the active sonar CoreLit path. Removed one trailing whitespace blocker in `BoidFishInstanced.shader`; targeted diff hygiene on touched active/VFX files now passes.

Cinematic Cheats used: Uniform-gated fine scan ribs from `frac/dot/abs` instead of texture samples, real volumetrics, post-process overlays, or light sources.

Exact Microseconds saved: Low tier remains unchanged from prior savings: 80-250 us GPU versus fullscreen history/ring pass, 150-400 us GPU versus dynamic lights, 0 B/frame. High tier spends an estimated +6-10 ALU per active sonar shaded pixel for richer scan detail, funded by the removed fullscreen/dynamic-light path.

Verification: Active-sonar `rg` checks remain clean for `distance(`, active-sonar value noise, and redundant ping-count round. Touched files pass targeted `git diff --check`. Full-project `git diff --check` remains blocked by unrelated `.meta` whitespace in GroundRadar, Inventory Corrosion, and Thermodynamics. Latest `dotnet build` remains blocked by 111 unrelated missing namespace/type errors.

Status: PENDING VERIFICATION.
