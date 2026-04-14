# HECTON-8 PostFX Visual Stack Audit

Status: `PENDING VERIFICATION`
Date: `2026-04-12`
Scene focus: `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
Quality focus: `Surface (Medium)` on MX350 baseline

## What Was Wrong

Current visual stack is not failing because "there are no post effects".
It is failing because the project is in the worst possible middle state:

- the active volume stack is too thin to carry realism
- the renderer already contains expensive features that can silently eat GPU/RT budget
- the current profiles do not separate `surface` and `underwater` look with enough discipline
- the scene is already RT-heavy, so adding more fullscreen work blindly is regression bait

The result is predictable:

- above water reads like raw 3D render
- underwater hides problems better because fog and water naturally compress detail
- the project risks paying for features that are not producing visible value

## Evidence

### Active Volume Stack

`Assets/_Project/Data/SampleSceneProfile.asset`

- `Tonemapping`: active, `ACES`
- `Bloom`: active, but `intensity = 0`
- `Vignette`: active, `intensity = 0.2`
- `ColorAdjustments`: active, but only `postExposure = 0`
- `MotionBlur`: present, disabled

`Assets/_Project/Scenes/02_HECTON_WORLD/Main Camera Profile.asset`

- `Vignette`: active, `intensity = 0.2`
- `ChromaticAberration`: active, but `intensity = 0`

Practical conclusion:

- the stack is visually under-authored
- there is almost no real above-water shaping beyond tonemap + vignette
- bloom and chromatic are effectively not doing useful work right now

### URP Tier Assets

`Assets/_Project/Data/URP_Medium (PC_RPAsset).asset`

- `HDR = on`
- `MSAA = 2`
- `RenderScale = 1.0`
- `OpaqueTexture = on`
- `DepthTexture = on`
- `AdditionalLightsPerObjectLimit = 2`
- `ShadowDistance = 200`
- `ShadowCascadeCount = 2`
- `ColorGradingMode = HDR`

`Assets/_Project/Data/URP_Low (PC_RPAsset).asset`

- same general feature class as Medium
- `RenderScale = 0.85`
- `UpscalingFilter = FSR`
- `ShadowDistance = 50`

`Assets/_Project/Data/URP_High (PC_RPAsset).asset`

- currently almost identical to Medium
- no meaningful "visual headroom tier" exists yet

Practical conclusion:

- tier structure exists on paper
- real visual differentiation is weak
- High is not yet a genuine upscale tier

### Renderer Features Already Active

`Assets/_Project/Data/PC_Renderer.asset`

- `Volumetric Fog 2`: active
- `ShapesRenderFeature`: active
- `ScreenSpaceShadows`: active
- `DecalRendererFeature`: active
- `ScreenSpaceAmbientOcclusion`: active

SSAO settings currently in renderer:

- `Downsample = 1`
- `AfterOpaque = 1`
- `Source = DepthNormals`
- `Intensity = 0.4`
- `Radius = 0.3`
- `Samples = 1`
- `BlurQuality = 0`

Practical conclusion:

- the project is already paying for more than the visible image suggests
- there is a real chance some cost is hidden in renderer features rather than volume profiles
- blindly adding more PP before profiling renderer feature cost is negligent

### Current Pipeline / Quality Readback

Live readback reported:

- pipeline: `Universal (URP)`
- quality: `Surface (Medium)`
- render scale: `1.0`
- HDR: `on`
- MSAA: `x2`
- shadow distance: `200`
- additional lights: `2`

### Atmosphere / Underwater Baseline

`Assets/_Project/Data/Atmosphere/Profile_Underwater.asset`

- `fogColor = {0, 0.2085, 0.4905}`
- `fogDensity = 0.002`
- `skyExposure = 1.2`
- `ambientColor = {0.45, 0.45, 0.55}`
- `sunIntensity = 1.69`

Practical conclusion:

- underwater already has stronger medium-shaping data than surface
- this is one reason underwater reads better than above water

### RT / GPU Pressure

Live graphics stats snapshot reported:

- `render_textures = 951`
- `render_textures_bytes = 1417054205`

Interpretation:

- this snapshot is large enough to treat every new fullscreen feature as suspect
- with AGENTS budget rules, this is already in a dangerous neighborhood
- visual uplift must come mostly from re-authoring existing systems, not from stacking more buffers

## Findings

### 1. Above-water realism is missing grading discipline, not "more effects"

Current above-water image needs:

- sky/water/terrain color coherence
- stronger haze shaping
- better highlight rolloff
- less raw material contrast

It does **not** primarily need:

- motion blur
- strong chromatic aberration
- gameplay DoF
- expensive volumetric post fog as default

### 2. The cheapest wins are in tonality, haze, and state-specific profiles

Best cost-to-value candidates:

- keep `ACES`
- author real `ColorAdjustments`
- add controlled `WhiteBalance`
- add `ShadowsMidtonesHighlights` or LUT-driven grade
- keep bloom very low and selective
- create explicit `Surface`, `ShallowUnderwater`, `DeepUnderwater`, `Storm`, `Cave` looks

### 3. Current renderer likely carries hidden cost that should be justified or reduced

Before adding any new fullscreen pass:

- verify whether `SSAO` is materially improving scene readability
- verify whether `Volumetric Fog 2` is actually needed on Medium
- verify whether both are active in gameplay cameras in shipping path

If the answer is "barely visible", they are first candidates for downscoping.

### 4. High tier is under-defined

Current `URP_High` is too close to `URP_Medium`.

Needed:

- `High` should be where optional headroom lives
- `Low/Medium` should remain disciplined and close to shipping baseline

### 5. Underwater should carry realism through medium behavior, not effect spam

Underwater realism should come from:

- depth-based color loss
- contrast compression with depth
- stronger particulate fog feel via existing fog path
- highlight attenuation and selective bloom on emissives
- smooth surface transition

Not from:

- strong CA
- gameplay DoF
- expensive blur stacks

## Hard Rules For Implementation

- Default tier must remain safe for `MX350 2GB`
- Any new fullscreen effect must justify its GPU and RT cost
- `MotionBlur` stays off for gameplay
- `DepthOfField` is non-default and non-gameplay
- `ChromaticAberration` stays near-zero unless there is a state-based reason
- `Bloom` stays restrained: low intensity, high threshold
- Surface and underwater looks must be authored as separate state profiles
- Renderer-feature cost must be audited before adding anything else

## Regression Model

CPU:

- volume blending itself is cheap
- expensive risk comes from renderer features, scene fog systems, and camera stacking side effects

GPU:

- primary risk is new fullscreen passes, higher sampling counts, and extra RT pressure

Memory / VRAM:

- current RT footprint is already high
- avoid features that introduce additional persistent RTs or upscale intermediate textures

Cadence:

- safest path is staged authoring and measurement
- do not change renderer, URP assets, and multiple profiles in one blind batch

Correctness:

- state separation must remain deterministic above/below water
- do not let menu/cutscene looks leak into gameplay volumes

## Conclusion

Correct strategy is not "add more post".
Correct strategy is:

1. re-author the existing look stack
2. separate visual states cleanly
3. audit active renderer features for hidden cost
4. reserve premium extras for `High`

Anything else is expensive noise.

## Implementation Snapshot

Implemented on `2026-04-12`:

- global grade pass in `Assets/_Project/Data/SampleSceneProfile.asset`
- `High` post profile split in `Assets/_Project/Data/SampleSceneProfile_High.asset`
- low/medium/high renderer split via `Mobile_Renderer.asset`, `PC_Renderer.asset`, `PC_High_Renderer.asset`
- atmosphere coherence pass in:
  - `Assets/_Project/Data/Atmosphere/Profile_Day.asset`
  - `Assets/_Project/Data/Atmosphere/Profile_Night.asset`
  - `Assets/_Project/Data/Atmosphere/Profile_Eclipse.asset`
  - `Assets/_Project/Data/Biomes/AtmosphereProfiles/*`
- above-water sky coherence pass in:
  - `Assets/_Project/Art/Materials/Mat_HectonSky.mat`
  - `Assets/_Project/Art/Materials/Mat_HectonSky_CloudOverlay.mat`
  - `Assets/_Project/Art/Materials/Mat_Skybox_Final.mat`
  - `Assets/_Project/Art/Skyboxes/Mat_Skybox_Day.mat`
  - `Assets/_Project/Art/Skyboxes/Mat_Skybox_Storm.mat`
- above-water ocean surface coherence pass in `Assets/_Project/_Archive/Mat_Ocean.mat`
- above-water light temperature pass in `Assets/_Project/Prefabs/Directional Light.prefab`

Latest above-water findings:

- the image is now materially more coherent by asset logic alone: sky, haze, horizon, ocean surface, and directional light are no longer fighting each other
- the biggest remaining gap is not "more post", but missing state routing for `storm / cave-interior / low-visibility surface`
- `Mat_Skybox_Storm.mat` exists as a valid production asset, but live ownership is still not proven from current disconnected Unity session
- active above-water ocean still resolves to `Assets/_Project/_Archive/Mat_Ocean.mat`, which is a bad ownership path even if the material is currently the real source of truth
- live gameplay camera ownership remains untrusted, so `allowHDR / allowMSAA` are still `PENDING VERIFICATION`

- `SampleSceneProfile.asset`: cheap core grading pass added (`Bloom`, `ColorAdjustments`, `WhiteBalance`, `ShadowsMidtonesHighlights`, weaker global `Vignette`)
- `Main Camera Profile.asset`: local vignette reduced to avoid stacked "gamey" darkening
- `URP_Low (PC_RPAsset).asset`: rerouted from `PC_Renderer.asset` to `Mobile_Renderer.asset` so Low tier no longer pays for `SSAO / Volumetric Fog 2 / Decals / ScreenSpaceShadows`
- `PC_Renderer.asset`: `Volumetric Fog 2` and `SSAO` disabled for baseline `Medium`
- `PC_Renderer.asset`: `ScreenSpaceShadows` disabled for baseline `Medium`
- `PC_High_Renderer.asset`: cloned premium renderer path preserving `Volumetric Fog 2` and `SSAO`
- `PC_High_Renderer.asset`: preserves `ScreenSpaceShadows` for premium headroom
- `URP_High (PC_RPAsset).asset`: rerouted to `PC_High_Renderer.asset`
- `SampleSceneProfile_High.asset`: dedicated `High` post profile with slightly richer bloom, softer vignette, lighter saturation loss, and stronger highlight rolloff
- `URP_High (PC_RPAsset).asset`: rerouted to a dedicated `High` volume profile instead of reusing `SampleSceneProfile.asset`
- `Profile_Day.asset`: surface atmosphere warmed slightly, haze density increased, exposure and sun intensity reduced
- `Profile_Underwater.asset`: underwater fog density increased, exposure reduced, ambient/sun energy reduced for denser medium response
- `Profile_Night.asset` and `Profile_Eclipse.asset`: darker ambient base, lower exposure, denser haze for better continuity with the revised day/surface stack
- biome atmosphere profiles in `Assets/_Project/Data/Biomes/AtmosphereProfiles/`: coherence pass applied so shallow families stay brighter and cleaner while rift/hadal/volcanic families read denser and less synthetic without new fullscreen cost
- sky material authoring pass applied in `Mat_HectonSky.mat` and `Mat_Skybox_Final.mat`: less synthetic purple horizon bias, calmer sunset HDR, tighter haze/cloud palette, slightly more grounded day/night tinting
- active ocean material pass applied in `Assets/_Project/_Archive/Mat_Ocean.mat`: shallows desaturated from tropical cyan, subsurface scattering reduced, foam toned down, absorption raised so surface water reads heavier and less gamey above water
- above-water light/overlay coherence pass applied in `Directional Light.prefab` and `Mat_HectonSky_CloudOverlay.mat`: sun temperature warmed slightly, cloud overlay de-neonized, haze and sunset tinting pulled back toward a grounded steel-blue palette

What this means:

- Low tier now has a genuinely cheaper renderer path
- Medium and High are no longer fake duplicates; expensive renderer features are isolated to High
- baseline `Medium` now relies on the standard shadow path instead of paying for screen-space shadow resolve
- `High` now differs from `Medium` in both renderer path and post profile, not only in renderer features
- surface look should rely more on authored atmosphere instead of raw flat exposure
- sky and haze now carry more of the premium look, which is cheaper than stacking additional post effects
- above-water water now leans more steel-blue and heavy, instead of bright aquarium cyan
- sun, clouds, haze, and water now sit closer in the same palette family instead of fighting each other
- underwater should read denser and less synthetic without adding new fullscreen cost
- non-day states should stop reading like untouched defaults beside the revised day profile

What is still unknown:

- actual GPU delta on MX350
- whether Medium should keep `SSAO` and `Volumetric Fog 2`
- whether waterline transition remains visually clean after these profile changes
- dedicated `Surface_Storm` / `Cave_Interior` routing is still not wired by the current `HectonAtmosphereManager` owner; it currently supports `SURFACE_DAY / SURFACE_NIGHT / UNDERWATER / ECLIPSE + biome overrides`
