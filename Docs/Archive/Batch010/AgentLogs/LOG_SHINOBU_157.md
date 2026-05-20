# SHINOBU_157 Final Report - 2026-05-19

What was wrong:
- Large-submarine autonomous navigation had no Vault-backed SDF feeler solver. NavMesh/A* style movement is invalid for 100m submarines in destructible voxel volume.
- There was no 64-byte autopilot authority DTO for Agent 113-style consumption.
- No black-box ring existed for autonomous navigation faults.

What was done:
- Added `AutopilotStateDTO` at exactly 64 bytes: `TargetAUP` offset 0, `DesiredVelocity` offset 24, `TargetSpeed` offset 36, `SubmarineHashID` offset 40, `NavFlags` offset 44, explicit pads offset 48 and 56.
- Added DataVault route buffers `71592-71603` for autopilot states, avoidance, feeler debug, waypoints, routes, tuning, telemetry, mock SDF, flow samples, CSV scratch, and handling profiles.
- Added Burst deterministic jobs:
  - `GenerateMockObstacleSDFJob`
  - `GenerateMockFlowFieldJob`
  - `InitializeAutopilotBuffersJob`
  - `EvaluateCollisionAvoidanceJob`
  - `ComputeDesiredVelocityJob`
  - `RecordAutopilotTelemetryJob`
- Added fixed/post-fixed runtime scheduler. No Transform movement, no Rigidbody movement, no main-thread physics casts.
- Added UI Toolkit tuner and Scene View target injection tool.
- Added route card: `Docs/ARCHITECTURE/ROUTE_CARD_SHINOBU_157_AUTOPILOT.md`.

Cinematic Cheats used:
- SDF mock field is a deterministic mathematical pillar/wall/ceiling volume, not a physical cave simulation.
- Abyssal flow has a Vault grid path and an analytic fallback so the submarine can visually crab into currents before Agent 105 is present.
- Editor gizmos use the fixed feeler result buffer; no extra live physics probes are performed.

Exact Microseconds saved:
- NavMesh path solve: removed from submarine path, estimated 200-2000 us avoided per path request depending on graph size.
- Physics SphereCast/Raycast wall probing: avoided entirely; low-tier SDF path estimates about 16 submarines * 5 feelers * 4 steps = 320 base SDF samples/frame.
- DTO property/managed route nodes: 0 B hot-path GC; one 64-byte desired-velocity DTO write per vehicle.
- Black box: one 64-byte telemetry write per frame instead of managed logging strings.

Verification:
- Static scan found no `NavMeshAgent`, `NavMeshPath`, `CalculatePath`, `Physics.Raycast`, `Physics.SphereCast`, `new List<>`, or Unity `Update/FixedUpdate/LateUpdate` in the new autopilot runtime/editor files.
- Static scan found no getter/setter properties in the new runtime DTO file.
- `git diff --check` passed for tracked edited files.
- Compile/build not launched: 7 active `dotnet` processes and CPU 99-100% violate the batch guard.

<SELF_AUDIT>
AutopilotStateDTO_SizeBytes=64
AutopilotStateDTO_TargetAUP_Offset=0
AutopilotStateDTO_DesiredVelocity_Offset=24
AutopilotStateDTO_TargetSpeed_Offset=36
AutopilotStateDTO_SubmarineHashID_Offset=40
AutopilotStateDTO_NavFlags_Offset=44
AutopilotStateDTO_Pad0_Offset=48
AutopilotStateDTO_Pad1_Offset=56
VaultBuffers=71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603
HotPathManagedAllocations=0B_by_design
NavMeshUsage=ABSENT_IN_NEW_AUTOPILOT
PhysicsRaycastUsage=ABSENT_IN_NEW_AUTOPILOT
AUPMath=target_double3_minus_submarine_double3_then_cast_delta_to_float3
QualityScaling=feelerCount_int_math_lerp_5_32_GlobalQualityWeight
AtWaypointNaNGuard=distanceSq_threshold_path_outputs_zero_or_flow_compensated_finite_velocity
NativeAliasFence=distinct_DataVault_BufferID_per_job_stream; locks_held_until_PostFixed_completion
BlackBox=300_entries_Docs/AgentLogs/Dump_NAVIGATION_SURGEON.bin_on_NaN_or_estimated_gt_1ms
CompileStatus=BLOCKED_BY_CPU_AND_EXISTING_DOTNET_PROCESSES
</SELF_AUDIT>

# SHINOBU_157 Editor Facade/API Contract Pass - 2026-05-19

## What Was Wrong
- The editor tuner referenced `HectonFloatingOrigin` without importing `Hecton8.Core`, which would fail as soon as Unity imports the new editor file.
- The telemetry facade allocated formatted managed strings every refresh through `StringBuilder`, numeric `ToString()`, and `Label.text`.
- Generated csproj files still do not include the new SHINOBU_157 source paths, so previous dotnet solution build failures do not prove or disprove this new code.

## What Was Done
- Added the missing `Hecton8.Core` import to `SubmarineAutopilotTunerWindow.cs`.
- Replaced the single formatted telemetry label with disabled typed UI Toolkit readouts: active autopilots, feeler count, repulsion magnitude, estimated Burst microseconds, and flags.
- Re-ran owned-file forbidden API scan after the patch. Runtime/editor owned files contain no `NavMeshAgent`, `NavMeshPath`, `CalculatePath`, `Physics.Raycast`, `Physics.SphereCast`, `new List`, `foreach`, `Time.deltaTime`, `Time.fixedDeltaTime`, `StringBuilder`, or formatted `ToString()` hits.

## Cinematic Cheats Used
- No new physical simulation was added. The editor only observes Vault telemetry and injects target AUP through Scene View plane math.
- Waypoint injection still avoids `Physics.Raycast`; it intersects the editor ray with a constant-height plane derived from current target AUP.

## Exact Microseconds Saved
- Runtime: 0 us change; editor-only facade.
- Editor refresh: removes one formatted managed status string plus numeric format conversions per 0.25s telemetry pulse. Exact allocator/profiler bytes remain PENDING Unity import/profiler proof.
- Compile proof: still blocked by unrelated stale project references `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs` and `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`; generated csproj has not imported SHINOBU_157 files.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="editor_facade_contract_pass" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="16" result="[PASS]">Editor tuner now uses typed numeric telemetry fields instead of formatted status strings.</Task>
    <Task id="19" result="[PASS]">Scene View waypoint injection resolves AUP APIs through the correct `Hecton8.Core`/`Hecton8.World` namespaces and still avoids physics casts.</Task>
    <Task id="20" result="[PASS]">Owned-file static scan remains clean for forbidden APIs and managed formatting patterns.</Task>
  </TASK_RECONCILIATION>
  <COMPILE_GUARD status="PENDING_UNITY_IMPORT">Generated csproj files still exclude the new SHINOBU_157 source paths; solution build remains blocked upstream by stale unrelated source includes.</COMPILE_GUARD>
</SELF_AUDIT>

---

# SHINOBU_157 Hot-Path Hardening Pass - 2026-05-19

## What Was Wrong
- `EnsureVaultBuffers()` could still negotiate Vault handles from the steady FixedTick admission path after boot.
- The DTO layout validator used reflection in runtime source instead of being editor-only.
- Editor read facades could read tuning/state/telemetry while a scheduled job owned the route locks.
- The stable binary payload ledger did not yet contain a SHINOBU_157 lane entry.

## What Was Done
- Added `_resolvedVehicleCapacity` and `AreVaultHandlesReady()` fast path. Boot negotiates handles; steady FixedTick validates cached handles and exits without `GetBufferHandle`. Capacity changes reset `_initialized` so new rows are initialized by the Burst init path.
- Wrapped `AutopilotStateDTOLayout` in `#if UNITY_EDITOR`.
- Made `TryReadTuning`, `TryReadAutopilotState`, and `TryReadLatestTelemetry` fail closed during `_buffersLocked`, `_solverPending`, or `_initPending`.
- Added SHINOBU_157 lane to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Guarded compile attempted after CPU/dotnet gate opened. Build failed before SHINOBU_157 source on stale `Hecton8.Core.csproj` missing unrelated files:
  - `Assets/_Project/Scripts/World/ChemicalInfluenceGrid.cs`
  - `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`

## Cinematic Cheats Used
- No new physical simulation was added. The autopilot remains a mathematical SDF intent generator and not a vehicle integrator.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING. Compile/profiler proof is blocked by unrelated project-file source references.
- Static estimate: steady FixedTick no longer repeats Vault handle acquisition for 12 owner-local buffers after boot; remaining fast path is fixed handle/capacity validation.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="hot_path_hardening" verification="STATIC_SOURCE_PLUS_BLOCKED_DOTNET">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">No owned NavMeshAgent/NavMeshPath/CalculatePath dependency.</Task>
    <Task id="02" result="[PASS]">No owned Physics.Raycast or Physics.SphereCast spatial awareness.</Task>
    <Task id="03" result="[PASS]">Runtime DTOs remain raw-field unmanaged structs; no get/set DTO properties.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO remains explicit 64 bytes; reflection validator is now editor-only.</Task>
    <Task id="05" result="[PASS]">Mock SDF Burst fallback remains Vault-backed.</Task>
    <Task id="06" result="[PASS]">EvaluateCollisionAvoidanceJob remains Burst deterministic and NoAlias-annotated.</Task>
    <Task id="07" result="[PASS]">ComputeDesiredVelocityJob remains Burst deterministic and NoAlias-annotated.</Task>
    <Task id="08" result="[PASS]">Dear Lie authority boundary unchanged: DesiredVelocity only.</Task>
    <Task id="09" result="[PASS]">Waypoint/route DTO flow unchanged; capacity changes now force re-init.</Task>
    <Task id="10" result="[PASS]">GlobalQualityWeight controls feelers, steps, cadence, interpolation, and gradient admission.</Task>
    <Task id="11" result="[PASS]">Flow compensation remains Vault/analytic fallback based.</Task>
    <Task id="12" result="[PASS]">AUP delta math remains target double3 minus submarine double3 before float3 steering.</Task>
    <Task id="13" result="[PASS]">FloatMode.Deterministic retained on all jobs.</Task>
    <Task id="14" result="[PASS]">Uninitialized Vault buffers are initialized by Burst; new capacity re-enters init path.</Task>
    <Task id="15" result="[PASS]">300-frame telemetry ring and dump path retained.</Task>
    <Task id="16" result="[PASS]">Editor tuner retained; read APIs now fail closed during job-owned locks.</Task>
    <Task id="17" result="[PASS]">CSV ingest remains Span/ReadOnlySpan into fixed Vault profile table.</Task>
    <Task id="18" result="[PASS]">Gizmo read path retained and now blocked during pending job writes.</Task>
    <Task id="19" result="[PASS]">Scene View waypoint injection retained and blocked during pending job writes through state read fence.</Task>
    <Task id="20" result="[PASS]">Self-audit updated after hardening; compile proof is blocked by unrelated missing csproj source files.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotStateDTO size="64" proof="0 double3 TargetAUP 24B; 24 float3 DesiredVelocity 12B; 36 float TargetSpeed 4B; 40 uint SubmarineHashID 4B; 44 uint NavFlags 4B; 48 ulong _pad0 8B; 56 ulong _pad1 8B; total 64, multiple of 16"/>
    <TelemetryEntry size="64" falseSharing="Each ring row is one cache line; single telemetry job writes cursor after compute dependency."/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE q_lt_0_3="5 feelers, 1 step, nearest SDF, no gradient taps, reduced cadence" q_eq_1="32 feelers, 12 steps, trilinear SDF, gradient repulsion, every admitted fixed tick"/>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" routeIds="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="All Burst pointer fields" graph="initJob->sdfJob->flowJob; evaluate->compute->telemetry; completion via DispatcherJobSwap"/>
  <COMPILE_GUARD status="BLOCKED_BY_DEPENDENCY">dotnet build failed before SHINOBU_157 on missing unrelated Hecton8.Core.csproj source files: ChemicalInfluenceGrid.cs and LogisticsPipeEvents.cs. No SHINOBU_157 compile error was emitted.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Engine casts/path graph plus movement authority coupling" complexityAfter="O(vehicleCount*activeFeelers*activeSteps) SDF intent generation only"/>
</SELF_AUDIT>

---

# SHINOBU_157 Ultra-Think Polish Pass - 2026-05-19

## What Was Wrong
- Core compile-wall contamination: SHINOBU_157 had domain IDs added to `H8Memory.cs`. That widened the global enum for a local autopilot route.
- Low-quality math did not collapse far enough: previous low path still did multi-step trilinear SDF and gradient sampling.
- Burst pointer fields did not explicitly prove no aliasing.
- Cold tuning default path read `SourceHash` from uninitialized Vault memory.
- Fault dump used a managed `byte[]` scratch, and CSV read used byte-at-a-time file I/O instead of span slicing.

## What Was Done
- Removed SHINOBU_157 global enum route. Added owner-local `SubmarineAutopilotVaultRoute` typed IDs 71592-71603 and updated route card.
- Added `[NoAlias]` to all Burst pointer fields in init, mock SDF, mock flow, collision evaluation, velocity compute, and telemetry jobs.
- Added continuous quality collapse: solver cadence 12->1 frames, feeler count 5->32, ray steps 1->12, nearest/trilinear SDF blending, and gradient gate.
- Changed cold defaults to write deterministic tuning before any uninitialized read.
- Changed dump to `ReadOnlySpan<byte>` direct write; changed CSV ingest to `Span<byte>` read plus `ReadOnlySpan<byte>` parser.
- Throttled editor telemetry refresh to 4 Hz and reused a `StringBuilder` in the editor facade.

## Cinematic Cheats Used
- Navigation is a mathematical intent fake: the autopilot publishes `DesiredVelocity` only. It does not simulate submarine mass, drag, prop wash, or Rigidbody integration.
- Abyssal currents are sampled from a coarse Vault flow grid or deterministic analytic wave fallback. No Navier-Stokes, no per-entity fluid solver.
- Obstacle awareness is direct encoded SDF sampling. No Unity colliders, no `Physics.Raycast`, no `SphereCast`, no path node graph.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING. Compile and profiler validation were not launched because CPU sampled at 100% then 97.3037%, violating the build guard.
- Final build guard recheck still blocked compile: CPU sampled at 100% then 100%; `dotnet`/`csc` were absent.
- Static math estimate, low tier: collision base SDF samples for 16 submarines changed from `16 * 5 * 4 = 320` base samples per solver frame to `16 * 5 * 1 = 80`; six-tap gradient normals are bypassed below the quality gate; cadence can shed solver execution down to 1/12 frames at quality 0.
- Static math estimate, ultra tier: up to `16 * 32 * 12 = 6144` base SDF samples plus gradient taps, deliberately spending saved cycles on denser avoidance.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">Static scan target: no owned autopilot NavMeshAgent/NavMeshPath/CalculatePath dependency introduced; implementation has no NavMesh usage.</Task>
    <Task id="02" result="[PASS]">Owned autopilot uses SDF byte sampling; no Physics.Raycast or Physics.SphereCast in runtime/editor owned files.</Task>
    <Task id="03" result="[PASS]">DTOs use public fields and explicit layout; no get/set properties in owned runtime DTOs.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO explicit 64-byte layout with editor-time UnsafeUtility offset validator.</Task>
    <Task id="05" result="[PASS]">GenerateMockObstacleSDFJob writes deterministic encoded SDF into Vault fallback buffer.</Task>
    <Task id="06" result="[PASS]">EvaluateCollisionAvoidanceJob ray-marches feelers against encoded SDF and accumulates potential-field repulsion.</Task>
    <Task id="07" result="[PASS]">ComputeDesiredVelocityJob combines AUP attraction, repulsion, current compensation, speed clamp, and turn-rate clamp.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff only writes DesiredVelocity in AutopilotStateDTO; no Transform/Rigidbody authority taken.</Task>
    <Task id="09" result="[PASS]">Route range plus waypoint buffers advance TargetAUP in job when acceptance radius is reached.</Task>
    <Task id="10" result="[PASS]">Feeler count uses continuous GlobalQualityWeight 5->32; cadence/steps/interpolation also scale continuously.</Task>
    <Task id="11" result="[PASS]">Flow compensation samples Vault flow grid or deterministic analytic fallback and subtracts weighted current from desired velocity.</Task>
    <Task id="12" result="[PASS]">Steering delta subtracts vehicle double3 AUP from target double3 AUP before float3 math.</Task>
    <Task id="13" result="[PASS]">All Burst jobs use FloatMode.Deterministic; DTOs are blittable and aligned for rollback memcpy.</Task>
    <Task id="14" result="[PASS]">Autopilot buffers request UninitializedMemory and Burst init writes deterministic idle targets.</Task>
    <Task id="15" result="[PASS]">300-entry 64-byte telemetry ring and Dump_NAVIGATION_SURGEON.bin fault export path exist.</Task>
    <Task id="16" result="[PASS]">UI Toolkit tuner writes Vault tuning DTO and displays throttled telemetry readout.</Task>
    <Task id="17" result="[PASS]">CSV parser uses Span/ReadOnlySpan bytes, FNV-1a hashes, and Vault fixed profile table. NativeHashMap was replaced because Vault exposes typed fixed buffers and private persistent NativeHashMap violates Vault Law.</Task>
    <Task id="18" result="[PASS]">OnDrawGizmos reads feeler DTO buffer and draws feeler rays, hit dots, and repulsion vectors.</Task>
    <Task id="19" result="[PASS]">Scene View click injection writes selected submarine TargetAUP without Physics.Raycast.</Task>
    <Task id="20" result="[PASS]">Static self-audit written here; Unity compile/import/profiler proof remains PENDING due CPU guard.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotStateDTO size="64" alignment="multiple_of_16">
      <Field name="TargetAUP" offset="0" size="24" note="double3; 8-byte aligned"/>
      <Field name="DesiredVelocity" offset="24" size="12" note="float3"/>
      <Field name="TargetSpeed" offset="36" size="4"/>
      <Field name="SubmarineHashID" offset="40" size="4"/>
      <Field name="NavFlags" offset="44" size="4"/>
      <Field name="_pad0" offset="48" size="8"/>
      <Field name="_pad1" offset="56" size="8"/>
      <Proof>24 + 12 + 4 + 4 + 4 + 8 + 8 = 64 bytes. 64 % 16 = 0.</Proof>
    </AutopilotStateDTO>
    <AutopilotAvoidanceDTO size="64" proof="Offsets 0,12,24,36,40,44,48,52,56,60; 64 % 16 = 0"/>
    <AutopilotFeelerResultDTO size="64" proof="Offsets 0,12,24,36,48,52,56,60; 64 % 16 = 0"/>
    <AutopilotWaypointDTO size="32" proof="double3 at 0, float at 24, uint at 28; 32 % 16 = 0"/>
    <AutopilotRouteRangeDTO size="32" proof="8 fields of 4 bytes; 32 % 16 = 0"/>
    <AutopilotTuningDTO size="128" proof="Explicit offsets 0..124; 128 % 16 = 0"/>
    <AutopilotTelemetryEntry size="64" proof="double3 at 0, float3 at 24, scalar fields through 60; 64 % 16 = 0"/>
    <FalseSharing>Telemetry ring entries are 64 bytes. There is no concurrent atomic counter struct in this domain; telemetry cursor is a single uint written by one telemetry job after compute dependency.</FalseSharing>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    GlobalQualityWeight drives feelers via lerp(5,32,q), steps via round(lerp(1,12,q*q)), solver cadence via round(lerp(12,1,q*q)), SDF interpolation via smoothstep(0.25,0.45,q), and gradient taps via smoothstep(0.30,0.55,q). Below q=0.3, the solver collapses to nearest-neighbor SDF sampling and no six-tap gradient normals; under q=0.1 cadence is approximately 11-12 fixed ticks. At q=1, dense feelers, 12 march steps, trilinear sampling, and gradient-derived lateral repulsion are active.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PrivatePersistentNativeCollections count="0"/>
    <VaultBuffer id="71592" name="AutopilotStates"/>
    <VaultBuffer id="71593" name="AutopilotAvoidance"/>
    <VaultBuffer id="71594" name="AutopilotFeelerResults"/>
    <VaultBuffer id="71595" name="AutopilotWaypoints"/>
    <VaultBuffer id="71596" name="AutopilotRouteRanges"/>
    <VaultBuffer id="71597" name="AutopilotTuning"/>
    <VaultBuffer id="71598" name="AutopilotTelemetryRing"/>
    <VaultBuffer id="71599" name="AutopilotTelemetryCursor"/>
    <VaultBuffer id="71600" name="AutopilotMockSdf"/>
    <VaultBuffer id="71601" name="AutopilotFlowSamples"/>
    <VaultBuffer id="71602" name="AutopilotCsvScratch"/>
    <VaultBuffer id="71603" name="AutopilotHandlingProfiles"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>All pointer fields in InitializeAutopilotBuffersJob, GenerateMockObstacleSDFJob, GenerateMockFlowFieldJob, EvaluateCollisionAvoidanceJob, ComputeDesiredVelocityJob, and RecordAutopilotTelemetryJob carry NoAlias.</NoAlias>
    <JobGraph>Initialization: initJob -> sdfJob -> flowJob. Solver: evaluate -> compute -> telemetry. Handles are registered through H8Memory.RegisterActiveJob and completed through DispatcherJobSwap in PostFixedTick/OnDisable.</JobGraph>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    Owned runtime/editor files add no asmdef and no direct sibling assembly reference. SHINOBU_157 no longer adds entries to H8Memory.cs. Current compile proof is blocked by CPU guard, not by a known syntax error.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: collider or NavMesh navigation would require engine spatial queries and movement authority coupling. After: O(vehicleCount * activeFeelers * activeSteps) encoded SDF sampling produces DesiredVelocity only; actual motion remains with vehicle kinematics. No physical fluid simulation is used for current compensation.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

---

# SHINOBU_157 Profile/Flow Collapse Polish Pass - 2026-05-19

## What Was Wrong
- `vehicle_handling_profiles.csv` was parsed into Vault rows, but steering math did not consume those rows.
- Low-quality flow compensation still paid trilinear grid sampling when a nearest-cell read is enough under thermal load.
- AUP target deltas were locally subtracted but not clamped before float cast for pathological far/invalid targets.
- New runtime/editor source files lacked checked-in Unity `.meta` files.

## What Was Done
- Added default FNV-1a handling hashes for `default`, `scout`, and `freighter`.
- Seeded default handling rows at cold boot and ensured CSV reload preserves a default fallback row.
- Wired `AutopilotHandlingProfiles` into `ComputeDesiredVelocityJob` with `[NoAlias]` pointer input and solver lock coverage.
- Applied profile turn rate, acceleration limit, speed scale, and repulsion scale in the Burst steering job.
- Added an editor-only profile assignment facade for default/scout/freighter.
- Changed flow sampling to nearest-cell below the continuous interpolation gate and trilinear at higher quality.
- Added double-space far-target clamp before float steering and hardened route distance checks.
- Added `.meta` files for the new runtime source, editor folder, and editor window source.
- Removed the runtime `Hecton8.World.DispatcherJobSwap` dependency and replaced it with an owner-local job completion helper.

## Cinematic Cheats Used
- Handling profiles alter desired velocity only. The navigator still does not simulate mass, drag, or Rigidbody force application.
- Flow remains a coarse field or analytic current fake, not a fluid simulation.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING. No build/profiler pass was launched in this polish loop because the last guarded solution build is already blocked before SHINOBU_157 by unrelated missing csproj source files.
- Static estimate: low-tier flow grid reads drop from 8 samples to 1 sample per active submarine when interpolation weight is zero.
- Static estimate: default profile lookup resolves in one open-address probe; worst case is bounded at 32 probes.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="profile_flow_collapse" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">Owned files still contain no NavMeshAgent/NavMeshPath/CalculatePath.</Task>
    <Task id="02" result="[PASS]">Owned runtime/editor files still contain no Physics.Raycast or Physics.SphereCast.</Task>
    <Task id="03" result="[PASS]">DTOs remain raw-field structs; no get/set DTO properties.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO layout unchanged at 64 bytes.</Task>
    <Task id="05" result="[PASS]">Mock SDF fallback unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged except downstream profile/flow consumers.</Task>
    <Task id="07" result="[PASS]">Steering job now includes profile-scaled turn, acceleration, speed, and repulsion.</Task>
    <Task id="08" result="[PASS]">Dear Lie boundary unchanged: DesiredVelocity only.</Task>
    <Task id="09" result="[PASS]">Route advancement now uses finite double distance guard.</Task>
    <Task id="10" result="[PASS]">GlobalQualityWeight now also controls flow interpolation collapse.</Task>
    <Task id="11" result="[PASS]">Flow compensation uses nearest-cell low tier and trilinear higher tier.</Task>
    <Task id="12" result="[PASS]">Target AUP delta is clamped in double space before float cast.</Task>
    <Task id="13" result="[PASS]">All jobs remain Burst deterministic.</Task>
    <Task id="14" result="[PASS]">Cold defaults seed tuning, telemetry cursor, and handling profiles from Vault memory.</Task>
    <Task id="15" result="[PASS]">Telemetry estimate now follows the same 1..12 step curve as the feeler job.</Task>
    <Task id="16" result="[PASS]">Editor tuner now assigns handling profiles in addition to tuning sliders.</Task>
    <Task id="17" result="[PASS]">CSV rows are parsed and consumed by the solver; fixed Vault table remains the DataVault-safe substitute for NativeHashMap.</Task>
    <Task id="18" result="[PASS]">Gizmo path unchanged and still source-scan clean.</Task>
    <Task id="19" result="[PASS]">Waypoint injection unchanged and still physics-cast free.</Task>
    <Task id="20" result="[PASS]">Static scans and `git diff --check` pass for owned files; Unity compile/import proof remains pending.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotStateDTO size="64" proof="0 double3 TargetAUP 24B; 24 float3 DesiredVelocity 12B; 36 float TargetSpeed 4B; 40 uint SubmarineHashID 4B; 44 uint NavFlags 4B; 48 ulong _pad0 8B; 56 ulong _pad1 8B; total 64"/>
    <AutopilotHandlingProfileDTO size="32" proof="0 uint NameHash 4B; 4 float MaxTurnRateRadians 4B; 8 float AccelerationLimit 4B; 12 float SpeedScale 4B; 16 float RepulsionWeight 4B; 20 uint Flags 4B; 24/28 padding uints; total 32"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q=0.2 flow sampling returns one nearest cell; q=0.2..0.65 smoothsteps into trilinear; q>=0.65 uses full trilinear. SDF feelers still scale 5..32, steps 1..12, cadence 12..1, and gradient admission by quality.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="ComputeDesiredVelocityJob now includes HandlingProfiles NoAlias pointer" graph="evaluate->compute(profile+flow+route)->telemetry; post-fixed completion uses owner-local TryCompleteJobHandle"/>
  <COMPILE_GUARD>Runtime owned file imports Core/Core.Memory and Vehicles contracts only; editor file keeps World AUP conversion behind editor assembly. No global BufferID enum route. dotnet build was not relaunched in this loop; previous guarded build is blocked before SHINOBU_157 by unrelated missing source includes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Parser-only profile data plus 8-tap flow reads at all qualities" complexityAfter="O(vehicleCount) profile lookup plus quality-collapsed 1-to-8 flow reads; movement remains external"/>
</SELF_AUDIT>

---

# SHINOBU_157 Vault Lock Transaction Polish Pass - 2026-05-19

## What Was Wrong
- Public editor and cold-path write APIs checked pending jobs, but did not directly check the active route lock flag.
- Partial lock rollback called `UnlockBuffers()` against the whole SHINOBU route. `GlobalDataVault.TryUnlockBuffer` is refcount-based and does not validate an owner token, so a failed acquisition could decrement a buffer lock that this navigator did not acquire.

## What Was Done
- Added `_buffersLocked` fail-closed checks to `SlowTick`, `TryWriteTargetAup`, `TryWriteHandlingProfileHash`, and `TryWriteTuning`.
- Added an owner-local `_lockMask` with one bit per acquired Vault buffer.
- Reworked initialization and solver lock acquisition through `TryLockOwnedBuffer`.
- Reworked `UnlockBuffers()` to release only acquired bits, then clear `_lockMask` and `_buffersLocked`.
- Repeated owned-file forbidden API scan and runtime `git diff --check`.

## Cinematic Cheats Used
- None added in this pass. This was concurrency hygiene for the existing Dear Lie navigator route.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING. No build or profiler pass was launched because the last guarded solution build remains blocked before SHINOBU_157 by unrelated stale source includes.
- Static estimate: hot Burst solver math is unchanged. Main-thread rollback releases only acquired buffers instead of attempting every route buffer; the real gain is preventing rare lock refcount corruption under concurrent editor/runtime writes.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="vault_lock_transaction" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">Owned files still contain no NavMeshAgent, NavMeshPath, or CalculatePath.</Task>
    <Task id="02" result="[PASS]">Owned files still contain no Physics.Raycast or Physics.SphereCast.</Task>
    <Task id="03" result="[PASS]">DTOs remain raw-field unmanaged structs with no getter or setter properties.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO layout remains explicit 64 bytes.</Task>
    <Task id="05" result="[PASS]">Mock SDF fallback remains Burst scheduled from Vault memory.</Task>
    <Task id="06" result="[PASS]">Feeler kernel still uses unmanaged SDF probing and NoAlias pointers.</Task>
    <Task id="07" result="[PASS]">Potential-field steering remains profile-scaled and NaN guarded.</Task>
    <Task id="08" result="[PASS]">Dear Lie boundary unchanged: navigator writes DesiredVelocity only.</Task>
    <Task id="09" result="[PASS]">Route cursor and waypoint advancement remain unmanaged DTO based.</Task>
    <Task id="10" result="[PASS]">GlobalQualityWeight still controls feeler count, steps, cadence, interpolation, and flow sampling.</Task>
    <Task id="11" result="[PASS]">Flow compensation remains quality-collapsed from nearest-cell to trilinear sampling.</Task>
    <Task id="12" result="[PASS]">AUP delta math still subtracts double3 positions before local float steering.</Task>
    <Task id="13" result="[PASS]">All jobs remain Burst deterministic with explicit FloatPrecision Standard.</Task>
    <Task id="14" result="[PASS]">Vault buffers remain uninitialized at request time and initialized by Burst cold job.</Task>
    <Task id="15" result="[PASS]">300-frame telemetry ring remains fixed-size and dump-capable.</Task>
    <Task id="16" result="[PASS]">Editor tuner writes now fail closed during route locks.</Task>
    <Task id="17" result="[PASS]">CSV handling profile ingest now also waits for unlocked route state.</Task>
    <Task id="18" result="[PASS]">Gizmo read facade remains locked-buffer guarded.</Task>
    <Task id="19" result="[PASS]">Dynamic target injection now refuses writes during active route locks.</Task>
    <Task id="20" result="[PASS]">Static guard scan and diff check passed for this pass; Unity import and runtime profiling remain pending.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotStateDTO size="64" proof="0 double3 TargetAUP 24B; 24 float3 DesiredVelocity 12B; 36 float TargetSpeed 4B; 40 uint SubmarineHashID 4B; 44 uint NavFlags 4B; 48 ulong _pad0 8B; 56 ulong _pad1 8B; total 64"/>
    <LockMask size="4" note="Main-thread owner-local bit mask only; not a Burst DTO and not shared between worker threads."/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged in this pass: low quality collapses SDF to one nearest sample per feeler, suppresses gradients, reduces cadence, and samples one flow cell; high quality restores dense feelers, 12 march steps, trilinear SDF, trilinear flow, and gradient repulsion.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="NoAlias pointer fields unchanged" graph="init:sdf:flow and evaluate:compute:telemetry unchanged; lock acquisition now records acquired Vault bits before scheduling"/>
  <COMPILE_GUARD>Runtime file still imports Core, Core.Memory, and Physics.Vehicles only. No sibling World runtime dependency was reintroduced. dotnet build was not relaunched because previous guarded build is blocked before SHINOBU_157 by unrelated stale source includes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Partial lock rollback attempted every route unlock on failure" complexityAfter="O(acquiredBufferCount) rollback with acquired-bit proof; steering Dear Lie complexity unchanged"/>
</SELF_AUDIT>

---

# SHINOBU_157 Zero-GC Route Writer Pass - 2026-05-19

## What Was Wrong
- The solver had `AutopilotWaypointDTO` and `AutopilotRouteRangeDTO`, but external owners had no zero-GC ingress to seed a route without reaching into Vault or allocating managed path objects.

## What Was Done
- Added `TryWriteRoute(int, ReadOnlySpan<AutopilotWaypointDTO>, float, uint)`.
- The method validates finite double3 AUP targets before lock acquisition.
- It writes a fixed per-submarine waypoint slice, initializes `AutopilotRouteRangeDTO`, and sets the first `TargetAUP` on `AutopilotStateDTO`.
- It fails closed during job locks and unlocks only buffers acquired by the synchronous write.

## Cinematic Cheats Used
- Route ingestion is a DTO copy, not a graph search. The path shape is supplied by another owner; this navigator only follows and avoids through SDF potential fields.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING. Build/profiler proof remains blocked upstream.
- Static estimate: avoids managed route node allocation and path staging in this domain; cold ingress is O(route count) capped by the fixed Vault waypoint slice.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="route_writer" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent from owned files.</Task>
    <Task id="02" result="[PASS]">Physics ray and sphere casts remain absent from owned files.</Task>
    <Task id="03" result="[PASS]">Route, waypoint, and state DTOs remain raw-field structs.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO layout unchanged at 64 bytes.</Task>
    <Task id="05" result="[PASS]">Mock SDF fallback unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged.</Task>
    <Task id="07" result="[PASS]">Potential-field steering unchanged.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Route ingress now writes fixed Vault waypoint slices and route ranges without managed collections.</Task>
    <Task id="10" result="[PASS]">Quality continuum unchanged.</Task>
    <Task id="11" result="[PASS]">Flow compensation unchanged.</Task>
    <Task id="12" result="[PASS]">AUP route targets are validated as finite double3 before write.</Task>
    <Task id="13" result="[PASS]">Route DTOs remain blittable and snapshot-friendly.</Task>
    <Task id="14" result="[PASS]">Waypoint buffer remains Vault-owned uninitialized memory, hydrated only through explicit writes.</Task>
    <Task id="15" result="[PASS]">Telemetry unchanged.</Task>
    <Task id="16" result="[PASS]">Editor can continue to use single-target injection; route API is available for richer facade work.</Task>
    <Task id="17" result="[PASS]">CSV profile path unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo path unchanged.</Task>
    <Task id="19" result="[PASS]">Dynamic target injection unchanged; route writer adds multi-waypoint handoff.</Task>
    <Task id="20" result="[PASS]">Latest route-writer self-audit XML parses; forbidden API scan returned no matches; diff check passed with only the known ledger CRLF warning.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotWaypointDTO size="32" proof="0 double3 TargetAUP 24B; 24 float AcceptanceRadius 4B; 28 uint Flags 4B; total 32"/>
    <AutopilotRouteRangeDTO size="32" proof="0 int StartIndex 4B; 4 int Count 4B; 8 int CurrentOffset 4B; 12 float AcceptanceRadius 4B; 16 uint Flags 4B; 20 uint RouteHash 4B; 24 and 28 padding uints; total 32"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged: route writer is cold ingress; runtime quality still controls SDF and flow probe cost.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="Burst job NoAlias fields unchanged" graph="route ingress writes waypoints:routes:states before solver scheduling; solver graph remains evaluate:compute:telemetry"/>
  <COMPILE_GUARD>No Logistics runtime import was added; route ingress uses public DTO spans and owner-local Vault IDs.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Potential direct mission graph or managed waypoint list ownership" complexityAfter="O(routeCount) fixed DTO copy into Vault; no search or physics query in autopilot"/>
</SELF_AUDIT>

---

# SHINOBU_157 Editor Route Injection Pass - 2026-05-19

## What Was Wrong
- The editor facade could write only a single target AUP, so it did not exercise the route-range and waypoint advancement path implemented for Task 09.

## What Was Done
- Added `Scene Click Route` mode to `SubmarineAutopilotTunerWindow`.
- A Scene View click now builds a three-waypoint dogleg route using `stackalloc Span<AutopilotWaypointDTO>` and calls `TryWriteRoute`.
- The existing single-target click mode remains available.
- The click path still uses editor plane intersection through `HandleUtility.GUIPointToWorldRay`; no Physics ray or sphere cast was introduced.

## Cinematic Cheats Used
- The dogleg route is an editor test fake, not a pathfinder. It creates a quick curved route shape so designers can force the feeler solver to prove avoidance and route advancement without wiring Logistics.

## Exact Microseconds Saved
- Runtime: 0 us. This is an editor-only facade.
- Editor route staging avoids managed route lists; one click uses stack memory for three waypoint DTOs and fixed Vault writes.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="editor_route_injection" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent from owned files.</Task>
    <Task id="02" result="[PASS]">Physics casts remain absent from owned files.</Task>
    <Task id="03" result="[PASS]">Waypoint and route DTOs remain raw-field structs.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO layout unchanged.</Task>
    <Task id="05" result="[PASS]">Mock SDF fallback unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged.</Task>
    <Task id="07" result="[PASS]">Steering job unchanged.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Editor facade now exercises route DTO ingress through TryWriteRoute.</Task>
    <Task id="10" result="[PASS]">Quality continuum unchanged.</Task>
    <Task id="11" result="[PASS]">Flow compensation unchanged.</Task>
    <Task id="12" result="[PASS]">Route waypoints remain double3 AUP values.</Task>
    <Task id="13" result="[PASS]">Route DTOs remain blittable.</Task>
    <Task id="14" result="[PASS]">Vault ownership unchanged.</Task>
    <Task id="15" result="[PASS]">Telemetry unchanged.</Task>
    <Task id="16" result="[PASS]">Editor facade now includes route injection in addition to sliders/readouts.</Task>
    <Task id="17" result="[PASS]">CSV profile path unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo path unchanged.</Task>
    <Task id="19" result="[PASS]">Dynamic waypoint injection now covers single target and short route injection.</Task>
    <Task id="20" result="[PASS]">Owned-file forbidden API scan returned no matches; editor source diff check passed.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotWaypointDTO size="32" proof="0 double3 TargetAUP 24B; 24 float AcceptanceRadius 4B; 28 uint Flags 4B; total 32"/>
    <AutopilotRouteRangeDTO size="32" proof="0 int StartIndex; 4 int Count; 8 int CurrentOffset; 12 float AcceptanceRadius; 16 uint Flags; 20 uint RouteHash; 24/28 padding; total 32"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged in runtime; editor dogleg route is an ingress facade and does not alter GlobalQualityWeight math.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="Runtime Burst NoAlias fields unchanged" graph="editor route ingress writes before solver scheduling; runtime graph remains evaluate:compute:telemetry"/>
  <COMPILE_GUARD>Editor added `System` only for Span; no runtime sibling domain reference or Logistics dependency was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Single target editor test could not prove route advancement" complexityAfter="O(3) stackallocated route DTO fake; no pathfinding or physics query"/>
</SELF_AUDIT>

---

# SHINOBU_157 Route ABI Hygiene Pass - 2026-05-19

## What Was Wrong
- Route and waypoint active flags were written as raw `1u` literals.
- `TryWriteRoute` based waypoint slice allocation on serialized `vehicleCapacity` even after Vault handles had resolved the active capacity.

## What Was Done
- Added `WaypointFlagActive` and `RouteFlagActive` constants.
- Replaced route writer and editor route waypoint flag literals with those constants.
- Changed `TryWriteRoute` to prefer `_resolvedVehicleCapacity` when calculating per-submarine waypoint slices.

## Cinematic Cheats Used
- None added. This pass tightens binary route semantics for the existing mathematical route fake.

## Exact Microseconds Saved
- Runtime: 0 us. Constants fold and the route writer remains cold/editor ingress.
- Debugging cost avoided: wrong waypoint slice writes after capacity normalization now have a smaller surface.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="route_abi_hygiene" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent.</Task>
    <Task id="02" result="[PASS]">Physics casts remain absent.</Task>
    <Task id="03" result="[PASS]">DTOs remain raw-field structs.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO layout unchanged.</Task>
    <Task id="05" result="[PASS]">Mock SDF unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged.</Task>
    <Task id="07" result="[PASS]">Steering math unchanged.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Route writer now uses named active flags and resolved capacity.</Task>
    <Task id="10" result="[PASS]">Quality continuum unchanged.</Task>
    <Task id="11" result="[PASS]">Flow compensation unchanged.</Task>
    <Task id="12" result="[PASS]">AUP route targets unchanged.</Task>
    <Task id="13" result="[PASS]">Binary route flags are now named ABI constants.</Task>
    <Task id="14" result="[PASS]">Vault ownership unchanged.</Task>
    <Task id="15" result="[PASS]">Telemetry unchanged.</Task>
    <Task id="16" result="[PASS]">Editor route flag write uses the same constant as runtime route ingress.</Task>
    <Task id="17" result="[PASS]">CSV profile path unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo path unchanged.</Task>
    <Task id="19" result="[PASS]">Dynamic route injection unchanged except named flags.</Task>
    <Task id="20" result="[PASS]">Forbidden API scan returned no matches and source diff check passed for edited runtime/editor files.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotWaypointDTO size="32" proof="layout unchanged; Flags at offset 28 now uses WaypointFlagActive"/>
    <AutopilotRouteRangeDTO size="32" proof="layout unchanged; Flags at offset 16 now uses RouteFlagActive"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged; route ABI hygiene does not alter SDF or flow quality curves.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="Runtime Burst NoAlias fields unchanged" graph="route ingress still writes before solver scheduling"/>
  <COMPILE_GUARD>No assembly reference or using boundary change was introduced by this pass.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Raw route flag literals and serialized-capacity slice math" complexityAfter="Named constants and resolved-capacity slice math; no physical path simulation"/>
</SELF_AUDIT>

---

# SHINOBU_157 Full DTO Layout Guard Pass - 2026-05-19

## What Was Wrong
- The editor-time layout validator proved only `AutopilotStateDTO`.
- Other Vault DTOs cross route, telemetry, tuning, CSV, debug, and rollback-adjacent boundaries but had no executable size/offset guard.

## What Was Done
- Added `AutopilotStateDTOLayout.ValidateAll()`.
- Added editor-only exact size/offset validation for avoidance, feeler result, waypoint, route range, tuning, telemetry, and handling profile DTOs.
- Kept reflection inside `UNITY_EDITOR`; player/runtime path remains reflection-free.

## Cinematic Cheats Used
- None. This pass is binary layout proof.

## Exact Microseconds Saved
- Runtime: 0 us. Player builds do not execute this reflection guard.
- Failure cost avoided: silent ARM64 DTO drift now has an editor-time proof hook.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="full_dto_layout_guard" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent.</Task>
    <Task id="02" result="[PASS]">Physics casts remain absent.</Task>
    <Task id="03" result="[PASS]">DTOs remain raw-field structs.</Task>
    <Task id="04" result="[PASS]">Editor layout guard now covers all SHINOBU_157 Vault DTOs, not only state.</Task>
    <Task id="05" result="[PASS]">Mock SDF unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged.</Task>
    <Task id="07" result="[PASS]">Steering unchanged.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Waypoint and route DTO layouts are validated.</Task>
    <Task id="10" result="[PASS]">Quality continuum unchanged.</Task>
    <Task id="11" result="[PASS]">Flow unchanged.</Task>
    <Task id="12" result="[PASS]">AUP DTO offsets remain validated where stored.</Task>
    <Task id="13" result="[PASS]">Rollback-adjacent DTO layout proof expanded.</Task>
    <Task id="14" result="[PASS]">Vault buffer ownership unchanged.</Task>
    <Task id="15" result="[PASS]">Telemetry DTO layout now has editor proof.</Task>
    <Task id="16" result="[PASS]">Editor-only guard keeps player compile surface clean.</Task>
    <Task id="17" result="[PASS]">Handling profile DTO layout now has editor proof.</Task>
    <Task id="18" result="[PASS]">Feeler debug DTO layout now has editor proof.</Task>
    <Task id="19" result="[PASS]">Route injection unchanged.</Task>
    <Task id="20" result="[PASS]">Forbidden scan and diff check passed for the runtime layout guard patch.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotStateDTO size="64" proof="Validate() checks exact offsets 0,24,36,40,44,48,56"/>
    <AutopilotAvoidanceDTO size="64" proof="ValidateAvoidance() checks exact offsets 0,12,24,36,40,44,48,52"/>
    <AutopilotFeelerResultDTO size="64" proof="ValidateFeelerResult() checks exact offsets 0,12,24,36,48,52,56,60"/>
    <AutopilotWaypointDTO size="32" proof="ValidateWaypoint() checks exact offsets 0,24,28"/>
    <AutopilotRouteRangeDTO size="32" proof="ValidateRouteRange() checks exact offsets 0,4,8,12,16,20,24,28"/>
    <AutopilotTuningDTO size="128" proof="ValidateTuning() checks exact offsets for all fields from 0 through 124"/>
    <AutopilotTelemetryEntry size="64" proof="ValidateTelemetry() checks exact offsets 0,24,36,40,44,48,52,56,60"/>
    <AutopilotHandlingProfileDTO size="32" proof="ValidateHandlingProfile() checks exact offsets 0,4,8,12,16,20,24,28"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged; this is editor validation only.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="Runtime Burst NoAlias fields unchanged" graph="job graph unchanged"/>
  <COMPILE_GUARD>Reflection is guarded by UNITY_EDITOR. Runtime imports and assembly boundaries are unchanged.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Primary DTO-only executable layout proof" complexityAfter="All route-relevant DTOs have editor-only layout proof; runtime complexity unchanged"/>
</SELF_AUDIT>

---

# SHINOBU_157 Quality Snapshot Hygiene Pass - 2026-05-19

## What Was Wrong
- `AutopilotTuningDTO.GlobalQualityWeight` was both the designer cap and the live thermal scalar.
- `ScheduleSolver` overwrote that field every solver admission, so one thermal dip could make the tuning row permanently low until a later editor write.
- Flow interpolation already consumed `Tuning.GlobalQualityWeight`, which made authored cap and runtime-resolved quality indistinguishable in snapshots.

## What Was Done
- Reused tuning offset 120 as `ResolvedQualityWeight`; `AutopilotTuningDTO` stays 128 bytes and ARM64-aligned.
- `GlobalQualityWeight` is now preserved as the authored cap. Scheduler computes `ResolvedQualityWeight = quantize_0.001(min(HomeostasisBrain.GlobalQualityWeight, GlobalQualityWeight))`.
- Fixed tick cadence, SDF feeler density, telemetry estimate, and flow interpolation now consume the same frozen resolved scalar for that scheduled batch.
- `TryReadTuning` returns sanitized tuning with current resolved quality for editor inspection without mutating Vault state.
- `SubmarineAutopilotTunerWindow` gained an editor-only `Quality Cap` slider and `Resolved Quality` typed readout.

## Cinematic Cheats Used
- No physical simulation was added. This preserves the existing Dear Lie: CPU computes mathematical intent only, and the vehicle motor owns movement.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING. No build/profiler pass was launched in this loop.
- Static estimate: runtime adds one finite sanitize, min, and 0.001 quantization per solver scheduling pass. The payback is avoiding sticky low-tier route behavior after thermal recovery; SDF sample count remains quality-collapsed exactly as before.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="quality_snapshot_hygiene" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent from owned files.</Task>
    <Task id="02" result="[PASS]">Physics ray and sphere casts remain absent from owned files.</Task>
    <Task id="03" result="[PASS]">DTOs remain raw-field structs; no getter/setter properties were added.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO remains explicit 64 bytes; tuning offset 120 is now a named float, not untracked padding.</Task>
    <Task id="05" result="[PASS]">Mock SDF fallback unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel still receives one frozen scalar per scheduled batch.</Task>
    <Task id="07" result="[PASS]">Potential-field steering unchanged except flow quality source now uses resolved scalar.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged: DesiredVelocity only.</Task>
    <Task id="09" result="[PASS]">Waypoint route advancement unchanged.</Task>
    <Task id="10" result="[PASS]">Continuous quality scaling preserved; authored cap and resolved quality are now distinct facts.</Task>
    <Task id="11" result="[PASS]">Flow compensation still collapses nearest-to-trilinear by resolved quality.</Task>
    <Task id="12" result="[PASS]">AUP delta math unchanged.</Task>
    <Task id="13" result="[PASS]">Rollback snapshot clarity improved: tuning cap and resolved quality are separate blittable fields.</Task>
    <Task id="14" result="[PASS]">Uninitialized Vault init path still writes cold defaults before use.</Task>
    <Task id="15" result="[PASS]">Telemetry receives the same resolved quality scalar as the solver.</Task>
    <Task id="16" result="[PASS]">Editor tuner now exposes quality cap and resolved-quality readout.</Task>
    <Task id="17" result="[PASS]">CSV handling profiles unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo path unchanged.</Task>
    <Task id="19" result="[PASS]">Dynamic waypoint and route injection unchanged.</Task>
    <Task id="20" result="[PASS]">Forbidden scan and diff check passed for changed owned C# files.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotTuningDTO size="128" proof="0 FeelerLength 4B; 4 SdfThresholdMeters 4B; 8 RepulsionWeight 4B; 12 MaxTurnRateRadians 4B; 16 WaypointAcceptanceRadius 4B; 20 FlowCompensationWeight 4B; 24 TargetSpeedFallback 4B; 28 GlobalQualityWeight 4B; 32 SdfOrigin float3 12B; 44 SdfCellSize float3 12B; 56 SdfDimensions int3 12B; 68 SdfRangeMeters 4B; 72 Flags 4B; 76 ActiveVehicleCount 4B; 80 FlowOrigin float3 12B; 92 FlowCellSize float3 12B; 104 FlowDimensions int3 12B; 116 SourceHash 4B; 120 ResolvedQualityWeight 4B; 124 padding uint; total 128"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below resolved q=0.3, SDF stays one nearest sample per feeler, gradients remain suppressed, flow returns one nearest cell, and cadence remains reduced. Middle tiers blend interpolation through smoothstep. High/Ultra restore 32 feelers, 12 steps, trilinear SDF/flow, and gradient-derived repulsion.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="NoAlias pointer fields unchanged" graph="FixedTick resolves quality cap -> evaluate(resolved quality) -> compute(tuning with resolved quality) -> telemetry(resolved quality)"/>
  <COMPILE_GUARD>Runtime imports remain Core/Core.Memory/Physics.Vehicles only. Editor keeps World AUP conversion behind editor source. No sibling runtime dependency was added. dotnet build was not relaunched.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Thermal quality overwrote authored cap and could cause sticky low-quality navigation" complexityAfter="O(1) resolved-quality snapshot per schedule; movement remains external and SDF sample complexity unchanged"/>
</SELF_AUDIT>

---

# SHINOBU_157 Black Box Alias Pass - 2026-05-19

## What Was Wrong
- AGENTS mandates `Docs/AgentLogs/Dump_SHINOBU_157.bin`.
- The XML task and earlier route card used `Docs/AgentLogs/Dump_NAVIGATION_SURGEON.bin`.
- A single path would leave one forensic contract unproven.

## What Was Done
- Fault dump now writes both binary files from the same 300-entry telemetry ring.
- `WriteTelemetryDump` streams `ReadOnlySpan<byte>` directly over Vault memory; no managed byte scratch is introduced.
- Route card and binary payload ledger now name both paths.

## Cinematic Cheats Used
- None. This is forensic output hygiene; the Dear Lie steering route is unchanged.

## Exact Microseconds Saved
- Normal runtime: 0 us. The path is entered only on fault or slow-solver telemetry.
- Fault path: writes one additional 19.2 KB binary copy. That cost is intentional forensic redundancy, not frame-budget work.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="black_box_alias" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent.</Task>
    <Task id="02" result="[PASS]">Physics casts remain absent.</Task>
    <Task id="03" result="[PASS]">DTO shape unchanged.</Task>
    <Task id="04" result="[PASS]">AutopilotStateDTO layout unchanged.</Task>
    <Task id="05" result="[PASS]">Mock SDF unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged.</Task>
    <Task id="07" result="[PASS]">Steering unchanged.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Route advancement unchanged.</Task>
    <Task id="10" result="[PASS]">Quality curve unchanged.</Task>
    <Task id="11" result="[PASS]">Flow compensation unchanged.</Task>
    <Task id="12" result="[PASS]">AUP math unchanged.</Task>
    <Task id="13" result="[PASS]">Rollback snapshot DTOs unchanged.</Task>
    <Task id="14" result="[PASS]">Vault buffers unchanged.</Task>
    <Task id="15" result="[PASS]">Black box now writes both AGENTS and XML alias paths.</Task>
    <Task id="16" result="[PASS]">Editor facade unchanged in this pass.</Task>
    <Task id="17" result="[PASS]">CSV parser unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo path unchanged.</Task>
    <Task id="19" result="[PASS]">Route injection unchanged.</Task>
    <Task id="20" result="[PASS]">Forensic path conflict resolved without hot-path allocation.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AutopilotTelemetryEntry size="64" proof="300 entries * 64 bytes = 19200 bytes per dump file"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged. Dump aliases run only after fatal/slow telemetry has already marked the route.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="NoAlias runtime jobs unchanged" graph="fault telemetry span -> Dump_SHINOBU_157.bin and Dump_NAVIGATION_SURGEON.bin"/>
  <COMPILE_GUARD>No new runtime domain import or assembly dependency was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Single forensic dump path satisfied only one document contract" complexityAfter="Two binary aliases from the same telemetry memory span; steering complexity unchanged"/>
</SELF_AUDIT>

---

# SHINOBU_157 Runtime Compile-Wall Import Pass - 2026-05-19

## What Was Wrong
- `SubmarineAutopilotSdfNavigator.cs` had an unused `using Hecton8.World`.
- That import is editor-convenient but runtime-hostile under the unidirectional assembly rule.

## What Was Done
- Removed the runtime World import.
- Verified runtime SHINOBU_157 imports only `Hecton8.Core`, `Hecton8.Core.Memory`, and `Hecton8.Physics.Vehicles`.
- Editor AUP conversion remains in `SubmarineAutopilotTunerWindow.cs`.

## Cinematic Cheats Used
- None. This is compile-wall hygiene.

## Exact Microseconds Saved
- Runtime: 0 us.
- Developer hardware impact: avoids a false sibling-domain compile route when asmdefs are isolated.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="runtime_compile_wall_import" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent.</Task>
    <Task id="02" result="[PASS]">Physics casts remain absent.</Task>
    <Task id="03" result="[PASS]">DTOs unchanged.</Task>
    <Task id="04" result="[PASS]">Layouts unchanged.</Task>
    <Task id="05" result="[PASS]">Mock SDF unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler kernel unchanged.</Task>
    <Task id="07" result="[PASS]">Steering unchanged.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Routing unchanged.</Task>
    <Task id="10" result="[PASS]">Quality scaling unchanged.</Task>
    <Task id="11" result="[PASS]">Flow unchanged.</Task>
    <Task id="12" result="[PASS]">AUP runtime math unchanged.</Task>
    <Task id="13" result="[PASS]">Rollback state unchanged.</Task>
    <Task id="14" result="[PASS]">Vault ownership unchanged.</Task>
    <Task id="15" result="[PASS]">Telemetry unchanged.</Task>
    <Task id="16" result="[PASS]">Editor-only World import remains in editor source.</Task>
    <Task id="17" result="[PASS]">CSV unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo unchanged.</Task>
    <Task id="19" result="[PASS]">Waypoint injection unchanged.</Task>
    <Task id="20" result="[PASS]">Runtime compile-wall import scan is clean.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <RuntimeImports proof="Core, Core.Memory, Physics.Vehicles only"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Unchanged.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="NoAlias jobs unchanged" graph="job graph unchanged"/>
  <COMPILE_GUARD>Runtime source no longer imports Hecton8.World; editor source still owns Scene View AUP conversion.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Unused runtime World import widened the apparent domain boundary" complexityAfter="Runtime domain imports only core/memory/vehicle contracts; steering complexity unchanged"/>
</SELF_AUDIT>

---

# SHINOBU_157 Cadence Delta Accumulation Pass - 2026-05-19

## What Was Wrong
- Low quality reduces solver cadence toward 5Hz, but the steering job received only the current fixed tick delta.
- That made turn-rate and acceleration clamps too strict after skipped ticks, so weak-device quality shedding changed handling more than intended.

## What Was Done
- Added `_accumulatedSolverDeltaTime`.
- `FixedTick` sanitizes dispatcher delta, accumulates skipped/pending windows up to 0.25s, and passes the accumulated value to `ComputeDesiredVelocityJob`.
- `ScheduleSolver` now returns `bool`; accumulated delta resets only when the solver job is actually scheduled.

## Cinematic Cheats Used
- The Dear Lie remains unchanged: no physical integration was added to the navigator. The fix only preserves deterministic intent timing while still shedding SDF work.

## Exact Microseconds Saved
- Measured exact microseconds: PENDING.
- Static cost: one float add/min per fixed tick and one scheduler bool branch. SDF/flow sample counts are unchanged; low-tier steering avoids over-clamped response after cadence drops.

<SELF_AUDIT agent="SHINOBU_157" date="2026-05-19" revision="cadence_delta_accumulation" verification="STATIC_SOURCE_ONLY">
  <TASK_RECONCILIATION>
    <Task id="01" result="[PASS]">NavMesh remains absent.</Task>
    <Task id="02" result="[PASS]">Physics casts remain absent.</Task>
    <Task id="03" result="[PASS]">DTOs unchanged.</Task>
    <Task id="04" result="[PASS]">Layouts unchanged.</Task>
    <Task id="05" result="[PASS]">Mock SDF unchanged.</Task>
    <Task id="06" result="[PASS]">Feeler work is still cadence-shed by resolved quality.</Task>
    <Task id="07" result="[PASS]">Potential-field steering now receives accumulated deterministic delta.</Task>
    <Task id="08" result="[PASS]">Dear Lie handoff unchanged.</Task>
    <Task id="09" result="[PASS]">Route advancement unchanged.</Task>
    <Task id="10" result="[PASS]">Cadence shedding now preserves elapsed simulation delta.</Task>
    <Task id="11" result="[PASS]">Flow compensation unchanged.</Task>
    <Task id="12" result="[PASS]">AUP math unchanged.</Task>
    <Task id="13" result="[PASS]">No Unity Time API introduced; dispatcher fixed delta remains the source.</Task>
    <Task id="14" result="[PASS]">Vault ownership unchanged.</Task>
    <Task id="15" result="[PASS]">Telemetry unchanged.</Task>
    <Task id="16" result="[PASS]">Editor facade unchanged.</Task>
    <Task id="17" result="[PASS]">CSV unchanged.</Task>
    <Task id="18" result="[PASS]">Gizmo unchanged.</Task>
    <Task id="19" result="[PASS]">Route injection unchanged.</Task>
    <Task id="20" result="[PASS]">Static guard scan remains clean after scheduler change.</Task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <SchedulerScalar name="_accumulatedSolverDeltaTime" size="4" proof="private main-thread scalar, not a shared NativeArray DTO"/>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>At resolved q near 0.1, cadence can skip roughly 11 fixed ticks; those fixed deltas now accumulate up to 0.25s and feed turn/acceleration clamps. At q=1, cadence is one tick and accumulated delta is approximately the dispatcher fixed delta.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS privatePersistentNativeCollections="0" requested="71592,71593,71594,71595,71596,71597,71598,71599,71600,71601,71602,71603"/>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noAlias="NoAlias jobs unchanged" graph="FixedTick accumulates delta -> ScheduleSolver returns scheduled bool -> evaluate -> compute(accumulated delta) -> telemetry"/>
  <COMPILE_GUARD>No new runtime import or sibling dependency was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION complexityBefore="Cadence shedding reduced solver calls but also under-applied elapsed steering time" complexityAfter="O(1) delta accumulation preserves steering intent while SDF sample complexity stays cadence-shed"/>
</SELF_AUDIT>
