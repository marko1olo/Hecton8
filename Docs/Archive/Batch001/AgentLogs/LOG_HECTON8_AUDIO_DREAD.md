# LOG_HECTON8_AUDIO_DREAD

## 2026-05-11 - Granular Audio & Acoustic Dread Pass

Status: PENDING VERIFICATION

What was wrong:
- Static loop-style hull creaks did not satisfy the requested procedural granular behavior.
- Panic audio artifacts were only partially source-backed by heartbeat dulling and lacked deterministic master jitter.
- Fauna Doppler batching had no dedicated Burst job surface.
- Sabine reverb had equation-based runtime resolution but no requested precomputed depth/volume 1D LUT.
- Several requested cues depend on missing content or cross-domain contracts: biolum coral emitters, flood-percent procedural ingress, seismic prewarning, VWS duck trigger, airlock procedural hiss, and HUD heartbeat bridge.

What was done:
- `PlayerCriticalProceduralAudioRenderer.StartStructuralGranularLoop` now selects exact 50 ms grains from a two-second metallic source window, with AUP-safe hash/LCG pitch jitter and power-of-two bank masking.
- `PlayerCriticalProceduralAudioRenderer.ApplyPanicGranularMasterJitter` adds deterministic held jitter/noise when heartbeat/player stress crosses the panic scalar.
- `PlayerCriticalProceduralAudioRenderer.RenderPressureScrubberHumSample` now pitches the procedural scrubber drone up as oxygen danger rises.
- `PlayerCriticalBufferJobs.DopplerShiftBatchJob` adds a Burst `IJobParallelFor` batch path for `SourceFreq * (SpeedOfSound / (SpeedOfSound + RelativeVelocity))`.
- `SpatialAudioManager` now owns a cold `float[64]` Sabine RT60 LUT, sampled by depth and module volume and blended with the existing Sabine equation.
- `Docs/Tasks/Status_HECTON8_AUDIO_DREAD.md` records all 30 tasks as done/source-backed or blocked with specific dependency reasons.
- `Docs/AgentLogs/Rationale_HECTON8_AUDIO_DREAD.md` records the technical decisions and rejected alternatives.

Cinematic cheats used:
- Fake-first metallic groan bank instead of runtime WAV slicing.
- Deterministic held noise and rational `FastSoftClip` instead of plugin saturation or `math.tanh`.
- 1D Sabine profile LUT instead of simulating acoustic rays/reverberant fields.
- Cached occlusion and power-of-two delay rings instead of expensive dynamic acoustic simulation.

Exact microseconds saved:
- Exact measured savings: 0 us claimed. No Unity profiler, Burst Inspector, or audio runtime capture was available in active tools.
- Estimate, pending profiler: hull grain selection <8 us per trigger; panic jitter <2 us per 512-frame block; Doppler 500 lanes <20 us; Sabine LUT sample <1 us per cave-acoustics update.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`
- Result after final code changes: build succeeded, 0 warnings, 0 errors.
- Unity Editor compile, PlayMode, audio capture, and profiler verification remain pending.
