# Route_SHINOBU_220_BulkheadContainment

Owner: `SystemID.Construction`
Domain: ECHELON 6 HABITAT & VEHICLES / BASE CONTAINMENT
Dispatcher phases: PreSimulation, Simulation, PostSimulation, VisualSync

## DataVault Buffers
- `Shinobu220BulkheadStates` (72000): `BulkheadStateDTO[256]`, exact 32-byte prompt layout.
- `Shinobu220BulkheadAups` (72001): `double3[256]`, AUP center per emergency bulkhead.
- `Shinobu220BulkheadPlanes` (72002): `BulkheadPlaneDTO[256]`, KCC mathematical blocking planes.
- `Shinobu220BulkheadCsrEdges` (72003): CSR edge scalar routing metadata.
- `Shinobu220BulkheadEdgeConductivity` (72004): CSR conductivity scalar lane.
- `Shinobu220BulkheadFluidFlow` (72005): CSR fluid-flow scalar lane.
- `Shinobu220BulkheadTuning` (72006): continuous quality/cadence tuning.
- `Shinobu220BulkheadTelemetryRing` (72007): last 300 frame black-box telemetry.
- `Shinobu220BulkheadTelemetryCursor` (72008): telemetry cursor.
- `Shinobu220BulkheadCollisionResults` (72009): one-frame KCC blocking result.
- `Shinobu220BulkheadProfiles` (72010): allocation-free CSV parsed profiles.
- `Shinobu220BulkheadCsvScratch` (72011): profile scratch.
- `Shinobu220BulkheadShaderUpload` (72012): shader upload staging lane.
- `Shinobu220BulkheadModuleIntegrity` (72013): normalized parent module integrity, consumed by catastrophic damage.
- `Shinobu220BulkheadIntentRing` (72014): `BulkheadContainmentIntentDTO[256]`, unmanaged ingress from BaseAirlock.
- `Shinobu220BulkheadIntentControl` (72015): `BulkheadContainmentIntentControlDTO[1]`, exact 64-byte write/read cursor row.

## Boundary Rule
`BaseAirlock` publishes edge lock intent and normalized parent integrity as `BulkheadContainmentIntentDTO` packets through `Hecton8.Core.Contracts.BulkheadContainmentIntentBus`. It does not import `Hecton8.Construction`, does not hold a Construction object reference, and does not own the state lane or any local closure-progress scalar. `BulkheadContainmentRuntime` consumes the ingress ring in `PreSimulation`, then owns closure, CSR sealing, KCC plane collision result, shader upload, telemetry, and dump path `Docs/AgentLogs/Dump_SHINOBU_220.bin`.

Manual override is a typed signal snapshot route, not a Vault queue alias. `BulkheadContainmentRuntime` reads the current `SignalBus<InteractionUiSignal>` frame snapshot, filters to `ToolHash == BulkheadContainmentConstants.OverrideToolHash`, and schedules the override mutation only when a matching signal exists. The gameplay `InteractionSignalQueue` Vault lane remains owned by the interaction/tools domain and is not read as bulkhead UI input.

Persistent SHINOBU Vault handles are allocated by cold owner bootstrap/rebind commit through `BootstrapVaultState`. Dispatcher phases call `RefreshVaultState`, which resolves and validates existing handles and writes the tuning row without creating or growing Vault lanes.

`PlayerKinematicsRuntime` consumes `Shinobu220BulkheadCollisionResults` as a data-only KCC correction: it projects player position/velocity out of closed bulkhead planes without Unity colliders or direct Construction object references.

CSR graph sealing and KCC blocking intentionally use different thresholds: KCC becomes solid at `ClosureProgress > 0.5`, while logistics/fluid CSR coefficients drop to `0.0` only at `ClosureProgress >= 0.95`. Destroyed bulkheads clear `SiblingNodeHash`, stay visually mangled at `0.73`, and leak because CSR coefficients reopen on the same authority tick.
