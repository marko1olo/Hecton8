# LOG_SIMULATION_BUCKET_DISTRIBUTOR

## 2026-05-16 - Master Modulo Time-Slicing Orchestrator
What was wrong:
- `SystemDispatcher` owned tick phases, but bucket consumers still derived local bucket uniforms from private masks, allowing frame clustering.
- `ModuloSimulationBucketer` had static modulo state but no load/cost EWMA, no DataVault-backed double buffer, no dynamic rebalance job, no jitter telemetry, and no phase-budget warning path.
- Crash telemetry stored active bucket count where the prompt required `JitterVarianceMs`.
- No typed scheduler signal existed for global bucket sync or mathematically impossible 60 FPS warnings.

What was done:
- Extended `ISimulationBucketer` and `SimulationBucketFrameState` with pre-simulation cost, jitter variance, expected bucket load, interpolation alpha, pacing flags, rebalance sequence, and entity-cost reporting.
- Rebuilt `ModuloSimulationBucketer` around NativeArray SOA buffers: front bucket table, work bucket table, entity cost EWMA, bucket load EWMA, rebalance scratch loads, rebalance result, and frame state buffer.
- Added DataVault buffer IDs and vault-aware initialization with H8Memory fallback.
- Added Burst `LoadBalancingJob` with 60-frame high-tier cadence, mutation-version protection for structural bucket changes, and low-tier static distribution.
- Wired `SystemDispatcher` as the single time owner: measures PRE_SIMULATION, advances bucketer, broadcasts `_SimulationBucketInterpolationAlpha`, emits `SimulationBucketSyncSignal`, emits `FramePacingWarningSignal`, and commands homeostasis once per offending frame.
- Registered typed signal lanes for simulation bucket sync and frame pacing warnings.
- Routed `SargassumMicroFaunaBoids` and `HectonFluidEngine` bucket uniforms through `ISimulationBucketer`; fallbacks use `SimulationBucketConstants`, not private `%16/%8` policy.
- Updated black-box scheduler telemetry so `GpuFrameTime` carries sanitized `JitterVarianceMs`; active slow bucket count remains packed in `AiStatePacked`.
- Performed Omega polish: removed unused Burst job input and stopped cost EWMA reports from invalidating pending rebalances.

Cinematic cheats used:
- Low tier uses a static 128-bucket distribution and skips dynamic rebalance.
- High tier spends the saved pacing budget on accepting real-time EWMA rebalance results every 60 frames.
- Visual smoothing is a single global scalar broadcast, not per-system physical interpolation.
- Homeostasis kill path sheds non-critical VFX, particle advection, distant fauna steering, slow tick, and 0.8 time dilation instead of simulating through an impossible frame.

Exact microseconds saved / budget estimates:
- Per-system modulo debt removal target: 20 us/frame worst-spike reduction.
- SOA entity bucket reads target: 8 us/frame cache benefit.
- Global DataVault bucket state target: 5 us/frame from stable native access.
- Low-tier static fake target: 70 us saved per 60 frames by skipping rebalance.
- Dynamic rebalance budget target: 80 us per 60-frame rebalance, off main structural path.
- Black-box telemetry target: <5 us/frame.
- AUP coupling avoided: 0 us/frame added.

Validation:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` failed with broad external compile errors.
- Errors-only attempts 2 and 3 were captured in `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt2.log` and `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt3.log`; blockers were external to touched scheduler files.
- Final post-polish attempt captured in `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt4_final.log`; it timed out behind external dependency errors and filtered to zero errors in touched files.
- External blockers observed: missing `Hecton8.VFX.Wakes`, missing `IDockingAutopilotService` / `ActiveSplineData`, interface drift in `EcosystemDirector`, and duplicate/member drift in other agents' determinism/lighting code.
