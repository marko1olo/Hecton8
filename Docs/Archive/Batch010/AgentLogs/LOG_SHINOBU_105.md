# LOG_SHINOBU_105

## 2026-05-19 - Abyssal Current Fluid Dynamics Pass

What was wrong:
- Macro biomass and micro boid visuals were already partially routed through `ShinobuEcosystemBalancer`, but the route was capped at 5,000 entities and still used ambiguous render matrix storage.
- Explicit SHINOBU_105 DTOs were missing: no exact 32B `BoidStateDTO`, no explicit 64B matrix payload, no profile DTO for swarm species CSV.
- Existing Burst jobs lacked the required `CompileSynchronously = true` directive and `[NoAlias]` field proof.
- The wall-avoidance response used normal push, not the requested Dear Lie vortex swirl.
- Telemetry dumped `Dump_ECOSYSTEM.*`, not the required agent-keyed `Dump_SHINOBU_105.bin`, and did not flag the 1.5ms solve threshold.
- Editor facade was still named Biomass & Boid Tuner and lacked solve-time graph/vector-field diagnostics.

What was done:
- Raised SHINOBU entity capacity to 100,000 and made active budget continuous through `HomeostasisBrain.GlobalQualityWeight`.
- Added explicit DTOs: `BoidStateDTO` 32B, `BoidTargetDTO` 32B, `BoidMatrixDTO` 64B, `BoidIndirectArgsDTO` 16B, `AbyssalFlowTensorDTO` 64B, `SwarmSpeciesProfileDTO` 32B.
- Changed `ShinobuRenderMatrices` Vault handle from `float4x4` to `BoidMatrixDTO` and requested large state/render buffers with `NativeArrayOptions.UninitializedMemory`.
- Renamed the steering and hash jobs to `BoidFlockingJob` and `BuildBoidSpatialHashJob`; rollback-owned SHINOBU jobs now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`.
- Added `[NoAlias]` to job NativeArray fields.
- Added `GenerateEmergencyMockFlowJob`, `GenerateEmergencyMockFlow()`, and `SampleEmergencyMockFlow()` for deterministic fallback flow.
- Added cross-product SDF wall swirl: `cross(up, wallNormal)` with quality-scaled strength.
- Added mapped matrix upload shim using `GraphicsBuffer.LockBufferForWrite` + `UnsafeUtility.MemCpy`.
- Added indirect argument DTO/job and `TryDrawProceduralIndirect()` seam.
- Added cold `ParseSwarmSpeciesProfiles()` parser for `swarm_species_profiles.csv`.
- Converted the editor facade into `Abyssal Swarm Tuner` with UI Toolkit host, Vault sliders, telemetry graph, hash grid, and SceneView flow/boid vectors.

Cinematic Cheats used:
- Triangle/curl fake flow instead of true fluid simulation.
- SDF-normal vortex swirl instead of pathfinding or Navier-Stokes around caves.
- Continuous quality curves collapse Reynolds alignment/cohesion and neighbor caps before cutting visual authority.

Exact microseconds saved:
- Measured: not available. Compile/profiler verification was blocked because total CPU remained ~98-100%, and the user explicitly forbade builds under >50% CPU.
- Hard savings with objective byte counts: 100,000 matrices at 64B = 6.4MB now avoid boot zero-fill via `UninitializedMemory`; managed matrix array copy is avoided by mapped `GraphicsBuffer` upload.

Compile status:
- `dotnet build` was not launched. CPU gate check: no `dotnet`/`csc` process was active, but total CPU repeatedly measured ~98-100%.
- Static scans passed for touched files: no `Pack=1`, `Instantiate`, `new GameObject`, managed matrix arrays, hot-path `foreach`, or old job names.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Legacy particle/fish archaeology run. ParticleSystem hits are unrelated support/construction/silt domains; no fish ParticleSystem authority found.</TASK>
    <TASK id="02" status="PASS">Old `HectonBoidController` GUID has no prefab/scene reference. No `BoidController`/`FishAI` prefab attachment found.</TASK>
    <TASK id="03" status="PASS">`BoidStateDTO` explicit 32B, public fields only.</TASK>
    <TASK id="04" status="PASS">`BoidMatrixDTO` explicit 64B, four 16B columns.</TASK>
    <TASK id="05" status="PASS">`GenerateEmergencyMockFlowJob` and scheduler helper added.</TASK>
    <TASK id="06" status="PASS">`BoidFlockingJob` Burst IJobParallelFor reads snapshots, writes next state, samples hash/flow.</TASK>
    <TASK id="07" status="PASS">Macro rehydrate converts 100 biomass to 1 boid and applies quality density.</TASK>
    <TASK id="08" status="PASS">Dear Lie vortex swirl uses `cross(up, wallNormal)`.</TASK>
    <TASK id="09" status="PASS">`BuildBoidSpatialHashJob` builds contiguous bucket heads/next links.</TASK>
    <TASK id="10" status="PASS">Matrix job writes `BoidMatrixDTO`; GPU upload shim uses mapped buffer memcpy.</TASK>
    <TASK id="11" status="PASS">Active budget and math complexity scale through `GlobalQualityWeight`.</TASK>
    <TASK id="12" status="PASS">Predator impulse is signal-driven and collider-free through fauna strike/`MockPredatorSignal` route.</TASK>
    <TASK id="13" status="PASS">AUP offset/write path wraps through 5000m sector math in `FromAbsoluteDouble3`.</TASK>
    <TASK id="14" status="PASS">Indirect args DTO/job and procedural indirect draw seam added.</TASK>
    <TASK id="15" status="PASS">Snapshot read and next-state write enforce deterministic parallel resolve.</TASK>
    <TASK id="16" status="PASS">Large entity/AUP/render buffers use `UninitializedMemory`.</TASK>
    <TASK id="17" status="PASS">300-frame telemetry ring dumps `Dump_SHINOBU_105.bin` and `.h8dump` on fault or >1.5ms solve.</TASK>
    <TASK id="18" status="PASS">`Abyssal Swarm Tuner` editor facade reads Vault and graphs telemetry.</TASK>
    <TASK id="19" status="PASS">Cold FNV CSV species profile parser added.</TASK>
    <TASK id="20" status="PASS">SceneView vector diagnostic added. It replaces runtime `OnDrawGizmos` to preserve no-GameObject swarm authority.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <BoidStateDTO size="32">
      <field name="AUP" offset="0" size="24"/>
      <field name="SpeciesID" offset="24" size="2"/>
      <field name="PackIndex" offset="26" size="2"/>
      <field name="Speed" offset="28" size="4"/>
    </BoidStateDTO>
    <BoidMatrixDTO size="64">
      <field name="C0" offset="0" size="16"/>
      <field name="C1" offset="16" size="16"/>
      <field name="C2" offset="32" size="16"/>
      <field name="C3" offset="48" size="16"/>
    </BoidMatrixDTO>
    <BoidIndirectArgsDTO size="16">DrawProceduralIndirect row: vertex count offset 0, instance count offset 4, start vertex offset 8, start instance offset 12.</BoidIndirectArgsDTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Below `GlobalQualityWeight` 0.3, active budget tends toward 1,000, update stride rises toward 12, neighbor samples collapse toward 4, spatial hash chain cap drops toward 8, visible cone narrows, and alignment/cohesion lerp toward zero. Flow-follow and predator panic remain active to preserve life without full Reynolds cost.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Zero private persistent `NativeArray`, `NativeList`, or `NativeHashMap` allocations were introduced. Existing Vault IDs used: `ShinobuAmbientEntities`, `ShinobuAmbientAups`, `ShinobuBoidStates`, `ShinobuAmbientEntitySnapshot`, `ShinobuAmbientAupSnapshot`, `ShinobuBoidStateSnapshot`, `ShinobuEcosystemSectors`, `ShinobuEcosystemTuning`, `ShinobuEcosystemCounters`, `ShinobuEcosystemTelemetryRing`, `ShinobuSpatialHashDebugCells`, `ShinobuRenderMatrices`, `ShinobuRenderCustomData`, `ShinobuBoidIndirectArgs`, `ShinobuSpatialHashBucketHeads`, `ShinobuSpatialHashNext`, `ShinobuEcosystemCsvScratch`, `ShinobuEcosystemLegacyScratch`, `ShinobuSwarmSpeciesProfiles`.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    Jobs: `LocalShiftAndSpatialHashJob` -> `BuildBoidSpatialHashJob` -> optional `BuildHashDebugCellsJob` -> `BoidFlockingJob` -> `BuildShinobuRenderPayloadJob` -> `CountTelemetryCountersJob` -> `WriteBoidIndirectArgsJob`. NativeArray fields in changed jobs are marked `[NoAlias]` where applicable.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No asmdef edits were made. No new direct concrete sibling assembly dependency was introduced by SHINOBU_105. Compile verification is pending CPU gate.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: obstacle avoidance could drift toward pathfinding/fluid solve, O(N * terrain query complexity). After: O(N) SDF sample plus cross-product swirl and triangle/curl fake flow. No Unity Physics query is used.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-19 Loop 6 Pointer Hardening Addendum

What was wrong:
- The first implementation defined the required explicit DTOs, but the hottest jobs still used `NativeArray<T>[index]` as the visible row-access pattern. That was not strong enough against the CS1612/defensive-copy mandate.

What was done:
- `LocalShiftAndSpatialHashJob` now obtains raw `AmbientEntityDTO*` / `AmbientEntityAupDTO*` / snapshot pointers and mutates row refs through `UnsafeUtility.AsRef`.
- `BoidFlockingJob` now writes output rows by pointer refs, reads previous-frame snapshots through read-only pointers, and passes pointer bases into the bounded neighbor query.
- `BuildShinobuRenderPayloadJob` now writes `BoidMatrixDTO` and custom data through pointer refs instead of NativeArray index stores.

Cinematic Cheats used:
- Unchanged from the main report: deterministic triangle/curl current fake plus SDF-wall cross-product swirl. The Loop 6 change is structural hardening, not a new visual trick.

Exact Microseconds saved:
- Still not measured. `Get-Counter '\Processor(_Total)\% Processor Time'` returned `100`, and no `dotnet`/`csc` process was active, so the user CPU gate forbids build/profiler execution. Claimed savings remain theoretical until the gate opens.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="6">
  <CS1612_POINTER_ROUTE status="PASS">Hot row mutation now uses `NativeArrayUnsafeUtility` pointers and `UnsafeUtility.AsRef` in local shift, flocking, neighbor sampling, and render payload jobs.</CS1612_POINTER_ROUTE>
  <COMPILE_GATE status="BLOCKED">No build launched because total CPU sampled at 100%.</COMPILE_GATE>
  <DIRECT_DEPENDENCY_RECHECK status="PASS">`Hecton8.World` symbols used by this file are in the existing core assembly route; no new sibling asmdef reference was added.</DIRECT_DEPENDENCY_RECHECK>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 51 GPU Density Double-Dip Removal

What was wrong:
- GPU culling applied an automatic quality density step after the CPU active budget had already reduced the active swarm. At q=0.1 this could render roughly 1% of capacity instead of the mandated 5% active swarm when compute culling was bound.

What was done:
- Removed `qualityDensityStep` from `ResolveGpuCullingParams()`.
- Kept explicit caller density step support, clamped to 1..8.
- Left HZB and frustum culling intact.
- Re-ran source slice, forbidden-pattern scan, Burst parity, diff check, and CPU/compiler gate.

Cinematic Cheats used:
- Population LOD stays owner-local in the CPU active-budget curve. The GPU pass remains a visibility fake, not a second population authority.

Exact Microseconds saved:
- No CPU hot-path microseconds claimed. This prevents visual underpopulation at low quality; GPU cost may increase relative to the invalid double-decimated path, but q=0.1 is still only 5k/100k before visibility culling.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="51">
  <ONE_OWNER_DENSITY status="PASS">`GlobalQualityWeight` population density is owned by `ResolveActiveEntityBudget()`; GPU density no longer auto-double-dips the curve.</ONE_OWNER_DENSITY>
  <Q01_ANCHOR status="PASS">At q=0.1, active/render input remains 5% before frustum/HZB visibility, matching the XML assignment.</Q01_ANCHOR>
  <EXPLICIT_DENSITY_STEP status="PASS">Caller-owned density step remains available and clamped to 1..8.</EXPLICIT_DENSITY_STEP>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 50 HZB Z-Buffer Parameter Guard

What was wrong:
- The optional GPU HZB route validated depth texture shape but still trusted caller `_ZBufferParams`. Bad params can make `LinearEyeDepth` meaningless inside the compute culler.

What was done:
- Added `_proceduralCullHasValidZBufferParams`.
- Added `IsUsableZBufferParams(Vector4)` with finite checks and denominator-capability check.
- Bound a safe fallback vector when caller params are invalid.
- Required valid z-buffer params before enabling depth occlusion.
- Preserved frustum and density culling when HZB depth is disabled.

Cinematic Cheats used:
- The route remains a GPU-side visibility fake: density hash decimation + frustum test + optional HZB depth test. No CPU raycasts, occlusion queries, or per-fish renderer state were added.

Exact Microseconds saved:
- Pending profiler proof. Runtime CPU cost added is one cold bind-time validation and one boolean check. The saved failure mode is corrupted depth culling without falling back to CPU visibility tests.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="50">
  <HZB_ZBUFFER_GUARD status="PASS">Depth occlusion now requires finite usable `_ZBufferParams`; invalid params disable HZB only.</HZB_ZBUFFER_GUARD>
  <SCALABILITY status="PASS">Low quality remains density/frustum culled; HZB requires quality >= 0.3 through the existing continuous gate.</SCALABILITY>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=95.5, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity shader import, render-frame culling proof, and profiler timings remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 7 Determinism Addendum

What was wrong:
- Runtime swarm scheduling still used Unity frame/time inputs for lane hashing, macro RNG frame, telemetry frame, and integration delta. That violates the rollback posture even when the jobs are otherwise double-buffered.

What was done:
- Added `_simulationFrameCounter` and advanced it only after the swarm job schedule is accepted.
- Routed flocking update-stride lane hashing, macro `Frame`, and telemetry `Frame` through the local deterministic counter.
- Replaced variable `deltaTime` integration with a deterministic fixed tick curve: `1/60s` at full quality, `12/60s` at minimum quality.

Cinematic Cheats used:
- Same visual fakes as before; this pass removed nondeterministic time authority instead of adding simulation.

Exact Microseconds saved:
- No measured saving claimed. This change buys rollback correctness, not raw frame time. Build remains blocked because CPU is sampled at 100%.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="7">
  <ROLLBACK_TIME_AUTHORITY status="PASS">No runtime `Time.frameCount` or variable `deltaTime` remains in the swarm simulation path.</ROLLBACK_TIME_AUTHORITY>
  <SIMULATION_TICK_DELTA status="PASS">Critical boid integration uses deterministic `DefaultSimulationTickDeltaSeconds` scaled continuously by `GlobalQualityWeight`.</SIMULATION_TICK_DELTA>
  <COMPILE_GATE status="BLOCKED">No build launched because total CPU sampled at 100%.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 8 GPU Args Guard Addendum

What was wrong:
- The indirect args upload helper trusted caller-provided `GraphicsBuffer` shape.

What was done:
- `TryUploadIndirectDrawArgs()` rejects zero-count buffers and stride mismatches before mapping. Loop 14 later superseded the indexed stride with `UnsafeUtility.SizeOf<BoidIndirectArgsDTO>()` for the 16B `DrawProceduralIndirect` row.

Cinematic Cheats used:
- No new fake. This is render-pipeline ABI hygiene.

Exact Microseconds saved:
- None claimed. This prevents a bad buffer route from failing the draw path; build/profiler still blocked by 100% CPU.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="8">
  <INDIRECT_ARGS_ABI status="PASS">Mapped indirect args buffer is validated for count and stride before write.</INDIRECT_ARGS_ABI>
  <COMPILE_GATE status="BLOCKED">No build launched because total CPU sampled at 100%.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 9 Species CSV Vault Route Addendum

What was wrong:
- `swarm_species_profiles.csv` could be selected by the old CSV resolver and fed into tuning parsing. The species parser existed, but it was not wired to a persistent unmanaged Vault lookup.

What was done:
- Added `BufferID.ShinobuSwarmSpeciesProfiles = 70443`.
- Added `_swarmSpeciesProfileHandle` and allocated 64 `SwarmSpeciesProfileDTO` rows through `GlobalDataVault`.
- Split CSV resolution into `ResolveTuningCsvPath()` and `ResolveSwarmSpeciesCsvPath()`.
- Added `MonitorSwarmSpeciesProfiles()` to parse species CSV bytes into unmanaged profile rows during cold ticks.

Cinematic Cheats used:
- No new simulation. This preserves the biomass-to-visual-profile bridge without runtime managed dictionaries.

Exact Microseconds saved:
- No measured runtime saving. The value is 0 B/frame by construction: species profile linkage is cold-loaded into Vault-owned unmanaged rows.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="9">
  <H_PHI_VAULT_STATUS status="PASS">New Vault buffer ID: `ShinobuSwarmSpeciesProfiles` with 64 unmanaged `SwarmSpeciesProfileDTO` rows.</H_PHI_VAULT_STATUS>
  <CSV_ROUTE status="PASS">Tuning CSV and species-profile CSV now resolve through separate cold routes.</CSV_ROUTE>
  <COMPILE_GUARD status="JUSTIFIED_CORE_TOUCH">`H8Memory.cs` was touched only to declare one domain-owned BufferID required for Vault data sovereignty.</COMPILE_GUARD>
  <COMPILE_GATE status="BLOCKED">No build launched because total CPU sampled at 100%.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 10 Indirect Args Vault Route Addendum

What was wrong:
- The indirect draw dispatcher had DTO/job/helper code, but no runtime-owned Vault row. That left Task 14 dependent on external caller discipline instead of a one-fact/one-owner route.

What was done:
- Added `BufferID.ShinobuBoidIndirectArgs = 70444`.
- Added `_indirectArgsHandle` and a one-row `BoidIndirectArgsDTO` Vault allocation with `NativeArrayOptions.UninitializedMemory`.
- Included the indirect args buffer in SHINOBU job lock/unlock coverage.
- Chained `WriteBoidIndirectArgsJob` after `CountTelemetryCountersJob`; Loop 14 later corrected the ABI to a 16B `DrawProceduralIndirect` row.
- Added a `TryUploadIndirectDrawArgs(GraphicsBuffer, NativeArray<BoidIndirectArgsDTO>)` bridge for zero-managed-array upload; Loop 14 later replaced the indexed-args target with the procedural indirect target.

Cinematic Cheats used:
- No new physics. This pass preserves the GPU-driven Dear Lie: one draw submission represents up to 100,000 fish while shader/VAT work carries the visible richness.

Exact Microseconds saved:
- Not measured. The structural saving is avoiding CPU instance-list construction and managed draw-args arrays. Static source scans passed; build/profiler remain blocked by the CPU >50% rule.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="10">
  <TASK_14_RECONCILIATION status="PASS">Indirect args now have a dedicated Vault owner: `ShinobuBoidIndirectArgs`.</TASK_14_RECONCILIATION>
  <STRUCT_LAYOUT status="SUPERSEDED_BY_LOOP_14">Loop 10 introduced the Vault owner; Loop 14 corrected `BoidIndirectArgsDTO` to the current 16B `DrawProceduralIndirect` row.</STRUCT_LAYOUT>
  <DEPENDENCY_GRAPH status="PASS">Frame chain now includes `CountTelemetryCountersJob -> WriteBoidIndirectArgsJob` without a main-thread `Complete()`.</DEPENDENCY_GRAPH>
  <COMPILE_GUARD status="JUSTIFIED_CORE_TOUCH">`H8Memory.cs` was touched only to declare one domain-owned BufferID required for Vault data sovereignty.</COMPILE_GUARD>
  <COMPILE_GATE status="BLOCKED">No build launched because total CPU sampled above the allowed threshold.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 11 BoidState Vault Pointer Route Addendum

What was wrong:
- `BoidStateDTO` was a real explicit-layout type, but runtime steering did not yet own a dedicated Vault buffer of that type. The hot path therefore proved pointer mutation on ambient DTOs, not on the specific DTO named by the assignment.

What was done:
- Added `BufferID.ShinobuBoidStates = 70445` and `BufferID.ShinobuBoidStateSnapshot = 70446`.
- Added `_boidStateHandle` and `_boidStateSnapshotHandle` with 100,000-row `UninitializedMemory` Vault allocations.
- Added both buffers to resolve and lock/unlock coverage.
- `LocalShiftAndSpatialHashJob` now writes current and snapshot `BoidStateDTO` rows through `BoidStateDTO*`.
- `BoidFlockingJob` now reads previous-frame `BoidStateDTO.AUP`, writes next `BoidStateDTO` speed/species/AUP, and still uses ambient velocity for direction.
- `BuildShinobuRenderPayloadJob` now subtracts camera AUP from `BoidStateDTO.AUP` before converting to `float3` matrix translation.

Cinematic Cheats used:
- No new simulation. The existing Dear Lie remains: local curl/triangle flow plus SDF-wall cross-product swirl. This pass removes an architectural ambiguity, not a visual trick.

Exact Microseconds saved:
- No saving claimed. Memory cost is explicit: 6.4MB for `BoidStateDTO` + snapshot at 100,000 capacity. The gain is compliance, deterministic snapshot separation, and 32B state cache locality; profiler proof remains blocked by CPU gate.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="11">
  <TASK_03_RECONCILIATION status="PASS">Hot jobs now use `BoidStateDTO*` rows via `NativeArrayUnsafeUtility` and `UnsafeUtility.AsRef`.</TASK_03_RECONCILIATION>
  <TASK_15_RECONCILIATION status="PASS">Position state now has a previous-frame `BoidStateSnapshot` and next-frame `BoidStates` route.</TASK_15_RECONCILIATION>
  <H_PHI_VAULT_STATUS status="PASS">New Vault buffer IDs: `ShinobuBoidStates`, `ShinobuBoidStateSnapshot`.</H_PHI_VAULT_STATUS>
  <STRUCT_LAYOUT status="PASS">`BoidStateDTO` remains 32B: `double3` AUP 24B, two `ushort` lanes 4B, `float` speed 4B.</STRUCT_LAYOUT>
  <COMPILE_GATE status="BLOCKED">Static scans passed; no build launched because total CPU remains above the allowed threshold.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 12 Center AUP Hot-Path Precompute Addendum

What was wrong:
- The new `BoidStateDTO.AUP` render/flocking path would otherwise reconstruct camera absolute AUP inside every parallel iteration.

What was done:
- The scheduler now computes `double3 cameraAbsolute = ToAbsoluteDouble3(_cameraAup)` once.
- `BoidFlockingJob` and `BuildShinobuRenderPayloadJob` receive `CenterAbsolute` and subtract it directly from `BoidStateDTO.AUP`.

Cinematic Cheats used:
- No new fake. This is hot-path math cleanup for the existing AUP-safe Dear Lie swarm route.

Exact Microseconds saved:
- Unmeasured. The removed work is two repeated 100,000x center-AUP reconstructions in the worst-case full-density frame. Build/profiler remain blocked by CPU >50%.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="12">
  <AUP_PRECISION status="PASS">Boid render/flocking still subtract camera AUP before float downcast; center absolute is now precomputed once.</AUP_PRECISION>
  <HOT_PATH_ALU status="PASS">Per-boid center-sector double reconstruction removed from flocking/render jobs.</HOT_PATH_ALU>
  <COMPILE_GATE status="BLOCKED">No build launched because total CPU sampled at 97.5%.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 13 Guarded Build Attempt

What was wrong:
- Compile proof was still pending behind the CPU/dotnet gate.

What was done:
- Ran a guarded build loop. Attempt 1 skipped at CPU 54.5%. Attempt 2 opened at CPU 35.5% with no active `dotnet/csc`, then executed `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal`.

Cinematic Cheats used:
- None. This is verification plumbing.

Exact Microseconds saved:
- None claimed.

Build result:
- `FAILED`, exit code 1.
- Blocking error: `CS2001 Source file 'C:\hades\Hecton8\Assets\_Project\Scripts\World\HectonMapMagicVegetationBridgeFloraCollisionProxies.cs' could not be found.`
- The file is deleted in the current worktree and outside SHINOBU_105 ownership. I did not restore it or edit that domain.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="13">
  <BUILD_GATE status="PASS">Build launched only after CPU sampled below 50% and no `dotnet/csc` process was active.</BUILD_GATE>
  <BUILD_RESULT status="BLOCKED_BY_EXTERNAL_DEPENDENCY">`Hecton8.Core.csproj` cannot compile because an unrelated generated-project source file is missing.</BUILD_RESULT>
  <SHINOBU_COMPILE_PROOF status="PENDING">The compiler did not reach a SHINOBU-specific diagnostic because it failed on the missing world-source file first.</SHINOBU_COMPILE_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 14 GPU ABI And Job-Fence Polish

What was wrong:
- The indirect args row was indexed-style and 32B even though the SHINOBU draw route is `Graphics.DrawProceduralIndirect`, which consumes a four-uint procedural ABI.
- The GPU route had upload/draw helper seams but no domain-owned double-buffered upload dispatcher.
- `LotkaVolterraMacroJob.Run()` was a synchronous job execution in `ColdTick`.
- Single-thread `IJob` counter fields carried unnecessary `NativeDisableParallelForRestriction`.
- Emergency flow mock still read as a triangle-only fake instead of a Perlin-style deterministic current field.

What was done:
- Replaced `BoidIndirectArgsDTO` with a 16B explicit procedural row: `VertexCountPerInstance`, `InstanceCount`, `StartVertex`, `StartInstance`.
- Changed indirect upload helpers to map `BoidIndirectArgsDTO` directly; removed `GraphicsBuffer.IndirectDrawIndexedArgs` from the SHINOBU file.
- Added `ShinobuBoidGpuUploadDispatcher`, owning double-buffered matrix, custom-data, and procedural-args `GraphicsBuffer` pairs and publishing `_H8ShinobuBoidMatrices`, `_H8ShinobuBoidCustomData`, and `_H8ShinobuBoidActiveCount`.
- Replaced macro `.Run()` with scheduled `LotkaVolterraMacroJob` completion through the existing late-frame job fence.
- Moved DataVault discovery to cold activation; `Tick`/`ColdTick` now use cached `_dataVault` only.
- Removed unnecessary `NativeDisableParallelForRestriction` from single jobs.
- Blended deterministic trilinear value-noise into `SampleEmergencyMockFlow()` via `GlobalQualityWeight`.

Cinematic Cheats used:
- Flow remains a deterministic visual fake: coarse triangle current at low quality, Perlin-style value-noise enrichment at high quality. No Navier-Stokes, no texture dependency, no GameObject route.

Exact Microseconds saved:
- No measured microsecond claim. Structural savings: 16B procedural args row instead of 32B indexed-style row; no managed argument arrays; no immediate macro `.Run()` stall. Compile/profiler proof remains blocked by the missing external world source discovered in Loop 13.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="14">
  <TASK_05_RECONCILIATION status="PASS">Emergency flow mock now blends deterministic Perlin-style value noise into the triangle/curl base through `GlobalQualityWeight`.</TASK_05_RECONCILIATION>
  <TASK_14_RECONCILIATION status="PASS">`BoidIndirectArgsDTO` is now a 16B `DrawProceduralIndirect` ABI row and is backed by a double-buffered GPU upload dispatcher.</TASK_14_RECONCILIATION>
  <STRUCT_LAYOUT status="PASS">`BoidIndirectArgsDTO`: offset 0 `VertexCountPerInstance` u32, offset 4 `InstanceCount` u32, offset 8 `StartVertex` u32, offset 12 `StartInstance` u32, total 16B.</STRUCT_LAYOUT>
  <JOB_FENCE status="PASS">`LotkaVolterraMacroJob` no longer uses `.Run()`; it is scheduled and completed through the late-frame fence.</JOB_FENCE>
  <REGISTRY_HOT_PATH status="PASS">DataVault discovery is cold activation/hot-swap only; runtime tick paths operate on cached `_dataVault`.</REGISTRY_HOT_PATH>
  <POINTER_SAFETY status="PASS">Unnecessary `NativeDisableParallelForRestriction` attributes were removed from single jobs.</POINTER_SAFETY>
  <BUILD_RESULT status="BLOCKED_BY_EXTERNAL_DEPENDENCY">No new build launched; Loop 13 already proved current `Hecton8.Core.csproj` fails first on missing `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraCollisionProxies.cs` outside SHINOBU_105 ownership.</BUILD_RESULT>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 15 Static Verify And CPU-Gated Build Probe

What was wrong:
- The status still named the old missing-source build failure as the active blocker after the generated `Hecton8.Core.csproj` no longer exposed that include.
- Loop 14 needed a current source-level scan after the ABI and job-fence changes.

What was done:
- Re-extracted the full `<AGENT_PROMPT id="SHINOBU_105">` block from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-scanned SHINOBU runtime/editor files for old indexed indirect args, `.Run()`, `Instantiate`, `new GameObject`, `Time.frameCount`, `UnityEngine.Random`, `Pack=1`, hot native collection allocation, and property-backed DTO patterns.
- Verified all SHINOBU job structs carried the required Burst directive shape at that point; Loop 27 later changed rollback-owned jobs to `FloatMode.Deterministic`.
- Ran `git diff --check` on touched SHINOBU/log files; only CRLF normalization warnings were reported.
- Probed build gate: CPU=100, compiler_count=0, so `dotnet build` was skipped under the explicit CPU rule.

Cinematic Cheats used:
- No new runtime cheat added in this loop; previous deterministic value-noise current fake and SDF cross-product wall swirl remain the current Dear Lie route.

Exact Microseconds saved:
- Build load avoided under saturated CPU: compile-time only, not a runtime microsecond claim.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="15">
  <STATIC_SCAN status="PASS">Touched SHINOBU runtime/editor files contain no old indexed indirect ABI, `.Run()`, `Instantiate`, `new GameObject`, `Time.frameCount`, `UnityEngine.Random`, `Pack=1`, hot `new NativeArray`, or DTO property pattern.</STATIC_SCAN>
  <BURST_DIRECTIVES status="SUPERSEDED_BY_LOOP_27">Nine SHINOBU jobs were found with Burst directives in Loop 15; Loop 27 later changed all nine to `FloatMode.Deterministic` for rollback-owned state.</BURST_DIRECTIVES>
  <BUILD_STATUS status="CPU_GATED">Build probe skipped `dotnet build`: CPU=100, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 16 Vault Boot Gate And Failure Path

What was wrong:
- `EnsureVaultState()` still called `GlobalDataVault.GetBufferHandle` from runtime `Tick`/`ColdTick` after handles were already created.
- Existing Vault handle reacquisition sanitizes finite payloads for existing buffers, so the readiness path could silently become O(N) over 100,000-row buffers.
- Schedule-path `catch (Exception)` blocks unlocked buffers and then rethrew, turning a domain scheduling failure into a gameplay crash without SHINOBU telemetry.

What was done:
- Added `_vaultBuffersReady`.
- Added `AreVaultHandlesCreated()` with creation and minimum-length checks for all SHINOBU Vault handles.
- Changed `EnsureVaultState()` to short-circuit through the ready flag before any `GetBufferHandle` call.
- Cleared `_vaultBuffersReady` on handle reset and cached-state clear.
- Replaced frame and macro schedule rethrows with numeric `GlobalTelemetryBus.PublishPerformanceWarning` calls after unlock.

Cinematic Cheats used:
- None added in this loop. This was ownership and failure-path hardening. Existing Dear Lie routes remain SDF cross-product wall swirl and deterministic triangle/value-noise current.

Exact Microseconds saved:
- No measured number claimed. Structural saving: normal `Tick`/`ColdTick` no longer risk repeated large-buffer sanitize scans during Vault readiness checks.
- Burst directive scan: 9 SHINOBU job types, 9 required Burst directives.
- Build probe: skipped correctly at CPU=64.3%, compiler_count=0.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="16">
  <VAULT_BOOT_STATUS status="PASS">`GetBufferHandle` is now boot/hot-swap/recovery only after `_vaultBuffersReady` is set; normal ticks validate handles and lengths without reacquiring Vault buffers.</VAULT_BOOT_STATUS>
  <FAILURE_PATH status="PASS">Schedule failures unlock job buffers and publish numeric telemetry warnings instead of rethrowing into gameplay.</FAILURE_PATH>
  <ZERO_GC_HOT_PATH status="PASS">Patch adds no managed runtime collections, no LINQ, no formatted strings, and no local persistent NativeArrays.</ZERO_GC_HOT_PATH>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=64.3, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 17 Procedural Args Target Hardening

What was wrong:
- The GPU args buffer used `GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw` with a 16B `BoidIndirectArgsDTO` stride.
- Raw buffer semantics can imply 4B lanes on Unity backends, creating avoidable validation risk for the procedural indirect path.

What was done:
- Changed `CreateIndirectArgsBuffer()` to use `GraphicsBuffer.Target.IndirectArguments` only.
- Kept the explicit 16B `BoidIndirectArgsDTO` ABI and direct mapped upload route.
- Re-scanned touched SHINOBU files for `Target.Raw`, old indexed args API, and critical forbidden patterns.
- Re-ran guarded build probe; it skipped correctly at CPU=98.9%, compiler_count=0.

Cinematic Cheats used:
- None added. This loop only corrected GPU buffer ABI.

Exact Microseconds saved:
- None claimed. This avoids a platform-specific indirect-buffer validation failure; performance remains tied to the mapped upload and single draw submission already implemented.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="17">
  <GPU_ABI status="PASS">Indirect args buffer target is now `GraphicsBuffer.Target.IndirectArguments` only, with one 16B `BoidIndirectArgsDTO` row.</GPU_ABI>
  <STATIC_SCAN status="PASS">No `Target.Raw`, `GraphicsBuffer.IndirectDrawIndexedArgs`, `throw;`, `.Run()`, `Time.frameCount`, `UnityEngine.Random`, `Pack=1`, or hot `new NativeArray` remains in touched SHINOBU files.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=98.9, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 18 Schedule Failure Ownership

What was wrong:
- Schedule catch blocks no longer rethrew, but they could still unlock Vault buffers if an exception occurred after a job was scheduled and before the runtime published `_jobScheduled/_jobLocksHeld`.
- That edge can allow a running Burst job to mutate buffers that another system believes are unlocked.

What was done:
- Added `scheduledHandle`/`scheduledWork` tracking to the frame schedule path.
- Added the same ownership tracking to the macro biomass schedule path.
- After every successful `Schedule()` call, the latest `JobHandle` is retained.
- On exception after scheduled work exists, SHINOBU preserves `_activeJobHandle`, `_jobScheduled`, and `_jobLocksHeld` for late-frame recovery; only pre-schedule failures unlock immediately.

Cinematic Cheats used:
- None added. This loop is concurrency hygiene.

Exact Microseconds saved:
- None claimed. The value is preventing a Vault unlock/use-after-schedule race, not reducing normal-frame ALU.
- Verification: critical static scan found no forbidden SHINOBU patterns; Burst scan found 9 job types and 9 required directives; `git diff --check` reported CRLF normalization warnings only; build probe skipped at CPU=100%, compiler_count=0.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="18">
  <SCHEDULE_OWNERSHIP status="PASS">Post-schedule failures now retain the job handle and Vault locks until dispatcher late-frame recovery.</SCHEDULE_OWNERSHIP>
  <NO_FORCED_COMPLETE status="PASS">The catch path does not call `Complete()`; it preserves ownership for the existing `DispatcherJobSwap` recovery lane.</NO_FORCED_COMPLETE>
  <ZERO_GC_HOT_PATH status="PASS">Patch adds only stack locals and control flow; no managed collections or runtime allocations.</ZERO_GC_HOT_PATH>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=100, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 19 GPU Upload Route Wiring

What was wrong:
- `ShinobuBoidGpuUploadDispatcher` existed but was not owned or called by the runtime.
- Frame jobs produced Vault matrices/custom data/indirect args, but the active system did not publish them to GPU buffers after job completion.

What was done:
- Added one cold-owned `ShinobuBoidGpuUploadDispatcher` to `ShinobuEcosystemBalancer`.
- Prewarmed GPU matrix, custom-data, and indirect-args buffers in `EnsureVaultState()` when not running in batch/headless mode.
- Added `UploadCompletedFrameToGpu()` after dispatcher job recovery and before telemetry write.
- Published `_H8ShinobuBoidMatrices`, `_H8ShinobuBoidCustomData`, and `_H8ShinobuBoidActiveCount` through the dispatcher upload path.
- Recorded matrix upload milliseconds into the existing telemetry ring.

Cinematic Cheats used:
- No new fake added. This wires the presentation route for the existing data-only fish school.

Exact Microseconds saved:
- No measured saving claimed. The route removes the remaining helper-only gap and avoids managed matrix arrays; upload timing is now recorded for future profiler proof.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="19">
  <GPU_UPLOAD status="PASS">Completed frame payloads are uploaded from Vault to double-buffered GPU buffers after `DispatcherJobSwap` recovery.</GPU_UPLOAD>
  <HEADLESS_GUARD status="PASS">GPU buffer prewarm/upload is skipped when `Application.isBatchMode` is true.</HEADLESS_GUARD>
  <ZERO_GC_HOT_PATH status="PASS">No managed matrix arrays or GameObject route were introduced; GPU buffers are cold-owned and prewarmed.</ZERO_GC_HOT_PATH>
  <STATIC_SCAN status="PASS">No critical forbidden SHINOBU pattern was found; 9 job types still have 9 required Burst directives.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=96.3, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 20 GPU Buffer Lock Safety

What was wrong:
- SHINOBU upload helpers and dispatcher upload code called `GraphicsBuffer.LockBufferForWrite` without a `finally` unlock guard.
- A failure after a successful lock could leave a matrix/custom/indirect args buffer mapped.

What was done:
- Wrapped render-matrix upload lock/unlock in `try/finally`.
- Wrapped indirect args upload lock/unlock in `try/finally`.
- Wrapped dispatcher matrix, custom-data, and args uploads in independent `try/finally` blocks.

Cinematic Cheats used:
- None added. This loop is GPU resource ownership hardening.

Exact Microseconds saved:
- None claimed. Normal route is the same mapped write path; failure-path resource safety is the objective.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="20">
  <GPU_LOCK_SAFETY status="PASS">Every successful SHINOBU `LockBufferForWrite` now has a `finally`-guarded `UnlockBufferAfterWrite`.</GPU_LOCK_SAFETY>
  <STATIC_SCAN status="PASS">No critical forbidden SHINOBU pattern was found; 9 job types still have 9 required Burst directives.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=56.6, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 21 Procedural Shader Consumer

What was wrong:
- SHINOBU runtime published matrix/custom GPU buffers, but no SHINOBU-owned shader consumed those exact global buffers.
- The old boid shader path consumes `_BoidsBuffer`, which is not the SHINOBU_105 matrix DTO route.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_AbyssalSwarmProcedural.shader`.
- Added its `.meta` file with a stable GUID.
- The shader reads `_H8ShinobuBoidMatrices`, `_H8ShinobuBoidCustomData`, and `_H8ShinobuBoidActiveCount`.
- It uses `SV_VertexID`/`SV_InstanceID` and draws a three-vertex fish silhouette per instance.
- It declares no `multi_compile` or `shader_feature` variants.

Cinematic Cheats used:
- Fish are procedural triangle silhouettes driven by matrix/custom buffers. This is the Dear Lie fallback for distant swarm density: three vertices per visible fish instead of skinned meshes, Animators, or per-fish GameObjects.

Exact Microseconds saved:
- No measured number claimed. Structural reduction: the fallback visual path is 3 vertices per fish and one indirect procedural route.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="21">
  <SHADER_CONSUMER status="PASS">A SHINOBU-owned shader now consumes the exact global buffers published by `ShinobuBoidGpuUploadDispatcher`.</SHADER_CONSUMER>
  <VARIANT_STATUS status="PASS">The shader introduces no `multi_compile` or `shader_feature` variants.</VARIANT_STATUS>
  <DEAR_LIE status="PASS">Fallback fish are procedural three-vertex silhouettes, not skinned meshes or GameObjects.</DEAR_LIE>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=100, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 22 Procedural Material Asset

What was wrong:
- The procedural shader had no stable material asset.
- Leaving the material undefined would push integration toward runtime `new Material(shader)`, `Resources.Load`, or an undocumented render-owner guess.

What was done:
- Added `Assets/_Project/Art/Materials/MAT_AbyssalSwarmProcedural.mat`.
- Added its `.meta` file with a stable material GUID.
- Bound the material to `Hecton_AbyssalSwarmProcedural.shader` via shader GUID `7b6d4f2c9a2f4b94a2a9f7b9e8a10511`.
- Kept shader keywords empty and instancing variants enabled.
- Verified shader/material symbol alignment and scanned touched SHINOBU files for critical forbidden patterns.

Cinematic Cheats used:
- Same Dear Lie visual route as Loop 21: one procedural triangle silhouette per fish, consuming matrix/custom buffers instead of meshes, Animators, or GameObjects.

Exact Microseconds saved:
- No measured runtime number claimed. This prevents a future cold/hot material allocation and first-use shader ambiguity; Unity import/warmup proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="22">
  <MATERIAL_ASSET status="PASS">`MAT_AbyssalSwarmProcedural.mat` exists as a cold asset bound to the SHINOBU procedural shader GUID.</MATERIAL_ASSET>
  <VARIANT_STATUS status="PASS">Material keyword lists are empty; no new shader keyword surface was introduced.</VARIANT_STATUS>
  <ZERO_GC_ROUTE status="PASS">The render integration now has a stable asset handle instead of requiring runtime material creation.</ZERO_GC_ROUTE>
  <STATIC_SCAN status="PASS">Forbidden-pattern scan returned no hits; `git diff --check` reported CRLF normalization warnings only.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">Build verification skipped: CPU=100, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 23 Render Dispatch Seam

What was wrong:
- The GPU upload dispatcher could upload and publish buffers, but `ShinobuEcosystemBalancer` did not expose a first-class render-dispatch route.
- Without a seam, integration would drift toward duplicate GPU owners, runtime material creation, or direct private-field coupling.

What was done:
- `ShinobuEcosystemBalancer` now implements `IRenderable`.
- Added `BindProceduralRenderMaterial(Material, Bounds, int)` for cold caller-owned material binding.
- Added `Render(float)` to submit one `Graphics.DrawProceduralIndirect` through the existing double-buffered dispatcher when a material is bound.
- Added `TryDrawUploadedSwarm()` and `TryGetUploadedSwarmBuffers()` for explicit non-alloc render-owner integration.
- Added render-dispatch hot-swap handling so binding can recover when `GlobalRegistryServiceSlot.RenderDispatcher` appears.

Cinematic Cheats used:
- The same procedural silhouette Dear Lie now has a real render-dispatch submission seam: one indirect procedural triangle path for dense distant fish, not per-fish meshes or GameObjects.

Exact Microseconds saved:
- No measured number claimed. Architectural saving is prevention of duplicate buffer ownership and runtime material allocation; frame proof remains pending until Unity import and profiler capture are legal.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="23">
  <DRAW_ROUTE status="PASS">`ShinobuEcosystemBalancer` can now submit uploaded swarm buffers through `GlobalRegistry.Renderables` with a cold-bound material asset.</DRAW_ROUTE>
  <ZERO_GC_ROUTE status="PASS">No `new Material`, `Shader.Find`, `Resources.Load`, GameObject, managed matrix array, or material clone was introduced.</ZERO_GC_ROUTE>
  <COMPILE_WALL status="PASS">No asmdef or Contracts change was made; the seam stays in the SHINOBU domain file.</COMPILE_WALL>
  <STATIC_SCAN status="PASS">Forbidden-pattern scan returned no hits; job/Burst parity remained 9/9; `git diff --check` reported CRLF normalization warnings only.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_AND_COMPILER_GATED">Build verification skipped: CPU=100, compiler_count=1; follow-up process check showed active `dotnet` and `csc`.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 24 Compile Gate Attempt

What was wrong:
- Compile verification was still pending after render-dispatch seam work.
- The CPU/compiler gate opened, so a guarded build was permitted.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal` only after CPU=40.2 and compiler_count=0.
- Build failed outside SHINOBU on missing Visor/Equipment/Editor DTOs and vault IDs.
- No compiler error referenced the SHINOBU runtime file, H8Memory buffer IDs, shader, or material asset.

Cinematic Cheats used:
- None added. This loop is verification and dependency forensics only.

Exact Microseconds saved:
- None claimed. This build attempt establishes the current compile wall is external to SHINOBU_105.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="24">
  <BUILD_GATE status="RAN">Build was launched only when CPU=40.2 and compiler_count=0.</BUILD_GATE>
  <BUILD_RESULT status="BLOCKED_BY_DEPENDENCY">Build failed on non-SHINOBU missing DTO/type dependencies in Visor, Equipment, DeferredDecal, GlobalRegistryContracts, and Somatic editor code.</BUILD_RESULT>
  <SHINOBU_ERROR_SCAN status="PASS">No emitted compiler error referenced `ShinobuEcosystemBalancer.cs`, `H8Memory.cs`, `Hecton_AbyssalSwarmProcedural.shader`, or `MAT_AbyssalSwarmProcedural.mat`.</SHINOBU_ERROR_SCAN>
  <NEXT_PROOF status="PENDING_VERIFICATION">Unity import, Play Mode, shader import, Frame Debugger, GCMonitor, and profiler proof remain blocked until the project compile wall is cleared.</NEXT_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 25 Cinematic Cheat Ledger

What was wrong:
- The SHINOBU shader/material route was not recorded in the stable cinematic cheat ledger.
- Static scan found no populated Addressables data or authored VFX prewarm manifest that would prove material retention or shader warmup.

What was done:
- Scanned content retention surfaces for `ShaderVariantCollection`, `ContentVfxPrewarmManifest`, Addressables data, and SHINOBU material/shader references.
- Updated `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` with the abyssal swarm procedural silhouette entry.
- The ledger explicitly states material binding, shader warmup/retention, Unity import, Frame Debugger, GCMonitor, and profiler proof remain pending.

Cinematic Cheats used:
- Procedural silhouette fish: three vertices per fish from uploaded matrix/custom buffers instead of per-fish objects or skinned meshes.

Exact Microseconds saved:
- Documentation-only, 0us. The value is preventing false readiness and shader-stutter claims.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="25">
  <LEDGER_STATUS status="PASS">`CINEMATIC_CHEATS_LEDGER.md` now records the SHINOBU_105 procedural swarm Dear Lie.</LEDGER_STATUS>
  <ASSET_RETENTION status="PENDING_VERIFICATION">No populated Addressables data or authored `ContentVfxPrewarmManifest` asset was found for this material.</ASSET_RETENTION>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, material binding, shader warmup, Frame Debugger, GCMonitor, profiler, and player-build proof remain absent.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 26 Static Validation Forensics

What was wrong:
- Broad forbidden scans included existing core `H8Memory.cs` allocation/telemetry internals and unrelated ledger text, producing non-SHINOBU false positives.
- The project compile wall is still external, so static validation must be exact about what it proves and what it does not.

What was done:
- Re-ran forbidden-pattern scans against SHINOBU runtime/shader/material paths only.
- Re-ran zero-context diff scan to verify the matched forbidden lines are removed, not added.
- Re-ran Burst directive parity at that point; Loop 27 later superseded the mode to 9 deterministic Burst jobs for rollback-owned state.
- Re-ran `git diff --check` across touched paths; it reports CRLF normalization warnings only.

Cinematic Cheats used:
- No new cheat added. The verified active cheat remains procedural silhouette fish: one indirect draw route, three vertices per fish, matrix/custom data from Vault/GPU buffers.

Exact Microseconds saved:
- No measured number claimed. Validation loop is 0us runtime; it prevents a false-positive scan from corrupting the proof record.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="26">
  <FORBIDDEN_SCAN status="PASS">SHINOBU runtime/shader/material scan returned no `throw;`, `.Run(`, `Time.frameCount`, `UnityEngine.Random`, `Pack=1`, hot `new NativeArray`, indexed indirect args, `Target.Raw`, `Instantiate`, `new GameObject`, `new Material`, `Resources.Load`, `Shader.Find`, or `renderer.material` hit.</FORBIDDEN_SCAN>
  <DIFF_SCAN status="PASS">Zero-context diff scan matched only removed lines for old `throw;`, `job.Run()`, and `GraphicsBuffer.IndirectDrawIndexedArgs` code.</DIFF_SCAN>
  <BURST_PARITY status="PASS">9 SHINOBU job structs, 9 required Burst directive attributes.</BURST_PARITY>
  <BUILD_STATUS status="BLOCKED_BY_DEPENDENCY">Last legal build failed outside SHINOBU on missing Visor/Equipment/Comfort DTOs; no SHINOBU compiler errors were emitted.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, material retention/warmup, Frame Debugger, GCMonitor, profiler, and player-build validation remain pending after the external compile wall is cleared.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 27 Deterministic Burst Correction

What was wrong:
- SHINOBU_105 owns rollback-compatible boid state, but its 9 Burst jobs still used `FloatMode.Fast`.
- The global Burst rule has an explicit rollback exception; keeping Fast would make Task 15's determinism proof incomplete.
- The earlier prompt extraction regex was too strict for tags with extra attributes.

What was done:
- Re-extracted the full `<AGENT_PROMPT id="SHINOBU_105" ...>` block with a tag-aware regex.
- Scanned the repo and confirmed `FloatMode.Deterministic` is an existing project pattern in rollback/physics/IK jobs.
- Converted all 9 SHINOBU job attributes to `FloatMode.Deterministic` while preserving `CompileSynchronously = true` and `FloatPrecision.Standard`.
- Re-ran static scans: jobs=9, deterministic=9, fast=0; forbidden-pattern scan returned no SHINOBU runtime/shader/material hits.

Cinematic Cheats used:
- No new cheat added. The active cheat remains procedural silhouette fish: one indirect draw, three vertices per fish, matrix/custom data from Vault/GPU buffers.

Exact Microseconds saved:
- None claimed. This loop trades possible Fast-mode ALU freedom for deterministic rollback safety. Runtime profiler proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="27">
  <PROMPT_RECALL status="PASS">Full SHINOBU_105 XML prompt re-extracted with `<AGENT_PROMPT id="SHINOBU_105"[\s\S]*?</AGENT_PROMPT>`.</PROMPT_RECALL>
  <BURST_MODE status="PASS">9 SHINOBU job structs, 9 `FloatMode.Deterministic` attributes, 0 remaining `FloatMode.Fast` attributes in `ShinobuEcosystemBalancer.cs`.</BURST_MODE>
  <ROLLBACK_STATUS status="HARDENED">Rollback-owned state no longer depends on Fast float mode.</ROLLBACK_STATUS>
  <COMPILE_GUARD status="UNCHANGED">No asmdef or sibling-domain file was edited; `Hecton8.World` AUP usage remains an existing Core/root assembly seam, not a new SHINOBU assembly reference.</COMPILE_GUARD>
  <BUILD_STATUS status="BLOCKED_BY_DEPENDENCY">Build was not relaunched because the last legal build is already blocked outside SHINOBU by Visor/Equipment/Comfort DTO gaps.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, material retention/warmup, Frame Debugger, GCMonitor, profiler, and player-build validation remain pending after external compile blockers are cleared.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 28 GPU Visibility Culling Route

What was wrong:
- The procedural swarm draw path uploaded and drew the active budget without a SHINOBU-owned GPU visibility compaction pass.
- That left frustum/HZB rejection to downstream GPU vertex work instead of a bounded compute cull stage.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_AbyssalSwarmCull.compute`.
- Added cold culling binding to `ShinobuEcosystemBalancer` and double-buffered visible-index plus GPU-written culled-args buffers to `ShinobuBoidGpuUploadDispatcher`.
- Updated `Hecton_AbyssalSwarmProcedural.shader` so `SV_InstanceID` can resolve through `_H8ShinobuBoidVisibleIndices`.
- Updated `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` with the compute-cull route and retained PENDING VERIFICATION labels for Unity import/warmup/profiler proof.

Cinematic Cheats used:
- GPU silhouette compaction: compute writes a compact source-index list and 16B procedural args row so the fish shader only processes visible/quality-retained instances. This avoids CPU visible lists and per-fish renderers.

Exact Microseconds saved:
- No measured number claimed. Expected gain is reduced vertex shader work after GPU frustum/density/HZB rejection; profiler proof is pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="28">
  <GPU_CULL_ROUTE status="PASS">Optional compute culler clears `DrawProceduralIndirect` args, compacts visible indices, and writes instance count via `RWByteAddressBuffer.InterlockedAdd`.</GPU_CULL_ROUTE>
  <QUALITY_CURVE status="PASS">Density step uses `ceil(lerp(5, 1, smoothQuality))`; HZB sampling is disabled below `GlobalQualityWeight` 0.3 through `math.step`.</QUALITY_CURVE>
  <ZERO_GC_ROUTE status="PASS">No runtime `Resources.Load`, `Shader.Find`, `new Material`, GameObject, managed matrix array, or CPU visible-list allocation was introduced.</ZERO_GC_ROUTE>
  <STATIC_SCAN status="PASS">Forbidden-pattern scan returned no SHINOBU hits; jobs=9, deterministic=9, fast=0; no hot DTO properties were found.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_AND_COMPILER_GATED">No build launched: CPU=85.4, compiler_count=7.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, compute/material binding, shader/compute warmup, Frame Debugger, GCMonitor, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 29 GPU Cull Resource Binding Hardening

What was wrong:
- The non-culled draw path did not bind `_H8ShinobuBoidVisibleIndices`, even though the procedural shader declares it.
- The frustum-only compute path could dispatch without binding `_H8ShinobuDepthPyramid` when no HZB texture is supplied.

What was done:
- `UploadFromVault()` now resolves the active visible-index buffer and passes it to `PublishBuffers(...)` even when culling is disabled.
- `TryDraw()` now publishes the visible-index buffer on both culled and fallback paths; `_H8ShinobuBoidUseVisibleIndices` remains the shader switch.
- `TryDispatchVisibilityCulling()` now always binds `_H8ShinobuDepthPyramid`, using the caller texture or `Texture2D.blackTexture` as a no-allocation fallback.
- Re-read the full SHINOBU_105 XML prompt from `CURRENT_BATCH.md`; the prompt contains Tasks 01 through 20.

Cinematic Cheats used:
- Same GPU silhouette compaction; this loop hardens resource binding so the fake can survive Unity backend validation.

Exact Microseconds saved:
- No measured saving claimed. This prevents unbound SRV/texture dispatch failures; profiler proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="29">
  <PROMPT_RECALL status="PASS">SHINOBU_105 XML was re-extracted from `CURRENT_BATCH.md`; Task 01 through Task 20 were found.</PROMPT_RECALL>
  <RESOURCE_BINDING status="PASS">Visible-index buffer is always globally bound; compute depth texture is always bound with a caller texture or `Texture2D.blackTexture` fallback.</RESOURCE_BINDING>
  <STATIC_SCAN status="PASS">Forbidden-pattern scan returned no SHINOBU hits; jobs=9, deterministic=9, fast=0; `git diff --check` reported CRLF normalization warnings only.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_AND_COMPILER_GATED">No build launched: CPU=70.9, compiler_count=7.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, compute/material binding, shader/compute warmup, Frame Debugger, GCMonitor, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 30 GPU Cull ABI Audit

What was wrong:
- The new compute cull path crosses C# DTO layout, HLSL `StructuredBuffer` layout, and a raw byte-addressed indirect args row. A mismatch would not be caught by normal source scans.
- Compile-wall proof also needed to distinguish namespace use of existing AUP authority from a new sibling assembly reference.

What was done:
- Audited `BoidMatrixDTO`: C# explicit size `64`, offsets `C0=0`, `C1=16`, `C2=32`, `C3=48`; both HLSL shaders declare the same four `float4` lanes.
- Audited `BoidIndirectArgsDTO`: C# explicit size `16`, offsets `VertexCountPerInstance=0`, `InstanceCount=4`, `StartVertex=8`, `StartInstance=12`; `Hecton_AbyssalSwarmCull.compute` writes `Store(0/4/8/12)` and increments offset `4`.
- Verified the GPU-written culled args buffer is `GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw` with `count=4`, `stride=4`, giving the required 16 byte-addressable bytes.
- Re-scanned SHINOBU forbidden patterns and asmdef surfaces. No new SHINOBU asmdef or direct sibling runtime assembly reference was introduced.

Cinematic Cheats used:
- Same procedural fish silhouette and GPU visible-index compaction. This loop proves the fake's buffer ABI instead of adding a heavier simulation.

Exact Microseconds saved:
- 0us measured. This is a fault-prevention audit: it avoids silent GPU layout corruption, not a profiled optimization claim.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="30">
  <STRUCT_LAYOUT status="PASS">`BoidMatrixDTO` = 64B, offsets `0/16/32/48`; HLSL compute/procedural structs match four `float4` lanes.</STRUCT_LAYOUT>
  <INDIRECT_ARGS_ABI status="PASS">`BoidIndirectArgsDTO` = 16B, offsets `0/4/8/12`; compute clear stores the same offsets and cull increments instance count at byte offset `4`.</INDIRECT_ARGS_ABI>
  <GPU_BUFFER_TARGET status="PASS">Culled args buffer uses `IndirectArguments | Raw`, `count=4`, `stride=4`, matching a 16B `RWByteAddressBuffer` row.</GPU_BUFFER_TARGET>
  <COMPILE_GUARD status="PASS">No new SHINOBU asmdef or direct sibling runtime assembly reference was added; existing `Hecton8.World` namespace use is the current core/root AUP seam.</COMPILE_GUARD>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, shader/compute compile, material/compute binding, Frame Debugger, GCMonitor, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 31 Content Prewarm Route Audit

What was wrong:
- The SHINOBU procedural shader/material/compute files exist, but content retention and shader/compute warmup are not proven by file existence.
- The project has ContentAuthority source support for compute prewarm, but the Addressables payload layer is empty in the current tree.

What was done:
- Audited `ContentAuthorityRuntime`: `ContentVfxPrewarmManifest` has a fixed 64-entry handle cap and `StartVfxPrewarm()` loads compute shader entries through `LoadAssetAsync<ComputeShader>()`.
- Audited `ContentAuthorityBuildValidators`: compute shaders under `Assets/_Project` are checked with `GetKernelThreadGroupSizes`; `ContentVfxPrewarmManifest` compute references are validated as Addressable `ComputeShader` assets.
- Recounted payload reality: `Assets/AddressableAssetsData` has 0 files, and no authored `ContentVfxPrewarmManifest` payload was found under `Assets/_Project`.
- Updated `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` with the exact source route and the remaining warmup/retention gap.
- Re-ran SHINOBU static scans: forbidden runtime/shader pattern scan returned no hits; jobs=9, deterministic=9, fast=0; log order is Loop 29 -> Loop 30 -> Loop 31; `git diff --check` reports CRLF normalization warnings only.

Cinematic Cheats used:
- No new visual fake. This loop protects the existing procedural silhouette and compute-cull fake from false warmup/retention claims.

Exact Microseconds saved:
- 0us runtime. This is a proof correction: it avoids introducing runtime `Resources.Load`, `Shader.Find`, raw YAML authoring risk, or a fake Addressables readiness claim. No build was launched because CPU=100, compiler_count=0, Loop 31 changed docs only, and the last legal build remains externally blocked.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="31">
  <CONTENT_AUTHORITY_ROUTE status="SOURCE_ONLY">`ContentAuthorityRuntime` can load compute shaders from `ContentVfxPrewarmManifest`; `ContentAuthorityBuildValidators` validates compute thread groups and manifest references.</CONTENT_AUTHORITY_ROUTE>
  <PAYLOAD_STATE status="MISSING">`Assets/AddressableAssetsData` file count is 0; no authored `ContentVfxPrewarmManifest` `.asset`, `.prefab`, or `.unity` payload was found under `Assets/_Project`.</PAYLOAD_STATE>
  <REJECTED_SHORTCUTS status="PASS">No raw Unity YAML manifest, `Resources.Load`, `Shader.Find`, runtime material creation, or SHINOBU-local Addressables bootstrap was added.</REJECTED_SHORTCUTS>
  <LEDGER_STATUS status="UPDATED">`CINEMATIC_CHEATS_LEDGER.md` now records both the SHINOBU source route and the absent payload proof.</LEDGER_STATUS>
  <STATIC_SCAN status="PASS">Forbidden SHINOBU runtime/shader scan returned no hits; jobs=9, deterministic=9, fast=0; Loop 29/30/31 log order is linear.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0; Loop 31 was documentation-only and the last legal build remains externally blocked.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, Addressables manifest creation, shader/compute warmup, material/compute binding, Frame Debugger, GCMonitor, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 32 HZB Texel Sanitizer

What was wrong:
- `BindProceduralCullingResources()` accepted caller-supplied depth pyramid texel-size and mip-count data without validating finite positive dimensions.
- Bad depth metadata could make the compute culler sample a fake 1x1 pyramid or request an impossible mip level, silently corrupting visibility.

What was done:
- Added `SanitizeDepthPyramidMipCount()` and `SanitizeDepthPyramidTexelSize()` to the SHINOBU render/cull seam.
- The culling bind path now derives width, height, inverse width, inverse height, and mip count from the bound `Texture` when caller metadata is zero, stale, or non-finite.
- Removed the first `Texture.mipmapCount` attempt because a repo scan found no local use; the final clamp derives max mip count from `Texture.width`/`height`.
- Re-extracted the SHINOBU_105 XML prompt and confirmed 20 task entries before recording this loop.

Cinematic Cheats used:
- Same GPU silhouette compaction and HZB fake. This loop makes the fake tolerate bad caller metadata without adding CPU visible lists or physics queries.

Exact Microseconds saved:
- No measured number claimed. Cold bind-path scalar sanitization prevents silent bad-cull GPU work; build/profiler proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="32">
  <HZB_METADATA_SANITIZER status="PASS">Depth pyramid mip count and texel-size vectors are sanitized before reaching the compute culler.</HZB_METADATA_SANITIZER>
  <QUALITY_ROUTE status="UNCHANGED">Low-quality HZB disablement still uses `math.step(0.3f, quality)`; density decimation remains `ceil(lerp(5, 1, smoothQuality))`.</QUALITY_ROUTE>
  <STATIC_SCAN status="PASS">Forbidden SHINOBU runtime/shader scan returned no hits, including no `mipmapCount`; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=98, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, shader/compute compile, HZB texture source binding, Frame Debugger, GCMonitor, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 33 Hot Struct And Allocation Audit

What was wrong:
- The cull/render seam had grown enough that the proof needed a new CS1612 and allocation audit, not just a narrow forbidden-token scan.
- Cold fail-fast and Unity graphics-resource ownership needed to be separated from gameplay hot-path allocation.

What was done:
- Re-scanned `ShinobuEcosystemBalancer.cs` for struct declarations and property setters. Result: DTO/job structs are field-only; zero `{ get; set; }` or `{ get; private set; }` properties.
- Re-scanned for private persistent native containers. Result: zero private `NativeArray`, `NativeList`, `NativeHashMap`, `NativeParallel`, `NativeQueue`, or `NativeStream` fields.
- Re-scanned allocation hits. Result: cold singleton/dispatcher construction, cold file I/O for CSV/blackbox, cold `GraphicsBuffer` bridge allocation, value-type constructors, and one cold boot `CriticalBootException` layout fail-fast path.

Cinematic Cheats used:
- No new fake. This audit confirms the procedural silhouette and GPU compaction route did not reintroduce managed per-fish allocation.

Exact Microseconds saved:
- 0us measured. Audit-only. It prevents a false claim that all `new` tokens are gameplay GC while still proving no managed array/list/string hot path was added.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="33">
  <CS1612_STATUS status="PASS">Zero property setters found in SHINOBU hot DTO/job scan.</CS1612_STATUS>
  <H_PHI_PRIVATE_NATIVE status="PASS">Zero private persistent `NativeArray`/`NativeList`/`NativeHashMap`/`NativeParallel`/`NativeQueue` fields found.</H_PHI_PRIVATE_NATIVE>
  <ALLOCATION_SCAN status="PASS">Only cold singleton/file/GraphicsBuffer ownership and value-type constructors were found; no per-frame managed array/list/string route was introduced.</ALLOCATION_SCAN>
  <COLD_FAIL_FAST status="DOCUMENTED">`CriticalBootException` remains a cold layout mismatch fail-fast path, not a gameplay exception route.</COLD_FAIL_FAST>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=53, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, profiler, GCMonitor, Frame Debugger, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 34 Shader Variant And Threadgroup Audit

What was wrong:
- The procedural swarm shader/compute files could still cause shader-variant stutter or mobile compute import failure if keywords or large thread groups slipped in.

What was done:
- Verified `MAT_AbyssalSwarmProcedural.mat` has empty `m_ValidKeywords` and `m_InvalidKeywords`.
- Verified `Hecton_AbyssalSwarmProcedural.shader` has only vertex/fragment pragmas plus target 4.5 and no `shader_feature` or `multi_compile`.
- Verified `Hecton_AbyssalSwarmCull.compute` has two kernels: clear `[numthreads(1,1,1)]`, cull `[numthreads(64,1,1)]`.
- Verified `ContentAuthorityBuildValidators.ValidateComputeShaderThreadGroups()` scans compute shaders under `Assets/_Project` and fails totals above 1024.

Cinematic Cheats used:
- No new fake. The existing silhouette/compute-cull fake remains one material/shader route with scalar quality data, not variant multiplication.

Exact Microseconds saved:
- 0us measured. This is stutter prevention and import-risk evidence; Unity shader compile/profiler proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="34">
  <MATERIAL_KEYWORDS status="PASS">`MAT_AbyssalSwarmProcedural.mat` has empty valid and invalid keyword lists.</MATERIAL_KEYWORDS>
  <SHADER_VARIANTS status="PASS">No `shader_feature` or `multi_compile` directives found in `Hecton_AbyssalSwarmProcedural.shader`.</SHADER_VARIANTS>
  <COMPUTE_THREADGROUPS status="PASS">SHINOBU compute kernels are 1 and 64 threads, below the 1024-thread validator cap.</COMPUTE_THREADGROUPS>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity shader import, compute import, warmup, Frame Debugger, GCMonitor, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 35 Agent-Keyed Blackbox Dump Path

What was wrong:
- The 300-frame telemetry blackbox existed, but dump filenames used the domain alias `Dump_ABYSSAL_SWARM.*` instead of the required agent-keyed `Dump_SHINOBU_105.*` route.

What was done:
- Changed `DumpRelativePath` to `Docs/AgentLogs/Dump_SHINOBU_105.bin`.
- Changed `DumpH8RelativePath` to `Docs/AgentLogs/Dump_SHINOBU_105.h8dump`.
- Left the 64-byte telemetry entry layout, 300-frame capacity, fault trigger conditions, and binary writer unchanged.

Cinematic Cheats used:
- No visual fake changed. This is forensic routing only.

Exact Microseconds saved:
- 0us normal runtime. Fault-path filename correction only.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="35">
  <BLACKBOX_ROUTE status="PASS">Fault dumps now use `Dump_SHINOBU_105.bin` and `Dump_SHINOBU_105.h8dump`.</BLACKBOX_ROUTE>
  <TELEMETRY_LAYOUT status="UNCHANGED">`ShinobuTelemetryEntry` remains 64B and the ring remains 300 entries.</TELEMETRY_LAYOUT>
  <FAULT_PATH status="UNCHANGED">Invalid math, overflow, and solve-over-budget triggers still call `DumpBlackBox(...)` once per fault session.</FAULT_PATH>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=70, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Actual NaN/fault replay and dump-file validation remain pending until Unity/runtime proof is available.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 36 Stale Forensic Claim Repair

What was wrong:
- Earlier audit text still described the indirect args ABI as 32B/indexed and Burst mode as Fast, even though later loops corrected the runtime to a 16B procedural row and deterministic Burst jobs.

What was done:
- Repaired stale top-of-log claims to point at the superseding Loop 14 and Loop 27 evidence.
- Re-ran the stale-proof search for old indirect-size claims, old indexed stride claims, and active Fast-mode proof text.

Cinematic Cheats used:
- No runtime fake changed. This is forensic hygiene for future agents.

Exact Microseconds saved:
- 0us runtime. Documentation-only.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="36">
  <INDIRECT_ARGS_DOC status="PASS">Stale 32B/current-indexed indirect args claims were replaced with superseded notes or current 16B procedural ABI proof.</INDIRECT_ARGS_DOC>
  <BURST_DOC status="PASS">Stale active `FloatMode.Fast` proof text now points to Loop 27 deterministic Burst correction.</BURST_DOC>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=68, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_CODE status="UNCHANGED">No C# or shader source was changed in Loop 36.</RUNTIME_CODE>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 37 HZB Sanitizer Consistency Repair

What was wrong:
- The HZB metadata sanitizer could derive `width/height` from the bound texture but preserve stale caller `1/width` and `1/height`, making compute cull pixel addressing internally inconsistent.
- A valid depth texture with missing caller mip count disabled HZB entirely instead of falling back to a safe mip0 sample.

What was done:
- Changed `SanitizeDepthPyramidMipCount()` to return mip0 when a texture exists and requested mip count is missing or zero.
- Changed `SanitizeDepthPyramidTexelSize()` to recompute inverse texel dimensions unless caller dimensions are valid and the inverse values match the final dimensions within 5%.
- Re-ran SHINOBU runtime/shader forbidden-pattern scan, Burst parity, stale-log search, and `git diff --check`.

Cinematic Cheats used:
- The GPU culler remains the Dear Lie: cheap frustum/density/HZB compaction replaces CPU fish visibility lists. This patch makes the fake coherent when render-pipeline metadata is incomplete.

Exact Microseconds saved:
- 0us measured. This is correctness hardening; it prevents silent GPU overdraw or underdraw caused by bad HZB metadata. Unity import/profiler proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="37">
  <HZB_MIP_FALLBACK status="PASS">Valid depth texture plus missing mip count now uses mip0 instead of disabling occlusion.</HZB_MIP_FALLBACK>
  <HZB_TEXEL_CONSISTENCY status="PASS">Inverse texel values are accepted only when coherent with final width/height; stale caller metadata is recomputed.</HZB_TEXEL_CONSISTENCY>
  <STATIC_SCAN status="PASS">Forbidden SHINOBU runtime/shader scan returned no hits; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity compute import, actual HZB texture binding, Frame Debugger, and profiler proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 38 Procedural Draw API Seam Audit

What was wrong:
- The newest SHINOBU draw path uses `Graphics.DrawProceduralIndirect` with a `GraphicsBuffer` args buffer; without a legal build, the risk is a source-level API mismatch hidden by the external compile wall.

What was done:
- Verified identical `Graphics.DrawProceduralIndirect(material, bounds, topology, GraphicsBuffer, 0, null, null, ShadowCastingMode, bool, layer)` call shape in `ProceduralWreckageGpuUploadDispatcher`, `ProceduralCoralGpuUploadDispatcher`, and `ShinobuPlasmaBeamRuntime`.
- Verified SHINOBU CPU-written matrix/custom/fallback args buffers use `GraphicsBuffer.UsageFlags.LockBufferForWrite`.
- Verified SHINOBU GPU-written culled args buffer is intentionally `IndirectArguments | Raw`, count 4, stride 4, and is consumed as a 16B `RWByteAddressBuffer` args row without CPU readback.
- Re-ran the SHINOBU runtime/shader forbidden-pattern scan.

Cinematic Cheats used:
- No new fake. This confirms the existing Dear Lie render route remains one procedural draw, not mesh instancing or GameObject hydration.

Exact Microseconds saved:
- 0us measured. The GPU-written raw args row avoids CPU visible-list count readback; profiler proof remains pending.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="38">
  <DRAW_API_PRECEDENT status="PASS">Project precedent confirms `Graphics.DrawProceduralIndirect` with `GraphicsBuffer` args in three existing systems.</DRAW_API_PRECEDENT>
  <BUFFER_USAGE status="PASS">CPU-written SHINOBU buffers use `LockBufferForWrite`; GPU-written culled args are raw indirect and never CPU-locked.</BUFFER_USAGE>
  <STATIC_SCAN status="PASS">Forbidden SHINOBU runtime/shader scan returned no hits.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, frame debugger, GPU capture, and profiler proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 39 Mesh Indexed ABI Residue Removal

What was wrong:
- An unused public `TryUploadIndirectDrawArgs(GraphicsBuffer, Mesh, int)` overload still accepted a Unity `Mesh` and wrote `GetIndexCount/GetIndexStart` into the 16B procedural args DTO. That kept indexed draw semantics alive inside the SHINOBU swarm seam.

What was done:
- Removed the mesh overload.
- Verified no SHINOBU runtime hits remain for `Mesh mesh`, `GetIndexCount`, `GetIndexStart`, `DrawMesh`, `SkinnedMeshRenderer`, `Animator`, `GameObject`, or `Instantiate`.
- Re-ran SHINOBU runtime/shader forbidden-pattern scan, Burst parity, stale-log search, and `git diff --check`.

Cinematic Cheats used:
- The swarm remains a meshless procedural triangle silhouette route. No mesh fallback is allowed in the SHINOBU draw-args seam.

Exact Microseconds saved:
- 0us measured. The deleted overload was unused; this is compile-wall/API hygiene and future-regression prevention.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="39">
  <MESH_SEAM status="PASS">Removed the only SHINOBU mesh/index-count draw-args overload.</MESH_SEAM>
  <PROCEDURAL_ONLY status="PASS">Current SHINOBU runtime scan has no `Mesh mesh`, `GetIndexCount`, `GetIndexStart`, `DrawMesh`, `GameObject`, `Instantiate`, `Animator`, or `SkinnedMeshRenderer` hits.</PROCEDURAL_ONLY>
  <STATIC_SCAN status="PASS">Forbidden SHINOBU runtime/shader scan returned no hits; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, draw execution, GPU capture, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 40 Procedural Render Bounds Fallback

What was wrong:
- `SanitizeRenderBounds(default(Bounds))` collapsed to a 1m cube. That can make Unity frustum-cull a valid procedural swarm before `Graphics.DrawProceduralIndirect` reaches the shader, especially when an integrator binds the material first and supplies bounds later.

What was done:
- Changed `SanitizeRenderBounds()` to classify non-finite, zero, negative, and sub-millimeter extents as invalid input.
- Invalid extents now fall back to the existing dehydration envelope: `DefaultDehydrateDistanceMeters * 2f`, currently 400m per axis.
- Valid finite caller bounds are preserved, with the existing 1m minimum still applied as a last guard.
- Re-ran SHINOBU runtime/shader/compute forbidden-pattern scan, Burst parity, and `git diff --check`.

Cinematic Cheats used:
- No new simulation. This protects the existing Dear Lie: meshless procedural triangle silhouettes driven by matrix/custom buffers, not GameObjects, mesh renderers, or physics bodies.

Exact Microseconds saved:
- 0us measured. This is draw-path correctness hardening; the win is preventing false blank-swarm culling without adding per-boid work.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="40">
  <RENDER_BOUNDS status="PASS">Default/zero/negative/non-finite extents now expand to the 400m dehydration envelope instead of a 1m cube.</RENDER_BOUNDS>
  <PROCEDURAL_ONLY status="PASS">No mesh, GameObject, Animator, SkinnedMeshRenderer, Instantiate, DrawMesh, GetIndexCount, or GetIndexStart patterns exist in SHINOBU runtime.</PROCEDURAL_ONLY>
  <STATIC_SCAN status="PASS">Forbidden SHINOBU runtime/shader/compute scan returned no hits; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, Frame Debugger, GPU capture, profiler, and player-build proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 41 Compute UV Bounds HLSL Ambiguity Removal

What was wrong:
- The HZB compute shader used scalar swizzles (`0.0.xx` and `1.0.xx`) in UV bounds checks. That is compact but avoidable shader-compiler ambiguity.

What was done:
- Replaced the UV bounds branch with explicit scalar component comparisons for `uv.x` and `uv.y`.
- Verified no scalar-swizzle residue or UV `any()` bounds shortcut remains in the compute shader.
- Re-ran SHINOBU runtime/shader/compute forbidden-pattern scan, Burst parity, and `git diff --check`.

Cinematic Cheats used:
- No new fake. The GPU HZB/density compaction fake remains intact; this only removes shader syntax ambiguity.

Exact Microseconds saved:
- 0us measured. The branch is equivalent; the value is avoiding a shader import/compiler failure on stricter backends.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="41">
  <HLSL_SYNTAX status="PASS">Scalar-vector swizzle in compute UV bounds check was removed.</HLSL_SYNTAX>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity compute import, GPU dispatch, Frame Debugger, and profiler proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 42 Procedural Draw Layer Clamp

What was wrong:
- `ShinobuBoidGpuUploadDispatcher.TryDraw()` accepted a raw render layer and passed it to `Graphics.DrawProceduralIndirect`. High-level SHINOBU wrappers clamp layers, but the dispatcher is also a public integration seam.

What was done:
- Added a dispatcher-local `math.clamp(layer, 0, 31)` immediately before the procedural draw call.
- Re-ran the draw-call source slice, SHINOBU runtime/shader/compute forbidden-pattern scan, Burst parity, and `git diff --check`.

Cinematic Cheats used:
- No new fake. The existing meshless procedural draw remains the only render route.

Exact Microseconds saved:
- 0us measured. This is API hardening; it avoids invalid render-layer failures without adding per-boid work.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="42">
  <DRAW_LAYER status="PASS">Low-level procedural dispatcher now clamps Unity layer to `0..31` before draw submission.</DRAW_LAYER>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, draw execution, Frame Debugger, GPU capture, and profiler proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 43 Global Quality Active Budget Anchor

What was wrong:
- Task 11 says `GlobalQualityWeight=0.1` must render/simulate 5% of hydrated biomass. The prior active-budget cubic was continuous but under-shot that anchor for a 100k swarm.

What was done:
- Added `ResolveActiveBudgetFraction()`.
- Anchored active density at 1% for quality 0.0, 5% for quality 0.1, and 100% for quality 1.0.
- Kept the curve data-driven through `math.lerp`, `Smooth01`, and `math.step`; no hardware boolean branch was added.
- Re-ran the active-budget source slice, SHINOBU forbidden-pattern scan, Burst parity, and `git diff --check`.

Cinematic Cheats used:
- No simulation realism added. This is population LOD math: sparse flow-following survival density at low quality, full visual overkill on high-end hardware.

Exact Microseconds saved:
- 0us measured. The schedule-path scalar helper is negligible; the value is predictable thermal population shedding at the exact XML anchor.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="43">
  <QUALITY_ANCHOR status="PASS">Active swarm fraction is now 1% at weight 0.0, 5% at weight 0.1, and 100% at weight 1.0.</QUALITY_ANCHOR>
  <NO_BINARY_SWITCH status="PASS">Curve uses `math.lerp`, `Smooth01`, and `math.step`; no hardware branch was added.</NO_BINARY_SWITCH>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, profiler, and runtime density visual proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 44 Low-Quality Neighbor Solve Collapse

What was wrong:
- Task 11 says `GlobalQualityWeight=0.1` drops cohesion and alignment and relies on flow-following. The prior code still entered the neighbor query and applied separation, so low quality paid the bucket traversal cost and still produced non-flow steering.

What was done:
- Added `ResolveNeighborSolveWeight()`.
- Gated `QueryNeighbors()` in `BoidFlockingJob` when that weight is zero.
- Scaled separation, alignment, and cohesion by `neighborSolve01` when the neighbor solve is active.
- Verified anchors: q=0/0.1/0.12 -> 0, q=0.21 -> 0.5, q=0.3/1 -> 1.
- Re-ran SHINOBU forbidden-pattern scan, Burst parity scan, and `git diff --check`.

Cinematic Cheats used:
- Low-quality boids now sell motion with emergency flow-following, predator panic impulse, and SDF wall vortex swirl. They do not compute Reynolds neighbor behavior until the quality curve gives budget back.

Exact Microseconds saved:
- Pending profiler proof. Static cost removed at q=0.1 is up to 27 spatial buckets plus bounded chain traversal per visible boid.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="44">
  <QUALITY_COLLAPSE status="PASS">Neighbor traversal and separation/alignment/cohesion are zero through quality 0.12 and full by 0.3.</QUALITY_COLLAPSE>
  <TASK_11_ANCHOR status="PASS">At quality 0.1 the active-budget anchor remains 5%, but steering is flow-following only except predator and wall Dear Lie responses.</TASK_11_ANCHOR>
  <NO_BINARY_SWITCH status="PASS">The collapse uses `Smooth01` and a continuous scalar, not a hardware branch.</NO_BINARY_SWITCH>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, profiler, and visual-density proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 45 Sqrt-Free Predator Panic Falloff

What was wrong:
- Predator avoidance used `math.sqrt(distSq)` to calculate a visual panic proximity scalar. The exact distance is unnecessary for a bounded flee impulse and violates the bias toward squared-distance hot-path math.

What was done:
- Replaced `sqrt(distSq) / radius` with `distSq * math.rcp(radiusSq)`.
- Kept `radiusSq >= 1` so the reciprocal is bounded.
- Preserved the sector-wide predator panic branch and the inverse flee vector.
- Re-ran SHINOBU forbidden-pattern scan, Burst parity scan, repo-root `git diff --check`, and the CPU/compiler gate.

Cinematic Cheats used:
- Predator shatter remains a visual-math panic impulse, not collider truth. Squared falloff is deliberately perceptual, not physically exact.

Exact Microseconds saved:
- Pending profiler proof. Source-level cost removed: one panic-path square root per boid affected by a predator signal.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="45">
  <PREDATOR_MATH status="PASS">Predator panic proximity now uses squared-distance reciprocal math, not `sqrt(distSq)`.</PREDATOR_MATH>
  <NAN_GUARD status="PASS">`radiusSq` is clamped to at least 1 before reciprocal.</NAN_GUARD>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, profiler, and predator shatter visual proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 46 Squared-Speed Clamp and Final Speed Reuse

What was wrong:
- `BoidFlockingJob` computed `math.length(velocity)`, divided by `speed` to clamp velocity, then computed `math.length(velocity)` again for `BoidStateDTO.Speed`. That is unnecessary hot-path ALU and a visible division-by-speed risk.

What was done:
- Replaced the clamp with `speedSq = math.lengthsq(velocity)`.
- Invalid or near-zero velocity now resets to `forward * maxSpeed`.
- Over-speed velocity now scales by `maxSpeed * math.rsqrt(math.max(0.0001f, speedSq))`.
- Reused a single `finalSpeed` scalar for `BoidStateDTO.Speed`.
- Verified old `float speed = math.length`, `/ speed`, and `boidState.Speed = math.length` residues are absent.

Cinematic Cheats used:
- No physical truth added. This preserves the same tuned speed cap while reducing math cost in the visual swarm solve.

Exact Microseconds saved:
- Pending profiler proof. Source-level cost removed: one redundant speed length calculation and the divide-by-speed normalization path per active boid.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="46">
  <SPEED_CLAMP status="PASS">Speed limiting now uses squared speed and guarded `rsqrt`.</SPEED_CLAMP>
  <DTO_SPEED status="PASS">`BoidStateDTO.Speed` receives the carried `finalSpeed` scalar instead of recomputing `math.length(velocity)`.</DTO_SPEED>
  <NAN_GUARD status="PASS">Invalid or near-zero speed resets to finite `forward * maxSpeed`; reciprocal uses `max(0.0001f, speedSq)`.</NAN_GUARD>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, Burst Inspector, profiler, and visual proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 47 Mock SDF Rsqrt Reuse

What was wrong:
- `MockTerrainSampler.SphereSdf()` computed `sqrt(lenSq)` for distance and `rsqrt(lenSq)` again for the normal. That duplicated scalar work in the SDF wall-vortex Dear Lie path.

What was done:
- Added one guarded `safeLenSq`.
- Calculated one `invLen = math.rsqrt(safeLenSq)`.
- Derived `len = safeLenSq * invLen`.
- Reused `invLen` for the normal.
- Re-ran SDF residue scan, SHINOBU forbidden-pattern scan, Burst parity scan, repo-root `git diff --check`, and CPU/compiler gate.

Cinematic Cheats used:
- The cave/reef response remains an SDF math fake plus cross-product vortex swirl. No raycasts, MeshColliders, or real fluid simulation were introduced.

Exact Microseconds saved:
- Pending profiler proof. Source-level cost removed: duplicate sqrt/rsqrt-style work per mock sphere SDF sample.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="47">
  <SDF_MATH status="PASS">Sphere SDF now derives distance and normal from one guarded `rsqrt`.</SDF_MATH>
  <DEAR_LIE status="PASS">Wall response remains direct SDF sampling plus cross-product swirl; no physics query path was added.</DEAR_LIE>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, profiler, and wall-swirl visual proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 48 Guarded Length Route

What was wrong:
- Flow tensor scalar lanes and local-shift state hydration still used direct `math.length`, and final speed storage still used a direct `math.sqrt`. Those values are scalar metadata, not a reason to bypass the guarded rsqrt discipline.

What was done:
- Added `SafeLength(float3)` with finite and epsilon checks.
- Routed flow tensor strength/curl/turbulence scalar lanes through `SafeLength`.
- Routed `LocalShiftAndSpatialHashJob` `BuildBoidState(..., speed)` calls through `SafeLength(entity.Velocity)`.
- Replaced final speed `sqrt` with `speedSq * math.rsqrt(math.max(0.00000001f, speedSq))`.
- Verified no direct `math.sqrt` or `math.length(` calls remain in `ShinobuEcosystemBalancer.cs`.

Cinematic Cheats used:
- No new simulation. This keeps scalar metadata cheap and guarded while preserving the flow-field and swarm visual fakes.

Exact Microseconds saved:
- Pending profiler proof. Source-level cost removed: remaining direct square-root/length helper calls in SHINOBU runtime.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="48">
  <LENGTH_ROUTE status="PASS">`ShinobuEcosystemBalancer.cs` has zero direct `math.sqrt` or `math.length(` calls.</LENGTH_ROUTE>
  <NAN_GUARD status="PASS">All replacement length paths check finite length-squared and epsilon before `rsqrt`, or clamp denominator before `rsqrt`.</NAN_GUARD>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, Burst Inspector, profiler, and visual proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 49 Compile-Wall Isolation Audit

What was wrong:
- `AI/Ecosystem` has no local runtime asmdef. SHINOBU source is currently compiled by the root `Hecton8.Core` asmdef, and `ShinobuEcosystemBalancer.cs` imports `Hecton8.World` for `AbsoluteUniversePosition`.

What was done:
- Verified there is no `Assets/_Project/Scripts/AI/Ecosystem/*.asmdef`.
- Verified `AbsoluteUniversePosition` currently lives under the root Core assembly path despite namespace `Hecton8.World`.
- Found then-current boot/editor references into SHINOBU: `EcosystemRuntimeInstaller` and the SHINOBU editor facade.
- Rejected a blind asmdef split because it would require moving boot/editor seams and deciding where the AUP contract belongs.

Cinematic Cheats used:
- None. This is compile-wall forensics only.

Exact Microseconds saved:
- 0us runtime. Iteration-time savings require an integrator-owned assembly split; this loop identifies the blocker instead of creating a false isolation claim.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="49">
  <COMPILE_WALL status="BLOCKED_BY_PREEXISTING_STRUCTURE">SHINOBU currently compiles under root `Hecton8.Core`; no local AI/Ecosystem asmdef exists.</COMPILE_WALL>
  <AUP_ROUTE status="BLOCKED_BY_CORE_WORLD_CONTRACT">`AbsoluteUniversePosition` is required and currently resides in the root Core assembly under namespace `Hecton8.World`.</AUP_ROUTE>
  <NO_NEW_SIBLING_REFERENCE status="PASS">No new direct sibling runtime assembly reference was added in this loop.</NO_NEW_SIBLING_REFERENCE>
  <REJECTED_SPLIT status="PASS">Blind asmdef split rejected because boot/editor references need an integrator-owned seam move.</REJECTED_SPLIT>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">No Unity import/build proof; compile remains blocked by external project state and CPU gate.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 50 HZB Z-Buffer Parameter Guard

What was wrong:
- HZB culling sanitized texture dimensions/mips but trusted caller-provided `_ZBufferParams`. Non-finite or denominator-degenerate values could corrupt occlusion decisions.

What was done:
- Added `_proceduralCullHasValidZBufferParams`.
- Added `IsUsableZBufferParams(Vector4)`.
- Stored a safe fallback vector when caller params are unusable.
- Gated depth occlusion on valid depth pyramid, valid mip data, valid z-buffer params, and quality >= 0.3.

Cinematic Cheats used:
- No simulation added. This protects the GPU Dear Lie culler while frustum/density culling continue when HZB depth is not trustworthy.

Exact Microseconds saved:
- 0us measured. The value is correctness: avoids false accept/reject culling from bad render-graph inputs.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="50">
  <HZB_PARAMS status="PASS">Depth occlusion requires usable `_ZBufferParams`; invalid params fall back to frustum/density culling.</HZB_PARAMS>
  <QUALITY_GATE status="PASS">HZB remains disabled below q=0.3 by continuous quality gate.</QUALITY_GATE>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=95.5, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, Frame Debugger, RenderGraph binding, and HZB readback proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 51 GPU Density Double-Dip Removal

What was wrong:
- `ResolveActiveEntityBudget()` already owned quality-driven population density, but `ResolveGpuCullingParams()` applied a second quality-derived density step. At q=0.1 that could reduce the mandated 5% hydrated swarm to roughly 1% before visibility culling.

What was done:
- Removed the automatic quality-derived GPU density step.
- Preserved explicit caller-owned density step clamped to 1..8.
- Verified the active-budget anchor values: q=0.1 -> 5000/100000 active rows.

Cinematic Cheats used:
- The procedural GPU silhouette route remains the visual fake. This loop removed an accidental double-decimation, not a physical simulation.

Exact Microseconds saved:
- CPU: 0us. GPU vertex work may increase where the previous culler over-decimated; that is required to satisfy the 5% quality contract.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="51">
  <QUALITY_OWNER status="PASS">CPU active-budget curve is the single owner of quality-driven swarm population.</QUALITY_OWNER>
  <GPU_DENSITY status="PASS">GPU density step is now explicit caller input only, clamped 1..8.</GPU_DENSITY>
  <STATIC_SCAN status="PASS">No SHINOBU forbidden patterns; jobs=9, deterministic=9, fast=0.</STATIC_SCAN>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, GPU count readback, visual density proof, and profiler evidence remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 52 Exact Editor Facade Identity

What was wrong:
- The editor facade still carried stale `BiomassBoidTunerWindow` class/file identity and a legacy `HECTON-8/Biomass & Boid Tuner` menu alias. The requested facade is SHINOBU/Abyssal Swarm, not a generic biomass/boid shell.
- The SceneView vector preview also used `Time.realtimeSinceStartup`, creating a needless Unity Time dependency in the SHINOBU authoring path.

What was done:
- Renamed `BiomassBoidTunerWindow.cs` to `AbyssalSwarmTunerWindow.cs`.
- Preserved the Unity `.meta` GUID `bb8da9056d0529046a7ac6f9e08d5df8`.
- Renamed the public class and `GetWindow<T>()` target to `AbyssalSwarmTunerWindow`.
- Removed the stale `HECTON-8/Biomass & Boid Tuner` menu alias.
- Replaced editor `Time.realtimeSinceStartup` flow phase with a deterministic position-derived phase.

Cinematic Cheats used:
- The editor vector preview remains a sparse flow-field fake. No runtime GameObjects, no debug UI in player builds, and no physical fluid simulation were introduced.

Exact Microseconds saved:
- Runtime: 0us. Editor-only path loses one cold time query and removes stale reflection/menu ambiguity.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="52">
  <EDITOR_FACADE status="PASS">Class/file/menu identity now matches Abyssal Swarm facade; legacy Biomass/Boid menu alias removed.</EDITOR_FACADE>
  <META_GUID status="PASS">Unity meta GUID preserved across rename.</META_GUID>
  <DETERMINISM status="PASS">Editor vector phase no longer uses Unity Time.</DETERMINISM>
  <STATIC_SCAN status="PASS">No stale `BiomassBoidTunerWindow`, no legacy menu alias, no `Time.` in facade.</STATIC_SCAN>
  <BUILD_STATUS status="PENDING_GATE">Build not launched yet; CPU/compiler gate must be checked after full static pass.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import and editor window smoke proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 53 Meta Hygiene and Compiler Gate

What was wrong:
- `Hecton_AbyssalSwarmCull.compute.meta` had trailing spaces on blank YAML scalar fields.
- Normal `git diff --check` does not cover untracked files, so the new compute/meta files needed an explicit whitespace scan.
- CPU was below 50%, but seven compiler processes were active.

What was done:
- Removed trailing spaces from `userData`, `assetBundleName`, and `assetBundleVariant`.
- Reran case-sensitive SHINOBU forbidden-pattern scan.
- Reran Burst parity scan.
- Reran tracked `git diff --check`.
- Reran explicit untracked-file whitespace scan.
- Held the build because active compiler count was 7.

Cinematic Cheats used:
- None. This is source hygiene and verification gating only.

Exact Microseconds saved:
- 0us runtime. Hardware protected by avoiding compiler contention with active `dotnet`/`csc` processes.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="53">
  <META_HYGIENE status="PASS">New SHINOBU compute/meta/editor files have no trailing whitespace hits.</META_HYGIENE>
  <STATIC_SCAN status="PASS">Case-sensitive SHINOBU forbidden-pattern scan returned no hits.</STATIC_SCAN>
  <BURST_PARITY status="PASS">jobs=9, deterministic=9, fast=0.</BURST_PARITY>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">Tracked `git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <BUILD_STATUS status="COMPILER_GATED">No build launched: CPU=45.3, compiler_count=7.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, compile, Play Mode, profiler, and visual proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 54 Designer Bridge Facade Completion

What was wrong:
- The editor facade had live sliders and telemetry, but not the full designer bridge proof: CSV source paths, output route, schema, row count, checksum, validation state, reload control, and DTO layout summary.
- A reload button needed a safe route that did not expose parser internals in player builds.

What was done:
- Added a `Designer Bridge` block to `AbyssalSwarmTunerWindow`.
- Reports tuning/species CSV source paths, data rows, byte counts, and FNV1A32 checksums.
- Names runtime outputs: `BufferID.ShinobuEcosystemTuning` and `BufferID.ShinobuSwarmSpeciesProfiles`.
- Reports live Vault row counts when DataVault is available.
- Prints DTO layout summary for `BoidStateDTO`, `BoidTargetDTO`, `BoidMatrixDTO`, `BoidIndirectArgsDTO`, `ShinobuEcosystemTuning`, and `SwarmSpeciesProfileDTO`.
- Warns if expected ARM64 sizes drift.
- Added play-mode `Force CSV -> Vault Reload`.
- Added `ShinobuEcosystemBalancer.ForceDesignerDataReload()` behind `#if UNITY_EDITOR`, reusing cold CSV monitor logic.

Cinematic Cheats used:
- None. This is designer-control infrastructure and does not add simulation.

Exact Microseconds saved:
- Runtime: 0us. Editor-only CSV/hash/layout proof replaces guesswork and does not enter player hot paths.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="54">
  <EDITOR_FACADE status="PASS">Facade now exposes source paths, schema, rows, checksums, validation, layout summary, and reload control.</EDITOR_FACADE>
  <CSV_ROUTE status="PASS">Reload reuses existing cold zero-GC runtime CSV monitor and DataVault output buffers.</CSV_ROUTE>
  <PLAYER_BUILD_SURFACE status="PASS">Reload method is compiled only under `UNITY_EDITOR`.</PLAYER_BUILD_SURFACE>
  <STATIC_SCAN status="PASS">No stale class/menu name, no `Time.` in facade, no SHINOBU forbidden runtime/shader patterns.</STATIC_SCAN>
  <BURST_PARITY status="PASS">jobs=9, deterministic=9, fast=0.</BURST_PARITY>
  <BUILD_STATUS status="COMPILER_GATED">No build launched: CPU=62.9, compiler_count=7.</BUILD_STATUS>
  <RUNTIME_PROOF status="PENDING_VERIFICATION">Unity import, editor window smoke, CSV reload smoke, and profiler proof remain pending.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 55 Forensic Log Chronology Repair

What was wrong:
- Loop 52-54 rationale/log entries were present but inserted above older loop entries, violating the top-old/bottom-new audit rule.

What was done:
- Mechanically moved Loop 52-54 rationale entries below Loop 51.
- Mechanically moved Loop 53-54 log entries below Loop 52.
- Verified Rationale and LOG now show monotonic Loop 45 -> Loop 54 ordering.
- Re-ran `git diff --check` and untracked SHINOBU whitespace scan.

Cinematic Cheats used:
- None. Documentation hygiene only.

Exact Microseconds saved:
- 0us runtime. Audit recovery risk reduced.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="55">
  <RATIONALE_ORDER status="PASS">Loop 45 through Loop 54 are monotonic in `Rationale_SHINOBU_105.md`.</RATIONALE_ORDER>
  <LOG_ORDER status="PASS">Loop 45 through Loop 54 are monotonic in `LOG_SHINOBU_105.md`.</LOG_ORDER>
  <DIFF_CHECK status="PASS_WITH_WARNINGS">`git diff --check` reports CRLF normalization warnings only.</DIFF_CHECK>
  <RUNTIME_PROOF status="NOT_APPLICABLE">No runtime code changed in this loop.</RUNTIME_PROOF>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 56 Facade Binary Output Label

What was wrong:
- The bridge row label said `Runtime Output`; the Designer Facade mandate expects a binary output path/route.

What was done:
- Renamed the row to `Binary Output`.
- Kept the route owner-local: `GlobalDataVault: ShinobuEcosystemTuning, ShinobuSwarmSpeciesProfiles`.
- Re-ran facade stale-name/time scan, SHINOBU runtime/shader forbidden scan, Burst parity scan, untracked whitespace scan, and CPU/compiler gate.

Cinematic Cheats used:
- None. Editor wording and verification only.

Exact Microseconds saved:
- 0us runtime.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="56">
  <EDITOR_FACADE status="PASS">Designer bridge now explicitly labels the unmanaged DataVault route as `Binary Output`.</EDITOR_FACADE>
  <STATIC_SCAN status="PASS">No stale facade names, no legacy menu alias, no `Time.` in facade; no SHINOBU runtime/shader forbidden hits.</STATIC_SCAN>
  <BURST_PARITY status="PASS">jobs=9, deterministic=9, fast=0.</BURST_PARITY>
  <BUILD_STATUS status="CPU_GATED">No build launched: CPU=100, compiler_count=0.</BUILD_STATUS>
</SELF_AUDIT_DELTA>

## 2026-05-19 Loop 57 Guarded Build External Blocker

What was wrong:
- A compile probe was needed after SHINOBU C# edits.
- The project file references `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`, but that file is missing/deleted in the current worktree.

What was done:
- Ran `dotnet build Hecton8.Core.csproj --no-restore --verbosity:minimal` only after CPU=20.4 and compiler_count=0.
- Build failed with CS2001 for missing `Construction/LogisticsPipeEvents.cs`.
- Verified `Test-Path` is false.
- Verified `git status` shows `D Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.
- Did not edit Construction or project-file ownership from SHINOBU.

Cinematic Cheats used:
- None. Compile-wall evidence only.

Exact Microseconds saved:
- 0us runtime. Avoided repeated compile attempts after one external missing-source failure.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="57">
  <BUILD_GATE status="PASS">Build launched only when CPU=20.4 and compiler_count=0.</BUILD_GATE>
  <BUILD_RESULT status="BLOCKED_BY_EXTERNAL_SOURCE">CS2001: missing `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs`.</BUILD_RESULT>
  <DOMAIN_BOUNDARY status="PASS">No Construction-domain restore/project-file mutation performed by SHINOBU_105.</DOMAIN_BOUNDARY>
  <SHINOBU_ERRORS status="NONE_EMITTED">Build stopped before SHINOBU diagnostics.</SHINOBU_ERRORS>
</SELF_AUDIT_DELTA>
