# 1877 Player Suit Mesh Source Authoring Implementation

Evidence class: STATIC_SOURCE. Unity, import, build, PlayMode, profiler, screenshots, menu execution, and generated Mesh asset output were not run.

## Source Route

Added `Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs`.

The route is editor-only and exposes:

- menu item `HECTON-8/Product Face/Author Player Suit Mesh Sources 1877`;
- callable `AuthorAll(float globalQualityWeight)`;
- static audit accessor `GetSpecsForStaticAudit()`;
- future output folder `Assets/_Project/Art/Generated/ProductFace/PlayerSuit`.

When a future Unity owner executes it, the script creates the output folder if needed and writes Mesh assets. It does not create prefabs, materials, textures, colliders, scene objects, GameObjects, runtime systems, or data assets.

## Spec Coverage

The static spec table contains the required 10 source parts:

- `FirstPerson_LeftGloveForearm`
- `FirstPerson_RightGloveForearm`
- `LeftShoulderChestEdge`
- `RightShoulderChestEdge`
- `TorsoHardShell`
- `PelvisHarness`
- `LeftThighCalfFin`
- `RightThighCalfFin`
- `HelmetVisorHousing`
- `VisorGlassSupportRim`

Each spec includes future mapping notes for `Swim_*Attachment`, `HandAnchor`, `Suit_Visor`, or HUD projection roots as relevant. These notes are source metadata only and do not mutate the prefab.

## Geometry Helpers

Implemented manual vertex/index generation helpers for:

- tapered capsule-like limb shells;
- beveled hard plates;
- curved visor rim bands;
- straps and hoses;
- fins;
- latch blocks;
- instrument trim strips.

The implementation does not call `GameObject.CreatePrimitive`.

## Validation

The route validates:

- exactly 10 source specs;
- distinct source names;
- required material source assumptions without creating materials;
- non-empty vertex/index data;
- index count divisible by 3;
- valid material slot count;
- finite positions, normals, tangents, UVs, and bounds;
- non-degenerate triangle area;
- normalized normals and tangents;
- final mesh non-zero vertex and triangle counts.

## GlobalQualityWeight

`GlobalQualityWeight` is continuous and scales:

- radial segment count;
- hose segment count;
- trim density;
- bevel width.

It does not change gameplay truth, collider identity, movement, camera, HUD, tool anchors, `HandAnchor`, `Suit_Visor`, save identity, DTO layout, or authority routes.

## What This Does Not Do

- Does not replace `Player.prefab`.
- Does not hide or delete current primitive body parts.
- Does not generate Mesh assets in this task.
- Does not create or assign materials.
- Does not create collider proxies.
- Does not change `HandAnchor`, `Swim_*Attachment`, `Suit_Visor`, `Suit_Diegetic_HUD_V4_Projection`, or HUD roots.
- Does not claim visual acceptance.

## Pending Unity Proof

Required future proof before acceptance:

- Unity import/compile.
- Menu execution under a scoped Unity owner.
- Generated Mesh asset inspection.
- Material assignment using resolved project-owned material assets.
- Player prefab relink preserving anchors/controllers.
- Collider/proxy split proof with no visual mesh collision ownership.
- First-person suit/hands/visor screenshot.
- Third-person or external suit screenshot.
- Compact, Middle, High, and Ultra capture/proof.
- Static prefab scan proving active built-in primitive body MeshFilters are gone after relink.

## Scaling Consequences

- Compact: lower segment and trim density, same authored silhouette route, no primitive fallback, no gameplay truth changes.
- Middle: more rounded limbs, readable straps/trims, stronger source density for grime/labels/material response after materials are assigned.
- High: richer bevels, hose/rim density, and first-person contour fidelity.
- Ultra: highest trim/rim/limb segment density for visual overkill, still visual-only.

## Acceptance Boundary

Status remains `PENDING VERIFICATION` beyond static source. This task removes the missing source-authoring route blocker only. Product acceptance still depends on generated assets, materials, relink, collider split, screenshots, Unity compile/import, and profiler evidence where later runtime work touches rendering.

## Orchestrator Follow-Up

After agent completion, the orchestrator replaced Unity-version-risky `float.IsFinite` calls with the local `IsFinite(float)` helper. Mesh generation behavior and authored suit part specs were not changed.

## Static Verification

Commands:

```powershell
git diff --check -- Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs.meta Docs/Tasks/Status_1877.md Docs/AgentLogs/Rationale_1877.md Docs/AgentLogs/LOG_1877.md Docs/Reports/Batch18/1877_PLAYER_SUIT_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md
```

Result: PASS, no output.

```powershell
rg -n "GameObject\.CreatePrimitive|CreatePrimitive" Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs
```

Result: PASS, no hits, exit 1.

```powershell
rg -n "float\.IsFinite|double\.IsFinite" Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs
```

Result: PASS, no hits, exit 1.

```powershell
rg -n "FirstPerson_LeftGloveForearm|FirstPerson_RightGloveForearm|LeftShoulderChestEdge|RightShoulderChestEdge|TorsoHardShell|PelvisHarness|LeftThighCalfFin|RightThighCalfFin|HelmetVisorHousing|VisorGlassSupportRim" Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs
```

Result: PASS, all 10 required source IDs present.

```powershell
rg -n "new GameObject|AddComponent|PrefabUtility|SaveAsPrefab|MeshCollider|HandAnchor\s*=|FindObject|GameObject\.Find|Resources\.Load" Assets/_Project/Scripts/Editor/ProductFacePlayerSuitMeshSourceAuthoring.cs
```

Result: PASS, no hits, exit 1.
