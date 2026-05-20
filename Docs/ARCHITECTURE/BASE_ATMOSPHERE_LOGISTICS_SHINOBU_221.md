# Base Atmosphere Logistics - SHINOBU_221

Owner: `Hecton8.Atmosphere.BaseAtmosphereLogisticsRuntime`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only as static documentation/source orientation. Current authority is `AGENTS.md`, `.agents-skills`, `Docs/Actual Domains of Project.txt`, current source files, current verification artifacts, and `Docs/Reports/2026-05-20_DOCUMENTATION_R43_ROOT_ARCHITECTURE_ROUTE_CARD_AND_COUNTER_RESIDUE_LOCAL.md`. R42 remains the prior counter/route-boundary/proof-label correction; R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers.

No Unity import, Unity Console, Play Mode, Burst Inspector, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, route soak, save/load route, atmospheric scene wiring, or visual proof is implied unless this document links a fresh evidence artifact.
Current DOC_GLOBAL boundary (2026-05-20 R47): `Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md` is the latest local static root/architecture authority-spine, runtime-wording, and counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Purpose: replaces base-wide oxygen reserve reads with a local 3D gas logistics authority backed by `GlobalDataVault`. Gas truth is stored as 32-byte `AtmosphereCellDTO` cells and solved across a CSR graph with double-buffered Jacobi diffusion in Burst.

## Runtime Data
Owner-local Vault IDs are declared in `AtmosphereLogisticsBufferIds` as numeric `BufferID` casts, not as central `H8Memory.BufferID` enum additions.

- Cells: `71500` front, `71501` back
- Graph: `71502` nodes, `71503` connections, `71504` offsets, `71505` destinations, `71506` conductance, `71507` write cursor
- Sources: `71508` consumers, `71509` toxic sources, `71510` vents
- Control/diagnostics: `71511` counters, `71512` tuning, `71513` telemetry, `71519` gas remainders, `71520` shader payload
- Delta lanes: `71514..71518`, each row is `AtmosphereDeltaLane64` padded to 64 bytes to prevent false sharing during atomic source/sink writes
- Cold tuning ingest: `71521` CSV scratch, `71522` gas profiles, source file `Docs/Atmosphere/gas_diffusion_profiles.csv`

Cold fallback generation uses Burst-decorated `IJob.Run()` for both the emergency mock topology and CSR builder. Direct job `Execute()` is not used in the SHINOBU_221 cold graph route.

CSR offsets use shifted degree counts: connection pass writes counts into `EdgeOffsets[node + 1]`, then the builder accumulates `EdgeOffsets[1..nodeCount]` into cumulative end offsets. This preserves `EdgeOffsets[i]..EdgeOffsets[i+1]` as the contiguous adjacency range for node `i`.

## Signal Inputs
- `PlayerBaseEnterSignal` / `PlayerBaseExitSignal`: maintains the active player breathing consumer.
- `FluidIncursionSignal`: injects breach oxygen drain, CO2, toxin, and cold shock.
- `ReactorDamageSignal`: published by `BioReactor` during overheat/meltdown; injects toxin, CO2, O2 drain, and heat. The payload lives in `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs`, not in the Atmosphere assembly, so reactor/gameplay consumers do not depend on the Atmosphere runtime.
- Legacy `HabitatIntegrityManager` global oxygen values are fallback-only. When the atmosphere runtime exposes a valid snapshot, module oxygen contribution syncing removes its old fallback contribution and stops maintaining a competing aggregate.

## Determinism And Scale
- Solver jobs use `FloatMode.Deterministic`.
- Diffusion uses self-weighted Jacobi: `(neighborGasSum + currentGas) / max(sumConductance + 1, 0.0001)` before continuous alpha blending and source/sink delta application.
- Diffusion clamps each node's CSR range to `[0, EdgeCount]` before reading edge destinations/conductance, so stale or corrupted offsets fail bounded instead of reading outside the lane.
- Iteration count is continuous: `(int)math.lerp(1f, 8f, smoothed GlobalQualityWeight)`.
- Runtime quality uses a smoothstep hysteresis filter before writing the tuning DTO, so thermal pressure sheds solver cost faster than it recovers and avoids one-frame iteration flicker.
- AUP positions stay as `double3`; jobs subtract source/node AUP in double before casting local deltas to `float3`.
- Gas changes enter integer delta lanes, then quantize back through million-unit remainders. Conservation residuals are applied after quantization as bounded distributed corrections across back-buffer cells per gas channel, not concentrated into one anchor cell.
- Gas profile CSV rows accept either numeric profile IDs or module type names. Module names are hashed with allocation-free lowercase FNV-1a over `ReadOnlySpan<byte>`.
- CSV malformed diagnostics are per-row (`rowMalformed`) with an aggregate file result, so one bad authored row does not poison later valid profile DTO flags.
- Simulation locks all job-read and job-write Vault lanes used by the scheduled solver, including read-only topology/source/tuning rows, and releases them only in `PostSimulation`.
- The active front/back cell `BufferID`s are captured at lock acquisition and reused for unlock, so odd Jacobi iteration counts can swap `_frontCells`/`_backCells` without leaking the originally locked Vault rows.
- Editor and gizmo read APIs fail closed while `_simulationScheduled` is true, because the active front handle is swapped before the scheduled job has necessarily completed writing the new front buffer.

## Black Box
The runtime writes a fixed 300-frame `AtmosphereTelemetryEntry` ring with node count, max CO2, average O2, max toxin, iterations, microseconds, and state hash. NaN detection dumps the ring to `Docs/AgentLogs/Dump_SHINOBU_221.bin`.

## Editor Facade
`BaseAtmosphereLogisticsTunerWindow` is editor-only. It draws an efficiency graph directly from the telemetry ring through `BaseAtmosphereLogisticsRuntime.TryGetTelemetryReadOnly`, and its diffusion/inhalation/toxin sliders mutate the live Vault `AtmosphereTuningDTO` with `UnsafeUtility.AsRef` while keeping pending defaults for cold-start boot.

Route card: `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`.
