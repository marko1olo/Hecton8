# SHINOBU_278 Coop Input Prediction Route Card

Owner: `Hecton8.Core.InputDispatcher` writes local PRE_SIMULATION input. `Hecton8.Networking.HectonRollbackNetcodeRuntime` consumes rollback snapshots and remote authoritative packets.

## Route

- Local hardware input is sampled by the existing input dispatcher phase, converted to `PredictedInputDTO`, and written to `BufferID.ShinobuPredictedInputRing` by `PredictedInputRingWriter.WriteLocalInput` without scheduling a tiny same-frame job.
- `QueueLocalInputJob` remains a deterministic Burst wrapper around the same writer for isolated batch/mock validation, not the PRE_SIMULATION hot route.
- Targeted actions preserve 64-bit AUP in the parallel `PredictedInputAupTargetDTO` ring at `BufferID.ShinobuPredictedInputAupTargets`.
- Rollback reads predicted and remote rings inside `RollbackFixedPipelineJob`; missing remote frames are filled by exponential decay "Dear Lie" extrapolation.
- Predicted input rings are acquired with `UninitializedMemory` and then cold-initialized by `InitializePredictedInputRingJob` into valid idle rows before mock or live producer writes.
- Emergency mock input history uses `Unity.Mathematics.Random` with deterministic `Seed/StartTick/count` hash.
- Mock stream is cold validation data exposed through `InputDispatcher.GenerateMockInputHistory(...)`.
- It adds no local RNG dialect or rollback-side write authority over predicted input truth.
- Designer CSV tuning uses `netcode_input_profiles.csv` cold file polling into Vault scratch.
- Supported rows: `active_profile,<name>`, scoped `<name>,key,value`, default/global/generic scoped rows, simple `key,value`.
- `buffer_capacity` tunes logical active prediction window, not physical Vault ring length.
- `Cooperative Input Tuner` uses a UI Toolkit `Painter2D` telemetry strip for live scalar readout.
- Dirty text annotations throttle to `0.25s`.
- Editor facade reads Vault telemetry; it does not own input truth or change packet/DTO identity.
- Tuner physical ring capacity uses `HectonRollbackNetcodeRuntime.TryGetPredictedInputCapacity(out int)`.
- It is a scalar-only pure read facade.
- Editor no longer requests mutable `NativeArray<PredictedInputDTO>` for display.
- Unused public `TryGetPredictedInputs(...)` was removed after source consumer inventory.
- Mismatches emit `SignalBus<RollbackRequiredSignal>` lane `0x52425153`; `FirstMismatchBufferId` points to the forensic `RollbackNetcodeVault.InputJournalRing` slot, not the raw predicted-input source lane.
- `HectonRollbackNetcodeRuntime` persists only `VaultGenerationHandle<T>` descriptors.
- Scope: rollback-owned and borrowed lanes.
- Mutating phases resolve phase-local views through `TryResolveHandle`.
- Schedule-time buffers refresh stale descriptors only after resolve failure or missing descriptor.
- Public `TryGet*` read accessors use `TryReadHandle`.
- Rollback signal writer opens during cold lane setup only after `OpenQueueForLegacyGlobalSignals()` returns an `IsCreated` native queue.
- Fixed schedule does not reopen the SignalBus writer facade.
- Disable clears cached writer; next owner-phase readiness check recaches through the same cold path.

Vault buffers:
- `BufferID.ShinobuPredictedInputRing = 75000`, `PredictedInputDTO[RollbackNetcodeConstants.InputRingCapacity]`, uninitialized memory, explicit init by producer/mock job.
- `BufferID.ShinobuPredictedInputAupTargets = 75001`, `PredictedInputAupTargetDTO[RollbackNetcodeConstants.InputRingCapacity]`, uninitialized memory.
- `BufferID.ShinobuInputPredictionTelemetry = 75002`, exposed as `RollbackNetcodeVault.InputPredictionTelemetry`, `InputPredictionTelemetryEntry[300]`, clear memory black-box ring.

Proof hooks:
- `PredictedInputLayoutGuard.Validate()` checks 32-byte input DTO offsets and telemetry size.
- `RollbackNetcodeLayoutGuard.Validate()` includes predicted input, remote frame, input journal, telemetry, and rollback signal sizes.
- `Input_Queue_Inquisition` scans whitespace-aware generic declarations for managed input prediction queues and writes `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json`.
- `RollbackTelemetryStripElement` visualizes quality, mismatch severity, resim pressure, packet loss, redundancy, and Dear Lie counts.
- Source: scalar telemetry, not per-editor-tick strings.
- Non-finite scalars collapse to zero visual pressure before drawing.
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
- No quality-gated rollback truth.
- `GlobalQualityWeight` scales prediction window, resend redundancy, optional Merkle leaf budget, and severity/cost curves only.
- Legacy look tuning field is mismatch severity weight, not truth threshold.
