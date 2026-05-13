# Rationale_PROLOGUE_ACOUSTIC_ORCHESTRATOR

Status: PENDING VERIFICATION

## Session Initialization
Problem: Orbital drop audio currently transitions instantly from vacuum to ocean, producing a pop and killing the intended sensory continuity.
Solution: Build a deterministic DSP orchestration layer around existing audio contracts, prologue signals, lock-free command flow, VISUAL_SYNC updates, tiered math LOD, and fixed blackbox telemetry.
Rejected Alternatives: AudioSource.PlayOneShot, string event names, coroutines, runtime singleton wiring, and per-frame allocations are rejected by AGENTS.md and audio mandates.
Scalability potential: Low uses LPF sweeps plus pitched loop proxy; Middle uses LPF, portal handoff, and splash sweep; High adds granular stress intensity; Ultra permits denser acoustic overkill only if queued DSP path remains allocation-free.
Hardware Impact: Target low silicon is i3/MX350. Expected gain versus naive AudioSource/coroutine path is reduced main-thread jitter and 0 B/frame orchestration overhead; measured proof absent.
