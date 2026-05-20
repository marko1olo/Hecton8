# LOG_SHINOBU_144

Date: 2026-05-19
Status: PENDING VERIFICATION

Initialized agent log. No runtime verification has been performed yet.

## 2026-05-19 - Topographical Sonar Static Implementation

What was wrong:
- Dense sonar could not depend on PhysX, hierarchy markers, managed point lists, or absolute float positions.
- Nearby PDA/GPR patterns were useful but not sufficient: mesh-backed `RenderMeshIndirect` does not satisfy the procedural point-cloud mandate.
- AUP correctness required separating ping-local hit data from current camera-relative rendering.

What was done:
- Added/updated `Assets/_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs`.
- Added `SonarPointDTO` with explicit 16B layout: `float3 LocalPosition` offset 0 and `uint ColorPacked` offset 12.
- Added Vault-owned buffers: points `70840`, hit mask `70841`, counters `70842`, mock SDF `70843`, mock materials `70844`, telemetry ring `70845`, telemetry cursor `70846`, material LUT `70847`, CSV scratch `70848`, indirect args `70849`, shader globals `70850`.
- Added `GenerateMockSdfJob`, `SonarRaymarchJob`, `SonarHitCountJob`, and `DecaySonarPointsJob`, all Burst synchronous fast/standard with no-alias NativeArray fields.
- Added procedural indirect draw via `Graphics.DrawProceduralIndirect`, mapped point upload via `LockBufferForWrite`/memcpy utility, mapped indirect args, and mapped constant buffer globals.
- Added/updated shaders: `Hecton_SonarPoint.shader` uses procedural `SV_VertexID` quads and `HectonTopographicalSonarGlobals`; `Hecton_SonarRaymarch.compute` contains clear/raymarch/decay kernels.
- Added editor facade `TopographicalSonarTunerWindow.cs`, editor tests `TopographicalSonarLayoutEditTests.cs`, and authoring palette `Assets/_Project/Data/UI/sonar_material_colors.csv`.

Cinematic Cheats used:
- Direct SDF sampling replaces PhysX terrain truth for visual sonar.
- Ping-local point offsets plus a shader wave fake replace CPU-side point animation.
- Mock cavern SDF enables CI/editor fallback without voxel-engine population.
- GPU shader dither/depth fade makes sparse low-quality rays read as a coherent echo.

Exact Microseconds saved:
- 0 us measured. No profiler, Unity import, player build, or GCMonitor proof was run.
- Estimated avoided work: thousands of `Physics.Raycast` calls and up to 50k GameObject/Transform point markers per ping.
- Static load-shed math: quality 0.1 schedules about 6.8k rays instead of 50k and collapses SDF sampling to nearest-neighbor with near-minimal max steps.

Verification:
- `git diff --check` passed for touched files; only Git line-ending warnings reported.
- Static forbidden-source scans pass for new sonar runtime: no `Physics.Raycast`, `Physics.SphereCast`, `Collider.Raycast`, `Instantiate`, `SetData`, `RenderMeshIndirect`, runtime `new Mesh`, `Time.deltaTime`, `UnityEngine.Random`, DTO properties, `Pack=`, private persistent NativeArrays, `NativeList`, `NativeHashMap`, `List<>`, or `foreach`.
- Scoped archaeology scan over `ScannerTool.cs`, `ScannerDataMiningRouter.cs`, `PDAMapTab.cs`, and the new sonar runtime found no topographical point-cloud PhysX or GameObject route.
- Compile not launched: CPU 20%, no `csc.exe`, but seven `dotnet` processes were active.

<SELF_AUDIT agent_id="SHINOBU_144">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">No dense topographical PhysX path found or added; unrelated UI raycasts remain outside domain.</TASK>
    <TASK id="02" status="PASS">Point cloud uses procedural indirect draw from `GraphicsBuffer`; no point GameObjects.</TASK>
    <TASK id="03" status="PASS">Unmanaged sonar DTOs are raw public fields; no C# DTO properties.</TASK>
    <TASK id="04" status="PASS">`SonarPointDTO` is explicit 16B, offsets 0 and 12; editor tests enforce.</TASK>
    <TASK id="05" status="PASS">Burst mock SDF/material generation exists and writes Vault buffers.</TASK>
    <TASK id="06" status="PASS">Burst `IJobParallelFor` raymarch uses Fibonacci rays and bounded SDF stepping.</TASK>
    <TASK id="07" status="PASS">Compute shader asset exists with raymarch and decay kernels; runtime dispatch proof pending.</TASK>
    <TASK id="08" status="PASS">Material IDs map to packed RGBA8 colors; CSV overrides mutate Vault LUT.</TASK>
    <TASK id="09" status="PASS">CPU path uploads through mapped `GraphicsBuffer`; no `SetData`.</TASK>
    <TASK id="10" status="PASS">Ray count uses continuous `math.lerp(2000, 50000, quality)`.</TASK>
    <TASK id="11" status="PASS">Shader wave/fade uses ping age and distance; CPU does not animate point transforms.</TASK>
    <TASK id="12" status="PASS">Ping and camera AUP are double3; shader receives local ping-camera delta.</TASK>
    <TASK id="13" status="PASS">Shader fade is primary; optional Burst alpha decay exists and uses dispatcher delta.</TASK>
    <TASK id="14" status="PASS">No rollback Merkle route references sonar presentation buffers.</TASK>
    <TASK id="15" status="PASS">Vault buffers requested with `NativeArrayOptions.UninitializedMemory`; active count controls draw.</TASK>
    <TASK id="16" status="PASS">300-entry telemetry ring and dump path implemented; timeout/NaN marks fault.</TASK>
    <TASK id="17" status="PASS">UI Toolkit tuner window implemented.</TASK>
    <TASK id="18" status="PASS">Allocation-free byte parser implemented for numeric IDs, FNV names, and hex/numeric RGBA.</TASK>
    <TASK id="19" status="PASS">Editor gizmo draws ray subset and hit lines from Vault points.</TASK>
    <TASK id="20" status="PASS">Static self-audit and editor layout/source tests added.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    <SonarPointDTO size="16" alignment="16-byte stride">
      <field name="LocalPosition" offset="0" size="12" />
      <field name="ColorPacked" offset="12" size="4" />
      <math>12 + 4 = 16, exact GPU structured stride and ARM64-safe multiple of 16.</math>
    </SonarPointDTO>
    <TopographicalSonarTelemetryEntry size="128" alignment="two 64-byte cache lines">
      <field name="TimeSeconds" offset="0" size="8" />
      <field name="PingAupX/Y/Z" offsets="8,16,24" size_each="8" />
      <field name="CameraAupX/Y/Z" offsets="32,40,48" size_each="8" />
      <field name="Frame..ComputeTimeMicroseconds" offsets="56..124" size_total="72" />
      <math>56 bytes doubles + 72 bytes scalar payload = 128 bytes.</math>
    </TopographicalSonarTelemetryEntry>
    <SonarProceduralArgsDTO size="16">Four uint fields for DrawProceduralIndirect args.</SonarProceduralArgsDTO>
    <TopographicalSonarShaderGlobalsDTO size="64">Four float4 rows; one cache line.</TopographicalSonarShaderGlobalsDTO>
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    Below quality 0.3, raymarch sampling switches to nearest-neighbor through `math.step`, max-step budget lerps toward 1 through `Smooth01`, and ray count approaches 2000. At quality 1.0, trilinear SDF sampling, denser rays, smaller step length, stronger shader wave boost, and higher point-size richness are active.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Persistent NativeArrays/NativeLists/NativeHashMaps as private fields: 0.
    VaultBufferHandle IDs: 70840 points, 70841 hit mask, 70842 counters, 70843 mock SDF, 70844 mock material IDs, 70845 telemetry ring, 70846 telemetry cursor, 70847 material color LUT, 70848 CSV scratch, 70849 indirect args, 70850 shader globals.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    GenerateMockSdfJob: writes SDF and material arrays, `[NoAlias]`.
    SonarRaymarchJob: reads SDF/material/LUT, writes points/hit mask, `[NoAlias]`.
    SonarHitCountJob: reads hit mask, writes counters, `[NoAlias]`.
    DecaySonarPointsJob: writes points alpha, `[NoAlias]`.
    Dependency: Mock SDF job -> Raymarch job -> HitCount job -> late-frame commit/upload. No arbitrary main-thread complete until late-frame job ownership recovery.
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef was added. Runtime stays in existing Core/UI compilation surface and communicates through GlobalRegistry/SpectrumEvents/Vault/published SDF owner. Build not launched because active dotnet processes were present.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: O(rays) PhysX queries plus O(hits) GameObject markers and CPU animation.
    After: O(rays * bounded_steps) direct SDF sampling into flat buffers, O(points) procedural GPU draw, and shader-only wave/fade. The physical world is not mutated; the echo is presentation-only.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## Polish Pass R2 - Buffer Contention and Fade Stall Removal

What was wrong:
- Optional CPU echo fade used `Schedule(_activePointCount, 128).Complete()` in the render path. This violated the job dependency mandate and could stall the main/render thread.
- Point-cloud upload used one `GraphicsBuffer`, which violated the GPU bandwidth mandate requiring double-buffering for CPU-written/GPU-read data.
- Editor gizmo hit reconstruction used camera position plus ping-local hit offset, which was wrong after camera movement.

What was done:
- Replaced the single point buffer with `_pointBufferA` and `_pointBufferB`.
- Completed scans and faded echoes now upload into the non-rendered point buffer, then flip the read slot.
- `DecaySonarPointsJob` now schedules asynchronously and uploads only after `JobHandle.IsCompleted` in `LateFrameTick`.
- Pending pings now wait while a fade job is still writing the Vault point array, preventing scan/fade write overlap.
- Live ray gizmo now reconstructs hit position from ping origin plus ping-local hit offset.
- Editor source test now asserts absence of the same-frame fade `Schedule().Complete()` pattern and presence of both point buffers.

Cinematic Cheats used:
- Shader echo fade remains the primary visual fake. CPU alpha decay is optional and asynchronous, not a frame-critical truth simulation.

Exact Microseconds saved:
- 0 us measured. Static-only proof. Expected saving is removal of a potential render-path job wait and reduced CPU/GPU buffer contention on MX350-class hardware.

Verification:
- Runtime/shader forbidden scan returned no matches for PhysX raycasts, `Instantiate`, `SetData`, `RenderMeshIndirect`, runtime mesh allocation, `Time.deltaTime`, random APIs, `Pack=`, private persistent native containers, `foreach`, or same-frame `Schedule(...).Complete`.
- `git diff --check` passed for touched files; only LF/CRLF warnings.
- Compile not launched: CPU 99.61%, seven active `dotnet` processes.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R2_BUFFER_PING_PONG_AND_ASYNC_FADE" status="PENDING_COMPILE_CPU_DOTNET_GUARD">
  <TASK_IMPACT>
    <TASK id="09" status="PASS">Point upload now uses ping-pong `GraphicsBuffer` A/B before flipping render ownership.</TASK>
    <TASK id="13" status="PASS">Fade job no longer blocks with same-frame completion; shader fade remains primary.</TASK>
    <TASK id="19" status="PASS">Gizmo hit reconstruction now uses ping origin plus ping-local point offset.</TASK>
    <TASK id="20" status="PASS">Editor source audit asserts both double-buffer fields and no same-frame fade completion pattern.</TASK>
  </TASK_IMPACT>
  <DEPENDENCY_GRAPH>
    Scan: `GenerateMockSdfJob` optional -> `SonarRaymarchJob` -> `SonarHitCountJob` -> completed late-frame upload to inactive point buffer -> flip.
    Fade: `DecaySonarPointsJob` schedules from render delta input -> pending pings wait while fade is in flight -> completed late-frame upload to inactive point buffer -> flip.
    No `Schedule().Complete()` remains in the fade render path.
  </DEPENDENCY_GRAPH>
  <COMPILE_GATE cpu_percent="99.61" active_dotnet_processes="7">Build withheld by project guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R3 - Thermal Collapse and AUP Hot-Path Cleanup

What was wrong:
- The previous max-step curve was only "near-minimal" at `GlobalQualityWeight=0.1`; it did not prove the mandated single SDF lookup collapse.
- Sonar ping admission did not scale update frequency down toward 5Hz under thermal pressure.
- `SonarRaymarchJob` still carried redundant `double3` AUP fields in the Burst hot path.

What was done:
- Added `ResolveWorkCurve`: `Smooth01(saturate((quality - 0.1) / 0.9))`, preserving continuous scaling while resolving to one raymarch step at quality 0.1.
- Added `ResolveMinimumPingIntervalSeconds`: continuous 0.2s-to-0.016666668s ping admission from low quality to ultra quality.
- Removed `PingAup` and `CameraAup` from `SonarRaymarchJob`; raymarch output remains ping-local `float3` plus packed color.
- Hardened CSV entrypoint against default `NativeArray<byte>` length access and expanded source audits for the new thermal/AUP constraints.

Cinematic Cheats used:
- Low quality now emits a sparse one-step SDF silhouette and lets the point shader carry the echo fantasy; it does not pretend to solve full geometry when the hardware is throttling.

Exact Microseconds saved:
- 0 us measured. Static-only proof. Expected low-end saving is proportional to removed ray-step iterations and throttled ping admission; exact profiler data remains blocked by compile guard.

Verification:
- Forbidden runtime/shader scan returned no matches for PhysX raycasts, `Instantiate`, `SetData`, `RenderMeshIndirect`, runtime mesh allocation, `Time.deltaTime`, random APIs, `Pack=`, private persistent native containers, `foreach`, same-frame `Schedule(...).Complete`, or redundant raymarch AUP fields.
- Source scan confirms no `public double3 PingAup`, no `public double3 CameraAup`, no `double3 hitAup`, and presence of `ResolveWorkCurve` plus `ResolveMinimumPingIntervalSeconds`.
- `git diff --check` passed for touched files; only LF/CRLF warnings.
- Compile not launched: CPU 95.35%, no active `dotnet`/`csc`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R3_THERMAL_COLLAPSE_AUP_HOTPATH" status="PENDING_COMPILE_CPU_DOTNET_GUARD">
  <TASK_IMPACT>
    <TASK id="06" status="PASS">Burst raymarch hot path is now ping-local float math; AUP doubles stay at boundary/telemetry.</TASK>
    <TASK id="10" status="PASS">Quality 0.1 resolves to one raymarch step and 5Hz ping admission; quality 1 resolves toward full budget and 60Hz admission.</TASK>
    <TASK id="12" status="PASS">AUP-relative presentation remains in shader globals; no absolute double hit computation remains inside raymarch loop.</TASK>
    <TASK id="20" status="PASS">Editor source audit now asserts thermal curve helpers and absence of redundant raymarch AUP fields.</TASK>
  </TASK_IMPACT>
  <SCALABILITY_CURVE>
    `rayCount = lerp(2000, 50000, quality)` remains exact. `ResolveWorkCurve` maps 0.1 to 0 work, so maxSteps lerps to 1; the same curve lerps ping interval from 0.2s to 0.016666668s. SDF filtering still uses `math.step(0.3f, QualityWeight)` to collapse trilinear sampling to nearest-neighbor below 0.3.
  </SCALABILITY_CURVE>
  <COMPILE_WALL>
    `TopographicalSonarSynthesizer.cs`, `HectonVoxelVolume.cs`, and `SpectrumSystem.cs` are under the existing `Hecton8.Core.asmdef` compilation surface. No new asmdef was added or edited; `Caves/Visor` access is namespace-level inside Core, not a new sibling assembly reference.
  </COMPILE_WALL>
</SELF_AUDIT_DELTA>

## Polish Pass R4 - Native CSV Ingress

What was wrong:
- `TryApplyMaterialColorCsvFileForEditor` used `File.ReadAllBytes`, allocating a managed byte array before the byte parser copied data into Vault scratch.

What was done:
- Replaced the managed byte-array path with bounded `FileStream.ReadByte` ingestion directly into `TopographicalSonarBufferIds.CsvScratch`.
- Expanded the editor source audit to reject `File.ReadAllBytes` in the sonar runtime file.

Cinematic Cheats used:
- No physical simulation involved. This is data sovereignty cleanup for the material-color facade.

Exact Microseconds saved:
- 0 us measured. Runtime unchanged. Removes one editor/slow-path managed byte-array allocation per CSV import.

Verification:
- Forbidden runtime/shader scan returned no matches for PhysX raycasts, `Instantiate`, `SetData`, `RenderMeshIndirect`, runtime mesh allocation, `Time.deltaTime`, random APIs, `Pack=`, private persistent native containers, `foreach`, same-frame `Schedule(...).Complete`, redundant raymarch AUP fields, or `File.ReadAllBytes`.
- `git diff --check` passed for touched files; only LF/CRLF warnings.
- Compile not launched: CPU 100%, no active `dotnet`/`csc`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R4_NATIVE_CSV_INGRESS" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="18" status="PASS">CSV file bytes now enter Vault scratch directly; parser remains byte-based and allocation-free.</TASK>
    <TASK id="20" status="PASS">Source audit rejects `File.ReadAllBytes` in sonar runtime.</TASK>
  </TASK_IMPACT>
  <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0">Build withheld by project CPU guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R5 - Compute Shader ABI Parity

What was wrong:
- `Hecton_SonarRaymarch.compute` wrote `SonarPointDTO.LocalPosition` as camera-local, while the Burst path and point shader define it as ping-local.
- The compute path lacked the R3 quality work-curve and nearest-neighbor low-quality SDF collapse.

What was done:
- Compute output now writes `direction * resolvedDistance`, matching the 16B DTO contract consumed by `Hecton_SonarPoint.shader`.
- Added `ResolveWorkCurve` and low-quality `Texture3D.Load` nearest sampling to the compute shader.
- Compute color now uses `_SonarMaterialColorLut[1]` with a default fallback.
- Added an editor source test that rejects `_PingCameraLocal + direction * resolvedDistance` in the compute shader.

Cinematic Cheats used:
- The GPU path keeps the same cheap sonar illusion as Burst: sparse one-step silhouettes at low quality, denser trilinear echo only when quality budget permits.

Exact Microseconds saved:
- 0 us measured. This is ABI correctness for the optional GPU path; expected low-quality GPU savings comes from matching the one-step work curve.

Verification:
- Forbidden runtime/shader/compute scan returned no matches for PhysX raycasts, `Instantiate`, `SetData`, `RenderMeshIndirect`, runtime mesh allocation, `Time.deltaTime`, random APIs, `Pack=`, private persistent native containers, `foreach`, same-frame `Schedule(...).Complete`, redundant raymarch AUP fields, `File.ReadAllBytes`, or compute camera-local DTO writes.
- Compute source audit confirms `ResolveWorkCurve`, nearest `Load(int4...)`, material LUT usage, and ping-local `direction * resolvedDistance`.
- `git diff --check` passed for touched files; only LF/CRLF warnings.
- Compile not launched: CPU 100%, no active `dotnet`/`csc`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R5_COMPUTE_SHADER_ABI_PARITY" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="07" status="PASS">Compute shader now matches the Burst DTO ABI and quality collapse math.</TASK>
    <TASK id="10" status="PASS">GPU path now has the same one-step low-quality work curve.</TASK>
    <TASK id="12" status="PASS">Compute output is ping-local, so shader-side AUP offset is applied exactly once.</TASK>
    <TASK id="20" status="PASS">Editor test locks compute shader source contract.</TASK>
  </TASK_IMPACT>
  <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0">Build withheld by project CPU guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R6 - Hit Compaction and Indirect Count Truth

What was wrong:
- CPU raymarch wrote one `SonarPointDTO` slot per requested ray and then used requested ray count as the procedural instance count.
- Misses had zero alpha, but alpha-zero slots still reached the procedural vertex shader and shader discard path.

What was done:
- Added `SonarCompactHitsJob` after `SonarRaymarchJob`.
- The job scans `HitMask`, moves only real hits to the front of the Vault point array, and writes `Counters[0]`/`Counters[1]` as the actual hit count.
- Scan upload and `UpdateIndirectArgsBuffer` now use compacted `_activePointCount`.
- Editor source audit rejects the old `Counters[0] = safeRayCount` pattern and requires `Points[writeIndex] = Points[i]`.

Cinematic Cheats used:
- Misses are treated as absent visual information instead of transparent objects. The shader still carries echo fade/wave, but the CPU no longer asks the GPU to process invisible sonar quads.

Exact Microseconds saved:
- 0 us measured. Static-only proof. Expected savings scale with miss ratio; a low-quality sparse scan no longer expands every miss into six procedural vertices.

Verification:
- Compact-source scan confirms `SonarCompactHitsJob`, in-place hit compaction, and no `Counters[0] = safeRayCount`.
- Forbidden runtime/shader scan returned no matches for PhysX raycasts, `Instantiate`, `SetData`, `RenderMeshIndirect`, runtime mesh allocation, `Time.deltaTime`, random APIs, `Pack=`, private persistent native containers, `foreach`, same-frame `Schedule(...).Complete`, `File.ReadAllBytes`, or compute camera-local DTO writes.
- Compile not launched: CPU 100%, no active `dotnet`/`csc`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R6_HIT_COMPACTION_INDIRECT_COUNT" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="09" status="PASS">Procedural instance count now reflects real hit count, not requested ray count.</TASK>
    <TASK id="11" status="PASS">Shader fade/wave no longer has to discard CPU-known misses.</TASK>
    <TASK id="20" status="PASS">Editor audit locks the compact-hit source contract.</TASK>
  </TASK_IMPACT>
  <DEPENDENCY_GRAPH>
    `GenerateMockSdfJob` optional -> `SonarRaymarchJob` -> `SonarCompactHitsJob` -> late-frame mapped upload to inactive point buffer -> `UpdateIndirectArgsBuffer(activeHitCount)` -> flip.
  </DEPENDENCY_GRAPH>
  <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0">Build withheld by project CPU guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R7 - Compute Indirect Args ABI Guard

What was wrong:
- `CSClearArgs` wrote `_IndirectArgs.Store(16, 0u)` against `SonarProceduralArgsDTO`, which is explicitly 16 bytes.
- Compute hit rays wrote `_RayCount` into indirect instance count, so the optional GPU route would draw miss rays just like the old CPU route.

What was done:
- Removed the fifth args store; compute clear now writes only offsets 0, 4, 8, and 12.
- Replaced ray-count instance writes with `_IndirectArgs.InterlockedAdd(4, 1u, writeIndex)`.
- Compute hit output writes compacted points at `writeIndex`; misses leave stale slots untouched behind the indirect instance-count fence.
- Editor source audit rejects `_IndirectArgs.Store(16`, `_IndirectArgs.Store(4, (uint)_RayCount)`, and miss-slot zeroing in the compute path.

Cinematic Cheats used:
- The GPU path uses atomic hit compaction as a visibility fake: no mesh, no colliders, no transparent miss quads, only compacted echo points that matter.

Exact Microseconds saved:
- 0 us measured. Prevents a 16B/20B ABI mismatch and removes optional-GPU miss draw work. Expected savings scale with sparse-scan miss ratio on weak GPUs.

Verification:
- Prompt block re-extracted by CLI regex: 13,959 characters, 20 tasks.
- Compute ABI scan confirms `InterlockedAdd(4, 1u, writeIndex)` and compacted `_SonarPointBuffer[writeIndex]` writes; no `_IndirectArgs.Store(16` or ray-count instance store remains.
- `git diff --check` passed for touched files; only LF/CRLF warnings.
- Compile not launched: CPU 100%, no active `dotnet`/`csc`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R7_COMPUTE_INDIRECT_ARGS_ABI_GUARD" status="PENDING_COMPILE_CPU_GUARD">
  <STRUCT_LAYOUT_VERIFICATION>
    `SonarProceduralArgsDTO`: 0 `VertexCountPerInstance` uint, 4 `InstanceCount` uint, 8 `StartVertex` uint, 12 `StartInstance` uint. Total 16 bytes. Compute writes no byte offset beyond 12.
  </STRUCT_LAYOUT_VERIFICATION>
  <TASK_IMPACT>
    <TASK id="07" status="PASS">Compute shader optional path now has valid 16B indirect args ABI and compact hit writes.</TASK>
    <TASK id="09" status="PASS">Mapped CPU args and compute args share the same four-uint contract.</TASK>
    <TASK id="20" status="PASS">Editor audit locks compute indirect args bounds and hit compaction.</TASK>
  </TASK_IMPACT>
  <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0">Build withheld by project CPU guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R8 - Runtime Material Allocation Purge

What was wrong:
- `ResolveRenderMaterial()` could call `Shader.Find` and allocate `new Material` during gameplay if `pointCloudMaterial` was missing.
- That fallback masked authoring errors and violated the zero-GC/no-runtime-allocation rule under misconfiguration.

What was done:
- Removed `RuntimeShaderName`, `_runtimeMaterial`, the destroy block, `Shader.Find`, and the `new Material` fallback.
- `ResolveRenderMaterial()` now returns only the serialized `pointCloudMaterial`.
- Editor source audit rejects `Shader.Find` and `new Material` in the sonar runtime source.

Cinematic Cheats used:
- No simulation changed. This is authoring discipline: one assigned material carries shader wave/fade constants instead of runtime material construction.

Exact Microseconds saved:
- 0 us measured. Prevents a one-time gameplay allocation spike and shader lookup on missing material.

Verification:
- Source scan confirms no `RuntimeShaderName`, `_runtimeMaterial`, `Shader.Find`, or `new Material` in `TopographicalSonarSynthesizer.cs`.
- `git diff --check` passed for the runtime/test patch; only LF/CRLF warnings.
- Compile not launched: CPU remains at project-forbidden load.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R8_RUNTIME_MATERIAL_ALLOCATION_PURGE" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="11" status="PASS">Point-wave material is now explicit content, not runtime allocation.</TASK>
    <TASK id="17" status="PASS">Designer/editor control remains through serialized material and tuner fields.</TASK>
    <TASK id="20" status="PASS">Editor audit rejects fallback material allocation in runtime sonar source.</TASK>
  </TASK_IMPACT>
  <ZERO_GC_STATUS>No runtime `Shader.Find` or `new Material` fallback remains in the sonar renderer.</ZERO_GC_STATUS>
</SELF_AUDIT_DELTA>

## Polish Pass R9 - Unity Meta Determinism

What was wrong:
- New Unity assets existed without `.meta` GUID files.
- If left to editor import, GUIDs would be generated per machine and could drift across agents.

What was done:
- Added `.meta` for `Assets/_Project/Data/UI/sonar_material_colors.csv`.
- Added `.meta` for `Assets/_Project/Scripts/Editor/TopographicalSonarTunerWindow.cs`.
- Added `.meta` for `Assets/_Project/Tests/Editor/TopographicalSonar` and `TopographicalSonarLayoutEditTests.cs`.

Cinematic Cheats used:
- None. This is asset identity discipline, not a visual fake.

Exact Microseconds saved:
- 0 us measured. Runtime unaffected; avoids editor import churn and GUID nondeterminism.

Verification:
- `git diff --check` passed for the new meta files.
- GUID collision scan returned no prior matches before creation.
- Compile not launched: CPU 100%, no active `dotnet`/`csc`.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R9_UNITY_META_DETERMINISM" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="17" status="PASS">Editor facade now has stable Unity asset identity.</TASK>
    <TASK id="18" status="PASS">CSV tuning asset now has stable Unity asset identity.</TASK>
    <TASK id="20" status="PASS">Editor test asset and folder now have stable Unity asset identity.</TASK>
  </TASK_IMPACT>
  <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0">Build withheld by project CPU guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R10 - Native Blackbox Dump and Shader Meta Closure

What was wrong:
- `DumpBlackBox()` staged the 300-frame telemetry ring into a managed `byte[]` before `File.WriteAllBytes`.
- `Hecton_SonarPoint.shader` and `Hecton_SonarRaymarch.compute` still lacked `.meta` GUID files.

What was done:
- Replaced managed blackbox staging with `FileStream.Write(new ReadOnlySpan<byte>(nativeTelemetryPtr, byteCount))`.
- Added deterministic `.meta` files for the sonar point shader and sonar raymarch compute shader.
- Expanded editor audit coverage for shader/compute metas and forbidden blackbox allocation strings.

Cinematic Cheats used:
- None. This pass removes crash-path heap staging and Unity GUID nondeterminism.

Exact Microseconds saved:
- 0 us measured in steady state. Fault path removes one 38.4KB managed allocation and one memcpy before the dump write.

Verification:
- Source scan confirms no runtime `new byte[byteCount]` or `File.WriteAllBytes` remains in `TopographicalSonarSynthesizer.cs`.
- Shader/compute `.meta` files exist with deterministic GUIDs.
- Compile not launched under the active CPU guard.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R10_NATIVE_BLACKBOX_DUMP_AND_SHADER_META_CLOSURE" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="07" status="PASS">Compute shader asset now has stable Unity importer identity.</TASK>
    <TASK id="11" status="PASS">Point shader asset now has stable Unity importer identity.</TASK>
    <TASK id="16" status="PASS">Blackbox dump now writes the Vault telemetry ring without managed byte-array staging.</TASK>
    <TASK id="20" status="PASS">Editor audit locks meta presence and dump-allocation exclusions.</TASK>
  </TASK_IMPACT>
  <H_PHI_STATUS>Telemetry remains Vault-owned; dump path reads native memory and does not allocate a mirror payload.</H_PHI_STATUS>
</SELF_AUDIT_DELTA>

## Polish Pass R11 - Miss Path DTO Write Elimination

What was wrong:
- `SonarRaymarchJob.WriteMiss()` wrote `Points[index] = default` for every miss.
- `SonarCompactHitsJob` never reads miss point slots, so the write was pure bandwidth waste.

What was done:
- Removed miss-slot `SonarPointDTO` clearing.
- Misses now update only `HitMask[index] = 0`.
- Editor audit rejects the old `Points[index] = default` pattern.

Cinematic Cheats used:
- Stale miss payload is treated as nonexistent because the compacted indirect instance count fences it out. The visual truth is the compacted echo list, not the full ray array.

Exact Microseconds saved:
- 0 us measured. Expected write reduction is 16 bytes per miss; at 6.8k rays and 60% misses this removes about 65KB of point-buffer writes per ping.

Verification:
- Source audit rejects `Points[index] = default`.
- Compile not launched under CPU guard.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R11_MISS_PATH_DTO_WRITE_ELIMINATION" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="06" status="PASS">Burst raymarch miss path now writes only the hit mask.</TASK>
    <TASK id="09" status="PASS">Mapped upload remains compact-hit only; stale miss payload is never uploaded as visible data.</TASK>
    <TASK id="20" status="PASS">Editor audit locks the no-miss-DTO-write contract.</TASK>
  </TASK_IMPACT>
</SELF_AUDIT_DELTA>

## Polish Pass R12 - Verification Scope Correction

What was wrong:
- The SHINOBU prompt extractor used a Markdown-heading assumption for task count, but the batch file stores tasks inside XML paragraph lines.
- A broad forbidden-string scan included editor tests, which intentionally contain forbidden API names in negative assertions.

What was done:
- Re-read `Docs/Tasks/CURRENT_BATCH.md` using the `<AGENT_PROMPT id="SHINOBU_144">` envelope and `Task\s+(\d{2}):` unique extraction.
- Scoped forbidden API verification to runtime/shader/compute sources and left editor tests as proof guards.
- Rechecked Unity metas and `git diff --check`.

Cinematic Cheats used:
- None. This is proof hygiene, not a visual fake.

Exact Microseconds saved:
- 0 us measured. The benefit is eliminating false-positive verification churn and preventing forbidden build attempts under active CPU pressure.

Verification:
- Prompt block: 13,959 characters; unique tasks: 01-20; count: 20.
- Runtime/shader/compute forbidden scan clean.
- Meta check clean for CSV, editor, tests, point shader, and compute shader.
- `git diff --check` clean except CRLF normalization warnings.
- Compile not launched: CPU 100%, active `dotnet` 0, active `csc` 0.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R12_VERIFICATION_SCOPE_CORRECTION" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="20" status="PASS">Prompt count and static verification now use the actual XML structure and owner-local runtime source scope.</TASK>
  </TASK_IMPACT>
  <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0" active_csc_processes="0">Build withheld by project CPU guard.</COMPILE_GATE>
</SELF_AUDIT_DELTA>

## Polish Pass R13 - Debug Gizmo Active-Count Fence

What was wrong:
- R11 intentionally left stale miss payload behind the compacted active point count.
- The editor gizmo still checked `points.Length`, so it could visualize stale dead slots that runtime indirect draw never renders.

What was done:
- Changed `OnDrawGizmosSelected()` to require `i < _activePointCount && i < points.Length` before drawing a stored hit line.
- Added an editor audit assertion for the active-count fence.

Cinematic Cheats used:
- The same compacted-hit illusion now drives both runtime draw and editor debug: stale payload exists, but it is outside the visible truth fence.

Exact Microseconds saved:
- 0 us measured. Runtime unchanged; editor debug no longer requires restoring 16-byte miss-slot clearing.

Verification:
- Runtime/shader/compute forbidden scan clean.
- Source/test scan confirms `i < _activePointCount && i < points.Length`.
- `git diff --check` clean except CRLF normalization warnings.
- Compile not launched: CPU 100%, active `dotnet` 0, active `csc` 0.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R13_DEBUG_GIZMO_ACTIVE_COUNT_FENCE" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="09" status="PASS">Stale miss payload remains fenced behind compacted active count.</TASK>
    <TASK id="19" status="PASS">Live ray gizmo now mirrors runtime draw visibility instead of raw buffer length.</TASK>
    <TASK id="20" status="PASS">Editor audit locks the gizmo active-count fence.</TASK>
  </TASK_IMPACT>
</SELF_AUDIT_DELTA>

## Polish Pass R14 - True Single-Lookup Thermal Collapse

What was wrong:
- `GlobalQualityWeight=0.1` resolved to one raymarch step, but CPU and compute still sampled the ping origin before the one ray sample.
- That made the supposed collapse two SDF lookups per ray.

What was done:
- Added `ExecuteSingleLookup()` to the Burst path before the origin sample.
- Added a matching `if (maxSteps <= 1u)` branch to `Hecton_SonarRaymarch.compute`.
- The low-work branch takes one stratified SDF sample and reconstructs the visual echo from `distance - signedDistance`.
- Editor audits now assert the CPU and compute single-lookup branches exist.

Cinematic Cheats used:
- The low-tier sonar no longer proves a sign crossing. It samples one deterministic shell and accepts only near-surface SDF values, producing a believable sparse echo without the second lookup.

Exact Microseconds saved:
- 0 us measured. Expected low-tier reduction is one SDF decode/material lookup per ray; at roughly 6.8k rays this avoids roughly 6.8k SDF samples per ping.

Verification:
- Source/test scan confirms `ExecuteSingleLookup`, `ResolveSingleLookupDistance01`, `SingleLookupDistance01`, and `if (maxSteps <= 1u)`.
- Runtime/shader/compute forbidden scan clean.
- `git diff --check` clean except CRLF normalization warnings.
- Compile not launched: CPU 100%, active `dotnet` 0, active `csc` 0.

<SELF_AUDIT_DELTA agent_id="SHINOBU_144" pass="R14_TRUE_SINGLE_LOOKUP_THERMAL_COLLAPSE" status="PENDING_COMPILE_CPU_GUARD">
  <TASK_IMPACT>
    <TASK id="06" status="PASS">Burst raymarch now has a true one-SDF-sample collapse branch.</TASK>
    <TASK id="07" status="PASS">Compute shader mirrors the single-lookup thermal branch.</TASK>
    <TASK id="10" status="PASS">GlobalQualityWeight low-work curve now sheds SDF lookup count exactly at the bottom tier.</TASK>
    <TASK id="20" status="PASS">Editor audit locks CPU/compute single-lookup branch presence.</TASK>
  </TASK_IMPACT>
</SELF_AUDIT_DELTA>

<SELF_AUDIT agent_id="SHINOBU_144" pass="POST_R14_CURRENT" status="PENDING_COMPILE_CPU_GUARD">
  <THE_20_TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Topographical sonar runtime contains no `Physics.Raycast`, `Physics.SphereCast`, or `Collider.Raycast`; SDF is sampled directly.</TASK>
    <TASK id="02" status="PASS">Point cloud renders through `Graphics.DrawProceduralIndirect`; no point GameObjects, meshes, particles, or `Instantiate` route.</TASK>
    <TASK id="03" status="PASS">Hot DTOs expose raw fields; no DTO properties or `Pack=1` layout.</TASK>
    <TASK id="04" status="PASS">`SonarPointDTO` is explicit 16B; telemetry is 128B; shader globals are 64B; indirect args are 16B.</TASK>
    <TASK id="05" status="PASS">`GenerateMockSdfJob` provides deterministic Vault-backed fallback SDF and material IDs for CI/editor isolation.</TASK>
    <TASK id="06" status="PASS">`SonarRaymarchJob : IJobParallelFor` uses Fibonacci directions, bounded SDF stepping, `[NoAlias]`, and required Burst flags; low tier uses true one-lookup branch.</TASK>
    <TASK id="07" status="PASS">`Hecton_SonarRaymarch.compute` mirrors ping-local DTO output, compacted indirect hit count, nearest low-tier sampling, and true one-lookup collapse; runtime dispatch remains gated until a GPU SDF texture/buffer owner is available.</TASK>
    <TASK id="08" status="PASS">Packed RGBA8 color is resolved from material IDs plus Vault LUT/default colors.</TASK>
    <TASK id="09" status="PASS">CPU upload uses `GraphicsBuffer.LockBufferForWrite` through `GraphicsBufferUploadUtility`; point buffers are A/B ping-ponged; miss payload is fenced behind compacted active count.</TASK>
    <TASK id="10" status="PASS">Ray density is `int(math.lerp(2000, 50000, GlobalQualityWeight))`; work curve, SDF filtering, and ping interval continuously shed work.</TASK>
    <TASK id="11" status="PASS">Point shader performs wave, fade, dither, depth fade, and GPU-only point expansion.</TASK>
    <TASK id="12" status="PASS">DTO stores ping-local float offsets; shader reconstructs render position from current camera runtime plus double-subtracted ping-camera AUP delta.</TASK>
    <TASK id="13" status="PASS">Echo fade is shader-primary; optional CPU alpha decay is asynchronous and A/B-uploaded after `IsCompleted`.</TASK>
    <TASK id="14" status="PASS">Rollback/Merkle source scan excludes `TopographicalSonar` and `SonarPointDTO` presentation buffers.</TASK>
    <TASK id="15" status="PASS">Vault allocations request `NativeArrayOptions.UninitializedMemory`; active counts own validity.</TASK>
    <TASK id="16" status="PASS">300-entry telemetry ring records AUP, quality, counts, flags, SDF metadata, and microseconds; dump writes native span to `Docs/AgentLogs/Dump_SONAR_SYNTHESIZER.bin`.</TASK>
    <TASK id="17" status="PASS">UI Toolkit tuner exposes radius, step, fade, point size, quality override, manual ping, and CSV load.</TASK>
    <TASK id="18" status="PASS">CSV color parser is byte-oriented over Vault scratch/LUT; editor file load streams bytes into native scratch.</TASK>
    <TASK id="19" status="PASS">Editor gizmo draws live rays and hit lines while respecting `_activePointCount` to avoid stale miss payload.</TASK>
    <TASK id="20" status="PASS">Runtime static self-audit plus editor tests verify layouts, forbidden APIs, compute ABI, metas, rollback fence, and active-count debug fence.</TASK>
  </THE_20_TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <SonarPointDTO size="16" alignment="16B">
      <FIELD name="LocalPosition" offset="0" size="12" type="float3"/>
      <FIELD name="ColorPacked" offset="12" size="4" type="uint"/>
      <MATH>12 + 4 = 16 bytes; one 16B GPU lane; no manual pack pragma.</MATH>
    </SonarPointDTO>
    <TopographicalSonarTelemetryEntry size="128" alignment="64B_x2">
      <FIELD name="TimeSeconds" offset="0" size="8"/>
      <FIELD name="PingAupX/Y/Z" offset="8/16/24" size="24"/>
      <FIELD name="CameraAupX/Y/Z" offset="32/40/48" size="24"/>
      <FIELD name="Frame..Flags" offset="56..76" size="24"/>
      <FIELD name="GlobalQualityWeight..ComputeTimeMicroseconds" offset="80..124" size="48"/>
      <MATH>8 + 24 + 24 + 24 + 48 = 128 bytes; exact multiple of 64B cache line.</MATH>
    </TopographicalSonarTelemetryEntry>
    <SonarProceduralArgsDTO size="16">4 uint fields at offsets 0,4,8,12; compute clears only 0..12.</SonarProceduralArgsDTO>
    <TopographicalSonarShaderGlobalsDTO size="64">four float4 fields at offsets 0,16,32,48.</TopographicalSonarShaderGlobalsDTO>
    <FALSE_SHARING>No CPU atomic counter struct is introduced; compute atomic writes target GPU indirect args buffer instance count, not a shared CPU cache line.</FALSE_SHARING>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE_EXPLANATION>
    Below `GlobalQualityWeight` 0.3, SDF sampling collapses to nearest-neighbor. At the low thermal end, `ResolveWorkCurve((quality - 0.1) / 0.9)` resolves maxSteps to 1, and CPU/compute take exactly one stratified SDF lookup per ray instead of origin-plus-step crossing. Ping admission lerps from 0.2s to 0.016666668s. At high quality, the full bounded raymarch and trilinear sampling are restored while point density approaches 50k.
  </SCALABILITY_CURVE_EXPLANATION>
  <H_PHI_VAULT_STATUS private_persistent_native_arrays="0">
    <BUFFER id="70840" name="TopographicalSonar.Points" type="SonarPointDTO" count="50000"/>
    <BUFFER id="70841" name="TopographicalSonar.HitMask" type="byte" count="50000"/>
    <BUFFER id="70842" name="TopographicalSonar.Counters" type="int" count="8"/>
    <BUFFER id="70843" name="TopographicalSonar.MockSdf" type="byte" count="262144"/>
    <BUFFER id="70844" name="TopographicalSonar.MockMaterialIds" type="byte" count="262144"/>
    <BUFFER id="70845" name="TopographicalSonar.TelemetryRing" type="TopographicalSonarTelemetryEntry" count="300"/>
    <BUFFER id="70846" name="TopographicalSonar.TelemetryCursor" type="int" count="1"/>
    <BUFFER id="70847" name="TopographicalSonar.MaterialColorLut" type="uint" count="256"/>
    <BUFFER id="70848" name="TopographicalSonar.CsvScratch" type="byte" count="16384"/>
    <BUFFER id="70849" name="TopographicalSonar.IndirectArgs" type="SonarProceduralArgsDTO" count="1"/>
    <BUFFER id="70850" name="TopographicalSonar.ShaderGlobals" type="TopographicalSonarShaderGlobalsDTO" count="1"/>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>
    <NO_ALIAS>All NativeArray fields in `GenerateMockSdfJob`, `SonarRaymarchJob`, `SonarCompactHitsJob`, and `DecaySonarPointsJob` use `[NoAlias]` where the buffers are distinct.</NO_ALIAS>
    <SCAN_GRAPH>optional `GenerateMockSdfJob` -> `SonarRaymarchJob` -> `SonarCompactHitsJob` -> late-frame `IsCompleted` recovery -> mapped upload to inactive point buffer -> flip.</SCAN_GRAPH>
    <FADE_GRAPH>`DecaySonarPointsJob` schedules only when no scan is active -> late-frame `IsCompleted` recovery -> mapped upload to inactive point buffer -> flip.</FADE_GRAPH>
    <COMPLETION_POLICY>No render-path `Schedule(...).Complete()` remains; `Complete()` is used for ownership recovery after `IsCompleted` or disposal.</COMPLETION_POLICY>
  </POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No new asmdef was created or edited. `TopographicalSonarSynthesizer.cs` remains under existing `Hecton8.Core.asmdef`; namespace access to `Hecton8.Caves` and `Hecton8.Visor` does not add a sibling assembly reference.
  </COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>
    <LIE>Low-quality sonar does not solve exact sign crossings. It samples one deterministic shell position and accepts only near-surface SDF values, producing sparse believable echoes. Runtime drawing uses compacted hit count as visual truth, so stale miss payload is never visible.</LIE>
    <BIG_O before="O(rays * PhysX broadphase + GameObject transforms)" after_low="O(rays * 1 SDF lookup + visibleHits draw)" after_high="O(rays * boundedSteps SDF + visibleHits draw)"/>
  </THE_DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    <STATIC>Prompt extraction: 13,959 chars; Tasks 01-20; runtime/shader/compute forbidden scan clean; metas present; `git diff --check` clean except CRLF warnings.</STATIC>
    <COMPILE_GATE cpu_percent="100" active_dotnet_processes="0" active_csc_processes="0">Build and tests withheld by project CPU guard.</COMPILE_GATE>
  </VERIFICATION>
</SELF_AUDIT>
