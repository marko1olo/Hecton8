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

## Decision 11: Current Batch Rebase Conflict Split

Problem: Rebase over fresh `origin/main` hit an add/add conflict in `Docs/Tasks/CURRENT_BATCH.md`.
Solution: Kept the remote/current batch file as the active directive and preserved the local auxiliary batch snapshot at `Docs/Archive/Batch_GIT_SYNC_REBASE/CURRENT_BATCH_local_auxiliary_20260517.md`.
Rejected Alternatives: Combining two active batch files was rejected because neighboring agent prompts must not contaminate the current batch directive. Dropping the local file was rejected because it contained prior integration evidence.
Scalability potential: Active batch remains single-source while archived evidence stays inspectable for later audit.
Hardware Impact: 0us runtime. Git-only operation.

## Decision 12: Post-Rebase Artifact Regeneration

Problem: Rebase changed source inputs underneath generated artifacts; `VerifyLore.py --check` and `AiBattleSim.py --check-artifacts --verify-rerun` failed against stale committed outputs.
Solution: Regenerated the lore blob/manifest from current Markdown sources and reran the AI battle simulator so the report matches the current `H8Memory.cs` buffer catalog (`knownBufferCount=476`).
Rejected Alternatives: Committing stale generated artifacts or editing reports by hand was rejected because both validators have deterministic owner tools.
Scalability potential: Downstream agents receive generated artifacts that match the rebased source tree rather than the pre-rebase source tree.
Hardware Impact: 0us runtime. Offline data packaging and simulation evidence only.

## Decision 13: Binary Hygiene Payload Scope

Problem: Binary hygiene was treating diagnostic `.binlog` files as product binary payloads because it matched any filename containing `.bin`.
Solution: Restricted the scan to actual `.bin` and `.h8bin` suffixes, then aligned `Data/Balance/Baked/Babel_Dictionary.h8bin` to 1296 bytes and reran binary hygiene/data-truth gates.
Rejected Alternatives: Padding arbitrary diagnostic `.binlog` evidence was rejected because build logs are not runtime binary payloads. Suppressing all `Docs/AgentLogs` was rejected because crash dumps named `.bin` still need alignment visibility.
Scalability potential: The gate now rejects real runtime binary payload misalignment without failing on unrelated compiler diagnostic containers.
Hardware Impact: 0us runtime. Static tooling only; Unity runtime remains PENDING VERIFICATION.

## Decision 14: Fast-Forward Pull Before Commit

Problem: Fresh fetch showed `origin/main` advanced by one commit (`edc1f7149`) while local `main` was clean.
Solution: Used `git pull --ff-only origin main`, producing a linear fast-forward with no merge commit and no unmerged index entries.
Rejected Alternatives: Merge pull was rejected because there were no local commits to merge. Force operations were rejected because remote history was authoritative.
Scalability potential: Keeps the shared branch linear for the next integrator and avoids synthetic conflict surfaces.
Hardware Impact: 0us runtime. Git-only operation.

## Decision 15: Owner-Tool Generated Artifact Repair

Problem: Post-pull full unittest exposed stale generated AI and lore artifacts: AI battle report had `knownBufferCount=476` while current `H8Memory.cs` parsed 523 buffers, and the lore blob lacked the current raw H8LR record for `DeepReach_ColonyFailureArchive`.
Solution: Regenerated `Tools/AiBattleSim_Report.json` with `Tools/AiBattleSim.py` and regenerated the lore package with `Tools/VerifyLore.py --check --hash-audit`, then reran focused and full test gates to PASS.
Rejected Alternatives: Manual JSON/binary edits were rejected because deterministic owner tools exist. Pushing the fast-forwarded batch without repair was rejected because full unittest was red.
Scalability potential: Committed generated data now tracks the current source and memory-buffer catalog, reducing repeated red gates for downstream agents.
Hardware Impact: 0us runtime. Offline data/tooling only; Unity runtime remains PENDING VERIFICATION.

## Decision 16: Stable Sliced Commits During Live Agent Churn

Problem: Parallel agents kept appending logs and generated reports while staging, causing short-read index failures and new unstaged tails after each pass.
Solution: Committed stable staged slices as `946f5595b`, `b73c8ec34`, and `8096669c4`, then reran owner gates that touch the generated reports before the final evidence commit and push.
Rejected Alternatives: `git reset`, force-push, or broad destructive cleanup were rejected. Waiting indefinitely for every parallel writer to stop was rejected because the user requested commit/pull/push progress.
Scalability potential: Sliced commits preserve live-agent work without blocking the shared branch forever on report churn.
Hardware Impact: 0us runtime. Git/tooling evidence only; Unity runtime remains PENDING VERIFICATION.
