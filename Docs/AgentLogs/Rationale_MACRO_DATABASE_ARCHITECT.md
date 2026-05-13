# Rationale_MACRO_DATABASE_ARCHITECT

Status: PENDING VERIFICATION

## Decision Log

### Initial Boundary
Problem: H8_MacroDB must cover 4 million sectors without permanent RAM residency or plugin dependency.
Solution: Build an isolated Core Database assembly with a service interface in Contracts, fixed binary `.h8db` pages, pointer-based B-Tree traversal, and native cache ownership hooks.
Rejected Alternatives: SQLite/plugin storage rejected by prompt and GC risk. Managed `Dictionary<ulong, byte[]>` rejected because it duplicates global world state in RAM and creates managed allocations.
Scalability potential: Low uses 1km hydration radius and minimal cache; Middle uses 2km; High keeps broader prefetch; Ultra spends saved CPU on wider speculative hydration and richer telemetry.
Hardware Impact: Estimated low-end i3/MX350 gain is removal of multi-million-entry managed state, avoiding GC spikes and limiting MicroSD I/O to local sector windows.

### Mandatory Mandates
Problem: Native pager touches contracts, persistence, streaming, AUP, unsafe memory, and telemetry at once.
Solution: Use mandate set: registry DI, binary save, crash telemetry, AUP, native jobs/memory, zero-GC, world residency, persistent registry.
Rejected Alternatives: Reading only AGENTS.md rejected because batch explicitly requires registry mandates.
Scalability potential: Mandates force tiered radius, hysteresis, and blackbox evidence instead of a fixed platform assumption.
Hardware Impact: Expected low-end benefit is bounded cache and I/O work; high-tier benefit is expanded prefetch without changing authority.
