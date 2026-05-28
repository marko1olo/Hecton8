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
Solution: Reran the Zero-GC scan with `-CaseSensitive`, producing 0 matches for `new Native*`, `Allocator.Persistent`, `foreach`, LINQ `.Where/.Select/.ToList/.ToArray`, `string.Format`, `.ToString`, interpolated strings, and literal string concatenation. Final report artifact hash is `312A38081912E6FE8E9227873AD7C7C453DD4700D067473411D34D08148733B5`.
Rejected Alternatives: Editing the runtime to silence a false positive, or launching a build while CPU/process gates were closed.
Scalability potential: No runtime cost. Keeps Low/Middle/High/Ultra truth routes unchanged and preserves continuous `GlobalQualityWeight` scaling instead of binary quality switches.
Hardware Impact: 0 us runtime. Build avoided on host with active `dotnet` PID 24928; final CPU sample was 64.92%, so both CPU and active-dotnet rules blocked a new build.

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
