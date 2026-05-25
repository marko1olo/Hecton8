# LOG_SHINOBU_236

## 2026-05-21 - Bilateral DRS Upscaler

What was wrong:
- Rendering domain had no explicit edge-preserving DRS reconstruction owner. Default engine upscale risk remained bilinear softness under thermal DRS drops.
- Existing temporal reconstruction path in Visor is history-dependent and can smear when resolution changes. SHINOBU_236 needed a current-frame-only path.
- No SHINOBU_236 Vault lanes existed for constants, tuning, profiles, mock DRS state, or black-box telemetry.

What was done:
- Added `UpscalerParamsDTO` at 32 bytes with explicit offsets: `ResolutionParams` at byte 0 and `FilterParams` at byte 16.
- Added Vault BufferIDs `71050-71056` for params, tuning, telemetry, cursor, profiles, CSV scratch, and mock DRS state.
- Added `GenerateMockDrsStateJob` and `CalculateUpscalerParamsJob`, both Burst-annotated and run through `IJob.Run()`.
- Added `Hecton_BilateralUpscale.compute` with `SobelDepthMask` and `BilateralUpscale` kernels using 8x8 threadgroups.
- Added `HectonBilateralDrsUpscalerRuntime` with uninitialized Vault staging, double constant-buffer upload via `LockBufferForWrite`, and `Dump_SHINOBU_236.bin` black-box writer.
- Added `HectonBilateralDrsUpscalerFeature` using URP RenderGraph compute passes with explicit texture and buffer dependencies.
- Added UI Toolkit `Bilateral DRS Tuner`, cold CSV profile ingestion, edge mask global texture, and static `Blit_Operation_Inquisition`.
- Added architecture doc and generated `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.

Cinematic Cheats used:
- Dear Lie Sobel prepass: heavy bilateral only at geometric silhouettes; flat ocean/walls use cheap bilinear.
- Radius+jitter packing: preserves 32-byte ARM64 CBuffer contract while passing sub-pixel jitter.
- Continuous quality scalar: low pressure collapses toward cross/bilinear taps; high/ultra spends ALU at edges only.

Exact microseconds saved:
- Static estimate only. Compile/playmode profiling was blocked by CPU guard at 100%.
- Expected CPU upload saving vs managed globals/`SetData`: ~2-8 us/frame on i3/MX350 class CPU.
- Expected owner job cost: ~6-12 us/frame.
- Expected GPU saving from Sobel bypass vs all-pixel 5x5 bilateral: ~600-1800 us at 1080p when edge density remains below 25%.
- Telemetry write cost: 64 bytes/frame into Vault, fixed 300-entry ring.

Compile status:
- Not run. CPU guard returned 100% via CIM and 73.07% via performance counter; no `dotnet` or `csc` process was active, but AGENTS forbids launching dotnet build above 50% CPU.

<SELF_AUDIT agent="SHINOBU_236" role="BILATERAL_DRS_UPSCALER">
  <TaskCount>20</TaskCount>
  <ByteLayout>
    <UpscalerParamsDTO size="32">
      <ResolutionParams offset="0" type="float4" semantic="LowResX,LowResY,HighResX,HighResY" />
      <FilterParams offset="16" type="float4" semantic="DepthWeight,ColorWeight,RadiusJitterPack,QualityScalar" />
    </UpscalerParamsDTO>
    <UpscalerTelemetryEntry size="64" capacity="300" />
  </ByteLayout>
  <VaultBufferIDs>
    <Params>71050</Params>
    <Tuning>71051</Tuning>
    <Telemetry>71052</Telemetry>
    <TelemetryCursor>71053</TelemetryCursor>
    <Profiles>71054</Profiles>
    <CsvScratch>71055</CsvScratch>
    <MockState>71056</MockState>
  </VaultBufferIDs>
  <RenderGraph>
    <Pass name="Hecton Bilateral DRS Sobel Edge Mask" reads="cameraDepth,constantBuffer" writes="edgeMask" />
    <Pass name="Hecton Bilateral DRS Upscale" reads="activeColor,cameraDepth,edgeMask,constantBuffer" writes="cameraColor" />
  </RenderGraph>
  <HotPathGC>
    <ManagedArrays>false</ManagedArrays>
    <ShaderSetGlobalFloat>false</ShaderSetGlobalFloat>
    <GraphicsBlit>false</GraphicsBlit>
    <ComputeBufferSetData>false</ComputeBufferSetData>
    <PerFrameNativeAllocation>false</PerFrameNativeAllocation>
  </HotPathGC>
  <RollbackIsolation presentationOnly="true" merkleState="excluded" saveState="excluded" gameplayAuthority="excluded" />
  <CompileProof status="DEFERRED_CPU_GUARD_OVER_50_PERCENT" />
</SELF_AUDIT>

---

## 2026-05-21 Ultra-Think Polish Pass 7

What was wrong:
- Runtime source was not yet isolated into its own rendering-domain asmdef, so SHINOBU_236 edits still risked inheriting the broad root assembly blast radius.
- New Unity assets lacked stable `.meta` GUIDs.
- The architecture note did not have a compact route card proving owner, route, phase, buffer IDs, and failure modes.
- The runtime layout validator checked sizes and constants but not actual `UpscalerParamsDTO` field offsets.
- The static grep around global texture publication needed a precise exception note: `SetGlobalTextureAfterPass` is a RenderGraph-declared Task 18 debug publication bridge, not a forbidden `Shader.SetGlobalFloat` route.

What was done:
- Added `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef`.
- Updated `Assets/_Project/Scripts/Editor/Hecton8.Editor.asmdef` to reference `Hecton8.Rendering.BilateralDrs`.
- Added stable `.meta` files for the BilateralDrs folder, asmdef, three runtime scripts, compute shader, CSV profile, editor tuner, and scanner.
- Added `Docs/ARCHITECTURE/SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md`.
- Linked the route card from `Docs/ARCHITECTURE/BILATERAL_DRS_UPSCALER_SHINOBU_236.md`.
- Extended `UpscalerParamsLayoutValidator.Validate()` with `Marshal.OffsetOf<UpscalerParamsDTO>` checks for offsets 0 and 16.
- Re-ran SHINOBU_236 static gates and CPU/dotnet guard.

Cinematic Cheats used:
- No new simulation. The same Sobel depth mask remains the Dear Lie: classify silhouette likelihood in screen-space and spend bilateral taps only where blur would be visible.

Exact microseconds saved:
- Runtime microseconds unchanged for asmdef/meta/docs.
- Field-offset validation is cold only; hot-frame cost 0 us.
- Compile-wall savings are expected in editor iteration, not frame time; exact seconds pending a legal build/import window.

Verification:
- CPU sample was 100%; no `dotnet`/`csc` process was active. Build remains blocked by the documented >50% CPU guard.
- Static grep found no direct sibling runtime references to AI, World, Gameplay, Physics, Audio, VFX, Environment, Vehicles, Habitat, Logistics, Power, or Input in the SHINOBU_236 runtime path.
- Static grep found no `job.Execute`, `Shader.SetGlobalFloat`, `Shader.SetGlobalVector`, `Graphics.Blit`, `CommandBuffer.Blit`, `SetData`, `AddUnsafePass`, `.Complete`, hot managed arrays/lists/dictionaries, `System.Linq`, `UnityEngine.Random`, `Time.deltaTime`, or `GlobalDataVault.TryGetLatestCreated` in the SHINOBU_236 runtime/shader path.

<SELF_AUDIT agent="SHINOBU_236" pass="7" status="PENDING_COMPILE_CPU_GUARD">
  <TaskCount>20</TaskCount>
  <TaskReconciliation>
    <Task id="01" status="PASS" note="No SHINOBU_236 Graphics.Blit upscale path introduced." />
    <Task id="02" status="PASS" note="No temporal history dependency introduced." />
    <Task id="03" status="PASS" note="Hot DTOs remain raw unmanaged fields." />
    <Task id="04" status="PASS" note="Size and real offsets are now validated through UnsafeUtility/editor and Marshal/runtime checks." />
    <Task id="05" status="PASS" note="Mock DRS state remains Vault-backed." />
    <Task id="06" status="PASS" note="Burst parameter job uses CompileSynchronously/Fast/Standard and job.Run." />
    <Task id="07" status="PASS" note="Bilateral compute kernel remains current color/depth only." />
    <Task id="08" status="PASS" note="Sobel edge Dear Lie gates bilateral cost." />
    <Task id="09" status="PASS" note="Double constant buffer upload remains SetData-free." />
    <Task id="10" status="PASS" note="Quality remains continuous; no low/high hardware switch." />
    <Task id="11" status="PASS" note="RenderGraph resources remain declared through TextureHandle/BufferHandle dependencies." />
    <Task id="12" status="PASS" note="Jitter remains packed inside the 32B CBuffer lane." />
    <Task id="13" status="PASS" note="Presentation-only route remains outside rollback/save/Merkle truth." />
    <Task id="14" status="PASS" note="Persistent data remains Vault-owned; no per-frame native allocation route found." />
    <Task id="15" status="PASS" note="300-entry telemetry ring and dump target preserved." />
    <Task id="16" status="PASS" note="Editor tuner remains UI Toolkit and cold-managed only." />
    <Task id="17" status="PASS" note="CSV parser remains span-based and cold." />
    <Task id="18" status="PASS" note="RenderGraph debug composite provides black/green edge mask; SetGlobalTextureAfterPass is the declared debug bridge." />
    <Task id="19" status="PASS" note="Scanner preserves previous shared report and writes SHINOBU_236 owner report." />
    <Task id="20" status="PASS" note="Pass 7 audit appended; compile honestly deferred by CPU guard." />
  </TaskReconciliation>
  <StructLayout>
    <UpscalerParamsDTO size="32">
      <Field name="ResolutionParams" offset="0" size="16" />
      <Field name="FilterParams" offset="16" size="16" />
      <Padding bytes="0" />
    </UpscalerParamsDTO>
    <UpscalerTelemetryEntry size="64" cacheLine="true" padding="0" />
  </StructLayout>
  <ScalabilityCurve note="GlobalQualityWeight controls radius, bypass threshold, and tap gates continuously; below 0.3 the pass collapses toward bilinear/cross-like work, mid tiers keep Sobel-gated 3x3-ish reconstruction, high/ultra spend wider gated edge taps." />
  <VaultStatus privateNativeArrays="0" buffers="71050,71051,71052,71053,71054,71055,71056" />
  <DependencyGraph consumes="DispatcherTimingDTO.FrameDelta, cached DataVault, cached ResolutionScalerService" outputs="pending params, telemetry, VisualSync CBuffer upload" noAlias="true" hiddenComplete="false" />
  <CompileGuard asmdef="Hecton8.Rendering.BilateralDrs" directSiblingRuntimeRefs="false" buildStatus="DEFERRED_CPU_100_PERCENT" dotnetOrCscActive="false" />
  <DearLie complexityBefore="O(P*25) bilateral every pixel" complexityAfter="O(P*9 Sobel + E*gatedTaps + F*bilinear)" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 8 Render-Path Purity Polish

What was wrong:
- `RecordRenderGraph` still called `EnsureRuntimeInstance()`. The usual path already bootstrapped from `AddRenderPasses`, but the graph-recording function still had a hidden cold-allocation fallback.
- `Hecton_BilateralUpscale.compute` had a literal `quality <= 0.015` bypass. It was not a hardware-tier switch, but it created a hard quality cutoff in shader source.
- `ClampPixel` relied on compact mixed `uint2`/`int2` arithmetic that could become a mobile shader compiler risk.

What was done:
- Added pure cached `HectonBilateralDrsUpscalerRuntime.TryGetRuntimeInstance(out runtime)`.
- Changed `HectonBilateralDrsUpscalerFeature.RecordRenderGraph` to fail closed if the runtime owner is absent. Cold GameObject bootstrap remains limited to `AddRenderPasses`.
- Replaced the shader hard quality cutoff with `smoothstep(0.015, 0.075, quality)` and multiplied tap gates by the continuous quality gate.
- Expanded `ClampPixel` into explicit safe dimensions and explicit `int2` max pixel arithmetic.
- Updated `Status_SHINOBU_236.md`, `Rationale_SHINOBU_236.md`, `BILATERAL_DRS_UPSCALER_SHINOBU_236.md`, and the route card.

Cinematic Cheats used:
- The Dear Lie remains a Sobel depth edge mask plus flat-pixel bilinear bypass. At weak quality, the continuous quality gate collapses silhouettes toward bilinear without introducing a device-tier branch or temporal history.

Exact Microseconds saved:
- Render path: avoids a worst-case one-frame cold GameObject/AddComponent allocation in `RecordRenderGraph`; profiler proof pending.
- Shader: keeps the prior expected ~600-1800 us saving versus full-screen 5x5 bilateral on MX350-class GPUs when edge density is low. The pass 8 change removes the hard cutoff and preserves low-quality bypass behavior.

Static proof:
- No literal hard quality threshold matches remained in SHINOBU_236 shader/runtime path.
- Forbidden hot-path grep found no `TryGetLatestCreated`, `Shader.SetGlobalFloat/Vector`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, `.Complete`, `job.Execute`, hot managed containers, `System.Linq`, `UnityEngine.Random`, `Time.deltaTime`, or `Pack=1`.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`. Dotnet compile still deferred by project rule.

<SELF_AUDIT agent="SHINOBU_236" pass="8" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="SOURCE_PATCHED_STATIC_GATES_PASSED_COMPILE_DEFERRED" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" />
  <ScalabilityCurve qualityGate="smoothstep(0.015,0.075,quality)" branchType="edge-confidence-bypass-not-device-tier" />
  <Authority renderGraphRecord="pure cached runtime lookup" coldBootstrap="AddRenderPasses" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 9 Subagent Audit Integration

What was wrong:
- RenderGraph submitted current source/full dimensions, then imported a constant buffer generated earlier. First activation could use stale `1x1` or previous-frame parameters.
- Render pass enqueue could still create the runtime owner. The runtime also used `DontDestroyOnLoad`.
- Compute setup used `FindKernel` without `HasKernel`.
- Shader depth reads assumed depth dimensions matched output dimensions.
- Shader UAV writes were not fully guarded against NaN/Inf.
- XR texture arrays and R8 UAV support were assumed.

What was done:
- Added scene-local owner bootstrap through runtime-load and `SceneManager.sceneLoaded`; removed `DontDestroyOnLoad`.
- `AddRenderPasses` now only reads the cached runtime owner.
- Added `TryPrepareRenderGraphConstants` so `RecordRenderGraph` receives a same-frame uploaded CBuffer with current dimensions and jitter.
- Added `HasKernel` guards before kernel lookup.
- RenderGraph fail-closes for XR arrays until array kernels exist.
- Edge mask resolves `R8_UNorm` or `R16_SFloat` only when LoadStore is supported; output color also requires/falls back through LoadStore validation.
- Shader maps output pixels into real depth texture dimensions, removes `GetDimensions` from helper/tap loops, normalizes Sobel by center eye depth, and finite-guards every UAV write.

Cinematic Cheats used:
- Still Sobel-depth Dear Lie, not temporal history or optical flow. The new depth mapping makes the fake reliable when URP scales depth.

Exact Microseconds saved:
- Removed repeated helper-loop `GetDimensions` calls on edge pixels.
- Prevented stale first-frame upscale garbage rather than adding a temporal repair pass.
- Exact GPU and CPU microseconds remain pending because build/profiler are blocked by CPU guard.

Static proof:
- Forbidden grep found no `DontDestroyOnLoad`, `TryGetLatestCreated`, `Shader.SetGlobalFloat/Vector`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, `.Complete`, `job.Execute`, hot managed containers, `System.Linq`, `UnityEngine.Random`, `Time.deltaTime`, or `Pack=1` in SHINOBU_236 files.
- `git diff --check` passed for the patched runtime, feature, and compute shader.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="9" status="PENDING_COMPILE_CPU_GUARD">
  <SubagentFindings integrated="true" staleCBuffer="patched" renderFlowSpawn="patched" kernelGuard="patched" depthMapping="patched" finiteGpuWrites="patched" xrArrayFailClosed="patched" />
  <Authority ownerBootstrap="RuntimeInitializeOnLoadMethod+SceneManager.sceneLoaded" renderFeatureCreatesOwner="false" dontDestroyOnLoad="false" />
  <ShaderSafety depthDimsMapped="true" helperLoopGetDimensions="false" finiteUavWrites="true" />
  <FormatGate edgeMask="R8_UNorm_or_R16_SFloat_LoadStore" outputColor="LoadStore_required" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 10 Ledger and Evidence Chain Repair

What was wrong:
- `LOG_SHINOBU_236.md` was no longer top-old/bottom-new. Pass 9 and pass 8 sat above pass 7 and pass 6.
- `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` had no SHINOBU_236 row, so Vault BufferIDs `71050-71056` were source-defined but absent from the project-wide payload ledger.
- Static grep after the doc patch still matched two forbidden strings because enforcement text and the scanner itself contain the forbidden token names.

What was done:
- Mechanically reordered `LOG_SHINOBU_236.md` into chronological evidence order: initial report, pass 6, pass 7, pass 8, pass 9, then this pass 10 entry.
- Added `2026-05-21 SHINOBU_236 Bilateral DRS Upscaler Vault Payload Boundary` to `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
- The ledger row records owner, source assets, runtime asmdef boundary, Vault BufferIDs, DTO sizes, Data Monolith non-claim, rollback/save exclusion, proof artifacts, and verification caveat.
- Re-ran ledger search, log-order check, forbidden-token static grep, `git diff --check` on the patched docs, and the CPU/dotnet build guard.

Cinematic Cheats used:
- No new simulation. The documented route remains the same screen-space Dear Lie: Sobel depth edge classification spends bilateral ALU only where silhouette blur is visible, while flat surfaces use cheap bilinear reconstruction.

Exact Microseconds saved:
- Runtime frame cost changed by 0 us; pass 10 is documentation/integration repair.
- Review/integration cost is reduced because payload ownership and log chronology no longer require manual reconstruction.
- Static estimates from pass 9 still stand: CBuffer upload avoids ~2-8 us CPU driver churn versus managed globals/`SetData`, and Sobel gating is expected to save ~600-1800 us GPU versus full-screen 5x5 bilateral on MX350-class hardware when edge density is low. Runtime profiler proof remains pending.

Static proof:
- Ledger search now finds SHINOBU_236 and BufferIDs `71050-71056`.
- Log order grep now reports initial report, pass 6, pass 7, pass 8, pass 9, pass 10 in ascending order.
- Forbidden grep found only intentional false positives: route-card text naming forbidden `Pack=1`, and `Blit_Operation_Inquisition` searching for `Graphics.Blit`.
- `git diff --check` reported no whitespace errors on the patched docs, only the existing LF/CRLF warning for the ledger.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="10" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="NO_RUNTIME_CODE_CHANGED_LEDGER_AND_LOG_EVIDENCE_REPAIRED" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" telemetryBytes="64" profileBytes="32" tuningBytes="64" />
  <Ledger bufferIds="71050,71051,71052,71053,71054,71055,71056" dataMonolithClaim="false" rollbackSaveExcluded="true" proofArtifactsNamed="true" />
  <LogOrdering chronological="true" order="initial,pass6,pass7,pass8,pass9,pass10" />
  <StaticGate falsePositiveCount="2" falsePositives="route-card Pack=1 enforcement text; Blit scanner Graphics.Blit pattern literal" />
  <ScalabilityCurve note="Pass 10 preserves the existing continuous GlobalQualityWeight curve; no binary quality switch, DTO layout change, save identity change, or authority route change." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="Vault-owned cold acquire/release; render constants double-buffered GraphicsBuffer upload" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie complexityBefore="O(P*25) bilateral every pixel" complexityAfter="O(P*9 Sobel + E*gatedTaps + F*bilinear)" runtimeChangedThisPass="false" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 11 Interop Compile-Risk Patch

What was wrong:
- `BilateralDrsUpscalerContracts.cs` uses `StructLayout`, `FieldOffset`, and `Marshal.OffsetOf` but did not locally import `System.Runtime.InteropServices`.
- The same-frame RenderGraph preparation path still calls the existing parameter job/upload path; this is a known profiler question, not a compile-safe refactor target while builds are blocked.

What was done:
- Added `using System.Runtime.InteropServices;` to `Assets/_Project/Scripts/Rendering/BilateralDrs/BilateralDrsUpscalerContracts.cs`.
- Inspected `TryPrepareRenderGraphConstantsInternal`, `RunParameterKernel`, and `CalculateUpscalerParamsJob`. I did not duplicate the parameter evaluator in C# because Task 06 requires the Burst parameter kernel and there is no legal compile/profiler window to validate a second math path.
- Re-ran import/layout token scan, forbidden-pattern scan, `git diff --check`, and the CPU/dotnet guard.

Cinematic Cheats used:
- No new simulation. The runtime visual cheat remains Sobel-depth silhouette detection plus bilateral taps only where geometry edges need protection.

Exact Microseconds saved:
- Runtime frame cost changed by 0 us.
- Compile-risk reduction prevents a hard asmdef compile failure; profiler proof for the remaining same-frame CBuffer update cost is still pending.

Static proof:
- `BilateralDrsUpscalerContracts.cs` now has exactly one `using System.Runtime.InteropServices;` import and still contains explicit 32B/64B layouts.
- Forbidden grep found only intentional false positives: `Blit_Operation_Inquisition` scanner literal `Graphics.Blit`, and route-card enforcement text naming forbidden `Pack=1`.
- `git diff --check` reported no whitespace errors on patched files, only the existing ledger LF/CRLF warning.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="11" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="COMPILE_RISK_PATCHED_NO_RUNTIME_ALGORITHM_CHANGE" />
  <CompileRisk file="BilateralDrsUpscalerContracts.cs" import="System.Runtime.InteropServices" reason="StructLayout, FieldOffset, Marshal.OffsetOf" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" />
  <RenderGraphSameFramePath inspected="true" duplicatedEvaluatorAdded="false" reason="Task06 Burst kernel ownership plus compile window blocked" />
  <StaticGate falsePositiveCount="2" falsePositives="route-card Pack=1 enforcement text; Blit scanner Graphics.Blit pattern literal" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie runtimeChangedThisPass="false" route="Sobel depth mask plus gated bilateral reconstruction" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 12 Assembly/API Boundary Audit

What was wrong:
- A separated rendering asmdef can still fail import if it lacks unsafe permission, if editor code cannot reference its runtime types, if Core symbols moved, or if the RenderGraph compute API shape differs from current local usage.
- Compile proof is still unavailable because the CPU guard reports 100% load.

What was done:
- Re-extracted the complete `<AGENT_PROMPT id="SHINOBU_236">` block from `Docs/Tasks/CURRENT_BATCH.md` using the line-based CLI extraction.
- Re-read relevant mandates: ARM64 runtime struct layout, URP RenderGraph hot path, descriptor binding reality, Zero-GC policy, MX350 compute kernels, and postmortem telemetry.
- Audited `Hecton8.Rendering.BilateralDrs.asmdef`: `allowUnsafeCode=true`, `autoReferenced=false`, references Core/Core.Contracts/Core.Memory and Unity Burst/Collections/Jobs/Mathematics/CoreRP/URP only.
- Audited `Hecton8.Editor.asmdef`: explicitly references `Hecton8.Rendering.BilateralDrs` and has `allowUnsafeCode=true` for editor layout validation.
- Verified current-source symbols for `IResolutionScalerService`, `ResolutionScaleState`, `DrsStateDTO`, `IDataVault`, `VaultGenerationHandle<T>`, `IDispatcherSystem`, `ILateFrameTickable`, `IGlobalRegistryHotSwapListener`, `SystemID.GraphicsScalability`, and BufferIDs `71050-71056`.
- Compared SHINOBU_236 RenderGraph compute usage against existing local features using `AddComputePass`, `SetComputeTextureParam(TextureHandle)`, `ImportBuffer`, `UseBuffer`, and `SetComputeConstantBufferParam(GraphicsBuffer)`.
- Re-ran forbidden-pattern static grep, `git diff --check`, log-order grep, and CPU/dotnet guard.

Cinematic Cheats used:
- No new simulation. The active fake remains depth Sobel edge classification plus cheap bilinear on flat pixels and gated bilateral only on silhouettes.

Exact Microseconds saved:
- Runtime frame cost changed by 0 us in pass 12.
- Compile-wall risk is reduced by proving the isolated runtime/editor asmdef route and avoiding speculative broad-assembly moves.
- Existing estimates still stand: CBuffer upload avoids roughly 2-8 us CPU driver churn versus managed globals/`SetData`; Sobel gating is expected to save roughly 600-1800 us GPU versus full-screen 5x5 bilateral on MX350-class hardware when edge density is low. Runtime profiler proof remains pending.

Static proof:
- Forbidden grep found only intentional false positives: `Blit_Operation_Inquisition` scanner literal `Graphics.Blit`, and route-card text naming forbidden `Pack=1`.
- `git diff --check` reported no whitespace errors on checked files, only LF/CRLF warnings for touched existing files.
- Log order remains chronological through pass 11 before this appended pass 12.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="12" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="ASMDEF_API_AUDIT_NO_SOURCE_PATCH_JUSTIFIED" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" telemetryBytes="64" tuningBytes="64" profileBytes="32" />
  <AssemblyBoundary runtimeAsmdef="Hecton8.Rendering.BilateralDrs" allowUnsafeCode="true" autoReferenced="false" directSiblingRuntimeRefs="0" references="Hecton8.Core,Hecton8.Core.Contracts,Hecton8.Core.Memory,Unity.Burst,Unity.Collections,Unity.Jobs,Unity.Mathematics,Unity.RenderPipelines.Core.Runtime,Unity.RenderPipelines.Universal.Runtime" />
  <EditorBoundary editorAsmdef="Hecton8.Editor" referencesRuntimeAsmdef="true" allowUnsafeCode="true" />
  <CoreSymbols verified="IResolutionScalerService,ResolutionScaleState,DrsStateDTO,IDataVault,VaultGenerationHandle,IDispatcherSystem,ILateFrameTickable,IGlobalRegistryHotSwapListener,SystemID.GraphicsScalability,BufferIDs71050-71056" />
  <RenderGraphApi verifiedAgainstLocalExamples="true" methods="AddComputePass,SetComputeTextureParam,ImportBuffer,UseBuffer,SetComputeConstantBufferParam" />
  <StaticGate falsePositiveCount="2" falsePositives="route-card Pack=1 enforcement text; Blit scanner Graphics.Blit pattern literal" />
  <ScalabilityCurve note="No binary switch or shader multi_compile entered SHINOBU_236; GlobalQualityWeight remains continuous and presentation-only." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="Vault-owned cold acquire/release, phase-local NativeArray resolution, double GraphicsBuffer CBuffer upload" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie complexityBefore="O(P*25) bilateral every pixel" complexityAfter="O(P*9 Sobel + E*gatedTaps + F*bilinear)" runtimeChangedThisPass="false" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 13 Unsigned Threadgroup Overload Patch

What was wrong:
- Subagent audit found `CeilByThreadGroup` using `Mathf.Max(1u, threadGroupSize)`.
- Unity `Mathf` overloads are float/int-oriented; unsigned argument binding is brittle in an isolated asmdef even if a compiler might convert it.

What was done:
- Patched `HectonBilateralDrsUpscalerFeature.CeilByThreadGroup` to clamp the unsigned group size with `System.Math.Max(1u, threadGroupSize)`.
- Kept the dispatch count clamp as `Math.Max(1, Mathf.CeilToInt(...))`.
- Re-ran targeted overload grep, forbidden-pattern scan, `git diff --check`, rationale loop-order grep, and CPU/dotnet guard.

Cinematic Cheats used:
- No new simulation. The existing Sobel depth edge mask still gates bilateral work to silhouettes and leaves flat pixels on cheap bilinear reconstruction.

Exact Microseconds saved:
- Runtime frame cost changed by 0 us; dispatch math is equivalent.
- Compile/import risk is reduced by removing unsigned `Mathf` overload ambiguity.
- Existing runtime estimates remain unchanged and still require profiler proof.

Static proof:
- Targeted grep found no remaining `Mathf.Max(1u...)` in `HectonBilateralDrsUpscalerFeature.cs`.
- Forbidden grep still reports only intentional false positives: scanner literal `Graphics.Blit` and route-card `Pack=1` enforcement text.
- `git diff --check` reported no whitespace errors on pass 13 touched files.
- Rationale loop order now reads Loop 10, Loop 11, Loop 12, Loop 13 in order.
- CPU guard: `CPU=93.01`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="13" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="SUBAGENT_COMPILE_RISK_PATCHED_NO_RUNTIME_ALGORITHM_CHANGE" />
  <Patch file="HectonBilateralDrsUpscalerFeature.cs" method="CeilByThreadGroup" oldRisk="Mathf.Max unsigned overload ambiguity" newRoute="System.Math.Max for uint, System.Math.Max for int dispatch clamp" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" unchanged="true" />
  <ScalabilityCurve note="Dispatch clamp patch does not change GlobalQualityWeight behavior; shader quality remains continuous with no binary tier switch." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="unchanged" />
  <StaticGate falsePositiveCount="2" falsePositives="route-card Pack=1 enforcement text; Blit scanner Graphics.Blit pattern literal" remainingUnsignedMathfMax="0" />
  <CompileGuard cpu="93.01" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie runtimeChangedThisPass="false" route="Sobel depth mask plus gated bilateral reconstruction" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 14 Log Chronology Repair

What was wrong:
- `LOG_SHINOBU_236.md` had regressed into non-chronological order after the pass 13 append: pass 13 appeared before pass 12, pass 10, and pass 11.
- The file also had separator duplication risk from prior mechanical reorders.

What was done:
- Mechanically reordered the log into strict top-old/bottom-new chronology: initial report, pass 6, pass 7, pass 8, pass 9, pass 10, pass 11, pass 12, pass 13, then this pass 14 entry.
- Normalized inter-section separators to a single `---`.
- Re-extracted the SHINOBU_236 prompt from `CURRENT_BATCH.md`.
- Re-ran targeted unsigned `Mathf.Max` grep, forbidden-pattern static grep, `git diff --check`, and CPU/dotnet guard.

Cinematic Cheats used:
- No new simulation. The runtime fake remains the Sobel depth edge mask plus gated bilateral reconstruction only on silhouette pixels.

Exact Microseconds saved:
- Runtime frame cost changed by 0 us.
- Evidence-chain repair reduces integration/review risk only; no GPU or CPU runtime microseconds are claimed for pass 14.

Static proof:
- Log order grep reports initial report, pass 6, pass 7, pass 8, pass 9, pass 10, pass 11, pass 12, pass 13 in ascending order before this appended pass.
- Targeted grep found no remaining `Mathf.Max(1u...)` in `HectonBilateralDrsUpscalerFeature.cs`.
- Forbidden grep still reports only intentional false positives: scanner literal `Graphics.Blit` and route-card `Pack=1` enforcement text.
- `git diff --check` reported no whitespace errors on checked files.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="14" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="EVIDENCE_CHAIN_REPAIRED_NO_RUNTIME_ALGORITHM_CHANGE" />
  <LogOrdering chronological="true" order="initial,pass6,pass7,pass8,pass9,pass10,pass11,pass12,pass13,pass14" separators="single" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" unchanged="true" />
  <ScalabilityCurve note="No runtime math changed; GlobalQualityWeight remains continuous and presentation-only." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="unchanged" />
  <StaticGate falsePositiveCount="2" falsePositives="route-card Pack=1 enforcement text; Blit scanner Graphics.Blit pattern literal" remainingUnsignedMathfMax="0" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie runtimeChangedThisPass="false" route="Sobel depth mask plus gated bilateral reconstruction" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 15 Renderer Install Bridge and Resource Fail-Close

What was wrong:
- Static renderer asset grep showed `Mobile_Renderer.asset`, `PC_Renderer.asset`, `PC_High_Renderer.asset`, and `Quest_VR_Renderer.asset` do not yet serialize `HectonBilateralDrsUpscalerFeature`; without Unity import/installer execution the feature is source-present but renderer-inert.
- The RenderGraph pass rejected multi-slice textures but still allowed non-2D or MSAA descriptors into `Texture2D`/`RWTexture2D` compute kernels.
- Non-finite raw depth fallback used `1.0`, which is wrong as a far-depth sentinel on reversed-Z platforms.
- After full-size output assignment, `cameraTargetDescriptor` was not updated to the reconstructed size.

What was done:
- Added `Assets/_Project/Scripts/Editor/BilateralDrsRendererFeatureInstaller.cs`. It is editor-only, targets PC, PC_High, Mobile, and Quest renderer assets, creates/reuses the SHINOBU feature as a sub-asset, appends the renderer feature through `SerializedObject`, rebuilds `m_RendererFeatureMap` from real local file IDs, binds `Hecton_BilateralUpscale.compute`, and avoids manual YAML surgery.
- Added RenderGraph input fail-close for `TextureDimension.Tex2D`, `slices == 1`, and `MSAASamples.None`.
- Added reversed-Z aware non-finite depth fallback in `Hecton_BilateralUpscale.compute`.
- Updated `cameraTargetDescriptor.width/height` after normal and debug upscale output routes.
- Updated SHINOBU_236 architecture docs with installer route, unsupported resource fail-close, and the `_ScreenSize` residual risk.

Cinematic Cheats used:
- No physics or temporal history was added. The fake remains Sobel depth edge detection plus gated bilateral reconstruction only where silhouettes need it. Unsupported XR/MSAA paths fail closed instead of spawning resolve work without proof.

Exact Microseconds saved:
- Installer: 0 runtime us; editor/import-only.
- Descriptor/depth patches: no claimed measurable frame savings. They prevent invalid dispatch/artifacts rather than optimizing a measured hot path.
- Avoided YAML corruption risk: integration-time reliability only, no runtime microseconds claimed.

Static proof:
- Scoped forbidden scan reports only intentional false positives: `Blit_Operation_Inquisition` scanner literal `Graphics.Blit`, route-card `Pack=1` prohibition text, and route-card `AddUnsafePass` rejection text.
- Trailing-whitespace scan over pass 15 touched files reports no hits.
- Renderer asset grep currently reports `NOT_YET_SERIALIZED` for PC, PC_High, Mobile, and Quest; this is expected until Unity imports and executes the editor installer.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="15" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="RENDERER_INSTALL_BRIDGE_AND_RESOURCE_FAIL_CLOSE_PATCHED" />
  <RendererInstall authority="editor SerializedObject" targets="PC_Renderer,PC_High_Renderer,Mobile_Renderer,Quest_VR_Renderer" yamlHandEdit="false" rendererAssetsSerializedNow="false" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" unchanged="true" />
  <ScalabilityCurve note="No binary quality switch added. Unsupported non-2D/MSAA/XR descriptors fail closed; supported paths retain continuous GlobalQualityWeight tap/radius behavior." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="unchanged" />
  <StaticGate falsePositiveCount="3" falsePositives="route-card Pack=1 enforcement text; route-card AddUnsafePass rejection text; Blit scanner Graphics.Blit pattern literal" rendererAssets="NOT_YET_SERIALIZED_UNTIL_UNITY_IMPORT" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie runtimeChangedThisPass="resource safety only" route="Sobel depth mask plus gated bilateral reconstruction" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 16 RenderGraph Recording Purity

What was wrong:
- `RecordRenderGraph` still had a same-frame owner-work escape hatch through `TryPrepareRenderGraphConstants`; that path could initialize services, touch Vault, run `job.Run()`, and upload a CBuffer while recording the graph.
- The blit scanner nested the previous full JSON report inside the next report, creating recursive evidence growth.
- The editor tuner refreshed and formatted labels every editor update.

What was done:
- Removed `TryPrepareRenderGraphConstants` and its internal implementation.
- Added `TryGetActiveConstantBufferForDimensions`, a pure static read of the owner-published CBuffer snapshot with exact low/full dimension validation.
- `RecordRenderGraph` now imports only a matching already-published CBuffer and fail-closes on stale/missing data.
- `AddRenderPasses` submits source/full dimensions and jitter for the next owner phase without Vault/job/upload work.
- Replaced nested scanner JSON with bounded `priorReportBytes` and `priorReportFnv1A`.
- Throttled `BilateralDrsTunerWindow` readout refresh to 8 Hz and only assigns label text when changed.

Cinematic Cheats used:
- No new simulation. The render path still uses the Sobel edge-mask fake plus bilateral reconstruction only on needed silhouette pixels. Dimension mismatch now fails closed rather than doing hidden same-frame CPU work.

Exact Microseconds saved:
- Removed possible graph-recording Vault/job/CBuffer upload cost: exact profiler proof pending; likely small but latency-sensitive on weak CPUs.
- Scanner JSON no longer grows recursively: editor-only, bounded write size.
- Tuner repaint churn reduced from editor-frame rate to 8 Hz: editor-only, no runtime microseconds claimed.

Static proof:
- No `TryPrepareRenderGraphConstants` or `TryPrepareRenderGraphConstantsInternal` remains in SHINOBU_236 source/docs.
- Scoped forbidden scan reports only intentional false positives: scanner literal `Graphics.Blit`, route-card `Pack=1` prohibition text, and route-card `AddUnsafePass` rejection text.
- Trailing-whitespace scan and `git diff --check` reported no issues on pass 16 touched files.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="16" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="RENDERGRAPH_RECORDING_OWNER_WORK_REMOVED" />
  <RenderGraphPurity recordPath="pure published-buffer read" removed="TryPrepareRenderGraphConstants, same-frame Vault/job/upload" failClose="stale or missing dimension-matched CBuffer" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" unchanged="true" />
  <ScalabilityCurve note="No quality switch added. Dimension changes can fail-close for one frame until owner VisualSync publishes a matching CBuffer; shader quality remains continuous." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="unchanged" graphRecordingVaultAccess="0" />
  <StaticGate falsePositiveCount="3" falsePositives="route-card Pack=1 enforcement text; route-card AddUnsafePass rejection text; Blit scanner Graphics.Blit pattern literal" tryPrepareReferencesInActiveDocsAndSource="0" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie runtimeChangedThisPass="owner-route purity only" route="Sobel depth mask plus gated bilateral reconstruction" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 17 Compute Backend Fail-Close

What was wrong:
- `HectonBilateralDrsUpscalerFeature` checked for the compute shader asset and kernels but did not fail-close on graphics backends where Unity reports compute shaders unsupported.
- Transient edge-mask/output UAV descriptors were mono by intent but relied on RenderGraph constructor defaults for `dimension`, `slices`, and `vrUsage`.

What was done:
- Added `SystemInfo.supportsComputeShaders` guards before pass enqueue and before RenderGraph recording.
- Explicitly set edge-mask and upscaled output descriptors to `TextureDimension.Tex2D`, `slices = 1`, and `VRTextureUsage.None`.
- Updated SHINOBU_236 route docs to state the unsupported-compute fail-close and explicit mono UAV contract.

Cinematic Cheats used:
- No fallback blit, CPU scaler, temporal history, or physics was added. Unsupported compute backends omit the pass; supported paths keep the Sobel depth-mask fake plus gated bilateral reconstruction.

Exact Microseconds saved:
- No measurable frame-time claim. The patch prevents invalid dispatch/fallback drift and descriptor ambiguity; profiler proof remains pending.

Static proof:
- Prompt was re-extracted from `CURRENT_BATCH.md`.
- Scoped forbidden scan reports only intentional false positives: `Blit_Operation_Inquisition` scanner literal `Graphics.Blit`, route-card `Pack=1` prohibition text, and route-card `AddUnsafePass` rejection text.
- Direct sibling-runtime reference scan over `Assets/_Project/Scripts/Rendering/BilateralDrs` produced no hits.
- Trailing-whitespace scan over SHINOBU source/docs produced no hits.
- `git diff --check` reported no whitespace errors on checked tracked paths.
- CPU guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; compile still deferred by rule.

<SELF_AUDIT agent="SHINOBU_236" pass="17" status="PENDING_COMPILE_CPU_GUARD">
  <TaskReconciliation count="20" result="COMPUTE_BACKEND_FAIL_CLOSE_AND_MONO_UAV_DESCRIPTOR_CONTRACT_PATCHED" />
  <RenderGraphFailClose computeUnsupported="true" enqueueGuard="true" recordGuard="true" fallbackBlit="false" />
  <TextureDescriptorContract edgeMask="Tex2D,slices=1,VRTextureUsage.None" output="Tex2D,slices=1,VRTextureUsage.None" shaderResources="Texture2D/RWTexture2D" />
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" resolutionOffset="0" filterOffset="16" paddingBytes="0" unchanged="true" />
  <ScalabilityCurve note="No binary quality switch added. Unsupported compute backends fail closed; supported devices retain continuous GlobalQualityWeight tap/radius behavior." />
  <VaultStatus privateNativeArrays="0" buffers="71050-71056" lifecycle="unchanged" graphRecordingVaultAccess="0" />
  <StaticGate falsePositiveCount="3" falsePositives="route-card Pack=1 enforcement text; route-card AddUnsafePass rejection text; Blit scanner Graphics.Blit pattern literal" directSiblingRuntimeRefs="0" trailingWhitespaceSourceDocs="0" />
  <CompileGuard cpu="100.00" dotnetOrCscActive="false" build="DEFERRED_BY_RULE" />
  <DearLie runtimeChangedThisPass="descriptor/backend safety only" route="Sobel depth mask plus gated bilateral reconstruction" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 18 Quality-Gated Sobel and Build Guard

What was wrong:
- `AddRenderPasses` could treat an ambiguous full-size `cameraTargetDescriptor` as the low-res DRS source, causing dimension mismatch against the owner-published CBuffer.
- Very low `GlobalQualityWeight` collapsed the shader to bilinear, but the pass still dispatched Sobel and paid 9 depth reads per output pixel.
- XR/MSAA/non-2D descriptors were rejected during graph recording, after the pass could already be enqueued.
- Unsupported constant-buffer binding had no dedicated fault flag.
- Renderer feature source could exist without serialized renderer asset references; generated `.csproj` files currently do not cover the isolated BilateralDrs asmdef, so dotnet build would not prove this code path.

What was done:
- `AddRenderPasses` now submits `0` low-dimension sentinels unless the descriptor proves a scaled source or full-resolution test mode is forced.
- `AddRenderPasses` now rejects unsupported compute backends, XR, non-2D, array, and MSAA descriptors before enqueue.
- `RecordRenderGraph` skips the Sobel pass at the zero-contribution edge of the continuous quality curve, publishes a graph-cleared edge mask, and the shader returns manual bilinear before reading the edge mask or entering bilateral taps.
- Runtime unsupported CBuffer binding now sets `FaultConstantBufferUnsupported` and requests a one-shot black-box dump.
- `BilateralDrsRendererFeatureBuildGuard` now invokes the editor installer and verifies renderer feature references, feature-map entries, compute shader binding, and injection point before player builds.
- Architecture docs, task status, and rationale were updated with the new fail-close, scalar quality-collapse, and build-guard evidence.

Cinematic Cheats used:
- No TAA history, CPU upscale, physics, MSAA resolve, or fallback blit was added. The visual fake remains depth Sobel for geometry silhouettes plus bilateral work only where the mask and continuous quality scalar require it. At the quality floor, the fake collapses to manual bilinear without even paying Sobel bandwidth.

Exact Microseconds saved:
- At quality-gate floor: avoids Sobel's 9 depth reads plus one R8 UAV write per output pixel and skips edge-mask read/bilateral loop. Exact us are PENDING profiler/RenderDoc proof.
- Descriptor fail-close and CBuffer fault flag: no claimed hot-path savings; they prevent invalid dispatch and bad fallback architecture.
- Build guard: 0 runtime us; prevents shipping a source-present but renderer-inert feature.

Static proof:
- Forbidden scan reports only intentional false positives: route-card `Pack=1` prohibition text, route-card `AddUnsafePass` rejection text, and `Blit_Operation_Inquisition` scanner literal `Graphics.Blit`.
- Direct sibling-runtime reference scan over `Assets/_Project/Scripts/Rendering/BilateralDrs` produced no hits.
- Trailing-whitespace scan excluding `.meta` produced no hits.
- `git diff --check` reported no source/doc whitespace errors on checked paths.
- `.csproj` coverage check reported `NO_CSPROJ_COVERAGE_FOR_BILATERAL_DRS`.
- Build guard evidence: first sample reported `CPU=100` with eight active `dotnet` processes; later sample cleared to `CPU=35.81` and `NO_DOTNET_OR_CSC`, but no build was launched because the current generated `.csproj` files do not include the isolated BilateralDrs asmdef.

<SELF_AUDIT agent="SHINOBU_236" pass="18" status="STATIC_SOURCE_PROOF_COMPILE_BLOCKED">
  <TaskReconciliation count="20">
    <Task id="01" result="[PASS]" note="No naive Graphics.Blit/bilinear replacement in SHINOBU runtime; compute route owns DRS reconstruction." />
    <Task id="02" result="[PASS]" note="No TAA/history dependency; current color plus depth only." />
    <Task id="03" result="[PASS]" note="Hot DTOs use raw unmanaged fields; no properties in the Burst payload rows." />
    <Task id="04" result="[PASS]" note="UpscalerParamsDTO 32B, offsets 0/16, padding 0; layout validators remain active." />
    <Task id="05" result="[PASS]" note="Vault-backed mock DRS lane remains the fallback data source for CI/editor isolation." />
    <Task id="06" result="[PASS]" note="CalculateUpscalerParamsJob remains Burst Fast/Standard and owner-phase routed; pass 18 avoids graph-recording job work." />
    <Task id="07" result="[PASS]" note="BilateralUpscale compute remains the reconstruction kernel." />
    <Task id="08" result="[PASS]" note="Sobel depth mask remains the Dear Lie; pass 18 skips it when continuous quality contributes zero edge work." />
    <Task id="09" result="[PASS]" note="Double constant-buffer upload via LockBufferForWrite/MemCpy remains; unsupported CBuffer path now faults instead of falling back." />
    <Task id="10" result="[PASS]" note="GlobalQualityWeight feeds radius/tap/Sobel-collapse continuously; no hardware-tier switch." />
    <Task id="11" result="[PASS]" note="RenderGraph TextureHandle and BufferHandle dependencies remain declared; unsupported descriptors fail closed." />
    <Task id="12" result="[PASS]" note="Jitter remains packed into FilterParams.z residual without expanding DTO." />
    <Task id="13" result="[PASS]" note="Presentation-only route remains outside rollback/save/Merkle truth." />
    <Task id="14" result="[PASS]" note="Vault buffers and persistent GPU buffers remain owner-managed; no per-frame temp container route added." />
    <Task id="15" result="[PASS]" note="300-frame telemetry ring and dump route remain; CBuffer unsupported now sets a domain fault." />
    <Task id="16" result="[PASS]" note="Editor tuner remains editor-only and cached/throttled." />
    <Task id="17" result="[PASS]" note="Cold CSV profile parser and Vault profile table unchanged." />
    <Task id="18" result="[PASS]" note="Edge mask debug route remains; Sobel-skip publishes cleared edge mask to avoid stale debug texture." />
    <Task id="19" result="[PASS]" note="Blit scanner remains bounded and static." />
    <Task id="20" result="[PASS]" note="This pass appends evidence; compile/Unity runtime proof remains honestly pending." />
  </TaskReconciliation>
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" alignment="16-byte lanes">
    <Field name="ResolutionParams" offset="0" sizeBytes="16" contents="lowWidth,lowHeight,highWidth,highHeight" />
    <Field name="FilterParams" offset="16" sizeBytes="16" contents="depthWeight,colorWeight,packedRadiusJitter,quality" />
    <Padding bytes="0" math="16+16=32" />
  </StructLayout>
  <StructLayout primary="UpscalerTelemetryEntry" sizeBytes="64" falseSharing="one row per cache line">
    <FieldGroup offsets="0..28" sizeBytes="32" contents="frameIndex,faultFlags,scale,quality,edgeThreshold,radius,lowPixels,highPixels" />
    <Field name="ResolutionParams" offset="32" sizeBytes="16" />
    <Field name="FilterParams" offset="48" sizeBytes="16" />
    <Padding bytes="0" math="32+16+16=64" />
  </StructLayout>
  <ScalabilityCurve>
    At quality below the zero-contribution edge, Sobel dispatch is omitted, edge mask is graph-cleared, and the shader returns manual bilinear before edge/bilateral reads. Middle qualities progressively admit Sobel-gated edge work and cross/diagonal taps through smoothstep gates. High/ultra keep the fixed 5x5 envelope but spend taps only where depth/color/spatial weights justify it. This changes presentation work only; DTO layout, save identity, rollback authority, and route ownership are unchanged.
  </ScalabilityCurve>
  <VaultStatus privateNativeArrays="0" buffers="71050,71051,71052,71053,71054,71055,71056" lifecycle="runtime acquires generation handles at boot/cold init and releases handles/GPU buffers on shutdown" />
  <PointerAliasingAndDependencyGraph>
    <NoAlias fields="GenerateMockDrsStateJob.MockState,CalculateUpscalerParamsJob.PendingParams,CalculateUpscalerParamsJob.Telemetry" />
    <ConsumedHandles note="Dispatcher PreSimulation owner phase and VisualSync bridge; RenderGraph consumes only already-published GraphicsBuffer snapshots." />
    <OutputHandles note="No arbitrary Complete; no graph-recording job schedule/readback loop; pass 18 did not add new jobs." />
  </PointerAliasingAndDependencyGraph>
  <CompileGuard runtimeAsmdef="Hecton8.Rendering.BilateralDrs" directSiblingRuntimeRefs="0" onlyCoreAndUnityRefs="true" csprojCoverage="NO_CSPROJ_COVERAGE_FOR_BILATERAL_DRS" build="NOT_LAUNCHED_NON_EVIDENTIARY_UNTIL_UNITY_REGENERATES_CSPROJ" latestCpu="35.81" latestDotnetOrCscActive="false" />
  <DearLie before="O(width*height*25) full bilateral or temporal history" after="O(width*height*9 + edgePixels*25 + flatPixels*4), and at quality floor O(width*height*4) with no Sobel" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 19 DTO-Dimensioned Edge Mask Hardening

What was wrong:
- GPU audit found the shader declared `ResolutionParams` but still derived reconstruction dimensions from physical `GetDimensions()`.
- Edge-mask global publication could survive from an older successful frame when graph-valid DRS frames failed closed or became inactive.
- Sobel work jumped from zero to full-resolution immediately above the low quality gate.
- Non-finite DTO rows were dumped but still marked pending for GPU upload.
- Frame-phase fallback could reacquire Vault buffers if `_vaultStateReady` dropped after cold init.
- Renderer installer verified presence, not uniqueness, so duplicate Bilateral DRS feature refs could enqueue duplicate passes.
- HLSL used `isfinite` and `exp2` in the active-edge loop; shader import/device timing remains unproven.

What was done:
- `Hecton_BilateralUpscale.compute` now resolves logical low/full dimensions from the 32B CBuffer, clamps low loads to physical source dimensions, maps depth/edge sampling explicitly, and adds a `ClearEdgeMask` kernel.
- `HectonBilateralDrsUpscalerFeature` now resolves DTO dimensions before accepting the CBuffer, sizes output from DTO full dimensions, scales edge-mask dimensions continuously by the quality gate, maps reduced edge mask in shader, and graph-publishes a 1x1 cleared edge mask on graph-valid skip/fail paths.
- `HectonBilateralDrsUpscalerRuntime` now stops invalid DTOs before GPU upload and fails closed in `RunOwnerPreSimulation` if Vault state is not ready.
- `BilateralDrsRendererFeatureInstaller` now normalizes renderer data to exactly one Bilateral DRS feature reference and verifies clear/Sobel/upscale/debug kernels.
- `BilateralDrsUpscalerContracts` telemetry cursor now guards `int.MinValue`.
- `AddRenderPasses` indentation/block shape was repaired after pass 18 patch churn.

Cinematic Cheats used:
- Reduced-resolution edge mask: silhouette detection scales from 37.5% to 100% area via smoothstep quality, then the full-res output maps into that mask.
- 1x1 black edge-mask publication: inactive/fail frames invalidate stale debug/global edge state without CPU readback or forbidden `Shader.SetGlobalTexture`.
- Rational falloff: active-edge weights avoid `exp2` transcendental ALU while preserving monotonic depth/color/spatial rejection.

Exact Microseconds saved:
- Low quality near the Sobel gate now pays about 14% of full Sobel pixel area at the 37.5% edge-mask floor instead of 100%; exact Quest/MX350 GPU us remain PENDING RenderDoc/Profiler proof.
- Removing three `exp2` calls per active tap reduces shader special-function pressure in the 5x5 edge loop; exact instruction/timing proof remains PENDING.
- Non-finite upload stop and frame-phase Vault fail-close are correctness/jitter savings, not claimed steady-state microseconds.
- Duplicate renderer feature normalization can prevent a full duplicate Sobel/upscale dispatch after Unity import; renderer asset serialization remains PENDING.

Static proof:
- Prompt block re-extracted from `Docs/Tasks/CURRENT_BATCH.md`.
- Active mandates re-read: `GPU_Compute_Kernels_Kernels_Optimization_MX350.txt`, `REND_DescriptorBinding_Reality_Check.txt`.
- Forbidden scan reports only intentional false positives: route-card `Pack=1` prohibition text, route-card `AddUnsafePass` rejection text, and `Blit_Operation_Inquisition` scanner literal `Graphics.Blit`.
- Direct sibling-runtime reference scan over `Assets/_Project/Scripts/Rendering/BilateralDrs` produced no hits.
- Trailing-whitespace scan over active source/docs/logs produced no hits.
- HLSL scan produced no `isfinite`, `exp2`, `multi_compile`, `shader_feature`, `Texture2DArray`, or `RWTexture2DArray`.
- Scoped `git diff --check` produced no source/doc whitespace errors.
- Renderer asset grep remains `NOT_YET_SERIALIZED` for PC, PC_High, Mobile, and Quest renderer assets until Unity imports/runs the installer.
- `.csproj` coverage scan produced no BilateralDrs hits; current generated projects cannot prove the isolated asmdef.
- Build guard: `CPU=100.00`, `NO_DOTNET_OR_CSC`; dotnet build was not launched.

<SELF_AUDIT agent="SHINOBU_236" pass="19" status="STATIC_SOURCE_PROOF_COMPILE_BLOCKED">
  <TaskReconciliation count="20">
    <Task id="01" result="[PASS]" note="No naive Graphics.Blit upscaler introduced; scanner literal remains editor-only evidence." />
    <Task id="02" result="[PASS]" note="No temporal history path added; current color, depth, edge mask, and CBuffer only." />
    <Task id="03" result="[PASS]" note="DTOs remain raw unmanaged fields; telemetry cursor guard added without properties." />
    <Task id="04" result="[PASS]" note="UpscalerParamsDTO remains 32B, offsets 0/16, padding 0." />
    <Task id="05" result="[PASS]" note="Mock DRS lane unchanged; frame phase no longer reacquires Vault if state drops." />
    <Task id="06" result="[PASS]" note="Burst parameter owner still publishes the DTO; RenderGraph only consumes the published snapshot." />
    <Task id="07" result="[PASS]" note="Bilateral compute now uses DTO logical dimensions instead of treating physical GetDimensions as truth." />
    <Task id="08" result="[PASS]" note="Sobel Dear Lie now supports reduced-resolution edge masks and 1x1 clear invalidation." />
    <Task id="09" result="[PASS]" note="CBuffer upload path remains LockBufferForWrite/MemCpy and now rejects non-finite rows before upload." />
    <Task id="10" result="[PASS]" note="Quality gate continuously scales edge-mask area and tap gates; no hardware-tier branch." />
    <Task id="11" result="[PASS]" note="RenderGraph dependencies remain declared for source/depth/edge/output/buffer; clear publication is graph-declared." />
    <Task id="12" result="[PASS]" note="Jitter remains packed in FilterParams.z; shader maps using logical high dims." />
    <Task id="13" result="[PASS]" note="Presentation-only route remains outside rollback/save/Merkle truth." />
    <Task id="14" result="[PASS]" note="Persistent buffers remain owner managed; pass 19 removed frame-phase Vault reacquire." />
    <Task id="15" result="[PASS]" note="Telemetry ring remains 300 entries; non-finite upload now faults and stops before GPU." />
    <Task id="16" result="[PASS]" note="Editor tuner path unchanged by pass 19." />
    <Task id="17" result="[PASS]" note="CSV profile route unchanged by pass 19." />
    <Task id="18" result="[PASS]" note="Edge mask debug now samples reduced masks by mapped dimensions and stale masks are invalidated." />
    <Task id="19" result="[PASS]" note="Static scanner remains bounded; pass 19 evidence added here and in status/rationale." />
    <Task id="20" result="[PASS]" note="Self-audit updated; compile/import/runtime proof remains honestly pending." />
  </TaskReconciliation>
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" alignment="16-byte lanes">
    <Field name="ResolutionParams" offset="0" sizeBytes="16" contents="lowWidth,lowHeight,fullWidth,fullHeight" />
    <Field name="FilterParams" offset="16" sizeBytes="16" contents="depthWeight,colorWeight,packedRadiusJitter,quality" />
    <Padding bytes="0" math="16+16=32" />
  </StructLayout>
  <ScalabilityCurve>
    Quality gate is `smoothstep(0.015,0.075,quality)`. At zero contribution the graph writes a 1x1 black edge mask and the shader returns bilinear before edge/bilateral reads. Above the gate, Sobel area scales continuously from 37.5% to 100% of full dimensions, then shader maps output pixels to that edge mask. Tap gates still scale continuously by radius/quality. No device-class branch, shader keyword, or DTO layout change was introduced.
  </ScalabilityCurve>
  <VaultStatus privateNativeArrays="0" buffers="71050,71051,71052,71053,71054,71055,71056" lifecycle="cold init/editor set/CSV load/hot-swap acquire; frame phase fail-closes if not ready" />
  <PointerAliasingAndDependencyGraph noAlias="GenerateMockDrsStateJob.MockState; CalculateUpscalerParamsJob.Parameters/Telemetry/TelemetryCursor/Tuning/Profiles" graphRoute="RenderGraph consumes published GraphicsBuffer only; no job completion/readback loop" />
  <CompileGuard runtimeAsmdef="Hecton8.Rendering.BilateralDrs" directSiblingRuntimeRefs="0" csprojCoverage="ABSENT" build="NOT_LAUNCHED_CPU_100_AND_NON_EVIDENTIARY" rendererSerialization="NOT_YET_SERIALIZED_UNTIL_UNITY_IMPORT_INSTALLER" />
  <DearLie before="Full-res Sobel and 5x5 bilateral near any positive edge gate" after="Reduced Sobel area + mapped edge mask + bilinear fast path + rational active-edge weights" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 20 Dispatcher-Scheduled Job Route

What was wrong:
- `GenerateMockDrsStateJob` and `CalculateUpscalerParamsJob` were still proven through `IJob.Run()`. That made the work synchronous in the owner phase and kept the job route outside the dispatcher-owned dependency graph.
- Runtime-absent and unsupported-descriptor render paths could leave `_H8BilateralDrsEdgeMask` from an older successful frame.
- Successful output changed descriptor width/height but did not carry the fallback output `graphicsFormat`.
- Cold CSV scratch used `stackalloc byte[512]`, above the local 256-byte stackalloc ceiling.

What was done:
- Added `SimulationKernelBridge` and `PostSimulationPublishBridge`. `PreSimulation` now only captures frame/dimension intent and validates Vault readiness. `Simulation` schedules mock-state and parameter jobs, chains `JobHandle`s, registers the handle with `H8Memory`, and returns it to `SystemDispatcher`. `PostSimulation` publishes the active DTO after dispatcher completion. `VisualSync` remains the only CBuffer upload phase.
- `CalculateUpscalerParamsJob` now reads the scheduled mock-state Vault lane directly when mock fallback is active.
- `HectonBilateralDrsUpscalerFeature` now supports clear-only graph recording and attempts a 1x1 cleared edge-mask publication on graph-valid fail/skip paths.
- Successful normal/debug output now updates `cameraTargetDescriptor.width/height/graphicsFormat`.
- CSV stack scratch was reduced to 256 bytes.
- Route card and architecture notes were updated to match the dispatcher-scheduled route.

Cinematic Cheats used:
- No physical simulation, temporal history, CPU upscale, fallback blit, or managed readback was added. The visual fake remains scalar-driven Sobel/reduced-edge-mask silhouette detection plus gated bilateral reconstruction, collapsing to manual bilinear at the quality floor.

Exact Microseconds saved:
- Main-thread serialization risk is reduced by moving the two scalar owner kernels into the dispatcher dependency graph; exact CPU us remain PENDING profiler proof.
- Clear-only fail paths cost one 1x1 compute dispatch when available and prevent stale debug/global edge state.
- Stack scratch reduction has 0 runtime frame cost.

Static proof:
- Scoped forbidden scan found no `IJob.Run()`, `.Complete()`, `job.Execute()`, `TryPrepareRenderGraphConstants`, `Shader.SetGlobal*`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, hot managed containers, `System.Linq`, `UnityEngine.Random`, `Time.deltaTime`, `TryGetLatestCreated`, or runtime `Pack=1` in the SHINOBU_236 path except intentional route-card/scanner literals.
- `git diff --check` on touched source produced no output.
- Renderer assets remain not serialized until Unity imports/runs the installer.
- Generated `.csproj` files still have no BilateralDrs coverage.
- Build/import guard: `CPU=100.00`; dotnet build and Unity import were not launched.

<SELF_AUDIT agent="SHINOBU_236" pass="20" status="STATIC_SOURCE_PROOF_COMPILE_BLOCKED">
  <TaskReconciliation count="20">
    <Task id="01" result="[PASS]" note="No naive blit/bilinear renderer path added; compute DRS remains the owner route." />
    <Task id="02" result="[PASS]" note="No temporal history/TAA dependency introduced; current color, depth, edge mask, and CBuffer only." />
    <Task id="03" result="[PASS]" note="Hot DTOs remain raw unmanaged fields; no property-backed Burst payload rows." />
    <Task id="04" result="[PASS]" note="UpscalerParamsDTO remains 32B, ResolutionParams offset 0, FilterParams offset 16, padding 0." />
    <Task id="05" result="[PASS]" note="Mock DRS state is scheduled through the Simulation bridge and read by the parameter job." />
    <Task id="06" result="[PASS]" note="Burst parameter kernel is scheduled via JobHandle and published from PostSimulation, not run synchronously in PreSimulation." />
    <Task id="07" result="[PASS]" note="Bilateral compute route unchanged; descriptor format publication repaired." />
    <Task id="08" result="[PASS]" note="Dear Lie edge mask now clears on graph-valid fail/skip paths to avoid stale proof artifacts." />
    <Task id="09" result="[PASS]" note="CBuffer upload remains LockBufferForWrite/MemCpy in VisualSync only." />
    <Task id="10" result="[PASS]" note="GlobalQualityWeight still controls radius, tap gates, Sobel area, and bilinear collapse continuously." />
    <Task id="11" result="[PASS]" note="RenderGraph dependencies remain declared; clear-only mode is graph-declared, not a global setter." />
    <Task id="12" result="[PASS]" note="Jitter remains packed into FilterParams.z without expanding the 32B DTO." />
    <Task id="13" result="[PASS]" note="Presentation-only route remains outside rollback/save/Merkle truth." />
    <Task id="14" result="[PASS]" note="No private persistent NativeArrays added; jobs use Vault generation handles and fail closed if unavailable." />
    <Task id="15" result="[PASS]" note="300-frame telemetry ring remains the black-box artifact; scheduled handle route is recorded in status/rationale." />
    <Task id="16" result="[PASS]" note="Editor facade unchanged except CSV scratch stack ceiling compliance." />
    <Task id="17" result="[PASS]" note="Cold CSV parser remains Span/Vault-backed; stack scratch reduced to 256 bytes." />
    <Task id="18" result="[PASS]" note="Edge-mask debug artifact is explicitly invalidated by 1x1 graph clear on fail paths." />
    <Task id="19" result="[PASS]" note="Static scanner evidence rerun; only scanner/doc literals remain." />
    <Task id="20" result="[PASS]" note="Self-audit and route docs appended; compile/import proof remains pending by guard." />
  </TaskReconciliation>
  <StructLayout primary="UpscalerParamsDTO" sizeBytes="32" alignment="16-byte lanes">
    <Field name="ResolutionParams" offset="0" sizeBytes="16" contents="lowWidth,lowHeight,fullWidth,fullHeight" />
    <Field name="FilterParams" offset="16" sizeBytes="16" contents="depthWeight,colorWeight,packedRadiusJitter,quality" />
    <Padding bytes="0" math="16+16=32" />
  </StructLayout>
  <StructLayout primary="UpscalerTelemetryEntry" sizeBytes="64" falseSharing="one row per cache line">
    <FieldGroup offsets="0..28" sizeBytes="32" contents="frameIndex,faultFlags,scale,quality,edgeThreshold,radius,lowPixels,highPixels" />
    <Field name="ResolutionParams" offset="32" sizeBytes="16" />
    <Field name="FilterParams" offset="48" sizeBytes="16" />
    <Padding bytes="0" math="32+16+16=64" />
  </StructLayout>
  <ScalabilityCurve>
    Below the zero-contribution edge of `smoothstep(0.015,0.075,quality)`, C# omits Sobel, publishes a cleared 1x1 edge mask, and the shader returns manual bilinear before edge/bilateral reads. Above that region, Sobel area and tap participation ramp continuously. No device-tier branch, keyword variant, DTO layout shift, save identity change, or authority-route change was introduced.
  </ScalabilityCurve>
  <VaultStatus privateNativeArrays="0" buffers="71050,71051,71052,71053,71054,71055,71056" lifecycle="cold init/editor set/CSV load/hot-swap acquire; frame phase fail-closes if not ready" />
  <PointerAliasingAndDependencyGraph>
    <ConsumedHandles name="incoming dispatcher Simulation dependency" />
    <OutputHandles name="mockHandle -> parameterHandle -> SystemDispatcher; VisualSync performs GPU upload after owner publication" />
    <NoAlias fields="GenerateMockDrsStateJob.MockState; CalculateUpscalerParamsJob.MockState/PendingParams/Telemetry/TelemetryCursor/Tuning/Profiles" />
    <LocalComplete calls="0" />
  </PointerAliasingAndDependencyGraph>
  <CompileGuard runtimeAsmdef="Hecton8.Rendering.BilateralDrs" directSiblingRuntimeRefs="0" csprojCoverage="ABSENT" build="NOT_LAUNCHED_CPU_100_AND_NON_EVIDENTIARY" rendererSerialization="NOT_YET_SERIALIZED_UNTIL_UNITY_IMPORT_INSTALLER" />
  <DearLie before="O(width*height*25) full bilateral or temporal history" after="O(edgeMaskPixels*9 + edgePixels*25 + flatPixels*4), quality floor O(width*height*4) with no Sobel" />
</SELF_AUDIT>


## 2026-05-21 Ultra-Think Polish Pass 6

What was wrong:
- `TryReadEditorTuning` was not a pure read facade. It could call `EnsureRuntimeInstance()` and `EnsureVaultState()`, which means a `TryRead*` accessor could instantiate a GameObject and acquire/grow Vault buffers.
- `RunParameterKernel()` re-read `GlobalRegistry.ResolutionScaler` in the owner tick path. That violated cold Registry use: dependencies must be cached, then rebound through hot-swap callbacks.
- Jobs were Burst-annotated but invoked through direct `Execute()`, bypassing the job runner path and undermining the Burst proof.
- The edge-mask debug route published an R8 texture but did not provide the requested visible black/green diagnostic output.
- Presentation-only math used `FloatMode.Deterministic`; this upscaler is not rollback/gameplay truth and should spend the cheaper `FloatMode.Fast` path.

What was done:
- `HectonBilateralDrsUpscalerRuntime` now implements `IGlobalRegistryHotSwapListener`.
- `IDataVault` and `IResolutionScalerService` are cached once during cold initialization; DataVault and ResolutionScaler replacement events rebind cached fields without per-frame Registry polling.
- Parameter evaluation now runs through `IDispatcherSystem` `PreSimulation`; CBuffer upload uses a dedicated `IDispatcherSystem` `VisualSync` bridge with `IUpdatable`/`ILateFrameTickable` only as dispatcher-registration fallback.
- `TryReadEditorTuning` now reads only the already-live singleton and resolved handle. It no longer creates runtime objects or acquires buffers.
- Edge-mask debug state is cached in `s_edgeMaskDebugEnabled`, so RenderGraph reads a cheap static flag instead of resolving Vault state from a read accessor.
- `GenerateMockDrsStateJob` and `CalculateUpscalerParamsJob` now run through `job.Run()`.
- `CalculateUpscalerParamsJob` is `unsafe` and writes `UpscalerParamsDTO` through `NativeArrayUnsafeUtility.GetUnsafePtr` plus `UnsafeUtility.AsRef`.
- Both Burst jobs now use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.
- Added compute kernel `EdgeMaskDebugComposite`; when the editor debug flag is enabled, RenderGraph writes black/green fullscreen edge diagnostics and skips the bilateral upscale pass.

Cinematic Cheats used:
- The silhouette detector remains a Sobel depth Dear Lie. It does not simulate temporal history, optical flow, or geometry classification; it reads depth gradients and spends bilateral taps only where the eye catches silhouette smear.
- Debug visualization stays GPU-local. No CPU readback, no gizmo mesh, no SceneView-only path.

Exact microseconds saved:
- Static estimate only. CPU guard blocked compile/profiler again.
- Removed hot Registry read: expected low single-digit microseconds on weak CPU under service contention; exact proof pending.
- Burst `Run()` versus direct `Execute()`: expected owner-kernel scalar improvement; exact proof pending Burst profiler.
- Disabled debug pass cost: 0 us. Enabled debug pass cost: one R8 read and one UAV color write per pixel; tooling only.

Verification:
- Static grep found no `Shader.SetGlobal*`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, `.Complete()`, managed container allocation, `GlobalDataVault.TryGetLatestCreated`, direct `job.Execute()`, or hot `GlobalRegistry` polling in SHINOBU_236 files.
- CPU sample before compile was 100%. No `dotnet`/`csc` process was active, but the >50% CPU guard blocks build launch.

<SELF_AUDIT agent="SHINOBU_236" pass="6" status="PENDING_COMPILE_CPU_GUARD">
  <TaskCount>20</TaskCount>
  <TaskReconciliation>
    <Task id="01" status="PASS" note="No naive SHINOBU_236 blit upscale path introduced." />
    <Task id="02" status="PASS" note="No temporal history dependency introduced." />
    <Task id="03" status="PASS" note="Hot DTOs remain raw-field unmanaged structs." />
    <Task id="04" status="PASS" note="32B/64B explicit layouts preserved." />
    <Task id="05" status="PASS" note="Mock DRS state remains Vault-backed." />
    <Task id="06" status="PASS" note="Burst jobs use FloatMode.Fast and job.Run; parameter write uses unsafe pointer lane." />
    <Task id="07" status="PASS" note="Bilateral compute shader remains current-frame color/depth based." />
    <Task id="08" status="PASS" note="Sobel edge Dear Lie still gates bilateral cost." />
    <Task id="09" status="PASS" note="Double GraphicsBuffer constant upload remains SetData-free." />
    <Task id="10" status="PASS" note="GlobalQualityWeight remains continuous, no low/high binary switch." />
    <Task id="11" status="PASS" note="RenderGraph compute dependencies remain declared." />
    <Task id="12" status="PASS" note="Jitter stays packed inside the 32B CBuffer." />
    <Task id="13" status="PASS" note="Presentation-only path remains outside save/rollback truth." />
    <Task id="14" status="PASS" note="Vault buffers remain uninitialized/cold-owned; no per-frame allocations found." />
    <Task id="15" status="PASS" note="300-entry telemetry ring and dump path preserved." />
    <Task id="16" status="PASS" note="Editor tuning path preserved; read facade purified." />
    <Task id="17" status="PASS" note="CSV profile parser remains cold span-based." />
    <Task id="18" status="PASS" note="Added visible black/green GPU debug composite." />
    <Task id="19" status="PASS" note="Static grep gate rerun for SHINOBU_236 files." />
    <Task id="20" status="PASS" note="Pass 6 audit appended; compile remains honestly blocked by CPU guard." />
  </TaskReconciliation>
  <StructLayout>
    <UpscalerParamsDTO size="32" alignment="16B lanes">
      <Field name="ResolutionParams" offset="0" size="16" />
      <Field name="FilterParams" offset="16" size="16" />
      <Padding bytes="0" />
    </UpscalerParamsDTO>
    <UpscalerTelemetryEntry size="64" cacheLine="true">
      <Fields offsets="0,4,8,12,16,20,24,28,32,48" />
      <Padding bytes="0" />
    </UpscalerTelemetryEntry>
  </StructLayout>
  <ScalabilityCurve>
    <Low note="Quality below 0.3 lowers bypass threshold and collapses tap gates toward bilinear/cross behavior." />
    <Middle note="Quality 0.4-0.7 keeps Sobel-gated 3x3-ish reconstruction." />
    <High note="Quality 0.7-0.9 spends wider silhouette taps." />
    <Ultra note="Quality 1.0 keeps the widest gated edge-preserving reconstruction without changing DTO layout or authority." />
  </ScalabilityCurve>
  <VaultStatus privateNativeArrays="0">
    <Buffer id="71050" name="Params" />
    <Buffer id="71051" name="Tuning" />
    <Buffer id="71052" name="Telemetry" />
    <Buffer id="71053" name="TelemetryCursor" />
    <Buffer id="71054" name="Profiles" />
    <Buffer id="71055" name="CsvScratch" />
    <Buffer id="71056" name="MockState" />
  </VaultStatus>
  <DependencyGraph>
    <Registry cold="DataVault,ResolutionScalerService" hotSwap="IGlobalRegistryHotSwapListener" hotPolling="false" />
    <Phases params="IDispatcherSystem.PreSimulation" cbufferUpload="IDispatcherSystem.VisualSyncBridge" />
    <Jobs output="owner writes pending params and telemetry; no Schedule/Complete chain; no hidden Complete" noAlias="true" />
  </DependencyGraph>
  <CompileGuard status="DEFERRED" cpuPercent="100" dotnetOrCscActive="false" />
  <DearLie complexityBefore="O(P*25) full bilateral every output pixel" complexityAfter="O(P*9 Sobel + E*gatedTaps + F*bilinear), where E is edge pixels and F is flat pixels" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 22 CSV Strict-Schema Hardening

What was wrong:
- `TryParseProfileRow` accepted 7-column profile rows and published default `qualityBias`.
- The same parser accepted 9+ numeric columns and ignored them after parsing, so bad authoring data could enter the Vault profile lane without a visible failure.
- UTF-8 BOM on the first row could poison the first header/profile token.

What was done:
- Added `QualityProfileCsvColumnCount = 8`.
- Rejected extra CSV cells before float parsing.
- Required exactly 8 parsed tokens before publishing `UpscalerProfileDTO`.
- Stripped optional UTF-8 BOM after ASCII trim.

Cinematic Cheats used:
- None added in this pass. Existing Dear Lie route remains Sobel-gated bilateral reconstruction, reduced edge-mask area, 1x1 stale-mask invalidation, and rational falloff instead of heavy exponentials.

Exact Microseconds saved:
- Hot path: 0 us claimed. This is cold profile ingestion.
- Cold path: two integer comparisons per token and one optional BOM branch; below useful measurement noise.
- Real saving is defect containment: malformed profile data no longer silently changes low/middle/high/ultra filter radius or quality bias.

Static proof:
- Source edit is confined to `Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerRuntime.cs`.
- No DTO layout, shader CBuffer layout, dispatcher route, Vault BufferID, save identity, or renderer feature serialization path changed.
- Dotnet/Unity proof remains pending until guard conditions allow an evidentiary import/build.

---

## SHINOBU_236 Pass 23 Dispatcher Fail-Closed Route

What was wrong:
- Dispatcher route registration was non-atomic; a partial PreSimulation/Simulation/PostSimulation/VisualSync route could advance dimensions without jobs, or schedule jobs without publication/upload.
- Vault resolve and upload failures could leave `s_hasPublishedParameters` true, so a stale CBuffer could still satisfy RenderGraph dimension checks.
- Clear-only edge-mask publication required Sobel/upscale/debug kernels even though the stale-mask repair needs only `ClearEdgeMask`.
- Native safety comments were not explicit enough about owner lanes, no-overlap proof, and rejected alternatives.

What was done:
- Added `RegisterDispatcherRouteAllOrFail`, partial route rollback, and `_dispatcherRouteReady` gating.
- Added `InvalidatePublishedParameters` and fail-closed invalidation on Simulation resolve failure, PostSimulation publish resolve failure, and VisualSync upload/CBuffer failure.
- Split RenderGraph kernel resolution into `TryResolveClearKernel` and `TryResolveActiveKernels`.
- Expanded safety comments for `MockState`, `Parameters`, `Telemetry`, and `TelemetryCursor` to document owner, handle chain, no-overlap proof, and rejected local completion/private-array routes.
- Updated SHINOBU_236 route-card and architecture notes with all-or-fail dispatcher registration, stale CBuffer invalidation, and clear-only kernel requirements.

Cinematic Cheats used:
- No new visual fake added. This pass preserves the existing Dear Lie path: reduced Sobel area, black 1x1 stale-mask invalidation, flat-pixel bilinear bypass, and rational edge weights.

Exact Microseconds saved:
- Hot success path: 0 us claimed. Registration is cold and failure invalidation is branch/flag work only.
- Failure path: avoids scheduling/rendering with invalid or stale constants; no profiler-backed microsecond claim.

Static proof:
- Scoped forbidden scan over SHINOBU_236 runtime/editor/compute path returned no `.Run`, local completion, `job.Execute`, `Shader.SetGlobal*`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, hot managed NativeArray allocation, LINQ, `UnityEngine.Random`, `Time.deltaTime`, `TryGetLatestCreated`, or runtime `Pack=1` hits.
- Scoped `git diff --check` passed for the modified SHINOBU_236 source files.
- Dotnet build and Unity import were not launched: CPU guard reported 100%, and generated `.csproj` files still have no BilateralDrs coverage until Unity import regenerates projects.

---

## SHINOBU_236 Pass 24 Post-Patch Static Verification

What was wrong:
- Pass 23 altered registration/fail-close code after the previous scan window; evidence had to be regenerated from disk.

What was done:
- Re-ran forbidden hot-path grep over `Assets/_Project/Scripts/Rendering/BilateralDrs`, `BilateralDrsRendererFeatureInstaller.cs`, and `Hecton_BilateralUpscale.compute`.
- Re-ran trailing-whitespace scan and `git diff --check` over touched SHINOBU_236 source/docs.
- Rechecked `.csproj` coverage, renderer asset serialization coverage, direct sibling-runtime references, compute shader portability, `SystemDispatcher.Register` duplicate behavior, and `H8Memory.RegisterActiveJob` signature.
- Re-extracted the SHINOBU_236 prompt block from `Docs/Tasks/CURRENT_BATCH.md`.
- Classified older log mentions of `IJob.Run`, `TryPrepareRenderGraphConstants`, and same-frame RenderGraph upload as historical only; current source scan shows those routes removed.

Cinematic Cheats used:
- No new rendering math. Existing Dear Lie remains Sobel-gated/reduced-resolution edge mask plus manual bilinear on flat/low-quality pixels.

Exact Microseconds saved:
- 0 us runtime; this was evidence regeneration.
- Build not launched: latest CPU guard reported 77% with seven active `dotnet` processes, and generated `.csproj` files still have no BilateralDrs coverage.

---

## SHINOBU_236 Pass 25 Quality-Gate Epsilon Cliff Removal

What was wrong:
- The C# Sobel skip and HLSL bilinear early return used `qualityGate <= 0.0001`, widening the zero-work region beyond the actual continuous smoothstep endpoint.

What was done:
- Changed C# `skipSobel` to exact `qualityGate == 0f`.
- Changed HLSL early return to exact `qualityGate == 0.0`.
- Re-ran scoped forbidden scan, threshold scan, shader portability scan, whitespace scan, and `git diff --check`.

Cinematic Cheats used:
- Existing Dear Lie remains intact: exact-zero quality publishes/uses the cheap cleared edge mask and bilinear path; non-zero quality ramps edge-mask resolution and tap gates continuously.

Exact Microseconds saved:
- No profiler-backed runtime saving claimed. The patch removes a continuity defect; hot-path cost is one scalar comparison with the same branch structure.
- Build not launched: latest guard reported CPU 94 with seven active `dotnet` processes, generated `.csproj` files still have no BilateralDrs coverage, and renderer assets still await Unity import/installer serialization.

---

## SHINOBU_236 Pass 26 XR Array Route and Fail-Closed Fallback Removal

What was wrong:
- XR texture-array inputs were previously fail-closed, blocking the Bilateral DRS path for stereoscopic VR.
- The edge-mask resource used one logical name for write and read phases, which is fragile for SRV/UAV declaration separation.
- Runtime dispatcher failure still had a dead `IUpdatable`/`ILateFrameTickable` fallback route, but that route did not schedule the Burst jobs, publish DTOs, or upload the CBuffer.
- Vault resolve/upload failure did not write a specific fault flag before fail-closing.

What was done:
- Added array kernels: `ClearEdgeMaskArray`, `SobelDepthMaskArray`, `BilateralUpscaleArray`, `EdgeMaskDebugCompositeArray`.
- Added `Texture2DArray`/`RWTexture2DArray` shader resources and array load helpers.
- RenderGraph now resolves mono versus array mode from `TextureDesc.dimension`/slice count, creates `Tex2DArray` output and edge-mask descriptors for slice count 1-2, and dispatches Z by slice count.
- Split edge-mask bindings into read/write IDs: `_H8EdgeMaskRead`, `_H8EdgeMaskWrite`, `_H8EdgeMaskArrayRead`, `_H8EdgeMaskArrayWrite`.
- Removed runtime `IUpdatable` and `ILateFrameTickable` fallback registration.
- Added `FaultVaultUnavailable` and one-shot black-box dump request on Vault/CBuffer resolve failures.
- Renderer installer now verifies all eight kernels plus `forceRunAtFullResolution=false` and `activationScale=0.995`.

Cinematic Cheats used:
- The Dear Lie remains GPU-side: reduced-resolution Sobel edge mask plus bilinear flat-pixel bypass. XR array support duplicates the same optical fake per eye slice; it does not add CPU physics or temporal history.

Exact Microseconds saved:
- No profiler-backed timing claim. Static architecture saving is removal of a dead fallback and avoidance of invalid partial-frame work.
- Theoretical complexity remains `O(P*9 Sobel + E*gated taps + F*bilinear)` per eye slice, with edge-mask area and tap gates controlled continuously by `GlobalQualityWeight`.

Static proof:
- Scoped forbidden scan returned no `.Run`, `.Complete`, `job.Execute`, `TryPrepareRenderGraphConstants`, `Shader.SetGlobal*`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, hot managed native-container allocation, LINQ, `UnityEngine.Random`, `Time.deltaTime`, `TryGetLatestCreated`, `Pack=1`, `IUpdatable`, or `ILateFrameTickable` hits in SHINOBU_236 source.
- Quality-threshold scan returned no stale edge-mask names, XR rejection, or epsilon quality cliffs.
- Shader portability scan returned only the intended array declarations/kernels; no `isfinite`, `exp2`, shader keywords, or `_Time`.
- Direct sibling-runtime reference scan returned no hits in `Assets/_Project/Scripts/Rendering/BilateralDrs`.
- `PolishMandateStaticAudit --fail-on-pack-one` passed with warnings and `packOne=0`.
- `BufferIDSovereigntyAudit --fail-on-duplicates` passed with `duplicates=0`.
- `JobCompletionAudit --fail-on-frame-path --fail-on-raw-runtime-complete` passed with warnings; `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `AssemblyDependencyAudit` still fails globally because of legacy `Hecton8.Input` core concrete sibling reference outside SHINOBU_236; cycles remain 0.
- `git diff --check` passed on touched source/docs.
- Dotnet build not launched: latest guard was CPU 36, but active `dotnet` and Unity processes exist, generated `.csproj` files still have no BilateralDrs coverage, and renderer assets still have no serialized BilateralDrs feature until Unity import/installer execution.

<SELF_AUDIT pass="26">
  <TaskReconciliation taskCount="20" result="PASS_STATIC_SOURCE_ONLY">
    <Task id="01" status="PASS" note="No naive SHINOBU_236 blit route introduced." />
    <Task id="02" status="PASS" note="History-free current-color/depth route preserved." />
    <Task id="03" status="PASS" note="DTO remains raw-field unmanaged layout." />
    <Task id="04" status="PASS" note="UpscalerParamsDTO remains 32B explicit layout." />
    <Task id="05" status="PASS" note="Mock DRS lane untouched; fallback does not bypass dispatcher." />
    <Task id="06" status="PASS" note="Dispatcher Simulation job route preserved." />
    <Task id="07" status="PASS" note="Mono and array compute upscale kernels exist." />
    <Task id="08" status="PASS" note="Sobel Dear Lie exists for mono and array paths." />
    <Task id="09" status="PASS" note="CBuffer upload route preserved; no Shader.SetGlobalFloat/Vector." />
    <Task id="10" status="PASS" note="GlobalQualityWeight remains continuous through qualityGate and edge-mask scale." />
    <Task id="11" status="PASS" note="RenderGraph declares texture/buffer read/write dependencies." />
    <Task id="12" status="PASS" note="Jitter packing in FilterParams.z unchanged." />
    <Task id="13" status="PASS" note="Presentation-only route still excluded from rollback/save truth." />
    <Task id="14" status="PASS" note="No private hot NativeArray ownership added." />
    <Task id="15" status="PASS" note="FaultVaultUnavailable now requests black-box dump on Vault/CBuffer failure." />
    <Task id="16" status="PASS" note="Editor tuner unchanged; installer verification strengthened." />
    <Task id="17" status="PASS" note="Cold CSV profile path unchanged." />
    <Task id="18" status="PASS" note="Edge-mask debug composite exists for mono and array paths." />
    <Task id="19" status="PASS" note="Static audit gates rerun." />
    <Task id="20" status="PASS" note="Pass 26 audit appended; build/import proof remains pending." />
  </TaskReconciliation>
  <StructLayout>
    <UpscalerParamsDTO size="32" multipleOf8="true" multipleOf16="true">
      <Field name="ResolutionParams" offset="0" size="16" />
      <Field name="FilterParams" offset="16" size="16" />
      <Padding bytes="0" />
    </UpscalerParamsDTO>
    <UpscalerTelemetryEntry size="64" cacheLine="true">
      <Field name="FrameIndex" offset="0" size="4" />
      <Field name="Flags" offset="4" size="4" />
      <Field name="CurrentRenderScale01" offset="8" size="4" />
      <Field name="TargetRenderScale01" offset="12" size="4" />
      <Field name="QualityScalar" offset="16" size="4" />
      <Field name="BilateralRadiusPixels" offset="20" size="4" />
      <Field name="DepthWeight" offset="24" size="4" />
      <Field name="EstimatedGpuMicros" offset="28" size="4" />
      <Field name="ResolutionParams" offset="32" size="16" />
      <Field name="FilterParams" offset="48" size="16" />
      <Padding bytes="0" />
    </UpscalerTelemetryEntry>
  </StructLayout>
  <VaultStatus privateNativeArrays="0" handles="71050,71051,71052,71053,71054,71055,71056" />
  <DependencyGraph consumes="PreSimulation intent, Simulation dependsOn" outputs="Simulation JobHandle, PostSimulation DTO publish, VisualSync CBuffer upload" noAlias="true" localComplete="false" />
  <CompileGuard directSiblingRuntimeRefs="false" buildLaunched="false" reason="dotnet and Unity active; no BilateralDrs csproj coverage" />
  <DearLie before="O(P*25) full bilateral per eye" after="O(P*9 Sobel + E*gated taps + F*bilinear) per eye slice" />
</SELF_AUDIT>

---

## SHINOBU_236 Pass 27 XR Descriptor Reality Check

What was wrong:
- Array RenderGraph descriptors manually overwrote dimension/slices after constructing `TextureDesc` with `xrReady:false`.
- The descriptor path forced `VRTextureUsage.TwoEyes` for two slices instead of preserving the source texture's `vrUsage`.

What was done:
- Output `TextureDesc` now uses `xrReady: useTextureArray`.
- Edge-mask `TextureDesc` now uses `xrReady: useTextureArray`.
- `sourceDesc.vrUsage` is preserved as `outputVrUsage` for array output and edge-mask descriptors.
- Clear-only mono stale-mask publication remains `VRTextureUsage.None`.

Cinematic Cheats used:
- No new fake. Existing reduced Sobel edge mask plus bilinear flat-pixel bypass remains the Dear Lie.

Exact Microseconds saved:
- 0 us claimed. This is descriptor correctness, not arithmetic optimization.
- It avoids a potential XR import/backend mismatch rather than reducing per-frame ALU.

Static proof:
- `CreateEdgeMaskDesc` reference scan shows all call sites updated to the new `vrUsage` parameter.
- XR descriptor scan shows no remaining `VRTextureUsage.TwoEyes` in SHINOBU_236 source.
- Scoped forbidden hot-path scan returned no hits.
- Quality/XR stale-pattern scan returned no hits.
- Direct tab/trailing-whitespace scan returned no hits.
- Dotnet build not launched: latest guard was CPU 33, active `dotnet` and Unity processes exist, and generated `.csproj` files still have no BilateralDrs coverage.

---

## SHINOBU_236 Pass 28 Raw XR Slice Validation

What was wrong:
- Subagent audit found `TryResolveTextureMode` coerced `TextureDesc.slices` with `Math.Max(1, ...)`.
- A malformed zero-slice array descriptor could therefore be accepted as a one-slice array route.

What was done:
- `sourceDesc.slices` and `depthDesc.slices` are now read raw.
- Raw slice counts `<= 0` fail closed before equality and `<= 2` checks.
- No descriptor is normalized into validity.

Cinematic Cheats used:
- No new visual fake. The existing Sobel edge-mask plus bilinear flat-pixel bypass remains the Dear Lie.

Exact Microseconds saved:
- No profiler-backed saving claimed. The patch adds two RenderGraph setup integer comparisons.
- Runtime arithmetic impact is below measurement noise; it prevents invalid XR descriptor dispatch.

Static proof:
- Subagent verified HLSL array `Load` coordinate shape and `RWTexture2DArray` write shape.
- Raw-slice scan shows no remaining `Math.Max(1, *Desc.slices)` in SHINOBU_236.
- Scoped forbidden hot-path scan returned no hits.
- Quality/XR stale-pattern scan returned no hits.
- Shader portability scan returned no forbidden hits.
- Direct sibling-runtime reference scan returned no hits.
- Direct tab/trailing-whitespace scan returned no hits.
- `.csproj` coverage grep still returns no BilateralDrs hits.
- Dotnet build not launched: latest guard was CPU 50 with Unity active, and generated `.csproj` files still cannot prove this isolated asmdef.

---

## SHINOBU_236 Pass 29 Array Capability Fail-Closed Gate

What was wrong:
- The XR `Texture2DArray` route validated raw slice counts and UAV formats, but did not explicitly require `SystemInfo.supports2DArrayTextures`.
- Existing project rendering code treats 2D array texture support as a hard capability requirement for array-backed water/XR paths.

What was done:
- `TryResolveTextureMode` now rejects `Tex2DArray` source/depth descriptors when `SystemInfo.supports2DArrayTextures` is false.
- `IsUnsupportedRenderTargetDescriptor` now rejects array camera descriptors without 2D-array texture support before enqueueing the active path.
- Project settings audit recorded Android graphics API as Vulkan (`m_APIs: 15000000`, automatic disabled), with no OpenGLES target in the active Android route.
- `SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md` and `BILATERAL_DRS_UPSCALER_SHINOBU_236.md` now document the 2D-array capability gate and repaired stale clear-only kernel wording.

Cinematic Cheats used:
- No new fake. Existing Sobel edge-mask plus bilinear flat-pixel bypass remains the Dear Lie.

Exact Microseconds saved:
- 0 us claimed. One boolean capability check in setup is below measurement noise.
- The patch removes invalid backend dispatch risk; it is not a frame-time optimization.

Static proof:
- Array capability scan shows `supports2DArrayTextures` in both `TryResolveTextureMode` and `IsUnsupportedRenderTargetDescriptor`.
- Scoped forbidden hot-path scan returned no hits.
- Quality/XR stale-pattern scan returned no hits.
- Shader portability scan returned no forbidden hits.
- Direct sibling-runtime reference scan returned no hits.
- Direct whitespace/tab scan returned no hits.
- Scoped `git diff --check` returned no whitespace errors.
- `.csproj` coverage grep still returns no BilateralDrs hits.
- Dotnet build not launched: latest guard was CPU 65.64 with Unity active, and generated `.csproj` files still cannot prove this isolated asmdef.

---

## SHINOBU_236 Pass 30 XR/Renderer Integration Blockers Routed

What was wrong:
- XR provider readiness was not proven by packages alone. Static project settings still show `m_BuildTargetVRSettings: []`, and no serialized XR Management/OpenXR settings assets were found on disk.
- Renderer assets still do not serialize `HectonBilateralDrsUpscalerFeature`; the source installer exists, but the YAML route is not yet changed by Unity import/installer execution.

What was done:
- Added explicit blocker notes to `SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md` and `BILATERAL_DRS_UPSCALER_SHINOBU_236.md`.
- Routed XR provider ownership to the existing platform repair path: `PlatformPortabilityRouteRepairer.WireAndroidQuestXrRoutesForCi()` -> `XrPlatformReadinessValidator.WireAndroidOpenXrProviderRouteForCi()`.
- Kept renderer feature serialization authority with `BilateralDrsRendererFeatureInstaller` and its build guard; no renderer YAML hand-edit was performed.

Cinematic Cheats used:
- No new visual fake. Existing Sobel edge-mask plus bilinear flat-pixel bypass remains the Dear Lie.

Exact Microseconds saved:
- 0 us claimed. This pass is integration-proof hygiene.
- It prevents false Quest/runtime readiness claims and avoids unsafe text mutation of Unity-owned serialized assets.

Static proof:
- `ProjectSettings/ProjectSettings.asset:544` has `m_BuildTargetVRSettings: []`.
- `rg --files ProjectSettings Assets | rg "(XRGeneralSettings|XRManagerSettings|OpenXRSettings|XRPackage|XRPlug)"` returned no serialized XR Management settings assets.
- `Packages/manifest.json` contains `com.unity.xr.management`, `com.unity.xr.openxr`, and `com.unity.xr.meta-openxr`; this is package availability only.
- Renderer asset grep returned `NO_SERIALIZED_BILATERAL_DRS_RENDERER_FEATURE`.
- Post-patch whitespace scan returned no hits.
- Post-patch `git diff --check` returned no whitespace errors for touched SHINOBU_236 docs/logs.
- Scoped forbidden-source scan over BilateralDrs runtime/editor/compute files returned no hits.
- `.csproj` coverage grep still returns no BilateralDrs hits.
- Dotnet build not launched: latest guard was CPU 36.45 with Unity active, and generated `.csproj` files still cannot prove this isolated asmdef.

---

## SHINOBU_236 Pass 31 Quest Depth Route Conflict Documented

What was wrong:
- SHINOBU_236 requires a valid high-resolution depth texture for Sobel edge detection and depth-weighted bilateral reconstruction.
- Static `URP_Quest_VR.asset` currently serializes `m_RequireDepthTexture: 1`, but `QuestVulkanRenderPipelineConfigurator.ConfigureUrpAsset()` writes `m_RequireDepthTexture=false` when the Quest platform repair/build route executes.

What was done:
- Added the depth-route conflict to the SHINOBU route card and architecture note.
- Did not edit the platform configurator and did not invent a color-only fallback inside the depth-bilateral route.

Cinematic Cheats used:
- No new visual fake. Existing Sobel depth edge mask plus bilinear flat-pixel bypass remains the Dear Lie.

Exact Microseconds saved:
- 0 us claimed. This is a route-truth correction.
- If Quest depth stays disabled, SHINOBU avoids its dispatches by failing closed; visual ownership must then come from a different depthless presentation route.

Static proof:
- `HectonBilateralDrsUpscalerFeature.RecordRenderGraph` returns to clear-mask publication when `resourceData.cameraDepthTexture` is invalid.
- `URP_Quest_VR.asset` currently contains `m_RequireDepthTexture: 1`.
- `QuestVulkanRenderPipelineConfigurator.ConfigureUrpAsset()` contains `SetBool(serialized, "m_RequireDepthTexture", false)`.
- Post-patch whitespace scan returned no hits.
- Post-patch `git diff --check` returned no whitespace errors for touched SHINOBU_236 docs/logs.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=863.
- Dotnet build not launched: latest guard was CPU 38.35 with a dotnet process active, and generated `.csproj` files still cannot prove this isolated asmdef.

---

## SHINOBU_236 Pass 32 Depth Build Guard Added

What was wrong:
- The depth dependency was only documented. A later platform repair/build route could set `m_RequireDepthTexture=false` and silently remove the input required by SHINOBU's Sobel/depth bilateral pass.

What was done:
- Added editor/build validation in `BilateralDrsRendererFeatureInstaller.VerifyRequiredFeatures`.
- The guard now checks `URP_Low`, `URP_Medium`, `URP_High`, and `URP_Quest_VR` serialized `m_RequireDepthTexture` via `SerializedObject`.
- The guard fails the player build with the exact URP asset path if a target disables camera depth texture.
- No URP assets, ProjectSettings, renderer YAML, or platform configurator source were mutated.

Cinematic Cheats used:
- No new fake. The existing Dear Lie remains Sobel depth edge mask plus bilinear flat-pixel bypass.

Exact Microseconds saved:
- 0 us runtime. This is build-time route protection.
- It prevents a silent Quest no-depth route from reaching runtime; actual Quest depth cost remains a platform budget decision.

Static proof:
- Current URP assets all serialize `m_RequireDepthTexture: 1`.
- Scoped `git diff --check` on the edited installer returned no errors.
- Scoped forbidden hot-path scan returned no hits.
- Renderer asset grep still returns no serialized `HectonBilateralDrsUpscalerFeature` until Unity import/installer execution.
- `.csproj` coverage grep still returns no BilateralDrs hits.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=863.
- Dotnet build not launched: latest guard was CPU 13.55 with a dotnet process active, and generated `.csproj` files still cannot prove this isolated asmdef.

---

## SHINOBU_236 Pass 33 Target-Scoped Build Guard Added

What was wrong:
- Subagent audit found the depth/renderer build guard validated every SHINOBU target asset for every player build.
- A Quest-specific depth or renderer serialization conflict could therefore block a standalone PC build that does not consume Quest renderer or Quest URP assets.

What was done:
- Added `VerifyRequiredFeatures(BuildTarget, out failure)`.
- `BilateralDrsRendererFeatureBuildGuard` now passes `report.summary.platform`.
- Standalone builds validate `PC_Renderer`, `PC_High_Renderer`, and Low/Medium/High URP depth assets.
- Android builds validate `Mobile_Renderer`, `Quest_VR_Renderer`, and Low/Quest URP depth assets.
- iOS builds validate `Mobile_Renderer` and Low URP depth.
- Manual no-target validation still scans all assets.

Cinematic Cheats used:
- No new fake. The existing Dear Lie remains Sobel depth edge mask plus bilinear flat-pixel bypass.

Exact Microseconds saved:
- 0 us runtime. This is build-time proof scoping.
- Developer iteration impact: prevents unrelated Quest validation from stopping standalone builds.

Static proof:
- Target predicates are present in `BilateralDrsRendererFeatureInstaller`.
- Scoped forbidden hot-path scan returned no hits.
- Direct trailing-whitespace scan returned no hits.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=863.
- Dotnet build not launched: latest guard was CPU 20.08 with a dotnet process active, and generated `.csproj` files still cannot prove this isolated asmdef.

---

## SHINOBU_236 Pass 34 Target-Scoped Installer Added

What was wrong:
- Build validation was target-scoped, but build preprocessing still called the no-target installer and could repair every SHINOBU renderer asset for every build target.
- That could mutate Quest renderer assets during a standalone build even though validation would no longer require them.

What was done:
- Added `InstallRequiredFeatures(BuildTarget)`.
- `OnPreprocessBuild` now passes `report.summary.platform` to both install and verify.
- Menu/manual no-target installation still repairs every SHINOBU renderer asset by explicit action.

Cinematic Cheats used:
- No new fake. Existing Sobel depth edge mask plus bilinear flat-pixel bypass remains the Dear Lie.

Exact Microseconds saved:
- 0 us runtime.
- Developer iteration impact: reduces avoidable renderer asset save/import churn for builds that do not consume Quest assets.

Static proof:
- Installer and verifier now share the same renderer target predicate.
- Scoped forbidden hot-path scan returned no hits.
- Direct trailing-whitespace scan returned no hits.
- `.csproj` coverage grep still returns no BilateralDrs hits.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=879.
- Dotnet build not launched: latest guard was CPU 17.22 with a dotnet process active.

---

## SHINOBU_236 Pass 35 Raster Fail-Closed Edge-Mask Clear Added

What was wrong:
- Compute-unsupported or compute-missing frames returned before publishing a new black `_H8BilateralDrsEdgeMask`.
- That left a theoretical stale debug/proof texture risk after a prior successful SHINOBU frame.

What was done:
- `AddRenderPasses` now enqueues clear-only mode when compute is unavailable or the compute asset is missing.
- `RecordRenderGraph` now falls back to a 1x1 raster RenderGraph clear when compute clear kernels cannot run.
- Raster clear format selection tries R8, then R16, then RGBA8 renderable formats.
- Normal compute-supported fail-close still uses `ClearEdgeMask`.
- No CPU blit, no `Shader.SetGlobal*`, no `SetData`, no unmanaged fallback route was added.

Cinematic Cheats used:
- No new physical simulation. The proof artifact is a 1x1 black mask, which is the cheapest honest visual fake for "no edge pixels need heavy bilateral."

Exact Microseconds saved:
- Active reconstruction path: 0 us claimed; unchanged.
- Failure path: one 1x1 raster clear replaces stale state. Runtime timing is PENDING Frame Debugger/profiler proof.

Static proof:
- Scoped forbidden hot-path scan returned no hits.
- Direct trailing-whitespace scan returned no hits.
- `git diff --check -- Assets/_Project/Scripts/Rendering/BilateralDrs/HectonBilateralDrsUpscalerFeature.cs`: no errors.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=853.
- Dotnet build not launched: latest guard was CPU 100; generated `.csproj` files still have no BilateralDrs coverage.

---

## SHINOBU_236 Pass 36 Active-Target Auto-Installer Scoped

What was wrong:
- Reload-time auto-install still scheduled the no-target installer.
- That could mutate every SHINOBU renderer asset on ordinary editor reload, even when the active work target was standalone PC.

What was done:
- Added `InstallRequiredFeaturesForActiveBuildTarget()`.
- `QueueInstallAfterReload` now schedules active-target installation.
- The menu command remains the deliberate all-target setup route through `BuildTarget.NoTarget`.
- Player build preprocessing still passes `BuildReport.summary.platform` for exact build-target install and verify.

Cinematic Cheats used:
- No new visual fake. Existing Sobel depth mask plus flat-pixel bilinear bypass remains the Dear Lie.

Exact Microseconds saved:
- 0 us runtime.
- Developer iteration impact: avoids unnecessary renderer asset save/import churn across unrelated target assets.

Static proof:
- `rg` over installer shows reload route uses `InstallRequiredFeaturesForActiveBuildTarget`.
- Scoped forbidden hot-path scan returned no hits.
- Direct trailing-whitespace scan returned no hits.
- `git diff --check` on edited SHINOBU source returned no errors.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=838.
- Dotnet build not launched: latest guard was CPU 100; generated `.csproj` files still have no BilateralDrs coverage.

---

## SHINOBU_236 Pass 37 Non-Finite DTO CBuffer Invalidation Added

What was wrong:
- A non-finite pending `UpscalerParamsDTO` stopped GPU upload but could leave the previous `s_publishedConstantBuffer` visible.
- If dimensions matched, RenderGraph could consume stale constants after a NaN/fault frame.

What was done:
- `PublishPendingParameters` now calls `InvalidatePublishedParameters()` when `CheckFaultsAndDump` rejects the active DTO.
- The existing black-box dump path remains unchanged.
- RenderGraph now fails closed through the cleared edge-mask path instead of importing stale constants after a non-finite DTO.

Cinematic Cheats used:
- No new visual fake. Existing fail-closed black edge-mask publication remains the proof artifact.

Exact Microseconds saved:
- Healthy path: 0 us claimed; unchanged.
- Fault path: static reference invalidation only, below measurement noise; it prevents stale GPU work after a numerical fault.

Static proof:
- Runtime grep shows `PublishPendingParameters` now invalidates published parameters after failed finite validation.
- Scoped forbidden hot-path scan returned no hits.
- Direct trailing-whitespace scan returned no hits.
- `git diff --check` on edited runtime source returned no errors.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=838.
- Dotnet build not launched: latest guard was CPU 100; generated `.csproj` files still have no BilateralDrs coverage.

---

## SHINOBU_236 Pass 38 CSV Profile Stale-Row Fail-Close Added

What was wrong:
- A failed `upscaler_quality_profiles.csv` load could leave previously parsed `UpscalerProfileDTO` rows active in the Vault profile lane.
- That made invalid or inaccessible authoring data silently preserve stale filter weights and quality bias.

What was done:
- `LoadQualityProfilesCsv` now clears the profile lane before reading/parsing.
- `_profilesSeeded` remains false until parsing returns at least one valid row.
- CSV path building now rejects null, rooted, and parent-traversal paths.
- File I/O failure returns zero rows instead of throwing through the editor/runtime cold facade.

Cinematic Cheats used:
- No new physical simulation. Invalid profile data now collapses to no profile override, leaving the existing continuous quality curve as the cheap baseline.

Exact Microseconds saved:
- 0 us hot runtime.
- Cold CSV path adds a bounded 32-row clear; no profiler claim until Unity import/playmode proof exists.

Static proof:
- Source patch is confined to `HectonBilateralDrsUpscalerRuntime.cs`.
- Scoped forbidden hot-path scan returned no hits.
- Direct sibling-runtime reference scan returned no hits.
- Direct trailing-whitespace scan returned no hits.
- Scoped `git diff --check` returned no errors.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`: PASS_WITH_WARNINGS, `framePathBlockers=0`, `rawRuntimeBlockers=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: duplicates=0, localCasts=838.
- Renderer assets still have no serialized Bilateral DRS feature until Unity import/installer execution.
- Generated `.csproj` files still have no BilateralDrs coverage.
- Dotnet build not launched: CPU guard reported 100.

---

## SHINOBU_236 Pass 39 CSV Whole-File Strict Parse Added

What was wrong:
- Pass 38 cleared stale CSV profile rows before parsing, but the parser could still publish partial data when a valid row appeared before a later malformed row.
- The parser stopped scanning once the fixed 32-row profile lane filled, so malformed or overflow rows after that point were invisible.

What was done:
- `ParseQualityProfiles` now scans the whole byte span instead of stopping at profile capacity.
- Non-header/non-comment malformed rows clear the Vault profile lane and return zero rows.
- Valid-row overflow beyond `UpscalerProfileDTO[32]` clears the lane and returns zero rows.
- Header, comment, and blank rows remain skippable; valid files still hydrate the fixed lane without managed string splitting.

Cinematic Cheats used:
- No new physical simulation. Invalid authoring data collapses to the base continuous quality curve instead of preserving stale profile overrides.

Exact Microseconds saved:
- 0 us hot runtime.
- Cold CSV validation scans at most the bounded scratch file and clears at most 32 profile rows on failure; no Unity profiler claim until import/playmode proof exists.

Static proof:
- Source patch is confined to `HectonBilateralDrsUpscalerRuntime.cs`.
- Sample `Assets/_Project/Data/upscaler_quality_profiles.csv` exists and matches the 8-column schema.
- Scoped forbidden hot-path scan returned no hits.
- Direct sibling-runtime reference scan over `Assets/_Project/Scripts/Rendering/BilateralDrs` returned no hits.
- Direct trailing-whitespace scan returned no hits.
- Scoped `git diff --check` returned no errors before this log append.
- `PolishMandateStaticAudit.py --fail-on-pack-one`: PASS_WITH_WARNINGS, `packOne=0`.
- `BufferIDSovereigntyAudit.py --fail-on-duplicates`: `duplicates=0`.
- Scoped `JobCompletionAudit.py --source-root Assets/_Project/Scripts/Rendering/BilateralDrs --fail-on-frame-path --fail-on-raw-runtime-complete`: zero findings.
- Broad `JobCompletionAudit.py` is externally blocked by missing `Assets/_Project/Scripts/Editor/ZeroGCComplianceScanner.cs`.
- Renderer assets still have no serialized Bilateral DRS feature until Unity import/installer execution.
- Generated `.csproj` files still have no BilateralDrs coverage.
- Dotnet build not launched: CPU guard reported 100.
