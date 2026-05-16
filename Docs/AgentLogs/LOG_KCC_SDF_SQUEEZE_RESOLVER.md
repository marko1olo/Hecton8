# LOG_KCC_SDF_SQUEEZE_RESOLVER

## 2026-05-16 - SDF Tight Gap Traversal
What was wrong:
- Existing player kinematics sampled voxel SDF but treated solid overlap as `FaultSolidTeleport`, forcing hard snap/velocity zero on jagged cave edges.
- No `KCCManager.Instance` or player `OnCollisionStay` debt existed in the searched Gameplay/Physics KCC scope; the actual debt was the solid-overlap fallback.
- Player kinematic hot state was still locally owned before the vault handoff path.

What was done:
- Added `Assets/_Project/Scripts/Physics/KCC/SdfSqueezeJob.cs`.
- Integrated SDF squeeze before `PlayerKinematicsBodyJob` solid-fault handling.
- Preferred `GlobalDataVault` for player position, velocity, and intended movement SOA buffers.
- Read/wrote `BufferID.PlayerKinematicState` as `LockstepPlayerKinematicState`.
- Published squeeze feedback through existing lanes: `PlayerStateSignal`, `HapticRequest`, `AcousticPingSignal`, `PhysiologyStateSignal`, and `IGasDynamicsSolver.TryApplyPlayerRoomCarbonDioxideEquivalentPressure`.
- Added KCC SDF telemetry flags and `Dump_KCC_SDF_SQUEEZE_RESOLVER.bin` to the fault dump set.
- Added Unity `.meta` files for the new KCC folder and job script.

Cinematic cheats used:
- Low/MX350: 4-tap tetrahedral SDF gradient instead of 6-axis gradient.
- High/Ultra: reuse SDF normal for micro camera roll; no extra body-twist physics.
- Homeostasis: when `SignalBusRegistry.SystemStress01 > 0.8`, sample every 5 frames and interpolate cached push-out.
- Scrape response is signal-driven fake feedback, not physical material simulation.

Exact microseconds saved:
- No `CapsuleCast`/collision callback repair path: estimated 35-80 us saved per active low-tier squeeze frame.
- DataVault SOA sharing: estimated 5-15 us saved by removing duplicate hot kinematic copies.
- Homeostasis 5-frame cadence: estimated 25-60 us saved across sustained squeeze frames.
- Signal-driven scrape feedback instead of concrete audio/haptic polling: estimated 8-12 us saved per active feedback frame.
- Total expected active squeeze saving on i3/MX350: 73-167 us per frame, before profiler validation.

Validation:
- `dotnet build Hecton8.Core.csproj` initially exposed KCC integration errors; fixed `SignalBusRegistry.SystemStress01` and moved `ApplyForwardSpeedPenalty` into the runtime class.
- Final `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` still fails with 178 foreign errors outside this prompt, including HectonVoxelEngine/Core.Contracts references, FaunaBrain missing `_slot`, HarvestableOutcrop missing `DebrisSpawnSignal`, Bootstrap contract reference failures, LockstepStateValidator missing ghost replay fields, GlobalSignals missing unrelated signal types, and HectonFloatingOrigin missing shader vault bridge.
- Final Roslyn log `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_core_final.exit.txt` contains no `PlayerKinematicsRuntime` or `SdfSqueezeJob` errors.

## 2026-05-16 - Multiplatform Data Sovereignty Pass
What was wrong:
- Prior DataVault eviction only covered position, velocity, and intended movement. Flow velocity, last-valid position, sync states, hand targets, telemetry ring/cursor, fault flags, probe batches, SDF squeeze results, and player motor sweep/repair caches still used local-primary NativeArrays.
- NativeArray payload structs in the resolver path used sequential Pack=4 layout instead of explicit byte offsets.

What was done:
- Added `BufferID.PlayerKinematicFlowVelocity` through `BufferID.PlayerKinematicSdfSqueezeResults`.
- Added `BufferID.PlayerMotorScheduledSweepCommands` through `BufferID.PlayerMotorKinematicRepairTargetResults`.
- Routed every `PlayerKinematicsRuntime` persistent NativeArray and the player motor sweep/repair command-result caches through vault-first allocation, with H8Memory left only as cold fallback when the vault is unavailable.
- Converted resolver payloads to explicit `Pack = 1` layouts: `SdfSqueezeResult`, runtime telemetry, sync state, accumulator state, hand target, and player telemetry.
- Re-scanned the resolver path for `Update`, `string.Format`, legacy `EventBus`, managed delegates, `GameObject.Find`, `FindObjectOfType`, `Physics.CapsuleCast`, and `OnCollisionStay`; none were found.
- Confirmed KCC has no compute/shader files, so this pass adds no Metal thread-group or DirectX-only shader risk.

Cinematic cheats used:
- Low tier still uses 4-tap tetrahedral SDF and slow-cadence interpolation under stress.
- High/Ultra still buy camera roll, scrape haptics, and acoustic fabric scratch from the same SDF normal/stress scalar.
- Salt/silt/hull-dent overkill remains downstream VFX territory; locomotion emits typed signals and does not add collision probes to fake art.

Exact microseconds saved:
- DataVault eviction expansion: estimated 4-12 us saved from reduced duplicate cache churn.
- Explicit payload layout: 0 us direct frame saving; removes ARM64 padding ambiguity and crash class.
- No new per-frame disk I/O; Steam Deck MicroSD impact remains 0 us outside fault dumps.

Validation:
- Final `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` captured in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_final_pass.exit.txt`.
- Build still fails with 70 unique foreign errors and 3 duplicate-compile warnings in UI navigation, tether signals, homeostasis, lockstep replay, and item pickup domains.
- Captured final log contains no diagnostics naming `SdfSqueezeJob`, `PlayerKinematicsRuntime`, `HectonPlayerState`, or `H8Memory`.

## 2026-05-16 - Signal Collapse And AUP Polish
What was wrong:
- The motor-side SDF fallback sampled SDF from runtime float coordinates instead of reconstructing the sample from AUP double-space.
- The older motor squeeze branch and runtime resolver both emitted scrape haptic/acoustic effects.
- Remaining locomotion owner/job structs still used non-`Pack = 1` `StructLayout` declarations.

What was done:
- Converted remaining KCC/player locomotion `StructLayout` declarations to `Pack = 1`; `ScheduledSweepState` is now explicit 64-byte layout.
- Added AUP double-space sampling to `HectonPlayerMotor.TryResolveSdfSqueeze`.
- Removed motor-side direct squeeze haptic/acoustic emission. It now publishes `PlayerStateSignal.StateSqueezing`; `PlayerKinematicsRuntime` consumes that typed lane and emits physiology, gas, haptic, acoustic, and high-tier visual fluid feedback once.
- Added high/ultra-only `FluidImpulseSignal` from SDF stress/normal/velocity for downstream dynamic silt/wake overkill.

Cinematic cheats used:
- Toaster mode keeps 4-tap SDF, cached interpolation, and no fluid impulse.
- God mode spends saved collision cost on a typed dynamic-fluid impulse rather than deeper physics.
- Salt/hull-specific rendering remains downstream VFX ownership; locomotion emits the signal, not private renderer code.

Exact microseconds saved:
- Duplicate squeeze feedback collapse: estimated 8-12 us saved on motor-side active squeeze frames.
- AUP motor fallback hardening: estimated 0-2 us cost, paid to remove drift-class SDF sampling errors.
- High-tier fluid impulse: estimated 3-8 us downstream cost only on High/Ultra; 0 us on low/MX350.

Validation:
- `rg` found all `StructLayout` entries in the KCC/player locomotion surface use `Pack = 1`.
- `rg` found no `Update`, `string.Format`, legacy `EventBus`, managed delegate, `GameObject.Find`, `FindObjectOfType`, `Physics.CapsuleCast`, or `OnCollisionStay` in the resolver/KCC runtime path.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` captured in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_polish2.exit.txt`; build is blocked by one foreign XR error in `HectonXRRuntimeState.cs`, and no KCC/locomotion touched file appears in diagnostics.

## 2026-05-16 - Final Green Validation
What was wrong:
- The prior validation wall was one foreign Core XR compile error: `XRDisplaySubsystem.TryRequestDisplayRefreshRate` did not exist in the targeted API surface.
- Current `Assets/_Project/Scripts/Core/HectonXRRuntimeState.cs` no longer contains that API call, so the old wall was stale in the current tree.

What was done:
- Re-extracted the `KCC_SDF_SQUEEZE_RESOLVER` XML block from `Docs/Tasks/CURRENT_BATCH.md` and recounted 18 tasks.
- Reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`.
- Captured the successful output in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_xr_validation.exit.txt`.
- Updated status and rationale to `VERIFIED MASTER GRADE / BUILD GREEN`.

Cinematic cheats used:
- No new cinematic code was added in this validation pass.
- Existing locomotion cheats remain: 4-tap low-tier SDF, 5-frame stress cadence interpolation, signal-driven scrape feedback, high-tier camera roll, and high/ultra `FluidImpulseSignal` for downstream silt/wake overkill.

Exact microseconds saved:
- Measured compile validation time: 90420000 us elapsed.
- Runtime savings were not profiler-measured in this pass.
- Existing engineering estimates remain: 73-167 us saved per active i3/MX350 squeeze frame before profiler validation.

Validation:
- Build result: succeeded.
- Warnings: 0.
- Errors: 0.
- Exit code: 0.

## 2026-05-16 - NaN Denominator Polish
What was wrong:
- Motor-side SDF/sweep helpers used `math.rsqrt` after comparison guards but did not pass explicit `math.max(...)` denominators at every site.
- This does not save frame time; it removes a NaN propagation class on ARM64/Quest/Android and other strict mobile GPU/CPU paths.

What was done:
- Hardened `HectonPlayerMotor` displacement direction, `SafeNormal`, voxel-proxy slide fallback, tangent-slide projection, and no-trig quaternion normalization.
- Re-scanned `SdfSqueezeJob`, `HectonPlayerMotor`, and `PlayerKinematicsRuntime`; `rg --pcre2 "math\\.rsqrt\\((?!math\\.max)"` now returns no matches in those files.
- Reconfirmed no `Update`, `string.Format`, legacy `EventBus`, managed delegate, `GameObject.Find`, `FindObjectOfType`, `Physics.CapsuleCast`, `OnCollisionStay`, or `KCCManager.Instance` in the scanned KCC/player locomotion surface.

Cinematic cheats used:
- No new visual cheat code was added in this pass.
- Existing low-tier 4-tap SDF, stress cadence interpolation, and high/ultra fluid impulse lanes remain unchanged.

Exact microseconds saved:
- 0 us runtime saving claimed for this pass.
- Measured failed build validation time: 105250000 us.

Validation:
- `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_nan_polish.exit.txt` exits 1 with 130 foreign errors.
- No diagnostics name `SdfSqueezeJob`, `HectonPlayerMotor`, `PlayerKinematicsRuntime`, or `HectonPlayerState`.
- First foreign blockers: `RepairTool.cs(1036,52)`, `HectonUnderwaterVisuals.cs(3534+)`, and `World/SargassumMicroFaunaBoids.cs(2564+)`.

## 2026-05-16 - Vault-Only State And Tether Compile Shim
What was wrong:
- The locomotion/player state path still had private H8Memory fallback allocation after DataVault hardening. That kept a second owner for hot NativeArray state.
- DataVault service replacement could leave stale player runtime aliases unless the runtime explicitly pumped outstanding jobs and reacquired vault buffers.
- The current validation tree briefly hit a PHYSICS/LOCOMOTION-adjacent `TetherManager` compile wall from a partial local fire-request queue edit.

What was done:
- Removed private H8Memory fallback allocation from `PlayerKinematicsRuntime`, `PlayerKinematicsNativeState`, and `HectonPlayerMotorNativeState`.
- Added DataVault replacement handling in `PlayerKinematicsRuntime`: hand environment jobs are pumped, native aliases are disposed, and buffers are reacquired when DataVault returns.
- Added fail-closed guards before player motor scheduled sweep and kinematic repair target buffers are indexed.
- Restored `TetherManager` fire drain/execute flow to the existing typed `TetherSignals.TryConsumeFireForManager` lane and removed the partial local queue references.

Cinematic cheats used:
- No new visual code was added in this pass.
- Existing low-tier 4-tap SDF, stress cadence interpolation, signal-driven scrape feedback, and high/ultra fluid impulse lanes remain the visual-cheat path.
- Locomotion still emits typed signals for downstream salt/silt/hull-dent overkill instead of owning renderer effects.

Exact microseconds saved:
- Vault-only fallback removal: 0 us measured in this pass; expected low-tier cache churn saving remains 4-12 us from prior DataVault sharing estimate.
- Tether compile shim: 0 us runtime saving; compile-wall containment only.
- Measured failed validation time for latest `Build_KCC_SDF_SQUEEZE_RESOLVER_vault_polish8.exit.txt`: 227056406 us.

Validation:
- Static scan found no local persistent `H8Memory.Allocate`, `Allocator.Persistent`, `NativeMemorySentinel.RegisterNativeArray`, `COLD FALLBACK`, or `AllocateLocalArray` in the scanned KCC/player/tether surface.
- Static scan found every `StructLayout` in the scanned KCC/player/tether surface uses `Pack = 1`.
- Static scan found no unguarded `math.rsqrt`, `Update`, `string.Format`, legacy `EventBus`, managed delegate, `GameObject.Find`, `FindObjectOfType`, `Physics.CapsuleCast`, `OnCollisionStay`, or `KCCManager.Instance` in the scanned surface.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false` exits 1 with 24 foreign `UI/Navigation/DiegeticGyroCompassRuntime.cs` and `World/EcosystemDirector.cs` errors.
- No diagnostics name `SdfSqueezeJob`, `HectonPlayerMotor`, `PlayerKinematicsRuntime`, `HectonPlayerState`, `TetherManager`, or `TetherSignals`.

## 2026-05-16 - Loop 11 Current Tree Green Revalidation
What was wrong:
- The status still treated `Build_KCC_SDF_SQUEEZE_RESOLVER_vault_polish8.exit.txt` as current, but the source had moved. The compass methods and ecosystem generic unsafe calls named by that stale log are present in the current files.
- The documentation also described the old `TryConsumeFireForManager` sidecar-drain tether path after the actual tree had removed the managed fire-request sidecar.

What was done:
- Re-extracted the exact `KCC_SDF_SQUEEZE_RESOLVER` XML prompt and reconfirmed 18 tasks.
- Reconciled status/rationale with the current tow flow: `HeavyTowWinch.TryAttach` publishes `TetherFiredSignal`, then calls `TetherManager.ExecuteFireRequest` directly.
- Reran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /p:UseSharedCompilation=false`.
- Captured the green build in `Docs/AgentLogs/Build_KCC_SDF_SQUEEZE_RESOLVER_loop11.exit.txt`.

Cinematic cheats used:
- No new visual code was added in this pass.
- Existing cheats remain active: MX350 4-tap SDF gradient, 5-frame stress cadence interpolation, signal-driven scrape feedback, high-tier camera roll, and high/ultra `FluidImpulseSignal` for downstream silt/wake overkill.

Exact microseconds saved:
- Loop 11 runtime saving claimed: 0 us measured.
- Measured build validation time: 93277423 us.
- Existing non-profiler low-tier active squeeze estimate remains 73-167 us saved per frame.

Validation:
- Build result: succeeded.
- Warnings: 4, all CS0649 in `Assets/_Project/Scripts/Core/Diagnostics/Visuals/ArchitectEyeVisualizer.cs`.
- Errors: 0.
- Exit code: 0.
- Static scan found no local persistent `H8Memory.Allocate`, `Allocator.Persistent`, `NativeMemorySentinel.RegisterNativeArray`, `COLD FALLBACK`, or `AllocateLocalArray` in the scanned KCC/player/tether surface.
- Static scan found no unguarded `math.rsqrt`, `Update`, `string.Format`, legacy `EventBus`, managed delegate, `GameObject.Find`, `FindObjectOfType`, `Physics.CapsuleCast`, `OnCollisionStay`, `KCCManager.Instance`, `TryConsumeFireForManager`, or `TetherFireRequest` in the scanned surface.
