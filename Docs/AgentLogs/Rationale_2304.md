# Rationale 2304

Evidence boundary: static-only. No Unity, no Play Mode, no build, no imports, no scene edits.

Decision: recommend staged renderer/layer isolation, not deletion.
Reason: active suspect objects may include occlusion/service geometry. Removing visible primitive slabs is required, but deleting service objects blindly risks exposing voids or breaking culling/proof cameras.

Decision: keep `H8_DEPTH_LOW_SHELF_1428` as first Unity-owner inspection/disable target despite material drift.
Reason: geometry matches the captured horizontal sheet better than any other active object: large active rendered cube at `y:-0.9 z:30`, scale `58 x 1.15 x 8`. Current static material is dark `MAT_H8WorldAbyssRidge_1428`; Batch22 beige-material claim conflicts with current YAML. Therefore the correct action is a proof disable, not a material-only fix or deletion.

Decision: classify `H8_WORLD_LOW_WATER_OCCLUSION_00/01/02/03_1428`, `H8_DEPTH_CEILING_OCCLUSION_1428`, and `NOIR_*` slabs as service/cheat candidates requiring hidden-layer or renderer-off proof.
Reason: names imply occlusion/vignette/staging, but they are active product-facing layer-0 primitive cubes with visible renderers. If they serve culling/presentation, they must be invisible to gameplay cameras or replaced by authored fog/post/shader routes.

Decision: inactive plane/ceiling/sheet objects remain watchlist, not immediate disable list.
Reason: static YAML shows `m_IsActive: 0` or renderer `m_Enabled: 0`; they cannot be blamed as current active source without Unity proof, but they are dangerous if reactivated.
