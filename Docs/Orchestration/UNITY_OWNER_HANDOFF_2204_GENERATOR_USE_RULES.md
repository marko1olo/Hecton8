# Unity Owner Handoff - 2204 Generator Use Rules

Worker: 2204
Scope: procedural mesh, biota, geology, support, wreckage, and placement quality gates.

## Do Not
- Do not run broad generators.
- Do not accept `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` assets in production scenes.
- Do not place live underwater flora or coral on dry land.
- Do not ship built-in primitive mesh refs as final assets.
- Do not use darkness, fog, or medium-depth noir to hide weak geometry/materials.

## First Static Gates
Run the narrow validators before any visual acceptance:
- Family contract validator.
- Final prefab quality gate.
- Flora final variant validator.
- Geology final validator.
- Support/structural final validators for support, ruins, debris, wreckage.
- Primitive/null/default/proxy material scan equivalent to Batch21/2104.

## Narrow Generator Run Rule
If a generator must be used, choose one family and one route band only.

Recommended first safe test:
- Route: photic 5-50m.
- Families: `family_coral_branching`, `family_coral_low`, controlled shallow kelp patch.
- Assets first: baked `Assets/_Project/Prefabs/Nature/Flora/Baked/**` plus real rock assets under `Assets/_Project/Art/Models/Rocks/**`.
- Reject any proxy material from `Assets/_Project/Art/Materials/WorldProceduralProxy/**`.

## Required Proof Packet
- Generator/menu path, family, profile, seed, and route band.
- Manifest: prefab, mesh LODs, materials, textures, collision proxy, placement profile.
- Validator outputs.
- Screenshots: close material, gameplay distance, compact/low quality, LOD transition, placement/collider debug.
- Placement proof: depth, waterline, slope, substrate, biome, route purpose.
- Runtime proof if runtime scatter/geology changed: frame cost, GC allocation, owner phase.

## Placement Rules
- Shore/above surface: wet rock, salt, tide marks, stranded/dead algae only. No live coral/kelp.
- Photic: bright readable water, valid substrate, authored coral/kelp materials, route landmarks.
- Medium depth: strong silhouettes, silt, controlled biolum, wreck/rock/cave route evidence.
- Cave/interior: breach-local biology only, no open-water kelp canopy, no visible spawn proxies.

## Acceptance Language
- Static source/path evidence: `STATIC VERIFIED`.
- Visual/runtime/frame evidence not produced in this task: `PENDING VERIFICATION`.
- Any hard gate miss: `REJECT`.

No Unity was run by Worker 2204.
