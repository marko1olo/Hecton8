# ECOSYSTEM_POPULATION_BALANCER Rationale

## Initial Scope

Problem: Batch prompt requires Lotka-Volterra enforcement over active entities, but the requested `Assets/_Project/Scripts/AI/Ecosystem/` folder is absent on disk. Existing surfaces are `Assets/_Project/Scripts/Ecosystem`, `Assets/_Project/Scripts/World/EcosystemDirector.cs`, and `Assets/_Project/Scripts/AI/Ecology/Migration`.

Solution: Inspect existing contracts first, then implement through the narrowest data-only AI/Ecology surface that can write entity AUP/flags or consume existing director/state buffers without `Instantiate`/`Destroy`.

Rejected Alternatives: Do not move the existing `World/EcosystemDirector.cs`; relocation is architecture drift and unsafe under 20+ concurrent agents. Do not create a detached balancer that cannot reach the active entity buffers; that would be fake compliance.

Scalability potential: Low uses 1Hz ColdTick and invisible/unloaded chunk culling only. Middle runs full local biomass clamps. High permits Tier 1 flee-down state before cull. Ultra keeps richer telemetry and more precise sector diagnostics while preserving gameplay cost caps.

Hardware Impact: Target i3/MX350; expected active-entity flag pass stays bounded and cold cadence. Estimated saving is workload-dependent; no profiler proof yet, so status is PENDING VERIFICATION.

