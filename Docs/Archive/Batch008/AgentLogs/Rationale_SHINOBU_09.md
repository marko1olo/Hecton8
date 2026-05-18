PROMPT IDENTIFIED: SHINOBU_09 | DOMAIN: BRG Scatter Director / Abyssal Forest Instancing | TASK COUNT: 20

Date: 2026-05-18
Status: ULTRA POLISHED + MATERIAL CACHE + CPU SCRATCH CACHE + BOXING GUARD + COMPUTE BINDING CACHE + HEADLIGHT UPLOAD GATE + MOTION VECTOR CACHE + INDIRECT ARGS CACHE + EXTERNAL BUILD WALL

## Decision 00 - Domain Lock
Problem: The prompt asks for a BRG scatter highway while the project already has a large vegetation BRG/indirect stack. Adding a separate renderer would double-own the same flora family.
Solution: Patch and audit the existing world vegetation render lane: `HectonIndirectVegetationRenderer`, `FloraCulling.compute`, and `Hecton_IndirectVegetation.shader`.
Rejected Alternatives: A new standalone manager with its own public API; direct dependencies on Agent 08 output; MeshRenderer hierarchy scatter.
Scalability potential: Low uses fewer visible instances and cheaper shader math; Middle keeps HZB and BRG; High/Ultra spend saved cycles on denser LOD residency and richer current/wake bending.
Hardware Impact: Avoids duplicate buffers and duplicate culling dispatches on i3/MX350; expected prevention of 0.2-0.7 ms CPU submission overhead, PENDING VERIFICATION.

## Decision 01 - Dear Lie Current Rule
Problem: 150,000 flora instances cannot own colliders, rigidbodies, or CPU current integration.
Solution: Treat currents and wake as shader deformation data. CPU owns visibility, paging, and finite scalar/vector uploads only.
Rejected Alternatives: Unity WindZone, trigger volumes per kelp patch, Rigidbody/AddForce plants, per-instance CPU matrix mutation.
Scalability potential: Low disables or decimates deformation; Middle keeps global flow and one wake lane; High supports more wake impulses and biolum variation; Ultra can widen near-field LOD residency and add extra shader harmonics.
Hardware Impact: Per-plant physics would be catastrophic on MX350/i3. Shader fake keeps the cost in visible vertices only and avoids broadphase churn.

## Decision 02 - Archive Distance Evidence
Problem: Legacy binary OSHINO thresholds were requested, but the archive scan has not found named `.h8bin`/`.bin` distance files yet.
Solution: Use archived rationale/status evidence for prior scatter distances while preparing a `MockLodProfile` fallback: LOD0 near 20m, LOD1 start around 50m, far/cull 100-150m depending quality.
Rejected Alternatives: Invent hidden binary data; hard-code unlogged distances with no rationale.
Scalability potential: Low clamps far/cull down; Middle uses current defaults; High/Ultra expands far-card residency if profiler proves budget.
Hardware Impact: Cached squared thresholds avoid sqrt and buffer churn; expected 5-15 us per cull batch, PENDING VERIFICATION.

## Decision 03 - Deterministic Density Decimation
Problem: Steam Deck/MX350-class devices cannot absorb a fixed 150,000-instance visible budget when the camera opens onto a dense abyssal forest.
Solution: Add a deterministic hash decimation step to the Burst fallback and GPU compute culling path. The step is driven by Max Density, `ScalabilityTierProfiles.LowMx350`, and `SystemHealthSignal` pressure from `SignalBus`.
Rejected Alternatives: Random per-frame thinning, dynamic GameObject disabling, per-plant quality scripts, and CPU-side matrix deletion. Those approaches either shimmer, allocate, or create hierarchy churn.
Scalability potential: Low uses step 2-4 density thinning; Middle uses explicit Max Density; High keeps step 1 unless stressed; Ultra spends saved cycles on wider near LOD and shader overkill.
Hardware Impact: On i3/MX350 the expected gain is 0.4-2.0 ms GPU/CPU pressure reduction in saturated views because fewer instances enter append buffers, indirect args, shadows, and vertex work.

## Decision 04 - GPU Cull Telemetry Black Box
Problem: Without cull counts, overdraw failures become guesswork and crash/NaN diagnosis has no last-frame evidence.
Solution: Add a 4-counter GPU telemetry buffer for total, frustum/distance/density culled, HZB occluded, and visible. Read back asynchronously every 30 frames into a fixed 300-frame NativeArray ring and dump `Docs/AgentLogs/Dump_SHINOBU_09.bin` on invalid counter state.
Rejected Alternatives: Per-frame synchronous readback, CPU-only counters, logging every instance, and no telemetry. Synchronous readback stalls; CPU-only misses the real GPU path.
Scalability potential: Low samples sparse telemetry and uses overdraw warning at 50k visible; Middle keeps the same ring; High/Ultra can tune higher density from measured data.
Hardware Impact: Async readback amortized across 30 frames should stay below the 0.1 ms suspicion threshold. It prevents multi-ms blind overdraw regressions on low-end silicon.

## Decision 05 - Blind Mock Matrix Lane
Problem: Agent 08/DataVault output may be absent, but BRG, HZB, and frustum culling must be testable in isolation.
Solution: Add `MockMatrixGeneratorJob` filling persistent `NativeList<Matrix4x4>` and `NativeList<HectonVegetationInstanceData>` with a deterministic 100x100 flat grid using LCG-style hashes, then bind through the existing native array upload path.
Rejected Alternatives: Waiting for another agent, generating managed arrays every test, scene-placed MeshRenderers, or `GameObject.Instantiate`.
Scalability potential: Low validates 10k mock plants; Middle/High/Ultra can raise grid dimensions up to the 150k cap for stress testing.
Hardware Impact: No runtime-frame saving; it removes integration stalls and makes cull/LOD/HZB profiling possible before the producer domain is ready.

## Decision 06 - Editor Diagnostics Facade
Problem: LOD and density tuning by inspector guessing hides HZB failures and visible-count spikes.
Solution: Add `Scatter Diagnostics` EditorWindow with telemetry chart, live LOD0/LOD1/Max Density sliders, 100x100 mock generation, SceneView bounds, and an editor-only `OnDrawGizmos` hook.
Rejected Alternatives: Console spam, profiler-only workflow, runtime UI, or material-property tuning per instance.
Scalability potential: Low shows when density decimation is active; Middle shows normal cull ratios; High/Ultra exposes whether overkill density is still under budget.
Hardware Impact: Editor-only. It protects low-end targets by exposing >50k visible overdraw before the scene ships.

## Decision 07 - ARM64 Struct Alignment
Problem: `Pack = 1` on GPU-facing scatter structs is hostile to ARM64/Vulkan/Metal alignment and can create buffer layout faults.
Solution: Remove `Pack = 1` from `GpuScatterFloraInstanceData`, `ScatterFrameConstants`, and `ScatterBlackBoxEntry` while retaining explicit 16-byte-multiple sizes and the existing `UnsafeUtility.SizeOf` audit.
Rejected Alternatives: Trusting compiler packing, adding byte fields manually, or interleaving matrices and metadata into an 80-byte opaque blob.
Scalability potential: Low/Middle/High/Ultra all keep Matrix4x4 at 64 bytes, metadata at 64 bytes, Vector4 payload at 16 bytes, and constant buffers at 176 bytes.
Hardware Impact: Prevents ARM64 alignment crashes on Steam Deck-like Vulkan paths and mobile-class GPUs; performance gain is stability, not raw frame time.

## Decision 08 - Polish Fallback
Problem: The batch file contains no `<POLISH_MANDATE>` tag after all SHINOBU_09 tasks were checked.
Solution: Execute the local equivalent: forbidden-pattern scan, alignment scan, Dear Lie audit, diagnostics audit, and build-wall capture.
Rejected Alternatives: Pretend the tag existed; skip polish; expand outside render domain to repair unrelated build errors.
Scalability potential: Low/Middle/High/Ultra remain governed by density step, HZB, LOD split, and shader fake current/wake paths.
Hardware Impact: No direct frame-time gain. The audit prevents regressions that would cost 0.2-3.0 ms on low-end devices if GameObject, shadow, or overdraw paths leaked back in.

## Decision 09 - Ultra Polish Corrections
Problem: The ultra mandate exposed four real rot points: mock NativeLists were runtime-visible, telemetry entries were 36-byte structs, disabled telemetry risked an unbound compute RW buffer if naively released, and the adjacent scatter manager still allowed 1024-thread kernels through its Metal guard.
Solution: Move mock matrix storage/job/API under `UNITY_EDITOR`, pad flora/scatter telemetry structs to 40 bytes, dump SHINOBU blackbox to both `.bin` and `.h8dump`, keep a 16-byte dummy telemetry counter buffer bound even when sampling is disabled, and clamp the adjacent scatter manager's Metal thread-group guard to 512.
Rejected Alternatives: Delete the blackbox NativeArray to appease H-Phi wording; route fatal crash telemetry through file I/O every frame; rerun full project builds already blocked by unrelated missing DLLs/Core errors.
Scalability potential: Low avoids runtime mock-state residency and 1024-thread compute surprises; Middle/High keep telemetry and diagnostics; Ultra still spends saved cycles on denser near LOD and richer shader motion.
Hardware Impact: Mock eviction saves diagnostic-only native residency in player builds; 40-byte telemetry entries avoid ARM64 odd-stride reads; 512 thread-group guard protects mobile/Metal paths; dummy counter binding prevents compute dispatch errors for the cost of 16 bytes.

## Decision 10 - CSV Hot Reload Bridge
Problem: The editor facade had live sliders, but the ultra mandate explicitly demanded a human-readable CSV-to-binary bridge so designers can tune unmanaged scatter constants without recompiling C#.
Solution: Add editor-only CSV import/export and hot reload for `lod0,lod1,maxDensity,minimumDensityStep`, plus `.h8bin` bake with magic/version and fixed scalar order. Runtime receives only a typed setter and read-only minimum-step getter.
Rejected Alternatives: Runtime file polling, ScriptableObject mutation during gameplay, reflection into private serialized fields, or changing Contracts assemblies for a tuning-only editor bridge.
Scalability potential: Low can ship tighter CSV density/LOD profiles for MX350/Steam Deck; Middle/High/Ultra can author wider near LOD and denser visuals from the same bridge without code churn.
Hardware Impact: 0 us normal player-frame cost. The bridge prevents designer recompilation loops and makes low-tier density clamps explicit, while all File I/O and string parsing stay editor-only.

## Decision 11 - Compile Wall Boundary
Problem: After adding the CSV bridge, a limited Core compile check needed to distinguish SHINOBU regressions from unrelated concurrent-domain failures.
Solution: Restore only `Hecton8.Core.csproj` with its explicit Temp obj path, then run one no-restore serial build. The build reached source compilation and stopped in Construction, not SHINOBU.
Rejected Alternatives: Full project/Unity import rebuild spam; editing Construction pathfinding DTOs from the BRG scatter domain; ignoring compile verification after touching runtime C#.
Scalability potential: Compile-loop protection matters equally on all hardware tiers because concurrent agents are mutating separate domains; SHINOBU remains isolated to render/editor files.
Hardware Impact: 0 us runtime. Developer iteration impact is bounded to one targeted restore/build and `dotnet build-server shutdown`.

## Decision 12 - Material Binding Signature Cache
Problem: `ApplyMaterialBindings()` was called for every BRG/indirect pass and rewrote the same material buffers, floats, vectors, and GPU-indirect keyword state even when the binding signature had not changed. That is unnecessary render-thread/main-thread pressure in the scatter hot path.
Solution: Add a small per-pass `MaterialBindingState` value cache keyed by material, matrix/data/age/phase/snap/visible buffers, AUP offset, LOD constants, impostor dimensions, pass mode, and GPU-indirect mode. If the signature matches, the material write block is skipped.
Rejected Alternatives: MaterialPropertyBlock per renderer, per-frame global shader writes for per-pass state, or moving this into a new renderer abstraction. Those would either break SRP batcher discipline, increase global state churn, or widen scope in a massive owner file.
Scalability potential: Low/MX350 reduces redundant CPU-side material dirty work across near/far/depth/shadow/motion passes. High/Ultra keep the same path while spending saved CPU budget on denser near-field visuals.
Hardware Impact: Estimated 10-80 us CPU saved in stable-view frames depending enabled pass count, PENDING PROFILER VERIFICATION. Correctness is guarded by exact buffer/reference/scalar signature matching.

## Decision 13 - External Compile Wall Refresh
Problem: After material binding changes, a targeted compile check was needed again, but concurrent domains changed the Core wall.
Solution: Re-run one no-restore serial `Hecton8.Core.csproj` build after the previous restore. It reached source compile and failed in non-SHINOBU files: `GlobalWorldSampler`, `BinaryLayoutManifest`, and `EcosystemRuntimeInstaller`.
Rejected Alternatives: Editing world sampler/ecosystem/binary manifest from the BRG scatter task; running repeated full Unity imports; claiming green compile from static scans.
Scalability potential: Compile isolation protects all tiers by keeping the render task from absorbing unrelated ecosystem/world ownership.
Hardware Impact: 0 us runtime. Developer iteration impact bounded with `dotnet build-server shutdown`.

## Decision 14 - CPU Fallback Scratch Cache
Problem: The BRG `OnPerformCulling` CPU fallback still allocated `Allocator.TempJob` scratch lanes for visibility masks, culling planes, and headlight payloads. That is short-lived native churn in a render callback and a hitch risk when GPU indirect culling is unavailable or disabled.
Solution: Add two persistent scratch buffers owned by `HectonIndirectVegetationRenderer`, sized by `NextPowerOfTwo(instanceCount)`, and rotate them by active `JobHandle`. If both are still busy, emit the existing all-visible draw output instead of blocking the render callback. Data and scratch arrays now use deferred `NativeArray.Dispose(JobHandle)` when culling jobs are in flight; synchronous `Complete()` is limited to already-completed handles before reuse.
Rejected Alternatives: Keep TempJob allocations; call `Complete()` in the culling callback until a scratch buffer frees; create a third-party pool or cross-domain DataVault contract mid-batch. TempJob churn causes MicroSD/Steam Deck hitch risk indirectly through allocator pressure; blocking the render callback is worse; changing DataVault contracts would widen compile walls.
Scalability potential: Low/MX350 avoids native allocation churn and render-thread stalls in CPU fallback. Middle keeps the same path as a safety net under GPU path failure. High/Ultra can spend the stable fallback budget on wider near LOD and richer shader sway while GPU culling remains the primary route.
Hardware Impact: Estimated 20-120 us CPU hitch-risk reduction on fallback frames, PENDING PROFILER VERIFICATION. H-Phi caveat: these scratch arrays remain renderer-local because no existing Vault fallback scratch contract exists; they are bounded, sentinel-registered, persistent, and not allocated per cull tick.

## Decision 15 - Hidden Boxing Guard
Problem: The culling/lifecycle path still used `JobHandle.Equals(default)` and BRG `Batch*ID.Equals(default)`. Even if Unity structs inline this in some backends, it is an avoidable risk under the zero-GC/no-boxing rule and makes the hot path depend on struct `Equals` implementation details.
Solution: Replace scratch job-handle default checks with explicit `ActiveHandleValid`, `_cpuCullingDataDisposeHandleValid`, and `_cpuCullingScratchDisposeHandleValid` booleans. Replace SHINOBU BRG handle default checks with raw `.value` comparisons. For external producer handles, rely on `IsCompleted`; default handles report completed and do not need an equality test.
Rejected Alternatives: Trusting JIT/Burst to devirtualize struct `Equals`; introducing wrapper objects for handle state; touching sibling BRG renderers outside SHINOBU. The first is unverifiable without IL/profiler proof, the second allocates/couples, the third violates domain scope.
Scalability potential: Low removes a hidden managed-edge risk from the render callback. Middle/High/Ultra get the same deterministic handle checks while the GPU path remains primary.
Hardware Impact: Estimated 0-5 us direct, PENDING PROFILER VERIFICATION. The real value is eliminating a possible hidden boxing/GC regression and making the CPU fallback path mechanically auditable.

## Decision 16 - Compute Binding Signature Cache
Problem: GPU culling dispatched every frame and rebound the same matrix/data/age/visible/telemetry buffers to compute kernels even when buffer identity was unchanged. Constants must change with camera/depth/LOD, but stable buffer bindings do not need repeated `SetBuffer` calls.
Solution: Add value-state caches for main cull, shadow cull, clear snap, and flag snap kernels. Cache keys are compute shader reference, kernel index, matrix/data/age/snap/visible/telemetry buffers, and kernel role. Buffer identity changes reset the relevant caches during visible buffer, telemetry buffer, snap buffer, and GPU-resource release paths.
Rejected Alternatives: Cache all compute constants, which risks stale camera/depth truth; move culling to a new renderer abstraction; leave redundant bindings because profiler proof is absent. Constants remain live per-dispatch; only stable GraphicsBuffer bindings are skipped.
Scalability potential: Low/MX350 reduces CPU/render-thread binding overhead around dense cull dispatches. Middle keeps identical semantics. High/Ultra spend saved CPU budget on larger near LOD residency and shader visual overkill without increasing gameplay truth.
Hardware Impact: Estimated 5-40 us CPU saved in stable-buffer GPU-cull frames depending shadow/snap kernels enabled, PENDING PROFILER VERIFICATION. No GPU visual change intended.

## Decision 17 - Headlight Payload Upload Gate
Problem: The GPU cull path uploaded four scooter-headlight `Vector4[]` shader arrays every cull dispatch, then uploaded them again for the shadow kernel, even when `_HectonScooterHeadlightCount` was zero. The compute shader breaks out before reading those arrays when count is zero, so this was pure CPU/render-thread binding churn on most no-headlight frames.
Solution: Add `ApplyScooterHeadlightPayloadToCullCompute`. The main cull always writes the count, uploads arrays only when `headlightCount > 0`, and the shadow cull repeats only the count because the array properties are global to the compute shader and already valid after the main dispatch.
Rejected Alternatives: Cache every headlight vector by value; zero-upload arrays on count transitions; move darkness culling to CPU raycasts. Per-vector hashing costs more than two headlights justify; zero-upload is unnecessary because shader count gates reads; CPU raycasts violate the Dear Lie rule.
Scalability potential: Low/MX350/Steam Deck skip four `SetVectorArray` calls on normal no-headlight forest frames and avoid duplicate shadow uploads when headlights are active. Middle/High/Ultra keep the same cheap dot/cone darkness fake while spending saved CPU budget on wider visible flora density and richer shader motion.
Hardware Impact: Estimated 3-25 us CPU/render-thread saved in GPU-cull frames depending shadow pass and driver overhead, PENDING PROFILER VERIFICATION. No gameplay truth or visual physics path was added.

## Decision 18 - Motion Vector and Headlight Scrub Cache
Problem: The renderer still scrubbed all local headlight payload arrays before every copy, even though `MantaScooter` publishes a dense count-gated payload and shader/job consumers never read beyond `headlightCount`. Motion-vector materials also received `_HectonPreviousCameraPosition` every render even when the same material and value were already bound.
Solution: Remove the redundant renderer-side clear and add a per-material `MaterialVectorBindingState` for previous-camera motion vectors. Material release resets the vector binding state, so recreated material clones still receive the required value on first use.
Rejected Alternatives: Keep scrub clears for psychological safety; hash scooter arrays every frame; move previous-camera data into a broader material CBuffer mid-batch. Count-gated reads make scrubs unnecessary, array hashing costs more than two headlights justify, and CBuffer migration is too wide for this isolated render-polish pass.
Scalability potential: Low/MX350 removes eight Vector4 zero writes from normal darkness checks and skips redundant motion material writes in static or single-camera frames. Middle/High/Ultra keep exact motion-vector semantics and spend saved CPU budget on denser visible flora and shader-side overkill.
Hardware Impact: Estimated 1-15 us CPU saved across darkness/motion-vector frames, PENDING PROFILER VERIFICATION. Runtime allocation delta remains 0 B.

## Decision 19 - Indirect Args Clear Signature Cache
Problem: Every GPU-cull frame cleared multiple indirect args buffers through the same compute kernel and rewrote mesh index constants even when the near mesh was reused for LOD0 and shadow clears. `Mesh.GetIndexCount`, `GetIndexStart`, `GetBaseVertex`, and three compute `SetInt` calls were repeated without a data change.
Solution: Add `IndirectArgsClearBindingState` keyed by compute shader, kernel, args buffer, mesh, and submesh. The clear path always dispatches per target args buffer, but it skips stable args-buffer binding and mesh constants when the signature did not change. The cache resets with the cull compute binding states when GPU indirect resources are released.
Rejected Alternatives: Replace the clear kernel with CPU `SetData`; batch all args clears into a new compute kernel; leave repeated constants because the cost is small. CPU `SetData` is the wrong direction for GPU sovereignty, a new multi-clear kernel widens shader contract risk, and repeated constants are avoidable hot-path churn.
Scalability potential: Low/MX350 avoids duplicate constant writes during near/shadow clear sequences. Middle/High/Ultra keep the exact indirect args flow and can spend the small CPU savings on denser visible flora or richer shader motion.
Hardware Impact: Estimated 2-20 us CPU/render-thread saved on GPU-cull frames depending far/shadow path count, PENDING PROFILER VERIFICATION. Runtime allocation delta remains 0 B.
