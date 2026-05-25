# [ARCHIVE] Pre-Strict Architecture Snapshot

Date: 2026-05-24
Owner: X_012 DOCUMENTATION_CLEANUP_AND_ACTUALIZATION_ENGINE
Original: Docs/ARCHITECTURE/SHINOBU_340_PIPE_DRAINAGE_ROUTE_CARD.md
Rule: historical snapshot only; not active doctrine.

# SHINOBU_340 Pipe Drainage Route Card

Owner: Construction / PIPE_CONDUCTANCE_SUMP_PUMP_FLOW

## Authority

`SumpPumpPipeGridRuntime` is the drainage authority for mathematical sump-pump evacuation. It transforms Vault-owned graph, pressure, power, and fluid-compartment lanes through Burst jobs. It does not query scene pumps or pipe hierarchies in the hot path.

## Vault Lanes

- `95820` `PumpNodes`: `DrainageNodeDTO`, 32 bytes, exact offsets 0/4/8/12/16 plus 12-byte padding.
- `95821` `PipeEdges`: flat directed pipe inputs, `PipeEdgeDTO`, 64 bytes.
- `95822` `NodeAup`: `double3` absolute universe positions.
- `95824..95829`: CSR offsets, destinations, conductance, flow, flat-edge index, write cursor.
- `95830` / `95831`: pressure front/back, allocated with `NativeArrayOptions.UninitializedMemory`.
- `95832`: local power potential snapshot for drainage jobs.
- `95833`, `95842`, `95843`: pump remainder, mass error, padded room locks.
- `95834`: `DrainageTuningDTO`, 80 bytes.
- `95835`, `95836`, `95840`: 300-frame telemetry ring (`DrainageTelemetryEntry`, 64 bytes), cursor, frame summary.
- `95841`: `DrainagePipeFlowGpuDTO`, 16-byte structured-buffer payload.
- `95844`, `95845`: pump base max rate and optional power-node hash mapping.

## Cross-Domain Inputs

- Fluid truth: `BufferID.ShinobuFluidCompartmentFront` and `BufferID.ShinobuFluidCompartmentBack`.
- Power truth: `PowerGridBufferIds.Nodes` and `PowerGridBufferIds.PotentialFront`.

Missing fluid or power lanes do not invent fallback truth. Telemetry flags the missing route and pumps evacuate zero water when power potential is unavailable.

YELLOW contract debt: current code no longer has literal `using Hecton8.Physics;` / `using Hecton8.Power;` imports, but it still references exact fully qualified Physics/Power DTOs and BufferIDs because GlobalDataVault validates the original runtime type identity. There is no Construction asmdef in the current tree, so this is not an immediate sibling-asmdef violation. Before a future Construction runtime asmdef split, those DTOs/IDs must move behind `Hecton8.*.Contracts` or equivalent shared contract facades.

## Job Chain

1. `GenerateMockPipeNetworkJob`: cold deterministic stress topology, 2000 nodes / 6000 edges.
2. `BuildCsrPipeGraphJob`: flat CSR build, sealed edges rejected.
3. `ApplyPumpPowerConstraintJob`: power potential clamps pump max rate.
4. `EvaluatePipePressureJob`: deterministic Burst Jacobi, CSR read, pressure front/back write, AUP gravity multiplier.
5. `PipeEdgeFlowJob`: Dear Lie GPU flow scalar.
6. `ExecuteWaterEvacuationJob`: reads `FrontCompartments` snapshot, atomically deducts only `BackCompartments` under 64-byte room locks.
7. `DrainageTelemetryRecorderJob`: fixed 300-frame ring and state hash.
8. `RecordTelemetryHeartbeat`: owner-phase LateFrame heartbeat row when no mock/solver job is scheduled, preserving last solved state while advancing blackbox frame coverage without `Complete()`.

## Quality Scaling

`HomeostasisBrain.GlobalQualityWeight` only changes solve cadence, Jacobi iteration count, and managed spline-flow presentation budget through continuous `smoothstep`/`lerp` curves. It does not alter DTO layout, BufferIDs, save identity, fluid authority, power authority, or compartment ownership.

## Black Box

Telemetry requests `Docs/AgentLogs/Dump_SHINOBU_340.bin` on non-finite state or solver wall time over 0.5 ms. `SolverWallMicroseconds` carries `ScheduleWindowTiming`, meaning scheduler-to-finalize wall timing; exact Burst-worker timing remains profiler/SystemDispatcher integration work and is not fabricated through hidden completion. Solver frames write telemetry through `DrainageTelemetryRecorderJob`; non-solve LateFrame windows write `HeartbeatFrame` rows so the 300-frame ring still captures frame progression while quality/cadence throttles the solver. Heartbeat rows zero per-frame evacuation and solver wall time, preserve total drained water and last pressure summary, and do not perform file I/O or job completion. The fault branch copies a raw snapshot into a cold preallocated byte buffer and signals a background writer; path creation and `FileStream` are not executed from `LateFrameTick`. The dump is raw fixed-row binary for forensic replay: 64-byte `DrainageDumpHeader` starting with 8-byte `HECTON8\0`, followed by oldest-to-newest 64-byte `DrainageTelemetryEntry` rows. `BinaryWriter` is not used.

## Accessor Discipline

`TryReadTuning()` is pure. It does not allocate, grow Vault buffers, poll GlobalRegistry, complete jobs, or mutate global state. Runtime caches `GlobalRegistry.DataVault` in `OnEnable()` and performs buffer creation only in cold initialization/write/setup paths.

Editor pressure x-ray does not poll `GlobalRegistry.DataVault` directly. It calls the runtime-owned `TryCopyPressureDebugSnapshot()` facade, which refuses reads while solver or mock jobs are scheduled and copies into editor-owned static arrays.

## Human Tuning Bridge

`Hydraulic Sump Tuner` exposes live tuning sliders plus a Pipe Profile CSV Bridge. Designers can select a CSV source, select a `.h8bin` output path, view schema version, source hash, row count, validation code, row, column, field, and `PipeProfileDTO` layout status, import the CSV into the active runtime through `TryLoadPipeProfilesFromCsvBytes()`, or bake deterministic binary profile rows. The bake header is 64 bytes and each `PipeProfileDTO` row is 32 bytes. Binary bake uses temp-write, flush, readback validation of magic/schema/count/stride/source hash/layout hash, then `File.Replace`/`File.Move` publication; direct overwrite of the active output is not used. This is editor-only cold work; runtime solver cadence does not poll files.
