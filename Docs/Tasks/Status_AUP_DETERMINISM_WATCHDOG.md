# Status: AUP_DETERMINISM_WATCHDOG

Prompt: `AUP_DETERMINISM_WATCHDOG`
Domain: `PHYSICS/AUP`
Role: `PHYSICS_PROGRAMMER`
Task count: 18
Status hygiene: fresh file created for current batch. Previous status file was missing.

## Mandates Read Before Coding

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Tasks

- [x] 1. PURGE_SINGLETONS: eradicated an early station-keeping `(float3)` integration cast; double3 delta survives until final `Vector3` handoff.
- [x] 2. DEBT_CLEANUP: removed the ballast dynamic-flood `math.sqrt()` feedback magnitude; retained squared threshold and cheap max/mid/min approximation.
- [x] 3. DATA_EVICTION: moved KCC sync-fence, GPU-flow, squeeze, and AUP pre-shift counters into `PlayerKinematicsAccumulatorState`.
- [x] 4. BURST_ALGORITHM: rewrote `AUPMath.AUPDirection` around `math.lengthsq(double3)` and guarded double `rsqrt`.
- [x] 5. AUP_INTEGRITY: verified KCC body job and `StageStateWrite` both snap position/velocity through millimeter quantization before commit.
- [x] 6. DOD_SOA_LAYOUT: verified `RigidbodyAUPs` is a contiguous `NativeArray<float3>` via DataVault/H8Memory and fed directly to `PhysicsDistanceCullingJob`.
- [x] 7. SIGNAL_FLOW: `PlayerKinematicsRuntime` now consumes `AupPreShiftSignal`, cancels pending state writes, and publishes one frozen KCC velocity frame.
- [x] 8. LOW_TIER_FAKE: distant vegetation matrix offset now uses float approximation only behind `#if _MATH_LOD_LOW` and only beyond 1000m.
- [x] 9. HIGH_END_OVERKILL: high/ultra Leviathan grab contact resolves root/tip from AUP double3 and guarded double rsqrt before damage contact handoff.
- [x] 10. REACTIVE_VFX: verified marine snow stores pending AUP shift offset, rebases flow-field centers, and uploads `_AupShiftOffset` to compute before dispatch.
- [x] 11. STP_STABILIZATION: verified `FoveatedSimulationManager` rebases both previous and current visual sample arrays on origin shift, preserving motion vectors.
- [x] 12. NAN_VACCINATION: guarded AUP/tentacle double rsqrt paths with `math.max(distSq, 0.0001)` and tightened remaining grab fallback.
- [x] 13. BLACKBOX_LOGGING: KCC sync fence now writes `AupMaxDriftErrorMeters`, sync-fence hash, and `Dump_AUP_DETERMINISM_WATCHDOG.bin`.
- [x] 14. TRIPLE_STRIKE_REPAIR: latest compile gate shows no `PlayerKinematicsRuntime` double3 conversion errors; remaining errors are outside AUP/KCC.
- [x] 15. HOMEOSTASIS_ADAPTATION: N/A for core math; no runtime adaptation branch added.
- [x] 16. GRAVITY_VECTOR_FIX: player default gravity now resolves from predicted AUP absolute position toward the AUP center with guarded double3 normalization.
- [x] 17. GHOST_REPLAY_VALIDATION: KCC body job, state staging, and sync-fence hashing all use millimeter quantization before persisted replay/hash state.
- [x] 18. FINAL_VALIDATION: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passes with 0 warnings and 0 errors on attempt 15.

## Loop 1 Evidence: Tasks 1-5

- Task 1 DOD: kept submarine station-keeping as double3 AUP math until Unity Rigidbody handoff; rejected `Vector3.MoveTowards` with float offset because it truncates the world delta before integration; estimated active-hull saving 1.2 us plus jitter correction avoidance.
- Task 2 DOD: retained squared comparison and replaced exact sqrt with visual-feedback approximation; rejected exact magnitude because this feeds acoustic stress, not authoritative physics; estimated saving 0.2 us per flood stress event.
- Task 3 DOD: grouped KCC accumulator counters into an unmanaged sequential struct; rejected scattered managed fields for sync telemetry because state hashing/replay needs a compact ownership boundary; estimated direct saving 0.0 us, indirect debug/replay cost reduction.
- Task 4 DOD: `AUPDirection` now uses double3 lengthsq and casts only after normalization; rejected float normalization and unguarded rsqrt; estimated saving 0.03 us and removes NaN retry churn.
- Task 5 DOD: KCC job end and state-write commit both use `DeterministicPhysicsMath.SnapMillimeter`; rejected Transform-space authority; estimated replay drift correction saving 2.0 us on bad frames.
- Compile gate: `[BLOCKED BY DEPENDENCY]` after three `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` attempts. Failures moved through unrelated concurrent `GlobalSignals`/`CombatDamageRuntime` damage-signal migration and were not caused by AUP/KCC files.

## Loop 2 Evidence: Tasks 6-10

- Task 6 DOD: confirmed `_rigidbodyAUPs` is allocated sequentially as `NativeArray<float3>` from `BufferID.RigidbodyAUPs` or H8Memory fallback, then consumed as a contiguous SoA by `PhysicsDistanceCullingJob`; rejected changing buffer type because `LockstepStateValidator` and DataVault contracts already own the lane; estimated saving 0.0 us, avoids contract churn.
- Task 7 DOD: KCC now listens to `SignalBus<AupPreShiftSignal>` and freezes integration for one frame; rejected integrating through rebase and fixing afterward; estimated spike avoidance 20-80 us on rebase frames.
- Task 8 DOD: added `_MATH_LOD_LOW` gate for >1000m vegetation matrix runtime offsets; rejected global float offsets because near flora still needs exact double offset until final upload; estimated MX350 saving 5-20 us during active payload copies.
- Task 9 DOD: high/ultra Leviathan grab contact uses root/tip `AbsoluteUniversePosition` caches and double3 direction before final damage point conversion; rejected all-tier 64-bit path because MX350 should keep one-iteration float Verlet; estimated high-tier cost +0.1 us per grab damage tick, bought with exact contact stability.
- Task 10 DOD: verified `HectonMarineSnowRenderer` accumulates `_pendingAupShiftOffset`, rebases flow-field centers, and pushes `_AupShiftOffset` to compute before dispatch; rejected particle-system rewrite because current GPU ping-pong path already carries the shift; estimated jump suppression cost 0.0 us except existing vector upload.
- Compile gate: `[BLOCKED BY DEPENDENCY]` on `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`. Current failure is broad missing-contract fallout outside this task (`GlobalRegistry`, movement contracts, macro database, foveated simulation, world pager, etc.).

## Loop 3 Evidence: Tasks 11-13

- Task 11 DOD: `FoveatedSimulationManager.OnOriginShift` subtracts the same shift from `_visualFromPositions` and `_visualToPositions`, preserving pre/post sample deltas for TAA/STP motion; rejected clearing history because it smears/pop-resets temporal data; estimated saving 0.0 us, avoids visual artifact.
- Task 12 DOD: AUP and high-tier tentacle direction rsqrt now use `math.max(distSq, 0.0001)`; fallback grab direction also uses guarded rsqrt; rejected relying only on `distSq > epsilon`; estimated saving is NaN recovery avoidance, not steady-state frame time.
- Task 13 DOD: sync fence every 300 frames writes `AupMaxDriftErrorMeters`, hash, shift sequence, and an AUP watchdog dump path; rejected string/debug logging in tick path; estimated cost 0.03 us every 300 frames plus existing binary dump on fault.
- Compile gate: `[BLOCKED BY DEPENDENCY]` on `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`. Current errors are `ProceduralLadderClimbRuntime.cs` missing `Hecton8.Input.Universal` and `UniversalInputStateSignal`, unrelated to AUP/KCC/Leviathan edits.

## Loop 4 Evidence: Tasks 14-16

- Task 14 DOD: searched `PlayerKinematicsRuntime` for stale sync-fence fields and verified accumulator usage plus latest build output; rejected patching unrelated contract/assembly fallout; estimated saving 0.0 us, compile hygiene only.
- Task 15 DOD: confirmed prompt marks homeostasis adaptation as N/A for core math; rejected adding a fake kill-switch branch to physics math; estimated saving 0.0 us.
- Task 16 DOD: `HectonPlayerMovement` now computes default gravity direction from predicted AUP absolute position to the AUP center using double3 lengthsq and guarded rsqrt; rejected local `Vector3.down` authority because it ignores planetary/AUP position; estimated cost 0.04 us per fixed tick.

## Loop 5 Evidence: Tasks 17-18

- Task 17 DOD: `PlayerKinematicsBodyJob`, `StageStateWrite`, and `BuildSyncFenceHash` quantize position, velocity, AUP locals, and rotation with `DeterministicPhysicsMath.QuantizeMillimeter`; rejected raw float hash bytes because replay states would diverge after origin shifts; estimated replay correction saving 2-5 us on drift frames.
- Task 18 DOD: final compile command executed and failed outside the AUP domain. Current blockers include missing `Hecton8.VFX.Wakes` types, missing `IDockingAutopilotService`/`ActiveSplineData`, and `EcosystemDirector` interface drift.

## Omega Polish

- `Vector3.Distance` scan: `rg -n "Vector3\\.Distance" Assets/_Project/Scripts` returns no matches after replacing two acoustic portal segment calls with a squared-length/rsqrt helper.
- 0.1ms frame-time check: added hot-path work is bounded to simple scalar math: AUP center gravity ~0.04 us/fixed tick, sync-fence drift ~0.03 us/300 frames, high-tier tentacle exact contact gated to High/Ultra, low-tier flora float path gated behind `_MATH_LOD_LOW`.
- Multiplatform packing scan: `rg --pcre2 -n "\[StructLayout\((?![^\)]*Pack\s*=\s*1)"` over the AUP/KCC/physics patch scope returns no matches after enforcing `Pack = 1` on determinism signals, KCC telemetry/state, docking spline packets, Leviathan telemetry, and AUP-touched player movement structs.
- Data sovereignty scan: `PlayerKinematicsRuntime` runtime arrays are DataVault-first through `AllocateRuntimeArray(..., BufferID.*, SystemID.GameplayPlayer)`; the only remaining `H8Memory.Allocate<T>` call is the fallback path with `SystemID.GameplayPlayer`.
- NaN vaccination scan: removed remaining `math.sqrt` in the scanned AUP/physics/audio-adjacent scope and clamped additional `rsqrt`/`rcp` sites in station keeping, CCD, fluid math, tether constraints, docking spline distance, and acoustic portal reverb mix.
- Status: `VERIFIED MASTER GRADE`; global compile gate passes with 0 warnings and 0 errors.
- Post-reopen correction: superseded by Loop 9. AUP/player/physics scans remain clean and the latest global compile gate is green.

## Loop 7 Evidence: Compile Wall Burn-Down

- Compile DOD: repaired the post-inquisition compile wall without changing AUP authority semantics. `GlobalDataVault.ValidateAbiLayout` was restored as a single method, `LockstepStateValidator` regained typed lane constants, `SubmarineFluidDynamics` vault handles were completed without local persistent ownership, and the transient fauna cognition helper error was resolved by the existing file state after recompile.
- Rejected: reporting blocked after a fixable compile wall was rejected; broad gameplay behavior rewrites were rejected. Only contract/handle glue needed to restore compile was touched.
- Microsecond estimate: runtime gain is 0.0 us for compile repairs; memory accounting gain is structural, because hydro/KCC state is DataVault-owned and tagged by `SystemID`.
- Final scan DOD: `Vector3.Distance`, `math.sqrt`, unguarded `math.rsqrt`, `string.Format`, standard `Update()`, and non-`Pack = 1` `StructLayout` scans return no matches in the AUP/player/physics patch scope. Whole-script `Vector3.Distance` scan returns no matches. `git diff --check` reports no whitespace errors.

## Loop 8 Evidence: Post-Reopen Multiplatform Burnish

- ARM64 layout DOD: broader AUP/vehicle-fluid scan found missed explicit-layout packets. Added `Pack = 1` to `AbsoluteUniversePositionBlit`, `SplashEvent`, ballast PID packets, hydro job packets, and hydro transfer jobs. Rejected changing field order or packet size because existing serialized/DataVault strides must remain stable. Microsecond estimate: 0.0 us runtime, removes Quest/ARM64 layout drift risk.
- NaN vaccination DOD: replaced remaining scanned branch-only `rsqrt` calls in ballast/hydro code with `math.rsqrt(math.max(...))`; replaced ballast PID stress and cavitation rumble exact magnitude paths with existing max/mid/min fakes. Rejected exact `math.length` because these values drive audio/VFX stress, not authoritative physics. Microsecond estimate: 0.02-0.08 us saved on stress/rumble events, plus NaN fault avoidance.
- Signal DOD: `PhysicsDeterminismSignals` no longer owns private `NativeQueue` lanes; it configures/publishes through typed `SignalBus<T>` lanes and its packets implement `ISignal`. Latest-value sidecars remain only as unmanaged value caches for existing KCC/lockstep API calls. Rejected a managed delegate/EventBus bridge because it would violate the typed-lane protocol. Microsecond estimate: 0.0 us direct, memory ownership moves to the existing SignalBus sentinel path.
- Compile DOD: three post-reopen compile attempts were executed. Current blockers are external to AUP: `World/SargassumMicroFaunaBoids.cs` missing vault/native fields and `RepairTool.cs` unassigned `localPoint`. Logs written to `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt2.txt` and `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt3.txt`.
- Final scan DOD: broad AUP/player/physics scan returns no matches for `Vector3.Distance`, `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, standard `Update()`, `GameObject.Find`, `FindObjectOfType`, or non-`Pack = 1` `StructLayout`.

## Loop 9 Evidence: Typed-Lane Event Inquisition

- Signal DOD: `FluidFeedbackEvents` and `PhysicsEventBus` no longer own private `NativeQueue` event lanes. `SplashEvent`, `PhysicsEventPayload`, and deferred submarine impact payloads are packed `ISignal` packets published through `SignalBus<T>` and consumed as `ReadOnlySpan<T>` snapshots.
- Late-frame DOD: both converted bridges requeue unconsumed snapshot tails when the late-frame budget is exhausted, preserving the old deferred behavior without private queue ownership.
- ARM64 layout DOD: `PhysicsApplySystem` force/acoustic/pressure packet structs now carry `Pack = 1`; scan over the AUP/physics patch scope returns no non-packed `StructLayout` matches.
- Rejected: renaming the public `PhysicsEventBus` API was rejected because existing listeners and producers depend on that surface. Converting `ForcePacket` command queues was rejected in this loop because they expose a fixed-step `NativeQueue<ForcePacket>.ParallelWriter` command contract, not a presentation event bus; changing that requires a dedicated force-command vault migration.
- Compile DOD: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` passes with 0 warnings and 0 errors. Log: `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt10.txt`.
- Microsecond estimate: event-lane migration saves 0.0 us directly; it removes duplicate native event queues and centralizes sentinel accounting. Requeue work is only on late-frame budget exhaustion and capped by existing lane capacity.

## Loop 10 Evidence: Force Command Vault Migration

- Data sovereignty DOD: `PhysicsApplySystem` no longer owns private `NativeQueue<ForcePacket>` front/back queues or private validation `NativeArray` fields. Force command front/back buffers and validation packet/mask buffers are `GlobalDataVault` handles: `PhysicsForceCommandFront`, `PhysicsForceCommandBack`, `PhysicsForceValidationPackets`, and `PhysicsForceValidationMask`.
- Producer audit DOD: repo-wide `rg` found no consumer of `TryGetForcePacketBackWriter`; the only force producers route through `PhysicsForceRouter` main-thread methods, so the unused `NativeQueue<ForcePacket>.ParallelWriter` API was removed instead of preserving a dead private queue.
- Fixed-step DOD: front/back swap semantics remain intact with `_frontCount`/`_backCount`; validation still runs through `ValidateForcePacketsJob`, but its input/output storage is vault-owned.
- Scan DOD: `rg -n "TryGetForcePacketBackWriter|NativeQueue<ForcePacket>|new NativeQueue<ForcePacket>|\b_frontPacketQueue\b|\b_backPacketQueue\b|\b_validationPackets\b|\b_validationMask\b|\b_frontPackets\b" Assets/_Project/Scripts/PhysicsApplySystem.cs` returns no matches.
- Compile DOD: attempts 11-13 were run. Attempt 11 exposed missing lockstep lane constants and attempt 12 exposed a diagnostics namespace compile gap; both were repaired. Attempt 13 is `[BLOCKED BY DEPENDENCY]` on external `DiegeticGyroCompassRuntime` DTO drift and `SystemDispatcher` missing blackbox/raycast lock members. Log: `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt13.txt`.
- Microsecond estimate: queue migration saves 0.0 us directly; it removes duplicate private native queue allocation/sentinel ownership. Main-thread enqueue remains O(1) bounded array write; validation copy remains capped at 64 packets.

## Loop 11 Evidence: Global Physics Vault Migration

- Data sovereignty DOD: `GlobalPhysicsStateManager` no longer owns private persistent `NativeArray` fields or a private `NativeQueue<PhysicsImpactEventData>`. Last-valid positions, AUP culling lanes, result lanes, culling telemetry, and deferred impact events resolve through `GlobalDataVault` handles.
- Deferred impact DOD: collision impact buffering now uses a vault-backed bounded ring with read/write cursors and the same late-frame flush budget; rejected `SignalBus<T>` for this path because the fixed/late flush cadence must not depend on pre-simulation signal snapshots.
- ARM64 layout DOD: `RigidbodyState`, `PhysicsConnection`, `PhysicsImpactEventData`, and `PhysicsCullingTelemetryEntry` now declare `Pack = 1`.
- NaN DOD: remaining `GlobalPhysicsStateManager` impact normals, acoustic energy radius, and rigidbody sleep distance paths use `math.rsqrt(math.max(...))`.
- Scan DOD: `rg --pcre2` over `GlobalPhysicsStateManager.cs` returns no matches for private native container ownership, non-`Pack = 1` `StructLayout`, `Vector3.Distance`, direct `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, or standard `Update()`.
- Compile DOD: attempt 14 is `[BLOCKED BY DEPENDENCY]` on external save/data-baker contract drift: `HectonContractVersion`, `CsvReadBufferBytes`, and `SignalBusRegistry`. Log: `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt14.txt`.
- Microsecond estimate: 0.0 us direct frame gain; impact enqueue remains O(1), culling job still receives contiguous NativeArray views, and low-end gain is sentinel/vault consolidation plus removal of duplicate private native ownership.

## Loop 12 Evidence: Player Blackbox Vault Closure

- Data sovereignty DOD: `HectonPlayerMovement` no longer owns the cinematic focus blackbox as a private `NativeArray`; it stores a `VaultBufferHandle<CinematicFocusTelemetryEntry>` and resolves the DataVault view only when writing or dumping telemetry.
- Blackbox DOD: the 300-entry cinematic focus telemetry ring keeps the same dump format and cooldown, but storage ownership remains in `GlobalDataVault` under `BufferID.PlayerCinematicFocusBlackBox`.
- Scan DOD: broad AUP/player/physics/vehicle scan returns no matches for private native container ownership, private native allocations, non-`Pack = 1` `StructLayout`, `Vector3.Distance`, direct `math.sqrt`, `math.length`, unguarded `math.rsqrt`, `string.Format`, or standard `Update()`.
- Compile DOD: attempt 15 passes with 0 warnings and 0 errors. Log: `Docs/AgentLogs/Dump_AUP_DETERMINISM_WATCHDOG_build_attempt15.txt`.
- Microsecond estimate: 0.0 us direct frame gain; telemetry writes still resolve to a contiguous DataVault buffer and remain cadence/fault gated.

## Loop State

- Iteration 0: prompt extracted; mandates read; codebase scan complete for AUP/KCC station-keeping targets.
- Iteration 1: tasks 1-5 implemented; prompt re-extracted after task 3; compile gate blocked by unrelated damage-signal dependency after 3 attempts.
- Iteration 2: tasks 6-10 implemented/verified; prompt re-extracted after task 9; compile gate blocked by broad unrelated contract dependency fallout.
- Iteration 3: tasks 11-13 implemented/verified; compile gate blocked by unrelated input namespace dependency.
- Iteration 4: tasks 14-16 implemented/verified; prompt re-extracted after task 12; compile gate blocked by unrelated contract/determinism/signal dependency fallout.
- Iteration 5: tasks 17-18 verified; final compile attempted and marked `[BLOCKED BY DEPENDENCY]` for non-AUP errors. Polish mandate completed with zero remaining `Vector3.Distance` matches.
- Iteration 6: post-inquisition hardening completed; ARM64 `Pack = 1` scan clean in touched AUP/physics scope, KCC scratch/state arrays are DataVault-first, and guarded `rsqrt`/`rcp` replaced remaining scanned sqrt/division risk.
- Iteration 7: compile wall burned down; `Hecton8.Core.csproj` builds clean with 0 warnings and 0 errors, and final scans are clean in the AUP/player/physics scope.
- Iteration 8: post-reopen scan tightened additional AUP/vehicle-fluid packing, magnitude paths, and physics determinism signal ownership; compile is `[BLOCKED BY DEPENDENCY]` after three attempts on external Sargassum/RepairTool errors, with AUP/player/physics scans clean.
- Iteration 9: converted fluid/physics deferred event bridges and submarine impact trauma dispatch to typed `SignalBus<T>` lanes, tightened `PhysicsApplySystem` packet packing, and restored a green `Hecton8.Core.csproj` compile.
- Iteration 10: migrated `PhysicsApplySystem` force command buffers and validation staging to `GlobalDataVault`; force-queue scans are clean, compile is blocked after three attempts by external UI/SystemDispatcher dependency drift.
- Iteration 11: migrated `GlobalPhysicsStateManager` native culling/impact storage to `GlobalDataVault`, enforced pack/NaN guards, and confirmed attempt 14 is blocked outside AUP by save/data-baker contract drift.
- Iteration 12: migrated the remaining player cinematic focus blackbox `NativeArray` field to a DataVault handle; broad AUP/player/physics scans are clean and attempt 15 builds green.
