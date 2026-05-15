# LOG_ORGANIC_ENTROPY_REGENERATOR

## 2026-05-15 - Macro World Regrowth Simulation

What was wrong:
- The project needed deterministic world recovery for ores, flora, and apex predator respawn after total overharvesting.
- Per-object timers, scene-object respawn, and active AI coupling would violate the multi-agent boundary and create managed hot-path cost.
- A same-array nutrient diffusion path would be order-dependent under `IJobParallelFor`; that was rejected and corrected with a scratch lane.

What was done:
- Added `Assets/_Project/Scripts/World/Resources/WorldRegrowthSimulation.cs`.
- Added `WorldRegrowthSimulationMemory` with byte SoA lanes: nutrients, scratch nutrients, temperature, biome id, lifecycle stage, tombstone age, regrowth progress, ore stock, flora stock, prey biomass, predator biomass, apex respawn days, and a 300-entry black box telemetry ring.
- Added Burst-compatible jobs for grid initialization, nutrient diffusion, daily regrowth, deterministic serial mining tombstones, predator/prey projection, and telemetry aggregation.
- Added `WorldRegrowthMacroDatabaseCodec` with fixed payload header, 11 serialized lanes, magic/version/cell-count validation, and checksum validation.
- Added `WorldRegrowthSimulation.TryDumpBlackBox(...)` for cold-path binary telemetry dumps to `Docs/AgentLogs/Dump_ORGANIC_ENTROPY_REGENERATOR.bin`.
- Added `Tools/WorldEntropySim.py` for deterministic offline entropy testing.
- Added `Data/Economy/Regrowth_Constants.json` with tuned constants and acceptance gates.
- Updated `Docs/ARCHITECTURE/ORGANIC_ENTROPY_MATH.md` with the macro regrowth contract.

Cinematic cheats used:
- Macro-sector byte lanes replace per-flora/per-ore biological truth.
- Tombstone-to-seed-to-mature is a deterministic lifecycle proxy, not physical ecology.
- Predator respawn is byte-quantized Lotka-Volterra math, not live predator simulation in unloaded sectors.
- High-end visual overkill is deferred to presentation systems fed by the same macro truth; low-end hardware keeps the cheap byte lanes.

Exact microseconds saved:
- Exact profiler-backed microseconds are PENDING UNITY VERIFICATION.
- Estimated managed overhead avoided: 250-600 us per 4096-sector daily solve versus thousands of per-node MonoBehaviour timers.
- Estimated cache gain: 100-300 us versus dictionary/list keyed sector state.
- Estimated deterministic diffusion cost: one extra 4096-byte scratch lane plus one memory pass; accepted to remove order-dependent parallel reads.

Verification:
- `python Tools/WorldEntropySim.py --constants Data/Economy/Regrowth_Constants.json --days 365 --mode total_overharvest`
- Result: `STATUS=ENTROPY BALANCED`, Safe Shallows half recovery 28 days, Deep Abyss half recovery 95 days, ratio 3.393, final mature ratio 1.000.
- `python Tools/WorldEntropySim.py --constants Data/Economy/Regrowth_Constants.json --days 1000 --mode total_overharvest`
- Result: `STATUS=ENTROPY BALANCED`, final mature ratio 1.000 through day 1000.
- `python -m unittest Tools.test_world_entropy_sim -v`
- Result: 3 tests passed in 19.942 s on final rerun, exit code 0.
- Static scan of target C# found no `Update`, `FixedUpdate`, `LateUpdate`, coroutines, managed collection hot-path creation, scene search, random, material access, or uncached component calls.
- Roslyn probe compile: `WorldRegrowthSimulation.cs` compiled with C# 9 against Unity/Hecton8 API stubs, exit code 0.
- Post-hardening Roslyn probe compile after header validation/mining changes: exit code 0.
- Roslyn probe compile after black box dump hook: exit code 0.
- Compile status: PROBE GREEN / PENDING UNITY VERIFICATION. `dotnet` is not on PATH and Unity executable/MCP resources are unavailable in this session.

Regression model:
- CPU: daily macro solve adds linear byte-lane jobs; no per-frame tick owner was introduced.
- GC: runtime hot path uses `NativeArray` and caller-owned buffers; no managed hot-path allocations found by static scan.
- Memory: current 64x64 grid uses 12 byte lanes plus telemetry; scratch lane costs 4096 bytes.
- Cadence: scheduler is caller-owned; no `Update`/coroutine loop was introduced.
- Correctness: Python 365-day and 1000-day entropy tests meet the 3x shallow-vs-abyss recovery requirement.

Status: ENTROPY BALANCED / PROBE GREEN / PENDING UNITY VERIFICATION

## 2026-05-15 - Post-Report Hardening Pass

What was wrong:
- Initialization could derive sector x/z from raw config dimensions instead of clamped allocated memory dimensions.
- H8_MacroDB unpack validated checksum/count but did not prove fixed lane offsets before unsafe copy.
- Loaded scratch nutrient data could remain stale until the next diffusion pass.
- Mining severity 1 could reduce ore but leave flora unchanged because `severity >> 1` truncated to zero.

What was done:
- Passed allocated width/height into `InitializeRegrowthGridJob`.
- Added fixed header layout validation before any unpack lane copy.
- Copied serialized soil into `SoilNutrientsScratch` on unpack.
- Confirmed tombstone mining uses a serial job and patched flora depletion minimum to 1.

Cinematic cheats used:
- No new simulation truth was added. The backend remains macro-sector byte data that render systems can convert into richer instanced visuals.

Exact microseconds saved:
- No measured microsecond change. This pass is correctness hardening, not runtime optimization.
- Expected runtime impact is neutral for daily solve; header validation and scratch restore are cold-path save/load work.

Verification:
- Re-ran `python Tools/WorldEntropySim.py --days 365 --mode total_overharvest`.
- Result: `STATUS=ENTROPY BALANCED`.
- Safe-to-Abyss recovery ratio: 3.393.
- Final mature ratio: 1.000.
- Re-ran `python Tools/WorldEntropySim.py --days 1000 --mode total_overharvest`; result remained `STATUS=ENTROPY BALANCED`.
- Ran `python -m py_compile Tools/WorldEntropySim.py`; exit code 0.
- Re-ran `python -m unittest Tools.test_world_entropy_sim -v`; 3 tests passed in 19.942 s.
- Re-ran target banned-token scan; `rg` returned exit code 1 with no matches.
- Re-ran `dotnet --info`; `dotnet` is still not recognized. Full Unity compile remains blocked by tooling.
