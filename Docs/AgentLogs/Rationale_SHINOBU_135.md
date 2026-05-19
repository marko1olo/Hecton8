# SHINOBU_135 Rationale

Status: PENDING VERIFICATION

## Initial Architecture Decision

Problem: Legacy adaptive music usually crossfades long AudioSource clips and manipulates AudioMixer string parameters. That path is memory-heavy, string-bound, and not reactive at DSP cadence.

Solution: Build an owner-local procedural audio presentation system with explicit 64-byte `SynthVoiceDTO`, Burst jobs for sample/grain generation, scalar-only input snapshots, depth low-pass filtering, continuous polyphony scaling from `GlobalQualityWeight`, and a 300-frame DSP telemetry ring.

Rejected Alternatives: Static WAV stems and AudioMixer string parameters were rejected because they keep memory/I/O pressure and string hash lookup in the control path. Direct world/AUP queries in audio DSP were rejected because the audio thread must consume scalar snapshots only.

Scalability potential: Low uses 16 active grains, sparser density, LPF-heavy pressure feel. Middle raises grain density and stereo width. High increases detune/LFO detail and overlap. Ultra reaches 128 voices and richer procedural shimmer while staying presentation-only.

Hardware Impact: On i3/MX350, replacing static music crossfades with procedural grains targets lower resident audio memory and avoids disk streaming stalls. Static estimate before profiling: 0.3-1.5 ms per 512-sample synth block depending on active voices, with 16-voice low tier expected below 500 us/block. Exact proof remains PENDING VERIFICATION.

## Mandate Binding

Problem: Audio synthesis touches hot paths, native memory, ARM64 layout, AUP context, dispatcher phase, and blackbox telemetry.

Solution: Apply these mandates before coding: AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, ARCH_Execution_Phases, ARCH_Global_Registry_ServiceLocator_DI_Init, MATH_AUP_Determinism_Sync, DBG_Telemetry_Crash_Reporting_PostMortem, OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.

Rejected Alternatives: Local unmanaged allocation without ownership records was rejected. Global registry polling in hot paths was rejected. Presentation music state entering gameplay rollback/hash state was rejected.

Scalability potential: The synth spends performance on perception, not simulation truth; continuous quality controls voice count, density, LFO depth, and filter detail instead of binary tiers.

Hardware Impact: Reduced managed allocation target is 0 B/call in audio and tick hot paths. Static target is less than 0.1 ms main-thread scheduling overhead and less than 1.5 ms DSP block time before telemetry dump.
