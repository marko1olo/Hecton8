# HECTON-8 Terrain, Biomes, Scatter, And World Surface Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: terrain surfaces, caves, cliffs, biome masks, MapMagic/runtime terrain bridges, scatter placement, ore/geology nodes, biome transitions, navigation readability, and terrain proof gates.

## Prime Law

Terrain is route, pressure, geology, and memory. It is not random noise.

Every terrain surface must explain how water, pressure, sediment, industry, collapse, biology, and salvage shaped it. HECTON-8 rejects toy low-poly terrain, smooth procedural blobs, random coral carpets, generic resource scatter, square-map feeling, and terrain that looks good only from one screenshot angle.

## Truth Ownership

Terrain owns surface shape, biome masks, scatter eligibility, geological logic, navigation affordances, and terrain validation. It does not own voxel persistence, generated asset topology, runtime streaming, physics truth, water truth, or narrative facts.

Current terrain source-of-truth route:

- `WorldMacroGeologyFields` owns deterministic macro-geology evaluation and the terrain artifact identity contract: authoring seed, macro artifact version, chunk size, chunk range, and chunk range hash.
- `WorldTerrainDetailContracts` owns the macro sample to terrain material/control contract: material classes, meso detail fields, packed control masks, and proof extents.
- `WorldProceduralTerrainSplatmapJobs` consumes macro and meso fields to produce runtime terrain/surface masks. It must not invent a separate geology truth.
- `MapMagicBridge` and MapMagic nodes are bridge/provider/bake adapters. They may supply active height payloads, splat payloads, biome matrices, and chunk identity, but the current macro-geology contract does not come from an old hand-authored MapMagic graph.
- `WorldProceduralFieldSampler` reads the active terrain provider first, then deterministic macro-geology fallback, then synthetic fallback. This fallback order is runtime behavior, not license to ship missing terrain payloads silently.
- `SaveManager` stores and validates terrain identity. Saves reference seeds, macro artifact version, chunk range/hash, provider flags, and water calibration. They do not serialize the entire macro field into the player save.
- Water truth remains in the ocean/water/terrain-provider calibration route. Macro geology may produce seafloor height; it does not own sea level.

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

Depth/light lock:

- 0-100 m terrain is mostly bright, wet, colorful, and readable.
- Shallow cave interiors can be dark, but open shallow terrain cannot be treated as abyss terrain.
- 200-400 m terrain becomes more subdued and twilight-like.
- 400-500 m and below can become truly dark, provided landmarks, silhouettes, and collision/traversal reads remain clear.

Surface and shallow terrain should include alien biota and coral-like growth where the biome supports it, plus visible colony/industrial leftovers in selected areas. Do not make photic terrain a pristine aquarium or a dead empty rock field.

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

## Current Static Source Anchors

Evidence class: STATIC_SOURCE only. Compile, Unity import, terrain capture, profiler, GC, player traversal, and player-build proof remain PENDING VERIFICATION.

| Runtime | Owner / boundary | Static route | GlobalQualityWeight consequence | Missing proof |
|---|---|---|---|---|
| `Assets/_Project/Scripts/World/WorldMacroGeologyFields.cs` | `Hecton8.World`; deterministic macro-geology evaluator and terrain artifact identity source. It owns macro zone/depth/slope/deposition/roughness/cavity field math, not Unity scene objects, water truth, mesh topology, or save serialization of full fields. | `CreateDefault`, `Evaluate`, `ResolveZone`, chunk coord/key/id/range/hash helpers, `ArtifactVersion`, `DefaultAuthoringSeed`, `MinimumWorldExtentMeters`, and `DefaultChunkSizeMeters` define the current terrain macro contract. | Quality tiers must not change macro field values, seed identity, chunk identity, biome/resource IDs, route silhouettes, or save compatibility. | No Unity import, generated artifact load, player traversal, save/load mismatch capture, or profiler proof was provided by this static audit. |
| `Assets/_Project/Scripts/World/WorldTerrainDetailContracts.cs` | `Hecton8.World`; terrain material/control contract over macro samples. It owns material-class resolution, meso detail masks, packed control RGBA meaning, tier constants, and proof extents. | `WorldTerrainSurfaceMaterialResolver`, `WorldTerrainMesoDetailFields`, and `WorldTerrainDetailContracts` map macro geology to ShellSand, LimestoneShelf, ClaySilt, HardRock, BrineSaltCrust, ManganeseNodulePlain, ReefRubble, and SeepCrust classes. | Quality tiers may alter density, render resolution, and optional detail, but not material-class semantics or packed-control channel meaning. | No material bake, terrain capture, compression/readability, or runtime mask proof was provided by this static audit. |
| `Assets/_Project/Scripts/World/WorldProceduralTerrainSplatmapJobs.cs` | `Hecton8.World`; Burst terrain/surface mask generation jobs. They consume macro/meso fields and write weights/control masks, not persistent terrain authority. | `WorldProceduralTerrainSlopeCavitySplatmapJob` and `WorldTerrainSurfaceMaterialMaskJob` fold macro geology, meso detail, slope/cavity, and resolver weights into terrain splat/control outputs. | Quality tiers may scale resolution/cadence before scheduling; jobs must preserve geology/material identity and navigation reads. | No job schedule window, Unity terrain application, profiler, GC, or visual mask capture was provided by this static audit. |
| `Assets/_Project/Scripts/World/WorldProceduralFieldSampler.cs` | `Hecton8.World`; runtime field sampler and seafloor read bridge. It owns cached read behavior and DataVault/hotswap listener integration, not macro authoring truth or water authority. | Reads active MapMagic/terrain provider heights first, deterministic macro-geology fallback second, and synthetic fallback last. Handles service replacement, cache invalidation, and repeated subscribe/unsubscribe lifecycle. | Quality tiers may scale sample cadence or diagnostic overlays only. They must not alter fallback order, terrain identity, or player-affecting depth truth. | No service replacement, scene unload/domain reload, save/load, stale provider, repeated subscribe/unsubscribe, or no-data runtime proof was provided by this static audit. |
| `Assets/_Project/Scripts/MapMagic/MapMagicBridge.cs` | `Hecton8.MapMagic`; active terrain provider/bridge. It owns bridge-local active payload identity and MapMagic-adapter reads, not the canonical macro-geology contract. | Exposes `ITerrainProvider`, height/normal/AUP/biome/matrix APIs, terrain artifact identity flags, and quality/streaming apply hooks. MapMagic remains a controlled bridge/bake/provider route, not the sole source of terrain truth. | Quality tiers may influence streaming/detail application through bounded hooks, but must not rewrite macro artifact version, runtime seed, or chunk range/hash semantics. | No active payload swap, duplicate owner, stale handle, missing payload, or provider replacement proof was provided by this static audit. |
| `Tools/BuildWorldMacroGeologyPreview.py` | Tool mirror for static preview artifacts under `Docs/GeneratedAssets/Terrain/MacroGeology`. It owns generated manifest creation, not runtime behavior. | Mirrors macro constants, writes previews/manifests, and records storage/runtime/save/load/stale-artifact policy for chunk artifacts. Generated files are evidence artifacts; source code remains authoritative. | Quality tiers must not depend on generated preview presence. Runtime may load baked chunks when present and deterministic-generate on cache miss only through the declared route. | No tool rerun, artifact diff, image validation, Addressables/sidecar load, or write-back proof was provided by this static audit. |
| `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | `Hecton8.World`; static terrain/anomaly/SDF processor. It owns no persistent runtime state, no DataVault handle, no scene object, no signal lane, and no GPU resource; caller owns buffers, phase, completion, and proof. | Schedules closed-basin detection/flood-fill, terrain-to-SDF top-surface snap, ridge/fissure feature detection, mega-pillar SDF injection, deep fissure SDF injection, and lateral SDF displacement over caller-provided `NativeArray`/`NativeQueue` storage. | No direct `GlobalQualityWeight` read is visible in this source. Callers must scale operation budgets continuously before invoking it and must preserve terrain truth, biome/resource IDs, and navigation authority. | No caller route card, DataVault ownership proof, job completion window, profiler, GCMonitor, terrain visual capture, or runtime traversal proof was provided by this static audit. |

Failure paths that must be modeled before runtime acceptance: no terrain data, bad dimensions, duplicate terrain owner, stale height/chunk handle, provider replacement, scene unload, domain reload, interrupted chunk bake/write-back, queue saturation, save/load terrain identity mismatch, water calibration mismatch, voxel overlay conflict, and repeated subscribe/unsubscribe.

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

## First-20 Route Hook

- First-20 moment: world load, first exit, swim, and resource approach across a semi-open shallow terrain route with landmarks, safe return geometry, and readable traversal surfaces.
- Route blocker removed: terrain cannot be random scatter, flat rock, hidden collision, or unproven traversal for the selected opening route.
- Proof class: screenshot from gameplay height, Play Mode/player capture for traversal and return path, Profiler/GCMonitor for runtime scatter or terrain code, import log for generated/imported terrain assets, and static-only manifest for masks/seeds when no runtime path changed.

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
