# GPU_SCATTER_LOD_MANAGER Rationale

Status: PENDING FINAL VALIDATION - ASSEMBLY METADATA BLOCKED

## 2026-05-16 Initialization

Problem: Procedural flora must render 100k instances without GameObjects, CPU-side transforms, or runtime Instantiate debt.

Solution: Use a rendering-domain indirect path centered on `Graphics.RenderMeshIndirect`, persistent `GraphicsBuffer` storage, `GraphicsBuffer.CopyCount` for visible instance counts, and deterministic camera/AUP culling. Keep data provider boundaries explicit so OSHINO/vault ownership can be attached without hard dependencies.

Rejected Alternatives: `Instantiate(KelpPrefab)` and `FloraManager.Instance` are rejected by prompt. `DrawMeshInstancedIndirect` is rejected by prompt and mandate. Stable MeshRenderer GPU Resident Drawer is not usable for pure procedural matrix data that has no GameObject ownership.

Scalability potential: Low uses 100m maximum cull distance and static cheap flora; Middle extends residency; High uses 500m cull distance and crossfade fields; Ultra spends saved CPU on longer visual residency and richer shader motion.

Hardware Impact: Target gain on i3/MX350 is removal of per-flora GameObject transform and renderer overhead. Exact microseconds remain PENDING VERIFICATION until build/profile capture exists.

## 2026-05-16 Loop 1: Vault + AUP Cull

Problem: OSHINO flora matrices need a stable cross-agent handoff without inventing a direct renderer-to-generator dependency.

Solution: Added DataVault IDs for flora scatter matrices, metadata, and motion vectors. `GpuScatterLodManager` resolves `IDataVault` through cached GlobalRegistry hot-swap hooks and reads `NativeArray<Matrix4x4>` from `BufferID.FloraScatterMatrices`. Public `PublishVaultInstanceRange` and `MarkVaultDirty` let a producer signal count/dirty state without exposing a concrete OSHINO class.

Rejected Alternatives: A direct `OshinoLSystemGenerator` field was rejected because 20+ agents are editing in parallel and concrete cross-domain references will break. A `List<Matrix4x4>` or GameObject hierarchy was rejected because it creates GC/Transform debt and violates the no-GameObject directive. A `DrawMeshInstancedIndirect` path was rejected because the prompt explicitly requires `Graphics.RenderMeshIndirect`.

Scalability potential: Low uses 100m and cheap shader defaults. Middle has a 250m residency lane. High uses 500m cull distance and crossfade material data. Ultra can keep the 500m residency and spend the saved CPU on denser shader motion/biolum payloads.

Hardware Impact: Estimated i3/MX350 gain is 900-1800us CPU by removing 100k Transform/GameObject submission work. PCIe bandwidth is controlled by DataVault generation/dirty upload and double-buffered `LockBufferForWrite`; unchanged matrices are not re-uploaded.

Problem: AUP-origin shifts can make absolute flora matrices cull against the wrong frustum if the offset is applied after culling.

Solution: Both the compute `ScatterCullJob` and Burst audit job add `AupShiftOffset` before distance/frustum culling. Rendering keeps stable source matrices and binds `_GlobalFloatingOffset`, preserving stable AUP seeds while drawing at runtime origin.

Rejected Alternatives: Rebasing all 100k matrices on CPU during every origin shift was rejected because it rewrites about 6.4MB and risks a frame spike. Ignoring AUP until the shader was rejected because visibility would diverge from rendering.

Scalability potential: Toaster path avoids CPU rebake and just shifts cull math. $5000 path keeps stable AUP genetic hashes and can use longer high-tier residency without origin jitter.

Hardware Impact: Estimated shift-spike avoidance is 150-400us per origin shift on i3/MX350, plus avoided 6.4MB CPU-to-GPU upload unless the producer actually dirties matrices.

Problem: Prompt asked for Burst `ScatterCullJob`, but the shipping hot path must not spend CPU culling 100k flora every frame.

Solution: Implemented a Burst `ScatterCullJob` as a diagnostic audit path and a GPU compute kernel with the same name for the shipping cull. Both use six planes and `math.dot`/HLSL `dot` against transformed OBB bounds.

Rejected Alternatives: Scheduling and completing a CPU cull every `Tick` was rejected as a direct violation of the 0.1ms suspicion rule. Sphere-only culling was rejected because the prompt required bounding-box culling.

Scalability potential: Low never enables the CPU audit. High/Ultra can enable the audit during validation captures without changing the render path.

Hardware Impact: Audit is off by default; hot-path CPU cost is expected near dispatch setup only. Exact GPU time remains PENDING VERIFICATION until Unity/RenderDoc capture.

## 2026-05-16 Loop 2: Visible Streams + LOD Tiers

Problem: The prompt explicitly required visible matrices in an append buffer, while the existing Hecton vegetation shader expects source matrices plus visible source indices.

Solution: The compute kernel appends both `_HectonScatterVisibleMatrices` and `_HectonScatterVisibleIndices`. `GraphicsBuffer.CopyCount` uses the visible-matrix append buffer as the source of indirect instance count, while the material keeps the visible-index buffer for compatibility with the existing indirect flora shader.

Rejected Alternatives: Index-only append was cheaper but rejected because it would not literally satisfy the visible-matrix task. CPU compaction into a visible matrix array was rejected because it would reintroduce 100k-loop CPU upload pressure.

Scalability potential: Low still pays only for visible instances after frustum/distance rejection. High/Ultra can consume the visible-matrix stream for future visual overkill passes without altering OSHINO or the shader-facing source-index path.

Hardware Impact: Avoids about 6.4MB/frame CPU compact/upload at 100k by doing append on GPU. GPU append bandwidth increases when many instances are visible; this is an intentional prompt-compliance cost and remains PENDING VERIFICATION in RenderDoc.

Problem: Camera flow must be decoupled from scene object searches while still producing exact cull planes.

Solution: Consumed `CameraFrustumSignal` through `SignalBus<CameraFrustumSignal>`. If a serialized Camera exists, Unity plane extraction is used; if not, fallback planes are built from the signal position/forward/up/FOV/near/far payload.

Rejected Alternatives: `Camera.main`, `FindObjectOfType<Camera>`, and per-frame registry camera queries were rejected for GC/lookup risk. Blindly requiring a Camera component was rejected because the signal lane is the architectural contract.

Scalability potential: Low can run with signal-only planes and no camera dependency. High can bind an explicit camera for exact Unity frustum parity.

Hardware Impact: Estimated 5-20us avoided versus repeated camera lookup and zero GC in the render tick. Exact number remains PENDING VERIFICATION.

Problem: Flora residency must be cheap on MX350 and visually overbuilt on strong hardware without flickering cull switches.

Solution: Low/MX350 cull distance is 100m. High/Ultra is 500m and writes a crossfade range to the material. Cull-distance changes use a 5m/2s hysteresis gate.

Rejected Alternatives: A balanced single middle distance was rejected by the scalability pillar. Immediate tier flipping was rejected by the state-hysteresis mandate.

Scalability potential: Low = 100m cheap dressing. Middle = 250m residency. High = 500m with crossfade. Ultra = 500m plus future shader overkill from metadata/motion buffers.

Hardware Impact: Low-tier far-field rejection should save shader and append work in dense fields. Exact microseconds depend on flora density and remain PENDING VERIFICATION.

## 2026-05-16 Loop 3: Stability + Homeostasis

Problem: Flora sway needs stable motion data without per-instance CPU animation.

Solution: `GpuScatterLodCull.compute` writes `_HectonScatterMotionVectors` per visible source index using deterministic hash-based sway direction and the current frame index. The renderer binds this buffer as `_HectonFloraMotionVectors` for shader consumption.

Rejected Alternatives: CPU-side sine/sway integration was rejected because 100k per-frame transforms are the original failure mode. Physics-based blade motion was rejected by the Cinematic Cheat Protocol; a deterministic visual fake is enough.

Scalability potential: Low can use the vector as a cheap scalar wobble. High/Ultra can use it for richer motion-vector or VAT blending without changing CPU code.

Hardware Impact: Avoids 100k CPU animation updates; estimated saving is 300-900us on i3/MX350 depending on previous Transform workload. Exact measurement remains PENDING VERIFICATION.

Problem: One bad OSHINO matrix can poison culling or rendering with NaN/INF.

Solution: C# upload validates finite matrix rows before GPU upload and dumps `Docs/AgentLogs/Dump_GPU_SCATTER_LOD_MANAGER.bin` on non-finite data. Compute and Burst cull paths reject non-finite positions and zero-scale matrix axes before appending.

Rejected Alternatives: Trusting producer matrices was rejected because critical rendering systems require blackbox evidence. CPU fixing zero-scale matrices in place was rejected because renderer must not mutate producer geometry truth except metadata defaults.

Scalability potential: Low drops invalid instances cheaply. High/Ultra can keep strict validation enabled on dirty uploads without adding per-frame CPU scans.

Hardware Impact: Dirty-upload validation is O(changed active matrices), not per-frame when DataVault generation/dirty state is stable. Hot path matrix-scale guards are GPU-side.

Problem: The critical render path needs last-300-frame evidence and `VisibleFloraCount` without blocking the CPU.

Solution: Added a fixed `NativeArray<ScatterBlackBoxEntry>[300]`. Visible count is sampled from the indirect args buffer with async GPU readback every 60 frames and written into the ring with active count, cull distance, stress, camera, AUP, and vault generations.

Rejected Alternatives: Per-frame CPU readback was rejected because it stalls. `Debug.Log` was rejected because it allocates and is not blackbox telemetry.

Scalability potential: Low keeps the same diagnostic ring. High/Ultra can increase shader complexity while keeping the evidence channel constant.

Hardware Impact: Async readback cadence avoids blocking. Expected CPU overhead is below 20us amortized, PENDING VERIFICATION.

Problem: Homeostasis must shed visual cost under pressure without flickering LOD state.

Solution: The renderer consumes `SystemHealthSignal`, accepts external `SetSystemStress01`, and halves desired cull distance when stress exceeds 0.8. The 5m/2s hysteresis gate prevents immediate oscillation.

Rejected Alternatives: Reducing instance capacity by rewriting the source count was rejected because it would desynchronize producer ownership. Immediate hard distance changes were rejected by the state-hysteresis mandate.

Scalability potential: Low under pressure cuts 100m to 50m. High under pressure cuts 500m to 250m while preserving high-tier shader features for retained near flora.

Hardware Impact: Savings are density-dependent. Worst-case far-field draw/append work should roughly halve in radius-gated dense scenes, PENDING VERIFICATION.

Problem: Full `dotnet build` cannot reach a clean verdict because the current baseline has missing core contract types unrelated to this scatter implementation.

Solution: Ran full and isolated build attempts, then filtered `Hecton8.Core.csproj` output for `GpuScatter`/`FloraScatter` to isolate this change. No scatter-specific errors surfaced before the baseline dependency wall.

Rejected Alternatives: Reverting the scatter work was rejected because there is no evidence this work caused the missing `ISimulationBucketer`, `IMacroDatabaseService`, `IPlayerMovementContracts`, `H8WorldPageReadTicket`, or related core contract errors. Stub-inventing those contracts was rejected as cross-domain sabotage.

Scalability potential: Not applicable to runtime visuals; this is an integration blocker.

Hardware Impact: 0us runtime. Integrator must restore baseline compile dependencies before final validation can be authoritative.

## 2026-05-16 Loop 4: Indirect Args + Lifetime

Problem: Indirect drawing needs the GPU-visible count without a CPU readback stall.

Solution: The visible matrix append buffer is the source of truth for `GraphicsBuffer.CopyCount`, copied directly into the indirect args instance-count slot. Async readback exists only for blackbox telemetry cadence, not draw submission.

Rejected Alternatives: `GetData`, synchronous `AsyncGPUReadback.WaitForCompletion`, or CPU-visible counters were rejected because they stall or move count ownership back to the CPU.

Scalability potential: Low/High/Ultra all use the same indirect args path; stronger hardware spends extra budget on retained visible flora, not CPU submission.

Hardware Impact: Avoids a CPU/GPU sync point every frame. Estimated saved stall is 200-2000us depending on GPU queue pressure; exact measurement remains PENDING VERIFICATION.

Problem: Persistent GPU/native buffers can leak across scene unloads if not explicitly released.

Solution: `OnDisable` and `OnDestroy` release matrix double buffers, metadata double buffers, visible append buffers, motion vectors, args buffer, CPU audit arrays, and the 300-frame blackbox. Vault NativeArray leases are invalidated but not disposed because DataVault owns them.

Rejected Alternatives: Static process-lifetime buffers were rejected because scene unload must reclaim VRAM. Disposing DataVault buffers from the renderer was rejected because that violates Vault ownership.

Scalability potential: Low devices reclaim VRAM aggressively. High devices can reallocate full 100k buffers on scene entry without stale data crossing scenes.

Hardware Impact: Prevents about 20MB of renderer-owned GPU/native retention for 100k capacity: two matrix buffers ~12.8MB, two metadata buffers ~12.8MB, visible matrix append ~6.4MB, plus indices/motion/args. Exact driver allocation size remains PENDING VERIFICATION.

## 2026-05-16 Loop 5: Omega Self-Review

Problem: `<POLISH_MANDATE>` was absent from `Docs/Tasks/CURRENT_BATCH.md`, but the finished task still required anti-bloat review before report.

Solution: Ran focused self-review grep on `GpuScatterLodManager.cs` for `Camera.main`, scene searches, coroutines, Unity `Update` methods, LINQ/list/dictionary allocations, debug logs, readbacks, and shader keyword usage. Fixed the GPU indirect keyword to match the existing shader variant (`HECTON_GPU_INDIRECT`) and made fallback draw bounds scale to active cull distance so 500m high-tier residency is not culled by a stale 200m default bound.

Rejected Alternatives: Leaving the underscore keyword was rejected because the existing shader only compiles `HECTON_GPU_INDIRECT`. Keeping static fallback bounds was rejected because indirect draw bounds are a Unity culling authority and would undercut high-tier rendering.

Scalability potential: Low still bounds around 100m. High/Ultra now get bounds wide enough for 500m visible residency when no explicit producer bounds are supplied.

Hardware Impact: Keyword fix is correctness-only. Dynamic fallback bounds can increase renderer-level culling volume on high tier; explicit producer bounds remain preferred for tight culling. Runtime impact is PENDING VERIFICATION.

## 2026-05-16 Loop 6: Multiplatform + H-Phi Inquisition

Problem: The renderer still held private native telemetry/audit arrays after the first pass, which violates the Data Sovereignty rule and weakens H-Phi connectivity.

Solution: Moved scatter blackbox, CPU frustum audit planes, and CPU visibility audit mask behind `VaultBufferHandle<T>` allocations in GlobalDataVault using `BufferID.FloraScatterBlackBox`, `FloraScatterCpuFrustumPlanes`, and `FloraScatterCpuVisibilityMask`. Matrix and metadata source data are also held only as Vault handles. The remaining `NativeArray<T>` variables are transient views resolved from Vault or Unity GPU APIs for a single operation, not renderer-owned storage.

Rejected Alternatives: Keeping private `NativeArray<T>` fields was rejected as feudal renderer ownership. Using managed arrays for the audit path was rejected because it creates GC and breaks Burst job inputs. Disposing Vault memory from the renderer was rejected because DataVault is the owner.

Scalability potential: Low uses the same global memory ownership with 100m culling and cheap material constants. Middle keeps 250m residency. High/Ultra keep 500m residency and enable `_QUALITY_HIGH` plus stronger existing vegetation SSS, edge bloom, and local caustic lanes. Visor salt, hull dents, and marine snow wake are outside this renderer's domain and already belong to post/VFX/hull systems, so this pass only drives flora-owned overkill.

Hardware Impact: Native memory leak surface is reduced by removing renderer-owned audit/blackbox allocations. Runtime microseconds saved remain PENDING VERIFICATION because the build cannot produce a profiling player; expected win is fault/lifetime stability, not hot-path math.

Problem: Quest/Android ARM64 and Metal require deterministic struct layout and guarded GPU math; implicit padding or one bad `rsqrt` denominator can kill mobile rendering.

Solution: Added `Pack = 1` and fixed sizes to GPU metadata and 64B blackbox telemetry structs. The compute shader now returns explicit `float3(0.0, 0.0, 0.0)` for non-finite transforms and clamps/repairs the sway-axis length before `rsqrt`. Thread group size remains 64, below the Metal 1024 thread-group limit.

Rejected Alternatives: Relying on default CLR packing was rejected for Quest. Trusting `max(dot(axis, axis), eps)` alone was rejected because a NaN dot survives `max` on some shader backends. Increasing the compute group size was rejected because MX350/Metal compatibility matters more than theoretical occupancy.

Scalability potential: Toaster mode drops invalid instances and keeps the dear-lie sway vector. God-mode keeps the same safe kernel but spends budget through longer flora residency and richer shader lighting lanes instead of bigger CPU systems.

Hardware Impact: NaN-vaccination cost is one finite branch in the compute kernel. Expected cost is below measurable CPU time and protects the GPU pipeline from catastrophic invalid output; exact GPU microseconds remain PENDING VERIFICATION.

Problem: The build gate still cannot prove final compilation after the additional pass.

Solution: Ran isolated `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1`, full `dotnet build Assembly-CSharp.csproj --no-restore -m:1`, and filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1`. Isolated Assembly-CSharp stops on missing generated/plugin DLLs in `Temp/bin/Debug`; full Assembly-CSharp stops earlier on missing RealtimeCSG source files; Hecton8.Core stops on unrelated XR/submarine/fauna/VFX/audio compile errors. Filtered scans for `GpuScatter`, `Rendering/Scatter`, `FloraScatter`, `H8Memory`, and `BufferID` produced no scatter/BufferID-specific compiler errors.

Rejected Alternatives: Creating fake RealtimeCSG/plugin files or cross-domain stubs was rejected as architectural sabotage. Reverting the scatter changes was rejected because the observed errors are project dependency/source-file failures outside the assigned domain.

Scalability potential: Not runtime-relevant. Integration must restore the missing external/generated artifacts before player profiling and final dotnet validation can be authoritative.

Hardware Impact: 0us runtime. Validation remains blocked by dependency state, not scatter code.

## 2026-05-16 Loop 7: Constant Buffer + Draw State Isolation

Problem: The scatter compute kernel was still fed through fragmented per-frame scalar/vector uploads, including six separate frustum plane values. That violates the GPU compute mandate's constant-buffer packing rule and increases driver work before the dispatch.

Solution: Added `HectonScatterFrameConstants`, a fixed 176B `[StructLayout(LayoutKind.Sequential, Pack = 1)]` C# payload mirrored by `CBUFFER_START(HectonScatterFrameConstants)` in `GpuScatterLodCull.compute`. The hot path uploads one `GraphicsBuffer.Target.Constant` when `SystemInfo.supportsSetConstantBuffer` is available, with an explicit individual-vector fallback for unsupported platforms.

Rejected Alternatives: Keeping `SetVectorArray` or separate `SetInt`/`SetFloat` calls was rejected because it scatters hot-path driver state and violates the compute mandate. A `StructuredBuffer` for frame constants was rejected because these are uniform values, not indexed per-instance data.

Scalability potential: Low/MX350 keeps the same 64-thread kernel and cheap 100m cull while paying fewer dispatch setup calls. Middle/High/Ultra use the same packed lane and spend saved CPU/driver overhead on 250m/500m flora residency, crossfade, and stronger existing vegetation lighting lanes.

Hardware Impact: Expected gain is driver-call reduction, not shader ALU removal. Exact microseconds are PENDING VERIFICATION until Unity profiler/RenderDoc capture; estimated setup saving is 5-20us on weak CPU paths, with no claimed measured value.

Problem: Shared material mutation for buffer and scalar bindings can contaminate other renderers using the same material asset, while runtime material clones are cold memory debt and explicitly bad for asset ownership.

Solution: Routed the indirect draw's buffer and scalar bindings through one cached draw-local state object passed by `RenderParams.matProps`. The material asset still owns shader variants/keywords; buffers, offsets, LOD distances, and high/low scalar lanes are isolated per submission.

Rejected Alternatives: Per-frame material clones were rejected as heap/asset-state debt. Mutating shared material buffers every render was rejected because multiple scatter managers or reused materials could observe stale state. Creating a custom wrapper around the flora material was rejected because this is first-party indirect draw plumbing, not third-party asset ownership.

Scalability potential: Low keeps one cheap draw lane. High/Ultra can push the same material asset with stronger draw-local values without cross-manager contamination. The visual overkill remains flora-domain only: longer residency, crossfade, SSS/caustic/bloom lanes, and deterministic motion vectors.

Hardware Impact: 0B/frame managed GC target remains intact because the state object is allocated once in `Awake`. Exact microseconds are PENDING VERIFICATION; the expected win is correctness and reduced material-state churn, not a guaranteed measurable CPU delta.

Problem: The blackbox dump path still had a managed debug log on exception and wrote the ring in physical buffer order, making post-crash analysis harder.

Solution: Removed `Debug.LogError` from the dump catch path and publish a typed telemetry fault instead. The binary dump now starts at the oldest available ring entry and wraps chronologically through the last recorded frame.

Rejected Alternatives: `Debug.Log` was rejected because fault paths must not allocate strings or depend on Unity console state. Raw physical ring order was rejected because it forces manual cursor reconstruction during crash triage.

Scalability potential: All tiers keep identical 300-frame evidence. Low devices avoid console string debt; High/Ultra can raise visual density without losing deterministic crash context.

Hardware Impact: Fault-path only. Hot-path microseconds saved are 0us; crash-analysis quality improves. Runtime validation remains PENDING VERIFICATION because the project baseline still cannot compile.

Problem: The latest compile probe still cannot deliver final validation, and the blocker has moved as other agents change the baseline.

Solution: Re-ran isolated `Assembly-CSharp` and filtered `Hecton8.Core` probes after the constant-buffer pass. `Assembly-CSharp` still stops on missing `Temp/bin/Debug` metadata DLLs. `Hecton8.Core.csproj` currently stops in unrelated `SubmarineFluidDynamics` CS1612/CS0200 read-only native handle writes. No filtered `GpuScatter`, `FloraScatter`, `BufferID`, or `H8Memory` error appeared.

Rejected Alternatives: Editing `SubmarineFluidDynamics` from the rendering scatter task was rejected as outside the assigned domain. Inventing metadata DLLs or cross-domain stubs was rejected because it would hide the real integration state.

Scalability potential: Not runtime-relevant. Scatter profiling still requires the baseline compile wall to be cleared first.

Hardware Impact: 0us runtime. Validation remains PENDING VERIFICATION.

## 2026-05-16 Loop 8: Shader Aux Lanes + Stale Args Quarantine

Problem: `Hecton_IndirectVegetation.shader` unconditionally reads `_HectonFloraAges01` and `_HectonFloraPhaseSeeds`, but the scatter renderer only bound matrices, metadata, motion vectors, and visible indices. On strict mobile/Metal backends that is a real unbound-buffer risk, not a cosmetic warning.

Solution: Added DataVault-owned `BufferID.FloraScatterAge01` and `BufferID.FloraScatterPhaseSeeds` lanes, resolves them through `VaultBufferHandle<float>`, creates matching structured GPU buffers, uploads them with the same generation/dirty rules as the other scatter data, and binds them draw-locally. If a producer has not supplied data, the renderer initializes ages to `1.0` and phase seeds through a deterministic hash.

Rejected Alternatives: Editing the vegetation shader to stop reading those lanes was rejected because that shader is already the flora visual authority and other producers may depend on those channels. Binding tiny dummy private arrays was rejected because it violates DataVault sovereignty and can break at 100k source-index reads. CPU-calculating per-frame ages was rejected because it adds managed/system ownership the renderer does not need.

Scalability potential: Low/MX350 gets deterministic cheap phase variation without CPU animation. Middle/High/Ultra can consume richer producer-filled ages/seeds later for crossfade, growth state, and shader overkill without changing the indirect draw contract.

Hardware Impact: Correctness and crash prevention are the main gain. Exact microseconds are PENDING VERIFICATION; default-lane initialization happens only when the Vault buffer is created or expanded, not per frame.

Problem: The vegetation shader has multiple optional `StructuredBuffer` reads gated by counts or resolutions. If shared material state from another renderer leaves those gates non-zero while this indirect draw does not bind the optional buffers, the GPU can read undefined resources.

Solution: Added draw-local optional fallback state that sets snap flags, flow resolution, interaction count, wake count, impact sphere count, predator AUP count, abyssal grid resolution, and abyssal flow texture activity to zero through the cached `RenderParams.matProps` state.

Rejected Alternatives: Mutating the shared material to zero these fields was rejected because it contaminates other renderers. Binding all optional buffers from this manager was rejected because those systems are outside the scatter domain. Ignoring the issue was rejected because Quest/Android and Metal drivers are less forgiving about invalid resource reads.

Scalability potential: Low avoids optional buffer pressure entirely. High/Ultra can still receive those optional effects from their owning systems once real buffers and non-zero counts are supplied through the correct domain path.

Hardware Impact: A handful of draw-local scalar writes are expected to be below profiling noise; exact microseconds are PENDING VERIFICATION. The value is deterministic resource safety.

Problem: Invalid frustum, zero active count, or failed upload returned early while the previous indirect args count could remain live. That can draw stale flora for one or more frames and poison the blackbox visible count.

Solution: Added `ClearVisibleState()` to reset append counters, copy a zero append count into the indirect args instance slot when needed, clear pending visible readback state, and record blackbox flags for invalid frustum and no-active-instance early exits. Args-buffer recreation now invalidates the indirect-args cache so a reused mesh cannot skip initialization on a fresh buffer.

Rejected Alternatives: Letting the next successful dispatch overwrite args was rejected because failure frames still render. CPU-writing args every early exit was rejected where `CopyCount` can keep ownership on the GPU path. Suppressing blackbox entries was rejected because no-active and invalid-frustum frames are diagnostic state.

Scalability potential: Low devices get stricter fault containment with no extra normal-frame work. High/Ultra can run aggressive 500m residency without stale indirect draws after a producer or frustum fault.

Hardware Impact: Normal-frame cost is 0us beyond a dirty flag. Fault/early-exit cost is one append-counter reset and one `GraphicsBuffer.CopyCount`. Exact microseconds are PENDING VERIFICATION.

Problem: The renderer still bound `_HectonScatterVisibleMatrices` to the material even though the current indirect vegetation shader uses source matrices plus visible source indices, not the appended visible-matrix buffer.

Solution: Removed the unused material buffer binding while preserving the compute append stream and `CopyCount` source required by the prompt.

Rejected Alternatives: Removing the visible-matrix append buffer entirely was rejected because task 6 explicitly requires it and task 16 uses it as the indirect count source. Keeping the unused shader binding was rejected because it is dead material state.

Scalability potential: All tiers keep the same prompt-compliant append stream. Future high-tier passes can still consume the visible-matrix buffer from compute without adding CPU compaction.

Hardware Impact: Estimated 1-3us driver-state reduction on weak CPU paths, PENDING PROFILER. No measured microsecond claim is made.

Problem: The latest validation pass still cannot produce a clean `dotnet build` verdict, and the external blocker changed again while scatter code stayed filtered-clean.

Solution: Re-ran the focused static scan, `git diff --check`, isolated `Assembly-CSharp`, and filtered `Hecton8.Core` build probes. Static scan returned no forbidden scatter hot-path patterns. `git diff --check` reported only LF-to-CRLF warnings. `Assembly-CSharp` still fails on missing `Temp/bin/Debug` metadata DLLs. `Hecton8.Core.csproj` now fails at `Assets/_Project/Scripts/Core/InputDispatcher.cs(7,2)` with CS1032, before scatter compilation evidence can be authoritative.

Rejected Alternatives: Editing `InputDispatcher.cs` from the rendering scatter mandate was rejected as outside the assigned domain. Inventing generated/plugin metadata DLLs was rejected because it would falsify the build state. Claiming verified microseconds was rejected because there is still no compiled profiling player.

Scalability potential: Not runtime-relevant. Profiling Low/Middle/High/Ultra tiers still depends on restoring the baseline compile pipeline.

Hardware Impact: 0us runtime. Validation remains dependency-blocked, not scatter-blocked by the filtered evidence available.

## 2026-05-16 Loop 9: Mobile Thread-Group Contract

Problem: The compute shader declares `[numthreads(64, 1, 1)]`, but the C# dispatch path still used a hardcoded `64`. The mobile compute mandate requires runtime query through `ComputeShader.GetKernelThreadGroupSizes`, because shader/C# drift can silently over-dispatch or under-dispatch after a kernel edit.

Solution: Added `_dispatchThreadGroupSizeX` populated from `GetKernelThreadGroupSizes` after kernel resolution. Dispatch group count now uses the queried X dimension. The CPU Burst audit keeps a separate `BurstAuditBatchSize` constant because it is job scheduling granularity, not GPU shader ABI.

Rejected Alternatives: Keeping one shared `ThreadGroupSize` constant was rejected because it hides shader/C# drift. Parsing shader text from C# was rejected as brittle and I/O-hostile. Assuming the current 64 is forever valid was rejected by the mobile warp-sizing mandate.

Scalability potential: Low/MX350 and Quest get the actual kernel size and avoid divergent dispatch math. High/Ultra can change kernel variants later without touching draw submission code, as long as the shader reports a valid group shape.

Hardware Impact: Expected hot-path microsecond delta is 0us after initialization because the query runs during GPU state resolution, not every frame. It removes a correctness failure mode; exact profiler data remains PENDING VERIFICATION.

Problem: Metal/Mac and mobile reject oversized thread groups; the previous code only relied on the current shader value being safe.

Solution: Added a 1024-total-thread guard for the queried kernel dimensions. Invalid or zero group dimensions fail GPU readiness, reset to the 64-thread fallback, and record `BlackBoxFlagInvalidThreadGroup` into the 300-frame scatter blackbox.

Rejected Alternatives: Letting Unity or the driver fail later was rejected because the blackbox would not identify the scatter dispatch contract as the cause. Clamping an oversized group and continuing was rejected because C# cannot change the shader's actual `numthreads`.

Scalability potential: Low/Mobile gets fail-fast protection. High/Ultra still uses the queried dispatch size and can add future desktop-only kernel variants only after platform capture.

Hardware Impact: Fault-path only. Normal-frame cost remains 0us target; no measured microsecond claim.

Problem: The validation wall shifted again after the patch.

Solution: Re-ran filtered build probes. `Assembly-CSharp` still fails before scatter on missing generated/plugin metadata DLLs. `Hecton8.Core.csproj` currently stops in unrelated `SubmarineFluidDynamics.cs(614-635)` with missing `VaultNativeBuffer<>`. Filtered output still shows no `GpuScatter`/`FloraScatter` compiler error.

Rejected Alternatives: Editing submarine fluid code from the rendering scatter prompt was rejected as outside domain. Claiming final validation was rejected because the project still cannot build.

Scalability potential: Not runtime-relevant.

Hardware Impact: 0us runtime. Validation remains dependency-blocked.

## 2026-05-16 Loop 10: ABI Layout + Unload Sentinels

Problem: ARM64/Quest and Metal builds are less tolerant of CPU/GPU struct drift. The renderer had `[StructLayout(Pack = 1)]` on the two owned payload structs, but it did not actively prove that the runtime ABI still matched the declared GPU strides after platform/backend changes.

Solution: Added a cold `UnsafeUtility.SizeOf<T>` guard in `Awake` for `Matrix4x4`, `Vector4`, `GpuScatterFloraInstanceData`, `ScatterFrameConstants`, and `ScatterBlackBoxEntry`. If any stride differs from the GPU contract, the component disables itself before registering the tick path and publishes typed telemetry with `BlackBoxDumpReasonAbiLayout`.

Rejected Alternatives: Trusting C# attributes alone was rejected because backend or type edits can break stride silently. Running the guard every frame was rejected because this is a cold ABI contract, not a hot-path condition. Continuing with clamped strides was rejected because the shader layout cannot be repaired from C# once buffers are created with the wrong ABI.

Scalability potential: Low/Quest fails closed instead of issuing malformed GPU buffer reads. Middle/High/Ultra keep the same render path and can add future struct lanes only after updating the explicit stride constants and guard.

Hardware Impact: 0us normal-frame target because the check runs only during component initialization. No measured microsecond claim; this is crash prevention and platform survival.

Problem: Scene unload already released GPU buffers on disable, but `OnDestroy` did not explicitly invalidate DataVault leases. In Unity teardown ordering that is usually covered by `OnDisable`, but the memory-sentinel contract should not depend on event ordering.

Solution: `OnDestroy` now calls `InvalidateDataVaultLease()` and clears `_gpuReady` after releasing GPU and CPU audit resources. `ReleaseGpuBuffers()` also resets the queried dispatch group size to the 64-thread fallback so a recreated GPU state cannot inherit a stale kernel ABI cache.

Rejected Alternatives: Relying only on `OnDisable` was rejected because teardown order and disabled-component destruction can vary. Disposing DataVault memory from the renderer was rejected because the vault owns native buffers.

Scalability potential: All tiers get identical unload safety. Low-memory devices avoid retained VRAM/handle assumptions; high-tier scenes can stream dense flora without renderer-owned stale handles.

Hardware Impact: Unload/fault path only. Normal-frame cost remains 0us target.

Problem: The validation gate changed after the ABI pass.

Solution: Re-ran static scans, `git diff --check`, `Hecton8.Core.csproj`, and `Assembly-CSharp.csproj` with a real restore attempt. Static scan found no forbidden scatter patterns. `git diff --check` reported only LF-to-CRLF warnings. `Hecton8.Core.csproj --no-restore -m:1` now succeeds with 0 warnings and 0 errors. `Assembly-CSharp.csproj --no-dependencies -m:1` restores project assets, then fails before scatter compilation on 48 missing generated/plugin metadata DLLs under `Temp/bin/Debug`.

Rejected Alternatives: Generating fake `Temp/bin/Debug` DLLs or editing package/plugin ownership from the rendering scatter task was rejected as falsifying integration state. Claiming final validation was rejected because `Assembly-CSharp` still cannot build.

Scalability potential: Not runtime-relevant. Full player profiling still requires the Unity-generated/plugin metadata wall to be restored first.

Hardware Impact: 0us runtime. Final validation remains dependency-blocked outside scatter.

## 2026-05-16 Loop 11: Shader NaN Fail-Closed

Problem: The compute shader checked scale and distance, but `TransformPoint` converted a non-finite transformed position to `(0,0,0)`. If a malformed finite-but-overflowing matrix produced an invalid transform, the next center check could see a fake finite origin and append an instance that should have been rejected.

Solution: Added `HasFiniteMatrix` to validate all four `float4x4` rows before scale, added explicit finite checks for local bounds center/extents, and changed `TransformPoint` to return the raw transformed value so the existing center finite check rejects overflow instead of hiding it.

Rejected Alternatives: Sanitizing invalid transforms to origin was rejected because it can turn poison into visible geometry. Relying only on the C# upload validator was rejected because GPU-side fault containment must survive stale buffers and platform-specific faults. Adding CPU repair of malformed matrices was rejected because producer data ownership belongs to the DataVault producer.

Scalability potential: Low/Quest avoids one invalid instance poisoning append/draw state. High/Ultra keep the same visible append stream and can spend cycles on visual density without accepting malformed source transforms.

Hardware Impact: Adds finite checks in the compute kernel before append. Expected cost is below meaningful CPU time and GPU cost is PENDING PROFILER; correctness is prioritized because one NaN can poison the mobile GPU pipeline.

## 2026-05-16 Loop 12: CPU Audit NaN Parity

Problem: The GPU kernel now fails closed on malformed matrices, but the optional Burst audit still used raw serialized local bounds and only validated scale axes before frustum math. That could make audit output diverge from the shipping GPU path when bounds or matrix rows are poisoned.

Solution: Added shared safe local-bounds resolution for CPU audit, compute constant upload, fallback draw bounds, and editor validation. The Burst `ScatterCullJob` now rejects full non-finite matrices and non-finite local bounds before transform/frustum work, matching the GPU fail-closed contract.

Rejected Alternatives: Leaving audit divergence was rejected because diagnostic code must not report a visibility mask that the GPU would reject. Per-frame CPU repair of producer matrices was rejected because the DataVault producer owns matrix validity. Letting NaN bounds travel to fallback draw bounds was rejected because it can corrupt culling bounds outside the compute kernel.

Scalability potential: Low devices keep the audit disabled by default and pay no runtime cost. High/Ultra validation captures can enable audit without getting false positives from a weaker CPU path.

Hardware Impact: Normal shipping frame cost remains 0us because the Burst audit is opt-in. Compute constant upload now receives sanitized bounds with no extra allocation; exact microseconds remain PENDING PROFILER.

## 2026-05-16 Loop 13: DataVault Visual Payload

Problem: The high-tier scatter path had broad material-level SSS/caustic/bloom controls, but no DataVault-owned per-instance visual payload. Using only shared material scalars makes 100k flora visually uniform on High/Ultra and pushes agents toward private renderer arrays or unmanaged shader side channels.

Solution: Added additive `BufferID.FloraScatterVisualPayload = 382`, a `VaultBufferHandle<Vector4>`, a matching `GraphicsBuffer`, generation-aware uploads, safe teardown, and deterministic cold defaults only when the Vault lane is absent or undersized. The indirect vegetation shader now reads `_HectonFloraScatterVisualPayload` only in `_QUALITY_HIGH` and uses the payload to modulate existing edge mask, curvature/SSS mask, flow caustic strength, and biolum intensity without adding a new interpolator.

Rejected Alternatives: A private renderer `NativeArray<Vector4>` was rejected by DataVault sovereignty. Adding new TEXCOORD varyings was rejected because the shader is already mobile-interpolator heavy. Per-frame material randomization was rejected because it would be uniform, stateful, and not source-index stable. Shifting existing `BufferID` values was rejected because other agents already claimed 376-381.

Scalability potential: Low/MX350 binds the buffer but disables consumption through `_HectonFloraScatterVisualPayloadEnabled = 0`, so the shader returns zero payload and keeps the cheaper visual path. Middle can keep the lane dormant. High/Ultra uses the same 500m residency to buy per-instance translucent rim, organic SSS, caustic shimmer, and biolum variation without C# simulation.

Hardware Impact: Extra memory is 16 bytes per active flora instance, about 1.6MB at 100k before allocator overhead. Default initialization is cold only. Normal-frame upload follows existing Vault generation/dirty rules and is skipped when generations do not change. No measured microseconds are claimed; exact cost remains PENDING PROFILER.

Problem: The validation wall changed after the visual payload pass.

Solution: Re-ran static debt scans, `git diff --check`, `Hecton8.Core.csproj --no-restore -m:1`, and `Assembly-CSharp.csproj --no-dependencies -m:1`. Static scans stayed clean. `git diff --check` only reported LF-to-CRLF normalization warnings. `Hecton8.Core` now fails outside scatter in `ArchitectEyeVisualizer` duplicate `ValidatePackedStructSizes` and ambiguous `LaserCutterEventPayload` references in audio/world systems. `Assembly-CSharp` restores, then fails before scatter on missing `Assembly-CSharp-firstpass.dll` and `RealtimeCSG.dll`.

Rejected Alternatives: Editing ArchitectEye, audio, or world systems from a rendering scatter prompt was rejected as outside the domain boundary. Inventing Unity-generated metadata DLLs was rejected as false validation. Claiming measured savings was rejected because no Unity player/profile capture exists.

Scalability potential: Not runtime-relevant. The implementation remains tier-gated, but final player validation requires the external compile wall to be repaired.

Hardware Impact: 0us runtime for the validation blocker. Integration remains dependency-blocked.

## 2026-05-16 Loop 14: Shared-Material Mutation Purge

Problem: The render path still mutated `floraMaterial.enableInstancing` and toggled `HECTON_GPU_INDIRECT`, `_QUALITY_MX350`, and `_QUALITY_HIGH` keywords on the shared material during draw submission. That is render-state contamination and violates the no shared-material mutation rule for SRP/instanced geometry.

Solution: Added optional pre-authored low/high material variant references and tier-based material selection. Removed runtime material keyword toggles and `enableInstancing` mutation from `Render`. Runtime tier scalar values remain draw-local through the existing `MaterialPropertyBlock`, while keyword-bearing shader variants must be authored on the material assets.

Rejected Alternatives: Runtime material clones were rejected because they leak/state-split assets and violate third-party/material integrity rules. Continuing to mutate shared material keywords was rejected because it contaminates other renderers. Converting shader feature keywords into dynamic branches was rejected because it would increase shader cost on low/MX350 and touches a broad shader contract beyond this scatter manager pass.

Scalability potential: Low uses a pre-authored `_QUALITY_MX350` material variant. High/Ultra use a pre-authored `_QUALITY_HIGH` material variant and can consume the visual-payload lane. If variants are not assigned, the fallback `floraMaterial` is used without runtime mutation, so authoring remains explicit and deterministic.

Hardware Impact: Removes per-frame shared material keyword churn and instancing flag writes; exact microseconds are PENDING PROFILER. The main benefit is deterministic render-state isolation, not a measured timing claim.

Problem: The validation wall changed again after the shared-material purge.

Solution: Re-ran the stricter scatter-domain static scan including `EnableKeyword`, `DisableKeyword`, `enableInstancing`, `renderer.material`, shared `material.Set*`, Unity `Update` methods, scene search, legacy EventBus, local private `NativeArray`, `Allocator.Persistent`, direct `H8Memory.Allocate/Release`, `Debug.Log`, and `string.Format`. It returned no matches. `git diff --check` only reported LF-to-CRLF normalization warnings. Latest `Hecton8.Core` fails outside scatter in `HectonMarineSnowRenderer` missing `CeilDivide`. `Assembly-CSharp` restores project references, then fails before scatter on 63 missing `Temp/bin/Debug` metadata DLLs.

Rejected Alternatives: Editing VFX marine snow, package metadata, or generated plugin DLLs was rejected as outside the RENDERING/BRG scatter domain. Claiming final validation was rejected because compile is still externally blocked.

Scalability potential: Not runtime-relevant. Material-variant selection supports Low/Middle/High/Ultra, but final player validation still requires external compile repair.

Hardware Impact: 0us runtime for the validation blocker.

## 2026-05-16 Loop 20: Frustum and DataVault Poison Containment

Problem: The indirect scatter path rejected bad matrices, but a poisoned camera frustum plane, metadata lane, age/phase lane, AUP offset, or lifecycle shader parameter could still slip into the GPU path. NaN frustum planes are especially dangerous because `signedDistance + radius < 0` fails open on many backends and can draw the full population instead of culling.

Solution: `GpuScatterLodManager` now validates finite metadata, age, phase, and visual-payload lanes before upload. AUP double-to-float conversion rejects NaN, infinity, and values outside float range before they reach compute constants. Camera/frustum planes must validate finite before dispatch. The compute kernel caps instance count against a capacity lane in `_HectonScatterParams4.w`, sanitizes AUP/camera/bounds constants, and rejects non-finite frustum planes. The lit, depth, shadow, and motion-vector vegetation passes now sanitize growth, age, runtime state, lifecycle decay, predator dim, flashbang, and cascade lanes before clamp/division math.

Rejected Alternatives: Trusting producers was rejected because DataVault is shared across many systems. Drawing through a bad frustum was rejected because it can turn one bad camera packet into 100k visible instances. Replacing poisoned metadata with arbitrary visible values was rejected because it hides the producer fault; the renderer must fail closed and record blackbox flags. Editing submarine physics or generated plugin metadata to force a green build was rejected as outside the RENDERING/BRG scatter domain.

Scalability potential: Low/MX350 fails closed to zero/fallback lanes instead of paying for far flora or poisoned deformation. Middle keeps the same indirect path with stronger producer validation. High/Ultra keep 500m residency, crossfade, SSS, caustics, biolum, and visual payloads, but payload/lifecycle/cascade faults collapse to baseline values instead of corrupting motion vectors, shadows, or lit output.

Hardware Impact: Added CPU validation only on DataVault upload/generation change and scalar GPU finite checks in existing branches. Exact CPU/GPU microseconds are PENDING PROFILER. The avoided failure case is a full-population bad-frustum draw and NaN propagation on Quest/Metal/mobile.

Problem: Validation after Loop 20.

Solution: Re-ran targeted raw-NaN shader scan, forbidden scatter-domain scan, sequential `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:minimal -clp:ErrorsOnly`, and sequential `dotnet build Assembly-CSharp.csproj --no-dependencies --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:minimal -clp:ErrorsOnly`. Shader and forbidden scatter scans are clean. `Hecton8.Core` is blocked outside scatter by `SubmarineFluidDynamics.cs(1853,60)` and `(4582,68)` ambiguous `float3`/`Vector3` subtraction. `Assembly-CSharp` is blocked before scatter by 53 missing `Temp/bin/Debug` generated/plugin metadata DLLs. `dotnet build-server shutdown` cleared lingering build-server processes after earlier timed-out parallel probes.

Rejected Alternatives: Claiming final validation was rejected because the baseline compile is not green. Killing arbitrary processes without attribution was rejected; build-server shutdown was used after checking active `dotnet` workers.

Scalability potential: Not runtime-relevant. The scatter implementation remains tier-gated and fault-contained, but final player validation still needs external compile repair.

Hardware Impact: 0us runtime for validation itself. Compile blockers are outside scatter.

## 2026-05-16 Loop 19: Companion Pass NaN Vaccination

Problem: The main scatter compute path and lit payload path were guarded, but the vegetation depth, shadow, and motion-vector passes still used raw player/interaction radius and speed lanes. A NaN radius or enable flag can bypass `<=` comparisons and then poison division or `smoothstep` threshold math.

Solution: Added finite non-negative/positive sanitizers to the lit, motion-vector, depth-only, and shadow vegetation shaders. The passes now sanitize player interaction enable/radius/speed/push, interaction point speed/radius, impact radius, submarine wash radius/speed, predator dim radius, and flash radius before divisions or radius-squared thresholds.

Rejected Alternatives: Guarding only the main lit pass was rejected because depth/shadow/motion can still poison the frame on mobile. Trusting producer-side values was rejected because optional VFX buffers can be stale or externally authored. Removing interaction deformation from low tier was rejected because the cheap deformation fake is part of the visual language; the correct fix is finite gates.

Scalability potential: Low/MX350 keeps the cheap triangle/current deformation but fails closed when optional lanes are poisoned. High/Ultra keep submarine wash, predator dimming, flash boosts, and interaction deformation without allowing one malformed event to corrupt motion vectors or shadow depth.

Hardware Impact: Adds small scalar finite checks around optional VFX lanes. Exact GPU cost is PENDING PROFILER; the checks are only on paths that already execute interaction/wash logic.

Problem: Validation after companion-pass shader hardening.

Solution: Re-ran shader scans for raw player enable comparisons, raw radius max patterns, groupshared/group-memory/wave constructs, forbidden scatter-domain C# patterns, and `git diff --check`. The remaining radius scan hit only the already-sanitized predator dim line. No groupshared, wave, group-memory, forbidden C# scatter patterns, or whitespace errors were found beyond LF-to-CRLF warnings.

Rejected Alternatives: Running a Unity shader import was not possible through the available tool surface. `dotnet build` does not compile HLSL, so shader validation remains static until Unity import/player build.

Scalability potential: Not runtime-relevant.

Hardware Impact: 0us runtime for validation itself.

## 2026-05-16 Loop 17: Compute Constant NaN Vaccination

Problem: The compute shader rejected poisoned matrices, bounds, center positions, and distance values, but packed frame constants still used direct `max` before uint casts and threshold math. A malformed constant lane should fail closed the same way malformed per-instance data does.

Solution: Added `SanitizeNonNegative`, `ResolveMaxDistanceSq`, `ResolveMotionStrength`, and `ResolveCrossfadeEnabled` to `GpuScatterLodCull.compute`. Instance count, frame index, max distance squared, motion strength, and crossfade scalar are now checked with `isfinite` before the shader casts, applies thresholds, or scales motion.

Rejected Alternatives: Trusting C# constant upload was rejected because GPU-side fault containment must survive stale buffers and backend-specific NaN behavior. Adding a broader dynamic validation buffer was rejected because the constants already fit in the packed 176B lane and do not need another resource.

Scalability potential: Low/Quest fails closed if a constant is poisoned instead of dispatching garbage instance counts or motion vectors. High/Ultra keep the same 64-thread Metal-safe kernel and spend cycles on visual payloads, not exception recovery.

Hardware Impact: Adds a few scalar finite checks in the compute kernel. Exact GPU cost is PENDING PROFILER; the branch count is tiny relative to 100k matrix/frustum work and prevents a poisoned constant from corrupting the whole append stream.

Problem: Validation after the compute-constant pass.

Solution: Re-ran symbol scans for `SanitizeNonNegative`, max-distance/motion-strength/crossfade resolver usage, thread-group declaration, and forbidden compute constructs. No groupshared barriers, wave intrinsics, or group-memory barriers were found. The forbidden scatter-domain scan remained clean. `git diff --check` reported only LF-to-CRLF warnings.

Rejected Alternatives: Skipping validation was rejected because this shader path is backend-sensitive on Metal/Mobile.

Scalability potential: Not runtime-relevant.

Hardware Impact: 0us runtime for validation itself.

## 2026-05-16 Loop 18: Shared-Material Revalidation

Problem: The material variant validator cached the last material instance/tier result and returned it without re-reading keywords. That is vulnerable if another renderer mutates a shared material keyword after this scatter manager has cached a valid result.

Solution: Removed the early-return cache path. `IsRenderMaterialVariantValid` now re-reads `HECTON_GPU_INDIRECT`, `_QUALITY_HIGH`, and `_QUALITY_MX350` on every validation pass before cull dispatch. The existing cache fields are retained only as last-observed blackbox telemetry.

Rejected Alternatives: Trusting the cache was rejected because this project is running many agents and shared material mutation has already been found as debt. Reintroducing runtime keyword repair was rejected because that would contaminate shared material state. Runtime material cloning was rejected because it creates allocation/state-split risk.

Scalability potential: Low/MX350 and High/Ultra both fail closed if material state is externally changed between frames. High/Ultra no longer risk spending 500m residency on a stale cached variant verdict.

Hardware Impact: Adds three material keyword checks per validation pass. Exact cost is PENDING PROFILER; correctness is prioritized because an invalid indirect keyword can break the shader indexing path.

Problem: Compile evidence after revalidation.

Solution: Re-ran forbidden scatter-domain scan, `git diff --check`, `dotnet build Hecton8.Core.csproj --no-restore -m:1`, and `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1`. The forbidden scan returned no matches. `git diff --check` reported only LF-to-CRLF warnings. `Hecton8.Core` fails outside scatter with 41 errors in `DiegeticGyroCompassRuntime`/`CompassStateDTO`, `ArchitectEyeVisualizer`, and `SystemDispatcher`. `Assembly-CSharp` fails before scatter on missing `Assembly-CSharp-firstpass.dll` and `RealtimeCSG.dll`.

Rejected Alternatives: Editing UI/core contracts or generated/plugin metadata from this rendering scatter task was rejected as outside the domain boundary. Claiming final validation was rejected because compile remains externally blocked.

Scalability potential: Not runtime-relevant.

Hardware Impact: 0us runtime for the validation blocker.

## 2026-05-16 Loop 16: Material Variant Fail-Closed

Problem: Runtime material keyword mutation was removed, but the renderer still trusted the authored material variant. A high-tier draw using a material without `HECTON_GPU_INDIRECT` or `_QUALITY_HIGH` would either index the wrong shader path or render mobile-grade flora on High/Ultra.

Solution: Added cached material-variant validation in `GpuScatterLodManager`. The check requires `HECTON_GPU_INDIRECT` for all draw materials, requires `_QUALITY_HIGH` on High/Ultra, and accepts low-tier variants that are `_QUALITY_MX350` or at least not `_QUALITY_HIGH`. Invalid variants clear indirect args before cull dispatch, skip `Graphics.RenderMeshIndirect`, and write `BlackBoxFlagInvalidMaterialVariant` into the 300-frame blackbox.

Rejected Alternatives: Re-enabling runtime `EnableKeyword`/`DisableKeyword` was rejected because it mutates shared material assets and contaminates other renderers. Runtime material cloning was rejected because it creates state split and leak risk. Drawing anyway was rejected because it can produce incorrect source-index reads or hidden high-tier visual downgrade.

Scalability potential: Low/MX350 still has a cheap authored path and fails closed if a high material is accidentally assigned. High/Ultra now prove the authored high shader variant before spending the 500m residency and visual-payload path.

Hardware Impact: Normal-frame work is a cached material instance/tier check plus one cache read after the first validation. Exact microseconds are PENDING PROFILER. Fault path clears indirect args before GPU cull dispatch and avoids an invalid draw.

Problem: Compile evidence changed after the material-variant pass.

Solution: Re-ran the forbidden scatter-domain scan, `git diff --check`, `dotnet build Hecton8.Core.csproj --no-restore -m:1`, and `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1`. The forbidden scan returned no matches. `git diff --check` reported only LF-to-CRLF warnings. Compile evidence is externally unstable: one `Hecton8.Core` probe succeeded with four unrelated `ArchitectEyeVisualizer` CS0649 warnings, then later probes failed outside scatter in tether/physics contracts and finally `SargassumMicroFaunaBoids` missing `SaturateFinite01` at 9 callsites. The latest `Assembly-CSharp` probe restores, then fails before scatter on 3 missing generated/plugin metadata DLLs in `Temp/bin/Debug`.

Rejected Alternatives: Editing generated/plugin metadata or third-party assemblies from this rendering scatter task was rejected as outside the domain boundary. Claiming final validation was rejected because `Assembly-CSharp` still cannot compile without those metadata DLLs.

Scalability potential: Not runtime-relevant.

Hardware Impact: 0us runtime for the validation blocker.

## 2026-05-16 Loop 15: Blackbox Visual Payload Telemetry

Problem: The visual-payload DataVault lane was generation-tracked for uploads, but the 300-frame scatter blackbox still only wrote matrix, metadata, age, and phase seed generations. A high-tier payload fault would be visible in rendering but opaque in the crash dump.

Solution: Bumped scatter `BlackBoxVersion` to 2 and preserved the fixed 64-byte `ScatterBlackBoxEntry` size by replacing separate age/phase generation fields with `AuxiliaryGenerationHash = hash(age, phase)` plus explicit `VisualPayloadGeneration`. Dump order was updated to write the new fields chronologically.

Rejected Alternatives: Expanding `ScatterBlackBoxEntry` was rejected because the blackbox contract is fixed-size and platform layout-guarded. Dropping auxiliary lane evidence entirely was rejected because age/phase lanes are still shader inputs. Adding managed diagnostic strings was rejected because blackbox dumps are binary and zero-GC in the hot path.

Scalability potential: Low/MX350 still records payload generation even when high-tier consumption is disabled, proving the lane state. High/Ultra now have crash telemetry for the per-instance visual-overkill payload.

Hardware Impact: No measured microseconds. Entry size remains 64 bytes and write cadence is unchanged; only two uint field meanings changed under version 2.

Problem: The high-tier shader payload read returned `saturate(payload)` directly. If a producer wrote NaN/Inf into the payload, saturate was not a sufficient cross-backend NaN vaccine.

Solution: `ResolveScatterVisualPayload` now checks `all(isfinite(payload))` and returns a zero payload on poison before payload values reach edge, curvature/SSS, flow, or biolum output channels.

Rejected Alternatives: Trusting DataVault producers was rejected because mobile GPU fault containment must survive malformed data. Clamping in C# only was rejected because stale GPU buffers and producer-side writes can still race validation. Expanding varyings to carry diagnostic flags was rejected because the shader is already interpolator-heavy for Quest.

Scalability potential: Low path remains disabled and cheap. High/Ultra keep per-instance visual payloads but fail closed to baseline flora variation when the payload is poisoned.

Hardware Impact: Adds one finite check on the high-tier vertex path only. Exact GPU cost is PENDING PROFILER; the value is NaN containment.

Problem: The validation wall moved again after the blackbox/payload NaN pass.

Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1` and `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1`. `Hecton8.Core` fails outside scatter in `PlayerCriticalProceduralAudioRenderer` missing `ClearVaultBackedAudioBufferAliases` and `TetherManager` missing `_fixedStepClockSeconds`/`TetherFixedClockWrapSeconds`. `Assembly-CSharp` restores, then fails before scatter on 55 missing `Temp/bin/Debug` metadata DLLs.

Rejected Alternatives: Editing audio/tether systems or generated plugin metadata was rejected as outside the RENDERING/BRG scatter domain. Claiming final validation was rejected because compile remains externally blocked.

Scalability potential: Not runtime-relevant.

Hardware Impact: 0us runtime for the validation blocker.

## 2026-05-17 Loop 21: Core Compile Wall Cleared, Final Metadata Wall Is External

Problem: The moving compile wall had shifted outside scatter again. `SubmarineFluidDynamics` carried a partial float3/DataVault migration, player motor native-state allocation was calling the new vault-backed API without a guaranteed vault resolver, and `InteractionSignal` explicit-layout padding assigned a `uint` literal to a `ushort`. These errors blocked any honest verification of the indirect scatter implementation.

Solution: Kept the cross-domain repair to compile contracts only. Submarine exterior thermal/buoyancy arrays now complete the float3 `VaultNativeBuffer` migration with finite conversion helpers and vault allocation/disposal/refresh coverage. `HectonPlayerMotor` resolves the current `IDataVault` before handing native sweep buffers to `HectonPlayerMotorNativeState`. `InteractionSignal` uses a width-correct padding assignment. Re-ran `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:minimal -clp:ErrorsOnly`; it succeeds with 0 warnings/0 errors.

Rejected Alternatives: Reverting other agents' moving physics/gameplay changes was rejected. Taking broad ownership of tether, submarine, or player movement behavior was rejected; the edits are compile glue only. Fabricating `Temp/bin/Debug` generated/plugin DLLs for `Assembly-CSharp` was rejected because that would hide the real build dependency.

Scalability potential: Low/MX350 and Quest get no added scatter runtime work from these compile repairs. The scatter path remains the same: 100m low-tier cull, DataVault-owned SoA lanes, 64-thread Metal-safe compute dispatch, NaN fail-closed lanes, and 500m High/Ultra visual payload/crossfade residency. Top-tier visual overkill remains paid for by avoided CPU flora managers, not by these repair edits.

Hardware Impact: 0us scatter runtime claimed. The repaired compile contracts are cold allocation/validation paths. Final `Assembly-CSharp` validation is still blocked before scatter by 52 missing generated/plugin metadata DLLs under `Temp/bin/Debug`, including `Assembly-CSharp-firstpass.dll`, `RealtimeCSG.dll`, `AmplifyImpostors.*`, `AstarPathfindingProject*`, `Bakery*`, `Crest*`, `GPUInstancer*`, `Hecton8.Editor.dll`, `Hecton8.Input.dll`, `Hecton8.World.Dots.dll`, `MapMagic*`, `MoreMountains.*`, and `Unity.RenderPipelines.Universal.Editor.dll`.

Problem: Validation after Loop 21.

Solution: Re-ran the current top-level check and static inquisition. `dotnet build Assembly-CSharp.csproj --no-dependencies --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:minimal -clp:ErrorsOnly` fails only on 52 missing generated/plugin metadata DLLs under `Temp/bin/Debug`. Targeted scatter-domain forbidden scan found no local `NativeArray` fields, persistent allocator use, direct `H8Memory.Allocate`, legacy `EventBus`, managed delegates, Unity `Update` methods, scene search, `Debug.Log`, `string.Format`, `Instantiate`, or `DrawMeshInstancedIndirect`. Precise shader scan found no wave intrinsics, `groupshared`, or memory barriers. Touched-file `git diff --check -- <paths>` reports only LF-to-CRLF warnings.

Rejected Alternatives: Reporting repository-wide `git diff --check` as clean was rejected because it currently fails on unrelated trailing whitespace in `Docs/AgentLogs/Dump_COMPILE_ERROR.txt`. Editing that unrelated dump from the scatter task was rejected as unnecessary ownership creep.

Scalability potential: Not runtime-relevant. Validation evidence now separates a green core assembly from the external generated-metadata wall.

Hardware Impact: 0us runtime for validation. No measured microseconds were invented.

## 2026-05-17 Loop 22: Compute Cast Overflow and Global Biolum Poison Pass

Problem: The compute kernel sanitized NaN and infinity, but finite absurd constants still reached `uint` casts for instance count and frame index. HLSL backend behavior around oversized float-to-uint casts is not a portable contract for Metal/Quest. The lit vegetation shader also accepted global biolum params, state arrays, AUP offset, and clock without finite guards, so one poisoned global VFX lane could contaminate high-tier emission.

Solution: Added `SanitizeFiniteRange` in `GpuScatterLodCull.compute`. Instance count and frame index now clamp to a safe 24-bit float-exact range before cast; instance count also clamps against the capacity lane before conversion. Max distance squared is capped at 250000.0, matching the 500m high-tier contract, and motion strength is capped to 2.0 before motion-vector output. `ResolveIndirectVegetationGlobalBiolum` now sanitizes params, AUP offset, clock, primary/secondary state, RGB, and intensity before overdrive/spark/haze math.

Rejected Alternatives: Trusting C# constants was rejected because stale GPU buffers and backend-specific cast rules can still bite. Raising the high-tier cull cap above 500m was rejected because the XML task explicitly defines 500m. Removing global biolum was rejected because High/Ultra should spend saved cycles on visual overkill; the correct fix is finite gating.

Scalability potential: Low/MX350 keeps the cheap cull and deformation path and now fails closed if constants are corrupt. Middle keeps the same 64-thread kernel. High/Ultra keep global biolum overdrive, spark, haze, 500m cull, SSS, caustic, and visual payloads, but poisoned biolum state collapses to zero/fallback instead of corrupting emission.

Hardware Impact: Added scalar clamp/finite checks in existing compute/shader setup paths. Exact GPU microseconds are PENDING PROFILER. No measured microseconds were invented.

Problem: Validation after Loop 22.

Solution: Re-ran targeted scans and builds. Forbidden scatter-domain scan found no local `NativeArray` ownership, persistent allocator use, direct `H8Memory.Allocate`, legacy `EventBus`, managed delegates, Unity `Update` methods, scene search, `Debug.Log`, `string.Format`, `Instantiate`, or `DrawMeshInstancedIndirect`. Precise shader scan found no wave intrinsics, `groupshared`, or memory barriers; only expected 64-thread `numthreads` remains. Cast-overflow scan found no remaining `(uint)SanitizeNonNegative` or direct `_Hecton` uint casts. Touched-file `git diff --check -- <paths>` reports only LF-to-CRLF warnings. `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:minimal -clp:ErrorsOnly` succeeds with 0 warnings/0 errors. `dotnet build Assembly-CSharp.csproj --no-dependencies --disable-build-servers -p:UseSharedCompilation=false -m:1 -v:minimal -clp:ErrorsOnly` remains blocked before scatter by missing `Temp/bin/Debug/Assembly-CSharp-firstpass.dll` and `Temp/bin/Debug/RealtimeCSG.dll`.

Rejected Alternatives: Claiming Unity shader import/player validation was rejected; the available validation is static HLSL scan plus C# project build. Editing generated/plugin metadata was rejected as false completion.

Scalability potential: Not runtime-relevant beyond the constant poisoning protection described above.

Hardware Impact: 0us runtime for validation. The shader changes require target GPU profiler capture before any cost claim.

## 2026-05-17 Loop 23: External Position Poison and Shader Import Pass

Problem: The vegetation shaders had scalar finite gates for many radii and speeds, but several external producer position lanes still entered deformation and emission math directly. Static scan also found two shader-import defects where a `float3` player position was assigned into a scalar `float` in depth/shadow passes.

Solution: Added fail-closed position validation to lit/depth/shadow/motion vegetation paths. Interaction points now skip if velocity or position is non-finite. Player bend returns zero when the player position is non-finite. Impact, predator, and flash positions are skipped or disabled when poisoned. Lit wake/wash center and velocity lanes sanitize to local/fallback vectors so High/Ultra wake visuals cannot propagate NaN. Fixed the depth/shadow scalar/vector player-position assignments. Per latest direction, validation used targeted shader scans and `git diff --check`, not another dotnet rebuild.

Rejected Alternatives: Falling back a poisoned player position to the evaluated vertex was rejected because it creates maximum local bend instead of zero bend. Repeating full `dotnet build` after every shader polish was rejected per direction and because `Assembly-CSharp` is already blocked before scatter by generated/plugin metadata.

Scalability potential: Low/MX350 keeps cheap bend and deformation fakes but poisoned external positions collapse to zero/skipped influence. High/Ultra retain wake, wash, predator dim, flash, global biolum, SSS, caustic, and visual payload overkill, but malformed producer positions no longer corrupt lit/depth/shadow/motion output.

Hardware Impact: Adds small scalar/vector finite checks in existing shader branches. Exact GPU microseconds are PENDING PROFILER. No measured microseconds were invented.

Problem: Validation after Loop 23.

Solution: Ran targeted scalar/vector mismatch scan, position-lane finite-guard scan, forbidden scatter-domain scan, precise wave/barrier scan, and touched-file `git diff --check`. Scalar/vector mismatch scan returned no matches. Forbidden scan returned no local `NativeArray`, allocator, direct `H8Memory.Allocate`, legacy `EventBus`, managed delegate, Unity `Update`, scene search, `Debug.Log`, `string.Format`, `Instantiate`, or `DrawMeshInstancedIndirect` hits. Wave/barrier scan found only the expected 64-thread `numthreads`. `git diff --check` reports only LF-to-CRLF warnings.

Rejected Alternatives: Claiming Unity shader import validation was rejected because no Unity import probe was run for this incremental pass. Claiming performance savings from the new finite checks was rejected without target GPU profiling.

Scalability potential: Validation confirms the same low/high behavior described above without extra managed hot-path work.

Hardware Impact: 0us runtime for validation. No rebuild was run for this polish pass.

## 2026-05-17 Loop 24: Legacy Scatter Compute Hardening

Problem: `Hecton_GpuScatter.compute` was adjacent scatter rendering code and still used unchecked count casts plus a `DeviceMemoryBarrierWithGroupSync()` despite not using groupshared memory. Negative or absurd grid/candidate/density constants could become huge uints, and malformed terrain/biome/depth/cave inputs could leak into generation or compaction.

Solution: Added finite sanitizers and safe count conversion helpers. Grid resolution, candidate count, density bin count, dither frame, height pixel bounds, biome IDs, foveated cadence, and density bin writes now clamp before uint-visible use. Frustum planes, clip coordinates, eye depth, terrain/camera vectors, scale/radius, cave SDF inputs, and compaction distances now fail closed or fallback to bounded values. Removed the unused device memory barrier from compaction because it did not synchronize across dispatches and had no groupshared dependency.

Rejected Alternatives: Leaving the legacy compute untouched was rejected because it lives in the scatter rendering surface and targets the same mobile/Metal risk class. Keeping the barrier was rejected because it buys no correctness without groupshared data and adds backend synchronization noise. Repeating a full dotnet rebuild was rejected per direction; this was shader-only polish validated by static scans.

Scalability potential: Low/MX350 keeps cheap foveated/dithered scatter generation and density binning but fails closed on bad constants. High/Ultra can keep denser scatter candidates without count-cast poison or backend-specific barrier behavior.

Hardware Impact: Removed one useless group-sync barrier from the compact kernel. Exact GPU microseconds are PENDING PROFILER. Added finite guards are scalar and bounded; no measured performance claim was made.

Problem: Validation after Loop 24.

Solution: Ran targeted scans on `Hecton_GpuScatter.compute`. No wave intrinsics, no `groupshared`, and no memory barriers remain. The only thread-group declarations are three `numthreads(HECTON_SCATTER_THREADS, 1, 1)` declarations with `HECTON_SCATTER_THREADS = 64`, under the 1024 Metal/Mobile limit. `git diff --check` reports only LF-to-CRLF warnings.

Rejected Alternatives: Claiming Unity shader import validation was rejected because no Unity import probe was run. Claiming exact barrier savings was rejected without target GPU capture.

Scalability potential: Validation is shader-static only but confirms the backend-sensitive constructs were removed or bounded.

Hardware Impact: 0us runtime for validation. No rebuild was run.

## 2026-05-17 Loop 25: Adjacent ScatterIndirect Lit Shader Hardening

Problem: `Hecton_ScatterIndirectLit.shader` was a clean adjacent BRG scatter shader still trusting GPU instance payload, material scalar lanes, AUP offsets, and deformation phases. It also used raw `sin()` for sway and let shadow caster deformation diverge from the lit pass by skipping procedural rock displacement. Finite-but-poisoned producer data could reach `uint` yaw sector conversion, `rsqrt`, `rcp`, UV transforms, fog, lighting, emission, and shadow bias.

Solution: Added local finite/positive/non-negative helpers and sanitized `PositionScale`, `NormalRotation`, `AtlasFlow`, material scalar lanes, micro normals, AUP offset, UVs, clip-space output, fog factor, lighting, caustics, biolum, sonar emission, and shadow path inputs. Replaced raw sine sway with `HectonCoreLitTrianglePulse01`, preserving a believable flora motion fake without transcendent trig. Shadow caster now applies the same procedural rock offset as the forward pass before storm-rain ripple and bias.

Rejected Alternatives: Leaving this adjacent shader untouched was rejected because it consumes the same scatter buffers and targets the same Quest/Metal/MX350 risk class. Adding CPU-side validation in `GPUScatterDirector` only was rejected because stale GPU buffers and authored material poison can still enter the shader. Keeping raw sine was rejected because the existing triangle pulse carries the same player-facing sway at lower ALU cost. Rebuilding the full project after this shader-only polish was rejected per direction and because `Assembly-CSharp` is already blocked by generated/plugin metadata.

Scalability potential: Low/MX350 keeps cheap triangle/parabola sway, bounded material lanes, and fail-closed instance data. Middle keeps the existing stochastic surface and caustics path. High/Ultra retain richer micro-normal, environmental wear, caustics, biolum, sonar, storm-rain, and procedural rock variation without letting producer poison corrupt the frame.

Hardware Impact: Removed one raw vertex-stage `sin()` in favor of an existing triangle-pulse fake. Exact GPU microseconds are PENDING PROFILER. Added scalar finite guards are shader hot-path branches; no measured performance claim was made.

Problem: Validation after Loop 25.

Solution: Ran targeted shader scans on `Hecton_ScatterIndirectLit.shader`. No `sin`, `cos`, `tan`, wave intrinsics, memory barriers, `groupshared`, or `DrawMeshInstancedIndirect` remain. Remaining `rsqrt` calls are behind finite length-squared checks; remaining `rcp` calls use `max(scale, 0.0001)`. `git diff --check -- Assets/_Project/Art/Shaders/Hecton_ScatterIndirectLit.shader` reports only LF-to-CRLF warnings.

Rejected Alternatives: Claiming Unity shader import validation was rejected because no Unity import probe was run. Running another dotnet rebuild was rejected for this incremental shader polish.

Scalability potential: Validation is static only but confirms the shader no longer relies on backend-specific behavior for poisoned scatter payloads or raw trigonometric sway.

Hardware Impact: 0us runtime for validation. No rebuild was run.
