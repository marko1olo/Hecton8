# 2026-05-17 Documentation Global Refresh
Date: 2026-05-17
Status: PENDING VERIFICATION
Evidence class: STATIC_DOC / STATIC_SOURCE / FILESYSTEM / PACKAGE_LOCK / GIT_CLI

## Scope

User request: study and update the full documentation set until documents are actualized.

This pass is documentation-only. It does not run Unity Editor, Play Mode, profiler, GCMonitor, Frame Debugger, Memory Profiler, player build, save/load route, scene wiring, or visual validation.

## Authority Rule Applied

Stable authority docs are editable current project brain. Dated reports, archives, deprecated folders, active agent logs, third-party notices, and old prompt bundles are evidence/provenance. They are classified and indexed, not rewritten as if they were current policy.

## Current Corpus Inventory

Static text inventory from `rg --files -g '*.md' -g '*.txt'`, excluding `Library`, `Temp`, `Logs`, `obj`, and `igra`:

| Class | Count |
|---|---:|
| active agent docs (`Docs/AgentLogs`, `Docs/Tasks`) | 10 |
| archive (`Docs/Archive`, `Docs/_Archive`) | 2079 |
| code-adjacent first-party docs (`Assets/_Project`, `Tools`, `Data`) | 60 |
| dated reports | 257 |
| deprecated / obsolete | 123 |
| root or other | 13 |
| stable docs | 149 |
| third-party or asset-local docs | 97 |
| total | 2788 |

Tracked stable `Docs` files excluding reports, archives, deprecated folders, active agent files, and dated forensic bundles now have header coverage:

| Header state | Count |
|---|---:|
| `Date:` + `Status:` present | 144 |

## Current Source Orientation

Static source counts were rerun because older May 13 and May 15 counters are stale under concurrent agent churn:

| Scan | Current value |
|---|---:|
| `Assets/_Project/**/*.cs` | 1635 |
| `Assets/_Project/Scripts/**/*.cs` | 1585 |
| `Assets/_Project/**/*.cs` physical lines | 915721 |
| `Assets/_Project/Scripts/**/*.cs` physical lines | 900303 |
| `Assets/_Project/**/*.asmdef` | 95 |
| `GlobalRegistryContracts.cs` direct public interfaces | 63 |
| interface declaration hits under `Assets/_Project` | 248 |
| `.agents-skills/*.txt` mandates | 78 |

These are static counts only. They are not compile, runtime, profiler, GC, or player-build proof.

## Package And Scene Orientation

`ProjectSettings/ProjectVersion.txt` still pins Unity `6000.4.1f1`.

`ProjectSettings/EditorBuildSettings.asset` still lists the normative scene chain:

- `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
- `Assets/_Project/Scenes/01_MAIN_MENU.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`

`Packages/manifest.json` current package pins include:

- URP `17.4.0`
- Addressables `2.7.6`
- Input System `1.19.0`
- AI Navigation `2.0.11`
- Memory Profiler `1.1.12`
- Unity MCP package from `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#beta`

Forbidden UPM IDs were not observed in `Packages/manifest.json`, but physical legacy/vendor contamination remains in the asset tree:

| Path | Files |
|---|---:|
| `Assets/AstarPathfindingProject` | 605 |
| `Assets/Plugins/Easy Save 3` | 422 |
| `Assets/Plugins/Demigiant` | 357 |
| `Assets/Plugins/DarkTonic` | 347 |
| `Assets/Eazy Sound Manager` | 5 |
| `Assets/Resources` | 9 |
| `Assets/Plugins` total | 1405 |

## Root Documentation Reality

Current root text scan sees four root markdown files:

- `AGENTS.md`
- `BUILD_PLAYTEST_ISSUES.md`
- `COMPUTE_AUDIT_BRIEF.md`
- `MASTER_RELEASE_WORK_PLAN.md`

`COMPUTE_AUDIT_BRIEF.md` is current root drift against the May 15 governance claim that root contained only three markdown anchors. It was already modified by a concurrent worker, so this pass does not move or stage it.

## Stable Docs Updated

This pass added missing `Date:` and/or `Status:` headers to tracked, clean, stable documentation files only:

- `Docs/Actual Domains of Project.txt`
- `Docs/ARCHITECT_HANDBOOK.md`
- `Docs/ARCHITECTURE/CONTENT_SAVE_SLOT_TOPOLOGY.md`
- `Docs/ARCHITECTURE/SUBNAUTICA2_HECTON8_IMPLEMENTATION_HANDOFF.md`
- `Docs/ARCHIVARIUS REPORTS/GLOBAL_TECH_DEBT.md`
- `Docs/DEPENDENCY_GRAPH.md`
- `Docs/Design/Acoustic_Binary_Specs.md`
- `Docs/Design/Atmosphere_Scattering_LUT.md`
- `Docs/Design/Biolum_Implementation_Guide.md`
- `Docs/Design/H8DB_Index_RLE_Spec.md`
- `Docs/Design/HardwareAdaptiveUIScaler.md`
- `Docs/Design/HardwareAdaptiveUIScaler_Runbook.md`
- `Docs/Design/Lore_Bible.md`
- `Docs/Design/LUT_Shader_Mapping.md`
- `Docs/Design/Missions/Outpost_Failure_Modes.md`
- `Docs/Design/Snell_Refraction_LUT_Shader_Mapping.md`
- `Docs/Legacy_Backlog/beklog.txt`
- `Docs/Legacy_Backlog/spetsifikatsii.txt`
- `Docs/Legacy_World_Reference/terrain_description.txt`
- `Docs/Lore/Archives/DeepReach_ColonyFailureArchive.md`
- `Docs/Lore/Lore_Bible.md`
- `Docs/Modding/API_Surface_Audit_Matrix.md`
- `Docs/Modding/Change_Control_Checklist.md`
- `Docs/Modding/Command_Audit_Matrix.md`
- `Docs/Modding/Event_Subscription_Audit_Matrix.md`
- `Docs/Modding/Loader_Save_Audit_Matrix.md`
- `Docs/Modding/Mod_API_Specification.md`
- `Docs/Modding/Payload_Layout_Audit_Matrix.md`
- `Docs/Modding/README.md`
- `Docs/Modding/Resource_Content_Audit_Matrix.md`
- `Docs/Modding/Runtime_Verification_Playbook.md`
- `Docs/Modding/Sample_InfiniteO2_Mod.md`
- `Docs/Modding/Signal_Audit_Matrix.md`
- `Docs/TECH_ART_PBR_SURFACE_DOCTRINE.md`

## Not Rewritten

These categories were intentionally not bulk-edited:

- `Docs/Archive/**` and `Docs/_Archive/**`: historical evidence.
- `Docs/DEPRECATED/**`, `Docs/Reports/DEPRECATED/**`, and `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/**`: deprecated evidence.
- `Docs/Reports/**`: dated snapshots; broad report header debt is recorded here instead of mutating every old report.
- `Docs/AgentLogs/**` and `Docs/Tasks/**`: live agent state under concurrent writes.
- third-party package docs and license files: external provenance.
- dirty/untracked concurrent docs from other agents.

## Concurrent Work Boundary

At scan time, the worktree contained unrelated or concurrent modifications in source, reports, Batch007 archive files, and untracked architecture/report docs. This pass avoids staging those files unless they are created or edited by `DOC_GLOBAL_DOCS_REFRESH`.

Known concurrent doc surfaces include:

- dirty `Docs/README.md`
- dirty `Docs/ARCHITECTURE/README.md`
- dirty `Docs/ARCHITECTURE/SAVE_PAGING_PROTOCOL.md`
- dirty `Docs/ARCHITECTURE/SAVE_V8_BINARY_SPEC.md`
- dirty `Docs/Reports/2026-05-16_COMPUTE_AUDIT/*`
- untracked `Docs/ARCHITECTURE/HECTON8_P0_FOUNDATION_PROOF_MATRIX.md`
- untracked `Docs/ARCHITECTURE/SUBNAUTICA2_EA_TO_HECTON8_PRODUCTION_CONTRACTS.md`
- untracked `Docs/ARCHITECTURE/SUBNAUTICA2_PLAYER_LOOP_TO_HECTON8_GAP_MATRIX.md`
- untracked `Docs/Reports/SUBNAUTICA_PUBLIC_MOD_ECOSYSTEM_DEEPDIVE.md`

## Verification Boundary

Claim: tracked stable active docs now have `Date:` and `Status:` headers.

Evidence class: STATIC_DOC.

Command: tracked stable-doc header scan excluding reports, archives, deprecated folders, active agent logs/tasks, and dated forensic bundles.

Residual risk: concurrent agents can create or modify docs after this scan.

Claim: project source/doc counters in this report reflect the current filesystem at scan time.

Evidence class: STATIC_SOURCE / STATIC_DOC / FILESYSTEM.

Command: `rg --files`, PowerShell `Get-Content | Measure-Object -Line`, package/buildsettings reads.

Residual risk: static counts can drift immediately in a multi-agent workspace and do not prove runtime readiness.

## Required Next Proof

To convert this from documentation actuality to runtime truth, run a separate Unity evidence pass:

- Unity import/Console readback.
- Play Mode boot through `00_BOOTSTRAP -> 01_MAIN_MENU -> 02_HECTON_WORLD`.
- GCMonitor and profiler capture.
- Memory/VRAM capture on low tier.
- player build or batchmode build artifact.

Until those exist, all runtime, visual, memory, and `0 B/frame` claims remain `PENDING VERIFICATION`.
