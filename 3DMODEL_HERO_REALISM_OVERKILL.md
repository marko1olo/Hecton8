# 3DMODEL_HERO_REALISM_OVERKILL

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Evidence class: STATIC_DOC
Scope: hero-grade generated models, close-camera setpieces, premium creatures, premium flora/coral, premium geology, major base modules, command equipment, and any generated asset that must read as handcrafted AAA work.

## First-20 Route Hook

- First-20 moment: close-camera inspection of the first shelter exit, airlock/dock machinery, shallow landmark, premium resource node, major wreck fragment, or threatening fauna silhouette.
- Route blocker removed: texture-hidden primitives, weak hero silhouettes, and unproven close-camera assets cannot carry the product-facing opening route.
- Proof class: STATIC_DOC until reference contracts, bake manifests, multi-light renders, wireframe/collider views, Unity import evidence, and route captures exist.

## 0. Prime Hero Asset Law

Hero assets are not "better generated meshes". They are player-inspectable product-defining objects. If a hero asset does not survive close camera, grazing light, material/mask inspection, wireframe inspection, LOD transition, collision overlay, and in-scene scale proof, it is not a hero asset.

The standard is not "less bad procedural output". The standard is believable macro form, functional construction, material history, optimized LODs, and premium surface response that can stand beside handcrafted AA/AAA art. Optimization changes density and proof cost; it does not permit primitive silhouettes, blurry textures, texture-hidden geometry, or low-poly read.

## 1. Purpose

Baseline generator compliance prevents defective assets. It does not automatically create world-class realism. Hero realism requires a stricter pipeline: functional design, reference discipline, high-poly source generation, controlled retopology, baked detail maps, layered materials, decal/trim support, render review, and rejection of primitive silhouette language.

The rule is simple: if the player can inspect the object, the object must carry believable macro form, meso construction detail, micro surface response, material history, and functional reason. A smooth blob with a noise texture is rejected. A cube with bevels is still rejected if it has no pressure-rated construction logic, no seams, no scale cues, no material transitions, and no wear story.

## 2. Hero Entry Conditions

This file is mandatory when any condition is true:

- Asset appears within 8 meters of the camera in normal gameplay.
- Asset is used in a trailer, cutscene, menu, codex inspection, interactable station, boss/fauna encounter, or critical mission location.
- Asset occupies more than 4 percent of screen height for more than 2 seconds.
- User or task asks for best, cinematic, premium, ultra, realistic, AAA, photorealistic, or non-low-poly output.
- Asset defines the visual identity of HECTON-8: base module, airlock, submarine equipment, leviathan/fauna body, coral forest, thermal vent, cave entrance, wreckage, command device.

Hero classification does not allow runtime generation. It increases offline bake effort, not runtime authority.

## 2A. Runtime And Hot-Path Boundary

Hero status raises offline source, bake, render-proof, LOD, collider, and material requirements. It never grants runtime authority to build or repair the asset.

Hot paths must not generate hero mesh detail, retopologize, decimate, bake normals/AO/curvature, fit colliders, synthesize masks, swap material channel semantics, search visual triangles for interaction, or create runtime material clones. Runtime may load the serialized hero package, choose approved LOD/variant/residency, drive authored shader parameters, and read named anchors or collider proxies. `GlobalQualityWeight` scales fidelity selection only; it must not change collision truth, sockets, prefab identity, save identity, or gameplay facts.

## 3. Realism Stack

Every hero asset must be built in four visible layers:

- Macro form: silhouette, mass, asymmetry, proportion, readable function, species/body plan, geological strata, or industrial assembly.
- Meso form: panels, seams, ribs, gaskets, welds, chips, branch sockets, bone plates, folds, vents, cracks, sediment shelves.
- Tertiary detail: bolts, decals, scratches, pores, scale edges, coral cups, cable clamps, tool marks, dented trim, mineral crust.
- Micro response: normal-map grain, roughness variation, AO cavities, curvature wear, wetness, biolum masks, rust bloom, fine sediment.

The generator must not spend triangles on micro response that belongs in normal/detail maps. The generator must not fake macro form using only textures. Macro silhouette is geometry. Micro grain is texture. Meso detail may be geometry, trim, decal, or normal bake depending on camera distance and budget.

## 4. Reference And Plausibility Contract

Every hero generator run must declare a reference contract:

- Industrial reference: pressure vessel, submarine hatch, oil-rig machinery, NASA hardware, diving gear, ship wreckage, lab equipment, ceramic insulation, rubber seals, hydraulic systems.
- Organic reference: deep-sea animal, coral, kelp, bone, shell, tendon, cartilage, root, fungal shelf, vascular tissue, biolum organ.
- Geological reference: basalt, shale, sediment, sulfide chimney, hydrothermal vent, ore vein, cave dripstone, eroded cliff, pressure-fractured stone.
- Material age: new, maintained, neglected, corroded, sediment-buried, biofouled, fractured, burned, mineralized, pressure-deformed.
- Scale witness: bolts, panel thickness, pores, fins, sediment grains, shell ridges, cable diameter, hatch radius, rib spacing.

Random generation is allowed only after the reference contract exists. The seed may vary proportions and damage; it must not invent visual logic from noise alone.

## 5. High-Poly Source Rule

Hero assets require an offline high-poly source, even if the final shipped mesh is optimized. The high-poly source may be generated by:

- CSG/SDF constructive passes for hard-surface panels, trims, welds, dents, recesses, and damage.
- Subdivision or bevel-expanded procedural meshes for manufactured parts.
- Voxel/SDF sculpt fields for rocks, vents, coral masses, shells, and organic deformable bodies.
- L-system or growth graph expansion followed by branch union, smoothing, knuckle inflation, and cavity baking.
- Procedural kitbash using approved motifs, sockets, trim sheets, and detail modules.

The high-poly source exists to bake normal, AO, curvature, thickness, cavity, height, and material masks into the final asset. It must not be shipped directly unless it passes the LOD0 budget and proof route.

## 6. Retopology And Shipped Mesh Rule

The shipped LOD0 must be a deliberate game mesh, not a raw decimated sculpt. Retopology must preserve:

- Silhouette edges visible from gameplay angles.
- Deformation loops for fauna, fins, tendrils, jaws, roots, tentacles, and moving tissue.
- Hard-surface support loops around bevels, sockets, hatches, gaskets, rails, and panel breaks.
- UV seams placed in low-visibility or material-boundary locations.
- Material borders, trim boundaries, decal zones, and high/low-poly bake cages.
- Boundary edges required by modular assembly.

Blind decimation is rejected for hero LOD0. Quadric Edge Collapse is allowed only after protected-edge masks are defined. The final mesh must look designed in wireframe: no random triangle soup on readable hard-surface panels, no star-poles on deforming fauna joints, no faceted cylinders in close view.

## 7. Silhouette Budgets

Hero realism spends geometry where silhouette changes:

- Round pipes, cables, tentacles, fins, and appendages close to camera should use enough radial segments to avoid visible faceting under grazing highlights.
- Large metal panels need bevel bands and thickness, not single flat planes.
- Organic branches need nonuniform cross-sections, root swelling, tip taper, and irregular asymmetry.
- Rocks need broken planes, strata ledges, chipped rims, and undercuts, not smoothed noise lumps.
- Coral needs cups, pores, branch collars, growth ridges, and dead/broken sections.

If triangle budget is tight, preserve silhouette first, then bevels, then material borders, then meso geometry. Push scratches, pores, grain, small pits, and fine rust into normal/detail maps.

## 8. Anti-Low-Poly Rejection Rules

Reject the asset if any condition is true:

- It reads as a primitive sphere, capsule, cylinder, cone, cube, torus, ribbon, or flat plane after textures are disabled.
- It depends on a single noise texture to look detailed.
- It uses perfect symmetry where age, biology, pressure, or corrosion should break symmetry.
- It has broad unbroken surfaces larger than 0.35 m on hero hard-surface assets without seams, trim, decals, dents, gaskets, thickness, or material change.
- It has organic tubes with constant radius and no anatomical landmarks.
- It has rocks that read as smoothed blobs or low-poly faceted boulders.
- It uses uniformly saturated emission with no organ, lens, vent, circuit, or biological source.
- It has visible faceting on curves within 8 meters of camera.
- It uses LOD0 visual mesh as collision.
- It has no proof render under grazing light.

Texture quality cannot rescue a failed silhouette. High polygon count cannot rescue a failed material story.

## 9. Hard-Surface Hero Rules

Hard-surface hero assets must communicate manufacturing and maintenance:

- Every large object has visible assembly logic: panels, brackets, welds, gaskets, bolts, rails, latches, access covers, hinges, sockets, service marks.
- Pressure-rated modules must have thickness cues around doors, windows, vents, and seams.
- Edge bevels must vary by function: structural bevels are broad, panel bevels are tighter, worn edges are chipped, rubber edges are softer.
- Wear must be directional and plausible: hands polish grips, boots scuff thresholds, water drips downward, sediment collects in cavities, exposed rims brighten.
- Trim sheets and decals must be aligned to UV/material zones, not randomly stamped across geometry.
- Greebles are allowed only when they imply cooling, fastening, sensing, pumping, insulation, cabling, or pressure sealing.

Procedural hard-surface generators should build from a shape grammar:

1. Primary mass and pressure envelope.
2. Functional cut lines and sockets.
3. Bevel/support-loop construction.
4. Panel and trim hierarchy.
5. Fasteners, gaskets, handles, rails, vents, and clamps.
6. Damage, corrosion, sediment, decal, and curvature masks.
7. LOD, collider proxy, material manifest, and render proof.

## 10. Organic Hero Rules

Organic hero assets must communicate anatomy or growth history:

- Fauna needs skeleton/body plan anchors: skull/jaw, spine, ribs, fins, joints, tendons, plates, eyes/lures, organ sacs, scars.
- Flora/coral needs growth anchors: root mass, branch collars, dead zones, new growth tips, cavities, pores, calcified rims, sediment contact.
- No constant-radius tubes. Radius changes must follow growth, load, age, or fluid pressure.
- Cross-sections should be oval, pinched, ridged, scarred, twisted, or flattened where plausible.
- Joints and bending regions need edge loops and texture mask alignment.
- Vertex color R/G/B/A semantics from the root bible remain mandatory.
- Bioluminescence must follow organs, veins, tips, cups, lures, or stress zones.

Organic generators should build with a semantic graph: root/skeleton nodes, flow direction, branch radius, age, scar fields, cavity fields, emission organs, and material regions. Noise may perturb these fields but must not replace them.

## 11. Geology Hero Rules

Geology hero assets must communicate physical formation:

- Stratified rock must have layer continuity across faces.
- Basalt and cliff faces need fracture planes, chipped edges, ledges, sediment shelves, and wet cavities.
- Hydrothermal vents need mineral chimneys, flow openings, crust rings, hot cracks, sulfur/oxide bands, and AO-dark interiors.
- Ore nodes need localized inclusions, host-rock borders, and nonuniform extraction scars.
- Cave entrances need undercuts, ceiling stress fractures, floor debris, waterline marks, and scale witnesses.

Rocks cannot be Perlin blobs. Use layered SDF, Voronoi fracture, plane clipping, erosion masks, sediment accumulation, and triplanar material projection with localized unique decals.

## 12. Bake And Map Requirements

Hero assets require these offline bakes unless explicitly impossible:

- Normal from high-poly source.
- Ambient occlusion.
- Curvature or convexity.
- Cavity/dirt mask.
- Thickness or translucency proxy for organic material when used by shader.
- Material ID mask.
- Emission mask where applicable.
- Position/gradient mask for drips, sediment, root/tip changes, or biological phase.
- Optional bent normal for premium static assets if shader route supports it.

Bakes must be generated from the actual high-poly and low-poly relationship. Painting random masks without geometry basis is rejected unless the mask is a decal or narrative mark.

## 13. Trim, Decal, And Detail Strategy

World-class assets use layered reuse:

- Trim sheets for industrial bevel strips, bolts, rails, vents, rubber seams, warning bands, and access edges.
- Decals for serial labels, scratches, leaks, algae patches, burn marks, mineral deposits, bite marks, and tool damage.
- Shared detail maps for metal grain, rubber ribbing, tissue pores, coral pitting, basalt grain, sediment dust.
- Unique bakes only where the object needs custom identity: hero faces, wounds, control panels, logos, key damage, large organic organs.

The generator must pick the cheapest layer that preserves the intended read. Do not model a 2 mm scratch. Do not texture a 20 cm broken silhouette. Do not use a unique 4K map for repeated cable clamps.

## 14. Render Proof Gate

Every hero asset requires render proof before production acceptance:

- Neutral studio light.
- Grazing rim light that exposes bevels and faceting.
- Low-contrast underwater blue-green light.
- High-contrast emergency red/orange light if the asset appears in alert scenes.
- Albedo-only view.
- Normal-only or matcap-like inspection.
- Roughness/metallic/AO/emission mask inspection.
- Wireframe or vertex-density inspection.
- LOD0/LOD1/LOD2 transition view.
- Collider proxy overlay view.

If the asset fails in any proof view, it is not accepted. A beautiful beauty shot with hidden wireframe, hidden masks, and hidden collider does not prove production quality.

## 14A. Proof Artifacts

Hero asset work must provide a proof packet:

- reference contract with image/source categories and material age;
- high-poly source path or generator manifest;
- shipped LOD0/LOD1/LOD2 triangle counts and protected-edge notes;
- UV density and material slot report;
- bake manifest for normal, AO, curvature, cavity, material ID, emission/thickness where used;
- texture resolution, compression, mip/padding, channel map, and atlas/trim/decal list;
- collision proxy overlay or collider manifest;
- neutral, grazing, underwater, albedo-only, normal/matcap, mask, wireframe, LOD transition, and collider proof renders;
- Compact and High/Ultra in-scene captures when the asset is player-facing.

Claims of "AAA", "realistic", "hero", "final", or "production ready" are invalid without these artifacts or an explicit `PENDING VERIFICATION` label.

## 15. Continuous Overkill Scaling

Hero scaling is continuous through `GlobalQualityWeight`:

- 0.0 to 0.25: preserve macro silhouette, one bevel band on visible hard edges, compact atlas, baked AO, strong LOD falloff.
- 0.25 to 0.5: add meso silhouette cuts, better bevel segmentation, more material masks, clearer decals, stronger normal detail.
- 0.5 to 0.75: add protected hero contours, richer high-poly bake, more decal layers, stronger organic landmarks, denser near LOD.
- 0.75 to 1.0: add hero-only sculpt/bake precision, premium decals, refined curvature wear, richer emission organs, tighter LOD transitions.

This scale changes fidelity and bake density only. It does not alter gameplay truth, collision identity, prefab ownership, runtime generation law, or material channel semantics.

## 16. Implementation Order

Hero model implementation must proceed in this order:

1. Declare family file, hero reason, reference contract, camera distance, and material target.
2. Generate macro blockout and reject if primitive read remains after textures are disabled.
3. Generate high-poly source with functional/biological/geological detail.
4. Generate retopologized LOD0 with protected silhouette, seams, loops, and material borders.
5. Bake normal, AO, curvature, cavity, material ID, and optional thickness/emission masks.
6. Build UVs, trims, decals, atlas, and material family using the texture playbook.
7. Generate LOD1/LOD2/HLOD/impostor preserving silhouette and material boundaries.
8. Generate `COL_*` proxies independent of visual LOD0.
9. Run mesh, texture, material, LOD, collider, and render proof validators.
10. Save only validated `.mesh`, `.prefab`, `.mat`, `.png`, `.asset`, and proof reports.

Any generator that skips high-poly source, bake maps, render proof, or collider separation is not a hero generator. It is a prototype generator.

## 17. Acceptance Sentence

A hero generated asset is accepted only when reference contract, high-poly source, deliberate retopology, protected silhouette, bake maps, trim/decal strategy, LOD chain, collision proxy, material proof, and multi-light render proof all agree that the object survives close inspection without reading as primitive, low-poly, texture-hidden, or runtime-corrected.
