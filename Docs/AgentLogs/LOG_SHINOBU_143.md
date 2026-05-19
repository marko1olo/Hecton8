# LOG_SHINOBU_143

## 2026-05-19 - AUP Verlet Tether Solver Pass

What was wrong: active tether runtime had a local `float3` Verlet path and continuous-quality gaps; `HarpoonLauncherTool` still used a `LineRenderer` tracer path. The project-wide compile wall is currently external: missing Visor/Somatic/Equipment DTO contracts prevent a full `dotnet build` proof.

What was done: added the SHINOBU_143 AUP DTO surface, deterministic Burst integration/constraint/spline/telemetry jobs, Vault mock buffers, UI Toolkit tuner, byte-span CSV parser, harpoon GPU tracer, and selected-tether gizmo. Updated `KINETIC_ENTANGLEMENT.md` with the current boundary.

Cinematic cheats used: harpoon feedback is a two-point GPU line-strip beam, not a simulated cable. Low quality collapses spline visuals to a linear/taut fake while authoritative tension stays bounded in the solver. Catmull-Rom exists for presentation only; force truth is node/constraint math.

Exact microseconds saved: not claimed. Static expected saving is removal of Harpoon LineRenderer mesh rebuild/component path and relaxation shedding from 15 iterations down to 2-3 under low `GlobalQualityWeight`. Profiler proof is blocked by the external compile wall.

<SELF_AUDIT agent_id="SHINOBU_143" domain="KINETIC_TETHER_AND_GRAPPLE_PHYSICS">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Target harpoon/tether/tow files scanned. No Unity Spring/Fixed/Character/ConfigurableJoint route remains in target path.</TASK>
    <TASK id="02" status="PASS">Harpoon `LineRenderer` path removed. Tether visuals already used GraphicsBuffer; harpoon now uses procedural tether shader buffers.</TASK>
    <TASK id="03" status="PASS">`TetherNodeDTO` has raw fields only; no get/set properties.</TASK>
    <TASK id="04" status="PASS">Layout validation added through `VerletCableLayout.ValidateTetherAupLayouts()`.</TASK>
    <TASK id="05" status="PASS">`InitializeMockTetherAupJob` creates 5 deterministic mock tethers with 30 nodes each.</TASK>
    <TASK id="06" status="PASS">`IntegrateTetherNodesJob` integrates AUP nodes with deterministic Burst and SimulationTickDelta input.</TASK>
    <TASK id="07" status="PASS">`SolveTetherConstraintsJob` relaxes distance constraints, records tension, and keeps NaN guards.</TASK>
    <TASK id="08" status="PASS">`GenerateTetherSplineVerticesJob` emits GPU spline vertices and uses Catmull-Rom only as a visual fake.</TASK>
    <TASK id="09" status="PASS">`TetherSplineGpuMemcpyJob` plus `LockBufferForWrite` performs bounded native upload.</TASK>
    <TASK id="10" status="PASS">Existing runtime iteration budget now uses continuous `math.lerp(2, 15, HomeostasisBrain.GlobalQualityWeight)`.</TASK>
    <TASK id="11" status="PASS">`TetherForcePacketDTO` emits paired endpoint force vectors with AUP application points.</TASK>
    <TASK id="12" status="PASS">Integrator consumes abyssal-current acceleration, scaled continuously by quality.</TASK>
    <TASK id="13" status="PASS">AUP deltas are subtracted as `double3` before bounded local `float3` casts.</TASK>
    <TASK id="14" status="PASS">Authoritative jobs use Burst deterministic float mode and blittable DTOs.</TASK>
    <TASK id="15" status="PASS">New SHINOBU slabs use `NativeArrayOptions.UninitializedMemory` when fully overwritten by cold Burst bootstrap.</TASK>
    <TASK id="16" status="PASS">`RecordTetherAupTelemetryJob` writes a 300-entry black-box telemetry ring.</TASK>
    <TASK id="17" status="PASS">`Shinobu143CablePhysicsTunerWindow` is UI Toolkit and writes Vault tuning.</TASK>
    <TASK id="18" status="PASS">`CableMaterialCsvParser.Parse(ReadOnlySpan&lt;byte&gt;, NativeArray&lt;CableMaterialDTO&gt;)` added; editor reload routes through bytes.</TASK>
    <TASK id="19" status="PASS">Selected tether gizmo draws red nodes and green constraints without runtime GameObjects.</TASK>
    <TASK id="20" status="BLOCKED_BY_DEPENDENCY">Self-audit/log complete, but compile proof is blocked by external missing DTO contracts in non-SHINOBU_143 files.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <PRIMARY_DTO name="TetherNodeDTO" size="64" alignment="16-compatible" false_sharing="one_node_per_64B_line">
      <FIELD name="CurrentAUP" offset="0" size="24">double3 = 3 * 8 bytes, covers 0..23.</FIELD>
      <FIELD name="PreviousAUP" offset="24" size="24">double3 = 3 * 8 bytes, covers 24..47.</FIELD>
      <FIELD name="InverseMass" offset="48" size="4">float, covers 48..51.</FIELD>
      <FIELD name="Flags" offset="52" size="4">uint, covers 52..55.</FIELD>
      <FIELD name="_pad0" offset="56" size="8">private ulong padding, covers 56..63.</FIELD>
      <MATH>24 + 24 + 4 + 4 + 8 = 64; 64 % 16 = 0; 64 % 8 = 0.</MATH>
    </PRIMARY_DTO>
    <RELATED_DTO name="TetherConstraintDTO" size="32">Two int indices, rest length, stiffness, flags, cable id, 8-byte tail pad.</RELATED_DTO>
    <RELATED_DTO name="TetherForcePacketDTO" size="64">Application `double3` at 0, force `float3` at 24, scalar/id/flags at 36..55, 8-byte tail pad.</RELATED_DTO>
    <RELATED_DTO name="TetherSplineVertexDTO" size="32">position/u/tangent/tension for GPU spline lane.</RELATED_DTO>
    <RELATED_DTO name="TetherAupTelemetryEntry" size="64">fixed ring entry, one cache line.</RELATED_DTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    Solver iterations resolve as `int iterations = (int)math.lerp(2f, 15f, GlobalQualityWeight)`. Below roughly 0.3 quality, spline generation uses `math.step(0.3f, q)` to skip Catmull-Rom blending and collapse to a linear cable fake; existing visual upload can also use the taut-line fake under low quality and high stress. Abyssal current contribution scales with `q*q`, so thermal collapse sheds visual smoothing and current ALU before authoritative constraint math fails.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS>
    <CLAIM>New SHINOBU_143 AUP solver surface declares zero persistent private NativeArray fields; arrays are local Vault resolves passed to jobs. Existing `TetherInstance` NativeArray aliases were pre-existing and Vault-backed.</CLAIM>
    <BUFFER id="71280" name="Shinobu143TetherAupNodes" type="TetherNodeDTO[150]" />
    <BUFFER id="71281" name="Shinobu143TetherConstraints" type="TetherConstraintDTO[145]" />
    <BUFFER id="71282" name="Shinobu143TetherEndpoints" type="TetherEndpointAupDTO[5]" />
    <BUFFER id="71283" name="Shinobu143TetherSplineVertices" type="TetherSplineVertexDTO[150]" />
    <BUFFER id="71284" name="Shinobu143TetherForcePackets" type="TetherForcePacketDTO[290]" />
    <BUFFER id="71285" name="Shinobu143TetherTelemetryRing" type="TetherAupTelemetryEntry[300]" />
    <BUFFER id="71286" name="Shinobu143TetherTelemetryHead" type="int[1]" />
    <BUFFER id="71287" name="Shinobu143CableMaterials" type="CableMaterialDTO[16]" />
    <BUFFER id="71288" name="Shinobu143CableMaterialCsvScratch" type="byte[16384]" />
    <BUFFER id="71289" name="Shinobu143TetherBootstrapState" type="int[1]" />
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>New Burst jobs annotate NativeArray fields with `[NoAlias]` where applicable.</NO_ALIAS>
    <JOB name="InitializeMockTetherAupJob" consumes="Vault handles" outputs="nodes,constraints,endpoints,materials,bootstrapState" schedule="cold Run only" />
    <JOB name="IntegrateTetherNodesJob" consumes="Nodes,PinnedAUPs,PinnedMask" outputs="Nodes" schedule="IJobParallelFor, caller-owned handle" />
    <JOB name="SolveTetherConstraintsJob" consumes="Nodes,Constraints" outputs="Nodes,SegmentTensions,SolverStats,ForcePackets" schedule="IJob, caller-owned handle" />
    <JOB name="GenerateTetherSplineVerticesJob" consumes="Nodes,SegmentTensions,CameraAUP" outputs="TetherSplineVertexDTO" schedule="IJobParallelFor, caller-owned handle" />
    <JOB name="TetherSplineGpuMemcpyJob" consumes="TetherSplineVertexDTO" outputs="mapped GraphicsBuffer memory" schedule="Run inside upload bridge after LockBufferForWrite" />
    <JOB name="RecordTetherAupTelemetryJob" consumes="Nodes,SolverStats" outputs="TelemetryRing,TelemetryHead" schedule="IJob, caller-owned handle" />
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef direct sibling dependency was added. New physics code depends on existing Core/Memory contracts for Vault IDs. `dotnet build .\Assembly-CSharp.csproj --no-restore` was launched only after CPU was 19% and no `dotnet`/`csc` was present. It failed in unrelated files: `HectonVisorUberPostFeature.cs`, `DeferredDecalPass.cs`, `ModularEquipmentEngine.cs`, `GlobalRegistryContracts.cs`, and `SomaticTunerWindow.cs` missing foreign DTOs. No SHINOBU_143 file error was emitted before that compile wall.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    The fake is deliberate: do not simulate a rendered rope mesh or harpoon cable with PhysX. Authoritative work is O(nodes + constraints * iterations). Visual work is O(vertices) buffer emission and GPU shader expansion. Low quality reduces visual spline to a line and harpoon feedback to two points. Before: PhysX joint chains plus LineRenderer CPU mesh rebuild could recurse unpredictably and rebuild mesh data per frame. After: bounded Verlet math, packetized forces, and shader-side line-strip presentation.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
