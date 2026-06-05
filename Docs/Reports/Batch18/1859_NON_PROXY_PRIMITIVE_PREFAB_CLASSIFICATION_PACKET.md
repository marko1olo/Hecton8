# 1859 Non-Proxy Primitive Prefab Classification Packet

Date: 2026-06-04
Evidence class: STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

Classified non-`WorldProceduralProxy` prefabs under `Assets/_Project/Prefabs` that reference Unity built-in primitive mesh GUID `0000000000000000e000000000000000`.

No prefab, asset, source, scene, binary, or `.meta` file was edited. This packet only writes the owned 1859 status/log/rationale/report/matrix outputs.

First-20-minutes impact: removes a visible-art blocker for the semi-open first exit and first route. Player body, sky, ocean support surfaces, held tools, resources, and transport stand-ins would contaminate the surface/shallow visual floor if instantiated as-is.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `gameplay.md`
- `tools.md`
- `inventory.md`
- `vehicles.md`
- `world.md`
- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`

Relevant mandates loaded: `QA_Evidence_Text_Filter_Audit`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, `STRM_Asset_Lifecycle_Addressables_Loading_Memory`, `CORE_Tools_Equipment_Interaction_Raycast_Heat`, `DATA_Inventory_Resources_Items_SOA_Layout`, `CORE_Submarine_Vehicles_Kinematics_AUP`, `REND_URP_Graphics_HotPath_Optimization_HLOD`, `TOOL_Procedural_Wreckage_Generator`.

## Static Method

- Used existing audit constant from `Tools/GeneratedAssetProductionAudit.py`: `BUILTIN_PRIMITIVE_GUID = "0000000000000000e000000000000000"`.
- Static file count command class: recursive prefab text search under `Assets/_Project/Prefabs`.
- Parsed representative YAML for MeshFilter names, GameObject active flags, and MeshRenderer enabled/material references.
- Did not run Unity, importer, build, bake, PlayMode, screenshot, or DataMonolith tooling.

## Count Confirmation

| Bucket | Prefab files |
|---|---:|
| Total primitive-prefab files under `Assets/_Project/Prefabs` | 183 |
| `WorldProceduralProxy` primitive-prefab files | 88 |
| Non-proxy primitive-prefab files | 95 |
| Non-proxy production `Final` primitive-prefab files | 21 |

These match the task packet counts.

## Non-Proxy Folder Split

| Category | Files |
|---|---:|
| `WorldRuntime` | 30 |
| `Items` | 12 |
| `Tools` | 12 |
| `Construction` | 10 |
| `WorldSupport` | 9 |
| `Resources` | 8 |
| `Transport` | 4 |
| `Nature` | 2 |
| `Buildings` | 1 |
| `Diagnostics` | 1 |
| `Player.prefab` | 1 |
| `Ocean_Crest.prefab` | 1 |
| `Sky_System.prefab` | 1 |
| `Directional Light.prefab` | 1 |
| `Item_Titanium.prefab` | 1 |
| `STRUCTURES.prefab` | 1 |

## Severity Classes

- `BLOCKER`: active visible production primitive not already covered by 1851-1858 and likely product-facing.
- `BLOCKER_COVERED`: active visible production `Final` primitive already covered by 1851/1853 replacement evidence.
- `HIGH`: active visible player/tool/item/resource/vehicle/sky/ocean primitive risk. Needs replacement or visual/runtime proof.
- `MEDIUM`: active primitive with unclear production wiring but plausible gameplay or route use.
- `LOW`: static primitive present but disabled, hidden, or not replacement priority without runtime proof.
- `DEV_ONLY`: diagnostic/dev/placeholder bucket. Allowed only with quarantine/reference proof.
- `UNKNOWN`: source-only evidence cannot determine scene use or production intent.

## Classification Result

### Already Covered Production Final Blockers

21 non-proxy production `Final` prefabs remain real blockers, but are not replanned here. They are covered by:

- `Docs/Reports/Batch18/1851_GENERATED_ASSET_PRODUCTION_AUDIT.md`
- `Docs/Reports/Batch18/1853_PRIMITIVE_FINAL_REPLACEMENT_PLAN.md`

This includes 10 `Construction/Final`, 9 `WorldSupport/Final`, and 2 `Nature/OrganicMisc/Final` prefab files. They keep `BLOCKER_COVERED` in the CSV matrix.

### Real Next Visual Blockers

Static source shows active primitive MeshFilters and enabled renderers in product-face classes:

- `Player.prefab`: 16 enabled primitive body renderers plus a disabled primitive visor.
- `Tools/Held`: 12 held tool bodies using active cube primitive renderers.
- `Items/Tools`: 12 world pickup tool prefabs using active cube primitive renderers.
- `Resources/Pickups`: 8 visible resource pickup stand-ins using cube/sphere/plane primitives.
- `Transport`: 4 visible transport bodies using active cube primitive renderers.
- `Sky_System.prefab`: active enabled sky sphere primitive renderer.
- `Ocean_Crest.prefab`: enabled primitive plane renderers on sargassum/ocean input surfaces.
- `Item_Titanium.prefab`: active visible item cube.

These are not collider-only by static evidence. They need replacement meshes or a scoped proof showing they are hidden/non-production.

### Dev/Proxy Buckets

- `WorldProceduralProxy`: 88 primitive prefab files excluded from primary queue by task scope. Dev/proxy risk only.
- `WorldRuntime/ProceduralPlaceholders`: 30 non-proxy primitive prefab files. Every scanned file contains `WorldProceduralPlaceholderMarker`, so they are classified `DEV_ONLY`, not production-safe. They require quarantine/reference proof before being excluded from future hard gates.
- `Diagnostics/PFB_ErrorCube.prefab`: `DEV_ONLY` if kept as diagnostics/error fallback only.

### Root Loose / Ambiguous

- `Directional Light.prefab`: primitive `Sun_Body` sphere exists but MeshRenderer is disabled in YAML. Severity `LOW`; runtime enabling remains unproven.
- `Buildings/Cube.prefab`: active cube in `Buildings`. Severity `UNKNOWN`; prove unused/dev or replace with real building module.
- `STRUCTURES.prefab`: root aggregate contains active `Item_Titanium` primitive child. Severity `UNKNOWN`; likely legacy aggregate, needs owner decision.

## Top 20 Next Replacement Queue

After the 21 covered `Final` blockers, prioritize visible product-face primitives:

1. `Assets/_Project/Prefabs/Player.prefab`
2. `Assets/_Project/Prefabs/Sky_System.prefab`
3. `Assets/_Project/Prefabs/Ocean_Crest.prefab`
4. `Assets/_Project/Prefabs/Tools/Held/Tool_Scanner_Held.prefab`
5. `Assets/_Project/Prefabs/Tools/Held/Tool_Flashlight_Held.prefab`
6. `Assets/_Project/Prefabs/Tools/Held/Tool_Builder_Held.prefab`
7. `Assets/_Project/Prefabs/Tools/Held/Tool_LaserCutter_Held.prefab`
8. `Assets/_Project/Prefabs/Tools/Held/Tool_Repair_Held.prefab`
9. `Assets/_Project/Prefabs/Tools/Held/Tool_Knife_Held.prefab`
10. `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_TitaniumScrap.prefab`
11. `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_CopperOre.prefab`
12. `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilverOre.prefab`
13. `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_SilicaShards.prefab`
14. `Assets/_Project/Prefabs/Resources/Pickups/PFB_Resource_FiberKelp.prefab`
15. `Assets/_Project/Prefabs/Transport/PFB_MicroSub_Transport.prefab`
16. `Assets/_Project/Prefabs/Transport/PFB_Exosuit_Frame_Transport.prefab`
17. `Assets/_Project/Prefabs/Transport/PFB_ScoutGlider_Transport.prefab`
18. `Assets/_Project/Prefabs/Transport/PFB_CargoSled_Transport.prefab`
19. `Assets/_Project/Prefabs/Item_Titanium.prefab`
20. `Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab`

Tool replacements should be authored as shared tool families so held and world pickup variants stop diverging.

## Replacement Strategy By Class

| Class | Strategy |
|---|---|
| `BLOCKER_COVERED` | Follow 1851/1853 replacement plans; do not duplicate here. |
| `HIGH` player/tools/vehicles | Real mesh authoring with bevels, materials, LODs, collision/proxy split, and screenshot proof. |
| `HIGH` resources/items | Real physical pickup proxies tied to inventory ids; no colored cube currency. |
| `HIGH` sky/ocean | Owner proof pass: replace sky sphere/ocean input primitives or prove hidden input-only path; screenshot proof required. |
| `UNKNOWN` root loose/buildings | Prove unused/dev and quarantine, or move into owned replacement queue. |
| `DEV_ONLY` placeholders/diagnostics | Keep only with explicit marker, dev folder, no production family/scene refs, and future audit allow-list. |
| `LOW` disabled renderer | Document hidden marker role; runtime proof needed before permanent exclusion. |

## Future Audit Hard Errors

Add later to `GeneratedAssetProductionAudit.py` or a sibling gate after owners finish quarantine decisions:

- Enabled `MeshRenderer` plus built-in primitive MeshFilter in `Assets/_Project/Prefabs/Tools/Held`.
- Enabled primitive renderers in `Assets/_Project/Prefabs/Items/Tools`.
- Enabled primitive renderers in `Assets/_Project/Prefabs/Resources/Pickups`.
- Enabled primitive renderers in `Assets/_Project/Prefabs/Transport`.
- Enabled primitive renderers in `Assets/_Project/Prefabs/Player.prefab`.
- Enabled primitive sky/ocean/celestial renderers outside documented hidden input/debug lanes.
- Any `WorldRuntime/ProceduralPlaceholders` or `WorldProceduralProxy` prefab referenced by production `Final`, final-ready family links, or build scenes.
- Root loose active primitive prefabs unless explicit diagnostics/dev allow-list exists.

Keep diagnostics/error fallback prefabs out of hard errors only when path, component marker, and reference proof identify them as fallback/dev.

## Separate Visual Proof Pass Required

Source-only classification cannot settle these:

- `Ocean_Crest.prefab`: whether sargassum input planes render visibly or are Crest input-only.
- `Sky_System.prefab`: actual sky dome appearance, material quality, Aegir/moon relation, and surface visual floor.
- `Player.prefab`: first-person/third-person body visibility, suit silhouette, and camera framing.
- `Tools/Held` and `Items/Tools`: product-face tool body quality and pickup/world variant reuse.
- `Resources/Pickups`: pickup scale, silhouette, material identity, and compact readability.
- `Transport`: vehicle silhouette, pressure-vessel identity, cockpit/anchor relation.
- `WorldRuntime/ProceduralPlaceholders`: whether any placeholder is still wired to production route/family/scene.

Required evidence class for that pass: `PLAYER-CAPTURE VERIFIED` or at minimum Unity/editor screenshot proof plus source matrix.

## False-Positive-Safe Cases And Required Proof

- `PFB_ErrorCube.prefab`: safe only with diagnostics/error fallback reference proof and no normal production placement.
- `Directional Light.prefab`: safe only if `Sun_Body` renderer remains disabled at runtime or is replaced by real celestial body art before use.
- `WorldRuntime/ProceduralPlaceholders`: safe only if dev/placeholder markers remain, no production family/scene/build references exist, and generated final assets replace them before route use.
- `WorldProceduralProxy`: outside primary queue for this task, but not production art proof.

## Evidence Boundary

Claim: primitive mesh references exist and path buckets/counts match task packet.
Evidence class: STATIC_SOURCE.
Artifact: this report plus `1859_NON_PROXY_PRIMITIVE_PREFAB_MATRIX.csv`.
Command/tool class: prefab YAML text scan and static regex parsing.
Date: 2026-06-04.
Residual risk: no Unity import, runtime instantiation, material rendering, camera view, screenshot, profiler, or scene wiring proof.
