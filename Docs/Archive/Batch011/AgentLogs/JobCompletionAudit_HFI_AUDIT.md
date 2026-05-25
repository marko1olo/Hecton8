# Job Completion Audit

Evidence class: STATIC_SOURCE. No Unity import, player build, profiler, or runtime frame proof was executed.

- Schema: `hecton8.job_completion_audit.v1`
- Source root: `Assets/_Project/Scripts`
- C# files scanned: `2197`
- Completion findings: `553`
- Frame-path blocker findings: `0`
- Raw runtime blocker findings: `12`
- Plugin synchronous generator findings: `4`

## Totals By Classification

| Classification | Count |
|---|---:|
| `DispatcherFenceInternalRawComplete` | `2` |
| `EditorOrTestComplete` | `154` |
| `FramePathDispatcherComplete` | `1` |
| `FramePathPolledDispatcherComplete` | `25` |
| `PluginSynchronousGeneratorRawComplete` | `4` |
| `RuntimeOtherDispatcherComplete` | `46` |
| `RuntimeOtherForcedDispatcherComplete` | `125` |
| `RuntimeOtherPolledDispatcherComplete` | `44` |
| `RuntimeOtherRawComplete` | `6` |
| `RuntimeScheduleCompleteChain` | `6` |
| `TeardownDispatcherComplete` | `5` |
| `TeardownForcedDispatcherComplete` | `135` |

## Totals By Surface

| Surface | Count |
|---|---:|
| `EditorOrTest` | `154` |
| `FramePath` | `26` |
| `RuntimeOther` | `233` |
| `Teardown` | `140` |

## Top Domains

| Domain | Count |
|---|---:|
| `World` | `109` |
| `Root` | `83` |
| `Editor` | `81` |
| `Gameplay` | `43` |
| `Physics` | `39` |
| `Core` | `24` |
| `Construction` | `18` |
| `QA` | `13` |
| `Fauna` | `12` |
| `Power` | `12` |
| `Dev` | `11` |
| `Lighting` | `9` |
| `Plugins` | `9` |
| `UI` | `9` |
| `Atmosphere` | `8` |
| `Habitat` | `8` |
| `AI` | `7` |
| `Audio` | `7` |
| `Tools` | `7` |
| `Physiology` | `5` |

## Frame-Path Blockers


## Plugin Synchronous Generator Completes

- `Assets/_Project/Scripts/Plugins/MapMagic/HectonAnomalyMapMagicNode.cs:311` `PluginSynchronousGeneratorRawComplete` `Generate`: `handle.Complete();`
- `Assets/_Project/Scripts/Plugins/MapMagic/HectonBiomeMatrixMapMagicPostProcessNode.cs:141` `PluginSynchronousGeneratorRawComplete` `Generate`: `handle.Complete();`
- `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs:165` `PluginSynchronousGeneratorRawComplete` `Generate`: `handle.Complete();`
- `Assets/_Project/Scripts/Plugins/MapMagic/HectonTerrainSplatmapMapMagicNode.cs:180` `PluginSynchronousGeneratorRawComplete` `Generate`: `handle.Complete();`

## Interpretation

- Editor/test/offline and teardown completions are review surfaces, not automatic runtime defects.
- `DispatcherFenceInternalRawComplete` is the canonical Core fence implementation site; callers are audited separately.
- `PluginSynchronousGenerator*Complete` means a plugin graph API currently requires concrete products before returning; do not rewrite without caller/lifecycle proof.
- Frame-path raw/forced completions are blockers until moved to dispatcher/fence windows or justified with profiler proof.
- Polled dispatcher completions with `forceComplete:false` remain review surfaces because same-frame schedule/readback loops can still hide elsewhere.
