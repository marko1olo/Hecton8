# Macro Ecosystem Mathematician

Owner: SHINOBU_116.

Runtime owner: `Hecton8.Ecosystem.MacroEcosystemMathematicianRuntime`.
Source anchors: `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`, `Assets/_Project/Scripts/Ecosystem/MacroEcosystemHeatmapGizmo.cs`.

## Source Anchors

Evidence class: STATIC_SOURCE / FILESYSTEM. These anchors prove only that the cited local paths exist at this capture time; they are not compile, Unity import, Play Mode, profiler, GC, player-build, save/load, platform, or visual proof.

- `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`
- `Assets/_Project/Scripts/Ecosystem/MacroEcosystemHeatmapGizmo.cs`
- `Assets/_Project/Data/biome_ecosystem_specs.csv`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-17 R4 Interior Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R45 root/architecture R43/R44 residue/proof-artifact/source-counter correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R45_ROOT_ARCHITECTURE_R43_R44_RESIDUE_PROOF_ARTIFACTS_AND_COUNTERS_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
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
- Per-sector invalid-math flags: `BufferID.ShinobuMacroEcosystemFaultFlags`

## Data Contract

`EcosystemSectorDTO` is fixed at 32 bytes:

- offset 0: `ulong SectorHash`
- offset 8: `uint PreyBiomass`
- offset 12: `uint PredatorBiomass`
- offset 16: `float LocalTemperature`
- offset 20: `float ToxinLevel`
- offsets 24-31: explicit padding

`MacroEcosystemLayoutManifest` validates size and offsets during cold boot/editor load. The same manifest also asserts the contract mirror records used by consumers: `MacroEcosystemSectorVaultRecord`, `MacroEcosystemSectorIndexRecord`, and `MacroEcosystemTuningVaultRecord`.

SHINOBU-touched ecosystem route structs use explicit `Size` and `FieldOffset` without `Pack=1`; binary shape is controlled by explicit offsets, not unaligned packing.

`MacroEcosystemCounterDTO` is fixed at 64 bytes:

- offset 0: `int Value`
- offset 4: `uint Flags`
- offsets 8-63: explicit ulong padding/reserved fields

This prevents adjacent job-written counters from sharing the same L1 cache line.

The sector hash index is not a private native map. It is a Vault-owned open-address table in `ShinobuMacroEcosystemIndexEntries`; biome spec lookup uses the same Vault-owned table pattern in `ShinobuMacroEcosystemBiomeSpecs`. `MacroEcosystemLayoutManifest` asserts sizes and offsets for all macro DTOs that cross Vault/Burst/telemetry boundaries.

Cold boot seeds default biome specs into `ShinobuMacroEcosystemBiomeSpecs` so player builds do not need CSV file probing to get carrying capacity, migration resistance, temperature optimum, or toxin penalty. Editor builds can hot-reload `Assets/_Project/Data/biome_ecosystem_specs.csv` through `ShinobuMacroEcosystemCsvScratch`; the parser writes into the same open-address table.

Consumer assemblies read macro biomass through `Hecton8.Core.Contracts.MacroEcosystemVaultContract` records, not through a concrete `MacroEcosystemMathematicianRuntime` call. The contract records mirror the Vault ABI and keep the route as DataVault/Contracts rather than sibling runtime coupling. Runtime consumers use their already cached `IDataVault` reference plus cached `VaultBufferHandle<T>` metadata; absence of that cache fails closed instead of performing a hot `GlobalRegistry.DataVault` lookup.

`MacroEcosystemTuningDTO.Flags` carries `TuningFlagSnapshotWriteInFlight` while the macro FrostTick chain may write the front sector buffer. Consumers must read tuning before and after the sector read, then fail closed on write-in-flight, `Flags`, `StateHash`, carrying-capacity drift, or temperature-tuning drift; `EcosystemDirector` falls back to legacy local biomass instead of reading torn macro sectors. Same-domain static readers in `MacroEcosystemMathematicianRuntime` apply the same pre/post tuning fence.

## Global Authority Route Card

Route ID: `SHINOBU_116_MACRO_ECOSYSTEM_VAULT_SNAPSHOT`
Date: 2026-05-19
Owner: SHINOBU_116
Owner domain: Echelon 3 Flora, Fauna & Biota / Macro Ecosystem Director
Owning file/system: `MacroEcosystemMathematicianRuntime`
Instrument: `GlobalDataVault / IDataVault`
Producer phase: FrostTick `SIMULATION`, completion patch in `POST_SIMULATION`/LateFrame
Consumer phase: spawn hydration through `IEcosystemDirectorService`
Cadence: producer every FrostTick, consumer expected O(1) query near spawn hydration windows
Payload/data shape: unmanaged explicit-layout sector/index/tuning records, no managed fields, no Unity objects
Capacity: 10,000 sectors, 32,768 index slots, one tuning record, 300 telemetry entries
Overflow policy: bounded sector/index tables fail closed to legacy local biomass fallback; no dynamic expansion or hot allocation is allowed.
Failure mode: absent cached Vault, absent buffers, or write-in-flight flag returns false and lets `EcosystemDirector` use legacy biomass fallback
Telemetry: 300-frame macro ring plus per-sector fault flags and solver microseconds
Shutdown/disposal: Vault-owned buffers, macro holds only `VaultBufferHandle<T>` metadata and unlocks after job completion
Rejected alternatives: direct concrete runtime call, new GlobalRegistry slot for one query, SignalBus broadcast for pull-only biomass, local consumer cache
Why this does not increase monolith risk: macro remains sole writer; consumers receive contract-record snapshots by BufferID, not mutable owner internals
Review disposition: `YELLOW` until Unity compile/runtime/profiler proof exists; static route fields are present and narrow.
Proof required before GREEN: fresh compile/import artifact, macro-ecosystem Play Mode route, profiler/GC proof, DataVault buffer ownership proof, and linked output path with command, timestamp, environment, and result.

## Execution

`FrostTick` schedules `EcosystemPopulationJob`, one to five `BiomassDiffusionJob` passes derived from a polynomial `HomeostasisBrain.GlobalQualityWeight` curve, a front-buffer copy when needed, and a telemetry reduction. The same curve also produces `QualityFlowWeight`, reducing migration amplitude from 0.25 to 1.0 as thermal headroom rises. LV and diffusion jobs consume biome spec capacity/resistance/temperature/toxin scalars. DataVault discovery is cold activation/hot-swap only; the FrostTick path uses the cached `_vault`. `LateFrameTick` completes only finished work, keeps `_jobScheduled` true until the snapshot write flag is cleared, and patches solver microseconds. Telemetry frame index is the deterministic macro simulation tick, not `Time.frameCount`.

Invalid LV math is written per sector to `ShinobuMacroEcosystemFaultFlags`; telemetry reduction ORs those sector flags into the 300-frame black box. No parallel job writes a shared fault counter.

## Consumers

`AmbientBiotaDirector` reads `IEcosystemDirectorService` through `GlobalRegistry`. `EcosystemDirector.TryGetBiomassAvailability` fronts the macro Vault biomass snapshot via cached `_dataVault`, cached Vault handles, and `MacroEcosystemVaultContract` when available, then falls back to legacy ecology only if the macro snapshot is absent. Other spawn/resource systems should route through the ecosystem service or macro contract records instead of owning population counters.

## Verification

Static scan found no `UnityEngine.Physics`, `Physics.`, managed collection creation, coroutine, `Update()`, private persistent `NativeParallelHashMap`, `NativeDisableParallelForRestriction`, runtime `Pack=1`, or `Time.frameCount` in the edited macro files. Latest source checks reported `FROSTTICK_REGISTRY_CLEAN`, `PRE_POST_TUNING_FENCE_WITH_CAPACITY_DRIFT_PRESENT`, `DIRECT_READERS_PRE_POST_TUNING_FENCE_PRESENT`, `BURST_EXACT=7`, and `JOBS=7`. Quality curve source samples using C# positive-float truncation: q=0.10 -> 1 step/0.2500 flow, q=0.29 -> 1 step/0.2763 flow, q=0.50 -> 2 steps/0.4873 flow, q=0.75 -> 4 steps/0.8260 flow, q=1.00 -> 5 steps/1.0000 flow. Build proof is pending because the batch policy forbids launching dotnet/Unity compile while CPU is above 50% or another dotnet/csc process is active; latest CPU samples were 72.2/100/99.2 with no dotnet/csc rows.
