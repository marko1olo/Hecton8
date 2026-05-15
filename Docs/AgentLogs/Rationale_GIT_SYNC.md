# Rationale_GIT_SYNC

## Decision 1: Rebase, Not Merge

Problem: Local `main` was behind remote and carried a large local queue.
Solution: Fetched `origin/main`, rebased local work onto remote, then verified `origin/main...HEAD` was left-only `0` and right-only local commits.
Rejected Alternatives: Merge commit was rejected because the user requested clean pull/push hygiene and conflict resolution. Force push was rejected because remote history must not be overwritten.
Scalability potential: Linear history keeps 20+ agent output easier to inspect and bisect.
Hardware Impact: 0us runtime. Git-only operation.

## Decision 2: Exact Path Staging

Problem: Parallel agents and generated reports created multiple dirty tails at different times.
Solution: Used exact `git add -- <paths>` staging per domain commit, separated VRAM, UX, atlas/evidence, and cleanup changes.
Rejected Alternatives: `git add -A` was rejected because it can silently capture unrelated concurrent writes.
Scalability potential: Smaller commits lower conflict blast radius for the next integrator.
Hardware Impact: 0us runtime. Repository hygiene only.

## Decision 3: Evidence Before Push

Problem: A clean git tree alone does not prove generated artifacts or validators are consistent.
Solution: Ran full Tools unittest, focused VRAM/UX validators, AI/hardware/atlas gates, conflict marker scan, unmerged index scan, orphan `.meta` scan, and Python cache scan before push.
Rejected Alternatives: Trusting commit success or static file presence was rejected under the evidence-reporting mandate.
Scalability potential: Downstream agents receive a pushed branch with machine-readable reports and known runtime evidence boundaries.
Hardware Impact: 0us runtime. Unity/runtime profiling remains PENDING VERIFICATION.

## Decision 4: Orphan Meta Cleanup

Problem: Hygiene scan found two orphan first-party Unity `.meta` files with no corresponding asset/directory.
Solution: Removed exact orphan files `Assets/_Project/Art/TEXTURES/VFX.meta` and `Assets/_Project/Scripts/Core/Memory/Layout.meta`, then reran orphan scan to `ORPHAN_META_COUNT 0`.
Rejected Alternatives: Leaving orphan metadata was rejected because it corrupts Unity asset-database hygiene and future imports.
Scalability potential: Cleaner asset metadata reduces false-positive import churn.
Hardware Impact: 0us runtime. Asset database hygiene only.
