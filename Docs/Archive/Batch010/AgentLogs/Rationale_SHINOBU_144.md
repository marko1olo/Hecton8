# Rationale_SHINOBU_144

Date: 2026-05-19
Status: PENDING VERIFICATION

## Decision 00 - Scope Boundary

Problem: Dense topographical sonar via PhysX raycasts and GameObject point markers would violate the 0.1 ms suspicion threshold, heap purity, and batching rules.
Solution: Limit authority to Echelon 8 presentation sonar. Use Voxel SDF samples and GPU/Native buffers; avoid gameplay truth mutation and exclude point clouds from rollback hashes.
Rejected Alternatives: Physics.Raycast fan and instantiated spheres were rejected because they route visual scanning through PhysX and the hierarchy.
Scalability potential: Low uses sparse rays and cheap SDF stepping; Middle increases ray density; High adds richer color sampling and smoother fade; Ultra spends saved CPU on denser glowing point clouds.
Hardware Impact: Expected low-end i3/MX350 gain is removal of thousands of PhysX calls and GameObject transforms; exact microseconds remain PENDING VERIFICATION until profiler/GCMonitor logs exist.

## Decision 01 - Procedural Point Cloud Draw

Problem: The first render route used a quad mesh/`RenderMeshIndirect` style inherited from PDA map code, but the assignment requires procedural point cloud output and no hierarchy/mesh authoring dependency.
Solution: Replaced the mesh route with `Graphics.DrawProceduralIndirect`, `SonarProceduralArgsDTO` (16B), `SV_VertexID` quad expansion in `Hecton_SonarPoint.shader`, and a mapped indirect args buffer.
Rejected Alternatives: `RenderMeshIndirect` and runtime `new Mesh()` were rejected because they preserve a mesh dependency for a point-cloud fake and drift from the explicit prompt.
Scalability potential: Low emits sparse procedural quads; Middle increases instances; High/Ultra spend saved CPU on denser point count and shader wave richness.
Hardware Impact: Avoids per-frame mesh/transform work. Measured savings pending; expected low-end gain is removal of CPU mesh instance setup for up to 50k echoes.

## Decision 02 - Ping-Local DTO, AUP-Relative Shader

Problem: Camera-local point positions become stale when the camera moves after the ping and absolute float world positions jitter at large AUP coordinates.
Solution: Store `SonarPointDTO.LocalPosition` as ping-local float offset. Runtime computes `double3 pingAup - cameraAup`, casts the local delta to float4 shader globals, and the shader reconstructs world position from current camera runtime position.
Rejected Alternatives: Absolute `float3` world positions and static camera-local bake were rejected for AUP jitter and post-ping camera movement drift.
Scalability potential: Same 16B DTO works from Low through Ultra; high tiers increase density without changing ABI.
Hardware Impact: No extra buffer stride; one float3 addition in shader. Prevents precision artifacts without CPU-side reprojecting every frame.

## Decision 03 - Continuous Math LOD

Problem: A binary low/high scan path would pop visually and violate the `GlobalQualityWeight` continuum.
Solution: Ray count uses `math.lerp(2000, 50000, quality)`, step length lerps inversely, max raymarch steps use a smooth remapped polynomial curve that reaches one SDF step at quality 0.1, ping admission lerps from 5Hz to 60Hz, and SDF sampling collapses from trilinear to nearest-neighbor below quality 0.3.
Rejected Alternatives: `if (lowTier)` hard switches and fixed 50k rays were rejected because thermal pressure must shed work continuously.
Scalability potential: Low uses sparse rays, nearest SDF, and near-single-step survival scans; Middle restores multi-step shape; High uses trilinear surface hits; Ultra pushes dense shimmer and stronger shader wave band.
Hardware Impact: At quality 0.1 the path schedules roughly 6.8k rays, one SDF step, and a 0.2s minimum ping interval instead of 50k full trilinear rays at 60Hz. Exact profiler microseconds pending.

## Decision 04 - Vault-Owned Human Tuning

Problem: Material color tuning via managed string CSV parsing or ScriptableObjects would create parallel content truth and hot-path allocation risk.
Solution: Added `sonar_material_colors.csv` as a human-readable authoring file and a byte parser that writes numeric IDs or FNV-1a material-name hashes directly into a Vault LUT using native scratch.
Rejected Alternatives: `string.Split`, LINQ, dictionaries, and per-material shader properties were rejected.
Scalability potential: Low can use four defaults; higher quality can load richer palettes without changing shader ABI.
Hardware Impact: Runtime scan pays one LUT read and packed uint write per hit. CSV parsing is editor/slow path only.

## Decision 05 - Compile Gate Discipline

Problem: The project forbids launching `dotnet build` while another dotnet/csc workload is active or CPU is under load.
Solution: Checked CPU and compiler processes before build. CPU was 20%, no `csc.exe`, but seven `dotnet` processes were active, so build was not launched.
Rejected Alternatives: Forcing a build despite active dotnet workloads was rejected because it violates the hardware-protection instruction.
Scalability potential: No runtime effect.
Hardware Impact: Preserves developer iteration hardware; compile proof remains pending.

## Decision 06 - Ping-Pong Point Upload and Non-Blocking Fade

Problem: The optional CPU echo fade path scheduled `DecaySonarPointsJob` and completed it immediately in the render path, and the point cloud had only one `GraphicsBuffer`, creating a CPU-write/GPU-read contention risk.
Solution: Split the sonar point buffer into `_pointBufferA`/`_pointBufferB`, upload completed scans/fades into the non-rendered buffer, then flip the read slot. `DecaySonarPointsJob` now schedules without same-frame completion; completion and upload occur only after `JobHandle.IsCompleted` in the late-frame path, and new pings wait while a fade write is still in flight.
Rejected Alternatives: Keeping a single point buffer was rejected because the GPU upload mandate requires double-buffering. Keeping `Schedule().Complete()` was rejected because it violates the job dependency mandate and can stall the render path.
Scalability potential: Low keeps shader-only fade unless explicitly enabled; Middle can enable async CPU alpha decay at sparse ray counts; High/Ultra can spend saved sync time on denser point clouds and richer shader shimmer without changing DTO stride.
Hardware Impact: Expected low-end i3/MX350 gain is removal of a possible render-path worker wait and reduced GPU/CPU buffer hazard. Exact microseconds remain PENDING VERIFICATION.

## Decision 07 - AUP Hot-Path Removal and Compile-Wall Audit

Problem: The Burst raymarch job still carried `double3` AUP fields only to perform a redundant finiteness check, and the file imports `Hecton8.Caves`/`Hecton8.Visor`, which looks like a compile-wall breach without assembly context.
Solution: Removed `PingAup` and `CameraAup` from `SonarRaymarchJob`; the raymarch kernel now resolves only ping-local `float3` point offsets. Verified that `TopographicalSonarSynthesizer.cs`, `HectonVoxelVolume.cs`, and `SpectrumSystem.cs` all compile under the existing monolithic `Hecton8.Core.asmdef`; no new asmdef was created or edited and no new sibling assembly reference was added.
Rejected Alternatives: Keeping absolute double AUP in the IJobParallelFor loop was rejected because the 100km precision rule says double AUP belongs at the boundary, not in ray-step math. Creating a new domain asmdef was rejected because it would force a larger dependency migration outside SHINOBU_144 ownership.
Scalability potential: Low through Ultra all keep the same ping-local 16B DTO and float-only hot path; higher tiers buy density and shader richness, not larger coordinates.
Hardware Impact: Removes two 24B double3 job fields and redundant double arithmetic from every hit path. Exact microseconds remain PENDING VERIFICATION; expected low-end gain is small but structurally correct.

## Decision 08 - Native CSV File Ingress

Problem: The CSV parser itself was byte-oriented and allocation-free, but the editor facade still used `File.ReadAllBytes`, creating a managed byte array before copying into Vault scratch.
Solution: Replaced `File.ReadAllBytes` with bounded `FileStream.ReadByte` ingestion directly into the Vault-owned CSV scratch `NativeArray<byte>`, then call the existing parser over the filled byte count.
Rejected Alternatives: Keeping managed file bytes was rejected because Task 18 explicitly asks to read bytes into a native scratchpad and bypass managed strings/arrays. `string.Split` and dictionaries remain forbidden.
Scalability potential: Low through Ultra share the same LUT path; richer palettes cost no runtime allocation and only one LUT read per sonar hit.
Hardware Impact: Removes one editor/slow-path managed byte-array allocation per CSV load. Runtime raymarch cost unchanged; exact microseconds remain PENDING VERIFICATION.

## Decision 09 - Compute Shader ABI Parity

Problem: The compute shader fallback wrote `LocalPosition` as `_PingCameraLocal + direction * resolvedDistance`, while the CPU Burst path and render shader now define `SonarPointDTO.LocalPosition` as ping-local. If the GPU path were enabled later, points would be offset twice by ping-camera delta.
Solution: Changed compute output to `direction * resolvedDistance`, added the same `ResolveWorkCurve` low-quality one-step collapse, added nearest-neighbor `Texture3D.Load` below quality 0.3, and used the material color LUT with a default fallback.
Rejected Alternatives: Leaving compute as documentation-only was rejected because Task 07 requires the shader path to be architecturally valid even if runtime dispatch remains gated by profiling/import wiring.
Scalability potential: Low GPU path now mirrors CPU path with one-step/nearest SDF collapse; Middle restores steps; High and Ultra use denser trilinear SDF sampling and the same packed-color ABI.
Hardware Impact: Prevents a future GPU-path rendering offset bug and cuts low-quality compute samples to the same survival budget as Burst. Exact microseconds remain PENDING VERIFICATION.

## Decision 10 - Hit-Count Truth for Indirect Drawing

Problem: The Burst path generated `RayCount` slots, but the procedural indirect args still used the full ray count after scan completion. Miss slots carried zero alpha, yet the GPU still had to execute vertex expansion and shader discard for those invisible points.
Solution: Added `SonarCompactHitsJob` after `SonarRaymarchJob`. It scans the Vault `HitMask`, compacts real `SonarPointDTO` hits to the front of the same Vault point array, and writes counters 0/1 as the real hit count before mapped args upload.
Rejected Alternatives: Drawing alpha-zero misses was rejected because it wastes vertex and fragment bandwidth at exactly the low-quality thermal moment where missed rays dominate. Allocating a second compact output list was rejected because the point array is Vault-owned and the prompt requires flat preallocated buffers.
Scalability potential: Low emits fewer actual instances when one-step scans miss geometry; Middle/High/Ultra still scale to dense point clouds when the SDF produces hits. The draw count now follows visible information, not requested ray budget.
Hardware Impact: Expected low-end i3/MX350 gain is proportional to miss ratio: a 6.8k-ray low-quality scan with 40% hits draws about 2.7k instances instead of 6.8k. Exact profiler microseconds remain PENDING VERIFICATION.

## Decision 11 - Compute Indirect Args ABI Guard

Problem: `CSClearArgs` wrote `_IndirectArgs.Store(16, 0u)` even though `SonarProceduralArgsDTO` is explicitly 16 bytes. The compute hit path also wrote `_RayCount` into instance count, reproducing the miss-draw defect in the optional GPU path.
Solution: Removed the out-of-bounds fifth store and changed compute hits to `_IndirectArgs.InterlockedAdd(4, 1u, writeIndex)`, writing compacted hit DTOs at `writeIndex`. Misses leave stale slots untouched because the indirect instance count fences them out.
Rejected Alternatives: Expanding the indirect args DTO to 20 bytes was rejected because Unity procedural indirect draw consumes four uints for this path and the C# DTO/layout tests already prove a 16B contract. Clearing every miss slot was rejected because stale data behind instance count is not rendered.
Scalability potential: Low GPU path now skips invisible miss instances; Middle/High/Ultra keep atomic compaction while increasing ray density. The same continuous `GlobalQualityWeight` curve controls how many rays can produce compacted hits.
Hardware Impact: Prevents a GPU ABI overwrite and avoids drawing miss rays in the optional compute route. Exact microseconds remain PENDING VERIFICATION; expected mobile gain is reduced indirect instance count under sparse scans.

## Decision 12 - No Gameplay-Time Material Allocation

Problem: The renderer could call `Shader.Find` and allocate `new Material` from the render path if `pointCloudMaterial` was not assigned. That is a cold fallback, but it still violates the zero-GC/no-runtime-allocation mandate under a designer misconfiguration.
Solution: Removed the runtime fallback material and `_runtimeMaterial` ownership. `ResolveRenderMaterial` now returns only the serialized `pointCloudMaterial`; missing material means no draw rather than a gameplay allocation.
Rejected Alternatives: Keeping an editor/runtime fallback was rejected because it hides content wiring errors and can allocate during active play. Creating the material in `Awake` was rejected because boot-time allocation still creates an owned local asset outside the authoring pipeline.
Scalability potential: Low through Ultra all share the same material instance assigned by content; quality scaling remains in shader constants and point count, not material churn.
Hardware Impact: Removes a possible one-time managed/native allocation spike and shader lookup on weak devices. Exact microseconds remain PENDING VERIFICATION; expected benefit is stutter prevention rather than steady-state cost.

## Decision 13 - Unity Asset GUID Determinism

Problem: New CSV, editor, and test assets existed without `.meta` files. Unity would regenerate GUIDs per workstation, creating nondeterministic asset identity and possible broken references.
Solution: Added deterministic `.meta` files for `sonar_material_colors.csv`, `TopographicalSonarTunerWindow.cs`, the `TopographicalSonar` edit-test folder, and `TopographicalSonarLayoutEditTests.cs`.
Rejected Alternatives: Letting Unity autogenerate metas was rejected because it defers identity creation to a local editor import and can drift across machines/agents.
Scalability potential: No runtime quality effect. This protects content determinism across cheap CI boxes and high-end editor machines.
Hardware Impact: No frame-time effect; prevents editor import churn and asset GUID instability. Exact microseconds not applicable.

## Decision 14 - Native Blackbox Dump and Shader Meta Closure

Problem: The sonar shader and compute assets still had no `.meta` files, and the telemetry crash dump copied the Vault ring into a managed `byte[]` before `File.WriteAllBytes`.
Solution: Added deterministic shader/compute `.meta` files and changed `DumpBlackBox` to write a `ReadOnlySpan<byte>` directly over the `NativeArray<TopographicalSonarTelemetryEntry>` pointer via `FileStream.Write`.
Rejected Alternatives: Managed `byte[]` staging was rejected because a crash-path dump must not create a second 38.4KB heap payload. Unity-generated shader/compute metas were rejected because GUID creation must not depend on local editor import order.
Scalability potential: Low through Ultra share the same telemetry ABI and asset GUIDs; higher tiers increase ray density without changing the dump format or shader identity.
Hardware Impact: Removes one managed allocation and one memory copy on blackbox dump. Steady-state frame time unchanged; fault-path dump writes the same 38.4KB payload from native memory.

## Decision 15 - Miss Path DTO Write Elimination

Problem: The CPU raymarch miss path zeroed `Points[index]` for every miss, even though `SonarCompactHitsJob` ignores miss slots and copies only entries whose `HitMask` is one.
Solution: Removed the miss-slot `SonarPointDTO` write. Misses now write only `HitMask[index] = 0`, leaving stale point payload behind the compacted hit-count fence.
Rejected Alternatives: Clearing invisible point payloads was rejected because it adds bandwidth exactly when low-quality one-step scans miss most rays. The draw path already fences stale payload behind the compacted indirect instance count.
Scalability potential: Low quality benefits most because sparse one-step scans have high miss ratios; Middle/High/Ultra retain the same compaction ABI while spending writes only on actual hits.
Hardware Impact: Avoids one 16-byte DTO write per miss. A 6.8k-ray low-quality scan with 60% misses avoids roughly 65KB of point-buffer writes before compaction; exact profiler microseconds remain pending.

## Decision 16 - Static Verification Scope Correction

Problem: The SHINOBU task block stores `Task 01:` through `Task 20:` inside XML paragraph lines, so a Markdown-heading regex returns zero tasks. A naive repository-wide forbidden-string scan also hits the editor tests because those tests intentionally contain forbidden API names inside negative assertions.
Solution: Re-read the XML block with `Task\s+(\d{2}):` unique extraction and scope forbidden API scans to runtime/shader/compute files. Keep the editor test strings as proof locks instead of treating them as runtime violations.
Rejected Alternatives: Counting Markdown headings was rejected because the source prompt is not Markdown-heading structured. Scanning tests as runtime source was rejected because it creates false positives and hides real runtime regressions in noise.
Scalability potential: No runtime quality effect. Verification now scales as a precise owner-local proof: source files prove behavior, tests prove guardrails.
Hardware Impact: No frame-time effect. Prevents unnecessary build attempts and rework under CPU guard; exact microseconds not applicable.

## Decision 17 - Debug Gizmo Active-Count Fence

Problem: After miss-slot DTO clearing was removed, stale payload behind the compacted active point count is valid dead memory. Runtime indirect draw fences it out, but the editor gizmo still checked only `points.Length`, so debug visualization could display stale echoes during shrinking scans.
Solution: Gate hit-line gizmo reads with `i < _activePointCount && i < points.Length`, matching the indirect instance-count contract. Added an editor source assertion to lock the fence.
Rejected Alternatives: Re-zeroing miss slots was rejected because it reintroduces the bandwidth waste fixed in R11. Leaving the gizmo stale was rejected because Task 19 is a verification tool and must not contradict runtime draw truth.
Scalability potential: Low quality benefits most because one-step scans produce high miss ratios and stale slots are expected. Middle/High/Ultra keep the same compacted debug view.
Hardware Impact: No runtime frame-time effect; editor-only. Prevents false visual debugging during sparse scans without restoring 16-byte miss writes.

## Decision 18 - True Single-Lookup Thermal Collapse

Problem: The prior low-quality path reduced `MaxSteps` to one, but still sampled the SDF at the ping origin before the loop and once along the ray. That is two SDF lookups at `GlobalQualityWeight=0.1`, not the mandated single lookup.
Solution: Added a low-work branch before the origin sample. When `maxSteps <= 1`, CPU Burst executes `ExecuteSingleLookup()` and compute executes `if (maxSteps <= 1u)`: one deterministic stratified distance sample, one SDF fetch, and a near-surface visual reconstruction from `distance - signedDistance`.
Rejected Alternatives: Keeping the origin sign sample was rejected because it violates the thermal-collapse contract. Clearing the ray entirely at low quality was rejected because the sonar still needs a visible survival-mode echo.
Scalability potential: Low uses one nearest-neighbor SDF fetch per ray and accepts only samples near the surface. Middle restores signed-crossing raymarch. High/Ultra use the full bounded march plus trilinear SDF sampling and denser point clouds.
Hardware Impact: At quality 0.1, per-ray SDF sampling drops from two lookups to one in both CPU and compute paths. Exact profiler microseconds remain pending; expected low-end benefit is one avoided SDF decode/material lookup per ray.
