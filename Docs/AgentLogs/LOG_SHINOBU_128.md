# LOG_SHINOBU_128

## 2026-05-19 - Batch Prompt Extraction Blocker
What was wrong -> `Docs/Tasks/CURRENT_BATCH.md` does not contain `<AGENT_PROMPT id="SHINOBU_128">`. Regex extraction failed, `rg` confirmation found current-batch XML prompts only for `SHINOBU_100` through `SHINOBU_120` with gaps.

What was done -> Read project authority files needed for authorization: `AGENTS.md`, `Docs/Actual Domains of Project.txt`, current batch file, status/rationale state. Created `Docs/Tasks/Status_SHINOBU_128.md` and `Docs/AgentLogs/Rationale_SHINOBU_128.md`. Identified relevant mandates for the requested Drone Fleet Commander domain, but did not touch runtime code.

Cinematic Cheats used -> None implemented. Future drone work must prefer potential-field steering and presentation-only GPU matrix interpolation over NavMesh/GameObject truth.

Exact Microseconds saved -> 0 us measured runtime. Engineering risk avoided: unauthorized implementation from chat-only summary, neighboring prompt contamination, and unnecessary build CPU load.

Verification -> Static file checks only. No Unity import, Play Mode, profiler, GCMonitor, Frame Debugger, or compilation was run. Status remains BLOCKED_BY_BATCH_PROMPT_MISSING.

## 2026-05-19 - Prompt Re-Extraction Corrected
What was wrong -> The first extraction ran before the current batch exposed the `SHINOBU_128` XML block, producing a stale blocker record.

What was done -> Re-ran cover-to-cover extraction. `SHINOBU_128` exists, role `SUBMARINE_DRONE_FLEET_COMMANDER`, task count 20. Status and rationale corrected to IN_PROGRESS before runtime source edits.

Cinematic Cheats used -> None implemented yet. Mandated path is potential-field math plus `DrawProceduralIndirect` presentation.

Exact Microseconds saved -> 0 us measured runtime. Engineering risk avoided: stale blocker state and wrong task count.

Verification -> Static extraction only. Runtime verification pending after code changes.

## 2026-05-19 - Drone Fleet Runtime Pass
What was wrong -> Real headless drone capacity was 64, DTO ABI was sequential, battery return was 10%, quality control used hard tiers, dump path was not the required fleet commander file, CSV default was `drone_specs.csv`, editor debug lacked final velocity, and docs overstated proof. Subagent audits also found exact `DrawProceduralIndirect` compliance and full AUP runtime state still unproven.

What was done -> Raised operational cap to 500 over 512 native slots, kept 64-wide job batches, added explicit 64 B `DroneStateDTO` and layout sentinel, added Burst `GenerateMockDroneTasksJob`, switched cognition/A* Burst attributes to deterministic sync compile, raised return threshold to 15%, added Abyssal Flow battery stress, moved task rebuild/steering/solve/probe/phantom/render-distance budgets to continuous `HomeostasisBrain.GlobalQualityWeight`, wrote black-box dumps to `Docs/AgentLogs/Dump_FLEET_COMMANDER.bin`, defaulted tuner CSV to `drone_chassis_specs.csv` with legacy fallback, exported debug velocity, drew green attraction/red SDF/blue velocity vectors, and updated `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md` with the real boundary.

Cinematic Cheats used -> Kept SDF/boid potential-field steering and phantom matrix swarm as bounded visual cheat. Did not replace the working matrix render path with blind procedural geometry because shader proof is absent.

Exact Microseconds saved -> Estimated 15-90 us/frame on weak hardware from continuous task/route/probe/phantom throttling at low quality; 3-8 us/dispatch stability from 512 storage over ragged 500; 0 B hot-path GC added. No profiler measurement was run.

Verification -> Static diff, `git diff --check`, source search, and two read-only subagent audits. `dotnet build` was not launched because CPU check returned 100%, above the forbidden threshold. Exact `DrawProceduralIndirect`, full runtime AUP migration, and profiler/GC proof remain pending.

## 2026-05-19 - Ultra Polish Runtime Reconciliation
What was wrong -> Previous runtime pass still had four hard violations: `DroneStateDTO` did not match the XML offsets/names, assignment was hidden inside cognition instead of a named O(N*M) job, macro A* waypoints still had a live scheduling path, and real fleet rendering still had mesh indirect submission. AUP target authority was also only partial.

What was done -> Corrected `DroneStateDTO` to exact explicit 64 B XML layout; added `DroneTargetDTO` and 16 B `DroneProceduralIndirectArgsDTO`; added `GenerateMockDroneTasksQueueJob`, `DroneTaskAssignmentJob`, `DroneMetabolismJob`, `ClearDroneMacroWaypointsJob`, `ExtractDroneMatricesJob`, and `BuildDroneProceduralArgsJob`; added vault-backed DTO/target/assignment/args buffers under local IDs 70265..70268; routed simulation chain as clear-waypoints -> assignment -> cognition -> metabolism -> matrix extraction -> args; disabled macro waypoint use; expanded `HeadlessDroneState` with `PositionAup/HomeAup/TargetAup/SupplyAup`; updated launch/docking/orphan/resupply/hijack/return paths to maintain AUP mirrors; replaced real and phantom mesh indirect rendering with `Graphics.DrawProceduralIndirect`; added `Assets/_Project/Art/Shaders/Hecton_DroneFleetProcedural.shader`; published `HullRepairedSignal` and `InventoryCommandSignal` from service paths while keeping compatibility mutation until the habitat owner consumes repair signals as sole authority; updated `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md` to remove stale `RenderMeshIndirect` authority.

Cinematic Cheats used -> The fleet now draws a procedural cuboid generated from `SV_VertexID` rather than a mesh asset. Macro navigation is reduced to potential fields and SDF repulsion; phantom overkill shares the same procedural matrix shader. This is the Dear Lie: no NavMesh, no mesh-instanced fleet, no per-drone transforms.

Exact Microseconds saved -> Static estimate only: macro route heap solve avoided, 10-90 us/frame depending old solve count; mesh indirect branch removed, exact CPU us pending Frame Debugger; assignment job costs roughly 18-45 us for 500x64 but replaces unmanaged/managed mixed selection ambiguity; metabolism job adds about 3-8 us per 512 slots; 0 B hot-path GC added.

Verification -> `git diff --check` passed with line-ending warnings only. Focused search in touched drone files found no `NavMeshAgent`, `UnityEngine.AI`, `Instantiate(`, `new GameObject(`, `RenderMeshIndirect`, or `DrawMeshInstancedIndirect`. Burst attributes in touched job files all include `CompileSynchronously = true` and deterministic float mode. Brace counts: `DroneFleetNavigationKernel.cs` 143/143, `DroneCognitionJob.cs` 110/110, `DroneFleetManager.cs` 500/500. `dotnet build` was not launched because CPU load was 88%, above the explicit 50% gate.

<SELF_AUDIT agent_id="SHINOBU_128">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">No NavMeshAgent/UnityEngine.AI in touched drone files.</TASK>
    <TASK id="02" status="PASS">No Instantiate/new GameObject in touched drone fleet files.</TASK>
    <TASK id="03" status="PASS">Explicit 64 B DTO plus raw pointer mutation in Burst jobs.</TASK>
    <TASK id="04" status="PASS">Layout sentinel validates size and offsets.</TASK>
    <TASK id="05" status="PASS">NativeQueue mock task job added.</TASK>
    <TASK id="06" status="PASS">DroneTaskAssignmentJob added with O(N*M) greedy scoring and atomic claims.</TASK>
    <TASK id="07" status="PASS">Macro A* lanes are cleared and ignored; potential/SDF/flow steering remains.</TASK>
    <TASK id="08" status="PASS">Real and phantom fleet use DrawProceduralIndirect with matrix buffers.</TASK>
    <TASK id="09" status="PARTIAL">Signals are published; direct BaseModule.Repair remains as compatibility until habitat owner consumes HullRepairedSignal as authority.</TASK>
    <TASK id="10" status="PASS">DroneMetabolismJob drains velocity-based battery and handles 15% return/0 stasis.</TASK>
    <TASK id="11" status="PASS">GlobalQualityWeight continuous cadence remains active.</TASK>
    <TASK id="12" status="PASS">Abyssal flow steering and battery stress retained.</TASK>
    <TASK id="13" status="PASS">AUP target deltas are subtracted in double before float cast.</TASK>
    <TASK id="14" status="PASS">Blittable state/target/args DTOs and deterministic Burst jobs.</TASK>
    <TASK id="15" status="PASS">Uninitialized native buffers plus cold explicit clear.</TASK>
    <TASK id="16" status="PASS">300-frame ring and `Dump_FLEET_COMMANDER.bin` path retained.</TASK>
    <TASK id="17" status="PASS">UI Toolkit tuner shell retained.</TASK>
    <TASK id="18" status="PASS">`drone_chassis_specs.csv` default retained.</TASK>
    <TASK id="19" status="PASS">Scene debug vectors retained.</TASK>
    <TASK id="20" status="FAIL">Compilation/profiler proof blocked by CPU 88%; no fake pass reported.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DroneStateDTO size="64" alignment="8/16 safe">
      <FIELD name="AUP_Position" offset="0" size="24" />
      <FIELD name="Velocity" offset="24" size="12" />
      <FIELD name="CurrentTaskHash" offset="36" size="4" />
      <FIELD name="BatteryLevel" offset="40" size="4" />
      <FIELD name="Flags" offset="44" size="4" />
      <FIELD name="_pad0" offset="48" size="4" />
      <FIELD name="_pad1" offset="52" size="4" />
      <FIELD name="_pad2" offset="56" size="8" />
    </DroneStateDTO>
    <DroneTargetDTO size="64" alignment="8/16 safe" />
    <DroneProceduralIndirectArgsDTO size="16" fields="vertexCount,instanceCount,startVertex,startInstance" />
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, task rebuild cadence expands toward 60 frames, steering frequency drops through the existing GlobalQualityWeight curve, phantom count trends toward zero, and potential-field math bypasses macro route solving entirely. Above quality 0.7, assignment cadence tightens and phantom procedural visuals spend the saved CPU/GPU budget.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>New persistent buffers are requested from GlobalDataVault/fallback through local BufferID casts: 70265 DroneStateDTO[512], 70266 DroneTargetDTO[512], 70267 DroneTaskDTO[64], 70268 DroneProceduralIndirectArgsDTO[1]. Existing NativeParallelMultiHashMap/NativeQueue fallbacks remain legacy local allocations.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCIES>NoAlias is applied to assignment, metabolism, clear, matrix, and args job arrays where possible. Dependency graph: ClearDroneMacroWaypointsJob -> DroneTaskAssignmentJob -> DroneCognitionJob -> DroneMetabolismJob -> ExtractDroneMatricesJob -> BuildDroneProceduralArgsJob.</POINTER_ALIASING_AND_DEPENDENCIES>
  <COMPILE_GUARD>No new sibling runtime assembly reference was added. Work stayed in Construction and shader asset files; Core enum was not edited.</COMPILE_GUARD>
  <DEAR_LIE>Before: mesh/pathfinding route pressure with macro A* and mesh indirect submission. After: O(N*M) bounded assignment, O(N*k) local potential field steering, and GPU procedural cuboids from matrices. No simulated hydrodynamic drones, no NavMesh, no per-drone Transform.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Compile Wall Evidence
What was wrong -> The first allowed build attempt did not reach SHINOBU_128 code. `Hecton8.Core.csproj` references a deleted World/MapMagic source file: `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`.

What was done -> Ran exactly one gated build after CPU dropped to 46% and no `dotnet/csc` process was active. Build command: `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal`. After failure, ran `dotnet build-server shutdown` and verified no active `dotnet/csc` remained.

Cinematic Cheats used -> None. This is validation plumbing, not runtime simulation.

Exact Microseconds saved -> 0 us runtime. Avoided cross-domain sabotage by not restoring or editing the World/MapMagic bridge from the Drone Fleet domain.

Verification -> Build result: `CSC : error CS2001: Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs' could not be found. [C:\hades\Hecton8\Hecton8.Core.csproj]`. Static SHINOBU checks remain clean; compiler proof remains externally blocked.

## 2026-05-19 - Ultra Polish Signal/Args Reconciliation
What was wrong -> The previous pass still had stale architectural residue: repair/sacrifice could mutate habitat authority directly, procedural indirect args still used managed upload-cache arrays/`SetData`, `ResolveDroneVaultBuffer` missed an existing latest `GlobalDataVault` when registry injection was late, and the architecture doc still described suicide weld as direct repair/drain mutation.

What was done -> Removed `BaseModule.Repair` and `ForceDrainComplete` from drone repair/sacrifice service execution. Kept `HullRepairedSignal` as the repair authority route and `InventoryCommandSignal` for mining. Removed managed procedural args upload arrays; real draw args now upload from vault-native `DroneProceduralIndirectArgsDTO[1]` through `GraphicsBufferUploadUtility.UploadNativeArray`, and phantom draw args are written directly with `GraphicsBuffer.LockBufferForWrite`. Created indirect args buffers with `GraphicsBuffer.UsageFlags.LockBufferForWrite`. Added `GlobalDataVault.TryGetLatestCreated` before `H8Memory` fallback. Updated `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md`, `Status_SHINOBU_128.md`, and this log.

Cinematic Cheats used -> Phantom drone overkill now draws a full-capacity procedural swarm while the compute shader writes zero matrices/colors for inactive slots. That keeps active quality changes smooth without changing CPU draw topology. Real drones remain matrix-driven procedural cuboids, not mesh instances or GameObjects.

Exact Microseconds saved -> Estimated small but deterministic render-sync saving from removing managed one-element args caches and `SetData` staging. Wrong-owner repair mutation cost is a correctness fix, not a measured frame-time win. H-PHI fallback hardening reduces boot-time local allocation risk when the vault exists but registry injection has not landed.

Verification -> Focused `rg` found no `BaseModule.Repair`, `target.Repair(`, `ForceDrainComplete(`, `GlobalSignals.Publish`, `RenderMeshIndirect`, `DrawMeshInstancedIndirect`, managed procedural args upload arrays, `NavMeshAgent`, `UnityEngine.AI`, `Instantiate(`, or `new GameObject(` in touched drone files. `DroneFleetManager.cs` brace count is 476/476. `dotnet build` was not rerun because the external World/MapMagic compile wall is unchanged.

<SELF_AUDIT agent_id="SHINOBU_128" pass="ULTRA_POLISH_SIGNAL_ARGS">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">NavMeshAgent/UnityEngine.AI absent from touched drone fleet files.</TASK>
    <TASK id="02" status="PASS">Per-drone GameObject spawn path absent from touched drone fleet files.</TASK>
    <TASK id="03" status="PASS">`DroneStateDTO` is explicit 64 B and Burst jobs mutate via raw pointer/ref where required.</TASK>
    <TASK id="04" status="PASS">Source sentinel validates `DroneStateDTO` offsets 0/24/36/40/44/48/52/56.</TASK>
    <TASK id="05" status="PASS">Mock task queue job remains NativeQueue/Burst-backed.</TASK>
    <TASK id="06" status="PASS">`DroneTaskAssignmentJob` remains O(N*M), no-alias, atomic claim based.</TASK>
    <TASK id="07" status="PASS">Macro waypoint authority is cleared/ignored; potential/SDF/flow steering remains.</TASK>
    <TASK id="08" status="PASS">Real and phantom rendering use matrices plus `Graphics.DrawProceduralIndirect`.</TASK>
    <TASK id="09" status="PASS_SOURCE_PENDING_RUNTIME">Direct habitat repair/drain mutation removed; runtime consumer proof pending external compile-wall clearance.</TASK>
    <TASK id="10" status="PASS">`DroneMetabolismJob` remains separate and deterministic.</TASK>
    <TASK id="11" status="PASS">Continuous `GlobalQualityWeight` cadence remains active.</TASK>
    <TASK id="12" status="PASS">Abyssal flow remains a vector-field cheat, not fluid simulation.</TASK>
    <TASK id="13" status="PASS">AUP mirrors and double3 local deltas remain in source.</TASK>
    <TASK id="14" status="PASS">Primary DTOs are blittable and memcpy-ready; jobs use deterministic Burst mode.</TASK>
    <TASK id="15" status="PASS">Cold uninitialized buffers are explicitly initialized before use.</TASK>
    <TASK id="16" status="PASS">300-frame black-box ring and `Dump_FLEET_COMMANDER.bin` path remain.</TASK>
    <TASK id="17" status="PASS">Editor tuner shell remains editor/UI Toolkit bounded.</TASK>
    <TASK id="18" status="PASS">`drone_chassis_specs.csv` remains default with legacy fallback.</TASK>
    <TASK id="19" status="PASS">Debug vectors remain source-backed.</TASK>
    <TASK id="20" status="FAIL_EXTERNAL">Compilation, Unity import, Frame Debugger, and GCMonitor proof remain blocked by deleted external World source.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DroneStateDTO size="64">0 double3 AUP_Position 24B; 24 float3 Velocity 12B; 36 uint CurrentTaskHash 4B; 40 float BatteryLevel 4B; 44 uint Flags 4B; 48 uint pad0 4B; 52 uint pad1 4B; 56 ulong pad2 8B.</DroneStateDTO>
    <DroneTargetDTO size="64">0 double3 TargetAUP 24B; 24 float3 LocalPosition 12B; 36 uint TaskHash; 40 int TaskIndex; 44 int TargetModuleId; 48 float Radius; 52 uint TaskKind; 56 uint Flags; 60 uint Reserved0.</DroneTargetDTO>
    <DroneProceduralIndirectArgsDTO size="16">0 uint VertexCountPerInstance; 4 uint InstanceCount; 8 uint StartVertex; 12 uint StartInstance.</DroneProceduralIndirectArgsDTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, task rebuild cadence stretches toward 60 frames, steering ticks decimate through the existing modulo, phantom active count lerps down, and inactive phantom slots become zero matrices/colors in compute. Above quality 0.7, the same draw topology spends budget on denser visual swarm and longer render distance.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs requested at boot include 70265 DroneStateDTO[512], 70266 DroneTargetDTO[512], 70267 DroneTaskDTO[64], and 70268 DroneProceduralIndirectArgsDTO[1]. `ResolveDroneVaultBuffer` now checks registry vault and latest-created vault before H8Memory fallback. Remaining local native scratch: hub task multimap, drone spatial hash multimap, service command queue, and snapshot event queues.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Dependency chain remains ClearDroneMacroWaypointsJob -> DroneTaskAssignmentJob -> DroneCognitionJob -> DroneMetabolismJob -> ExtractDroneMatricesJob -> BuildDroneProceduralArgsJob. NoAlias remains on DTO/target/task/matrix/args arrays where source supports it.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new sibling runtime asmdef reference was added. Touched files remain in Construction/shader/docs/log domain. Full compile proof is externally blocked.</COMPILE_GUARD>
  <DEAR_LIE>Before: potential standard Unity mesh/repair truth path and direct module mutation. After: bounded potential-field drones, procedural cuboids from matrices, phantom compute zeroing, and signal-only habitat repair route. Complexity stays bounded at O(N*M) assignment plus O(N*k) local steering; no NavMesh or per-drone Transform.</DEAR_LIE>
</SELF_AUDIT>

## 2026-05-19 - Continuous Quality Residue Removal
What was wrong -> Two drone math paths still carried binary quality behavior: docking cross-current visual slip used a low-tier enum gate, and dominant-axis telemetry switched between approximate and exact distance via `DistanceMath.IsHighQualityTier`.

What was done -> Replaced the docking slip gate with `CrossCurrentVisualSlipWeight = GlobalQualityWeight`. Replaced the telemetry precision switch with a polynomial weight that lerps `DominantAxisMagnitudeSq` to exact `math.distancesq`.

Cinematic Cheats used -> Low-weight hardware keeps the dominant-axis distance fake as the visual/telemetry approximation; high-weight hardware buys exact squared distance without a hard pop.

Exact Microseconds saved -> No measured timing. This pass removes binary quality discontinuity rather than claiming a profiler win.

Verification -> Focused `rg` found no `IsLowDockingMathTier`, `DistanceMath.IsHighQualityTier`, `HectonQualityTier.Low`, `HectonQualityTier.Mx350`, or `HectonQualityTier.Unknown` in touched drone files. Brace counts: `DroneFleetManager.cs` 475/475, `DroneCognitionJob.cs` 110/110.

## 2026-05-19 - Render Binding / Tier Parameter Hygiene
What was wrong -> The procedural shader now serves real and phantom drones, but real drone draws could happen before phantom buffers existed. That left `_PhantomColors` as a possible unbound structured buffer even though `_UsePhantomColors` is zero. Three quality-resolved helper methods also still accepted dead `HectonQualityTier` arguments, which made the source look binary even after the math was continuous.

What was done -> Added `s_DroneDefaultColorBuffer`, a one-element white `float4` structured buffer, initialized through `LockBufferForWrite` and bound before real `Graphics.DrawProceduralIndirect`. Phantom rendering keeps the compute-authored color buffer and sets `_UsePhantomColors = 1`. Removed dead tier arguments from `ResolveDroneSteeringTickModulo`, `ResolveDroneAStarSolveBudget`, and `ResolveDockingObstacleSegmentCount`; those methods now resolve `GlobalQualityWeight` internally.

Cinematic Cheats used -> One shared procedural cuboid shader covers both real and phantom drones. Real drones use a constant white color lane; phantom overkill uses per-instance color authored by compute. No material variant or mesh instancing path was added.

Exact Microseconds saved -> No profiler claim. Expected value is correctness: avoids backend buffer validation warnings or black reads on weak GPUs. Runtime cost is one cold 1-slot GPU buffer; no per-frame managed allocation was introduced.

Verification -> `git diff --check` passed for touched files. Focused `rg` found no direct repair mutation, NavMesh, GameObject spawn, mesh indirect render, managed args upload arrays, or binary low/MX350 quality checks. `DroneFleetManager.cs` brace count is 477/477. H-PHI scan still finds legacy `NativeQueue` and `NativeParallelMultiHashMap` scratch; that is recorded as unresolved route-card debt, not hidden.

## 2026-05-19 - Cognition Pointer Aliasing Hygiene
What was wrong -> The main cognition/origin-shift jobs had independent `NativeArray` lanes without `[NoAlias]`, so Burst had less proof for vectorized state, matrix, DTO, position, waypoint, and telemetry access.

What was done -> Added `[NoAlias]` to the separate `NativeArray` fields in `DroneFleetOriginShiftJob`, `DroneCognitionJob`, and the dormant `DroneMacroAStarJob` that still exists in Burst source. Did not add alias claims to `NativeQueue<DroneServiceCommand>.ParallelWriter` or `NativeParallelMultiHashMap` fields; those remain the explicit unresolved H-PHI container route problem.

Cinematic Cheats used -> None new. This pass supports the existing potential-field Dear Lie by giving Burst clearer memory ownership for its hot arrays.

Exact Microseconds saved -> Not measured. Expected effect is small but real if Burst can emit cleaner NEON/AVX load-store sequences under dense 500-drone steering.

Verification -> Source scan shows `[NoAlias]` on cognition/origin-shift array fields. Brace counts remain `DroneFleetManager.cs` 477/477, `DroneCognitionJob.cs` 110/110, `DroneFleetNavigationKernel.cs` 143/143. Forbidden-pattern scan remains empty.

## 2026-05-19 - Service Command Vault Lane
What was wrong -> `s_DroneServiceCommands` was still a private persistent `NativeQueue<DroneServiceCommand>`. The command struct was 40 bytes, so parallel workers could write adjacent commands across shared cache lines.

What was done -> Replaced the service queue with vault-backed flat buffers: BufferID 70269 `DroneServiceCommand[1536]` and BufferID 70270 `DroneServiceCommandCursor[1]`. `DroneServiceCommand` is now explicit 64 B; `DroneServiceCommandCursor` is explicit 64 B with `Count` at offset 0. `DroneCognitionJob` writes through an atomic cursor into the bounded command array; `DroneFleetManager` drains the written range after the job chain and resets the cursor.

Cinematic Cheats used -> None new. This is H-PHI and concurrency hygiene for the existing potential-field service lane.

Exact Microseconds saved -> Not measured. Expected saving is allocator/fragmentation risk reduction plus reduced false-sharing when many drones publish repair/mining/docking service commands in the same frame.

Verification -> Focused `rg` found no `NativeQueue<DroneServiceCommand>`, no `AsParallelWriter`, no service command `TryDequeue`, and no service command `Enqueue` in touched drone files. Layout scan shows `DroneServiceCommand` and `DroneServiceCommandCursor` are both explicit 64 B. Brace counts after the patch: `DroneFleetManager.cs` 478/478 and `DroneCognitionJob.cs` 115/115.

## 2026-05-19 - Snapshot Event Vault Lane
What was wrong -> `HectonDroneFleetEvents` still owned persistent private `NativeQueue` fields for pending and reentrant snapshot events. That left H-PHI debt in the cold telemetry bridge after the service command lane had already been moved to vault-backed flat storage.

What was done -> Replaced pending and next-frame event queues with vault-backed `NativeArray<HectonDroneFleetSnapshotPayload>[64]` lanes. BufferID 70271 owns pending snapshot payloads; BufferID 70272 owns next-frame reentrant payloads. Added read/count cursor draining, partial-dispatch compaction, and next-frame promotion when the front lane is empty. Removed dead `RegisterNativeQueue`, `DisposeNativeQueue`, and `PrewarmQueue` helpers from `DroneFleetManager`.

Cinematic Cheats used -> None new. This is ownership and forensics hygiene. The listener deferral remains a bounded flat-lane "queue fake" rather than a real container owner.

Exact Microseconds saved -> Not measured. Expected runtime delta is near zero because snapshot dispatch is cold. The concrete gain is removal of two persistent local native queue allocations and reduced allocator/forensics ambiguity on weak CPUs.

Verification -> Focused `rg` now finds only the required transient mock `NativeQueue<DroneTaskDTO>.ParallelWriter` and `Tasks.Enqueue(new DroneTaskDTO)` in `GenerateMockDroneTasksQueueJob`; it finds no persistent snapshot/service `NativeQueue`, no queue helper definitions, no service command enqueue/dequeue, and no snapshot queue enqueue/dequeue. `git diff --check` passed with line-ending warnings only. Brace counts: `DroneFleetManager.cs` 475/475, `DroneCognitionJob.cs` 115/115, `DroneFleetNavigationKernel.cs` 143/143, `Hecton_DroneFleetProcedural.shader` 12/12, `Hecton_PhantomDrones.compute` 4/4. Compile was not rerun because the external World/MapMagic compile wall is unchanged.

<SELF_AUDIT agent_id="SHINOBU_128" pass="H_PHI_SNAPSHOT_EVENT">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Touched drone fleet files remain clean of NavMeshAgent and UnityEngine.AI.</TASK>
    <TASK id="02" status="PASS">Touched drone fleet files remain clean of per-drone Instantiate/new GameObject paths.</TASK>
    <TASK id="03" status="PASS">Primary DTO lanes remain explicit/blittable; hot snapshot/service DTOs expose fields, not mutable properties.</TASK>
    <TASK id="04" status="PASS">Primary state DTO layout remains explicit 64 B; service command/cursor layouts are explicit 64 B.</TASK>
    <TASK id="05" status="PASS">CI/mock task generator still uses the required transient Burst NativeQueue writer; it is not a persistent private queue.</TASK>
    <TASK id="06" status="PASS">O(N*M) `DroneTaskAssignmentJob` remains the task assignment authority.</TASK>
    <TASK id="07" status="PASS">Macro A* authority remains disabled; potential fields, SDF repulsion, boid separation, and flow counterforce drive motion.</TASK>
    <TASK id="08" status="PASS">Real and phantom fleets still use matrix buffers plus `Graphics.DrawProceduralIndirect`.</TASK>
    <TASK id="09" status="PASS_SOURCE_PENDING_RUNTIME">Direct habitat repair/drain mutation remains absent; typed signal consumer proof remains blocked by external compile wall.</TASK>
    <TASK id="10" status="PASS">`DroneMetabolismJob` remains separate from cognition.</TASK>
    <TASK id="11" status="PASS">Continuous `GlobalQualityWeight` cadence/probe/render math remains source-clean of low/MX350 binary checks.</TASK>
    <TASK id="12" status="PASS">Abyssal current remains a vector-field cheat, not fluid simulation.</TASK>
    <TASK id="13" status="PASS">AUP mirrors and local double3 delta before float cast remain in source.</TASK>
    <TASK id="14" status="PASS">DTOs remain blittable and Burst jobs use deterministic compile flags.</TASK>
    <TASK id="15" status="PASS">Cold uninitialized buffers are still explicitly initialized before use.</TASK>
    <TASK id="16" status="PASS">300-frame black-box ring and `Dump_FLEET_COMMANDER.bin` path remain.</TASK>
    <TASK id="17" status="PASS">Editor tuning shell remains editor-only and UI Toolkit bounded.</TASK>
    <TASK id="18" status="PASS">`drone_chassis_specs.csv` remains default with legacy fallback.</TASK>
    <TASK id="19" status="PASS">Debug vectors remain available in the SceneView hook.</TASK>
    <TASK id="20" status="FAIL_EXTERNAL">Compiler, Unity import, Frame Debugger, GCMonitor, and runtime signal-consumer proof remain blocked by deleted external World source.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DroneStateDTO size="64">0 double3 AUP_Position 24B; 24 float3 Velocity 12B; 36 uint CurrentTaskHash 4B; 40 float BatteryLevel 4B; 44 uint Flags 4B; 48 uint pad0 4B; 52 uint pad1 4B; 56 ulong pad2 8B.</DroneStateDTO>
    <DroneServiceCommand size="64">0 int Slot 4B; 4 int DroneId 4B; 8 byte Kind 1B; 9 byte State 1B; 10 ushort Reserved 2B; 12 float DeltaTime 4B; 16 float3 Position 12B; 28 float3 TargetPosition 12B; 40 ulong Pad0 8B; 48 ulong Pad1 8B; 56 ulong Pad2 8B.</DroneServiceCommand>
    <DroneServiceCommandCursor size="64">0 int Count 4B; 4 implicit explicit-layout gap 4B; 8..63 seven ulong pads 56B. Atomic count occupies its own cache-line DTO.</DroneServiceCommandCursor>
    <HectonDroneFleetSnapshotPayload size="48">Sequential payload remains 16 B aligned by explicit tail padding fields.</HectonDroneFleetSnapshotPayload>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, task rebuild cadence stretches toward 60 frames, steering tick modulo increases, docking probes reduce through lerp, phantom count approaches zero, inactive phantom slots are zeroed by compute, and exact telemetry distance is blended toward a dominant-axis approximation. At high/ultra quality the same lanes spend budget on denser steering, full phantom colors, longer render distance, and exact distance.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs used by this pass: 70269 `DroneServiceCommand[1536]`, 70270 `DroneServiceCommandCursor[1]`, 70271 `HectonDroneFleetSnapshotPayload[64]` pending lane, and 70272 `HectonDroneFleetSnapshotPayload[64]` next-frame lane. No persistent private `NativeQueue` remains in touched drone source. Remaining local native scratch is limited to hub task and spatial-hash `NativeParallelMultiHashMap` lanes.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`DroneCognitionJob` consumes state/back-buffer, DTO, target, position, flow, waypoint, service command, and cursor arrays with `[NoAlias]` on flat lanes. Job chain remains ClearDroneMacroWaypointsJob -> DroneTaskAssignmentJob -> DroneCognitionJob -> DroneMetabolismJob -> ExtractDroneMatricesJob -> BuildDroneProceduralArgsJob. Snapshot event lanes are main-thread late-frame drains, not Burst jobs.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No direct sibling runtime assembly reference was added. Work stayed in Construction/shader/docs/log files. Build proof remains externally blocked by missing World/MapMagic source in `Hecton8.Core.csproj`.</COMPILE_GUARD>
<THE_DEAR_LIE_CONFIRMATION>Movement remains a potential-field/SDF/boid heuristic instead of NavMesh/path physics. Rendering remains procedural cuboids from matrices instead of GameObjects or mesh instances. Snapshot event queues are now bounded flat arrays, a container fake with O(1) append and O(n) late-frame drain over at most 64 payloads.</THE_DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - Flat Spatial Hash / Multimap Removal
What was wrong -> The service queue and snapshot queues were fixed, but two persistent private `NativeParallelMultiHashMap` containers remained inside Drone Fleet: hub task fanout and boid spatial hash. The task fanout duplicated the dense assignment DTO lane; the spatial hash could be represented as fixed flat buckets.

What was done -> Removed `HeadlessDroneTask`, `s_HeadlessTasksByHub`, `TasksByGrid`, and the cognition task-selection fallback. `DroneTaskAssignmentJob` is now the single task authority and is scheduled only in Repair formation so escort/search formation control is not preempted. Replaced `s_HeadlessDroneSpatialHash` with vault-backed flat arrays: BufferID 70273 `int[2048]` bucket heads, 70274 `int[512]` next indices, and 70275 `int[512]` exact spatial keys. `DroneCognitionJob` now samples 27 neighbor cells by hashing cell keys into buckets and validating exact keys before reading candidate drones.

Cinematic Cheats used -> The spatial hash is a bounded flat-array fake for local boid lookup. It keeps the visual swarm behavior without NavMesh, physics overlap queries, or an owned multimap container.

Exact Microseconds saved -> No profiler claim. Expected benefit is lower native container/iterator overhead and removal of the last two persistent local native containers in touched drone runtime. The algorithm remains O(N*k) for local neighbors instead of O(N^2).

Verification -> Focused `rg` finds no `NativeParallelMultiHashMap`, `NativeParallelMultiHashMapIterator`, `HeadlessDroneTask`, `TasksByGrid`, `DroneSpatialHash`, multimap register helper, or multimap dispose helper in touched drone files. Brace counts: `DroneFleetManager.cs` 473/473, `DroneCognitionJob.cs` 108/108, `DroneFleetNavigationKernel.cs` 143/143. Compile was not rerun because the external World/MapMagic compile wall is unchanged.

<SELF_AUDIT agent_id="SHINOBU_128" pass="FLAT_SPATIAL_HASH">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">No NavMeshAgent/UnityEngine.AI in touched drone files.</TASK>
    <TASK id="02" status="PASS">No per-drone Instantiate/new GameObject in touched drone files.</TASK>
    <TASK id="03" status="PASS">DTO lanes remain explicit/blittable; task/service/event/spatial hot data are fields, not properties.</TASK>
    <TASK id="04" status="PASS">Primary state DTO is 64 B; service command/cursor are 64 B; new spatial lanes are 4 B int arrays with fixed lengths.</TASK>
    <TASK id="05" status="PASS">Transient CI/mock `NativeQueue<DroneTaskDTO>.ParallelWriter` remains; no persistent private NativeQueue remains.</TASK>
    <TASK id="06" status="PASS">`DroneTaskAssignmentJob` is now the only task assignment authority.</TASK>
    <TASK id="07" status="PASS">Movement remains potential-field/SDF/flow/boid based, with no NavMesh and no live macro route authority.</TASK>
    <TASK id="08" status="PASS">Real and phantom visualization remain matrix buffers plus `DrawProceduralIndirect`.</TASK>
    <TASK id="09" status="PASS_SOURCE_PENDING_RUNTIME">Repair/mining authority remains signal-routed; runtime consumer proof is externally compile-blocked.</TASK>
    <TASK id="10" status="PASS">Metabolism remains a separate deterministic Burst job.</TASK>
    <TASK id="11" status="PASS">Continuous `GlobalQualityWeight` still controls cadence/probes/render/telemetry blend.</TASK>
    <TASK id="12" status="PASS">Abyssal flow remains a vector-field Dear Lie, not fluid simulation.</TASK>
    <TASK id="13" status="PASS">AUP-local deltas remain before float math.</TASK>
    <TASK id="14" status="PASS">Blittable DTOs and deterministic Burst compile flags remain.</TASK>
    <TASK id="15" status="PASS">Uninitialized buffers are still cold-cleared before use.</TASK>
    <TASK id="16" status="PASS">300-frame black-box ring and dump path remain.</TASK>
    <TASK id="17" status="PASS">Editor tuner remains editor-only.</TASK>
    <TASK id="18" status="PASS">`drone_chassis_specs.csv` remains default.</TASK>
    <TASK id="19" status="PASS">Debug vectors remain wired.</TASK>
    <TASK id="20" status="FAIL_EXTERNAL">Compiler/Unity/profiler proof remains blocked by the deleted external World source referenced by `Hecton8.Core.csproj`.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <DroneStateDTO size="64">0 double3 AUP_Position 24B; 24 float3 Velocity 12B; 36 uint CurrentTaskHash 4B; 40 float BatteryLevel 4B; 44 uint Flags 4B; 48 uint pad0 4B; 52 uint pad1 4B; 56 ulong pad2 8B.</DroneStateDTO>
    <DroneServiceCommand size="64">0 int Slot; 4 int DroneId; 8 byte Kind; 9 byte State; 10 ushort Reserved; 12 float DeltaTime; 16 float3 Position; 28 float3 TargetPosition; 40/48/56 ulong pads.</DroneServiceCommand>
    <FlatSpatialHash>70273 bucket heads: 2048 * 4 B = 8192 B; 70274 next indices: 512 * 4 B = 2048 B; 70275 keys: 512 * 4 B = 2048 B. Total 12288 B, all 4 B aligned and vault-owned.</FlatSpatialHash>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, task rebuild cadence stretches, steering ticks decimate, docking probes reduce, exact telemetry distance blends toward dominant-axis approximation, and phantom count approaches zero. The flat spatial hash preserves local boid lookup without allocating; lower quality simply invokes it less often.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Persistent native drone lanes touched in this pass route through vault handles. Added 70273, 70274, and 70275 for spatial lookup. No persistent private `NativeQueue` or `NativeParallelMultiHashMap` remains in touched drone runtime source.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`DroneCognitionJob` consumes flat spatial arrays with `[ReadOnly, NoAlias]`. Dependency chain remains ClearDroneMacroWaypointsJob -> DroneTaskAssignmentJob -> DroneCognitionJob -> DroneMetabolismJob -> ExtractDroneMatricesJob -> BuildDroneProceduralArgsJob.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling runtime assembly reference was added; edits stayed in Construction/shader/docs/log scope.</COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>Before: container-owned multimap lookup and duplicate task fallback. After: one dense DTO task lane plus a flat bucket spatial fake. Complexity remains bounded O(N*M) assignment and O(N*k) local steering.</THE_DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
