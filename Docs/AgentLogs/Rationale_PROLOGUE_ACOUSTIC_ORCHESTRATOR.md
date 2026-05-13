# Rationale_PROLOGUE_ACOUSTIC_ORCHESTRATOR

Status: PENDING VERIFICATION - UNITY EDITOR LOCKED / MCP SESSION UNAVAILABLE

## Session Initialization
Problem: Orbital drop audio currently transitions instantly from vacuum to ocean, producing a pop and killing the intended sensory continuity.
Solution: Build a deterministic DSP orchestration layer around existing audio contracts, prologue signals, lock-free command flow, VISUAL_SYNC updates, tiered math LOD, and fixed blackbox telemetry.
Rejected Alternatives: AudioSource.PlayOneShot, string event names, coroutines, runtime singleton wiring, and per-frame allocations are rejected by AGENTS.md and audio mandates.
Scalability potential: Low uses LPF sweeps plus pitched loop proxy; Middle uses LPF, portal handoff, and splash sweep; High adds granular stress intensity; Ultra permits denser acoustic overkill only if queued DSP path remains allocation-free.
Hardware Impact: Target low silicon is i3/MX350. Expected gain versus naive AudioSource/coroutine path is reduced main-thread jitter and 0 B/frame orchestration overhead; measured proof absent.

## Contract and Signal Decision
Problem: The prompt names PrologueStageSignal, but the codebase exposes AtmosphericReentrySignal and PrologueCompleteSignal as the actual prologue stage lanes.
Solution: Consume AtmosphericReentrySignal for Space/Plasma/Whiteout and PrologueCompleteSignal for OceanHandoff; expose the seam through IAudioService.QueuePrologueAudioTransition(in AudioTransitionState).
Rejected Alternatives: Creating a duplicate PrologueStageSignal would split authority and require another publisher. Direct renderer dependency from prologue assembly would violate the GlobalRegistry interface pattern.
Scalability potential: Low keeps only LPF/LFE scalar state; Middle adds filter sweep and portal blend; High/Ultra preserve granular stress intensity through the existing structural acoustic renderer.
Hardware Impact: Main-thread stage consumption is bounded span iteration over existing SignalBus snapshots, estimated under 4 us for 32 packets on i3/MX350.

## DSP Path Decision
Problem: The seam must create vacuum muffling, plasma tearing, splashdown bass, and underwater handoff without managed audio callbacks or clip churn.
Solution: Use NativeQueue<AudioTransitionState> as the SPSC command lane, drain into double-buffered AudioParameterSnapshot, then render 40Hz LFE and 100ms splashdown sweep through existing producer-side phase accumulation.
Rejected Alternatives: AudioSource.PlayOneShot, AudioMixer string automation, coroutine fades, and new plasma-specific voice banks.
Scalability potential: Low disables granular plasma and uses the procedural pitched LFE proxy; Middle runs oscillator and LPF sweep; High/Ultra spend existing granular voices on metal stress scream.
Hardware Impact: Expected hot cost is scalar interpolation plus two oscillator calls during active splashdown, estimated below 0.01 ms per 512-sample block on low-end silicon.

## Portal Handoff Decision
Problem: AcousticPortalPropagation is a spatial path system, but the prologue seam is local to the capsule and must not invent an AUP source/listener pair.
Solution: Keep SpatialAudioManager as portal owner, pass FlagPortalActive/PortalBlend01 through AudioTransitionState, and feed portal blend into ambient depth drive and interior FDN send during ocean handoff.
Rejected Alternatives: Running path graph resolution for a non-world-space capsule cue, or adding Transform/AUP dependence to the re-entry seam.
Scalability potential: Low gets the cheap LPF/opening cue; High/Ultra receive stronger ambience/reverb density without pathfinding overhead.
Hardware Impact: Avoids portal graph expansion on MX350 while preserving the sensory handoff; estimated savings versus forced path query is tens of microseconds on dense scenes.

## Activation and Blackbox Decision
Problem: A pure code component would not run unless authored into the scene/prefab, and failures must be postmortem-debuggable.
Solution: Attach PrologueAcousticOrchestrator to PFB_SpatialAudioManagerRoot and add a 300-entry NativeArray telemetry ring with Dump_PROLOGUE_ACOUSTIC_ORCHESTRATOR.bin on invalid transition state.
Rejected Alternatives: Runtime-created singleton GameObject and Debug.Log telemetry spam.
Scalability potential: Activation cost is cold-only; telemetry is fixed-size and independent of device tier.
Hardware Impact: Native ring writes are one struct copy per transition command, estimated near 1 us and 0 B/frame.

## Verification Blocker
Problem: Compile verification is required, but Unity MCP reports no active session while Unity batchmode refuses to open because another Unity instance owns the project lock.
Solution: Record verification as blocked by external editor lock. Local dotnet build was attempted and rejected as authoritative because generated csproj references are stale and fail on pre-existing missing asmdef references.
Rejected Alternatives: Claiming stale Library/ScriptAssemblies binaries as proof, or closing another user's Unity process.
Scalability potential: No runtime impact; compile proof still required when the active editor is available.
Hardware Impact: None until verification can run.

## OMEGA POLISH CHANGES
Problem: Self-review found that AtmosphericReentrySignal can continue after PrologueCompleteSignal and downgrade the seam from OceanHandoff back to Whiteout, aborting the 3s LPF opening and portal blend.
Solution: Made StageOceanHandoff sticky inside PrologueAcousticOrchestrator once accepted; later atmospheric packets may update velocity/heat but cannot downgrade the completed handoff.
Rejected Alternatives: Clearing atmospheric lanes or depending on publisher shutdown order; both are cross-domain assumptions.
Scalability potential: Low/Middle/High/Ultra all preserve deterministic handoff state with one byte branch and no allocations.
Hardware Impact: One branch inside bounded SignalBus span consumption; estimated under 0.1 us per packet on i3/MX350.

Problem: The seam had code but could be inactive if no scene object owned it.
Solution: Attached PrologueAcousticOrchestrator to Assets/_Project/Prefabs/Audio/PFB_SpatialAudioManagerRoot.prefab, the existing audio bootstrap root.
Rejected Alternatives: Runtime-created singleton GameObject, bootstrap cross-assembly dependency from Core into Prologue.
Scalability potential: Cold activation only; no hot path cost after registration.
Hardware Impact: One MonoBehaviour component in the audio root; no frame cost beyond the existing ILateFrameTickable seam.

Cinematic Cheats used: 400Hz LPF vacuum fake, 40Hz LFE bone-conduction fake, structural-granular plasma scream driven by UniverseVelocity, 100ms 40->56Hz splashdown sine sweep, scalar portal blend instead of physical capsule acoustic propagation.

Final Git Diff: relevant owned files show PFB_SpatialAudioManagerRoot.prefab +21, PrologueAcousticOrchestrator.cs +2, Status_PROLOGUE_ACOUSTIC_ORCHESTRATOR.md updated, Rationale_PROLOGUE_ACOUSTIC_ORCHESTRATOR.md updated, LOG_PROLOGUE_ACOUSTIC_ORCHESTRATOR.md added. Existing dirty files from other agents remain untouched.
