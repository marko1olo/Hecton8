# Domain Architecture Coverage Matrix

Date: 2026-05-28
Status: STATIC_DOC / SOURCE-ORIENTED COVERAGE
Owner: DOC_ROOT_ARCH_AUDIT
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM

Purpose: map domain ranges to active architecture docs.

Use this before changing an assigned domain.

This is not runtime proof.

This does not replace `Actual Domains of Project.txt`.

## Use Rule

1. Identify the assigned domain in `Docs/Actual Domains of Project.txt`.
2. Read `PROJECT_RUNTIME_TOPOLOGY.md`.
3. Read `GLOBAL_AUTHORITY_BOUNDARIES.md`.
4. Read `FIRST_20_MINUTES_VERTICAL_SLICE_CONTRACT.md`.
5. Read the matching echelon below.
6. Read source anchors before editing.
7. If source and doc disagree, patch the doc with evidence.

## Echelon 1: Core And Memory

Domains: `1-10`.

Runtime surface:

- boot
- memory
- save
- Data Monolith
- AUP
- telemetry
- scalability
- platform
- dispatcher

Architecture docs:

- `PROJECT_RUNTIME_TOPOLOGY.md`
- `BOOT_SEQUENCE_TOPOLOGY.md`
- `DISPATCH_PIPELINE.md`
- `GLOBAL_AUTHORITY_BOUNDARIES.md`
- `SYSTEM_INTERCONNECT_MATRIX.md`
- `DATA_MONOLITH_H8BIN_SPEC.md`
- `DATA_MONOLITH_RUNTIME_INTEGRATION.md`
- `SAVE_PAGING_PROTOCOL.md`
- `AUP_PRECISION_STANDARDS.md`
- `SCALABILITY_MATRIX.md`
- `PLATFORM_PORTABILITY_PROOF_LADDER.md`
- `CORE_REPLAY_DETERMINISM.md`
- `ARENA_ALLOCATOR_2_0.md`
- `ASYNCHRONOUS_TELEMETRY_EXPORTER_SHINOBU_160.md`

Source anchors:

- `Assets/_Project/Scripts/Bootstrap`
- `Assets/_Project/Scripts/Core`
- `Assets/_Project/Scripts/Data/Monolith`
- `Assets/_Project/Scripts/SaveSystem`
- `Assets/_Project/Scripts/SaveBinaryStorage.cs`
- `Assets/_Project/Scripts/SaveManager.cs`

Default proof gap:

- Compile/import/runtime proof is not implied.
- Native ownership debt remains in the actuality ledger.

## Echelon 2: World And Terrain

Domains: `11-20`.

Runtime surface:

- terrain
- voxel
- scatter
- geology
- biomes
- flow
- vents
- wreckage

Architecture docs:

- `PROCEDURAL_WORLD_VERTICAL_ARCHITECTURE.md`
- `FLOODED_TERRESTRIAL_GEOGRAPHY.md`
- `TERRAIN_RUNTIME_AUTHORITY_ROUTE.md`
- `TERRAIN_CHUNK_PAGING_SYSTEM_SHINOBU_245.md`
- `STATIC_CAVE_SDF_VOLUME_BAKER.md`
- `VOXEL_TERRAIN_SEAM_BINDER_SHINOBU_246.md`
- `VOXEL_DYNAMIC_NAVGRID_VAULT_ROUTE_1316.md`
- `FLOW_FIELD_MATH.md`
- `BIOME_WEIGHT_MAP_BAKER_SHINOBU_243.md`
- `PROCEDURAL_WRECKAGE_GLOBAL_AUTHORITY_ROUTE_CARD_SHINOBU_121.md`

Source anchors:

- `Assets/_Project/Scripts/World`
- `Assets/_Project/Scripts/Environment`
- `Assets/_Project/Scripts/Physics`
- `Assets/_Project/Scripts/Rendering`
- `Assets/_Project/Scripts/Data`

Default proof gap:

- Static terrain contracts do not prove streamed residency.
- Copper route availability needs Play Mode/player evidence.

## Echelon 3: Flora Fauna Biota

Domains: `21-30`.

Runtime surface:

- ecosystem
- fauna spatial lookup
- swarm
- predator cognition
- pathing
- procedural IK
- flora
- genetics

Architecture docs:

- `AI_PACING_MODEL.md`
- `AI_POTENTIAL_FIELD_NAVIGATION.md`
- `SHINOBU_302_UTILITY_AI_COGNITION_ROUTE.md`
- `SHINOBU_FLORA_FAUNA_SYMBIOSIS.md`
- `FLORA_PROCEDURAL_SWAY_FIELD.md`
- `MIGRATORY_FLORA_SYSTEM.md`
- `PARASITIC_FAUNA_PARTICLE_SWARMS_SHINOBU_313.md`
- `BIOTA_DENSITY_MAP_BAKER_SHINOBU_308.md`

Source anchors:

- `Assets/_Project/Scripts/AI`
- `Assets/_Project/Scripts/Fauna`
- `Assets/_Project/Scripts/Ecosystem`
- `Assets/_Project/Scripts/World`
- `Assets/_Project/Scripts/Animation`

Default proof gap:

- Ecology breadth is parked without First 20 Minutes impact.
- Runtime profiler and GC proof remain required.

## Echelon 4: Player Tools Kinematics

Domains: `31-40`.

Runtime surface:

- KCC
- buoyancy
- hand IK
- tether
- tools
- scavenging
- inventory
- crafting
- XR interaction

Architecture docs:

- `KINEMATICS_AUP_INTEGRATION.md`
- `SHINOBU_276_EXOSUIT_6D_KINEMATICS.md`
- `EQUIPMENT_SOA_LAYOUT.md`
- `AUXILIARY_EQUIPMENT_ROUTER_SHINOBU_229.md`
- `SHINOBU_229_AUXILIARY_EQUIPMENT_ROUTE_CARD.md`
- `SOA_INVENTORY_QUERY_ENGINE.md`
- `SOA_INVENTORY_ROUTING_NETWORK_SHINOBU_141.md`
- `SHINOBU_317_CRAFTING_FAST_FAIL_ROUTE.md`

Source anchors:

- `Assets/_Project/Scripts/Gameplay`
- `Assets/_Project/Scripts/Physics/KCC`
- `Assets/_Project/Scripts/Interaction`
- `Assets/_Project/Scripts/Equipment`
- `Assets/_Project/Scripts/Inventory`
- `Assets/_Project/Scripts/Scavenging`
- `Assets/_Project/Scripts/Tools`
- `Assets/_Project/Scripts/Visor`

Default proof gap:

- Starting tool truth is not proven.
- Copper acquisition is still a route proof blocker.

## Echelon 5: Combat Physiology

Domains: `41-50`.

Runtime surface:

- combat
- armor
- status effects
- physiology
- decompression
- gas
- crush depth
- wounds

Architecture docs:

- `X_008_COMBAT_ARMOR_LUT_ROUTE_CARD.md`
- `SHINOBU_318_ARMOR_PENETRATION_LUT_ROUTE_CARD.md`
- `DECOMPRESSION_SICKNESS_SHINOBU_321.md`
- `SHINOBU_323_SUIT_INTEGRITY_DEPTH_CRUSH_ROUTE_CARD.md`
- `SHINOBU_325_SCREEN_SPACE_TRAUMA_DECAL_ROUTE_CARD.md`
- `TRAUMA_GLITCH_SYSTEM.md`
- `SHINOBU_275_SCREEN_SPACE_VISOR_WOUNDS_ROUTE_CARD.md`

Source anchors:

- `Assets/_Project/Scripts/Gameplay/Combat`
- `Assets/_Project/Scripts/Physiology`
- `Assets/_Project/Scripts/Physics`
- `Assets/_Project/Scripts/UI`
- `Assets/_Project/Scripts/VFX`
- `Assets/_Project/Scripts/Rendering`

Default proof gap:

- Combat docs are route cards without runtime artifacts.
- Hazard route proof remains required.

## Echelon 6: Habitat Vehicles

Domains: `51-60`.

Runtime surface:

- habitat
- construction
- flooding
- power
- logistics
- deconstruction
- submarine
- drones
- scooter

Architecture docs:

- `BASE_MODULE_CATALOG_SHINOBU_216.md`
- `CONSTRUCTION_SOCKET_CSR_SOLVER_SHINOBU_217.md`
- `CONSTRUCTION_BUILDER_HOLOGRAPHY_SHINOBU_228.md`
- `HABITAT_FLUID_INCURSION.md`
- `BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`
- `SHINOBU_336_MODULE_DECONSTRUCTION_RESOURCE_RETURN_ROUTE_CARD.md`
- `SUBMARINE_OS_MANUAL.md`
- `SHINOBU_332_SUBMARINE_GYRO_ROUTE_CARD.md`
- `SHINOBU_333_SUBMARINE_BALLAST_BUOYANCY_ROUTE_CARD.md`
- `SHINOBU_251_SUBMARINE_ADDED_MASS_ROUTE_CARD.md`
- `DRONE_FLEET_PROTOCOL.md`

Source anchors:

- `Assets/_Project/Scripts/Construction`
- `Assets/_Project/Scripts/Habitat`
- `Assets/_Project/Scripts/Power`
- `Assets/_Project/Scripts/Logistics`
- `Assets/_Project/Scripts/Vehicles`
- `Assets/_Project/Scripts/Physics`
- `Assets/_Project/Scripts/UI`

Default proof gap:

- Do not create a second owner for save truth.
- Do not create a second owner for power or physics truth.

## Echelon 7: Atmosphere Celestial

Domains: `61-68`.

Runtime surface:

- celestial
- tides
- weather
- gas
- thermodynamics
- marine snow
- fog
- light shafts
- GI

Architecture docs:

- `SHINOBU_345_CELESTIAL_ORBIT_ROUTE_CARD.md`
- `ABYSSAL_THERMODYNAMICS_SOLVER.md`
- `SURFACE_STORM_ABYSSAL_PROPAGATION_SHINOBU_234.md`
- `ABYSSAL_CAUSTICS_SHINOBU_232.md`
- `FLOW_FIELD_MATH.md`
- `CINEMATIC_CHEATS_LEDGER.md`
- `TECH_ART_PBR_SURFACE_DOCTRINE.md`

Source anchors:

- `Assets/_Project/Scripts/Atmosphere`
- `Assets/_Project/Scripts/Thermodynamics`
- `Assets/_Project/Scripts/Environment`
- `Assets/_Project/Scripts/Lighting`
- `Assets/_Project/Scripts/Rendering`
- `Assets/_Project/Scripts/VFX`
- `Assets/_Project/Scripts/World`

Default proof gap:

- Prefer deterministic cheats over expensive realism.
- Quality load-shed must be continuous.

## Echelon 8: Presentation UX

Domains: `69-78`.

Runtime surface:

- UI
- subtitles
- terminals
- visor
- PDA
- narrative POIs
- cartography
- scanning
- audio
- warnings

Architecture docs:

- `ZERO_GC_UI_PIPELINE.md`
- `VISOR_AR_STENCIL_RENDERER.md`
- `SHINOBU_348_SCREEN_SPACE_PDA_PROJECTOR_ROUTE_CARD.md`
- `PDA_ENCYCLOPEDIA_STREAMER.md`
- `SHINOBU_226_SCANNER_LORE_DATABASE_SYNC.md`
- `SHINOBU_349_AUP_NARRATIVE_POI_TRIGGER_ROUTE_CARD.md`
- `AUDIO_DSP_PIPELINE.md`
- `ADAPTIVE_STEM_AUDIO_MIXER.md`
- `VOCAL_WARNING_QUEUE_SHINOBU_352.md`
- `VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`

Source anchors:

- `Assets/_Project/Scripts/UI`
- `Assets/_Project/Scripts/PDA`
- `Assets/_Project/Scripts/Visor`
- `Assets/_Project/Scripts/Narrative`
- `Assets/_Project/Scripts/Cartography`
- `Assets/_Project/Scripts/Audio`
- `Assets/_Project/Scripts/AudioLog`
- `Assets/_Project/Scripts/Interaction`

Default proof gap:

- Presentation consumes snapshots.
- Presentation does not own gameplay truth.
- Hot registry polling is forbidden.

## Echelon 9: Meta Integration

Domains: `79-85`.

Runtime surface:

- haptics
- camera
- physics culling
- integration
- docs
- QA
- research

Architecture docs:

- `SHINOBU_353_HAPTIC_SYNTHESIS_ROUTE_CARD.md`
- `SHINOBU_354_PROCEDURAL_CAMERA_SHAKE_ROUTE_CARD.md`
- `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING_ROUTE_CARD.md`
- `THIRD_PARTY_POISON.md`
- `URP_SCREENSHOT_PIPELINE.md`
- `PLATFORM_PORTABILITY_PROOF_LADDER.md`
- `PROJECT_RUNTIME_TOPOLOGY.md`

Source anchors:

- `Assets/_Project/Scripts/Input`
- `Assets/_Project/Scripts/Gameplay`
- `Assets/_Project/Scripts/Optimization`
- `Assets/_Project/Scripts/QA`
- `Assets/_Project/Scripts/Editor`
- `Assets/_Project/Scripts/BuildTools`
- `Assets/_Project/Scripts/Meta`
- `Docs`
- `Tools`

Default proof gap:

- Meta systems may measure, validate, or present.
- They must not silently change runtime authority.

## Missing-Proof Defaults

- A source path proves visibility only.
- A route card proves intent only.
- Generated docs need validator exit `0`.
- First 20 Minutes route proof is pending.
- Platform packages do not prove readiness.
- `GlobalQualityWeight` remains continuous.
- Named tiers are authoring labels only.
