# Rationale_PHYSICS_CULLING_OVERSEER

STATUS: PENDING VERIFICATION

## Decision 0: Audit Trail Bootstrapping
Problem: The mandated status and rationale files did not exist for PHYSICS_CULLING_OVERSEER.
Solution: Created durable disk state before implementation, matching the state machine protocol.
Rejected Alternatives: Chat-only reporting was rejected because CTO-facing evidence lives under Docs/AgentLogs and Docs/Tasks.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process state, not runtime.
Hardware Impact: 0 us runtime. No impact on i3/MX350.

## Mandates Selected
- PHYS_Physics_Integrity_Determinism_ForceMode.txt
- PHYS_Determinism_Multithreaded_Body_Solving.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- ARCH_Project_Bootstrap_Sequence_Init_Safety.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Decision 1: Authority Consolidation
Problem: A new physics culling singleton would race the existing bootstrap-owned GlobalPhysicsStateManager.
Solution: Extended GlobalPhysicsStateManager with IPhysicsCullingOverseer and registered that interface through GlobalRegistry during the existing GameBootstrapper-owned InitializeService path.
Rejected Alternatives: A separate PhysicsOptimization.Instance clone was rejected because it would create two owners for Rigidbody sleep state and origin-shift recovery.
Scalability potential: Low uses the same authority with shorter sleep radius; Middle/High/Ultra can preserve stronger visuals while still centralizing solver suppression.
Hardware Impact: Estimated 35-80 us saved on i3/MX350 by avoiding duplicated manager scans and per-object wake decisions.

## Decision 2: 10 Hz Burst Culling Cadence
Problem: The project SlowTick lane is 0.5 s, but the task requires a 10 Hz culling pass.
Solution: Added a local 0.1 s physics slow cadence driven by the fixed dispatcher, schedules PhysicsDistanceCullingJob, and completes results before main-thread PhysX state changes.
Rejected Alternatives: Per-FixedTick Vector3.Distance was rejected because 512 bodies at 50 Hz wastes main-thread time and violates deterministic culling budget.
Scalability potential: Low/MX350 sleeps at 40 m; Middle/High/Ultra keep 50 m, behind-camera bias, and abyss depth bias while using saved CPU for presentation.
Hardware Impact: Estimated 120-220 us/frame saved on i3/MX350 versus main-thread distance checks and state mutation every fixed step.

## Decision 3: AUP Player Signal Migration
Problem: Direct scene searches for Player would allocate/race during bootstrap and break floating-origin correctness.
Solution: Resolved player PredictedAup, CameraForward, and DepthMeters from PlayerRuntimeContextService, then fell back only to the existing GlobalRegistry.Player contract.
Rejected Alternatives: FindObjectOfType<Player> and runtime Transform distance were rejected because they are scene-coupled and precision-unsafe.
Scalability potential: Low keeps cheap AUP-relative float3 snapshots; Ultra can spend the same stable AUP source on richer visual overkill.
Hardware Impact: Estimated 10-25 us saved per culling pass by avoiding scene queries and double conversion.

## Decision 4: Sleep/Kinematic/Collider LOD Split
Problem: One binary sleep mode cannot cover solver cost, broadphase cost, and visual restoration without edge thrash.
Solution: Split culling into sleep at 40/50 m, kinematic at 100 m, MeshCollider strip at 150 m, with 90 m hysteresis restore for heavy state.
Rejected Alternatives: A single 500 m kinematic cutoff was rejected as too late for cheap devices and visually useless in abyss fog.
Scalability potential: Low sees aggressive early sleep; Middle holds stable defaults; High/Ultra keep visual density while solver cost stays bounded.
Hardware Impact: Estimated 180-420 us saved on i3/MX350 in debris-heavy scenes by suppressing solver and MeshCollider broadphase work.

## Decision 5: EventBus Wake Only
Problem: Sleeping bodies still need to react to acoustic pings and impacts without direct dependencies on sonar, audio, or combat systems.
Solution: Subscribed to PhysicsEventBus acoustic ping/impulse and PhysicsEvents impact signals, waking only culled bodies inside an AUP radius.
Rejected Alternatives: Direct sonar/audio references and global wake-all sweeps were rejected as cross-domain coupling and frame spikes.
Scalability potential: Low uses tight radius clamps; Ultra can keep cinematic acoustic wake response without reopening all physics bodies.
Hardware Impact: Estimated 50-150 us saved per event burst by restoring only bodies inside the signal radius.

## Decision 6: Origin Shift and Black Box
Problem: A culling job running across an origin shift can apply stale camera-relative positions and cause false wakeups.
Solution: Complete/discard pending culling jobs before origin-shift mutation, update native position snapshots without waking, and dump the last 300 culling telemetry entries on NaN/invalid input.
Rejected Alternatives: Letting culling finish after origin shift was rejected because AUP-relative results would be semantically stale.
Scalability potential: Low/Middle stay stable under cheap CPU stalls; High/Ultra can retain more bodies without losing postmortem state.
Hardware Impact: Estimated crash-debug gain: removes "unknown" failure class; runtime overhead is fixed 300-entry NativeArray and one entry write per dispatched body.

## Decision 7: Compile Wall
Problem: Assembly-CSharp build currently fails before this patch is compiled because Hecton8.Bootstrap.Contracts.csproj cannot resolve ITickDispatcher and GlobalRegistry from BootstrapStatus.cs.
Solution: Ran three verification attempts, then classified compile verification as dependency-blocked under the fail-fast protocol.
Rejected Alternatives: Editing BootstrapContracts was rejected because it is outside this agent domain and contains unrelated uncommitted work.
Scalability potential: Runtime scalability unaffected until the external asmdef dependency is fixed.
Hardware Impact: 0 us runtime. Verification blocked by project graph, not by sleep enforcer code.

## OMEGA POLISH CHANGES
Problem: The first implementation used one math.sqrt equivalent for acoustic impulse radius and needed final anti-bloat audit against the Polish mandate.
Solution: Replaced direct math.sqrt with `energy * math.rsqrt(energy)` and confirmed the Burst culling job uses bitmask state lanes, math.distancesq, fixed NativeArray lanes, and no managed foreach/string formatting in the new hot path.
Rejected Alternatives: Exact acoustic radius precision was rejected because wake radius is a cinematic cheat, not a physical acoustic simulation.
Scalability potential: Low/MX350 uses 40 m sleep distance, behind-camera 50 percent threshold, and abyssal 20 percent threshold reduction; High/Ultra retain more active visual bodies while the solver remains capped.
Hardware Impact: Estimated 2-5 us saved on acoustic impulse bursts; main gain remains 120-420 us/frame from Burst distance culling, sleep, kinematic cull, and MeshCollider strip.

Exact Cinematic Cheats Used:
- Distance is squared-only in the Burst job; no honest distance length except telemetry conversion in existing sleep signal code.
- Behind-camera bodies cut sleep distance by 50 percent.
- Depth >= 500 m cuts sleep distance by 20 percent because abyss visibility hides early sleep.
- Low/MX350 tier uses 40 m sleep distance instead of the default 50 m.
- Acoustic wake radius uses clamped energy/intensity heuristics, not physical sound propagation.
- MeshCollider strip is a visual fake: far heavy bodies keep transform identity but leave broadphase.

Final Git Diff:
- `Assets/_Project/Scripts/GlobalPhysicsStateManager.cs`: added IPhysicsCullingOverseer/IPhysicsCullingFlagProvider, Burst PhysicsDistanceCullingJob, NativeArray culling state/result lanes, 10 Hz local culling cadence, sleep/kinematic/MeshCollider dispatch, EventBus wake, origin-shift discard/update, and 300-entry black-box dump.
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`: added IPhysicsCullingOverseer slot, getter, register, and unregister methods. Other existing GlobalRegistry diff hunks are unrelated concurrent work and were not authored by this agent.
- `Docs/Tasks/Status_PHYSICS_CULLING_OVERSEER.md`: state-machine evidence for all 19 tasks with task 19 dependency-blocked.
- `Docs/AgentLogs/Rationale_PHYSICS_CULLING_OVERSEER.md`: rationale and Omega polish evidence.

Verification:
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` failed before this assembly on `Assets/_Project/Scripts/Core/BootstrapContracts/BootstrapStatus.cs(87)` because `ITickDispatcher` and `GlobalRegistry` are invisible to `Hecton8.Bootstrap.Contracts.csproj`.
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /p:BuildProjectReferences=false /clp:ErrorsOnly` failed because referenced generated metadata DLLs were absent.
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false /p:UseSharedCompilation=false /clp:ErrorsOnly` hit the same BootstrapContracts dependency wall.

## Decision 8: Static Hardening Pass After Build Ban
Problem: The first culling dispatcher applied kinematic cull before distance-sleep velocity dampening for far bodies, kept obsolete pre-overseer helper methods, and had edge risk in telemetry/index clearing paths.
Solution: Reordered sleep before kinematic cull, removed dead distance-kinematic helper methods and duplicate SlowTick scheduling, bounded the telemetry ring writer, preserved explicit culling flags when a body is already tracked, added DataVault fallback for the AUP buffer, bounded native clear length when a vault buffer is larger than the local lanes, and reset the GlobalRegistry physics culling slot.
Rejected Alternatives: Running another dotnet build was rejected because the user explicitly prohibited it. Keeping duplicate slow scheduling was rejected because 10 Hz must be owned by the fixed-step accumulator only.
Scalability potential: Low/MX350 keeps deterministic 10 Hz culling with less duplicate scheduling; Middle/High/Ultra keep the same visual headroom without extra slow-lane wake/sleep passes.
Hardware Impact: Estimated 10-35 us saved per half-second slow tick by removing duplicate scheduling; telemetry and clear bounds prevent rare out-of-range fault classes without adding hot-path allocations.

Static Verification After User Build Ban:
- `rg` confirmed no remaining `ApplyDistanceKinematicSleep`, `RestoreDistanceKinematicSleep`, `ISlowTickable`, `SlowTick`, or `TryRegisterSlowTick` path in `GlobalPhysicsStateManager.cs`.
- `git diff --check -- Assets/_Project/Scripts/GlobalPhysicsStateManager.cs Assets/_Project/Scripts/Core/GlobalRegistry.cs` passed with CRLF normalization warnings only.
- No `dotnet build` or MSBuild command was launched during this pass.

## Decision 9: Duplicate Registration and Native Failure Guard
Problem: Hydrodynamic and tether/dock callers can re-register already tracked bodies. The previous implementation completed/discarded a pending culling job before it knew whether registration was a no-op, creating avoidable stalls and discarded 10 Hz work. A stale same-EntityId mapping could also leave an old body unindexed, and failed H8Memory/DataVault allocation could crash later when tracking writes native lanes.
Solution: Existing-body registration now updates flags/state without completing the culling job. Same-EntityId but different tracked body entries are removed before the new body is appended. Required native lanes, including the 300-frame black-box telemetry buffer, are checked before tracking accepts a body.
Rejected Alternatives: Completing every pending culling job on every duplicate registration was rejected as needless hot-path synchronization. Allowing tracking with missing native buffers was rejected because a later fixed tick would fail without useful context.
Scalability potential: Low/MX350 avoids culling-job discard churn from frequent hydrodynamic updates; Middle/High/Ultra keep the same visual budget while maintaining stable registry identity.
Hardware Impact: Estimated 20-80 us saved during dense buoyancy/connection frames by avoiding unnecessary job completion/discard; failure guard prevents rare crash classes on constrained memory.

Static Verification:
- `rg` confirmed duplicate registration now reaches `CompletePhysicsCullingJobForStateMutation(discardResults: true)` only on append/removal mutation, not on same-body updates.
- `rg` confirmed no `FindObjectOfType`, `GameObject.Find`, `Vector3.Distance`, `math.sqrt`, old sleep helpers, or `SlowTick` culling path in `GlobalPhysicsStateManager.cs`.
- `git diff --check` passed with line-ending warnings only.
- No `dotnet build` or MSBuild command was launched.

## Decision 10: Origin-Shift Culling Ownership Guard
Problem: Distance culling can make a dynamic body kinematic beyond 100 m. The existing origin-shift path skipped all kinematic bodies, which would also skip culling-owned kinematic bodies and risk leaving their runtime transform in the pre-shift frame.
Solution: Origin-shift prepare/commit now skips only authored kinematic bodies; bodies with `DistanceKinematicSleepActive` still receive snapshot and shift correction. Native culling state validation now requires fixed lane capacity, and runtime reset clears black-box telemetry entries plus the write cursor.
Rejected Alternatives: Waking all culling-owned kinematic bodies before every origin shift was rejected because it would reopen solver and collision cost during the most sensitive transform mutation window. Trusting DataVault buffer size without checks was rejected because a short native lane would fail inside Burst.
Scalability potential: Low/MX350 keeps far debris cheap and shift-safe; Middle/High/Ultra retain far visual set dressing without stale transform drift after floating-origin corrections.
Hardware Impact: Estimated crash/drift prevention with 0 us normal-frame hot-path cost; lane-capacity checks are registration/scheduling guards, and telemetry clear happens only on runtime reset.

Static Verification:
- `rg` confirmed culling-owned kinematic origin-shift guards at prepare/commit and fixed-capacity native validation.
- `rg` confirmed no forbidden scene search, exact distance, sqrt, obsolete sleep helper, or SlowTick culling path.
- `git diff --check` passed with line-ending warnings only.
- No `dotnet build` or MSBuild command was launched.

## Decision 11: PickupItem Local Distance Cull Purge
Problem: `PickupItem.FixedTick` still resolved the player transform, checked a local 100 m squared distance, and called `Sleep/WakeUp` independently of the centralized culling overseer. That reintroduced the exact scattered per-object sleep authority the prompt ordered removed.
Solution: Removed the local player-transform distance branch and kept PickupItem registered with `GlobalPhysicsStateManager`. Sleeping pickups now skip loose-current work, and awake loose-current force/torque route as ambient packets with `wake: false` so environmental drift does not immediately undo overseer sleep.
Rejected Alternatives: Leaving PickupItem as a special-case local culler was rejected because it creates two sleep authorities for loose objects. Disabling all loose-current physics was rejected because near-field item motion still buys useful immersion on higher tiers.
Scalability potential: Low/MX350 removes per-pickup player lookup/distance/sleep churn; Middle/High/Ultra retain close-range underwater drift while centralized culling owns far-body suppression.
Hardware Impact: Estimated 10-25 us saved per active loose-pickup fixed tick cluster by removing player transform resolution and distance branch; no-wake ambient packets prevent sleep/wake churn between 40-100 m.

Static Verification:
- `rg` confirmed `PickupItem` no longer contains `_playerTransform`, `CurrentSimulationCullDistance`, or `WorldRuntimeReferenceUtility.TryResolvePlayerTransform`.
- `rg` confirmed the remaining PickupItem wake is overflow-drop gameplay, not distance culling.
- `git diff --check` passed for `PickupItem.cs` with line-ending warning only.
- No `dotnet build` or MSBuild command was launched.

## Decision 12: PhysicsStateReporter Reentrancy Guard
Problem: New body registration attached `PhysicsStateReporter` before the rigidbody was inserted into `_trackedBodies` and `_trackedBodyIndexByEntityId`. Unity can invoke `Awake`/`OnEnable` during `AddComponent`, and reporter `OnEnable` calls `RegisterTrackedBody`, creating a recursive registration before the outer call had committed its index. That could orphan a duplicate tracked-body slot.
Solution: Commit the tracked body, state row, entity-id map, and last-valid position first; attach `PhysicsStateReporter` last. If `OnEnable` re-enters, the duplicate path now sees the same body in the map and exits as an update instead of appending.
Rejected Alternatives: Removing reporter registration was rejected because impact wake events require collision relay data. Adding a reentrancy bool was rejected because it hides the ordering bug and adds another lifecycle flag to maintain.
Scalability potential: Low/MX350 avoids silent registry bloat and duplicate per-body culling work; Middle/High/Ultra keep impact-driven wake behavior without duplicate body rows.
Hardware Impact: Estimated 20-60 us saved in scene-load bursts with many unreported rigidbodies by preventing duplicate insertion and later orphan cleanup; 0 us normal hot-path cost.

Static Verification:
- `rg` confirmed `_trackedBodyIndexByEntityId[bodyEntityId] = bodyIndex` now precedes `EnsureReporter(body)`.
- `rg` confirmed reporter `OnEnable` still registers through the global manager, now hitting the same-body update path.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No `dotnet build` or MSBuild command was launched.

## Decision 13: Native Lane Self-Heal
Problem: `HasRequiredNativeState` correctly rejected short native lanes, but `EnsureNativeState` only allocated missing lanes. If a DataVault alias or recovered native lane existed with too small a length, tracking would reject forever instead of repairing the cold allocation path.
Solution: Added a cold `ReleaseUndersizedNativeState` pass at the start of `EnsureNativeState`. It discards any pending culling job, releases undersized H8Memory-owned lanes, drops undersized DataVault aliases without freeing vault memory, and lets the normal allocation path reacquire fixed-capacity buffers.
Rejected Alternatives: Accepting short lanes was rejected because Burst culling would risk out-of-range writes. Fatal-failing on short lanes was rejected because cold self-heal is cheaper than leaving the runtime unrecoverable after a bad alias.
Scalability potential: Low/MX350 recovers from constrained-memory/bootstrap ordering without disabling the sleep enforcer; Middle/High/Ultra keep the same fixed-capacity fast path.
Hardware Impact: 0 us normal hot-path cost; cold recovery prevents a no-culling fallback that would cost 120-420 us/frame in dense debris scenes.

Static Verification:
- `rg` confirmed `ReleaseUndersizedNativeState` runs before culling native allocation checks.
- `rg` confirmed undersized DataVault aliases are defaulted instead of released through H8Memory.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No `dotnet build` or MSBuild command was launched.

## Decision 14: Depth Signal Sanitization
Problem: Abyss depth LOD depends on player depth. Runtime or fallback depth could be NaN, and `math.max(0f, NaN)` can preserve NaN, leaving the abyss threshold comparison false and disabling the intended culling reduction.
Solution: Sanitize both PlayerRuntimeContext depth and GlobalRegistry.Player fallback depth with `math.isfinite` before clamping to non-negative meters.
Rejected Alternatives: Trusting player movement depth was rejected because this system is the last physics-culling gate before Burst scheduling. Dumping the black box for depth-only NaN was rejected because bad depth can fall back safely without invalidating body state.
Scalability potential: Low/MX350 keeps the 20 percent abyss cull reduction deterministic; Middle/High/Ultra keep visual density decisions tied to valid depth only.
Hardware Impact: 0 us meaningful runtime cost; prevents far-body solver retention in invalid-depth frames.

Static Verification:
- `rg` confirmed both depth sources are now guarded by `math.isfinite`.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No `dotnet build` or MSBuild command was launched.

## Decision 15: Registry Slot Mapping for Overseer Facade
Problem: `GlobalRegistry.RegisterPhysicsCullingOverseer` stored the interface facade, but generic service-slot resolution did not know `IPhysicsCullingOverseer`. That left registry diagnostics and rebound masks treating the culling facade as `Unknown`.
Solution: Mapped `IPhysicsCullingOverseer` to the existing `PhysicsStateManager` service slot because the overseer is implemented by `GlobalPhysicsStateManager` and shares the same bootstrap lifetime.
Rejected Alternatives: Adding a new registry enum slot was rejected because this is not an independent service owner and would expand boot dependency surface without runtime value.
Scalability potential: Low/MX350 and High/Ultra share one physics-state authority; diagnostics now reflect that the culling facade is part of the same scalable physics manager.
Hardware Impact: 0 us runtime hot-path cost; improves boot/rebind observability and prevents Unknown-slot blind spots.

Static Verification:
- `rg` confirmed `IPhysicsCullingOverseer` resolves to `GlobalRegistryServiceSlot.PhysicsStateManager`.
- `git diff --check` passed for `GlobalRegistry.cs` with line-ending warning only.
- No `dotnet build` or MSBuild command was launched.

## Decision 16: Idempotent Culling Command Enforcement
Problem: Once a body was already marked `DistanceSleepActive`, `DistanceKinematicSleepActive`, or `MeshColliderStripActive`, the dispatcher skipped the apply path. A different script could wake the body, restore collision detection, or re-enable a stripped MeshCollider, leaving the overseer state saying culled while PhysX was paying live cost. The hot restore path also performed a linear `FindTrackedBodyIndex` even when the caller already had the body index.
Solution: Made sleep, kinematic cull, and mesh-strip application idempotent. Active sleep now re-dampens and re-sleeps if the body was disturbed. Active kinematic cull reasserts `isKinematic = true`, `detectCollisions = false`, and `Sleep()`. Active mesh stripping re-disables cached MeshColliders. Restore calls in dispatch, event wake, removal, and runtime reset now pass the known body index.
Rejected Alternatives: Trusting external scripts not to touch culling-owned state was rejected because physics ownership is explicitly centralized. A broad per-frame reconciliation scan was rejected because the 10 Hz command pass already owns the correct cadence.
Scalability potential: Low/MX350 keeps far debris suppressed even when ambient systems poke Rigidbodies; Middle/High/Ultra keep the same visual density without leaked solver/broadphase cost.
Hardware Impact: Estimated 5-20 us saved per disturbed far-body cluster by preventing repeated live PhysX cost; removes O(n) restore self-lookups from the 10 Hz dispatch path.

Static Verification:
- `rg` confirmed direct-index restore calls in dispatch, acoustic/impact wake, removal, and runtime reset.
- `rg` confirmed `DampenBodyVelocityForSleep` and `EnforceMeshColliderStrip` are used by active culling state paths.
- `git diff --check` passed for `GlobalPhysicsStateManager.cs` with line-ending warning only.
- No `dotnet build` or MSBuild command was launched.

## Decision 17: Ambient Packet Sleep Authority Guard
Problem: Ambient force and torque packets can reach Unity `AddForce`/`AddTorque` without the explicit WakeBody flag. Unity can still implicitly wake sleeping Rigidbodies during force application, which creates a 0.1 s leak window where current/fluid packets can undo centralized culling before the next overseer pass re-sleeps the body.
Solution: Added `IPhysicsCullingOverseer.IsBodyCulled(Rigidbody)` as a narrow query and made `PhysicsApplySystem` discard no-wake ambient packets for bodies currently owned by distance sleep, distance kinematic cull, or MeshCollider strip. Critical/gameplay packets still pass through and can wake through explicit event/force paths.
Rejected Alternatives: Adding a broad physics-layer dependency from `PhysicsApplySystem` to `GlobalPhysicsStateManager` internals was rejected; the registry facade keeps ownership decoupled. Relying only on idempotent re-sleep was rejected because it still spends solver/broadphase cost until the next 10 Hz command pass.
Scalability potential: Low/MX350 avoids ambient-current churn on far debris; Middle/High/Ultra keep near-field fluid motion while far bodies stay visually present but physically silent.
Hardware Impact: Estimated 10-40 us saved during dense ambient-fluid frames by preventing no-wake environmental packets from reopening culled rigidbodies; added cost is one O(1) dictionary-backed overseer query per ambient packet.

Static Verification:
- `rg` confirmed `IsBodyCulled` exists on the overseer facade and is implemented by `GlobalPhysicsStateManager`.
- `rg` confirmed `PhysicsApplySystem` discards no-wake ambient packets before force/torque application when the overseer reports a culled body.
- `git diff --check` passed for the touched physics and evidence files with CRLF warnings only.
- No `dotnet build` or MSBuild command was launched.

## Decision 18: Explicit Tether Culling Locks
Problem: The prompt requires critical items such as tethers to never sleep. The connection registry existed, but culling was only blocked for mass-ratio compensated bodies. A tethered payload with normal mass ratio could still be distance-slept or kinematic-culled if its cable length or camera bias pushed it outside the threshold.
Solution: Added `CullingLockRefCount` to tracked body state. Active tether connections lock both anchor and payload bodies; dock connections lock the docked body. Register/update applies an immediate lock and restores any already-culled body, while the fixed-step connection evaluation refreshes locks before the 10 Hz culling job snapshots state.
Rejected Alternatives: Marking all tether payloads through tags or prefab scripts was rejected because it scatters sleep authority back into object authorship. Reusing `CompensationRefCount` alone was rejected because mass compensation and culling exclusion are related but not identical policies.
Scalability potential: Low/MX350 still culls loose debris aggressively, but active tethers preserve gameplay control. Middle/High/Ultra keep cable visuals and payload interaction alive without far-body sleep errors.
Hardware Impact: Estimated 0-5 us overhead for two O(1) lock updates per active tether; prevents gameplay-breaking solver removal on active tow payloads.

Static Verification:
- `rg` confirmed `CullingLockRefCount` is reset, incremented for tether/dock connections, and consumed by both culling-allowed checks and Burst state snapshots.
- `rg` confirmed `TetherInstance.Configure` already registers tether connections through `GlobalPhysicsStateManager.RegisterTetherConnection`.
- `git diff --check` passed for the touched physics and evidence files with CRLF warnings only.
- No `dotnet build` or MSBuild command was launched.
