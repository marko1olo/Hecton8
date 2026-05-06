# Documentation Actuality Sweep

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: active documentation authority, current source/build guard evidence, Unity MCP readback boundary

## Mandates Followed

- `.agents-skills/ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `.agents-skills/PROJECT_LTS_Compatibility_Layer.txt`

## What Was Checked

- `AGENTS.md`
- task-relevant mandates listed above
- repository-root documentation anchors: `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`, `TERRAIN_AND_BIOME_REALITY_MAP.md`
- active documentation indexes: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/Reports/README.md`
- current-state anchor: `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
- latest May 4 reports:
  - `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_REPORT.md`
  - `Docs/Reports/2026-05-04_CELESTIAL_ENVIRONMENT_ORBITAL_SYNC_REPORT.md`
  - `Docs/Reports/2026-05-04_CELESTIAL_ORBITAL_PROTOCOL_METEOR_REPORT.md`
- recent May 3 guard/report anchors:
  - `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
  - `Docs/Reports/2026-05-03_REGISTRY_RENDERABLE_AND_JOB_BARRIER_GUARD.md`
  - `Docs/Reports/2026-05-03_CONSOLE_VR_READINESS_AUDIT.md`

Archived, deprecated, package, asset-store, third-party README/license, and raw prompt/log bundles are not current authority. The working tree currently contains a dirty deprecated raw log bundle; this sweep does not use it as current evidence.

## Current Project Facts

- Unity project version: `6000.4.1f1`.
- Active platform from MCP project info: `StandaloneWindows64`.
- Packages manifest includes URP `17.4.0`, Addressables `2.7.6`, Input System `1.19.0`, Memory Profiler `1.1.12`, ProBuilder `6.0.9`, and Unity MCP from Git.
- Packages manifest does not declare `com.unity.entities`.
- Build Settings scene order is still:
  - `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
  - `Assets/_Project/Scenes/01_MAIN_MENU.unity`
  - `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- First-party C# inventory:
  - `Assets/_Project/**/*.cs`: `1118`
  - `Assets/_Project/Scripts/**/*.cs`: `1078`
- Active `Docs/**/*.md` count excluding `_Archive`, `DEPRECATED`, `Reports/DEPRECATED`, and `ARCHIVARIUS REPORTS/03_OBSOLETE`: `191`.
- Active/root markdown surface count excluding archives/deprecated/packages/third-party readmes: `217`.
- Post-sorting note: `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` was added after this sweep and increments active `Docs/**/*.md` to `192`; use that report for latest sorting inventory.
- Working tree was already dirty before this documentation update. Existing dirty source, asset, renderer, shader, deprecated-log, report, and artifact files were not reverted.

## Fresh Static Rechecks

- `rg -n "\.Run\(" Assets/_Project/Scripts -g "*.cs"` returned no matches.
- `.Complete(` source hits under `Assets/_Project/Scripts` are now `5`, not the older `1`:
  - `VoxelDeformationSmokeTester.cs`
  - `BiomeTransitionSmokeTester.cs`
  - `HectonBiomeMatrixMapMagicPostProcessNode.cs`
  - `DispatcherJobSwap.cs`
- `rg -n "Terrain\.activeTerrain|Terrain\.activeTerrains|Terrain\.SampleHeight|GetHeights\(" Assets/_Project/Scripts -g "*.cs"` returned no matches.
- `RenderSettings.skybox =` source scan returns only `Assets/_Project/Scripts/HectonAtmosphereManager.cs:80`, inside the `AtmosphereDirector.SetSkybox()` facade.
- `_StarTex` scan in `HectonCelestialEngine.cs` and `Hecton_AlienSky_Master.shader` returned no matches.
- `Unity.Entities`, `IComponentData`, `SystemBase`, and `ISystem` source scan under `Assets/_Project/Scripts` found only the editor generated-project pruner reference to the stale name `Unity.Entities`.
- `StartCoroutine(` scan under `Assets/_Project/Scripts` returned no matches.
- `Camera.main` / `Resources.Load` scan under `Assets/_Project/Scripts` found only editor/comment contexts, no gameplay owner hit.

## Post-Sweep Guard Repair Addendum

After this documentation sweep, a focused foundation guard repair was applied and documented in `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`.

Current post-repair evidence:

- `CrashTelemetryBuffer` bootstrap safe-halt MMF dump now uses `UnsafeMemoryCopyGuard.TryMemCpy(...)`.
- `MainMenuController.Update()` fallback was removed; menu cadence now depends on dispatcher/registry tick registration.
- `rg -n "UnsafeUtility\.MemCpy" Assets/_Project/Scripts -g "*.cs"` now returns only `Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs`.
- `.\Tools\ReloadAudit\Scan-FoundationGuards.ps1` exits `0`.
- Fresh post-repair `Hecton8.Core.csproj --no-restore` build reports `0 Warning(s)` / `0 Error(s)`.

This is source/build evidence only. It is not Play Mode, menu UX, zero-GC, profiler, or player-build proof.

## Pre-Repair Guard Evidence

Command:

```text
.\Tools\ReloadAudit\Scan-FoundationGuards.ps1
```

Original result before the focused repair:

- exit code: `1`
- generated: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- hard zeroes still clear for blind registry flag drift, origin-shift blind flag drift, synchronous `.Run(` sites, hot-path `.Run(` review sites, raw listener dispatch, `GlobalRegistry.Input` nullable misuse, direct `InputManager.Instance`, optimization singleton residue, unauthorized Unity loops, legacy coroutines, forbidden runtime asset APIs, release-reachable direct hot-path `Debug.Log`, and broad physics masks
- pre-repair hard failure: raw `UnsafeUtility.MemCpy` outside `UnsafeMemoryCopyGuard` had count `1`
- pre-repair hard-fail location: `Assets/_Project/Scripts/CrashTelemetryBuffer.cs:903`
- current review inventory:
  - `.Complete(` text hits: `5`
  - runtime Find API text hits outside Editor folder: `7`, all inside `Assets/_Project/Scripts/Dev/CelestialSyncSmokeTester.cs`

This pre-repair failure is superseded by `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`. The guard scan is still source-only and does not prove runtime GC, frame time, memory retention, or safe dispatcher cadence.

## Build Evidence

Commands were run serially because Unity-generated projects share `Temp\obj`; parallel builds can create false `CS2012` file-lock failures.

```text
dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal
```

- result: `Build succeeded`
- summary: `0 Warning(s)`, `0 Error(s)`
- elapsed: `00:00:31.62`

```text
dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal
```

- result: `Build succeeded`
- summary: `0 Warning(s)`, `0 Error(s)`
- elapsed: `00:00:17.86`

```text
dotnet build .\Hecton8.World.Dots.csproj --no-restore ...
```

- result before restore: `NETSDK1004`, missing `Temp\obj\Hecton8.World.Dots\project.assets.json`

```text
dotnet build .\Hecton8.World.Dots.csproj -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal
```

- result after restore: `Build succeeded`
- summary: `1 Warning(s)`, `0 Error(s)`
- warning: `ScatterEntitiesComponentPlaceholder.Value` is never assigned and stays `0`
- elapsed: `00:00:07.60`

```text
dotnet build .\Hecton8.PlayModeTests.csproj --no-restore ...
```

- result before restore: `NETSDK1004`, missing `Temp\obj\Hecton8.PlayModeTests\project.assets.json`

```text
dotnet build .\Hecton8.PlayModeTests.csproj -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal
```

- result after restore: `Build succeeded`
- summary: `0 Warning(s)`, `0 Error(s)`
- elapsed: `00:00:19.34`

These are command-line compile checks. They are not Unity Play Mode, player-build, profiler, GCMonitor, scene/prefab, save/load, or memory-retention proof.

## Unity MCP Readback

MCP initially returned session/disconnect errors for `editor/state`, rendering stats, renderer features, and console reads. After retry:

- `mcpforunity://project/info` returned project root `C:/hades/Hecton8`, Unity `6000.4.1f1`, platform `StandaloneWindows64`.
- editor state returned:
  - active scene: `Assets/_Project/Scenes/01_MAIN_MENU.unity`
  - `is_playing: true`
  - `is_changing: true`
  - `is_compiling: false`
  - `ready_for_tools: true`
- `manage_scene(get_active)` returned `01_MAIN_MENU`, build index `1`, loaded, not dirty, root count `17`.
- console errors: `0`
- console warnings/errors combined: `18` warning entries
  - `17` warnings: `GlobalRegistry` unregister called for non-registered tick buckets
  - `1` warning: `SystemDispatcher` dispatcher phase exceeded slow threshold
- rendering stats returned:
  - draw calls: `0`
  - batches: `0`
  - set pass calls: `0`
  - render textures: `32`
  - render texture bytes: `68,215,964`
- active renderer data: `PC_High_Renderer`
- renderer features: `8`
  - inactive: `ScreenSpaceAmbientOcclusion`
  - active: `ShapesRenderFeature`, `DecalRendererFeature`, `ScreenSpaceShadows`, `HectonScooterVolumetricShaftsFeature`, `HectonAbyssalSsdoFeature`, `HectonVisorFluidDistortionFeature`, `HectonRetinaDistortionFeature`

This is editor readback, not player-build proof. The editor was already in Play Mode transition; this pass did not stop Play Mode.

## Documentation Changes Made

- Created this report as the current documentation actuality sweep.
- Updated `Docs/README.md` to point at May 4 reports and the current verification boundary.
- Updated `Docs/Reports/README.md` to include May 4 high-authority reports and the current guard-fail fact.
- Updated `Docs/DOC_GOVERNANCE.md` authority order to use this report as the latest broad documentation truth.
- Updated `Docs/ROOT_DOCS_REFERENCE.md` root-surface check to May 4.
- Updated `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` with a May 4 addendum while keeping the stable path.
- Updated `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md` with the May 4 documentation/guard evidence delta.
- Updated root `MASTER_RELEASE_WORK_PLAN.md` and `BUILD_PLAYTEST_ISSUES.md` current-state boundaries.
- Updated `Docs/QUALITY_GATES.md` current-state boundary.
- Updated active procedural, scatter, architecture, fauna, flora, legacy-reference README, Archivarius `01_GENERAL_INFO`, and targeted Archivarius `02_ACTUAL_REPORTS` boundary links from the May 2 sweep to this May 4 sweep where those files still used the May 2 authority path.
- Updated the root compatibility mirror `TERRAIN_AND_BIOME_REALITY_MAP.md` with the May 4 terrain API scan delta; `Docs/Reports/TERRAIN_AND_BIOME_REALITY_MAP.md` remains the canonical terrain/biome report.
- Added `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` as the post-sweep guard repair addendum.
- Added `Docs/Reports/2026-05-04_WARNING_CLEANUP.md` as the post-sweep warning cleanup addendum.

## Post-Sweep Warning Cleanup Addendum

Read `Docs/Reports/2026-05-04_WARNING_CLEANUP.md` after this sweep for the current warning boundary.

- Latest warning-cleanup Core build returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:27.18`.
- Latest warning-cleanup Editor build returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:15.12`.
- Latest warning-cleanup DOTS build returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:04.03`.
- Latest warning-cleanup PlayModeTests build returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:15.62`.
- Latest foundation guard rerun after `MainMenuController.Update()` removal exited `0` with unauthorized Unity loop methods `0`.
- Latest warning-cleanup Unity console readback after clear/script refresh returned `0` error/warning entries.
- The earlier `18` warning MCP console snapshot above remains historical evidence from this documentation sweep, not the latest console readback.
- This does not prove Play Mode stability, runtime frame time, GC, memory retention, scene/prefab wiring, or player-build readiness.

## Current Documentation Truth

- `Docs/README.md` is the entry point.
- `Docs/Reports/2026-05-06_DOCUMENTATION_SYNCHRONIZATION_PASS.md` is the latest broad documentation synchronization and current May 6 inventory/header/MCP boundary.
- `Docs/Reports/2026-05-04_DOCUMENTATION_SORTING_AUTHORITY_MAP.md` is the latest documentation sorting and authority classification map, amended by the May 6 synchronization pass.
- This file is the latest documentation sweep and current evidence boundary.
- `Docs/Reports/2026-05-04_WARNING_CLEANUP.md` is the latest warning-cleanup and current post-refresh console-readback boundary.
- `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md` is the latest foundation guard repair boundary.
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` remains the stable conceptual current-state anchor, now amended with May 4 evidence.
- May 4 celestial reports are current source/build reports, but their own residual limits still apply.
- Older reports are evidence only after checking their latest delta sections.
- Archive/deprecated files are not active authority.

## Do Not Claim

- Do not claim runtime stability from the post-repair guard pass. The current source guard exits `0`, but Play Mode/profiler proof is absent.
- Do not claim sustained runtime console cleanliness. Latest post-cleanup console readback is `0` error/warning entries after clear/script refresh, but no bounded Play Mode run was captured.
- Do not claim Play Mode is stable. Editor state reports Play Mode transition on `01_MAIN_MENU`.
- Do not claim global zero-GC. GCMonitor/profiler proof was not captured.
- Do not claim memory retention is flat.
- Do not claim player-build readiness.
- Do not claim runtime celestial/meteor behavior was observed in scene.
- Do not claim DOTS/Entities is production architecture.

## Regression Model

CPU: original sweep edits were documentation-only plus guard report regeneration. The post-sweep guard repair touched `CrashTelemetryBuffer` and `MainMenuController`; the warning cleanup touched first-party warning sites and `SystemDispatcher` console-warning routing. No runtime profiler capture was taken.

GC: no hot-path code was changed. Measured `0 B/frame` proof is absent.

Memory: no assets, textures, scenes, native containers, or runtime capacity values were intentionally changed. Rendering stats showed `32` render textures and `68,215,964` render texture bytes in the current editor readback, but this is not a retention slope.

Cadence: no dispatcher, tick, fixed tick, slow tick, or scene-load cadence was changed. Static `.Run(` is `0`; `.Complete(` has `5` review sites and cannot be called runtime-safe without owner/profiler proof.

Correctness: active docs now point to current May 4 evidence, including clean Core/Editor/DOTS/PlayModeTests compile, latest warning-cleanup console readback `0` error/warning entries, and the post-repair source guard pass. Runtime correctness remains unverified.

## Failure Modes

- Dirty source changes after this pass can invalidate build and scan statements.
- Unity editor state was already in Play Mode transition; runtime warning inventory can change after transition completes.
- MCP can disconnect or return stale state; it did so before retry.
- Source scans do not prove hot-path frequency, GC allocation, frame-time, or scene/prefab wiring.
- Generated reports can be overwritten by guard scripts; read timestamps before citing them.

STATUS: PENDING VERIFICATION
