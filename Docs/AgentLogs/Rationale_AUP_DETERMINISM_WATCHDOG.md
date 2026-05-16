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
