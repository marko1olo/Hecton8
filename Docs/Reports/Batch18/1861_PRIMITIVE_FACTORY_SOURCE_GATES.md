# 1861 Primitive Factory Source Gates

Date: 2026-06-04
Evidence class: STATIC_SOURCE, STATIC_AUDIT
Unity/build/runtime: NOT RUN

## Scope

This remediation pass addressed the five blocker-class primitive factory routes identified by `Docs/Reports/Batch18/1860_PRIMITIVE_FACTORY_RISK_CLASSIFICATION_PACKET.md`.

Edited source:

- `Assets/_Project/Scripts/Editor/WorldProceduralFinalPrefabQualityGate.cs`
- `Assets/_Project/Editor/Assembly/PowerGridPrefabFactory.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralInteriorColonyFinalAuthoring.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ResourceDistributionBootstrapAuthoring.cs`

No prefab, scene, `.asset`, `.meta`, binary, import, bake, Unity menu action, PlayMode, build, screenshot, profiler, or runtime proof was produced by this pass.

## Source Routes Closed

### PowerGridPrefabFactory

`PowerGridPrefabFactory` no longer appends analytic primitive fallback groups when baseline power node source groups are missing. Missing baseline power sources now produce a fatal factory violation:

- Reactor
- RTG
- Battery
- Relay
- Breaker
- Junction

If a legacy `useAnalyticFallback` group reaches visual attachment anyway, the factory throws before save. Saved power prefabs are also rejected if `WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh` finds Unity built-in primitive mesh references.

Residual risk: old private analytic helper code still exists as unreachable legacy source. It is guarded by discovery and attach-path checks, but future cleanup can remove the dead helper block after real power source meshes are confirmed.

### WorldProceduralInteriorColonyFinalAuthoring

`RebuildInteriorAndColonyFinals` now calls `AllowLegacyPrimitiveFinalAuthoring` before creating folders, materials, or final prefabs. The route fails closed instead of regenerating `Assets/_Project/Prefabs/Construction/Final/InteriorColony` from cube/cylinder composites.

Residual risk: real interior/colony final meshes still need authoring or generated production mesh replacement.

### WorldProceduralPlaceholderAuthoring

The placeholder authoring menu is now `Rebuild Procedural Placeholder Proxy Variants`.

Behavior changed:

- Old placeholder entries with `finalReady=true && proxyOnly=false` are removed.
- Newly created placeholder entries use `variantId = "{family}.proxy.placeholder"`.
- Newly created placeholder entries set `proxyOnly=true`.
- Newly created placeholder entries set `finalReady=false`.
- `WorldProceduralPlaceholderMarker.Configure` now receives the `proxy.placeholder` id, not `final.placeholder`.

Residual risk: existing family assets must be cleaned by running the menu in a safe Unity slot or by a controlled asset patch. This source change prevents future promotion but does not mutate current `.asset` files.

### ResourceWorldBootstrapAuthoring

`RebuildStarterResourceSources` now calls `AllowLegacyPrimitiveProductionAuthoring` before creating resource pickup prefabs or scene placements. The starter resource route no longer silently writes visible cube/sphere/capsule pickup prefabs under `Assets/_Project/Prefabs/Resources/Pickups`.

Residual risk: resource pickups and scene resource nodes still need real authored/generated pickup meshes and material identity before this route can be restored.

### ResourceDistributionBootstrapAuthoring

`Install Resource Distribution Director` now calls `AllowLegacyPrimitiveProductionAuthoring` before creating runtime ore and magma vent prefabs. The route no longer writes `PFB_Ore_Generic` from a cube or `PFB_Ore_MagmaVentMarker` from a cylinder.

Residual risk: `ResourceDistributionDirector` needs non-primitive ore/vent prefabs before install can complete without violating the visual floor.

## Verification

Claim: source-route guards were added to the five blocker-class factory routes.
Evidence class: STATIC_SOURCE.
Artifact: this report plus the edited source files above.
Command or tool:

```powershell
rg -n "AllowLegacyPrimitiveProductionAuthoring|AllowLegacyPrimitiveFinalAuthoring|Analytic primitive fallback visual is blocked|Saved power prefab contains Unity built-in primitive mesh" Assets/_Project/Scripts/Editor Assets/_Project/Editor -g "*.cs"
```

Date: 2026-06-04.
Residual risk: static source cannot prove Unity compile, menu behavior, or runtime scene wiring.

Claim: placeholder authoring no longer promotes placeholder variants to final-ready.
Evidence class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs`.
Command or tool:

```powershell
rg -n "final\.placeholder|proxy\.placeholder|RebuildPlaceholder(Final|Proxy)Variants|SetIfDifferent\(ref entry\.(proxyOnly|finalReady)" Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs
```

Result: only `proxy.placeholder`, `proxyOnly=true`, and `finalReady=false` remain in the authoring path.
Date: 2026-06-04.
Residual risk: current family `.asset` files are not edited by this pass.

Claim: edited files have no whitespace errors.
Evidence class: STATIC_SOURCE.
Command or tool:

```powershell
git diff --check -- Assets/_Project/Scripts/Editor/WorldProceduralFinalPrefabQualityGate.cs Assets/_Project/Editor/Assembly/PowerGridPrefabFactory.cs Assets/_Project/Scripts/Editor/WorldProceduralInteriorColonyFinalAuthoring.cs Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs Assets/_Project/Scripts/Editor/ResourceDistributionBootstrapAuthoring.cs
```

Result: no diff-check errors; Git reported CRLF normalization warnings only.
Date: 2026-06-04.
Residual risk: no C# compiler run.

Claim: generated asset audit remains honest about existing production prefab debt.
Evidence class: STATIC_AUDIT.
Command or tool:

```powershell
python Tools/GeneratedAssetProductionAudit.py --root .
```

Result: `generated_asset_packages=392 fatal=0 error=41 warn=1281`.
Date: 2026-06-04.
Residual risk: the 41 production prefab/family errors still require real mesh/material/LOD/proof work.

## Remaining Blockers

This pass did not make the game visually acceptable. It only prevents several authoring tools from making the debt worse.

Still open:

- 21 production `Final` prefabs still contain Unity built-in primitive mesh references.
- 20 final-ready family links still point at primitive production prefabs.
- `PFB_SargassumCollapseChunk.prefab` remains a direct production-path primitive final and has a known `SargassumGlobalDragManager.OnValidate` relink risk.
- 95 non-`WorldProceduralProxy` primitive prefabs remain classified by `1859`; high-risk product-face classes include player, sky/ocean, held tools, world tool pickups, resource pickups, and transport.
- `HectonPrefabIntegrityScanner` repair behavior and `H8AppliedLoreBindingCatalogWindow` cube fallback remain high-conditional routes from `1860` and need scoped follow-up.
- Unity compile, menu execution, player capture, screenshots, profiler, and DataMonolith integration proof are still pending.

## Next Work

1. Patch `SargassumGlobalDragManager.OnValidate` so it cannot relink `PFB_SargassumCollapseChunk.prefab` when that prefab is primitive or production-unproven.
2. Patch high-conditional repair/fallback routes so diagnostics repair and applied-lore terminal authoring cannot save primitive fallbacks into production paths.
3. Dispatch product-face replacement packets for player/tool/item/resource/transport/sky-ocean primitives using `1859_NON_PROXY_PRIMITIVE_PREFAB_CLASSIFICATION_PACKET.md`.
4. Dispatch actual mesh replacement work for the 21 production `Final` prefab blockers using 1854-1858 reports.
5. Run Unity compile/menu validation only when Unity/import/build contention clears and one Unity owner is active.
