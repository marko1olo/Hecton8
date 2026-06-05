# 1898 Construction Final Authoring Source-Risk Packet

Agent ID: 1898
Title: CONSTRUCTION_FINAL_AUTHORING_SOURCE_RISK_PACKET
Evidence class: STATIC_SOURCE, STATIC_DOC, STATIC_AUDIT_TEXT
Unity/import/build/PlayMode/profiler/screenshots/DataMonolith: NOT RUN
Runtime proof: PENDING UNITY
Mutation boundary: no source, assets, prefabs, scenes, `.meta`, binaries, generated meshes, DataMonolith, task files, or sibling outputs edited.

## Result

The 10 `Assets/_Project/Prefabs/Construction/Final` primitive blockers from 1855 remain blocked. This packet turns 1855 into an implementation route by naming source candidates, exact future source file scopes, blocked legacy routes, output folders, validator hooks, abort conditions, and proof requirements.

This packet does not unblock primitive authoring. `ConstructionBootstrapAuthoring.RebuildStarterConstructionKit` remains a fail-closed legacy route. `WreckagePrefabFactory` remains conditional on a real source set. `ScifiFacility` is a source library, not a direct replacement library.

## Authorities Read

- Root/project: `AGENTS.md`, `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `TASTE.md`, `quality.md`.
- Domain: `construction.md`, `world.md`, `3dmodel.md`, `3DMODEL_TEXTURES_MATERIALS.md`, `3DMODEL_HARD_SURFACE_MODULES.md`, `PROCEDURAL_ASSET_PIPELINE.md`.
- Batch evidence: `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`, `Docs/Reports/Batch18/1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`, `Docs/Reports/Batch18/1855_CONSTRUCTION_FINAL_MESH_REBUILD_PACKET.md`, `Docs/Reports/Batch18/1861_PRIMITIVE_FACTORY_SOURCE_GATES.md`.
- Mandates: `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`, `.agents-skills/TOOL_Procedural_Wreckage_Generator.txt`, `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`, `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`.
- Targeted source/data: `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs`, `Assets/_Project/Editor/Assembly/WreckagePrefabFactory.cs`, construction buildable/template data, targeted ScifiFacility model/prefab inventory.

All four requested mandate files were present.

## Static Evidence Summary

- 1851 reports `FINAL_PREFAB_BUILTIN_PRIMITIVE_MESH` on all 10 construction targets and `FAMILY_FINAL_READY_BUILTIN_PRIMITIVE_MESH` on 9 linked construction family variants. `PFB_SargassumCollapseChunk.prefab` is the unlinked direct production-path extra.
- 1855 confirms the same 10 construction blockers, valid/invalid candidate classes, downstream contracts, and proof gates. It is static-only.
- 1861 closes several primitive factory routes but does not repair current prefab or family asset debt.
- `ConstructionBootstrapAuthoring.cs` line 44 calls `WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveFinalAuthoring(...)` before creating primitive construction finals. Lines 78-128 and 1247-1740 still contain primitive prefab creation code behind that gate.
- `WreckagePrefabFactory.cs` defaults to `Assets/_Project/BakedGeometry/Wreckage` as source, `Assets/_Project/BakedGeometry/Wreckage/PrefabFactory1735` as generated mesh output, and `Assets/Prefabs/Environment/Wrecks` as output. The source folder currently has 0 non-meta files and the old output folder is missing.
- `Assets/ScifiFacility/Models` inventory: 282 FBX files. Structural: 142, props: 80, decals: 38, furniture: 22.
- `Assets/ScifiFacility/Prefabs` inventory: 255 prefabs. Structural: 117, props: 79, decals: 38, furniture: 21.
- ScifiFacility primitive-contaminated prefab evidence: `Assets/ScifiFacility/Prefabs/structural/rails+scaffolds+stairs/stairs_01.prefab` and `Assets/ScifiFacility/Prefabs/structural/walls/wall_01_4x3_door_02.prefab` contain Unity built-in Cube mesh refs.
- `SargassumGlobalDragManager.cs` has `CollapseChunkFallbackPrefabPath = "Assets/_Project/Prefabs/Construction/Final/PFB_SargassumCollapseChunk.prefab"` and calls `ValidateCollapseChunkPrefabAssignment()` from `OnValidate`. That is a relink risk until patched or proven fail-closed against primitive/unproven prefabs.

## Blocker Inventory

| Target prefab | Current defect | Production role | Required source route |
| --- | --- | --- | --- |
| `PFB_Debris_WreckField.prefab` | 12 built-in primitive mesh refs: 6 Cube, 3 Cylinder, 3 Capsule | Wreck-field carrier and salvage/perch silhouette | Real hull/debris/COL source set, then conditional `WreckagePrefabFactory` assembly or construction-specific assembler |
| `PFB_Debris_ScrapCluster.prefab` | 9 refs: 4 Cube, 2 Cylinder, 3 Capsule | Small salvage cluster | ScifiFacility props/tubes/technical details or real wreck debris source, then construction-specific non-primitive prefab |
| `PFB_Module_Pylon.prefab` | 1 Cylinder ref | Utility/power pylon, `Build_Utility_Pylon` | ScifiFacility structural columns/rails/tubes/technical details; custom socket/cable anchors |
| `PFB_Module_CurrentTurbine.prefab` | 1 Cylinder ref | Power generator, `Build_Current_Turbine`, `powerRating=18`, `powerPriority=15` | ScifiFacility tube/ring/structural/prop kit plus custom shroud/rotor/blade mesh |
| `PFB_Ruin_ClusterMedium.prefab` | 16 refs: 9 Cube, 3 Cylinder, 3 Capsule, 1 Quad | Medium abandoned module route landmark | ScifiFacility walls/floors/ceilings/trims/rails plus authored ruin breaks and HLOD |
| `PFB_Ruin_Megastructure.prefab` | 23 refs: 15 Cube, 2 Cylinder, 4 Capsule, 2 Quad | Large landmark | ScifiFacility structural kit plus custom ring/core/bridge/frame HLOD package |
| `PFB_Module_Foundation.prefab` | 9 Cube refs | Foundation buildable/ruin module, sockets/interior trigger | ScifiFacility floor/border/trim/wall kit plus pressure deck mesh preserving template bounds |
| `PFB_Module_Corridor.prefab` | 9 Cube refs | Corridor buildable/ruin module, sockets/interior trigger | ScifiFacility wall/ceiling/floor/trim kit plus rounded pressure shell |
| `PFB_Module_ServicePump.prefab` | 1 Cube ref | Service scar pump, `Build_Service_Pump`, `powerRating=-8`, `powerPriority=20` | ScifiFacility props, tubes, control panels, technical details; custom intake/outflow body |
| `PFB_SargassumCollapseChunk.prefab` | 1 Cube ref in `Construction/Final`; no scanned family link | Collapse/debris chunk or wrong-owner support prefab | First classify ownership; either remove from `Construction/Final` in a separate scoped asset task or rebuild as non-primitive collapse chunk |

## Exact Future Source Scopes

Allowed future implementation source files:

- `Assets/_Project/Scripts/Editor/ConstructionFinalSourceSet.cs`
- `Assets/_Project/Scripts/Editor/ConstructionFinalPrefabAssembler.cs`
- `Assets/_Project/Scripts/Editor/ConstructionFinalPrefabValidator.cs`
- `Assets/_Project/Scripts/Editor/ConstructionFinalProofWriter.cs`
- `Assets/_Project/Scripts/Editor/WorldProceduralFinalPrefabQualityGate.cs` only for adding validator checks, never for weakening gates.
- `Assets/_Project/Editor/Assembly/WreckagePrefabFactory.cs` only for wreck/debris source-set validation or output-route alignment after real source files exist.
- `Assets/_Project/Scripts/World/SargassumGlobalDragManager.cs` only for a future guard preventing `PFB_SargassumCollapseChunk.prefab` fallback assignment when that prefab is primitive or production-unproven.

Blocked or fail-closed future source files:

- `Assets/_Project/Scripts/Editor/ConstructionBootstrapAuthoring.cs`: read-only contract reference unless a task explicitly removes primitive final authoring or adds a non-primitive call path. It must not loosen `AllowLegacyPrimitiveFinalAuthoring`.
- `Assets/_Project/Scripts/Editor/WorldProceduralInteriorColonyFinalAuthoring.cs`: keep 1861 fail-closed guard.
- `Assets/_Project/Scripts/Editor/WorldProceduralPlaceholderAuthoring.cs`: keep proxy-only/non-final placeholder behavior.
- `Assets/_Project/Scripts/Editor/ResourceWorldBootstrapAuthoring.cs`: keep production primitive pickup authoring blocked.
- `Assets/_Project/Scripts/Editor/ResourceDistributionBootstrapAuthoring.cs`: keep runtime ore/vent primitive fallback blocked.
- `Assets/_Project/Editor/Assembly/PowerGridPrefabFactory.cs`: do not use as a pylon/turbine shortcut; keep analytic primitive fallbacks blocked.

The future implementation must not touch source outside the allowed list unless a new task updates this packet with a narrower proof reason.

## ScifiFacility Source Policy

Use `Assets/ScifiFacility/Models` as source mesh inventory first, not `Assets/ScifiFacility/Prefabs` as direct final replacements.

Allowed ScifiFacility model classes:

- `Models/structural/walls`, `ceiling`, `floor`, `trims`: corridor/foundation/ruin shell, panel, gasket, flange, deck, and socket frame source.
- `Models/structural/rails+scaffolds+stairs`: megastructure frames, exposed ruin frames, pylon brackets, maintenance platforms. Require primitive contamination check before prefab use.
- `Models/props/details/controlpanels`, `props/details/pipes`, `props/details/technical`, `props/lights`, `props/server_racks`, `props/tubes`: service pump detail, turbine cable/hub support, pylon sockets, salvage affordances, interior dressing.
- `Models/decals`: overlay source only after render queue/material proof. Transparent decal spam is not mesh quality proof.

Direct unsafe prefab drop-in is blocked because ScifiFacility prefabs do not preserve HECTON construction sockets, `BaseModuleTemplate` proxy bounds, collision truth, `InteriorTrigger`, power metadata, family variant identity, LOD/HLOD policy, or proof reports. Prefabs may be opened as reference packaging only after a primitive scan and material/SRP proof.

## WreckagePrefabFactory Conditional Use

`WreckagePrefabFactory` can be used for `PFB_Debris_WreckField` and possibly `PFB_Debris_ScrapCluster` only after a real source set exists.

Minimum source set required before use:

- Source folder with non-meta hull meshes or prefabs.
- Debris segments named so `IsDebrisName` classifies them as debris.
- Collision proxy source named `COL_*`, with no renderers and only BoxCollider/CapsuleCollider/convex MeshCollider.
- Required wreck material set: exterior, burned/interior, debris/scrap, all SRP Batcher candidates.
- No Unity built-in primitive visible mesh refs in source.

Abort if `Assets/_Project/BakedGeometry/Wreckage` is still empty or if the implementation tries to use `Assets/Prefabs/Environment/Wrecks` as proof before output exists.

## ConstructionBootstrapAuthoring Block

`ConstructionBootstrapAuthoring.RebuildStarterConstructionKit` is legacy primitive authoring. It may identify old object names and contracts only. It must not be used to rebuild production finals.

Fail-closed requirements:

- Keep `AllowLegacyPrimitiveFinalAuthoring(nameof(ConstructionBootstrapAuthoring), FinalPrefabFolder)` in front of all primitive final writes.
- Do not call `CreateFinalPrefab`, `CreateCompositeFinalPrefab`, `BuildFinalVisuals`, `BuildCompositeVisuals`, or `CreateVisualPrimitive` for production final art.
- Do not treat `GameObject.CreatePrimitive` children as acceptable visible art.
- Collision primitives are allowed only as named `COL_*` collider children in the future replacement prefabs.

## Future Output Routes

Preferred in-place prefab route for GUID safety:

- Existing prefab paths: `Assets/_Project/Prefabs/Construction/Final/PFB_*.prefab`
- Meshes: `Assets/_Project/Art/Meshes/Construction/Final/MESH_Construction_<Package>_<Variant>_LOD0.asset`, `_LOD1.asset`, `_LOD2.asset`
- HLOD: `Assets/_Project/Art/Meshes/Construction/HLOD/MESH_Construction_<Package>_<Variant>_HLOD.asset`
- Materials: `Assets/_Project/Art/Materials/Construction/MAT_Construction_<Role>.mat`
- Textures: `Assets/_Project/Art/Textures/Construction/TX_Construction_<Atlas>_<Role>.png`
- Proof: `Docs/Reports/GeneratedAssets/Construction/<PrefabName>_PROOF.md`
- Optional manifest: `Assets/_Project/Data/GeneratedAssets/Construction/MANIFEST_Construction_<Package>.asset`

Current static folder reality:

- `Assets/_Project/BakedGeometry/Wreckage`: present, 0 non-meta files.
- `Assets/Prefabs/Environment/Wrecks`: missing.
- `Assets/_Project/Art/Meshes/Construction`: missing.
- `Assets/_Project/Art/Textures/Construction`: missing.
- `Docs/Reports/GeneratedAssets/Construction`: missing.

Future mutating implementation must create missing output folders only inside its scoped task and must not write temporary proof or logs into `Assets`.

## Relink Versus In-Place Strategy

Preferred strategy: replace internals at the existing `Assets/_Project/Prefabs/Construction/Final/PFB_*` paths. This preserves prefab GUIDs and reduces risk to `ProceduralFamily`, `BuildableData`, catalog, placement, and save/load references.

Relink strategy is allowed only when in-place replacement is impossible. A relink task must update and prove every affected:

- `ProceduralFamily_*` variant reference.
- `BuildableData` final prefab reference and `stableId`/persistent identity.
- Construction catalog alias or recipe reference.
- Save/load identity surface using prefab path, GUID, stable ID, or variant ID.
- Scene/placement reference if any exists.

GUID risk: replacing a prefab file outright or deleting/recreating it can break serialized object references even if the path remains identical. Future implementation must preserve `.meta` and use PrefabUtility edits on the existing prefab asset when possible. If a new prefab is required, it needs a complete relink report before replacing any family/buildable link.

## Contracts To Preserve

Buildable contracts:

- `Build_Corridor_Straight`: preserve `stableId=Build_Corridor_Straight`, `powerRating=-6`, `powerPriority=35`, final prefab identity.
- `Build_Foundation_Platform`: preserve `stableId=Build_Foundation_Platform`, `powerRating=0`, `powerPriority=25`.
- `Build_Current_Turbine`: preserve `stableId=Build_Current_Turbine`, `powerRating=18`, `powerPriority=15`.
- `Build_Service_Pump`: preserve `stableId=Build_Service_Pump`, `powerRating=-8`, `powerPriority=20`.
- `Build_Utility_Pylon`: preserve `stableId=Build_Utility_Pylon`, `powerRating=0`, `powerPriority=40`.

Template and module contracts:

- `BaseModuleTemplate` socket definitions, proxy bounds, structural role flags, air volume, flood/integrity thresholds, breach area, dry mass, buoyancy displacement, center-of-mass shift, and VFX sockets.
- `Socket_*` child names and transform positions for corridor/foundation style modules.
- `InteriorTrigger` child and `BoxCollider.isTrigger=true` assignment for corridor/foundation style modules.
- `ModuleMarker`, `BaseModule`, `ModuleSocket`, `PowerNode`, power metadata, placement footprint, collision layer, static flags, and family variant identity.

Runtime truth must not move to source-generated scripts. Final runtime prefabs consume static authored assets, shared materials, LOD/HLOD, simple collision, sockets, and metadata.

## Validator Hooks

Future implementation must use or add these hooks:

- `WorldProceduralFinalPrefabQualityGate.UsesUnityBuiltInPrimitiveMesh` or stricter equivalent: visible MeshFilter primitive scan.
- `ConstructionFinalPrefabValidator`: new static validator for source provenance, visible primitive ban, source folder whitelist, material slot count, shared material proof, `renderer.material` ban, LOD/HLOD presence, collision proxy naming, socket/interior/power metadata preservation, and proof report presence.
- `WreckagePrefabFactory.ValidatePrefabContract`: allowed for wreck/debris packages after real source exists; must be extended if construction output needs LOD0/1/2/HLOD rather than one-step merged LOD.
- `Tools/GeneratedAssetProductionAudit.py`: future static audit proof after prefab mutation, not run by 1898.
- `git diff --check`: owned-file hygiene only for this packet.

## Red Gates And Abort Conditions

Abort future implementation if any condition occurs:

- Source set is missing, empty, all `.meta`, or derived from `WorldProceduralProxy`, `WorldRuntime/ProceduralPlaceholders`, resource pickups, current primitive finals, hidden support markers, or scanner/perch/school proxies.
- Any visible MeshFilter keeps Unity built-in primitive mesh GUID `0000000000000000e000000000000000`.
- Any ScifiFacility prefab is dropped directly into `Construction/Final` without construction-specific sockets, collision, materials, LOD/HLOD, metadata, and proof.
- `ConstructionBootstrapAuthoring` primitive gate is loosened, bypassed, or used to regenerate production finals.
- `WreckagePrefabFactory` is run with empty `Assets/_Project/BakedGeometry/Wreckage` or without `COL_*` proxy source.
- `SargassumGlobalDragManager` can assign primitive/unproven `PFB_SargassumCollapseChunk.prefab` through `OnValidate`.
- Buildable `stableId`, `powerRating`, `powerPriority`, template sockets, proxy bounds, `InteriorTrigger`, `PowerNode`, family variant ID, or save identity changes without a separate design/relink task.
- Runtime generation, runtime material clone, runtime texture generation, runtime collider cooking, or hot scene search is introduced.
- No proof report, no primitive scan, no LOD/HLOD proof, no material/SRP proof, no collision proof, or no screenshot/render proof exists.

Rollback for future mutating task:

- Stop immediately before saving further prefabs.
- Restore only files touched by that task from the task-local backup or VCS patch context. Do not revert unrelated agents.
- Keep existing prefab GUIDs if rollback is path-preserving.
- Record failed target, source paths, validator failure, and first bad artifact.
- Leave primitive finals blocked. Do not "fix forward" by relinking to proxies.

## Scalability Consequences

- Compact: shared construction atlases, strict material slot count, simple `COL_*` collision, early LOD2/HLOD, silhouette/sockets/flanges/readable paneling preserved. No flat primitive art.
- Middle: longer LOD1, more material breakup, limited decals, stable sockets/colliders unchanged.
- High: richer bevels, wetness, corrosion, cable/pipe detail, longer LOD0 range, optional visual-only turbine motion after proof.
- Ultra: denser bolts, trims, decals, secondary cables, stronger ruin interior breakup, extended HLOD range. Gameplay truth, save identity, DTO layout, and authority route unchanged.

## Verification State

1898 created this as static source-risk documentation only. Unity import, prefab mutation, screenshots, profiler, PlayMode, build, DataMonolith, and player proof are explicitly PENDING UNITY.

Verification executed:

- `git diff --check -- Docs/Reports/Batch18/1898_CONSTRUCTION_FINAL_AUTHORING_SOURCE_RISK_PACKET.md Docs/Reports/Batch18/1898_CONSTRUCTION_FINAL_AUTHORING_SEQUENCE.csv Docs/Tasks/Status_1898.md Docs/AgentLogs/Rationale_1898.md Docs/AgentLogs/LOG_1898.md`: PASS, no output.
- `Import-Csv Docs/Reports/Batch18/1898_CONSTRUCTION_FINAL_AUTHORING_SEQUENCE.csv | Measure-Object`: Count 10.
- Static term cross-check: PASS for `PFB_Module_Corridor`, `PFB_Module_Foundation`, `PFB_Ruin_Megastructure`, `ScifiFacility`, `ConstructionBootstrapAuthoring`, `WreckagePrefabFactory`, `PENDING UNITY`.
