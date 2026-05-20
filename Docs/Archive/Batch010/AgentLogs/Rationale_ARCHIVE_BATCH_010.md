# Rationale ARCHIVE_BATCH_010

Problem: Active Docs/AgentLogs and Docs/Tasks contain Batch010 transient evidence mixed by agent and artifact type.
Solution: Move active evidence into Docs/Archive/Batch010 and generate slim grouped collections by first filename token.
Rejected Alternatives: A single combined file repeats Batch008/Batch009 bloat and makes LOG/Rationale/Status retrieval slow. Keeping POLISH and instruction text active conflicts with the user's explicit 'except CURRENT_BATCH.md' rule.
Scalability potential: Low = active folders stay minimal for weak devices/tools; Middle = prefix grouped retrieval; High = parallel human review of LOG/Rationale/Status groups; Ultra = split parts keep giant signal dumps searchable without loading one monolith.
Hardware Impact: Runtime game impact 0 microseconds. Tooling impact is reduced editor/CLI scan surface in active folders; no profiler claim.

Problem: Combined evidence files can exceed 3MB, especially SignalBus contract audit groups.
Solution: Emit part files with 40-line overlap when slim output crosses the threshold.
Rejected Alternatives: Hard truncation loses forensic data; unsplit full files violate user threshold.
Scalability potential: Low = smaller chunks open on cheap machines; Middle/High/Ultra = review can target prefixes and parts.
Hardware Impact: Runtime game impact 0 microseconds. Workstation memory pressure reduced during document review; no measured benchmark.

Problem: Previous combined format wrote file size and write timestamps into every section.
Solution: Section separator only records filename and end filename.
Rejected Alternatives: Batch009 metadata format rejected by current user order.
Scalability potential: Less noisy corpus for search and summarization across all tiers.
Hardware Impact: Runtime game impact 0 microseconds; static document bytes reduced only.
Problem: Concurrent agents recreated SHINOBU files while archive build was running.
Solution: Late-sweep active folders into Batch010 using collision-safe __late suffixes, then rebuild combined manifests.
Rejected Alternatives: overwrite original archived files; leave active clutter.
Scalability potential: preserves forensic sequence without blocking other writers.
Hardware Impact: Runtime game impact 0 microseconds.

Problem: Second concurrent write wave appeared during combined rebuild.
Solution: Moved wave 2 and rebuilt affected LOG/Rationale/Route/Status groups only.
Rejected Alternatives: keep rebuilding full SignalBus dump; block other agents by permissions.
Scalability potential: bounded archive pass under concurrent workspace churn.
Hardware Impact: Runtime game impact 0 microseconds.
