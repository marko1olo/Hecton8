# ECOLOGICAL_BIOMASS_ENGINE Rationale

Status: PENDING VERIFICATION

## Decision 0 - Use Existing Ecosystem Owner

Problem: The batch asks for an ecological biomass economy, but the project already has `World/EcosystemDirector` registered as `IEcosystemDirectorService` and used by fauna/audio/director systems.
Solution: Extend the existing ecosystem owner and exposed service contract. This keeps ownership inside ECHELON 3 and avoids a second ecology brain.
Rejected Alternatives: A new manager singleton was rejected because AGENTS.md forbids classic singletons and the registry already owns `IEcosystemDirectorService`. Direct references from `EncounterDirector` to unrelated systems were rejected; the stable bridge is the existing director service interface.
Scalability potential: Low uses 50 m float biomass without diffusion; Middle enables local diffusion; High/Ultra can spend saved CPU on richer flora/AI presentation via scalar outputs.
Hardware Impact: Low-end i3/MX350 avoids GameObject ecology and keeps work in Burst arrays; target cost is below 0.1 ms amortized per FrostTick with no per-frame allocations.

## Decision 1 - Mandate Set

Problem: Ecology touches AI pacing, native arrays, save, AUP indexing, telemetry, and zero-GC hot paths.
Solution: Read and apply: AI_Director_Encounter_Manager, ARCH_Global_Registry_ServiceLocator_DI_Init, DBG_Telemetry_Crash_Reporting_PostMortem, DATA_Save_Persistence_Binary_Delta_Checksum, MATH_Deterministic_RNG_SlotMachine, MATH_Coordinate_Precision_AUP_FloatingOrigin, OPT_Native_Memory_Collections_JobSystem_Protocol, OPT_Zero_GC_Policy_AllocFree_Mandate.
Rejected Alternatives: Reading all 35+ mandates was rejected as noise; these eight are the directly relevant constraint set.
Scalability potential: The selected mandates force Math LODs and isolate expensive diffusion from low-tier hardware.
Hardware Impact: Keeps native memory predictable and avoids managed collections in recurring ecology paths on i3/MX350.
