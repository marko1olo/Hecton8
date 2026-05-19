# Rationale_SHINOBU_68

## 2026-05-19 DRS Lane Reassertion

Problem: `CURRENT_BATCH.md` contains duplicate `SHINOBU_68` XML blocks, and disk memory was overwritten by the procedural-bone duplicate. The active user request is DRS/TAA/PostProcess: TargetRenderScale smoothness, ARM64 DRS layout, and URP Pipeline Asset optimization.
Solution: Treat the first `SHINOBU_68` block, `role="DYNAMIC_RESOLUTION_AND_FSR_DIRECTOR"`, as authoritative for this pass. Restore status/rationale/reporting to DRS and reject the later procedural duplicate for this request.
Rejected Alternatives: Mixing procedural-bone evidence into a DRS audit, trusting stale agent memory, or editing animation ownership files.
Scalability potential: Low uses minimum render scale plus TAA/bilinear/mobile cull; Middle restores selected post effects; High uses FSR sharpen and smoother scale recovery; Ultra spends saved fillrate on shader richness rather than fixed-resolution brute force.
Hardware Impact: 0 us runtime; prevents wrong-domain compile churn and false report routing.

## 2026-05-19 TargetRenderScale And URP Asset Polish

Problem: A DRS controller can become visually hostile if `TargetRenderScale` snaps, or if URP assets use a mismatched upscale mode. Raw resolution drops are visible and destabilizing in VR.
Solution: Keep `TargetRenderScale = math.lerp(MinScaleLimit, 1.0f, GlobalQualityWeight)` and apply EWMA smoothing to `CurrentRenderScale`, with panic drop only for hard frame-time spikes. PC URP assets use FSR sharpness override; Mobile and Quest retain the cheaper bilinear/TAA-compatible path.
Rejected Alternatives: `Screen.SetResolution`, per-frame `RenderTexture` allocation, binary quality switches, or universal FSR on weak mobile ALU.
Scalability potential: Low can fall toward 0.5-0.6 internal scale while preserving display resolution; Middle breathes between quality weights without popping; High/Ultra use reconstruction and sharpness to hide missing pixels.
Hardware Impact: Fillrate reduction is proportional to scale squared; exact microseconds require Unity Profiler/Frame Debugger capture.

## 2026-05-19 ARM64 DTO And Vault Audit

Problem: Quest-class ARM64 hardware punishes packed or misordered DTOs, and DRS state must be memcpy-safe for telemetry/rollback-style blackbox capture.
Solution: `DrsStateDTO` stays a 16-byte sequential structure: `float CurrentRenderScale`, `float TargetRenderScale`, `uint UpscalerTypeHash`, `uint _pad0`. `ResolutionScaleState` stays `[StructLayout(LayoutKind.Explicit, Size = 64)]` to isolate the shared scale state in one cache line.
Rejected Alternatives: `Pack=1`, bool fields, properties on hot structs, or managed state containers.
Scalability potential: All tiers read the same lightweight state layout; quality variation changes math, not memory shape.
Hardware Impact: Prevents unaligned access traps and false-sharing risk on the shared scale state.

## 2026-05-19 Cached Post-Processing Survival Gate

Problem: SSDO, half-res particles, and scooter volumetric shafts each queried DRS state separately. That duplicated concrete service polling and risked stale static cache behavior when Unity runs without domain reload.
Solution: Route all three features through `HectonDrsRenderFeatureGate.ShouldCullForSurvivalScale()`. The helper caches `IResolutionScalerService` but clears it via `RuntimeInitializeOnLoadMethod(SubsystemRegistration)`.
Rejected Alternatives: Per-feature `GlobalRegistry.ResolutionScaler` calls, direct sibling-domain references, or hard-coded low/high platform flags.
Scalability potential: Low culls heavy post effects when STP is active and scale is <= 0.6001; Middle/High/Ultra retain effects as render scale recovers.
Hardware Impact: Saves repeated service lookup and, more importantly, enables last-resort GPU post stack shedding under thermal pressure.

## 2026-05-19 Compile And Static Verification

Problem: Full project builds are expensive under parallel-agent load, and the user explicitly forbids launching `dotnet build` until needed. At the same time, DRS changes need objective evidence.
Solution: Use scoped Roslyn csc only. `Hecton8.Graphics.Scalability` and `Hecton8.Core.Contracts` pass. The helper's `Hecton8.Core` scoped check is blocked by unrelated pre-existing missing dependencies in Construction, Babel, Networking, Modding, and VolcanicUpdraft lanes; the helper itself emits no diagnostic before that wall.
Rejected Alternatives: Whole-solution `dotnet build`, claiming Unity runtime proof from static scans, or hiding the compile wall.
Scalability potential: No runtime behavior change from verification strategy.
Hardware Impact: Developer hardware protected; compile wall contained to unrelated owners.

## 2026-05-19 GlobalQualityWeight ABI Compile-Wall Guard

Problem: `ThermalDynamicResolutionAdapter` directly called `HomeostasisBrain.GlobalQualityWeight`, but the current Bee `Hecton8.Core.ref.dll` is stale and does not expose that source-defined property. Scoped `Hecton8.Graphics.Scalability.rsp` therefore failed even though `HomeostasisBrain.ScalabilityDictator.cs` defines the property.
Solution: Read the already-published `_H8GlobalQualityWeight` and `_GlobalQualityWeight` shader scalars, sanitize them, and store the continuous value in `ResolutionScaleState.GlobalQualityWeight01`. This preserves the GlobalQualityWeight continuum without forcing a Core rebuild under parallel-agent compile pressure.
Rejected Alternatives: Running full `dotnet build`, mutating Core public ABI again, using reflection, or falling back to binary platform switches.
Scalability potential: Low can still drop continuously through quality 0..0.3; Middle/High/Ultra recover through the same scalar without an if-low-end branch.
Hardware Impact: One pair of scalar shader-global reads per DRS tick; no managed allocation and scoped Graphics csc is green.

## 2026-05-19 Vault Offset-Zero Quality Read Repair

Problem: A later source pass reintroduced `state[0].GlobalQualityWeight`, which fails against the stale `ScalabilityStateDTO` metadata in `Hecton8.Core.ref.dll` even though the source struct defines `GlobalQualityWeight` at byte offset 0.
Solution: Resolve `BufferID.ShinobuScalabilityState` through `VaultBufferHandle<ScalabilityStateDTO>.ResolvePointer`, read the first 4 bytes as a float, and only fall back to the published shader globals when the vault handle is missing or invalid.
Rejected Alternatives: Full `dotnet build`, Core ABI mutation, reflection, direct source-only property access, or binary low/high hardware switches.
Scalability potential: Low reads the same continuous quality scalar and can collapse to the tier floor; Middle/High/Ultra recover through the same value and retain smooth DRS without a platform branch.
Hardware Impact: One aligned float load from a 16-byte source-defined DTO at offset 0; no managed allocation and scoped Graphics csc is green.

## 2026-05-19 Mutable DRS State Backdoor Removal

Problem: `GetMutableDrsState()` exposed a mutable `ref DrsStateDTO`, allowing external code to bypass the DRS solver and corrupt render-scale telemetry.
Solution: Replace the public mutable ref API with `GetDrsStateReadOnly()`. Internal mutation remains vault-backed through explicit pointer writes.
Rejected Alternatives: Keeping the mutable ref and trusting callers, or wrapping the DTO with hot-path properties.
Scalability potential: All tiers keep one authoritative state writer, so thermal scale response stays deterministic.
Hardware Impact: 0 us runtime; removes a correctness hazard.

## 2026-05-19 Blackbox Dump Path Cold Prebind

Problem: Fault dumping built `Docs/AgentLogs/Dump_DRS_SURGEON.bin` path inside the dump path, adding unnecessary managed path work exactly when the renderer is already faulting.
Solution: Resolve and create the log directory once in `Awake`, cache the dump path, and use only the cached path when a fault dump is triggered.
Rejected Alternatives: Per-fault `Directory.GetParent`, repeated `Path.Combine`, or silent dump disable.
Scalability potential: No visual tier difference; this is fault-path determinism.
Hardware Impact: Removes managed path construction from the fault path; gameplay hot path unchanged.

## 2026-05-19 Pixel-Stable TargetRenderScale Polish

Problem: A mathematically smooth render-scale value can still cause visible TAA shimmer if it lands on arbitrary fractional pixel boundaries each frame.
Solution: Apply EWMA first, then snap the resulting render scale to a 2-pixel dominant-axis grid. This keeps the scale continuous at player-visible cadence while preventing subpixel-scale jitter from feeding TAA.
Rejected Alternatives: Integer render-scale buckets, binary low/high switches, or leaving raw fractional drift.
Scalability potential: Low still collapses smoothly toward tier floor; Middle/High/Ultra recover in small pixel-stable increments rather than hard steps.
Hardware Impact: Two `Screen` scalar reads plus constant math per tick; no allocation or render-target creation.

## 2026-05-19 TAA Sharpen Ringing Guard

Problem: Pure inverse-scale sharpening can ring aggressively when GlobalQualityWeight is low and reconstruction already carries temporal history noise.
Solution: Blend smooth linear deficit with inverse deficit, then damp the final sharpen scalar by the sanitized quality weight. DearLie still hides missing pixels, but low-quality collapse avoids brittle edge halos.
Rejected Alternatives: Flat sharpening, raw inverse-scale sharpening, or disabling reconstruction at all subnative scales.
Scalability potential: Low retains enough sharpen to read silhouettes; Middle restores stronger reconstruction; High/Ultra reduce sharpening as native scale returns and spend visual budget elsewhere.
Hardware Impact: Small scalar math; no texture, RT, or post-volume allocation.

## 2026-05-19 URP Pipeline Asset Bandwidth And Quest Drift Repair

Problem: Live URP assets drifted from the DRS/TAA plan. `URP_Quest_VR.asset` had depth texture, opaque texture, HDR, and MSAA-off state re-enabled, contradicting the Quest Vulkan configurator and forcing extra tile-memory resolves on the thermally constrained target. Low/Medium/High PC and Mobile assets also left store actions on `Auto`, which can retain pass targets when the RenderGraph path is already declared by first-party features.
Solution: Restore Quest to depth/opaque/HDR off, MSAA x4, bilinear upscaling, no FSR sharpness override, and Discard store actions. Set Low/Medium/High PC plus Mobile `m_StoreActionsOptimization` to Discard. Add explicit Quest configurator guards for `m_UpscalingFilter = 1` and `m_FsrOverrideSharpness = false` so generated Quest assets cannot inherit future FSR settings from the Mobile source asset.
Rejected Alternatives: Mutating screen resolution, adding runtime render textures, trusting stale asset defaults, or pushing FSR compute onto Quest/mobile ALU.
Scalability potential: Low/Mobile/Quest shed bandwidth through Discard store actions and bilinear/TAA-compatible upscaling; Middle/High keep FSR reconstruction and richer post-processing; Ultra retains visual overkill through existing shader globals without changing the runtime DTO layout.
Hardware Impact: Static GPU bandwidth reduction only. Quest depth/opaque/HDR removals avoid known extra color/depth resolves, and Discard store actions reduce render-target store bandwidth; exact microseconds require Unity Profiler/Frame Debugger on target hardware. No gameplay GC or DRS hot-path CPU change.

## 2026-05-19 Shader Quality Fallback Pressure Merge Repair

Problem: `TryReadPublishedShaderQualityWeight` merged `_H8GlobalQualityWeight` and `_GlobalQualityWeight` with `math.max`. Because lower quality means more thermal/frame pressure, a stale/default `1.0` on either global could mask a real `0.2` collapse on the other global and keep `TargetRenderScale` too high.
Solution: Treat each shader global as valid only when finite and positive, then choose the lowest valid weight. If only one channel is valid, use it. This keeps the compile-wall fallback alive while making the pressure merge pessimistic in favor of frame survival.
Rejected Alternatives: Keeping optimistic `math.max`, deleting the shader fallback, forcing a Core rebuild to read the source property, or adding another managed service dependency.
Scalability potential: Low/Quest now honors either fallback channel when it requests survival scale; Middle/High/Ultra still recover through the same continuous scalar when both channels publish high values.
Hardware Impact: Same two `Shader.GetGlobalFloat` scalar reads as before; no allocation, no extra vault buffer, no render target churn. Expected gain is avoided over-rendering during fallback-only thermal pressure.
