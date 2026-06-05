# 1887 Product-Face Legacy Reference Quarantine Decision Packet

Date: 2026-06-04
Agent: 1887
Mode: REPORT_ONLY_STATIC_REFERENCE_AUDIT
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Owned outputs:

- `Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md`
- `Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv`
- `Docs/Tasks/Status_1887.md`
- `Docs/AgentLogs/Rationale_1887.md`
- `Docs/AgentLogs/LOG_1887.md`

No source code, Unity asset, prefab, scene, binary, generated mesh, task file, `.meta`, import, bake, menu, PlayMode, profiler, build, or DataMonolith action was run or modified.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `inventory.md`
- `construction.md`
- `tools.md`
- `Docs/Reports/Batch18/1867_PRODUCT_FACE_PREFAB_AUDIT_GATE.md`
- `Docs/Reports/Batch18/1870_RESOURCE_PICKUP_VISUAL_SOURCE_PACKAGE.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_AND_PROOF_CONTRACT.md`
- `Docs/Reports/Batch18/1879_PRODUCT_FACE_RELINK_SEQUENCE.csv`
- `Docs/Reports/Batch18/1880_TOOL_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1881_RESOURCE_MATERIAL_TEXTURE_ROLE_PACKAGE.md`
- `Docs/Reports/Batch18/1885_PRODUCT_FACE_PREFAB_ANCHOR_REFERENCE_STATIC_SNAPSHOT.md`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `.agents-skills/TOOL_Designer_Facades_CSV_Binary_Bridge.txt`

`Docs/Actual Domains of Project.txt` was checked and is absent. Narrow domain used: product-face legacy/default reference quarantine decision.

## Static Boundary

Static text/YAML hits prove text presence only. This packet does not prove Unity import health, scene instance wiring, PlayMode behavior, player-visible quality, material import state, frame time, GC, profiler, Frame Debugger, or runtime acceptance.

No deletion, move, quarantine, relink, or package/default material replacement is authorized by this packet.

## Production Reference Scan Results

### Item_Titanium

Target: `Assets/_Project/Prefabs/Item_Titanium.prefab`

Static facts:

- Prefab exists with meta GUID `64bf1bfdf2fc079449f22ccd9187776e`.
- Scoped GUID scan across `Assets`, `Packages`, `ProjectSettings`, `Docs/Reports`, `Docs/Tasks`, `Docs/AgentLogs`, and `Tools` found only `Assets/_Project/Prefabs/Item_Titanium.prefab.meta` for that GUID.
- Path/stem/object scan found active source references in `Tools/GeneratedAssetProductionAudit.py`, `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs`, `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs`, `Assets/_Project/Scripts/Editor/ProductFaceResourcePickupMeshSourceAuthoring.cs`, and `Assets/_Project/Scripts/Editor/ScanIntelValidator.cs`.
- `ScanIntelValidator` searches scene object path `--- GAMEPLAY ---/Item_Titanium`, so a scene or editor bootstrap route may still expect the object even though the prefab GUID is not referenced by scoped static scan.
- Prefab YAML uses built-in cube `m_Mesh fileID 10202`, non-trigger `BoxCollider` size `1,1,1`, `PickupItem`, `InteractionHighlighter`, and `ScannableTarget`.
- `itemData` resolves by prior/static evidence to `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset`.
- `ScannableTarget.entryId` is `resource.titanium_fragment`.

Decision: `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`.

Reason: no scoped GUID production reference was found, but active editor/bootstrap/validator references and potential scene object expectations prevent deletion or quarantine from static evidence alone. If retained, it must inherit canonical `TitaniumScrap` mesh/material/data truth only.

### STRUCTURES

Target: `Assets/_Project/Prefabs/STRUCTURES.prefab`

Static facts:

- Prefab exists with meta GUID `fe67818ec2cf8f74684ab2f6ef6711ee`.
- Scoped GUID scan found only `Assets/_Project/Prefabs/STRUCTURES.prefab.meta` for that GUID.
- Path/stem scan found active source references in `Tools/GeneratedAssetProductionAudit.py` and `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs`, plus prior Batch18 reports.
- A sibling `Assets/_Project/Prefabs/STRUCTURES 1.prefab` exists by stem scan. This packet did not modify or classify it beyond duplicate-root risk.
- Prefab YAML contains root `m_Name: STRUCTURES` and child `m_Name: Item_Titanium`.
- Child uses built-in cube `m_Mesh fileID 10202`, material GUID `31321ba15b8f8eb4c954353edc038b1d`, and non-trigger `BoxCollider` size `1,1,1`.

Decision: `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`.

Reason: no scoped GUID production reference was found, but aggregate/root uncertainty remains and the child leaks primitive `Item_Titanium` plus package/default material risk if the aggregate is used. If retained, it must delegate to canonical child assets; it must not keep the primitive child.

### Buildings/Cube

Target: `Assets/_Project/Prefabs/Buildings/Cube.prefab`

Static facts:

- Prefab exists with meta GUID `68067bbdb13059f4b80ecc380f14ce7d`.
- Scoped GUID scan found `Assets/_Project/Prefabs/Buildings/Cube.prefab.meta` and `Assets/MapMagic/Map_Graph/Old tries/Terrain.asset`.
- Path/stem scan also found `Tools/GeneratedAssetProductionAudit.py`, `Assets/_Project/Scripts/Editor/ProductFacePrefabQualityValidator.cs`, and prior Batch18 reports.
- Prefab YAML uses built-in cube `m_Mesh fileID 10202`.
- Prior production texture manifest contains a historical material-reference row for this prefab, but no current Unity visual acceptance, pressure-rated construction source, or screenshot proof exists.

Decision: `DELETE_FORBIDDEN_WITHOUT_UNITY_OWNER`.

Reason: a live asset GUID reference exists in a MapMagic graph under `Assets`. The path name `Old tries` suggests historical risk, but static text cannot prove it is inactive. A MapMagic/construction Unity owner must classify it before quarantine; if retained, it needs a pressure-rated construction/module source package.

## Default And Material Risk Scan

GUID: `31321ba15b8f8eb4c954353edc038b1d`

Static facts:

- `Assets/_Project/Prefabs/Item_Titanium.prefab` uses the GUID as renderer material.
- `Assets/_Project/Prefabs/STRUCTURES.prefab` uses the GUID as renderer material.
- `Assets/_Project/Prefabs/Tools/Held/Tool_Propulsion_Held.prefab` uses the GUID as renderer material.
- `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`, `PFB_MicroSub_Transport.prefab`, `PFB_Exosuit_Frame_Transport.prefab`, and `PFB_ScoutGlider_Transport.prefab` use the GUID as renderer material.
- `Assets/_Project/Prefabs/Player.prefab` has multiple renderer material slots using the GUID.
- `Assets/_Project/Data/UniversalRenderPipelineGlobalSettings.asset` uses the GUID as `m_DefaultMaterial`.
- `Assets/_Project/Scenes/_Temp/FloraBeautyAudit_TMP.unity` uses the GUID.
- The GUID resolves by meta lookup to `.codexbuild/ShallowsBakeProject_20260514_030549/Library/PackageCache/com.unity.render-pipelines.universal@580a03820d50/Runtime/Materials/Lit.mat.meta`.

Risk classification:

- Product-face target: Player, transport prefabs, `Tool_Propulsion_Held`, `Item_Titanium`, `STRUCTURES`.
- Legacy root: `Item_Titanium`, `STRUCTURES`.
- Report-only or historical artifact: prior Batch18 report mentions and archives.
- Project/default setting: URP global settings default material. It explains the GUID route; it is not product material proof.
- Temp scene artifact: `_Temp/FloraBeautyAudit_TMP.unity`, not production acceptance.

Decision: every retained product-face prefab using this GUID requires canonical project-owned material replacement. Package-cache `Lit.mat`, Unity default material, placeholder, trial, debug, runtime flat-color, checkerboard, and error cube routes are rejected as product-face material sources.

## Decision Matrix Summary

Detailed CSV: `Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv`

Rows: 12.

Legacy-root decisions:

- `Item_Titanium.prefab`: `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`; retained route requires canonical `TitaniumScrap` source and `Data_TitaniumScrap`/scan preservation.
- `STRUCTURES.prefab`: `QUARANTINE_CANDIDATE_PENDING_REFERENCE_PROOF`; retained route must not keep primitive child via `Item_Titanium`.
- `Buildings/Cube.prefab`: `DELETE_FORBIDDEN_WITHOUT_UNITY_OWNER`; GUID reference in `Assets/MapMagic/Map_Graph/Old tries/Terrain.asset` blocks static quarantine/deletion.

Highest retained risks:

1. `Buildings/Cube.prefab` has an asset GUID reference in a MapMagic graph. Static report cannot prove the graph is inactive.
2. `Item_Titanium.prefab` has active editor/bootstrap/validator references and a scannable route. Quarantine could break first-hour static setup if done without Unity proof.
3. `STRUCTURES.prefab` can leak a primitive `Item_Titanium` child and package/default material if any aggregate reference exists outside current static GUID hits.
4. `Tool_Propulsion_Held.prefab` uses package-cache URP `Lit.mat` and a built-in cube, so tool relink acceptance is blocked until project-owned source exists.
5. Player and transport prefabs also use the same default/package material GUID, so future relink proof must include default-material eradication, not only primitive mesh replacement.

## Future Unity-Owner Checklist

Preflight:

- Confirm uncontested Unity slot; do not run with another Unity import/build/profiler/DataMonolith owner active.
- Snapshot target prefabs and `.meta` state through VCS or an owned rollback folder approved by integrator policy.
- Run scoped reference scan by prefab GUID, path, file stem, likely object name, and scene object names:
  - `64bf1bfdf2fc079449f22ccd9187776e`
  - `fe67818ec2cf8f74684ab2f6ef6711ee`
  - `68067bbdb13059f4b80ecc380f14ce7d`
  - `Item_Titanium`
  - `STRUCTURES`
  - `Buildings/Cube`
  - `Cube`
- Separate active `Assets` references from docs, archives, task files, reports, generated logs, and temp scenes.

Backup/quarantine rules:

- No deletion from static evidence.
- Quarantine only after zero active production references are proven in the same Unity state.
- Move prefab and `.meta` together if a future owner performs quarantine.
- Preserve a rollback path and rerun static validators after quarantine.
- Do not quarantine `Buildings/Cube.prefab` until MapMagic/construction owner classifies `Assets/MapMagic/Map_Graph/Old tries/Terrain.asset`.

Retained-route rules:

- `Item_Titanium` can only inherit canonical `TitaniumScrap` mesh/material/data truth.
- `STRUCTURES` must not keep a primitive child via `Item_Titanium`; it must delegate to canonical child assets or leave production routes.
- `Buildings/Cube` must become a pressure-rated construction/module source if retained, not a raw cube.
- `Tool_Propulsion_Held` package `Lit.mat` route must be replaced by project-owned tool material source before acceptance.
- No retained product-face prefab may use `31321ba15b8f8eb4c954353edc038b1d`, placeholder, trial, debug, runtime flat-color, checkerboard, or `MAT_ErrorCube` material as final body material.

Proof reruns:

- Product-Face Prefab Quality Gate.
- `python Tools/GeneratedAssetProductionAudit.py --root . --fail-on-error`.
- Sky/Ocean primitive gate only if sky/ocean/default material work is touched.
- Static `rg` cross-check for the three legacy roots, their GUIDs, and `31321ba15b8f8eb4c954353edc038b1d`.
- `Import-Csv` parse check for any decision matrix generated by the owner.

Screenshot/proof requirements:

- If `Item_Titanium` remains player-visible: compact and normal-tier pickup screenshots, material close view, collider/proxy split proof, `Data_TitaniumScrap` and scan route preservation.
- If `STRUCTURES` remains player-visible or production-referenced: screenshot of aggregate route, canonical child proof, no primitive cube/default material.
- If `Buildings/Cube` remains production-referenced: pressure-rated module source screenshots at compact and high tiers, material/texture role proof, collision proxy proof, construction owner signoff.
- If `Tool_Propulsion_Held` remains player-facing: first-person held screenshot, world/held family proof, project-owned material role proof, profiler/Frame Debugger only if render/runtime path changes.

## Continuous Quality Consequences

This task changes no runtime or visuals.

For future retained routes:

- Compact: no ugly primitive/default fallback; preserve readable physical silhouette, material family, data identity, pickup/tool/building anchors, and cheap collider truth.
- Middle: add grime, labels, residue, seams, stronger normals, and longer LOD residency without changing item/building/tool truth.
- High: add wetness, bevels, fracture response, glass/metal/rubber richness, and stronger material transitions.
- Ultra: add micro chips, scratches, bolts, labels, droplets, secondary fittings, and richer masks only. Gameplay truth, item IDs, collision identity, save identity, DTO layout, and authority routes do not change.

## Verification Performed

Commands run after writing:

```powershell
git diff --check -- Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv Docs/Tasks/Status_1887.md Docs/AgentLogs/Rationale_1887.md Docs/AgentLogs/LOG_1887.md
```

```powershell
Import-Csv Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv | Measure-Object
```

```powershell
rg -n "Item_Titanium|STRUCTURES|Buildings/Cube|31321ba15b8f8eb4c954353edc038b1d|Lit\.mat|Data_TitaniumScrap|Tool_Propulsion_Held|QUARANTINE" Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_QUARANTINE_DECISION_PACKET.md Docs/Reports/Batch18/1887_PRODUCT_FACE_LEGACY_REFERENCE_MATRIX.csv Docs/Tasks/Status_1887.md Docs/AgentLogs/Rationale_1887.md Docs/AgentLogs/LOG_1887.md
```

Results are recorded in `Docs/AgentLogs/LOG_1887.md`.

Results:

- `git diff --check` -> PASS, no output.
- `Import-Csv ... | Measure-Object` -> PASS, `Count: 12`.
- Static term cross-check for `Item_Titanium`, `STRUCTURES`, `Buildings/Cube`, `31321ba15b8f8eb4c954353edc038b1d`, `Lit.mat`, `Data_TitaniumScrap`, `Tool_Propulsion_Held`, and `QUARANTINE` -> PASS, hits present in owned outputs.

## Result

What was wrong: legacy/default product-face roots and package/default material routes could poison future visual relink proof. The highest static risks are duplicate titanium truth, primitive aggregate leakage, MapMagic-referenced building cube, and package-cache/default `Lit.mat` on product-face prefabs.

What I did: produced a static decision packet and CSV matrix. The packet separates active source references, asset GUID references, report-only mentions, package/default material risks, and future Unity-owner actions.

In-game result: PENDING VERIFICATION. Unity, screenshots, PlayMode, profiler, import, bake, build, and DataMonolith were forbidden.

What was verified: static docs, prefab YAML, meta GUIDs, scoped `rg` reference scans, and CSV parse after final verification.
