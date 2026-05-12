# Status_AUDIO_DSP

Identity: ACOUSTIC_DIRECTOR
Domain: ECHELON 8 Audio DSP / Acoustic Psycho-Acoustics
Source prompt: `Docs/Tasks/CURRENT_BATCH.md`
Status: PENDING VERIFICATION

## Mandates Loaded

- `AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt`
- `AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt`
- `AUDIO_Hrtf_Binaural_Spatialization.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`

## Loop 0 - Setup

- [x] Extract `<AGENT_PROMPT id="AUDIO_DSP">` | DOD: CLI line extraction from `Docs/Tasks/CURRENT_BATCH.md`. Rejected: trusting chat prompt only. Estimate: 50 us.
- [x] Load relevant mandates | DOD: audio/DSP, occlusion, HRTF, zero-GC, native memory, telemetry, fake-first mandates read. Rejected: broad registry ingestion. Estimate: 300 us.
- [x] Create tracking files | DOD: status and rationale persisted before code. Rejected: chat-only reporting. Estimate: 80 us.

## Core Tasks

- [x] Task 1 - POT ENFORCER | DOD: added `AudioBufferCapacity` compile-time Po2 guard plus centralized runtime capacity resolution. Rejected: modulo fallback and inspector-only trust. Estimate: 0 us/frame; cold init only.
- [x] Task 2 - SCALABLE REVERBERATION | DOD: verified Low/Unity profile path uses Sabine RT60 volume/area data and High path selects native convolution cave IR tier. Rejected: always-on convolution. Estimate: PENDING PROFILER; branch/cold parameter path only outside sample loop.
- [x] Task 3 - DYNAMIC OCCLUSION TIERING | DOD: verified Low AUP muffle zone uses -12 dB transmission and High path uses async `RaycastCommand` cache. Rejected: synchronous physics casts from audio path. Estimate: PENDING PROFILER; high-tier raycasts async.
- [x] Task 4 - LINEAR ECHO SAMPLING | DOD: verified `LinearSampleRing` remains 2-tap interpolation with masked indices. Rejected: nearest-neighbor echo read. Estimate: PENDING PROFILER; 2 reads/sample.
- [x] Task 5 - PRECOMPUTED DELAY SAMPLES | DOD: verified sonar taps and Sabine delays store integer `DelaySamples` outside hot loops. Rejected: per-sample seconds-to-samples conversion. Estimate: PENDING PROFILER; removes repeated multiply/cast inside echo loops.
- [x] Task 6 - BLOCK-LEVEL THRUSTER FILTER | DOD: verified `ComputeBandPassCoefficients` is called once before `RenderThrusterBlock` sample loop. Rejected: per-sample biquad coefficient recompute. Estimate: PENDING PROFILER; removes trig/rcp work from each sample.
- [x] Task 7 - DOMINANT-AXIS BINAURAL FAKE | DOD: verified listener/source basis uses `ResolveDominantAxisDirection` axis snapping instead of normalized vectors. Rejected: exact HRTF direction solve. Estimate: PENDING PROFILER; removes normalize/sqrt from source direction path.
- [x] Task 8 - BITWISE WRAP GUARD | DOD: verified ring constants have compile-time Po2 guards and SPSC ring has runtime invariant checks before `& mask`. Rejected: unguarded serialized capacities. Estimate: 0 us/frame steady state.
- [x] Task 9 - PARABOLIC SINE FAKE | DOD: verified `FastSine01` parabolic sine feeds LFO/carrier paths. Rejected: `math.sin` hot-path oscillators. Estimate: PENDING PROFILER; no transcendental sine in these DSP helpers.
- [x] Task 10 - SOFT CLIP APPROXIMATION | DOD: verified `FastSoftClip` rational approximation replaces tanh-style saturation in DSP outputs; no `math.tanh` in audio renderer. Rejected: transcendental tanh. Estimate: PENDING PROFILER; rational math only.
- [x] Task 11 - ZERO-GC AUDIO QUEUE | DOD: verified `NativeQueue<AudioEvent>` capped at 32 and drained in `LateFrameTick` into the authored 32-source pool. Rejected: `AudioSource.PlayClipAtPoint` and per-event allocation. Estimate: PENDING PROFILER; no managed hot allocation in queue drain.
- [x] Task 12 - HULL CREAK GENERATOR | DOD: verified procedural hull stress block drives pressure creaks from depth, hull stress, and structural stress. Rejected: random one-shot creak spam. Estimate: PENDING PROFILER; procedural block already in audio render path.
- [x] Task 13 - DISTANCE LOW-PASS FILTER | DOD: verified distance LOD applies >2000 Hz rolloff through tiered low-pass filters, with Tier1 fixed below 2000 Hz. Rejected: full-band far sources. Estimate: PENDING PROFILER; per-source filter assignment only.
- [x] Task 14 - ADPCM MEMORY OPTIMIZATION | DOD: verified editor importer enforces Ambient/Music Vorbis compressed-in-memory, SFX ADPCM decompress-on-load, and SFX force-to-mono. Rejected: stereo 3D SFX and uncompressed ambient beds. Estimate: import-time only; runtime memory/voice cost reduced.
- [x] Task 15 - STRESS-DRIVEN HEARTBEAT | DOD: verified heartbeat BPM lerps from 54 to 124 BPM by stress/oxygen danger, with player health stress and survival pressure feeding targets. Rejected: static heartbeat loop. Estimate: PENDING PROFILER; scalar envelope synthesis.
- [x] Task 16 - STRUCT PADDING | DOD: verified `AudioEvent` uses `[StructLayout(LayoutKind.Sequential, Size = 32)]` with reserved padding fields. Rejected: implicit CLR layout. Estimate: 0 us/frame.
- [x] Task 17 - LITERAL LOGGING | DOD: replaced scoped editor audio interpolated logs with fixed literals carrying hex error codes; scoped `$"..."` scan is clean. Rejected: asset-path/count interpolation in validation logs. Estimate: editor/import path only.
- [x] Task 18 - REMOVE ONAUDIOFILTERREAD | DOD: scoped runtime scan confirms no `OnAudioFilterRead` fallback in audio renderer or spatial manager. Rejected: managed Unity audio callback fallback. Estimate: avoids managed audio callback path entirely.
- [x] Task 19 - CAST ROUNDING | DOD: scoped runtime scan confirms no `math.round` depth-blend usage in audio renderer or spatial manager; existing conversions use `(int)(value + 0.5f)`. Rejected: `math.round` in DSP/depth blend path. Estimate: PENDING PROFILER; scalar cast path only.
- [x] Task 20 - OMEGA COMPILE CHECK | DOD: verified DSP job `PlayerCriticalBufferJobs.DopplerShiftBatchJob` is `[BurstCompile(FloatPrecision = FloatPrecision.Standard, FloatMode = FloatMode.Fast)]`; runtime and editor no-dependency builds succeed. Rejected: unmanaged job without fast Burst mode. Estimate: compile/config only.

## Verification

- [x] Loop 1 compile after tasks 1-5 | `dotnet build Assembly-CSharp.csproj --no-dependencies -v:minimal` succeeded with 0 warnings/errors. Full dependency build blocked by pre-existing `VoxelDeltaProcessor.cs` errors outside AUDIO_DSP domain.
- [x] Loop 2 compile after tasks 6-10 | `dotnet build Assembly-CSharp.csproj --no-dependencies -v:minimal` succeeded with 0 warnings/errors.
- [x] Loop 3 compile after tasks 11-15 | `dotnet build Assembly-CSharp.csproj --no-dependencies -v:minimal` succeeded with 0 warnings/errors.
- [x] Loop 4 compile after tasks 16-20 | `dotnet build Assembly-CSharp.csproj --no-dependencies -v:minimal`, `dotnet build Assembly-CSharp-Editor-firstpass.csproj --no-dependencies -v:minimal`, and `dotnet build Assembly-CSharp-Editor.csproj --no-dependencies -v:minimal` succeeded with 0 warnings/errors.
- [x] Loop 5 strict self-review and polish mandate | OMEGA audit completed. Scoped scans clean for `foreach`, `$"..."`, `string.Format`, `.ToString()`, `math.sqrt`, and `math.normalize` in touched audio/editor files. `Hecton8.Core.csproj` remains blocked by non-audio Core/Fauna/Construction compile errors, so top status stays PENDING VERIFICATION.
