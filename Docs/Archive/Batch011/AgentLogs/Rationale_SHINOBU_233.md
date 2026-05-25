# Rationale_SHINOBU_233

Agent: SHINOBU_233
Status: EDITOR TELEMETRY HANDLE READ RECORDED / STATIC CHECKS PASS / COMPILE BLOCKED BY CPU GUARD

## Decision 001: Quality Curve Must Collapse To Dear Lie Proxy

Problem: Full 3D volumetric raymarching violates the MX350 mandate when executed at native resolution or without a load-shed path.
Solution: Implement a continuous GlobalQualityWeight route that drives render scale, Z steps, noise octave budget, and blends toward a depth+dither proxy instead of a binary quality switch.
Rejected Alternatives: Unity built-in fog and particle planes are rejected because they are black-box or overdraw-heavy and cannot expose unmanaged DTO ownership.
Scalability potential: Low uses depth exponential fog plus Bayer/IGN dither. Middle uses quarter/half-res low-step compute. High uses denser raymarching. Ultra spends saved bandwidth on more slices, more octaves, and richer light scatter.
Hardware Impact: Expected low-end gain versus transparent silt particle planes is fill-rate reduction, estimated 300-900 microseconds on i3/MX350 scenes with heavy overdraw. Exact proof requires GPU capture.

## Decision 002: Presentation State Is Excluded From Gameplay Truth

Problem: Fog and silt drift are visual state; hashing or rollback ownership would inject non-gameplay entropy into deterministic systems.
Solution: Keep FogConstantsDTO, point-light mock buffers, and telemetry in presentation/rendering lanes only; document rollback exclusion and use local render feature ownership.
Rejected Alternatives: StateRingBuffer/Merkle inclusion rejected because visual drift does not affect authority, save identity, or gameplay truth.
Scalability potential: Low through Ultra can vary visuals freely without corrupting deterministic replay or network rollback.
Hardware Impact: Avoids CPU/network work for visual-only state. Estimated per-frame CPU saving is small, 5-20 microseconds, but removes a correctness risk.

## Decision 003: Low-Quality Proxy Must Bypass Volumetric Dispatch

Problem: The previous proxy route still entered the volumetric compute pass and only short-circuited inside the kernel, so low-end hardware paid dispatch, target import, and half-res write cost.
Solution: Add a RenderGraph-side Dear Lie bypass when proxy blend reaches 0.999. The volumetric feature records vault telemetry and DTO state, then returns before creating RTHandles, importing buffers, or adding raymarch/composite compute passes. The existing Noir depth fog raster pass remains the 2D dithered proxy lane.
Rejected Alternatives: Keeping an in-kernel early return was rejected because it is still a compute dispatch. Adding another full shader stack was rejected because the project already owns `HectonNoirDepthFogFeature` for the exact raster proxy.
Scalability potential: Low bypasses volumetric dispatch. Middle enters reduced-res 4-20 step compute. High/Ultra uses more steps, more light influence, and flow/noise detail.
Hardware Impact: Saves one half-res raymarch dispatch and one composite dispatch on low quality. Estimated MX350 saving is 80-260 microseconds depending on resolution; proof requires RenderGraph/Profiler capture.

## Decision 004: FogConstantsDTO Becomes The Current Contract Name

Problem: The assignment explicitly requires `FogConstantsDTO`, while the existing implementation used `VolumetricFogParamsDTO`.
Solution: Add `FogConstantsDTO` with the exact 64-byte explicit layout and switch runtime/editor vault access to it. Keep the older struct only as a cold compatibility record for stale references.
Rejected Alternatives: A mass rename without compatibility was rejected because other agents may still have stale references in parallel work. Keeping only the old name was rejected because the current prompt names the contract.
Scalability potential: The DTO preserves one 64-byte GPU constant page across Low, Middle, High, and Ultra.
Hardware Impact: Layout stability prevents ARM64/GPU CBuffer misreads; direct microseconds saved are not claimed without capture.

## Decision 005: Frustum Grid Is Real But XY-Capped

Problem: The prior screen-space half-res raymarch met the visual target but did not satisfy the explicit frustum voxel grid requirement. A naive half-res 3D texture at 4K would allocate hundreds of MB and fail cheap hardware.
Solution: Add `BuildVolumetricFogGrid` as a real `RWTexture3D<float4>` compute pass. XY resolution is continuously quality-scaled and capped at 384x224, while Z dispatch uses the active `FogConstantsDTO.QualityAndLimits.y` ray-step count. The raymarch pass integrates from the 3D grid and composites through the existing bilateral half-res resolve.
Rejected Alternatives: Full half-res XY volume was rejected as memory sabotage. Pure screen-space raymarch was rejected as incomplete against Task 06. CPU-built density textures were rejected as GC/cache abuse.
Scalability potential: Low bypasses the grid completely. Middle builds a small capped grid with low Z. High and Ultra increase XY cap pressure, Z slices, noise octaves, flow, and point light contribution without changing gameplay truth.
Hardware Impact: Compared with a naive 1280x720x64 RGBAHalf grid, capped 384x224x64 saves roughly 3.6 GB/s of write bandwidth per frame equivalent and prevents 400+ MB transient texture residency. Exact GPU saving requires RenderGraph capture.

## Decision 006: FogConstantsDTO Uses Ping-Pong Constant Buffers

Problem: Streaming the 64-byte fog constants into the same `GraphicsBuffer` that the current RenderGraph pass may read risks an implicit driver stall.
Solution: Replace the single constants buffer with A/B `GraphicsBuffer.Target.Constant` buffers. `LockBufferForWrite` writes the inactive buffer, `UnsafeUtility.MemCpy` pushes exactly 64 bytes, then the active index flips before RenderGraph import.
Rejected Alternatives: `Shader.SetGlobalFloat` fanout was rejected by prompt and zero-GC doctrine. Keeping one constant buffer was rejected because it hides synchronization pressure on weak drivers.
Scalability potential: Same contract across Low, Middle, High, Ultra; only the values change through `GlobalQualityWeight`.
Hardware Impact: Avoids likely driver sync when parameters change every frame. Estimated main-thread saving is 10-40 microseconds on low-end silicon under pressure; proof requires profiler markers.

## Decision 007: Volumetric Fog Is Documented As Presentation-Only

Problem: A future netcode/save owner could accidentally treat fog parameters, silt phase, or frustum voxels as deterministic truth and poison rollback/Merkle hashes with visual entropy.
Solution: Add `Docs/ARCHITECTURE/SHINOBU_233_COMPUTE_VOLUMETRIC_FOG.md` declaring Vault IDs, RenderGraph route, and rollback exclusion. Static scan of SHINOBU_233 runtime files shows no `StateRingBuffer`, `Merkle`, or rollback hooks.
Rejected Alternatives: Adding rollback flags to the DTO was rejected because this renderer does not own netcode descriptors. Registering visual fog in save/Merkle was rejected because it has no gameplay authority.
Scalability potential: Low through Ultra may change fog cadence, density, flow, and light response without desyncing deterministic gameplay.
Hardware Impact: Keeps network/save work at 0 bytes for this visual system and avoids hash churn. Estimated saving is route-level, not per-frame GPU.

## Decision 008: Proxy Path Must Still Write Fog Output

Problem: Returning before all SHINOBU_233 passes at proxy blend `>= 0.999` skipped 3D cost but also left this feature with no owned fog output.
Solution: Keep the RenderGraph route active in proxy-only mode, skip only `BuildVolumetricFogGrid`, then run the cheap screen-space dither raymarch and composite. Near-proxy quality scales render scale, ray steps, and light count by volumetric contribution.
Rejected Alternatives: Full early return was rejected because it delegated output to a neighboring raster feature. Full 3D dispatch under mostly-proxy blend was rejected because it spends volume bandwidth on invisible contribution.
Scalability potential: Low executes 2D Dear Lie only. Middle fades in small-grid compute. High/Ultra expand grid slices, XY cap, and mock/real point-light contribution.
Hardware Impact: Saves the 3D grid write and volume sampling at survival quality while preserving fog output. Estimated MX350 saving versus previous near-proxy full-grid path is 90-280 microseconds depending on resolution.

## Decision 009: Main Render Targets Belong To RenderGraph

Problem: Persistent `_halfTexture`, `_volumeTexture`, and `_compositeTexture` RTHandles could be released/reallocated during `RecordRenderGraph` when resolution or quality changed.
Solution: Replace the main volumetric outputs with transient `TextureDesc`/`renderGraph.CreateTexture` resources. Only cold fallback external texture handles remain persistent.
Rejected Alternatives: Persistent RTHandles were rejected for non-history outputs because RenderGraph can own lifetime and aliasing. Native-resolution 3D persistence was rejected as memory pressure.
Scalability potential: Low creates only proxy half/composite transients. Middle/High/Ultra add a capped transient 3D grid when volumetric contribution is visible.
Hardware Impact: Removes persistent RTHandle churn risk on resolution/quality changes and lets RenderGraph alias memory. Exact savings require RenderGraph memory capture.

## Decision 010: Vault Handles Must Be Pointer-Free

Problem: `VaultBufferHandle<T>` stores legacy pointer metadata and can tempt stale cross-frame alias use.
Solution: Store `VaultGenerationHandle<T>` descriptors, allocate/refresh from cold feature gates, and resolve phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle`.
Rejected Alternatives: Continuing `.Resolve(_vault)` was rejected because it is now explicitly marked legacy in `GlobalDataVault`.
Scalability potential: Same descriptors work across Low, Middle, High, Ultra without changing DTO layout or ownership route.
Hardware Impact: Prevents stale pointer use during vault relocation/fence events. Direct frame-time saving is not claimed; correctness gain is required.

## Decision 011: External Fog Inputs Are Previous-Frame Shader Globals

Problem: Marine snow and abyssal flow currently arrive through shader globals, so SHINOBU_233 cannot declare a same-frame RenderGraph dependency on their producer passes.
Solution: Treat those textures as previous-frame presentation bridge inputs, validate texture dimensions/formats before binding, and fail closed to 1x1 fallbacks. The route card records that same-frame graph handles require an upstream shared resource contract.
Rejected Alternatives: Pretending shader globals are current-frame graph dependencies was rejected. Adding direct references to upstream owners was rejected as cross-domain coupling.
Scalability potential: Low can ignore invalid inputs. Middle/High/Ultra consume valid bridge textures without blocking compile boundaries.
Hardware Impact: Avoids undefined resource sampling and driver validation errors. Saves unknown debugging time; microsecond gain is not claimed.

## Decision 012: Frame Parameters Move To A Validated CBuffer

Problem: Per-pass `SetComputeVectorParam`, `SetComputeFloatParam`, and `SetComputeMatrixParam` fanout creates command-buffer parameter spam and mismatches RenderGraph compute command overloads.
Solution: Add a private explicit `FogFrameConstantsDTO` with 224-byte HLSL CBuffer layout: vector lanes at offsets 0..144 and inverse view-projection rows at 160/176/192/208. Upload uses A/B constant buffers and `UnsafeUtility.MemCpy`; creation is gated by exact offset validation.
Rejected Alternatives: Keeping per-dispatch scalar/vector writes was rejected because it repeats CPU command setup every pass and caused API fragility. Packing the matrix into ad hoc floats was rejected because it is harder to audit against HLSL register lanes.
Scalability potential: Low through Ultra share one stable frame CBuffer; only values change continuously with quality, resolution, and external bridge state.
Hardware Impact: Estimated command setup reduction is 5-20 microseconds on low-end drivers when all three passes execute. Exact proof requires Unity profiler markers.

## Decision 013: Proxy Compute Must Bind A Real Volume SRV

Problem: The proxy-only route skips 3D grid creation, but the raymarch kernel still declares `_HectonVolumetricFogVolume`. Some drivers validate declared SRVs even if the proxy branch returns before sampling.
Solution: Prewarm a 1x1x1 fallback Texture3D RTHandle and import it as the proxy volume read texture. The grid pass is still skipped; only validation-safe resource binding remains.
Rejected Alternatives: Leaving the SRV unbound was rejected as driver-dependent undefined behavior. Creating a graph 3D volume in proxy mode was rejected because it spends memory bandwidth for a texture that is not sampled.
Scalability potential: Low quality keeps Dear Lie cost but avoids validation failures. Middle/High/Ultra bind the real transient frustum grid.
Hardware Impact: Saves the 3D grid allocation/dispatch while preventing black-frame or validation stalls on strict compute drivers.

## Decision 014: RenderGraph Recording Is Allocation-Free

Problem: `RecordRenderGraph` could previously call GPU buffer creation, fallback texture creation, or RTHandle wrapper creation while the renderer was recording passes.
Solution: Move GPU/fallback preparation to `Create()` and make `RecordRenderGraph` fail closed when `HasGpuState` or Vault views are absent. External bridge RTHandles are refreshed before enqueue and only when source identity changes; fallback wrappers are separate from external wrappers.
Rejected Alternatives: Allocating wrappers inside `RecordRenderGraph` was rejected because graph recording should describe work, not repair ownership. Disabling external bridge textures entirely was rejected because Task 09 requires abyssal flow and marine density consumption when valid producers exist.
Scalability potential: Low still binds fallbacks with no graph allocation. Middle/High/Ultra can consume valid previous-frame bridge textures without a C# assembly dependency.
Hardware Impact: Avoids surprise RTHandle/GPU allocation in the graph record phase. Exact frame saving not claimed; the correctness target is no hidden allocation churn.

## Decision 015: Editor Read Path Must Not Create Vault State

Problem: `AbyssalAtmosphereTunerWindow.TryResolveParams` was named like a read accessor but called `GetBuffer<FogConstantsDTO>`, which can create or grow Vault storage.
Solution: Replace it with `TryGetGenerationHandle<FogConstantsDTO>` plus `TryResolveHandle`. Missing runtime-owned params now fail closed in the editor instead of silently materializing a shadow lane.
Rejected Alternatives: Letting the editor create the runtime params buffer was rejected because the render feature owns that fact and must provide the proof route. Adding another editor-owned DTO mirror was rejected as shadow state.
Scalability potential: Designers can still tune the live Vault DTO once the runtime owner has bootstrapped it; no quality tier changes DTO identity.
Hardware Impact: Editor-only, runtime frame cost 0. Avoids false refcount/growth side effects during tuning.

## Decision 016: Compile Verification Hit An Unrelated Core Dependency Wall

Problem: A compile check was required after C# edits, but full `dotnet build .\Assembly-CSharp.csproj --no-restore` failed before SHINOBU code on `Hecton8.Core.csproj` because `Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs` is missing. A later `--no-dependencies` compile failed before SHINOBU code on 38 unrelated missing Dynamic Decals and `_Archive/HectonWaterPhysics*.cs` source paths.
Solution: Record both dependency failures and stop compile attempts before a third strike. Static checks and source scans continue; build proof remains blocked by other domains/project file drift.
Rejected Alternatives: Creating missing Gameplay, Dynamic Decals, or Archive files was rejected as outside SHINOBU_233 domain. Re-running builds into known missing-source project files was rejected as compile-wall IO churn.
Scalability potential: No rendering scalability impact.
Hardware Impact: Prevents unnecessary compile-wall IO/CPU churn after an unrelated CS2001 failure.

## Decision 017: Cold State Repair Must Be Throttled And Inactive-Only

Problem: `Create()` is allowed to run before `GlobalRegistry.DataVault` is initialized or before GPU fallback resources are valid. Failing closed forever would disable the fog route even after dependencies become ready.
Solution: Add a 30-frame pre-enqueue cold repair lane that runs only while `HasNativeState` or `HasGpuState` is false. Native repair now refuses `IDataVault.IsAllocationLocked`; `RecordRenderGraph` still performs no Vault acquisition, fallback allocation, or RTHandle allocation. External bridge wrappers are retained across invalid producer frames and imported only when the current source still matches.
Rejected Alternatives: Per-frame `GlobalRegistry` polling after successful boot was rejected. Allocation repair inside `RecordRenderGraph` was rejected. Releasing external RTHandles on every invalid shader-global frame was rejected because transient producer inactivity can cause wrapper churn.
Scalability potential: Low through Ultra recover from bootstrap ordering without binary feature toggles; visual quality still follows `GlobalQualityWeight` after readiness.
Hardware Impact: Prevents permanent feature loss after early boot failure and avoids RTHandle release/realloc churn from unstable bridge producers. Estimated avoided hitch risk is 20-120 microseconds on wrapper churn frames; profiler proof remains pending.

## Decision 018: Marine Snow Integer Texture Sampling Must Be Scalar

Problem: `Hecton_VolumetricFog.compute` assigned `_HectonMarineSnowFogDensityTex.Load(int3(pixel, 0))` directly to an `int`, while the sibling Noir shader samples the same `Texture2D<int>` with `.r`. Some HLSL paths reject implicit vector-to-scalar conversion for typed texture loads.
Solution: Read the explicit `.r` channel in `SampleMarineSnowDensity`. This keeps the previous-frame marine snow bridge contract scalar and matches `Hecton_NoirDepthFog.shader`.
Rejected Alternatives: Repacking marine snow into another texture, adding a C# bridge conversion, or disabling marine snow in volumetric fog were rejected as unnecessary domain expansion and visual regression.
Scalability potential: Low proxy, Middle, High, and Ultra all keep the same scalar density route; quality still only changes contribution weight and ray/grid cost.
Hardware Impact: No frame-time saving claimed. This is shader compile/validation risk removal with zero runtime cost.

## Decision 019: RenderGraph RenderFuncs Must Reject Captures

Problem: The three SHINOBU_233 RenderGraph compute passes used non-static lambdas. They did not currently capture state, but the syntax allowed future hidden captures during maintenance, which would create managed delegate pressure in the render feature path.
Solution: Mark the grid, raymarch, and composite `SetRenderFunc` delegates as `static`. All required state already travels through pass data and imported graph handles.
Rejected Alternatives: Leaving ordinary lambdas was rejected because it relies on discipline instead of compiler enforcement. Moving render callbacks into instance methods was rejected because that reintroduces instance capture pressure.
Scalability potential: Low proxy and Ultra full-grid paths share the same capture-free graph callbacks; quality changes only alter pass data values and dispatched work.
Hardware Impact: Expected runtime saving is small and not claimed without profiler proof. The concrete gain is preventing future hot-path managed captures and keeping RenderGraph setup deterministic.

## Decision 020: External Bridge Inputs Must Fail Closed Before Frame CBuffer Upload

Problem: Subagent audit found that SHINOBU_233 uploaded `HectonVolumetricFogFrameParams` before final marine-snow and abyssal-flow fallback binding. If an external wrapper could not be resolved, the shader would sample fallback textures while still receiving stale active bridge params. The same audit found unrestricted external RTHandle wrapper churn on producer texture identity changes, loose 3D flow format validation, unsupported XR texture shape, and a direct `GlobalSignals.CurrentRuntimeOriginAup()` read in local AUP conversion.
Solution: Resolve final bridge handles before uploading the 224-byte frame CBuffer, then upload with the final fallback-adjusted `marineFogParams` and `abyssalFlowTextureActive`. External bridge wrappers are now a bounded two-slot cache per bridge; new producer identities beyond the cache fail closed to fallback instead of release/realloc loops. Abyssal flow validation now requires created 3D textures with float4 volume formats. The temporary XR rejection protected stereo correctness and was superseded by Decision 021's array-aware Dear Lie proxy path. Camera AUP local conversion now consumes a per-frame cached `HectonFloatingOrigin.CurrentTotalOffsetDouble` snapshot and no longer calls `GlobalSignals.CurrentRuntimeOriginAup()`.
Rejected Alternatives: Re-uploading stale CBuffers after import was rejected because final binding should be known before upload. Unbounded `RTHandles.Alloc(texture)` release/realloc was rejected under the zero-GC/render-resource churn rule. Accepting arbitrary 3D textures was rejected because the compute shader samples `Texture3D<float4>`. Claiming XR support with Tex2D/slice-1 outputs was rejected because it risks broken stereo output.
Scalability potential: Low proxy uses the same final fallback params as higher tiers. Middle/High/Ultra can consume cached valid bridge textures without changing the owned DTO layout; unsupported producer churn degrades continuously to the local shader noise route instead of stalling.
Hardware Impact: Avoids stale bridge constants and prevents unbounded RTHandle churn from ping-ponging producers. Estimated avoided hitch risk is 20-120 microseconds on unstable bridge frames; no measured GPU saving claimed. The temporary XR guard avoided invalid stereo dispatch before the Loop 11 proxy route replaced it.

## Decision 021: XR Must Collapse To Stereo Dear Lie Before Full Per-Eye Volume Exists

Problem: A blanket XR fail-closed guard protected correctness but violated the hardware matrix because Quest-class VR is an explicit target. Full stereo volumetric grid support requires per-eye frustum constants and array-aware volume ownership; pretending the existing single grid is stereo-correct would be false.
Solution: Convert the low-tier path to Unity's established XR compute convention: `RW_TEXTURE2D_X`, `COORD_TEXTURE2D_X`, `UNITY_XR_ASSIGN_VIEW_INDEX`, and `DISABLE_TEXTURE2D_X_ARRAY` keyword control. XR forces the Dear Lie proxy path, dispatches the raymarch/composite kernels across active single-pass views, and writes effective proxy blend `1.0` into `FogConstantsDTO.QualityAndLimits.w` so shader behavior matches the RenderGraph resource route.
Rejected Alternatives: Keeping XR disabled was rejected as unacceptable for Quest validation. Building a full per-eye 3D frustum grid in this patch was rejected because it would require a new stereo camera constants contract and likely double transient volume bandwidth. Letting compute macros default to array mode was rejected because non-XR 2D targets would bind against array declarations on supported compute platforms.
Scalability potential: Low and XR use cheap dithered depth fog with stereo-correct texture-array writes. Middle desktop remains small-grid 3D. High/Ultra non-XR retain capped 3D raymarch with flow, mock/real lights, and heatmap. A future high-end XR route can add per-eye frustum grids without changing the 64-byte DTO.
Hardware Impact: Quest-class single-pass XR avoids per-eye 3D volume allocation and full raymarch bandwidth, trading it for two 2D proxy slices. Estimated saving versus a naive stereo volume is hundreds of microseconds plus transient memory bandwidth; profiler proof is still blocked by CPU/build guard.

## Decision 022: XR Variants Must Be Kernel-Owned, Not Runtime Keyword Mutations

Problem: Subagent audit found `ComputeCommandBuffer.EnableKeyword/DisableKeyword` inside RenderGraph compute callbacks. That can require `AllowGlobalStateModification(true)` and still mutates shader state from graph execution. The audit also found XR proxy depth/ray reconstruction still used one mono inverse view-projection matrix and that texture-array mode trusted `XRPass` without validating the actual graph texture descriptor.
Solution: Replace runtime keyword toggles with separate compute kernel entry points. `BuildVolumetricFogGrid`, `RaymarchVolumetricFog`, and `CompositeVolumetricFog` compile with `DISABLE_TEXTURE2D_X_ARRAY`; `RaymarchVolumetricFogXR` and `CompositeVolumetricFogXR` compile without it. C# selects kernel indices at record time and never changes compute keywords. The proxy branch now samples depth and uses linear eye depth plus a screen-space shaft fake before any inverse-VP reconstruction. Single-pass array dispatch is allowed only when the source descriptor is `Tex2DArray` with enough slices.
Rejected Alternatives: `builder.AllowGlobalStateModification(true)` was rejected because it legalizes a RenderGraph side effect rather than eliminating it. Keeping the mono inverse VP for both eyes was rejected because stereo fog directions would be false. Trusting only `XRPass.singlePassEnabled` was rejected because bound texture shape is the actual resource contract.
Scalability potential: Low/Quest uses stereo-safe proxy kernels with no 3D grid. Middle/High/Ultra non-XR keeps the 2D full-grid kernels. A later high-end XR volume path can add dedicated per-eye grid kernels without changing the DTO or Vault IDs.
Hardware Impact: Removes graph validation risk and shader keyword churn. Proxy depth no longer pays inverse-VP reconstruction on XR. Exact microseconds are not claimed; expected benefit is correctness and avoiding driver/RenderGraph stalls.

## Decision 023: Compute Kernel Discovery Must Be Cold And Guarded

Problem: After splitting 2D and XR compute entry points, `Setup()` still called `FindKernel` directly. A missing shader kernel or hot-swapped compute asset could throw during render setup or keep stale kernel indices/thread-group sizes.
Solution: Move kernel route validation into a guarded cold path. `Create()` calls `PrepareComputeKernels`, all required kernel names are checked with `ComputeShader.HasKernel` before `FindKernel`, and `Setup()` returns false before enqueue if the route is invalid. Kernel indices and all 2D/XR thread-group sizes reset whenever the compute asset identity changes.
Rejected Alternatives: Letting `RecordRenderGraph` discover missing kernels was rejected because graph recording should describe already validated work. Runtime keyword fallback was rejected because Decision 022 removed keyword mutation. Auto-disabling only XR kernels was rejected because 2D and XR routes share the same shader asset contract.
Scalability potential: Low/XR proxy and Middle/High/Ultra 3D routes are validated as one cold shader contract. Weak devices do not pay exception/log churn in frame setup; high-end devices keep independent thread-group metadata for XR and non-XR kernels if future kernels diverge.
Hardware Impact: No direct GPU saving claimed. Avoids render-thread exception traps, stale kernel index dispatch, and validation stalls from malformed shader assets; this is hitch prevention, not a measured frame-time reduction.

## Decision 024: Editor Validator Must Prove Shader Contract, Not Only DTO Layout

Problem: The UI Toolkit/tuning route had an editor layout validator, but it only verified native DTO offsets. After the XR kernel split, a shader asset missing one required kernel would not be caught by the menu validator.
Solution: Extend `VolumetricFogLayoutValidator` to load `Assets/_Project/Art/Shaders/Hecton_VolumetricFog.compute` and verify `BuildVolumetricFogGrid`, `RaymarchVolumetricFog`, `RaymarchVolumetricFogXR`, `CompositeVolumetricFog`, and `CompositeVolumetricFogXR` with `ComputeShader.HasKernel`.
Rejected Alternatives: Relying only on runtime `Setup()` fail-closed was rejected because it catches the defect later and provides weaker artist-facing proof. Creating another runtime warmup component was rejected as unnecessary object lifecycle surface.
Scalability potential: The validator proves that low/XR Dear Lie and high-tier 3D routes are both present in one shader asset before runtime.
Hardware Impact: Editor-only, runtime frame cost 0. Prevents integration hitches from malformed shader assets reaching scene playback.

## Decision 025: Editor CSV Loading Must Respect Allocation Fences

Problem: The atmosphere tuner CSV command can create or grow Vault profile and scratch buffers. It checked compaction fence state but not the allocation lock used during AUP shifts and relocation windows.
Solution: Gate `LoadExtinctionCsv()` on `!IDataVault.IsAllocationLocked` before any `GetBuffer<T>` calls.
Rejected Alternatives: Treating editor tooling as exempt was rejected because the editor command writes the same Vault-owned buffers used by runtime fog presentation.
Scalability potential: No visual-tier change. The low-to-ultra extinction profile route remains designer-controlled, but only outside allocation-locked maintenance windows.
Hardware Impact: Editor-only, runtime frame cost 0. Prevents tooling-induced buffer growth during memory relocation fences.

## Decision 026: CSV UI Status Must Not Format Proof Data

Problem: `AbyssalAtmosphereTunerWindow.LoadExtinctionCsv()` parsed the CSV into Vault storage correctly, but then built a UI status string with `fileHash.ToString("X8")` and string concatenation. It is editor-only, but it contradicts the zero-GC tuning bridge discipline and adds managed formatting to a proof value already owned by the parser/Vault route.
Solution: Discard parser proof outputs in the UI command with `out _` and set a fixed success message. The parser still computes hash/count for callers that need it; the editor label no longer materializes formatted proof strings.
Rejected Alternatives: Keeping dynamic hash/count text was rejected because a status label is not the proof artifact. Adding a custom char-buffer formatter for UI Toolkit was rejected because `Label.text` still consumes a managed string, making the extra machinery fake rigor.
Scalability potential: No visual-tier change. Designer CSV tuning stays cold and Vault-backed from low to ultra quality; runtime fog scaling is untouched.
Hardware Impact: Editor-only, runtime frame cost 0. Removes avoidable managed formatting from the tooling path.

## Decision 027: Editor Validator Must Reject Shader Variant Drift

Problem: The compute route no longer mutates runtime keywords, but the editor validator only checked kernel existence. A future edit could add `multi_compile` or `shader_feature` pragmas to `Hecton_VolumetricFog.compute`, creating extra compute variants and possible first-use shader compilation stalls during gameplay.
Solution: Extend `VolumetricFogLayoutValidator` with source-level pragma validation. The validator rejects variant pragmas and verifies that the three non-XR kernels carry the `DISABLE_TEXTURE2D_X_ARRAY` define while the two XR kernels do not.
Rejected Alternatives: Relying on runtime `HasKernel` was rejected because kernel existence says nothing about variant count. Adding runtime keyword warmup was rejected because the correct route is no runtime keyword surface in this domain.
Scalability potential: Low/XR proxy and high-tier 3D paths remain fixed-kernel routes; quality changes use DTO values and graph resources, not shader permutation churn.
Hardware Impact: Editor-only validation, runtime frame cost 0. Prevents accidental shader variant growth from reaching play mode; no measured frame saving claimed.

## Decision 028: Proxy Blend Must Not Carry Binary Step Semantics

Problem: `ResolveProxyBlendForQuality()` used `proxySurvivalFloor = 1 - math.step(0.12f, quality)`. The final `max` made the output visually continuous at the threshold, but the core quality continuum still contained a binary step.
Solution: Remove the step floor and use the saturated polynomial release directly. Values below 0.12 still clamp to full proxy because `proxyRelease` saturates at 0; values above 0.12 fade continuously through `t*t*(3-2*t)`.
Rejected Alternatives: Keeping the step was rejected because the task explicitly forbids binary low-end quality switches. Moving the hard switch to C# graph logic was rejected because visual contribution must remain driven by a continuous DTO scalar.
Scalability potential: Low stays full Dear Lie proxy, middle fades in fractional volumetric contribution, high/ultra reduce proxy to zero without changing DTO layout or ownership route.
Hardware Impact: No measured frame saving claimed. This removes a quality-continuum defect and prevents future code from reading a binary floor as a device-tier switch.

## Decision 029: Route Card Must Carry Latest Shader And Quality Invariants

Problem: Code and status logs recorded the shader-variant validator and binary-step removal, but the architecture route card still described the older route at a higher level.
Solution: Update `SHINOBU_233_COMPUTE_VOLUMETRIC_FOG.md` with two durable invariants: editor validation rejects shader variant pragmas, and proxy blend uses saturated polynomial input rather than a binary step.
Rejected Alternatives: Relying on chat or status logs was rejected because other agents use architecture route cards for cross-domain contracts.
Scalability potential: Keeps low/XR proxy and high-tier volumetric expansion behavior explicit for future integrators.
Hardware Impact: Documentation-only, runtime frame cost 0. Reduces integration risk from stale architecture assumptions.

## Decision 030: Blackbox Dump I/O Must Use Cold Maintenance Cadence

Problem: `FlushDeferredDiagnosticDump()` was called directly from `AddRenderPasses` after bridge refresh. The dump was deferred out of telemetry ring writes, but the actual file I/O still sat on the normal render enqueue path and was included in setup timing.
Solution: Move the call into `RunColdMaintenanceIfDue`, the 30-frame cold maintenance lane that also performs missing native/GPU repair. Invoke that lane before setup timing starts.
Rejected Alternatives: Keeping the direct per-frame call was rejected because diagnostic disk I/O is allowed for forensic proof, not as part of normal frame setup. Spawning async managed file tasks was rejected because it would add lifecycle/GC surface in the render feature.
Scalability potential: Low through Ultra keep identical telemetry semantics; only dump export cadence changes under fault conditions.
Hardware Impact: Normal frames do not gain measured GPU time. Fault frames avoid placing synchronous dump I/O directly on the RenderGraph enqueue path; exact hitch avoided depends on dump size and storage.

## Decision 031: Dear Lie Must Avoid Unused CPU Matrix Inversion

Problem: The shader proxy branch no longer reconstructs world position, but `RecordRenderGraph` still computed `viewProjection.inverse` before dispatching proxy-only and XR Dear Lie frames. That is unnecessary CPU math on the exact path intended for thermal collapse.
Solution: Add `ResolveInverseViewProjection(camera, proxyOnly)`. Proxy-only/XR frames upload identity; non-proxy frames compute the real inverse view-projection matrix.
Rejected Alternatives: Keeping the unused inverse matrix was rejected because the low-tier path must collapse both GPU and CPU work. Moving the calculation into shader was rejected because the shader proxy branch does not need the matrix.
Scalability potential: Low and XR skip the inverse. Middle/High/Ultra 3D raymarching still receives the correct inverse VP when needed.
Hardware Impact: Avoids one projection multiply and one matrix inversion on proxy-only frames. Exact microseconds require Unity profiler proof.

## Decision 032: Proxy Path Must Not Build Unused 3D Texture Descriptors

Problem: `RecordRenderGraph` skipped `renderGraph.CreateTexture(volumeDesc)` when `proxyOnly` was true, but still constructed the 3D `TextureDesc` for `_HectonVolumetricFogFrustumGrid`.
Solution: Move volume descriptor construction inside `if (!proxyOnly)`. Proxy-only/XR frames now leave `volumeTexture` at default and bind the prewarmed 1x1x1 fallback SRV for validation.
Rejected Alternatives: Leaving dead descriptor construction was rejected because the Dear Lie route is specifically meant to shed unused volume work.
Scalability potential: Low/XR proxy avoids 3D grid descriptor and texture creation. Middle/High/Ultra still build the capped frustum grid descriptor and texture.
Hardware Impact: Small CPU setup reduction on proxy-only frames. Exact microseconds require profiler proof.

## Decision 033: RenderGraph Pass Data Must Not Mirror CBuffer State

Problem: After moving frame parameters to `HectonVolumetricFogFrameParams`, the three RenderGraph pass-data classes still carried old vector/matrix fields that static render funcs never read.
Solution: Remove the stale pass-data fields and assignments. Pass data now carries only resource handles, kernel/thread metadata, and dispatch sizing values.
Rejected Alternatives: Keeping dead fields was rejected because it preserves managed setup surface after the CBuffer route made them obsolete.
Scalability potential: All quality tiers use the same smaller pass-data contracts; low proxy and high grid paths differ only by scheduled passes and dispatch sizes.
Hardware Impact: Reduces C# graph setup state. Exact microseconds require profiler proof.

## Decision 034: Binary Ledger Needs SHINOBU_233 Static Boundary

Problem: SHINOBU_233 owns Vault payload lanes and a route card, but `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` had no SHINOBU_233 row. Integrators would not see the fog payload boundary in the central static ledger.
Solution: Add a concise static-source boundary row naming owner, source files, Vault IDs `71130..71133`, DTO sizes, route card, dump path, Data Monolith absence, and compile/runtime caveats.
Rejected Alternatives: Leaving the route card as the only proof was rejected because the ledger is the central cross-agent orientation document for payload ownership.
Scalability potential: No runtime visual-tier change. Integrators can now distinguish editor/cold CSV profile ingestion from Data Monolith boot readiness.
Hardware Impact: Documentation-only, runtime frame cost 0.

## Decision 035: Shader Bridge Globals Must Be Snapshotted Once Per Enqueue

Problem: The render feature polled marine snow, abyssal flow, and biome shader globals before enqueue for external RTHandle cache refresh, then polled the same global state again inside `RecordRenderGraph`. That duplicated Unity global-state reads on the render route and allowed the graph record phase to see a different bridge state than the wrapper cache prepared.
Solution: Move all bridge-global reads into `RefreshExternalBridgeState()` and store an owner-local frame snapshot. `RecordRenderGraph` consumes only the cached snapshot fields. The feature now runs `Setup()` before bridge refresh so malformed compute-kernel state fails closed without touching shader globals or external wrapper caches.
Rejected Alternatives: Keeping duplicate `Shader.GetGlobal*` calls was rejected because it violates the one-read snapshot discipline and burns avoidable CPU on weak hardware. Passing direct upstream C# references was rejected as a sibling-domain compile-wall dependency; the current bridge remains shader-global until upstream graph handles exist.
Scalability potential: Low/XR proxy, middle grid, and high/ultra volume paths all consume the same bridge snapshot; continuous quality still decides contribution and cost without changing ownership.
Hardware Impact: Removes one duplicate set of global shader reads and invalid-shader bridge cache work per enqueued frame. Exact saving pending profiler; expected impact is small but deterministic CPU setup reduction.

## Decision 036: Render Frame Identity Must Be Owner-Local

Problem: `AddRenderPasses` already captured `Time.frameCount` for cold maintenance and setup measurement, but `RecordRenderGraph` and telemetry still read `Time.frameCount` again through visual phase and ring writes. That can drift if Unity changes frame counters between enqueue and graph recording, and it adds another global read in the render pass.
Solution: Pass the captured `currentFrame` through `Setup()` into the pass. `ResolveVisualPhaseSeconds()` and telemetry `FrameIndex` now use `_frameIndex`; the only remaining frame-count read in this domain is the owner-phase read in `AddRenderPasses`.
Rejected Alternatives: Keeping duplicate `Time.frameCount` reads was rejected because one owner-phase frame snapshot is enough for presentation fog. Moving to simulation tick was rejected because this is visual-only presentation drift, not gameplay truth.
Scalability potential: Low uses 5 Hz quantized phase, middle interpolates through the same frame snapshot, high/ultra can update at 60 Hz. No quality tier changes DTO identity or save/rollback authority.
Hardware Impact: Small CPU hygiene improvement and deterministic telemetry/phase alignment. Exact microseconds not claimed without profiler proof.

## Decision 037: Shader Size Constants Must Use The Sanitized Descriptor Snapshot

Problem: `RecordRenderGraph` computed safe `fullWidth/fullHeight` from the active color descriptor, but later reused raw `sourceDesc.width/sourceDesc.height` for half-target quantization and `_HectonVolumetricFogFullSize`. A bad or dynamic descriptor could therefore bypass the C# safety clamp and feed zero or negative dimensions into shader reciprocal math.
Solution: Route all downstream size math through the sanitized `fullWidth/fullHeight` snapshot. Half-resolution dimensions now multiply the safe snapshot by render scale, and the frame CBuffer writes `1f / fullWidth` and `1f / fullHeight` from guaranteed positive integers.
Rejected Alternatives: Adding shader-side recovery only was rejected because the CPU already owns descriptor normalization. Letting RenderGraph validation catch this was rejected because invalid size constants can still poison fog UV math before a visible validation failure.
Scalability potential: Low proxy, XR proxy, middle grid, and high/ultra volume paths all consume identical sanitized screen-size constants. Quality changes still affect render scale and grid dimensions without changing DTO layout or ownership.
Hardware Impact: No measured microsecond gain claimed. Prevents NaN/invalid reciprocal risk in the render path and removes duplicated descriptor reads after the owner-local size snapshot.

## Decision 038: Editor Telemetry Must Use The Same Generation-Checked Vault Route

Problem: The atmosphere tuner used generation handles for live fog params, but its telemetry graph still read `ShinobuVolumetricFogTelemetryRing` through `TryGetBuffer`. Even though this is editor-only, it created a second access style for the same domain and weakened the H-Phi proof trail for the 300-frame ring.
Solution: Change `DrawTelemetryGraph` to acquire `VaultGenerationHandle<VolumetricFogTelemetryEntry>` and then resolve the phase-local `NativeArray` view through `TryResolveHandle`.
Rejected Alternatives: Keeping `TryGetBuffer` was rejected because the prompt and route card name generation descriptors as the current SHINOBU_233 handle policy. Creating an editor-owned telemetry cache was rejected because it would be shadow state.
Scalability potential: No runtime visual-tier change. Low through Ultra telemetry remains the same Vault-owned ring; the editor now reads it through the same descriptor route regardless of quality.
Hardware Impact: Editor-only, runtime frame cost 0. Reduces route ambiguity and prevents future tooling from treating the telemetry ring as a direct global buffer.

## Decision 039: Contracts Must Resolve Their Own System Symbols

Problem: `VolumetricFogContracts.cs` uses `Obsolete` and `IndexOutOfRangeException`, but the file did not import `System`. Unity project implicit-usings are not a valid contract for first-party runtime source in this codebase.
Solution: Add a direct `using System;` to the SHINOBU_233 contracts file. No DTO layout, Vault route, or runtime logic changed.
Rejected Alternatives: Waiting for a blocked `dotnet build` was rejected because the missing namespace is visible from source. Fully qualifying each symbol was rejected because the local file already uses normal namespace imports for runtime primitives.
Scalability potential: No quality-tier behavior change; Low through Ultra keep the same CBuffer and Vault contracts.
Hardware Impact: Runtime cost 0. Compile-wall impact is positive because the fix is confined to one domain file and avoids an avoidable C# symbol error.

## Decision 040: Dear Lie Must Be Raster, Not Compute

Problem: The proxy-only branch skipped the 3D grid, but still scheduled compute raymarch and compute composite passes. The batch requires low-tier Dear Lie to be a fragment shader and to bypass expensive compute dispatches entirely.
Solution: Add `Hecton_VolumetricFog_DearLie.shader` with a `DearLieProxy` fragment pass and route `proxyOnly` frames directly through a raster RenderGraph pass. That pass samples scene color/depth and applies analytical exponential depth fog plus Bayer/stochastic dither from the existing CBuffers.
Rejected Alternatives: Keeping proxy math inside `RaymarchVolumetricFog` was rejected because it still pays compute-dispatch overhead on the thermal-collapse path. Adding a second compute kernel was rejected for the same reason.
Scalability potential: Low/XR uses one raster full-screen pass and no fog compute dispatch. Middle/High/Ultra still run 3D frustum grid plus reduced raymarch, then raster bilateral composite.
Hardware Impact: Low/XR path removes grid, raymarch, and compute composite dispatches from proxy-only frames. Exact GPU microseconds require Frame Debugger/GPU profiler proof.

## Decision 041: Camera Composite Must Not Be An RGBA16F UAV

Problem: `_HectonVolumetricFogComposite` was created through the same descriptor helper as 3D/half fog targets, forcing `R16G16B16A16_SFloat` and random write on the camera-color replacement.
Solution: Split graph target formats. Volume and half fog remain `R16G16B16A16_SFloat` UAVs because compute writes them. The full-resolution camera replacement is now a raster attachment with `enableRandomWrite=false`; `RGBA16F/RGBA32F/None` source formats collapse to `B10G11R11_UFloatPack32`.
Rejected Alternatives: Keeping `RGBA16F` as main color was rejected by the Noir rendering mandate. Writing the final composite from compute into source format was rejected because source formats are not guaranteed UAV-capable and would keep unnecessary full-res compute.
Scalability potential: Low/XR proxy and High/Ultra volumetric both share the same camera-format raster composite route. Quality changes cost and look, not the final target ownership.
Hardware Impact: Removes full-resolution random-write HDR color UAV from the fog route and reduces main-color bandwidth on HDR pipelines that expose `RGBA16F`. Exact saving pending GPU capture.

## Decision 042: Raster Helper Must Reuse Imported CBuffer Handles

Problem: The new raster helper initially accepted raw `GraphicsBuffer`s and imported params/frame buffers internally. The non-proxy route already imports those buffers for grid/raymarch compute, so the helper created redundant RenderGraph handle setup for the same resources.
Solution: Change `AddRasterFogCompositePass` to accept `BufferHandle`s. Proxy imports params/frame once before its raster pass; non-proxy reuses the same handles for grid, raymarch, and raster composite.
Rejected Alternatives: Leaving duplicate imports was rejected because it weakens the claim that the raster route is the cheapest possible graph setup. Passing global shader IDs only was rejected because RenderGraph must still declare buffer reads.
Scalability potential: Low proxy and High/Ultra composite both use one explicit CBuffer import route per frame path.
Hardware Impact: Tiny CPU graph-recording reduction; exact microseconds require profiler proof.

## Decision 043: Unscheduled Compute Kernels Must Be Removed

Problem: After final composite moved to the raster shader, the compute asset still declared `CompositeVolumetricFog` and `CompositeVolumetricFogXR`, and C# validation still required them. That retained dead shader warmup/import surface.
Solution: Delete the compute composite kernels, their source-color/half-input/composite RW declarations, and C#/editor validation references. The compute shader now owns only grid build and raymarch; raster shader owns proxy and final composite.
Rejected Alternatives: Keeping unused kernels as a fallback was rejected because there is no runtime route that schedules them, and the fallback itself is now the raster Dear Lie route.
Scalability potential: Low proxy validates only raster fallback. Middle/High/Ultra validate grid plus reduced raymarch compute, then raster composite.
Hardware Impact: Runtime frame cost unchanged from Loop 31; shader import/warmup surface reduced by two kernels.

## Decision 044: Tail Audit Must Reflect The Raster Ownership Split

Problem: The append-only log still ended with an older self-audit that named a compute composite pass and an outdated pointer/dependency graph. That report no longer matched the code after the Dear Lie proxy and full-resolution composite moved to raster passes.
Solution: Append a new self-audit instead of rewriting history. The refreshed report records the 3-kernel compute asset, raster-only proxy, raster bilateral composite, B10G11 camera replacement fallback for HDR source formats, DTO byte layouts, Vault IDs, and the CPU-gated compile state.
Rejected Alternatives: Editing the older audit in place was rejected because `LOG_SHINOBU_233.md` is top-old/bottom-new evidence. Leaving the stale audit as the tail entry was rejected because other agents would read the wrong dependency graph.
Scalability potential: Low/XR evidence now states one raster Dear Lie pass with no fog compute dispatch. Middle/High/Ultra evidence states 3D grid plus reduced raymarch compute, then raster composite.
Hardware Impact: Documentation-only, runtime frame cost 0. Reduces integration risk around low-tier GPU dispatch count and shader warmup surface.

## Decision 045: Audit Placement Is Part Of The Evidence Contract

Problem: The first Loop 34 log patch matched an earlier `</SELF_AUDIT>` token, so the refreshed audit was present but not physically at the bottom of `LOG_SHINOBU_233.md`.
Solution: Re-append the refreshed current-route audit at EOF and record this placement repair in status/rationale. The bottom block now carries `revision="Loop34_Bottom_RasterOwnership"`.
Rejected Alternatives: Deleting or moving older log material was rejected because the file is append-only. Leaving the audit in the middle was rejected because bottom-of-log is the agreed reader contract.
Scalability potential: Documentation-only. The current low/mid/high/ultra route proof is now the last visible evidence for other agents.
Hardware Impact: Runtime frame cost 0. Prevents integration decisions from using stale compute-composite evidence.
