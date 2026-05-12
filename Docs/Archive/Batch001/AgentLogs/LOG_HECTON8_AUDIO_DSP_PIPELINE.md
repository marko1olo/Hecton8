# LOG_HECTON8_AUDIO_DSP_PIPELINE

## 2026-05-11 Acoustic DSP Polish Pass

What was wrong:
- Scalable reverb high tier was still a four-comb Sabine-style tail, not the requested convolution-style cave coloration.
- Dynamic occlusion low tier was correctly fake-only, but high tier had no async one-ray path.
- The occlusion cache capacity was non-Po2 and still wrapped with modulo.

What was done:
- Added `ReverbDspTier.NativeConvolution` for High/Ultra.
- Added fixed 32-tap cave convolution IR and 128-sample Po2 delay ring in `PlayerCriticalProceduralAudioRenderer`.
- Added acoustic-density sampling from `WorldSpatialHashGrid.TryGetAcousticDensityMap` into the audio parameter snapshot.
- Added high-tier `RaycastCommand.ScheduleBatch` occlusion queue in `AcousticOcclusionUtility`, completed in `LateFrameTick`, with cached result reuse.
- Kept Low/Mx350/Mid occlusion on deterministic distance/flora cinematic muffles.
- Wired `SpatialAudioManager.LateFrameTick` to advance the async occlusion batch before draining `NativeQueue<AudioEvent>`.
- Raised occlusion cache to 64 entries and replaced `% MaxQueuedRequests` with `& MaxQueuedRequestsMask`.
- Fixed external build wall in `ScannableFragment.cs` by adding missing `using System;` for the existing span hash call.

Cinematic cheats used:
- Full cave acoustics -> 32-tap pre-baked cave IR + density scalar.
- True obstacle thickness -> one async raycast + collider AABB projected-thickness proxy.
- Low-tier physical occlusion -> distance/flora muffle envelope.
- Integer modulo wrap -> Po2 masked wrap.

Estimated microseconds saved:
- Low-tier reverb profile instead of native convolution: 10-70 us per DSP block.
- 32-tap convolution instead of long IR convolution: 250-600 us per DSP block on High/Ultra.
- One async ray + AABB proxy instead of multi-hit/chained occlusion: 12-30 us per source query.
- Bitwise cache wrap instead of modulo: about 0.02 us per cache write.
- Preserved existing linear sonar sampling instead of Hermite: 0.04-0.08 us per tap/sample.

Verification:
- Static scan found no `Physics.Raycast`, `RaycastNonAlloc`, `HermiteSampleRing`, `OnAudioFilterRead`, `math.floor`, `math.round`, `math.sin`, `math.sqrt`, or `$"` in touched audio/acoustic hot files.
- `dotnet restore Hecton8.Core.csproj --verbosity:minimal` regenerated missing assets.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nodeReuse:false` passed: 0 warnings, 0 errors.
- Runtime Unity Editor, Play Mode, Profiler, GCMonitor, AudioMixer behavior, and imported clip settings remain unverified.

Final git diff:
- `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`: high-tier native convolution branch, cave IR buffers, density snapshot field, native buffer registration/disposal.
- `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs`: high-tier async RaycastCommand queue, AABB thickness proxy, Po2 cache mask.
- `Assets/_Project/Scripts/SpatialAudioManager.cs`: acoustic occlusion runtime lifetime and late-frame pump.
- `Assets/_Project/Scripts/Gameplay/ScannableFragment.cs`: missing `using System;` build fix.
- Full diff is present in the working tree via `git diff -- Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs Assets/_Project/Scripts/SpatialAudioManager.cs`.

STATUS: PENDING VERIFICATION
