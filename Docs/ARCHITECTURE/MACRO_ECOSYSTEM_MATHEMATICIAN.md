# Macro Ecosystem Mathematician



Owner: SHINOBU_300. SHINOBU_116 remains historical route context only.



Runtime owner: `Hecton8.Ecosystem.MacroEcosystemMathematicianRuntime`.



Source anchors: `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`, `Assets/_Project/Scripts/Ecosystem/MacroEcosystemHeatmapGizmo.cs`.



## Source Anchors



Evidence: STATIC_SOURCE / FILESYSTEM.

Scope: cited local paths exist at capture time. No compile/import/Play/profiler/GC/player/save/platform/visual proof.



- `Assets/_Project/Scripts/Ecosystem/MacroEcosystemMathematicianRuntime.cs`

- `Assets/_Project/Scripts/Ecosystem/MacroEcosystemHeatmapGizmo.cs`

- `Assets/_Project/Data/macro_ecosystem_coefficients.csv`

- `Assets/_Project/Data/biome_ecosystem_specs.csv` legacy editor fallback



The macro ecosystem is a headless FrostTick simulation. It owns no scene spawners, does not call `UnityEngine.Physics`, and writes flora/prey/predator biomass as unmanaged sector records in `GlobalDataVault`.



## Vault Route



- Front sectors: `BufferID.ShinobuMacroEcosystemSectorFront`



- Back sectors: `BufferID.ShinobuMacroEcosystemSectorBack`



- Fractional carry: `BufferID.ShinobuMacroEcosystemRemainders`



- 64-bit sector coordinates: `BufferID.ShinobuMacroEcosystemSectorCoords`



- Sector hash index entries: `BufferID.ShinobuMacroEcosystemIndexEntries`



- Biome coefficient specs: `BufferID.ShinobuMacroEcosystemBiomeSpecs`

- Tuning: `BufferID.ShinobuMacroEcosystemTuning`



- Counters: `BufferID.ShinobuMacroEcosystemCounters` using 64-byte padded `MacroEcosystemCounterDTO`



- Black box telemetry: `BufferID.ShinobuMacroEcosystemTelemetryRing`



- CSV scratch: `BufferID.ShinobuMacroEcosystemCsvScratch`



- Per-sector invalid-math flags: `BufferID.ShinobuMacroEcosystemFaultFlags`



## Data Contract



`Hecton8.Core.Contracts.EcosystemSectorDTO` is the canonical hot Vault type and is fixed at 64 bytes:



- offset 0: `ulong SectorHash`

- offset 8: `float FloraBiomass`

- offset 12: `float PreyBiomass`

- offset 16: `float PredatorBiomass`

- offset 20: `float CarryingCapacity`

- offset 24: `uint DominantSpeciesMask`

- offsets 28-63: explicit padding



`MacroEcosystemLayoutManifest` validates size and offsets during cold boot/editor load.

It also asserts legacy/cold contract mirrors: `MacroEcosystemSectorVaultRecord`, `MacroEcosystemSectorIndexRecord`, `MacroEcosystemTuningVaultRecord`.

Hot sector buffer type must be exactly `EcosystemSectorDTO`; `GlobalDataVault` validates generic type hash, not only byte size.



SHINOBU-touched ecosystem route structs use explicit `Size` and `FieldOffset` without `Pack=1`; binary shape is controlled by explicit offsets, not unaligned packing.



`MacroEcosystemCounterDTO` is fixed at 64 bytes:



- offset 0: `int Value`



- offset 4: `uint Flags`



- offsets 8-63: explicit ulong padding/reserved fields



This prevents adjacent job-written counters from sharing the same L1 cache line.



`BiomeEcosystemSpecDTO` is fixed at 64 bytes:



- offset 0: `uint BiomeHash`

- offset 4: `float CarryingCapacityPrey`

- offset 8: `float CarryingCapacityPredator`

- offset 12: `float MigrationResistance`

- offset 16: `float TemperatureOptimum`

- offset 20: `float ToxinPenalty`

- offset 24: `float BaseBirthRate` alpha override

- offset 28: `float PredationRate` beta override

- offset 32: `float PredatorConversionRate` delta override

- offset 36: `float PredatorStarvationRate` gamma override

- offsets 40-63: explicit padding



- `MacroEcosystemTelemetryEntry` is fixed at 64 bytes.
- Offset 28 stores `SolverMicroseconds` as the exact dispatcher-observed elapsed window from FrostTick schedule to `DispatcherJobFence` finalization.
- Offset 56 stores `TimingMode = 1` and offset 60 stores `TimingSourceHash = 0x53574643` (`SWFC`) so readers can distinguish this deterministic wall-clock solve-chain sample from a platform-specific CPU cycle counter.
- The runtime does not read a timestamp inside Burst jobs because that would introduce non-deterministic platform timing into rollback-relevant code.



The sector hash index is not a private native map.

It is a Vault-owned open-address table in `ShinobuMacroEcosystemIndexEntries`. Biome spec lookup uses the same pattern in `ShinobuMacroEcosystemBiomeSpecs`.

`MacroEcosystemLayoutManifest` asserts sizes and offsets for macro DTOs crossing Vault/Burst/telemetry boundaries.



- Cold boot seeds default biome specs into `ShinobuMacroEcosystemBiomeSpecs` so player builds do not need CSV file probing to get carrying capacity, migration resistance, temperature optimum, or toxin penalty.
- Editor builds hot-reload `Assets/_Project/Data/macro_ecosystem_coefficients.csv` through `ShinobuMacroEcosystemCsvScratch`, with `biome_ecosystem_specs.csv` retained only as a legacy fallback.
- CSV columns are `biome, prey_capacity, predator_capacity, migration_resistance, temperature_optimum, toxin_penalty, alpha, beta, delta, gamma`; the last four fields are optional, and missing/non-positive coefficient values fall back to the live Vault tuning row.



- Consumer assemblies read macro biomass through `Hecton8.Core.Contracts.EcosystemSectorDTO`, `MacroEcosystemSectorIndexRecord`, `MacroEcosystemTuningVaultRecord`, and `MacroEcosystemVaultContract`, not through a concrete `MacroEcosystemMathematicianRuntime` call.
- These contract records keep the route as DataVault/Contracts rather than sibling runtime coupling.
- Runtime consumers use their already cached `IDataVault` reference plus cached `VaultGenerationHandle<T>` metadata; absence of that cache fails closed instead of performing a hot `GlobalRegistry.DataVault` lookup.



`AI/Ecosystem/ShinobuEcosystemBalancer` still contains a legacy 32-byte entity-sector fallback for swarm hydration.

It checks for canonical `ShinobuMacroEcosystemSectorFront` before scheduling legacy `LotkaVolterraMacroJob`. If SHINOBU_300 exists, the legacy macro pass is skipped.



- `MacroEcosystemTuningDTO.Flags` carries `TuningFlagSnapshotWriteInFlight` while the macro FrostTick chain may write the front sector buffer.
- Consumers read tuning before and after sector read.
- Fail-closed triggers: write-in-flight, `Flags`, `StateHash`, carrying-capacity drift.
- `EcosystemDirector` falls back to legacy local biomass.
- It does not read torn macro sectors.
- Same-domain static readers in `MacroEcosystemMathematicianRuntime` apply the same pre/post tuning fence.



- Macro sector identity is a horizontal regional biomass layer.
- Sector lookup hashes absolute AUP `X/Z` in double precision and intentionally collapses `Y` to `0`; depth effects belong in biome/profile/presentation scalars, not in the macro sector key.
- This matches the seeded mock grid and `StressDrivenSpawnDirector` hash route, preventing underwater AUP reads from missing the flat macro layer.



## Global Authority Route Card



Route ID: `SHINOBU_300_MACRO_ECOSYSTEM_VAULT_SNAPSHOT`

Date: 2026-05-22

Owner: SHINOBU_300

Owner domain: Echelon 3 Flora, Fauna & Biota / Macro Ecosystem Director



Owning file/system: `MacroEcosystemMathematicianRuntime`



Instrument: `GlobalDataVault / IDataVault`



Producer/consumer phase: FrostTick `SIMULATION` producer and `POST_SIMULATION`/LateFrame completion patch -> spawn hydration and ecosystem consumers through cached contract-record snapshots.



Cadence/capacity: FrostTick cadence; 10,000 64-byte sectors, 32,768 index slots, one tuning record, and 300 telemetry entries.

Overflow/failure: bounded sector/index tables fail closed to legacy local biomass fallback; absent cached Vault, absent buffers, or write-in-flight flags return false.



Producer phase: FrostTick `SIMULATION`, completion patch in `POST_SIMULATION`/LateFrame



Consumer phase: spawn hydration through `IEcosystemDirectorService`



Cadence: producer every FrostTick, consumer expected O(1) query near spawn hydration windows



Payload/data shape: unmanaged explicit-layout sector/index/tuning records, no managed fields, no Unity objects

Capacity: 10,000 sectors, 32,768 index slots, one tuning record, 300 telemetry entries



Overflow policy: bounded sector/index tables fail closed to legacy local biomass fallback; no dynamic expansion or hot allocation is allowed.



Failure mode: absent cached Vault, absent buffers, or write-in-flight flag returns false and lets `EcosystemDirector` use legacy biomass fallback



Telemetry: 300-frame macro ring plus per-sector fault flags, solver microseconds, and explicit timing source fields

Shutdown/disposal: Vault-owned buffers, macro holds only `VaultGenerationHandle<T>` metadata and unlocks after job completion

Rejected alternatives: direct concrete runtime call, new GlobalRegistry slot for one query, SignalBus broadcast for pull-only biomass, local consumer cache



Why this does not increase monolith risk: macro remains sole writer; consumers receive contract-record snapshots by BufferID, not mutable owner internals



Review disposition: `YELLOW` until Unity compile/runtime/profiler proof exists; static route fields are present and narrow.



Proof required before GREEN: fresh compile/import artifact, macro-ecosystem Play Mode route, profiler/GC proof, DataVault buffer ownership proof, and linked output path with command, timestamp, environment, and result.



## Execution



- `FrostTick` schedules `EcosystemPopulationJob`.
- It schedules one to six Lotka-Volterra/logistic substeps from polynomial `HomeostasisBrain.GlobalQualityWeight`.
- It schedules one to five adjacent-sector `BiomassDiffusionJob` passes when quality/cadence allows.
- It schedules front-buffer copy when needed and telemetry reduction.
- The same curve also produces `QualityFlowWeight`, reducing migration amplitude from 0.25 to 1.0 as thermal headroom rises.
- LV and diffusion jobs consume biome spec capacity/resistance scalars plus optional per-biome alpha/beta/delta/gamma overrides and keep all state in flat Vault rows.
- DataVault discovery is cold activation/hot-swap only; the FrostTick path uses the cached `_vault`.
- `LateFrameTick` completes only finished work, keeps `_jobScheduled` true until the snapshot write flag is cleared, and patches solver microseconds.
- Telemetry frame index is the deterministic macro simulation tick, not `Time.frameCount`.


- Invalid LV math writes per-sector `ShinobuMacroEcosystemFaultFlags`.
- Telemetry reduction ORs sector flags into the 300-frame black box.
- Recorded scalars: flora/prey/predator biomass, carrying-capacity sum, diffusion transfers, max predator density, substeps, solver microseconds.
- No parallel job writes a shared fault counter.



## Consumers



- `AmbientBiotaDirector` reads `IEcosystemDirectorService` through `GlobalRegistry`.
- `EcosystemDirector.TryGetBiomassAvailability` fronts the macro Vault biomass snapshot.
- Sources: cached `_dataVault`, cached generation handles, `MacroEcosystemVaultContract`.
- It falls back to already-published legacy ecology slots only when macro snapshot is absent.
- The read path does not create sector/biomass slots or refresh Vault descriptors.
- `StressDrivenSpawnDirector` reads the same contract DTO snapshot for spawn-pressure input before falling back to the ecosystem service bridge.
- Other spawn/resource systems should route through the ecosystem service or macro contract records instead of owning population counters.



## Verification



- Prior static scan text reported no `UnityEngine.Physics`, `Physics.`, managed collection creation, coroutine, `Update()`, private persistent `NativeParallelHashMap`, `NativeDisableParallelForRestriction`, runtime `Pack=1`, or `Time.frameCount` in the edited macro files.
- Treat findings and labels as STATIC_SOURCE orientation only until an artifact tuple is attached.
- Labels: `FROSTTICK_REGISTRY_CLEAN`, `PRE_POST_TUNING_FENCE_WITH_CAPACITY_DRIFT_PRESENT`, `DIRECT_READERS_PRE_POST_TUNING_FENCE_PRESENT`, `BURST_EXACT=7`, `JOBS=7`.
- Required artifact tuple: command/tool, timestamp, environment, scanned root, output path, unresolved list.
- Quality curve source samples use C# positive-float truncation.
- q=0.10 -> 1 LV substep / 1 diffusion pass / 0.2500 flow.
- q=0.29 -> 1 / 1 / 0.2763.
- q=0.50 -> 2 / 2 / 0.4873.
- q=0.75 -> 5 / 4 / 0.8260.
- q=1.00 -> 6 / 5 / 1.0000.
- Build proof is pending because no current guarded compile/Unity import artifact is linked.
