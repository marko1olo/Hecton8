# Rationale_PROLOGUE_SEQUENCE_DIRECTOR

Status: PENDING VERIFICATION.

## Decision 0 - Agent Scope

Problem: Prologue pacing touches narrative, input, audio, haptics, streaming, velocity, camera juice, and fluid systems while 20+ agents may be changing those domains.
Solution: Keep ownership in a narrative/prologue service and communicate by contracts/signals/registry interfaces only. Inspect existing contracts before introducing any type.
Rejected Alternatives: Direct references to concrete audio, input, streaming, or fluid classes would compile faster initially but create cross-domain coupling and race other agents.
Scalability potential: Low tier uses deterministic flow with cheap waits and proxy surface; Middle/High/Ultra can consume the same signals for richer VWS, haptics, camera impulse, and ocean visuals.
Hardware Impact: Estimated low-end gain vs concrete polling/wiring is 10-35 us per sequence wait iteration and lower compile churn risk on i3/MX350.

## Decision 1 - Mandate Selection

Problem: Awaitable drop sequence is not a single subsystem; it is orchestration across registry, streaming, telemetry, input, haptics, audio, and diegetic UI.
Solution: Use eight mandates: GlobalRegistry DI, Bootstrap Awaitable safety, Zero-GC, Crash Telemetry, World Streaming Residency, DSP SPSC Audio, Device/Haptics, Diegetic UI.
Rejected Alternatives: Reading only narrative docs would miss hot-path allocation, chunk readiness, and haptic/audio signal constraints.
Scalability potential: Low tier skips high-res chunk waits; Ultra can continue waiting for full visual hydration without changing service API.
Hardware Impact: Prevents blind waits and string/event spam; estimated 0.02-0.08 ms avoided during transition frames on i3/MX350.
