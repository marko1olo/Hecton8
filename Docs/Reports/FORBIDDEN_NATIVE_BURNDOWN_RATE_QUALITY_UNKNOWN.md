# Forbidden Native Burndown Rate And Quality - UNKNOWN

Generated local: 2026-05-25 23:24:46 +04:00
Updated local: 2026-05-26 00:55:25 +04:00
Evidence class: STATIC_SOURCE_AND_REPORT_ONLY
Build/profiler proof: not run in this pass.

## Superseding Recheck - 2026-05-26 00:55

Fresh audit artifact:
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json`
- `scannedFiles = 2421`
- `parseFailures = 0`
- `forbiddenPersistentCandidates = 1770`
- `forbiddenMonoBehaviourCandidates = 358`
- `totalNativeFieldDeclarations = 7324`
- `jobTransientFields = 5490`
- `stackOnlyRefStructViewFields = 19`
- `coreMemoryAllowedFields = 45`
- `rawPointerFields = 865`
- `auditHashSha256 = 68217d9f155aeb5233cbb3cc004518df4a1eb2c1d0d222bd810ca241008bbe31`

Excluded artifact:
- `Docs/Reports/VAULT_NATIVE_ALIAS_LEDGER_X_000.json`, modified `2026-05-26 00:47:20`, reports `2138/581`.
- It is not accepted as current proof because it contradicts current source. Example: it reports raw pointer fields in `TerrainChunkPagerRuntime.cs:118-136`, but current source at those lines is scalar length state; it reports old Drone native fields at `DroneFleetManager.cs:802-906`, but current source there stores `VaultGenerationHandle<T>` descriptors.

Updated burndown:
- From previous accepted `2026-05-25 23:16:10` ledger: persistent `1784 -> 1770`, MonoBehaviour `364 -> 358`.
- From latest trusted pre-recheck `2026-05-25 23:46:40` ledger: persistent `1778 -> 1770`, MonoBehaviour `358 -> 358`.
- Current late rate from `23:46:40 -> 00:55:25`: persistent `-8` over `1.146h` = `6.98/hour`; MonoBehaviour `0/hour`.
- Current rate from prior user report baseline `23:16:10 -> 00:55:25`: persistent `-14` over `1.654h` = `8.46/hour`; MonoBehaviour `-6` = `3.63/hour`.

Main source-visible changes since the previous accepted full ledger:
- `HabitatGraphManager.cs`: `21 -> 13`, delta `-8`. The remaining 13 are private view-bundle struct fields (`HabitatGraphWriteViews`, `HabitatFloodGraphJobViews`), not physical instance owners. They still need a formal stack-only/ref-struct or scanner classification decision.
- `WorldChunkResidencyManager.cs`: `0` current forbidden persistent candidates.
- `TerrainChunkPagerRuntime.cs`: `0` current forbidden persistent candidates, but full-lifetime Vault locks / unsafe alias strategy still need owner/fence proof.
- `DroneFleetManager.cs`: `0` current forbidden persistent candidates.
- `FluidPipeGraphRuntime.cs`: `0` current forbidden persistent candidates.
- `VoxelDeltaProcessor.cs`: `0` current forbidden persistent candidates.

## Question Answered

Questions:
- How fast are the 13XX agents removing forbidden persistent native aliases and forbidden MonoBehaviour native aliases?
- What is the quality of the work?
- Should all of these be removed?

Short verdict:
- Direction is correct. This is not empty churn.
- Quality is mixed but improved. Several scoped migrations are architecturally real, Construction/World/Voxel target owners are now at `0` current forbidden field candidates, but most proof is still static-only and the project is not release-clean.
- No, not every `NativeArray`/native container token should be removed. Every unclassified `forbidden_persistent_native_alias_candidate` should be either removed/migrated or reclassified with proof. Transient job fields, stack-only views, and core memory authority fields are allowed and must not be blindly deleted.

## Comparable Full-Ledger Burndown

Only full-project ledgers with comparable scanned-file counts and zero parse failures are used for rate. Scoped one-file reports are excluded.

| Time | Artifact | Scanned | Parse failures | Forbidden persistent | Forbidden MonoBehaviour | Total native fields |
|---|---:|---:|---:|---:|---:|---:|
| 2026-05-25 07:46:40 | `VAULT_NATIVE_ALIAS_LEDGER_X_000_HAZARD_PENDING.json` | 2406 | 0 | 2042 | 510 | 7612 |
| 2026-05-25 11:29:02 | `VAULT_NATIVE_ALIAS_LEDGER_X_000_SPATIAL_PENDING.json` | 2406 | 0 | 2027 | 497 | 7597 |
| 2026-05-25 17:04:25 | `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL.json` | 2413 | 0 | 1947 | 432 | 7516 |
| 2026-05-25 17:20:35 | `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_AFTER_APEX.json` | 2417 | 0 | 1930 | 422 | 7502 |
| 2026-05-25 17:22:06 | `VAULT_NATIVE_ALIAS_LEDGER_1307_PROJECT_PARSE_CHECK.json` | 2417 | 0 | 1930 | 422 | 7502 |
| 2026-05-25 17:39:00 | `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_AFTER_VAULT_AUDIO.json` | 2417 | 0 | 1927 | 418 | 7497 |
| 2026-05-25 18:23:29 | `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_FINAL.json` | 2418 | 0 | 1884 | 417 | 7462 |
| 2026-05-25 18:40:12 | `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_APEX_FINAL.json` | 2419 | 0 | 1884 | 417 | 7462 |
| 2026-05-25 19:11:20 | `VAULT_NATIVE_ALIAS_LEDGER_1304_FULL_APEX_RECHECK.json` | 2418 | 0 | 1866 | 417 | 7462 |
| 2026-05-25 21:09:52 | `VAULT_NATIVE_ALIAS_LEDGER_1304_APEX_LOOP12_FULL.json` | 2418 | 0 | 1860 | 417 | 7457 |
| 2026-05-25 21:32:49 | `VAULT_NATIVE_ALIAS_LEDGER_1304_APEX_LOOP13_FULL.json` | 2418 | 0 | 1853 | 403 | 7485 |
| 2026-05-25 23:05:15 | `VAULT_NATIVE_ALIAS_LEDGER_1303_WHOLE_SCRIPTS.json` | 2420 | 0 | 1791 | 366 | 7349 |
| 2026-05-25 23:16:10 | `VAULT_NATIVE_ALIAS_LEDGER_X_000.json` | 2420 | 0 | 1784 | 364 | 7342 |
| 2026-05-25 23:43:25 | `VAULT_NATIVE_ALIAS_LEDGER_1303_WHOLE_SCRIPTS.json` | 2421 | 0 | 1778 | 358 | 7330 |
| 2026-05-25 23:46:40 | `VAULT_NATIVE_ALIAS_LEDGER_1307_PROJECT_PARSE_CHECK.json` | 2421 | 0 | 1778 | 358 | 7332 |
| 2026-05-26 00:55:25 | `VAULT_NATIVE_ALIAS_LEDGER_UNKNOWN_CURRENT_20260526_0052.json` | 2421 | 0 | 1770 | 358 | 7324 |

Rate windows:

| Window | Persistent delta | Persistent rate | MonoBehaviour delta | MonoBehaviour rate |
|---|---:|---:|---:|---:|
| 07:46:40 -> 23:16:10 | -258 | 16.65/hour | -146 | 9.42/hour |
| 17:04:25 -> 23:16:10 | -163 | 26.31/hour | -68 | 10.97/hour |
| 21:32:49 -> 23:16:10 | -69 | 40.05/hour | -39 | 22.64/hour |
| 23:05:15 -> 23:16:10 | -7 | 38.44/hour | -2 | 10.98/hour |
| 23:16:10 -> 00:55:25 | -14 | 8.46/hour | -6 | 3.63/hour |
| 23:46:40 -> 00:55:25 | -8 | 6.98/hour | 0 | 0.00/hour |

Conclusion on speed:
- Persistent alias removal accelerated late in the day, mostly because construction/world slices landed.
- MonoBehaviour alias removal is slower. It is concentrated in large old runtime MonoBehaviours and cannot be cleared by trivial handle renames.
- Current remaining count is still high: 1770 persistent candidates, 358 MonoBehaviour candidates.

## Quality Evidence

Positive quality proof:
- 1305 world streaming: current fresh ledger reports `WorldChunkResidencyManager.cs = 0` and `TerrainChunkPagerRuntime.cs = 0` forbidden persistent candidates. Terrain pager still has lifetime lock / alias strategy proof debt, but the field-level residual is gone.
- 1306 construction: `VAULT_EXORCISM_PHASE0_1306.md` shows real staged removal, not regex hiding. Fresh ledger confirms `DroneFleetManager.cs = 0`, `FluidPipeGraphRuntime.cs = 0`, `LogisticsPipeTransportScheduler.cs = 0`, `LogisticsRouteScratchMemory.cs = 0`, `DroneFleetManager_Transactions.cs = 0`; `HabitatGraphManager.cs` is down to 13 view-bundle fields.
- 1307 audio propagation: report status shows propagation scope has 0 forbidden persistent fields and 11 transient IJob native fields. That is correct classification, not deletion of job data.
- 1313 data monolith: strict validator now passes active h8bin static checks and reports 0 text StreamingAssets artifacts, but release verdict remains rejected pending platform PAL/player proof.
- 1314 audio bridge: report status has failedChecks=0, runtime token scan hits=0, and native dump route avoids managed runtime IO; however compile/runtime proof is explicitly absent.

Negative quality proof:
- Most 13XX reports are static-only. Compile, Unity player, profiler, live fuzzer, and runtime GC proof are repeatedly absent.
- `TerrainChunkPagerRuntime.cs` has 0 forbidden field candidates, but it still locks many Vault buffers for runtime lifetime through `LockVaultBuffers()` around lines 749-775 and caches unsafe aliases through `CacheUnsafePointers()` around lines 652-680. That is better than raw persistent fields, but not automatically a clean final architecture.
- `HabitatGraphManager.cs:390-406` still has 13 NativeArray fields inside private view-bundle structs. This is not a physical owner regression, but it is still unclassified in the scanner and should become stack-only/ref-struct or be formally classified.
- Top project residuals remain outside the current 13XX cleaned scopes: `HectonVoxelEngine.cs` 124, `VegetationMemoryPool.cs` 75, `PlayerInventory.cs` 63, `DestructibleOrganicManager.cs` 50, `LogisticsNetworkGraph.cs` 50.

Quality verdict:
- Architecture direction: correct.
- Static implementation quality: medium-to-good in scoped areas; real handle/window migrations are visible.
- Release quality: not reached. Static pass is not runtime proof.
- Biggest risk: agents may declare "zero" in scoped domains while full-project ledger still carries 1770 forbidden persistent candidates and 358 MonoBehaviour candidates.

## Latest Residual Hotspots

Top forbidden persistent candidate owners in latest full ledger:

| Count | Path |
|---:|---|
| 124 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` |
| 75 | `Assets/_Project/Scripts/World/VegetationMemoryPool.cs` |
| 63 | `Assets/_Project/Scripts/PlayerInventory.cs` |
| 50 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` |
| 50 | `Assets/_Project/Scripts/Power/LogisticsNetworkGraph.cs` |
| 45 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` |
| 42 | `Assets/_Project/Scripts/PDA/CartographyGridJobs.cs` |
| 39 | `Assets/_Project/Scripts/HectonFluidEngine.cs` |
| 37 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` |
| 31 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` |

Top forbidden MonoBehaviour candidate owners:

| Count | Path |
|---:|---|
| 50 | `Assets/_Project/Scripts/World/DestructibleOrganicManager.cs` |
| 49 | `Assets/_Project/Scripts/PlayerInventory.cs` |
| 39 | `Assets/_Project/Scripts/HectonFluidEngine.cs` |
| 33 | `Assets/_Project/Scripts/SubmarineAtmosphereSystem.cs` |
| 31 | `Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs` |
| 19 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` |
| 15 | `Assets/_Project/Scripts/SubmarineStructuralGrid.cs` |
| 14 | `Assets/_Project/Scripts/World/FloraInteractionManager.cs` |
| 13 | `Assets/_Project/Scripts/World/ProceduralWreckGenerator.cs` |
| 13 | `Assets/_Project/Scripts/Fabricator.cs` |

## Must Remove, May Stay, Needs Decision

Must remove or migrate:
- Direct `NativeArray`, `NativeList`, `NativeQueue`, `NativeParallelHashMap`, or raw pointer fields on runtime MonoBehaviours.
- Long-lived cached DataVault physical views across frames.
- Direct native queues used as hot event lanes where `SignalBus<T>` or bounded Vault ring is the first-party route.
- Managed `FileStream`/`BinaryWriter`/exception strings in player crash/dump hot or failure paths.
- Hidden `.Complete()` and same-frame schedule/readback loops without profiler proof.

May stay if classified and proven:
- `NativeArray<T>` fields inside `IJob`/Burst job structs as transient job parameters.
- Method-local `NativeArray<T>` views resolved through `TryReadOnlyHandle` or `TryAcquireWriteLock` and released in `finally`.
- `ref struct` stack-only view types that cannot escape.
- Core memory authority fields in `H8Memory`/DataVault-owned code.
- Fixed-size black-box rings when owned by the correct system and dumped through the approved route.
- Editor-only validators/fuzzers when excluded from player runtime and documented.

Needs architectural decision, not blind deletion:
- Local subsystem native containers that are the actual truth owner. They can remain only if there is a single owner route card, clear lifetime, no DataVault relocation conflict, no MonoBehaviour ownership, and proof that consumers read immutable snapshots. Otherwise migrate to DataVault handles or `SignalBus<T>`.
- `TerrainChunkPagerRuntime` style full-lifetime Vault locks. It has no forbidden field candidates now, but the lock/pointer strategy still needs explicit owner/fence proof before calling it clean.

Final answer:
- They are still reducing the numbers, but the current late rate slowed: `6.98` persistent candidates/hour and `0` MonoBehaviour candidates/hour from `23:46:40 -> 00:55:25`.
- Quality improved in Construction/World/Voxel target owners, but is still not release-grade because proof is mostly static and large old MonoBehaviour owners remain elsewhere.
- Do not delete all native containers. Delete or migrate all unproven persistent aliases. Preserve transient job/native views and core memory authority with proof.
