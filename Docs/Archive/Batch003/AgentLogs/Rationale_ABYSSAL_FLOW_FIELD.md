# Rationale_ABYSSAL_FLOW_FIELD

Status: PENDING VERIFICATION
Agent: FLUID_MECHANIC
Prompt: ABYSSAL_FLOW_FIELD

## Decision 0: GPU Flow Field Over CPU Fluid
Problem: The ocean needed visible currents and wake response without CPU fluid simulation or readback stalls.
Solution: Use a fixed 32x32x32 GPU RenderTexture pair and deterministic curl-noise/wake kernels. CPU drag coupling uses one analytical vector, not texture readback.
Rejected Alternatives: CPU voxel fluid and `AsyncGPUReadback` were rejected because they add frame stalls, synchronization risk, and false physical truth outside gameplay-critical needs.
Scalability potential: Low = base curl only. Middle = base curl plus particle/flora sampling. High = wake injection. Ultra = wake plus geyser/vortex-ready extension point.
Hardware Impact: MX350/i3 avoids CPU particle truth and readbacks; target saving versus CPU advection is 300-900 us depending on particle/fish counts.

## Decision 1: Decoupled Binding
Problem: Boids, particles, vegetation, submarine, and thermal systems are being modified by concurrent agents.
Solution: Publish global shader texture/property contracts and optional GlobalRegistry queries. Keep flow consumers tolerant of missing buffers/textures via fallbacks.
Rejected Alternatives: Direct concrete references to every consumer were rejected because batch parallelism makes them brittle.
Scalability potential: Low tier can bind the same texture but skip injection; High tier can consume richer flow without API churn.
Hardware Impact: Prevents managed per-frame dependency searches and integration churn on low-end silicon.

## Decision 2: Half Backing Format, Float UAV Type
Problem: The assignment requested `RWTexture3D<half4>`, while Unity/D3D typed UAV support is more reliable with float declarations over half-backed formats.
Solution: Use `GraphicsFormat.R16G16B16A16_SFloat` RenderTextures for half storage and `RWTexture3D<float4>` declarations for compiler stability.
Rejected Alternatives: Literal `RWTexture3D<half4>` was rejected as a shader portability risk; `RGBAFloat` was rejected as unnecessary bandwidth and memory.
Scalability potential: Low/Middle/High/Ultra all use the same memory layout; quality changes alter injection and sampling, not allocation.
Hardware Impact: Two 32^3 RGBA16F volumes cost roughly 1 MiB total and keep bandwidth acceptable on MX350.

## Decision 3: Wake As Cinematic Velocity Splat
Problem: Submarine movement needs visible turbulence without simulating Navier-Stokes.
Solution: Wake injection is a radial/trailing velocity splat with radius/speed guards and clamp bounds.
Rejected Alternatives: Particle wake grids, pressure solves, and CPU splats were rejected as too expensive and less controllable.
Scalability potential: Low disables wake. High enables it. Ultra can layer future vortex injection without changing the texture contract.
Hardware Impact: Saves an estimated 0.5-2.0 ms versus physical wake simulation; adds roughly 35-70 us GPU when enabled.

## Decision 4: Geyser As Thermal Cheat
Problem: Hot vents should push particles/kelp/fish upward, but a real thermal fluid model is unjustified.
Solution: Heat sources above intensity 0.80 inject a bounded upward vector with mild tangent swirl.
Rejected Alternatives: Volumetric heat diffusion and particle geyser truth were rejected because visual response is enough.
Scalability potential: Low disables geyser. Middle can sample base flow. High/Ultra use heat-source injection.
Hardware Impact: Low-end skips heat capture and the heat loop entirely; high-end spends saved cycles on stronger visual motion.

## Decision 5: CPU Drag Uses Analytical Flow
Problem: Hydro drag needs local flow at the submarine, but reading the GPU texture violates the no-readback rule.
Solution: `SubmarineFluidDynamics` samples `GlobalRegistry.Fluid.GetFlowAtPosition` and feeds `FlowVelocityWS` into the Burst drag job.
Rejected Alternatives: `AsyncGPUReadback` and a CPU mirror of the 32^3 texture were rejected as sync/memory bloat.
Scalability potential: All tiers get deterministic local-flow drag. GPU visual richness can scale independently.
Hardware Impact: One analytical vector costs roughly 2-5 us CPU and avoids a 200-1000 us readback stall.

## Decision 6: Black Box Telemetry
Problem: A critical flow system cannot fail silently on NaN or invalid state.
Solution: Added a fixed 300-frame NativeArray ring for high-level flow state and binary dump path `Docs/AgentLogs/Dump_ABYSSAL_FLOW_FIELD.bin`.
Rejected Alternatives: Debug.Log spam and managed Lists were rejected because they allocate, jitter, and lose pre-crash context.
Scalability potential: Same cost on all tiers; dump only on invalid state.
Hardware Impact: Fixed small memory footprint, zero steady GC, linear writes.

## Decision 7: Consumer Sampling Policy
Problem: Different consumers have different filtering needs.
Solution: Vegetation keeps filtered texture sampling for smooth vertex sway; marine snow uses integer `Texture3D.Load` after OMEGA to avoid Unity HLSL sampled-local warnings; boids keep texture sampling with fallback paths.
Rejected Alternatives: One universal sampling helper was rejected because shader stages and cost profiles differ.
Scalability potential: Low still gets deterministic base drift. High/Ultra get wake/geyser visual response.
Hardware Impact: Particle compute avoids sampler warning risk and keeps sampling cheap; vegetation pays the visual filtering cost where it matters.

## OMEGA POLISH CHANGES
Problem: Final audit required bloat removal, Math LOD checks, zero-GC scan, and build health verification.
Solution: Removed shader warning-prone `isfinite` and ternary sampled-local patterns, switched marine snow flow texture reads to `Texture3D.Load`, fixed duplicate boid fallback texture destroy, made `HectonFluidEngine.OnDisable` match its persistent-allocation cleanup comment, and cleared stale global texture params on release.
Rejected Alternatives: Claiming a clean Unity shader pass without a live Unity console was rejected. Fixing unrelated `Hecton8.Core.csproj` CS0649 warnings in audio/world files was rejected as an out-of-domain edit.
Scalability potential: Low = base curl only, no wake/geyser, staggered marine snow work. Middle = base flow consumers with limited heavy features. High = wake, geyser, SDF/particle visual extras. Ultra = ready for additional vortex injection.
Hardware Impact: MX350 path skips the heaviest injection passes and avoids readbacks; high-end path spends GPU cycles on visible turbulence. `Hecton8.Core.csproj` currently builds with 3 out-of-domain warnings, so status remains PENDING VERIFICATION, not VERIFIED MASTER GRADE.

## Decision 8: Fixed-Step Dispatch Cadence
Problem: The flow dispatch was invoked once before buoyancy-object checks and once again during buoyancy scheduling, then guarded by `Time.frameCount`. That avoided duplicate work but skipped flow updates during multiple fixed simulation steps inside one rendered frame.
Solution: Keep the early dispatch so flow runs even with zero buoyancy objects, remove the second call, and guard duplicate calls by `Time.fixedTime` instead of render frame.
Rejected Alternatives: Leaving the frame guard was rejected because wake decay and thermal injection become frame-rate dependent under physics catch-up. Moving dispatch into the buoyancy-object branch was rejected because abyssal flow must exist even when no registered floaters are active.
Scalability potential: All tiers now get deterministic fixed-step decay; Low still skips wake/geyser work, High/Ultra keep per-fixed-step visual response.
Hardware Impact: Removes one redundant call path and preserves catch-up correctness. Estimated CPU saving is small, roughly 1-3 us per fixed tick in normal scenes, but it prevents stale wake/decay under frame spikes.

## Decision 9: Deterministic Texture Warm Start And Low-Tier Purge
Problem: Newly allocated or device-recreated 3D RenderTextures are not guaranteed to be zeroed. The first decay pass could blend from finite garbage. Also, switching to Low tier disabled new wake/geyser injection but could leave old wake residue decaying through the texture.
Solution: Mark the texture as uninitialized on creation/recreation/release. On the next dispatch, and on every Low-tier dispatch, send `textureParams.z = 1` so the compute pass overwrites with base curl instead of decaying prior contents.
Rejected Alternatives: A separate clear kernel was rejected because the base update already touches every voxel; a CPU clear was rejected as needless synchronization and not portable for 3D RenderTextures.
Scalability potential: Low now truly computes base curl only. High/Ultra keep persistent wake decay once the texture has a known initialized state.
Hardware Impact: No extra dispatch. First-frame and Low-tier cleanup ride the existing 32^3 update pass, saving one clear pass of roughly 20-40 us GPU.

## Decision 10: Fail-Closed Flow Publication
Problem: Once a valid flow texture was published, later invalid states such as feature disable, missing compute asset, failed kernel initialization, lost observer, missing GPU resources, or editor/domain transition could leave global shaders sampling stale active flow.
Solution: Add `DeactivateAbyssalFlowPublication` with a one-shot clear latch. It clears texture-active state, texture/grid globals, and cached metadata on the first invalid path, then stays quiet until the next real publication. Duplicate calls inside the same fixed step still reuse the current valid texture.
Rejected Alternatives: Leaving stale globals was rejected because consumers would keep reacting to a dead flow center. Releasing and reallocating textures on every invalid tick was rejected because it violates the persistent allocation contract.
Scalability potential: Low/Middle/High/Ultra all fail closed to fallback behavior when the producer is unavailable; the next valid dispatch warm-starts through the existing full base-curl overwrite path.
Hardware Impact: 0 us steady-state cost after the one-shot clear. Invalid-path shader global clearing is estimated 3-8 us CPU and prevents stale current sampling without resource churn.

## Decision 11: Bounded Vortex Impulse Lane
Problem: The original recursive prompt called out a future Leviathan tail-whip vortex, but direct dependencies on fauna code would be brittle under parallel agent work.
Solution: Add a decoupled public `TryQueueAbyssalVortexImpulse` API on `HectonFluidEngine`, backed by a fixed four-slot array and a high-tier-only `InjectAbyssalVortexTexture` kernel. Low tier ages impulses without injecting them.
Rejected Alternatives: Direct Leviathan component references were rejected because they violate batch decoupling. A persistent vortex fluid sim was rejected because a transient tangential velocity splat buys the visible motion at lower cost.
Scalability potential: Low = no vortex injection. Middle = existing base texture drift. High = transient tail-whip vortex. Ultra = multiple overlapping vortex impulses up to the fixed four-slot cap.
Hardware Impact: 0 us steady-state cost. Active high-tier vortex impulse costs one 32^3 compute dispatch, estimated 35-70 us GPU per live impulse, capped at four and short-lived.

## Verification Dependency Note: PDA Sonar Constants
Problem: `Hecton8.Core.csproj` verification was blocked by unrelated `PDAMapTab` references to removed individual sonar compute property IDs.
Solution: Switched the call site to the compute shader's existing packed `_SonarScalarParams` and `_SonarDispatchParams` vectors.
Rejected Alternatives: Leaving the blocker was rejected because it prevented objective compile verification. Adding dead individual shader properties was rejected because the compute shader already uses packed constants.
Scalability potential: Low/High PDA point-cloud dispatch now receives the same packed axis/step/predator/quad constants as the compute shader expects.
Hardware Impact: No runtime overhead increase; fewer compute property writes than the broken individual-ID path.

## Decision 12: Consumer Fallback And Bandwidth Hardening
Problem: The flow producer and consumers still had small integration risks: zero heat sources still uploaded the whole heat buffer, published textures could survive as lost RenderTextures, generic boids could sample the 1x1 fallback texture instead of using the structured-buffer fallback, and some shader fallback paths trusted flow vectors without a final finite guard.
Solution: Upload only `heatSourceCount` thermal slots and skip zero-count uploads; make `TryGetGpuAbyssalFlowFieldTexture` require an actually created `RenderTexture`; zero `_AbyssalFlowSpacing.w` for generic boids when only the structured buffer is active; add dot-product finite guards to vegetation, marine snow, and boid consumer fallback reads.
Rejected Alternatives: Always uploading eight heat slots was rejected as wasted PCIe/driver bandwidth. Treating fallback textures as active flow was rejected because it hides producer loss. Adding managed-side validation loops was rejected because shader-side guards are cheaper and closer to the data.
Scalability potential: Low avoids all heat uploads when geysers are disabled. Middle/High consumers fail back to zero or structured-buffer flow instead of propagating invalid vectors. Ultra keeps the same visual overkill path with tighter guardrails.
Hardware Impact: Low/MX350 saves one small but unnecessary `LockBufferForWrite` upload on no-heat frames, estimated 2-6 us CPU/driver overhead. Active high-tier frames upload only 1-8 heat records instead of always eight, saving proportional copy bandwidth while preserving visuals.

## Verification Note: 2026-05-12 Continuation
Problem: Verification was noisy because `Assembly-CSharp` with `BuildProjectReferences=false` failed when referenced temp DLLs had not been regenerated, and Unity MCP refresh was disconnected.
Solution: Rebuilt `Hecton8.Core.csproj` directly, then rebuilt full referenced `Assembly-CSharp.csproj` without suppressing project references.
Rejected Alternatives: Reporting the missing-temp-DLL pass as a source compile failure was rejected because the referenced build regenerated those assemblies successfully. Claiming Unity shader import verification was rejected because MCP refresh failed over HTTP.
Scalability potential: No runtime change.
Hardware Impact: No runtime impact. Earlier objective result: full referenced `Assembly-CSharp` build succeeded with 0 errors and 45 out-of-domain third-party/package warnings before the later restore-enabled verification pass; Unity compute-shader import remains PENDING VERIFICATION.

## Decision 13: Dead Aggregate Dispatch Removal
Problem: `HectonFluidEngine` still allocated `_gpuAbyssalAggregateBuffer`, looked up reset/surge kernels, and dispatched `ResetAbyssalFlowAggregate` every fixed-step flow update, but no source path consumed or read back that mask for the abyssal wake texture contract.
Solution: Remove the aggregate buffer allocation/release, reset-kernel lookup, reset dispatch, aggregate buffer binding, stale debug field, and unreferenced aggregate reset/detect compute kernels. Update `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md` to describe the live no-readback texture topology.
Rejected Alternatives: Reintroducing a GPU readback for biolume surge was rejected because the assignment forbids abyssal flow readbacks and the wake task only needs visual/advection output. Keeping legacy aggregate compute kernels was initially considered for import compatibility, then rejected after static references proved no live C# binding remains.
Scalability potential: Low = one fewer fixed-step compute dispatch before base curl. Middle/High/Ultra = saved dispatch overhead can be spent on visible texture wake/geyser/vortex passes instead of dead bookkeeping.
Hardware Impact: Removes one 1x1x1 reset dispatch and one raw buffer allocation from the flow path. Estimated steady saving is 3-8 us CPU/driver overhead and 1-3 us GPU scheduling overhead per flow fixed tick on low-end drivers; cold memory saving is one 4-byte raw buffer plus driver object overhead.

## Verification Note: 2026-05-12 Aggregate Strip
Problem: Full `Assembly-CSharp` no-restore failed before source compilation because `Temp/obj/Assembly-CSharp/project.assets.json` was missing.
Solution: Rebuilt `Hecton8.Core.csproj` with no restore and rebuilt full `Assembly-CSharp.csproj` with restore enabled.
Rejected Alternatives: Treating the missing assets file as a source compile error was rejected.
Scalability potential: No runtime change.
Hardware Impact: No runtime impact. Earlier objective result: `Hecton8.Core.csproj` succeeded with 0 errors; full `Assembly-CSharp.csproj` succeeded with 0 errors and 153 out-of-domain package/third-party warnings. Unity MCP console remains unreachable over HTTP; `Editor.log` search found no fresh `AbyssalFlowField` or shader error entries, but shader import is still PENDING VERIFICATION. No new build was run after the later no-build directive.

## Decision 14: Vortex Lifecycle And Stale Kernel Hardening
Problem: Queued vortex impulses used runtime world positions but were not rebased during floating-origin shifts. Invalid producer paths could also keep queued impulses alive without aging until the flow texture came back, causing delayed turbulence unrelated to the triggering event.
Solution: Rebase queued vortex impulse positions with the same AUP runtime offset as buoyancy cached positions, age queued impulses once per fixed step on invalid flow-producer paths, and clear queued impulses on disable/destroy. Remove the now-unreferenced aggregate reset/detect kernels from `AbyssalFlowField.compute` after the C# aggregate path was already stripped.
Rejected Alternatives: Keeping stale queued impulses until the next valid high-tier dispatch was rejected because it creates non-deterministic delayed wake response. Reintroducing aggregate C# bindings was rejected because the mask has no live consumer and no readback is allowed for this flow contract.
Scalability potential: Low = vortex impulses expire without GPU dispatch. Middle = no extra work beyond fixed-step aging. High/Ultra = vortex splats remain bounded to four live impulses and stay aligned after origin shifts.
Hardware Impact: AUP rebase cost is a fixed loop over at most four impulses on rare origin-shift events. Invalid-path aging is a four-slot loop once per fixed step only when impulses exist. Removing stale compute kernels reduces shader import surface and keeps runtime dispatch topology to live visual work only.

## Verification Note: 2026-05-12 No-Build Continuation
Problem: The latest instruction forbids `dotnet build` and build/compile runs.
Solution: Performed static-only verification: `rg` found no remaining aggregate-mask/reset/surge references in `HectonFluidEngine.cs` or `AbyssalFlowField.compute`; `git diff --check` on touched flow files reported only CRLF normalization warnings.
Rejected Alternatives: Running another build was rejected because the user explicitly forbade it.
Scalability potential: No runtime change beyond Decision 14.
Hardware Impact: No measured compile verification after this change. Status remains PENDING VERIFICATION until Unity shader import can be checked without violating the no-build directive.

## Decision 15: Documentation And Stale Constant Purge
Problem: After removing the aggregate kernels, stale constants and architecture text still described the dead biolume-surge threshold/readback path as live or semi-live.
Solution: Remove unused `PressureWaveVelocityThreshold` shader constants and `AbyssalBiolumeSurgeHoldSeconds` from `HectonFluidEngine`; update `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md` to state that visible turbulence now travels through the 3D flow texture with no aggregate mask allocation or readback.
Rejected Alternatives: Leaving historical text ambiguous was rejected because future agents could reintroduce the forbidden readback path. Rebuilding to prove this tiny static purge was rejected due the explicit no-build directive.
Scalability potential: Low/Middle/High/Ultra all keep the same live flow topology; documentation now points future work toward texture-side visual features instead of CPU-visible aggregate flags.
Hardware Impact: Runtime effect is negligible for constants, but shader import surface is smaller and future integration risk is reduced. No measured compile verification after this change.

## Decision 16: Published Payload Validity Gates
Problem: Legacy buffer and texture getter APIs could report active flow with non-null objects but stale/lost GPU handles or non-finite metadata after device/editor transitions.
Solution: Require `GraphicsBuffer.IsValid()` for structured-buffer publication and reject non-finite center/spacing metadata for both structured-buffer and texture payloads. Add a `Vector4` finite guard to match the existing `Vector3` guard.
Rejected Alternatives: Letting consumers discover invalid handles in their own bind code was rejected because it duplicates defensive logic across boids, marine snow, vegetation, and fauna consumers. Adding managed per-consumer validation was rejected as wider churn.
Scalability potential: Low/Middle/High/Ultra all fail closed to existing fallback buffers/textures when producer metadata is invalid.
Hardware Impact: Getter overhead is a few scalar comparisons on bind/refresh paths, below 1 us. It prevents undefined GPU sampling/binding behavior after device loss or domain transitions without allocations.

## Decision 17: Vegetation Flow Volume Boundary Parity
Problem: Vegetation texture sampling returned zero outside the 100 m flow volume, but the structured-buffer fallback clamped coordinates to the nearest edge cell. Distant plants could inherit edge-current motion when only the legacy buffer path was active.
Solution: Make `Hecton_IndirectVegetation.shader` reject out-of-bounds structured-buffer samples instead of clamping them. This matches marine snow and boid fallback behavior.
Rejected Alternatives: Keeping the clamp was rejected because it hides volume bounds and creates false current response outside the authored flow field. Expanding the flow volume was rejected because the prompt owns a fixed 100 m texture.
Scalability potential: Low/Middle fallback paths now degrade to no-flow outside the volume instead of propagating edge artifacts. High/Ultra texture paths were already bounded.
Hardware Impact: Replaces a clamp with six scalar comparisons and an early return in the fallback path. Cost is negligible; visual correctness improves for distant vegetation on fallback devices.

## Decision 18: Heat Source Count Clamp
Problem: The CPU capture path clamps thermal sources to eight, but the shader loops trusted `_AbyssalFlowHeatSourceCount` directly. A bad property write or stale integration could index past the fixed heat-source buffer.
Solution: Add `HECTON_ABYSSAL_HEAT_SOURCE_CAPACITY` in `AbyssalFlowField.compute` and clamp both structured-buffer and 3D-texture heat loops to that capacity.
Rejected Alternatives: Relying on C# capture bounds alone was rejected because the shader should fail closed when external property state is wrong. Adding dynamic buffer-length logic was rejected because the capacity is intentionally fixed for this system.
Scalability potential: Low still uses zero heat sources. High/Ultra retain up to eight vent/updraft influences with a hard shader-side cap.
Hardware Impact: Two integer clamps per dispatch path, negligible cost. Prevents undefined GPU memory access from bad heat-source counts.
