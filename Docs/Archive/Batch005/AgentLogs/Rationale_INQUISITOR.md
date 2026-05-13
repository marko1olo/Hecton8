# Rationale: INQUISITOR

## Decision 001: Static audit instead of Unity mutation
Problem: The prompt requires validation only and forbids code writing.
Solution: Run CLI forensic scans, inspect source/log deltas, and write findings to docs only.
Rejected Alternatives: Unity scene mutation and runtime code patches; both violate the Supreme Validator scope.
Scalability potential: Low/Middle/High/Ultra unchanged because this pass does not alter runtime systems.
Hardware Impact: 0 us runtime impact on i3/MX350; audit cost is editor/CLI-only.

## Decision 002: Evidence class downgrade
Problem: Agent logs contain timing and completion claims that may not have profiler or build artifacts.
Solution: Treat `rg`/`Select-String` as text-presence evidence only; mark timing claims without profiler context as HEARSAY / UNVERIFIED.
Rejected Alternatives: Accepting log prose as proof; AGENTS.md and QA mandate forbid that.
Scalability potential: Prevents fake savings from being spent on visual overkill without real budget.
Hardware Impact: Avoids shipping unmeasured hot-path costs to i3/MX350.

## Decision 003: Current-batch prompt source
Problem: `Docs/Tasks/CURRENT_BATCH.md` did not contain `ARCHITECTURAL_INQUISITOR_SENTINEL`.
Solution: Use the user-provided XML block as the operative assignment and record the missing batch prompt as audit context.
Rejected Alternatives: Blocking indefinitely on a missing batch tag; user supplied the full tag in chat.
Scalability potential: None; process integrity only.
Hardware Impact: 0 us runtime impact.

## Decision 004: Conviction threshold
Problem: Static text scans can identify forbidden patterns, but cannot prove runtime GC, frame time, or profiler cost.
Solution: Split findings into code convictions, evidence downgrades, and pending/no-conviction observations.
Rejected Alternatives: Treating every `rg` hit as a hot-path crime or accepting log prose as proof.
Scalability potential: Prevents fake timing budgets from being spent on visual overkill tiers.
Hardware Impact: Protects i3/MX350 and Quest targets from unmeasured claims; audit itself costs 0 runtime us.

## Decision 005: H-Phi spot check scope
Problem: The prompt required H-Phi verification for a specific domain, but the global H-Phi formula is already static and incomplete.
Solution: Apply the existing static formula to `Assets/_Project/Scripts/Narrative/Campaign` and record counts exactly: 5 signal operations, 12 `GlobalRegistry.` refs, 10 `NativeArray` refs, 0 `GlobalDataVault` refs, `HphiStatic=0`.
Rejected Alternatives: Inventing a new runtime consciousness score without compile/runtime evidence.
Scalability potential: Exposes low data-sovereignty density before teams spend saved cycles on high-tier presentation claims.
Hardware Impact: 0 runtime us; this is CLI-only analysis.

## Decision 006: Burst Unity API scan discipline
Problem: Files may contain both `[BurstCompile]` and Unity object APIs without the Burst job itself calling Unity APIs.
Solution: Use same-file scans only as candidates, then perform a bounded 80-line block scan after each `[BurstCompile]`; issue no direct Burst API conviction when the block scan found no object calls.
Rejected Alternatives: Convicting based on same-file co-occurrence.
Scalability potential: Keeps platform report factual and prevents false repair orders.
Hardware Impact: 0 runtime us.

## Decision 007: Thermal contract attribution
Problem: `HardwareThermalSnapshot` and related interfaces appear in current Core.Contracts diff, but no `LOG_THERMAL_THROTTLING_DIRECTOR.md` exists.
Solution: Attribute the conviction to `THERMAL_THROTTLING_DIRECTOR` based on matching status/rationale domain and missing final evidence, while recording the exact source diff facts.
Rejected Alternatives: Leaving unaligned hardware contract drift unreported because authorship metadata is incomplete.
Scalability potential: Thermal policy is core to low-end survival and high-end visual rollback; contract drift here is not harmless.
Hardware Impact: Prevents unverified thermal throttling policy from silently controlling Quest/i3/MX350 load shedding.
