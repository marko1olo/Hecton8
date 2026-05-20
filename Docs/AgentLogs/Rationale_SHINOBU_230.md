# SHINOBU_230 Rationale

Status: PENDING VERIFICATION

## Initial Decision Record

Problem: Charger behavior was assigned as an energy transaction boundary between SOA inventory slots and CSR power nodes; direct `Battery` object references, per-charger `Update`, and material mutation would violate zero-GC and cache locality rules.

Solution: Start with source archaeology, then implement unmanaged DTOs and Burst jobs only where existing project contracts can be matched. Treat chargers as cold adapters registering indices; simulation truth remains flat native data.

Rejected Alternatives: Standard Unity `Update` scripts, `Battery[]` object fields, material instance LED updates, and singleton polling were rejected because they create per-object cadence, heap scatter, renderer churn, or hidden global coupling.

Scalability potential: Low uses throttled logistics cadence and shader-side LED status. Middle runs full transaction cadence with conservative telemetry. High increases visual/audio proxy richness. Ultra spends saved CPU on richer presentation buffers, not unbounded simulation.

Hardware Impact: Expected low-end i3/MX350 gain is removal of per-charger managed dispatch and object traversal. Exact microseconds remain PENDING until compile/runtime profiling evidence exists.
