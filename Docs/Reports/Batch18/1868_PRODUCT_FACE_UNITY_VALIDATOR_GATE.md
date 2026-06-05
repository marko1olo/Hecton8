# 1868 Product-Face Unity Validator Gate

Date: 2026-06-04
Owner: local orchestrator
Evidence class: STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

Added an editor-side product-face prefab quality validator:

- `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs`
- `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs.meta`

No Unity menu action, import, compile, PlayMode, screenshot, profiler, prefab edit, asset creation, bake, or build was run.

## Purpose

`Tools/GeneratedAssetProductionAudit.py` now catches product-face primitive prefab debt outside Unity. This pass adds the matching Unity-side validation entry point for a future single Unity owner.

Menu:

```text
Hecton8/Validation/Product-Face Prefab Quality Gate
```

The validator checks:

- required exact product-face prefabs exist;
- required product-face roots exist;
- all prefabs in product-face roots are scanned;
- scanned product-face prefabs do not contain Unity built-in primitive mesh ids;
- scanned product-face prefabs have renderer hierarchy unless they have a future explicit hidden-only proof path outside this generic gate.

It does not repair, relink, create, delete, or save assets.

## Product-Face Scope

Exact required prefabs:

- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Sky_System.prefab`
- `Assets/_Project/Prefabs/Ocean_Crest.prefab`
- `Assets/_Project/Prefabs/Item_Titanium.prefab`
- `Assets/_Project/Prefabs/STRUCTURES.prefab`
- `Assets/_Project/Prefabs/Buildings/Cube.prefab`

Root scans:

- `Assets/_Project/Prefabs/Tools/Held`
- `Assets/_Project/Prefabs/Items/Tools`
- `Assets/_Project/Prefabs/Resources/Pickups`
- `Assets/_Project/Prefabs/Transport`

## Source Behavior

The validator uses `WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh(prefabPath)` so the Unity-side red gate matches the same built-in primitive mesh id rule used by the procedural final prefab gate.

Expected current Unity menu result is failure until the 42 product-face primitive-prefab errors from 1867 are fixed or explicitly proven hidden-input-only.

## Verification

Claim: source file exists and contains the menu gate.
Evidence class: STATIC_SOURCE.
Command:

```powershell
rg -n "ProductFacePrefabQualityValidator|Product-Face Prefab Quality Gate|ValidateProductFacePrefabs" Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs
```

Result: validator class, menu item, and validation entry point are present.

Claim: the gate checks the same primitive mesh path rule as the editor final-prefab quality gate.
Evidence class: STATIC_SOURCE.
Command:

```powershell
rg -n "WorldProceduralFinalPrefabQualityGate.AssetPathUsesUnityBuiltInPrimitiveMesh|Missing required product-face prefab" Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs
```

Result: primitive mesh check and required exact-prefab missing check are present.

Claim: edited validator file has no whitespace diff errors.
Evidence class: STATIC_SOURCE.
Command:

```powershell
git diff --check -- Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs
```

Result: no diff-check errors.

Claim: the new Unity script has a stable `.meta` file.
Evidence class: STATIC_SOURCE.
Command:

```powershell
Test-Path Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs.meta
rg -n "d0ed3eb992c448eab359e4ad8cbc4064" Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs.meta
```

Result: `.meta` file exists with GUID `d0ed3eb992c448eab359e4ad8cbc4064`.

## Acceptance Boundary

This pass is not a visual fix. It is a Unity-side red gate.

Still required:

- C# compile/import proof when Unity is no longer busy;
- menu execution proof and console output capture;
- replacement mesh/material/collider/LOD/proof work for product-face prefabs;
- screenshots/player capture for surface, sky, ocean, tools, pickups, and transport after replacement;
- profiler/build proof if any runtime presentation or vehicle/tool behavior changes.

## Next Work

Wave 08 implementation packets should not bypass this gate. They should either:

- replace product-face primitive visual meshes with authored/generated production meshes and proof;
- prove a prefab is hidden-input-only and should be excluded by an explicit, documented route;
- quarantine legacy root prefabs with production-reference proof.
