# LOG_SPLASHDOWN_FLUID_DYNAMICS

## Session Start

What was wrong: Prologue completion already exists, but `HectonFluidEngine` has no dedicated kinetic splashdown impulse consumer.
What was done: Prompt extracted, mandates selected, existing fluid/advection/signal architecture scanned.
Cinematic Cheats used: Visual fake first; bubble ring and bounded vector impulse instead of fluid simulation.
Exact Microseconds saved: PENDING VERIFICATION. Static estimate only until Unity/profiler data exists.

## Final Report - Kinetic Splash Vector Field

What was wrong: Ocean handoff had no kinetic fluid response. The prologue capsule could hit the water while the abyssal flow field and GPU-advection bubble buffers stayed unaware.

What was done:
- Extended `HectonFluidEngine` as the existing fluid owner; no new singleton or direct prologue dependency.
- Consumed `PrologueCompleteSignal` from `SignalBus<T>` and de-duplicated per frame/sequence.
- Added `FluidImpulseJob` under `Hecton8.Environment.Fluids`; isolated local compile passed against Unity 6000.4.1f1 assemblies.
- Added fixed persistent splashdown impulse upload/stats arrays and a GPU structured buffer.
- Injected `_AbyssalSplashdownImpulseBuffer` into `AbyssalFlowField.compute` for both structured flow and 3D flow texture updates.
- Spawned 500 deterministic radial advected bubbles into the existing bubble structured buffers.
- Added Low/MX350/low-memory bypass: bubble ring only, no 32^3 vector overwrite.
- Added `FluidImpulseCount` to abyssal telemetry hashing and dump output, with dump path `Docs/AgentLogs/Dump_SPLASHDOWN_FLUID_DYNAMICS.bin`.
- Omega polish removed unnecessary origin-shift upload invalidation so the 10-second wake survives uniform floating-origin rebases.

Cinematic Cheats used: inverse-square vector impulse instead of fluid solve; golden-angle bubble ring; scalar shader dampening instead of per-frame CPU decay; Low-tier bubble-only lie.

Exact Microseconds saved:
- Low/MX350 vector-field bypass: estimated 250-600 us on impact frame versus 32768-cell CPU/Burst field work plus upload.
- Shader scalar decay: estimated one 512 KB upload avoided per frame for 10 seconds after impact.
- `rsqrt` normalization instead of `sqrt`/divide in impulse and bubble velocity: estimated 30-70 us across the field job.
- Singleton/direct scene coupling avoided: ~8 us cold registration/lookup risk, zero hot-path dependency.

Verification:
- `mcp__unityMCP__.refresh_unity`: timed out after 60s.
- `mcp__unityMCP__.validate_script` and console reads: Unity session unavailable.
- `dotnet build Hecton8.Core.csproj --no-restore`: blocked by unrelated generated-project reference failures outside this patch.
- Local isolated compile of `FluidImpulseJob.cs`: PASS.
- `git diff --check` on touched patch paths: PASS.
- Status remains PENDING VERIFICATION because full Unity compile/runtime proof is blocked.

## Follow-up Audit - Splashdown Scalability and Safety

What was wrong: The splashdown vector buffer was allocated during normal abyssal GPU setup, so Low/MX350/no-splash sessions paid about 512 KB VRAM for a rare high-tier cinematic. Off-volume splashes could also schedule a 32768-cell impulse job and upload a useless zero field. Lazy allocation introduced one safety edge: releasing base flow buffers could leave stale active splash params.

What was done:
- Added explicit splashdown flag constants for Low tier, outside-flow-volume, job-invalid, and invalid-input states.
- Added `IsSplashdownInsideFlowVolume()` and skipped vector-field scheduling when the impact cannot intersect the active 100m flow volume plus 30m radius.
- Moved `_gpuSplashdownImpulseBuffer` creation out of `EnsureGpuAbyssalFlowBuffers()` and into `EnsureSplashdownImpulseGpuBuffer()`, called only by non-low vector-field scheduling.
- Added upload-size guards before `GraphicsBufferUploadUtility.UploadNativeArray()` pushes the splash field.
- Disabled `_splashdownImpulseUploaded` when base abyssal GPU buffers release the splash buffer, preventing active params with the empty fallback buffer.

Cinematic Cheats used: bubble-only fallback for Low/off-volume cases; bounded AABB cull instead of a wasted field solve; lazy vector buffer only when the high-tier pressure wave can be visible.

Exact Microseconds saved:
- Low/no-splash cold path: saves about 512 KB GPU buffer residency and one cold `GraphicsBuffer` creation.
- Off-volume high-tier splash: estimated 250-600 us CPU saved by skipping the 32^3 job, plus one 512 KB upload avoided.
- Base flow reset during/after splash: avoids reintroducing a lazy-buffer recovery allocation and removes stale GPU-read risk.

Verification:
- `git diff --check` on splashdown-touched paths: PASS except Git's existing LF-to-CRLF warning for `HectonFluidEngine.cs`.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`: BLOCKED by 130 pre-existing missing-reference errors outside this patch path.
- Unity MCP `read_console`: BLOCKED, no active Unity session.
- Isolated Roslyn probe for `FluidImpulseJob.cs`: BLOCKED this run by missing local `System.Text.Encoding.CodePages` dependency in Unity Mono Roslyn; previous isolated PASS remains recorded.

## Follow-up Audit - Job Sequencing and No-Work Uploads

What was wrong: Splash state could be overwritten by a second signal while the first `FluidImpulseJob` was still active. Low-tier mode could still sample a previously uploaded vector wake after a runtime quality drop. Zero-cell or invalid jobs still uploaded the full 512 KB vector field.

What was done:
- Added `SplashdownImpulseJobBusyFlag`, `SplashdownImpulseUploadFailedFlag`, and `SplashdownImpulseNoAffectedCellsFlag`.
- Completed ready splash jobs before draining new splash signals, while still refusing to block the main thread on unfinished jobs.
- Changed `ScheduleSplashdownImpulseField()` to report success/failure so busy or failed vector scheduling publishes telemetry without resetting an active wake.
- Gated `ResolveSplashdownImpulseParams()` by low-tier state, so Low/MX350/low-memory never pays splash buffer reads during decay.
- Changed `UploadSplashdownImpulseBuffer()` to return success/failure and skip the 512 KB upload when the job affected zero cells.

Cinematic Cheats used: single active cinematic vector wake instead of multi-splash fluid blending; bubble-only fallback for busy, low, and off-volume cases; no-cell jobs become telemetry only.

Exact Microseconds saved:
- Busy signal path: avoids force-completing a Burst job on the main thread; estimated 200-600 us stall avoided on i3-class CPU.
- Low-tier active decay: prevents 32768 splash buffer reads per abyssal flow dispatch while low tier is active.
- Zero-cell job: avoids one 512 KB GPU upload and upload synchronization on edge cases.

Verification:
- `git diff --check` on splashdown-touched paths: PASS except Git LF-to-CRLF warnings.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`: TIMED OUT after 120s; log shows the same generated-project missing-reference wall, with `HectonFluidEngine.cs(47,27)` still the only HectonFluidEngine line before timeout.
- Unity MCP `read_console`: BLOCKED by HTTP transport failure to `127.0.0.1:8088/mcp`.
- Killed leftover `dotnet` build child processes from the timed-out verification run.
