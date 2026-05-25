# DRONE FLEET PROTOCOL

Date: 2026-05-07

Status: PENDING VERIFICATION

## 2026-05-19 SHINOBU_128 Runtime Boundary

- Operational cap is 500 drones; native storage is 512 slots to keep 64-wide job batches and GPU buffers aligned.

- `DroneStateDTO` is explicit 64 B ABI (`double3 AUP_Position`, `float3 Velocity`, task hashes, battery, flags) with a layout sentinel in source.

- State/matrix native buffers are allocated with `NativeArrayOptions.UninitializedMemory` and cold-cleared through slot reset before runtime use.

- Task-map rebuild cadence is continuous: `framesBetweenUpdates = (int)math.lerp(5, 60, 1 - GlobalQualityWeight)`.

- Steering cadence, macro route solve budget, docking probe count, phantom draw count, and render distance now consume `HomeostasisBrain.GlobalQualityWeight` instead of hard quality-tier switches.

- Docking cross-current visual slip and dominant-axis telemetry precision also consume `GlobalQualityWeight`; no low/MX350 enum equality remains in touched drone math.

- Required black-box dump target is `Docs/AgentLogs/Dump_FLEET_COMMANDER.bin`; legacy `Dump_DRONE_FLEET.bin` and `.h8dump` are still emitted for older readers.

- CSV tuner:
  - default: `drone_chassis_specs.csv`;
  - fallback: `drone_specs.csv`;
  - read target: Vault scratch `(BufferID)12870277`;
  - parser: `ReadOnlySpan<byte>`;
  - staged rows: hashed `64 B` `DroneChassisSpecDTO`;
  - commit target: `(BufferID)12870276`;
  - malformed reload cannot clear the live chassis table;
  - absent CSV uses deterministic fallback chassis specs from Vault-backed tuning DTO.

- Real and phantom drone rendering now submit through `Graphics.DrawProceduralIndirect`.
- Shader: `Hecton8/Construction/DroneFleetProcedural`.
- Indirect args upload uses `LockBufferForWrite`/vault-native staging instead of managed `SetData` arrays.
- Unity import and Frame Debugger proof remain pending.

- Fleet snapshot event deferral is now vault-array-backed: pending and next-frame payload lanes use local BufferIDs 70271 and 70272 instead of persistent `NativeQueue` fields.

- Boid spatial lookup is now a flat vault-backed bucket/head/next/key lane: BufferIDs 70273, 70274, and 70275 replace the former `NativeParallelMultiHashMap<int,int>`.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not drone runtime, scene wiring, Frame Debugger, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Construction/DroneFleetManager.cs`

- `Assets/_Project/Scripts/Construction/DroneCognitionJob.cs`

- `Assets/_Project/Scripts/Construction/DroneFleetNavigationKernel.cs`

- `Assets/_Project/Scripts/Construction/RepairDroneHub.cs`

- `Assets/_Project/Scripts/Construction/RepairDroneEntity.cs`

- `Assets/_Project/Scripts/Construction/BaseLogisticsNetwork.cs`

- `Assets/_Project/Scripts/Editor/FleetAutomationTunerWindow.cs`

- `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`

- `Assets/_Project/Art/Shaders/Hecton_DroneFleetProcedural.shader`

- `Assets/_Project/Art/Shaders/DroneCulling.compute`

## 2026-05-11 Historical Override + 2026-05-17 Actuality Pointer

- Historical manifest: `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`.

- Historical actuality manifest: `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json` (historical snapshot only; do not use for current counts or proof).

- Current actuality ledger: `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.

- Existing May 4 boundary sections in this file are historical unless they describe local system intent not contradicted by newer reports.

- Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, and visual quality remain `PENDING VERIFICATION`.

## Historical 2026-05-04 Boundary

- Evidence limit: fleet contract only; scene wiring, native buffers, render submission, and hub requests remain unproven.

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

- `NativeArray<DroneTaskDTO>[64]` dense task snapshot for hub-keyed assignment

- `NativeArray<int>[2048]` spatial bucket heads plus `NativeArray<int>[512]` next/key lanes for 2 m boid separation

- `NativeArray<int>[512]` task claim owners

Scheduling model:

1. `RepairDroneHub.SlowTick()` queues launch/abort/release requests into managed fixed-capacity arrays.

2. `DroneFleetManager.HeadlessFleetDriver.Tick()` schedules the chain: `ClearDroneMacroWaypointsJob -> DroneTaskAssignmentJob -> DroneCognitionJob -> DroneMetabolismJob -> ExtractDroneMatricesJob -> BuildDroneProceduralArgsJob`.

3. `LateFrameTick()` completes the job in the dispatcher swap window, swaps front/back buffers, applies managed-side repair/storage/organic/voxel commits, then applies queued hub requests.

4. SRP render callback uploads `NativeArray<float4x4>` and calls `Graphics.DrawProceduralIndirect` for real and phantom drones.

The job never reads and writes the same drone state buffer in one pass.

## Task Arbitration

`DroneTaskAssignmentJob` evaluates the vault-backed dense `NativeArray<DroneTaskDTO>` generated from hub scans. `DroneCognitionJob` no longer owns a task multimap fallback; macro A* waypoints are cleared and ignored.

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

- publish `HullRepairedSignal` for the requested recovery amount; habitat/base owner remains the only authority that may mutate integrity or flooding state.

- mark the drone `Sacrificed`

- mark the native slot permanently destroyed

- increment fleet destroyed count

## 2026-05-19 SHINOBU_128 Procedural Boundary

Current source boundary:

- `DroneStateDTO` is explicit 64 B with XML offsets 0/24/36/40/44/48/52/56.

- `DroneTargetDTO` is explicit 64 B.

- `DroneProceduralIndirectArgsDTO` is explicit 16 B.

- Local vault IDs:
  - `70265`: `DroneStateDTO[512]`;
  - `70266`: `DroneTargetDTO[512]`;
  - `70267`: `DroneTaskDTO[64]`;
  - `70268`: `DroneProceduralIndirectArgsDTO[1]`;
  - `70269`: `DroneServiceCommand[1536]`;
  - `70270`: `DroneServiceCommandCursor[1]`;
  - `70271`: `HectonDroneFleetSnapshotPayload[64]` pending lane;
  - `70272`: `HectonDroneFleetSnapshotPayload[64]` next-frame lane;
  - `70273`: `int[2048]` spatial bucket heads;
  - `70274`: `int[512]` spatial next indices;
  - `70275`: `int[512]` spatial cell keys;
  - `(BufferID)12870276`: `DroneChassisSpecDTO[8]`;
  - `(BufferID)12870277`: `byte[16384]` CSV scratch.

- `DroneChassisSpecDTO`: explicit `64 B`.
  - `TypeHash`: offset `0`;
  - `Flags`: offset `4`;
  - seven `4 B` tuning floats: offsets `8..36`;
  - `ulong` pads: offsets `40`, `48`, `56`.
- CSV parser:
  - lower-case FNV-1a hashes for chassis type names;
  - applies key/value tuning lines first;
  - stages multi-column chassis rows on the stack before commit;
  - no `string.Split`;
  - no `File.ReadAllBytes`;
  - no runtime `NativeHashMap`.

- `DroneServiceCommand` and `DroneServiceCommandCursor` are explicit 64 B DTOs. The cursor is a single cache-line atomic counter; command slots are cache-line padded to prevent worker false sharing during parallel service writes.

- `HectonDroneFleetEvents` drains pending snapshot payloads from vault-backed flat arrays with read/count cursors; reentrant listener updates are deferred into the next-frame flat lane.

- `DroneCognitionJob` samples neighboring drones through the flat bucket/head/next/key spatial lane. It checks exact spatial cell keys after bucket hashing, so hash collisions only add bounded comparisons, not false neighbors.

- `HeadlessDroneState` mirrors `PositionAup`, `HomeAup`, `TargetAup`, and `SupplyAup`.

- `Hecton_DroneFleetProcedural.shader` expands 36 procedural vertices from `SV_VertexID`; inactive zero matrices are clipped.

- Real and phantom drones share the same procedural shader.
- Real draws bind a 1-slot white color buffer with `_UsePhantomColors = 0`.
- Phantom draws bind compute-authored color buffer with `_UsePhantomColors = 1`.
- Real fleet path has no hidden dependency on phantom-resource initialization.

- Scheduling/probe methods now take tuning data only and resolve `GlobalQualityWeight` internally; no drone steering/probe method accepts a misleading `HectonQualityTier` parameter.

- `DroneFleetOriginShiftJob`, `DroneTaskAssignmentJob`, `DroneMetabolismJob`, `ExtractDroneMatricesJob`, `BuildDroneProceduralArgsJob`, dormant `DroneMacroAStarJob`, and the flat `NativeArray` lanes in `DroneCognitionJob` carry `[NoAlias]` where the arrays are independent.

- `ApplyFriendlyRepairService` and sacrifice execution no longer call `BaseModule.Repair` or `ForceDrainComplete`; they emit typed signal lanes and wait for the habitat owner to apply authority.

- `ResolveDroneVaultBuffer` first uses `GlobalRegistry.DataVault`, then `GlobalDataVault.TryGetLatestCreated`, then the existing `H8Memory` fallback for CI/mock survival.

- This `GlobalRegistry` / `GlobalDataVault` route is static source orientation only until a proof artifact names the owner, producer/consumer phase, capacity/overflow behavior, failure/telemetry behavior, command, timestamp, environment, and output.

- No persistent private `NativeQueue` or `NativeParallelMultiHashMap` remains in touched drone runtime source.
- Remaining touched-source `NativeQueue` use: transient `GenerateMockDroneTasksQueueJob` writer.
- Purpose: CI/mock task injection, not persistent private state.

Older SHINOBU_128 status text recorded an external World/MapMagic compile wall around `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.

R34 filesystem/source grep did not find that path on disk or in `Hecton8.Core.csproj`. Treat the old compile-wall sentence as historical unless fresh compile or current project-file grep proves it again.

Fresh 2026-05-19 active project-file probe did not find the MapMagic source include in active `.csproj`, `.rsp`, or solution files. Current static project-file blockers outside SHINOBU_128 ownership are:

- `Assembly-CSharp.csproj` -> missing `Assets/_Project/_Archive/HectonWaterPhysics.cs`

- `Assembly-CSharp.csproj` -> missing `Assets/_Project/_Archive/HectonWaterPhysicsEditor.cs`

- `Hecton8.Core.csproj` still contains generated include text for missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, but R37-era `Directory.Build.targets` removed that stale item for guarded Core CLI builds; remaining Core errors are external missing contract/source bridge types.

R35 project-file/filesystem scan finds `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` present on disk despite the Core project include, so older ChemicalInfluenceGrid missing-file wording is stale. This is not compile/import proof.

Do not use the historical MapMagic sentence as the current SHINOBU compile blocker without a new compiler artifact.

## Verification Boundaries

Evidence limit: owner map and intended data flow only.

Not proven without fresh Unity runtime logs:

- project compile-green state

- MCP console has zero current errors

- GCMonitor 0 B/frame

- Frame Debugger proof for the procedural drone draw path
