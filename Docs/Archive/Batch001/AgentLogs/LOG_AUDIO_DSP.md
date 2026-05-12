# LOG_AUDIO_DSP

## 2026-05-11 - AUDIO_DSP Ring Buffer / DSP Audit

What was wrong:
- The audio SPSC ring accepted serialized/runtime capacities without one central hard Po2 contract, while mask wrapping depends on `capacity - 1`.
- Editor audio validation logs still used interpolated strings.
- Full project compile health is currently blocked outside audio.

What was done:
- Added `AudioBufferCapacity = 65536` with compile-time Po2 guard in `NativeAudioFrameRingBuffer`.
- Centralized runtime capacity rounding/assertion through `ResolvePowerOfTwoCapacity`.
- Guarded ring state reads/writes against invalid capacity/mask invariants.
- Routed `PlayerCriticalProceduralAudioRenderer` authoring validation through the same Po2 resolver.
- Replaced scoped `HectonAudioPostprocessor` interpolated logs with fixed literal hex-code logs.
- Verified all 20 AUDIO_DSP tasks in `Docs/Tasks/Status_AUDIO_DSP.md`.

Cinematic cheats used:
- Po2 `& mask` wrapping instead of modulo.
- Parabolic `FastSine01` instead of hot transcendental sine.
- `FastSoftClip` rational saturation instead of `math.tanh`.
- Dominant-axis binaural direction instead of exact normalized HRTF direction.
- Sabine/Unity low-tier reverb and high-tier cave convolution path gating.

Exact microseconds saved:
- Measured exact microseconds: PENDING VERIFICATION. No Unity Profiler capture was available in this terminal pass.
- Static estimate only: 0 us/frame added by Po2 validation after initialization; editor log changes are import/editor-only; per-sample savings come from already-verified no-modulo/no-transcendental DSP paths.

Verification:
- `dotnet build Assembly-CSharp.csproj --no-dependencies -v:minimal` passed with 0 warnings/errors.
- `dotnet build Assembly-CSharp-Editor-firstpass.csproj --no-dependencies -v:minimal` passed with 0 warnings/errors.
- `dotnet build Assembly-CSharp-Editor.csproj --no-dependencies -v:minimal` passed with 0 warnings/errors.
- `dotnet build Hecton8.Core.csproj -v:minimal` failed outside AUDIO_DSP: missing `AnomalySignal`, `AcousticPingSignal`, `HypoxiaSignal`, `ScanCompleteSignal`, `FaunaTier1LodProxyEntry`, and `ConstructionManager` missing `IOriginShiftListener.OnOriginShift`; also one duplicate using warning in `ConstructionManager.cs`.

Final scoped diff:
- `Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs`
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`
- `Assets/_Project/Scripts/Editor/HectonAudioPostprocessor.cs`
- Working tree stat for scoped code diff: 3 files changed, 341 insertions, 38 deletions.

Status:
- PENDING VERIFICATION because the required full Core build is blocked outside the AUDIO_DSP domain.
