# ECOLOGY_MUTATION_DIRECTOR Rationale

Status: PENDING VERIFICATION

## Decision 0 - Task Boundary
Problem: The batch prompt declares 19 tasks but the primary objective list only numbers 1-18.
Solution: Track 18 implementation tasks plus recursive re-verification as Task 19.
Rejected Alternatives: Treating the XML as 18 tasks would violate the declared task count and skip the anti-division recheck.
Scalability potential: Low keeps mutation checks on loaded entities only; Middle/High/Ultra can spend saved cycles on macro-swarm and richer shader twitch.
Hardware Impact: 0us runtime; prevents scope drift before code.

## Decision 1 - Mandate Set
Problem: Mutation touches genetics, hazards, AI behavior, visual fake, telemetry, save compression, and AUP safety.
Solution: Read Zero-GC, deterministic RNG, AUP, blackbox telemetry, swarm/cognition AI, save persistence, and cinematic fake mandates before editing.
Rejected Alternatives: Coding directly from prompt text would miss hot-path and persistence rules.
Scalability potential: Low uses bitmask edits and loaded-entity mutation; Ultra can layer more visible shader response without changing authority data.
Hardware Impact: Planning only; expected runtime target remains below 0.1ms per FrostTick slice on i3/MX350.

