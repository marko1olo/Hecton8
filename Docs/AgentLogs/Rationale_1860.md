# Rationale 1860

Evidence class: STATIC_SOURCE_AUDIT

## Decisions

- Classification uses source-route evidence only. `rg` hits can prove primitive/factory code presence and prefab-save routes, but cannot prove runtime scene use, visual quality, import health, or profiler state.
- Collider-only primitive creation is not a visual failure unless source evidence shows visible renderer output is saved or production-visible.
- Save-only editor scripts with no `CreatePrimitive`, `PrimitiveType`, or `AddAnalyticPrimitive` hit are classified as `SAVE_ONLY_NO_PRIMITIVE_SOURCE`, not visually cleared. They still belong under generated-asset validation, but they are not primitive factory blockers from this task's exact source evidence.
- `WorldProceduralPlaceholderAuthoring` is blocker-class despite the placeholder folder name because it writes generated primitive prefabs into family variants with `proxyOnly=false` and `finalReady=true`.
- `PowerGridPrefabFactory` is blocker-class because analytic fallback groups generate visible primitives and save them to `Assets/Prefabs/Construction/Power`; warning logs do not satisfy the final visual floor.
- `H8AppliedLoreBindingCatalogWindow` is conditional source risk, not an active missing-mesh proof: read-only filesystem checks found `M_Diegetic_HUD_V4_CurvedPanel.asset` and `MAT_Diegetic_HUD_V4_Projection.mat`, but the source still starts from `GameObject.CreatePrimitive(PrimitiveType.Cube)` and lacks fail-fast behavior if the mesh disappears.
- Scalability consequence: blocker primitive routes fail low, middle, high, and ultra tiers. Low/middle tiers get cheap silhouettes instead of premium approximations; high/ultra tiers expose the primitive shapes harder. Remediation must scale authored mesh/material/LOD fidelity through continuous `GlobalQualityWeight`, not binary low/ultra switches.

## Authority Used

- Root: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`, `PROCEDURAL_ASSET_PIPELINE.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`.
- Prior packets: `1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`, `1852_PROCEDURAL_PLACEHOLDER_FINAL_GATE.md`, `1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`.
- Mandates: `QA_Evidence_Text_Filter_Audit.txt`, `TOOL_Procedural_Wreckage_Generator.txt`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`, `LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt`, `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`.
