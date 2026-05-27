# Unity Signal Contract Pass UNKNOWN 2026-05-27

Date: 2026-05-27
Agent: UNKNOWN
Evidence class: STATIC_SOURCE_CLASSIFIED
Scope: first-party Unity runtime signal contracts and asmdef route

## Verdict

The confirmed signal-contract errors found in this pass are fixed.

`SignalBusContractAuditCli` after the patch reports:

- scanned C# files: `2439`
- scanned compute files: `71`
- confirmed/probable errors: `0`
- total errors: `0`
- warnings: `529`
- infos: `681`

Raw machine proof is `SIGNAL_BUS_CONTRACT_AUDIT_UNKNOWN_20260527_FINALSCAN.json`.

## What Was Wrong

`SignalBusContractAuditCli` initially reported two `DUPLICATE_RUNTIME_SIGNAL_NAME` errors:

- `Assets/_Project/Scripts/Gameplay/PlayerSignalEvents.cs:72` declared `ToolDepletedSignal`.
- `Assets/_Project/Scripts/Tools/EquipmentThermalBatteryContracts.cs:116` declared `ToolDepletedSignal : ISignal`.

This violated the project rule that signal names are globally unique runtime contracts. The two payloads were not the same fact:

- `Tools.ToolDepletedSignal` is the equipment `SignalBus<T>` payload with battery, frame, power, flags, and grid state.
- `Gameplay.ToolDepletedSignal` was a local player/HUD depletion event with only `ToolHashId`.

The same scan also reported one asmdef warning: `Hecton8.Plugins.asmdef` used signal contracts through `MapMagicRuntimeBridge.cs` without a direct `Hecton8.Core.Contracts` reference.

## What Changed

Renamed the local gameplay event payload:

- `Gameplay.ToolDepletedSignal` -> `Gameplay.PlayerToolDepletedSignal`

Updated all source call sites:

- `PlayerSignalEvents`
- `PlayerToolManager`
- `PlayerStressVFX`
- `SuitHUDV4CanvasOverlay`
- `DiegeticVisorHudMesh`

Kept the authoritative equipment bus payload unchanged:

- `Tools.ToolDepletedSignal : ISignal`

Fixed the assembly contract route:

- added `Hecton8.Core.Contracts` to `Assets/_Project/Scripts/Plugins/Hecton8.Plugins.asmdef`

## Static Proof

Post-patch source scan:

- old gameplay qualified type hits: `0`
- old local `NativeQueue<ToolDepletedSignal>` hits: `0`
- remaining `struct ToolDepletedSignal` in first-party source: only `Tools/EquipmentThermalBatteryContracts.cs`
- brace delta for all touched C# files: `0`
- scoped `git diff --check`: no whitespace errors, line-ending warnings only

Post-patch contract scan:

- command: `SignalBusContractAuditCli --scope Full --include-hot-path-heuristics`
- result: `errors=0`, `confirmedErrors=0`
- asmdef contract boundary hits: `0`

## Build Boundary

Guarded build launched legally with CPU `10.89%` and no active `dotnet`, `csc`, `MSBuild`, or `VBCSCompiler`.

`dotnet build Hecton8.slnx /m:1 /nr:false /p:UseSharedCompilation=false` failed before C# compile:

- log: `BUILD_UNKNOWN_SIGNAL_CONTRACT_PASS_RECHECK_20260527.log`
- summary: `0 Warning(s)`, `62 Error(s)`
- error class: `MSB3202_MISSING_UNITY_GENERATED_CSPROJ`

This is the same generated Unity project-file boundary as the previous UNKNOWN passes. It is not proof of a C# source error in this patch.

## Documentation Gates

- `VerifyDocStructure.py`: `pass=true`, active docs `668`, UTF-8-SIG misses `0`
- `OOP_Doc_Scanner.py`: `finalPass=true`, active files `668`, source sync `true`

## Residuals

The final contract CLI still reports warnings. The important residual classes are:

- `8` registered local native telemetry rings outside GlobalDataVault. These have sentinel coverage but still need owner-route review.
- `8` possible orphaned signal queues. Manual spot-check shows `SuitMeshUpdateEvents` and `VehicleCommandSignalBus` do register queues with `NativeMemorySentinel`, so this class is not automatically a bug.
- `220` hot-path heuristics. These are review rows, not confirmed errors.

No runtime/profiler/player proof was produced in this pass.
