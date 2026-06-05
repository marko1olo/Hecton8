# Texture Authoring Manifest 3212 - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence boundary: `STATIC_SOURCE` / `STATIC_IMAGE_QA` only.
Owner lane: Asset Worker 3212 - Texture Authoring Manifest Owner.
Write scope: source-authoring plan only. No `Assets/` edits. No import. No Unity run. No dotnet run.
First-20 route blocker addressed: bright surface exit, shoreline, photic shallows, and Aegir/sky source confusion is converted into route-owned PBR authoring packs.

Mandates followed:

- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `streaming.md`

Hard rejections:

- `Assets/_Project/Art/TEXTURES/foam.png` is rejected as visible shoreline/waterline art. It may be used only as source/reference/support until replaced by a route-owned RGBA contact mask pack.
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png` is rejected as final hero Aegir. It remains prototype/source only.
- Generated Batch31/Gemini wet basalt and shell/sand files are source/reference only. Do not direct-import them as final product material art.
- No pack below has Unity import proof, material-slot readback, route screenshot proof, VRAM proof, or runtime residency proof.

## Global Authoring Rules

Import target by map role:

- Albedo / beauty color: sRGB true, BC7 high quality on Standalone, ASTC 6x6 or platform-approved equivalent on mobile/XR lanes.
- Normal: texture type NormalMap, sRGB false, BC5 where supported.
- Packed MRAO/masks: sRGB false, BC7 high quality on Standalone, ASTC 6x6 or platform-approved equivalent on mobile/XR lanes.
- World/sky/terrain textures: mipmaps enabled, streaming mips enabled unless a future Unity owner documents an exception.
- No shipped uncompressed RGB/RGBA world texture.
- No runtime `Texture2D` generation, compression, pixel fill, or mask baking for production gameplay.

Streaming rule:

- Import work must obey the global async upload budget after hardware tier resolution: low/compact 64 MB and 1 ms, middle 128 MB and 2 ms, high/ultra 256 MB and 4 ms.
- `asyncUploadPersistentBuffer` remains a gameplay bootstrap setting, not a texture-pack setting.
- Texture residency must scale continuously through `GlobalQualityWeight`; no binary low/high switches.
- If texture budget pressure crosses the graduation threshold, mip residency downgrades before visual identity is destroyed.
- Terrain variety uses texture arrays by default. Streaming Virtual Texturing is not claimed for this manifest and remains blocked until GPU memory, page miss, shader variant, and MX350-class motion proof exist.

## Pack 1 - Wet Basalt Shoreline

Route role: bright shoreline, wet coastline rocks, route-edge basalt, shallow-water rock contact.

Source paths:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_Color.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/Rock031_1K-JPG_NormalGL.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_AlbedoSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_HeightSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_MRAOSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_NormalSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429/TX_B31_WetBasaltShoreline_1429_SourceCrop.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429_FullSourcePrototype/TX_B31_WetBasaltShoreline_1429_FullSourcePrototype_SourceFull.png`
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png`
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png`
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png`

Rejected sources / uses:

- Direct final import of any Batch31/Gemini wet basalt output.
- Direct use of `TX_H8_WetBasaltShoreline_Albedo_1428.png` as broad terrain art; static row has streaming mips disabled and static review flags clean PBR work as required.
- Random scanned terrain tiles mixed into the shoreline route without one named material family and visual breakup proof.

Intended channels:

- `TX_H8_WetBasaltShoreline_Author_Albedo`: basalt base color only, no baked directional light, no painted wet highlight.
- `TX_H8_WetBasaltShoreline_Author_Normal`: tangent-space fracture and chipped edge normal, BC5 target.
- `TX_H8_WetBasaltShoreline_Author_MRAO`: R = metallic 0 except rare mineral inclusions, G = roughness, B = cavity AO, A = wetness/waterline mask.
- Optional `TX_H8_WetBasaltShoreline_Author_Detail`: fine wet mineral grain/detail normal or grayscale detail source for near-field layering.

Compression / import target:

- Albedo: sRGB true, BC7, mipmaps on, streaming mips on, 2048 default world/hero target after cleaned authoring.
- Normal: NormalMap, sRGB false, BC5, mipmaps on, streaming mips on.
- MRAO/detail masks: sRGB false, BC7, mipmaps on, streaming mips on.

Material slot target:

- `Mat_Terrain.mat` plus `TerrainMaster.shader` wet basalt terrain layer.
- Candidate route materials: `MAT_H8_HeroWetBasaltRock_1453.mat`, `MAT_H8_AuthoredWetBasaltBreakup_1465.mat`, and `H8_PhoticTerrainLit_1453.shader`.
- Slot intent: Slot 0 primary basalt; wetness/contact driven by MRAO alpha or terrain control mask, not material clones.

Visual risks:

- Repeated macro rock islands and visible 2x2 tiling.
- Baked-light/highlight contamination fighting scene lighting.
- Too-dark shoreline used as noir cover instead of bright readable coast art.
- Missing streaming mip settings on existing generated/source candidates.
- Texture-array/SVT misuse increasing independent sampled Texture2D bindings.

GlobalQualityWeight consequences:

- Low/compact near 0.0: keep basalt identity with 1024 resident mips, baked AO, reduced decal density, and lower detail-normal strength. No flat dark replacement.
- Middle around 0.35: 2048 terrain/hero shoreline maps, full MRAO, stronger fracture normal, limited waterline decals.
- High around 0.7: longer mip residency, richer wet edge sheen, more shoreline breakup masks, stronger near-field detail normal.
- Ultra near 1.0: optional 4096 source bake for hero-only inspection surfaces after memory proof; spend surplus on decal layering and wet/mineral response, not extra material slots.

Proof still required:

- 2x2 tile seam and mip preview.
- Histogram and baked-light rejection.
- MRAO channel independence report.
- Unity import report for sRGB/type/compression/mips/streaming mips.
- Material-slot readback for terrain and candidate wet basalt materials.
- Bright shoreline and first-exit route screenshots.
- Texture memory, reserved memory, and streaming spike proof before route placement.

## Pack 2 - Shell/Sand Photic Bed

Route role: photic shallows, shell/sand substrate, first salvage floor readability, shallow seabed material identity.

Source paths:

- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_AmbientOcclusion.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_Color.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand +green/Ground074_1K-JPG_NormalGL.jpg`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/Ground079S_1K-PNG_Color.png`
- `Assets/_Project/Art/TEXTURES/Terrain Textures/sand/NORMAL.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_AlbedoSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_HeightSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_MRAOSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_NormalSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticShellSandSubstrate_2102/TX_B31_PhoticShellSandSubstrate_2102_SourceCrop.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_AlbedoSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_HeightSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_MRAOSource.png`
- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_PhoticSeabedSubstrate_2102/TX_B31_PhoticSeabedSubstrate_2102_NormalSource.png`
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png`

Rejected sources / uses:

- Direct final import of any Batch31/Gemini shell/sand or seabed output.
- Shell/sand sources with baked highlights, baked shadows, or tile repeats after 2x2 check.
- Use as random terrain scan filler without named photic-bed material role.

Intended channels:

- `TX_H8_PhoticShellSandBed_Author_Albedo`: clean shell fragments, pale sand, mineral tint, no baked light.
- `TX_H8_PhoticShellSandBed_Author_Normal`: ripple, grain, shell chips, shallow erosion.
- `TX_H8_PhoticShellSandBed_Author_MRAO`: R = metallic 0, G = roughness, B = AO under shells/ripples, A = shell density or wetness blend.
- Optional `TX_H8_PhoticShellSandBed_Author_Detail`: fine shell grit/ripple detail for near camera.

Compression / import target:

- Albedo: sRGB true, BC7, mipmaps on, streaming mips on, 2048 default for key photic route substrate.
- Normal: NormalMap, sRGB false, BC5, mipmaps on, streaming mips on.
- MRAO/detail masks: sRGB false, BC7, mipmaps on, streaming mips on.

Material slot target:

- `Mat_Terrain.mat` plus `TerrainMaster.shader` photic sand/shell layer.
- Candidate route shader: `H8_PhoticTerrainLit_1453.shader`.
- Slot intent: Slot 0 primary substrate; shell density/wetness from packed mask, not separate material-per-patch.

Visual risks:

- Baked shadow/highlight in generated sources.
- Shell fragments turning into noisy white mush at mips.
- Sand becoming flat beige fill instead of readable photic seabed.
- Over-bright substrate fighting water color and caustic readability.
- Incorrect normal import on `NORMAL.png` dimensions/source role.

GlobalQualityWeight consequences:

- Low/compact near 0.0: 1024 resident mips, baked AO retained, shell/ripple silhouettes preserved, reduced detail layer intensity. No blank sand fallback.
- Middle around 0.35: 2048 substrate maps, full MRAO, stable ripple normal, limited shell-density variation.
- High around 0.7: richer shell density masks, localized wet/dry and caustic receiver variation, longer near-field mip residency.
- Ultra near 1.0: hero photic-bed bake or 4096 source derivation for inspection surfaces after memory proof; denser shell/decal layering without changing terrain authority.

Proof still required:

- 2x2 tile seam and mip preview.
- Shell/sand histogram and baked-light rejection.
- MRAO channel independence report.
- Unity import report for sRGB/type/compression/mips/streaming mips.
- Terrain material readback and photic route receiver proof.
- Bright photic shallows screenshots with water color and seabed material readable.
- Texture memory, reserved memory, and streaming spike proof before route placement.

## Pack 3 - Foam/Contact RGBA Masks

Route role: shoreline contact, waterline breakup, salt rim, wet residue, Crest-compatible foam contribution, optional visor/UI support reuse only where explicitly bound.

Source paths:

- `Assets/_Project/Art/TEXTURES/foam.png`
- `Assets/_Project/Art/TEXTURES/Detali/mineral seep mask - looks seamless.png`
- `Assets/_Project/Art/TEXTURES/Detali/Mineral Seep Mask - second try.png`
- `Assets/_Project/Art/TEXTURES/Detali/Soft Plume Noise - second try.png`
- `Assets/_Project/Art/TEXTURES/Detali/soft_plume_noise_-_kakoy_to_seryy_nu_norm.png`
- `Assets/_Project/Art/TEXTURES/Detali/visor droplet mask.png`
- `Assets/_Project/Art/TEXTURES/Detali/visor runoff normal.png`

Rejected sources / uses:

- `foam.png` as visible shoreline/waterline art. Static review says it reads as repeated turquoise pool foam.
- Visor droplet/runoff sources as world shoreline art. They are support/reference candidates, not shoreline route art.
- Any Crest runtime wrapper, material clone, or runtime material override.
- Any per-droplet/per-bubble simulation as default contact truth.

Intended channels:

- `TX_H8_FoamContact_Author_RGBA`: R = salt rim / mineral residue, G = bubble breakup / microfoam, B = wet contact darkening / waterline persistence, A = foam opacity/contribution.
- Optional `TX_H8_FoamContact_Detail_Normal`: linear normal/detail support for close camera only if material route proves it does not add wasteful bindings.
- Optional `TX_H8_FoamContact_CausticReceiverMask`: linear mask for where shallow caustic contact may appear; no visible cheap caustic planes.

Compression / import target:

- RGBA mask: sRGB false, BC7 high quality, mipmaps on, streaming mips on, 1024 or 2048 according to route proof.
- Detail normal: NormalMap, sRGB false, BC5, mipmaps on, streaming mips on.
- Any support mask currently with sRGB enabled must be corrected during import work, not in this static manifest.

Material slot target:

- Crest ocean material asset foam/contact contribution slot, assigned through existing asset material route only.
- Shoreline/wet basalt and photic-bed material contact slot through packed mask or decal receiver.
- Visor material support remains separate; it must not be promoted into world foam art.

Visual risks:

- `foam.png` visible repeat, turquoise pool color, and non-premium waterline look.
- Streaming mips disabled on most support masks.
- Mask sRGB/type ambiguity on `soft_plume_noise_-_kakoy_to_seryy_nu_norm.png`.
- Foam/contact overdraw or extra samples with no route readability value.
- Contact art hiding weak water/terrain instead of improving material transition.

GlobalQualityWeight consequences:

- Low/compact near 0.0: one compressed RGBA contact mask, lower foam contribution intensity, material-resident contact retained at route edges, no bloom dependency.
- Middle around 0.35: 2048 RGBA mask or two packed masks where route proof needs it, better bubble breakup and residue variation.
- High around 0.7: stronger wet edge response, denser shoreline breakup decals, optional detail normal where visible.
- Ultra near 1.0: richer local foam/contact layering and caustic receiver variation after frame/memory proof; no physical bubble truth ownership.

Proof still required:

- Author a new RGBA mask contact sheet; do not use `foam.png` as final visible art.
- Channel view proof for R/G/B/A semantics.
- Unity import report for linear mask, compression, mipmaps, streaming mips.
- Crest material-slot readback with no wrapper/cloned-material path.
- Shoreline screenshots proving foam/contact improves wet transition without hiding weak terrain.
- Frame, overdraw, texture memory, and streaming proof before route placement.

## Pack 4 - Aegir/Cloud Stack

Route role: bright surface sky, Aegir hero view, cloud structure, celestial scale signal, storm/detail masks.

Source paths:

- `Assets/_Project/Art/TEXTURES/clouds0_diff.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/clouds0_diff.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/pLANET/clouds0_diff.png`
- `Assets/_Project/Art/TEXTURES/Aegir_storms.png`
- `Assets/_Project/_PROLOGUE_CONTENT/Textures/Planets/Clouds/GASgIANT/Aegir_storms.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblaka!.png`
- `Assets/_Project/Art/TEXTURES/Sky/oblakajip.png`
- `Assets/_Project/Art/TEXTURES/Sky/bo2.png`
- `Assets/_Project/Art/TEXTURES/Sky/bo3.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod1.png`
- `Assets/_Project/Art/TEXTURES/Sky/clod2.png`
- `Assets/_Project/Art/TEXTURES/Sky/eb2.png`
- `Assets/_Project/Art/TEXTURES/clouds.png`
- `Assets/_Project/Art/Skyboxes/panorama_den.png`
- `Assets/_Project/Art/Skyboxes/panorama_shtorm.png`
- `Assets/_Project/Art/TEXTURES/TX_H8AegirGasGiantBakedDisc_1428.png`

Rejected sources / uses:

- `TX_H8AegirGasGiantBakedDisc_1428.png` as final hero Aegir.
- `Aegir_storms.png` as primary beauty art; it is storm/detail mask source only.
- Skybox panoramas as proof of active sky route without shader-slot readback.
- Any dark/noir grade used to hide weak Aegir, cloud, moon, or surface sky art.

Intended channels:

- `TX_H8_AegirCloudBands_Author_Albedo`: composed gas-giant band/color beauty texture from stronger cloud-band sources, sRGB true.
- `TX_H8_AegirStormMask_Author_RGBA`: R = storm cells, G = band turbulence, B = rim/limb breakup, A = optional cloud opacity/detail blend, linear.
- `TX_H8_SkyCloudMain_Author_AlbedoAlpha`: main sky cloud layer for `Mat_HectonSky` candidate slots, sRGB true with alpha if shader contract requires it.
- Optional `TX_H8_SkyCloudHigh_Author_Mask`: high-cloud density/detail mask, linear.

Compression / import target:

- Aegir/cloud beauty: sRGB true, BC7, mipmaps on, streaming mips on. 2048 default hero target; 4096 source or higher residency only after route memory proof.
- Storm/cloud masks: sRGB false, BC7, mipmaps on, streaming mips on.
- Existing 4096x2048 sources with streaming mips disabled must be corrected during future import work before use in route material.

Material slot target:

- `Mat_HectonSky.mat` candidate slots: `_MainCloudTex`, `_HighCloudTex`, `_MainCloudAtlas`.
- Active Aegir/gas-giant material texture slots after Unity material readback.
- No raw YAML patch of sky materials; Unity owner must read effective shader properties and scene skybox refs.

Visual risks:

- Baked disc softness, toy-like banding, polar/UV smear.
- Cloud source mismatch producing incoherent sky/Aegir material language.
- Large 4096 source residency pressure without streaming mips.
- Surface sky crushed by exposure/fog/post.
- Stale/deleted sky material refs in prior static evidence.

GlobalQualityWeight consequences:

- Low/compact near 0.0: composed Aegir/cloud stack keeps silhouette, banding, and sky brightness with lower mip residency and fewer cloud layers. No dark fallback.
- Middle around 0.35: primary Aegir band map plus storm mask and main sky cloud layer, stable streaming mips.
- High around 0.7: richer cloud-band detail, stronger storm breakup, longer Aegir/sky mip residency, better surface reflection contribution where proven.
- Ultra near 1.0: optional 4096 hero Aegir/cloud residency after memory proof; spend surplus on atmospheric limb detail and cloud depth, not more unbounded Texture2D bindings.

Proof still required:

- Composed Aegir/cloud contact sheet and source decision record.
- Unity shader-slot readback for `Mat_HectonSky` and active Aegir material.
- Import report for sRGB/type/compression/mips/streaming mips.
- Bright surface/shoreline screenshots proving Aegir, clouds, moons, and sky stay legible.
- Texture memory, reserved memory, and streaming spike proof before scene route use.
- Frame Debugger/RenderGraph proof only if new runtime rendering path is introduced later.

## Key Blocker Table

| Pack | Main blocker | Consequence | Next proof |
|---|---|---|---|
| Wet basalt shoreline | Generated/source maps need clean PBR reauthoring and streaming mip/import proof | Cannot use as route terrain or hero shoreline material | Clean PBR pack, import report, terrain/material readback, bright shoreline capture |
| Shell/sand photic bed | Baked-light, tiling, mip mush, and material binding risk | Cannot use as photic route substrate | Clean albedo/normal/MRAO, channel proof, terrain material readback, photic screenshot |
| Foam/contact RGBA masks | `foam.png` is rejected visible art; support masks lack route ownership/import proof | Active visible shoreline foam replacement remains blocked | New RGBA mask pack, channel proof, Crest slot readback, shoreline capture |
| Aegir/cloud stack | Baked Aegir disc rejected; source stack blocked by shader-slot/readback and streaming proof | Cannot claim hero Aegir/sky route art | Composed stack, `Mat_HectonSky`/Aegir readback, bright surface capture, memory proof |

## Regression Model For Future Owner

- CPU: this manifest changes no runtime code. Future import/material work must prove no avoidable main-thread upload or shader/material churn.
- GC: this manifest creates no gameplay path. Future runtime paths need measured hot-path allocation proof before any route claim.
- Memory/VRAM: texture source pools are large; imported route packs must report texture memory, reserved memory, and streaming behavior before promotion.
- Cadence: future streaming must obey async upload budgets and avoid every-frame setting changes.
- Correctness: `GlobalQualityWeight` may scale resolution, mip residency, detail intensity, decal density, and layer count. It must not change material ownership, save identity, gameplay truth, or route authority.

Final status: `PENDING VERIFICATION`.
