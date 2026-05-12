# Rationale_FAUNA_ECOSYSTEM

Status: PENDING VERIFICATION

## Session Start

Problem: The batch file requested by the user as `Docs/Tasks/CURRENT_BATCH.md` is not present; the live file is `Docs/Tasks/CURRENT_BATCH.txt`.
Solution: Extracted `<AGENT_PROMPT id="FAUNA_ECOSYSTEM" role="ECO_DIRECTOR">` from the `.txt` batch file using PowerShell line scanning.
Rejected Alternatives: Reading all prompts or relying on neighboring XML blocks was rejected because the batch protocol requires strict prompt isolation.
Scalability potential: No runtime impact.
Hardware Impact: No frame impact; documentation/control-plane only.

Problem: The ecosystem prompt targets deterministic macro-simulation with AUP sector tie-breakers, Burst jobs, SoA state, and zero-GC hot paths.
Solution: Loaded targeted mandates for swarm spatial hash, AI director, AUP, deterministic RNG/bit-mix, rsqrt/squared-distance, zero-GC, native memory/jobs, and crash telemetry.
Rejected Alternatives: Loading the full registry was rejected because mandate ingestion must be relevant and bounded.
Scalability potential: Low tier uses headless sector math and deterministic bit-mix; High/Ultra can spend saved CPU on denser fauna visuals.
Hardware Impact: Expected gain on i3/MX350 is reduced branchy migration and no heap churn; exact microseconds pending code inspection and compile verification.

## Loop 1: Tasks 1-5

Problem: Equal food-score migration previously risked deterministic directional bias and North-West biomass stacking.
Solution: Audited the implemented AUP tie-breaker in `HeadlessThresholdMigrationJob`: `((candidateCoord.x * 73856) + (candidateCoord.y * 19349)) & 3`.
Rejected Alternatives: Fixed neighbor order was rejected because it creates directional drift; `math.hash` was rejected because the prompt forbids complex hashing and opaque costs.
Scalability potential: Low uses four deterministic migration lanes; Middle/High/Ultra can keep the same authority while rendering denser fish movement around the chosen sectors.
Hardware Impact: Estimated 1-2 us saved per FrostTick versus generic hashing on i3/MX350; exact profiler data absent.

Problem: Ecology must be macro-simulated without GameObject truth.
Solution: Audited `LotkaVolterraPopulationJob : IJobParallelFor`, scheduled only when the 5s cold-tick accumulator reaches `coldTickIntervalSeconds`.
Rejected Alternatives: Per-fauna `Update()`/distance simulation and per-frame population churn were rejected as GC/cadence violations.
Scalability potential: Low keeps 5s headless solves; High/Ultra can spend saved CPU on richer visible fauna and longer LOD residency.
Hardware Impact: Estimated 80-140 us saved per 128 sectors versus managed per-sector loops; exact profiler data absent.

Problem: Micro-fauna panic cannot spend cycles on distance falloff when the gameplay truth is sector-level apex presence.
Solution: Audited `SectorPopulationState.ApexInSector` byte and `PublishApexPresenceFake()` publishing binary `_ApexInSector` and ocean panic globals.
Rejected Alternatives: Distance falloff panic math was rejected because the prompt demands packed byte flag behavior.
Scalability potential: Low reads one byte; High/Ultra can use the same byte to drive richer biolum/audio presentation.
Hardware Impact: Estimated 4-8 us saved per publish/query set on low-end silicon; exact profiler data absent.

Problem: Food capacity must come from geology/world-load data instead of live procedural loops.
Solution: Audited `BindSectorFoodDensityHeatmap()` and `ResolveSectorBaseFoodCapacity01()` using a power-of-two R8 `NativeArray<byte>` sampled as a 1D buffer.
Rejected Alternatives: Runtime noise/texture math loops were rejected as unnecessary work during FrostTick.
Scalability potential: Low samples one byte; High/Ultra can increase visual density without changing simulation authority.
Hardware Impact: Estimated 20-35 us saved per 128-sector solve; exact profiler data absent.

Problem: Sector IDs/randomness must be deterministic and cheap.
Solution: Audited `MixSectorBits()` using `((uint)sectorX * 73856093u) ^ ((uint)sectorZ * 19349663u)`.
Rejected Alternatives: `math.hash`, Unity RNG, and table-order RNG were rejected by prompt and deterministic RNG mandate.
Scalability potential: Same bit-mix applies across Low/Middle/High/Ultra with no replay divergence.
Hardware Impact: Estimated 3-6 us saved per 128-sector solve versus generic hash calls; exact profiler data absent.

## Loop 2: Tasks 6-10

Problem: Hibernated fauna cannot remain GameObjects without blowing CPU, memory, and tick cadence.
Solution: Audited `HeadlessEntitySoA` with `NativeArray<float3> Positions`, `NativeArray<byte> SpeciesID`, `NativeArray<byte> Hunger`, sector coords, and sector IDs.
Rejected Alternatives: Managed lists of brain references and hibernated GameObjects were rejected as non-SoA and non-headless.
Scalability potential: Low uses only SoA authority; Middle/High/Ultra can hydrate more visible fauna while retaining headless fallback.
Hardware Impact: Estimated 60-120 us saved per 128 hibernated records; exact profiler data absent.

Problem: Biomass migration should trigger from absolute sector pressure, not continuous steering.
Solution: Audited `HeadlessThresholdMigrationJob` with food-threshold and predator-tolerance gates before moving to the best neighbor.
Rejected Alternatives: Probabilistic wander and per-frame steering were rejected because headless biomass should move only on FrostTick authority.
Scalability potential: Low uses threshold moves; High/Ultra can visualize the chosen migration with richer fish swarms without changing macro state.
Hardware Impact: Estimated 15-30 us saved per FrostTick versus continuous steering; exact profiler data absent.

Problem: Spatial hash cleanup cannot scan all handles every frame.
Solution: Audited dense handle cleanup in `FaunaSpatialHashRegistry`: `DeferredCleanupHandlesPerFrame = (MaxEntryCapacity + 60 - 1) / 60`, which equals 18 for 1024 capacity, called from `LateFrameTick()`.
Rejected Alternatives: Full dictionary sweep and allocation-backed despawn queues were rejected as frame spikes.
Scalability potential: Low spreads cleanup over 60 frames; High/Ultra can increase registry capacity only with new slab math and profiling.
Hardware Impact: Estimated 0.05-0.12 ms cleanup spike avoided on i3/MX350; exact profiler data absent.

Problem: Apex spawn must reject wall/voxel intersections without a synchronous physics stall.
Solution: Audited cached async `CapsulecastCommand.ScheduleBatch`; spawn is denied until the pending result completes and is cached by quantized cell.
Rejected Alternatives: `Physics.CapsuleCast` and immediate spawn-then-fix logic were rejected as stall/regression risks.
Scalability potential: Low uses one cached command; High/Ultra can expand candidate counts outside the hot path.
Hardware Impact: Estimated 0.08-0.20 ms stall avoided on spawn check frames; exact profiler data absent.

Problem: Burst sector state and apex territory samples need stable cache-line stride.
Solution: Audited `[StructLayout(LayoutKind.Sequential, Pack = 4, Size = 64)]` on `SectorPopulationState` and `ApexTerritorySample`.
Rejected Alternatives: Unpinned/default struct layout and managed references inside jobs were rejected as Burst/cache hazards.
Scalability potential: Low gets predictable stride; High/Ultra can process larger sector counts with less cache waste.
Hardware Impact: Estimated 5-12 us locality gain per 128-sector solve; exact profiler data absent.

Problem: The post-loop compile gate is no longer green after unrelated atmosphere/celestial edits.
Solution: Recorded `dotnet build Assembly-CSharp.csproj` timeout and `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` failure against `HectonCelestialEngine.cs` missing celestial helper methods.
Rejected Alternatives: Editing `HectonCelestialEngine.cs` from the ecosystem batch was rejected as cross-domain sabotage without a critical interface justification.
Scalability potential: No runtime impact from the blocked compile gate; ecosystem work remains code-review-only until the external dependency is fixed.
Hardware Impact: No frame impact; build verification is blocked outside the fauna domain.

## Loop 3: Tasks 11-15

Problem: Leviathan death needed to persist as a gameplay ecology signal after live corpse actors unload.
Solution: Audited `RegisterApexPredatorKill()` saving death AUP through `PersistentWorldRegistry.TryCacheWhaleFallPoiState()` and `ResolveWhaleFallSpawnInfluence01()` expiring whale-fall POIs after 7200s, with 500m scavenger influence and 10x selection multiplier.
Rejected Alternatives: Live corpse-only GameObject references and full corpse actor scans were rejected because sector unload would erase ecology memory and add hot-path cost.
Scalability potential: Low keeps only a POI record; Middle/High/Ultra can spend the same signal on denser scavenger visuals, acoustic pulses, and corpse-biome dressing.
Hardware Impact: Estimated 10-25 us saved on i3/MX350 versus scanning live corpse actors during spawn queries; exact profiler data absent.

Problem: Prey bleed needs predator-readable scent without particle/cloud truth.
Solution: Audited `ChemicalInfluenceGrid.QueueBloodScent()` writing a 64x64 byte scent grid and using `math.lengthsq`/radiusSq for breadcrumb proximity and falloff tests.
Rejected Alternatives: `Vector3.Distance`, sqrt checks, and simulated scent particles were rejected by the rsqrt/squared-distance and cinematic cheat mandates.
Scalability potential: Low uses byte-grid chemistry; High/Ultra can render richer visible blood plumes while keeping the same scalar authority.
Hardware Impact: Estimated 8-18 us saved per 64 scent samples versus sqrt-heavy proximity checks; exact profiler data absent.

Problem: Player stress should reduce apex pressure instead of escalating into a fake difficulty spike.
Solution: Audited `TryResolveDirectorPlayerStress01()`, `_playerStress01`, spawn budget recovery scaling, and `ResolvePlayerStressSpawnWeight()` reducing apex weight above 80% stress.
Rejected Alternatives: Always-escalating predator spawn tables and per-archetype behavior tree stress checks were rejected as unpredictable and too expensive.
Scalability potential: Low uses scalar spawn gates; Middle/High/Ultra can preserve the same director behavior while adding richer distant predator presentation when stress is low.
Hardware Impact: Estimated 2-5 us saved per spawn selection versus additional director/behavior queries; exact profiler data absent.

Problem: Prey overpopulation needs sector-level consequence without simulating oxygen organisms.
Solution: Audited `LotkaVolterraPopulationJob` increasing `AlgaeBloom01`, depleting `Oxygen01`, and applying die-off when prey exceeds sector capacity.
Rejected Alternatives: Dedicated oxygen event GameObjects and per-prey oxygen consumers were rejected as unnecessary simulation truth.
Scalability potential: Low stores two floats per sector; High/Ultra can use bloom/oxygen values for stronger visual overkill in water tint, particles, and fauna behavior.
Hardware Impact: Estimated 5-10 us saved per 128-sector FrostTick versus event fanout; exact profiler data absent.

Problem: The LOD prompt requires Tier1 BRG instanced-mesh fauna between 50m and 150m, but ecosystem code only proves Tier0 GameObject and Tier2 SoA/hybrid hibernation authority.
Solution: Marked task 15 as dependency-blocked: `ResolveLogicalLodTier()` returns `FullSim`, `DataOnly`, and `Hibernating`, but no fauna-owned BRG visual handoff was found; existing BRG systems are vegetation, scavenger, HLOD, wreck, or world-owned.
Rejected Alternatives: Adding a new ecosystem-owned renderer inside `EcosystemDirector` was rejected because rendering ownership is outside this macro-simulation task and would couple domains under parallel-agent conditions.
Scalability potential: Low can still use SoA hibernation; Middle/High/Ultra require a separate fauna visual owner exposing a BRG contract before the 50-150m tier can become visual overkill.
Hardware Impact: Potential 0.10-0.30 ms gain after a proper Tier1 BRG contract exists; exact profiler data absent.

Problem: The post-loop compile gate exposed a fauna-side private-scope bug in `PredatorCognitionDomain.cs`: `useHighTierSmoothSteering` was computed in `Execute()` but referenced inside `EvaluatePredator()` without being forwarded.
Solution: Added the existing bool as a private `EvaluatePredator()` parameter and passed it into `ResolvePredatorDirection()`.
Rejected Alternatives: Removing high-tier S-curve steering was rejected because it would destroy the scalability/visual-overkill path; adding a static/global flag was rejected as shared-state drift.
Scalability potential: Low still uses dominant-axis steering; High/Ultra can use smooth apex S-curve steering through the existing packed flag.
Hardware Impact: No measurable cost; one bool argument only. Compile error removed; exact runtime profiler data absent.

## Loop 4: Tasks 16-20

Problem: Sector and spawn-gate quantization cannot use division in hot loops.
Solution: Audited `InvSectorEdgeLengthMeters` and `InvApexSpawnGateCacheCellSizeMeters`, with `QuantizeSector()` and `QuantizeApexSpawnGateCell()` multiplying by reciprocals before `math.floor`.
Rejected Alternatives: Repeated `/ SectorEdgeLengthMeters` and `/ ApexSpawnGateCacheCellSizeMeters` were rejected as slower scalar divides.
Scalability potential: Low benefits from cheap sector lookups; High/Ultra can increase visible fauna density without changing quantization authority.
Hardware Impact: Estimated 1-3 us saved per 1k quantizations on i3/MX350; exact profiler data absent.

Problem: Apex presence queries must use the packed sector byte only.
Solution: Audited `IsApexInSectorState(in state) => state.ApexInSector != 0` and the publish/query paths that consume it.
Rejected Alternatives: Distance-falloff fallback and predator scan fallback were rejected by the packed-byte objective.
Scalability potential: Low reads one byte; High/Ultra can use the byte to drive stronger shader/audio response without runtime ecology scans.
Hardware Impact: Estimated 3-7 us saved per query batch versus branchy fallback checks; exact profiler data absent.

Problem: Apex spawn wall checks cannot pay string layer lookup costs.
Solution: Audited `PassesApexSpawnVoxelGate()` using `HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask`; `LayerMask.NameToLayer` appears only in a commented `FaunaPOI` line, not a runtime lookup.
Rejected Alternatives: Runtime `LayerMask.NameToLayer` and authoring string layer paths were rejected as GC/native lookup hazards.
Scalability potential: Low avoids string work; High/Ultra can spend saved CPU on more spawn candidates.
Hardware Impact: Microsecond gain unmeasured; avoids lookup/GC risk on low-end silicon.

Problem: The fauna spatial registry cannot resize dictionaries under population pressure.
Solution: Audited `FaunaSpatialHashRegistry` static fixed-capacity `Dictionary<int, Entry>(MaxEntryCapacity)` and the 1024-entry handle slab with canonical cold-allocation comment.
Rejected Alternatives: Dynamic dictionary growth and uncapped cleanup handles were rejected as spike risks.
Scalability potential: Low uses bounded registry behavior; High/Ultra must raise capacity only with matching slab math and profiling.
Hardware Impact: Estimated 20-60 us resize spike avoided under capacity pressure; exact profiler data absent.

Problem: The omega compile check needed to verify whether `EcosystemDirector.cs` itself causes `Hecton8.Core` failures.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`; after fixing the E3 private flag-forward error, remaining failures are only in E2 `VoxelDeltaProcessor.cs`.
Rejected Alternatives: Editing voxel persistence/carving from ECO_DIRECTOR was rejected because Echelon 2 owns that domain and the current task only authorizes ecosystem/fauna macro-simulation.
Scalability potential: No runtime impact; compile verification remains blocked until the voxel owner/integrator fixes `VoxelDeltaProcessor.cs`.
Hardware Impact: No frame impact; build remains PENDING VERIFICATION because Unity/import/profiler evidence is absent.

## OMEGA POLISH CHANGES

Problem: The polish mandate requested "VERIFIED MASTER GRADE", but project authority and the agent prompt require PENDING VERIFICATION without Unity Console/profiler evidence.
Solution: Retained `Status: PENDING VERIFICATION`; recorded the conflict instead of fabricating verification.
Rejected Alternatives: Claiming verified status from static scans or `dotnet build` was rejected by AGENTS and prompt section V.
Scalability potential: No runtime impact; reporting integrity preserved.
Hardware Impact: No frame impact.

Problem: The anti-bloat audit needed to find honest math that should become cinematic cheats.
Solution: No new honest calculations were introduced by the authored code fix. Existing audited cheats remain: AUP tie bucket `((candidateCoord.x * 73856) + (candidateCoord.y * 19349)) & 3`, sector bit-mix, R8 food heatmap, 5s FrostTick Lotka-Volterra, byte apex flag, squared scent falloff, reciprocal quantization, SoA hibernation, and fixed-slab hash cleanup.
Rejected Alternatives: Replacing these with `math.hash`, per-frame ecology, `Vector3.Distance`, live corpse scans, or runtime layer strings was rejected.
Scalability potential: Low = byte grids, reciprocal math, SoA, dominant-axis fauna; Middle = more hydrated fauna; High/Ultra = spend saved CPU on richer visible fauna, scavenger dressing, bloom water response, and smooth apex steering. Tier1 fauna BRG remains blocked pending visual owner contract.
Hardware Impact: Aggregated static estimates remain 0.10-0.35 ms avoided on i3/MX350-class frames, but profiler proof is absent.

Problem: Hidden GC and slow math needed a final scan.
Solution: Ran static scans across `EcosystemDirector.cs`, `FaunaSpatialHashRegistry.cs`, `ChemicalInfluenceGrid.cs`, `PersistentWorldRegistry.cs`, and `PredatorCognitionDomain.cs`: no `math.sqrt`/`math.normalize`/`Vector3.Distance`, no `foreach`, no `$"..."`, no `string.Format`, and no `.ToString()` hits in those audited files. `new` hits are cold allocations or existing persistence paths, not new authored hot-path work.
Rejected Alternatives: Broad refactoring of persistence allocations was rejected because those paths are outside the ECO_DIRECTOR prompt and need separate save-system ownership.
Scalability potential: No new scalability burden found.
Hardware Impact: No added CPU/GC cost from the authored fix.

Problem: Final build health remains red.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`. The E3 `PredatorCognitionDomain` compile error was removed. Remaining errors are only in E2 `VoxelDeltaProcessor.cs`: mismatched compacted chunk constructor arguments, missing `VoxelDeltaUniformRunDetectJob.UniformFlag`, `NativeArray<byte>` passed where `NativeArray<int>` is expected, and missing `DebrisSpawnSignal`.
Rejected Alternatives: Editing voxel topology/persistence from ECO_DIRECTOR was rejected as domain leakage; this belongs to the Voxel SDF Pipeline or Integrator.
Scalability potential: No runtime impact from this agent until E2 compile blockers are fixed.
Hardware Impact: No frame impact; compile blocked.

Problem: Final diff needed to be recorded.
Solution: Observed tracked diff currently marks `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs` modified with pre-existing other-agent changes plus my authored private flag-forward fix; untracked files are `Docs/Tasks/Status_FAUNA_ECOSYSTEM.md`, `Docs/AgentLogs/Rationale_FAUNA_ECOSYSTEM.md`, and `Docs/AgentLogs/LOG_FAUNA_ECOSYSTEM.md`.
Rejected Alternatives: Reverting unrelated PredatorCognition changes was rejected because the worktree is shared by many agents.
Scalability potential: Authored code change preserves high-tier apex smooth steering and low-tier dominant-axis fallback.
Hardware Impact: One bool argument only; no measured runtime cost.

## Loop 6: Tier1 Proxy Handoff

Problem: Task 15 still had a real domain gap: Tier1 `DataOnly` suppressed GameObject presentation, but no fauna-owned handoff existed for a future BRG/instanced-mesh visual owner.
Solution: Added `FaunaTier1LodProxyRegistry` with fixed 512-slot cold arrays and `[StructLayout(... Size = 64)]` `FaunaTier1LodProxyEntry` containing AUP, UID, species, packed flags, heading octant, health, hunger, and quality tier. `FaunaBrain` now registers/updates this proxy on `DataOnly` transitions and slow Tier1 ticks, and unregisters on FullSim/Hibernating/despawn/disable/destroy.
Rejected Alternatives: Adding BRG draw code in `EcosystemDirector` or `FaunaBrain` was rejected as renderer ownership leakage. Using `Dictionary<uint,Entry>`, `List<T>`, or per-frame GameObject impostors was rejected because Tier1 handoff must stay fixed-capacity and zero-GC.
Scalability potential: Low uses only packed 64-byte proxy truth and suppressed GameObject actors; Middle can consume the slab with coarse instancing; High/Ultra can spend saved CPU/GPU budget on richer BRG animation, species material variants, and overkill distant schools without changing sim authority.
Hardware Impact: Estimated 20-45 us saved per 128 Tier1 fauna on i3/MX350 versus hydrated hidden GameObjects once a visual owner consumes the slab; current registry write is O(1), active-copy scan is bounded at 512 slots, and profiler proof is absent.

Problem: Tier1 heading could have used quaternion/atan/vector normalization.
Solution: Packed `HeadingOctant` through sign and X/Z dominance checks only; no sqrt, no normalize, no angle math.
Rejected Alternatives: `Vector3.Angle`, `Mathf.Atan2`, normalized forward vectors, and physically honest schooling orientation were rejected as unnecessary for distant instanced presentation.
Scalability potential: Low gets 4-8 fake heading buckets; High/Ultra can decode the same octant into shader sway/turn animation variants.
Hardware Impact: Estimated 2-5 us saved per 128 Tier1 fauna versus trig/normalization; exact profiler data absent.

Problem: The new registry file had to participate in local `dotnet build` verification.
Solution: Added `Assets/_Project/Scripts/Fauna/FaunaTier1LodProxyRegistry.cs` to the local generated `Hecton8.Core.csproj` include after `--no-dependencies` reported `FaunaTier1LodProxyEntry` missing.
Rejected Alternatives: Inlining the registry into `FaunaBrain.cs` was rejected because it would bury the handoff contract inside a 6k-line actor controller.
Scalability potential: No runtime effect; build graph can see the fixed proxy contract for future visual consumers.
Hardware Impact: No frame impact.

Problem: Compile verification remains red after the Tier1 handoff.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`. No fauna/ecosystem errors were reported; current failures are outside ECO_DIRECTOR ownership in `SaveBinaryPayloadCodec.cs`, `SaveBinaryStorage.cs`, `HabitatGraphManager.cs`, and `ConstructionManager.cs`.
Rejected Alternatives: Editing save/construction systems from ECO_DIRECTOR was rejected as cross-domain work without a critical fauna interface reason.
Scalability potential: No runtime impact until external compile blockers are fixed.
Hardware Impact: No frame impact; STATUS remains PENDING VERIFICATION.
