# LOG_GPU_SCATTER_LOD_MANAGER

## 2026-05-16 - 100k Flora Indirect Scatter

What was wrong:
- Procedural flora had no dedicated rendering-domain handoff for 100k OSHINO matrices through `Graphics.RenderMeshIndirect`.
- No production references existed for `FloraManager.Instance` or `Instantiate(KelpPrefab)`, but the regression gates were not documented.
- The renderer needed GPU-side visibility, AUP-safe culling, CopyCount indirect args, homeostasis shedding, and fixed blackbox evidence.

What was done:
- Added `Assets/_Project/Scripts/Rendering/Scatter/GpuScatterLodManager.cs`.
- Added `Assets/_Project/Art/Shaders/GpuScatterLodCull.compute` with kernel `ScatterCullJob`.
- Added DataVault IDs `FloraScatterMatrices`, `FloraScatterMetadata`, and `FloraScatterMotionVectors` to `BufferID`.
- Implemented double-buffered matrix/metadata GPU uploads through `GraphicsBuffer.LockBufferForWrite`.
- Implemented append-visible indices and append-visible matrices.
- Implemented `GraphicsBuffer.CopyCount(_visibleMatrixBuffer, _argsBuffer, sizeof(uint))`.
- Implemented `Graphics.RenderMeshIndirect` submission.
- Implemented `SignalBus<CameraFrustumSignal>` consumption with signal-built fallback frustum planes.
- Implemented low 100m, mid 250m, high/ultra 500m cull tiers with 5m/2s hysteresis.
- Implemented homeostasis shed: `SystemStress01 > 0.8` halves desired cull distance.
- Implemented deterministic GPU sway motion-vector writes.
- Implemented finite matrix validation, zero-scale GPU/Burst rejection, and blackbox dump on non-finite matrix data.
- Implemented 300-frame `NativeArray<ScatterBlackBoxEntry>` with `VisibleFloraCount` from async indirect-args readback.
- Implemented `OnDisable`/`OnDestroy` release for GPU buffers, CPU audit buffers, blackbox, and Vault leases.

Cinematic cheats used:
- Replaced per-flora physical sway with deterministic hash/vector shader data.
- Replaced far-field flora truth on MX350 with 100m distance rejection.
- Kept high-tier visual overkill as longer 500m residency plus crossfade range, not more CPU simulation.

Exact microseconds saved:
- `FloraManager.Instance` removal: 0us measured; no production reference existed.
- `Instantiate(KelpPrefab)` deletion: 0us measured; no production reference existed.
- 100k GameObject/Transform submission avoided: estimated 900-1800us CPU on i3/MX350, PENDING PROFILER.
- AUP shift CPU matrix rebake avoided: estimated 150-400us per shift and 6.4MB upload avoided, PENDING PROFILER.
- CPU compacted visible matrix upload avoided: estimated 6.4MB/frame avoided at 100k capacity, PENDING PROFILER.
- CPU/GPU sync readback avoided for draw args: estimated 200-2000us stall avoided under queue pressure, PENDING PROFILER.

Verification:
- `rg "FloraManager\.Instance" Assets/_Project/Scripts Assets/_Project/Art`: no production matches.
- `rg "Instantiate\s*\(\s*KelpPrefab\s*\)" Assets/_Project/Scripts Assets/_Project/Art`: no production matches.
- `dotnet build Assembly-CSharp.csproj --no-restore`: BLOCKED by pre-existing `Hecton8.Core.csproj` missing dependency contracts.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1`: BLOCKED by missing dependency DLLs from the same baseline compile wall.
- Filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1` for `GpuScatter`/`FloraScatter`: no scatter-specific errors surfaced before the existing dependency wall.

Integrator note:
- Restore the baseline missing contracts/types first: examples include `ISimulationBucketer`, `IMacroDatabaseService`, `IPlayerMovementContracts`, `IPlayerMovementPoseReadModel`, `H8WorldPageReadTicket`, and related core contract symbols.
- Do not invent stubs in the rendering scatter domain. That would hide a cross-domain dependency failure.
## 2026-05-16 Continued Pass: Multiplatform / H-Phi Inquisition

What was wrong:
- Scatter manager still owned blackbox and CPU audit `NativeArray` fields locally after the first implementation pass.
- Blackbox telemetry did not have a fixed Pack=1 64B layout for Quest/ARM64 confidence.
- The compute kernel used a compact zero-vector syntax and only partially guarded the sway `rsqrt` denominator.
- High-tier flora residency existed, but the material was not explicitly switched into the existing `_QUALITY_HIGH` shader lane.

What was done:
- Moved scatter blackbox, CPU frustum audit planes, and CPU visibility audit mask into GlobalDataVault via `VaultBufferHandle<T>` and new BufferIDs 161-163.
- Converted blackbox telemetry to `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]` with reserved padding lanes.
- Kept source matrices and metadata as Vault handles only; remaining `NativeArray<T>` values are transient Vault/GPU views, not renderer-owned storage.
- Hardened `GpuScatterLodCull.compute` for Metal/mobile with explicit zero vectors and finite-checked `rsqrt` input.
- Added high-tier material switching for `_QUALITY_HIGH` plus stronger existing vegetation SSS, edge bloom, and local caustic lanes; low tier switches `_QUALITY_MX350` with cheap constants.

Cinematic Cheats used:
- Low/MX350 still uses a hard 100m residency lie and cheap material response.
- High/Ultra spends the saved CPU/GPU visibility budget on 500m residency, crossfade, stronger translucent flora lighting, and caustic shimmer instead of physical vegetation simulation.

Exact Microseconds saved:
- Private-native ownership eviction: 0us hot-path target, but removes leak/stale-handle risk and improves DataVault compaction compatibility.
- `rsqrt` guard: 0us CPU; GPU cost is one finite check and protects against catastrophic mobile pipeline poisoning.
- Existing 100k GameObject purge estimate remains 900-1800us CPU saved on i3/MX350 pending profiler capture.
- Existing indirect args path remains estimated 200-2000us stall avoided by not CPU-reading visible counts.

Validation:
- `rg` found no renderer-owned private `NativeArray` fields, `H8Memory.Allocate`, `H8Memory.Release`, `Allocator.Persistent`, legacy `EventBus`, scene search, or Unity Update methods in `GpuScatterLodManager.cs`.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1` is blocked by missing generated/plugin DLLs under `Temp/bin/Debug`.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1` is blocked first by missing RealtimeCSG source files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` is blocked by unrelated XR/submarine/fauna/VFX/audio errors; filtered output shows no `H8Memory`, `BufferID`, or flora scatter error.
- Filtered build scans show no `GpuScatter`/`FloraScatter` compiler errors before the external dependency wall.

## 2026-05-16 Continued Pass: Constant Buffer / Draw-State Polish

What was wrong:
- Compute cull frame constants were still fragmented across individual scalar/vector uploads instead of one packed constant buffer.
- The frustum planes were not grouped with the rest of the dispatch constants.
- Shared material buffer/scalar state could bleed between reused flora materials or multiple scatter managers.
- The blackbox dump path still had a managed debug-log fallback and wrote entries in physical ring order.

What was done:
- Added `HectonScatterFrameConstants`, a 176B `Pack = 1` C# payload mirrored in `GpuScatterLodCull.compute`.
- Created and released a `GraphicsBuffer.Target.Constant` frame constants buffer through the existing renderer lifetime.
- Switched compute dispatch setup to one constant-buffer upload when supported, with explicit per-vector fallback for unsupported platforms.
- Removed the old per-frame compute uniform IDs and `SetVectorArray` path.
- Routed indirect draw buffer/scalar bindings through one cached draw-local state object passed in `RenderParams`.
- Kept shader keyword mutation on the material asset because Unity keyword variants are material state, while per-draw buffers/scalars are isolated.
- Removed `Debug.LogError` from `DumpBlackBox` catch handling and published typed telemetry instead.
- Changed blackbox binary output to chronological ring order.

Cinematic Cheats used:
- No new physical flora simulation was added.
- Low/MX350 remains the 100m dear-lie cull with deterministic fake sway.
- High/Ultra keeps 500m residency, crossfade, SSS, caustic, edge bloom, and motion-vector lanes as flora-domain overkill.

Exact Microseconds saved:
- Fragmented compute uniform uploads replaced by one constant-buffer upload: estimated 5-20us dispatch setup reduction on weak CPU/driver paths, PENDING PROFILER.
- Shared material state isolation: 0us claimed hot-path saving; prevents state contamination and clone churn, PENDING PROFILER.
- Blackbox debug-log removal: 0us hot-path saving; fault-path allocation risk removed.
- Existing 100k GameObject/Transform submission avoidance remains estimated 900-1800us CPU on i3/MX350, PENDING PROFILER.
- Existing CPU/GPU sync readback avoidance remains estimated 200-2000us stall avoided under queue pressure, PENDING PROFILER.

Validation:
- Static scan found no `SetVectorArray`, legacy scatter per-frame uniform names, `Debug.Log`, `H8Memory.Allocate`, `H8Memory.Release`, `Allocator.Persistent`, scene search, legacy `EventBus`, Unity `Update` methods, `string.Format`, or `material.SetBuffer/SetFloat/SetVector` in the scatter manager.
- `GpuScatterLodCull.compute` remains `#pragma target 4.5` with 64 threads per group, below Metal's 1024 thread-group cap.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1` remains blocked by missing generated/plugin metadata DLLs under `Temp/bin/Debug`; filtered output still shows no scatter-specific compiler error.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` currently stops in unrelated `SubmarineFluidDynamics` CS1612/CS0200 read-only native handle errors; filtered output shows no `GpuScatter`, `FloraScatter`, `BufferID`, or `H8Memory` error.

## 2026-05-16 Continued Pass: Shader Aux Lane / Stale Args Hardening

What was wrong:
- The active vegetation shader unconditionally reads flora age and phase-seed buffers that the scatter renderer did not bind.
- Optional shader buffer reads could be enabled by stale shared material counts from other systems.
- Early exits could leave the previous indirect args instance count live, causing stale draws and stale blackbox visible counts.
- A recreated args buffer could reuse the same mesh cache key and skip initialization.
- The renderer bound the visible-matrix append buffer to material state even though the current shader does not consume it.

What was done:
- Added DataVault `BufferID.FloraScatterAge01` and `BufferID.FloraScatterPhaseSeeds`.
- Added Vault handles, GPU buffers, generation tracking, upload, draw-local binding, and scene-unload release for the age/phase lanes.
- Filled missing/expanded age data with `1.0` and phase data with deterministic hash seeds.
- Added draw-local zero fallbacks for optional shader gates: snap flags, flow field resolution, interaction/wake/impact/predator counts, abyssal grid resolution, and abyssal flow activity.
- Added `ClearVisibleState()` so invalid frustum, no active instances, or upload failure clears append counters and copies a zero count into the indirect args instance slot.
- Added blackbox flags for invalid-frustum and no-active-instance early exits.
- Invalidated the indirect-args cache on args-buffer creation/release.
- Removed the unused `_HectonScatterVisibleMatrices` material binding while preserving the append buffer for task compliance and `CopyCount`.

Cinematic cheats used:
- Low/MX350 keeps deterministic cheap age/phase shader variation without CPU animation.
- High/Ultra can use producer-authored age/phase lanes for richer growth, crossfade, and flora material overkill later without changing the renderer.
- No physical vegetation simulation was added.

Exact Microseconds saved:
- Removed unused visible-matrix material binding: estimated 1-3us driver-state reduction on weak CPU paths, PENDING PROFILER.
- Optional fallback scalar writes: no microsecond saving claimed; resource-safety only.
- Early-exit args quarantine: normal-frame cost 0us target; fault-frame cost PENDING PROFILER.
- Existing 100k GameObject/Transform submission avoidance remains estimated 900-1800us CPU on i3/MX350, PENDING PROFILER.

Validation:
- Focused static scan found no `SetVectorArray`, legacy scatter uniform paths, `Debug.Log`, local private `NativeArray`, `H8Memory.Allocate/Release`, `Allocator.Persistent`, legacy `EventBus`, scene search, Unity `Update` methods, `string.Format`, or shared `material.Set*` calls in the scatter manager / compute path.
- Symbol scan confirmed `FloraScatterAge01`, `FloraScatterPhaseSeeds`, `ApplyOptionalShaderFallbacks`, `ClearVisibleState`, indirect-args invalidation, early-exit blackbox flags, and age/phase generation telemetry are present.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1` remains blocked by missing `Temp/bin/Debug` metadata DLLs including `Assembly-CSharp-firstpass.dll`, `Hecton8.Editor.dll`, `RealtimeCSG.dll`, and plugin/runtime DLLs.
- Filtered `dotnet build Hecton8.Core.csproj --no-restore -m:1` now stops at unrelated `Assets/_Project/Scripts/Core/InputDispatcher.cs(7,2)` CS1032; no `GpuScatter`, `FloraScatter`, `BufferID`, or `H8Memory` scatter error surfaced.

## 2026-05-16 Continued Pass: Mobile Thread-Group Contract

What was wrong:
- Compute dispatch group count used a hardcoded C# `64` even though the mandate requires `ComputeShader.GetKernelThreadGroupSizes`.
- Metal/mobile safety depended on the shader staying at 64 threads, with no blackbox flag if a future kernel exceeded the 1024-thread group limit.

What was done:
- Replaced the compute dispatch sizing constant with `_dispatchThreadGroupSizeX` queried from the actual `ScatterCullJob` kernel.
- Added a separate `BurstAuditBatchSize` for CPU job scheduling so GPU ABI and CPU batch size no longer share a misleading constant.
- Added a 1024-total-thread guard for Metal/mobile compliance.
- Added `BlackBoxFlagInvalidThreadGroup` so invalid kernel dimensions show up in the 300-frame scatter blackbox.

Cinematic Cheats used:
- None added. This was a platform correctness pass.

Exact Microseconds saved:
- Query is cold GPU-state setup only: 0us normal-frame target.
- No measured microsecond claim. Correctness and platform survival only.

Validation:
- Static scan found no forbidden scatter hot-path patterns after the patch.
- `git diff --check` reported only LF-to-CRLF warnings for touched scatter files.
- Filtered `Assembly-CSharp` build remains blocked by missing `Temp/bin/Debug` metadata DLLs before scatter evidence.
- Filtered `Hecton8.Core.csproj` now stops at unrelated `SubmarineFluidDynamics.cs(614-635)` missing `VaultNativeBuffer<>`; no `GpuScatter`/`FloraScatter` compiler error surfaced.

## 2026-05-16 Continued Pass: ABI Layout / Memory Sentinel

What was wrong:
- The owned GPU payload structs had explicit `Pack = 1`, but the renderer did not actively prove their runtime size against the buffer stride contract.
- Scene unload relied on `OnDisable` to invalidate DataVault leases even though the task explicitly demands teardown proof.
- GPU dispatch group size cache could persist across buffer release until the next kernel query.

What was done:
- Added a cold `UnsafeUtility.SizeOf<T>` layout guard for `Matrix4x4`, `Vector4`, `GpuScatterFloraInstanceData`, `ScatterFrameConstants`, and `ScatterBlackBoxEntry`.
- Disabled the component before tick registration if ABI stride drift is detected.
- Published typed telemetry with `BlackBoxDumpReasonAbiLayout` on ABI failure.
- Converted `ScatterBlackBoxEntry` size to the named `ScatterBlackBoxEntryStrideBytes` constant.
- Made `OnDestroy` explicitly invalidate DataVault leases and clear GPU readiness.
- Reset `_dispatchThreadGroupSizeX` to the fallback during GPU buffer release.

Cinematic Cheats used:
- None added. This pass was platform survival and memory-sentinel hardening.

Exact Microseconds saved:
- ABI guard: 0us normal-frame target; cold initialization only, PENDING PROFILER.
- OnDestroy lease invalidation: 0us normal-frame target; unload-only.
- Dispatch group cache reset: 0us normal-frame target; correctness only.

Validation:
- Static scan found no `SetVectorArray`, `Debug.Log`, local private `NativeArray`, `H8Memory.Allocate/Release`, `Allocator.Persistent`, legacy `EventBus`, scene search, Unity `Update` methods, `string.Format`, or shared `material.Set*` calls in the scatter manager / compute path.
- ABI symbol scan confirmed `UnsafeUtility`, `ValidateAbiLayoutCold`, `BlackBoxDumpReasonAbiLayout`, `ScatterBlackBoxEntryStrideBytes`, `_abiLayoutValid`, `GetKernelThreadGroupSizes`, and unload/reset hooks are present.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` succeeded with 0 warnings and 0 errors.
- `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1` restores project assets, then remains blocked by 48 missing generated/plugin metadata DLLs under `Temp/bin/Debug`; this is outside scatter and prevents final validation.

## 2026-05-16 Continued Pass: Shader NaN Fail-Closed

What was wrong:
- `TransformPoint` returned zero on non-finite transformed positions, which could hide a poisoned source matrix and let bad flora append at origin.
- Shader-side matrix validation checked scale axes but not every matrix row.
- Local bounds constants were trusted after upload.

What was done:
- Added `HasFiniteMatrix` to validate all four rows of the source `float4x4`.
- Rejected non-finite local bounds center/extents before transform.
- Changed `TransformPoint` to return raw transformed coordinates so the existing finite center guard fails closed.

Cinematic Cheats used:
- None added. This is NaN vaccination and mobile GPU survival.

Exact Microseconds saved:
- No saving claimed. This adds defensive GPU branches; exact cost is PENDING PROFILER.
- Avoided failure mode: one invalid matrix can no longer append a visible instance through a fake origin.

Validation:
- Shader scan confirmed `HasFiniteMatrix`, raw `TransformPoint`, finite local-bounds checks, guarded `rsqrt`, and append-only-after-validation order.
- Domain scan still found no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, local private `NativeArray`, legacy `EventBus`, scene search, or debug logging.
- `git diff --check` reported only LF-to-CRLF warnings.

## 2026-05-16 Continued Pass: CPU Audit NaN Parity

What was wrong:
- The optional Burst audit used raw serialized local bounds while the GPU path was being hardened.
- CPU audit matrix validation checked usable scale but not all four rows.
- Fallback draw bounds could inherit a non-finite serialized local-bounds extent.

What was done:
- Added `ResolveSafeLocalBoundsCenter`, `ResolveSafeLocalBoundsExtents`, and `ResolveSafePositiveExtent`.
- Fed sanitized bounds into `RunBurstCullAuditOnce`, compute constants, fallback draw bounds, and editor validation.
- Added full-matrix finite rejection to the Burst `ScatterCullJob`.

Cinematic Cheats used:
- None added. This was diagnostic parity and NaN containment.

Exact Microseconds saved:
- Shipping frame: 0us target because Burst audit remains opt-in.
- No measured saving claimed; defensive checks are correctness work.

Validation:
- Symbol scan confirmed safe-bounds methods, CPU audit wiring, compute constant wiring, fallback bounds usage, and Burst `HasFiniteMatrix`.
- Forbidden-pattern scan over the scatter domain returned no `Update`, `LateUpdate`, `FixedUpdate`, `string.Format`, `EventBus`, `Debug.Log`, scene search, local private `NativeArray`, or direct `H8Memory.Allocate/Release`.
- `git diff --check` reported only LF-to-CRLF warnings.

## 2026-05-16 Continued Pass: DataVault Visual Payload

What was wrong:
- High/Ultra flora had material-wide SSS/caustic/bloom boosts but no DataVault-owned per-instance visual payload.
- The next obvious shortcuts were all bad: private renderer `NativeArray<Vector4>`, extra shader interpolators, or shared material randomization.

What was done:
- Added `BufferID.FloraScatterVisualPayload = 382` without shifting existing memory IDs.
- Added a `VaultBufferHandle<Vector4>` and `_floraVisualPayloadBuffer` to `GpuScatterLodManager`.
- Wired the payload through Vault handle resolution, active-count clamp, generation change detection, upload, draw-local binding, teardown, and lease invalidation.
- Added cold deterministic defaults only when the Vault lane is missing or undersized.
- Added `_HectonFloraScatterVisualPayload` and `_HectonFloraScatterVisualPayloadEnabled` to `Hecton_IndirectVegetation.shader`.
- In `_QUALITY_HIGH`, the shader now uses the payload to modulate existing edge, curvature/SSS, flow/caustic, and biolum channels. No extra TEXCOORD was added.

Cinematic cheats used:
- Low/MX350 binds the lane but disables consumption, returning a zero payload and preserving the cheap shader path.
- High/Ultra spends the same source-index-stable payload on visual variation instead of simulating more flora physics.
- The shader reuses existing varyings instead of adding another interpolator path for Quest/mobile.

Exact Microseconds saved:
- No measured microseconds claimed.
- Added payload memory: 16 bytes per instance, about 1.6MB for 100k active flora before allocator overhead.
- Default payload generation is cold only.
- Normal-frame upload uses existing Vault generation/dirty checks; unchanged generations skip upload.

Validation:
- Symbol scan confirmed `FloraScatterVisualPayload`, `_HectonFloraScatterVisualPayload`, `ResolveScatterVisualPayload`, generation checks, upload, binding, and release paths.
- Focused static scan found no `SetVectorArray`, `Debug.Log`, local private `NativeArray`, `H8Memory.Allocate/Release`, `Allocator.Persistent`, legacy `EventBus`, scene search, Unity `Update` methods, `string.Format`, or shared `material.Set*` calls in the scatter manager / compute path.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` is dependency-blocked outside scatter: `ArchitectEyeVisualizer` duplicate `ValidatePackedStructSizes`, plus ambiguous `LaserCutterEventPayload` references in audio/world systems.
- `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1` is dependency-blocked before scatter on missing `Temp/bin/Debug/Assembly-CSharp-firstpass.dll` and `Temp/bin/Debug/RealtimeCSG.dll`.

## 2026-05-16 Continued Pass: Shared-Material Mutation Purge

What was wrong:
- `GpuScatterLodManager.Render` still mutated `floraMaterial.enableInstancing` and toggled material keywords at draw time.
- That contaminates shared material state and makes high/low tier selection depend on the last renderer that touched the asset.

What was done:
- Added optional pre-authored `lowTierFloraMaterial` and `highTierFloraMaterial` fields.
- Added `ResolveRenderMaterial()` and `HasAnyConfiguredMaterial()`.
- Removed runtime `enableInstancing`, `EnableKeyword`, and `DisableKeyword` calls from the scatter render path.
- Kept tier scalar values draw-local in the reused `MaterialPropertyBlock`.
- Added the visual-payload Vault handle to the GPU-readiness and buffer-resolution gate.

Cinematic cheats used:
- Low/MX350 now relies on an authored `_QUALITY_MX350` material variant and keeps high-tier payload disabled.
- High/Ultra relies on an authored `_QUALITY_HIGH` material variant and spends the DataVault payload on visual overkill.
- No shader dynamic branch expansion was added for quality keywords.

Exact Microseconds saved:
- No measured microseconds claimed.
- Removed per-frame material keyword churn and instancing flag writes from this renderer.
- Expected timing is PENDING PROFILER; the confirmed gain is deterministic material-state isolation.

Validation:
- Stricter static scan found no `EnableKeyword`, `DisableKeyword`, `enableInstancing`, `renderer.material`, `.materials`, shared `material.Set*`, `SetVectorArray`, `Debug.Log`, local private `NativeArray`, `H8Memory.Allocate/Release`, `Allocator.Persistent`, legacy `EventBus`, scene search, Unity `Update` methods, or `string.Format` in the scatter domain.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- Latest `dotnet build Hecton8.Core.csproj --no-restore -m:1` is dependency-blocked outside scatter in `HectonMarineSnowRenderer`: missing `CeilDivide` at lines 1917 and 1918.
- `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1` is dependency-blocked before scatter on 63 missing `Temp/bin/Debug` metadata DLLs.

## 2026-05-16 Continued Pass: Blackbox / Payload NaN Polish

What was wrong:
- The new visual-payload lane was not visible in the scatter blackbox dump.
- `ResolveScatterVisualPayload` saturated high-tier payload values without proving they were finite.

What was done:
- Bumped scatter blackbox packet version from 1 to 2.
- Kept `ScatterBlackBoxEntry` at 64 bytes.
- Replaced separate age/phase generation dump fields with `AuxiliaryGenerationHash` and `VisualPayloadGeneration`.
- Added `CombineGenerationHash()` for age/phase auxiliary telemetry.
- Added `all(isfinite(payload))` guard in the shader before applying high-tier visual-payload modulation.

Cinematic cheats used:
- None added. This pass is crash forensics and NaN containment.

Exact Microseconds saved:
- No measured microseconds claimed.
- Blackbox write size and cadence are unchanged.
- High-tier shader adds one finite check; exact GPU cost is PENDING PROFILER.

Validation:
- Symbol scan confirmed `BlackBoxVersion = 2`, `AuxiliaryGenerationHash`, `VisualPayloadGeneration`, `CombineGenerationHash`, and shader `isfinite(payload)` guard.
- Forbidden scatter-domain scan stayed clean.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` is dependency-blocked outside scatter: `PlayerCriticalProceduralAudioRenderer` missing `ClearVaultBackedAudioBufferAliases`, and `TetherManager` missing `_fixedStepClockSeconds` / `TetherFixedClockWrapSeconds`.
- `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1` is dependency-blocked before scatter on 55 missing `Temp/bin/Debug` metadata DLLs.

## 2026-05-16 Continued Pass: Material Variant Fail-Closed

What was wrong:
- After removing runtime keyword mutation, `GpuScatterLodManager` still trusted authored material variants.
- A missing `HECTON_GPU_INDIRECT` keyword could draw the wrong shader path.
- A missing `_QUALITY_HIGH` keyword on High/Ultra could silently downgrade the 4090 path to the wrong visual tier.

What was done:
- Added cached material-variant validation in `GpuScatterLodManager`.
- Required `HECTON_GPU_INDIRECT` for every indirect flora draw material.
- Required `_QUALITY_HIGH` when `_cachedHighTier` is true.
- Accepted low-tier materials only when `_QUALITY_MX350` is enabled or `_QUALITY_HIGH` is absent.
- On invalid variant, the renderer clears indirect args before cull dispatch, skips `Graphics.RenderMeshIndirect`, and records `BlackBoxFlagInvalidMaterialVariant`.
- Kept runtime behavior mutation-free: no `EnableKeyword`, no `DisableKeyword`, no `enableInstancing` write.

Cinematic cheats used:
- Low/MX350 keeps a cheap authored path and prevents accidental high-variant use.
- High/Ultra now proves the authored high shader variant before spending the 500m residency and visual payload.

Exact Microseconds saved:
- No measured microseconds claimed.
- Normal-frame cost is a cached material instance/tier validity read after first validation.
- Fault path avoids cull dispatch, clears stale indirect args, and avoids an invalid draw.

Validation:
- Forbidden scatter-domain scan found no `EnableKeyword`, `DisableKeyword`, `enableInstancing`, `renderer.material`, `.materials`, shared `material.Set*`, `SetVectorArray`, `Debug.Log`, local private `NativeArray`, `H8Memory.Allocate/Release`, `Allocator.Persistent`, legacy `EventBus`, scene search, Unity `Update` methods, or `string.Format`.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` was externally unstable: one probe succeeded with four unrelated `ArchitectEyeVisualizer` CS0649 warnings, then later probes failed outside scatter in tether/physics contracts and finally `SargassumMicroFaunaBoids` missing `SaturateFinite01` at 9 callsites.
- `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1` remains dependency-blocked before scatter on 3 missing generated/plugin metadata DLLs under `Temp/bin/Debug`.

## 2026-05-16 Continued Pass: Compute Constant NaN Vaccination

What was wrong:
- The compute shader rejected poisoned matrices and bounds, but packed scalar constants still used direct `max` before uint casts and threshold math.
- A NaN instance count, frame index, max distance, motion strength, or crossfade scalar should not depend on backend-specific clamp behavior.

What was done:
- Added `SanitizeNonNegative` in `GpuScatterLodCull.compute`.
- Routed instance count, frame index, max distance squared, motion strength, and crossfade scalar through finite-checked resolvers.
- Preserved 64-thread `numthreads`; no groupshared or wave-specific path was added.

Cinematic cheats used:
- None added. This pass is GPU fault containment.

Exact Microseconds saved:
- No measured microseconds claimed.
- Added scalar finite checks in the compute kernel; exact GPU cost is PENDING PROFILER.
- Fault value now fails closed before append/cast/motion-vector corruption.

Validation:
- Symbol scan confirmed `SanitizeNonNegative`, `ResolveMaxDistanceSq`, `ResolveMotionStrength`, and `ResolveCrossfadeEnabled` use.
- Shader scan found no groupshared barriers, wave intrinsics, or group-memory barriers.
- Forbidden scatter-domain scan stayed clean.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.

## 2026-05-16 Continued Pass: Shared-Material Revalidation

What was wrong:
- Material variant validation cached the previous valid result and could miss a shared-material keyword mutation made by another renderer.
- That could let an invalid `HECTON_GPU_INDIRECT` or quality variant reach cull/draw after one valid frame.

What was done:
- Removed the early-return keyword cache from `IsRenderMaterialVariantValid`.
- The renderer now re-reads `HECTON_GPU_INDIRECT`, `_QUALITY_HIGH`, and `_QUALITY_MX350` every validation pass before cull dispatch.
- Cache fields remain only as last-observed telemetry for `BlackBoxFlagInvalidMaterialVariant`.

Cinematic cheats used:
- None added. This is render-state fault containment.

Exact Microseconds saved:
- No measured microseconds claimed.
- Added three material keyword checks per validation pass; exact CPU cost is PENDING PROFILER.
- Invalid variants still fail before compute dispatch and draw.

Validation:
- Forbidden scatter-domain scan found no shared-material mutation, legacy events, local private `NativeArray`, direct `H8Memory.Allocate/Release`, Unity `Update` methods, `Debug.Log`, or `string.Format`.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1` remains dependency-blocked outside scatter with 41 UI/core errors in `DiegeticGyroCompassRuntime`/`CompassStateDTO`, `ArchitectEyeVisualizer`, and `SystemDispatcher`.
- `dotnet build Assembly-CSharp.csproj --no-dependencies -m:1` remains dependency-blocked before scatter on missing `Assembly-CSharp-firstpass.dll` and `RealtimeCSG.dll`.

## 2026-05-16 Continued Pass: Companion Pass NaN Vaccination

What was wrong:
- Depth, shadow, and motion-vector vegetation passes still used raw optional VFX radius/speed lanes.
- NaN player enable flags or radii can bypass `<=` checks and poison radius-squared division or `smoothstep` thresholds.
- The lit pass had the same risk in submarine wash, interaction, impact, predator dim, and flash radius lanes.

What was done:
- Added `SanitizeNonNegativeFinite` and `SanitizePositiveFinite` to the lit, depth, shadow, and motion-vector vegetation passes.
- Guarded player interaction enable/radius/speed/push.
- Guarded interaction point speed/radius and impact radius.
- Guarded submarine wash radius/speed, predator dim radius, and flash radius in the lit pass.

Cinematic cheats used:
- Kept cheap current/interaction deformation; invalid optional VFX lanes now collapse to zero/fallback instead of disabling the visual feature globally.

Exact Microseconds saved:
- No measured microseconds claimed.
- Added scalar finite checks on optional interaction/wash branches; exact GPU cost is PENDING PROFILER.
- Fault path prevents NaN propagation into depth, shadow, and motion-vector outputs.

Validation:
- Shader scans found no raw player enable comparisons and no unsanitized radius max patterns except the already-sanitized predator dim expression.
- Shader scans found no groupshared memory, wave intrinsics, or group-memory barriers.
- Forbidden scatter-domain C# scan stayed clean.
- `git diff --check` reported only LF-to-CRLF warnings for touched files.
