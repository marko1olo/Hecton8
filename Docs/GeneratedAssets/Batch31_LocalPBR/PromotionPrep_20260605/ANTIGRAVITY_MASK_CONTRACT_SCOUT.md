# ANTIGRAVITY MASK CONTRACT SCOUT

Evidence class: `STATIC_SOURCE` / `STATIC_DOC`.

## Findings

**1. Authoritative Convention**
There is a documented divergence between the source generation output and the preferred production packer route. The target shader (`Hecton_Master_Lit`) supports both, controlled by a material parameter.
- The generation source convention is **MRAO** (R=Metallic, G=Roughness, B=AO).
- The preferred production packed-mask route is **ARM** (R=AO, G=Roughness, B=Metallic).
- The target shader supports both via `_MasterShadowParams.w` (0 for MRAO, 3 for ARM), but defaults to 0 (MRAO).

**2. Proof and Contradictions**
- **Generation Source Constraint**: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md` (Line 51) dictates generated offline source output as MRAO: `R metallic, G roughness or smoothness... B ambient occlusion`.
- **Production Packer Constraint**: `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md` (Lines 16, 20) claims the production route is ARM, but explicitly warns that `Hecton_Master_Lit` defaults to layout 0 (MRAO) and requires `_MasterShadowParams.w = 3` to decode ARM.
- **Shader Decoding Support**: `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader` (Line 57) confirms `_MasterShadowParams.w` controls the layout (`0 MRAO, 1 legacy, 2 MetallicGloss, 3 ARM`) and defaults to 0.
- **Audit Conflict Detection**: `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md` (Lines 23-26) explicitly flags this MRAO-vs-ARM conflict as the reason for the semantic block.

**3. BLOCKED_CHANNEL_SEMANTICS Resolution**
The `BLOCKED_CHANNEL_SEMANTICS` flag **cannot be removed** based on the current static state.
To remove the block, an owner must make an explicit routing decision and provide static proof. The required change is:
- **Either**: Repack the Batch31 MRAO source textures into ARM format offline, AND create/update a `.mat` artifact that serializes `_MasterShadowParams.w: 3`.
- **Or**: Accept the MRAO source format directly, AND create/update a `.mat` artifact that explicitly serializes `_MasterShadowParams.w: 0` to prove the target material expects MRAO. 

**4. Safe Next Action**
Without running Unity, the safe static next action is:
- Author a python or static YAML script to generate `.mat` files for the Batch31 packages that explicitly target `Hecton_Master_Lit` and serialize the correct `_MasterShadowParams` vector, thereby satisfying the layout proof requirement.
