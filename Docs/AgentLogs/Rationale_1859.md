# Rationale 1859

Evidence class: STATIC_SOURCE

## Mandates Loaded

- `QA_Evidence_Text_Filter_Audit.txt`: static text proof cannot become runtime proof.
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`: fake/proxy is allowed only when it preserves belief and visual floor.
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`: production assets must have bounded runtime residency and fallback route; static scan is not residency proof.
- `CORE_Tools_Equipment_Interaction_Raycast_Heat.txt`: tools are product-face verbs; visible cube tool bodies are high-risk.
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`: item proxies are dumb views but still need clear physical silhouettes.
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`: vehicles are vulnerable pressure vessels, not primitive transport markers.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt`: render path must preserve visual floor; primitive visible renderers are asset debt.
- `TOOL_Procedural_Wreckage_Generator.txt`: generated replacements need authored-looking meshes, LODs, and collision/proxy split.

## Decisions

1. `WorldProceduralProxy` was excluded from the primary replacement queue because task scope explicitly makes it dev/proxy risk only. It remains a proxy debt bucket, not proof-safe production art.
2. `WorldRuntime/ProceduralPlaceholders` was classified `DEV_ONLY` even though active renderers exist, because all 30 scanned files contain `WorldProceduralPlaceholderMarker` and live under a placeholder folder. This is not acceptance for production wiring; it needs quarantine/reference proof.
3. The 21 `Final` prefabs were marked `BLOCKER_COVERED` instead of replanned. Evidence is `1851_GENERATED_ASSET_PRODUCTION_AUDIT.md` plus `1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`.
4. Player, held tools, world tool items, resource pickups, vehicles, sky, and ocean support prefabs were treated as visible-risk classes because representative YAML shows active MeshFilters and enabled MeshRenderers using the primitive GUID.
5. `Directional Light.prefab` was downgraded to low static risk because the primitive `Sun_Body` MeshRenderer is disabled in source. Runtime enabling remains unproven.

## Scalability Consequence

No runtime quality logic was changed. Replacement classes must still scale continuously through `GlobalQualityWeight`: compact keeps readable silhouettes/material identity; middle adds denser model/material detail; high adds richer response and LOD residency; ultra adds sensory density without changing gameplay truth, item ids, vehicle authority, or tool semantics.
