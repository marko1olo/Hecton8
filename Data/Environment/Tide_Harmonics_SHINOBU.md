# Tide Harmonics SHINOBU Mapping

Status: TIDES BAKED
Evidence Class: OFFLINE_PYTHON_BAKE

## Payload

- Main file: `Data/Environment/Tide_Harmonics.bin`
- Low file: `Data/Environment/Tide_Harmonics_Low.bin`
- Ultra file: `Data/Environment/Tide_Harmonics_Ultra.bin`
- Index file: `Data/Environment/Tide_Harmonics.index.h8bin`
- Manifest file: `Data/Environment/Tide_Harmonics.manifest.json`
- Layout: raw little-endian `float32`, no header
- Index layout: raw little-endian `<8I` records, 32 bytes each, no header
- Main count: 2400 samples
- Main duration: 100 in-game days
- Main cadence: 1 sample per in-game hour
- Main byte size: 9600 bytes
- Alignment: 16-byte blob length, modulo 0
- Hash: `9f126311c293f891580a5e5eb852b347f18739b752e32cf4e34a2c60c1350d5f`
- CRC32: `0xEC586CA4`

## Runtime Sampling Contract

SHINOBU `HectonFluidEngine` must not solve orbital harmonics per frame. The table is cold-loaded by the owning tide/celestial authority, then fluid code consumes the already-resolved tide height through the existing celestial snapshot route.

`H8Time.Time` is interpreted as authoritative simulation seconds. In current source, the equivalent authority is `ITickDispatcher.DilatedTimeSeconds` / `H8TimeSnapshot.Time`, with `GlobalRegistry.AbsoluteUniverseTime` as fallback in existing tide code.

```csharp
// Cold path only: load Tide_Harmonics.bin into fixed native memory once.
const int TideSampleCount = 2400;
const double SecondsToHours = 1.0 / 3600.0;

double tableHour = (H8Time.Time * SecondsToHours) % TideSampleCount;
if (tableHour < 0.0)
    tableHour += TideSampleCount;

int i0 = (int)math.floor(tableHour);
int i1 = i0 + 1;
if (i1 == TideSampleCount)
    i1 = 0;

float t = (float)(tableHour - i0);
float tideHeightMeters = math.lerp(tideTable[i0], tideTable[i1], t);
```

## Integration Boundary

- Preferred owner: `HectonSeismicTideDirector` or the celestial tide authority, not `HectonFluidEngine`.
- Published data: `CelestialRuntimeSnapshot.TideHeightMeters`, `TideHigh01`, and sequence.
- Hot path rule: no per-frame file IO, JSON parsing, string formatting, LINQ, or managed allocation.
- Low tier: 600 samples, 2,400 bytes, 4-hour cadence.
- Main tier: 2,400 samples, 9,600 bytes, 1-hour cadence.
- Ultra tier: 9,600 samples, 38,400 bytes, 15-minute cadence for foam, caustics, bilge alarm timing, and flood-siren polish.

## Physics Basis

- Tide forcing uses `M / r^3` relative equilibrium tide ratios.
- Eccentric anomaly components use first-order `3e` tide modulation.
- Moon constants are sourced from `HectonCelestialEngine.CinematicOrbitDefinition` defaults.
- Phases are fixed-seed and syzygy-anchored so Day 14 and Day 42 are exact local king-tide samples.

## Base Placement Scalars

- Min tide: -4.0503 m
- Max tide: 5.0000 m
- Recommended foundation clearance: 6.5000 m
- Pump-room dry margin: 7.2500 m
- King tide warning threshold: 4.2500 m
