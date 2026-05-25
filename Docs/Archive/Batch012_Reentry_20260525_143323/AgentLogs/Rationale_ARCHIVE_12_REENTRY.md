# Rationale ARCHIVE_12_REENTRY
Problem: active Docs/Tasks and Docs/AgentLogs contained stale/residue files after existing Batch012 archive; current Batch012 archive already exists.
Solution: create unique Batch012_Reentry archive, write extractive compressed summaries, move raw evidence intact, preserve CURRENT_BATCH.md.
Rejected Alternatives: overwrite Batch012 would destroy prior proof boundary; delete raw logs would erase evidence; move CURRENT_BATCH.md would break current batch bootstrap; kill dental-crm node processes violates project boundary.
Scalability potential: low tier disk/agent context benefits from smaller summaries; high/ultra retains raw evidence for forensic overkill.
Hardware Impact: runtime 0 us; editor/context-load pressure reduced by using summaries instead of multi-MB raw logs.
