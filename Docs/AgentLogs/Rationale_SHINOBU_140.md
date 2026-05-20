# SHINOBU_140 Rationale

Status: PENDING VERIFICATION

## Decision - Self-Audit Count Drift Lock

Problem: `SHINOBU_140_SELF_AUDIT.xml` could remain parseable while its debt counters drifted from the generated static gate summary. This happened on Task 14: the XML carried `Compile_Wall criticalDebt=119` while the current scanner summary carried `118`.

Solution: Extend `Self_Audit_Proof` to compare Hygiene totals and mapped task debt rows against `Docs/Reports/SHINOBU_140_STATIC_GATE_SUMMARY.json`. Add `test_live_self_audit_counts_match_static_summary` to the executable Python self-test harness and sync XML to `2103/182`, Task 14 `118`, self-tests `4`.

Rejected Alternatives: Manual XML edits without a gate were rejected because they would rot again. Raising the frozen baseline was rejected because no scanner regressed. Running `dotnet build` was rejected because this was Python/report proof work and the user forbade build/rebuild unless technically required.

Scalability potential: Governance only. Low-tier runtime cost is unchanged; the benefit is earlier routing of architecture debt before it becomes frame-time or compile-wall damage.

Hardware Impact: `0 us` player runtime. Python static verification only; no Roslyn/MSBuild load.

Current result: static gate remains red on legacy debt at `2103/182`, no-regression remains `0/0`, self-tests are `4/4 PASS`, H-Phi embeds the updated values.

## Decision - Hot Helper Reachability Gates

Problem: The hot-path `GlobalRegistry` and mid-frame `JobHandle.Complete()` scanners only caught direct calls inside hot methods. A hot method could delegate to `ResolveLiveService()` or `CompleteHiddenJob()` and still execute the same forbidden authority lookup or sync fence during the frame without tripping the gate.

Solution: Add helper-reachability scanners `Hot_Helper_Registry_Polling` and `Hot_Helper_Complete`. The scanner extracts method blocks, finds non-hot helpers containing `GlobalRegistry.` or unmarked `.Complete(`, and reports hot/mid-frame callers that invoke those helpers. The self-audit count model now derives expected task debt from the current in-memory scanner results and synchronizes XML counts before validating them.

Rejected Alternatives: Full Roslyn call-graph analysis was rejected for this CI fallback because it would drag compiler/MSBuild cost into a static text gate and violate command discipline. Directly editing the 269 helper findings across UI/World/Atmosphere/Core/etc. was rejected because SHINOBU_140 owns the integration gate, not those runtime domains. Raising old baselines was rejected; only the two new scanner rows were seeded at their first measured debt.

Scalability potential: Governance only. Low-tier devices benefit indirectly because hidden boot/service polling and hidden frame sync fences are now surfaced before they become frame-time spikes. High/ultra devices keep the same routing but can spend recovered CPU on presentation overkill once owners remove their debt.

Hardware Impact: `0 us` player runtime. Python static verification only; no Roslyn/MSBuild load. Current gate is red at `2303/182`, with regressions `Burst_Job_Directives +8` and `Hot_Helper_Registry_Polling +3`.
