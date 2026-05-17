# Rationale_DOC_ARCHIVE_BATCH007

Problem: Batch007 active `Docs/Tasks` and `Docs/AgentLogs` need archival after Git synchronization, without corrupting concurrent agent work or inventing runtime dependencies.

Solution: Use the Batch006 archive layout, create `Docs/Archive/Batch007`, move only direct active folder children into matching archive folders after path-boundary validation, and generate two `.md/.txt`-only combined documents with explicit source boundaries.

Rejected Alternatives: Destructive cleanup was rejected. Flat archive was rejected because it destroys Tasks/AgentLogs provenance. Boundary-free concatenation was rejected because evidence reports must list scanned files. Force push/reset was rejected because shared remote history is authoritative.

Scalability potential: Low keeps active task/log folders small for cheap search. Middle preserves per-agent forensic files. High/Ultra gets combined grep documents without losing original file granularity.

Hardware Impact: Runtime impact is 0 us/frame. This is repository/documentation hygiene only; no Unity performance gain is claimed.

Evidence Class: GIT_CLI / FILESYSTEM / STATIC_DOC. Runtime verification remains PENDING VERIFICATION.
