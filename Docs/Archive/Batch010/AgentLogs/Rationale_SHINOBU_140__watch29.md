# SHINOBU_140 Rationale

Status: PENDING VERIFICATION

## Decision - Presentation Suppression Vault Route

Problem: Rollback visual sync was fenced and netcode audio suppression existed, but particles had no shared unmanaged suppression fact.

Solution: Add dispatcher-owned Vault buffer `70626` and explicit 32-byte `DispatcherPresentationSuppressionDTO`. Dispatcher writes one row before visual sync with rollback/health-pressure suppression flags and continuous `GlobalQualityWeight`.

Rejected Alternatives: A new global `SignalBus<T>` lane, direct VFX/Audio/Networking references, and dispatcher-owned rollback resimulation loop were rejected. Netcode already owns restore/resimulation through `RollbackFixedPipelineJob.ExecuteRollback()` and `HeadlessResimulationCommandJob`; duplicating that loop would double-run side effects.

Scalability potential: Low devices collapse rollback presentation to one 32-byte fact and skipped visual sync; higher devices can resume visual overkill when the same scalar clears.

Hardware Impact: One Vault overwrite per visual-sync decision, 0 private native arrays, 0 managed hot-path allocation.

## Decision - Ledger Proof

Problem: Adding buffer `70626` without architecture documentation would hide a payload route.

Solution: Update `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with SHINOBU_140 buffer IDs `70620..70626`, DTO layouts, rollback probe boundary, and verification status.

Rejected Alternatives: Chat-only or status-only proof.

Hardware Impact: Documentation only.

## Decision - Build Gate Recheck

Problem: Task 20 needs compile evidence, but the current workstation CPU load is above the explicit build gate.

Solution: Sample the gate and refuse to launch `dotnet build` when `CPU_AVG=88.33`, even though no `dotnet` or `csc` process is active.

Rejected Alternatives: Launching a targeted build under CPU pressure was rejected because the user explicitly forbade it. Claiming static proof as compile proof was rejected.

Scalability potential: None in player runtime. This protects the shared multi-agent workstation from compile contention.

Hardware Impact: Avoided adding compiler load to a saturated CPU. Runtime proof remains pending.

## Decision - Self-Audit Proof Gate

Problem: A forensic XML file can rot silently if the scanner summary and H-Phi artifact do not validate it.

Solution: Add `Self_Audit_Proof` to the Python CI fallback scanner. The gate parses `Docs/Reports/SHINOBU_140_SELF_AUDIT.xml`, validates the SHINOBU_140 agent id, declared task count, exact task id set, and required proof sections.

Rejected Alternatives: Treating the XML as passive documentation was rejected because Task 20 requires proof, not prose. Failing the whole run because global legacy debt remains was rejected; the scanner already exits non-zero while preserving per-gate evidence.

Scalability potential: Governance only. It keeps the first-20-minutes architecture blockers visible without player runtime cost.

Hardware Impact: 0 us player runtime. CI fallback scan now carries one extra XML parse.

## Decision - Red Debt Regression Budget

Problem: The architecture gate is red, so a normal non-zero exit does not distinguish "old debt still exists" from "new debt got worse." That lets agents add Burst/DTO/Vault debt without a crisp regression signal.

Solution: Add `Static_Gate_Regression` to `Tools/RunShinobu140StaticScanners.py` and freeze current per-scanner budgets in `Docs/Reports/SHINOBU_140_STATIC_GATE_BASELINE.json`. The new gate fails if any scanner's critical or warning count exceeds that baseline.

Rejected Alternatives: Updating the baseline after the first regression was rejected because it would normalize new debt. Treating all red debt as equivalent was rejected because it hides direction of travel.

Scalability potential: Governance only. Low-end and high-end runtime behavior is unchanged, but compile/Burst debt now has a no-regression tripwire before it becomes frame-time damage.

Hardware Impact: 0 us player runtime. CI fallback adds one small JSON parse and 11 row comparisons.

Current result: `Static_Gate_Regression=0/0`; no scanner currently exceeds the frozen red-debt baseline.

## Decision - Owner Map Artifact

Problem: Red debt without owner grouping turns the scanner into a wall of findings and encourages cross-domain edits by the integrator.

Solution: Add `Docs/Reports/SHINOBU_140_STATIC_GATE_OWNER_MAP.json` generation to the CI fallback scanner. The map groups findings by scanner, path, and domain hint so owners can burn their own debt.

Rejected Alternatives: Directly editing RootScripts/World/Core/GamePlay debt from SHINOBU_140 was rejected because it violates owner-local boundaries and would collide with active agents.

Scalability potential: Governance only. It improves remediation routing for low-end frame-time risks without player runtime cost.

Hardware Impact: 0 us player runtime. CI fallback performs in-memory grouping over existing findings after the scan.

## Decision - Build Gate Recheck After Owner Map

Problem: C# compile proof remains desirable, but the CPU gate is fully closed.

Solution: Do not launch `dotnet build` at `CPU_AVG=99.67`, even though no compiler process is active.

Rejected Alternatives: Running a build at saturated CPU was rejected as a direct command-discipline violation.

Scalability potential: None in player runtime. This protects the shared workstation from compile contention.

Hardware Impact: Avoided adding Roslyn/MSBuild load to a saturated CPU.

## Decision - Evidence Drift Resync

Problem: The latest scanner pass changed the red-debt totals after H-Phi embedding, but the fallback status, rationale, ledger, and XML still carried older counts.

Solution: Sync documentation evidence to the generated reports from that pass without changing the frozen baseline: total critical `2607`, warnings `182`, and regression `Burst_Job_Directives 646 > 645`.

Rejected Alternatives: Raising the baseline was rejected because it would normalize new Burst directive debt. Running `dotnet build` was rejected because this loop only changed evidence/doc artifacts and the user explicitly forbade rebuild unless technically required.

Scalability potential: Governance only. Owner-domain routing improves low-tier risk burn-down without touching runtime.

Hardware Impact: 0 us player runtime. No compiler load launched.

## Decision - Regression Attribution Artifact

Problem: A red no-regression row without owner attribution still forces reviewers to open the full scanner JSON and manually infer domain ownership.

Solution: Add `Docs/Reports/SHINOBU_140_STATIC_GATE_REGRESSION_ATTRIBUTION.json`, generated by the SHINOBU scanner and embedded into H-Phi. The artifact lists only scanners exceeding the frozen baseline, with top owner domains and paths. `Static_Gate_Regression` is excluded from comparing against itself.

Rejected Alternatives: Editing World/RootScripts/Core debt directly was rejected because it violates owner-local boundaries. Raising the frozen baseline was rejected because it would hide regressions. Running `dotnet build` was rejected because this is Python/report evidence work and no compile proof was required.

Scalability potential: Governance only. It reduces burn-down latency for low-end frame risks by routing debt to the right owners without touching player runtime.

Hardware Impact: 0 us player runtime. Python-only report generation; no Roslyn/MSBuild load.

Current result: `Static_Gate_Regression=0/0`, total static debt `2174/182`, H-Phi embeds empty regression attribution because no scanner exceeds the baseline.

## Decision - Scanner Self-Test Harness

Problem: The static analyzers were red/green reporting tools without executable guardrails. A future edit could weaken hot-registry, mid-frame-complete, Burst, layout, self-audit, or regression-attribution detection while still emitting JSON.

Solution: Add `Tools/TestShinobu140StaticScanners.py` with synthetic fixtures and a durable report at `Docs/Reports/SHINOBU_140_SCANNER_SELF_TESTS.json`. Embed the self-test result into H-Phi.

Rejected Alternatives: Relying on scanner output alone was rejected because it proves current source text only, not scanner correctness. Unity/dotnet build was rejected because this is Python static-tool work and the user explicitly forbade build/rebuild unless required.

Scalability potential: Governance only. It protects low-tier frame-time remediation by keeping the static gates trustworthy before owners touch runtime debt.

Hardware Impact: 0 us player runtime. Python-only CI fallback; no Roslyn/MSBuild load.

Current result: self-tests `PASS`, total static debt `2128/182`, no-regression gate `0/0`.

## Decision - Self-Audit Count Drift Lock

Problem: `SHINOBU_140_SELF_AUDIT.xml` could remain parseable while its debt counters drifted from the generated static gate summary. Concrete drift was present: Task 14 carried `Compile_Wall criticalDebt=119`, while the current generated summary carried `118`.

Solution: Extend `Self_Audit_Proof` to compare Hygiene totals and mapped task debt rows against `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json`. Add `test_live_self_audit_counts_match_static_summary` to the executable Python self-test harness and sync XML to `2103/182`, Task 14 `118`, self-tests `4`.

Rejected Alternatives: Manual XML edits without an analyzer gate were rejected because they would rot again. Raising the frozen baseline was rejected because no scanner regressed. Running `dotnet build` was rejected because this was Python/report proof work and the user forbade build/rebuild unless technically required.

Scalability potential: Governance only. Low-tier runtime cost is unchanged; the benefit is earlier routing of architecture debt before it becomes frame-time or compile-wall damage.

Hardware Impact: `0 us` player runtime. Python static verification only; no Roslyn/MSBuild load.

Current result: self-tests `4/4 PASS`, total static debt `2103/182`, no-regression gate `0/0`, H-Phi embeds updated values.

## Decision - Hot Helper Reachability Gates

Problem: Direct hot-path scanners missed helpers that hide forbidden work. A hot method could call `ResolveLiveService()` or `CompleteHiddenJob()`, and those helpers could contain `GlobalRegistry.` or unmarked `.Complete(` while direct hot rows stayed green.

Solution: Add helper-reachability scanners `Hot_Helper_Registry_Polling` and `Hot_Helper_Complete`. The scanner extracts method blocks, finds non-hot helpers containing forbidden calls, and reports hot/mid-frame callers that invoke them. Self-audit counts now derive from the current in-memory scanner results before validation.

Rejected Alternatives: Full Roslyn call-graph analysis was rejected because this fallback gate must not launch compiler/MSBuild load. Directly editing owner-domain debts was rejected because SHINOBU_140 owns the integration gate, not UI/World/Atmosphere/Core runtime code. Raising old baselines was rejected; only the two new helper rows were seeded at first measurement.

Scalability potential: Governance only. Low-tier devices benefit once owners remove hidden frame polling/sync fences; high/ultra devices can spend recovered CPU on visual overkill after debt burn-down.

Hardware Impact: `0 us` player runtime. Python static verification only. Current gate is red at `2303/182`, with regressions `Burst_Job_Directives +8` and `Hot_Helper_Registry_Polling +3`.
