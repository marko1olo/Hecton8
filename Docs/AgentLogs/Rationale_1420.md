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
