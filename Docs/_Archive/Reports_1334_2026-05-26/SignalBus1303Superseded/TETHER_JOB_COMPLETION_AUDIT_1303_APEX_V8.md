# [ARCHIVE] Job Completion Audit

Archived by agent 1334 on 2026-05-26. Active replacement: `Docs/Reports/SIGNALBUS_HOTPATH_AUDIT_1303_APEX_V16.md`.

Evidence class: STATIC_SOURCE. No Unity import, player build, profiler, or runtime frame proof was executed.

- Schema: `hecton8.job_completion_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files scanned: `2418`
- Completion findings: `589`
- Frame-path blocker findings: `0`
- Raw runtime blocker findings: `2`
- Plugin synchronous generator findings: `0`

## Totals By Classification

| Classification | Count |
|---|---:|
| `DispatcherFenceInternalRawComplete` | `2` |
| `EditorOrTestComplete` | `178` |
| `FramePathDispatcherComplete` | `1` |
| `FramePathPolledDispatcherComplete` | `21` |
| `RuntimeOtherDispatcherComplete` | `43` |
| `RuntimeOtherForcedDispatcherComplete` | `158` |
| `RuntimeOtherPolledDispatcherComplete` | `36` |
| `RuntimeOtherRawComplete` | `2` |
| `TeardownDispatcherComplete` | `5` |
| `TeardownForcedDispatcherComplete` | `142` |
| `TeardownRawComplete` | `1` |

## Totals By Surface

| Surface | Count |
|---|---:|
| `EditorOrTest` | `178` |
| `FramePath` | `22` |
| `RuntimeOther` | `241` |
| `Teardown` | `148` |

## Top Domains

| Domain | Count |
|---|---:|
| `World` | `118` |
| `Editor` | `82` |
| `Root` | `82` |
| `Physics` | `55` |
| `Gameplay` | `30` |
| `Core` | `22` |
| `Construction` | `19` |
| `AI` | `15` |
| `Power` | `13` |
| `QA` | `13` |
| `Fauna` | `12` |
| `Dev` | `11` |
| `Habitat` | `10` |
| `Lighting` | `10` |
| `Atmosphere` | `9` |
| `Audio` | `9` |
| `Plugins` | `9` |
| `Ecosystem` | `8` |
| `UI` | `8` |
| `Tools` | `7` |

## Frame-Path Blockers


## Plugin Synchronous Generator Completes


## Interpretation

- Editor/test/offline and teardown completions are review surfaces, not automatic runtime defects.
- `DispatcherFenceInternalRawComplete` is the canonical Core fence implementation site; callers are audited separately.
- `PluginSynchronousGenerator*Complete` means a plugin graph API currently requires concrete products before returning; do not rewrite without caller/lifecycle proof.
- Frame-path raw/forced completions are blockers until moved to dispatcher/fence windows or justified with profiler proof.
- Polled dispatcher completions with `forceComplete:false` remain review surfaces because same-frame schedule/readback loops can still hide elsewhere.
