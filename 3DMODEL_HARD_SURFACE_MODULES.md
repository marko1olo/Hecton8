# 3DMODEL_HARD_SURFACE_MODULES

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: DeepReach modules, airlocks, wreckage shells, submarine components, doors, panels, corridors, pipes, supports, and industrial structure meshes.

## 1. Shape Language

Generated hard-surface assets must look pressure-rated, serviceable, and physically manufactured. The base shape may be a box, cylinder, arch, capsule, or WFC module, but the saved mesh must carry bevels, wall thickness, panels, sockets, trim, vents, seams, welds, bolted flanges, gasket grooves, cable ports, maintenance plates, and corrosion catchments. A plain cube with material color is rejected even if it satisfies collision and socket math.

Required silhouette layers:

- Macro form: readable module body, hull section, door frame, pipe, corridor, or machine casing.
- Meso form: bevels, inset panels, ribs, braces, pressure rings, trims, hatches, sockets, access covers.
- Micro form: material-driven normals, decal masks, chipped paint, rust streaks, biofilm, wetness, cavity AO.

## 2. Bevel And Chamfer Rules

Every visible hard edge above 35 degrees must be beveled. Default bevel pass:

```text
for each edge with exactly two adjacent faces:
    n0 = faceNormal(faceA)
    n1 = faceNormal(faceB)
    angleDeg = degrees(acos(clamp(dot(n0, n1), -1, 1)))
    if angleDeg > 35 and edge not hidden:
        width = clamp(sizeClassWidth * qualityScale, minWidth, shortestAdjacentEdge * 0.20)
        segmentCount = lerp(1, 6, GlobalQualityWeight) by family importance
        insert support loops
        tag bevel faces for edge wear and smoothing
```

Open boundary edges must be handled as design features: cap, rim, gasket, torn metal, or socket. An accidental open border is a validation failure.

Segment budget:

- Low lane: one chamfer segment on every visible hard edge, no optional micro bevels.
- Middle lane: one to two segments on primary edges, one segment on trim.
- High lane: two to four segments on hero/near edges, bevel masks on wear surfaces.
- Ultra lane: three to six segments on hero assets, layered trim bevels, richer baked normal detail.

## 3. Normal And Smoothing Groups

Hard-surface normals must be explicit. Do not trust `RecalculateNormals` as default output.

Smoothing group construction:

1. Assign every triangle a material class: flat panel, bevel, gasket, glass, rubber, pipe, trim, interior cut, or damage.
2. Connect adjacent triangles only if material class matches and normal angle is below the smooth threshold.
3. Smooth panel fields at 15 degrees or less.
4. Smooth bevel chains at 65 degrees or less.
5. Split vertices across material borders, UV seams, hard edge borders, mirrored islands, and socket cuts.
6. Use weighted normals by face area and corner angle.

Metallic panels need broad stable normals. Bevel faces need softer normals so specular highlight rolls across the edge. Gaskets and rubber can use softer normals but must not bleed into metal panels.

## 4. Panelization And Socket Law

WFC and modular systems must separate connection logic from visual form. Socket boxes may drive placement, but generated visual modules must add:

- Inset frame around every connector face.
- Gasket or flange ring around airlocks and pipe sockets.
- Alignment keys and bolt patterns derived from socket ID.
- Non-identical panel breakup per seed using deterministic slots.
- Internal face culling for hidden connectors.
- Exterior face trim for exposed connectors.

Socket-compatible modules must share seam dimensions exactly so no cracks appear. Visual detail may vary, but connector lips, collision proxy bounds, and snap planes must remain deterministic.

## 5. Wear, Rust, And Grime Baking

Hard-surface generators must bake curvature and cavity data offline.

Vertex color contract:

- R = edge wear. High on exposed convex bevels, low on protected flat fields.
- G = rust, salt streak, biofilm, or grime accumulation. High below seams, bolts, vents, and water traps.
- B = ambient occlusion/cavity darkness. Low in crevices and undercuts, high on exposed faces.
- A = decal eligibility, emissive mask, warning paint mask, or damage reveal mask.

Curvature estimate:

```text
convexity(edge) = saturate((angleDeg - 35) / 120)
cavity(face) = occlusionSampleCountBlocked / occlusionSampleCount
wear = convexity * exposureMask * materialWearCoefficient
grime = cavity * downwardBias * wetnessRoute
```

No runtime script computes rust maps, dirt masks, edge wear, or grime placement.

## 6. UV And Material Requirements

Industrial parts must use calibrated texel density. Default:

- Hero/interactable: 512 to 1024 px per meter at LOD0 source texture scale.
- Standard modules: 256 to 512 px per meter.
- Distant shell/HLOD: 64 to 128 px per meter or baked composite.

UV islands must align panel grain and brushed metal direction. Bevel strips may share a trim atlas only if the tangent basis is correct and normal map direction remains consistent.

Material slots:

- Slot 0: painted or bare structural metal.
- Slot 1: bevel/wear trim or exposed metal.
- Slot 2: rubber/gasket/glass/plastic secondary material.
- Slot 3: emissive strips, labels, warning markers, or wet biofilm overlay.

## 7. LOD Rules

LOD0 includes all silhouette bevels, panels, trim, and visible damage.
LOD1 preserves macro silhouette, primary bevels, sockets, and panel cuts; removes micro bolts, tiny cables, and non-silhouette trim.
LOD2 preserves bounding silhouette and major connector shapes; replaces small panels with baked normal/mask detail.
HLOD uses a shell or card with baked albedo/normal/AO composite.

Connector bounds, attach points, and gameplay affordances must not move between LODs.

## 8. Collision Rules

Hard-surface collision uses compound primitives. Preferred order:

1. BoxCollider for corridors, rooms, panels, crates, supports.
2. CapsuleCollider for pipes, rails, cylindrical tanks, handles.
3. SphereCollider only for knobs, domes, and spherical pressure parts.
4. Convex hull only when primitive decomposition cannot represent gameplay contact.

Visual bevels, bolts, wires, panels, and dents never enter collision. Collision children must be named `COL_*`, be generated offline, and use assigned physics layers before prefab save.

## 9. Rejection Gates

Reject the generated asset if:

- Any visible 90 degree edge lacks a bevel or authored rim.
- Any panel is a flat unbroken rectangle larger than 1.5 m without seams, trim, decals, or material breakup.
- Any visual LOD0 mesh is assigned to MeshCollider.
- Any UV island has less than required padding.
- Any material is an instance clone created for one prefab without reason.
- Any socket seam creates a visible crack or overlap.
- Any generated module cannot state its LOD triangle counts and collision proxy count.

## 10. Proof Artifacts

Hard-surface generation must output a compact proof packet before the prefab is accepted:

- asset family, seed, module ID, socket IDs, and deterministic generation parameters;
- LOD0/LOD1/LOD2/HLOD triangle counts;
- bevel threshold, bevel width range, and segment count by quality lane;
- smoothing split report: hard edges, UV seams, material borders, socket cuts;
- UV density and atlas padding report;
- material slot list with shared material asset paths;
- vertex color channel summary for wear, grime, AO, and decal/emissive masks;
- collision proxy count and primitive/convex hull triangle budgets;
- screenshots or renders with textures enabled and disabled;
- wireframe, collider, and material-ID debug captures for hero or close-view modules.

Static documents may only claim `STATIC VERIFIED`. Unity import, batching, collider, and profiler claims remain `PENDING UNITY/PROFILER VERIFICATION` until measured.

## 11. Acceptance Sentence

A hard-surface generated asset is accepted only when it reads as pressure-rated manufactured machinery before textures are applied, proves bevels and weighted normals, keeps sockets and collision deterministic, uses shared PBR material routes, ships a full LOD/proxy chain, and provides the proof packet above.
