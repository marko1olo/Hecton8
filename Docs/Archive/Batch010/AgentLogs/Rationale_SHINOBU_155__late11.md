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
Solution: Add only a contract-signal snapshot read in `PredatorCognitionDomain`'s existing signal/data stage and clear `TargetHashID`/state when the signal marks a requested or committed respawn. No direct Physiology type references are introduced.
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
Solution: Patch `HydrodynamicKccRuntime` to read only the contract signal snapshot. A request or committed respawn packet with `SuspendCollision` sets a one-frame `_respawnCollisionBypassFrames` latch keyed by `SignalBus<PlayerRespawnSignal>.SnapshotGeneration`; the KCC skips `CapsulecastCommand.ScheduleBatch`, bypasses collision hit extraction, passes `CollisionBypass=1` into `KinematicResolutionJob`, and marks debug flags with `FlagRespawnCollisionBypass`. The snapshot-generation latch prevents duplicate extension, so the suspend remains exactly one snapshot generation.
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
Solution: Added `PlayerRespawnSignal` to `GlobalSignals` direct pre-simulation flush and post-simulation clear, registered its central capacity through `InitializeCategorySignalLanes`, validated the 96-byte payload size, added a finite sanitizer for both `double3` AUPs plus phase/collision-frame bounds, added `HectonSignalLaneContract.PlayerRespawnSignal` with stable hash `0x5253504E`, and preserved the closed generic lane in `SignalWardenRuntime`. The payload now exposes its lane capacity constants so Gameplay and Physiology early-boot calls reuse the same numbers instead of hardcoded duplicates.
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
Solution: Keep Physiology as the sole med-bay resolver, then mutate the current `SignalBus<PlayerRespawnSignal>` snapshot in-place with `SignalBus<PlayerRespawnSignal>.TransformSnapshot`. The transformer writes resolved `RespawnAUP`, `MedicalBayHashID`, `Requested`, `Committed`, `SuspendCollision`, translated med-bay flags, and clamps `SuspendCollisionFrames` to the payload maximum. `HydrodynamicKccRuntime` now treats request or committed respawn packets as eligible, while the snapshot-generation latch preserves the one-frame collision bypass.
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
