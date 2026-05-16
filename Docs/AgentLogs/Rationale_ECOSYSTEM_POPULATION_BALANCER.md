# ECOSYSTEM_POPULATION_BALANCER Rationale

## Initial Scope

Problem: Batch prompt requires Lotka-Volterra enforcement over active entities, but the requested `Assets/_Project/Scripts/AI/Ecosystem/` folder is absent on disk. Existing surfaces are `Assets/_Project/Scripts/Ecosystem`, `Assets/_Project/Scripts/World/EcosystemDirector.cs`, and `Assets/_Project/Scripts/AI/Ecology/Migration`.

Solution: Inspect existing contracts first, then implement through the narrowest data-only AI/Ecology surface that can write entity AUP/flags or consume existing director/state buffers without `Instantiate`/`Destroy`.

Rejected Alternatives: Do not move the existing `World/EcosystemDirector.cs`; relocation is architecture drift and unsafe under 20+ concurrent agents. Do not create a detached balancer that cannot reach the active entity buffers; that would be fake compliance.

Scalability potential: Low uses 1Hz ColdTick and invisible/unloaded chunk culling only. Middle runs full local biomass clamps. High permits Tier 1 flee-down state before cull. Ultra keeps richer telemetry and more precise sector diagnostics while preserving gameplay cost caps.

Hardware Impact: Target i3/MX350; expected active-entity flag pass stays bounded and cold cadence. Estimated saving is workload-dependent; no profiler proof yet, so status is PENDING VERIFICATION.

## Loop 1 - Purge, Coefficients, AUP Sectoring

Problem: The assignment names `Ecosystem_Coefficients.json`, while the actual baked payload on disk is `Data/Precomputed/ecosystem_coefficients.json`. It also requires no private ecology arrays under the later H-Phi/DataVault mandate.

Solution: Load the JSON once on boot, sanitize it, and store it in `BufferID.EcosystemPopulationCoefficients` owned by `SystemID.AIEcology`. Sector state, cull events, telemetry, counters, and free-list storage also use DataVault buffer IDs. The runtime keeps only `VaultBufferHandle<T>` fields and a job handle.

Rejected Alternatives: Rejected hardcoding the coefficients into inspector fields because OSHINO's bake already exists. Rejected private persistent `NativeArray<T>` fields because they violate data sovereignty and complicate defrag/relocation. Rejected touching `World/EcosystemDirector` internals because its biomass arrays are not exposed through a stable interface.

Scalability potential: Low/Toaster uses 1 Hz ColdTick and Tier 2 frozen-only culls. Middle uses sector biomass from `IEcosystemDirectorService` where available. High flags Tier 1 entities with `Flag_EcologyFleeDown` instead of vanishing. Ultra can consume the same flags for richer SDF dive presentation without changing the population kernel.

Hardware Impact: JSON I/O is one cold boot read. Runtime math is a bounded sector/entity pass at 1 Hz; expected i3/MX350 cost is under the 0.1 ms frame budget because the work is off the render tick and completes in the dispatcher swap window. Exact microseconds are not claimed without profiler capture.

Problem: Active entity flags are shared with loot. Clearing bit 0 blindly would deactivate non-ecology pickups.

Solution: Ecology culling requires `Flag_IsActive | Flag_IsPrey | Flag_Tier2Frozen`; ecology ownership bits live in high flag bits and leave loot's low bits alone.

Rejected Alternatives: Rejected clearing every active entity in an overpopulated sector. That would be fast but corrupt cross-domain data and violate signal-lane segregation.

Scalability potential: Low devices get invisible freezer culls; high devices can render flee-down for loaded Tier 1 entities because the active flag remains set until a presentation consumer hides them.

Hardware Impact: The extra bitmask guard is one integer test per candidate and prevents expensive repair of cross-domain data corruption.

Problem: `AbsoluteUniversePosition` has no public `SectorHash` field despite the prompt wording.

Solution: Compute a deterministic 1 km macro-sector hash from AUP grid/local coordinates using integer packing. This keeps sectoring Burst-safe and independent of runtime world-origin shifts.

Rejected Alternatives: Rejected runtime `Vector3` position as the authority for cull sectoring because origin shifts and float precision make it weaker than AUP.

Scalability potential: The hash is device-neutral and deterministic across PC, Android/Quest, Steam Deck, and Mac.

Hardware Impact: Two double additions, two floors, and a pack per ecology entity on ColdTick; no hot-frame cost.

## Loop 2 - SoA Culling, Signal Lane, Visual States

Problem: Ecology enforcement must touch `GlobalDataVault.EntityAUPs` and `EntityFlags` directly without object lifetimes, while avoiding cross-domain damage to loot and other entity lanes.

Solution: The Burst job scans SoA buffers by index. It only clears `Flag_IsActive` for entries carrying ecology prey and Tier 2 frozen bits in the matching AUP sector, then writes the same index into a DataVault free ring.

Rejected Alternatives: Rejected `Destroy(gameObject)`, `Instantiate`, managed registries, and per-entity MonoBehaviour callbacks. Rejected clearing active flags for non-ecology entities.

Scalability potential: Low uses the cheapest invisible data cull. Middle/High/Ultra can consume the same free-list and flee-down flags for richer presentation without changing the population math.

Hardware Impact: Sequential SoA access is cache-friendly on PC and ARM64. Exact microseconds are not claimed until Burst profiler capture.

Problem: Ecology deaths already have a first-party signal type. Creating a new signal would duplicate the lane and split biomass consumers.

Solution: Reuse `EntityDeathSignal` and push through `SignalBus<EntityDeathSignal>.Push` with `SourceHash = ECOL`. `World/EcosystemDirector` already drains `ReadOnlySpan<EntityDeathSignal>` for native biomass impact.

Rejected Alternatives: Rejected managed delegates, legacy EventBus-style fanout, and a new ecology death DTO.

Scalability potential: One typed lane remains deterministic across platforms; high-end presentation can listen to existing snapshots without extra allocation.

Hardware Impact: One fixed payload enqueue per cull event. Event count is bounded by the DataVault cull-event buffer.

Problem: Loaded Tier 1 entities cannot vanish without visual debt, but the population kernel cannot own visual SDF animation.

Solution: For Tier 1 prey, set `Flag_EcologyFleeDown` and leave active state intact. Tier 2 frozen prey are culled immediately because they are unloaded/invisible.

Rejected Alternatives: Rejected spawning VFX or controlling transforms from the balancer; that would cross presentation ownership and violate the prompt's data-only signal rule.

Scalability potential: Toaster mode gets no visible work. High/Ultra can attach SDF dive, particles, and richer biome response to the flag later.

Hardware Impact: Tier 1 path is one flag write, not an animation simulation.

## Loop 3 - Stability, Bounds, Blackbox

Problem: Population math divides by capacity and biomass-per-entity. Non-finite biomass would poison counters and could propagate into telemetry.

Solution: Clamp biomass to `[0, MaxCapacity]`, guard all reciprocal denominators with `math.max(1f, value)`, sanitize coefficients before use, and set an invalid-math counter when a non-finite next biomass is recovered.

Rejected Alternatives: Rejected trusting offline coefficients alone; runtime biomass can still be corrupted by save data, player actions, or external producers.

Scalability potential: Same scalar clamps run on all devices. There is no shader or GPU dependence, so Android/Quest, Mac/Metal, Steam Deck, and PC behave identically.

Hardware Impact: Clamp/finite guards add scalar ALU on a 1 Hz job. Exact microseconds are not claimed until profiler capture.

Problem: Critical ecology state needs postmortem evidence without managed growth or strings in the hot path.

Solution: Keep a 300-entry `EcosystemPopulationTelemetryEntry` ring in `GlobalDataVault`; on invalid math, dump it to `Docs/AgentLogs/Dump_ECOSYSTEM_POPULATION_BALANCER.bin`.

Rejected Alternatives: Rejected Debug.Log spam and managed lists because they allocate and do not survive crash analysis cleanly.

Scalability potential: Low devices keep the same fixed-size ring. High-end builds can inspect richer telemetry by consuming the ring without changing runtime storage.

Hardware Impact: One fixed telemetry write per ColdTick. Dump cost only occurs on fault.

Problem: The Loop 3 compile verification became blocked by external `LaserCutter.cs` edits after Loop 2 had already compiled cleanly.

Solution: Do not edit or revert `LaserCutter.cs`; it is outside this assignment. Record the external compile wall and continue the task loop because owned files had a clean Loop 2 build after the last owned code change.

Rejected Alternatives: Rejected killing shared `csc` processes or repairing another agent's incomplete LaserCutter event-buffer work from this ecology task.

Scalability potential: No runtime change.

Hardware Impact: No runtime change.

## Loop 4 - Stress Cull, Player Impact, Reuse

Problem: The first stress path reused the Lotka-Volterra prey cull target only. That satisfies population correction, but it does not answer the explicit homeostasis requirement to shed Tier 2 cost when `SystemStress01 > 0.8`.

Solution: Split the job into two bounded passes per sector: normal Lotka-Volterra culls still target Tier 2 prey only, while the stress pass can cull any active Tier 2 ecology-owned entity carrying prey or predator kind bits. Non-ecology lanes remain protected by the high-bit ecology mask.

Rejected Alternatives: Rejected clearing every Tier 2 active flag in the shared `EntityFlags` buffer because loot and other systems share low bits. Rejected moving the cull into GameObject lifetime code because the prompt forbids `Destroy(gameObject)`.

Scalability potential: Low devices get aggressive invisible Tier 2 shedding under pressure. Middle keeps predator/prey balance through the primary LV pass. High and Ultra still preserve Tier 1 entities for flee-down presentation instead of visible popping.

Hardware Impact: Stress adds a second sequential SoA scan only when pressure exceeds 0.8 and only at ColdTick cadence. Runtime microseconds are unmeasured without profiler capture; Loop 4 build wall-clock was blocked externally after 53,720,000 us reported by `dotnet build`.

Problem: Player mining and killing must alter biomass natively without inventing a duplicate signal or writing private biomass arrays from the balancer.

Solution: Reuse the existing `World/EcosystemDirector` native biomass path. It drains `ReadOnlySpan<EntityDeathSignal>` and `ReadOnlySpan<ItemAcquiredSignal>` from typed `SignalBus<T>` snapshots, and the balancer emits `EntityDeathSignal` with the ecology source hash.

Rejected Alternatives: Rejected a new ecology biomass signal and rejected direct writes into `EcosystemDirector` private biomass arrays from this domain.

Scalability potential: One biomass consumer owns the grid on all hardware tiers. High-end visual response can layer on the same signal snapshots without changing the balancer.

Hardware Impact: No additional player-impact scan was added. The balancer contributes bounded death events only for culls it already performed.

Problem: New spawns cannot allocate new objects or rely on `Instantiate`; they must reuse dead indices.

Solution: Store culled indices in `BufferID.EcosystemPopulationFreeRing`. Prey respawns reactivate valid prey slots in-place by setting `Flag_IsActive` and clearing cull/free/flee bits. Emergency predator culls are recorded as valid free slots but are not reclassified into prey slots, avoiding archetype corruption.

Rejected Alternatives: Rejected spawning fresh GameObjects, managed queues, and reusing non-prey presentation slots as prey without a dedicated archetype remap contract.

Scalability potential: Low devices amortize population recovery through index reuse. High/Ultra can attach richer presentation to reactivation while the data path stays constant.

Hardware Impact: Free-ring scan is bounded by the DataVault ring capacity and ColdTick cadence. Runtime microseconds are unmeasured until profiler capture.

