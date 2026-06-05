# 1845 Vocal Managed Callback Release Guard

Date: 2026-06-04 07:36 +04

## Scope

Closed the release-player risk where `VocalBankPlaybackRuntime.OnAudioFilterRead` decoded vocal-bank audio, locked DataVault views, wrote telemetry, and used `Stopwatch` on Unity's managed audio callback thread.

## Source Changes

- `Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs`
  - Added a non-editor, non-development `OnAudioFilterRead` body.
  - Release-player callback now zero-fills the Unity-provided buffer and returns.
  - Release-player callback does not:
    - acquire DataVault views or mutation guards;
    - call `VocalDecodeKernel.DecodeIntoAudioBuffer`;
    - write telemetry/counters;
    - call `Stopwatch`;
    - touch the vocal bank.
  - Existing legacy decode callback remains available only for Editor/Development builds.

- `Assets/_Project/Scripts/Audio/Editor/VocalWarningAlarmBitmaskAudit_1629.cs`
  - Added source assertions that the release callback body has no vocal decode, DataVault lock path, or `Stopwatch` timing.
  - Added assertion that the release fail-closed preprocessor guard exists.

- `Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`
  - Updated route and status.
  - Removed the old implication that `OnAudioFilterRead` is an acceptable production route.
  - Documented that release vocal playback requires a native/DSPGraph or native audio-kernel bridge.

## Evidence

- `audio.md` rejects release synthesis/decode/lock/Stopwatch inside managed `OnAudioFilterRead`.
- `.agents-skills/AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt` and `AUDIO_Hrtf_Binaural_Spatialization.txt` reject managed audio callbacks as production DSP routes.
- Existing critical audio renderer already has a native bridge path; vocal playback does not yet have an equivalent output route.

## Verification

- `git diff --check -- Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs Assets/_Project/Scripts/Audio/Editor/VocalWarningAlarmBitmaskAudit_1629.cs Docs/ARCHITECTURE/VOCAL_SYNTHESIS_PIPELINE_SHINOBU_260.md`
  - Passed.
  - Git reported LF-to-CRLF working-copy warnings only.
- Focused source scan confirms:
  - release guard exists;
  - audit checks release callback for decode/lock/Stopwatch absence;
  - legacy decode path remains only behind Editor/Development branch.
- Unity/process check still shows active Unity editor, `Unity.ILPP.Runner`, and multiple `UnityShaderCompiler` processes.
  - Did not run Unity menu audits, profiler, build, or Play Mode while compilation/shader work is active.

## Remaining Blocker

Release vocal playback is intentionally silent until the native/DSPGraph output route exists. Next audio work should move vocal bank decode into a producer/native route feeding a preallocated ring/output job, then remove the managed callback dependency instead of re-enabling release callback decode.
