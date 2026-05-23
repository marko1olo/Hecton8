# Job Completion Audit

Evidence class: STATIC_SOURCE. No Unity import, player build, profiler, or runtime frame proof was executed.

- Schema: `hecton8.job_completion_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files scanned: `2362`
- Completion findings: `591`
- Frame-path blocker findings: `0`
- Raw runtime blocker findings: `15`
- Plugin synchronous generator findings: `0`

## Totals By Classification

| Classification | Count |
|---|---:|
| `DispatcherFenceInternalRawComplete` | `2` |
| `EditorOrTestComplete` | `166` |
| `FramePathDispatcherComplete` | `1` |
| `FramePathPolledDispatcherComplete` | `25` |
| `RuntimeOtherDispatcherComplete` | `48` |
| `RuntimeOtherForcedDispatcherComplete` | `139` |
| `RuntimeOtherPolledDispatcherComplete` | `44` |
| `RuntimeOtherRawComplete` | `7` |
| `RuntimeScheduleCompleteChain` | `8` |
| `TeardownDispatcherComplete` | `5` |
| `TeardownForcedDispatcherComplete` | `145` |
| `TeardownRawComplete` | `1` |

## Totals By Surface

| Surface | Count |
|---|---:|
| `EditorOrTest` | `166` |
| `FramePath` | `26` |
| `RuntimeOther` | `248` |
| `Teardown` | `151` |

## Top Domains

| Domain | Count |
|---|---:|
| `World` | `117` |
| `Root` | `83` |
| `Editor` | `81` |
| `Gameplay` | `47` |
| `Physics` | `42` |
| `Core` | `23` |
| `Construction` | `18` |
| `AI` | `15` |
| `Power` | `13` |
| `QA` | `13` |
| `Fauna` | `12` |
| `Dev` | `11` |
| `Habitat` | `10` |
| `Lighting` | `9` |
| `Plugins` | `9` |
| `UI` | `9` |
| `Atmosphere` | `8` |
| `Ecosystem` | `8` |
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
