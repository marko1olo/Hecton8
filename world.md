# HECTON-8 World Bible

Status: AUTHORING STANDARD - PENDING UNITY/PROFILER VERIFICATION
Scope: world composition, biome identity, route design, habitat placement, wrecks, caves, geology, ecology scatter, landmarks, navigation, and environmental storytelling.

## 0. Prime World Law

The world is not random underwater decoration. It is flooded geography, failed industry, salvage history, and pressure architecture.

Every location must provide:

- a route decision;
- a pressure or visibility condition;
- a machine, geological, biological, or salvage reason to exist;
- a scale cue;
- an evidence cue.

Pretty emptiness is rejected. Random scatter is rejected. Biomes that differ only by color are rejected.

## 1. World Layers

A strong location has layered logic:

- Geological base: trench, shelf, cliff, vent, cave, sediment basin, fracture, drowned terrain.
- Industrial intrusion: habitat, cable, pump, relay, dock, wreck, pipe, scaffold, hatch, salvage cut.
- Biological response: coral, kelp, fauna route, biofilm, biolum field, carcass, nest, avoidance zone.
- Player route: entry, landmark, risk corridor, return path, shortcut, lockout, extraction point.
- Evidence: scars, bodies, logs, broken machinery, black-box fragment, missing wall, cut panel.

If only one layer exists, the area will feel like a test map.

## 2. Biome Identity

Each biome must define:

- dominant silhouette;
- fog and lighting behavior;
- navigation landmarks;
- acoustic signature;
- hazards and safe pockets;
- material palette;
- resource/evidence logic;
- creature behavior pressure;
- low-tier readability method.

Do not define a biome by tint alone. "Blue cave", "green kelp", or "red danger area" is not enough.

## 3. Route Design

Routes must support tension:

- visible goal with uncertain path;
- return path cost;
- at least one readable landmark;
- at least one risk modifier;
- optional shortcut with tradeoff;
- failure consequences;
- low-end silhouette readability.

Avoid straight corridors with decorative props. Avoid open fields with no route grammar. Avoid maze design that relies on confusion instead of pressure.

## 4. Habitat And Wreck Placement

Habitats and wrecks must obey engineering:

- pressure doors face route logic;
- pumps and conduits connect plausible systems;
- docking geometry has approach space;
- broken modules have failure direction;
- salvage cuts expose structure;
- interiors show how the machine survived or failed;
- exterior silhouettes remain readable in fog.

Do not place modules as random boxes. A module must explain why it is there, what it did, how it failed, and what the player can do with it.

## 5. Geology And Terrain

Geology must guide:

- strata indicate direction;
- cliffs frame routes;
- caves compress risk;
- vents create heat, sound, mineral, fauna, and light logic;
- sediment shows current and disturbance;
- rock shapes support cover, landmarks, and collision proxies.

Perlin hills and smoothed boulders are rejected. Use fracture planes, undercuts, shelves, debris fans, vent chimneys, cave ribs, and waterline/sediment cues.

## 6. Ecology Scatter

Scatter is authored density, not random decoration:

- flora follows current, light, substrate, depth, and hazard;
- coral clusters have parent/child growth logic;
- fauna zones respond to food, shelter, sound, light, and territory;
- debris follows gravity, current, collapse, and salvage trails;
- empty zones exist only to create dread, route clarity, or performance relief.

Scatter must be instance-friendly, LOD-aware, and tied to `3dmodel.md` asset standards.

## 7. Landmarks

Every major area needs at least one landmark:

- silhouette landmark;
- sound landmark;
- light landmark;
- route landmark;
- industrial landmark;
- geological landmark;
- evidence landmark.

Landmarks must survive low-tier graphics. If the player needs ultra fog or dense assets to navigate, the area fails.

## 8. World Generation Order

1. Define route and player decision.
2. Define geological carrier.
3. Place industrial or biological reason.
4. Place landmark and return path.
5. Add hazards and safe pockets.
6. Add evidence and salvage.
7. Add scatter density by substrate/current/depth.
8. Bake LOD/HLOD, occlusion, colliders, probes, and streaming cells.
9. Capture low-tier and normal screenshots.
10. Reject if the area reads as random scatter.

## 9. World QA Gates

Reject if:

- area has no route decision;
- biome differs only by color;
- no low-tier landmark exists;
- scatter is random or evenly spaced;
- modules ignore engineering;
- terrain is smoothed noise;
- no evidence cue exists;
- player cannot describe why the place exists;
- world composition depends on runtime generation in gameplay.

## 10. Truth Ownership

World composition owns placement logic, route grammar, biome identity, landmarks, and environmental evidence. It does not own runtime streaming, voxel rebuilds, generated mesh authoring, AI cognition, or rendering passes.

World work must route to:

- `streaming.md` for chunk residency and HLOD.
- `voxels.md` for SDF caves, carving, and seams.
- `3dmodel.md` for generated asset quality.
- `rendering.md` and `presentation.md` for fog, lighting, and capture quality.
- `ai.md` and `creatures.md` for ecology pressure.

## 11. GlobalQualityWeight Scaling

Compact keeps route silhouettes, landmarks, low-cost fog, simple scatter, HLOD, proxy collision, and readable evidence. Middle adds density and local biome detail. High adds richer lighting, material response, and ecology. Ultra adds sensory density and longer sightline richness without making navigation depend on expensive effects.

## 12. Proof Artifacts

World work must provide:

- route sketch or description;
- biome identity statement;
- landmark list;
- evidence cue list;
- streaming/HLOD note;
- collider/navigation note;
- normal-tier screenshot;
- compact-tier screenshot;
- rejection check for random scatter.

## 13. Acceptance Sentence

A world area is accepted only when it has route decisions, physical reason, readable landmarks, believable geology/industry/ecology, evidence, streaming discipline, and compact-tier readability.
