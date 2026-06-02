# HECTON-8 Procedural Asset Pipeline

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: offline generation of procedural meshes, textures, materials, prefabs, LODs, colliders, manifests, and proof artifacts.

## Prime Law

Procedural assets are generated only in the Unity Editor or external offline tooling. Runtime loads finished `.mesh`, `.prefab`, `.mat`, `.asset`, and texture files. Runtime code may select, stream, cull, instance, animate shaders, and swap LODs, but it must not author mesh topology, unwrap UVs, synthesize textures, bake maps, cook colliders, or allocate/grow geometry buffers during gameplay.

Any procedural asset task starts here, then routes into the matching specialist bible:

- Mesh, texture, material, LOD, collision fundamentals: `3dmodel.md`.
- Hard-surface modules, wreckage, base shells, submarine pieces: `3DMODEL_HARD_SURFACE_MODULES.md`.
- Flora, coral, kelp, abyssal organic growth: `3DMODEL_FLORA_CORAL.md`.
- Fauna bodies, skeletons, deformation topology: `3DMODEL_FAUNA.md`.
- Rocks, cliffs, vents, strata, seafloor geology: `3DMODEL_GEOLOGY_ROCKS.md`.
- Equipment, tools, props, interactable machines: `3DMODEL_EQUIPMENT_PROPS.md`.
- UV, atlas, PBR texture, import, material rules: `3DMODEL_TEXTURES_MATERIALS.md`.
- Texture family source generation and AI/procedural source rules: `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`.
- Close-camera hero realism: `3DMODEL_HERO_REALISM_OVERKILL.md`.

If a generated object can be touched, collided with, scanned, salvaged, repaired, entered, cut, opened, or used as cover, its visual mesh is not the only artifact. It also needs collider proxies, interaction anchors, material IDs, LODs, bounds, prefab metadata, and validation output.

## Required Output Package

Every generator must emit a complete asset package, not an isolated mesh.

Required files or serialized objects:

- `MESH_<Family>_<Name>_LOD0.asset`: highest visual runtime mesh.
- `MESH_<Family>_<Name>_LOD1.asset`: mid-distance visual mesh.
- `MESH_<Family>_<Name>_LOD2.asset`: far visual mesh or billboard proxy when approved.
- `COL_<Family>_<Name>.prefab` or collider children inside the final prefab: primitive compound or convex proxy only.
- `MAT_<Family>_<SurfaceRole>.mat`: shared SRP Batcher-compatible materials.
- `TEX_<Family>_<AtlasOrUnique>_<Role>.png` or existing project texture references.
- `PF_<Family>_<Name>.prefab`: final runtime prefab with LODGroup, renderer/material slots, colliders, anchors, layers, tags, and metadata.
- `MANIFEST_<Family>_<Name>.asset` or JSON: seed, generator version, source references, triangle counts, material slots, texture set, collider budget, proof paths, and validation hash.

Generated object families must never create one material per instance unless the asset is explicitly approved as a hero unique. Material variety comes from atlases, trim sheets, vertex colors, secondary UVs, instanced shader properties, and shared texture families.

## Deterministic Source Contract

Every procedural asset must be reproducible. The generator must store deterministic seed, generator script name and semantic version, `GlobalQualityWeight`, asset family, intended scale in meters, camera distance class, target platform lane, source texture/reference IDs, and validation summary hash.

No generated mesh may depend on random process state, current scene object search order, editor selection, wall-clock time, or nondeterministic collection iteration for final topology. If artist variation is needed, variation is a named seed, not hidden chance.

## Generation Order

The generator must run in this order:

1. Read a deterministic manifest and source references.
2. Build high-level shape grammar: modules, branches, bones, strata, sockets, anchors, and silhouette landmarks.
3. Create high-detail source geometry or parametric surface fields.
4. Apply topology rules from the family bible: bevels, welds, loop flow, branch unions, fracture planes, or skeletal loops.
5. Generate UVs and material IDs.
6. Bake normals, AO, curvature, thickness, emission masks, and vertex color channels.
7. Build shared materials and texture references.
8. Generate LOD chain using silhouette-preserving decimation.
9. Generate collision proxies independent from visual LODs.
10. Assemble prefab with LODGroup, renderers, colliders, anchors, layers, tags, and metadata.
11. Run validation gates before saving.
12. Save assets.
13. Emit proof artifacts: screenshots, wireframe/render captures when required, validation report, and manifest.

Skipping a step is allowed only if the family bible explicitly says that step is not applicable. "Small asset" is not an exemption from UVs, normals, tangents, material IDs, LOD policy, or validation.

## Continuous Quality Scaling

`GlobalQualityWeight` is continuous. It may scale density, bevel segment count, LOD distance, texture size, decal count, proof requirement, and bake precision. It must not alter gameplay truth, collider identity, interaction anchors, save identity, DTO layout, or route ownership.

Quality lanes:

- Compact: strongest silhouette per triangle, shared atlases, one to two bevel segments where required, strict collider simplicity, aggressive LOD falloff, no unique hero textures unless the asset is narrative-critical.
- Middle: richer silhouette breaks, more material zones, stronger AO/curvature bakes, better LOD transitions, more local decals inside shared atlases.
- High: denser bevels, richer organic branch/strata detail, larger atlases where justified, stronger render proof, better close-camera normals.
- Ultra: offline visual overkill for hero assets: dense source geometry, superior bakes, trim/decal layering, near-field micro detail, and render proof. Runtime still receives static optimized assets.

Quality scaling must be monotonic. Raising quality may add visual detail but must not remove anchors, change prefab names, change collider truth, or create a different gameplay route.

## Mesh Family Routing

Hard-surface procedural objects must read as manufactured pressure equipment. The source shape cannot remain a box with panels painted on it. The generator must model bevels, flanges, reinforcement ribs, bolt fields, gasket lips, service cuts, cable glands, worn edges, and collision-safe silhouettes.

Organic flora and coral must read as grown under current, pressure, and feeding behavior. The generator must model roots, holdfasts, taper, branch hierarchy, knuckles, damaged tips, flow-facing asymmetry, and vertex-color semantic channels for shader animation and lighting masks.

Fauna must read as bodies with load paths, locomotion, organs, and threat function. The generator must model skeletal landmarks, fin or limb bases, deformation loops, sensory organs, jaw/appendage sockets, scar/wear masks, and LODs that preserve readable threat silhouette.

Geology must read as material history. The generator must model strata, fractures, sediment ledges, wet cavities, mineral seams, pressure breaks, scale witnesses, and route-facing landmarks. A rock is not accepted if it is a noise sphere with a rock material.

Equipment and props must read as used tools. The generator must model grip zones, fasteners, labels, seals, cables, latch arcs, heat/wear zones, service panels, and interaction anchors. A prop that cannot imply how a human used it is not finished.

## Texture And Material Integration

Procedural generation does not justify synthetic flat color. Each asset must use the existing project texture library when a suitable authored material exists, or a texture family generated under `3DMODEL_TEXTURE_GENERATION_PLAYBOOK.md`.

Material assignment rules:

- assign material IDs by physical surface role, not by random face groups;
- use trim sheets for repeated hard-surface edges, rails, labels, and brackets;
- use atlas pages for families of small props, coral clusters, bolts, plates, and debris;
- use triplanar projection only for approved large geology or dirty/wet overlays where visible seams would be worse than UV cost;
- use vertex colors and secondary UV channels for wear, AO, current sway, emission phase, grime, and wetness masks;
- pack masks consistently with the texture bible; do not invent per-generator channel meanings.

AI-generated textures are source candidates, not automatic final assets. Reject them if they contain baked lighting, fake shadows, random symbols, unreadable labels, perspective artifacts, inconsistent scale, false normal information, or material channels that do not match physical surface behavior.

## LOD And Bounds

Every saved prefab must have an LOD policy. LOD0 is close/interaction only. LOD1 preserves silhouette and major material zones. LOD2 preserves navigation and threat read, not decorative detail. Tiny props may share clustered LODs or be merged into an HLOD group, but the decision must be recorded in the manifest.

Bounds must be conservative and finite. Animated shader sway, fauna appendage motion, projected wetness, and emissive pulses must fit inside runtime culling bounds. A beautiful mesh that disappears because bounds ignored shader motion is a failed asset.

## Collision And Interaction Package

Visual mesh triangles are presentation. Collision is gameplay truth.

The generator must create primitive compound colliders for manufactured objects, convex hull proxies for irregular objects, interaction anchors named by verb, socket transforms for modular assembly, layer/tag assignment before prefab save, and optional navigation blockers or occluders as separate simplified data.

Required interaction anchor names include `ANCHOR_Scan`, `ANCHOR_Cut`, `ANCHOR_Weld`, `ANCHOR_Loot`, `ANCHOR_Pry`, `ANCHOR_Open`, and `ANCHOR_Repair` when those verbs apply.

`MeshCollider` using LOD0 is rejected. Runtime collider cooking is rejected. Runtime searching for interaction anchors is rejected; anchors must be serialized and registered through cold setup or manifest-driven references.

## Prefab Assembly Law

The final prefab must be boring for runtime code to consume.

Required prefab properties:

- stable root name and GUID;
- `LODGroup` or documented HLOD/merge exemption;
- renderer material slot order matching manifest;
- static flags where valid;
- colliders on correct child objects and physics layers;
- no editor-only generator component in the runtime prefab unless stripped or marked editor-only;
- no generated per-instance material clones;
- no unbounded particle, audio, UI, or script side effects attached by the asset generator;
- optional metadata component or ScriptableObject reference for cold identity only.

The prefab is not accepted if a gameplay system must fix it during play.

## Validation Before Save

Before `AssetDatabase.SaveAssets`, the generator must validate:

- all vertices, normals, tangents, UVs, colors, weights, bounds, and indices are finite;
- triangle count is within family and LOD budget;
- zero degenerate triangles;
- no inverted or broken winding except deliberate double-sided shells documented by family bible;
- normals are normalized;
- tangents are normalized and handedness is valid;
- UV density is inside allowed range;
- atlas islands have required padding;
- material slot count is bounded and named;
- vertex color semantics match family requirements;
- LODs exist or exemption is recorded;
- collision proxy exists and does not use LOD0;
- prefab layer/tag/static flags are correct;
- texture import settings match role;
- manifest is present and hashable.

On validation failure the save is aborted. The generator must write a failure report and, for critical corruption such as NaN data or invalid index buffers, dump a black-box artifact under `Docs/AgentLogs/`.

## Proof Artifacts

A generator report that only says "created assets" is invalid.

Minimum proof:

- asset paths;
- seed and generator version;
- triangle counts per LOD;
- material and texture references;
- collider count and type summary;
- UV density and atlas utilization summary;
- validation pass/fail report;
- screenshot or render capture for visual assets;
- wireframe/render proof for hero or close-camera assets;
- profiler/Frame Debugger proof only when runtime behavior is changed.

If proof is missing, the asset is not production-ready, even if the prefab exists.

## Rejection List

Reject immediately:

- runtime mesh generation for final gameplay assets;
- runtime texture synthesis or UV unwrapping;
- runtime collider cooking;
- LOD0 used as physics collider;
- primitive spheres, boxes, cylinders, tubes, ribbons, or blobs sold as final visuals;
- high triangle counts without silhouette intelligence;
- flat color materials;
- AI texture output with baked lighting or false PBR channels;
- UV islands with visible stretch, overlap, or insufficient padding;
- material-per-instance spam;
- generated prefabs that need hot-path scene search or setup repair;
- temporary art committed as final generated content.

## Acceptance Sentence

A procedural asset is accepted only when it is deterministic, offline-authored, physically readable, materially credible, efficiently serialized, collision-safe, LOD-complete, proof-backed, and aligned with HECTON-8 pressure, machinery, salvage, and black-water taste.
