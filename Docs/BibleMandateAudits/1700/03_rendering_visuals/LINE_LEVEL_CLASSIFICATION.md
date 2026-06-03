# Rendering / Shaders / Lighting / VFX / Water Presentation Line-Level Runtime Classification

Status: LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING  
Date: 2026-06-02  
Evidence class: `STATIC_SOURCE` + `STATIC_DOC`

This file classifies all 109 static suspect lines from:

- `Docs/BibleMandateAudits/1700/03_rendering_visuals/RUNTIME_TRIAGE.md`
- `Docs/BibleMandateAudits/1700/03_rendering_visuals/RUNTIME_PRECLASSIFICATION.md`
- `Docs/BibleMandateAudits/1700/_scans/03_rendering_visuals_runtime_risks.txt`

This is not RenderGraph Viewer proof, Frame Debugger proof, GPU profiler proof, Memory Profiler proof, GC proof, player-build proof, device proof, or proof that authored render assets are wired in scenes. The system remains yellow until runtime artifacts prove the static classifications.

## Classification Summary

| Class | Count | Meaning |
|---|---:|---|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 68 | The line is inside `#if UNITY_EDITOR`, `#if UNITY_EDITOR || DEVELOPMENT_BUILD`, an editor-only `EditorWindow`/scanner/baker, or a compile-stripped `H8Debug`/conditional helper. |
| `LEGAL_COLD_PATH` | 39 | The line is owner initialization, bootstrap validation, black-box/fault dump, persistent readback storage, or DataVault/fallback storage setup. These still require boot/profiler/build proof where noted. |
| `RUNTIME_VIOLATION` | 1 registered | One production-policy mismatch remains: runtime fallback octahedron debris mesh generation. It is already covered by `RB-123`. |
| `FALSE_POSITIVE` | 1 | One line is a constant allocator definition, not an allocation or callsite. |

## Existing Blockers Still Binding This Group

- `RB-005`: sonar point-cloud/mock truth and GPU upload proof.
- `RB-006`: vegetation staging/upload/capacity proof.
- `RB-013`: sargassum fallback mesh/material/trail route proof.
- `RB-016`: blocking GPU readback waits must be teardown/fault-only or removed.
- `RB-106`: scatter/microfauna async readback and material proof.
- `RB-107`: ocean readback and water presentation proof.
- `RB-123`: runtime VFX mesh/material/RT fallback assets, including `CarveDebrisComputeRenderer` fallback octahedron mesh.
- `RB-124`: volumetric fog fallback texture/mock light lifecycle proof.
- `RB-125`: RenderFeature material lifecycle and shader assignment proof.
- `RB-127`: TBDR mock/fallback DataVault route proof.
- `RB-128`: construction preview, ambient biota, beacon, and diagnostic GPU/material fallback lifecycle proof.
- `RB-130`: buoyancy/storm/thermal visual upload and readback proof.

## Line Classification

| Source line(s) | Classification | Reason | Residual proof required |
|---|---|---|---|
| `CarveDebrisComputeRenderer.cs:2393` | `RUNTIME_VIOLATION` registered | `BuildOctahedronMesh()` calls `mesh.RecalculateNormals()` on a fallback low-poly debris mesh in runtime VFX code. This violates the generated-asset law if it is reachable as normal production presentation. | `RB-123`: authored/default debris meshes and materials, or hard release exclusion of fallback geometry; GPU/profiler proof for debris route. |
| `HectonBilateralDrsUpscalerRuntime.cs:2118` | `LEGAL_COLD_PATH` | `NativeArray<byte>(Allocator.Temp)` is in `WriteBlackBoxDump()`, a fault/export path. | Fault-trigger proof; no healthy-frame dump spam; 300-frame dump artifact. |
| `AbyssalDeferredCausticsRuntime.cs:1925` | `LEGAL_COLD_PATH` | `NativeArray<byte>(Allocator.Temp)` is in `DumpBlackBox()`, not the normal caustics render cadence. | Fault-trigger proof and no normal-frame dump route. |
| `GpuScatterLodManager.cs:1682`, `:1684` | `LEGAL_COLD_PATH` | Persistent visible-count readback arrays are allocated in owner readback storage setup, not per draw. | `RB-016`/`RB-106`: no same-frame wait, no post-bootstrap growth, readback cadence proof. |
| `AbyssalScatterBrgDataVaultBootstrap.cs:322`, `:323`, `:324`, `:486` | `LEGAL_COLD_PATH` | `Allocator.TempJob` arrays are used for cold DataVault payload read/validation and quality-map validation. | DataVault boot readiness proof; no gameplay-time bootstrap validation; leak/Dispose proof. |
| `InstanceCullingService.cs:895`, `:897` | `LEGAL_COLD_PATH` | Persistent 5-uint indirect-args readback storage is owner-owned. Release/teardown can wait; healthy gameplay must not block. | `RB-016`: no healthy-frame blocking waits; compact/high culling readback proof. |
| `TBDRPipelineSurgeonRuntime.cs:509`, `:510`, `:511`, `:512`, `:513`, `:514`, `:515`, `:516`, `:517`, `:518`, `:519` | `LEGAL_COLD_PATH` | These allocate persistent fallback/mock native buffers only when DataVault binding is unavailable. Static review classifies them as owner/fallback setup, not hot draw code. | `RB-127`: release player must prove DataVault route is ready or fallback/mock route is excluded and measured. |
| `TBDRPipelineSurgeonTypes.cs:477`, `:478`, `:479`, `:480`, `:518`, `:519`, `:520`, `:521`, `:1291`, `:1719` | `LEGAL_COLD_PATH` | Persistent TBDR budget, fallback buffer, texture-streaming slice, and telemetry-ring arrays are owner-lifetime storage. | `RB-127`: one owner, fixed capacity, DataVault readiness, shutdown/leak proof, no fallback production truth. |
| `ShinobuVoxelSculptorWindow.cs:248`, `:249`, `:250`, `:251`, `:679`, `:680`, `:681`, `:682`, `:683`, `:684`, `:685`, `:686` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The file is an editor sculptor window; TempJob arrays are authoring/tool allocations. | None for player runtime; keep sculptor editor-only. |
| `BiolumPulseSyncRuntime.cs:316`, `:318` | `LEGAL_COLD_PATH` | Persistent black-box dump snapshot arrays are allocated by a dump snapshot owner, not by every biolum pulse. | Fault dump artifact, ownership, disposal, and no healthy-frame allocation proof. |
| `ShinobuPlasmaBeamRuntime.cs:1486` | `LEGAL_COLD_PATH` | `Allocator.Temp` payload belongs to telemetry dump/export, not beam simulation cadence. | Fault-trigger proof and no normal-frame dump spam. |
| `GasDynamicsSolver.cs:1746`, `:1748` | `LEGAL_COLD_PATH` | Persistent telemetry scratch belongs to the gas solver owner. | `RB-107`/gas proof: completion windows, fixed scratch capacity, leak proof. |
| `ShinobuOceanSurfaceAtmosphereRuntime.cs:1789`, `:1791` | `LEGAL_COLD_PATH` | Persistent async wave readback arrays are owner storage for waterline queries. | `RB-107`: readback latency, no blocking wait, compact/high water capture, shutdown proof. |
| `ShinobuStormPropagationRuntime.cs:144` | `LEGAL_COLD_PATH` | Persistent native job arrays are owner solver storage. | `RB-130`: boot prewarm, completion window, fault dump, disposal proof. |
| `WeatherEvents.cs:45` | `FALSE_POSITIVE` | This is a constant allocator selector, not an allocation callsite. | None from this line; actual signal-lane capacity proof remains under runtime architecture if allocated elsewhere. |
| `LutArrayResolver.cs:501`, `:508`, `:515`, `:522`, `:529`, `:536`, `:543` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Logging uses conditional/diagnostic helpers; release calls are stripped. | Assigned LUT/shader asset proof still required; log line is not a release hot path. |
| `GlobalShaderDispatcher.cs:1394` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The allocation/log context is inside editor/development diagnostics. | Shader dispatcher still needs runtime proof separately. |
| `HectonBilateralDrsUpscalerFeature.cs:389`, `:422` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Shader fallback and diagnostic log routes are editor/development guarded. | Release shader assignment and variant proof. |
| `AbyssalScatterBrgDataVaultBootstrap.cs:606` | `LEGAL_EDITOR_OR_DEV_GUARDED` | `LogWarningCold()` is decorated with conditional editor/development attributes; direct `Debug.LogWarning` does not compile into release callsites. | DataVault boot proof still required. |
| `BiomeProfile.cs:68` | `LEGAL_EDITOR_OR_DEV_GUARDED` | `OnValidate()` correction logging is editor-only. | None for player runtime. |
| `CameraJuiceSystem_CameraJuiceBurst.cs:184` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Burst/profile validation logging is editor-only. | Camera-juice runtime profiler proof remains separate. |
| `CameraJuiceSystem.cs:861`, `:869`, `:877`, `:885`, `:893`, `:901`, `:909`, `:917`, `:925`, `:933`, `:941`, `:949`, `:957`, `:965`, `:973` | `LEGAL_EDITOR_OR_DEV_GUARDED` | All listed logging helpers are editor/development gated or conditional diagnostic routes. | Camera stack visual/profiler proof remains required. |
| `HectonMarineSnowRenderer.cs:2845`, `:2853`, `:2861`, `:2869`, `:2877`, `:2885` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Marine-snow diagnostics use editor/development guarded logging. | `RB-123`: authored resources, fixed RT/buffer counts, GPU proof. |
| `BiolumPulseSyncRuntime.cs:1472` | `LEGAL_EDITOR_OR_DEV_GUARDED` | ABI/layout error logging uses the compile-stripped `H8Debug` facade. | DTO layout proof still required; log line is release-stripped. |
| `ShakeProfile.cs:52` | `LEGAL_EDITOR_OR_DEV_GUARDED` | `OnValidate()` logging is editor-only. | None for player runtime. |
| `ParasiteSwarmGpuRuntime.cs:130` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Layout/diagnostic error uses the compile-stripped `H8Debug` facade. | GPU swarm buffer proof remains separate. |
| `HectonGIRelaySystem.cs:966` | `LEGAL_EDITOR_OR_DEV_GUARDED` | GI relay diagnostics are editor/development gated. | Lighting relay capture/profiler proof remains required. |
| `HectonLightingRuntime_DayNightRelay.cs:608` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Day/night relay warning is editor-only. | Runtime lighting proof remains required. |
| `InteriorGIProbeVolumeRuntime.cs:1693`, `:1699`, `:1728`, `:1732`, `:1813` | `LEGAL_EDITOR_OR_DEV_GUARDED` | CSV reload/dump logs are editor/development or `H8Debug` diagnostic routes. | GI/probe volume runtime proof remains required. |
| `BaseAtmosphereLogisticsEditor.cs:28`, `:146`, `:156` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The file is editor-only; UI status/log lines cannot enter player runtime. | None for player runtime. |
| `ShinobuAtmosphereWaveTunerWindow.cs:150` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Editor tuner UI label. | None for player runtime. |
| `SurfaceWeatherVfxRig.cs:215`, `:252` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Diagnostics are editor/development gated. | Weather VFX runtime capture/proof remains required. |
| `ShinobuStormPropagationContracts.cs:722` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Contract/validation logging is editor-only. | Storm runtime proof remains under `RB-130`. |
| `WeatherEvents.cs:402` | `LEGAL_EDITOR_OR_DEV_GUARDED` | Listener exception logging uses a conditional helper. | Event-lane capacity and listener policy proof remains outside this log line. |
| `HectonSeismicTideDirector.cs:2028`, `:3857`, `:5362`, `:5410` | `LEGAL_EDITOR_OR_DEV_GUARDED` | The listed logs/status strings are editor/development diagnostics or editor tuner UI. | Seismic/tide runtime proof remains separate. |

## Current System Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

All 109 listed rendering/visual static suspect lines are now classified. This does not clear rendering for release. The static audit still leaves real acceptance work: RenderGraph/Frame Debugger captures, GPU and VRAM profiler captures, material/SRP batching proof, shader assignment and variant proof, authored fallback asset proof, DataVault/TBDR readiness proof, async readback latency proof, no healthy-frame blocking readback, fixed resource lifetime proof, and compact/high visual captures.

