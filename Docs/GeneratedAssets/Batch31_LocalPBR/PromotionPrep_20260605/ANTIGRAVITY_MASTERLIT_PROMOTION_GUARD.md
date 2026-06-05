# ANTIGRAVITY MASTERLIT PROMOTION GUARD

Evidence class: `STATIC_SOURCE` / `STATIC_DOC`.

## Findings

Current documentation and audit artifacts consistently block Batch31 packed-mask promotion until explicit routing/repacking and serialized layout proof is provided:

- `Docs/ARCHITECTURE/ARM_TEXTURE_PACKING_PIPELINE.md`: Explicitly blocks Batch31 `MRAOSource` direct assignment to `_MaskMap`. Requires `_MasterShadowParams.w = 3` for ARM decode in `Hecton_Master_Lit`. Notes ARM alpha is currently reserved for parallax height and emission is layout 2 only.
- `Docs/AssetAudit/BATCH31_LOCAL_PBR_IMPORT_INTENT_20260605.md`: Correctly marks MRAO masks as `BLOCKED_CHANNEL_SEMANTICS` and `requires_shader_target_layout_decision`. Explicitly states that for `Hecton_Master_Lit`, ARM requires `_MasterShadowParams.w = 3` and ARM alpha emission is not proven.
- `Tools/Batch31LocalPbrImportIntent.py`: Hardcodes `TARGET_MASK_CONTRACT` blocking the semantic mismatch, and correctly assigns `BLOCKED_CHANNEL_SEMANTICS` to the `mrao` import role.
- `Assets/_Project/Art/Shaders/Hecton_Master_Lit.shader`: Confirms the default `_MasterShadowParams.w` is 0. Confirms emission mask requires layout 2 (MetallicGloss). ARM alpha emission is explicitly not supported.
- `Assets/_Project/Scripts/Editor/HectonMasterMaterialMigrator1615.cs`: Confirms it reads mask semantics and explicitly writes `_MasterShadowParams.w` based on the source.
- `Docs/GeneratedAssets/Batch31_LocalPBR/PromotionPrep_20260605/Batch31_PromotionPrep_INDEX.json` and manifests: Explicitly set `promotion_ready: false` and `semantic_status: BLOCKED_CHANNEL_SEMANTICS` correctly stating the explicit shader target layout proof requirement.

## Stale Claims

`NO_STALE_PROMOTION_CLAIMS_FOUND`

## Promotion Checklist

To unblock Unity material promotion for the future Unity material owner:

- **Required source channel proof**: Source MRAO must be repacked to production ARM format, or explicit target routing to an MRAO decoder must be chosen.
- **Required importer settings**: Standalone BC7, Android ASTC 6x6, Linear color space, read/write off, mipmaps on.
- **Required material serialized values**: The target `.mat` must serialize `_MasterShadowParams.w = 3` for ARM layout, or `_MasterShadowParams.w = 0` for true MRAO. Do not rely on shader default layout 0.
- **Required no-Unity/static boundary**: No runtime `.Complete()` paths. Materialization is Editor-only. Do not hash texture pixels for gameplay state.
- **Required runtime/visual proof still missing**: Requires compile, import, profiler, player build proof, and visual acceptance.

## Boundary

- Evidence class: `STATIC_SOURCE` / `STATIC_DOC`.
- Unity was not run.
- `Assets`, `ProjectSettings`, `Packages` were not edited.
- Runtime proof remains pending.
