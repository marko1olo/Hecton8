# AI Texturing Template Output

## Authority Boundary

This folder is an Editor-output target for AI texture control-map templates. It is not a runtime asset source, not a proof artifact, and not a quality-scaling contract.

Stable authority routes:

- `PROCEDURAL_ASSET_PIPELINE.md`
- `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `rendering.md`
- `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`

Do not cite these templates as proof of texture import settings, ARM packing output, material quality, VRAM residency, generated asset quality, or visual/art QA.

## Output Owner

Unity Editor tools write UV-space template PNGs here:

- `HECTON-8/AI Texture Control Maps/Bake Selected Meshes`
- `HECTON-8/AI Texture Control Maps/AI Control Map Forge`

Expected generated suffixes:

- `_Normal.png`
- `_Depth.png`
- `_ColorID.png`
- `_Curvature.png`

Generated template resolution follows the authored CSV/profile resolution and clamps to the current 4096 cap. `GlobalQualityWeight` does not downscale these source templates; quality scaling applies to runtime presentation, not authoring reference outputs.

No Unity Editor bake execution artifact is claimed by this README.
