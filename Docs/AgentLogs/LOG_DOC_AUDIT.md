# DOC_AUDIT Log

Status: PENDING VERIFICATION  
Evidence ceiling: STATIC_SOURCE / STATIC_DOC / FILESYSTEM / PACKAGE_LOCK unless explicitly noted otherwise.  
Batch note: Previous LOG_DOC_AUDIT is archived under `Docs/Archive/Batch003/AgentLogs/`; this active file starts the current R5 continuation.

## R5 Package / Config / Script-Local Docs Reality Pass - 2026-05-13

What was wrong:
- Stable docs did not clearly separate a clean `Packages/manifest.json` from physical legacy package folders still sitting under `Assets`.
- AGENTS/active docs wording implied Low URP renderer state that no longer matches current asset GUID mapping: `URP_Low` points to `Mobile_Renderer`, not `PC_Renderer`.
- `Assets/_Project/Scripts/UI/SETTINGS_SYSTEM_GUIDE.md` still described PlayerPrefs / Easy Save 3 and a stale four-preset quality model.
- `Assets/_Project/Scripts/README2.md` still described `SaveData v2`, ES3 dictionary persistence, and an old `ToolHUDPanel.cs` claim not found in the current script tree.

What was done:
- Patched current active docs: `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, `Docs/README.md`, `Docs/Reports/README.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, and `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`.
- Patched script-local docs: `Assets/_Project/Scripts/UI/SETTINGS_SYSTEM_GUIDE.md` and `Assets/_Project/Scripts/README2.md`.
- Recorded current package/config facts: Unity `6000.4.1f1`, URP `17.4.0`, Addressables `2.7.6`, Input System `1.19.0`, AI Navigation `2.0.11`, Memory Profiler `1.1.12`, BuildSettings scenes `00_BOOTSTRAP`, `01_MAIN_MENU`, `02_HECTON_WORLD`.
- Recorded contamination facts: Astar `605` files, Easy Save 3 `422` files, Demigiant `357` files including DOTween/DOTweenPro, and DarkTonic/MasterAudio `346` runtime files plus separate editor/resources files.
- Recorded first-party scan boundary: no first-party `.cs` hits for DG.Tweening, DOTween, ES3, Easy Save, MasterAudio, or DarkTonic; Astar remains only dormant label/editor/guard text in the scoped scan.

Cinematic Cheats used:
- None. Documentation-only reality correction.
- Runtime proof deliberately not claimed. Evidence classes are STATIC_SOURCE, STATIC_DOC, FILESYSTEM, and PACKAGE_LOCK.

Exact Microseconds saved:
- `0 us/frame`. No runtime code changed.

Verification:
- `rg` confirmed stale script-doc phrases for PlayerPrefs backend, Easy Save 3 wrapper, ES3 dictionaries, `SaveData v2`, `CurrentVersion = 2`, and old Ultra quality-preset wording are gone or explicitly superseded.
- `rg` confirmed R5 package/config and contamination facts are present in the active docs.
- `git diff --check -- Docs Assets/_Project/Scripts/UI/SETTINGS_SYSTEM_GUIDE.md Assets/_Project/Scripts/README2.md` reported line-ending warnings only, no whitespace-error exit.

Residual risk:
- Physical third-party contamination is still unresolved. This pass documented it; it did not strip assets.
- Unity import, Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, scene wiring, and visual-quality proof remain absent.

## 2026-05-13 - Save / Persistence Static X-Ray

What was wrong:

- Persistence had not been promoted into the durable project-state x-ray even though it is a major long-lived-state risk surface.
- Old save artifacts under `.codex-artifacts` are stale relative to the current workspace. The visible `unity-save-persistence-omega-smoke-2026-05-05.log` stops at Unity startup/licensing and contains no visible PASS/FAIL save result.
- `SaveManager` is active and enabled in `00_BOOTSTRAP`, and source shows `Awake()` allocating persistent save buffers immediately.

What was found:

- `SaveBinaryStorage.cs` is 334 KB; `PersistentWorldRegistry.cs` is 263.6 KB / 5120 static lines; `SaveManager.cs` is 131 KB; `SaveBinaryPayloadCodec.cs` is 110.4 KB.
- `SaveManager` reserves about 132 MB native staging at boot: 64 MB raw payload buffer and 68 MB compressed payload buffer, plus small candidate scratch.
- Persistence architecture is materially serious: binary header versions, checksums, LZ4/Deflate paths, backup load candidates, migration, indexed persistent-world sectors, mod sidecars, save events, and smoke/playmode surfaces.
- Normal primary save promotion uses backup rotation plus `File.Move(temp, final)`; `File.Replace` exists in recovery paths. This is not naive, but it is not a uniform atomic-replace policy.
- Static YAML evidence did not prove placed `SaveStation` or save smoke tester components in `_Project` scenes/prefabs/assets; GUID hits were only `.meta` files.

Cinematic cheats used:

- None. Documentation/audit only.

Exact microseconds saved:

- 0 us/frame. No runtime optimization was executed. The only quantified runtime concern is boot memory residency, approximately 132 MB native staging before a save/load request.

Result:

- Added `Save / Persistence Addendum` to `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Status remains `PENDING VERIFICATION` until fresh Play Mode/player save-load artifacts and memory captures exist.

## R7 AGENTS Authority Reality Patch - 2026-05-13

What was wrong:
- `AGENTS.md` and `.codexrules/AGENTS.md` still said Low tier used `PC_Renderer` and render scale `0.65`.
- Current project assets prove `Abyss (Low)` -> `URP_Low`, `URP_Low` -> `Mobile_Renderer`, and `m_RenderScale: 0.85`.
- The same authority files implied `Assets/_ThirdParty` as the third-party location even though the current tree lacks that folder and contamination lives under `Assets/Plugins`, `Assets/AstarPathfindingProject`, `Assets/Resources`, and physical `Packages`.
- A legacy Easy Save 3 instruction still told agents to add `[ES3NonSerializable]`.

What was done:
- Patched both authority files to Low renderer `Mobile_Renderer` and scale `0.85`.
- Patched third-party wording to distinguish preferred quarantine from current contamination paths.
- Replaced the Easy Save 3 attribute instruction with no-new-ES3/quarantine wording.
- Added R7 notes to `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, `Docs/README.md`, `Docs/Reports/README.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.

Cinematic Cheats used:
- None. Documentation-only authority correction.

Exact Microseconds saved:
- `0 us/frame`. No runtime code changed.

Verification:
- `rg` readback confirmed `AGENTS.md` and `.codexrules/AGENTS.md` now contain `Mobile_Renderer` and scale `0.85`.
- Static source proof came from `ProjectSettings/QualitySettings.asset` and `Assets/_Project/Data/URP_Low (PC_RPAsset).asset`.
- `git diff --check` on touched R7 authority/report files returned no whitespace errors.

Residual risk:
- Unity import/build/player proof remains absent.
- Physical third-party contamination and live scripting defines remain unresolved.

## 2026-05-13 - Package / Player Settings Drift X-Ray

What was wrong:

- The earlier package/config wording could make the project sound cleaner than it is if read only through `manifest.json`.
- `manifest.json` does not show all current package/import risk. `packages-lock.json`, physical `Packages/`, physical legacy plugin folders, asmdefs, and PlayerSettings defines all matter.

What was found:

- `packages-lock.json` and physical `Packages/` include embedded Crest `5.4.1`, MicroSplat `3.9.0`, and embedded ShaderGraph even though `manifest.json` does not list those IDs directly.
- Crest metadata targets Unity `2022.3`; lock dependency text still mentions RP Core/ShaderGraph `14.0.11`; current project pin is Unity `6000.4.1f1` with URP `17.4.0`.
- MicroSplat core metadata targets Unity `2019.4`; URP2022 support targets Unity `2022.2`.
- PlayerSettings define `DOTWEEN` on many platforms. Standalone also defines Crest, MicroSplat, MapMagic, GPUInstancer, Odin, Amplify, Shapes, NiceVibrations, Bakery, VLB, RealtimeCSG, and DOTween symbols.
- XR package proof is absent: no XR Management or OpenXR package entries were found.
- Release metadata still contains Unity template app identifiers.

Cinematic cheats used:

- None. Documentation/audit only.

Exact microseconds saved:

- 0 us/frame. No runtime changes. The saved work is future risk isolation: fewer stale symbols, fewer package paths, and fewer shader/import surprises if cleaned.

Result:

- Added `Package / Player Settings Drift Addendum` to `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Updated `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, `Docs/README.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.

## 2026-05-13 - World / Scatter / Streaming X-Ray

What was wrong:

- The project had serious world/scatter files, but the stable docs did not clearly separate "real world runtime architecture exists" from "production scene/runtime wiring is proven."
- That ambiguity can create fake readiness.

What was done:

- Audited scatter, field sampler, chunk residency, vegetation bridge, streaming profile, world data assets, editor authoring tools, and bootstrap registration paths.
- Promoted the findings to `Docs/PROJECT_STATE_STATIC_XRAY.md`, the current DOC_AUDIT report, docs index, and architecture map.

Cinematic Cheats used:

- Proxy/final variant architecture and layer-specific streaming were recognized as the intended visual-fake/scalability path.
- No runtime optimization was performed.

Exact Microseconds saved:

- 0 us/frame direct.
- Runtime costs remain unmeasured; static risk is concentrated in scene wiring, Addressables payload readiness, vegetation native pool memory, and scatter GC/frame-time proof.

## 2026-05-13 - Root Docs / Atlas Governance Boundary

What was wrong:

- `Docs/ROOT_DOCS_REFERENCE.md` still had a stale future-cleanup sentence implying the root surface was only three active anchors plus the terrain mirror.
- `PROJECT_ATLAS.md` and `Docs/PROJECT_ATLAS.md` needed an explicit boundary: they are asmdef graph snapshots, not package/config/runtime authority.
- `Docs/Tasks/Status_DOC_AUDIT.md` had R8 above R7, and `Docs/AgentLogs/Rationale_DOC_AUDIT.md` had duplicate `Decision 006` headings.

What was done:

- Updated root-governance docs to the current static root surface: 6 markdown, 3 log, 3 json, 0 txt.
- Reaffirmed active root authority as only `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
- Demoted `BROKEN_PREFABS.md`, root `PROJECT_ATLAS.md`, and root `TERRAIN_AND_BIOME_REALITY_MAP.md` as generated snapshot / compatibility mirror files.
- Added atlas boundary notes: asmdef dependency references, including editor `EasySave3`, are contamination/evidence, not approval for forbidden runtime backends.
- Repaired DOC_AUDIT status ordering and rationale numbering.

Cinematic Cheats used:

- None. Documentation governance only.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Static filesystem scan confirmed root counts and file names.
- Static asmdef readback confirmed `Hecton8.Editor` references `EasySave3`.
- Runtime, Unity import, Console, Play Mode, profiler, GCMonitor, player build, and visual proof remain absent.

## 2026-05-13 - Active Root Anchor Proof Boundary

What was wrong:

- `BUILD_PLAYTEST_ISSUES.md` and `MASTER_RELEASE_WORK_PLAN.md` still promoted the absent May 11 Core build artifact as current compile-only evidence.
- `BROKEN_PREFABS.md` contained a `0` missing-script table without saying it was only a generated/static snapshot.

What was done:

- Replaced the root-anchor May 11 build-proof wording with a May 13 DOC_AUDIT boundary: the cited summary/log are absent and the compile-success line is stale report text until restored or replaced.
- Added a `PENDING VERIFICATION` proof-boundary header to `BROKEN_PREFABS.md`.
- Updated the current DOC_AUDIT X-Ray, Docs index, Reports index, status, and rationale.

Cinematic Cheats used:

- None. Documentation proof hygiene only.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Static grep/readback targeted the two root anchors and `BROKEN_PREFABS.md`.
- No Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, or prefab-validation run was performed.

## 2026-05-13 - SpaceEngine Research Proof Boundary

What was wrong:

- `Docs/SPACE_ENGINE_RESEARCH/SPACE_ENGINE_MATH_INTEGRATION_2026-05-05.md` listed a stale MapMagic node path under `Assets/_Project/Scripts/World/`.
- The same file presented a historical `Build succeeded`, `0 Warning(s)`, `0 Error(s)` source gate without enough May 13 boundary text.
- `Library/SpaceEngine098TerrainSmokeTester.json` exists, but it is old-schema May 5 output and lacks the current per-node timing fields.

What was done:

- Updated the MapMagic node path to `Assets/_Project/Scripts/Plugins/MapMagic/HectonSpaceEngine098MapMagicNodes.cs`.
- Added a May 13 DOC_AUDIT boundary to separate static source presence from current Unity smoke proof.
- Replaced final readiness wording with static-present / runtime-pending wording.
- Updated current DOC_AUDIT report, Docs index, Reports index, status, and rationale.

Cinematic Cheats used:

- None. Documentation proof hygiene only.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Static readback confirmed the current SpaceEngine source paths and old Library smoke JSON timestamp.
- Static grep confirmed current smoke code emits `ridgedMsX1000`, `craterMsX1000`, `rilleMsX1000`, `metricsMsX1000`, and `nodeBudgetPassed`.
- No Unity, `dotnet`, Play Mode, profiler, GCMonitor, or smoke harness run was performed.

## 2026-05-13 - Omega Smoke Artifact Drift

What was wrong:

- Active docs still treated Omega smoke as PASS through `Library/OmegaAutonomySmokeTester.json`.
- Current disk artifact `Library/OmegaAutonomySmokeTester.json` actually reports `FAIL`.
- The failing field is `nativeSentinelBalance.pass=false`, with `allocationDelta=2` and `trackedByteDelta=2560`.
- The saved `Docs/SPACE_ENGINE_RESEARCH/OMEGA_AUTONOMY_UNITY_SMOKE_CODEX_2026-05-05.json` remains PASS but is older than the current Library artifact.

What was done:

- Patched `Docs/SPACE_ENGINE_RESEARCH/OMEGA_AUTONOMY_CODEX_AUDIT_2026-05-05.md` and `Docs/SPACE_ENGINE_RESEARCH/OMEGA_AUTONOMY_AUDIT.md`.
- Patched `Docs/README.md`, `Docs/Reports/README.md`, and current DOC_AUDIT X-Ray.
- Recorded the exact current failure values and demoted older PASS/OMEGA labels to historical scoped evidence.

Cinematic Cheats used:

- None. Documentation proof hygiene only.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Read current `Library/OmegaAutonomySmokeTester.json`, older saved Omega JSON, standalone shelf JSON, and SpaceEngine smoke JSON.
- Verified the cited `CodexArtifacts/unity-omega-smoke-2026-05-05-doc-continuation.log` is absent from the current filesystem.
- No Unity, `dotnet`, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-13 - Active Documentation Manifest Boundary

What was wrong:

- Four `Docs/Reports/*ACTIVE_DOCUMENTATION_MANIFEST.json` files were valid generated snapshots, but their count/build-state surfaces could still be read as current authority.
- The May 9 manifest described R186 as current local dotnet coverage for that sampled state.
- The May 11 manifest still carried May 11 build artifact paths previously found absent in the current filesystem.

What was done:

- Added `docAuditR13Boundary` to the May 6, May 7, May 9, and May 11 active documentation manifests.
- Demoted the May 9 manifest's `coveredCurrentSource` to `false` and preserved the original value as `originalSnapshotCoveredCurrentSource=true`.
- Scoped all manifest counts, `sourceCounts`, `currentAuthority*`, `buildState`, and `entries` to historical snapshot evidence only.
- Pointed current authority to `Docs/Reports/README.md` and `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`.

Cinematic Cheats used:

- None. Documentation proof hygiene only.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Completed static readback: `docAuditR13Boundary` is present in all four active manifest JSON files and R13 is promoted to the current report/indexes.
- `git diff --check` will be run after the R14 documentation pass.
- No Unity, `dotnet`, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-13 - Gameplay Economy / Resource Loop X-Ray

What was wrong:

- The project had real gameplay-economy code, but no durable doc separated "systems exist" from "the first-hour resource loop is proven".
- Static data shows `23 / 27` resource-node harvest items lack `worldPrefab`.
- `ResourceNode` skips legacy loot for template-driven extractor items, while `PersistentWorldRegistry.TryRegisterDroppedItem` refuses null `worldPrefab`; this is a concrete "node can deplete but pickup emission can fail" candidate.
- Copper has split authority: root `Data_Copper.asset` is used by resource nodes/barter and is not in `ItemCatalog`; raw `Resources/Raw/Data_Copper.asset` is cataloged and used by recipes. Both share `stableId: Data_Copper`.

What was done:

- Audited ItemData, ItemCatalog, recipes, resource-node templates, inventory, crafting, fabricator, scarcity, resource distribution, construction/logistics, and pipe-runtime surfaces statically.
- Promoted the resource acquisition seam, duplicate copper authority, and unproven procedural/pipe scene wiring to stable/current docs.
- Kept the verdict bounded: gameplay economy is real source architecture, but resource-loop readiness remains `PENDING VERIFICATION`.

Cinematic Cheats used:

- Recommended a cheap canonical pickup route before visual/object polish: generic raw-resource pickup prefab or explicit inventory-grant path, then richer pickup variants only after progression is proven.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Static only: file/YAML/source/string evidence. Required later runtime route is mine copper -> item collected event -> inventory contains `Data_Copper` -> `quest_copper_sample` completes -> craft `Copper Wire` -> save/load.
- No Unity, `dotnet`, Play Mode, profiler, GCMonitor, or player build was run.

## 2026-05-13 - AI/Fauna Data vs Runtime Wiring Boundary

What was wrong:

- AI/Fauna docs preserved real coverage data, but did not clearly separate authored fauna data from active production-scene runtime proof.
- Current static inventory is strong: `22` recursive creature archetypes, `22` fauna data templates, `108` fauna biome datasets, `432` non-null biome spawn prefab entries, `17` large-threat macro-zone refs, `13` family profiles, and `6` generated proxy prefabs.
- Static script-GUID scan did not prove serialized `FaunaDirector`, `WorldFaunaSpawnRegistry`, `FaunaRuntimeSmokeTester`, or `EcosystemRuntimeInstaller` in current scenes/prefabs/assets.
- `EcosystemRuntimeInstaller` creates genetics/health/migration managers, not `FaunaDirector` or `WorldFaunaSpawnRegistry`.
- `GameBootstrapper` can register `DemiurgeFaunaSimulationService.Shared` when no real fauna director registers `IFaunaSim`; that fallback is ready but has `ResidentSlotCapacity = 0`.
- `.codex-artifacts/fauna-omega-smoke-2026-05-05.log` ends with Unity return code `1` and is not PASS evidence.

What was done:

- Patched `Docs/AI_Fauna/README.md`, `AI_FAUNA_WORLD_INTEGRATION_REPORT.md`, and `AI_CREATURE_ROSTER_ENTERPRISE.md` with the R15 proof boundary.
- Promoted the same boundary to `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md`, `Docs/README.md`, `Docs/Reports/README.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.
- Updated DOC_AUDIT status and rationale with the data-vs-runtime distinction.

Cinematic Cheats used:

- Kept Low-tier direction on proxy-prefab/data-only fauna and adaptive density caps before any creature-simulation spending. High/Ultra visual overkill is allowed only after the real director path is proven.

Exact Microseconds saved:

- 0 us/frame. Documentation-only change.

Verification:

- Static only: filesystem counts, YAML field scan, source readback, script-GUID grep, and artifact log readback.
- No Unity, `dotnet`, Play Mode, profiler, GCMonitor, player build, or live scene load was run.

## 2026-05-13 - Tools / PDA / First-Hour Interface X-Ray

What was wrong:

- Tool and PDA code could be misread two wrong ways: either as giant-file bloat or as a proven first-hour route.
- Static inventory shows the tool stack is real: `12` tool ItemData assets, `12` held prefabs, `12` world prefabs, `13` ToolMetadata assets, and all tool ItemData `worldPrefab` refs non-null.
- `ToolMetadata_LogicSpanner.asset` and `LogicSpannerTool.cs` exist, but no `Item_Tool_LogicSpanner.asset`, held prefab, world prefab, catalog ref, or recipe ref was found.
- `Player.prefab` carries `ToolLoadoutProvisioner` with `provisionInventoryOnStart=1`, `assignCoreLoadoutOnStart=1`, and `provisionConstructionMaterialsOnStart=1`; it grants all tool items plus root `Data_Copper` starter material.
- `Player.prefab` `PlayerPDA` has null `pdaPanel`, null `pdaCanvasGroup`, and null tab refs. World scenes contain PDA tab components, but `DiegeticPDAController` placement was not proven by class string or MonoScript GUID scan.

What was done:

- Audited tool data, held/world prefabs, metadata, player prefab wiring, tool provisioning, scanner/scan-log/PDA event paths, interaction pickup path, PDA shell code, runtime installers, and shipping cleanup boundaries statically.
- Promoted the findings to stable/current docs as R16 because the current X-Ray already has a parallel R15 AI/Fauna boundary.
- Kept the verdict bounded: tools are real architecture; first-hour truth is currently contaminated by dev provisioning and PDA bridge proof gaps.

Cinematic Cheats used:

- Recommended first-hour proof through one deterministic, cheap route before visual overkill: no startup all-tools grant, canonical pickup/grant path, visible PDA shell, then scan/craft proof.

Exact Microseconds saved:

- 0 us/frame. No runtime code changed.

Verification:

- Static only: source, prefab YAML, filesystem inventory, and binary scene string scans.
- No Unity, `dotnet`, Play Mode, profiler, GCMonitor, player build, or scene import validation was run.
