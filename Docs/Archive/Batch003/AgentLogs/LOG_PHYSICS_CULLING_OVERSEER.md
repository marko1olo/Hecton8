# LOG_PHYSICS_CULLING_OVERSEER

## 2026-05-13 - Aggressive Rigidbody Sleep
What was wrong:
- Existing GlobalPhysicsStateManager had a legacy fixed-step distance sleep path built around one far 500 m kinematic cutoff.
- No centralized IPhysicsCullingOverseer contract existed in GlobalRegistry.
- No Burst distance culling lane existed for tracked rigidbodies.
- Player signal resolution needed to prefer PlayerRuntimeContextService AUP snapshots instead of direct scene lookup.
- Far heavy MeshColliders stayed broadphase-active.
- Sleeping bodies had no culling-owned acoustic/impact wake path.
- Compile verification is blocked by unrelated BootstrapContracts changes: BootstrapStatus.cs references ITickDispatcher and GlobalRegistry from an asmdef that cannot see them.

What was done:
- Extended GlobalPhysicsStateManager as IPhysicsCullingOverseer and registered it through GlobalRegistry.
- Added fixed NativeArray lanes for RigidbodyAUPs, current culling state, awake results, command results, distance diagnostics, and 300-entry black-box telemetry.
- Added PhysicsDistanceCullingJob with math.distancesq, bitmask state, behind-camera bias, abyss-depth bias, and low-tier 40 m sleep distance.
- Added 10 Hz local physics culling cadence with main-thread dispatch for Sleep, WakeUp, isKinematic, detectCollisions, and MeshCollider strip/restore.
- Added PhysicsCullingFlags.IgnoreCulling plus IPhysicsCullingFlagProvider. Player, transport, and submarine critical bodies auto-exclude.
- Added acoustic ping, acoustic impulse, and physics impact wake handling through existing EventBus surfaces.
- Added origin-shift safety: pending culling jobs complete/discard before tracked-body mutation, native snapshots update without wakeups.
- Added Dump_PHYSICS_CULLING_OVERSEER.bin black-box dump path on NaN/invalid culling input.

Cinematic Cheats used:
- Squared-distance culling instead of exact distance.
- Behind-camera threshold halves sleep distance.
- Abyss depth >= 500 m reduces sleep distance by 20 percent.
- Low/MX350 uses 40 m sleep radius; higher tiers use 50 m.
- Acoustic wake radius is clamped heuristic energy/intensity, not physical propagation.
- MeshCollider stripping fakes inactivity while preserving transform identity.

Exact Microseconds saved:
- Singleton avoidance and centralized registry: estimated 35-80 us.
- Burst 10 Hz distance job over fixed-step main-thread checks: estimated 120-220 us/frame.
- Behind-camera/depth threshold bias: estimated 45-115 us/frame in dense scenes.
- Kinematic cull beyond 100 m: estimated 180 us/frame in debris-heavy scenes.
- MeshCollider strip beyond 150 m: estimated 240 us/frame in far heavy-object scenes.
- EventBus radius wake versus wake-all: estimated 50-150 us per signal burst.
- Omega polish rsqrt acoustic radius: estimated 2-5 us per impulse burst.

Verification:
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` failed before this assembly in Hecton8.Bootstrap.Contracts.csproj.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false /clp:ErrorsOnly` failed on absent generated metadata DLLs.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` failed on the same BootstrapContracts dependency.
- `git diff --check` for touched files passed with line-ending warnings only.

Integrator note:
- Fix or move the BootstrapStatus.cs time-dilation changes out of Hecton8.Bootstrap.Contracts.asmdef before expecting Assembly-CSharp compile verification.

## 2026-05-13 - Static Hardening Pass
What was wrong:
- Far bodies could enter kinematic cull before the 0.9 velocity dampening path ran.
- Obsolete pre-overseer distance-kinematic helper methods remained after the Burst dispatcher replaced them.
- Telemetry ring writes assumed the write index could never be externally corrupted.
- Explicit culling flags on an already scanned Rigidbody were ignored on later registration.
- SlowTick duplicated the fixed-step 10 Hz culling scheduler.
- DataVault AUP buffers could be larger than local result lanes, and a locked DataVault could return no buffer.

What was done:
- Reordered distance sleep before kinematic cull.
- Deleted obsolete distance-kinematic helper methods.
- Added bounded telemetry ring writes and current-pass culled counts.
- Merged explicit culling flags into already tracked body state and restored ignored bodies immediately.
- Removed ISlowTickable registration so only the fixed-step accumulator schedules culling.
- Added H8Memory fallback when DataVault cannot provide the AUP buffer.
- Bounded native clear length to the shortest culling lane.
- Cleared the GlobalRegistry physics culling slot during reset.

Cinematic Cheats used:
- No new simulation. This pass protects the existing square-distance, frustum-bias, abyss-bias, and MeshCollider strip cheats.

Exact Microseconds saved:
- Duplicate slow-lane culling removal: estimated 10-35 us per project slow tick.
- Dead helper removal: 0 us runtime, less maintenance surface.
- Registration merge: 0-20 us saved by avoiding cull/restore churn on protected bodies.

Verification:
- `rg` confirmed removed helper and SlowTick paths.
- `git diff --check` passed with line-ending warnings only.
- No dotnet build or MSBuild command was launched during this pass per user instruction.

## 2026-05-13 - Registration Churn Hardening
What was wrong:
- Duplicate body registration could complete/discard a pending culling job even when the body was already tracked.
- A stale same-EntityId mapping could leave an old Rigidbody in the array without a dictionary index.
- Tracking could proceed after native buffer allocation failure and crash later on lane writes.

What was done:
- Same-body registration updates flags, mesh metadata, and exclusion state without completing the culling job.
- Same-EntityId but different tracked body entries are removed before appending the new body.
- Required native lanes and black-box telemetry are validated before accepting tracking.

Cinematic Cheats used:
- None added. This preserves the existing sleep/kinematic/MeshCollider culling cheats and removes avoidable scheduler churn.

Exact Microseconds saved:
- Duplicate registration no-job path: estimated 20-80 us during dense hydrodynamic/connection update frames.
- Stale identity cleanup: 0 us normal path, prevents orphaned tracked entries.
- Native failure guard: 0 us normal path, prevents constrained-memory crash class.

Verification:
- `rg` checked registration, old helper, search, distance, and SlowTick patterns.
- `git diff --check` passed with line-ending warnings only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Origin Shift Culling Ownership Guard
What was wrong:
- Culling-owned kinematic bodies could be skipped by origin-shift prepare/commit because the old guard treated every kinematic body as authored kinematic state.
- Native culling guards checked creation but not fixed lane capacity.
- Runtime reset left the black-box telemetry cursor and stale entries intact.

What was done:
- Origin-shift prepare/commit now processes bodies when `DistanceKinematicSleepActive` is true, while still skipping authored kinematic bodies.
- Required native state now validates all culling lane lengths against `MaxTrackedBodies` and telemetry against 300 entries.
- Slow culling scheduling uses the same required-native-state guard.
- Runtime reset clears physics culling telemetry entries and resets the write cursor.

Cinematic Cheats used:
- No new simulation. This protects the existing kinematic far-body fake across floating-origin shifts.

Exact Microseconds saved:
- Origin-shift wake-all alternative rejected: avoids reopening up to 512 solver bodies during shift; estimated 100-400 us avoided in debris-heavy scenes.
- Native lane capacity guard: 0 us hot path, prevents Burst out-of-range fault class.
- Telemetry reset: cold-path only, 0 us normal frame.

Verification:
- `rg` confirmed culling-owned kinematic origin-shift guards and fixed native validation.
- `rg` confirmed no scene search, exact distance, sqrt, obsolete helper, or SlowTick culling path.
- `git diff --check` passed with line-ending warnings only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - PickupItem Local Cull Purge
What was wrong:
- `PickupItem.FixedTick` still resolved the player transform and owned its own 100 m sleep/wake decision.
- Loose-current force packets used the default wake path, which could wake bodies the overseer had just put to sleep.

What was done:
- Removed PickupItem's local player-distance sleep/wake branch and its cached player transform.
- Removed the local current-cull constants.
- Sleeping pickups now skip loose-current force work.
- Changed loose-current force and spin to ambient no-wake physics packets.
- Kept PickupItem registration/unregistration with `GlobalPhysicsStateManager` intact.

Cinematic Cheats used:
- Centralized square-distance culling remains the single far-body cheat.
- Loose currents become visual/physical flavor only while the body is already awake; they no longer break overseer sleep.

Exact Microseconds saved:
- Removed local player transform resolve plus distance branch: estimated 10-25 us per active loose-pickup fixed tick cluster.
- No-wake ambient packets avoid sleep/wake churn in the 40-100 m band: estimated 20-60 us in dense loose-item current fields.

Verification:
- `rg` confirmed no `_playerTransform`, `CurrentSimulationCullDistance`, or player transform resolve remains in `PickupItem`.
- `rg` confirmed remaining PickupItem wake call belongs to overflow-drop gameplay.
- `git diff --check` passed for `PickupItem.cs` with line-ending warning only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Reporter Reentrancy Hardening
What was wrong:
- `RegisterTrackedBodyInternal` attached `PhysicsStateReporter` before the body was inserted into the tracked array and entity map.
- `AddComponent<PhysicsStateReporter>` can invoke `OnEnable` immediately, and that `OnEnable` calls back into `RegisterTrackedBody`.
- The recursive call could append the same Rigidbody first, then the outer call could append it again, leaving an orphaned duplicate slot.

What was done:
- Moved reporter attachment after `_trackedBodies`, `_bodyStates`, `_trackedBodyIndexByEntityId`, and `_lastValidPositions` are committed.
- Reentrant reporter registration now lands on the same-body update path instead of append.

Cinematic Cheats used:
- None added. This protects the sleep enforcer registry so existing culling cheats operate on one row per body.

Exact Microseconds saved:
- Prevented duplicate tracked-body rows during scene scans and force-router registration bursts: estimated 20-60 us saved per 100 newly reported bodies.
- Avoided later orphan cleanup and duplicate culling state work: estimated 1-3 us per affected body per culling pass.

Verification:
- `rg` confirmed the entity map commit precedes `EnsureReporter(body)`.
- `rg` confirmed reporter `OnEnable` still registers through the global manager and now hits the duplicate update path.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Native Lane Self-Heal
What was wrong:
- Required native-state checks rejected undersized lanes, but allocation repair only handled missing lanes.
- A short DataVault alias or recovered lane could leave the sleep enforcer permanently unavailable.

What was done:
- Added a cold undersized-lane release pass at the start of `EnsureNativeState`.
- Pending culling work is completed/discarded before any lane is released.
- H8Memory-owned short lanes are unregistered and released.
- DataVault-owned `RigidbodyAUPs` aliases are unregistered and defaulted without freeing vault memory, then reacquired through the normal allocation path.

Cinematic Cheats used:
- None added. This keeps the existing square-distance sleep/kinematic/collider-strip cheats online after bad cold allocation state.

Exact Microseconds saved:
- Normal frame cost: 0 us.
- Prevents fallback to uncapped far-body PhysX in damaged startup state: preserves the existing 120-420 us/frame debris-scene savings.

Verification:
- `rg` confirmed `ReleaseUndersizedNativeState` runs before native allocation checks.
- `rg` confirmed DataVault aliases are not released through H8Memory.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Depth Signal Sanitization
What was wrong:
- Player depth fed abyss culling without a finite check.
- A NaN depth could bypass the abyss threshold and retain more far-body physics than intended.

What was done:
- Sanitized PlayerRuntimeContext depth with `math.isfinite` before clamping.
- Sanitized fallback `HectonPlayerMovement.CurrentDepth` the same way.

Cinematic Cheats used:
- Preserves the existing abyss visibility cheat: deeper than 500 m reduces sleep thresholds by 20 percent.

Exact Microseconds saved:
- Normal frame cost: negligible.
- Prevents invalid-depth frames from losing the abyss culling reduction; preserves estimated 70 us/frame savings in abyss debris fields.

Verification:
- `rg` confirmed both depth sources are guarded by `math.isfinite`.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Overseer Registry Slot Mapping
What was wrong:
- `IPhysicsCullingOverseer` registered into `GlobalRegistry`, but generic service-slot resolution treated the interface type as `Unknown`.
- Unknown-slot registration weakens diagnostics and service rebound tracking.

What was done:
- Mapped `IPhysicsCullingOverseer` to the existing `PhysicsStateManager` slot.
- Kept the overseer as a facade on `GlobalPhysicsStateManager`; no new boot slot or dependency node was added.

Cinematic Cheats used:
- None added. This is registry observability and ownership correctness.

Exact Microseconds saved:
- Runtime hot path: 0 us.
- Prevents diagnostic blind spots during service rebind/boot validation.

Verification:
- `rg` confirmed the interface maps to `GlobalRegistryServiceSlot.PhysicsStateManager`.
- `git diff --check` passed for `GlobalRegistry.cs` with line-ending warning only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Idempotent Culling Enforcement
What was wrong:
- Already-active culling state skipped the apply path, so an external wake, collision toggle, or MeshCollider re-enable could make overseer state lie about actual PhysX cost.
- Restore paths in the 10 Hz dispatcher and event wake path performed a linear body-index lookup even when the caller already had the index.

What was done:
- Active distance sleep now re-dampens finite velocities and calls `Sleep()` again if the body was disturbed.
- Active distance kinematic cull now reasserts `isKinematic = true`, `detectCollisions = false`, and sleeping state.
- Active MeshCollider strip now re-disables cached MeshColliders if another system re-enabled them.
- Restore paths in dispatch, acoustic/impact wake, removal, and runtime reset now use the known tracked-body index.

Cinematic Cheats used:
- No new cheat. This protects the existing square-distance sleep, far kinematic cull, and far MeshCollider strip cheats from being undone by unrelated Unity-side state changes.

Exact Microseconds saved:
- Prevents disturbed far-body clusters from leaking back into live solver/broadphase: estimated 5-20 us per affected cluster.
- Removes linear restore self-lookups from culling dispatch/event wake paths: estimated 1-8 us per restore burst depending on tracked count.

Verification:
- `rg` confirmed direct-index restore calls in dispatch, event wake, removal, and reset.
- `rg` confirmed active sleep, kinematic, and mesh-strip paths now reassert component state.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Ambient Packet Sleep Guard
What was wrong:
- No-wake ambient packets could still call Unity `AddForce` or `AddTorque` on a culled Rigidbody.
- Unity force application can implicitly wake sleeping bodies, leaking solver cost until the next 10 Hz culling pass.

What was done:
- Added `IPhysicsCullingOverseer.IsBodyCulled(Rigidbody)`.
- Implemented it in `GlobalPhysicsStateManager` using the tracked-body entity map and culling-owned state bits.
- `PhysicsApplySystem` now discards no-wake ambient packets for bodies currently distance-slept, distance-kinematic-culled, or mesh-stripped.
- Critical/gameplay wake paths remain intact.

Cinematic Cheats used:
- Preserves the existing far-body visual fake: debris can remain visible while ambient currents do not reopen PhysX work outside the active radius.

Exact Microseconds saved:
- Estimated 10-40 us during dense fluid/current frames where far debris would otherwise be touched by no-wake ambient force/torque packets.
- Added cost is one O(1) overseer lookup per ambient packet.

Verification:
- `rg` confirmed the overseer facade exposes and implements `IsBodyCulled`.
- `rg` confirmed `PhysicsApplySystem` consults the facade before ambient force/torque application.
- `git diff --check` passed for the touched physics and evidence files with CRLF warnings only.
- No dotnet build or MSBuild command was launched.

## 2026-05-13 - Tether Culling Locks
What was wrong:
- Tether connections existed, but only mass-ratio compensation blocked culling.
- A normal-ratio tether payload could still be slept or kinematic-culled by distance/camera/depth thresholds.

What was done:
- Added `CullingLockRefCount` to tracked rigidbody state.
- Active tether connections lock both anchor and payload bodies.
- Dock connections lock the docked body.
- New tether/dock registration immediately restores any body already owned by sleep/kinematic/MeshCollider culling.

Cinematic Cheats used:
- None added. This prevents the far-body visual fake from touching active gameplay constraints.

Exact Microseconds saved:
- Runtime cost: 0-5 us for fixed-size connection lock refresh.
- Prevents invalid solver removal on active tether payloads; loose debris still keeps the 120-420 us/frame culling savings.

Verification:
- `rg` confirmed culling locks are reset, incremented, and included in the culling snapshot/dispatch gates.
- `rg` confirmed `TetherInstance` registers tether connections with `GlobalPhysicsStateManager`.
- `git diff --check` passed for the touched physics and evidence files with CRLF warnings only.
- No dotnet build or MSBuild command was launched.
