# Rationale_ABYSSAL_CURRENT_ADVECTION

Status: PENDING VERIFICATION  
Agent: FLUID_MECHANIC  
Prompt ID: ABYSSAL_CURRENT_ADVECTION

## Decision 0 - Prompt Count Mismatch
Problem: The XML header claims "19 TITANIUM TASKS" but the primary numbered list contains tasks 1-18 only.
Solution: Treat the actual numbered primary objectives as authority and record the mismatch in status/log files.
Rejected Alternatives: Inventing a task 19 would contaminate scope and create fake completion evidence.
Scalability potential: No runtime impact. Prevents unbounded work and keeps the batch integrator's accounting deterministic.
Hardware Impact: 0 us/frame. No i3/MX350 runtime cost.

## Decision 1 - Mandate Selection
Problem: Fluid advection touches GPU compute, abyssal currents, VFX particles, AUP, zero-GC, telemetry, and fake-first policy.
Solution: Read eight targeted mandates before code: abyssal flow, fluid VFX, MX350 compute kernels, mobile warp sizing, AUP precision, crash telemetry, zero-GC, and cinematic cheat.
Rejected Alternatives: Reading all mandates would waste context and increase risk of neighbor-domain drift; reading none violates batch protocol.
Scalability potential: Keeps design locked to Low/Mid/High/Ultra tier gates instead of a single middle path.
Hardware Impact: 0 us/frame. One-time CLI reads only.

