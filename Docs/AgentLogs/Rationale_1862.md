# Rationale 1862

Decision:
- Patch only `SargassumGlobalDragManager.OnValidate`.
- Keep guard under `#if UNITY_EDITOR`.
- Reuse `Hecton8.EditorTools.WorldProceduralFinalPrefabQualityGate` through editor-only reflection to avoid a direct runtime-assembly dependency on an Editor asmdef.

Reason:
- Batch18 report `1857_SARGASSUM_COLLAPSE_FINAL_CLASSIFICATION_PACKET.md` classifies `PFB_SargassumCollapseChunk.prefab` as `LATENT_RELINK_RISK / PRODUCTION-PATH PRIMITIVE FINAL`.
- Batch18 report `1861_PRIMITIVE_FACTORY_SOURCE_GATES.md` names this exact relink route as remaining work.
- Runtime spawn behavior must remain unchanged outside editor validation.

Evidence boundary:
- Static source inspection only.
- `flora.md` requested by task is absent at root; no stall. `world.md`, `TASTE.md`, `quality.md`, Batch18 reports, and named mandates were loaded.
- Unity compile and runtime behavior are PENDING VERIFICATION.
- If the editor gate cannot be loaded, validation fails closed and refuses the fallback instead of silently relinking primitive final art.

Scaling consequence:
- Low/Middle/High/Ultra: no runtime quality path changed. Editor validation prevents primitive final art from contaminating every lane.
