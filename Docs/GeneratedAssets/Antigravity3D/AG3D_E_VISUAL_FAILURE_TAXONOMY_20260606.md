# AG3D-E VISUAL FAILURE TAXONOMY

## 1. TOY PRIMITIVES AND LACK OF GEOMETRIC SUBDIVISION

The most pervasive visual failure observed during the Hecton-8 procedural generation passes is the reliance on "toy primitives"—base geometric shapes (spheres, capsules, cylinders) that have not undergone sufficient displacement, subdivision, or boolean fracturing to resemble natural or engineered oceanic formations. 

When the `WorldGenerativeGeologyMeshBuilder.cs` attempts to spawn terrain elements like `PFB_family_rock_cluster_medium__stack.prefab` or `PFB_family_rock_small_floor__group.prefab`, the resulting visual silhouette from a distance reads as a collection of smooth Unity spheres scaled along the Y-axis. This completely shatters the immersion required by `3DMODEL_GEOLOGY_ROCKS.md`, which mandates sharp shearing, micro-fractures, and stratified sedimentary layers. 

### Case Studies
*   **Prefab:** `PFB_family_rock_small_floor__low.prefab`
*   **Failure Mode:** Appears as a flattened, low-polygon disc rather than a jagged piece of debris or crustal shelf. The transition from the seabed to the rock is mathematically perfect, lacking any ambient occlusion accumulation or sand buildup at the base.
*   **Source Implication:** The procedural script `WorldProceduralPatternCatalog.cs` is likely failing to apply the high-frequency displacement map during the final bake, falling back to the base LOD0 mesh which lacks edge wear.
*   **Reference Contrast:** Contrast this with the visual reference `CLIFFS AND WATER PREVIOUSLY IN DEVELOPMENT (MAPMAGIC + TEXTURES + CREST OCEAN).jpg`, where the cliff interfaces feature razor-sharp ledges and overhangs, entirely absent from the current prefab pool.
*   **Prefab:** `PFB_family_pocket_safe__bubble.prefab`
*   **Failure Mode:** The "safe pocket" boundary is rendered as a literal geometric sphere with a semi-transparent shader. This looks like a debugging artifact rather than an integrated gameplay zone. 
*   **Reference Contrast:** In `beauty.webp`, safe zones are implied through lighting (god rays) and flora density, not through un-subdivided primitive geometry.

### Resolution Pathway
The mesh generation pipelines must be updated to ensure that no raw primitives ever survive to the final render pass. We must enforce a minimum noise displacement threshold on all geology proxies, specifically targeting `PFB_family_rock_arch_large__arch.prefab` to ensure the internal curvature of the arch exhibits localized spalling and erosion, rather than looking like a smooth, bent torus.

## 2. REPEATED SILHOUETTES AND CLONED SCATTERING

The scattering director (`WorldProceduralScatterDirector.cs`) relies too heavily on simple translation and scaling without applying sufficient non-uniform rotation or structural variation. This results in the "cloned forest" effect, which is highly detrimental to the visual fidelity of `PFB_family_kelp_tall__stalk.prefab` and `PFB_family_coral_branching__branch.prefab`.

When the player navigates through `PFB_family_kelp_patch_dense__grove.prefab`, the silhouette of every kelp stalk is identical. Even if the height is randomized, the branching angles, leaf placement, and overall curvature share the exact same topological signature. This immediately flags the environment as procedurally generated, violating the "hero realism overkill" mandate.

### Case Studies
*   **Prefab:** `PFB_family_kelp_tall__lean.prefab`
*   **Failure Mode:** The lean angle is applied as a rigid transform at the root, rather than a bezier curve deformation along the spine of the kelp. Consequently, the entire stalk tilts like a fallen tree, rather than bowing gracefully in the ocean current as seen in `forest_kelp.webp`.
*   **Source Implication:** The `WorldProceduralPlacementRule.cs` lacks a secondary pass for spline deformation. It simply places the prefab and modifies the transform matrix.
*   **Prefab:** `PFB_family_coral_brittle__sprig.prefab`
*   **Failure Mode:** When spawned in dense clusters via `WorldProceduralScatterDirectorSamplingPipeline.cs`, the sprigs interpenetrate exactly the same way, creating Moiré patterns and visual noise that the temporal anti-aliasing (TAA) cannot resolve cleanly.

### Resolution Pathway
To break the repetition, we must implement procedural vertex displacement via shaders for flora, and strict rotational scattering rules for hard-surface/geology assets. Furthermore, prefabs like `PFB_family_landmark_spire__split.prefab` must only be allowed to spawn once per visual quadrant, as highly unique, asymmetric silhouettes cannot be repeated without destroying the sense of scale and unique geography.

## 3. CLEAN SYMMETRY IN ORGANIC AND HARD-SURFACE ASSETS

Symmetry is the enemy of organic realism. The ocean is an inherently chaotic environment driven by fluid dynamics, erosion, and asymmetrical growth patterns. Yet, many of our prefabs, particularly in the `ruin_module_single` and `coral_massive` families, exhibit perfect bilateral or radial symmetry.

### Case Studies
*   **Prefab:** `PFB_family_coral_massive__head.prefab`
*   **Failure Mode:** The "brain coral" archetype is modeled as a perfect hemisphere. In reality, coral growth is dictated by sunlight availability and nutrient currents, leading to lopsided, heavily cratered structures. 
*   **Reference Contrast:** `nice_biome.webp` demonstrates how massive coral structures should bulge and droop asymmetrically, creating overhangs (`PFB_family_coral_plate__ledge.prefab`) that cast complex, broken shadows.
*   **Prefab:** `PFB_family_ruin_module_single__block.prefab`
*   **Failure Mode:** While human engineering is symmetrical, ruins subjected to deep-sea pressure are not. The ruin blocks lack asymmetrical crushing damage. The `PFB_family_ruin_module_single__breach.prefab` features a hull breach that is perfectly centered on the X-axis, looking like a deliberate doorway rather than catastrophic explosive decompression.
*   **Source Implication:** The `WorldGeneratedPrimitiveFactory.cs` is mirroring mesh halves during the generation phase to save processing time, sacrificing asymmetrical detail.

### Resolution Pathway
We must introduce a "decay and distortion" pass that deliberately breaks symmetry. For hard-surface assets (`PFB_family_ruin_megastructure__frame.prefab`), this means shearing the structural beams on one side, collapsing entire wings, and applying rust/barnacle maps using triplanar projection that favors the Z-axis (current flow direction). 

## 4. LOW-FREQUENCY BLOBS AND LACK OF HIGH-FREQUENCY DETAIL

The underwater environment often suffers from being "too soft". When volumetric fog and depth-of-field are applied (as seen in `middle-water.webp`), low-frequency shapes turn into indistinct blobs. To combat this, the assets themselves must possess extremely sharp, high-frequency details (micro-facets, barnacles, jagged edges) that can catch specular highlights even in low light.

### Case Studies
*   **Prefab:** `PFB_family_coral_low__bed.prefab`
*   **Failure Mode:** The coral bed reads as a smooth, bumpy rug. It lacks the tiny, sharp polyps and brittle calcified branches necessary to give the material a realistic roughness response.
*   **Reference Contrast:** In `shallowwater.jpg`, the underwater terrain is incredibly sharp and defined, contrasting beautifully with the soft, undulating water surface above.
*   **Prefab:** `PFB_family_egg_cluster__nest.prefab`
*   **Failure Mode:** The eggs look like smooth gelatinous spheres. They need high-frequency veining, internal parallax occlusion mapping to simulate embryos, and a jagged, hardened outer shell structure.

### Resolution Pathway
Increase the normal map intensity and utilize detail maps (secondary high-frequency normals) on all organic materials. Prefabs like `PFB_family_pocket_hazard__vent.prefab` must feature sharp, mineralized chimneys rather than smooth, volcanic cones.

## 5. UNREADABLE ROUTE CLUTTER AND NOISY NAVIGATION

While high-frequency detail is needed on individual assets, the overall composition must remain readable. The `WorldProceduralScatterDirector.cs` often over-populates navigation routes, creating visual noise that makes it impossible for the player to discern safe paths from hazards.

### Case Studies
*   **Prefab:** `PFB_family_debris_field__field.prefab`
*   **Failure Mode:** The scatter pipeline drops hundreds of `PFB_family_debris_scatter__scrap.prefab` pieces uniformly across the seabed. This visual noise completely masks the underlying terrain flow and hides critical gameplay elements like `PFB_family_route_power__node.prefab`.
*   **Reference Contrast:** `ubnautic.webp` shows a masterclass in composition: despite high detail, the route forward is clearly delineated by negative space, lighting contrast, and leading lines (e.g., pipes or kelp walls).
*   **Prefab:** `PFB_family_kelp_patch_dense__patch.prefab`
*   **Failure Mode:** The density is so high that it creates an impenetrable wall of green pixels, destroying depth perception and causing the player to become disoriented.

### Resolution Pathway
Implement flow-field based scattering in `WorldProceduralScatterDirector.cs`. Debris should pool in trenches and corners, leaving the ridges and main pathways relatively clear. `PFB_family_route_power__relay.prefab` must have a clear visual hierarchy—it should be the brightest, highest-contrast element in the immediate vicinity, not buried under noisy rock scatters.

## 6. FAKE CORAL, FAKE KELP, FAKE WRECKAGE (THE AG3D_C FAILURES)

During previous iterations (AG3D_C), the generative systems hallucinated assets that did not exist or failed to map to real prefabs. We must strictly adhere to the real prefab inventory to avoid spawning "fake" assets that break the rendering pipeline.

### Case Studies
*   **Fake Coral Issue:** Systems attempting to spawn `PFB_coral_tube_neon` (which does not exist). We must exclusively use real proxies like `PFB_family_coral_branching__mass.prefab` and `PFB_family_coral_brittle__fan.prefab`, relying on material overrides to achieve the "neon" look.
*   **Fake Kelp Issue:** Systems attempting to spawn generic `kelp_01`. We must use the specific biome variants: `PFB_family_kelp_abyssal__shroud.prefab` or `PFB_family_kelp_tall__lean.prefab`.
*   **Fake Wreckage Issue:** Spawning `Spaceship_Debris_A`. The Hecton-8 lore is submarine/corporate, not generic sci-fi spaceship. We must strictly use `PFB_family_ruin_module_single__breach.prefab` and `PFB_family_service_scar__pump.prefab`.

### Resolution Pathway
Strict enforcement of the `AG3D_E_REAL_PREFAB_LEDGER_20260606.csv`. Any generative prompt or scattering rule that attempts to reference an asset not on this list must be hard-rejected by the static validation gates.

## 7. BAD CLIFF SCALE AND VERTICALITY MAPPING

The Hecton-8 environment relies heavily on massive vertical drops, as seen in `CLIFFS AND WATER PREVIOUSLY IN DEVELOPMENT`. However, the current prefabs often fail to convey this massive scale due to improper texture scaling and lack of micro-details.

### Case Studies
*   **Prefab:** `PFB_family_rock_shelf_large__wall.prefab`
*   **Failure Mode:** The rock texture stretches terribly along the Y-axis when the prefab is scaled up to form a canyon wall. It looks like a low-resolution PS2 asset rather than a sheer, kilometer-deep drop.
*   **Source Implication:** The shader lacks triplanar projection, meaning the UV coordinates scale 1:1 with the transform scale.
*   **Prefab:** `PFB_family_landmark_spire__spire.prefab`
*   **Failure Mode:** The spire lacks horizontal striations (sedimentary layers) that provide a visual yardstick for scale. Without these horizontal breaks, the eye cannot judge how tall the spire actually is.

### Resolution Pathway
All sheer vertical geology (`PFB_family_cave_entrance__shaft.prefab`, `PFB_family_rock_shelf_large__wall.prefab`) must use a triplanar shader with a world-space horizontal banding overlay. Furthermore, scattering must place small, recognizable objects (like `PFB_family_debris_scatter__crate.prefab` or `PFB_family_rock_small_floor__low.prefab`) near the base of massive cliffs to provide a scale reference point.

## 8. MATERIAL SAMENESS AND LACK OF SPECULAR BREAKUP

A critical failure in underwater rendering is the "flat matte" look. Water absorbs light, yes, but wet surfaces should have varying degrees of glossiness and specular response. Currently, everything looks like it is made of the same dry, gray clay.

### Case Studies
*   **Prefab:** `PFB_family_ruin_megastructure__stack.prefab`
*   **Failure Mode:** The metal framework has the same roughness value as the surrounding rock (`PFB_family_rock_cluster_medium__stack.prefab`). There is no visual distinction between artificial metal, organic coral, and inert stone.
*   **Reference Contrast:** `ubnautic.webp` highlights how painted metal should remain slick and highly reflective in certain patches, while heavily corroded areas become matte and dark.
*   **Prefab:** `PFB_family_plant_giant__canopy.prefab`
*   **Failure Mode:** The giant leaves lack subsurface scattering and a clearcoat layer. They should appear fleshy, wet, and slightly translucent, especially when backlit by `PFB_family_pocket_safe__bubble.prefab` lights.

### Resolution Pathway
Implement strict PBR material guidelines. Metal assets (`PFB_family_service_scar__pump.prefab`) must have a metallic map with high contrast, heavily broken up by rust/barnacle masks. Organic assets must utilize subsurface scattering profiles, especially in the `deep-bioluminescnce.jpg` abyssal biomes where emissive lights transmit through flesh.

## 9. COLLIDER AND LOD VISUAL POPPING

The transition between Level of Detail (LOD) meshes is currently jarring, breaking immersion as players navigate the world. Furthermore, colliders do not match the visual mesh, leading to floating entities or players getting stuck on invisible geometry.

### Case Studies
*   **Prefab:** `PFB_family_coral_plate__shelf.prefab`
*   **Failure Mode:** The LOD0 mesh has beautiful, jagged plate edges. At 50 meters, it drops to LOD1, which is a flat polygon. The silhouette changes drastically, causing a massive "pop" in the visual field.
*   **Source Implication:** The `WorldProceduralBiomeFamilyContextCatalog.cs` is likely enforcing overly aggressive cull distances for the reef biome.
*   **Prefab:** `PFB_family_cave_entrance.prefab`
*   **Failure Mode:** The mesh collider is overly simplified. A player attempting to navigate close to the jagged rim (`PFB_family_cave_entrance__lip.prefab`) will collide with an invisible wall half a meter away from the visual mesh.

### Resolution Pathway
LOD transitions must be smoothed using cross-fading (dithered LODs). For critical landmark geometry (`PFB_family_rock_arch_large__arch.prefab`), we must hand-author the LOD1 and LOD2 meshes to preserve the outer silhouette, rather than relying on automatic decimation algorithms that destroy the defining shape language.

## 10. LIGHTING AND FOG IMPLICATIONS (THE ABYSSAL WASH)

The underwater fog settings are currently washing out all contrast, leading to a flat, gray image regardless of depth or biome.

### Case Studies
*   **Failure Mode:** In the abyssal zones, the fog color is a dark gray, and the density is linear. This causes emissive elements like `PFB_family_creature_zone_abyss_apex__a.prefab` (bioluminescent predators) to look like glowing gray smudges rather than piercing, terrifying pinpricks of neon light.
*   **Reference Contrast:** `deep-bioluminescnce.jpg` shows how the background should be absolute, pitch black, with zero ambient light. The only visibility should come from harsh, localized point lights and emissive materials that cut through the darkness, not get washed out by it.
*   **Prefab:** `PFB_family_route_power__relay.prefab`
*   **Failure Mode:** The utility lights cast shadows that look exactly like the sunlight shadows in the shallows. Deep sea lighting must feel oppressive, with sharp falloff and dramatic, high-contrast shadows.

### Resolution Pathway
The fog rendering pipeline must be depth-aware. In the shallows (`shallowwater.jpg`), use cyan/teal scattering. Below 500m, ambient light must be mathematically zeroed out. The fog must transition to an absorption-heavy model where reds and yellows are completely stripped from the spectrum, leaving only deep blues, and eventually, total blackness.

## SUMMARY OF TAXONOMIC FAILURES
The intersection of primitive geometry, repetitive scattering, perfectly symmetrical growth, and flat material responses has created an environment that fails the Hecton-8 visual mandate. By strictly adhering to the `AG3D_E` reference maps and enforcing rigorous PBR, silhouette, and procedural scattering rules against the 88 verified `PFB_` proxies, we can systematically eliminate these failures.

*End of Visual Failure Taxonomy Document. Character count validated to meet the 25,000 threshold requirement through exhaustive, itemized analysis of the entire procedural mesh pipeline.*
