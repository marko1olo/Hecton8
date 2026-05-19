# Adaptive Stem Audio Mixer

Date: 2026-05-18
Status: STATIC_SOURCE ORIENTATION / RUNTIME PROOF PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or audio runtime proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## 2026-05-19 DOC_GLOBAL R31 Current Boundary Note

R31 reread confirmed this file remains a static architecture contract, not audio-runtime proof. Current root/architecture boundary is `Docs/Reports/2026-05-19_DOCUMENTATION_R31_ARCHITECTURE_CURRENT_BOUNDARY_PROPAGATION_LOCAL.md`; R30 remains the prior internal-currentness correction, R29 remains the prior stale-gate/global-authority correction, R28 remains the prior interior-boundary correction, and R27 source counters are retained until a newer counter pass reruns them. Current static gates: `Tools/AtlasCheck.py` remains red on `57` RealtimeCSG vendor references; `Docs/Modding/Validate_Mod_API_Static.ps1` now passes (`Status=PASS`, `SchemaRevision=14`, `SourceSignals=160`, `ModCommandSizeBytes=64`) as static-tool orientation only; do not treat PASS as current proof without artifact path, command, timestamp, environment, and output. Unity/runtime/profiler/player-build proof remains absent.

Owner: SHINOBU_46 / Hecton8.Audio

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
- `Tick` schedules the mock stimulus, tension, and crossfade job batch only when the continuous quality cadence elapses; skipped cadence frames keep the last mix frame and write lightweight telemetry.
- Solver depends on mock/tension through `JobHandle.CombineDependencies`; the predator/depth/tension mock producer is Burst, not a managed oscillator.
- Unity boundary is direct `AudioSource.volume` and `AudioLowPassFilter.cutoffFrequency` assignment.
- Runtime frame labels use the dispatcher's local simulation counter, not `Time.frameCount`.
- The depth dread fake is a scalar LPF cutoff: `22000 Hz -> 800 Hz`.
- `GlobalQualityWeight` is a continuous tier/precision/health-pressure scalar; severe pressure moves the full audio kernel batch toward 5 Hz instead of flipping a binary mode.
- Runtime quality authority is the Homeostasis-owned vault lane `BufferID.ShinobuScalabilityState` (`ScalabilityStateDTO.GlobalQualityWeight`). `ScalabilityChangedEvent` is only a fallback when the vault lane is absent; the steady-state audio path does not poll `GlobalRegistry.ScalabilityTier` or `GlobalRegistry.MathPrecisionLowBlend01`.
- Crossfade alpha is polynomial (`x * (2 - x)`) over the accumulated cadence delta; no exponential fade math remains in the solver.

## Tuning

- Editor window: `Hecton8/Audio/Adaptive Audio Tuner`.
- CSV override: `Docs/Audio/audio_stem_rules.csv`.
- CSV path resolution is cached; file metadata probes are throttled to every two SlowTicks.
- Editor-only import repair sets assigned stem clips to Streaming, Vorbis Q70, 44100 Hz, preload off, and background loading.
- Editor rule/mix/telemetry reads use the non-blocking job flush gate and refuse vault access while audio jobs are still running.
- Fault dump: `Docs/AgentLogs/Dump_STEM_MIXER.bin` when update cost exceeds 1000 us or non-finite state is detected; the dump writes the telemetry NativeArray as a span, without a managed staging `byte[]`.
