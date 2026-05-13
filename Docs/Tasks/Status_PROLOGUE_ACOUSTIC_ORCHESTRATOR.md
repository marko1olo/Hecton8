# Status_PROLOGUE_ACOUSTIC_ORCHESTRATOR

Prompt: PROLOGUE_ACOUSTIC_ORCHESTRATOR
Role: DSP_ACOUSTIC_LEAD
Domain: Audio / Prologue DSP
Status: PENDING VERIFICATION - UNITY EDITOR LOCKED / MCP SESSION UNAVAILABLE

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
- [x] 1. Extend IAudioService for prologue DSP control | Justification: Added 64-byte AudioTransitionState and QueuePrologueAudioTransition bridge through IAudioService/SpatialAudioManager/NoOpAudioService. DOD: interface expansion only. | Alternatives Rejected: direct singleton reads from orchestrator into renderer. | Estimate: 1.2 us enqueue path
- [x] 2. Consume PrologueStageSignal | Justification: No PrologueStageSignal exists in source; consumed actual AtmosphericReentrySignal plus PrologueCompleteSignal with span snapshots. DOD: adapt to existing signal contract. | Alternatives Rejected: inventing duplicate stage signal. | Estimate: 4.0 us at 32 signals
- [x] 3. ASMDEF isolation: Hecton8.Audio.Prologue -> Contracts | Justification: Added Hecton8.Audio.Prologue asmdef and PrologueAcousticOrchestrator. DOD: isolated assembly with explicit Core/Contracts references required by current IAudioService location. | Alternatives Rejected: folding prologue logic into SpatialAudioManager. | Estimate: 0 us hot path
- [x] 4. Vacuum LPF 400Hz + LFE bone vibration | Justification: StageSpace/Plasma/Whiteout clamp renderer LPF to 400Hz and drive a 40Hz LFE oscillator. DOD: cinematic fake, not environment simulation. | Alternatives Rejected: AudioMixer coroutine automation. | Estimate: 3.5 us per 512-sample block
- [x] 5. Granular plasma from UniverseVelocity | Justification: UniverseVelocity normalizes into PrologueGranularStress and raises structural granular stress/velocity in renderer. DOD: reuse STRUCTURAL_ACOUSTICS granular path. | Alternatives Rejected: new plasma synth voice bank. | Estimate: 2.0 us per block plus existing granular cost
- [x] 6. Splashdown 100ms 40Hz sine sweep on PrologueCompleteSignal | Justification: PrologueCompleteSignal queues one splashdown sequence and renderer injects 100ms 40->56Hz sub sweep. DOD: oscillator phase accumulation. | Alternatives Rejected: PlayOneShot impact clip. | Estimate: 4.0 us per active 512-sample block
- [x] 7. 3s filter sweep from 400Hz to 22000Hz | Justification: Orchestrator applies smoothstep LPF sweep from 400Hz to 22000Hz over 3 seconds. DOD: deterministic scalar interpolation. | Alternatives Rejected: per-frame AudioMixer parameter strings. | Estimate: 1.0 us late-frame
- [x] 8. Enable AcousticPortalPropagation handoff | Justification: Ocean handoff sets PortalActive and feeds portal blend into ambient depth/reverb send while existing SpatialAudioManager remains the propagation owner. DOD: decoupled portal cue, no fake path graph. | Alternatives Rejected: running portal pathfinding without a valid AUP source/listener pair. | Estimate: 1.5 us per block
- [x] 9. Route transition audio through lock-free NativeQueue | Justification: Renderer owns NativeQueue<AudioTransitionState> with 32-command soft cap and drains into double-buffered snapshots. DOD: SPSC command lane. | Alternatives Rejected: direct volatile writes from orchestrator to renderer. | Estimate: 1.5 us per command
- [x] 10. Keep re-entry AUP-independent | Justification: Orchestrator reads no Transform/world position and only passes scalar DSP state. DOD: no AUP math in seam. | Alternatives Rejected: capsule listener distance model. | Estimate: 0 us
- [x] 11. Math LOD: Low tier disables granular plasma | Justification: Low/MX350/low-memory path sets FlagLowTierProxy and zeroes granular stress while preserving LPF/LFE proxy. DOD: cheap sonic fake. | Alternatives Rejected: inventing/importing an unauthored WAV asset. | Estimate: saves existing granular voices on low tier
- [x] 12. Update audio parameters in VISUAL_SYNC | Justification: PrologueAcousticOrchestrator registers ILateFrameTickable on PriorityLayer.Environment. DOD: no Update/coroutine lane. | Alternatives Rejected: MonoBehaviour.Update. | Estimate: 4.0 us late-frame worst case
- [x] 13. Zero-GC interpolation and oscillator injection | Justification: Uses spans, NativeQueue, scalar fields, fixed rings, and phase accumulation; no LINQ/string/coroutine hot path. DOD: hot path allocation scan. | Alternatives Rejected: managed event delegates per transition. | Estimate: 0 B/frame
- [x] 14. Push AudioTransitionState to telemetry | Justification: Added 300-entry NativeArray prologue transition ring and Dump_PROLOGUE_ACOUSTIC_ORCHESTRATOR.bin on invalid state. DOD: fixed blackbox. | Alternatives Rejected: Debug.Log stream. | Estimate: 1.0 us per command
- [BLOCKED BY UNITY LOCK] 15. Verify Burst compilation of sine sweep | Justification: Added BurstCompile PrologueSplashdownSineSweepProbeJob; Unity batchmode aborted because another Unity instance has the project open and MCP reports zero attachable sessions. | Alternatives Rejected: claiming stale Library DLL proof. | Estimate: verification blocked, no runtime cost

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md using CLI. Status and rationale files were missing; created fresh batch memory.
- Loop 1: Re-read prompt and mandates; verified contract direction against GlobalRegistry/IAudioService and existing AtmosphericReentrySignal/PrologueCompleteSignal.
- Loop 2: Reviewed renderer queue/snapshot path; confirmed command lane drains into audio parameter snapshot before producer thread reads.
- Loop 3: Reviewed DSP block; confirmed LFE, splash sweep, LPF clamp, portal blend, and granular stress are scalar-only and AUP-independent.
- Loop 4: Reviewed activation path; attached orchestrator to PFB_SpatialAudioManagerRoot so the bootstrap scene instantiates the seam.
- Loop 5: Re-read orchestrator and found ocean-handoff downgrade bug from lingering atmospheric whiteout signal; fixed StageOceanHandoff to be sticky after complete signal.
- Verification: `dotnet build Hecton8.Core.csproj --no-restore` fails on stale generated project references unrelated to this seam. Unity batchmode compile aborted because another Unity instance has C:/hades/Hecton8 open. MCP editor/console reads report no active Unity session.
