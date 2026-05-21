# ATMOSPHERE_AND_SCALE_099

Date: 2026-05-07
Status: PENDING VERIFICATION

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Project: HECTON-8

Source target: `C:\GOG Games\SpaceEngine`

Output scope: documentation only. No `Assets/` files were modified.

Operation status: MINING COMPLETE for accessible static data.

Verification status: PENDING VERIFICATION. This document has not been converted into runtime code, compiled in Unity, profiled on MX350, or validated against HECTON-8 scenes.

## Mandates Applied

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

The report path is outside the normal `Docs/Reports/` location because the user explicitly required `Docs/SPACE_ENGINE_RESEARCH/ATMOSPHERE_AND_SCALE_099.md`. This is a research artifact, not an implementation package.

## Hard Boundary

`C:\GOG Games\SpaceEngine\data\shaders\Shaders.pak` is a ZIP archive with 137 entries. Python `zipfile` reported `flag_bits=1` for every entry checked, meaning encrypted entries. `tar -tf` listed shader names, but extracting full GLSL stopped with a passphrase error.

No passphrase bypass, cracking, or decryption attempt was performed.

Therefore this report cannot honestly include full raw GLSL source. It uses:

- shader archive manifest names,
- unencrypted atmosphere and planet configuration files,
- shader binary/cache symbol strings,
- clean-room HLSL/Burst translations from the exposed parameter model and standard published scattering equations.

## Surgery Log

Raw GLSL status: blocked by encrypted `Shaders.pak` entries. The requested "raw GLSL alongside translation" cannot be satisfied without decryption. The accessible substitute is listed below:

- raw shader manifest evidence: encrypted shader filenames and module names;
- raw config evidence: atmosphere, gas giant, depth buffer, and ring parameters from unencrypted `.cfg` / `.sc` files;
- raw binary symbol evidence: uniforms, samplers, and shader variant names from cache binaries;
- translation evidence: clean-room URP HLSL and Burst reference kernels in this report.

This is the maximum defensible static extraction without bypassing encryption or importing proprietary source.

## Evidence Index

### Shader Archive Manifest

Archive: `C:\GOG Games\SpaceEngine\data\shaders\Shaders.pak`

Relevant encrypted entries listed by archive table:

```text
ag_common.glh
ag_copyInscatter1.glsl
ag_copyInscatterN.glsl
ag_copyIrradiance.glsl
ag_inscatter1.glsl
ag_inscatterN.glsl
ag_inscatterS.glsl
ag_irradiance1.glsl
ag_irradianceN.glsl
ag_transmittance.glsl
atmo_common.glh
atmo_transm.glsl
eclipse_common.glh
emu_double.glh
gr_geodesic.glh
gr_intersection.glh
planet.glsl
rings.glsl
rings_common.glh
rings_raymarch.glsl
sky.glsl
terrain_noise.glh
tg_common.glh
tg_gasgiant_color.glsl
tg_gasgiant_glow.glsl
tg_gasgiant_height.glsl
water.glsl
einstein.glsl
corona_plane_proc.glsl
```

Interpretation: SpaceEngine 0.9.9 separates atmosphere generation into precomputed transmittance, irradiance, and inscatter passes, matching a Bruneton-style atmospheric LUT pipeline. It also has explicit double-emulation, GR intersection/geodesic, gas giant terrain generation, ring raymarch, eclipse, and corona shader modules.

### Runtime Log Evidence

File: `C:\GOG Games\SpaceEngine\system\se.log`

Observed facts:

- shader language target: `#version 460 core`;
- binary shader program support enabled;
- `data/models/atmospheres/Atmospheres.pak` loaded;
- `data/shaders/Shaders.pak` loaded;
- gas giant passes loaded: `tg_gasgiant_height`, `tg_gasgiant_color`, `tg_gasgiant_glow`;
- planet atmosphere/eclipses/rings shader variants loaded;
- `einstein` warp shaders loaded;
- Bruneton tone shaders loaded;
- Earth atmosphere model loaded;
- corona and accretion jet shaders loaded.

### Shader Cache Symbol Evidence

The shader sources were encrypted, but cache binaries exposed uniform and sampler symbols.

Atmosphere transmittance cache:

```text
AtmoMieExt
AtmoParams1
AtmoParams2
AtmoRayleigh
EyePos
ModelViewProj
Radiuses
AtmoTransmUniforms
OutColor
```

Planet atmosphere and eclipse cache:

```text
AmbientColor
AtmoColAdjust
AtmoMieExt
AtmoParams1
AtmoParams2
AtmoParams3
AtmoRayleigh
EclipseCasters[0]
LightColor[0]
LightParams[0]
LightPos[0]
LogZParams
Radiuses
RingsParams
inscatterSampler
irradianceSampler
transmittanceSampler
```

Sky rings and eclipse cache:

```text
RingsMap
RingsParams
AtmoRayleigh
AtmoMieExt
inscatterSampler
transmittanceSampler
```

Volumetric ring raymarch cache:

```text
BlueNoiseMap
CamSubringIndex
DensityParams
DepthBuffer
DetailParams
EclipseCastersDust[0]
EclipseCastersStones[0]
FadeParams
HapkeParams
HapkeParams2
HapkeParamsP
HostLightIndex
InverseModelViewHi
InverseModelViewLo
InverseProjection
LightingParams
NoiseMap
NoiseParams
PlanetParams
RingsSize
ScaleParams
SubringParams
TexSampleParams
Rings3DUniforms
```

Black hole / accretion cache:

```text
DepthBuffer
DiskParams1
DiskParams2
DiskParams3
DiskParams4
DiskParams5
InverseModelViewHi
InverseModelViewLo
InverseProjection
JetParams1
JetParams2
JetParams3
MetricParams
MvpImp
MvpObj
NoiseParams
Radiuses
StarParams
WarpMap
```

Gas giant cache:

```text
HeightMapArray
arrayParams
canyonsParams
climateParams
cloudsParams1
cloudsParams2
colorParams
colorParams2
cycloneParams
cycloneParams2
mainParams
scaleParams
textureParams
```

Interpretation: the rendering model is not one monolithic shader. It is a parameterized atmosphere LUT pipeline with planet, sky, eclipse, ring, ring raymarch, gas giant procedural texture, and relativistic warp variants.

## Config Evidence

### Atmosphere Models

Archive: `C:\GOG Games\SpaceEngine\data\models\atmospheres\Atmospheres.pak`

Text file: `atmospheres.cfg`

Representative exposed values:

```text
Earth:
RadiusGround 6360
Height 60
RayleighH 8
MieH 2
MieG 0.8
RayleighBeta (0.0058, 0.0135, 0.0331)
MieScaBeta (0.004, 0.004, 0.004)
MieExtBeta (0.004, 0.004, 0.004)

Jupiter:
RayleighH 12
MieH 2
MieG 0.8
RayleighBeta (0.0117, 0.0135, 0.018)
MieScaBeta (0.004, 0.004, 0.004)
MieExtBeta (0.004, 0.004, 0.004)

Titan:
RayleighH 10
MieH 8
MieG 0
RayleighBeta (0.004, 0.004, 0.01)
MieScaBeta (0.001, 0.01, 0.06)
MieExtBeta (0.001, 0.01, 0.06)

Neptune:
RayleighH 8
MieH 4.5
MieG 0.6
RayleighBeta (0.0058, 0.0135, 0.0331)
MieScaBeta (0.00058, 0.0027, 0.1)
MieExtBeta (0.00058, 0.00027, 0.005)
```

The files beside `atmospheres.cfg`, such as `Earth.atm`, `Jupiter.atm`, `Neptune.atm`, and `Sun.atm`, are binary precomputed atmosphere model assets.

### Render Settings

Config files: `config\main-user.cfg`, `config\main-def.cfg`

Relevant exposed settings:

```text
DepthBufferMode 1
; 0 - standard, 1 - reversed, 2 - log in vertex shader, 3 - log in pixel shader

AtmoHalfPrecision false
AtmoExtinction true
AtmoRingsShadow true
AtmoAtSeaLevel true
AtmoHeightHarb true
AtmoAnalyticTransm true
AtmoFixHorizon true
AtmoBottomOffset 1e-05
AtmoDistanceLimit 30
AtmoMinWidthPix 2

VolumetricRings true
EnableEclipses true

RingsSubdivisions 32
RingRockMaxSpacing 10000
RingDetailNoiseScale 0.0005
```

The installed renderer was detected as:

```text
NVIDIA GeForce MX350/PCIe/SSE2
```

This matters because the target hardware class is close to HECTON-8 LOW/MED constraints.

### Gas Giant Catalog Parameters

Archive: `C:\GOG Games\SpaceEngine\data\catalogs\Catalogs.pak`

Text file: `planets\SpaceEngine.sc`

Representative exposed procedural gas giant controls:

```text
SurfStyle 0.16031
Randomize (0.677, -0.965, -0.808)
stripeZones 2.9156
stripeFluct 0.44312
stripeTwist 8.3928
cycloneMagn 10.137
cycloneFreq 0.56695
cycloneDensity 0.36636
cycloneOctaves 5

Clouds:
Height 31.992
Velocity 1138.8
BumpHeight 20.284
RingsWinter 0.83728
mainFreq 0.84143
mainOctaves 12
Coverage 0.40727
```

Interpretation: procedural gas giants are driven by latitudinal stripe zoning, stripe turbulence, twist, cyclone density/frequency/octaves, cloud coverage, and bump height. This is enough to build a clean deterministic Burst texture generator without copying shader source.

### Ring Catalog Parameters

Text file: `planets\SolarSys.sc`

Representative exposed ring controls:

```text
Jupiter Rings:
InnerRadius 102200
OuterRadius 227000
Thickness 10
Density 1e-4
Opacity 1e-4
SelfShadow 1e-4
PlanetShadow 1e-4

Saturn Rings:
InnerRadius 64535
OuterRadius 318120
MeanRadius 106000
EdgeRadius 137000
Thickness 0.15
RocksMaxSize 0.008
Density 1
Opacity 1
SelfShadow 1
PlanetShadow 1
Hapke 1
SpotBright 0.45
SpotWidth 0.015
SpotBrightCB 1.95
SpotWidthCB 0.00245

Neptune Rings:
InnerRadius 40501
OuterRadius 62996
Thickness 0.1
Density 0.15
Opacity 0.15
SelfShadow 0.15
PlanetShadow 0.05
```

Interpretation: atmosphere and ocean ring shadows should be parameterized by inner radius, outer radius, opacity, density, and thickness, with optional Hapke/phase terms only for visible ring rendering.

## Atmosphere Translation

### Model

SpaceEngine exposes a classic two-component atmosphere:

- Rayleigh scattering for molecular scattering;
- Mie scattering and extinction for aerosol/cloud haze;
- exponential density falloff per component;
- precomputed LUTs for transmittance, irradiance, and inscatter;
- optional analytic transmittance path.

Clean-room equations suitable for HECTON-8:

```text
h = max(0, length(p) - planetRadius)

rhoR(h) = exp(-h / rayleighHeight)
rhoM(h) = exp(-h / mieHeight)

phaseR(mu) = 3 / (16 * PI) * (1 + mu * mu)

phaseM(mu, g) =
    (1 - g * g) /
    (4 * PI * pow(max(1 + g * g - 2 * g * mu, 1e-3), 1.5))

T(optR, optM) =
    exp(-(betaRayleigh * optR + betaMieExt * optM))
```

Where:

```text
mu = dot(viewDir, lightDir)
optR = integral(rhoR) along path
optM = integral(rhoM) along path
```

### URP HLSL Reference

This is clean-room reference code. It is not SpaceEngine source.

Reference file: `ReferenceKernels/HectonAtmosphereReference.hlsl`

### HECTON-8 Integration Rule

Use this as a reference implementation only.

For LOW/MX350:

- max 8 view samples;
- max 4 sun samples;
- no per-pixel multi-scatter LUT update;
- compute atmosphere once per sky/planet pass, not per transparent object;
- keep geometry and ray math in `float`;
- cast final color to `half3`;
- disable expensive atmosphere on non-critical cameras.

For MED/HIGH:

- precompute transmittance/irradiance/inscatter into textures;
- keep runtime path as LUT lookup plus short correction term;
- update LUT only when atmosphere parameters change.

## Gas Giant Bands Translation

### Recovered Parameter Model

Available evidence proves the following controls:

- `stripeZones`
- `stripeFluct`
- `stripeTwist`
- `cycloneMagn`
- `cycloneFreq`
- `cycloneDensity`
- `cycloneOctaves`
- `mainFreq`
- `mainOctaves`
- `Coverage`
- cloud height, velocity, bump height
- texture array uniforms and climate/cloud/cyclone parameter uniforms

This points to latitudinal stripes plus turbulent domain warping and cyclone masks.

### Burst Reference

This is clean-room reference code. It is not SpaceEngine source.

Reference file: `ReferenceKernels/HectonGasBandBakeJob.reference.md`

### HECTON-8 Use

Accepted uses:

- offline or loading-screen bake to `NativeArray<Color32>`;
- deterministic toxic brine pool masks;
- skybox or gas-body texture generation;
- weather-band normal/height maps generated at fixed resolution.

Rejected uses:

- per-frame CPU rebake;
- managed arrays in hot paths;
- runtime LINQ or allocations;
- unbounded octaves;
- using this for authoritative gameplay physics.

## AUP, Reversed-Z, and Log Depth

### SpaceEngine Evidence

Exposed configuration:

```text
DepthBufferMode 1
; 0 - standard, 1 - reversed, 2 - log in vertex shader, 3 - log in pixel shader
```

Shader cache symbols:

```text
LogZParams
InverseModelViewHi
InverseModelViewLo
InverseProjection
```

Archive manifest:

```text
emu_double.glh
```

Interpretation: SpaceEngine supports reversed-Z as default, has log depth alternatives, and carries high/low matrix or double-emulation paths for large-scale transforms.

### HECTON-8 Rule

Use the stricter HECTON-8 AUP mandate:

- sector math and origin rebasing happen on CPU;
- shaders receive camera-relative small coordinates;
- do not accumulate `_GlobalFloatingOffset` in shader hot paths;
- reversed-Z remains the preferred depth mode;
- log depth is a fallback for extreme visual-only objects.

This rejects the old pattern of applying large global offsets in vertex shader. It creates precision loss exactly where HECTON-8 cannot afford it.

### CPU Camera-Relative Conversion

Reference only:

Reference file: `ReferenceKernels/HectonAupCameraRelative.reference.md`

### HLSL Log Depth Fallback

Use only for non-physics, non-authoritative, distant visual geometry when reversed-Z is insufficient.

Reference file: `ReferenceKernels/HectonAupDepthReference.hlsl`

Do not use this for submarine/ocean interaction, collision evidence, or deterministic simulation.

## Ring Shadow Translation

### Model

Available evidence:

- atmosphere setting `AtmoRingsShadow true`;
- ring shader entries `rings.glsl`, `rings_common.glh`, `rings_raymarch.glsl`;
- eclipse module `eclipse_common.glh`;
- cache uniforms `RingsParams`, `RingsMap`, `DensityParams`, `HapkeParams`, `PlanetParams`;
- catalog ring opacity, density, self-shadow, planet-shadow, and thickness values.

Clean model for HECTON-8:

1. Cast a ray from shaded point toward light.
2. Intersect with the ring plane.
3. Convert hit point to radial ring coordinate.
4. Sample analytic band or texture mask.
5. Convert density and opacity into transmittance.

### URP HLSL Reference

Reference file: `ReferenceKernels/HectonRingShadowReference.hlsl`

## Accretion Disk, Corona, and GR Salvage

### Evidence

Encrypted source entries:

```text
einstein.glsl
gr_geodesic.glh
gr_intersection.glh
corona_plane_proc.glsl
```

Cache symbols:

```text
DiskParams1..5
MetricParams
WarpMap
JetParams1..3
StarParams
InverseModelViewHi
InverseModelViewLo
```

Catalog black hole parameters:

```text
DiskNoiseContrast
DiskTempContrast
TwistMagn
AccretionRate
```

### HECTON-8 Decision

Keep:

- twisted disk UV math for stylized brine swirls, gas giant storms, or distant set dressing;
- radial temperature/brightness gradients for visual-only disk rendering;
- warp-map concept for cinematic scenes only.

Reject:

- GR geodesic simulation in production gameplay;
- per-pixel black hole raymarch in LOW/MX350;
- any use in deterministic gameplay systems.

Reference disk warp file: `ReferenceKernels/HectonAccretionDiskReference.hlsl`

## Integration Notes for HECTON-8

### Atmosphere

Implement as a separate `HectonAtmosphereReference` package or doc-backed prototype first. Do not merge directly into production rendering.

Minimum implementation sequence:

1. Create ScriptableObject atmosphere profiles using exposed parameter names: Rayleigh height, Mie height, Mie G, Rayleigh beta, Mie scatter beta, Mie extinction beta.
2. Build a URP sky/planet material using the LOW single-scatter function.
3. Add optional ring shadow transmittance.
4. Profile on MX350 at 1080p before adding LUTs.
5. Promote LUT path only after measured frame cost is acceptable.

### Gas Giants / Brine Pools

Do not run texture generation per frame. Bake into a persistent texture at load time or in editor tooling. Use `NativeArray<Color32>` and `IJobParallelFor`. Use fixed octave caps.

LOW profile:

```text
texture size <= 512 x 256
mainOctaves <= 4
cycloneOctaves <= 3
no animated CPU rebake
```

MED profile:

```text
texture size <= 1024 x 512
mainOctaves <= 6
cycloneOctaves <= 5
slow time animation in shader only
```

### AUP / Depth

Adopt reversed-Z first. Use camera-relative render poses. Keep AUP authoritative coordinates out of shader math.

Log depth is visual fallback only. It should not be used for physics, save data, vehicle control, buoyancy, or deterministic interaction.

## Regression Model

### Risks

- Atmosphere loops can exceed LOW GPU budget quickly.
- Mie phase with high `g` can create bright forward-scattering spikes.
- Ring shadow can alias if ring radii are huge and edge softness is too small.
- CPU gas texture bake can allocate or stall if implemented with managed buffers.
- Shader-side offsets can reintroduce precision loss if old floating-origin patterns are reused.
- Precomputed atmosphere LUTs can create memory pressure on MX350-class hardware.

### Required Tests Before Runtime Adoption

- Unity shader compile for URP target platforms.
- RenderDoc or Unity Frame Debugger pass count verification.
- MX350 frame timing at 1080p.
- Camera altitude sweep: underwater, sea level, low orbit, high orbit.
- Ring shadow sweep across atmosphere/ocean surface.
- AUP sector boundary visual stability test.
- GC allocation test for gas-band baking path.

### Failure Modes

- Horizon banding: increase view samples or add dither before tonemap.
- Black atmosphere at grazing view: clamp ray-sphere intersection and apply bottom offset.
- Overbright haze: reduce `MieScaBeta` or clamp HG denominator.
- Z fighting at planetary scale: verify CPU camera-relative matrix path first, then reversed-Z near/far setup.
- Gas bands swim or flicker: bake texture, animate only UV phase in shader.
- Ring shadow pops: use soft radial edges and stable light-space inputs.

## Hot Path Impact

### Atmosphere LOW Estimate

The reference LOW function is approximately:

```text
8 view samples * 4 sun samples = 32 density evaluations per pixel
```

This is expensive as a full-screen pass on MX350. It is acceptable only if:

- rendered at reduced resolution;
- limited to sky/planet pixels;
- cached through LUTs on MED/HIGH;
- disabled or simplified for secondary cameras.

### Gas Band Bake

Burst bake cost is acceptable only outside the frame-critical path. A 512 x 256 texture with capped octaves is a loading/editor job, not a gameplay update.

### Ring Shadow

The analytic ring shadow is cheap:

```text
one plane intersection
one radial length
two smoothsteps
one exp
optional one texture fetch
```

This is acceptable for atmosphere/ocean lighting if used once per shaded pixel and guarded by a feature keyword.

## Why Kept / Rejected

Kept:

- Rayleigh/Mie parameter model: directly supported by exposed atmosphere config and cache symbols.
- Bruneton-style LUT architecture: supported by shader names and `transmittanceSampler`, `irradianceSampler`, `inscatterSampler`.
- reversed-Z-first depth model: supported by config and aligned with HECTON-8 precision mandates.
- ring plane shadow math: supported by ring/eclipses shader names, uniforms, and catalog parameters.
- gas band procedural controls: supported by catalog parameters and gas shader uniform symbols.

Rejected:

- exact SpaceEngine GLSL extraction: source entries are encrypted.
- shader-side global floating offset accumulation: violates HECTON-8 AUP precision mandate.
- per-frame CPU procedural texture generation: violates hot path and zero-GC mandates.
- GR black hole shader as gameplay feature: too expensive and not relevant to core HECTON-8 simulation.
- importing proprietary shader code: not required and not available.

## Final Status

MINING COMPLETE.

Accessible SpaceEngine 0.9.9 atmosphere, scale, gas giant, ring, eclipse, and GR-adjacent evidence has been mined from archive manifests, unencrypted configs/catalogs, runtime logs, and shader cache symbols.

Runtime integration remains PENDING VERIFICATION.
