# Rationale 1878

## Decisions
- Used a dedicated editor validator instead of expanding the generic product-face validator. Reason: sky/ocean needs separate handling for hidden Crest data-input primitives versus visible source art.
- Treated missing `Sky_System.prefab` or `Ocean_Crest.prefab` as failure. Reason: missing source cannot be accepted by static absence.
- Accepted only exact Crest input-source paths with exact Crest component types and hidden semantics. Reason: broad `Ocean_Crest` primitive whitelisting would hide visible product-face debt.
- Rejected `SargassumMicroFaunaBoids.boidMesh` when it resolves to Unity built-in primitive mesh GUID. Reason: indirect `RenderMeshIndirect` presentation can expose flat primitive cards even without a MeshFilter.
- Added scene override warnings as structured findings. Reason: scene YAML cleanup/overrides do not prove runtime first-frame state, visual quality, or profiler/GC behavior.

## Scaling Consequences
- Compact: validator blocks primitive source art from being justified by darkness/fog; later proof must show readable ocean/sky.
- Middle: same source truth; richer water/sky is proof burden, not validator mutation.
- High: same source truth; extra reflections/cloud detail cannot excuse primitive source.
- Ultra: same source truth; visual overkill is sensory only and cannot change acceptance route.
