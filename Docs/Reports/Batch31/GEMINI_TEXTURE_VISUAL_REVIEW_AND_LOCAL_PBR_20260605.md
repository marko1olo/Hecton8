# Gemini Texture Visual Review / Local PBR Prep - 2026-06-05

Status: SOURCE PREP / NOT UNITY IMPORTED / NO VISUAL ACCEPTANCE

Evidence class: `STATIC_IMAGE_VISUAL_REVIEW`, `LOCAL_SOURCE_BAKE_STATIC_ONLY`.

## Verdict

The static intake script over-rejects artistically usable sources. It is useful for seam/clipping warnings, not for final art judgment.

The current Gemini sources are not trash. They are also not ready for direct production import.

## Manual Visual Review

### Wet Basalt Shoreline 1429

Path: `Docs/GeneratedAssets/Gemini/TX_H8_WetBasaltShoreline_Albedo_1429.png`

Useful:

- good wet-rock structure;
- strong shoreline mineral/salt/barnacle detail;
- readable NASA-punk coastal material direction.

Problems:

- visible Gemini watermark in the bottom-right corner; this is a final-cleanup issue, not a reason to throw away the source for prototyping;
- large repeated rock islands will tile obviously at route scale;
- shadows are baked into albedo;
- needs true PBR separation and Unity material proof.

### Periodic Wet Basalt Variants

Paths under `Docs/GeneratedAssets/Gemini/Refined/`

Useful:

- source texture continuity attempt.

Problems:

- periodic and dark-preserve versions are overbright/washed;
- mean version is too dark and harsh;
- none should be directly imported as final albedo.

### Photic Seabed / Shell Sand

Paths under `Docs/GeneratedAssets/Gemini/Outputs/Batch21/`

Useful:

- visually credible shallow substrate sources;
- shell/debris detail is better than current route placeholder material quality.

Problems:

- baked light/shadow patterns;
- not authored as material channels;
- needs seam review, de-shadow, normal/MRAO authoring, and Unity proof.

## Local Prep Done

Created local source-bake packages:

`Docs/GeneratedAssets/Batch31_LocalPBR/`

Packages:

- `TX_B31_WetBasaltShoreline_1429`
- `TX_B31_WetBasaltShoreline_1429_FullSourcePrototype`
- `TX_B31_PhoticSeabedSubstrate_2102`
- `TX_B31_PhoticShellSandSubstrate_2102`

Each package contains:

- source crop;
- albedo source;
- height source;
- normal source;
- MRAO source;
- 2x2 albedo preview;
- 2x2 normal preview;
- JSON/Markdown manifest with SHA-256.

Contact sheet:

`Docs/GeneratedAssets/Batch31_LocalPBR/Batch31_LocalPBR_contact_sheet.png`

Full-source prototype:

- `Docs/GeneratedAssets/Batch31_LocalPBR/TX_B31_WetBasaltShoreline_1429_FullSourcePrototype/`
- Purpose: preserve the full useful wet-basalt image for material prototyping instead of destroying information through a blind watermark crop.
- Status: prototype source only. The small Gemini mark is tolerated for temporary material tests because the user will remove it later if the source proves useful.
- Risk: visible mark, baked light, and macro-repeat remain final blockers.

## Restrictions

- Not imported into Unity.
- Not final PBR.
- Not visual acceptance.
- Do not write into `Assets` until an owner imports with correct BC7/BC5 settings and route material proof.
- Temporary material prototyping may use the original Gemini sources if it improves route readability.
- Final acceptance still requires watermark removal, repeat breakup, PBR channel cleanup, import settings proof, and route screenshot proof.
- Do not crop, overwrite, or destroy useful texture information blindly. Any crop/cleanup variant is an experiment, not the canonical source.
- Watermark presence alone is not a reason to reject a source during prototype material work. It is a final-cleanup blocker only.

## Next Worker Task

Use the local source-bake pack as input for a Unity material owner:

1. choose one wet basalt and one shallow substrate package;
2. bind through route-owned materials, not runtime material clones;
3. use BC7 for albedo/MRAO and BC5 for normals;
4. verify 2x2 seam in Unity;
5. capture shoreline/photic route screenshot;
6. compare against mandatory examples by eye, not only script thresholds.
