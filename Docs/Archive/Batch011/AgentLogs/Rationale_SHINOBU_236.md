# Rationale_SHINOBU_236

Status: POLISH PASS 39 CSV WHOLE-FILE FAIL-CLOSED STRICT PARSE - DOTNET BUILD NOT LAUNCHED: CPU 100 / NO CSPROJ COVERAGE

## Initial Boundary

Problem: DRS currently risks standard bilinear blur and temporal history artifacts.
Solution: Implement a presentation-only depth-aware compute reconstruction path in rendering domain, with continuous quality scalar and explicit 32-byte GPU DTO.
Rejected Alternatives: FSR/DLSS/TAA-heavy history is not universal across mobile VR and can smear under resolution jumps; default bilinear destroys silhouettes.
Scalability potential: Low uses bilinear/cross fallback on flat pixels; Middle uses Sobel-gated 3x3; High uses wider effective radius where quality scalar allows; Ultra spends saved cycles on sharper edge-preserving reconstruction, not gameplay truth.
Hardware Impact: Expected MX350/i3 gain is ALU avoidance on non-edge pixels; exact microseconds are PENDING RenderDoc/Profiler capture.

Problem: GPU parameter upload can become a hidden GC/stall path if implemented through managed globals or per-frame wrappers.
Solution: Use explicit unmanaged DTO, Persistent staging, double GraphicsBuffer constant buffers, and LockBufferForWrite/MemCpy upload.
Rejected Alternatives: Shader.SetGlobalFloat/Vector, ComputeBuffer.SetData, per-frame arrays.
Scalability potential: Same ownership model from weak devices to high-end; only scalar values and shader sampling radius change continuously.
Hardware Impact: Removes managed upload churn and reduces driver binding chatter; exact microseconds are PENDING profiler proof.

Problem: Task 12 asks for jitter compensation but the DTO contract is exactly 32 bytes and assigns FilterParams as DepthWeight, ColorWeight, Radius, QualityScalar.
Solution: Preserve the 32-byte contract; encode jitter compensation only if source code already exposes an approved field/route or derive it in shader from sampling coordinate conventions. If no safe route exists, document as constrained rather than expanding DTO.
Rejected Alternatives: Expanding DTO past 32 bytes or smuggling AUP/world position data into the shader.
Scalability potential: Stable layout across Quest/ARM64 and PC GPUs; no tier-specific structure drift.
Hardware Impact: Prevents ARM64 constant-buffer corruption; microseconds saved are not meaningful, correctness risk avoided.

## Loop 1 Decisions

Problem: The upscaler needs a DataVault route but `BufferID` had no SHINOBU_236 lanes.
Solution: Added `Shinobu236BilateralDrs*` IDs at 71050-71056 next to existing reconstruction IDs. This is a critical cross-domain interface edit; it creates one owner/one route/one proof artifact instead of a hidden allocation.
Rejected Alternatives: Reusing Uber Noir IDs would alias another owner; local static arrays would bypass GlobalDataVault and break black-box telemetry.
Scalability potential: Low/Middle/High/Ultra all share the same DTO lanes; only values change continuously.
Hardware Impact: Removes lookup ambiguity and heap fallback risk; expected runtime gain is ~1-3 us CPU avoided versus managed side channels on i3/MX350.

Problem: Task 12 requires jitter compensation but the mandated DTO is exactly 32 bytes and only has two float4 lanes.
Solution: Preserve the 32B contract and pack sub-pixel jitter into the fractional residual of `FilterParams.z`, while quantizing the bilateral radius to 1/16 pixel. Shader decode can recover radius plus jitter without adding a third lane.
Rejected Alternatives: Expanding the CBuffer to 48B, using `Shader.SetGlobalFloat`, or passing AUP/world coordinates to the GPU.
Scalability potential: Low uses near-radius/cross taps; Middle uses gated 3x3; High and Ultra spend wider effective radius at silhouettes.
Hardware Impact: Keeps ARM64 constant layout stable; jitter decode is a few ALU ops, cheaper than temporal history resolve and avoids history-buffer memory.

Problem: Compile verification is mandatory but CPU guard reported 100% load.
Solution: Deferred dotnet compile. The rule forbids launching dotnet over 50% CPU or while csc/dotnet is active; no csc/dotnet was active, CPU was the blocker.
Rejected Alternatives: Running build anyway would violate batch law and contend with other agents.
Scalability potential: No runtime effect.
Hardware Impact: Prevents build-time contention; no frame-time estimate.

## Loop 2-4 Decisions

Problem: Full bilateral filtering every output pixel would burn ALU on water/flat walls.
Solution: Added `SobelDepthMask` as the Dear Lie prepass. Edge pixels receive bilateral reconstruction; flat pixels return manual bilinear from low-res color.
Rejected Alternatives: 5x5 bilateral over the whole screen, TAA history accumulation, vendor upscalers that are not universal on mobile VR.
Scalability potential: Low collapses toward bilinear/cross taps; Middle keeps 3x3 edge taps; High widens silhouettes; Ultra enables near-5x5 gated taps.
Hardware Impact: Expected MX350/i3 GPU saving is ~600-1800 us at 1080p if edge density stays below 25%; exact profiler capture is blocked until build/playmode can run.

Problem: URP dynamic resolution can make `cameraTargetDescriptor` represent scaled size, not final display size.
Solution: RenderGraph feature uses active color as low-res source and camera pixel dimensions as high-res target, then writes a high-res output texture.
Rejected Alternatives: Trusting URP's implicit upscaler or `AddBlitPass` copy; both can collapse to bilinear softness.
Scalability potential: Same pass handles 0.4-1.0 scale; activation threshold avoids work when no DRS drop is present.
Hardware Impact: Adds one Sobel pass and one gated upscale pass only during resolution drop; no gameplay CPU cost.

Problem: CSV profile request specified NativeHashMap, but project ownership doctrine routes persistent runtime data through GlobalDataVault BufferIDs.
Solution: Implemented cold `ReadOnlySpan<byte>` parser into fixed `UpscalerProfileDTO` Vault lane. It behaves as a bounded hash-keyed table by FNV-1a `ProfileHash` without introducing a separate native container lifetime.
Rejected Alternatives: `string.Split`, managed dictionaries, or a NativeHashMap with unclear DataVault ownership/release semantics.
Scalability potential: Toaster/middle/high/ultra profiles are data-authored and feed the same continuous radius/weight math.
Hardware Impact: 0 us runtime after cold load; avoids managed CSV garbage and lifetime ambiguity.

Problem: Compile and shader validation are still required, but CPU guard remained above the allowed threshold.
Solution: Continued static/manual verification and left compile status explicitly deferred. No dotnet process was launched.
Rejected Alternatives: Violating CPU guard to force a build while 20+ agents are active.
Scalability potential: No runtime effect.
Hardware Impact: Avoids workstation contention; actual code compile proof remains pending. Latest samples were 100% by CIM and 73.07% by performance counter.

## Loop 5 Self-Audit

Problem: Final report cannot be chat-only and cannot claim unverified compilation.
Solution: Appended detailed report and `<SELF_AUDIT>` XML to `Docs/AgentLogs/LOG_SHINOBU_236.md`; `Status_SHINOBU_236.md` records compile deferred by CPU guard.
Rejected Alternatives: Fake compile pass, omitting XML, or burying evidence in chat.
Scalability potential: Audit captures low/middle/high/ultra route and fixed Vault ownership for future renderer asset wiring.
Hardware Impact: No runtime effect; evidence quality prevents later integration churn.

## Loop 6 Ultra-Think Polish

Problem: The runtime still had a read-looking `TryReadEditorTuning` path that could instantiate the runtime and allocate/acquire Vault buffers via `EnsureRuntimeInstance()`/`EnsureVaultState()`.
Solution: Converted `TryReadEditorTuning` into a pure cached read against the already-live singleton and resolved handle only. Editor mutation remains on `TrySetEditorTuning`, where allocation/acquire is explicit and cold.
Rejected Alternatives: Keeping a convenient lazy read path; it violates the Global Systems Doctrine because `TryRead*` must not mutate, allocate, or publish.
Scalability potential: Low/Middle/High/Ultra receive identical runtime behavior; only editor tooling ergonomics changed.
Hardware Impact: Removes accidental cold allocation from a read facade and prevents hidden scene/bootstrap work on weak CPUs; expected hot-frame gain is correctness, not measurable GPU time.

Problem: `RunParameterKernel()` polled `GlobalRegistry.ResolutionScaler` in the owner tick path and direct `job.Execute()` bypassed the Burst path.
Solution: Cached `IDataVault` and `IResolutionScalerService` once during cold initialization, added `IGlobalRegistryHotSwapListener` rebinding for `DataVault` and `ResolutionScalerService`, registered the parameter owner as an `IDispatcherSystem` in `PreSimulation`, registered a dedicated `VisualSync` bridge for the CBuffer upload, and replaced direct job calls with `job.Run()`. The parameter job also writes the DTO through `NativeArrayUnsafeUtility.GetUnsafePtr` + `UnsafeUtility.AsRef`.
Rejected Alternatives: Per-frame registry polling, interface array dispatch, direct `Execute()` calls, or introducing a dispatcher dependency into the rendering feature.
Scalability potential: Low tier avoids registry churn; middle/high/ultra use the same dependency cache while `GlobalQualityWeight` continues to scale radius/tap gates continuously.
Hardware Impact: Expected i3/MX350 CPU gain is small but real: removes hot service lookup and ensures Burst-compiled math for the owner kernel; exact microseconds remain PENDING profiler capture.

Problem: Task 18's old proof exposed only an R8 global edge mask; it did not produce the requested visible black/green inspection output.
Solution: Added `EdgeMaskDebugComposite` compute kernel and RenderGraph branch controlled by a cached debug flag. Disabled path is zero additional GPU work; enabled path writes a black/green fullscreen diagnostic output without CPU readback.
Rejected Alternatives: CPU AsyncGPUReadback for mask inspection, SceneView gizmos, or material debug blits.
Scalability potential: Low/Middle/High/Ultra all keep the debug pass disabled in runtime. Development builds can inspect Sobel thresholds without altering gameplay truth or save state.
Hardware Impact: Disabled cost is 0 us. Enabled debug pass is one R8 read and one color UAV write per pixel; acceptable only as tooling, not a shipping frame cost.

Problem: Presentation-only Burst jobs were using `FloatMode.Deterministic`, which is reserved for rollback/netcode truth domains under the current mandate.
Solution: Switched `GenerateMockDrsStateJob` and `CalculateUpscalerParamsJob` to `FloatMode.Fast` with `FloatPrecision.Standard`; the upscaler remains outside rollback/save/Merkle truth.
Rejected Alternatives: Deterministic math for a visual-only shader parameter path; it spends CPU without preserving any gameplay fact.
Scalability potential: Same DTO layout and authority route across tiers; math speed increases without changing save identity or network state.
Hardware Impact: Expected ARM64/i3 improvement is lower scalar math cost in the owner kernel; exact microseconds remain PENDING Unity/Burst profiler proof.

Problem: Compile verification is still required, but the machine reported 100% CPU load.
Solution: Did not launch dotnet. Static grep gates were run instead: no `Shader.SetGlobal*`, `Graphics.Blit`, `SetData`, `AddUnsafePass`, `.Complete()`, managed container allocation, `GlobalDataVault.TryGetLatestCreated`, direct `job.Execute()`, or hot `GlobalRegistry` polling remains in SHINOBU_236 files.
Rejected Alternatives: Violating the >50% CPU build guard to force a scoped compile.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local IO/CPU contention while other agents are active; compile proof remains PENDING.

## Loop 7 Compile-Wall and Route Polish

Problem: The Bilateral DRS runtime lived under the broad script tree and would inherit the root assembly blast radius instead of a rendering-domain boundary.
Solution: Added `Assets/_Project/Scripts/Rendering/BilateralDrs/Hecton8.Rendering.BilateralDrs.asmdef` with only Core/Core.Contracts/Core.Memory plus Unity Burst/Collections/Jobs/Mathematics/RenderPipeline references. The editor asmdef references this runtime assembly explicitly.
Rejected Alternatives: Leaving the runtime inside the root assembly, moving the logic into `Hecton8.Graphics.Scalability`, or referencing sibling gameplay/rendering domains directly.
Scalability potential: No visual change; the same low/middle/high/ultra shader curve remains intact while C# iteration cost is reduced.
Hardware Impact: Runtime frame cost unchanged. Developer hardware impact is lower compile blast radius for SHINOBU_236 edits; exact compile seconds are pending a legal build window.

Problem: Unity asset GUIDs for new runtime/editor/shader/profile files were missing, which would let Unity generate them differently across machines and merge passes.
Solution: Added stable `.meta` files for the BilateralDrs folder, runtime asmdef, runtime scripts, compute shader, CSV profile, editor tuner, and scanner.
Rejected Alternatives: Waiting for Unity import to mint GUIDs after the fact; that creates unstable references in a 20-agent worktree.
Scalability potential: No frame-time effect; asset identity stays deterministic across device tiers.
Hardware Impact: Prevents import churn and missing-script/missing-shader fallout; microsecond frame impact is 0.

Problem: Task 04's runtime validator checked layout constants but did not read actual field offsets.
Solution: Extended `UpscalerParamsLayoutValidator.Validate()` to check `Marshal.OffsetOf<UpscalerParamsDTO>` for `ResolutionParams` offset 0 and `FilterParams` offset 16; the editor tuner still audits with `UnsafeUtility.GetFieldOffset`.
Rejected Alternatives: Trusting `[FieldOffset]` comments or only checking `UnsafeUtility.SizeOf`.
Scalability potential: Same 32-byte payload across mobile, middle, high, and ultra devices.
Hardware Impact: Avoids ARM64 constant-buffer misread risk; runtime cost is cold validation only.

Problem: The authority path needed a compact proof artifact separate from the long architecture note.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_236_BILATERAL_DRS_ROUTE_CARD.md` and linked it from `BILATERAL_DRS_UPSCALER_SHINOBU_236.md`.
Rejected Alternatives: Relying on chat history or burying ownership in a long log.
Scalability potential: Documents that `GlobalQualityWeight` changes fidelity only, never DTO layout/save identity/authority route.
Hardware Impact: Documentation only; prevents integration churn and accidental sibling coupling.

Problem: `SetGlobalTextureAfterPass` showed up in static grep when searching for global texture publication.
Solution: Kept it deliberately because Task 18 asks for an edge-mask output, and existing RenderGraph features in the project use `SetGlobalTextureAfterPass` as a graph-declared publication bridge. It is not `Shader.SetGlobalFloat/Vector`, not `SetData`, and not a per-frame unmanaged route mutation.
Rejected Alternatives: CPU readback for debug overlay, SceneView gizmo mesh, or hiding the edge mask inside a private transient texture.
Scalability potential: Disabled debug path costs 0 us. Enabled path is editor/development inspection only and does not alter runtime fidelity tiers.
Hardware Impact: No shipping-frame cost when disabled; enabled debug pass is one R8 read plus one UAV color write per pixel.

## Loop 8 Render-Path Purity and Shader Gate Polish

Problem: `RecordRenderGraph` called `EnsureRuntimeInstance()`. In the common path `AddRenderPasses` had already created the owner, but a render-graph record path must not have a hidden GameObject allocation fallback.
Solution: Added pure cached `TryGetRuntimeInstance(out runtime)` and changed `RecordRenderGraph` to fail-close when the owner is absent. The only bootstrap call remains in `AddRenderPasses`, before the graph is recorded.
Rejected Alternatives: Keeping lazy allocation inside `RecordRenderGraph`, searching the scene, or moving the upscaler into a broad bootstrap file outside the SHINOBU_236 domain.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this only removes a lifecycle hazard. The same continuous filter curve remains active once the owner exists.
Hardware Impact: Prevents a one-frame cold GameObject/AddComponent allocation from appearing on the render path. Frame-time gain is mostly risk removal; microsecond proof remains PENDING profiler capture.

Problem: The compute shader used a literal `quality <= 0.015` bypass. It was not a hardware-tier switch, but it looked like a binary quality cliff and was easy to misread in future audits.
Solution: Replaced it with a continuous `smoothstep(0.015, 0.075, quality)` quality gate. Edge work now collapses smoothly toward bilinear at weak quality while preserving the Sobel Dear Lie branch for flat pixels.
Rejected Alternatives: Always running the 5x5 loop at quality 0, adding a low-end keyword variant, or branching on a named device tier.
Scalability potential: Low quality quickly resolves to bilinear on silhouettes; middle gradually admits cross/diagonal taps; high/ultra unlock wider gated taps without shader variants or tier switches.
Hardware Impact: Weak GPUs avoid the bilateral loop for most low-quality cases while avoiding a visible pop at the threshold. Expected MX350/i3 saving remains in the same ~600-1800 us envelope versus full-screen 5x5, exact capture pending.

Problem: HLSL `ClampPixel` mixed `uint2`, `int2`, and scalar literal arithmetic in one expression. It is probably legal, but cross-compiler behavior on mobile shader backends should not rely on implicit vector conversions.
Solution: Split it into `safeDims`, explicit `int2` max pixel, and one clamp. This is a compile-risk reduction with identical output.
Rejected Alternatives: Trusting implicit HLSL casts or replacing clamped `Load` calls with samplers, which would weaken exact pixel math.
Scalability potential: No visual tier change.
Hardware Impact: Same ALU count class; reduces shader import risk on Quest/mobile backends.

Problem: Compile verification is still required, but the CPU guard again reported 100% load.
Solution: Did not launch dotnet. Static gates were rerun and passed for hard quality thresholds, forbidden hot-path calls, managed container allocation patterns, TAA/history use, and unsafe global upload shortcuts in SHINOBU_236 files.
Rejected Alternatives: Violating the >50% CPU build guard while other agents are active.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local contention; compile proof remains PENDING.

## Loop 9 Subagent Audit Integration

Problem: The RenderGraph pass submitted current dimensions, then read a constant buffer produced by earlier owner phases. First DRS activation could therefore use stale `1x1` or previous-frame dimensions.
Solution: Added `TryPrepareRenderGraphConstants`, which sets the current low/full dimensions and jitter, runs the existing parameter kernel, uploads the active 32-byte CBuffer immediately, and returns the buffer imported by RenderGraph. The VisualSync phase still handles frames where no RenderGraph same-frame preparation occurs.
Rejected Alternatives: Accepting one-frame smear, widening the CBuffer, or reading texture sizes in shader from global state without CPU telemetry.
Scalability potential: Low/Middle/High/Ultra now get current dimensions on the exact frame DRS engages; quality still only changes presentation weights and tap gates.
Hardware Impact: Adds one owner scalar parameter evaluation and one 32-byte LockBuffer upload in the active DRS render pass; avoids first-frame upscale garbage and stale jitter. Exact microseconds remain PENDING profiler capture.

Problem: `AddRenderPasses` could still trigger `EnsureRuntimeInstance()` from render flow, and the runtime used `DontDestroyOnLoad`.
Solution: Moved owner creation to scene-local cold bootstrap via `RuntimeInitializeOnLoadMethod` plus `SceneManager.sceneLoaded`; `AddRenderPasses` now only uses pure `TryGetRuntimeInstance`. Removed `DontDestroyOnLoad`.
Rejected Alternatives: Render-path auto-spawn, scene search, or broad bootstrap file edits outside the rendering domain.
Scalability potential: No shader tier change; lifecycle becomes predictable across scene reloads without a persistent hidden root.
Hardware Impact: Removes render-pass enqueue allocation risk and avoids DDOL cleanup debt; cold scene bootstrap cost is one GameObject/AddComponent per scene.

Problem: Wrong or stripped compute shader kernels would hard-fail on `FindKernel`.
Solution: Added `HasKernel` validation for `SobelDepthMask`, `BilateralUpscale`, and `EdgeMaskDebugComposite` before resolving kernel IDs.
Rejected Alternatives: Trusting serialized shader assignment or letting a stripped kernel crash setup.
Scalability potential: No visual tier change.
Hardware Impact: Cold validation only; prevents runtime fault churn.

Problem: The compute shader assumed the depth texture matched output dimensions. URP DRS can scale depth with color, poisoning Sobel and bilateral weights if coordinates are used directly.
Solution: Added high-to-depth `MapPixel`, passed `lowDims/highDims/depthDims` into helpers, and removed `GetDimensions` from helper calls inside tap loops. Sobel gradients are now normalized by center eye depth.
Rejected Alternatives: Treating depth as always full-res, CPU readback validation, or adding temporal history to repair edge mistakes.
Scalability potential: Low retains bilinear/edge bypass, middle/high/ultra get correct depth confidence regardless of depth target size.
Hardware Impact: Fewer repeated resource dimension queries on edge pixels and fewer false edge triggers at far depth. Exact GPU microseconds remain PENDING RenderDoc capture.

Problem: Raw depth/color inputs can produce NaN/Inf through `LinearEyeDepth`, `sqrt`, `exp2`, or weight division.
Solution: Added shader-side finite guards for raw depth, linear eye depth, low color, Sobel edge, debug output, bilinear bypass, and final bilateral color writes.
Rejected Alternatives: Relying only on CPU CBuffer validation or allowing NaN to propagate into UAV textures.
Scalability potential: Same across all tiers; correctness guard does not alter authority or DTO layout.
Hardware Impact: Adds small ALU guards; prevents catastrophic post stack contamination.

Problem: Current shader is 2D-only while XR array textures require separate array kernels, and R8 UAV LoadStore support is not guaranteed on every mobile path.
Solution: RenderGraph fail-closes for texture arrays until explicit array kernels exist. Edge mask format resolves to `R8_UNorm` only if LoadStore is supported, otherwise `R16_SFloat`; output color also requires LoadStore support or falls back to `R16G16B16A16_SFloat`.
Rejected Alternatives: Pretending `Texture2D` handles XR slices, forcing R8 UAV on unsupported GPUs, or adding shader variants without compile proof.
Scalability potential: Flat fail-close protects unsupported paths while the supported mono path keeps continuous quality scaling.
Hardware Impact: Avoids undefined UAV writes on mobile/VR backends; exact support matrix pending Unity import/player validation.

Problem: Compile verification is still required, but the CPU guard reported 100% load again.
Solution: Did not launch dotnet. Static gates passed after the patch, including no `DontDestroyOnLoad`, no forbidden global upload shortcuts, no `job.Execute`, no `.Complete`, no `SetData`, no blits, no hot managed containers, and no `Pack=1`.
Rejected Alternatives: Violating the >50% CPU build guard.
Scalability potential: No runtime effect.
Hardware Impact: Prevents build contention; compile proof remains PENDING.

## Loop 10 Ledger and Log Ordering Repair

Problem: `Docs/AgentLogs/LOG_SHINOBU_236.md` contained pass 9 and pass 8 above pass 7/pass 6, violating the reporting rule that old evidence stays at the top and newer evidence is appended at the bottom.
Solution: Performed a mechanical block reorder of the SHINOBU_236 log: initial report, pass 6, pass 7, pass 8, pass 9. No technical claims were rewritten; only chronology was corrected.
Rejected Alternatives: Leaving the misordered log and adding a footnote. That would preserve a known evidence-chain defect and force the CTO/integrator to reconstruct chronology manually.
Scalability potential: No runtime visual tier change. Evidence quality improves because low/middle/high/ultra decisions can now be read in actual iteration order.
Hardware Impact: 0 runtime us. Prevents integration review churn; profiler proof remains pending.

Problem: `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` had no SHINOBU_236 row, so Vault lanes `71050-71056` existed in source without a global binary/payload ledger boundary.
Solution: Added the SHINOBU_236 Bilateral DRS Upscaler Vault Payload Boundary. The row names the owner, source assets, asmdef boundary, BufferIDs, DTO sizes, Data Monolith non-claim, rollback/save exclusion, proof artifacts, and verification caveat.
Rejected Alternatives: Relying on the route card alone. The ledger is the project-wide payload index; missing rows create ambiguity for future payload auditors and migration agents.
Scalability potential: Low collapses toward bilinear/cross-like work, middle keeps Sobel-gated reconstruction, high/ultra spend richer silhouette taps; the ledger states that GlobalQualityWeight changes values only, not DTO layout or authority route.
Hardware Impact: 0 runtime us. Prevents cross-domain payload aliasing and compile-wall confusion around `71050-71056`; exact runtime perf remains pending Unity/Frame Debugger/Profiler proof.

Problem: Static grep after pass 10 matched forbidden token strings even though the matches were not runtime violations.
Solution: Classified both matches as intentional false positives: the route-card text explicitly forbids `Pack=1`, and `Blit_Operation_Inquisition` contains the literal `Graphics.Blit` because it scans for that forbidden pattern.
Rejected Alternatives: Deleting the scanner pattern or watering down the route-card warning to make grep green. That would weaken enforcement.
Scalability potential: No runtime visual tier change.
Hardware Impact: 0 runtime us.

Problem: Compile verification is still required, but the CPU guard again reported 100% CPU with no active `dotnet` or `csc`.
Solution: Did not launch dotnet. `git diff --check` was run for the patched docs and reported no whitespace errors, only the existing LF/CRLF warning for the ledger.
Rejected Alternatives: Violating the >50% CPU build guard to chase compile proof during active multi-agent work.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local CPU/IO contention; compile proof remains PENDING.

## Loop 11 Interop Compile-Risk Patch

Problem: `BilateralDrsUpscalerContracts.cs` uses `StructLayout`, `FieldOffset`, and `Marshal.OffsetOf` but did not have a local `System.Runtime.InteropServices` import in the file header. Depending on project-wide/global usings would be a brittle compile assumption for an isolated asmdef.
Solution: Added `using System.Runtime.InteropServices;` directly to the SHINOBU_236 contracts file. This keeps layout attributes and offset validation self-contained inside `Hecton8.Rendering.BilateralDrs`.
Rejected Alternatives: Waiting for a dotnet build to prove the missing import, or relying on a global using from another assembly. Isolated asmdef boundaries should not depend on ambient imports.
Scalability potential: No runtime visual tier change. This is compile-wall hygiene that preserves the same low/middle/high/ultra CBuffer and shader behavior.
Hardware Impact: 0 runtime us. Prevents a compile failure that would block Unity import and profiler validation.

Problem: The pass 9 same-frame RenderGraph preparation path still calls the existing parameter kernel and uploads the CBuffer from `RecordRenderGraph`.
Solution: Inspected the path and left it intact for this pass. Task 06 explicitly requires `CalculateUpscalerParamsJob` in the PRE_SIMULATION route, and replacing the render-record safety update with a duplicated direct evaluator would require compile/profiler validation that the CPU guard currently forbids. The known cost/risk remains documented instead of hidden.
Rejected Alternatives: Duplicating the parameter math in C# without a legal compile window. That would risk divergence from the Burst job and create a more dangerous unverified patch.
Scalability potential: Current behavior remains continuous: same DTO layout, same `GlobalQualityWeight` curve, same Sobel Dear Lie.
Hardware Impact: Potential tiny job/render-record overhead remains PENDING profiler proof. The pass 11 code change itself costs 0 us.

Problem: Compile verification is still required, but the CPU guard again reported 100% CPU with no active `dotnet` or `csc`.
Solution: Did not launch dotnet. Static grep after the import patch found only intentional false positives: scanner literal `Graphics.Blit` and route-card `Pack=1` enforcement text. `git diff --check` reported no whitespace errors on patched files, only the ledger LF/CRLF warning.
Rejected Alternatives: Violating the build guard during multi-agent work.
Scalability potential: No runtime effect.
Hardware Impact: Prevents local CPU/IO contention; compile proof remains PENDING.

## Loop 12 Assembly/API Boundary Audit

Problem: A separated rendering asmdef can still fail compile if it misses unsafe permission, references a non-existent Core symbol, or uses a RenderGraph API shape that the local Unity 6000 codebase does not actually use.
Solution: Audited `Hecton8.Rendering.BilateralDrs.asmdef`, `Hecton8.Editor.asmdef`, Core contracts, DataVault contracts, Dispatcher contracts, and local RenderGraph compute examples. The runtime asmdef has `allowUnsafeCode=true`, is `autoReferenced=false`, and depends only on Core/Core.Contracts/Core.Memory plus Unity Burst/Collections/Jobs/Mathematics/URP/CoreRP. Editor assembly references it explicitly and allows unsafe layout inspection. Symbols verified in current source: `IResolutionScalerService`, `ResolutionScaleState`, `DrsStateDTO`, `IDataVault`, `VaultGenerationHandle<T>`, `IDispatcherSystem`, `ILateFrameTickable`, `IGlobalRegistryHotSwapListener`, `SystemID.GraphicsScalability`, and BufferIDs `71050-71056`. Existing local passes confirmed the same `AddComputePass`, `SetComputeTextureParam(TextureHandle)`, `ImportBuffer`, `UseBuffer`, and `SetComputeConstantBufferParam(GraphicsBuffer)` pattern.
Rejected Alternatives: Patching speculative API changes without compile output, or moving SHINOBU_236 into broad root/Core assemblies to hide missing references.
Scalability potential: No visual math changed; low/middle/high/ultra behavior remains governed by the continuous `GlobalQualityWeight` radius/tap gates.
Hardware Impact: Runtime frame cost unchanged. Compile-wall risk is reduced by keeping SHINOBU_236 in its isolated assembly and verifying no sibling runtime dependency slipped in; build proof remains PENDING until CPU guard allows compile.

Problem: Static gates still need a fresh run after the assembly/API audit.
Solution: Re-ran forbidden-pattern scan over SHINOBU_236 runtime/editor/shader/docs and `git diff --check` over touched files. Findings: only the intentional `Blit_Operation_Inquisition` scanner literal `Graphics.Blit` and route-card text naming forbidden `Pack=1`; no `DontDestroyOnLoad`, `TryGetLatestCreated`, `Shader.SetGlobalFloat/Vector`, `SetData`, `AddUnsafePass`, `.Complete`, direct `job.Execute`, hot managed Native containers, `System.Linq`, `UnityEngine.Random`, `Time.deltaTime`, `IsLowEnd`, or shader multi_compile was found in the SHINOBU_236 path. `git diff --check` reported no whitespace errors, only LF/CRLF warnings for existing touched files.
Rejected Alternatives: Treating the editor scanner literal or documentation warning sentence as runtime violations.
Scalability potential: Static-only; confirms no binary quality switch or variant split entered the code.
Hardware Impact: Prevents hidden GC/render-path regressions; exact frame microseconds remain PENDING Unity/Profiler proof.

Problem: Compile verification remains mandatory but the CPU guard reported `CPU=100.00` and `NO_DOTNET_OR_CSC`.
Solution: Did not launch dotnet. Recorded the blocked compile state instead of creating a false green report.
Rejected Alternatives: Running build over the explicit >50% CPU prohibition.
Scalability potential: No runtime effect.
Hardware Impact: Avoids local CPU/IO contention with other agents; compile proof remains PENDING.

## Loop 13 Unsigned Threadgroup Overload Patch

Problem: Subagent compile-risk audit flagged `CeilByThreadGroup` using `Mathf.Max(1u, threadGroupSize)`. Unity's `Mathf` overload set is float/int-oriented; relying on unsigned argument conversion is brittle inside an isolated rendering asmdef.
Solution: Replaced the unsigned `Mathf.Max` call with `uint safeThreadGroupSize = Math.Max(1u, threadGroupSize);` and kept the final integer clamp as `Math.Max(1, Mathf.CeilToInt(...))`. The math is identical and removes overload ambiguity.
Rejected Alternatives: Leaving the brittle overload until compile output, or converting the whole helper to floats and losing the explicit unsigned thread-group contract.
Scalability potential: No visual math changed. Low/middle/high/ultra still use the same dispatch geometry and continuous shader-side quality gates.
Hardware Impact: Runtime cost is unchanged. Compile/import risk is lower; no frame-time microseconds claimed.

Problem: The overload patch required fresh static gates.
Solution: Re-ran targeted grep for `Mathf.Max(1u`, forbidden-pattern scan over SHINOBU_236 runtime/editor/shader/docs, `git diff --check` over pass 13 touched files, and the CPU/dotnet guard. No `Mathf.Max(1u...)` remains. Forbidden scan still only reports the intentional scanner literal `Graphics.Blit` and route-card `Pack=1` enforcement text. `git diff --check` reported no whitespace errors on pass 13 touched files.
Rejected Alternatives: Treating the subagent warning as informational only without rerunning gates.
Scalability potential: No runtime visual tier change.
Hardware Impact: 0 runtime us; compile risk reduced. Compile remains PENDING because CPU guard reported `CPU=93.01` with no active `dotnet` or `csc`.

## Loop 14 Log Chronology Repair

Problem: After the pass 13 append, `LOG_SHINOBU_236.md` again violated top-old/bottom-new order: pass 13 sat above pass 12, pass 10, and pass 11. That corrupts the evidence chain even though runtime code was not affected.
Solution: Mechanically reordered the log sections and normalized separators into strict chronology: initial report, pass 6, pass 7, pass 8, pass 9, pass 10, pass 11, pass 12, pass 13. No runtime source was changed in this repair.
Rejected Alternatives: Leaving the out-of-order log with a note. That would force integrators to reconstruct iteration order manually and would violate the local reporting protocol.
Scalability potential: No visual math changed. Low/middle/high/ultra behavior remains the same continuous `GlobalQualityWeight` route with Sobel-gated bilateral reconstruction.
Hardware Impact: 0 runtime us. Review/integration risk reduced; profiler and compile proof remain pending.

Problem: The evidence repair still required fresh static gates.
Solution: Re-extracted the SHINOBU_236 prompt from `CURRENT_BATCH.md`, re-ran the targeted unsigned `Mathf.Max` grep, the forbidden-pattern scan, `git diff --check`, and the CPU/dotnet guard. No `Mathf.Max(1u...)` remains. Forbidden scan still only reports the intentional scanner literal `Graphics.Blit` and route-card `Pack=1` enforcement text. `git diff --check` reported no whitespace errors on checked files.
Rejected Alternatives: Treating documentation-only repair as proof-free. The log is part of the deliverable and must be validated.
Scalability potential: Static-only; confirms no binary quality switch or shader variant split entered during the repair.
Hardware Impact: 0 runtime us. Compile remains PENDING because CPU guard reported `CPU=100.00` with no active `dotnet` or `csc`.

## Loop 15 Renderer Install Bridge and Resource Fail-Close

Problem: Static renderer asset grep showed `PC_Renderer`, `PC_High_Renderer`, `Mobile_Renderer`, and `Quest_VR_Renderer` do not yet serialize `HectonBilateralDrsUpscalerFeature` or `Hecton_BilateralUpscale.compute`. A complete shader/feature implementation that is not wired into RendererData is inert.
Solution: Added editor-only `BilateralDrsRendererFeatureInstaller` in the SHINOBU_236 editor surface. It loads the four renderer assets, creates or reuses `HectonBilateralDrsUpscalerFeature` sub-assets, appends references through `SerializedObject`, rebuilds `m_RendererFeatureMap` from real local file IDs, binds the compute shader path, and leaves runtime code untouched.
Rejected Alternatives: Hand-editing YAML was rejected because `m_RendererFeatureMap` is Unity's hidden local-ID map and a stale map corrupts renderer feature ordering. Editing broad `HectonRenderPipelineValidator` was rejected because that file is a large shared editor authority and would widen compile-wall blast radius.
Scalability potential: Low/mobile/Quest paths can receive the same feature but still fail-close on unsupported XR array/MSAA resources; PC/high keep the same continuous `GlobalQualityWeight` shader behavior. The installer changes authoring reach, not DTO layout or gameplay truth.
Hardware Impact: 0 runtime us until Unity imports and serializes the feature. After import, only active DRS frames pay the existing Sobel-gated compute cost; inert renderer-asset risk is removed without a hand-edited YAML failure mode.

Problem: Subagent render audit identified a texture declaration mismatch risk. The compute shader declares `Texture2D` and `RWTexture2D`, but the C# path only rejected multi-slice inputs. Texture arrays or MSAA-backed targets could still be sent to a non-array, non-MSAA kernel.
Solution: Added `IsSupportedTextureInput(TextureDesc)` and fail-closed unless color and depth descriptors are `TextureDimension.Tex2D`, `slices == 1`, and `msaaSamples == MSAASamples.None`.
Rejected Alternatives: Auto-resolving MSAA or treating `slices == 1` as enough. That would require a resolve pass or array kernels, neither of which has profiler/Frame Debugger proof here.
Scalability potential: Weak devices avoid undefined compute dispatch on tile/MSAA/XR resources. High/ultra retain the same bilateral path when descriptors are supported.
Hardware Impact: Prevents invalid dispatch and GPU driver stalls; exact saved microseconds are not claimed without a failing capture.

Problem: Non-finite depth fallback used raw depth `1.0`. On reversed-Z platforms, that can behave like near-plane depth and create false foreground edges from bad depth samples.
Solution: `LoadRawDepth` now returns `0.0` for non-finite raw depth under `UNITY_REVERSED_Z`, and `1.0` otherwise, then saturates finite values.
Rejected Alternatives: Leaving the single fallback or converting all depth math to eye-depth before validation. The macro branch is shader-compile-time and preserves the current low-ALU path.
Scalability potential: All tiers keep the same tap logic; invalid depth no longer creates high-confidence edge masks on reversed-Z devices.
Hardware Impact: 0 measurable runtime cost; one compile-time macro branch in sanitizer. Prevents artifact-driven over-filtering on bad depth pixels.

Problem: After writing a full-size color texture, later URP passes can inspect `cameraTargetDescriptor` and still see the pre-upscale dimensions.
Solution: Updated `cameraTargetDescriptor.width/height` to the full output size after both normal and debug output routes.
Rejected Alternatives: Duplicating URP's `_ScreenSize` update with `AddUnsafePass`. URP's own helper uses an unsafe global-state pass, but SHINOBU_236 has no profiler/Frame Debugger proof justifying a new unsafe pass under current mandates.
Scalability potential: No shader quality change. Descriptor consistency improves for middle/high/ultra post chains while low-tier unsupported descriptors fail closed.
Hardware Impact: 0 runtime us beyond two field writes. `_ScreenSize` global repair remains a documented residual pending Unity proof.

Problem: Compile verification remains mandatory, but the CPU guard reported `CPU=100.00` and `NO_DOTNET_OR_CSC`.
Solution: Did not launch dotnet. Ran scoped forbidden scans, renderer asset grep, and trailing-whitespace scan. Renderer assets are still `NOT_YET_SERIALIZED` until Unity import executes the editor installer.
Rejected Alternatives: Violating the explicit >50% CPU build prohibition or claiming serialized renderer proof before Unity import.
Scalability potential: Static-only; confirms no binary quality switch or shader variant split entered during pass 15.
Hardware Impact: Avoids local CPU/IO contention; compile, shader import, renderer serialization, Frame Debugger, and profiler proof remain PENDING.

## Loop 16 RenderGraph Recording Purity

Problem: Subagent C# audit found a hard doctrine violation: `RecordRenderGraph` called `TryPrepareRenderGraphConstants`, which could initialize owner state, touch Vault, run `job.Run()`, and upload a GPU CBuffer while recording the render graph. Even if compile-valid, that is not a pure graph-recording read path.
Solution: Removed `TryPrepareRenderGraphConstants` and its internal same-frame path. Added `TryGetActiveConstantBufferForDimensions`, which only reads the owner-published static CBuffer snapshot and validates low/full dimensions against the active 32-byte DTO. `RecordRenderGraph` now imports that already-published buffer or fail-closes. `AddRenderPasses` submits current descriptor/camera dimensions and jitter for the next owner phase, but it does not resolve Vault, run jobs, or upload GPU buffers.
Rejected Alternatives: Keeping same-frame correctness by running owner work in graph recording was rejected because it violates route discipline. Duplicating scalar evaluator in RenderGraph was rejected because Task 06 requires the Burst parameter route and would create a second unverified math path. Adding an unsafe `_ScreenSize` pass was rejected until Frame Debugger/profiler proof exists.
Scalability potential: Low/middle/high/ultra shader behavior is unchanged. The visible tradeoff is one-frame fail-close on dimension changes until the owner publishes a matching CBuffer; that is preferable to hidden graph-recording mutation.
Hardware Impact: Removes a possible same-frame Vault/job/CBuffer upload from RenderGraph recording. Exact microseconds are not claimed without profiler proof; correctness risk and main-thread jitter risk are lower.

Problem: `Blit_Operation_Inquisition` embedded the previous full JSON report into the next JSON report. Repeated runs could grow the report and contaminate scanner evidence.
Solution: Replaced nested `previousReport` JSON with fixed metadata: prior report byte count and FNV-1a hash. The scanner still preserves continuity without recursive payload growth.
Rejected Alternatives: Deleting prior report evidence entirely, or continuing to nest full JSON for convenience.
Scalability potential: Editor/static only; no runtime tier behavior changes.
Hardware Impact: 0 runtime us. Editor report write stays bounded instead of growing with repeated runs.

Problem: `BilateralDrsTunerWindow` refreshed and string-formatted every editor update.
Solution: Throttled readout refresh to 0.125 seconds and only assigns label text when the value changes.
Rejected Alternatives: Leaving it as editor-only noise. Editor windows can still contribute to iteration stalls when multiple agents/tools are active.
Scalability potential: Editor-only; runtime low/middle/high/ultra behavior unchanged.
Hardware Impact: 0 runtime us. Editor UI churn reduced to roughly 8 Hz.

Problem: Compile verification remains mandatory, but CPU guard reported `CPU=100.00` and `NO_DOTNET_OR_CSC`.
Solution: Did not launch dotnet. Ran scoped forbidden-pattern scan, trailing-whitespace scan, and `git diff --check`; all pass except intentional scanner/doc false positives.
Rejected Alternatives: Violating the >50% CPU build prohibition.
Scalability potential: Static-only; confirms no binary quality switch or shader variant split entered during pass 16.
Hardware Impact: Avoids local CPU/IO contention; compile, shader import, Unity installer execution, Frame Debugger, and profiler proof remain PENDING.

## Loop 17 Compute Backend and Descriptor Fail-Close

Problem: The RenderGraph pass assumed compute shader availability once the asset reference existed. A backend reporting `SystemInfo.supportsComputeShaders == false` could still reach enqueue/record logic and fail at dispatch time or invite a forbidden blit fallback later.
Solution: Added an explicit compute-support fail-close in both `AddRenderPasses` and `RecordRenderGraph`. Unsupported backends do not enqueue the pass, do not record resources, and do not introduce a bilinear replacement route.
Rejected Alternatives: Adding a fragment blit fallback or attempting a CPU upscale. Both violate the assignment's edge-preserving compute route and would either smear silhouettes or burn CPU on presentation work.
Scalability potential: Weak unsupported devices degrade by omission instead of invalid dispatch. Supported low/middle/high/ultra devices retain the same continuous `GlobalQualityWeight` Sobel-gated bilateral behavior.
Hardware Impact: Prevents invalid compute dispatch and driver-side errors on unsupported backends. Runtime microseconds are not claimed without Unity/Frame Debugger proof.

Problem: Edge-mask and output RenderGraph UAV descriptors relied on constructor defaults for dimension, slice count, and VR usage even though the shader declares mono `Texture2D`/`RWTexture2D` resources.
Solution: Set both transient UAV descriptors to `TextureDimension.Tex2D`, `slices = 1`, and `VRTextureUsage.None` explicitly.
Rejected Alternatives: Relying on current defaults or silently supporting XR arrays without `Texture2DArray` kernels. That would leave a descriptor/shader contract gap.
Scalability potential: Low and mobile paths get a narrower valid descriptor contract; middle/high/ultra behavior is unchanged when inputs are supported.
Hardware Impact: 0 expected runtime cost. Reduces backend/import ambiguity; compile and shader import proof remain PENDING.

Problem: The pass 17 patch still required evidence without violating the build guard.
Solution: Re-extracted the SHINOBU_236 prompt, reran scoped forbidden-pattern scan, direct sibling-runtime reference scan, trailing-whitespace scan, `git diff --check`, and CPU/dotnet guard. The only forbidden-pattern hits remain intentional doc/scanner literals.
Rejected Alternatives: Running dotnet at 100% CPU or claiming compile proof from static source.
Scalability potential: Static-only. Confirms no binary quality switch or shader variant split entered during the patch.
Hardware Impact: Avoids local CPU/IO contention; compile, Unity import, shader import, renderer installer execution, Frame Debugger, GCMonitor, and profiler proof remain PENDING.

## Loop 18 Quality-Gated Sobel and Build Guard

Problem: `AddRenderPasses` received `cameraTargetDescriptor` dimensions that can be full display size even when URP dynamic scaling is active. Treating that descriptor as the low-res source could make the owner publish a full-size CBuffer and force `RecordRenderGraph` to reject a valid scaled source as stale.
Solution: Preserved `0` low-dimension sentinels unless the descriptor is smaller than the camera pixel dimensions or full-resolution test mode is explicitly forced. The owner phase now resolves the low dimensions from `IResolutionScalerService` or the Vault-backed mock state instead of inventing a low-res fact from an ambiguous descriptor.
Rejected Alternatives: Trusting the descriptor as low-res unconditionally, duplicating scalar DRS inference inside `RecordRenderGraph`, or widening the DTO to carry extra provenance bits.
Scalability potential: Low/middle/high/ultra still use one continuous DRS scale route. The sentinel only controls authority of dimension facts, not fidelity tier.
Hardware Impact: Prevents a one-frame fail-close or wrong-size dispatch under DRS transitions. Runtime cost is 0 us beyond two branch checks in `AddRenderPasses`.

Problem: The shader quality gate could collapse bilateral work to bilinear at very low `GlobalQualityWeight`, but C# still dispatched the full Sobel pass and paid 9 depth reads per output pixel before that collapse.
Solution: Added a C# Sobel skip at the zero-contribution edge of the same continuous quality curve, graph-cleared the R8 edge mask, and added a shader early bilinear return before reading `_H8EdgeMask` when `qualityGate <= 0.0001`.
Rejected Alternatives: Leaving Sobel bandwidth in place because the shader later bypasses, adding a low-end keyword, or branching on hardware class. This patch is scalar-driven by the existing quality curve.
Scalability potential: Low quality collapses to manual bilinear without Sobel bandwidth; middle gradually admits edge mask and cross/diagonal taps; high/ultra pay Sobel and wider gated bilateral only when the continuous scalar justifies it.
Hardware Impact: On weak GPUs, avoids approximately 9 depth texture loads and one R8 UAV write per output pixel at the quality floor, plus skips the bilateral loop. Exact microseconds remain PENDING Unity profiler/RenderDoc capture.

Problem: Unsupported XR/MSAA/array descriptors were rejected inside `RecordRenderGraph`, after the renderer feature could already enqueue itself.
Solution: Added the same fail-close descriptor predicate to `AddRenderPasses` so unsupported XR, non-2D, array, and MSAA resources are rejected before enqueue.
Rejected Alternatives: Letting unsupported resources reach graph recording, auto-resolving MSAA, or silently treating arrays as `Texture2D`. Dedicated array/MSAA kernels need separate proof.
Scalability potential: Weak/mobile/XR configurations fail closed rather than dispatching undefined kernels; supported mono DRS uses the same quality curve.
Hardware Impact: Avoids invalid compute dispatch and graph work for unsupported descriptors. Runtime gain is branch-only unless an unsupported target would have reached dispatch.

Problem: If `SystemInfo.supportsSetConstantBuffer` is false, the runtime could fail closed silently with no domain fault bit.
Solution: Added `FaultConstantBufferUnsupported`, requests one dump through the existing black-box path, and keeps the no-fallback stance.
Rejected Alternatives: Falling back to `Shader.SetGlobal*`, `ComputeBuffer.SetData`, or managed material properties. Those violate the upload route and increase driver/GC risk.
Scalability potential: All tiers use the same CBuffer route; unsupported backends omit the feature instead of changing truth ownership or DTO layout.
Hardware Impact: No hot cost on supported backends. Unsupported backend diagnostics improve without adding a slow fallback.

Problem: Renderer feature code existed, but generated `.csproj` files do not cover the isolated `Hecton8.Rendering.BilateralDrs` asmdef yet, and serialized renderer feature references can still be absent in player builds until Unity imports/runs the installer.
Solution: Added `BilateralDrsRendererFeatureBuildGuard` using `IPreprocessBuildWithReport`. It runs the installer and verifies feature sub-assets, renderer feature references, feature-map entries, compute shader binding, and injection point before a build proceeds.
Rejected Alternatives: Hand-editing renderer YAML, assuming Unity import will run, or treating source presence as renderer wiring proof.
Scalability potential: PC, high, mobile, and Quest renderer assets get the same feature wiring while runtime descriptor gates still fail closed where mono compute resources are unsupported.
Hardware Impact: 0 runtime us. Prevents shipping an inert DRS upscaler source path with no renderer feature binding.

Problem: Compile proof remains required, but initial guard reported 100% CPU with eight active `dotnet` processes; after that cleared, `rg` still found `NO_CSPROJ_COVERAGE_FOR_BILATERAL_DRS`.
Solution: Did not launch dotnet because the current generated project files do not include the isolated BilateralDrs asmdef. Ran forbidden-pattern scan, direct sibling-runtime reference scan, trailing-whitespace scan excluding `.meta`, `git diff --check`, CPU/process guard, and `.csproj` coverage check instead.
Rejected Alternatives: Starting another build under explicit guard violation, or later claiming a dotnet pass that cannot cover the new isolated asmdef.
Scalability potential: Static-only evidence; confirms no binary quality switches, shader variants, or sibling runtime dependencies were introduced in pass 18.
Hardware Impact: Avoids build-system contention. Compile, Unity import, shader import, renderer installer execution, Frame Debugger, GCMonitor, and profiler proof remain PENDING.

## Loop 19 DTO-Dimensioned Edge Mask Hardening

Problem: GPU audit found the compute shader declared `ResolutionParams` but still treated texture `GetDimensions()` as the reconstruction truth. Under RTHandle/dynamic-resolution behavior, physical texture dimensions can differ from logical DRS dimensions.
Solution: Added HLSL logical-dimension helpers that read `ResolutionParams.xy/zw`, clamp low-res reads to physical source dimensions, keep high-res mapping on the owner DTO, and sample depth/edge masks through explicit `MapPixel` conversions. C# `RecordRenderGraph` now resolves logical low/full dimensions from the active DTO before creating output/edge resources or accepting the CBuffer.
Rejected Alternatives: Trusting `TextureDesc`/`GetDimensions()` as the owner of DRS facts, widening the DTO, or querying global scaler state inside RenderGraph recording.
Scalability potential: Low/middle/high/ultra all use one DTO route; `GlobalQualityWeight` changes edge-mask resolution and filter work, not authority or layout.
Hardware Impact: Prevents wrong-size dispatch/sampling under RTHandle DRS. Frame-time gain is correctness first; exact GPU capture remains PENDING.

Problem: At quality `0.016`, pass 18 could jump from zero Sobel to full-resolution Sobel while shader contribution was still nearly zero.
Solution: C# now computes the same smoothstep quality gate and scales edge-mask dimensions continuously from 37.5% to 100% full resolution. The shader maps full-res output pixels to the reduced edge mask, so low quality sheds Sobel bandwidth without a binary hardware tier switch.
Rejected Alternatives: Full-res Sobel immediately above the gate, low-end keywords, or device-name branches.
Scalability potential: Low uses a 1x1 cleared mask or coarse Sobel; middle ramps edge-mask area; high/ultra pays full edge precision.
Hardware Impact: Weak GPUs avoid large portions of the 9-depth-read Sobel prepass near the quality floor. Exact MX350/Quest timing remains PENDING.

Problem: `_H8BilateralDrsEdgeMask` could remain globally visible from an older successful frame when DRS became inactive or the current CBuffer was stale.
Solution: Added `ClearEdgeMask` compute kernel and a graph-declared 1x1 clear/publish path for graph-valid fail-close/skip cases. Active zero-contribution Sobel also writes the 1x1 cleared mask before the upscale pass reads it.
Rejected Alternatives: `Shader.SetGlobalTexture`, CPU readback, or leaving global consumers to infer validity from stale texture dimensions.
Scalability potential: All tiers get the same proof artifact behavior; no save/rollback identity changes.
Hardware Impact: One 1x1 compute dispatch on fail/skip frames; avoids misleading debug/global state. Runtime cost is effectively negligible, exact proof pending Frame Debugger.

Problem: Non-finite DTO rows were dumped but still allowed to mark `_pendingGpuUpload`, making bad data eligible for CBuffer upload.
Solution: `CheckFaultsAndDump` now returns validity; `PublishPendingParameters` uploads only valid rows and keeps the last published CBuffer guarded by `s_hasPublishedParameters`.
Rejected Alternatives: Relying on shader-side finite guards after uploading invalid CBuffer data.
Scalability potential: Same across all quality levels; fault handling does not change presentation tier.
Hardware Impact: Prevents corrupted GPU constants from reaching the pass; no steady-state cost.

Problem: Runtime could reacquire/grow Vault buffers from `RunOwnerPreSimulation` if `_vaultStateReady` dropped after initialization.
Solution: Frame-phase owner now fail-closes when Vault state is not ready. Allocation/acquire remains in cold initialization, explicit editor mutation, CSV load, and registry hot-swap rebinding.
Rejected Alternatives: Silent `GetGenerationHandle` from a frame phase.
Scalability potential: Same shader curve; ownership timing is stricter on all devices.
Hardware Impact: Removes a possible main-thread allocation/growth spike during gameplay. Exact profiler proof remains PENDING.

Problem: Renderer installer could leave duplicate `HectonBilateralDrsUpscalerFeature` references in `m_RendererFeatures`, which would enqueue duplicate DRS passes.
Solution: Installer now normalizes renderer features to exactly one Bilateral DRS reference, rebuilds `m_RendererFeatureMap`, and build verification fails if the count is not one. It also verifies all four compute kernels including the new clear kernel.
Rejected Alternatives: Presence-only check or hand-editing renderer YAML.
Scalability potential: PC/high/mobile/Quest assets get one route after Unity import; descriptor gates still reject unsupported XR/MSAA resources.
Hardware Impact: Prevents duplicate Sobel/upscale dispatches. Renderer assets are still not serialized until Unity runs the installer.

Problem: HLSL portability audit found `isfinite` use and mixed typed `Load` coordinates; inner loop also used three `exp2` transcedentals per active tap.
Solution: Replaced HLSL finite checks with explicit `value == value && abs(value) <= FLT_MAX`, added explicit `LoadCoord(uint2)` construction, and replaced `exp2` with a bounded rational falloff.
Rejected Alternatives: Trusting D3D-only behavior or keeping transcendental-heavy weights in the MX350/mobile path.
Scalability potential: Low/middle gain cheaper active-edge taps; high/ultra still buy more quality through larger edge-mask area and tap gates.
Hardware Impact: Removes shader-import ambiguity and heavy transcendental ALU from the 5x5 active-edge loop. RenderDoc instruction/timing proof remains PENDING.

Problem: Compile and Unity import evidence are still required.
Solution: Re-ran prompt extraction, active mandate reads, forbidden-pattern scans, direct sibling-runtime reference scan, shader portability scan, trailing-whitespace scan, scoped `git diff --check`, renderer serialization grep, `.csproj` coverage check, and CPU/process guard. Did not launch dotnet: CPU was `100.00`, no `dotnet`/`csc`, and generated `.csproj` files still have no BilateralDrs coverage.
Rejected Alternatives: Claiming a dotnet result that cannot cover the isolated asmdef, or launching build during a 100% CPU guard violation.
Scalability potential: Static-only; no visual tier changes beyond the patched continuous edge-mask resolution.
Hardware Impact: Avoids local build contention. Compile, Unity import, shader import, renderer installer execution, Frame Debugger, GCMonitor, and profiler proof remain PENDING.

## Loop 20 Dispatcher-Scheduled Job Route Hardening

Problem: `DISPATCHER_OPTIMIZATION_REPORT.json` and subagent C# audit both identified SHINOBU_236 as owner-disputed because the DRS jobs were only evidenced through `IJob.Run()`. That kept Burst math synchronous in owner pre-simulation, outside the dispatcher dependency graph, and left no `JobHandle` for the central completion window.
Solution: Added `SimulationKernelBridge` and `PostSimulationPublishBridge`. `RunOwnerPreSimulation` now only advances presentation timing and fail-closes on missing Vault state. `ScheduleOwnerSimulation` resolves existing Vault handles, snapshots the scaler service, schedules `GenerateMockDrsStateJob` only when needed, then schedules `CalculateUpscalerParamsJob` on the returned handle. The combined handle is returned to `SystemDispatcher` and registered with `H8Memory`; `RunOwnerPostSimulation` publishes the active DTO only after the dispatcher post-simulation completion window.
Rejected Alternatives: Keeping `IJob.Run()` for tiny-job convenience was rejected because no profiler proof exists and the local dispatcher report already marks it as debt. Calling `.Complete()` locally was rejected as a direct Native Memory Jobs violation. Duplicating the scalar parameter evaluator in C# was rejected because Task 06 requires the Burst parameter kernel and would create a second math route.
Scalability potential: Low devices still get the same continuous quality and edge-mask collapse, but the CPU work now sits in the central Kahn-style dependency graph instead of blocking the owner phase. Middle/high/ultra keep the same GPU visual-overkill route; only job ownership changed.
Hardware Impact: Expected MX350/i3 gain is reduced main-thread serialization risk rather than a guaranteed lower arithmetic cost; exact microseconds remain PENDING profiler proof.

Problem: Subagent shader audit found stale global edge-mask exposure on early RenderGraph returns and a descriptor-format mismatch when output color falls back to `R16G16B16A16_SFloat`.
Solution: Added a clear-only RenderGraph mode so runtime-absent and unsupported descriptor paths enqueue a graph-declared 1x1 edge-mask clear instead of leaving `_H8BilateralDrsEdgeMask` stale. Record-time fail paths now attempt the same clear publication after compute/clear-kernel availability is known. Successful debug and normal output paths update `cameraTargetDescriptor.graphicsFormat` along with width/height.
Rejected Alternatives: `Shader.SetGlobalTexture` and CPU readback were rejected because they bypass RenderGraph and add global state mutation outside the declared pass. Leaving stale globals as "debug-only" was rejected because Task 18 explicitly exposes the mask as a proof artifact.
Scalability potential: Low/middle/high/ultra visual math is unchanged. Fail-close frames now publish an explicit black mask proof artifact without changing DTO layout, save identity, or gameplay truth.
Hardware Impact: One 1x1 compute dispatch on graph-valid fail paths; effectively negligible, exact Frame Debugger proof pending. Descriptor format consistency prevents downstream render-path ambiguity.

Problem: The cold CSV reader used `stackalloc byte[512]`, above the 256-byte stackalloc mandate.
Solution: Reduced the stack scratch block to 256 bytes while retaining the same bounded copy into the Vault-owned CSV scratch lane.
Rejected Alternatives: Keeping 512 because the path is cold/editor-facing was rejected; the mandate is cheaper to satisfy directly. Allocating a managed byte array was rejected as unnecessary.
Scalability potential: No visual tier change; profile loading remains cold.
Hardware Impact: 0 runtime frame cost; lower stack footprint in editor/profile ingestion.

Problem: The route card, architecture note, and CTO-facing log still described the older PreSimulation compute route after the source moved jobs into dispatcher Simulation/PostSimulation.
Solution: Updated the route card and architecture note to state PreSimulation intent capture, Simulation job scheduling, PostSimulation DTO publication, VisualSync CBuffer upload, clear-only edge-mask fail paths, and graphics-format descriptor publication. Appended pass 20 evidence and `<SELF_AUDIT>` to `LOG_SHINOBU_236.md`.
Rejected Alternatives: Leaving stale docs for later integrators or relying on chat output. The project protocol requires file-backed evidence.
Scalability potential: No visual change; it prevents future agents from reintroducing synchronous owner jobs or stale global mask behavior.
Hardware Impact: 0 runtime frame cost; integration risk reduced.

## Loop 21 Evidence Hygiene

Problem: The domain-local trailing-whitespace scan found Unity `.meta` empty-value fields with trailing spaces, and broad shader scans can accidentally mix SHINOBU_236 compute evidence with the older Visor `Hecton_BilateralUpsample.shader` from another domain.
Solution: Removed the trailing spaces from SHINOBU_236 BilateralDrs `.meta` files and narrowed final shader portability evidence to `Hecton_BilateralUpscale.compute`, the compute asset owned by this route.
Rejected Alternatives: Editing the older Visor shader would cross domain boundaries; claiming its `isfinite` hits as SHINOBU_236 failures would be false routing.
Scalability potential: No runtime visual change; evidence now cleanly separates this compute route from an older Visor material route.
Hardware Impact: 0 runtime frame cost; reduces integration audit noise.

## Loop 22 CSV Strict-Schema Hardening

Problem: `TryParseProfileRow` accepted malformed 7-column rows by publishing a default `qualityBias`, and accepted 9+ column rows by parsing all extra numeric tokens then ignoring them. That makes authored profile mistakes silently alter the continuous quality curve.
Solution: Added an explicit 8-column schema constant, fail-closed on extra tokens before parsing, required `tokenIndex == 8`, and stripped an optional UTF-8 BOM after ASCII trim so first-row headers/profiles parse deterministically.
Rejected Alternatives: Keeping permissive CSV shape because the path is cold was rejected; Task 17 is a human tuning bridge, and permissive malformed input creates invisible visual-scaling defects. Managed CSV libraries or `string.Split` remain rejected.
Scalability potential: Low/Middle/High/Ultra profiles still feed the same continuous `GlobalQualityWeight` math. This patch only prevents missing/extra authoring columns from mutating quality bias or radius envelopes by accident.
Hardware Impact: 0 runtime frame cost. Cold CSV ingest adds two integer comparisons and one optional BOM branch per token/first-token trim; expected impact is below measurement noise and avoids later frame-time/visual debugging churn.

## Loop 23 Dispatcher Fail-Closed Route

Problem: Subagent audit found the dispatcher route was non-atomic. PreSimulation, Simulation, PostSimulation, and VisualSync could register independently, creating phase splits where dimensions advance without scheduled jobs or scheduled jobs never publish.
Solution: Replaced independent registration calls with `RegisterDispatcherRouteAllOrFail`. The runtime now creates cold bridge objects, registers every dispatcher phase as one route, rolls back partial registrations on any failure, and only uses the fallback `IUpdatable` lane when the full dispatcher route is absent.
Rejected Alternatives: Keeping PreSimulation/VisualSync fallbacks for partial route cases was rejected because that creates a second execution route. Registering only the parameter job bridge was rejected because it would bypass the required PostSimulation publication and VisualSync upload phases.
Scalability potential: Low/Middle/High/Ultra keep the same continuous DRS math; this patch only hardens phase ownership so visual fidelity does not depend on a partially registered dispatcher graph.
Hardware Impact: Runtime steady-state cost is unchanged. Failure cases now avoid invalid work and stale graph publication; exact microseconds are not claimed without Unity profiler proof.

Problem: Vault resolve failures could leave a previously published CBuffer alive in static state, and RenderGraph could keep consuming it if dimensions happened to match.
Solution: Added `InvalidatePublishedParameters` and route fail-close calls for Simulation resolve failure, PostSimulation publication resolve failure, and VisualSync upload/constant-buffer failure. The invalidation clears `s_hasPublishedParameters`, the published frame index, and the static buffer pointer.
Rejected Alternatives: Relying on dimension mismatch was rejected because same-dimension stale constants are possible. Falling back to global shader setters remains rejected by the upload route.
Scalability potential: All quality levels fail closed to the 1x1 cleared edge mask instead of consuming stale visual state.
Hardware Impact: Failure-path only; prevents stale GPU constants and debugging churn. Hot success path adds no new work.

Problem: Clear-only RenderGraph publication still depended on Sobel/upscale/debug kernels, so a compute asset with `ClearEdgeMask` only could not invalidate stale edge-mask state.
Solution: Split kernel resolution into `TryResolveClearKernel` and `TryResolveActiveKernels`. Clear-only setup requires only `ClearEdgeMask`; active reconstruction still requires Sobel, upscale, and debug kernels.
Rejected Alternatives: Requiring every active kernel for stale-mask clearing was rejected because it weakens the fail-close proof artifact. A CPU/global texture fallback remains rejected.
Scalability potential: Low/fail-close frames can publish a black mask without activating the full upscaler path.
Hardware Impact: One 1x1 compute pass remains the clear-only cost; no added cost on active reconstruction frames.

Problem: Safety comments on disabled NativeArray restrictions were too broad for the current proof standard.
Solution: Expanded the comments to name owner lanes, no-overlap proof, dispatcher JobHandle ownership, and rejected alternatives for mock state, parameters, telemetry, and telemetry cursor.
Rejected Alternatives: Leaving generic comments was rejected because future audits need per-buffer alias/lifetime evidence.
Scalability potential: Documentation/proof only; no runtime visual change.
Hardware Impact: 0 runtime cost.

## Loop 24 Post-Patch Static Verification

Problem: Pass 23 changed route registration and fail-close semantics, so the evidence trail needed a fresh post-patch scan instead of relying on pre-cleanup output.
Solution: Re-ran forbidden hot-path scans over the SHINOBU_236 source set, whitespace checks, `git diff --check`, sibling-runtime reference checks, renderer asset grep, `.csproj` coverage grep, compute shader portability scan, dispatcher API read, H8Memory `RegisterActiveJob` API read, editor installer review, and a fresh SHINOBU_236 prompt extraction from `CURRENT_BATCH.md`.
Rejected Alternatives: Claiming compile/runtime proof from static checks was rejected. Launching dotnet was rejected because the latest guard reported 77% CPU with seven active `dotnet` processes and the generated project files still do not include the isolated BilateralDrs asmdef.
Scalability potential: Static verification only; no visual-tier math changed. It confirms the current source still routes all fidelity through continuous `GlobalQualityWeight` and does not add a hardware-tier branch.
Hardware Impact: 0 runtime frame cost. Developer hardware impact is preserved by not launching a non-proving build under CPU saturation; runtime/profiler proof remains pending Unity import and project regeneration.

Problem: Historical rationale/log entries still mention `IJob.Run`, `TryPrepareRenderGraphConstants`, and same-frame RenderGraph upload from earlier passes.
Solution: Treat those as chronological history only. Current architecture evidence is pass 20+ dispatcher Simulation/PostSimulation scheduling and pass 16+ pure RenderGraph published-buffer read; current source grep confirms no `IJob.Run` or `TryPrepareRenderGraphConstants` remains.
Rejected Alternatives: Rewriting historical audit sections was rejected because it would damage chronological forensic evidence.
Scalability potential: No visual change; prevents integrators from mistaking old audit text for current route doctrine.
Hardware Impact: 0 runtime cost.

## Loop 25 Quality-Gate Epsilon Cliff Removal

Problem: The shader and RenderGraph pass still used `qualityGate <= 0.0001` to skip Sobel/bilateral work. That epsilon is not a hardware-tier switch, but it is still an arbitrary near-zero cliff on the continuous quality curve.
Solution: Changed both checks to exact `qualityGate == 0`. The zero-work collapse now happens only when `smoothstep(0.015, 0.075, GlobalQualityWeight-derived quality)` mathematically returns zero; any value above the curve floor enters the continuous edge-mask sizing and tap-gating path.
Rejected Alternatives: Removing the branch entirely was rejected because quality zero would still pay loop/edge-mask overhead for no visual contribution. Keeping the epsilon was rejected because it widened the zero-work zone beyond the actual smoothstep endpoint.
Scalability potential: Low still collapses to bilinear when the curve is exactly zero; middle/high/ultra ramp continuously with no arbitrary near-zero cutoff.
Hardware Impact: Hot path changes one comparison constant only. Runtime timing difference is expected below measurement noise; visual/architecture impact is removal of a small discontinuity.

## Loop 26 XR Array Route and Dead Fallback Removal

Problem: The render path previously rejected XR texture arrays, which meant stereoscopic VR could not use the Bilateral DRS route. The same polish pass also found split SRV/UAV edge-mask binding risk and a dead fallback route that registered update interfaces but never executed the full dispatcher job/publication/upload chain.
Solution: Added `ClearEdgeMaskArray`, `SobelDepthMaskArray`, `BilateralUpscaleArray`, and `EdgeMaskDebugCompositeArray`; RenderGraph now resolves mono versus `Texture2DArray` mode from texture descriptors, binds `_H8EdgeMask*Read` for SRV reads and `_H8EdgeMask*Write` for UAV writes, and dispatches the array kernels across the eye slice dimension. Removed `IUpdatable` and `ILateFrameTickable` from the runtime so dispatcher failure is a fail-closed state, not a second execution route. Added `FaultVaultUnavailable` dump signaling for Vault/CBuffer resolve failure.
Rejected Alternatives: Keeping XR rejected was rejected because the mission target includes Quest-class stereoscopic VR. Binding the same edge-mask name as both `Texture2D` and `RWTexture2D` was rejected because it is fragile across Unity shader import/backends. Keeping the update fallback was rejected because it advanced only part of the pipeline and violated one owner/one route doctrine. Adding a CPU blit fallback was rejected.
Scalability potential: Low quality still collapses to exact-zero bilinear/cleared-mask work; middle ramps reduced edge-mask area; high/ultra use the same array-capable Sobel/upscale/debug kernels with full slice dispatch. `GlobalQualityWeight` remains a continuous scalar and does not change DTO layout, save identity, or authority.
Hardware Impact: Quest/VR now has a legal texture-array path instead of a hard reject. Split SRV/UAV binding reduces shader import risk. Removing the fallback has no hot success-path cost and prevents invalid partial-frame work. Exact GPU/CPU microseconds remain PENDING Unity import, shader import, Frame Debugger, and profiler proof.

## Loop 27 XR Descriptor Reality Check

Problem: Unity Core `TextureDesc.InitDefaultValues` uses the `xrReady` constructor argument to initialize XR dimensions and slices. SHINOBU_236 manually overwrote dimension/slices afterward, but keeping `xrReady:false` on array outputs diverged from the existing Visor XR RenderGraph pattern and could leave backend-specific XR descriptor metadata ambiguous.
Solution: Changed array output and edge-mask descriptor creation to pass `xrReady: useTextureArray`; added `outputVrUsage = useTextureArray ? sourceDesc.vrUsage : VRTextureUsage.None`; threaded that `vrUsage` through `CreateEdgeMaskDesc`. Mono clear-only stale-mask publication remains explicit `VRTextureUsage.None`.
Rejected Alternatives: Forcing `VRTextureUsage.TwoEyes` was rejected because the source texture already carries the authoritative XR usage. Leaving `xrReady:false` was rejected as avoidable API risk. Copying the whole source descriptor was rejected because the upscaler owns exact output size, UAV state, and dynamic-scale flags.
Scalability potential: Low/middle/high/ultra quality math is unchanged. The patch only makes the same continuous DRS reconstruction route legal for mono and XR array descriptors without creating a hardware-tier branch.
Hardware Impact: No hot-path arithmetic change. It reduces Unity import/backend descriptor risk on Quest/VR. Exact runtime timing remains PENDING Unity shader/import and Frame Debugger proof.

## Loop 28 Raw XR Slice Validation

Problem: Subagent audit found `TryResolveTextureMode` accepted invalid XR array descriptors by coercing raw `sourceDesc.slices` and `depthDesc.slices` through `Math.Max(1, ...)`. That could turn a bad zero-slice descriptor into a legal one-slice array route.
Solution: Read raw slice counts first, reject `sourceSlices <= 0 || depthSlices <= 0`, then compare equality and enforce `sourceSlices <= 2`. No descriptor is normalized into validity.
Rejected Alternatives: Leaving coercion in place was rejected because invalid RenderGraph descriptors should fail closed. Expanding support beyond two slices was rejected because this route is stereo VR only, not cubemaps or arbitrary texture arrays.
Scalability potential: Low/middle/high/ultra quality behavior is unchanged; this only hardens the eligibility gate before the same continuous DRS math runs.
Hardware Impact: Hot path adds two integer comparisons only on RenderGraph setup. Cost is below measurement noise; risk reduction is avoiding invalid XR array dispatch/import behavior.

## Loop 29 Array Capability Fail-Closed Gate

Problem: The XR array route validated dimensions, slices, MSAA, and UAV formats, but did not explicitly reject texture-array dispatch when the active backend reports `SystemInfo.supports2DArrayTextures == false`. Existing Crest runtime code treats that capability as required for array-backed water rendering, so SHINOBU_236 should not rely on descriptor shape alone.
Solution: Added the same capability gate in two places: enqueue-time `IsUnsupportedRenderTargetDescriptor` and RenderGraph-time `TryResolveTextureMode`. Mono `Tex2D` remains legal; `Tex2DArray` now requires positive equal slices, max two slices, non-MSAA, and 2D-array texture support. Updated SHINOBU_236 architecture docs to match source and corrected stale clear-only kernel wording.
Rejected Alternatives: A graphics-device-type blacklist was rejected because Android is already pinned to Vulkan in `ProjectSettings.asset`, and API-name branching would create a broader backend policy outside SHINOBU_236. A CPU/blit fallback was rejected because this domain owns an explicit compute upscaler or a graph-declared cleared-mask fail-close.
Scalability potential: Low/middle/high/ultra quality math is unchanged. The patch is a capability gate before the continuous `GlobalQualityWeight` curve runs; it cannot alter DTO layout, save identity, rollback state, or authority route.
Hardware Impact: Hot path adds one boolean capability read during pass eligibility only. Expected frame-time impact is below measurement noise. Risk reduction is avoiding invalid `Texture2DArray` compute dispatch on unsupported backends while preserving the Quest Vulkan route.

## Loop 30 XR Provider and Renderer Integration Blockers

Problem: Static audit found two integration blockers outside the successful SHINOBU_236 source route. `ProjectSettings/ProjectSettings.asset` still has `m_BuildTargetVRSettings: []`, no serialized XR Management/OpenXR settings assets were present on disk, and the renderer YAML assets still do not serialize a `HectonBilateralDrsUpscalerFeature` sub-asset.
Solution: Routed the XR provider issue to the existing platform-owner repair path (`PlatformPortabilityRouteRepairer.WireAndroidQuestXrRoutesForCi()` and `XrPlatformReadinessValidator.WireAndroidOpenXrProviderRouteForCi()`) instead of hand-editing platform settings from the rendering domain. Kept renderer serialization authority with the SHINOBU-owned Unity importer/build guard (`BilateralDrsRendererFeatureInstaller` with `InitializeOnLoadMethod` plus `IPreprocessBuildWithReport`) and documented that static assets remain inert until Unity import/installer execution.
Rejected Alternatives: Hand-editing `ProjectSettings` or renderer YAML was rejected because Unity's serialized XR and URP renderer feature maps are importer-owned, not a safe text-patch surface. Claiming XR readiness from package presence alone was rejected; `Packages/manifest.json` proves only package availability, not an active provider route. Running dotnet was rejected because generated project files still do not cover the isolated BilateralDrs asmdef and Unity is active.
Scalability potential: No visual math changed. Low/middle/high/ultra all keep the same continuous `GlobalQualityWeight` route; missing XR provider or missing renderer serialization causes fail-closed/no-route behavior, not a hidden binary quality branch.
Hardware Impact: 0 runtime frame cost. The value is proof hygiene: Quest/OpenXR and renderer-asset readiness are now explicit blockers instead of false runtime claims.

## Loop 31 Quest Depth Route Conflict

Problem: SHINOBU_236's algorithm is explicitly depth-driven: `RecordRenderGraph` requires `resourceData.cameraDepthTexture` and the compute path uses Sobel depth edges before depth-weighted bilateral reconstruction. Static `URP_Quest_VR.asset` currently serializes `m_RequireDepthTexture: 1`, but `QuestVulkanRenderPipelineConfigurator.ConfigureUrpAsset()` writes `m_RequireDepthTexture=false` when the platform repair/build route executes.
Solution: Documented the conflict as an external platform/rendering integration decision instead of silently changing SHINOBU math or platform assets. The valid choices are: preserve Quest depth when Bilateral DRS is required, or deliberately accept the SHINOBU fail-closed clear-mask/no-route behavior on depthless Quest frames.
Rejected Alternatives: A luma-only fallback was rejected for this pass because Task 07/08 require high-resolution depth and depth-edge Sobel proof; adding a second color-only route would be a new algorithm requiring shader import, visual QA, and profiler proof. Editing `QuestVulkanRenderPipelineConfigurator` was rejected because it is platform-owner code and could re-open Quest fill-rate/depth resolve costs without that owner's budget decision.
Scalability potential: No visual math changed. Low/middle/high/ultra still use the same continuous quality scalar when depth exists; depthless Quest remains a fail-closed integration state rather than a hidden binary hardware branch.
Hardware Impact: 0 runtime frame cost from this documentation pass. If platform owners preserve depth, Quest pays the existing depth texture/resolve cost but enables SHINOBU silhouette reconstruction. If they disable depth, SHINOBU saves its Sobel/upscale dispatch by failing closed, but visual upscaling falls back to whatever route owns the depthless frame. Dotnet was not launched because generated `.csproj` files still do not cover BilateralDrs and the latest guard found an active dotnet process.

## Loop 32 Depth Build Guard

Problem: The Quest depth conflict was documented but not enforced by SHINOBU-owned validation. A platform repair pass could disable `m_RequireDepthTexture` after static documentation, leaving the player build with a renderer feature that requires `cameraDepthTexture` but a URP asset that refuses to produce it.
Solution: Added `BilateralDrsRendererFeatureInstaller.VerifyPipelineDepthTexture`. The existing build guard now verifies `URP_Low`, `URP_Medium`, `URP_High`, and `URP_Quest_VR` serialized `m_RequireDepthTexture` through `SerializedObject` before player build proceeds. Failure is explicit and points at the offending URP asset.
Rejected Alternatives: Mutating URP assets from SHINOBU was rejected because AGENTS forbids changing URP/project settings from a domain pass. Editing `QuestVulkanRenderPipelineConfigurator` was rejected again because platform owners must choose whether Quest pays the depth route cost. A luma-only fallback remains rejected because Tasks 07 and 08 require depth and Sobel edge proof.
Scalability potential: Low/middle/high/ultra visual math is unchanged. This guard does not create a hardware-tier branch; it protects the one valid route so the continuous `GlobalQualityWeight` curve only runs when its required depth input exists.
Hardware Impact: 0 runtime frame cost. Build-time validation prevents a silent depthless Quest route. If depth remains enabled, Quest pays the known depth texture cost and gains SHINOBU silhouette reconstruction; if platform disables depth, the build now stops rather than shipping a hidden no-route state.

## Loop 33 Target-Scoped Build Guard

Problem: Subagent audit found the new validation was too global. A Quest-specific depth or renderer serialization conflict could block a standalone PC build even though that build does not consume the Quest URP or renderer assets.
Solution: Added `VerifyRequiredFeatures(BuildTarget, out failure)` and target-scope predicates for URP and renderer assets. Build preprocessing now passes `report.summary.platform`; standalone builds validate PC renderer assets and Low/Medium/High URP depth assets, Android validates Mobile and Quest assets, iOS validates Mobile/Low, and the manual no-target overload still validates every asset.
Rejected Alternatives: Keeping global build validation was rejected because it creates false blockers across unrelated target routes. Dropping Quest validation for Android was rejected because the current Android route is also the Quest/mobile XR route in this project and must keep the depth input explicit.
Scalability potential: No runtime visual math changed. Target scoping affects only proof selection; the same continuous `GlobalQualityWeight` curve and fail-closed behavior remain active for each built route.
Hardware Impact: 0 runtime frame cost. Developer hardware impact is fewer false build stops for PC iteration while Android/Quest still gets strict depth-route enforcement.

## Loop 34 Target-Scoped Installer

Problem: Validation was target-scoped, but `OnPreprocessBuild` still called the no-target installer and could repair/mutate every SHINOBU renderer asset for every build target. That is unnecessary cross-target churn in a multi-agent workspace.
Solution: Added `InstallRequiredFeatures(BuildTarget)`. Player-build preprocessing now repairs only renderer assets selected by the same target predicate used for validation; menu/no-target setup still installs all assets by explicit human action.
Rejected Alternatives: Keeping all-target repair during every build was rejected because it creates avoidable asset churn. Removing the manual all-target installer was rejected because artists/engineers still need a deliberate one-shot setup route.
Scalability potential: No runtime visual math changed. This only scopes editor asset repair to the route being built.
Hardware Impact: 0 runtime frame cost. Developer hardware impact is lower Unity asset save/import churn for standalone iteration while Android remains strict for Mobile/Quest renderer routes.

## Loop 35 Raster Fail-Closed Edge-Mask Clear

Problem: The stale edge-mask invalidation route still assumed compute was available. If `SystemInfo.supportsComputeShaders` was false, or if the compute asset was missing, `AddRenderPasses`/`RecordRenderGraph` returned before publishing any graph-declared black `_H8BilateralDrsEdgeMask`.
Solution: Kept the normal compute-supported fail-close on `ClearEdgeMask`, but added a 1x1 raster RenderGraph clear that creates an R8/R16/RGBA8 renderable black edge mask, binds it as a color attachment, clears it with `RasterCommandBuffer.ClearRenderTarget`, and publishes it through `SetGlobalTextureAfterPass`. `AddRenderPasses` now enqueues clear-only mode for compute-missing or compute-unsupported frames so stale mask state is invalidated without a blit or global setter.
Rejected Alternatives: A CPU/global shader fallback was rejected because it would add unmanaged state mutation outside RenderGraph. A color-only upscaler fallback was rejected because Tasks 07/08 require depth/Sobel bilateral reconstruction. Leaving unsupported compute as a silent return was rejected because prior successful frames could leave a debug/proof texture visible.
Scalability potential: No visual-tier math changed. Low/middle/high/ultra still use the same continuous `GlobalQualityWeight` bilateral route when compute and depth are valid; unsupported compute now has one graph-declared black proof artifact instead of stale data.
Hardware Impact: Active reconstruction cost is unchanged. Failure-path cost is one 1x1 raster clear and one graph-declared global texture publication, below measurable frame budget in practice but still PENDING Unity Frame Debugger/profiler proof. It removes stale debug/proof-state risk on weak or malformed backends.

## Loop 36 Active-Target Auto-Installer Scope

Problem: Pass 34 target-scoped build preprocessing, but editor reload still scheduled the no-target installer. That meant a normal script reload could repair/mutate every SHINOBU renderer asset, including Quest assets during PC iteration.
Solution: Added `InstallRequiredFeaturesForActiveBuildTarget()` and changed `QueueInstallAfterReload` to schedule the active build target route. The explicit menu command keeps `BuildTarget.NoTarget` for deliberate all-target setup, and build preprocessing still uses `BuildReport.summary.platform`.
Rejected Alternatives: Disabling auto-install was rejected because renderer feature serialization still needs a Unity-API repair route. Keeping no-target auto-install was rejected because it creates avoidable cross-target asset churn in a multi-agent workspace.
Scalability potential: No runtime visual math changed. Low/middle/high/ultra routes still use the same continuous `GlobalQualityWeight` math; this only narrows editor mutation to the currently selected build target.
Hardware Impact: 0 runtime frame cost. Developer hardware impact is reduced Unity asset save/import churn during script reload and standalone iteration.

## Loop 37 Non-Finite DTO Published-CBuffer Invalidation

Problem: `PublishPendingParameters` copied pending params into the active Vault lane and used `CheckFaultsAndDump`, but a failed finite check only stopped `_pendingGpuUpload`. A previous successful `s_publishedConstantBuffer` could remain visible to RenderGraph if dimensions still matched.
Solution: `PublishPendingParameters` now calls `InvalidatePublishedParameters()` immediately when the active DTO fails the finite/layout guard. RenderGraph then fails closed to the declared edge-mask clear path instead of importing stale constants.
Rejected Alternatives: Relying on dimensions to mismatch was rejected because NaN/fault frames can preserve the previous resolution dimensions. Forcing a local job completion or immediate upload retry was rejected because dispatcher/VisualSync owns the completion and upload windows.
Scalability potential: No visual-tier math changed. This protects every tier from stale visual state after a numerical fault while preserving the same continuous quality curve on healthy frames.
Hardware Impact: Hot healthy path adds no work. Fault path clears static references only; expected cost is below measurement noise and prevents stale GPU constants after a NaN event.

## Loop 38 CSV Profile Fail-Closed Stale Row Purge

Problem: `LoadQualityProfilesCsv` parsed into the existing Vault profile lane, but if the file was missing, malformed, inaccessible, or parsed zero rows, the previous valid `UpscalerProfileDTO` rows could remain active. That is stale authoring state masquerading as current data.
Solution: Clear the `UpscalerProfileDTO[32]` Vault lane before reading/parsing, mark `_profilesSeeded=false` until a positive parse count succeeds, reject null/rooted/parent-traversal paths, and catch cold file I/O failures as a zero-row fail-close.
Rejected Alternatives: Keeping last-known-good profiles was rejected because this route has no versioned profile provenance row and would violate one fact/one route proof. Throwing on CSV load was rejected because the editor facade should fail closed without destabilizing Play Mode.
Scalability potential: Low/middle/high/ultra profiles still feed the same continuous `GlobalQualityWeight` math when valid. Invalid CSV now collapses to no profile override rather than preserving stale tier curves.
Hardware Impact: 0 us hot runtime. Cold load cost adds one bounded 32-row clear and exception guard; expected cost is below editor-visible noise and prevents stale Vault tuning rows on weak devices and CI runs.

Verification: Scoped forbidden hot-path scan, direct sibling-runtime reference scan, direct trailing-whitespace scan, scoped `git diff --check`, `PolishMandateStaticAudit.py --fail-on-pack-one`, `JobCompletionAudit.py --fail-on-frame-path --fail-on-raw-runtime-complete`, and `BufferIDSovereigntyAudit.py --fail-on-duplicates` were run after the patch. Results: no owned-source forbidden hits, no sibling-runtime references, no whitespace errors, no duplicate BufferIDs, and only the known global warning counts. Dotnet build was not launched because CPU guard returned 100 and generated `.csproj` files still have no BilateralDrs coverage.

## Loop 39 CSV Whole-File Strict Parse

Problem: Pass 38 cleared stale profile rows before CSV load, but `ParseQualityProfiles` could still publish partial data if one valid row parsed before a later malformed row. It also stopped scanning after the 32-row profile lane filled, hiding malformed or overflow rows at the end of the same file.
Solution: `ParseQualityProfiles` now scans the whole CSV byte span, distinguishes skippable rows from malformed data rows, clears the Vault profile lane and returns zero on any malformed data row, and fails closed when valid rows exceed the fixed `UpscalerProfileDTO[32]` capacity.
Rejected Alternatives: Keeping first-valid-row partial publication was rejected because the Vault lane would no longer represent the current authoring file. Growing the profile lane dynamically was rejected because the runtime route owns a fixed DataVault capacity and the parser is a cold tuning bridge, not a global heap.
Scalability potential: Low/middle/high/ultra profiles still feed the same continuous `GlobalQualityWeight` curve when the file is valid. Invalid or over-capacity CSV now collapses to the base continuous curve with no stale or partial tier override.
Hardware Impact: 0 us hot runtime. Cold load adds full-file validation over at most `CsvScratchBytes` bytes and a bounded 32-row clear on failure; this prevents stale tuning state on weak devices and CI without touching RenderGraph frame cost.

Verification: The route card, SHINOBU architecture doc, and durable LOG now record the same whole-file fail-closed CSV behavior. The sample `Assets/_Project/Data/upscaler_quality_profiles.csv` exists and matches the 8-column schema. Scoped forbidden hot-path scan returned no hits. `Select-String` sibling-runtime reference scan over `Assets/_Project/Scripts/Rendering/BilateralDrs` returned no hits. Direct trailing-whitespace scan and scoped `git diff --check` returned no errors. `PolishMandateStaticAudit.py --fail-on-pack-one` returned `PASS_WITH_WARNINGS`, `packOne=0`. `BufferIDSovereigntyAudit.py --fail-on-duplicates` returned `duplicates=0`. Scoped `JobCompletionAudit.py --source-root Assets/_Project/Scripts/Rendering/BilateralDrs --fail-on-frame-path --fail-on-raw-runtime-complete` returned zero findings. Broad `JobCompletionAudit.py` is externally blocked by missing `Assets/_Project/Scripts/Editor/ZeroGCComplianceScanner.cs`. Dotnet build was not launched because CPU guard returned 100 and generated `.csproj` files still have no BilateralDrs coverage.
