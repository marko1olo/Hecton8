# Texture Import Role Matrix - 2026-06-05

Status: `PENDING_VERIFICATION`.
Evidence class: `STATIC_DOC` + `STATIC_SOURCE`.
Scope: texture import-role planning only. No Unity import settings, `.meta` files, materials, prefabs, scenes, or files under `Assets` were edited.

## Authority

Mandates followed:

- `QA_Evidence_Text_Filter_Audit`
- `STRM_Async_Asset_Upload_Texture_Settings`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`

Root/domain docs used:

- `TASTE.md`
- `VISION_LOCKS.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `rendering.md`
- `streaming.md`
- `TEXTURE_AUTHORING_RECIPES_20260605.md`
- `SOURCE_PROTOTYPE_CLEANUP_REVIEW_20260605.md`

## Output

Matrix file:

- `Docs/AssetAudit/TEXTURE_IMPORT_ROLE_MATRIX_20260605.csv`

The CSV defines intended import roles for:

- P0 foam/contact albedo, normal, MRAO, and RGBA contact masks.
- P1 Aegir/cloud band albedo, storm mask, and detail.
- P1 wet basalt and shell/sand terrain albedo/normal/MRAO candidates.
- P1 flora/coral albedo, normal, detail, and mask candidates.
- P2 oxygen UI icon and oxygen-tank mask role split.

## Static Policy

- Albedo/color textures: `sRGB=true`, mipmaps on for world/celestial textures, compressed high quality.
- Normal maps: `sRGB=false`, `NormalMap` import type, BC5 or platform equivalent.
- MRAO/masks/detail: `sRGB=false`, linear import, compressed high quality, channel contract documented before binding.
- UI sprites: no streaming mips by default; atlas/import proof required before HUD readiness.
- Source-only cleanup outputs under `Docs/GeneratedAssets` must not be imported directly as final art.

## Hard Rejections

- Do not promote generated source files as final material art.
- Do not raw-patch `.meta`, `.mat`, `.prefab`, `.unity`, or `.asset` files.
- Do not use the rejected Crest `foam.png` as visible final waterline art.
- Do not claim Aegir/sky readiness without `Mat_HectonSky` or Aegir material readback and bright surface screenshot.
- Do not turn compact lane into ugly mode. Lower mips/residency are allowed only if surface, waterline, terrain, and Aegir remain premium and readable.

## Proof Needed Before Import Changes

- Unity import settings proof for sRGB, texture type, compression, mipmaps, streaming mips, max size, and platform overrides.
- Material slot readback for Crest/ocean, sky/Aegir, terrain, flora/coral, and UI/HUD bindings.
- Contact sheets plus in-scene screenshots.
- Frame Debugger or Stats proof for visible material paths.
- Addressables/group ownership before heavy route assets become runtime dependencies.

## Scalability Consequences

- Low/compact: use compressed maps, baked AO, conservative normals, and strong composition. No flat fallback textures.
- Middle: route-owned PBR stacks with documented channel contracts and stable mip/streaming behavior.
- High: richer detail normals, wetness masks, cloud/storm detail, and longer LOD residency after baseline proof.
- Ultra: layered material detail and visual overkill only after memory and render proof; gameplay truth and material authority route do not change.

## Regression Model

- CPU: no runtime code changed.
- GC: no runtime code changed.
- Memory/VRAM: matrix only; import/residency changes remain future work and require proof.
- Cadence: no runtime cadence changed.
- Correctness: reduces future import mistakes by separating color, normal, linear mask, UI sprite, and source-only roles.

Final status: `PENDING_VERIFICATION`.
