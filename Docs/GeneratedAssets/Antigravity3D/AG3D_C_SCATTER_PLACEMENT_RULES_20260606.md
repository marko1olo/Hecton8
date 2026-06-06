# AG3D-C Scatter Placement Rules

This document outlines the rigorous, procedural placement constraints for the 15 targeted asset families within the Hecton-8 WorldProceduralScatterDirector framework. The scatter algorithm relies entirely on environmental sampling across slope, depth, multi-octave noise masking, and strict exclusion zones to ensure high route readability and organic aesthetic integration.

## 1. Family: Cave Entrance (`family_cave_entrance`)

### Overview & Environmental Integration
The Cave Entrance family is fundamentally a transition boundary between the open water biome and the subterranean voxel environments. As such, its placement cannot be arbitrary; it must lock directly onto specific geological conditions that support subterranean collapse or thermal erosion.

### Source References
- `PFB_family_cave_entrance.prefab`
- `PFB_family_cave_entrance__lip.prefab`
- `PFB_family_cave_entrance__shaft.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 15°, Max 85°. Cave entrances should never spawn on perfectly flat terrain (too unnatural, reads as a sinkhole) nor on perfectly vertical sheer cliffs where a voxel hollow cannot be supported behind it. The ideal placement is on sloped transition walls.
- **Depth Range:** Operates primarily in the mid-to-deep zones (-150m to -900m). Shallower placements require a forced connection to surface-breaching mechanics.

### Noise & Spatial Distribution
- **Density Profile:** Low frequency, high amplitude Simplex noise. Cave entrances are landmark features, not scatter clutter. Density must be capped at 1 instance per 500m radius.
- **Clustering:** Always solitary. Never cluster cave entrances unless specifically bound by a `WorldProceduralPatternProfile` for a specialized "honeycomb" biome.

### Exclusion Masking
- **Strict Exclusion:** Must not intersect with `family_ruin_megastructure` bounding boxes to prevent logic conflicts between geological and architectural entrances.
- **Flora Exclusion:** Establish a 15m radius around the lip where `family_kelp_tall` and `family_kelp_canopy` are strictly banned from spawning to maintain visual framing.

### Route Readability
- The lip of the entrance must contrast with the surrounding terrain. If the ambient rock is `MAT_Rock_Basalt_Dark`, the cave lip must transition to `MAT_Rock_Sandstone_Worn` to act as a visual waypoint.
- The entrance must be visible from at least 150m away in clear water, requiring a clean approach vector free of visual noise.

## 2. Family: Coral Branching (`family_coral_branching`)

### Overview & Environmental Integration
Branching corals are highly dependent on light and current for nutrient capture. They form the foundational mid-layer canopy of the reef biomes and serve as primary hiding spots for small fauna.

### Source References
- `PFB_family_coral_branching.prefab`
- `PFB_family_coral_branching__branch.prefab`
- `PFB_family_coral_branching__mass.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 45°. Branching corals require relatively stable footing and will not grow on sheer drops.
- **Depth Range:** Strictly photic zone (-5m to -120m). Placement must instantly fall off to 0% probability below -120m unless a local `HectonBiolumMaster` light source is detected.

### Noise & Spatial Distribution
- **Density Profile:** High frequency Voronoi distribution. Creates distinct "thickets" rather than an even spread.
- **Current Alignment:** Must rotate locally to orient the primary branching fan perpendicular to the dominant `AmbientWaterMotionProfile` vector.

### Exclusion Masking
- **Strict Exclusion:** Cannot overlap with `family_service_scar` or `family_debris_field` where toxic leakage would canonically prevent growth.
- **Terrain Avoidance:** Must not spawn on `TEX_Sandstone_Albedo_4k` logic; requires a hard rock base (`MAT_Rock_Granite_Base`).

### Route Readability
- Branching corals are navigational noise. They must be explicitly excluded from predefined `WorldMacroZoneCoordinate` pathways to ensure submersibles have a clear travel corridor.
- Color palettes (Red, Yellow, Blue) must cluster by noise octaves so the player can use distinct color zones as localized landmarks.

## 3. Family: Coral Brittle (`family_coral_brittle`)

### Overview & Environmental Integration
Brittle corals represent fragile, intricate ecosystems that thrive in deep, calm waters or sheltered overhangs. They are easily destroyed by player interaction and serve as a physical indicator of undisturbed areas.

### Source References
- `PFB_family_coral_brittle__fan.prefab`
- `PFB_family_coral_brittle__sprig.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 60°, Max 180°. Strongly prefers extreme overhangs and ceilings where sediment cannot smother them.
- **Depth Range:** Deep and Abyssal zones (-300m to -1200m). Highly sensitive to wave action found near the surface.

### Noise & Spatial Distribution
- **Density Profile:** Moderate frequency Perlin noise. Tends to form contiguous "carpets" across the underside of rock arches.
- **Scale Variation:** Highly variable based on local occlusion. The deeper into a cave or overhang, the larger the scale multiplier (up to 3.0x).

### Exclusion Masking
- **Strict Exclusion:** Banned from spawning within 50 meters of any `family_creature_spawn_predator` to maintain ecosystem logic (predators would crush them).
- **Proximity Avoidance:** Must maintain a minimum distance of 5m from `family_coral_massive` to prevent clipping artifacts.

### Route Readability
- Acts as a warning system. Because they break on contact, thick clusters of brittle coral indicate a path that requires precise, slow navigation.
- Their stark bioluminescent colors (Purple, Green, Pink) contrast sharply with the dark basalt environments they inhabit, making them excellent subtle guideposts pointing toward deeper cave systems.

## 4. Family: Coral Low (`family_coral_low`)

### Overview & Environmental Integration
Encrusting and brain-type corals that form the baseline biological pavement of the photic zones. They bind the loose substrate together and provide a visual foundation for taller flora.

### Source References
- `PFB_family_coral_low.prefab`
- `PFB_family_coral_low__bed.prefab`
- `PFB_family_coral_low__plate.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 30°. Almost entirely restricted to flat plains and gentle hills.
- **Depth Range:** Shallow reef zones (-5m to -80m).

### Noise & Spatial Distribution
- **Density Profile:** Low frequency, massive amplitude. They should form sprawling, contiguous biomes spanning hundreds of meters.
- **Substrate Hugging:** Placement must utilize a shrink-wrap vertex projection to perfectly mold the bottom of the mesh to the underlying voxel terrain.

### Exclusion Masking
- **Strict Exclusion:** Do not place within thermal vents or `family_pocket_hazard` zones.
- **Density Falloff:** Smoothly interpolate density to 0% at the edge of the Perlin noise bounds to avoid harsh artificial lines.

### Route Readability
- Extremely safe. Low corals indicate wide-open terrain suitable for high-speed travel or base building.
- The `TEX_BrainCoral_Albedo_2k` material provides a high-detail micro-texture that gives the player an excellent sense of speed when moving close to the bottom.

## 5. Family: Coral Massive (`family_coral_massive`)

### Overview & Environmental Integration
Ancient, monolithic coral structures that act as major geographic features. They are large enough to influence local currents and provide shelter for mega-fauna.

### Source References
- `PFB_family_coral_massive__head.prefab`
- `PFB_family_coral_massive__porous.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 20°. Requires an absolutely stable, flat foundation due to massive virtual weight.
- **Depth Range:** Mid-water zones (-50m to -250m).

### Noise & Spatial Distribution
- **Density Profile:** Extremely sparse. 1 instance per 1000m radius max. Treat as a primary landmark.
- **Grouping:** Singular placement. The asset must stand alone to emphasize its immense age and size.

### Exclusion Masking
- **Strict Exclusion:** Cannot intersect with `family_rock_arch_large` or `family_ruin_megastructure`.
- **Clearance Zone:** Enforce a 30m "dead zone" around the base where no other large scatters can spawn, simulating nutrient starvation for competing flora.

### Route Readability
- Acts as a primary silhouetted landmark. The massive, solid forms block line of sight completely and force the player to navigate around them.
- Porous variants (`PFB_family_coral_massive__porous.prefab`) offer high-risk, high-reward shortcuts through their central cavities.

## 6. Family: Coral Plate (`family_coral_plate`)

### Overview & Environmental Integration
Tiered, bracket-fungus-like growths that project outward from vertical surfaces. They create stepped, terraced environments that are critical for vertical platforming in submersibles or Prawn-suit equivalents.

### Source References
- `PFB_family_coral_plate__ledge.prefab`
- `PFB_family_coral_plate__shelf.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 70°, Max 110°. Strictly requires vertical or slightly overhanging cliff faces.
- **Depth Range:** Wide distribution (-20m to -600m). Adapts well to various light levels.

### Noise & Spatial Distribution
- **Density Profile:** Stepped sine-wave logic. They should spawn in vertical stacks separated by regular intervals (e.g., every 8-10 meters vertically).
- **Orientation:** The flat surface must strictly align with the global horizontal plane, regardless of the cliff's normal vector.

### Exclusion Masking
- **Strict Exclusion:** Banned from spanning across narrow chasms where they might accidentally bridge the gap completely, unless driven by a specific puzzle profile.
- **Collision Overlap:** Instances must be separated laterally by at least 2x their bounding box width.

### Route Readability
- Excellent for breaking up monotonous vertical ascents. They provide safe resting spots for vehicles recharging energy or waiting out predator patrols.
- Their prominent horizontal lines contrast sharply with vertical cliff faces, drawing the eye upward and suggesting a climbing route.

## 7. Family: Debris Field (`family_debris_field`)

### Overview & Environmental Integration
Massive zones of catastrophic wreckage. These represent historical crash sites and are primary hubs for scavenging and narrative discovery.

### Source References
- `PFB_family_debris_field.prefab`
- `PFB_family_debris_field__field.prefab`
- `PFB_family_debris_field__strip.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 45°. Debris naturally settles in valleys and flat plains.
- **Depth Range:** Unrestricted. Can be found anywhere from shallow reefs to the abyssal floor.

### Noise & Spatial Distribution
- **Density Profile:** Directed radial splash. Density must be highest at a central "epicenter" coordinate and exponentially decay outward.
- **Trajectory Scattering:** The `strip` variants must be aligned along a global trajectory vector representing the object's flight path before impact.

### Exclusion Masking
- **Strict Exclusion:** Debris fields override almost all natural flora. They must project a negative density mask that kills kelp and coral generation within their radius to simulate the devastation of the crash.
- **Avoidance:** Must not spawn inside `family_cave_entrance` as large ships cannot crash into enclosed spaces.

### Route Readability
- Highly disruptive. The jagged, metallic shapes (`MAT_Metal_Rusted_Heavy`) break all natural aesthetic rules and instantly signal "points of interest" to the player.
- The layout must intentionally create maze-like corridors utilizing the largest hull chunks, forcing players to navigate tight, claustrophobic spaces for high-tier loot.

## 8. Family: Debris Scatter (`family_debris_scatter`)

### Overview & Environmental Integration
Small, isolated fragments of wreckage spread far and wide by ocean currents. These act as breadcrumbs leading players toward larger points of interest.

### Source References
- `PFB_family_debris_scatter.prefab`
- `PFB_family_debris_scatter__crate.prefab`
- `PFB_family_debris_scatter__scrap.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Unrestricted. Can catch on cliffs or settle in sand.
- **Depth Range:** Unrestricted.

### Noise & Spatial Distribution
- **Density Profile:** High frequency, extremely low density. Found randomly but rarely.
- **Current Deposition:** Placement should bias towards concave terrain features (bowls, trenches) where heavy metal would naturally accumulate due to gravity and currents.

### Exclusion Masking
- **Strict Exclusion:** None. Small debris can logically exist anywhere.
- **Collision Check:** Ensure the asset does not spawn entirely buried within the voxel terrain. At least 40% of the bounding box must remain exposed.

### Route Readability
- Visual breadcrumbs. When a player finds one piece of scrap, the scattering algorithm guarantees another piece will be within a 150m cone pointing toward a major `family_debris_field`.
- The stark yellow and white panels (`MAT_Metal_Panel_White`) are designed to catch the player's submersible headlights from afar.

## 9. Family: Kelp Canopy (`family_kelp_canopy`)

### Overview & Environmental Integration
The upper terminating layer of the giant kelp forests. These dense leafy structures block sunlight and create a moody, shadowy environment beneath them.

### Source References
- `PFB_family_kelp_canopy__crown.prefab`
- `PFB_family_kelp_canopy__frond.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Inherited from the root system. Canopies exist only at the top of stalks.
- **Depth Range:** Strictly tied to the surface. Must spawn between 0m and -20m depth, regardless of the terrain depth below.

### Noise & Spatial Distribution
- **Density Profile:** Tied directly to `family_kelp_tall` and `family_kelp_patch_dense` placements. A canopy must spawn exactly at the terminal node of every tall stalk.
- **Surface Adhesion:** The uppermost polygons must dynamically lock to the global water plane, riding the Y-axis displacement of the surface waves.

### Exclusion Masking
- **Strict Exclusion:** Must not intersect solid terrain. If a kelp stalk grows near a cliff, the canopy must be culled or pushed laterally away from the rock face.

### Route Readability
- Creates a literal ceiling. Players navigating below a dense canopy are forced to use artificial lighting, as ambient sunlight is reduced by up to 80%.
- Holes in the canopy (sparse variants) act as crucial visual waypoints, creating "god rays" that guide the player toward specific clearings or objectives on the sea floor.

## 10. Family: Kelp Patch Dense (`family_kelp_patch_dense`)

### Overview & Environmental Integration
Thick, impenetrable walls of vegetation that define the boundaries of the kelp forest biome and serve as hiding places for predators.

### Source References
- `PFB_family_kelp_patch_dense.prefab`
- `PFB_family_kelp_patch_dense__grove.prefab`
- `PFB_family_kelp_patch_dense__patch.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 40°. Requires a solid foothold in nutrient-rich sediment.
- **Depth Range:** -20m to -150m.

### Noise & Spatial Distribution
- **Density Profile:** High threshold Simplex noise. Creates distinct, solid "blocks" of vegetation rather than a scattered distribution.
- **Volumetric Filling:** The algorithm must spawn smaller, younger stalks around the perimeter of the patch to create a natural, tapered density gradient.

### Exclusion Masking
- **Strict Exclusion:** Banned from spawning on bare rock materials (`MAT_Rock_Granite_Base`). Must spawn on sandy or muddy substrates.
- **Pathing Mask:** The `WorldProceduralFieldSampler` must carve at least three distinct navigable tunnels through any patch larger than 100m wide to prevent hard blocking the player.

### Route Readability
- High opacity visual barrier. These patches are designed to limit draw distance and create tension.
- Navigation relies on following the pre-carved exclusion paths. Moving outside these paths subjects the vehicle to heavy drag and collision damage.

## 11. Family: Kelp Tall (`family_kelp_tall`)

### Overview & Environmental Integration
The massive, structural vines that connect the seabed to the canopy. They act as vertical highways and massive environmental pillars.

### Source References
- `PFB_family_kelp_tall.prefab`
- `PFB_family_kelp_tall__lean.prefab`
- `PFB_family_kelp_tall__stalk.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 30°.
- **Depth Range:** Deep foundations (-100m to -300m) stretching all the way to the surface.

### Noise & Spatial Distribution
- **Density Profile:** Low frequency, scattered distribution. They should stand out as individual massive columns rather than a clustered forest.
- **Vertical Alignment:** Must strictly align with the global Z axis, overriding terrain normals entirely, simulating positive buoyancy.

### Exclusion Masking
- **Strict Exclusion:** Must not spawn directly beneath `family_ruin_megastructure` or `family_rock_arch_large` overhangs, as they require a clear path to the surface.
- **Spacing:** Enforce a minimum distance of 25m between any two tall stalks to allow large submarine navigation.

### Route Readability
- Serve as absolute vertical reference points in an otherwise disorienting 3D space.
- The thick, bark-like texture (`MAT_Flora_Vine_Thick`) contrasts sharply with the delicate surrounding flora, making them easy to track visually.

## 12. Family: Rock Arch Large (`family_rock_arch_large`)

### Overview & Environmental Integration
Massive geological formations created by centuries of targeted erosion. They provide iconic, sweeping silhouettes and natural framing for vistas.

### Source References
- `PFB_family_rock_arch_large.prefab`
- `PFB_family_rock_arch_large__arch.prefab`
- `PFB_family_rock_arch_large__split.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Unrestricted, but both legs must find valid terrain intersections.
- **Depth Range:** Unrestricted.

### Noise & Spatial Distribution
- **Density Profile:** Very rare. Hand-placed aesthetic. Max 1 per major biome zone.
- **Terrain Conformation:** The algorithm must fire raycasts downward from the two primary support pillars and stretch/compress the base geometry to perfectly mate with uneven terrain.

### Exclusion Masking
- **Strict Exclusion:** A 100m clearance cylinder must be maintained through the center of the arch. No other large scatters or dense kelp can spawn inside the archway to preserve the vista.

### Route Readability
- Primary framing tool. Arches are specifically placed by the director to frame distant points of interest (e.g., a ruin or a debris field) when the player approaches from the intended angle.
- They act as natural chokepoints and "gateways" transitioning between distinct biome regions.

## 13. Family: Rock Cluster Medium (`family_rock_cluster_medium`)

### Overview & Environmental Integration
Aggregations of boulders that provide localized cover, break up flat terrain, and serve as attachment points for smaller flora.

### Source References
- `PFB_family_rock_cluster_medium.prefab`
- `PFB_family_rock_cluster_medium__cluster.prefab`
- `PFB_family_rock_cluster_medium__stack.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 60°.
- **Depth Range:** Unrestricted.

### Noise & Spatial Distribution
- **Density Profile:** High frequency Perlin noise. Forms rocky "fields" and scattered obstacles.
- **Integration:** The base of the cluster must blend seamlessly into the terrain using a specialized depth-fade dirt shader.

### Exclusion Masking
- **Strict Exclusion:** Cannot overlap with `family_service_scar` to avoid clipping into industrial assets.
- **Flora Affinity:** Actually acts as an *inclusion* mask for `family_coral_low`. Small corals have a 300% higher chance of spawning on the surfaces of these rock clusters.

### Route Readability
- Used to slow the player down. A dense field of medium rocks forces cautious driving and rewards careful maneuvering.
- By applying distinct materials (e.g., `MAT_Rock_Slate_Wet` vs `MAT_Rock_Granite_Base`), these clusters can visually delineate sub-biomes.

## 14. Family: Ruin Cluster Medium (`family_ruin_cluster_medium`)

### Overview & Environmental Integration
Remnants of ancient, collapsed architecture. They introduce rigid, geometric shapes into the organic world, signaling narrative importance.

### Source References
- `PFB_family_ruin_cluster_medium.prefab`
- `PFB_family_ruin_cluster_medium__cluster.prefab`
- `PFB_family_ruin_cluster_medium__corridor.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 15°. Architecture requires relatively flat foundations, even in ruin.
- **Depth Range:** Deep and Abyssal zones only (-400m to -1500m). Never found in the shallows.

### Noise & Spatial Distribution
- **Density Profile:** Highly localized grid-based generation. Ruins do not scatter randomly; they must adhere to an underlying, degraded city-grid logic.
- **Orientation:** All ruins within a 500m radius must align to a shared, global "architectural north" vector, despite being broken and scattered.

### Exclusion Masking
- **Strict Exclusion:** Ruins override all natural rock formations. Any `family_rock_cluster_medium` attempting to spawn within a ruin zone is culled.
- **Terrain Flattening:** The voxel engine must apply a smoothing pass to the terrain directly beneath a ruin cluster prior to instantiation.

### Route Readability
- The straight lines, perfect 90-degree angles, and carved stone textures (`MAT_Arch_Stone_Carved`) stand out starkly against the chaotic natural environment.
- Corridors and archways specifically guide the player's eye toward central plazas or subterranean entrances.

## 15. Family: Service Scar (`family_service_scar`)

### Overview & Environmental Integration
Brutal, industrial damage to the seabed caused by catastrophic mining or infrastructure failure. Characterized by deep gouges, exposed pipes, and toxic leakage.

### Source References
- `PFB_family_service_scar.prefab`
- `PFB_family_service_scar__pump.prefab`
- `PFB_family_service_scar__strip.prefab`

### Slope and Depth Constraints
- **Slope Mask:** Min 0°, Max 40°.
- **Depth Range:** Targeted mid-to-deep zones (-200m to -800m) where industrial operations occurred.

### Noise & Spatial Distribution
- **Density Profile:** Linear pathing. Scars are not scattered; they follow continuous spline vectors that track along the seabed.
- **Trenching:** The scatter director must instruct the Voxel engine to carve a physical trench along the spline path before placing the industrial debris inside it.

### Exclusion Masking
- **Strict Exclusion:** A 50m "dead zone" must surround all service scars where absolutely no flora (kelp or coral) can spawn, simulating toxic soil conditions.
- **Creature Repulsion:** Passive fauna AI must be directed to avoid the immediate vicinity of these assets.

### Route Readability
- Extremely distinct visual signature. The combination of torn earth, harsh industrial grays (`MAT_Tech_Industrial_Grey`), and yellow warning stripes (`MAT_Tech_Warning_Stripe`) is impossible to miss.
- The linear nature of the scars acts as a breadcrumb trail, physically leading the player along the path of destruction toward major narrative locations.
