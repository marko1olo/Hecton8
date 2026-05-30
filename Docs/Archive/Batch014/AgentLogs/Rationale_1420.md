# Agent 1420 Rationale

## Decision 000: Fresh Ledger Creation
Problem: Required Status_1420.md and Rationale_1420.md files were absent.
Solution: Created fresh ledgers before code analysis so disk is the durable state source.
Rejected Alternatives: Chat-only tracking rejected because context compression destroys assignment state.
Scalability potential: No runtime impact. Enables deterministic audit of Low/Middle/High/Ultra decisions later.
Hardware Impact: 0 us runtime. No i3/MX350 impact.

## Decision 001: Missing Named Runtime Boundary
Problem: Batch target `Assets/_Project/Scripts/Vehicles/SubmarineNavigationRuntime.cs` does not exist in this workspace, but submarine ballast/navigation logic exists in `Assets/_Project/Scripts/Gameplay/SubmarineAutoLevelBallastController.cs`.
Solution: Treat the existing controller and SHINOBU_333 route card as the active runtime surface; write a JSON alias ledger proving the named file absence and 0 forbidden class-scope Native* aliases in the active controller.
Rejected Alternatives: Creating a stub `SubmarineNavigationRuntime.cs`, editing archived prompts, or searching neighboring agents' logs. Those would fabricate ownership.
Scalability potential: Low/Middle/High/Ultra unchanged. This is provenance only.
Hardware Impact: 0 us runtime. No i3/MX350 impact.

## Decision 002: Vault Write Lock Fail-Closed Route
Problem: Several hot mutation paths resolved mutable Vault arrays through read-style helpers, so compaction/write contention could silently skip or leave no actionable fault route.
Solution: Add a local `TryAcquireVaultWrite` helper that checks cached handle identity, compaction fence, lock acquisition, and view length; all callers release with `finally` and record code/buffer/frame into PID telemetry.
Rejected Alternatives: Direct `TryResolveHandle` before writes, `GlobalDataVault.TryGetLatestCreated()`, or scene searches. Those violate phase ownership and can mask defrag contention.
Scalability potential: Low tier skips the frame cleanly under contention; Middle/High/Ultra can use the same route while spending saved cycles on visual ballast lies, not truth divergence.
Hardware Impact: Expected cost is a few atomic/metadata checks per mutation window; estimate <5 us on i3/MX350 during active ballast frames, 0 us when paths are inactive.

## Decision 003: Burst Job Lock Lifetime
Problem: PID, flood mass, and ballast Burst jobs write Vault-backed DTOs after scheduling; releasing the lock before job completion would reopen a relocation window.
Solution: Hold the relevant write lock from schedule through explicit completion, then release in `Complete*Job` and disposal cleanup. This is not a cached physical view field; it is a bounded mutation lease.
Rejected Alternatives: Same-frame `.Complete()` to shorten lock lifetime, unmanaged persistent array fields, or tiny job removal. Same-frame completion would burn frame time and defeat dispatcher-owned completion.
Scalability potential: Low devices fail closed if defrag owns the lane; Middle/High/Ultra preserve deterministic truth and can scale presentation separately through `GlobalQualityWeight`.
Hardware Impact: Prevents catastrophic relocation reads; lock bookkeeping estimate <10 us on i3/MX350 when all three jobs are active, no claimed steady-frame saving.

## Decision 004: Read-Only Accessor Purity
Problem: Public ballast fill readback returned `AsReadOnly()` from a mutable resolved view, and SHINOBU_332 suppression used legacy `TryReadHandle`.
Solution: Route those reads through `IDataVault.TryReadOnlyHandle` with cached-handle and length checks. Failure returns default/inactive state without publishing, completing jobs, or mutating global state.
Rejected Alternatives: Keeping mutable read handles for convenience or using `TryGetLatestCreated()` fallback. Both blur owner/consumer boundaries.
Scalability potential: Low/Middle/High/Ultra identical gameplay truth. Read-only failure degrades HUD/suppression observation, not physics ownership.
Hardware Impact: Similar metadata read cost to previous path; no expected measurable frame change on i3/MX350.

## Decision 005: Editor Proof Artifacts
Problem: Layout and lock behavior needed executable proof without a full Unity play session or dotnet rebuild.
Solution: Added an editor layout validator and opt-in editmode stress harness. The harness uses `GlobalDataVault` handles and ballast Burst jobs with extreme commands, after a warmup, and asserts fail-closed lock contention.
Rejected Alternatives: Runtime self-test in player frames, managed arrays, or forcing a build while CPU/dotnet gate is closed.
Scalability potential: No runtime cost. Editor proof covers Low/Middle/High/Ultra DTO layout and lock semantics before content scaling.
Hardware Impact: 0 us runtime. Editor-only validation cost occurs on script reload or opt-in tests.

## Decision 006: Compilation Gate Obedience
Problem: Verification normally requires a compiler pass, but the machine showed active compiler processes and the CPU gate previously sampled at 100%.
Solution: Did not launch `dotnet build`; used static syntax checks, brace balance, diff check, targeted regex scans, JSON validation, and SHA-256 proof instead.
Rejected Alternatives: Competing build process, rebuild spam, or claiming green compile without running it.
Scalability potential: No runtime impact. Prevents starving sibling agents on shared hardware.
Hardware Impact: Saved one full project build on saturated CPU; exact time not claimed, likely seconds to minutes on i3/MX350.

## Decision 007: APEX Evidence Recheck
Problem: Final verification required exact proof, not a prose completion claim, and the first case-insensitive scanner counted `math.select` as a false LINQ `.Select` hit.
Solution: Reran the Zero-GC scan with `-CaseSensitive`, producing 0 matches for `new Native*`, `Allocator.Persistent`, `foreach`, LINQ `.Where/.Select/.ToList/.ToArray`, `string.Format`, `.ToString`, interpolated strings, and literal string concatenation. Final report artifact hash is `E57F679DFB487D1AA64C6F8E9914B5F8A986A57C1384F22D464FF1141F31B27F`.
Rejected Alternatives: Editing the runtime to silence a false positive, or launching a build while CPU/process gates were closed.
Scalability potential: No runtime cost. Keeps Low/Middle/High/Ultra truth routes unchanged and preserves continuous `GlobalQualityWeight` scaling instead of binary quality switches.
Hardware Impact: One targeted runtime build was executed only after gate opened at CPU 37.00% and 0 compiler processes; `Hecton8.Core.csproj` passed with 0 warnings and 0 errors in 19.91s. A second build was rejected after CPU rose to 51.61%.

## Decision 008: Buoyancy Math LOD Made Real
Problem: APEX review found `ActiveSampleBudget` was written into the DTO but `CalculateBuoyancyForceJob` still evaluated all four analytical submerged-ratio samples; `quality` algebraically cancelled out, so processing load did not actually scale.
Solution: `ResolveBallastActiveSampleBudget(float quality)` now maps continuous `GlobalQualityWeight` to a maximum 1..4 sample budget, while `CalculateBuoyancyForceJob` skips unavailable bow/stern/beam sample math and fractionally weights the next sample from the scalar.
Rejected Alternatives: Binary `isLowEnd` branches, full fluid simulation, or leaving telemetry-only quality claims without code consumption.
Scalability potential: Weak devices execute center-only buoyancy; middle devices add weighted bow/stern approximations; high/ultra devices run four cheap analytical sample points. No BufferID, DTO layout, save identity, or force ownership changes.
Hardware Impact: Low-end frames skip up to three saturate/rcp sample calculations per ballast solve; high-end spends those cycles on smoother buoyancy response. Estimated sub-microsecond per solve, but now actually load-scaled.

## Decision 009: Mutable Read Surface Narrowed
Problem: Several private owner-internal helpers were named `TryRead*` but returned mutable `NativeArray<T>` views. Most call sites only read, but the API shape allowed accidental lockless writes later.
Solution: Converted ballast fill, tank rows, tank positions, PID telemetry, and ballast telemetry observation to `NativeArray<T>.ReadOnly` via `TryReadOnlyHandle`. Mutable resolves remain only for PID output, flood mass output, and ballast force packet job outputs, and each helper verifies its write lock is already held.
Rejected Alternatives: Leaving the semantic hazard because call sites were currently disciplined, or adding comments without narrowing the type surface.
Scalability potential: Low/Middle/High/Ultra unchanged. This is safety surface reduction, not a visual or physics feature.
Hardware Impact: Same metadata read cost; prevents future accidental mutable alias writes during compaction-sensitive frames.

## Decision 010: APEX Scalability Claim Rejected Until Code Matched It
Problem: A second source read proved the previous scalability claim was false in the actual file: `CalculateBuoyancyForceJob` still used `const int activeSamples = 4`, and its `qualitySecondary + invariantCompensation` math made `GlobalQualityWeight` cancel out.
Solution: Changed the buoyancy solve to clamp `sample.ActiveSampleBudget` to `1..4`, always compute the center sample, and compute bow/stern/beam only when the continuous `GlobalQualityWeight` span activates each lane. The next lane is fractionally weighted instead of using a binary low/high branch.
Rejected Alternatives: Leaving the report as proof, adding a device-tier `if(isLowEnd)`, or replacing ballast with a full fluid simulation. The correct fix is a cheap deterministic analytical cheat that actually scales work.
Scalability potential: Weak devices run center-only buoyancy; middle devices add fractional bow/stern lanes; high/ultra devices run all four cheap analytical sample points with smoother force output. Gameplay truth ownership and BufferIDs stay unchanged.
Hardware Impact: Low quality can skip up to three submerged-ratio sample calculations per ballast solve. Estimated sub-microsecond per solve; no profiler proof claimed.

## Decision 011: Lock Release Proof Tightened
Problem: A repeated APEX read found two stale facts on disk: the buoyancy job had reverted to `const int activeSamples = 4`, and partial ballast solver lock acquisition still used manual release-before-return cleanup instead of a single proof-friendly `finally`.
Solution: Reapplied the `ActiveSampleBudget` buoyancy Math LOD and rewrote partial solver lock acquisition plus invalid-view release in `TryAcquireVaultWrite` to release through `finally` blocks guarded by explicit lock-acquired flags.
Rejected Alternatives: Keeping manual release paths because they were short, or relying on report text while the source disagreed. Evidence must follow source bytes.
Scalability potential: Weak devices keep center-only submerged-ratio math; middle/high/ultra get progressively richer analytical probes. Fail-closed lock cleanup is identical across devices and does not alter gameplay truth.
Hardware Impact: Lock cleanup adds a few boolean writes only on acquisition paths; estimated sub-microsecond. Prevents leaked write locks under failed multi-buffer acquisition.

## Decision 012: Stress Harness Must Exercise Math LOD
Problem: The opt-in stress harness varied `GlobalQualityWeight` but pinned `ActiveSampleBudget = 4`, so it did not prove low/middle/high/ultra sample budget behavior.
Solution: The harness now derives `ActiveSampleBudget` from the same continuous scalar route and asserts that completed packets span `ActiveSamples == 1..4` across the 1000-iteration measured loop.
Rejected Alternatives: Leaving the harness as a generic ballast stability test while the report implies scalability coverage.
Scalability potential: The editor proof now covers center-only weak-device math through four-probe high/ultra math without platform booleans.
Hardware Impact: 0 us runtime. Editor-only proof adds two integer min/max accumulators inside the measured harness loop.

## Decision 013: Mirror Fill Must Not Reenter Tanks Read-Only Under Writer Lock
Problem: `CompleteBallastSolverJob` holds the ballast solver write locks, then calls `MirrorBallastFillFromTanks`. That method tried to read tanks through `TryReadBallastTanksReadOnly`, which can fail or become contract-ambiguous while the same owner holds the tanks write-lock.
Solution: Added `TryResolveBallastTanksLocked` and routed mirror-fill through the lock-held mutable tanks view before acquiring the separate ballast fill write-lock.
Rejected Alternatives: Assuming `TryReadOnlyHandle` can coexist with a writer lease, or releasing solver locks before mirroring. Releasing early would reopen a relocation window before force packet readback.
Scalability potential: No quality-tier behavior changes. This is deterministic owner-phase correctness for all devices.
Hardware Impact: No measurable runtime saving claimed; removes a possible silent mirror skip during ballast job completion.

## Decision 014: Report Line Proof Must Match Source Bytes
Problem: The JSON proof artifact still listed old `H8Memory.cs` line numbers and omitted `TryResolveBallastTanksLocked` from the named mutable locked route list, even though the source and line proof had moved on.
Solution: Corrected only `Docs/Reports/SUBMARINE_MEMORY_OPTIMIZATION_REPORT_1420.json`: BufferID line proof now matches `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` lines 144-147 and 2111-2118, and the mutable route list includes `TryResolveBallastTanksLocked`.
Rejected Alternatives: Leaving stale evidence because the C# was correct, or running another compiler pass for a JSON-only edit.
Scalability potential: No runtime behavior change. This preserves audit integrity across Low/Middle/High/Ultra without touching gameplay truth.
Hardware Impact: 0 us runtime. Avoided an unnecessary second build while the proof-correction CPU gate sampled at 100.00%; the final compilation gate resampled at CPU 72.46% with active `csc` PID 320 and `dotnet` PID 8104.

## Decision 015: Dynamic Flood Room Inputs Must Be Read-Only Vault Consumers
Problem: `SubmarineMassSolverJob` only reads room water levels, room volumes, and room local AUPs, but the resolver path still used mutable `TryResolveHandle` through a generic existing-buffer helper. That violated the read-accessor purity doctrine and expanded the accidental write surface for a consumer-side job input.
Solution: Changed room input DTO views to `NativeArray<T>.ReadOnly`, replaced the mutable existing-buffer helper with `TryReadExistingVaultBuffer<T>`, and routed `RoomWaterLevels`, `RoomVolumes`, `RoomLocalAUPs`, plus SHINOBU_332 gyro counters through `TryReadOnlyHandle`.
Rejected Alternatives: Keeping mutable views because the current job body was disciplined, or adding comments without narrowing the type. Type-level read-only proof is the safer route under simultaneous-agent edits.
Scalability potential: Low/Middle/High/Ultra gameplay truth is unchanged. Weak devices skip the flood mass frame cleanly if the vault cannot provide read-only room inputs; stronger devices get the same deterministic inputs and spend quality budget on presentation, not ownership divergence.
Hardware Impact: Same metadata read cost class as the previous route; expected sub-microsecond. Latest C# build passed after CPU gate sampled 48.10% with 0 compiler processes; no full solution rebuild was run.

## Decision 016: Mutating Accessor Names Must Say Refresh
Problem: `TryReadExistingVaultBuffer` and `TryResolveRoomBuffers` updated cached handles and recorded telemetry faults while carrying read/resolve-style names. That violated the local purity language even though the route was owner-internal.
Solution: Renamed the mutating helpers and call sites to `TryRefreshExistingReadOnlyVaultBuffer` and removed the stale room resolver name from the active path.
Rejected Alternatives: Leaving misleading names because behavior was bounded. Under simultaneous-agent edits, names are part of the contract surface.
Scalability potential: No device-tier behavior change. This is audit correctness, not runtime fidelity.
Hardware Impact: 0 us runtime behavior change. Latest source later changed again, so the previous build proof is not current.

## Decision 017: Scheduled Room Inputs Need Pinned Lifetime
Problem: A read-only Vault view from `TryReadOnlyHandle` is current-phase only. Passing room water levels, volumes, and local AUPs into `SubmarineMassSolverJob` allowed the job to outlive the relocation-safe phase.
Solution: `AdvanceDynamicFloodSolver` now acquires all three room input buffers through `TryAcquireFloodRoomInputAliases`; each buffer is pinned by `TryLockBuffer`, assigned to the job only after the pin succeeds, and released by `ReleaseFloodRoomInputVaultLocks` from failure cleanup, completion cleanup, and dispose cleanup.
Rejected Alternatives: Plain `TryReadOnlyHandle` into a scheduled job, forcing `.Complete()` in the same frame, or copying room SOA into managed arrays. Same-frame completion burns the dispatcher window; managed copies violate Zero-GC.
Scalability potential: Low devices fail closed if the room buffers cannot be pinned; middle/high/ultra keep the same deterministic room truth and spend quality budget on visuals, not divergent physics.
Hardware Impact: Adds three bounded lock/unlock metadata operations only when the 0.5s flood solve is scheduled. Estimated sub-microsecond to a few microseconds on i3/MX350; profiler proof not run.

## Decision 018: Ballast Tanks Must Not Be Read During Pending Writer Job
Problem: `ApplyMassDistribution` could read `BallastTankDTO` while `_ballastSolverJobPending` kept the tank write lock alive after a nonblocking completion miss. That creates a possible read/write overlap with `EvaluateBallastTanksJob`.
Solution: `ApplyMassDistribution` and `SumBallastFill` read tanks only when `_ballastSolverJobPending == false` and `_ballastSolverVaultLocksHeld == false`; otherwise they fall back to the mirrored `SubmarineBallastFill01` buffer.
Rejected Alternatives: Forcing completion before mass distribution or assuming read-only access can coexist with the writer job. Forcing completion would break the no-hidden-complete rule.
Scalability potential: Weak hardware keeps frame progress by using the last mirrored fill value. Stronger devices normally complete the job in the swap window and read fresh tanks next frame.
Hardware Impact: No measured saving. The fix avoids a possible data race without adding allocations; fallback work is the same four-tank accumulation.

## Decision 019: External Read-Only Handles Must Not Require VehiclesPhysics Ownership
Problem: `LockstepStateValidator.MirrorRoomWaterLevelsToVault` can create `BufferID.RoomWaterLevels` under `SystemID.CoreDeterminism`. The submarine flood solver only consumes room buffers as pinned read-only job inputs, so requiring `handle.SystemID == VehiclesPhysics` on that route would fail closed on valid external snapshots and silently disable flood mass integration.
Solution: Added `IsVaultHandleForBuffer<T>` and used it only for external read-only refresh/pin routes (`TryAcquirePinnedReadOnlyVaultBuffer` and `TryRefreshExistingReadOnlyVaultBuffer`). Mutable and write routes still require `IsVehiclesPhysicsVaultHandle<T>` and `TryAcquireWriteLock`.
Rejected Alternatives: Accepting foreign handles for mutable/write routes, changing `GlobalDataVault` owner semantics, or patching `CoreDeterminism` ownership from this domain. Those would cross ownership boundaries without a route card.
Scalability potential: Weak devices fail closed only on actual missing/contended room buffers; middle/high/ultra keep deterministic room truth and spend `GlobalQualityWeight` on analytical sample richness, not ownership divergence.
Hardware Impact: Metadata check only: `BufferID + Generation` for external read-only routes. No allocations, no extra math, estimated sub-microsecond.

## Decision 020: Late-Frame Feedback Registration Must Be Cold
Problem: The current source contained a late-frame acoustic/haptic feedback queue, but the queue helpers attempted `TryRegisterLateFrameTickable()` from a path reached by `FixedTick`. That is a hot `GlobalRegistry` registration call and violates the registry-as-cold-DI rule.
Solution: Register `ILateFrameTickable` once from `RegisterRuntime`, keep `QueueFloodStressAcoustic` and `QueueCriticalFloodHaptic` to pure pending-struct/dirty-flag writes, and flush `SignalBus` feedback from `LateFrameTick`.
Rejected Alternatives: Direct `SignalBus` publish from the simulation phase, dynamic on-demand registry registration from flood feedback, or disabling feedback. Direct publish keeps phase coupling; dynamic registration hot-polls the registry.
Scalability potential: Low devices pay two dirty-flag checks in `LateFrameTick`; middle/high/ultra keep one-frame-delayed cinematic feedback without altering ballast truth or BufferID ownership.
Hardware Impact: Removes 3 hot `GlobalRegistry` registration calls from flood feedback. Estimated saving is sub-microsecond and unprofiled; proof is static scan `hot queue GlobalRegistry registration calls: 0`. Final JSON report SHA-256: `4DC098FEB8A1225F10F8E62419EFBF999D4CA96B495014782F9F80D99B4968AF`.

## Decision 021: SHINOBU_332 Cached Read Must Respect External Ownership
Problem: `RefreshShinobu332GyroRouteHandleCold` accepted `BufferID.Shinobu332GyroCounters` from an external owner, but `TryReadShinobu332GyroCountersCached` later used an owner-strict `VehiclesPhysics` read route. Valid gyro suppression snapshots could fail closed forever.
Solution: Added `TryReadExternalReadOnlyVaultBuffer<T>` for cached external read-only handles. It validates `BufferID + Generation`, compaction fence, `TryReadOnlyHandle`, creation, and length, without accepting the handle for mutable/write routes.
Rejected Alternatives: Weakening `IsVehiclesPhysicsVaultHandle` globally, polling `GlobalDataVault.TryGetLatestCreated()`, or creating a concrete dependency on SHINOBU_332 code. Those would break ownership boundaries.
Scalability potential: Low/Middle/High/Ultra gameplay truth remains unchanged. External gyro suppression now degrades only on real missing/contended data, not owner mismatch.
Hardware Impact: Metadata-only read check. Estimated sub-microsecond; no GC and no scene lookup.

## Decision 022: Ballast Tuning Write Must Not Expand Tank/Command Lock Window
Problem: `PrepareBallastCommands` wrote `SubmarineBallastTuningDTO` while both tank and command Vault write-locks were held. That nested a third Vault mutation inside the hottest ballast command path.
Solution: Recorded the tank volume and a `wroteCommands` flag while tank/command locks were held, released both locks in `finally`, then called `WriteBallastTuning` afterward.
Rejected Alternatives: Leaving nested locks because they were short, or deleting tuning writes. The former widens contention; the latter removes evidence for tuning/debug.
Scalability potential: Weak devices see shorter lock windows under ballast command spam; stronger devices keep the same tuning telemetry and spend visual budget through `GlobalQualityWeight`.
Hardware Impact: Removes one nested write-lock from the command window. Estimated sub-microsecond contention reduction; profiler proof absent.

## Decision 023: PID Maelstrom Input Must Be Copied, Not Borrowed
Problem: `SchedulePidJob` passed `NativeArray<WhirlpoolFlow>.ReadOnly` from `IAnalyticalFlowReadModel` into `SubmarineAutoLevelPidJob` without a pin/fence. The provider owns and rewrites that backing buffer.
Solution: Copied at most two `WhirlpoolFlow` structs, matching `FluidAnalyticalContractConstants.MaxActiveMaelstromCount`, into blittable job fields `ActiveMaelstrom0/1`. The job samples those values directly.
Rejected Alternatives: Forcing same-frame job completion, adding a new cross-domain pin contract from this domain, or ignoring the lifetime mismatch. Same-frame completion is a stall; new contract is outside the domain.
Scalability potential: Low devices copy 0-2 structs and run the same cheap analytical fake; high/ultra keep maelstrom steering without unbounded fluid simulation.
Hardware Impact: Copies at most 128 bytes before scheduling. Removes undefined lifetime risk with negligible CPU cost.

## Decision 024: Visual Fluid Signals Belong In LateFrame
Problem: Tail-heavy bubble and fluid impulse signals are visual-fluid/VFX lanes, but were pushed directly from the simulation path. That crossed phase ownership and could make VFX respond before the simulation frame was fully resolved.
Solution: Added pending `BubbleSpawnSignal` and `FluidImpulseSignal` structs plus dirty flags. `EmitTailHeavyBubbleSignal` and `EmitTailHeavyFluidImpulse` only queue data; `FlushDynamicFloodFeedback` publishes from `LateFrameTick`.
Rejected Alternatives: Simulating more physical bubbles, publishing immediately, or removing the feedback. The correct route is a one-frame visual fake: player belief through bubbles/impulse VFX, physics truth unchanged.
Scalability potential: Low devices coalesce to one pending signal per LateFrame; middle/high/ultra retain richer visible feedback through existing SignalBus capacity, not extra physics.
Hardware Impact: Adds two dirty-flag branches in LateFrame. Removes simulation-phase VFX publication; estimated sub-microsecond.

## Decision 025: Nonfatal PID Telemetry Must Not Block Stabilization Or Dump IO
Problem: `PidTelemetryFlagDerivativeDisabled` is a nonfatal load-shed flag, but `CompletePidJob` previously treated any nonzero flag as a reason to dump telemetry and refuse torque/maelstrom output.
Solution: Added `PidTelemetryDumpFaultMask` and `PidTelemetryPidOutputForceBlockMask`. Dumps now occur only for fault flags; force output is blocked only by invalid PID output or critical flood.
Rejected Alternatives: Clearing the derivative-disabled flag or keeping binary success/fail behavior. Clearing loses telemetry; binary behavior disables stabilization during stress.
Scalability potential: Weak devices can shed derivative math under system stress while still applying P/I stabilization. High/ultra keep full derivative path when stress allows.
Hardware Impact: Removes false dump IO trigger and preserves stabilizer torque. No profiler proof; static proof line route recorded in the report.

## Decision 026: Final Build Proof Must Stay Honest
Problem: The final CPU/compiler gate opened after the last source edits, so leaving the source as "not compiled due gate" was no longer valid evidence.
Solution: Ran exactly one targeted build after CPU sampled `45.21%` and compiler processes were `none`. The build failed with `0 warnings / 57 errors` in external files: `PlayerCriticalProceduralAudioRenderer.cs` and `ModularEquipmentEngine.cs`. No 1420 submarine file appeared in the compiler output.
Rejected Alternatives: Running another build, editing audio/equipment domains to force a green report, or claiming the submarine domain is fully compiled while the project compile wall remains.
Scalability potential: No runtime behavior change. The report remains PENDING VERIFICATION until external compile walls and Unity Editor/PlayMode proof exist.
Hardware Impact: One targeted build cost `00:01:06.77`. No rebuild spam. Final report SHA-256: `A3C740477F5A512C5615917B405DF1E46560285B3EC603E7AF4019AC2FA4D230`.

## Decision 027: PID Telemetry Validator Must Match Runtime Offsets
Problem: The editor layout validator checked private `SubmarinePidTelemetryEntry.StateHash` at offset 12, but the explicit runtime struct places `StateHash` at offset 4 and `Flags` at offset 8. That would make the validator reject the actual source layout during editor reload.
Solution: Updated `SubmarineNavigationLayoutValidator1420.cs` to validate `Frame=0`, `StateHash=4`, `Flags=8`, and preserve the fault-field checks at `116/117/120/124`.
Rejected Alternatives: Changing the runtime struct to match stale proof text, or deleting the private telemetry offset check. The runtime layout was already internally consistent and 128 bytes; the proof artifact was wrong.
Scalability potential: No runtime cost. Low/Middle/High/Ultra devices keep identical telemetry bytes; editor proof now matches ARM64 offset facts.
Hardware Impact: 0 us runtime. Editor reload avoids a false validation failure.

## Decision 028: CSV Profile Import Is A Write Route
Problem: `TryApplyBallastProfilesCsv` writes `CsvScratch` and `Profiles` buffers but used direct `TryLockBuffer` plus mutable resolve, outside the `TryAcquireVaultWrite` proof route. It is editor-cold, but it is still a Vault mutation path.
Solution: Routed both `SubmarineBallastBufferIds.CsvScratch` and `SubmarineBallastBufferIds.Profiles` through `TryAcquireVaultWrite`, then released both with `ReleaseVaultWrite` from `finally`.
Rejected Alternatives: Accepting the path as harmless because it is under `UNITY_EDITOR`, or using direct lock/unlock as a parallel convention. One mutation route is easier to audit and less likely to rot.
Scalability potential: Runtime devices are unaffected because the path is editor-cold. Content authors can still tune ballast profiles without creating a second unmanaged ownership convention.
Hardware Impact: 0 us player runtime. Editor import lock bookkeeping is metadata-only and below measurement relevance.

## Decision 029: Current Source Build Is Gated Off, Not Green
Problem: The latest source changed after the last targeted build attempt. A new compiler check was required for current-source proof, but the final CPU/compiler gate sampled `97.00%` with active `csc:57928` and `dotnet:14652`.
Solution: Did not run `dotnet build`. Updated the report/status to mark current source as not compiled after the latest fixes, while preserving the last known targeted build failure evidence from external files.
Rejected Alternatives: Building under >50% CPU, repeating builds to chase a green report, or editing external audio/equipment domains from the submarine agent.
Scalability potential: No runtime behavior change. This preserves shared-agent hardware and keeps evidence honest.
Hardware Impact: Avoided one targeted build on a saturated CPU. Final JSON report SHA-256: `A3FAE12B46E40EDAB3ACB5DCDFD7EF57FAD5F9FDFDED470FAB7A51E945565B92`.

## Decision 030: Adjacent Kinematic/Gyro Read And Lock Discipline
Problem: The adjacent submarine kinematics and gyroscopic stabilizer runtime still exposed readback paths through legacy mutable `TryReadHandle`, and grouped simulation/gyro buffer locking had no local `finally` proof for partial acquisition failure.
Solution: Renamed the local read helper to `TryReadOnlyVaultHandle<T>` and routed dynamics/gyro public, visual, telemetry, damage, and signal-bridge reads through `IDataVault.TryReadOnlyHandle`. Wrapped invalid-view cleanup in `TryAcquireVaultWriteLock`, grouped simulation acquisition in `LockSimulationBuffers`, and grouped gyro acquisition in `TryLockGyroBuffers` with `finally` release paths.
Rejected Alternatives: Leaving mutable readbacks because current call sites only read, or trusting the caller to clean up a failed grouped lock. The type surface and local lock block now prove the contract instead of relying on convention.
Scalability potential: Low devices fail closed on Vault contention with immutable snapshots; middle/high/ultra keep the same gameplay truth and spend `GlobalQualityWeight` on telemetry stride/visual sync richness, not binary route switches.
Hardware Impact: Read-only handle routing is metadata-equivalent to the previous read path. The extra `finally` boolean guards are sub-microsecond and only execute on grouped lock acquisition; they prevent writer-lock leakage under defrag contention. Final build was skipped honestly: CPU `77.76%`, compiler processes `none`, so the >50% gate remained closed. Final JSON report SHA-256: `F2467E16B982F7AD403510E1F6F9D07979E5C7AC5743765A86F0940EC3C36B17`.

## Decision 031: Adjacent Autopilot Must Not Bypass Vault Locks Or Quality Scalar
Problem: `SubmarineAutopilotSdfNavigator.cs` still had three domain defects: public/cold write routes used raw `TryLockBuffer`/mutable resolve instead of the agent write-lock helper, read routes returned mutable `NativeArray<T>` views, and runtime solver cadence/tuning forced `AuthoritativeQualityWeight` instead of the continuous global scalar.
Solution: Added `TryAcquireAutopilotVaultWrite<T>` and `ReleaseAutopilotVaultWrite<T>`; routed target, profile-hash, route, tuning, cold-default, CSV, initialization, and solver mutations through `IDataVault.TryAcquireWriteLock`; converted observation helpers to `NativeArray<T>.ReadOnly` via `TryReadOnlyHandle`; wrapped grouped initialization/solver acquisitions in `finally`; and resolved runtime quality through `MathLodRuntimeConfig.TryReadLatestConfig` or `HomeostasisBrain.GlobalQualityWeight`.
Rejected Alternatives: Keeping raw buffer locks as an autopilot exception, using binary `isLowEnd` switches, increasing physical hydrodynamics fidelity, or editing external audio/equipment compile blockers from this domain. The correct route is a cheap mock SDF/flow visual-navigation fake whose cost scales continuously.
Scalability potential: Low uses cadence up to 12 frames, 5 feelers, one SDF step, nearest flow lookup, and cheap direction normals. Middle increases feelers/steps/interpolation gradually. High/Ultra runs toward 32 feelers, 12 SDF steps, SDF/flow interpolation, gradient normals, and per-frame solve cadence. BufferID identity, DTO layout, save identity, and authority route do not change with quality.
Hardware Impact: Low-end silicon skips most SDF raymarch work and interpolation; exact microseconds not claimed because profiler/Unity was not run. Final build was skipped honestly: CPU `76.01%`, active compiler process `dotnet:31496`. Final JSON report SHA-256: `E9FE572DAB60249C9E47E7B0584B130D56E300799AD8A7B222EC55D92ED53F93`.

## Decision 032: Scheduled Read-Only Vault Lanes Must Be Pins, Not Writers
Problem: A final lock-semantics audit found several scheduled-job inputs held writer authority even though the jobs only read them: autopilot kinematic/waypoint/SDF/flow/profile lanes, ballast command rows, dynamics hull/tuning/drag LUT rows, and gyro tuning rows. This blocks legitimate owners and overstates mutation authority.
Solution: Split writer locks from relocation read pins. `TryAcquirePinnedJobReadBuffer` pins ballast commands for `EvaluateBallastTanksJob`; `TryAcquireVaultReadPin` pins dynamics hull profiles, added-mass tuning, drag LUT, and gyro tuning; `TryPinAutopilotVaultRead` pins autopilot read-only scheduled inputs. Autopilot tuning is now a short write before scheduling, then released before the job receives the DTO by value.
Rejected Alternatives: Keeping writer locks because they compile, switching to unpinned `TryReadOnlyHandle` for scheduled pointers, or building a heavier physical navigation simulation. Writer locks serialize unrelated owners; unpinned read-only handles are current-phase only and unsafe for job lifetime.
Scalability potential: Low devices fail closed on contention without blocking cold tuning/profile writers longer than needed. Middle/high/ultra keep deterministic truth while spending `GlobalQualityWeight` on more feelers, SDF steps, interpolation, and visual feedback rather than binary device tiers.
Hardware Impact: Adds bounded `TryLockBuffer/TryUnlockBuffer` metadata for read-only job inputs and removes unnecessary writer leases. CPU-gated targeted build passed with `0 Warning(s), 0 Error(s)` in `00:03:16.61`; final report SHA-256: `4FB6AEE1F2695ABF53FE5FBFD3D671B88061B3C594585EAE396C937AD8B0936C`.

## Decision 033: Config Writer Lock Must End Before Scheduled Read Jobs
Problem: `SubmarineDynamicsRuntime.FixedTick` still held `BufferID.SubmarineKinematicConfig` as a writer lease through the scheduled added-mass and integrator jobs, even though the jobs only needed a stable config snapshot after owner-phase `ConsumeSignals` mutated `configs[0]`.
Solution: Keep the writer lock only through `ConsumeSignals`, copy `configs[0]` into `frameConfig`, release via `ReleaseSimulationConfigWriteLock`, and pass `SubmarineKinematicConfig Config` by value into `CalculateAddedMassTensorJob` and `Submarine6DIntegratorJob`.
Rejected Alternatives: Using an unpinned `TryReadOnlyHandle` in scheduled jobs, holding the writer lease until job completion, or adding a heavier native copy buffer. The by-value DTO route removes job lifetime pointer ownership without adding heap/native allocation.
Scalability potential: Low devices release the config lane before the scheduled hydro solve and fail closed on real contention. Middle/high/ultra keep deterministic config truth and spend `GlobalQualityWeight` on analytical sample richness, SDF feelers, interpolation, and visual feedback rather than locking global data longer.
Hardware Impact: Removes one job-lifetime writer lock on `BufferID.SubmarineKinematicConfig`; adds one 128-ish byte unmanaged DTO copy per scheduled dynamics frame. Exact microseconds not claimed because Unity profiler was not run. Current build was skipped honestly: first CPU `64.45%`, compiler processes `none`; final recheck CPU `50.87%` with active `dotnet:68208`; gate stayed closed. Final JSON report SHA-256: `FD695291846037D799CEF20D03D301834052F6733DD99C39744DC02DB303E04B`.
