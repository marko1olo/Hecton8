# DRONE FLEET PROTOCOL
Date: 2026-05-07

Status: PENDING VERIFICATION

## 2026-05-19 SHINOBU_128 Runtime Boundary

- Operational cap is 500 drones; native storage is 512 slots to keep 64-wide job batches and GPU buffers aligned.
- `DroneStateDTO` is explicit 64 B ABI (`double3 AUP_Position`, `float3 Velocity`, task hashes, battery, flags) with a layout sentinel in source.
- State/matrix native buffers are allocated with `NativeArrayOptions.UninitializedMemory` and cold-cleared through slot reset before runtime use.
- Task-map rebuild cadence is continuous: `framesBetweenUpdates = (int)math.lerp(5, 60, 1 - GlobalQualityWeight)`.
- Steering cadence, macro route solve budget, docking probe count, phantom draw count, and render distance now consume `HomeostasisBrain.GlobalQualityWeight` instead of hard quality-tier switches.
- Required black-box dump target is `Docs/AgentLogs/Dump_FLEET_COMMANDER.bin`; legacy `Dump_DRONE_FLEET.bin` and `.h8dump` are still emitted for older readers.
- CSV tuner default is `drone_chassis_specs.csv`; `drone_specs.csv` remains a fallback path only.
- Real drone rendering still submits through `Graphics.RenderMeshIndirect` because the current material/shader contract for procedural vertex generation is unproven. Treat exact `DrawProceduralIndirect` compliance as pending shader proof, not as completed runtime fact.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical data boundary snapshot: `Docs/Reports/2026-05-11_DOCUMENTATION_CURRENT_DATA_CONTINUATION.md`.
- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.
- Current actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`.
- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Visual-realistic-fake doctrine snapshot: `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`; re-check `.agents-skills` for newer mandates before implementation.
- Historical May 14/R43 CLI compile wording is stale report text, not current proof. Current R31 static/tool boundary: R31 is the latest DOC_GLOBAL root/architecture current-boundary propagation layer; R30 remains the prior internal-currentness layer; AtlasCheck fails `57` RealtimeCSG refs; Mod API static validation now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity import, Console, Play Mode, profiler, GCMonitor, player build, scene wiring, save/load, and visual proof remain PENDING VERIFICATION.
- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.
- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.
## Historical 2026-05-04 Boundary

- Read `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md`, `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`, `Docs/Reports/2026-05-04_WARNING_CLEANUP.md`, `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`, and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this protocol as current runtime truth.
- This document is a fleet architecture contract, not proof that active scenes, native buffers, render submission, or hub requests are runtime-validated.
- Re-open `DroneFleetManager`, `RepairDroneHub`, logistics owners, and profiler/console evidence before surgery.

## Scope

Runtime owners:
- `DroneFleetManager.cs`: native drone state pool, Burst cognition scheduling, task arbitration, fleet snapshot publisher, OS overclock latch, suicide-weld latch, Logic-Leech hijack latch, indirect rendering submission.
- `DroneCognitionJob.cs`: Burst-compatible movement, battery drain, task scoring, atomic task claims, emergency scalar application, and boid separation.
- `RepairDroneHub.cs`: powered dock, logistics intake, integer drone-slot lease owner. It no longer spawns per-drone GameObjects for sorties.
- `RepairDroneEntity.cs`: retired source-name marker plus shared torch-audio event structs. It is not a `MonoBehaviour` and cannot be spawned as a drone body.
- `BaseLogisticsNetwork.cs`: two-phase storage reservation and nearest supply endpoint resolver.
- `ThreadSafeCommandQueue.cs`: main-thread structural command drain for `CommitStorageReservation`.
- `FloraInteractionManager.cs`: parasite target resolution and plasma-cut bridge into `DestructibleOrganicManager`.
- `HectonSubmarineOS.cs`: publishes emergency level snapshots consumed by the fleet.

Mandates followed:
- `AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt`
- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `PHYS_Destructible_Organic_Entropy.txt`

## Headless Runtime

Drone sorties are represented by native slots. No per-drone GameObject or `MonoBehaviour` exists in the runtime sortie path:
- `NativeArray<HeadlessDroneState>[512]` front buffer
- `NativeArray<HeadlessDroneState>[512]` back buffer
- `NativeArray<float4x4>[512]` render matrix buffer
- `NativeParallelMultiHashMap<int, HeadlessDroneTask>[512]` hub-keyed task fanout
- `NativeParallelMultiHashMap<int,int>[512]` spatial hash for 2 m boid separation
- `NativeArray<int>[512]` task claim owners

Scheduling model:
1. `RepairDroneHub.SlowTick()` queues launch/abort/release requests into managed fixed-capacity arrays.
2. `DroneFleetManager.HeadlessFleetDriver.Tick()` schedules the chain: `ClearDroneMacroWaypointsJob -> DroneTaskAssignmentJob -> DroneCognitionJob -> DroneMetabolismJob -> ExtractDroneMatricesJob -> BuildDroneProceduralArgsJob`.
3. `LateFrameTick()` completes the job in the dispatcher swap window, swaps front/back buffers, applies managed-side repair/storage/organic/voxel commits, then applies queued hub requests.
4. SRP render callback uploads `NativeArray<float4x4>` and calls `Graphics.DrawProceduralIndirect` for real and phantom drones.

The job never reads and writes the same drone state buffer in one pass.

## Task Arbitration

`DroneTaskAssignmentJob` evaluates the vault-backed dense `NativeArray<DroneTaskDTO>` generated from the hub task map. `DroneCognitionJob` keeps a compatibility fallback over `NativeParallelMultiHashMap<int, HeadlessDroneTask>` for launch-era targets, but macro A* waypoints are cleared and ignored.

Score:

```csharp
Score = (Criticality / max(distanceSq, 0.5625f)) * saturate(BatteryPercent * 0.01f);
```

Atomic claim:

```csharp
int priorOwner = Interlocked.CompareExchange(ref claimPtr[taskIndex], droneId, 0);
bool claimed = priorOwner == 0 || priorOwner == droneId;
```

Before scheduling, `DroneFleetManager.ClearHeadlessTaskClaims()` clears the claim-owner array and seeds it with active drones that already hold a valid `TargetTaskIndex`. New idle drones can only claim still-unowned task indices.

Emergency rule:
- when OS level is `Evacuate`, parasite tasks are skipped by assignment.
- speed multiplier = `3x`.
- battery drain multiplier = `5x` in `DroneMetabolismJob`.

Legacy hub assignment still exists as a compatibility front door for launch decisions, but active headless claims are included when rebuilding claim counts.

## Supply Cycle

Launch load:
1. hub resolves `Nanite_Solder`, then falls back to `Data_TitaniumScrap`.
2. hub checks accessible stock through `BaseLogisticsNetwork.CountAccessibleItem`.
3. hub queues a headless drone launch.
4. hub commits launch stock through `BaseLogisticsNetwork.TryReserveResources` and `CommitReserved`.

Field resupply:
1. a drone with `SolderUnits <= 0` switches to `ResupplyTravel`.
2. hub resolves the nearest connected `StorageCrate` or `Fabricator` through `BaseLogisticsNetwork.TryResolveNearestSupplyEndpoint`.
3. when docked, hub calls `TryAcquireDroneResupply`.
4. `BaseLogisticsNetwork.TryReserveResources` reserves one unit.
5. `BaseLogisticsNetwork.CommitReservedViaCommandQueue` registers touched crates and enqueues `EntityCommandType.CommitStorageReservation`.
6. `ThreadSafeCommandQueue.DrainMainThread` calls `StorageCrate.CommitReservation`.
7. no supply leaves the drone in `Stasis`.

## Parasite Defense

Parasite tasks are high-priority fleet tasks:
- source: `FloraInteractionManager.TryResolveNearestModuleParasite`
- criticality: `4 + infection*6 + airRisk*1.5`, plus cascade and emergency modifiers
- execution: `FloraInteractionManager.TryApplyDroneParasiteCut`
- organic damage channel: `DestructibleOrganicManager.TryApplyToolHit(... PlasmaCut)`

Direct native organic health writes are not used because `DestructibleOrganicManager` owns those lanes.

## Logic-Leech Hijack

External fauna code can call:

```csharp
DroneFleetManager.ReportLogicLeechContact(contactPosition, radiusMeters);
```

The nearest drone inside the radius flips to `HeadlessDroneFactionBit.Hostile`.
Hostile drones stop repairing and apply:
- `BaseModule.ApplyDamage`
- `HectonVoxelVolume.ApplyPlasmaCutDda`

Player damage is not wired here because no existing Logic-Leech/player damage contract exists in this task scope.

## Boid Separation

`DroneCognitionJob` samples neighboring drone indices through the native spatial hash:
- cell size: `2 m`
- sample area: 3x3x3 cells
- separation: inverse-square push
- alignment: average neighbor velocity, weight `0.25`
- cohesion: `0.8` open water, `0.1` tight voxel corridor
- player repulsion: 2.5 m radius, stronger than drone separation

Corridor state is sampled on the main thread with `VoxelDynamicNavGridRuntime.TrySampleHybridNavigation` before scheduling.

## Suicide Weld

Trigger:

```csharp
DroneFleetManager.RequestFleetSacrifice();
```

Eligibility:
- target is breached, or
- target is flooded and integrity is at or below 20% recoverable integrity

Effect:
- repair target to recoverable integrity
- clear flooded state through `ForceDrainComplete`
- mark the drone `Sacrificed`
- mark the native slot permanently destroyed
- increment fleet destroyed count

## 2026-05-19 SHINOBU_128 Procedural Boundary

Current source boundary:
- `DroneStateDTO` is explicit 64 B with XML offsets 0/24/36/40/44/48/52/56.
- `DroneTargetDTO` is explicit 64 B.
- `DroneProceduralIndirectArgsDTO` is explicit 16 B.
- New local vault IDs are 70265 `DroneStateDTO[512]`, 70266 `DroneTargetDTO[512]`, 70267 `DroneTaskDTO[64]`, and 70268 `DroneProceduralIndirectArgsDTO[1]`.
- `HeadlessDroneState` mirrors `PositionAup`, `HomeAup`, `TargetAup`, and `SupplyAup`.
- `Hecton_DroneFleetProcedural.shader` expands 36 procedural vertices from `SV_VertexID`; inactive zero matrices are clipped.

Compile/runtime proof is still blocked by the shared CPU gate recorded in `Docs/Tasks/Status_SHINOBU_128.md`.

## Verification Boundaries

This document proves owner mapping and intended data flow only.

Not proven without fresh Unity runtime logs:
- project compile-green state
- MCP console has zero current errors
- GCMonitor 0 B/frame
- Frame Debugger proof for the procedural drone draw path
