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

## Decision 5: Final Remote Advancement Rebase

Problem: The last pre-push fetch showed `origin/main` had advanced by 5 commits after local validation.
Solution: Rebasing the full local queue over fresh `origin/main` completed without conflicts; post-rebase divergence was `0 126`, then full Tools unittest and artifact gates were rerun.
Rejected Alternatives: Pushing against stale remote was rejected. Merge commit was rejected for history hygiene.
Scalability potential: Keeps local agent output linear on top of the newest integrator state.
Hardware Impact: 0us runtime. Git-only operation; Unity runtime remains PENDING VERIFICATION.

## Decision 6: Remote Push Verification

Problem: Evidence files created before push still showed remote verification pending.
Solution: After `git push origin main`, fetched `origin/main` and verified local and remote hashes matched at `68c2fe0a388ae7371aa8f70930859207c12364f7` with divergence `0 0`.
Rejected Alternatives: Ending with chat-only push proof was rejected because the repository must carry the handoff evidence.
Scalability potential: Integrators can verify from files that the queue reached remote before this final evidence update.
Hardware Impact: 0us runtime. Git-only operation.

## Decision 7: No Self-Referential Commit Hash In Evidence File

Problem: Recording the exact pushed commit hash inside the evidence file creates a self-referential loop: committing the file changes the hash it tries to record.
Solution: Record the deterministic verification command sequence in the committed file and perform the exact hash equality check after the evidence commit is pushed.
Rejected Alternatives: Amending forever to chase the file's own commit hash was rejected as process churn. Leaving an older exact hash in status was rejected as stale evidence.
Scalability potential: Future GIT_SYNC runs can verify remote equality without creating a new evidence-only hash mismatch each time.
Hardware Impact: 0us runtime. Git-only operation.

## Decision 8: Validate Before Batch Commit

Problem: The working tree contained 600+ local paths from parallel agents, and the first targeted test pass found red tooling gates.
Solution: Fixed the blocking offline validator state before commit: the H8 hash verifier was returned to the established API contract, the lore package was regenerated from the current two Markdown sources, and the `.h8bin` payload was padded to a 16-byte file boundary required by binary and net-sync gates.
Rejected Alternatives: Committing known-red tests or bypassing binary alignment checks was rejected. Force-pushing or rewriting remote history was rejected.
Scalability potential: A single validated integration commit gives downstream agents a consistent data/tooling baseline instead of a dirty local batch.
Hardware Impact: 0us runtime. Tooling/data packaging only; Unity runtime remains PENDING VERIFICATION.

## Decision 9: Generated Pathspec Staging

Problem: The batch mixed tracked edits and hundreds of untracked artifacts, including non-ASCII task filenames.
Solution: Generated exact pathspec files from `git diff --name-only` and `git ls-files --others --exclude-standard`, staged `37` tracked paths and `626` untracked paths, then removed the temp pathspec files.
Rejected Alternatives: `git add -A` was rejected because broad staging can capture unrelated concurrent writes. Manual shell glob staging was rejected because it is error-prone with non-ASCII and spaces.
Scalability potential: Exact staged evidence makes future conflict analysis and audit easier.
Hardware Impact: 0us runtime. Git-only operation.

## Decision 10: Whitespace Gate Cleanup Before Commit

Problem: `git diff --cached --check` rejected new `.meta`, Markdown, and log artifacts for trailing whitespace.
Solution: Cleaned only paths named by `git diff --cached --check`, normalized the one progress-log `.out` carriage-return stream, re-staged those exact paths, and reran the check to PASS.
Rejected Alternatives: Ignoring `diff --check` warnings was rejected because the requested push must be mechanically clean.
Scalability potential: Cleaner text artifacts reduce avoidable review noise and future patch failures.
Hardware Impact: 0us runtime. Repository hygiene only.
