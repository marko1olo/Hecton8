# PROJECT_ATLAS

Date: 2026-05-26
Status: TOOL CONTRACT / STATIC DOMAIN INDEX
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / GENERATED_SOURCE_INDEX

Purpose: stable root path for atlas tooling and the 85-domain project map. This file preserves required verification tokens and the domain table only; full generated detail belongs in generated artifacts or deprecated snapshots.

Full pre-distillation snapshot: `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/PROJECT_ATLAS.md`.

## Authority

- `Docs/PROJECT_BASELINE.md` defines root documentation boundaries.
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` owns concise current proof snapshots.
- Current C# source under `Assets/_Project` wins over this static index.
- This file is not compile, Unity import, Play Mode, profiler, GC, memory, shader, save/load, or platform proof.

## Required Tool Tokens

These phrases are intentionally preserved because local validators and data tools read this path directly:

- DataSovereignty
- SignalBus
- Data Monolith
- Crafting Fast-Fail Validator
- 85 Identified Domains
- observedAssemblyCount = 83
- observedDomainIndexCount = 85

## H-Phi / DataSovereignty Boundary

- DataSovereignty: one fact has one owner, one route, and one proof artifact.
- SignalBus is the first-party hot broadcast route.
- GlobalDataVault owns cross-domain native memory; it is not a global heap.
- Data Monolith payloads are static data authority, not per-frame lookup excuses.
- Crafting Fast-Fail Validator depends on the Data Monolith/data-sovereignty route and must fail closed when source data or binary payloads drift.

### 85 Identified Domains



| ID | Echelon | Domain | Description |

|---:|---|---|---|

| 1 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `BIOS Bootstrapper` | BIOS Bootstrapper: Awaitable boot orchestration, startup state machine, initialization validation of all systems before removing the boot screen. |

| 2 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Typed SignalBus / NativeQueue Bridge Corridor` | Typed `SignalBus<T>` lanes are the preferred first-party signal route; `GlobalSignals` remains a documented NativeQueue bridge. Static source orientation only; 0-GC/runtime transport claims require fresh profiler/GCMonitor evidence. |

| 3 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Data Archivist (MMF Codec)` | Data Archivist (MMF Codec): Read/write slot_0.sav, binary delta packing, checksums (XXHash3), MMF chunk paging. |

| 4 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Data Monolith (Static DB)` | Data Monolith (Static DB): Parses balances/recipes from binary blobs into a NativeArray at startup. No ScriptableObjects in hot paths. |

| 5 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Origin Shift (AUP Manager)` | Origin Shift (AUP Manager): 64-bit coordinate shift math (int64x3 + float3). RebaseSignal broadcast at sector boundaries. |

| 6 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Native Arena Allocator` | Native Arena Allocator: Custom UnsafeUtility allocator for bypassing TempJob limits and memory pooling for Burst Jobs. |

| 7 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Crash Telemetry (Blackbox)` | Crash Telemetry (Blackbox): Circular event buffer (32-byte structures). Dump to .h8dump during crashes or FPS drops on a background thread. |

| 8 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Scalability Dictator (Hardware)` | Scalability Dictator (Hardware): Continuous `GlobalQualityWeight` scaling of Math LOD, cadence, capacity, and presentation cost from survival budget to visual-overrun budget. |
| 9 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Platform Abstraction Layer (PAL)` | Platform Abstraction Layer (PAL): IInputProvider. Steam Deck broadcasting of Trackpad/gyro/gamepad inputs without API leaks in Core. |

| 10 | `1: CORE & MEMORY INFRASTRUCTURE (Engine Foundation)` | `Tick Dispatcher & Time Dilation` | Tick Dispatcher & Time Dilation: Cadence control (FastTick 60Hz, SlowTick 10Hz, ColdTick 1Hz, FrostTick 0.2Hz) and Bullet Time effects. |

| 11 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `MapMagic Runtime Bridge` | MapMagic Runtime Bridge: 2D Heightmap reader. O(1) height access without allocations. Isolation from third-party SDKs. |

| 12 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Voxel SDF Pipeline` | Voxel SDF Pipeline: 3D distance fields. sbyte density for caves and arches. |

| 13 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Marching Cubes Mesher` | Marching Cubes Mesher: Asynchronous voxel polygonization on worker threads. 2-frame latency hiding. |

| 14 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Voxel Carving (Deformation)` | Voxel Carving (Deformation): Laser drilling, RLE compression of modified cells, writing deltas to savegames. |

| 15 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `BRG Scatter Director` | BRG Scatter Director: BatchRendererGroup instancing of grass and coral. 15Hz Frustum Culling in Compute Shader. |

| 16 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Procedural Wreckage Assembler` | Procedural Wreckage Assembler: Modular assembly of ghost ships (wrecks) based on WorldSeed and AUP. |

| 17 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Geological Node Spawner` | Geological Node Spawner: Distribution of ore veins. Deterministic LCG spawning based on a terrain mask. |

| 18 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Biome Transition Manager` | Biome Transition Manager: Mathematical blending of colors, fog, and biome parameters at boundaries (Dithered blending). |

| 19 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Abyssal Flow Fields` | Abyssal Flow Fields: 3D vector field of underwater currents. Affects the physics of submarines, GPU fish, and particles. |

| 20 | `2: WORLD GENERATION & TERRAIN (Topology and Environment)` | `Thermal Vents & Geysers` | Thermal Vents & Geysers: Danger zones. Ejects boiling water (physical impulse + damage) based on TriangleWave cycles. |

| 21 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Ecosystem Director (Macro)` | Ecosystem Director (Macro): Lotka-Volterra equations. FrostTick migration of biomass without GameObjects. |

| 22 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Fauna Spatial Hash Grid` | Fauna Spatial Hash Grid: O(1) neighbor search. Collision resolution and aggression radii in Burst. |

| 23 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Swarm Compute Director (Boids)` | Swarm Compute Director (Boids): GPU simulation of small fish schools. Swarm fragmentation during sonic booms. |

| 24 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Predator Cognition (Utility AI)` | Predator Cognition (Utility AI): Polynomial scoring of hunger, fear, and aggression. 1km headless fast-fail. |

| 25 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Predator Steering & Lunge` | Predator Steering & Lunge: Octant-aligned navigation, S-curved attacks, obstacle avoidance (SDF raycasts). |

| 26 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `A Funnel Smoothing` | A Funnel Smoothing:* 3D voxel-based pathfinding with smoothed "square" corners (String Pulling). |

| 27 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Leviathan Procedural IK` | Leviathan Procedural IK: Boneless VAT body blending + burst calculation of tentacles/tails (Constrained S-Curves). |

| 28 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Flora Procedural Sway` | Flora Procedural Sway: Vertex shaders for algae animation (interactive bending from submarine propellers). |

| 29 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Bioluminescence Sync` | Bioluminescence Sync: Coral pulsation. Glow phases are tied to global _Time and moon cycles. |

| 30 | `3: FLORA, FAUNA & BIOTA (Ecosystem and AI)` | `Fauna Genetics & Mutation` | Fauna Genetics & Mutation: 64-bit trait masks (size, aggression, coloration) transferred when spawning in biomes. |

| 31 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Kinematic Character Controller (KCC)` | Kinematic Character Controller (KCC): Asynchronous capsule casts. Speculative collisions. Complete absence of SphereCastNonAlloc in the main thread. |

| 32 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Hydrodynamic Drag & Buoyancy` | Hydrodynamic Drag & Buoyancy: Scalar mass and inertia calculation. force * math.rcp(mass + addedMass). |

| 33 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Contextual Hand IK (FABRIK)` | Contextual Hand IK (FABRIK): Procedural sticking of player hands to ladders, airlocks, and tools. |

| 34 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Tether & Cable Physics` | Tether & Cable Physics: Tether physics using acceleration constraints. Without Unity Joints. |

| 35 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Equipment Runtime (Tools)` | Equipment Runtime (Tools): Cutter heating math, scanner power consumption (SOA arrays). |

| 36 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Scavenging & Harvesting` | Scavenging & Harvesting: Convert procedural loot points to ItemIds upon cutting/interaction. |

| 37 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `S.O.A. Inventory System` | S.O.A. Inventory System: Inventory as a NativeArray<int> (ID, Quantity, Wear). Search via math.tzcnt. |

| 38 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `Crafting Fast-Fail Validator` | Crafting Fast-Fail Validator: Bitmasks for checking the presence of recipes for 1 CPU cycle before the discard cycle. |

| 39 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `VR Somatic Comfort` | VR Somatic Comfort: OpenXR bridge. Virtual horizon locking, FOV tunneling during jerks, Foveated Rendering. |

| 40 | `4: PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)` | `VR Interaction Bridge` | VR Interaction Bridge: Physically grabbing objects with your hands, translating controllers into the 3D world. |

| 41 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Combat Damage Router` | Combat Damage Router: NativeQueue for ForcePackets. Damage distribution to modules, players, or creatures in a single pass. |

| 42 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Armor Penetration LUT` | Armor Penetration LUT: 8x8 penetration tables. Hitboxes based on local AABB primitives (Headshot multipliers). |

| 43 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Status Effects Engine` | Status Effects Engine: 32-bit masks. Bleeding, poison, and stun processing in the SlowTick job. |

| 44 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Player Stress & Fear System` | Player Stress & Fear System: Panic buildup from darkness and sounds. Affects O2 consumption and distortion shaders. |

| 45 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Decompression Sickness (Bends)` | Decompression Sickness (Bends): Calculates tissue nitrogen saturation. Instantaneous damage when ascending > 10 m/s from a depth of > 100 m. |

| 46 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Hypoxia & Gas Toxicity` | Hypoxia & Gas Toxicity: Nitrogen intoxication (introduces noise into controls), CO2 poisoning (stamina penalty). |

| 47 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Crush Depth Integrity` | Crush Depth Integrity: Pressure calculation. Exponential suit destruction overDepth * math.rsqrt(overDepth). |

| 48 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Diet & Metabolism` | Diet & Metabolism: Hunger, hydration, core body temperature. Affects regeneration rate. |

| 49 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Radiation Scrubber` | Radiation Scrubber: Contaminated areas from reactors. Rad dose accumulation, hand texture mutation. |

| 50 | `5: COMBAT & SURVIVAL PHYSIOLOGY (Combat and Body)` | `Screen-Space Wounds & Decals` | Screen-Space Wounds & Decals: Rendering visor cracks and blood through decals/postprocess (Math LODs). |

| 51 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Grid Snapping & Ghost Preview` | Grid Snapping & Ghost Preview: Snapping modules to a 4x4m grid (Integer AUP). Checking AABB collisions without prefab instantiation. |

| 52 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Structural Integrity Math` | Structural Integrity Math: Scalar calculation of base deformation (Depth - Reinforcements). Stress factor output to the shader. |

| 53 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Fluid Incursion (Flooding)` | Fluid Incursion (Flooding): BFS water distribution between compartments. The mass of water bends the submarine. |

| 54 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Power Grid (Jacobi Solver)` | Power Grid (Jacobi Solver): Relaxation algorithm for energy distribution over the network. Bitwise disconnection of broken wires. |

| 55 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Pipe & Sump Pump Logistics` | Pipe & Sump Pump Logistics: Liquid and oxygen transfer system. Automatic pumping when energy is surplus. |

| 56 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Module Deconstruction` | Module Deconstruction: Mathematically net return of 50% of resources with a rollback of the connection graph. |

| 57 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Submarine OS (Core)` | Submarine OS (Core): Submarine terminal. Engine temperature, sonar telemetry, battery consumption. |

| 58 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Submarine Navigation (Auto-Level)` | Submarine Navigation (Auto-Level): Physical pitch and roll stabilizers of the vessel. Buoyancy control (Ballast). |

| 59 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Drone Fleet Commander` | Drone Fleet Commander: AI for repair and mining mini-bots. Stateless solutions based on distances. |

| 60 | `6: HABITAT & VEHICLES (Engineering and Bases)` | `Scooter (Seaglide) Kinematics` | Scooter (Seaglide) Kinematics: Additional thrust vectors and physics for a manual submarine tug. |

| 61 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Celestial Orbit Mechanics` | Celestial Orbit Mechanics: Triangular waves of moon and planet orbits on FrostTick. Without Kepler. |

| 62 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Tide & Seismic Generator` | Tide & Seismic Generator: Deterministic earthquakes (affect caves) and tides (affect AUP water levels). |

| 63 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Weather & Wind Director` | Weather & Wind Director: Merging storms on the surface with turbidity (Silt) at depth. |

| 64 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Gas Dynamics (Dalton's Law)` | Gas Dynamics (Dalton's Law): O2/CO2 partial pressure in compartments. Calculated without particle simulation. |

| 65 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Thermodynamics (Heat Diffusion)` | Thermodynamics (Heat Diffusion): Heat diffusion from geothermal sources. Boiling of water around. |

| 66 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Marine Snow & Silt Compute` | Marine Snow & Silt Compute: GPU fog that reacts to localized flows of submarine propellers (Propwash). |

| 67 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Volumetric Fog & Light Shafts` | Volumetric Fog & Light Shafts: Bezier fog ray-marching. Dithered masks for MX350. |

| 68 | `7: ATMOSPHERE & CELESTIAL (Macro-World)` | `Day/Night GI Relay` | Day/Night GI Relay: Changing ocean lighting and color (Cyan to Deep Blue). Affects predators. |

| 69 | `8: PRESENTATION & UX (Interaction and Perception)` | `Zero-GC Subtitles (Babel)` | Zero-GC Subtitles (Babel): ReadOnlySpan<char> + CharBufferPool. Localization without a single string allocation. |

| 70 | `8: PRESENTATION & UX (Interaction and Perception)` | `Diegetic Terminals (3D UI)` | Diegetic Terminals (3D UI): Projecting mouse/gamepad inputs into UV space of 3D monitors. |

| 71 | `8: PRESENTATION & UX (Interaction and Perception)` | `Visor AR (HUD)` | Visor AR (HUD): Stencil helmet masks. Rational approximations of tangents for the scanner. |

| 72 | `8: PRESENTATION & UX (Interaction and Perception)` | `PDA Encyclopedia Streaming` | PDA Encyclopedia Streaming: MMF reading of ENT pages directly from disk. 128-byte masks of open records. |

| 73 | `8: PRESENTATION & UX (Interaction and Perception)` | `AUP Narrative Triggers` | AUP Narrative Triggers: Spatial POIs (points of interest). Triggered via math.distancesq. |

| 74 | `8: PRESENTATION & UX (Interaction and Perception)` | `Cartography & Fog of War` | Cartography & Fog of War: Opening a 3D sonar map with data packing in 1-bit voxels. |

| 75 | `8: PRESENTATION & UX (Interaction and Perception)` | `Frequency Tuning (Scanning)` | Frequency Tuning (Scanning): Artifact search minigame by matching signal/noise sine waves. |

| 76 | `8: PRESENTATION & UX (Interaction and Perception)` | `DSP Acoustic Radar` | DSP Acoustic Radar: Zonal sound occlusion. Muffle zones (muffling) without ray checks. |

| 77 | `8: PRESENTATION & UX (Interaction and Perception)` | `Granular Synthesis` | Granular Synthesis: Procedural grinding of hull metal under pressure. On-the-fly sample slicing. |

| 78 | `8: PRESENTATION & UX (Interaction and Perception)` | `Vocal Warning System (VWS)` | Vocal Warning System (VWS): "Bitchin' Betty". Audio warning queue (alarm bitmasks). |

| 79 | `9: META, POLISH & INTEGRATION (Quality Control)` | `Haptic Feedback Director` | Haptic Feedback Director: Translates ImpactEvents into micro-vibrations of controllers (Steam Deck/DualSense). |

| 80 | `9: META, POLISH & INTEGRATION (Quality Control)` | `Camera Juice & Shake` | Camera Juice & Shake: Procedural camera shake impulses (without AnimationClip). |

| 81 | `9: META, POLISH & INTEGRATION (Quality Control)` | `Physics Culling Overseer` | Physics Culling Overseer: Rigidbody.Sleep() for objects further than 50m. Deactivate colliders. |

| 82 | `9: META, POLISH & INTEGRATION (Quality Control)` | `The Integrator (Compile Medic)` | The Integrator (Compile Medic): Blind compiler. Fixes cyclic dependencies and .asmdef files. |

| 83 | `9: META, POLISH & INTEGRATION (Quality Control)` | `The Chronicler (Docs)` | The Chronicler (Docs): Reads source/asmdef/doc contracts and generates volatile dependency artifacts under `Docs/Generated`; root `PROJECT_ATLAS.md` stays a stable 85-domain/tool-token contract. |

| 84 | `9: META, POLISH & INTEGRATION (Quality Control)` | `QA Watchdog Bot` | QA Watchdog Bot: A bot that swims 10 km in PlayMode. FPS and memory dump to CSV via TryFormat. |

| 85 | `9: META, POLISH & INTEGRATION (Quality Control)` | `Tech Researcher (Mandate Evolver)` | Tech Researcher (Mandate Evolver): An agent that updates rules .agents-skills/ for Unity 6000.x features. |

## Generated Detail Boundary

Regenerate expanded atlas detail with the project architecture tools when needed. Keep root concise; promote only stable facts into active contracts.
