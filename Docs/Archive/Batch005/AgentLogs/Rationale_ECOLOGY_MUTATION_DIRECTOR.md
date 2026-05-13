# ECOLOGY_MUTATION_DIRECTOR Rationale

Status: PENDING VERIFICATION

## Decision 0 - Task Boundary
Problem: The batch prompt declares 19 tasks but the primary objective list only numbers 1-18.
Solution: Track 18 implementation tasks plus recursive re-verification as Task 19.
Rejected Alternatives: Treating the XML as 18 tasks would violate the declared task count and skip the anti-division recheck.
Scalability potential: Low keeps mutation checks on loaded entities only; Middle/High/Ultra can spend saved cycles on macro-swarm and richer shader twitch.
Hardware Impact: 0us runtime; prevents scope drift before code.

## Decision 1 - Mandate Set
Problem: Mutation touches genetics, hazards, AI behavior, visual fake, telemetry, save compression, and AUP safety.
Solution: Read Zero-GC, deterministic RNG, AUP, blackbox telemetry, swarm/cognition AI, save persistence, and cinematic fake mandates before editing.
Rejected Alternatives: Coding directly from prompt text would miss hot-path and persistence rules.
Scalability potential: Low uses bitmask edits and loaded-entity mutation; Ultra can layer more visible shader response without changing authority data.
Hardware Impact: Planning only; expected runtime target remains below 0.1ms per FrostTick slice on i3/MX350.

## Decision 2 - Genome Authority Boundary
Problem: Radiation mutations need cross-domain hazard data without creating a singleton genetics manager or concrete dependency chain.
Solution: Expanded `IEcosystemDirectorService` with `FaunaGenomeMutationRequest` and kept mutation ownership inside `EcosystemDirector`; `FaunaBrain` sends loaded-entity requests through the interface.
Rejected Alternatives: A new `MutationManager.Instance` would violate registry authority; direct FaunaBrain reads from all hazard systems would couple AI to world/physiology internals.
Scalability potential: Low mutates loaded fauna only; Mid/High/Ultra add headless sector and macro-swarm genome jobs.
Hardware Impact: Loaded entity path runs once per 5 seconds per active fauna; expected cost is sub-5us per entity on i3/MX350 because it is scalar sampling plus bit ops.

## Decision 3 - Bit Kernel and Save Shape
Problem: Mutation must alter 64-bit genomes with deterministic chance and survive saves without bloating every sector record.
Solution: Added `FaunaGenome64` with byte-field deltas and LCG bit-mask chance; save snapshot writes only mutated genomes as marker/detail records.
Rejected Alternatives: Division-based probability and full genome tables were rejected; both waste Burst cycles and save bandwidth.
Scalability potential: Low stores no macro/headless mutation churn; Ultra can persist many visible variants because only mutated genomes add records.
Hardware Impact: Burst job path is 0 B GC; per candidate mutation is fixed integer work, estimated below 1us per genome batch item on MX350-era CPU.

## Decision 4 - Visual Fake First
Problem: The task asks for visible spasming but physical deformation simulation would be expensive and unstable.
Solution: Expose `_FaunaMutationTwitch` as a material scalar using the existing fauna presentation material path; shader-side sine remains a visual fake.
Rejected Alternatives: Rigidbody twitch impulses and per-bone deformation would contaminate physics/cognition and burn frame time.
Scalability potential: Low can ignore the property when material lacks it; High/Ultra shaders can spend the saved CPU on stronger vertex displacement/noise.
Hardware Impact: CPU cost is only a cached material float when the value changes; no per-frame allocation.

## Decision 5 - AUP Mutation Sampling
Problem: Macro-swarm mutation candidates live as absolute biomass-cell coordinates, while radiation/brine samplers expect shifted runtime positions.
Solution: Resolve macro-swarm current cell centers through `AbsoluteUniversePosition.FromAbsolutePosition(...).ToRuntimeFloat3()` before hazard sampling.
Rejected Alternatives: Sampling raw cell coordinates would mutate the wrong swarms after floating-origin shifts; storing runtime positions in the genome would make genetics origin-dependent.
Scalability potential: Low skips macro-swarm mutation entirely; Middle/High/Ultra can mutate many swarms without violating AUP safety.
Hardware Impact: One double3 conversion per macro-swarm FrostTick candidate; estimated <3us for 32 candidates on i3/MX350.

## Decision 6 - Gameplay Consequence Routing
Problem: Mutation flags must affect behavior and meat without adding a new loot manager.
Solution: Resolve Speed/Aggression bytes into existing fauna runtime multipliers, and carry `ItemHash_ContaminatedMeat` through the corpse resource node record when mutated fauna dies.
Rejected Alternatives: Direct edits to `PredatorCognitionDomain` scoring tables and string-based loot profile swaps would widen the blast radius.
Scalability potential: Low gets contaminated node metadata and behavior changes only on loaded fauna; Ultra can render stronger mutated materials and persist more mutated swarms from the same genome bits.
Hardware Impact: Behavior path reuses existing multipliers; corpse route is one uint copy per death, 0us recurring frame cost.

## Decision 7 - Low-Tier Mutation Budget
Problem: Low tier cannot spend FrostTick budget on background macro-swarm mutation checks, but loaded fauna still need visible consequences.
Solution: Background genome jobs return immediately when `ScalabilityTierProfileByte == 0`; the registry request path only rejects low-tier requests explicitly flagged as `MacroSwarm`.
Rejected Alternatives: Disabling all mutation on low tier would violate the prompt; always mutating macro-swarms would spend cold-background CPU on entities the player cannot inspect.
Scalability potential: Low = loaded fauna only; Middle = background batches; High = wider batch caps; Ultra = full macro-swarm mutation plus shader overkill.
Hardware Impact: Saves all background mutation sampling/job scheduling on i3/MX350 low tier while keeping a 5s loaded-entity scalar path.

## Decision 8 - Telemetry Ring
Problem: Mutation state must be postmortem-debuggable without string logs or per-frame allocation.
Solution: Added a fixed 300-entry `NativeArray<FaunaMutationTelemetryEntry>` ring and binary dump path keyed to `Dump_ECOLOGY_MUTATION_DIRECTOR.bin`.
Rejected Alternatives: `Debug.Log` per mutation or managed queues would break zero-GC and drown useful state.
Scalability potential: Low writes only loaded mutation batches; Ultra writes richer batch totals without changing storage shape.
Hardware Impact: One struct write per mutation batch, estimated <2us; dump path only runs on invalid scalar detection.

## Decision 9 - Save Compression Shape
Problem: Genome mutations must persist without bloating the existing sector save records.
Solution: Emit only mutated genomes as sparse marker/detail records; unmutated genomes are omitted and regenerated from deterministic base data.
Rejected Alternatives: Full contiguous genome arrays and literal RLE over random 64-bit genomes were rejected; random mutated genomes do not produce useful runs, while sparse delta records compress to zero for unchanged sectors.
Scalability potential: Low has almost no mutated background records; Ultra can store many mutated swarms while save cost scales only with visible mutation count.
Hardware Impact: 0 bytes for unmutated genomes; two 24-byte sector records per mutated genome in the existing snapshot buffer.

## Decision 10 - Verification Boundary
Problem: `dotnet build Hecton8.Core.csproj` fails before mutation code because generated project references omit existing asmdef assemblies.
Solution: Use Unity `validate_script` for the Burst kernel and contract file, record the csproj reference wall, and keep status `PENDING VERIFICATION` per prompt.
Rejected Alternatives: Editing generated `.csproj` files or claiming full build success would create false evidence.
Scalability potential: Verification choice has no runtime effect; it prevents bad build metadata from hiding actual mutation code review.
Hardware Impact: 0us runtime; developer-time impact is isolated to the existing project-generation dependency wall.

## OMEGA POLISH CHANGES
Problem: The polish mandate required a post-completion anti-bloat pass after the 19 tasks were checked.
Solution: Extracted `<POLISH_MANDATE id="OMEGA_POLISH">`, ran diff-focused scans for managed `foreach`, `string.Format`, interpolation, `.ToString()`, `math.sqrt`, `math.normalize`, LINQ, and random/division chance. Replaced fauna overlay health normalization division with `math.rcp`.
Rejected Alternatives: Repo-wide cleanup was rejected because the worktree contains archive/vendor/other-agent noise and unrelated dirty files.
Scalability potential: Low keeps loaded-only mutation and no background mutation jobs; Middle runs batched macro/headless mutation; High/Ultra spend saved CPU on mutation shader twitch and broader swarm persistence.
Hardware Impact: Replacing `_currentHealth / _maxHealth` with `_currentHealth * math.rcp(_maxHealth)` removes one float divide from the runtime overlay path; estimated <1us saved per overlay refresh on i3/MX350.
Cinematic Cheats used: 64-bit genome byte nudges instead of physical mutation simulation; sparse save deltas instead of full genome tables; `_FaunaMutationTwitch` shader scalar instead of bone/rigidbody spasms; low-tier macro-swarm skip instead of background simulation.
Final Git Diff: task-scoped diff reports 12 files changed, 2744 insertions, 127 deletions. Name-status: `M Assets/_Project/Scripts/AI/Ecology/Migration/MacroSwarm.cs`; `M Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`; `M Assets/_Project/Scripts/Core/GlobalSignals.cs`; `M Assets/_Project/Scripts/Ecosystem/FaunaBrain.Ecosystem.cs`; `M Assets/_Project/Scripts/Ecosystem/FaunaGeneticTraits.cs`; `M Assets/_Project/Scripts/Ecosystem/FaunaGeneticsManager.cs`; `M Assets/_Project/Scripts/Fauna/FaunaBrain.cs`; `M Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs`; `M Assets/_Project/Scripts/World/DestructibleOrganicManager.cs`; `M Assets/_Project/Scripts/World/EcosystemDirector.cs`; `M Docs/AgentLogs/Rationale_ECOLOGY_MUTATION_DIRECTOR.md`; `M Docs/Tasks/Status_ECOLOGY_MUTATION_DIRECTOR.md`.

## Decision 11 - Recheck Upgrade Pass
Problem: The first pass exposed two quality gaps: mutation rolls used frame count as entropy, and the runtime wrote `_FaunaMutationTwitch` before the main fauna shader exposed the property.
Solution: Added explicit deterministic `RollIndex` fields for loaded and background mutation attempts, sanitized hazard scalars before writing mutation NativeArrays, and added `_FaunaMutationHueShift`/`_FaunaMutationTwitch` to `Hecton_LeviathanOrganic.shader` with cheap sickly hue/glow and high-frequency vertex twitch.
Rejected Alternatives: Frame-count authority was rejected because it weakens replay determinism; CPU bone deformation and physics twitch were rejected because shader motion buys the same player belief without contaminating collision or cognition.
Scalability potential: Low still uses loaded-only mutation and cheap scalar material writes; Middle/High run background mutation batches; Ultra can push stronger shader mutation parameters without changing CPU authority.
Hardware Impact: Deterministic roll fields are zero heap and one uint per request/job; finite guards are a few scalar checks per candidate; shader work is two triangle waves plus one color lerp only on fauna materials that opt into the property. Estimated CPU delta remains below 1us per loaded changed fauna and below 14us per 32-candidate FrostTick prep on i3/MX350.
Cinematic Cheats used: Shader hue/glow/twitch replaces physical spasms; bitfield-derived visual scalars replace prefab swaps; finite scalar dump preserves blackbox evidence without per-sample logs.
Verification: `git diff --check` passed with CRLF warnings only. Unity validation passed for `FaunaGenome64.cs`, `FaunaGeneticTraits.cs`, and `GlobalRegistryContracts.cs`. Fresh Unity console after clear/refresh reports unrelated errors in `PrologueReentrySignals.cs`, `GlobalDataVault.cs`, and missing `Hecton8.UI.Tools`; no mutation file or `Hecton_LeviathanOrganic.shader` error appeared. Full `dotnet build Hecton8.Core.csproj --no-restore` still fails on generated `.csproj` reference walls and unrelated missing assemblies.

## Decision 12 - Finite Authority Boundary
Problem: Loaded mutation requests could still enter authority code with a non-finite runtime position or authoring multipliers, which would let bad upstream state contaminate genome-derived runtime traits or hazard sampling.
Solution: Added a finite runtime-position guard in `TryMutateFaunaGenome`, centralized scalar sanitation in `FaunaGenome64.SanitizeScalar01`, and sanitized positive base multipliers before resolving mutated scale, speed, and health.
Rejected Alternatives: Letting Unity `Mathf.Clamp01` or shader-side clamps absorb bad values was rejected because mutation authority and the blackbox need deterministic, CPU-side finite state before any visual fake runs.
Scalability potential: Low rejects bad loaded samples cheaply and keeps loaded-only mutation stable; Middle/High/Ultra keep the same finite kernel while spending saved budget on broader macro-swarm mutation and stronger shader mutation response.
Hardware Impact: Three finite checks for runtime position plus a few scalar sanitation branches per mutation request. Estimated under 1us per loaded fauna 5s mutation tick on i3/MX350; prevents NaN propagation into NativeArray telemetry and shader property writes.
Cinematic Cheats used: Invalid-world samples are recorded through blackbox evidence instead of expensive recovery simulation; visual overkill remains shader-driven only after authority state is finite.
Verification: Prompt re-extracted from `CURRENT_BATCH.md:1036-1078`. Unity validation passed `FaunaGenome64.cs` with 0 diagnostics after the finite guard edits. Corrected static scan found no managed random, LINQ, string formatting, `.ToString()`, or division-style chance markers in the mutation patch paths. `git diff --check` reported only CRLF warnings. Unity console is currently blocked by unrelated `GlobalDataVault.cs` core-memory errors outside this domain, so project status remains `PENDING VERIFICATION`.

## Decision 13 - SRP Batcher Infection Overlay
Problem: The infection overlay still used a `MaterialPropertyBlock` on fauna geometry, which violates the current SRP Batcher rule and conflicts with the mutation shader path that already owns per-fauna runtime material state.
Solution: Removed the MPB field and property-block writes, added cached color/base-color/emission property masks to the existing fauna runtime material list, and restored original material colors from the source material when infection clears.
Rejected Alternatives: Keeping MPB for infection was rejected because it breaks the stated batching rule. Creating a separate infection material system was rejected because fauna already has an owned runtime material clone path for biolum, death, hit flash, and mutation scalars.
Scalability potential: Low keeps the same material count as the existing presentation path and avoids SRP-batcher-breaking MPB state; Middle/High/Ultra can stack infection, sickly mutation hue, glow, and twitch through the same CBUFFER-backed material properties.
Hardware Impact: No per-frame allocation. Infection changes now cost a bounded loop over the existing runtime material list only when the infection state/severity changes; expected under 1us per changed loaded fauna on i3/MX350. It avoids MPB state changes on standard geometry.
Cinematic Cheats used: Infection and mutation remain material/shader fakes, not CPU skin deformation or physics impulses.
Verification: Prompt re-extracted from `CURRENT_BATCH.md:1036-1078`. Static scan found no `MaterialPropertyBlock`, `_ecosystemPropertyBlock`, `renderer.material`, `.materials`, managed random, LINQ, string formatting, or `.ToString()` in the touched mutation/presentation paths. `git diff --check` reported CRLF warnings only. Unity MCP refresh timed out and later pings failed, so Unity console validation is absent for this pass. `dotnet build Hecton8.Core.csproj --no-restore` still fails on known generated-project missing-reference walls (`Fluids`, `CCD`, audio propagation/echolocation, `MacroSwarm`, `BrineLayerSample`, etc.). Status remains `PENDING VERIFICATION`.

## Decision 14 - Loaded Fauna Service Cache
Problem: Loaded fauna mutation and disease checks still read `GlobalRegistry.EcosystemDirector` directly in the slow-tick ecology path.
Solution: Added cached `IEcosystemDirectorService` and concrete `EcosystemDirector` references refreshed on enable/spawn and cleared on disable/despawn; slow-tick mutation now uses the cached interface service and refreshes only when absent or uninitialized.
Rejected Alternatives: Expanding `IEcosystemDirectorService` with corpse-disease methods was rejected for this pass because it would widen a public contract during an active multi-agent batch. Caching only the concrete type was rejected because loaded mutation authority is already available through the interface.
Scalability potential: Low keeps loaded-only mutation with fewer registry lookups; Middle/High/Ultra keep the same cache while broader macro-swarm mutation runs through the director-owned jobs.
Hardware Impact: Removes repeated registry property reads from the common loaded-fauna ecology refresh when the service is stable. Expected saving is under 1us per loaded fauna slow tick on i3/MX350, but it reduces cadence jitter and keeps the mutation request path closer to two-stage dependency caching.
Cinematic Cheats used: No new simulation; this protects the existing shader/material mutation fake by keeping authority lookup cheaper and more predictable.
Verification: Prompt re-extracted from `CURRENT_BATCH.md:1036-1078`. Static scan remained clean for `MaterialPropertyBlock`, `_ecosystemPropertyBlock`, `renderer.material`, `.materials`, managed random, LINQ, string formatting, and `.ToString()` in touched mutation/presentation paths. `git diff --check` reported CRLF warnings only. Unity MCP reports no active Unity session, so editor validation is absent. Local `dotnet build Hecton8.Core.csproj --no-restore` still fails on existing missing assemblies; the touched `FaunaBrain.cs` still appears only with its pre-existing `Hecton8.Physics.CCD` missing-reference wall.
