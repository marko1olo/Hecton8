# Sky, Aegir, Moons Source Role Package

Date: 2026-06-04
Workspace: `C:\hades\Hecton8`
Evidence class: STATIC_DOC / STATIC_SOURCE
Unity/build/import: NOT RUN
Assets edited: NO

## Scope

Offline source-role package for Aegir, sky, moons, clouds, atmospheric occlusion, day gradient, and horizon veil. This package is a Unity-owner handoff. It does not modify `Assets`, does not import generated files, does not prove live material binding, and does not certify runtime visuals.

Fresh capture reviewed:

- `Docs/Orchestration/Captures/unity_focus_state_20260604_125701.png`

Observed defect from that capture: Aegir has scale but reads as a pale translucent disc/sticker. Texture authority, cloud-band breakup, limb softness, atmospheric occlusion, and horizon integration are not strong enough. Blue/purple methane direction is acceptable by `VISION_LOCKS.md` and `TASTE.md`; pale flat sticker presentation is rejected.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `celestial.md`
- `atmosphere.md`
- `lighting.md`
- `water.md`
- `rendering.md`
- `presentation.md`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/REND_Shader_Noir_Aesthetics_Dithering_Fog.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `Docs/Reports/Batch18/1808_AEGIR_SKY_ACTIVE_PATH_AUDIT.md`
- `Docs/Reports/Batch18/1808_AEGIR_SKY_BINDING_MATRIX.csv`
- `Docs/Reports/Batch18/1865_SKY_OCEAN_PRIMITIVE_RISK_PROOF_PACKET.md`
- `Docs/Reports/Batch18/1865_SKY_OCEAN_PRIMITIVE_RISK_MATRIX.csv`
- `Docs/Reports/Batch18/1873_SKY_OCEAN_PROOF_SHOT_LIST.csv`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_MATRIX.csv`

No Batch19 sky/ocean report matching sky, ocean, Aegir, moon, celestial, atmosphere, or water was found.

## Static Inputs From Earlier Reports

Prior static reports identify the likely active route:

- Active skybox route: `Mat_HectonSky.mat`, with unresolved cloud GUID risk.
- Active Aegir route by static scene text: `H8_SURFACE_AEGIR_GAS_GIANT_REAL_1428` using `MAT_AegirGasGiant_Impostor_1428`.
- Active Aegir textures by static material reference: `clouds0_diff.png`, `Aegir_storms.png`, and `Sky/oblakajip.png`.
- Active moon risk: `MAT_CelestialMoon_Khepri` and `MAT_CelestialMoon_Thalos` reportedly use a basalt terrain color texture; `SURFACE_MOON_A_1428` and `SURFACE_MOON_B_1428` are flat/null-texture risks.
- Active cloud risk: multiple transparent cloud cards need horizon capture and overdraw proof.

These are STATIC_SOURCE facts only. They do not prove live Unity material instances, visual quality, Frame Debugger pass order, profiler cost, or compact/high quality behavior.

## Source Role Doctrine

The Unity owner should treat every texture below as a source role, not a guaranteed asset path. The role names are stable handoff terms. Final import names, folders, and material binding belong to the celestial/atmosphere/rendering owner.

Rules:

- Do not use darkness, storm, eclipse, fog, bloom, crop, or overexposure to hide weak sky or planet art.
- Do not cut Aegir directly against the horizon with a texture alpha edge. Horizon loss must come from atmospheric haze, optical depth, and veil masks.
- Do not make low-tier or compact visuals ugly. `GlobalQualityWeight = 0.0` keeps scale, color, silhouette, route readability, and premium texture identity.
- Do not create binary low/high art swaps. Quality changes scale texture residency, mask sharpness, layer count, update cadence, and optional atmospheric richness continuously.
- Do not use ProductFace PBR masks for sky/celestial roles unless the route shader declares those channels.
- Do not use procedural sine stripes, muddy noise, AI baked-light artifacts, flat moon discs, or generic blue/purple gradient sky as final source.

## Required Source Roles

Machine-readable matrix:

- `Docs/Reports/Batch20/sky_aegir_moons_source_roles_20260604.csv`

Primary roles:

| Role | Purpose | Required channel semantics | Reject if |
|---|---|---|---|
| `AEGIR_ALBEDO_CLOUD_BANDS` | Main methane-rich visible Aegir color and cloud-band authority. | sRGB equirectangular or disc-impostor source. Large-scale bands, storm cells, cloud streaks, polar/subpolar variation. | Pale wash, sticker disc, sine stripes, muddy blur, low-res smear, no band hierarchy. |
| `AEGIR_STORM_DENSITY_MASK` | Overlay mask for storm depth and band breakup. | Linear grayscale or RGBA masks. No baked lighting. | Reads as random noise, same density everywhere, or makes the planet dirty instead of structured. |
| `AEGIR_HAZE_LIMB_SOFTNESS` | Limb glow, edge falloff, terminator softness, atmosphere thickness. | Linear radial/edge mask or shader control map. | Hard cutout, transparent sticker edge, rim halo disconnected from sun/sky. |
| `ATMOSPHERIC_OCCLUSION_MASK` | Haze occlusion between Aegir and horizon/cloud layers. | Linear alpha/height mask. World or screen-space usage controlled by Unity owner. | Planet texture is cut at horizon, haze hides all detail, mask becomes opaque fog wall. |
| `SURFACE_CLOUD_PANORAMA_A` | Day surface cloud body and sky depth. | sRGB cloud color/luminance, tile/seam-safe if panorama. | Flat white blobs, card edge visible, generic stock clouds, crushed horizon. |
| `SURFACE_CLOUD_COVERAGE_MASK` | Cloud edge density, shadow softness, veil interaction. | Linear coverage/alpha mask. | Uniform alpha, noisy grit, no soft edge, kills route visibility. |
| `MOON_ALBEDO_SET` | Body-specific moon identity. | sRGB albedo per moon. Crater, ice, basalt, salt, scar, or methane frost identity must be body-specific. | Basalt terrain reused visibly, flat grey ball, tiny debug disc, no scale cue. |
| `MOON_NORMAL_HEIGHT_SET` | Moon crater relief and rim detail. | BC5-suitable normal, optional height if shader declares it. | Baked lighting in normal, noisy fake relief, specular plastic look. |
| `MOON_PHASE_TERMINATOR_MASK` | Phase, occultation, and limb readability support. | Linear phase/terminator mask or shader-friendly ramp. | Binary black cut, unmotivated crescent, phase changes gameplay truth. |
| `DAY_GRADIENT_SKY_LUT` | Bright surface day gradient and exposure anchor. | 256x16 or 1024x256 LUT/ramp, linear/HDR route as shader declares. | One-note blue, purple sci-fi gradient, white wash, surface mud. |
| `HORIZON_VEIL_MASK` | Coastline/sky/ocean blend and distant haze. | Linear alpha/depth haze mask. Must preserve route silhouettes. | Fog hides weak terrain, cuts Aegir, removes coastline/ocean readability. |
| `PLANET_SHINE_WATER_CONTEXT_CUE` | Optional Aegir-color influence on ocean/atmosphere response. | Low-frequency color/ramp guidance, not gameplay truth. | Makes ocean purple soup, hides water color, becomes required for navigation. |

## Unity Owner Handoff

The Unity owner must discover actual shader slots before import or binding. This package intentionally avoids writing `Assets` paths.

Required Unity-owner steps:

1. Confirm active route in `02_HECTON_WORLD` with Inspector and Frame Debugger.
2. Identify actual shader properties for `MAT_AegirGasGiant_Impostor_1428`, `Mat_HectonSky.mat`, active cloud materials, and active moon materials.
3. Decide whether existing `clouds0_diff.png` / `Aegir_storms.png` are salvageable at source quality or must be replaced by generated source.
4. Bind any generated sources only through route-owned materials, not ProductFace donor materials and not unapproved third-party clones.
5. Preserve continuous `GlobalQualityWeight` scaling for residency, layer count, mask sharpness, cloud density, haze opacity, and optional high-tier atmospheric polish.
6. Capture proof shots listed below before claiming visual acceptance.

## Reject Gates

Reject the source package or runtime binding if any proof shot shows:

- Aegir as a pale translucent disc, sticker, or sphere with no atmospheric integration.
- Aegir cloud bands as sine stripes, random noise, mud, or low-resolution smear.
- Horizon occlusion achieved by cutting the planet texture instead of atmospheric/haze veil.
- Moons as flat/null-texture/debug discs or terrain texture stand-ins in final camera framing.
- Day sky as one-note blue/purple gradient, flat fog wall, or dark/noir cover.
- Surface clouds as visible cards with hard alpha edges or unrelated stock cloud blobs.
- Coastline/ocean/skies hidden by bloom, fog, darkness, storm, crop, or eclipse-only framing.
- Compact path loses scale, route readability, water color, sky brightness, or Aegir/moon silhouette.
- Ultra path adds new gameplay truth rather than sensory richness.
- Any report claims Unity/import/profiler/build readiness without artifact paths.

## Required Proof Shots

These are for the future Unity owner. This worker did not run Unity.

| Shot ID | Camera | Required elements | Required tier | Evidence |
|---|---|---|---|---|
| SKY20-01 | Surface day, player eye, horizon | Aegir, sky gradient, cloud layers, coastline, ocean skin. | Compact | Player capture |
| SKY20-02 | Same camera as SKY20-01 | Same route at Middle, High, Ultra. | Middle/High/Ultra | Matched player captures |
| SKY20-03 | Low waterline view | Aegir over ocean, horizon veil, foam/specular/wet rock. | Compact | Player capture |
| SKY20-04 | Aegir close framing from normal playable route | Band detail, storm mask, limb softness, no sticker edge. | High | Player capture |
| SKY20-05 | Horizon occlusion test | Aegir partially lost through atmosphere/haze, not texture cut. | Middle | Player capture plus Frame Debugger |
| SKY20-06 | Moon visibility shot | Each active visible moon reads as textured body with phase/terminator. | Middle | Player capture |
| SKY20-07 | Cloud edge/horizon shot | Cloud deck depth, soft alpha, no flat card edge. | Middle | Player capture plus Frame Debugger |
| SKY20-08 | Storm/eclipse optional event | Temporary dimming preserves route silhouette and Aegir/moon relation. | High | Player capture |
| SKY20-09 | Frame Debugger material binding frame | Active skybox, Aegir, clouds, moons, sun disc, pass order, texture bindings. | Compact | Frame Debugger |
| SKY20-10 | Profiler/GC frame set | Celestial/sky/cloud pass cost, overdraw, GC allocation, active quality scalar. | Compact | Profiler/GC |

## Scalability Consequences

Compact / Low:

- Aegir stays readable with clear silhouette, non-flat banding, and softened limb. Use lower mip residency, fewer overlays, and simple haze masks before reducing visual identity.
- Day gradient stays bright and clean. Horizon veil preserves coastline and ocean route cues.
- Moons remain textured silhouettes with phase read. No flat discs.
- Surface clouds keep soft shape even if layer count is reduced.

Middle:

- Add stronger cloud breakup, more stable horizon veil, moon phase detail, and Aegir storm-density overlay.
- Improve day gradient transitions and water/sky color continuity.
- Keep route truth unchanged.

High:

- Add cleaner limb/terminator softness, richer storm bands, better cloud shadow/veil interaction, moon normal detail, and selective atmospheric shafts if shader route supports them.
- Require Frame Debugger proof for transparent cloud and haze pass cost.

Ultra:

- Add visual overkill: higher source texture residency, richer layered clouds, stronger Aegir atmospheric thickness, subtle planet-shine color context, sharper moon relief, and denser horizon haze detail.
- Ultra cannot be the only tier where the sky is readable.

## Top Blockers

1. Aegir currently reads as a pale translucent sticker in the reviewed capture. It needs source texture authority plus limb/haze integration, not a color-only tweak.
2. Horizon occlusion must be routed through atmospheric veil masks. Direct texture clipping will preserve the current fake/sticker read.
3. Moon texture roles remain unresolved and prior static reports flag basalt/null texture risks.
4. Active sky/cloud material bindings include unresolved/static-only risk and need Unity-slot proof.
5. Cloud cards and haze masks can solve integration only if Frame Debugger proves they are correctly ordered and not hiding weak art.
6. No Unity, import, profiler, Frame Debugger, or build proof was produced by this task.

## Evidence Claims

Claim: Prior static reports identify likely active Aegir, sky, moon, and cloud routes, but visual acceptance is unproven.
Evidence Class: STATIC_SOURCE
Artifact: `Docs/Reports/Batch18/1808_AEGIR_SKY_ACTIVE_PATH_AUDIT.md`, `Docs/Reports/Batch18/1808_AEGIR_SKY_BINDING_MATRIX.csv`, `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
Command or Unity tool: PowerShell file reads / `rg`
Date: 2026-06-04
Residual risk: runtime route may differ; Unity proof required.

Claim: The reviewed capture shows Aegir as pale, translucent, and weakly integrated.
Evidence Class: STATIC_DOC
Artifact: `Docs/Orchestration/Captures/unity_focus_state_20260604_125701.png`
Command or Unity tool: static image review only
Date: 2026-06-04
Residual risk: single capture angle/state; no runtime inspection was performed.

Claim: This task produced only offline handoff artifacts and did not run Unity/build/import or edit `Assets`.
Evidence Class: STATIC_DOC
Artifact: this report plus created CSV/prompt files
Command or Unity tool: none
Date: 2026-06-04
Residual risk: Unity owner must perform all runtime proof.

## Final State

STATIC SOURCE-ROLE PACKAGE COMPLETE.

Three-pillar acceptance is not complete. Graphics are not Unity-verified. Optimization is not profiler-verified. Gameplay/readability impact is not runtime-verified.
