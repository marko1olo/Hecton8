# 3DMODEL_FLORA_CORAL

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: kelp, seaweed, coral, roots, abyssal flora, static biological growth, harvestable plants, and non-fauna organic props.

## 1. Organic Asset Law

Flora and coral are not primitive cylinders, spheres, ribbons, or cones. Those shapes are allowed only as construction scaffolds. The saved mesh must include asymmetry, taper, branching hierarchy, secondary silhouette breakup, thickness variation, scars, pores, cavities, bent growth direction, and anchor geometry.

Every flora/coral generator must output a stable LOD chain, vertex color masks, tangents, UVs or approved triplanar coordinates, material slots, and optional interaction proxies. Runtime shaders may bend, pulse, and shade using baked streams. Runtime scripts must not calculate mesh deformation weights.

## 2. Vertex Color Contract

Mandatory for every flora and coral mesh:

- R = water-current sway amplitude. Anchor/root = 0. Rigid mineralized coral = 0 to 32. Flexible frond tips = 192 to 255.
- G = bioluminescence mask or phase. Non-emissive tissue = 0. Pulsing tips or polyps = phase/mask in 32 to 255.
- B = baked ambient occlusion/cavity darkness. Use low values in crevices, under plates, root clusters, and branch intersections.
- A = thickness, damage eligibility, harvest mask, or wetness. Meaning must be written into the manifest.

The red channel must follow physical leverage: `sway = saturate(distanceFromAnchor / maxFlexibleLength) ^ stiffnessExponent`. Roots and holdfasts are rigid. Tips, frills, and thin membranes carry the most movement.

## 3. Topology Families

### Kelp And Seaweed

Required structures:

- Holdfast or root cluster, not a loose vertical ribbon.
- Stipe or spine with taper and ribbing.
- Blade/frond sheets with thickness or edge rim.
- Serration, folds, tears, bubbles, or scars at LOD0.
- Anchor socket or placement pivot at root.

Blade surfaces must not be zero-thickness if seen from both sides at close range. Use a thin shell with edge rim or a shader-authorized two-sided leaf only when overdraw budget accepts it. Alpha blending is forbidden for dense fields on MX350; use alpha clip and dithered fade.

### Coral

Required structures:

- Low coral: mound, bed, porous cap, plate rim, cavities, knuckles.
- Branching coral: welded trunk, branch hierarchy, knuckles, tip clusters, asymmetry.
- Plate coral: layered plates, thick rims, underside AO, chipped edges, support stems.
- Brittle coral: fine branches only at LOD0/LOD1; LOD2 becomes preserved silhouette or impostor.

Branch intersections must be blended, welded, or explicitly hidden by knuckles. Intersecting tubes with z-fighting are rejected.

### Roots And Biofilms

Roots must follow surface curvature, include anchor pads, and avoid perfectly parallel strands. Biofilm can be material/decal-driven but still needs baked mask placement.

## 4. Organic Normals And Tangents

Normals must be generated from the analytical surface where possible:

- Tubes: radial normal blended with curve tangent frame.
- Blades: surface normal from cross of width and length derivatives.
- Plates: top and underside normals separated at rim.
- Coral blobs: gradient of warped implicit surface or weighted face normal.

Tangent direction must follow the UV flow. Tubes use tangent along U or length consistently. Blades use tangent along blade length unless shader expects cross-blade normal map flow. Mirrored UV islands must set tangent handedness correctly.

## 5. UV And Texture Rules

Kelp blades use lengthwise UVs: V from root to tip, U from left edge to right edge. Stipes and branches use cylindrical unwrap with seam on the least visible rear side. Coral massive/rock-like surfaces may use triplanar material projection plus baked vertex AO and masks, but branch tubes still need coherent UVs for detail normal and phase masks.

Texel density:

- Hero harvestable flora: 512 px/m.
- Common instanced flora: 256 px/m.
- Dense field HLOD: atlas/impostor, 64-128 px/m equivalent.

Atlas groups must pack by biome and material family: kelp, brittle coral, massive coral, plate coral, root/biofilm. Do not mix unrelated hue/material families into one atlas if it forces shader variants or destroys streaming locality.

## 6. LOD Rules

LOD0:

- Full silhouette, branch hierarchy, blades, rims, tears, pores, knuckles, and tip clusters.
- Vertex color masks complete.
- Material slots complete.

LOD1:

- Preserve silhouette and branch count that affects outline.
- Collapse tiny pores and serrations into normal/AO.
- Remove non-silhouette companion blades by deterministic selection.

LOD2:

- Preserve mass and root/anchor shape.
- Replace minor branches/blades with simplified shells or cards.
- Keep vertex color R/G/B semantics because shader fakes still read them.

HLOD:

- Impostor card, cluster shell, or GPU Resident Drawer-owned static mesh.
- Dithered clip/fade only. Alpha blend is rejected for dense fields.

## 7. Collision And Interaction

Default flora/coral collision is none. Interaction is represented by coarse proxies:

- Root harvest point: sphere or capsule.
- Large coral blocking path: convex hull under 200 triangles or compound boxes.
- Kelp contact: trigger capsules only if gameplay reads it.
- Fauna navigation obstruction: SDF/proxy bounds, not detailed plant mesh.

Visual fronds, small branches, pores, and serrations never become physical collision.

## 8. Rejection Gates

Reject if:

- Vertex color R/G/B do not follow required semantics.
- Root vertices sway as much as tips.
- Branches intersect without weld, knuckle, or hidden union.
- Blades are flat untextured rectangles at near LOD.
- Any dense flora material uses alpha blend on compact lane.
- Any LOD removes the anchor or changes harvest point identity.
- Any generated asset has no atlas/UV proof or triplanar justification.

## 9. Continuous Quality Scaling

`GlobalQualityWeight` scales flora and coral fidelity through offline branch count, pore density, blade serration density, texture resolution, mask precision, LOD transition distance, and field population density. It never changes harvest point identity, root anchor identity, collider proxy route, shader vertex color semantics, or runtime generation law.

Compact flora still requires organic taper, asymmetry, anchor geometry, correct vertex color R/G/B/A semantics, and readable silhouette. Higher tiers add richer branching, pore fields, emissive organs, scar detail, and near-camera mesh density; they do not permit primitive cylinders, flat cards, or noisy blobs as final assets.

## 10. Proof Artifacts

Flora and coral generation must output:

- asset family, seed, biome/depth route, growth algorithm, and material family;
- branch/blade/coral topology report with weld, knuckle, seam, and anchor validation;
- vertex color channel summary for sway amplitude, bioluminescence phase, AO darkness, and optional stiffness/interaction mask;
- UV unwrap or triplanar justification with atlas rects, padding, texel density, and material slot report;
- LOD triangle counts, simplification method, preserved anchor identity, and shader semantic preservation;
- collision/interaction proxy report or explicit no-collision justification;
- flat-material screenshot proving the silhouette is biological before texture detail;
- final-material screenshot proving wetness, translucency, bioluminescence, scars, pores, and abyssal coloration support the organism.

## 11. Acceptance Sentence

A generated flora or coral asset is accepted only when it reads as grown biological structure, carries required vertex color shader data, preserves harvest/anchor identity across LODs, avoids alpha-blend and primitive-card failure modes on compact hardware, and proves that texture detail enhances a real mesh instead of hiding one.
