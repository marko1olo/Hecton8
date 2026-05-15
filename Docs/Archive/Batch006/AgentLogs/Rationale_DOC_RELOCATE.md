# Rationale_DOC_RELOCATE

Problem: Hecton active `Docs/Tasks` and `Docs/AgentLogs` can contain files from other projects, contaminating batch context for parallel agents.
Solution: Use static filename/content scan, move only high-confidence foreign documents to their owning project roots, and leave ambiguous Hecton documents untouched.
Rejected Alternatives: Bulk archive under Hecton was rejected because it preserves contamination. Deletion was rejected because user asked to move documents home.
Scalability potential: Low/Middle/High/Ultra unaffected; documentation hygiene reduces agent context noise but does not alter runtime.
Hardware Impact: Runtime gain on i3/MX350 is 0 us; agent-scan time reduced by removing irrelevant files from active folders.

Problem: Timaert markers appeared only inside Hecton audit provenance files, not as Timaert-owned task/log files.
Solution: Leave Hecton audit provenance in Hecton and record that no Timaert-owned active files were found by filename marker scan.
Rejected Alternatives: Moving `Status_COMPUTE_LOGISTICS_AUDITOR.md`, `LOG_COMPUTE_LOGISTICS_AUDITOR.md`, or `Rationale_COMPUTE_LOGISTICS_AUDITOR.md` to Timaert was rejected because those files document Hecton audit work and would remove current Hecton evidence.
Scalability potential: Low/Middle/High/Ultra unaffected; preserves clean project memory without destroying audit traceability.
Hardware Impact: 0 runtime microseconds.

Problem: Stomchat/stomatology agent files were located in Hecton active task/log folders.
Solution: Move exactly three files to `C:\hades\dental-crm\docs\Tasks` and `C:\hades\dental-crm\docs\AgentLogs`, preserving names and content.
Rejected Alternatives: Copy-only was rejected because the user asked to move them home and Hecton active folders would remain contaminated. Deletion was rejected because the dental project still needs the evidence.
Scalability potential: Low/Middle/High/Ultra unaffected; side project docs are now under their project root.
Hardware Impact: 0 runtime microseconds.
