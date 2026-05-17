# Documentation Concurrent Delta Ledger
Date: 2026-05-17
Status: STATIC_DOC_DELTA_LEDGER / PENDING OWNER RECONCILIATION
Agent: DOC_GLOBAL_DOCS_REFRESH
Evidence Class: STATIC_DOC / GIT_CLI

## Purpose
The user requested continued documentation actualization after the pushed global refresh. This second-pass ledger records the current documentation delta visible in the working tree without claiming ownership of concurrent agents' uncommitted work.

## Current Delta Summary
- Documentation candidates in git diff plus untracked files: `71`.
- Tracked changed documentation candidates: `54`.
- Untracked documentation candidates: `17`.
- Dirty source/shader files outside documentation: `8`.
- Stable active .md / .txt docs scanned for metadata headers: `150`.
- Stable active .md / .txt header failures: `0`.
- Stable .json docs intentionally excluded from Markdown header injection: `16`.

## Boundary
- This ledger updates project documentation by making the live concurrent delta explicit and searchable.
- It does not stage or commit another agent's uncommitted content.
- It does not rewrite archive evidence, dated reports, active AgentLogs, active Tasks, or JSON schema/config files.
- It does not claim Unity compile, Play Mode, profiler, GC, Frame Debugger, or player build proof.

## Dirty Source Files Blocking Stronger Doc Truth
- `Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute`
- `Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute`
- `Assets/_Project/Scripts/Core/CameraJuiceSignals.cs`
- `Assets/_Project/Scripts/Core/IPlatformIntegration.cs`
- `Assets/_Project/Scripts/HectonBoidController.cs`
- `Assets/_Project/Scripts/HectonFluidEngine.cs`
- `Assets/_Project/Scripts/VFX/Debris/CarveDebrisComputeRenderer.cs`
- `Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs`

## Documentation Delta By Class

### active_agent_evidence (`7`)
- `??` `Docs/AgentLogs/LOG_SUBNAUTICA_RESEARCHER.md`
- `??` `Docs/AgentLogs/LOG_VOLUMETRIC_SILT_ADVECTION.md`
- `??` `Docs/AgentLogs/Rationale_SUBNAUTICA_RESEARCHER.md`
- `??` `Docs/AgentLogs/Rationale_VOLUMETRIC_SILT_ADVECTION.md`
- `??` `Docs/Tasks/CURRENT_BATCH.md`
- `??` `Docs/Tasks/Status_SUBNAUTICA_RESEARCHER.md`
- `??` `Docs/Tasks/Status_VOLUMETRIC_SILT_ADVECTION.md`

### archive_or_deprecated_evidence (`4`)
- `??` `Docs/Archive/Batch007/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`
- `M` `Docs/Archive/Batch007/AgentLogs/LOG_SUBNAUTICA_RESEARCHER.md`
- `M` `Docs/Archive/Batch007/AgentLogs/Rationale_SUBNAUTICA_RESEARCHER.md`
- `M` `Docs/Archive/Batch007/Tasks/Status_SUBNAUTICA_RESEARCHER.md`

### dated_report_or_generated_manifest (`11`)
- `??` `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_CURRENT_20260517_1539.json`
- `??` `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_LIVE_REBASE_20260517_1539.md`
- `??` `Docs/Reports/2026-05-17_ACTIVE_DOCUMENTATION_ACTUALITY_MANIFEST.json`
- `??` `Docs/Reports/2026-05-17_DOCUMENTATION_ACTUALITY_SUBNAUTICA_RESEARCHER.md`
- `??` `Docs/Reports/SUBNAUTICA_PUBLIC_MOD_ECOSYSTEM_DEEPDIVE.md`
- `M` `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_AUDIT_INDEX.md`
- `M` `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_HPHI_SEARCH_INDEX_20260517.md`
- `M` `Docs/Reports/2026-05-16_COMPUTE_AUDIT/COMPUTE_TOKEN_BURN_RATE_LEDGER.md`
- `M` `Docs/Reports/COMPUTE_DOMINANCE_REPORT.md`
- `M` `Docs/Reports/README.md`
- `M` `Docs/Reports/SUBNAUTICA_2_UE5_REFERENCE_DOSSIER.md`

### root_doc_drift (`1`)
- `M` `COMPUTE_AUDIT_BRIEF.md`

### stable_docs_index_or_other (`3`)
- `M` `Docs/DOC_GOVERNANCE.md`
- `M` `Docs/README.md`
- `M` `Docs/ROOT_DOCS_REFERENCE.md`

### stable_or_domain_doc (`45`)
- `??` `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- `??` `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`
- `??` `Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`
- `??` `Docs/ARCHITECTURE/SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`
- `M` `Docs/AI_Fauna/AI_CREATURE_ROSTER_ENTERPRISE.md`
- `M` `Docs/AI_Fauna/AI_FAUNA_WORLD_INTEGRATION_REPORT.md`
- `M` `Docs/AI_Fauna/README.md`
- `M` `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
- `M` `Docs/ARCHITECTURE/AUP_PRECISION_STANDARDS.md`
- `M` `Docs/ARCHITECTURE/CONTENT_SAVE_SLOT_TOPOLOGY.md`
- `M` `Docs/ARCHITECTURE/COOP_MERKLE_STATE_DELTA_PROTOCOL.md`
- `M` `Docs/ARCHITECTURE/DRONE_FLEET_PROTOCOL.md`
- `M` `Docs/ARCHITECTURE/EQUIPMENT_SOA_LAYOUT.md`
- `M` `Docs/ARCHITECTURE/FLOW_FIELD_MATH.md`
- `M` `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md`
- `M` `Docs/ARCHITECTURE/HEADLESS_ECOSYSTEM_SIMULATION.md`
- `M` `Docs/ARCHITECTURE/KINEMATICS_AUP_INTEGRATION.md`
- `M` `Docs/ARCHITECTURE/KINETIC_ENTANGLEMENT.md`
- `M` `Docs/ARCHITECTURE/MIGRATORY_FLORA_SYSTEM.md`
- `M` `Docs/ARCHITECTURE/ORGANIC_ENTROPY_MATH.md`
- `M` `Docs/ARCHITECTURE/PROJECT_CONTENT_LEDGER.md`
- `M` `Docs/ARCHITECTURE/QUEST_DAG_PROTOCOL.md`
- `M` `Docs/ARCHITECTURE/REACTIVE_ECONOMY_SYSTEM.md`
- `M` `Docs/ARCHITECTURE/README.md`
- `M` `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`
- `M` `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md`
- `M` `Docs/ARCHITECTURE/SCANNER_DATA_MINING.md`
- `M` `Docs/ARCHITECTURE/SEISMIC_GEOLOGY_SYSTEM.md`
- `M` `Docs/ARCHITECTURE/SUBMARINE_OS_MANUAL.md`
- `M` `Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`
- `M` `Docs/ARCHITECTURE/SUBNAUTICA2_TO_HECTON8_TACTICAL_BACKLOG.md`
- `M` `Docs/ARCHITECTURE/THIRD_PARTY_POISON.md`
- `M` `Docs/ARCHITECTURE/TRAUMA_GLITCH_SYSTEM.md`
- `M` `Docs/ARCHITECTURE/URP_SCREENSHOT_PIPELINE.md`
- `M` `Docs/ARCHITECTURE/ZERO_GC_FABRICATION.md`
- `M` `Docs/Design/HECTON8_DREAM_VS_SUBNAUTICA2_COUNTERPOSITION.md`
- `M` `Docs/Flora_Pipeline/AI_FLORA_EXECUTION_BRIEF.md`
- `M` `Docs/Flora_Pipeline/FLORA_SYSTEM_PLAN.md`
- `M` `Docs/Flora_Pipeline/README.md`
- `M` `Docs/Legacy_Backlog/README.md`
- `M` `Docs/Legacy_World_Reference/README.md`
- `M` `Docs/Scatter_Runtime/SCATTER_DOTS_NARROW_SCOPE_SPEC.md`
- `M` `Docs/Scatter_Runtime/SCATTER_PHASE1_BASELINE_CHECKLIST.md`
- `M` `Docs/Scatter_Runtime/SCATTER_REFACTOR_EXECUTION_PLAN.md`
- `M` `Docs/Scatter_Runtime/SCATTER_REFACTORING_MANIFESTO_V2.md`

## Header Gate
- Stable active `.md` / `.txt` metadata gate remains clean: `0` failures.
- JSON files are schema/config artifacts. Adding `Date:` / `Status:` text would corrupt valid JSON, so they remain excluded from the Markdown header rule.

## Required Owner Actions
- Owners of `stable_or_domain_doc` changes must either commit them, request DOC_GLOBAL_DOCS_REFRESH review, or move obsolete material to the archive path used by Batch007.
- Owners of `dated_report_or_generated_manifest` changes must keep reports immutable once committed; new facts require a new dated report, not mutation of old evidence unless the report explicitly tracks a living index.
- Owners of `active_agent_evidence` and `archive_or_deprecated_evidence` changes must preserve chronology and avoid merging active logs into stable authority pages.
- `root_doc_drift` must be moved under `Docs/Reports/` or declared explicitly as a temporary root exception before the next release lock.

## Verification Commands
- `git status --short --branch`
- `git diff --name-only`
- `git ls-files --others --exclude-standard`
- Stable `.md` / `.txt` header scan in PowerShell

## Result
Second pass does not close the global documentation task as fully clean, because concurrent writers currently own uncommitted documentation and source deltas. It closes the audit gap by giving the project a current, committed reconciliation ledger.
