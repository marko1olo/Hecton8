# Rationale_SHINOBU_147

Status: PENDING VERIFICATION

## Decision 000 - Domain Gate
Problem: Surface-wave task touches rendering, physics, AUP, weather, and abyssal currents; unmanaged cross-domain edits can break parallel agents.
Solution: Keep ownership in `Hecton8.Atmosphere`/surface weather files; expose only compact DTO/query interfaces or existing registry/vault routes after source verification.
Rejected Alternatives: Direct edits to submarine buoyancy/vehicle classes before discovering existing interfaces; this couples domains and violates parallel-agent isolation.
Scalability potential: Low = one to two broad octaves and tiny readback grid; Middle = partial octaves and moderate sample count; High = full visual Gerstner stack; Ultra = foam/whitecap overkill while physics sample count remains bounded.
Hardware Impact: Expected benefit on i3/MX350 comes from eliminating CPU mesh vertex loops and PhysX mesh rebuilds; exact microseconds are unmeasured and remain PENDING VERIFICATION.

## Decision 001 - Prompt Extraction
Problem: Batch file contains neighboring agent directives; polluted context would drive wrong file edits.
Solution: CLI regex extracted only `<AGENT_PROMPT id="SHINOBU_147">...</AGENT_PROMPT>`.
Rejected Alternatives: Manual skim or MCP partial read; both risk truncation and neighboring-prompt leakage.
Scalability potential: Not runtime-facing.
Hardware Impact: None at runtime.
