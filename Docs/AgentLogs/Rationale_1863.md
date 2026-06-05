# Rationale 1863

Decision: fail closed on prefab-asset destructive repair.

Reason:
- No explicit existing approval path was present for overwriting arbitrary prefab assets.
- `PFB_ErrorCube` is acceptable only as a diagnostics prefab under Diagnostics, not as a production-path replacement asset.
- Broken prefab assets and missing prefab instances inside prefab assets now produce static findings instead of source-authored overwrite behavior.

Decision: hard-fail applied-lore terminal anchor save when real mesh/material are missing.

Reason:
- The previous route created a cube first and only replaced mesh/material if assets loaded.
- Missing `M_Diegetic_HUD_V4_CurvedPanel.asset` or `MAT_Diegetic_HUD_V4_Projection.mat` would allow primitive fallback save.
- The patched route loads both required assets before root creation, folder creation, or prefab save.

Evidence class: STATIC_SOURCE only.
