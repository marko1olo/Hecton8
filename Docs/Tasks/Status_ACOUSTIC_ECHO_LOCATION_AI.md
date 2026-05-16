# Status_ACOUSTIC_ECHO_LOCATION_AI

Prompt ID: ACOUSTIC_ECHO_LOCATION_AI
Role: AI_PROGRAMMER
Domain: AI/PATHING
Task Count: 18
Status: CORE COMPLETE / FINAL VALIDATION BLOCKED BY DEPENDENCY

## Mandates Read
- AUD_Acoustic_Sonar_Occlusion_Sensory_Simulation.txt
- AUD_DSP_Audio_Synthesis_ThreadSafe_SPSC.txt
- AI_Creature_Cognition_States.txt
- AI_Navigation_AStar_Funnel_Smoothing_Pathfinding.txt
- MATH_AUP_Determinism_Sync.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt
- ARCH_Signal_Lane_Segregation.txt

## Task Checklist
- [x] 1. PURGE_SINGLETONS: N/A | Justification: no singleton purge was in scope; static sensory runtime owns native queues only | Alternatives Rejected: scene lookup and FindObjectOfType | Estimate: 0 us/frame
- [x] 2. DEBT_CLEANUP: Strip exact player AUP knowledge from Abyssal predators | Justification: predator acoustic branch now resolves `AcousticEchoLocationRuntime.TryResolvePredatorEcho` and uses `InvestigateAup`; visual contact remains the only direct player path | Alternatives Rejected: feeding `NoiseSystem.PlayerNoiseSignal.PositionAup` to predators | Estimate: saves ~3 us/frame of direct-distance acoustic logic per predator
- [x] 3. DATA_EVICTION: Read NativeQueue<EchoTap> from DSP system | Justification: `NativeQueue<EchoTap>` bridge, parallel writer, bounded 32-tap frame slab, and sonar tap hydration API implemented | Alternatives Rejected: managed List/array append from DSP | Estimate: bounded to ~8-18 us/frame for 32 taps
- [x] 4. BURST_ALGORITHM: EchoTrackingJob updates InvestigateAUP to portal AUP | Justification: `EchoTrackingJob` selects the loudest valid tap and writes `bestTap.PortalAup` as `InvestigateAup` | Alternatives Rejected: main-thread LINQ/nearest-player selection | Estimate: ~4-10 us/job for 32 taps
- [x] 5. AUP_INTEGRITY: Use absolute portal AUPs | Justification: portal acoustics use `AcousticPathResult.LastPortalAup`, converted cell/local exact into `AbsoluteUniversePosition` | Alternatives Rejected: runtime Vector3 as authority | Estimate: avoids ~2 us/frame origin reconstruction drift checks
- [x] 6. DOD_SOA_LAYOUT: Predator pathfinds node-to-node following acoustic breadcrumbs | Justification: cognition receives portal breadcrumb AUP as the acoustic target, so existing predator path selection follows sound nodes | Alternatives Rejected: direct player target injection | Estimate: no new pathing allocation; reused existing cognition submit path
- [x] 7. SIGNAL_FLOW: Listen to MovementAcousticSignal | Justification: sensory refresh consumes `SignalBus<MovementAcousticSignal>.GetFrameSnapshot()` into direct-node low-tier taps | Alternatives Rejected: destructive `GlobalSignals.TryDequeueMovementAcoustic` reads | Estimate: ~3-12 us/frame depending on snapshot count, capped at 32 taps
- [x] 8. LOW_TIER_FAKE: Predator swims directly to last known sound node | Justification: low-tier movement and ping fallback set `PortalAup = SourceAup` and flag `FlagLowTierDirectNode` | Alternatives Rejected: portal graph solve on MX350 path | Estimate: ~0 portal solve cost on low tier
- [x] 9. HIGH_END_OVERKILL: Predator head sweep IK hint while approaching echo portal | Justification: high-tier echo result emits `HeadSweep01`; Fauna evaluation offsets the head-look target laterally while no visual player target exists | Alternatives Rejected: full procedural smell cone simulation | Estimate: ~2-4 us/frame, no allocation
- [x] 10. REACTIVE_VFX: N/A | Justification: prompt marks N/A; no VFX surface edited | Alternatives Rejected: speculative visual pings | Estimate: 0 us/frame
- [x] 11. STP_STABILIZATION: N/A | Justification: prompt marks N/A | Alternatives Rejected: unrelated stabilization edits | Estimate: 0 us/frame
- [x] 12. NAN_VACCINATION: Validate portal AUPs before pathing | Justification: all source/portal AUP locals and intensities are finite-checked; invalid runtime conversion dumps black box | Alternatives Rejected: trusting DSP or portal data | Estimate: ~1-3 us/frame
- [x] 13. BLACKBOX_LOGGING: Log AcousticHuntsTriggered | Justification: 300-entry native black-box ring records hunt count, portal AUP, source, flags, sequence, and hash; fault dump path is `Docs/AgentLogs/Dump_ACOUSTIC_ECHO_LOCATION_AI.bin` | Alternatives Rejected: chat/debug-only reporting | Estimate: ~1 us/resolved hunt
- [x] 14. TRIPLE_STRIKE_REPAIR: Fix array reads from DSP | Justification: sonar tap hydration clamps `tapCount` against read-only length and frame cap; queue overflow drains stale surplus after frame cap | Alternatives Rejected: trusting DSP tap count | Estimate: prevents out-of-bounds at ~0.5 us guard cost
- [x] 15. HOMEOSTASIS_ADAPTATION: N/A | Justification: prompt marks N/A | Alternatives Rejected: unrelated metabolic adaptation | Estimate: 0 us/frame
- [x] 16. DECOY_MECHANIC: Noisemakers prioritized by loudest echo tap | Justification: job scores `Volume01 * Transmission01` and selects maximum intensity, so loud noisemaker taps beat quiet player movement | Alternatives Rejected: source-type priority over loudness | Estimate: included in 32-tap scan
- [x] 17. SILENCE_LOSS: Silence for 5 seconds loses trail and begins random search | Justification: trail expires when `CurrentTime - LastHeardTime >= 5f`; `TryResolvePredatorEcho` returns false, leaving cognition to non-target wander/search | Alternatives Rejected: infinite last-known-player memory | Estimate: no extra cost beyond timestamp compare
- [BLOCKED BY DEPENDENCY] 18. FINAL_VALIDATION: dotnet build | Justification: `dotnet build Hecton8.Core.csproj --no-restore` is blocked by external batch changes: deleted `Assets/_Project/Scripts/Core/Bucketing/ModuloSimulationBucketer.cs` and missing/restored tether contract churn outside this agent domain | Alternatives Rejected: reverting other agents' Bucketing/Tether work | Estimate: validation cost unknown until dependency wall is cleared

## Iteration Log
- Loop 0: Prompt extracted from CURRENT_BATCH.md. Status file was missing; initialized.
- Loop 1: Tasks 1-5 implemented/re-read. Compile attempt required restore, then exposed external tether contract churn.
- Loop 2: Tasks 6-10 verified against Fauna consumer path and IK head-look handoff.
- Loop 3: Tasks 12-14 verified against AUP finite guards, sonar tap count clamps, queue overflow drain, and black-box dump path.
- Loop 4: Tasks 16-17 verified against loudest-tap selection and five-second silence expiry.
- Loop 5: Re-read `AcousticEchoLocationRuntime`, `FaunaBrain.Compatibility`, and `SpatialAudioManager`; patched stale queue overflow behavior; final build remains blocked by external deleted Bucketing source.
