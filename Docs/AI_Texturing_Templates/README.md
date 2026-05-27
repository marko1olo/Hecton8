# AI Texturing Template Output

## Authority Boundary

This folder is an Editor-output target for AI texture control-map templates. It is not a runtime asset source, not a proof artifact, and not a quality-scaling contract.

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
