# 1846 Dynamic Music Transfer Bridge Guard

Date: 2026-06-04 07:39 +04

## Scope

Added source-level guard coverage for `DynamicMusicGranularSynthesizer.OnAudioFilterRead` so the remaining managed callback stays a transfer bridge only.

## Source Changes

- `Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs`
  - Added assertions for `DynamicMusicGranularSynthesizer.OnAudioFilterRead`.
  - The audit now requires the callback to:
    - read only the published audio-thread copy buffer via `TryResolvePublishedAudioThreadCopyBuffer`;
    - copy prebuilt interleaved samples with `UnsafeUtility.MemCpy`;
    - rely on `PublishAudioThreadCopyBufferLateFrame()` for the dedicated audio-thread copy buffer.
  - The audit rejects callback use of:
    - `TryAcquire`;
    - `ScheduleSynthJobs`;
    - `GranularSynthesisJob`;
    - `Stopwatch`;
    - `AudioSettings`.

- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
  - Clarified current owner status:
    - critical renderer has no managed callback;
    - dynamic music is a transitional transfer bridge;
    - vocal release callback is fail-closed until native/DSPGraph output exists.
  - Clarified that managed callbacks may only copy from a prebuilt ring/copy buffer or fail-close to silence.

## Verification

- `git diff --check -- Assets/_Project/Scripts/Audio/Editor/AdvancedAcousticsSmokeTester.cs Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
  - Passed.
  - Git reported LF-to-CRLF working-copy warnings only.
- Focused source scan confirmed new dynamic music guard text and updated DSP architecture text.
- Unity/process check still shows active Unity editor, `Unity.ILPP.Runner`, and multiple `UnityShaderCompiler` processes.
  - Did not run Unity menu audits, profiler, build, or Play Mode while compilation/shader work is active.

## Remaining Blocker

This is not runtime DSP acceptance. Dynamic music still needs profiler/GC/underrun proof on compact hardware, and preferably native output once the project has a stable route for it.
