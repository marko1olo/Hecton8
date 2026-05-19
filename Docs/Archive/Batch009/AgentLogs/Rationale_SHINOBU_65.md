# SHINOBU_65 Rationale - Diegetic Visor Lens

Date: 2026-05-18  
Agent: SHINOBU_65  
Prompt: `Docs/Tasks/CURRENT_BATCH.md` second `<AGENT_PROMPT id="SHINOBU_65">`

## Duplicate-ID Override

Problem: `CURRENT_BATCH.md` contains two `SHINOBU_65` XML blocks and the durable status/rationale files contained the wrong-domain duplicate before the active visor assignment.
Solution: Trim `Status_SHINOBU_65.md` and `Rationale_SHINOBU_65.md` to the active second XML block: diegetic visor and lens simulator, 20 tasks. Historical reports remain in append-only `LOG_SHINOBU_65.md`.
Rejected Alternatives: Keeping wrong-domain content in the active anti-amnesia files, or continuing non-visor work while the user explicitly asked for visor glass. Both would corrupt shared-agent memory.
Scalability potential: Visor uses continuous `GlobalQualityWeight`: low = static/chroma distortion, middle = condensation/dirt scalar masks, high = head-motion droplet vector, ultra = richer refraction/reflection/glitch.  
Hardware Impact: Documentation selection is 0 us/frame; it prevents shipping the wrong domain work.

## Decisions

Problem: Canvas Image overlays and particle droplets are forbidden, and the project already has a RenderGraph visor refraction pass.  
Solution: Reuse `HectonVisorFluidDistortionFeature`, extend its shader with `HectonDiegeticVisorLensGlobals`, and add a downscaled RenderGraph compute prepass that resolves condensation/crack/dirt/glitch masks from the CPU scalar lanes. CPU writes state, droplet gravity/refraction, quality/anomaly/darkness, and pressure/silt/head speed once; the compute mask replaces repeated full-res fragment mask work when quality allows it.
Rejected Alternatives: Creating Canvas/RawImage dirt, particle droplets, per-material `SetFloat` churn, or a one-thread compute shader that only copies four scalar lanes without visual payoff.
Scalability potential: Low devices receive the scalar CBuffer and fall to static/chroma distortion; middle/high/ultra enable the downscaled compute mask and richer Snell/reflection/crack presentation through continuous blend and render-scale math.
Hardware Impact: One 64-byte CBuffer upload when dirty plus one transient downscaled compute dispatch only when lens activity and `GlobalQualityWeight` justify it; exact GPU/CPU us pending profiler.

Problem: Physiology, pressure, silt, and anomaly owners are sibling domains and cannot be hard dependencies for visor.  
Solution: Add local field-only mock DTOs and consume neutral core signals only: `PlayerExhaleSignal`, `PlayerWaterSplashSignal`, `PlayerFatalPressureSignal`, and `SystemGlitchSignal`.  
Rejected Alternatives: Direct references to physiology/submarine/anomaly runtimes, AUP-heavy anomaly/droplet lanes, or scene searches.  
Scalability potential: Mock scalar inputs can later be replaced by Vault readers without changing shader or DTO layout.  
Hardware Impact: Bounded `ReadOnlySpan` snapshot scans; no per-frame managed allocation.

Problem: The visor is camera-local and must not inherit AUP precision costs or jitter risk.  
Solution: New visor runtime/types contain no `double`, `double3`, or `AbsoluteUniversePosition`. Head movement uses camera-local quaternion delta and `float3` angular velocity only.  
Rejected Alternatives: Reading `WaterTransitionSignal`/`VisorDropletSignal` AUP payloads or using absolute world coordinates in jobs.  
Scalability potential: Same local float math works from toaster to ultra; quality only changes visual richness.  
Hardware Impact: No 64-bit math in Burst visor job.

Problem: `VisorStateDTO` must be ARM64-safe and mutable from jobs without CS1612 property copies.
Solution: `VisorStateDTO` is `[StructLayout(LayoutKind.Sequential, Size = 16)]` with four public floats. Runtime mutation uses private unsafe helpers behind guarded `TryWriteState`/`TryWriteTuning` gates.
Rejected Alternatives: properties, bool fields, `Pack=1`, managed state classes, or public ref-return escape hatches.
Scalability potential: 16-byte state is cheap to copy into Vault/GPU lanes for every tier.  
Hardware Impact: One aligned 16-byte state read/write per job.

Problem: Fogged glass needs believable biology but not fluid truth.  
Solution: `VisorCondensationJob` uses the Dear Lie: respiration/heart/core temperature/cold water feed condensation, `math.exp(-ClearingRate * dt)` clears it, and shader value noise makes it look spatial.  
Rejected Alternatives: simulating water vapor particles, per-pixel CPU masks, or render textures for fog accumulation.  
Scalability potential: Low keeps scalar fog alpha; high/ultra spend shader ALU on spatial mask/refraction.  
Hardware Impact: One scalar Burst job; target below 0.02 ms pending profiler.

Problem: Water droplets must react to head motion without particle physics.  
Solution: Runtime converts camera angular velocity to `float2 DropletGravityVector`; shader skews procedural droplet UV flow.  
Rejected Alternatives: Rigidbody droplets, particle emitters, or texture scroll scripts per droplet.  
Scalability potential: Low lerps toward static downward flow; high/ultra use dynamic angular response.  
Hardware Impact: One quaternion delta per Tick plus scalar shader math.

Problem: Cracks, dirt, and breach audio need shared truth without concrete audio/UI dependencies.  
Solution: Pressure grows `CrackSeverity`; silt grows `DirtAccumulation`; wipe scalar decays both; unmanaged `VisorBreachSignal` emits on crack > 0.8 with cooldown.  
Rejected Alternatives: crack decal GameObjects, direct audio service calls, or Canvas scratch layers.  
Scalability potential: Low gets cheaper masks; high/ultra can intensify procedural cracks/reflection from the same DTOs.  
Hardware Impact: One SignalBus push per breach window, one 64-byte telemetry write per job commit.

Problem: Crash analysis cannot rely on chat or console logs.  
Solution: Vault-owned `VisorLensTelemetryEntry[300]` plus cursor buffers record state, pressure, silt, quality, head speed, and hashes. NaN sets a flag and writes `Docs/AgentLogs/Dump_VISOR_SURGEON.bin`.  
Rejected Alternatives: local persistent NativeArray owner, text spam, or "cannot reproduce" reports.  
Scalability potential: Same telemetry layout across low/middle/high/ultra.  
Hardware Impact: 64 bytes written per committed visor job; binary dump only on fault.

Problem: Designers need to tune the glass and prove masks change without recompiling.  
Solution: Add `Diegetic Visor Tuner` EditorWindow with state/tuning sliders, mock/reset/wipe controls, CSV reload, and procedural 2D preview.  
Rejected Alternatives: hardcoded tuning only, runtime debug GameObjects, or Canvas preview.  
Scalability potential: Tuning applies to every quality weight; editor preview shows scalar behavior before runtime capture.  
Hardware Impact: Editor-only, 0 player-frame cost.

Problem: Full compile verification was requested by process, but the project guard forbids `dotnet build` when CPU is above 50% or another compiler is running.
Solution: Initial CPU samples were 93.17%, 63.80%, and 86.43%; latest CPU samples were 100/100/99.42. Build was not launched. Static scans and `git diff --check` were used instead.
Rejected Alternatives: Violating the explicit build guard to force a compile, or claiming compile proof from stale generated project files.
Scalability potential: No runtime impact. It keeps verification honest in a 20+ agent workspace.  
Hardware Impact: 0 us/frame.

## Ultra Polish Loop - 2026-05-18

Problem: The visor runtime exposed `public static DiegeticVisorLensRuntime Instance`, which is a classic singleton-shaped access path and unnecessary for player runtime.  
Solution: Remove the static instance and route the editor tuner through an editor-only scene lookup. Runtime ownership remains component + GlobalRegistry tick registration + Vault handles.  
Rejected Alternatives: Keeping the singleton because it was convenient for the EditorWindow. That leaks a bad access pattern into runtime code.  
Scalability potential: No quality-tier change; compile-wall isolation is cleaner because the editor facade no longer becomes a runtime access convention.  
Hardware Impact: Runtime 0 us/frame; editor query only when the tuner window is drawn.

Problem: The 64-byte visor GPU globals buffer could be allocated on first `LateFrameTick`, creating a first-use render-frame allocation.  
Solution: Allocate and clear the `GraphicsBuffer.Target.Constant` buffer during `EnsureNativeState()` and publish neutral globals on disable. Dirty upload now updates global scalar vectors only when the DTO changes.  
Rejected Alternatives: Lazy allocation during the first visible condensation frame or per-frame vector spam.  
Scalability potential: Low/middle/high/ultra share the same 64-byte scalar contract; quality only changes the math consumed by the shader.  
Hardware Impact: Removes a possible first-visual-frame stutter; steady state remains one CBuffer bind and dirty 64-byte upload.

Problem: The inherited visor RenderFeature still carried a binary low-tier scalar into the shader, contradicting the continuous quality requirement.  
Solution: Replace it with `LowTierWeight01`, derived from `GlobalQualityWeight`, hardware fallback, stress fallback, and visor refraction scale. HLSL now uses `dynamicVisorWeight` and `refractionWeight` so low quality collapses into static film/chroma while high quality restores droplet noise and Snell refraction.  
Rejected Alternatives: Boolean `lowTier ? 1f : 0f` as the primary algorithm gate, or hiding expensive droplet noise behind a zero lerp after already paying for it.  
Scalability potential: q below roughly 0.3 skips dynamic droplet/noise/refraction branches; middle blends static film with droplet flow; high/ultra spends ALU on richer refraction, reflection, salt crystals, and silt.  
Hardware Impact: Low-tier shader path avoids `ComputeDropletMask` value-noise and Snell sampling work; exact GPU us requires Frame Debugger/profiler.

Problem: The task explicitly requested a `partial struct VisorBreachSignal`; the previous struct was unmanaged and correct size, but not partial.  
Solution: Mark `VisorBreachSignal` partial without changing its 32-byte layout.  
Rejected Alternatives: Treating "partial" as cosmetic while other agents may need a disjoint declaration surface.  
Scalability potential: No runtime visual change. It preserves cross-agent extension space without a hard dependency.  
Hardware Impact: 0 us/frame.

Problem: The cold file reader depended on a mixed int/long `math.min` expression, which is an avoidable compile-risk in Unity.Mathematics version drift.  
Solution: Replace it with explicit long capping before casting to int.  
Rejected Alternatives: Trusting a package overload that may not exist on this project version.  
Scalability potential: Boot/cold path remains deterministic and bounded to the Vault scratch buffer.  
Hardware Impact: 0 us/frame during gameplay.

Problem: Final forensic proof was chat-only and therefore non-durable.  
Solution: Add `Docs/AgentLogs/SELF_AUDIT_SHINOBU_65.xml` with the 20-task reconciliation, DTO offsets, Vault IDs, dependency graph, compile-wall status, and Dear Lie complexity.  
Rejected Alternatives: Claiming rigor in the final chat without a durable artifact.  
Scalability potential: Documentation-only, but future agents can validate the same low/middle/high/ultra assumptions.  
Hardware Impact: 0 us/frame.

## Telemetry Completeness Loop - 2026-05-19

Problem: Task 17 explicitly required `ShaderUpdateComputeTimeNs`, but the visor telemetry entry recorded refraction scale, darkness/anomaly, hashes, and state without an explicit shader update timing lane.  
Solution: Reuse the existing 64-byte cache-line telemetry entry by replacing the lower-value darkness lane at offset 56 with `uint ShaderUpdateComputeTimeNs`. `UploadGpuGlobals()` now measures the scalar publish/CBuffer bind path with integer `Stopwatch` ticks and patches the latest ring entry.  
Rejected Alternatives: Expanding telemetry to 80 or 128 bytes, adding a second telemetry buffer, or claiming shader upload cost from speculation.  
Scalability potential: The same timing lane works for low static-film mode, middle blend mode, and high/ultra refraction mode; future profiler captures can compare real upload/bind cost across quality weights.  
Hardware Impact: Two timestamp reads plus one 64-byte telemetry read/write per upload; exact nanoseconds are now written into the black box.

Problem: The user wording explicitly says "Compute Shader"; a pure CBuffer-to-fragment path is too easy to misread as a non-literal implementation even if it is cheaper.
Solution: Add `Hecton_DiegeticVisorLens.compute` and wire it through the existing visor RenderGraph feature as a declared compute prepass. It writes `_HectonDiegeticVisorLensMaskTex` at a quality-scaled, quantized internal resolution. The raster visor shader samples that mask and blends it over the procedural fallback for condensation, cracks, dirt, and glitch.
Rejected Alternatives: A one-thread scalar-copy compute dispatch, hidden global UAV writes, persistent RT allocation in `RecordRenderGraph`, or a second fullscreen composite pass.
Scalability potential: q below roughly 0.3 keeps compute blend at zero and uses static film/chroma; middle quality turns on a small downscaled mask; high/ultra increase mask scale and spend saved fragment ALU on richer refraction/reflection/silt.
Hardware Impact: Adds a real compute dispatch only when lens activity and quality justify it. It shifts repeated full-res fragment noise to a lower-resolution mask; exact GPU us requires Frame Debugger/profiler.

## Continuous CPU Cadence Loop - 2026-05-19

Problem: The shader path shed ALU at low quality, but the CPU Burst condensation solver still had permission to schedule every dispatcher tick. That violates the hardware matrix requirement that low quality reduce update frequency, not just visual complexity.  
Solution: Add `_simulationAccumulator` and `ResolveSimulationInterval()`. The interval maps `GlobalQualityWeight` through a smooth polynomial and `math.lerp`: q=0.1 resolves to 5 Hz, q=1.0 resolves to 60 Hz. `Tick()` accumulates dispatcher delta and schedules `VisorCondensationJob` only when the interval expires.  
Rejected Alternatives: Every-frame CPU solver with a low-tier shader branch, or a binary low/high cadence switch.  
Scalability potential: Low = 5 Hz scalar condensation/crack/dirt update with static film/chroma shader; middle = blended cadence and blended shader masks; high/ultra = 60 Hz scalar updates feeding dynamic head-motion droplets and richer refraction.  
Hardware Impact: At q=0.1, steady-state job schedules drop from up to 60/sec to 5/sec. Exact CPU microseconds require profiler capture.

Problem: A naive cadence throttle would delay important player-facing events such as surface wash, wipe, pressure crack, glitch, or breath spike.  
Solution: Add `_forceImmediateSimulation`; event ingress and mock injection paths set it for one immediate schedule, then `Tick()` clears it after scheduling.  
Rejected Alternatives: Letting surface emergence wait up to 200 ms on low quality, or exempting the whole system from cadence reduction.  
Scalability potential: Low tier still collapses steady-state cost while preserving instantaneous critical feedback.  
Hardware Impact: One extra job only on event frames; no steady-state overhead beyond a boolean check.

## Mutation Barrier and Dependency Discipline Loop - 2026-05-19

Problem: Public visor APIs could write the same Vault state, physiology, environment, or tuning buffers while `VisorCondensationJob` was scheduled against them.  
Solution: Add a scalar pending-input barrier. Mock physiology, pressure, environment, surface wash, wipe, and mock reset calls stage primitive fields while `_hasScheduledWork` is true. The runtime applies those pending fields only after the scheduled job has been committed and before the next job is scheduled.  
Rejected Alternatives: Blocking every public mutator with `JobHandle.Complete()`, or allowing editor/runtime calls to race Burst-owned buffers.  
Scalability potential: Low/middle/high/ultra all preserve immediate player feedback through `_forceImmediateSimulation`, but the actual Vault write still happens in the safe pre-schedule window.  
Hardware Impact: Prevents data races and undefined reads; steady-state cost is a few primitive branches, 0 B/frame.

Problem: `Tick()` still called the completion path to read job output when `IsCompleted` was true. Even when nonblocking, it violates the project rule that simulation phase does not own job completion.  
Solution: `Tick()` now returns if work is active. `LateFrameTick()` is the visual-sync commit point and `OnDisable()` remains the only forced drain.  
Rejected Alternatives: Keeping the nonblocking complete in `Tick()` because it usually does not stall. That keeps a future regression path open.  
Scalability potential: CPU cadence remains 5 Hz to 60 Hz by `GlobalQualityWeight`; completion timing is phase-stable instead of simulation-phase opportunistic.  
Hardware Impact: Avoids accidental main-thread stalls in the simulation phase; exact microseconds require Unity profiler.

Problem: The editor tuner used `GetStateRef()` and `GetTuningRef()` to mutate Vault memory directly. During Play Mode, that can collide with the active Burst job.  
Solution: Add `TryWriteState()` and `TryWriteTuning()` methods that fail closed while a job is active. The editor now writes through these gates.  
Rejected Alternatives: Allowing editor-only tooling to bypass the same memory rules as runtime, or blocking editor GUI with a forced completion.  
Scalability potential: Designer tooling remains usable across every quality weight without creating a timing-dependent race.  
Hardware Impact: Editor-only. Player frame cost 0 us.

Problem: The render feature still had normal-path registry lookups for player/fluid services and an explicit `Pack=4` CBuffer DTO layout marker.  
Solution: Cache the player and fluid interfaces after first successful resolve and remove the explicit pack declaration from `VisorFluidGlobalsDTO`; it remains 128 bytes through eight `Vector4` lanes.  
Rejected Alternatives: Re-reading GlobalRegistry every pass as the steady-state path, and keeping pack metadata that adds audit noise without improving shader alignment.  
Scalability potential: Low quality can skip expensive shader branches while the CBuffer layout stays stable for high/ultra visual-overkill lanes.  
Hardware Impact: Normal-case registry lookup churn is removed; CBuffer layout remains one active 128-byte bind from a ping-pong pair. Exact us pending profiler.

Problem: The public ref-return accessors survived after the mutation gate was added, so external code could still bypass `_hasScheduledWork`.
Solution: Convert the ref accessors to private unsafe helpers used only after `TryWriteState`/`TryWriteTuning` have verified no job is active.
Rejected Alternatives: Trusting call sites to avoid public ref mutation during jobs, or blocking public writes with a forced job completion.
Scalability potential: Same behavior across quality weights; the safety property is independent of cadence.
Hardware Impact: 0 B/frame. Prevents data corruption rather than claiming speed.

Problem: The visor lens and visor fluid CBuffer paths were single-buffered. A CPU `LockBufferForWrite` could target the same buffer still bound for GPU reads.
Solution: Allocate two constant buffers per path and alternate writes. Runtime lens globals are cold-allocated in `EnsureNativeState`; render-feature fluid globals are prewarmed in `Create()`, and the render path refuses allocation if buffers are missing.
Rejected Alternatives: Keeping single-buffered CBuffer writes, or allocating from `AddRenderPasses`/`RecordRenderGraph` during rendering.
Scalability potential: Low tier still receives cheap scalar globals; high/ultra can use richer shader optics without hidden CPU/GPU sync from rewriting the bound buffer.
Hardware Impact: Removes a driver sync hazard. Exact microseconds require GPU profiler or Frame Debugger evidence.

## Literal RenderGraph Compute Mask Loop - 2026-05-19

Problem: The scalar CBuffer path was architecturally sound but did not literally send the visor scalars through a Compute Shader as requested. A fake compute pass that only copies constants would satisfy wording while adding no useful rendering work.
Solution: Add `Hecton_DiegeticVisorLens.compute` and bind it from `HectonVisorFluidDistortionFeature` as a declared RenderGraph compute pass. The kernel consumes the same CPU/Burst lens scalar lanes and writes a compact RGBA lens mask: R condensation/droplet haze, G crack ridge, B dirt/silt specks, A anomaly/glitch. The raster visor pass samples `_HectonDiegeticVisorLensMaskTex` and blends it with the existing procedural fallback.
Rejected Alternatives: One-thread scalar-copy compute, hidden UAV/global writes, persistent RTHandle allocation inside render, Canvas/RawImage preview in runtime, or per-droplet simulation.
Scalability potential: Low = compute blend resolves to zero and the raster shader keeps static film/chroma. Middle = small quantized downscaled mask blended into fog/crack/dirt. High = larger mask and full dynamic droplet/refraction blend. Ultra = same scalar authority plus higher mask scale and overkill optics/silt/reflection.
Hardware Impact: It trades repeated full-resolution fragment noise for one downscaled compute mask when lens activity and quality justify it. Exact GPU us is pending Frame Debugger/profiler; no fabricated timing.

Problem: C# compile proof is still blocked.
Solution: Latest CPU samples were 100/100/99.42, so the build guard forbids `dotnet build`. Also, the generated `Hecton8.Core.csproj` currently lists `HectonVisorFluidDistortionFeature.cs` but not the new `DiegeticVisorLensRuntime.cs`, `DiegeticVisorLensTypes.cs`, or editor tuner, so a dotnet compile would not prove the full visor addition until Unity regenerates project files.
Rejected Alternatives: Launching build under 100% CPU or reporting compile proof from a stale generated project file.
Scalability potential: Verification integrity only.  
Hardware Impact: Prevents compile-wall load on already saturated hardware.

## XR-Safe Compute Mask Descriptor Loop - 2026-05-19

Problem: The compute mask was initially cloned from `activeColorTexture` via `new TextureDesc(sourceDesc)`. In XR, the camera color descriptor can carry array texture dimensions, multiple slices, dynamic scaling, or VR usage that do not match the compute kernel's `RWTexture2D<float4>` contract.
Solution: Create the lens mask from explicit width/height with `xrReady: false`, then force `slices = 1`, `TextureDimension.Tex2D`, `VRTextureUsage.None`, clamp wrap, no dynamic scale, no mips, and UAV access. The raster shader samples this non-XR transient mask with raw fullscreen `input.screenUV`; only scene/depth/color sampling uses stereo-transformed UVs.
Rejected Alternatives: Keeping camera descriptor inheritance, converting the kernel to `RWTexture2DArray` without a proven need, or sampling a single-slice non-XR mask with XR-transformed screen UVs.
Scalability potential: Low quality still skips the compute mask. Middle/high/ultra receive the same stable 2D lens mask independent of XR camera target layout, with render scale governed by `GlobalQualityWeight`.
Hardware Impact: Descriptor hardening is 0 us/frame. It prevents an XR resource-shape mismatch rather than claiming speed.

Problem: After the descriptor fix, compile proof was needed for the touched RenderFeature but had to obey the build guard.
Solution: Re-ran XML parse, banned-pattern scan, descriptor scan, `git diff --check`, and the build guard. XML, pattern, and descriptor checks passed. `git diff --check` produced CRLF normalization warnings only. A later guard sample had no dotnet/csc process and CPU samples of 18.05, 14.19, and 24.21, so one targeted `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` was allowed.
Rejected Alternatives: Launching build while CPU was above 50 percent, or claiming compile proof from static checks alone.
Scalability potential: Verification-only.
Hardware Impact: One targeted compile attempt.

Problem: The targeted compile failed before SHINOBU_65 could obtain a green C# proof.
Solution: Treat it as a compile-wall dependency block and do not edit unrelated domains. The failure set is outside the visor lens patch: `math.reversebytes` in `ShinobuFloraFaunaSymbiosisSolver.cs`, unassigned `sanitizedWeight` in `HomeostasisBrain.ScalabilityDictator.cs`, missing `IndustrialLoreBitMask` in `SaveBinaryPayloadCodec.cs`, missing AUP fields on `AssetRecord` in `AssetLifecycleGovernor.cs`, and missing `HectonDrsRenderFeatureGate` in `HectonAbyssalSsdoFeature.cs` and `HectonScooterVolumetricShaftsFeature.cs`.
Rejected Alternatives: Repairing non-SHINOBU compile errors, running repeated build attempts into the same wall, or hiding the failed build in the chat only.
Scalability potential: Verification-only.
Hardware Impact: No runtime impact; compile proof remains blocked by dependency errors.

## Scalability Event Mutation Barrier Loop - 2026-05-19

Problem: `OnScalabilityChanged()` still wrote `VisorLensTuningDTO.Version` directly through the Vault while `VisorCondensationJob` could be scheduled with the tuning buffer as a read input.
Solution: Route scalability version changes through the same pending-input discipline as physiology, pressure, silt, wipe, and mock reset. If work is active, set `_pendingTuningVersionIncrement` and `_forceImmediateSimulation`. `Tick()` applies the pending version increment only in the safe pre-schedule window after the previous job has committed.
Rejected Alternatives: Forcing `JobHandle.Complete()` inside the scalability callback, or treating a version counter write as harmless while Burst may read the same cache line.
Scalability potential: Low/middle/high/ultra quality changes still trigger immediate visor scalar refresh, but the memory ownership rule is now stable across all cadences.
Hardware Impact: One boolean check in the pre-schedule path. The gain is correctness and data-race removal, not measured frame time.

## Binary Low-Tier Gate Removal Loop - 2026-05-19

Problem: `HectonVisorFluidDistortionFeature` still derived one shader-pressure lane from `ResolveLowTier()` and `lowTier ? 1f : 0f`. That is a binary algorithm switch even though the final `LowTierWeight01` was a float.
Solution: Delete the bool gate and replace it with `ResolveHardwareLowPressure01()`: a continuous curve comparing `SystemInfo.graphicsMemorySize` against the configured VRAM threshold. Visual overkill now multiplies continuous `GlobalQualityWeight`, low-tier headroom, and designer strength only; the enum tier remains telemetry, not algorithm authority.
Rejected Alternatives: Keeping `HectonQualityTier.Low/Mx350` as an immediate render algorithm switch, or pretending that wrapping a boolean in `math.max` made the path continuous.
Scalability potential: Low hardware smoothly suppresses compute masks/refraction/salt/silt as pressure rises. Middle hardware blends. High/ultra restore richer lens effects only when `GlobalQualityWeight` and thermal headroom support them.
Hardware Impact: Negligible CPU math change. The value is visual stability and proportional ALU shedding, not a fabricated microsecond saving.

## Compute CBuffer and Motion Ramp Loop - 2026-05-19

Problem: The RenderGraph compute pass consumed lens scalars through five `SetComputeVectorParam` calls. That is valid Unity API, but it keeps the compute lane outside the project's preferred CBuffer/GraphicsBuffer upload model and leaves the buffer read invisible to RenderGraph.
Solution: Add an 80-byte `LensComputeGlobalsDTO` made from five `Vector4` lanes, prewarm two `GraphicsBuffer.Target.Constant` buffers in the visor feature, alternate writes with `LockBufferForWrite`, import the active buffer into RenderGraph, declare `UseBuffer(..., AccessFlags.Read)`, and bind it with `SetComputeConstantBufferParam`. The compute shader now wraps those lanes in `CBUFFER_START(HectonDiegeticVisorLensComputeGlobals)`.
Rejected Alternatives: Keeping multiple vector parameter calls because the payload is small, or writing a persistent render texture/global UAV outside graph visibility. Both keep unnecessary driver call surface or hidden resource state.
Scalability potential: Low quality still resolves compute blend to zero. Middle/high/ultra dispatch the same downscaled mask, but the scalar transport is one CBuffer bind rather than five compute vector uniforms. Ultra can spend the saved call surface on richer mask detail without changing the CPU authority contract.
Hardware Impact: No profiler number. The theoretical gain is reduced render-thread API calls on active compute-mask frames and cleaner graph resource declaration, not a claimed frame-time win.

Problem: Thermal motion culling used a binary threshold on local velocity squared. It is not a quality tier switch, but it is still an abrupt visual algorithm cutoff.
Solution: Replace the threshold with a continuous smooth ramp from 12 m/s to 15 m/s using squared speed and `Smooth01`. The shader receives a scalar fade, so high-speed distortion suppression blends instead of popping.
Rejected Alternatives: Keeping the hard cutoff because it is cheap. The one extra multiply/smooth polynomial is cheaper than visible flicker at sprint/scooter boundary.
Scalability potential: Low, middle, high, and ultra all use the same stable ramp; quality still controls how much optical detail exists, while motion controls how much of that detail survives fast camera movement.
Hardware Impact: Negligible CPU cost; visual stability improvement. Exact us pending profiler.

## Unity API Source and Guard Recheck Loop - 2026-05-19

Problem: The compute CBuffer patch could still hide an API-signature mismatch because the fresh `dotnet build` is blocked by the project guard.
Solution: Verify against local Unity 6000.4 package source instead of memory. `Library/PackageCache/com.unity.render-pipelines.core` contains `RenderGraph.ImportBuffer(GraphicsBuffer)` and `CommandBuffer.SetComputeConstantBufferParam(ComputeShader, int, GraphicsBuffer, int, int)`, which matches the visor compute pass. Static scans were rerun after this check.
Rejected Alternatives: Using web docs, stale chat memory, or another build attempt while CPU guard blocks it.
Scalability potential: Verification-only. The runtime scaling policy remains unchanged: low collapses compute blend/refraction, middle enables a small downscaled mask, high/ultra spend ALU on richer visor optics.
Hardware Impact: 0 us/frame. Build guard found no dotnet/csc process, but CPU samples first hit 30.89, 43.80, and 53.37 percent, then hit 100.00, 66.22, and 47.27 percent. No build was launched.
