# HECTON-8 Voxel And SDF Bible

Status: AUTHORING LAW / STATIC DOC / RUNTIME PROOF NOT IMPLIED
Scope: voxel caves, SDF terrain, carving, Marching Cubes output, seams, collision bake, persistence, and visual quality.

## First-20 Route Hook

- First-20 moment: world load, swim, tool interaction, first route change, and save/load return need seam-safe shallow caves/cuts, readable terrain silhouettes, collision-baked traversal, and persistent carve or blockage state where used.
- Route blocker removed: prevents the opening route from exposing blocky caves, terrain/voxel seams, unbaked collision, direct per-tool rebuilds, or voxel edits that cannot survive save/load.
- Proof class: STATIC_DOC only; route acceptance still requires seam/skirt capture, mesh/collider bake proof, persistence delta proof, compact/high visual capture, and profiler/GC/memory evidence for runtime carving or rebuild changes.

## Prime Law

Voxel terrain must never look like blocks, random blobs, or low-poly caves. HECTON-8 voxel work exists to create carved pressure geography: caves, fractures, flooded industrial cuts, volcanic vents, collapsed tunnels, and player-made scars that remain physically readable.

## Current Source Boundary

The active first-party voxel path is Marching Cubes with float working SDF buffers, sbyte quantized extraction, edge-indexed vertex construction, baked normals, curvature/skirt channels, UV3 absolute world position, staged mesh upload, and staged physics bake.

Do not replace this path with Transvoxel, half-buffer runtime truth, or a second primary mesher because an older mandate mentions it. Migration requires owner, fallback, save compatibility, seam proof, MX350 profiler proof, and visual comparison.

## Truth Ownership

Voxel owns SDF field state, carve deltas, extraction state, seam state, mesh upload admission, collider bake state, and voxel persistence deltas. It does not own player movement, tool permission, AI cognition, world narrative, or rendering presentation beyond the mesh/material payload it publishes.

Tools, construction, AI, physics, and rendering consume voxel-owned snapshots or baked outputs. They do not edit voxel buffers directly.

## SDF Contract

Signed distance convention in production HECTON-8 SDF fields:

- positive = solid rock (depth below terrain surface);
- zero = surface boundary;
- negative = void (open air or water).

`ProceduralCaveSdfCarveJob` in `WorldProceduralCaveSdfJobs.cs` is the canonical owner of 3D cave carving and mandates `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Coordinates use double-precision AUP wrapped via a `6627.0m` period to preserve continuity across chunk boundaries.

Every SDF operation must guard division, clamp exponentials, preserve finite values, and keep material IDs or blend weights where surface materials change.

Allowed shape logic:

- analytic primitives for clean cuts and forms;
- smooth union where biological or eroded blending is wanted;
- crisp subtract for tool cuts and industrial tunnels;
- bounded displacement below topology inversion threshold;
- deterministic noise with named seed.

Random noise pasted onto a cave is rejected.

## Mesh Extraction

Marching Cubes output must:

- clamp edge interpolation away from exact corners;
- avoid degenerate triangles;
- use deterministic edge ownership;
- bake normals from density gradients;
- store curvature or seam/skirt support channels where required;
- generate UV/material data compatible with `3dmodel.md`;
- upload meshes only through the approved staged route;
- mark physics bake pending before interaction.

Physics interaction is blocked until collider bake is complete.

## Carving And Persistence

Runtime carving is gameplay truth only when it creates a physical decision: access, blockage, resource extraction, breach, repair, evidence, or hazard.

Carving operations must batch. Direct per-frame SDF edit and mesh rebuild is rejected. Dirty chunks must propagate to neighbors when boundary voxels change.

Persistent voxel changes are stored as deltas from deterministic seed state, not whole-world snapshots. Per-tile edit caps and corruption validation are mandatory.

## Seam And Terrain Integration

Terrain/voxel seams must be solved as geometry, normals, collision, and raycast ownership. A seam hidden only by fog is not accepted.

Required:

- overlap or skirt band;
- normal blending near terrain boundary;
- collision arbitration so the player does not hit duplicate colliders;
- unified raycast route for tools and interaction;
- LOD synchronization;
- AUP/floating-origin safety;
- **SurfaceProtectionMeters & Heightmap Protection**: To prevent 3D voxel carving from punching gaping holes through the 2D heightmap, the carving density must fade to zero within `30 meters` of the terrain surface (`depthToTerrainSurface < 30f`, scale density via `smoothstep`).
- **CaveMouthCandidate Masks**: Exiting voxel caves onto the 2D surface is allowed strictly through designated `CaveMouthCandidate` masks using transition meshes (collars/seams) to cover the geometry junction.

## SDF Collision Read Model

Voxel owns environment SDF truth and collider bake admission for voxel caves, carved terrain, tunnels, and large geology. Player movement, vehicles, AI, tools, and physics systems consume voxel-owned read models, baked colliders, or generation-checked DataVault snapshots; they do not run their own terrain/voxel truth.

Rules:

- traversal collision for character and vehicle movement must prefer baked voxel/terrain colliders or an approved SDF read model over synchronous cast chains;
- physics interaction remains disabled until collider bake or SDF read-model readiness is proven for the touched chunk;
- unified tool/interaction ray routes may query the voxel read model or a bounded cast bridge, but the bridge must name owner, cadence, buffer, and migration/fallback;
- stale SDF handles, missing chunk bake, non-finite density, and seam arbitration failure must be black-boxed and surfaced to physics/tool owners;
- `GlobalQualityWeight` may scale SDF resolution, extraction distance, diagnostic depth, and rebuild cadence, but it must not change collision truth, carve permission, or save delta identity.

A movement route that depends on hot synchronous `SphereCast`, `CapsuleCast`, or `Raycast` chains for terrain/voxel environment collision is migration debt unless a current physics/voxel route card proves why the SDF/collider route cannot serve it.

## 2026-06-05 Static Source Anchors

Evidence class: STATIC_SOURCE only. Compile, Unity import, voxel carve replay, profiler, GC, save/load, and player-build proof remain PENDING VERIFICATION.

| Runtime | Owner / boundary | Static route | GlobalQualityWeight consequence | Missing proof |
|---|---|---|---|---|
| `Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs` | `Hecton8.Gameplay.Mining`, DataVault owner `SystemID.GameplayTools`; deployable powered SDF thumper/mining node. It is not the handheld starter `SeafloorDrillTool`, does not own voxel terrain authority, and must treat SDF edits as requested visual/authoritative routes owned by voxel/mining owners. | Registers cold tick, late-frame tick, origin-shift, pool, cuttable, hot-swap, and interactable tree routes. Owns DataVault buffers `DeployableSdfDrillSlotOwners`, inventory capacities/quantities/item hashes/ore hashes, `DeployableSdfDrillBlackBox`, and `DeployableSdfDrillExtractionResult`. Statically references `VoxelDeltaProcessor`, `HectonVoxelVolume`, and `IVoxelSonarSdfReadModel`; emits `AcousticPingSignal`, `ItemAcquiredSignal`, `CombatDamageSignal`, and `DebrisSpawnSignal`. | Reads `HomeostasisBrain.GlobalQualityWeight` for snap SDF step and visual carve cadence/weight with hysteresis. Static source states quality affects visual SDF carve cadence; extraction truth and inventory authority must not downgrade with graphics quality. | No voxel-delta persistence proof, SDF collision/read model proof, visual carve capture, profiler/GCMonitor, save/load, power-grid runtime proof, or black-box dump artifact was provided by this static audit. |
| `Assets/_Project/Scripts/World/HectonAnomalyEngine.cs` | `Hecton8.World`; caller-owned SDF processor for terrain snap, mega pillars, deep fissures, and lateral SDF displacement. It has no persistent DataVault/GPU/signal ownership. | Schedules jobs over caller-provided `NativeArray<float>` SDF and terrain height storage; caller owns job dependency, completion window, dirty chunk propagation, and persistence. | No direct quality read is visible. Callers must scale SDF dimensions/budgets continuously through their own `GlobalQualityWeight` route without changing carve permission, save delta identity, or collision truth. | No owning caller, proof artifact, player traversal capture, mesh/collider bake proof, profiler, GCMonitor, or save delta validation was provided by this static audit. |

## Visual Quality And GlobalQualityWeight Scaling

Voxel surfaces need geology, not polygons:

- strata;
- erosion ledges;
- pressure fractures;
- mineral seams;
- wet cavities;
- sediment shelves;
- tool scars;
- scale witnesses;
- route silhouettes.

High/Ultra may increase SDF resolution, surface detail, and seam quality, but Compact must keep navigational silhouette and collision truth.

`GlobalQualityWeight` may scale SDF resolution, extraction distance, material blend richness, seam/skirt detail, tool-scar decal density, diagnostic overlay depth, and background rebuild cadence. It must not change SDF truth, carve permission, save delta identity, collision bake requirements, or terrain ownership.

## Rejection Gates

Reject:

- blocky terrain unless explicitly authored as machinery;
- smooth noise caves with no geological history;
- visible seams between terrain and voxel meshes;
- direct mesh rebuild per tool tick;
- collision enabled before bake completion;
- voxel saves without delta schema and checksum;
- migration away from current pipeline without source-compatible fallback.

## Proof Artifacts

Voxel work must provide:

- SDF convention and operation list;
- dirty chunk count and rebuild budget;
- mesh extraction validation report;
- seam/skirt proof;
- collider bake proof;
- persistence delta schema and checksum;
- Compact and High visual capture where terrain changed;
- profiler/GC/memory proof for runtime carving or rebuild changes;
- black-box fields for invalid SDF, failed bake, seam failure, and rebuild overload.

## Acceptance Sentence

Voxel work is accepted only when it is deterministic, seam-safe, persistent, collision-baked, visually geological, and bounded enough to run without starving player-critical systems.
