# 2306 Photic Texture Intake And Material Binding Plan

Evidence class: STATIC FILE REVIEW ONLY. Unity was not run. No `Assets/**` file was edited.

## Result

No current Gemini/source texture candidate is ready for Unity import, PBR derivation, or material binding.

The only verified user-downloaded sand/shell path is:

`Docs/GeneratedAssets/Gemini/Outputs/Batch21/TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png`

Status: `SOURCE_REFERENCE_ONLY / STATIC_REJECTED / UNITY_MATERIAL_BLOCKED`.

## Evidence Read

- `Docs/Reports/Batch22/2203_PHOTIC_TEXTURE_PROMPT_PACK.md`
- `Docs/Reports/Batch22/2203_TEXTURE_INTAKE_CHECKLIST.md`
- `Docs/GeneratedAssets/Gemini/Outputs/Batch21/*_MANIFEST.md`
- `Docs/GeneratedAssets/Gemini/Audit/Batch21/GeminiTextureIntakeAudit.md`
- `Tools/GeminiTextureIntakeAudit.py`
- `Tools/TextureSeamPeriodicRefiner.py`
- Static scans of `Assets/_Project/Art/Materials/World/**` and `Assets/_Project/Art/TEXTURES/**`

## Current Candidate Inventory

| Target | Static status | Binding decision |
|---|---|---|
| Batch21 sand/shell substrate `TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642.png` | `REJECT`; top-bottom band mismatch; seam warnings; possible crushed/baked range; repeated shell/stone clusters. | Do not import. Reference only. |
| Batch21 photic seabed substrate `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742.png` | `REJECT`; top-bottom band mismatch; seam warnings; diagonal dune/ripple repetition. | Do not import. Reference only. |
| Wet basalt 1428 QA route | Batch22 evidence: `SOURCE_ONLY / REJECT`; LR/TB mismatch and teal vein repetition. | Do not bind. Reference/correction prompt only. |
| Wet basalt 1429 QA route | Batch22 evidence: `SOURCE_ONLY / REJECT`; edge/band mismatch and repeated rock forms. | Do not bind. Reference/correction prompt only. |
| Wet basalt 1429 periodic mean | Batch22 evidence: `REJECT`; worse band mismatch, clipping, channel saturation. | Do not derive. |
| Foam/salt contact | Missing Gemini source in static scan. | Generate before planning import. |
| Caustic masks | Missing Gemini source in static scan. | Generate before planning import. |
| Algae/biofilm breakup | Missing Gemini source in static scan. | Generate after basalt/sand direction stabilizes. |

`Docs/GeneratedAssets/Gemini/Outputs/Batch22/` was absent or empty in static scan.

## Existing Unity-Side Routes

Observed material routes:

- Root 1428 surface materials: `MAT_H8SurfaceWetBasaltReal_1428`, `MAT_H8TerrainLit_BasaltSediment_1428`, `MAT_H8SurfaceShoreFoam_1428`, `MAT_SurfaceIslandWetBasalt_1428`, `MAT_SurfaceSplashFoamDirty_1428`.
- Photic material folders: `Assets/_Project/Art/Materials/World/Photic1428` through `Photic1469`.
- Existing photic/shore candidates include `MAT_H8_PhoticWetBasaltSand_1428`, `MAT_H8_PhoticShoreFoamOrganic_1428`, `MAT_H8_PhoticRouteTerrain_1464`, `MAT_H8_WetBasaltDetail_1464`, `MAT_H8_AuthoredWetBasaltBreakup_1465`, `MAT_H8_ShorelineFoamFine_1469`.

Observed texture routes:

- Existing terrain legacy route: `Assets/_Project/Art/TEXTURES/Terrain Textures/`.
- Existing generated world route: `Assets/_Project/Art/TEXTURES/World/Photic1464/`.
- Future canonical intake route for this plan: `Assets/_Project/Art/TEXTURES/World/Photic/`.

Risk:

- `git status` shows root 1428 surface materials modified and all `Photic14xx` material folders untracked.
- `Assets/_Project/Art/TEXTURES/Terrain Textures/basalt/TX_H8_WetBasaltShoreline_Albedo_1428.png` is untracked. Batch22 evidence still classifies the wet basalt 1428 source as rejected/source-only. Treat it as quarantine until a sidecar manifest and current audit prove otherwise.

## Required PBR Families Before Binding

### Sand/Shell Substrate

- Albedo: bright photic base color, no baked shadows/highlights, shell fragments mostly 1-7 cm.
- Height: grayscale relief for shells, calcite grains, basalt chips, silt pockets.
- Normal: derived from accepted height; Unity/OpenGL tangent-space unless shader contract says otherwise; BC5 target.
- MRAO: R metallic black; G roughness or smoothness per shader contract; B cavity-biased AO; A shell/calcite/algae/wetness family mask.
- Optional detail: fine sand/silt shared detail overlay.

Binding targets after audit:

- TerrainLayer replacement candidate for `L_Sand` only after controller/Unity owner approval.
- Material candidate `MAT_H8_PhoticWetBasaltSand_1428` or route terrain material clone/copy under a confirmed owner slot. Do not mutate dirty root materials.

### Wet Basalt Shoreline

- Albedo: bright wet basalt shoreline, salt erosion, pores, strata, no abyss grade.
- Height: cracks, fracture lips, pores, salt deposits.
- Normal: derived from height or high-quality existing basalt normal.
- MRAO: R metallic black except explicit mineral/ore contract; G wet/dry roughness variation; B cavity AO; A wetness/salt/mineral mask.
- Optional cyan vein mask: separate sparse accent, not base coverage.

Binding targets after audit:

- Safer future target: new `MAT_H8_PhoticWetBasalt_[sourceId]` under controller-approved photic route.
- Existing risky targets: dirty root `MAT_H8SurfaceWetBasaltReal_1428`, `MAT_SurfaceIslandWetBasalt_1428`, and untracked `Photic1464/1465` basalt materials. Do not touch during active parallel work.

### Shore Foam/Salt Contact

- RGBA mask source:
  - R: long foam/contact strength.
  - G: cross-flow wet edge breakup.
  - B: micro-bubbles/lace/sediment interruption.
  - A: confidence/wetness.
- No scenic wave photo, no opaque white strip, no storm mud.

Binding targets after audit:

- `MAT_H8_PhoticShoreFoamOrganic_1428`, `MAT_H8_ShorelineFoamFine_1469`, or Crest foam input only through the Unity owner.
- Third-party Crest materials must not be cloned or patched at runtime. Assign approved asset materials only.

### Caustic Masks

- Grayscale decal first: clean shallow caustic lace, 2048 source, controlled contrast.
- Optional RGBA lookup after grayscale pass:
  - R primary lace.
  - G secondary offset lace.
  - B suspended sparkle gaps.
  - A projection confidence.
- Depth/light reason required: bright shallows, lamps, glass, pools, or local photic projection. No global abyss caustics.

Binding targets after audit:

- `MAT_H8_PhoticFloorCaustics_1428`, `MAT_H8_FloorCausticSoft_1443`, deferred caustics feature atlas slot, or route terrain `_Caustic*` fields only by rendering owner.

### Algae/Biofilm Breakup

- Albedo/tint source: restrained teal-green/cyan film, calcite dust, organic specks, sediment breaks.
- Mask extraction: placement/tint/wetness, not random neon.
- Optional height: only for calcified crust or thick biofilm transitions.
- Roughness: wet biofilm variable, matte algae/calcite, no uniform glossy slime.

Binding targets after audit:

- Material overlay/mask on wet basalt and sand/shell route materials.
- Kelp/coral material routes only if source is a true organic material family, not a generic tint wash.

## Gemini Prompt Queue Priority

1. Wet basalt shoreline albedo source. Existing basalt sources are rejected and Unity has active/risky basalt material routes. Need a clean base material before wetness/foam work.
2. Sand/shell substrate albedo source. User-downloaded candidate is useful but rejected; first route terrain needs readable photic seabed.
3. Shore foam/salt RGBA mask. Foam/salt controls waterline material identity and can bind as a fake-first decal/mask once basalt exists.
4. Caustic grayscale decal mask. Generate before RGBA lookup; reject scenic/underwater-render outputs immediately.
5. Algae/biofilm tint breakup. Generate after basalt/sand scale is locked so it can be placed as breakup, not random color wash.
6. Wet basalt height/roughness/wetness and sand/shell height only after a source reaches `READY_FOR_DERIVATION`.

Stop rules:

- Stop any target after two repeated hard failures unless the next prompt names the exact failure.
- Do not derive PBR from `REJECT` albedo.
- Do not spend a generation on optional normal/roughness until albedo or height source is accepted.

## Seam And Static Audit Requirements

Run:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch23/[FILE].png --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch23/2306_[Target]
```

Reject thresholds from current tool/checklist:

- non-square source;
- severe LR/TB edge mismatch: `>18.0`;
- severe LR/TB 8-pixel band mismatch: `>22.0`;
- albedo luminance mean `<45.0` for surface/shallows;
- albedo black clipping `>5.0%`;
- albedo white clipping `>1.0%`;
- albedo channel saturation `>12.0%`;
- visible 2x2/3x3 seam, border, watermark, text, perspective, baked directional lighting, repeated hero motif, diagonal dune/vein/crack/foam stamp.

`PASS_STATIC` is not final approval. It only permits human 2x2/3x3 review and PBR derivation planning.

## Import Naming And Folders

Future source archive:

`Docs/GeneratedAssets/Gemini/Outputs/Batch23/`

Future production texture route after acceptance:

`Assets/_Project/Art/TEXTURES/World/Photic/`

Names:

- `TX_H8_PhoticShellSand_[Variant]_Albedo`
- `TX_H8_PhoticShellSand_[Variant]_Normal`
- `TX_H8_PhoticShellSand_[Variant]_MRAO`
- `TX_H8_PhoticShellSand_[Variant]_HeightSource`
- `TX_H8_WetBasaltShoreline_[Variant]_Albedo`
- `TX_H8_WetBasaltShoreline_[Variant]_Normal`
- `TX_H8_WetBasaltShoreline_[Variant]_MRAO`
- `TX_H8_ShoreFoamSalt_[Variant]_RGBAMask`
- `TX_H8_CausticShallows_[Variant]_Mask`
- `TX_H8_AlgaeBiofilm_[Variant]_TintMask`

Material names:

- `MAT_H8_PhoticShellSand_[Variant]`
- `MAT_H8_WetBasaltShoreline_[Variant]`
- `MAT_H8_ShoreFoamSalt_[Variant]`
- `MAT_H8_CausticShallows_[Variant]`
- `MAT_H8_AlgaeBiofilmOverlay_[Variant]`

Each family requires a sidecar manifest with source path, SHA-256, meters per tile, role map, audit output, 2x2/3x3 preview paths, import settings, shader channel contract, and rollback path.

## Import Settings Plan

- Albedo: sRGB true, compressed high quality, mips on, streaming mips on for world textures.
- Normal: Texture Type NormalMap, sRGB false, BC5/ASTC target, mips on.
- MRAO/masks/height: sRGB false, compressed high quality, mips on for world/decal usage unless shader contract requires no mips for lookup.
- Foam/caustic masks: linear if used as data masks; point or bilinear per shader contract; repeat wrap for tileable masks.
- No uncompressed runtime texture. No runtime `Texture2D` generation. No `renderer.material` clones.

## Tier Behavior

- Compact / `GlobalQualityWeight` near 0.0: 1024 standard world material max where possible, 512 for non-hero masks/decals, caustic masks 256-512, packed MRAO, shared detail maps, streaming mips. Material identity must remain readable.
- Middle / around 0.35: 1024-2048 key photic terrain, stronger roughness/wetness masks, foam/salt decals around 512-1024.
- High / around 0.7: 2048 hero route surfaces, richer normals, stronger local foam/wetness layers, longer residency if profiler allows.
- Ultra / near 1.0: 4096 source/bake archive and hero-only import where proof allows, denser decal layers, richer caustic lookup. Runtime still obeys compression, mip, and VRAM pressure downgrade.

Quality can scale resolution, decal density, detail strength, atlas page count, and residency cadence. It must not change material truth, terrain/resource identity, gameplay route, shader channel contract, or save authority.

## Rollback

- Preserve source PNG and manifest under `Docs/GeneratedAssets/Gemini/Outputs/Batch23/`.
- Never delete rejected sources; mark status and leave evidence.
- Before material binding, record old texture GUIDs and material slot values in manifest.
- Binding rollback is slot restore only: `_BaseMap`, `_BumpMap`, mask/MRAO slot, foam/caustic mask slot, scalar tint/spec/wetness values.
- No deletion without manifest and paired `.meta` deletion if deletion is ever explicitly assigned.

## Generated Texture Audit Report Format

Controller/Unity owner packet must include:

- source file path and SHA-256;
- prompt ID/text and target taxonomy;
- intended meters per tile;
- audit command;
- audit CSV/Markdown path;
- 2x2 and 3x3 preview paths;
- verdict: `STATIC_REJECTED`, `CANDIDATE_REVIEW`, or `READY_FOR_DERIVATION`;
- PBR role map: albedo, normal, height, MRAO, masks, optional detail/emission;
- import settings plan;
- material binding target and old slot rollback values;
- Compact/Middle/High/Ultra consequences;
- `PENDING UNITY/PROFILER VERIFICATION` until Unity screenshot/profiler proof exists.

## Exact Next 3 Texture Generations

1. `WetBasaltShoreline_AlbedoSource`: unlocks wet basalt shoreline and rock/waterline material identity. Existing 1428/1429 sources are rejected and one untracked Unity-side basalt PNG is unsafe.
2. `PhoticShellSand_AlbedoSource`: unlocks bright shallow seabed. The user-downloaded sand/shell candidate is the right visual direction but rejected for top-bottom banding and repeated shell/stone clusters.
3. `ShoreFoamSalt_RGBAMaskSource`: unlocks fake-first shoreline foam/salt binding after basalt, with clear RGBA channel roles and no water simulation.
