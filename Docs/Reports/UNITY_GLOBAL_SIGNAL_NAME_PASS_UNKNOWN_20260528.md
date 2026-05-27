# Unity Global Signal Name Pass - UNKNOWN - 2026-05-28

Status: STATIC SOURCE PROOF ONLY / RUNTIME PENDING

Domain: Core & Memory Infrastructure / SignalBus Contracts / Zero-GC Runtime Architecture, with cross-domain DTO name hygiene because the SignalBus audit treats signal-like names as global AOT/operator identifiers.

## Problem

The fresh SignalBus audit reported `68` warnings after the Core sync-IO pass. Core subtree warnings were already closed, but two global contract categories remained:

- `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=8`
- `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=3`

The duplicate DTO names were not C# namespace compile defects. They were global telemetry/operator defects: tooling, AOT reports, dump readers, and route ledgers cannot safely treat two unrelated telemetry structs with the same short name as one unambiguous contract.

## Fix

Renamed only local/private or narrowly owned DTOs. No field layout, offset, size, BufferID, SystemID, capacity, or runtime behavior was changed.

- `Hecton8.Physics.OceanSurfaceTelemetryEntry` -> `FluidOceanSurfaceTelemetryEntry`
- `SubmarineStructuralGrid.StructuralTelemetryEntry` -> `SubmarineStructuralTelemetryEntry`
- `AbyssalThermalManager.ThermalTelemetryEntry` -> `AbyssalThermalManagerTelemetryEntry`
- `GasDynamicsSolver.AtmosphereTelemetryEntry` -> `GasDynamicsTelemetryEntry`
- `SystemDiagnosticsBoard.TelemetrySnapshotRow` -> `CrashSnapshotRow`

The public/root DTO names that remain are now unique in the first-party source scan:

- `AtmosphereTelemetryEntry`
- `OceanSurfaceTelemetryEntry`
- `StructuralTelemetryEntry`
- `ThermalTelemetryEntry`

## Source Files Changed

- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Scripts/SubmarineStructuralGrid.cs`
- `Assets/_Project/Scripts/World/AbyssalThermalManager.cs`
- `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs`
- `Assets/_Project/Scripts/Atmosphere/AtmosphereMemorySovereigntyValidator1324.cs`
- `Assets/_Project/Scripts/Editor/SystemDiagnosticsBoard.cs`

## Proof

- Exact old struct-name scan now finds one owner per old name, not duplicate owners.
- Touched source brace balance: `0`.
- `git diff --check` on touched source: exit `0`; line-ending warnings only.
- `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260528_GLOBAL_SIGNAL_NAME_RECHECK.json`:
  - `files=2443`
  - `shaders=71`
  - `errors=0`
  - `confirmedErrors=0`
  - `warnings=57`
  - `infos=1024`
- Warning categories after this pass:
  - `RUNTIME_SYNC_FILE_IO_REVIEW=57`
  - `DUPLICATE_SIGNAL_LIKE_NAME_REVIEW=0`
  - `EDITOR_MANAGED_STRING_IN_SIGNAL_REVIEW=0`

## Residuals

- The remaining `57` warnings are synchronous file IO review items spread across AI, input, localization, meta, narrative, QA, quest, rendering, UI, VFX, visor, world, and thermodynamics files. They are not Core subtree findings from this pass.
- Full `Hecton8.slnx` build was not run by this agent; overall compile errors remain owned by another agent per user instruction.
- No Unity Editor import, Play Mode, profiler, GC, save/load, or player-build proof was produced.

## Hardware Impact

Runtime microseconds saved claimed: `0`. No profiler/player proof.

Low-tier value is avoiding ambiguous telemetry contract names in crash/operator tooling. Middle, high, and ultra tiers keep identical runtime layout and behavior while global report identifiers become deterministic.
