# H8BIN Validator SHINOBU_258



Date: 2026-05-28

Owner domain: data/static Data Monolith validation tooling

Status: ACTIVE TOOLING; CURRENT PAYLOAD PRESENT; SCOPED PAYLOAD VALIDATOR PASS; FULL DEFAULT RERUN PENDING



`Tools/h8bin_validator.py` is the external Data Monolith firewall. It runs under Python only and does not load Unity, C# assemblies, Burst, or `dotnet`.



## CLI



```powershell



python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets



```



Useful flags:



- `--cs-source-dir <path>` can be repeated to point at C# layout sources.



- `--fail-fast` exits after the first fatal finding.



- `--thorough` validates every record instead of deterministic 5% sampling.



- `--report-json <path>` writes the machine JSON report.

- `--report-junit <path>` writes CI-readable JUnit XML.

- `--csv-diff <source.csv> <generated.h8bin>` compares source hashes against binary `Items.HashId`.

- `--runtime-source-dir <path>` can be repeated to scan runtime C# for forbidden text `StreamingAssets` loaders.

- `--allow-runtime-text-loaders` disables that source gate for emergency archaeology only.



- The JSON report also emits `migration_summary`, a deterministic owner grouping for current text-artifact and runtime-loader blockers.
- `--csv-diff` now fails closed when the source CSV contains no hash-bearing rows, and hash column names are case-insensitive.
- `--csv-diff --fail-fast` preserves binary probe errors in the main JSON/JUnit report before stopping.
- `--csv-diff` validates the generated external `.h8bin` target through the same binary firewall before extracting `Items.HashId`; corrupt probes stop before hash comparison noise.
- `--csv-diff` also compares known numeric `H8ItemRecord` fields such as `Cost`, `MassKg`, `VolumeM3`, `MaxStack`, and `YieldHash` when those columns are present in the source CSV.
- Checksum mmap views are scoped inside `compute_payload_checksum(...)` and released in `finally` before fail-fast errors can unwind, preserving Task 20 file-handle closure.
- Known non-H8DM `.h8bin` magics are not parsed as Data Monolith directories.
- `H8VB`/Audio/VocalBank now has a source-backed sidecar validator inside SHINOBU_258; unknown non-H8DM payloads still fail through the normal magic/header gate instead of an H8DM directory cascade.
- The JUnit writer emits concrete failure nodes for directory/payload failures even when no `.h8bin` testcase exists.
- `--fail-fast` now preserves schema/layout findings in JSON/JUnit instead of aborting before report emission.


## Source Authority



The validator parses C# source as the authority:



- `H8DataLayoutConstants` for magic, version, header/directory sizes, and alignment.



- `H8DataSectionId` and compiler `SectionOrder`.



- `[StructLayout(LayoutKind.Explicit, Size=...)]`.

- `[FieldOffset(...)]`.

- `H8DataLayoutAudit.GetExpectedRecordSize(...)`.



- Struct/field parser is a lightweight syntax-tree scanner.
- It strips comments, walks balanced C# attribute blocks, matches declaration bodies by braces, and extracts layout/offset attributes.
- It accepts namespaced and `global::` attribute forms.
- It accepts combined lists: `[Serializable, StructLayout(...)]`, `[NonSerialized, FieldOffset(...)]`.
- It accepts extra attributes between layout markers and declarations.
- It accepts readonly/partial/unsafe modifiers and casted integer expressions such as `(int)16`.



Current source-truth magic is `0x4D443848` (`H8DM` as little-endian bytes `48 38 44 4D`). The prompt example `H8BN` is not the active project contract.



## Data Monolith Bake Route



The active compiler source roots are `Assets/_SourceData/DataMonolith` and `Data/Balance`. Domain-owned source folders such as



`Assets/_SourceData/Signals`, `Assets/_SourceData/Atmosphere`, `Assets/_SourceData/Equipment`, and



`Assets/_SourceData/HadalGraphs` are editor/source inputs for their owning routes, not Data Monolith rows. Unknown monolith CSV



table names now fail the editor bake instead of being silently ignored.



`H8DataMonolithCompiler.ValidateProductionSectionCoverage` also rejects sparse production bakes before output. A blob with



valid header/directory/checksum but empty `Biomes`, `Recipes`, `LootCdf`, `VoxelMaterials`, `AudioClipRegistry`,



`VfxScalars`, `ToolHeatCapacity`, `SubmarineHullConstants`, `PhysicsMaterials`, `GhostModules`, `SpawnCreditCosts`,



`SopErrors`, `HudLayouts`, or `SectorPageDirectory` is not payload-ready.



The coverage gate is no longer row-count only. `ValidateCrossReferences` now rejects required record IDs that resolve to



hash `0` and verifies the current semantic links: `Biomes.SurfaceId -> VoxelMaterials.VoxelHash`,



`VoxelMaterials.YieldHash -> Items.HashId`, `VoxelMaterials.SurfaceId -> PhysicsMaterials.SurfaceHash`,



`GhostModules.RecipeHash -> Recipes.OutputHash`, `SpawnCreditCosts.EntityHash -> Creatures.SpeciesHash`, and



`SectorPageDirectory.BiomeHash -> Biomes.BiomeHash`. `BiomeHeatmap` cells must also resolve to a baked biome hash after



normalization. Duplicate production identity hashes, non-finite floats, invalid depth ranges, zero audio bank hashes,



non-positive critical quantities, and sector AUP coordinates outside `[-100000, 100000]` fail the editor bake before



any blob is written.



CSV rows are now fail-closed on header/value count mismatch. The compiler no longer silently truncates over-wide rows;



`HudLayout.csv` must match the 64-byte `H8HudLayoutRecord` schema and cannot smuggle unowned `m32/m33` matrix cells.



`H8DataMonolithCompilerWindow` exposes this through an editor-only `Coverage` button and `Production Coverage` panel. The



same window now writes `Data/Balance/Schemas` templates for the required sections; that schema folder remains excluded from



compiler source discovery and is not runtime payload data.



Historical static source coverage is archived at `Docs/DEPRECATED/X_012_Stale_DataMonolith_Reports_2026-05-23/SHINOBU_258_DataMonolith_SourceCoverage.md`. It records the

authored row counts, generated sections, semantic gate status, and remaining payload blockers without generating a fake



`static_data.h8bin`.



Batch entrypoint for the real payload bake:



```powershell



Unity.exe -batchmode -quit -projectPath C:\hades\Hecton8 -executeMethod Hecton8.EditorValidation.H8DataMonolithCompiler.BakeFromCommandLine



```



This command was not run during the SHINOBU_258 source-only pass.



## Checks



- Rejects `.json`, `.xml`, and `.csv` in the runtime target as unbaked artifacts.



- Rejects runtime C# source outside `Editor` folders that cold-loads `.csv`, `.json`, or `.xml` from `StreamingAssets`.



- Validates header and directory magic/version/byte counts.



- Verifies Unity-compatible XXH3-64 checksum over `bytes[16..end)`.



- Traverses the section table and rejects out-of-file ranges, overlaps, bad order, bad record sizes, zero record strides, under-struct record strides, and bad empty offsets.

- Skips payload sampling for sections whose byte ranges are out of file or overlap another owned range, so corruption reports remain validator findings rather than Python tracebacks.

- Skips hash extraction and payload sampling for non-empty sections whose declared record stride cannot contain the parsed C# explicit-layout struct.

- Emits hex dumps around corrupt section-table entries for section order, record-size, empty-offset, alignment, out-of-file, fixed-range, and overlap findings.

- Enforces section alignment from `SectionAlignmentBytes` and field alignment from parsed C# primitive types.

- Enforces a hard 16-byte minimum on effective section/file/data-start alignment even if C# constants regress lower.

- Runs RLE probe only when C# defines `RleDirectoryFlag`; otherwise non-zero directory flag bits fail as unsupported instead of being guessed.

- Samples payload records for non-finite floats, AUP NaN/Infinity/out-of-bounds, integer AUP out-of-bounds, zero critical hashes, broken recipe/loot foreign keys, and empty reference master sets.

- Uses lazy sampling iterators: `--thorough` walks `range(count)` and default sampling streams deterministic pseudo-random indices without allocating a full index list.

- Routes `H8VB` by magic before H8DM parsing and validates 64-byte header, 32-byte sorted index, FNV bank hash, payload ranges, codecs, and H8ADPCM block headers.

- Uses `mmap.ACCESS_READ`; no full-file `bytearray` staging.



## Current Gate Result



Historical 2026-05-21 command:



```powershell



python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log

```



Historical result: `FAIL`.



Historical fatal finding:



- `STATIC_DATA_MISSING` for `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` in the old capture.
- Current 2026-06-01 filesystem state is different: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists and was `1,804,864` bytes in that check. Superseded by the 2026-08-05 measurement: `7,457,664` bytes, mtime 2026-06-07. Route-specific Unity boot proof remains pending.

Current 2026-05-28 scoped payload/schema recheck:

Command: scoped `h8bin_validator.py` over `Assets\StreamingAssets` plus `Assets\_Project\Scripts\Data\Monolith`.

Report: `Docs\Reports\DOC_ROOT_ARCH_AUDIT_h8bin_validator_narrow_20260528.json`.

Result: `PASS`, `files=2`, `structs=32`, `mb=1.0495`, `seconds=0.491846`.

Scope: Python schema/payload validation for current `.h8bin` files and Data Monolith source roots.

Non-claim: no Unity import, Play Mode, player boot, profiler, GC, platform, or audio proof.



Non-fatal sidecar proof:



- `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` validates as Audio/VocalBank sidecar.
- Facts: magic `H8VB`; size `19,680`; one sorted record; FNV bank hash match.
- H8ADPCM payload: `ceil(34800/64) * 36 = 19584`.
- First-block step byte is in range; no H8DM directory cascade.



2026-05-21 remediation pass moved/guarded the then-known literal runtime text routes:



- Atmosphere



- Core/Origin



- Equipment/Auxiliary



- Fauna



- Power/Logistics



- Thermodynamics

- Core/Signals

- UI/TerminalOS

- World/OfflineHadalArchBaker

- Physics/KCC



PROJECT_AUDIT symbolic-loader addendum:
- `Tools/h8bin_validator.py` resolves const/static readonly text artifact symbols used on `StreamingAssets` loader lines.
- Older "0 runtime loader sites" wording is invalid for variable-based CSV fallbacks.
- Current no-static-data sidecar validation writes `Docs/Reports/PROJECT_AUDIT_h8bin_validator_symbol_post.json`.
- It fails on five runtime CSV loader routes:



- `Assets/_Project/Scripts/AI/Cognition/ShinobuApexBrainVault.cs:907` via `CsvFileName=apex_predator_stats.csv`.

- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3129` via `ApexCortexBehaviorCsvName=ai_behavior_overrides.csv`.

- `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs:3341` via `MesofaunaSpeciesProfilesCsvName=mesofauna_species_profiles.csv`.

- `Assets/_Project/Scripts/Fauna/StressDrivenSpawnDirector.cs:2234` via `RulesCsvName=director_spawn_rules.csv`.

- `Assets/_Project/Scripts/World/VolcanicUpdraftDirector.cs:1896` via `CsvFileName=volcanic_vents.csv`.



- PROJECT_AUDIT route cleanup addendum: those five runtime CSV `StreamingAssets` fallbacks are now removed from player runtime source.
- The affected CSV bridges resolve only editor/development source-data paths (`Assets/_SourceData/...`, `Data/...`, or project-root legacy dev files) and fail closed to deterministic defaults/binary payload absence in production.
- Historical sidecar validation wrote `Docs/Reports/PROJECT_AUDIT_h8bin_validator_after_csv_routes.json` and passed with only `H8VB_SCHEMA_VALIDATED`.
- Historical mandatory validation wrote `Docs/Reports/PROJECT_AUDIT_h8bin_validator_after_csv_routes_required.json` and failed on `STATIC_DATA_MISSING` before the current payload appeared.
- Current scoped recheck validates the present static payload.
- Remaining production status is gated by full default validator rerun, Unity import, player boot, checksum, profiler, GC, and owner-route proof.



This is a payload-readiness/schema-ownership failure, not a Python validator failure.



## Remediation Map



The current JSON report includes remediation strings and active references when text artifacts are present. Current text-artifact remediation is done.



Remediated source routes now use editor/source-data paths and no longer count as runtime `StreamingAssets` text loaders:



- `Assets/_SourceData/Signals/signal_tuning_profiles.csv`



- `Assets/_SourceData/Signals/signal_corridor_capacities.csv`



- `Assets/_SourceData/Atmosphere/storm_depth_impact_profiles.csv`



- `Assets/_SourceData/Core/Origin/aup_constants.csv` when present.



- `Assets/_SourceData/Equipment/Auxiliary/auxiliary_equipment_profiles.csv`



- `Assets/_SourceData/Fauna/leviathan_rig_constraints.csv` when present.



- `Assets/_SourceData/Power/logistics_components.csv` when present.



- `Assets/_SourceData/Thermodynamics/heat_source_profiles.csv` and `hazard_profiles.csv` when present.



- `Assets/_SourceData/Atmosphere/weather_profiles.csv` and `beaufort_scale_profiles.csv` when present.



- `Assets/_SourceData/UI/TerminalOS/terminal_layouts.csv` when present.

- `Assets/_SourceData/HadalGraphs/hadal_structure_graphs.csv`

- `Assets/_SourceData/VFX/Propwash/vehicle_wake_profiles.csv`

- `Assets/_SourceData/Physics/KCC/locomotion_environment_profiles.csv`



- The propwash route is additionally compile-time guarded: `HectonMarineSnowRenderer` keeps CSV staging buffers,
- background file IO,
- and wake-profile parsing behind `UNITY_EDITOR`;
- player builds compile no-op lifecycle methods and keep deterministic default wake rows until binary hydration exists.



Do not solve this by allowlisting CSV files in `StreamingAssets`. That would keep parallel runtime truth outside Data Monolith.



Runtime source loader findings are fatal even if the text file is absent. Otherwise a check-in can reintroduce a parallel source of truth without touching the validator.



Detailed migration list: `Docs/ARCHITECTURE/STREAMINGASSETS_TEXT_RUNTIME_MIGRATION_SHINOBU_258.md`.



## Verification



- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`

- `python Tools\Security\ReplayHasher.py self-test` -> `SELFTEST_OK`

- `python Tools\test_h8bin_validator.py` -> 51 tests OK

- PROJECT_AUDIT symbolic-loader gate: `python -m py_compile Tools\h8bin_validator.py` passes; `python Tools\test_h8bin_validator.py` ran 53 tests OK after adding a symbol-loader regression case.

- PROJECT_AUDIT route cleanup gate:
  - Command: `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --no-require-static-data --report-json Docs\Reports\PROJECT_AUDIT_h8bin_validator_after_csv_routes.json --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`.
  - Result: `PASS files=1 structs=32` with validated `H8VB` sidecar.
  - Stale note: old required-mode missing-`static_data.h8bin` finding is superseded by current filesystem payload presence.
