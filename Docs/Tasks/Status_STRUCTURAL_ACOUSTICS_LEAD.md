# STRUCTURAL_ACOUSTICS_LEAD Status

Agent: DSP_ACOUSTIC_LEAD  
Prompt ID: STRUCTURAL_ACOUSTICS_LEAD  
Status: PENDING VERIFICATION  
Batch source: Docs/Tasks/CURRENT_BATCH.md  
Domain: ECHELON 8 / Audio Synthesis with Habitat stress interface boundary

## Relevant Mandates

- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- AUDIO_Hrtf_Binaural_Spatialization.txt
- CORE_Damage_System_Hull_Integrity_VFX_Feedback.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- CTRL_Device_Abstraction_Haptics.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt

## Checklist

- [ ] 1. Extend IAudioService / singleton eradication N/A | DOD: interface expansion only, no new singleton | Rejected: concrete cross-domain dependency | Estimate: 4 us call path
- [ ] 2. Consume HullStressSignal(PressureDelta) | DOD: typed signal or adapter with fixed ring input | Rejected: per-frame service polling | Estimate: 8 us enqueue
- [ ] 3. ASMDEF isolation Hecton8.Audio.Synthesis -> Contracts | DOD: audio synthesis assembly references contracts only | Rejected: dumping into Core | Estimate: 0 us runtime
- [ ] 4. Dead code hunt: AudioSource creaking on base modules | DOD: source/prefab scan and report/removal if first-party creak-only | Rejected: blanket vendor delete | Estimate: 0 us runtime
- [ ] 5. Grain buffer NativeArray PCM | DOD: single buffer path with cold allocation and fallback procedural seed | Rejected: 50 clips | Estimate: 0 us hot path allocation
- [ ] 6. DSP kernel granular synthesizer | DOD: fixed grain ring, 10-50ms snippets, Burst fast math | Rejected: managed OnAudioFilterRead | Estimate: <80 us/block
- [ ] 7. Pressure derivative | DOD: current-prev pressure snapshot, finite guards | Rejected: health bar polling | Estimate: 3 us update
- [ ] 8. Grain spawning by pressure/depth | DOD: deterministic spawn accumulator and density clamp | Rejected: random uncontrolled voice spawn | Estimate: 12 us update
- [ ] 9. Pitch modulation depth lie | DOD: playback speed scales down to 0.5 at depth | Rejected: expensive structural simulation | Estimate: 2 us/block
- [ ] 10. Node localization most stressed room | DOD: habitat stress snapshot bridge with AUP position | Rejected: Transform search | Estimate: 6 us snapshot
- [ ] 11. Binaural routing through acoustic portals | DOD: route via existing AcousticPortalPropagation if present | Rejected: local custom HRTF | Estimate: tier gated
- [ ] 12. Hull popping cavitation spike | DOD: pressure spike injection in DSP params | Rejected: extra AudioClip | Estimate: 4 us/block
- [ ] 13. AUP shift safety | DOD: source position stored as AUP-like scalar snapshot or shift-aware handle | Rejected: raw world-space cache | Estimate: 0 us idle
- [ ] 14. Math LOD Low tier fallback | DOD: Low disables granular density, uses pitched fallback request | Rejected: full granular on MX350 | Estimate: saves >40 us/block
- [ ] 15. Zero-GC grain ring | DOD: fixed structs/NativeArray, no managed alloc in Tick/DSP | Rejected: List/Queue | Estimate: 0 B/frame
- [ ] 16. VISUAL_SYNC parameter updates | DOD: late-frame tick or existing dispatcher phase | Rejected: Update loop | Estimate: 5 us/frame
- [ ] 17. Blackbox ActiveAudioGrains telemetry | DOD: 300-entry fixed ring + dump path | Rejected: Debug.Log spam | Estimate: 2 us/frame
- [ ] 18. Haptics tie-in | DOD: fixed haptic request bridge or no-op interface fallback | Rejected: direct Gamepad call from audio | Estimate: 4 us enqueue
- [ ] 19. Compile check FloatMode.Fast | DOD: BurstCompile attribute and compile evidence | Rejected: non-Burst math kernel | Estimate: compile gate

## Loop Log

- Loop 0: Prompt extracted from CURRENT_BATCH.md, mandates selected, codebase mapping in progress.
