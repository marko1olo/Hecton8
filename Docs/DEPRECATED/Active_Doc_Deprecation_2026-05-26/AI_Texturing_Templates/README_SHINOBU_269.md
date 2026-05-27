# SHINOBU_269 AI Texture Control Map Output

Generated UV-space template PNGs are written here by the Unity Editor menu:

- `HECTON-8/AI Texture Control Maps/Bake Selected Meshes`
- `HECTON-8/AI Texture Control Maps/AI Texture Forge`

Expected pass suffixes: `_Normal.png`, `_Depth.png`, `_ColorID.png`, `_Curvature.png`.

Exported template resolution follows the authored CSV/profile resolution and is only aligned/clamped to the 4096 cap. `GlobalQualityWeight` does not downscale these ControlNet source PNGs.

Current status: pipeline implemented; no Unity Editor bake execution artifact is claimed in this pass.
