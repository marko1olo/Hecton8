# Rationale_DOC_ARCHIVE_BATCH007

Problem: Batch007 active `Docs/Tasks` and `Docs/AgentLogs` need archival after Git synchronization, without corrupting concurrent agent work or inventing runtime dependencies.

Solution: Use the Batch006 archive layout, create `Docs/Archive/Batch007`, move only direct active folder children into matching archive folders after path-boundary validation, and generate two `.md/.txt`-only combined documents with explicit source boundaries.

Rejected Alternatives: Destructive cleanup was rejected. Flat archive was rejected because it destroys Tasks/AgentLogs provenance. Boundary-free concatenation was rejected because evidence reports must list scanned files. Force push/reset was rejected because shared remote history is authoritative.

Scalability potential: Low keeps active task/log folders small for cheap search. Middle preserves per-agent forensic files. High/Ultra gets combined grep documents without losing original file granularity.

Hardware Impact: Runtime impact is 0 us/frame. This is repository/documentation hygiene only; no Unity performance gain is claimed.

Evidence Class: GIT_CLI / FILESYSTEM / STATIC_DOC. Runtime verification remains PENDING VERIFICATION.

## Decision 1: Pre-Archive Push Despite `.meta` Whitespace Debt

Problem: The user explicitly required GitHub pull/push/commits before archiving, while `git diff --cached --check` reported trailing whitespace in Unity-generated `.meta` files.

Solution: Commit and push the current batch state as `dc2753111`, record the whitespace failure as hygiene debt, and avoid altering generated `.meta` content during the Git sync phase.

Rejected Alternatives: Blocking the archive on broad `.meta` whitespace rewrites was rejected because it would touch generated Unity import metadata outside the archive task. Force push/reset was rejected.

Scalability potential: Low/Middle keeps the branch synchronized before heavy file movement. High/Ultra can run targeted metadata cleanup later with Unity import context.

Hardware Impact: Runtime 0 us/frame. Git-only operation.

## Decision 2: Kill Stale Push Processes

Problem: The first `git push origin main` timed out and left orphaned Git processes.

Solution: Stopped exact stale process IDs from the timed-out command, then retried push with bounded HTTP low-speed settings and verified divergence `0 0`.

Rejected Alternatives: Leaving stale Git processes running was rejected because they could hold network sessions or locks. Killing unrelated processes was rejected; only observed Git PIDs from the timed-out command were targeted.

Scalability potential: Repository operations remain deterministic for the archive phase.

Hardware Impact: Runtime 0 us/frame. Process hygiene only.

## Decision 3: Two Combined Category Documents

Problem: The archive needs searchability without producing more bulky bundles than requested.

Solution: Generate exactly two combined documents: one for archived `AgentLogs` `.md/.txt` sources and one for archived `Tasks` `.md/.txt` sources. Each section carries file path, size, timestamp, and extension.

Rejected Alternatives: A global boundary-free dump was rejected because it loses source provenance. Four duplicate `.md`/`.txt` variants were rejected because the user asked for two files. JSON-inclusive bundles were rejected because the request named only `.txt` and `.md`.

Scalability potential: Low/Middle users get two grep targets. High/Ultra forensic users still have original per-agent files in the archive folders.

Hardware Impact: Runtime 0 us/frame. Documentation search hygiene only.

## Decision 4: Bounded Late-Writer Sweep

Problem: A new `Build_BOID_SENSORY_INPUT_PUMP_Polish23.txt` appeared in active `Docs/AgentLogs` after the first archive move and was briefly locked by another process.

Solution: Use a bounded retry loop with caught `Move-Item` failures, wait for the writer to release the file, then move it into Batch007 and require two empty active-folder scans.

Rejected Alternatives: Deleting the active late file was rejected. Killing unknown writer processes was rejected. Leaving it active without note was rejected because the user requested cleanup.

Scalability potential: The archive remains tolerant of concurrent agents without destructive locking behavior.

Hardware Impact: Runtime 0 us/frame. Filesystem hygiene only.

## Decision 5: Regenerate Combined After Late Writes

Problem: Active folders repopulated after the first combined generation, so the combined documents were stale within minutes.

Solution: Move the late AgentLogs/Tasks files into Batch007, then regenerate the two combined documents from the archive source folders.

Rejected Alternatives: Keeping stale combined outputs was rejected because the user requested merged `.txt/.md` archives. Infinite waiting for every possible future writer was rejected; the criterion is bounded sweeps with empty readback.

Scalability potential: The two combined documents remain useful grep targets for the completed Batch007 evidence window.

Hardware Impact: Runtime 0 us/frame. Static document generation only.

## Decision 6: Split AgentLogs Summary Into Four Parts

Problem: The Batch007 AgentLogs combined summary is too large for quick review as one file.

Solution: Split `AgentLogs_Batch007_COMBINED_MD_TXT.md` into four line-boundary parts with approximately equal UTF-8 source bytes.

Rejected Alternatives: Exact `FILE START` boundary splitting was rejected after measurement because large source sections made the parts materially uneven. Mid-byte splitting was rejected because it can corrupt UTF-8 text.

Scalability potential: Low/Middle review can open one quarter at a time. High/Ultra forensic review can still use the unsplit combined source and original per-file archive.

Hardware Impact: Runtime 0 us/frame. Static document handling only.

## Decision 7: Split First AgentLogs Part Into Two

Problem: The first AgentLogs split part still remained large for targeted review.

Solution: Keep the original `PART01_OF_04` as source evidence and create two line-boundary subparts, `PART01A_OF_04` and `PART01B_OF_04`, with approximately half the original source bytes each.

Rejected Alternatives: Replacing the original first part was rejected because it would destroy the already reported 4-part layout. Mid-byte splitting was rejected because it can corrupt UTF-8.

Scalability potential: Reviewers can open the smaller first-quarter slices without losing the canonical four-part set.

Hardware Impact: Runtime 0 us/frame. Static document handling only.

## Decision 8: Final Evidence Closeout After Archive Push

Problem: The archive commit was pushed and verified, but the checklist still showed the final task as pending.

Solution: Update only the task-local evidence files after remote verification, then commit and push this closeout as a narrow documentation-only delta.

Rejected Alternatives: Leaving the checklist stale was rejected because the reporting protocol requires disk evidence. Editing unrelated concurrent work was rejected because other agents are writing live code and reports.

Scalability potential: Low/Middle readers get a closed archive status. High/Ultra forensic review can correlate the pre-archive commit, archive commit, and closeout commit without scanning chat history.

Hardware Impact: Runtime 0 us/frame. Documentation evidence only.
