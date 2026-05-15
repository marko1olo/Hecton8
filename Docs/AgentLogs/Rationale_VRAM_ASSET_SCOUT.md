# Rationale_VRAM_ASSET_SCOUT

Active rationale file recreated after active VRAM task files were moved to `Docs/Archive/Batch006` during this continuation. This file records current active-batch decisions only.

## Decision 34: Split Redline Flag Payload Validation

Problem: Split report identity checks proved redline paths belonged to the broad CSV, but they still did not prove the redline `flags` payload matched the broad CSV `redline_flags`. A stale remediation queue with correct paths but obsolete risk labels could pass.
Solution: Extend `validate_generated_reports()` to compare texture, mesh, and RenderTexture split redline `flags` against the matching broad CSV `redline_flags`.
Rejected Alternatives: Path-only validation. Paths are necessary but not sufficient because asset owners act on the flag payload.
Scalability potential: Low/MX350 remediation queues now retain exact risk labels; Middle/High/Ultra report growth remains valid after regeneration if broad and split artifacts agree.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` still passes; unit coverage remains 17 tests and ran in 6.553 seconds.

## Decision 35: Broad Redline Set Parity

Problem: The validator compared split CSVs and JSON, but did not explicitly prove the broad CSV's non-empty `redline_flags` sets matched the split remediation queue path sets.
Solution: Add broad redline set derivation for texture, mesh, and RenderTexture rows, compare those counts to JSON counters, and require split CSV path sets to equal the broad redline path sets.
Rejected Alternatives: Trusting split CSV parity alone. The broad CSV is the canonical inventory, so its redline set must agree with every derived artifact.
Scalability potential: Low/MX350 remediation queues now stay mechanically tied to both broad inventory and split queues; higher tiers can change report sizes after regeneration without weakening parity checks.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` still passes; unit coverage remains 17 tests and ran in 9.753 seconds.

## Decision 36: Markdown Report Drift Guard

Problem: The no-scan validator protected CSV and JSON artifacts, but the human-facing summary and remediation plan could go stale while machine artifacts remained valid.
Solution: Extend `validate_generated_reports()` to read `VRAM_Budget_Audit_Summary.md` and `VRAM_Remediation_Plan.md`, requiring evidence boundary text, scan-root text, key counts, gate text, and remediation priority headings to match the current JSON/broad CSV state.
Rejected Alternatives: Trusting Markdown as presentation-only. The CTO and asset owners read Markdown, so stale Markdown is operationally dangerous even if JSON is correct.
Scalability potential: Low/MX350 gates and human cleanup queues now share the same validated numbers; higher-tier content changes remain valid after regeneration if machine and Markdown artifacts stay aligned.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` still passes; unit coverage remains 17 tests and ran in 3.686 seconds.

## Decision 37: JSON Payload Parity

Problem: `--validate-reports` proved CSV split queues and broad CSV redline rows agreed, but JSON `mesh_redlines` and `render_textures` payloads could still drift while keeping matching counts. Downstream agents consume JSON first, so stale JSON risk labels or RenderTexture estimates are a real handoff fault.
Solution: Compare JSON mesh redline paths and flags against the mesh redline CSV, compare JSON RenderTexture paths, flags, dimensions, and estimates against the broad CSV, and add a synthetic regression test that mutates JSON flags/estimates and expects validation failure.
Rejected Alternatives: Count-only JSON validation. Counts catch missing rows but do not catch stale flags, dimensions, or MiB estimates.
Scalability potential: Low/MX350 queues now preserve exact redline labels and static RT estimates for cheap-device remediation. Middle/High/Ultra JSON consumers can trust machine payloads after report regeneration without re-parsing every CSV manually.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` passes current artifacts; unit coverage is now 18 tests and explicitly rejects stale JSON payload drift.

## Decision 38: CSV Schema And Evidence-Class Guard

Problem: The no-scan validator accepted loose CSV schemas as long as a few consumed columns existed. A broad or split report could lose evidence-boundary columns or mutate `evidence_class` while still passing count and payload parity checks.
Solution: Added exact header contracts for the broad audit CSV, all split redline CSVs, and RenderTexture hotspot CSV. Added evidence-class checks for broad CSV rows and RenderTexture hotspot rows, plus tests for schema drift and evidence-class drift. Regenerated the VRAM report artifacts after validation exposed stale missing `texture_redlines` JSON payloads.
Rejected Alternatives: Required-column-only validation. It is not enough for CTO handoff because missing columns silently weaken downstream tooling and false runtime evidence claims can enter the reports.
Scalability potential: Low/MX350 remediation queues now keep a stable machine-readable schema; Middle/High/Ultra report consumers can parse without defensive guessing after regeneration.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` passes current artifacts; unit coverage is now 21 tests and rejects CSV schema/evidence-boundary drift.

## Decision 39: Texture JSON Redline Detail Parity

Problem: The active status claimed regenerated JSON texture redline payloads, but the no-scan validator still only proved texture redline counts and CSV-to-broad flag parity. A stale JSON `texture_redlines` payload could keep obsolete dimensions, first-party markers, BC7 estimates, or flags while CSV reports were correct.
Solution: Add full JSON `texture_redlines` payload generation and validation. `--validate-reports` now compares texture redline path sets, flags, width, height, full-mip BC7 estimate, and first-party production markers against `VRAM_Texture_Redlines.csv`.
Rejected Alternatives: Relying on broad CSV parity. The JSON report is consumed by downstream agents and needs the same detail-level proof as split CSVs.
Scalability potential: Low/MX350 remediation queues now carry exact texture risk payloads in the machine-readable report; Middle/High/Ultra can regenerate larger reports without weakening downstream JSON consumers.
Hardware Impact: 0us runtime measured. Tooling impact: `--validate-reports` passes current artifacts; unit coverage remains 21 tests and now explicitly rejects stale texture JSON payload drift.

## Decision 40: JSON Authority Drift Regression

Problem: The validator already rejected JSON `schema_version`, `evidence_class`, `scan_root_names`, and `ci_expected_exit_code` drift, but the tests did not include a fixture that falsified JSON authority fields directly.
Solution: Add a regression test that loads the current generated JSON report, mutates `evidence_class` to a false runtime claim and `ci_expected_exit_code` to an impossible green value, then verifies both errors are reported.
Rejected Alternatives: Relying on implementation inspection. Authority fields are evidence-boundary claims and require a failing fixture, not code-review memory.
Scalability potential: Low/MX350 reports cannot silently claim runtime profiler evidence or green CI while redlines remain; Middle/High/Ultra payload consumers inherit the same authority guard.
Hardware Impact: 0us runtime measured. Tooling impact: unit coverage is now 22 tests and rejects JSON authority drift.

## Decision 41: JSON Derived Counter And Gate-Reason Parity

Problem: JSON authority fields were guarded, but derived remediation counters could still drift from the broad CSV while keeping raw asset counts valid. A stale `gate_reasons` list could also claim green CI despite redline rows.
Solution: Recompute texture crime rows, texture container risks, streaming mip risks, mesh import-risk categories, first-party subsets, RenderTexture depth/stencil risk rows, and expected gate reasons from CSV rows inside `validate_generated_reports()`. Add a regression fixture that mutates those JSON counters and proves validation fails.
Rejected Alternatives: Trusting generated JSON counters because the generator created them. The no-scan validator must defend handoff artifacts after manual edits, partial regeneration, or stale merges.
Scalability potential: Low/MX350 remediation queues keep exact risk totals for the hard budget gate; Middle/High/Ultra can add larger report payloads without allowing stale machine-readable counters.
Hardware Impact: 0us runtime measured. Tooling impact: unit coverage now rejects derived counter and gate-reason drift.

## Decision 42: JSON Budget Aggregate Parity

Problem: Derived risk counters were guarded, but JSON budget constants and MiB aggregates could still drift from the broad CSV. A stale `critical_vram_overflow` flag could hide the MX350 texture-pool overflow even when CSV bytes prove it.
Solution: Recompute BC7 full-mip totals, runtime texture totals, first-party texture totals, mesh geometry totals, first-party mesh totals, RenderTexture totals, budget constants, and critical overflow state inside `validate_generated_reports()`. Add a regression fixture that mutates those JSON aggregates and proves validation fails.
Rejected Alternatives: Trusting generated aggregate fields because the current generator writes them. Handoff artifacts must survive manual edits, stale merges, and partial report regeneration.
Scalability potential: Low/MX350 keeps hard-budget overflow evidence tied to CSV bytes; Middle/High/Ultra can consume higher-budget aggregate reports without accepting stale machine-readable totals.
Hardware Impact: 0us runtime measured. Tooling impact: unit coverage is now 24 tests and rejects JSON budget aggregate drift.

## Decision 43: JSON Derived List Parity

Problem: Scalar counters and aggregate MiB values were guarded, but JSON list payloads for non-first-party runtime directory pressure, texture extension pressure, mesh extension pressure, atlas suggestions, and RenderTexture source hotspots could still drift from the CSV/static evidence. Downstream agents use those lists to prioritize asset cleanup, so stale ordering or stale members would misroute work.
Solution: Recompute those derived JSON lists inside `validate_generated_reports()` from the broad CSV and RenderTexture hotspot CSV, including list order, MiB totals, counts, atlas members, and source snippets. Add a regression fixture that mutates each list family and proves validation rejects the drift.
Rejected Alternatives: Count-only validation. Counts prove size, not payload identity, priority ordering, or row membership.
Scalability potential: Low/MX350 cleanup queues now keep exact directory, extension, atlas, and RT hotspot priorities tied to static evidence; Middle/High/Ultra can consume larger reports after regeneration without accepting stale list payloads.
Hardware Impact: 0us runtime measured. Tooling impact: unit coverage is now 26 tests and rejects JSON derived list drift.
