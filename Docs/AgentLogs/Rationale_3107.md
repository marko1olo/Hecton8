# Rationale 3107 - Product-Face Prefab Placement Prep

## Mandates

- `REND_Terrain_VirtualTexturing`
- `REND_Instanced_Flora_Physics`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`

## Decisions

- Placement remains blocked. Static prefab existence is not production art proof.
- No `USABLE NOW` classification was assigned because Unity material readback, screenshots, LOD transition proof, collider proof, and compact/high captures are absent.
- `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` are rejected for visible product-facing placement because they use proxy/placeholder materials, built-in primitive meshes, and lack LOD/collider packages.
- Baked flora/coral and BioForge shallow pools are not accepted until proxy materials are replaced or proven final by Unity owner.
- BioForge porous rock is blocked by `MeshCollider` on visual mesh references.
- Construction/Final and WorldSupport/Final names are not trusted as final art because primitive mesh refs are present.

## Evidence

- Static prefab YAML scan only.
- Static material YAML scan only.
- Existing Batch31 material criticals.

Runtime status: `PENDING VERIFICATION`.
