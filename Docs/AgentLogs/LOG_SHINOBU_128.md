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
