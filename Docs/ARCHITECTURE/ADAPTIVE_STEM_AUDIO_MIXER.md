# Adaptive Stem Audio Mixer

Date: 2026-05-18
Status: STATIC_SOURCE ORIENTATION / RUNTIME PROOF PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM path check. These anchors prove current path visibility only, not Unity import, audio-runtime, profiler, GC, or player-build proof.

- `Assets/_Project/Scripts/Audio/AdaptiveStem/AdaptiveStemAudioMixer.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirector.cs`
- `Assets/_Project/Scripts/Audio/HectonMusicDirectorConfig.cs`
- `Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset`
- `Assets/_Project/Data/Audio/Music/Profiles`

## 2026-05-20 DOC_GLOBAL R46 Root/Architecture Boundary Note

R51 root/architecture encoding/boundary/read-order/route-card/source-counter correction (`Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`) keeps this file as a static architecture/source contract, not runtime proof. Current DOC_GLOBAL boundary is `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md`; R50 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R50_ROOT_ARCHITECTURE_ATLAS_REGEN_R48_INTERIOR_DUMPTARGET_AND_COUNTER_DRIFT_LOCAL.md`; R49 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R49_ROOT_ARCHITECTURE_ATLASCHECK_BOUNDARY_ROUTE_FIELDS_AND_COUNTER_DRIFT_LOCAL.md`; R48 remains prior at `Docs/Reports/2026-05-21_DOCUMENTATION_R48_ROOT_ARCHITECTURE_DATE_ROLLOVER_ATLASCHECK_AND_COUNTER_REFRESH_LOCAL.md`; R47 remains prior at `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`; R46/R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current static gates: `Tools/AtlasCheck.py` remains red on `ATLAS_CHECK_FAIL references=6881 missing=60` (one Dynamic Decals vendor asset ref, RealtimeCSG vendor icon/readme image refs, and missing HectonMaskChannelPacker and HectonMaterialChannelPackValidator source refs in the current atlas); `Docs/Modding/Validate_Mod_API_Static.ps1` passes (`Status=PASS`, `SchemaRevision=16`, `SourceSignals=162`, `ModCommandSizeBytes=64`) as static-tool orientation only. Runtime proof remains absent.

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
- Runtime quality authority is the Homeostasis-owned vault lane `BufferID.ShinobuScalabilityState` (`ScalabilityStateDTO.GlobalQualityWeight`). The runtime no longer drains `ScalabilityChangedEvent` as a fallback quality mapper; if the vault lane is temporarily absent it preserves the last sanitized continuous weight. The steady-state audio path does not poll `GlobalRegistry.ScalabilityTier` or `GlobalRegistry.MathPrecisionLowBlend01`.
- Dynamic music scalar publication keeps `lowTierFrameSignals` equal to the full 64-frame lane budget so the signal route does not shed events through a binary hardware profile.
- Crossfade alpha is polynomial (`x * (2 - x)`) over the accumulated cadence delta; no exponential fade math remains in the solver.

## Tuning

- Editor window: `Hecton8/Audio/Adaptive Audio Tuner`.
- CSV override: `Docs/Audio/audio_stem_rules.csv`.
- CSV path resolution is cached; file metadata probes are throttled to every two SlowTicks.
- Editor-only import repair sets assigned stem clips to Streaming, Vorbis Q70, 44100 Hz, preload off, and background loading.
- Editor rule/mix/telemetry reads use the non-blocking job flush gate and refuse vault access while audio jobs are still running.
- Fault dump: `Docs/AgentLogs/Dump_STEM_MIXER.bin` when update cost exceeds 1000 us or non-finite state is detected; the dump writes the telemetry NativeArray as a span, without a managed staging `byte[]`.
