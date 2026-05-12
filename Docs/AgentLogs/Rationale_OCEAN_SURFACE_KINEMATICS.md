# Rationale: OCEAN_SURFACE_KINEMATICS

Agent: HYDRO_MECHANIC
Status: PENDING VERIFICATION

## Mandates Loaded Before Code
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- CORE_Weather_Abyssal_FlowField_Currents.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt
- REND_GPU_Sovereignty.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt

## Pre-Code Analysis
Target: Replace mass-object Crest height sampling with deterministic Burst Gerstner sampling over NativeArray S.O.A. buffers.

Affected systems: hydrodynamic buoyancy sampling, physics force routing, AUP rebasing, surface weather wind drift, sargassum presentation coupling, telemetry/recon logs.

Zero GC proof: hot path must use Allocator.Persistent NativeArray/NativeQueue, index-based loops only, no LINQ, no managed allocations, no string formatting, no UnityEngine.Transform/Vector3 inside Burst jobs.

State check: registry capacity must be fixed or grown only outside hot path; sleep bitmask must cull >500m with hysteresis; queue drains must be owned by physics router; NativeContainers require deterministic dispose.

Rule quote: "Direct Rigidbody.AddForce calls are owned by PhysicsApplySystem after force packet gather" and "Default solution is a deterministic presentation fake."

## Decisions

### D0: System Boundary
Problem: Prompt asks for Burst Gerstner buoyancy while existing project ownership is unknown.
Solution: Recon existing code first, then add only decoupled structs/interfaces or a narrow runtime service under existing Hydrodynamic/World ownership.
Rejected Alternatives: Directly editing Crest or MapMagic internals is forbidden third-party coupling; direct dependencies on other agents' unfinished systems are forbidden.
Scalability potential: Low/Middle/High/Ultra tiering will reduce octave count and active floater count on weak hardware while allowing 16-octave presentation on high-end.
Hardware Impact: Expected low-end i3/MX350 gain is main-thread stall removal from mass Crest queries; exact microseconds pending profiler/compile verification.

### D1: Registry Ownership
Problem: Prompt requested FloaterPositions/BuoyancyResults but HectonFluidEngine already owns dense BuoyancyObject state.
Solution: Expose the existing Persistent NativeArray<float3> positions and NativeArray<float> wave offsets as the S.O.A. surface instead of adding a second registry.
Rejected Alternatives: A new HYDRO_MECHANIC singleton would race the existing fluid runtime and duplicate Rigidbody handles.
Scalability potential: Low/Middle/High/Ultra all share one cache-coherent registry; tier behavior is handled by wave octave count and sleep mask.
Hardware Impact: i3/MX350 avoids an extra N-object copy and managed lookup; estimated 35 us at 256 floaters, pending profiler.

### D2: Gerstner Sampling
Problem: Existing CPU wave query sampled only three fallback components and used a triangle-wave cheat.
Solution: Fill a persistent 16-slot Gerstner spectrum from weather waves, use math.sincos in Burst, and cap active octaves by hardware tier.
Rejected Alternatives: Crest.SampleHeightHelper was rejected for mass objects because it is managed and not job-parallel.
Scalability potential: Unknown/Mobile: 1 octave; Low/MX350: 4; Mid: 8; High: 12; Ultra: 16 with storm amplitude multiplier.
Hardware Impact: Low tier trades spectral richness for stable fixed-step cost; top-tier spends saved cycles on overkill wave detail.

### D3: Shore Height Fallback
Problem: Near shore, wave-only buoyancy can put floating bodies below terrain lips and MapMagic shelves.
Solution: Pass MapMagic R16 TerrainHeightSamplePayload as read-only alias into WaveQueryJob and resolve max(waveSurfaceY, terrainY) inside a 14m shore band.
Rejected Alternatives: TerrainData.GetInterpolatedHeight and MapMagic managed calls inside Burst are impossible and too slow.
Scalability potential: Same code path on all tiers; fallback activates only when payload and bounds are valid.
Hardware Impact: R16 bilinear sample is four ushort reads; expected 12-40 us saved versus managed terrain query clusters.

### D4: Force Ownership
Problem: Burst can compute forces, but Rigidbody mutation must remain centralized.
Solution: Keep HectonFluidEngine as force packet producer and route results through PhysicsForceRouter.
Rejected Alternatives: Direct Rigidbody.AddForce would violate PhysicsApplySystem ownership and break replay/debug ordering.
Scalability potential: Queue routing is independent of visual tier and can be drained by existing physics budget controls.
Hardware Impact: Neutral microsecond delta; correctness gain is deterministic force application order.

### D5: AUP Phase Stability
Problem: Runtime-space Gerstner phase would jump when floating origin subtracts root-transform offsets.
Solution: Sample waves with runtime XZ + HectonFloatingOrigin.CurrentTotalOffset, and rebase cached position arrays on IOriginShiftListener callbacks.
Rejected Alternatives: Resetting phase after shifts would create visible wave pops and unstable buoyancy impulses.
Scalability potential: Same absolute-coordinate phase on toaster and Ultra; higher tier only changes octave count.
Hardware Impact: One float2 add per wave query; cost is negligible versus eliminating post-shift correction churn.

### D6: Splash Signal Gate
Problem: Surface debris can spam splash VFX if every water contact becomes an event.
Solution: Gate impact events in BuoyancyJob by previous-above/current-below transition, depth > 1m, and velocity threshold; publish DebrisSpawnSignal only during main-thread event drain.
Rejected Alternatives: Spawning debris or VFX from Burst is illegal; publishing every submersion tick would flood GlobalSignals.
Scalability potential: Low tier gets the same sparse signal contract; Ultra can spend the saved event budget on denser splash assets.
Hardware Impact: i3/MX350 avoids managed splash churn during bobbing debris; estimated 8-20 us saved during busy surface contacts.

### D7: Math LOD
Problem: Sixteen Gerstner octaves on every floater is visual overkill on MX350-class hardware.
Solution: Cap physics wave octaves by GlobalRegistry.ScalabilityTier: Unknown/Mobile 1, Low/MX350 4, Mid 8, High 12, Ultra 16.
Rejected Alternatives: A balanced single middle setting wastes low-end frame time and undersells high-end surface richness.
Scalability potential: Low is a simple sine/4-octave lie; Ultra runs the full synthesized spectrum plus storm multiplier.
Hardware Impact: i3/MX350 saves roughly 55-180 us at 256 floaters versus 16-octave evaluation.

### D8: Sleep Bitmask
Problem: Distant debris still costs wave sampling and force output even when the player cannot read it.
Solution: Add a persistent byte sleep mask with 500m sleep and 495m wake hysteresis before scheduling force work.
Rejected Alternatives: Rigidbody.Sleep alone is not authoritative because active debris can stay awake offscreen.
Scalability potential: Low-end devices can sleep broad debris fields; Ultra still wakes nearby objects with the same deterministic hysteresis.
Hardware Impact: Estimated 70-220 us saved in populated ocean fields by skipping far buoyancy work.

### D9: Surface Normal Alignment
Problem: Life pods need to match wave tilt, but main-thread Transform alignment would reintroduce the stall pattern.
Solution: WaveQueryJob writes finite-difference normals; BuoyancyJob consumes target up and applies stability torque, using dominant-axis approximation outside high tier.
Rejected Alternatives: Transform reads/writes in the buoyancy loop and per-object Crest normals both violate the hot-path contract.
Scalability potential: Low/MX350 uses coarse dominant-axis tilt; High/Ultra uses normalized finite-difference normals.
Hardware Impact: Estimated 10-45 us saved versus main-thread surface normal sampling at 100+ objects.

### D10: Sargassum Coupling
Problem: Sargassum mats must ride the same surface without becoming rigidbody floaters.
Solution: Publish the first three Gerstner wave components as shader globals and evaluate matching lift in Hecton_IndirectVegetation; keep physical coupling one-way through SargassumGlobalDrag density samples.
Rejected Alternatives: Registering every mat as a BuoyancyObject would explode Rigidbody count; CPU sampling per mat is a main-thread trap.
Scalability potential: Low tier still receives one/four-octave shared lift; Ultra receives richer wave globals and local shader bob for visual overkill.
Hardware Impact: Avoids millisecond-scale rigidbody/mat simulation on i3/MX350; vertex fake cost is paid only by rendered instances.

### D11: Wind Advection
Problem: Surface floaters need weather-driven drift without a separate integrator.
Solution: Read WeatherRuntimeSnapshot.GlobalWindVector from the SurfaceWeather service and fold a surface-faded lateral force into BuoyancyJob.
Rejected Alternatives: Transform translation by wind would bypass PhysicsForceRouter; separate wind job would duplicate registry reads.
Scalability potential: Low tier gets a single vector force; Ultra can layer storm turbulence and analytical flow on top.
Hardware Impact: Adds one vector multiply path in Burst; avoids managed per-object wind components, estimated 4-15 us saved at scale.

### D12: Zero-GC Contract
Problem: The original stall pattern is caused by managed surface queries and per-object work.
Solution: Use Allocator.Persistent arrays for floater positions, wave offsets, wave spectrum, sleep mask, normal outputs, event scratch, and telemetry; only resize on cold capacity growth.
Rejected Alternatives: Per-frame request lists, LINQ, managed queues, or TempJob churn would violate the fixed-step contract.
Scalability potential: Low and Ultra share the same memory ownership; tier changes alter math cost, not allocation behavior.
Hardware Impact: Removes GC pressure from mass buoyancy; low-end silicon avoids collection spikes during debris fields.

### D13: Burst API Boundary
Problem: Burst jobs cannot touch UnityEngine objects or managed APIs.
Solution: Keep Transform/Rigidbody/Shader/Time access in main-thread gather/schedule/apply; jobs consume only blittable structs and NativeContainers.
Rejected Alternatives: Passing Transform or Rigidbody handles into jobs is illegal and would disable Burst.
Scalability potential: API-free jobs stay SIMD-friendly across all hardware tiers.
Hardware Impact: Preserves worker-thread scheduling and avoids main-thread stalls.

### D14: Crest Recon
Problem: The prompt specifically calls out Crest.SampleHeightHelper as the mass-object hazard.
Solution: Scan active `Assets/_Project/Scripts` with ripgrep and write the result to RECON_OCEAN_SURFACE_KINEMATICS.md.
Rejected Alternatives: Searching archived folders as runtime proof would add noise and misidentify non-active history.
Scalability potential: Confirms this implementation does not depend on managed Crest query helpers for any tier.
Hardware Impact: Removes the suspected main-thread Crest helper path from the ocean buoyancy domain.

### D15: Compile Wall
Problem: Unity editor validation is unavailable and the project build is currently blocked outside this domain.
Solution: Ran `dotnet build Hecton8.Core.csproj`; filtered compile output shows no HectonFluidEngine.cs errors, while observed walls moved from `SubmarineStructuralGrid.cs(654,17) CS1501` to `UI/PDAMapTab.cs(92)` missing StructLayout/LayoutKind symbols.
Rejected Alternatives: Claiming Burst compile verification without an editor session is a fake report.
Scalability potential: Burst verification remains pending until the external compile wall/editor session is fixed.
Hardware Impact: No runtime claim until Burst validation can run; implementation remains designed for i3/MX350 steady-state budgets.

### D16: Self-Review
Problem: The recursive prompt requires proof that Gerstner math and storm escalation were rechecked after implementation.
Solution: Re-extracted the agent prompt, scanned HectonFluidEngine and the vegetation shader for math.sin/math.cos/Crest helper usage, and confirmed `math.sincos` plus storm amplitude multiplier in the Gerstner path.
Rejected Alternatives: Marking tasks done from memory would violate anti-amnesia rules.
Scalability potential: Review confirmed low-tier octave caps and high-tier storm overkill are present.
Hardware Impact: No new runtime cost; this is verification only.

## OMEGA POLISH CHANGES

Honest calculations replaced with cinematic cheats:
- Low-tier surface normals no longer pay finite-difference Gerstner sampling. WaveQueryJob writes flat up vectors when `DistanceMath.IsHighQualityTier(GlobalRegistry.ScalabilityTier)` is false; BuoyancyJob already snaps low-tier normal use to dominant-axis behavior.
- Height-only Gerstner sampling no longer builds unused horizontal displacement. `SampleHeight` now calls `ComputeHeight`, retaining `math.sincos` for phase consistency while discarding horizontal displacement math.
- Sargassum mats use shader-side Gerstner lift from the first three published wave components plus reduced local bob, not rigidbody truth.

Scalability matrix:
- Unknown/Mobile: 1 octave, flat normal output, no finite-difference normal cost, surface advection only.
- Low/MX350: 4 octaves, flat normal output, 500m sleep mask, shader sargassum lift uses cheap globals.
- Mid: 8 octaves, same deterministic sleep and event gates.
- High: 12 octaves, finite-difference normals enabled, storm turbulence enabled.
- Ultra: 16 octaves, full synthesized spectrum with storm multiplier and high-tier normal fidelity.

Hot path impact:
- Removed four extra `SampleHeight` calls per floater on non-high tiers. At 256 floaters and 4-octave Low tier, avoided up to 4096 Gerstner component height evaluations per fixed step.
- Replaced y-only height evaluation from full float3 displacement to scalar height math. Horizontal displacement is still available visually through shader presentation, not physics.
- All new buffers remain Persistent; no steady fixed-step managed allocation was added.

Silo / domain justification:
- `Assets/_Project/Scripts/HectonFluidEngine.cs` is inside hydrodynamic physics ownership.
- `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader` is a justified cross-domain presentation edit for Task 10 only: sargassum mats ride the same water surface without rigidbodies or CPU sampling.
- No third-party Crest or MapMagic assets were modified.

Final git diff:
- Modified: `Assets/_Project/Scripts/HectonFluidEngine.cs`
- Modified: `Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader`
- Added: `Docs/Tasks/Status_OCEAN_SURFACE_KINEMATICS.md`
- Added: `Docs/AgentLogs/Rationale_OCEAN_SURFACE_KINEMATICS.md`
- Added: `Docs/AgentLogs/RECON_OCEAN_SURFACE_KINEMATICS.md`
- Focused diff stat: `HectonFluidEngine.cs` +1117/-157, `Hecton_IndirectVegetation.shader` +138/-25.

Verification:
- `dotnet build Hecton8.Core.csproj`: BLOCKED outside domain by `UI/PDAMapTab.cs(92)` StructLayout/LayoutKind errors. Earlier external wall: `SubmarineStructuralGrid.cs(654,17)` CS1501.
- Unity MCP `validate_script`: unavailable after refresh timeout; reason `no_unity_session`.
- Burst job API scan: no Transform/Vector3/Rigidbody/GameObject/Shader/Time/Application/Debug usage inside WaveQueryJob/BuoyancyJob slice.

Status remains PENDING VERIFICATION per AGENTS.md. No measured profiler or Unity Burst compile proof exists in this session.

## CONTINUATION PASS

### D17: Compile-Gate Namespace Fix
Problem: `dotnet build Hecton8.Core.csproj` was blocked by `UI/PDAMapTab.cs` missing `StructLayout` / `LayoutKind` symbols, preventing ocean compile verification.
Solution: Added the missing `using System.Runtime.InteropServices;` import only. No UI behavior or layout logic was changed.
Rejected Alternatives: Leaving the project red would block evidence-based ocean verification; broad UI edits are outside this agent's domain.
Scalability potential: No runtime ocean scalability effect. This is a compile-gate exception so hydrodynamic code can be validated.
Hardware Impact: Zero frame-time effect. Validation unblock only.

### D18: Wave-Query Culling Correction
Problem: The 500m sleep bitmask and frame-stagger simulation mode skipped final buoyancy force work, but `WaveQueryJob` could still spend Gerstner and terrain fallback samples before the later `BuoyancyJob` return.
Solution: Added a `simulationMode != 0` early return inside `WaveQueryJob` before base-depth, Gerstner, finite-difference normal, or MapMagic heightmap work.
Rejected Alternatives: Keeping culling only in BuoyancyJob was a half-measure because inactive floaters still paid wave-query cost.
Scalability potential: Unknown/Mobile and Low/MX350 skip all wave octaves for sleeping/staggered slots; High/Ultra preserve nearby fidelity while broad debris fields stop consuming worker time.
Hardware Impact: On i3/MX350, each inactive floater now saves up to 4 `math.sincos` Gerstner evaluations on Low tier, 1 on Unknown/Mobile, and avoids terrain bilinear reads where shore fallback would have activated. Exact profiler numbers pending.

### D19: Surface Normal SIMD Hygiene
Problem: High-tier exact surface normal alignment still called `math.normalize`, hiding a sqrt/divide path in a hot job branch.
Solution: Replaced it with explicit `math.rsqrt(math.max(lengthSq, epsilon))` after finite fallback selection.
Rejected Alternatives: Trusting normalize to lower optimally is weaker than the mandate's explicit rsqrt preference.
Scalability potential: Low/MX350 remains on dominant-axis/flat-normal cheats; High/Ultra keep exact normals with cleaner scalar math.
Hardware Impact: Small per-active-floater SIMD hygiene gain; exact microseconds pending profiler.

### D20: Verification Update
Problem: Prior status recorded external compile walls that have since been cleared or fixed by a minimal namespace import.
Solution: Re-ran `dotnet build Hecton8.Core.csproj`; result is `Build succeeded. 0 Warning(s) 0 Error(s)`.
Rejected Alternatives: Reporting the old blocked state after a clean build would be stale evidence.
Scalability potential: Compile verification now covers the hydrodynamic implementation and adjacent shader/UI compile surface, but runtime tier claims still require Unity editor/Burst/profiler verification.
Hardware Impact: No measured hardware impact yet. Unity MCP still returns `no_unity_session` for `validate_script` and console reads, so Burst compile, GCMonitor, and profiler data remain pending.

Updated verification:
- `dotnet build Hecton8.Core.csproj`: PASS, 0 warnings, 0 errors.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- `rg Crest.SampleHeightHelper|SampleHeightHelper Assets/_Project/Scripts`: no hits.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script` and `read_console`: unavailable, reason `no_unity_session`.

Status remains PENDING VERIFICATION per batch prompt because no Unity editor Burst compile, profiler, or GCMonitor proof exists in this session.

## CONTINUATION PASS 2

### D21: GPU Buoyancy Parity Gate
Problem: The legacy GPU buoyancy path can activate at high object counts and skip `WaveQueryJob`. That bypasses the mandated 16-slot Burst Gerstner spectrum, AUP phase offset, MapMagic shore fallback, sleep-mode wave-query cut, and finite-difference surface normals.
Solution: Gate GPU buoyancy dispatch/readback behind `GpuBuoyancySurfaceParityAvailable=false`. The CPU Burst wave query remains authoritative until the compute path has full parity.
Rejected Alternatives: Keeping the GPU path active would produce stale one-frame readbacks with only three weather waves and runtime-space phase. Upgrading the compute shader to 16 waves plus AUP/terrain parity is larger than this domain pass and would still not satisfy the explicit Burst-job assignment.
Scalability potential: Low/MX350 keeps deterministic Burst LOD and sleep cuts; High/Ultra can re-enable GPU only after matching the same data contract. Correctness is prioritized over a non-parity acceleration path.
Hardware Impact: No microsecond saving claimed. It removes AsyncGPUReadback dispatch/readback churn for this surface path but shifts high-count work back to Burst; profiler remains required for the net cost.

### D22: Pending AUP Rebase
Problem: `OnOriginShift` returned without rebasing cached floater positions if a buoyancy job was still running. That could leave `_positions` / `_previousPositions` in the old origin frame and cause false splash/depth artifacts on the next step.
Solution: Accumulate pending shift offsets while the job is running. After job completion, skip stale force application for that rare frame, apply the pending rebase, and gather fresh positions on the next fixed step.
Rejected Alternatives: Blocking the main thread until the job completes would violate the nonblocking fixed-step contract; ignoring the shift corrupts cache coherence.
Scalability potential: All tiers share the same rare-event path. Low-end hardware avoids a blocking wait; high-end hardware avoids stale force output during AUP transitions.
Hardware Impact: One scheduled force packet batch is discarded only during a concurrent origin shift. Expected frame cost is lower than a forced complete; exact spike delta pending profiler.

Updated verification 2:
- `dotnet build Hecton8.Core.csproj`: PASS, 0 warnings, 0 errors after GPU/AUP hardening.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script`: unavailable, reason `no_unity_session`.

Status remains PENDING VERIFICATION per AGENTS.md. Unity MCP editor validation is still unavailable in this session, so Burst compile, GCMonitor, profiler, and visual evidence remain unproven.

## CONTINUATION PASS 3

### D23: Completed-But-Undrained AUP Result Guard
Problem: The pending AUP rebase gate only deferred when the scheduled buoyancy job was active and not completed. If a job completed but was not drained yet, an origin shift could rebase caches immediately and still allow the pre-shift force batch to apply afterward.
Solution: Treat any `_scheduledBuoyancyJobActive` state as stale across an origin shift. Accumulate the shift, defer rebase until drain, skip the stale result batch, and gather fresh Rigidbody state on the next fixed step.
Rejected Alternatives: Checking only `!_scheduledBuoyancyHandle.IsCompleted` was incomplete. Blocking to drain immediately would violate the nonblocking fixed-step policy.
Scalability potential: Same rare-event policy on Low through Ultra. Weak hardware avoids a forced complete; high-end hardware avoids applying stale hydrodynamic truth during floating-origin transitions.
Hardware Impact: One scheduled force batch is skipped only during an origin-shift overlap. No steady-state cost; exact spike delta pending profiler.

### D24: Whole-File Hot-Math Scan
Problem: Prior verification focused on the Burst job slice; a whole-file math scan was needed after multiple passes.
Solution: Scanned `HectonFluidEngine.cs` for `math.normalize(`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, and `math.length(`. No matches remain.
Rejected Alternatives: Slice-only scans can miss helper functions called from jobs or fixed-step paths.
Scalability potential: Confirms the fluid owner is aligned with the i3 reciprocal-square-root mandate after hardening.
Hardware Impact: No new runtime cost; this is verification.

Updated verification 3:
- `dotnet build Hecton8.Core.csproj`: PASS, 0 warnings, 0 errors after the completed-job AUP guard.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- Whole-file hot-math scan: no `math.normalize(`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(` in `HectonFluidEngine.cs`.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script`: unavailable, reason `no_unity_session`.

Status remains PENDING VERIFICATION per AGENTS.md. Unity editor Burst compile, GCMonitor, profiler, and visual validation are still not proven.

## CONTINUATION PASS 4

### D25: Shader Ocean-Lift Direction Parity
Problem: `Hecton_IndirectVegetation` evaluated ocean lift with a cheap direction magnitude approximation. That was acceptable as a visual fake, but it could slowly desynchronize sargassum phase against the CPU Gerstner direction used by buoyancy.
Solution: Normalize the shader wave direction with `rsqrt(max(dot(direction, direction), epsilon))`, matching the C# path's phase direction policy while keeping the vertex fake lightweight.
Rejected Alternatives: Leaving the approximation would preserve a tiny ALU saving but invite visible shader/physics phase drift. Replacing vegetation motion with rigidbody truth is rejected outright because sargassum is presentation, not physics.
Scalability potential: Low tier still uses only the published cheap wave globals and local bob. High/Ultra gain tighter visual parity between ocean floaters and sargassum without adding CPU sampling.
Hardware Impact: Adds a small vertex-side reciprocal square-root for the active ocean wave globals. No fixed-step CPU cost; profiler number not claimed.

### D26: Evidence Supersession Discipline
Problem: Earlier append-only log entries recorded blocked builds and later 0-warning builds. The latest local build now succeeds but emits 47 warnings from Unity package cache / third-party packages, so the current evidence must supersede older summaries without deleting them.
Solution: Keep old log lines as historical facts and append the current result: 0 errors, 47 external/package warnings observed, no first-party ocean warning reported in the latest output, Unity MCP still unavailable.
Rejected Alternatives: Rewriting prior append-only reports would destroy audit history. Claiming 0 warnings after the latest build changed would be a false report.
Scalability potential: No runtime effect. This protects integration decisions from stale verification state.
Hardware Impact: No frame-time effect. The hydrodynamic status remains PENDING until Unity editor Burst, GCMonitor, profiler, and visual validation are actually measured.

Updated verification 4:
- `dotnet build Hecton8.Core.csproj`: PASS, 0 errors, 47 warnings observed in Unity package cache, Crest, GPUInstancer, and ShaderGraph output.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or `math.normalize(` tokens.
- Shader ocean lift now uses `rsqrt` for direction normalization.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script` on `Assets/_Project/Scripts/HectonFluidEngine.cs`: PASS, 0 diagnostics.
- Unity MCP `refresh_unity` with script compile request: timed out after 60 seconds waiting for editor readiness.
- Unity MCP console reads after the timeout: 0 errors, 0 warnings.

Status remains PENDING VERIFICATION per AGENTS.md. Unity editor Burst compile, GCMonitor, profiler, and visual validation are still not proven.

## CONTINUATION PASS 5

### D27: Gerstner Hot-Path Rsqrt Cleanup
Problem: `HectonGerstnerWater` still used `math.normalizesafe` inside the Gerstner direction path and finite-difference normal cleanup. That hid the exact normalization cost in the most repeated ocean wave loop.
Solution: Replaced those sites with explicit `dot + math.rsqrt` helpers: `ResolveDirectionOrDefault` for wave vectors and `ResolveNormalOrUp` for finite-difference normals.
Rejected Alternatives: Leaving `normalizesafe` in place would probably compile acceptably, but it violates the local i3 reciprocal-square-root mandate and weakens static hot-path inspection.
Scalability potential: Unknown/Mobile and Low/MX350 benefit on every active octave. High/Ultra keep the same visual fidelity but use clearer SIMD-friendly normalization.
Hardware Impact: Small per-octave CPU hygiene gain; exact microseconds remain PENDING PROFILER because no reference hardware profiler run exists.

Updated verification 5:
- `dotnet build Hecton8.Core.csproj`: PASS, 0 warnings, 0 errors.
- Whole-file hot-math scan: no `math.normalize(`, `normalizesafe`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(` in `HectonFluidEngine.cs`.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or forbidden normalize/sqrt tokens.
- `rg Crest.SampleHeightHelper|SampleHeightHelper Assets/_Project/Scripts`: no active hits.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script` retry: unavailable, reason `no_unity_session`; console reads also failed because MCP ping was not answered.

Status remains PENDING VERIFICATION per AGENTS.md. Unity editor Burst compile, GCMonitor, profiler, and visual validation are still not proven.

## CONTINUATION PASS 6

### D28: Sargassum Wave Global Cache
Problem: `PublishOceanSurfaceWaveUniforms` pushed all six wave parameter globals every fixed publish. Only the meta/time vector needs to move every pass for phase parity; the wave vectors are often unchanged.
Solution: Cache the last six wave parameter `Vector4` values and call `Shader.SetGlobalVector` only when the corresponding vector changes. Keep `_HectonOceanSurfaceWaveMeta` live every publish so shader lift uses the same weather time accumulator as physics.
Rejected Alternatives: Setting meta time to zero and letting the shader use `_Time.y` would reduce one more global write, but it would break strict weather-time parity. Moving sargassum mats to physical floaters remains rejected because this is presentation-only vegetation motion.
Scalability potential: Low/MX350 avoids redundant main-thread global updates when spectrum is stable; High/Ultra still get the same richer wave presentation without extra CPU churn.
Hardware Impact: Saves up to six `Shader.SetGlobalVector` calls per fixed publish when wave parameters are unchanged. Exact microseconds remain PENDING PROFILER.

Updated verification 6:
- First `dotnet build Hecton8.Core.csproj` reached `Build succeeded` text but hit the 240s shell timeout, so it was not counted as a clean exit.
- Second `dotnet build Hecton8.Core.csproj`: PASS, exit 0, 1 warning, 0 errors. The warning is external Crest editor code: `Packages/com.waveharmonic.crest/Editor/Scripts/Utility/Shared/Helpers.cs(240,43) CS0649`.
- Whole-file hot-math scan: no `math.normalize(`, `normalizesafe`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(` in `HectonFluidEngine.cs`.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or forbidden normalize/sqrt tokens.
- Unity MCP `validate_script` and `read_console`: unavailable, reason `no_unity_session`.

Status remains PENDING VERIFICATION per AGENTS.md. Unity editor Burst compile, GCMonitor, profiler, and visual validation are still not proven.

## CONTINUATION PASS 7

### D29: No-Floater Ocean Wave Publication
Problem: When `_objects.Count == 0`, the fluid engine released NativeArray buffers and returned before publishing sargassum ocean wave globals. Sargassum mats could therefore keep stale lift from the last active floater batch, even though they are intentionally not rigidbody floaters.
Solution: Publish the first three sanitized weather Gerstner waves from local blittable structs before the no-object return. This keeps vegetation presentation coupled to weather without allocating buffers or registering mats as physics bodies.
Rejected Alternatives: Keeping NativeArrays alive solely for sargassum would waste memory in empty scenes. Registering sargassum mats as `BuoyancyObject` instances remains rejected because it destroys the vertex-fake performance model.
Scalability potential: Low/MX350 still publishes cheap primary wave globals while physics buffers sleep. High/Ultra keep weather-driven visual overkill even in shots with only vegetation and no debris floaters.
Hardware Impact: No fixed-step microseconds claimed. This adds only bounded shader-global publication when no floaters exist, and avoids CPU rigidbody/height-sampling work for sargassum.

### D30: Ad-Hoc Height Query Parity
Problem: `GetWaterHeightAtPosition` sampled only the three raw weather waves and missed the tiered harmonic synthesis plus storm multiplier used by the Burst buoyancy path.
Solution: Added shared wave synthesis helpers and routed the height query through the same active-octave budget, AUP XZ phase, sanitized primary waves, harmonic expansion, and storm amplitude multiplier.
Rejected Alternatives: Leaving the method as a cheap 3-wave query would be faster for rare callers but creates physics/query phase disagreement. Calling Crest remains rejected for this domain.
Scalability potential: Unknown/Mobile evaluates 1 octave, Low/MX350 4, Mid 8, High 12, Ultra 16. The same code path now scales consistently across public query and Burst job inputs.
Hardware Impact: No frame-time saving claimed. This is correctness parity for non-mass callers; mass objects still use the Burst `NativeArray` path.

### D31: Process-Wide Shader Global Ownership
Problem: Unity shader globals are process-wide. Clearing them unconditionally during `OnDisable` / `OnDestroy` would let a duplicate fluid engine destroyed during singleton registration wipe the active runtime's ocean globals.
Solution: Added an owner guard so ocean globals are cleared only when the instance is the registered fluid runtime or the editor is outside play mode.
Rejected Alternatives: Never clearing globals leaves stale waves across scene teardown. Unconditional clearing breaks concurrent/duplicate runtime safety.
Scalability potential: Same behavior across Low through Ultra; it protects presentation state without adding per-floater work.
Hardware Impact: Cold-path only. No steady fixed-step cost.

Updated verification 7:
- `dotnet build Hecton8.Core.csproj -v:minimal`: PASS, exit 0, 47 warnings, 0 errors. Warnings are external/package/editor output from URP/Core RP, Crest, GPUInstancer, ShaderGraph, and WaveHarmonic Crest.
- Static hot-math scan: no `math.normalize(`, `normalizesafe`, `.normalized`, `Mathf.Sqrt`, `math.sqrt(`, or `math.length(` in `HectonFluidEngine.cs`.
- Focused WaveQueryJob/BuoyancyJob slice scan: no Transform, Vector3, Rigidbody, GameObject, Shader, Time, Application, Debug, or forbidden normalize/sqrt tokens.
- `rg Crest.SampleHeightHelper|SampleHeightHelper Assets/_Project/Scripts`: no active hits.
- `git diff --check`: CRLF normalization warnings only.
- Unity MCP `validate_script` and `read_console`: unavailable, HTTP request failure to `127.0.0.1:8088/mcp`.

Status remains PENDING VERIFICATION per AGENTS.md. Unity editor Burst compile, GCMonitor, profiler, and visual validation are still not proven.
