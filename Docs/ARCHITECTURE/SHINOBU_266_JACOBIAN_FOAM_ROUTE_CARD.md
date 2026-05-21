# SHINOBU_266 Jacobian Foam Route Card

Date: 2026-05-21
Owner: SHINOBU_266
Owner domain: Echelon 7 Graphics & Fluid Dynamics / Visual Foam Compute
Status: PROPOSED / YELLOW / PENDING UNITY COMPILE, PROFILER, AND GPU CAPTURE
Evidence class: STATIC_SOURCE / STATIC_DOC only

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Frame Debugger, RenderGraph Viewer, shader import, GPU timestamp query, Quest run, or player build proof is claimed by this route card.

## Route Card

```text
Route ID: SHINOBU_266_JACOBIAN_FOAM_COMPUTE_VAULT_ROUTE
Date: 2026-05-21
Owner: SHINOBU_266 / JACOBIAN_FOAM_COMPUTE_GENERATOR
Owner domain: Echelon 7 Graphics & Fluid Dynamics / Visual Foam Compute
Owning file/system:
  Assets/_Project/Scripts/VFX/JacobianFoam/Hecton8.VFX.JacobianFoam.Runtime.asmdef
  Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs
  Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs
  Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs
  Assets/_Project/Scripts/VFX/JacobianFoam/Editor/Hecton8.VFX.JacobianFoam.Editor.asmdef
  Assets/_Project/Art/Shaders/Hecton_CalculateFoam.compute

Problem:
  Persistent visual foam parameters, wake injection rows, tuning rows, profile rows,
  CSV scratch, and telemetry black-box state need a relocation-safe native route
  that can be written by the visual owner phase, consumed by RenderGraph/editor
  tooling, and inspected after fault without CPU particles or GPU readback.

Why owner-local data is insufficient:
  The route is consumed across owner late-frame, RenderGraph, editor tuning,
  static scanner proof, and crash forensics. Private NativeArray fields would
  hide ownership, defrag behavior, stale-handle behavior, and telemetry capacity.

Why direct caller/owner interface is insufficient:
  RenderGraph and editor tools cross phase and assembly boundaries. A direct
  sibling-domain reference would create compile-wall risk. The route needs stable
  BufferID ownership, not concrete runtime class coupling.

Instrument:
  [x] GlobalRegistry cold service/interface
  [ ] SignalBus<T> first-party broadcast
  [ ] GlobalSignals bridge/direct queue
  [ ] HectonEventBus mod/API/cold event
  [x] GlobalDataVault / IDataVault
  [x] Black-box/telemetry route

Producer/consumer phase:
  Producer: JacobianFoamGpuRuntime cold enable resolves IDataVault and late-frame
    owner phase publishes prepared RenderGraph payload plus telemetry.
  Consumer: HectonJacobianFoamRenderFeature imports the prepared payload during
    RenderGraph record; JacobianFoamTunerWindow reads editor-only telemetry and
    writes tuning rows outside player hot paths.

Cadence/capacity:
  JacobianFoamParams: BufferID 71920, capacity 1 row, 32 bytes.
  JacobianFoamTuning: BufferID 71921, capacity 1 row, 64 bytes.
  JacobianFoamWakeImpacts: BufferID 71922, capacity 64 rows, 32 bytes each.
  JacobianFoamTelemetryRing: BufferID 71923, capacity 300 rows, 64 bytes each.
  JacobianFoamProfiles: BufferID 71924, capacity 32 rows, 64 bytes each.
  JacobianFoamCsvScratch: BufferID 71925, capacity 16384 bytes.
  JacobianFoamDumpScratch: BufferID 71926, reserved dump scratch lane.

Expected max events/reads per frame:
  One params row update, up to 64 wake rows copied to GPU, one telemetry row
  write, one prepared RenderGraph payload read. No signal fan-out.

GlobalQualityWeight behavior:
  Continuous. It controls foam resolution 512..2048, wake upload cap 8..64,
  Gerstner layer contribution weights, advection intensity, decay visibility,
  and persistent foam blend. It does not change BufferID identity, DTO layout,
  save ownership, rollback ownership, or route authority.
  Foam RTHandle storage resolves from platform support during cold/hysteresis
  allocation: R16_SFloat LoadStore+Sample preferred, R32_SFloat fallback, and
  R8_UNorm survival fallback.

Accessor purity:
  [x] No Get/TryGet/Resolve/Read API publishes signals
  [x] No Get/TryGet/Resolve/Read API syncs scene state
  [x] No Get/TryGet/Resolve/Read API allocates/grows buffers
  [x] No Get/TryGet/Resolve/Read API completes jobs
  [x] No Get/TryGet/Resolve/Read API mutates global state
  [x] No Get/TryGet/Resolve/Read API searches the scene

Payload/data shape:
  Managed fields present: no
  UnityEngine.Object fields present: no in DTOs; RenderGraph payload references
    GPU resource handles only after owner publication.
  Layout proof:
    FoamComputeParamsDTO = 32 bytes:
      offset 0  AdvectionVectors float4, 16 bytes
      offset 16 DecayAndIntensity float4, 16 bytes
    FoamWakeImpactDTO = 32 bytes:
      offset 0  LocalPositionRadius float4, 16 bytes
      offset 16 IntensityAgeFlags float4, 16 bytes
    FoamTuningDTO = 64 bytes:
      scalar lanes through Flags at offset 52, explicit pads at 56 and 60.
    FoamRenderTelemetryEntry = 64 bytes.
    FoamAestheticProfileDTO = 64 bytes.
    No Pack=1, no properties, no managed references.
  Overflow/failure:
    Wake rows clamp to capacity and excess wakes are dropped for the visual lane.
    Missing compute support or missing shader kernels disables dispatch without
    creating gameplay truth. Vault request failure keeps route inactive. Budget
    breach writes the 300-row dump to Docs/AgentLogs/Dump_SHINOBU_266.bin.

Telemetry fields:
  FrameIndex, Resolution, WakeCount, DispatchGroupsX, DispatchGroupsY,
  QualityWeight, EstimatedGpuMicros, ShorelineGain, ScrollOffsetX,
  ScrollOffsetY, StateHash, Flags, RingCursor, ProfileHash, DecayRate.

Black-box fields:
  Same as telemetry fields, fixed 300-row ring, raw dump path
  Docs/AgentLogs/Dump_SHINOBU_266.bin.

Profiler marker:
  Hecton Jacobian Foam

GC proof required:
  Unity Profiler and GCMonitor evidence showing 0 B/frame for late-frame owner
  dispatch setup and RenderGraph record path.

Shutdown/disposal:
  Runtime unregisters late-frame tickable, releases GraphicsBuffers and RTHandles,
  clears prepared payload and active texture reference. Vault lanes remain under
  DataVault lifetime ownership and are not freed by the visual leaf.

Scene unload behavior:
  OnDisable releases GPU resources and unregisters the tickable. Re-enable cold
  path resolves handles again and marks history clear for first dispatch.

Stale-handle behavior:
  Handles are generation checked through IDataVault request/resolve paths. If
  handle creation or compaction fence fails, _vaultReady remains false and no
  RenderGraph payload is published.

Rejected alternatives:
  [x] owner-local field
  [x] cached owner interface
  [x] existing SignalBus lane
  [x] existing Vault buffer
  [x] cold HectonEventBus hook
  [ ] no global route needed

Why this does not increase global monolith risk:
  The route owns only visual foam presentation/proof buffers with bounded
  capacities. It does not own wave physics truth, weather truth, propwash truth,
  save identity, rollback identity, or gameplay authority.

H-Phi impact expected:
  Neutral. The route exists for native payload ownership, relocation/fault
  proof, and editor tuning. It is not justified by H-Phi movement.

Proof required before GREEN:
  Unity import success, Unity Console compile success, compute shader import,
  RenderGraph Viewer pass proof, Frame Debugger proof of _H8JacobianFoamTexture,
  Profiler/GCMonitor 0 B/frame proof, GPU timestamp capture for Jacobian and
  advection passes, Play Mode disable/enable sweep, and forced telemetry dump
  test.

Reviewer: PENDING_INTEGRATOR
Review disposition: YELLOW
Status: PROPOSED / PENDING VERIFICATION
```

## Review Note

```text
Global authority review:
Result: YELLOW
Route ID: SHINOBU_266_JACOBIAN_FOAM_COMPUTE_VAULT_ROUTE
Owner: SHINOBU_266
Instrument: GlobalRegistry cold IDataVault discovery + GlobalDataVault buffers + telemetry ring
Producer/consumer phase: late-frame visual owner -> RenderGraph compute pass/editor diagnostics
Cadence/capacity: one params row, 64 wake rows, 300 telemetry rows, 32 profile rows, 16KB CSV scratch
Overflow/failure: wake clamp/drop, no dispatch on missing compute/vault, raw telemetry dump on budget breach
Shutdown/disposal: unregister late tick, release GraphicsBuffers/RTHandles, clear prepared payload
Proof required before GREEN: compile, import, profiler/GCMonitor, Frame Debugger, GPU timestamp, dump test
Review disposition: YELLOW
Reason: Route card is complete statically, but runtime and profiler proof are absent.
Required fixes: Run Unity verification when CPU guard permits.
Proof still missing: Unity Console, RenderGraph Viewer, Profiler/GCMonitor, GPU timestamps, player/device run.
Reviewer: PENDING_INTEGRATOR
Date: 2026-05-21
```

## 2026-05-21 Static Review Integration Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Camera-stack fence: AddRenderPasses and RecordRenderGraph reject Overlay cameras, so UI/XR overlays do not duplicate the base-camera foam compute dispatch.
RenderGraph handle hygiene: _FoamWakeImpacts is bound through the imported BufferHandle declared with builder.UseBuffer, not through an undeclared raw payload buffer.
Namespace hygiene: HectonJacobianFoamRenderFeature lives under Hecton8.VFX to match the dedicated VFX JacobianFoam asmdef island.
Vault init: JacobianFoamTuning, JacobianFoamWakeImpacts, JacobianFoamTelemetryRing, and JacobianFoamProfiles use cold ClearMemory because they can be read before an external producer writes. JacobianFoamParams and JacobianFoamCsvScratch remain UninitializedMemory because params are fully overwritten before publish and CSV scratch is cold temporary parser storage.
Read-only editor telemetry: JacobianFoamTunerWindow telemetry graph reads the ring with IDataVault.TryReadHandle. The tuning writer path still uses lock plus generation-checked resolve because it mutates exactly one owner-approved tuning row.
Fail-closed params: missing or stale mandatory params clear the prepared payload before RenderGraph can consume stale constants. Wake rows remain optional visual embellishment.
Evidence: static source/docs only. CPU guard still blocked Unity compile/import/profiler proof.
```

## 2026-05-21 Loop 24 Hardening Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Shader finite safety: Hecton_CalculateFoam.compute clamps depth samples and all UAV outputs through finite-safe helpers, wraps long-running Gerstner phase, and guards shoreline depth interpretation with UNITY_REVERSED_Z.
Continuous quality: Hecton_OceanSurfaceAtmosphere.hlsl no longer has a binary persistent-foam step gate; foam visibility is smoothstep-driven.
Vault ownership: LateFrameTick calls EnsureVaultState(false), so missing handles fail closed and no Vault buffer creation/grow occurs from the visual frame path.
Black-box dump route: telemetry budget spikes set a deferred dump flag. Raw Dump_SHINOBU_266.bin write is flushed through diagnostic/shutdown, not from per-frame telemetry recording.
RenderGraph dependency: foam generation/clear and advection are split into separate compute passes, making generation texture write/read ordering graph-visible.
Published payload bridge: RenderGraph reads a late-frame published payload/texture bridge through TryReadPublishedRenderGraphPayload instead of polling a live Active MonoBehaviour reference.
Report proof: jacobianFoam was reinserted into RENDERING_OPTIMIZATION_REPORT.json without deleting neighboring report objects after another scanner overwrite.
Evidence: static source/docs/JSON validation only. CPU guard returned 100%; Unity compile/import/profiler proof remains pending.
```

## 2026-05-21 Loop 25 XR Depth Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Depth contract: Hecton_CalculateFoam.compute uses pass-local _FoamSourceDepthTexture plus explicit _FoamSourceDepthTexture_TexelSize instead of global _CameraDepthTexture, DeclareDepthTexture.hlsl, or LoadSceneDepth.
XR boundary: single-pass texture-array XR disables only the depth-shoreline Dear Lie by zeroing the shoreline fade lane and binding RenderGraph blackTexture. Jacobian crest foam, wake circles, advection, decay, AUP wrapping, telemetry, and surface sampling remain active.
Package API proof: local package source confirms RTHandles.Alloc random-write overload, TextureHandle/BufferHandle implicit conversions, RenderGraph.AddComputePass class pass-data constraint, and IComputeCommandBuffer overloads used by the pass.
Evidence: static source/package scan only. CPU guard returned 100%; Unity compile/import/profiler proof remains pending.
```

## 2026-05-21 Loop 28 Compute Depth Correction Addendum

```text
Result: YELLOW, unchanged. Static correction only.
Local evidence: Assets/_Project/Art/Shaders/Hecton_VolumetricLight.compute explicitly documents that DeclareDepthTexture maps incorrectly on cs_5_0. The earlier Loop 25 DeclareDepthTexture approach is superseded.
Current shader route: _FoamSourceDepthTexture is declared as a normal 2D texture, dimensions are supplied by _FoamSourceDepthTexture_TexelSize from RenderGraph target metadata, and shoreline sampling exits before any depth load when fade <= 0.
Current RenderGraph route: HectonJacobianFoamRenderFeature detects XRPass.singlePassEnabled with viewCount > 1, routes depth to defaultResources.blackTexture, sets payload.WakeParams.z = 0, and still dispatches generation/advection so VR keeps Jacobian/wake foam.
Authority impact: no BufferID, DTO layout, rollback, save, Vault, or GlobalRegistry route changed.
Evidence: static source/package scan only. Unity compile/import/profiler/GPU capture remains pending behind CPU guard.
```

## 2026-05-21 Loop 30 Wake Upload Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Wake upload: UploadWakes maps the structured GraphicsBuffer and runs CopyFoamWakesToMappedBufferJob instead of copying/clearing 64 rows in C#.
Burst proof: CopyFoamWakesToMappedBufferJob has CompileSynchronously=true, FloatMode.Fast, FloatPrecision.Standard, [ReadOnly] source, and [NoAlias] source/destination arrays.
Authority impact: no BufferID, DTO layout, Vault route, rollback/save boundary, RenderGraph pass, or shader payload changed.
Scalability: active wake count still follows continuous GlobalQualityWeight; buffer capacity remains fixed at 64 for ABI stability.
Evidence: static source scan only. Unity compile/import/profiler proof remains pending behind CPU guard.
```

## 2026-05-21 Loop 26 RenderGraph Transient Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Temporary texture ownership: _HectonJacobianFoamGeneration is no longer a runtime-owned RTHandle. RenderGraph creates it as a transient TextureHandle through TextureDesc each frame and carries it from the generate pass to the advection pass.
Persistent state boundary: only the ping-pong foam history textures remain external RTHandles because they are the cross-frame visual memory required for advection and decay.
Format bridge: FoamRenderGraphPayload now carries FoamTextureFormat from the runtime platform-support resolver so the transient generation UAV matches the selected R16/R32/R8 history format.
Evidence: static source scan only. Unity RenderGraph Viewer, Memory Profiler, import, and GPU timestamp proof remain pending because CPU guard returned 74.42%, 90.63%, then 100%.
```

## 2026-05-21 Loop 29 Dispatcher Clock And Shader ABI Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Timing route: JacobianFoamGpuRuntime no longer reads Unity Time.deltaTime or Time.time. Foam visual phase/advection advances by fixed 1/60 when TimeSliceScheduler.CurrentFrameId changes. SystemDispatcher.CurrentFrameDeltaTime was rejected because it is internal to Core and would weaken the dedicated VFX asmdef boundary.
Depth ABI: _FoamSourceDepthTexture is now a normal 2D texture declaration/load route. Single-pass XR continues to bind RenderGraph blackTexture and zero shoreline fade, so no texture-array depth source is required for the disabled fake.
NaN/cast guard: wake count is finite-clamped before int conversion, and ocean hash noise sanitizes uv/time before uint2 conversion.
RenderGraph sizing: depth texel size now uses GetRenderTargetInfo(depthTexture) instead of descriptor-only metadata.
External dependency note: Plato's two-record _H8OceanWaveParameters concern was checked against ShinobuOceanSurfaceAtmosphereRuntime, which cold-allocates/uploads GraphicsBuffer[2 WaveParametersDTO].
Authority impact: no BufferID, DTO layout, rollback, save, Vault, GlobalRegistry, or quality-scaling route changed.
Evidence: static source/package scan only. Unity compile/import/profiler/GPU capture remains pending behind CPU guard.
```

## 2026-05-21 Loop 31 GPU Resource Fail-Closed Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Format support: ResolveFoamTextureFormat now returns GraphicsFormat.None when R16_SFloat, R32_SFloat, and R8_UNorm all fail LoadStore+Sample support. EnsureGpuState releases textures, clears resolution/format state, and refuses payload publication instead of attempting an unsupported R16 UAV allocation.
Generation texture: RenderGraph transient generation texture uses the already-validated payload FoamTextureFormat directly; no fallback format is injected in RecordRenderGraph.
Mapped upload: UploadParams and UploadWakes validate the selected double-buffered GraphicsBuffer before LockBufferForWrite; invalid buffers clear the active upload handle and make payload publication fail closed.
Camera route: Runtime no longer calls Camera.main. If no serialized camera is assigned, the owner phase caches GlobalRenderContext.CurrentCamera and still computes AUP wrapping through the existing origin route.
Authority impact: no BufferID, DTO layout, rollback, save, Vault, GlobalRegistry, RenderGraph pass topology, shader ABI, or continuous quality curve changed.
Evidence: static source scan only. Unity compile/import/profiler/GPU capture remains pending behind CPU guard.
```

## 2026-05-21 Loop 32 Dalton Audit Closure Addendum

```text
Result: YELLOW, unchanged. Static hardening only.
Dispatch budget: effective runtime foam resolution is capped at 1024. With the shader's 8x8 thread group, this caps the dispatch at 1,048,576 launched threads. The former 2048 single-dispatch target would launch 4,194,304 threads and is rejected until a tiled path has GPU proof.
History ownership: FoamRenderGraphPayload carries OwnerId, Sequence, and HistoryWriteIndex. Runtime publication no longer advances ping-pong state. The advect RenderGraph execution callback acknowledges the sequence, and the late-frame owner consumes that acknowledgement next frame.
Fallback publication: invalid payload/depth routes now publish RenderGraph defaultResources.blackTexture to _H8JacobianFoamTexture. This prevents stale foam from leaking into the ocean shader after fail-closed frames.
Preview bridge: the public mutable PublishedFoamTexture static was removed. Editor preview uses TryReadFoamPreviewTexture, which only returns a texture after RenderGraph acknowledgement.
Authority impact: no BufferID, DTO layout, rollback, save, Vault, GlobalRegistry, shader ABI, or signal route changed. Continuous quality still controls wave lanes, wake budget, advection, decay, and visibility; the 1024 ceiling is a compute-dispatch safety bound.
Evidence: static source scan only. Unity compile/import/profiler/GPU capture remains pending behind CPU guard.
```
