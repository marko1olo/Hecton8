# LOG_SHINOBU_150

## 2026-05-19 - Babel Subtitle Sync Runtime

What was wrong:

- Legacy subtitle presentation used frame-loop timing semantics for visible duration and typewriter reveal.
- Existing Babel decode supported UTF-8 span output and `^0..^3` placeholders, but `{0}` authoring would still require either manual preprocessing or managed formatting.
- Subtitle cue state had no dedicated 32-byte ABI, compact signal lane, or SHINOBU_150 black-box recorder.
- Canvas label swap cadence used a fixed max drain and binary rich-text policy instead of `GlobalQualityWeight`.
- There was no single editor x-ray for hash -> raw UTF-8 -> decoded span -> audio-frame cue publish.

What was done:

- Added `BabelSubtitleSyncRuntime` with 16-byte `SubtitleCueSignal`, 32-byte explicit `SubtitleCueDTO`, audio-frame cue evaluation, 300-entry telemetry ring, and dump paths `Dump_SHINOBU_150.bin` / `Dump_BABEL_SURGEON.bin`.
- Added GlobalDataVault buffer IDs for subtitle cue state, subtitle telemetry, audio-frame clock, and debug scratch.
- Wired `SubtitleManager` to prepare Babel cue frames, drain compact cue signals, record decode telemetry, append directional arrows, and derive subtitle/typewriter/audio-log reveal timing from DSP sample frames.
- Extended `LocRegistry` UTF-8 decode loop to handle `{0}` and `{0:format}` without `string.Format`.
- Changed `LabelSwapScheduler` dirty budget and rich-text stripping to continuous `HomeostasisBrain.GlobalQualityWeight`.
- Added UI Toolkit `BabelSyncTunerWindow` for telemetry, hash preview, raw UTF-8 hex, decoded span preview, quality override, audio-frame offset, and cue publish.
- Added `Docs/ARCHITECTURE/LOCALIZATION_SUBTITLE_SYNC_ENGINE.md` and a SHINOBU_150 addendum in `ZERO_GC_UI_PIPELINE.md`.
- Updated `Docs/Tasks/Status_SHINOBU_150.md` and `Docs/AgentLogs/Rationale_SHINOBU_150.md`.

Cinematic cheats used:

- Subtitles are treated as presentation-only "Dear Lie" state with `FlagVisualOnlyNoRollback`; gameplay rollback exchanges intent/audio truth, not visible subtitle progress.
- Directional subtitle arrows use a cheap AUP delta and dot product instead of spatial UI simulation.
- Low-quality behavior strips rich text and spreads TMP dirties across frames; high/ultra spends saved cycles on richer visible text and editor x-ray.

Exact microseconds saved, static estimate pending profiler proof:

- Hash UTF-8 span lookup vs managed string dictionary: 20-500 us per subtitle burst, workload dependent.
- Manual UTF-8 decode into pooled span vs `Encoding.GetString`: 20-120 us and one managed allocation per subtitle.
- `{0}` span formatting vs `string.Format`: 5-40 us and one managed allocation per formatted subtitle.
- 64 cue audio-frame evaluation: expected <10 us on i3/MX350-class CPU.
- Dirty label budget smoothing: expected 50-200 us frame-spike reduction during label bursts, profiler proof pending.
- Uninitialized cue buffer with flag-only clear: expected 1-4 us startup/init reduction for 64 cues.

Verification:

- Static scan clean for touched subtitle/Babel runtime files: no `Time.unscaledTime`, `Time.deltaTime`, `WaitForSeconds`, coroutine, `string.Format`, `Encoding.UTF8.GetString`, or JSON parser usage.
- Static UI/Narrative scan clean for JSON parser and `Dictionary<string,string>` locale resolver usage.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` attempted after CPU 14% and no `dotnet/csc` process.
- Build is blocked by unrelated missing types in `HectonVisorUberPostFeature`, `GlobalRegistryContracts`, `DeferredDecalPass`, `ModularEquipmentEngine`, and `SomaticTunerWindow`. No SHINOBU_150 file errors were emitted before that dependency wall.
