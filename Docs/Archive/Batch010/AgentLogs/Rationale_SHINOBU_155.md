# SHINOBU_155 Rationale - Player Death And Reconciliation Sequence

Status: PENDING_COMPILE_RUNTIME_PROOF_EXTERNAL_BRIDGE_ERRORS

## Decision 0 - Establish Fresh Batch State

Problem: SHINOBU_155 had no current `Status_` or `Rationale_` files, and the batch requires file-backed memory before code work.
Solution: Create fresh status/rationale files before gameplay edits. This preserves task boundaries under context compression and prevents undocumented work.
Rejected Alternatives: Chat-only tracking; modifying gameplay code before source archaeology; reading previous batch logs as authority.
Scalability potential: Low/Middle/High/Ultra unchanged; this is process state, not runtime feature.
Hardware Impact: Runtime impact 0 us. Editor/process impact irrelevant to i3/MX350 frame time.

## Decision 1 - Mandate Set

Problem: Death reconciliation crosses survival, AUP, Vault, SignalBus, shader presentation, and telemetry boundaries.
Solution: Use eight mandates: non-reload reset, zero-GC, ARM64 DTO layout, AUP precision, AUP determinism sync, SignalBus lane segregation, execution phases, post-mortem telemetry.
Rejected Alternatives: Treating death as UI/cutscene only; adding direct concrete dependencies on kinematics/base/AI; binary quality toggles.
Scalability potential: Low uses shortest fade and cheapest shader scalar; Middle keeps normal fade; High adds richer presentation; Ultra may add stronger post-process distortion in VISUAL_SYNC only.
Hardware Impact: Target is one-frame reconciliation under 0.1 ms suspicious threshold. Expected low-end gain is removal of scene reload, asset churn, and forced GC; exact microseconds pending profiler.

## Decision 2 - Reconciliation Route

Problem: `HectonPlayerHealth.Die()` has no reload path to delete, but it also has no authoritative AUP/physiology reset and leaves death as a managed event/TODO.
Solution: Keep the player GameObject alive, emit a blittable `PlayerRespawnSignal` through the contract lane, restore the local health scalar immediately to avoid a stuck dead state, and let SHINOBU_155 reconcile Vault-backed physiology/AUP in dispatcher phases.
Rejected Alternatives: Directly referencing Physiology from Gameplay; moving the Transform in `Die()`; building a loading-screen substitute; waiting for Base Logistics before providing a mock medical bay.
Scalability potential: Low uses a single scalar blackout and fast decay; Middle adds moderate grain/chromatic scalar; High/Ultra retain longer Dear Lie cover and stronger shader distortion without CPU simulation.
Hardware Impact: Death path removes scene reload/asset churn. Expected low-end gain is frame-time continuity instead of 15 s stall; per-death CPU target is under 100 us and no managed allocation after boot.

## Decision 3 - Vault Buffer IDs

Problem: SHINOBU_155 needs persistent buffers but core `BufferID` should not be churned for a domain-owned feature.
Solution: Follow existing physiology pattern and define local constants cast to `BufferID` in the SHINOBU_155 data file. Use the 71604-71613 range after source audit showed 71580-71584 were already documented by SHINOBU_124 and 71592-71603 are owned by the submarine autopilot lane.
Rejected Alternatives: Editing `H8Memory.cs`; declaring private persistent `NativeArray` fields; piggybacking respawn state into unrelated physiology DTO bits.
Scalability potential: One owner, one route: every tier reads the same Vault state; quality only affects fade curve and shader complexity scalars.
Hardware Impact: Local `BufferID` constants have 0 runtime cost and avoid compile-wall edits in core memory headers.

## Decision 4 - Cross-Domain Mesofauna Consumer

Problem: Task 12 explicitly requires Mesofauna to drop player aggro on `PlayerRespawnSignal`, but AI is a sibling domain.
Solution: Add only a contract-signal snapshot read in `PredatorCognitionDomain`'s existing signal/data stage and clear `TargetHashID`/state only for coherent respawn facts: request phase with `Requested` and no `Committed`, or committed phase with `Committed`. No direct Physiology type references are introduced.
Rejected Alternatives: A managed callback from Physiology into AI; destroying predator targets; scheduling a separate job that would need a new dependency fence for a small same-stage mutation; ignoring Task 12.
Scalability potential: Low/Middle/High/Ultra identical data mutation; predator simulation cost decreases for at least one frame after death because hunt state is cleared.
Hardware Impact: Signal scan is bounded to same-frame snapshot and clear is O(active mesofauna). Expected cost is below 10 us for current slot counts; it removes repeated predator targeting after respawn.

## Decision 5 - Vault Lock Semantics

Problem: The first pass treated `GlobalDataVault.IsAllocationLocked` as a hard runtime failure. That would prevent already-created respawn handles from being resolved after the boot allocation window closes.
Solution: Split `EnsureVaultState()` into "handles already created" versus "need new handle requests." Allocation lock now blocks only first-time buffer requests; hot-path reads of existing Vault handles continue.
Rejected Alternatives: Leaving the runtime disabled after allocation lock; using private persistent arrays as a fallback; force-unlocking the Vault.
Scalability potential: Low/Middle/High/Ultra share the same preallocated memory. Throttled devices do not pay a late allocation retry loop after boot.
Hardware Impact: Removes a branch-to-failure path and prevents death reconciliation from falling back to managed component-only state. Estimated saved cost is failure recovery, not frame CPU; hot path remains one handle-created branch.

## Decision 6 - Dear Lie Route

Problem: A real camera teleport without visual cover is perceptually broken, but a UI fade prefab or coroutine would allocate and reintroduce managed transition state.
Solution: Publish `_HectonRespawnDearLieParams` and `_HectonDeathFadeIntensity` through the existing shader global Vault bridge, then let `Hecton8_UberNoir.hlsl` darken the frame with quality-weighted grain/chromatic bias.
Rejected Alternatives: Canvas overlay; Timeline/coroutine fade; simulated camera travel; scene reload blackout.
Scalability potential: Low uses fast blackout and minimal shader math; Middle keeps grain; High/Ultra spend saved CPU on stronger chromatic/film response.
Hardware Impact: CPU cost is one `float4` publish. Shader work collapses continuously by quality scalar; expected CPU saving versus UI fade/controller is 20-60 us on low-end silicon and eliminates managed transition objects.

## Decision 7 - Editor And CSV Human Control

Problem: Designers need to tune death fade and penalties without C# recompiles, but runtime string parsing and editor UI must not leak into the gameplay path.
Solution: Keep the UI Toolkit tuner under `#if UNITY_EDITOR`, write tuning directly into `RespawnTuningDTO`, use a cold precomputed fade-readout LUT to avoid per-refresh string formatting, and parse `respawn_penalty_rules.csv` cold via byte spans into Vault-backed `InventoryDeathPenaltyRuleDTO` rows.
Rejected Alternatives: ScriptableObject-only settings requiring import/recompile; `string.Split`; direct inventory mutation from the respawn job.
Scalability potential: Low-to-Ultra all consume the same unmanaged tuning DTO. Quality controls fade rate and shader detail; penalty logic stays bounded.
Hardware Impact: Runtime hot path has 0 managed parsing allocation. Cold CSV read is editor/boot only; per-death inventory command emission is one NativeQueue write when enabled.

## Decision 8 - Verification Boundary

Problem: A full C# build is required for final compile proof, but AGENTS forbids running `dotnet build` while CPU load exceeds 50% or another compiler is active, and the user explicitly said not to launch build until needed.
Solution: Run static source scans and the CPU/compiler guard first. Current guard found no `dotnet`/`csc` process; CPU samples were `100`, `72.039`, and `29.782`, so compile was deferred because the first two samples violated the guard and static verification was the lower-risk next step.
Rejected Alternatives: Launching build under transient high CPU load; claiming compile proof from static scans; waiting on unrelated system load without continuing docs/static verification.
Scalability potential: Process-only decision. Runtime tier behavior unchanged.
Hardware Impact: Prevents additional compile contention on a saturated workstation. Runtime impact 0 us.

## Decision 9 - Inventory Penalty Rule Contract Repair

Problem: The first penalty route parsed CSV rows but inventory consumed only a coarse `DropNonEquippedResources` command. It also hashed item tokens with lowercased byte FNV, while inventory item IDs use `LocHash` UTF-16 FNV over exact persistent IDs. Parsed rules could silently fail to match runtime items.
Solution: Move the 16-byte rule row into `InventoryDeathPenaltyRuleDTO` under Core contracts, keep the SHINOBU-owned Vault buffer ID, and pass buffer ID/rule count through spare fields in the existing 32-byte `InventoryCommandSignal`. Inventory resolves the Vault table through cached `IDataVault`, scans bounded rule rows, and applies `DropOnDeath`/`RetainIfEquipped` per item hash without polling `GlobalRegistry` from command consumption. If a command claims a Vault rule table and the table cannot be resolved, Inventory fails closed instead of applying broad fallback drops. The CSV parser now accepts raw numeric hashes or computes `LocHash.ComputeUtf8AsUtf16` from byte spans. The XML's NativeHashMap wording is intentionally mapped to a fixed Vault row table because GlobalDataVault exposes typed buffers, not owned hash containers, and the 64-row cap keeps lookup deterministic and memcpy-safe.
Rejected Alternatives: Direct Physiology-to-Inventory calls; duplicating an identical DTO in Inventory; a managed dictionary/NativeHashMap field in the runtime; case-folded hashes that do not match item persistent IDs.
Scalability potential: Low keeps the same bounded table scan and can use a small death penalty budget; Middle/High/Ultra can author richer per-item rules without code changes. Visual overkill remains bought by the shader Dear Lie, not by heavier inventory logic.
Hardware Impact: Hot path adds only a bounded rule scan on death frames, not per-frame work. Low-end i3/MX350 impact is expected below the 0.1 ms suspicion threshold for current `64` rule capacity; avoids accidental broad item scans caused by mismatched rules.
First 20 Minutes Route Impact: Removes the early survival-loop death loading screen blocker while preserving item-loss rules for the first resource collection -> danger -> recovery loop.

## Decision 10 - Physics Collision Suspend Consumer

Problem: The first implementation set `PlayerRespawnSignalFlags.SuspendCollision`, but Physics did not actually consume it. That left Task 06 only half-proven.
Solution: Patch `HydrodynamicKccRuntime` to read only the contract signal snapshot. A coherent request packet (`Request+Requested+no Committed`) or committed packet (`Committed+Committed`) with `SuspendCollision` sets a one-frame `_respawnCollisionBypassFrames` latch keyed by `SignalBus<PlayerRespawnSignal>.SnapshotGeneration`; the KCC skips `CapsulecastCommand.ScheduleBatch`, bypasses collision hit extraction, passes `CollisionBypass=1` into `KinematicResolutionJob`, and marks debug flags with `FlagRespawnCollisionBypass`. The snapshot-generation latch prevents duplicate extension, so the suspend remains exactly one accepted snapshot generation.
Rejected Alternatives: Direct Physiology call into Physics; disabling the CapsuleCollider; clearing Unity layers; allowing committed and requested signals to produce two collision-free frames.
Scalability potential: Low saves the full capsulecast batch on the death frame; Middle/High/Ultra keep the same deterministic bypass and spend the saved CPU on the Dear Lie shader cover rather than extra collision work.
Hardware Impact: On the respawn frame, one `CapsulecastCommand.ScheduleBatch` plus hit extraction is skipped. Expected saving depends on `entityCapacity * maxHits`, but for the player lane it removes a physics query and avoids contact correction against stale pre-teleport geometry.

## Decision 11 - Deterministic Frame Stamp For Death Request

Problem: `PlayerDeathReconciliationBridge` originally stamped `PlayerRespawnSignal.Frame` with `Time.frameCount`, which is not the deterministic dispatcher-facing frame source requested by the rollback constraint.
Solution: Stamp the request with `TimeSliceScheduler.CurrentFrameId`, which is advanced by the master dispatcher pre-simulation route. The authoritative reconciliation job still records `DispatcherJobContext.Frame`.
Rejected Alternatives: Keeping `Time.frameCount`; adding a direct SystemDispatcher API; storing a managed clock singleton in Gameplay.
Scalability potential: Low/Middle/High/Ultra unchanged; this removes nondeterministic metadata drift rather than changing visual cost.
Hardware Impact: Runtime impact is one static uint read instead of a Unity time call. The meaningful gain is rollback trace consistency, not frame time.

## Decision 12 - Core Signal Lane Authority Repair

Problem: `PlayerRespawnSignal` existed as a contract payload and local producers configured it, but Core did not yet own its direct flush/clear, finite guard, layout validation, or IL2CPP preservation entry. That left the route dependent on local fallback registration instead of the same explicit GlobalSignals authority used by other first-party lanes.
Solution: Added `PlayerRespawnSignal` to `GlobalSignals` direct pre-simulation flush and post-simulation clear, registered its central capacity through `InitializeCategorySignalLanes`, validated the current 128-byte payload size, added a finite sanitizer for both `double3` AUPs plus phase/collision-frame bounds, added `HectonSignalLaneContract.PlayerRespawnSignal` with stable hash `0x5253504E`, and preserved the closed generic lane in `SignalWardenRuntime`. The payload now exposes its lane capacity constants so Gameplay and Physiology early-boot calls reuse the same numbers instead of hardcoded duplicates.
Rejected Alternatives: Leaving the lane as fallback-only; adding a direct Gameplay-to-Physiology callback; moving the signal into a sibling runtime assembly; accepting unguarded `double3` AUP payloads.
Scalability potential: Low/Middle/High/Ultra consume the same bounded lane: expected capacity 8, max frame signals 16, low-tier frame signals 4. The quality-dependent cost remains in the shader fade and KCC work avoided on the respawn frame, not in extra broadcast traffic.
Hardware Impact: Prevents a lost or wiped respawn snapshot without increasing steady-state frame work. On low-end silicon, the bounded direct lane avoids fallback registry dispatch for this death route and rejects non-finite AUPs before AI/Physics consumers can ingest corrupted data.
First 20 Minutes Route Impact: Protects the first death/recovery loop from a signal-routing failure where health resets locally but AI/KCC/physiology consumers never see the authoritative respawn packet.

## Decision 13 - Visual Sync Phase Discipline

Problem: `PlayerDeathReconciliationBridge` emitted the contract signal and also pushed `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie` immediately from Gameplay. That created a second visual route outside `VISUAL_SYNC` and could allocate/initialize shader-global Vault storage from the fatal-damage seam if the graphics slot buffer was not prewarmed.
Solution: Remove the Gameplay shader write. Gameplay now emits only `PlayerRespawnSignal`. The first visual cover is authored by `ResetPlayerPhysiologyJob` into `RespawnFadeDTO`, then `ShinobuRespawnReconciliationRuntime` publishes the Dear Lie scalar during its VisualSync adapter.
Rejected Alternatives: Keeping a duplicate emergency shader write; adding a Gameplay-to-Rendering direct dependency; using a UI overlay fallback; forcing shader-global Vault allocation from fatal damage.
Scalability potential: Low/Middle/High/Ultra still consume the same continuous `GlobalQualityWeight` in the fade job and shader. The change removes a non-authoritative route; it does not reduce visual overkill on high tier.
Hardware Impact: Death detection now performs one bounded signal enqueue and no shader-global DataVault lookup/write. The shader scalar update remains in VisualSync, where graphics state belongs.
First 20 Minutes Route Impact: Prevents the first death recovery from mutating rendering state before the authoritative Vault reconciliation has accepted the respawn request.

## Decision 14 - Visual Sync Job Fence Repair

Problem: `VisualSyncTick` read `RespawnFadeDTO` after `PostSimulationTick` attempted a non-blocking reclaim, but it did not re-check the active job fence. If the fade job was still running, VisualSync could read a Vault row while Burst still owned writes.
Solution: Add a non-blocking fence gate at the start of VisualSync. If `_activeHandle` is not completed, VisualSync returns and keeps the previous shader scalar. If it is completed, `CompleteActiveJobIfReady(false)` reclaims the handle before reading the fade row.
Rejected Alternatives: Calling `JobHandle.Complete()` unconditionally in VisualSync; allowing a read/write race; duplicating fade state in a managed field; publishing a Gameplay-phase fallback shader scalar.
Scalability potential: Low/Middle/High/Ultra unchanged. The only behavior difference is correctness under a slow frame: weak hardware can skip a VisualSync publish instead of stalling the main thread.
Hardware Impact: Prevents a main-thread block and a native data race. In overloaded frames the cost is one `IsCompleted` branch and no shader-global write until the job fence is safe.
First 20 Minutes Route Impact: Protects death recovery visuals from stale or half-written fade values during the early survival loop.

## Decision 15 - Cross-Frame Writer Fence Repair

Problem: `ScheduleSimulation` could start a new reset/fade job over the same Vault pointers while the previous frame's `_activeHandle` was still in flight. VisualSync was protected, but the simulation producer could still stack two writers to `RespawnStateDTO`/`RespawnFadeDTO` rows.
Solution: Gate `ScheduleSimulation` on `_jobScheduled`. If the prior handle is incomplete, return `JobHandle.CombineDependencies(dependsOn, _activeHandle)` and do not schedule another writer. If it is complete, reclaim it non-blockingly and then schedule the next job.
Rejected Alternatives: Completing the old job unconditionally in Simulation; allowing overlapping writers; copying state into a second private buffer; adding a managed queue of pending deaths.
Scalability potential: Low/Middle/High/Ultra unchanged; overloaded devices naturally skip one respawn/fade update rather than blocking or racing.
Hardware Impact: Avoids false sharing/data races on Vault rows with one branch and one combined dependency in the rare slow-job case. No new persistent memory is allocated.
First 20 Minutes Route Impact: Prevents repeated lethal events during early survival from corrupting the active respawn row while the previous reconciliation is still executing.

## Decision 16 - Deterministic Frame Source Sweep

Problem: Static verification still found Unity `Time.frameCount` in health/survival signal payloads adjacent to lethal physiology traffic. `PlayerRespawnSignal` was already dispatcher-stamped, but `SurvivalVitalsChangedSignal` on death and physiology/vital warning freshness were still in Unity frame space.
Solution: Replace those frame stamps and the freshness delta with `TimeSliceScheduler.CurrentFrameId`, the dispatcher frame source already used by the respawn bridge.
Rejected Alternatives: Leaving Unity frame metadata around the death route; adding a new clock service; changing signal payload layouts; touching unrelated gameplay time users outside SHINOBU's health/survival files.
Scalability potential: Low/Middle/High/Ultra unchanged. The value is deterministic trace coherence under rollback and black-box replay, not visual fidelity.
Hardware Impact: Static uint read cost is equivalent or cheaper than Unity frame access. The important gain is removal of a rollback drift source from lethal survival telemetry.
First 20 Minutes Route Impact: Keeps the first death/recovery loop's vitals, physiology, and respawn signals in one frame domain for post-mortem correlation.

## Decision 17 - Compile Wall Boundary

Problem: After the CPU/process guard passed, `dotnet build Hecton8.Core.csproj --no-restore` failed before SHINOBU code on `CS2001` because `Assets/_Project/Scripts/Construction/LogisticsPipeEvents.cs` is deleted while still referenced by the generated project.
Solution: Treat this as an unrelated compile wall and do not create a construction-domain stub or mutate the generated project file. Continue static verification and record the exact blocker for the integrator.
Rejected Alternatives: Fabricating a missing Construction file; editing `Hecton8.Core.csproj` by hand; rerunning build repeatedly against the same missing source; reverting another agent's deletion.
Scalability potential: Runtime tier behavior unchanged. This is integration hygiene, not SHINOBU feature logic.
Hardware Impact: Prevents wasted compile loops. Runtime impact 0 us.
First 20 Minutes Route Impact: The respawn route cannot receive compile proof until the construction source/project reference is reconciled.

## Decision 18 - Cached Vault Authority Tightening

Problem: `ShinobuRespawnReconciliationRuntime.ResolveVault()` could be called from dispatcher phases and would fall back to `GlobalRegistry.DataVault` if `_dataVault` was null. That violates the cold-discovery rule and hides missing boot wiring behind hot-path service polling.
Solution: Split the route: dispatcher phases use cached `_dataVault` only and fail closed when absent; `ResolveVaultCold()` retains the `GlobalRegistry`/latest-Vault fallback for Awake, Start, hot-swap/editor utilities, and gizmos.
Rejected Alternatives: Polling `GlobalRegistry` in PreSimulation/Simulation/VisualSync; storing private NativeArrays as fallback; direct construction of a local Vault; throwing exceptions on missing Vault.
Scalability potential: Low/Middle/High/Ultra unchanged. Weak devices avoid a possible cold service lookup branch in death/fade ticks.
Hardware Impact: Hot path removes service locator fallback. Estimated saving is tiny per tick, but the real gain is architectural: no hidden global lookup in deterministic dispatcher phases.
First 20 Minutes Route Impact: Death recovery now either runs on the boot-injected Vault or fails closed with no scene reload/object churn.

## Decision 19 - Legacy Managed Death Event Ejection

Problem: Reconciled deaths still invoked legacy managed `OnDeath` listeners before local reset. `HectonSurvivalSystem.OnDeath` currently reaches PDA logbook code and can create UI/logbook side effects inside the fatal frame, conflicting with the zero-GC reconciliation mandate.
Solution: Reconciled deaths return immediately after signal emission and local scalar reset. Managed `OnDeath`, `PlayerDiedEvent`, and development log fallback remain only for the unreconciled failure path.
Rejected Alternatives: Publishing `PlayerDiedEvent` for reconciled death; keeping PDA logbook side effects in the fatal frame; adding a direct PDA callback; allocating a replacement managed event.
Scalability potential: Low/Middle/High/Ultra unchanged. Death truth is now the signal/Vault black box; presentation is shader-driven.
Hardware Impact: Removes managed delegate fan-out and PDA/meta side effects from the reconciled death frame. Exact GC delta requires Unity Profiler, but the code path no longer calls the known `OnDeath` subscriber on successful reconciliation.
First 20 Minutes Route Impact: The first lethal survival event no longer risks dragging journal/meta systems into the one-frame respawn path.

## Decision 20 - Current-Frame Respawn Snapshot Repair

Problem: Gameplay can only publish the lethal request immediately, so the first `PlayerRespawnSignal` starts with `RespawnAUP = DeathAUP`. Physiology resolves the actual med-bay AUP later in `PreSimulation`. If that resolved target is only stored in Vault or republished as a queued signal, same-frame Physics/AI consumers can see stale death AUP or wait one frame.
Solution: Keep Physiology as the sole med-bay resolver, then mutate the current `SignalBus<PlayerRespawnSignal>` snapshot in-place with `SignalBus<PlayerRespawnSignal>.TransformSnapshot`. The transformer writes resolved `RespawnAUP`, `MedicalBayHashID`, `Requested`, `Committed`, `SuspendCollision`, translated med-bay flags, and clamps `SuspendCollisionFrames` to the payload maximum. `HydrodynamicKccRuntime` now treats only coherent request or committed respawn packets as eligible, while the accepted-generation latch preserves the one-frame collision bypass.
Rejected Alternatives: Queueing a second committed signal for the next frame; making Gameplay query med-bay Vault state; adding a direct Physiology-to-Physics call; keeping KCC dependent on the producer's original `Requested` bit only; leaving `math.max(byte, byte)` in the transformer.
Scalability potential: Low/Middle/High/Ultra unchanged for simulation truth. Same-frame correction prevents a visual/physics pop on weak devices and lets high-tier Dear Lie shader spend more time on presentation instead of covering an avoidable stale-collision frame.
Hardware Impact: Adds an O(snapshotCount) unmanaged value-transform over a lane capped at 16 events per frame; expected cost is below 1 us on i3/MX350 for normal 0-1 death packet traffic. Avoids one extra frame of stale capsule collision and a queued duplicate signal.
First 20 Minutes Route Impact: The first death/recovery loop now gives KCC and mesofauna the resolved med-bay AUP in the same frame the request is accepted, without a loading screen or object churn.

## Decision 21 - Kinematic Sector Denominator Guard

Problem: `ResetPlayerPhysiologyJob.WriteKinematic()` divided target AUP by `HectonPhysicsContract.AupSectorSizeMetersDouble`. The project contract expects that constant to be valid, but the NaN mandate requires every division feeding physics truth to be guarded locally.
Solution: Clamp the denominator with `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)` before calculating the sector. The existing local-position math then subtracts the guarded sector root and casts only the local residual to `float3`.
Rejected Alternatives: Trusting a global contract constant without a local guard; moving the sector split to managed code; using `Transform.position`; adding exception checks in a Burst path.
Scalability potential: Low/Middle/High/Ultra unchanged. This is correctness hardening, not visual cost. It prevents a single bad constant or corrupted contract payload from poisoning KCC/rollback state.
Hardware Impact: One `math.max` in the rare respawn job. Expected cost is below measurement noise on i3/MX350 and buys NaN containment for physics truth.
First 20 Minutes Route Impact: The first death recovery cannot poison the player kinematic row through a zero sector-size denominator.

## Decision 22 - Local AUP Clamp Range Guard

Problem: After the sector-division fix, `SafeLocal()` and runtime `AupDeltaToFloat3()` still used `HectonPhysicsContract.AupSectorSizeMetersDouble` directly as the clamp range. It is not a division, but if that constant were corrupted to zero or non-finite, local validation vectors could collapse or propagate invalid values before the fallback path.
Solution: Add `SafeAupClampMeters()` in the Burst job file and the runtime file, each returning `math.max(HectonPhysicsContract.AupSectorSizeMetersDouble, 0.0001d)`. Local AUP residual conversion and medical-bay validation now use the guarded range before casting to `float3`.
Rejected Alternatives: Trusting the contract constant in clamp helpers; duplicating raw `math.max` calls at each clamp site; moving validation into managed `Vector3` math; widening the patch into unrelated AUP systems.
Scalability potential: Low/Middle/High/Ultra unchanged. This is deterministic containment; the visual overkill budget stays in the Dear Lie shader.
Hardware Impact: One double `math.max` per local conversion helper call on the rare respawn/validation path. Expected cost is below measurement noise on i3/MX350; the gain is preventing a bad clamp range from poisoning target validation or rollback kinematic truth.
First 20 Minutes Route Impact: The first lethal recovery route now protects both the sector split and the local validation clamp before med-bay AUP selection reaches Physics/Fauna consumers.

## Decision 23 - Med-Bay Authority De-Duplication

Problem: `ShinobuRespawnReconciliationRuntime` already resolves the med-bay target in `PreSimulation`, writes `RespawnStateDTO`, and transforms the current `PlayerRespawnSignal` snapshot. `ResetPlayerPhysiologyJob` then repeated the same nearest-med-bay scan as its primary route, creating two target-selection authorities in one death frame.
Solution: Keep `PreSimulation` as the owner of med-bay resolution. `ResetPlayerPhysiologyJob` now consumes the staged `RespawnStateDTO.TargetAUP` and `MedicalBayHashID` when the staged state is pending, finite, and either has a med-bay hash or an explicit fallback flag. Staged route flags are applied only when the staged target is accepted; the fallback scan recomputes mock/invalid/fallback flags itself so stale state cannot leak into a new death request. The committed request row now preserves `MockMedicalBay` along with invalid/fallback/penalty flags for black-box correlation. The job scans `MedicalBayRespawnPointDTO` rows only as a fail-closed fallback when the staged state is missing or invalid.
Rejected Alternatives: Leaving duplicate med-bay scans as "harmless"; moving med-bay selection into Gameplay; expanding `RespawnRequestDTO` layout to carry another target copy; adding a direct Base Logistics dependency to the job; deleting the fallback scan and making the job brittle under boot/order failures.
Scalability potential: Low removes redundant Burst work from the death frame while keeping deterministic fallback. Middle/High/Ultra keep the same authoritative target and spend saved CPU budget on the Dear Lie shader, not on a second simulation truth lookup.
Hardware Impact: Normal death path removes one O(medBayCount) scan from `ResetPlayerPhysiologyJob`; with current capacity this is up to 8 row reads and AUP local-distance evaluations avoided on i3/MX350. Fallback path remains bounded and only runs on missing/non-finite staged state.
First 20 Minutes Route Impact: The first death/recovery loop now has one med-bay owner in the accepted frame, reducing the chance of Physics/Fauna seeing a target chosen by one path while Kinematic truth is chosen by another.

## Decision 24 - Hot-Path Literal New Erasure

Problem: The respawn path had no managed gameplay allocations, but `new` value-type/object-initializer syntax remained in Burst jobs, Simulation job scheduling, VisualSync shader payload publishing, cold DTO defaults, and helper return paths. That creates bad review evidence and can hide unnecessary struct construction/copy patterns in the exact files that must be trivially memcpy/Burst-readable.
Solution: Rewrite SHINOBU job bodies and hot dispatcher code to `default` locals with explicit field assignment. `ResetPlayerPhysiologyJob`, `UpdateRespawnFadeJob`, VisualSync `Vector4`, `RespawnFadeDTO`, `InventoryDeathPenaltyRuleDTO`, fallback `double3`, local `float3`, editor gizmo offsets, and telemetry/physiology DTO writes now avoid literal `new` syntax. Remaining `new` sites are cold and explicit: one runtime host GameObject, four dispatcher adapter objects, cold FileStream/BinaryWriter dump/CSV IO, stack-only `Span<byte>` constructor, and teardown/service-replacement `JobHandle.Complete()` fences.
Rejected Alternatives: Treating value-type `new` as harmless and leaving noisy hot-path evidence; adding managed pools for structs; removing the cold runtime host without proving another dispatcher owner exists; replacing teardown `Complete()` with unsafe native-buffer release while a job may still own pointers.
Scalability potential: Low/Middle/High/Ultra unchanged in behavior. The low-tier benefit is tighter Burst/codegen hygiene and fewer false positives in allocation scans; high/ultra keep the same VisualSync shader overkill route.
Hardware Impact: No measured microsecond saving is claimed from syntax alone. The important i3/MX350 impact is risk removal: job scheduling, shader payload publish, and Burst DTO writes now have no literal `new` evidence, and static scans can distinguish hot path from cold boot/editor IO.
First 20 Minutes Route Impact: The first death/recovery loop now has a cleaner zero-GC proof surface: lethal request -> Vault reconciliation -> shader Dear Lie publish uses explicit unmanaged value mutation, not initializer syntax that can be misread as allocation.

## Decision 25 - Death-Vicinity Signal Initializer And Pack Purge

Problem: After the SHINOBU respawn files were cleaned, adjacent health/survival code still published mutable GlobalSignals via object-initializer `new`, and `SurvivalDatabaseItemRecord` used `[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]`. The signal initializers are value-type syntax rather than heap allocation, but they weaken zero-GC evidence in the fatal-damage vicinity. `Pack=1` is a direct ARM64 alignment violation even if the row is cold parsed.
Solution: Rewrite `VitalWarningSignal`, `PhysiologyStateSignal`, and `SurvivalVitalsChangedSignal` publishers to `default` plus field assignment in `HectonPlayerHealth`, `HectonSurvivalSystem`, and `ShinobuPhysiologyRuntime`. Replace `SurvivalDatabaseItemRecord` with explicit 24-byte layout: `StableHash` offset 0, `MassKilograms` 4, `VolumeLiters` 8, `EnergyDensityMegajoulesPerKilogram` 12, `BaseDurability` 16, `_pad0` 20.
Rejected Alternatives: Leaving `Pack=1` because the row is cold; broad-refactoring readonly `TraumaHudSignal` constructors in the HUD event lane; changing the survival database parser contract; adding a new DTO type and forcing downstream copies.
Scalability potential: Low/Middle/High/Ultra unchanged. The fix removes alignment risk and noisy signal construction evidence without changing visual quality or survival tuning.
Hardware Impact: `SurvivalDatabaseItemRecord` grows from 20 to 24 bytes, a 20% cold staging-row increase for a 256-row cap, but removes unaligned ARM64 row reads. Mutable signal publish cost remains equivalent and cleaner for Burst/IL2CPP inspection.
First 20 Minutes Route Impact: The first lethal survival loop now has no mutable GlobalSignal object-initializer `new` in the health/survival death-adjacent publishers and no `Pack=1` row in the survival data parser used by the same component.

## Decision 26 - VisualSync Idle Publish And Registry-Poll Cull

Problem: `ShinobuRespawnReconciliationRuntime.VisualSyncTick()` published the respawn Dear Lie shader payload every VisualSync frame once the Vault row existed, even when `DeathFadeIntensity` was zero and the effect was inactive. The publish used the bridge's no-argument method, whose slot resolution starts from `GlobalRegistry.DataVault`; that hid a registry poll behind an otherwise cached SHINOBU VisualSync route.
Solution: Add `_respawnDearLieVisualActive` as a dirty latch. VisualSync now publishes while the fade scalar or active flag is live, then publishes exactly one zero payload to clear shader state and returns on later idle frames. Add `HectonShaderGlobalDataVaultBridge.PublishRespawnDearLie(IDataVault, Vector4)` and route SHINOBU through its cached `_dataVault`. The Core bridge now has `TryPrepareSlotsVault(IDataVault)` so caller-cached Vault routes reuse the same slot validation without entering `GlobalRegistry.DataVault`. The bridge now uses `default` field-assignment helpers for `float4` and `Vector4`, removing typed `new float4`/`new Vector4` from the whole file.
Rejected Alternatives: Leaving idle shader publishes in place; making Physiology write raw shader-global slot `19` directly; moving shader publication back into Gameplay; adding a sibling Rendering dependency; globally refactoring every shader bridge publisher in this SHINOBU pass.
Scalability potential: Low skips all idle Dear Lie bridge work after the final zero-clear and still exits visual cover fastest through `GlobalQualityWeight`. Middle/High/Ultra keep the same active-frame chromatic/grain path, but only pay for it while the effect is actually covering the teleport.
Hardware Impact: Steady-state respawn VisualSync cost drops to a branch and early return after the zero-clear. Death-frame active cost remains one cached-Vault bridge write plus shader/global-dispatch handoff. No measured microseconds are claimed until profiler proof; static cost class removes one hidden hot registry lookup and idle shader slot write from normal gameplay.
First 20 Minutes Route Impact: The first death recovery still blacks out immediately, but the post-recovery route no longer keeps touching shader-global state every frame during the resource collection -> danger -> recovery loop.

## Decision 27 - Hot Dispatcher Vault Allocation Gate

Problem: `PreSimulationTick`, `ScheduleSimulation`, `VisualSyncTick`, and fault-dump reads still called `EnsureVaultState(vault)`. That method is correct for cold boot because it requests Vault buffers, but using it as a dispatcher-phase guard creates a hidden allocation-capable route if boot order fails.
Solution: Add `HasHotVaultState(IDataVault)` as a pure `vault != null && AreVaultHandlesCreated()` check. Dispatcher phases, default hydration after a successful cold ensure, and telemetry dump reads now use this hot gate. Allocation-capable `EnsureVaultState(...)` remains restricted to Awake, Start, DataVault hot-swap rehydration, and editor/manual utilities.
Rejected Alternatives: Letting dispatcher ticks lazily allocate missing Vault buffers; throwing exceptions on missing handles; polling `GlobalRegistry.DataVault` from hot phases; adding private native fallback buffers.
Scalability potential: Low/Middle/High/Ultra unchanged in visible behavior. Weak devices avoid a surprise first-death allocation path; high/ultra still spend their budget on the Dear Lie shader only while active.
Hardware Impact: Hot path removes a cold allocation branch from every dispatcher phase. Expected steady-state saving is a few branches, not a measurable claim until profiler proof; the material gain is deterministic H-PHI ownership and no runtime buffer request from death/fade ticks.
First 20 Minutes Route Impact: The first death/recovery loop now either runs on boot-created Vault handles or fails closed without allocating, reloading, or fabricating owner-local buffers.

## Decision 28 - Reconciled Death Legacy Telemetry Ejection

Problem: `HectonPlayerHealth.Die()` published `GlobalTelemetryBus.PublishPlayerDeath()` before the respawn bridge, and `HectonSurvivalSystem.CheckLethalConditions()` called `RecordDeathTelemetry()` plus emitted a `SurvivalVitalsChangedSignalFlags.Death` UI/advisory signal before the bridge. The global telemetry bus can cold-initialize buffers and uses a legacy frame source; the survival recorder builds a human-readable log; the death vitals flag is a legacy presentation signal. None belong in the zero-GC reconciled death frame because SHINOBU already owns Vault black-box telemetry and Dear Lie presentation.
Solution: Move `GlobalTelemetryBus.PublishPlayerDeath()`, `SurvivalVitalsChangedSignalFlags.Death`, `RecordDeathTelemetry()`, `OnDeath`, and `PlayerDiedEvent` to unreconciled fallback only. Add a finite AUP gate to health death AUP resolution before emitting `PlayerRespawnSignal`; fallback runtime positions now use `default` field assignment instead of `new Vector3`.
Rejected Alternatives: Keeping legacy telemetry before the bridge; duplicating black-box death data into managed logs on successful reconciliation; emitting death UI/advisory signals during a successful mathematical respawn; emitting zero AUP from health when `CurrentAup` is non-finite; adding a direct dependency on telemetry from SHINOBU jobs.
Scalability potential: Low avoids surprise legacy telemetry/log work on the death frame. Middle/High/Ultra keep the same Vault black-box and Dear Lie shader route; richer post-death presentation must remain shader-side, not managed logging.
Hardware Impact: Removes a possible cold telemetry initialization and human-readable log construction from successful death reconciliation. Exact microseconds require profiler proof; static risk removed is a managed/global side route before the one-frame Vault reset.
First 20 Minutes Route Impact: Early oxygen/integrity death now either reconciles via SignalBus/Vault only or falls back to legacy death UX if the bridge fails.

## Decision 29 - Legacy Last-Loss Record Side-Route Ejection

Problem: `HectonSurvivalSystem.CheckLethalConditions()` still called `CaptureDeathRecord()` before `PlayerDeathReconciliationBridge.RequestRespawn(...)`. That records `_hasLastDeathRecord` and `_lastDeathRecord` even when reconciliation succeeds, and PDA/HUD consumers can read that last-loss state without `OnDeath` or `PlayerDiedEvent`.
Solution: Move `CaptureDeathRecord()` into the unreconciled fallback branch and clear `_hasLastDeathRecord` plus `_lastDeathRecord` during successful survival reconciliation. Successful mathematical death now leaves legacy last-loss UX state empty; SHINOBU Vault telemetry remains the authoritative forensic record.
Rejected Alternatives: Keeping a legacy last-death marker for reconciled deaths; publishing another UI signal to hide the marker; moving PDA/HUD consumers into SHINOBU; deleting `SurvivalDeathRecord` globally.
Scalability potential: Low avoids stale PDA/HUD last-loss work after the one-frame rebirth. Middle/High/Ultra keep post-death presentation in the Dear Lie shader path and use Vault black-box telemetry for forensic detail.
Hardware Impact: Removes one value-type record construction and downstream legacy last-loss visibility from successful reconciliation. No profiler number is claimed; the important gain is eliminating a non-authoritative UX side route.
First 20 Minutes Route Impact: Early oxygen/integrity death no longer leaves a fake "last loss" marker after a successful med-bay reconciliation.

## Decision 30 - Health Change Delegate Fan-Out Ejection

Problem: `ApplyRespawnReconciliationHealth()` still invoked `OnHealthChanged` after successful `PlayerRespawnSignal` emission. Source scan found no production subscriber, and any future runtime subscriber would create unmanaged-to-managed observer fan-out in the death frame.
Solution: Remove the respawn-only `OnHealthChanged` invocation. `MarkCombatDamageSyncDirty()` remains in the method, so combat health truth is still synchronized after reconciliation without managed observer callbacks.
Rejected Alternatives: Keeping the delegate because it currently has no source subscriber; adding a new HUD signal from Gameplay; moving health HUD correction into SHINOBU; changing normal damage/heal `OnHealthChanged` behavior outside the respawn path.
Scalability potential: Low avoids surprise delegate fan-out during the one-frame rebirth. Middle/High/Ultra keep visible feedback in shader Dear Lie and existing signal/HUD state routes.
Hardware Impact: Removes one managed delegate null-check/invocation point from successful health reconciliation. No profiler number is claimed; the important gain is preventing external subscriber side effects from entering the deterministic respawn path.
First 20 Minutes Route Impact: Early combat/survival death now reconciles health through combat sync and Vault state without dragging unmanaged death truth through an optional managed observer event.

## Decision 31 - Pre-Die Lethal Health Fan-Out Ejection

Problem: `TakeDamage()` and `Kill()` set `currentHealth` to zero and invoked managed health/damage observers before `Die()` attempted `PlayerDeathReconciliationBridge.RequestRespawn(...)`. A successful mathematical rebirth therefore still leaked `OnHealthChanged`, `OnDamageTaken`, vital warning, and zero-health combat sync side effects in the same frame.
Solution: Split health death into `TryApplyRespawnReconciliation(uint damageHash)` and `PublishLegacyDeathFallback()`. Lethal `TakeDamage()` and `Kill()` now attempt the SignalBus/Vault respawn route first. If it succeeds, `ApplyRespawnReconciliationHealth(1f)` restores health and exits without managed delegate fan-out. If it fails, the old callback order is preserved for legacy death: health/damage observers, combat sync, vital warning, telemetry, `OnDeath`, and debug log.
Rejected Alternatives: Keeping callbacks before death because they predated respawn; moving all health damage to SHINOBU; suppressing normal non-lethal damage callbacks; syncing a zero-health combat target before a guaranteed full-health reconciliation.
Scalability potential: Low avoids extra observer/UI/combat writes on the death frame. Middle/High/Ultra keep all visible rebirth feedback in the shader Dear Lie and Vault-driven state, while normal non-lethal combat feedback remains unchanged.
Hardware Impact: Removes managed delegate fan-out, vital warning publication, and zero-health combat target mutation from successful lethal health reconciliation. No microsecond claim without profiler; the static risk class is removed from the hottest death edge.
First 20 Minutes Route Impact: Early fauna/combat damage can trigger one-frame med-bay rebirth without a stale zero-health HUD/observer pulse preceding the Sweet Lie cover.

## Decision 32 - Post-Damage Trauma Fan-Out Ejection

Problem: `ReceiveDamage()` and `TakeLeviathanDamage()` call `TakeDamage()` and then continue normal post-damage presentation. After the pre-Die repair, a lethal `TakeDamage()` can successfully reconcile health back to full and return `true`, so the callers could still emit trauma HUD/advisory side effects after the med-bay rebirth was already accepted.
Solution: Add a private same-call `_lastDamageTriggeredRespawnReconciliation` flag. `TakeDamage()` clears it at entry and sets it only on successful `TryApplyRespawnReconciliation(...)`. `ReceiveDamage()` and `TakeLeviathanDamage()` immediately return when the flag is set, before trauma HUD or leviathan advisory fan-out.
Rejected Alternatives: Changing public `TakeDamage()` to return an enum; adding a managed event for respawn status; letting zero-intensity trauma HUD publishes through because current health was restored; pushing this suppression into Fauna or Combat sibling domains.
Scalability potential: Low avoids extra managed presentation writes on the death frame. Middle/High/Ultra keep normal non-lethal trauma feedback and spend death presentation budget only on the shader Dear Lie.
Hardware Impact: Removes one possible trauma HUD signal and leviathan advisory path from successful lethal reconciliation. No profiler microsecond claim; the important gain is preventing local managed post-damage presentation from contradicting one-frame Vault reconciliation.
First 20 Minutes Route Impact: Early combat/fauna death no longer raises a normal damage HUD pulse after the player has already been mathematically moved to the med-bay route.

## Decision 33 - Bridge Non-Finite AUP Fail-Closed

Problem: `PlayerDeathReconciliationBridge.RequestRespawn(...)` still converted a non-finite `deathAup` to `double3.zero`. Health and survival callers already finite-gate before calling the bridge, but the bridge seam itself should not fabricate a plausible origin AUP if a future caller violates the contract.
Solution: Return `false` immediately when `deathAup` is non-finite, before lane configuration or sequence mutation. Valid packets copy `deathAup` directly to `PlayerRespawnSignal.DeathAUP`.
Rejected Alternatives: Sanitizing to zero; pushing an invalid packet and relying on Core signal guards; adding a mock fallback AUP in Gameplay; widening the bridge into medical-bay selection.
Scalability potential: Low/Middle/High/Ultra unchanged. This is correctness hardening before the SignalBus route; visual scalability remains in fade/shader math.
Hardware Impact: Adds one finite check on the rare fatal request path and avoids downstream invalid/zero-origin reconciliation work. No profiler number claimed.
First 20 Minutes Route Impact: Early death with corrupted AUP now falls back to legacy death handling instead of silently reconciling at world origin.

## Decision 34 - Cold Layout Guard Activation

Problem: `ShinobuRespawnLayoutGuards.ValidateRespawnLayouts()` defined the ARM64/offset proof but was not called. That made the guard a static artifact instead of an executable boot-time fail-closed check.
Solution: Call the guard from cold `EnsureVaultState(IDataVault)` after "handles already created" short-circuit and before any Vault buffer request. If a DTO size/offset changes, SHINOBU refuses to allocate its handles and hot dispatcher phases fail closed through `HasHotVaultState()`.
Rejected Alternatives: Running reflection-backed layout validation in PreSimulation or VisualSync; trusting docs only; expanding validation into massive Core signal guards; allocating handles and discovering mismatches later in jobs.
Scalability potential: Low/Middle/High/Ultra unchanged. Layout proof is cold boot validation; the runtime quality continuum still lives in fade/shader math.
Hardware Impact: Adds cold boot reflection/UnsafeUtility offset checks only when handles are not created. Hot death path cost remains 0 us.
First 20 Minutes Route Impact: The first death/recovery loop cannot run on unverified respawn DTO layouts after a future field edit.

## Decision 35 - Respawn Dear Lie Binary Shader Branch Removal

Problem: `H8UberNoirApplyRespawnDearLie` still used an `_MATH_LOD_LOW` compile-time branch. That violated the SHINOBU death-mask requirement: the respawn visual fake must breathe with `GlobalQualityWeight`, not jump between low and non-low shader bodies.
Solution: Replace the respawn Dear Lie branch body with a continuous `detailWeight = H8UberNoirSmoothRange01(0.18, 0.72, quality) * H8UberNoirHighCostAllowed()`. Screen-cell frequency, grain amplitude, chromatic bias, and abyss tint now scale through that value. At low weight the function collapses toward cheap blackout with almost no chroma/grain detail; at high weight it restores higher-frequency screen grain and stronger chromatic cover.
Rejected Alternatives: Keeping `_MATH_LOD_LOW` because it is "only shader code"; rewriting unrelated UberNoir LOD branches in this SHINOBU pass; moving death cover back to a UI overlay; simulating camera travel to hide the teleport.
Scalability potential: Low uses blackout plus nearly suppressed detail for the cheapest mask. Middle introduces moderate grain/chroma without changing CPU work. High and Ultra spend saved CPU on richer shader-side optical cover while the authoritative player death remains a one-frame Vault/AUP numeric reset.
Hardware Impact: CPU impact is unchanged: one VisualSync shader payload publish while active. GPU cost now scales continuously by `detailWeight` instead of a compile-time split. No frame-time number is claimed until Frame Debugger/profiler proof; static risk removed is the binary low/high branch inside the respawn mask.
First 20 Minutes Route Impact: The first death/recovery screen cover no longer pops between low and high mask logic when quality weight changes under thermal pressure.

## Decision 36 - Death AUP Compile-Wall Import Repair

Problem: The polished death route still exposed `AbsoluteUniversePosition` in health/survival producer methods and kept `Hecton8.World` imports in already-touched death-adjacent files. That weakened the Compile Wall proof and left a runtime-position AUP fabrication fallback in survival death handling.
Solution: Convert health and survival death AUP producers to emit only finite `double3` absolute AUP values obtained from `HectonPlayerMovement.CurrentAup` or `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot(...)`. Remove the survival runtime-position AUP fallback for death packets; if movement/snapshot AUP is unavailable or non-finite, reconciliation fails closed and legacy death handling runs. Add a `double3` absolute-point overload to the existing `HectonHazardManager` compatibility bridge so survival hazard queries keep AUP precision without importing `Hecton8.World`. Remove the explicit World import from `ShinobuPhysiologyRuntime` by consuming `snapshot.Aup` through the Core pose contract and member access.
Rejected Alternatives: Keeping explicit `AbsoluteUniversePosition` in producer signatures; reconstructing death AUP from `Transform.position`; moving HazardZoneManager references into survival; adding a direct Physiology/World dependency; broad-refactoring `PlayerStressMetricsRuntime`, whose acoustic AUP model is outside SHINOBU_155 death reconciliation ownership.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The route still uses the same finite AUP truth and continuous shader Dear Lie; the change reduces compile coupling and rejects bad death coordinates instead of fabricating a low-cost but wrong origin packet.
Hardware Impact: Runtime cost is neutral on successful death requests: existing AUP conversion still happens once, now surfaced as `double3` at the producer seam. Low-end benefit is compile-wall and failure-mode hygiene, not a measured frame-time gain. Hazard queries preserve AUP precision by converting inside the existing compatibility bridge instead of truncating to runtime `Vector3`.
First 20 Minutes Route Impact: Early oxygen/combat death now cannot enter the reconciliation lane from a synthetic runtime-position AUP if player AUP authority is missing; it fails closed to legacy death handling instead of teleporting from a questionable coordinate.

## Decision 37 - Survival Scalar Burst Layout Tightening

Problem: `SurvivalPhysiologyScalarJob` was a death-adjacent survival sidecar still using Burst `FloatMode.Fast`/`FloatPrecision.Low`, an implicit `SurvivalPhysiologyScalarResult` layout with a byte tail, object-initializer job construction, `new NativeSlice<SurvivalPhysiologyScalarResult>`, and ClearMemory allocation for a fully overwritten one-row result. That weakened ARM64 layout, rollback determinism, and hot-path proof even though the main respawn lane was already hardened.
Solution: Make `SurvivalPhysiologyScalarResult` explicit 32 bytes with offsets `0/4/8/12/16/17/18/20/24`, change the job to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`, write through `[NoAlias] NativeArray<SurvivalPhysiologyScalarResult>`, build the caller job and result row through `default` field assignment, and request the one-row Vault result with `NativeArrayOptions.UninitializedMemory`.
Rejected Alternatives: Scheduling a one-row scalar job through the dispatcher for architectural theater; keeping `NativeSlice` construction; relying on implicit layout; accepting Low precision Burst near death physiology; zero-filling a row that is overwritten before read.
Scalability potential: Low/Middle/High/Ultra visible behavior is unchanged. Low hardware benefits from fewer hidden hot constructs and no unnecessary result zero-fill; high and ultra retain the same physiology scalar truth while shader Dear Lie remains the place where saved CPU budget is spent visually.
Hardware Impact: No profiler number claimed. Static cost removed: one hot job object initializer, one `NativeSlice` construction, a ClearMemory zero-fill for the scalar result buffer, and an implicit 20-byte-ish row risk. The job intentionally remains `Run()` because it writes one scalar row; scheduling would add more overhead than work on weak silicon.
First 20 Minutes Route Impact: The early oxygen/pressure/decompression death loop now has a deterministic, aligned scalar physiology sidecar feeding the same survival system that triggers SHINOBU death reconciliation.

## Decision 38 - Physiology VisualSync Vector Payload Tightening

Problem: `ShinobuPhysiologyRuntime.PublishVisualSyncScalars()` still constructed `new Vector4(...)` for the decompression shader scalar payload. It is visual-only, but it runs in a runtime VisualSync publisher in the same physiology domain and weakens the no-constructor hot-path proof already enforced on the respawn Dear Lie payload.
Solution: Build the decompression shader payload as `Vector4 payload = default` with explicit `x/y/z/w` assignment before calling `HectonShaderGlobalDataVaultBridge.PublishPhysiologyDecompression(payload)`.
Rejected Alternatives: Ignoring the constructor because `Vector4` is a value type; pushing raw shader globals from physiology; changing the bridge API; widening the pass into unrelated shader-global publishers.
Scalability potential: Low/Middle/High/Ultra visuals are unchanged. The scalar still carries bends risk, narcosis severity, ambient pressure, and continuous `GlobalQualityWeight`; this change only removes constructor noise from the VisualSync path.
Hardware Impact: No profiler number claimed. Static cost removed is a value-constructor callsite in a runtime VisualSync payload path; the real gain is consistent proof that physiology shader scalar publishers use field assignment and cached bridge routes.
First 20 Minutes Route Impact: Decompression/narcosis visual feedback during the first pressure danger loop remains shader-side and does not reintroduce standard Unity transition patterns.

## Decision 39 - Survival Scalar Executable Layout Guard

Problem: `SurvivalPhysiologyScalarResult` had explicit 32-byte layout and documentation proof, but the cold allocation route did not execute a guard before creating the Vault buffer. A future field edit could drift the row and still allocate a hot result buffer.
Solution: Add `ValidateSurvivalPhysiologyScalarResultLayout()` to `HectonSurvivalSystem` and call it inside `TryResolvePhysiologyScalarBuffer()` before `vault.GetBufferHandle<SurvivalPhysiologyScalarResult>(...)`. The guard checks `UnsafeUtility.SizeOf` and offsets `0/4/8/12/16/17/18/20/24`; `FieldOffset<T>` returns `-1` on missing fields so drift fails closed without fabricating a valid handle.
Rejected Alternatives: Trusting docs only; running reflection/offset validation every dispatcher tick; throwing an exception from the death/survival frame; moving the scalar DTO into SHINOBU respawn contracts and widening compile surface.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Weak devices pay zero steady-state cost after handle creation; high-tier visuals still consume scalar physiology through the shader bridge.
Hardware Impact: Cold first-handle path pays several reflection/UnsafeUtility offset checks. Hot path after handle creation pays no guard cost beyond the existing handle-created branch. This prevents ARM64 row drift from reaching gameplay memory.
First 20 Minutes Route Impact: The first survival scalar buffer used by oxygen/pressure/decompression death checks now refuses creation on bad layout instead of silently feeding a misaligned row into death reconciliation.

## Decision 40 - Verification Mirror And Scan Path Correction

Problem: The first focused verification pass after compaction used a stale shader bridge path (`Core/Rendering/HectonShaderGlobalDataVaultBridge.cs`), which produced an `rg` path error unrelated to source correctness. Active and archived SHINOBU logs also needed proof that they had not drifted after the latest doc writes.
Solution: Locate the live bridge with `rg --files`, rerun the forbidden/import scans against `Assets/_Project/Scripts/Rendering/HectonShaderGlobalDataVaultBridge.cs`, hash-compare active `Status/Route/Rationale/LOG` with `Docs/Archive/Batch010` mirrors, inspect the physiology asmdef, and sample CPU/compiler guard without launching build.
Rejected Alternatives: Treating a bad scan path as a code defect; claiming scan success while one input file failed; copying active logs to archive without equality proof; launching `dotnet build` just because CPU was below 50 when the user had not asked for compile proof in this pass.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. This is evidence hygiene: the same continuous `GlobalQualityWeight` shader route and one-frame Vault reset remain intact.
Hardware Impact: Runtime impact 0 us. Workstation impact is limited to static file scans and hash reads; build was not launched.
First 20 Minutes Route Impact: Protects the early death/rebirth route proof from stale verification artifacts and confirms the active/archived forensic trail points at the same implementation facts.

## Decision 41 - Telemetry Dump Fence Repair

Problem: `PostSimulationTick()` attempted a non-blocking `CompleteActiveJobIfReady(false)` and then always called `TryDumpFaultedTelemetry()`. If `_activeHandle` was still running, the dump path could read `RespawnTelemetryCursor64` while `ResetPlayerPhysiologyJob.WriteTelemetry()` was writing the same cursor and ring.
Solution: Add `_jobScheduled` guards after the non-blocking reclaim, inside `TryDumpFaultedTelemetry()`, and inside `TryDumpTelemetry(...)`. The fault dump now waits for the already scheduled job to finish through the existing non-blocking fence path; it does not force a dispatcher-phase `Complete()`.
Rejected Alternatives: Unconditional `JobHandle.Complete()` in PostSimulation; duplicating telemetry cursor into a managed field; ignoring the race because dumps are rare; moving dump I/O into the Burst job.
Scalability potential: Low avoids a main-thread stall and a data race on weak hardware. Middle/High/Ultra keep the same 300-frame black-box fidelity; the Dear Lie visual mask remains shader-side and quality-weighted.
Hardware Impact: Hot path cost is one `_jobScheduled` branch before dump reads. It removes a possible NativeArray read/write race without adding allocations or a scheduler block.
First 20 Minutes Route Impact: Early death with an invalid med-bay target now dumps only after the reconciliation job owns a completed telemetry cursor, so the black-box file cannot capture a half-written respawn forensic row.

## Decision 42 - Editor Facade Fence Repair

Problem: The editor facade was cold, but in Play Mode `TryReadEditorState()`, `TryWriteEditorTuning()`, `TryReloadPenaltyCsvFromEditor()`, and `TryDumpBlackBoxForEditor()` could touch `RespawnFadeDTO`, `RespawnTuningDTO`, penalty rule rows, or telemetry rows while `ResetPlayerPhysiologyJob`/`UpdateRespawnFadeJob` still owned those Vault buffers.
Solution: Add `TryPrepareEditorVaultAccess()`. It uses the same non-blocking reclaim path (`CompleteActiveJobIfReady(false)`) and returns false if `_jobScheduled` is still true. Editor read/write/reload/dump routes now fail closed during an active job rather than forcing a completion or racing the job.
Rejected Alternatives: Force `Complete()` from editor UI; keep the race because editor-only code is "not gameplay"; copy tuning/fade into managed shadow state; lock Vault buffers from editor while jobs hold raw pointers.
Scalability potential: Low avoids a main-thread stall on weak hardware while a designer has the tuner open. Middle/High/Ultra keep live tuning responsiveness whenever the job fence is already clear.
Hardware Impact: Editor-facing cost is one non-blocking branch and possible already-completed reclaim. Runtime dispatcher path unchanged; no new allocation or private state buffer.
First 20 Minutes Route Impact: Designers can tune the first death/rebirth loop without corrupting active fade/tuning/penalty rows during a live med-bay reconciliation.

## Decision 43 - Full Respawn Layout Guard Expansion

Problem: `ShinobuRespawnLayoutGuards.ValidateRespawnLayouts()` checked all DTO sizes but only a subset of offsets. An explicit-layout struct can keep the same total size while a field offset drifts, which would break AUP, flags, tuning, or telemetry interpretation on ARM64 without tripping a size-only guard.
Solution: Split validation into per-DTO layout guard functions and check every field offset for `RespawnStateDTO`, `RespawnRequestDTO`, `MedicalBayRespawnPointDTO`, `RespawnFadeDTO`, `RespawnTuningDTO`, `InventoryDeathPenaltyRuleDTO`, `InventoryCommandSignal`, `RespawnTelemetryEntry`, and `RespawnTelemetryCursor64`. `OffsetOf<T>()` now returns `-1` if reflection cannot find a field, so renamed or removed fields fail closed.
Rejected Alternatives: Trusting `[FieldOffset]` attributes by inspection; relying on total size only; moving the guard into hot dispatcher phases; using `Pack=1` or implicit layout.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged. Weak devices pay zero hot cost because validation runs only before first Vault handle allocation; high-tier visuals remain shader-side.
Hardware Impact: Cold boot does more reflection offset checks. Hot path cost remains 0 us after handles exist. The gain is preventing silent ARM64/rollback row drift.
First 20 Minutes Route Impact: The first death/rebirth loop cannot allocate respawn Vault buffers if any field offset in the numeric rebirth route drifts.

## Decision 44 - Internal AUP Zero-Origin Fallback Purge

Problem: The gameplay bridge failed closed on non-finite death AUP, but internal SHINOBU sanitizers still had final fallback routes to world origin through `double3.zero` and telemetry `default` arguments. A corrupted fallback tuning row or forensic write could therefore produce a plausible origin coordinate instead of an explicit med-bay fallback.
Solution: Add explicit `DefaultFallbackAup()` helpers returning mock lifepod AUP `(0,-18,0)` and use them in mock med-bay generation, reset-job tuning sanitation, runtime default tuning, runtime signal sanitation, and telemetry writes. The bridge still rejects non-finite producer AUP before SignalBus emission; this patch only removes zero-origin fallback inside SHINOBU-owned reconciliation and black-box rows.
Rejected Alternatives: Keeping `(0,0,0)` because it is finite; throwing from Burst jobs on corrupted AUP; fabricating a runtime `Transform.position`; adding direct Base Logistics dependency before that graph exists.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The same one-frame numeric reset runs on every tier; the visual tiering remains in the Dear Lie shader scalar and fade rate.
Hardware Impact: No profiler number claimed. Runtime cost is a few constant fallback assignments on rare sanitation paths; removed risk is an invalid origin teleport/telemetry coordinate that would poison AUP debugging and QA black-box replay.
First 20 Minutes Route Impact: Early death during missing/corrupted med-bay tuning now lands on the deterministic mock lifepod fallback rather than a silent world-origin coordinate.

## Decision 45 - PreSimulation Writer Fence Repair

Problem: `PreSimulationTick()` wrote `RespawnRequestDTO` and `RespawnStateDTO` directly from the `PlayerRespawnSignal` snapshot, but it did not check the active reset/fade job fence. If the prior scheduled job had not completed, PreSimulation could race the same Vault rows that `ResetPlayerPhysiologyJob` or `UpdateRespawnFadeJob` still owned.
Solution: Add the same non-blocking `_jobScheduled`/`_activeHandle.IsCompleted` gate used by Simulation and VisualSync before reading the signal snapshot. If the prior job is incomplete, PreSimulation returns and does not write the rows; if it is complete, it reclaims the handle without a forced stall and then processes the snapshot.
Rejected Alternatives: Forcing `Complete()` in PreSimulation; adding a managed pending-death queue; allowing overlapping writers because repeated death during active fade is rare; moving request staging into Gameplay.
Scalability potential: Low avoids a main-thread stall and a NativeArray race on weak hardware. Middle/High/Ultra keep the same signal route and shader Dear Lie; repeated lethal packets during an active reconciliation are dropped rather than corrupting Vault truth.
Hardware Impact: Hot path adds one branch only while a job is scheduled. The removed risk is a read/write race on `RespawnRequestDTO`/`RespawnStateDTO`; no private buffers or allocations were added.
First 20 Minutes Route Impact: Repeated oxygen/combat lethal pulses during the first respawn fade cannot overwrite active reconciliation rows before the previous job fence clears.

## Decision 46 - Consumer Signal AUP Fail-Closed

Problem: Producer-side bridge and Core finite guards reject malformed death AUP, but `WriteRequestFromSignal()` still sanitized `signal.DeathAUP` to a lifepod fallback. A future producer or test harness bypassing the bridge could therefore create a valid-looking respawn request from a corrupted signal.
Solution: Add a consumer-side `math.isfinite(signal.DeathAUP)` check before med-bay resolution and request/state writes. Valid signals copy the death AUP directly into `RespawnRequestDTO`; invalid signals are dropped and leave existing Vault state untouched. The committed snapshot transformer now falls back to explicit lifepod AUP if its resolved target ever becomes non-finite, rather than reusing death AUP as a target.
Rejected Alternatives: Sanitizing malformed death AUP to lifepod; relying only on Core signal sanitation; throwing in the dispatcher phase; writing a telemetry row for a packet SHINOBU refuses to own.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged for valid packets. Invalid packets now cost one finite check and do not trigger shader/physics/inventory side effects.
Hardware Impact: Hot signal path adds one vector finite check per respawn packet. Removed risk is a corrupted AUP becoming a deterministic but false med-bay reconciliation record.
First 20 Minutes Route Impact: Early respawn tests or external producers cannot accidentally route bad coordinates into the first death/rebirth loop.

## Decision 47 - Core Invalid Death AUP Flag

Problem: Core `SanitizePlayerRespawnSignal()` still used the generic `SanitizeDouble3Zero` helper on `DeathAUP`. After that sanitation, SHINOBU's consumer could see a finite zero AUP and could not prove whether it came from a real origin death or a non-finite producer packet.
Solution: Add `PlayerRespawnSignalFlags.InvalidDeathAup` to the existing 32-bit flag field, set it when Core sanitizes `DeathAUP`, and make SHINOBU reject that flag before med-bay resolution or Vault row writes. The flag is contract-level because the information is destroyed at the Core sanitizer boundary.
Rejected Alternatives: Rejecting every zero death AUP; widening the payload layout; encoding invalidity in `Phase`; adding a sibling callback; trusting future producers to always use the gameplay bridge.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Invalid packets are dropped after one bit test, so weak devices do not spend med-bay scan, inventory, physics, or shader work on corrupted signals.
Hardware Impact: Hot request path adds one flag test. No payload size change, no new allocation, no new sibling dependency. Compile surface is limited to the existing Core contract and Core signal sanitizer.
First 20 Minutes Route Impact: Bad coordinates in early automated respawn tests cannot be laundered into zero-origin death packets by Core sanitation.

## Decision 48 - External Respawn Consumer Invalid-AUP Gate

Problem: KCC and Mesofauna are legitimate direct consumers of the contract respawn snapshot. After Core marks `InvalidDeathAup`, those consumers could still see the packet and apply collision bypass or aggro reset before SHINOBU dropped the malformed request.
Solution: Add the same `PlayerRespawnSignalFlags.InvalidDeathAup` fail-closed test to `HydrodynamicKccRuntime.ConsumeRespawnCollisionSuspendSignals()` and `PredatorCognitionDomain.ProcessMesofaunaRespawnSignals()` before any external side effect is applied.
Rejected Alternatives: Letting external consumers trust only `Requested`/`Committed`; moving all consumers behind a direct Physiology callback; widening the signal payload; adding a managed invalid-packet notification. The contract flag is the smallest route because Core already owns sanitation evidence and the side-effect consumers already read the lane.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Invalid packets cost one bit test and avoid KCC capsulecast suppression, predator state edits, med-bay scan, inventory command, and shader work.
Hardware Impact: Added cost is one flag test per respawn snapshot row in the two external consumers. Removed risk is a corrupted packet causing a collision-free frame or aggro wipe without an accepted Vault reconciliation.
First 20 Minutes Route Impact: Automated early death tests with malformed AUP cannot silently grant collision bypass or clear predators when the rebirth packet is invalid.

## Decision 49 - Malformed Packet Vault Resolve Bypass

Problem: `WriteRequestFromSignal()` rejected `InvalidDeathAup` before med-bay resolution, but only after resolving the request/state Vault arrays. A malformed packet should not enter even cheap Vault-resolve work.
Solution: Move the `InvalidDeathAup` and `math.isfinite(signal.DeathAUP)` guard to the top of `WriteRequestFromSignal()`, before `_requestHandle.Resolve(vault)` and `_stateHandle.Resolve(vault)`.
Rejected Alternatives: Treating handle resolve as harmless; recording rejected packets in SHINOBU telemetry; relying on external consumers to ignore malformed packets. The consumer owner should fail closed before touching Vault rows.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Invalid packets now collapse to one bit test plus one finite check and skip Vault resolve, med-bay scan, state write, snapshot transform, shader activation, and inventory side effects.
Hardware Impact: Valid packets unchanged. Invalid packet path removes two Vault handle resolves and all downstream reconciliation work. No new memory or dependency edge.
First 20 Minutes Route Impact: Malformed early respawn tests cannot touch SHINOBU request/state Vault rows before rejection.

## Decision 50 - Route Card Invalid-AUP Contract Update

Problem: The route card still described finite guard and collision suspend broadly but did not document the new `InvalidDeathAup` preservation/fail-closed contract across Core, Physiology, KCC, and Mesofauna.
Solution: Update `Route_SHINOBU_155_Respawn.md` consumer phase, expected events, physics suspend, payload shape, and failure mode sections with the exact invalid-AUP behavior.
Rejected Alternatives: Leaving the route card stale and relying on status/log history; over-editing global architecture docs. This is a SHINOBU-owned route card correction only.
Scalability potential: Runtime behavior unchanged. The documentation now makes clear that invalid packets collapse before Vault/AI/physics/shader work on every tier.
Hardware Impact: Documentation-only. Runtime impact 0 us.
First 20 Minutes Route Impact: Integrators can see that malformed early death packets do not grant any rebirth side effect.

## Decision 51 - Contract Layout And Snapshot Transform Guard

Problem: The cold layout guard proved SHINOBU-owned DTO offsets, but not the `PlayerRespawnSignal` contract offsets. The same-sequence snapshot transformer also assumed `WriteRequestFromSignal()` was the only way to reach transformation, leaving a narrow future bug where a malformed duplicate packet could be committed if the caller path changed.
Solution: Add `ValidatePlayerRespawnSignalLayout()` to the same cold guard that runs before Vault handle allocation. It now validates the 128-byte signal and all offsets carrying `DeathAUP`, `RespawnAUP`, hashes, frame, sequence, flags, phase, collision frames, and tail padding through `Reserved7=120`. Add a transformer-side fail-closed check for `InvalidDeathAup` and non-finite `DeathAUP` before mutating the snapshot.
Rejected Alternatives: Trusting Core `ValidateSignalSize<PlayerRespawnSignal>(128)` as sufficient without exact offsets; relying on docs for offsets; allowing the transformer to remain sequence-only; widening the signal payload; moving this proof into global Core.
Scalability potential: Low/Middle/High/Ultra valid behavior is unchanged. The invalid path collapses to a flag/finite check before snapshot mutation; the Dear Lie shader route still scales by continuous `GlobalQualityWeight`.
Hardware Impact: Cold boot adds one signal offset guard batch before SHINOBU Vault allocation. Hot valid path adds no new work before transformation beyond checks inside the rare death-frame transformer. Invalid duplicate packets avoid committed snapshot side effects entirely.
First 20 Minutes Route Impact: Early death/rebirth tests cannot pass a layout-drifted respawn packet into Vault allocation, and malformed same-sequence packets cannot be upgraded into committed rebirth state.

## Decision 52 - Respawn Flag Collision Guard

Problem: `InvalidDeathAup` now carries destroyed evidence across Core sanitation and external consumers. The layout guard proved the signal row offsets but did not prove that the invalid bit remains unique and does not collide with request, commit, collision suspend, fallback, target-invalid, or penalty flags.
Solution: Add `ValidatePlayerRespawnSignalFlags()` to the cold respawn guard. It verifies every `PlayerRespawnSignalFlags` constant from `Requested` through `InvalidDeathAup` is exactly `1u << 0` through `1u << 7`, and the accepted mask equals `0xFF`.
Rejected Alternatives: Adding a second invalidity byte; widening `PlayerRespawnSignal`; trusting code review to catch bit drift; moving the check into Core hot signal sanitation. The existing flags field is the contract owner, and cold guard proof is enough for SHINOBU Vault admission.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Invalid packets remain a constant bit-test fail-closed path; the Dear Lie shader still scales by continuous `GlobalQualityWeight`.
Hardware Impact: Cold boot adds constant comparisons before Vault handle allocation. Hot path adds 0 us. The gain is preventing a future flag collision from granting physics/AI/shader side effects to a malformed rebirth packet.
First 20 Minutes Route Impact: The first death/rebirth loop cannot accept a contract where `InvalidDeathAup` overlaps an accepted respawn side-effect flag.

## Decision 53 - Single Accepted Respawn Request Per Snapshot

Problem: `PreSimulationTick()` consumed the whole `PlayerRespawnSignal` snapshot and could write `RespawnRequestDTO`/`RespawnStateDTO` more than once if health, survival, or tests emitted different valid sequences in the same frame. That creates "last writer wins" on a single-row Vault truth buffer before Simulation owns the reset.
Solution: Make `WriteRequestFromSignal()` return `true` only after it writes request/state rows and transforms the snapshot. `PreSimulationTick()` returns immediately after the first accepted packet. Invalid packets and unresolved Vault rows return `false`, so a later valid packet in the same snapshot can still be accepted.
Rejected Alternatives: Adding a managed queue; expanding request/state to multiple rows; accepting repeated same-frame overwrites; dropping the whole snapshot after the first invalid packet. The lane can broadcast multiple rows, but SHINOBU's owner-local Vault admission is one current rebirth fact.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Burst reset, KCC bypass, and Dear Lie shader still run once for the accepted death packet; duplicate producer chatter no longer scales med-bay search or Vault writes.
Hardware Impact: Valid primary packet adds one bool branch and returns from the loop. Duplicate same-snapshot valid packets skip additional med-bay scans, request/state writes, and snapshot transforms. No allocation or job dependency is added.
First 20 Minutes Route Impact: Simultaneous oxygen/combat lethal triggers during the first survival loop cannot overwrite the accepted med-bay target inside the same PreSimulation snapshot.

## Decision 54 - Core Respawn Phase Flag Normalization

Problem: `SanitizePlayerRespawnSignal()` repaired invalid phase values but did not add the matching flag when a packet arrived with a valid `Request` or `Committed` phase and a missing `Requested` or `Committed` flag. That makes consumers choose between phase truth and flag truth.
Solution: Keep the existing invalid-phase fallback, then add branch repair for valid phase values: `Request` sets `Requested`, `Committed` sets `Committed`. The sanitizer remains the one owner of this contract repair.
Rejected Alternatives: Forcing Physiology, KCC, and Mesofauna to duplicate the repair; rejecting phase-only packets in SHINOBU; widening the payload; adding a second signal. Core already owns finite/phase/collision-frame sanitation for this lane.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed phase/flag packets pay two bit tests in sanitation and then follow the normal single accepted request path.
Hardware Impact: Existing sanitizer gains constant branch work only for malformed packets. No payload, allocation, Vault handle, job, or sibling dependency is added.
First 20 Minutes Route Impact: Early death/rebirth tests cannot produce a phase-only request that some consumers treat as a respawn and others treat as flagless noise.

## Decision 55 - Request-Only Vault Admission

Problem: `PreSimulationTick()` accepted packets when `Phase == Request` or the `Requested` flag was present. A malformed or externally emitted `Committed` packet carrying `Requested` could therefore enter SHINOBU's request/state Vault truth even though committed packets are produced by SHINOBU after med-bay resolution.
Solution: Add `IsAdmissibleRequestSignal(in PlayerRespawnSignal)` and use it in both `PreSimulationTick()` and `WriteRequestFromSignal()`. The predicate admits only `Phase == Request` packets with no `Committed` flag. `Committed` phase and `Committed`-flag packets remain visible to KCC/Mesofauna as output side-effect facts but cannot create a new Vault request.
Rejected Alternatives: Trusting phase/flag normalization alone; accepting request+commit packets as harmless; widening the signal with an owner byte; adding a managed pending queue. The existing phase/flag contract is sufficient if the owner admission gate is fail-closed.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed committed packets collapse to two scalar tests and skip Vault resolve, med-bay search, job scheduling, inventory command, and shader activation.
Hardware Impact: Hot request scan adds one static helper call that the compiler can inline; malformed committed packets save downstream work. No allocation, new Vault handle, or JobHandle edge is added.
First 20 Minutes Route Impact: A same-frame committed respawn snapshot cannot be re-ingested as a second death request during the first death/rebirth loop.

## Decision 56 - Requested-Flag Admission And Phase Guard

Problem: The request-only predicate still trusted `PlayerRespawnSignalPhase.Request` without requiring `PlayerRespawnSignalFlags.Requested`. Core repairs valid phase-only packets, but a bypassing producer or test harness could still reach Physiology with a phase-only request and enter the single-row Vault truth.
Solution: Tighten `IsAdmissibleRequestSignal(in PlayerRespawnSignal)` so admission requires `Phase == Request`, `Requested` flag present, and no `Committed` flag. Add `ValidatePlayerRespawnSignalPhase()` to the cold respawn layout guard so `Request=1` and `Committed=2` are executable constants before Vault handle allocation.
Rejected Alternatives: Accepting phase-only requests as valid; duplicating Core sanitizer logic in every consumer; widening `PlayerRespawnSignal`; adding a managed owner token. Core remains the repair owner, and Physiology remains the fail-closed Vault admission owner.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed phase-only packets collapse to three scalar tests and skip Vault resolve, med-bay search, Burst scheduling, inventory command emission, and Dear Lie shader activation.
Hardware Impact: Hot request scan adds one bit test that should inline with the existing predicate. Cold boot gains two constant comparisons in the guard. No allocation, new Vault handle, direct sibling reference, or JobHandle edge is added.
First 20 Minutes Route Impact: Early death/rebirth tests that bypass Core cannot create a phase-only respawn request; only normalized request facts reach the med-bay reconciliation row.

## Decision 57 - Zero Sequence Admission Rejection

Problem: `PlayerDeathReconciliationBridge` skips sequence zero on wrap and `_lastRequestSequence` starts at zero, but the owner gate did not explicitly reject zero. After any accepted nonzero request, a bypassing producer could emit `Sequence == 0` and pass the duplicate check.
Solution: Add `signal.Sequence != 0u` to `IsAdmissibleRequestSignal(in PlayerRespawnSignal)`, which is shared by `PreSimulationTick()` and `WriteRequestFromSignal()`. Zero remains a malformed/sentinel sequence that never reaches the request/state Vault write seam.
Rejected Alternatives: Relying on `_lastRequestSequence` default; checking zero only in `PreSimulationTick()`; widening the signal with another validity byte; changing the producer counter contract. The existing private predicate is the single owner admission seam.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed zero-sequence packets collapse to scalar checks and skip Vault resolve, med-bay search, snapshot transform, Burst scheduling, inventory command, and shader activation.
Hardware Impact: Hot request scan adds one integer comparison. No allocation, no new Vault handle, no direct sibling reference, and no JobHandle change.
First 20 Minutes Route Impact: Early test harness packets or external producers cannot replay sequence zero into the first death/rebirth loop after a valid prior death.

## Decision 58 - External Zero Sequence Side-Effect Rejection

Problem: KCC collision suspend and Mesofauna aggro reset consume `PlayerRespawnSignal` snapshots directly. After Physiology rejected zero-sequence packets, those external consumers could still treat the same malformed packet as a respawn side-effect fact.
Solution: Add `signal.Sequence == 0u` rejection to `HydrodynamicKccRuntime.ConsumeRespawnCollisionSuspendSignals()` and `PredatorCognitionDomain.ProcessMesofaunaRespawnSignals()` before any collision bypass or target reset. This mirrors the owner admission contract without adding a direct Physiology dependency.
Rejected Alternatives: Trusting Physiology rejection to protect external consumers; routing KCC/Mesofauna through a Physiology callback; widening the signal; ignoring the seam because the gameplay bridge never emits zero. Contract consumers must fail closed against bypass packets.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed zero-sequence packets now collapse to one integer check in external consumers and cannot clear predator targets or suppress capsule casts.
Hardware Impact: Adds one integer comparison per respawn snapshot row in two existing consumers. It can save a capsulecast skip side-effect and predator loop mutation on malformed packets; no allocation, Vault handle, or JobHandle edge is introduced.
First 20 Minutes Route Impact: Early malformed test packets cannot produce collision-free frames or aggro wipes unless the owner rebirth packet has a valid nonzero sequence.

## Decision 59 - External Coherent Phase Flag Gate

Problem: KCC and Mesofauna still accepted respawn side effects through broad `phase OR flag` logic. That allows phase-only or flag-only malformed packets to clear predator aggro or trigger collision bypass even when Physiology rejects them at the owner admission gate.
Solution: In both external consumers, compute `requestPacket` only when `Phase == Request` and `Requested` is present, and `committedPacket` only when `Phase == Committed` and `Committed` is present. Side effects run only for those coherent packet states, plus existing nonzero sequence and invalid-AUP gates.
Rejected Alternatives: Trusting Core normalization as the only guard; routing external consumers through Physiology; adding a new signal; accepting flag-only packets because they are rare. Any direct contract consumer must fail closed when the packet is not internally coherent.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed phase/flag packets collapse to scalar bit tests and cannot spend KCC or Mesofauna side-effect work.
Hardware Impact: Adds two phase comparisons and two bit tests inside existing rare respawn snapshot loops. No allocation, no Vault handle, no JobHandle edge, and no new assembly reference.
First 20 Minutes Route Impact: Automated early death tests cannot cause collision-free frames or predator target resets unless the respawn packet is coherent enough for the owner rebirth route.

## Decision 60 - PlayerRespawnSignal 128-Byte Proof Repair

Problem: Current source truth defines `PlayerRespawnSignal` as `[StructLayout(LayoutKind.Explicit, Size = 128)]` and Core validates `ValidateSignalSize<PlayerRespawnSignal>(128)`, but SHINOBU's route/proof text still carried stale pre-repair audit language. The cold layout guard also checked only through `Reserved3=88`, leaving the tail padding lanes unproven.
Solution: Extend `ValidatePlayerRespawnSignalLayout()` to validate `Reserved4=96`, `Reserved5=104`, `Reserved6=112`, and `Reserved7=120`. Correct the active route proof to describe the actual two-cache-line contract: 48 bytes of AUP payload, 28 bytes of scalar contract fields, and 52 bytes of explicit 4/8-byte aligned padding/extension lanes.
Rejected Alternatives: Shrinking the signal to match stale docs; leaving tail padding as size-only proof; editing only the log while executable guard stayed partial; accepting a stale size audit statement for a 128-byte contract. The contract source and Core validation are the owner truth.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. Valid packets keep the same bounded lane capacity; malformed layout drift fails closed before SHINOBU Vault allocation. The Dear Lie shader still scales continuously by `GlobalQualityWeight`.
Hardware Impact: Cold boot adds four offset comparisons. Hot path cost is 0 us. The gain is preventing an ARM64/tail-padding ABI mismatch from entering Vault-backed death reconciliation.
First 20 Minutes Route Impact: The first death/rebirth loop cannot proceed under a stale packet-size assumption; the route now proves the current 128-byte signal packet before allocating respawn buffers.

## Decision 61 - External Request-Committed Flag Exclusivity

Problem: KCC and Mesofauna coherent gates still treated `Phase.Request + Requested + Committed` as a request packet. Physiology owner admission rejects that state because request input must not carry the output `Committed` bit. External consumers could therefore still grant collision bypass or aggro reset for a packet the owner refuses.
Solution: Add `(signalFlags & PlayerRespawnSignalFlags.Committed) == 0u` to the request-side gate in `HydrodynamicKccRuntime` and `PredatorCognitionDomain`. Leave the committed-side gate as `Phase.Committed + Committed` because SHINOBU's resolved snapshot intentionally carries both `Requested` and `Committed` after med-bay resolution.
Rejected Alternatives: Clearing contradictory bits in Core sanitation; adding a new signal; routing KCC/Mesofauna through Physiology; rejecting committed packets that also carry `Requested`. The current signal contract needs direct consumers to fail closed for malformed request packets while accepting the SHINOBU-produced committed snapshot.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed request+commit packets collapse before external side-effect work and cannot spend KCC or Mesofauna mutation cycles.
Hardware Impact: Adds one bit-test to rare respawn snapshot loops in two existing consumers. No allocation, no Vault handle, no JobHandle edge, and no new assembly reference.
First 20 Minutes Route Impact: Early test harness or bypass packets cannot clear predators or suspend collision unless their request semantics match the Physiology owner gate.

## Decision 62 - KCC Accepted-Generation Latch Repair

Problem: `HydrodynamicKccRuntime.ConsumeRespawnCollisionSuspendSignals()` wrote `_lastRespawnCollisionSnapshotGeneration` before scanning for an admissible packet. A malformed-only snapshot could consume the generation and suppress a later valid transformed packet if phase ordering or test harness calls exposed the same generation again.
Solution: Move `_lastRespawnCollisionSnapshotGeneration = snapshotGeneration` into the accepted path after `_respawnCollisionBypassFrames = 1`. The latch now means "this generation already produced a collision bypass" rather than "this generation was scanned."
Rejected Alternatives: Keeping the early latch for micro-optimization; adding a second scanned-generation field; forcing a Physiology callback; blocking on a dispatcher fence. The snapshot is capped and rare, and accepted-only latching preserves the no-duplicate-bypass invariant without letting invalid packets consume authority.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. Malformed packets may be rescanned if the method is called more than once in the same generation, but the lane is bounded to 16 max frame signals and normal death traffic is one packet.
Hardware Impact: No hot steady-state allocation or job change. In the rare malformed packet path, a repeated same-generation call may rescan up to 16 entries; accepted valid path cost is unchanged except the same assignment runs later.
First 20 Minutes Route Impact: Early death tests cannot lose the one-frame KCC suspend because an invalid packet reached the consumer before the valid transformed packet.

## Decision 63 - Proof Drift Archive Sync And Static Verification

Problem: After the 128-byte `PlayerRespawnSignal` proof repair and coherent external-gate repair, active files and archive mirrors could diverge. A stale proof file is a functional integration hazard because reviewers may trust the wrong packet size or request/commit semantics.
Solution: Sync the direct Batch010 archive mirrors from active `Status/Route/Rationale/LOG`, SHA-256 verify all four pairs, and run focused scans for obsolete packet-size claims, coherent KCC/Mesofauna gates, accepted-only KCC generation latching, DTO property/`Pack=` usage, forbidden coroutine/LINQ/reload/object-churn patterns, direct external Physiology imports, and trailing whitespace.
Rejected Alternatives: Leaving archive mirrors stale as historical noise; editing combined archive aggregates and creating broad churn; claiming proof from source only while route/rationale/log still carried old wording; launching a compile for documentation-only proof, especially with CPU guard at 100% and compiler processes already active.
Scalability potential: Runtime behavior unchanged. The preserved route still scales by continuous `GlobalQualityWeight`: low devices get shorter Dear Lie cover and suppressed shader detail, middle tiers keep moderate grain/chroma, high/ultra tiers spend saved CPU on shader overkill while collision/aggro side effects remain bounded to one coherent packet.
Hardware Impact: Documentation/static verification only, 0 us runtime. The evidence protects ARM64 ABI correctness by keeping the current two-cache-line 128-byte signal layout synchronized across source, route, ledger, rationale, log, and archive mirrors.
First 20 Minutes Route Impact: The first death/rebirth review path now has one consistent proof chain: no scene reload, no GameObject respawn, finite AUP only, coherent signal admission, one-frame KCC suspend, Mesofauna aggro reset, and shader-only Dear Lie cover.

## Decision 64 - Respawn Vault Generation Descriptor Migration

Problem: `ShinobuRespawnReconciliationRuntime` still stored legacy `VaultBufferHandle<T>` fields. Those handles carry stale pointer-era metadata and violate the current Vault generation-handle addendum, even if `.Resolve(vault)` internally routes through the generation path.
Solution: Replace all sixteen respawn, telemetry, tuning, CSV, physiology, metabolism, and kinematic handle fields with 16-byte `VaultGenerationHandle<T>` descriptors. Allocate/request descriptors only through `IDataVault.GetGenerationHandle`, and resolve phase-local `NativeArray<T>` views through `IDataVault.TryResolveHandle` helpers immediately before use.
Rejected Alternatives: Keeping legacy handles because the bridge still works; storing persistent `NativeArray<T>` aliases after first resolve; releasing shared Physiology/Kinematic buffers from SHINOBU on disable; adding a private fallback buffer. Legacy handles preserve pointer-shaped state, persistent aliases violate Vault ownership, and releasing shared buffers from this route would steal ownership from adjacent systems.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. The death route still scales visual cost through continuous `GlobalQualityWeight`; this patch reduces stale-pointer risk across all tiers and does not add a quality branch.
Hardware Impact: Runtime steady-state cost is one descriptor validity check before phase-local resolve. Low-end i3/MX350 gain is safety, not measured frame time: stale Vault generation now fails closed instead of trusting cached pointer metadata. No new allocation, job, native container, or sibling dependency was introduced.
First 20 Minutes Route Impact: Early death/rebirth tests now exercise the current Vault ABI: one owner route, pointer-free descriptors, method-local native views, and no local native memory ownership inside the respawn manager.

## Decision 65 - Owner-Local Respawn Descriptor Release

Problem: The generation descriptor migration cleared cached handles but did not release SHINOBU-owned respawn buffers. Because `GlobalDataVault.ReleaseBuffer(in VaultGenerationHandle<T>)` decrements refcount or frees the allocation and bumps generation, lifetime handling must distinguish owner-local buffers from shared live-state buffers.
Solution: Add `ReleaseOwnedVaultDescriptors(IDataVault)` and call it after the active job fence on disable, on DataVault replacement before swapping `_dataVault`, and after failed handle acquisition. The method releases only respawn-owned buffers `71604..71613`: state, request, med-bay, fade, telemetry ring, telemetry cursor, tuning, penalty rules, penalty count, and CSV scratch. Shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic descriptors are cleared but never released by this route.
Rejected Alternatives: `ReleaseOwnerBuffers(SystemID.GameplayPlayer)`; releasing all sixteen descriptors; leaving owner buffers resident forever; adding private fallback containers. `SystemID.GameplayPlayer` is shared by multiple live-state runtimes, and releasing all descriptors could tombstone player kinematics or physiology state outside SHINOBU_155 ownership.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. Descriptor release is lifecycle-only; the Dear Lie still scales continuously by `GlobalQualityWeight`, while low-tier and ultra-tier death routes use the same owner-local Vault rows.
Hardware Impact: Hot path cost 0 us. Shutdown/hot-swap cost is ten bounded `ReleaseBuffer` calls after the job fence. Low-end i3/MX350 gain is avoiding persistent Vault residency and generation ambiguity across scene toggles without touching shared live-state buffers.
First 20 Minutes Route Impact: Repeated early death/rebirth scene toggles no longer leave respawn telemetry/tuning/CSV buffers resident, and they also cannot delete the live player physiology or kinematic state.

## Decision 66 - Shared Live-State Descriptor Read-Only Acquisition

Problem: `ShinobuRespawnReconciliationRuntime.EnsureVaultState()` still used allocation-capable `GetGenerationHandle<T>` for shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic lanes. If their real owners had not booted yet, SHINOBU could create a shared buffer under the death route and turn a dependency absence into shadow state.
Solution: Keep `GetGenerationHandle<T>` only for SHINOBU-owned respawn buffers `71604..71613`. Acquire shared live-state descriptors only through `IDataVault.TryGetGenerationHandle`; if they are absent, partial owner-local buffers are released and all descriptors are cleared. Dispatcher phases continue to use `HasHotVaultState()` and never allocate.
Rejected Alternatives: Leaving creation in place for resilience; releasing shared buffers on failure; adding private fallback physiology/kinematic rows; polling GlobalRegistry in hot phases to wait for owners. Resilience through shadow state violates one fact -> one owner, and private fallback rows are not rollback truth.
Scalability potential: Low/Middle/High/Ultra valid behavior unchanged. On any tier where shared owners are absent, the route collapses to fail-closed before med-bay search, Burst reset, inventory penalty, KCC bypass, AI reset, or shader publish.
Hardware Impact: Hot path cost 0 us. Cold boot replaces six possible create/grow calls with six descriptor reads. Low-end i3/MX350 gain is avoiding accidental buffer allocation and finite-sanitize passes for lanes this route does not own.
First 20 Minutes Route Impact: The first lethal event can reconcile only after real Physiology/Metabolism/Kinematic owner lanes exist; otherwise it falls back to legacy death handling instead of creating false body state.

## Decision 67 - Allocation-Lock Existing Descriptor Recovery

Problem: `EnsureVaultState()` checked `IDataVault.IsAllocationLocked` before trying to reacquire existing owner-local descriptors. Under domain-reload-disabled entry, DataVault hot-swap, or descriptor loss, already-created SHINOBU buffers could be present but unreachable because the manager returned false before `TryGetGenerationHandle`.
Solution: Add `TryAcquireOwnedVaultDescriptor<T>`. It first reads an existing descriptor with `TryGetGenerationHandle<T>` and resolves it to prove the row count. Only if the descriptor is missing or undersized does it test `IsAllocationLocked` and then call allocation-capable `GetGenerationHandle<T>` for SHINOBU-owned buffers.
Rejected Alternatives: Keeping the pre-lock hard fail; using `GetGenerationHandle<T>` unconditionally; using shared live-state `TryGetGenerationHandle` for owner-local buffers without row-count proof; adding private fallback rows. The correct route is existing descriptor recovery first, owner-local allocation second, fail-closed third.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. Locked-Vault recovery is lifecycle-only; visual cost still scales through continuous Dear Lie quality math, and no quality branch was added.
Hardware Impact: Hot path cost 0 us. Cold recovery adds descriptor read plus one transient resolve per SHINOBU-owned lane and avoids failed respawn bootstrap or unnecessary allocation attempts when the Vault is locked. Low-end i3/MX350 gain is deterministic non-reload transition recovery without compile/runtime churn.
First 20 Minutes Route Impact: Re-entering the first death/rebirth loop after a non-reload transition can reuse existing respawn Vault rows instead of silently disabling reconciliation until a full scene/domain rebuild.

## Decision 68 - Stale Generation Descriptor Cold Gate

Problem: `EnsureVaultState()` treated nonzero `VaultGenerationHandle<T>` descriptors as sufficient proof. After Vault relocation, compaction, release/reacquire, or service replacement, a cached descriptor can still carry nonzero IDs while resolving to no current row or to an undersized row.
Solution: Add `AreVaultHandlesResolvable(IDataVault)` and `IsVaultDescriptorResolvable<T>`. The cold early return now resolves every cached descriptor and proves required row count before accepting Vault state. If any descriptor is stale, the manager clears cached handles and reacquires through the existing-descriptor-first path.
Rejected Alternatives: Trusting nonzero BufferID/Generation metadata; polling `GlobalRegistry` from hot dispatcher phases; unconditional `GetGenerationHandle<T>` on every cold entry; adding private fallback rows. Descriptor metadata is not the owned fact; resolved Vault rows are the proof.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. Stale descriptors collapse the route to cold fail-closed reacquisition before med-bay search, Burst reset, inventory command, or shader publish. The Dear Lie still scales only through continuous `GlobalQualityWeight`.
Hardware Impact: Hot path cost 0 us. Cold entry pays bounded descriptor resolves for sixteen lanes and prevents a wedged first death route after Vault replacement. Low-end i3/MX350 gain is deterministic non-reload recovery without hidden allocation churn or shadow state.
First 20 Minutes Route Impact: After non-reload transition or Vault relocation, the first lethal event can refresh descriptors instead of accepting stale metadata and failing later inside the med-bay/job/shader route.

## Decision 69 - Shared Descriptor Fresh-Acquisition Row Proof

Problem: The cached-descriptor gate proved all lanes, but the fresh acquisition chain still used `TryGetExistingVaultDescriptor<T>` as metadata-only for shared Physiology, Decompression, Tissue, PhysiologyScalar, Metabolism, and PlayerKinematic descriptors. A stale, zero-length, or undersized shared lane could let `EnsureVaultState()` return true until a later phase-local resolve failed.
Solution: Add `requiredLength` to `TryGetExistingVaultDescriptor<T>` and make it require `TryGetGenerationHandle`, `TryResolveHandle`, `IsCreated`, and `Length >= requiredLength`. The six shared live-state lanes now prove row availability on first cold acquisition exactly like the cached gate.
Rejected Alternatives: Relying on later hot phase resolve failures; using allocation-capable `GetGenerationHandle<T>` for shared lanes; adding private fallback rows; marking descriptors created without row proof. SHINOBU can read shared truth only after the true owner has provided a resolvable row.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. Missing or undersized shared rows fail closed before med-bay search, reset job scheduling, inventory penalty, KCC side effects, or shader publish. No binary quality switch was added.
Hardware Impact: Hot path cost 0 us. Cold acquisition adds six bounded resolve+length checks and can save downstream failed phase attempts after partial Vault owner boot. Low-end i3/MX350 gain is deterministic cold failure without shadow state or late frame work.
First 20 Minutes Route Impact: The first death/rebirth test cannot pass cold boot with a descriptor-only shared lane; it requires actual Physiology/Metabolism/Kinematic rows before reconciling the player.

## Decision 70 - Hot Vault Generation And Row-Length Gate

Problem: `HasHotVaultState()` still treated nonzero generation descriptors as enough proof inside dispatcher phases. If a Vault compaction fence, release/reacquire, or owner replacement invalidated descriptors after cold acquisition, PreSimulation/Simulation/VisualSync could enter work with stale metadata. Separately, some resolve sites tested only `NativeArray.IsCreated` before row-zero indexing or unsafe pointer extraction.
Solution: Strengthen the hot gate with `!IDataVault.IsCompactionFenceActive` and per-buffer generation equality through `IDataVault.TryGetBufferGeneration` for all sixteen descriptors. Keep allocation-capable handle creation out of dispatcher phases. Add `HasRequiredLength(...)` and use it at default hydration, request write, simulation pointer extraction, CSV ingest, black-box dump, editor read/write, VisualSync fade read, and editor gizmo read seams before any row-zero access or unsafe pointer handoff.
Rejected Alternatives: Calling `AreVaultHandlesResolvable(...)` every dispatcher phase; trusting later `TryResolveHandle` failures; adding private fallback rows; reacquiring handles from `GlobalRegistry` inside hot phases. Full resolve proof every phase would build transient array views for all lanes even when no death packet exists; generation proof plus explicit length checks catches stale descriptors and keeps row proof at the exact access seams.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. The death presentation still scales through continuous `GlobalQualityWeight`; stale or fenced Vault state collapses to no-op before med-bay search, reset scheduling, telemetry dump, CSV mutation, or shader publish.
Hardware Impact: Hot path adds bounded metadata generation checks, not allocations, not `GetGenerationHandle`, and not `JobHandle.Complete`. It prevents stale descriptor work and unsafe pointer extraction on weak devices without adding a binary quality branch.
First 20 Minutes Route Impact: A first death during Vault relocation or after shared-owner replacement now fails closed before any row-zero read, pointer handoff, or Dear Lie shader publish.

## Decision 71 - Compile-Wall And Burst Alias Proof Refresh

Problem: After the Vault generation hardening, the proof set still needed a current assembly-reference and Burst alias pass. A stale sibling asmdef reference would widen the compile wall, and a missing `[NoAlias]` or deterministic Burst flag would weaken the rollback/vectorization claims even if the code body looked clean.
Solution: Re-read `Hecton8.Physiology.asmdef` and `Hecton8.Physiology.Editor.asmdef`, then scan SHINOBU respawn runtime/data/job source. Runtime Physiology references only `Hecton8.Core`, `Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, and Unity packages; the editor asmdef references runtime Physiology plus the same Core/Unity set. No direct sibling runtime assembly reference to World, Physics, Rendering, Inventory, AI, Fauna, Construction, Habitat, Graphics, or Gameplay was found. `GenerateMockRespawnPointsJob`, `ResetPlayerPhysiologyJob`, and `UpdateRespawnFadeJob` all remain `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`; every NativeArray or unsafe pointer job input is annotated `[NoAlias]`.
Rejected Alternatives: Editing asmdefs to satisfy a theoretical "contracts only" reading; adding wrapper interfaces; moving Core/Memory references out of Physiology; launching a build for a proof-only pass. Core/Core.Contracts/Core.Memory are the existing foundation assemblies, not sibling gameplay domains, and this pass found no runtime dependency to delete.
Scalability potential: Low/Middle/High/Ultra behavior unchanged. The route still buys visual overkill through the shader Dear Lie while CPU work stays bounded to one signal row, one Vault request row, and the O(8) med-bay scan. No binary quality switch was introduced.
Hardware Impact: Runtime hot path 0 us change. The proof protects iteration time and Burst codegen: no sibling compile-wall edge, no virtual interface array, and no alias ambiguity on reset/fade pointer lanes. Low-end i3/MX350 benefit is preserving existing vectorization and avoiding needless rebuild fan-out.
First 20 Minutes Route Impact: First death/rebirth can continue to route through Core contracts and Vault descriptors without Physiology taking a hard dependency on World/Physics/AI/Inventory runtimes or weakening deterministic reset/fade jobs.

## Decision 72 - Shader Bridge Generation Descriptor Migration

Problem: SHINOBU VisualSync used the cached-vault `PublishRespawnDearLie(IDataVault, Vector4)` overload, but the shared `HectonShaderGlobalDataVaultBridge` still cached `ShaderGlobalState` through legacy `VaultBufferHandle<float4>` and `.Resolve(vault)`. That left a pointer-bearing bridge immediately downstream of the respawn Dear Lie route, even though SHINOBU's own descriptors had already moved to generation handles.
Solution: Migrate only the bridge descriptor to `VaultGenerationHandle<float4>`. `TryPrepareSlotsVault(IDataVault, bool allowAllocation)` now recovers existing descriptors through `TryGetGenerationHandle`, allocates missing bridge storage through `GetGenerationHandle` only when the caller explicitly allows allocation and the Vault is unlocked, and proves every slot view through `TryResolveHandle` and `Length >= SlotCount`. `PublishRespawnDearLie(IDataVault, Vector4)` passes `allowAllocation:false`, so SHINOBU VisualSync can write existing shader slots but cannot create or grow `ShaderGlobalState`. `WriteReadSlot(IDataVault, ...)` resolves a method-local `NativeArray<float4>` before writing the slot and never calls the obsolete pointer handle resolver.
Rejected Alternatives: Leaving the bridge legacy because it is outside Physiology; duplicating a private shader-global buffer inside SHINOBU; changing `GlobalShaderDispatcher`; deleting the generic registry-resolving overload used by other bridge callers. The narrow bridge migration removes pointer-shaped state from the exact cross-domain interface SHINOBU uses without expanding compile dependencies or changing rendering semantics.
Scalability potential: Low/Middle/High/Ultra visual behavior unchanged. Respawn blackout/grain/chroma still scales continuously with `GlobalQualityWeight`; this patch changes only descriptor safety for the shader-global slot transport.
Hardware Impact: Hot VisualSync cost remains one lock, one generation-resolved `NativeArray<float4>` view, and one slot write when the slot buffer exists; missing storage is a fallback scalar write, not a Vault allocation. Low-end i3/MX350 benefit is stale-pointer avoidance and no extra managed allocation; high/ultra retains the same shader overkill path.
First 20 Minutes Route Impact: The first death/rebirth Dear Lie cover no longer depends on a legacy pointer-bearing shader bridge while publishing `_HectonRespawnDearLieParams`.

## Decision 73 - Med-Bay Radius And Fault-Flag Isolation

Problem: `RespawnTuningDTO.MedicalBaySearchRadiusMeters` existed in the 64-byte tuning row but the PreSimulation med-bay resolver and Burst fallback scan ignored it. The same scan also set `InvalidTargetAup` directly on the output flags for any rejected candidate, so a later valid med bay could still carry a black-box fault bit caused by an earlier out-of-radius or invalid candidate.
Solution: Sanitize tuning before med-bay selection and clamp `MedicalBaySearchRadiusMeters` to `1..50000` meters. Both resolver paths now compute `maxSearchSq` from that continuous radius and reject candidates outside the designer-tuned radius before clearance validation. Rejected candidates write only to a local `rejectedCandidateFlags` mask; selected candidates write only `selectedCandidateFlags`. The rejected mask is applied only when the final route falls back to the deterministic lifepod.
Rejected Alternatives: Treating radius as an editor-only hint; keeping all candidate rejections in the final flag word; adding another signal field for rejected candidate count; allocating a per-candidate diagnostics list. The tuning row already owns the search radius, and the telemetry flag should describe the chosen route, not every discarded row when a valid med bay exists.
Scalability potential: Low devices can tune a smaller radius so the fallback scan rejects distant rows after one local AUP subtraction and distance compare; middle tiers can keep a wider med-bay net; high and ultra tiers can spend the same bounded O(8) scan on larger bases without changing authoritative state. No binary quality switch was added.
Hardware Impact: Adds one multiply and one compare per med-bay candidate in the rare death path. It can skip later clearance work for distant rows and prevents noisy telemetry dumps after valid selection. No allocation, new Vault lane, new signal, direct sibling dependency, or JobHandle edge was introduced.
First 20 Minutes Route Impact: Early death/rebirth tests now honor designer search radius in both the primary and fallback med-bay route, and a valid medical bay no longer looks like a target-AUP failure because an earlier candidate was invalid.
