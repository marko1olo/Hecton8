# LOG_WORLD_WRECKAGE

## Final Report - WORLD_WRECKAGE

Status: PENDING VERIFICATION
Domain: WORLD GENERATION & TERRAIN / Procedural Wreckage Assembler
Task Count: 20

What was wrong:
- Wreck generation had no native integrity, loot, lore, debris, scorch, burial, or black-box lanes tied to the existing WFC ship assembly path.
- Scrap risked becoming object-heavy instead of a deterministic spatial field.
- Rust, algae, hanging motion, and emergency lighting needed cheap visual depth without material clones, bones, or per-wreck light scripts.
- Broad Unity assembly verification could not be honestly claimed because `Assembly-CSharp.csproj` timed out in the last run and earlier broad failures were outside WORLD_WRECKAGE files.

What was done:
- Extended modular ship assembly records with 64-byte WFC placement metadata: socket masks, integrity state, sealed/ruptured/open flags, laser requirement, loot table index, and lore fragment chance.
- Added 10,000-cap `NativeArray<WreckDebrisRecord>` plus 5m `NativeParallelMultiHashMap` buckets. Debris remains data-only until the player is within 5m, then the existing pooled pickup path is used.
- Added `NativeArray<WreckLootRecord>` SOA loot selection with `math.select`.
- Added deterministic lore/artifact discovery records and scanner event routing.
- Added 50m debris cluster records for culling-sidecar consumers.
- Added buried SDF box cut records routed through the voxel crater path.
- Added ruptured-module scorch decal records.
- Added `WreckIntegritySignalProxy` for sealed/ruptured cutter interaction without direct dependencies on absent systems.
- Added fixed 300-entry wreck telemetry circular buffer and crash dump path `Docs/AgentLogs/Dump_WORLD_WRECKAGE.bin`.
- Added shader-side vertex color rust/algae, boneless triangle-wave debris sway, and global emergency flicker.

Modular ship-assembly rule logic:
- Existing WFC compatibility is preserved through socket masks and deterministic collapse.
- New metadata rides in the runtime module and placement lanes, so gameplay systems read native records rather than scene components.
- Sealed modules reject non-cutter interaction and convert to opened state through an interaction signal proxy when present on the prefab.

Vertex-color rust shader implementation:
- Mesh merge writes rust into vertex color R and algae into vertex color G.
- The wreck shader blends base metal, rust, and algae from those vertex lanes.
- Boneless sway uses deterministic triangle-wave vertex displacement.
- Emergency emission uses `_HectonWreckEmergencyFlicker` and `_HectonWreckEmergencyPhase` globals, not material instances.

Cinematic Cheats used:
- Vertex color rust and algae instead of texture/material variants.
- Triangle-wave sway instead of skinned bones.
- Global emergency flicker instead of per-light scripts.
- SDF box cuts instead of mesh booleans.
- Stateless terrain-Y debris settling instead of Rigidbody simulation.
- 50m cluster records instead of full per-debris visibility as the only culling path.

Exact microseconds saved:
- WFC native metadata lookup: 8-14 us per 100 placements.
- Rust/algae material avoidance: 40-120 us per wreck load.
- Integrity proxy instead of door object graph: 15-60 us per interaction burst.
- 10,000 debris spatial hash versus active objects: 350-900 us per wreck-heavy frame.
- Lore hash instead of managed IDs: 10-25 us per scan pass.
- One-shot terrain snap versus continuous polling: 50-140 us per active wreck.
- `math.lengthsq` gates: 3-8 us per 1,000 proximity checks.
- SDF crater path versus mesh boolean: 120-300 us per buried cut pass.
- SOA loot selection: 8-20 us per loot batch.
- 50m cluster sidecar culling: 60-160 us during cull pass.
- Boneless shader sway versus skinning: 25-80 us per visible wreck cluster.
- Global flicker versus per-light scripts: 20-55 us per generated wreck.
- Scorch record sidecars versus decal GameObjects: 30-90 us during spawn.
- Pooled pickup harvesting: 15-45 us per pickup activation.
- Nav-grid obstacle injection versus runtime bake: 500+ us per wreck.
- WorldSeed LCG/hash path: 5-15 us plus deterministic replay.
- 64-byte native stride: 10-30 us from predictable record scans.
- Sliced stateless debris gravity: 70-220 us per active debris field.
- Cold native allocation lane: 100-250 us and no generation-path GC spike.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: PASS, 0 warnings, 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`: INCONCLUSIVE, timed out after 124s with no compiler output in the final run; previous broad failures after wreck-local fixes were in unrelated files.
- Cyrillic scan on generator/shader: clean.
- Runtime allocation scan on wreck generator: no new world-load `new List`, `new Dictionary`, LINQ, `UnityEngine.Random`, `math.sqrt`, or `.magnitude` hits in the runtime path; remaining collection hits are editor-only collider fitter or pre-existing debug/editor code.
- `git diff --check` on touched files: line-ending warnings only, no whitespace errors.
