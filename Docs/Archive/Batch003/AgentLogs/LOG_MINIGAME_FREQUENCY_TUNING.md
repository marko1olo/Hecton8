# LOG_MINIGAME_FREQUENCY_TUNING

## 2026-05-12 - Scanner Artifact Decryption / Sine Wave Hacking

Status: PENDING VERIFICATION. Compile is blocked by external dependency walls, not marked verified.

What was wrong:
- Scanner artifact interaction was architecturally tied to passive hold/poll behavior and old Canvas-style spectrogram assumptions.
- No `ScannerToolActiveSignal` lane existed for a decoupled minigame activation handoff.
- The panel used managed UI-style concepts instead of native arrays, Burst waveform work, GPU-buffer presentation, and fixed feedback signals.
- Low-tier first pass used 32 active samples but still allocated high-tier wave capacity; Omega polish corrected this to real 32-sample allocation.

What was done:
- Added `ScannerToolActiveSignal` to `GlobalSignals` and made `ScannerTool` publish scanner target state without direct minigame dependencies.
- Rebuilt `PDADecryptionSpectrogramPanel` as a dispatcher-driven native/Burst minigame: target/player `NativeArray<float>`, 3-stage locks, `math.sin`, `math.abs`, 2-second continuous unlock, and `BlueprintUnlockedSignal` emission.
- Added persistent `GraphicsBuffer` upload and `Graphics.RenderMeshIndirect` draw path for red target and cyan player waves through `Hecton_PDA_FrequencyTuningWave.shader`.
- Routed feedback through `_HectonFrequencyTuningError01`, `ToolAcousticSignal`, `PlayerSignalEvents`, and `ToolHapticsRuntime`.
- Added a 300-frame native telemetry ring and cold binary dump path at `Docs/AgentLogs/Dump_MINIGAME_FREQUENCY_TUNING.bin`.
- Updated `SignalCryptographySmokeTester` to enforce the new frequency-tuning requirements.
- Omega polish replaced a literal seed-normalization division with `Hash24ToUnit` and confirmed no forbidden APIs remain in the spectrogram panel.

Cinematic Cheats used:
- Local normalized sine samples instead of a physical radio/signal simulation.
- BRG segment/tube impostor shader instead of `LineRenderer` or CPU mesh curves.
- Scalar visor aberration/static/haptic feedback instead of simulated alien interference.
- Deterministic hard-mode coherent-noise drift instead of random target motion.
- Math LOD: Low/MX350 = 32 samples and 62 GPU segments; Mid/High/Ultra = 128 samples and 254 GPU segments.

Microseconds saved, estimated because global compile prevents profiler proof:
- No singleton/poll bridge: 5 us.
- Fixed 32-byte scanner signal instead of string/UI event routing: 8 us.
- Removed old slider/image/TMP spectrogram hot path: 65 us.
- Burst sine generation/error aggregation versus main-thread managed loops: 30 us high-tier, 14 us low-tier.
- Stage bitmask/no coroutine sequencing: 5 us.
- BRG buffer draw versus LineRenderer/Canvas curve refresh: 45 us.
- Shader global feedback instead of material clone: 5 us.
- Fixed signal audio/haptic path instead of direct component mutation: 10 us.
- Low-tier 32-sample allocation/upload versus fixed 128: 14 us on MX350.
- Precomputed seed reciprocal: 0.4 us.

Total estimated active-frame saving:
- Low/MX350: 171.4 us.
- Mid/High/Ultra: 187.4 us CPU-side headroom, spent on cleaner BRG/shader presentation rather than Canvas work.

Verification:
- Source scans confirm `PDADecryptionSpectrogramPanel` has no `foreach`, `string.Format`, `.ToString()`, `Mathf.Abs`, `math.sqrt`, `math.normalize`, `UnityEngine.UI`, `LineRenderer`, or `MinigameManager.Instance`.
- `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false` fails before verification on unrelated missing `Hecton8.Core.Memory`, `Hecton8.Physics.Determinism`, `Hecton8.Cartography`, `IDataVault`, and `SystemID` dependencies.

## 2026-05-13 - AAA Recheck / Segment Tube Pass

Status: PENDING VERIFICATION. `dotnet build` was not launched per user instruction.

What was wrong:
- `LateFrameTick` used `Time.deltaTime` to commit match progress after dispatcher jobs completed.
- The first GPU presentation path still behaved like point beads, not a continuous sine-wave tube.
- `SignalCryptographySmokeTester` inspected the no-arg error job when it claimed to inspect the sine generation job.

What was done:
- Cached sanitized `Tick(float deltaTime)` into `_lastTickDeltaTime` and used that value in `CommitWaveResult`.
- Replaced point payloads with `FrequencyTuningWaveGpuSegment` center/tangent/length data and bound `_HectonFrequencyTuningSegments`.
- Updated the shader to expand each segment into a continuous PDA tube strip.
- Used `math.rsqrt` for segment tangent setup and reduced GPU instances to 62 on Low/MX350 and 254 on Mid/High/Ultra.
- Corrected the editor smoke audit to extract `Execute(int index)` for generation and `Execute()` for error reduction.

Cinematic Cheats used:
- Continuous tube impostor strips instead of CPU-generated spline mesh or physical signal rendering.
- Dispatcher-time handoff instead of sampling Unity static frame time in the late-frame lane.
- QA source-body extraction instead of launching a forbidden build.

Microseconds saved, estimated because no build/profiler run was permitted:
- Removed late-frame static delta sample: 0 us runtime, deterministic timing gain only.
- Segment count reduction versus point/bead capacity: 0.2 us on Low/MX350 and 0.2 us on high tiers.
- Smoke tester extraction fix: 0 us runtime.

Verification:
- Forbidden-token scan returned no matches for `Time.deltaTime`, `FrequencyTuningWaveGpuPoint`, `_HectonFrequencyTuningPoints`, `LineRenderer`, `UnityEngine.UI`, `Mathf.Abs`, `math.sqrt`, `math.normalize`, `.text =`, `SetText(`, or `MinigameManager.Instance` in the minigame/shader files.
- Required-token scan found `FrequencyTuningWaveGpuSegment`, `_HectonFrequencyTuningSegments`, `math.rsqrt`, `Graphics.RenderMeshIndirect`, `GraphicsBufferUploadUtility.UploadNativeArray`, native wave arrays, `IJobParallelFor`, `math.sin`, and `math.abs`.
- Trailing-whitespace scan returned no matches.
- `git diff --check` returned only CRLF normalization warnings for touched C# files.

## 2026-05-13 - Activation/Lifecycle Recheck

Status: PENDING VERIFICATION. `dotnet build` was not launched per user instruction.

What was wrong:
- Scanner-active signals were event-only, so a newly created PDA panel could miss an already-active scanner after the frame snapshot was drained.
- Render, feedback, telemetry, hard drift, and unlock code sampled Unity time/frame globals from multiple paths.
- The smoke test did not enforce the latest-state fallback or late-frame time-cache rule.

What was done:
- Added latest `ScannerToolActiveSignal` storage and sequence tracking in `GlobalSignals`.
- Added a sequence-guarded latest-signal fallback in `PDADecryptionSpectrogramPanel`.
- Cached `Time.unscaledTime` and `Time.frameCount` once in `Tick`; late-frame render/feedback/telemetry/unlock paths now use `_lastTickUnscaledTime` and `_lastTickFrame`.
- Extended `SignalCryptographySmokeTester` to require `TryGetLatestScannerToolActiveSignal` and no `Time.` usage in `LateFrameTick`.

Cinematic Cheats used:
- Latest 32-byte signal snapshot instead of scanner polling or per-frame event spam.
- One dispatcher-frame time sample reused across all visual/audio/haptic feedback.

Microseconds saved, estimated because build/profiler validation was not permitted:
- Avoided per-frame scanner-active signal spam: queue growth prevented; direct per-frame cost avoided depends on scanner count, estimated 3-8 us under active scanning.
- Collapsed repeated time/frame reads: sub-1 us.
- Smoke-test additions: 0 us runtime.

Verification:
- Forbidden-token scan found no `Time.deltaTime`, old point buffer names, `LineRenderer`, `UnityEngine.UI`, `Mathf.Abs`, `math.sqrt`, `math.normalize`, `.text =`, `SetText(`, or `MinigameManager.Instance` in the minigame/shader files.
- Required-token scan found latest scanner fallback, cached frame fields, segment renderer, rsqrt, indirect draw, native arrays, Burst sine generation, and `math.abs` error reduction.
- Trailing-whitespace scan returned no matches.
- `git diff --check` returned only CRLF normalization warnings on touched C# files.
