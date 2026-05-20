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
R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity import, audio-runtime, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirectorConfig.cs`
- `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`
- `Assets/_Project/Data/Audio/Music/Profiles`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R46 root/architecture interior-authority/route-field/proof-language correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`) (R45 prior R43/R44 residue/proof-artifact/source-counter correction) keeps this file as a static architecture contract, not audio-runtime proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-20_DOCUMENTATION_R46_ROOT_ARCHITECTURE_INTERIOR_AUTHORITY_ROUTE_FIELDS_AND_PROOF_LANGUAGE_LOCAL.md`; R45 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`; R44 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R44_ROOT_ARCHITECTURE_INTERNAL_RESIDUE_EXACT_ROUTE_FIELDS_LOCAL.md`; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6741 missing=59` (one Dynamic Decals missing vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HabitatDamageBakePipeline source ref in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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
- Unity component assignment through `AudioSource.volume` and `AudioLowPassFilter.cutoffFrequency` is a cold/low-cadence legacy endpoint only. Primary runtime audio truth remains the DSPGraph/ParamSnapshot route; this document does not prove audio runtime, profiler, GC, or platform behavior.
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



