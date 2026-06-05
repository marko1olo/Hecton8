# 1860 Primitive Factory Risk Classification Packet

Evidence class: STATIC_SOURCE_AUDIT. One read-only filesystem existence check was used for the applied-lore terminal mesh/material. No Unity, build, import, bake, screenshot, prefab, asset, scene, binary, source, or `.meta` mutation was performed.

State: COMPLETE

## Scope

Owned outputs only:

- `Docs/Tasks/Status_1860.md`
- `Docs/AgentLogs/Rationale_1860.md`
- `Docs/AgentLogs/LOG_1860.md`
- `Docs/Reports/Batch18/1860_PRIMITIVE_FACTORY_RISK_CLASSIFICATION_PACKET.md`
- `Docs/Reports/Batch18/1860_PRIMITIVE_FACTORY_MATRIX.csv`

Search command executed:

```powershell
rg -n "CreatePrimitive|PrimitiveType|AddAnalyticPrimitive|SaveAsPrefabAsset" Assets/_Project/Scripts/Editor Assets/_Project/Editor -g "*.cs"
```

Static result:

- 58 unique editor scripts matched the exact factory/primitive search.
- 19 scripts contain `CreatePrimitive`, `PrimitiveType`, or `AddAnalyticPrimitive`.
- 39 scripts contain `SaveAsPrefabAsset` but no primitive source token from the exact pattern.
- Full row matrix: `Docs/Reports/Batch18/1860_PRIMITIVE_FACTORY_MATRIX.csv`.

## Risk Taxonomy

- `BLOCKER`: unguarded source route can save visible primitive renderers into final, production, runtime, or `finalReady && !proxyOnly` content.
- `HIGH_CONDITIONAL`: source can replace or generate production-visible primitive fallback under a condition; not proven active at runtime by this task.
- `MEDIUM_PROXY`: saved visible primitive proxies; acceptable only while quarantined from final/runtime art selection.
- `COVERED_BY_1852_GUARD`: known legacy primitive final authoring exists, but 1852 added a fail-closed guard.
- `COLLIDER_ONLY_ACCEPTABLE`: primitive token is used for collider fit/type budgeting, not visible primitive renderer generation.
- `SAVE_ONLY_NO_PRIMITIVE_SOURCE`: prefab saver found, but this exact source scan found no primitive creation route.

## Blockers

1. `Assets/_Project/Editor/Assembly/PowerGridPrefabFactory.cs`
   - Domain: construction/power.
   - Evidence: `OutputDirectory = "Assets/Prefabs/Construction/Power"`; source fallback directory includes `Assets/_Project/Prefabs/Construction/Power/Sources`; `useAnalyticFallback` groups are appended when source groups are missing; visible analytic shapes are emitted through `CreateAnalyticVisualRoot` and `AddAnalyticPrimitive`; `AddAnalyticPrimitive` calls `GameObject.CreatePrimitive`; `PrefabUtility.SaveAsPrefabAsset` writes `PFB_*.prefab`.
   - Classification: `BLOCKER`.
   - Required action: fail-closed if source mesh/prefab is missing. Remove production analytic visual fallback or route it only to diagnostics. Add output validation that rejects built-in primitive meshes in `Assets/Prefabs/Construction/Power`.

2. `Assets/_Project/Scripts/Editor/WorldProceduralInteriorColonyFinalAuthoring.cs`
   - Domain: construction/interior/colony final prefabs.
   - Evidence: `FinalPrefabFolder = "Assets/_Project/Prefabs/Construction/Final/InteriorColony"`; menu rebuild creates six `PFB_Interior_*` and `PFB_Colony_*` final prefabs; `CreateVisualPrimitive` calls `GameObject.CreatePrimitive`; `CreateCompositeFinalPrefab` saves to the final folder. No `WorldProceduralFinalPrefabQualityGate` guard was found in this file.
   - Classification: `BLOCKER`.
   - Required action: add the same fail-closed legacy primitive final guard used by 1852 or replace the route with authored final meshes before any final prefab regeneration.

3. `Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs`
   - Domain: procedural family final variant selection.
   - Evidence: menu name says `Rebuild Procedural Placeholder Final Variants`; `EnsurePlaceholderFinalVariant` sets `entry.proxyOnly=false` and `entry.finalReady=true`; generated placeholder prefabs are saved under `Assets/_Project/Prefabs/WorldRuntime/ProceduralPlaceholders`; shape build calls `GameObject.CreatePrimitive`.
   - Classification: `BLOCKER`.
   - Required action: placeholders must never be promoted to final-ready. Force `proxyOnly=true`, `finalReady=false`, or fail if no real final variant exists.

4. `Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs`
   - Domain: resource pickup prefabs.
   - Evidence: `PickupPrefabFolder = "Assets/_Project/Prefabs/Resources/Pickups"`; starter resources create cube/sphere/capsule pickups through `CreatePickupPrefab`; `CreatePickupPrefab` calls `GameObject.CreatePrimitive` and `PrefabUtility.SaveAsPrefabAsset`.
   - Classification: `BLOCKER`.
   - Required action: replace visible pickup primitives with authored resource meshes/material LODs; reserve primitive colliders only for hidden collision if needed.

5. `Assets/_Project/Scripts/Editor/ResourceDistributionBootstrapAuthoring.cs`
   - Domain: resource node prefabs.
   - Evidence: `RuntimeOrePrefabFolder = "Assets/_Project/Prefabs/Resources/Nodes"`; `CreateOrUpdateOreNodePrefab` saves `PFB_Ore_Generic.prefab` from `PrimitiveType.Cube`; `CreateOrUpdateMagmaVentPrefab` saves `PFB_Ore_MagmaVentMarker.prefab` from `PrimitiveType.Cylinder`.
   - Classification: `BLOCKER`.
   - Required action: replace runtime node primitive visuals with authored ore/vent assets or fail authoring when final assets are absent.

## High And Medium Risk

- `Assets/_Project/Scripts/Editor/HectonPrefabIntegrityScanner.cs`: `HIGH_CONDITIONAL`. It creates `PFB_ErrorCube` from `PrimitiveType.Cube` under diagnostics, but also repairs broken prefab assets with `PFB_ErrorCube` by saving back to the original prefab path. This must be dry-run/diagnostics-only unless an explicit scoped repair approval exists.
- `Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs`: `HIGH_CONDITIONAL`. It creates the terminal anchor root with `GameObject.CreatePrimitive(PrimitiveType.Cube)`, then replaces the mesh if `M_Diegetic_HUD_V4_CurvedPanel.asset` loads. Read-only filesystem checks found that mesh and material present today. Source still needs fail-fast on missing mesh and must not save a cube fallback.
- `Assets/_Project/Scripts/Editor/FloraFoundationAuthoring.cs`: `MEDIUM_PROXY`. It saves visible primitive proxy flora prefabs under `Assets/_Project/Data/Flora/GeneratedProxies/Prefabs`. Keep quarantined as proxy/debug content unless replaced with real flora final assets.
- `Assets/_Project/Scripts/Editor/CreatureProxyPrefabAuthoring.cs`: `MEDIUM_PROXY`. It saves visible primitive creature proxies under `Assets/_Project/Data/AI/GeneratedProxies/Prefabs` and exposes `ResolveDefaultProxyPrefab`. This route is acceptable only as proxy fallback, never final creature art.

## Covered Or Acceptable

- `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs`: `COVERED_BY_1852_GUARD`. It still contains final primitive visual authoring, but calls `WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring` before rebuilding `Assets/_Project/Prefabs/Construction/Final`.
- `Assets/_Project/Scripts/Editor/WorldProceduralSupportFinalAuthoring.cs`: `COVERED_BY_1852_GUARD`. Guarded before writing `Assets/_Project/Prefabs/WorldSupport/Final`.
- `Assets/_Project/Scripts/Editor/WorldProceduralOrganicMiscFinalAuthoring.cs`: `COVERED_BY_1852_GUARD`. Guarded before writing `Assets/_Project/Prefabs/Nature/OrganicMisc/Final`.
- `Assets/_Project/Editor/Physics/ColliderOptimizationEngine1609.cs`: `COLLIDER_ONLY_ACCEPTABLE`. Primitive references generate `BoxCollider`, `SphereCollider`, or `CapsuleCollider` components and save optimized collider prefab contents; no visible primitive renderer route was found.
- `Assets/_Project/Editor/Physics/ColliderOptimizerEngine1716.cs`: `COLLIDER_ONLY_ACCEPTABLE`. Same collider-only classification; primitive budget is collider fit count, not visible art.

## Dev, Scene, And Proxy-Only Routes

- `Assets/_Project/Editor/ObjectSpawner.cs`: `DEV_ONLY_SCENE_NO_SAVE`; creates scene debris cubes through a tool menu, no prefab save.
- `Assets/_Project/Scripts/Editor/FabricationBootstrapAuthoring.cs`: `DEV_ONLY_SCENE_NO_SAVE`; creates a scene station primitive for starter fabrication trial/outpost placement, no prefab save.
- `Assets/_Project/Scripts/Editor/WorldProceduralProxySceneBuilder.cs`: `DEV_ONLY_SCENE_NO_SAVE`; creates scene proxy instances and no prefab save.
- `Assets/_Project/Scripts/Editor/WorldProceduralFloraProxyShapeBuilder.cs`: `DEV_ONLY_HELPER`; helper creates visible primitive proxy shapes, no direct save.
- `Assets/_Project/Scripts/Editor/WorldProceduralProxyAuthoring.cs`: `DEV_ONLY_PROXY_SAVE`; saves proxy prefabs and writes `proxyOnly=true`, `finalReady=false`. This is acceptable only while that invariant remains enforced.

## Save-Only Candidates

The remaining 39 scripts are `SAVE_ONLY_NO_PRIMITIVE_SOURCE` for this task: they call `SaveAsPrefabAsset` but this exact source scan found no `CreatePrimitive`, `PrimitiveType`, or `AddAnalyticPrimitive` token. They are not visual-quality cleared. They should remain under final prefab and built-in mesh validators, but they are not primitive factory blockers by source evidence in task 1860.

## PowerGrid Decision

`PowerGridPrefabFactory` is not a covered route. It is a blocker. It writes production construction/power prefabs from analytic primitive fallback visuals when source prefabs/meshes are missing. A warning log is not a gate. This violates the primitive final visual floor and the power/logistics domain requirement for predictable authored routes.

## Tier Consequence

- Low: primitive final outputs are cheap silhouettes, not acceptable "minimum survival" visuals.
- Middle: primitive final outputs still read as placeholders and waste asset review time.
- High: stronger lighting/materials expose the primitive shapes more clearly.
- Ultra: visual overkill budget is wasted on primitive geometry and cannot meet the Subnautica-level floor.

No remediation code was authored by this task. Any future fix must preserve continuous `GlobalQualityWeight` scaling and must not create binary low/ultra quality switches.
