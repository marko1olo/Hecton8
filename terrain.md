# HECTON-8 Terrain, Biomes, Scatter, And World Surface Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: terrain surfaces, caves, cliffs, biome masks, MapMagic/runtime terrain bridges, scatter placement, ore/geology nodes, biome transitions, navigation readability, and terrain proof gates.

## Prime Law

Terrain is route, pressure, geology, and memory. It is not random noise.

Every terrain surface must explain how water, pressure, sediment, industry, collapse, biology, and salvage shaped it. HECTON-8 rejects toy low-poly terrain, smooth procedural blobs, random coral carpets, generic resource scatter, square-map feeling, and terrain that looks good only from one screenshot angle.

## Truth Ownership

Terrain owns surface shape, biome masks, scatter eligibility, geological logic, navigation affordances, and terrain validation. It does not own voxel persistence, generated asset topology, runtime streaming, physics truth, water truth, or narrative facts.

Current terrain source-of-truth route:

- `WorldMacroGeologyFields` owns deterministic macro-geology evaluation and the terrain artifact identity contract: authoring seed, macro artifact version, chunk size, chunk range, and chunk range hash. Exact default production constants: `ShelfDepthMeters = 90.0f`, `AbyssDepthMeters = 2950.0f`, `HadalDepthMeters = 4600.0f`, `ShelfBreakWidthMeters = 5200.0f`, `RidgeHeightMeters = 1550.0f`, `RidgeWidthMeters = 2350.0f`, `TrenchDepthMeters = 900.0f`, `TrenchWidthMeters = 2200.0f`, `BasinDepthMeters = 620.0f`, `DetailProbeMeters = 120.0f`.
- `WorldTerrainDetailContracts` owns the macro sample to terrain material/control contract: material classes, meso detail fields, packed control masks, and proof extents.
- `WorldProceduralTerrainSplatmapJobs` consumes macro and meso fields to produce runtime terrain/surface masks. It must not invent a separate geology truth.
- `MapMagicBridge` and MapMagic nodes are bridge/provider/bake adapters. They may supply active height payloads, splat payloads, biome matrices, and chunk identity, but the current macro-geology contract does not come from an old hand-authored MapMagic graph.
- `HectonTerrainSampling.hlsl` enforces stochastic anti-tiling invariants: explicit UV gradients (`ddx`, `ddy`) are pre-calculated before dynamic branching to eliminate GPU quad derivative divergence; perceptual space bilinear blending (`sqrt -> blend -> sq`) prevents anti-tiling darkening; cubic smooth weights (`w = fuv * fuv * (3.0 - 2.0 * fuv)`) enforce C2 continuity across cell boundaries.
- `HectonSandboxAbyssalShelfJobs.cs` enforces 64-bit double precision AUP coordinates (`AupCellSizeMeters`, `DescentRadiusMeters`, `PlateCellSizeMeters`) and explicit Burst Compilation attributes `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
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
| `Assets/_Project/Scripts/WorldProceduralFieldSampler.cs` | `Hecton8.World`; runtime field sampler and seafloor read bridge. It owns cached read behavior and DataVault/hotswap listener integration, not macro authoring truth or water authority. | Reads active MapMagic/terrain provider heights first, deterministic macro-geology fallback second, and synthetic fallback last. Handles service replacement, cache invalidation, and repeated subscribe/unsubscribe lifecycle. | Quality tiers may scale sample cadence or diagnostic overlays only. They must not alter fallback order, terrain identity, or player-affecting depth truth. | No service replacement, scene unload/domain reload, save/load, stale provider, repeated subscribe/unsubscribe, or no-data runtime proof was provided by this static audit. |
| `Assets/_Project/Scripts/MapMagicBridge.cs` | `Hecton8.MapMagic`; active terrain provider/bridge. It owns bridge-local active payload identity and MapMagic-adapter reads, not the canonical macro-geology contract. | Exposes `ITerrainProvider`, height/normal/AUP/biome/matrix APIs, terrain artifact identity flags, and quality/streaming apply hooks. MapMagic remains a controlled bridge/bake/provider route, not the sole source of terrain truth. | Quality tiers may influence streaming/detail application through bounded hooks, but must not rewrite macro artifact version, runtime seed, or chunk range/hash semantics. | No active payload swap, duplicate owner, stale handle, missing payload, or provider replacement proof was provided by this static audit. |
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

## Terrain Math & Slope Rules

[RULE] Coordinate Wrap Protection: Using coordinate-reflecting functions like `math.abs(wrapX - period)` in generation algorithms is strictly banned. This produces a Triangle Wave domain input for fractal noise, causing mirror symmetry (kaleidoscope effect) across chunk borders. Continuous coordinate wrapping must be achieved via signed modulo (`math.fmod`) exclusively, preserving direction sign. Raw `worldPos.x` / `worldPos.z` combined with AUP sector seed is the preferred base domain.

[RULE] Steepness & Splatmap Mappings: Ban early slope map saturation (e.g. `slope * 4.5f` which turns everything > 12.5 deg into rock, making sand appear only on a billiard-table flat surface). Use `math.saturate(slope * 0.6f)` and smoothstep ranges. The canonical H8 splatmap formula:

```csharp
float steepSlope = math.smoothstep(0.16f, 0.28f, sample.Slope01); // 15-25 deg transition
float verySteep  = math.smoothstep(0.40f, 0.70f, sample.Slope01); // >45 deg full rock
float rockBase   = math.saturate((hardRock * 0.72f) + (ridge * 0.44f) + (sample.FaultMask * 0.24f) - (sediment * 0.22f));
float finalRock  = math.saturate(math.max(rockBase, steepSlope) + verySteep * 2f);
```

Geological masks must account for macro-zones: in fault lines bare rock must appear even at low slope; on the shelf sand may persist on slopes up to 20 degrees.

## Multi-Scale Geology Manifesto

HECTON-8 seafloor is not a noise field — it is physically aggressive, tactile geology that affects KCC kinematics, cover calculations, and flora simulation. Generation in `WorldMacroGeologyFields.cs` is split into four mathematically strict Burst-compiled layers:

### Macro-Scale (1–10 km): Tectonic Structure

Owner: low-frequency `FractalSimplexNoise` with mild Domain Warping (perturb input coordinates by a secondary low-amplitude noise before sampling). Domain Warping avoids perfectly round or square tectonic shapes. This layer forms the Base Depth — the continental shelf vs. abyss boundary. **No Domain Warping on chunk seam coordinates** — only apply warping after the worldPos is confirmed continuous via fmod.

### Meso-Scale (100 m – 1 km): Canyons & Erosion

Owner: inverted `RidgedMultifractal01` **subtracted** from shelf height, creating deep V-shaped canyons. Canyon edges are softened via `math.smoothstep`. This layer produces the readable trench and ravine routes players navigate. Output must produce branching dendritic drainage patterns visible on a 1 km slope X-Ray card — a blurry smear means the frequency or amplitude is wrong.

### Micro-Meso Scale (10–100 m): Geological Terraces (Strata)

Owner: smoothed height quantization formula:

```csharp
// terraceErosion = high-freq noise injected BEFORE dividing by step scale
// Without it, strata edges are perfectly straight lines (Minecraft topographic map look)
float noisy = depth + terraceErosion;
float terrace = math.floor(noisy / terraceScale) * terraceScale
              + math.smoothstep(0f, 1f, math.frac(noisy / terraceScale)) * terraceScale;
```

Terracing is applied **only through a slope mask** — not on vertical cliffs (already rock), not on flat plains (already sediment). Strata must appear on mid-angle geology only.

### Micro-Scale (< 1 m): Physical Grit (Osyp' — Rock Scree)

We do NOT fake surface micro-detail through normal maps alone. On the height mesh itself we add/subtract a high-frequency `RidgedMultifractal` with amplitude **1.5–3.5 metres** strictly through HardRock and Scree slope masks.

Rationale: the KCC uses `CapsuleCast` against `TerrainCollider`. On a flat mesh the player slides over rock like ice. Physical micro-bumps make the KCC stumble realistically. Shadow Cascades cast real micro-shadows at low sun angles, adding volume that a normal map cannot reproduce.

Proof gate: a 100 m slope X-Ray card (GetSteepness export) must show dense red-black noise on hard rock slopes — proving the mesh is deformed by math, not painted by normal maps.

## HectonTerrain Shader Doctrine

Shader: `HectonTerrain.shader`. Mode: Single-Pass with 8-layer `Texture2DArray` (Albedo, Normal, Mask).

### Biplanar Mapping (Required on Slopes > 45°)

Standard planar UV on XZ produces catastrophic texture stretching on vertical faces. Triplanar is too expensive. Required approach:

1. Convert surface normal to World Space in HLSL.
2. Take `abs(normalWS)` and find the two strongest axes (discard the weakest).
3. Sample `Texture2DArray` twice, blend by axis weights.

This gives 2 texture fetches vs 3 for triplanar, with no stretching on cliff faces.

### Anti-Tiling Macro Noise (Required)

`_HectonUVScale` is set for high density (10–20 m per tile). At bird's-eye view this creates a checkerboard. Fix: sample a second pass of the same texture with UV **rotated 60 degrees**, then blend the two by a very low-frequency fractal noise mask (`HectonMacroNoise(worldPos.xz)`). Result: readable close up, non-repeating from above.

### Height-Based Blending (Required at biome transitions)

Alpha blending produces smearing at material boundaries. At ShellSand / HardRock transitions:

```hlsl
// Read Displacement channel (Alpha in Albedo or Blue in MaskMap)
float depthA = textureHeightA + splatWeightA;
float depthB = textureHeightB + splatWeightB;
float blend  = saturate((depthA - depthB) / contrastBias + 0.5);
```

This makes sand granules appear only in rock crevices — a sharp, photorealistic AAA seam.

### Texture2DArray Baker Rule

Source PBR textures may differ in resolution (512×512, 1024×1024). `Texture2DArray.SetPixels()` will crash on size mismatch. Required approach (`BakeDeepSeaTerrainArrays.cs`): blit every source texture via `Graphics.Blit` into a temporary `RenderTexture` at uniform target size (1024×1024), then read pixels from there into the array. This GPU-powered resize pipeline must be run before build and before any terrain validation session.

## X-Ray Matrix Protocol

We do not trust shaded 3D screenshots for terrain validation. An agent MUST produce raw data exports via `TerrainData.GetHeights()` and `GetSteepness()` stitched into unified PNG maps (9-chunk 1536×1536). These X-Ray maps are the only accepted terrain truth.

| Scale | Map | What to look for | Failure signature |
|---|---|---|---|
| **10 km** (Macro) | Heightmap + Slope | No perfectly straight red/black lines at chunk borders (those = seams) | Straight lines across borders = coordinate drift bug |
| **1 km** (Meso) | Slope | Branching dendritic ravines (red veins on black) | Blurry grey smear = frequency/amplitude wrong |
| **100 m** (Micro) | Slope | Dense red-black noise on hard rock slopes (1–3 m amplitude grit) | Smooth gradient = Grit not applied; KCC will slide |

Python validators may only evaluate X-Ray maps (raw height/slope arrays). They are **banned from evaluating Beauty Renders** — beauty assessment is Multimodal Vision (AI eyes) only, with mandatory ACES tonemapping, soft shadows, and Exponential Depth Fog enabled.

## Clean Room Testing Protocol

Testing on `02_HECTON_WORLD` is banned — bootstrapper overhead and system coupling make it unreliable. Isolated testing scene or `020_RENDER_SANDBOX_V2` only.

Required setup, driven from `Assets\_Project\Scripts\Editor\CleanRoomTerrainTest.cs` (the live runner; the
NakedTerrainProtocolRunner.cs (unbackticked: dead path) named here previously does not exist — verified 2026-07-27, the `NTP_`
prefixes below are what remains of that name):

1. Destroy all cameras and lights in scene.
2. Create `NTP_Camera` (SolidColor background, `Color(0.02, 0.03, 0.05)`).
3. Create `DirectionalLight` (Intensity 1.8, Color.white, `LightShadows.Soft`, Pitch 35° for long micro-shadows).
4. Create `GlobalVolume` with **ACES tonemapping** — never disable ACES to pass a pixel check.
5. Initialize MapMagic graph, subscribe to `EditorApplication.update` state machine.
6. Wait condition: `Terrain.activeTerrains.Length == 9` AND every terrain has `alphamapTextureCount > 0` AND every terrain has an active `TerrainCollider` AND 200+ stable frames of full quiescence.
7. Only after 200-frame quiet window: render via `UniversalRenderPipeline.SubmitRenderRequest`.

**Anti-Hallucination Camera Raycast**: Before rendering beauty shots, cast `Physics.SphereCast` (radius 2 m) from the target point toward the camera. If the cast hits a `TerrainCollider`, shift camera 5 m along the hit normal. This permanently eliminates black-screen-inside-rock bugs.

**No `-nographics` flag ever**: MapMagic uses Compute Shaders and `Graphics.Blit` for splatmap layer composition. Without a GPU context these return zeros. The only valid batch test mode is Play Mode or Editor Play with GPU context.

## Zero-GC Terrain Height Reads

**Banned at runtime:** `Terrain.SampleHeight()` and `TerrainData.GetHeights()` are managed-allocation calls. Banned in all hot game loops (creature AI depth queries, player spawner, AbyssalThermalManager pressure queries, etc.).

**Required architecture:**

1. On chunk generation complete (or chunk stream-in), the terrain subsystem copies its height data via a Burst job into a flat unmanaged buffer.
2. That buffer is registered in `GlobalDataVault` under `BufferID.WorldTerrainHeights`.
3. Any external gameplay system (e.g. `HectonPlayerSpawner`, shark AI depth queries, `AbyssalThermalManager`) requests a **Read-Only generation handle** from `GlobalDataVault`. It reads height at XZ coordinates via pure O(1) index math inside its own `IJobParallelFor`, then immediately releases the handle.
4. On chunk unload: height buffer handle is disposed, DataVault entry cleared.

This guarantees 100% thread-safety, zero main-thread blocking, and zero GC allocations.

## Stale Artifact Rule

Before every Unity session that produces terrain screenshots or exports: delete all `.png` files in the artifact output directory and all `.log` files in the Logs directory via `Remove-Item -Force`. No file = no hallucination. An agent that reads a yesterday's `Naked_Macro_10km.png` and writes a report from it must be treated as producing a fabricated result.

## Atomic File Delete Rule (Pre-Run Mandatory)

```powershell
# Run this BEFORE every Unity terrain bake/test session
Remove-Item -Path "C:\hades\Hecton8\Docs\GeneratedAssets\Terrain\*.png" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "C:\hades\Hecton8\Logs\*.log" -Force -ErrorAction SilentlyContinue
```

