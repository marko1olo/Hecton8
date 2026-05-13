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

## Decision 2 - Sparse 50 m Biomass SoA

Problem: The project had a 1 km cinematic population table, not a local biomass economy. A dense world-sized 50 m grid would waste memory and a GameObject ecology model would violate the frame-time mandate.
Solution: Add sparse NativeArray front/back buffers for prey and predator biomass keyed by absolute 50 m macro-cell coordinates. The player cell and four neighbors seed on demand; cold jobs operate only on active cells.
Rejected Alternatives: A full Cartography-sized dense array was rejected for memory waste. MonoBehaviour fish counters were rejected for allocations, scheduling jitter, and uncontrolled update order.
Scalability potential: Low uses local LV only; Middle/High enable diffusion; Ultra can increase tracked cell count and flora presentation without changing the contract.
Hardware Impact: On i3/MX350, the active set stays near the player and diffusion can be disabled, keeping FrostTick below the 0.1 ms suspicion line.

## Decision 3 - Non-Destructive Signal Consumption

Problem: `HectonDirectorAI` destructively drains the legacy `EntityDeathSignal` queue, so ecology could not consume deaths without racing the encounter director.
Solution: Mirror legacy `Publish(EntityDeathSignal)` and `Publish(ItemAcquiredSignal)` into typed `SignalBus<T>` lanes. `EcosystemDirector` reads frame snapshots and defers impacts into a fixed NativeArray when Burst owns the front buffers.
Rejected Alternatives: Reading the legacy queue was rejected because it would starve the encounter director. Managed event subscriptions were rejected for GC and order ambiguity.
Scalability potential: Low only pays when signals exist; High can add more biomass impact kinds without changing the queue shape.
Hardware Impact: Signal drain is bounded by snapshot count and uses no managed allocations in the hot path.

## Decision 4 - Encounter Pacing Through Contract

Problem: The encounter director spawned from stress/token state only, so predator overhunting never affected future apex pressure.
Solution: Extend `IEcosystemDirectorService` with `TryGetBiomassAvailability` and apply biomass modifiers to a copy of `EncounterThreatAuthoringSnapshot` before scheduling the Burst job.
Rejected Alternatives: Referencing `EcosystemDirector` concrete from the job was rejected because jobs must be pure data. Hardcoding biomass into `EncounterDirectorJob` without a contract was rejected as a cross-domain dependency.
Scalability potential: Low reads a single local cell; High can make the service return richer availability without touching encounter job structure.
Hardware Impact: One main-thread service query per encounter cold tick, estimated near 2 us on low-end silicon.

## Decision 5 - SaveBinaryStorage Bridge Without Signature Churn

Problem: The save writer already accepts one ecosystem record array. Adding a new section would touch `SaveManager`, recovery repair paths, indexed storage, and migration code.
Solution: Encode biomass RLE runs as marked `EcosystemSectorSaveRecord` entries in the existing ecosystem section. Values use sbyte 0-100 quantization and restore through the ecology owner.
Rejected Alternatives: A new SaveBinaryStorage section was rejected as too broad for this domain slice. Floating point biomass saves were rejected for size and determinism.
Scalability potential: Low saves mostly single-cell runs; High/Ultra can benefit from longer contiguous run lengths if the active biomass frontier grows.
Hardware Impact: Save-only cold path; no frame-time cost.

## Decision 6 - ASMDEF Isolation Blocker

Problem: The prompt requested `Hecton8.AI.Ecology -> Contracts`, but existing `Hecton8.Core` directly references `Hecton8.Ecosystem` concrete types through `GlobalRegistry`, `GameBootstrapper`, and several fauna/world systems.
Solution: Keep the new biomass API on the existing contract (`IEcosystemDirectorService`) and mark asmdef isolation blocked until concrete registry dependencies are moved to contracts.
Rejected Alternatives: Creating a new `Hecton8.AI.Ecology.asmdef` now was rejected because it would create a compile cycle or strand existing concrete references.
Scalability potential: Contract-first access still allows later asmdef extraction once registry concrete slots are decomposed.
Hardware Impact: No runtime cost; prevents a structural compile break.

## Decision 7 - Verification Boundary

Problem: Unity MCP script validation returned `no_unity_session`, and local `dotnet build Hecton8.Core.csproj` fails on pre-existing missing project references unrelated to this task.
Solution: Run filtered build output and `git diff --check`; record the compile wall instead of reporting a false green.
Rejected Alternatives: Claiming Burst verification without Unity was rejected. Reverting unrelated dependency work was rejected because the worktree is shared with other agents.
Scalability potential: Once Unity is attached, Burst can validate the LV kernel directly without further design changes.
Hardware Impact: Verification-only; no runtime impact.
