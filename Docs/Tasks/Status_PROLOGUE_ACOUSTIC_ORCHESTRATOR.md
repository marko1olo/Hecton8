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
- [x] 9. Route transition audio through lock-free NativeQueue | Justification: Renderer owns a prewarmed NativeQueue<AudioTransitionState> with 32-command soft cap and drains into double-buffered snapshots. DOD: SPSC command lane with cold queue block allocation. | Alternatives Rejected: direct volatile writes from orchestrator to renderer. | Estimate: 1.5 us per command
- [x] 10. Keep re-entry AUP-independent | Justification: Orchestrator reads no Transform/world position and only passes scalar DSP state. DOD: no AUP math in seam. | Alternatives Rejected: capsule listener distance model. | Estimate: 0 us
- [x] 11. Math LOD: Low tier disables granular plasma | Justification: Low/MX350/low-memory path sets FlagLowTierProxy and zeroes granular stress while preserving LPF/LFE proxy. DOD: cheap sonic fake. | Alternatives Rejected: inventing/importing an unauthored WAV asset. | Estimate: saves existing granular voices on low tier
- [x] 12. Update audio parameters in VISUAL_SYNC | Justification: PrologueAcousticOrchestrator registers ILateFrameTickable on PriorityLayer.Environment. DOD: no Update/coroutine lane. | Alternatives Rejected: MonoBehaviour.Update. | Estimate: 4.0 us late-frame worst case
- [x] 13. Zero-GC interpolation and oscillator injection | Justification: Uses spans, NativeQueue, scalar fields, fixed rings, and phase accumulation; no LINQ/string/coroutine hot path. DOD: hot path allocation scan. | Alternatives Rejected: managed event delegates per transition. | Estimate: 0 B/frame
- [x] 14. Push AudioTransitionState to telemetry | Justification: Added 300-entry NativeArray prologue transition ring and Dump_PROLOGUE_ACOUSTIC_ORCHESTRATOR.bin on invalid state. DOD: fixed blackbox. | Alternatives Rejected: Debug.Log stream. | Estimate: 1.0 us per command
- [BLOCKED BY GLOBAL COMPILE STATE] 15. Verify Burst compilation of sine sweep | Justification: Added BurstCompile PrologueSplashdownSineSweepProbeJob with CompileSynchronously=true and a cold setup schedule/complete probe; Unity batchmode aborted because another Unity instance has the project open and MCP transport is unavailable. Local dotnet build cannot reach final prologue proof. | Alternatives Rejected: claiming stale Library DLL proof or leaving the Burst marker unscheduled. | Estimate: verification blocked, no runtime cost after cold setup

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md using CLI. Status and rationale files were missing; created fresh batch memory.
- Loop 1: Re-read prompt and mandates; verified contract direction against GlobalRegistry/IAudioService and existing AtmosphericReentrySignal/PrologueCompleteSignal.
- Loop 2: Reviewed renderer queue/snapshot path; confirmed command lane drains into audio parameter snapshot before producer thread reads.
- Loop 3: Reviewed DSP block; confirmed LFE, splash sweep, LPF clamp, portal blend, and granular stress are scalar-only and AUP-independent.
- Loop 4: Reviewed activation path; attached orchestrator to PFB_SpatialAudioManagerRoot so the bootstrap scene instantiates the seam.
- Loop 5: Re-read orchestrator and found ocean-handoff downgrade bug from lingering atmospheric whiteout signal; fixed StageOceanHandoff to be sticky after complete signal.
- Loop 6: Re-read prefab activation and found default StageSpace could publish a 400Hz LPF from the global audio root before prologue was armed. Fixed with signal-armed neutral startup and dirty-state publish throttling.
- Loop 7: Re-read completion handling and found repeated PrologueCompleteSignal packets could restart the 3s sweep. Fixed with first-seen complete sequence gating.
- Loop 8: Re-read post-handoff atmospheric handling and removed redundant forced publishes after OceanHandoff.
- Loop 9: Upgraded the sine sweep probe job to CompileSynchronously=true for early Burst failure when Unity compile is available.
- Loop 10: Re-read replay/rearm behavior against OrbitalRelativityDirector and found AtmosphericReentrySignal.Sequence increments per packet, so packet sequence is not a safe replay discriminator. Kept OceanHandoff hard-sticky after completion instead of guessing replay state.
- Loop 11: Matched HectonFluidEngine's PrologueCompleteSignal filter so audio only fires splashdown/ocean handoff for PhaseOceanHandoff or FlagForceWhiteout packets.
- Loop 12: Matched the existing sonar upload queue cold-prewarm pattern for the prologue NativeQueue so the first transition command does not pay queue block allocation during the cinematic seam.
- Loop 13: Removed AtmosphericReentrySignal.Sequence as a force-publish trigger because OrbitalRelativityDirector increments it per packet. The dirty-state gate now controls atmospheric DSP publication by actual scalar/stage deltas, reducing queue churn.
- Loop 14: Added WarmPrologueSplashdownBurstProbeCold so the synchronous Burst proof job is actually scheduled once during renderer buffer initialization instead of existing only as a passive attribute.
- Verification: `git diff --check` passes for touched seam/log files after Loop 14. Latest `dotnet build Hecton8.Core.csproj --no-restore` attempt timed out after 120s before a final compiler result; the refreshed log only reached unrelated project/package output and no prologue error. Prior local attempts stopped on stale/global missing namespaces and types such as `Hecton8.Environment.Fluids`, `Hecton8.Audio.Echolocation`, `Hecton8.Audio.Propagation`, and related service contracts before prologue proof was reachable. Unity batchmode still cannot verify because another Unity instance has C:/hades/Hecton8 open; MCP editor/console transport is unavailable.
