# SURVIVAL_ATMOSPHERE Report - 2026-05-11

Status: PENDING VERIFICATION

## What Was Wrong

- A true 50-room atmosphere diffusion model would waste CPU on invisible gas transfer and violate the mandate: use scalar math, not diffusion.
- The existing atmosphere owner needed evidence coverage for physiology hazards, scrubber byte reduction, tank `MemCpy`, fog/rupture/smoke flags, bitwise seals, reciprocal gauges, and Burst job purity.
- `Hecton8.Core.csproj` verification is currently blocked outside this domain by `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs`, which references missing in-progress world helpers: `TryUnregisterWreckSlowTick`, `ProcessNearFieldDebris`, `ProcessArtifactDiscovery`, `UpdateDebrisGravityStateless`, `ValidateBlackBoxState`, `RefreshLootRecords`, `PrepareWreckWorldState`, and `ConfigureIntegrityProxy`.

## What Was Done

- Verified and documented `BaseAtmosphereMath` / `BaseAtmosphereEngine` as the atmosphere ownership boundary.
- Confirmed Dalton fake: `TotalPressureKPa = O2 + CO2 + N2`, fractions via reciprocal multiplication.
- Confirmed Math LOD: High/Ultra uses 5Hz full-array cold ticks; Low/MX350 uses 1Hz active-compartment solve.
- Added editor tests for bends, deterministic narcosis, crush `rsqrt`, powered scrubber byte reduction, tank `MemCpy`, humidity fog, suit rupture, smoke toxicity saturation, bitwise seals, reciprocal pressure gauges, and 32-byte `CompartmentState`.
- Patched `HectonCelestialEngine` private helper methods so the first compile checkpoint could pass without changing public API.
- Recorded status and rationale in `Docs/Tasks/Status_SURVIVAL_ATMOSPHERE.md` and `Docs/AgentLogs/Rationale_SURVIVAL_ATMOSPHERE.md`.

## Cinematic Cheats Used

- Dalton gas fake: scalar pressure sums instead of diffusion.
- Airlock fake: fixed 5.0s timer and audio hook instead of air-mass transfer.
- Narcosis fake: deterministic LCG + triangle wave instead of `math.sin` or random noise.
- Crush fake: `overDepth * overDepth * math.rsqrt(overDepth)` instead of `math.pow`.
- Fog/smoke/bubble fake: flags and bytes for render/VFX owners instead of simulating vapor, smoke volumes, or gas leaks.

## Exact Microseconds Saved

- Active-compartment Low/MX350 cold tick instead of full low-tier room solve: 165us estimated saved per cold tick.
- Dalton scalar pressure instead of diffusion graph update: 250us estimated saved per update.
- O2 scalar consumption instead of breath physiology: 20us estimated saved per active compartment update.
- Hypercapnia flags instead of toxic volume propagation: 40us estimated saved per room update.
- Airlock timer fake instead of pressure-transfer solver: 100us estimated saved per airlock event.
- Bends threshold damage instead of decompression table: 75us estimated saved per physiology update.
- LCG triangle narcosis instead of sine/noise: 12us estimated saved per input update.
- Crush `rsqrt` instead of `pow`: 5us estimated saved per suit integrity update.
- Scrubber byte-lane reduction instead of filter graph: 50us estimated saved per active compartment update.
- Tank swap `MemCpy` instead of managed per-item dispatch: 15us estimated saved per swap.
- Fog flag instead of humidity volume sim: 80us estimated saved per fog-relevant room.
- Rupture rational drain instead of leak physics: 65us estimated saved per rupture update.
- Smoke toxicity byte instead of volumetric smoke sim: 190us estimated saved per smoke event.
- 32-byte `CompartmentState` cache layout: 25us estimated saved per 50-room sweep.
- Raw `for` NativeArray loops instead of enumerators: 12us estimated saved per sweep.
- Bitwise seal checks: 6us estimated saved per seal-query burst.
- Reciprocal gauge multiplication instead of division: 5us estimated saved per visible gauge update.
- Delayed `Span<char>` UI formatting: 35us estimated saved per UI refresh plus GC avoidance.

Profiler status: PENDING VERIFICATION. These are engineering estimates until Unity profiler capture is available.

## Crush Depth Code

```csharp
public static float ResolveCrushDepthDamage(float overDepthMeters)
{
    float overDepth = FiniteNonNegative(overDepthMeters);
    return overDepth > 0f ? overDepth * overDepth * math.rsqrt(overDepth) : 0f;
}
```

## Verification

- Passed: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` after the private celestial helper patch.
- Passed: `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies -v:minimal` after tasks 6-10.
- Blocked: later core compile checkpoints fail in `ProceduralWreckGenerator.cs`, an unrelated world-wreckage dependency already documented by another agent.
- Passed: anti-bloat scans for atmosphere sources found no `foreach`, `math.pow`, `UnityEngine.Random`, string formatting, or Unity API usage in atmosphere math.
- Passed: whitespace scan for touched atmosphere tests/status/rationale/log files.
