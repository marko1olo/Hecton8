# SHINOBU_134 Agent Log

## 2026-05-19 Abyssal Shadow Culling Static Integration

Status: PENDING VERIFICATION

### What Was Wrong

The project had no owner-local SHINOBU_134 shadow-culling lane. Shadow suppression could only be inferred from Unity renderer/LOD behavior, which is not an acceptable authority route for 50,000 underwater casters. There was no explicit 32-byte `ShadowCullStateDTO`, no Vault buffer IDs for shadow state, no Burst AUP-local culling kernel, no HZB/SDF presentation fake, no indirect args row, and no 300-frame shadow blackbox.

### What Was Done

- Added `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingTypes.cs` with explicit DTOs, Vault IDs `71340..71350`, layout validator, AUP plane localization helper, byte-level CSV parser, and binary telemetry dump writer.
- Added `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingJobs.cs` with deterministic mock data generation, HZB tile mock generation, `EvaluateShadowCullingJob`, telemetry reduction, and indirect args generation. Every job uses `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Added `Assets/_Project/Scripts/Graphics/Culling/AbyssalShadowCullingRuntime.cs` with owner-local Vault handles, uninitialized buffer requests, Simulation/VisualSync dispatcher phase adapters, double-buffered `GraphicsBuffer.LockBufferForWrite` upload, blackbox telemetry, editor gizmo, and cold CSV ingestion.
- Added `Assets/_Project/Scripts/Graphics/Culling/Editor/AbyssalShadowTunerWindow.cs` as the UI Toolkit facade for sliders, mock execution, layout validation, CSV load, and snapshot readout.
- Added `Assets/_Project/Art/Shaders/Hecton_AbyssalShadowDither.hlsl` for Bayer shadow dissolve using the packed cull state buffer.
- Added Unity `.meta` files for all new source/shader assets.
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the SHINOBU_134 Vault lane.

### Cinematic Cheats Used

- Dithered shadow fade replaces hard cutoff: screen-space Bayer `clip()` consumes `IlluminationScalar` instead of spawning/transferring fade objects.
- HZB occlusion is a tile-depth scalar check, not a CPU visibility query or voxel raymarch.
- SDF darkness is a scalar `OcclusionScalar` gate, ready for a voxel/probe owner to fill later, not a per-caster ray simulation.
- Point-light shadow allowance is quality-weighted deterministic decimation; below ultra, local point shadows are treated as presentation luxury.

### Exact Microseconds Saved

Measured proof is absent. Estimates only:

- Removing 50,000 `Vector3.Distance`/sqrt evaluations: estimated 20-80 us on low-end CPU.
- Avoiding managed visible list construction and CPU sparse upload: estimated 30-150 us depending on caster count.
- Avoiding per-renderer shadow state mutation: estimated 6-20 us per 1,000 renderer writes plus avoided Unity render-state churn.
- Avoiding four-sample CPU voxel checks for 50,000 casters: approximately 200,000 sample operations avoided; runtime us pending profiler.
- Real acceptance remains blocked until Unity import, Burst Inspector, Frame Debugger shadow-map captures, Profiler, GCMonitor, and player build proof exist.

<SELF_AUDIT agent_id="SHINOBU_134" status="PENDING_VERIFICATION">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS_STATIC">Legacy first-party shadow-distance MonoBehaviour scan found no owned script to delete; vendor and unrelated renderer paths were not mutated.</TASK>
    <TASK id="02" status="PASS_STATIC">Shadow pass authority now exists as Vault cull flags and indirect args; LODGroup YAML mutation rejected without ownership/FileID proof.</TASK>
    <TASK id="03" status="PASS_STATIC">Hot DTOs use raw fields. No getter/setter properties in `ShadowCullStateDTO` or other culling DTOs.</TASK>
    <TASK id="04" status="PASS_STATIC">`ShadowCullStateDTO` explicit 32B layout and editor validator implemented; counters explicit 64B.</TASK>
    <TASK id="05" status="PASS_STATIC">`GenerateMockCullingDataJob` seeds deterministic 50,000 instance windows.</TASK>
    <TASK id="06" status="PASS_STATIC">`EvaluateShadowCullingJob` performs AUP-local AABB/frustum checks with directional shadow expansion, HZB, SDF, `[NoAlias]`, and required Burst flags.</TASK>
    <TASK id="07" status="PASS_STATIC">Squared distance only; owned static scan found no `math.sqrt`.</TASK>
    <TASK id="08" status="PASS_STATIC">Fade scalar is written to cull state and shader include provides Bayer dither clip.</TASK>
    <TASK id="09" status="PASS_STATIC">Illumination/material/occlusion scalar clears `CastShadows` below darkness threshold.</TASK>
    <TASK id="10" status="PASS_STATIC">VisualSync uploads state and indirect args through `GraphicsBuffer.LockBufferForWrite`; no managed visible list.</TASK>
    <TASK id="11" status="PASS_STATIC">`GlobalQualityWeight` continuously drives distance, radius, HZB resolution, SDF threshold, point-light allowance, and fade behavior.</TASK>
    <TASK id="12" status="PASS_STATIC">Point-light shadows are culled unless quality-weight allowance reaches ultra range.</TASK>
    <TASK id="13" status="PASS_STATIC">AUP plane localization helper exists; culling subtracts `CameraAUP` before float cast.</TASK>
    <TASK id="14" status="PASS_STATIC">Cull state is presentation-only and flagged rollback-excluded; it is not introduced into gameplay state hashing.</TASK>
    <TASK id="15" status="PASS_STATIC">All steady-state Vault buffers use `NativeArrayOptions.UninitializedMemory`.</TASK>
    <TASK id="16" status="PASS_STATIC">300-entry telemetry ring and `Docs/AgentLogs/Dump_SHADOW_DIRECTOR.bin` writer implemented.</TASK>
    <TASK id="17" status="PASS_STATIC">UI Toolkit `Abyssal Shadow Tuner` editor facade implemented.</TASK>
    <TASK id="18" status="PASS_STATIC">Allocation-free parser over Vault byte scratch writes unmanaged profile rules.</TASK>
    <TASK id="19" status="PASS_STATIC">Editor-only live gizmo draws green/yellow/red AUP-local boxes from Vault states.</TASK>
    <TASK id="20" status="PASS_STATIC">Static scans and this self-audit generated; Unity/runtime/profiler proof remains pending.</TASK>
  </TASK_RECONCILIATION>

  <STRUCT_LAYOUT_VERIFICATION>
    <ShadowCullStateDTO size="32" alignment_proof="32 % 16 == 0">
      <field name="InstanceHash" offset="0" size="4" type="uint" />
      <field name="DistanceSq" offset="4" size="4" type="float" />
      <field name="CullFlags" offset="8" size="4" type="uint" />
      <field name="IlluminationScalar" offset="12" size="4" type="float" />
      <field name="_pad0.._pad15" offset="16..31" size="16" type="byte[16]" />
    </ShadowCullStateDTO>
    <ShadowCullCountersDTO size="64" false_sharing_guard="one cache line">
      <field name="EvaluatedCount" offset="0" size="4" />
      <field name="MainCulledCount" offset="4" size="4" />
      <field name="ShadowCulledCount" offset="8" size="4" />
      <field name="DarknessCulledCount" offset="12" size="4" />
      <field name="PointLightCulledCount" offset="16" size="4" />
      <field name="ShadowOnlyCount" offset="20" size="4" />
      <field name="DitheredCount" offset="24" size="4" />
      <field name="Flags" offset="28" size="4" />
      <field name="HzbCulledCount" offset="32" size="4" />
      <field name="SdfCulledCount" offset="36" size="4" />
      <field name="VisibleShadowCount" offset="40" size="4" />
      <field name="ProfileRuleCount" offset="44" size="4" />
      <field name="StateHash" offset="48" size="4" />
      <field name="_pad0" offset="52" size="4" />
      <field name="_pad1" offset="56" size="8" />
    </ShadowCullCountersDTO>
    <ShadowCullHzbTileDTO size="16" alignment_proof="16 % 16 == 0" fields="DepthMeters@0, OcclusionBiasMeters@4, TileHash@8, Flags@12" />
    <ShadowCullIndirectArgsDTO size="32" alignment_proof="32 % 16 == 0" fields="VertexCountPerInstance@0, InstanceCount@4, StartVertex@8, StartInstance@12, StartIndex@16, Flags@20, pads@24/28" />
  </STRUCT_LAYOUT_VERIFICATION>

  <SCALABILITY_CURVE>
    When `GlobalQualityWeight` drops below 0.3, `maxShadowDistance = lerp(20m, BaseShadowDistance, q)` collapses to the near band, minimum caster radius lerps toward the low-tier 1.25m gate, SDF occlusion threshold rises toward 0.55, HZB effective resolution moves toward the low 8-tile grid, point-light shadow allowance remains near zero until the ultra threshold, and the shader dither band hides the resulting shadow loss. At q=1.0, the same path admits smaller casters, longer shadow-only directional reach, conservative HZB bias, and point-light shadows according to deterministic allowance.
  </SCALABILITY_CURVE>

  <H_PHI_VAULT_STATUS private_native_allocations="0">
    <buffer id="71340" name="Instances" type="ShadowCullInstanceDTO" capacity="50000" />
    <buffer id="71341" name="States" type="ShadowCullStateDTO" capacity="50000" />
    <buffer id="71342" name="IlluminationScalars" type="float" capacity="50000" />
    <buffer id="71343" name="FrustumPlanes" type="float4" capacity="6" />
    <buffer id="71344" name="Counters" type="ShadowCullCountersDTO" capacity="1" />
    <buffer id="71345" name="TelemetryRing" type="CullingTelemetryEntry" capacity="300" />
    <buffer id="71346" name="RuntimeState" type="AbyssalShadowRuntimeStateDTO" capacity="1" />
    <buffer id="71347" name="ProfileRules" type="ShadowCullProfileRuleDTO" capacity="64" />
    <buffer id="71348" name="CsvScratch" type="byte" capacity="32768" />
    <buffer id="71349" name="HzbDepthTiles" type="ShadowCullHzbTileDTO" capacity="256" />
    <buffer id="71350" name="IndirectArgs" type="ShadowCullIndirectArgsDTO" capacity="1" />
  </H_PHI_VAULT_STATUS>

  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NoAlias>All NativeArray fields in owned jobs are marked `[NoAlias]` where applicable; read-only inputs use `[ReadOnly, NoAlias]`.</NoAlias>
    <Graph>SystemDispatcher Simulation dependsOn -> optional GenerateMockCullingDataJob -> optional GenerateMockHzbTilesJob -> EvaluateShadowCullingJob -> ReduceShadowCullTelemetryJob -> BuildShadowIndirectArgsJob -> returned JobHandle. VisualSync commits only when the handle is completed.</Graph>
    <BlockingPolicy>No arbitrary mid-frame `Complete()` in the scheduled path. Force-complete exists only in explicit editor/mock run and teardown.</BlockingPolicy>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>

  <COMPILE_GUARD>
    Runtime asmdef references Core, Core.Contracts, Core.Memory, World.Contracts, Unity.Burst, Unity.Collections, Unity.Jobs, and Unity.Mathematics. No concrete sibling runtime assembly reference was added. Static source scans passed for owned files. Full build was not rerun after polish because the user forbade build runs until needed and unrelated project compile blockers are already known.
  </COMPILE_GUARD>

  <DEAR_LIE_CONFIRMATION>
    The physical truth rejected here is per-caster lighting/voxel visibility simulation. The implemented fake is scalar darkness + HZB tile depth + Bayer dither dissolve. Before: O(n * renderer_state) plus potential O(n * rays * steps) if CPU occlusion were simulated. After: O(n) cull evaluation plus O(t) HZB tile seeding/readback, where t <= 256. The GPU receives one state buffer and one indirect args row.
  </DEAR_LIE_CONFIRMATION>

  <VERIFICATION>
    <static_scan>No owned-file hits for LINQ, foreach, new NativeArray/List/HashMap, Renderer.shadowCastingMode, math.sqrt, UnityEngine.Random, JobHandle.ScheduleBatchedJobs, or Shader.SetGlobalInteger.</static_scan>
    <diff_check>`git diff --check` passed for SHINOBU_134 code/shader/docs.</diff_check>
    <pending>Unity import, Burst Inspector, shader compile, Frame Debugger, Profiler, GCMonitor, Play Mode, and player build.</pending>
  </VERIFICATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 011

What was wrong: External producer and editor/control methods used `EnsureInitialized`, which also initializes GPU upload buffers. That allowed a producer resolving Vault input arrays to cause hidden GraphicsBuffer allocation.
What was done: Changed producer/tuner/CSV/snapshot access to `EnsureVaultBuffers` only. Added cold `OnEnable` prewarm through `EnsureInitialized` when the Vault is already present, leaving GPU buffer creation on explicit boot/simulation/visual paths.
Cinematic Cheats used: None added; this protects the data route that feeds HZB/SDF/dither fakes.
Exact Microseconds saved: 0 us measured. This prevents allocation jitter, not a measured frame improvement.
Verification: Static forbidden-pattern scan remains empty for owned files; DTO property/Pack/interface-array scan remains empty; no build was run.

<SELF_AUDIT agent_id="SHINOBU_134" pass="011" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="vault_only_producer_access">Tasks 09, 10, 15, and 20 are strengthened: external producers resolve only Vault input buffers and do not trigger GPU upload allocation.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No math change; allocation timing is now cleaner across low-to-ultra modes.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">Producer facade resolves Vault handles only; no private NativeArray/List/HashMap added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Dependency graph unchanged; producer buffer access is cleaner before handle registration.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling reference added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The fake remains data-driven; no Unity Renderer mutation or managed draw-list route introduced.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 010

What was wrong: VisualSync upload used `GraphicsBuffer.LockBufferForWrite` and unlocked normally, but did not guard the mapped range with `try/finally`.
What was done: Wrapped state-buffer and indirect-args buffer mappings in `try/finally` unlock guards while preserving raw `UnsafeUtility.MemCpy`, double buffering, and zero managed visible list construction.
Cinematic Cheats used: None added; this protects the indirect/dither data bridge that carries the existing shadow culling fake.
Exact Microseconds saved: 0 us measured. This prevents mapped-buffer poisoning under Editor/driver fault conditions rather than claiming frame-time savings.
Verification: Static forbidden-pattern scan remains empty for owned files; Burst directive scan still reports five exact attributes; `git diff --check` passes with only the existing CRLF warning on the architecture ledger.

<SELF_AUDIT agent_id="SHINOBU_134" pass="010" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="gpu_upload_unlock_guard">Task 10 is strengthened: the asynchronous indirect dispatch bridge now guarantees unlock attempts for both mapped GPU buffers.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No change; upload count still follows the culled active window and quality-driven flags.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new memory allocation; only control-flow guards around existing GraphicsBuffer mappings.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job graph unchanged; VisualSync resource release is stricter.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No new assembly reference.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>GPU receives the same state and indirect args fake; no CPU draw-list simulation added.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 009

What was wrong: `EvaluateShadowCullingJob` uses `Frame` in the point-light shadow hash. Runtime fallback frame identity still used Unity `Time.frameCount`, which is not a deterministic simulation tick source.
What was done: Removed every `Time.frameCount` usage from the SHINOBU_134 runtime. Scheduling now uses dispatcher `context.Frame`; if absent, the fallback advances from Vault `AbyssalShadowRuntimeStateDTO.Frame`. VisualSync/teardown telemetry reuses the scheduled/Vault frame.
Cinematic Cheats used: No new cheat; this keeps the existing point-shadow luxury lottery deterministic inside the presentation lane.
Exact Microseconds saved: 0 us measured. This is determinism hardening, not a performance claim.
Verification: Static forbidden-pattern scan now includes `Time.` and has zero owned-file hits; Burst directive scan still reports five exact attributes.

<SELF_AUDIT agent_id="SHINOBU_134" pass="009" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="deterministic_frame">Tasks 12, 14, 16, and 20 are strengthened: point-light culling hash, rollback exclusion, telemetry frame identity, and static verification no longer depend on Unity wall-frame time.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>DTO layout unchanged; frame is still `AbyssalShadowRuntimeStateDTO.Frame@16` and telemetry `Frame@0`.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No change to q-driven math. The low-to-ultra point-light allowance now samples a deterministic frame seed from dispatcher/Vault frame identity.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new memory lane; fallback frame identity uses existing RuntimeState buffer `71346`.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job dependency graph unchanged from pass 008; only frame source changed.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling reference added. Static scan confirms no `Time.` remains in owned SHINOBU_134 source.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Presentation-only point-shadow lottery remains a deterministic visual fake, not rollback truth.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 008

What was wrong: HZB tile lookup used raw AUP-local `center.xy` and depth used `center.z`, which is only valid for an identity camera basis. That is not acceptable for a GPU-generated HZB depth pyramid.
What was done: Added runtime/editor-settable HZB view basis vectors and static active facade. `EvaluateShadowCullingJob` now maps AABB centers to HZB tiles with `dot(center, right/up)` and computes front depth with `dot(center, forward) - dot(abs(forward), extents)`, with `normalizesafe` fallbacks and guarded span division.
Cinematic Cheats used: HZB remains a tile scalar fake, not CPU raycasting. The fake is now camera-basis coherent with the producer that generates the tile pyramid.
Exact Microseconds saved: 0 us measured. This adds minimal ALU to avoid wrong culls; it protects visual correctness while keeping O(n + t).
Verification: Static forbidden-pattern scan still has zero owned-file hits; Burst directive scan still reports five exact attributes; `git diff --check` passes with only the existing CRLF warning on the architecture ledger.

<SELF_AUDIT agent_id="SHINOBU_134" pass="008" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="hzb_camera_basis">Tasks 06, 10, 13, and 20 are strengthened: frustum/AUP-local culling now feeds HZB occlusion through explicit camera-basis vectors instead of world-axis assumptions.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>DTO layout unchanged; no new persistent buffers or Pack directives added.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q=0.3 the same basis-correct HZB path consumes fewer effective tiles and tighter bias; at q=1.0 it can use the full 16x16 tile buffer supplied by the external HZB producer.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new private NativeArray/List/HashMap allocation; basis vectors are scalar runtime fields passed into the Burst job by value.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>NoAlias fields unchanged; HZB producer handle still enters through pass 007 dependency ingress.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling reference added; basis is pure math on Unity.Mathematics float3.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Still O(n + t) HZB tile scalar rejection, not O(n*rays*steps) occlusion simulation.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 007

What was wrong: The culling lane had a correct internal job chain, but no explicit route for Lighting Probe Grid, HZB readback, or World/BRG producers to provide data and dependency fences. That forced either mock-data overwrites or hidden scheduling assumptions.
What was done: Added external producer APIs on `AbyssalShadowCullingRuntime`: `TryResolveProducerBuffers`, `RegisterExternalProducerDependency`, static active facades, active instance/HZB tile count handoff, producer telemetry flags, and `TryGetPublishedGpuBuffers`. `ScheduleCullingPass` now starts from `JobHandle.CombineDependencies(dependsOn, producerDependency)` and suppresses fallback mock seeding when external instance or HZB data is marked written.
Cinematic Cheats used: Still Bayer dither + HZB scalar depth + SDF scalar. The new route lets real GPU HZB readback data fill the same tiny tile DTOs instead of introducing CPU raycasts or voxel marches.
Exact Microseconds saved: 0 us measured. Expected saving is indirect: real active counts reduce stale-candidate evaluation and dependency chaining avoids main-thread producer completion.
Verification: Static forbidden-pattern scan still has zero owned-file hits; Burst directive scan still reports five exact attributes; strict XML extraction still reports `TASK_COUNT=20`.

<SELF_AUDIT agent_id="SHINOBU_134" pass="007" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="dependency_ingress">Tasks 09, 10, 16, and 20 are strengthened: external illumination/HZB producers now have a handle route, GPU buffers are published without managed visible lists, telemetry marks external inputs, and the dependency graph is explicit.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged from pass 006; aggregate validator still covers state=32B, counters=64B, HZB=16B, indirect=32B.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>External active counts and HZB tile counts can now shrink with the same `GlobalQualityWeight` policy owned by their producers; this culler consumes the resulting count and continues its own q-driven distance/radius/point-shadow collapse.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new NativeArray/List/HashMap fields were added; producer access resolves existing Vault handles `71340`, `71342`, `71343`, and `71349`.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Producer job handle -> `RegisterExternalProducerDependency` -> `CombineDependencies(dependsOn, producerDependency)` -> optional mock/HZB fallback -> Evaluate -> Reduce -> BuildIndirectArgs -> VisualSync upload. Jobs still use `[NoAlias]` on array fields.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling assembly reference added. The route is static facade + Vault + JobHandle only.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>External HZB data remains a 16-byte tile scalar fake, not a CPU visibility simulation. Complexity remains O(n + t), t <= 256.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 006

What was wrong: The editor validator proved only `ShadowCullStateDTO`, while the implementation also relies on 64-byte counters/telemetry/runtime state, 16-byte HZB tiles, and 32-byte indirect/profile records. The XML task was satisfied, but the ULTRA audit needed executable layout proof for the wider data lane.
What was done: Added `AbyssalShadowLayoutAudit.ValidateAllLayouts()` and exact offset checks for state, instance, counters, telemetry, runtime state, HZB tile, indirect args, tuner snapshot, profile rule, and CSV parse result DTOs. The UI Toolkit tuner and menu validator now call the aggregate check. `OnDrawGizmos` is now fenced with `#if UNITY_EDITOR`.
Cinematic Cheats used: None added in this pass; the existing cheats remain Bayer dither shadow dissolve, HZB tile scalar occlusion, and per-instance SDF scalar suppression.
Exact Microseconds saved: 0 us measured. This pass is prevention, not a frame-time claim: it blocks ARM64/cache-line layout regression before runtime profiling.
Verification: Corrected XML extraction reports `TASK_COUNT=20`; forbidden-pattern scan still has zero owned-file hits; Burst directive scan still reports five exact required attributes; `git diff --check` passes with only the existing CRLF warning on the architecture ledger.

<SELF_AUDIT agent_id="SHINOBU_134" pass="006" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS_STATIC">Legacy MonoBehaviour culling scan found no first-party owner script to delete; vendor/editor code left untouched.</TASK>
    <TASK id="02" status="PASS_STATIC">Shadow authority is data-driven through Vault state + indirect args, not LODGroup side effects.</TASK>
    <TASK id="03" status="PASS_STATIC">Hot DTOs use raw public fields; no properties added.</TASK>
    <TASK id="04" status="PASS_STATIC">`ValidateAllLayouts()` now proves state/instance/counter/telemetry/runtime/HZB/indirect/profile layout offsets.</TASK>
    <TASK id="05" status="PASS_STATIC">50k deterministic mock data path exists through Burst.</TASK>
    <TASK id="06" status="PASS_STATIC">`EvaluateShadowCullingJob` remains Burst, AUP-local, NoAlias, frustum + light-expanded.</TASK>
    <TASK id="07" status="PASS_STATIC">Squared-distance only; no `math.sqrt` in owned culling files.</TASK>
    <TASK id="08" status="PASS_STATIC">Bayer dither shader include handles shadow fade lie.</TASK>
    <TASK id="09" status="PASS_STATIC">Illumination + material + SDF/HZB scalar gates clear `CastShadows` in darkness/occlusion.</TASK>
    <TASK id="10" status="PASS_STATIC">VisualSync uses double-buffered `GraphicsBuffer.LockBufferForWrite` and indirect args buffer upload.</TASK>
    <TASK id="11" status="PASS_STATIC">Continuous `GlobalQualityWeight` drives distance, radius, HZB grid, SDF threshold, point-light allowance, and fade.</TASK>
    <TASK id="12" status="PASS_STATIC">Point-light shadows are luxury-only through smooth quality allowance near ultra.</TASK>
    <TASK id="13" status="PASS_STATIC">Frustum math subtracts camera AUP before float operations; helper localizes world planes.</TASK>
    <TASK id="14" status="PASS_STATIC">Shadow state is presentation-only and rollback-excluded.</TASK>
    <TASK id="15" status="PASS_STATIC">Vault requests use `NativeArrayOptions.UninitializedMemory` for steady-state buffers.</TASK>
    <TASK id="16" status="PASS_STATIC">300-frame telemetry ring and raw dump path exist.</TASK>
    <TASK id="17" status="PASS_STATIC">UI Toolkit tuner exists and now validates all layouts.</TASK>
    <TASK id="18" status="PASS_STATIC">CSV parser writes unmanaged profile rules from Vault scratch bytes.</TASK>
    <TASK id="19" status="PASS_STATIC">Gizmo callback is now editor-only via `#if UNITY_EDITOR`.</TASK>
    <TASK id="20" status="PASS_STATIC">Static scans and log audit are present; Unity/profiler proof remains pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION aggregate_validator="AbyssalShadowLayoutAudit.ValidateAllLayouts">
    <state size="32" fields="InstanceHash@0 DistanceSq@4 CullFlags@8 IlluminationScalar@12 pads@16..31" />
    <instance size="64" fields="CenterAUP@0 Extents@24 BoundsRadius@36 InstanceHash@40 ProfileHash@56 pad@60" />
    <counters size="64" false_sharing_guard="one_l1_line" fields="Evaluated@0 VisibleShadow@40 StateHash@48 pad64@56" />
    <hzb size="16" fields="Depth@0 Bias@4 Hash@8 Flags@12" />
    <indirect size="32" fields="VertexCount@0 InstanceCount@4 StartIndex@16 pad@28" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q=0.3, shadow distance lerps toward 20m, caster radius gate lerps high, SDF threshold becomes aggressive, HZB grid resolves lower, and point-light shadow allowance remains near zero; the dither shader hides the collapse.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0" buffers="71340..71350" />
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH noalias="true">dependsOn -> mock data/HZB optional jobs -> Evaluate -> Reduce -> BuildIndirectArgs -> returned JobHandle; VisualSync uploads only completed state.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Runtime asmdef references Core/Core.Contracts/Core.Memory/World.Contracts plus Unity packages only; no concrete sibling runtime dependency was added.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Per-caster physical occlusion/raycasting is replaced by scalar darkness, 16x16 HZB tile depth, SDF occlusion scalar, and Bayer shader dissolve: O(n + t) instead of O(n*rays*steps).</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 012

What was wrong: The Vault lock safety fix existed in source but was not recorded in the status/rationale/ledger chain, which made the ownership proof incomplete for a multi-agent workspace.
What was done: Verified `TryLockJobBuffers(out lockedCount)` fails scheduling closed, records `TelemetryFlagVaultLockFailed`, reverse-unlocks only the acquired subset, and does not clear producer handoff data before the lock succeeds. Updated the disk forensic trail.
Cinematic Cheats used: None added; this protects the same HZB/SDF/Bayer fake route from running against contested Vault memory.
Exact Microseconds saved: 0 us measured. The value is preventing a stall or ownership corruption; runtime timing remains pending.
Verification: Source grep confirms the fail-fast lock route in `AbyssalShadowCullingRuntime.cs`; build still not launched per user instruction.

<SELF_AUDIT agent_id="SHINOBU_134" pass="012" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="vault_lock_failfast">Tasks 10, 15, 16, and 20 are strengthened: the indirect dispatch chain now schedules only after Vault ownership is acquired, uses uninitialized Vault memory under a lock fence, records lock failure telemetry, and has documented proof.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged: primary `ShadowCullStateDTO` remains 32B; counter/telemetry/runtime DTOs remain 64B.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No q-curve change. Under contention, the frame skips scheduling rather than blocking; the next successful frame resumes the same continuous quality math.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No NativeArray/List/HashMap allocation added; lock state is a stack `int lockedCount` and existing telemetry flag.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Lock success gates producer handle consumption. Failure path returns `dependsOn`; success path remains `CombineDependencies(dependsOn, producerDependency) -> mock/HZB optional -> Evaluate -> Reduce -> BuildIndirectArgs`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling reference added; this is owner-local runtime control flow.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The visual fake remains O(n + t). The lock guard prevents contested memory access; it does not add simulation or renderer mutation.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 013

What was wrong: `RunMockCullingOnce()` could return success after `ScheduleCullingPass` declined to schedule because `CompletePendingJob()` returns true when `_jobPending` is already false. That made the editor/CI 50k mock path capable of lying.
What was done: Added a `_jobPending` gate immediately after `ScheduleCullingPass`. If Vault locks fail or scheduling exits early, the mock facade returns `false` and does not claim a completed pass.
Cinematic Cheats used: None added; this protects the synthetic proof route for the existing mathematical culling fake.
Exact Microseconds saved: 0 us measured. This removes a false-positive verification path.
Verification: Source grep confirms `RunMockCullingOnce()` now requires `_jobPending` after scheduling; runtime proof remains pending.

<SELF_AUDIT agent_id="SHINOBU_134" pass="013" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="mock_facade_fail_closed">Tasks 05, 16, 17, and 20 are strengthened: the synthetic 50k mock path, telemetry proof, editor tuner, and self-audit route now fail closed when no job was scheduled.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No runtime q-curve change; the proof facade now accurately reports whether the q-driven jobs actually ran.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new memory allocation or buffer ID; one branch checks existing `_jobPending` state.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Failure path remains unscheduled. Success path still produces `Evaluate -> Reduce -> BuildIndirectArgs -> CompletePendingJob` proof.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No CPU simulation added; the patch only prevents false reporting of the 50k dither/HZB/SDF culling proof pass.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 014

What was wrong: `GenerateMockHzbTilesJob` used `math.length(uv)` for tile radial falloff. That hides a sqrt in a Burst job, undermining the squared-distance discipline used by the real culling pass.
What was done: Replaced `math.length` with `math.dot(uv, uv)` and a squared radial scalar for HZB mock occluder generation.
Cinematic Cheats used: The HZB remains a tile-depth scalar fake; this pass makes the fake cheaper instead of more physically accurate.
Exact Microseconds saved: Unmeasured and expected small because the fallback tile count is <=256. One sqrt-class operation per mock HZB tile is removed.
Verification: Static ALU scan now has no `math.length`, `math.distance`, `Vector3.Distance`, or `.magnitude` hits in owned SHINOBU_134 source.

<SELF_AUDIT agent_id="SHINOBU_134" pass="014" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="hzb_mock_sqrt_free">Tasks 05, 07, 11, and 20 are strengthened: fallback mock data, squared-distance policy, scalability HZB path, and static audit now avoid hidden sqrt via `math.length`.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q=0.3 the mock HZB still uses coarse effective resolution and aggressive occluder bias; the radial falloff is now dot-product based at every quality weight.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new buffers or allocations.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job graph unchanged; only arithmetic inside `GenerateMockHzbTilesJob` changed.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>HZB remains O(t) scalar tile fake, t <= 256; no CPU raycast, voxel march, or renderer mutation added.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 015

What was wrong: `AbyssalShadowLayoutAudit` lived in runtime DTO source and used reflection to fetch field offsets. The validator is editor proof, not player/runtime logic.
What was done: Moved the layout audit class into the Editor facade file under `#if UNITY_EDITOR`, preserving UI Toolkit and menu validation while removing reflection proof code from runtime source.
Cinematic Cheats used: None added. This is compile/player hygiene around the existing culling fake.
Exact Microseconds saved: 0 us measured. This reduces accidental runtime reflection surface, not frame cost.
Verification: Source grep confirms runtime SHINOBU_134 files no longer declare `AbyssalShadowLayoutAudit` or call `typeof(T).GetField`; editor facade still exposes `ValidateAllLayouts()`.

<SELF_AUDIT agent_id="SHINOBU_134" pass="015" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="editor_only_layout_audit">Tasks 04, 17, and 20 are strengthened: layout validation remains editor-accessible while runtime DTO/source files stay free of reflection validation helpers.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged offsets and sizes; proof route moved to editor-only code.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No runtime math change.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No buffer or allocation change.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No job graph change.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>Runtime assembly surface shrinks; editor assembly owns reflection proof and references runtime contract.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No simulation added; proof code moved out of runtime.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 016

What was wrong: The Bayer dither shader clipped every caster by `IlluminationScalar`, even when the Burst job had not marked `DitherFadeActive`. That made accepted but dim objects render noisy shadows instead of solid admitted shadows.
What was done: Added HLSL constants for `CastShadows` and `DitherFadeActive`, and skipped Bayer threshold work unless `DitherFadeActive` is set.
Cinematic Cheats used: Bayer dither remains the Dear Lie, now correctly scoped to fade-band dissolves only.
Exact Microseconds saved: Unmeasured. Non-fading casters avoid Bayer threshold ALU; expected saving is small per caster but removes permanent visual noise.
Verification: Static shader/source grep confirms named flag constants and dither flag gate are present; shader compilation remains pending.

<SELF_AUDIT agent_id="SHINOBU_134" pass="016" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="fade_band_dither_gate">Tasks 08, 09, 11, and 20 are strengthened: dither is now a fade-band visual fake, darkness culling remains the actual no-shadow gate, quality culling controls when fade occurs, and shader/static audit reflects this route.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged: no DTO field added; 32B state ABI preserved.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q=0.3 more casters enter the fade band near the shortened distance threshold, but admitted non-fading casters remain solid until flagged.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No memory change.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No job graph change.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No C# assembly reference change; shader include only.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Bayer dither is now explicitly gated by `DitherFadeActive`: O(1) shader clip fake only for fade-band casters, not a permanent dim-light shadow simulation.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 017

What was wrong: CSV profile reload cleared live profile rules before parsing. A bad CSV could wipe designer culling rules and make subsequent frames run with default profile behavior.
What was done: `LoadProfileCsv()` now parses first, requires `ParsedRuleCount > 0`, then clears only the stale tail beyond the parsed prefix. Zero-valid-row input returns `false` and preserves the previous live rules.
Cinematic Cheats used: None added. This protects human tuning for the existing culling/dither/HZB fake.
Exact Microseconds saved: 0 us measured. Cold reload path only; this is data integrity.
Verification: Static source grep confirms `MemClear` moved behind successful parse and tail count calculation.

<SELF_AUDIT agent_id="SHINOBU_134" pass="017" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="csv_fail_closed">Tasks 17, 18, and 20 are strengthened: designer facade/hot reload no longer erases last-good unmanaged rules when input is invalid.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Profile-driven distance/radius/darkness/fade scalars remain stable across bad reloads at every quality weight.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No persistent staging array added; existing Vault scratch and rule table are reused.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No job graph change; cold profile mutation remains outside scheduled culling ownership.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>CSV rules continue to tune the same mathematical fake; bad content cannot erase the last valid fake parameters.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 018

What was wrong: Frustum plane writes and CSV profile reload could mutate NativeArrays that `EvaluateShadowCullingJob` reads if a control/editor call landed while `_jobPending` was true.
What was done: Added `_jobPending` gates to `SetLocalizedFrustumPlanes()` and `LoadProfileCsv()`. Contested mutations now fail/no-op and can retry after VisualSync completes the scheduled job.
Cinematic Cheats used: None added. This protects the data feeding frustum/HZB/dither culling fakes.
Exact Microseconds saved: 0 us measured. This prevents race conditions and main-thread forced completion.
Verification: Static grep confirms both mutation facades guard on `_jobPending`.

<SELF_AUDIT agent_id="SHINOBU_134" pass="018" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="scheduled_reader_mutation_gate">Tasks 06, 13, 18, and 20 are strengthened: frustum and profile data cannot be mutated by control paths while the Burst reader is scheduled.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>No q-curve change; this protects the inputs that q-scaled culling consumes.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No staging allocation added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Control mutation path now checks `_jobPending`; scheduled path remains `producerDependency -> Evaluate -> Reduce -> BuildIndirectArgs`.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No simulation added; the same scalar fake inputs are protected from races.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 019

What was wrong: CSV reload could still partially mutate live rules if a file had valid rows before malformed rows, and over-capacity rows were silently ignored.
What was done: Added `AbyssalShadowProfileCsv.Validate()` as a no-commit byte scan. `LoadProfileCsv()` now rejects zero-row, malformed, or over-capacity input before committing the second parse into the Vault profile table.
Cinematic Cheats used: None added. This protects designer control over the existing mathematical shadow fake.
Exact Microseconds saved: 0 us measured. Cold path scans twice intentionally to protect last-good state.
Verification: Static source grep confirms validation runs before commit parse and `RejectedLineCount != 0` rejects reload.

<SELF_AUDIT agent_id="SHINOBU_134" pass="019" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="csv_transactional_reload">Tasks 18 and 20 are strengthened: profile ingestion is now no-commit validated before live Vault mutation.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Quality-scaled profile rules now update only from fully valid CSV content; bad content preserves last-good behavior across all weights.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No staging NativeArray added; validation reuses the byte scratch and performs no-commit parsing.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No scheduled job graph change; mutation remains gated by `_jobPending` from pass 018.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No simulation added; valid CSV only adjusts scalar culling/dither fake parameters.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 020

What was wrong: The custom byte-level float parser accepted valid numeric prefixes and ignored trailing token garbage. `1abc` could become `1.0`.
What was done: Added an exact token-end check to `TryParseFloat()` after digit/fraction parsing. Any trailing non-whitespace content now rejects the scalar and fails the transactional CSV validation.
Cinematic Cheats used: None added. This protects scalar tuning for the shadow culling fake.
Exact Microseconds saved: 0 us measured. Cold parser validation only.
Verification: Static source grep confirms the parser now checks `cursor != end`.

<SELF_AUDIT agent_id="SHINOBU_134" pass="020" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="csv_scalar_exhaustion">Task 18 is strengthened: byte-level CSV float tokens must be fully consumed before unmanaged rule commit.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Malformed scalar text cannot skew any quality-weight curve.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No allocation change.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No job graph change.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No simulation added; parser only protects scalar fake tuning.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 021

What was wrong: Shadow-state gates were immediate threshold decisions. Distance, darkness, SDF, radius, and frustum edges could flip on small camera/quality changes. Point-light culling used a deterministic but frame-varying hash, which can visually reroll shadows every frame.
What was done: `EvaluateShadowCullingJob` now reads the previous `ShadowCullStateDTO` only when `InstanceHash` matches and applies previous-state hysteresis: 3-5 m for distance/frustum, scalar bands for darkness/SDF, radius bands for small casters, and point-budget hysteresis. Point-light admission now uses an instance-stable hash.
Cinematic Cheats used: No physical simulation added. The Dear Lie remains scalar shadow admission plus Bayer dissolve; hysteresis only stabilizes the fake.
Exact Microseconds saved: Unmeasured. Expected cost is one previous-state read plus scalar comparisons per candidate; expected win is fewer false shadow-map draw bursts from threshold chatter.
Verification: Static grep found no `math.sqrt`, `math.length`, `Vector3.Distance`, `.magnitude`, LINQ, new Native containers, `Renderer.shadowCastingMode`, `UnityEngine.Random`, `UnityEngine.Time`, `Time.frameCount`, `JobHandle.ScheduleBatchedJobs`, or `Shader.SetGlobalInteger` in owned files after the patch.

<SELF_AUDIT agent_id="SHINOBU_134" pass="021" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="previous_state_hysteresis">Tasks 06, 07, 11, 12, and 20 are strengthened: the Burst culler remains squared-distance/AUP-local, but threshold decisions now have previous-state hysteresis and point-light shadows no longer reroll by frame.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged. `ShadowCullStateDTO` remains explicit 32B: InstanceHash@0 u32, DistanceSq@4 f32, CullFlags@8 u32, IlluminationScalar@12 f32, pad bytes @16..31.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Below q=0.3 the nominal distance still collapses toward 20m and small/SDF/darkness gates stay aggressive; previously culled casters must re-enter through tighter thresholds, while previously admitted casters get a bounded hysteresis band to prevent visual chatter.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No new persistent buffer. Previous state is read from the existing Vault `States` lane after `InstanceHash` validation.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job graph unchanged: producer dependency -> optional mock/HZB -> Evaluate -> Reduce -> BuildIndirectArgs. `States` remains the single read/write lane for the cull state.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The fake is still mathematical shadow suppression plus dithered dissolve, O(n) over candidates. Hysteresis avoids flicker without CPU raycasts, GameObject toggles, or extra renderer state.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 022

What was wrong: The mock data generator seeds `States` with `CastShadows` and `DistanceSq=0`. Pass 021 could treat that seeded state as valid previous history and apply relaxed hysteresis before a real cull result existed.
What was done: Previous-state hysteresis now requires matching `InstanceHash`, finite positive `DistanceSq`, and no `NonFinite` flag before it consumes prior `CullFlags`.
Cinematic Cheats used: None added. This keeps the shadow fake deterministic on its first evaluated frame.
Exact Microseconds saved: 0 us measured. Adds two scalar validity checks; prevents seeded-state bias.
Verification: Static source grep confirms the previous-state gate includes `math.isfinite(previousState.DistanceSq)`, `previousState.DistanceSq > 0f`, and `NonFinite` rejection.

<SELF_AUDIT agent_id="SHINOBU_134" pass="022" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="hysteresis_seed_guard">Tasks 05, 06, 11, 12, and 20 are strengthened: synthetic seed rows no longer count as culling history.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged. The 32B `ShadowCullStateDTO` ABI is not expanded.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Low-tier first-frame culling stays aggressive because seeded `CastShadows` rows do not relax thresholds; all tiers receive hysteresis only after a real evaluated distance exists.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No history buffer added; validity is derived from the existing state row.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No graph change.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No simulation added; this only prevents seeded-state bias in the culling fake.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 - SHINOBU_134 Polish Pass 023

What was wrong: `ApplyTunerSettings()` could mutate `AbyssalShadowRuntimeStateDTO` while a culling job was pending. The job used captured scalars, but completion telemetry reads the runtime row, so the blackbox could report settings that were not used by the frame.
What was done: Added the same `_jobPending` fail-closed guard already used by frustum-plane writes and CSV reload.
Cinematic Cheats used: None added. This protects the tuning route for the existing culling/dither fake.
Exact Microseconds saved: 0 us measured. Prevents telemetry corruption and avoids force-completing a job from the tuner path.
Verification: Static source grep confirms `ApplyTunerSettings()` now returns when `_jobPending` is true.

<SELF_AUDIT agent_id="SHINOBU_134" pass="023" status="PENDING_RUNTIME_VERIFICATION">
  <TASK_RECONCILIATION update="tuner_mutation_gate">Tasks 11, 16, 17, and 20 are strengthened: live tuning no longer mutates runtime telemetry inputs during a scheduled culling frame.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>Unchanged.</STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Quality and tuner changes still apply continuously, but only on clean frame boundaries after the pending job is resolved.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_allocations="0">No staging buffer added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No scheduled graph change; control writes now fail closed while `_jobPending` is true.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No assembly reference change.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>No simulation added; this only protects scalar fake tuning.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
