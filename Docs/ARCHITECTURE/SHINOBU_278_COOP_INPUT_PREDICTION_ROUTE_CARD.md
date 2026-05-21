# SHINOBU_278 Coop Input Prediction Route Card

Owner: `Hecton8.Core.InputDispatcher` writes local PRE_SIMULATION input. `Hecton8.Networking.HectonRollbackNetcodeRuntime` consumes rollback snapshots and remote authoritative packets.

Route:
- Local hardware input is sampled by the existing input dispatcher phase, converted to `PredictedInputDTO`, and written to `BufferID.ShinobuPredictedInputRing` by `PredictedInputRingWriter.WriteLocalInput` without scheduling a tiny same-frame job.
- `QueueLocalInputJob` remains a deterministic Burst wrapper around the same writer for isolated batch/mock validation, not the PRE_SIMULATION hot route.
- Targeted actions preserve 64-bit AUP in the parallel `PredictedInputAupTargetDTO` ring at `BufferID.ShinobuPredictedInputAupTargets`.
- Rollback reads predicted and remote rings inside `RollbackFixedPipelineJob`; missing remote frames are filled by exponential decay "Dear Lie" extrapolation.
- Predicted input rings are acquired with `UninitializedMemory` and then cold-initialized by `InitializePredictedInputRingJob` into valid idle rows before mock or live producer writes.
- Emergency mock input history uses `Unity.Mathematics.Random` with a deterministic `Seed/StartTick/count` hash. The mock stream is cold validation data, is exposed through `InputDispatcher.GenerateMockInputHistory(...)`, and does not introduce a local RNG dialect or rollback-side write authority over predicted input truth.
- Designer CSV tuning uses `netcode_input_profiles.csv` cold file polling into Vault scratch. It supports `active_profile,<name>`, scoped `<name>,key,value`, default/global/generic scoped rows, and simple `key,value`; `buffer_capacity` tunes the logical active prediction window, not physical Vault ring length.
- `Cooperative Input Tuner` uses a UI Toolkit `Painter2D` telemetry strip for the live scalar readout and throttles dirty text annotations to 0.25s. The editor facade reads Vault telemetry; it does not own input truth or change packet/DTO identity.
- Tuner physical ring capacity uses `HectonRollbackNetcodeRuntime.TryGetPredictedInputCapacity(out int)`, a scalar-only pure read facade. The editor no longer requests a mutable `NativeArray<PredictedInputDTO>` just to display capacity, and the unused public `TryGetPredictedInputs(...)` escape hatch was removed after source consumer inventory.
- Mismatches emit `SignalBus<RollbackRequiredSignal>` lane `0x52425153`; `FirstMismatchBufferId` points to the forensic `RollbackNetcodeVault.InputJournalRing` slot, not the raw predicted-input source lane.
- `HectonRollbackNetcodeRuntime` persists only `VaultGenerationHandle<T>` descriptors for rollback-owned and borrowed lanes. Mutating phases resolve phase-local views through `TryResolveHandle`; schedule-time bound buffers refresh stale generation descriptors only after a resolve failure or missing descriptor. Public `TryGet*` read accessors use `TryReadHandle`.
- The rollback signal writer is opened during cold lane setup only after `OpenQueueForLegacyGlobalSignals()` returns an `IsCreated` native queue; the fixed schedule does not reopen the SignalBus writer facade. Disable clears the cached writer and the next owner-phase readiness check recaches it through the same cold path.

Vault buffers:
- `BufferID.ShinobuPredictedInputRing = 75000`, `PredictedInputDTO[RollbackNetcodeConstants.InputRingCapacity]`, uninitialized memory, explicit init by producer/mock job.
- `BufferID.ShinobuPredictedInputAupTargets = 75001`, `PredictedInputAupTargetDTO[RollbackNetcodeConstants.InputRingCapacity]`, uninitialized memory.
- `BufferID.ShinobuInputPredictionTelemetry = 75002`, exposed as `RollbackNetcodeVault.InputPredictionTelemetry`, `InputPredictionTelemetryEntry[300]`, clear memory black-box ring.

Proof hooks:
- `PredictedInputLayoutGuard.Validate()` checks 32-byte input DTO offsets and telemetry size.
- `RollbackNetcodeLayoutGuard.Validate()` includes predicted input, remote frame, input journal, telemetry, and rollback signal sizes.
- `Input_Queue_Inquisition` scans whitespace-aware generic declarations for managed input prediction queues and writes `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
- `RollbackTelemetryStripElement` visualizes quality, mismatch severity, resim pressure, packet loss, redundancy, and Dear Lie counts from scalar telemetry without making per-editor-tick strings the live readout; non-finite telemetry scalars collapse to zero visual pressure before drawing.
- `TryGetPredictedInputCapacity()` proves the editor capacity read does not expose predicted-input row mutation rights.
- Crash/NaN/slow prediction dump path is `Docs/AgentLogs/Dump_SHINOBU_278.bin`.

Rejected routes:
- No managed `Queue<InputState>` or `List<InputState>` on the hot path.
- No GlobalRegistry polling inside jobs.
- No same-frame search loop for historical input lookup.
- No hot-path `IJob.Run()` for local input queueing.
- No rollback-side mock writes into dispatcher-owned predicted input lanes.
- No float truncation for target AUP payloads.
- No pointer-bearing `VaultBufferHandle<T>` or obsolete `.Resolve(_vault)` route in the SHINOBU_278 rollback runtime.
- No quality-gated rollback truth. `GlobalQualityWeight` scales prediction window, resend redundancy, optional Merkle leaf budget, and severity/cost curves only; the legacy look tuning field is treated as look mismatch severity weight, not a truth threshold.
