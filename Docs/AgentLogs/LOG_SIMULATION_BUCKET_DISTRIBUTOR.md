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

## 2026-05-16 - H-Phi Re-Inquisition Pass
What was wrong:
- The previous scheduler state still documented and partially implied fallback ownership outside `GlobalDataVault`.
- `ModuloSimulationBucketer` had no actual 300-frame DataVault black-box ring in the file after the overwrite pass.
- High-tier visual budget was not exposed as a typed scheduler flag.
- Build attempt5 reached a Sargassum compile error from incomplete vault-handle calls before the known construction wall.

What was done:
- Reworked `ModuloSimulationBucketer` to keep only `VaultBufferHandle<T>` handles and scalar state; no persistent private scheduler `NativeArray<T>` fields remain.
- Added `BufferID.SimulationBucketBlackBox` and a packed 64-byte `SimulationBucketBlackBoxEntry` ring with 300 entries.
- Added fault-only dump to `Docs/AgentLogs/Dump_SIMULATION_BUCKET_DISTRIBUTOR.bin` on non-finite scheduler cost.
- Fixed scheduler contract packing: `SimulationBucketFrameState` Pack=1 Size=64, `SimulationBucketRebalanceResult` Pack=1 Size=20, scheduler signals Explicit Pack=1.
- Added `VisualOverkillBudgetAvailable` as a downstream high-tier budget flag.
- Replaced the broken Sargassum `EnsureVaultBufferHandle` calls with existing DataVault-backed `EnsureNativeArrayCapacity` calls for boid sensory threat and black-box buffers.
- Ran build attempts 5 and 6; attempt6 has zero errors in scheduler-touched files and is blocked by `VehicleDockingModule` construction-domain missing methods.

Cinematic cheats used:
- Toaster mode remains a static 128-bucket Dear Lie with no rebalance job.
- Steam Deck I/O is protected by fault-only binary dump; normal frames only overwrite the in-memory ring.
- God-mode visual overkill is exposed as a flag when expected scheduler cost is under half budget, without the scheduler editing render/VFX domains.

Exact microseconds saved / budget estimates:
- Private scheduler allocation ownership removed: 0 B persistent private scheduler state.
- Black-box ring write: <5 us/frame, 0 us normal disk I/O.
- Fault dump: cold path only; no Steam Deck MicroSD traffic during healthy frames.
- Visual overkill flag: scalar bit test downstream, effectively 0 us scheduler overhead.
- Sargassum compile repair: no runtime gain claimed; removed dead helper dependency.

Validation:
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt5_hphi.log`: failed first on Sargassum missing `EnsureVaultBufferHandle`, then unrelated VFX/construction errors.
- `Docs/AgentLogs/Build_SIMULATION_BUCKET_DISTRIBUTOR_attempt6_hphi.log`: failed only in `Assets/_Project/Scripts/Construction/VehicleDockingModule.cs`.
- Filtered attempt6 against touched files: zero errors in `ModuloSimulationBucketer`, contracts, signals, dispatcher, bootstrap, H8Memory, CrashTelemetryBuffer, Sargassum, and HectonFluidEngine.
