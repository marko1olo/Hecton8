# LOG_SHINOBU_132

## 2026-05-19 - TETHER_AND_CABLE_PHYSICS_SOLVER

What was wrong:
- No first-party tether Unity Joint scripts were found, but cable-domain visual paths still carried LineRenderer dependencies in BioCableIK, AbyssalThermalManager cable spawn, LogisticsPipeNode, and PowerRelayNode.
- Existing AUP tether solver used TetherNodeDTO naming and several NativeArray copy/writeback loops instead of the required CableNodeDTO* contract.
- SHINOBU_132 had no separate vault IDs, no exact CableNodeDTO ABI, no 50-node mock cable generator, no SHINOBU_132 telemetry ring, no Cable Surgeon dump, and no Abyssal Tether Tuner.

What was done:
- Added CableNodeDTO, explicit 64-byte ABI: CurrentAUP @0, PreviousAUP @24, InverseMass @48, Flags @52, _pad0.._pad7 @56..63.
- Added TetherTelemetryEntry, 300-frame vault ring, state hash, max tension, iteration count, and agent-keyed Dump_SHINOBU_132.bin writer.
- Added SHINOBU132 vault buffers: nodes 71320, constraints 71321, spline vertices 71322, tensions 71323, physics events 71324, telemetry ring/head 71325/71326, pinned AUP/mask 71327/71328, tuning 71329, materials 71330, bootstrap 71331, endpoints 71332.
- Implemented GenerateMockTethersJob: 5 deterministic mock cables x 50 nodes, anchored endpoints, 10..64 visual vertices per cable, UninitializedMemory backing, cold zero-init job.
- Implemented SimulateCablePointsJob, SolveCableConstraintsJob, GenerateSplineVerticesJob, CableSplineGpuMemcpyJob, RecordTetherTelemetryJob.
- Added final-pass finite PhysicsEventPayload writes to SignalBus NativeQueue plus vault mirror, using existing PressureImpulse lane plus SHINOBU_132 tension status bit.
- Replaced cable-specific LineRenderer paths with existing ConnectionSplineBatchRenderer pipe-link spline submissions; no SHINOBU_132 Core renderer batch remains.
- Added Abyssal Tether Tuner UI Toolkit window and CablePhysicsDebugGizmo132 OnDrawGizmos.
- Hooked SHINOBU132 mock solver into TetherManager fixed tick without removing SHINOBU143 work.

Cinematic cheats used:
- Cheap Verlet node truth with Catmull-Rom "Dear Lie" visual spline instead of extra simulated nodes.
- GlobalRegistry.Fluid flow sample plus sin/cos fallback/noise instead of fluid particle or voxel flow simulation.
- Shader-bent spline tube for BioCable visuals instead of CPU LineRenderer mesh rebuilds.

Exact microseconds saved / cost targets:
- PhysX joint island avoidance: estimated 40-180 us/frame per active flexible joint chain.
- Cable-domain LineRenderer purge: estimated 30-120 us/frame during active cable visual refresh.
- Pointer ref CableNodeDTO mutation: estimated 2-8 us/frame at 250 nodes versus copy/writeback.
- LockBufferForWrite + MemCpy upload: estimated 10-45 us/upload versus managed SetData path.
- Abyssal current cheat: target <2 us/frame.
- Telemetry ring write: target <8 us/frame.
- Event bus force packets: target <10 us/frame at mock scale.

Verification:
- rg joints in Assets/_Project/Scripts: no ConfigurableJoint/SpringJoint/CharacterJoint/HingeJoint matches.
- rg LineRenderer in cable-domain files: no matches in BioCableIK, AbyssalThermalManager cable visuals, LogisticsPipeNode, PowerRelayNode.
- diff --check on touched SHINOBU_132 files: no whitespace errors; Git reported only CRLF normalization warnings on existing files.
- Build not run: CPU samples were 99.23/91.11/91.21 percent and project root has no .sln. Running dotnet build would violate the explicit >50% CPU/csc policy.

<SELF_AUDIT>
<BYTE_LAYOUT>
CableNodeDTO Size=64.
Offset 0 double3 CurrentAUP.
Offset 24 double3 PreviousAUP.
Offset 48 float InverseMass.
Offset 52 uint Flags.
Offset 56..63 explicit pad bytes _pad0.._pad7.
</BYTE_LAYOUT>
<VAULT_BUFFER_IDS>
Shinobu132CableNodes=71320; Shinobu132CableConstraints=71321; Shinobu132SplineVertices=71322; Shinobu132SegmentTensions=71323; Shinobu132PhysicsEvents=71324; Shinobu132TelemetryRing=71325; Shinobu132TelemetryHead=71326; Shinobu132PinnedAups=71327; Shinobu132PinnedMask=71328; Shinobu132Tuning=71329; Shinobu132CableMaterials=71330; Shinobu132BootstrapState=71331; Shinobu132Endpoints=71332.
</VAULT_BUFFER_IDS>
<GC_ASSERTION>
Hot solver jobs contain no List/LINQ/string.Split/new GameObject/new Material/Rigidbody/AddForce/LineRenderer calls. Runtime allocations are restricted to cold bootstrap/editor/gizmo paths.
</GC_ASSERTION>
<AUP_ASSERTION>
Solver truth remains double3 AUP. Constraint math subtracts double3 nodes, clamps local delta, casts to float3 only for distance normalization, visual spline output, and event runtime position.
</AUP_ASSERTION>
<SCALABILITY_ASSERTION>
Constraint iterations are int(math.lerp(2,15,GlobalQualityWeight)), clamped 2..15. No low/ultra binary quality switch was introduced.
</SCALABILITY_ASSERTION>
<FORCE_ROUTING_ASSERTION>
No Rigidbody force is applied by SHINOBU_132. Tension writes unmanaged PhysicsEventPayload with existing EventType=PressureImpulse plus CableNodeFlags132.TetherTensionEvent into SignalBus NativeQueue and a vault mirror.
</FORCE_ROUTING_ASSERTION>
<BUILD_ASSERTION>
Compile not verified because CPU stayed above policy threshold and no .sln exists. Static audit passed; Unity compile still required when machine load permits.
</BUILD_ASSERTION>
</SELF_AUDIT>

## 2026-05-19 - Loop 6 Compile-Wall And Sweet-Lie Repair

What was wrong:
- The first pass widened Core with SHINOBU_132-specific BufferID enum entries, a PhysicsEventType enum value, and a BioCable renderer service surface.
- The spline generation path treated all mock nodes as one strip, allowing visual interpolation across cable boundaries.
- The editor tuner wrote Vault DTOs, but solver scheduling did not consume the full tuning set.
- Abyssal current advection was only a deterministic fallback, not routed from the existing fluid service when available.

What was done:
- Replaced Core BufferID enum edits with owner-local numeric casts `71320..71332` in `CablePhysics132BufferIds`.
- Removed the SHINOBU_132 Core event/renderer extensions; tension now uses the existing SignalBus payload route and a domain status bit.
- Changed spline extraction to calculate cable/local indices explicitly and scale visual vertices per cable from 10 to 64 using `GlobalQualityWeight` and tuner `Reserved0`.
- Solver now consumes gravity, fluid friction, max iterations, break force, and spline steps from `VerletCableTuningDTO`.
- `TetherManager` samples `GlobalRegistry.Fluid.TrySampleModAbyssalFlow` once outside Burst and passes the finite vector as data.
- Fault dump path changed from task alias `Dump_CABLE_SURGEON.bin` to the black-box mandated `Dump_SHINOBU_132.bin`.
- Added stable `.meta` GUIDs for the three new SHINOBU_132 C# assets.

Cinematic cheats used:
- "Sweet Lie" remains a Catmull-Rom visual hallucination over 50 physics nodes; low quality writes 10 vertices/cable, high quality can write 64/cable without simulating more physical particles.

Exact microseconds saved / cost targets:
- Low-quality spline extraction: writes 50 visual vertices total instead of 320 max, estimated 5-20 us/frame saved at mock scale.
- Compile-wall repair: removes SHINOBU_132-specific shared Core churn; runtime microseconds unchanged, iteration cost reduced by avoiding unrelated rebuild surfaces.

Verification:
- Static grep found no `Shinobu132`, `TetherTensionForce`, `SubmitBioCableSpline`, `RemoveBioCableSpline`, or `BioCable` residue in `Assets/_Project/Scripts/Core`.
- Static grep found no first-party `ConfigurableJoint`, `SpringJoint`, `CharacterJoint`, or `HingeJoint` in `Assets/_Project/Scripts`.
- Static grep found no `LineRenderer` residue in BioCableIK, AbyssalThermalManager cable visuals, LogisticsPipeNode, or PowerRelayNode.
- `git diff --check` on SHINOBU_132 touched files reported only CRLF normalization warnings.
- Compile still pending CPU gate; latest CPU samples were 73.73/63.86/37.05 percent with no active dotnet/csc, so no dotnet build was launched.

## 2026-05-20 - Loop 8 Async Upload And CSV Scratch Repair

What was wrong:
- Task 09 was overstated: the previous `UploadSplineVertices` path scheduled `CableSplineGpuMemcpyJob` and immediately force-completed it, creating a hidden VISUAL_SYNC stall.
- Task 18 still staged CSV bytes through `File.ReadAllBytes`, producing a managed `byte[]` in the editor bridge.
- Telemetry sampling returned index 0 after ring wrap instead of the actual last written slot at capacity - 1.

What was done:
- Removed the blocking upload facade and added `TryBeginSplineVertexUpload` / `TryFinalizeSplineVertexUpload` with `CableSplineUploadTicket132`.
- Added `TetherSplineIndirectArgsDTO` (16 bytes), `CableSplineIndirectArgsJob`, `CreateSplineIndirectArgsBuffer`, and `TryDrawSplineProceduralIndirect` so the renderer can use `Graphics.DrawProceduralIndirect`.
- Hardened `CableSplineGpuMemcpyJob` with `[NoAlias]` destination pointer and mapped-byte copy clamping.
- Changed `Shinobu132CablePhysicsTunerWindow.ReloadCsv` to read through `FileStream.Read(new Span<byte>(nativePtr, len))` into Temp `NativeArray<byte>` and feed `ReadOnlySpan<byte>` into `CableMaterialCsvParser.ParseHashTable`.
- Fixed latest-telemetry readback to wrap `head <= 0` to `capacity - 1`.

Cinematic cheats used:
- Physical truth remains 50 Verlet nodes per mock cable; visual smoothness is Catmull-Rom spline hallucination and GPU procedural expansion.
- Draw arguments can scale vertices-per-spline-point without increasing gameplay particles or PhysX bodies.

Exact microseconds saved / cost targets:
- Removed forced GPU upload completion: estimated 10-45 us/upload stall avoided when the copy overlaps the dispatcher chain.
- Temp NativeArray CSV scratch: 0 gameplay us; removes one managed byte[] allocation per editor reload.
- Telemetry wrap fix: 0 meaningful frame cost; prevents false post-mortem reads after 300-frame wrap.

Verification:
- rg SHINOBU_132 upload/CSV: no `File.ReadAllBytes`, `UploadSplineVertices`, `GraphicsBuffer.SetData`, managed `byte[]`, `string.Split`, or `.Split()` matches.
- rg first-party Unity joints: no `ConfigurableJoint`, `SpringJoint`, `CharacterJoint`, or `HingeJoint` matches in `Assets/_Project/Scripts`.
- rg cable-domain LineRenderer: no matches in BioCableIK, AbyssalThermalManager cable visuals, LogisticsPipeNode, or PowerRelayNode.
- rg hot DTO/solver bans: no get/set DTO properties, `Pack=1`, `foreach`, LINQ, `Time.deltaTime`, `UnityEngine.Random`, `Rigidbody`, `AddForce`, `new List`, or Persistent `NativeArray` matches in SHINOBU_132 solver/DTO files.
- rg BurstCompile: all SHINOBU_132 solver/DTO jobs use `CompileSynchronously=true`, `FloatMode.Deterministic`, and `FloatPrecision.Standard`.
- `git diff --check` reported no whitespace errors; CRLF normalization warnings only.
- Compile not launched: latest CPU average was 25.07 percent, but dotnet Id 53260 was already running, so the no-concurrent-dotnet/csc gate remained closed.

<SELF_AUDIT>
  <TASKS>
    <TASK id="01" status="PASS">No first-party tether ConfigurableJoint/SpringJoint/CharacterJoint/HingeJoint matches remain under Assets/_Project/Scripts.</TASK>
    <TASK id="02" status="PASS">Cable-domain LineRenderer references removed from the inspected cable visual paths.</TASK>
    <TASK id="03" status="PASS">CableNodeDTO uses public fields; hot jobs mutate by pointer refs through UnsafeUtility.AsRef.</TASK>
    <TASK id="04" status="PASS">CableNodeDTO is explicit 64 bytes with AUP fields on 8-byte offsets and pad bytes 56..63.</TASK>
    <TASK id="05" status="PASS">GenerateMockTethersJob seeds 5 deterministic 50-node cables in Vault buffers.</TASK>
    <TASK id="06" status="PASS">SimulateCablePointsJob performs deterministic Verlet integration with guarded dt, step, and finite recovery.</TASK>
    <TASK id="07" status="PASS">SolveCableConstraintsJob relaxes distance constraints for continuous 2..15 iteration budget.</TASK>
    <TASK id="08" status="PASS">GenerateSplineVerticesJob emits 10..64 visual vertices/cable via Catmull-Rom Sweet Lie.</TASK>
    <TASK id="09" status="PASS">Ticketed LockBufferForWrite upload plus Burst memcpy and indirect-args job; no immediate force-complete on normal finalize.</TASK>
    <TASK id="10" status="PASS">ResolveIterationCount uses lerp(2,maxIterations,GlobalQualityWeight) with no low/high switch.</TASK>
    <TASK id="11" status="PASS">Tension exits as unmanaged SignalBus PhysicsEventPayload; no Rigidbody mutation.</TASK>
    <TASK id="12" status="PASS">Fluid input is sampled once outside Burst via GlobalRegistry and blended with deterministic fallback current.</TASK>
    <TASK id="13" status="PASS">Constraint math subtracts double3 AUP first, clamps local delta, then casts to float3.</TASK>
    <TASK id="14" status="PASS">Deterministic Burst flags and 64-byte DTO support rollback memcpy fencing.</TASK>
    <TASK id="15" status="PASS">Vault buffers request UninitializedMemory and cold zero-init writes owned spans once.</TASK>
    <TASK id="16" status="PASS">300-entry TetherTelemetryEntry ring and Dump_SHINOBU_132.bin writer exist; telemetry wrap readback fixed.</TASK>
    <TASK id="17" status="PASS">UI Toolkit Abyssal Tether Tuner writes Vault tuning consumed by the solver.</TASK>
    <TASK id="18" status="PASS">CSV bridge uses Temp NativeArray<byte>, ReadOnlySpan<byte>, FNV hash table parser, no File.ReadAllBytes.</TASK>
    <TASK id="19" status="PASS">CablePhysicsDebugGizmo132 draws true nodes and constraints from Vault in editor only.</TASK>
    <TASK id="20" status="PARTIAL">Static self-audit passed; guarded compile is pending because active dotnet process blocks the build gate.</TASK>
  </TASKS>
  <STRUCT_LAYOUT>
    CableNodeDTO: 0 double3 CurrentAUP size 24; 24 double3 PreviousAUP size 24; 48 float InverseMass size 4; 52 uint Flags size 4; 56..63 byte padding size 8; total 64 bytes.
    TetherSplineIndirectArgsDTO: 0 uint VertexCountPerInstance; 4 uint InstanceCount; 8 uint StartVertex; 12 uint StartInstance; total 16 bytes.
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Below GlobalQualityWeight 0.3, solver iterations collapse toward 2, spline vertices approach 10/cable, Catmull-Rom weight is suppressed by math.step(0.25,q) * Smooth01(q), current influence is lerped down, and indirect draw vertex expansion remains renderer-owned. No binary tier branch is used.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private persistent NativeArray ownership in SHINOBU_132 runtime. VaultBufferHandle IDs requested: 71320 CableNodes, 71321 CableConstraints, 71322 SplineVertices, 71323 SegmentTensions, 71324 PhysicsEvents, 71325 TelemetryRing, 71326 TelemetryHead, 71327 PinnedAups, 71328 PinnedMask, 71329 Tuning, 71330 CableMaterials, 71331 BootstrapState, 71332 Endpoints.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    Dependencies: caller input -> ClearFrameCableOutputsJob -> AdvanceMockCableEndpointsJob -> SimulateCablePointsJob -> SolveCableConstraintsJob -> GenerateSplineVerticesJob -> RecordTetherTelemetryJob. Upload route: caller visual dependency -> CableSplineGpuMemcpyJob/CableSplineIndirectArgsJob -> TryFinalizeCompleted -> UnlockBufferAfterWrite. NativeArray and pointer job fields use NoAlias where applicable.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    SHINOBU_132 runtime solver references Core/Core.Memory plus Unity packages only; no direct sibling runtime domain reference was added. Guarded compile is pending due active dotnet process.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Rejected PhysX joint chains, Rigidbody segment chains, and LineRenderer CPU mesh rebuilds. Gameplay truth is O(nodes * iterations + constraints * iterations). Visual truth is O(visualVertices) spline interpolation, not O(simulatedVisualNodes * iterations) physics. At mock scale, 250 physical nodes become 50..320 visual vertices without adding simulated particles.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
