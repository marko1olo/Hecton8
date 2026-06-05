# 2605 Generated Asset Intake / Staging Audit

Status: STATIC AUDIT ONLY / NO UNITY IMPORT / NO RUNTIME PROOF  
Worker: Batch26 2605  
Date: 2026-06-04  
Scope: `Docs/GeneratedAssets/Gemini` generated texture/source intake for shoreline, foam, caustics, and photic shallow material recovery.

No Unity Editor, Play Mode, dotnet build, image generation, browser automation, process kill, asset import, material edit, scene edit, or `Assets/**` write was performed.

## Authority Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `rendering.md`
- `terrain.md`
- `water.md`
- `shaders.md`
- `streaming.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/REND_VFX_Fluid_Aesthetics_Compute_Particles.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `Docs/Reports/Batch25/BATCH25_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Docs/Reports/Batch20/2017_GEMINI_TEXTURE_PIPELINE_ADVERSARIAL_REVIEW.md`
- `Docs/Reports/Batch22/2203_TEXTURE_INTAKE_CHECKLIST.md`
- `Docs/Reports/Batch22/2203_PHOTIC_TEXTURE_PROMPT_PACK.md`
- `Docs/Reports/Batch23/2306_PHOTIC_TEXTURE_BINDING_PLAN.md`
- `Docs/Reports/Batch23/2306_TEXTURE_CANDIDATE_MATRIX.csv`
- `Docs/Reports/Batch24/2402_UNDERWATER_MATERIAL_RECEIVER_AUDIT.md`
- `Docs/Reports/Batch25/2503_UNDERWATER_VISUALS_OWNER_PHASE_AUDIT.md`
- `Docs/Reports/Batch25/2504_CREST_OCEAN_CLIP_FOAM_CAUSTIC_RISK_AUDIT.md`
- `Docs/GeneratedAssets/Gemini/**` docs, manifests, prompts, QA, audit outputs, and source image inventory.

`Docs/Actual Domains of Project.txt` was not present. Narrow inferred domain: generated photic/surface material source intake.

## Verdict

No generated Gemini image currently in the tree is ready for Unity import, production PBR derivation, material binding, TerrainLayer replacement, Crest foam binding, or caustic receiver promotion.

Current asset state:

- `READY_FOR_UNITY_IMPORT`: none.
- `READY_FOR_DERIVATION`: none.
- `CANDIDATE_REVIEW`: none.
- `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`: all inspected source images.
- `MISSING_SOURCE`: foam/salt contact masks, caustic mask/lookup sources, shallow algae/biofilm source, accepted wet basalt source, accepted photic shell/sand source.

Route relevance: this removes intake ambiguity for the first-20-minutes surface exit, shoreline close foam/wet contact, underwater 0-5 m, and underwater 20-50 m visual proof blockers. It does not prove visual quality. Batch25 remains reject-only until a clean route and new visual packet exist.

## Current Staging Map

| Path | Contents | Static status | Action |
|---|---|---|---|
| `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md` | Legacy queue naming `Outputs/Batch22/`, next prompts, stop rules | `STATIC_DOC` | Use rules, but update output folder for Batch26 unless controller explicitly continues Batch22. |
| `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1428_MANIFEST.md` | Manifest for existing 1428 wet basalt source already under `Assets/**` | Honest reject/source-only manifest | Do not bind. |
| `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_1429_MANIFEST.md` | Manifest for raw 1429 and local refinement attempts | Honest reject, but no exact source sidecar | Keep as family manifest. |
| `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png` | Raw Gemini wet basalt source, 1024 square | `REJECT` | Reference only for correction prompt. |
| `Docs/GeneratedAssets/Gemini/Refined/` | Three local wet-basalt periodic/refined variants | `REJECT`; missing sidecar manifests | Diagnostic evidence only. |
| `Docs/GeneratedAssets/Gemini/Outputs/Batch21/` | Two downloaded photic substrate PNGs plus exact sidecar manifests | `REJECT` | Reference only. |
| `Docs/GeneratedAssets/Gemini/Audit/Batch21/` | Batch21 audit CSV/MD, 2x2 previews, contact sheet | Evidence only | Keep. |
| `Docs/GeneratedAssets/Gemini/QA/**` | Wet basalt QA outputs and previews | Evidence only | Keep. |
| `Docs/GeneratedAssets/Gemini/Prompts/**` | Batch19/20/21 prompt packs | `READY_FOR_GENERATION_REFERENCE` | Use updated Batch26 naming and stop rules. |
| `Docs/GeneratedAssets/Gemini/Batch19/1905/` | Old prompt pack | Source-only reference | Do not treat as current queue. |
| `Docs/GeneratedAssets/Gemini/WetBasaltShoreline_1428_DerivedCandidates/` | Empty folder | No candidate | Leave empty. |
| `Docs/GeneratedAssets/Gemini/QA/1905/` | Empty folder | No evidence | Leave empty. |
| `Docs/GeneratedAssets/Gemini/Outputs/Batch22/` | Missing | No candidate | Create only during a future generation task if controller wants Batch22 continuity. |
| `Docs/GeneratedAssets/Gemini/Outputs/Batch23/` | Missing | No candidate | Do not backfill. |

The entire `Docs/GeneratedAssets/Gemini/` tree is currently untracked in git: `?? Docs/GeneratedAssets/Gemini/`.

## Source Inventory

| Source | Size | SHA-256 | Status | Blocker |
|---|---:|---|---|---|
| `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png` | 1024x1024 | `60E1BDFE3D69F076B41FA378EC6F84457210134E60FA127A0F44CF15ABFB72A7` | `REJECT` | LR/TB seam `30.611/34.508`, band `37.609/40.462`, large repeated rock forms. |
| `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png` | 1024x1024 | `B1A1F5CD124A008C866B3C19269667E8EC638E86DB941697C59F65B25E73DADD` | `REJECT` | Edge-pinned false seam; band mismatch `57.797/62.653`, white clipping `12.54%`, channel saturation `14.22%`. |
| `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png` | 1024x1024 | `B1A1F5CD124A008C866B3C19269667E8EC638E86DB941697C59F65B25E73DADD` | `REJECT / DUPLICATE` | Byte-identical to `1429_periodic.png`; no separate candidate value. |
| `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png` | 1024x1024 | `42468BA3ECB7324EE65AE2A00F88FEADA72F912DC028C9381A9224D03DBF4FDC` | `REJECT` | Worse seam/band mismatch `68.255/84.430`, black clipping `16.73%`, white clipping `3.50%`, saturation `21.90%`. |
| `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png` | 1024x1024 | `ED65B4399BD6E868CF7E587D80411523E81C837AA6B8B39114FA569B8E92A5C2` | `REJECT` | Top-bottom band mismatch, diagonal dune/ripple repetition. |
| `Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png` | 1024x1024 | `5BC241A044CBF1817458AF01B7893A9BF62D50D02D3D31AE0B1B571A28851462` | `REJECT` | Top-bottom band mismatch, repeated shell/stone clusters, possible baked/crushed range. |

## Unity-Side Comparison Risks

This assignment did not alter `Assets/**`. Existing relevant assets were read as static context only.

| Path | Git state | Size | SHA-256 | Intake decision |
|---|---|---:|---|---|
| `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png` | untracked with untracked `.meta` | 1024x1024 | `7A161FBE3C129A25FB59C5FF246A65C9BD5F0DD78C1BCFCF8FC3256E940B49CF` | Already under `Assets/**` from prior work, but QA verdict is `REJECT`. Do not bind or promote. |
| `Assets/_Project/Art/TEXTURES/foam.png` | existing asset | 2048x2048 | `0DDF6821B2088D39F885E0A9D769AF88D49130F1A0E7FA8D4479D8D0E695A1D1` | Not a Gemini candidate. Needs separate owner proof before use as shoreline solution. |

No exact hash match was found between selected production-side image assets and the current Gemini source images.

## Duplicate Findings

- Exact duplicate source files:
  - `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png`
  - `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png`
  - Shared SHA-256: `B1A1F5CD124A008C866B3C19269667E8EC638E86DB941697C59F65B25E73DADD`
- Exact duplicate QA outputs:
  - `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429Periodic/GeminiTextureIntake_contact_sheet.png`
  - `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicDarkPreserve/GeminiTextureIntake_contact_sheet.png`
  - Shared SHA-256: `7EA2AE36E6AE9180AB913390C837F5025C28A934373FBDE4A51DD7FE168A68D0`
  - `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429Periodic/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_tile2x2.png`
  - `Docs/GeneratedAssets/Gemini/QA/WetBasalt1429PeriodicDarkPreserve/tile_previews/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve_tile2x2.png`
  - Shared SHA-256: `68E943CF224721D607557E6D2E8C05447356EC53B8E4D7BEF7D1C5BB82070E7D`

Decision: collapse `periodic_darkpreserve` into `periodic` in future reports. It is not a distinct candidate.

## Missing Manifests And Evidence Gaps

- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic.png`: no exact sidecar manifest.
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_darkpreserve.png`: no exact sidecar manifest; duplicate.
- `Docs/GeneratedAssets/Gemini/Refined/TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean.png`: no exact sidecar manifest.
- `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png`: no exact sidecar manifest, but covered by nearby family manifest `TX_H8_WetBasaltShoreline_1429_MANIFEST.md`.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png`: manifest exists in `Docs/GeneratedAssets/Gemini/`, not beside the imported asset. It is untracked and rejected.
- No source exists for `ShoreFoamSaltContact_RGBAMaskSource`.
- No source exists for `CausticDecal_GrayscaleMaskSource`.
- No source exists for `CausticLookup_RGBAMaskSource`.
- No source exists for `ShallowAlgaeBiofilm_TintSource`.
- No accepted normal, height, MRAO, wetness, foam, or caustic mask exists for these targets.
- No import settings report exists for any accepted candidate because no accepted candidate exists.
- No URP preview, Frame Debugger, RenderGraph, profiler, or visual route screenshot proof exists from this task.

## Staging Rules

1. Do not place new Gemini downloads in `Assets/**`.
2. For Batch26, prefer `Docs/GeneratedAssets/Gemini/Outputs/Batch26/2605/` unless the controller explicitly orders continuity with the older `Outputs/Batch22/` queue.
3. Every downloaded source needs an exact sidecar manifest beside it:
   - prompt ID and full prompt text;
   - target taxonomy;
   - source role;
   - intended meters per tile;
   - SHA-256;
   - audit command;
   - audit CSV/Markdown path;
   - 2x2 and 3x3 preview paths;
   - status label;
   - explicit note: Unity import blocked until audit and visual tile review pass.
4. Run static intake before any derivation:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch26/2605/[FILE].png --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch26/2605_[TARGET]
```

5. Add or inspect a 3x3 preview manually before calling anything `READY_FOR_DERIVATION`; current tool writes 2x2 only.
6. Do not derive normal, roughness, AO, MRAO, wetness, foam, or caustic channels from a `REJECT` albedo/source.
7. Do not use `TextureSeamPeriodicRefiner.py --edge-pin` output as seam proof. Edge pinning can force exact seam metrics to `0.000` while inner bands fail harder.
8. Do not replace active TerrainLayers, Crest materials, ocean materials, or route materials from this intake lane. Unity owner slot only.
9. Do not use darkness, haze, or green/blue grading to hide repetition, weak terrain, or missing waterline assets.
10. Keep rejected sources. Do not delete them without a scoped cleanup task and paired `.meta` deletion when applicable.

## Promotion Gates

`SOURCE_ONLY`: file exists in Docs, manifest missing or audit not run. No Unity use.

`STATIC_REJECTED`: audit or human tile review found seam, band, clipping, baked-light, perspective, text/logo/border, or repeated hero motif. No derivation. No Unity use.

`CANDIDATE_REVIEW`: script has no hard issue but human 2x2/3x3 review or channel plan is incomplete. No Unity use.

`READY_FOR_DERIVATION`: script passes, human 2x2/3x3 review passes, prompt/source role is recorded, meters-per-tile exists, channel derivation route is explicit. Still no Unity import.

`READY_FOR_UNITY_IMPORT`: requires accepted source, derived channel stack, import settings plan, PBR role map, rollback plan, and explicit Unity-owner slot. None currently qualify.

## Required Material Families Before Unity Binding

Wet basalt shoreline:

- Albedo source, no baked lighting or hero crack/vein repetition.
- Height source for pores, fracture lips, salt deposits.
- Normal derived from accepted height/source, Unity/OpenGL tangent route, BC5 target.
- MRAO: R metallic black; G roughness or smoothness per shader contract; B cavity AO; A wetness/salt/mineral/family mask if shader owner accepts it.
- Optional sparse cyan vein mask as accent only.

Sand/shell substrate:

- Bright albedo source, no diagonal dune bands or repeated shell clusters.
- Height source for shell, calcite, basalt chips, silt pockets.
- Normal from accepted height.
- MRAO: R black, G roughness, B cavity AO, A shell/calcite/algae/wetness mask.

Shore foam/salt contact:

- RGBA mask source only, not a scenic wave render.
- R long foam/contact strength.
- G cross-flow wet-edge breakup.
- B micro-bubbles/lace/sediment interruption.
- A confidence/wetness mask.

Caustic masks:

- Grayscale decal first, controlled contrast, no terrain/fish/diver/scene render.
- Optional RGBA lookup only after grayscale passes:
  - R primary lace;
  - G secondary offset lace;
  - B suspended sparkle gaps;
  - A projection confidence.
- Use only where light reason exists: bright shallows, lamps, glass, pools, local photic projection.

Algae/biofilm breakup:

- Tint/mask source for restrained teal-green/cyan film, calcite dust, organic specks, sediment breaks.
- No neon slime, candy reef wash, or random glowing dots.

## Exact Next Generation Prompts

Use English. Save downloads only under `Docs/GeneratedAssets/Gemini/Outputs/Batch26/2605/` unless the controller explicitly orders another folder. Stop after two repeated hard failures for the same target unless the correction prompt names that exact failure.

### Prompt 01 - Wet Basalt Shoreline Albedo

```text
Create ONE seamless square tileable PBR albedo texture source for a premium Unity URP ocean survival game.

Subject: bright photic-zone wet basalt shoreline rock on an alien ocean moon, black-gray volcanic basalt, salt-water erosion, chipped fracture planes, small pores, stratified cracks, pale salt residue in crevices, tiny sediment caught in cracks, and very subtle teal mineral staining.

This is base color only for later PBR derivation. Do not include normal-map colors, roughness data, AO dirt, emission, baked shadows, baked highlights, or a beauty-render lighting pass.

Use orthographic top-down material view. Use even neutral bright photic daylight suitable for albedo. No perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no border, no watermark.

Make the pattern reusable in a 3x3 tile preview: no large hero cracks, no repeated rock plates, no giant teal vein, no diagonal seam bands. The surface must stay readable and beautiful for shoreline and 0-100 m shallow water, not dark noir.

Output ONE square 4096x4096 texture only.
```

### Prompt 02 - Sand/Shell Substrate Albedo Source

```text
Create ONE seamless square tileable PBR albedo texture source for a bright photic shallow seabed substrate.

Subject: pale gray volcanic sand, small black basalt chips, shell fragments, calcite grains, tiny reef limestone pieces, soft silt pockets, small algae specks, and subtle teal mineral traces. Shell fragments should mostly be 1-7 cm scale, with no large hero shell or repeated stone cluster.

This is base color only for later height, normal, roughness, and AO derivation. Do not bake shadows or directional highlights into the albedo. Do not create a beach photograph or perspective scene.

Use orthographic top-down material view with even bright diffuse daylight. No perspective, no horizon, no object silhouette, no labels, no text, no logo, no UI, no border.

The material must stay beautiful and readable through clear shallow water. Avoid beige mud, brown beach sand, dark abyss sediment, diagonal dune bands, and obvious 2x2 repetition.

Output ONE square 4096x4096 texture only.
```

### Prompt 03 - Shore Foam/Salt Contact RGBA Mask

```text
Create ONE seamless square tileable RGBA shoreline foam and salt contact mask source for wet basalt in a bright alien ocean shoreline.

Subject: thin white sea-foam lace, translucent micro-bubbles, tide-sheared strands, broken foam cells, wet edge breakup, salt residue, pale sediment contact marks, and natural foam interruption by rough basalt cracks.

This is source data for a Unity URP material, not a scenic wave photo and not a water simulation. No baked lighting, no cast shadows, no directional highlights.

Use orthographic top-down mask/source view. No perspective, no horizon, no object silhouette, no text, no logo, no UI, no border.

Channel intent: Red = long foam strand and contact foam strength. Green = cross-flow wet edge breakup. Blue = small bubbles, lace, and sediment interruption. Alpha = clean confidence/wetness mask.

Foam must be sparse and broken enough to blend. No solid opaque white strip, no repeated bubble stamps, no storm mud. Output ONE square 4096x4096 texture only.
```

### Prompt 04 - Caustic Decal Mask Source

```text
Create ONE seamless square tileable grayscale caustic decal mask source for bright shallow underwater projection in a premium Unity URP ocean survival game.

Subject: clean photic daylight caustic lace, refracted curved streaks, broken nonuniform bands, subtle small silt sparkle gaps, and soft water-column micro variation.

This is a mask/source panel for shader projection and decal derivation. It is not a scenic underwater render. No terrain, no fish, no diver, no objects, no bloom glare, no baked shadows, no perspective, no horizon, no text, no logo, no UI, no border.

Use orthographic top-down source-panel view. Edges must tile invisibly in 2x2 and 3x3 previews. Keep the contrast controlled: readable caustic structure, not crushed white lines or black void.

Output ONE square 2048x2048 grayscale texture only.
```

### Prompt 05 - Caustic RGBA Lookup Source

```text
Create ONE seamless square tileable RGBA lookup source for fake-first shallow-water caustics and suspended particle breakup.

Subject: layered caustic lace, soft refracted streaks, tiny suspended bright specks, broken water-column bands, and low-frequency mask variation suitable for shader animation.

This is source data, not a beauty render. No perspective, no horizon, no fish, no diver, no terrain, no labels, no text, no logo, no UI, no border, no baked lighting, no bloom.

Channel intent: Red = primary caustic lace, Green = secondary offset lace, Blue = suspended particle sparkle gaps, Alpha = soft projection confidence mask.

Edges must tile cleanly. Avoid repeated bright knots, grid patterns, harsh stripes, and generic noise.

Output ONE square 2048x2048 texture only.
```

### Prompt 06 - Shallow Algae/Biofilm Tint Breakup

```text
Create ONE seamless square tileable albedo/tint source for shallow photic algae and biofilm color breakup on wet basalt and shell-sand substrate.

Subject: thin teal-green algae film, muted cyan biofilm stains, pale calcite dust, small organic speckles, soft edge breakup, and sediment interruption. The pattern should be natural and patchy, usable as an overlay mask/tint source.

This is a source texture for later mask extraction. No baked light, no cast shadows, no directional highlights, no normal-map colors, no full coral object render.

Use orthographic top-down material view with even bright diffuse daylight. No perspective, no horizon, no labels, no text, no logo, no UI, no border.

Keep the color restrained and believable. No neon slime, no candy reef gradient, no random glowing dots, no muddy dark abyss staining, no obvious 2x2 repetition.

Output ONE square 4096x4096 texture only.
```

### Correction Prompt - Seam And Repetition Fix

Use only for a near-miss candidate where the failure is seam/band/repetition, not perspective/text/baked-light garbage.

```text
Revise the attached/generated texture into a TRUE production seamless square tile.

Keep the same material identity and scale, but fix tileability: left/right and top/bottom edges must match invisibly in a 2x2 and 3x3 tiled preview.

Remove large recognizable repeated hero shapes, diagonal bands, repeated shell/stone clusters, repeated veins, repeated cracks, repeated foam stamps, and any obvious border treatment. Make the pattern more isotropic and stochastic while preserving believable material structure.

Use even diffuse lighting suitable for source texture work. No baked shadows, no directional highlights, no perspective, no horizon, no labels, no text, no logo, no UI, no border.

Output ONE square texture only.
```

### Correction Prompt - Albedo Cleanup

Use only after a tile is close but contains baked lighting or clipped albedo.

```text
Revise the attached/generated texture as a clean PBR albedo source.

Keep the material identity, but remove baked shadows, black crushed crevices, white clipped highlights, strong lighting gradients, glossy render shine, and camera/photo artifacts. Preserve base color variation and material readability under neutral URP lighting.

Do not add normal-map colors, AO dirt, roughness data, emission, text, labels, logos, UI, perspective, horizon, or object silhouettes.

Edges must remain seamless and tileable. No repeated hero shapes in 2x2 or 3x3 preview.

Output ONE square albedo texture only.
```

## Generation Priority

1. Wet basalt shoreline albedo: 3 attempts maximum before stop unless a candidate reaches `CANDIDATE_REVIEW` or better.
2. Sand/shell substrate albedo: 3 attempts maximum, one correction only if the failure is narrow.
3. Shore foam/salt contact RGBA mask: 2 attempts.
4. Caustic decal grayscale: 2 attempts.
5. Caustic RGBA lookup: 2 attempts only after grayscale target is not garbage.
6. Algae/biofilm tint: 2 attempts after basalt/sand visual scale is stable.
7. Height/roughness/normal/MRAO follow-ups: only after the related source is `READY_FOR_DERIVATION`.

## Low / Middle / High / Ultra Consequences

- Low / compact: keep accepted world materials at 512-1024 imported size where possible, foam/caustic masks at 256-512, mips on, compression on, streaming mips on. Material identity must stay readable; no muddy/noir fallback.
- Middle: 1024-2048 key photic materials, stronger local roughness/wetness masks, foam/salt decals around 512-1024 if budget allows.
- High: 2048 hero route surfaces, richer normals, stronger wetness and foam layering, longer residency only after profiler proof.
- Ultra: 4096 source/bake archives and hero-only runtime import where proof allows; richer caustic/foam/decal density without changing material truth, terrain truth, water truth, shader channel contract, gameplay route, or save identity.

## Final Blockers

1. Existing Gemini wet basalt, refined wet basalt, Batch21 photic seabed, and Batch21 shell/sand images are all rejected.
2. Foam/salt and caustic source masks are missing, so current material receiver work is forced to use procedural/sine/overdrive routes instead of authored mask sources.
3. The imported untracked 1428 wet-basalt PNG under `Assets/**` is rejected by static QA and must not become an active TerrainLayer/material source.
4. `TextureSeamPeriodicRefiner.py` edge pinning is diagnostic only. It cannot prove visual tileability.
5. Any visual acceptance claim remains blocked until a Unity owner produces clean import/material proof, current route screenshots, log tail, and profiler/VRAM evidence where runtime behavior is touched.

## Static Proof Boundary

This report proves file inventory, hashes, manifest gaps, duplicate status, and static QA verdicts only. It does not prove Unity import health, material response, Crest sampling, RenderGraph state, Frame Debugger state, profiler cost, GC behavior, or in-game visual acceptance.
