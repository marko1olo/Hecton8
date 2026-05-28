# Rationale_1405

Status: PENDING VERIFICATION

## 2026-05-28 Initial Scope

Problem: Vendor compute shaders may contain `numthreads` groups above mobile-safe occupancy limits and matching C# dispatch code may assume hardcoded group sizes.
Solution: Offline static audit first, then surgical edits only where the target vendor code proves oversized groups or stale dispatch math. DOD pattern: one shader fact -> one ledger row -> one modified file hash.
Rejected Alternatives: Blind global replace of `numthreads(512, 1, 1)` to `256` is rejected because FFT/sort kernels can depend on group-local shared memory and barriers.
Scalability potential: Low uses 64/128 where algorithm permits; Middle uses 128/256 only with bounds proof; High uses 256 if occupancy is safe; Ultra can keep visual density through dispatch count and fidelity, not unsafe group width.
Hardware Impact: Expected low-end benefit is avoiding register/shared-memory pressure and mobile TDR risk on i3/MX350-class and XR silicon. Exact microseconds saved are PENDING GPU CAPTURE.

## 2026-05-28 Crest FFT 512 Rewrite

Problem: `FFTCompute.compute` compiled 512-resolution horizontal/vertical kernels with 512 threads while also requiring 512 shared elements per row/column.
Solution: Keep `groupshared` SIZE=512, reduce kernel group width/height to 256, and make each group thread process `coord=t` and `coord2=t+256`. DOD pattern: data length unchanged, thread count reduced, barrier cadence unchanged.
Rejected Alternatives: Shrinking shared arrays to 256 would drop half the FFT row. Dispatching two separate 256-group passes would break group-local shared-memory FFT stages unless the algorithm were rewritten into global-memory passes.
Scalability potential: Low/Middle avoid 512-thread mobile pressure; High/Ultra keep 512-resolution ocean output because the saved occupancy is spent on preserving full wave fidelity, not reducing resolution.
Hardware Impact: Expected gain on i3/MX350/mobile XR is TDR/register-pressure risk removal. Microseconds saved are PENDING GPU CAPTURE.

## 2026-05-28 GPUInstancer Thread Constant Clamp

Problem: GPUInstancer HLSL fallback and C# runtime settings could emit 512 or 1024 thread assumptions.
Solution: Clamp default, PS4, Xbox, custom x512, custom x1024, and generated PlatformDefines fallback to 256. Dispatch group math now uses integer ceiling helpers.
Rejected Alternatives: Removing enum values would mutate public vendor API and break serialized settings. Leaving the enum but clamping execution preserves compatibility while removing unsafe runtime values.
Scalability potential: Low/Middle use 128 or 256 based on platform; High/Ultra scale visual density through more groups and instance counts, not illegal group width.
Hardware Impact: Expected gain is stable occupancy and correct tail coverage for prime instance counts. CPU impact of helper is trivial integer math; measured frame impact is PENDING PROFILER.

## 2026-05-28 Build Throttle

Problem: Compile validation was requested, but host CPU was sampled at 99% and an active `dotnet` process was already running.
Solution: Build was not launched. Status marked `BLOCKED_BY_CONTENTION`; static validators and source review used instead.
Rejected Alternatives: Launching `dotnet build` under contention violates batch rule and risks host instability.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected. Runtime impact remains PENDING VERIFICATION.

## 2026-05-28 APEX Zero-GC Recheck

Problem: APEX scan found `GPUInstancerUtility.cs:2080` still used `dilationCompute.SetInts("billboardSize", new int[2] { ... })` in the touched compute submission area.
Solution: Added a cold cached property id and a cold static `int[2]` scratch buffer at `GPUInstancerUtility.cs:21-22`; the dilation path now writes width/height into the scratch buffer and calls `SetInts(int, int[])` at current `GPUInstancerUtility.cs:2087-2089`. DOD pattern: remove per-call allocation from compute submission without changing shader contract.
Rejected Alternatives: Passing `SetInts(id, width, height)` was rejected because `params int[]` would still allocate at the callsite. Replacing the int2 uniform with `SetVector` was rejected because it changes typed shader binding semantics without Unity shader import proof.
Scalability potential: Low/Middle avoid avoidable managed churn during billboard dilation tooling; High/Ultra keep identical billboard output while avoiding a preventable managed allocation spike. This is not a GlobalQualityWeight scaler; it is allocation hygiene.
Hardware Impact: Expected host/editor allocation reduction is one short `int[2]` array per dilation call. Runtime microseconds saved are PENDING PROFILER.

## 2026-05-28 APEX Data Sovereignty and Scalability Check

Problem: Polish mandate required DataVault lock proof and continuous scalability proof for a pass that only changed vendor shader group sizing and dispatch math.
Solution: Verified modified Crest/GPUInstancer files contain 0 `GlobalDataVault`, 0 `BufferID`, 0 `TryAcquireWriteLock`, and 0 `ReleaseWriteLock` occurrences. No DataVault state was introduced, so no lock/finally route exists. Verified modified vendor files contain no `HomeostasisBrain`, `GlobalQualityWeight`, `isLowEnd`, `LowEnd`, `HighEnd`, or `Ultra` branches.
Rejected Alternatives: Injecting `HomeostasisBrain.GlobalQualityWeight` into vendor dispatch math was rejected because compute group width is fixed by shader `numthreads`; a dynamic quality scalar must scale workload/cadence/density from a first-party owner route, not desynchronize C# group math from HLSL thread declarations.
Scalability potential: Low/Middle/High/Ultra all use legal group widths; visual richness remains controlled by existing Crest/GPUInstancer fidelity inputs and future first-party quality routing. Current pass is a safety clamp, not a complete vendor quality scaler.
Hardware Impact: Removes oversized group crash risk on i3/MX350/mobile XR class hardware. Exact frame gain remains PENDING GPU CAPTURE.

## 2026-05-28 APEX Report Hash Refresh

Problem: `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json` contained a stale SHA for `GPUInstancerDetailManager.cs` after the working tree changed.
Solution: Regenerated the report with current file hashes and wrote sidecar `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json.sha256`. Guarded actual compile attempt then sampled CPU 71 and did not launch build. Final report SHA-256: `e5d1e936d628584bb8ba573d9d12f6469f1f5cafc6a677cca457a0d5c11dc98e`.
Rejected Alternatives: Leaving the stale report would make the evidence artifact invalid even if the code were correct.
Scalability potential: Not runtime-facing.
Hardware Impact: None. Evidence integrity only.

## 2026-05-28 APEX2 Macro-Resolved Thread Scan

Problem: The previous static proof field `maxNumthreadsProduct=64` was incomplete because it counted only literal numeric `[numthreads]` attributes and did not resolve FFT `TX/TY` pragmas or GPUInstancer `GPUI_THREADS` macros.
Solution: Rebuilt the report scanner to resolve Crest FFT `#pragma kernel ... TX/TY`, `GPUI_THREADS`, `GPUI_THREADS_2D`, Crest `THREAD_GROUP_SIZE_X/Y`, and local `GROUP_SIZE` defines. The resolved scan covers 59 entries; max group product is 256; violation count is 0.
Rejected Alternatives: Keeping the numeric-only proof was rejected because it understated the real maximum and could hide macro-expanded violations.
Scalability potential: Low/Middle/High/Ultra proof now matches real shader variant products instead of a regex artifact.
Hardware Impact: Evidence integrity only; no runtime change.

## 2026-05-28 APEX2 Billboard Dilation Frame Count Guard

Problem: `DilateBillboardTexture` routed `frameCount` through the safe group-count helper, but still uploaded and dispatched the raw `frameCount`. A zero or negative input could produce invalid z-dispatch or divide-by-zero in `CSBillboard.compute`.
Solution: Added `safeFrameCount` at current `GPUInstancerUtility.cs:2076` and used it for shader uniform upload at `GPUInstancerUtility.cs:2090` and z-dispatch at `GPUInstancerUtility.cs:2095-2096`.
Rejected Alternatives: Trusting all callers to pass a positive frame count was rejected because this is a vendor utility boundary and the helper already encoded the safe intent.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid frame counts; invalid input degrades to one-frame dilation instead of undefined GPU submission.
Hardware Impact: Prevents one invalid-dispatch failure class. Runtime microseconds saved are PENDING PROFILER.

## 2026-05-28 APEX2 Build Throttle

Problem: APEX2 requested final compile proof after further C# edits.
Solution: Preflight sampled CPU 100 and compiler process count 0. Build was not launched by rule. Summary written to `Docs/AgentLogs/Build_1405_Apex2.summary.json`.
Rejected Alternatives: Launching build at 100% CPU would violate the host contention protocol.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected.

## 2026-05-28 APEX3 Billboard Shader Bounds

Problem: `CSBillboard.compute` dispatches in 16x16 groups, so tail threads can exceed the frame rectangle. The shader rejected `id.x > frameWidth` instead of `id.x >= frameWidth`, did not reject `id.y >= billboardSize.y`, and did not guard neighbor Y after unsigned offset wrap.
Solution: Changed the primary guard to `id.x >= frameWidth || id.y >= billboardSize.y` at `CSBillboard.compute:38` and added `neighbourCoord.y >= billboardSize.y` before neighbor `Texture2D.Load` at `CSBillboard.compute:59`.
Rejected Alternatives: Relying on texture load clamping was rejected because `Texture2D.Load` integer coordinates do not provide a correctness contract for out-of-range vendor compute access.
Scalability potential: Low/Middle/High/Ultra all get identical valid-region dilation; tail work is discarded deterministically.
Hardware Impact: Prevents out-of-bounds texture load risk on non-multiple-of-16 billboard dimensions. Runtime microseconds saved are PENDING GPU CAPTURE.

## 2026-05-28 APEX3 Build Throttle

Problem: APEX3 requested final compile proof after shader and C# safety fixes.
Solution: Preflight sampled CPU 100 and compiler process count 1. Build was not launched by rule. Summary written to `Docs/AgentLogs/Build_1405_Apex3.summary.json`. Final report SHA-256: `108347b3c0cf76c90784c241caae7f147aea789f95a7b35339770bad91a8384c`.
Rejected Alternatives: Launching build while `csc/dotnet` are active and CPU is saturated would violate the host contention protocol.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected.

## 2026-05-28 APEX4 Runtime Foreach Cleanup

Problem: A range scan of touched GPUInstancer submit surfaces found pre-existing `foreach` loops in runtime draw/upload/offset methods adjacent to the dispatch-size fixes. The project mandate tolerates some `List<T>` foreach cases, but the APEX request demanded zero `foreach` in modified hot paths.
Solution: Replaced `foreach` over `runtimeDataList` with indexed `for` loops in `GPUInstancerUtility.cs` at `GPUIDrawMeshInstancedIndirect`, `DispatchBufferToTexture`, `SetGlobalPositionOffset`, and `SetGlobalMatrixOffset`. Updated report file hashes and SHA sidecar. Runtime added-line scan now reports 104 added lines, 0 `string.Format`, 0 `.ToString()`, 0 `foreach`, 0 LINQ, and one `new int[2]` only as cold static scratch cache.
Rejected Alternatives: Purging every remaining vendor `foreach` in `GPUInstancerUtility.cs` was rejected because the remaining hits are outside this dispatch-sizing surface and include cold/editor/setup paths; broad vendor loop refactor would increase regression surface without direct proof.
Scalability potential: Low/Middle avoid iterator-pattern ambiguity in frequent submit methods; High/Ultra keep the same render and upload behavior with less managed-runtime risk. This is still not a GlobalQualityWeight scaler.
Hardware Impact: Removes enumerator-pattern debt from touched submit loops. Exact microseconds saved are PENDING PROFILER.

## 2026-05-28 APEX4 Build Throttle

Problem: APEX4 requested final compile proof after the runtime loop cleanup and report refresh.
Solution: Preflight sampled CPU 100, sample1 100, sample2 100, compiler process count 1 (`dotnet` pid 36612, CPU 16.515625). Build was not launched by rule. Summary written to `Docs/AgentLogs/Build_1405_Apex4.summary.json`. Final report SHA-256: `ba289cae62c32ec06624535a28d6ae0325ae557a6e55625cca5346036e44a53e`.
Rejected Alternatives: Launching build at CPU 100 with an active `dotnet` process would violate the compilation resource throttle.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected.

## 2026-05-28 APEX5 2D Dispatch Denominator Hardening

Problem: `GPUInstancerConstants.GetComputeThreadGroupCount2D(int elementCount, int frameCount)` multiplied `COMPUTE_SHADER_THREAD_COUNT_2D * frameCount` as `int`. Normal billboard frame counts are small, but the helper is public and an extreme frame count could overflow the denominator and produce wrong dispatch math.
Solution: Changed the denominator to `long` at `GPUInstancerConstants.cs:55` and changed `GetThreadGroupCount` to accept `long` at `GPUInstancerConstants.cs:67-73`. Added `TwoDimensionalFrameCountMultiplier_UsesLongDenominator` in `ComputeDispatchSizingEditTests.cs:72-82`.
Rejected Alternatives: Relying on caller sanity was rejected because this is a central dispatch helper and the fix is pure integer math. Querying every kernel every frame was rejected for this pass because GPUInstancer already calls `SetPlatformDependentVariables` during manager setup and the immediate bug was arithmetic overflow, not a proven shader/C# mismatch.
Scalability potential: Low/Middle avoid undefined dispatch coverage under invalid frame metadata; High/Ultra keep identical workload for valid inputs. This does not add a GlobalQualityWeight scaler.
Hardware Impact: Prevents an extreme integer overflow class in billboard/2D dispatch math. Exact microseconds saved are PENDING PROFILER.

## 2026-05-28 APEX5 Build Throttle

Problem: APEX5 requested final compile proof after dispatch denominator hardening.
Solution: Preflight sampled CPU 100, compiler process count 1 (`dotnet` pid 16488, CPU 20.640625). Build was not launched by rule. Summary written to `Docs/AgentLogs/Build_1405_Apex5.summary.json`. Final report SHA-256: `4ad5f230044131675079ef33872280e6c7afd5c0bd622a3f163a2efe4bc4936a`.
Rejected Alternatives: Launching build at CPU 100 with an active `dotnet` process would violate the compilation resource throttle.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected.

## 2026-05-28 APEX6 GPUInstancer Grass Height-Map Tail Guard

Problem: A residual domain audit found `CSInstancedRenderingGrassInstantiationKernel.compute` and the Android/Vulkan CPU fallback both reading `heightMapData[heightIndex + 1]` without the same `FixBounds` protection already used for top/rightTop neighbours. The CPU fallback also wrote `rightBottomH` into `P.y`, diverging from `Terrain.hlsl ComputeNormals` where the right sample belongs to `R.y`.
Solution: In `CSInstancedRenderingGrassInstantiationKernel.compute`, map dimensions are cast to `uint` once, zero height-map dimensions return before buffer reads, the base height index is fail-closed, and right/top/rightTop samples use `FixBounds`. In `GPUInstancerDetailManager.cs`, the CPU fallback clamps `heightIndex`, clamps `heightIndex + 1`, and assigns `rightBottomH` to `R.y`.
Rejected Alternatives: Trusting normal Unity terrain sizes was rejected because this is vendor boundary code and the shader already used partial guard logic. Rewriting detail generation or reducing density was rejected because the fix is a bounds/parity correction, not a visual LOD change.
Scalability potential: Low/Middle/High/Ultra keep identical grass density and placement for valid inputs. Invalid or edge metadata now fails closed instead of reading outside height-map buffers; no binary `isLowEnd` branch or new simulation path was added.
Hardware Impact: Removes one GPU buffer OOB risk and one CPU fallback normal parity bug. Exact runtime microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX6 Build Throttle

Problem: APEX6 required final compile proof after C# fallback edits, but the host was under active compiler contention.
Solution: Preflight sampled CPU `[100,100,100]`, compiler process count 2 (`csc` pid 34456, CPU 1.4375; `dotnet` pid 40436, CPU 146.328125). Build was not launched. Summary written to `Docs/AgentLogs/Build_1405_Apex6.summary.json`. Final report SHA-256: `f42d46821dd74adfe143ab950bd13802b820edb2bc051d90110ca40de2f8a70c`.
Rejected Alternatives: Launching `dotnet build` at CPU 100 with active compiler processes would violate the compilation resource throttle.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected.

## 2026-05-28 APEX7 GPUInstancer Tree Instancing Capacity Guard

Problem: `CSTreeInstantiationKernel.compute` consumed two `treeData` float4 records per logical tree and wrote `gpuiInstanceData[instanceIndex]` after `InterlockedAdd`, but the shader did not receive explicit source length or output capacity. CPU-side `instanceCount` is expected to match, but vendor boundary code must fail closed if counts drift.
Solution: Added `treeDataLength` and `instanceCapacity` uniforms. The shader now returns before reading `treeData[index + 1]` when `index + 1 >= treeDataLength`, and returns before writing output when `instanceIndex >= instanceCapacity`. `GPUInstancerTreeManager.cs` binds `treeDataList.Count` and per-prototype `instanceCount`; `GPUInstancerConstants.TreeKernelProperties` owns the property IDs.
Rejected Alternatives: Disabling GPU tree instantiation was rejected because it would remove the intended vendor path. Recomputing tree counts on GPU was rejected because the existing CPU count is already available and the defect is missing bounds proof, not data ownership.
Scalability potential: Low/Middle/High/Ultra keep identical tree instance output for valid metadata. If counts drift, the shader skips invalid reads/writes instead of corrupting output buffers. No binary quality branch or new physical simulation was introduced.
Hardware Impact: Removes one GPU output-buffer OOB class. Exact runtime microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX7 Build Throttle

Problem: APEX7 required final compile proof after adding two C# property IDs and two shader uniforms.
Solution: Preflight sampled CPU `[100,100,100]`, compiler process count 1 (`dotnet` pid 40436, CPU 440.484375). Build was not launched. Summary written to `Docs/AgentLogs/Build_1405_Apex7.summary.json`. Final report SHA-256: `e08535ac8fa03f04f151285fb041a45f7db4bf1f951061eb5d7b80826551364c`.
Rejected Alternatives: Launching `dotnet build` at CPU 100 with active `dotnet` would violate the compilation resource throttle.
Scalability potential: Not runtime-facing.
Hardware Impact: Host CPU protected.

## 2026-05-28 APEX8 Tree Scale Length and After-Null Fix

Problem: APEX7 self-audit exposed a real compile/stability risk in the tree bounds patch. The source `treeDataList.Count` proof was unsafe if read after `treeDataList = null`, and `treeScales[prototypeIndex]` still trusted prototype metadata without a shader-side source-length guard.
Solution: `GPUInstancerTreeManager.cs:222-225` now captures `treeDataLength` and `treeScalesLength` before nulling source containers. `GPUInstancerConstants.cs:274-276` owns `TREE_DATA_LENGTH`, `TREE_SCALES_LENGTH`, and `INSTANCE_CAPACITY`. `GPUInstancerTreeManager.cs:256/258/260` binds all three uniforms before dispatch. `CSTreeInstantiationKernel.compute:15-16/34/42/55` rejects invalid tree data pairs, tree scale reads, and output writes.
Rejected Alternatives: Disabling GPU tree instantiation was rejected because it removes the intended vendor path. Rebuilding the tree data layout was rejected because the defect was missing bounds evidence, not a layout failure.
Scalability potential: Low/Middle/High/Ultra keep identical tree output for valid metadata. Invalid metadata fails closed on every tier instead of causing buffer OOB. No binary `isLowEnd` branch and no new simulation path were introduced.
Hardware Impact: Removes one shader read-OOB class for tree scales plus one after-null C# compile risk. Runtime microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX8 Evidence Refresh and Build Throttle

Problem: APEX7 evidence was stale after the tree scale fix and could not be treated as final proof.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, verified sidecar SHA equality, and verified source-file hash mismatch count = 0. Static macro-resolved shader scan now covers 60 entries, max group product 256, violation count 0. APEX8 hot dispatch/helper added-line scan: 119 lines, `string.Format` 0, `.ToString()` 0, `foreach` 0, LINQ 0, SetData/SetInts/SetFloats new-array callsites 0; one `new int[2]` remains classified as cold static scratch cache at `GPUInstancerUtility.cs:22`.
Rejected Alternatives: Claiming the APEX7 report as still final was rejected because file hashes and line evidence changed.
Scalability potential: Evidence integrity only. Runtime quality-scaling caveat remains: this vendor thread sizing pass does not integrate `HomeostasisBrain.GlobalQualityWeight` because shader group size is static and must not be desynchronized from HLSL `numthreads`.
Hardware Impact: Host CPU protected. APEX8 build preflight sampled CPU `[100,100,100]` with active `dotnet` pid 40436 CPU 681.5; build was not launched. Final report SHA-256: `8fc47524611fdc7891a70154cb3530659269ca8479547a4132e873da33a66df3`.

## 2026-05-28 APEX9 Crest Query and Underwater Bounds

Problem: Residual vendor audit found two real Crest bounds defects outside the original FFT edit. `QueryBase.cs` counted `queryPoints.Length` for capacity while normals expand into three extra query slots each, and query compute shaders trusted dispatch tail threads before reading query buffers. `CrestFillMaskArtefacts.compute` read +/-1 mask neighbours without width/height guards.
Solution: `QueryBase.cs` now computes `countTotal = countPts + countNorms * 3`, checks max query count and backing array capacity against `countTotal`, binds `_QueryCount`, and `QueryDisplacements.compute` / `QueryFlow.compute` return when `id.x >= _QueryCount`. `UnderwaterRenderer.Mask.cs` binds `_CrestOceanMaskWidth/_CrestOceanMaskHeight`, and `CrestFillMaskArtefacts.compute` rejects tail and edge pixels before all neighbour reads.
Rejected Alternatives: Trusting caller-side query discipline and render target load behaviour was rejected because both are vendor boundary assumptions with GPU OOB failure modes.
Scalability potential: Low/Middle/High/Ultra preserve the same ocean query and underwater mask visuals for valid inputs. Invalid or padded dispatch lanes fail closed. No binary low-end branch, no new physical simulation.
Hardware Impact: Removes query-buffer read OOB and underwater mask neighbour OOB classes. Runtime microseconds remain PENDING GPU CAPTURE.

## 2026-05-28 APEX9 GPUInstancer Grass and Texture Copy Bounds

Problem: `CSInstancedRenderingGrassInstantiationKernel.compute` wrote `gpuiInstanceData[index]` after `InterlockedAdd` without shader-side output capacity. `CSTextureUtils.compute` copy kernels could write beyond destination bounds when atlas offset/mip dimensions diverged from source dispatch dimensions.
Solution: Grass C# binds `instanceCapacity = visibilityBuffer.count`; shader rejects `index >= instanceCapacity` before output write. Texture copy C# computes source and destination mip dimensions, binds both, dispatches by source mip dimensions, and compute kernels reject `indexX >= destinationSizeX || id.y >= destinationSizeY`.
Rejected Alternatives: Assuming CPU counts and full-resolution texture dimensions always match was rejected because this pass exists to harden vendor compute boundaries.
Scalability potential: Low/Middle/High/Ultra preserve valid grass and texture-copy output while rejecting invalid tails. No `isLowEnd` branch and no simulation expansion.
Hardware Impact: Removes one grass output-buffer OOB class and one texture destination write OOB class. Exact runtime cost is a few scalar comparisons per tail lane; measured microseconds remain PENDING GPU CAPTURE.

## 2026-05-28 APEX10 Billboard Scalar Uniform Cleanup

Problem: The APEX8 allocation fix removed a per-call `new int[2]`, but left a cold static managed `int[]` scratch in a modified runtime file. The polish mandate asked for hard text evidence, and keeping the array weakened the Zero-GC proof even though it was not per-frame allocation.
Solution: Replaced `uint2 billboardSize` with scalar `uint billboardWidth` and `uint billboardHeight` in `CSBillboard.compute`. `GPUInstancerUtility.cs` now binds both with `SetInt`; `SetInts`, cached `int[]`, and `new int[2]` are gone from the billboard path.
Rejected Alternatives: Keeping the static scratch was rejected because scalar uniforms express the same contract and remove the remaining reference allocation surface. Using `SetInts(id, width, height)` was rejected because the `params int[]` call can allocate.
Scalability potential: Low/Middle/High/Ultra keep identical billboard dilation output for valid inputs and keep deterministic tail guards. This is allocation hygiene, not a GlobalQualityWeight scaler.
Hardware Impact: Removes the last managed array allocation introduced by this pass. APEX10 hot production C# added-line scan reports 172 lines, new reference/array 0, `string.Format` 0, `.ToString()` 0, `foreach` 0, LINQ 0, SetData/SetInts/SetFloats new-array callsites 0.

## 2026-05-28 APEX10 Evidence Refresh and Build Throttle

Problem: APEX9 evidence became stale after the billboard scalar-uniform cleanup and report hash refresh.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, and recorded APEX10 build preflight. Static shader scan: 86 compute/include files, 59 resolved thread group entries, max product 256, violation count 0. File hash mismatch count was verified separately after report generation.
Rejected Alternatives: Launching `dotnet build` under CPU 100 and active compiler process was rejected by the explicit compilation throttle.
Scalability potential: Evidence integrity only. The runtime quality caveat remains: vendor thread-group sizing must stay shader-static; continuous quality scaling belongs in first-party workload/cadence/density owners.
Hardware Impact: Host CPU protected. APEX10 preflight sampled CPU `[100,100,100]` with active `dotnet` pid 46580 CPU 21.21875; build was not launched. Final report SHA-256: `926db6cc0cf5767f6a3442431e22cbd5b167899b4d1503713fed7fdaaa6e6e11`.

## 2026-05-28 APEX11 Partial Copy and Buffer Texture Contracts

Problem: Residual GPUInstancer audit found three vendor boundary defects. `CSInstancedComputeBufferSetDataPartialKernel.compute` wrote `gpuiInstanceData[computeBufferStartIndex + id.x]` and read `gpuiManagedData[id.x]` without explicit source/destination capacity uniforms. `DilateBillboardTexture` could still pass a frame count larger than texture width, producing `frameWidth = 0`. `CSInstancedBufferToTexture.compute` trusted `argsBuffer[argsBufferIndex]` and consumed append IDs before proving they were within `bufferSize`.
Solution: `CSInstancedComputeBufferSetDataPartialKernel.compute` now receives `computeBufferCapacity` and `managedBufferCapacity`, rejects `id.x >= managedBufferCapacity`, rejects uint addition overflow, rejects `destinationIndex >= computeBufferCapacity`, and guards the single-write kernel. `GPUInstancerUtility.cs` binds these capacities for single, partial, copy, and merge paths. Billboard C# clamps `safeFrameCount` to texture width and shader rejects invalid metadata before division. Buffer-to-texture C# binds `bufferSize` for normal/shadow dispatches; shader rejects args drift, zero max texture size, and consumed instance IDs beyond buffer capacity.
Rejected Alternatives: Adding CPU readback validation was rejected because it would stall GPU work. Changing append-buffer layout or texture packing was rejected because the defects were missing guards, not data layout failure. Leaving invalid billboard metadata to produce an empty output was rejected because silent no-write is worse than fail-closed clamping.
Scalability potential: Low/Middle/High/Ultra keep identical output for valid data. Invalid vendor metadata now fails closed without binary `isLowEnd` branches and without adding simulation. Saved stability budget can be spent by first-party quality owners later; this patch does not inject `HomeostasisBrain.GlobalQualityWeight` into static shader thread contracts.
Hardware Impact: Removes one partial-copy buffer OOB class, one invalid billboard division/no-write class, and one append-consume matrix-buffer OOB class on mobile/MX350-class GPUs. Runtime cost is scalar comparisons and two `SetInt` uploads in an existing dispatch path; exact microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX11 Evidence Refresh and Build Throttle

Problem: APEX10 report and hash were stale after APEX11 edits.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json` and sidecar. Static shader scan: 86 compute/include files, 59 resolved thread group entries, max product 256, unknown token count 0, oversized violation count 0. File hash mismatch count after report generation: 0.
Rejected Alternatives: Reporting old APEX10 SHA was rejected because it no longer described the current working tree.
Scalability potential: Evidence integrity only.
Hardware Impact: Host CPU protected. APEX11 preflight sampled CPU `[100,100,100]` with active `dotnet` pid 55080 CPU 673.46875; build was not launched. Final report SHA-256: `87a0a54c3115d31baa2f899b7898642eca5a30a0edc802dabb0ff35a877f3cc6`.

## 2026-05-28 APEX12 Runtime Count Clamp

Problem: A self-audit found that `GPUInstancerUtility` could still bind or dispatch by `runtimeData.instanceCount` / `runtimeData.bufferSize` after physical GPU buffers had lower `ComputeBuffer.count` values. Camera/visibility culling, buffer-to-texture normal/shadow/cross-fade, and global transform offset kernels all consume matrix or LOD buffers and must fail closed if metadata drifts.
Solution: Added `GetSafeRuntimeInstanceCount` and `GetSafeRuntimeTransformBufferCount` clamp routes in `GPUInstancerUtility.cs:482-516`. Camera and visibility dispatches now bind/dispatch `safeInstanceCount`; buffer-to-texture normal, shadow, and cross-fade paths bind/dispatch `safeInstanceCount`; global position/matrix offset kernels use transform-buffer-safe count. Added `RuntimeCullingDispatch_ClampsLogicalInstanceCountToBufferCapacity` in `ComputeDispatchSizingEditTests.cs:109-121`.
Rejected Alternatives: CPU readback validation was rejected because it stalls the GPU. Trusting vendor metadata was rejected because this pass exists to harden third-party compute boundaries. Injecting `GlobalQualityWeight` into dispatch width was rejected because thread width is a static HLSL `numthreads` contract and desyncing CPU dispatch math from shader declarations is a correctness fault.
Scalability potential: Low/Middle/High/Ultra keep identical output for valid metadata. Invalid metadata drops excess lanes instead of reading outside buffers. Visual density scaling remains a first-party workload/cadence/density responsibility, not a vendor dispatch-width branch.
Hardware Impact: Removes runtime matrix/LOD buffer OOB classes from GPUInstancer culling, matrix texture copy, and offset kernels. Runtime cost is scalar integer min/checks plus unchanged dispatch count math; measured microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX12 Evidence Refresh and Build Throttle

Problem: APEX11 report and status were stale after the runtime count clamp.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json` and sidecar. Report SHA-256 is `84b924e1d66f074dee5647337f6b0a3ecdfaf35ee9c378a488ebfca4a0b9b1db`; sidecar matches; source file hash mismatch count is 0. Static shader scan covers 86 compute/include files, 170 resolved thread group entries, max product 256, unknown token count 0, oversized violation count 0. Hot-range Zero-GC scan covers 9 ranges / 813 lines and reports 0 `new`, 0 `string.Format`, 0 `.ToString()`, 0 `foreach`, 0 LINQ, 0 SetData/SetInts/SetFloats `new` callsites.
Rejected Alternatives: Claiming whole-file Zero-GC was rejected because cold/setup vendor code still contains pre-existing allocations, foreach, and LINQ outside the modified hot dispatch ranges.
Scalability potential: Evidence integrity only; no binary quality switch was added.
Hardware Impact: Host CPU protected. APEX12 build preflight sampled CPU `[87,82,57]` with active `dotnet` pid 66408 CPU 76.8125; build was not launched.

## 2026-05-28 APEX13 Append Buffer, Args Buffer, and Texture Capacity Guard

Problem: APEX12 safe counts clamped matrix and LOD data buffers, but did not prove every downstream submit surface. Visibility kernels append into per-LOD append buffers, `ComputeBuffer.CopyCount` writes into indirect args buffers by byte offset, `CSInstancedBufferToTexture.compute` read `argsBuffer[argsBufferIndex]` before proving the index, and texture bridge kernels had no shader-side texture-capacity guard.
Solution: Added `GetSafeVisibilityDispatchCount`, `GetMatrixTextureCapacity`, `GetTextureCapacity`, `GetSafeBufferToTextureDispatchCount`, `GetSafeTextureDispatchCount`, `TryGetArgsInstanceCountByteOffset`, and `TryGetArgsDrawByteOffset` in `GPUInstancerUtility.cs:507-615`. Visibility dispatch now clamps by target append-buffer counts. CopyCount and DrawMeshInstancedIndirect validate args buffer element ranges before submit. `CSInstancedBufferToTexture.compute` now receives `argsBufferLength` and `textureCapacity` and returns before args-buffer reads or texture writes when metadata drifts. `GPUInstancerConstants.cs:160/164` owns the new property IDs.
Rejected Alternatives: GPU counter readback was rejected because it stalls the render path. Trusting initialization to keep append/args/texture capacities coherent was rejected because this is a vendor boundary hardening pass. Rebuilding GPUInstancer's append/cross-fade layout was rejected because it is a larger vendor architecture change without Unity import/profiler proof.
Scalability potential: Low/Middle/High/Ultra keep identical valid output; invalid metadata drops unsafe dispatch/draw lanes instead of corrupting GPU state. This remains a static vendor safety clamp, not a `GlobalQualityWeight` scaler; continuous quality must scale first-party workload/cadence/density, not HLSL group width or indirect args layout.
Hardware Impact: Removes append-buffer over-dispatch, args-buffer OOB read, texture OOB write, invalid CopyCount offset, and invalid indirect draw offset classes on mobile/MX350-class GPUs. Runtime cost is scalar integer checks and extra `SetInt` uniforms inside existing dispatch paths. Microseconds remain PENDING GPU/Profiler capture.
Residual Risk: Cross-fade append remaining-capacity cannot be mathematically proven from CPU code without reading append counters or restructuring the vendor cross-fade append layout. Current patch clamps physical buffer capacities and texture/args surfaces, but Unity runtime GPU counter telemetry is still required before declaring full runtime proof.

## 2026-05-28 APEX13 Evidence Refresh and Build Throttle

Problem: APEX12 report and status were stale after append/args/texture contract fixes.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json` and sidecar. Report SHA-256 is `13d531ad1511dd9a5194d17f31ad43b04927dfc66f647e568fa1c612fe1ad564`; sidecar matches; source file hash mismatch count is 0. APEX13 lightweight shader scan covers 86 compute/include files, 59 resolved entries, max product 256, unknown token count 0, oversized violation count 0. Hot-range Zero-GC scan covers `GPUInstancerUtility.cs:450-940`, 491 lines, with 0 `new`, 0 `string.Format`, 0 `.ToString`, 0 `foreach`, 0 LINQ.
Rejected Alternatives: Launching `dotnet build` under contention was rejected by the explicit throttle. Claiming whole-file Zero-GC was rejected because cold/setup vendor code still contains pre-existing allocations and LINQ.
Scalability potential: Evidence integrity only; no binary quality switch was added.
Hardware Impact: Host CPU protected. APEX13 build preflight sampled CPU `[67,66,38]` with active `dotnet` pid 34436 CPU 217.171875; build was not launched.

## 2026-05-28 APEX14 Cross-Fade Append Remaining Capacity Guard

Problem: APEX13 clamped visibility dispatch count by physical append-buffer count, but cross-fade visibility passes (`lodAppendIndex != 0`, non-shadow) append into the same transformation append buffers after the normal pass has already populated their counters. If an append buffer capacity drifts below `safeInstanceCount`, clamping the cross-fade dispatch count to that smaller count is not sufficient: the normal pass can already consume the full smaller capacity before the cross-fade pass adds more entries.
Solution: `GPUInstancerUtility.cs:517/525/538/757` now passes `lodAppendIndex` into `GetSafeVisibilityDispatchCount`. For non-shadow counter-preserving append passes, the helper returns 0 unless every target append buffer count is at least `safeInstanceCount`. Mathematical proof: normal LOD target set and cross-fade target set are disjoint per instance for animated cross-fade (`oldLodNo != lodNo`) and distance cross-fade target `lodNo + 1`; therefore per-target total is bounded by `safeInstanceCount`, not by a smaller drifted capacity. If capacity is smaller, the safe no-stall route is to skip the cross-fade append pass and keep the normal pass.
Rejected Alternatives: GPU counter readback was rejected because it stalls the render path. Rebuilding the vendor append layout was rejected because it is a larger vendor architecture change without Unity import/profiler proof. Continuing to clamp the cross-fade pass to smaller capacity was rejected because counter state is already non-zero.
Scalability potential: Low/Middle/High/Ultra keep valid normal culling output. Under invalid metadata drift, the optional visual cross-fade is dropped instead of risking append overflow. This is a cinematic cheat: preserve stable rendering and accept a possible fade loss only when the buffer contract is already broken. No binary `isLowEnd` branch and no `GlobalQualityWeight` dispatch-width desync were introduced.
Hardware Impact: Removes a counter-preserving append overflow class on mobile/MX350-class GPUs. Runtime cost is one boolean and one scalar capacity comparison per target LOD before dispatch. Microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX14 Evidence Refresh and Build Throttle

Problem: APEX13 report carried a cross-fade remaining-capacity residual risk that was now reducible by static CPU-side guard logic.
Solution: Updated `ComputeDispatchSizingEditTests.cs:132-136`, regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, and verified source file hash mismatch count = 0. Report SHA-256 is `f13dfcbca3bef90cc5c49f342125153c62216b90846b9a86f4849831862a1345`. Hot-range Zero-GC scan covers `GPUInstancerUtility.cs:450-940`, 491 lines, with 0 `new`, 0 `string.Format`, 0 `.ToString`, 0 `foreach`, 0 LINQ.
Rejected Alternatives: Launching `dotnet build` under contention was rejected by the explicit throttle.
Scalability potential: Evidence integrity only; no binary quality switch was added.
Hardware Impact: Host CPU protected. APEX14 build preflight sampled CPU `[53,61,54]` with active `dotnet` pid 12600 CPU 13.203125; build was not launched.

## 2026-05-28 APEX15 Debug GPU Readback Release Guard

Problem: `GPUInstancerUtility.cs:486-494` had a documented debug path that calls `ComputeBuffer.GetData` after compute dispatch when `showRenderedAmount` is true. The comment warned that it impacts FPS, but the code could still compile into release players.
Solution: Wrapped the rendered-amount readback block in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` at `GPUInstancerUtility.cs:487-497`. Added static proof `RenderedAmountGpuReadback_IsDebugBuildOnly` at `ComputeDispatchSizingEditTests.cs:146-153`.
Rejected Alternatives: Deleting the debug feature was rejected because editor/development rendered-count diagnostics remain useful. Replacing it with `AsyncGPUReadback` was rejected for this pass because the display contract and frame-latency behavior are a larger vendor UI/debug feature change.
Scalability potential: Low/Middle/High/Ultra release players avoid a debug GPU-to-CPU stall path. Development builds can still pay the diagnostic cost intentionally. No binary quality branch and no `GlobalQualityWeight` route were introduced.
Hardware Impact: Prevents accidental release-player GPU readback stalls on MX350/mobile-class hardware. Runtime microseconds saved are PENDING PROFILER because the path is debug-flag dependent.

## 2026-05-28 APEX15 Evidence Refresh and Build Throttle

Problem: APEX14 report and status were stale after the release-player debug readback guard.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, and verified source file hash mismatch count = 0. Report SHA-256 is `de503ed189ba843601d6f98c768c73d78f845356c0071e5c45062e9db9768725`. Hot-range Zero-GC scan covers `GPUInstancerUtility.cs:450-940`, 491 lines, with 0 `new`, 0 `string.Format`, 0 `.ToString`, 0 `foreach`, 0 LINQ.
Rejected Alternatives: Launching `dotnet build` after the just-in-time preflight failed was rejected by the explicit throttle.
Scalability potential: Evidence integrity only; no binary quality switch was added.
Hardware Impact: Host CPU protected. APEX15 build preflight for `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` sampled CPU `[46,47,82]` with active `csc` pid 44804 CPU 6.40625 and `dotnet` pid 9696 CPU 24.71875; build was not launched.

## 2026-05-28 APEX16 Grass Source Buffer Capacity Guard

Problem: APEX15 still left a source-capacity gap in `CSInstancedRenderingGrassInstantiationKernel.compute`. The shader checked logical detail/height dimensions, but physical `ComputeBuffer.count` can drift from dimension metadata; `detailMapData[detailIndex]` and `heightMapData[...]` therefore lacked direct source-capacity proof.
Solution: Added scalar uniforms `detailMapCapacity` and `heightMapCapacity`. The shader rejects zero capacities, rejects `detailIndex >= detailMapCapacity` before the detail count read, and uses `heightMapCapacity` as the `FixBounds` source size for height samples. `GPUInstancerConstants.GrassKernelProperties` owns the property IDs and `GPUInstancerDetailManager.cs:584-585` binds `detailMapBuffer.count` and `heightMapBuffer.count`.
Rejected Alternatives: Trusting Unity terrain dimensions was rejected because this is a vendor compute boundary. Recomputing grass placement or reducing density was rejected because the defect was missing buffer proof, not a fidelity or simulation problem. CPU readback was rejected because it would stall.
Scalability potential: Low/Middle/High/Ultra keep identical grass placement for valid metadata. If metadata drifts, unsafe source reads fail closed. No binary `isLowEnd` branch and no `GlobalQualityWeight` dispatch-width desync were introduced; continuous grass density/cadence scaling belongs to a first-party quality owner, not static shader thread contracts.
Hardware Impact: Removes one detail-map source OOB class and one height-map source-capacity drift class on mobile/MX350-class GPUs. Runtime cost is two scalar uniforms and two branch checks in an existing dispatch path. Exact microseconds remain PENDING GPU/Profiler capture.

## 2026-05-28 APEX16 Evidence Refresh and Build Throttle

Problem: APEX15 report and sidecar were stale after APEX16 shader/C# edits.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, updated line proof for `CSInstancedRenderingGrassInstantiationKernel.compute:27-28/57/62/73`, `GPUInstancerConstants.cs:267-268`, `GPUInstancerDetailManager.cs:584-585`, and `ComputeDispatchSizingEditTests.cs:157-167`. Report SHA-256 is `0837e68c9c968f240e95835b27fc06d7534f357e8baf05ffd4338b96920fb109`.
Rejected Alternatives: Reporting APEX15 SHA was rejected because it did not describe the current working tree.
Scalability potential: Evidence integrity only; no binary quality switch was added.
Hardware Impact: Host CPU protected. APEX16 build preflight for `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` sampled CPU `[63,66,22]` with active `dotnet` pid 22412 CPU 168.734375; build was not launched.

## 2026-05-28 APEX17 Args, Counter, Mip, and Readback Containment

Problem: Subagent and self-audit found four residual vendor-boundary risks after APEX16. `CSArgsBuffer.compute` needed physical args-buffer length proof and the guard needed to precede index multiplication. Tree/grass `InterlockedAdd` capacity overflow lanes returned without cleaning the atomic counter. Reduce-texture dispatch could still feed zero mip dimensions. Detail instance CPU storage path had a synchronous `visibilityBuffer.GetData(result)` route compiled into production.
Solution: `CSArgsBuffer.compute` now returns on `id.x >= count`, computes `argsIndex`, then rejects `argsIndex >= argsBufferLength`; `GPUInstancerManager.cs:640` binds `ARGS_BUFFER_LENGTH`. Tree/grass shaders atomically add `0xffffffffu` before returning from output-capacity overflow lanes. `CSTextureUtils.compute` rejects zero reduce dimensions and `GPUInstancerUtility.cs` uses `GetTextureMipDimension` after mip clamp. The `Matrix4x4[]` compute-readback helper is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`; release `SetInstanceDataForDetailCells` uses the existing CPU bake route.
Rejected Alternatives: Leaving counter inflation was rejected because later diagnostics/counter consumers would see false counts even when writes were guarded. AsyncGPUReadback was rejected for this surgical pass because it requires coroutine ownership and visual parity tests around terrain detail storage; a half-converted callback chain would be higher risk. Trusting release `GetData` was rejected because it is a known GPU/CPU sync stall.
Scalability potential: Low/Middle/High/Ultra all keep safe 128/256 thread groups and fail-closed physical-capacity guards. Production detail storage trades the synchronous GPU readback for an existing CPU bake route; this is a stability cheat, not a device-tier quality switch. A future first-party quality owner can scale detail density/cadence with `GlobalQualityWeight`, but this vendor pass must not desync dispatch width from HLSL `numthreads`.
Hardware Impact: Removes XR indirect-args OOB risk, false tree/grass counter growth after overflow, reduce-texture zero-dimension dispatch risk, and a release-player synchronous detail-readback route. Exact microseconds remain PENDING GPU/Profiler capture; build was not launched because APEX17 preflight sampled CPU `[83,43,69]` with active `dotnet` pid 10992 CPU 21.3125.

## 2026-05-28 APEX17 Evidence Refresh and Build Throttle

Problem: APEX16 report, status, and sidecar were stale after APEX17 edits.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, updated file hashes and line proof for `CSArgsBuffer.compute:8/13/16/17/20`, `GPUInstancerManager.cs:638-641`, `CSTreeInstantiationKernel.compute:55-59`, `CSInstancedRenderingGrassInstantiationKernel.compute:87-92`, `CSTextureUtils.compute:45`, `GPUInstancerUtility.cs:3940/4023-4026`, `GPUInstancerDetailManager.cs:489-533/933-962`, and `ComputeDispatchSizingEditTests.cs:171-242`. Report SHA-256 is `38ab591e5f8b461f0ad94b9425dcaff53a5508952bca9734ba07ee37d3d14aaa`.
Rejected Alternatives: Reporting APEX16 SHA was rejected because it no longer described the working tree. Launching build under CPU 83 and an active dotnet process was rejected by the explicit compilation throttle.
Scalability potential: Evidence integrity only. No binary `isLowEnd` switch or `GlobalQualityWeight` misuse was added.
Hardware Impact: Host CPU protected. APEX17 build preflight artifact: `Docs/AgentLogs/Build_1405_Apex17.summary.json`.

## 2026-05-28 APEX18 CPU Detail Fallback Parity and Truthful Zero-GC Boundary

Problem: APEX17 correctly removed the production synchronous `visibilityBuffer.GetData(result)` route, but that made the release `DETAIL_STORE_INSTANCE_DATA` path depend on the CPU fallback. The fallback still used `new System.Random`, ignored `detailDensity`, ignored `terrainNormalEffect`, trusted logical map sizes before physical array lengths, and allocated output before rejecting negative `instanceCount`.
Solution: `GPUInstancerDetailManager.cs:411/423` now uses deterministic Random.hlsl-style hash helpers for placement/density/rotation; `GPUInstancerDetailManager.cs:442/444` rejects `instanceCount <= 0` before allocating the persistent output array; `GPUInstancerDetailManager.cs:449-452/477-480` uses physical `heightMapData.Length` and `detailMap.Length`; `GPUInstancerDetailManager.cs:489-493` applies the density gate while preserving zero-matrix lane count; `GPUInstancerDetailManager.cs:525/528/530` applies terrain normal influence, deterministic rotation, and deterministic scale. `GPUInstancerDetailManager.cs:972/983-987` replaced modified-route `foreach` loops with indexed traversal. Static tests were updated at `ComputeDispatchSizingEditTests.cs:201/225/229/240`.
Rejected Alternatives: Sampling `healthyDryNoiseTexture` from CPU was rejected because the only direct route would require texture readback/CPU staging and would undo the release readback containment. Hiding the cold `new Dictionary<int, Matrix4x4[]>` or `new Matrix4x4[instanceCount]` behind a helper was rejected because it would only satisfy a text scan while preserving the allocation. The honest contract is: hot dispatch paths are clean; cold persistent detail storage allocates by design.
Scalability potential: Low uses fail-closed CPU detail storage without GPU readback stalls; Middle/High keep deterministic detail placement and normal alignment; Ultra can still use the existing compute path in editor/development diagnostics. This patch does not inject `GlobalQualityWeight` into static vendor shader thread contracts. The scale fake is a cinematic cheat: deterministic hash scale instead of exact noise texture sampling when release storage cannot read back GPU data safely.
Hardware Impact: Removes nondeterministic CPU detail storage and one negative-count crash class on mobile/MX350-class machines. Runtime hot dispatch cost is unchanged. Cold storage still allocates persistent `Matrix4x4[]` arrays and one per-cell dictionary; exact microseconds remain PENDING GPU/Profiler capture. Build was not launched because APEX18 preflight sampled CPU `[73,91]` with active `dotnet`, `VBCSCompiler`, and `csc` processes.

## 2026-05-28 APEX18 Evidence Refresh and Build Throttle

Problem: APEX17 report, status, and sidecar were stale after the CPU fallback hardening.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, and verified source file hash mismatch count = 0. APEX18 hot-range scan covers `GPUInstancerManager.cs:624-642`, `GPUInstancerUtility.cs:450-940`, `GPUInstancerUtility.cs:4016-4036`, and `GPUInstancerDetailManager.cs:623-651`: 560 lines, `new=0`, `string.Format=0`, `.ToString=0`, `foreach=0`, LINQ=0. Cold bake disclosure remains in the report: `GPUInstancerDetailManager.cs:406-542` has the required persistent `Matrix4x4[]` output allocation and value-type constructors; `GPUInstancerDetailManager.cs:978-1024` has one persistent dictionary allocation; both cold ranges have 0 `string.Format`, 0 `.ToString`, 0 `foreach`, 0 LINQ.
Rejected Alternatives: Launching `dotnet build` under contention was rejected by the explicit throttle. Reporting APEX17 SHA was rejected because it no longer described the working tree.
Scalability potential: Evidence integrity only. No binary `isLowEnd` switch and no `GlobalQualityWeight` misuse were added.
Hardware Impact: Host CPU protected. APEX18 build preflight artifact: `Docs/AgentLogs/Build_1405_Apex18.summary.json`. Final report SHA-256: `902437e2c53b098b674b645e989d668aa717d2ec75e42bfa475d7d9ad38fb778`.

## 2026-05-28 APEX19 Optional Grass Noise Texture and Divisor Guard

Problem: APEX18 still left a vendor shader contract hole. `CSInstancedRenderingGrassInstantiationKernel.compute` could sample `healthyDryNoiseTexture` even when C# did not bind it, because `GPUInstancerDetailManager.cs` only called `SetTexture` when the texture was non-null. The same shader also divided by `detailResolution`, `heightResolution`, and `terrainSize.x / terrainSize.y` without an explicit zero-metadata guard. APEX18 CPU fallback seed parity was weaker than the shader because it used grid indices rather than world-corner coordinates.
Solution: Added `hasHealthyDryNoiseTexture` scalar uniform, C# property ID, and C# binding from `healthyDryNoiseTexture != null`. The shader now uses deterministic `randomFloat((grassPosition.x * multiplier) + grassPosition.z)` as a no-texture fallback, and samples the healthy/dry noise texture only when the flag is set. Added zero-divisor guard for `detailResolution`, `heightResolution`, `terrainSize.x`, and `terrainSize.y`. CPU fallback now builds `cornerPositionX/cornerPositionZ` and uses world-corner seeds for density and random point generation. The editor/development compute-readback helper now rejects `instanceCount <= 0` before allocating `Matrix4x4[]`.
Rejected Alternatives: Binding a dummy texture was rejected because it would add managed/resource setup debt to hide an optional dependency. Sampling `healthyDryNoiseTexture` in release CPU bake was rejected because it would require texture readback or staging and undo the release readback containment. Changing valid grass density or scale semantics globally was rejected because the defect was missing descriptor/divisor proof, not visual design.
Scalability potential: Low/Middle/High/Ultra keep the same valid grass path when the texture exists. When the optional texture is absent, scale uses a deterministic hash fake instead of undefined descriptor sampling. This is a cinematic cheat and fail-closed stability path, not a binary `isLowEnd` switch. No `GlobalQualityWeight` was injected because vendor shader thread contracts and dispatch dimensions must remain static; continuous density/cadence scaling belongs to a first-party quality owner.
Hardware Impact: Removes one optional descriptor binding fault class, one zero-division metadata class, one CPU/GPU fallback seed drift, and one editor/development negative-count allocation crash. Runtime cost is one scalar uniform and one uniform branch in the existing grass instancing kernel. Exact GPU microseconds remain PENDING profiler/RenderDoc capture.

## 2026-05-28 APEX19 Evidence Refresh and Build

Problem: APEX18 report, status, sidecar, and build state were stale after APEX19 shader/C# edits.
Solution: Regenerated `Docs/Reports/VENDOR_COMPUTE_OPTIMIZATION_REPORT_1405.json`, wrote sidecar SHA, and verified source file hash mismatch count = 0. APEX19 hot-range scan covers `GPUInstancerManager.cs:624-642`, `GPUInstancerUtility.cs:450-940`, `GPUInstancerUtility.cs:3980-4040`, and `GPUInstancerDetailManager.cs:625-655`: 602 lines, `new=0`, `string.Format=0`, `.ToString=0`, `foreach=0`, LINQ=0. Cold bake/storage disclosure remains explicit. Compilation gate was clear: CPU samples `[1,23,18]`, compiler process count 0. One `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /v:minimal` was launched and succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Reporting APEX18 SHA or build-blocked state was rejected because it no longer described the working tree. Running Unity import/EditMode/profiler from this CLI session was rejected because those require Unity Editor/runtime artifacts and were outside available proof.
Scalability potential: Evidence integrity only. APEX19 introduced no binary quality switch and no `GlobalQualityWeight` misuse.
Hardware Impact: Host CPU rule was observed. Build artifact: `Docs/AgentLogs/Build_1405_Apex19.summary.json`. Final report SHA-256: `f08d1309c122979be4dcbfec906d641c82fd2328951150e0449c22d538d4a6c0`.

## 2026-05-28 APEX20 Physical Detail Map Capacity Recheck

Problem: APEX19 still left one detail compute source-capacity weakness. The editor/development compute bake helper created `detailMapBuffer` from `detailMapSize * detailMapSize`, and the merge route reused a shared `detailMapBuffer` / `heightMapBuffer` without proving the current cell's actual source array length matched the buffer count. Because the shader now trusts `detailMapCapacity = detailMapBuffer.count`, an oversized buffer can expose stale trailing source lanes when the flattened detail array is shorter than logical dimensions.
Solution: `GPUInstancerDetailManager.cs` now rejects null/empty detail and height arrays before compute buffer creation, creates the editor/development `detailMapBuffer` from `detailMap.Length`, recreates the merge-route height/detail buffers when `count != source.Length`, and returns before detail dispatch when any required buffer is null or non-positive. Added `DetailComputeBake_UsesPhysicalDetailMapCapacity` in `ComputeDispatchSizingEditTests.cs`.
Rejected Alternatives: Padding detail arrays was rejected because it hides source truth and adds allocation/copy debt. Trusting terrain dimensions was rejected because the active contract is physical GPU buffer capacity, not logical terrain metadata. CPU readback/texture staging was rejected because this is a source buffer sizing defect, not a visual parity problem.
Scalability potential: Low/Middle/High/Ultra keep identical detail placement for valid source arrays. Invalid or drifted cell data now fails closed or uses exact physical source lengths. No `GlobalQualityWeight` or binary tier branch was added; this remains static vendor boundary hardening.
Hardware Impact: Removes a stale-detail-source lane class on mobile/MX350-class GPUs and prevents invalid zero/null buffer dispatch. Runtime hot submit cost remains scalar checks; cold setup can recreate ComputeBuffers when source lengths change. Build was not launched because APEX20 CPU samples were `[73,30,97,74]` with compiler process count 0, exceeding the >50 CPU throttle. Final report SHA-256: `adcc5b2901b8ee094a3b94a6bdf6abf00a6a20dda0347fdec9128c1d62d3c4be`.
