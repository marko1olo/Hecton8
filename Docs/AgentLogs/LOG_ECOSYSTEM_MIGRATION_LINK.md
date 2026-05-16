# LOG_ECOSYSTEM_MIGRATION_LINK

## 2026-05-16 - Macro Swarm DB To Active Simulation Bridge

What was wrong:
- OSHINO/H8 macro swarms were abstract database payloads and macro travel records only. Loaded sectors had no bridge that claimed active fish slots from the ambient SOA.
- Legacy-style scene spawning was forbidden. `BufferID.EntityAUPs` was also not a safe target because the current project owns that lane for loot/entity AUP data, not active ecology boids.
- Chunk unload risked losing active visual biomass because no path packed hydrated fish back into a macro swarm.
- Capacity overflow had multiple possible entry points: DB import cap, hydration scratch cap, dehydration scratch cap, active macro cap, and active biota slot cap.

What was done:
- Extended `IEcosystemDirectorService` with vault import, hydration claim, and dehydration repack contracts.
- Extended `IAmbientBiotaService` with active macro hydration and macro-hydrated biota packing contracts.
- Added fixed 64-byte `EntitySpawnSignal` and registered/published it through `GlobalSignals`.
- Imported `MacroSwarm` records from `GlobalDataVault` macro database payload handles using fixed-stride native reads, then sanitized biomass, speed, AUP, hash, and genome fields.
- Routed sector hydration into `AmbientBiotaMacroHydrationJob`, which scans fixed SOA state lanes and claims only inactive slots.
- Converted macro sector authority into runtime AUP offsets through deterministic hash offsets, with non-finite position rejection before any SOA write.
- Added low-tier border ring hydration and high-tier SDF-gated cave emergence flags.
- Added stress culling: `SystemStress01 > 0.7` hydrates 50 percent of visual biomass while abstract macro biomass remains authoritative.
- Added unload seam: `SectorDehydratedSignal` packs macro-hydrated active ambient biota back into one `MacroSwarm` before legacy biomass fallback.
- Added capacity overflow blackbox pushes and changed macro-swarm blackbox dump target to `Docs/AgentLogs/Dump_ECOSYSTEM_MIGRATION_LINK.bin`.

Cinematic cheats used:
- Low tier: instant border-ring fish spawn with billboard flag. No cave math, no per-fish SDF, no prefab path.
- Middle tier: deterministic radius fill from macro swarm biomass into ambient SOA slots.
- High/Ultra: one published SDF sample at hydration center gates cave emergence. The fish still use deterministic inward/deep offsets and `FlagSdfEmergence`; downstream VFX can sell the cave swim-out without ecology sampling every fish.
- Stress adaptation: visual fish count halves under high system stress; macro biomass remains abstract and recoverable.

Exact microseconds saved / estimated:
- Prefab path rejected: structural savings versus Instantiate/Destroy; expected multi-ms spike avoided for 64 fish, not measured in Unity profiler.
- Vault native import: estimated 18 us for 64 fixed-stride records.
- Hydration job: estimated 42 us for 64 visual boids at normal stress.
- Inactive-slot SOA claim: estimated 31 us for 64 claims at 2048 capacity.
- Low-tier border offsets: estimated 7 us for 64 offsets.
- High-tier SDF gate: one center sample, estimated 3 us, replacing 64+ per-fish SDF samples.
- Stress cull at >0.7: estimated 21 us saved and 32 visual slots avoided for a 64-fish swarm.
- Blackbox push: estimated 4 us excluding rare file dump.

Validation:
- `dotnet build Hecton8.Core.csproj -v:minimal /p:UseSharedCompilation=false` executed.
- Final result: blocked by unrelated compile wall. Current errors are in XR refresh rate, item acquisition signal, submarine structural grid, bioluminescence VFX, vault probe diagnostics, and visor fluid distortion files.
- No compiler errors were reported in the edited ecology/global-signal files before the external wall stopped validation.
