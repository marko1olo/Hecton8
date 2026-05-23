# Rationale ARCHIVE_12
Date: 2026-05-23 14:47:47
Status: COMPLETE WITH CONCURRENT RESIDUE - PENDING USER REVIEW

Problem: Active Docs/Tasks and Docs/AgentLogs carried batch artifacts that would contaminate next-batch prompt extraction and active agent scans.
Solution: Created Batch012 with Batch011-compatible Tasks/AgentLogs/Summaries layout, moved raw artifacts intact, preserved CURRENT_BATCH.md, generated reconstructed move manifest and verification JSON after initial manifest-write failure.
Rejected Alternatives: Flat archive loses source buckets. Copy-only leaves stale active files. Moving CURRENT_BATCH.md breaks current batch prompt extraction. Summarizing raw build logs instead of moving them intact would destroy evidence.
Scalability potential: Low: active docs scan small and cheap. Middle: summaries searchable without loading raw logs. High: manifests support indexing. Ultra: raw evidence remains intact for forensic ingestion.
Hardware Impact: Runtime/game frame impact 0 us on i3/MX350. Operational disk IO only. No profiler microsecond claim.

Problem: User requested shorter summaries without losing important technical signal.
Solution: Summary filter keeps status, problem/solution/rejected, hardware impact, verification, compile/build/runtime/GC/error/blocker, paths, authority-route terms, task checkboxes. It removes blank lines, common repeated evidence tables, English articles, duplicated normalized lines, and generic intro phrases.
Rejected Alternatives: Full concatenation stays too large. Aggressive prose rewrite risks hallucination and deletion of blockers. Per-file hard deletion without overflow count hides loss.
Scalability potential: Low: concise summaries reduce grep/read pressure. Middle/high/ultra: source artifacts remain available when exact proof is needed.
Hardware Impact: Runtime/game frame impact 0 us. Summary generation is one-time filesystem CPU/disk cost.

Problem: Concurrent EXTERNAL_CODEX files were recreated after late-catch passes.
Solution: Archived collision copies already in Batch012, then marked final verification unstable with explicit remaining files.
Rejected Alternatives: Infinite move loop would race active writer and create duplicate archive noise. False stable=true would be process lie.
Scalability potential: Archive remains usable; active residue is bounded and named.
Hardware Impact: Runtime/game frame impact 0 us.