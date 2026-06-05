# 2504 Crest Ocean Clip Foam Caustic Risk Audit

Status: STATIC_SOURCE_AUDIT_ONLY - PENDING UNITY/PROFILER VERIFICATION
Agent: 2504
Date: 2026-06-04

## Scope

Audit current Crest/ocean/foam/caustic material diffs that may cause dark flat water, acid/flat green water, black streaks, missing shoreline foam, sheet/streak caustics, or terrain/water clipping artifacts.

No Unity, Play Mode, builds, imports, material edits, code edits, shader edits, texture generation, or scene edits were run.

## Evidence Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `water.md`
- `rendering.md`
- `shaders.md`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `Docs/Reports/Batch23/2303_FOAM_CAUSTIC_PATCH_PLAN.md`
- `Docs/Reports/Batch24/2401_CURRENT_SCENE_DELTA_UNDERWATER_CUT_AUDIT.md`
- `Docs/Reports/Batch24/2402_UNDERWATER_MATERIAL_RECEIVER_AUDIT.md`
- `Docs/Orchestration/UNITY_OWNER_STEER_20260604_BATCH23_1474_OPERATIONAL.md`
- `Docs/Orchestration/UNITY_OWNER_STEER_20260604_BATCH24_1474_SLAB_CAUSTIC_ISOLATION.md`
- `Assets/Crest/Crest/Materials/Ocean.mat`
- `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`
- `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat`
- `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`
- `Assets/_Project/Data/Ocean/Sim_Settings_Foam.asset`
- cited foam/caustic receiver materials under `Assets/_Project/Art/Materials/World/Photic1428`, `Photic1453`, `Photic1464`, and `Photic1469`

`Docs/Actual Domains of Project.txt` was not present.

## Staleness Notes

- Batch24 report `2402_UNDERWATER_MATERIAL_RECEIVER_AUDIT.md` is already stale in one important detail: current `git diff` for `Ocean-Underwater.mat` shows color/fog edits only. It does not currently show `_CAUSTICS_ON`, `_Caustics: 0 -> 1`, or `_CausticsStrength: 0 -> 0.16`.
- `MAT_H8_FloorCausticSoft_1443.mat` currently reads `_Tint.a 0.24`, `_ScaleA 1.05`, `_ScaleB 1.72`, `_Sharpness 8.2`. Batch24 cited `_Tint.a 0.42`, `_ScaleA 0.62`, `_ScaleB 0.98`, `_Sharpness 5.8`. Treat Batch24 values as stale screenshot-era evidence, not current source truth.
- Several Photic material folders are untracked in git. They can be current working evidence, but they have no tracked baseline in `git diff`.

## Diff-Risk Table

Risk classes:

- BLOCKER: likely to create visible slab/clip/sheet failure or destroy surface/shallow water floor.
- HIGH: likely visual regression without runtime proof.
- MEDIUM: useful direction but needs isolated proof.
- LOW: probably safe by itself, still unverified.

### `Assets/Crest/Crest/Materials/Ocean.mat`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| keyword `_CLIPSURFACE_ON` | present | removed | BLOCKER | Removing the keyword while `_ClipSurface` is also set to `0` is the strongest material-side terrain/water clipping suspect. |
| keyword `_CLIPUNDERTERRAIN_ON` | present | removed | BLOCKER | Same class as above. It can expose ocean/water planes through terrain or service geometry. |
| keyword `_CAUSTICS_ON` | absent | present | HIGH | Enables caustic path without proof that receiver/depth/light gating is correct. |
| `_Caustics` | `0` | `1` | HIGH | Turns caustics on globally for this material. Useful only after clipping and receiver proof. |
| `_CausticsDepthOfField` | `0.33` | `0.62` | MEDIUM | Wider depth focus can spread caustics into sheet-like read if receiver is broad. |
| `_CausticsDistortionStrength` | `0.075` | `0.12` | MEDIUM | More distortion can help organic breakup, but paired with high scale/strength can smear or streak. |
| `_CausticsFocalDepth` | `2` | `3.4` | MEDIUM | Moves caustic focus deeper. Needs shallow/depth capture. |
| `_CausticsStrength` | `0` | `0.56` | HIGH | Nonzero caustic strength before receiver proof. |
| `_CausticsTextureAverage` | `0.07` | `0.045` | MEDIUM | Lower average can sharpen contrast. Risk is high when strength is also enabled. |
| `_CausticsTextureScale` | `5` | `9.5` | MEDIUM | Finer caustic scale can help lace, but can shimmer/grid if texture route is wrong. |
| `_ClipSurface` | `1` | `0` | BLOCKER | Direct terrain/water clip regression suspect. Test before adding any haze/foam. |
| `_ClipUnderTerrain` | `1` | `0` | BLOCKER | Direct under-terrain plane exposure suspect. |
| `_FoamScale` | `0.001528351` | `0.044` | HIGH | 28x larger. Can change foam cell scale from absent to broad artificial pattern. |
| `_ReflectionBlur` | `0` | `0.08` | LOW | Could soften surface glare. Not a primary blocker. |
| `_ShorelineFoamMinDepth` | `0.95` | `0.82` | MEDIUM | Slightly shallower threshold. Could help contact foam if Crest route is proven. |
| `_WaveFoamBubblesCoverage` | `1.68` | `0.42` | HIGH | Large reduction may help avoid overdrawn bubbles, but may also explain no visible foam if the foam route depends on bubbles. |
| `_WaveFoamFeather` | `0.4` | `0.19` | MEDIUM | Harder foam edge. Risk: crisp technical bands. |
| `_WaveFoamLightScale` | `0.55` | `1.55` | HIGH | Brightens foam lighting. Risk: white/green pasted foam if coverage appears. |
| `_WaveFoamSpecularBoost` | `0.08` | `0.105` | LOW | Minor boost. Only risky with near-white foam colors. |
| `_DepthFogDensity` | `{0.09,0.12,0.12}` | `{0.025,0.032,0.04}` | MEDIUM | Reduces fog density, can improve surface clarity but can expose flat slabs/empty water. |
| `_Diffuse` | `{0.01,0.07,0.075}` | `{0.012,0.076,0.132}` | MEDIUM | Pushes blue channel. Useful against mud, but not enough to create depth by itself. |
| `_DiffuseGrazing` | `{0.03,0.15,0.165}` | `{0.105,0.285,0.435}` | MEDIUM | Brightens grazing water. Can help Subnautica-floor readability if not hiding clip failures. |
| `_DiffuseShadow` | `{0.004,0.032,0.038}` | `{0.008,0.038,0.066}` | LOW | Slight blue lift. |
| `_FoamBubbleColor` | `{0.06,0.16,0.15,1}` | `{0.73,0.93,0.92,1}` | HIGH | Huge jump from dark teal to near-white. Risk: overdriven foam if foam appears. |
| `_FoamWhiteColor` | `{0.58,0.82,0.78,0.72}` | `{0.92,1,0.96,1}` | HIGH | Near-white full alpha. Needs close shoreline proof or it will read as debug foam. |
| `_SkyAwayFromSun` | `{0.006,0.038,0.046}` | `{0.11,0.28,0.4}` | MEDIUM | Brightens surface reflection. Useful if surface was black, but not proof of realism. |
| `_SkyBase` | `{0.004,0.026,0.032}` | `{0.13,0.32,0.43}` | MEDIUM | Same. |
| `_SkyTowardsSun` | `{0.045,0.09,0.08}` | `{0.56,0.75,0.78}` | MEDIUM | Strong brightening. Watch for flat cyan sheen. |
| `_SubSurfaceColour` | `{0.018,0.11,0.125}` | `{0.018,0.095,0.135}` | LOW | Minor blue shift. |
| `_SubSurfaceShallowCol` | `{0.035,0.18,0.18}` | `{0.105,0.33,0.41}` | HIGH | Shallow color becomes much brighter/cyan. Can improve beauty or create acid/flat green if unsupported by depth/particles. |
| `_SubSurfaceShallowColShadow` | `{0.5379,0.6753,0.7926}` | `{0.03,0.125,0.175}` | HIGH | Shadow shallow color becomes much darker. Can create black bands/streaks in transition zones. |

### `Assets/Crest/Crest/Materials/Ocean-Underwater.mat`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| `_DepthFogDensity` | `{0.03,0.03,0.026635999}` | `{0.032,0.032,0.032}` | LOW | Minor uniform fog increase. |
| `_Diffuse` | `{0.1491,0.2252,0.2458}` | `{0.1263,0.2288,0.2655}` | MEDIUM | Slightly darker red and bluer. Can reinforce green/blue underwater grade. |
| `_DiffuseGrazing` | `{0.5733,0.5735,0.5558}` | `{0.4442,0.4787,0.4602}` | MEDIUM | Dims grazing underwater response. Risk: flatter/darker underside. |
| `_DiffuseShadow` | `{0.222195,0.387602,0.433724}` | `{0.222192,0.387599,0.433720}` | LOW | No meaningful change. |
| `_SkyAwayFromSun` | `{0.3989,0.4362,0.4990}` | `{0.2963,0.4744,0.5605}` | LOW | Blue/cyan shift. |
| `_SkyBase` | `{0.6664,0.7356,0.8}` | `{0.4646,0.5875,0.6506}` | MEDIUM | Darkens base sky reflection. Can worsen black/stale underwater surface if not balanced. |
| `_SubSurfaceColour` | `{0.5733,0.5735,0.5558}` | `{0.4442,0.4787,0.4602}` | MEDIUM | Dims subsurface. Risk: less luminous water volume. |
| `_SubSurfaceShallowCol` | `{0.5733,0.5735,0.5558}` | `{0.4442,0.4787,0.4602}` | MEDIUM | Same. |

### `Assets/Crest/Crest/Materials/Ocean_UnderwaterCurtain.mat`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| keyword `_CLIPUNDERTERRAIN_ON` | present | removed | BLOCKER | Curtain loses under-terrain clipping keyword. If visible, it can contribute to water/terrain plane artifacts. |
| keyword `_CAUSTICS_ON` | absent | present | BLOCKER | Existing current value `_CausticsStrength: 10` becomes dangerous when keyword activates. This is not acceptable without isolated proof. |
| keyword `_TRANSPARENCY_ON` | present | removed | HIGH | Removing transparency keyword can change curtain sorting/opacity behavior and produce hard plane reads. |

Current watch values in the curtain material include `_CausticsStrength: 10`, `_FoamScale: 15`, `_LightIntensityMultiplier: 5.31`, `_DepthFogDensity: {0.2,0.15,0.15,1}`, `_DiffuseGrazing: {0,0,0,1}`, `_DiffuseShadow: {0,0,0.099,1}`, and `_FoamBubbleColor: {0.435,1,0,1}`. Those values are not new diffs, but the keyword diff can expose them.

### `Assets/_Project/Art/Materials/MAT_H8_SurfaceCrestOcean_1428.mat`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| keyword `_CAUSTICS_ON` | absent | present | HIGH | Enables caustic path on the custom surface material. |
| `_CausticsBase` | `0.07` | `0.11` | MEDIUM | Raises base caustic floor. Risk: visible sheet even in low contrast areas. |
| `_CausticsDistortionStrength` | `0.075` | `0.105` | MEDIUM | May help breakup, but paired with high strength can streak. |
| `_CausticsStrength` | `0.22` | `1.45` | BLOCKER | 6.6x increase. Too high for unproven surface/shallow caustics. Likely neon/sheet risk. |
| `_CausticsTextureAverage` | `0.07` | `0.045` | MEDIUM | Sharper contrast. High strength makes this risky. |
| `_CausticsTextureScale` | `6.2` | `4.8` | MEDIUM | Larger features. Can read as broad streaks instead of fine lace. |
| `_FoamBubbleParallax` | `0.1` | `0.32` | HIGH | More apparent foam depth. Without contact proof, can become pasted. |
| `_FoamScale` | `0.0032` | `0.019` | HIGH | 5.9x larger. Risk: foam blobs/sheets. |
| `_LightIntensityMultiplier` | `0.52` | `1.95` | HIGH | 3.75x brighter. Can cause acid/flat cyan water and overbright foam. |
| `_NormalsStrength` | `0.34` | `0.38` | LOW | Small increase. |
| `_NormalsStrengthOverall` | `0.86` | `0.94` | LOW | Small increase. |
| `_RefractionStrength` | `0.35` | `0.42` | MEDIUM | Useful for water material identity; needs capture for shimmer/aliasing. |
| `_ShorelineFoamMinDepth` | `1.28` | `3.75` | HIGH | Much deeper shoreline foam threshold. Can smear foam offshore or miss tight contact depending Crest semantics. |
| `_Smoothness` | `0.52` | `0.62` | LOW | More specular. Not blocker by itself. |
| `_SmoothnessFar` | `0.18` | `0.33` | MEDIUM | More distant sheen. Can flatten horizon if terrain/water depth is weak. |
| `_Specular` | `0.24` | `0.36` | LOW | More sparkle. Needs surface proof. |
| `_SubSurfaceBase` | `0.11` | `0.48` | HIGH | Large subsurface lift. Can improve photic beauty or make flat acid water. |
| `_SubSurfaceDepthMax` | `3.2` | `4.6` | MEDIUM | Extends shallow response. |
| `_SubSurfaceSun` | `0.18` | `0.72` | HIGH | 4x sun subsurface response. Risk: glowing green/cyan water. |
| `_WaveFoamBubblesCoverage` | `0.78` | `1.95` | HIGH | 2.5x bubble coverage. Risk: overdriven broad foam. |
| `_WaveFoamBubblesFeather` | `0.32` | `0.48` | MEDIUM | Softer bubble edge. Could help if not over-covered. |
| `_WaveFoamBubblesStrength` | `0.637` | `1.18` | HIGH | 1.85x strength. |
| `_WaveFoamCoverage` | `0.52` | `0.27` | HIGH | Drops base wave foam while raising bubble foam. Risk: wrong kind of foam, still no shoreline contact. |
| `_WaveFoamFeather` | `0.4` | `0.18` | MEDIUM | Hardens base foam. Risk: technical edge. |
| `_WaveFoamLightScale` | `0.38` | `2.15` | HIGH | 5.7x brighter foam lighting. |
| `_WaveFoamSpecularBoost` | `0.025` | `0.19` | HIGH | 7.6x specular foam boost. |
| `_WaveFoamStrength` | `1.25` | `3.45` | HIGH | 2.76x strength. |
| `_DepthFogDensity` | `{0.02,0.02,0.02}` | `{0.018,0.022,0.021}` | LOW | Small color/density shift. |
| `_Diffuse` | `{0.032,0.128,0.158}` | `{0.06,0.28,0.27}` | HIGH | Large green/cyan lift. Acid/flat green risk if unsupported by depth variation. |
| `_DiffuseGrazing` | `{0.18,0.33,0.38}` | `{0.62,0.92,0.86}` | HIGH | Very bright grazing color. Can flatten the ocean into a luminous sheet. |
| `_DiffuseShadow` | `{0.012,0.03,0.045}` | `{0.08,0.22,0.24}` | MEDIUM | Strong shadow lift. Useful against black water, but can reduce depth contrast. |
| `_FoamBubbleColor` | `{0.34,0.82,0.86,0.22}` | `{0.76,1,0.88,0.98}` | HIGH | Alpha and brightness jump. Foam can become full-alpha green-white. |
| `_FoamWhiteColor` | `{0.64,0.92,0.9,0.42}` | `{0.98,1,0.91,1}` | HIGH | Full-alpha near-white foam. |
| `_SkyAwayFromSun` | `{0.008,0.055,0.064}` | `{0.24,0.58,0.64}` | HIGH | Huge sky reflection lift. Useful against black surface but can cause flat turquoise water. |
| `_SkyBase` | `{0.005,0.035,0.039}` | `{0.4,0.76,0.74}` | HIGH | Same class, stronger. |
| `_SkyTowardsSun` | `{0.06,0.115,0.105}` | `{0.66,0.94,0.88}` | HIGH | Same class. |
| `_SubSurface` | `{0.08,0.38,0.58,1}` | `{0.34,0.86,0.92,1}` | HIGH | Major cyan lift. |
| `_SubSurfaceColour` | `{0.025,0.085,0.105}` | `{0.12,0.46,0.52}` | HIGH | Major green/cyan lift. |
| `_SubSurfaceShallowCol` | `{0.08,0.18,0.22}` | `{0.64,0.92,0.84}` | BLOCKER | Strong acid-green/flat-water suspect. |
| `_SubSurfaceShallowColShadow` | `{0.015,0.04,0.055}` | `{0.14,0.4,0.42}` | HIGH | Shadow side becomes bright green/cyan. Depth structure can collapse. |
| `_SubSurfaceShallowColour` color entry | present `{0.5,0.78,0.92,1}` | removed | MEDIUM | Float toggle remains. Shader fallback/default behavior needs Unity/Frame Debugger proof. |

### `Assets/_Project/Data/Ocean/Sim_Settings_Foam.asset`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| `_foamFadeRate` | `0.5` | `0.42` | MEDIUM | Slower fade means foam persists longer. Can help contact, but can also smear broad bands. |
| `_waveFoamStrength` | `1.4` | `2.05` | HIGH | Stronger wave foam before shoreline proof. |
| `_waveFoamCoverage` | `0.75` | `0.82` | MEDIUM | More coverage. Needs screenshot proof. |
| `_shorelineFoamMaxDepth` | `1.2` | `1.85` | HIGH | Extends shoreline foam depth. Can create offshore banding if the input/mask is broad. |
| `_shorelineFoamStrength` | `2.8` | `4.25` | HIGH | Stronger shoreline foam without proven contact mask. |

### `Assets/_Project/Art/Materials/World/MAT_H8SurfaceShoreFoam_1428.mat`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| shader GUID | `650dd...` | `293b...` | HIGH | Shader family changed. Needs proof that it is the intended foam ribbon shader and SRP pass state is correct. |
| `_SURFACE_TYPE_TRANSPARENT` | valid keyword | invalid keyword | MEDIUM | Transparent state moved from URP keyword style to custom shader data. Needs render queue proof. |
| render queue | `3000` | `3012` | MEDIUM | Later transparent queue can sort on top of water/rocks. Sheet risk if mesh is broad. |
| `RenderType` tag | `Transparent` | removed | MEDIUM | Can affect render pipeline classification/debug expectations. |
| `_Alpha` | absent | `1` | HIGH | Full material alpha in foam shader. Risk: opaque-looking foam sheet. |
| `_EdgeFade` | absent | `0.08` | MEDIUM | Narrow edge fade. Risk: hard mesh outline. |
| `_Softness` | absent | `0.52` | LOW | Could soften threshold. |
| `_Threshold` | absent | `0.08` | HIGH | Low threshold means broad visible foam coverage. |
| `_FoamColor` | absent | `{0.94,1,0.97,1}` | HIGH | Full-alpha near-white. |
| `_TilingA` | absent | `{3.4,0.88,0.012,0.005}` | MEDIUM | Motion/tiling route needs visual proof. |
| `_TilingB` | absent | `{6.7,1.72,-0.015,0.007}` | MEDIUM | Same. |

### `Assets/_Project/Art/Materials/World/MAT_SurfaceSplashFoamDirty_1428.mat`

| Property | Old | Current | Risk | Audit |
|---|---:|---:|---|---|
| shader GUID | `650dd...` | `293b...` | HIGH | Shader family changed. Needs isolated proof. |
| `_SURFACE_TYPE_TRANSPARENT` | valid keyword | invalid keyword | MEDIUM | Same keyword migration risk. |
| render queue | `3000` | `-1` | MEDIUM | Queue now shader/default driven. Needs proof around water sorting. |
| `RenderType` tag | `Transparent` | removed | MEDIUM | Can affect render debug/path classification. |
| `_BaseMap` | null | texture `e330b3...` | MEDIUM | Texture binding is useful, but `_MainTex` remains null in current file. |
| `_Alpha` | absent | `0.46` | MEDIUM | Safer than full alpha, still needs proof. |
| `_EdgeFade` | absent | `0.16` | LOW | Less hard than shoreline material. |
| `_Softness` | absent | `0.22` | MEDIUM | Relatively tight softness. |
| `_Threshold` | absent | `0.42` | MEDIUM | More selective than shoreline material. |
| `_FoamColor` | absent | `{0.82,0.98,0.94,0.64}` | MEDIUM | Not full alpha. |
| `_TilingA` | absent | `{2.1,0.72,0.014,0.004}` | LOW | Needs proof only if active. |
| `_TilingB` | absent | `{4.8,1.22,-0.01,0.006}` | LOW | Same. |

## Current Cited Receiver Watchlist

These are not all current git diffs, but Batch23/24 cite them as live or candidate routes.

| Route | Current source state | Risk | Audit |
|---|---|---|---|
| `H8_FloorCausticSoft_1443` / `MAT_H8_FloorCausticSoft_1443` | Material `_Tint {0.58,0.92,1,0.24}`, `_ScaleA 1.05`, `_ScaleB 1.72`, `_Sharpness 8.2`; scene object cited active/enabled by Batch24. | HIGH | Improved versus stale Batch24 alpha `0.42`, but still additive/no texture/depth/light gate. Broad mesh can still read as sheet/streak. |
| `H8_PHOTIC_SOFT_WATER_HAZE_1430` | `_Alpha 0.3`, weak near/far color alpha. | MEDIUM | Can be too weak to create particulate depth while still tinting as a plane. |
| `H8_CREST_FOAM_INPUT_PASS_1464` / `MAT_H8_CrestFoamInput_1464` | `_Strength 4.8`; `RegisterFoamInput` route cited active; MeshRenderer disabled expected. | MEDIUM | Safe as Crest input only. Do not enable renderer. Needs Frame Debugger/Crest sim proof. |
| `H8_ORGANIC_SHORELINE_FOAM_FINE_1469` | disabled candidate, texture-bound, `_Alpha 0.72`, `_Threshold 0.18`, `_EdgeFade 0.1`. | MEDIUM | Safer than rejected sheet/blob routes, but disabled and unproven. One-route test only. |
| `H8_UnderwaterSuspendedSpecks_1446` | disabled candidate, `_Tint.a 0.34`, `_Softness 1.35`. | MEDIUM | Least dangerous missing particulate route. Activate only after slab/caustic isolation. |
| `H8_UnderwaterHazeCurtain_1454` | disabled, `_TopColor.a 0.72`, `_BottomColor.a 0.42`, `_CausticColor.a 0.32`. | BLOCKER if enabled raw | Broad high-alpha curtain can recreate green sheet/plane failure. Keep disabled until isolated proof route exists. |
| `H8_SurfaceFoamLace_1453`, `MAT_H8_SurfaceFoamBlob_1447`, `H8_VisibleFoamUnlit_1436`, `H8_VisibleBrokenFoam_1435`, `SURFACE_FOAM_RIBBON_*`, `WATER_CAUSTIC_RIB_*` | Batch23 rejected or watchlisted as sheet/grid/ribbon routes. | HIGH/BLOCKER | Do not use as first recovery route. |

## Likely Cause Map

### Acid / Flat Green Water

Most likely material contributors:

1. `MAT_H8_SurfaceCrestOcean_1428` color lift is extreme: `_SubSurfaceShallowCol` to `{0.64,0.92,0.84}`, `_SkyBase` to `{0.4,0.76,0.74}`, `_SubSurface` to `{0.34,0.86,0.92}`, and `_LightIntensityMultiplier` to `1.95`.
2. `MAT_H8_SurfaceCrestOcean_1428` `_SubSurfaceBase 0.48` and `_SubSurfaceSun 0.72` can make water glow uniformly instead of showing depth falloff.
3. `Ocean.mat` also brightens sky and foam colors, but it is less acid-green than the H8 surface material.
4. Missing active particulate/haze layering from Batch24 means color is carrying too much of the water read alone.

Useful changes: brightening the surface away from black/mud is aligned with `TASTE.md` and `water.md`.

Likely blocker: the H8 material's shallow/subsurface/sky color lift is too aggressive without depth/receiver proof.

### Black Streaks

Most likely contributors:

1. Batch24 scene geometry suspects remain the primary cause class: active rendered slabs/occlusion strips can create hard black horizontal cuts independent of material color.
2. `Ocean.mat` `_SubSurfaceShallowColShadow` drops from bright blue `{0.5379,0.6753,0.7926}` to dark teal `{0.03,0.125,0.175}`. That can create dark transition bands.
3. `Ocean_UnderwaterCurtain.mat` now has `_CAUSTICS_ON`, no `_TRANSPARENCY_ON`, no `_CLIPUNDERTERRAIN_ON`, and existing black grazing/shadow colors. If it renders, black/blue streaks are plausible.
4. `_ClipSurface`/`_ClipUnderTerrain` off can reveal water/terrain intersections as dark hard lines.

Useful changes: none of the black-streak risks are useful until isolated.

Likely blockers: clip keyword removal, clip floats off, curtain keyword change, and scene slabs.

### No Shoreline Foam

Most likely contributors:

1. There is no current proof that Crest foam simulation output is sampled by the active visible ocean material. `H8_CREST_FOAM_INPUT_PASS_1464` is configured as an input lane, not visible foam.
2. `Ocean.mat` reduced `_WaveFoamBubblesCoverage` from `1.68` to `0.42`; depending Crest semantics this can suppress one visible foam channel.
3. `MAT_H8_SurfaceCrestOcean_1428` drops `_WaveFoamCoverage` from `0.52` to `0.27` while raising bubble foam. It may produce wrong foam type instead of contact foam.
4. `MAT_H8_SurfaceCrestOcean_1428` `_ShorelineFoamMinDepth 3.75` and `Sim_Settings_Foam._shorelineFoamMaxDepth 1.85` can push/expand shoreline logic beyond tight contact. This may create offshore haze/foam while still failing the waterline.
5. Mesh fallback routes remain disabled or rejected as sheet/grid risks. That is correct until Crest proof exists.

Useful changes: `Sim_Settings_Foam` boosts and the 1469 fine foam material could help after a proven mask/contact route.

Likely blocker: missing Crest sim/material proof, not just numeric strength.

### Caustics As Sheet / Streak

Most likely contributors:

1. `MAT_H8_SurfaceCrestOcean_1428` `_CausticsStrength 1.45` is a direct overdrive risk.
2. `Ocean.mat` newly enables caustics with `_CausticsStrength 0.56` and finer scale. This is not automatically wrong, but unproven.
3. `Ocean_UnderwaterCurtain.mat` activates `_CAUSTICS_ON` while carrying `_CausticsStrength 10`. If the curtain path renders, this is a blocker.
4. `MAT_H8_FloorCausticSoft_1443` is additive, procedural/sine, and lacks source/depth/light gating. Current alpha `0.24` is better than stale `0.42`, but still needs isolate proof.
5. Batch23 rejected caustic rib routes as recovery visuals. Do not use them as a broad fix.

Useful changes: enabling subtle caustics is required for photic beauty when motivated by shallow light.

Likely blockers: `MAT_H8_SurfaceCrestOcean_1428` strength `1.45`, underwater curtain caustic keyword, and active floor caustic mesh if broad.

### Terrain / Water Clipping Artifacts

Most likely contributors:

1. `Ocean.mat`: `_ClipSurface 1 -> 0`, `_ClipUnderTerrain 1 -> 0`, and removal of both clip keywords.
2. `Ocean_UnderwaterCurtain.mat`: `_CLIPUNDERTERRAIN_ON` removed.
3. Batch24 static scene suspects: `H8_DEPTH_LOW_SHELF_1428`, `H8_WORLD_LOW_WATER_OCCLUSION_00..03_1428`, `H8_DEPTH_CEILING_OCCLUSION_1428`, `NOIR_UPPER_PRESSURE_LID`.
4. Active/broad transparent receiver meshes can visually imitate clipping if sorted over terrain/water.

Useful changes: none. Clip-off is not a valid visual fix without exact proof.

Likely blocker: clip-off changes are the first material-side rollback test after scene-slab isolation.

## Reversible Unity Owner Test Matrix

All tests require exact camera reuse, baseline screenshot, after screenshot, active scene, camera transform, fog state, ocean material property snapshot, enabled suspect renderers, and log tail newer than the screenshot. Roll back after each test unless it clearly proves the offender.

| Step | Test | What to change temporarily | Expected proof | Rollback |
|---:|---|---|---|---|
| 1 | Baseline | Change nothing. Capture current surface, shoreline, underwater 0-5 m, underwater 20-50 m. | Establish current failure after latest diffs. | None. |
| 2 | Service slab isolation | Disable only `H8_DEPTH_LOW_SHELF_1428` MeshRenderer. | If hard shelf/wall changes, geometry is primary. | Re-enable renderer. |
| 3 | Water occlusion strip isolation | Disable `H8_WORLD_LOW_WATER_OCCLUSION_00..03_1428` MeshRenderers as one group. | If black/green band or blue wall changes, service strip route is primary. | Re-enable all four. |
| 4 | Ceiling/lid isolation | Disable `H8_DEPTH_CEILING_OCCLUSION_1428`, then `NOIR_UPPER_PRESSURE_LID` separately. | Prove/disprove overhead/transparent slab contribution. | Re-enable tested renderer before next group unless proven. |
| 5 | Crest clip rollback | On `Ocean.mat` only: restore `_ClipSurface 1`, `_ClipUnderTerrain 1`, `_CLIPSURFACE_ON`, `_CLIPUNDERTERRAIN_ON`; do not alter foam/caustics in the same pass. | If terrain/water slicing improves, clip-off is blocker. | Restore current clip-off values only if test disproves it. |
| 6 | Underwater curtain keyword isolation | If curtain path is visible/enabled, restore `_CLIPUNDERTERRAIN_ON`, `_TRANSPARENCY_ON`, and remove `_CAUSTICS_ON` for the curtain only. | If curtain/streak/plane read improves, curtain keyword diff is blocker. | Restore current keyword state only if disproven. |
| 7 | Active caustic receiver isolation | Disable only `H8_FloorCausticSoft_1443` MeshRenderer. | If bright sheet/streak disappears, retune receiver before adding effects. | Re-enable renderer. |
| 8 | Caustic strength isolation | With clipping proven clean: test `MAT_H8_SurfaceCrestOcean_1428._CausticsStrength` at old `0.22`, then current `1.45`; do not change foam. | Prove if high caustic strength causes streak/sheet. | Return to chosen proven value and record exact state. |
| 9 | Surface color isolation | With clipping and caustics isolated: test H8 surface shallow/subsurface color block against old values or a conservative midpoint. | Prove acid/flat green source. | Restore exact before state if not offender. |
| 10 | Crest foam proof | Keep `H8_CREST_FOAM_INPUT_PASS_1464` MeshRenderer disabled. Use Frame Debugger/Crest debug to prove foam sim and material sampling. | If sim is absent, value boosts cannot fix foam. | Restore any debug view/settings. |
| 11 | Foam value isolation | Test `Sim_Settings_Foam` old values versus current boosted values after Crest sampling proof. | Prove whether boosts create contact or just broad foam. | Restore exact values. |
| 12 | One authored foam candidate | Test only `H8_ORGANIC_SHORELINE_FOAM_FINE_1469`. | Accepted only if narrow organic contact foam appears with no mesh sheet/grid. | Disable object and renderer if rejected. |
| 13 | Particulate depth | Test `H8_UnderwaterSuspendedSpecks_1446` only after slab/caustic failure is removed. | Sparse motes and depth evidence, no full-screen additive wash. | Set `m_IsActive 0`. |
| 14 | Weak horizon haze | Test weak `H8_UnderwaterHorizonHaze_1437`, not `H8_UnderwaterHazeCurtain_1454`. | Far-depth structure without curtain sheet. | Restore disabled state. |

Do not raw-enable `H8_UnderwaterHazeCurtain_1454`, `H8_SurfaceFoamLace_1453`, `MAT_H8_SurfaceFoamBlob_1447`, `H8_VisibleFoamUnlit_1436`, `H8_VisibleBrokenFoam_1435`, `SURFACE_FOAM_RIBBON_*`, or `WATER_CAUSTIC_RIB_*` as first fixes.

## Material Strategy Consequences

### Low / Compact

- Keep clip correctness before visual richness.
- Use Crest foam only if Frame Debugger proves sampling.
- Use one subtle caustic receiver only where shallow light has a reason.
- Use sparse specks or weak horizon haze; no broad curtain, no caustic strength overdrive, no full-alpha foam sheet.
- `GlobalQualityWeight` should scale strength/cadence/coverage continuously, not flip features on/off.

### Middle

- Add proven Crest shoreline foam plus one particulate/depth route.
- Caustics can use a modest moving lace route if receiver proof is clean.
- Keep surface bright, but reduce H8 surface acid/cyan overdrive if it collapses depth.

### High

- Spend saved slab/overdraw budget on better wet rock response, foam breakup, finer caustic texture, and local silt/particulate density.
- Stronger foam/caustics are allowed only after the low/middle route is clean and screenshots prove no sheet/grid/clip artifacts.

### Ultra

- Layer surface Crest caustics, fine shoreline foam, particulate depth, local haze, wet rocks, and richer reflection only after each route passes separately.
- Ultra buys sensory density. It must not introduce new gameplay truth or hide a broken base material/clip route.

## Top Findings

1. `Ocean.mat` clip-off is a blocker-risk diff: `_ClipSurface` and `_ClipUnderTerrain` changed from `1` to `0`, and both clip keywords were removed. Test this before adding more haze or foam.
2. `Ocean_UnderwaterCurtain.mat` is a hidden high-risk keyword diff: `_CAUSTICS_ON` replaced `_CLIPUNDERTERRAIN_ON`, `_TRANSPARENCY_ON` was removed, and existing `_CausticsStrength: 10` can become visible if the curtain path renders.
3. `MAT_H8_SurfaceCrestOcean_1428` is overdriven: `_CausticsStrength 1.45`, `_WaveFoamStrength 3.45`, `_WaveFoamLightScale 2.15`, `_FoamWhiteColor.a 1`, `_SubSurfaceShallowCol {0.64,0.92,0.84}`, and `_LightIntensityMultiplier 1.95` are plausible causes of acid/flat green water and sheet caustics.
4. Foam is not solved by value boosts. Current evidence still lacks Crest foam simulation/material sampling proof and lacks shoreline contact screenshots.
5. The active floor caustic receiver is less aggressive than Batch24's stale values but still unaccepted: additive, procedural, broad-mesh caustic receivers remain sheet/streak risks until isolated.
6. Missing underwater particulate/depth is a routing/proof problem. `H8_UnderwaterSuspendedSpecks_1446` is the least dangerous next candidate; raw haze curtain activation is rejected.

## Static Verdict

The next safe Unity-owner path is: isolate scene slabs, restore/verify Crest clipping, isolate the active floor caustic receiver, then prove Crest foam sampling before any authored foam fallback. Do not revert every visual change blindly. The useful direction is brighter surface/photic water and real foam/caustic presence. The blocker direction is clip-off, curtain caustic activation, full-alpha foam, broad receiver sheets, and acid-green subsurface overdrive without depth evidence.

Acceptance remains `PENDING UNITY/PROFILER VERIFICATION`.
