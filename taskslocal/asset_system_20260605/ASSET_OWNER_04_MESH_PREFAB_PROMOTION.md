# Asset Owner 04 - Mesh Prefab Promotion

Mission: prove which generated geology/flora/route prefabs can be promoted out of candidate state, and identify primitive/proxy product-face debt.

Read first:

- `Docs/AssetAudit/ASSET_ACTION_QUEUE_20260605.md`
- `Docs/AssetAudit/ASSET_SYSTEM_INDEX_20260605.md`
- `Docs/Reports/Batch32/CONTROLLER_MANDATORY_VISUAL_REFERENCE_READ_20260605.md`
- `3dmodel.md`
- `3DMODEL_GEOLOGY_ROCKS.md`
- `3DMODEL_FLORA_CORAL.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`

Candidate pools:

- `Assets/_Project/Prefabs/Nature/Rocks/ProceduralFinals`
- `Assets/_Project/Prefabs/Nature/Flora/Baked`
- `Assets/_Project/Prefabs/Nature/Flora/BioForge/Shallows`

Rejected visible pools:

- `Assets/_Project/Prefabs/WorldProceduralProxy`
- `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`

Required checks:

- P0 first: reject/promote nothing from active route while `WorldProceduralProxy` flora/coral/kelp materials remain serialized in `02_HECTON_WORLD.unity`.
- LODGroup count and transition quality.
- Renderer material refs: no proxy/placeholder/null materials.
- Mesh source: no Unity built-in primitive refs for product-face content.
- Collider route: no complex MeshCollider without justification.
- Static batching / GPU instancing compatibility.
- Screenshot proof in route-like lighting, not isolated dark inspector view.
- Screenshot proof must compare candidate/promoted prefab silhouettes and materials against the mandatory visual-reference digest where they appear in water, terrain, sky/ocean, flora, UI/cockpit, product-face, or medium-depth route views.

Proof output:

- Candidate/reject table.
- List of prefabs requiring generated/authored mesh replacement.
- Unity screenshots and Console/Stats if Unity is run.
- No visible-route promotion without material proof.
- No visible-route promotion if the asset reads as primitive, proxy, sparse, flat, or visually weaker than the matching digest reference context.
