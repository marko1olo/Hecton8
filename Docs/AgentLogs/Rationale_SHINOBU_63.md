# Rationale_SHINOBU_63 - Dynamic Trade And Marauder Logic

Date: 2026-05-18
Status: PENDING UNITY / PROFILER VERIFICATION - LOCAL STATIC GREEN / CORE BLOCKED BY EXTERNAL COMPILE ERRORS

## Decision 00 - Prompt Disambiguation

Problem: `CURRENT_BATCH.md` contains two `AGENT_PROMPT id="SHINOBU_63"` blocks. Blind ID-only parsing would merge dynamic trade with unrelated interior GI work.

Solution: Use the first `SHINOBU_63` block whose role and user request match `DYNAMIC_TRADE_AND_MARAUDER_LOGIC`, A* macro-routing, economy, and marauder submarines. Treat later GI block as rejected neighbor prompt.

Rejected Alternatives: Taking the last matching ID was rejected because it contradicts user text. Reading neighboring tasks was rejected because strict parsing forbids prompt contamination.

Scalability potential: Low/Middle/High/Ultra unaffected directly; the decision prevents wrong-domain edits that would waste compile and review time.

Hardware Impact: Estimated gain for i3/MX350 is non-runtime; avoids wrong files and compile churn.

## Decision 01 - Mandate Set

Problem: The task spans routing, economy, AUP, ARM64 layout, telemetry, typed signals, and editor tuning. Reading unrelated graphics or physics mandates would dilute implementation focus.

Solution: Read eight direct mandates: A* pathfinding, runtime struct layout, AUP determinism, zero GC, native memory/jobs, signal lanes, designer CSV bridge, and telemetry crash reporting.

Rejected Alternatives: Reading all 80 registry files was rejected as context noise. Reading only A* was rejected because DTO layout and signal/DataVault constraints are compile and architecture gates.

Scalability potential: Low uses coarse sector routing and cached economy. Middle keeps stable route cadence. High/Ultra can spend saved CPU on richer intercept routes and telemetry/debug visualization without changing gameplay truth.

Hardware Impact: i3/MX350 benefit is prevention of GC stalls and graph recalculation hitches; expected budget target remains <0.1 ms, pending profiler proof.

## Decision 02 - Missing Binary Weights

Problem: The prompt requires recon of `faction_economy_weights.h8bin`, but repository/archive search found no authoritative runtime payload for that exact file. Only stale economy archive hints existed.

Solution: Build a cold emergency mock economy initializer with aligned unmanaged DTOs, Copper/Titanium hashes, sector supply/demand defaults, and deterministic Copper hoarding proof. Runtime can be overridden by CSV without changing hot-path code.

Rejected Alternatives: Fabricating a binary schema was rejected because it would create false authority. Blocking the task was rejected because a mock dependency was explicitly required.

Scalability potential: Low uses one cheap mock pressure gradient. Middle uses sector supply/demand. High/Ultra can layer more faction/item weights through CSV into the same buffers.

Hardware Impact: Estimated i3/MX350 gain is 60-140 us per economy tick versus managed dictionary bootstrap during play.

## Decision 03 - DataVault-Only Marauders

Problem: Distant submarines across 50 km cannot be represented as GameObjects without transform, lifecycle, and culling overhead.

Solution: Store marauder positions, hull, velocity, inventory, route plans, and faction standing as fixed DataVault buffers. The only GameObject is the cold `TradeMarauderDirector` owner, installed under the existing economy runtime root.

Rejected Alternatives: Prefabs, NavMeshAgents, hidden scene objects, or pooled transforms were rejected because they still bind distance simulation to Unity object lifetime.

Scalability potential: Low runs fewer DTO rows and route solves. Middle keeps stable macro trade. High/Ultra increases active marauder count and editor visualization without changing authority.

Hardware Impact: Estimated i3/MX350 gain is 300-900 us per frame compared with even a small distant transform fleet.

## Decision 04 - Stutter-Free A* Cadence

Problem: The main pain point is hitching when macro graphs are recalculated.

Solution: Use a 101x101 sector grid, preallocated native min-heap, reusable cost/came-from buffers, and stamped node states so each solve touches only visited nodes. Heavy routing is scheduled from frost cadence and completed only when the handle is ready.

Rejected Alternatives: Rebuilding graph objects, clearing all scratch memory every solve, completing jobs in the same tick, and pathing every marauder every frame were rejected.

Scalability potential: Low reuses cached routes aggressively. Middle solves constrained route budgets. High/Ultra spends extra route budget on richer pirate behavior and salvage attraction.

Hardware Impact: Estimated i3/MX350 gain is 700-1800 us per 0.2 Hz solve burst, mostly from avoiding full scratch clear and same-frame completion.

## Decision 05 - AUP Precision

Problem: 50 km routing loses precision if macro truth is stored in `float3`.

Solution: Store all macro position and route centroids as `double3` AUP. Convert to `float3` only for local tactical deltas under 500 m and editor gizmo drawing relative to scene view.

Rejected Alternatives: Global float routing was rejected for drift. Per-node `Vector3` was rejected because Unity structs are not the simulation authority.

Scalability potential: Low/Middle/High/Ultra share exact macro truth; only visualization and tactical detail scale with quality.

Hardware Impact: Estimated i3/MX350 cost is acceptable at 0.2 Hz; saved precision prevents route jitter and correction work.

## Decision 06 - Economy And Transaction Boundaries

Problem: Trade, theft, salvage, and faction reputation need to operate while neighbor systems are incomplete and while many agents edit nearby domains.

Solution: Use Vault buffers for item hashes, quantities, faction standing, loot nodes, and transaction scratch. Job-level trade deltas use `Interlocked.Add`; offscreen theft emits a typed mock transaction signal instead of touching player inventory classes.

Rejected Alternatives: Direct inventory singleton access, object locks, and concrete economy service references were rejected as cross-domain coupling.

Scalability potential: Low emits sparse transactions. Middle adds faction price pressure. High/Ultra can increase route/loot richness through the same DTO contract.

Hardware Impact: Estimated i3/MX350 gain is 80-220 us per trade burst and prevents lock contention.

## Decision 07 - Continuous Quality Weight

Problem: The project rejects binary low/ultra switches; routing and economy must scale continuously.

Solution: Resolve `GlobalQualityWeight` from serialized tuning, `GlobalRegistry.ScalabilityTierProfileByte`, stress, and DataVault pressure. Jobs consume this scalar for route count, cache reuse, tactical update density, signal budgets, and theft/salvage pressure.

Rejected Alternatives: enum-tier branches as the only control were rejected. Removing high-tier overkill was rejected because saved CPU should buy immersion.

Scalability potential: Low uses cached coarse routes and minimal signals. Middle uses normal supply routes. High increases marauder activity and tactical nuance. Ultra spends surplus on denser routing, acoustic emissions, and editor inspection.

Hardware Impact: Estimated i3/MX350 gain is 180-520 us during stressed ticks by reducing active solve budgets without discarding macro truth.

## Decision 08 - Telemetry And Crash Dump

Problem: Economy/pathing failures must be diagnosable after NaN or fatal state without relying on chat or console logs.

Solution: Keep a fixed 300-frame `MarauderTelemetryEntry` ring in Vault and dump it to `Docs/AgentLogs/Dump_TRADE_SURGEON.bin` on fault/infinite-loop detection.

Rejected Alternatives: `Debug.Log` spam and managed per-frame history were rejected as non-deterministic and GC-heavy.

Scalability potential: Low keeps the same fixed ring. Middle/High/Ultra can inspect richer fields without allocation growth.

Hardware Impact: Estimated i3/MX350 steady-state cost is under 10 us per recorded tick; crash-path file allocation is outside frame-critical operation.

## Decision 09 - Editor Facade And CSV Override

Problem: Designers need price, spawn, theft, and aggression tuning without editing runtime components or forcing hot-path parsing.

Solution: Add `TradeMarauderTunerWindow` as editor-only facade. It reads/writes tuning DTOs, parses `faction_economy.csv` on demand, and draws AUP routes from Vault.

Rejected Alternatives: Runtime UI, inspector-only component exposure, and route debug GameObjects were rejected.

Scalability potential: Low validates route sparsity. Middle tunes normal trade. High/Ultra can inspect denser route curves and extreme marauder pressure.

Hardware Impact: No player-runtime cost outside editor. Editor-only drawing cost is isolated.

## Decision 10 - Verification Result

Problem: New unmanaged jobs touched core and editor compile surfaces; false completion without compile proof is unacceptable.

Solution: Built `Hecton8.Core.csproj` and `Hecton8.Editor.csproj`, then ran static scans for DTO setters, LINQ/foreach, GameObject/NavMesh misuse, and uninitialized scratch allocation proof.

Rejected Alternatives: Relying on code review only was rejected. Fixing unrelated repository warnings was rejected to avoid touching other agents' work.

Scalability potential: Verification protects Low/Middle/High/Ultra paths because they share the same DTO and job contracts.

Hardware Impact: No runtime impact; prevents broken build churn on shared machines.

## Decision 11 - Burst Directive Repair

Problem: The first pass compiled but used `CompileSynchronously = false` on every Burst job, which violates the polish mandate and risks first-use Burst warmup stutter.

Solution: Updated all eight trade/marauder jobs to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`.

Rejected Alternatives: Leaving async Burst warmup was rejected because the stated pain is stutter. Deterministic float mode was rejected because this domain uses deterministic integer/sector frame seeds for authoritative random events and float results are not cross-client rollback authority yet.

Scalability potential: Low avoids first-contact JIT hitch. Middle/High/Ultra keep fast math and can spend budget on visual proxies/acoustic richness.

Hardware Impact: Estimated i3/MX350 gain is not steady-state; it removes a potential first-evaluation spike.

## Decision 12 - NoAlias And Counter False Sharing

Problem: Job arrays had no aliasing proof, and shared counters were adjacent `int` slots in one cache line.

Solution: Added `[NoAlias]` to job `NativeArray` fields and replaced raw int counters with `MarauderPaddedCounterDTO`, an explicit 64-byte cache-line DTO.

Rejected Alternatives: Keeping `NativeArray<int>` was rejected because future parallel work would invalidate adjacent counters. Pointer wrappers were rejected because existing Vault handle patterns prefer typed `NativeArray<T>` views.

Scalability potential: Low reduces cache churn. Middle/High/Ultra keep SIMD-friendly memory assumptions as route and signal budgets rise.

Hardware Impact: Estimated i3/MX350 gain is 20-90 us in stressed route/signal bursts, with larger value when jobs expand.

## Decision 13 - Deterministic Theft RNG

Problem: Offscreen theft originally used a hash threshold. It was deterministic but did not satisfy the explicit `Unity.Mathematics.Random` mandate.

Solution: Seed `Unity.Mathematics.Random` from simulation frame, base AUP sector hash, marauder index, and item hash. Zero seed is remapped to one.

Rejected Alternatives: `UnityEngine.Random` was never used and remains forbidden. Wall-clock seeds were rejected for co-op rollback.

Scalability potential: All quality tiers preserve identical theft outcomes for identical frame/sector state.

Hardware Impact: RNG cost is sub-microsecond at 20 marauders; determinism prevents desync debugging cost.

## Decision 14 - Continuous Quality And Hysteresis

Problem: The first pass still had a binary profile branch and hard cache skip threshold shape.

Solution: Resolve profile through `math.lerp`, smooth quality by 25% toward raw target per FrostTick, and make economy-cache reuse stochastic/proportional under 0.4 quality instead of a fixed low/high step.

Rejected Alternatives: Hard low-tier branch was rejected. Recomputing full economy while throttled was rejected because combat thermal pressure must shed ALU.

Scalability potential: Low uses cached economy most ticks. Middle mixes cache and solve ticks. High/Ultra solve denser item/route budgets and hydrate more visual proxies.

Hardware Impact: Estimated i3/MX350 gain is 180-560 us on stressed FrostTicks.

## Decision 15 - BRG Proxy Hydration Without GameObjects

Problem: The prompt says distant marauders exist as math only and are hydrated into BRG matrices near the player. First pass stopped at logic/gizmos.

Solution: Added `MarauderVisualHydrationJob` and a Vault `MarauderVisualProxyDTO` buffer. Within 1 km, the job writes 64-byte local-space matrices for a renderer/BRG owner to consume.

Rejected Alternatives: Hidden transforms, pooled presentation objects, and direct renderer coupling were rejected.

Scalability potential: Low hydrates at most a tiny proxy budget. Middle hydrates normal visible contacts. High/Ultra can hydrate all near contacts and use saved CPU for richer GPU-side presentation.

Hardware Impact: Estimated i3/MX350 gain versus GameObject hydration remains 300-900 us/frame; proxy matrix write cost is bounded by 20 rows.

## Decision 16 - Compile Wall Reality

Problem: Core rebuild took 75 seconds after touching this domain. The code path is isolated, but the physical assembly boundary is not.

Solution: Do not split `Assets/_Project/Scripts/Economy` during this pass because existing economy classes have unrelated cross-domain dependencies. Record the assembly split as explicit debt for an owner task.

Rejected Alternatives: Creating a new asmdef in the folder now was rejected because it would move `ScrapManager` and `ResourceScarcityDirector` into the new assembly and risk broad breakage.

Scalability potential: Runtime unaffected. Iteration speed remains a tooling debt.

Hardware Impact: No player-runtime impact; developer hardware impact remains severe until assembly boundary work is scheduled.

## Decision 17 - AUP Local Distance Enforcement

Problem: Some distance predicates subtracted AUP coordinates but still evaluated length in `double`, which violates the stricter local-float distance rule.

Solution: Added local distance helpers and changed offscreen raid, base reach, tactical intercept, and visual hydration checks to subtract first, cast to local `float3`, then measure finite `float` distance.

Rejected Alternatives: Continuing absolute/double predicates was rejected because the world-scale jitter rule is explicit. Casting before subtracting was rejected because it would destroy precision.

Scalability potential: Low/Middle/High/Ultra keep identical route authority while local presentation and predicates remain stable.

Hardware Impact: Estimated i3/MX350 gain is 5-20 us per FrostTick and better SIMD fit; primary value is correctness.

## Decision 18 - AcousticEchoTap Dependency Rejection

Problem: The prompt names `AcousticEchoTap`, but the actual type lives in `Hecton8.Audio.Virtualization.Contracts`. Referencing it from this economy runtime would introduce a sibling-domain assembly dependency.

Solution: Continue emitting `AcousticPingSignal`, the existing decoupled core signal lane consumed by sensory/radar systems. Record the deviation explicitly instead of manufacturing a duplicate echo-tap type.

Rejected Alternatives: Adding a direct audio virtualization reference was rejected by unidirectional routing. Defining another `AcousticEchoTap` was rejected because duplicate acoustic truth already has archived rejection evidence.

Scalability potential: Low emits fewer pings. Middle emits detectable contacts. High/Ultra can spend extra budget on richer audio consumers without economy owning them.

Hardware Impact: Estimated i3/MX350 gain is 10-40 us versus bridging into audio virtualization structures directly and avoids compile-wall expansion.

## Decision 19 - No Arbitrary Disable Complete

Problem: `OnDisable` still called `JobHandle.Complete()` unconditionally, creating a possible teardown stall.

Solution: Only complete and publish if the handle is already completed; otherwise stop scheduling and clear owner handles without blocking.

Rejected Alternatives: Blocking teardown was rejected. Disposing or reallocating Vault memory here was rejected because this domain does not own those bytes.

Scalability potential: All tiers avoid a disable-time hitch.

Hardware Impact: Prevents an unbounded main-thread stall during scene or tool disable.

# Rationale_SHINOBU_63 - Dynamic Trade Reactivation

Date: 2026-05-18
Status: PENDING UNITY / PROFILER VERIFICATION - LOCAL STATIC GREEN / CORE BLOCKED BY EXTERNAL COMPILE ERRORS

## Decision 23 - Latest User Directive Overrides Duplicate-ID Drift

Problem: The status/rationale files contain a later GI section caused by the duplicate `SHINOBU_63` prompt ID. The latest user message explicitly names dynamic trade, marauder submarines, A* macro-routing, no distant GameObjects, and AUP precision.

Solution: Reactivate the first XML block, `CURRENT_BATCH.md` lines 1234-1289, and leave the GI text as collision evidence only. All code changes in this pass are confined to economy/marauder files and the SHINOBU_63 docs.

Rejected Alternatives: Deleting the GI history was rejected because it may be another agent's evidence trail. Continuing GI work was rejected because it contradicts the current explicit Russian instruction.

Scalability potential: Low/Middle/High/Ultra unaffected directly; this prevents wrong-domain churn and protects the compile wall from irrelevant lighting edits.

Hardware Impact: Non-runtime; avoids another broad recompile from wrong-domain work.

## Decision 24 - A* Route Must Drive Movement, Not Only Gizmos

Problem: The A* result was written from goal backward to start, but movement consumed route slot 0. That made the marauder steer toward the far goal instead of the next macro sector, weakening Leviathan avoidance and making the path solve partly cosmetic.

Solution: Count the came-from chain, store route nodes in start-to-goal order without allocations, and move toward slot 1 when available. The route buffer remains fixed-size and Vault-owned.

Rejected Alternatives: Allocating a temporary reverse list was rejected. Leaving direct goal steering was rejected because it wastes A* work and can cross forbidden sectors.

Scalability potential: Low stores a truncated near-start route and still avoids obvious forbidden sectors. Middle/High/Ultra get denser route solves without changing DTO authority.

Hardware Impact: Estimated i3/MX350 gain is not raw CPU; it converts existing A* work into actual avoidance, preventing future corrective replans and route oscillation.

## Decision 25 - Acoustic Scratch Must Be Owned DTO Layout

Problem: `AcousticPingSignal` is a valid core signal lane, but it embeds the world `AbsoluteUniversePosition` layout. Writing that directly as Burst scratch ties this domain to a foreign AUP transfer layout inside the job.

Solution: Add `MarauderAcousticSignatureDTO`, a 64-byte sequential scratch row containing `double3 AUP`, scalar radius/intensity/source/channel fields, and explicit 8-byte padding. The Burst job writes this owned DTO; the main-thread publish bridge converts to `AcousticPingSignal`.

Rejected Alternatives: Direct `AcousticEchoTap` was rejected because it lives in the audio virtualization assembly. Direct `AcousticPingSignal` scratch was rejected after the layout audit. Defining a duplicate echo tap was rejected as parallel acoustic truth.

Scalability potential: Low emits sparse scalar pings. Middle/High/Ultra can publish more signatures or let downstream audio spend saved budget on richer DSP without economy depending on it.

Hardware Impact: Estimated i3/MX350 gain is 5-25 us per publish burst from simpler scratch layout and reduced Burst dependency surface; bigger value is compile-wall containment.

## Decision 26 - Binary Quality Flag Removed From Tuning DTO

Problem: `Tuning.Flags = quality < 0.4 ? 1u : 0u` was only diagnostic, but it reintroduced a binary low/high marker into the hot tuning DTO.

Solution: Encode fixed-point quality weight into `Flags` with `round(quality * 65535)`. Runtime behavior already uses continuous item budgets, A* iteration budgets, proxy budgets, and stochastic cache reuse.

Rejected Alternatives: Keeping the binary flag was rejected. Removing `Flags` was rejected because the field can carry compact telemetry/debug state without changing layout.

Scalability potential: Low/Middle/High/Ultra now see a continuous encoded scalar instead of a cliff marker.

Hardware Impact: Runtime cost is negligible; prevents future systems from branching on a binary flag.

## Decision 27 - Reflection Layout Proof

Problem: Manual struct layout math is easy to falsify accidentally when adding a new acoustic DTO.

Solution: After rebuilding, reflect offsets from `Temp/bin/Debug/Hecton8.Core.dll` with Unity.Mathematics loaded. Verified `MarauderStateDTO`, `MarauderAcousticSignatureDTO`, and `MarauderPaddedCounterDTO` all declare size 64 and match expected offsets.

Rejected Alternatives: Trusting `[StructLayout(Size=64)]` alone was rejected. Editing a permanent test harness was rejected because this pass should not add new build surface.

Scalability potential: All quality tiers share the same DTO layouts and memcpy safety.

Hardware Impact: No runtime cost; protects ARM64 alignment and false-sharing guarantees.

## Decision 28 - Sector Hash Buffer Must Carry Data, Not Just Allocation

Problem: `ShinobuTradeMarauderSectorHash` was reserved in the Vault but not resolved or initialized during mock boot. That left Task 13's sector-hash authority weaker than the docs claimed.

Solution: Thread `MarauderSectorHashEntryDTO` through `TryResolveAllViews` and populate every 1000 m sector with signed sector coordinates, hash, sector index, and flags during `GenerateEmergencyMockEconomy`.

Rejected Alternatives: Leaving it as future wiring was rejected because the prompt explicitly says marauders move through Sector Hash coordinates. Direct dependency on Agent 52 spatial hash was rejected because that owner is outside this task.

Scalability potential: Low uses the same hash rows with fewer route solves. Middle/High/Ultra can map richer supply/loot/threat overlays into the same sector hash table.

Hardware Impact: Mock boot fill is cold; runtime benefit is avoiding future managed hash dictionaries and graph rebuilds.

## Decision 29 - External Compile Wall Classification

Problem: After the sector-hash patch, the Core build no longer reaches a green result because unrelated files now fail: `DiegeticGlitchSurgeonRuntime.cs` references missing `TerminalOsRuntime.ApplyDiegeticGlitchToActiveRuntimes`, `SignalWardenRuntime.cs` references missing `WaterlineBreachSignal`, and `SaveData.cs` references missing `DataArchaeologyDiscoveryBitMask`.

Solution: Do not edit unowned UI/core/save systems under the trade task. Record the external compile wall and keep SHINOBU validation to static scans, previous green build evidence, and compiler absence of SHINOBU errors in the failing full build.

Rejected Alternatives: Patching those files here was rejected as cross-domain sabotage. Reverting unknown changes was rejected because they are not mine.

Scalability potential: Runtime trade scalability unaffected; project-wide verification is blocked until the owning agents repair their compile surfaces.

Hardware Impact: Developer hardware is currently paying full Core compile cost for unrelated failures; this remains an integration blocker.

## Decision 30 - Deferred Job Fence On Disable

Problem: The non-blocking `OnDisable` path avoided a main-thread stall, but it also cleared handles while a job could still be running. That could allow a later enable to schedule over a live Vault write.

Solution: If the job is not completed, unregister tick lanes and clear editor active state, but leave `_jobScheduled`, `_activeJobHandle`, `_vault`, and buffer handles intact. On the next enable/tick, the director drains the old fence before scheduling new work. Already-completed jobs are still completed and published immediately.

Rejected Alternatives: Blocking with unconditional `Complete()` was rejected for hitch risk. Clearing handles while running was rejected for lost-fence risk.

Scalability potential: All quality tiers share the same job safety path; low-end devices avoid both arbitrary teardown stalls and overlapping route/economy jobs.

Hardware Impact: Prevents unbounded teardown stall while preserving dependency safety; expected hitch avoidance remains 250-700 us per disabled heavy burst.

## Decision 31 - External Compile Wall Drift

Problem: A later Core build attempt now fails in `ThermalGeyser.cs` with missing `HectonPlayerMovement` and duplicate `SerializeField`. This confirms the project compile wall is moving due parallel agents, not SHINOBU changes.

Solution: Record the current blocker and keep trade-domain validation scoped to static scans plus compiler absence of SHINOBU file errors.

Rejected Alternatives: Editing `ThermalGeyser.cs` here was rejected because it is outside dynamic trade/marauder ownership.

Scalability potential: Runtime trade scalability unaffected; integration cannot claim full project green until the thermal gameplay owner repairs that file.

Hardware Impact: Another full Core compile attempt cost about 55 seconds and returned unrelated errors.

## Decision 32 - AUP Millimeter Quantization After Macro Integration

Problem: Macro route truth correctly used `double3`, but repeated 5-second integration steps could still accumulate sub-millimeter drift in telemetry, rollback snapshots, and route-source hashes.

Solution: Quantize stored `MarauderStateDTO.AUP` to a 1 mm grid after each A* movement integration. Distance and steering math still subtracts first and casts the local delta to `float3`; the quantization is only a storage stabilization step. Non-finite next AUP values are rejected and the fault counter is set.

Rejected Alternatives: Quantizing before local steering was rejected because it would reduce route precision before math. Leaving unbounded double accumulation was rejected because blackbox and rollback buffers need stable, comparable state rows.

Scalability potential: Low/Middle/High/Ultra share the same macro truth precision. Low-end devices get more stable cached routes; high-end devices can run denser replan cadence without growing state entropy.

Hardware Impact: Estimated i3/MX350 runtime cost is under 5 us per solved marauder batch; expected gain is fewer corrective replans and cleaner blackbox diffs.

## Decision 33 - Deterministic Pathfinding Telemetry Proxy

Problem: `PathfindingComputeTimeMs` was always zero, which made the 300-frame blackbox less useful for diagnosing graph-recalc stutter. Direct wall-clock timing inside Burst would violate determinism and introduce platform-specific noise.

Solution: Write a deterministic A* iteration-cost proxy: `iterations * 0.000018 ms`, capped at 0.25 ms. The value is not a profiler replacement; it is a monotonic blackbox pressure signal that survives rollback and dump comparison.

Rejected Alternatives: `Stopwatch`, Unity `Time`, profiler markers inside the hot job, and managed counters were rejected. Leaving zero was rejected because it hides the exact failure mode this task targets.

Scalability potential: Low shows route-cache pressure without per-frame timing. Middle/High/Ultra can compare iteration pressure as budgets rise through `GlobalQualityWeight`.

Hardware Impact: Cost is negligible; debugging gain is avoiding repeated 47-120 second compile/profiler cycles to locate macro-route stutter.

## Decision 34 - External Compile Wall Drift To Modding API

Problem: After the AUP/telemetry patch, `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` now fails in `Assets/_Project/Scripts/ModdingAPI/HectonAPI.cs` because `FutureCommandEnvelope` is missing.

Solution: Record this as the current external compile blocker. Do not patch the modding API from the marauder trade task.

Rejected Alternatives: Defining a dummy command envelope here was rejected because it would create false cross-domain API authority. Reverting unknown edits was rejected because parallel agents own those changes.

Scalability potential: Runtime trade scalability unaffected; global build verification remains blocked by another domain.

Hardware Impact: The failed Core build consumed about 47.7 seconds and reported no SHINOBU runtime errors.

## Decision 35 - Do Not Generate NaN Even On Rejected Path

Problem: The first AUP quantizer guard returned a newly constructed `NaN` triplet when given non-finite input. The caller rejected it, but the domain rule is stricter: never manufacture poison values if the old value can be preserved for rejection.

Solution: Return the original non-finite value from `QuantizeAupMillimeters`; the outer `IsFinite` guard rejects it and sets `FaultFlags` without creating a new NaN. Static scan now confirms no `double.NaN` in SHINOBU runtime/editor files.

Rejected Alternatives: Keeping the constructed NaN sentinel was rejected. Coercing bad AUP to zero was rejected because it would silently teleport a marauder and corrupt route telemetry.

Scalability potential: Low/Middle/High/Ultra share the same fault behavior; bad state is rejected instead of hidden by tier-specific coercion.

Hardware Impact: Runtime cost is unchanged; reliability gain is preventing a poison sentinel from escaping future refactors.

## Decision 36 - External Compile Wall Drift To Symbiosis

Problem: A fresh Core build after the NaN guard patch now fails in `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs` because `SymbiosisAup48` is missing across multiple lines.

Solution: Record this as external to dynamic trade. Keep SHINOBU verification to static scans and compiler absence of SHINOBU file errors in the full build.

Rejected Alternatives: Adding `SymbiosisAup48` here was rejected because it belongs to the flora/fauna symbiosis domain, not marauder trade. Reverting unknown changes was rejected because parallel agents may own that file.

Scalability potential: Runtime trade scalability unaffected. Integration still needs the ecosystem owner to repair their compile surface before global Unity/profiler validation is meaningful.

Hardware Impact: The failed Core build consumed about 50.6 seconds and reported no SHINOBU runtime errors.

## Decision 37 - Status File Domain Decontamination

Problem: `Docs/Tasks/Status_SHINOBU_63.md` still contained a detailed Interior GI active-pass block after the dynamic trade section. Because the anti-amnesia protocol treats this file as primary memory, that block could redirect future SHINOBU_63 work into the wrong domain.

Solution: Remove the detailed GI task block from the status file and retain only the one-line duplicate-ID collision note under Prompt Identity. Rationale keeps the explanation of why the later GI prompt was rejected.

Rejected Alternatives: Keeping the block as "history" was rejected because status files are active operational state, not archive logs. Deleting every duplicate-ID mention was rejected because the collision is still useful evidence.

Scalability potential: Runtime unaffected; prevents wrong-domain edits and compile churn across Low/Middle/High/Ultra validation.

Hardware Impact: Non-runtime; avoids another broad compile cycle from stale-domain work.

## Decision 38 - Runtime Base AUP Must Drive Demand

Problem: The supply-chain solver accepted `BaseAup`, but demand selection still leaned on the mock `PlayerBase` sector flag. If Agent 13 or an origin-shifted base relocates, marauders could route toward stale central-grid demand.

Solution: Add a local-space base-demand curve: subtract `BaseAup` from each sector centroid, cast only that local delta to `float3`, then compute a 2 km smooth polynomial demand bias. The result is blended with the old flag bias so mock boot data still works.

Rejected Alternatives: Absolute double distance scoring was rejected because the AUP rule demands subtract-first local math for distance predicates. Flag-only demand was rejected because it cannot follow relocated bases. A binary inside/outside radius switch was rejected because the scalability pillar requires smooth math.

Scalability potential: Low keeps coarse cached routes but demand still follows the real base. Middle/High/Ultra can run more route solves and richer item weights without changing the base-demand authority.

Hardware Impact: Estimated i3/MX350 cost is 10-35 us per supply solve over the current sector budget; it prevents bad routes and avoidable replans when the base AUP is not the mock center.

## Decision 39 - Explicit `math.step` In Active Trade Math

Problem: The mandate explicitly requires `math.lerp`, `math.step`, and polynomial curves. The runtime already used `math.lerp` and polynomial smoothing but had no active `math.step` gate.

Solution: Gate the smooth base-demand curve with `math.step(0.0001f, inside01)`. This keeps the curve continuous inside the radius while hard-zeroing the denormal tail outside the demand radius.

Rejected Alternatives: Adding a dead `math.step` call was rejected. Using `if (inside)` was rejected because it adds a branch and does not satisfy the active math mandate.

Scalability potential: All tiers share the same curve; the global quality scalar still controls how often and how densely this demand field is evaluated.

Hardware Impact: Micro-cost is negligible; the branchless gate keeps SIMD-friendly sector scoring.

## Decision 40 - External Compile Wall Drift To Construction Signals

Problem: After the base-demand patch, `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` now fails in `Assets/_Project/Scripts/Construction/ConstructionSignals.cs` because `ISignal` is missing on two signal structs.

Solution: Record the blocker as external. Do not patch construction signals from the marauder trade domain.

Rejected Alternatives: Adding an import or changing construction signal contracts here was rejected because it is outside the dynamic trade boundary and could mask another agent's assembly/reference work.

Scalability potential: Runtime trade scalability unaffected; global verification remains blocked by construction-domain compile hygiene.

Hardware Impact: The failed Core build consumed about 29.9 seconds and reported no SHINOBU runtime errors.

## Decision 41 - ItemEvaluationLimit Must Cap Actual Work

Problem: `BuildTuning` produced `ItemEvaluationLimit`, but the supply-chain job recomputed item count from quality and could diverge from editor/runtime tuning or future CSV overrides. The old code also assumed at least one economy row after only checking `IsCreated`.

Solution: Add `itemCapacity`, guard zero-length economy buffers before indexing, clamp `Tuning.ItemEvaluationLimit`, and make actual `itemLimit = min(qualityItemLimit, configuredItemLimit)`.

Rejected Alternatives: Trusting telemetry-only `ItemEvaluationLimit` was rejected because designers need the cap to control actual CPU work. Ignoring zero-length buffers was rejected because it could fault before blackbox telemetry.

Scalability potential: Low stays at 10-or-fewer configured items. Middle/High/Ultra can raise item count through the same continuous scalar and editor cap without hidden extra work.

Hardware Impact: Expected low-end gain is workload predictability, not raw speed: the solver now obeys the exact cap and avoids accidental 50-item passes under mismatched tuning.

## Decision 42 - External Compile Wall Expanded

Problem: After the item-cap patch, Core build now fails in multiple unrelated domains: `WorldProceduralStateRegistry.cs`, `SargassumGlobalDragManager.cs`, `AssetLifecycleGovernor.cs`, `BiolumPulseSyncRuntime.cs`, `FutureCommandSandboxValidator.cs`, and `FaunaDirector.cs`.

Solution: Record the expanded external wall and keep SHINOBU validation to static scans plus absence of SHINOBU file diagnostics in the full build.

Rejected Alternatives: Fixing fauna/world/optimization/vfx/modding errors under this trade task was rejected as cross-domain churn and likely conflict with parallel owners.

Scalability potential: Runtime trade scalability unaffected; project-wide verification is currently blocked by multiple owners.

Hardware Impact: The failed Core build consumed about 59.1 seconds and reported no SHINOBU runtime errors.

## Decision 43 - Low-Tier Sector Sampling Must Cover The Whole Map

Problem: The low-quality sector budget evaluated the first N rows in row-major order. At `GlobalQualityWeight` near 0.1, that can completely miss the player's base near the grid center and make the economy path toward stale corner sectors.

Solution: Convert the sector loop to distributed sampling over the full `SectorEconomy.Length`, and force the sector resolved from real `BaseAup` into sample 0. Sector demand still uses local subtract-first base bias.

Rejected Alternatives: Evaluating all 10,201 sectors at low quality was rejected because it reintroduces thermal stutter. Keeping row-major truncation was rejected because it violates macro-economy correctness. Random sector sampling was rejected because deterministic rollback and repeatable blackbox dumps matter more than noise.

Scalability potential: Low now preserves macro truth with sparse representative samples. Middle samples denser coverage. High/Ultra evaluate nearly/all sectors without changing code paths.

Hardware Impact: Runtime cost is unchanged for a given `sectorLimit`; the fix converts wasted corner scans into useful whole-map coverage and avoids route replans caused by missing base demand.

## Decision 44 - External Compile Wall After Sector Sampling

Problem: After the sector-sampling patch, Core build still fails outside SHINOBU with 33 errors across procedural fauna, Sargassum, AssetLifecycleGovernor, Biolum, SaveBinaryPayloadCodec, and FaunaDirector.

Solution: Record the updated external wall. Continue refusing cross-domain edits under this agent.

Rejected Alternatives: Patching missing `DataArchaeologyDiscoveryBitMask`, fauna DTO fields, or Addressables lifecycle methods here was rejected because those are unrelated owner lanes.

Scalability potential: Runtime trade scalability unaffected; no Unity/profiler verification can be claimed until the global build wall is cleared.

Hardware Impact: The failed Core build consumed about 51.3 seconds and reported no SHINOBU runtime errors.

## Decision 45 - Full-Budget Sector Coverage Must Be Literal

Problem: The distributed sector sampler fixed low-tier row-major blind spots, but the same interpolation formula could still skip sector 0 when `sectorLimit >= sectorCapacity`. That weakens high/ultra full-map economy passes and makes "all sectors" a lie.

Solution: `ResolveSampledSectorIndex` now switches to direct one-to-one sector traversal when the requested sample count covers the full sector buffer. The forced `BaseAup` sector is applied only to sparse thermal sampling, where replacing sample 0 is intentional and bounded.

Rejected Alternatives: Keeping interpolation for all tiers was rejected because it makes full-budget coverage imprecise. Always forcing the base sector was rejected because it duplicates one sector and drops another during high-tier exhaustive passes.

Scalability potential: Low/Middle use distributed representative samples with guaranteed base coverage. High/Ultra traverse every sector exactly once, preserving full economy precision without changing the job contract.

Hardware Impact: No added runtime cost; the fix prevents high-tier correctness loss and avoids follow-up replans caused by a missing sector in an intended full-map pass.

## Decision 46 - External Compile Wall Narrowed To AssetLifecycleGovernor Duplicates

Problem: After the full-budget sampler patch, the latest Core build fails in `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs` with duplicate method definitions. The diagnostics are outside the dynamic trade/marauder domain and no SHINOBU runtime error is reported before the external wall.

Solution: Record this as the current global compile blocker and refuse cross-domain edits. SHINOBU validation remains static scans, XML validation, source diff audit, and previous green compile evidence before the external optimization file drifted.

Rejected Alternatives: Removing duplicate lifecycle methods here was rejected because optimization/addressables ownership is external to Agent 63. Reverting unknown changes was rejected because parallel agents may own them.

Scalability potential: Runtime trade scalability is unaffected. Project-wide Unity/profiler verification remains blocked until the optimization owner repairs the duplicate definitions.

Hardware Impact: The failed Core compile consumed about 21.6 seconds and reported no SHINOBU runtime diagnostics; continuing to re-run global compile without owner repair wastes developer hardware.

## Decision 47 - A* Iteration Budget Must Be Per FrostTick, Not Per Submarine

Problem: The macro A* job limited each submarine independently. At high quality, `MaxRouteSolves` can reach 12 and `MaxAStarIterations` can reach 10,201, so the worst-case frost tick could expand more than 120,000 nodes. That is exactly the graph-recalculation stutter pattern the prompt calls out.

Solution: Treat `Tuning.MaxAStarIterations` as one global frost-tick budget shared across all marauders in `MarauderMacroAStarJob.Execute`. Each solve receives only the remaining budget. When the budget is exhausted, the job writes the best partial route found so far and records `AStarBudgetExhausted` plus telemetry flag `2`.

Rejected Alternatives: Keeping a per-route cap was rejected because it hides the hitch behind active route count. Calling `Complete()` earlier or scheduling fewer GameObject-like agents was rejected because the authority must remain Vault DTOs and non-blocking jobs.

Scalability potential: Low gets one cheap bounded route solve and partial-route continuity. Middle spreads a moderate budget across active plans. High/Ultra can solve more routes when the graph is easy, but a hard per-tick ceiling prevents pathological replans from stealing the frame.

Hardware Impact: Worst-case high-quality A* expansion is capped at 10,201 nodes per frost tick instead of 12 * 10,201; expected hitch avoidance on i3/MX350-class silicon is 600-2400 us during synchronized replans.

## Decision 48 - Weighted A* Collapses Low-Quality Search Without Losing High-Tier Exactness

Problem: Sparse route budgets reduce work, but a low-quality device can still spend too many node expansions on long 50 km routes if the heuristic remains exact and conservative.

Solution: Add a continuous weighted heuristic: `weight = lerp(lerp(1.85, 1.35, priority), 1.0, smoothQuality)`. Below 0.3 quality, low-priority routes expand aggressively toward the goal; high-priority routes remain closer to exact. At quality 1.0 the weight is exactly 1.0, so normal A* ordering is restored.

Rejected Alternatives: A binary low-end greedy pathfinder was rejected. Applying the weight at all tiers was rejected because ultra/high should keep exact routing when budget allows. Lowering AUP precision was rejected; all macro coordinates remain `double3` and local float casts still happen only after subtracting AUP.

Scalability potential: Low uses bounded weighted search with partial routes. Middle transitions smoothly through the polynomial. High/Ultra recover exact A* while still respecting the global frost-tick budget.

Hardware Impact: Expected low-end reduction is 20-45 percent fewer expanded nodes on open 50 km routes, with no GameObject or graph rebuild cost added.

## Decision 49 - Latest Compile Wall Remains Optimization Domain

Problem: After the A* frost-tick budget patch, `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` still fails before a full green result. The current diagnostics are concentrated in `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs`: `SetNativeRefCount` is called with the wrong arity, `_assetTrackers`, `_assetTrackerFlags`, `_assetTimeToLive`, `_assetCacheProfiles`, and `_heapTelemetryRing` are missing, and several helper calls have mismatched signatures.

Solution: Classify this as an external optimization/addressables compile wall. SHINOBU validation remains source-static plus compiler absence of SHINOBU diagnostics in the failed global build. Do not patch `AssetLifecycleGovernor.cs` from the dynamic trade/marauder task.

Rejected Alternatives: Adding local dummy asset lifecycle fields or changing helper signatures was rejected because that file is outside the assigned domain and could overwrite another agent's in-progress ownership. Reverting unknown optimization edits was rejected for the same reason.

Scalability potential: Runtime trade scalability is unaffected. Project-wide Unity/profiler validation remains blocked until the optimization owner repairs the asset lifecycle surface.

Hardware Impact: The failed Core build consumed about 52.3 seconds and reported no `TradeMarauderRuntime.cs` errors before the external wall.

## Decision 50 - Authoritative Marauder Jobs Need Deterministic Burst Floats

Problem: The SHINOBU jobs previously used `FloatMode.Fast`. That is acceptable for purely visual fakes, but this domain mutates authoritative route choice, theft probability gates, inventory deltas, velocity, acoustic state, and blackbox telemetry. Cross-platform float reassociation can create rollback drift between x86 and ARM64.

Solution: Switch all eight SHINOBU Burst jobs to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]`. Keep the continuous quality math, AUP `double3` authority, deterministic RNG seeding, and fixed Vault buffers unchanged.

Rejected Alternatives: Keeping `FloatMode.Fast` was rejected because the generic polish mandate's rollback exception applies here. Splitting visual hydration into a fast job was rejected for now because the matrix derives from authoritative velocity/AUP and should remain bit-stable for blackbox comparison.

Scalability potential: Low/Middle/High/Ultra share the same deterministic state transitions. Low loses a small amount of ALU throughput but gains reproducible route/economy behavior; high/ultra can still spend extra budget through `GlobalQualityWeight`.

Hardware Impact: Expected i3/MX350 cost is a small Burst optimization loss, estimated 15-60 us per frost tick under load. The trade is accepted to avoid multiplayer desync and non-reproducible QA dumps.

## Decision 51 - Do Not Inject Infinity Or Normalize Non-Finite Fallbacks

Problem: NaN vaccination still had two weak edges: macro fallback normalization could normalize an infinite fallback velocity, and offscreen local distance returned `float.PositiveInfinity` as a sentinel. Infinity is not NaN, but it is still a non-finite value that can hide in branch math and blackbox records.

Solution: Guard fallback vector length with `math.isfinite` before `rsqrt`, and return a finite huge scalar `3.402823e+38f` for rejected local distances. This preserves branch behavior while keeping telemetry/math finite.

Rejected Alternatives: Leaving `float.PositiveInfinity` was rejected because the mandate asks for long-run fatalism around every denominator/vector. Clamping bad positions to zero was rejected because it could teleport a marauder and corrupt route proof.

Scalability potential: All tiers share the same finite rejection behavior; low-end partial routes and high-end exact routes remain comparable in dumps.

Hardware Impact: Negligible runtime cost; prevents rare non-finite propagation that would cost hours in endurance QA.

## Decision 52 - Deterministic Patch Compile Wall Audit

Problem: After deterministic Burst conversion, the Core build still cannot reach a green result because an external optimization file blocks compilation.

Solution: Run `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` and classify the current wall precisely: `Assets/_Project/Scripts/Optimization/AssetLifecycleGovernor.cs(553,21)` calls `SetNativeRefCount` without the required `trackers` parameter. No `TradeMarauderRuntime.cs` diagnostics are reported before this external wall.

Rejected Alternatives: Editing `AssetLifecycleGovernor.cs` here was rejected because asset lifecycle/addressables ownership is outside the dynamic trade/marauder domain. Reverting unknown edits was rejected because parallel agents may own that file.

Scalability potential: Runtime SHINOBU scalability is unaffected; global Unity/profiler verification remains blocked by the optimization owner.

Hardware Impact: The build consumed about 68.5 seconds and produced 1 unrelated error plus 8 pre-existing physics warnings.

## Decision 53 - Local A* Nodes Must Resolve To Global Sector Hash Rows

Problem: The A* grid is local around a marauder's `SourceAup`, but threat lookup used the local packed node id directly against `SectorEconomy`. That only works when the route source is at the world origin. At 50 km scale, a marauder near the map edge could avoid or enter the wrong Leviathan sectors because local node id and global sector index diverge.

Solution: Resolve the source global sector coordinate once from `SourceAup`, then map each local A* node by integer offset into the global 101x101 sector grid. `ResolveThreatCost` reads `SectorEconomy` through that global index; off-map nodes return `LeviathanAvoidCost`. `WriteRoute` now stores global `SectorIndex` metadata, not local packed ids.

Rejected Alternatives: Recomputing `double3` AUP to global sector index for every neighbor was rejected because it adds double math to the hottest A* loop. Keeping local ids was rejected because it violates the AUP/Sector Hash mandate.

Scalability potential: Low-quality partial routes and high-quality exact routes now avoid the same real threat rows. High/Ultra route visualization and telemetry can map route nodes back to global sector hash rows without interpretation errors.

Hardware Impact: Expected i3/MX350 gain is 20-80 us during large A* bursts versus repeated per-neighbor double conversions, and more importantly it prevents bad routes that would trigger corrective replans.

## Decision 54 - Sector-Index Patch Compile Wall Drift

Problem: After the global sector-index repair, the Core build still cannot finish because an unrelated save/binary codec symbol is missing.

Solution: Run `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` and record the current external wall: `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs` references missing `DataArchaeologyDiscoveryBitMask` at lines 890, 892, 900, 931, 932, and 956. No SHINOBU runtime diagnostics were reported.

Rejected Alternatives: Patching the save codec here was rejected because it is outside dynamic trade/marauder ownership. Adding a dummy mask type from this domain was rejected because it would create false save-format authority.

Scalability potential: Runtime trade scalability unaffected; global verification remains blocked by save/binary payload ownership.

Hardware Impact: The failed build consumed about 76.3 seconds and produced 6 unrelated save codec errors plus 8 pre-existing physics warnings.

## Decision 55 - Invalid Plans Must Not Keep Old Routes Alive

Problem: If a route plan becomes idle, has a non-finite target, or starts outside the global sector grid, the macro A* job used to skip solving without clearing the previous route count. Editor gizmos, telemetry readers, or future BRG consumers could then display a stale route as if it were current.

Solution: Add `ClearRoute(marauderIndex)` and call it for idle plans, invalid targets, out-of-map source AUP, and invalid start/goal node cases. The route buffer rows remain untouched, but `RouteCounts[marauderIndex] = 0` makes the stale data unreachable.

Rejected Alternatives: Clearing all 64 route nodes was rejected because the count is the authoritative live length and full clears add unnecessary memory bandwidth. Leaving stale routes was rejected because it creates false proof in the editor facade.

Scalability potential: All quality tiers avoid stale route visualization. Low-tier partial routes remain valid only while their plan is valid.

Hardware Impact: Cost is one byte write per invalid plan; expected gain is avoiding debugging time and false route-read work.

# Rationale_SHINOBU_63 - Interior GI Reasserted Tail Anchor

Date: 2026-05-19
Status: PENDING UNITY / PROFILER VERIFICATION - LOCAL STATIC ONLY / DOTNET BUILD NOT LAUNCHED BY USER ORDER

## Decision 56 - Duplicate-ID Drift Must Not Override Current GI Request

Problem: `Docs/Tasks/Status_SHINOBU_63.md` and this rationale file contain stale dynamic-trade history because `CURRENT_BATCH.md` has two `SHINOBU_63` blocks. The current user text explicitly names `SHINOBU_INTERIOR_GI_PROBES`, WFC base GI, Spherical Harmonics, no Unity Realtime GI, and SDF wall blocking.

Solution: Re-extract the later block at `CURRENT_BATCH.md` line 2388 and treat the earlier dynamic-trade block as contamination for this turn. Continue edits only in `Assets/_Project/Scripts/Lighting` plus SHINOBU_63 evidence files.

Rejected Alternatives: Deleting stale history was rejected because parallel-agent evidence may still matter. Continuing trade work was rejected because it contradicts the active user request.

Scalability potential: Low/Middle/High/Ultra unaffected directly; the decision prevents wrong-domain compile churn.

Hardware Impact: Non-runtime; protects developer hardware from irrelevant rebuild/edit loops.

## Decision 57 - AUP Hash Must Not Cast 100km Coordinates Through Float

Problem: `HashAup` used `math.asint((float)(aup * 0.03125))`. That reintroduces large-world precision loss exactly where the GI grid tries to identify root/source room identity. A 100km world can alias unrelated base cells if the hash path truncates through 32-bit float before integer hashing.

Solution: Quantize each double AUP component to a 32m integer cell with `math.floor`, clamp to a safe integer range, cast to `long`, then FNV-mix low and high 32-bit halves. All local SH distance math still subtracts `RootAup` first.

Rejected Alternatives: Keeping the float hash was rejected because it violates the AUP precision rule. Hashing raw double bytes was rejected because tiny sub-cell floating drift would churn room hashes.

Scalability potential: Low uses sparse sources and still resolves stable room identity. Middle/High/Ultra increase source count and active resolution without hash alias drift.

Hardware Impact: Hashing runs cold/source-update only; expected low-end cost is negligible. It prevents wrong-room source reuse and blackbox ambiguity.

## Decision 58 - L0-Only Mode Must Stop Burning L1/L2 ALU

Problem: `GlobalQualityWeight` correctly collapsed `DirectionalWeight` and `L2Weight`, but `AddScaled`, `AddDirectional`, and `PackTexture` still executed dead directional math. At `q < 0.08`, this means the runtime advertises L0-only while still spending multiply/normalize/sqrt work on zero directional coefficients.

Solution: Add early L1/L2 gates. `AddScaled` writes only L0 when L1/L2 weights are zero. `AddDirectional` injects L0 radiance then returns before normalization and SH basis construction when both directional weights are zero. `PackTexture` skips the L1 `sqrt` when L1 energy is zero.

Rejected Alternatives: Leaving dead math was rejected because the central pain is FPS loss from bounce calculations. Binary low/high quality branches were rejected; the existing continuous weights remain the control.

Scalability potential: Low keeps believable flat bounced color at minimum ALU. Middle gradually restores L1. High/Ultra restores L2 and the full directional texture signal.

Hardware Impact: Estimated low-end saving is 20-80 us per active 12-16 resolution solve depending on source count, plus removed `sqrt` in empty L1 upload rows.

## Decision 59 - Directional SH Coefficients Should Scale Linearly With Radiance

Problem: `AddDirectional` multiplied source color by gain and then multiplied L1/L2 coefficients by gain again. Emergency/flashlight HDR values could therefore square the injection energy and rely on `SanitizeAndClamp` to recover.

Solution: Apply radiance gain once to the color and use L1/L2 weights only as basis scale factors. This keeps L0, L1, and L2 coefficients in the same radiance family and lowers NaN/clamp pressure.

Rejected Alternatives: Clamping harder was rejected because it hides incorrect SH energy. Leaving quadratic gain was rejected because high-tier L2 would over-emphasize emergency flashes while low-tier L0 remains normal.

Scalability potential: Low L0 is unchanged. Middle/High/Ultra get more stable directional gradients and less coefficient saturation.

Hardware Impact: CPU cost unchanged except for gated work above; visual stability improves and reduces blackbox fault risk.

## Decision 60 - Quality Fallback Must Remain Continuous

Problem: `ResolveQualityWeight` consumed `HomeostasisBrain.GlobalQualityWeight`, but its non-finite fallback used a binary `Mx350/Low ? 0.1 : 1.0` branch. That violates the project rule that quality selection must be a scalar continuum, even when the primary quality source is invalid.

Solution: Map `HectonQualityTier` to a normalized scalar from Low/Mx350/Mid/High/Ultra, run it through `Smooth01`, then `math.lerp(0.1f, 1f, curved)`. The fallback is still cold/error-path logic, but it feeds the same continuous math as the normal `GlobalQualityWeight`.

Rejected Alternatives: Keeping the branch was rejected because it creates a hidden low/ultra dichotomy. Returning a fixed 0.5 was rejected because it ignores boot-time hardware facts.

Scalability potential: Weak devices fall back near 0.1-0.24, middle devices near the midpoint, and high/ultra near 0.84-1.0 without changing solver code paths.

Hardware Impact: Negligible runtime cost; prevents an invalid quality source from forcing a full 32^3 L2 solve on mid/high-class hardware by accident.

## Decision 56 - Stale-Route Patch Compile Wall Drift

Problem: After stale-route invalidation, the Core build still fails outside SHINOBU.

Solution: Run `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal` and record the current wall: `SaveBinaryPayloadCodec.cs` still misses `DataArchaeologyDiscoveryBitMask`, and `Visor/HectonAbyssalSsdoFeature.cs` plus `Visor/HectonScooterVolumetricShaftsFeature.cs` miss `HectonDrsRenderFeatureGate`. No SHINOBU runtime diagnostics were reported.

Rejected Alternatives: Editing save codec or visor render feature gates was rejected because both are outside Agent 63's dynamic trade domain.

Scalability potential: Runtime trade scalability unaffected; global compile/profiler verification remains blocked by save and visor owners.

Hardware Impact: The failed build consumed about 106.6 seconds and produced 8 unrelated errors plus 8 pre-existing physics warnings.
