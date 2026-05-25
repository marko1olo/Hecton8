# LOCALIZATION_SUBTITLE_SYNC_ENGINE

Date: 2026-05-19

Owner: SHINOBU_150

Status: PENDING VERIFICATION

Evidence class: STATIC_SOURCE until Unity import, Burst compile, Play Mode, GCMonitor, and profiler captures exist.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not runtime subtitle synchronization, audio-clock correctness, TMP allocation behavior, profiler, or player-build proof.

- `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs`

- `Assets/_Project/Scripts/UI/CharBufferPool.cs`

- `Assets/_Project/Scripts/UI/SubtitleManager.cs`

- `Assets/_Project/Scripts/LocNumericBuffer.cs`

- `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin`

## Boundary

The Babel subtitle sync layer is Presentation & UX only.

Allowed: read localization bytes, decode text, schedule visible subtitle cues, write editor diagnostics.

Forbidden: gameplay truth, rollback Merkle state, inventory state, quest state, save authority.

## Runtime Data Path

- Cold language data enters `LocRegistry`/`BabelDictionaryStore` as sorted 32-bit hash entries plus UTF-8 byte slabs. Standalone/editor cold path can use `MemoryMappedFile`; the hot path consumes pointer/offset/length spans.

- Hot lookup uses hash binary search and `ReadOnlySpan<byte>`. It does not resolve by `Dictionary<string,string>`.

- Hot decode writes UTF-8 directly into caller-owned `Span<char>` and commits to TMP through `SetCharArray`.

- Dynamic token injection supports `^0..^3`, `{0}`, and `{0:format}` through `BabelFormatArgs` and `ZeroGCFormatter`; `string.Format` is not part of the hot path.

- `LocRegistry.ReloadBinaryOrMock(...)` loads only static/binary UTF-8 authority or the unmanaged emergency mock. The previous dictionary reload bridge has been removed from `LocRegistry`.

- `LocalizationManager` still exposes managed string compatibility APIs for older callers.
- Current-language lookup hashes into Babel first.
- Built-in fallback strings resolve through static switch dispatch.
- Runtime `Dictionary<string,string>` language tables are no longer owned.

- Legacy mod localization injection is disabled. `ModLocalizationBridge` ignores discovered JSON language files, and `HectonAPI.Localization` exposes only a rejected future `InjectBabelEnvelope(ReadOnlySpan<byte>)` seam until binary/hash mod envelopes exist.

- Legacy JSON parsing for key generation and CJK validation lives only in `Assets/_Project/Scripts/Editor/LocalizationEditorJsonTableParser.cs`; it is Editor-only tooling and is not a runtime localization authority.

- The legacy string compatibility formatter is quarantined behind `string.Create` plus primitive `TryFormat`; numeric format suffixes are preserved, but the method still returns a managed string and is not zero-GC proof.

- Fallback decode storage cap: `4096` glyphs.
- Static 500-word paragraph audit path no longer truncates at old `1024` glyph ceiling.
- Larger PDA/lore views should pass caller-owned span to `TryWriteVisualSpanFromUtf8(...)`.

- Legacy `ResolveRaw`/`TryGetRawBuffer` APIs use a fixed 16-slot prewarmed `char[4096]` decode ring.
- Removed: thread-static grow-on-first-use decode buffer.
- This ring is not the subtitle hot path.
- New subtitle/lore surfaces should provide caller-owned spans or `CharBufferPool` leases.

- `LocNumericBuffer` uses fixed 16-slot prewarmed `char[4096]` ring for compatibility APIs returning `char[]`.
- Removed: thread-static grow/expand route and `new char[capacity]` overflow fallback.
- Hot callers should prefer caller-owned `Span<char>` overloads.

- `CharBufferPool.RequiredBabelTextCapacity` is 512 chars across 500 subtitle slots. Megabyte lore windows must use encyclopedia page leases or caller-owned page spans, not inflate the common subtitle lease.

- `LocRegistry` missing-key suppression uses a fixed 256-bit bloom mask (`ulong * 4`), not `HashSet<int>`. It is diagnostic-only and cannot grow managed memory during missing-token storms.

- `SubtitleManager` legacy string subtitle requests use the same fixed 8-slot ring discipline as the Babel command and buffered-span queues. The previous managed `List<SubtitleRequest>` queue has been removed.

## Subtitle Clock

- `BabelSubtitleSyncRuntime` evaluates subtitle cue visibility against `AudioSettings.dspTime * AudioSettings.outputSampleRate`, stored as `uint AudioFrameClock`.
- `SubtitleManager` arms durations as audio-frame intervals and derives remaining time from the same frame clock.
- `LocalizationManager` PDA corrosion and madness visual override windows also expire by DSP/audio-frame counters, with wrap-safe `uint` signed-diff comparison.
- `Time.deltaTime` is retained only for visual alpha smoothing, not subtitle truth.

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

Registry-side public DTOs/signals are explicit-layout records:

- `LocalizationEntryDTO`, `SubtitleCommandDTO`, `SubtitleStateDTO`: 16 bytes.
- `BabelFormatArgs`: 24 bytes.
- `BabelDictionaryStage`: 32 bytes.
- `BabelTelemetryEntry`: 64 bytes.

No `Pack=1` or sequential-layout registry DTO remains.

## Vault And Dispatch

- Runtime persistent state is requested from `GlobalDataVault`.
- It resolves into transient `NativeArray<T>` views only when needed.
- SHINOBU_150 does not add IDs to the core `BufferID` enum.
- Domain-local IDs are cast constants:

- `(BufferID)70540`: `char[500 * 512]` Babel UTF-16 arena, owned by `CharBufferPool`

- `(BufferID)15070550`: `SubtitleCueDTO[64]`

- `(BufferID)15070551`: `LocalizationTelemetryEntry[300]`

`CharBufferPool` no longer creates persistent private native fallback storage.

If Vault arena is unavailable in a cold/editor mock route, Babel leases write into the prewarmed TMP bridge slot and keep the `SetCharArray` commit path.

`EvaluateSubtitleCuesJob` and `ClearSubtitleCueFlagsJob` both use synchronous Burst with `FloatMode.Fast` and `FloatPrecision.Standard`.

Cue evaluation is scheduled through `DispatcherBridge.ScheduleSimulation`. If `PreSimulationTick` already scheduled the job, the dispatcher receives combined dependencies.

Completion uses `IsCompleted` before `Complete()`.

## Rollback Fence

- Subtitle cue state is presentation-only.
- Runtime cues set `FlagVisualOnlyNoRollback`.
- `BabelSubtitleSyncRuntime.RollbackStateExcluded` documents DTO exclusion from Merkle truth.
- Gameplay/network rollback exchanges narrative intent or audio state, not visible subtitle progress.

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
