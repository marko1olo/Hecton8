# GPU_SCATTER_LOD_MANAGER Rationale

Status: PENDING VERIFICATION

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

Solution: Ran isolated `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1` and full `dotnet build Assembly-CSharp.csproj --no-restore -m:1`. Isolated build stops on missing generated/plugin DLLs in `Temp/bin/Debug`; full build stops earlier on missing RealtimeCSG source files. A filtered build scan for `GpuScatter`, `Rendering/Scatter`, and `FloraScatter` produced no scatter-specific compiler errors.

Rejected Alternatives: Creating fake RealtimeCSG/plugin files or cross-domain stubs was rejected as architectural sabotage. Reverting the scatter changes was rejected because the observed errors are project dependency/source-file failures outside the assigned domain.

Scalability potential: Not runtime-relevant. Integration must restore the missing external/generated artifacts before player profiling and final dotnet validation can be authoritative.

Hardware Impact: 0us runtime. Validation remains blocked by dependency state, not scatter code.
