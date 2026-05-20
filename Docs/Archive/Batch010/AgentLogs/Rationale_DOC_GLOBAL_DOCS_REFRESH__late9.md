# DOC_GLOBAL_DOCS_REFRESH Rationale

Date: 2026-05-20
Status: IN PROGRESS / STATIC DOCUMENTATION ONLY

Active memory note: this file was recreated after concurrent workspace archival/deletion of active `Docs/AgentLogs/Rationale_DOC_GLOBAL_DOCS_REFRESH.md`. Historical full snapshots remain under `Docs/Archive/Batch008`, `Docs/Archive/Batch009`, and `Docs/Archive/Batch010`.

## Decision 36: Root / Architecture Authority-Spine And Domain-Map Correction

Problem: After R35, active root/architecture docs still had R35 validation-pending residue, some root work-plan docs lacked R4/current actuality boundaries, the domain map contained fused domain lines and a typo, and AtlasCheck totals drifted after regeneration.

Solution: Treat R36 as a static documentation authority-spine correction. Write `Docs/Reports/2026-05-20_DOCUMENTATION_R36_ROOT_ARCHITECTURE_AUTHORITY_SPINE_LOCAL.md`; promote R36 through active root/architecture entrypoints; update `Docs/Actual Domains of Project.txt`; add R4/R36 boundaries to `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md`; regenerate the architecture atlas; and record current red gates exactly.

Rejected Alternatives: Leaving R35 validation-pending wording was rejected after R36 static checks ran. Creating placeholder vendor icons, `Decal.obj`, missing source files, screenshots, or Unity logs was rejected as fake evidence. Claiming Unity/runtime/profiler/player-build proof was rejected because none was run.

Scalability potential: Low-tier readers get a single current root/architecture boundary and exact blocker list. Middle-tier review gets source-anchor and link scans instead of stale report routing. High/Ultra review can focus on real Unity import, player build, profiler/GC, AtlasCheck vendor cleanup, and generated-project stale include cleanup.

Hardware Impact: 0 us/frame. Documentation/tooling only. No runtime optimization or microsecond saving is claimed without profiler evidence.

Evidence Class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PY_TOOL / POWERSHELL_STATIC / READ_ONLY_SUBAGENT_AUDIT. Runtime verification remains PENDING VERIFICATION.

## Decision 37: Recreate Active DOC_GLOBAL Memory Before Further Root/Architecture Pass

Problem: Active `Docs/Tasks/Status_DOC_GLOBAL_DOCS_REFRESH.md`, `Docs/AgentLogs/Rationale_DOC_GLOBAL_DOCS_REFRESH.md`, and `Docs/AgentLogs/LOG_DOC_GLOBAL_DOCS_REFRESH.md` were deleted again by concurrent workspace archival while the root/architecture documentation pass was still active. Without these files, AGENTS anti-amnesia and reporting protocol cannot be satisfied.

Solution: Recreate only the DOC_GLOBAL active memory files from verified disk state and archived Batch010 history, preserving R36 validation and blockers. Do not restore unrelated deleted SHINOBU/HFI files because those are outside this agent's ownership and likely concurrent workspace churn.

Rejected Alternatives: Restoring every deleted `Docs/Tasks` or `Docs/AgentLogs` file was rejected as cross-agent interference. Ignoring the missing files was rejected because reporting protocol requires active status/rationale/log. Treating archive snapshots as current without recreating active files was rejected because the active task state must exist in `Docs/Tasks` and `Docs/AgentLogs`.

Scalability potential: Low-tier review gets current task state without scanning archive batches. Middle-tier and High/Ultra review can use active log/rationale as the current evidence index while preserving archive history.

Hardware Impact: 0 us/frame. Documentation/tooling only.

Evidence Class: FILESYSTEM / STATIC_DOC. Runtime verification remains PENDING VERIFICATION.

