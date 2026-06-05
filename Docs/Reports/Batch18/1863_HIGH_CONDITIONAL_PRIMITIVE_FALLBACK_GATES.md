# 1863 High-Conditional Primitive Fallback Gates

Date: 2026-06-04
Evidence class: STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

Edited:

- `Assets/_Project/Scripts/Editor/HectonPrefabIntegrityScanner.cs`
- `Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs`

Owned logs:

- `Docs/Tasks/Status_1863.md`
- `Docs/AgentLogs/Rationale_1863.md`
- `Docs/AgentLogs/LOG_1863.md`
- `Docs/Reports/Batch18/1863_HIGH_CONDITIONAL_PRIMITIVE_FALLBACK_GATES.md`

## Changes

### HectonPrefabIntegrityScanner

- Prefab asset scans now call `ScanHierarchy(..., allowRepair: false)` for prefab contents.
- Missing prefab instances inside prefab assets are recorded in `SkippedPrefabAssetRepairs` instead of being unpacked or replaced and saved.
- Broken variant/null-loading prefab asset repair is diagnostic-only. It records the candidate replacement path and primitive-candidate state, but does not call `SaveAsPrefabAsset` on the original prefab path.
- `PFB_ErrorCube` creation remains scoped to `Assets/_Project/Prefabs/Diagnostics/PFB_ErrorCube.prefab`.
- `WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh` is used only to annotate blocked candidate replacements; it does not authorize a write.

### H8AppliedLoreBindingCatalogWindow

- `CreateAppliedLoreTerminalAnchorPrefab` now loads `M_Diegetic_HUD_V4_CurvedPanel.asset` and `MAT_Diegetic_HUD_V4_Projection.mat` before creating the primitive root, creating folders, or saving the prefab.
- Missing mesh or material logs an error and returns with `CreatedOrUpdated=false`.
- A root created from `PrimitiveType.Cube` must bind the real mesh and material before `SaveAsPrefabAsset` can run.

## Evidence

Claim: scanner no longer writes `PFB_ErrorCube` back to production prefab paths.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/Editor/HectonPrefabIntegrityScanner.cs`.
Command or Unity tool: `rg -n "CreatePrimitive|PrimitiveType|PFB_ErrorCube|SaveAsPrefabAsset|M_Diegetic_HUD_V4_CurvedPanel|WorldProceduralFinalPrefabQualityGate" Assets/_Project/Scripts/Editor/HectonPrefabIntegrityScanner.cs Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs`.
Result: scanner hits show `PFB_ErrorCube` only as diagnostics path/name and diagnostics save path; production-path broken-prefab repair now records blocked repair text and has no `SaveAsPrefabAsset(tempRoot, prefabPath)` route.
Date: 2026-06-04.
Residual risk: no Unity compile/import proof.

Claim: applied-lore terminal anchor authoring cannot save a cube fallback when required mesh/material are missing.
Evidence Class: STATIC_SOURCE.
Artifact: `Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs`.
Command or Unity tool: same static `rg` command above.
Result: `CreatePrimitive(PrimitiveType.Cube)` remains in the anchor method, but required mesh/material loads and failure returns now precede root creation and `SaveAsPrefabAsset`.
Date: 2026-06-04.
Residual risk: no Unity menu execution proof.

Claim: scoped diff has no whitespace errors.
Evidence Class: STATIC_SOURCE.
Artifact: scoped Git diff.
Command or Unity tool: `git diff --check -- Assets/_Project/Scripts/Editor/HectonPrefabIntegrityScanner.cs Assets/_Project/Scripts/Editor/Narrative/H8AppliedLoreBindingCatalogWindow.cs Docs/Reports/Batch18/1863_HIGH_CONDITIONAL_PRIMITIVE_FALLBACK_GATES.md`.
Result: exit code 0; Git printed LF-to-CRLF normalization warnings for the two edited source files only.
Date: 2026-06-04.
Residual risk: no C# compiler run.

## Scaling Consequence

- Low: blocked primitive fallback prevents cheap cube silhouettes from entering production paths.
- Middle: missing authored assets become visible authoring failures instead of silent placeholder saves.
- High: stronger lighting/material response cannot expose an accidental cube fallback from these two routes.
- Ultra: visual-overkill budget is not spent polishing primitive placeholders from these authoring paths.

## Pending Verification

- Unity compile: NOT RUN.
- Unity menu execution: NOT RUN.
- Asset import behavior: NOT RUN.
- Runtime scene behavior: NOT RUN.
