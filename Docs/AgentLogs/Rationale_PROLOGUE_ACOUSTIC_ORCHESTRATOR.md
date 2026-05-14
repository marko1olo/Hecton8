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
Solution: Record verification as blocked by external editor lock. Local dotnet build was attempted and rejected as authoritative because generated csproj references are stale and fail on pre-existing missing asmdef/namespace references before the prologue compile proof is reachable.
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

## SECOND QUALITY PASS - SCALABLE ACTIVATION
Problem: PrologueAcousticOrchestrator is now attached to the global audio root. Defaulting to StageSpace plus immediate publishing could clamp normal gameplay audio to 400Hz before any prologue signal exists.
Solution: Added signal-armed neutral startup. The orchestrator now starts at open LPF, publishes nothing until AtmosphericReentrySignal or PrologueCompleteSignal is observed, and dirty-state throttles commands after the renderer has the current target.
Rejected Alternatives: Keeping the component disabled in prefab and relying on another prologue system to enable it; that creates a direct cross-domain dependency and a missed-activation risk.
Scalability potential: Low tier avoids all seam queue/telemetry writes outside the actual prologue. Middle/High/Ultra preserve the same DSP quality when armed.
Hardware Impact: Normal gameplay cost drops from one NativeQueue command and telemetry write per late frame to zero until prologue is armed. Estimated saving is 1-3 us/frame and prevents unintended 400Hz global muffling.

Problem: Repeated PrologueCompleteSignal packets can reset the LPF sweep to 400Hz every frame, turning a 3s opening into a stuck muffled state.
Solution: Added `_hasCompleteSequence` and only arms splashdown/sweep on first-seen completion sequence. OceanHandoff remains sticky.
Rejected Alternatives: Trusting publisher one-shot behavior; SignalBus snapshots do not guarantee neighboring systems will never repeat a packet.
Scalability potential: All tiers get deterministic handoff timing. Low remains LPF/LFE only; High/Ultra keep portal blend without redundant command churn.
Hardware Impact: Removes repeated sweep restarts and reduces queue writes after handoff. Estimated saving is proportional to repeated completion packets, usually 1-3 us per suppressed command.

Problem: Burst proof job existed but did not request synchronous Burst compilation.
Solution: Added CompileSynchronously=true to PrologueSplashdownSineSweepProbeJob so Unity/Burst reports the sine-sweep compile failure early when the editor is available.
Rejected Alternatives: Leaving proof as a passive `[BurstCompile]` marker or claiming verification from stale assemblies.
Scalability potential: No runtime quality impact; compile-time proof becomes stricter.
Hardware Impact: No frame cost. Compile-time only.

## THIRD QUALITY PASS - SIGNAL VALIDITY
Problem: A proposed replay rearm path used AtmosphericReentrySignal.Sequence as a session discriminator, but OrbitalRelativityDirector increments that sequence per atmospheric packet. That would let lingering post-handoff packets downgrade OceanHandoff again.
Solution: Keep OceanHandoff hard-sticky after completion and reject sequence-based replay guessing until a real lifecycle/reset signal exists. Also filter PrologueCompleteSignal by PhaseOceanHandoff or FlagForceWhiteout before firing splashdown/ocean DSP, matching the fluid impulse consumer.
Rejected Alternatives: Treating every new atmospheric packet as a new prologue run, or accepting every PrologueCompleteSignal regardless of phase.
Scalability potential: Low/Middle/High/Ultra preserve deterministic handoff with no extra allocations; invalid/non-handoff complete packets now cost one branch and do not wake the renderer queue.
Hardware Impact: Prevents redundant queue writes and sweep restarts from irrelevant complete packets; estimated saving is 1-3 us per rejected packet and no steady-state cost outside signal consumption.

## FOURTH QUALITY PASS - QUEUE PREWARM
Problem: The prologue NativeQueue had a 32-command soft cap but no cold prewarm. The first Enqueue could force an internal native queue block allocation during the cinematic seam.
Solution: Added PrewarmPrologueTransitionQueue(), mirroring the existing sonar tap upload queue pattern: enqueue 32 default states at buffer initialization and drain them immediately while the renderer is still in cold setup.
Rejected Alternatives: Trusting NativeQueue first-use allocation timing, or replacing the queue with direct volatile fields.
Scalability potential: Low/Middle/High/Ultra all get deterministic first-command cost; high-end devices spend saved jitter budget on the actual DSP fakes instead of allocator work.
Hardware Impact: Moves queue block allocation out of the hot transition path. Estimated first-transition saving is tens of microseconds on i3/MX350 if Unity's NativeQueue would otherwise allocate its first block lazily.

## FIFTH QUALITY PASS - ATMOSPHERIC DIRTY GATE
Problem: AtmosphericReentrySignal.Sequence increments per packet in OrbitalRelativityDirector, so using it to force publish bypassed the dirty-state gate and queued a transition command for every atmospheric packet.
Solution: Removed the atmospheric sequence force-publish path. First publish still happens through unset cache sentinels, and subsequent atmospheric updates publish only when stage, flags, cutoff, LFE, granular stress, splash, or portal blend actually changes beyond epsilon.
Rejected Alternatives: Keeping packet sequence as a pseudo-session ID, or adding a new replay/session dependency outside the audio domain.
Scalability potential: Low/Middle/High/Ultra all retain the same sonic behavior; packet-heavy reentry now scales by actual DSP deltas instead of signal cadence.
Hardware Impact: Suppresses redundant NativeQueue commands, telemetry writes, and producer wakes during reentry. Expected saving is 1-3 us per unchanged atmospheric packet on i3/MX350, with 0 B/frame maintained.

## SIXTH QUALITY PASS - BURST PROBE EXECUTION
Problem: The sine-sweep proof job had CompileSynchronously=true, but a passive Burst attribute is weaker than a scheduled cold probe because Unity may not compile the job until first use.
Solution: Added WarmPrologueSplashdownBurstProbeCold(), which allocates a one-float TempJob scratch, schedules PrologueSplashdownSineSweepProbeJob once during renderer buffer initialization, completes it immediately, and disposes the scratch.
Rejected Alternatives: Leaving the job unscheduled, or scheduling the proof in the active prologue path.
Scalability potential: Low/Middle/High/Ultra all pay the proof cost only during cold renderer setup; runtime DSP path remains unchanged.
Hardware Impact: Moves Burst compile/proof work out of the cinematic seam. Runtime cost is 0 B/frame; cold setup has one TempJob NativeArray<float>[1] allocation and immediate disposal.

## SEVENTH QUALITY PASS - ATTRIBUTED PROOF CONSISTENCY
Problem: Code readback found PrologueSplashdownSineSweepProbeJob was missing CompileSynchronously=true even though the status/rationale described it as synchronously compiled.
Solution: Restored CompileSynchronously=true on the BurstCompile attribute so the scheduled cold probe and the compile policy agree.
Rejected Alternatives: Leaving documentation ahead of code, or relying only on the scheduled probe without explicit synchronous compile intent.
Scalability potential: No runtime behavior change; all tiers keep the cold proof path and zero hot-path allocation.
Hardware Impact: No frame cost. Improves editor/build failure locality for the splashdown sine probe.
