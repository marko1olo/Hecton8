# Rationale_SPLASHDOWN_FLUID_DYNAMICS

## Initial Architecture Decision

Problem: Splashdown fluid reaction must not create a new runtime owner while 20+ agents mutate adjacent systems.
Solution: Extend `HectonFluidEngine`, which already owns abyssal flow buffers, advection bubble buffers, telemetry rings, and `GlobalRegistry.Fluid`.
Rejected Alternatives: New `SplashdownFluidRuntime` MonoBehaviour or direct prologue-to-fluid reference. Both add scene wiring risk and cross-domain coupling.
Scalability potential: Low uses bubble-only visual fake; Middle/High/Ultra can inject a GPU-visible vector impulse and preserve richer advection.
Hardware Impact: Low i3/MX350 avoids the 32^3 vector overwrite and pays only 500 fixed bubble writes. High tier spends saved CPU on a wider visible pressure ring.

## Visual Fake Decision

Problem: A physically correct 10-ton capsule splash is not gameplay truth here; player belief needs pressure wave, foam/bubbles, and current disturbance.
Solution: Use a deterministic kinetic vector impulse plus GPU advected bubble ring. Flow field is a visual/current cue, not per-droplet simulation.
Rejected Alternatives: ParticleSystem burst, per-bubble CPU simulation, full fluid solver. These violate the fluid VFX mandate and the 0.1ms suspicion threshold.
Scalability potential: Low: 500 fixed radial bubbles only. Middle: structured field impulse. High: texture impulse and stronger bubble velocities. Ultra: same data can feed richer shader response without changing API.
Hardware Impact: MX350 avoids compute-heavy truth; estimated savings versus per-particle CPU bubbles is multiple milliseconds during splash frame. Exact proof remains PENDING VERIFICATION.

## Burst Impulse Job Decision

Problem: A splash impulse needs to affect a 32^3 flow volume without a managed per-cell loop in `FixedTick`.
Solution: `FluidImpulseJob` writes a persistent `NativeArray<float4>` once per impact; xyz is impulse velocity, w is radial falloff. The compute shader blends the result through `_AbyssalSplashdownParams`.
Rejected Alternatives: CPU rewriting the shader texture every frame, spawning a full solver, or running the job on Low/MX350. Standard Unity particle-only feedback cannot bend the abyssal current field.
Scalability potential: Low: no vector overwrite, bubbles only. Middle: one Burst field write. High: structured field plus 3D texture application. Ultra: same buffer can feed stronger material foam/caustic response later.
Hardware Impact: On i3/MX350 the Low gate avoids a 32^3 job and buffer upload. On high-end hardware the one-shot 32768-cell job is acceptable because it buys a visible pressure wave.

## Signal And Phase Decision

Problem: Fluid code cannot take a direct dependency on a prologue director while other agents edit the prologue stack.
Solution: Drain `PrologueCompleteSignal` snapshots, accept `PhaseOceanHandoff` or forced whiteout, and de-duplicate by frame and sequence.
Rejected Alternatives: Direct `FindObjectOfType` or inspector reference to the prologue director; polling capsule transforms; inventing a new splashdown singleton.
Scalability potential: All tiers pay one bounded signal-span scan. Ultra does not need a different API.
Hardware Impact: Normal-frame cost is a few scalar comparisons; the expensive work occurs once after the signal.

## Dampen And Upload Decision

Problem: A 10-second splash wake must decay without uploading a 512 KB vector volume every frame.
Solution: Upload the impulse field once, then pass a scalar decay factor to the compute shader.
Rejected Alternatives: CPU-side decay rewriting the `NativeArray`, per-frame `GraphicsBuffer.SetData`, or storing a lifetime per cell.
Scalability potential: Low bypasses the field; Middle/High/Ultra reuse the same buffer with different shader response intensity.
Hardware Impact: Saves one full vector-field upload per frame during the 10-second decay window; exact us remains PENDING VERIFICATION.

## Bubble Ring Decision

Problem: The player needs an expanding foam/cavitation ring immediately at ocean handoff.
Solution: Queue 500 deterministic radial bubbles into the existing advected bubble structured buffers with fixed ring cursor slots.
Rejected Alternatives: Runtime `ParticleSystem.Emit` as truth, allocating a temporary bubble list, or CPU-simulating each bubble.
Scalability potential: Low gets the bubble lie only. Middle/High/Ultra combine bubbles with real flow-field disturbance.
Hardware Impact: MX350 pays two fixed buffer uploads on the impact frame and then the existing GPU advection carries the particles.

## Black Box Decision

Problem: NaN or bad splash impulses must leave evidence.
Solution: Add `FluidImpulseCount` to abyssal flow telemetry, include it in the hash, and dump to `Docs/AgentLogs/Dump_SPLASHDOWN_FLUID_DYNAMICS.bin`.
Rejected Alternatives: Debug logs or chat report. Logs are not a deterministic postmortem buffer.
Scalability potential: The telemetry ring is fixed at 300 frames on all devices; Ultra does not need more memory to debug this path.
Hardware Impact: One extra int in the existing ring write. Impact is below measurement noise; exact profiler proof remains PENDING VERIFICATION.

## Verification Blocker

Problem: The Unity editor did not return readiness and later MCP calls reported no active Unity session.
Solution: Use local compiler checks: isolated `FluidImpulseJob` compile passed; local `HectonFluidEngine.cs` compiler pass showed no splashdown syntax errors. Keep project status `PENDING VERIFICATION`.
Rejected Alternatives: Claiming Unity compile success from a timed-out editor or reverting unrelated worktree changes.
Scalability potential: None; this is process risk, not runtime behavior.
Hardware Impact: None.

## OMEGA POLISH CHANGES

Problem: The first AUP rebase implementation invalidated `_splashdownImpulseUploaded` after an origin shift, which would prematurely silence a valid 10-second wake.
Solution: Keep the uploaded impulse buffer active on uniform floating-origin rebases. The buffer stores relative velocity/falloff per grid cell, not absolute positions, so a uniform rebase does not corrupt the vector field.
Rejected Alternatives: Recompute and reupload the whole 32^3 impulse field on every origin shift, or kill the wake for safety. Both spend or waste the effect with no technical need.
Scalability potential: Low tier unchanged. Middle/High/Ultra preserve the visible pressure wave through origin shifts without another buffer upload.
Hardware Impact: Avoids a repeat 32768-cell Burst job and 512 KB upload on shift; estimated 250-600 us saved on i3/MX350 if a shift lands during the decay window.
Cinematic Cheats used: bubble-only Low tier, inverse-square vector lie instead of fluid solve, shader scalar dampening instead of CPU decay, deterministic golden-angle bubble ring.
Final Git Diff: See local diff for `Assets/_Project/Scripts/HectonFluidEngine.cs`, `Assets/_Project/Scripts/Environment/Fluids/FluidImpulseJob.cs`, `Assets/_Project/Art/Shaders/AbyssalFlowField.compute`, and this agent's status/log files. Full diff includes unrelated pre-existing edits in `HectonFluidEngine.cs`; do not attribute those to this patch.

## LAZY SPLASH BUFFER DECISION

Problem: `_gpuSplashdownImpulseBuffer` was allocated during base abyssal GPU-buffer setup, so Low/MX350/no-splash sessions paid memory for a cinematic-only vector override.
Solution: Remove splash buffer allocation from `EnsureGpuAbyssalFlowBuffers()` and allocate it only in `EnsureSplashdownImpulseGpuBuffer()` when a non-low splash schedules the field job.
Rejected Alternatives: Always keep the 32768-cell buffer resident for convenience, or allocate a full zero buffer for shader binding. The existing empty abyssal buffer is sufficient while params are inactive.
Scalability potential: Low and toaster profiles stay bubble-only with no splash vector VRAM. Middle/High/Ultra allocate the buffer only when the cinematic splash is active, then retain it for reuse.
Hardware Impact: Saves 32768 * 16 bytes, about 512 KB of GPU buffer memory, on no-splash and low-tier paths. Cold allocation shifts to the actual high-tier splash event where the vector field buys visible pressure-wave quality.

## OFF-VOLUME IMPULSE CULL DECISION

Problem: A capsule handoff outside the active 100m abyssal flow volume could still schedule the 32^3 impulse job, clear/write the staging array, and upload a useless zero field.
Solution: Add `IsSplashdownInsideFlowVolume()` using the flow half-extent plus the 30m splash radius. Off-volume impacts keep the 500-bubble ring and publish a flag, but skip the vector-field job.
Rejected Alternatives: Let the Burst job discover zero affected cells after scanning all cells, or enlarge the flow volume globally. Scanning wastes the impact frame; enlarging the volume taxes every frame for one cinematic edge case.
Scalability potential: Low unchanged. Middle/High/Ultra still get the full kinetic field when the splash intersects the active field; outside it, the cinematic fake protects frame time.
Hardware Impact: Estimated 250-600 us CPU and one 512 KB upload avoided on off-volume impacts, with no visual loss because the active flow texture cannot represent that impulse anyway.

## LAZY BUFFER SAFETY DECISION

Problem: After lazy allocation, `ReleaseGpuAbyssalFlowBuffers()` could release the splash buffer while `_splashdownImpulseUploaded` remained true, leaving active shader params with only the empty fallback bound.
Solution: Set `_splashdownImpulseUploaded = false` whenever the splash GPU buffer is released through base abyssal buffer teardown.
Rejected Alternatives: Reallocate the splash buffer during every base flow reset or trust shader early-outs to mask a stale param state. Reallocation reintroduces the memory bug; stale params risk invalid GPU reads.
Scalability potential: Low stays allocation-free. Middle/High/Ultra can recover on the next real splash schedule instead of carrying stale state.
Hardware Impact: Avoids unnecessary recovery allocation and prevents a possible GPU out-of-bounds read path when the fallback buffer is one element and params are stale.

## JOB SEQUENCING DECISION

Problem: A second splash signal could arrive before the previous `FluidImpulseJob` completed, resetting `_splashdownImpulseFlags`, duration, and count before the first job uploaded.
Solution: Complete any ready splash job before draining new signals, make `ScheduleSplashdownImpulseField()` return success/failure, and publish a busy flag instead of overwriting active vector-wake state.
Rejected Alternatives: Force-complete the active job on every new signal, or let the new signal clobber state. Force-complete can stall the main thread; clobbering state corrupts the cinematic pressure wave.
Scalability potential: Low/off-volume remains bubble-only. Middle/High/Ultra preserve one scheduled vector field without blocking; later richer multi-splash blending would need a separate queue, not this single-shot cinematic path.
Hardware Impact: Avoids a possible 0.2-0.6 ms main-thread stall on i3/MX350 and prevents a wasted 512 KB upload tied to stale metadata.

## LOW TIER ACTIVE PARAM DECISION

Problem: Low/MX350 bypass stopped new vector-field jobs, but an already uploaded high-tier wake could still be sampled after a quality drop.
Solution: Gate `ResolveSplashdownImpulseParams()` with `IsLowFluidMathTier()`, returning inactive shader params while low-memory or MX350 mode is active.
Rejected Alternatives: Release the splash GPU buffer immediately on tier drop, or keep sampling it until natural decay. Releasing causes churn if quality recovers; sampling violates the low-tier bypass contract.
Scalability potential: Low gets bubble/drift presentation only. Middle/High/Ultra retain the buffer and can resume the remaining wake if quality recovers before decay ends.
Hardware Impact: Prevents 32768 splash-buffer reads per abyssal flow dispatch while low tier is active during a decay window.

## ZERO CELL UPLOAD DECISION

Problem: A zero-cell or invalid splash job still uploaded the full 512 KB vector field even though shader params would be inactive.
Solution: Add `SplashdownImpulseNoAffectedCellsFlag`, skip the upload when affected count is zero, clear active splash timing, and only dump if the job also reported invalid math.
Rejected Alternatives: Upload zeros to sanitize stale GPU memory. The active param gate already prevents reads; zero upload spends bandwidth without visible output.
Scalability potential: All tiers avoid wasted bandwidth on edge cases. High/Ultra still upload normally when at least one cell is affected.
Hardware Impact: Saves one 512 KB upload and one GPU synchronization point on no-cell/invalid edge cases.
