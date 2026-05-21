# SHINOBU_260 Vocal Synthesis Pipeline

Status: STATIC_SOURCE_PENDING_IMPORT

## Route

`Docs/Audio/dialogue_script.csv` -> `Tools/voice_baker.py` -> `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` -> `SignalBus<VocalCueSignal>` -> `VocalBankPlaybackRuntime.OnAudioFilterRead`.

Runtime voice playback does not use JSON, managed voice `AudioClip` tables, runtime string IDs, or AudioSource instantiation. Producers publish a 32-bit FNV-1a phrase hash in `VocalCueSignal`.

`OnAudioFilterRead` is a legacy Unity callback seam required by the SHINOBU_260 XML prompt. The project audio mandate still prefers DSPGraph/`IAudioOutputJob` for future production hardening. Until that route is implemented, the callback uses a pinned Unity `float[]` for the callback boundary and does not allocate inside the decode loop.

Default callback mode is master-listener mix mode: decoded voice samples are added to the existing audio graph and idle/fault states leave the existing mix untouched. A source-driver overwrite mode remains available only when a preexisting dedicated host is supplied; SHINOBU_260 does not create an AudioSource GameObject or driver AudioClip at runtime.

Legacy `PlayerCriticalProceduralAudioRenderer` warning PCM lanes are removed from the active producer path. `VocalWarningSystem` publishes hash-only `VocalCueSignal` payloads; the renderer no longer allocates or mixes double-buffered vocal-warning PCM samples. The remaining authored `AudioClip` scratch in that renderer is named for cold metal-stress grain import and is not a dialogue/protagonist voice route.

## H8BIN Layout

- Header: `VocalBankHeaderDTO`, 64 bytes, little-endian, magic `H8VB`.
- Index: sorted `VocalBankIndexRecordDTO[RecordCount]`, 32 bytes per row.
- Payload: mono PCM16, H8ADPCM, or Vorbis bytes with 16-byte aligned record starts and zeroed inter-record padding.

`voice_baker.py` defaults to 44.1 kHz H8ADPCM. Current Burst runtime supports PCM16 and H8ADPCM. Vorbis can be packed only with `--allow-runtime-unsupported-vorbis` for archival/high-fidelity authoring, and playback fails closed until a native Vorbis route is added and profiled.

`Tools/h8bin_validator.py` routes magic `H8VB` before Data Monolith parsing and validates the vocal sidecar as its own owner schema: 64-byte header, 32-byte sorted records, FNV bank hash, 16-byte payload starts, zeroed inter-record padding, contiguous aligned payload ranges, mono/sample-rate lanes, runtime codec set, and H8ADPCM block headers. Current gate proof for the generated bank is `H8VB_SCHEMA_VALIDATED`; the remaining global validator failure is the unrelated missing `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

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

The manager stores generation handles and audio-thread raw pointers derived from Vault views. It does not own private persistent `NativeArray` allocations. MMF release is fenced by an audio-callback in-flight counter so hot-swap/teardown cannot release a mapped view while the callback is inside the Burst function pointer.

## Dear Lie Radio Filter

The "Sweet Lie" radio effect is a mathematical fake: one-pole low state, band state, soft saturation, deterministic static noise, and quality-scaled quantization. No AudioMixer graph or physical radio simulation is introduced.

`GlobalQualityWeight` continuously changes sample stride and filter density. Low quality collapses to coarse held samples plus cheap bandpass. High quality keeps stride 1 and denser quantization. `VocalStateDTO` remains the mandated 32-byte playhead row; payload and filter metadata live in the 64-byte codec row. Hash identity, DTO layout, and save/rollback ownership stay fixed.

## Rollback Boundary

Voice playback is presentation-only. It must not enter StateRingBuffer, save Merkle, WAL, deterministic gameplay authority, or network rollback truth. Critical proof is the 300-frame black-box dump at `Docs/AgentLogs/Dump_SHINOBU_260.bin`.

## Static Verification

2026-05-21 source-only checks:

- `python Tools\voice_baker.py --csv Docs\Audio\dialogue_script.csv --out Assets\StreamingAssets\Hecton8\Audio\vocal_banks.h8bin --codec h8adpcm` wrote a 36,096-byte H8ADPCM bank.
- `python -B Tools\test_h8bin_validator.py` passed 52 tests.
- `python Tools\AudioClip_Reference_Scanner.py` reported zero director/protagonist managed voice suspects.
- Static residual-VWS scan found no `RenderVocalWarningSample`, `TryActivatePendingVocalWarning`, VWS pending buffer fields, or VWS clip sample Vault handles in `PlayerCriticalProceduralAudioRenderer`.
- `python -B Tools\h8bin_validator.py --target-dir Assets\StreamingAssets` reported `H8VB_SCHEMA_VALIDATED`; the command still fails globally on unrelated runtime text-loading findings and missing `DataMonolith/static_data.h8bin`.
- `dotnet`/Unity compile was not launched because CPU sampled at 100 percent, above the explicit 50 percent build gate.
