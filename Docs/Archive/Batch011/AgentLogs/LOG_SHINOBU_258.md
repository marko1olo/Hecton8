# LOG_SHINOBU_258

## 2026-05-20 External H8BIN Validator Pass

What was wrong:
- No standalone external `.h8bin` validator existed at `Tools/h8bin_validator.py`.
- Data Monolith readiness could be confused with source readiness because `static_data.h8bin` is absent while source/baker code exists.
- Runtime `Assets/StreamingAssets` currently contains human-readable CSV artifacts, which violates the Data Monolith deployment doctrine under SHINOBU_258.

What was done:
- Added `Tools/h8bin_validator.py`.
- Added `Tools/test_h8bin_validator.py`.
- Added `Docs/ARCHITECTURE/H8BIN_VALIDATOR_SHINOBU_258.md`.
- Wrote current validation reports to `Docs/Reports/SHINOBU_258_h8bin_validation_current.json` and `.junit.xml`.
- Appended CI metrics to `Docs/Reports/CI_BINARY_VALIDATION.log`.

Cinematic Cheats used:
- The validator always fully proves header, directory, section table, checksum, and alignment.
- Payload record validation uses deterministic 5% sampling by default as the "Dear Lie" for huge blobs.
- `--thorough` is available for nightly/full validation.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed. This is CI/editor tooling.
- Avoided Unity/dotnet launch cost: no `dotnet build`, no Unity compile, no C# assembly load.
- Current validator run over absent payload processed 0 MB in 0.002676 seconds after schema parse.

Current gate result:
- `FAIL`.
- `UNBAKED_ARTIFACT`: `Assets/StreamingAssets/HadalGraphs/hadal_structure_graphs.csv`.
- `UNBAKED_ARTIFACT`: `Assets/StreamingAssets/signal_corridor_capacities.csv`.
- `UNBAKED_ARTIFACT`: `Assets/StreamingAssets/signal_tuning_profiles.csv`.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.

Remediation pass:
- Added owner-reference and remediation fields to `UNBAKED_ARTIFACT` JSON findings.
- `hadal_structure_graphs.csv` owner evidence: `HadalStructureForgeWindow.cs` hardcodes the StreamingAssets CSV path. Required route: move source CSV to `_SourceData/HadalGraphs`; runtime uses baked mesh/binary output.
- `signal_corridor_capacities.csv` owner evidence: `SignalWardenRuntime.cs` cold-loads it from StreamingAssets. Required route: move human source to source-data/schema and bake into Data Monolith/domain h8bin; runtime reads binary/Vault.
- `signal_tuning_profiles.csv` owner evidence: `SignalWardenRuntime.cs` and `SignalTrafficMonitorWindow.cs` read the StreamingAssets CSV. Required route: editor hot reload may read source CSV; runtime cold boot reads baked binary/Vault.
- Optimized owner scan from archival-wide traversal to active scripts plus architecture docs. Current gate with remediation fields: 10.158064 seconds.

Verification:
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\Security\ReplayHasher.py self-test`: `SELFTEST_OK`.
- `python Tools\test_h8bin_validator.py`: 7 tests OK.
- Re-run after remediation fields: `python Tools\test_h8bin_validator.py`: 7 tests OK.

<SELF_AUDIT>
  <agent_id>SHINOBU_258</agent_id>
  <task_count>20</task_count>
  <runtime_dependency>none; pure Python only</runtime_dependency>
  <compile_guard>No dotnet build, no Unity compile, no runtime C# edits.</compile_guard>
  <struct_layout_verification>
    <H8DataBlobHeader size="16" largest_alignment="8" final_padding="0">
      <field offset="0" type="uint" name="Magic" size="4" />
      <field offset="4" type="ushort" name="FormatVersion" size="2" />
      <field offset="6" type="ushort" name="HeaderBytes" size="2" />
      <field offset="8" type="ulong" name="Checksum64" size="8" />
    </H8DataBlobHeader>
    <H8DataBlobDirectory size="64" largest_alignment="4" final_padding="0" />
    <H8DataSectionEntry size="16" largest_alignment="4" final_padding="0" />
  </struct_layout_verification>
  <regexes>
    <struct>\[StructLayout\s*\([^\]]*LayoutKind\.Explicit[^\]]*\)\]\s*(public|internal|private)? ... struct Name</struct>
    <field>\[FieldOffset\s*\(X\)\]\s*(public|internal|private)? type field;</field>
    <section_order>SectionOrder = { H8DataSectionId.* }</section_order>
    <record_size>case H8DataSectionId.*: return ...;</record_size>
  </regexes>
  <mmap>Binary files opened with mmap.ACCESS_READ. No full-file bytearray staging.</mmap>
  <checksum>Unity-compatible pure-Python XXH3-64 oracle from Tools/Security/ReplayHasher.py over bytes[16..end).</checksum>
  <dear_lie>Deterministic 5 percent payload record sampling by default; full validation behind --thorough.</dear_lie>
  <vault_status>No GlobalDataVault or NativeArray ownership. External OS mmap only.</vault_status>
  <job_graph>No Burst jobs, no JobHandle, no Complete.</job_graph>
  <global_quality_weight>Not consumed by validator; never changes gameplay truth ownership, DTO layout, save identity, or authority route.</global_quality_weight>
</SELF_AUDIT>

## 2026-05-20 Ledger Integration Pass

What was wrong:
- The validator proof was present in SHINOBU reports, but the central binary payload ledger did not yet name the active CI firewall or the exact StreamingAssets red gate.

What was done:
- Updated `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md` with the SHINOBU_258 validator route.
- Ledger now names `Tools/h8bin_validator.py`, the current JSON proof file, the three runtime CSV artifacts, the missing `static_data.h8bin`, and the requirement to move CSVs to source/editor-only routes before binary bake.

Cinematic Cheats used:
- None in runtime. This is documentation integration for an external validation gate.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- Prevented false-green review path for StreamingAssets readiness; runtime savings only materialize after owners remove text payload boot paths and bake binary files.

## 2026-05-20 Runtime Text Loader Gate Pass

What was wrong:
- Filesystem-only sanitation could catch current `.csv/.json/.xml` files under `StreamingAssets`, but it could not catch runtime C# cold-loader code paths waiting to reintroduce text truth later.
- First source scan pushed the current gate to 15.631635 seconds, producing a performance warning.

What was done:
- Added `RUNTIME_TEXT_STREAMINGASSETS_LOAD` fatal findings to `Tools/h8bin_validator.py`.
- Runtime source scan skips `Editor` folders, flags `.csv/.json/.xml` `StreamingAssets` loaders, and exposes `--runtime-source-dir` plus `--allow-runtime-text-loaders`.
- Optimized artifact reference lookup with a process cache and runtime source scan with `os.walk` directory pruning plus bytes-level early rejection.
- Added two tests: runtime loader fails; editor-only loader is ignored.
- Updated `Docs/ARCHITECTURE/H8BIN_VALIDATOR_SHINOBU_258.md` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic Cheats used:
- Bytes-first source scan avoids decoding/splitting C# files unless they contain both `streamingassets` and a forbidden text suffix.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- CI validator wall time on current checkout dropped from 15.631635s to 5.395143s.

Current gate result:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 4 files, including new `Assets/StreamingAssets/auxiliary_equipment_profiles.csv`.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 13 runtime C# loader sites across Atmosphere, AUP Origin, Signals, Fauna, Power, Thermodynamics, and Terminal UI.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.

Verification:
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\Security\ReplayHasher.py self-test`: `SELFTEST_OK`.
- `python Tools\test_h8bin_validator.py`: 9 tests OK.
- Current gate command: FAIL by design, 0 `.h8bin`, 32 structs parsed, 5.395143 seconds.

## 2026-05-20 Migration Route Card Pass

What was wrong:
- The JSON report is precise but not ergonomic enough for cross-domain route owners to plan migrations without rerunning the tool.

What was done:
- Added `Docs/ARCHITECTURE/STREAMINGASSETS_TEXT_RUNTIME_MIGRATION_SHINOBU_258.md`.
- Linked it from `Docs/ARCHITECTURE/H8BIN_VALIDATOR_SHINOBU_258.md`.
- The card lists current filesystem text artifacts, current runtime text loader categories, and the required binary/Vault migration contract.

Cinematic Cheats used:
- None. This is architecture routing documentation.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- Prevents redundant re-discovery work by downstream owners; measurable runtime savings only after loaders are removed.

## 2026-05-20 Prompt Re-Extraction Pass

What was wrong:
- Exact-tag regex lookup for `SHINOBU_258` can fail when the current batch tag includes attributes.

What was done:
- Re-extracted the active prompt with an attribute-aware regex and confirmed the current source tag:
  `<AGENT_PROMPT id="SHINOBU_258" role="BINARY_SCHEMA_VALIDATOR_BOT" chat_name="SHINOBU_258">`.

Cinematic Cheats used:
- None.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- Prevents wrong-domain work caused by stale prompt extraction.

## 2026-05-20 Migration Summary Report Pass

What was wrong:
- The current StreamingAssets gate changed under concurrent work. A new unbaked Atmosphere CSV appeared at `Assets/StreamingAssets/Hecton8/storm_depth_impact_profiles.csv`.
- The JSON report listed raw findings but did not group them by route owner, so downstream migration work required manual sorting every run.

What was done:
- Added `migration_summary` to `Tools/h8bin_validator.py` JSON output.
- Grouped `UNBAKED_ARTIFACT` and `RUNTIME_TEXT_STREAMINGASSETS_LOAD` blockers by owner, required source-data root, and required binary route.
- Added `test_migration_summary_groups_route_owners` to `Tools/test_h8bin_validator.py`.
- Re-ran the current gate and refreshed `Docs/Reports/SHINOBU_258_h8bin_validation_current.json` and `.junit.xml`.

Cinematic Cheats used:
- None in runtime. CI report grouping avoids manual discovery work; it does not simulate or alter gameplay.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- CI migration-discovery saving is qualitative only. Current clean solo validator run: 12.66832 seconds, under the 15 second warning budget.

Current gate result:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 5 files.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 14 runtime C# loader sites.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.
- `migration_summary.owner_count`: 9.

Verification:
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK.
- `python Tools\Security\ReplayHasher.py self-test`: `SELFTEST_OK`.
- Current gate command: FAIL by design, 0 `.h8bin`, 32 structs parsed, 12.66832 seconds.

## 2026-05-21 Runtime Route Cleanup Pass

What was wrong:
- The validator was green as tooling but the project still had deployed text-data routes. That is a real architecture defect, not a Python-tool concern.
- `SignalWardenRuntime`, storm propagation, ocean surface atmosphere, and TerminalOS had runtime or player-adjacent routes pointing at text `StreamingAssets` CSVs.
- SHINOBU_258 docs still reported the stale 5-artifact/14-loader gate after source cleanup.

What was done:
- Moved `signal_tuning_profiles.csv` and `signal_corridor_capacities.csv` from `Assets/StreamingAssets` to `Assets/_SourceData/Signals`; moved their `.meta` files with them.
- Changed Signals CSV hot-swap defaults to editor/source-data paths; player builds return false instead of parsing text.
- Moved `storm_depth_impact_profiles.csv` to `Assets/_SourceData/Atmosphere`; player builds keep deterministic fallback rows and do not touch text files.
- Changed ocean weather and Beaufort profile probes to `Assets/_SourceData/Atmosphere` and disabled player text probing.
- Changed TerminalOS layout path to `Assets/_SourceData/UI/TerminalOS/terminal_layouts.csv`.
- Changed Core/Origin, Fauna, Power, Thermodynamics, and Auxiliary CSV probes to editor/source-data paths or player fallbacks.
- Moved `auxiliary_equipment_profiles.csv` to `Assets/_SourceData/Equipment/Auxiliary`; moved its `.meta`.
- Moved `hadal_structure_graphs.csv` to `Assets/_SourceData/HadalGraphs`; moved its `.meta`; updated Hadal Forge editor input path.
- Updated the H8BIN validator doc, StreamingAssets migration card, binary payload ledger, auxiliary docs, signal route card, storm propagation doc, and zero-GC UI doc to the current gate.

Cinematic Cheats used:
- No runtime simulation added. The route uses deterministic fallback rows as a temporary "do nothing expensive" path until binary hydration exists.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Cold-route saving: text file IO/parsing probes are removed from cleaned player routes; exact microseconds require Unity boot profiling after compile/import.

Current gate result after cleanup:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 0.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 0.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.
- `migration_summary.owner_count`: 0.

Verification:
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK.
- `python Tools\Security\ReplayHasher.py self-test`: `SELFTEST_OK`.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for missing binary payload.
- Scoped runtime text scan now reports only `Editor` folder references; validator skips editor folders by design.
- Touched C# brace/preprocessor count check passed for 8 files.
- No `dotnet build` or Unity import/build was launched.

## 2026-05-21 Data Monolith Bake Route Hardening

What was wrong:
- The previous cleanup moved domain CSVs into `Assets/_SourceData`, but the Data Monolith compiler still treated broad `_SourceData` as a source root.
- Unknown CSV table names were parsed and then ignored by `ParseRow`, so a bake could produce a structurally valid sparse blob while unrelated rows never entered any section.
- There was no explicit command-line method that CI could call to bake and validate `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

What was done:
- Changed `H8DataMonolithCompiler.SourceFolder` to `Assets/_SourceData/DataMonolith`.
- Changed the Data Monolith file watcher to watch the narrowed source root.
- Added `H8DataMonolithCompiler.BakeFromCommandLine()` for Unity batchmode bake/validate/exit-code execution.
- Added `IsRecognizedCsvTable` and a fail-closed `ParseCsv` guard for unknown monolith table names.
- Kept `Data/Balance/armor_penetration_matrix.csv` and `Data/Balance/btree_tuning_profiles.csv` as explicit non-monolith cold-tuning exceptions because existing owners consume them directly.
- Added `ValidateProductionSectionCoverage` so a sparse structurally valid blob is rejected before output when critical sections have zero rows.
- Added `TryAnalyzeProductionCoverage` and a `Production Coverage` panel/action to the Data Monolith compiler window.
- Expanded `Schemas` generation so required production-section templates are visible under excluded `Data/Balance/Schemas`.
- Updated `DATA_MONOLITH_H8BIN_SPEC.md`, `H8BIN_VALIDATOR_SHINOBU_258.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, `HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`, and `SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`.

Cinematic Cheats used:
- No runtime simulation added. The editor gate uses fail-fast source classification instead of later runtime discovery.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- Editor/CI saving is correctness, not measured runtime: unrelated domain source folders are no longer scanned by the monolith watcher/discovery path.

Current gate result after hardening:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 0.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 0.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.
- `migration_summary.owner_count`: 0.

Real payload blockers:
- Current recognized monolith payload sources are `Data/Balance/Items.csv`, `Fauna.csv`, `Economy.csv`, and `Physics.csv`.
- Missing authored production sections include `Biomes`, `Recipes`, `LootCdf`, `VoxelMaterials`, `AudioClipRegistry`, `VfxScalars`, `ToolHeatCapacity`, `SubmarineHullConstants`, `PhysicsMaterials`, `GhostModules`, `SpawnCreditCosts`, `SopErrors`, `HudLayouts`, and `SectorPageDirectory`.
- A sparse structurally valid `static_data.h8bin` is now rejected by the editor compiler before output.

Verification:
- C# brace/preprocessor count for `H8DataMonolithCompiler.cs`: 313 `{`, 313 `}`, 1 `#if`, 1 `#endif`.
- C# brace/preprocessor count for `H8DataMonolithCompilerWindow.cs`: 38 `{`, 38 `}`, 1 `#if`, 1 `#endif`.
- `git diff --check` on touched files: passed with CRLF normalization warnings only.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for missing binary payload, 2.285255 seconds.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

## 2026-05-21 Propwash Wake CSV Runtime Route Cleanup

What was wrong:
- A concurrent VFX task reintroduced `Assets/StreamingAssets/vehicle_wake_profiles.csv`.
- `HectonMarineSnowRenderer` had a cold `Application.streamingAssetsPath` CSV reader route for wake profiles.
- This violated the SHINOBU_258 gate even though the row parser itself used `ReadOnlySpan<byte>` and wrote unmanaged `PropwashWakeProfileDTO` rows.

What was done:
- Moved `vehicle_wake_profiles.csv` and `.meta` to `Assets/_SourceData/VFX/Propwash/`.
- Removed the propwash wake CSV from deployed `StreamingAssets`.
- Changed `HectonMarineSnowRenderer` so CSV source paths resolve only under `UNITY_EDITOR`.
- Wrapped CSV staging buffers, background thread, file IO, and parser refresh behind `UNITY_EDITOR`.
- Left non-editor/player lifecycle methods as no-ops; player builds use deterministic default wake rows until binary hydration exists.
- Updated `PROPWASH_GPU_DIRECTOR_SHINOBU_237.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, `H8BIN_VALIDATOR_SHINOBU_258.md`, and `STREAMINGASSETS_TEXT_RUNTIME_MIGRATION_SHINOBU_258.md`.

Cinematic Cheats used:
- No gameplay simulation added. Player builds keep the deterministic default visual wake profile instead of doing cold text IO.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Cold player allocation surface reduced by six 4 KB CSV staging byte buffers plus two lock objects because that reader surface is editor-only compiled.

Current gate result after cleanup:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 0.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 0.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.
- `migration_summary.owner_count`: 0.

Verification:
- `HectonMarineSnowRenderer.cs` brace/preprocessor count: 347 `{`, 347 `}`, 11 `#if`, 11 `#endif`.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK in 10.799s.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for missing binary payload, 1.308042 seconds.
- `git diff --check` on touched files: passed with CRLF normalization warnings only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

## 2026-05-21 Data Monolith Source Coverage Materialization

What was wrong:
- `static_data.h8bin` is still absent.
- Active monolith sources currently cover only `Items`, `Creatures/Fauna`, `Economy`, and `PhysicsConstants`.
- Required production sections are broader; generating placeholder rows would fake payload readiness.

What was done:
- Added `Docs/Reports/SHINOBU_258_DataMonolith_SourceCoverage.md` with active source roots, current authored row counts, generated sections, and missing production sections.
- Materialized excluded schema headers under `Data/Balance/Schemas`.
- Added a schema README warning that templates are not payload proof and must not be copied into active roots as empty rows.
- Updated `DATA_MONOLITH_H8BIN_SPEC.md` and `H8BIN_VALIDATOR_SHINOBU_258.md` to point at the source coverage report.

Cinematic Cheats used:
- None. This is editor/source authoring infrastructure. The deliberate "cheap path" is refusing fake runtime rows.

Exact Microseconds saved:
- Runtime frame saving: 0 us.
- Prevented future wasted bake/boot attempts by making missing sections explicit before Unity batchmode bake.

Current gate result after schema materialization:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 0.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 0.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.

Current source coverage:
- `Data/Balance/Items.csv`: 4 authored rows.
- `Data/Balance/Fauna.csv`: 3 authored rows.
- `Data/Balance/Economy.csv`: 3 authored rows.
- `Data/Balance/Physics.csv`: 3 authored rows.
- `Assets/_SourceData/DataMonolith`: no authored CSV/JSON source files.
- Missing non-generated production sections: `Biomes`, `Recipes`, `LootCdf`, `VoxelMaterials`, `AudioClipRegistry`, `VfxScalars`, `ToolHeatCapacity`, `SubmarineHullConstants`, `PhysicsMaterials`, `GhostModules`, `SpawnCreditCosts`, `SopErrors`, `HudLayouts`, `SectorPageDirectory`.

Verification:
- `Data/Balance/Schemas`: 22 CSV template headers plus `README_SHINOBU_258.md`.
- `git diff --check` on schema/report/doc/status/rationale files: passed with CRLF normalization warnings only.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for missing binary payload, 1.491853 seconds.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

## 2026-05-21 Data Monolith Seed Rows And Semantic Gate

What was wrong:
- The Data Monolith source coverage report had only Items, Fauna, Economy, and Physics rows.
- The compiler coverage gate was count-based. Blank IDs could hash to `0` and still satisfy section coverage.
- Schema-style filenames such as `AudioRegistry.csv`, `VfxScalars.csv`, `PhysicsMaterials.csv`, and `SectorPages.csv` needed explicit parser aliases.
- `Data/Balance/Schemas/VoxelMaterials_template.csv` used `melting_point`, while the compiler reads `melting_point_c`.

What was done:
- Added active production seed rows under `Data/Balance` for `Biomes`, `Recipes`, `Loot`, `VoxelMaterials`, `AudioRegistry`, `VfxScalars`, `ToolHeat`, `SubmarineHull`, `PhysicsMaterials`, `GhostModules`, `SpawnCredits`, `SopErrors`, `HudLayout`, and `SectorPages`.
- Added `basalt_fractured` to `PhysicsMaterials.csv` so both voxel material surface references resolve.
- Added compact table-name aliases in `H8DataMonolithCompiler` for schema-style CSV filenames.
- Hardened `ValidateCrossReferences` with nonzero identity checks and semantic links:
  `Biome -> VoxelMaterial`, `BiomeHeatmap -> Biome`, `VoxelMaterial -> Item`, `VoxelMaterial -> PhysicsMaterial`,
  `GhostModule -> Recipe output`, `SpawnCredit -> Creature`, and `SectorPage -> Biome`.
- Fixed `VoxelMaterials_template.csv` to `melting_point_c`.
- Updated `SHINOBU_258_DataMonolith_SourceCoverage.md`, `H8BIN_VALIDATOR_SHINOBU_258.md`,
  `DATA_MONOLITH_H8BIN_SPEC.md`, and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic Cheats used:
- No runtime simulation added. The cheap path is failing bad source rows at editor bake instead of discovering broken static truth in player boot.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- Cold/player risk reduced by preventing malformed rows from reaching `static_data.h8bin`; no runtime route or DTO layout changed.

Current authored source coverage:
- Required non-generated monolith sections now have active `Data/Balance` rows.
- Generated sections remain `DepthPressureCurve`, `LightAttenuationCurve`, and normalized `BiomeHeatmap`.
- `SectorPages.csv` rows are directory coverage only with `byte_count=0`; real world-page byte payload routing is still pending.

Current gate result after semantic hardening:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 0.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 0.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.
- `migration_summary.owner_count`: 0.

Verification:
- `H8DataMonolithCompiler.cs` brace/preprocessor count: 326 `{`, 326 `}`, 1 `#if`, 1 `#endif`.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK in 10.332s.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for missing binary payload, 1.073008 seconds.
- `git diff --check` on touched files: passed with CRLF normalization warnings only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

## 2026-05-21 Data Monolith Numeric And Duplicate Source Gate

What was wrong:
- A real Unity bake is the next proof step, but the local CPU guard sampled `99.815823` percent. Launching Unity batchmode under that load would violate the project's build discipline.
- The editor compiler still relied on post-bake validation for duplicated static identities, NaN/Infinity floats, inverted depth ranges, and impossible sector AUP values.

What was done:
- Confirmed Unity `6000.4.1f1` is installed and no `dotnet`, `csc`, or `Unity` process was active.
- Refused Unity batchmode bake under the CPU guard.
- Added `ValidateUniqueProductionHashes` for production identity sections that do not already have duplicate checks.
- Added `ValidateProductionNumericRanges` for finite floats, positive critical item/physical/crafting quantities, nonzero audio bank hashes, valid depth ranges, and sector AUP bounds.
- Updated `SHINOBU_258_DataMonolith_SourceCoverage.md`, `H8BIN_VALIDATOR_SHINOBU_258.md`,
  `DATA_MONOLITH_H8BIN_SPEC.md`, `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`, status, and rationale.

Cinematic Cheats used:
- No runtime simulation added. The source gate rejects corrupt authoring rows before Unity emits bytes, which is cheaper than validating a bad blob after the fact.

Exact Microseconds saved:
- Runtime frame saving: 0 us claimed.
- Editor/CI prevention only: bad rows fail before player boot and before devices map corrupt static bytes.

Current gate result:
- Expected to remain `FAIL` until `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` exists.
- Text runtime route remains clean: no deployed CSV/JSON/XML artifacts and no runtime `StreamingAssets` text loader findings in the latest validator report.

Verification:
- Unity bake not launched: CPU guard sampled `99.815823`, then `100.000000`.
- `H8DataMonolithCompiler.cs` brace/preprocessor count: 347 `{`, 347 `}`, 1 `#if`, 1 `#endif`.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK in 21.542s.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`, 1.052193 seconds.
- `git diff --check` on touched files: passed with CRLF normalization warnings only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

## 2026-05-21 Runtime Fallback And CSV Truncation Gate

What was wrong:
- `ShinobuOceanSurfaceAtmosphereRuntime` still used latest-created DataVault fallback for normal owner/tuner snapshot routes.
- `SubmarineOsThermalGridRuntime` could create a private standalone `GlobalDataVault` fallback in player runtime if the registered Vault was missing.
- `Data/Balance/HudLayout.csv` had row tails beyond the header, and `ReadCsvRows` silently truncated them.

What was done:
- Split ocean Vault resolution into registered-owner and diagnostic-snapshot routes. Owner/tuner paths now require `GlobalRegistry.DataVault`; latest-created fallback is diagnostic-only.
- Wrapped submarine OS latest-created/standalone Vault fallback in `UNITY_EDITOR`; player builds now fail closed without a registered Vault.
- Fixed `HudLayout.csv` to the 64-byte `H8HudLayoutRecord` schema and made `ReadCsvRows` throw on header/value count mismatch.
- Updated the SHINOBU_258 source coverage report and stable binary payload docs.

Cinematic Cheats used:
- None. This pass removes hidden authority and silent source truncation; no runtime simulation was added.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Player non-editor memory fallback reduced by removing the 2 MB standalone Vault arena path from `SubmarineOsThermalGridRuntime`.

Current gate result:
- `FAIL`.
- `UNBAKED_ARTIFACT`: 0.
- `RUNTIME_TEXT_STREAMINGASSETS_LOAD`: 0.
- `STATIC_DATA_MISSING`: `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `NO_H8BIN_FILES`: no `.h8bin` files under `Assets/StreamingAssets`.
- `migration_summary.owner_count`: 0.

Verification:
- `H8DataMonolithCompiler.cs`: 349 `{`, 349 `}`, 1 `#if`, 1 `#endif`.
- `ShinobuOceanSurfaceAtmosphereRuntime.cs`: 192 `{`, 192 `}`, 2 `#if`, 2 `#endif`.
- `SubmarineOsThermalGridRuntime.cs`: 214 `{`, 214 `}`, 4 `#if`, 4 `#endif`.
- Active `Data/Balance` CSV row counts match headers: `CSV_COLUMN_COUNTS_OK`.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK in 20.268s.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`, 1.639079 seconds.
- `git diff --check` on touched files: passed with CRLF normalization warnings only.
- Unity bake not launched: no `dotnet`/`csc`/`Unity` process was found, but CPU sampled `100.000000`.

## 2026-05-21 Fabrication DataVault Fallback Cut

What was wrong:
- `FabricationAssemblerRuntime.ResolveVault()` could bind normal runtime state through `GlobalDataVault.TryGetLatestCreated()` when the registered DataVault service was missing.

What was done:
- Removed latest-created fallback from fabrication. The file now resolves cached `_vault` or `GlobalRegistry.DataVault` only.

Cinematic Cheats used:
- None. This is authority cleanup.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- One hidden global fallback path removed from fabrication buffer ownership.

Verification:
- `FabricationAssemblerRuntime.cs`: 178 `{`, 178 `}`, 3 `#if`, 3 `#endif`.
- `rg TryGetLatestCreated` on fabrication/atmosphere/power now shows no fabrication hit; remaining touched-route hits are atmosphere diagnostic snapshot and power editor-only fallback.
- `git diff --check` on touched files: passed with CRLF normalization warnings only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

## 2026-05-21 Runtime DataVault Latest-Created Cut

What was wrong:
- Normal runtime owners and consumers still used `GlobalDataVault.TryGetLatestCreated()` as fallback authority after `GlobalRegistry.DataVault` failed or instead of registry injection.
- A macro ecosystem gizmo route was diagnostic by intent but not explicitly `UNITY_EDITOR` guarded.

What was done:
- Removed latest-created fallback from Fauna, Lighting GI, Thermodynamics, VehicleMotor, VolcanicUpdraft, ChemicalInfluence, BiomeTransition, Migration, MacroEcosystem, DroneFleet, PDA cartography, Scavenging, LocRegistry, DiegeticGlitch, and FutureCommandSandboxValidator.
- Added `UNITY_EDITOR` guard to `MacroEcosystemHeatmapGizmo.OnDrawGizmos()`.
- Kept remaining latest-created routes only where they are diagnostic/editor/smoke/crash: ocean diagnostic snapshot, power editor fallback, signal crash dump route, batch smoke tester, gizmos/inspector windows, and the `GlobalDataVault` definition.

Cinematic Cheats used:
- None. This pass removes hidden memory authority; no simulation path was added.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Removed hidden runtime fallback ownership and one player-build diagnostic probe.

Current gate result:
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`, 1.500298 seconds.

Verification:
- Modified C# files: balanced braces and preprocessor pairs.
- `rg TryGetLatestCreated` on the modified normal-runtime files: no hits.
- Non-editor scan residual callsites: diagnostic/editor/smoke/crash only plus `GlobalDataVault.cs` definition.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK in 19.080s.
- `git diff --check` on touched runtime files: passed with CRLF normalization warnings only.
- CPU sampled `100`; Unity bake blocked by guard. No `dotnet build`, Unity import, Play Mode, profiler, or player build was launched.

## 2026-05-21 Job Completion Audit Start

What was wrong:
- Raw `.Complete()` is mostly centralized in `DispatcherJobFence`, smoke testers, and plugin/editor-style nodes, but many runtime systems route through `DispatcherJobFence.TryComplete` / `DispatcherJobSwap.TryComplete`.
- `PlayerExplorationTracker` cartography upload completion did not clear `H8Memory` active-job registration after finalization, and a helper named teardown was also used for structural buffer mutation.

What was done:
- Started wrapper-completion classification instead of mechanically deleting `.Complete()` callsites.
- Repaired PDA cartography upload completion bookkeeping: normal publish remains non-blocking via `TryFinalizeCompleted`; structural clear/init uses an explicit `[BLOCKING_SYNC_POINT]`; all completion paths now clear `H8Memory.RegisterActiveJob(SystemID.UI, default)`.

Cinematic Cheats used:
- None. This is job-fence truth cleanup.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Removed stale active-job telemetry risk; no runtime algorithm changed.

Verification:
- `PlayerExplorationTracker.cs`: 214 `{`, 214 `}`, 2 `#if`, 2 `#endif`.
- `git diff --check` on PDA/status/rationale/log touched set: passed with CRLF normalization warnings only.
- Direct `.Complete()` scan: no direct `.Complete()` in `PlayerExplorationTracker.cs`.
- No `dotnet build`, Unity import, Play Mode, profiler, or player build was launched.

Follow-up same pass:
- `MacroEcosystemMathematicianRuntime` no longer routes DataVault replacement through a generic teardown-named forced completion. Hot-swap/rebind now use `CompleteScheduledJobForVaultSwapBarrier()` with `[BLOCKING_SYNC_POINT]`; disposal keeps `CompleteScheduledJobForTeardown()`.
- Verification: `MacroEcosystemMathematicianRuntime.cs` has 169 `{`, 169 `}`, 3 `#if`, 3 `#endif`; `git diff --check` passed with CRLF normalization warnings only.

## 2026-05-21 Active Job Bookkeeping Pass

What was wrong:
- `GlobalPhysicsStateManager` scheduled physics culling jobs without registering the active fence in `H8Memory`.
- Thermodynamics jobs cleared local pending flags but did not consistently clear active-job ownership after finalization or teardown drain.

What was done:
- `GlobalPhysicsStateManager` now registers `_physicsCullingJobHandle` at schedule time and clears `H8Memory` after publication or discard.
- `ThermodynamicsHazardGridRuntime` clears active thermodynamics ownership after late-frame completion and teardown drain.
- `AbyssalThermodynamicsSolver` clears thermodynamics ownership during teardown after pending/sample drains.
- Forced discard/teardown paths are labeled `[BLOCKING_SYNC_POINT]`; normal publication paths still use non-blocking `TryFinalizeCompleted`.

Cinematic Cheats used:
- None. This is job-fence and black-box proof cleanup.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Prevents stale active-job forensic state; does not alter solver work.

Verification:
- `GlobalPhysicsStateManager.cs`: 406 `{`, 406 `}`, 4 `#if`, 4 `#endif`.
- `ThermodynamicsHazardGridRuntime.cs`: 138 `{`, 138 `}`, 1 `#if`, 1 `#endif`.
- `AbyssalThermodynamicsSolver.cs`: 83 `{`, 83 `}`, 2 `#if`, 2 `#endif`.
- `git diff --check` on touched code/docs: passed with CRLF normalization warnings only.
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: passed.
- `python Tools\test_h8bin_validator.py`: 10 tests OK in 17.065s.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: FAIL only for `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`, 0.954885 seconds.
- CPU sampled `100`; Unity bake remains blocked. No `dotnet build`, Unity import, Play Mode, profiler, or player build was launched.

## 2026-05-21 Runtime GlobalRegistry Hot-Polling Cut - Second Pass

What was wrong:
- `PDAInventoryTab` still read `GlobalRegistry.Player`, `GlobalRegistry.NativeInputManager`, and `GlobalRegistry.Audio` from inventory UI work paths.
- `SpectrumSystem` still read `GlobalRegistry.Audio` in passive radar/abyssal anchor audio routes.
- `HectonFloatingOrigin` still read `GlobalRegistry.Player` and `GlobalRegistry.Submarine` from safe-teleport and drift tracker routes.
- `DestructibleOrganicManager` still read `GlobalRegistry.PlayerInventory`, `GlobalRegistry.PersistentWorldRegistry`, and `GlobalRegistry.Audio` from drop drain, persistence sync, harvest audio, and spore audio paths.
- Shared-core `GlobalSignals.SignalBus<T>.FlushPreSimulation` and `SystemDispatcher.RunDispatcherUpdate` still mutate registry-backed kill-switch/time precision state and require a core route-card, not a mechanical local cache.

What was done:
- `PDAInventoryTab` now caches player/input/audio services on enable and rebinds them through `IGlobalRegistryHotSwapListener`.
- `SpectrumSystem` now caches audio/spatial audio, rebinds audio through hot-swap, and handles DataVault replacement by releasing/reacquiring Spectrum Vault handles.
- `HectonFloatingOrigin` now caches player/submarine runtime contexts during owner boot and rebinds both through the existing hot-swap listener.
- `DestructibleOrganicManager` now caches inventory/world/audio services and rebinds them through `IGlobalRegistryHotSwapListener`; drop drain, persistence sync, harvest audio, and spore audio use cached references.
- `GlobalSignals` and `SystemDispatcher` ranked callsites were inspected and recorded as `PENDING CORE ROUTE CARD`.

Cinematic Cheats used:
- None added in this pass. This is authority-route cleanup.
- Existing sonar/flora presentation remains compatible with visual/audio fakes; this pass removes service lookup from those routes instead of adding simulation.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Measurable proof target is architectural: registry reads were removed from PDA auto-resolve/parallax/sound, passive sonar audio, floating-origin safety tracking, flora drop drain, persistence sync, and flora audio paths.

Verification:
- Remaining `GlobalRegistry.NativeInputManager/Audio/Player/Submarine/PlayerInventory/PersistentWorldRegistry` hits in the four touched files are cold cache or hot-swap callback reads only.
- `PDAInventoryTab.cs`: 320 `{`, 320 `}`, 0 `#if`, 0 `#endif`.
- `SpectrumSystem.cs`: 327 `{`, 327 `}`, 0 `#if`, 0 `#endif`.
- `HectonFloatingOrigin.cs`: 219 `{`, 219 `}`, 6 `#if`, 6 `#endif`.
- `DestructibleOrganicManager.cs`: 390 `{`, 390 `}`, 0 `#if`, 0 `#endif`.
- `git diff --check` on the touched runtime files: passed with CRLF normalization warnings only.
- Build guard: CPU sampled `96.3258405406834`, above the local 50 percent threshold. No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent; current binary payload gate remains red by design.
- Shared-core registry mutation routes in `GlobalSignals` and `SystemDispatcher` need a dedicated route-card and owner-state replacement pass.

## 2026-05-21 Runtime GlobalRegistry Kill-Switch Read Cut

What was wrong:
- `HectonFluidEngine` and `SargassumMicroFaunaBoids` used `GlobalRegistry.SystemKillSwitchMask` as a direct hot read surface for abyssal-flow and ambient-fauna VFX shedding.
- Core still has registry mutation producers in `GlobalSignals`/`SystemDispatcher`; those require a separate owner route-card and were not safe for blind rewrite.

What was done:
- Added `SystemKillSwitchBitsSignal`, explicit layout, 32 bytes.
- Registered/flushed/cleared/configured the typed lane in `GlobalSignals`.
- `GlobalRegistry.SetSystemKillSwitchBits` now publishes previous/current/changed mask state after a successful atomic CAS.
- `HectonFluidEngine` and `SargassumMicroFaunaBoids` now consume `SignalBus<SystemKillSwitchBitsSignal>.GetFrameSnapshot()` once per frame and cache the latest mask locally.
- Side-agent read-only audit was integrated as evidence: `PublishAbsoluteUniverseTime`, `TickMathPrecisionTransition`, `SetSystemKillSwitchBits`, and `GlobalRegistry.JobAdmission` in core phase loops stay marked `PENDING CORE ROUTE CARD`.

Cinematic Cheats used:
- None added. This pass preserves the existing cheap VFX kill-switch route and removes direct registry mask polling.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Architectural proof: `rg "GlobalRegistry\.SystemKillSwitchMask" Assets/_Project/Scripts` now returns no runtime consumers.

Verification:
- `HectonSignalLaneContract.cs`: 16 `{`, 16 `}`, 0 `#if`, 0 `#endif`.
- `GlobalRegistry.cs`: 678 `{`, 678 `}`, 19 `#if`, 19 `#endif`.
- `GlobalSignals.cs`: 843 `{`, 843 `}`, 7 `#if`, 7 `#endif`.
- `HectonFluidEngine.cs`: 623 `{`, 623 `}`, 9 `#if`, 9 `#endif`.
- `SargassumMicroFaunaBoids.cs`: 584 `{`, 584 `}`, 4 `#if`, 4 `#endif`.
- `git diff --check` on touched code files passed with CRLF normalization warnings only.
- No direct `GlobalRegistry.SystemKillSwitchMask` consumers remain under `Assets/_Project/Scripts`.
- Build guard: CPU sampled `86.7375468748143`, above the 50 percent threshold. No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Core producer route still needs a route-card before removing registry mutation calls from `GlobalSignals` and `SystemDispatcher`.
- `static_data.h8bin` remains absent; Data Monolith payload gate is still red.

## 2026-05-21 AUP JobAdmission Barrier Route Cut

What was wrong:
- `GlobalSignals.Publish(AupPreShiftSignal)` and `Publish(AupShiftSignal)` mutated `GlobalRegistry.JobAdmission` directly.
- That made a signal publish API responsible for scheduler service mutation through the registry instead of routing through the dispatcher owner.

What was done:
- `SystemDispatcher.RequestAupPreShiftPause` now sets the cached `_jobAdmission` AUP barrier true before DataVault allocation lock and AUP fence completion.
- Added `SystemDispatcher.ReleaseAupPreShiftPause` to set the cached `_jobAdmission` AUP barrier false.
- `GlobalSignals.Publish(AupPreShiftSignal/AupShiftSignal)` now routes through `SystemDispatcher.ActiveRuntimeInstance` and no longer calls `GlobalRegistry.JobAdmission`.

Cinematic Cheats used:
- None. This is scheduler authority routing.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Architectural proof: the `GlobalRegistry.JobAdmission` hit left in `SystemDispatcher` is cold dependency refresh; the direct hits in `GlobalSignals` are gone.

Verification:
- `GlobalSignals.cs`: 843 `{`, 843 `}`, 7 `#if`, 7 `#endif`.
- `SystemDispatcher.cs`: 635 `{`, 635 `}`, 33 `#if`, 33 `#endif`.
- `git diff --check` on the touched files passed with CRLF normalization warnings only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `PublishAbsoluteUniverseTime` and `TickMathPrecisionTransition` still mutate registry-owned/shared global state from dispatcher frame update and need a separate consumer-map route-card.
- `SetSystemKillSwitchBits` producers still require owner-route cleanup after the new typed signal consumers are proven in Unity.

## 2026-05-21 AbsoluteUniverseTime Consumer Cut

What was wrong:
- `GlobalRegistry.AbsoluteUniverseTime` was still read directly by celestial solve, weather shader bridge, random event seeds, physics-water tide fallback, and seismic/tide fallback.
- This kept time as a registry-pulled fact after typed time and celestial snapshot routes already existed.

What was done:
- `HectonCelestialEngine.ResolveSynchronizedUniverseTimeSeconds()` now uses owner-local `Time.timeAsDouble` and keeps finite/negative guards.
- `GlobalWeatherDirector.ResolveAtmosphericBridgeTimePhase()` now uses the already-read `CelestialRuntimeSnapshot` and fails closed to the existing weather intensity phase.
- `RandomEventSystem` now refreshes `_cachedUniverseTimeSeconds` from `SignalBus<GlobalTimeSyncSignal>.GetFrameSnapshot()` and reads that cached scalar for meteor/seismic seed construction.
- `GlobalPhysicsStateManager.ResolveAbsoluteUniverseTideTimeSeconds()` now uses the passed fallback time when the celestial snapshot is invalid.
- `HectonSeismicTideDirector.RefreshCachedRuntimeState()` now caches celestial snapshot time or `Time.timeAsDouble` as dispatcher-absence fallback; no registry time read remains.

Cinematic Cheats used:
- Existing triangle-wave tide/cloud/meteor timing remains the cheap visual fake. No physics simulation was added.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Architectural proof: `rg "GlobalRegistry\.AbsoluteUniverseTime" Assets/_Project/Scripts -g "*.cs"` returns no callsites.

Verification:
- `GlobalWeatherDirector.cs`: 105 `{`, 105 `}`, 3 `#if`, 3 `#endif`.
- `HectonCelestialEngine.cs`: 493 `{`, 493 `}`, 11 `#if`, 11 `#endif`.
- `RandomEventSystem.cs`: 190 `{`, 190 `}`, 6 `#if`, 6 `#endif`.
- `GlobalPhysicsStateManager.cs`: 405 `{`, 405 `}`, 4 `#if`, 4 `#endif`.
- `HectonSeismicTideDirector.cs`: 294 `{`, 294 `}`, 7 `#if`, 7 `#endif`.
- `git diff --check` on touched code passed with CRLF normalization warnings only.
- Build guard: CPU sampled `100.00` percent. No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Superseded below by `2026-05-21 Dispatcher Math Precision Facade Cut`: the direct dispatcher call was narrowed to `FrameTimeWatchdog.TickMathPrecisionTransition`.
- `GlobalRegistry.PublishAbsoluteUniverseTime` remains as an inert internal method/property surface until compile proof allows API removal.
- Math precision transition remains shared-core route-card debt, not part of the absolute-time consumer cut.

## 2026-05-21 Dead AbsoluteUniverseTime Producer Cut

What was wrong:
- After the consumer cut, `SystemDispatcher.RunDispatcherUpdate` still wrote `Time.timeAsDouble` into `GlobalRegistry.PublishAbsoluteUniverseTime(...)` every frame.
- No runtime code under `Assets/_Project/Scripts` read `GlobalRegistry.AbsoluteUniverseTime` anymore, so the write was dead global mutation.

What was done:
- Removed the dispatcher call to `GlobalRegistry.PublishAbsoluteUniverseTime(Time.timeAsDouble)`.
- Left the internal registry method/property in place to avoid API-surface churn without Unity compile proof.

Cinematic Cheats used:
- None. This is route hygiene.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Architectural proof: only the inert `GlobalRegistry.PublishAbsoluteUniverseTime` method definition remains; no dispatcher call remains.

Verification:
- `SystemDispatcher.cs`: 635 `{`, 635 `}`, 33 `#if`, 33 `#endif`.
- `git diff --check` on `SystemDispatcher.cs` passed with CRLF normalization warnings only.
- `rg "GlobalRegistry\.AbsoluteUniverseTime" Assets/_Project/Scripts -g "*.cs"` returns no callsites.

Remaining:
- Superseded below by `2026-05-21 Dispatcher Math Precision Facade Cut`: the direct dispatcher-to-registry call was removed; full math-precision ownership extraction still needs a separate route-card/consumer map.

## 2026-05-21 Dispatcher Math Precision Facade Cut

What was wrong:
- `SystemDispatcher.RunDispatcherUpdate` still called `GlobalRegistry.TickMathPrecisionTransition(Time.frameCount)` directly.
- That made dispatcher frame update depend on registry math-precision mutation, even though `FrameTimeWatchdog` owns degradation decisions and already caches registry precision writer delegates.

What was done:
- Added `FrameTimeWatchdog` cold-bound `MathPrecisionTransitionTicker`.
- Added internal `FrameTimeWatchdog.TickMathPrecisionTransition(int frame)`.
- Replaced the dispatcher call with `FrameTimeWatchdog.TickMathPrecisionTransition(Time.frameCount)`.

Cinematic Cheats used:
- None. This preserves the existing 60-frame shader math LOD ramp.

Exact Microseconds saved:
- Hot-frame saving: 0 us claimed.
- Architectural proof: the only `GlobalRegistry.TickMathPrecisionTransition` reference outside `GlobalRegistry` is now the cold-bound watchdog delegate.

Verification:
- `FrameTimeWatchdog.cs`: 28 `{`, 28 `}`, 0 `#if`, 0 `#endif`.
- `SystemDispatcher.cs`: 635 `{`, 635 `}`, 33 `#if`, 33 `#endif`.
- `git diff --check` passed on touched files with CRLF normalization warnings only.
- Build guard: CPU sampled `94.94` percent. No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Full math-precision ownership extraction is still pending because `GlobalRegistry` owns shader keyword/blend mutation and many systems still read math precision state.

## 2026-05-21 CSV Diff And KCC Text Payload Regression Cut

What was wrong:
- `--csv-diff` could pass a source CSV that produced zero hash-bearing rows.
- A new `Assets/StreamingAssets/Hecton8/locomotion_environment_profiles.csv` reintroduced runtime text payload debt.

What was done:
- `Tools/h8bin_validator.py` now fails closed on empty/no-hash/zero-hash CSV diff sources and accepts hash headers case-insensitively.
- `locomotion_environment_profiles.csv` and its `.meta` moved to `Assets/_SourceData/Physics/KCC`.
- Added `Physics/KCC` migration-summary classification for future KCC locomotion text regressions.
- JUnit output now emits concrete failure nodes for missing payload/directory errors, and CI metrics use the current run date.

Cinematic Cheats used:
- Validator still uses deterministic sampled payload inspection after full header/table proof; no runtime simulation added.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 14 tests OK; live gate fails only on `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`.
- `Docs/Reports/SHINOBU_258_h8bin_validation_current.junit.xml` now contains `<failure type="STATIC_DATA_MISSING">` and `<failure type="NO_H8BIN_FILES">`.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent.
- No `.h8bin` files are currently present under `Assets/StreamingAssets`.

## 2026-05-21 Mmap Fail-Fast Cleanup Cut

What was wrong:
- The checksum path released its `memoryview`, but its wider function-scope `payload_view` variable made Task 20 mmap-closure proof harder than necessary.
- A future edit could accidentally leave an exported pointer alive when `--fail-fast` raises after checksum validation.

What was done:
- Added `compute_payload_checksum(mm_obj, header_size)`.
- The helper owns the checksum `memoryview` and releases it in `finally`.
- Added a fail-fast checksum mismatch regression that rejects `BufferError`/traceback and verifies `XXHASH3_MISMATCH` still reaches the report.

Cinematic Cheats used:
- None. This is external CI file-handle safety.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 35 tests OK; `python Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`.
- Live gate: `STATIC_DATA_MISSING` and `NO_H8BIN_FILES` only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent.
- No `.h8bin` files are currently present under `Assets/StreamingAssets`.

## 2026-05-21 Csv-Diff External Target Firewall Cut

What was wrong:
- `--csv-diff` accepts a generated `.h8bin` path that may live outside `--target-dir`.
- Its item-hash extraction path read section entries directly, so a corrupt external diff target could bypass the main firewall's range/stride proof or emit misleading missing-hash noise.

What was done:
- External diff targets now run through `validate_h8bin_file` with fail-fast disabled before `Items.HashId` extraction.
- Fatal probe findings are copied into the main report; if the probe is corrupt, the CSV comparison stops before `CSV_TO_BIN_MISSING_HASHES`.
- Added a regression for a corrupt external Items section offset.

Cinematic Cheats used:
- None. This is proof-route hardening for the designer CSV-to-binary bridge.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 29 tests OK.
- Live gate: `STATIC_DATA_MISSING` and `NO_H8BIN_FILES` only.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent.
- No `.h8bin` files are currently present under `Assets/StreamingAssets`.

## 2026-05-21 Record Stride Sampling Guard Cut

What was wrong:
- A non-empty section with `record_size == 0` or a stride smaller than its C# explicit-layout struct could still reach item-hash extraction or payload sampling.
- That created two bad outcomes: traceback-only CI failure or a false sampled read across the declared record boundary.

What was done:
- Added `RECORD_SIZE_ZERO`, `RECORD_SIZE_UNDER_STRUCT`, and `FIELD_EXCEEDS_RECORD_SIZE` fatal findings.
- Sections with invalid payload stride are not admitted into `entries_by_name`, so `gather_item_hashes` and `validate_record_sample` cannot read with a corrupt stride.
- Added regression tests for zero stride and under-struct stride.

Cinematic Cheats used:
- Same Dear Lie remains: full header/directory/stride proof plus sampled record validation. The cheap sampling route is now gated by an explicit ABI stride proof.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 28 tests OK.
- Live gate: `STATIC_DATA_MISSING` and `NO_H8BIN_FILES` only; `migration_summary.owner_count=0`.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent.
- No `.h8bin` files are currently present under `Assets/StreamingAssets`.

## 2026-05-21 Variant CSharp Layout Syntax Parser Cut

What was wrong:
- The validator could miss explicit-layout DTOs if C# authors used `StructLayoutAttribute`, namespaced or `global::` attributes, extra attributes between layout attributes and declarations, alternate readonly/partial modifier order, or casted integer layout expressions.
- `(int)16` sanitized to `()16`, making otherwise valid layout constants unresolved.

What was done:
- Widened struct and field regexes to accept namespaced/global `StructLayout(Attribute)` and `FieldOffset(Attribute)` forms plus intervening attributes.
- Added balanced named-argument extraction for `Size` and `Pack`.
- Fixed integer-cast sanitization order for `(int)`/`(uint)` style casts.
- Added a regression case where a variant `FieldOffsetAttribute((int)1)` field must be parsed and must trigger `FIELD_ALIGNMENT`.

Cinematic Cheats used:
- None beyond the existing validator Dear Lie: schema parsing remains static regex plus focused regression fixtures, not a heavyweight compiler invocation.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 23 tests OK.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Live Data Monolith payload is still absent from `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

## 2026-05-21 Csv-Diff Fail-Fast Probe Report Cut

What was wrong:
- `--csv-diff` used a separate probe state to read `Items.HashId`.
- With `--fail-fast`, corrupt probe binaries could abort before probe findings were copied into the main JSON/JUnit report.

What was done:
- Binary probe validation now runs fail-fast-neutral.
- Probe errors are appended into the main validator state, then `FailFastAbort` is raised through the normal report-preserving path.
- Added a regression using a valid target directory plus an empty external diff target.

Cinematic Cheats used:
- None. This is CI evidence preservation for the human-readable CSV-to-binary bridge.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 26 tests OK.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Live Data Monolith payload is still absent from `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

## 2026-05-21 Lazy Sampling Index Cut

What was wrong:
- `--thorough` used `list(range(count))`; a huge `.h8bin` section could allocate millions of Python integers before validation.
- Default 5 percent sampling also staged index collections, working against the mmap/no-full-staging rule.

What was done:
- `sample_indices` now returns `range(count)` for thorough/full coverage paths.
- Default sampling returns a deterministic iterator that emits the first record, last record, then a coprime-stride pseudo-random walk.
- Added regression coverage for a billion-record thorough path and default non-list edge sampling.

Cinematic Cheats used:
- This preserves the Dear Lie sampling strategy: full header/table proof plus streamed sampled record inspection. No full payload copy and no full index staging.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- CI memory reduced from O(sample_count) Python index storage to O(1) traversal state for default and thorough sampling iteration.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 25 tests OK.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Live Data Monolith payload is still absent from `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.

## 2026-05-21 Fail-Fast And Section Corruption Regression Cut

What was wrong:
- `--fail-fast` could abort inside C# schema parsing before JSON/JUnit reports were written.
- Out-of-file or overlapping section table entries were fatal, but could still be sampled afterward, risking Python traceback instead of a named validator finding.
- The test suite did not explicitly cover `Pack=1`, bad struct-size alignment, AUP bound overflow, malformed section ranges, malformed RLE probe, or fail-fast report persistence.

What was done:
- Schema parsing now suppresses immediate fail-fast and transfers layout findings into the main validator state before stopping.
- Invalid section byte ranges are excluded from payload sampling after the fatal section-table finding is recorded.
- Added synthetic corruption tests for `PACK_1_FORBIDDEN`, `STRUCT_SIZE_ALIGNMENT`, `AUP_OUT_OF_BOUNDS`, `SECTION_OVERLAP`, `SECTION_OUT_OF_FILE`, `SECTION_ALIGNMENT`, `RLE_UNPACKED_SIZE_MISMATCH`, and fail-fast report emission.
- Section-table corruption findings now include a hex dump around the 16-byte directory row for order/size/offset/alignment/range/overlap failures.

Cinematic Cheats used:
- The validator still uses the intended Dear Lie: full header/directory proof plus sampled record validation, not full 2GB byte-by-byte scans by default.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed after isolated rerun; `python Tools\test_h8bin_validator.py` ran 22 tests OK; `python Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`.
- Live gate remains `FAIL` only on `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`; `migration_summary.owner_count=0`.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is still absent.
- No `.h8bin` files are currently present under `Assets/StreamingAssets`.

## 2026-05-21 Current Request Verification Pass

What was wrong:
- The live repository already contained `Tools/h8bin_validator.py`, `Tools/test_h8bin_validator.py`, and SHINOBU_258 proof artifacts. The current request required creation of the validator, but a second implementation would create a parallel CI route instead of strengthening the existing one.

What was done:
- Re-extracted the SHINOBU_258 XML prompt from `Docs/Tasks/CURRENT_BATCH.md`.
- Re-read `Docs/Actual Domains of Project.txt`, `Docs/Tasks/Status_SHINOBU_258.md`, and `Docs/AgentLogs/Rationale_SHINOBU_258.md`.
- Reviewed the relevant mandate files: ARM64 layout, binary checksum, AUP determinism, CSV-to-binary bridge, math CI gate, performance budget, zero-GC, and telemetry/postmortem.
- Ran `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`; result: pass.
- Ran `python Tools\Security\ReplayHasher.py self-test`; result: `SELFTEST_OK`.
- Ran `python Tools\test_h8bin_validator.py`; result: 26 tests OK in 28.282s.
- Ran the live gate against `Assets\StreamingAssets` with Data Monolith C# source roots; result: fail only on `STATIC_DATA_MISSING` and `NO_H8BIN_FILES`.
- Rewrote `Docs/Reports/SHINOBU_258_h8bin_validation_current.json` and `.junit.xml` through the validator.

Cinematic Cheats used:
- The validator keeps the Dear Lie sampling path: full header/schema/directory/checksum proof plus deterministic 5 percent sampled payload validation by default, with `--thorough` for full record checks.

Exact Microseconds saved:
- Runtime saving: 0 us claimed. The validator is external CI tooling and does not enter Unity hot paths.
- CI memory protection remains mmap-based; no full `.h8bin` bytearray staging was introduced.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` is absent.
- No `.h8bin` files are currently present under `Assets/StreamingAssets`.
- Status remains `PENDING VERIFICATION` for runtime readiness until a real Data Monolith bake/import/boot proof exists.

## 2026-05-21 Polish Audit Closure: Source-Backed Flags, AUP Integers, CSV Field Diff

What was wrong:
- The external audit found remaining false-proof risks: RLE probing could be inferred from a hardcoded directory bit instead of C# source, `SectionAlignmentBytes` could regress below the H8DM 16-byte floor, integer AUP fields were not range checked, recipe/loot references could skip proof when no item master existed, and `--csv-diff` only proved `Items.HashId`.
- The live StreamingAssets gate changed since the previous report: `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` now exists with `H8VB` magic and is not an H8DM Data Monolith blob.

What was done:
- RLE probe now runs only if parsed C# source defines `RleDirectoryFlag`; otherwise non-zero directory bits fail as `DIRECTORY_FLAGS_UNSUPPORTED`.
- Effective section/file/data-start alignment is clamped to a 16-byte minimum and schema parsing emits `SECTION_ALIGNMENT_CONTRACT` if C# declares less.
- Integer AUP fields named `Aup*`/`AUP*` are checked against `+/-100000` with little-endian reads.
- Recipe/Loot references now emit `REFERENCE_MASTER_EMPTY` when `Items.HashId` cannot be proven, instead of pretending a broken-key proof exists.
- `--csv-diff` now compares known numeric `H8ItemRecord` fields (`Cost`, `MassKg`, `VolumeM3`, `MaxStack`, `YieldHash`, etc.) when those columns exist in the CSV.
- `Docs/ARCHITECTURE/H8BIN_VALIDATOR_SHINOBU_258.md` now records the field-diff, RLE, integer-AUP, foreign-H8VB, and 43-test proof state.

Cinematic Cheats used:
- The Dear Lie remains intact: full C# schema/header/directory/checksum proof plus deterministic sampled payload validation by default; no full binary copy and no Unity/C# process.

Exact Microseconds saved:
- Runtime saving: 0 us claimed. This is CI/editor-only proof.
- CI memory remains mmap/page-cache based with O(1) sampling iterator state.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`; `python Tools\test_h8bin_validator.py` ran 43 tests OK in 27.126s.
- Live gate result: `FAIL` on `STATIC_DATA_MISSING` for `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` and `FOREIGN_H8BIN_SCHEMA_UNVALIDATED` for `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin`; no text-artifact owners in `migration_summary`.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

<SELF_AUDIT agent_id="SHINOBU_258" task_count="20">
  <task_reconciliation>
    <task id="01" status="PASS">Runtime `.json`/`.xml`/`.csv` artifacts in `StreamingAssets` fail as unbaked artifacts.</task>
    <task id="02" status="PASS">C# explicit layout, `StructLayoutAttribute`, `FieldOffsetAttribute`, namespaced/global attributes, and casted integer offsets are parsed from source.</task>
    <task id="03" status="PASS">H8DM magic is read little-endian through mmap; known foreign magic such as H8VB fails closed.</task>
    <task id="04" status="PASS">XXH3-64 oracle hashes `bytes[16..end)` and compares against the embedded header checksum.</task>
    <task id="05" status="PASS">Synthetic corruption regression suite covers bad magic, checksum, alignment, NaN/AUP, RLE, flags, CSV, and mmap fail-fast closure.</task>
    <task id="06" status="PASS">Section table ranges, counts, overlaps, fixed-area collisions, and table byte counts are validated.</task>
    <task id="07" status="PASS">Payload/file/data-start alignment uses `max(16, SectionAlignmentBytes)` and rejects lower C# constants.</task>
    <task id="08" status="PASS">Default payload validation samples deterministic 5 percent after full table proof; `--thorough` validates all records.</task>
    <task id="09" status="PASS">All binary unpacking uses explicit little-endian struct formats.</task>
    <task id="10" status="PASS">JSON and JUnit reports are emitted, including directory-level failures with synthetic testcases.</task>
    <task id="11" status="PASS">Recipe/Loot references validate against `Items.HashId`/known hash list and fail on empty master sets.</task>
    <task id="12" status="PASS">Floating and integer AUP fields are checked for finite/range safety.</task>
    <task id="13" status="PASS">RLE probe is source-backed by parsed `RleDirectoryFlag`; malformed pairs/zero-runs/size mismatch fail.</task>
    <task id="14" status="PASS">File reads use `mmap.ACCESS_READ`; no full-file bytearray staging in validator flow.</task>
    <task id="15" status="PASS">Metrics append MB, files, structs, seconds, and performance warning state.</task>
    <task id="16" status="PASS">CLI exposes target/source dirs, reports, fail-fast, thorough, sample percent, known hash list, max bytes, and CSV diff.</task>
    <task id="17" status="PASS">CSV diff compares hashes and known numeric item fields against generated binary records.</task>
    <task id="18" status="PASS">Fatal binary/table/layout findings carry 32-byte hexdump context where bytes exist.</task>
    <task id="19" status="PASS">Validator acts as the external H8DM static binary firewall and refuses unowned foreign `.h8bin` schemas.</task>
    <task id="20" status="PASS">Self-audit is embedded in report output and mmap checksum memoryview is released in `finally`.</task>
  </task_reconciliation>
  <struct_layout_verification>
    <struct name="H8DataBlobHeader" size="16" alignment="8">offset 0 uint Magic size 4; offset 4 ushort FormatVersion size 2; offset 6 ushort HeaderBytes size 2; offset 8 ulong Checksum64 size 8; final size 16 = 2*8 and 1*16.</struct>
    <struct name="H8DataBlobDirectory" size="64" alignment="4">sixteen 4-byte scalar slots from offsets 0..60; final size 64 = one cache line and 4*16.</struct>
    <struct name="H8DataSectionEntry" size="16" alignment="4">offsets 0/4/8/12 uint fields; final size 16 = 4*4.</struct>
  </struct_layout_verification>
  <scalability_curve>No runtime `GlobalQualityWeight` is consumed because this process is external CI. The continuous-quality doctrine is preserved by refusing to change DTO layout, save identity, gameplay truth, or authority route; `sample_percent` is a CI cadence knob and `--thorough` is the full proof path.</scalability_curve>
  <h_phi_vault_status>No `NativeArray`, `NativeList`, `NativeHashMap`, `VaultBufferHandle`, Unity job, or GlobalRegistry access exists. The only byte ownership is OS mmap scoped by Python context managers.</h_phi_vault_status>
  <pointer_aliasing_dependency_graph>No Burst kernels, no `[NoAlias]`, no `JobHandle`, no `.Complete()`. Consumes filesystem/C# source; outputs JSON/JUnit/metrics reports.</pointer_aliasing_dependency_graph>
  <compile_guard>No direct sibling Unity assembly reference was added; no C# source was edited in this pass; no `dotnet` or Unity rebuild was launched.</compile_guard>
  <dear_lie>Heavy byte-for-byte payload scans are replaced by full schema/table/checksum proof plus deterministic sampled record validation by default. Complexity moves from O(all records) field unpacking to O(headers + sections + sampled records), with `--thorough` available for nightly full scans.</dear_lie>
</SELF_AUDIT>

## 2026-05-21 Lightweight C# Layout AST Scanner Cut

What was wrong:
- The validator accepted many C# attribute variants, but the critical struct/field extraction was still owned by a regex-first path.
- Combined attribute lists such as `[Serializable, StructLayout(...)]` and `[NonSerialized, FieldOffset(...)]` were not explicitly proven.

What was done:
- Replaced the regex-first struct/field extractor with a lightweight syntax-tree scanner.
- The scanner strips comments, reads balanced attribute blocks, walks declaration braces, extracts `StructLayout`/`FieldOffset` calls from attribute lists, and parses the final field declaration statement.
- Added regression coverage for combined struct and field attribute lists.
- Updated report self-audit from `csharp_regexes` to `csharp_parser` with the concrete scanner contract.

Cinematic Cheats used:
- No Roslyn or Unity process. The external CI tool stays standalone Python and uses only the grammar slice required for explicit-layout DTO proof.

Exact Microseconds saved:
- Runtime saving: 0 us claimed.
- Verification: current H8DM source parses 32 structs; `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\test_h8bin_validator.py` ran 44 tests OK in 46.297s.
- Fresh live gate: `h8bin_validator status=FAIL files=1 structs=32 mb=0.018768 seconds=0.572785`; failures remain `STATIC_DATA_MISSING` and `FOREIGN_H8BIN_SCHEMA_UNVALIDATED`.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Live payload readiness is still blocked by absent `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin`.
- `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` remains a foreign `H8VB` payload requiring Audio/VocalBank schema proof outside SHINOBU_258.

## 2026-05-21 RLE Flag And Foreign H8BIN Schema Cut

What was wrong:
- RLE tests were red because the regression schema did not publish `RleDirectoryFlag=1u`; the validator correctly classified bit `1` as unsupported.
- Current `Assets/StreamingAssets` now contains `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` with magic `H8VB`, which is not a Data Monolith `H8DM` payload. The old validation path emitted a noisy H8DM directory cascade for this foreign ABI.

What was done:
- Added `RleDirectoryFlag` to the synthetic C# schema and added a separate unsupported directory flag regression for bit `2`.
- Added a known-foreign `.h8bin` magic guard for `H8VB`/Audio/VocalBank. SHINOBU_258 now emits `FOREIGN_H8BIN_SCHEMA_UNVALIDATED` and stops before H8DM directory parsing.
- Added the foreign-schema guard to the JSON `self_audit`.
- Repaired JUnit `tests` count after synthetic non-file failure testcases are emitted; live JUnit now reports `tests="2"` for two testcase nodes.
- Updated the current JSON/JUnit reports through the live validator.

Cinematic Cheats used:
- The Data Monolith validator still uses the Dear Lie sampling model: full schema/header/directory/checksum proof plus deterministic sampled record validation by default. Foreign-domain sidecars are not sampled through an invalid schema; they fail closed until their owning route supplies proof.

Exact Microseconds saved:
- Runtime saving: 0 us claimed. This is external CI tooling.
- CI diagnostic cost reduced by removing false H8DM cascade work for known foreign sidecars.
- Verification: `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py` passed; `python Tools\Security\ReplayHasher.py self-test` returned `SELFTEST_OK`; `python Tools\test_h8bin_validator.py` ran 43 tests OK.
- Live gate: `STATIC_DATA_MISSING` for absent `Assets/StreamingAssets/Hecton8/DataMonolith/static_data.h8bin` and `FOREIGN_H8BIN_SCHEMA_UNVALIDATED` for `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin`.
- JUnit proof: `Docs/Reports/SHINOBU_258_h8bin_validation_current.junit.xml` has `tests="2"` and `failures="2"`, matching the two emitted testcase nodes.
- No `dotnet build`, Unity import, Unity bake, Play Mode, profiler, or player build was launched.

Remaining:
- Real Data Monolith bake/import/boot proof is still absent.
- Audio/VocalBank `H8VB` needs its own validator/proof route or explicit integration with the binary payload CI ledger before SHINOBU_258 can stop flagging it.

## 2026-05-21 H8VB Sidecar Validation Pass

What was wrong:
- `Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin` is a valid `H8VB` Audio/VocalBank sidecar, not an `H8DM` Data Monolith blob. The previous SHINOBU_258 route correctly avoided an H8DM directory cascade but still emitted a generic `FOREIGN_H8BIN_SCHEMA_UNVALIDATED` blocker after enough source evidence existed to validate the ABI.
- The sidecar proof was incomplete: no FNV bank-hash check, no sorted/duplicate index proof, no exact payload contiguity, no runtime codec support gate, and no ADPCM block-header proof.

What was done:
- Added source-backed H8VB validation inside `Tools/h8bin_validator.py`: dispatch by `0x42563848` before H8DM parsing; validate header size `64`, record size `32`, endian `0xFEFF`, exact payload offset, exact file end, FNV-1a bank hash, sorted nonzero hashes, mono lanes, supported codecs `PCM16/H8ADPCM`, contiguous payload records, PCM16/ADPCM declared length, and ADPCM step/reserved bytes.
- Kept H8VB separate from Data Monolith authority. It is a sidecar proof, not an H8DM section and not a runtime playback proof.
- Expanded `Tools/test_h8bin_validator.py` to 51 tests, adding valid H8VB, corrupt header/no H8DM cascade, payload alignment, bank hash, ADPCM length, unsupported Vorbis codec, unsorted records, and ADPCM block-header regressions.
- Updated `Docs/ARCHITECTURE/H8BIN_VALIDATOR_SHINOBU_258.md`, `Docs/ARCHITECTURE/STREAMINGASSETS_TEXT_RUNTIME_MIGRATION_SHINOBU_258.md`, and `Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.

Cinematic Cheats used:
- The Data Monolith "Dear Lie" remains deterministic 5 percent record sampling after full header/directory/checksum proof for massive H8DM sections. H8VB is small enough to validate every record and every ADPCM block header without runtime cost.
- No Unity runtime decode, no DSP simulation, and no audio-thread playback was attempted. That proof remains SHINOBU_260 ownership.

Exact microseconds saved:
- Runtime/player: 0 us claimed; this is CI/editor-only.
- CI false-cascade avoidance: one foreign-H8DM error path removed for H8VB; current live gate reports one file `status=OK` instead of an unvalidated sidecar blocker.

Verification:
- `python -m py_compile Tools\h8bin_validator.py Tools\test_h8bin_validator.py`: pass.
- `python Tools\Security\ReplayHasher.py self-test`: `SELFTEST_OK`.
- `python Tools\test_h8bin_validator.py`: 51 tests OK.
- `python Tools\h8bin_validator.py --target-dir Assets\StreamingAssets --report-json Docs\Reports\SHINOBU_258_h8bin_validation_current.json --report-junit Docs\Reports\SHINOBU_258_h8bin_validation_current.junit.xml --metrics-log Docs\Reports\CI_BINARY_VALIDATION.log`: `FAIL` only on `STATIC_DATA_MISSING`; `vocal_banks.h8bin` is `status=OK`, 19,680 bytes, one sampled/validated record.
- No `dotnet` or Unity build launched.

<SELF_AUDIT>
  <TASK_RECONCILIATION>
    <task id="01" status="PASS">Runtime text purge remains enforced; current migration summary has zero text owners.</task>
    <task id="02" status="PASS">C# explicit-layout extraction remains lightweight AST scanner; 32 H8DM structs parse in current source.</task>
    <task id="03" status="PASS">H8DM magic remains source-derived; H8VB now routes to sidecar validation before H8DM parsing.</task>
    <task id="04" status="PASS">H8DM XXH3 oracle unchanged; H8VB bank hash uses source-backed FNV-1a over records plus payload.</task>
    <task id="05" status="PASS">Corruption suite expanded to 51 tests including H8VB ABI failures.</task>
    <task id="06" status="PASS">H8DM section traversal unchanged; H8VB index traversal is exact 32-byte stride.</task>
    <task id="07" status="PASS">H8DM section/file alignment unchanged; H8VB payload/file alignment requires 16-byte boundaries.</task>
    <task id="08" status="PASS">H8DM Dear Lie sampling unchanged; H8VB record/block validation is full because live bank is small.</task>
    <task id="09" status="PASS">All H8VB and H8DM reads use explicit little-endian struct formats.</task>
    <task id="10" status="PASS">JSON/JUnit reports updated; JUnit has two testcases and one failure for missing static data.</task>
    <task id="11" status="PASS">H8DM FK checks unchanged; H8VB record hashes must be nonzero and strictly sorted.</task>
    <task id="12" status="PASS">AUP checks unchanged for H8DM; H8VB has no AUP fields.</task>
    <task id="13" status="PASS">RLE source-backed gate unchanged; H8VB codec shape is handled separately.</task>
    <task id="14" status="PASS">Both H8DM and H8VB validation use mmap; no full-file bytearray staging in validator.</task>
    <task id="15" status="PASS">Metrics log appended with 1.147232 second live run.</task>
    <task id="16" status="PASS">CLI unchanged; H8VB is reached by normal target-dir scan.</task>
    <task id="17" status="PASS">CSV-to-bin diff unchanged and still probes through binary firewall.</task>
    <task id="18" status="PASS">Hex-dump formatter remains attached to binary corruption findings.</task>
    <task id="19" status="PASS">Validator remains static payload firewall; H8VB sidecar proof is recorded in architecture docs.</task>
    <task id="20" status="PASS">Self-audit updated; mmap view lifetimes remain scoped and released.</task>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <h8vb_header size="64" alignment="8">u32 Magic@0, Version@4, HeaderSize@8, RecordSize@12, RecordCount@16, Flags@20; u64 PayloadOffset@24, PayloadBytes@32; u32 SampleRate@40; u8 DefaultCodec@44, DefaultChannels@45; u16 EndianMarker@46; u32 BankHash@48, BlockSamples@52, CreatedUnixSeconds@56, Reserved0@60. Final size 64, exact cache-line multiple.</h8vb_header>
    <h8vb_record size="32" alignment="8">u32 HashID@0, ByteLength@4; u64 ByteOffset@8; u32 TotalSamples@16, SampleRate@20; u8 Codec@24, Channels@25, Priority@26, RadioDistortionByte@27; u32 Flags@28. Final size 32, 8-byte lanes aligned.</h8vb_record>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>Not a gameplay runtime. GlobalQualityWeight is not consumed because this CI validator must not alter DTO layout, save identity, or authority route. H8DM uses continuous validation cost scaling by CLI mode: default deterministic sampling for massive sections, `--thorough` for nightly/full proof. H8VB is tiny enough for full ABI validation.</SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>No Unity NativeArray, NativeList, NativeHashMap, or GlobalDataVault ownership. OS mmap only; lifecycle is Python context-manager open/close.</H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_DEPENDENCY_GRAPH>No Burst jobs, no JobHandle, no Complete. No NativeArray aliasing surface exists in this Python CI tool.</POINTER_ALIASING_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>No sibling Unity assembly reference added. No C# runtime compile invoked; no dotnet or Unity build launched.</COMPILE_GUARD>
  <DEAR_LIE_CONFIRMATION>Heavy runtime/audio/physics playback is avoided. The validator proves static bytes with O(records + ADPCM blocks) for H8VB and sampled O(0.05n) payload checks for massive H8DM sections instead of O(n) default full scans.</DEAR_LIE_CONFIRMATION>
</SELF_AUDIT>
