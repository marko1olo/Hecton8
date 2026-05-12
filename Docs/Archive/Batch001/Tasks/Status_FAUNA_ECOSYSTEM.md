# Status_FAUNA_ECOSYSTEM

Prompt: `FAUNA_ECOSYSTEM`
Role: `ECO_DIRECTOR`
Domain: ECHELON 3 / Flora, Fauna & Biota / Ecosystem Director Macro
Batch Source: `Docs/Tasks/CURRENT_BATCH.md`
Status Rule: PENDING VERIFICATION until Unity console/profiler evidence exists.

## Mandates Loaded

- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `AI_Director_Encounter_Manager.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `MATH_Rsqrt_i3_SIMD.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `REND_GPU_Sovereignty.txt`
- `REND_Foveated_Simulation_LOD.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Assignment Tasks

- [x] 1. AUP TIE-BREAKER | DOD: `HeadlessThresholdMigrationJob.ResolveAupMigrationTieBucket()` uses `((candidateCoord.x * 73856) + (candidateCoord.y * 19349)) & 3` when food scores tie | Rejected: fixed N/W/S/E priority that corner-stacks biomass; `math.hash` too expensive/opaque | Estimate: 1-2 us saved per FrostTick vs generic hash, PENDING VERIFICATION
- [x] 2. BURST LOTKA-VOLTERRA SOLVER | DOD: `LotkaVolterraPopulationJob : IJobParallelFor` solves prey/predator `dx/dt` on accumulated 5s `coldTickIntervalSeconds` | Rejected: MonoBehaviour per-fauna Update and per-frame ecology loops | Estimate: 80-140 us saved per 128 sectors on i3/MX350 vs managed per-sector loop, PENDING VERIFICATION
- [x] 3. APEX PRESENCE FLAG | DOD: `SectorPopulationState.ApexInSector` byte drives `PublishApexPresenceFake()` and `_ApexInSector`/panic shader globals | Rejected: distance-falloff panic math for micro-fauna | Estimate: 4-8 us saved per publish/query set, PENDING VERIFICATION
- [x] 4. DENSITY HEATMAP (1D TEX) | DOD: `BindSectorFoodDensityHeatmap()` accepts R8 `NativeArray<byte>` and `ResolveSectorBaseFoodCapacity01()` samples it by power-of-two mask | Rejected: runtime procedural food loops per sector | Estimate: 20-35 us saved per 128-sector solve, PENDING VERIFICATION
- [x] 5. DETERMINISTIC BIT-MIX | DOD: `MixSectorBits()` uses `((uint)sectorX * 73856093u) ^ ((uint)sectorZ * 19349663u)` for sector IDs/randomness | Rejected: `math.hash`, `Random.Range`, table-order RNG | Estimate: 3-6 us saved per 128-sector solve, PENDING VERIFICATION
- [x] 6. S.O.A. HEADLESS ENTITY DATA | DOD: `HeadlessEntitySoA` owns `NativeArray<float3> Positions`, `NativeArray<byte> SpeciesID`, `NativeArray<byte> Hunger`, plus sector coord/id arrays | Rejected: object-reference fauna truth for hibernated sectors | Estimate: 60-120 us saved per 128 hibernated records, PENDING VERIFICATION
- [x] 7. ABSOLUTE THRESHOLD MIGRATION | DOD: `HeadlessThresholdMigrationJob` moves only when `FoodDensity01 < MigrationFoodThreshold01` or `PredatorPopulationRounded > MigrationPredatorTolerance` | Rejected: probabilistic wander and per-frame steering for headless biomass | Estimate: 15-30 us saved per FrostTick vs continuous steering, PENDING VERIFICATION
- [x] 8. SPATIAL HASH GARBAGE COLLECTION | DOD: `FaunaSpatialHashRegistry` dense handle slab uses `(1024 + 60 - 1) / 60 = 18` cleanup handles per frame and runs from `LateFrameTick()` | Rejected: full-registry cleanup spikes and per-despawn allocation queues | Estimate: avoids 0.05-0.12 ms cleanup spike on MX350-class CPU, PENDING VERIFICATION
- [x] 9. ASYNC APEX SPAWN WALL CHECK | DOD: `PassesApexSpawnVoxelGate()` schedules cached `CapsulecastCommand.ScheduleBatch`; uncached/scheduled states deny spawn until result is completed | Rejected: synchronous `Physics.CapsuleCast` in spawn path | Estimate: 0.08-0.20 ms stall avoided on spawn check frames, PENDING VERIFICATION
- [x] 10. SECTOR STRUCT ALIGNMENT | DOD: `[StructLayout(... Size = 64)]` pins `SectorPopulationState` and `ApexTerritorySample`; `AbsoluteUniversePositionBlit128` is explicit 48B payload inside 64B sample | Rejected: variable struct stride and managed reference payloads in Burst jobs | Estimate: 5-12 us cache-line locality gain per 128-sector solve, PENDING VERIFICATION
- [x] 11. WHALE-FALL PERSISTENCE | DOD: `RegisterApexPredatorKill()` saves Leviathan death AUP via `TryCacheWhaleFallPoiState()`, persistent registry expires at 7200s, and scavenger selection uses 10x weight inside 500m influence | Rejected: live corpse-only spawn memory that disappears on sector unload | Estimate: preserves encounter belief with 0 us per-frame until scavenger query; 10-25 us saved vs scanning live corpse actors, PENDING VERIFICATION
- [x] 12. SQUARED-FALLOFF SCENT GRID | DOD: `ChemicalInfluenceGrid.QueueBloodScent()` writes low-res scent cells and proximity checks use `math.lengthsq`/radiusSq before falloff | Rejected: `Vector3.Distance`, sqrt radius checks, and particle-truth scent clouds | Estimate: 8-18 us saved per 64 scent samples vs sqrt checks, PENDING VERIFICATION
- [x] 13. STRESS-MODULATED SPAWN BUDGET | DOD: `TryResolveDirectorPlayerStress01()` reads director/player stress, budget recovery shrinks with stress, and `ResolvePlayerStressSpawnWeight()` reduces apex weight above 0.8 stress | Rejected: panic-escalating apex spawns during high player stress | Estimate: 2-5 us saved per spawn selection by scalar gate instead of director subqueries, PENDING VERIFICATION
- [x] 14. BIOME DOMINANCE SHIFT | DOD: `LotkaVolterraPopulationJob` raises `AlgaeBloom01` and depletes `Oxygen01` when prey exceeds `PreyCapacity`, then applies oxygen die-off | Rejected: separate oxygen event objects or per-prey simulation | Estimate: 5-10 us saved per 128-sector FrostTick vs event fanout, PENDING VERIFICATION
- [x] 15. LOD TIERING CONTROLLER [PARTIAL DOMAIN HANDOFF / VISUAL OWNER BLOCKED] | DOD: `ResolveLogicalLodTier()` still resolves 0-50m GameObject, 50-150m `DataOnly`, and >150m SoA hibernation; `FaunaTier1LodProxyRegistry` now exposes fixed 512-slot, 64-byte Tier1 proxy entries and `FaunaBrain` registers/updates/unregisters them on `DataOnly` lifecycle | Rejected: adding ecosystem-owned BRG rendering or stealing vegetation/world renderer ownership | Estimate: 20-45 us avoided per 128 Tier1 fauna versus hydrated GameObjects after a visual owner consumes the proxy slab; actual BRG draw remains BLOCKED BY DEPENDENCY, PENDING VERIFICATION
- [x] 16. RECIPROCAL DIVISIONS | DOD: sector and apex-spawn quantization use `InvSectorEdgeLengthMeters` / `InvApexSpawnGateCacheCellSizeMeters` reciprocal multiplies before `math.floor` | Rejected: repeated `/ SectorEdgeLengthMeters` divisions in hot quantization | Estimate: 1-3 us saved per 1k quantizations, PENDING VERIFICATION
- [x] 17. PACKED BYTE FLAG | DOD: `IsApexInSectorState()` reads only `state.ApexInSector != 0`; publish/query paths use the packed byte without distance fallback branches | Rejected: predator-distance fallback and sector scan panic paths | Estimate: 3-7 us saved per apex presence query batch, PENDING VERIFICATION
- [x] 18. LAYER INDICES | DOD: apex spawn wall gate uses `HectonLayerMasks.TerrainLayerMask | HectonLayerMasks.VoxelCaveLayerMask`; no runtime `LayerMask.NameToLayer` calls found in ecosystem/fauna hot paths | Rejected: string layer lookup in spawn query | Estimate: avoids string/native lookup cost and GC risk; microsecond gain unmeasured, PENDING VERIFICATION
- [x] 19. STATIC COLD INIT | DOD: `FaunaSpatialHashRegistry` uses `private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>(MaxEntryCapacity)` with canonical `COLD ALLOC` and fixed 1024 capacity | Rejected: runtime growth dictionary and unbounded handle table | Estimate: avoids resize spike and heap churn; 20-60 us avoided on capacity pressure, PENDING VERIFICATION
- [x] 20. OMEGA COMPILE CHECK | DOD: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` shows no `EcosystemDirector.cs` errors; E3 `PredatorCognitionDomain` missing flag forwarding was fixed; remaining compile errors are E2 `VoxelDeltaProcessor.cs` only | Rejected: cross-domain voxel patch from ECO_DIRECTOR | Estimate: no runtime cost; compile state BLOCKED BY DEPENDENCY, PENDING VERIFICATION

## Compile Gates

- After Tasks 1-5: `dotnet build Hecton8.Core.csproj` PASS, 0 errors; `dotnet build Assembly-CSharp.csproj` PASS, 0 errors, third-party/editor deprecation warnings only.
- After Tasks 6-10: `dotnet build Assembly-CSharp.csproj` timed out at 306s; `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` FAILS in `Assets/_Project/Scripts/Atmosphere/HectonCelestialEngine.cs` missing celestial helper methods, not `EcosystemDirector.cs`. BLOCKED BY DEPENDENCY.
- After Tasks 11-15: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` initially failed in E2 `VoxelDeltaProcessor.cs` and E3 `PredatorCognitionDomain.cs`; fauna flag-forward compile error was fixed.
- After Tasks 16-20: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` FAILS only in E2 `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`; no `EcosystemDirector.cs` or `PredatorCognitionDomain.cs` errors. BLOCKED BY DEPENDENCY.
- Continuation Tier1 handoff: initial `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal` caught missing generated project include for `FaunaTier1LodProxyRegistry.cs`; include added to local `Hecton8.Core.csproj`, then no fauna errors remained. Full `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` FAILS in `SaveBinaryPayloadCodec.cs`, `SaveBinaryStorage.cs`, `HabitatGraphManager.cs`, and `ConstructionManager.cs`; no `FaunaBrain.cs`, `FaunaTier1LodProxyRegistry.cs`, `EcosystemDirector.cs`, or `PredatorCognitionDomain.cs` errors. BLOCKED BY DEPENDENCY.

## Loop State

- Loop 0: Prompt extracted. Mandates loaded. Existing code discovery in progress.
- Loop 1: Tasks 1-5 audited in `Assets/_Project/Scripts/World/EcosystemDirector.cs`; compile gates green; Unity Play Mode/profiler evidence absent, therefore PENDING VERIFICATION.
- Loop 2: Tasks 6-10 audited in `EcosystemDirector.cs` and `FaunaSpatialHashRegistry.cs`; compile rerun blocked by non-ecosystem `HectonCelestialEngine.cs`.
- Loop 3: Tasks 11-15 audited in `EcosystemDirector.cs`, `PersistentWorldRegistry.cs`, and `ChemicalInfluenceGrid.cs`; task 15 remains dependency-blocked on a fauna Tier1 BRG visual owner.
- Loop 4: Tasks 16-20 audited in `EcosystemDirector.cs`, `FaunaSpatialHashRegistry.cs`, and compile output; E3 `PredatorCognitionDomain` compile error fixed; global compile remains blocked by E2 voxel dependency.
- Loop 5: OMEGA polish audit complete. Static scans found no `math.sqrt`/`math.normalize`/`Vector3.Distance`, no `foreach`, and no string formatting/interpolation/`.ToString()` hits in audited ecosystem/fauna hot files. Final report appended to `Docs/AgentLogs/LOG_FAUNA_ECOSYSTEM.md`. STATUS remains PENDING VERIFICATION.
- Loop 6: Task 15 revisited after user requested continued domain work. Implemented fauna-owned Tier1 proxy handoff via `FaunaTier1LodProxyRegistry` and `FaunaBrain` lifecycle hooks; actual BRG instanced mesh rendering remains outside ECO_DIRECTOR ownership. STATUS remains PENDING VERIFICATION.
