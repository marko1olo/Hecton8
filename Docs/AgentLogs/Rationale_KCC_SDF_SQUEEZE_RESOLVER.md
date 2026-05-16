# Rationale_KCC_SDF_SQUEEZE_RESOLVER

STATUS: VERIFIED MASTER GRADE / KCC SDF SQUEEZE IMPLEMENTED / BUILD GREEN WITH 4 FOREIGN CS0649 WARNINGS

## Decision 0 - Scope Lock
Problem: KCC tight-gap traversal touches physics, voxel SDF, player signals, telemetry, haptics/audio, and gas dynamics. Direct concrete references across those domains would create compile and ownership risk.
Solution: Keep code authority under `Assets/_Project/Scripts/Physics/KCC/` unless an existing cross-domain interface/signal already exists. Use Burst-compatible structs, DataVault or local contract adapters only where the repo exposes them, and typed signal lanes where available.
Rejected Alternatives: Standard Unity `Physics.CapsuleCast`, `OnCollisionStay`, singleton manager polling, and direct audio/gas concrete calls were rejected because the prompt and mandates forbid them or make them hot-path allocation/dependency risks.
Scalability potential: Low uses 4-tap tetrahedral SDF gradient and bounded push interpolation; Middle uses normal 6-tap where cadence allows; High/Ultra can drive camera roll and richer scrape feedback from the same scalar stress without increasing collision truth cost.
Hardware Impact: Estimated gain for low-end silicon as i3/MX350 is 50-120 us per active squeeze frame versus main-thread capsule/collision-stay logic, pending profiler proof.

## Decision 1 - SDF Push Instead Of Capsule Repair
Problem: The existing player kinematics path sampled SDF but converted solid overlap into `FaultSolidTeleport`, which preserves determinism but creates hard snaps on jagged voxel edges.
Solution: Added `Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs`, a Burst `IJob` that samples `VoxelSdfTexture3D`, computes an open-space normal, caps correction speed to 1 m/s, and applies a 60% forward speed penalty only while the SDF penetration is positive.
Rejected Alternatives: Unity `Physics.CapsuleCast`, `OnCollisionStay`, and rigidbody impulse repair were rejected because they add broad-phase dependency, GC/managed callback risk, and non-SDF behavior in the exact tight-gap path.
Scalability potential: Low/MX350 uses 4 tetrahedral taps; Mid/High uses 6 axis taps; Ultra reuses the same normal for camera roll and feedback instead of increasing collision truth complexity.
Hardware Impact: Low-tier expected active-frame saving is 35-80 us versus six ray/capsule probes; high-tier spends saved CPU on roll/haptic/acoustic response, not deeper collision loops.

## Decision 2 - AUP And Vault Ownership
Problem: SDF sampling must survive floating-origin shifts, and player kinematic hot state was still locally owned in persistent arrays.
Solution: The squeeze job receives AUP absolute `double3` plus floating-origin offset before texture-coordinate conversion. Resolver runtime buffers now prefer `GlobalDataVault` for position, velocity, intended movement, flow velocity, last-valid position, sync states, hand targets, telemetry, fault flags, probe batches, and SDF squeeze results. H8Memory fallback is cold bootstrap insurance only when the vault is absent.
Rejected Alternatives: Runtime `float3` world offsets alone were rejected because they accumulate drift during AUP shifts; always allocating local NativeArrays was rejected because it violates DataVault sovereignty.
Scalability potential: Low devices keep one shared SOA cache line for player kinematics; high devices can consume the same vault state for richer IK/VFX without adding per-system copies.
Hardware Impact: Estimated i3/MX350 gain is 5-15 us and lower memory churn by sharing vault buffers already used by player state systems.

## Decision 3 - Decoupled Feedback And Physiology
Problem: Tight-gap traversal needs suit scrape, stress, and oxygen load without direct dependencies on concrete audio, haptic, or gas implementations.
Solution: Emitted existing typed lanes: `PlayerStateSignal`, `HapticRequest`, `AcousticPingSignal`, `PhysiologyStateSignal`, and `IGasDynamicsSolver.TryApplyPlayerRoomCarbonDioxideEquivalentPressure` through `GlobalRegistry`.
Rejected Alternatives: Direct audio source playback, direct haptic device calls, and concrete gas-room mutation were rejected because they would cross domain ownership and break signal-lane segregation.
Scalability potential: Low devices receive bounded fake scrape feedback; High/Ultra devices can overdrive downstream reactive VFX/audio from the same stress scalar.
Hardware Impact: Signal publication is expected below 10 us per active squeeze frame; avoids persistent per-effect polling.

## Decision 4 - Homeostasis And Black Box
Problem: SDF sampling is cheap but still suspicious under global pressure, and NaN/solid overlap failures need post-mortem evidence.
Solution: When `SignalBusRegistry.SystemStress01 > 0.8`, the resolver samples at a 5-frame cadence and interpolates cached push-out. Squeeze events write the 300-frame telemetry ring and the fault dump now includes `Dump_KCC_SDF_SQUEEZE_RESOLVER.bin`.
Rejected Alternatives: Disabling squeeze under stress was rejected because it would reintroduce hard stuck states; per-frame expensive overkill under stress was rejected by the Frame Time Dictatorship.
Scalability potential: Low uses slow-cadence interpolation; Middle runs normal cadence; High/Ultra use the same telemetry to justify richer cinematic roll and scrape intensity.
Hardware Impact: Under stress, low-end devices avoid roughly 4 of every 5 SDF gradient samples, saving approximately 25-60 us across sustained squeeze frames.

## Decision 5 - Multiplatform Layout And Vault Eviction Pass
Problem: The first implementation left several runtime arrays as local-primary allocations and left NativeArray payload structs with sequential Pack=4 layout. On ARM64/Quest that is a padding ambiguity; on long sessions it also weakens DataVault ownership.
Solution: Added BufferIDs `PlayerKinematicFlowVelocity` through `PlayerKinematicSdfSqueezeResults` and `PlayerMotorScheduledSweepCommands` through `PlayerMotorKinematicRepairTargetResults`. Routed every `PlayerKinematicsRuntime` persistent buffer plus the player motor sweep/repair command-result caches through vault-first allocation, and converted resolver payload structs to explicit `Pack = 1` layouts with fixed sizes: 64-byte `SdfSqueezeResult`, 80-byte runtime telemetry, 64-byte sync state, 32-byte accumulator, 32-byte hand target, and 64-byte player telemetry.
Rejected Alternatives: Keeping H8Memory local arrays as the primary path was rejected because systems would still own data privately. Relying on compiler sequential layout was rejected because it leaves platform padding to the ABI. Replacing the remaining broad motor sweep with SDF-only movement was rejected in this pass because that sweep is a general non-alloc probe batch and its voxel-proxy branch already defers tight-gap correction to SDF sampling.
Scalability potential: Low/MX350 keeps one vault-owned compact SOA path and 4-tap SDF math. Middle uses standard axis gradients. High uses camera roll and feedback from the same SDF normal. Ultra can feed downstream salt/silt/hull-dent VFX through existing typed signals without increasing collision truth cost inside KCC.
Hardware Impact: Low-end i3/MX350 expected saving is another 4-12 us from reduced duplicate cache churn and cleaner vault aliasing, including the motor sweep cache. Quest/Android risk is reduced by explicit payload offsets. Steam Deck I/O pressure remains unchanged because the resolver only writes disk dumps on faults. PC 4090 keeps the visual-overkill budget in downstream VFX lanes instead of burning CPU on extra collision probes.

## Decision 6 - Single Squeeze Lane And High-Tier Fluid Impulse
Problem: The older player motor SDF branch and the new runtime resolver both emitted scrape haptic/acoustic feedback. That creates duplicate effects, hides ownership, and makes signal tuning nondeterministic. The motor fallback also sampled SDF from runtime float coordinates instead of reconstructing the sample from AUP double-space.
Solution: Converted all remaining locomotion `StructLayout` attributes to `Pack = 1`, routed motor-side SDF sample coordinates through `AbsoluteUniversePosition.ToAbsoluteDouble3()` minus the current floating-origin offset, and made `PlayerStateSignal.StateSqueezing` the single squeeze lane. The runtime bridge now emits physiology, gas load, haptic, acoustic, and high/ultra-only `FluidImpulseSignal` feedback from that lane.
Rejected Alternatives: Keeping duplicate motor haptic/acoustic broadcasts was rejected because it violates signal-lane ownership. Adding rendering-specific salt crystal or hull dent code from locomotion was rejected because that belongs to downstream VFX/rendering systems; locomotion now emits a typed fluid impulse they can consume without extra collision truth.
Scalability potential: Low/MX350 gets the cheap signal-only path and no fluid impulse. Middle keeps normal scrape feedback. High/Ultra spend saved CPU on dynamic fluid impulse for volumetric silt/wake overkill while retaining deterministic KCC truth.
Hardware Impact: Duplicate feedback collapse avoids an estimated 8-12 us on active motor-side squeeze frames. AUP hardening costs 0-2 us and removes drift-class sampling errors. High/Ultra fluid impulse spends roughly 3-8 us downstream only when the tier can afford it.

## Decision 7 - Final Validation Without False Ownership
Problem: The previous final build was blocked by a foreign Core XR API call (`XRDisplaySubsystem.TryRequestDisplayRefreshRate`) outside PHYSICS/LOCOMOTION ownership. The current tree no longer contains that call, so the only defensible action was to revalidate rather than claim a cross-domain repair.
Solution: Re-extracted the KCC XML assignment, confirmed the current XR runtime path, and reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`. The build now exits 0 with 0 warnings and 0 errors, captured in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_xr_validation.exit.txt`.
Rejected Alternatives: Editing Core XR from the locomotion prompt was rejected unless the current source still proved the blocker. Claiming profiler-measured microsecond savings was rejected because this pass measured compile success, not frame timings.
Scalability potential: Low remains 4-tap SDF plus stress cadence interpolation; Middle keeps 6-axis gradient when pressure allows; High/Ultra retain camera-roll and fluid-impulse overkill through typed lanes without adding collision truth cost.
Hardware Impact: Measured validation elapsed time was 90.42 seconds (90420000 us). Runtime savings remain engineering estimates until profiler capture: low-tier active squeeze saving is estimated at 73-167 us per frame from SDF-vs-capsule repair, DataVault sharing, stress cadence, and duplicate feedback collapse.

## Decision 8 - Motor-Side Rsqrt Hardening
Problem: The SDF squeeze job already used `math.rsqrt(math.max(...))`, but motor-side squeeze/sweep helpers still relied on prior magnitude comparisons before calling `math.rsqrt`. That is not explicit enough for mobile NaN containment, especially when a squared magnitude can become non-finite.
Solution: Added finite checks and `math.max(..., MinVectorMagnitudeSq)` denominators to displacement direction resolution, `SafeNormal`, voxel-proxy slide fallback, tangent-slide projection, and no-trig quaternion normalization in `HectonPlayerMotor`.
Rejected Alternatives: Leaving comparison-only guards was rejected because NaN comparisons are false in a way that can silently reach rsqrt. Replacing the motor sweep solver was rejected because the existing deferred sweep lane is outside the SDF kernel and already uses DataVault-backed command/result buffers.
Scalability potential: Low/MX350 keeps the cheap branch and avoids NaN recovery spikes. Middle/High/Ultra retain the same visual-overkill signal path without extra samples or renderer ownership changes.
Hardware Impact: Runtime saving is 0 us; this is stability armor. The latest validation build elapsed 105.25 seconds (105250000 us) and failed on 130 foreign errors in RepairTool, HectonUnderwaterVisuals, and SargassumMicroFaunaBoids; no diagnostics name KCC/player files touched by this pass.

## Decision 9 - Vault-Only Locomotion State
Problem: Earlier DataVault work still left private H8Memory fallback allocation paths in `PlayerKinematicsRuntime`, `PlayerKinematicsNativeState`, and `HectonPlayerMotorNativeState`. That kept a second ownership route alive and violated the current DataVault sovereignty mandate.
Solution: Removed the private H8Memory fallback allocation path. When DataVault is absent or cannot provide a buffer, the allocator returns default; runtime and motor hot paths now fail closed through existing `IsCreated`/length guards. `PlayerKinematicsRuntime` now handles DataVault service replacement by pumping outstanding hand jobs, disposing stale aliases, and reacquiring vault buffers when the service returns.
Rejected Alternatives: Keeping cold private NativeArrays was rejected because it still makes locomotion own state outside the vault. Adding a second allocator facade was rejected because it would hide the same sovereignty problem behind a new name. Blocking gameplay with exceptions was rejected because missing vault state should disable the unsafe path and preserve telemetry, not crash.
Scalability potential: Low/MX350 keeps one compact vault-owned SOA path and no duplicate kinematic caches. Middle keeps standard SDF cadence. High/Ultra keep visual overkill through typed signal consumers, not extra private locomotion state.
Hardware Impact: Estimated runtime saving remains 4-12 us on low-end silicon from reduced duplicate cache churn. This pass claims 0 us additional measured runtime gain because no profiler capture was run. Latest measured failed build validation elapsed 227056406 us in `Build_KCC_SDF_SQUEEZE_RESOLVER_vault_polish8.exit.txt`.

## Decision 10 - Tether Compile Shim Kept Inside Physics Boundary
Problem: The validation build hit a PHYSICS/LOCOMOTION-adjacent tether compile wall after another edit partially redirected `TetherManager` to manager-local fire-request helpers that were not present. That blocked KCC final validation before Roslyn could reach the remaining tree.
Solution: Kept `TetherFiredSignal` as the typed telemetry lane, removed the managed `TetherFireRequest` sidecar queue, and routed the owner-local attach through `TetherManager.ExecuteFireRequest` immediately after publish. This preserves signal observability without storing Unity object references in a static sidecar.
Rejected Alternatives: Reviving `TryConsumeFireForManager` was rejected because it required a managed object-reference sidecar outside the signal payload. Implementing another manager-local queue was rejected because it would create two fire-request authorities. Editing `World/EcosystemDirector.cs` was rejected because it is outside KCC/locomotion and now forms the foreign compile wall.
Scalability potential: Low/MX350 keeps one typed fire signal plus direct attach with no per-frame drain. Middle/High/Ultra keep the same lane; visual overkill remains in tether rendering and downstream effects, not in KCC collision truth.
Hardware Impact: Runtime saving is 0 us. This is compile-wall containment. The follow-up checkpoint advanced past tether and audio blockers; at that checkpoint the wall was 24 foreign `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs` errors with no diagnostics naming `SdfSqueezeJob`, `HectonPlayerMotor`, `PlayerKinematicsRuntime`, `HectonPlayerState`, `TetherManager`, or `TetherSignals`. Decision 11 supersedes this stale wall with the current green build.

## Decision 11 - Stale Wall Revalidation
Problem: The latest status still treated `Build_KCC_SDF_SQUEEZE_RESOLVER_vault_polish8.exit.txt` as current truth, but the source had already moved: compass methods and ecosystem generic unsafe calls referenced by that log now exist in the files.
Solution: Revalidated the current tree with `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`, captured in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_loop11.exit.txt`. The build exits 0 with 0 errors. The only remaining diagnostics are four CS0649 warnings in `Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`, outside PHYSICS/LOCOMOTION.
Rejected Alternatives: Trusting the stale blocker was rejected because it contradicted current source. Editing UI/Ecosystem from the KCC prompt was rejected because the current build no longer requires it. Claiming 0-warning validation was rejected because Roslyn reports four unrelated warnings.
Scalability potential: Low remains 4-tap SDF plus stress cadence interpolation. Middle keeps standard SDF cadence and typed feedback. High/Ultra keep camera-roll and fluid-impulse overkill through downstream lanes, not extra KCC collision truth.
Hardware Impact: Measured validation time is 93277423 us. Runtime savings remain non-profiler estimates: 73-167 us saved per active low-tier squeeze frame from SDF-vs-capsule repair, DataVault sharing, stress cadence, and duplicate feedback collapse.
