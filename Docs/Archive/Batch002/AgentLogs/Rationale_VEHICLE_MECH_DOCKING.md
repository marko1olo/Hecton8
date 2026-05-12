# VEHICLE_MECH_DOCKING Rationale

Status: PENDING VERIFICATION

## Decision 0 - Mandate Selection

Problem: Docking failure involves vehicle kinematics, AUP origin shifts, Unity physics joints, HUD/audio side effects, and Seaglide inventory state.
Solution: Loaded vehicle AUP, physics determinism, floating origin, zero-GC, GlobalRegistry, crash telemetry, cinematic-cheat, and SOA inventory mandates before code.
Rejected Alternatives: Reading every registry mandate would add noise and violate contextual ingestion; coding from prompt alone would miss AUP and zero-GC contracts.
Scalability potential: Low uses instant snap and minimal per-tick work; Middle/High/Ultra can spend saved cycles on interpolation, audio weight, haptics, and richer docking feedback.
Hardware Impact: Estimated low-end i3/MX350 gain is preventing FixedJoint solver spikes, target savings unknown until Unity profiler; status PENDING VERIFICATION.

## Decision 1 - Kinematic Lock Instead Of Unity Joint

Problem: FixedJoint and CharacterJoint can explode during origin shifts and high mass-ratio docking.
Solution: Use a deterministic state machine: approach gate by distancesq and alignment dot, disable dynamic forces, set Rigidbody kinematic, sync matrix to dock pose, and eject with finite-checked velocity.
Rejected Alternatives: Unity FixedJoint, CharacterJoint, ConfigurableJoint, or transform.SetParent runtime locking. These create solver instability or reference-frame tearing.
Scalability potential: Low instant snap; Middle S-curve; High adds richer audio/haptic pulses; Ultra can add visual clamp alignment polish while physics remains cheap.
Hardware Impact: Avoids PhysX joint solver iteration and depenetration spikes on i3/MX350. Estimated hot-path savings: 20-80 us during dock/undock events, pending profiler proof.

## Decision 2 - AUP Relative Dock State

Problem: Docking to a habitat while the ocean origin shifts can tear interpolation if the target pose is treated as global runtime coordinates only.
Solution: Store docking start/target as `AbsoluteUniversePosition`, derive dock target relative to the owning habitat AUP, and finalize instantly on origin-shift notification.
Rejected Alternatives: `transform.SetParent`, local transform offsets, or runtime-only Vector3 interpolation. Those break during floating-origin rebases or introduce hidden parent scale/rotation coupling.
Scalability potential: Low/MX350 snaps immediately; Middle runs 1.5s S-curve; High/Ultra can layer richer clamp lights/audio while the physical state remains a kinematic matrix sync.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding transform hierarchy churn and solver warm-start loss; expected event savings 10-40 us versus parent/joint repair paths.

## Decision 3 - Seaglide As KCC Force And Drag Modifier

Problem: Treating Seaglide as a vehicle would create an extra authority stack and fight player KCC movement.
Solution: Keep Manta as `IPlayerTransportSource`, expose forward propulsion and a drag coefficient multiplier, and apply both through `HectonPlayerMovement`.
Rejected Alternatives: Rigidbody vehicle body, separate scooter controller, or tool-specific player movement branches. Those fragment the KCC authority model.
Scalability potential: Low uses one scalar drag multiplier; Middle/High/Ultra can add presentation/audio from boost without changing physics.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding a second Rigidbody solve and contact pass; hot-path cost remains scalar math, under 3 us/player tick.

## Decision 4 - SOA Condition Drain

Problem: Manta battery drain existed as a local float only, so thrust did not touch the inventory ItemCondition SOA.
Solution: Added `PlayerInventory.TryDrainItemConditionByHash` and routed Manta thrust drain through an accumulator to mutate `_qualityMilli` and `_durabilities` at the anchor index.
Rejected Alternatives: Per-item MonoBehaviour battery state, object lookup every frame with no accumulator, or duplicating tool condition outside inventory. Those violate SOA and create drift.
Scalability potential: Low drains only when accumulated threshold is crossed; High/Ultra can visualize battery fatigue from the same condition value.
Hardware Impact: Estimated low-end i3/MX350 cost is bounded inventory scan after accumulator threshold; avoids managed allocation and keeps NativeArray mutation direct.

## Decision 5 - Decoupled Hatch HUD Drone Hooks

Problem: Hatch UI, driving HUD, and drone mass owners are separate domains running under parallel-agent constraints.
Solution: Expose allocation-free booleans and scalar hooks from docking, then push attached drone mass into `SubmarineFluidDynamics` through a generic external mass setter.
Rejected Alternatives: Direct HUD mutation, direct drone manager dependency, or scene-wide hatch searches. Those create brittle cross-domain coupling.
Scalability potential: Low reads one bool/scalar; High/Ultra can add richer HUD state and mass-driven animation without altering docking.
Hardware Impact: Estimated low-end i3/MX350 gain is avoiding hierarchy scans and event-string dispatch; query cost stays near 1-2 us.

## Decision 6 - Black Box Ring

Problem: A docking NaN or invalid pose must leave deterministic evidence instead of a chat-only claim.
Solution: Added a fixed 300-entry `NativeArray` ring in `VehicleDockingModule` and dumps `Docs/AgentLogs/Dump_VEHICLE_MECH_DOCKING.bin` on invalid pose detection.
Rejected Alternatives: `List<T>` telemetry, Debug.Log-only traces, or JSON dumps from the hot path. Those allocate or lose pre-crash sequence data.
Scalability potential: Low stores compact state only; High/Ultra can decode richer diagnostics without changing runtime mechanics.
Hardware Impact: Estimated low-end i3/MX350 hot-path cost is one bounded struct write while active; dump cost is cold-path only.

## Decision 7 - Cached SOA Anchor Drain

Problem: The first Seaglide condition-drain pass used item-hash lookup every accumulator tick, which is correct but wastes cycles once the equipped tool's inventory anchor is known.
Solution: Added `PlayerInventory.TryDrainItemConditionAtAnchor` and made `MantaScooter` cache the validated anchor index plus item hash. If the item moves, reservation changes, or the hash no longer matches, the path falls back to `TryDrainItemConditionByHash` and refreshes the cache.
Rejected Alternatives: Keeping the full SOA scan on every drain tick, storing battery condition locally on the scooter, or binding to a concrete inventory slot owner. Those either waste hot-path budget or split condition authority away from inventory.
Scalability potential: Low uses a single anchor check after first hit; Middle/High/Ultra can drive richer battery fatigue visuals from the same `_qualityMilli` without adding runtime lookup cost.
Hardware Impact: Estimated i3/MX350 gain is replacing N-anchor scan with one bounds/hash/reservation check on steady thrust; target cost drops from roughly 2 us on scan pass to roughly 0.5 us cached, pending profiler proof.

## Decision 8 - OMEGA POLISH CHANGES

Problem: OMEGA audit found avoidable "honest math" in active docking: forward vectors were normalized even though Unity/Quaternion forward vectors are already unit direction, S-curve time used division, and telemetry cursor used modulo.
Solution: Replaced normalize calls with direct finite dot checks, converted `_dockingElapsedSeconds / duration` to `_dockingElapsedSeconds * math.rcp(duration)`, changed the telemetry ring cursor from modulo to branch wrap, skipped idle telemetry writes, and kept low-tier instant snap as the Math LOD path.
Rejected Alternatives: Full physical docking simulation, trig-heavy smoothing, List-backed telemetry, and unconditional interpolation on weak hardware. Those spend frame budget without buying reliable immersion.
Scalability potential: Low/MX350 snaps immediately and records telemetry only while active; Middle uses 1.5s S-curve; High/Ultra can spend saved CPU on clamp lights, audio/haptics, and overkill docking presentation while physics remains deterministic.
Hardware Impact: Estimated i3/MX350 gain is small but real: removes two normalize paths per acquisition/telemetry sample, one float division per fixed docking tick, and one modulo per active telemetry write. Exact profiler value remains blocked by external compile errors.

Verification Constraint: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /m:1` still fails outside vehicle code. The latest run reaches `Hecton8.Core` and reports 95 external errors, led by unresolved `HectonPersistentPathPolicy`, `HectonNativeBridge`, `HectonNativeLibrary`, `SteamDeckInputPal`, `VoxelChunkModifiedEvent`, `VoxelChunkModifiedEvents`, `HapticWaveformLibrary`, `HardwareTierDetector`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, and `HectonThreadRole`. Therefore status remains PENDING VERIFICATION, not VERIFIED MASTER GRADE.

## Decision 9 - Fail-Closed Dock Lifecycle

Problem: A missing or invalid dock anchor during finalize/origin-shift could leave the transport marked occupied while its body stayed kinematic, and undocking depended on trigger exit even though a hard-locked kinematic vehicle may never physically exit the trigger.
Solution: Made anchor snap return a success bool, abort docking on invalid pose by dumping the black-box ring and releasing the transport, added public `TryUndock()` for explicit eject release, defensively dispose telemetry on destroy, and clear attached drone mass on release.
Rejected Alternatives: Waiting for `OnTriggerExit`, leaving the transport locked until another system repairs the anchor, or adding a direct UI dependency. Those create hidden state traps and violate parallel-agent decoupling.
Scalability potential: Low gets deterministic fail-closed release with no solver work; Middle/High/Ultra can call the same `TryUndock()` from richer diegetic controls without adding physics coupling.
Hardware Impact: Estimated i3/MX350 gain is avoiding pathological stuck-kinematic recovery and repeated telemetry dumps; steady-state cost is zero because the new branch only runs on release/finalize/origin-shift.
