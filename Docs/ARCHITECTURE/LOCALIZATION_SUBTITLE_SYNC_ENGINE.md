# LOCALIZATION_SUBTITLE_SYNC_ENGINE
Date: 2026-05-19
Owner: SHINOBU_150
Status: PENDING VERIFICATION

Evidence class: STATIC_SOURCE until Unity import, Burst compile, Play Mode, GCMonitor, and profiler captures exist.

## Boundary

The Babel subtitle sync layer is Presentation & UX only. It may read localization bytes, decode text, schedule visible subtitle cues, and write editor diagnostics. It must not write gameplay truth, rollback Merkle state, inventory state, quest state, or save authority.

## Runtime Data Path

- Cold language data enters `LocRegistry`/`BabelDictionaryStore` as sorted 32-bit hash entries plus UTF-8 byte slabs. Standalone/editor cold path can use `MemoryMappedFile`; the hot path consumes pointer/offset/length spans.
- Hot lookup uses hash binary search and `ReadOnlySpan<byte>`. It does not resolve by `Dictionary<string,string>`.
- Hot decode writes UTF-8 directly into caller-owned `Span<char>` and commits to TMP through `SetCharArray`.
- Dynamic token injection supports `^0..^3`, `{0}`, and `{0:format}` through `BabelFormatArgs` and `ZeroGCFormatter`; `string.Format` is not part of the hot path.

## Subtitle Clock

`BabelSubtitleSyncRuntime` evaluates subtitle cue visibility against `AudioSettings.dspTime * AudioSettings.outputSampleRate`, stored as `uint AudioFrameClock`. `SubtitleManager` arms durations as audio-frame intervals and derives remaining time from the same frame clock. `Time.deltaTime` is retained only for visual alpha smoothing, not subtitle truth.

## Cue ABI

`SubtitleCueSignal` is a 16-byte unmanaged `SignalBus<T>` payload for decoupled producers.

`SubtitleCueDTO` is a 32-byte explicit-layout GlobalDataVault DTO:

- `0`: `uint TokenHash`
- `4`: `float DisplayDuration`
- `8`: `uint StartAudioFrame`
- `12`: `float CurrentProgress`
- `16`: `uint Flags`
- `20..31`: explicit pad bytes

The layout is validated through `UnsafeUtility.SizeOf` and field offset checks.

## Rollback Fence

Subtitle cue state is presentation-only. `FlagVisualOnlyNoRollback` is set on runtime cues, and `BabelSubtitleSyncRuntime.RollbackStateExcluded` documents that these DTOs are not Merkle truth. Gameplay/network rollback must exchange narrative intent or audio state, not visible subtitle progress.

## Scalability

Canvas dirty budget is continuous:

- Low: small text-dirty budget, rich-text stripping below the quality threshold.
- Middle: wider staged label drain with the same zero-string decode.
- High: larger per-frame text dirty budget.
- Ultra: editor x-ray, raw UTF-8 preview, and richer telemetry without changing rollback truth.

The scalar source is `HomeostasisBrain.GlobalQualityWeight` with `SignalBusRegistry.GlobalQualityWeight01` fallback.

## Black Box

`LocalizationTelemetryEntry` is a fixed 64-byte telemetry frame. The ring capacity is 300 entries and dumps to:

- `Docs/AgentLogs/Dump_SHINOBU_150.bin`
- `Docs/AgentLogs/Dump_BABEL_SURGEON.bin`

Dump triggers include missing token hashes, slow decode guard, and invalid layout/fault state.

## Editor Tooling

`BabelSyncTunerWindow` is editor-only UI Toolkit tooling:

- hash parse and preview
- raw UTF-8 hex x-ray
- decoded `Span<char>` preview
- audio-frame offset override
- compact cue publish test
- continuous quality override

Editor allocations inside the window are permitted; they are outside the runtime hot path.

## Verification Required

- Unity import/compile.
- Burst compile of subtitle cue jobs.
- Play Mode cue publish and drain.
- GCMonitor proof for lookup/decode/TMP commit burst.
- Profiler proof for subtitle burst cost on low-end target class.
