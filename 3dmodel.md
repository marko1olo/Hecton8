# HECTON-8 3D Model Generation Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Owner: 3D_MODEL_GENERATION_STANDARDS_DIRECTOR
Scope: offline Editor-time mesh, UV, material, texture, LOD, prefab, and collision generation.

## First-20 Route Hook

- First-20 moment: bright shallow exit, first readable resource object, tool target, route landmark, shallow biota, and technogenic trace assets.
- Route blocker removed: primitive or noisy generated meshes cannot enter the opening route without authored-looking silhouette, material identity, LOD chain, collision proxy, and validation manifest.
- Proof class: STATIC_DOC until asset manifests, validation reports, render captures, Unity import evidence, and route screenshots exist.

## 0. Prime 3D Product Law

Generated assets must look authored. HECTON-8 rejects procedural-looking props, rocks, fauna, flora, terrain pieces, and modules that merely satisfy a mesh count or socket contract.

The product bar is: believable silhouette, material identity, UV discipline, LOD chain, collision proxy, texture response, scale witnesses, and proof renders. Offline generation is allowed because it can be controlled, inspected, rejected, and improved. It is not permission to ship cheap noise, primitive shapes, flat materials, or placeholder prefabs.

User vision lock: procedural/offline generation is acceptable only when the result looks authored and premium. Surface, coastline, shallow, medium-depth route assets, fauna, flora, rocks, capsules, structures, and interactable objects must respect the `TASTE.md` floor: Subnautica-level or better as the minimum, with HECTON-8 targeting more detail and stronger material identity. If generator output reads as primitive, blurry, noisy, crayon-like, PS1-like, flat, or placeholder, it is editor diagnostic only. Improve the generator, use existing project textures/assets, author a better source texture, ask the user for source art, or cut the asset from production.

## 0. Foundational Law: Offline Permanence

**All mesh generation, texture synthesis, UV unwrapping, tangent construction, normal baking, atlas packing, collider fitting, LOD decimation, and prefab assembly MUST occur only in Unity Editor tooling or external offline DCC/bake tools.**

Runtime gameplay must never create, resize, unwrap, decimate, compress, bake, or procedurally mutate visual mesh buffers. Runtime is a blind consumer of serialized `.mesh`, `.prefab`, `.mat`, `.png`, `.asset`, Addressables, and Data Monolith records that were already validated on disk. Any code path that builds vertex arrays in Play Mode, calls `mesh.vertices`, creates `new Mesh()` for production visuals, bakes texture pixels, rebuilds tangents, or assigns a freshly generated `MeshCollider` during gameplay is a production defect unless it is an explicitly documented debug/editor-only bridge behind `#if UNITY_EDITOR`.

Reason: vertex buffer construction, topology repair, UV packing, texture compression, collider cooking, and tangent-space generation are CPU-heavy, allocation-heavy, and upload-heavy. On the compact lane, one bad runtime generator can consume several milliseconds, fragment managed/native memory, force GPU upload spikes, and break the 0 B/frame mandate. HECTON-8 buys immersion with offline labor. The shipped player loads finished data.

Every generated visual asset must therefore ship as:

- `GEN_*` prefab with an `LODGroup` or approved HLOD/impostor route.
- One or more persistent `.mesh` assets named by family, variant, and LOD.
- Static material references named `MAT_*`, never runtime material clones.
- Persistent texture references named `TX_*`, imported with compression and mip settings.
- Separate collision proxy assets or primitive collider children named `COL_*`.
- A validation report or manifest with triangle counts, UV density, material slots, collider type, and failure gates.

## 0A. Runtime And Hot-Path Boundary

The runtime owner for generated models is streaming, culling, instancing, shader animation, LOD selection, and interaction through serialized anchors/proxies. It is not generation.

Hot paths must not allocate or mutate mesh topology, vertex/index buffers, UVs, tangents, texture pixels, atlas data, material instances, collider cooking, LOD chains, prefab structure, sockets, anchors, or validation manifests. `GlobalQualityWeight` may select prebuilt variants, distances, density, shader presentation, and residency only. It must not change vertex channel meaning, collider identity, socket names, save identity, prefab ownership, or gameplay truth.

## 1. Routing Map

If a task asks an agent to generate or improve a 3D asset, route it through the relevant file before writing code:

- Hard-surface base modules, airlocks, wreckage shells, pressure doors, hull panels: `3DMODEL_HARD_SURFACE_MODULES.md`
- Flora, kelp, coral, roots, organic static growth: `3DMODEL_FLORA_CORAL.md`
- Fauna meshes, shells, appendages, jaws, fins, skeletons, skinned or VAT-ready bodies: `3DMODEL_FAUNA.md`
- Rocks, cliffs, ore nodes, thermal vents, cave geology, terrain fragments: `3DMODEL_GEOLOGY_ROCKS.md`
- Tools, equipment, props, machinery, cockpit parts, lab devices: `3DMODEL_EQUIPMENT_PROPS.md`
- Textures, PBR masks, UVs, atlases, triplanar material assignment: `3DMODEL_TEXTURES_MATERIALS.md`
- Texture family generation recipes, AI-source rules, procedural material bakes, and final texture acceptance: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`
- Hero assets, close-camera setpieces, premium fauna, premium modules, and any request for maximum generated realism: `3DMODEL_HERO_REALISM_OVERKILL.md`

This root file overrides weaker family documents. Specialist files add stricter rules for their domain.

## 2. Existing Generator Reality

The project already has useful offline and hybrid generation lanes: coral and seaweed mesh builders, flora texture/material authoring, geology forge, wreckage merge/proxy logic, DeepReach station module surrogates, primitive visual shells, texture bakers, and collider audit/fitting tools. These are foundations, not proof of final art quality.

The visible risk is clear:

- Box surrogates can satisfy socket math but fail NASA-punk silhouette quality unless bevels, panels, trims, decals, and material slots are added.
- Blob, tube, ribbon, plate, and sphere-based organic builders can become recognizable only if they enforce silhouette variance, root/tip deformation channels, cavity/AO masks, nonuniform thickness, and UV density.
- Geology builders can produce LOD chains and proxy colliders, but rocks still need believable stratification, chipped edges, occluded cracks, scale-calibrated tri budgets, and material projection rules.
- Texture bakers can output albedo/normal/MRAO, but generated meshes must consume those maps through correct UVs, material IDs, channel packing, compression, and mip padding.
- Any runtime mesh path must be treated as legacy, diagnostic, or a source for future offline baking, not a production route.

Existing generators are not evidence that the final look is solved. Each generated family still needs visual judgment against screenshots, scene captures, material proof, LOD proof, and route context. A technically valid mesh that makes the game look cheap is a failed asset.

## 3. Universal Mesh Data Contract

Every persistent generated mesh must declare a stable vertex layout. The minimum layout is:

| Stream | Format | Required Use |
|---|---:|---|
| Position | Float32 x3 | Local-space meters. Finite. Bounds validated. |
| Normal | Float32 x3 or packed normalized | Unit length within 0.5 percent. Split where smoothing groups demand. |
| Tangent | Float32 x4 or packed normalized | MikkTSpace-compatible. `w` is handedness. Required for normal maps. |
| Color | UNorm8 x4 | Domain-specific masks. No decorative random tint without meaning. |
| TexCoord0 | Float32 x2 or Float16 x2 | Primary material UV. Must pass density and padding gates. |
| TexCoord1 | Float32 x2 or Float16 x2 | Lightmap, detail, atlas remap, or packed baked masks when required. |
| TexCoord2 | Optional | Curvature, blend, flow, VAT, or family-specific data. |

Generated meshes must use `Mesh.AllocateWritableMeshData`, `SetVertexBufferParams`, `SetIndexBufferParams`, `SetSubMesh`, and `Mesh.ApplyAndDisposeWritableMeshData` where practical. `RecalculateNormals`, `RecalculateTangents`, and `RecalculateBounds` are allowed only as editor fallback after a documented failure, never as the default strategy. A generator owns normals, tangents, UVs, and bounds because it owns the geometry.

All positions, normals, tangents, UVs, colors, bounds, and indices must be finite. No `NaN`, no `Infinity`, no zero-length normal, no degenerate triangle, no inverted unintentional winding, no unbounded UV island.

## 4. Hard-Surface Engineering Law

Ninety-degree mathematical corners are banned on visible metal, plastic, ceramic, glass, rubber, pressure doors, habitat modules, wreckage, pipes, railings, consoles, and equipment. Real manufactured objects have edge radius. PBR specular response needs that radius. A perfect cube edge is one infinitely sharp normal discontinuity, so it either renders as a dead black cut or a razor highlight that exposes the mesh as programmer output.

Every visible hard edge whose adjacent face angle is greater than 35 degrees must be processed by a bevel/chamfer pass unless the edge is explicitly hidden inside an occluded connector seam. Default bevel width is:

- Small handheld prop: 0.006 m to 0.018 m.
- Interior equipment and panel trim: 0.012 m to 0.035 m.
- Base module structural edge: 0.035 m to 0.12 m.
- Exterior hull/wreckage macro edge: 0.08 m to 0.35 m.

Bevel width scales continuously with `GlobalQualityWeight`, asset size, camera importance, and LOD. Low tier keeps fewer bevel segments but keeps at least one chamfer face on every visible hard edge. Ultra tier may add 3-6 bevel segments on hero modules, secondary trim, dents, welded rims, and gasket seams. The silhouette must not step from low to high; it must gain density along the same authored shape.

Required bevel algorithm summary:

1. Build an edge map from sorted vertex-index pairs to adjacent triangle indices.
2. Compute face normals using stable cross products and reject zero-area faces.
3. For each edge, compute `angle = acos(clamp(dot(n0, n1), -1, 1))`.
4. If `angle > threshold` and the edge is visible or silhouette-critical, insert bevel support loops.
5. Clamp bevel width to 20 percent of the shortest adjacent edge to prevent self-overlap.
6. Assign new bevel faces to a bevel material slot or bevel wear mask when needed.
7. Rebuild smoothing groups and split vertices where surface identity changes.

Smoothing groups are not optional. A generator must group connected faces when their normal angle is below the smooth threshold and their material/surface type matches. Vertices are shared only inside a smoothing group. Across a hard group boundary, duplicate the vertex with identical position/UV but different normal/tangent. Weighted normals must use face area and bevel importance:

```text
weightedNormal(v, group) = normalize(sum(faceNormal[i] * faceArea[i] * cornerAngleWeight[i]))
```

Hard-surface generated modules must also bake wear data offline:

- Vertex color R: exposed edge wear or salt-polished rim mask.
- Vertex color G: rust, oxidation, biofilm, or fluid stain phase/amount.
- Vertex color B: baked ambient occlusion and cavity darkness.
- Vertex color A: optional emission, warning paint, or decal eligibility mask.

## 5. Organic Growth Law

Organic meshes must not look like spheres, cones, tubes, or flat ribbons after generation. The primitive may be a construction scaffold, but the saved mesh must contain secondary silhouette noise, believable taper, compression, scars, growth rings, cavities, and nonuniform cross-sections.

Mandatory organic vertex color contract:

- **Red** = current/water sway amplitude. Root/anchor vertices are `0`. Tips/fronds/tentacle ends approach `255`.
- **Green** = bioluminescence phase or mask. Non-emissive tissue is `0`. Pulsing tissue stores phase/mask.
- **Blue** = baked ambient occlusion or cavity darkness. Exposed bright surfaces are high. Crevices and undercuts are low.
- **Alpha** = family-specific stability, wetness, damage eligibility, thickness, or shader blend weight. The meaning must be documented in the asset manifest.

Runtime shaders may animate sway, glow, wetness, and low-cost presentation using these baked streams. Runtime gameplay must not compute per-vertex sway weights or build texture masks.

Organic topology must be manifold where the asset is a solid object. Plants and fins may be intentionally open shells, but all sheet borders must be capped, thickened, or tagged as non-collision render-only surfaces. Branch intersections must be welded or intentionally blended; overlapping tubes that z-fight inside the same surface are forbidden.

## 6. UV, Atlas, And Material Law

Every mesh must own one of these approved UV routes:

- Conformal unwrap using LSCM/ABF-style angle preservation for unique surfaces.
- Cylindrical unwrap for tubes, pipes, stalks, tentacles, and branches, with seam placed on the least visible underside.
- Box/projection unwrap for industrial panels only when each face has calibrated texel density and bevel islands are handled.
- Triplanar material assignment for large geology and heavily irregular rocks when unique UVs would waste space; still requires UV0 or object-space coordinates for decals and masks.
- UDIM-like tiled authoring is editor-only; runtime must consume packed Unity textures, arrays, or atlases.

Forbidden UV states:

- Overlapping islands on unique-baked textures unless the surface is explicitly mirrored and the normal map supports mirroring.
- Stretched polygons above 15 percent aspect distortion for hero/near assets or 25 percent for distant-only assets.
- UV shells touching atlas border without padding.
- Islands smaller than 4 pixels at target mip 0 for any visible LOD0 detail.
- Texel density mismatch above 20 percent between adjacent hard-surface panels unless the difference is a deliberate material scale change.

Atlas packing must use MaxRects, Skyline, or equivalent rectangle packing. Random shelf packing that leaves large holes is rejected. Padding must be calculated from the highest mip count:

```text
requiredPaddingPixels = max(8, 2 ^ mipCountNeededForSmallestSupportedMip)
```

Baseline padding:

- 512 atlas: 8 px minimum.
- 1024 atlas: 12 px minimum.
- 2048 atlas: 16 px minimum.
- 4096 atlas: 24 px minimum.

Edge bleed is mandatory. Every island must extrude its border color/normal/mask outward through the padding region so lower mips do not create dark seams.

Material assignment must use authored material IDs or deterministic slots:

- Slot 0: primary structural/tissue material.
- Slot 1: exposed cut, bevel, edge, scar, or fracture material.
- Slot 2: secondary trim, gasket, barnacle, mineral vein, or growth plate.
- Slot 3: emissive/bioluminescent/details only when needed.

For generated PBR masks, use project convention unless a shader explicitly states otherwise:

- MRAO texture R = Metallic.
- MRAO texture G = Roughness or smoothness according to shader contract; do not guess.
- MRAO texture B = Ambient occlusion.
- MRAO texture A = Emission mask or packed family mask.

## 7. LOD And HLOD Law

No generated LOD0 asset may be saved without a complete LOD chain unless the asset is an approved single-triangle impostor/card or editor-only debug mesh. Required chain:

- LOD0: near silhouette and baked detail.
- LOD1: preserved silhouette, reduced bevel/branch/ridge density.
- LOD2: coarse silhouette or proxy shell.
- HLOD/impostor: cluster card, simplified shell, or GPU Resident Drawer/GPU instancing route for distant groups.

Decimation must preserve boundary edges, UV seams, hard normals, material borders, sockets, attachment points, silhouette curvature, and collision proxy fit. Quadric Edge Collapse is the default allowed algorithm for arbitrary meshes. Grid/cell collapse is allowed for geology when it preserves silhouette and avoids degenerate triangles. Blind vertex skipping is forbidden.

Default triangle budgets per individual generated asset:

| Asset Class | LOD0 Max | LOD1 Max | LOD2 Max | HLOD/Impostor |
|---|---:|---:|---:|---:|
| Small prop/equipment | 6,000 | 2,000 | 350 | 2-80 |
| Base module piece | 15,000 | 5,000 | 700 | 12-300 |
| Wreckage structural chunk | 25,000 | 8,000 | 1,200 | 12-500 |
| Flora/coral instance | 6,500 | 1,800 | 300 | 2-80 |
| Large flora cluster | 14,000 | 4,000 | 700 | 12-200 |
| Fauna body | 35,000 | 12,000 | 2,000 | VAT/impostor only |
| Geology rock/vent | 18,000 | 7,000 | 1,200 | 12-250 |

These are hard maxima, not targets. If the silhouette reads correctly at lower counts, spend saved budget on material detail, lighting response, better masks, and better atlas packing. If a hero asset needs more, the task must record hardware lane, proof need, and rejection of cheaper silhouette fakes.

LOD switching must use hysteresis and dithered cross-fade where the renderer supports it. Alpha-blended cross-fade is forbidden for dense flora/coral on MX350 because it creates overdraw.

## 8. Rendering Compatibility And Continuous Quality

Generated assets must be compatible with Unity 6000 URP, SRP Batcher, GPU Resident Drawer, and BatchRendererGroup routes. A mesh that looks good but breaks batching, creates material clones, or forces per-instance renderer mutation is rejected.

Rendering gates:

- Materials use stable shader property layouts compatible with SRP Batcher.
- Generated prefabs reference shared `MAT_*` assets. Runtime `renderer.material` clones are forbidden.
- Dense repeated assets use shared meshes and shared materials suitable for GPU Resident Drawer or BRG ownership.
- Mesh bounds are conservative and finite so GPU culling does not pop silhouettes.
- LOD cross-fade uses dithered clip where supported, not alpha blend for dense fields.
- Vertex streams must match shader expectations. Missing tangents, colors, UVs, or masks are validation failures when the material reads them.
- Texture arrays, atlases, or shared material slots are preferred over material-per-variant proliferation.

All generator quality controls must consume a continuous `GlobalQualityWeight` from `0.0` to `1.0`. Labels such as Low, Middle, High, and Ultra are documentation checkpoints, not binary branches. The same asset family must scale through continuous parameters:

- Bevel width and segment count.
- Branch/ridge/blade density.
- Decimation target.
- Texture size and atlas density.
- Optional detail mesh inclusion.
- HLOD/impostor transition distance.
- Validation budget thresholds.

`GlobalQualityWeight` may change fidelity, cadence, density, and presentation cost. It must not change gameplay truth, collision identity, save identity, material role semantics, or vertex channel meaning.

## 9. Collision Proxy Law

**LOD0 visual meshes must never be assigned directly to production `MeshCollider` components.**

Collision is gameplay truth. Visual mesh detail is player belief. Mixing them makes PhysX burn CPU on decorative triangles, creates narrow-phase instability, and turns art polish into frame-time debt.

Required collider routes:

- Industrial/base/equipment: compound `BoxCollider`, `CapsuleCollider`, and `SphereCollider` children fitted offline.
- Rocks/coral/complex geology: convex hull or convex decomposition under 200 triangles total per asset, preferably much lower.
- Large wreckage: modular primitive proxies per room/module plus navigation/SDF proxy where required.
- Flora: no collision by default. Interaction uses coarse trigger capsules/spheres at root or harvest points only.
- Fauna: physics uses capsules, spheres, and hitbox primitives. Render mesh and skinned/VAT mesh stay visual-only.

Collider proxy child names must start with `COL_`. Visual children must start with `VIS_` or `LOD_`. Prefab validation must fail if a `MeshCollider.sharedMesh` points to an LOD0 visual mesh asset or if a convex collision proxy exceeds budget.

## 10. Automated Quality Gates

Before any generator calls `AssetDatabase.SaveAssets`, `PrefabUtility.SaveAsPrefabAsset`, or writes a manifest, it must run validation. Failure aborts save. Warnings are allowed only for non-shipping diagnostic assets.

Minimum mesh validation:

- Vertex count > 0 and index count multiple of 3.
- No index outside vertex range.
- No degenerate triangle: `length(cross(b - a, c - a)) > epsilon`.
- No zero-area UV triangle for textured material surfaces unless triplanar-only and documented.
- No non-finite position, normal, tangent, UV, color, bounds, or matrix.
- Normals normalized within 0.995 to 1.005 length.
- Tangents normalized and finite; handedness is `-1` or `1`.
- Bounds finite and nonzero.
- Vertex color channels match family contract.
- UV0 density and padding pass family gate.
- Submesh count matches material slot declaration.
- LOD chain exists and each LOD is under budget.
- Collision proxy exists and is not LOD0.
- Texture imports pass role settings and compression.
- Generated asset naming follows `GEN_`, `MAT_`, `TX_`, `COL_`, and LOD suffix rules.

Required validation pseudocode:

```text
for each mesh in generatedMeshes:
    assert mesh.vertexCount > 0
    assert mesh.indexCount % 3 == 0
    for each vertex:
        assert finite(position, normal, tangent, uv0, color)
        assert abs(length(normal) - 1) <= 0.005
        assert abs(length(tangent.xyz) - 1) <= 0.005
    for each triangle:
        assert indices inside vertex range
        area = length(cross(p1 - p0, p2 - p0))
        assert area > 0.0000001
    assert bounds finite and extents above 0.001 m
    assert lodTriangleBudget(mesh)
    assert uvContract(mesh)
    assert materialSlotContract(mesh)
assert colliderProxyContract(prefab)
assert textureImportContract(materials)
save only after all asserts pass
```

The validator must emit a compact proof artifact containing asset path, family, seed, triangle counts, material slots, UV utilization, atlas padding, LOD budgets, collider type/triangle count, and validator version. This artifact is evidence, not marketing.

## 11. Black Box And Failure Evidence

Critical generator pipelines must keep the last 300 high-level bake steps in a fixed ring during generation: seed, family, stage, vertex count, triangle count, warning flags, hash, and failure code. On exception, non-finite geometry, or validation abort, the generator must dump the ring to `Docs/AgentLogs/Dump_[GeneratorOrAgentId].bin` or an explicitly owned equivalent.

The accepted answer to a corrupt mesh is never "unknown." The ring must explain the last accepted stage and the first invalid stage.

## 12. Visual Target

Generated assets must read as Deep Sea Noir and NASA-punk:

- Heavy pressure-rated machinery, thickened shells, industrial seams, corrosion, welds, rubber gaskets, oxidized bolts, worn paint, and service panels.
- Abyssal biological mass with roots, scars, translucent membranes, glow masks, cavity darkness, parasites, broken edges, and water-shaped asymmetry.
- Geology that looks stratified, fractured, mineral-stained, pressure-eroded, and cold, not smooth procedural noise.
- Surface and photic-zone assets must read as wet, sunlit or sky-lit, materially rich, scenic, and beautiful: exposed rock has waterline erosion and mineral breakup, shallow flora/coral has pigment and growth logic, and coastline props/terrain are not abyss leftovers with brightness raised.

Clean sterile sci-fi, low-poly toy silhouettes, flat procedural colors, perfect spheres, perfect cylinders, unchipped cubes, and untextured ribbons are rejected.

## 13. Acceptance Statement

A generated model is accepted only when it has correct topology, bevels or organic deformation, calibrated UVs/materials, LODs, collision proxies, validation proof, and a route-specific specialist document compliance note. Without those facts, status is `PENDING VERIFICATION`.
