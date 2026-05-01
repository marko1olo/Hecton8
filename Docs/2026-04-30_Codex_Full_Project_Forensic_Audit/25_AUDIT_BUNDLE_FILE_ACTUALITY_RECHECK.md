# 25 Audit Bundle File Actuality Recheck

Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`

Purpose:
- Recheck the current Codex forensic audit bundle after the later audit passes.
- Identify stale numeric snapshots.
- Mark which files are current, superseded, or still only code-review-level evidence.
- Keep all runtime/performance claims in `PENDING VERIFICATION` state until Unity play-mode/profiler evidence exists.

## 1. Bundle Index Integrity

Current bundle:

| Item | Count |
|---|---:|
| README files | 1 |
| numbered audit files | 28 |
| total markdown files in bundle | 29 |

README `Contents` integrity:

| Check | Result |
|---|---|
| listed numbered files | 28 |
| actual numbered files | 28 |
| missing from README | none |
| listed but absent on disk | none |

Status discipline:

| Rule | Current state |
|---|---|
| Every audit file must remain evidence-bound | satisfied at doc level |
| Every audit file must avoid solved/verified claims without logs | satisfied by `PENDING VERIFICATION` status |
| Performance readiness must not be declared from static code review | not declared |

## 2. Fresh Repository Counters

Snapshot date: 2026-05-01.

| Surface | Fresh count |
|---|---:|
| first-party C# under `Assets/_Project` | 1060 |
| C# under `Assets/_Project/Scripts` | 1020 |
| C# tests under `Assets/_Project/Tests` | 4 |
| non-meta files under `Assets/_Project/Data` | 1287 |
| prefabs under `Assets/_Project/Prefabs` | 378 |
| first-party `.shader` files | 65 |
| first-party `.compute` files | 22 |
| non-meta files under `Docs` after this addendum | 584 |

Fresh folder source-size checks:

| Folder | C# files | Lines |
|---|---:|---:|
| `Assets/_Project/Scripts/World` | 115 | 71,255 |
| `Assets/_Project/Scripts/UI` | 79 | 37,860 |
| `Assets/_Project/Scripts/Gameplay` | 106 | 35,225 |
| `Assets/_Project/Scripts/Core` | 35 | 10,639 |
| `Assets/_Project/Scripts/Construction` | 25 | 10,866 |
| `Assets/_Project/Scripts/Visor` | 19 | 8,464 |
| `Assets/_Project/Scripts/Audio` | 13 | 7,880 |
| `Assets/_Project/Scripts/Fauna` | 17 | 8,692 |
| `Assets/_Project/Scripts/Optimization` | 22 | 4,294 |
| `Assets/_Project/Scripts/VFX` | 5 | 2,132 |

## 3. Fresh Code-Truth Spot Checks

Confirmed current facts:

| Claim checked | Current result |
|---|---|
| Inventory save genetics path exists | `PlayerInventory.cs` writes `dto.itemGeneticsWords` |
| Save genetics backing field exists | `SaveData.cs` defines `public uint[] itemGeneticsWords;` |
| Resource node density ranges exist | `ResourceNodeTemplate.cs` defines `MinimumDensity` and `MaximumDensity` |
| Dedicated `Scripts/Physics` folder contains implementation files | true, folder contains current physics implementation files |
| Physics namespace implementation exists | true, root-level `PhysicsApplySystem.cs` uses `Hecton8.Physics` |
| Dedicated `Scripts/AI` folder exists | false |
| AI implementation exists elsewhere | true, root-level `HectonDirectorAI.cs`, `EncounterDirector.cs`, `EncounterProfile.cs`, `ThreatCostTable.cs` under `Hecton8.Systems.AI` |

Interpretation:
- Several systems exist but are not aligned to folder ownership implied by mandates/namespaces.
- Early audit wording that treats folder absence as total system absence must be read narrowly.
- Later files in this bundle supersede earlier raw counts where they conflict.

## 4. Per-File Actuality Table

| File | Actuality status | Notes |
|---|---|---|
| `01_EXECUTIVE_FORENSIC_SUMMARY.md` | current with supersession | High-level conclusion remains usable, but numeric/code snapshots are superseded by later files and this recheck. |
| `02_SYSTEM_REALITY_MATRIX.md` | current as model | System readiness percentages remain audit estimates, not measured runtime proof. |
| `03_CODE_HEALTH_AND_RUNTIME_ARCHITECTURE.md` | current with caution | Architecture risks remain relevant; compile/editor claims must be read through `07`. |
| `04_PLAYER_PERSPECTIVE_AND_READINESS.md` | current as qualitative assessment | Player-facing readiness remains non-metric and must not be used as a shipping proof. |
| `05_EVIDENCE_LEDGER.md` | refreshed | First-party C# count updated to 1060 and script count to 1020. Other grep evidence remains static-code evidence. |
| `06_CRITICAL_ACTION_QUEUE.md` | current with caution | Queue remains directionally valid; exact priorities require compile/runtime verification. |
| `07_REVERIFICATION_ADDENDUM_2026-04-30.md` | current as correction layer | Supersedes earlier compile-error assumptions. |
| `08_SUBSYSTEM_IMPLEMENTATION_CATALOG.md` | refreshed | World/Core/Visor line counts updated. |
| `09_MANDATE_PRESSURE_HEATMAP.md` | current as static pressure map | Still useful for where rules are stressed; not a profiler result. |
| `10_VERIFICATION_AND_DOCSET_REALITY.md` | current directionally | Doc trust model remains valid. Counts are superseded by `24` and `25`. |
| `11_OWNER_DEEP_DIVE_WORLD_GAMEPLAY_UI.md` | current directionally | Ownership critique remains valid from static evidence. |
| `12_SERVICE_AND_EVENT_DRIFT_DEEP_DIVE.md` | current directionally | Service/event drift critique remains valid from static evidence. |
| `13_PERSISTENCE_CONTENT_AND_ECOSYSTEM_DEEP_DIVE.md` | current directionally | Persistence/content critique remains valid; exact save behavior needs play-mode proof. |
| `14_DEPENDENCY_GRAVITY_AND_MONOLITH_RISK.md` | current directionally | Dependency-risk model remains valid from static evidence. |
| `15_SECONDARY_DOMAIN_DEEP_DIVE.md` | current directionally | Secondary subsystem scan remains usable. |
| `16_SECONDARY_DOMAIN_REALITY_AND_AUTHORITY_MATRIX.md` | current as estimate matrix | Percentages remain static audit estimates. |
| `17_SENSORY_SIMULATION_AND_PLAYER_CHANNELS.md` | current directionally | Sensory/player-channel critique remains valid. |
| `18_INTERACTION_CONSTRUCTION_AND_RUNTIME_LINKAGE.md` | current directionally | Runtime linkage risk remains valid. |
| `19_BOOTSTRAP_OPTIMIZATION_AND_RUNTIME_GOVERNANCE.md` | current directionally | Bootstrap/governance critique remains valid pending runtime logs. |
| `20_SUPPORT_LAYER_REALITY_AND_DOCTRINE_DRIFT.md` | current directionally | Support-layer doctrine drift remains valid. |
| `21_CONTENT_LOCALIZATION_AND_AUTHORED_SURFACE.md` | current | Content/localization scan remains current at audit level. |
| `22_SCENE_PREFAB_AND_SOURCE_OF_TRUTH_DISCIPLINE.md` | current | Scene/prefab source-of-truth critique remains current at audit level. |
| `23_RENDERING_VISUAL_STACK_AND_GPU_IDENTITY.md` | current | Rendering/GPU identity critique remains current pending Frame Debugger/profiler proof. |
| `24_VERIFICATION_DOC_TRUST_AND_EVIDENCE_MODEL.md` | refreshed | Docs count and active bundle count updated. |
| `25_AUDIT_BUNDLE_FILE_ACTUALITY_RECHECK.md` | refreshed snapshot | This file remains the bundle-file actuality recheck; `26` extends the scope to the wider legacy docset. |
| `26_LEGACY_DOCSET_ACTUALITY_AND_UPDATE_QUEUE.md` | refreshed snapshot | This file is the old-doc actuality and update-queue layer. |
| `27_PLAYMODE_DEADLOCK_STATIC_AUDIT.md` | current adjacent static audit | This file exists in the bundle and must be included in index integrity checks. |
| `28_STALE_ERROR_PURGE_AND_TRUST_SYNC.md` | current latest truth-sync pass | This file explicitly invalidates stale symbol-missing compile blockers and synchronizes docset trust counts. |

## 5. Files Updated In This Recheck

Updated across the latest bundle actuality passes:
- `README.md`
- `05_EVIDENCE_LEDGER.md`
- `08_SUBSYSTEM_IMPLEMENTATION_CATALOG.md`
- `24_VERIFICATION_DOC_TRUST_AND_EVIDENCE_MODEL.md`
- `25_AUDIT_BUNDLE_FILE_ACTUALITY_RECHECK.md`
- `26_LEGACY_DOCSET_ACTUALITY_AND_UPDATE_QUEUE.md`

Added across the latest bundle actuality passes:
- `25_AUDIT_BUNDLE_FILE_ACTUALITY_RECHECK.md`
- `26_LEGACY_DOCSET_ACTUALITY_AND_UPDATE_QUEUE.md`
- `27_PLAYMODE_DEADLOCK_STATIC_AUDIT.md`
- `28_STALE_ERROR_PURGE_AND_TRUST_SYNC.md`

Confirmed:
- README lists every numbered audit file.
- No numbered audit file is missing from disk.
- No listed numbered audit file is absent from disk.
- The bundle remains a static/code-review audit, not a Unity-verified runtime validation package.

## 6. Regression Model

CPU:
- No runtime code changed.
- CPU regression risk: none from this documentation update.

GC:
- No runtime code changed.
- GC regression risk: none from this documentation update.

Memory:
- No runtime code changed.
- Memory regression risk: none from this documentation update.

Cadence:
- Audit cadence improved because stale file counts are now isolated and superseded by this report.

Correctness:
- Main correctness risk is human interpretation: earlier files contain estimates and older snapshots.
- Mitigation: read later-numbered files as superseding earlier numeric snapshots.

## 7. Hard Conclusion

The active audit bundle is current as a documentation package after the `28` stale-error purge pass.

Not verified:
- Unity play-mode behavior.
- Real GC/frame.
- Real CPU frame time.
- Real GPU/VRAM pressure.
- Scene/prefab instance parity.
- Build output.

Rules for reading the bundle:
- `07_REVERIFICATION_ADDENDUM_2026-04-30.md` supersedes earlier compile-error assumptions.
- `24_VERIFICATION_DOC_TRUST_AND_EVIDENCE_MODEL.md` and this file supersede earlier doc-count snapshots.
- This file supersedes earlier first-party C# count and major folder line-count snapshots.
- All readiness percentages are audit estimates unless a future report attaches profiler/play-mode/build evidence.
