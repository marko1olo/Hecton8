# ECOLOGY_MUTATION_DIRECTOR Log

## 2026-05-13 - 64-Bit Radiation Mutations
What was wrong:
- Existing 64-bit fauna genetics were static after spawn; radiation/toxicity/brine did not mutate live, headless, or macro-swarm fauna.
- Mutation consequences were not routed to behavior, death yield metadata, save persistence, event bus, or postmortem telemetry.
- Macro-swarm hazard sampling risked using absolute biomass-cell coordinates as runtime positions after floating-origin shifts.

What was done:
- Added `FaunaGenome64` with 64-bit byte fields for Size, Speed, Aggression, Hue, and high mutation flags.
- Added Burst-safe headless and macro-swarm mutation jobs using fixed `NativeArray` SOA lanes.
- Extended `IEcosystemDirectorService` with `FaunaGenomeMutationRequest` and kept mutation authority inside `EcosystemDirector`.
- Sampled radiation, toxicity, and brine through existing registry/signal-backed systems; no singleton mutation manager added.
- Routed loaded fauna through 5s mutation requests, then reapplied speed, aggression, health, scale, hue, twitch, and contaminated yield traits.
- Added low-tier skip for background macro/headless mutation while keeping loaded-entity mutation active.
- Added `_FaunaMutationTwitch` presentation scalar as a visual fake for spasming instead of physics/bone simulation.
- Added contaminated meat hash propagation to corpse resource node metadata.
- Added `FaunaStateChangedSignalKinds.Mutated` and mutation signal publishing for loaded and batch mutations.
- Added sparse genome save/restore marker records for mutated headless sectors and macro-swarms.
- Added fixed 300-entry fauna mutation blackbox ring and binary dump path `Docs/AgentLogs/Dump_ECOLOGY_MUTATION_DIRECTOR.bin`.
- Ran OMEGA polish and replaced one runtime health normalization division with `math.rcp`.

Cinematic Cheats used:
- Byte-level genome nudges instead of simulating biological mutation.
- LCG + bitmask chance instead of floating probability division.
- Sparse save deltas instead of a full genome table.
- Shader twitch scalar instead of physical spasms.
- Low-tier macro-swarm skip with loaded-only mutation for visible payoff.

Exact microseconds saved / estimated:
- No mutation singleton: 0us/frame manager overhead avoided.
- LCG bitmask chance: estimated <1us per 32-genome Burst slice; avoids divide/probability work.
- Low-tier macro-swarm skip: saves the full background mutation prep/job schedule on low tier, estimated <14us per FrostTick candidate slice for 32 candidates on i3/MX350.
- Shader twitch fake: avoids bone/rigidbody deformation; CPU cost is a cached material scalar update, estimated <1us per changed loaded fauna.
- Sparse genome persistence: 0 bytes for unmutated genomes; two sector records per mutated genome.
- Blackbox telemetry: one struct write per mutation batch, estimated <2us; binary dump only on invalid scalar.
- OMEGA rcp cleanup: one runtime float divide removed from fauna overlay refresh, estimated <1us per overlay refresh.

Verification:
- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` by CLI at least five times, scoped to `ECOLOGY_MUTATION_DIRECTOR`.
- Mandates read from `.agents-skills`: Zero-GC, deterministic RNG, AUP, blackbox, swarm AI, cognition AI, save persistence, cinematic cheat.
- `FaunaGenome64.cs` Unity validation passed with 0 diagnostics before later MCP timeouts.
- `GlobalRegistryContracts.cs` Unity validation passed with 0 diagnostics.
- Diff-focused scans found no added managed `foreach`, string formatting/interpolation, `.ToString()`, LINQ, `math.sqrt`, or `math.normalize` in the mutation patch.
- Recursive recheck confirmed random chance uses `rng & chanceMask`; no division-based chance.
- `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by generated `.csproj` reference failures for existing asmdef assemblies (`Hecton8.Environment.Fluids`, `Hecton8.AI.Ecology.Migration`, `Hecton8.Core.Scheduling`, `Hecton8.Physics.CCD`, audio propagation/echolocation, etc.). Status remains `PENDING VERIFICATION` as required.

## 2026-05-13 - Mutation Recheck Upgrade Pass
What was wrong:
- Mutation chance entropy still depended on frame count in the first implementation path.
- The main first-party fauna shader did not expose `_FaunaMutationTwitch`, so visible mutation could silently degrade to metadata only.
- Hazard sample scalars needed a finite guard before being written into mutation NativeArrays.

What was done:
- Added deterministic `RollIndex` to `FaunaGenomeMutationRequest` and mutation jobs.
- Loaded fauna now increments a local mutation roll counter at the 5s cadence; background mutation uses an ecology-owned epoch instead of frame count.
- Added `MutationHueShift01` to fauna traits and resolved it from mutation flags.
- Wired `_FaunaMutationHueShift` and `_FaunaMutationTwitch` through `FaunaBrain` cached shader IDs and runtime material masks.
- Added `_FaunaMutationHueShift` and `_FaunaMutationTwitch` to `Hecton_LeviathanOrganic.shader`.
- Added cheap shader-side sickly hue/glow and triangle-wave vertex twitch.
- Added finite scalar sanitation for radiation, toxicity, and brine before mutation arrays are written; invalid scalar detection now pushes the existing blackbox dump path once per frame.

Cinematic Cheats used:
- Shader color/glow/twitch sells mutation without physics impulses or bone deformation.
- Deterministic uint roll indices preserve gameplay authority without floating probability or wall-clock/frame authority.
- Finite scalar dump uses the existing 300-entry blackbox instead of string logs.

Exact microseconds saved / estimated:
- Removing frame-count entropy does not change frame cost; it removes nondeterministic replay variance.
- Shader visual upgrade costs no CPU beyond cached material scalar changes, estimated <1us per changed loaded fauna.
- Finite scalar checks add a few scalar branches per mutation candidate; expected under 1us for loaded fauna and within the existing <14us FrostTick prep estimate for 32 candidates on i3/MX350.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md:1036-1078`.
- `git diff --check` passed with CRLF warnings only.
- Static scan found no `Random.Range`, `UnityEngine.Random`, `System.Random`, LINQ, `string.Format`, or `.ToString()` in the mutation patch paths.
- Unity validation passed for `FaunaGenome64.cs`, `FaunaGeneticTraits.cs`, and `GlobalRegistryContracts.cs`.
- Large-file MCP validation for `FaunaBrain.cs` and `EcosystemDirector.cs` still reports duplicate-method false positives; `rg` verified single definitions for the named methods.
- Fresh Unity console after clear/refresh reports unrelated errors in `PrologueReentrySignals.cs`, `GlobalDataVault.cs`, and missing `Hecton8.UI.Tools`; no mutation file or `Hecton_LeviathanOrganic.shader` error appeared.
- `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by existing generated `.csproj` reference failures. Status remains `PENDING VERIFICATION`.

## 2026-05-13 - Finite Authority Hardening Pass
What was wrong:
- Loaded mutation authority still accepted non-finite runtime positions before hazard sampling.
- Base fauna authoring multipliers could leak non-finite or non-positive values into mutated runtime traits.
- The second static scan initially used a stale shader path, so that evidence had to be corrected before reporting.

What was done:
- Added finite runtime-position rejection in `EcosystemDirector.TryMutateFaunaGenome`.
- Centralized radiation/toxicity/brine sanitation through `FaunaGenome64.SanitizeScalar01`.
- Sanitized base scale, speed, and health multipliers before resolving runtime mutated traits.
- Reran prompt extraction, Unity validation, diff check, corrected static scan, and Unity console read.

Cinematic Cheats used:
- Bad samples are rejected and blackbox-recorded instead of trying to simulate recovery from corrupted world coordinates.
- Mutation visuals stay shader-side; authority only emits finite scalar controls.

Exact microseconds saved / estimated:
- Runtime-position finite guard: three scalar finite checks per loaded mutation request, under 1us at the 5s mutation cadence on i3/MX350.
- Centralized scalar sanitation: fixed branch cost per candidate, still within the existing <14us FrostTick prep estimate for 32 background candidates.
- Authoring multiplier sanitation: only runs during trait resolution; prevents NaN propagation that would cost debugging time and visual instability.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md:1036-1078`.
- Unity `validate_script` passed `Assets/_Project/Scripts/Ecosystem/FaunaGenome64.cs` with 0 diagnostics after hardening.
- Corrected static scan found no managed random, LINQ, string formatting, `.ToString()`, or division-style chance markers in mutation paths, including `Assets/_Project/Art/Shaders/Hecton_LeviathanOrganic.shader`.
- `git diff --check` reported CRLF warnings only.
- Unity console currently reports unrelated `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` errors; no mutation file error is present in the last 20 error/warning entries.
- Status remains `PENDING VERIFICATION` because the project-wide compile is blocked outside the ECHELON 3 fauna genetics domain.

## 2026-05-13 - SRP Batcher Overlay Hardening
What was wrong:
- The infection overlay in the fauna ecosystem partial still used `MaterialPropertyBlock` on standard fauna geometry.
- Mutation visuals had already moved onto owned runtime material scalars, so infection and mutation presentation were split across two incompatible state paths.

What was done:
- Removed the fauna infection MPB field and property-block writes.
- Added cached `_Color`, `_BaseColor`, and `_EmissionColor` property masks to the existing fauna runtime material pipeline.
- Infection tint/emission now writes only to the owned runtime materials already used for biolum, hit flash, death, and mutation hue/twitch.
- Clearing infection restores source material colors from the original material references.

Cinematic Cheats used:
- Infection color remains a material fake; no CPU skin deformation, bone animation, or physics impulses.
- Mutation hue/twitch and infection tint now share the same shader/material control path.

Exact microseconds saved / estimated:
- Removed MPB state churn on standard geometry; expected SRP Batcher stability gain depends on scene material layout and needs Unity Frame Debugger proof.
- Infection apply cost is a bounded loop over existing runtime material slots only when infection state/severity changes, estimated under 1us per changed loaded fauna on i3/MX350.
- No new per-frame allocations or new runtime material clones were added beyond the pre-existing fauna presentation clone path.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md:1036-1078`.
- Static scan found no `MaterialPropertyBlock`, `_ecosystemPropertyBlock`, `renderer.material`, `.materials`, managed random, LINQ, `string.Format`, or `.ToString()` in touched mutation/presentation paths.
- `git diff --check` reported CRLF warnings only.
- Unity MCP refresh timed out after 60 seconds and later console/validation pings failed; Unity-verified proof is absent for this pass.
- `dotnet build Hecton8.Core.csproj --no-restore` still fails on existing generated-project missing-reference errors (`Fluids`, `CCD`, audio propagation/echolocation, `MacroSwarm`, `BrineLayerSample`, etc.).
- Status remains `PENDING VERIFICATION`.

## 2026-05-13 - Loaded Fauna Service Cache Pass
What was wrong:
- The loaded-fauna ecology refresh still read `GlobalRegistry.EcosystemDirector` directly during the slow-tick mutation/disease path.
- Mutation authority was already interface-routed, but the reference was not retained across pooled lifecycle transitions.

What was done:
- Added cached `IEcosystemDirectorService` and `EcosystemDirector` references in `FaunaBrain.Ecosystem.cs`.
- Refreshed the cache on enable/spawn and cleared it on disable/despawn in `FaunaBrain.cs`.
- Loaded mutation requests now use the cached interface reference and refresh only when the cache is absent or uninitialized.

Cinematic Cheats used:
- No new physical simulation; the existing genome bitfield plus shader fake path stays intact.
- The improvement buys stability and cadence headroom for mutation visuals rather than adding more CPU simulation.

Exact microseconds saved / estimated:
- Expected under 1us per loaded fauna slow tick on i3/MX350 by avoiding repeated stable registry reads once the service is cached.
- No GC change: reference fields only, no lists, delegates, lambdas, strings, or allocations.

Verification:
- Prompt re-extracted from `Docs/Tasks/CURRENT_BATCH.md:1036-1078`.
- Static scan stayed clean for `MaterialPropertyBlock`, `_ecosystemPropertyBlock`, `renderer.material`, `.materials`, managed random, LINQ, `string.Format`, and `.ToString()` in touched mutation/presentation paths.
- `git diff --check` reported CRLF warnings only.
- Unity MCP reports no active Unity session; Unity validation is absent for this pass.
- `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by existing missing assemblies. The only touched-file entry in the filtered build output is the pre-existing `FaunaBrain.cs` missing `Hecton8.Physics.CCD` reference.
- Status remains `PENDING VERIFICATION`.
