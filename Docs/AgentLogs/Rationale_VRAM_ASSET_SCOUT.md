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
