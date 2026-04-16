# HECTON-8 PostFX Execution Plan

Status: `PENDING VERIFICATION`
Date: `2026-04-12`

## Goal

Make `02_HECTON_WORLD` read as a premium, grounded, believable image:

- above water: cinematic but not fake
- underwater: richer, denser, more physical
- low/medium tier: safe for MX350 baseline
- high tier: real upscale headroom, not pointless duplication

## Phase 0 - Freeze Bad Decisions

Do not do these as default gameplay features:

- `MotionBlur`
- gameplay `DepthOfField`
- strong `ChromaticAberration`
- heavy always-on volumetric post fog
- new RT-hungry renderer features without profiling

Exit condition:

- visual work is constrained to cheap grading, fog, and state-profile authoring first

## Phase 1 - Author The Cheap Core Look

Primary target:

- `Assets/_Project/Data/SampleSceneProfile.asset`
- `Assets/_Project/Scenes/02_HECTON_WORLD/Main Camera Profile.asset`

Actions:

1. keep `ACES`
2. give `ColorAdjustments` real values
3. add `WhiteBalance`
4. add either `ShadowsMidtonesHighlights` or LUT-based global grade
5. set `Bloom` to low but meaningful gameplay-safe values
6. reduce duplicate vignette stacking if it reads artificial
7. keep `ChromaticAberration` effectively off except for exceptional states

Target look:

- softer digital harshness
- warmer solar highlights
- better sky-to-horizon cohesion
- less "editor default" surface render

Verification:

- compare above-water screenshots before/after
- confirm no obvious GPU spike

## Phase 2 - Split Surface And Underwater Properly

Current problem:

- underwater already has environmental shaping
- surface does not have enough authored look separation

Actions:

1. define explicit visual states:
   - `Surface_Default`
   - `Surface_Storm`
   - `Underwater_Shallow`
   - `Underwater_Deep`
   - `Cave_Interior`
2. route state transitions through existing atmosphere / underwater owners
3. keep underwater realism driven by:
   - fog color
   - fog density
   - exposure
   - saturation loss
   - bloom only on emissive highlights

Verification:

- smooth transition at waterline
- no menu/camera profile leakage into gameplay

## Phase 3 - Audit Renderer Feature Value

Targets:

- `Assets/_Project/Data/PC_Renderer.asset`

Audit order:

1. `ScreenSpaceAmbientOcclusion`
2. `Volumetric Fog 2`
3. `ScreenSpaceShadows`
4. `DecalRendererFeature`

Questions:

- is the feature visibly helping shipping gameplay?
- is the same visual result achievable cheaper through authored lighting/material/fog?
- does disabling it reclaim frame time or RT pressure materially?

Likely rule:

- if a feature is not clearly visible in side-by-side captures, it should not stay active on Medium baseline

## Phase 4 - Build Real Tier Separation

### Low / MX350

Keep:

- ACES
- minimal bloom
- restrained vignette
- authored color grading
- underwater depth shaping

Avoid:

- costly AO
- volumetric extras
- non-essential fullscreen passes

### Medium

Keep:

- same visual language as Low
- slightly richer grading
- carefully justified renderer features only

### High

Add headroom only here:

- richer bloom quality
- stronger atmospheric depth if measured safe
- selective premium fog or AO if measurable benefit exists

Rule:

- High must be a controlled upscale tier
- Medium must remain shipping-safe

## Phase 5 - Verification Protocol

For each visual phase capture:

1. scene screenshot above water
2. scene screenshot underwater
3. scene screenshot at waterline transition
4. renderer stats / RT pressure
5. console sanity

Mandatory checks:

- no black-screen regressions from camera/volume confusion
- no menu-scene contamination
- no strong new RT growth without justification
- no visual mismatch between sky, water, terrain, fog

## Immediate Implementation Order

1. audit and tune `SampleSceneProfile.asset`
2. remove redundant/no-value effects from `Main Camera Profile.asset`
3. add missing grading controls for surface realism
4. move `URP_Low` off `PC_Renderer.asset` so MX350 baseline does not pay for `SSAO / Volumetric Fog 2 / Decals / ScreenSpaceShadows`
5. tighten `Atmosphere/Profile_Day.asset` and `Atmosphere/Profile_Underwater.asset` so look is carried by authored atmosphere, not fullscreen cost
6. define underwater profile states more explicitly
7. measure whether `SSAO` and `Volumetric Fog 2` deserve to stay on Medium
8. only then touch High-tier extras

## Progress Snapshot

Implemented:

- Phase 1 global grade pass in `SampleSceneProfile.asset`
- camera-local cleanup in `Main Camera Profile.asset`
- low-tier renderer split by routing `URP_Low` to `Mobile_Renderer.asset`
- medium/high renderer split by moving `SSAO` and `Volumetric Fog 2` out of baseline `PC_Renderer` and into `PC_High_Renderer`
- baseline `Medium` shadow path tightened further by removing `ScreenSpaceShadows` from `PC_Renderer`
- dedicated `SampleSceneProfile_High.asset` created and wired into `URP_High`
- first-pass atmosphere tuning in `Profile_Day.asset` and `Profile_Underwater.asset`
- coherence pass for `Profile_Night.asset` and `Profile_Eclipse.asset`
- biome atmosphere coherence pass applied across `Assets/_Project/Data/Biomes/AtmosphereProfiles`
- sky material coherence pass applied in `Assets/_Project/Art/Materials/Mat_HectonSky.mat` and `Mat_Skybox_Final.mat`
- active ocean material coherence pass applied in `Assets/_Project/_Archive/Mat_Ocean.mat`
- above-water light and cloud-overlay coherence pass applied in `Assets/_Project/Prefabs/Directional Light.prefab` and `Mat_HectonSky_CloudOverlay.mat`

Not yet verified:

- runtime visual continuity at waterline
- GPU / RT delta on live hardware
- whether Medium should keep `SSAO` and `Volumetric Fog 2`
- whether dedicated storm / cave state routing should be added to `HectonAtmosphereManager` or owned somewhere else
- whether `Mat_Skybox_Storm.mat` is wired into live surface-state switching or remains a prepared asset only
- whether current editor camera ownership is stable enough for trustworthy screenshot/stat capture

## Deliverables

- revised global gameplay profile
- revised camera-local profile
- explicit state profiles for surface / underwater / cave
- tier matrix for `Low / Medium / High`
- performance notes per feature kept or rejected

## Next Step

Next implementation pass should prioritize:

- runtime verification of above-water state assets once Unity session is stable again
- owner cleanup for the active above-water ocean material so it no longer lives under `_Archive`
- architecture-safe decision on whether `storm / cave-interior / low-visibility surface` belong in `HectonAtmosphereManager` or another existing owner

Do not touch scene topology, underwater/Crest assets, or camera stack while live editor ownership is unstable.
