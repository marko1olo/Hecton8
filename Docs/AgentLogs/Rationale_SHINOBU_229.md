# SHINOBU_229 Rationale

Status: PENDING VERIFICATION

## Decision 0 - Work Boundary

Problem: Auxiliary equipment routing touches tools, physics, lighting, sonar, VFX, telemetry, and editor diagnostics. Direct concrete dependencies would collide with parallel agents and violate Global Authority boundaries.
Solution: Own only the data-oriented router surface: aligned DTOs, deterministic lifecycle jobs, typed unmanaged payloads, telemetry, static scanner, and editor x-ray. Use hash-based signal payloads and no direct class references to downstream lighting/physics/sonar owners.
Rejected Alternatives: Direct calls into lighting, physics, sonar, or player equipment systems; those would introduce dependency walls on agents 143/144/151 and create new global authority without owner review.
Scalability potential: Low uses bounded arrays, cadence throttling, and visual fake signals; Middle keeps normal cadence; High increases signal density and telemetry; Ultra spends saved CPU on downstream VISUAL_SYNC overkill without bloating simulation truth.
Hardware Impact: Low-end i3/MX350 avoids GameObject/Light/Joint churn and per-frame managed dispatch. Estimated saved work versus 500 component updates: 3000-9000 us CPU and 0 B GC target, pending Unity profiler proof.

## Decision 1 - Mandate Set

Problem: The task demands NativeArray lifecycle, SignalBus broadcasts, AUP precision, ARM64 layout, and tether routing in one pass.
Solution: Read and apply these mandates before coding: Zero-GC, Native Memory/Jobs, ARM64 Layout, AUP Determinism, Signal Lane Segregation, Execution Phases, Tool Equipment Routing, Tether Constraints.
Rejected Alternatives: Reading generic project rules only; this misses exact field layout, phase, and physics ownership laws.
Scalability potential: Mandates enforce continuous `GlobalQualityWeight` cadence instead of low/ultra switches, with separate Low/Middle/High/Ultra behavior in rationale and code.
Hardware Impact: Mandate-driven linear NativeArray and typed signal routing target L1-friendly sequential reads and no managed allocation spikes on MX350/i3-class silicon.
