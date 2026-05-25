# SHINOBU_352 Vocal Warning Queue

Owner: `Assets/_Project/Scripts/Audio/VocalWarningSystem.cs`

Runtime route:

- Producers publish typed `SignalBus<T>` packets or call the cold `IVocalWarningSystem.TryQueueWarning` bridge.
- `VocalWarningSystem` evaluates warnings in `DispatcherPhase.PostSimulation`.
- Dispatcher frames use `DispatcherTimingDTO.FrameId`; fallback editor/registration paths use an owner-local monotonic frame id and do not read Unity `Time.frameCount`.
- Pending voice lines live in `GlobalDataVault` as `NativeMinHeap<VocalWarningDTO>` over `BufferID.AudioVocalWarningQueue`.
- `VocalWarningDTO` is fixed at 16 bytes: `uint AudioBankHashID`, `float PriorityScore`, `float ExpirationTime`, `uint Flags`.
- Dispatch emits hash-only `VocalCueSignal` and `SubtitleCueSignal` directly through typed `SignalBus<T>.TryPush` lanes. The queue owner does not create managed strings, clips, or subtitles.
- `SubtitleCueSignal.StartAudioFrame == 0` is a documented owner-phase sentinel: the subtitle runtime resolves it to its current audio-frame clock so VWS does not reference UI concrete clocks.
- Rejected vocal/subtitle lanes set Vault heap fault bits.
- If vocal cue lane rejects a packet, current playback state is cleared.
- Telemetry cannot claim an active line when no cue was accepted.
- SHINOBU_352 local Vault lanes are casted `BufferID` constants in `VocalWarningSystem.cs`, not new `H8Memory` enum entries: `72430 HeapState`, `72431 CurrentState`, `72432 Dispatch`, `72433 Profiles`, `72434 CsvScratch`, `72435 Tuning`.

Priority rule:

- Water breach / hull breach hashes resolve to base priority `1000` plus critical boost.
- Battery low resolves to base priority `120`.
- Tuning lives in one 64-byte `VocalWarningTuningDTO` Vault row.
- Fields: base priorities, critical boost, producer scale, severity boost, interruption threshold.
- Defaults: hull `1000`, crush `940`, oxygen `820`, radiation `430`, power `120`.
- Extra defaults: critical boost `220`, interruption threshold `180`.
- Active playback is interrupted only when the pending warning exceeds current priority by the tuning threshold and carries critical/interrupt flags.
- Therefore hull breach mathematically preempts battery low without object identity, clip references, or string comparisons.

Scalability:

- `MaxEvaluations = round(lerp(8, 64, GlobalQualityWeight))`.
- Low devices evaluate the hard survival subset.
- Middle devices admit broader signal fan-in.
- High and Ultra devices keep the same truth route while spending saved budget on richer radio distortion, spatial blend, debug telemetry, and editor visualization.

Rollback fence:

- The vocal queue is presentation-only. It is not part of authoritative gameplay state, save identity, or deterministic rollback hashes.
- Authoritative systems own health, flood, oxygen, radiation, and power facts. VWS reads immutable signal snapshots and dispatches presentation cues only.

Black box:

- Last 300 frames are written to `BufferID.AudioVocalWarningTelemetry`.
- Fault or overbudget state dumps to `Docs/AgentLogs/Dump_SHINOBU_352.bin`.
- Dump format is a 32-byte `VwsTelemetryDumpHeader` followed by oldest-to-newest raw `VwsTelemetryEntry` rows. The writer uses `FileStream.Write(ReadOnlySpan<byte>)` from the native ring and does not use `BinaryWriter`.
- Queue/current/dispatch/tuning/profile/telemetry owner writes use raw `NativeArrayUnsafeUtility` pointers plus `UnsafeUtility.AsRef`; heap swaps already use raw refs through `VocalWarningHeapOps`.

Editor proof:

- `VocalWarningQueueTunerWindow` edits the Vault tuning row and injects hull/power/mock warnings without creating runtime text routes.
- `VocalWarningQueueDebugGizmo` displays pending count, current priority, and the first three raw heap rows with hash and priority.
- `OOP_Voice_Scanner_SHINOBU_352` is a Roslyn AST-primary editor scanner for gameplay `AudioSource.PlayOneShot`, subtitle, and managed voice-queue regressions. It writes a SHINOBU_352 sidecar report and a non-destructive section in `Docs/Reports/AUDIO_OPTIMIZATION_REPORT.json`.
