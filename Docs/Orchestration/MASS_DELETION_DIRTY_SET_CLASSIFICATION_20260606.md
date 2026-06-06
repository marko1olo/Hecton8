# Mass Deletion Dirty-Set Classification 2026-06-06

Status: STATIC_SOURCE classification only. No deletion acceptance. No commit readiness. No runtime, Unity, build, import, Play Mode, profiler, or player proof.

FIRST_20_NOT_APPLICABLE: read-only source-control deletion risk classification.

Authority used: AGENTS.md; Docs/AGENT_AUTHORITY_ROUTING.md; quality.md; testing.md; release.md; Docs/QUALITY_GATES.md; taskslocal/night_controller_20260605/NIGHT_OWNER_11_MASS_DELETION_DIRTY_SET_CLASSIFIER.txt; Docs/Orchestration/ORCHESTRATOR_NIGHT_20260605.md tail; .agents-skills/QA_Evidence_Text_Filter_Audit.txt; .agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt.

## Git Resample

Commands run:

- `git status --short`
- `git diff --name-status`
- `git diff --name-only --diff-filter=D`
- `git diff --cached --name-status`
- `git -c core.quotePath=false diff --name-only --diff-filter=D`
- `git -c core.quotePath=false status --short`
- `git -c core.quotePath=false diff --name-status`
- `git -c core.quotePath=false diff --cached --name-status`

Current counts from the `core.quotePath=false` resample:

| Metric | Count |
|---|---:|
| `git status --short` rows | 11438 |
| `git diff --name-status` rows | 11398 |
| tracked deletions | 11225 |
| tracked modifications | 173 |
| untracked rows | 40 |
| staged rows | 0 |

## Deletions By Top-Level Folder

| Top-level folder | Deleted tracked files |
|---|---:|
| Docs | 7929 |
| .codexbuild | 2490 |
| .codex-artifacts | 325 |
| igra | 242 |
| .codex-build | 90 |
| Assets | 76 |
| Tools | 65 |
| root | 6 |
| NativeAudio | 2 |

## Deletions By File Type

Top deleted extensions:

| Type | Count |
|---|---:|
| `.md` | 3427 |
| `.json` | 2417 |
| `.txt` | 1115 |
| `.cache` | 970 |
| `.dll` | 686 |
| `.png` | 683 |
| `.log` | 385 |
| `.meta` | 322 |
| `.editorconfig` | 304 |
| `.xml` | 166 |
| `.buildwithskipanalyzers` | 71 |
| `.cs` | 50 |
| `.csv` | 45 |
| `.props` | 40 |
| `.targets` | 40 |
| `.diff` | 33 |
| `.svg` | 32 |
| `.prefab` | 28 |
| `.bin` | 26 |
| `.sha256` | 24 |
| `.unity` | 17 |
| `.asset` | 11 |
| `.patch` | 11 |
| `.backup` | 11 |
| `.ps` | 10 |
| `.h8dump` | 8 |
| `.config` | 7 |
| no extension | 5 |
| `.exe` | 4 |
| `.py` | 4 |
| `.ps1` | 4 |
| `.jsonl` | 4 |
| `.locked_snapshot` | 4 |

Risk signal: this is not only cache deletion. It includes scenes, prefabs, assets, C# source, Python source, screenshots, reports, task files, and black-box dump artifacts.

## Production/Evidence/Cache Classes

| Class | Count | Risk |
|---|---:|---|
| docs-other | 6007 | HIGH until docs owner separates stale archives from live authority/provenance |
| generated-cache-proof-build-artifacts | 2905 | MEDIUM; likely generated, but proof/build artifacts still need owner approval |
| docs-evidence-provenance | 1922 | HIGH; reports, screenshots, task state, black-box dumps |
| player-build-output-legacy | 242 | MEDIUM; likely build output, still requires release/build owner decision |
| generated-tool-bin-obj | 63 | LOW/MEDIUM; likely tool build products, but do not use as blanket approval |
| assets-other | 45 | HIGH; Unity asset tree and third-party package metadata |
| production-assets-project | 31 | CRITICAL; first-party project assets/source/scenes |
| root-or-other | 6 | MEDIUM; root files need exact owner |
| third-party-nativeaudio | 2 | MEDIUM/HIGH; native audio owner required |
| source-tooling-or-tool-proof | 2 | HIGH; tracked source tool deletion |

## High-Risk Examples

`Assets/_Project` deleted count: 31. Exact examples:

- `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_BAKED_PREVIEW.asset`
- `Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_BAKED_PREVIEW.asset.meta`
- `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_HEAVY_BOOT_1428.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_HEAVY_BOOT_1428.unity.meta`
- `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_MONOBEHAVIOURS_1428.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_MONOBEHAVIOURS_1428.unity.meta`
- `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_TERRAIN_PROCEDURAL_1428.unity`
- `Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_TERRAIN_PROCEDURAL_1428.unity.meta`
- `Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity`
- `Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity.meta`
- `Assets/_Project/Scenes/GeminiSandbox.unity`
- `Assets/_Project/Scenes/GeminiSandbox.unity.meta`
- `Assets/_Project/Scenes/XXX_SANDBOX.unity`
- `Assets/_Project/Scenes/XXX_SANDBOX.unity.meta`
- `Assets/_Project/Scenes/XX_SANDBOX_MASUM.unity`
- `Assets/_Project/Scenes/XX_SANDBOX_MASUM.unity.meta`
- `Assets/_Project/Scenes/X_GPUSANDBOX.unity`
- `Assets/_Project/Scenes/X_GPUSANDBOX.unity.meta`
- `Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs`
- `Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs.meta`
- `Assets/_Project/Scripts/Editor/LegacyStubs/BrushSettings.cs`
- `Assets/_Project/Scripts/Editor/LegacyStubs/ColorPalette.cs`
- `Assets/_Project/Scripts/Editor/LegacyStubs/PrefabPalette.cs`
- `Assets/_Project/Scripts/Editor/LegacyStubs/Readme.cs`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs`

`Tools` source deletion count outside `bin/obj`: 2.

- `Tools/workspace_hygiene_1331.py`
- `Tools/workspace_hygiene_apex_reaudit_1331.py`

`Docs/Reports` deleted count: 1686. Examples:

- `Docs/Reports/..h8bin_validator_ast_cache_SHINOBU_358.json.tmp.11652`
- `Docs/Reports/.h8bin_validator_ast_cache_SHINOBU_358.json`
- `Docs/Reports/AGENT_PROMPT_1318_EXTRACTED.xml`
- `Docs/Reports/AGENT_PROMPT_1318_REEXTRACTED_APEX.xml`
- `Docs/Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.json`
- `Docs/Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.json.sha256`
- `Docs/Reports/APEX_FINAL_VERIFICATION_UNKNOWN_AUP_LOCK_PIN_PASS_20260528.md`
- `Docs/Reports/APEX_HOTPATH_AUDIT_1319_RERUN2_RAW.json`

`Docs/Screenshots` deleted count: 216. Examples:

- `Docs/Screenshots/H8_mainmenu_probe.png`
- `Docs/Screenshots/H8_mainmenu_probe.png.meta`
- `Docs/Screenshots/codex_playmode_smoke_1428.png`
- `Docs/Screenshots/codex_playmode_smoke_1428.png.meta`
- `Docs/Screenshots/gemini_account_switch_probe_01_before.png`
- `Docs/Screenshots/gemini_account_switch_probe_02_menu.png`
- `Docs/Screenshots/gemini_account_switch_probe_03_after_click.png`
- `Docs/Screenshots/gemini_gui_probe_20260604_01.png`

`Docs/AgentLogs` deleted count: 13.

- `Docs/AgentLogs/Dump_1309_TerminalDecryption.bin`
- `Docs/AgentLogs/Dump_1309_TerminalOS.bin`
- `Docs/AgentLogs/Dump_1309_TerminalOSMirror.h8dump`
- `Docs/AgentLogs/Dump_1309_TerminalProjection.bin`
- `Docs/AgentLogs/Dump_CELESTIAL_SURGEON.bin`
- `Docs/AgentLogs/Dump_CORE_DATA_VAULT_WARDEN.txt`
- `Docs/AgentLogs/Dump_GLOBAL_TELEMETRY_BUS.bin`
- `Docs/AgentLogs/Dump_SEISMIC_DIRECTOR.bin`
- `Docs/AgentLogs/Dump_SENTINEL_DISPOSAL_GUARD.bin`
- `Docs/AgentLogs/Dump_SENTINEL_DISPOSAL_GUARD.h8dump`
- `Docs/AgentLogs/Dump_SHINOBU_345.bin`
- `Docs/AgentLogs/Dump_SHINOBU_346.bin`
- `Docs/AgentLogs/логиуц.txt`

`Docs/Tasks` deleted count: 7.

- `Docs/Tasks/CURRENT_BATCH.md`
- `Docs/Tasks/ExtractedPrompt_1712.tmp.xml`
- `Docs/Tasks/ExtractedPrompt_1724.tmp.xml`
- `Docs/Tasks/ExtractedPrompt_1729.tmp.xml`
- `Docs/Tasks/ExtractedPrompt_1734.tmp.xml`
- `Docs/Tasks/ExtractedPrompt_1737.tmp.xml`
- `Docs/Tasks/POLISH.txt`

## Asset Meta Pairing

Deleted `Assets` files:

| Signal | Count |
|---|---:|
| total deleted under `Assets` | 76 |
| deleted non-meta assets | 18 |
| deleted `.meta` files | 58 |
| non-meta deleted assets with matching deleted `.meta` | 18 |
| non-meta deleted assets missing matching deleted `.meta` | 0 |
| deleted `.meta` with matching deleted base asset | 18 |
| deleted `.meta` whose base file still exists on disk | 0 |
| deleted non-meta asset whose `.meta` still exists on disk | 0 |

Pairing signal: no sampled orphan pattern was found for deleted `Assets` paths. This does not make the deletion safe. Production `Assets/_Project` scene, asset, and source deletion remains blocked pending owner decision.

`Assets` non-`_Project` examples are mostly third-party/package metadata, including AmplifyImpostors, Bakery, Feel/NiceVibrations, GPUInstancer, MapMagic native plugin metadata, and `Assets/_Recovery.meta`. These still require the relevant third-party/import owner before acceptance.

## Owner Decision Matrix

| Class | Count | Risk | Required owner | Allowed action | Blocked action |
|---|---:|---|---|---|---|
| `Assets/_Project` production assets/scenes/source | 31 | CRITICAL | route owner for world/scenes/source plus integrator | Restore or block pending per-file owner disposition | Accept deletion, commit, or call harmless cleanup |
| `Assets` non-`_Project` metadata/package files | 45 | HIGH | asset/import and third-party integration owner | Block pending package/import owner review | Accept deletion because files are mostly `.meta` |
| `Tools` source files outside `bin/obj` | 2 | HIGH | tooling owner | Restore or block pending tooling owner disposition | Delete as cache |
| `Tools` `bin/obj` generated files | 63 | LOW/MEDIUM | tooling owner | Keep deletion only after tool owner confirms generated build products | Use this to approve unrelated Tools source deletion |
| `Docs/Reports` | 1686 | HIGH | evidence/docs owner and route owner for reports with proof value | Archive or restore after provenance review | Delete proof/history without owner |
| `Docs/Screenshots` | 216 | HIGH | visual/proof owner | Archive or restore after capture relevance review | Delete screenshots used as proof/rejection evidence |
| `Docs/AgentLogs` | 13 | HIGH | telemetry/black-box owner | Restore or archive with manifest after owner review | Delete crash dumps or black-box evidence |
| `Docs/Tasks` | 7 | HIGH | orchestration/task owner | Restore or archive after controller decision | Delete active task/protocol files as cleanup |
| `Docs/Archive` | 4749 | MEDIUM/HIGH | docs provenance owner | Archive elsewhere only with manifest/provenance | Treat archive volume as proof deletion is safe |
| `Docs/DEPRECATED` | 676 | MEDIUM/HIGH | docs governance owner | Keep deletion only after no-loss route/provenance review | Delete deprecated rule/proof history silently |
| `.codexbuild`, `.codex-build`, `.codex-artifacts` | 2905 | MEDIUM | build/proof artifact owner | Keep deletion only after proof-artifact owner approves generated cleanup | Use generated-cache cleanup to approve Docs/Assets/Tools deletions |
| `igra` player build output | 242 | MEDIUM | release/build owner | Keep deletion only after release/build owner confirms obsolete build output | Delete if referenced by current proof/release packet |
| `NativeAudio` | 2 | MEDIUM/HIGH | audio/native integration owner | Block pending owner review | Delete native audio files without owner |
| root files | 6 | MEDIUM | integrator/docs owner depending path | Review exact paths | Blanket cleanup |

## Decision

Deletion acceptance is blocked. Commit readiness is blocked. Runtime/build proof is absent by task constraint and was not attempted.

Owner requirement: every high-risk class above needs a responsible owner decision before integration, checkpoint, or cleanup language. `.codexbuild` and `Tools/bin/obj` likely generated cleanup does not lower risk for `Assets/_Project`, source tooling, reports, screenshots, agent logs, or task files.

Low/Middle/High/Ultra consequences: deletion safety is lane-independent. No hardware tier changes source-control truth, evidence provenance, `.meta` pairing risk, or owner responsibility.

## Peirce / Controller Resample - 2026-06-06

Status: read-only resample. No deletion, restore, archive move, staging, commit, Unity, build, import, Play Mode, profiler, scene, prefab, material, or raw YAML action was performed.

Current counts:

| Metric | Count |
|---|---:|
| `git status --short` rows | 11458 |
| tracked deletions | 11233 |
| tracked modifications | 179 |
| untracked rows | 46 |
| staged rows | 0 |
| `Assets` deletions | 84 |
| `Assets/_Project` deletions | 38 |
| deleted `.meta` files | 327 |
| deleted `.unity` files | 18 |
| deleted `.cs` files | 50 |
| deleted `.asset` files | 11 |
| `Docs/Reports` deletions | 1686 |
| `Docs/Screenshots` deletions | 216 |
| `Docs/AgentLogs` deletions | 13 |
| `Docs/Tasks` deletions | 7 |

Current `Assets/_Project` deletion disposition remains `RESTORE / BLOCK UNTIL OWNER`. Exact current paths:

```text
Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_OldAmberPaint.mat
Assets/_Project/Art/Materials/RuntimeVisualProof/MAT_RuntimeVisualProof_OldAmberPaint.mat.meta
Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_BAKED_PREVIEW.asset
Assets/_Project/Data/World/Sandbox/HECTON_SANDBOX_BIOMES_BAKED_PREVIEW.asset.meta
Assets/_Project/Diagnostics/auto_baseline_test.raw
Assets/_Project/Diagnostics/auto_baseline_test.raw.meta
Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_HEAVY_BOOT_1428.unity
Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_HEAVY_BOOT_1428.unity.meta
Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_MONOBEHAVIOURS_1428.unity
Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_MONOBEHAVIOURS_1428.unity.meta
Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_TERRAIN_PROCEDURAL_1428.unity
Assets/_Project/Scenes/02_HECTON_WORLD_BISECT_NO_TERRAIN_PROCEDURAL_1428.unity.meta
Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity
Assets/_Project/Scenes/03_HECTON_SANDBOX_BIOMES.unity.meta
Assets/_Project/Scenes/GeminiSandbox.unity
Assets/_Project/Scenes/GeminiSandbox.unity.meta
Assets/_Project/Scenes/XXX_SANDBOX.unity
Assets/_Project/Scenes/XXX_SANDBOX.unity.meta
Assets/_Project/Scenes/XX_SANDBOX_MASUM.unity
Assets/_Project/Scenes/XX_SANDBOX_MASUM.unity.meta
Assets/_Project/Scenes/X_GPUSANDBOX.unity
Assets/_Project/Scenes/X_GPUSANDBOX.unity.meta
Assets/_Project/Scenes/_Temp.meta
Assets/_Project/Scenes/_Temp/FloraBeautyAudit_TMP.unity
Assets/_Project/Scenes/_Temp/FloraBeautyAudit_TMP.unity.meta
Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs
Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs.meta
Assets/_Project/Scripts/Editor/LegacyStubs.meta
Assets/_Project/Scripts/Editor/LegacyStubs/BrushSettings.cs
Assets/_Project/Scripts/Editor/LegacyStubs/BrushSettings.cs.meta
Assets/_Project/Scripts/Editor/LegacyStubs/ColorPalette.cs
Assets/_Project/Scripts/Editor/LegacyStubs/ColorPalette.cs.meta
Assets/_Project/Scripts/Editor/LegacyStubs/PrefabPalette.cs
Assets/_Project/Scripts/Editor/LegacyStubs/PrefabPalette.cs.meta
Assets/_Project/Scripts/Editor/LegacyStubs/Readme.cs
Assets/_Project/Scripts/Editor/LegacyStubs/Readme.cs.meta
Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs
Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs.meta
```

Current source-tooling deletion disposition remains `RESTORE / BLOCK UNTIL TOOLING OWNER`:

```text
Tools/workspace_hygiene_1331.py
Tools/workspace_hygiene_apex_reaudit_1331.py
```

`Docs/Tasks/POLISH.txt` remains `RESTORE OR FORMALLY REPLACE VIA DOC GOVERNANCE`. Root `AGENTS.md` still routes polish work to this file.

Pairing result: no current orphan `.meta` survivor pattern was found for deleted `.cs`, `.shader`, `.asset`, or deleted `Assets` non-meta files. This is only hygiene. It does not make production asset, source-tooling, report, screenshot, black-box dump, or task-file deletion safe.

Updated decision: deletion acceptance remains blocked. Any cleanup/commit/integration claim remains rejected until responsible owners explicitly restore, archive with manifest, or approve each high-risk class.

## Static Gate Implementation - 2026-06-06

Banach implemented an enforceable static gate for this front.

Files added/changed:

- `Tools/ValidateMassDeletionDirtySet.py`
- `Tools/test_validate_mass_deletion_dirty_set.py`
- `Tools/RunAssetStaticValidators.py`
- `Tools/test_run_asset_static_validators.py`
- `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.csv`
- `Docs/AssetAudit/ASSET_FRONT_FILE_MAP_20260605.md`
- `Docs/Reports/AssetSystem_20260605/ASSET_STATIC_VALIDATION_SUMMARY_20260605.md`

Fresh validation:

- `python -B -m unittest Tools.test_validate_mass_deletion_dirty_set Tools.test_run_asset_static_validators Tools.test_validate_asset_front_file_map` ran `16` tests OK.
- `python -B Tools\ValidateAssetFrontFileMap.py` returned `ASSET_FRONT_FILE_MAP_OK rows=183 csv_rows=64`.
- `python -B Tools\ValidateMassDeletionDirtySet.py --no-fail` returned `MASS_DELETION_DIRTY_SET_REJECTED blockers=11 high_risk_deletions=true owner_disposition=false`.
- `python -B Tools\RunAssetStaticValidators.py` returned `ASSET_STATIC_VALIDATORS_OK count=24` while preserving the mass-deletion rejection output.

Latest live rejection summary:

- status rows: `11468`;
- tracked deletions: `11233`;
- tracked modifications: `184`;
- untracked rows: `51`;
- staged rows: `0`;
- `Assets=84`;
- `Assets/_Project=38`;
- `Tools` source deletions outside `bin/obj=2`;
- `Docs/Reports=1686`;
- `Docs/Screenshots=216`;
- `Docs/AgentLogs=13`;
- `Docs/Tasks=7`;
- `Docs/Tasks/POLISH.txt` deleted;
- deleted extensions: `.meta=327`, `.cs=50`, `.shader=0`, `.asset=11`, `.unity=18`;
- pairing: `missing_meta=0`, still `PAIRING_CLEAN_BUT_NOT_DELETION_APPROVAL`.

Gate decision: the validator is working. The dirty set is still rejected. Runner success is not cleanup approval; it only proves the static guard surfaces the blocker without masking the rest of the asset-front static checks.

## Cicero Sidecar Resample - 2026-06-06

Status: read-only sidecar audit. No deletion, restore, move, archive, staging, commit, Unity, import, build, Play Mode, profiler, test, scene, prefab, material, or raw YAML action was performed.

Fresh validator result:

```text
MASS_DELETION_DIRTY_SET_REJECTED blockers=11 high_risk_deletions=true owner_disposition=false
status-rows: total=11536 tracked_deletions=11233 tracked_modifications=210 untracked=93 staged=0
deletions: assets=84 assets_project=38 tools_source_outside_bin_obj=2 docs_reports=1686 docs_screenshots=216 docs_agentlogs=13 docs_tasks=7 polish_deleted=true
deleted-extensions: meta=327 cs=50 shader=0 asset=11 unity=18
```

Both expected owner disposition artifacts are absent:

- `Docs/AssetAudit/MASS_DELETION_DIRTY_SET_DISPOSITION.md`
- `Docs/MASS_DELETION_DIRTY_SET_DISPOSITION.md`

The missing disposition is not a paperwork nit. It blocks cleanup, commit, and integration because the deletion wave contains production source/assets/scenes, tools, proof artifacts, screenshots, task files, and black-box/log evidence. The script's meta-pairing result remains only `PAIRING_CLEAN_BUT_NOT_DELETION_APPROVAL`.

Current controller matrix:

| Condition | Decision |
|---|---|
| No disposition file | `RED`: block cleanup/commit/integration. |
| `Assets/_Project`, `.cs`, `.asset`, or `.unity` deletions present | `CRITICAL`: restore or require route-owner/integrator disposition. |
| `Docs/Reports`, `Docs/Screenshots`, `Docs/AgentLogs` deletions present | `HIGH`: restore/archive only with evidence owner manifest. |
| `Docs/Tasks/POLISH.txt` deleted | `HIGH`: restore or formally replace through doc governance. |
| `.codexbuild`, `.codex-artifacts`, `Tools/bin/obj`, `igra` only | Candidate cleanup only after build/proof/release owner approval; does not lower other classes. |
| Meta pairing clean | Hygiene signal only; never approval. |
| Resolved sentinel without per-class owners | Reject as weak disposition. |
| Per-class owners approve and validator passes | Static deletion gate only; Unity/import/runtime/proof may still be pending. |

Updated decision: `DELETION_ACCEPTANCE_BLOCKED / OWNER_DISPOSITION_ABSENT / NO_COMMIT_READY_STATE`.

## Godel Disposition Packet - 2026-06-06

Status: read-only sidecar audit. No deletion, restore, move, archive, staging, commit, Unity, import, build, Play Mode, profiler, test, scene, prefab, material, or raw YAML action was performed.

Fresh validator result:

```text
MASS_DELETION_DIRTY_SET_REJECTED blockers=11 high_risk_deletions=true owner_disposition=false
status rows=11551
tracked deletions=11233
tracked modifications=212
untracked=106
staged=0
```

Tracked deletion breakdown from `git diff --name-only --diff-filter=D`:

```text
Docs=7929
.codexbuild=2490
.codex-artifacts=325
igra=242
.codex-build=90
Assets=84
Tools=65
NativeAudio=2
root/temp files=7
```

High-risk classes remain:

```text
Assets/_Project=38
Tools source outside bin/obj=2
Docs/Reports=1686
Docs/Screenshots=216
Docs/AgentLogs=13
Docs/Tasks=7
Docs/Archive=4749
Docs/DEPRECATED=676
deleted .cs=50
deleted .prefab=28
deleted .unity=18
deleted .asset=11
deleted .meta=327
```

Critical examples still present in deletion set:

```text
Assets/_Project/Scenes/*.unity sandbox/bisect/temp scenes
Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs
Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs
Tools/workspace_hygiene_1331.py
Tools/workspace_hygiene_apex_reaudit_1331.py
Docs/Tasks/POLISH.txt
Docs/AgentLogs/Dump_*.bin / *.h8dump black-box artifacts
```

Both expected owner-disposition artifacts are still absent:

- `Docs/AssetAudit/MASS_DELETION_DIRTY_SET_DISPOSITION.md`
- `Docs/MASS_DELETION_DIRTY_SET_DISPOSITION.md`

Required future disposition file content:

- owner per class and, for `Assets/_Project`, `Tools`, `Docs/Tasks`, and `Docs/AgentLogs`, preferably owner per path;
- action: `restore`, `keep deletion`, `archive with manifest`, or `blocked`;
- reason and provenance check;
- `.meta` handling;
- rollback path;
- proof required after action.

A resolved sentinel alone is rejected if it does not cover high-risk classes. Clean `.meta` pairing remains hygiene only, not approval.

Updated decision: `DELETION_ACCEPTANCE_BLOCKED / OWNER_DISPOSITION_ABSENT / NO_COMMIT_READY_STATE / NO_CLEANUP_APPROVAL`.

## Staged Deletion Escalation - 2026-06-06

Status: `STATIC_GIT_STATUS / COMMIT_BLOCKED`. No restore, reset, checkout, add, delete, move, clean, stage, unstage, commit, Unity, import, build, Play Mode, profiler, scene, prefab, material, or raw YAML action was performed by this controller.

Fresh static runner output now reports the deletion wave as staged:

```text
MASS_DELETION_DIRTY_SET_REJECTED blockers=11 high_risk_deletions=true owner_disposition=false
status-rows: total=11557 tracked_deletions=11233 tracked_modifications=215 untracked=109 staged=11234
```

Direct staged readback:

```text
git diff --cached --name-status | Group-Object status:
D = 11233
M = 1

git diff --cached --name-only -- Assets/_Project:
38

git diff --cached --name-only -- *.cs:
50

git diff --cached --name-only -- *.unity *.prefab *.asset *.meta:
384
```

High-risk staged examples include:

```text
Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs
Assets/_Project/Scripts/Compatibility/AddressablesCompatibility.cs.meta
Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs
Assets/_Project/Scripts/World/HectonMapMagicVegetationBridgeFloraVisualSway.cs.meta
Docs/Tasks/POLISH.txt
Docs/AgentLogs/Dump_*.bin / *.h8dump
```

This is worse than the previous static state because the risky deletion wave is now in the index. The decision is not "cleanup pending"; it is `NO_COMMIT_READY_STATE`. A future action must first either unstage/restore with explicit user instruction or create a real owner-disposition artifact covering every high-risk class and path. Clean `.meta` pairing remains irrelevant to approval.

Updated decision: `STAGED_MASS_DELETION_REJECTED / COMMIT_BLOCKED / OWNER_DISPOSITION_ABSENT`.

## Current Deletion State Correction - 2026-06-06

Status: `STATIC_GIT_STATUS / DELETION_WAVE_NOT_PRESENT_IN_CURRENT_SNAPSHOT`. No restore, reset, checkout, add, delete, move, clean, stage, unstage, commit, Unity, import, build, Play Mode, profiler, scene, prefab, material, or raw YAML action was performed by this controller.

A later fresh sample shows the staged deletion wave is no longer present:

```text
MASS_DELETION_DIRTY_SET_OK blockers=0 high_risk_deletions=false owner_disposition=false
status-rows: total=329 tracked_deletions=0 tracked_modifications=214 untracked=115 staged=0
```

Additional readback:

```text
git diff --cached --name-status: no output
git diff --name-status --diff-filter=D: no output
```

Interpretation:

- The staged-deletion escalation above was a valid earlier snapshot.
- It is not the current index/working-tree deletion state.
- The repository is still heavily dirty with tracked modifications and untracked files, so commit/handoff still requires scoped review.
- The current blocker moved back from deletion-wave rejection to ordinary dirty-worktree integration risk plus the remaining audio/player/surface/texture runtime blockers.

Updated decision: `MASS_DELETION_WAVE_ABSENT_CURRENTLY / DIRTY_WORKTREE_REMAINS / COMMIT_STILL_NOT_READY_WITHOUT_SCOPED_REVIEW`.
