# APEX Final Verification - UNKNOWN Hardware Thermal Owner Route Pass - 2026-05-28

Status: `PENDING_RUNTIME_VERIFICATION`
Evidence class: `STATIC_SOURCE_ONLY`

## What Was Wrong

`HardwareThermalService.SampleAndApplyCold` and `WriteBlackBox` called helpers whose names and behavior mixed hot write-lock access with cold DataVault open/acquire. If a handle was missing, those helpers could reach `EnsureGenerationHandle` from FrostTick/Tick-capable routes.

## What Changed

- `SampleAndApplyCold` now uses `TryAcquireThermalSeverityWriteView` at `HardwareThermalService.cs:295`.
- `WriteBlackBox` now uses `TryAcquireThermalBlackBoxWriteView` at `HardwareThermalService.cs:685`.
- `EnsureNativeState` keeps owner-open behavior through `OpenOrAcquireThermalSeverityWriteViewForOwnerRoute` and `OpenOrAcquireThermalBlackBoxWriteViewForOwnerRoute` at `HardwareThermalService.cs:773` and `:776`.
- Exact old helper names `OpenOrAcquireThermalSeverityWriteView(` and `OpenOrAcquireThermalBlackBoxWriteView(` now scan as `0`.

## DataVault Proof

- `BufferID.HardwareThermalSeverity = 166` at `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:289`.
- `BufferID.HardwareThermalBlackBox = 167` at `Assets/_Project/Scripts/Core/Memory/H8Memory.cs:290`.
- Write-lock acquisition lines: `815`, `848`, `901`, `934`.
- Release lines: `821`, `857`, `867`, `907`, `943`, `953`.
- Active hot write bodies still release through `finally`: severity `299-303`, blackbox `712-716`.

## Zero-GC Static Scan

Added-line scan:

```text
AddedLines=46
ReferenceNewSuspects=0
StringFormat=0
DotToString=0
LinqTokens=0
ForeachTokens=0
CompleteTokens=0
EnsureGenerationHandle=0
GlobalRegistry=0
BinaryLowEndTokens=0
```

Modified hot ranges:

```text
SampleAndApplyCold Lines=269-321 ReferenceNew=0 StructNew=0 StringFormat=0 DotToString=0 Linq=0 Foreach=0 EnsureGenerationHandle=0
WriteBlackBox Lines=683-718 ReferenceNew=0 StructNew=1 StructType=HardwareThermalTelemetryEntry StringFormat=0 DotToString=0 Linq=0 Foreach=0 EnsureGenerationHandle=0
```

The one `new` in `WriteBlackBox` is a 64-byte explicit-layout value struct write, not a managed reference allocation.

## Layout Proof

`HardwareThermalTelemetryEntry` is explicit layout, declared 64 bytes, multiple of 8. Field offsets are `0,4,8,12,14,15,16,17,18,19,20,21,22,23,24,32,40,48,56`.

## Scalability Residual

No new binary quality switch was added. Existing thermal policy still uses `GlobalRegistry.SetTransientLowScalabilityOverride(bool)` at `HardwareThermalService.cs:581-583` and `:610-612`. I did not change that route because `GlobalRegistry.cs` is central/dirty and replacing a boolean registry API must be coordinated with Homeostasis/Registry, not patched locally.

## Build Guard

`dotnet build` invocations: `0`. First CPU/build sample was `100%`; active process: `dotnet.exe PID 65020`. Final recheck sample was `100%`; active processes: `csc.exe PID 28228`, `dotnet.exe PID 46892`. Build skipped by AGENTS compilation throttling rule and by the user's instruction that global compile errors are being handled by another agent.

## Hashes

- Source SHA-256: `A58EF53E751769CA26BD7CA593E57F4042C677C8EBE4B3382D33151929063478`
- JSON SHA-256: `3CF3074A50A62DB74DB866C832FF78E0D0ACB44C3704E78AF9E391FA962CC918`.
