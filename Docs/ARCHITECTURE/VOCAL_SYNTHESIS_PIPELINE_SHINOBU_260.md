# SHINOBU_260 Vocal Synthesis Pipeline

Status: STATIC_SOURCE_PENDING_IMPORT

## Route

`Docs/Audio/dialogue_script.csv` -> `Tools/voice_baker.py` -> `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` -> `SignalBus<VocalCueSignal>` -> `VocalBankPlaybackRuntime.OnAudioFilterRead`.

Runtime voice playback does not use JSON, managed voice `AudioClip` tables, runtime string IDs, or AudioSource instantiation. Producers publish a 32-bit FNV-1a phrase hash in `VocalCueSignal`.

`OnAudioFilterRead` is a legacy Unity callback seam required by the SHINOBU_260 XML prompt.

Project audio mandate still prefers DSPGraph/`IAudioOutputJob` for future hardening.

Until then, callback uses pinned Unity `float[]` at boundary and does not allocate inside decode loop.

Default callback mode: master-listener mix.

- Decoded voice samples add into the existing audio graph.
- Idle/fault states leave the existing mix untouched.
- Source-driver overwrite mode requires a preexisting dedicated host.
- SHINOBU_260 creates no AudioSource GameObject or driver AudioClip at runtime.

Legacy warning route:

- `PlayerCriticalProceduralAudioRenderer` warning PCM lanes are out of the active producer path.
- `VocalWarningSystem` publishes hash-only `VocalCueSignal` payloads.
- Renderer no longer allocates or mixes double-buffered vocal-warning PCM samples.
- Remaining authored `AudioClip` scratch is cold metal-stress grain import, not dialogue/protagonist voice.

## H8BIN Layout

- Header: `VocalBankHeaderDTO`, 64 bytes, little-endian, magic `H8VB`.
- Index: sorted `VocalBankIndexRecordDTO[RecordCount]`, 32 bytes per row.
- Payload: mono PCM16, H8ADPCM, or Vorbis bytes with 16-byte aligned record starts and zeroed inter-record padding.

- `voice_baker.py` default: 44.1 kHz H8ADPCM.
- Current Burst runtime supports PCM16 and H8ADPCM.
- Vorbis packing requires `--allow-runtime-unsupported-vorbis`.
- Vorbis use is archival/high-fidelity authoring only.
- Vorbis playback fails closed until a native profiled route exists.

- `Tools/h8bin_validator.py` routes magic `H8VB` before Data Monolith parsing.
- Vocal sidecar schema gates: 64-byte header, 32-byte sorted records, FNV bank hash, 16-byte payload starts.
- Additional gates: zeroed inter-record padding, contiguous aligned payload ranges, mono/sample-rate lanes, runtime codec set, H8ADPCM block headers.
- Current sidecar proof for the generated bank remains `H8VB_SCHEMA_VALIDATED`.
- 2026-05-28 scoped payload validator recheck also validates the current `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`; this is Python schema/payload proof only, not Unity boot or audio-runtime proof.

## Runtime Memory

Owner: `SystemID.AudioVocalSynthesis`.

Vault buffers:

- `72420` active 32-byte playhead state
- `72421` codec/filter state
- `72422` telemetry ring, 300 x 64 bytes
- `72423` cache-line isolated counters
- `72424` editor waveform ring
- `72425` reserved waveform cursor lane
- `72426` emergency mock bank bytes
- `72427` emergency mock bank records
- `72428` CSV metadata rows
- `72429` CSV byte scratch

Rejected range: `71860..71869`, already occupied by `SHINOBU_160` telemetry exporter local BufferIDs.

The manager stores generation handles and audio-thread raw pointers derived from Vault views.

It owns no persistent `NativeArray` allocations.
MMF release is fenced by an audio-callback in-flight counter.
Hot-swap/teardown cannot release a mapped view during the Burst function pointer.

## Dear Lie Radio Filter

"Sweet Lie" radio is a math fake: one-pole low/band state, soft saturation, deterministic noise, quality-scaled quantization. No AudioMixer graph or physical radio simulation.

`GlobalQualityWeight` changes sample stride and filter density.

- Low: coarse held samples plus cheap bandpass.
- High: stride `1` plus denser quantization.
- `VocalStateDTO`: mandated 32-byte playhead row.
- Payload/filter metadata: 64-byte codec row.
- Hash identity, DTO layout, and save/rollback ownership stay fixed.

## Rollback Boundary

Voice playback is presentation-only.

Forbidden authority lanes:

- StateRingBuffer
- save Merkle
- WAL
- deterministic gameplay authority
- network rollback truth

Fault proof target: `Docs/AgentLogs/Dump_SHINOBU_260.bin`.

No current dump file exists in active AgentLogs.

## Static Verification

2026-05-21 source-only checks:

- `python Tools\voice_baker.py --csv Docs\Audio\dialogue_script.csv --out Assets\StreamingAssets\Hecton8\Audio\vocal_banks.h8bin --codec h8adpcm` wrote a 36,096-byte H8ADPCM bank.
- `python -B Tools\test_h8bin_validator.py` passed 52 tests.
- `python Tools\AudioClip_Reference_Scanner.py` reported zero director/protagonist managed voice suspects.
- Static residual-VWS scan found no `RenderVocalWarningSample`, `TryActivatePendingVocalWarning`, VWS pending buffer fields, or VWS clip sample Vault handles in `PlayerCriticalProceduralAudioRenderer`.
- Historical `python -B Tools\h8bin_validator.py --target-dir Assets\StreamingAssets` reported `H8VB_SCHEMA_VALIDATED` while the Data Monolith payload was still absent.
- 2026-05-28 scoped h8bin recheck returned `PASS`, `files=2`, `structs=32`, `mb=1.0495`, `seconds=0.491846`.
- Report: `Docs\Reports\DOC_ROOT_ARCH_AUDIT_h8bin_validator_narrow_20260528.json`.
- `dotnet`/Unity compile was not launched because CPU sampled at 100 percent, above the explicit 50 percent build gate.
