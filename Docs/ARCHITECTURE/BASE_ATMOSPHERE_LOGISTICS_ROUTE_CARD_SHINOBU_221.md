# Base Atmosphere Logistics Route Card - SHINOBU_221

Status: `YELLOW / STATIC SOURCE UPDATED / UNITY PROOF PENDING`

## R48 Exact Route Field Normalization

Route ID: BASE_ATMOSPHERE_LOGISTICS_ROUTE_CARD_SHINOBU_221

Owner: `Hecton8.Atmosphere.BaseAtmosphereLogisticsRuntime`

Instrument: documented route instrument in this file; no new route is accepted from this normalization block alone.

Producer/consumer phase: producer and consumer phases documented below; hot GlobalRegistry polling is forbidden.

Cadence/capacity: bounded cadence/capacity documented below; no hot dynamic allocation or unbounded queue growth is implied.

Overflow/failure: fail closed, clamp/drop/coalesce as documented below, and treat dump paths as planned/generated-on-fault until a timestamped artifact exists.

Shutdown/disposal: owner/Vault/SignalBus lifecycle documented below; visual/debug consumers do not own native memory.

Proof required before GREEN: fresh compile/import, Play Mode route, profiler/GC, platform/player proof where runtime-facing, and linked artifact path with command, timestamp, environment, and output.

Review disposition: YELLOW / STATIC_SOURCE_ONLY.

Owner: `Hecton8.Atmosphere.BaseAtmosphereLogisticsRuntime`

First 20 Minutes route impact: removes global-scalar air quality blocker.

Early habitat corridors can expose local oxygen, carbon dioxide, toxin, and heat gradients.

Compile-wall boundary: owned BaseAtmosphereLogistics runtime/gizmo/jobs/types import Core, Core.Contracts/Signals, Core.Memory, Unity Collections/Jobs/Math/Engine only; they do not import gameplay, construction, world, physics, AI, vehicle, habitat, tool, or power namespaces.

## Fact Routes

- Gas truth: `AtmosphereCellDTO` front/back Vault buffers `71500` and `71501`.

- Graph truth: CSR Vault buffers `71502..71507`.

- Cold fallback graph: mock topology and CSR builder execute through Burst-decorated `IJob.Run()`; no direct job `Execute()` route remains.

- CSR range contract: `EdgeOffsets[i]..EdgeOffsets[i+1]` is the exact adjacency span for node `i`; shifted degree counts are prefix-accumulated before destination writes.

- CSR read guard: diffusion clamps every `EdgeOffsets[i]..EdgeOffsets[i+1]` span into `[0, EdgeCount]` before destination/conductance reads.

- Jacobi contract: diffusion uses `(neighborGasSum + currentGas) / max(sumConductance + 1, 0.0001)` and then applies continuous alpha from tuning/quality cadence; it is not in-place Gauss-Seidel.

- Conservation correction distributes quantization residuals across already-quantized back-buffer cells per gas channel after expected/actual integer totals are known.
- No single cell becomes a permanent rounding sink.

- Source/sink truth: consumers, toxic sources, and vents in Vault buffers `71508..71510`.

- Reactor input: `SignalBus<ReactorDamageSignal>`, unmanaged 64-byte payload, configured capacity 64, minimum-quality frame cap 8. Contract source is `Assets/_Project/Scripts/Core/Contracts/Signals/ReactorDamageSignal.cs`; Atmosphere owns consumption, not payload authority.
- Existing inputs: `SignalBus<FluidIncursionSignal>`, `SignalBus<PlayerBaseEnterSignal>`, `SignalBus<PlayerBaseExitSignal>`.

- Legacy oxygen bridge: `HabitatIntegrityManager` public global O2 reads route to `BaseAtmosphereLogisticsRuntime.TryGetGlobalOxygenSnapshot`; its aggregate fields are fallback-only and stop receiving module contributions once the runtime snapshot is valid.

- Cold tuning input: `Docs/Atmosphere/gas_diffusion_profiles.csv`, parsed from `ReadOnlySpan<byte>` into profile rows with numeric IDs or lowercase FNV-1a module-name hashes.

- Editor facade: `BaseAtmosphereLogisticsTunerWindow` reads the telemetry ring directly for its efficiency graph and writes live tuning through the Vault `AtmosphereTuningDTO`.

- Proof/dump targets: telemetry ring `71513`, shader scalar payload `71520`.
- Fault dump target: `Docs/AgentLogs/Dump_SHINOBU_221.bin`.
- Dump path is generated on fault.
- No runtime artifact is implied without timestamped trigger/output.

## Phase Ownership

- `PreSimulation`: resolve Vault, smooth `GlobalQualityWeight`, apply tuning, ingest typed signal snapshots into Vault source rows.

- `Simulation`: locks every resolved solver input/output lane.
- Schedules clear, breathing, toxic source, vent leak, Jacobi, conservation, quantization, and telemetry jobs.
- Output `JobHandle` returns to dispatcher.
- Dispatcher registers it with `H8Memory.RegisterActiveJob`.

- `PostSimulation`: read completed telemetry, patch solver microseconds, unlock job buffers, dump fault ring on NaN.

- Lock lifetime: active front/back cell buffer IDs are frozen at lock acquisition, so unlock releases the same Vault rows even if the Jacobi loop swaps active handles.

- `VisualSync`: publish one global shader scalar vector for oxygen/CO2/toxin/flow presentation.

- Editor/gizmo debug reads: fail closed while `_simulationScheduled` is true; debug presentation never reads the swapped front buffer before solver completion.

## Buffers

- `71500` `CellsFront`: `AtmosphereCellDTO[1000]`

- `71501` `CellsBack`: `AtmosphereCellDTO[1000]`

- `71502` `Nodes`: `AtmosphereNodeDTO[1000]`

- `71503` `Connections`: `AtmosphereConnectionDTO[2500]`

- `71504` `EdgeOffsets`: `int[1001]`

- `71505` `EdgeDestinations`: `int[5000]`

- `71506` `EdgeConductance`: `float[5000]`

- `71507` `EdgeWriteCursor`: `int[1000]`

- `71508` `Consumers`: `AtmosphereConsumerDTO[128]`

- `71509` `ToxicSources`: `AtmosphereToxicSourceDTO[128]`

- `71510` `Vents`: `AtmosphereVentDTO[64]`

- `71511` `Counters`: `AtmosphereGraphCountersDTO[1]`

- `71512` `Tuning`: `AtmosphereTuningDTO[1]`

- `71513` `TelemetryRing`: `AtmosphereTelemetryEntry[300]`

- `71514..71518` gas/temperature delta lanes: `AtmosphereDeltaLane64[1000]` each

- `71519` `GasRemainders`: `AtmosphereGasRemainderDTO[1000]`

- `71520` `ShaderPayload`: `AtmosphereShaderPayloadDTO[1]`

- `71521` `CsvScratch`: `byte[16384]`

- `71522` `Profiles`: `AtmosphereGasProfileDTO[64]`

## Failure Modes

- Empty or invalid graph: fail closed, mark `EmptyGraph`, keep old fallback oxygen scalar available.

- CSR overflow: mark `CsrOverflow`, clamp edge count to destination/conductance capacity.

- Source overflow: mark `SourceOverflow`, retain bounded source rows only.

- Non-finite gas: sanitize to finite fallback, mark `NaNDetected`, dump telemetry once per continuous fault.

- Vault lock failure: do not schedule simulation; return upstream dependency unchanged.

- Locked lanes during Simulation: active front/back cells, CSR offsets/destinations/conductance, nodes, consumers, toxic sources, vents, counters, tuning, telemetry, gas delta lanes, remainders, and shader payload.

- Residual saturation: if every cell is already at channel capacity or zero, the correction pass clamps remaining residual instead of emitting non-finite gas.

## Review Disposition

`YELLOW`: route shape is documented; source-level checks are STATIC_SOURCE orientation only.

One legal `dotnet build Hecton8.Core.csproj` attempt failed on unrelated external dependency errors outside SHINOBU_221 files.

`GREEN` requires clean compile/import, Play Mode, GCMonitor, profiler, and scene proof after that wall clears.

## R43 Route-Card Fields

| Field | Value |

|---|---|

| Route ID | `SHINOBU_221_BASE_ATMOSPHERE_LOGISTICS` |

| Owner | `Hecton8.Atmosphere.BaseAtmosphereLogisticsRuntime` |

| Instrument | GlobalDataVault buffers `71500..71522`, typed SignalBus ingest snapshots, shader scalar payload `71520`, and black-box telemetry/fault-dump route |

| Producer phase | `PreSimulation` signal ingest and `Simulation` atmosphere solver jobs |

| Consumer phase | `PostSimulation` telemetry/fault readback and `VisualSync` shader scalar publication |

| Cadence | Fixed simulation cadence for gas truth; visual scalar publication only after completed solver output |

| Capacity | 1000 cells, 2500 graph connections, 5000 CSR destinations/conductance rows, 128 consumers, 128 toxic sources, 64 vents, 300 telemetry entries |

| Producer/consumer phase | `PreSimulation` signal ingest and `Simulation` atmosphere solver jobs -> `PostSimulation` telemetry/fault readback and `VisualSync` shader scalar publication |

| Cadence | Fixed gas cadence; visual scalar publish after solver output |
| Capacity | 1000 cells; 2500 graph links; 5000 CSR rows; 128 consumers; 128 toxic sources; 64 vents; 300 telemetry entries |

| Overflow/failure | CSR/source overflow clamps to bounded Vault rows and sets flags; empty graph, non-finite gas, or Vault lock failure fail closed and preserve bounded fallback behavior |

| Overflow policy | CSR/source overflow clamps to bounded Vault rows and sets flags; no dynamic expansion is implied |

| Failure mode | Empty graph, CSR overflow, source overflow, non-finite gas, or Vault lock failure fail closed and preserve bounded fallback behavior |

| Shutdown/disposal | Vault-owned buffers and signal snapshots remain owner-local; teardown must not be owned by visual consumers |

| Fault dump target | `Docs/AgentLogs/Dump_SHINOBU_221.bin` is planned/generated on fault; no existing artifact is implied unless a timestamped runtime trigger and output are linked |

| Proof required before GREEN | Linked artifact path, command/tool, timestamp, environment, and output tuple for compile/import/runtime/profiler claims |

| Review disposition | `YELLOW / STATIC_SOURCE_ONLY` until compile/import/runtime/profiler/player evidence exists |
