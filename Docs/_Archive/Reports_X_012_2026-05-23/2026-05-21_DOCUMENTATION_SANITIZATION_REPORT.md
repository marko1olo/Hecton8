# [ARCHIVE] X_012 Historical Report

Archive date: 2026-05-23
Reason: removed from active documentation corpus by X_012; historical evidence only.
Active index: ../../Reports/README.md

# Documentation Sanitization Report

Date: 2026-05-21
Status: PENDING VERIFICATION
Owner: SHINOBU_ARCHIVARIUS_SURGEON
Evidence class: STATIC_DOC / STATIC_FILESYSTEM

Active markdown count: `303`
Active text count: `1`
Active `.md`/`.txt` bytes at latest revalidation scan: `4559660`

Counting rule: all `.md` and `.txt` files under `Docs/`, excluding `Docs/DEPRECATED/`, `Docs/Archive/`, `Docs/_Archive/`, `Docs/AgentLogs/`, and `Docs/Tasks/`. Batch archives are not active agent-ingest material. Byte count is a point-in-time scan because other agents are editing active docs.

Bytes removed from active read paths by quarantine: `43179948`.

Archived manifests:

- `Docs/DEPRECATED/Reports_2026-05-21_SANITIZED/ARCHIVED_FILES_2026-05-21.csv`
- `Docs/DEPRECATED/Documentation_Bundles_2026-05-21_SANITIZED/ARCHIVED_BUNDLES_2026-05-21.csv`
- `Docs/DEPRECATED/Reports_2026-05-21_REVALIDATION_QUARANTINE/ARCHIVED_REPORTS_REVALIDATION_2026-05-21.csv`
- `Docs/DEPRECATED/Reports_2026-05-21_LOOP11_STALE_HANDOFF/README.md`

Remaining gaps:

- Data Monolith payload is absent: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- Save/load runtime proof for active container `0x000B` is absent.
- Coop netcode remains static design; no transport/fuzz/runtime proof.
- Global authority lanes need runtime overflow/profiler evidence.
- UI zero-GC needs GCMonitor or Memory Profiler evidence.
- Terrain generators need proof against `FLOODED_TERRESTRIAL_GEOGRAPHY.md`.
- Current-day report artifacts remain in `Docs/Reports`; they are evidence snapshots until durable facts are promoted into active contracts.
