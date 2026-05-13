# Status_KINETIC_IMPACT_ACOUSTICS

Status: PENDING VERIFICATION
Agent: DSP_ACOUSTIC_LEAD
Prompt: KINETIC_IMPACT_ACOUSTICS
Domain: ECHELON 8 PRESENTATION & UX / DSP AUDIO
Task Count: 17

## Mandates Identified Before Coding
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt
- AUDIO_Hrtf_Binaural_Spatialization.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt

## Loop 1: Tasks 1-5
- [ ] 1. SINGLETON ERADICATION: extend `IAudioService` without new singleton dependency.
- [ ] 2. SIGNAL MIGRATION: consume `HighSpeedImpactSignal(Velocity, Mass, MaterialHash)`.
- [ ] 3. ASMDEF ISOLATION: route `Hecton8.Audio.Synthesis` through contracts.
- [ ] 4. DEAD CODE HUNT: remove impact `AudioSource.PlayClipAtPoint` usage.
- [ ] 5. ENERGY CALCULATION: `0.5 * Mass * lengthsq(Velocity)`.
- [ ] Compile checkpoint after tasks 1-5.

## Loop 2: Tasks 6-10
- [ ] 6. PROCEDURAL THUD: metal pitch-descending sine 150Hz to 40Hz over 0.2s.
- [ ] 7. DISTORTION FOLD: hard clip extreme impacts.
- [ ] 8. BINAURAL ROUTING: push generated impact data into echo/portal propagation lane.
- [ ] 9. WATER MUFFLE: apply 800Hz low-pass for underwater impacts.
- [ ] 10. AUP SHIFT SAFETY: resolve impact AUP at event time.
- [ ] Compile checkpoint after tasks 6-10.

## Loop 3: Tasks 11-15
- [ ] 11. MATH LOD: low tier pre-baked fallback with volume scaling.
- [ ] 12. EXECUTION PHASE: DSP path remains async / no hot main-thread blocking.
- [ ] 13. ZERO-GC: PCM/DSP generation uses preallocated or unmanaged buffers only.
- [ ] 14. BLACKBOX DUMP: push `PeakImpactEnergy` telemetry.
- [ ] 15. OMEGA COMPILE CHECK: verify Burst sine oscillator compile path.
- [ ] Compile checkpoint after tasks 11-15.

## Loop 4: Recursive Re-Verification
- [ ] 16. Re-read prompt and re-check every task against actual code.
- [ ] 17. Clamp kinetic energy against speaker blowout / infinite energy.
- [ ] Compile checkpoint after recursive verification.

## Loop 5: Omega Polish
- [ ] Read `<POLISH_MANDATE>` only after all core tasks are done or blocked.
- [ ] Execute final anti-bloat pass on owned code only.
- [ ] Append final report to `Docs/AgentLogs/LOG_KINETIC_IMPACT_ACOUSTICS.md`.
