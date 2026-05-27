# Base Atmosphere Logistics - SHINOBU_221

Owner: `Hecton8.Atmosphere.BaseAtmosphereLogisticsRuntime`

Purpose: replace base-wide oxygen reserve reads with local 3D gas logistics authority backed by `GlobalDataVault`.

- Gas truth: 32-byte `AtmosphereCellDTO` cells.
- Solver: CSR graph, double-buffered Jacobi diffusion, Burst.

## Runtime Data

Owner-local Vault IDs are declared in `AtmosphereLogisticsBufferIds` as numeric `BufferID` casts, not as central `H8Memory.BufferID` enum additions.

- Cells: `71500` front, `71501` back

- Graph: `71502` nodes, `71503` connections, `71504` offsets, `71505` destinations, `71506` conductance, `71507` write cursor

- Sources: `71508` consumers, `71509` toxic sources, `71510` vents

- Control/diagnostics: `71511` counters, `71512` tuning, `71513` telemetry, `71519` gas remainders, `71520` shader payload

- Delta lanes: `71514..71518`, each row is `AtmosphereDeltaLane64` padded to 64 bytes to prevent false sharing during atomic source/sink writes

- Cold tuning ingest: `71521` CSV scratch, `71522` gas profiles, source file `Docs/Atmosphere/gas_diffusion_profiles.csv`

Cold fallback generation uses Burst-decorated `IJob.Run()` for both the emergency mock topology and CSR builder. Direct job `Execute()` is not used in the SHINOBU_221 cold graph route.

- CSR offsets use shifted degree counts.
- Connection pass writes counts into `EdgeOffsets[node + 1]`.
- Builder accumulates `EdgeOffsets[1..nodeCount]` into cumulative end offsets.
- `EdgeOffsets[i]..EdgeOffsets[i+1]` remains the adjacency range for node `i`.

## Signal Inputs

- `PlayerBaseEnterSignal` / `PlayerBaseExitSignal`: maintains the active player breathing consumer.

- `FluidIncursionSignal`: injects breach oxygen drain, CO2, toxin, and cold shock.

- `ReactorDamageSignal`: published by `BioReactor` during overheat/meltdown.
- Effects: toxin, CO2, O2 drain, heat.
- Payload path: `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs`.
- Payload is not in Atmosphere assembly.
- Reactor/gameplay consumers do not depend on Atmosphere runtime.

- Legacy `HabitatIntegrityManager` global oxygen is fallback-only.
- With valid atmosphere snapshot, module syncing removes old fallback contribution and stops maintaining a competing aggregate.

## Determinism And Scale

- Solver jobs use `FloatMode.Deterministic`.

- Diffusion uses self-weighted Jacobi: `(neighborGasSum + currentGas) / max(sumConductance + 1, 0.0001)` before continuous alpha blending and source/sink delta application.

- Diffusion clamps each node's CSR range to `[0, EdgeCount]` before reading edge destinations/conductance, so stale or corrupted offsets fail bounded instead of reading outside the lane.

- Iteration count is continuous: `(int)math.lerp(1f, 8f, smoothed GlobalQualityWeight)`.

- Runtime quality uses a smoothstep hysteresis filter before writing the tuning DTO, so thermal pressure sheds solver cost faster than it recovers and avoids one-frame iteration flicker.

- AUP positions stay as `double3`; jobs subtract source/node AUP in double before casting local deltas to `float3`.

- Gas changes enter integer delta lanes.
- Quantization uses million-unit remainders.
- Conservation residuals become bounded distributed back-buffer corrections per gas channel.
- No one-cell anchor correction.

- Gas profile CSV rows accept either numeric profile IDs or module type names. Module names are hashed with allocation-free lowercase FNV-1a over `ReadOnlySpan<byte>`.

- CSV malformed diagnostics are per-row (`rowMalformed`) with an aggregate file result, so one bad authored row does not poison later valid profile DTO flags.

- Simulation locks all job-read and job-write Vault lanes used by the scheduled solver, including read-only topology/source/tuning rows, and releases them only in `PostSimulation`.

- Active front/back cell `BufferID`s are captured at lock acquisition.
- Unlock reuses the captured IDs.
- Odd Jacobi iteration counts can swap `_frontCells`/`_backCells`.
- Originally locked Vault rows do not leak.

- Editor and gizmo read APIs fail closed while `_simulationScheduled` is true.
- Reason: active front handle swaps before the scheduled job necessarily finishes writing the new front buffer.

## Black Box

The runtime writes a fixed 300-frame `AtmosphereTelemetryEntry` ring.

Fields: node count, max CO2, average O2, max toxin, iterations, microseconds, state hash. NaN dumps to `Docs/AgentLogs/Dump_SHINOBU_221.bin`.

## Editor Facade

- `BaseAtmosphereLogisticsTunerWindow` is editor-only.
- It draws an efficiency graph through `BaseAtmosphereLogisticsRuntime.TryGetTelemetryReadOnly`.
- Diffusion/inhalation/toxin sliders mutate live Vault `AtmosphereTuningDTO` with `UnsafeUtility.AsRef`.
- Pending defaults remain for cold-start boot.

Route card: `Docs/ARCHITECTURE/BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221.md`.
