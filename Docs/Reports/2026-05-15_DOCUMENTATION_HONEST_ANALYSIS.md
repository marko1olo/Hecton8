# 2026-05-15 Documentation Honest Analysis

Date: 2026-05-15
Status: PENDING VERIFICATION
Agent: DOC_HONEST_ANALYSIS
Domain: Documentation Integrity / Echelon 9 Meta, Polish & Integration
Evidence class: FILESYSTEM / STATIC_DOC / STATIC_TEXT_SCAN

This report is not Unity Console, Play Mode, profiler, GCMonitor, player-build, scene-wiring, or visual proof.

## Scope

The pass continued after `DOC_ROOT_CLEANUP` reduced repository root to approved anchors.

Scanned and patched current navigation surfaces:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`
- `Docs/Reports/2026-05-15_COMPUTE_AUDIT/README.md`

Historical dated reports were not rewritten. They remain evidence snapshots unless a current index promotes their claims.

## Findings

### 1. Root Cleanup Holds

Claim: Repository root documentation/evidence scope is reduced to approved anchors.

Evidence Class: FILESYSTEM

Artifact: root file scan for `.md/.log/.json/.xml/.png/.zip/.txt/.py`.

Command: `Get-ChildItem -LiteralPath C:\hades\Hecton8 -File -Force | Where-Object { $_.Extension -in '.md','.log','.json','.xml','.png','.zip','.txt','.py' }`

Date: 2026-05-15

Result: only `AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, and `MASTER_RELEASE_WORK_PLAN.md` remain.

Residual risk: Other agents can create new root files after this snapshot.

### 2. Current Indexes Had Real Stale Root Wording

Claim: Active navigation docs still described root mirrors/evidence as current after the cleanup.

Evidence Class: STATIC_TEXT_SCAN + STATIC_DOC

Artifacts:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`
- `Docs/Reports/README.md`
- `Docs/README.md`

What was corrected:

- Archivarius atlas now points former root `PROJECT_ATLAS.md` and `TERRAIN_AND_BIOME_REALITY_MAP.md` to `Docs/DEPRECATED/Root_Compatibility_Mirrors_2026-05-15/`.
- Archivarius classification root table now lists only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
- `COMPUTE_DOMINANCE_REPORT.md` no longer frames the compute slices as near-root files; it points to `Docs/Reports/2026-05-15_COMPUTE_AUDIT/`.
- `Docs/Reports/README.md` now has a May 15 addendum and treats May 13 broad counters as historical/static orientation where not rerun.
- `Docs/README.md` no longer presents the May 13 root file counts or `BROKEN_PREFABS.md` root placement as current.

Residual risk: Active docs outside the scanned current-navigation set may still contain historical root wording. Those are lower-priority unless a current index points to them as authority.

### 3. Compute Audit Bundle Is Live And Volatile

Claim: The compute audit bundle changed after the root move.

Evidence Class: FILESYSTEM

Artifact: `Docs/Reports/2026-05-15_COMPUTE_AUDIT/`

Command: `Get-ChildItem -LiteralPath Docs\Reports\2026-05-15_COMPUTE_AUDIT -File`

Date: 2026-05-15

Result: current bundle contains `21` files including `README.md`. Two files not listed in the first README pass were present and added to the index:

- `COMPUTE_ENERGY_EQUIVALENTS.md`
- `COMPUTE_LIVE_BURN_PERSISTENCE_CHECK.md`

Residual risk: The bundle can continue changing under the active compute auditor. Treat token/cost numbers inside as snapshot-local unless the report names its timestamp and source database state.

### 4. Broad Counters Are Not Current Truth

Claim: May 13 broad counters remain useful orientation, not current truth after May 15 cleanup and concurrent churn.

Evidence Class: STATIC_DOC + FILESYSTEM

Artifacts:

- `Docs/README.md`
- `Docs/Reports/README.md`
- current root scan

Current narrow counters from this pass:

- root filtered documentation/evidence files: `3`
- direct `Docs/*.md`: `15`
- direct `Docs/Reports/*.md`: `91`
- compute audit bundle files: `21`

Residual risk: This pass did not regenerate a full active documentation manifest. Any claim about total active markdown count, source count, or interface count must run a fresh manifest/source scan first.

### 5. Verification Language Remains Mostly Guarded In Stable Docs

Claim: Stable docs still use proof language, but the sampled stable surfaces pair it with evidence-class boundaries and runtime-proof denial.

Evidence Class: STATIC_TEXT_SCAN

Artifacts:

- `Docs/README.md`
- `Docs/Reports/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/QUALITY_GATES.md`

Result: sampled hits for `0 Warning(s)`, `0 Error(s)`, `Build succeeded`, and smoke labels are generally framed as CLI/source/editor evidence, with explicit denial of Play Mode/profiler/GCMonitor/player-build proof.

Residual risk: This is not a full proof that every active report is honest. Large historical/report surfaces still contain scoped labels like `VERIFIED`; each must be read with its local boundary.

### 6. Known Missing Artifacts Stay Missing

Claim: The May 11 Core build artifacts remain absent and must not be resurrected by prose.

Evidence Class: FILESYSTEM + STATIC_DOC

Artifacts named by stable docs:

- `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`
- `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.log`

Result: Current docs correctly treat those as stale report text. No artifact was restored in this pass.

Residual risk: Some old reports may still cite them inside historical context. That is acceptable only if current indexes demote them.

## Continuation R2 - Archivarius Current/Latest Pointer Cleanup

Claim: Active Archivarius navigation still contained stale May 11 and May 4 "current/latest" routing language after the first pass.

Evidence Class: STATIC_TEXT_SCAN + FILESYSTEM

Artifacts patched:

- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/MASTER_INDEX.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOC_AUTHORITY_CLASSIFICATION.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/CONCEPTUAL_SYSTEM_AUTHORITY_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/DOCSET_COVERAGE_MATRIX.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/Reports/README.md`
- active `01_GENERAL_INFO` domain maps with top-level "current project truth" notes

What was wrong:

- May 11 continuation/manifest were still called latest/current counter or manifest authority in active navigation.
- May 4 actuality sweep was still described as a latest/current counter or current project truth boundary in active read-order notes.
- Some recommended read orders skipped the May 15 honest-analysis report even though it is the current root/index cleanup boundary.

What was done:

- Current documentation/status override now starts at `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.
- Current root/index cleanup now starts at `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md`.
- May 11 continuation and manifest are retained as historical evidence/manifest snapshots.
- May 4 actuality sweep is retained as historical broad context unless rerun.
- Domain maps now tell readers to consume May 13/May 15 before older counter, root-path, or build-artifact claims.

Residual risk: This continuation did not read or rewrite every historical report. It corrected active navigation surfaces found by focused text scan. Runtime remains unverified.

## Continuation R3 - H-Phi Core Graph Debt Cleanup

Claim: Current H-Phi static summary should not rely on stale R49 evidence after concurrent project churn.

Evidence Class: STATIC_SOURCE + CLI_COMPILE

Artifacts:

- `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CurrentStaticSummary.json`
- `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CurrentStaticSummary.exit.txt`
- `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.json`
- `Docs/AgentLogs/HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.exit.txt`
- `Docs/AgentLogs/Build_DOC_HONEST_ANALYSIS_R3_20260515_AfterGprAsmdefPrune_Hecton8Core.log`
- `Docs/AgentLogs/Build_DOC_HONEST_ANALYSIS_R3_20260515_AfterGprAsmdefPrune_Hecton8Core.exit.txt`

What was wrong:

- Fresh H-Phi summary still had `HPhiStaticRisk=0.000636091`, but Core asmdef debt had drifted to `26`.
- Optional unused-reference scan identified `Hecton8.World.GPR` as a high-confidence unused Core asmdef reference.

What was done:

- Removed only `Hecton8.World.GPR` from `Assets/_Project/Scripts/Hecton8.Core.asmdef`.
- Left World GPR runtime source and contracts untouched.
- Re-ran Core graph H-Phi gate: post-prune Core graph debt is `25/10/14/8/6`, and unused Core asmdef candidates are clear.
- Re-ran `Hecton8.Core.csproj` CLI compile: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.

Residual risk: This is not Unity import, Unity Console, Play Mode, profiler, GCMonitor, player-build, frame-time, memory, scene wiring, save/load, or visual proof. It is a narrow static dependency cleanup.

## Regression Model

CPU: No runtime code changed. Runtime CPU delta: `0` claimed.

GC: No runtime code changed. GC delta: `0` claimed; no GCMonitor proof collected.

Memory: No runtime asset or allocation code changed. Memory delta: `0` claimed.

Cadence: No dispatcher/tick/player cadence changed.

Correctness: Documentation navigation improved for current root state. Remaining correctness risk is stale secondary documents outside the patched navigation set.

## Hot Path Impact

None. This pass changed markdown only.

## Failure Modes

- Concurrent agents can add new root or report files after this pass.
- A future agent can read a historical report directly and ignore current indexes.
- `COMPUTE_*` reports can keep changing while the bundle README lags.
- Broad active markdown/source counters can drift within minutes in this workspace.

## Why Kept / Rejected

Kept:

- Historical reports remain in place because they are evidence snapshots.
- May 13 counters remain in docs as historical orientation because they still explain prior conclusions.
- Current root anchors stay in root because governance allows them.

Rejected:

- Rewriting every old dated report.
- Claiming runtime readiness from text scans.
- Claiming microseconds saved without profiler context.
- Creating new root reports.

## Final Boundary

This pass proves a static documentation cleanup and honesty correction only.

Current runtime status remains `PENDING VERIFICATION`.
