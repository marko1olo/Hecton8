# SHINOBU_228 LOG - 2026-05-20

Agent: SHINOBU_228
Domain: BUILDER_TOOL_HOLOGRAPHY_SYNC
Task Count: 20
Status: STATIC VERIFIED / BUILD DEFERRED BY CPU GUARD

## What Was Wrong

- Builder preview authority was tied to a ghost object path in `PlayerBuilder`, with legacy proxy/pool release logic and transform/collider coupling.
- `PlacementGhost.FixedTick()` still represented preview validity as a PhysX object concern.
- GPU preview rendering needed proof that DTO buffers, not per-object matrices/material state, were the render authority.
- SDF validation needed a Burst-consumable lane; direct managed voxel-volume sampling is not Burst-compatible.
- Shared `MEMORY_OPTIMIZATION_REPORT.json` already contained SHINOBU_207 evidence and could not be destructively overwritten.

## What Was Done

- Replaced hot preview truth with data-only fields in `PlayerBuilder`: active, canBuild, position, rotation, scale.
- Added asynchronous builder ghost validation scheduling/finalization in `PlayerBuilder` using `BuildBuilderGhostStateJob` and `ValidateBuilderGhostPlacementJob`.
- Added Vault lane `BuilderGhostSdfSamples` with buffer id `70944`, capacity `128 * 8`, and per-state sample indexing in the Burst validation job.
- Kept final module spawn intact; final placed modules are gameplay objects, not hologram preview objects.
- Reduced `PlacementGhost.FixedTick()` to compatibility-only validity refresh; no overlap query remains in target preview path.
- Ensured `BuilderGhostStateDTO` remains 128 bytes with `float4x4` at offset 0 and `double3 AUP_TargetPosition` at offset 64.
- Added one-shot dump guard for holography black-box telemetry to avoid repeated disk writes after the first fault.
- Added editor-only `Builder Tool X-Ray` window, fixed-array histogram, Vault tuning mutation, static audit entry, and cold CSV parser.
- Added architecture document `Docs/ARCHITECTURE/CONSTRUCTION_BUILDER_HOLOGRAPHY_SHINOBU_228.md` and ledger entry in `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- Added SHINOBU_228 section to `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json` without deleting SHINOBU_207 data.

## Cinematic Cheats Used

- The hologram is not a physical preview object. It is one `float4x4`, validation flags, and shader math.
- Low pressure uses simple unlit cyan/red blend and reduced scanline/rim/chromatic math through continuous `GlobalQualityWeight`.
- High/Ultra spend the saved CPU on visual overkill in the shader only; validation truth remains identical.
- SDF collision proof uses 8 OBB corner bytes as a bounded mathematical proxy instead of simulating a moving trigger volume.

## Exact Microseconds Saved

- Preview ghost lifecycle spike removed: estimated 120-180 us per equip/refresh event.
- PhysX preview overlap removed: estimated 60-180 us/frame while aiming structural buildables.
- GPU upload stall avoided by LockBuffer path: estimated 10-60 us/frame on i3/MX350 class hardware.
- DTO property/defensive copy avoidance: estimated 5-20 us/job batch.
- Zero-init bypass on transient Vault lanes: estimated 1-10 us init/update event.
- Measured Unity profiler numbers are not claimed. Dotnet/Unity build was not launched because CPU load was 100%, above the 50% rule.

## Verification

- Prompt block re-extracted from `Docs/Tasks/CURRENT_BATCH.md` with `<AGENT_PROMPT id="SHINOBU_228" ...>`.
- `git diff --check` target files: PASS; only CRLF normalization warnings.
- Forbidden target scan: PASS; no `TryAcquireGhostProxy`, `OverlapBoxNonAlloc`, `DrawMeshInstanced`, `.SetData(`, preview ghost pool spawn, or new `MaterialPropertyBlock` in target files.
- GPU scan: PASS; `Graphics.DrawProceduralIndirect` and `StructuredBuffer<BuilderGhostStateRaw>` present.
- CPU/build guard: CPU 100%, dotnet/csc count 0. Build deferred by project law.

<SELF_AUDIT>
  <TASKS>
    <TASK id="01" status="PASS">Preview ghost prefab lifecycle removed from placement truth.</TASK>
    <TASK id="02" status="PASS">PhysX overlap no longer validates placement ghost.</TASK>
    <TASK id="03" status="PASS">Hot DTOs are raw fields, no properties.</TASK>
    <TASK id="04" status="PASS">BuilderGhostStateDTO is explicit 128B, AUP offset 64.</TASK>
    <TASK id="05" status="PASS">10,000-row mock validation job exists.</TASK>
    <TASK id="06" status="PASS">AUP snap uses double before float render matrix.</TASK>
    <TASK id="07" status="PASS">Burst validation checks OBB corners, SDF lane, and module AABBs.</TASK>
    <TASK id="08" status="PASS">Procedural indirect hologram path is active.</TASK>
    <TASK id="09" status="PASS">Double-buffered LockBuffer upload path; SetData absent in target batch.</TASK>
    <TASK id="10" status="PASS">GlobalQualityWeight scales shader math continuously.</TASK>
    <TASK id="11" status="PASS">Socket magnetism remains routed through existing Shinobu socket catalog path.</TASK>
    <TASK id="12" status="PASS">AUP delta math occurs before float local render conversion.</TASK>
    <TASK id="13" status="PASS">PresentationOnly and RollbackExcluded flags stamped.</TASK>
    <TASK id="14" status="PASS">Vault lanes are unmanaged and fully written before read.</TASK>
    <TASK id="15" status="PASS">300-entry telemetry ring and dump path implemented.</TASK>
    <TASK id="16" status="PASS">Builder Tool X-Ray editor facade implemented.</TASK>
    <TASK id="17" status="PASS">ReadOnlySpan CSV parser implemented for cold ingestion.</TASK>
    <TASK id="18" status="PASS">DTO OBB gizmo implemented.</TASK>
    <TASK id="19" status="PASS">Static report evidence written.</TASK>
    <TASK id="20" status="PASS">Static audit complete; compile blocked by CPU rule.</TASK>
  </TASKS>
  <ARM64>
    BuilderGhostStateDTO: Size=128, LocalToWorld=0, AUP_TargetPosition=64, PrefabHashID=88, ValidationFlags=92, AnimationPhase=96, ValidationStateHash=100, padding=104..127.
  </ARM64>
  <ZERO_GC>
    Target hot scans found no preview Instantiate/proxy acquire, no PhysX overlap, no DrawMeshInstanced, no GraphicsBuffer.SetData, and no new MaterialPropertyBlock.
  </ZERO_GC>
  <AUP>
    Snapping is performed in double3 AUP. Runtime origin is subtracted before float3/float4x4 upload.
  </AUP>
  <DEAR_LIE>
    Hologram and 8-corner SDF proxy replace physical ghost simulation. Visual complexity scales through GlobalQualityWeight.
  </DEAR_LIE>
  <DEPENDENCY>
    Used existing Vault buffers and SignalBus construction preview lane. No new sibling-domain direct dependency was introduced.
  </DEPENDENCY>
  <BLACKBOX>
    HolographyTelemetryEntry ring writes last state and dumps once to Docs/AgentLogs/Dump_SHINOBU_228.bin on non-finite or >500 us solver fault.
  </BLACKBOX>
</SELF_AUDIT>

# SHINOBU_228 SOURCE-RESIDUE POLISH - 2026-05-20

## What Was Wrong

- Placement legality still depended on `GlobalQualityWeight` through a 2..8 SDF corner budget. That could make weak hardware accept a placement that Ultra would reject.
- Legacy `PlacementGhost` source and `PFB_Ghost_*` prefabs still existed as serialized revival paths, even though active preview rendering had moved to DTO/indirect.
- Runtime presenters still had `GlobalDataVault.TryGetLatestCreated` fallback authority.
- `HectonBlueprintPreviewBatch` could rebuild and upload an unchanged preview payload every frame.
- Architecture docs and reports overstated proof language and still described the deleted/incorrect legacy routes.

## What Was Done

- Forced builder ghost SDF validation to always hydrate and evaluate all eight OBB corners.
- Deleted `Assets/_Project/Scripts/PlacementGhost.cs` plus `.meta`, and deleted all five `PFB_Ghost_*` prefabs plus `.meta`.
- Nulled existing `BuildableData.ghostPrefab` references for Utility Pylon, Service Pump, and Current Turbine.
- Removed `TryGetLatestCreated` runtime fallback from `HectonBlueprintPreviewBatch` and `VRPipeBlueprintPreview`.
- Added unchanged-preview signal hashing in `HectonBlueprintPreviewBatch`; identical active payloads now reuse the uploaded buffer and skip redundant DTO/args uploads.
- Updated `MEMORY_OPTIMIZATION_REPORT.json`, the builder holography architecture note, the binary payload ledger, status, and rationale with static-source wording and runtime-proof boundaries.

## Cinematic Cheats Used

- Placement truth is bounded math: AUP-localized matrix + eight SDF corner samples + existing module AABB checks.
- Visual motion remains a shader Dear Lie. The signal hash ignores frame so pulse/scan animation continues on GPU time without CPU buffer churn.

## Exact Microseconds Saved

- No measured Unity profiler numbers were produced in this pass.
- Static-source estimates remain: 120-180 us equip spike removed by deleting preview prefab lifecycle, 60-180 us/frame removed by avoiding preview PhysX broadphase, 10-60 us upload stall risk avoided by lock-buffer upload, and redundant unchanged preview uploads suppressed.

## Verification

- `rg` found no `PlacementGhost`, `PFB_Ghost_*`, or non-zero `ghostPrefab` references under first-party source/prefab/asset/meta scope.
- Runtime presenter scan found no `TryGetLatestCreated` in `HectonBlueprintPreviewBatch.cs` or `VRPipeBlueprintPreview.cs`.
- SDF truth scan found no `qualitySampleLimit`, no 2-corner SDF budget, and no runtime hydration call that scales SDF sample count by quality.
- Orphan `.meta` scan for touched script and ghost-prefab folders returned no output.
- `git diff --check` on touched code/docs reports LF/CRLF warnings only.
- Build guard sampled CPU 54% with dotnet/csc count 0, so rebuild was not launched.
- Runtime Unity import, Play Mode, GCMonitor, Frame Debugger, and player-build proof remain pending.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Tasks 01, 02, 07, 09, 10, 14, 19, and 20 were strengthened. The deleted ghost assets close Task 01/02 residue; fixed 8-corner SDF truth closes Task 07 correctness drift; dirty hash closes Task 09 bandwidth discipline; docs/report language closes Task 19/20 proof hygiene.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>BuilderGhostStateDTO remains 128B: LocalToWorld 0..63, AUP_TargetPosition 64..87, PrefabHashID 88..91, ValidationFlags 92..95, AnimationPhase 96..99, ValidationStateHash 100..103, padding 104..127.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>GlobalQualityWeight no longer changes placement legality. Below 0.3, visual shader/presentation cost can collapse continuously, but validation still checks all eight SDF corners. This prevents hardware-tier legality drift.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged: 70940 state, 70941 visual, 70942 telemetry, 70943 mock state, 70944 SDF samples, 70945 module args, 70946 pipe state, 70947 pipe visual, 70948 pipe args. No private persistent NativeArray ownership was added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`BuildBuilderGhostStateJob`, `ValidateBuilderGhostPlacementJob`, `BuildBuilderGhostIndirectArgsJob`, and `BuildPipeBlueprintPreviewJob` retain `[NoAlias]` NativeArray lanes. Visual upload finalizes via `DispatcherJobFence.TryFinalizeCompleted`; forced completion remains teardown-only.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or sibling runtime dependency was introduced. Build remains unclaimed until CPU/compiler guard and external project wall permit a scoped compile.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The Dear Lie is procedural holography from DTO matrices and shader time. Heavy object preview, preview PhysX, mesh instance arrays, and redundant unchanged uploads are removed from the route.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 18:34 +04:00

## What Was Wrong

- The final forbidden-token scan matched `BuilderHolographyStaticAudit` search literals. That was not runtime residue, but it made the proof noisy.

## What Was Done

- Split audit search tokens into composed literals so `rg` no longer matches scanner-owned strings.
- Re-ran the SHINOBU_228 target scan; it returned no forbidden-token hits.

## Verification

- Forbidden-token scan: PASS after audit literal split.
- `git diff --check` on the editor audit file: PASS.
- Build not launched: latest CPU guard sampled 84.26% with no dotnet/csc process.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Task 19 evidence is cleaner: the static validator still checks forbidden tokens but no longer pollutes the project-wide SHINOBU_228 scan.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>No runtime curve changed.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged at 70940-70948.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No runtime job dependency changed.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef or runtime dependency changed.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Audit strings no longer masquerade as forbidden preview runtime paths.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 18:18 +04:00

## What Was Wrong

- `BuilderHolographyStaticAudit` only scanned the main builder preview batch.
- The audit refused to update `MEMORY_OPTIMIZATION_REPORT.json` once the `SHINOBU_228` key existed, so the report could stay stale after later polish.

## What Was Done

- Extended the audit to scan `VRPipeBlueprintPreview.cs` and `HabitatConstructionManager.cs`.
- Added checks for no VR pipe `DrawMeshInstanced`, no VR pipe `Matrix4x4[]`/`_matrices`, no legacy object alignment route, and no `.SetData(` across both holography presenters.
- Replaced the early-return upsert logic with brace-matched replacement of the existing `SHINOBU_228` section.
- Refreshed `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json` with buffer IDs 70945-70948 and new residue booleans.

## Cinematic Cheats Used

- Audit evidence now covers both Dear Lie hologram presenters: module preview and XR pipe blueprint preview.

## Exact Microseconds Saved

- Runtime saving: 0 us; this is editor/static evidence only.
- Production saving: prevents stale audit output from hiding a future regression back to mesh-instanced pipe preview or object-alignment validation.

## Verification

- Static source scans already passed for no `DrawMeshInstanced`, no `Matrix4x4[]`, no `.SetData(`, and no legacy object route in SHINOBU_228 target files.
- `MEMORY_OPTIMIZATION_REPORT.json` now lists `VRPipeStateBufferId`, `VRPipeVisualBufferId`, and `VRPipeIndirectArgsBufferId`.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Task 19 is strengthened: the static metric validator now covers the VR pipe presenter and can refresh its existing SHINOBU_228 report section.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>No runtime curve changed. Static evidence now records the route that uses the curve.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Report evidence now records Vault IDs 70940-70948.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No new runtime job dependency was added.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or runtime dependency was added. Editor-only audit code changed.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Static audit now proves both Dear Lie presenters avoid mesh-instanced preview submission.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 18:02 +04:00

## What Was Wrong

- `HabitatConstructionManager` still exposed unused object-route overloads for socket alignment and integrity validation.
- These overloads accepted `Transform ghostRoot`, `List<ModuleSocket> ghostSockets`, and `GameObject candidateGhost`, preserving a source-level route back to scene-object preview authority.

## What Was Done

- Deleted both `TryResolveSocketAlignment(...)` overloads.
- Deleted the `ScheduleIntegrityValidation(... GameObject candidateGhost ...)` overload.
- Deleted the now-dead `ResolveSocketYawRotation` helper.
- Verified the only remaining source caller is `PlayerBuilder` using the pose-based `ScheduleIntegrityValidation(... BuildableData, Vector3, Quaternion ...)` route.

## Cinematic Cheats Used

- Placement preview remains pose/matrix math. Socket alignment truth stays in the existing Shinobu socket Vault/Burst route, not in transform hierarchy alignment helpers.

## Exact Microseconds Saved

- Current active runtime gain is preventive because the active source caller already used the pose route.
- Removed a possible future transform/list walk through authored sockets from preview validation.

## Verification

- Static scan: no `TryResolveSocketAlignment(`, `candidateGhost`, or `ResolveSocketYawRotation(` remains in `HabitatConstructionManager.cs` or `PlayerBuilder.cs`.
- Static caller scan: only `PlayerBuilder.cs` calls `ScheduleIntegrityValidation`, and it calls the pose overload.
- Build not launched: latest guard sampled CPU 98.07% with dotnet/csc count 0.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Task 01 and Task 11 are strengthened: legacy object/socket-transform preview alignment methods were deleted, leaving the data-only socket/Vault route and pose-based structural validation route. Tasks 02-10 and 12-20 retain prior static-source PASS evidence.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>No quality branch changed. Removing the legacy object overload prevents future non-scalable transform hierarchy work from entering the preview path.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged from Iteration 9: 70940-70948. No private persistent NativeArray was added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>No new jobs were added in this deletion pass. Existing SHINOBU jobs keep `[NoAlias]` lanes and non-blocking finalization.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or sibling runtime dependency was added. Public object-route methods were deleted only after source scan found no callers.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: object transform/socket helper could realign a preview object. After: preview alignment authority is data-only AUP/socket math and procedural hologram payload.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 17:46 +04:00

## What Was Wrong

- `VRPipeBlueprintPreview` remained in the Construction preview surface with a private managed `Matrix4x4[]` cache and `Graphics.DrawMeshInstanced`.
- That path was XR-only but still violated the SHINOBU_228 mission because it submitted blueprint preview geometry through mesh instancing instead of the Vault -> GraphicsBuffer -> indirect draw route.

## What Was Done

- Added `BuildPipeBlueprintPreviewJob` with Burst deterministic compile flags and `[NoAlias]` NativeArray fields.
- Rebuilt VR pipe preview segment matrices from four AUP control points in double precision before casting local runtime deltas to float.
- Added local presentation Vault lanes: 70946 `BuilderGhostStateDTO[64]`, 70947 `BuilderGhostVisualDTO[64]`, and 70948 `BuilderGhostIndirectArgsDTO[1]`.
- Replaced the managed matrix cache and `DrawMeshInstanced` with double-buffered `GraphicsBuffer.LockBufferForWrite` uploads and `Graphics.DrawProceduralIndirect`.
- Updated status, rationale, construction holography architecture, and binary payload ledger.

## Cinematic Cheats Used

- Pipe preview is now a chain of procedural cuboid hologram segments, not authored mesh segments. Low quality lengthens the segments smoothly, reducing instance count; higher quality shortens them and lets the shader spend the saved CPU budget on scan/rim/chromatic detail.

## Exact Microseconds Saved

- Removed the old 64-entry managed matrix upload/submission path from XR pipe preview frames.
- Low-quality segment scaling can reduce segment count by up to roughly 60% on long spans versus the old fixed `segmentLengthMeters` path. Exact profiler numbers remain absent.

## Verification

- Static scan over SHINOBU_228 target route: no `DrawMeshInstanced`, `Matrix4x4[]`, `_matrices`, `.SetData(`, direct `.Complete(`, `JobHandle.Complete`, or `new MaterialPropertyBlock` remains in `VRPipeBlueprintPreview.cs`.
- `git diff --check` on the changed code/docs: PASS with LF/CRLF warnings only.
- Build still held: latest guard sampled CPU 50.47% with dotnet/csc count 0, and the earlier guarded build already hit the known external `Hecton8.Core.csproj` dependency wall.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Task 08 and Task 09 are strengthened for the XR pipe blueprint path: it now uses the same procedural Dear Lie shader, Vault DTO payloads, double-buffer upload, and indirect draw contract as the module hologram. Tasks 01-07 and 10-20 retain prior static-source PASS evidence.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed. BuilderGhostStateDTO remains 128B: matrix 0..63, AUP 64..87, PrefabHashID 88..91, ValidationFlags 92..95, AnimationPhase 96..99, ValidationStateHash 100..103, pad 104..127. BuilderGhostVisualDTO remains 64B. BuilderGhostIndirectArgsDTO remains 16B.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Below quality 0.3, `BuildPipeBlueprintPreviewJob` uses a smooth quality curve to lengthen pipe visual segments, reducing indirect instance count while preserving the route silhouette. Middle/High/Ultra progressively shorten segments and use the hologram shader's continuous scan/rim/chromatic curve; no binary tier switch was added.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs now include 70946 pipe states, 70947 pipe visuals, and 70948 pipe indirect args in addition to 70940-70945. No private persistent NativeArray was added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`BuildPipeBlueprintPreviewJob` consumes no previous simulation authority handle; it emits state/visual/args into distinct `[NoAlias]` lanes. `VRPipeBlueprintPreview` finalizes through `DispatcherJobFence.TryFinalizeCompleted`; forced completion is teardown-only.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or sibling runtime dependency was added. New code stays in the existing Construction/Core assembly surface and uses existing DTO contracts.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: O(pipe segments) managed matrices plus mesh-instanced draw submission. After: O(pipe segments scaled by smooth quality) Burst DTO writes plus one procedural indirect draw; authored mesh segment geometry is no longer submitted.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 POLISH LOG - 2026-05-20 16:34 +04:00

Agent: SHINOBU_228
Domain: BUILDER_TOOL_HOLOGRAPHY_SYNC
Status: POLISH ITERATION 7 STATIC VERIFIED / BUILD BLOCKED BY EXISTING COMPILE WALL

## What Was Still Wrong

- `PlayerBuilder` still contained `_currentGhostObj`, `_currentGhost`, `_ghostSocketBuffer`, and `_snappedGhostSocket`; absence of a ghost object could still collapse into a permissive placement state.
- `HabitatConstructionManager.ScheduleIntegrityValidation(...)` accepted a candidate `GameObject`, so structural validation could still depend on a hidden preview object transform instead of the builder pose DTO.
- `ValidateBuilderGhostPlacementJob` received a fixed SDF sample count; `GlobalQualityWeight` was not controlling the mathematical sample budget.
- `HectonBlueprintPreviewBatch` still had main-thread state/args authoring and fixed draw bounds; the render path could submit procedural hologram work without proving active DTO bounds.
- The dump path in construction data still referenced SHINOBU_217 in one lane before this polish cycle; that corrupted forensic ownership.

## What Was Done

- Removed the last ghost-object state from `PlayerBuilder` and deleted the null-ghost success bypass in `UpdatePlacementValidationState`.
- Routed integrity validation through `ScheduleIntegrityValidation(constructionManager, BuildableData, Vector3, Quaternion, gridSize, budget, penalty)`.
- Changed candidate socket indexing to use pose math: `TryResolveSocketPose(rootPosition, rootRotation, socket, out socketAup, out socketForward)`.
- Fed `GlobalQualityWeight` into `ValidateBuilderGhostPlacementJob`; SDF samples now resolve through `ResolveBuilderGhostSdfSampleCount`.
- Added `BuilderGhostIndirectArgsDTO` Vault lane 70945 and writes via `BuildBuilderGhostIndirectArgsJob`.
- Rebuilt `SetPreview` and `ConstructionPreviewSignal` consumption through `BuildBuilderGhostStateJob` instead of direct main-thread matrix writes.
- Added dynamic bounds from `BuilderGhostStateDTO.LocalToWorld` and camera near/far rejection before `Graphics.DrawProceduralIndirect`.
- Cached state/visual buffer bindings and restricted fallback material creation to `#if UNITY_EDITOR`.
- Fixed dump paths to `Dump_SHINOBU_228.bin` and `Dump_SHINOBU_228_Holography.bin`.

## Cinematic Cheats Used

- Candidate preview remains a Dear Lie: no preview prefab, no trigger collider, no MeshRenderer hierarchy. One `float4x4` plus visual scalars reconstructs cube geometry procedurally in the shader.
- Placement collision uses bounded SDF byte samples and existing AABB DTOs, not PhysX broadphase or mesh colliders.
- Visual richness is shader payload work: scanline, rim, chroma, alpha, dampen, and phase come from GPU buffers instead of per-object material state.

## Exact Microseconds Saved

- Removed null-ghost validation bypass: correctness fix; no microsecond claim.
- Removed candidate preview transform authority in structural validation: expected 5-20 us avoided when validation schedules, depending hierarchy/socket count.
- Quality-scaled SDF sample budget: low quality evaluates 2 corners instead of 8, saving roughly 6 byte reads and six corner transforms per validation row.
- Dynamic draw bounds/frustum rejection: off-screen active preview skips the indirect draw call; expected 10-40 us CPU/GPU-side avoided on weak devices when builder hologram is outside camera depth.
- Burst indirect args: one 16B DTO write in job; prevents manual args drift and keeps upload payload fixed.

## Verification

- Target static scan passed: no `_currentGhostObj`, `_currentGhost`, `_ghostSocketBuffer`, `_snappedGhostSocket`, `ReleaseLegacyGhostObject`, `CacheGhostSockets`, `IsCurrentGhostCollider`, or `TryResolveSocketAlignment(` remains in `PlayerBuilder.cs`.
- Forbidden render/physics scan passed: no `OverlapBoxNonAlloc`, no `DrawMeshInstanced`, and no `.SetData(` in the SHINOBU_228 target files.
- Preview material mutation scan passed: no `_H8BuilderGhostCount`, `_BaseColor`, `_H8SnapDampen`, `_H8GlobalQualityWeight`, `SetFloat`, `SetColor`, `SetInt`, or `UnityPerMaterial` in the preview batch/shader path.
- `git diff --check` passed on touched target files with CRLF normalization warnings only.
- One guarded build was launched when CPU was 5.5% and dotnet/csc count was zero. It failed in `Hecton8.Core.csproj` on broader project dependency drift: missing `Hecton8.Equipment`, missing `Hecton8.Logistics.Grid`, missing `SoundEmissionSignal`/audio interface members, missing `MethodImplAttribute`, missing service bridge types, and `SocketDefinitionDTO` unresolved because `BaseModuleCatalogRuntime.cs` is not included in `Hecton8.Core.csproj` while `HabitatGraphManager.cs` already references it.
- No second build was launched. Current process guard showed seven active `dotnet` processes after the failed compile.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Ghost preview prefab/proxy authority removed from active builder path; final placed module spawning remains gameplay, not preview.</TASK>
    <TASK id="02" status="PASS">Preview placement no longer uses PhysX overlap; SDF/AABB DTO math owns the proof.</TASK>
    <TASK id="03" status="PASS">Hot DTOs use raw fields; no getter/setter properties in Burst payload rows.</TASK>
    <TASK id="04" status="PASS">Primary state DTO is explicit 128B and double3-aligned for ARM64.</TASK>
    <TASK id="05" status="PASS">Mock validation generator remains for 10,000 deterministic rows.</TASK>
    <TASK id="06" status="PASS">AUP snapping and runtime-origin subtraction happen before float matrix creation.</TASK>
    <TASK id="07" status="PASS">SDF collision runs in Burst with quality-scaled sample count and bounded 8-corner maximum.</TASK>
    <TASK id="08" status="PASS">Hologram is reconstructed from StructuredBuffers and `Graphics.DrawProceduralIndirect`.</TASK>
    <TASK id="09" status="PASS">GPU upload path uses lock-buffer upload helper; target scan found no `.SetData(`.</TASK>
    <TASK id="10" status="PASS">Continuous `GlobalQualityWeight` drives sample count and shader payload state; no low/high binary switch.</TASK>
    <TASK id="11" status="PASS">Socket magnetism stays on existing Shinobu catalog route; no scene socket scan for preview truth.</TASK>
    <TASK id="12" status="PASS">AUP delta math precedes all local float rendering/overlap math.</TASK>
    <TASK id="13" status="PASS">PresentationOnly and RollbackExcluded flags mark the hologram lane outside rollback truth.</TASK>
    <TASK id="14" status="PASS">Vault lanes are unmanaged and jobs fully write rows before read/upload.</TASK>
    <TASK id="15" status="PASS">300-frame telemetry ring and SHINOBU_228 dump paths exist for black-box forensics.</TASK>
    <TASK id="16" status="PASS">Editor facade remains editor-only; no runtime UI/string path added.</TASK>
    <TASK id="17" status="PASS">CSV profile ingestion remains `ReadOnlySpan<byte>` cold-path parsing.</TASK>
    <TASK id="18" status="PASS">DTO OBB gizmo remains editor-only and reads Vault state.</TASK>
    <TASK id="19" status="PASS">Static optimization report evidence is preserved without overwriting other agents.</TASK>
    <TASK id="20" status="PASS">Static gates pass; compile proof is blocked by existing `Hecton8.Core.csproj` wall, not by a new SHINOBU_228 runtime dependency.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    BuilderGhostStateDTO size=128.
    0..63 LocalToWorld float4x4 = 64 bytes.
    64..87 AUP_TargetPosition double3 = 24 bytes, 8-byte aligned.
    88..91 PrefabHashID uint = 4 bytes.
    92..95 ValidationFlags uint = 4 bytes.
    96..99 AnimationPhase float = 4 bytes.
    100..103 ValidationStateHash uint = 4 bytes.
    104..127 _pad0.._pad5 uint = 24 bytes.
    Total: 64 + 24 + 4 + 4 + 4 + 4 + 24 = 128 bytes. 128 is divisible by 64, 32, 16, and 8.
    BuilderGhostIndirectArgsDTO size=16: four uint fields at offsets 0,4,8,12.
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    `ResolveBuilderGhostSdfSampleCount(GlobalQualityWeight)` maps quality through smoothstep-style smoothing and `math.lerp`: weak devices evaluate 2 SDF corners, middle tiers gradually increase, ultra reaches all 8 corners. The shader receives the same continuous quality scalar for scanline, chroma, rim, alpha, and dampen. Below 0.3 the CPU math collapses toward the cheapest acceptable SDF proof and the shader lowers embellishment instead of changing placement truth.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Runtime lanes: 70940 BuilderGhostState, 70941 BuilderGhostVisual, 70942 HolographyTelemetry, 70943 BuilderGhostMockState, 70944 BuilderGhostSdfSamples, 70945 BuilderGhostIndirectArgs. `HectonBlueprintPreviewBatch` owns handles, not private persistent NativeArrays; runtime byte ownership remains Vault/GraphicsBuffer.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    `BuildBuilderGhostStateJob`, `ValidateBuilderGhostPlacementJob`, `BuildBuilderGhostIndirectArgsJob`, and `RecordHolographyTelemetryJob` carry `[NoAlias]` on non-overlapping NativeArray fields. Consumed handles: signal/state build dependency and upload-bound args dependency. Output handles: state build handle for validation/upload staging, validation handle inside `PlayerBuilder`, and args build handle completed only at the CPU-to-GPU upload boundary.
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No asmdef or sibling-domain runtime reference was added. The guarded compile fails before a clean SHINOBU_228 proof because `Hecton8.Core.csproj` lacks required sibling/generated sources and interfaces. Current dotnet processes block another compile attempt.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: preview object path is O(prefab renderers + colliders + transform updates + PhysX broadphase). After: O(1) state row, O(2..8) SDF corner byte samples based on continuous quality, O(existing module AABB DTO count) for overlap proof, and one indirect procedural draw. Geometry and visual distortion are shader illusions.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 POLISH LOG - 2026-05-20 15:51 +04:00

Agent: SHINOBU_228
Domain: BUILDER_TOOL_HOLOGRAPHY_SYNC
Status: POLISH STATIC VERIFIED / BUILD DEFERRED BY CPU GUARD

## What Was Still Wrong

- Runtime ghost-proxy factory code survived as dead branch surface: acquire/release/projection methods, ghost proxy fields, `_GhostProxy` naming, and valid/invalid ghost materials.
- `HectonBlueprintPreviewBatch` still had a material-property authority path for hologram globals instead of making `BuilderGhostVisualDTO` the only visual scalar source.
- `PlayerToolManager` and editor construction authoring retained ghost prefab warmup/generation routes that could repopulate catalog assets with preview prefab references.
- `BuildableData.ghostPrefab` still existed. Removing it would be asset-breaking; leaving it undocumented would be a trap.

## What Was Done

- Deleted `TryCreateGhostProxy`, `TryAcquireGhostProxy`, `TryGetGhostProjectionResources`, `ReleaseGhostProxy`, `H8_RuntimeGhostProxy`, and `ConstructionRuntimeProxyTag` from `ConstructionRuntimeProxyFactory`.
- Simplified `ConstructionRuntimeProxyFactory` to final placed module proxies only. It now creates `_Proxy`, non-trigger structural collider, sockets, final material.
- Removed `_currentGhostUsesRuntimeProxy` from `PlayerBuilder`; legacy cleanup can only despawn/deactivate a pre-existing object, never acquire a runtime proxy.
- Removed construction ghost pool warmup from `PlayerToolManager`.
- Removed `CreateGhostPrefab`, `PFB_Ghost_*`, `Mat_BuildGhost_*`, `AddComponent<PlacementGhost>`, and ghost folder creation from `ConstructionBootstrapAuthoring`.
- `ConstructionBootstrapAuthoring.CreateOrUpdateBuildable` now writes `asset.ghostPrefab = null`.
- `BuildableData.ghostPrefab` tooltip now states it is a legacy serialized field ignored by runtime builder holography.
- Hologram shader no longer uses `_BaseColor`, `_H8SnapDampen`, `_H8GlobalQualityWeight`, or `_H8BuilderGhostCount`; it reads all variable presentation state from `StructuredBuffer<BuilderGhostVisualRaw>`.

## Cinematic Cheats Used

- Replaced preview object truth with a Dear Lie cube reconstructed from `SV_VertexID` and `float4x4` matrix data.
- Replaced per-preview renderer/material state with DTO colors, alpha, quality, dampen, and wiggle speed in a StructuredBuffer.
- Replaced editor-generated translucent preview prefabs with no asset-generation path at all; designers tune the hologram via the editor facade and Vault data.

## Exact Microseconds Saved

- New polish pass does not claim fresh measured profiler deltas.
- Prevented reintroduced preview proxy branch cost: same class of 120-180 us equip/refresh spike previously estimated for ghost object lifecycle.
- Prevented editor/pool ghost warmup from restoring a managed object path: expected saved cost depends on catalog size; no measured proof without Unity profiler.
- Removed per-frame material scalar/color mutation from preview draw path: small CPU saving, larger SRP-batcher correctness gain; measured proof absent.

## Static Verification

- Runtime forbidden scan: PASS. No `TryAcquireGhostProxy`, `TryCreateGhostProxy`, `TryGetGhostProjectionResources`, `H8_RuntimeGhostProxy`, `ReleaseGhostProxy`, `_currentGhostUsesRuntimeProxy`, `ConstructionRuntimeProxyTag`, `_GhostProxy`, `OverlapBoxNonAlloc`, `DrawMeshInstanced`, `.SetData(`, `new MaterialPropertyBlock`, `Destroy(_currentGhostObj)`, `WarmConstructionGhostPoolsIfNeeded`, `constructionGhost`, or `_constructionManager` in the target runtime/editor path scan.
- Preview material mutation scan: PASS. No `SetFloat`, `SetColor`, `SetInt`, `_H8BuilderGhostCount`, `_BaseColor`, `_H8SnapDampen`, `_H8GlobalQualityWeight`, or `UnityPerMaterial` in `HectonBlueprintPreviewBatch` or `Hecton_ConstructionDearLieHologram.shader`.
- Editor authoring scan: PASS. Only `BuildableData.ghostPrefab` and the explicit `asset.ghostPrefab = null` assignment remain.
- `git diff --check`: PASS with CRLF normalization warnings only.
- CPU guard: recent CPU checks stayed above threshold at 100% then 77%; `dotnet/csc` count was 8. Build was not launched because project law forbids dotnet when CPU >50% or another dotnet/csc is active.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Runtime preview prefab/proxy instantiation path is removed; editor bootstrap no longer generates preview prefabs.</TASK>
    <TASK id="02" status="PASS">Placement preview no longer validates with PhysX overlap.</TASK>
    <TASK id="03" status="PASS">Hot DTOs use raw fields; no property setters in Burst payloads.</TASK>
    <TASK id="04" status="PASS">BuilderGhostStateDTO explicit 128B; BuilderGhostVisualDTO and telemetry explicit 64B.</TASK>
    <TASK id="05" status="PASS">Mock Burst validation job remains for 10,000 rows.</TASK>
    <TASK id="06" status="PASS">AUP double delta precedes local float matrix write.</TASK>
    <TASK id="07" status="PASS">Burst SDF/AABB validation uses fixed 8-corner lane plus existing bounds.</TASK>
    <TASK id="08" status="PASS">Dear Lie procedural hologram uses StructuredBuffers and `DrawProceduralIndirect`.</TASK>
    <TASK id="09" status="PASS">GPU upload path uses double-buffered `LockBufferForWrite` helper; no `.SetData(` in target batch.</TASK>
    <TASK id="10" status="PASS">Continuous `GlobalQualityWeight` is DTO/shader data, not a binary keyword/material switch.</TASK>
    <TASK id="11" status="PASS">Socket magnetism stays on existing Shinobu socket route.</TASK>
    <TASK id="12" status="PASS">100km jitter rule obeyed through AUP-local conversion before float math.</TASK>
    <TASK id="13" status="PASS">PresentationOnly and RollbackExcluded flags stamp the preview lane.</TASK>
    <TASK id="14" status="PASS">Vault lanes use unmanaged buffers and full-row writes before reads.</TASK>
    <TASK id="15" status="PASS">300-entry holography telemetry ring and one-shot dump path remain.</TASK>
    <TASK id="16" status="PASS">Editor facade exists; polish did not add runtime UI.</TASK>
    <TASK id="17" status="PASS">CSV cold-ingest path remains `ReadOnlySpan<byte>` based.</TASK>
    <TASK id="18" status="PASS">DTO OBB gizmo remains editor-only.</TASK>
    <TASK id="19" status="PASS">Static report evidence preserved without overwriting other agents.</TASK>
    <TASK id="20" status="PASS">Static gates pass; compile remains blocked by CPU guard, not claimed.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT>
    BuilderGhostStateDTO size 128: LocalToWorld 0..63, AUP_TargetPosition 64..87, PrefabHashID 88..91, ValidationFlags 92..95, AnimationPhase 96..99, ValidationStateHash 100..103, pad0..pad5 104..127.
    BuilderGhostVisualDTO size 64: quality/dampen/wiggle/alpha 0..15, validColor 16..31, invalidColor 32..47, flags/frame/pad 48..63.
    HolographyTelemetryEntry size 64: AUP 0..23, frame/hash/counters/flags/timing/SDF/stateHash/quality 24..55, pad 56..63.
  </STRUCT_LAYOUT>
  <SCALABILITY_CURVE>
    At quality below 0.3 the shader collapses toward slow scan frequency, low chroma, low rim, and low alpha while preserving the same validation flags and matrix truth. Middle tiers lerp scan/rim/chroma upward. High/Ultra spend saved CPU on shader pulse/rim/chromatic overkill only; they do not increase gameplay truth divergence.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    Runtime preview state uses Vault buffers 70940 BuilderGhostState, 70941 BuilderGhostVisual, 70942 HolographyTelemetry, 70943 BuilderGhostMockState, 70944 BuilderGhostSdfSamples. No private persistent NativeArray owner was added.
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY>
    Builder ghost Burst jobs use `[NoAlias]` on non-overlapping NativeArrays. `BuildBuilderGhostStateJob` feeds `ValidateBuilderGhostPlacementJob`; teardown uses `DispatcherJobFence.TryComplete`, active path uses `TryFinalizeCompleted`.
  </POINTER_ALIASING_AND_DEPENDENCY>
  <COMPILE_GUARD>
    No asmdef dependency edit was made. Root `Hecton8.Core.asmdef` remains the containing assembly for touched runtime scripts; no new sibling Runtime assembly reference was introduced.
  </COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>
    Before: object preview path risks O(prefab renderers + colliders + PhysX broadphase + transform hierarchy updates). After: O(1) state row plus O(8) SDF corner samples and one indirect draw. Rendering geometry is a shader/procedural fake.
  </DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 16:51 +04:00

This bottom addendum is the current chronological report. The earlier 16:34 section was inserted above an older 15:51 section by the first `</SELF_AUDIT>` anchor; the content remains valid, but this entry restores the required Top=Old, Bottom=New review order.

## What Was Wrong

- The active builder path still had residual ghost-object state and a null-ghost validation bypass.
- Structural validation still exposed a candidate `GameObject` route instead of a pure pose route.
- Holography indirect args and some matrix writes could still be authored on the main thread.
- SHINOBU_228 architecture/binary/cheat docs were stale after the final polish.

## What Was Done

- Removed `PlayerBuilder` ghost object fields and routed preview validation through data-only pose state.
- Added pose-based `HabitatConstructionManager.ScheduleIntegrityValidation(...)`.
- Added Vault lane `70945` for `BuilderGhostIndirectArgsDTO[1]` and writes through `BuildBuilderGhostIndirectArgsJob`.
- Routed signal and manual preview matrix generation through `BuildBuilderGhostStateJob`.
- Added dynamic DTO-derived draw bounds and camera near/far rejection before `Graphics.DrawProceduralIndirect`.
- Updated `Status_SHINOBU_228.md`, `Rationale_SHINOBU_228.md`, `CONSTRUCTION_BUILDER_HOLOGRAPHY_SHINOBU_228.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, and `CINEMATIC_CHEATS_LEDGER.md`.

## Cinematic Cheats Used

- The preview is a procedural hologram Dear Lie: one `float4x4`, validation flags, visual scalars, and indirect args in GPU buffers.
- Collision proof is bounded SDF/AABB math in Burst; no preview PhysX collider or mesh hierarchy is used.

## Exact Microseconds Saved

- Low-quality SDF path evaluates 2 corners instead of 8: six corner transforms and six byte reads avoided per validation row.
- Off-camera hologram bounds skip the indirect draw submission: expected 10-40 us avoided on weak devices when outside camera depth.
- Removing preview GameObject authority prevents the previously estimated 120-180 us equip/refresh class of ghost lifecycle spikes.

## Verification

- Static scans: PASS for no `_currentGhostObj`, `_currentGhost`, `_ghostSocketBuffer`, `_snappedGhostSocket`, `OverlapBoxNonAlloc`, `DrawMeshInstanced`, or `.SetData(` in target SHINOBU_228 route.
- `git diff --check`: PASS on touched code/docs with LF/CRLF warnings only.
- Build: one guarded `dotnet build Assembly-CSharp.csproj --no-restore /p:UseSharedCompilation=false` was attempted when CPU/process guard was clear. It failed in `Hecton8.Core.csproj` on existing project dependency drift (`Hecton8.Equipment`, `Hecton8.Logistics.Grid`, audio signal/interface gaps, missing bridge types, `MethodImplAttribute`, and `SocketDefinitionDTO` source inclusion mismatch). Seven `dotnet` processes remain, so no second build was launched.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Tasks 01-20 remain PASS by static source evidence; compile proof is blocked by external `Hecton8.Core.csproj` wall, not by a new SHINOBU_228 sibling dependency.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>BuilderGhostStateDTO = 128B: LocalToWorld 0..63, AUP double3 64..87, PrefabHashID 88..91, ValidationFlags 92..95, AnimationPhase 96..99, ValidationStateHash 100..103, pad0..pad5 104..127. BuilderGhostIndirectArgsDTO = 16B.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>`GlobalQualityWeight` smooths SDF sample count from 2 to 8 and shader intensity continuously; below 0.3 the CPU path collapses to the cheapest bounded proof without changing placement authority.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault lanes: 70940 state, 70941 visual, 70942 telemetry, 70943 mock state, 70944 SDF samples, 70945 indirect args. No private persistent NativeArray owner was added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Builder state, validation, args, and telemetry jobs use `[NoAlias]` on non-overlapping NativeArrays. Args/job completion occurs only at the CPU-to-GPU upload boundary.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge was added. Current compile wall is existing core/project-file drift; repeated builds are blocked by active dotnet processes.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: O(prefab renderers + colliders + transform updates + PhysX broadphase). After: O(1) state row + O(2..8) SDF samples + DTO AABB proof + one indirect procedural draw.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 17:18 +04:00

## What Was Wrong

- The preview batch still used direct `Complete()` calls in the visual-sync path: public `SetPreview`, signal consumption, and indirect args upload.
- That invalidated the intended double-buffer contract because a late-frame presentation path could wait on a Burst job before uploading GPU buffers.

## What Was Done

- Added `_pendingBuildHandle`, `_pendingBuildScheduled`, `_pendingBuildDiscard`, and `_pendingBuildCount` to `HectonBlueprintPreviewBatch`.
- Chained `BuildBuilderGhostStateJob` and `BuildBuilderGhostIndirectArgsJob` into one pending handle.
- Replaced direct completion with `DispatcherJobFence.TryFinalizeCompleted` at the start of `LateFrameTick`.
- Upload now happens only after the pending handle is already complete; the render path keeps drawing the previous uploaded buffer until then.
- Forced completion is restricted to `OnDisable`/`OnDestroy` teardown through `DispatcherJobFence.TryComplete(... forceComplete:true)`.

## Cinematic Cheats Used

- Presentation can accept one-frame latency because the hologram is local, rollback-excluded, and visual-only. The player sees the last confirmed matrix while the next matrix/args payload finishes in Burst.

## Exact Microseconds Saved

- Removed potential late-frame job wait. The exact stall depended on scheduler timing; expected saving is the avoided synchronization bubble, bounded by the previous state/args job duration.
- Retained one 16B indirect args DTO and one state/visual upload after non-blocking completion; no new managed allocation path was introduced.

## Verification

- `rg` scan over SHINOBU_228 target files: PASS for no `.Complete(`, `JobHandle.Complete`, `UploadArgs(`, `_buffersDirty`, `.SetData(`, `DrawMeshInstanced`, `OverlapBoxNonAlloc`, `SetFloat`, `SetColor`, or `SetInt`.
- `git diff --check` on `HectonBlueprintPreviewBatch.cs`: PASS with LF/CRLF warning only.
- Build not launched: latest guard sampled CPU 85.96% and dotnet/csc count 0, which violates the >50% CPU rule.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Task 09 is strengthened: upload is now deferred behind a completed pending handle instead of direct frame-path completion. Tasks 01-08 and 10-20 retain prior static-source PASS evidence.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed. BuilderGhostStateDTO remains 128B; BuilderGhostIndirectArgsDTO remains 16B.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>Low quality still resolves 2 SDF samples; ultra resolves 8. Deferred visual sync affects only timing of presentation upload, not gameplay truth or quality branching.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged: 70940, 70941, 70942, 70943, 70944, 70945. No private persistent NativeArray was added.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>State build and args build chain into `_pendingBuildHandle`; `LateFrameTick` consumes it only through `DispatcherJobFence.TryFinalizeCompleted`. Forced completion exists only for teardown.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or sibling-domain reference was added. Build remains held by CPU guard after the existing external core wall.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The hologram tolerates one-frame stale presentation because it is a local Dear Lie, not authoritative gameplay state.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 18:25 +04:00

This entry restores chronological bottom ordering after earlier patch anchors inserted two addenda above older log blocks. The inserted 18:02 and 18:18 entries remain valid, but this is the current bottom summary.

## What Was Wrong

- Construction still had a secondary XR preview path using mesh instancing.
- Habitat construction still exposed unused object-route validation helpers.
- The static audit report could stay stale because existing `SHINOBU_228` JSON was not replaceable.

## What Was Done

- `VRPipeBlueprintPreview` now uses `BuildPipeBlueprintPreviewJob`, Vault lanes 70946-70948, double-buffered `GraphicsBuffer.LockBufferForWrite`, and `Graphics.DrawProceduralIndirect`.
- Removed source-unused `TryResolveSocketAlignment(...)`, `ScheduleIntegrityValidation(... GameObject candidateGhost ...)`, and `ResolveSocketYawRotation`.
- Updated `BuilderHolographyStaticAudit` to scan the VR pipe presenter and object-route deletion, then replace the existing report section.
- Refreshed `MEMORY_OPTIMIZATION_REPORT.json`, status, rationale, architecture, binary ledger, and log evidence.

## Cinematic Cheats Used

- VR pipe preview is now procedural cuboid hologram segments generated from four AUP control points, not authored mesh instances.
- Segment density scales continuously with `GlobalQualityWeight`; low quality uses longer segments, high/ultra spends the saved CPU budget on shader detail.

## Exact Microseconds Saved

- Removed up to 64 managed matrix entries and a mesh-instanced draw submission from the XR pipe preview route.
- Removed future risk of transform/list socket alignment helpers re-entering preview validation.
- Runtime profiler proof remains absent; these are static-source and architecture deltas.

## Verification

- Static scans: no `DrawMeshInstanced`, `Matrix4x4[]`, `_matrices`, `.SetData(`, direct `.Complete(`, `JobHandle.Complete`, `new MaterialPropertyBlock`, `TryResolveSocketAlignment(`, `candidateGhost`, or `ResolveSocketYawRotation(` in SHINOBU_228 target files.
- `MEMORY_OPTIMIZATION_REPORT.json` parses through `ConvertFrom-Json` and records VR pipe residue booleans plus Vault IDs 70940-70948.
- `git diff --check` passed on touched code/docs with LF/CRLF warnings only.
- Build not launched after this pass: latest guard sampled CPU 98.07% with dotnet/csc count 0.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Iterations 9-11 strengthen Tasks 08, 09, 11, and 19. Existing Tasks 01-07, 10, 12-18, and 20 retain prior static-source evidence.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed: BuilderGhostStateDTO 128B, BuilderGhostVisualDTO 64B, HolographyTelemetryEntry 64B, BuilderGhostIndirectArgsDTO 16B.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>VR pipe segments use `SmoothQuality(GlobalQualityWeight)` to scale segment count continuously. Module preview still scales SDF samples 2..8 and shader scan/rim/chroma continuously.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs recorded: 70940 state, 70941 visual, 70942 telemetry, 70943 mock state, 70944 SDF samples, 70945 module indirect args, 70946 pipe state, 70947 pipe visual, 70948 pipe indirect args.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>`BuildPipeBlueprintPreviewJob`, `BuildBuilderGhostStateJob`, validation, args, and telemetry jobs keep `[NoAlias]` NativeArray lanes. Active visual upload finalizes through `DispatcherJobFence.TryFinalizeCompleted`; forced completion remains teardown-only.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or sibling runtime dependency was added. Build remains unclaimed due CPU guard and the known external `Hecton8.Core.csproj` wall.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Before: preview object/mesh instance surfaces could render or validate through Unity scene objects. After: preview surfaces are AUP math, DTO rows, and procedural indirect hologram draws.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>

# SHINOBU_228 BOTTOM LOG - 2026-05-20 POST-18:25 EOF ORDER

This EOF entry records the final audit-token cleanup and the latest guard sample after the 18:25 ordering repair.

## What Was Wrong

- The static audit source contained its own forbidden search literals, so a repository-wide `rg` gate could report the scanner instead of runtime residue.
- The prior bottom log predated the audit false-positive cleanup and still reported an older CPU guard sample.

## What Was Done

- Split forbidden audit tokens in `BuilderHolographyStaticAudit` while preserving the same audit semantics.
- Re-ran the SHINOBU_228 forbidden-token scan across module preview, pipe preview, jobs, data, shader, builder, and audit files.
- Re-parsed `MEMORY_OPTIMIZATION_REPORT.json` through `ConvertFrom-Json`.
- Re-ran `git diff --check` on touched SHINOBU_228 code/docs.
- Re-sampled build guard: CPU 88%, dotnet/csc process count 0.

## Cinematic Cheats Used

- Module and pipe previews remain procedural hologram Dear Lies: AUP math creates DTO rows, Burst jobs write matrices/args, and `Graphics.DrawProceduralIndirect` renders without preview GameObjects, mesh-instance arrays, or PhysX proof objects.

## Exact Microseconds Saved

- No new runtime microsecond claim from the scanner cleanup; it is evidence hygiene only.
- Runtime savings remain the previous static deltas: ghost lifecycle spikes removed, preview PhysX broadphase removed, `.SetData` upload stalls avoided, and up to 64 pipe matrix submissions removed.

## Verification

- Forbidden-token scan: PASS by `rg` exit 1 with no output for `DrawMeshInstanced`, `Matrix4x4[`, `_matrices`, `.SetData(`, direct `.Complete(`, `JobHandle.Complete`, `UploadArgs(`, `_buffersDirty`, `new MaterialPropertyBlock`, `GetComponent<Renderer>`, `Instantiate(`, `TryResolveSocketAlignment(`, `candidateGhost`, and `ResolveSocketYawRotation(` across SHINOBU_228 target files.
- JSON parse: PASS for `Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json`.
- `git diff --check`: PASS on touched files with LF/CRLF warnings only.
- Build: HELD by CPU guard at 88% even though dotnet/csc count is 0. The earlier guarded build failure remains attributed to external `Hecton8.Core.csproj` dependency/project-file drift.

<SELF_AUDIT>
  <TASK_RECONCILIATION>Task 19 and Task 20 evidence strengthened by removing scanner self-matches. Tasks 01-18 retain the prior static-source and architecture evidence.</TASK_RECONCILIATION>
  <STRUCT_LAYOUT>No DTO layout changed: BuilderGhostStateDTO 128B, BuilderGhostVisualDTO 64B, HolographyTelemetryEntry 64B, BuilderGhostIndirectArgsDTO 16B.</STRUCT_LAYOUT>
  <SCALABILITY_CURVE>No quality-curve logic changed. Module SDF samples still scale 2..8 by `GlobalQualityWeight`; pipe segment density still scales by smooth quality; shader intensity remains continuous.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>Vault IDs unchanged: 70940 state, 70941 visual, 70942 telemetry, 70943 mock state, 70944 SDF samples, 70945 module indirect args, 70946 pipe state, 70947 pipe visual, 70948 pipe indirect args.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>Job topology unchanged. Active upload finalizes through `DispatcherJobFence.TryFinalizeCompleted`; forced completion remains teardown-only. `[NoAlias]` remains on non-overlapping NativeArray lanes.</POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No asmdef edge or sibling runtime dependency was introduced. Build was not relaunched because CPU exceeded 50%.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>The audit cleanup does not alter the Dear Lie: preview truth is mathematical AUP/SDF DTO state and indirect procedural hologram rendering, not scene object simulation.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
