# 1884 Product-Face Editor Source Static Compile Risk Audit

Date: 2026-06-04
Agent: 1884
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/dotnet/menu/runtime: NOT RUN

## Scope

Audited only the five Batch18 product-face editor source routes and their `.meta` files:

- `Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ProductFaceTransportMeshSourceAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs`

This pass did not edit source, Unity assets, prefabs, scenes, binaries, generated meshes, task files, or `.meta` files.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `PROCEDURAL_ASSET_PIPELINE.md`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1875_RESOURCE_PICKUP_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1876_TRANSPORT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1877_PLAYER_SUIT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1878_SKY_OCEAN_SOURCE_VALIDATOR_IMPLEMENTATION.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_SEQUENCE.csv`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`

`Docs/Actual Domains of Project.txt` was absent. Narrow domain used: product-face editor source/static compile risk and relink-route risk.

## Finding Summary

Matrix artifact:

`Docs/Reports/Batch18/1884_PRODUCT_FACE_EDITOR_SOURCE_STATIC_COMPILE_RISK_MATRIX.csv`

Counts:

- BLOCKER: 4
- MAJOR: 0
- MINOR: 1
- INFO: 7

## Blockers

The blockers are route mismatches, not proven C# syntax failures.

The 1879 relink CSV defines future mesh asset folders without the `ProductFace` subfolder and, for tools/resources/transport, expects per-family folders:

- player suit: `Assets/_Project/Art/Generated/PlayerSuit/`
- tools: `Assets/_Project/Art/Generated/Tools/<ToolName>/`
- resources: `Assets/_Project/Art/Generated/Resources/<ResourceName>/`
- transport: `Assets/_Project/Art/Generated/Transport/<TransportName>/`

Actual source constants write elsewhere:

- `ProductFaceToolMeshSourceAuthoring.cs:21` writes under `Assets/_Project/Art/Generated/ProductFace/Tools`
- `ProductFaceResourcePickupMeshSourceAuthoring.cs:19` writes under `Assets/_Project/Art/Generated/ProductFace/Resources`
- `ProductFaceTransportMeshSourceAuthoring.cs:30` writes under `Assets/_Project/Art/Generated/ProductFace/Transport`
- `ProductFacePlayerSuitMeshSourceAuthoring.cs:22` writes under `Assets/_Project/Art/Generated/ProductFace/PlayerSuit`

This is likely to waste the future serialized Unity slot because generated meshes will not land where the relink sequence, material proof plan, and future owner instructions expect them.

## Static Risk Notes

No finite API risk was found in the five audited `.cs` files. The prior finite-API cleanup notes in reports 1875, 1876, and 1877 are consistent with current source.

No `GameObject.CreatePrimitive` or `CreatePrimitive` call was found in the five audited `.cs` files.

No destructive asset write route was found. The four source-authoring files write Mesh assets through `AssetDatabase.CreateAsset`, update existing Mesh assets with `EditorUtility.CopySerialized` / `EditorUtility.SetDirty`, and call `AssetDatabase.SaveAssets`. The sky/ocean validator is read-only by static scan.

The sky/ocean validator traverses loaded prefab assets through `AssetDatabase.LoadAssetAtPath` and `GetComponentsInChildren`. That is acceptable for an Editor validation route, but it is not scene/runtime proof.

Menu paths introduced:

- `HECTON-8/Product Face/Author Tool Mesh Sources 1874`
- `HECTON-8/Product Face/Author Resource Pickup Source Meshes`
- `HECTON-8/Product Face/Author Transport Mesh Sources`
- `HECTON-8/Product Face/Author Player Suit Mesh Sources 1877`
- `Hecton8/Validation/Sky-Ocean Source Primitive Gate`

The validator casing matches the 1878/1879 runbooks. The split between `HECTON-8` and `Hecton8` is a minor discoverability risk only.

## GUID Hygiene

All five audited `.meta` GUIDs are present and unique by repository static scan:

- `f9fe30c6f06647818151e5466d0488f6`: 1 hit
- `6e9ab5fd8ddc47b2a27df93f1c95b875`: 1 hit
- `0d3b9586093f4f1fb7a4e02d8076e876`: 1 hit
- `1877f7b3d84e4f81a8b44e08a8d71877`: 1 hit
- `1878f6a2c2a44a77a810bdf1e0bfb878`: 1 hit

## Verification

Command:

```powershell
git diff --check -- Docs/Reports/Batch18/1884_PRODUCT_FACE_EDITOR_SOURCE_STATIC_COMPILE_RISK_AUDIT.md Docs/Reports/Batch18/1884_PRODUCT_FACE_EDITOR_SOURCE_STATIC_COMPILE_RISK_MATRIX.csv Docs/Tasks/Status_1884.md Docs/AgentLogs/Rationale_1884.md Docs/AgentLogs/LOG_1884.md
```

Result: PASS, no output.

Command:

```powershell
rg -n "float\.IsFinite|double\.IsFinite" Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceTransportMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs
```

Result: PASS, no hits, exit 1.

Command:

```powershell
rg -n "GameObject\.CreatePrimitive|CreatePrimitive" Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceTransportMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceSkyOceanSourceValidator.cs
```

Result: PASS, no hits, exit 1.

Command:

```powershell
$guids = @('1878f6a2c2a44a77a810bdf1e0bfb878','6e9ab5fd8ddc47b2a27df93f1c95b875','f9fe30c6f06647818151e5466d0488f6','0d3b9586093f4f1fb7a4e02d8076e876','1877f7b3d84e4f81a8b44e08a8d71877'); foreach ($g in $guids) { $hits = rg -n "guid: $g" .; $count = ($hits | Measure-Object).Count; "${g},${count}"; $hits }
```

Result: PASS, each GUID count was 1.

Command:

```powershell
Import-Csv Docs/Reports/Batch18/1884_PRODUCT_FACE_EDITOR_SOURCE_STATIC_COMPILE_RISK_MATRIX.csv | Measure-Object
```

Result: PASS, Count = 12.

## Acceptance Boundary

This pass does not prove:

- Unity import or compile health;
- menu execution;
- generated Mesh asset validity;
- material/texture acceptance;
- prefab relink correctness;
- scene state;
- screenshots or visual acceptance;
- Frame Debugger, profiler, GC, memory, VRAM, or player-build behavior.

All visual/runtime acceptance remains `PENDING VERIFICATION`.

## Highest-Risk Follow-Up

Before any Unity owner executes the authoring menus, reconcile the output folder constants against `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_SEQUENCE.csv`. Either the source paths or the contract must become one canonical route. Running Unity first would generate assets into disputed folders and contaminate the relink proof chain.

## Orchestrator Follow-Up

After this audit completed, the orchestrator reconciled `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md` and `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_SEQUENCE.csv` to the implemented source constants:

- `Assets/_Project/Art/Generated/ProductFace/PlayerSuit/`
- `Assets/_Project/Art/Generated/ProductFace/Tools/`
- `Assets/_Project/Art/Generated/ProductFace/Resources/`
- `Assets/_Project/Art/Generated/ProductFace/Transport/`

The 1884 matrix remains a historical static audit snapshot. The four route mismatch blockers are resolved in the current 1879 contract/CSV state, but Unity import/menu/generation/relink proof remains not run.
