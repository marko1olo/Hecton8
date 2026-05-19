# Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.

- # Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

- # Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

- # Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

- # Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

- # Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

- # Scalability Matrix

Date: 2026-05-18
Status: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R32 architecture R4/proof-wording correction is the latest artifact-backed local static DOC_GLOBAL boundary for architecture/root documentation. R31 remains the prior current-boundary propagation layer, R30 remains the prior internal-currentness layer, R29 remains the prior stale-gate/global-authority layer, R28 remains the prior interior-boundary layer, and R27 remains the latest source-counter/index snapshot until rerun.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING



## 2026-05-19 DOC_GLOBAL R32 Current Boundary Note

R32 artifact-backed reread evidence keeps this matrix as static scalability-policy orientation, not profiler, device, VRAM, or frame-time proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R32_ARCHITECTURE_R4_AND_PROOF_WORDING_LOCAL.md`; R31 remains the prior current-boundary propagation correction. R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `59` missing refs (RealtimeCSG vendor refs plus absent `VaultXRayWindow.cs` and `HectonMapMagicVegetationBridgeFloraCollisionProxies.cs`); `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owners: `DistanceMath`, `GlobalRegistry`, `GameBootstrapper`, shader keywords

## Continuous Scalability Contract

`HomeostasisBrain` now publishes the authoritative continuous scalar through `ScalabilityStateDTO`:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `GlobalQualityWeight` | `1.0` visual overkill, `0.0` minimum survival |
| 4 | `FractionalTimeSlice` | `lerp(0.1, 1.0, GlobalQualityWeight)` for smooth logic cadence |
| 8 | `VramPressure` | normalized graphics-memory pressure |
| 12 | `ThermalIndex` | normalized heat/downclock risk |

Runtime consumers must prefer this float contract over new binary quality branches. The dictator also pushes `_GlobalQualityWeight` / `_H8GlobalQualityWeight` shader globals and sends `lerp(0.5, 1.0, GlobalQualityWeight)` to `IDynamicResolutionRuntime` when the scalar changes.

The dictator owns a dedicated 300-frame telemetry ring:

| Offset | Field | Meaning |
|---:|---|---|
| 0 | `Timestamp` | Stopwatch tick |
| 8 | `RawFrameMs` | measured frame time |
| 12 | `SmoothedFrameMs` | EWMA-derived frame time |
| 16 | `GlobalQualityWeight` | current continuous quality scalar |
| 20 | `VramPressure` | normalized VRAM pressure |
| 24 | `Flags` | folded active pressure bits |
| 28 | `_pad0` | explicit 32-byte alignment padding |

Current verification: scoped static scans were reported for the scalability files, but this document does not link a fresh scan artifact. Treat the result as `PENDING VERIFICATION` until the command, timestamp, and output are recorded. Full `Hecton8.Core.csproj` and `Hecton8.Editor.csproj` builds were historically blocked outside this domain by `Assets/_Project/Scripts/PlayerBuilder.cs` missing Construction/Habitat DTOs; rerun current compile before using that blocker as live status. Profiler/Unity Play Mode capture is still pending.

## Math Precision Paths

| Path | Keyword | CPU/GPU Meaning | Target |
|---|---|---|---|
| Low | `_MATH_LOD_LOW` | cheap dominant-axis or reduced-cost approximation | weak device, far distance, overloaded frame |
| High | `_MATH_LOD_HIGH` | exact or expanded visual path | high tier, close range, stable frame |

`DistanceMath.ResolveMathLodMode(...)` chooses low/high from `HectonQualityTier`. `GameBootstrapper.WarmMathLodShaderKeywords()` pushes the initial shader state during boot.

## Shader Evidence

| File | Keyword Use |
|---|---|
| `Hecton_CoreLit.hlsl` | declares `_MATH_LOD_LOW` and `_MATH_LOD_HIGH`; mode scalar documents 0=cheap, 1=exact |
| `Hecton_AbyssalVoxelRock.shader` | skips additional lights under low math LOD |
| `Hecton_VolumetricLight.compute` | low/high compute variants |
| `TerrainMaster.shader` | low math LOD terrain branch |
| `Hecton_CoralMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_KelpMaster*.shader` | `_QUALITY_MX350` / `_QUALITY_HIGH` branch |
| `Hecton_IndirectVegetation.shader` | low-tier vegetation path |
| `Hecton_RetinaDistortion.shader` | `_QUALITY_MX350` post path |

## Tier Matrix

| Tier | CPU Math | GPU Feature Budget | Required Behavior |
|---|---|---|---|
| Low | branchless approximations, reduced sample counts | `_MATH_LOD_LOW`, `_QUALITY_MX350`, half-res where available | stable frame before fidelity |
| Middle | mixed exact/approx by distance | high only for close hero surfaces | avoid thrash; switch by budget |
| High | exact close-range math | `_MATH_LOD_HIGH`, extra lights, richer flora | visual overkill when frame budget holds |
| Ultra | high path plus density | high path plus extra density/features | spend saved cycles on presentation, not unbounded simulation |

## Visual Orgasm Matrix

Status: SOURCE DEFINED / RUNTIME PENDING
Owner: RENDER_STRATEGIST / VISUAL_LOD_GRADE_ARCHITECT

The phrase "Visual Orgasm" maps to the project rule "visual overkill on strong hardware." It is not permission for unbounded cost. Gameplay truth stays deterministic across tiers; only presentation density, residency, lighting, and post quality change.

| Axis | TOASTER / MX350 2GB | LOW / GTX 1060 | MED / RTX 2060 | RTX / RTX 3070+ | GOD_MODE / RTX 4080+ |
|---|---|---|---|---|---|
| Target | 60 FPS at 16.67 ms, 1800 MB VRAM ceiling | 60 FPS, 6 GB VRAM | 60-90 FPS, 8 GB VRAM | 120 FPS target, 8 GB+ VRAM | 144 FPS target, 16 GB+ VRAM |
| Render scale | 0.65-0.85, never below 720p internal | 0.85-1.0 | 1.0 | 1.0 with STP/TAA only if proven | 1.0-1.25 only after GPU headroom proof |
| LOD bias | 0.6, early LOD drop | 0.8 | 1.0 | 1.2 | 1.5 with VRAM guard |
| HLOD | LOD2 impostor/cards by 40 m, cull small props by 30-60 m | LOD2 by 60 m | LOD2 by 80 m | longer LOD0/1 residency near hero routes | extended hero residency plus denser near dressing |
| Hysteresis | 5 m or 3 frames minimum before switch | 5 m or 3 frames | 4 m or 3 frames | 3 m or 3 frames | 3 m or 3 frames |
| Shader LOD | LOD 100 diffuse+AO, LOD 0 unlit HLOD cards | LOD 100/200 by distance | LOD 200 default, LOD 300 near hero | LOD 300 for hero/near field | LOD 300 plus gated detail overlays |
| Fog and haze | depth-only exp fog, LUT haze, baked AO | depth fog + vertical stratification | half-res volumetrics only where budgeted | half-res volumetrics, 16-48 steps by zone | higher step count only in hero visibility cones |
| Caustics | off or baked/static lightmap | dual-layer cheap caustic, no deep zones | dual-layer + shadow mask in shallow lit zones | higher contrast and longer shallow range | hero-zone caustic volume only with profiler proof |
| Lighting | darkness volumes, emissive proxies, max 1-2 pixel lights | max 2 pixel lights, player shadow priority | max 4 pixel lights by tile | max 6-8 by tile where Forward+ stays under budget | dense proxy lights, lumen cap still enforced |
| Shadows | baked/dither proxy, 512-1024 atlas, no point shadows | 1024 atlas, 2 cascades | 2048 atlas, 2-3 cascades | 2048-4096 atlas, PCSS only priority lights | 4096 atlas, PCSS/soft shadows in hero zones |
| Materials | packed masks, shared 512 detail or disabled, mip bias +1.5 | 1024 base where visible, shared detail | 2048 hero, detail overlays on close surfaces | longer mip residency, wetness and brushed-metal fakes | GOD_MODE overrides only under VRAM < 0.90 |
| Flora/coral | impostors, VAT static fallback, global flow only | limited near-field sway | richer shader sway near camera | denser near-field dressing, VAT LOD0 | dense hero patches, static fallback beyond LOD2 |
| VFX particles | strict caps, billboard fakes, no GPU luxury path | moderate caps, no shadows | GPU compute only for selected systems | larger compute buffers, flow-reactive particles | visual storms allowed by zone budget only |
| Post FX | FXAA, ACES, vignette, minimal CA, no Bloom, no SSR | add light DoF, Bloom still off if budget tight | dual-filter Bloom, half-res SSDO, gated god rays | stronger DoF/Bloom/SSDO with frame proof | richer lens/noir stack, never at cost of frame stability |
| Occlusion | GPU Resident Drawer only where measured; stale visible | GRD for repeated MeshRenderers | GRD plus zone GPU occlusion | broad GRD and occlusion by zone | broad GRD, longer residency, no double ownership |
| VRS/foveation | OFF, unsupported until player capture proves caps | OFF by default | optional only with capability proof | optional, capability-gated | optional, capability-gated |
| Async upload | 64 MB buffer, 1 ms slice, persistent | 128 MB, 2 ms | 128 MB, 2 ms | 256 MB, 4 ms | 256 MB, 4 ms unless capture proves more |
| Demotion trigger | VRAM > 0.90 or sustained frame > 25 ms | same | same | same | same, first demote GOD_MODE overrides |

## Visual Load-Shed Order

When VRAM used/total exceeds `0.90`, demote in this order:

1. Drop GOD_MODE material overrides by one mip tier.
2. Disable MED+ detail normal overlays on non-hero surfaces.
3. Reduce non-primary render textures to 0.75 scale.
4. Increase global LOD bias cost control by 0.5.
5. Force raymarching and post effects to the next lower tier.
6. If still above threshold after 5 frames, force TOASTER render tier until pressure stays below `0.75` for 10 consecutive frames.

When sustained frame time exceeds `25 ms` for 3 frames, demote in this order:

1. Disable volumetric shadowing and caustic volume paths.
2. Halve SSDO/raymarch samples or disable them on TOASTER.
3. Cut VFX emission budgets by 50 percent outside 30 m.
4. Force distant flora/coral to static VAT/impostor.
5. Drop nonessential post to color grade, FXAA, vignette, and required underwater distortion.

Recovery is one step per 30 frames for VRAM pressure and one step per 10 stable frames for frame-time pressure. No tier may upgrade and downgrade in the same second.

## Evidence Gates

| Claim | Required proof |
|---|---|
| TOASTER stable | MX350 Player capture, Profiler, Memory Profiler, GCMonitor, Frame Debugger |
| RTX visual overkill | Player capture proving frame budget, VRAM below 0.90, no shader variant explosion |
| VRS active | `SystemInfo.foveatedRenderingCaps` or equivalent Unity capability capture plus visual artifact review |
| GPU Resident Drawer win | Frame Debugger/Rendering Stats showing SetPass, batches, and GRD path |
| Post/volumetric promotion | named profiler marker under assigned tier budget and load-shed path |

No runtime evidence exists in this document. Current status remains SOURCE DEFINED / RUNTIME PENDING.

## SHINOBU_44 Continuous Dictator Delta

Runtime authority is the continuous `GlobalQualityWeight` exported by `HomeostasisBrain`, not a tier switch. The human-readable tier table above is documentation vocabulary only; runtime consumers must treat it as curve endpoints and interpolate using the 0.0-1.0 scalar.

The dictator writes its 300-frame forensic ring through `BufferID.ShinobuScalabilityOscilloscope`. Hot-path samples are stored by `VaultBufferHandle.GetElementAsRef` into the 32-byte `ScalabilityTelemetryEntry`; `NativeArray` views are reserved for cold clear, dump, and editor oscilloscope copy. A frame over 20 ms while weight is at minimum survival triggers `Docs/AgentLogs/Dump_SCALABILITY_DICTATOR.bin`.

`MockHeavyLoadSignal.FrameSpikeMs` is applied immediately after the Stopwatch frame sample and before FPS EWMA/history updates. The fake pressure therefore flows through the same monitor, telemetry, DRS, and oscilloscope path as a real frame-time spike, and is not added a second time in the raw SHI solver. The canonical blind-test payload is 20 ms; emergency mock profiles store that value with flags disabled until the tuner or CSV arms the signal. First-time partial CSV overrides remain lane-specific: `mock_vram_pressure` does not inherit the dormant 20 ms spike unless the mock was already armed or a frame-spike lane is explicitly supplied.

The hot state writer resolves only `ShinobuScalabilitySystemHealth` and `ShinobuScalabilityState`. Mock load, mock terrain proof, CSV scratch, and telemetry each own separate handle-resolution helpers so cold/editor support buffers do not leak into the per-frame state path.

`_MATH_LOD_LOW` is retained as a shader scalar for compatibility, but it is not binary at runtime. SHINOBU_44 publishes a continuous low-weight: polynomial pressure from `GlobalQualityWeight`, polynomial pressure from `SystemHealthIndex01`, and a `math.step` survival floor below about `0.1`.

Forced quality overrides are test/tuning controls, not a second quality mode. Releasing an override must resume from the current scalar and recover through slow release; it must not reseed the controller and jump upward.

The live editor oscilloscope uses a separate sample count so cleared entries in the fixed 300-frame forensic ring are not presented as valid zero-quality samples immediately after boot.

The oscilloscope copy path also rejects invalid frame samples after both raw and smoothed lanes are checked. If neither lane is finite and positive, the graph receives the current target frame time rather than NaN or zero.

The human tuning facade is backed by `BufferID.ShinobuScalabilityTunerState`, not editor-local truth. `ScalabilityTuningDTO` is 16 bytes: offset 0 `TargetFrameMs`, offset 4 `EmergencyThreshold`, offset 8 `HysteresisReleaseFrames`, offset 12 `Flags`. Hot runtime mirrors these values into scalar fields after a tuner/CSV change.

Tuner values are finite-sanitized at every facade boundary. Invalid target frame time falls back to the contract target, invalid emergency threshold falls back to the default threshold, and invalid forced quality disables the override instead of feeding NaN into `GlobalQualityWeight`.

Public scalar and snapshot reads are also finite-sanitized. `FractionalTimeSlice` and render scale are derived from the repaired `GlobalQualityWeight` at readback time, not accepted as stale cached scalars. `TryGetHardwareDictatorSnapshot` read-repairs `SystemHealthDTO` and `ScalabilityStateDTO` in `GlobalDataVault`; `TryGetMockTerrainSamplerStatus` read-repairs the mock proof to the canonical `weight` / `1 - weight` pair. Crash dump serialization clamps invalid telemetry rows to finite fallback values and marks them with the high bit of the existing `Flags` lane (`ScalabilityTelemetryFlagSanitized`) instead of writing NaN into `.bin` / `.h8dump` evidence.

Frame-time samples that are not finite and positive are not accepted as proof of headroom. The dictator falls back to target frame time for controller, DTO, and DRS publication rather than allowing cleared `0ms` state to accelerate recovery.

Deterministic stochastic decimation has exact endpoints: `GlobalQualityWeight <= 0` executes no optional stochastic work, `>= 1` executes all optional stochastic work, and intermediate weights use strict probability comparison.

The exported stochastic threshold is saturated at the public boundary. Consumers never need to defend against cold/reset values outside `0.0-1.0`; they still must treat the scalar as a continuous probability, not a mode bit.

The 300-frame telemetry ring stores only finite positive frame samples. Invalid, zero, or negative frame-time input is replaced with the current target frame time before persistence, keeping blackbox evidence useful during boot, reset, and editor-forced transitions.

The global culling multiplier is continuous as well: it lerps from `1.0` toward the configured low multiplier using the same low-pressure curve that drives `_MATH_LOD_LOW`. Binary culling mask bits are compatibility/telemetry only.

Pressure-policy branches consume repaired scalars, not raw vault/static floats. `ApplyDictatorPressurePolicy` derives finite-safe system health and positive frame time once, then feeds emergency hysteresis, math-LOD pressure, visual-overkill promotion/revoke, culling squeeze, GC pulse policy, state DTO writes, and blackbox dump triggers from those values. Low culling multiplier and hardware SHI floor are sanitized before entering the continuous curves, so corrupt data fails toward conservative load shedding instead of false headroom.

CSV curve hot reload is an editor control surface only. Player builds do not reserve `ShinobuScalabilityCsvScratch`, do not resolve the CSV scratch buffer, and do not perform frame-path file probing for `scalability_curves.csv`. The editor facade still parses into vault-owned scratch memory, preserving designer control without importing file I/O cadence into runtime scalability decisions.

The editor tuner owns its transient leases. Closing `Continuous Scalability Tuner` during Play Mode clears forced quality, mock heavy load, and GC safe-base flags so hidden editor state cannot keep the 20 ms synthetic load active after the control surface is gone.

## Rule

Performance is currency. The low path exists to buy stable presentation on weak hardware. The high path exists to spend that currency on visible detail. Neither path may change deterministic gameplay state unless the source contract explicitly says it is presentation-only.

STATUS: SOURCE PATCHED / FULL BUILD BLOCKED OUTSIDE SCALABILITY / RUNTIME PROFILER PENDING
