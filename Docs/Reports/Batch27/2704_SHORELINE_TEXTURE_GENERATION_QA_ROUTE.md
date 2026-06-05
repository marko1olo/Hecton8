# Batch27 2704 Shoreline Texture Generation QA Route

Date: 2026-06-04
Worker: Batch27 Worker 2704
Mode: report-only static route definition. No Unity, no Play Mode, no dotnet build, no browser/Gemini generation, no image import, no asset import.
Evidence class: STATIC_DOC / STATIC_SOURCE. Runtime, Unity import, visual acceptance, profiler, Frame Debugger, RenderGraph, VRAM, and GC proof remain PENDING VERIFICATION.

## Verdict

No current Gemini shoreline source is ready for derivation, Unity import, production material binding, TerrainLayer replacement, Crest foam binding, or route proof.

The Batch27 recovery route is not "generate more images and drop them into Assets." It is:

1. Generate source candidates only under `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/`.
2. Write exact sidecar manifests beside every downloaded source.
3. Run static intake QA and manual 2x2/3x3 tile review before any derivation.
4. Derive normal/MRAO/wetness/foam/caustic channels only from `READY_FOR_DERIVATION` sources.
5. Promote a complete PBR/mask stack to `READY_FOR_UNITY_IMPORT` only after channel QA, import settings plan, material family naming, rollback plan, and Unity-owner slot exist.
6. Keep all non-accepted sources outside `Assets/**`.

## Authority Read

- `AGENTS.md`
- `TASTE.md`
- `VISION_LOCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `terrain.md`
- `water.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/STRM_Async_Asset_Upload_Texture_Settings.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/REND_Terrain_VirtualTexturing.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `Docs/Reports/Batch26/2603_SHORELINE_TERRAIN_ART_ROUTE_AUDIT.md`
- `Docs/Reports/Batch26/2605_GENERATED_ASSET_INTAKE_STAGING_AUDIT.md`
- `Docs/GeneratedAssets/Gemini/README_GENERATION_QUEUE_20260604.md`
- `Tools/GeminiTextureIntakeAudit.py`
- relevant prompt packs under `Docs/GeneratedAssets/Gemini/Prompts/**`

`Docs/Actual Domains of Project.txt` was checked and was not present. Narrow inferred domain: generated photic shoreline texture source intake and QA route.

## Current Blockers

| Blocker | Evidence class | Consequence |
|---|---|---|
| Active photic terrain still depends on rejected wet basalt lineage from `TX_H8_WetBasaltShoreline_Albedo_1428`. | STATIC_DOC from Batch26 2603/2605 | Broad shoreline terrain cannot be accepted until this source is replaced by a complete accepted material family. |
| Existing wet basalt 1428, 1429, periodic variants, Batch21 photic seabed, and Batch21 shell/sand sources are all rejected. | STATIC_IMAGE_QA from existing reports | No current source may feed normal, AO, roughness, MRAO, wetness, foam, or caustic derivation. |
| Foam/salt contact, caustic decal, caustic lookup, and algae/biofilm accepted sources are missing. | STATIC_SOURCE inventory | Current foam/caustic route is forced toward generic/procedural masks until new accepted source candidates exist. |
| `Tools/GeminiTextureIntakeAudit.py` writes 2x2 previews only. | STATIC_SOURCE | 3x3 preview remains a required manual or future-command artifact before `READY_FOR_DERIVATION`. |
| No Unity import settings report, material binding proof, route screenshot packet, log tail, profiler, or VRAM proof exists from this task. | STATIC_DOC | All runtime/import claims remain PENDING VERIFICATION. |

## Final Batch27 Folder Contract

Do not reuse Batch21/Batch22 output folders for new work. They are historical source lanes and make controller review ambiguous.

Generated source root:

```text
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/
```

Final source folders:

```text
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/WetBasaltShoreline/
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/ShellSandSubstrate/
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/ShoreFoamSaltContact/
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/Caustics/DecalMask/
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/Caustics/RgbaLookup/
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/AlgaeBiofilm/
Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/DerivedStacks/
```

QA/audit root:

```text
Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/
```

Per-candidate QA folder:

```text
Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/[PROMPT_ID]_[YYYYMMDD_HHMMSS]/
```

Preview outputs:

```text
Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/[PROMPT_ID]_[YYYYMMDD_HHMMSS]/tile_previews/
Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/[PROMPT_ID]_[YYYYMMDD_HHMMSS]/manual_review/
```

No final source, manifest, QA, or preview folder above is permission to write `Assets/**`.

## Naming Contract

Source filename:

```text
TX_B27_2704_[Target]_[Role]_[YYYYMMDD]_Gemini_[HHMMSS].png
```

Manifest filename:

```text
TX_B27_2704_[Target]_[Role]_[YYYYMMDD]_Gemini_[HHMMSS]_MANIFEST.md
```

Derived stack filename:

```text
TX_B27_2704_[Target]_[Role]_DERIVED_[YYYYMMDD]_[HHMMSS].png
```

Never overwrite a prior source candidate. A retry gets a new timestamp and a new manifest.

## Generation Queue

Spend order is fixed until a route owner changes it in a new report.

| Order | Final prompt ID | Lineage | Target role | Attempts | Stop condition |
|---:|---|---|---|---:|---|
| 1 | `TX_B27_2704_WetBasaltShoreline_AlbedoSource` | 2605 Prompt 01, B20-WB-001, `TX_B21_WetBasaltShoreline_Albedo_20260604`, 1907-P03 | Seamless bright photic wet basalt albedo source | 3 max | Stop on perspective/object render/text/border/baked light immediately. Stop after 2 same seam/repetition failures unless correction prompt names exact failure. |
| 2 | `TX_B27_2704_ShellSandSubstrate_AlbedoSource` | 2605 Prompt 02, Batch21 2102, 1907-P08, 1908_SAND_SEDIMENT_OVERLAY | Seamless shell/sand/calcite shallow substrate albedo source | 3 max | Stop on diagonal dune bands, repeated shell cluster, beige mud, dark abyss sediment, baked lighting, or perspective scene. |
| 3 | `TX_B27_2704_ShoreFoamSaltContact_RGBAMaskSource` | 2605 Prompt 03, B20-WB-007, `TX_B21_ShoreFoamSaltContact_Mask_20260604`, 1907-P01/P02 | RGBA foam/salt/wet-contact mask source | 2 max | Stop on solid opaque white strip, repeated bubble stamps, scenic wave render, muddy black contact band, or non-separable channels. |
| 4 | `TX_B27_2704_CausticDecal_GrayscaleMaskSource` | 2605 Prompt 04, 1907-P10 | Grayscale caustic decal mask source | 2 max | Stop on terrain/fish/diver/scene content, bloom glare, crushed contrast, black void, or non-tileable lace. |
| 5 | `TX_B27_2704_CausticLookup_RGBAMaskSource` | 2605 Prompt 05, Batch21 2022 Prompt 7 | Optional RGBA caustic lookup source | 2 max, only after order 4 is not garbage | Stop on repeated bright knots, grid/noise pattern, non-separable RGBA channels, or scenic underwater render. |
| 6 | `TX_B27_2704_ShallowAlgaeBiofilm_TintMaskSource` | 2605 Prompt 06, 1908_ALGAE_FILM | Restrained algae/biofilm tint/mask source | 2 max | Stop on neon slime, candy reef wash, dark abyss staining, random glowing dots, or repeated overlay patches. |

Follow-up derivation prompts or offline derivation may start only after the matching source is `READY_FOR_DERIVATION`:

- `TX_B27_2704_WetBasaltShoreline_HeightSource`
- `TX_B27_2704_WetBasaltShoreline_Normal`
- `TX_B27_2704_WetBasaltShoreline_MRAO`
- `TX_B27_2704_WetBasaltShoreline_WetnessSaltMask`
- `TX_B27_2704_ShellSandSubstrate_HeightSource`
- `TX_B27_2704_ShellSandSubstrate_Normal`
- `TX_B27_2704_ShellSandSubstrate_MRAO`
- `TX_B27_2704_ShellSandSubstrate_ShellCalciteAlgaeMask`

## Prompt Body Rules

The final prompt text should use the 2605 exact prompts as the base because they already corrected the old path ambiguity and hard stop rules. Append these universal negative clauses to every prompt unless stricter text is already present:

```text
Negative prompt: visible seams, mismatched edges, baked highlights, baked shadows, directional cast shadow, lighting gradient, perspective, horizon, scene camera, object render, labels, readable text, numbers, logo, UI, watermark, frame, copied game art, crayon marks, procedural scribbles, low-resolution bands, JPEG mush, blurry detail, flat plastic sheen, repeated obvious tiling, giant hero shapes, black noir darkness used to hide weak detail, muddy grade, cartoon, painterly, low-poly, generic noise, smooth blobs, random neon, opaque cover-up.
```

Correction prompt for near-miss seam/repetition failures:

```text
Revise the attached/generated texture into a TRUE production seamless square tile.

Keep the same material identity and scale, but fix tileability: left/right and top/bottom edges must match invisibly in a 2x2 and 3x3 tiled preview.

Remove large recognizable repeated hero shapes, diagonal bands, repeated shell/stone clusters, repeated veins, repeated cracks, repeated foam stamps, and any obvious border treatment. Make the pattern more isotropic and stochastic while preserving believable material structure.

Use even diffuse lighting suitable for source texture work. No baked shadows, no directional highlights, no perspective, no horizon, no labels, no text, no logo, no UI, no border.

Output ONE square texture only.
```

Correction prompt for near-miss albedo cleanup:

```text
Revise the attached/generated texture as a clean PBR albedo source.

Keep the material identity, but remove baked shadows, black crushed crevices, white clipped highlights, strong lighting gradients, glossy render shine, and camera/photo artifacts. Preserve base color variation and material readability under neutral URP lighting.

Do not add normal-map colors, AO dirt, roughness data, emission, text, labels, logos, UI, perspective, horizon, or object silhouettes.

Edges must remain seamless and tileable. No repeated hero shapes in 2x2 or 3x3 preview.

Output ONE square albedo texture only.
```

## Sidecar Manifest Template

Every downloaded source requires this exact sidecar shape before QA.

```markdown
# [FILENAME WITHOUT EXTENSION] Manifest

Evidence class: STATIC_SOURCE
Status label: SOURCE_ONLY
Generated source path: `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/[Target]/[filename].png`
Manifest path: `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/[Target]/[filename]_MANIFEST.md`

## Identity

- Batch: Batch27
- Worker/route: 2704 shoreline texture generation QA route
- Prompt ID: `TX_B27_2704_[Target]_[Role]`
- Prompt lineage: [2605 Prompt ## / B20-WB-### / Batch21 ID / 1907 or 1908 ID]
- Target family: [WetBasaltShoreline | ShellSandSubstrate | ShoreFoamSaltContact | CausticDecal | CausticLookup | AlgaeBiofilm]
- Source role: [AlbedoSource | HeightSource | RGBAMaskSource | GrayscaleMaskSource | TintMaskSource]
- Intended material scale: [meters per tile]
- Intended color space: [sRGB albedo/tint | linear mask/normal/MRAO]
- Intended tileability: seamless square tile
- Runtime use: blocked until `READY_FOR_UNITY_IMPORT`

## Prompt

```text
[full prompt text]
```

## Negative Prompt

```text
[full negative prompt text]
```

## Source File

- Width/height:
- Format:
- SHA-256:
- Local timestamp:
- UTC timestamp:
- Download/operator note: no account names or emails
- AI/tool source: Gemini/browser source candidate only

## Channel Contract

- Albedo: base color only, no baked lighting
- Normal: not present unless this is a normal candidate
- MRAO: not present unless this is a packed candidate
- RGBA mask intent:
  - R:
  - G:
  - B:
  - A:
- Shader contract dependency: PENDING UNITY OWNER

## QA

- Static audit command:
- Static audit Markdown:
- Static audit CSV:
- 2x2 preview:
- 3x3 preview:
- Manual review result: PENDING
- Manual reviewer:
- Status label after QA: SOURCE_ONLY

## Gates

- Unity import: BLOCKED
- Derivation: BLOCKED
- `Assets/**` write: BLOCKED
- Rejected-source reuse: reference only
```

## QA Commands

Run commands from `C:\hades\Hecton8`. These commands are for the future generation owner; Worker 2704 did not run them because they write QA artifacts outside this report.

Static intake and 2x2 preview:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root "Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/[Target]/[SOURCE_FILE].png" --out-dir "Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/[PROMPT_ID]_[YYYYMMDD_HHMMSS]" --max-tile-preview 1024 --max-contact-thumb 512
```

Manual 3x3 preview generation:

```powershell
python -c "from PIL import Image; from pathlib import Path; import sys; p=Path(sys.argv[1]); out=Path(sys.argv[2]); im=Image.open(p).convert('RGB'); tile=Image.new('RGB',(im.width*3, im.height*3)); [tile.paste(im,(x*im.width,y*im.height)) for y in range(3) for x in range(3)]; out.parent.mkdir(parents=True, exist_ok=True); tile.save(out)" "Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/[Target]/[SOURCE_FILE].png" "Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/[PROMPT_ID]_[YYYYMMDD_HHMMSS]/tile_previews/[SOURCE_FILE_STEM]_tile3x3.png"
```

SHA-256 capture:

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath "Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/[Target]/[SOURCE_FILE].png"
```

Manifest/source pairing check:

```powershell
Test-Path -LiteralPath "Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/[Target]/[SOURCE_FILE_STEM]_MANIFEST.md"
```

Do not run these commands against `Assets/**`. If a source is already in `Assets/**`, the intake route is contaminated and must be reported as `UNITY_MATERIAL_BLOCKED`.

## Preview Requirements

Each candidate needs:

- 1x source view at native or 1024 preview size.
- 2x2 tile preview from `GeminiTextureIntakeAudit.py`.
- 3x3 tile preview from the command above or equivalent manual export.
- Contact sheet when multiple candidates are compared.
- Manual review notes listing visible seams, banding, repeated hero shapes, baked lighting, text/logo/border, perspective/object render, crushed albedo, and material truth errors.

2x2 pass alone is insufficient because previous sources hid repeat motifs until larger preview.

## Pass / Reject Labels

Use only these labels.

| Label | Meaning | Allowed next action |
|---|---|---|
| `SOURCE_ONLY` | File exists in Docs but manifest or QA is incomplete. | Complete manifest and QA. No derivation. No Unity import. |
| `STATIC_REJECTED` | Script or manual review found a hard blocker. | Keep as reference/diagnostic only. No derivation. No Unity import. |
| `CANDIDATE_REVIEW` | Script has no hard reject but warnings/manual review/channel plan remain unresolved. | Manual review or correction prompt. No Unity import. |
| `READY_FOR_DERIVATION` | Script passes, 2x2/3x3 manual review passes, sidecar complete, meters-per-tile and channel route recorded. | Derive normal/MRAO/wetness/foam/caustic channels offline. Still no Unity import. |
| `DERIVED_STACK_REVIEW` | Derived channels exist but channel independence/import plan/preview proof is incomplete. | Run channel QA, pack MRAO, produce previews. No Unity import. |
| `READY_FOR_UNITY_IMPORT` | Accepted source plus derived stack plus import settings plan plus material names plus rollback plan plus Unity-owner slot. | Unity owner may import during controlled window. |
| `UNITY_IMPORTED_PENDING_ROUTE_PROOF` | Unity owner imported assets but route screenshots/log/profiler proof are not complete. | Produce route packet. Do not claim visual acceptance. |
| `ROUTE_ACCEPTED_STATIC_SOURCE_ONLY` | Static source route is clean, but no runtime proof exists. | Report static source status only. |

Hard rejection triggers:

- Perspective scene, object render, horizon, border, frame, text, logo, UI, watermark.
- Baked directional light, cast shadow, specular beauty render, painted caustics inside albedo.
- Severe left/right or top/bottom edge mismatch from the audit tool.
- 2x2 or 3x3 repeated hero cracks, shells, foam stamps, caustic knots, diagonal bands, or obvious tiled patches.
- Crushed black/white albedo, channel saturation, or muddy/noir hiding.
- False material truth: metallic rock, glossy dirt, glowing sand, constant roughness on wet/corroded surfaces, identical AO/roughness without documented reason.

## Manual Visual Checks By Target

Wet basalt:

- Bright photic wet basalt, not black abyss rock.
- Fractures, pores, salt residue, sediment, and mineral staining have real scale.
- No giant hero vein or repeated plate.
- No baked wet highlight. Wetness belongs in masks/roughness, not albedo lighting.

Shell/sand substrate:

- Pale shell/calcite/basalt-chip scale is plausible at 1 to 2 meters per tile.
- No repeated shell cluster, diagonal dune band, beige mud, dark abyss silt, or candy reef gravel.
- Height-like cues support later normal/AO derivation.

Foam/salt contact:

- RGBA regions are visually separable.
- Foam is sparse lace and contact breakup, not opaque paint.
- No scenic wave photo, no storm mud, no repeated bubble stamp.

Caustic decal:

- Grayscale lace has controlled contrast and no scenic content.
- Usable only for bright shallows, lamps, glass, pools, or other justified light sources.
- No global abyss caustic implication.

Caustic RGBA lookup:

- R/G/B/A channels must be separable in intent.
- No grid pattern, random noise, repeated bright knots, or bloom glare.

Algae/biofilm:

- Restrained teal-green/cyan film, calcite dust, sediment interruption.
- No neon slime, random glow dots, or global color wash that turns shoreline into a cartoon reef.

## Derivation Gates

No derived channel may be made from `STATIC_REJECTED`, `SOURCE_ONLY`, or unresolved `CANDIDATE_REVIEW` input.

Wet basalt stack:

- Albedo: accepted source only, sRGB.
- Height: accepted height-like source or offline extraction from accepted albedo plus manual correction. No baked shadows.
- Normal: derived from accepted height/source, tangent-space, linear, target import BC5.
- MRAO: R metallic = 0 for basalt; G roughness unless target shader explicitly uses smoothness; B cavity AO; A wetness/salt/mineral/family mask if shader owner accepts it.
- Wetness/salt/contact mask: separate linear mask if MRAO alpha is not owned by wetness.
- Optional cyan mineral vein mask: sparse accent only, never broad neon.

Shell/sand stack:

- Albedo: accepted source only, sRGB.
- Height/normal: shell, calcite, chips, silt pockets, and small relief must survive mip preview.
- MRAO: R metallic = 0; G roughness; B cavity AO; A shell/calcite/algae/wetness mask.
- No diagonal band, repeated shell cluster, or color-only substrate.

Foam/salt contact:

- Linear RGBA mask only.
- R = long foam/contact strength.
- G = cross-flow wet edge breakup.
- B = micro-bubbles/lace/sediment interruption.
- A = confidence/wetness mask unless shader owner assigns a different documented contract.

Caustics:

- Grayscale decal mask passes before optional RGBA lookup.
- No caustic albedo. No baked caustics inside terrain color.
- Use as projected/material response where light reason exists.

Algae/biofilm:

- Tint/mask overlay only unless a material owner defines a packed slot.
- Must preserve underlying basalt/shell material identity.

Derived preview requirements:

- Albedo-only.
- Normal-only or normal debug.
- MRAO channel split.
- Wetness/foam/caustic/algae mask split.
- 2x2 and 3x3 final tile stack preview.
- Neutral, low-angle, and grazing-light offline render preview before Unity import.

## Unity Import Gate

Unity import is a separate owner slot. This report does not authorize import.

Required before import:

- Status label `READY_FOR_UNITY_IMPORT`.
- Full source manifest and derived stack manifests.
- Import settings plan:
  - Albedo: sRGB true, compressed high quality, mips on, streaming mips on.
  - Normal: texture type NormalMap, sRGB false, BC5 where supported, mips on.
  - MRAO/masks: sRGB false, compressed high quality, mips on.
  - Caustic/foam masks: linear, compressed, mips on unless shader owner proves otherwise.
- Target material/TerrainLayer names.
- Rollback plan with previous GUIDs and exact file list.
- Unity owner import window and route proof plan.

Target texture names after accepted import:

```text
TX_H8_ShorelineWetBasalt_B27_Albedo
TX_H8_ShorelineWetBasalt_B27_Normal
TX_H8_ShorelineWetBasalt_B27_MRAO
TX_H8_ShorelineWetBasalt_B27_WetnessSaltMask
TX_H8_PhoticShellSand_B27_Albedo
TX_H8_PhoticShellSand_B27_Normal
TX_H8_PhoticShellSand_B27_MRAO
TX_H8_ShoreFoamSaltContact_B27_RGBA
TX_H8_ShallowCausticDecal_B27_Grayscale
TX_H8_ShallowCausticLookup_B27_RGBA
TX_H8_ShallowAlgaeBiofilm_B27_TintMask
```

Target material family names:

```text
MAT_H8_ShorelineWetBasalt_B27
MAT_H8_PhoticShellSand_B27
MAT_H8_ShoreFoamSaltContact_B27
MAT_H8_ShallowCausticReceiver_B27
MAT_H8_ShallowAlgaeBiofilmOverlay_B27
```

Target TerrainLayer names, if terrain owner chooses TerrainLayer routing:

```text
TL_H8_ShorelineWetBasalt_B27
TL_H8_PhoticShellSand_B27
```

Do not replace `L_Basalt`, Rock031, Crest ocean materials, or active route material GUIDs in place. Create new B27 assets, bind in an isolated route-owner change, then prove or roll back.

Rollback requirements:

- Record previous material/TerrainLayer GUIDs before binding.
- Keep a file list for every imported `.png`, `.meta`, `.mat`, `.asset`, and TerrainLayer.
- If rejected after import, unbind B27 material family first.
- If cleanup is explicitly assigned later, delete asset and `.meta` pairs atomically.
- Keep rejected Docs sources as evidence unless a scoped cleanup task orders otherwise.

## No-Assets Staging Law

Before `READY_FOR_UNITY_IMPORT`, these are forbidden:

- Saving generated browser/Gemini output under `Assets/**`.
- Importing source candidates into Unity.
- Replacing active TerrainLayers or shared route materials.
- Binding rejected albedos as broad shoreline terrain.
- Deriving normal/MRAO/wetness/foam/caustic maps from rejected sources.
- Using darkness, haze, green/blue grading, sine caustic overdrive, or foam ribbons to hide weak terrain texture.

Allowed before import:

- Save source candidates under `Docs/GeneratedAssets/Gemini/Outputs/Batch27/2704/`.
- Write sidecar manifests.
- Run static intake QA into `Docs/GeneratedAssets/Gemini/Audit/Batch27/2704/`.
- Generate 2x2/3x3 previews under the audit folder.
- Mark candidates with the labels in this report.
- Use rejected candidates as reference-only correction prompts.

## Route Owner Replacement Plan

After accepted sources and derived stacks exist:

1. Create new B27 wet basalt and shell/sand material/TerrainLayer families. Do not mutate broad legacy basalt in place.
2. Bind wet basalt and shell/sand through a controlled route material or terrain blend. The shoreline must not become one repeated basalt tile.
3. Use accepted wetness/salt/foam masks to break the waterline. The foam ribbon may remain a visual fake only if it reads as contact-caused at 1 meter.
4. Use accepted caustic masks only on justified shallow/light receivers. No global sine sheet pretending to be material quality.
5. Use algae/biofilm as restrained breakup, not a green cover-up.
6. Capture the next proof packet as a new packet, not a renamed 1474 continuation. The close shoreline view must show 1 meter waterline contact, wet rock transition, material breakup, shell/sand or sediment scale cue, and shallow depth falloff.
7. If the route still reads black, shell-like, repetitive, or weak, roll back B27 binding and keep the accepted sources for another material pass. Do not patch with post grade.

## Low / Middle / High / Ultra Texture Residency Consequences

Low / compact:

- No ugly mode. Preserve bright ocean read, wet basalt identity, shell/sand scale cue, foam contact, and waterline material breakup.
- Runtime import target: 512 to 1024 for world terrain slices, 256 to 512 for foam/caustic masks unless a Unity owner proves headroom.
- Use mips, streaming mips, compression, packed masks, texture arrays where validated, and mip bias before reducing material identity.
- No SVT claim without MX350/page-miss proof.

Middle:

- 1024 to 2048 key photic materials where budget allows.
- Stronger roughness/wetness masks, shell/calcite masks, foam breakup, and local decals.
- Texture arrays remain default for repeated terrain families.

High:

- 2048 hero route surfaces where profiler/VRAM proof allows.
- Richer normal/detail maps, stronger wetness, more contact foam variation, and longer residency near the route.
- Spend extra budget on visible material detail, not invisible layer count.

Ultra:

- 4096 source archives are allowed; runtime 4096 is hero-only and proof-gated.
- Add denser decal layers, sharper derived channels, richer caustic/foam layering, and near-field overkill without changing material truth, terrain truth, water truth, shader channel contract, gameplay route, or save identity.

## Static Proof Boundary

This report defines the route and gates only. It proves no source is ready, imported, visually accepted, optimized, or runtime-clean. Every claim above static documentation/source inventory remains PENDING VERIFICATION until a Unity owner provides import settings report, route screenshots, clean log tail, Frame Debugger/RenderGraph evidence where relevant, profiler/VRAM proof, and explicit visual acceptance.
