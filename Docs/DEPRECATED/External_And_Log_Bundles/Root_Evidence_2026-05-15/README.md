# Root Evidence Bundle 2026-05-15

Status: DEPRECATED / RAW EVIDENCE
Evidence class: FILESYSTEM

Purpose: preserve raw root-level logs, generated artifacts, stale external payloads, and a destructive legacy cleanup script without leaving them in repository root.

This bundle is excluded from standard agent context. Open it only for provenance, artifact recovery, or a cleanup audit.

Moved from repository root:

- `_agent_screen_capture.png`
- `build-core.log`
- `clean_project.py`
- `codex_async_persistence_build.log`
- `codex_unity_compile.log`
- `codex_unity_sync.log`
- `CYRILLIC_PURGE_REPORT_2026-05-10.json`
- `playmode_metrics.json`
- `preprocessed-core.xml`
- `tools_list_mcp.json`
- `Unity-GPU-Boids-master.zip`

Boundary:

- No file in this bundle is active documentation authority.
- `clean_project.py` is preserved as a legacy artifact; do not execute it without a separate reviewed cleanup task because it recursively deletes third-party demo/example folders.
- Logs here are raw historical evidence only; they do not override fresh build, Unity Console, Play Mode, profiler, or player-build artifacts.
