# LOG_SHINOBU_06

## 2026-05-17 - SHINOBU Somatic Kinematics

What was wrong:
- Legacy player embodiment still depended on Unity physics authority patterns that are hostile to VR comfort: Rigidbody jitter, collider snagging, and large-coordinate float drift.
- Agent 04 world sampler and historical OSHINO binaries were not guaranteed to exist, so a hard dependency would stall or crash the player locomotion stack.
- Tuning values were buried in code paths rather than exposed as live unmanaged controls.

What was done:
- Added `Assets/_Project/Scripts/Gameplay/SomaticKinematicsRuntime.cs`.
- Added DataVault-backed `PlayerKinematicState`, `PlayerStateDTO`, `PlayerBoundingSphere`, hand history, drag LUT, tuning, signal scratch, and 300-frame blackbox buffers.
- Added a Burst `SomaticKinematicsJob` for VR swim strokes, seaglide motor acceleration, 1D drag LUT, soft abyssal current advection, surface buoyancy, SDF squeeze, and speed/radius CCD.
- Added local mocks: `MockWorldSampler`, `MockSDFCollisionPlane`, and `MockFluidDensityLUT`.
- Added local partial signal payloads: `PlayerExertionSignal`, `AcousticEchoTap`, and `HapticRequestSignal`.
- Added `Dump_SHINOBU_06.h8dump` blackbox dump on non-finite KCC state.
- Added `Assets/_Project/Scripts/Editor/SomaticTunerWindow.cs` with live unmanaged sliders and SceneView vector drawing.
- Attached the runtime through `VRSomaticRuntimeBootstrap` without direct dependencies on survival, audio, haptics, or terrain systems.

Cinematic Cheats used:
- SDF cave squeeze instead of PhysX depenetration.
- Backward-hand dot-product "Dear Lie" swim thrust instead of limb fluid simulation.
- 1D velocity-magnitude drag LUT instead of volumetric water displacement.
- Soft current acceleration instead of raw current velocity shove.
- Algebraic surface spring/buoyancy instead of volume sampling.

Exact Microseconds saved:
- SDF KCC versus PhysX cave contact: estimated 20-70 us per active player fixed frame, pending profiler.
- Low-tier single-step CCD versus multi-step: estimated 30-100 us saved on MX350/toaster path.
- 1D drag LUT versus fluid displacement: estimated 15-60 us saved.
- SignalBus decoupling below thresholds: 0 us event cost when no exertion/haptic/acoustic threshold fires.
- Binary archaeology, CSV parsing, editor tuner, and blackbox dumps: 0 us in the fixed movement loop.

Verification:
- `dotnet restore Hecton8.Core.csproj` succeeded.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` is blocked by unrelated missing ecosystem/seismic/binary-layout types.
- Filtered build output reported no SHINOBU-owned errors for `SomaticKinematicsRuntime`, `SomaticTunerWindow`, `VRSomaticRuntimeBootstrap`, or `ShinobuSomatic`.
- Local scans found no `Rigidbody`, `CharacterController`, `CapsuleCollider`, `Physics.SphereCast`, `Pack = 1`, auto-setters, `private set`, `foreach`, `Debug.Log`, `.Complete()`, `.Run()`, `new List`, or TODO markers in SHINOBU-owned runtime/editor files.

Integrator note:
- Current compile wall is `[BLOCKED BY DEPENDENCY]`: missing unrelated types in `Core/BinaryLayoutManifest.cs`, `Ecosystem/EcosystemRuntimeInstaller.cs`, and `Environment/HectonSeismicTideDirector.cs`.

## 2026-05-17 - SHINOBU Ultra-Think Polish Pass

What was wrong:
- The first SHINOBU pass still cached private `NativeArray<T>` views from the DataVault. That was bounded but not strict H-Phi.
- CSV override ingestion still had managed byte-array allocation risk on the cold designer-edit path.
- Local prompt-required signal lanes existed, but current global consumers also needed canonical movement acoustic and haptic signal publishes.
- A stale `using Hecton8.World;` import survived after the concrete references were already made explicit.

What was done:
- Replaced persistent private NativeArray views with `VaultBufferHandle<T>` fields and explicit lock/unlock around scheduled jobs.
- Added `BufferID.ShinobuSomaticCsvScratch = 70128` and moved CSV file reads into a vault byte scratch span.
- Replaced legacy 16-byte binary probes with stack-span reads.
- Published canonical `MovementAcousticSignal` and `HapticRequest` alongside SHINOBU local partial SignalBus payloads.
- Removed runtime `Pack=1` from three used signal structs: `KccVelocitySignal`, `HapticRequest`, `MovementAcousticSignal`.
- Removed the stale `using Hecton8.World;`; remaining world references are fully-qualified value/helper calls.

Cinematic Cheats used:
- No new simulation realism was added. Polish preserved the cheap vestibular lies: 1D drag LUT, tetra SDF squeeze, dot-product swim thrust, algebraic buoyancy, and low-tier single-step CCD.

Exact Microseconds saved:
- No new measured microseconds are claimed. Expected cold-path improvement is removal of managed byte[] allocation during CSV/binary reads. Hot fixed-loop estimates remain unchanged and pending Unity profiler proof.

Verification:
- Re-extracted SHINOBU XML from `Docs/Tasks/CURRENT_BATCH.md`; live task count is 20.
- Final forbidden scan of SHINOBU files returned no hits for `using Hecton8.World`, `private NativeArray`, `ReadAllBytes`, `Pack=1`, Unity physics authority types, `foreach`, `new List`, `LINQ`, `Debug.Log`, or `.Complete(`.
- `git diff --check` passed on touched files; only CRLF conversion warnings remain on dirty shared files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails, but not in SHINOBU code. Current unrelated blockers are `SaveSystem/H8BinaryWorldPager.cs` missing arena/telemetry fields and `VFX/Bioluminescence/BiolumPulseSyncRuntime.cs` missing `BiolumVisualSyncJob.PredatorSignal` and `.Frame`.

Integrator note:
- SHINOBU code is not claiming Unity runtime/profiler proof. It is static-source clean and Core-build clean relative to SHINOBU-owned files under the current compile wall.

## 2026-05-17 - SHINOBU Ultra-Think Polish Pass 2

What was wrong:
- SHINOBU still held a concrete `HectonFluidEngine` field for current sampling. That was registry-routed but still a sibling-runtime dependency.
- `FixedTick` still had fallback `GlobalRegistry` reads through cached-provider fallback logic.
- CSV polling used `FileInfo`, creating avoidable cold-path heap churn.
- The fatal blackbox artifact still used `.bin` despite the latest mandate requiring `.h8dump`.

What was done:
- Replaced concrete fluid sampling with cached `IWeatherService.GlobalCurrentVector` plus a deterministic local triangle-wave abyssal-flow fallback.
- Removed fixed-loop fallback reads of `GlobalRegistry.VRSomatic`, `GlobalRegistry.ScalabilityTier`, and `GlobalRegistry.DataVault`.
- Replaced `FileInfo` CSV polling with timestamp-first `File.GetLastWriteTimeUtc` and vault-backed stream reads only after the file changes.
- Changed the blackbox dump target to `Docs/AgentLogs/Dump_SHINOBU_06.h8dump`.

Cinematic Cheats used:
- The no-weather fallback current is a triangle-wave scalar fake. It buys vestibular water motion without CPU fluid truth or concrete fluid-engine coupling.

Exact Microseconds saved:
- No measured claim. Expected fixed-loop saving is removal of registry fallback reads and concrete fluid flow sampling. Expected SlowTick saving is one avoided managed `FileInfo` allocation per CSV poll.

Verification:
- SHINOBU forbidden scan after pass 2 returned no hits for `HectonFluidEngine`, `FileInfo`, `ReadAllBytes`, `private NativeArray`, `Pack=1`, Unity physics authority tokens, `foreach`, `new List`, `LINQ`, `Debug.Log`, or direct `.Complete(`.
- `git diff --check` passed on touched files; only CRLF conversion warnings remain in shared dirty files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails, but not in SHINOBU code. Current unrelated blockers are in `TetherInstance.cs`, `GlobalTelemetryBus.cs`, `AI/Ecosystem/ShinobuEcosystemBalancer.cs`, `SpatialAudioManager.cs`, and `Construction/DroneFleetManager.cs`.

## 2026-05-17 - SHINOBU Ultra-Think Polish Pass 3

What was wrong:
- SHINOBU still had negative `DefaultExecutionOrder` attributes on the runtime and XR bootstrap. That hid cadence in Unity script-order metadata while the system already owns explicit dispatcher/bootstrap registration.

What was done:
- Removed `DefaultExecutionOrder` from `SomaticKinematicsRuntime`.
- Removed `DefaultExecutionOrder` from `VRSomaticRuntimeBootstrap`.
- Kept execution authority in `IFixedTickable`, `IPostFixedTickable`, `ISlowTickable`, hot-swap callbacks, and `GameBootstrapper` events.

Cinematic Cheats used:
- No new physical simulation was added. The pass preserves the cheap path: dot-product hand strokes, 1D drag LUT, tetra SDF squeeze, algebraic buoyancy, triangle-wave current fallback, and low-tier single-step CCD.

Exact Microseconds saved:
- No measured microsecond claim. This pass removes an implicit Unity scheduling dependency rather than a hot ALU path.

Verification:
- SHINOBU forbidden scan after pass 3 returned no hits for `DefaultExecutionOrder`, `HectonFluidEngine`, `FileInfo`, `ReadAllBytes`, `private NativeArray`, `Pack=1`, Unity physics authority tokens, `foreach`, `new List`, `LINQ`, `Debug.Log`, or direct `.Complete(`.
- `git diff --check` passed on touched files; CRLF warnings remain in shared dirty files.
- `dotnet restore Hecton8.Core.csproj` succeeded after missing temp assets.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails, but not in SHINOBU code. Current unrelated blockers are `Construction/DroneFleetManager.cs`, `Core/HomeostasisBrain.cs`, `AI/Ecosystem/ShinobuEcosystemBalancer.cs`, and `World/HectonIndirectVegetationRenderer.cs`; duplicate source warning remains for `HectonPhysicsContract.cs`.

## 2026-05-18 - SHINOBU Ultra-Think Polish Pass 4

What was wrong:
- The Burst KCC job still trusted DataVault tuning values after CSV/legacy/editor input. A poisoned tuning buffer could feed NaN drag, negative radius, or invalid CCD counts into the solver.
- Drag and CSV fractional parsing had mathematically safe denominators in practice, but not explicit survival-grade denominator guards.

What was done:
- Added `SanitizeTuning(ref SomaticKinematicsTuningData)` at the job boundary.
- Clamped every tuning field used by the solver: drag, stroke, seaglide, buoyancy, current, fatigue, SDF epsilon, radius, sea level, gravity, blend range, chest offset, thresholds, mass, damping, max speed, CCD steps, and LUT count.
- Hardened drag denominator with `math.max(0.0001f, denominator)`.
- Replaced CCD raw speed length with finite `lengthsq` plus guarded `sqrt` before step-count conversion.
- Replaced CSV parser `fraction / scale` with guarded reciprocal multiplication.
- Rechecked `SomaticTunerWindow.cs`: it is already wrapped in `#if UNITY_EDITOR`.

Cinematic Cheats used:
- No new realism. The pass protects the existing cheap lies: dot-product hand strokes, 1D drag LUT, triangle-wave current fallback, tetra SDF squeeze, algebraic buoyancy, low-tier single-step CCD.

Exact Microseconds saved:
- No saving is claimed. This pass spends a tiny fixed scalar clamp cost to prevent NaN cascades and poisoned tuning stalls.

Verification:
- SHINOBU forbidden scan after pass 4 returned no hits for `DefaultExecutionOrder`, `HectonFluidEngine`, `FileInfo`, `ReadAllBytes`, `private NativeArray`, `Pack=1`, Unity physics authority tokens, `foreach`, `new List`, `LINQ`, `Debug.Log`, direct `.Complete(`, stale raw `fraction / scale`, unsafe drag denominator, or raw `math.length(velocity)` CCD speed.
- `git diff --check` passed on touched files; CRLF warnings remain in shared dirty files.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` still fails, but not in SHINOBU code. Current unrelated blocker is `GlobalPhysicsStateManager.cs` missing `WakeRequestSignal`.
