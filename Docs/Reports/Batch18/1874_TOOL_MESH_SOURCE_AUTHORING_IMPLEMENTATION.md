# 1874 Tool Mesh Source Authoring Implementation

Date: 2026-06-04
Agent: 1874
Evidence class: STATIC_SOURCE
Unity/build/runtime: NOT RUN

## Scope

Implemented an editor-only source authoring script for future non-primitive Mesh assets for the 12 product-face tool bodies.

Owned source:

- `Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs`
- `Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs.meta`

No prefab, scene, `.asset` mesh output, material, binary, Unity menu execution, import, bake, PlayMode, profiler, dotnet build, or Data Monolith action was run.

## Source Route

Future callable route:

```text
HECTON-8/Product Face/Author Tool Mesh Sources 1874
```

Future method:

```text
ProductFaceToolMeshSourceAuthoring.AuthorAll(float globalQualityWeight)
```

Future output folder:

```text
Assets/_Project/Art/Generated/ProductFace/Tools
```

The script writes only Mesh `.asset` files when executed later. It does not replace held/world prefabs, assign materials, create colliders, create anchors, or mutate gameplay truth.

## Tool Specs

The source table defines all 12 tool identities from 1869:

- `Tool_BeaconDeployer`
- `Tool_Builder`
- `Tool_EnvAnalyzer`
- `Tool_Flashlight`
- `Tool_HarpoonLauncher`
- `Tool_Knife`
- `Tool_LaserCutter`
- `Tool_Propulsion`
- `Tool_Repair`
- `Tool_SalvageSampler`
- `Tool_Scanner`
- `Tool_StunPistol`

Each spec carries a distinct silhouette enum, mesh asset name, visual-part intent, and future anchor names as static source metadata. The script preserves anchor intent by name only; it does not create or move runtime anchors.

## Geometry Helpers

Included helper routes:

- beveled boxes;
- cylinders;
- rails;
- nozzles;
- lenses;
- screens;
- fins;
- spools;
- grips;
- blade wedge.

The mesh source builds vertex/index data manually. It does not use Unity built-in primitive creation.

## Validation

The future execution path validates:

- non-empty vertex/index data;
- index count divisible by 3;
- finite positions, normals, tangents, and UVs;
- triangle area above `0.0000001`;
- normal length tolerance;
- expected submesh/material slot count;
- finite non-zero bounds.

It fails closed if required shader source assumptions are missing:

- `Assets/_Project/Art/Shaders/Hecton_ToolDecayLit.shader`
- `Assets/_Project/Art/Shaders/Hecton_ToolScreenDiegetic.shader`

## Continuous Quality Route

`GlobalQualityWeight` is consumed as a continuous `0.0..1.0` float:

- radial cylinder/lens segment count scales continuously;
- fin count scales continuously;
- bevel width scales continuously.

Scaling consequences:

- Low: retained silhouette, one bevel/chamfer route, reduced radial and fin density.
- Middle: stronger detail density from the same source spec.
- High: smoother nozzles/lenses and richer bevel response.
- Ultra: highest helper detail count, still presentation-only and no gameplay truth change.

## Static Checks

Claim: owned source contains no `GameObject.CreatePrimitive`.
Evidence class: STATIC_SOURCE.
Command:

```powershell
rg -n "GameObject\.CreatePrimitive" Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs
```

Result: no hits.

Claim: all 12 tool IDs exist in the source spec table.
Evidence class: STATIC_SOURCE.
Command:

```powershell
rg -n "Tool_BeaconDeployer|Tool_Builder|Tool_EnvAnalyzer|Tool_Flashlight|Tool_HarpoonLauncher|Tool_Knife|Tool_LaserCutter|Tool_Propulsion|Tool_Repair|Tool_SalvageSampler|Tool_Scanner|Tool_StunPistol" Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs
```

Result: all 12 IDs found.

Claim: owned file diffs have no whitespace errors.
Evidence class: STATIC_SOURCE.
Command:

```powershell
git diff --check -- Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs Assets/_Project/Scripts/Editor/ProductFaceToolMeshSourceAuthoring.cs.meta Docs/Tasks/Status_1874.md Docs/AgentLogs/Rationale_1874.md Docs/AgentLogs/LOG_1874.md Docs/Reports/Batch18/1874_TOOL_MESH_SOURCE_AUTHORING_IMPLEMENTATION.md
```

Result: PASS.

## Acceptance Boundary

This is not visual acceptance. It is a source route.

Pending proof:

- Unity import/compile.
- Unity menu execution.
- Mesh assets created and inspected.
- Material assignment and texture role proof.
- Prefab YAML primitive replacement proof.
- Collider proxy split proof.
- Anchor preservation proof.
- Held/world screenshots and player capture.
- Profiler/Frame Debugger only if future runtime/render path changes.

## Result

What was wrong: 1869/1867/1868 show product-face tools still need non-primitive source meshes before prefab replacement can be proven.

What I did: added the static editor-only authoring route and batch evidence artifacts for 1874.

In-game result: PENDING VERIFICATION. Unity execution was forbidden.

What was verified: static source presence, static no-primitive-creation scan, static 12-tool spec scan, and diff whitespace gate.

## Orchestrator Follow-Up

After agent completion, the local orchestrator made one hygiene patch in the owned source route:

- Existing mesh asset update now calls `EditorUtility.SetDirty(existing)` and destroys the temporary generated `Mesh` after `EditorUtility.CopySerialized`.
- Report counting now reads from the persisted existing mesh when the update path is used.

Evidence class remains `STATIC_SOURCE`. Unity import/menu execution is still pending.
