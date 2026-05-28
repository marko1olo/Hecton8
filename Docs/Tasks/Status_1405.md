# Status_1405

Agent: 1405
Role: MOBILE_GPU_COMPUTE_SHADER_THREAD_GROUP_SIZER
Status: PENDING VERIFICATION
Batch source: Docs/Tasks/CURRENT_BATCH.md

## Loop 1: Tasks 01-05

- [x] Task 01: EXHAUSTIVE_VENDOR_COMPUTE_INQUISITION | DOD: scanned 23 Crest/GPUInstancer `.compute` files, extracted `numthreads`, `groupshared`, barrier, interlocked, append, and line data into `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json` | Alternative rejected: manual spot-check, because one missed vendor kernel can still TDR mobile GPUs | Estimate: 920039 us
- [x] Task 02: CSHARP_DISPATCH_LINKAGE_TRACING | DOD: traced GPUInstancer `ComputeShader.Dispatch` and Crest FFT `DispatchCompute` callers; identified hardcoded/constants path in GPUInstancer and non-scalar dispatch topology in Crest FFT | Alternative rejected: editing HLSL without CPU dispatch ownership proof | Estimate: 240000 us
- [x] Task 03: FFT_AND_SORT_ALGORITHM_DECONSTRUCTION | DOD: Crest FFT pass/butterfly ping-pong flow documented; `coord=t` and `coord2=t+256` rewrite selected for 512 variant only | Alternative rejected: shrinking `groupshared` arrays to 256, because SIZE=512 data still has 512 elements | Estimate: 310000 us
- [x] Task 04: CONSTANT_BUFFER_AND_PROPERTY_MAPPING | DOD: mapped `GPUI_THREADS`, `COMPUTE_SHADER_THREAD_COUNT`, `COMPUTE_SHADER_THREAD_COUNT_2D`, GPUInstancer presets, and generated PlatformDefines text | Alternative rejected: leaving C# and HLSL with separate 512 fallbacks | Estimate: 190000 us
- [x] Task 05: TELEMETRY_AND_REPORTING_PLANNING | DOD: report schema written to `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json` with modified files, hashes, violations, tests, and verification limits | Alternative rejected: chat-only proof | Estimate: 539395 us

## Loop 2: Tasks 06-10

- [x] Task 06: SHADER_NUMTHREADS_ANNIHILATION | DOD: Crest FFT 512 variants changed to 256 threads; GPUInstancer platform fallback thread macros clamped to 256; static validator reports 0 oversized vendor thread groups | Alternative rejected: disabling Crest FFT or GPUInstancer culling | Estimate: 624782 us
- [x] Task 07: GROUPSHARED_MEMORY_REFACTORING | DOD: Crest keeps `groupshared` SIZE=512 arrays and processes two coordinates per 256-thread group | Alternative rejected: multiple dispatch passes, because existing row/column group topology can preserve one-group shared memory locality | Estimate: 280000 us
- [x] Task 08: SYNCHRONIZATION_BARRIER_VALIDATION | DOD: no early returns added; each thread writes both coordinates before reaching the next `GroupMemoryBarrierWithGroupSync` | Alternative rejected: per-coordinate barrier, because it would not fix stage ownership and would add unnecessary sync points | Estimate: 210000 us
- [x] Task 09: CSHARP_DISPATCH_MATH_CORRECTION | DOD: GPUInstancer dispatches now route through integer ceil helpers and thread count clamps; 2D frame-count denominator uses `long` math to prevent int overflow; literal grep finds no stale `Mathf.CeilToInt(...COMPUTE_SHADER_THREAD_COUNT...)` dispatch math | Alternative rejected: float division by mutable thread constants | Estimate: 360000 us + 80000 us APEX5 denominator hardening
- [x] Task 10: VENDOR_ZERO_GC_OPPORTUNISTIC_PURGE | DOD: modified dispatch lines introduce no per-call arrays, delegates, strings, LINQ, or reference allocations; `GPUInstancerUtility.cs:2080` per-call `new int[2]` SetInts allocation was removed via cold cached property id and scratch array; billboard dilation now sanitizes `frameCount` before uniform upload and z-dispatch; touched runtime draw/upload/offset submit loops now use index `for` instead of `foreach` | Alternative rejected: broad vendor allocation refactor outside the surgical thread sizing surface | Estimate: 160000 us + 160000 us APEX fixes + 90000 us APEX4 loop cleanup

## Loop 3: Tasks 11-15

- [x] Task 11: BOUNDS_CHECK_INJECTION | DOD: GPUInstancer kernels guard buffer/texture tails; `CSBillboard.compute` now rejects `id.x >= frameWidth`, `id.y >= billboardSize.y`, and out-of-range neighbor Y before texture loads; Crest FFT exact power-of-two row/column dispatch has no tail group after two-coordinate rewrite | Alternative rejected: adding early returns before FFT barriers, which would violate group sync safety | Estimate: 155000 us + 140000 us APEX3 fix
- [x] Task 12: CREST_SPECIFIC_FFT_HARDENING | DOD: 512 horizontal and vertical passes both map `coord=t`, `coord2=t+256`; sign/output coordinate uses adjusted store ID | Alternative rejected: changing complex multiply/trig/butterfly values | Estimate: 300000 us
- [x] Task 13: COMPILE_WALL_AND_NAMESPACE_HYGIENE | DOD: no new using directives or vendor assembly references added | Alternative rejected: adding external parser/runtime dependencies for a local math helper | Estimate: 90000 us
- [x] Task 14: DRY_RUN_VERIFICATION_EXECUTION | DOD: report records thread 0 and thread 255 path through load, butterfly, barrier, and store | Alternative rejected: unproved two-element loop rewrite | Estimate: 140000 us
- [BLOCKED_BY_CONTENTION] Task 15: BATCHED_COMPILATION_AND_EXECUTION_CHECK | DOD: preflights sampled CPU=99 with active `dotnet` pid 33312, CPU=58 with active `dotnet` pid 66860, CPU=74 with no compiler process, guarded actual attempt CPU=71 with no compiler process, APEX2 CPU=100 with no compiler process, APEX3 CPU=100 with compiler process count 1, APEX4 CPU=100 with active `dotnet` pid 36612, and APEX5 CPU=100 with active `dotnet` pid 16488; build was not launched by rule | Alternative rejected: violating the build throttle gate | Estimate: 2700000 us

## Loop 4: Tasks 16-18

- [x] Task 16: MOCK_FFT_ALGORITHM_ASSERTION | DOD: added `Crest512FftTwoElementsPerThread_MatchesSingleElementReference` editor test that compares reduced 256-thread order against single-element reference order | Alternative rejected: claiming bit-perfect equivalence without a local executable assertion | Estimate: 260000 us
- [x] Task 17: DISPATCH_BOUNDARY_FUZZER_TEST | DOD: existing editor dispatch fuzzer covers prime 1,000,003, near-int-max group count, and 2D frame-count multiplier overflow boundary; retained and supplemented by centralized GPUInstancer integer helper | Alternative rejected: power-of-two-only dispatch tests | Estimate: 110000 us + 70000 us APEX5 test
- [x] Task 18: SHADER_SYNTAX_STRICTNESS_AUDIT | DOD: brace/paren/bracket scanner reports 0 structure problems; `git diff --check` reports no whitespace errors beyond line-ending notices | Alternative rejected: waiting for Unity import as the only syntax guard | Estimate: 705639 us

## Loop 5: Tasks 19-20

- [x] Task 19: ZERO_COMPILATION_HOT_PATH_VERIFICATION | DOD: static inspection confirmed no allocation constructs in modified dispatch lines; pre-existing vendor allocations remain documented as outside scope | Alternative rejected: fake 0 B profiler claim without Unity capture | Estimate: 130000 us
- [x] Task 20: AUTOMATED_METRIC_VALIDATOR_REPORT | DOD: final JSON report refreshed with current file hashes, macro-resolved thread group scan, zero oversized group count, zero SetInts/SetData/SetFloats `new` callsite pattern count, DataVault N/A proof, scalability caveat, and SHA sidecar `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json.sha256` | Alternative rejected: stale or numeric-only proof report | Estimate: 539395 us + 1010000 us APEX refresh

## Loop 6: APEX6 Residual Domain Audit

- [x] APEX6-01: GPUINSTANCER_GRASS_HEIGHTMAP_TAIL_AUDIT | DOD: inspected `CSInstancedRenderingGrassInstantiationKernel.compute`, CPU fallback `GetInstanceDataForDetailPrototype`, cell height-map extraction, and random range contract; found unguarded `heightMapData[heightIndex + 1]` in both GPU and CPU paths | Alternative rejected: relying only on normal Unity terrain dimensions, because vendor boundaries must fail closed | Estimate: 210000 us
- [x] APEX6-02: GPUINSTANCER_GRASS_BOUNDS_AND_PARITY_FIX | DOD: HLSL casts map dimensions once, rejects zero height-map dimensions before reads, fail-closes base/right/top/rightTop height samples through `FixBounds`; CPU fallback now clamps base/right neighbour and writes `rightBottomH` into `R.y` for parity with `Terrain.hlsl ComputeNormals` | Alternative rejected: changing grass density/visual generation algorithm | Estimate: 180000 us
- [BLOCKED_BY_CONTENTION] APEX6-03: FINAL_BUILD_PREFLIGHT | DOD: CPU samples `[100,100,100]`, compiler process count 2 (`csc` pid 34456, `dotnet` pid 40436); build not launched by rule | Alternative rejected: running `dotnet build` under active compiler contention | Estimate: 30000 us

## Loop 7: APEX7 Residual Tree Instancing Audit

- [x] APEX7-01: GPUINSTANCER_TREE_INTERLOCKED_CAPACITY_AUDIT | DOD: inspected `CSTreeInstantiationKernel.compute` and `GPUInstancerTreeManager.cs`; found shader writes after `InterlockedAdd` trusted CPU-side `instanceCount` with no shader-side `instanceCapacity` guard, and paired `treeData[index+1]` read had no explicit source-length uniform | Alternative rejected: assuming CPU count math is always correct at vendor boundary | Estimate: 150000 us
- [x] APEX7-02: GPUINSTANCER_TREE_BOUNDS_GUARD_FIX | DOD: shader now receives `treeDataLength` and `instanceCapacity`, rejects paired float4 reads if `index + 1 >= treeDataLength`, and rejects output writes if `instanceIndex >= instanceCapacity`; C# binds both uniforms before dispatch | Alternative rejected: changing tree generation data layout or disabling GPU tree instantiation | Estimate: 140000 us
- [BLOCKED_BY_CONTENTION] APEX7-03: FINAL_BUILD_PREFLIGHT | DOD: CPU samples `[100,100,100]`, compiler process count 1 (`dotnet` pid 40436); build not launched by rule | Alternative rejected: running `dotnet build` under CPU 100 with active dotnet | Estimate: 30000 us

## Loop 8: APEX8 Final Self-Audit

- [x] APEX8-01: TREE_LENGTH_BINDING_SELF_AUDIT | DOD: re-opened `GPUInstancerTreeManager.cs` after APEX7 and found the prior evidence was stale: the source length must be captured before `treeDataList` is nulled, and `treeScales[prototypeIndex]` also required an explicit shader-side length guard | Alternative rejected: relying on CPU prototype count discipline at the shader boundary | Estimate: 120000 us
- [x] APEX8-02: TREE_SCALE_AND_AFTER_NULL_FIX | DOD: `GPUInstancerTreeManager.cs:222-225` now stores `treeDataLength` and `treeScalesLength` before nulling source arrays; `GPUInstancerTreeManager.cs:256/258/260` binds `TREE_DATA_LENGTH`, `TREE_SCALES_LENGTH`, and `INSTANCE_CAPACITY`; `CSTreeInstantiationKernel.compute:15-16/34/42/55` guards tree data, tree scales, and output capacity | Alternative rejected: disabling GPU tree instantiation or mutating tree data layout | Estimate: 150000 us
- [BLOCKED_BY_CONTENTION] APEX8-03: REPORT_AND_FINAL_BUILD_PREFLIGHT | DOD: regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, sidecar SHA matches, file hash mismatches = 0; CPU samples `[100,100,100]`, compiler process count 1 (`dotnet` pid 40436), build not launched by rule | Alternative rejected: launching `dotnet build` under CPU 100 with active dotnet | Estimate: 30000 us

## Loop 9: APEX9 Residual Bounds Audit

- [x] APEX9-01: CREST_QUERY_COUNT_AND_TAIL_AUDIT | DOD: found `QueryBase.cs` capacity checks counted only point queries while normals expand to 3 query slots; added `countTotal`, checked `_maxQueryCount` and backing array capacity with total slots, bound `_QueryCount`, and guarded `QueryDisplacements.compute` / `QueryFlow.compute` before query-buffer reads | Alternative rejected: trusting caller-side counts across a ring-buffer vendor boundary | Estimate: 190000 us
- [x] APEX9-02: CREST_UNDERWATER_MASK_EDGE_GUARD | DOD: bound `_CrestOceanMaskWidth/_CrestOceanMaskHeight` and rejected dispatch-tail plus edge pixels in `CrestFillMaskArtefacts.compute` before all +/-1 neighbor loads | Alternative rejected: relying on render target load behavior outside texture bounds | Estimate: 120000 us
- [x] APEX9-03: GPUINSTANCER_GRASS_AND_TEXTURE_CAPACITY_GUARDS | DOD: grass instancing now binds `instanceCapacity` and rejects interlocked output indices beyond buffer capacity; texture copy kernels reject destination tail writes and C# dispatches by source/destination mip dimensions | Alternative rejected: assuming CPU metadata and full-size mip dimensions are always coherent | Estimate: 210000 us

## Loop 10: APEX10 Zero-GC Scalar Uniform Cleanup

- [x] APEX10-01: BILLBOARD_SETINTS_ARRAY_PURGE | DOD: removed the cached `int[2]` scratch and `SetInts` billboard-size path; `CSBillboard.compute` now consumes scalar `billboardWidth` / `billboardHeight` uniforms and `GPUInstancerUtility.cs` binds both via `SetInt` | Alternative rejected: retaining a cold managed array when scalar uniforms preserve the shader contract with less allocation surface | Estimate: 70000 us
- [BLOCKED_BY_CONTENTION] APEX10-02: REPORT_AND_FINAL_BUILD_PREFLIGHT | DOD: regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, sidecar SHA matches, file hash mismatches = 0; CPU samples `[100,100,100]`, compiler process count 1 (`dotnet` pid 46580, CPU 21.21875), build not launched by rule | Alternative rejected: launching `dotnet build` under CPU 100 with active dotnet | Estimate: 30000 us

## Loop 11: APEX11 Residual Buffer Contract Audit

- [x] APEX11-01: GPUINSTANCER_PARTIAL_COPY_CAPACITY_AUDIT | DOD: inspected `CSInstancedComputeBufferSetDataPartialKernel.compute` plus all `computeBufferSetDataPartialKernelId` dispatch routes; found shader write/read trusted source and destination buffer capacities | Alternative rejected: relying on CPU callsite discipline for a vendor fallback copy kernel | Estimate: 130000 us
- [x] APEX11-02: GPUINSTANCER_PARTIAL_COPY_CAPACITY_FIX | DOD: shader now receives `computeBufferCapacity` and `managedBufferCapacity`, rejects source lanes, uint addition overflow, destination overflow, and single-write overflow; C# binds capacities for single, partial, copy, and merge routes | Alternative rejected: adding a new copy layout or CPU readback validation | Estimate: 110000 us
- [x] APEX11-03: BILLBOARD_AND_BUFFER_TO_TEXTURE_CONTRACT_FIX | DOD: billboard frame count is clamped to physical width and shader rejects invalid frame metadata before division; buffer-to-texture kernel rejects args count drift past `bufferSize`, `maxTextureSize == 0`, and consumed `instanceId >= bufferSize`; C# binds `bufferSize` for normal and shadow transform texture dispatches | Alternative rejected: allowing invalid metadata to produce silent empty atlas writes or OOB matrix reads | Estimate: 160000 us
- [BLOCKED_BY_CONTENTION] APEX11-04: REPORT_AND_FINAL_BUILD_PREFLIGHT | DOD: regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, sidecar SHA matches, file hash mismatches = 0; CPU samples `[100,100,100]`, compiler process count 1 (`dotnet` pid 55080, CPU 673.46875), build not launched by rule | Alternative rejected: launching `dotnet build` under CPU 100 with active dotnet | Estimate: 30000 us

## Verification State

- Static oversized-thread validator: 0 violations across 86 vendor shader/include files; macro-resolved scan covers 59 thread group entries and reports max resolved group product = 256.
- APEX6 residual shader scan: 0 hits for raw `heightMapData[heightIndex + 1]`, `P.y = rightBottomH`, `uint(detailAndHeightMapSize`, `uint(floor`, `TX=512`, `TY=512`, `numthreads(512)`, `numthreads(1024)`, or 512/1024 GPUI macro definitions in the audited Crest/GPUInstancer surface.
- APEX11 contract scan: partial-copy dispatches = 4; capacity SetInt lines = 9; `CSInstancedBufferToTexture.compute` no longer contains `floor(id.x / float(maxTextureSize))`; `CSBillboard.compute` rejects invalid frame metadata before division.
- Zero-GC APEX11 scan: hot dispatch/helper production added lines = 147; 0 `new` reference/array hits, 0 `string.Format`, 0 `.ToString()`, 0 `foreach`, 0 LINQ, 0 SetData/SetInts/SetFloats `new` callsite pattern count. All modified production C# added-line scan count = 193; the only `new` reference hits are three cold `new GPUIRenderingSettings` preset initializers in `GPUInstancerSettings.cs`.
- Data Sovereignty: no GlobalDataVault migration in this vendor sizing pass; BufferID constants = 0; TryAcquireWriteLock = 0; release/finally proof N/A because no lock is acquired.
- C# build: BLOCKED_BY_CONTENTION, APEX11 preflight CPU samples `[100,100,100]`, compiler process count 1 (`dotnet` pid 55080, CPU 673.46875); build not launched because CPU > 50 and compiler process is active. Artifact: `Docs/AgentLogs/Build_1405_Apex11.summary.json`.
- Final report SHA-256: `87a0a54c3115d31baa2f899b7898642eca5a30a0edc802dabb0ff35a877f3cc6`.
- Unity shader import, EditMode tests, profiler, RenderDoc, GCMonitor: PENDING VERIFICATION.
