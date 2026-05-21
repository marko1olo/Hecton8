# SHINOBU_252 Foundation Snapping Calculator Log

## 2026-05-21

What was wrong:
- Base support rendering had no SHINOBU_252-owned DOD route; physical/prefab pylons would create Transform, renderer, and potential PhysX pressure.
- Pylon length needed to be derived mathematically from voxel SDF, not from Unity physics or main-thread scene probes.
- AUP precision, rollback exclusion, GPU upload, black-box telemetry, and editor proof artifacts were missing for this route.

What was done:
- Added `FoundationSnappingCalculatorData.cs` with explicit unmanaged DTOs, DataVault buffer IDs, tuning, zero-alloc CSV profile parser, telemetry ring, and dump route.
- Added `FoundationSnappingCalculatorJobs.cs` with `GenerateMockSeafloorSDFJob`, `BuildFoundationModulesFromSocketModulesJob`, `CalculateFoundationPylonsJob`, counter reduction, and indirect-args build.
- Added `FoundationPylonGpuBatch.cs` to build pylon matrices from Construction socket modules, subtract camera AUP, double-buffer GPU uploads through `GraphicsBuffer.LockBufferForWrite`, draw one procedural indirect batch, emit `BaseStructuralWarningSignal`, and draw editor gizmo rays.
- Added `Hecton_FoundationPylon.shader` for procedural cylindrical supports with bottom flare driven by SDF normal.
- Added `FoundationSnappingCalculatorEditor.cs` with `Base Grounding Tuner`, ARM64 layout validator, and `Foundation_Physics_Inquisition`.
- Added `FoundationSnappingCalculatorEditTests.cs` for layout, quality ramp, mock SDF raymarch, PhysX/prefab token guard, and rollback/Merkle exclusion.
- Added route card `Docs/ARCHITECTURE/FOUNDATION_SNAPPING_CALCULATOR_SHINOBU_252.md`.
- Updated `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` with a `shinobu_252_foundation_snapping` static report entry.

Cinematic Cheats used:
- SDF downward raymarch replaces physical support collision.
- Shader bottom flare fakes terrain embedding instead of terrain deformation.
- Zero-scale hidden matrices replace support deletion/spawn churn.
- Quality-driven ray count/radius/flare/step budget buys visual overkill only when device headroom exists.

Exact Microseconds saved:
- Per pylon: estimated 0.1-0.3 us saved by avoiding PhysX/main-thread ray probes.
- Per visible base chunk: estimated 20-80 us saved by avoiding pylon GameObjects, Transform traversal, and per-object renderer submissions.
- GPU upload: one memcpy per matrix/surface/args buffer instead of per-instance managed upload; exact profiler value pending Unity runtime import.
- Telemetry: fixed 19.2 KB black-box ring; no variable managed logging in hot path.

Verification:
- Static forbidden scan over SHINOBU_252-owned foundation files: `Physics.Raycast=0`, `RaycastCommand=0`, `Instantiate(=0`, `List<Transform>=0`.
- `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` parses as JSON after report insertion.
- Build attempt 1: `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo /m:1` failed before compile with missing `Temp/obj/Assembly-CSharp/project.assets.json`.
- Build attempt 2: after CPU/dotnet gate cleared, `dotnet build .\Assembly-CSharp.csproj --nologo /m:1` restored projects and failed on external missing source files:
  - `Assets/_Project/Scripts/World/Contracts/GroundRadarContracts.cs` in `Hecton8.World.Contracts.csproj`
  - `Assets/_Project/Scripts/IBuildPlacementRule.cs` in `Hecton8.Core.csproj`
- No SHINOBU_252 compile error was reported before the external project-file wall.

## 2026-05-21 Ultra Polish Pass

What was wrong:
- BufferIDs `70960..70974` were local numeric casts without an authoritative ledger range.
- `FoundationPylonFrameCounters` was 32 bytes, so adjacent per-module counter writes could share one 64-byte cache line under `IJobParallelFor`.
- `TryReadEditorState` initialized Vault handles, violating read-accessor purity.
- The late-frame SDF route used `TryGetBuffer` for `BufferID.VoxelSdfTexture3D`; that path can mutate external-view accounting and is not the correct hot route.
- The first low-quality proxy patch had an explicit named boolean split; the route now blends proxy and raymarch output through continuous `sdfInterpolationWeight`.

What was done:
- Registered `70960..70974` in `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` and added a SHINOBU_252 payload boundary with offsets, endian route, rollback/save exclusion, and dump route.
- Expanded `Docs/ARCHITECTURE/FOUNDATION_SNAPPING_CALCULATOR_SHINOBU_252.md` and `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` with BufferID, layout, mock SDF, evidence, runtime-proof, and quality-route fields.
- Changed `FoundationPylonFrameCounters` to explicit 64-byte layout with padding `32..63`.
- Changed editor read state to `TryReadEditorState(IDataVault vault, ...)` so it only resolves existing handles; editor code supplies the cold Vault reference.
- Cached the external voxel SDF `VaultGenerationHandle<byte>` during cold setup and uses hot `TryResolveHandle` only.
- Kept `FloatMode.Fast` intentionally: pylon matrices, counters, hashes, and warnings are presentation/proof lanes and are excluded from rollback/save authority.

Cinematic Cheats used:
- Low-quality mode collapses the terrain solve to one nearest-neighbor SDF proxy lookup per ray and blends toward full raymarch as `GlobalQualityWeight` rises.
- `FoundationPylonSurfaceDTO` keeps the Dear Lie normal/flare route so the shader handles terrain embedding instead of CPU mesh deformation.
- Overextended supports remain zero-scale matrix rows; no GameObject creation/deletion is used.

Static Microsecond Estimates - profiler proof absent:
- Avoided PhysX/main-thread support ray probe: estimated 0.1-0.3 us per ray.
- Avoided pylon GameObject hierarchy/renderer submissions: estimated 20-80 us per visible base chunk.
- Avoided per-module counter false sharing: no measured value yet; expected to remove cache-line ping-pong risk under parallel module writes.
- Low-quality SDF path: removes trilinear 8-tap sampling and 6-sample gradient normal path when `sdfInterpolationWeight` is zero; exact Burst timings pending.

Verification:
- Static forbidden scan over SHINOBU_252-owned foundation files: no `Physics.Raycast`, `RaycastCommand`, `Instantiate(`, or `List<Transform>` hits after test literals were split.
- Static polish scan: no owned `TryGetBuffer(`, `FloatMode.Deterministic`, `qualityProxy`, private persistent `NativeArray`, `NativeList`, or `NativeHashMap` hits.
- `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` parses via `ConvertFrom-Json`.
- `git diff --check` reports no whitespace errors; only CRLF warnings for shared docs/report files.
- Build not relaunched in this pass: CPU sampled at 43.1 percent but one `dotnet/csc` process was active, so the user build-gate forbids another build.

<SELF_AUDIT agent="SHINOBU_252" domain="FOUNDATION_SNAPPING_CALCULATOR" task_count="20" evidence_class="STATIC_SOURCE_STATIC_DOC">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Owned foundation support route has no PhysX raycast authority.</TASK>
    <TASK id="02" status="PASS">Pylons render through procedural indirect GPU buffers, not support GameObjects.</TASK>
    <TASK id="03" status="PASS">Hot DTOs use explicit raw unmanaged fields; no DTO properties.</TASK>
    <TASK id="04" status="PASS">`PylonMatrixDTO=64`, `LocalToWorld@0`; validator checks size/offset.</TASK>
    <TASK id="05" status="PASS">`GenerateMockSeafloorSDFJob` fills deterministic fallback SDF rows.</TASK>
    <TASK id="06" status="PASS">`CalculateFoundationPylonsJob` raymarches/bypasses by quality against SDF arrays in Burst.</TASK>
    <TASK id="07" status="PASS">Matrices scale Y to resolved pylon length and place center at midpoint.</TASK>
    <TASK id="08" status="PASS">Dear Lie normal/flare row drives shader bottom embedding.</TASK>
    <TASK id="09" status="PASS">Double-buffered `GraphicsBuffer.LockBufferForWrite` upload route exists.</TASK>
    <TASK id="10" status="PASS">`GlobalQualityWeight` drives ray count, radius, flare, steps, and SDF interpolation.</TASK>
    <TASK id="11" status="PASS">Over-extension writes zero-scale matrix and emits unmanaged warning signal.</TASK>
    <TASK id="12" status="PASS">Job subtracts camera AUP before float matrix emission.</TASK>
    <TASK id="13" status="PASS">Pylon matrices/surfaces are rollback/save excluded presentation rows.</TASK>
    <TASK id="14" status="PASS">Vault rows use `UninitializedMemory`; counters are explicit writes and 64-byte isolated.</TASK>
    <TASK id="15" status="PASS">`FoundationTelemetryEntry[300]` black-box ring and dump route exist.</TASK>
    <TASK id="16" status="PASS">`Base Grounding Tuner` UI Toolkit facade exists.</TASK>
    <TASK id="17" status="PASS">CSV profile parser uses `ReadOnlySpan<byte>` into Vault rows.</TASK>
    <TASK id="18" status="PASS">Editor gizmo draws debug rays from `FoundationDebugRayDTO`.</TASK>
    <TASK id="19" status="PASS">`Foundation_Physics_Inquisition` static scanner/report route exists.</TASK>
    <TASK id="20" status="PASS">Layout/tests/docs/self-audit route exists; runtime proof remains pending.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DTO name="PylonMatrixDTO" size="64" alignment="64">
      <FIELD name="LocalToWorld" offset="0" size="64" />
      <PADDING bytes="0" />
    </DTO>
    <DTO name="FoundationPylonSurfaceDTO" size="64" alignment="64">
      <FIELD name="SurfaceNormalFlare" offset="0" size="16" />
      <FIELD name="AxisRadius" offset="16" size="16" />
      <FIELD name="HitLocalLength" offset="32" size="16" />
      <FIELD name="Flags" offset="48" size="4" />
      <FIELD name="ModuleHash" offset="52" size="4" />
      <FIELD name="RayIndex" offset="56" size="4" />
      <FIELD name="ResultHash" offset="60" size="4" />
      <PADDING bytes="0" />
    </DTO>
    <DTO name="FoundationPylonFrameCounters" size="64" alignment="64" false_sharing_guard="true">
      <FIELD name="ActivePylonCount" offset="0" size="4" />
      <FIELD name="SlotCount" offset="4" size="4" />
      <FIELD name="RaysCast" offset="8" size="4" />
      <FIELD name="HitCount" offset="12" size="4" />
      <FIELD name="CulledCount" offset="16" size="4" />
      <FIELD name="MaxResolvedLength" offset="20" size="4" />
      <FIELD name="ResultHash" offset="24" size="4" />
      <FIELD name="Flags" offset="28" size="4" />
      <PADDING offset="32" bytes="32" reason="one cache line per parallel counter row" />
    </DTO>
    <DTO name="FoundationTelemetryEntry" size="64" alignment="64" ring_capacity="300" />
    <DTO name="BaseStructuralWarningSignal" size="64" alignment="64" />
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    `ResolveSdfInterpolationWeight = smoothstep(0.3, 0.55, GlobalQualityWeight)`. At and below 0.3 the interpolation weight is zero: one SDF read at ray start, nearest-neighbor sample, proxy length, and up-normal shader embed. Between 0.3 and 0.55, proxy and raymarch hit length blend. High/Ultra use full trilinear SDF sampling, longer bounded march budget, SDF-gradient normal, wider radius, and stronger shader flare. Quality never changes DTO layout, save identity, authority owner, or rollback boundary.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS private_native_arrays="0">
    <BUFFER id="70960" name="FoundationModuleAupDTO" />
    <BUFFER id="70961" name="PylonMatrixDTO" />
    <BUFFER id="70962" name="FoundationPylonSurfaceDTO" />
    <BUFFER id="70963" name="FoundationPylonFrameCounters_PerModule" />
    <BUFFER id="70964" name="FoundationPylonFrameCounters_Frame" />
    <BUFFER id="70965" name="FoundationTelemetryEntry_Ring" />
    <BUFFER id="70966" name="TelemetryCursor" />
    <BUFFER id="70967" name="FoundationTuningDTO" />
    <BUFFER id="70968" name="MockSdfDistance" />
    <BUFFER id="70969" name="FoundationSdfConfigDTO" />
    <BUFFER id="70970" name="FoundationRayOriginDTO" />
    <BUFFER id="70971" name="FoundationProfileRangeDTO" />
    <BUFFER id="70972" name="CsvScratchBytes" />
    <BUFFER id="70973" name="FoundationDebugRayDTO" />
    <BUFFER id="70974" name="FoundationPylonIndirectArgsDTO" />
    GraphicsBuffer fields are GPU resources, not DataVault/native gameplay truth.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    All job NativeArray fields that do not alias use `[NoAlias]`. Schedule graph: optional `BuildFoundationModulesFromSocketModulesJob` and optional `GenerateMockSeafloorSDFJob` feed `CalculateFoundationPylonsJob`; then `ReduceFoundationPylonCountersJob`; then `CompactFoundationPylonDrawListJob`; then `BuildFoundationPylonIndirectArgsJob`; final handle is stored and completed only through `DispatcherJobFence.TryFinalizeCompleted`. Forced complete exists only for teardown.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No `.asmdef` reference was added by SHINOBU_252. Owned runtime files import Core/Core.Memory/Core.Contracts.Signal routes and existing AUP/floating-origin surface; no new sibling runtime assembly edge was created. Build proof remains blocked by external missing source files and current dotnet/csc gate.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: per-support PhysX raycasts plus pylon GameObjects would be O(P) scene queries, Transform churn, renderer submissions, and potential mesh/collider work. After: O(M*R*S) bounded Burst SDF math plus O(slots) active-row compaction over flat arrays plus one procedural indirect draw, with low quality collapsing S to one SDF read and shader flare faking terrain embed. No support GameObject, collider, or terrain deformation is required; inactive slots are compacted out before upload/draw.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

## 2026-05-21 Active Draw Compaction / Shader ABI Polish Pass

What was wrong:
- Inactive supports were zero-scale rows, but the previous upload/indirect route could still ship fixed slot capacity to GPU buffers and rely on shader discard.
- Material colors were written every draw even when unchanged.
- The procedural shader carried integer flags without explicit `nointerpolation`, kept material colors outside `UnityPerMaterial`, and declared a fallback shader route.

What was done:
- Added `CompactFoundationPylonDrawListJob` after counter reduction. It packs only active `PylonMatrixDTO` and `FoundationPylonSurfaceDTO` rows to the front of the Vault buffers and rewrites `FrameCounters[0].SlotCount` to active count.
- Changed `FoundationPylonGpuBatch` so upload count and indirect instance count use the compacted active support count.
- Added cold material warmup through `Material.SetPass(0)` during setup and changed material color updates to change-only state writes.
- Updated `Hecton_FoundationPylon.shader` with `UnityPerMaterial` CBuffer, `nointerpolation uint flags`, and `Fallback Off`.
- Updated edit test scheduling so mock raymarch proof includes compaction before indirect args.
- Changed `Foundation_Physics_Inquisition` menu output to `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_252.json` so the editor proof button cannot overwrite the shared construction report.

Cinematic Cheats used:
- Culled/quality-suppressed supports remain proof rows on CPU only; they are compacted out before GPU upload and never become procedural instances.
- The shader still fakes terrain embedding through bottom flare from SDF normal; no CPU mesh cutting or terrain deformation path was introduced.

Static Microsecond Estimates - profiler proof absent:
- Per inactive/cull slot: avoids one 64-byte matrix upload row, one 64-byte surface upload row, and one procedural cylinder instance.
- Per render: avoids two unconditional material color writes when colors are unchanged.
- Added CPU cost: one contiguous Burst compaction pass over possible slots after raymarch; it is cache-linear and replaces downstream GPU/driver work.

Verification:
- Static scan over owned runtime/test/shader files found no `TryGetBuffer`, `FloatMode.Deterministic`, lower-case `qualityProxy`, private persistent `NativeArray/NativeList/NativeHashMap`, PhysX raycast, `RaycastCommand`, `Instantiate(`, or `List<Transform>` hits.
- Report JSON and route card were updated to state active draw-list compaction.
- Build attempt 3: `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo /m:1` failed before C# compile with `NETSDK1004` missing `Temp/obj/Assembly-CSharp/project.assets.json`. Follow-up restore/build was not launched because CPU sampled at 55 percent, above the user gate.

## 2026-05-21 Subagent Static Audit Integration Pass

What was wrong:
- Camera-relative pylon matrices were being interpreted as Unity world positions by shader and draw bounds.
- Runtime material creation was editor-guarded, leaving player builds dependent on serialized materials only.
- Scheduled jobs held DataVault `NativeArray` views without explicit Vault locks.
- Editor status reads used the resolving accessor instead of the pure read accessor.
- Late-frame camera AUP capture read the floating-origin static route directly.
- Editor tooling allocations were not explicitly fenced as editor-only.

What was done:
- Added scheduled camera world offset capture. Bounds add that offset to matrix centers, and `Hecton_FoundationPylon.shader` adds `_H8FoundationPylonCameraWorldOffset` before `TransformWorldToHClip`.
- Moved only AssetDatabase shader lookup behind `UNITY_EDITOR`; player runtime can create a material when `pylonShader` is serialized.
- Added `IOriginShiftListener`; origin AUP is cached cold and updated on committed shifts. Pending batches are discarded on origin shift.
- Added owner-tagged DataVault `TryLockBuffer/TryUnlockBuffer` coverage for foundation buffers, consumed socket inputs, and optional encoded voxel SDF during scheduled jobs.
- Added `TryReadVaultViews` over `TryReadHandle` and routed editor/gizmo reads through it.
- Renamed the old approximate low-quality flag from quality-proxy terminology to `ApproximateSdf`.

Cinematic Cheats used:
- The matrix stays camera-relative for AUP precision; the shader reconstructs world space with one constant offset instead of forcing CPU absolute-float matrices.
- Origin shifts discard stale pending visual batches instead of trying to rebase an in-flight presentation buffer.

Static Microsecond Estimates - profiler proof absent:
- One material vector update per uploaded batch when the camera offset changes.
- Fixed lock/unlock work around scheduled jobs; cost buys Vault relocation safety.
- No new GameObjects, colliders, PhysX queries, or per-pylon managed allocations.

Verification:
- Subagent audit finding 1 fixed by camera-offset shader/bounds reconstruction.
- Finding 2 fixed by player material creation path.
- Finding 3 fixed by explicit Vault job locks.
- Finding 4 fixed by `TryReadHandle` editor read route.
- Finding 5 fixed by cached origin snapshot and `IOriginShiftListener`.
- Finding 6 documented as editor-only allocation; runtime zero-GC claim is unchanged.

## 2026-05-21 Post-Polish Build Wall Recheck

What was wrong:
- The previous report still named the earlier build wall, not the post-Loop-9 guarded restore/build attempt.

What was done:
- Re-ran the build only after the user gate was open (`CPU=12%`, `dotnet/csc=0`).
- Recorded the result in `Status_SHINOBU_252.md`, `Rationale_SHINOBU_252.md`, and the SHINOBU_252 sidecar report.

Cinematic Cheats used:
- None. This was evidence hygiene only; no runtime route changed.

Static Microsecond Estimates - profiler proof absent:
- 0 us runtime. The work prevents duplicate compiler archaeology after context compression.

Verification:
- `dotnet build .\Assembly-CSharp.csproj --nologo /m:1` restored/compiled until the same external missing source wall: `Assets/_Project/Scripts/World/Contracts/GroundRadarContracts.cs` and `Assets/_Project/Scripts/IBuildPlacementRule.cs`.
- No SHINOBU_252 source compile diagnostic was emitted before the external wall.

## 2026-05-21 Cold Allocation Evidence Hygiene

What was wrong:
- `FoundationPylonGpuBatch` cold allocation comments were descriptive but did not follow the exact `AGENTS.md` canonical marker format.

What was done:
- Updated the pending Vault lock array comment, GraphicsBuffer allocation comment, runtime material fallback comment, and indirect args GraphicsBuffer comment to `COLD ALLOC: Type[capacity] - reason - owner`.

Cinematic Cheats used:
- None. Evidence-only hygiene.

Static Microsecond Estimates - profiler proof absent:
- 0 us runtime. This prevents static-audit churn, not frame cost.

Verification:
- Source comments now expose explicit allocation type/capacity/owner for the SHINOBU-owned cold allocation sites.

## 2026-05-21 Camera Lookup Hardening

What was wrong:
- `FoundationPylonGpuBatch.EnsureCameraCold()` still used `Camera.main` as a cold fallback. It was not in the scheduled Burst/GPU hot path, but the token is a known tag-search regression vector.

What was done:
- Added `IGlobalRegistryHotSwapListener` to `FoundationPylonGpuBatch`.
- Cached `GlobalRegistry.Player` during cold setup and rebound `targetCamera` from `IPlayerRuntimeContext.PlayerCamera` when the Player registry slot is replaced.
- Removed the `Camera.main` fallback entirely.

Cinematic Cheats used:
- None. Presentation authority hygiene only.

Static Microsecond Estimates - profiler proof absent:
- Avoids hidden `Camera.main` tag lookup on uncached setup/rebind frames. No pylon math or draw cost changed.

Verification:
- Static scan over SHINOBU-owned runtime/test/shader files found no `Camera.main`, `FindObject`, `GameObject.Find`, or `Resources.Load` hits.
- Build not relaunched after this patch because latest CPU sampled at 28 percent but `dotnet/csc` count was 1, and the user gate forbids concurrent compiler work.

## 2026-05-21 Subagent Numeric ID / Low-Tier SDF Hardening

What was wrong:
- Foundation Vault buffers used local numeric `(BufferID)709xx` casts after the ledger range was reserved.
- The low-quality SDF path skipped the raymarch loop but still paid six extra SDF reads for gradient normal extraction.
- Telemetry cursor memory came from `UninitializedMemory` and needed explicit generation seeding.
- DataVault views were resolved before the lock window and could stale if Vault storage moved.
- SDF flat-index math used int-scale assumptions for large dimensions.
- Repeated CSV rows for one module profile could overlap the next profile's ray slots.
- Telemetry dump staged native rows through a managed byte array.
- Telemetry/cursor rows were not held in the scheduled finalize lock window.

What was done:
- Added named `BufferID.FoundationSnapping*` entries in `H8Memory.cs` for `70960..70974` and updated SHINOBU constants to those symbols.
- Changed `CalculateFoundationPylonsJob` so `GlobalQualityWeight <= 0.3` uses one nearest-neighbor SDF proxy read per ray and an up-normal; high-quality gradient normal remains available above the interpolation threshold.
- Seeded the telemetry cursor once per handle generation and changed cursor index wrap to unsigned modulo.
- Re-resolved foundation/socket/SDF Vault views after `TryLockBuffer` succeeds, then scheduled jobs.
- Sanitized tuning/SDF config before scheduling and guarded SDF sample counts/indexes with 64-bit bounds.
- Stored CSV profile rays in fixed `profileIndex * MaxRaysPerModule + rayIndex` slots.
- Wrote telemetry dump bytes from a native `ReadOnlySpan<byte>` directly to `FileStream`.
- Added telemetry and telemetry cursor buffers to the Vault lock list.

Cinematic Cheats used:
- Low quality now commits to the cheap fake: one mathematical distance proxy and up-normal shader embed instead of trying to infer exact local terrain normal.
- Shader bottom flare still sells terrain contact without terrain deformation, colliders, or support GameObjects.

Static Microsecond Estimates - profiler proof absent:
- Low tier saves six SDF sample calls per hit by skipping gradient normal extraction, plus all skipped march iterations.
- Named BufferIDs and post-lock re-resolve are runtime-neutral except for fixed lock/re-resolve cost; they prevent integration drift and aliasing faults.
- Native telemetry dump avoids one telemetry-sized managed allocation on fault capture.

Verification:
- Static scan over SHINOBU-owned runtime/test/shader files found no `File.ReadAllBytes`, numeric `(BufferID)709xx` casts, `Camera.main`, `FindObject`, `GameObject.Find`, `Resources.Load`, `TryGetBuffer`, `FloatMode.Deterministic`, quality-proxy token, PhysX raycast, `RaycastCommand`, `Instantiate(`, or `List<Transform>` hits.
- Both construction report JSON files parse with `ConvertFrom-Json`.
- `git diff --check` reports no whitespace errors; only CRLF normalization warnings on shared docs/report files.
- Generated `.csproj` audit found `EnableDefaultItems=false` and no compile includes for the new SHINOBU_252 source files; Unity import/project regeneration is required before local dotnet sees them. Generated project files were not hand-edited.
- Renamed the mutating module-input fallback helper from `TryResolveModuleInputs` to `TryPrepareModuleInputs`; no runtime helper with the mutating `TryResolveModuleInputs` name remains in SHINOBU-owned source.
- Build was not relaunched because the fresh gate sample was `CPU=91.7%` and `dotnet/csc=0`; the user rule forbids build while CPU is above 50%.

## 2026-05-21 Subagent Audit Patch - Profile Fence / Black Box / Shader Inclusion

What was wrong:
- CSV profile reloads wrote DataVault ray-origin/profile rows without an explicit race fence against scheduled pylon jobs.
- Foundation hot reads still used resolve-style view helpers in places where pure reads were enough.
- Non-finite pylon math set flags but did not force a black-box telemetry dump, and one invalid-length branch did not OR `NonFinite`.
- Ray-count scaling could appear as full-thickness support topology thresholds.
- The pylon shader used per-vertex trigonometry, fragment `pow`, and unguarded `normalize` paths.
- Player-build shader inclusion was not proven; the shader GUID appeared only in its `.meta`.

What was done:
- Added SHINOBU profile read/write fences. Scheduled jobs hold the read fence until `DispatcherJobFence.TryFinalizeCompleted`; CSV reload refuses while fenced and locks `RayOrigin/ProfileRange/CsvScratch`.
- Removed SHINOBU-owned `TryResolveVaultViews`; foundation hot-path access now uses `TryReadVaultViews`.
- Added a narrow pure `ShinobuSocketConstructionRuntime.TryReadVaultViews` bridge so foundation can consume socket module rows without resolve-side fault telemetry mutation.
- Added black-box `DumpTelemetry` on `NonFinite` counters and marked non-finite resolved lengths explicitly.
- Added `ResolveRayBudget` and fractional ray radius/flare scaling so transitional supports fade in instead of appearing at full thickness.
- Replaced shader `sin/cos` with a 16-entry ring LUT, replaced `pow(x,2)` with `x*x`, and added `SafeNormalize`.
- Added the shader GUID `0e3d6c95b94344c7b864f17da3f25205` to `ProjectSettings/GraphicsSettings.asset` `m_AlwaysIncludedShaders`.

Cinematic Cheats used:
- Extra support rays fade by matrix radius and bottom flare; no physical support objects, no terrain deformation, no PhysX probes.
- Shader flare still fakes seabed embed while CPU math only finds an SDF hit length.

Static Microsecond Estimates - profiler proof absent:
- Avoids undefined CSV/job row tearing.
- MX350/Quest-class GPU avoids 96 trig evaluations per full pylon instance and one fragment `pow`; exact GPU timing still requires profiler proof.
- Fault path writes a fixed 19.2 KB native telemetry ring; normal path adds one flag test.

Verification:
- Static scan over SHINOBU-owned foundation files found no `TryResolveVaultViews`, `File.ReadAllBytes`, numeric `(BufferID)709xx` casts, `Camera.main`, scene search tokens, `TryGetBuffer`, `FloatMode.Deterministic`, quality-proxy token, PhysX raycast, `RaycastCommand`, `Instantiate(`, `List<Transform>`, shader `cos(`, shader `sin(`, or shader `pow(` hits.
- Scoped `git diff --check` over touched SHINOBU paths reports no whitespace errors; only a CRLF normalization warning on the cross-domain socket file.
- Generated `.csproj` files still do not include the new SHINOBU_252 source/test files; Unity import/project regeneration is still required.
- Build was not relaunched because the gate sample was `CPU=100%`, `dotnet/csc=0`.

## 2026-05-21 CSV Fence / Default Four-Corner Polish

What was wrong:
- The public CSV byte parser could mutate profile arrays without acquiring the SHINOBU profile write fence.
- CSV header detection treated any first token beginning with `module` as a header, which could silently drop valid module IDs such as `module_alpha`.
- Empty first CSV tokens were accepted as the FNV offset basis instead of being rejected.
- The no-CSV fallback support pattern produced a center pylon plus three corners at Ultra, leaving one corner unrepresented.

What was done:
- Wrapped public `TryLoadProfilesFromCsvBytes` with the profile write fence.
- Routed `TryLoadProfilesFromCsvFile` through a private parser after it already owns the write fence and Vault edit locks.
- Replaced broad header matching with exact first-token matching and rejected empty module hash tokens.
- Added edit tests for read-fence parser rejection and module-prefixed profile names.
- Changed default ray zero to slide from center toward the missing fourth corner with `smoothstep(0.65, 1.0, GlobalQualityWeight)`.

Cinematic Cheats used:
- Weak devices still get the cheap central pillar; Ultra gets four corner anchors through a continuous visual glide instead of adding a fifth support lane or changing DTO capacity.

Static Microsecond Estimates - profiler proof absent:
- 0 us normal hot path for CSV changes; CSV parsing is cold/editor.
- One `smoothstep` in the fallback default profile path buys a stronger Ultra silhouette without increasing ray capacity or GPU buffer layout.

Verification:
- Static scan over SHINOBU-owned foundation runtime/test/shader files found no `TryResolveVaultViews`, `File.ReadAllBytes`, numeric `(BufferID)709xx` casts, `Camera.main`, scene search tokens, `TryGetBuffer`, `FloatMode.Deterministic`, quality-proxy token, PhysX raycast, `RaycastCommand`, `Instantiate(`, `List<Transform>`, shader `cos(`, shader `sin(`, or shader `pow(` hits.
- Scoped diff check on edited SHINOBU source/test files reported no whitespace errors.
- Sidecar construction report JSON was updated; parse verification is pending after this log append.
- Build was not relaunched because the fresh gate sample was `CPU=100%`, `dotnet/csc=0`.

## 2026-05-21 Shader ABI / Variant Warmup / Socket Fence Patch

What was wrong:
- Shader inclusion was not enough to prove variant warmup; `SetPass(0)` inside component setup could still hitch.
- Transparent procedural supports were unsorted and overdraw-prone.
- Matrix X/Z scale carried radius while the procedural shader ring uses local radius `0.5`, producing a radius/diameter ABI mismatch.
- Socket module buffers were read by scheduled Foundation jobs without an executable producer/consumer fence beyond DataVault relocation locks.
- Profile fences were plain static ints, and schedule exceptions could leak profile/socket/Vault locks.

What was done:
- Added and preloaded `Hecton_FoundationPylon.shadervariants` GUID `0e3d6c95b94344c7b864f17da3f25207`.
- Removed runtime `SetPass` warmup; shader stays one opaque `ZWrite On` pass.
- Changed pylon matrices to store diameter on X/Z and draw bounds to use local half-extents plus flare inflation.
- Added socket module read/write fences; Foundation holds the read fence across scheduled jobs and the socket mock writer owns the write fence.
- Made profile fences atomic with `Interlocked`/`Volatile`.
- Wrapped the schedule chain with cleanup; exceptional partial schedules force-complete before releasing locks.
- Added `[WriteOnly]` to pure output job arrays and 64-bit product guards to the public mock SDF job.

Cinematic Cheats used:
- Still one procedural indirect draw and shader bottom flare; no support GameObjects, no terrain deformation, no PhysX raycasts.
- Opaque depth-writing supports buy cheaper visibility behavior than transparent sorting.

Static Microsecond Estimates - profiler proof absent:
- Normal path adds only fixed atomic fence operations per scheduled batch.
- Removes one cold runtime shader pass call and reduces transparent overdraw risk.
- Diameter ABI fix prevents visual/bounds mismatch without extra shader ALU.

Verification:
- SHINOBU-owned foundation runtime/test/shader files have no `TryResolveVaultViews`, `File.ReadAllBytes`, numeric `(BufferID)709xx` casts, `Camera.main`, scene search tokens, `TryGetBuffer`, `FloatMode.Deterministic`, quality-proxy token, PhysX raycast, `RaycastCommand`, `Instantiate(`, `List<Transform>`, shader `cos(`, shader `sin(`, shader `pow(`, or `SetPass(` hits.
- Sidecar and shared construction report JSON parse with `ConvertFrom-Json`.
- Scoped `git diff --check` reports no whitespace errors; only CRLF normalization warning on the touched socket bridge file.
- Build was not relaunched because the fresh gate sample was `CPU=100%`, `dotnet/csc=0`.

## 2026-05-21 Bootstrap SVC / Pure Socket Read / CSV Lock Ownership Patch

What was wrong:
- Dewey audit found the pylon SVC was in `GraphicsSettings.m_PreloadedShaders` but absent from `00_BOOTSTRAP.unity` `shaderVariantCollections`, the list warmed by `GameBootstrapper`.
- Hegel audit found the touched socket bridge missed `Hecton8.Core.Memory.Layout`, so `[BinaryBlittableSafe]` could fail resolution.
- Legacy `ShinobuSocketConstructionRuntime.TryResolveVaultViews` still called `TryResolveHandle`, keeping a read-looking facade on a mutation-capable Vault route.
- `TryLoadProfilesFromCsvFile` could call `EndProfileEditLocks` after partial lock acquisition and decrement a lock this caller did not own.

What was done:
- Added pylon SVC GUID `0e3d6c95b94344c7b864f17da3f25207` to `Assets/_Project/Scenes/00_BOOTSTRAP.unity` `shaderVariantCollections`.
- Extended the editor test to assert that both `GraphicsSettings.asset` and `00_BOOTSTRAP.unity` reference the pylon SVC.
- Added `using Hecton8.Core.Memory.Layout;` to `ShinobuSocketConstructionData.cs`.
- Changed `TryResolveVaultViews` into a pure compatibility wrapper over `TryReadVaultViews`.
- Changed `TryBeginProfileEditLocks` to return an acquired lock count and `EndProfileEditLocks(vault, lockedCount)` to release only owned locks.

Cinematic Cheats used:
- Pylons remain a procedural indirect draw with shader flare; the new warmup route only prevents shader discovery cost from appearing during active play.
- No support GameObjects, no PhysX raycasts, no terrain deformation.

Static Microsecond Estimates - profiler proof absent:
- 0 us per-frame pylon path for namespace/read-wrapper/CSV lock-count changes.
- Moves shader variant warmup into boot/loading instead of risking an active-frame hitch.
- Prevents cold CSV reload lock-count corruption that could expose a DataVault buffer to relocation while a scheduled read expects it locked.

Verification:
- `TryResolveVaultViews` body is only `return TryReadVaultViews(vault, out views);`.
- `00_BOOTSTRAP.unity` contains `SceneRoots`, `m_Roots`, and pylon SVC GUID `0e3d6c95b94344c7b864f17da3f25207`.
- Brace/preprocessor counts are balanced in the touched foundation/socket/editor/test C# files.
- SHINOBU-owned foundation runtime/test/shader files have no PhysX raycast, `RaycastCommand`, `Instantiate(`, `List<Transform>`, `Camera.main`, scene search tokens, `Resources.Load`, `TryGetBuffer`, `FloatMode.Deterministic`, quality-proxy token, `File.ReadAllBytes`, numeric `(BufferID)709xx`, `SetPass(`, shader `sin(`, shader `cos(`, or shader `pow(` hits.
- Sidecar and shared construction report JSON parse with `ConvertFrom-Json`; scoped `git diff --check` reports no whitespace errors, only LF/CRLF normalization warnings on the touched socket bridge and shared report.
- Build was not relaunched because the fresh gate sample was `CPU=100%`, `dotnet/csc=0`.
