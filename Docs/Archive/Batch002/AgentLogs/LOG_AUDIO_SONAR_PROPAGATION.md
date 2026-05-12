# LOG_AUDIO_SONAR_PROPAGATION

## 2026-05-11 - SDF Echo Implementation

Identity: SONAR_TECHNICIAN  
Prompt ID: AUDIO_SONAR_PROPAGATION  
Domain: Audio/Sonar Propagation  
Status: PENDING VERIFICATION  
Task Count: 15

### What Was Wrong

Active sonar did not have a terrain-derived echo path. The existing ping path generated synthetic tap behavior but did not query published voxel SDF distance, did not expose a voxel material atlas to audio, and had no visual-only return signal aligned to the DSP echo delay. A clip/source-based approach would violate the prompt, add managed scheduling risk, and fail the zero-GC audio-thread mandate.

### What Was Done

- Implemented SDF echo tap generation in `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`.
- `BuildSdfSonarEchoTaps` queries `HectonVoxelVolume.TryRaymarchAnyPublishedSdf` at fixed intervals, defaulting to 50 meters, and does not use Unity physics raycasts.
- Echo delay uses the required formula: distance multiplied by reciprocal speed of sound in water.
- Echo taps are written to the inactive `NativeArray<SonarEchoTap>` buffer and published through the existing double-buffered DSP bridge.
- Existing DSP render path reads `_workerSonarEchoTaps`, samples `_sonarEchoDelay`, applies low-pass/filter shaping, and feeds the master sonar bus through `FastSoftClip`.
- Added voxel-side sonar material sampling in `Assets/_Project/Scripts/HectonVoxelVolume.cs` through a published byte atlas and `TrySamplePublishedSonarAudioMaterialId`.
- Material response now supports rock/metal/glass style pitch and filter mapping without coupling audio to voxel storage internals.
- Added `PingReturnSignal` in `Assets/_Project/Scripts/Visor/SpectrumSystem.cs`, using fixed NativeQueue event lanes and listener buckets.
- `SpectrumSystem` consumes `PingReturnSignal` and schedules a visor echo blip using the same delay value as DSP audio.
- Added predator ping-back path through `WorldSpatialHashGrid.TryGetNearestAggressiveBioform`, constrained to aggressive Leviathan profiles and emitted as `AcousticImpulseEvent` bio-echo.
- Added depth muffling as a scalar cinematic cheat reducing echo amplitude and low-pass cutoff by ambient pressure/depth.
- Added math LOD: Low/MX350/Unknown = 4 probes, Mid = 8 probes, High/Ultra = 16 probes.
- Confirmed no `AudioSource.PlayOneShot` calls in `Assets/_Project/Scripts/Audio/`.
- Wrote recon report: `Docs/AgentLogs/RECON_AUDIO_SONAR_PROPAGATION.md`.
- Wrote task checklist: `Docs/Tasks/Status_AUDIO_SONAR_PROPAGATION.md`.
- Wrote rationale journal: `Docs/AgentLogs/Rationale_AUDIO_SONAR_PROPAGATION.md`.

### Cinematic Cheats Used

- SDF raymarch echo approximation instead of physical acoustic propagation.
- Fixed cardinal/diagonal probe sets instead of dense rays or wave simulation.
- One-byte voxel sonar material atlas instead of mesh/material resolver traversal.
- Depth muffling scalar instead of frequency-dependent pressure simulation.
- FastSoftClip sonar bus instead of Unity mixer limiter or per-source automation.
- Visual return blips are `PingReturnSignal` shader timing, not simulated acoustic particles.

### Exact Microseconds Saved

Estimates remain PENDING VERIFICATION because global compile and Burst verification are blocked by unrelated project errors.

- SDF probe path versus Unity physics raycasts: estimated 45-140 us saved per ping on i3/MX350.
- DSP delay taps versus clip scheduling / `AudioSource.PlayOneShot`: estimated 80-250 us saved per ping plus avoided managed churn.
- Low-tier 4-probe LOD versus 16-probe high tier: estimated 30-100 us saved per ping.
- Precomputed Doppler/material tap data versus per-sample Unity object reads: estimated 8-24 us saved per 1024-frame block at 16 taps.
- Visual fixed NativeQueue return signal versus managed event fanout: estimated 3-8 us saved per ping.
- Depth scalar cheat versus per-band propagation: estimated sub-3 us per 16-hit ping.
- OMEGA reciprocal polish in ping-time paths: estimated 1-4 us saved per 16-hit ping.

No verified Burst timing claim is made.

### Evidence

- SDF echo producer: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`, `BuildSdfSonarEchoTaps`.
- Voxel material query: `Assets/_Project/Scripts/HectonVoxelVolume.cs`, `TrySamplePublishedSonarAudioMaterialId`.
- DSP echo delay mix: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`, `RenderSonarBlock`.
- Soft clip bus: `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs`, `FastSoftClip` use in sonar render.
- Visual return event: `Assets/_Project/Scripts/Visor/SpectrumSystem.cs`, `PingReturnSignal` and `HandlePingReturnSignal`.

### Verification

- Unity console filters for touched sonar files reported zero errors:
  - `PlayerCriticalProceduralAudioRenderer`
  - `HectonVoxelVolume`
  - `SpectrumSystem`
  - `PingReturnSignal`
- `rg "AudioSource.PlayOneShot" Assets/_Project/Scripts/Audio -g "*.cs"` returned no matches.
- Hot-path audit over touched sonar implementation found no `foreach`, `math.sqrt`, `math.normalize`, `Vector3.Normalize`, `string.Format`, or `.ToString(`.
- `git diff --check` passed except line-ending warnings.
- Full project compile is blocked by unrelated errors in `ProceduralCrabLegIKRuntime` and missing generated/platform symbols from core dependencies.
- Burst compilation of the Echo Delay DSP job is therefore BLOCKED BY DEPENDENCY. No fake verified status was recorded.

### Final State

All 15 prompt tasks are closed in `Status_AUDIO_SONAR_PROPAGATION.md`; task 15 is explicitly marked dependency-blocked. Project status remains `PENDING VERIFICATION` until unrelated compile blockers are cleared and Burst verification can run.
