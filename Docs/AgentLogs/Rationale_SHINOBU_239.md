# Rationale_SHINOBU_239

Date: 2026-05-20
Agent: SHINOBU_239
Status: PENDING VERIFICATION

## Decision 00 - Route Boundary
Problem: Visual degradation needs per-instance aging without material clones, decals, or gameplay-truth ownership.
Solution: Treat degradation as presentation-only GPU data derived from physical DTO snapshots during VISUAL_SYNC. Use a 32-byte explicit DTO, Burst compilation jobs, double-buffered GraphicsBuffer upload, and UberNoir shader consumption through a global StructuredBuffer.
Rejected Alternatives: Per-renderer Material.SetFloat, MaterialPropertyBlock, decal projectors, and spawned corrosion GameObjects. These break SRP Batcher/GPU Resident Drawer assumptions, add draw/hierarchy overhead, and create CPU-side mutation lanes.
Scalability potential: Low uses simple height/AO blend and throttled/noised scalar upload. Middle adds cheap hash noise. High adds triplanar noise modulation. Ultra spends saved CPU on richer GPU burn/rust/bio-fouling blends without adding gameplay truth.
Hardware Impact: i3/MX350 gain estimate is material-clone stall removal and zero extra draw calls; exact microseconds are PENDING PROFILER. Static estimate: 50-400 us CPU saved per 1000 damaged modules versus per-renderer material mutation, plus reduced SetPass pressure.

## Decision 01 - Authority Boundary
Problem: The task references GlobalDataVault and SystemDispatcher, but adding a new hot global service or polling Registry every render frame violates current authority law.
Solution: Do not add a broad GlobalRegistry slot unless existing code already exposes an owner interface. Prefer owner-local renderer feature setup with cached handles/interfaces and explicit cold injection hooks. Any unresolved Vault integration remains fail-closed with documented route-card requirement.
Rejected Alternatives: Runtime calls to GlobalDataVault.TryGetLatestCreated(), per-frame GlobalRegistry queries, or new catch-all rendering signal lanes.
Scalability potential: Low/middle/high/ultra all use the same DTO layout and route; only counts/cadence/shader work scale by continuous GlobalQualityWeight.
Hardware Impact: Avoids hidden per-frame global lookup and buffer growth costs on low-end silicon. Exact gain PENDING PROFILER.

## Decision 02 - Reuse Existing Aging Owner
Problem: A previous VisualPressureAgingRuntime already owned the base aging route; adding a separate SHINOBU_239 runtime would create two owners writing conflicting shader degradation facts.
Solution: Extend the existing GraphicsMaterials-owned runtime with the exact 32-byte `InstanceDegradationDTO`, new Vault buffer IDs 71247-71249, and `_GlobalUberNoirDegradation` upload while preserving the existing 64-byte UberNoir aging buffer for compatibility.
Rejected Alternatives: New RenderFeature/service with parallel buffers, direct renderer/material mutation, or deleting the 64-byte path before shader compatibility proof.
Scalability potential: Low uses one 32-byte scalar payload and cheap shader blend; Middle adds stable noise; High uses texture-array scorch/rust/bio slices; Ultra layers normal perturbation and richer noise without changing the CPU DTO.
Hardware Impact: i3/MX350 saves CPU by keeping one global buffer bind and no per-renderer mutation. Added upload is 32B * active instance count; 4096 instances = 128 KiB streamed with `LockBufferForWrite`.

## Decision 03 - Thermal Source Fallback
Problem: The prompt names `ThermalCellDTO`, but `Hecton8.Graphics.Materials.asmdef` has no legal reference to `Hecton8.Thermodynamics`; taking that DTO directly would create a sibling-runtime compile-wall edge.
Solution: Read the existing `ThermodynamicsTemperatureFrontMirror` float Vault lane through cached generation descriptors, then fall back to the mock temperature buffer. All reads occur under dispatcher/Vault locks.
Rejected Alternatives: Direct `ThermalCellDTO` dependency, direct thermodynamics asmdef reference, hard failure when Agent 117 cells are absent, or scene search for thermal objects.
Scalability potential: Low/middle use one nearest thermal scalar; High/Ultra can improve thermal indexing later without changing `InstanceDegradationDTO` ABI.
Hardware Impact: Avoids scene queries and managed allocation. Static estimate: 10-40 us avoided per sync versus resolving thermal components.

## Decision 04 - GPU Dear Lie
Problem: CPU knows only module-level integrity, not exact rust/scorch placement on mesh triangles.
Solution: CPU uploads scalar truth-adjacent presentation data; UberNoir first accepts `SeedFadeFlags.w = degradationIndex + 1` from the renderer instance buffer when a producer exists, otherwise uses a bounded `SV_InstanceID` fallback matching the original assignment route. Both paths load `_GlobalUberNoirDegradation` by `degradationIndex` and grow rust/scorch/bio through localized material coordinates, atlas slices, and continuous quality-scaled noise.
Rejected Alternatives: CPU-generated decal positions, mesh deformation, or per-instance material atlas offsets.
Scalability potential: Low path darkens/blends linearly; Middle uses one noise term; High uses atlas slices; Ultra adds scorch normal perturbation and hot-edge tint.
Hardware Impact: Removes draw-call and hierarchy overhead. GPU ALU cost scales continuously; CPU cost is a bounded memcpy.

## Decision 05 - Black Box and Rollback Fence
Problem: Visual degradation must be diagnosable but must not affect gameplay rollback/Merkle truth.
Solution: Added `DegradationTelemetryEntry` ring in the Vault and dump path `Docs/AgentLogs/Dump_SHINOBU_239.bin`; kept degradation under GraphicsMaterials BufferIDs with `FlagNoRollbackState` and no Networking/SaveSystem references.
Rejected Alternatives: Hashing visual buffers into rollback or relying on chat/debug logs after NaN.
Scalability potential: All tiers retain 300-frame forensic state; telemetry detail is fixed-size and independent of active instance count.
Hardware Impact: Fixed 19.2 KiB telemetry ring plus one cursor. No per-frame managed allocation.

## Decision 06 - Verification Gate
Problem: Project rules forbid `dotnet build` when CPU load is above 50%, and current CPU guard returned 100% twice.
Solution: Defer compile/Unity import proof, record the block, and rely only on static source checks in this pass.
Rejected Alternatives: Running build anyway and worsening contention with 20+ agents.
Scalability potential: Verification route is unchanged; next pass should compile when CPU drops.
Hardware Impact: Prevented build contention on already saturated host.

## Decision 07 - Descriptor Refresh Fence
Problem: The first pass still refreshed external Vault generation descriptors from normal dispatcher phases, creating hot-path descriptor churn risk.
Solution: Keep `RefreshExternalInputHandles` in cold/editor paths only. Hot phases now call `MarkExternalGenerationRefresh`, which checks cached generation staleness and fails closed until a cold/editor refresh.
Rejected Alternatives: Per-frame `TryGetGenerationHandle` recovery or runtime `TryGetLatestCreated` fallback.
Scalability potential: All tiers keep the same owner route; quality affects cadence/detail only, not descriptor ownership.
Hardware Impact: Avoids hot descriptor work and global memory contention. Static estimate: 2-10 us per visual-sync frame on low-end silicon, pending profiler.

## Decision 08 - Black Box Version 2
Problem: The first version-2 black-box fix copied the Vault scratch image into raw `UnsafeUtility.Malloc(..., Allocator.Persistent)` memory before file write. That avoided holding Vault locks during file IO, but it created an unsentinelized native allocation in a fault path and split the proof story from the SHINOBU_219 preserved owner route.
Solution: Keep the preserved owner dump and SHINOBU_239 mirror. Write the 32-byte header plus both 300-entry 64-byte telemetry rings into the Vault-owned scratch lane, then write that same scratch image to `Dump_SHINOBU_219.bin` and `Dump_SHINOBU_239.bin` only after a layout/upload/NaN fault. No raw native clone, no managed byte array, no active-frame allocation.
Rejected Alternatives: Raw persistent `UnsafeUtility.Malloc` clone, managed `byte[]` clone, leaving SHINOBU_239 without a dump mirror, or adding a new async writer service without a dispatcher phase/route card.
Scalability potential: Fixed diagnostic payload across low/middle/high/ultra; no steady-state frame cost. Fault-only file IO may hold Vault locks longer, but only after a diagnostic fault flag and once per dump path.
Hardware Impact: Fault-only 38,432-byte image per dump path. Runtime steady-state cost remains zero beyond existing telemetry writes and Vault scratch reuse.

## Decision 09 - WriteOnly and ReadOnly Ownership Closure
Problem: Writer jobs exposed output NativeArrays without `[WriteOnly]`, and editor snapshot methods returned mutable `NativeArray<T>` views into Vault-owned buffers.
Solution: Mark writer output lanes `[WriteOnly, NoAlias]`; compute mock degradation from local scalars; expose editor/gizmo snapshots as `NativeArray<T>.ReadOnly`.
Rejected Alternatives: Mutable editor views, read-after-write in mock job, or copying snapshots to managed/editor arrays.
Scalability potential: Same ABI and route for every tier; improved Burst alias clarity.
Hardware Impact: Removes one mock-row reread and strengthens vectorization proof. Static estimate: 3-12 us per 4096 mock rows, pending Burst Inspector.

## Decision 10 - Continuous Cadence Gate
Problem: A rounded modulo cadence made update frequency step across quality values.
Solution: Replace cadence modulo with deterministic frame-hash probability from `GlobalQualityWeight`, preserving continuous expected update rate from 5 Hz to 60 Hz without runtime randomness.
Rejected Alternatives: Binary low/high cadence switch, modulo-only cadence, or `UnityEngine.Random`.
Scalability potential: Low sheds update work probabilistically, middle fills in smoothly, high/ultra converge toward every-frame upload without changing DTO layout.
Hardware Impact: O(1) integer hash and one float compare per frame; smoother thermal shedding than modulo cadence.

## Decision 11 - Dual Owner Proof Boundary
Problem: Final ledger/source reconciliation showed `VisualPressureAgingRuntime` is the preserved SHINOBU_219 visual-aging owner route, but SHINOBU_239 owns the new UberNoir texture-degradation task and must still emit `Dump_SHINOBU_239.bin`.
Solution: Preserve SHINOBU_219 `SystemHash`, primary dump, editor report identity, and cold owner comments. Add a SHINOBU_239 degradation dump mirror and a separate `Visual_Material_Inquisition` report facade for the new degradation link proof.
Rejected Alternatives: Renaming the whole runtime to SHINOBU_239, creating a second runtime owner writing the same shader buffers, or leaving SHINOBU_239 without a black-box proof artifact.
Scalability potential: Same low/middle/high/ultra runtime path; only forensic/report identity is split. DTO layout, quality curve, shader route, save identity, and rollback exclusion remain unchanged.
Hardware Impact: No active-frame cost. Fault-only second disk write shares the same Vault scratch image and avoids extra telemetry rings or unmanaged clone allocation.

## Decision 12 - Mutating API Name Closure
Problem: Schedule-time helpers named `TryResolve*` were not pure reads; they locked Vault buffers or bound job-write lanes. That violates the read-accessor doctrine even if the code path was technically controlled.
Solution: Rename schedule mutators to `AcquireThermalInputForSchedule`, `AcquireStructuralInputsForSchedule`, `AcquireStructuralTuningForSchedule`, `BindLockedJobBuffersForSchedule`, and cold boot allocation to `EnsureVaultBufferForInit`.
Rejected Alternatives: Keeping read-looking names, hiding mutation behind `TryResolve*`, or widening the GlobalRegistry path.
Scalability potential: Naming does not alter low/middle/high/ultra runtime math; it prevents future agents from treating lock/acquire helpers as pure reads.
Hardware Impact: No frame-time gain claimed. The value is concurrency correctness and lower risk of accidental hot-path polling.

## Decision 13 - Editor Snapshot Player Guard
Problem: The read-only snapshot APIs were editor tools but callable in player builds, where they could lock Vault buffers from non-owner presentation code.
Solution: Keep the public signatures for editor source compatibility but compile the body to an immediate `false`/no-op outside `UNITY_EDITOR`. Editor gizmos and the tuner still receive `NativeArray<T>.ReadOnly` snapshots.
Rejected Alternatives: Player-side snapshot locks, copying snapshots into managed arrays, or deleting editor APIs and breaking the tuner.
Scalability potential: Runtime tiers are unaffected; editor visualization remains capped and read-only.
Hardware Impact: Player builds now pay zero snapshot-lock cost. Editor-only cost remains bounded by the fixed gizmo/tuner sample caps.

## Decision 14 - Static Inquisition Regression Gates
Problem: The SHINOBU_239 validator could prove the shader buffer route but did not fail regressions for raw fault dump cloning, read-looking schedule helper names, missing editor snapshot guards, or gizmo preview bypassing `InstanceDegradationDTO`.
Solution: Extend `Visual_Material_Inquisition` with counters for `UnsafeUtility.Malloc/Free`, stale `TryResolve*` schedule helper names, editor snapshot preprocessor guards, and component gizmo degradation DTO references.
Rejected Alternatives: Manual grep in chat only, relying on SHINOBU_219 inquisition identity, or leaving future agents without a source-level regression tripwire.
Scalability potential: No runtime quality effect. The validator protects the low/middle/high/ultra route from reverting to CPU/decal/material mutation patterns.
Hardware Impact: Editor-only source scan. No player frame cost.

## Decision 15 - Burst Upload Copy Without Schedule/Complete Loop (Superseded)
Problem: The upload lane used `LockBufferForWrite` and a raw `UnsafeUtility.MemCpy`, but Task 09 explicitly requires a Burst copy kernel for GPU buffer upload. Scheduling a tiny copy job and calling `.Complete()` before `UnlockBufferAfterWrite` would violate dispatcher discipline.
Solution: The initial patch kept the existing one-frame mapped buffer window and ran `CopyVisualAgingUploadJob.Run()` / `CopyDegradationUploadJob.Run()` synchronously over the mapped `NativeArray` views. Both kernels were `[BurstCompile(CompileSynchronously=true, FloatMode=Fast, FloatPrecision=Standard)]` and used `[ReadOnly/WriteOnly, NoAlias]`. This implementation was later overwritten out of the shared runtime owner file; current source truth is recorded in Decision 21 and Task 09 remains blocked by dependency.
Rejected Alternatives: Main-thread C# copy body only, scheduled same-frame upload job plus hidden `.Complete()`, or synchronous `ComputeBuffer.SetData`.
Scalability potential: Low/middle/high/ultra keep the same DTO layout and buffer route; quality still scales count/cadence/shader detail, not upload authority.
Hardware Impact: No profiler claim. The attempted change would have kept copy cost bounded to contiguous memcpy under Burst and avoided an extra scheduled fence, but that proof is not present in the current runtime source.

## Decision 16 - Stable Degradation Index Fence
Problem: Shader-side degradation sampling used the renderer instance/material index, while the CPU uploads degradation rows by structural node order. Source search found no active C# producer for `_H8UberNoirInstanceData` / `SeedFadeFlags.w`, so a strict fail-closed stable-index-only route would hide SHINOBU_239 degradation in the current tree.
Solution: Keep the stable renderer-provided index when available, but add a bounded `SV_InstanceID` fallback inside `H8UberNoirResolveDegradationIndex(instanceData, resolvedInstanceID)`. The fallback is capacity-bounded and still rejected by the active-count guard before any `_GlobalUberNoirDegradation[degradationIndex]` read.
Rejected Alternatives: Strict fail-closed output with no visible degradation, unbounded `SV_InstanceID` indexing, CPU decal/object mapping, or scene search pairing renderers and structural nodes.
Scalability potential: All tiers share the same route. Low tiers use the bounded legacy instance row; future high/ultra renderer owners can feed exact `SeedFadeFlags.w` indices without changing DTO layout or shader buffer ABI.
Hardware Impact: One finite check plus one bounds branch in the vertex path. No microsecond gain claimed; it prevents out-of-range buffer reads while keeping the assigned-instance fallback alive.

## Decision 17 - Shader AUP Locality Closure
Problem: `H8UberNoirMaterialStablePosition` subtracted `_TotalUniverseOffset` in shader space, reintroducing a global-offset accumulation route after CPU had already localized AUP data.
Solution: Remove `_TotalUniverseOffset` from the stable-position helper. Degradation and aging noise keep using CPU-localized `DepthAndPressure.xyz` plus UV/material-space seeds.
Rejected Alternatives: Global double/float world subtraction in the shader, absolute world-position noise, or CPU mesh/decal placement to avoid shader noise.
Scalability potential: Low keeps cheap deterministic triangle/noise masks; middle/high/ultra layer richer texture/noise from the same local seed without changing DTO layout.
Hardware Impact: Removes one float3 subtraction and one finite fallback from the helper. No profiler claim; primary impact is large-world jitter avoidance.

## Decision 18 - Dispatcher-Owned Post-Simulation Fence
Problem: A local `_scheduledSimulationHandle.Complete()` in `PostSimulationTick` would duplicate the core dispatcher fence and violate the project rule against domain-owned hidden completes in gameplay phases.
Solution: Leave completion ownership with `SystemDispatcher.RunMasterPostSimulationPhase`, which force-completes the combined simulation handle inside the post-simulation swap window before domain post-simulation systems run. `VisualPressureAgingRuntime.PostSimulationTick` only checks `_scheduledSimulationHandle.IsCompleted`, unlocks Vault buffers, and clears its local handle metadata.
Rejected Alternatives: Domain-local `Complete()`, hidden same-frame upload completes, arbitrary mid-frame blocking, or unlocking before the dispatcher has reached the completed state.
Scalability potential: No quality-tier behavior changes. Dispatcher phasing remains the authority fence across all devices.
Hardware Impact: No additional scheduler bookkeeping in the graphics domain; completion cost remains centralized in the already-measured dispatcher swap window.

## Decision 19 - Dual CSV Bridge Without SHINOBU_219 Theft
Problem: SHINOBU_219 owns the preserved base `environmental_aging_rules.csv` route, while SHINOBU_239's XML requires `environmental_degradation_rules.csv` and the existing degradation file lacked bridge metadata.
Solution: Preserve the aging CSV path and add `UberNoirDegradationCsvBridge`, an editor-only byte parser that loads the degradation CSV and applies the parsed tuning through `VisualPressureAgingRuntime.TryWriteEditorTuning`. Add schema hash, checksum, DataMonolith output path, validation report, DTO size, field order, BufferID 71247, and generation policy metadata to the degradation CSV.
Rejected Alternatives: Repointing the entire preserved SHINOBU_219 route to the SHINOBU_239 file, deleting the degradation CSV, editing private runtime fields in a parallel-owner fight, or parsing rows with managed string splitting.
Scalability potential: Low/middle/high/ultra consume the same tuning DTO; quality only scales cadence and shader detail.
Hardware Impact: Cold editor reload only. No active-frame cost.

## Decision 20 - Concurrent Drift Watch
Problem: A parallel SHINOBU_219 pass reverted `_degradationCsvPath` to `_csvPath` after the SHINOBU_239 overlay was restored, creating a silent Task 17 route loss. The same drift was observed repeatedly during final readbacks.
Solution: Stop fighting the private runtime field and move SHINOBU_239 CSV ingestion to the editor bridge through the public tuning API. Keep the runtime primary owner route untouched.
Rejected Alternatives: Renaming the primary runtime owner to SHINOBU_239, deleting SHINOBU_219's aging CSV route, continuing a write-war over `_degradationCsvPath`, or leaving the degradation CSV as an inactive artifact.
Scalability potential: No tier behavior changes; this preserves the cold tuning bridge used by every quality level.
Hardware Impact: No active-frame cost. The watch is static file hygiene only.

## Decision 21 - Upload Kernel Drift Gate
Problem: A delayed readback showed the runtime upload copy kernels had been overwritten back to direct helper-body `UnsafeUtility.MemCpy`, while Status/LOG still claimed Burst upload copy jobs. That is a proof drift and a Task 09 regression.
Solution: Attempted to restore `CopyVisualAgingUploadJob` and `CopyDegradationUploadJob` as immediate `.Run()` Burst copy kernels over the `LockBufferForWrite` mapped buffers. Both attempts were overwritten by a parallel runtime owner after delayed readback, so the current source is marked `[BLOCKED BY DEPENDENCY]` for Task 09. `Visual_Material_Inquisition` keeps `burstUploadCopyKernelProof` so the static report fails visibly until the owner collision is resolved.
Rejected Alternatives: Continuing a shared-file write-war, scheduling tiny upload jobs and forcing `.Complete()` before `UnlockBufferAfterWrite`, or treating stale log text as proof.
Scalability potential: Low/middle/high/ultra keep the same DTO layout and upload route. Quality still scales cadence and shader detail, not buffer authority or save identity.
Hardware Impact: No profiler claim. Current source still avoids `SetData`, `.Complete()`, and raw fault allocation, but the explicit Burst upload-copy proof is blocked. Build proof also remains blocked by CPU guard.

## Decision 22 - Subagent Owner Boundary Audit
Problem: Task 09 still needed a clear technical answer: whether SHINOBU_239 could satisfy the Burst upload-copy requirement by adding a separate bridge file, avoiding the contested SHINOBU_219 runtime owner.
Solution: Explorer `019e4793-0dda-7da2-b2eb-074b8b28d1a0` audited the route and confirmed the only active GPU upload call sites are `VisualSyncTick -> UploadNativeArray/UploadDegradationNativeArray` inside `VisualPressureAgingRuntime.cs`. A separate editor bridge or standalone job declaration cannot affect the mapped-buffer copy. The least-invasive valid implementation still requires editing those shared call sites, while SHINOBU_219 docs already record removal of these tiny synchronous upload jobs. Updated the binary payload ledger to replace the stale PASS with current source truth and blocked-state language.
Rejected Alternatives: Adding dead copy jobs in a separate file with no call-site use, doing a third write-war patch into the shared owner, or leaving the ledger claiming copy jobs exist.
Scalability potential: Low/middle/high/ultra behavior is unchanged. The active path still scales cadence and shader work continuously; Task 09's missing Burst wrapper does not change DTO layout, save identity, rollback identity, or authority route.
Hardware Impact: Static audit only. No new frame-time claim; current source still avoids `SetData`, hidden `.Complete()`, and raw fault allocation.

## Decision 23 - Inquisition Copy-Kernel Declaration Scope
Problem: The least-invasive valid Task 09 patch can place `CopyVisualAgingUploadJob` and `CopyDegradationUploadJob` in a separate non-editor runtime file, but the existing inquisition scanned only `VisualPressureAgingRuntime.cs` for the declarations. That would reject a valid conflict-minimized integrator patch.
Solution: `Visual_Material_Inquisition` now builds a non-editor `Graphics/Materials` runtime-source scan for copy-kernel declarations while still requiring the actual `new CopyVisualAgingUploadJob` and `new CopyDegradationUploadJob` `.Run()` call sites in `VisualPressureAgingRuntime.cs`. Current source still fails because neither the declarations nor active call sites exist.
Rejected Alternatives: Forcing job structs into the contested owner file, relaxing the validator enough for dead standalone job declarations to pass, or removing `burstUploadCopyKernelProof`.
Scalability potential: No runtime behavior change. This is an editor-only proof gate that preserves the same low/middle/high/ultra data route.
Hardware Impact: Editor-only source scan allocation. No player frame cost and no new runtime claim.

## Decision 24 - Explicit Task09 Validator Status
Problem: A generic `STATIC_FAIL` report forces the integrator to infer whether the failure is material/decal regression, missing shader buffer, or the known upload-copy owner collision.
Solution: `Visual_Material_Inquisition` now emits `runtimeMemCpyReferences`, `task09Status`, `uploadCopyCallSiteScope`, and `uploadCopyDeclarationScope`. If `burstUploadCopyKernelProof` is false, the report states `BLOCKED_BY_DEPENDENCY` for Task 09 while still exposing the exact active call-site/declaration scope contract.
Rejected Alternatives: Leaving only a boolean proof, hiding direct helper-body memcpy count, or weakening the static pass to ignore Task 09.
Scalability potential: No runtime behavior change. The report improves handoff across low/middle/high/ultra route ownership without changing DTO layout or shader math.
Hardware Impact: Editor-only source scan output. No player frame cost.

## Decision 25 - Forbidden Pattern Sweep
Problem: After multiple owner-collision corrections, stale source could still hide standard Unity patterns in the SHINOBU_239 slice.
Solution: Reran scoped scans over `VisualPressureAgingRuntime.cs`, `Visual_Material_Inquisition.cs`, `UberNoirDegradationCsvBridge.cs`, `VisualPressureAgingTunerWindow.cs`, and `Hecton8_UberNoir.hlsl`. Runtime slice has no `Pack=1`, DTO auto-properties, runtime `SetData`, hidden `.Complete`, `TryGetLatestCreated`, Unity/System random, LINQ, persistent private NativeCollections, direct Thermodynamics/Networking/Save/Merkle refs, or runtime material mutation. Remaining `.material`/`MaterialPropertyBlock`/`SetData` hits are editor scanner literals.
Rejected Alternatives: Relying on prior Status claims or ignoring scanner literals as if they were hot-path code.
Scalability potential: No behavior change. This protects the continuous quality route and shader-buffer ownership from regressing into material/decal/object paths.
Hardware Impact: Static proof only. No new frame-time claim.

## Decision 26 - Dedicated Report Artifact
Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` currently belongs to SHINOBU_235, and the SHINOBU_239 Unity editor inquisition has not run because build/import verification is blocked by CPU guard.
Solution: Create `Docs/Reports/UBERNOIR_DEGRADATION_INQUISITION_REPORT.json` as a dedicated static evidence artifact with current source truth, including `task09Status=BLOCKED_BY_DEPENDENCY`, no runtime `SetData`, no hidden `.Complete`, no raw fault allocation, DTO layout, shader proof booleans, and CPU guard state. Leave the shared rendering report untouched until the editor menu writer can run inside Unity and preserve prior report content through its existing `previousReportRaw` path.
Rejected Alternatives: Overwriting SHINOBU_235's shared report manually, claiming the editor report ran, or leaving SHINOBU_239 without a concrete report artifact.
Scalability potential: No runtime behavior change. This is reporting hygiene for the same continuous shader-buffer route.
Hardware Impact: Documentation artifact only. No player frame cost.

## Decision 27 - Mandate/Prompt Revalidation And Stale Log Hygiene
Problem: A strict XML extraction command failed because the `SHINOBU_239` tag carries `role` and `chat_name` attributes, and older chronological LOG sections still described broad SHINOBU_239 runtime identity takeover attempts before the later dual-proof boundary corrected them.
Solution: Re-extract `SHINOBU_239` using a wildcard tag regex and verify `TASK_COUNT=20`. Reread the relevant zero-GC, ARM64, GPU, shader-noir, AUP, registry, and native-memory mandates. Rerun live source truth for runtime upload, shader index/AUP, CSV bridge, validator, asmdef, and dedicated report fields. Patch only `LOG_SHINOBU_239.md` section headings/lines so superseded identity-swap attempts cannot be mistaken for current architecture.
Rejected Alternatives: Trusting compressed chat memory, treating the failed exact-tag regex as missing assignment, editing the shared runtime owner again, or deleting chronological log history instead of labeling it.
Scalability potential: No runtime behavior change. The correction protects the same continuous quality route from documentation drift and preserves SHINOBU_219/SHINOBU_239 ownership boundaries.
Hardware Impact: Documentation/static-audit only. No player frame cost and no new profiler claim.
