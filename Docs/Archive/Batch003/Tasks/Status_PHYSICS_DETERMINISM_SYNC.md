# Status_PHYSICS_DETERMINISM_SYNC

Agent: LOCOMOTION_ENGINEER
Prompt: PHYSICS_DETERMINISM_SYNC
Domain: ECHELON 4 - PLAYER, KINEMATICS & TOOLS (Locomotion and Equipment)
Task Count: 19
Status: PENDING VERIFICATION

## Extracted Directive

Source: Docs/Tasks/ANOTHER_BATCH.md
Task block extracted by CLI regex on start and re-read during verification loops.

## Mandates Loaded

- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- MATH_Rsqrt_i3_SIMD.txt
- NET_Logistics_Sync_BitPacking_Reconciliation.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt

## Checklist

- [x] 1. SINGLETON ERADICATION: Purge PhysicsManager.Instance.
  - DOD: `rg PhysicsManager.Instance Assets/_Project/Scripts` returns no matches.
  - Rejected alternative: adapter singleton shim; would preserve hidden ordering.
  - Microsecond estimate: 0 us/frame runtime; removes future singleton lookup risk.
- [x] 2. SIGNAL MIGRATION: Physics consumes SignalBus<InputSignal>, not Input.GetAxis().
  - DOD: `InputDispatcher.CaptureState` publishes `PhysicsDeterminismSignals.InputSignal`; `PlayerKinematicsRuntime` drains queued/latest signal.
  - Rejected alternative: direct `GlobalRegistry.Input.GetState()` read inside fixed tick; frame ownership stays ambiguous.
  - Microsecond estimate: +0.4 us enqueue/drain, -1.2 us from no service lookup in fixed lane.
- [x] 3. ASMDEF ISOLATION: Hecton8.Physics.Determinism depends only on Contracts.
  - DOD: added `Hecton8.Physics.Determinism.asmdef` with only `Hecton8.Core.Contracts` reference and `noEngineReferences=true`.
  - Rejected alternative: placing math helpers in Core; would widen dependency graph.
  - Microsecond estimate: 0 us/frame; compile/domain hygiene only.
- [x] 4. DEAD CODE HUNT: Remove math.sqrt() and Vector3.magnitude from physical solvers.
  - DOD: targeted hot-path scan shows no `math.sqrt()` or `Vector3.magnitude` in locomotion/global physics solver files; existing kinetic-energy path uses rsqrt.
  - Rejected alternative: tolerating sqrt in solver telemetry; still causes cross-CPU drift/cost.
  - Microsecond estimate: 1.5-4.0 us saved during dense contact frames.
- [x] 5. FIXED POINT MATH FAKE: Quantization snap after every KCC movement integration.
  - DOD: `PlayerKinematicsBodyJob`, `PlayerKinematicsRuntime` state staging, and `HectonPlayerMotor.MovePosition` snap to millimeter.
  - Rejected alternative: true fixed-point KCC; too expensive for i3/MX350.
  - Microsecond estimate: +0.5 us/frame; buys deterministic replay stability.
- [x] 6. DIVISION BAN: Replace / dt with precalculated math.rcp(dt).
  - DOD: targeted locomotion/physics scan finds no `/ dt` in changed physical solvers except a comment; motor projection division uses `math.rcp`.
  - Rejected alternative: standard division in contact resolver; inconsistent reciprocal rounding risk.
  - Microsecond estimate: 0.2-0.8 us saved in wall-slide telemetry frames.
- [x] 7. TRIGONOMETRY APPROXIMATION: Replace critical sin/cos with deterministic polynomial/LUT.
  - DOD: player impact roll wave uses `DeterministicPhysicsMath.SinApprox` instead of `math.sin`.
  - Rejected alternative: hardware `math.sin` in Burst; architecture variation is the original bug class.
  - Microsecond estimate: 0.3-0.7 us saved during impact roll events.
- [x] 8. THE 300-FRAME FENCE: FNV-1a hash of Player AUP, velocity, rotation.
  - DOD: `FastTick` publishes a Sync-Fence every 300 ticks; hash covers AUP grid/local, velocity, and rotation.
  - Rejected alternative: per-frame hash; needless queue pressure.
  - Microsecond estimate: +2.0 us every 300 fast ticks, 0.006 us amortized/frame.
- [x] 9. STATE COMPARISON: Emit DesyncDetectedSignal on hash mismatch.
  - DOD: state correction path compares expected local hash and publishes `DesyncDetectedSignal`.
  - Rejected alternative: log-only mismatch; no native lane for reconciliation.
  - Microsecond estimate: +0.7 us only on correction packets.
- [x] 10. AUTHORITATIVE SNAP: Consume StateCorrectionSignal and overwrite KCC state.
  - DOD: `PostFixedTick` drains up to 8 `StateCorrectionSignal` packets and stages authoritative snapped state.
  - Rejected alternative: interpolated correction; hides drift instead of fixing authority.
  - Microsecond estimate: +1.0 us only when correction queue is non-empty.
- [x] 11. DOUBLE-BUFFER ISOLATION: Read State_Read, write State_Write, swap at POST_SIMULATION.
  - DOD: `_stateRead` and `_stateWrite` NativeArrays stage fixed output and commit in `PostFixedTick`.
  - Rejected alternative: immediate Rigidbody writes from fixed job output; mid-frame observers can see partial state.
  - Microsecond estimate: +0.6 us/frame for deterministic swap isolation.
- [x] 12. AUP ORIGIN SHIFT PERFECTING: Integer subtraction only for origin shift.
  - DOD: KCC local state buffers subtract the same origin-shift offset with no scale/rotation mutation; AUP grid authority remains in `HectonFloatingOrigin`.
  - Rejected alternative: recomputing from transform after shift; risks float divergence.
  - Microsecond estimate: 0 us/frame; shift-only safety.
- [x] 13. ZERO-GC RAYCASTS: KCC ground checks use RaycastCommand.ScheduleBatch.
  - DOD: KCC path uses `CapsulecastCommand.ScheduleBatch` and `RaycastCommand.ScheduleBatch`; no synchronous raycast/capsule cast found in KCC files.
  - Rejected alternative: `Physics.Raycast`/`CapsuleCast` in main thread; nondeterministic and stalls.
  - Microsecond estimate: 8-35 us saved during crowded collision frames.
- [x] 14. MATH LOD: Low Tier KCC penetration resolver steps 4 -> 2.
  - DOD: `MaxSlideSweepIterations` is 2.
  - Rejected alternative: restoring 4 universal iterations; wastes low-tier budget.
  - Microsecond estimate: 6-18 us saved on low-tier wall-slide frames.
- [x] 15. NO MONOBEHAVIOUR UPDATE: Kinematics run in SystemDispatcher SIMULATION phase.
  - DOD: `PlayerKinematicsRuntime` has no Update/FixedUpdate/LateUpdate and registers Fixed/PostFixed/Fast/Late lanes through `GlobalRegistry`.
  - Rejected alternative: MonoBehaviour message loop; order cannot be audited.
  - Microsecond estimate: 0.5-1.5 us saved in dispatch overhead variance.
- [x] 16. BLACKBOX DUMP: Push Sync-Fence hash to Telemetry every 300 frames.
  - DOD: telemetry entry stores `SyncFenceHash`; fault dump writes last 300 entries to `Docs/AgentLogs/Dump_PHYSICS_DETERMINISM_SYNC.bin`.
  - Rejected alternative: text log on desync; allocates and loses previous frames.
  - Microsecond estimate: +3.5 us per fence, 0.012 us amortized/frame.
- [x] 17. EVENT BUS DAMAGE: Push SignalBus<ImpactSignal> on high-velocity wall collision.
  - DOD: wall-slide contact emits `GlobalSignals.Publish(in ImpactSignal)` when blocked speed exceeds 4 m/s.
  - Rejected alternative: direct damage/audio calls from KCC; creates domain coupling.
  - Microsecond estimate: +0.8 us only on high-velocity wall impacts.
- [x] 18. CROSS-DOMAIN AUDIT: Submarine Autopilot uses Quantization Snap.
  - DOD: `SubmarineAutoLevelBallastController` snapshots and telemetry snap position/velocity to millimeter and Burst job uses deterministic mode.
  - Rejected alternative: leave vehicle autopilot unsnapped; player/submarine drift would diverge across authority systems.
  - Microsecond estimate: +0.4 us per autopilot post-fixed tick.
- [x] 19. OMEGA COMPILE CHECK: Verify Burst jobs use FloatMode.Deterministic.
  - DOD: `PlayerKinematicsBodyJob`, `PlayerKinematicsHandPlacementJob`, and `SubmarineAutoLevelPidJob` use `FloatMode.Deterministic`/`FloatPrecision.Standard`; `dotnet build Hecton8.Core.csproj --no-restore` passed with 0 errors, 0 warnings.
  - Rejected alternative: keep `FloatMode.Fast`; explicitly conflicts with determinism directive.
  - Microsecond estimate: may cost 1-5 us in math-heavy frames; budget is spent for sync authority.

## Verification

- PASS: Direct Roslyn compile of `DeterministicPhysicsMath.cs` to `Temp/DeterminismCheck_PHYSICS_DETERMINISM_SYNC.dll`.
- PASS: `dotnet build Hecton8.Core.csproj -v:minimal /nologo --no-restore` -> 0 errors, 0 warnings.
- PASS: Static scans show no `PhysicsManager.Instance`, no synchronous KCC raycast calls, no `FloatMode.Fast` in patched deterministic jobs, and no `/ dt` in changed physical solvers except a comment.
- PASS: Omega polish scan over touched authority files shows no `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, `FloatMode.Fast`, `FloatPrecision.Low`, or `/ dt`.
- PASS: No-build recheck on 2026-05-13: bounded NativeQueue lanes added, correction flags moved out of fault-bit range, correction desync now reports `AuthoritativeHash`, rotation payloads are canonicalized with `math.rsqrt`, and future-frame input overrides no longer consume early.
- PASS: No-build static check on 2026-05-13: `git diff --check` passed for touched files; targeted forbidden-pattern scans returned no matches.
- PASS: No-build hardening on 2026-05-13: correction position/velocity now require explicit valid payload flags or preserve current state; authoritative-only hash packets now compare; millimeter quantization clamps before int conversion.
- PASS: No-build stability pass on 2026-05-13: KCC body drag now uses reciprocal damping, roll phase is kept inside deterministic signed-pi range, Sync-Fence rotation comes from committed state buffer, and origin-shifted sync buffers are rehashed.
- PASS: No-build lifecycle pass on 2026-05-13: enable warm-state velocity remains snapped, deterministic session counters/input/fence/GPU-flow telemetry reset on enable/dispose, and fault dump guards missing telemetry buffers.
- PASS: `HectonPlayerMovement.cs` source scan on 2026-05-13: impulse/velocity-change usage is routed through `PhysicsForceRouter`; no direct `Input.GetAxis`, synchronous raycast/capsulecast, `math.sqrt`, or `Vector3.magnitude` hits found by targeted scan.
- BLOCKED: Unity batchmode compile cannot own the project while another Unity editor instance is open; batch log exits before script compilation. Runtime/Burst verification remains PENDING VERIFICATION.

## Loop Log

- Loop 1 Tasks 1-5: Prompt/domain/mandates extracted; singleton scan, signal migration, contracts-only asmdef, sqrt/magnitude scan, KCC millimeter snap.
- Loop 2 Tasks 6-10: Re-read prompt; rcp division cleanup, deterministic trig helper, 300-frame hash fence, desync signal, authoritative correction snap.
- Loop 3 Tasks 11-15: Re-read local code; double-buffer commit in PostFixed, origin-shift state buffers, async cast audit, low-tier 2-step resolver, dispatcher lanes.
- Loop 4 Tasks 16-19: Blackbox Sync-Fence telemetry, impact event bus, submarine autopilot quantization, deterministic Burst attributes.
- Loop 5 Self-Inquisition: Found `SyncFenceSignal` fixed struct size too small for payload; corrected to 128 bytes and re-ran compile/static scans.
- Loop 6 No-Build Quality Pass: User forbade `dotnet build`; source review found and fixed unbounded signal queues, state flag overlap with fault bits, correction hash attribution, early future-frame input override consumption, and quaternion sign/length canonicalization.
- Loop 7 No-Build Hardening Pass: Source review fixed default correction packets snapping to origin/zero velocity, added velocity-valid correction flag, compared authoritative-only hashes, and clamped quantization conversion.
- Loop 8 No-Build Stability Pass: Source review fixed frame-step-sensitive drag, long-session roll phase growth, body-rotation Sync-Fence drift, and stale origin-shift state hashes without launching `dotnet build`.
- Loop 9 No-Build Lifecycle Pass: Source review fixed re-enable stale velocity/state leakage, reset session-local determinism counters and telemetry, guarded fault dumps, and audited `HectonPlayerMovement.cs` for direct input/physics violations without launching `dotnet build`.
