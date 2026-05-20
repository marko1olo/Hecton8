# LOCALIZATION_SUBTITLE_SYNC_ENGINE
Date: 2026-05-19
Owner: SHINOBU_150
Status: PENDING VERIFICATION

Evidence class: STATIC_SOURCE until Unity import, Burst compile, Play Mode, GCMonitor, and profiler captures exist.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R45 Root/Architecture Actuality Boundary
This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, subtitle route, localization runtime, or player-build proof is implied unless this document links a fresh evidence artifact.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not runtime subtitle synchronization, audio-clock correctness, TMP allocation behavior, profiler, or player-build proof.

- `Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs`
- `Assets/_Project/Scripts/UI/CharBufferPool.cs`
- `Assets/_Project/Scripts/UI/SubtitleManager.cs`
- `Assets/_Project/Scripts/LocNumericBuffer.cs`
- `Assets/_Project/Data/Localization/Babel_Dictionary.h8bin`

## Boundary

The Babel subtitle sync layer is Presentation & UX only. It may read localization bytes, decode text, schedule visible subtitle cues, and write editor diagnostics. It must not write gameplay truth, rollback Merkle state, inventory state, quest state, or save authority.

## Runtime Data Path

- Cold language data enters `LocRegistry`/`BabelDictionaryStore` as sorted 32-bit hash entries plus UTF-8 byte slabs. Standalone/editor cold path can use `MemoryMappedFile`; the hot path consumes pointer/offset/length spans.
- Hot lookup uses hash binary search and `ReadOnlySpan<byte>`. It does not resolve by `Dictionary<string,string>`.
- Hot decode writes UTF-8 directly into caller-owned `Span<char>` and commits to TMP through `SetCharArray`.
- Dynamic token injection supports `^0..^3`, `{0}`, and `{0:format}` through `BabelFormatArgs` and `ZeroGCFormatter`; `string.Format` is not part of the hot path.
- `LocRegistry.ReloadBinaryOrMock(...)` loads only static/binary UTF-8 authority or the unmanaged emergency mock. The previous dictionary reload bridge has been removed from `LocRegistry`.
- `LocalizationManager` still exposes managed string compatibility APIs for older callers, but current-language lookup hashes into Babel first and built-in fallback strings resolve through static switch dispatch. It no longer owns runtime `Dictionary<string,string>` language tables.
- Legacy mod localization injection is disabled. `ModLocalizationBridge` ignores discovered JSON language files, and `HectonAPI.Localization` exposes only a rejected future `InjectBabelEnvelope(ReadOnlySpan<byte>)` seam until binary/hash mod envelopes exist.
- Legacy JSON parsing for key generation and CJK validation lives only in `Assets/_Project/Scripts/Editor/LocalizationEditorJsonTableParser.cs`; it is Editor-only tooling and is not a runtime localization authority.
- The legacy string compatibility formatter is quarantined behind `string.Create` plus primitive `TryFormat`; numeric format suffixes are preserved, but the method still returns a managed string and is not zero-GC proof.
- Fallback decode storage is capped at 4096 glyphs so the static 500-word paragraph audit path is no longer truncated at the old 1024-glyph ceiling. Larger PDA/lore views should pass their own caller-owned span to `TryWriteVisualSpanFromUtf8(...)`.
- Legacy `ResolveRaw`/`TryGetRawBuffer` compatibility APIs use a fixed 16-slot prewarmed `char[4096]` decode ring. The previous thread-static grow-on-first-use decode buffer is removed. This ring is not the subtitle hot path; new subtitle and lore surfaces should still provide caller-owned spans or `CharBufferPool` leases.
- `LocNumericBuffer` uses a fixed 16-slot prewarmed `char[4096]` numeric formatting ring for compatibility APIs that return `char[]`. The previous thread-static grow/expand route and `new char[capacity]` overflow fallback are removed; hot callers should prefer caller-owned `Span<char>` overloads.
- `CharBufferPool.RequiredBabelTextCapacity` is 512 chars across 500 subtitle slots. Megabyte lore windows must use encyclopedia page leases or caller-owned page spans, not inflate the common subtitle lease.
- `LocRegistry` missing-key suppression uses a fixed 256-bit bloom mask (`ulong * 4`), not `HashSet<int>`. It is diagnostic-only and cannot grow managed memory during missing-token storms.
- `SubtitleManager` legacy string subtitle requests use the same fixed 8-slot ring discipline as the Babel command and buffered-span queues. The previous managed `List<SubtitleRequest>` queue has been removed.

## Subtitle Clock

`BabelSubtitleSyncRuntime` evaluates subtitle cue visibility against `AudioSettings.dspTime * AudioSettings.outputSampleRate`, stored as `uint AudioFrameClock`. `SubtitleManager` arms durations as audio-frame intervals and derives remaining time from the same frame clock. `LocalizationManager` PDA corrosion and madness visual override windows also expire by DSP/audio-frame counters, with wrap-safe `uint` signed-diff comparison. `Time.deltaTime` is retained only for visual alpha smoothing, not subtitle truth.

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

Registry-side public DTOs/signals are explicit-layout records: `LocalizationEntryDTO` 16 bytes, `SubtitleCommandDTO` 16 bytes, `SubtitleStateDTO` 16 bytes, `BabelFormatArgs` 24 bytes, `BabelDictionaryStage` 32 bytes, and `BabelTelemetryEntry` 64 bytes. No `Pack=1` or sequential-layout registry DTO remains in the SHINOBU runtime registry.

## Vault And Dispatch

Runtime persistent state is requested from `GlobalDataVault` and resolved into transient `NativeArray<T>` views only when needed. SHINOBU_150 does not add IDs to the core `BufferID` enum; the domain-local IDs are cast constants:

- `(BufferID)70540`: `char[500 * 512]` Babel UTF-16 arena, owned by `CharBufferPool`
- `(BufferID)15070550`: `SubtitleCueDTO[64]`
- `(BufferID)15070551`: `LocalizationTelemetryEntry[300]`

`CharBufferPool` no longer creates a persistent private `NativeArray<char>` or `NativeBitArray` fallback. If the Vault arena is unavailable in a cold/editor mock route, Babel leases write directly into the already prewarmed TMP bridge slot and keep the same `SetCharArray` commit path.

`EvaluateSubtitleCuesJob` and `ClearSubtitleCueFlagsJob` both use `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Cue evaluation is scheduled through `DispatcherBridge.ScheduleSimulation`; if `PreSimulationTick` already scheduled the job, the dispatcher receives `JobHandle.CombineDependencies(dependsOn, pendingCueEvaluationHandle)`. Completion uses an `IsCompleted` guard before `Complete()` to avoid arbitrary main-thread blocking.

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
