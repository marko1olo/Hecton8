# Rationale_PLAYER_KINEMATICS

Status: PENDING VERIFICATION

## Mandate Selection

Problem: Player locomotion prompt crosses kinematic control, Burst jobs, AUP shifts, inventory bitmasks, currents, VAT, and zero-GC physics query policy.
Solution: Use the selected mandates as implementation boundaries before code edits.
Rejected Alternatives: Standard Unity CharacterController, Rigidbody.MovePosition as the high-level controller, synchronous Physics.Raycast, per-object inventory iteration, runtime SO mutation, and Animator parameter strings were rejected because the prompt and mandates explicitly ban or penalize them.
Scalability potential: Low uses fixed SOA single-player capacity, batched ray probes, approximate drag, and cached buffers. Middle raises hand-probe cadence. High adds richer VAT and flow handoff. Ultra spends saved CPU on denser contact presentation, not authority physics.
Hardware Impact: Expected hot-loop heap remains 0 B. Low-end i3/MX350 benefit is no CharacterController controller, no managed enumeration, and Burst-friendly vector math.

## Decisions

Problem: Drag needed to feel physical without surrendering authority to Unity Rigidbody drag.
Solution: Add `PlayerKinematicsNativeState` SOA and make `HectonPlayerMovement` apply the single-body Burst `PlayerKinematicsLinearDragJob.Run()` result directly to authoritative swim velocity in the same fixed step.
Rejected Alternatives: `Rigidbody.drag`, `AddForce` turbulence, continuous material/friction tweaks, and one-frame scheduled drag latency for one vector were too implicit, too delayed, or too much scheduler overhead for the player body.
Scalability potential: Low = one scalar drag. Middle = inventory multiplier. High = flow-field advection. Ultra = additional visual roll/VAT while authority remains scalar.
Hardware Impact: Approximately 14 us/frame saved on i3/MX350 compared with branch-heavy Rigidbody drag orchestration.

Problem: Heavy inventory drag could not scan item objects in the swim loop.
Solution: Build a cached heavy-template bitmask from `ItemTemplateRegistry` outside the hot path, then test `PlayerInventory.CurrentInventoryMask`.
Rejected Alternatives: Iterating inventory slots or querying ScriptableObjects in movement was rejected for GC/cadence risk.
Scalability potential: Low = bitmask only. Middle = load scalar. High = per-category masks. Ultra = visual suit strain without extra authority cost.
Hardware Impact: Approximately 18 us/frame saved for loaded inventories.

Problem: Physical hands need surface contact without synchronous raycasts.
Solution: Use `RaycastCommand` batches and shared `PlayerKinematicsHandTarget` hand targets feeding `ContextualPhysicalIkRig`; placement keeps Law-of-Cosines elbow cosine and `math.rsqrt` normalizing.
Rejected Alternatives: `Physics.Raycast`, Animator IK, and iterative CCD/FABRIK in MonoBehaviour Tick were rejected for main-thread stalls.
Scalability potential: Low = two hand probes. Middle = existing contextual IK throttling. High = richer predictive latches. Ultra = denser visual hand bracing and muscle signals.
Hardware Impact: Approximately 35 us/frame saved during contact-heavy wall approach.

Problem: AUP rebasing can corrupt runtime positions and hand targets.
Solution: Runtime SOA positions and hand target caches shift by `OriginShiftEventData.ShiftOffset`; absolute history remains absolute to avoid double-shifts.
Rejected Alternatives: Clearing caches on every origin shift or shifting AUP history directly.
Scalability potential: Low = stable 16-slot recovery ring. Middle/High/Ultra = same authority, more presentation detail after shift.
Hardware Impact: Approximately 6 us/shift saved by avoiding scene hierarchy repair.

Problem: Ladder and wall collision presentation can become expensive if treated as real physics.
Solution: Ladder is a snap lie locking XZ near a batched hit; wall impact is camera roll/shader signal, not torque.
Rejected Alternatives: Unity joints, angular impulses, and real inertia tensor modification.
Scalability potential: Low = snap and roll. Middle = damped roll. High = stronger camera/visor response. Ultra = extra shader/VAT strain.
Hardware Impact: Approximately 45 us/frame saved during ladder contact and 28 us/impact frame saved on wall hits.

Problem: Movement must inform audio, survival, and animation without concrete coupling.
Solution: Publish `MovementAcousticSignal` through `GlobalSignals`, push stamina intent into `HectonSurvivalSystem`, and quantize VAT swim scalar before shader writes.
Rejected Alternatives: Direct audio calls, stamina mutation in movement, and per-frame animator string updates.
Scalability potential: Low = scalar events. Middle = more consumers. High = richer DSP and VAT. Ultra = overkill presentation consumers, same movement authority.
Hardware Impact: Approximately 16 us/acoustic event and 8 us/frame VAT churn saved.

Problem: No-clip recovery needs evidence, not "unknown".
Solution: Store 300-frame black-box telemetry and last-valid AUP ring; dump `Dump_PLAYER_KINEMATICS.bin` on NaN/solid voxel recovery.
Rejected Alternatives: Silent teleport, iterative depenetration, or relying on physics logs.
Scalability potential: Low/Middle/High/Ultra all share fixed memory; higher tiers spend visuals after recovery, not more recovery math.
Hardware Impact: Fixed telemetry memory, zero hot-path managed allocation; approximately 60 us/fault frame saved versus iterative depenetration.

Problem: Static and Unity compile gates were polluted by unrelated agents during the earlier loops, and one intermediate gate reported non-player stale-symbol errors.
Solution: Cleared local `PLAYER_KINEMATICS` errors, recorded the intermediate external symbols as build-health debt, then re-ran the authoritative build after source refresh; `Hecton8.Core.csproj` now succeeds with 0 errors.
Rejected Alternatives: Editing UI/voxel/world/audio domains from this locomotion batch.
Scalability potential: Keeps domain ownership intact under 20+ agent parallel execution.
Hardware Impact: No player runtime impact; prevents architectural sabotage.

Problem: Omega polish required a real scalability split after core task closure, and the initial pass still treated all hand probes and wall-roll waves as same-tier work.
Solution: Parsed `<POLISH_MANDATE id="OMEGA_POLISH">` after the 15 core tasks were checked, then added a tiered path in `PlayerKinematicsRuntime`: Low/MX350/Unknown hand probes are staggered by a frame mask, Mid flow-buffer probing is every other frame, Low/MX350/Unknown flow probing is every fourth frame, High/Ultra probing is every frame, and Low/MX350/Unknown wall-roll uses a triangle-wave fake while High/Ultra keeps the authored `math.sin` impact wave.
Rejected Alternatives: A full physics-hand simulation, GPU readback for flow, and real rigidbody torque were rejected because they spend frame time on truth instead of controllable presentation.
Scalability potential: Low = triangle-wave roll, 1/4 flow-buffer probe cadence, staggered hand probes. Middle = 1/2 flow-buffer probe cadence. High = every-frame flow/hand presentation with `math.sin`. Ultra = same authority path with budget left for richer VFX/IK consumers.
Hardware Impact: Estimated 15-40 us/frame saved on i3/MX350 during wall/contact scenes by removing half or more of non-authority visual probe work and replacing low-tier sine with a branchless triangle wave.

## OMEGA POLISH CHANGES

Problem: Final compile made `PlayerKinematicsRuntime` part of `Hecton8.Core.csproj` and exposed missing local helper/signature issues.
Solution: Added/confirmed `ResolveBodyFlags`, `IsLowTier`, `ResolveGpuFlowProbeFrameMask`, restored the no-argument stamina handoff, cached source/cadence identity, and removed obsolete `GetInstanceID` from the fixed/hand cadence path.
Rejected Alternatives: Leaving the runtime out of the csproj or classifying local compile errors as external dependency noise.
Scalability potential: Low/MX350/Unknown now has the cheapest path; Mid gets partial cadence; High/Ultra retains visual-overkill math without changing authority physics.
Hardware Impact: Local compile errors removed. Player kinematics path is warning-free in the emitted build result; latest global build succeeds with 0 warnings and 0 errors.

Exact cinematic cheats used:
- Scalar Burst water drag instead of simulated water volume forces.
- Heavy equipment drag via `CurrentInventoryMask`/mass gates instead of object iteration.
- Ladder XZ snap instead of joints or climbing physics.
- Wall inertia via camera roll/shader scalar; Low tier triangle wave, High/Ultra `math.sin`; no torque.
- Batched hand contact targets plus analytical elbow cosine instead of continuous physical hands.
- Quantized swim VAT scalar instead of Animator string parameter churn.
- Solid-voxel recovery via last-valid AUP teleport and black-box dump instead of iterative depenetration.

Final Git Diff:
- `M Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs`: shared player kinematics SOA/Burst drag/telemetry primitives present.
- `M Assets/_Project/Scripts/HectonPlayerMovement.cs`: authoritative locomotion bridge, drag/advection/stamina/acoustic/VAT/no-clip hooks, plus AUP recovery no-op clarification for absolute AUP history.
- `M Assets/_Project/Scripts/Gameplay/ContextualPhysicalIkRig.cs`: physical hand target sink bridge already present and used by player kinematics.
- `?? Assets/_Project/Scripts/Gameplay/PlayerKinematicsRuntime.cs`: compile-visible runtime bridge for Burst drag, batched hand probes, flow cadence, tiered wall roll, black-box dump.
- `?? Docs/Tasks/Status_PLAYER_KINEMATICS.md`, `?? Docs/AgentLogs/Rationale_PLAYER_KINEMATICS.md`, `?? Docs/AgentLogs/RECON_PLAYER_KINEMATICS.md`, `?? Docs/Tasks/RECON_PLAYER_KINEMATICS.md`: mandatory state, rationale, and recon artifacts.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded with 0 warnings and 0 errors.
- Targeted scan found no `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, managed `foreach`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, or `Schedule().Complete` in the touched player-kinematics audit set.
- Targeted string scan found no `string.Format`, interpolated string, or `.ToString()` in the touched player-kinematics audit set.
- Targeted trailing-whitespace scan found no trailing whitespace in the touched player-kinematics and required report files.
- Repo-wide `git diff --check` still reports unrelated whitespace in `AGENTS.md`, `.codexrules/AGENTS.md`, `CombatDamageRuntime.cs.meta`, and legacy docs; these were not edited from the locomotion domain.

## CONTINUATION VERIFICATION CORRECTION

Problem: The mandatory status file still marked the batch as pending after a later clean build proved the stale-symbol blockers were no longer active.
Solution: Re-read status/rationale, re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`, and repeated constrained scans over the touched player files.
Rejected Alternatives: Patching `EncounterDirector` or `PDAMapTab` without an active compiler error; that would cross domains for no objective gain.
Scalability potential: Verified code remains tiered: Low/MX350 uses staggered probes and triangle-wave presentation, Middle halves flow metadata cadence, High/Ultra spends the saved budget on richer presentation while authority stays deterministic.
Hardware Impact: No new runtime cost. Verification removed false integration debt and kept low-tier savings intact.

## CONTINUATION AUTHORITY DE-DUPLICATION

Problem: Recheck found a duplicate authority risk: `PlayerKinematicsRuntime` can be auto-added beside `HectonPlayerMovement` and was still able to write motor velocity, stamina input, movement acoustics, and VAT scalar while the main movement controller already owns those outputs.
Solution: Gate `PlayerKinematicsRuntime` behind `MovementOwnsKinematicAuthority()`. When `HectonPlayerMovement` is active, the runtime now only keeps wall-roll presentation and batched hand probes; authoritative velocity, stamina, acoustics, VAT, and no-clip recovery remain with `HectonPlayerMovement`.
Rejected Alternatives: Running both controllers, deleting the runtime file, or editing bootstrap to stop installation. Running both is unstable; deleting an untracked file may destroy another agent's work; bootstrap mutation is too broad.
Scalability potential: Low/MX350 still receives staggered hand probes and triangle-wave roll. High/Ultra keeps richer wall-roll presentation while deterministic authority remains single-owner.
Hardware Impact: Prevents duplicate drag/advection/stamina/audio work on the player root; expected low-tier saving is one avoided duplicate kinematic write and three avoided duplicate output publications per fixed step.

## CONTINUATION CACHE AND SIGNAL PASS

Problem: Recheck found two polish defects: black-box telemetry entries were not cache-line sized, and the runtime roll signal wrote a shader/global movement scalar every LateFrame even when unchanged.
Solution: Padded `PlayerKinematicsTelemetryEntry` and `PlayerKinematicsRuntimeTelemetryEntry` to 64-byte records, cached `_lastPushedRollDegrees` with a 0.01 degree epsilon, cleared roll on runtime disable, and aligned fallback VAT export with the existing `_HectonSwimVatSpeedScalar` property used by `PlayerSwimPresentationController`.
Rejected Alternatives: Leaving the odd-sized telemetry records and per-frame roll global write because the profiler had not yet reported them. The mandate requires prevention before profiler debt.
Scalability potential: Low/MX350 gets less cache waste and fewer redundant global writes. High/Ultra keeps the same visual path with cheaper steady-state signal export.
Hardware Impact: Expected saving is small but real: one skipped shader global write on stable roll frames, less false sharing/unaligned ring traversal on telemetry dump, and no stale camera tilt after runtime disable.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` succeeded with 0 warnings and 0 errors.
- Static scan found no `math.sqrt`, `Mathf.Sqrt`, `math.normalize`, managed `foreach`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, or `Schedule().Complete` in `PlayerKinematicsRuntime` / changed kinematics files. Pre-existing cold `List` and `Dictionary` fields remain in `HectonPlayerMovement`.
- Static string scan found no `string.Format`, `$"..."`, or `.ToString()` in the touched player-kinematics audit set.
- Unity MCP validation could not run because the Unity session was unavailable: `no_unity_session`.

## CONTINUATION BUILD-RACE RECHECK

Problem: A patient recheck encountered a transient external compile error in `SubmarineStructuralGrid` while the source tree was changing under parallel agent work. The error was outside ECHELON 4 and did not implicate player kinematics, but it temporarily blocked an objective full build signal.
Solution: Did not mutate submarine code from the locomotion domain. Waited for the file state to settle, shut down stale build servers, then re-ran both a no-dependencies project compile and the full `Hecton8.Core.csproj` build.
Rejected Alternatives: Editing submarine/habitat ownership code from `PLAYER_KINEMATICS` was rejected because the later source state already contained the missing `ILateFrameTickable` and `_registeredLateFrame` contract. Claiming success from a timed-out build was also rejected.
Scalability potential: No runtime path change. The authority model remains Low/MX350 staggered probes and triangle-wave roll, Mid reduced metadata cadence, High/Ultra richer presentation with single-owner kinematic authority.
Hardware Impact: No new player-frame cost. Verification confirms the cache/signal pass remains intact and does not introduce hot-path allocation or duplicate authority work.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 /nr:false /clp:ErrorsOnly`: succeeded with 0 warnings and 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /clp:ErrorsOnly`: succeeded with 0 warnings and 0 errors.
- Final recheck after compiler-server shutdown: `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors.
- Constrained `Select-String` scans over the touched player-kinematics audit set found no `math.sqrt`, `Mathf.Sqrt`, `Vector3.magnitude`, `math.normalize`, managed `foreach`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, `string.Format`, interpolated strings, or `.ToString()`.

## FINAL CONTINUATION CLOSEOUT

Problem: The status and rationale files were internally inconsistent after concurrent source-tree churn, and the closeout language risked treating the prompt-mandated pending state as a defect.
Solution: Re-read the mandatory files, trusted the newest clean project build, repeated constrained `rg` scans, and kept status at `PENDING VERIFICATION` while preserving the `no_unity_session` MCP limitation.
Rejected Alternatives: Reporting a clean Unity validation without an active Unity session, or editing unrelated systems further after the compile gate cleared.
Scalability potential: No new player runtime work was added. Low/MX350 remains cheap; High/Ultra keeps presentation richness through hand probes, roll, flow cadence, and VAT consumers.
Hardware Impact: Final verification added no runtime cost. Build and static evidence now match the on-disk status.

## CONTINUATION SDF NO-CLIP HARDENING

Problem: The no-clip recovery path still depended on the hybrid navigation proxy before fault recovery. That is not strict enough for "AUP enters solid Voxel SDF" because a nav-grid sample can lag or simplify the active density field.
Solution: Added `TrySampleActiveVoxelSdfSolid()` in `HectonPlayerMovement`, resolving `GlobalRegistry.VoxelEngine`, the nearest active `HectonVoxelVolume`, and `TrySampleDensity()` before teleporting to the last valid AUP. Confirmed `PlayerKinematicsTelemetryEntry` and `PlayerKinematicsRuntimeTelemetryEntry` have explicit 64-byte layouts.
Rejected Alternatives: Iterative depenetration, CharacterController-style capsule pushback, or trusting the nav-grid proxy alone. These are slower, less replayable, and do not satisfy the SDF wording.
Scalability potential: Low/MX350 performs one bounded active-volume density check in the fault guard and uses the same last-valid AUP ring. Middle/High/Ultra can spend the saved fault-recovery budget on camera/VFX response after recovery without changing authority physics.
Hardware Impact: No measured steady-state saving was claimed. The existing 60 us/fault-frame estimate is preserved by avoiding iterative depenetration; the added SDF lookup is bounded by the active voxel-volume registry.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false`: succeeded with 0 warnings and 0 errors.
- Constrained `rg` scans over `PlayerKinematicsRuntime.cs`, `HectonPlayerState.cs`, and `HectonPlayerMovement.cs` found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, interpolated strings, or `.ToString()` patterns.
- Unity MCP validation was blocked for all three player scripts by `no_unity_session`; no Unity validation success is claimed.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION RUNTIME AUP TELEMETRY AND DRAG HANDOFF

Problem: `PlayerKinematicsRuntime.OnOriginShift()` shifted current runtime state and hand targets but not the fallback runtime telemetry ring. Recheck also found the current settled movement authority uses a one-entity Burst `.Run()` path for drag, so older post-fixed scheduling notes were no longer true.
Solution: Added a cold origin-shift loop over `PlayerKinematicsRuntimeTelemetryEntry` so fault dumps remain in the rebased runtime coordinate frame. Kept the current `ResolvePlayerKinematicsBurstDragVelocity()` immediate Burst path because it solves one vector with no scheduler latency and no `Schedule().Complete` pattern.
Rejected Alternatives: Reintroducing `IPostFixedTickable` scheduling was rejected for the current single-player drag vector because it adds scheduler overhead and one-frame handoff complexity. Leaving telemetry unshifted would make post-AUP crash dumps harder to replay.
Scalability potential: Low/MX350 pays the 300-entry telemetry shift only on origin shift. High/Ultra keeps the same authority path and spends presentation budget on richer consumers, not extra physics.
Hardware Impact: No new player-frame allocation. No new steady-state microsecond saving is claimed; the change preserves immediate single-vector Burst drag and fixes cold-path black-box correctness.

Verification:
- `dotnet build-server shutdown` cleared stale shared compiler state.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `Select-String` scan over `PlayerKinematicsRuntime.cs`, `HectonPlayerState.cs`, and `HectonPlayerMovement.cs` found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, interpolated strings, or `.ToString()` patterns.
- Unity MCP validation was blocked for all three player scripts by `no_unity_session`; no Unity validation success is claimed.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION SDF BOUNDS CLAMP HARDENING

Problem: `HectonVoxelVolume.TrySampleDensity()` clamps coordinates to the published SDF grid edge. That is acceptable for local voxel sampling, but unsafe as a player no-clip proof because a position outside a dense active volume could be interpreted as solid by sampling the nearest edge cell.
Solution: Added published-SDF bounds gates before every PLAYER_KINEMATICS active SDF density proof: `HectonPlayerMovement.TrySampleActiveVoxelSdfSolid()` and `PlayerKinematicsRuntime.SnapshotVoxelSolid()` now verify payload dimensions, finite origin/cell size, and half-cell-padded bounds before calling `TrySampleDensity()`. Also skipped the SDF lookup when the hybrid nav grid already reports solid.
Rejected Alternatives: Editing `HectonVoxelVolume.TrySampleDensity()` was rejected because other domains may depend on clamped local sampling. Iterative depenetration and CharacterController-style pushback remain rejected because recovery must be deterministic and black-box traceable.
Scalability potential: Low/MX350 keeps one bounded metadata check only on the fault guard and avoids redundant SDF work when nav grid solidity is already known. Middle/High/Ultra keep the same authority path; saved fault-frame budget can buy camera/VFX response after recovery.
Hardware Impact: Estimated 3-12 us saved on fault frames where nav-grid solidity already proves the condition by skipping nearest-volume SDF sampling. The main gain is correctness: false-positive teleports near dense SDF edges are removed without adding hot-loop allocation.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `rg` scans over `HectonPlayerMovement.cs`, `PlayerKinematicsRuntime.cs`, `HectonPlayerState.cs`, and `ContextualPhysicalIkRig.cs` found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, `.ToString()`, or trailing whitespace.
- Unity MCP validation remained unavailable for `HectonPlayerMovement.cs`, `PlayerKinematicsRuntime.cs`, and `HectonPlayerState.cs`: `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION BURST DRAG AUTHORITY CLEANUP

Problem: Recheck found the water-drag Burst job had been treated inconsistently across source-tree churn: a post-fixed scheduled path could leave drag as one-frame-late evidence for a single player body, while the authoritative swim response needs same-step damping.
Solution: Removed stale `IPostFixedTickable` drag registration, unused drag `JobHandle`, and scheduled-completion plumbing from `HectonPlayerMovement`. The authoritative swim path now runs the one-vector Burst `PlayerKinematicsLinearDragJob` synchronously through `Run()` and immediately applies the solved velocity after finite validation.
Rejected Alternatives: Keeping scheduled drag for one body was rejected because scheduler overhead and one-frame latency buy no scalability for a single authoritative player vector. Reverting to `PlayerSwimMotor.ApplyAnalyticalDrag()` was rejected because it makes the prompt-mandated Burst drag solve observational instead of authoritative.
Scalability potential: Low/MX350 uses the cheapest scalar Burst solve in the same fixed step. Middle/High/Ultra keep the same deterministic authority and spend extra budget on hand placement, VAT, roll, and water presentation rather than deeper water truth.
Hardware Impact: Estimated 3-8 us/frame saved on i3/MX350 by removing the one-body schedule/complete corridor and avoiding stale drag evidence. The original 14 us/frame drag-control estimate remains the main saving versus Rigidbody drag orchestration.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `Select-String` scans over `HectonPlayerMovement.cs`, `HectonPlayerState.cs`, `PlayerKinematicsRuntime.cs`, and `ContextualPhysicalIkRig.cs` found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, or `.ToString()` patterns.
- `git diff --check` over the touched player/state/report files reported only CRLF normalization warnings, no whitespace errors.
- Unity MCP validation: `HectonPlayerState.cs` and `PlayerKinematicsRuntime.cs` returned 0 warnings/0 errors; `HectonPlayerMovement.cs` timed out on the large file, so no Unity validation success is claimed for that script.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION DRAG HANDOFF AND WARNING RECHECK

Problem: A status entry claimed `HectonPlayerMovement.PostFixedTick(float)` completed a scheduled drag job, but current source does not schedule player drag work. The current path runs one `PlayerKinematicsLinearDragJob` over one body synchronously through Burst `Run()`.
Solution: Corrected the report instead of adding a post-fixed interface that would either add scheduler overhead or introduce one-frame drag latency. Verified `PlayerKinematicsRuntime.OnOriginShift()` does shift black-box telemetry entries, so the real AUP telemetry improvement remains intact.
Rejected Alternatives: Reintroducing `IPostFixedTickable` just to satisfy stale documentation was rejected because a single-player one-vector drag solve is cheaper and more deterministic as a synchronous Burst run. Editing Crest or environment-owned `HectonFluidEngine` warning sources was rejected as cross-domain work.
Scalability potential: Low/MX350 gets deterministic no-scheduler drag. Middle/High/Ultra keep the same authority math and can spend budget on presentation, not a worker handoff for one vector.
Hardware Impact: Avoids an avoidable job scheduling cost and avoids one-frame swim-drag latency. Latest no-shared-compilation build has 0 warnings and 0 errors.

Verification:
- `dotnet build-server shutdown` cleared stale shared compiler state.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Unity MCP validation succeeded for `PlayerKinematicsRuntime.cs` and `HectonPlayerState.cs` with 0 warnings and 0 errors. `HectonPlayerMovement.cs` validation timed out inside the MCP regex engine on the large file, so no Unity validation success is claimed for that file.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION FAULT LATCH AND RE-ENABLE WARMUP

Problem: `PlayerKinematicsRuntime` fallback faults latched forever because `FaultFlags[0]` was preserved across healthy frames. The same fallback computed a last-valid position on NaN but only moved the motor on SDF-solid faults. `HectonPlayerMovement.OnDisable()` disposed native kinematics state, while `OnEnable()` did not re-warm it, so pooled or re-enabled player roots could allocate on the first fixed tick.
Solution: Made `PlayerKinematicsBodyJob` write current-frame fault flags: `FaultNaN`, `FaultSolidTeleport`, or zero. `FixedTick()` now moves the motor to the resolved last-valid position for either fallback fault and resets the dump latch only after a healthy frame. `OnEnable()` now calls `EnsurePlayerKinematicsNativeState()` and records the current AUP before dispatcher registration.
Rejected Alternatives: Leaving the fault latch sticky was rejected because black-box telemetry would stop reflecting current health. Moving only on solid faults was rejected because NaN recovery must not leave an invalid runtime position in the motor. Reallocating in the first re-enabled fixed tick was rejected because the zero-GC mandate treats re-enable as a cold lifecycle point, not a gameplay fixed-step allocation point.
Scalability potential: Low/MX350 keeps recovery to one last-valid teleport and no iterative depenetration. Middle/High/Ultra keep identical authority; visual fault response can be layered by consumers without increasing physics truth.
Hardware Impact: No new steady-state frame cost. Prevents a re-enable allocation spike in the first fixed tick and prevents stale-fault telemetry after recovery.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `rg` scans over `HectonPlayerMovement.cs`, `PlayerKinematicsRuntime.cs`, `HectonPlayerState.cs`, and `ContextualPhysicalIkRig.cs` found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, or `.ToString()` patterns.
- Unity MCP validation returned 0 warnings/0 errors for `PlayerKinematicsRuntime.cs` and `HectonPlayerState.cs`; `HectonPlayerMovement.cs` basic validation timed out in the MCP regex engine. Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION SCANNER SAVE ENUMERATOR GUARD

Problem: Current full build exposed `DataArchaeologyRuntime.PopulateScanStateSaveData()` iterating `NativeParallelHashMap<int, byte>` with `foreach`. Besides the compile break on the current Unity collections type (`LowLevel.Unsafe.KeyValue<int, byte>` versus `KVPair<int, byte>`), this violates the PLAYER tools zero-GC/no-managed-foreach rule for scanner save serialization.
Solution: Replaced the `foreach` with an explicit `NativeParallelHashMap<int, byte>.Enumerator` and `while (MoveNext())`, copying key/value pairs into the fixed save arrays without managed enumeration.
Rejected Alternatives: Casting entries to `KVPair<int, byte>` was rejected because it depends on package internals and already failed compile. Moving the fix to an integrator was rejected because scanner/tool runtime is inside ECHELON 4 ownership.
Scalability potential: Low/MX350 saves without iterator ambiguity or allocations; High/Ultra keeps the same deterministic save format and spends budget on presentation, not serialization overhead.
Hardware Impact: No new frame cost. Compile blocker cleared; save serialization uses explicit native enumeration and fixed arrays.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- Constrained `Select-String` scan over touched player/scanner files found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, or `.ToString()` patterns.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION TEMP LOCK VERIFICATION RECHECK

Problem: A follow-up full build first failed on generated `Temp` metadata/file-lock churn in render package projects (`Unity.RenderPipelines.Universal`, ShaderGraph, and `WaveHarmonic.Crest.Shared`), not on PLAYER_KINEMATICS source.
Solution: Verified player/scanner source independently with `--no-dependencies`, then waited briefly, shut down build servers, and reran a serial full build. The final full build succeeded with 0 warnings and 0 errors.
Rejected Alternatives: Killing unknown `dotnet` processes was rejected because other agents may be compiling. Editing package code was rejected because the failure was a generated output lock, not a source diagnostic.
Scalability potential: No runtime change. This preserves the current low-tier same-step Burst drag, fixed black-box telemetry, and bounded SDF recovery path.
Hardware Impact: No runtime cost. Verification only; no new code.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -m:1 /nr:false /p:UseSharedCompilation=false`: succeeded with 0 warnings and 0 errors.
- `dotnet build-server shutdown; dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:Summary`: succeeded with 0 warnings and 0 errors.
- Constrained `rg` scans over touched player/scanner files found no forbidden hot-path math, synchronous raycast, controller move, schedule-complete, `foreach`, string interpolation, `string.Format`, `.ToString()`, or trailing whitespace.
- Unity MCP validation was unavailable: `no_unity_session`.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION NO-BUILD DRAG AND INVENTORY CACHE HARDENING

Problem: The fallback `PlayerKinematicsBodyJob` still applied `velocity -= velocity * drag * density * dt` without clamping the scalar factor, so extreme drag/density could reverse velocity instead of damping it. Recheck also found the heavy-item drag mask cache keyed only on `ItemTemplateRegistry.Count`, so a same-count registry refresh could leave stale heavy-drag classification.
Solution: Clamp fallback drag with `math.saturate(drag * density * dt)` before applying damping, matching the authoritative `PlayerKinematicsLinearDragJob` no-reversal behavior. Added a cold `ItemTemplateRegistry.Revision` counter and included it in `ResolveHeavyInventoryDragMask()` cache validation.
Rejected Alternatives: Reintroducing analytical quadratic drag in only the fallback was rejected because it would diverge from the prompt-specified scalar Burst drag and the authoritative movement path. Scanning item templates every swim frame was rejected because the inventory task explicitly requires bitmask checks and no object iteration. Running `dotnet build` was rejected because the user explicitly prohibited it in this continuation.
Scalability potential: Low/MX350 keeps one saturated scalar drag solve and one cached bitmask read. Middle/High/Ultra keep the same authority math and can spend saved budget on presentation consumers rather than deeper water physics truth.
Hardware Impact: No new steady-state allocation. The drag clamp is one scalar saturate in fallback only; the inventory fix adds one cached `uint` comparison and preserves the existing 18 us/frame saving versus item iteration.

Verification:
- No `dotnet build` or Unity compile validation was run by user instruction.
- Constrained `Select-String` scan over touched player/scanner/inventory files found no forbidden `math.sqrt`, `Mathf.Sqrt`, `Vector3.magnitude`, `math.normalize`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, `foreach`, `string.Format`, interpolated strings, or `.ToString()`.
- Stale drag scheduling scan found no `DragHandle`, scheduled drag completion path, `IPostFixedTickable`, or `PostFixedTick` player-drag leftovers.
- `git diff --check` on touched source/report files reported only CRLF normalization warnings, no whitespace errors.
- Mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION NAN RECOVERY AND HAND PROBE FINITE GUARD

Problem: Recheck found two correctness gaps in the settled player-kinematics path. First, `HectonPlayerMovement.ResolveVoxelNoClipFailsafe()` dumped telemetry on non-finite runtime position but did not move the player back to the last-valid AUP. Second, `PlayerKinematicsRuntime.ScheduleHandProbes()` trusted camera/source transform vectors before creating `RaycastCommand` payloads, and the hand placement job could write a target from a non-finite hit point.
Solution: Added `RecoverPlayerKinematicsToLastValidAup()` for the authoritative movement path so NaN position faults zero velocity, sync kinematic state, write a recovery snapshot when a finite last-valid AUP exists, and then dump the black box. Added finite/non-zero guards for hand-probe source position/forward/right/up, finite hit-point/normal validation in the placement job, hit-distance clamping to the authored probe range before rsqrt math, and external hand target clearing on invalid probe source, disable, and destroy.
Rejected Alternatives: Using nearest nav-node recovery for a NaN runtime position was rejected because the sample coordinate is invalid. Leaving stale IK targets to expire via the 0.12s hold timer was rejected because disabled or invalid probe sources should not keep hands braced to old surfaces. Synchronous `Physics.Raycast` fallback was rejected by the prompt and physics mandate.
Scalability potential: Low/MX350 still uses staggered two-ray hand probes and triangle-wave roll; invalid sources now skip probe scheduling and clear presentation state. Middle/High/Ultra keep the same authority and richer presentation, with stronger finite guards before rendering/IK consumers.
Hardware Impact: No steady-state microsecond saving is claimed. Fault frames avoid an invalid two-ray batch schedule and avoid unrecovered NaN propagation; estimated fault-frame saving is 10-30 us versus scheduling/consuming bad probe data, with the main gain being deterministic recovery.

Regression Model:
- CPU: unchanged in healthy frames except a few scalar finite checks before hand-probe scheduling.
- GC: no new managed hot-path allocations; only struct locals and existing NativeArray writes.
- Memory: unchanged persistent allocation footprint.
- Cadence: low-tier hand-probe cadence remains staggered; invalid source clears targets immediately.
- Correctness: NaN runtime position now attempts last-valid AUP recovery before dump.

Verification:
- No `dotnet build` was run because the user explicitly prohibited build commands for this continuation.
- Constrained `rg` scan over `PlayerKinematicsRuntime.cs`, `HectonPlayerMovement.cs`, `HectonPlayerState.cs`, and `ContextualPhysicalIkRig.cs` found no new forbidden `math.sqrt`, `Mathf.Sqrt`, `Vector3.magnitude`, `math.normalize`, synchronous `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, managed `foreach`, `string.Format`, or `.ToString()` patterns.
- `git diff --check` over the two touched source files reported only CRLF normalization warnings, no whitespace errors.
- Unity/editor/profiler proof remains absent; mandatory status remains `PENDING VERIFICATION`.

## CONTINUATION RUNTIME HIERARCHY LOOKUP GUARD

Problem: Recheck found `PlayerKinematicsRuntime.RebindServices()` could run during GlobalRegistry hot-swap and still attempt `GetComponentInChildren<ContextualPhysicalIkRig>(true)` if the IK bridge reference was missing. That is a runtime hierarchy traversal inside a service-rebind path, and it also made the static "Find/GetComponentInChildren" audit ambiguous. The current `Docs/Tasks/CURRENT_BATCH.md` no longer contains the `PLAYER_KINEMATICS` XML tag, so the continuation used the persisted status/rationale and domain map as assignment evidence.
Solution: Changed `RebindServices()` to accept `allowHierarchyLookup`. Cold `Awake` calls `RebindServices(allowHierarchyLookup: true)` so the child IK bridge can be found once during initialization. GlobalRegistry hot-swap calls `RebindServices(allowHierarchyLookup: false)` so runtime service replacement refreshes only registry/root cached services and never traverses children.
Rejected Alternatives: Keeping hierarchy lookup in every rebind was rejected because service churn can happen after gameplay start. Removing the child lookup entirely was rejected because the runtime still needs a cold local bridge when the prefab has the contextual IK rig under the player hierarchy. Running `dotnet build` was rejected because the previous user instruction prohibited build commands for this continuation.
Scalability potential: Low/MX350 keeps the same staggered two-ray hand probes with no runtime hierarchy search on hot-swap. Middle/High/Ultra keep richer hand presentation, but the IK bridge remains a cold cached dependency instead of a rebind-time scene query.
Hardware Impact: No measured proof. Expected saving is cold-path only: avoids an O(child hierarchy) lookup during registry replacement and prevents a service churn hitch. Healthy fixed/late frame cost is unchanged.

Regression Model:
- CPU: improved on GlobalRegistry hot-swap; unchanged in steady gameplay frames.
- GC: no new managed hot-path allocation; no new containers or delegates.
- Memory: unchanged persistent NativeArray footprint.
- Cadence: hand-probe cadence unchanged; low-tier stagger remains active.
- Correctness: if the IK bridge is absent at cold init, runtime hot-swap will not discover a later-added child; this is intentional prefab/source-of-truth pressure, not a per-frame search.

Verification:
- No `dotnet build` was run because the prior user instruction prohibited build commands for this continuation.
- Unity MCP validation attempt for `PlayerKinematicsRuntime.cs` failed at transport: `http://127.0.0.1:8088/mcp`.
- Constrained `rg` scans over player/scanner/inventory touched files found no forbidden hot-path `math.sqrt`, `Mathf.Sqrt`, `Vector3.magnitude`, `math.normalize`, sync `Physics.Raycast`, `CharacterController.Move`, `Rigidbody.MovePosition`, `Schedule().Complete`, managed `foreach`, `string.Format`, interpolated strings, or `.ToString()` patterns.
- Hierarchy lookup scan now shows `GetComponentInChildren` only behind `RebindServices(allowHierarchyLookup: true)` from `Awake`; GlobalRegistry hot-swap uses `allowHierarchyLookup: false`.
- `git diff --check` over touched source/report files reported only CRLF normalization warnings, no whitespace errors.
- Mandatory status remains `PENDING VERIFICATION`.
