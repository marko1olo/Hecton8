# SHINOBU_338 Airlock Pressurization Route Card

Status: static owner route rollback-polished with KCC intent bridge, explicit completion-gated flush, generated csproj sourcegraph bridge extended; guarded compile currently blocked by CPU threshold.

## Ownership

SHINOBU_338 owns airlock pressure/water/gas exchange math only. It does not own KCC movement, atmosphere graph scheduling, fluid graph scheduling, GPU VFX rendering, or acoustic DSP playback.

## Runtime Route

Cold setup:
`AirlockPressurizationVault.AcquireHandles` -> DataVault BufferIDs `73380..73392` -> cached `AirlockPressurizationVaultHandles`.

Dispatcher phase:

- `AirlockPressurizationVault.ResolveViews` opens owner/write views through `TryResolveHandle`.
- Next: `AdvanceCadence(GlobalQualityWeight)` -> `ScheduleSimulation(input JobHandle)`.
- Returned `JobHandle` registers through `H8Memory.RegisterActiveJob(SystemID.HabitatAtmosphere, handle)`.
- No SHINOBU code calls `.Complete()`.
- Public/editor read accessors remain on `TryReadHandle`.

`AirlockStateDTO[32B]` + `AirlockDoorPoseDTO` + `AirlockTuningDTO`
-> `EvaluateAirlockCyclesJob`
-> `AirlockEvaluationResultDTO`, `BulkheadContainmentIntentDTO`, `BubbleSpawnSignal`, `MovementAcousticSignal`, `AirlockDebugGizmoDTO`
-> owner-phase intent flush / signal flush / editor debug readers.

Layout proof uses explicit `[FieldOffset]` declarations and `UnsafeUtility.SizeOf<T>()`; no SHINOBU route uses `Marshal.OffsetOf` or reflection for DTO offset validation.

`AirlockExchangeIndexDTO` + `AirlockStateDTO` + read-only `AirlockTuningDTO`
-> deterministic ascending-index `IntegrateAirlockExchangeJob`
-> CAS-protected `FluidCompartmentDTO.CurrentWaterVolume/WaterLevelHeight01/Flags`
-> CAS-protected `AtmosphereCellDTO.Oxygen01/CarbonDioxide01/Nitrogen01/Flags`.

- The exchange phase is intentionally `IJob`, not `IJobParallelFor`, because shared Fluid/Atmosphere targets are rollback-critical.
- Parallel CAS preserved memory safety but not deterministic worker ordering when multiple airlocks target the same compartment.
- Exchange schedules when Fluid or Atmosphere owner inputs exist.
- Gas mix uses `AirlockTuningDTO.ChamberVolumeLiters`, not a fixed chamber constant.
- Saturated Fluid target writes restore unapplied water to airlock source.

## Collision Fence

- KCC keeps reading the existing Construction-owned `BufferID.Shinobu220BulkheadCollisionResults` lane.
- SHINOBU does not write that lane directly.
- When pressure or water is unsafe, `EvaluateAirlockCyclesJob` writes `BulkheadContainmentIntentDTO` lock row into lane `73385`.
- Completed owner flush publishes through `BulkheadContainmentIntentBus`.
- `BulkheadContainmentRuntime` converts locked plane into `BulkheadCollisionResultDTO`.
- `PlayerKinematicsRuntime.TryApplyBulkheadCollisionResult` resolves position/velocity.
- Doors are blocked when water or pressure is not equalized.
- No collider enable/disable route is authoritative.

## Presentation Fence

Presentation signal rules:

- Payloads are unmanaged.
- `AbsoluteUniversePosition` is preserved.
- Owner flush consumes each VFX/acoustic row.
- Consumed rows are cleared to `default`.
- Publish uses `SignalBus<T>.TryPush`.
- Duplicate flush cannot replay stale rows.
- VFX/audio density may scale from `GlobalQualityWeight`.
- Consumers must not feed gameplay truth back into the solver.

## First 20 Minutes Route Impact

Unsafe airlock pressure or water level stages bulkhead lock intent before KCC traversal resolves.

Copper Wire / early-base route cannot be bypassed by walking through an unequalized door. Construction still owns collision plane and depth calculation.

## Telemetry

`AirlockTelemetryEntry[300]` records:

- frame, active cycles, water displacement
- max pressure delta, blocked count, signal counts
- fault count, timing proxy

Dump route:

- Trigger: fatal nonfinite or `>200 us`.
- Lane: `DumpRequested` (`73392`).
- Gate: `FlushCompletedOutputs(..., dispatcherCompletionConfirmed)`.
- No proof: returns `false`, publishes nothing.
- With proof: writes `Docs/AgentLogs/Dump_SHINOBU_338.bin`.

## Vault Buffers

`73380` AirlockStateDTO, `73381` AirlockTuningDTO, `73382` AirlockDoorPoseDTO, `73383` AirlockExchangeIndexDTO, `73384` AirlockEvaluationResultDTO, `73385` BulkheadContainmentIntentDTO scratch, `73386` BubbleSpawnSignal, `73387` MovementAcousticSignal, `73388` AirlockTelemetryEntry[300], `73389` telemetry cursor, `73390` AirlockHardwareProfileDTO, `73391` AirlockDebugGizmoDTO, `73392` dump request latch.

## Verification State

Static scanner mirror: 13 relevant non-editor Habitat/Gameplay airlock files, zero forbidden coroutine/Animator/trigger wetting hits.

Source import:

- Unity `.meta` files exist for the SHINOBU-owned AirlockPressurization folder and files.
- Local ignored/generated `Hecton8.Core.csproj` has temporary explicit `Compile Include` rows.
- Bridged owned files: AirlockPressurization sources.
- Bridged references: Core/Atmosphere/Physics contracts.
- Bridged external omissions: `Core/Contracts/Signals/HapticPulseSignal.cs`, `Narrative/HectonNarrativeDirector_PoiTriggers.cs`, `Power/PowerGridSolarContracts.cs`.
- Durable route: Unity regeneration.

- Compile: guarded run failed outside SHINOBU first.
- Guard passed with no active `dotnet`/`csc`/`VBCSCompiler` and CPU samples 24.53/18.21/26.85/49.92/35.13%.
- `dotnet build Hecton8.Core.csproj --no-restore -nologo -clp:ErrorsOnly -m:1 /nr:false` failed with external Core errors: `Core/GlobalSignals.cs` missing `HapticPulseSignal`, `HectonNarrativeDirector.cs` missing `IUpdatable.Tick(float)` and `ILateFrameTickable.LateFrameTick()`, `Gameplay/SolarPanel.cs` missing `SolarPanelStateDTO` and `SolarConditionsDTO`.
- Source investigation found the required types/methods in files omitted from the local generated project, so those files were bridged.
- Follow-up build wrappers skipped launch because CPU samples were 76.83/83.21/98.07/96.33/92.68% and then 43.74/58.77/72.56/56.48/46.04%, above the mandated 50% threshold.
- No SHINOBU_338 file diagnostics were reported, and no green compile claim is made.
