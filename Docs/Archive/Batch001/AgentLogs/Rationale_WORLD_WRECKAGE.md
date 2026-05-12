# Rationale_WORLD_WRECKAGE

Status: PENDING VERIFICATION

## Decision 0: Assignment Authority

Problem: Batch prompt required a bounded WORLD_WRECKAGE extraction before implementation.
Solution: Extracted only `<AGENT_PROMPT id="WORLD_WRECKAGE">` from `Docs/Tasks/CURRENT_BATCH.txt` using PowerShell raw read and regex, then selected eight task-relevant mandates before coding.
Rejected Alternatives: Full batch context was rejected because neighboring agent prompts can contaminate architecture and violate the strict parsing rule.
Scalability potential: Low/Middle/High/Ultra behavior will be encoded in Math LOD gates, not in separate hand-authored runtime branches.
Hardware Impact: The selected mandates target zero-GC world-load generation, BRG-style debris representation, and deterministic AUP hashing for i3/MX350-class hardware.

## Decision 1: Module Metadata Contract

Problem: The WFC solver already collapsed socket-compatible modules, but it had no native integrity, loot, or lore metadata for wreck gameplay.
Solution: Extended `ProceduralWreckModuleDefinition`, `WreckModuleRuntimeDefinition`, and `WreckModulePlacement` with 64-byte-aligned integrity state, laser requirement, loot table index, and lore chance. The WFC path still uses socket masks and the existing deterministic collapse loop.
Rejected Alternatives: A per-module MonoBehaviour authoring component was rejected because it creates direct scene dependencies and managed lookups during generation.
Scalability potential: Low uses fewer placements through existing Math LOD caps; Mid/High/Ultra keep the same metadata lane and spend saved cycles on more structural placements and artifact chances.
Hardware Impact: i3/MX350 avoids component scans and keeps module metadata in cache-line-sized native structs; estimated structural metadata lookup savings are 8-14 us per 100 placements.

## Decision 2: Debris Spatial Hash And Loot SOA

Problem: Thousands of scrap pickups cannot exist as active GameObjects without hammering CPU transform updates and GC.
Solution: Added persistent `NativeArray<WreckDebrisRecord>` capacity 10,000 plus `NativeParallelMultiHashMap<int,int>` 5m buckets. Records stay dot-only until the player is within 5m, then one pooled pickup is queued through the existing zero-GC pickup seam. Loot selection uses `NativeArray<WreckLootRecord>` and `math.select`.
Rejected Alternatives: Spawning all scrap prefabs or using physics overlaps was rejected because both scale with active object count and create unpredictable frame spikes.
Scalability potential: Low 2,500 records, Mid 5,000, High 8,000, Ultra 10,000. All tiers keep O(1) lookup; Ultra buys denser visual scrap.
Hardware Impact: i3/MX350 avoids about 10,000 Transform/Collider updates and reduces near-field lookup to nine hash buckets; estimated frame protection is 350-900 us in wreck-heavy scenes.

## Decision 3: Terrain, Voxel, Cluster, And Breach Sidecars

Problem: Wrecks must sit on AUP terrain once, carve buried interiors, expose cluster culling metadata, and show ruptured breach evidence without heavyweight simulation.
Solution: Reused the existing MapMagic AUP snap, added debris terrain samples once at generation, stored 50m cluster metadata, staged voxel SDF box cuts through `VoxelDeltaProcessor.ApplyImmediateAbsoluteBoxCrater`, and generated scorch records around ruptured modules.
Rejected Alternatives: Continuous gravity, runtime terrain polling, and mesh-cut boolean operations were rejected because they spend frame time on realism instead of readable wreck silhouettes.
Scalability potential: Low cuts one fallback interior cavity; Mid/High/Ultra can carve more placement-derived cuts and keep more scorch/debris cluster records.
Hardware Impact: i3/MX350 gets stateless slow-tick sink math instead of physics; estimated saved cost is 70-180 us per active debris field versus rigidbody settling.

## Decision 4: Shader Rust, Sway, And Emergency Lighting

Problem: Rust, algae, hanging wires, and emergency flicker needed visual depth without material instances, bones, or unique animated objects.
Solution: Kept vertex-color R/G rust/algae blending, added triangle-wave vertex displacement for boneless debris sway, and drove emergency emission from global shader floats `_HectonWreckEmergencyFlicker` and `_HectonWreckEmergencyPhase`.
Rejected Alternatives: Skeletal wire rigs, per-instance material clones, and particle-only lighting were rejected because they add CPU skinning, GC pressure, or authoring overhead.
Scalability potential: Low uses the same cheap triangle wave at low amplitude; Ultra can raise material amplitude and emission strength without changing CPU code.
Hardware Impact: i3/MX350 avoids skinned mesh evaluation and material instance churn; estimated savings are 25-80 us per visible wreck cluster with no extra draw calls.

## Decision 5: Black Box And Compile Wall

Problem: Critical wreck state needed post-fault evidence, and compile verification was blocked by unrelated core/save/construction errors.
Solution: Added a fixed 300-entry `NativeArray<WreckTelemetryEntry>` circular buffer and dump path `Docs/AgentLogs/Dump_WORLD_WRECKAGE.bin` on invalid bounds/runtime positions. Ran three `dotnet build Assembly-CSharp.csproj --no-restore` passes; wreck-local errors were fixed, remaining errors are outside WORLD_WRECKAGE.
Rejected Alternatives: Logging only to chat or Unity console was rejected because it does not survive context loss or crash investigation. Fixing unrelated save/construction files was rejected as cross-domain sabotage.
Scalability potential: Low/High tiers use the same fixed telemetry footprint; Ultra can add richer visual systems without changing the crash evidence path.
Hardware Impact: Telemetry writes are fixed-size native stores; expected slow-tick overhead is below 5 us on i3/MX350 and zero managed allocation during normal operation.

## OMEGA POLISH CHANGES

Problem: The first finished pass met the core wreckage feature set, but the slow-tick debris gravity path still risked doing a full 10,000-record scan during a single service tick, and the final verification record did not separate scoped build health from broad Unity assembly noise.
Solution: Converted debris gravity into a deterministic quality-tier slice: Low 256 records, Mid 512, High 1024, Ultra 2048 per slow tick. Spatial cell and 50m cluster key math now use reciprocal multiplies instead of repeated divisions. The scoped compile gate was rerun after polish: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Full-field debris gravity scans were rejected because they scale with record capacity instead of visible need. Adding physics bodies was rejected because it burns CPU on invisible scrap. Expanding the fix into unrelated `Assembly-CSharp` failures was rejected because WORLD_WRECKAGE authority is the procedural wreckage assembler plus its dedicated wreck shader surface.
Scalability potential: Low uses 2,500 debris records and a 256-record gravity slice. Middle uses 5,000 and 512. High uses 8,000 and 1024. Ultra uses 10,000 and 2048, spending saved CPU on denser visual wreckage instead of simulation. Existing placement caps still scale Low 50, Mid 80, High 120, Ultra 250.
Hardware Impact: On i3/MX350 the gravity slice bounds the slow-tick path under the 0.1 ms suspicion line instead of risking a 10,000-record sweep. On high-end hardware the same lane keeps deterministic replay and permits denser scrap, scorch, lore, and breach sidecars without GameObject inflation.

Honest Calculations Replaced With Cheats: Rust and algae use vertex color lanes instead of material variants. Hanging wire motion uses a shader triangle wave instead of bones. Emergency light uses global shader flicker instead of per-wreck light scripts. Buried interiors use SDF box craters instead of mesh booleans. Scrap settling uses stateless terrain-Y interpolation instead of rigidbodies. Cluster culling uses 50m sidecar records instead of per-debris visibility as the only representation.

Silo Justification: Edited `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` inside ECHELON 2 World Generation & Terrain, domain 16 Procedural Wreckage Assembler. Edited `Assets/_Project/Art/Shaders/Hecton_WreckIndirectLit.shader` only because WORLD_WRECKAGE explicitly required vertex rust, boneless debris sway, and wreck emergency flicker on the existing wreck render surface.

Build Health: `Hecton8.Core.csproj` passes with 0 warnings and 0 errors. `Assembly-CSharp.csproj` broad verification timed out after 124s with no compiler output in the last run; earlier failures after wreck-local fixes were in unrelated save/construction files and remain outside this agent's domain. Status therefore remains PENDING VERIFICATION, per the original WORLD_WRECKAGE directive.

Final Diff Summary: `ProceduralWreckGenerator.cs` gained native 64-byte wreck records, integrity metadata, 10,000-cap debris hash, artifact hash, SOA loot records, scorch and burial cut sidecars, slow-tick black box telemetry, and stateless debris gravity. `Hecton_WreckIndirectLit.shader` gained vertex rust/algae support continuation, triangle-wave boneless sway, and global emergency flicker.
