# 1897 Titanium Canonical Resource Route Decision Packet

Agent: 1897  
Mode: static report only  
Unity: not run  
Source/assets/prefabs/scenes/meta/binaries/task files: not edited  
Evidence class: text/YAML/static asset inspection only

## Authority Scope

Read:
- `AGENTS.md`
- `VISION_LOCKS.md`
- `PROJECT_BIBLES.md`
- `quality.md`
- `inventory.md`
- `TASTE.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `taskslocal/batch18_night_orchestration/1897_TITANIUM_CANONICAL_RESOURCE_ROUTE_DECISION_PACKET.txt`
- `.agents-skills/QA_Evidence_Text_Filter_Audit.txt`
- `.agents-skills/DATA_Inventory_Resources_Items_SOA_Layout.txt`
- Prior Batch18 packets: 1814, 1826, 1870, 1875, 1881, 1885, 1887, 1888, 1890

Missing:
- `resources.md` was requested by task but was not present at the project root.
- `Docs/Actual Domains of Project.txt` existed but returned no static content in this pass.

## Decision

Canonical runtime item identity for the titanium pickup route is `Data_TitaniumScrap`, GUID `e5817766c5653214f8db9a6161026a98`. It is the cataloged inventory item, the first-hour quest item, the structural recipe ingredient, and the resource-node/harvest yield.

Canonical pickup prefab route is `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`, GUID `cc05a4753de6c1f48b51d49126443a9c`, because `Data_TitaniumScrap.asset` points to it as `worldPrefab`. Current visual proof rejects the present prefab as final product face: it is a built-in cube mesh using `Mat_Resource_Scrap`, and that material has flat color with empty texture slots.

`Assets/_Project/Prefabs/Item_Titanium.prefab`, GUID `64bf1bfdf2fc079449f22ccd9187776e`, is not a separate item identity and must not become `Data_Titanium`. Static proof shows it also points to `Data_TitaniumScrap` and carries the scanner entry `resource.titanium_fragment`. It is a legacy compatibility/scanner alias candidate. Immediate deletion or quarantine is blocked by editor/bootstrap/scanner references until the future owner proves those routes are migrated or dead.

There is no active `Data_Titanium.asset` or `stableId: Data_Titanium` asset found in the scoped static search. The exact source reference `Assets/_Project/Scripts/FieldToolRuntimeSmokeTester.cs` still attempts to load `Assets/_Project/Data/Items/Data_Titanium.asset`; that is a stale compatibility/test risk, not canonical data.

## Route Classification

Accepted canonical route:
- Item/data: `Data_TitaniumScrap`
- Inventory hash route: `Data_TitaniumScrap`
- Pickup prefab route: `PFB_Resource_TitaniumScrap`
- Craft/quest/repair route: consume or observe `Data_TitaniumScrap`
- Scanner fragment route: preserve `resource.titanium_fragment` only as scan/intel presentation, not item identity

Rejected as canonical identity:
- `Data_Titanium`
- `Item_Titanium` as an independent item
- Generic "Titanium" as a runtime item stableId
- Default/package material GUID `31321ba15b8f8eb4c954353edc038b1d`
- Built-in primitive visible cube as final pickup art
- Flat `Mat_Resource_Scrap` with empty texture slots as final product-face material

Allowed future alias behavior:
- Keep `Item_Titanium.prefab` only as a compatibility alias while it references `Data_TitaniumScrap`.
- If retained, replace its visible mesh/material route with the same canonical TitaniumScrap visual/material truth as `PFB_Resource_TitaniumScrap`.
- Move the scanner route onto the canonical pickup or a dedicated scan proxy before deleting or quarantining the legacy prefab.
- Preserve save/quest/catalog identity by keeping `Data_TitaniumScrap` stable.

Forbidden future behavior:
- Creating a parallel `Data_Titanium` item without a migration owner, explicit save compatibility proof, quest/craft scanner migration, and DataMonolith route card.
- Treating titanium ore/material class as the same thing as the inventory pickup item.
- Letting editor bootstrap convenience prefabs define canonical runtime identity.
- Reusing titanium scrap pickup prefab as an unrelated CarbonGraphite world prefab without an explicit shared-proxy decision.

## Static Evidence

`Data_TitaniumScrap.asset`:
- Path: `Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset`
- GUID: `e5817766c5653214f8db9a6161026a98`
- `stableId: Data_TitaniumScrap`
- `legacyItemName: Titanium Scrap`
- `weight: 1.2`
- `maxStack: 32`
- `category: 1`
- `resourceFamily: 1`
- `progressionTier: 1`
- `isRawResource: 1`
- `worldPrefab` GUID: `cc05a4753de6c1f48b51d49126443a9c`

`PFB_Resource_TitaniumScrap.prefab`:
- Path: `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`
- GUID: `cc05a4753de6c1f48b51d49126443a9c`
- Uses built-in primitive cube mesh.
- Uses material GUID `1ab6d90547f252c41bb386cefc8d12c2` (`Mat_Resource_Scrap`).
- `PickupItem.itemData` points to `Data_TitaniumScrap`.
- Current state is canonical route holder, not final visual proof.

`Item_Titanium.prefab`:
- Path: `Assets/_Project/Prefabs/Item_Titanium.prefab`
- GUID: `64bf1bfdf2fc079449f22ccd9187776e`
- Uses built-in primitive cube mesh.
- Uses default/package material GUID `31321ba15b8f8eb4c954353edc038b1d`.
- `PickupItem.itemData` points to `Data_TitaniumScrap`.
- Has scanner entry `resource.titanium_fragment`, title `TITANIUM FRAGMENT`.
- Static references in bootstrap and scanner validation block blind deletion.

`Mat_Resource_Scrap.mat`:
- Path: `Assets/_Project/Art/Materials/Resources/Mat_Resource_Scrap.mat`
- GUID: `1ab6d90547f252c41bb386cefc8d12c2`
- URP Lit shader route.
- `_BaseColor` is flat gray-blue.
- `_BaseMap`, `_BumpMap`, `_MetallicGlossMap`, `_OcclusionMap`, `_EmissionMap`, and detail texture slots are empty.
- It is a placeholder material, not a product-face material packet.

Quest/craft/scanner evidence:
- `ItemCatalog.asset` contains `Data_TitaniumScrap`.
- `Quest_FirstHour_CollectTitanium.asset` completes on `Data_TitaniumScrap`.
- `Quest_FirstHour_CraftScanner.asset` triggers on `Data_TitaniumScrap`.
- `Recipe_StructuralBracket.asset` consumes `Data_TitaniumScrap`.
- `Recipe_ReinforcedPlate.asset` consumes `Data_TitaniumScrap`.
- `Recipe_Scanner.asset` does not consume TitaniumScrap in static data found.
- `ScanIntelValidator.cs` references the scene path `--- GAMEPLAY ---/Item_Titanium` and only requires `ScannableTarget`.

Ore/material evidence:
- `VoxelDeltaProcessor.cs` distinguishes `TitaniumOreHash` from emitted `TitaniumScrapItemHash`.
- `ProceduralOreSpawner.cs` uses `WorldOreTypeIds.Titanium` and a serialized titanium item hash for ore yields.
- `ResourceNodeTemplate_TitaniumBasaltMass.asset`, `ResourceNodeTemplate_TitaniumScrap.asset`, and `HarvestableTemplate_TitaniumOutcrop.asset` all yield `Data_TitaniumScrap`.
- Therefore ore/material class can remain titanium while inventory pickup item remains TitaniumScrap.

Compatibility risk:
- `ConstructionBootstrapAuthoring.cs` has `TitaniumPrefabPath = "Assets/_Project/Prefabs/Item_Titanium.prefab"` and spawns it across trial/scanner/endgame editor bootstrap routes.
- `STRUCTURES.prefab` contains an `Item_Titanium` child per prior packet 1887.
- `FieldToolRuntimeSmokeTester.cs` has a stale load path for missing `Data_Titanium.asset`.
- `Data_CarbonGraphite.asset` references the `PFB_Resource_TitaniumScrap` prefab GUID. That is cross-resource contamination unless a future owner writes a shared-proxy decision.

## DataMonolith Implication

`Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists, but this packet makes no runtime claim about its contents. Static source folders checked in this pass did not show `Data_TitaniumScrap` or `Data_Titanium` rows in `Assets/_SourceData/DataMonolith` or `Data/Balance/Items.csv`.

Future DataMonolith owner actions:
- Add or verify a canonical static item row for `Data_TitaniumScrap` with stable hash, stack, category, family, progression tier, raw-resource flag, and any approved pickup proxy/material/template indices.
- Add or verify recipe and resource-node references if the DataMonolith schema owns those runtime facts.
- Reject `Data_Titanium` as an alias unless there is an explicit migration row and save compatibility plan.
- Keep Unity prefab paths, authoring texture manifests, mesh source provenance, and scanner text as source/editor authoring data unless the runtime schema explicitly owns numeric proxy records for them.
- Run import/bake/boot validation after source data changes. This agent did not run Unity or bake the binary.

## Product-Face Requirements For Future Owner

Canonical TitaniumScrap visual:
- Manufactured salvage shard, not natural ore.
- Bent/cut titanium plate or hull fragment.
- Torn edges, bolt holes, paint remnants, stamped/printed markings, scratch/bend normals, salt and oil grime.
- Distinct from copper, lithium, generic scrap, and natural rocks.

Mesh route:
- Project-owned mesh asset, not built-in primitive visible cube.
- `VIS_`/`COL_` split.
- Simple pickup collider/trigger; no LOD0 visual MeshCollider as runtime collider.
- LOD0/LOD1/LOD2/HLOD or equivalent route.

Material route:
- Project-owned material.
- Albedo, normal, and declared packed mask.
- Packed mask channel semantics must be owned by the material/texture packet, either approved MRAO or ToolDecayLit route.
- No default/package `Lit.mat`.
- No flat-color-only placeholder as product-face pass.

Validator route:
- Fail built-in primitive visible mesh on canonical titanium scrap pickup.
- Fail default/package material GUID `31321ba15b8f8eb4c954353edc038b1d`.
- Fail `Mat_Resource_Scrap` if texture slots stay empty.
- Fail `Data_Titanium` runtime item resurrection without migration proof.
- Flag CarbonGraphite if it continues to use the titanium scrap pickup prefab without explicit shared-proxy documentation.
- Preserve or migrate scanner entry `resource.titanium_fragment`.

## Scalability Consequences

Low:
- Use simplified shard mesh, coarse collider, small texture variant, and infrequent optional sparkle/highlight cadence.
- Identity remains `Data_TitaniumScrap`; gameplay yield remains unchanged.

Middle:
- Use normal-mapped scrap mesh with correct silhouettes and material masks.
- Use stable pickup prefab route and scanner alias only where still required.

High:
- Use richer edge wear, decals/labels, stronger normal detail, and LOD selection that keeps pickup recognizable at distance.
- No new item IDs and no gameplay truth changes.

Ultra:
- Use higher texture resolution, optional micro-surface detail, premium scan/pickup presentation, and additional non-gameplay visual passes.
- Ultra may increase visual fidelity only; it must not alter inventory identity, quest gates, recipe requirements, DTO layout, save identity, or DataMonolith authority.

## Final Decision Table

| Route | Decision |
| --- | --- |
| `Data_TitaniumScrap` | Canonical item identity. |
| `PFB_Resource_TitaniumScrap` | Canonical pickup route holder; current visual/material state rejected as final. |
| `Item_Titanium` | Legacy compatibility/scanner alias candidate; retain only until references are migrated or proven dead. |
| `Data_Titanium` | Rejected as current canonical item; stale/missing reference only. |
| Titanium ore/material class | Allowed as material/voxel/ore source; must emit or map to `Data_TitaniumScrap` for inventory pickup unless a separate owner creates a migration. |
| `Mat_Resource_Scrap` | Placeholder shared scrap material; not final product-face proof. |
| DataMonolith | Needs future owner bake/validation for canonical item/static references; no runtime proof claimed here. |

## Required Future Owner Actions

1. Data owner: remove or migrate the stale `Data_Titanium.asset` load path in `FieldToolRuntimeSmokeTester.cs`; do not create `Data_Titanium` unless a migration packet owns save, quest, craft, scanner, and DataMonolith compatibility.
2. Prefab owner: convert `PFB_Resource_TitaniumScrap` to the canonical project-owned scrap mesh/material route while preserving `Data_TitaniumScrap` item data.
3. Legacy prefab owner: migrate `ConstructionBootstrapAuthoring.cs`, `ScanIntelValidator.cs`, scene references, and `STRUCTURES.prefab` away from `Item_Titanium` or keep it as an explicit alias using canonical TitaniumScrap visuals.
4. Material/texture owner: replace flat `Mat_Resource_Scrap` placeholder with approved albedo, normal, and packed mask route or assign a new canonical titanium scrap material.
5. DataMonolith owner: bake and validate `Data_TitaniumScrap` as the canonical static item/reference route; keep authoring-only prefab/material provenance out of runtime binary unless represented by approved numeric proxies.
6. Validation owner: extend product-face validators to fail primitive mesh, default/package materials, missing texture channels, stale `Data_Titanium`, and cross-resource prefab contamination.

## Verification

Final command pass:
- `git diff --check -- Docs/Reports/Batch18/1897_TITANIUM_CANONICAL_RESOURCE_ROUTE_DECISION_PACKET.md Docs/Reports/Batch18/1897_TITANIUM_CANONICAL_RESOURCE_ROUTE_MATRIX.csv Docs/Tasks/Status_1897.md Docs/AgentLogs/Rationale_1897.md Docs/AgentLogs/LOG_1897.md`: PASS, no output.
- `Import-Csv Docs/Reports/Batch18/1897_TITANIUM_CANONICAL_RESOURCE_ROUTE_MATRIX.csv | Measure-Object`: PASS, `Count: 24`.
- Static bounded term cross-check outside the new 1897 files:
  - `Item_Titanium`: 198
  - `TitaniumScrap`: 87
  - `Data_Titanium`: 3
  - `Data_TitaniumScrap`: 360
  - `Mat_Resource_Scrap`: 68
  - `DataMonolith`: 5199
  - `scanner`: 95942
  - `craft`: 1784
