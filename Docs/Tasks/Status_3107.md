# Status 3107 - Product-Face Prefab Placement Prep

Status: `STATIC AUDIT / PLACEMENT BLOCKED`

## Done

- Authority docs and relevant mandates read.
- Static candidate pools inventoried and classified.
- `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders` rejected for visible production placement.
- Active material blocker carried forward from `MATERIAL_TEXTURE_CRITICALS_20260605.md`.
- Placement gates and staged plan written to `Docs/Reports/Batch31/3107_PRODUCT_FACE_PREFAB_PLACEMENT_PREP.md`.

## Classification Summary

- `ProceduralFinals` rocks: candidate pool, but `NEEDS MATERIAL / NEEDS UNITY VISUAL PROOF`.
- `Flora/Baked`: `NEEDS MATERIAL / NEEDS ROUTE PROOF`; currently bound to `WorldProceduralProxy` materials.
- `BioForge/Shallows/Kelp` and `TubeCoral`: `NEEDS MATERIAL / UNKNOWN VISUAL QUALITY`.
- `BioForge/Shallows/PorousRock`: `NEEDS COLLIDER / BLOCKED`; uses `MeshCollider`.
- `Construction/Final`: `NEEDS LOD / NEEDS MATERIAL / REJECT FOR PRODUCT-FACE UNTIL MESH AUDIT`; primitive mesh refs present.
- `WorldSupport/Final`: support-only; visible geometry rejected.
- `WorldProceduralProxy` and `WorldRuntime/ProceduralPlaceholders`: rejected visible placement.

## Blocked / Pending

- No Unity placement was performed.
- No Unity visual capture/readback exists.
- Base water/terrain/sky route remains a blocker.
- Product-face photic proxy material blockers remain.
- Runtime/profiler/GC proof absent.

## Next

- Wait for clean Unity slot and base route recovery.
- Material owner must rebind or prove final route-owned natural/hardware materials.
- Placement owner may then stage only gate-passing assets with substrate/contact/ecology proof.
