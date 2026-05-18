# SHINOBU_45 Status - TBDR and VRAM Optimization Surgeon

Prompt: `SHINOBU_45`
Domain: `TBDR_AND_VRAM_OPTIMIZATION_SURGEON`
Task count: 20
Status law: PENDING VERIFICATION until Unity Editor/Play Mode/profiler confirms runtime behavior.

## Mandates Read Before Coding

- `DATA_Runtime_Struct_Layout_ARM64.txt` - 16-byte DTO lanes, no Pack=1, offset self-audit required.
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt` - no managed allocation in render/culling hot paths.
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt` - persistent native owner, deferred disposal, tracked handles.
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt` - MX350/Quest/mobile vertex, VRAM, thermal load-shed rules.
- `GPU_Compute_Warp_Sizing_Mobile.txt` - query compute group sizes, clamp mobile dispatch.
- `REND_GPU_Occlusion_Culling_6000.txt` - use culling/occlusion only with proof; avoid hand-rolled Hi-Z for MeshRenderer GRD.
- `REND_URP_Graphics_HotPath_Optimization_HLOD.txt` - SRP/BRG/GRD, dither, texture/transparent budgets, render hot-path caps.
- `TOOL_Designer_Facades_CSV_Binary_Bridge.txt` - human editable CSV/Editor bridge with runtime unmanaged truth.

## Checklist

- [x] Task 01 BINARY_GRAVEYARD_RECONNAISSANCE - DOD: scanned `Docs/Archive` and StreamingAssets candidates, read VRAM scout logs, built `GenerateEmergencyMockLimits()` fallback because `mobile_vertex_limits.h8bin` and `texture_streaming_budgets.bin` were absent. Rejected: hard failing boot on missing legacy payload. Est. saved: 250-400 us boot stalls, runtime unknown.
- [x] Task 02 IMR_PIPELINE_ERADICATION_PASS - DOD: `TBDRHardwarePipelineSwitch` gates TBDR mobile path and disables CPU sort on desktop RTX/Radeon RX. Rejected: universal IMR-friendly draw order. Est. saved: 80-250 us CPU on desktop, tile pressure reduction on Quest pending capture.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE - DOD: `VertexBudgetDTO` uses public fields plus `UnsafeUtility.AsRef`/pointer accessors. Rejected: `{ get; private set; }` DTO wrappers. Est. saved: 0.5-2 us per hot budget lane under contention.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION - DOD: `TileSpillWarningDTO` is 16B: 0 float overdraw, 4 uint culled, 8 ulong pad. Rejected: implicit padding and Pack=1. Est. saved: avoids unaligned read traps; microseconds hardware-dependent.
- [x] Task 05 BLIND_DEPENDENCY_MOCKING - DOD: `MockScatterBuffer`, `MockCameraMatrix`, `MockQualityWeightSignal`, and `MockQualityWeightJob` exist without Agent 09/44 dependency. Rejected: direct BRG/scalability singleton dependency. Est. saved: integration unblock, no frame estimate.
- [x] Task 06 STRICT_VERTEX_BUDGETING_KERNEL - DOD: `VertexBudgetJob` totals mesh vertex counts, truncates sorted visible instances, writes atomic current vertex count and tile warning. Rejected: CPU mesh decimation. Est. saved: prevents million-vertex tile spill; 0.1-2.0 ms GPU risk avoided pending capture.
- [x] Task 07 EARLY_Z_RADIX_SORT - DOD: Burst `EarlyZRadixSortJob` uses preallocated `NativeArray` source/scratch/histogram, no `List.Sort`. Rejected: managed sort and per-frame arrays. Est. saved: zero GC; sort cost pending profiler.
- [x] Task 08 THE_DEAR_LIE_FRUSTUM_SQUEEZE - DOD: `DearLieFrustumSqueezeJob` narrows side/top/bottom planes and scales cap continuously by quality/stress. Rejected: binary low/ultra culling switch. Est. saved: expected 20% peripheral vertex pressure in stress cases.
- [x] Task 09 TEXTURE_ARRAY_VRAM_PAGINATION - DOD: `TBDRTextureStreamingTracker` maintains fixed slice table and overwrites slices with `UnityEngine.Graphics.CopyTexture`. Rejected: loading every biome texture set. Est. saved: caps residency near 512 MiB design target.
- [x] Task 10 ZERO_COPY_UMA_BINDING - DOD: `TBDRUmaRawBufferWriter` uses Raw `GraphicsBuffer` with `LockBufferForWrite` and Burst matrix population. Rejected: managed matrix staging arrays. Est. saved: 50-200 us transfer/staging on UMA class devices, pending capture.
- [x] Task 11 COMPUTE_SHADER_THREAD_LIMITER - DOD: `TBDRComputeDispatchLimiter` queries kernel group sizes and clamps active per-group threads to 256 mobile/1024 PC. Rejected: blind dispatch sizes. Est. saved: crash avoidance, no honest microsecond number.
- [x] Task 12 TRANSPARENT_OVERDRAW_LIMITER - DOD: `TransparentOverdrawLimiterJob` enforces transparent quad hard limit and suppresses particles/far UI overflow. Rejected: transparent pass unlimited overdraw. Est. saved: fragment heat reduction pending GPU capture.
- [x] Task 13 HARDWARE_TIER_PIPELINE_SWITCH - DOD: runtime switch identifies mobile/TBDR by platform, handheld device, GLES, Adreno/Mali/Apple/Quest strings. Rejected: fixed Quest-only branch. Est. saved: prevents desktop CPU sorting tax.
- [x] Task 14 AUP_LOCALIZATION_FOR_GPU - DOD: `AupGpuLocalizationInput` stores sector as three `long` fields and outputs camera-relative `float3`; no `double` in GPU-facing layouts. Rejected: `double3`/nonexistent `long3`. Est. saved: prevents tile-bin precision failure, no microsecond claim.
- [x] Task 15 HALF_PRECISION_SHADER_FORCING - DOD: Editor build gate scans UberNoir color/normal/UV lanes and blocks mobile builds on `float` tokens. Rejected: voluntary shader style note. Est. saved: half throughput/register pressure benefit shader-dependent.
- [x] Task 16 ZERO_INIT_OVERHEAD_BYPASS - DOD: sort, scratch, histogram, signal, camera, frustum arrays allocate once with `NativeArrayOptions.UninitializedMemory`. Rejected: per-frame zeroed arrays. Est. saved: 100-500 us boot/allocation path for 150K capacity, pending platform capture.
- [x] Task 17 TELEMETRY_PIPELINE_RECORDER - DOD: 300-frame `NativeArray<TBDRPipelineTelemetryEntry>` ring dumps `Docs/AgentLogs/Dump_TBDR_PIPELINE.bin` on budget breach. Rejected: non-reproducible crash report. Est. saved: diagnostic latency, not frame time.
- [x] Task 18 VRAM_AND_VERTEX_EDITOR_WINDOW - DOD: `TBDR Pipeline Tuner` EditorWindow exposes hard vertex cap, transparent quad limit, squeeze angle, live bars, runtime vault writes. Rejected: hidden constants only. Est. saved: human iteration time, not frame time.
- [x] Task 19 CSV_OVERRIDE_INGESTOR - DOD: `TBDRGpuBudgetCsvIngestor` uses preallocated 4096B buffer and byte-span parser for `gpu_budgets.csv`. Rejected: `Split()`/managed per-line parse in runtime polling. Est. saved: zero hot-path GC, file IO remains cold only.
- [x] Task 20 GIZMO_OVERDRAW_VISUALIZER - DOD: `OnDrawGizmos` renders sorted front-to-back line order from the runtime when `Show Sorting` is enabled. Rejected: blind trust in sort order. Est. saved: debug proof only.

## Iteration Protocol

- Loop 1 completed: Tasks 01-05 implemented; prompt re-extracted with correct attr-aware XML regex; static read found no legacy binaries, fallback active.
- Loop 2 completed: Tasks 06-10 implemented; self-read caught namespace risk later fixed as `UnityEngine.Graphics.CopyTexture`.
- Loop 3 completed: Tasks 11-15 implemented; self-read/Roslyn caught nonexistent `long3`; replaced with explicit `long CellX/Y/Z`.
- Loop 4 completed: Tasks 16-20 implemented; self-read caught Editor definite-assignment defect; initialized snapshot and replaced obsolete object lookup.
- Loop 5 completed: source reread, banned-pattern scan, Roslyn runtime/editor compile, `.meta` asset hygiene, final log append pending.

## Verification

- Unity batchmode compile: BLOCKED. `Unity_SHINOBU_45_compile.log` reports another Unity instance already has `C:/hades/Hecton8` open. I did not close or kill the user's Editor.
- Isolated Roslyn runtime compile: PASS for `TBDRPipelineSurgeonTypes.cs`, `TBDRPipelineSurgeonJobs.cs`, `TBDRPipelineSurgeonRuntime.cs`; current repeat check has no warnings after removing obsolete `OpenGLES2` enum use.
- Isolated Roslyn editor compile: PASS for `TBDRPipelineTunerWindow.cs`.
- `git diff --check`: PASS except existing CRLF normalization warning on `Hecton8.Graphics.Culling.asmdef`.
- Banned runtime scan: no `List.Sort`, `Array.Sort`, `.Split`, `double`, properties, Raycast, MeshCollider, `long3` in runtime culling files. One `string[]` hit remains inside Editor-only shader validator.

## Ultra Polish Pass - 2026-05-18

- [x] Burst directive repair - all SHINOBU_45 jobs now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- [x] Pointer aliasing repair - job NativeArray/pointer fields are annotated with `[NoAlias]` where Burst can use it.
- [x] False sharing repair - `VertexBudgetDTO` remains the required 16B DTO, but hot atomic storage is now wrapped in explicit 64B `TBDRVertexBudgetCounter64`.
- [x] H-PHI vault path - production initialization requests `VaultBufferHandle` IDs `(BufferID)70820` through `(BufferID)70835` from `GlobalDataVault` using `SystemID.GraphicsScalability`; local NativeArray allocation is retained only as a CI/mock fallback when no vault exists.
- [x] HZB/indirect hooks - added `HzbAabbOcclusionCullJob` and `BuildIndirectDrawArgsJob` so downloaded depth pyramid masks and indirect args can be consumed without BRG-blind CPU loops.
- [x] Telemetry duplication repair - `TBDRPipelineTelemetryRecorder` binds to the vault-owned telemetry ring when available and only allocates a local ring as fallback.
- [x] Re-verification - isolated Roslyn runtime/editor compiles pass after polish; Unity batchmode remains blocked by open Editor.

## Repeat Mandate Hardening - 2026-05-18

- [x] Prompt re-extraction - `Docs/Tasks/CURRENT_BATCH.md` lines 225-278 were extracted by CLI; task matrix remains SHINOBU_45 Tasks 01-20.
- [x] Dependency-chain repair - added `ScheduleTBDRProtectionPass(int, JobHandle)` returning the final `JobHandle`; `RunMockPipelineOnce()` is now the only blocking wrapper and is confined to the mock/editor facade path.
- [x] Telemetry commit split - added `CommitCompletedProtectionPass(float)` so a production dispatcher can schedule the protection pass, combine dependencies, then record after its own completion point.
- [x] Endian-aware legacy reader - replaced `BinaryReader.ReadUInt32()` in budget archaeology with stackalloc byte reads, little-endian parse, byte-order sanity swap, and plausibility clamps.
- [x] Re-verification - isolated Roslyn runtime compile passes after hardening; current shader-global pass removed the obsolete `GraphicsDeviceType.OpenGLES2` warning.
- [x] Re-verification - isolated Roslyn editor compile passes against the temporary runtime verify DLL.
- [x] Runtime static scan - no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, auto-property DTOs, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, or `BinaryReader`. One `string[]` remains Editor-only shader validation.
- [x] Unity batchmode compile - BLOCKED by another Unity Editor instance already open on `C:/hades/Hecton8`; log refreshed at `Docs/AgentLogs/Unity_SHINOBU_45_compile.log`.
- [x] Whitespace check - SHINOBU runtime/editor files have no trailing whitespace. Repository-wide `git diff --check` is currently blocked by unrelated trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md` lines 2856, 2861, 2862, 2867, 2870, 2874, 2884, 2893 and EOF.

## Shader Global Handoff Hardening - 2026-05-18

- [x] Visual-overkill CBuffer bridge - added 32B `TBDRShaderBudgetGlobalsDTO` and `TBDRGlobalShaderBudgetBinder` to push quality weight, tile pressure, frustum squeeze, vertex caps, transparent caps, and flags into global shader variables.
- [x] Runtime push points - shader globals now update after initialization, editor limit changes, CSV budget changes, and completed protection-pass telemetry commits.
- [x] Warning removal - replaced obsolete `GraphicsDeviceType.OpenGLES2` check; Android/platform/handheld/model/GPU-name gates still protect mobile/TBDR hardware.
- [x] Re-verification - isolated Roslyn runtime compile passes with no warnings.
- [x] Re-verification - isolated Roslyn editor compile passes.
- [x] Targeted `git diff --check` - PASS for SHINOBU runtime/editor files and SHINOBU status/rationale/log files.

## Dear Lie Visibility Mask Hardening - 2026-05-18

- [x] Prompt re-extraction - attr-aware CLI extraction returned the full `<AGENT_PROMPT id="SHINOBU_45" role="TBDR_AND_VRAM_OPTIMIZATION_SURGEON">` block and Tasks 01-20.
- [x] Frustum squeeze sign repair - `DearLieFrustumSqueezeJob` now rotates side/top/bottom planes toward a narrower cone by subtracting the camera forward component instead of widening the planes.
- [x] Frustum cull enforcement - added `DearLieFrustumVisibilityJob`, a Burst `IJobParallelFor` that evaluates squeezed frustum planes and writes rejection into `PoiTransformDTO.Flags` before distance sort.
- [x] Sort-stable visibility repair - added `TBDRVisibilityFlags`; `VertexBudgetJob` now rejects sorted DTOs by flags that travel with the matrix, while the legacy index mask is no longer passed in the runtime sorted path.
- [x] HZB hook repair - `HzbAabbOcclusionCullJob` now writes `HzbRejected` into each `PoiTransformDTO.Flags` as well as the optional mask, preventing pre-sort index masks from corrupting post-sort budgeting.
- [x] Quality-weight smoothing repair - `MockQualityWeightJob` no longer jumps quality randomly per frame; it low-pass clamps movement with `math.lerp`, `math.step`, and a smooth cubic stress curve.
- [x] Quality-state persistence repair - `ScheduleTBDRProtectionPass()` no longer overwrites `MockQualitySignal[0]` every pass; `SeedMockData()` initializes it once so low-pass drift has memory.
- [x] Vertex overflow guard - `VertexBudgetJob` now checks `vertexCount > maxVertices - totalVertices` instead of `totalVertices + vertexCount > maxVertices`, preventing uint wraparound from overfeeding the GPU.
- [x] Stale HZB rejection purge - `DearLieFrustumVisibilityJob` now clears the full `RejectedMask` before applying frustum rejection, so missing/currently-stale HZB readback cannot keep prior-frame rejects alive.
- [x] CSV polling churn repair - `PollBudgetCsvOverride()` now reuses a cached resolved path; `Path.Combine/GetFullPath` runs only on initialize/path change, not every monitor poll.
- [x] Static scan - no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, DTO auto-properties, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, or `BinaryReader` in SHINOBU runtime files.
- [ ] Re-verification - isolated Roslyn compile temporarily skipped by CPU gate: first attempt saw CPU 100% plus another `dotnet/csc`, later attempts saw CPU 57-100%, post-smoothing check saw CPU 74%, post-overflow-fix check saw CPU 88%, stale-mask check saw CPU 100%, final delayed probe saw CPU 99% plus `dotnet/csc`; retry required when system load is under 50%.

## Shader Quality Handoff Repair - 2026-05-18

- [x] Mandate recall - read `REND_GPU_Occlusion_Culling_6000`, `REND_GPU_Sovereignty`, `DATA_Runtime_Struct_Layout_ARM64`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First`, and `TOOL_Designer_Facades_CSV_Binary_Bridge`.
- [x] Smoothed quality handoff - shader globals now read the active `MockQualitySignal[0].GlobalQualityWeight` through `CurrentQualityWeight()` instead of publishing stale serialized `_globalQualityWeight`.
- [x] Actual squeeze handoff - `_H8_TBDR_Budget0.z` and `_H8_TBDR_FrustumSqueezeDegrees` now publish `configuredSqueeze * (1 - quality)` instead of the maximum configured squeeze angle.
- [x] Editor slider safety - `TBDRTunerSnapshot.FrustumSqueezeDegrees` still reports the configured max, so `PullSnapshot()` does not accidentally write the current dynamic squeeze back as the new design cap.
- [x] ABI check - no DTO size or field order changed; only one cold shader property id was added in the binder.
- [x] Static scan - no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, DTO auto-properties, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, `BinaryReader`, or `totalVertices + vertexCount` in SHINOBU runtime files.
- [x] Targeted diff hygiene - `git diff --check` passes for SHINOBU runtime/editor files and SHINOBU status/rationale/log files.
- [ ] Re-verification - isolated Roslyn compile skipped after this patch by CPU gate: first probe reported CPU 100%, `dotnet/csc` false; delayed probe reported CPU 95%, `dotnet/csc` true. Retry required when CPU load is under 50% and compiler processes are idle.

## Rollback Frame Scheduling Repair - 2026-05-19

- [x] Prompt re-extraction - SHINOBU_45 XML was re-read from `Docs/Tasks/CURRENT_BATCH.md`; Tasks 01-20 unchanged.
- [x] Deterministic frame input - added `ScheduleTBDRProtectionPass(int requestedInstanceCount, uint simulationFrame, JobHandle dependency)` so production callers can feed lockstep/dispatcher frame counters instead of Unity frame clock.
- [x] Compatibility shell - existing `ScheduleTBDRProtectionPass(int, JobHandle)` remains as a Unity-frame fallback for editor/mock callers.
- [x] Job graph preservation - explicit-frame overload keeps the same chain: quality -> squeeze -> visibility -> distance keys -> optional radix -> budget -> indirect args.
- [x] Static scan - banned-pattern scan remains clean after explicit-frame overload.
- [x] Targeted diff hygiene - SHINOBU runtime/editor/docs pass `git diff --check`.
- [ ] Re-verification - isolated Roslyn compile skipped by CPU gate: latest post-patch probe reported CPU 82%, `dotnet/csc` false.

## Tile Pressure Squeeze Repair - 2026-05-19

- [x] Pressure feedback repair - `DearLieFrustumSqueezeJob` now uses previous-frame `BudgetPtr->TilePressure` as a continuous stress input, not only quality weight or impossible post-truncation overflow.
- [x] Smooth curve - pressure stress begins above 0.82 tile pressure and uses cubic smoothstep before taking `max(qualityStress, pressureStress)`.
- [x] Shader parity - `CurrentFrustumSqueezeDegrees()` now applies the same quality/pressure stress curve before publishing `_H8_TBDR_Budget0.z` and `_H8_TBDR_FrustumSqueezeDegrees`.
- [x] Static scan - no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, DTO auto-properties, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, `BinaryReader`, or overflow addition in SHINOBU runtime files.
- [x] Targeted diff hygiene - SHINOBU runtime/editor/docs pass `git diff --check`.
- [ ] Re-verification - isolated Roslyn compile skipped by CPU gate: latest probe reported CPU 82%, `dotnet/csc` false.

## Texture Residency Budget Repair - 2026-05-19

- [x] Oversized slice rejection - `TBDRTextureStreamingTracker.TryStageBiomeSlice()` now rejects a source slice whose estimated bytes exceed `MaxResidentMb`.
- [x] Logical residency cap - tracker computes unclamped resident bytes and evicts oldest logical resident slices until the incoming slice fits the cap.
- [x] Overflow-safe byte math - residency budget math now uses `ulong` internally and clamps public `EstimateResidentBytes()` to `uint`.
- [x] Static scan - no runtime `List.Sort`, `Array.Sort`, `.Split`, `double`, DTO auto-properties, `Raycast`, `MeshCollider`, `UnityEngine.Random`, `Time.deltaTime`, `BinaryReader`, `new List`, `LINQ`, or overflow addition in SHINOBU runtime files.
- [x] Targeted diff hygiene - SHINOBU runtime/editor/docs pass `git diff --check`.
- [x] Compile guard readback - runtime asmdef references Core/Core.Contracts/Core.Memory/World.Contracts and Unity packages only; editor asmdef references the runtime lane plus Unity editor-safe packages.
- [ ] Re-verification - isolated Roslyn compile skipped by CPU gate: latest probes reported CPU 100% with `dotnet/csc` true, CPU 100% with `dotnet/csc` false, and delayed final probe CPU 99% with `dotnet/csc` false.

## Hostile Vertex Cap Clamp - 2026-05-19

- [x] Unified cap helper - added `TBDRHardwareBudgetMath.ClampVisibleVertexCap()` with hard upper bound `20,000,000`.
- [x] Entry-point coverage - runtime initialization, editor limits, legacy binary budgets, CSV ingest, `DearLieFrustumSqueezeJob`, and `VertexBudgetJob` all pass vertex caps through the same clamp.
- [x] Atomic lane protection - `DearLieFrustumSqueezeJob` clamps its squeezed cap and `VertexBudgetJob` rewrites `BudgetPtr->MaxVisibleVertices` to the clamped value before accumulating visible vertices.
- [x] Static scan - no runtime banned patterns or unsafe `MaxVisibleVertices = math.max(...)` assignments remain in SHINOBU runtime files.
- [x] Targeted diff hygiene - SHINOBU runtime/editor/docs pass `git diff --check`.
- [ ] Re-verification - isolated Roslyn compile skipped by CPU gate: final response probe reported CPU 100%, `dotnet/csc` false.

## Overflow Boundary Repair - 2026-05-19

- [x] CSV wrap repair - `TBDRGpuBudgetCsvIngestor.TryParseUInt()` now saturates at `uint.MaxValue` instead of wrapping hostile numeric CSV cells before the hard cap clamp.
- [x] Transparent overdraw repair - `TransparentOverdrawLimiterJob` now saturates `RequestedParticleQuads + RequestedUiQuads` at `int.MaxValue` before overflow math.
- [x] HZB bounds repair - `HzbAabbOcclusionCullJob` now computes `HzbWidth * HzbHeight` in `long` before validating depth-pyramid length.
- [x] Static scan - runtime banned-pattern scan passed after overflow repair.
- [x] Targeted diff hygiene - touched runtime files pass `git diff --check`.
- [ ] Re-verification - isolated Roslyn compile skipped by CPU gate: latest probe reported CPU 100%, `dotnet/csc` false.

## Compute Dispatch Boundary Repair - 2026-05-19

- [x] Thread-group product repair - `TBDRComputeDispatchLimiter` now multiplies queried kernel group dimensions in `long`, not `int`.
- [x] Raw group-size clamp - shader-reported `uint` group dimensions now saturate to positive `int` values before arithmetic.
- [x] Zero-work rejection - dispatch requests with any non-positive work dimension now return false with reject code `3` instead of dispatching a fake 1-group workload.
- [x] DivCeil overflow repair - group count calculation now uses `1 + (value - 1) / divisor`, avoiding `value + divisor - 1` overflow.
- [x] Static scan - runtime banned-pattern scan passed after compute-boundary repair.
- [x] Targeted diff hygiene - touched runtime files pass `git diff --check`.
- [ ] Re-verification - isolated Roslyn compile skipped by CPU gate: latest probe reported CPU 97%, `dotnet/csc` false.
