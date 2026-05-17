# Rationale: AUP_DETERMINISM_WATCHDOG

Prompt: `AUP_DETERMINISM_WATCHDOG`
Domain: `PHYSICS/AUP`
Role: `PHYSICS_PROGRAMMER`

## Pre-Code Mandate Selection

Problem: Float truncation and stale origin-shift math can corrupt physics at >5000m.
Solution: Use AUP authority, double3 deltas at math boundaries, millimeter quantization at commits, and 300-frame sync-fence telemetry.
Rejected Alternatives: Raw `Vector3`/`float3` world-space authority; `Vector3.Distance`; `math.sqrt` for thresholds; per-frame string/debug logging.
Scalability potential: Low uses sparse probes and presentation smoothing; Middle retains hash plus samples; High retains render matrix samples; Ultra keeps full 300-frame debug payload.
Hardware Impact: Static proof pending. Expected benefit on i3/MX350 comes from avoiding sqrt, eliminating float jitter correction churn, and keeping authority in compact AUP math.

## Mandates Read

- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `CORE_Submarine_Vehicles_Kinematics_AUP.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Decisions

### Loop 1: Tasks 1-5

Problem: Submarine station keeping truncated an AUP double3 target delta to float3 before position integration.
Solution: Keep `_targetAbsolutePosition - currentAbsolutePosition` as double3, use double lengthsq and guarded double rsqrt for the step, and cast only at the final Rigidbody `Vector3` handoff.
Rejected Alternatives: `Vector3.MoveTowards` and float3 offset integration were rejected because both collapse absolute-space precision before the physical move.
Scalability potential: Low/Middle keep the same cheap final handoff; High/Ultra get exact long-range station keeping with no vibration at deep offsets.
Hardware Impact: Estimated 1.2 us saved per active station-keeping hull on i3/MX350 by avoiding float jitter corrections and redundant MoveTowards normalization.

Problem: Dynamic-flood feedback used `math.sqrt()` after a squared threshold.
Solution: Preserve the squared threshold and feed the visual/audio stress signal through max/mid/min magnitude approximation.
Rejected Alternatives: Exact Euclidean magnitude was rejected because the value is a cinematic stress intensity, not an authoritative physics constraint.
Scalability potential: Low uses the same cheap fake; High/Ultra can spend saved cycles on richer hull stress VFX instead of exact scalar math.
Hardware Impact: Estimated 0.2 us saved per flood feedback event on i3/MX350.

Problem: KCC runtime counters for sync-fence, GPU-flow, squeeze telemetry, and AUP pre-shift state were scattered as class fields.
Solution: Pack them into `PlayerKinematicsAccumulatorState`, a sequential unmanaged struct owned by the runtime.
Rejected Alternatives: Leaving counters as independent managed fields was rejected because replay/debug state needs one compact deterministic boundary.
Scalability potential: Low keeps minimal counters; Middle/High/Ultra can extend the struct for more telemetry without per-frame allocations.
Hardware Impact: Direct frame gain is negligible; the practical gain is lower replay/debug ambiguity and no GC exposure.

Problem: `AUPMath.AUPDirection` normalized a double delta with an unguarded near-zero rsqrt threshold and reported invalid casts without a safe return.
Solution: Use `math.lengthsq(double3)`, guard `math.rsqrt(math.max(distSq, 0.0001d))`, and return zero on invalid final float3.
Rejected Alternatives: Float normalization and `Vector3.Distance` were rejected because both lose precision and add sqrt cost.
Scalability potential: Same deterministic kernel on all tiers; High/Ultra can build richer AUP collision/features on a stable primitive.
Hardware Impact: Estimated 0.03 us saved per call plus removal of NaN recovery churn.

Problem: KCC states need replay-identical commits.
Solution: Verified `PlayerKinematicsBodyJob` and `StageStateWrite` both snap position and velocity with `DeterministicPhysicsMath.SnapMillimeter` before state commit.
Rejected Alternatives: Transform-space authority and post-render snapping were rejected because they create nondeterministic replay deltas.
Scalability potential: Low/Middle get stable cheap movement; High/Ultra can layer visual interpolation over byte-stable physics state.
Hardware Impact: Estimated 2.0 us saved on bad drift-correction frames by avoiding correction churn.

Problem: Loop 1 compile verification is blocked outside the AUP domain.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; after three attempts, failures remained in concurrent `GlobalSignals`/`CombatDamageRuntime` damage-signal migration.
Rejected Alternatives: Continuing to patch another agent's signal migration was rejected after the 3-strike protocol.
Scalability potential: No runtime effect; integrator needs to complete the signal rename before authoritative compile validation.
Hardware Impact: None from this dependency wall.

### Loop 2: Tasks 6-10

Problem: `RigidbodyAUPs` had to be verified as SoA without breaking lockstep/data-vault contracts.
Solution: Confirmed `_rigidbodyAUPs` is a contiguous `NativeArray<float3>` allocated from `BufferID.RigidbodyAUPs` or H8Memory and passed directly into `PhysicsDistanceCullingJob`.
Rejected Alternatives: Replacing the lane with full `AbsoluteUniversePosition` was rejected because `LockstepStateValidator` and DataVault consumers already depend on the compact camera-relative float3 lane.
Scalability potential: Low/Middle keep compact culling; High/Ultra can add separate AUP-precise lanes without invalidating this hot cull buffer.
Hardware Impact: 0.0 us direct gain; avoids broad contract churn and extra memory bandwidth on i3/MX350.

Problem: KCC could integrate through an AUP rebase frame and amplify delta-time spikes.
Solution: Consume `AupPreShiftSignal`, cancel pending state writes, preserve current snapped position/velocity, and publish a frozen KCC velocity frame.
Rejected Alternatives: Applying a correction after integration was rejected because it allows a bad physics step to enter sync-fence state.
Scalability potential: Low gets spike suppression; High/Ultra can layer richer visual rebase smoothing over the frozen authoritative frame.
Hardware Impact: Estimated 20-80 us avoided during shift frames on i3/MX350 by skipping KCC job and correction churn.

Problem: Distant flora could use cheaper float math only if explicitly gated by math LOD.
Solution: Add `_MATH_LOD_LOW` branch that uses float offset approximation beyond 1000m; non-low and near flora use double offset until final matrix upload.
Rejected Alternatives: Always using float offsets was rejected because nearby flora would reveal AUP jitter; always using double was rejected for MX350 distant bulk copies.
Scalability potential: Low uses cheap far-field approximation; Middle/High/Ultra retain exact offset path for visual overkill.
Hardware Impact: Estimated 5-20 us saved during active vegetation payload copies on i3/MX350 depending on instance count.

Problem: Leviathan grab contact used runtime float positions for damage direction/contact even though root/target AUP caches existed.
Solution: High/Ultra path reads root/tip `AbsoluteUniversePosition`, resolves double3 direction with guarded rsqrt, and converts only the final contact point to runtime space.
Rejected Alternatives: Running the 64-bit contact on every tier was rejected because low tiers should spend cycles on silhouette and one-iteration Verlet stability.
Scalability potential: Low keeps cheap float Verlet; High/Ultra get exact 64-bit grab contacts for cinematic tentacle overkill.
Hardware Impact: Estimated +0.1 us per high-tier grab damage tick; no MX350 cost because the branch is tier-gated.

Problem: Marine snow must not pop during AUP rebase.
Solution: Verified existing renderer accumulates `_pendingAupShiftOffset`, rebases flow-field centers, and uploads `_AupShiftOffset` to compute before dispatch.
Rejected Alternatives: Rebuilding particle simulation around CPU particles was rejected because the GPU ping-pong path already has the correct shift handoff.
Scalability potential: Low keeps cheap offset vector upload; High/Ultra can increase particle capacity without changing rebase semantics.
Hardware Impact: 0.0 us new cost; existing vector upload prevents visible jump.

Problem: Loop 2 compile verification remains blocked outside the AUP task.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; build now reports broad missing-contract fallout in `GlobalRegistry`, movement contracts, macro database, foveated simulation, and world pager.
Rejected Alternatives: Repairing broad project contract fallout was rejected as an unrelated dependency wall after the prior 3-strike stop.
Scalability potential: None until integrator restores project contracts.
Hardware Impact: None from the dependency wall.

### Loop 3: Tasks 11-13

Problem: Temporal upscalers smear if origin-shift rebases only one side of the previous/current visual sample pair.
Solution: Verified `FoveatedSimulationManager.OnOriginShift` subtracts `shiftOffset` from both `_visualFromPositions` and `_visualToPositions`, preserving the motion-vector delta.
Rejected Alternatives: Clearing visual history on every shift was rejected because it trades one smear for a visible temporal pop.
Scalability potential: Low keeps stable previous/current samples; High/Ultra retain TAA/STP stability while increasing visual target count.
Hardware Impact: 0.0 us new cost; preserves existing motion-vector cache.

Problem: Remaining rsqrt paths could still rely on branch guards instead of explicit max clamps.
Solution: Guard AUP double direction, high-tier tentacle AUP contact, station-keeping double step, and fallback Leviathan grab direction with `math.max(distSq, 0.0001)`.
Rejected Alternatives: Relying on `distSq > epsilon` was rejected because NaN/denormal edge cases can survive branch-only hygiene.
Scalability potential: Same guard on all tiers; High/Ultra can run exact contact math without NaN risk.
Hardware Impact: Negligible steady-state cost; avoids expensive NaN recovery and blackbox dumps.

Problem: AUP drift and sync-fence hash were not explicitly present in the KCC telemetry payload.
Solution: Added `AupMaxDriftErrorMeters`, compute drift from AUP double absolute position against runtime position, write sync-fence telemetry every 300 frames, and dump `Dump_AUP_DETERMINISM_WATCHDOG.bin` on fault.
Rejected Alternatives: Text logging or per-frame managed reports were rejected because they violate zero-GC and hot-path timing.
Scalability potential: Low stores compact drift/hash; High/Ultra can correlate with richer visual telemetry without changing the ring.
Hardware Impact: Estimated 0.03 us every 300 frames on i3/MX350; fault dump is off hot path.

Problem: Loop 3 compile verification is still blocked outside AUP.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; build stops on `ProceduralLadderClimbRuntime` missing `Hecton8.Input.Universal` and `UniversalInputStateSignal`.
Rejected Alternatives: Repairing the input package/namespace migration was rejected as unrelated to this AUP determinism task.
Scalability potential: None until input dependency is restored.
Hardware Impact: None from the dependency wall.

### Loop 4: Tasks 14-16

Problem: `PlayerKinematicsRuntime` double3 conversion work needed a repair pass without absorbing unrelated compile fallout.
Solution: Searched for stale `_lastSyncFenceHash` / `_lastSyncFenceFrame` usage, verified the accumulator struct owns the sync-fence state, and reran the compile gate. No `PlayerKinematicsRuntime` double3 conversion error remains in the latest build output.
Rejected Alternatives: Editing `LockstepStateValidator`, `GlobalSignals`, shader vault bridges, or bootstrap assembly references was rejected because those are separate agents' dependency walls.
Scalability potential: Low/Middle/High/Ultra all benefit from isolated KCC state without broad contract churn.
Hardware Impact: 0.0 us direct runtime gain; prevents false AUP blame during integration.

Problem: Homeostasis adaptation is explicitly N/A for this core math prompt.
Solution: Marked it complete without introducing a fake stress branch.
Rejected Alternatives: Adding a kill-switch or quality-tier branch to deterministic KCC math was rejected because deterministic physics authority must not vary by stress unless the prompt requires it.
Scalability potential: Low/Middle/High/Ultra keep identical authoritative math.
Hardware Impact: 0.0 us.

Problem: Player gravity used Unity's local down vector as default authority, so far-from-center AUP positions could keep a non-planetary acceleration direction.
Solution: Resolve default gravity from predicted AUP absolute position toward the AUP center using double3 length squared, guarded double rsqrt, and final Vector3 conversion only after normalization.
Rejected Alternatives: Continuing to use `UnityEngine.Physics.gravity` as direction authority was rejected because it ignores AUP center. Simulating gravity fields or planet-radius force falloff was rejected because the task only requires direction correction and the frame budget is 0.1 ms.
Scalability potential: Low uses the same cheap radial vector; Middle keeps deterministic direction; High/Ultra can spend saved correctness budget on visual horizon/atmosphere exaggeration without changing physics authority.
Hardware Impact: Estimated +0.04 us per fixed tick on i3/MX350, below the suspicious threshold; avoids larger correction spikes from wrong gravity frames.

Problem: Loop 4 compile verification still cannot reach an all-clear.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; latest blockers are missing `Hecton8.Core.Contracts`/`Hecton8.Core.Memory` references, broken `LockstepStateValidator` members, missing signal structs, shader-vault bridge references, and unrelated fauna/bootstrap type drift.
Rejected Alternatives: Fixing those external migrations was rejected under the 3-strike/dependency-wall rule.
Scalability potential: None until integrator restores dependency graph.
Hardware Impact: None from the dependency wall.

### Loop 5: Tasks 17-18

Problem: Replay sync needs byte-identical state after origin shifts and floating-point drift.
Solution: Verified the KCC Burst job snaps position and velocity with `DeterministicPhysicsMath.SnapMillimeter`, `StageStateWrite` snaps again before state commit, and `BuildSyncFenceHash` hashes integer millimeter-quantized AUP locals, velocity, and rotation components.
Rejected Alternatives: Hashing raw float bytes or runtime transform positions was rejected because replay order, origin shifts, and platform float noise can diverge.
Scalability potential: Low can keep 300-frame hash cadence; Middle/High/Ultra can increase telemetry density while preserving the same millimeter authority.
Hardware Impact: Estimated 2-5 us avoided on drift frames by preventing correction churn; steady-state hash cost remains tiny and cadence-gated.

Problem: Final validation cannot pass while unrelated compile walls are present.
Solution: Executed the final compile command and recorded `[BLOCKED BY DEPENDENCY]` with concrete blocker families.
Rejected Alternatives: Reporting "verified compile" would be false; reverting external agents' work would violate workspace ownership.
Scalability potential: AUP changes remain isolated for integrator replay once global compile is restored.
Hardware Impact: None from the compile wall.

### Omega Polish

Problem: The polish mandate required removing `Vector3.Distance`; whole-project scan found two remaining acoustic portal segment calls in `SpatialAudioManager`.
Solution: Replaced them with `ResolveRuntimeDistanceMeters`, an allocation-free squared-length plus guarded rsqrt helper.
Rejected Alternatives: Leaving them because they were outside the physics domain was rejected because the polish mandate was explicit. Rewriting spatial audio routing was rejected as cross-domain bloat.
Scalability potential: Low avoids sqrt on portal graph builds; Middle/High/Ultra keep the same portal topology with cheaper scalar distance resolution.
Hardware Impact: Estimated 0.02-0.05 us saved per acoustic portal graph build on i3/MX350; no per-frame allocation impact.

Problem: The 0.1 ms frame-time dictatorship had to be checked after all AUP edits.
Solution: Kept new hot-path math scalar and cadence-gated: AUP gravity is one double3 normalization per fixed tick, sync-fence drift runs every 300 frames, high-tier tentacle exact contact is quality-gated, and low-tier flora uses `_MATH_LOD_LOW` float approximation beyond 1000m.
Rejected Alternatives: Any all-tier 64-bit collision sweep, per-frame text logging, or broad audio/physics architecture rewrite was rejected as frame-budget debt.
Scalability potential: Low/MX350 uses approximations and cadence gates; Middle keeps exact AUP only near visible physics; High gets exact tentacle contact; Ultra can increase visual telemetry density without changing authority.
Hardware Impact: Estimated added cost remains below 0.1 ms: ~0.04 us per fixed tick for radial gravity, ~0.03 us per 300 frames for AUP drift, +0.1 us only on high-tier grab damage ticks, and 5-20 us saved on low-tier distant flora payload copies.

Problem: Final compile after polish still fails outside AUP.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false`; current blockers are missing `Hecton8.VFX.Wakes` types, missing `IDockingAutopilotService`/`ActiveSplineData`, and `EcosystemDirector` interface implementation drift.
Rejected Alternatives: Patching wake VFX, docking, or ecosystem contracts was rejected as another agent's dependency wall.
Scalability potential: AUP scope is ready for integration once those contracts compile.
Hardware Impact: None from the external compile wall.

### Phase 6: Multiplatform Inquisition

Problem: ARM64/Quest builds cannot rely on platform-default struct padding for physics packets and telemetry rings.
Solution: Enforced `StructLayout(Pack = 1)` on AUP/KCC/physics-facing structs touched by this pass: KCC telemetry/state/accumulator, `PhysicsDeterminismSignals` packets, docking spline packets, Leviathan telemetry, and player movement telemetry/callback structs. `ActiveSplineData` keeps an explicit 144-byte stride with `ReservedTail` so the DataVault packet stride remains stable while the padding is no longer implicit.
Rejected Alternatives: Leaving `Pack = 4`, `Pack = 8`, or default sequential layout was rejected because that lets Mono/IL2CPP/Burst choose alignment details per platform. Rewriting unrelated world/core packet families was rejected as cross-domain churn outside this AUP pass.
Scalability potential: Low/Quest gets deterministic packet strides; Middle/High/Ultra preserve the same serialized/vault layout with no runtime branch.
Hardware Impact: Estimated 0.0 us steady-state gain; the benefit is removing ARM64 layout drift and avoiding misread packet recovery work.

Problem: KCC still appeared to own private NativeArrays even after accumulator cleanup.
Solution: Verified every KCC runtime scratch/state array now resolves through `AllocateRuntimeArray(..., BufferID.*, SystemID.GameplayPlayer, GlobalDataVault)` first. The only local allocation path left is the H8Memory fallback with `SystemID.GameplayPlayer` when the vault is missing.
Rejected Alternatives: Replacing NativeArray job fields with managed containers or direct singletons was rejected because Burst jobs require native views and the vault already owns the storage contract.
Scalability potential: Low keeps one compact vault-backed KCC state set; Middle/High/Ultra can expand BufferIDs without changing job signatures.
Hardware Impact: Estimated 0.0 us direct gain; prevents duplicate persistent allocations and improves memory accounting on 8GB systems.

Problem: Additional scanned physics-adjacent math still had branch-only `rsqrt` guards, one guarded divide, or direct `math.sqrt`.
Solution: Clamped station-keeping, CCD normalization, fluid ingress sqrt approximation, tether constraint weighting, docking spline distance, and acoustic portal reverb curve through `math.max` + `math.rsqrt`/`math.rcp`.
Rejected Alternatives: Keeping branch-only guards was rejected because mobile NaN propagation can survive edge-case data. Simulating richer audio/physics response was rejected because these are scalar authority/perception curves.
Scalability potential: Low gets deterministic safe fallbacks; High/Ultra spend saved stability budget on existing tier-gated contact precision and particle density rather than extra physics.
Hardware Impact: Estimated 0.01-0.05 us saved across cold/warm scalar calls; primary value is avoiding NaN fault cascades.

### Phase 7: Compile Gate Burn-Down

Problem: Final validation still had fixable compile walls after the AUP scope was clean.
Solution: Restored a single `GlobalDataVault.ValidateAbiLayout`, localized missing lockstep typed-lane constants, completed `SubmarineFluidDynamics` DataVault handle fields/allocation glue, and reran the full build until `Hecton8.Core.csproj` passed with 0 warnings and 0 errors.
Rejected Alternatives: Leaving compile as `[BLOCKED BY DEPENDENCY]` after fixable glue errors was rejected. Rewriting hydrodynamic behavior, fauna cognition, or docking gameplay was rejected because the required repair was contract glue, not domain logic.
Scalability potential: Low/Quest/Steam Deck keep vault-owned state and typed lanes; High/Ultra retain the same deterministic buffers and can spend visual budget elsewhere without changing physics authority.
Hardware Impact: 0.0 us steady-state runtime gain from compile glue. The practical low-end gain is avoiding duplicate persistent native ownership and making memory sentinels see the correct `SystemID`.

### Phase 8: Post-Reopen Multiplatform Burnish

Problem: A broader post-reopen scan found AUP/vehicle-fluid structs still relying on explicit size without explicit `Pack = 1`, plus several ballast/hydro `rsqrt` and magnitude paths that were only branch-guarded.
Solution: Added `Pack = 1` to `AbsoluteUniversePositionBlit`, `SplashEvent`, ballast PID packets, hydro job packets, and hydro transfer jobs. Replaced branch-only `rsqrt` with `math.rsqrt(math.max(...))` and moved ballast stress/cavitation rumble magnitude to the existing max/mid/min approximation.
Rejected Alternatives: Reordering packet fields, changing packet sizes, or converting stress/rumble back to exact magnitude was rejected because these are serialization/job ABI and perception paths, not authority improvements.
Scalability potential: Low/Quest gets deterministic packet layout and cheaper stress math; Middle/High/Ultra keep identical authority and can spend saved scalar budget on the existing high-tier contact/VFX lanes.
Hardware Impact: Pack changes are 0.0 us runtime. Magnitude/guard cleanup is estimated at 0.02-0.08 us saved on stress/rumble events; primary gain is NaN fault avoidance on mobile GPUs.

Problem: Post-reopen global compile no longer matches the earlier green state.
Solution: Ran the compile gate three times and wrote attempt dumps. Current failures are external to this AUP domain: missing native/vault fields in `World/SargassumMicroFaunaBoids.cs` and unassigned `localPoint` in `RepairTool.cs`.
Rejected Alternatives: Claiming green compile or patching broad world/repair ownership after the three-strike dependency wall was rejected.
Scalability potential: No AUP runtime effect; the AUP patch set remains isolated and scan-clean for integrator replay after external owners restore compile.
Hardware Impact: 0.0 us.

Problem: `PhysicsDeterminismSignals` still owned private `NativeQueue` lanes instead of the project typed signal lane path.
Solution: Converted the shim to configure and publish `InputSignal`, `StateCorrectionSignal`, `DesyncDetectedSignal`, `SyncFenceSignal`, and `KccVelocitySignal` through `SignalBus<T>`. All five packet structs now implement `ISignal`; existing latest-value sidecars remain unmanaged value caches for compatibility.
Rejected Alternatives: Keeping local `NativeQueue` ownership was rejected because it bypasses typed-lane telemetry and central sentinel ownership. Adding managed delegates or a new event bridge was rejected as worse signal fragmentation.
Scalability potential: Low/Quest gets central lane caps and memory accounting; Middle/High/Ultra keep the same KCC/lockstep API while using the common signal telemetry path.
Hardware Impact: 0.0 us direct frame gain; practical gain is one fewer private native queue family and unified SignalBus memory sentinel reporting.

### Phase 9: Typed-Lane Event Inquisition

Problem: The physics presentation event bridges still owned private native event queues and relied on local deferred dispatch state outside the central signal telemetry path.
Solution: Converted `FluidFeedbackEvents`, `PhysicsEventBus`, and deferred submarine impact trauma dispatch to typed `SignalBus<T>` lanes. `SplashEvent`, `PhysicsEventPayload`, and `DeferredSubmarineImpactSignal` are packed unmanaged `ISignal` payloads; consumers drain `ReadOnlySpan<T>` snapshots and requeue unconsumed tails on late-frame budget exhaustion.
Rejected Alternatives: Renaming/removing `PhysicsEventBus` was rejected because existing listeners and producers depend on the API name. Converting fixed-step `ForcePacket` command queues in the same pass was rejected because they expose a `NativeQueue<ForcePacket>.ParallelWriter` command contract for physics jobs and need a separate vault-backed command-buffer migration.
Scalability potential: Low/Quest gets central lane caps and no duplicate presentation event queues. Middle/High/Ultra keep the same listener API while SignalBus telemetry can scale event caps by tier.
Hardware Impact: 0.0 us direct frame gain. Practical gain is lower memory-accounting fragmentation and removal of redundant private native queues; requeue cost only appears on late-frame budget exhaustion.

Problem: `PhysicsApplySystem` still had physics-facing packets without explicit ARM64 pack declarations.
Solution: Added `Pack = 1` to force packet, pressure, EMP, acoustic ping, acoustic impulse, large acoustic impulse, and deferred submarine impact packets.
Rejected Alternatives: Reordering or resizing packets was rejected because force application and listener payloads already depend on the field order.
Scalability potential: All tiers keep identical packet ABI; Quest/Android avoid implicit padding drift.
Hardware Impact: 0.0 us runtime gain; removes ABI ambiguity on ARM64.

Problem: Final compile status needed to reflect the current workspace, not stale dependency-wall logs.
Solution: Re-ran the full `Hecton8.Core.csproj` build after the typed-lane conversion and source stabilization.
Rejected Alternatives: Reporting blocked from older attempt logs was rejected after the latest compile passed.
Scalability potential: Green compile restores integration confidence for low/high tier paths.
Hardware Impact: 0.0 us runtime; compile hygiene only.

### Phase 10: Force Command Vault Migration

Problem: `PhysicsApplySystem` still owned fixed-step force command storage as private `NativeQueue<ForcePacket>` front/back queues plus private validation `NativeArray` fields. That violated DataVault sovereignty even after the event lanes were migrated.
Solution: Audited all force producers and confirmed the `NativeQueue<ForcePacket>.ParallelWriter` accessor had no repo consumers. Removed the dead writer API and moved force command front/back buffers plus validation packet/mask staging into `GlobalDataVault` via `VaultBufferHandle<T>` using `BufferID.PhysicsForceCommandFront`, `PhysicsForceCommandBack`, `PhysicsForceValidationPackets`, and `PhysicsForceValidationMask`.
Rejected Alternatives: Keeping the private queues was rejected because it preserved duplicate native ownership. Converting `ForcePacket` to a normal `SignalBus<T>` lane was rejected because signal snapshots flush on a different cadence than the fixed-step force front/back swap. A managed `List<ForcePacket>` was rejected because it breaks zero-GC and Burst validation.
Scalability potential: Low/MX350 keeps the same 64-packet cap and O(1) bounded writes with central memory accounting. Middle/High/Ultra can raise the DataVault buffer length later without changing producers or validation jobs, buying denser impact VFX/acoustic feedback while physics authority remains deterministic.
Hardware Impact: Estimated 0.0 us direct frame gain. The practical low-end gain is memory-sentinel consolidation and removal of two private persistent NativeQueue allocations; enqueue remains a bounded array write and validation remains capped to 64 packets.

Problem: Post-migration compile attempts surfaced external dependency drift after the AUP/physics force path compiled past its own changes.
Solution: Restored missing lockstep typed-lane constants and fully qualified the diagnostics `DebugSignal` reference. After those repairs, the remaining attempt 13 errors are `DiegeticGyroCompassRuntime` presentation DTO mismatch and `SystemDispatcher` missing blackbox/raycast-lock members.
Rejected Alternatives: Editing the UI compass presentation layer from the AUP agent was rejected as cross-domain ownership drift. Reporting green compile was rejected because attempt 13 is not green.
Scalability potential: No AUP runtime effect; the force buffer migration remains isolated for integration once UI/SystemDispatcher owners restore their contracts.
Hardware Impact: 0.0 us runtime; compile-wall status only.

### Phase 11: Global Physics Vault Migration

Problem: `GlobalPhysicsStateManager` still held private persistent `NativeArray` lanes and a private `NativeQueue<PhysicsImpactEventData>`, so rigidbody culling, last-valid position recovery, culling telemetry, and impact deferral bypassed full DataVault sovereignty.
Solution: Replaced those fields with typed `VaultBufferHandle<T>` bindings backed by `GlobalDataVault`. Existing culling jobs still receive contiguous `NativeArray` views resolved at schedule time, while deferred physics impacts now use a vault-backed bounded ring with read/write cursors and the same late-frame flush budget.
Rejected Alternatives: Keeping the private native containers was rejected because it preserved untracked system-owned memory. Moving collision impacts to `SignalBus<T>` was rejected for this path because SignalBus snapshots flush on pre-simulation cadence, while this collision queue must bridge fixed collision callbacks into late-frame presentation without changing timing. Managed `Queue<T>` was rejected for GC and Burst-adjacent memory accounting.
Scalability potential: Low/MX350 keeps the same capped culling and impact counts with central sentinel accounting. Middle/High/Ultra can raise DataVault capacities later for denser rigidbody wakeups, richer impact feedback, or more culling telemetry without changing the physics authority path.
Hardware Impact: Estimated 0.0 us direct frame gain; impact enqueue remains O(1), culling remains contiguous SoA, and the real low-end gain is duplicate persistent allocation removal plus cleaner memory sentinel ownership.

Problem: Global physics packets and scalar math still had ARM64/NaN risk after the force-buffer pass.
Solution: Added `Pack = 1` to `RigidbodyState`, `PhysicsConnection`, `PhysicsImpactEventData`, and `PhysicsCullingTelemetryEntry`; clamped remaining impact-normal, acoustic-energy, and rigidbody-sleep `rsqrt` paths through `math.max`.
Rejected Alternatives: Reordering struct fields or changing culling packet semantics was rejected because replay/telemetry readers depend on the current order. Branch-only `distSq > epsilon` guards were rejected because mobile NaN propagation can still poison downstream GPU or signal paths when malformed inputs slip through.
Scalability potential: Low/Quest gets deterministic packet layout and fault containment; High/Ultra keep the same culling telemetry and can spend saved stability budget on impact VFX overkill without changing authoritative physics.
Hardware Impact: Pack changes are 0.0 us runtime. Guarded `rsqrt` is neutral-to-tiny cost, estimated below 0.01 us per impacted event path, with the benefit of avoiding NaN recovery and blackbox dump churn.

Problem: Compile validation after the migration cannot be reported green.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary` and wrote attempt 14. The build stops on external save/data-baker contract drift: missing `HectonContractVersion`, `CsvReadBufferBytes`, and `SignalBusRegistry`.
Rejected Alternatives: Editing save system and data-baker ownership from the AUP agent was rejected under the domain boundary and dependency-wall protocol. Claiming AUP compile success from an incomplete build was rejected.
Scalability potential: No AUP runtime effect; the global physics vault pass remains scan-clean for integrator replay once save/data owners restore their contracts.
Hardware Impact: 0.0 us runtime; compile-wall status only.

### Phase 12: Player Blackbox Vault Closure

Problem: A broader AUP/player/physics scan still found `HectonPlayerMovement` holding the cinematic focus blackbox as a private `NativeArray<CinematicFocusTelemetryEntry>` field.
Solution: Replaced the field with `VaultBufferHandle<CinematicFocusTelemetryEntry>` and resolve the DataVault-backed `NativeArray` only inside write/dump methods. The 300-entry ring, binary dump layout, and cooldown behavior remain unchanged.
Rejected Alternatives: Keeping the cached `NativeArray` view was rejected because the audit target is no private native field ownership. Moving the cinematic focus blackbox to a managed array was rejected because fault telemetry must remain contiguous and zero-GC.
Scalability potential: Low/MX350 keeps the same 300-entry compact ring. Middle/High/Ultra can raise the DataVault buffer capacity later for richer camera/focus diagnostics without changing player movement authority.
Hardware Impact: 0.0 us direct frame gain; blackbox writes still hit contiguous DataVault storage and only run while focus telemetry is active.

Problem: Final compile status needed to be updated after the player blackbox migration and external contract drift settled.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`; attempt 15 passes with 0 warnings and 0 errors.
Rejected Alternatives: Leaving the status as dependency-blocked was rejected after objective green build evidence.
Scalability potential: Green compile restores integration confidence for low-tier and high-tier AUP paths.
Hardware Impact: 0.0 us runtime; compile hygiene only.

### Phase 13: Floating-Origin And KCC Vault Handle Closure

Problem: Post-green broad scans still found `HectonFloatingOrigin` owning three persistent drift-probe native buffers and `PlayerKinematicsRuntime` caching KCC scratch/state lanes as private `NativeArray<T>` fields. KCC storage was already DataVault-first, but the cached field type still looked like system-owned native state.
Solution: Moved floating-origin drift runtime positions, absolute positions, and invalid masks to `GlobalDataVault` handles under `BufferID.FloatingOriginDriftRuntimePositions`, `FloatingOriginDriftAbsolutePositions`, and `FloatingOriginDriftInvalidMask`. Replaced KCC cached arrays with `VaultBufferBinding<T>` handle bindings that resolve native views only for Burst job scheduling, reads, and writes. Removed the KCC local allocation/disposal helper path.
Rejected Alternatives: Keeping cached `NativeArray<T>` fields was rejected because the audit target is persistent private native ownership. Replacing job inputs with managed containers was rejected because Burst jobs need native views. Releasing every `SystemID.CoreDeterminism` vault buffer on floating-origin shutdown was rejected because that owner can contain unrelated core determinism buffers.
Scalability potential: Low/MX350 keeps compact fixed-capacity drift/KCC lanes with central memory accounting. Middle/High/Ultra can raise buffer capacity or telemetry density by BufferID without changing physics authority or job contracts.
Hardware Impact: 0.0 us measured direct frame-time gain. Static CPU cost is neutral; the gain is memory ownership consolidation and fewer invisible native lifetime paths on low-memory hardware.

Problem: Floating-origin still had two scanned `rsqrt` paths that were branch-only or not in clamp form.
Solution: Clamped drift-error and radial-origin normalization through `math.rsqrt(math.max(...))` with existing finite checks.
Rejected Alternatives: Relying on previous `distSq > epsilon` branches was rejected because malformed mobile/Quest data can still propagate non-finite state.
Scalability potential: All tiers retain identical authority; high-tier visual overkill can spend budget outside the authority path.
Hardware Impact: Below measurable frame impact; primary value is NaN fault containment.

Problem: Attempt 15 green build used normal shared project output, but concurrent editor/agent builds can lock `Temp\obj\Hecton8.Core\Hecton8.Core.dll`.
Solution: Re-ran the compile gate as attempt 16 with isolated `IntermediateOutputPath=Temp\obj\Hecton8.Core.AUP16\` and `OutputPath=Temp\bin\AUP16\`; it passes with 0 warnings and 0 errors.
Rejected Alternatives: Killing unrelated build processes was rejected under the multi-agent workspace rule. Reporting a file-lock failure as a compile wall was rejected because isolated outputs prove the AUP code compiles.
Scalability potential: No runtime effect; isolated output is validation hygiene for parallel agent work.
Hardware Impact: 0.0 us runtime; compile hygiene only.

### Phase 14: Explicit Downcast And Normalize Inquisition

Problem: The post-reopen scan still found explicit `(float3)` downcasts in physics/AUP glue and two `math.normalizesafe` camera-forward paths in the rigidbody culling job/context path.
Solution: Replaced the downcasts with explicit component construction at final Unity/PhysX handoff points. Replaced `math.normalizesafe` with a local guarded normalizer that rejects non-finite inputs and uses `math.rsqrt(math.max(lengthSq, 0.0001f))`.
Rejected Alternatives: Leaving `(float3)` casts was rejected because the prompt explicitly targets float truncation audits. Rewriting the systems to avoid Unity `Vector3` at PhysX contact boundaries was rejected because those APIs are Unity-defined final handoff surfaces, not AUP authority.
Scalability potential: Low/MX350 and Quest keep deterministic, finite final-handoff conversions. Middle/High/Ultra keep the same authority path and can spend visual budget outside physics.
Hardware Impact: 0.0 us measured direct gain. The value is NaN/precision audit closure and reduced ambiguity in static scans.

Problem: Attempt 17 failed outside AUP because `AcousticZoneController` referenced `Type` without a resolvable namespace in the current project compile.
Solution: Changed the editor-only reflection line to `global::System.Type` and removed the duplicate `using System` warning source. Attempt 20 passes with 0 warnings and 0 errors.
Rejected Alternatives: Marking a compile wall after one trivial namespace error was rejected. Broad acoustic behavior edits were rejected as cross-domain.
Scalability potential: No runtime effect; this is compile hygiene for the shared assembly.
Hardware Impact: 0.0 us runtime; compile hygiene only.

### Phase 15: Player Presentation Typed-Lane Purge

Problem: `HectonPlayerMovement` still exposed managed `System.Action` presentation broadcasts for footsteps, water splashes, exhale bubbles, sprint FOV, wet-lens pulses, transport bailout, and fatal-pressure ramp. That left a hot player movement surface outside typed signal lanes.
Solution: Added packed unmanaged player presentation signals and routed them through `SignalBus<T>`. Converted the direct consumers to `ReadOnlySpan<T>` snapshot drains: `PlayerFootstepAudio`, `HectonSurfaceWeatherDirector`, `HectonUnderwaterVisuals`, `InternalFloodWaterlineRuntime`, `CameraJuiceSystem`, and `HectonOSBootManager`.
Rejected Alternatives: Keeping delegates as "already cached" was rejected because the mandate bans managed gameplay broadcasts. Creating a duplicate submerge signal was rejected because `WaterTransitionEvent` already carries that state. Creating a wet-lens-only lane was rejected because `VisorDropletSignal` already exists for visor-local external droplet requests.
Scalability potential: Low/MX350 consumes bounded snapshot lanes and can drop noncritical presentation pulses under SignalBus low-tier caps. Middle/High/Ultra can attach richer presentation consumers to the same typed lanes without touching player movement authority.
Hardware Impact: 0.0 us measured direct frame-time gain. Static estimate: neutral-to-sub-microsecond overhead shift; value is removal of delegate subscription surfaces, central lane telemetry, and deterministic low-tier load shedding.

Problem: The presentation lane migration had to prove it did not reopen the compile wall.
Solution: Ran one isolated `dotnet build --no-restore` after completing the patch. Attempt 21 passes with 0 warnings and 0 errors.
Rejected Alternatives: Running repeated rebuild loops was rejected per user instruction. Reporting scan-only success was rejected because public API removal across consumers needs compile evidence.
Scalability potential: Green compile preserves the current low/high AUP path while enabling future presentation overkill on High/Ultra through typed consumers.
Hardware Impact: 0.0 us runtime; validation hygiene only.
