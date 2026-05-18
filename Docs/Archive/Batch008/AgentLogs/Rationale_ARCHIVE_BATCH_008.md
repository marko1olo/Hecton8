# Rationale ARCHIVE_BATCH_008

Date: 2026-05-18
Status: PENDING VERIFICATION
EvidenceClass: FILESYSTEM / STATIC_DOC

Problem: Batch008 execution artifacts were still active under `Docs\AgentLogs` and `Docs\Tasks`, while previous batches are isolated under `Docs\Archive\Batch00X`.
Solution: Created `Docs\Archive\Batch008` with `AgentLogs`, `Tasks`, `AgentLogs_Combined`, and `Tasks_Combined`; moved active artifacts into the matching buckets; generated separate combined MD/TXT documents.
Rejected Alternatives: Leaving active duplicates was rejected because `Docs\DOC_GOVERNANCE.md` flags active agent logs as cleanup debt. Moving `Docs\Reports` was rejected because it was outside the explicit path scope and previous batch archives did not treat the full report vault as batch-local payload.
Scalability potential: Low tier: active context load drops because `Docs\AgentLogs` and `Docs\Tasks` are empty. Middle tier: archive scan has deterministic bucket paths. High tier: combined documents support quick static review without thousands of filesystem opens. Ultra tier: raw per-file artifacts are still preserved for forensic tooling.
Hardware Impact: Runtime i3/MX350 gain is 0 us because no game code changed. Operational impact is filesystem/context hygiene only; no profiler-backed microsecond saving is claimed.

Problem: Combined output needed to preserve report readability without absorbing `.log`, `.json`, `.csv`, `.rsp`, and `.xml` noise.
Solution: Followed Batch007 precedent: combined documents include `.md`, `.txt`, `.md.collision_*`, and `.txt.collision_*`, with per-file boundaries and metadata.
Rejected Alternatives: Combining every extension was rejected because binary/structured audit payloads become unreadable and inflate the human review surface.
Scalability potential: Low tier: smaller text bundles. Middle tier: manifests preserve file inventory. High tier: external tooling can still parse raw JSON/log files in the archive. Ultra tier: both raw and combined views exist.
Hardware Impact: Runtime impact 0 us. Review-time IO reduction is unmeasured and not reported as a performance win.

Problem: Concurrent agents recreated active `Docs\AgentLogs` / `Docs\Tasks` files after the first move.
Solution: Ran late sweeps and moved 41 additional files into Batch008. Existing archive names were preserved by collision suffixes.
Rejected Alternatives: Overwriting same-name archive files was rejected because it would destroy earlier evidence. Ignoring late files was rejected because the user requested all current batch logs/reports moved.
Scalability potential: Low tier: no active context drift from late files. Middle tier: collision suffixes preserve chronology. High tier: manifests keep both initial and late-move evidence. Ultra tier: forensic review can compare original and late collision variants.
Hardware Impact: Runtime impact 0 us. This is active-doc hygiene only.

Problem: New active log/status clutter appeared after Batch008 archive creation, and the user explicitly forbade touching `CURRENT_BATCH.md`.
Solution: Moved all free files from `Docs\AgentLogs` into `Batch008\AgentLogs`; moved `Status_*.md` and root-level prompt/batch dumps into `Batch008\Tasks`; left `CURRENT_BATCH.md` in place. Locked files were copied as `.locked_snapshot` artifacts and recorded as blockers.
Rejected Alternatives: Moving `CURRENT_BATCH.md` was rejected by explicit user order. Killing an unknown writer process to release log handles was rejected because it could corrupt a running Unity/QA writer.
Scalability potential: Low tier: active context avoids hundreds of megabytes of Unity logs. Middle tier: Batch008 keeps raw evidence and manifests. High tier: locked snapshots preserve current bytes without process disruption. Ultra tier: archive remains parseable by combined MD/TXT plus raw evidence.
Hardware Impact: Runtime impact 0 us. No game code changed; no frame-time claim is made.
