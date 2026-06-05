# AssetSystem 2026-06-05

Status: `STATIC_SOURCE / STATIC_IMAGE_QA`.

This folder contains source/prototype texture work for Aegir/cloud bands, storm masks, foam/contact masks, cleaned source candidates, and texture authoring manifests. It is an authoring corpus, not an imported production asset set.

Local entry points:

- `TEXTURE_AUTHORING_MANIFEST_3212_20260605.md`
- `AegirCloudPrototype_20260605/AegirCloudPrototype_MANIFEST_20260605.md`
- `FoamContactPrototype_20260605/FoamContactPrototype_MANIFEST_20260605.md`
- `CleanupPass_20260605/CleanupPass_MANIFEST_20260605.md`

Boundary:

- No Unity import is proven by these files.
- No material, shader, sky, ocean, terrain, Addressables, or scene binding is proven.
- No runtime texture readiness, mip residency, VRAM, memory, frame-time, GC, or platform proof is present.
- No visual acceptance is proven by source previews, contact sheets, or cleaned source files.

Route any production promotion through `PROCEDURAL_ASSET_PIPELINE.md`, `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `rendering.md`, `water.md`, `terrain.md`, and `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`.
