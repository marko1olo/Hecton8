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
