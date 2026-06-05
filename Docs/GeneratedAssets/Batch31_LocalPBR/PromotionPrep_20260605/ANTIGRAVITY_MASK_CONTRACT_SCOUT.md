# Batch31 Mask Contract Scout Report

**Evidence Class:** `STATIC_SOURCE` / `STATIC_DOC`

## Findings

1. **Authoritative Packed-Mask Convention:**
   The production `_MaskMap` convention for Hecton8 standard pipeline is **ARM** (`R=AO, G=Roughness, B=Metallic`). However, `Hecton_Master_Lit` uses layout `0` (`MRAO`) by default. To correctly decode an ARM texture, it requires `_MasterShadowParams.w = 3`. To decode a true `MRAO` texture, it requires `_MasterShadowParams.w = 0`. The Batch31 promotion path currently provides `MRAOSource` textures.

2. **File Proofs & Contradictions:**
   - **`Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md:16-28`**: Proves production `_MaskMap` is ARM (`R=AO`, `G=Roughness`, `B=Metallic`). Also states `Hecton_Master_Lit` requires `_MasterShadowParams.w = 3` to decode ARM, and its material default is layout `0` (`MRAO`). Explicitly forbids assigning `MRAOSource` to production `_MaskMap` by filename without a repack or an explicit material layout target.
   - **`Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader:57,241-263`**: Proves `_MasterShadowParams.w` selects mask layout. Layout `3` is ARM, Layout `0` is MRAO. Emission mask weighting requires layout `2`.
   - **`Assets/_Project/Scripts/Editor/HectonMasterMaterialMigrator1615.cs:235,424-435`**: Proves the migrator serializes `_MasterShadowParams.w` to `3` (ARM) for UberNoir, or `0` for `_MraoMap`.
   - **`Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md:23-27`**: Proves Batch31 source is `MRAO`, triggering `blocked_channel_semantics_mrao_vs_arm` against the ARM standard.
   - **`Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/ANTIGRAVITY_MASTERLIT_PROMOTION_GUARD.md:24-26`**: Proves promotion blocks until `_MasterShadowParams.w` is serialized explicitly or textures repacked.

3. **Status of `BLOCKED_CHANNEL_SEMANTICS`:**
   **No, it cannot be removed.** The mismatch is real and intentional to prevent broken visuals. To remove the block, one of the following changes is required:
   - **Option A (Offline Repack):** An offline pipeline script must repack the `MRAOSource.png` assets into true `ARM.png` format before import, natively satisfying the production pipeline.
   - **Option B (Material Serialization):** The material generator/importer must be updated to explicitly serialize `_MasterShadowParams.w = 0` on the target `.mat` files so they correctly decode the raw `MRAOSource`.

4. **Safe Next Action (Without Unity):**
   Update the Python pipeline tools (e.g., `Tools/Batch31LocalPbrImportIntent.py`) to add an offline texture-repacking step from `MRAO` to `ARM`, or update the material generation definitions to enforce `_MasterShadowParams.w = 0`. Neither step requires Unity.
