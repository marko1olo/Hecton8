# Rationale_CAUSTICS_PROJECTION_ENGINEER

Status: PENDING VERIFICATION

## Session Init
Problem: Existing caustics directive reports texture/projector-style caustics that ignore wave data, depth, shadows, AUP shifts, and quality tiers.
Solution: Build an analytical GPU-owned caustics subsystem with GlobalRegistry service registration, VISUAL_SYNC execution, AUP-safe projection, low-tier shader fallback, depth gate, and telemetry state.
Rejected Alternatives: Unity Projector/DecalProjector and per-object material overrides burn fill-rate or break batching; CPU ray-style simulation violates the 0.1 ms suspicion threshold.
Scalability potential: Low disables compute and uses fragment Voronoi; Middle uses 512 map with cheap derivatives; High increases visual response through chromatic split and stronger shadow/depth masking; Ultra can raise dispatch cadence/detail if verified.
Hardware Impact: Expected low-end gain is from deleting projector fill-rate and disabling compute on MX350/i3; exact gain remains PENDING VERIFICATION until Unity profiler data exists.

## Loop 1 Core Wiring
Problem: Core bootstrap cannot directly reference a new graphics asmdef without creating a Core -> Graphics -> Core cycle.
Solution: Added `ICausticsService` to the existing Core registry contracts and used a cold-path `GameBootstrapper` reflection bridge to instantiate `Hecton8.Graphics.Caustics.AnalyticalCausticsService`; the service itself owns the `Hecton8.Graphics.Caustics` asmdef and registers through `GlobalRegistry`.
Rejected Alternatives: Direct Core asmdef reference to Graphics.Caustics would produce a circular dependency; a local singleton would violate the explicit GlobalRegistry mandate.
Scalability potential: Low/MX350 path skips compute and keeps fragment procedural caustics; Mid/High/Ultra use the 512 map and retain chromatic split from the saved projector fill-rate.
Hardware Impact: Low-end avoids the compute dispatch and old projector-style fill-rate. Expected hot dispatch managed allocation remains 0 bytes because buffers are persistent and `LateFrameTick` uses cached IDs and NativeArray scratch.

Problem: Ocean Gerstner data was private to `HectonFluidEngine`, so caustics could only read stale shader globals.
Solution: Added `BufferID.OceanGerstnerWaves` and `OceanGerstnerWaveMeta`; `HectonFluidEngine` publishes its 16-wave `NativeArray` copy into `GlobalDataVault`, and caustics reads that buffer first with shader-global fallback only when the vault is unavailable.
Rejected Alternatives: Per-frame `TrySampleWaveKinematics` calls would move the math to CPU and create direct service dependency; a duplicate authored wave set would drift from buoyancy.
Scalability potential: Low skips the data on the GPU; Mid reads 8-ish active waves; High/Ultra can consume all 16 waves without shader feature churn.
Hardware Impact: The shared 16-wave copy is 512 bytes plus 16-byte meta, cheaper than CPU refraction or projector overdraw; exact microsecond impact remains PENDING VERIFICATION.

Problem: Analytical caustics need refraction feel without ray tracing or trig-heavy Snell math.
Solution: Compute shader derives Gerstner gradients with `sincos`, perturbs RGB sample positions from the slope vector, and outputs `R8G8B8A8_UNorm`; no `asin` or `acos` are used.
Rejected Alternatives: Real ray marching or ray intersection through the water volume is prohibited for the frame budget; Unity Projector/DecalProjector was not used.
Scalability potential: Low fragment fallback, Mid single 512 pass, High/Ultra stronger chromatic response and higher wave count through existing quality tier.
Hardware Impact: 512x512 with 8x8 thread groups is 4096 groups; MX350 skips this path entirely.

## OMEGA POLISH CHANGES
Problem: OMEGA audit found direct utility math and cold reflection bloat in the caustics-owned path: HLSL `normalize`/`sqrt`, C# `math.normalizesafe`, and `Marshal.SizeOf` for a known two-Vector4 GPU struct.
Solution: Replaced HLSL direction normalization and phase velocity with explicit `rsqrt` math, replaced C# direction normalization with `NormalizeDirectionFast`, and replaced `Marshal.SizeOf<CausticsWaveGpuData>()` with `WaveGpuStrideBytes = 32`. Static scan after the patch found no direct `sqrt(`, `normalize(`, `asin`, `acos`, `math.normalize`, `Marshal.SizeOf`, `foreach`, `string.Format`, interpolated strings, or `.ToString()` in caustics-owned hot code.
Rejected Alternatives: Keeping library helpers was convenient but opaque; real Snell/ray marching remains rejected because it spends frame time on realism the player cannot control. LUT-only caustics was rejected for Mid/High because the task explicitly requires reading live Gerstner wave data.
Scalability potential: Low/MX350 disables compute and falls back to fragment procedural caustics. Middle dispatches one 512 map. High keeps all 16 wave derivatives and RGB split. Ultra can spend saved projector/fill-rate budget on stronger wave count cadence or larger projection only after profiler proof.
Hardware Impact: Low-end i3/MX350 avoids the 4096-group compute dispatch entirely. Mid/High remove one direct normalize and one direct sqrt-style op per wave evaluation in the compute kernel; exact microsecond savings are PENDING because Unity is blocked by unrelated compile errors.

Problem: The work touched shared Core, Bootstrap, Fluid, Shader, and Visor files while multiple agents were active.
Solution: Cross-domain edits were limited to hard interfaces required by the prompt: `GlobalRegistry`/contracts for `ICausticsService`, `GameBootstrapper` for service creation, `HectonFluidEngine` for Gerstner wave publication to `GlobalDataVault`, `Hecton_CoreLit.hlsl` for material consumption, and `CausticsProjectorManager` for legacy path suppression.
Rejected Alternatives: Duplicating wave data inside Graphics.Caustics would drift from buoyancy; direct Core->Graphics references would create an asmdef cycle; deleting the old manager could break prefab GUID references.
Scalability potential: The interfaces preserve decoupling so Low can skip compute without disabling shader fallback, while High/Ultra can consume the same wave source as ocean motion.
Hardware Impact: Shared data publication is a 16-wave NativeArray copy plus meta when waves change; projected runtime gain comes from avoiding legacy projector/fill-rate and per-renderer material work. Exact profiler data remains blocked.

Final Git Diff: Shared-file diff stat contains concurrent modifications from other agents in `GameBootstrapper.cs`, `GlobalRegistry.cs`, `GlobalRegistryContracts.cs`, and `HectonFluidEngine.cs`; caustics-owned new files are `Assets/_Project/Scripts/Graphics/Caustics/AnalyticalCausticsService.cs`, `Assets/_Project/Scripts/Graphics/Caustics/Hecton8.Graphics.Caustics.asmdef`, and `Assets/_Project/Art/Shaders/Hecton_CausticsGenerator.compute`. Current targeted stat for tracked touched files: `Hecton_CoreLit.hlsl` 24 lines, `GameBootstrapper.cs` 121 lines, `GlobalRegistry.cs` 251 lines, `GlobalRegistryContracts.cs` 311 lines, `H8Memory.cs` 18 lines, `HectonFluidEngine.cs` 835 lines, `CausticsProjectorManager.cs` 10 lines.

## Loop 7 Re-Audit Upgrade
Problem: The first analytical pass still spent too much GPU math on chromatic aberration by running the full Gerstner derivative loop three times per pixel. Low/Unknown tier also still paid cold compute resource allocation even when dispatch was disabled.
Solution: Reduced compute to one Gerstner solve per pixel, then derives RGB split from the resulting refraction vector with cheap triangle-wave edge weights. Added Math LOD wave caps: Unknown/Low/MX350 dispatch 0 and release compute resources; Mid dispatches up to 8 waves; High up to 12; Ultra up to 16. Compute buffers and the 512 `R8G8B8A8_UNorm` RT now allocate lazily only when a dispatch-capable tier reaches the visual sync tick.
Rejected Alternatives: Three full Gerstner solves per pixel were rejected as 3x math cost for a color fringe. A smaller Low-tier compute texture was rejected because Low/MX350 already has a fragment fallback and the project mandate prioritizes predictable cheap behavior on weak hardware.
Scalability potential: Low/Unknown = zero compute and no caustics RT; Middle = 512 map with 8 waves; High = 512 map with 12 waves; Ultra = 512 map with all 16 waves and chromatic overkill. The shader fallback still supplies motion when compute is disabled.
Hardware Impact: MX350/i3 saves the entire compute dispatch plus 1 MB RT allocation and wave upload buffer. Mid/High save two full 512x512 wave-loop passes per frame compared with the first implementation; exact microseconds remain PENDING until the unrelated World/Streaming compile wall is cleared.

Problem: Black-box telemetry had flags and positions but no state hash, and a persistent NaN could dump the same 300-frame buffer every frame.
Solution: Added per-entry `StateHash`, `ContextHash`, and `DispatchWaveCount`, and gated dump creation to once per non-finite incident until state returns finite.
Rejected Alternatives: Logging only through `GlobalTelemetryBus` was rejected because the mandate requires a disk dump. Repeated dumps were rejected because they turn a NaN into an IO storm.
Scalability potential: The telemetry cost stays one struct write per frame on every tier; dump IO remains crash/anomaly-only.
Hardware Impact: Runtime cost is negligible compared with the compute dispatch; failure-mode IO is bounded.

Problem: Unity D3D11 shader import caught `line` as an invalid token in the compute shader after the re-audit.
Solution: Renamed the local to `ridgeLine`; post-fix console shows no caustics shader or caustics C# errors. `AnalyticalCausticsService.cs` validates with 0 diagnostics.
Rejected Alternatives: Ignoring console errors or claiming Vulkan/DX11 verification without import evidence was rejected.
Scalability potential: The fix is platform-neutral syntax hygiene.
Hardware Impact: No runtime cost; removes the actual D3D11 compile blocker for caustics.

## Loop 8 Resource-Policy Upgrade
Problem: The compute shader initially lived under `Assets/Resources` and the service could load it with `Resources.Load`. Project rules forbid that path, and it also hides scene/bundle ownership from bootstrap. The depth gate also used a single threshold, which could flicker if the player hovered around -100m.
Solution: Moved `Hecton_CausticsGenerator.compute` to `Assets/_Project/Art/Shaders` with its `.meta` preserved, removed the `Resources.Load` fallback, and added a serialized `GameBootstrapper.analyticalCausticsCompute` field that cold-reflects into `AnalyticalCausticsService.AssignComputeShader`. Added a `try/finally` around the reflection scratch array, latched missing compute-kernel detection, and added -100m disable / -95m enable hysteresis for the abyss gate.
Rejected Alternatives: Keeping `Resources.Load` was rejected because AGENTS forbids it and because hidden runtime loads undermine deterministic bootstrap. Addressables were not introduced because the bootstrap path already owns serialized dependencies and adding an addressable group would widen scope. A shader-only depth fade was rejected because it still pays the dispatch cost below useful visual range.
Scalability potential: Low/Unknown/MX350 still allocate no compute RT and use fragment fallback. Middle and above can bind the compute explicitly through bootstrap. Ultra remains free to spend saved cycles on stronger live-wave response after profiler proof because the service path stays deterministic and asset-owned.
Hardware Impact: Weak hardware gains from the same full-dispatch skip and from avoiding a hidden runtime asset lookup. Hysteresis prevents dispatch/resource churn at abyss threshold crossings; exact microseconds remain PENDING until the external compile wall is cleared.

Problem: After refresh, Unity's active compile blocker moved again outside VFX ownership, and `00_BOOTSTRAP` exposed `BootstrapController`, not serialized `GameBootstrapper`, so the authored compute slot on `GameBootstrapper` could not be assigned in the scene.
Solution: Verified the compute asset imports as `UnityEngine.ComputeShader` at `Assets/_Project/Art/Shaders/Hecton_CausticsGenerator.compute`. Added a scene-owned `BootstrapController.analyticalCausticsCompute` field, bound it in `00_BOOTSTRAP` to GUID `27b7cf5d630bd8d4dbc699ff38f19ac2`, and added a cold bootstrap handoff so runtime-created `GameBootstrapper` adopts that compute reference before caustics registration.
Rejected Alternatives: Editing Core/Memory compile blockers or inventing a runtime asset search was rejected by domain boundary and loading policy. Reintroducing `Resources.Load` was rejected. Addressables were still rejected because this is a bootstrap scene dependency, not a streaming content dependency.
Scalability potential: The service remains deterministic: Low tier still falls back without compute, and Mid+ compute activates only when the bootstrap scene explicitly owns the asset reference.
Hardware Impact: No frame cost added. One cold `FindAnyObjectByType<BootstrapController>()` is permitted during bootstrap owner resolution only; it replaces a broken scene binding path and does not execute in VISUAL_SYNC.

## Loop 9 Bootstrap Binding Completion
Problem: Runtime `GameBootstrapper` can be added dynamically by `BootstrapController`, so serializing the compute shader only on `GameBootstrapper` left analytical caustics in permanent fragment fallback when the bootstrap shell was the authored scene component.
Solution: Added a `BootstrapController` serialized compute reference and transferred it into the runtime bootstrapper through `TryAdoptBootstrapControllerCausticsCompute`. The no-owner bootstrap path now first resolves the bootstrap scene controller and routes through the owner-aware path, avoiding a race where `BeginBootstrap` starts before compute ownership is copied.
Rejected Alternatives: Loading the compute by path/name at runtime was rejected. Making `BootstrapController` directly know about `AnalyticalCausticsService` was rejected because it would couple Bootstrap to the Graphics.Caustics asmdef and break isolation. The chosen path keeps reflection isolated in `GameBootstrapper`.
Scalability potential: Low/Unknown/MX350 still never allocate the caustics RT. Mid/High/Ultra now have a deterministic bootstrap-owned compute reference so the analytical path can actually turn on when quality allows.
Hardware Impact: Runtime hot path remains unchanged. Bootstrap gets one cold owner query and one serialized reference copy; estimated 0 us/frame and no managed allocation in caustics dispatch.

## Loop 10 Abyss Semantics Fix
Problem: The depth gate disabled compute dispatch below -100m, but `PublishShaderGlobals` still published positive projected caustic intensity. That allowed the CoreLit procedural fallback to continue drawing fake sunlight in the abyss when `_HectonCausticsAUP.w` was zero.
Solution: Passed `depthDisabled` into `PublishShaderGlobals` and forced projected intensity to 0 while depth-gated. `PublishDisabledGlobals` now also clears `_HectonProjectedCausticsParams`, so disabled or destroyed services do not leave stale projected intensity behind.
Rejected Alternatives: Relying on scene-depth fade alone was rejected because the prompt explicitly states there is no sunlight in the abyss, and depth conventions can vary by scene/water-level source. Keeping procedural fallback alive below -100m was rejected as visual contradiction.
Scalability potential: Low-tier shallow water still gets fragment fallback. Abyss gets no caustic work or fallback energy. High/Ultra still regain analytical light only after the hysteresis threshold is crossed.
Hardware Impact: Abyss saves compute dispatch plus fallback fragment caustic evaluation. Exact microseconds remain PENDING because project compile is blocked outside caustics.

## Loop 11 Resource Lifecycle Fix
Problem: Low-tier or abyss gating releases the wave scratch/buffer/RT. On re-enable, a new scratch buffer could be allocated while `_lastWaveMetaVersion` still matched the vault meta version. The vault read path would then return the wave count without refilling scratch, causing the next `GraphicsBuffer.SetData` to upload cleared wave data.
Solution: Invalidate `_lastWaveMetaVersion` when allocating a fresh `_waveUploadScratch` and when releasing compute-only resources. This forces the next vault-bound enable to repopulate all 16 GPU wave slots before dispatch.
Rejected Alternatives: Always copying vault waves every frame was rejected because it wastes CPU upload prep when the version is unchanged. Keeping a stale version through release was rejected because release destroys the data carrier, not just the GPU handle.
Scalability potential: Low/Unknown/MX350 can continue releasing compute resources aggressively. Mid/High/Ultra recover analytical caustics correctly after surfacing from abyss or after quality-tier promotion without permanent zero-wave output.
Hardware Impact: 0 us/frame steady state. The extra work occurs only on cold resource allocation/re-enable and is a bounded 16-entry struct fill plus one GPU buffer upload already required by the dirty flag.
