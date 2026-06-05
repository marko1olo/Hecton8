# 2204 Generated World Asset Reject Gates

Worker: 2204
Evidence mode: STATIC POLICY AND SOURCE AUDIT
Runtime/visual proof: PENDING VERIFICATION

## Hard Reject Taxonomy

Reject generated world asset or placement if any item is true:
- Uses Unity built-in primitive mesh GUID as a final or final-ready mesh.
- Uses `WorldProceduralProxy` or `WorldRuntime/ProceduralPlaceholders` material/prefab as product-facing art.
- Has null renderer material slots, default material refs, empty `_BaseMap` / `_MainTex`, or flat synthetic color pretending to be final texture work.
- Places live kelp, coral, abyssal flora, underwater egg clusters, or underwater creature-zone visuals on dry land or above waterline.
- Places shallow photic coral/kelp in abyss/cave/interior without light/current/substrate explanation and route proof.
- Ships without LOD0/LOD1/LOD2 or with non-descending triangle budgets.
- Uses LOD0 render mesh as the only collision mesh for large assets.
- Has collision proxy that does not match the readable silhouette or gameplay footprint.
- Repeats obvious stamps without scale, rotation, deformation, material, or silhouette variation.
- Produces primitive, crayon, muddy, flat, proxy, or toy-like silhouettes below the `TASTE.md` floor.
- Generates final mesh/texture/collider at runtime without explicit owner phase, profiler proof, and no-GC proof.
- Ships without a proof packet.

## Zone Rules

### Shore / Coast / Above Surface
Allowed:
- Wet basalt, salt crust, tide marks, foam-wet rocks, stranded debris, wreck fragments, sparse dead/stranded algae.
- Intertidal biology only when tagged as beached, dead, salt-crusted, or waterline-bound.

Rejected:
- Live tall kelp, coral garden, abyssal plants, egg clusters, reef apex markers, or underwater spawn visuals on dry terrain.
- Smooth primitive rocks or repeated cones/cylinders as cliffs/spires.

### Photic Underwater
Allowed:
- Coral low/branching/massive/plate/brittle only on valid substrate.
- Shallow kelp/plant patches with current/light/depth proof.
- Authored rocks, shells/sand/silt variation, small passive creature cues, route landmarks.

Rejected:
- Dry shore materials underwater without wet/submerged variant.
- Proxy coral/kelp material refs.
- Dense random carpets that ignore current, slope, substrate, and route readability.

### Medium Depth
Allowed:
- Strong silhouettes, muted color, suspended particulates, silt, wreck fragments, cave entrances, predator/passive spawn ecology, biolum accents if biologically placed.

Rejected:
- Bright surface reef carpet without light logic.
- Black/noir darkness used to hide weak geometry or flat materials.
- Abyss-only assets copied into medium depth without transitional dressing.

### Cave / Interior / Wreck
Allowed:
- Ruin modules, service scars, debris fields, resource/hazard/safe pockets, sparse biofilm, leakage flora near breaches only with depth/water proof.

Rejected:
- Open-water kelp canopy in sealed interiors.
- Visible creature-spawn marker proxies as final art.
- WFC/procedural wreck modules with no socket, collider, LOD, or navigation proof.

## Required Static Validators Before Unity Owner Acceptance

No-Unity validators to keep or add:
- Production scene YAML scan rejecting `WorldProceduralProxy` and `ProceduralPlaceholders` refs outside explicit diagnostic/dev scenes.
- Built-in primitive mesh GUID scan for final-ready prefabs and product-facing scenes.
- Material YAML scan rejecting null material slots, default material refs, empty `_BaseMap` / `_MainTex`, and missing required texture roles.
- Family placement profile scan requiring min/max depth, substrate, slope, biome, route band, and waterline legality.
- Underwater-biota-on-dry-land scan: reject kelp/coral/abyssal/egg/creature-zone families when instance/profile permits above-water placement unless tagged `BeachedDead`, `Intertidal`, or `DiagnosticOnly`.
- Collision scan rejecting large render meshes with only LOD0 MeshCollider and no `COL_` proxy.
- Repetition scan counting same prefab/family within a local grid and failing obvious stamp carpets without variation data.
- Proof-packet manifest scan requiring mesh/material/texture/prefab/LOD/collider paths and validator outputs.

## Minimum Proof Packet

Required for every generated asset family:
- Asset manifest with final prefab path, mesh LOD paths, material paths, texture paths, collision proxy path, placement profile, and family contract.
- Validator report paths and clean result summary.
- Screenshot set: close material, gameplay distance, compact/low quality, LOD transition, collision/placement debug.
- Placement record: zone, depth range, slope range, substrate, biome, light/current assumption, route purpose.
- Low/Middle/High/Ultra consequence note showing density/fidelity scaling without changing gameplay truth.

## Static Acceptance Language

Use only:
- `STATIC VERIFIED` for source/path/report evidence inspected on disk.
- `PENDING VERIFICATION` for visuals, runtime, frame cost, generator output quality, and scene composition not executed in this task.
- `REJECT` for any hard gate failure above.

Do not use "visually acceptable" without screenshot proof and route-owner acceptance.
