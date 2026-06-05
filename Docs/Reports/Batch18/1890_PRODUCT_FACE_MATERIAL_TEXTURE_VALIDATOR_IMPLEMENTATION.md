# 1890 Product-Face Material Texture Validator Implementation

Agent: 1890
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/import/menu/PlayMode/profiler/screenshots/DataMonolith: NOT RUN

## Scope

Implemented an editor-only, read-only source validator:

- `Assets/_Project/Scripts/Editor/ProductFaceMaterialTextureValidator.cs`
- menu path: `Hecton8/Validation/Product-Face Material Texture Gate`
- namespace: `Hecton8.EditorTools`

The validator does not repair, create, import, delete, relink, instantiate, save, or mutate scene/prefab/material/texture state. It builds a report object with checked prefab/material counts, failures, warnings, and findings, then logs the findings from the menu route.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1882_TRANSPORT_PLAYER_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1883_SKY_OCEAN_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1886_PRODUCT_FACE_TEXTURE_AUTHORING_PIPELINE_DISCOVERY.md`
- existing validators listed in the task file

`Docs/Actual Domains of Project.txt` was checked and produced no content. Narrow domain used: product-face material/texture source validation.

## Validator Rejection Gates

The validator rejects or flags:

- missing required albedo/base texture slots on targeted product-face materials;
- missing required normal/detail-normal slots;
- missing required packed material mask slots;
- missing packed-channel declarations on materials that require packed masks;
- unresolved/default GUID `31321ba15b8f8eb4c954353edc038b1d` in scanned product-face prefab YAML;
- package-cache URP `Lit.mat` routes in current prefab YAML or material assignments;
- historical/default-material mentions inside prior Batch18 markdown reports as warnings only, because old evidence reports must not keep the live Unity gate permanently red after the actual assets are fixed;
- `Mat_Tool_*_Placeholder` material routes;
- `MAT_PlayerSwimBlockout`;
- diagnostics/error/checker/flat-color material routes;
- null renderer material slots;
- missing product-face prefab roots or required exact prefabs;
- environment, event-only, storm/noir, or deep-only material routes assigned outside allowed product-face scope.

Allowed environment scope is intentionally narrow:

- sky/ocean materials are allowed only on `Sky_System.prefab` or `Ocean_Crest.prefab`;
- event-only and deep-only environment materials are not accepted for generic product-face body prefabs by this validator.

## Static Target Coverage

Static target material paths include:

- tool placeholders from 1880;
- resource pickup flat material shells from 1881;
- player/transport candidate, blockout, glass, runtime proof, shell, and equipment material paths from 1882;
- sky, moon, Aegir, ocean, storm/noir/deep materials from 1883;
- known package/default/diagnostic routes.

The target list is deliberately strict. Existing flat or placeholder routes are expected to fail until a future Unity owner authors and binds real texture/material sources.

## Orchestrator Follow-Up

The local orchestrator performed a static review after the 1890 agent completed and patched two issues before any Unity compile/import attempt:

- added exact `_MraoMap` packed-mask property support to match `Hecton_MraoAtlasLit` from the 1888 shader/channel contract;
- changed historical Batch18 markdown report scans from fail to warning while keeping prefab YAML/default-material asset debt as fail. Reports can preserve old evidence; current asset state must drive the live gate.

## Continuous Quality Consequences

This is a source validator, not a runtime visual system. `GlobalQualityWeight` is not consumed in code because the validator has no runtime presentation path.

- Low/compact: validator still requires distinct material roles and real texture slots; flat color or default material fallback is rejected.
- Middle: relink candidates must carry declared albedo/normal/packed-mask roles before they pass.
- High: richer wetness, wear, labels, glass/scratch, and response maps can be added without changing validator truth.
- Ultra: extra detail maps and decal density may exist, but product-face authority still requires declared roles and non-placeholder material routes.

No quality lane may change item IDs, gameplay truth, collider identity, save identity, anchors, DTO layout, or authority routes.

## Verification

Commands run:

```powershell
rg -n "GameObject\.CreatePrimitive|CreatePrimitive|new GameObject|AddComponent|PrefabUtility|SaveAsPrefab|AssetDatabase\.DeleteAsset|Resources\.Load|GameObject\.Find|float\.IsFinite|double\.IsFinite" Assets/_Project/Scripts/Editor/ProductFaceMaterialTextureValidator.cs
rg -n "Hecton8/Validation/Product-Face Material Texture Gate|31321ba15b8f8eb4c954353edc038b1d|PackageCache|Placeholder|MAT_PlayerSwimBlockout|Product-Face Material Texture Gate" Assets/_Project/Scripts/Editor/ProductFaceMaterialTextureValidator.cs
rg -n "1890a7db6c8d4c208c0fb3ff2897d7a1" -g "*.meta" .
```

Results:

- forbidden API scan: PASS, no matches;
- required token scan: PASS, menu path, default GUID, `PackageCache`, placeholder names, and gate name found;
- new `.meta` GUID uniqueness scan: PASS, exactly one match in `ProductFaceMaterialTextureValidator.cs.meta`.
- orchestrator follow-up scan: `_MraoMap` is now present in the packed-mask property list; static report text debt is warning-only and prefab YAML text debt remains fail.

Final `git diff --check` was run after all owned files were written. Result is recorded in `Docs/AgentLogs/LOG_1890.md`.

## Proof Boundary

This implementation is `STATIC_SOURCE` only.

Remaining Unity proof gap:

- Unity compilation/import was not run;
- menu execution was not run;
- actual `AssetDatabase` material resolution and live finding counts were not produced by Unity;
- no screenshots, Frame Debugger, profiler, GC, or runtime proof exists.

Status remains `PENDING UNITY PROOF`.
