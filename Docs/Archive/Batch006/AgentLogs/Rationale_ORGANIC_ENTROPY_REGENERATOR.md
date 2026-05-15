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

Solution: Added `Tools/WorldEntropySim.py` and `Data/Economy/Regrowth_Constants.json`. The latest C#-parity 365-day total-overharvest run produced Safe half-recovery day 28, Abyss half-recovery day 88, ratio 3.143, final mature ratio 1.000.

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

Solution: Added `Tools/test_world_entropy_sim.py` using standard library `unittest`. It verifies the 365-day overharvest contract, seeded C#-parity biome count snapshot, deterministic reduced-grid replay, and locked acceptance constants.

Rejected Alternatives: Adding pytest was rejected because pytest is not installed in this environment. Re-running multiple 4096-cell 1000-day tests inside unittest was rejected after timeout; the direct 1000-day command remains logged separately.

Scalability potential: Test keeps the production 64x64 acceptance run for the core balance check while using an 8x8 reduced grid for replay determinism, so CI can catch logic regressions without bloating test time.

Hardware Impact: Tooling only. No runtime impact.

## Decision 13 - Cold-Path Black Box Dump Hook
Problem: The system had a fixed 300-entry telemetry ring but no concrete binary dump hook for crash/post-mortem capture.

Solution: Added `WorldRegrowthSimulation.TryDumpBlackBox(...)` with default path `Docs/AgentLogs/Dump_ORGANIC_ENTROPY_REGENERATOR.bin`. The method copies the fixed telemetry NativeArray into a cold staging byte array and writes it to disk.

Rejected Alternatives: Leaving dump ownership implicit was rejected because the black box mandate requires a dump path. Logging text was rejected because it allocates and loses binary state fidelity.

Scalability potential: Low/Middle/High/Ultra all share the same 300-entry binary ring; richer tiers can add more renderer-side diagnostics separately without changing the core dump.

Hardware Impact: No hot-path cost. The only managed allocation is a crash/manual dump staging buffer on a cold path.

## Decision 14 - Python Harness Must Mirror C# Biome Hashing
Problem: The offline entropy harness initially used a simpler depth-band biome layout while C# initialization used `Hash32`, rotate-left sector mixing, local-z banding, world seed, and macro-sector origin. That made the old Deep Abyss recovery day a tester artifact rather than proof of runtime parity.

Solution: Patched `Tools/WorldEntropySim.py` to mirror the C# resolver and exported `entropyTestWorldSeed`, `macroSectorOriginX`, and `macroSectorOriginZ` into `Regrowth_Constants.json`. Updated expected half-recovery days to the actual seeded 64x64 layout: Safe 28, Temperate 41, Thermal 44, Deep Abyss 88. Added a unittest snapshot for seeded biome counts `[1729, 996, 564, 807]`.

Rejected Alternatives: Keeping hidden Python defaults was rejected because exported constants would not fully define the test. Keeping the stale 95-day Deep Abyss expectation was rejected because it came from a non-runtime layout. Using random biome distribution was rejected because replay determinism is mandatory.

Scalability potential: Low can reduce grid dimensions while preserving seed/origin semantics. Middle/High/Ultra can expand macro-sector coverage and still produce reproducible biome lanes for denser presentation systems.

Hardware Impact: Tooling-only change. Runtime C# cost is unchanged; the gain is correctness of offline acceptance evidence.

## Decision 15 - Negative Macro-Sector Remainder Hardening
Problem: C# `ResolveBiomeId` used `math.abs(sectorZ)` before modulo. That can overflow at `int.MinValue` and makes negative macro-sector handling depend on a raw absolute-value edge case.

Solution: Replaced raw absolute-z with bounded C# remainder conversion before absolute local-z banding. Mirrored that exact negative-remainder behavior in `WorldEntropySim.py` and added a negative-sector resolver test.

Rejected Alternatives: Assuming macro-sector origins are never negative was rejected because the world grid accepts an origin parameter. Leaving Python `%` semantics in the harness was rejected because Python and C# disagree for negative remainders.

Scalability potential: Low through Ultra tiers can shift macro-sector origins without changing biome distribution rules or losing replay parity.

Hardware Impact: CPU impact is neutral. The added branch is initialization/tooling-path only for biome placement, not a per-frame solve cost.

## Decision 16 - Full SOA IsCreated Guard
Problem: `WorldRegrowthSimulationMemory.IsCreated` initially checked only a subset of lanes. Because the lanes are public data-owner fields, external misuse or partial disposal could leave a scheduler guard passing while later jobs touched missing lanes.

Solution: Expanded `IsCreated` to verify every NativeArray lane used by initialization, solve, mining, telemetry, and codec paths.

Rejected Alternatives: Leaving the subset check was rejected because the scheduler guards would be weaker than `HasValidLaneLengths`. Making all lanes private was rejected as a larger API shift during a multi-agent batch.

Scalability potential: Low through Ultra tiers all depend on the same data block being coherent before scheduling. The guard cost is branch-only and does not change the daily math model.

Hardware Impact: No meaningful runtime cost; the hot solve still runs in jobs. This prevents corrupted memory-state scheduling rather than buying frame time.

## Decision 17 - Fresh Probe Compile After Final Guard Patch
Problem: After the guard change, prior probe compile evidence was stale.

Solution: Located Visual Studio 2022 Community Roslyn `csc.exe` and ran a fresh C# 9 unsafe library compile against temporary Unity/Hecton8 API stubs. Result: exit code 0. Temporary probe files were removed.

Rejected Alternatives: Reusing the previous probe result was rejected. The .NET Framework `csc.exe` was also rejected because it is too old for the C# surface and produced invalid-language errors.

Scalability potential: Verification-only. Runtime tiers unchanged.

Hardware Impact: No runtime impact. Compile confidence improved; full Unity import and profiler proof still require Unity tooling.

## Decision 18 - H8Memory Owner-Tracked Native Lanes
Problem: Regrowth lanes were still allocated with direct `new NativeArray<T>` calls even though the project has `H8Memory.Allocate<T>` and owner-based `SystemID` tracking. Direct allocation kept sentinel labels but bypassed the broader memory owner ledger.

Solution: Switched every regrowth NativeArray lane to `H8Memory.Allocate<T>` with `SystemID.WorldStreaming` and switched both deferred and immediate release helpers to `H8Memory.Release` with the same owner.

Rejected Alternatives: Keeping raw NativeArray allocation was rejected after confirming `H8Memory` is available in `Hecton8.Core.Memory`. Moving to `GlobalDataVault` was rejected for this pass because there is no existing regrowth buffer ID and adding one would mutate shared core contracts during a multi-agent batch.

Scalability potential: Low through Ultra tiers now account regrowth memory under a stable world-system owner, so future grid-size scaling has a single memory-budget owner.

Hardware Impact: Runtime solve cost is unchanged. Cold allocation/release now pays H8Memory tracking overhead and gains leak/accounting visibility.

## Decision 19 - Offline Harness Hot Loop Reduction
Problem: The entropy harness was correct but slow under current machine load. It rescanned the full grid after recovery milestones were already known, copied nutrient lists every day, recalculated byte-quantized apex timers per cell, and used division/modulo in the diffusion loop.

Solution: Added summary-scan gating, persistent nutrient scratch swapping, a 256x256 apex respawn LUT, and row-based diffusion traversal. Output stayed identical: Safe day 28, Deep Abyss day 88, ratio 3.143, final mature ratio 1.000.

Rejected Alternatives: Leaving the slow tester was rejected because regression tests were becoming expensive enough to discourage repeated use. Adding numpy was rejected because the project should not gain a new Python dependency for a local validation harness.

Scalability potential: Low-end CI can run the same deterministic harness with less waste. High-end validation can still run 1000-day checks without changing the acceptance model.

Hardware Impact: Tooling-only. Latest measured wrapper times under current load: 365-day command 68.723 s, 1000-day command 160.281 s, unittest wrapper 145.819 s. Runtime Unity code is unchanged.

## Decision 20 - Persistent Allocator Enforcement
Problem: `WorldRegrowthSimulationMemory.Allocate` accepted an allocator parameter but registered every lane as scene-lifetime native memory. A caller passing Temp or TempJob would create a lifetime mismatch.

Solution: Force all regrowth lanes to allocate with `Allocator.Persistent` inside `Allocate`, and leave the parameter ignored for compatibility with the existing call shape.

Rejected Alternatives: Trusting callers was rejected because the data block must survive across simulated days and save/load boundaries. Removing the allocator parameter was rejected as a public API change during the batch.

Scalability potential: Low through Ultra tiers use the same persistent macro-state block. Larger grids still have explicit disposal paths and sentinel registration.

Hardware Impact: Cold-path allocation policy only. Runtime solve cost is unchanged. This reduces leak/stale-buffer risk on low-memory devices.

## Decision 21 - Allocation Failure Rollback And Bounded Grid Budget
Problem: H8Memory can reject allocations when budget/tracking capacity is exhausted. Without rollback, a partially allocated regrowth block could remain live while `IsCreated` stays false. Also, clamping each dimension independently still allowed a pathological 4096x4096 grid.

Solution: Added partial-allocation rollback when any required lane fails, then tightened that rollback in Decision 22 to use a pre-registration release path. Added a `1,048,576` cell cap by reducing height after width clamp. Exported constants now carry exact `ENTROPY BALANCED` status while keeping Unity proof in a separate field.

Rejected Alternatives: Letting callers retry after partial allocation was rejected because it leaks ownership records and memory. Allowing a 4096x4096 macro grid was rejected because it is excessive for the low-end memory target. Hiding Unity verification status inside the main status string was rejected because the XML requires a clear entropy status.

Scalability potential: Low-end devices cannot be forced into an oversized macro grid by bad config. High-end devices still retain up to 1,048,576 macro-sector cells, which is already visual-overkill for this backend.

Hardware Impact: Default 64x64 path unchanged. Worst-case backend lane memory is capped to roughly 12 MB for the 12 byte lanes plus telemetry before payload buffers, instead of allowing roughly 192 MB for 4096x4096 lanes.

## Decision 22 - Sentinel-Free Failed Allocation Cleanup
Problem: The failed-allocation rollback in Decision 21 used the normal disposal path before `RegisterNativeArrays()` had run. `NativeMemorySentinel.UnregisterNativeArray` currently no-ops for untracked pointers, but depending on that behavior is a weak lifecycle contract.

Solution: Added `ReleaseUnregisteredNativeArrays()` for the pre-registration failure path. It releases only H8Memory-owned lanes with `SystemID.WorldStreaming`, then `ResetState()` clears width, height, cell count, and day. Normal registered disposal still unregisters from the sentinel first.

Rejected Alternatives: Keeping full `Dispose()` on pre-registration failure was rejected because it mixes registered and unregistered lifetimes. Registering partially created lanes was rejected because jobs require the full SOA block, not a partial data owner.

Scalability potential: Low through Ultra tiers get the same fail-fast behavior when memory budgets reject oversized grids. High-end grids still register only after the entire SOA block exists.

Hardware Impact: No hot-path cost. This is cold allocation failure hygiene; default 64x64 simulation and daily jobs are unchanged.

## Decision 23 - Partial State Reallocation Guard
Problem: `Allocate()` previously returned only when the full SOA block was created. If a caller reused a memory struct with only some lanes still alive, `Allocate()` could overwrite those fields with new H8Memory allocations and leave the old lanes live.

Solution: Added `HasAnyCreatedLane` and made `Allocate()` dispose any partial lane set before allocating a fresh coherent block. Full blocks still return without churn.

Rejected Alternatives: Trusting callers never to retry allocation after partial field disposal was rejected because the lanes are public data-owner fields. Making all lanes private was rejected as a larger API change during a multi-agent batch.

Scalability potential: Low through Ultra tiers get deterministic allocation ownership even after failed or partial teardown scenarios. Larger high-end grids still allocate as one coherent SOA block.

Hardware Impact: No hot-path cost. This is cold bootstrap/retry hygiene and does not affect daily solve timing.

## Decision 24 - Dimension Coherence Guard
Problem: `Width`, `Height`, and `CellCount` are public fields on the data-owner struct. If external code corrupts them while lanes remain alive, scheduler entry points can run jobs with invalid dimensions, including divide-by-zero in nutrient diffusion, and the codec can write payload headers that do not match the lane topology.

Solution: Added `HasValidDimensions` and used it before initialization, daily solve, mining tombstone scheduling, H8_MacroDB packing, and H8_MacroDB unpacking. The guard requires positive dimensions, the configured max grid bounds, the max cell budget, and `Width * Height == CellCount`.

Rejected Alternatives: Trusting allocation-time values was rejected because the fields are public. Making fields private was rejected as a larger public API shift during the batch. Adding exception throws was rejected because gameplay/backend code should fail closed and return false/dependency unchanged.

Scalability potential: Low through Ultra tiers all get the same coherent topology guarantee. High-end oversized configs still respect the `1,048,576` cell cap before any job or codec path accepts state.

Hardware Impact: Branch-only entry validation. Daily job bodies and entropy math are unchanged; low-end cost is effectively zero outside scheduler calls.

## Decision 25 - Exact SOA Storage Guard
Problem: Dimension coherence alone still allowed a corrupted but internally valid smaller topology to run against larger already-allocated lanes. The old codec lane check accepted `Length >= CellCount`, which could hide public field corruption and serialize only a prefix of the true SOA block.

Solution: Added `HasValidStorage`. Scheduler and codec entry points now require every serialized byte lane length to equal `CellCount` and require `BlackBox.Length == 300`. The old permissive lane-length helper was removed.

Rejected Alternatives: Keeping `Length >= CellCount` was rejected because it silently accepts mismatched topology. Making all lanes private was again rejected as a larger public API change during a multi-agent batch.

Scalability potential: Low through Ultra tiers now have a strict one-to-one topology contract between dimensions, lanes, and persisted payloads. High-end larger grids still work when allocated coherently.

Hardware Impact: Branch-only entry validation. Daily jobs execute the same math and memory traversal after the guard passes.

## Decision 26 - Fixed-Point Config Overflow Guard
Problem: `WorldRegrowthConfig` is caller supplied and contains `ushort` coefficient fields. Values far above the exported constants can overflow int products in growth and Lotka-Volterra math before clamps run.

Solution: Added `HasValidConfig` with conservative bounds: positive grid and macro-sector size, base growth <= 255, permille coefficients <= 1000, positive seed/tombstone thresholds, valid apex min/max days, and nonzero biome temperatures. Scheduling entry points reject invalid configs. The public `ResolveApexRespawnDays` helper fails closed to a 90-day delay if config is invalid.

Rejected Alternatives: Casting all hot-path math to 64-bit was rejected because this would tax every cell for invalid caller input. Silently clamping bad config into a new truth was rejected because it hides data defects. Throwing exceptions was rejected for backend/gameplay code.

Scalability potential: Low through Ultra tiers keep the same fast int math for valid data. Bad data fails at entry instead of producing overflow-driven ecology.

Hardware Impact: Branch-only entry validation. Daily jobs are unchanged for valid config; low-end runtime cost is effectively zero outside scheduler calls.

## Decision 27 - Exported Config Schema Regression Test
Problem: The C# fast-path config guard is only useful if exported constants stay inside the same bounds. The existing Python tests locked acceptance output but did not explicitly assert the coefficient and threshold ranges.

Solution: Added `test_exported_constants_match_csharp_fast_path_bounds` to `Tools/test_world_entropy_sim.py`. It validates positive grid/macro-sector sizes, base growth range, permille coefficient range, positive lifecycle thresholds, valid apex min/max, and biome temperature/nutrient sanity.

Rejected Alternatives: Manual JSON inspection was rejected because constants can drift. Duplicating the full C# validator in production code was rejected for this pass because the Python harness already owns offline acceptance validation.

Scalability potential: Low through Ultra tiers keep exported data within the same safe int-math envelope. Future higher-tier constants must remain explicit and tested.

Hardware Impact: Tooling only. Runtime code is unchanged.

## Decision 30 - Absent-Biome Acceptance Guard
Problem: Total-overharvest acceptance used final mature ratio and a recovery ratio, but an invalid constants slice with no Deep Abyss cells could report perfect final maturity while lacking required biome recovery evidence.

Solution: Extracted `calculate_balance` and made total-overharvest require Safe and Deep Abyss recovery days before comparing the ratio. Added a one-cell regression proving an absent Deep Abyss biome fails acceptance even with final mature ratio `1.0`.

Rejected Alternatives: Treating missing biome recovery as infinite recovery was rejected because it hides invalid coverage. Trusting only final mature ratio was rejected because it ignores biome-specific recovery requirements.

Scalability potential: Low through Ultra validation machines now fail malformed acceptance constants deterministically before publishing misleading balance evidence.

Hardware Impact: Tooling only. Runtime code is unchanged.

## Decision 28 - Entropy CLI Day Count Guard
Problem: The entropy harness CLI silently clamped non-positive `--days` values to one simulated day while printing the invalid original day count. That can create misleading evidence in automation logs.

Solution: `WorldEntropySim.py` now rejects `--days < 1` through argparse. Added `test_cli_rejects_non_positive_day_count` with stderr suppression so the regression is quiet and deterministic.

Rejected Alternatives: Keeping the hidden clamp was rejected because invalid evidence is worse than a failed command. Allowing day zero as an initial-state summary was rejected because the XML acceptance target is a simulated 365-day overharvest test, not a static snapshot.

Scalability potential: Tooling results remain comparable across low-end and high-end validation machines because invalid invocation parameters fail consistently.

Hardware Impact: Tooling only. Runtime code is unchanged.

## Decision 29 - Direct Harness API Day Count Guard
Problem: The CLI rejected non-positive day counts, but direct `run_sim()` callers could still pass `0` or negative values. That lets automation bypass the evidence guard and create misleading validation state outside argparse.

Solution: Added a fail-fast `ValueError` at the top of `run_sim()` before state construction. Added `test_run_sim_rejects_non_positive_day_count` so CLI and programmatic entry points share the invalid-day contract.

Rejected Alternatives: Keeping validation only in `main()` was rejected because the test suite and future batch automation call `run_sim()` directly. Silently clamping in `run_sim()` was rejected because it repeats the original misleading-evidence failure mode.

Scalability potential: Low/Middle/High/Ultra tiers are unaffected at runtime. Validation tooling is stricter, so bad long-horizon entropy evidence fails before expensive simulation loops run.

Hardware Impact: Tooling-only. Unity runtime backend cost is unchanged; invalid direct harness calls now avoid allocating Python state and avoid any simulated-day work.

## Decision 31 - Empty-Biome Half-Recovery Guard
Problem: `summarize()` marked half recovery when `matureByBiome * 2 >= countByBiome`. For a biome with zero cells, that condition is `0 >= 0`, so malformed or tiny validation grids could record a fake recovery day for an absent biome.

Solution: Required `countByBiome > 0` before writing `firstHalfRecoveryDays[biome]`. The existing `calculate_balance` fail-closed path then treats missing Safe/Deep evidence as failed total-overharvest acceptance.

Rejected Alternatives: Leaving the edge case to `calculate_balance` alone was rejected because the summary payload itself would still contain false recovery data. Throwing on missing biomes was rejected because reduced-grid determinism tests can intentionally omit a biome while still needing a summary.

Scalability potential: Low/Middle/High/Ultra validation grids now report absent biome coverage honestly. Larger high-end validation maps keep the same acceptance output when all required biomes are present.

Hardware Impact: Tooling-only. Runtime Unity backend unchanged; added one branch in Python summary generation.

## Decision 32 - Biome Constants Contract Guard
Problem: The entropy acceptance math treats biome index `0` as Safe Shallows and index `3` as Deep Abyss. `Regrowth_Constants.json` is external data, so order/id drift could make the harness validate the wrong biome without a clear failure.

Solution: Added `validate_constants` to require exactly four biome constants with ids and names matching runtime indices. `run_sim()` calls it before state construction. Added a regression that corrupts the Deep Abyss id and expects `ValueError`.

Rejected Alternatives: Trusting JSON order was rejected because the acceptance ratio uses hard-coded indices. Checking only exported happy-path constants was rejected because it would not prove malformed automation inputs fail closed.

Scalability potential: Low/Middle/High/Ultra validation runs now share the same biome contract before any expensive day loop starts. Larger future validation maps must keep the same index contract or intentionally change both runtime and harness.

Hardware Impact: Tooling-only. Unity runtime backend unchanged; validation cost is four biome table checks before simulation.

## Decision 33 - Python Fast-Path Config Parity Guard
Problem: `test_exported_constants_match_csharp_fast_path_bounds` verified the shipped JSON, but `run_sim()` itself still accepted malformed constants that the C# backend `HasValidConfig` would reject. That made direct automation weaker than runtime scheduling.

Solution: Extended `validate_constants` with the C# fast-path bounds: positive macro sector, base growth `1..255`, permille coefficients `0..1000`, positive seed/tombstone thresholds, and valid apex min/max days. Added a regression that sets `predationPermille` to `1001` and expects `ValueError`.

Rejected Alternatives: Trusting the unit test alone was rejected because future tools can call `run_sim()` with arbitrary constants. Silently clamping invalid constants was rejected because it would create a new untracked simulation truth.

Scalability potential: Low/Middle/High/Ultra validation runs now reject invalid constants before any expensive entropy loop starts. High-end validation can still use larger grids if the constants stay inside the runtime math envelope.

Hardware Impact: Tooling-only. Unity runtime backend unchanged; validation cost is a small constant number of integer checks before simulation.

## Decision 34 - Entropy Harness Grid Budget Guard
Problem: The C# regrowth backend caps allocation at `1,048,576` macro cells, but the Python entropy harness still accepted arbitrary `gridWidth * gridHeight`. A malformed constants file could force huge Python list allocations before failure.

Solution: Added `MAX_SAFE_GRID_CELLS = 1_048_576` and made `validate_constants` reject larger grids before state construction. Added `test_run_sim_rejects_oversized_grid` using `1025x1025`.

Rejected Alternatives: Relying on machine memory pressure was rejected because validation tools must fail deterministically. Shrinking oversized grids silently was rejected because it would change the tested world.

Scalability potential: Low-end validation machines avoid accidental huge allocations. High-end validation can still use up to the same cap as the runtime backend and must explicitly change both systems if a larger cap is required.

Hardware Impact: Tooling-only. Unity runtime backend unchanged; invalid oversized grids now abort before Python allocates per-cell lanes.
