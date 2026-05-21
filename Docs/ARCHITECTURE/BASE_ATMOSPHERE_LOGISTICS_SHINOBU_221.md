# Base Atmosphere Logistics - SHINOBU_221

Owner: `Hecton8.Atmosphere.BaseAtmosphereLogisticsRuntime`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
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
