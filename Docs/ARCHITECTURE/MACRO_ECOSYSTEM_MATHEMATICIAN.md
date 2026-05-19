# Macro Ecosystem Mathematician

Owner: SHINOBU_116.

Runtime owner: `Hecton8.Ecosystem.MacroEcosystemMathematicianRuntime`.

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

The macro ecosystem is a headless FrostTick simulation. It owns no scene spawners, does not call `UnityEngine.Physics`, and writes prey/predator biomass as unmanaged sector records in `GlobalDataVault`.

## Vault Route

- Front sectors: `BufferID.ShinobuMacroEcosystemSectorFront`
- Back sectors: `BufferID.ShinobuMacroEcosystemSectorBack`
- Fractional carry: `BufferID.ShinobuMacroEcosystemRemainders`
- 64-bit sector coordinates: `BufferID.ShinobuMacroEcosystemSectorCoords`
- Sector hash index entries: `BufferID.ShinobuMacroEcosystemIndexEntries`
- Biome CSV specs: `BufferID.ShinobuMacroEcosystemBiomeSpecs`
- Tuning: `BufferID.ShinobuMacroEcosystemTuning`
- Counters: `BufferID.ShinobuMacroEcosystemCounters` using 64-byte padded `MacroEcosystemCounterDTO`
- Black box telemetry: `BufferID.ShinobuMacroEcosystemTelemetryRing`
- CSV scratch: `BufferID.ShinobuMacroEcosystemCsvScratch`

## Data Contract

`EcosystemSectorDTO` is fixed at 32 bytes:

- offset 0: `ulong SectorHash`
- offset 8: `uint PreyBiomass`
- offset 12: `uint PredatorBiomass`
- offset 16: `float LocalTemperature`
- offset 20: `float ToxinLevel`
- offsets 24-31: explicit padding

`MacroEcosystemLayoutManifest` validates size and offsets during cold boot/editor load.

`MacroEcosystemCounterDTO` is fixed at 64 bytes:

- offset 0: `int Value`
- offset 4: `uint Flags`
- offsets 8-63: explicit ulong padding/reserved fields

This prevents adjacent job-written counters from sharing the same L1 cache line.

The sector hash index is not a private native map. It is a Vault-owned open-address table in `ShinobuMacroEcosystemIndexEntries`; biome spec lookup uses the same Vault-owned table pattern in `ShinobuMacroEcosystemBiomeSpecs`.

## Execution

`FrostTick` schedules `EcosystemPopulationJob`, one to five `BiomassDiffusionJob` passes derived from `HomeostasisBrain.GlobalQualityWeight`, a front-buffer copy when needed, and a telemetry reduction. `LateFrameTick` completes only finished work and patches solver microseconds. Telemetry frame index is the deterministic macro simulation tick, not `Time.frameCount`.

## Consumers

`AmbientBiotaDirector` reads `IEcosystemDirectorService` through `GlobalRegistry`. `EcosystemDirector.TryGetBiomassAvailability` fronts the macro Vault biomass snapshot when available and falls back to legacy ecology only if the macro snapshot is absent. Other spawn/resource systems should route through the ecosystem service or macro spawn-weight method instead of owning population counters.

## Verification

Static scan found no `UnityEngine.Physics`, `Physics.`, managed collection creation, coroutine, `Update()`, private persistent `NativeParallelHashMap`, or `Time.frameCount` in the edited macro files. Build proof is pending because local CPU load is 100%, and the batch policy forbids launching dotnet/Unity compile while CPU is above 50%.
