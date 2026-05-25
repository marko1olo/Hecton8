# SHINOBU_266 Jacobian Foam Route Card

Date: 2026-05-21
Owner: SHINOBU_266
Domain: Echelon 7 Graphics & Fluid Dynamics / Visual Foam Compute
Status: PROPOSED / YELLOW / PENDING UNITY COMPILE, PROFILER, GPU CAPTURE
Evidence class: STATIC_SOURCE / STATIC_DOC

Evidence limit: no Unity import, Console, Play Mode, profiler, GCMonitor, Frame Debugger, RenderGraph Viewer, shader import, GPU timestamp, Quest run, or player build proof.

## Route

| Field | Value |
|---|---|
| Route ID | `SHINOBU_266_JACOBIAN_FOAM_COMPUTE_VAULT_ROUTE` |
| Owner | `SHINOBU_266 / JACOBIAN_FOAM_COMPUTE_GENERATOR` |
| Problem | Persistent visual foam params, wake rows, tuning rows, profile rows, CSV scratch, telemetry |
| Need | Visual owner writes; RenderGraph/editor consume; fault inspection reads telemetry |
| Rejected cost | CPU particles; GPU readback |
| Instrument | `GlobalRegistry` cold service/interface; `GlobalDataVault` / `IDataVault`; black-box telemetry |
| Not used | `SignalBus<T>` fan-out; `GlobalSignals`; `HectonEventBus` |
| Reviewer | `PENDING_INTEGRATOR` |
| Disposition | `YELLOW`; runtime/profiler proof absent |

## Source Anchors

- `Assets/_Project/Scripts/VFX/JacobianFoam/Hecton8.VFX.JacobianFoam.Runtime.asmdef`
- `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamContracts.cs`
- `Assets/_Project/Scripts/VFX/JacobianFoam/JacobianFoamGpuRuntime.cs`
- `Assets/_Project/Scripts/VFX/JacobianFoam/HectonJacobianFoamRenderFeature.cs`
- `Assets/_Project/Scripts/VFX/JacobianFoam/Editor/Hecton8.VFX.JacobianFoam.Editor.asmdef`
- `Assets/_Project/Art/Shaders/Hecton_CalculateFoam.compute`

## Ownership Boundary

| Question | Decision |
|---|---|
| Owner-local fields | Rejected: would hide ownership, defrag, stale-handle, and telemetry capacity behavior |
| Direct owner interface | Rejected: RenderGraph/editor cross phase and asmdef boundaries |
| Global monolith risk | Bounded visual foam buffers only; no wave physics, weather, propwash, save, rollback, or gameplay authority |
| H-Phi impact | Neutral; route exists for native payload ownership, relocation proof, fault proof, editor tuning |

## Phases

| Phase | Owner action |
|---|---|
| Cold enable | `JacobianFoamGpuRuntime` resolves `IDataVault` |
| Late-frame owner | Publishes prepared RenderGraph payload plus telemetry |
| RenderGraph record | `HectonJacobianFoamRenderFeature` imports prepared payload |
| Editor only | `JacobianFoamTunerWindow` reads telemetry and writes tuning rows outside player hot paths |

## Vault Buffers

| BufferID | Name | Capacity | Row size |
|---:|---|---:|---:|
| `71920` | `JacobianFoamParams` | `1` row | `32` bytes |
| `71921` | `JacobianFoamTuning` | `1` row | `64` bytes |
| `71922` | `JacobianFoamWakeImpacts` | `64` rows | `32` bytes |
| `71923` | `JacobianFoamTelemetryRing` | `300` rows | `64` bytes |
| `71924` | `JacobianFoamProfiles` | `32` rows | `64` bytes |
| `71925` | `JacobianFoamCsvScratch` | `16384` bytes | byte scratch |
| `71926` | `JacobianFoamDumpScratch` | reserved | dump scratch |

Per-frame budget:

- params update: `1` row;
- wake GPU upload: `0..64` rows;
- telemetry write: `1` row;
- RenderGraph payload read: `1`;
- signal fan-out: `0`.

## Continuous Quality

| Control | Range / rule |
|---|---|
| Foam resolution | quality-scaled; effective runtime cap `1024` until tiled GPU proof |
| Former `2048` target | rejected: single dispatch would launch `4,194,304` threads |
| Wake upload cap | `8..64` rows |
| Shader controls | Gerstner weights, advection, decay, blend |
| True identity | BufferID, DTO layout, save ownership, rollback ownership, route authority unchanged |
| Texture format | `R16_SFloat` LoadStore+Sample preferred; `R32_SFloat` fallback; `R8_UNorm` survival fallback; `None` fail-closed |

## DTO Layout

| DTO | Size | Layout facts |
|---|---:|---|
| `FoamComputeParamsDTO` | `32` bytes | `AdvectionVectors` `float4` at `0`; `DecayAndIntensity` `float4` at `16` |
| `FoamWakeImpactDTO` | `32` bytes | `LocalPositionRadius` `float4` at `0`; `IntensityAgeFlags` `float4` at `16` |
| `FoamTuningDTO` | `64` bytes | scalar lanes through `Flags` at `52`; explicit pads at `56` and `60` |
| `FoamRenderTelemetryEntry` | `64` bytes | telemetry ring row |
| `FoamAestheticProfileDTO` | `64` bytes | profile row |

Payload law:

- DTO managed fields: none;
- `UnityEngine.Object` fields in DTOs: none;
- `Pack=1`: none;
- properties in DTOs: none;
- RenderGraph payload may reference GPU resource handles only after owner publication.

## Accessor Purity

- no `Get*` / `TryGet*` / `Resolve*` / `Read*` API publishes signals;
- no scene sync;
- no buffer allocation/grow;
- no job completion;
- no global mutation;
- no scene search.

## Failure Modes

| Failure | Behavior |
|---|---|
| Wake overflow | clamp to `64`; drop excess visual wakes |
| Missing compute/shader kernel | disable dispatch; gameplay truth unchanged |
| Vault request failure | route inactive |
| Missing/stale mandatory params | clear prepared payload before RenderGraph consumption |
| Unsupported graphics format | release textures, clear state, refuse payload publication |
| Invalid upload buffer | clear upload handle; publication fails closed |
| Invalid payload/depth route | publish RenderGraph `defaultResources.blackTexture` to `_H8JacobianFoamTexture` |
| Budget breach | request `Docs/AgentLogs/Dump_SHINOBU_266.bin` |

## Telemetry

Fields:

- `FrameIndex`, `Resolution`, `WakeCount`, `DispatchGroupsX`, `DispatchGroupsY`;
- `QualityWeight`, `EstimatedGpuMicros`, `ShorelineGain`;
- `ScrollOffsetX`, `ScrollOffsetY`;
- `StateHash`, `Flags`, `RingCursor`, `ProfileHash`, `DecayRate`.

Black-box:

- ring: `300` rows;
- dump path: `Docs/AgentLogs/Dump_SHINOBU_266.bin`;
- profiler marker: `Hecton Jacobian Foam`.

## Lifecycle

| Event | Required action |
|---|---|
| Shutdown | unregister late-frame tickable; release `GraphicsBuffer` and `RTHandle`; clear prepared payload and active texture reference |
| Scene unload | `OnDisable` releases GPU resources and unregisters tickable |
| Re-enable | cold path resolves handles; history marked clear before first dispatch |
| Stale handle | generation check through `IDataVault`; failed handle/compaction fence keeps `_vaultReady=false` |

## Static Hardening Log

| Loop | Result | Facts preserved |
|---|---|---|
| Review | `YELLOW` | complete static route card; missing Unity Console, RenderGraph Viewer, Profiler/GCMonitor, GPU timestamps, player/device run |
| 2026-05-21 integration | `YELLOW` | overlay cameras rejected; `_FoamWakeImpacts` uses declared `BufferHandle`; VFX namespace/asmdef island; cold clear for readable-before-write lanes; params/CSV scratch uninitialized by design; editor telemetry read-only path |
| Loop 24 | `YELLOW` | finite-safe compute shader; smooth foam visibility; no Vault grow from frame path; deferred dump; split generate/advection passes; published payload bridge; JSON report object restored |
| Loop 25 | `YELLOW` | pass-local `_FoamSourceDepthTexture`; XR disables shoreline Dear Lie only; package API support checked |
| Loop 28 | `YELLOW` | `DeclareDepthTexture` approach superseded; normal 2D depth texture plus texel size; XR black texture and `WakeParams.z=0`; no authority change |
| Loop 30 | `YELLOW` | `UploadWakes` maps `GraphicsBuffer`; `CopyFoamWakesToMappedBufferJob`; Burst sync compile and `[NoAlias]`; wake capacity remains `64` |
| Loop 26 | `YELLOW` | transient generation `TextureHandle`; ping-pong history stays external; payload carries selected foam format |
| Loop 29 | `YELLOW` | no Unity `Time`; fixed `1/60` on `TimeSliceScheduler.CurrentFrameId`; normal 2D depth ABI; finite wake count; `GetRenderTargetInfo`; no authority change |
| Loop 31 | `YELLOW` | unsupported formats return `GraphicsFormat.None`; generation texture uses validated format; upload buffers validated before lock; no `Camera.main`; cached `GlobalRenderContext.CurrentCamera` |
| Loop 32 | `YELLOW` | dispatch cap `1024`; payload carries `OwnerId`, `Sequence`, `HistoryWriteIndex`; RenderGraph acknowledgement consumed next frame; stale foam fails to black texture; preview via `TryReadFoamPreviewTexture` |

## Proof Required Before GREEN

- Unity import success;
- Unity Console compile success;
- compute shader import;
- RenderGraph Viewer pass proof;
- Frame Debugger proof for `_H8JacobianFoamTexture`;
- Profiler/GCMonitor `0 B/frame` for late-frame owner and RenderGraph record;
- GPU timestamp capture for Jacobian/advection passes;
- Play Mode disable/enable sweep;
- forced telemetry dump test.
