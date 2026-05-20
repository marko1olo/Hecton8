Status: PENDING VERIFICATION
Agent: SHINOBU_104
Domain: Echelon 8 Presentation & UX

## Rationale Entries

### Setup - Prompt Isolation
Problem: Batch file contains many neighboring agent prompts; neighboring architecture would contaminate SHINOBU_104 scope.
Solution: Extracted only `<AGENT_PROMPT id="SHINOBU_104">...</AGENT_PROMPT>` via CLI regex from `Docs/Tasks/CURRENT_BATCH.md`.
Rejected Alternatives: Reading the entire batch into working memory; using IDE open tab context; acting from the Russian summary only.
Scalability potential: Low/Middle/High/Ultra unaffected; this is scope hygiene.
Hardware Impact: 0 us runtime. Prevents cross-domain edits that would cost integration time, not frame time.

### Setup - Mandate Selection
Problem: Reconstruction touches render hot paths, GPU shader constants, ARM64 DTOs, and cinematic fake policy.
Solution: Read 8 relevant mandate files before coding: zero-GC, ARM64 layout, GPU compute, warp sizing, URP RenderGraph, noir shader aesthetics, visual fake first, execution phases.
Rejected Alternatives: Reading all registry files; skipping mandates and relying on generic Unity knowledge.
Scalability potential: Low uses bilateral/noir masking; Middle uses stable full pass; High/Ultra buys temporal and overkill constants without binary switches.
Hardware Impact: 0 us runtime. Prevents MX350-hostile render work.

### Loop 1 - Sharpening And DTO Layout
Problem: Old DRS used inverse render-scale sharpening, which amplifies ringing when scale drops below 0.65.
Solution: Replaced inverse-deficit sharpening with bounded CAS-style scalar: scale variance proxy, GlobalQualityWeight ringing guard, and dynamic clamp. Replaced BLTA hash with BILU to stop lying about bilinear+TAA.
Rejected Alternatives: Raw `1.0 - scale`, `rcp(scale) - 1`, and adding a separate heavy CPU edge detector. Shader does edge-aware reconstruction where luminance/depth exists.
Scalability potential: Low: clamp stays below 0.38-0.55 with stronger bilateral/noir masking. Middle: radius settles near 1 px. High: clamp permits sharper reconstruction without hard tier switch. Ultra: shader overkill scalar adds diagonal taps/glints.
Hardware Impact: CPU delta under 5 us; avoids spending ~0.08-0.15 ms on a separate CPU variance pass. MX350/i3 gains come from moving edge judgment into one fullscreen shader.

### Loop 1 - ARM64 Reconstruction Contracts
Problem: Reconstruction needs GPU and telemetry payloads that cannot rely on sequential compiler packing or C# properties.
Solution: Added explicit DTOs: `UberNoirReconstructionConstantsDTO` 48B at offsets 0/16/32; `MockReconstructionInputSignal` 32B; `ReconstructionTelemetryEntry` 64B one-cache-line ring sample. Added editor layout assertions.
Rejected Alternatives: `[StructLayout(Pack=1)]`, separate loose floats, managed classes, and property wrappers.
Scalability potential: Low/Middle/High/Ultra all use the same CBuffer contract; scalar lanes decide cost, not binary type swapping.
Hardware Impact: Prevents ARM64 unaligned loads. 64B telemetry entry avoids false-sharing style adjacent-frame ambiguity in forensic reads.

### Loop 2 - Bilateral Shader And Dear Lie
Problem: A clean reconstruction target from 0.5x input is too expensive and visually sterile; weak GPUs need edge preservation without FSR-class cost.
Solution: Added `Hidden/Hecton8/BilateralUpsample`: 5-tap cross baseline, scalar-gated 9-tap diagonal overkill, depth/luma/spatial weights using reciprocal math, neighborhood clamp, procedural film grain, vignette, chromatic offset, and sparse salt glints.
Rejected Alternatives: FSR-only path, separable Gaussian blur, full temporal accumulation by default, and expensive `exp` weights in the inner loop.
Scalability potential: Low: 5 taps plus Dear Lie masking. Middle: radius/history/grain lerp from GlobalQualityWeight. High: sharper clamp and lower grain. Ultra: diagonal taps and glints via `_H8OverkillParams.w`.
Hardware Impact: Estimated 0.06-0.11 ms at 1080p-class low scale on MX350/Quest for baseline; rejected full temporal path would add motion/history bandwidth and an extra pass.

### Loop 2 - Temporal Hook Fail-Closed Decision
Problem: Task 07 requires temporal hooks, but this feature must not own a private cross-frame history texture that reallocates during DRS size changes.
Solution: Shader samples `_H8ReconstructionHistoryTex`, reads `_MotionVectorTexture`, clamps history to neighborhood min/max, and pass setup requests `ScriptableRenderPassInput.Motion` only when `TemporalParams.z > 0.001`.
Rejected Alternatives: Sampling the current source as fake history; allocating two RTHandles in this feature; enabling motion pass on weak devices without usable history.
Scalability potential: Low/Middle use spatial-only until history is available. High/Ultra use the scalar lane after history warm-up.
Hardware Impact: Saves a motion-vector pass and history bandwidth on weak silicon when temporal is not viable. High-end temporal cost estimate remains +0.05 ms to +0.18 ms depending resolution.

### Loop 3 - RenderGraph And CBuffer Dispatcher
Problem: Reconstruction constants must reach HLSL without per-frame string properties or managed arrays.
Solution: Added one `GraphicsBuffer.Target.Constant` buffer, updated only on epsilon delta using `LockBufferForWrite` and `UnsafeUtility.MemCpy`, bound via `RasterCommandBuffer.SetGlobalConstantBuffer` inside the RenderGraph pass. Package source confirms RasterCommandBuffer supports this overload.
Rejected Alternatives: `Shader.SetGlobalFloat(string)`, material keyword variants, `Graphics.Blit`, and `RenderTexture.GetTemporary`.
Scalability potential: Same CBuffer feeds Low/Middle/High/Ultra. Scalar transitions avoid SRP batcher keyword churn.
Hardware Impact: Constant upload is 48 bytes and should remain below 1 us when changed; unchanged frames only bind the existing buffer.

### Loop 4 - Vault, Telemetry, And CSV
Problem: Runtime state, profiles, and black-box telemetry need fixed unmanaged ownership, not private persistent NativeArrays.
Solution: Requested Vault buffers 71030 constants, 71031 telemetry ring, 71032 aesthetic profiles, 71033 CSV scratch, 71034 mock signal. CSV loads cold via `FileStream` into native scratch and hashes names without managed string parsing. Telemetry ring is 300x64B and dumps to `Docs/AgentLogs/Dump_UBER_NOIR.bin` below 0.4 scale.
Rejected Alternatives: `File.ReadAllLines`, string split, LINQ, managed profile lists, and local persistent NativeArray fields.
Scalability potential: Low can load aggressive depth/sanity profiles; Ultra can lift overkill scalar per art-authored profile.
Hardware Impact: 19.2 KB telemetry + 16 KB scratch + 2 KB profiles in Vault. No gameplay-frame managed allocation by parser.

### Loop 5 - Human Facade And Renderer Wiring
Problem: Designers need live reconstruction control and proof without waiting for thermal throttling or capture tooling.
Solution: Added UI Toolkit `Uber Noir Tuner`, sliders for required constants, mock 0.3x signal bridge, Vault-first DTO readback, and A/B split. Wired reconstruction shader GUID into PC, PC High, Mobile, and Quest renderer assets.
Rejected Alternatives: Inspector-only serialized settings, screenshot-only comparison, and depending on real thermal pressure to trigger test cases.
Scalability potential: Low/Middle can tune radius/grain; High/Ultra tune overkill threshold. Same scalar lanes are used in runtime.
Hardware Impact: Editor-only allocation accepted. Runtime shader GUID wiring has 0 us frame cost; A/B split branch is debug-only scalar.

### Loop 5 - Renderer Asset YAML Verification
Problem: Renderer assets were touched as YAML to assign the reconstruction shader; raw YAML mutation can corrupt FileID/GUID alignment.
Solution: Verified all four renderer assets contain `reconstructionShader: {fileID: 4800000, guid: b104c09d7a4e49d69f0e8467bb15a104, type: 3}` and verified the shader meta owns the same GUID. Paths checked: `PC_Renderer.asset`, `PC_High_Renderer.asset`, `Mobile_Renderer.asset`, `Quest_VR_Renderer.asset`.
Rejected Alternatives: Blind find-and-replace without GUID proof, or leaving renderer assets unwired and relying on editor auto-assignment.
Scalability potential: All renderer tiers now point at the same shader; scalar constants, not asset swaps, decide cost.
Hardware Impact: 0 us runtime beyond enabling the intended pass. Prevents missing-shader fallback/black output on all four configured renderer assets.

### Loop 5 - Material Overkill Continuum
Problem: Existing UberNoir material bridge still used binary tier gating for visual overkill (`High/Ultra` style behavior), conflicting with the GlobalQualityWeight continuum.
Solution: Replaced bridge overkill/high-cost scalars with continuous `HomeostasisBrain.GlobalQualityWeight`, stress allowance, and hardware ceiling. Removed the unused `_H8_VISUAL_OVERKILL` keyword toggle from the global dispatcher; material shaders already read scalar `_HectonUberNoirRuntimeParams`.
Rejected Alternatives: Keeping binary Ultra keyword, or changing material shaders to add new variants.
Scalability potential: Low: ceiling 0.24-0.34 clamps POM/SSS/glint budget. Middle: 0.58 ceiling. High: 0.82. Ultra: 1.0 only when stress allows.
Hardware Impact: Saves shader variant churn and avoids sudden overkill enable hitches; CPU cost is a few scalar ops in late-frame sync.

### Loop 6 - URP-Owned Temporal History
Problem: The remaining Task 07 gap was stable history ownership. A feature-owned RTHandle history would violate RenderGraph allocation purge and can resize when DRS changes internal resolution.
Solution: Use URP's camera `RawColorHistory` as the owner-local history source. The reconstruction pass requests `RawColorHistory` through `UniversalCameraData.historyManager`, imports the previous raw color RTHandle into RenderGraph, binds `_H8ReconstructionHistoryTex`, binds `_MotionVectorTexture`, and enables temporal blend only when `RawColorHistory.GetPreviousTexture()` is already readable and the renderer supports motion vectors. The request itself is gated by a smooth GlobalQualityWeight warm-up curve so weak devices do not pay for the raw-history copy when temporal is collapsed.
Rejected Alternatives: Private double-buffer RTHandles, source-as-history, TAA accumulation texture theft, always-on raw history copy, and per-frame event subscription to `OnGatherHistoryRequests`. The selected route has a 2-frame warm-up but no new global owner and no DRS reallocation surface inside SHINOBU_104.
Scalability potential: Low: temporal scalar stays 0, bilateral/noir masks do the work. Middle: history warms but blend remains low through GlobalQualityWeight. High: scalar opens temporal accumulation. Ultra: temporal plus diagonal taps and salt glints spend headroom without keyword flips.
Hardware Impact: Avoids 50-300 us resize hitch risk and private history memory. High-end spends estimated +0.05 ms to +0.18 ms for history/motion sampling only after availability proof.

### Loop 6 - Motion Vector Sign Correction
Problem: The first temporal hook used `uv - motion * jitterPixels`, which mismatched URP's TAA convention and incorrectly used jitter amplitude as motion scale.
Solution: Compared against URP `TemporalAA.hlsl`; URP applies backward velocity as `uv + velocity`. Reconstruction now uses `uv + motion * TemporalParams.z`, while `TemporalParams.y` remains the render-scale-stabilized jitter scalar for tuning/telemetry.
Rejected Alternatives: Keeping the wrong sign until visual QA, or multiplying motion by jitter pixels.
Scalability potential: All tiers benefit from correct reprojection; low tiers still bypass temporal by scalar.
Hardware Impact: Correctness fix. Prevents ghost smear and history misregistration; no CPU saving claimed.

### Loop 7 - Temporal Fail-Closed Binding
Problem: `BuildReconstructionConstants` proves raw-history availability before enqueue, but URP history import can still fail later in `RecordRenderGraph`; a temporal scalar mismatch must not sample an unbound history texture.
Solution: When raw-history warm-up is requested but previous `RawColorHistory` or `motionVectorColor` is missing, the pass binds source color as `_H8ReconstructionHistoryTex` and RenderGraph's default black texture as `_MotionVectorTexture`. The shader then resolves temporal as current-to-current if a stale scalar opens the branch. Real temporal still requires URP-owned `RawColorHistory` plus a valid motion texture.
Rejected Alternatives: Adding a fourth CBuffer lane, allocating private fallback RTHandles, using source-as-history as the normal accumulation route, calling protected-internal `ScriptableRenderer.SupportsMotionVectors()` from this external feature, or leaving the history texture unbound.
Scalability potential: Low stays spatial-only; Middle/High/Ultra only receive real temporal after owner-local history exists. The fallback is a safety binding, not a quality tier.
Hardware Impact: No claimed speedup. It prevents black-frame/garbage-sample failure with one default black texture dependency only during history warm-up.

### Loop 7 - Depthless Reconstruction Fallback
Problem: The Quest/TBDR path can intentionally skip a real depth texture, but the bilateral shader always samples `SampleSceneDepth` for edge weights. Leaving `_CameraDepthTexture` unbound risks stale global depth or invalid sampling.
Solution: The reconstruction pass now binds RenderGraph's default black texture as `_CameraDepthTexture` when real depth is absent. Depth differences collapse to a constant, so the shader becomes luma/spatial bilateral plus Dear Lie masking on depthless frames.
Rejected Alternatives: Skipping reconstruction on depthless frames, adding a second depth-available CBuffer lane, or relying on Unity's previous global depth binding.
Scalability potential: Low/depthless devices keep the cheap bilateral/noir path. Middle/High/Ultra still use real depth when available.
Hardware Impact: No speedup claimed. It prevents a black/stale depth artifact without allocating a texture or widening the 48B CBuffer.

### Loop 7 - Editor Vault Readback Lock
Problem: The editor tuner read constants from Vault by raw pointer without taking the same lock discipline used by runtime writes.
Solution: Wrapped the editor-only `TryReadEditorReconstructionConstants` pointer read in `TryLockBuffer/TryUnlockBuffer`.
Rejected Alternatives: Leaving a naked pointer read because it is editor-only.
Scalability potential: No runtime scalability impact; live tuning remains deterministic enough for A/B proof.
Hardware Impact: Editor-only. 0 us gameplay impact.

### Loop 7 - CBuffer Cold Allocation Guard
Problem: `EnsureReconstructionConstantsBuffer` could allocate a `GraphicsBuffer` from `AddRenderPasses` or constants update if the buffer was missing, violating the zero-init/frame-loop allocation requirement.
Solution: Renamed the allocation route to `EnsureReconstructionConstantsBufferCold()` and call it from `Create()` only. Frame path now uses `IsReconstructionConstantsBufferReady()`; if false, reconstruction, RawColorHistory request, and motion input fail closed.
Rejected Alternatives: Recreating the CBuffer from the render frame after device loss, or keeping history/motion requests active when no CBuffer can drive the shader.
Scalability potential: Low/Middle/High/Ultra all share the same cold CBuffer. Unsupported devices fall back to existing visor post without reconstruction.
Hardware Impact: Prevents a possible runtime allocation hitch. No steady-state CPU saving claimed.

### Loop 7 - CSV Cold Probe Clamp
Problem: The CSV profile loader could retry `File.Exists`/`FileStream` from `AddRenderPasses` if the Vault or CSV was missing, turning a human-tuning feature into a frame-loop filesystem probe.
Solution: Added one-shot `_aestheticCsvLoadAttempted` gating. `Create()` resets the attempt for feature recreation; `AddRenderPasses` only performs a single delayed attempt if Create ran before the Vault was available.
Rejected Alternatives: Per-frame hot reload polling, managed watcher callbacks in runtime, or keeping file-system checks until the CSV appears.
Scalability potential: All tiers use the same parsed profile table once loaded. Missing CSV now fails closed without IO churn.
Hardware Impact: Prevents missing-file IO/GC jitter in frame loop. No shader cost change.

### Loop 7 - Mock Jitter Coverage
Problem: `MockReconstructionInputSignal` forced render scale and quality, but its jitter/stress lanes were not reflected in the CBuffer constants, weakening the 0.3x temporal-stability proof.
Solution: `JitterPixels` now raises the final jitter floor and `TemporalStress01` continuously reduces history trust. Editor mock mode synthesizes the same jitter/stress from mock quality, so the tuner can force harsh reconstruction without waiting for thermal pressure.
Rejected Alternatives: Scale-only mock proof, binary temporal-off mock mode, or adding another serialized editor slider outside the requested facade.
Scalability potential: Low/mock stress collapses history and stresses spatial/noir masking. High/Ultra retain temporal when stress is absent.
Hardware Impact: Correctness/proof coverage only. No steady-state speedup claimed.

### Loop 7 - Visor Low-Tier Gate Continuum
Problem: The pre-existing visor post section still used binary `lowTier ?` gates for heat haze, bullet-time visual, and internal waterline distortion in a file now owned by this reconstruction pass.
Solution: Derived `_HectonUberLowTier`, heat haze amplitude, bullet-time visual, and waterline distortion from a continuous low-tier weight computed from `GlobalQualityWeight`. The low-tier boolean is only a fallback when the resolution scaler service is absent.
Rejected Alternatives: Leaving binary low-tier visual gates because they predate SHINOBU_104, or introducing a new shader keyword.
Scalability potential: Low smoothly damps expensive/unstable visor distortion; Middle partially restores it; High/Ultra receive the authored amplitude without a pop.
Hardware Impact: Same material upload count, a few scalar ops. Removes a visual discontinuity rather than saving measurable CPU.

### Loop 7 - Global Shader Dispatcher Continuum
Problem: SHINOBU_104 touched `GlobalShaderDispatcher` to remove visual-overkill keyword churn, but the same global shader lane still had binary low-tier wake capacity and mock caustic constants plus an Ultra boolean fallback.
Solution: Added continuous `GlobalQualityWeight` helpers. Mock shader data now lerps flow magnitude/caustic intensity by low-tier weight, dynamic wake upload count interpolates from 16 slots down to 4 slots, wake params carry a float low-tier weight, and fallback overkill uses a smooth quality curve. The dispatcher Burst job now includes `CompileSynchronously = true` and `[NoAlias]` on its slot buffer.
Rejected Alternatives: Leaving binary low-tier wake constants because they were pre-existing, restoring a visual-overkill keyword, or leaving Burst aliasing ambiguous on the touched NativeArray.
Scalability potential: Low uploads 4 wake slots and subdued caustics; Middle uploads/interpolates between 4 and 16; High/Ultra use the full wake and caustic budget without a keyword flip.
Hardware Impact: Low-tier wake upload can drop from 16 to 4 float4 slots per buffer. Estimated CPU upload/scan saving is small but deterministic, roughly 2-8 us depending driver and buffer path; main value is no visual step.

### Loop 7 - Telemetry Fallback Truth
Problem: The reconstruction telemetry ring could report bilateral/temporal/decorative mode facts from constants even when the material or CBuffer was unavailable and the pass would fail closed.
Solution: Added `FALL` mode hash and fallback flag when the reconstruction path is inactive. Temporal, Dear Lie, and A/B flags are only written when the reconstruction material and CBuffer are valid.
Rejected Alternatives: Leaving black-box telemetry optimistic, inheriting decorative flags from a skipped pass, or using chat/report language instead of recorded state.
Scalability potential: No visual change. Low/unsupported hardware dumps now show the truth.
Hardware Impact: One branch in telemetry recording. 0 us meaningful frame impact.

### Verification - External Compile Wall
Problem: Build verification could not reach SHINOBU_104 code because `Hecton8.Core.csproj` references a deleted World/MapMagic source file.
Solution: Recorded the exact compiler blocker and did not fabricate a local stub or alter World ownership. Static SHINOBU_104 scans remain clean while compile proof waits for the World file/reference repair.
Rejected Alternatives: Creating a dummy `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`, editing `Hecton8.Core.csproj`, or reverting another agent's deletion.
Scalability potential: No runtime impact; this is dependency hygiene.
Hardware Impact: 0 us runtime. Build failed after 37.89s on `CS2001` before SHINOBU_104 diagnostics.

### Loop 8 - URP History Request Timing
Problem: A raw history request made from `RecordRenderGraph` is too late for URP's `GatherHistoryRequests` phase; the temporal path can look implemented while never receiving a previous raw color buffer.
Solution: Register a stable `ICameraHistoryReadAccess.OnGatherHistoryRequests` delegate from `AddRenderPasses` when the continuous quality curve requests temporal history. `RecordRenderGraph` now only imports `RawColorHistory.GetPreviousTexture()` if URP already wrote it.
Rejected Alternatives: Late `cameraData.historyManager.RequestAccess<RawColorHistory>()`, feature-owned RTHandle history, always-on raw history request, and per-frame delegate allocation.
Scalability potential: Low: request is cleared and temporal remains collapsed. Middle: request warms up through scalar curve. High/Ultra: temporal history becomes real after URP has a previous texture.
Hardware Impact: Correctness fix. Avoids a dead temporal hook without adding a DRS-resizing allocation surface.

### Loop 8 - RenderGraph-Owned CBuffer Binding
Problem: Constants were uploaded correctly, but the frame path also called `Shader.SetGlobalConstantBuffer`, mutating global shader state outside the declared RenderGraph pass.
Solution: Removed the external global bind. The pass already binds `UberNoirReconstructionConstants` through `RasterCommandBuffer.SetGlobalConstantBuffer` after declaring texture dependencies.
Rejected Alternatives: Keeping both global and pass-local binds, or moving the bind to material properties.
Scalability potential: Low/Middle/High/Ultra unchanged; one CBuffer contract feeds all tiers.
Hardware Impact: Removes one redundant global state write on changed and unchanged frames. Expected saving is below 1 us but improves RenderGraph isolation.

### Loop 8 - Explicit Shader Telemetry Layout
Problem: `UberNoirShaderTelemetryEntry` still used `Sequential, Pack=4` in a touched runtime telemetry ring, leaving ARM64 layout to compiler packing.
Solution: Converted it to `[StructLayout(LayoutKind.Explicit, Size = 48)]`, mapped offsets 0 through 44, and added editor offset assertions. The hot ring write now assigns a default struct field-by-field.
Rejected Alternatives: Leaving `Pack=4` because the struct already happened to be 48 bytes, or adding a managed class wrapper.
Scalability potential: No visual tier change. The same telemetry ring remains readable across weak and high-end devices.
Hardware Impact: Prevents unaligned/padding ambiguity on ARM64. Runtime speed claim is 0-3 us at most; this is ABI safety.

### Loop 8 - Editor Mock Route Decoupling
Problem: `Uber Noir Tuner` directly cast the registry service to `ThermalDynamicResolutionAdapter`, adding an editor facade dependency on a concrete sibling runtime class and leaving the Vault mock buffer unused.
Solution: Added `HectonVisorUberPostFeature.TryWriteEditorMockReconstructionSignal` to write `MockReconstructionInputSignal` into Vault buffer 71034 under lock. The tuner now writes that owner-local buffer and clears it with `Flags=0` when the mock toggle is off.
Rejected Alternatives: Keeping the concrete adapter cast, expanding `IResolutionScalerService` for an editor-only mock method, or leaving stale mock flags in the Vault.
Scalability potential: Low mock can force 0.3x scale and jitter; Middle/High/Ultra use the same path with higher quality weights.
Hardware Impact: Editor-only. 0 us gameplay cost; improves compile-wall hygiene and CI mock isolation.

### Loop 8 - XR And Format Fail-Closed
Problem: Manual fullscreen triangle UV generation and a forced B10G11R11 reconstruction texture format can break XR layout or unsupported render-target formats before shader logic executes.
Solution: Use URP/Core fullscreen triangle helpers in the bilateral shader and inherit `sourceDesc.colorFormat` for reconstruction/post textures.
Rejected Alternatives: Manual vertex/Y-flip code, hard-coded HDR packed format, or renderer-tier asset swaps.
Scalability potential: All tiers keep the same shader; format compatibility no longer depends on a high-end target format.
Hardware Impact: No speed claim. Prevents Quest/TBDR or project-format black-frame risk.

### Loop 8 - Material Overkill Stress Continuum
Problem: The material-side UberNoir helper still used `step(0.8, stress)` inside `H8UberNoirHighCostAllowed`, creating a hard visual cliff for POM/SSS/refraction/glint contribution even though CPU constants were continuous.
Solution: Replaced the hard step with `H8UberNoirSmoothRange01(0.72, 0.88, stress)` so stress sheds high-cost visuals continuously through shader constants.
Rejected Alternatives: Keeping a binary stress cutoff because it was not a keyword, or adding another shader variant for stress shedding.
Scalability potential: Low/stressed devices fade high-cost features out smoothly; Middle partially restores them; High/Ultra retain full overkill when quality and stress allow.
Hardware Impact: 0 us CPU. Shader ALU delta is one smoothstep-style polynomial; visual pop removal is the purpose.

### Loop 9 - Mock Scale Authority Repair
Problem: The editor facade's Vault-only mock route made the reconstruction shader constants see 0.3x scale, but it did not necessarily force the actual dynamic-resolution scaler state. That weakens Task 05 because the proof can become shader-only.
Solution: Moved the 71030-71034 reconstruction Vault IDs into `UberNoirReconstructionVaultIds` under Core.Contracts and pinned them with an EditMode test. `ThermalDynamicResolutionAdapter` now reads the same `MockReconstructionInputSignal` from Vault buffer 71034 under lock each tick. When `Flags != 0`, it clamps current/target/next scale to the requested value, updates `s_systemScalePercentage`, and keeps quality forcing in a separate reconstruction mock lane. A default zeroed buffer is ignored until a mock was active, so clear-memory allocation does not reset quality every frame.
Rejected Alternatives: Restoring the editor concrete cast to `ThermalDynamicResolutionAdapter`, extending `IResolutionScalerService` with an editor-only method, or keeping duplicated magic buffer IDs in two files.
Scalability potential: Low/mock forces 0.3x with high jitter for proof. Middle/High/Ultra use the same DTO with higher `GlobalQualityWeight01`. `Flags=0` clears only this reconstruction mock and does not poison the separate DRS tuner mock.
Hardware Impact: One locked read of a 32B Vault DTO per scaler tick when the buffer exists. No production speedup claimed; this is correctness and isolated CI/editor proof.

### Loop 10 - Bilateral Diagonal Tap Continuum
Problem: The bilateral shader still had a hard diagonal-tap threshold tied to `_H8OverkillParams.w > 0.35`, which could pop the 9-tap overkill contribution on even though all CPU constants were continuous.
Solution: Replaced the hard comparison with `SmoothRange01(0.25, 0.85, _H8OverkillParams.w)` and multiplied diagonal tap weights by that scalar. The branch now exists only as a near-zero ALU skip at `0.001`; visual contribution ramps continuously.
Rejected Alternatives: Adding shader variants, keeping the hard `> 0.35` threshold because it was only a uniform branch, or always running diagonal taps on weak devices.
Scalability potential: Low keeps the 5-tap cross path and avoids diagonal ALU until overkill is effectively zero. Middle fades diagonal samples in. High/Ultra receive the extra 4 taps and salt-glint synergy without SRP keyword churn.
Hardware Impact: 0 us CPU. GPU cost remains scalar-gated; weak devices skip diagonal work, high-end spends the saved budget on sharper silhouettes.

### Loop 11 - Vault Lock Discipline Polish
Problem: The visor feature wrote constants/telemetry with Vault locks, but runtime mock reads and aesthetic profile reads still resolved Vault `NativeArray` views without a matching lock. That leaves a race against the editor mock writer or delayed CSV profile ingest.
Solution: Added owner-tagged locks around `TryReadMockReconstructionSignal`, cold CSV scratch/profile ingest, and runtime profile lookup. The mock buffer 71034 is locked for the 32B copy, scratch 71033 and profiles 71032 are locked during parse, and profile reads lock 71032 while scanning DTO rows. Moved the CSV one-shot marker after Vault/path resolution, with retry preserved for missing Vault or transient lock failure.
Rejected Alternatives: Relying on editor-only timing, assuming cold CSV ingest cannot overlap render constants, burning the CSV attempt before Vault exists, or cloning profiles into a private array.
Scalability potential: Low/Middle/High/Ultra remain unchanged visually. The same Vault-owned profile table continues to modulate reconstruction and overkill continuously.
Hardware Impact: No speedup claimed. Adds short owner-tagged locks on 32B/2KB proof and profile payloads; removes stale-handle/race risk without adding private persistent memory.

### Loop 12 - DRS GlobalQualityWeight Vault Lock
Problem: `ThermalDynamicResolutionAdapter.TryReadScalabilityStateQualityWeight` resolved the `ShinobuScalabilityState` pointer and read the published quality float without taking a Vault lock. That made the continuous DRS/reconstruction scalar a read-side race against the publishing system.
Solution: Wrapped the pointer resolve and value copy in `TryLockBuffer(BufferID.ShinobuScalabilityState, SystemID.GraphicsScalability)` with `TryUnlockBuffer` in `finally`, preserving all existing fallback behavior and early-return validation.
Rejected Alternatives: Treating a single float read as harmless, cloning the value into private adapter storage, or routing through shader globals first.
Scalability potential: Low/Middle/High/Ultra all keep the same continuous scalar law; the fix protects the authority path that decides how much reconstruction, grain, history, and overkill are allowed.
Hardware Impact: No speedup claimed. One short Vault lock around a 4B read on the DRS tick path; prevents torn/stale quality input from causing visual or scale discontinuity.

### Loop 13 - Continuous Shader Feature Telemetry
Problem: `UberNoirShaderTelemetryEntry` carried continuous `HighCostAllowed01` and `VisualOverkill01`, but the per-feature forensic lanes still collapsed POM, secondary caustics, and refraction to 0/1 bitmask values. That contradicted the shader-constant continuum and made dumps less useful.
Solution: Store `highCostAllowed01` directly in `PomEnabled01`, `SecondaryCaustics01`, and `Refraction01`. The state hash now mixes stress, high-cost, overkill, feature mask, and tier buckets so small scalar drifts are visible in the 300-frame ring. Feature mask bits now use `FeatureMaskEpsilon` presence checks instead of hard 0.5 gates; actual shader cost remains scalar-driven.
Rejected Alternatives: Keeping the bitmask-derived 0/1 telemetry, keeping hard 0.5 feature-mask thresholds, adding a second telemetry DTO, or changing shader feature gating to chase the log format.
Scalability potential: Low/Middle/High/Ultra dumps now show the actual cost allowance curve; weak devices prove gradual shedding, high-end devices prove gradual overkill.
Hardware Impact: No measurable runtime change. Three scalar assignments and two extra hash buckets on the late-frame telemetry path; forensic value only.

### Loop 14 - Global Shader CSV Hot-Path Eviction
Problem: `GlobalShaderDispatcher.LateFrameTick` called `RefreshCsvOverrides`, which could perform `Path.Combine`, `File.Exists`, `File.GetLastWriteTimeUtc`, `FileStream`, and allocate a 4KB managed scratch buffer from VISUAL_SYNC. Even throttled polling is still frame-loop IO jitter.
Solution: Removed the call from `LateFrameTick`, renamed the path to `TryLoadCsvOverridesCold`, and invoke it from `Awake` only. Runtime frame sync now consumes the already-loaded scalar override without filesystem polling. Also split legacy binary archaeology from the hot fallback: `RunBinaryGraveyardProbeCold` may check files during cold boot, while delayed-Vault frames use `GenerateEmergencyMockShaderGlobalsNoIo`.
Rejected Alternatives: Keeping the 0.05/0.25 second poll, adding a managed file watcher, making the poll rarer while leaving IO reachable from the render dispatcher, or assuming `_binaryProbeCompleted` is always set before first frame.
Scalability potential: Low/Middle/High/Ultra all keep the same shader scalar path. Designers still have the editor/global tuner and cold CSV boot value, but weak devices no longer risk mid-frame storage stalls.
Hardware Impact: Avoids periodic filesystem stat and optional 4KB read from VISUAL_SYNC, plus the delayed-Vault legacy-file probe edge case. On i3/MX350/mobile storage this removes unpredictable IO spikes; no steady-state ALU change.

### Loop 15 - DRS Visual Flag Presence Semantics
Problem: `ThermalDynamicResolutionAdapter.ResolveVisualFeatureFlags` derived feature bits with `math.step(0.5f, weight)`. The shader already receives continuous `_H8VisualFeatureWeights0/1`, so half-scale bit flips made telemetry/future consumers less faithful to the scalar overkill curve.
Solution: Added `VisualFeatureFlagEpsilon` and changed feature bits to presence semantics. A bit now means the continuous lane is materially nonzero; magnitude still lives only in the weight vectors.
Rejected Alternatives: Keeping the 0.5 step, deleting the feature flags entirely, or moving shader cost back to bitmask control.
Scalability potential: Low has no flags until weights rise above epsilon, Middle gradually gains presence while still using fractional weights, High/Ultra keep full continuous overkill strength.
Hardware Impact: No speedup claimed. Branchless `math.step` becomes simple comparisons on six scalar lanes; frame impact is negligible and the benefit is eliminating a forensic/control discontinuity.

### Loop 16 - Global Keyword Churn Removal
Problem: `GlobalShaderDispatcher.LateFrameTick` still converted low/high tier state into global shader keyword flips. That violates the scalar quality continuum and can disturb shader variant state even when HECTON visuals already receive scalar globals.
Solution: Removed the keyword constants and `Shader.EnableKeyword`/`Shader.DisableKeyword` calls. The dispatcher now only refreshes tier telemetry and publishes scalar globals (`_H8HardwareTierParams`, caustic runtime, low-tier weight, overkill/runtime params).
Rejected Alternatives: Keeping the keyword flips because they were legacy, flipping only on tier changes, or moving the same binary gate into shader code.
Scalability potential: Low/Middle/High/Ultra use the same loaded variants and shed/add visual cost through scalar lanes instead of global keyword state.
Hardware Impact: Avoids global keyword churn and variant-state discontinuity on tier transitions. No deterministic per-frame ALU saving claimed.

### Loop 17 - UberNoir HLSL Scalar Gate Polish
Problem: C# visual-overkill state had moved to continuous shader constants, but `Hecton8_UberNoir.hlsl` still used hard `step(0.5, _UberNoirFeatureFlags.*)` gates for dither, hull deformation, POM, and caustics, plus hard active checks for textured caustics/refraction.
Solution: Added `H8UberNoirFeatureScalar` and replaced those hard feature gates with saturating scalar lanes. POM now ramps from rust threshold through `H8UberNoirSmoothRange01`; decay allowance fades over 0.45-0.55; caustic/refraction activity fades from intensity parameters instead of snapping. The same helper now handles legacy instance-buffer and hull-dent DTO enable lanes, leaving only resource/count branches and POM layer-depth traversal as binary control flow.
Rejected Alternatives: Leaving binary shader gates because legacy material flags are currently 0/1, preserving half-threshold availability gates because they were not quality switches, adding shader variants, or pushing the problem back into C# feature masks.
Scalability potential: Low keeps lanes near zero and bypass branches by epsilon. Middle can partially engage deformation/caustics/POM without a pop. High/Ultra receive full feature strength through the same scalar path.
Hardware Impact: No CPU change. GPU branch predicates now follow scalar inputs; weak devices still skip near-zero work, high-end work fades in instead of flipping.

### Loop 18 - Low-Tier Bool Route Eradication
Problem: The visor runtime state still carried `bool LowTier`, and the fallback hardware path used a VRAM threshold as a binary visual policy even though the material/scaler side now publishes continuous reconstruction weights.
Solution: Replaced `RuntimeState.LowTier` with `RuntimeState.LowTierWeight01`. `ResolveLowTierFloor01` now computes a soft polynomial memory-shortage floor from graphics memory and the configured threshold, then `ResolveLowTierWeight01` merges that floor with `GlobalQualityWeight`. Internal waterline distortion, `_HectonUberLowTier`, and bullet-time visual damping all consume the same scalar.
Rejected Alternatives: Keeping `ResolveLowTierWeight01(bool)`, using `graphicsMemory <= threshold ? 0.25f : 0f`, or moving the threshold into a shader keyword/variant.
Scalability potential: Low receives a gradual floor when memory is near the configured pressure band; Middle ramps through partial damping; High and Ultra reduce the floor to zero while still respecting real thermal GlobalQualityWeight drops.
Hardware Impact: No measurable CPU saving claimed. The cost is a handful of scalar ops when building render pass state; the gain is eliminating a visual tier pop and keeping weak hardware behavior inside the same scalar continuum.
