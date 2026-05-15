# Rationale_ORGANIC_ENTROPY_REGENERATOR

Agent: `ORGANIC_ENTROPY_REGENERATOR`  
Role: `BACKEND_ENGINEER`  
Domain: Echelon 2/3 World Generation + Ecosystem  
Prompt source: `Docs/Tasks/CURRENT_BATCH.md`

## Decision 1 - New Macro Regrowth Backend Instead Of Patching Ore/Ecosystem Owners
Problem: The batch requested a deterministic 1000-day world regrowth model spanning ores, flora, nutrients, and predator timers. Existing ore and ecosystem files already own adjacent responsibilities and may be under active edits by other agents.

Solution: Implemented `WorldRegrowthSimulation` as a decoupled backend under the world/resource domain. It exposes NativeArray-backed memory, scheduled jobs, and a binary codec, but does not rewrite existing spawners or AI directors.

Rejected Alternatives: Patching `ProceduralOreSpawner` directly would couple regrowth to ore placement and risk public API churn. Patching `EcosystemDirector` would mix backend regrowth with live AI ecology and increase merge risk.

Scalability potential: Low uses sparse macro-sector byte lanes. Middle uses the same data to drive instanced proxy density. High extends visible flora density from the same mature/seed state. Ultra can spend saved CPU on richer shader response and denser near-field dressing without changing simulation truth.

Hardware Impact: On i3/MX350-class hardware, replacing per-object timers with 4096 linear byte-lane cells is expected to avoid roughly 250-600 us of managed dispatch in a daily solve. This is an engineering estimate; Unity profiler proof is absent.

## Decision 2 - Visual Fake First For Organic Recovery
Problem: Continuous regrowth can tempt per-plant/per-ore simulation, but the task needs 1000-day deterministic recovery, not plant biology.

Solution: Treated the simulation as macro-sector visual truth: nutrient, temperature, stage, ore stock, flora stock, prey, predator, and apex timer lanes. Presentation systems can read these lanes and choose density/variants.

Rejected Alternatives: Full mesh rebuilds, plant GameObjects, per-frond nutrient truth, and runtime procedural biology were rejected as expensive and unnecessary for player belief.

Scalability potential: Low uses mature/immature/cull density bands. Middle uses dithered LOD with seeded variants. High uses richer biome scatter from the same lanes. Ultra uses the saved CPU budget for overgrown silhouettes, richer wind/VAT, and higher near-camera variation.

Hardware Impact: Expected low-end gain is mainly memory and GC stability: no spawned flora timers, no managed collections, no runtime string state. Measured resident memory delta is pending Unity profiler.

## Decision 3 - Deterministic Nutrient Diffusion Scratch Lane
Problem: A diffusion pass that reads and writes `SoilNutrients` in one parallel job can become order-dependent and nondeterministic.

Solution: Added `SoilNutrientsScratch` as a separate write lane. `NutrientDiffusionJob` reads the previous soil state and writes scratch; `DailyRegrowthJob` reads scratch and writes the authoritative soil lane.

Rejected Alternatives: In-place diffusion, random neighbor sampling, and frame-staggered managed queues were rejected. In-place math is faster on paper but corrupts determinism.

Scalability potential: Low can reduce diffusion cadence to SlowTick/day-batch. Middle can run full 4-neighbor diffusion. High/Ultra can add biome-specific visual richness while keeping the same deterministic scratch model.

Hardware Impact: Adds one byte per macro-sector. For 4096 sectors this is 4 KB, acceptable against the determinism gain. Estimated CPU cost is below the 0.1 ms suspicion threshold for daily cadence, but measured proof is absent.

## Decision 4 - H8_MacroDB Fixed Binary Payload
Problem: Regrowth state must serialize into `H8_MacroDB` without runtime JSON, reflection, or managed object graphs.

Solution: Implemented `WorldRegrowthMacroDatabaseCodec` with a fixed 80-byte header, lane offsets, lane count validation, and checksum. Serialized lanes: soil, temperature, biome, stage, tombstone age, progress, ore, flora, prey, predator, apex. Scratch is excluded because it is derived.

Rejected Alternatives: JSON saves, BinaryFormatter, direct SaveManager changes, or adding new public contract requirements were rejected. Codec stays caller-owned and interface-compatible.

Scalability potential: Low stores compact byte lanes. Middle can delta-compress changed macro sectors. High/Ultra can preserve the same save payload while rendering denser presentation from it.

Hardware Impact: Payload for 4096 cells is 45,136 bytes before outer compression. This is cheap for save bandwidth and avoids managed serialization allocation. Compression/checksum integration remains pending with the macro DB owner.

## Decision 5 - Lotka-Volterra As Timer Source, Not AI Truth
Problem: The task requested predator repopulation using Lotka-Volterra to define apex respawn timer. Directly spawning predators would cross AI/runtime ownership.

Solution: Implemented fixed-point prey/predator biomass lanes and apex respawn day calculation. The result is a timer/data signal, not a spawned enemy.

Rejected Alternatives: Direct AI prefab spawning, concrete AI manager references, and event spam per sector were rejected. This backend should not own combat or AI lifecycle.

Scalability potential: Low uses coarse apex timers. Middle uses timer bands to select ambient threat density. High/Ultra can layer richer stalker audio/visual tells while preserving the same deterministic timer.

Hardware Impact: Two byte lanes plus one timer byte per sector. Expected cost is negligible at daily cadence; measured CPU proof absent.

## Decision 6 - Constants Tuned By Offline Entropy Harness
Problem: The prompt requires Safe Shallows to regrow 3x faster than Deep Abyss and status `ENTROPY BALANCED`; constants cannot be hand-waved.

Solution: Added `Tools/WorldEntropySim.py` and `Data/Economy/Regrowth_Constants.json`. The 365-day total-overharvest run produced Safe half-recovery day 28, Abyss half-recovery day 95, ratio 3.393, final mature ratio 1.000.

Rejected Alternatives: Static inspection of formulas, one-biome tests, and hardcoded status output were rejected. The status comes from acceptance checks.

Scalability potential: Low/Middle/High/Ultra all share deterministic constants but can scale visual response: density caps, LOD residency, shader wetness/bioluminescence, and near-field dressing.

Hardware Impact: Python is offline tooling. Runtime constants are byte/integer values and do not add frame cost. Final Unity runtime measurement is still pending.

## Decision 7 - Verification Boundary Is Honest
Problem: Project rules require compile/profiler proof, but local command execution repeatedly timed out and `dotnet` was not recognized during the attempted build.

Solution: Recorded the compile state as `[BLOCKED BY TOOLING]`, not green. Python entropy verification is recorded as real. Unity import, GCMonitor, player build, and profiler remain pending.

Rejected Alternatives: Claiming compile success from static inspection or Python success was rejected. Those are separate proof types.

Scalability potential: No scalability claim depends on unmeasured runtime data. The design supports quality tiers, but exact budgets require Unity profiler.

Hardware Impact: Estimated low-end gains are documented as estimates only. No fake GC/frame-time metrics were recorded.

## Decision 8 - Payload Header Validation Before Unsafe Copy
Problem: The initial H8_MacroDB unpack path validated magic, version, count, and checksum, but did not prove that header offsets matched the fixed lane layout before unsafe `MemCpy`.

Solution: Added fixed layout validation for width, height, cell count, and every lane offset before checksum/copy. Also restored `SoilNutrientsScratch` from serialized soil so loaded memory is immediately coherent.

Rejected Alternatives: Trusting checksum alone was rejected because the checksum covers data bytes, not header offsets. Ignoring scratch until the next diffusion pass was rejected because public memory should not expose stale derived lanes after load.

Scalability potential: Low uses the compact fixed payload. Middle can add outer delta compression without changing header semantics. High/Ultra can build richer visual state from the same validated payload.

Hardware Impact: Validation is cold-path save/load work. Runtime solve cost is unchanged. Low-end impact is negligible compared with preventing corrupted payload reads.

## Decision 9 - Allocated Grid Dimensions Own Initialization
Problem: Allocation clamps grid dimensions to safe bounds, but initialization previously derived x/z from raw config values.

Solution: Passed allocated `memory.Width/Height` into `InitializeRegrowthGridJob` and used those dimensions for sector indexing and biome banding.

Rejected Alternatives: Trusting config after clamp was rejected because invalid or oversized config could initialize with a layout different from allocated memory.

Scalability potential: Low can clamp to small grids; High/Ultra can allocate larger grids while using the same deterministic indexing path.

Hardware Impact: CPU cost is neutral. Correctness gain prevents bad macro-sector mapping on constrained devices that intentionally downscale grid size.

## Decision 10 - Serial Tombstone Writes And Minimum Flora Depletion
Problem: Mining index batches can contain duplicate cells. A parallel job would make duplicate depletion order-dependent, and severity 1 previously halved to zero for flora.

Solution: Kept mining tombstone writes as a serial Burst job and patched flora depletion to use a minimum severity of 1.

Rejected Alternatives: Parallel duplicate writes were rejected for nondeterminism. Trusting every caller to de-duplicate mined cells was rejected because no such contract exists.

Scalability potential: Low batches few mined cells and pays trivial serial cost. High/Ultra can still process larger mining batches deterministically because this path is event-cadenced, not per-frame ecology.

Hardware Impact: Serial mining is not the daily broad solve and is expected below practical frame concern for small event batches. If future mining batches become large, the correct path is deterministic de-duplication into a scratch set, not unsafe parallel writes.

## Decision 11 - Roslyn Probe Compile As Partial Proof
Problem: Full Unity import cannot run here, but leaving only Python evidence is weak for a new C# file with unsafe codec code and jobs.

Solution: Used Visual Studio Roslyn `csc.exe` with C# 9 and minimal Unity/Hecton8 API stubs to compile `WorldRegrowthSimulation.cs`. Probe result: exit code 0. Temporary stubs/output were removed after the probe.

Rejected Alternatives: Treating old `.lscache` as compile proof was rejected. Claiming full Unity compile from the probe was also rejected because Unity assembly definitions, Burst import, and real package APIs were not loaded.

Scalability potential: Probe compile does not affect runtime tiering; it improves code confidence before Unity import.

Hardware Impact: No runtime impact. Verification confidence improved without fake profiler numbers.

## Decision 12 - Unittest Coverage For Entropy Harness
Problem: Manual console output from `WorldEntropySim.py` is easy to misread and easy to regress.

Solution: Added `Tools/test_world_entropy_sim.py` using standard library `unittest`. It verifies the 365-day overharvest contract, deterministic reduced-grid replay, and locked acceptance constants.

Rejected Alternatives: Adding pytest was rejected because pytest is not installed in this environment. Re-running multiple 4096-cell 1000-day tests inside unittest was rejected after timeout; the direct 1000-day command remains logged separately.

Scalability potential: Test keeps the production 64x64 acceptance run for the core balance check while using an 8x8 reduced grid for replay determinism, so CI can catch logic regressions without bloating test time.

Hardware Impact: Tooling only. No runtime impact.

## Decision 13 - Cold-Path Black Box Dump Hook
Problem: The system had a fixed 300-entry telemetry ring but no concrete binary dump hook for crash/post-mortem capture.

Solution: Added `WorldRegrowthSimulation.TryDumpBlackBox(...)` with default path `Docs/AgentLogs/Dump_ORGANIC_ENTROPY_REGENERATOR.bin`. The method copies the fixed telemetry NativeArray into a cold staging byte array and writes it to disk.

Rejected Alternatives: Leaving dump ownership implicit was rejected because the black box mandate requires a dump path. Logging text was rejected because it allocates and loses binary state fidelity.

Scalability potential: Low/Middle/High/Ultra all share the same 300-entry binary ring; richer tiers can add more renderer-side diagnostics separately without changing the core dump.

Hardware Impact: No hot-path cost. The only managed allocation is a crash/manual dump staging buffer on a cold path.
