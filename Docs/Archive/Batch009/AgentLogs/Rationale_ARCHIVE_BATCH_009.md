# Rationale ARCHIVE_BATCH_009

Date: 2026-05-19
Status: PENDING VERIFICATION
EvidenceClass: FILESYSTEM / STATIC_DOC
Phase: FINAL

Problem: Current batch artifacts were active under Docs/AgentLogs and Docs/Tasks, contaminating the next agent context.
Solution: Created Docs/Archive/Batch009 with AgentLogs, Tasks, AgentLogs_Combined, and Tasks_Combined; moved active evidence into matching buckets; wrote Batch009_MoveManifest.json.
Rejected Alternatives: Leaving active duplicates was rejected because AGENTS.md requires batch handover hygiene. Moving CURRENT_BATCH.md was rejected by direct user order. Moving POLISH.txt and НЕ ДВИГАТЬ! ИНСТРЫ.txt was rejected because they are active instruction surfaces, and Batch008 precedent preserved POLISH.txt.
Scalability potential: Low: active context avoids loading stale logs. Middle: deterministic batch folders. High: slim collections reduce review IO. Ultra: raw artifacts remain preserved for forensic tooling while compact views stay lightweight.
Hardware Impact: Runtime gain on i3/MX350 is 0 us; no runtime code changed. Operational gain is context/file hygiene only, unmeasured.

Problem: Previous combined files were raw and heavy; user requested trimmed MD/TXT collectors.
Solution: Generated separate slim MD and TXT files per bucket. The filter strips English articles, markdown/table/braces/XML noise, collapses whitespace, caps noisy files, and preserves high-signal status/error/problem/solution/task lines.
Rejected Alternatives: Concatenating full JSON/log payloads was rejected because it inflates prompt weight and buries useful evidence. Summarizing without file boundaries was rejected because forensic traceability would be lost.
Scalability potential: Low: compact text for weak machines and small contexts. Middle: manifests preserve source inventory. High: raw files remain available for deep audit. Ultra: archive root index allows top-level navigation without loading historical batches.
Hardware Impact: Runtime impact 0 us. Human/context IO reduction is not profiler evidence and is not claimed as frame-time gain.

Problem: Concurrent writers may recreate files during archive.
Solution: Move pass records locked files as snapshots when possible; verification records any active leftovers. Collision suffixes preserve evidence instead of overwriting.
Rejected Alternatives: Killing unknown writer processes was rejected because it risks corrupting Unity/QA output. Overwriting same-name evidence was rejected because it destroys chronology.
Scalability potential: Low: no forced process disruption. Middle: collision-safe archive. High: manifests expose unresolved files. Ultra: forensic comparison remains possible.
Hardware Impact: Runtime impact 0 us.