# Gemini Generation Queue 2026-06-04

Evidence class: `STATIC_DOC / OPERATOR_QUEUE`. No browser automation was run for this file. Do not save generated images into `Assets/**`.

This queue is static operator planning only. It does not prove generation, Unity import, material binding, Addressables residency, runtime texture readiness, visual acceptance, VRAM, memory, frame-time, GC, or platform readiness.

## Save Location

Use:

`Docs/GeneratedAssets/Gemini/Outputs/Batch22/`

This is an expected/future output path unless an explicit generation task creates it. Do not infer Batch22 generation occurred from the path text alone.

Use sidecar manifests beside every downloaded source.

## Naming

`TX_B22_[Target]_[Role]_[YYYYMMDD]_Gemini_[HHMMSS].png`

Manifest:

`TX_B22_[Target]_[Role]_[YYYYMMDD]_Gemini_[HHMMSS]_MANIFEST.md`

## Daily Budget

Assumption: 7 accounts x 3-4 generations/day = 21-28 generations/day.

Spend order:

1. `WetBasaltShoreline_AlbedoSource`
2. `PhoticShellSandSubstrate_AlbedoSource`
3. `ShoreFoamSaltContact_RGBAMaskSource`
4. `CausticDecal_GrayscaleMaskSource`
5. `ShallowAlgaeBiofilm_TintSource`
6. `BasaltCyanVeins_AlbedoMaskSource`
7. `CausticLookup_RGBAMaskSource`
8. `ShallowCoralCalcite_AlbedoHeightSource`
9. Optional `HeightSource` follow-up from an accepted source
10. Optional `RoughnessSource` follow-up from an accepted source
11. Optional `NormalSource` follow-up from an accepted height/source

## Stop Conditions

- Stop after two same-failure attempts unless the next correction prompt names the failure exactly.
- Stop any source with perspective, object render, text, logo, frame, baked lighting, or visible border.
- Stop derivation when source audit is `REJECT`.
- Stop if a 2x2/3x3 preview shows repeated hero forms even when script metrics look acceptable.

## Audit Path

Run:

```powershell
python Tools/GeminiTextureIntakeAudit.py --project-root . --root Docs/GeneratedAssets/Gemini/Outputs/Batch22/[FILE].png --out-dir Docs/GeneratedAssets/Gemini/Audit/Batch22/[TARGET]
```

Then inspect:

- `GeminiTextureIntakeAudit.csv`
- `GeminiTextureIntakeAudit.md`
- `tile_previews/*_tile2x2.png`
- contact sheet if multiple sources were scanned

## Next Five Prompts

Full prompt text is in `Docs/Reports/Batch22/2203_PHOTIC_TEXTURE_PROMPT_PACK.md`.

1. Prompt 01: Wet Basalt Shoreline Albedo.
2. Prompt 03: Sand/Shell Substrate Albedo Source.
3. Prompt 05: Shore Foam/Salt Contact RGBA Mask.
4. Prompt 06: Caustic Decal Mask Source.
5. Prompt 08: Shallow Algae/Biofilm Tint Breakup.

## Current Blocked Sources

- `TX_H8_WetBasaltShoreline_Albedo_1428`: `SOURCE_ONLY / REJECT`.
- `TX_H8_WetBasaltShoreline_Albedo_1429`: `SOURCE_ONLY / REJECT`.
- `TX_H8_WetBasaltShoreline_Albedo_1429_periodic_mean`: `REJECT`.
- `TX_B21_PhoticSeabedSubstrate_AlbedoHeightSource_20260604_Gemini_155742`: `SOURCE_REFERENCE_ONLY / REJECT`.
- `TX_B21_PhoticShellSandSubstrate_AlbedoSource_20260604_Gemini_164642`: `SOURCE_REFERENCE_ONLY / REJECT`.

No inspected source is currently `READY_FOR_DERIVATION`.
