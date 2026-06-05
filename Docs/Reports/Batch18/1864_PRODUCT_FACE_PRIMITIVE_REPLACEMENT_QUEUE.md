# 1864 Product-Face Primitive Replacement Queue

Date: 2026-06-04
Agent: 1864
Evidence class: STATIC_SOURCE / STATIC_DOC
Unity/build/runtime: NOT RUN

## Scope

Built a concrete replacement queue for product-face primitive prefabs outside production `Final` roots, excluding sky and ocean because 1865 owns them.

Included:
- `Assets/_Project/Prefabs/Player.prefab`
- `Assets/_Project/Prefabs/Tools/Held/*.prefab`
- `Assets/_Project/Prefabs/Items/Tools/*.prefab`
- `Assets/_Project/Prefabs/Resources/Pickups/*.prefab`
- `Assets/_Project/Prefabs/Transport/*.prefab`
- `Assets/_Project/Prefabs/Item_Titanium.prefab`
- `Assets/_Project/Prefabs/STRUCTURES.prefab`
- `Assets/_Project/Prefabs/Buildings/Cube.prefab`

No source, prefab, asset, scene, importer, bake, Unity, PlayMode, screenshot, profiler, or build action was run.

## Authorities Read

- `AGENTS.md`
- `PROJECT_BIBLES.md`
- `VISION_LOCKS.md`
- `TASTE.md`
- `quality.md`
- `3dmodel.md`
- `3DMODEL_TEXTURES_MATERIALS.md`
- `tools.md`
- `inventory.md`
- `vehicles.md`
- `Docs/Reports/Batch18/1859_NON_PROXY_PRIMITIVE_PREFAB_CLASSIFICATION_PACKET.md`
- `Docs/Reports/Batch18/1859_NON_PROXY_PRIMITIVE_PREFAB_MATRIX.csv`

Relevant mandates loaded:
- `QA_Evidence_Text_Filter_Audit`
- `CORE_Tools_Equipment_Interaction_Raycast_Heat`
- `DATA_Inventory_Resources_Items_SOA_Layout`
- `CORE_Submarine_Vehicles_Kinematics_AUP`
- `REND_URP_Graphics_HotPath_Optimization_HLOD`

## Static Evidence

Command classes used:
- Static text reads: `Get-Content`
- Static recursive file search: `rg --files`
- Static YAML/text search: `rg -n`
- Read-only PowerShell regex GUID/material resolver
- Final whitespace verification: `git diff --check -- <owned outputs>`

Evidence found:
- Every queued product-face prefab still contains Unity built-in primitive mesh GUID `0000000000000000e000000000000000`.
- Primitive mesh fileIDs found: `10202` cube, `10207` sphere/capsule-class primitive, `10208` plane.
- Tool held/world variants reference placeholder tool materials where resolvable, not production mesh assets.
- Resource pickups reference resource materials where resolvable, but the visual meshes remain cube/sphere/plane primitives.
- Transport prefabs keep primitive cube bodies plus rider/dismount anchors.
- `Player.prefab` includes active primitive swim body parts and a disabled primitive visor; it also has an existing non-primitive HUD quad mesh, which does not replace the suit body.
- Static search did not prove any concrete accepted replacement mesh/prefab path for these queued visuals. No replacement is claimed.

## Queue Output

Detailed per-row requirements are in:

`Docs/Reports/Batch18/1864_PRODUCT_FACE_REPLACEMENT_MATRIX.csv`

Rows: 40.

Split:
- Player body: 1
- Held tools: 12
- World tool pickups: 12
- Resource pickups: 8
- Transport: 4
- Root loose item: 1
- Ambiguous/legacy aggregate/building rows: 2

## Priority

1. First-person product face: `Player.prefab` and `Tools/Held`.
2. World pickups the player stares at, scans, or collects: `Items/Tools`, `Resources/Pickups`, `Item_Titanium.prefab`.
3. Vehicle/transport bodies: `Transport`.
4. Ambiguous root/legacy rows: `STRUCTURES.prefab`, `Buildings/Cube.prefab`.

## Acceptance Boundary

Static source can prove only prefab text content, primitive mesh references, material GUID/path references, and absence of found replacement mesh/prefab paths in targeted search.

Acceptance still requires:
- prefab YAML proof after replacement;
- mesh asset path proof;
- material/texture role proof;
- LOD/HLOD proof;
- collision/proxy split proof;
- Unity screenshot or player capture for visual floor;
- profiler/build proof when runtime behavior, vehicle motion, collision, or hot-path presentation changes.

## Continuous Quality Consequence

All queued replacements must scale through continuous `GlobalQualityWeight`.

- Low consequence: no ugly mode; keep silhouette, material identity, readable interaction/pickup shape, and cheap collision proxy.
- Middle consequence: add trim, labels, grime, packed masks, stronger pickup readability, and stable LOD dither.
- High consequence: add richer bevel density, decals, damage/wear masks, stronger first-person material response, and longer near LOD residency.
- Ultra consequence: add sensory overkill only: secondary detail meshes, richer wetness, instrument glow, visor/tool contamination, denser vehicle/tool material breakup. No gameplay truth, item id, collision identity, recipe truth, or authority route changes.

## Result

What was wrong: product-facing player/tool/item/resource/transport prefabs still use visible built-in primitive meshes. This fails the surface/shallow visual floor and turns the product face into blockout art.

What I did: wrote a static replacement queue with per-prefab exposure class, unacceptable primitive reason, required mesh/material/collision/LOD/source owner, proof needs, and continuous quality consequence.

In-game result: PENDING VERIFICATION. Unity and player capture were forbidden for this task.

What was verified: static prefab YAML/text evidence and owned report/matrix formatting only.
