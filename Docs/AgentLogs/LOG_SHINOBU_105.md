# LOG_SHINOBU_105

## 2026-05-19 - Abyssal Current Fluid Dynamics Pass

What was wrong:
- Macro biomass and micro boid visuals were already partially routed through `ShinobuEcosystemBalancer`, but the route was capped at 5,000 entities and still used ambiguous render matrix storage.
- Explicit SHINOBU_105 DTOs were missing: no exact 32B `BoidStateDTO`, no explicit 64B matrix payload, no profile DTO for swarm species CSV.
- Existing Burst jobs lacked the required `CompileSynchronously = true` directive and `[NoAlias]` field proof.
- The wall-avoidance response used normal push, not the requested Dear Lie vortex swirl.
- Telemetry dumped `Dump_ECOSYSTEM.*`, not the requested `Dump_ABYSSAL_SWARM.bin`, and did not flag the 1.5ms solve threshold.
- Editor facade was still named Biomass & Boid Tuner and lacked solve-time graph/vector-field diagnostics.

What was done:
- Raised SHINOBU entity capacity to 100,000 and made active budget continuous through `HomeostasisBrain.GlobalQualityWeight`.
- Added explicit DTOs: `BoidStateDTO` 32B, `BoidTargetDTO` 32B, `BoidMatrixDTO` 64B, `BoidIndirectArgsDTO` 32B, `AbyssalFlowTensorDTO` 64B, `SwarmSpeciesProfileDTO` 32B.
- Changed `ShinobuRenderMatrices` Vault handle from `float4x4` to `BoidMatrixDTO` and requested large state/render buffers with `NativeArrayOptions.UninitializedMemory`.
- Renamed the steering and hash jobs to `BoidFlockingJob` and `BuildBoidSpatialHashJob`; all changed jobs now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
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
    <TASK id="17" status="PASS">300-frame telemetry ring dumps `Dump_ABYSSAL_SWARM.bin` on fault or >1.5ms solve.</TASK>
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
    <BoidIndirectArgsDTO size="32">First 20 bytes are indirect draw row; final 12 bytes are padding to 32B.</BoidIndirectArgsDTO>
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
- `TryUploadIndirectDrawArgs()` now rejects zero-count buffers and stride mismatches before mapping. Required stride is `GraphicsBuffer.IndirectDrawIndexedArgs.size`.

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
- Chained `WriteBoidIndirectArgsJob` after `CountTelemetryCountersJob` so the current continuous active budget is written as a 32B unmanaged row every scheduled frame.
- Added a `TryUploadIndirectDrawArgs(GraphicsBuffer, NativeArray<BoidIndirectArgsDTO>)` bridge for zero-managed-array upload into a `GraphicsBuffer.IndirectDrawIndexedArgs` buffer.

Cinematic Cheats used:
- No new physics. This pass preserves the GPU-driven Dear Lie: one draw submission represents up to 100,000 fish while shader/VAT work carries the visible richness.

Exact Microseconds saved:
- Not measured. The structural saving is avoiding CPU instance-list construction and managed draw-args arrays. Static source scans passed; build/profiler remain blocked by the CPU >50% rule.

<SELF_AUDIT_DELTA agent_id="SHINOBU_105" loop="10">
  <TASK_14_RECONCILIATION status="PASS">Indirect args now have a dedicated Vault owner: `ShinobuBoidIndirectArgs`.</TASK_14_RECONCILIATION>
  <STRUCT_LAYOUT status="PASS">`BoidIndirectArgsDTO` remains 32B: 20B draw row + 12B explicit padding.</STRUCT_LAYOUT>
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
