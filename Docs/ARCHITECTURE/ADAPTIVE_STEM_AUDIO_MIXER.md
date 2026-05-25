# Adaptive Stem Audio Mixer

Date: 2026-05-18

Status: STATIC_SOURCE ORIENTATION / RUNTIME PROOF PENDING

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity import, audio-runtime, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs`

- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`

- `Assets/_Project/Scripts/Audio/HectonMusicDirectorConfig.cs`

- `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`

- `Assets/_Project/Data/Audio/Music/Profiles`

## Runtime Contract

- Runtime owner: `AdaptiveStemAudioMixer`.

- Persistent memory owner: `SystemID.AudioStemMixer`.

- Vault buffers:

  - `AudioStemState` - one `AudioStemStateDTO`, 16 bytes.

  - `AudioStemCommands` - two `StemCommandDTO` lanes, 16 bytes each.

  - `AudioStemMixFrame` - one 64-byte frame for Unity AudioSource application.

  - `AudioStemRules` - one 128-byte tuning block.

  - `AudioStemMockPredator` / `AudioStemMockDepth` / `AudioStemMockTension` - blind dependency mocks.

  - `AudioStemTelemetry` - 300 entries, 64 bytes each.

  - `AudioStemTelemetryCursor` - circular write cursor.

  - `AudioStemCsvScratch` - 4096-byte ASCII CSV scratch.

## Hot Path

- No coroutine fades.

- No `AudioMixer.SetFloat` string path.

- Burst jobs use `FloatMode.Fast`, `NoAlias` unsafe pointers, and `UnsafeUtility.AsRef`.

- `Tick` schedules stimulus, tension, and crossfade jobs only when quality cadence elapses; skipped frames keep the last mix and write lightweight telemetry.

- Solver depends on mock/tension through `JobHandle.CombineDependencies`; the predator/depth/tension mock producer is Burst, not a managed oscillator.

- Unity `AudioSource.volume` and `AudioLowPassFilter.cutoffFrequency` assignment is cold/low-cadence legacy endpoint only.
- Primary audio truth remains DSPGraph/ParamSnapshot. Runtime, profiler, GC, and platform proof remain absent.

- Runtime frame labels use the dispatcher's local simulation counter, not `Time.frameCount`.

- The depth dread fake is a scalar LPF cutoff: `22000 Hz -> 800 Hz`.

- `GlobalQualityWeight` is a continuous tier/precision/health-pressure scalar; severe pressure moves the full audio kernel batch toward 5 Hz instead of flipping a binary mode.

- Runtime quality authority: Homeostasis-owned `BufferID.ShinobuScalabilityState` (`ScalabilityStateDTO.GlobalQualityWeight`).
- Runtime no longer drains `ScalabilityChangedEvent` as fallback quality mapper.
- If the vault lane is temporarily absent, the last sanitized continuous weight is preserved.
- Steady-state audio path does not poll `GlobalRegistry.ScalabilityTier` or `GlobalRegistry.MathPrecisionLowBlend01`.
- Dynamic music scalar publication keeps `lowTierFrameSignals` equal to the full 64-frame lane budget so the signal route does not shed events through a binary hardware profile.
- Crossfade alpha is polynomial (`x * (2 - x)`) over the accumulated cadence delta; no exponential fade math remains in the solver.

## Tuning

- Editor window: `Hecton8/Audio/Adaptive Audio Tuner`.

- CSV override: `Docs/Audio/audio_stem_rules.csv`.

- CSV path resolution is cached; file metadata probes are throttled to every two SlowTicks.

- Editor-only import repair sets assigned stem clips to Streaming, Vorbis Q70, 44100 Hz, preload off, and background loading.

- Editor rule/mix/telemetry reads use the non-blocking job flush gate and refuse vault access while audio jobs are still running.

- Fault dump: `Docs/AgentLogs/Dump_STEM_MIXER.bin` when update cost exceeds 1000 us or non-finite state is detected; the dump writes telemetry NativeArray as a span, without a managed staging `byte[]`.
