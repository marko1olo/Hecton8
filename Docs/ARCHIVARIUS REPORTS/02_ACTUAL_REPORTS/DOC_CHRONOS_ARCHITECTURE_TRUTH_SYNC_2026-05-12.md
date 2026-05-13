# DOC_CHRONOS Architecture Truth Sync

Date: 2026-05-12
Status: HISTORICAL STATIC AUDIT / SUPERSEDED COUNTERS / RUNTIME PENDING
Auditor: DOC_CHRONOS

## 2026-05-13 Supersession

Read `../../Reports/2026-05-13_DOC_AUDIT_XRAY.md` before using numeric counters in this report.

Current static corrections:

| Scan | 2026-05-13 value |
|---|---:|
| first-party C# files under `Assets/_Project` | 1,411 |
| first-party C# physical lines under `Assets/_Project` | 866,103 |
| first-party non-test C# files | 1,401 |
| first-party non-test C# physical lines | 863,364 |
| interface declaration hits | 204 |
| first-party asmdefs under `Assets/_Project` | 24 |

The original May 12 table below is retained as historical evidence only. It is not current source truth.

## Scope

Allowed write scope:

- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit`
- `Docs/ARCHITECTURE`
- `Docs/ARCHIVARIUS REPORTS`
- `Docs/SYSTEMS_CONTRACTS.md`

Forbidden write scope honored:

- no edits to other agents' `Docs/AgentLogs`
- no edits to other agents' `Docs/Tasks`

## Original 2026-05-12 Source Recon - Superseded

| Scan | Result |
|---|---:|
| first-party C# files under `Assets/_Project` | 1,383 |
| first-party C# physical lines under `Assets/_Project` | 824,799 |
| first-party non-test C# files | 1,373 |
| first-party non-test C# physical lines | 822,060 |
| interface declaration hits | 189 |
| `GlobalSignals` public signal structs | 33 |
| `GlobalSignals` `ParallelWriter` lanes | 13 |
| `[StructLayout(... Size = 32/64/128)]` hits | 127 |
| authoritative domain ids found | 85 |
| first-party asmdefs under `Assets/_Project` | 13 |

## EventBus Truth

The current source truth is `GlobalSignals.cs`, not the old prose-only five-artery event bus.

`GlobalSignals` owns 33 typed `NativeQueue<T>` lanes. Payloads are fixed 32/64-byte structs. Thirteen lanes expose `NativeQueue<T>.ParallelWriter` for MPSC producer jobs. `SpscSignalRingBuffer<T>` exists for SPSC-only cases.

## Aligned Struct Hot List

Critical aligned records found by static source scan:

| Source | Records |
|---|---|
| `Core/GlobalSignals.cs` | 33 signal structs, 32/64 bytes |
| `Core/GlobalTelemetryBus.cs` | `TelemetryEvent`, 64 bytes |
| `Core/DodReplayRecorder.cs` | replay headers/segments/sidecars, 32/64/128 bytes |
| `Core/HectonArenaAllocator.cs` | 64-byte allocator alignment contract |
| `Data/Monolith/H8DataMonolithTypes.cs` | 16/32/64-byte static DB records |
| `Atmosphere/BaseAtmosphereMath.cs` | `CompartmentState`, 32 bytes |
| `Gameplay/Combat/CombatDamageRuntime.cs` | damage packets, 32/64 bytes |
| `World/*` | residency, scatter, fauna, vegetation, world registry records |
| `SaveBinaryStorage.cs` | save storage structs including 32/128-byte records |

## Compilation Debt

`rg -n '\[BLOCKED BY DEPENDENCY\]' Docs/AgentLogs` found:

| File | Line | Claim |
|---|---:|---|
| `Docs/AgentLogs/LOG_CORE_SCAVENGING_CRAFTING.md` | 44 | task 15 blocked because project build was not clean outside domain |
| `Docs/AgentLogs/Rationale_CORE_CHUNK_STREAMING.md` | 49 | compile blocked by external missing symbols, final blocker `SurvivalPhysiologyScalarResult` |
| `Docs/AgentLogs/Rationale_UI_DIEGETIC_HUD.md` | 118 | task 15 marked blocked after third build attempt |
| `Docs/AgentLogs/Rationale_WEATHER_THERMODYNAMICS.md` | 99 | build blocked by unrelated missing core/native symbols |

This report lists the debt but does not edit those logs.

## Silo Violation Candidates

Static regex scan candidates. These require owner review before code changes.

| Corridor | Files |
|---|---|
| UI -> Voxel | `UI/AcousticRadarSphereRenderer.cs`, `UI/PDAMapTab.cs`, `UI/SubmarineSonarHoloMapRenderer.cs` |
| UI -> Save | `UI/PauseMenuController.cs`, `UI/SaveSlotHoverPreview.cs`, `UI/SaveSlotThumbnail.cs` |
| UI -> Fauna/Ecosystem | `UI/AcousticEcholocationTranslator.cs`, `UI/FakeRadarBlipController.cs`, `UI/PDAIntrusionManager.cs`, `UI/SuitHUDV4CanvasOverlay.cs`, `UI/SubnauticaSystemsDebugUI.cs` |
| Core -> UI package surface | `Core/RuntimeWatchdog.cs`, `Core/SceneRuntimeService.cs` |

Rule for cleanup:

```text
domain owner -> GlobalRegistry interface or GlobalSignals packet -> presentation consumes snapshot
```

No UI file should own voxel, fauna, save, or ecosystem state.

## Non-ASCII Recon

Scan scope:

- `Assets/_Project`
- `Docs/ARCHITECTURE`
- `Docs/ARCHIVARIUS REPORTS`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/QUALITY_GATES.md`

Results:

| Metric | Value |
|---|---:|
| files scanned | 1,737 |
| content non-ASCII files | 578 |
| content Cyrillic files | 0 |
| path non-ASCII files | 1 |
| path Cyrillic files | 1 |
| target doc text files scanned | 164 |
| target doc text files with non-ASCII content | 81 |

Path requiring rename:

- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/[CYRILLIC_DIALOG_REVIEWER].txt`

Target docs with non-ASCII content:

- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/01_EXECUTIVE_FORENSIC_SUMMARY.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/02_SYSTEM_REALITY_MATRIX.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/03_CODE_HEALTH_AND_RUNTIME_ARCHITECTURE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/04_PLAYER_PERSPECTIVE_AND_READINESS.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/07_REVERIFICATION_ADDENDUM_2026-04-30.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/11_OWNER_DEEP_DIVE_WORLD_GAMEPLAY_UI.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/12_SERVICE_AND_EVENT_DRIFT_DEEP_DIVE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/14_DEPENDENCY_GRAVITY_AND_MONOLITH_RISK.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/17_SENSORY_SIMULATION_AND_PLAYER_CHANNELS.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/18_INTERACTION_CONSTRUCTION_AND_RUNTIME_LINKAGE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/19_BOOTSTRAP_OPTIMIZATION_AND_RUNTIME_GOVERNANCE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/22_SCENE_PREFAB_AND_SOURCE_OF_TRUTH_DISCIPLINE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/23_RENDERING_VISUAL_STACK_AND_GPU_IDENTITY.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/26_LEGACY_DOCSET_ACTUALITY_AND_UPDATE_QUEUE.md`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/DIALOG_REVYuER.txt`
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/[CYRILLIC_DIALOG_REVIEWER].txt`
- `Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md`
- `Docs/ARCHITECTURE/HABITAT_LOGISTICS_GRAPH.md`
- `Docs/ARCHITECTURE/QUEST_DAG_PROTOCOL.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/ASSET_DEPENDENCY_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/AUP_SURGERY_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/BUILD_DEPENDENCY_GRAPH.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/GAMEPLAY_SYSTEM_OWNERSHIP_LEDGER.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/GLOSSARY.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/HUD_EDITOR_SPEC.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/NARRATIVE_DISCOVERY_PROGRESSION_SYSTEM_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/SURVIVAL_DAMAGE_HAZARD_SYSTEM_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/SYSTEM_INTERCONNECT_MATRIX.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/WORLD_ENVIRONMENT_SUBMARINE_SYSTEM_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_ASSET_DEPENDENCY_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_CYRILLIC_SWEEP.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_DATA_DICTIONARY.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_DEAD_ASSET_SWEEP.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_DEEP_FORENSIC_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_ETA2_SUPREME_SUMMARY.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_LIAR_DETECTION.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_MEMORY_ALIGNMENT_FIX.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_SUPREME_AUDITOR_REPORT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_VRAM_BUDGET_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-28_VRAM_EXECUTION_LIST.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-30_EDITOR_RUNTIME_FORENSICS.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-30_PERSISTENCE_AND_SCENE_SEARCH_DRIFT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/2026-04-30_SAVE_PARTICIPANT_LEDGER.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/AGENTS_SKILLS_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/AUDIO_ROUTING_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/COMPUTE_BUFFER_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/DEBUG_LOG_DELETION_QUEUE.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/FRAME_TIMELINE.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/GOD_OBJECT_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/HARD_LINK_DEBT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/ITEM_ASSET_GUIDS.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/RENDERGRAPH_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/SCENE_VRAM_FOOTPRINT.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/SINGLETON_FIX_PRIORITY.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/SINGLETON_VIOLATIONS.md`
- `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/VRAM_BUDGET_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/AGENT_03_PHYSICS_LOG.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/AGENT_05_UI_AUDIO_LOG.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/AST_AWARE_SMART_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/AUP_DRIFT_WARNINGS.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/DOUBLE_BUFFER_COMPLIANCE.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/EVENT_FLOW_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/GOD_OBJECT_AUDIT.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/HALL_OF_SHAME_2026-04-28.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/MATH_API_WARNINGS.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/MEMORY_LEAK_WARNINGS.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/NAMING_VIOLATIONS.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/OTChET_ARHIVARIUSA.txt`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/PERFORMANCE_WARNINGS.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/SCENE_OBJECT_HYGIENE.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/SHADER_VARIANT_BLOAT.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/STATIC_AUDIT_MASTER_SUMMARY.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/STRING_LITERAL_CRIMES.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/SUPREME_AUDITOR_FINAL_REPORT.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/SYSTEM_COUPLING_WARNINGS.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/TECH_DEBT_REGISTRY.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/THIRD_PARTY_POISON.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/TRANSFORM_ACCESS_CRIMES.md`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/INVENTORY_AUDIT/VERIFICATION_REPORT_2026-04-28.md`

## Three Lies Corrected

| Old lie | Code truth |
|---|---|
| "MMF-backed save paging is current." | `SaveBinaryStorage` uses FileStream plus cached native read windows; no source `MemoryMappedFile` usage was found. |
| "Event bus is five arteries." | `GlobalSignals.cs` defines 33 typed NativeQueue lanes and 13 `ParallelWriter` producers. |
| "Architecture matrix is 80 domains." | `Docs/Actual Domains of Project.txt` contains domain ids 1 through 85. |

## Omega Reverification

Internal source pass:

| Discrepancy | Fix |
|---|---|
| Save docs treated FileStream as universal after the MMF purge. | `DATA_MONOLITH_H8BIN_SPEC.md` and the forensic summary now state that `H8StaticDataArena` still uses boot-only `File.ReadAllBytes` before native blit. |
| Stable map carried a May 11 green Core build as current truth. | `HECTON8_GLOBAL_ARCHITECTURE_MAP.md` now marks that result historical and records the May 12 DOC_CHRONOS gate: `111` C# errors, `3` warnings, `[BLOCKED BY DEPENDENCY]`. |
| The new non-ASCII report copied a Cyrillic filename into active ASCII documentation. | The path is now reported through the ASCII placeholder `[CYRILLIC_DIALOG_REVIEWER].txt`. |

External mandate pass:

| Mandate | Result |
|---|---|
| AGENTS.md: no runtime readiness from docs/static scans/local build. | All verification language is split into documentation audit versus runtime `PENDING VERIFICATION`. |
| AGENTS.md: stable authority files win over dated reports. | Build-gate and architecture rules were promoted into `QUALITY_GATES.md`, `SYSTEMS_CONTRACTS.md`, and `Docs/ARCHITECTURE/*.md`. |
| AGENTS.md: performance savings buy visible quality. | `SCALABILITY_MATRIX.md` defines Low/Middle/High/Ultra math and shader paths instead of a flat optimization target. |

## Verification Boundary

Documentation audit status is now `PENDING VERIFICATION` because the May 13 DOC_AUDIT pass superseded source counters and found the cited May 11 build artifacts absent from the current filesystem.

Runtime proof remains `PENDING VERIFICATION` until a fresh Unity import, Console readback, Play Mode boot, profiler/GC capture, and build artifact are present.

STATUS: PENDING VERIFICATION
