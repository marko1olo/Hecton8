# HECTON-8 Terrain, Biomes, Scatter, And World Surface Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: terrain surfaces, caves, cliffs, biome masks, MapMagic/runtime terrain bridges, scatter placement, ore/geology nodes, biome transitions, navigation readability, and terrain proof gates.

## Prime Law

Terrain is route, pressure, geology, and memory. It is not random noise.

Every terrain surface must explain how water, pressure, sediment, industry, collapse, biology, and salvage shaped it. HECTON-8 rejects toy low-poly terrain, smooth procedural blobs, random coral carpets, generic resource scatter, square-map feeling, and terrain that looks good only from one screenshot angle.

## Truth Ownership

Terrain owns surface shape, biome masks, scatter eligibility, geological logic, navigation affordances, and terrain validation. It does not own voxel persistence, generated asset topology, runtime streaming, physics truth, water truth, or narrative facts.

Use:

- `voxels.md` for SDF caves/carving/persistence;
- `world.md` for macro composition and landmarks;
- `3DMODEL_GEOLOGY_ROCKS.md` for generated rock meshes;
- `streaming.md` for residency/HLOD;
- `physics.md` for collision truth;
- `rendering.md`/`lighting.md` for final visibility.

## Terrain Shape Rules

Required:

- readable macro routes: trench, shelf, ridge, ruin line, cable path, vent chain, wreck fall, cave mouth;
- meso variation: ledges, sediment slopes, fractured faces, collapsed industrial debris;
- micro detail via materials/decals/bakes, not dense runtime mesh everywhere;
- slope and traversal classification;
- landmark silhouettes for navigation;
- no terrain feature without biome/geology/industrial reason.

Flat planes, smooth sinusoidal hills, isolated random rocks, and resource dots are rejected.

## Surface Terrain Beauty Boundary

Surface and photic-zone terrain must read as wet, bright, geologically shaped, and materially detailed. Exposed rock, shore shelves, arches, shallows, and waterline cliffs need strata, erosion, sediment, puddled water, foam contact, mineral breakup, and readable silhouettes. They are not abyss props with the brightness raised.

Dark, oppressive terrain treatment belongs to abyssal depth, caves, interior voids, storms, and temporary route events. Compact may reduce scatter density and texture resolution, but surface terrain still needs the Subnautica-level floor for beauty, clarity, material richness, and scenic composition.

## Biome And Scatter Rules

Scatter is an ecological/geological consequence:

- coral attaches to surfaces that make biological sense;
- flora respects current, light, sediment, depth, and shelter;
- ore veins follow strata, fault lines, vents, or industrial spill;
- wreckage follows impact direction, buoyancy, breakage, and salvage history;
- creature routes align with food, cover, pressure, and acoustic corridors.

Scatter must use deterministic seeds and masks. Runtime scatter may select/reside assets; it must not invent final mesh topology in gameplay.

## Collision And Navigation

Terrain collision must be practical:

- simplified collision/SDF/proxy route declared;
- no visual terrain triangles as expensive physics truth unless proven and bounded;
- traversal blockers and route affordances are readable;
- navigation masks align with visible terrain;
- underwater vehicle clearance is considered.

## GlobalQualityWeight Scaling

`GlobalQualityWeight` may scale scatter density, decal density, terrain material detail, HLOD distance, fog reveal distance, optional small props, and diagnostic overlays. It must not change terrain truth, resource ids, save identity, biome ownership, or navigation authority.

Compact keeps macro silhouettes, route cues, cheap scatter, and strong material masks. Middle adds richer scatter and decals. High adds denser near-field geology. Ultra adds local terrain overkill only where streaming, physics, and readability remain proven.

## Production Packet

Any terrain, biome mask, scatter, traversal, or geology-placement change must declare:

- terrain source, deterministic seed, and owner;
- biome mask, slope class, depth band, and traversal classification;
- scatter family list and density caps;
- collision, SDF, voxel, or proxy relationship;
- route silhouette and landmark readability plan;
- resource/evidence placement rules if present;
- Compact and High captures;
- profiler/GC proof when runtime terrain or scatter code changes.

Terrain that reads as random noise, flat dressing, or low-poly filler is rejected even if it technically covers space.

## Proof Artifacts

Terrain work must provide:

- terrain/biome mask manifest;
- seed and deterministic route;
- slope/traversal classification;
- scatter family list and density caps;
- compact screenshot from gameplay height;
- high-tier screenshot if visual density changed;
- collision/proxy/SDF proof;
- streaming/HLOD proof if residency changed.

## Rejection Gates

Reject:

- random noise terrain;
- blocky/low-poly toy cliffs;
- scatter without biome/geology reason;
- ore/resources as colored dots;
- terrain that hides route and interaction;
- runtime terrain generation in gameplay without explicit approved pipeline and proof;
- screenshots that avoid traversal angles.

## Acceptance Sentence

Terrain is accepted only when it creates readable routes, credible geology, deterministic scatter, scalable detail, cheap collision/navigation truth, and proof across compact and high-tier views.
