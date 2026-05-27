# Job Completion Audit

Evidence class: STATIC_SOURCE. No Unity import, player build, profiler, or runtime frame proof was executed.

- Schema: `hecton8.job_completion_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files scanned: `2439`
- Completion findings: `581`
- Frame-path blocker findings: `0`
- Raw runtime blocker findings: `7`
- Plugin synchronous generator findings: `0`

## Totals By Classification

| Classification | Count |
|---|---:|
| `DispatcherFenceInternalRawComplete` | `2` |
| `EditorOrTestComplete` | `177` |
| `FramePathDispatcherComplete` | `1` |
| `FramePathPolledDispatcherComplete` | `20` |
| `RuntimeOtherDispatcherComplete` | `40` |
| `RuntimeOtherForcedDispatcherComplete` | `164` |
| `RuntimeOtherPolledDispatcherComplete` | `24` |
| `RuntimeOtherRawComplete` | `7` |
| `TeardownDispatcherComplete` | `5` |
| `TeardownForcedDispatcherComplete` | `138` |
| `TeardownRawComplete` | `3` |

## Totals By Surface

| Surface | Count |
|---|---:|
| `EditorOrTest` | `177` |
| `FramePath` | `21` |
| `RuntimeOther` | `237` |
| `Teardown` | `146` |

## Top Domains

| Domain | Count |
|---|---:|
| `World` | `105` |
| `Editor` | `82` |
| `Root` | `80` |
| `Physics` | `55` |
| `Gameplay` | `30` |
| `Core` | `22` |
| `Construction` | `19` |
| `AI` | `17` |
| `Power` | `15` |
| `QA` | `13` |
| `Fauna` | `12` |
| `Dev` | `11` |
| `Habitat` | `10` |
| `Lighting` | `10` |
| `Plugins` | `9` |
| `Ecosystem` | `8` |
| `UI` | `8` |
| `Atmosphere` | `7` |
| `Audio` | `7` |
| `Tools` | `7` |

## Frame-Path Blockers


## Plugin Synchronous Generator Completes


## Interpretation

- Editor/test/offline and teardown completions are review surfaces, not automatic runtime defects.
- `DispatcherFenceInternalRawComplete` is the canonical Core fence implementation site; callers are audited separately.
- `PluginSynchronousGenerator*Complete` means a plugin graph API currently requires concrete products before returning; do not rewrite without caller/lifecycle proof.
- Frame-path raw/forced completions are blockers until moved to dispatcher/fence windows or justified with profiler proof.
- Polled dispatcher completions with `forceComplete:false` remain review surfaces because same-frame schedule/readback loops can still hide elsewhere.
