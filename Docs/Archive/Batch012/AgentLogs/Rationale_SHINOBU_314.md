# SHINOBU_314 Rationale

## Preflight
Problem: Carrion decay must replace delayed `Object.Destroy` creature cleanup without adding a new global manager or unmanaged memory island.
Solution: Use existing Ecosystem domain surfaces and DataVault-owned buffers. Integrate through isolated ecosystem files or partials only after source scan proves the correct owner.
Rejected Alternatives: A standalone `HectonCarrionManager` would duplicate lifecycle authority and increase compile-wall risk; direct AI-domain class references would couple sibling domains.
Scalability potential: Low uses low-frequency linear/cheap decay approximation; Middle uses mixed decay; High and Ultra use higher fidelity exponential weighting plus richer editor/debug presentation.
Hardware Impact: MX350/i3 path keeps recurring work on Frost/Slow tick and bounded NativeArray jobs; expected hot-frame managed allocation = 0 B.

Problem: Nutrient injection must use AUP without float precision loss.
Solution: Subtract the grid origin AUP in double precision before casting the local delta to `float3` for index calculation.
Rejected Alternatives: Casting corpse AUP directly to `float3` before subtraction would break at map edges.
Scalability potential: Same truth path on Low/Middle/High/Ultra; only cadence/math fidelity scales.
Hardware Impact: Three double subtracts per active corpse on low-frequency tick are cheaper than corrupting spatial authority and later correcting broad ecosystem state.

Problem: Scavenger attraction can become a custom AI feature sink.
Solution: Use the Dear Lie: represent carrion as high-value, zero-velocity food in spatial hash style records so existing utility queries can treat it like prey/food.
Rejected Alternatives: Dedicated carrion AI scripts and per-scavenger polling would add coupling and hot-loop cost.
Scalability potential: Low emits fewer/cheaper attraction records; High/Ultra can expose richer debug/gizmo values without changing gameplay truth.
Hardware Impact: Spatial record write is O(active carrion) at Frost cadence; avoids per-agent managed queries.

## Implementation Decisions
Problem: The prompt requires clearing a `FaunaStateDTO` active flag, but repository archaeology did not expose a canonical runtime `FaunaStateDTO` in the Echelon 3 surface.
Solution: Add a carrion-owned unmanaged `FaunaStateDTO` mirror buffer as a dependency seam, keyed by `EntityHash`, while consuming the real first-party `EntityDeathSignal` lane for truth ingress. The job clears matching mirror rows and never calls `Object.Destroy`.
Rejected Alternatives: Directly coupling to `FaunaBrain` or `ShinobuEcosystemBalancer.AmbientEntityDTO` would bind carrion to sibling presentation/ambient systems and create compile-wall risk. Inventing a new death signal was rejected because `EntityDeathSignal` already carries AUP, entity hash, source hash, intensity, and flags.
Scalability potential: Low/Middle use the same flat carrion buffer with fewer active attractions; High/Ultra can increase mock/event density and editor visualization without changing the truth route.
Hardware Impact: Bounded 512-signal ingress plus 5000-slot linear slot search at Frost cadence is cheaper than Transform hierarchy teardown; estimated avoided delayed-destroy hierarchy cleanup is 200-2000 us per corpse cluster on i3/MX350 depending on child count.

Problem: `CarrionStateDTO` needed the mandated first 40 bytes plus runtime age/loss/flags to satisfy Tasks 07-10.
Solution: Preserve mandated offsets exactly through `ToxicityEmissionRate@36`, keep the 64-byte envelope, and use the tail 24 bytes for `AgeSeconds`, `BiomassLostLastTick`, `DecayRate`, `Flags`, and `EntityHash` instead of opaque padding. The self-audit verifies the required offsets and size.
Rejected Alternatives: Encoding active state by `CurrentBiomass > epsilon` only would make Task 08's active-flag clear ambiguous and force extra comparisons into consumers. A second flag array would add another buffer and memory route for one fact.
Scalability potential: Low/Middle/High/Ultra share the same DTO layout; only math fidelity and attraction density scale with `GlobalQualityWeight`.
Hardware Impact: One 64-byte state per corpse keeps linear scans cache predictable; no object references, no managed list traversal, no GC pressure.

Problem: Parallel nutrient injection would race if every corpse wrote directly into the same `float* Injection` cell from an `IJobParallelFor`.
Solution: Split decay and injection. `CalculateBiomassDecayJob` is parallel and writes only its own state slot; `InjectCarrionNutrientsJob` is a deterministic single job that folds biomass losses into the nutrient injection grid and writes bounded attraction records.
Rejected Alternatives: Atomic float adds are not available in this Unity/Burst surface and per-cell accumulation buffers would add another Vault route and clear pass.
Scalability potential: Low devices keep deterministic single-pass injection; higher tiers still benefit from parallel decay and can spend saved cycles on richer debug surfaces.
Hardware Impact: Avoids same-frame race fixes and hidden completes; expected cost is O(active carrion) once per Frost tick, not per frame.

Problem: `FaunaBrain.Die()` already avoided `Object.Destroy`, but local predation/death paths could bypass the shared `EntityDeathSignal` lane consumed by carrion decay.
Solution: Add `PublishCarrionDeathSignal()` at the death edge and add `EntityHash` dedupe inside `ProcessEntityDeathJob` before allocating a new carrion slot.
Rejected Alternatives: Directly writing carrion DTOs from `FaunaBrain` would couple fauna presentation to ecosystem nutrient ownership; creating a new signal lane would duplicate `EntityDeathSignal`.
Scalability potential: Low/Middle/High/Ultra keep one truth route; only downstream attraction count and decay math fidelity scale with `GlobalQualityWeight`.
Hardware Impact: One death-edge signal write is cold and replaces possible duplicate DTO growth; on i3/MX350 it prevents repeated 64B carrion rows and extra Frost-tick scan cost per duplicate death.

Problem: External Burst job structs reference carrion telemetry/default constants through `NutrientDriftRuntime`; private constants would fail compile outside the partial class.
Solution: Promote the required constants to public const fields and route default tuning/sanitization through those central values.
Rejected Alternatives: Duplicating literals in job structs would keep source compiling but split tuning truth and invite drift.
Scalability potential: Same constants feed Low/Middle/High/Ultra curves; designers still override via CSV/tuner without changing DTO layout.
Hardware Impact: Compile-time constants remain inlined; no runtime cost, no lookup, no allocation.

Problem: Low quality decay still evaluated `math.exp`, and the stored `DecayRate` was overwritten with temperature-multiplied decay, causing possible multiplier compounding across Frost ticks.
Solution: Use `math.step(0.4f, GlobalQualityWeight)` as an exp gate, skip `math.exp` below threshold, blend with `math.smoothstep(0.4f, 0.95f)`, and preserve the base decay rate in the DTO after each tick.
Rejected Alternatives: Always computing exponential and selecting the result was rejected because it saves no ALU on mobile. Storing effective decay was rejected because warm/cold samples would multiply repeatedly over time.
Scalability potential: Low collapses to one linear subtraction; Middle blends linear/exponential; High/Ultra pay exact exponential for organic rot without route or layout changes.
Hardware Impact: On i3/MX350 or thermally constrained mobile, low-quality carrion avoids one `exp` per active corpse per Frost tick; for 5000 corpses that is 5000 transcendental evaluations removed.

Problem: `EntityDeathSignal` has separate `EntityHash` and `SourceHash`; using `EntityHash` as species made Fauna-owned deaths lose species-specific decay profiles.
Solution: Reserve `EntityDeathSignal.FlagFaunaBrainCarrion` for Fauna-owned carrion signals. `FaunaBrain` writes species into `SourceHash`; carrion ingress resolves `OriginalSpeciesHash` from `SourceHash` only when that flag is present. `ProcessEntityDeathJob` skips unflagged duplicate signals when a row for the same `EntityHash` is already active, preserving the more authoritative Fauna species profile. The flag is now declared in the 64B signal contract without changing payload layout.
Rejected Alternatives: Directly referencing `NutrientDriftRuntime` from `FaunaBrain` or writing carrion Vault rows from fauna was rejected as cross-domain compile-wall coupling.
Scalability potential: Species profile selection is stable across Low/Middle/High/Ultra; quality changes math fidelity, not identity.
Hardware Impact: No recurring cost; one extra bit test per death signal prevents profile miss and bad toxin/nutrient tuning.

Problem: The Fauna/Ecosystem carrion route still carried a local magic scalar for death-signal ownership, creating drift risk between producer and consumer.
Solution: Add `FlagFaunaBrainCarrion` to `EntityDeathSignal` and consume that central constant from both `FaunaBrain` and `NutrientDriftRuntime_Carrion`. This is a contract-only Core edit; explicit struct size and offsets remain unchanged.
Rejected Alternatives: Keeping mirrored local constants was rejected because it hides a cross-domain ABI rule outside the signal payload owner.
Scalability potential: Low/Middle/High/Ultra identity route remains invariant; only math cadence/fidelity scales.
Hardware Impact: Compile-time const, 0 runtime cost; removes one class of profile-routing regression.

Problem: The mock mass-extinction harness used a deterministic FNV-style hash for biomass/toxicity variation, but the domain mandate requires `Unity.Mathematics.Random` for deterministic state-affecting randomization.
Solution: Seed `Unity.Mathematics.Random.CreateFromIndex` from `math.hash(seed,index)` inside `GenerateMockMassExtinctionJob`, then derive mock entity hash, vertical jitter, biomass multiplier, and toxicity from that unmanaged RNG.
Rejected Alternatives: Keeping the custom hash was deterministic but failed the explicit RNG contract; `UnityEngine.Random` was rejected as non-deterministic and managed/engine-coupled.
Scalability potential: Low/Middle/High/Ultra mock density remains bounded by requested count and carrion capacity; random distribution does not alter runtime truth layout.
Hardware Impact: Cold/editor stress harness cost only; no gameplay hot-frame allocation or branchy managed RNG.

Problem: Carrion death publishing in `FaunaBrain` briefly resolved entity identity through `CombatDamageRuntime.ResolveTargetId`, which made the carrion route lean on an unrelated Gameplay runtime helper.
Solution: Use the existing fauna-local `ResolveStableFaunaHash` with a dedicated carrion salt to generate the `EntityDeathSignal.EntityHash`; species identity remains in `SourceHash` under `EntityDeathSignal.FlagFaunaBrainCarrion`.
Rejected Alternatives: Depending on CombatDamage target IDs was rejected because carrion identity is an ecosystem death-afterlife fact, not a combat routing fact.
Scalability potential: Stable entity identity is invariant across Low/Middle/High/Ultra; quality only affects decay math and attraction capacity.
Hardware Impact: One `math.hash` call at death edge, no registry lookup, no scene search, no new domain dependency.

Problem: After moving mock mass-extinction rows to per-index RNG, the counter row still estimated `TotalActiveBiomass` from index 0 biomass, making stress telemetry seed-dependent and noisy.
Solution: Use the expected mean of the configured multiplier range `(0.35+2.25)/2 = 1.3` times `DefaultBiomass*count` for the mock counter. The real active-biomass counter is recomputed by the decay job on subsequent ticks.
Rejected Alternatives: A parallel reduction for the cold mock harness was rejected because it adds an extra job/buffer route just to initialize a diagnostic counter.
Scalability potential: Low/Middle/High/Ultra mock harness remains bounded; telemetry starts from a stable deterministic estimate.
Hardware Impact: One multiply at index 0, no extra memory pass.

Problem: Carrion telemetry used the parent nutrient solver schedule-to-finalize time, so `CarrionFaultBudgetMicros` could include unrelated nutrient work.
Solution: Record `_carrionScheduleTicks` immediately before scheduling the carrion death/decay/injection/telemetry subchain and patch `CarrionTelemetryEntry.BurstExecutionMicroseconds` from that subchain window.
Rejected Alternatives: A separate timing job or profiler readback was rejected because it would add synchronization or editor-only dependency to runtime scheduling.
Scalability potential: Low/Middle/High/Ultra telemetry remains comparable; quality changes workload, not the timing route.
Hardware Impact: One timestamp read per Frost tick, no hot allocation, no job dependency change.

Problem: The carrion partial still imported `Hecton8.Gameplay` after removing combat target routing, leaving a stale compile-wall edge in the new file.
Solution: Remove the unused Gameplay import; carrion code uses Core signals/contracts, Vault memory, and the existing World spatial hash API.
Rejected Alternatives: Keeping the import was rejected because stale using directives hide unnecessary assembly edges during batch integration.
Scalability potential: No runtime behavior change.
Hardware Impact: 0 runtime; compile hygiene only.

Problem: The editor scanner wrote a full JSON object to the shared `AI_OPTIMIZATION_REPORT.json`, which could erase proof sections from other agents.
Solution: Keep the stable SHINOBU_314 report as a full object and upsert only the `shinobu314CarrionDecay` property into the aggregate report using a local brace scanner.
Rejected Alternatives: Adding JSON.NET/Roslyn or overwriting the aggregate report was rejected as dependency churn and multi-agent evidence loss.
Scalability potential: Editor-only proof path; runtime devices pay zero.
Hardware Impact: No runtime impact. Editor scan remains linear file I/O with no new package dependency.

Problem: The scanner task asks for AST proof, but adding Roslyn to editor assemblies would mutate asmdef dependencies during a 20-agent batch.
Solution: Implement `OOP_Destroy_Scanner` as an editor-only comment/string-stripped structural syntax scanner and write both stable and aggregate JSON report entries.
Rejected Alternatives: Adding Roslyn packages or asmdef references was rejected as compile-wall churn for a scanner that only needs static proof of delayed `Destroy` and `WaitForSeconds` corpse timers.
Scalability potential: Scanner cost is editor-only; runtime devices pay zero.
Hardware Impact: No runtime impact. Editor scan is linear file I/O and does not affect player frame time.

Problem: The stable SHINOBU_314 JSON report carried richer proof fields than the editor scanner would regenerate, so a future Unity menu run could erase timing/RNG/mock-counter evidence.
Solution: Extend `OOP_Destroy_Scanner` JSON emission with the same carrion route, deterministic RNG, mock biomass estimate, telemetry timing, DataMonolith pending marker, and editor facade paths used by the stable report.
Rejected Alternatives: Leaving the scanner as a narrow violation counter was rejected because generated proof would drift from the on-disk forensic record. Running Unity to regenerate the report now was rejected because compile/build execution remains CPU-gated.
Scalability potential: Editor-only evidence generation; runtime Low/Middle/High/Ultra paths unchanged.
Hardware Impact: 0 runtime cost; editor scan writes a larger JSON object only when manually invoked.

Problem: Fauna-owned carrion signals carry numeric `ComputeStableSpeciesId()` values, but the first CSV bridge keyed profiles by FNV hashes of authoring names. That made species-specific decay profiles silently miss unless a designer guessed the internal hash route.
Solution: Make `CarrionDecayCsvParser` parse `species_key` as `default`, decimal speciesID, `0x` hash, or token FNV. `FindProfile` now returns the `default` row keyed by `CarrionRouteHash` when no exact species match exists, and CSV reload clears stale profile rows before parsing.
Rejected Alternatives: Changing `FaunaBrain` to hash ScriptableObject names was rejected because fauna identity is already numeric across cognition, migration, save, and spatial systems. A direct FaunaSpeciesProfile dependency from Ecosystem was rejected as cross-domain coupling.
Scalability potential: Low/Middle/High/Ultra use the same identity route; designers can tune generic, known numeric species, or hashed content tokens without C# recompilation.
Hardware Impact: Cold CSV parse only. Runtime profile lookup remains a bounded 64-row linear scan on Frost cadence with default fallback.

Problem: Active carrion with corrupted AUP, biomass, decay, age, or toxicity could retire late or leave a stale fault word, creating noisy telemetry and a possible NaN injection path.
Solution: Add `CarrionStateDTO.FlagMathFault` inside the existing 64B flag field. `ProcessEntityDeathJob` sanitizes biomass scale, default biomass, epsilon, decay rate, and toxicity seed before writing a DTO. `CalculateBiomassDecayJob` validates finite state before any decay math, retires invalid rows, and marks the DTO. `InjectCarrionNutrientsJob` clears `ShinobuCarrionFaultFlags` at tick start, consumes `FlagMathFault` into the current telemetry word, sanitizes nutrient/attraction scalars, and clears the row fault after one proof sample.
Rejected Alternatives: Writing shared fault flags directly from the parallel decay job was rejected because it would add a contested cache line and data race. Adding a separate fault queue was rejected as another Vault lane for a fact already expressible in the DTO flags.
Scalability potential: Low/Middle/High/Ultra use identical truth-state fault semantics; quality still scales decay fidelity only.
Hardware Impact: Adds finite branch checks on active corpses at Frost cadence; avoids NaN propagation into the nutrient grid/spatial hash and prevents stale fault dumps on i3/MX350-class devices.

Problem: Compile verification became available after the CPU gate briefly dropped, but `Hecton8.Core.csproj` is shared by many active agents.
Solution: Run only the targeted Core build with `--no-restore`, then stop at the first external compile wall. The emitted errors are outside SHINOBU_314 files and reference missing/renamed DTO or config symbols in predator acoustic SDF, radiation, VR somatic comfort, and SHINOBU_315 hand IK routes.
Rejected Alternatives: Fixing those cross-domain files here was rejected as ownership breach. Reverting carrion patches was rejected because the build produced no carrion-file error before the external wall.
Scalability potential: No runtime impact; this is integration evidence.
Hardware Impact: One guarded compile attempt consumed the safe slot; further rebuild attempts are blocked because `dotnet` is now active and CPU sampled high again.
