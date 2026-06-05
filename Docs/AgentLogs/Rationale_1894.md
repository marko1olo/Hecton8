# Rationale 1894

Evidence class: STATIC_DOC / STATIC_SOURCE only.

## Decisions

1. `water.md` was used as the ocean fallback because requested `ocean.md` is absent at root. This is logged as missing authority, not a blocker.
2. ProductFace import is source/material-only. Prefab writes are forbidden until a separate `ProductFaceRelinkOwner` dry-run exists.
3. `Assets/_Project/Data/AITexturing/ai_texture_prefab_bindings.csv` is rejected for ProductFace because prior 1891 evidence shows the generic AITexture path can save prefab material changes.
4. Packed-map terms are not normalized. `ToolDecayLit` PackedMaskV1, `ProceduralBio` ORM, `MraoAtlasLit` MRAO, and `SuitVisor` dedicated slots stay distinct.
5. AI/UberNoir ARM remains blocked until the exact target shader/channel contract is attached.
6. Environment rows from 1883 are seeded as route-owned and not ProductFace body donors. They are visual floor/context only.
7. Titanium uncertainty is preserved: `Item_Titanium` must inherit canonical `TitaniumScrap` or be quarantined.
8. Low/Middle/High/Ultra consequences scale texture resolution/detail/proof depth only. Channel truth, item identity, route ownership, prefab authority, and gameplay truth do not scale.

## Residual Risk

No Unity/import/material/screenshot/profiler proof exists. This packet is a schema and static seed only.
