# DOCS_ACTUALIZATION Log

## 2026-05-26 Active Docs And Report Triage Pass

What was wrong:
- Active agents are writing heavily into `Docs/Reports`, `Docs/AgentLogs`, and `Docs/Tasks`.
- `Docs/Reports` snapshot at 2026-05-26 20:49 local contained `1166` top-level files / `419,826,648` bytes.
- Current stable entry docs did not give a concise live read order for the 1315-1337 batch.
- Older green CLI compile references were still easy to misread as current global readiness despite newer active source edits.

What was done:
- Added `Docs/ACTIVE_WORKSPACE_INDEX.md`.
- Updated `Docs/README.md` to point to the active workspace/report wave.
- Updated `Docs/Reports/README.md` with live triage rules and the current clutter boundary.
- Updated `Docs/ROOT_DOCS_REFERENCE.md` to separate root human docs from generated `.csproj`, `.slnx`, and CSV files.
- Updated `Docs/DOC_GOVERNANCE.md` with live report rules.
- Updated `Docs/ARCHITECTURE/README.md` with the active workspace/evidence boundary.
- Updated `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` with the 2026-05-26 active agent report index entry.
- Created `Docs/Tasks/Status_DOCS_ACTUALIZATION.md` and `Docs/AgentLogs/Rationale_DOCS_ACTUALIZATION.md`.

Cinematic Cheats used:
- None. Documentation-only pass.

Exact Microseconds saved:
- Runtime: `0 us`.
- Engineering search savings are not measured. The practical effect is a shorter read path: `ACTIVE_WORKSPACE_INDEX.md` -> `Status_[ID].md` -> `LOG_[ID].md` -> cited report artifact.

Verification:
- `git diff --check` on touched docs returned exit code `0`; only LF/CRLF normalization warnings were emitted.
- `Select-String` confirmed `ACTIVE_WORKSPACE_INDEX.md` is linked from `Docs/README.md`, `Docs/Reports/README.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- No `dotnet build`, Unity editor command, or runtime validation was run.

Residual risk:
- Active agents can change status/report state immediately after this snapshot.
- No bulk archive was performed. This is intentional until active owners stop writing.
- Global compile/runtime readiness remains `PENDING VERIFICATION`.

## 2026-05-26 Correction: Distill, Do Not Index Transient Folders

What was wrong:
- The first follow-up added transient folder indexes for `Docs/Tasks` and `Docs/AgentLogs`.
- That preserved process-folder clutter instead of distilling durable project truth.

What was done:
- Removed active `Docs/ACTIVE_WORKSPACE_INDEX.md`.
- Removed active `Docs/Tasks/README.md`.
- Removed active `Docs/AgentLogs/README.md`.
- Added `Docs/PROJECT_BASELINE.md`.
- Added `Docs/DEPRECATED/DOCS_ACTUALIZATION_TRANSIENT_INDEXES_2026-05-26.md` to record the rejected direction.
- Rewired `Docs/README.md`, `Docs/Reports/README.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, and `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md` to the distillate.

Cinematic Cheats used:
- None. Documentation-only correction.

Exact Microseconds saved:
- Runtime: `0 us`.
- Engineering search savings are not measured.

Verification:
- Stale-link grep found no active references to `ACTIVE_WORKSPACE_INDEX`, `Docs/Tasks/README`, or `Docs/AgentLogs/README`.
- `git diff --check` on touched docs returned exit code `0`; only LF/CRLF normalization warnings were emitted.
- No compile or Unity command was run.

## 2026-05-26 Root Noise Deprecation And Distillation

What was wrong:
- Active `Docs/` root still contained local token telemetry, old FAQ prose, glossary prose, and a binary marketing archive.
- Main README/report/architecture surfaces still enumerated old report and concision scan chains instead of current facts.
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` still pointed at the moved token ledger as current telemetry.

What was done:
- Moved `TOKEN_USAGE_LEDGER.md`, `TECHNICAL_FAQ.md`, `H8_GLOSSARY.md`, `Marketing.rar`, and `TOKEN_USAGE_AUDIT_2026-05-26.*` to `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/`.
- Added `Docs/DEPRECATED/Root_Docs_Noise_2026-05-26/MANIFEST.md`.
- Updated `Docs/DEPRECATED/README.md` and `Docs/_Archive/README.md` for the new archive boundary.
- Rewrote `Docs/Reports/README.md` to category-level evidence rules instead of long artifact lists.
- Compressed `Docs/README.md` report/concision sections and kept `Docs/PROJECT_BASELINE.md` as the stable summary.
- Updated `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md` to stop treating moved files as active authority.

Cinematic Cheats used:
- None. Documentation-only cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Engineering search/read savings are not measured.

Verification:
- Root `Docs/` no longer contains the moved root-noise files or `ACTIVE_WORKSPACE_INDEX.md`.
- `Docs/Reports` no longer contains `TOKEN_USAGE_AUDIT_2026-05-26.*`.
- `Docs/Tasks/README.md` and `Docs/AgentLogs/README.md` are absent.
- Active core docs no longer reference the moved token/FAQ/glossary/marketing files as active authority.
- No compile or Unity command was run.

## 2026-05-26 Correction: Baseline In Root, Not Current Notes

What was wrong:
- `Docs/CURRENT_ENGINEERING_DISTILLATE.md` used current-work framing in the active `Docs` root.
- Stable entry docs still mentioned active batch/process framing.

What was done:
- Replaced `Docs/CURRENT_ENGINEERING_DISTILLATE.md` with `Docs/PROJECT_BASELINE.md`.
- Rewrote the baseline as authority, proof boundary, native-memory baseline, stable system facts, and documentation baseline.
- Updated `Docs/README.md`, `Docs/Reports/README.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, and deprecated indexes to point at the baseline.

Cinematic Cheats used:
- None. Documentation-only correction.

Exact Microseconds saved:
- Runtime: `0 us`.

Verification:
- Active core docs no longer link `Docs/CURRENT_ENGINEERING_DISTILLATE.md`.
- Root baseline file is `Docs/PROJECT_BASELINE.md`.
- No compile or Unity command was run.

## 2026-05-26 Legacy Domain Bundle Deprecation

What was wrong:
- `Docs/AI_Fauna`, `Docs/Flora_Pipeline`, and `Docs/Scatter_Runtime` were old planning bundles.
- They carried stale May report-chain orientation instead of base contracts.

What was done:
- Moved the three folders to `Docs/DEPRECATED/Legacy_Domain_Bundles_2026-05-26/`.
- Added `Docs/DEPRECATED/Legacy_Domain_Bundles_2026-05-26/MANIFEST.md`.
- Updated `Docs/DEPRECATED/README.md` and `Docs/PROJECT_BASELINE.md`.
- Relinked `Assets/_Project/Prefabs/Nature/Flora/Baked/README.md` away from the deprecated flora bundle.

Cinematic Cheats used:
- None. Documentation-only cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.

Verification:
- Follow-up scan found no remaining `Assets` or `Tools` reference to the moved folders.
- No compile or Unity command was run.

## 2026-05-26 Root README Compression

What was wrong:
- `Docs/README.md` still embedded historical X_012/APEX pass history.
- The root index was carrying change-log material instead of base navigation.

What was done:
- Removed historical X_012/APEX sections from `Docs/README.md`.
- Kept authority order, source reality, contract map, archive map, and verification language.

Cinematic Cheats used:
- None. Documentation-only cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.

Verification:
- `Docs/README.md` is now `85` lines / `678` words in the measured snapshot.
- No compile or Unity command was run.

## 2026-05-26 Base Docs Distillation

What was wrong:
- Root/base docs duplicated current build/native-proof state.
- Runtime master plan, global architecture map, quality gates, and systems contracts mixed durable contracts with dated correction history.
- Atlas/handbook/procedural/tech-art generated-reference docs still carried DOC_GLOBAL Rxx banner residue.

What was done:
- Root/base docs now define stable authority, boundaries, read order, verification language, runtime doctrine, and contracts.
- Current native-memory and compile proof snapshots were moved into `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`.
- Oversized base contracts were rewritten as concise contract documents.
- Repeated DOC_GLOBAL Rxx banners were removed from active generated/reference docs.
- `Docs/DEPENDENCY_GRAPH.md` no longer treats `Docs/Tasks/CURRENT_BATCH.md` as source authority.

Cinematic Cheats used:
- None. Documentation-only cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Search/read-path savings are operational only.

Verification:
- Active-doc grep excluding `Docs/Reports`, `Docs/AgentLogs`, `Docs/Tasks`, `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` returned no hits for removed current-note/index/R-chain patterns.
- `git diff --check -- Docs Assets/_Project/Prefabs/Nature/Flora/Baked/README.md ':!Docs/Tasks/CURRENT_BATCH.md'` passed; output contains line-ending warnings only.

## 2026-05-26 Root Contract Reduction And Active Folder Cleanup

What was wrong:
- Root still carried full generated/reference bodies and domain doctrine.
- Active Design, Modding, Marketing, Lore, and route-card docs still carried DOC_GLOBAL/Rxx correction boilerplate.
- Root-heavy tech-art/procedural-world doctrine belonged in architecture, not in the base read path.

What was done:
- Archived full snapshots to `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/`.
- Replaced root `ARCHITECT_HANDBOOK.md` and `DEPENDENCY_GRAPH.md` with short tool-entry contracts.
- Reduced root `PROJECT_ATLAS.md` to required validator tokens and the 85-domain table.
- Moved tech-art PBR and procedural vertical-world doctrine to `Docs/ARCHITECTURE`.
- Replaced 64 active DOC_GLOBAL guard blocks with a short authority boundary.
- Normalized route-card R48/R50 labels to stable route-contract language.
- Updated `Tools/BuildArchitectureAtlas.py` so regeneration does not reintroduce DOC_GLOBAL/R51 boilerplate.
- Updated `Tools/test_architecture_atlas.py` to validate the root dependency graph as a stub.

Cinematic Cheats used:
- None. Documentation-only cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Search/read-path savings are operational only.

Verification:
- Atlas static scan reports `85` domain rows and required tokens: `DataSovereignty`, `SignalBus`, `Data Monolith`, `Crafting Fast-Fail Validator`, `observedAssemblyCount = 83`, `observedDomainIndexCount = 85`.
- Active-doc grep excluding `Docs/Reports`, `Docs/AgentLogs`, `Docs/Tasks`, `Docs/DEPRECATED`, `Docs/_Archive`, and `Docs/Archive` returned no DOC_GLOBAL/R-chain/current-note/old-path residue after cleanup.
- Generator/test grep for `Tools/BuildArchitectureAtlas.py` and `Tools/test_architecture_atlas.py` returned no stale DOC_GLOBAL/R-chain generation strings.
- `git diff --check -- Docs Assets/_Project/Prefabs/Nature/Flora/Baked/README.md Tools/BuildArchitectureAtlas.py Tools/test_architecture_atlas.py Tools/CodexTokenUsageAudit_20260525.py ':!Docs/Tasks/CURRENT_BATCH.md'` exited `0`; output contains line-ending warnings only.
- No compile, Unity import, or Play Mode command was run.

## 2026-05-26 Root Data Artifact Eviction

What was wrong:
- `Docs` root still contained multi-megabyte generated graph JSON/cache files.
- `Docs` root also contained CSV authoring profiles for water, lighting, and flora.
- Those files are generated artifacts or data inputs, not base documentation.

What was done:
- Moved `DEPENDENCY_GRAPH.json` and `DEPENDENCY_GRAPH.cache.json` to `Docs/Generated`.
- Added `Docs/Generated/README.md` and a generated graph placeholder at `Docs/Generated/DEPENDENCY_GRAPH.md`.
- Moved profile CSV files to `Docs/Data/Profiles`.
- Updated `Tools/BuildArchitectureAtlas.py`, `Tools/AtlasCheck.py`, `Tools/HectonPhiStaticAudit.py`, and `Tools/test_architecture_atlas.py`.
- Updated `Tools/OOP_Doc_Scanner.py` so stale-boundary detection does not pollute active residue scans.
- Updated source/editor path strings for flora sway, water optics, ambient lighting, and water extinction profile loading.
- Updated architecture docs and root indexes to point at the new folders.

Cinematic Cheats used:
- None. Documentation/data path cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Search/read-path savings are operational only.

Verification:
- Active grep found no remaining old root references to `Docs/DEPENDENCY_GRAPH.json`, `Docs/DEPENDENCY_GRAPH.cache.json`, moved profile CSV paths, DOC_GLOBAL/R-chain residue, or current-note paths.
- Root `Docs` file list contains stable docs/domain map only; generated graph artifacts are under `Docs/Generated`, CSV profiles are under `Docs/Data/Profiles`.
- No compile, Unity import, or Play Mode command was run.

## 2026-05-26 Generator Guard And CSV Schema Boundary

What was wrong:
- H-Phi static audit could overwrite root `Docs/PROJECT_ATLAS.md` with generated report content.
- `AtlasCheck` could return green on the placeholder generated graph.
- Stale generated JSON/cache snapshots were active under `Docs/Generated`.
- Profile CSVs lacked schema version/hash metadata.

What was done:
- Redirected H-Phi generated atlas output to `Docs/Generated/PROJECT_ATLAS_HPHI.md`.
- Added an `AtlasCheck` placeholder guard that fails on `Status: PLACEHOLDER / REGENERATE BEFORE USE`.
- Moved stale `DEPENDENCY_GRAPH.json` and `DEPENDENCY_GRAPH.cache.json` into the deprecated root snapshot archive.
- Added schema version/hash/owner metadata to four profile CSVs and removed BOMs so existing comment-skip parsers ignore metadata safely.
- Updated `Docs/Generated/README.md`, `Docs/DEPENDENCY_GRAPH.md`, and the deprecated snapshot manifest.

Cinematic Cheats used:
- None. Documentation/tool/data boundary cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Avoided false-positive tooling and schema drift; no frame-time claim.

Verification:
- `python -m py_compile Tools/AtlasCheck.py Tools/BuildArchitectureAtlas.py Tools/HectonPhiStaticAudit.py Tools/test_architecture_atlas.py Tools/OOP_Doc_Scanner.py` passed.
- `python Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md` returns `ATLAS_CHECK_FAIL placeholder_atlas=...` as intended.
- CSV metadata checker reports `CSV_SCHEMA_METADATA_PASS files=4`.
- Active grep found no old root JSON/cache/CSV references outside tool output contracts.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Generated Atlas Authority Cleanup

What was wrong:
- `Tools/BuildArchitectureAtlas.py` still scanned active agent logs and current batch prompts.
- Generated atlas source authority still listed process files from `Docs/Tasks` and `Docs/AgentLogs`.
- Generated output could emit broken references for absent VRAM audit evidence and for explanatory process-folder text.
- The Chronicler domain description still implied root `PROJECT_ATLAS.md` was generated output.

What was done:
- Removed SHERST/PHI log scans from atlas data collection and JSON output.
- Replaced process-log sections with a code-only selected signal route snapshot.
- Rebased source authority on stable docs/contracts, generated outputs, and tool files.
- Updated `Docs/Actual Domains of Project.txt` and root `Docs/PROJECT_ATLAS.md` so Chronicler owns volatile generated artifacts under `Docs/Generated`, not root doctrine.
- Regenerated `Docs/Generated/DEPENDENCY_GRAPH.md`, `Docs/Generated/DEPENDENCY_GRAPH.json`, and `Docs/Generated/DEPENDENCY_GRAPH.cache.json`.
- Updated `Docs/Generated/README.md` with current AtlasCheck pass state.

Cinematic Cheats used:
- None. Documentation/tool-output authority cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Avoided broken generated-reference checks and stale process-log authority; no frame-time claim.

Verification:
- `python -m py_compile Tools/BuildArchitectureAtlas.py Tools/test_architecture_atlas.py Tools/AtlasCheck.py` passed.
- `python -m unittest Tools.test_architecture_atlas` passed: `10` tests.
- `python Tools/BuildArchitectureAtlas.py` wrote generated graph markdown/json/cache.
- `python Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Grep found no SHERST, PHI self-audit, current-batch, or VRAM scout log residue in generated graph/test outputs.
- `git diff --check -- Docs Tools/BuildArchitectureAtlas.py Tools/test_architecture_atlas.py ':!Docs/Tasks/CURRENT_BATCH.md'` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Authority Docs Evidence Distillation

What was wrong:
- `Docs/ARCHITECTURE/UNCLAIMED_FUTURE_SYSTEM_SEAMS.md` was an active architecture doc but still read like a dated ownership-trail snapshot.
- `Docs/Modding/Future_Command_Kernel_Reservations.md` exposed SHINOBU/process slots as if they were API authority.
- `GLOBAL_AUTHORITY_BOUNDARIES.md` and `GLOBAL_AUTHORITY_OPERATING_MODEL.md` embedded old compile-loop evidence in stable authority docs.
- `DATA_MONOLITH_H8BIN_SPEC.md` used `VERIFIED` wording for static file/header evidence.

What was done:
- Rewrote the future seam architecture doc as a stable dormant-seam contract: purpose, authority boundary, reserved surfaces, activation rules, implemented contract-only code, authoring bridge, non-public labels, and proof limits.
- Updated the modding reservation contract to link to the stable seam contract, name future owner domains, and remove process-log/status change-control wording.
- Replaced global authority compile-loop diary text with durable route rules and evidence boundaries.
- Downgraded Data Monolith status to static file/header parse recorded with Unity runtime proof pending.

Cinematic Cheats used:
- None. Documentation authority cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- Prevented false authority/proof claims; no frame-time claim.

Verification:
- Targeted grep found no old compile-loop, process-owner, status/log, SHINOBU slot, `CURRENT_BATCH`, or static-verified-overclaim residue in the touched authority docs.
- `python Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- `git diff --check -- Docs/ARCHITECTURE/GLOBAL_AUTHORITY_BOUNDARIES.md Docs/ARCHITECTURE/GLOBAL_AUTHORITY_OPERATING_MODEL.md Docs/ARCHITECTURE/DATA_MONOLITH_H8BIN_SPEC.md Docs/ARCHITECTURE/UNCLAIMED_FUTURE_SYSTEM_SEAMS.md Docs/Modding/Future_Command_Kernel_Reservations.md` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Outpost Fail-Safe Static Contract Repair

What was wrong:
- `Docs/Design/Missions/Outpost_Failure_Modes.md` and `Outpost_FailSafe_Handoff.json` still treated mutable batch-prompt drift as source authority.
- `Tools/OutpostFailSafeValidate.py` and `Assets/_Project/Scripts/Editor/OutpostFailSafeHandoffValidator.cs` enforced the same stale process state.
- The Python validator also checked obsolete literal gas constants in `GasDynamicsSolver` instead of the current `HectonSurvivalContract` owner.

What was done:
- Replaced the Outpost source boundary with `STATIC_MISSION_CONTRACT`.
- Rebased the JSON handoff on the stable mission prose path and `OUTPOST_FAILSAFE_STATIC_CONTRACT`.
- Removed active batch reads from the Python and Editor validators.
- Updated gas validation to require `GasDynamicsSolver` to reference `HectonSurvivalContract` and require the numeric constants in `HectonSurvivalContract`.
- Updated the Babel dictionary manifest SHA-256 for the changed Outpost handoff JSON.

Cinematic Cheats used:
- None. Static design/tooling authority cleanup. The existing Ghost Power fallback remains a mission fake, not a physical generator.

Exact Microseconds saved:
- Runtime: `0 us`.
- Editor/static validation no longer depends on mutable process files; no frame-time claim.

Verification:
- `python -m py_compile Tools/OutpostFailSafeValidate.py` passed.
- `python Tools/OutpostFailSafeValidate.py` returned `OUTPOST_FAIL_SAFE_STATIC_VALIDATION PASS flags=32 topo=32 loc=15 fallbacks=3 sourceAuthority=STATIC_MISSION_CONTRACT`.
- Targeted grep found no old exact batch/agent drift tokens in the touched Outpost docs/tools.
- `python Tools/AtlasCheck.py --atlas Docs/Generated/DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- `git diff --check` on touched Outpost docs/tools and manifest exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Active Design Prompt Residue Cleanup

What was wrong:
- Active Design docs still mentioned current-batch prompt extraction as proof/source material.
- That preserved the process-folder-as-authority pattern after root docs had already been cleaned.

What was done:
- Rewrote `Docs/Design/HardwareAdaptiveUIScaler_Runbook.md` so generated prompt-provenance fields are audit metadata only, not source authority.
- Rewrote the final evidence bullet in `Docs/Design/Save_Binary_Header.md` to remove current-batch extraction as a contract claim.

Cinematic Cheats used:
- None. Documentation authority cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.

Verification:
- Active-doc grep excluding Reports/AgentLogs/Tasks/DEPRECATED/_Archive/Archive returned no `CURRENT_BATCH`, Outpost drift, or Mission Fail-Safe prompt residue.
- `git diff --check -- Docs/Design/HardwareAdaptiveUIScaler_Runbook.md Docs/Design/Save_Binary_Header.md` exited `0`; output contains line-ending warnings only.

## 2026-05-26 Active Process Artifact Deprecation

What was wrong:
- `Docs/AI_Texturing_Templates/README_SHINOBU_269.md` was a process-ID handoff, not a stable folder contract.
- `Docs/Marketing/AgentOps/VerificationBatches_2026-05-19/` was an old raw verification sprint area still referenced by active marketing docs.
- `Docs/ArtDrop/SHINOBU_361/` was a PNG output drop under active Docs with no active contract owner.
- `Docs/Design/Economy_Matrix_v1.md` still claimed runtime binding files were absent/pending under `Docs/Design`, while current files exist under `Data/Economy`.
- Active Modding/Marketing/Biolum docs still carried prompt/lane owner labels.

What was done:
- Moved the AI process README, marketing verification batch folder, and art-drop folder into `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/`.
- Added `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/MANIFEST.md`.
- Added stable `Docs/AI_Texturing_Templates/README.md` with the active template-output contract.
- Patched marketing workflow/readme/data docs so the old verification batch is archived reference only and future raw sprints require fresh dated files.
- Patched the economy matrix to reference `Data/Economy/Runtime_Binding_Review.json` and `Data/Economy/Runtime_Binding_Plan.json`.
- Replaced active `Owner prompt`/SHINOBU lane labels with domain owner labels where the documents remain active contracts.
- Left Modding audit matrices active because the static modding validator and schema still use them as contract evidence.

Cinematic Cheats used:
- None. Documentation deprecation and contract cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- `powershell -NoProfile -ExecutionPolicy Bypass -File Docs\Modding\Validate_Mod_API_Static.ps1` returned `Status PASS`.
- `python Tools\AtlasCheck.py --atlas Docs\Generated\DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Targeted active grep returned no `Owner prompt:`, `Owner lane: SHINOBU`, `README_SHINOBU_269`, active `Docs/ArtDrop`, old active verification-batch path, `PENDING GENERATION`, or stale `Docs/Design/Runtime_Binding` residue.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- Global `git diff --check -- Docs Tools Assets Data` still reports pre-existing trailing whitespace in `Docs/Tasks/CURRENT_BATCH.md`, outside this pass.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Architecture Process Note Deprecation

What was wrong:
- Active `Docs/ARCHITECTURE` still carried unindexed SHINOBU-labeled notes that were stubs, blocked static-source handoffs, historical notes, or duplicates beside stronger route cards.
- `Docs/Marketing/Operations/DAILY_AGENT_TASK_LOOP.md` still required a SHINOBU-specific rationale audit and expected the pre-archive marketing file count.

What was done:
- Moved 14 files to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Architecture/Unindexed_Process_Notes/`:
  - `SHINOBU_48_SEED_SHIP_ANOMALY.md`
  - `SHINOBU_69_VOLUMETRIC_PLASMA_BEAM.md`
  - `SHINOBU_61_APEX_COGNITION.md`
  - `SHINOBU_61_VOXEL_SURFACE_NETS.md`
  - `SHINOBU_115_STRUCTURAL_INTEGRITY_CALCULATOR.md`
  - `SHINOBU_218_DEPTH_BASED_INTEGRITY_SOLVER.md`
  - `PROCEDURAL_WRECKAGE_ASSEMBLER_SHINOBU_121.md`
  - `CACHE_CONSCIOUS_BTREE_MMF_SHINOBU_207.md`
  - `BASE_ATMOSPHERE_LOGISTICS_SHINOBU_221.md`
  - `SHINOBU_234_SURFACE_STORM_ABYSSAL_PROPAGATION.md`
  - `DEEP_SEA_NOIR_POST_PROCESSOR_SHINOBU_235.md`
  - `BILATERAL_DRS_UPSCALER_SHINOBU_236.md`
  - `Buoyancy_Sleep_State_SHINOBU_249.md`
  - `ScreenSpaceVisorWounds_SHINOBU_275.md`
- Added manifest rows for the moved architecture notes.
- Patched `SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md` to point to the archived implementation note.
- Replaced the marketing rationale audit target from `Rationale_SHINOBU_81.md` to optional `Rationale_MARKETING.md`.
- Updated expected Marketing file count from `100` to `90` after the archived verification-batch folder.

Cinematic Cheats used:
- None. Documentation deprecation and workflow cleanup.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- `python Tools\AtlasCheck.py --atlas Docs\Generated\DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Active grep found no old `Docs/ARCHITECTURE/<moved file>` references.
- Marketing file count: `90`.
- Marketing CSV parse: `CSV_PARSE_OK count=9`.
- CRM rows/status counts: `100`; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Asset metadata rows: `13`; required gate columns remain present.
- Marketing Backtick Path Audit returned `BACKTICK_PATH_AUDIT_OK`.
- Marketing rationale audit returned `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Superseded Route Card Deprecation

What was wrong:
- `Docs/ARCHITECTURE/SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD.md` was still active while its own status declared it historical and superseded by `SHINOBU_248`.
- The live replacement card referenced the prior route card only by name, without an archived path.

What was done:
- Moved `SHINOBU_156_ABYSSAL_CAVITATION_ROUTE_CARD.md` to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Architecture/Superseded_Route_Cards/`.
- Added the deprecation manifest row.
- Patched `Docs/ARCHITECTURE/SHINOBU_248_SHOCKWAVE_NAN_ROUTE_CARD.md` to point to the archived historical route card.

Cinematic Cheats used:
- None. Documentation deprecation only.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- Old active path no longer exists; archived path exists.
- Active grep for old active path finds no hits outside `Docs/DEPRECATED`.
- Active architecture grep for `Status: HISTORICAL`, `SUPERSEDED FOR LIVE`, and `retained as historical route context` returns no active hits.
- `python Tools\AtlasCheck.py --atlas Docs\Generated\DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, or platform proof was run.

## 2026-05-26 Parked Marketing Raw Sheet Deprecation

What was wrong:
- `Docs/Marketing/CreatorOutreach/PRIORITY_250_PITCH_SHEET_FROM_RAW.md` was a parked 128 KB raw public-index pitch sheet in the active CreatorOutreach folder.
- The file itself said `not outreach-ready` and instructed agents to keep it parked behind asset proof.
- Keeping it active made a raw bulk sheet look like an operating outreach surface.

What was done:
- Moved the sheet to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/PRIORITY_250_PITCH_SHEET_FROM_RAW.md`.
- Updated `Docs/Marketing/README.md` so the sheet is an archived raw planning reference, not active directory-map surface.
- Updated `Docs/Marketing/Data/SOURCE_LEDGER.md` and `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` references to the archived path.
- Updated `Docs/Marketing/Operations/DAILY_AGENT_TASK_LOOP.md` expected file count from `90` to `89`.
- Added deprecation manifest rows and replacement rules.

Cinematic Cheats used:
- None. Documentation deprecation only.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- Marketing file count returned `89`.
- Marketing CSV parse returned `CSV_PARSE_OK count=9`.
- CRM rows/status counts stayed `100`; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields stayed `0`.
- Asset metadata rows stayed `13`; required gate columns are present.
- Marketing corruption grep returned no hits.
- Marketing Backtick Path Audit returned `BACKTICK_PATH_AUDIT_OK`.
- Marketing rationale audit returned `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Active grep found no old `Docs/Marketing/CreatorOutreach/PRIORITY_250_PITCH_SHEET_FROM_RAW.md` or backticked active relative path references outside `Docs/DEPRECATED`.
- `python Tools\AtlasCheck.py --atlas Docs\Generated\DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, outreach, account, browser, public post, or platform proof was run.

## 2026-05-26 Raw Lead Seed Queue Deprecation

What was wrong:
- `Docs/Marketing/CreatorOutreach/RAW_LEAD_EXPANSION_QUEUE.md` was a parked public seed list in active CreatorOutreach.
- The file itself said `public seed list / not outreach-ready`.
- Active Marketing operations already say not to expand raw leads unless a human explicitly asks for another source-backed lead sprint.

What was done:
- Moved the file to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/CreatorOutreach/RAW_LEAD_EXPANSION_QUEUE.md`.
- Removed it from the active Marketing directory map and added an archived seed-queue note.
- Updated `Docs/Marketing/Data/SOURCE_LEDGER.md` and `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` to point at the archived path.
- Updated `Docs/Marketing/Operations/DAILY_AGENT_TASK_LOOP.md` expected Marketing file count from `89` to `88`.
- Updated the active deprecation manifest and documentation actuality ledger.

Cinematic Cheats used:
- None. Documentation deprecation only.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- Marketing file count returned `88`.
- Marketing CSV parse returned `CSV_PARSE_OK count=9`.
- CRM rows/status counts stayed `100`; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields stayed `0`.
- Asset metadata rows stayed `13`; required gate columns are present.
- Marketing corruption grep returned no hits.
- Marketing Backtick Path Audit returned `BACKTICK_PATH_AUDIT_OK`.
- Marketing rationale audit returned `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Active grep found no old `Docs/Marketing/CreatorOutreach/RAW_LEAD_EXPANSION_QUEUE.md` or backticked active relative path references outside `Docs/DEPRECATED`.
- `python Tools\AtlasCheck.py --atlas Docs\Generated\DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, outreach, account, browser, public post, or platform proof was run.

## 2026-05-26 Raw Prospecting List Deprecation

What was wrong:
- `Docs/Marketing/CreatorOutreach/ADJACENT_SURVIVAL_CREATOR_LEADS.md` and `Docs/Marketing/Regional/REGIONAL_CREATOR_LEADS.md` were active Marketing files.
- Both files said `raw public prospecting list / not outreach-ready`.
- Keeping them active made raw public seed volume look like current CRM or current verification work.

What was done:
- Moved both files to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/`.
- Removed both rows from the active Marketing directory map and added an archived raw-prospecting note.
- Updated source ledger and backlog references to archived paths.
- Rewrote P1 regional/adjacent creator tasks to require a fresh bounded sprint after asset-gap proof.
- Updated `Docs/Marketing/Operations/DAILY_AGENT_TASK_LOOP.md` expected Marketing file count from `88` to `86`.
- Updated the active deprecation manifest and documentation actuality ledger.

Cinematic Cheats used:
- None. Documentation deprecation only.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- Marketing file count returned `86`.
- Marketing CSV parse returned `CSV_PARSE_OK count=9`.
- CRM rows/status counts stayed `100`; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields stayed `0`.
- Asset metadata rows stayed `13`; required gate columns are present.
- Marketing corruption grep returned no hits.
- Marketing Backtick Path Audit returned `BACKTICK_PATH_AUDIT_OK`.
- Marketing rationale audit returned `RATIONALE_ORDER_AUDIT_NOT_APPLICABLE path_absent`.
- Active grep found no old active `ADJACENT_SURVIVAL_CREATOR_LEADS.md` or `REGIONAL_CREATOR_LEADS.md` path references outside archived paths.
- `python Tools\AtlasCheck.py --atlas Docs\Generated\DEPENDENCY_GRAPH.md` returned `ATLAS_CHECK_PASS references=5795`.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, outreach, account, browser, public post, or platform proof was run.

## 2026-05-26 Raw Scrape Summary Deprecation

What was wrong:
- `Docs/Marketing/Data/RAW_LEAD_SCRAPE_SUMMARY_2026-05-18.md` was a dated narrative scrape result in active `Marketing/Data`.
- Active `Data` should carry CSV schemas/data contracts and validation rules, not human-readable historical scrape summaries.
- `Docs/Marketing/AgentOps/scrape_letsplayindex_public_leads.ps1` would recreate a summary in active `Data` on a future forced raw-lead refresh.

What was done:
- Moved the summary to `Docs/DEPRECATED/Active_Doc_Deprecation_2026-05-26/Marketing/Data/RAW_LEAD_SCRAPE_SUMMARY_2026-05-18.md`.
- Updated `Docs/Marketing/README.md`, `Docs/Marketing/Data/RAW_PUBLIC_CREATOR_LEADS_README.md`, `Docs/Marketing/Data/SOURCE_LEDGER.md`, and `Docs/Marketing/Data/MARKETING_BACKLOG_INDEX.md` to treat the summary as archived evidence.
- Updated `Docs/Marketing/Operations/DAILY_AGENT_TASK_LOOP.md` expected active Marketing file count from `86` to `85`.
- Updated the active deprecation manifest and documentation actuality ledger.
- Updated `scrape_letsplayindex_public_leads.ps1` with `SummaryDir`, dated summary output names, no-force guard wording, and a summary boundary line.
- Updated `AgentOps/AGENT_MARKETING_WORKFLOWS.md` so future generated summaries stay out of active `Data`.

Cinematic Cheats used:
- None. Documentation/tool-boundary cleanup only.

Exact Microseconds saved:
- Runtime: `0 us`.
- No frame-time claim.

Verification:
- Marketing file count returned `85`.
- Marketing CSV parse returned `CSV_PARSE_OK count=9`.
- CRM rows/status counts stayed `100`; `DO_NOT_CONTACT=3; LOW_PRIORITY_VERIFY_LATER=52; NEEDS_ASSET=22; VERIFY_BEFORE_CONTACT=23`.
- Creator send-log fields stayed `0`.
- Asset metadata rows stayed `13`; required gate columns are present.
- Scraper PowerShell parse returned `PS_PARSE_OK`.
- Scraper no-force run returned `HOLD_RAW_LEAD_REFRESH` and did not touch network/output paths.
- Marketing corruption grep returned `CORRUPTION_GREP_NO_HITS`.
- Marketing Backtick Path Audit returned `BACKTICK_PATH_AUDIT_OK`.
- Old active summary path returned `OLD_ACTIVE_SUMMARY_PATH_ABSENT`.
- Active absolute old summary refs returned `ACTIVE_ABSOLUTE_OLD_SUMMARY_REFS_NO_HITS`.
- `python Tools/AtlasCheck.py` returned `ATLAS_CHECK_PASS references=5795`.
- Touched-path `git diff --check` exited `0`; output contains line-ending warnings only.
- No dotnet build, Unity import, Play Mode, profiler, GC, memory, render, player-build, save/load, outreach, account, browser, public post, or platform proof was run.
