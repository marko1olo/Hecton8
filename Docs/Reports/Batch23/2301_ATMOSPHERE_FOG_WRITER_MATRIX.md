# 2301 Atmosphere Fog Writer Matrix

Status: STATIC AUDIT / PENDING UNITY OWNER PROOF  
Worker: 2301 atmosphere fog writer  
Boundary: no Unity, no Play Mode, no builds, no imports. Evidence is source, YAML, listed reports, orchestration log, and listed screenshots.

## Executive Finding

The likely live fault is an ownership/state mismatch, not absence of fog code.

`HectonAtmosphereManager` is serialized in `02_HECTON_WORLD` with `_useAutoUnderwaterDetection: 0` and `_waterSurfaceY: 14.02`. Its underwater evaluator returns false when auto detection is disabled unless an external caller sets `_underwaterExternalFlag`. `HectonUnderwaterVisuals` independently resolves camera/player depth and can enter visual underwater at `0.18m`. Therefore the project can produce a capture labelled underwater while the atmosphere profile remains surface/biome and while surface/celestial fog writers still believe they own the frame.

The forced-day surface has a green swamp floor by screenshot evidence. The 0-5 m underwater screenshot is not valid underwater route proof: it shows a surface composition with a large opaque yellow/green slab, no believable photic particulate/haze, and no route-readable underwater volume.

## Mandate Basis

- Surface, sky, coastline, ocean surface, and 0-100 m photic water must remain bright, beautiful, readable, and above Subnautica-level floor.
- Fog and haze are controlled visual fakes. They must preserve route readability and cannot hide weak terrain, weak water, or missing art.
- Generic blue fog, green swamp cast, transparent empty underwater, pure darkness, and false underwater labels are reject states.
- Runtime claims require Unity/capture/profiler proof. Serialized profile values are only source candidates.
- `GlobalQualityWeight` must scale fog/haze continuously from Minimum to Ultra without changing gameplay truth or ownership.

## Source Writer Inventory

| Writer | File/line | Condition | Value route | Owner phase | Risk | Proof needed |
|---|---|---|---|---|---|---|
| Scene `RenderSettings` | `Assets/_Project/Scenes/02_HECTON_WORLD.unity:17-24` | Scene load/default before runtime writers | `fog=1`, `fogColor={0.035,0.3,0.34}`, `fogDensity=0.01`, green/teal ambient | Unity scene initialization | Green/teal swamp floor if runtime writers fail, are late, or capture is before owner writes | First 10 frame ordered log of RenderSettings after each writer |
| `HectonAtmosphereManager` serialized state | `02_HECTON_WORLD.unity:90985-90992` | Manager setup | Day/night/underwater/eclipse profiles assigned; `_useAutoUnderwaterDetection: 0` | Runtime config | Atmosphere can stay surface while camera is underwater | Log atmosphere state, profile, auto flag, external flag, player/camera depth |
| `HectonAtmosphereManager.EvaluateUnderwater()` | `HectonAtmosphereManager.cs:1737-1773` | Every state evaluation | external flag wins; auto disabled returns false; otherwise movement/depth | Atmosphere tick | Static proof of false underwater risk | Log evaluator inputs/output for surface, 0.5m, 20-50m |
| `HectonAtmosphereManager.SetUnderwater()` | `HectonAtmosphereManager.cs:2376-2390` | External API caller | Sets `_underwaterExternalFlag`; can update water surface | Control API | Unknown force path; if not called, auto disabled blocks underwater atmosphere | Static caller grep plus runtime call log |
| `HectonAtmosphereManager` biome fog | `HectonAtmosphereManager.cs:2136-2195` | Playing + biome influence snapshot | Primary/secondary biome `AtmosphereProfile` fog blends into `_currentValues` | Atmosphere tick | Green profiles can leak into surface route | Log active biome profile(s), blend, final current fog |
| `HectonAtmosphereManager` abyss globals | `HectonAtmosphereManager.cs:1897-1964` | Abyss presentation dirty | `_H8AbyssAbsorptionColor`, `_H8AbyssAtmosphereParams`, `_CausticOffset` | Shader payload | Does not directly write `RenderSettings`; logging only globals is insufficient | Log globals plus final `RenderSettings` same frame |
| `HectonCelestialEngine` surface fog | `HectonCelestialEngine.cs:3848-4128` | Valid surface atmospheric state | Computes readable surface fog, clamps density <= `0.001`, writes fog/ambient/global color | Late-frame surface presentation | Can fight underwater writer if state/order is wrong; not correct primary fog truth owner | Ordered before/after writer log |
| `HectonCelestialEngine` weather override | `HectonCelestialEngine.cs:7212-7264` | Weather caller sets override | Stores fog color/density/ambient/sun/weather values | Surface weather control | Hidden surface fog writer unless logged | Log override calls and source profile |
| `HectonUnderwaterVisuals` surface floor | `HectonUnderwaterVisuals.cs:2899-2929` | Visual state not underwater | Rewrites surface fog color/density and ambient floors | Visual tick/callback | Can preserve green source if surface color source is green-biased | Log branch and resolved surface color |
| `HectonUnderwaterVisuals` underwater fog | `HectonUnderwaterVisuals.cs:2932-2987` | Visual underwater | Writes `RenderSettings.fogColor`, `fogDensity`; near-surface blends to surface; density floor/haze boosts | Visual tick | At 0-0.5 m it may blend back to green surface; if profile/motes inactive, underwater can stay empty | Log depth, surfaceBlend, color before/after, density terms |
| `HectonUnderwaterVisuals.EnforceFogState()` | `HectonUnderwaterVisuals.cs:2989-3043` | Per camera | Forces fog per camera; SceneView is scaled/handled differently | Camera render callback | GameView/SceneView/proof-camera divergence | Log camera type/name and `ShouldRenderUnderwaterFogForCamera` |
| `HectonUnderwaterVisuals` ambient/camera | `HectonUnderwaterVisuals.cs:3049-3105` | Underwater visual branch | Blends underwater ambient with surface state; camera background = underwater fog color | Visual camera presentation | Background can imply underwater even if world/atmosphere route is false | Log clear flags/background/ambient |
| `HectonUnderwaterVisuals` state resolver | `HectonUnderwaterVisuals.cs:7339-7388` | Visual state update | depth exit `0.03`, enter `0.08`, forced `0.18`, movement/submerged checks | Visual owner state | Can disagree with `HectonAtmosphereManager` | Log both resolved states same frame |
| `HectonUnderwaterVisuals` profile fallback | `HectonUnderwaterVisuals.cs:7630-7746` | No/active biome profile | Fallback shallow scatter `{0,0.15,0.12}`; profile fog/depth/turbidity if active | Visual profile resolution | Green/teal defaults and high runtime profile densities can dominate | Log active biome profile and resolved values |
| `HectonUnderwaterVisuals` noir/water globals | `HectonUnderwaterVisuals.cs:6436-6508` | Noir resolve update | `_HectonNoirFogStratification`, `_FogScatteringCoeff`, water extinction payload | Shader global sync | Shader fog can diverge from `RenderSettings` | Frame Debugger + final value log |
| Crest underwater pass | `02_HECTON_WORLD.unity:66908-66932` | Camera component enabled | `_depthFogDensityFactor=0.92`, `_copyOceanMaterialParamsEachFrame=1` | Crest camera pass | Active component does not prove correct underwater state; may copy material params over time | Frame Debugger Crest pass and material param log |
| `WaterOpticsRuntime` | `WaterOpticsRuntime.cs:445`, `995-1010` | If runtime owner exists and profile applied | Global constant buffer absorption/scattering/light/maxDistance/quality | Rendering water optics | Batch22 found no serialized owner in searched assets; likely not live | Owner GUID proof plus runtime `TryGetActiveConstantBuffer` and GPU timing |
| URP renderer fog feature data | `PC_Renderer.asset:95`, `PC_High_Renderer.asset:197`, `Mobile_Renderer.asset:320` | Renderer feature active | Serialized `fogColor={0.015,0.045,0.065}` | Renderer feature data | Static asset value is not live proof | Frame Debugger active pass/material/global values |

Full CSV: `Docs/Reports/Batch23/2301_ATMOSPHERE_FOG_WRITER_MATRIX.csv`.

## Green/Teal Source Colors

- Scene default: `02_HECTON_WORLD.unity:18` has `m_FogColor {0.035,0.3,0.34}` and density `0.01`. This is a green/teal swamp floor.
- Day profile: `Profile_Day.asset:15-16` has `fogColor {0.66,0.71,0.77}`, density `0.00024`. This is a pale blue/cyan floor, not the green swamp source.
- Underwater profile: `Profile_Underwater.asset:15-16` has `fogColor {0.12,0.33,0.58}`, density `0.0021`. Blue route exists, but it may not be selected because atmosphere auto-underwater is disabled.
- Green biome candidates:
  - `Atmos_FossilReef.asset:15-16`: `{0.24,0.43,0.37}`, density `0.013`.
  - `Atmos_ChemosyntheticBrine.asset:15-16`: `{0.08,0.22,0.17}`, density `0.028`.
  - `Atmos_CrystalGrowth.asset:15-16`: `{0.17,0.39,0.53}`, density `0.01`.
- Underwater visual defaults:
  - `HectonUnderwaterVisuals.cs:158-159`: daylight sea tint shallow `{0.118,0.402,0.424}`, mid `{0.026,0.156,0.238}`.
  - `HectonUnderwaterVisuals.cs:7635-7640`: fallback base `{0,0.03,0.07}`, shallow `{0,0.15,0.12}`.
- Runtime visual profiles under `Assets/_Project/Data/Biomes/RuntimeVisualProfiles/*.asset` commonly use teal/green shallow scatter and depth fog densities around `0.2-0.7`. These can be correct for depth if active, but they are not live proof.

## Underwater Force And False-Proof Findings

Static proof:
- Scene: `_useAutoUnderwaterDetection: 0` at `02_HECTON_WORLD.unity:90992`.
- Code: `HectonAtmosphereManager.EvaluateUnderwater()` returns false when auto detection is disabled unless `_underwaterExternalFlag` is true (`HectonAtmosphereManager.cs:1747-1756`).
- Code: `HectonUnderwaterVisuals.ResolveUnderwaterVisualStateForCameraDepth()` can return true from visual depth >= `0.18m` or from movement/submerged state (`HectonUnderwaterVisuals.cs:7344-7388`).

Conclusion: yes, `_useAutoUnderwaterDetection: 0` can make screenshots falsely show surface atmosphere state while the camera is underwater or while the screenshot is labelled underwater. It does not alone disable `HectonUnderwaterVisuals`; it specifically blocks `HectonAtmosphereManager` from selecting the underwater profile via depth. This creates a split-brain route.

## Write Order Risk

Documented execution comments and attributes:
- `HectonAtmosphereManager`: `[DefaultExecutionOrder(-6000)]`, computes profiles before visuals.
- `HectonUnderwaterVisuals`: comments indicate `-4000`, writes visual underwater fog and sun/light response.
- `HectonCelestialEngine`: `[DefaultExecutionOrder(-3000)]`, runs after underwater visuals and writes surface atmospheric presentation.

Risk:
- This order is valid only if state gates agree. If `HectonUnderwaterVisuals.IsUnderwater` is true but `HectonAtmosphereManager` and `HectonCelestialEngine` still consider the frame surface, later surface fog/ambient writes can contaminate underwater captures or vice versa.
- Crest `UnderwaterRenderer` is enabled and copies ocean material params each frame. It must be logged/Frame-Debugged because static component state does not prove correct pass values.
- Post-processing/renderer features have fog defaults, but active pass and material values require Frame Debugger proof.

## Surface Forced-Day Verdict

Current forced-day surface has a green swamp floor.

Evidence:
- Required screenshot `Docs/Screenshots/MCP/h8_1473_mainrt_surface_forced_day.png` reads green-heavy ocean and coast.
- Scene default fog is green/teal and dense (`{0.035,0.3,0.34}`, `0.01`).
- Day profile is pale blue/cyan, so if the final live forced-day frame remains green, the likely source is scene default leakage, biome/visual profile influence, ocean material/shader globals, or underwater/surface writer ordering, not `Profile_Day` alone.

## Underwater Verdict

Current 0-5 m underwater proof is invalid and too empty/transparent.

Evidence:
- Required screenshot `Docs/Screenshots/MCP/h8_1473_mainrt_underwater_0_5m.png` shows surface/coastline framing plus a large opaque yellow/green slab. It does not prove real underwater route composition.
- Batch22 2201 found many underwater haze/speck/sheet helpers disabled or missing active runtime owners.
- `Profile_Underwater` density `0.0021` is probably too weak alone for 20-50 m photic route proof if no particulate/haze/marine snow route is active.
- `HectonUnderwaterVisuals` has density floors and haze boosts, but live proof must show the branch executed and the final camera used those values.

## Ownership Call

Accepted route:
- `HectonAtmosphereManager`: state/profile owner. Publishes current atmosphere, biome influence, abyss atmosphere shader payloads. It should not be the only underwater visual truth.
- `HectonUnderwaterVisuals`: camera-local underwater presentation owner. Owns underwater `RenderSettings` fog, background, ambient blend, underwater pass enforcement, motes/haze/caustics presentation.
- `HectonCelestialEngine`: celestial/sky/surface polish owner. It may compute readable surface haze from atmosphere state and write surface `RenderSettings` only when underwater visuals are not active. It should not decide underwater truth or override underwater fog.
- Crest: third-party underwater render pass. It should be configured and proven, not wrapped or overridden by runtime material clones.

Ownership risk: `HectonCelestialEngine` currently writes surface `RenderSettings.fog*`. That is acceptable only as late-frame surface polish gated by `!UnderwaterVisuals.IsUnderwater`; if it writes during visual underwater state, it is an owner violation.

## Unity-Owner Live Logging Probe Plan

No allocations, no scene search, no per-frame string formatting. Add temporary cached-reference logging/probe only for capture frames or every N frames around proof packet.

Log exact values in this order:
1. Frame index, camera name/type, capture label.
2. Camera world Y, player world Y, water surface Y, camera depth, player depth.
3. `HectonAtmosphereManager`: `_useAutoUnderwaterDetection`, external flag, auto state, current state, current profile name/GUID if available, current fog color/density.
4. `HectonUnderwaterVisuals`: resolved visual underwater, `_cachedVisualDepth`, `_wasUnderwater`, `_cachedUnderwaterFogColor`, `_cachedFogDensity`, active biome visual profile name, turbidity.
5. `HectonCelestialEngine`: `TryGetCurrentAtmosphericLightingState`, surface fog color/density before apply, whether surface directional/fog write allowed.
6. Final `RenderSettings`: `fog`, `fogMode`, `fogColor`, `fogDensity`, `ambientMode`, `ambientLight` or Trilight colors, `ambientIntensity`.
7. Shader globals after final camera render: `_FogScatteringCoeff`, `_HectonNoirFogStratification`, `_H8AbyssAtmosphereParams`, `_H8AbyssAbsorptionColor`, `_HectonWaterSurfaceEmission`, `_HectonUnderwaterSurfaceColor`.
8. Crest: underwater renderer enabled/active, `_depthFogDensityFactor`, `_copyOceanMaterialParamsEachFrame`, ocean material `_DepthFogDensity`, `_Underwater`, `_Diffuse`, `_SubSurfaceShallowCol`.
9. Active camera clear flags/background color.
10. Screenshot path and whether it is GameView/player camera, not detached temp camera.

Exact checks:
- Capture a surface forced-day frame and 0.5 m/20 m/50 m underwater frames.
- For each capture, dump one compact row before `HectonUnderwaterVisuals`, after `HectonUnderwaterVisuals`, after `HectonCelestialEngine`, and after final camera render.
- Do not use `FindObjectOfType` or scene searches in the hot probe. Cache refs during setup from existing `GlobalRegistry` or inspector references.

## Minimal Patch Plan: Surface

Goal: avoid green swamp cast while keeping bright readable surface.

1. First prove final writer order with logging. Do not tune profiles blind.
2. Ensure `HectonCelestialEngine` surface fog write is blocked whenever `HectonUnderwaterVisuals.IsUnderwater` is true.
3. Replace scene default `RenderSettings` fog floor with a pale sky/ocean blue fallback close to day profile, not green. Candidate static fallback: `{0.56,0.72,0.82}` with density no higher than `0.001` for surface proof. This is a Unity-owner scene edit, not for this worker.
4. Clamp surface biome influence so green biome atmosphere cannot dominate above-water forced-day proof unless a named weather/biome transition is active and logged.
5. Keep daylight surface luminance floors; do not lower exposure or add darkness.

Rollback:
- Restore original scene `m_FogColor {0.035,0.3,0.34}` and density `0.01`.
- Restore previous `HectonCelestialEngine` gating.
- Disable temporary probe code after proof if not promoted to a permanent diagnostic path.

## Minimal Patch Plan: Underwater

Goal: blue/teal depth haze with visible particulate and route readability in 0-100 m.

1. First fix proof route: use real player/GameView camera underwater, log atmosphere/visual state agreement, and remove/disable the yellow/green slab offender from the capture route.
2. Turn on or bind one owned particulate/haze route with proof. Prefer existing `HectonUnderwaterVisuals` suspended motes/marine snow route if components are present and bounded; otherwise one authored speck/haze mesh for proof only.
3. Raise effective near-surface underwater density/haze only through `HectonUnderwaterVisuals` terms, not scene default fog. 0-5 m should show water volume and particles without becoming opaque.
4. For 20-50 m, require visible blue/teal extinction, suspended matter, caustic/light cues where justified, and route silhouettes. `Profile_Underwater` alone at `0.0021` is not enough proof if no particulate/haze route is active.
5. Keep 0-100 m bright/readable; do not use abyss darkness or storm grade.

Rollback:
- Revert changed underwater profile values.
- Restore prior `HectonUnderwaterVisuals` density constants or serialized overrides.
- Disable newly activated haze/mote route and restore original object/component enabled states.
- Restore Crest underwater material/pass values if touched.

## Fog/Haze Tier Behavior

Use continuous `GlobalQualityWeight`; tier labels are explanation bands, not binary switches.

| Band | Fog/haze behavior |
|---|---|
| Minimum | Depth fog/LUT only, bright surface, shallow water readable, sparse bounded particles, no raymarch. Preserve blue/teal photic identity and route silhouettes. |
| Low | Slightly richer depth haze, low-count motes near camera, conservative caustic hints, still no darkness hiding terrain. |
| Middle | Biome-aware fog blend, stronger but bounded particulate, shallow caustic/shaft cues where justified, stable hysteresis. |
| High | Layered haze/particulate, better extinction color response, richer local shafts/caustics with GPU proof. |
| Ultra | Visual overkill: dense cinematic water column, richer motes and shafts, refined color extinction, but same truth ownership and route readability. |

## Reject Gates

- Green swamp: final surface forced-day fog/ocean reads green, brackish, or acid instead of bright ocean/sky blue.
- Transparent empty underwater: 0-100 m water lacks haze, particles, route silhouettes, or depth extinction.
- Darkness hiding weak terrain: fog/post makes bad shoreline/terrain/underwater geometry less visible instead of fixing it.
- False underwater label: screenshot name says underwater but logged atmosphere/visual/camera state does not prove underwater route.
- Writer split: atmosphere state, underwater visual state, Crest pass, and final `RenderSettings` disagree without documented reason.
- Static-only acceptance: serialized profile values are presented as live proof.

## Exact Unity-Owner Proof Commands/Checks

No command was run by 2301. Unity owner should perform equivalent checks inside its existing proof harness:

- Capture `surface_forced_day`, `underwater_0_5m`, `underwater_20m`, `underwater_50m` from the real GameView/player camera.
- For each capture, emit ordered writer rows: `BeforeAtmosphere`, `AfterAtmosphere`, `AfterUnderwaterVisuals`, `AfterCelestial`, `AfterCameraRender`.
- Include final `RenderSettings` and shader globals in every row.
- Include camera/player depth and both underwater booleans: atmosphere state and visual state.
- Include Crest underwater renderer enabled/active and ocean material fog params.
- Reject the packet if any underwater capture has atmosphere state `SURFACE_*` without an explicit documented transitional reason.

## Proof Boundary

This report does not claim runtime acceptance. It identifies static writer routes and the minimal proof/patch path for the Unity owner.
