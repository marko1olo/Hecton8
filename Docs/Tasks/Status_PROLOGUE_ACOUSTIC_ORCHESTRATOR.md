# Status_PROLOGUE_ACOUSTIC_ORCHESTRATOR

Prompt: PROLOGUE_ACOUSTIC_ORCHESTRATOR
Role: DSP_ACOUSTIC_LEAD
Domain: Audio / Prologue DSP
Status: PENDING VERIFICATION

## Selected Mandates
- READ: AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt | Constraint: DSP params via lock-free queue / double-buffer snapshots; no managed audio callbacks.
- READ: AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt | Constraint: LPF cutoff clamps 80..22000Hz; underwater audio defaults to cheap perceptual filters.
- READ: AUDIO_Hrtf_Binaural_Spatialization.txt | Constraint: MX350 uses cheap filtered ambience; full HRTF is high/ultra only.
- READ: ARCH_Global_Registry_ServiceLocator_DI_Init.txt | Constraint: extend interfaces, cache services outside hot paths, no singleton invention.
- READ: OPT_Zero_GC_Policy_AllocFree_Mandate.txt | Constraint: 0 B in Tick/hot paths; no LINQ, coroutines, hot strings, or heap allocations.
- READ: OPT_Native_Memory_Collections_JobSystem_Protocol.txt | Constraint: persistent native buffers have one owner; no mid-frame Complete; Burst jobs unmanaged only.
- READ: DBG_Telemetry_Crash_Reporting_PostMortem.txt | Constraint: 300-frame fixed telemetry ring and dump path for critical system state.
- READ: MATH_Coordinate_Precision_AUP_FloatingOrigin.txt | Constraint: re-entry seam must not depend on Transform/world shift state.

## Tasks
- [ ] 1. Extend IAudioService for prologue DSP control | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 2. Consume PrologueStageSignal | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 3. ASMDEF isolation: Hecton8.Audio.Prologue -> Contracts | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 4. Vacuum LPF 400Hz + LFE bone vibration | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 5. Granular plasma from UniverseVelocity | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 6. Splashdown 100ms 40Hz sine sweep on PrologueCompleteSignal | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 7. 3s filter sweep from 400Hz to 22000Hz | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 8. Enable AcousticPortalPropagation handoff | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 9. Route transition audio through lock-free NativeQueue | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 10. Keep re-entry AUP-independent | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 11. Math LOD: Low tier disables granular plasma | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 12. Update audio parameters in VISUAL_SYNC | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 13. Zero-GC interpolation and oscillator injection | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 14. Push AudioTransitionState to telemetry | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us
- [ ] 15. Verify Burst compilation of sine sweep | Justification: PENDING | Alternatives Rejected: PENDING | Estimate: PENDING us

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md using CLI. Status and rationale files were missing; created fresh batch memory.
