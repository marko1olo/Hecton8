# Status_HECTON8_AUDIO_DSP_PIPELINE

Mandates followed:
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation
- AUDIO_Hrtf_Binaural_Spatialization
- OPT_Zero_GC_Policy_AllocFree_Mandate
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First
- OPT_Native_Memory_Collections_JobSystem_Protocol

## Adaptive Acoustics Checklist

- [x] 1. Power-of-two ring guard | DOD: existing native ring buffer throws on non-Po2 capacity and DSP rings use mask wrapping. Alternative rejected: modulo wrap in DSP because slower and less deterministic. Estimate: 0.1-0.4 us/block kept.
- [x] 2. AudioLogDiscoveryBitMask build reference | DOD: `dotnet build Hecton8.Core.csproj --no-restore` passes with 0 errors. Alternative rejected: guessing stale namespace issue. Estimate: build blocker removed, runtime estimate not applicable.
- [x] 3. Scalable reverb low tier | DOD: Low/Mx350/Mid remain `UnityProfileOnly`, driven by Sabine-derived RT60/mixer profile. Alternative rejected: native convolution on low hardware. Estimate: 10-70 us/block saved on low.
- [x] 4. Scalable reverb high tier | DOD: High/Ultra select `NativeConvolution`, 32-tap pre-baked cave IR, density scalar from `WorldSpatialHashGrid.TryGetAcousticDensityMap`. Alternative rejected: 2-second full convolution. Estimate: 250-600 us/block saved versus full IR.
- [x] 5. Dynamic occlusion low tier | DOD: distance/flora cinematic muffle remains default; no physics query on low path. Alternative rejected: synchronous raycasts. Estimate: 15-80 us/source saved.
- [x] 6. Dynamic occlusion high tier | DOD: high tier queues one `RaycastCommand` per uncached source path, completes on later `LateFrameTick`, applies projected collider thickness. Alternative rejected: multi-hit synchronous thickness chain. Estimate: 12-30 us/source saved versus chained queries.
- [x] 7. Linear echo sampling | DOD: `LinearSampleRing` remains 2-tap and read cursors are float. Alternative rejected: Hermite 4-tap. Estimate: 0.04-0.08 us/tap/sample saved.
- [x] 8. Precomputed delay samples | DOD: `SonarEchoTap.DelaySamples` persists integer delay outside sample loop. Alternative rejected: per-sample rounding. Estimate: 0.02 us/tap/sample saved.
- [x] 9. Block-level thruster filter | DOD: filter coefficients resolved per block, not inside sample loop. Alternative rejected: per-sample coefficient rebuild. Estimate: 10-25 us/block saved.
- [x] 10. Dominant-axis binaural fake | DOD: existing binaural/telemetry path uses cheap dot/axis approximations and 64-byte telemetry structs. Alternative rejected: full HRTF truth on MX350. Estimate: 4-15 us/source saved.
- [x] 11. Bitwise wrap guard | DOD: ring buffers have Po2 compile/runtime guards; new occlusion cache write index uses `& MaxQueuedRequestsMask`. Alternative rejected: `%` in cache rotation. Estimate: 0.02 us/cache write saved.
- [x] 12. Parabolic sine fake | DOD: scans show no `math.sin` in touched hot audio/acoustic files. Alternative rejected: trig in sample loop. Estimate: 3-10 us/block preserved.
- [x] 13. Fast soft clip | DOD: scans show no `math.tanh` in touched hot audio/acoustic files; limiter uses rational approximation. Alternative rejected: tanh saturation. Estimate: 2-8 us/block preserved.
- [x] 14. Zero-GC AudioEvent queue | DOD: `NativeQueue<AudioEvent>` drained in `SpatialAudioManager.LateFrameTick` before pooled AudioSource route. Alternative rejected: managed event list/play-one-shot hot path. Estimate: allocation risk removed.
- [x] 15. Hull creak generator | DOD: pressure/structural creak synthesis is present in player critical renderer. Alternative rejected: physics-driven hull strain simulation. Estimate: 20-100 us/frame saved.
- [x] 16. Distance low-pass filter | DOD: distance/rear/cinematic muffle curves use mixer/source low-pass scalars and reciprocal math. Alternative rejected: occlusion ray density per source on low. Estimate: 5-20 us/source saved.
- [x] 17. ADPCM import policy | DOD: editor audio postprocessor has SFX ADPCM + decompress-on-load policy; Unity reimport not executed in this session. Alternative rejected: runtime transcoding. Estimate: memory/runtime validation pending.
- [x] 18. Stress heartbeat | DOD: heartbeat stress is carried in audio parameter snapshot and modulates renderer state. Alternative rejected: clip timeline heartbeat. Estimate: GC risk removed.
- [x] 19. Acoustic ping event | DOD: sonar/acoustic events still flow through Spectrum/Native queues; no string event IDs introduced. Alternative rejected: direct cross-system calls. Estimate: decoupling preserved.
- [x] 20. AudioEvent 32-byte alignment | DOD: `AudioEvent` struct is `[StructLayout(LayoutKind.Sequential, Size = 32)]`. Alternative rejected: managed class event payload. Estimate: one cache-half-line event.
- [x] 21. Sonar/binaural 64-byte alignment | DOD: sonar tap and spatial telemetry structs are padded to 64 bytes. Alternative rejected: unpadded mixed structs. Estimate: cache miss risk reduced.
- [x] 22. Fixed diagnostic logs | DOD: touched file scan found no `$"` interpolation in audio/acoustic hot files; existing logs are fixed literals/editor guarded where applicable. Alternative rejected: runtime formatted strings. Estimate: 0 B/frame target preserved.
- [x] 23. SFX routing | DOD: existing `SpatialAudioManager` pool routes through configured mixer groups. Alternative rejected: fallback unmanaged AudioSource routing outside SFX group. Estimate: routing correctness pending Unity scene check.
- [x] 24. Managed audio callback removal | DOD: scan found no `OnAudioFilterRead` in touched audio/acoustic files. Alternative rejected: managed fallback. Estimate: audio-thread GC/stall risk removed.
- [x] 25. Burst fast mode | DOD: DSP jobs in renderer carry `[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Alternative rejected: default precision in hot DSP jobs. Estimate: SIMD/codegen risk reduced.

## Polish Checklist

- [x] Phase 1 Math LOD audit | High-only raycast/convolution; low path remains cinematic fake.
- [x] Phase 2 Frame-time dictatorship | No new per-frame managed allocation; high physics runs async and cached.
- [x] Phase 3 Zero-GC purge | Scans: no `math.floor`, `math.round`, `math.sin`, `math.sqrt`, `HermiteSampleRing`, `OnAudioFilterRead`, `$"` in touched hot files.
- [x] Phase 4 Cache locality | New buffers are fixed `NativeArray`/flat arrays; 32-tap IR and 128-ring stay cache-resident.
- [x] Phase 5 Build fix | Restored missing `project.assets.json`, fixed external `ScannableFragment.cs` missing `using System;`, then `dotnet build Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nodeReuse:false` passed with 0 warnings and 0 errors.

STATUS: PENDING VERIFICATION
