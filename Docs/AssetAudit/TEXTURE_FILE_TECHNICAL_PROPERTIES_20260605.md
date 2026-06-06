# Texture File Technical Properties - 2026-06-05

Status: `PENDING VERIFICATION`.
Evidence class: `STATIC_SOURCE` + `STATIC_IMAGE_PROBE` + `STATIC_META_SCAN`.
Scope: image/texture source files under `Assets/_Project`.

This file is not Unity import proof, material binding proof, visual acceptance, Frame Debugger proof, VRAM proof, or runtime texture residency proof. `.meta` values are static source text only until Unity import readback confirms them.

CSV companion: `Docs/AssetAudit/TEXTURE_FILE_TECHNICAL_PROPERTIES_20260605.csv`.

## Summary

- Source image files scanned: `140`.
- Texture ledger path matches: `140`.
- Missing texture ledger rows: `0`.
- Image probe unsupported rows: `1`.
- Missing `.meta` companions: `0`.
- Extension counts: `exr`=1, `jpg`=36, `png`=103.
- Class counts: `lighting_reflection_probe`=1, `sky_aegir_cloud`=15, `terrain_geology`=41, `texture_source`=72, `ui_sprite`=10, `water_contact`=1.
- Flag counts: `HERO_SCALE_PIXELS`=12, `IMAGE_PROBE_UNSUPPORTED`=1, `SOURCE_GT8MB`=11, `STATIC_META_SRGB_RISK_FOR_NONCOLOR_NAME`=2, `STATIC_META_STREAMING_MIPS_OFF_WORLD_RISK`=37.

## Use

Use this matrix before texture import, material route, sky/Aegir, ocean/contact, terrain/geology, UI sprite, or Addressables work. It names source dimensions, byte size, alpha presence, static `.meta` fields, and ledger match state.

## Rejection Boundary

- Do not treat `.meta` values as Unity importer readback.
- Do not treat dimensions or byte size as resident memory.
- Do not treat image probe success as visual quality.
- Do not claim material or route acceptance from this matrix.
- Do not mutate image, `.meta`, material, prefab, scene, or project files from this inventory.

## Regression Model

- CPU: static probe only; no runtime CPU change.
- GC: no runtime code changed; no allocation proof.
- Memory/VRAM: source byte size and pixel count only; no imported size, mip residency, or texture budget proof.
- Cadence: no runtime cadence changed.
- Correctness: source/image/meta risk is mapped; acceptance remains blocked by Unity importer readback, material slot readback, screenshots, Frame Debugger, memory, and route proof.

Final status: `PENDING VERIFICATION`.
