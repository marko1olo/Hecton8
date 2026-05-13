# Rationale - WAKE_TURBULENCE_COMPUTE

Status: PENDING VERIFICATION

## Intake

Problem: Wake turbulence must be added without coupling fluid VFX to creature/drop-pod concrete implementations that may be edited by other agents.
Solution: Use the existing fluid engine and signal/contracts layer if present; otherwise add the narrowest contract-level signal surface and keep emitters optional.
Rejected Alternatives: Direct references from fluid runtime to Leviathan or Drop Pod scripts would create cross-domain compile risk and violate parallel-agent decoupling.
Scalability potential: Low = 2 wakes and cheapest dot-distance branch; Middle = 4 wakes; High = 8 wakes; Ultra = 8 wakes with stronger vortex shaping and visual-overkill particle response if existing shader budget allows.
Hardware Impact: Expected MX350 gain versus CPU particle displacement is preserved by keeping wake advection GPU-side and limiting uploaded wake records to a fixed buffer.

Problem: Physical wake simulation would be expensive and uncontrollable.
Solution: Treat wake turbulence as temporary visual velocity primitives in the advection field, not fluid truth.
Rejected Alternatives: Navier-Stokes or per-particle CPU wake forces are too slow, harder to tune, and irrelevant for gameplay correctness.
Scalability potential: The same signal can drive cheap Low-tier push or richer High-tier vortex without changing gameplay state.
Hardware Impact: Fixed 8-record GPU buffer bounds PCIe upload and ALU; Low-tier cap trims shader work to 2 records.
