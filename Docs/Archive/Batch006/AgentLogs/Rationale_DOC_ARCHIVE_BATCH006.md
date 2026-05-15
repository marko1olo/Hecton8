# Rationale_DOC_ARCHIVE_BATCH006

Problem: Active `Docs/Tasks` and `Docs/AgentLogs` contain current batch state and must be archived before the next batch. Unbounded old logs in active folders violate batch handover hygiene.

Solution: Create `Docs/Archive/Batch006` using the Batch005 structure, move active contents into `AgentLogs` and `Tasks`, then generate combined md/txt/json text bundles with explicit file boundaries and provenance headers.

Rejected Alternatives: Copy-only archive was rejected because stale active files would remain. Flat archive was rejected because it loses the Tasks/AgentLogs boundary. Blind concatenation was rejected because QA evidence rules forbid "all agents audited" style claims without source-file names and unresolved-file boundaries.

Scalability potential: Low keeps active folders empty for cheap search/index operations. Middle preserves per-agent files for targeted forensic reads. High/Ultra adds combined bundles for fast global grep without destroying original file granularity.

Hardware Impact: Runtime impact is 0 us/frame. Editor/process impact is lower future CLI scan noise, not a measured Unity performance gain.

Evidence Class: FILESYSTEM and STATIC_DOC. No runtime proof claimed.

## Execution Notes

Problem: The active folders held 913 AgentLogs files and 86 Tasks files, including the archive operation's own status/rationale/log.

Solution: Moved direct children into `Docs/Archive/Batch006/AgentLogs` and `Docs/Archive/Batch006/Tasks` after absolute path validation under `C:\hades\Hecton8\Docs`.

Rejected Alternatives: Recursive deletion/recreate was rejected because it would be destructive and unnecessary. Moving via cmd/batch was rejected; native PowerShell `Move-Item` kept path handling in one shell.

Scalability potential: Combined files cover only `.md`, `.txt`, and `.json`, preserving binary/png/csv/log artifacts as individual files while keeping searchable report text compact.

Hardware Impact: Runtime 0 us/frame. Editor-side future grep/index scan should touch fewer active files; no profiler claim.

Readback: AgentLogs moved 913/913; Tasks moved 86/86; active AgentLogs remaining 0; active Tasks remaining 0. Combined candidates generated: AgentLogs 549, Tasks 86.

Late-write handling: Because other agents are still active, 7 AgentLogs files and 2 Tasks files appeared after the initial move. A second bounded sweep moved them into Batch006 and regenerated combined bundles. This preserves latest report content without claiming a global freeze of concurrent writers.

Final late-write handling: 2 additional AgentLogs files and 1 Tasks file appeared during finalization. They were moved into Batch006. Final archive counts recorded before final readback: AgentLogs 917 files, Tasks 87 files.

Stabilization late-write handling: A bounded idle loop moved 7 additional AgentLogs items and 2 Tasks items until two consecutive scans found active folders empty. Stabilized counts: AgentLogs 921 files, Tasks 87 files. This is still a filesystem readback, not a guarantee that another external agent will never write after the readback.
