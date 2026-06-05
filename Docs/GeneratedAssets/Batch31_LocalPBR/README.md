# Batch31 Local PBR

Status: `LOCAL_SOURCE_BAKE_STATIC_ONLY`.

This folder contains local PBR source-bake candidates, manifests, source crops, contact sheets, and static index data. Use `Batch31_LocalPBR_INDEX.md` as the detailed local index.

Promotion-prep artifacts live in `PromotionPrep_20260605/`. Current status is `BLOCKED_CHANNEL_SEMANTICS`: albedo/normal source prep exists, but generated `MRAOSource` masks must not be promoted to production `_MaskMap` by filename. Promotion requires either repacking to ARM (`R=AO/G=Roughness/B=Metallic`) and setting the target shader/material layout explicitly, or targeting a shader that actually decodes MRAO.

Boundary:

- Source-bake output here is not Unity import proof.
- These files are not material, shader, terrain, water, Addressables, or scene binding proof.
- These files are not runtime texture readiness, mip residency, VRAM, memory, frame-time, GC, or platform proof.
- Contact sheets, tile previews, and source crops are not visual acceptance.
- `PromotionPrep_20260605` PNG previews are inspection artifacts only; they are not Unity import, material binding, route, or runtime proof.

Production promotion must route through `PROCEDURAL_ASSET_PIPELINE.md`, `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `rendering.md`, `water.md`, `terrain.md`, and `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`.
