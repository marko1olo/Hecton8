# SHINOBU_37 Status - Physics Culling And LOD Overseer

Agent: SHINOBU_37  
Domain: PHYSICS_CULLING_AND_LOD_OVERSEER  
Task Count: 20  
Current State: CORE TASKS CHECKED / POLISH AUDIT COMPLETE / PENDING UNITY RUNTIME VERIFICATION

## Mandates Read

- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- MATH_AUP_Determinism_Sync.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Execution_Phases.txt

## Task Matrix

- [x] Task 01 - Archive scan found no `physics_culling_radii.h8bin`; fallback `GenerateEmergencyMockRadii()` writes aligned tuning DTO. DOD: filesystem scan + legacy rationale read. Rejected: silent defaults. Estimate: 0 us hot path.
- [x] Task 02 - No `DistanceSleeper.cs` found; centralized manager owns sleep. DOD: `rg DistanceSleeper`. Rejected: prefab `Update()` sleepers. Estimate: 100-500 us/frame saved in dense debris.
- [x] Task 03 - `PhysicsCullingDTO` is raw fields only; job mutates `IsAsleep` via `UnsafeUtility.ArrayElementAsRef`. DOD: no properties on DTO. Rejected: C# wrappers/properties. Estimate: 5-15 us per 10k pass.
- [x] Task 04 - Added 16-byte `PhysicsCullingTargetWakeRequestSignal` (`uint + float3`). Existing global `WakeRequestSignal` is 48-byte AUP-radius and was retained to avoid signal fragmentation. DOD: explicit layout. Rejected: redefining existing global signal. Estimate: 0 us hot path except queued wakes.
- [x] Task 05 - Added `MockSeismicShockwaveSignal`, `GenerateMockPhysicsBodies(1000)`, and Burst wake job. DOD: mock path compiles. Rejected: direct dependency on Agent 25. Estimate: 0 us normal frame.
- [x] Task 06 - Replaced linear intent kernel with `PhysicsDistanceCullingJobShinobu37` over DTO candidates. DOD: isolated Assembly-CSharp compile clean. Rejected: main-thread distance checks. Estimate: 80-300 us/frame saved at scale.
- [x] Task 07 - Added `NativeParallelMultiHashMap<int,int>` 50m cell hash and 9-cell candidate window. DOD: hash built on slow tick. Rejected: 100k linear Burst sweep as default. Estimate: O(N) to near O(active+visible).
- [x] Task 08 - Added `NativeQueue<int> StateChangedIndices`; state sync drains changed indices only in post-fixed/late completion. DOD: no full Unity API mutation loop. Rejected: calling Sleep/WakeUp for all bodies. Estimate: 50-1000 us/frame saved in churn scenes.
- [x] Task 09 - Added vault `FrozenVelocityDTO`; sleep stores velocity, zeros body, wake restores velocity. DOD: old dampening helper removed. Rejected: velocity loss/damp fake. Estimate: visual correctness, 0 us normal overhead except transitions.
- [x] Task 10 - Added targeted wake queue and frozen impulse merge by instance ID. DOD: dictionary lookup + changed-index enqueue. Rejected: wake-all radius blast for torpedoes. Estimate: 10-200 us/event saved.
- [x] Task 11 - Added six camera frustum planes to Burst job and inner-sphere guard. DOD: camera-relative plane conversion. Rejected: distance-only culling. Estimate: solver savings for offscreen near debris.
- [x] Task 12 - Hardware scale reads `GlobalRegistry.ScalabilityTier`; Low/MX350 multiplies radius sq by 0.25, High/Ultra relax. DOD: tier branch in job setup. Rejected: one-size radii. Estimate: MX350 solver budget protection.
- [x] Task 13 - DTO stores double3 absolute AUP; job subtracts camera AUP to local float3 before frustum/distance. DOD: no absolute float cast. Rejected: runtime transform-only math. Estimate: prevents 100km jitter faults.
- [x] Task 14 - Hysteresis implemented as parallel vault SoA `float` lane to preserve exact 40-byte DTO. DOD: job locks state while age < tuning. Rejected: expanding DTO to 44 bytes. Estimate: avoids Sleep/WakeUp oscillation spikes.
- [x] Task 15 - `CullingFlags` overlays DTO `_pad3` at offset 36; bit 1 exempts bodies. DOD: 40-byte explicit layout retained. Rejected: separate oversized field. Estimate: critical props never sleep.
- [x] Task 16 - DTO/frozen/state/candidate/telemetry/tuning vault lanes use `NativeArrayOptions.UninitializedMemory`; registration overwrites slots. DOD: compile verified. Rejected: clear-memory massive lanes. Estimate: cold allocation savings.
- [x] Task 17 - Added 300-frame `PhysicsCullingFrameTelemetry`; sync >1ms dumps `Docs/AgentLogs/Dump_PHYSICS_CULLING.bin`. DOD: dump writer includes new ring. Rejected: old overseer-only dump name. Estimate: forensic coverage.
- [x] Task 18 - Added `Physics Culling Tuner` EditorWindow with sliders writing vault tuning. DOD: Assembly-CSharp-Editor compile clean. Rejected: ScriptableObject-only recompilation path. Estimate: designer iteration saved.
- [x] Task 19 - Added CSV byte-span parser and editor/dev monitor for `Docs/Modding/physics_culling_profiles.csv`. DOD: no string split/LINQ parser. Rejected: managed CSV libraries. Estimate: 0 us player hot path.
- [x] Task 20 - EditorWindow SceneView gizmo hook draws awake/asleep/hysteresis X-Ray. DOD: no scene search; reads overseer debug DTOs. Rejected: runtime gizmo MonoBehaviour per object. Estimate: editor-only.

## Iteration Log

### Loop 0 - Truth Recovery

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex over `<AGENT_PROMPT id="SHINOBU_37">`.
- Domain confirmed through `Docs/Actual Domains of Project.txt`.
- Existing implementation found in `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`.

### Loop 1 - Tasks 01-05

- Archive and `DistanceSleeper` scans completed.
- Added status/rationale files.
- Established DTO/signal/mock design and preserved existing global `WakeRequestSignal` instead of redefining it.

### Loop 2 - Tasks 06-10

- Added Burst DTO kernel, vault lanes, spatial candidate hash, changed-index queue, frozen velocity storage, collider disable, and targeted wake route.
- Removed obsolete velocity dampening helper.

### Loop 3 - Tasks 11-15

- Added frustum planes, AUP camera-relative subtraction, hardware radius scale, hysteresis SoA lane, and DTO exemption flags.
- Removed `Pack=1` from runtime structs in the touched physics culling owner.

### Loop 4 - Tasks 16-20

- Added uninitialized vault allocation, frame telemetry, dump path, editor tuner, CSV parser, and SceneView gizmo visualizer.

### Loop 5 - Self-Review

- `rg` confirmed no `FindObject*`, no `Vector3.Distance`, no `foreach`, no `.ToString()`, and no `Pack=1` in the touched physics/editor files.
- `git diff --check` passed with line-ending warnings only.

## Verification

- Full `dotnet build Assembly-CSharp.csproj` is blocked by unrelated generated-project state: missing RealtimeCSG source files, missing `project.assets.json` for several package/editor projects, and missing Temp output paths.
- Isolated runtime compile: `dotnet build Assembly-CSharp.csproj -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false /clp:ErrorsOnly` after staging existing `Library/ScriptAssemblies` metadata to `Temp/bin/Debug` passed with `0 Warning(s), 0 Error(s)`.
- Isolated editor compile: `dotnet build Assembly-CSharp-Editor.csproj -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false /clp:ErrorsOnly` passed with `0 Warning(s), 0 Error(s)`.
- Unity Play Mode/profiler/runtime telemetry: PENDING VERIFICATION.
- Polish mandate: COMPLETE.

<SELF_AUDIT>
  <TASK_CHECK>
    <TASK id="01" status="PASS">Archive scan completed; no h8bin found; emergency radii initializer added.</TASK>
    <TASK id="02" status="PASS">No DistanceSleeper script found; centralized manager owns sleep.</TASK>
    <TASK id="03" status="PASS">DTO raw fields only; Burst job uses UnsafeUtility.AsRef path.</TASK>
    <TASK id="04" status="PASS">16-byte targeted wake payload added; existing 48-byte global WakeRequestSignal retained for signal matrix compatibility.</TASK>
    <TASK id="05" status="PASS">Mock seismic signal and Burst wake job added.</TASK>
    <TASK id="06" status="PASS">Burst distance/frustum evaluator over DTO candidates added.</TASK>
    <TASK id="07" status="PASS">50m NativeParallelMultiHashMap spatial hash and 9-cell camera window added.</TASK>
    <TASK id="08" status="PASS">NativeQueue changed-index state sync added.</TASK>
    <TASK id="09" status="PASS">FrozenVelocityDTO Dear Lie added; velocity dampening removed.</TASK>
    <TASK id="10" status="PASS">Targeted wake route by Unity instance id added.</TASK>
    <TASK id="11" status="PASS">Six frustum planes passed to Burst job.</TASK>
    <TASK id="12" status="PASS">Low/MX350 activation radius sq scale is 0.25.</TASK>
    <TASK id="13" status="PASS">AUP subtracts camera double3 before float math.</TASK>
    <TASK id="14" status="PASS">Hysteresis implemented as parallel vault SoA to preserve 40-byte DTO.</TASK>
    <TASK id="15" status="PASS">CullingFlags overlays DTO offset 36 pad slot.</TASK>
    <TASK id="16" status="PASS">Massive vault lanes use UninitializedMemory and explicit slot writes.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry ring and Dump_PHYSICS_CULLING.bin writer added.</TASK>
    <TASK id="18" status="PASS">Physics Culling Tuner EditorWindow added.</TASK>
    <TASK id="19" status="PASS">Byte-span CSV override parser and monitor added.</TASK>
    <TASK id="20" status="PASS">SceneView gizmo X-Ray added.</TASK>
  </TASK_CHECK>
  <ARM64_CHECK>
    PhysicsCullingDTO explicit size 40: offset 0 double3 AUP 24b; offset 24 int InstanceId 4b; offset 28 float ActivationRadiusSq 4b; offset 32 byte IsAsleep 1b; offsets 33-35 pad bytes; offset 36 uint _pad3 / CullingFlags overlay 4b. No Pack=1 in touched culling structs.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    No FindObject*, Vector3.Distance, foreach, ToString, or private NativeArray fields in touched physics/editor files. Runtime tick math uses vault NativeArrays, Native queues/hash maps, and direct indexed loops. CSV file I/O is editor/development-only on file timestamp change; parser itself uses ReadOnlySpan&lt;byte&gt; and no Split/LINQ.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    DTO stores absolute double3 AUP. The Burst evaluator subtracts CameraAbsoluteAup first, then casts the local delta to float3 for distance/frustum dot products.
  </AUP_CHECK>
  <DEAR_LIE_CHECK>
    Distant physics is faked by freezing velocity into FrozenVelocityDTO, zeroing Rigidbody velocity, disabling colliders, and sleeping the body. Wake restores collider state and velocity, so the player sees continuity without offscreen solver cost.
  </DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>
    Existing GlobalRegistry/IPhysicsCullingOverseer surface is reused. Existing SignalBus&lt;WakeRequestSignal&gt; is preserved. Mock seismic/targeted wake payloads are local and unmanaged; no direct dependency on seismic, torpedo, terrain, or submarine classes was introduced.
  </DEPENDENCY_CHECK>
  <BLACKBOX_CHECK>
    PhysicsCullingFrameTelemetry[300] lives in GlobalDataVault; sync spikes above 1 ms write Docs/AgentLogs/Dump_PHYSICS_CULLING.bin.
  </BLACKBOX_CHECK>
</SELF_AUDIT>
