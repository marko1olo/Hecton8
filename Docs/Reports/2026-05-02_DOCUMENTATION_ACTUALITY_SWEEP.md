# Documentation Actuality Sweep

Date: 2026-05-07
Status: PENDING VERIFICATION
Scope: active documentation authority, root documentation anchors, and current local verification evidence

Supersession note: this report is historical. The current broad documentation-sweep authority is `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.

## Mandates Followed

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `STRM_Asset_Lifecycle_Addressables_Loading_Memory.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

## What Was Checked

- `AGENTS.md`
- task-relevant `.agents-skills/*` mandates listed above
- repository-root documentation anchors: `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, `BUILD_PLAYTEST_ISSUES.md`
- active `Docs/**` inventory
- current report indexes: `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/Reports/README.md`
- current project-state anchor: `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md`
- critical queue: `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md`
- recent local build/log artifacts under root and `.codex-artifacts/`

Current documentation inventory counted `179` active/reference `.md/.txt` files under `Docs/` when excluding:

- `Docs/_Archive/**`
- `Docs/DEPRECATED/**`
- `Docs/Reports/DEPRECATED/**`
- `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/**`

This count is the active `Docs/` surface only. The three repository-root anchors are `AGENTS.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.

This pass did not rewrite archived, deprecated, package, asset-store, or third-party readme/license files. Those are preserved as historical/reference material unless an active index explicitly promotes them.

Full markdown read-pass addendum:

- root/`Docs` markdown files opened in the full pass: `389`
- approximate lines read after this sweep: `127905`
- read errors: `0`

Text evidence read-pass addendum:

- root/`Docs` text-like documentation/evidence files opened: `602`
- included extensions: `.md`, `.txt`, `.patch`, `.diff`, `.log`
- approximate text-like lines read: `352184`
- text-like read errors: `0`
- active text-like files excluding archive/deprecated/obsolete: `195`
- active text-like lines excluding archive/deprecated/obsolete: `70555`

Snapshot classification from that pass:

| Bucket | Count |
|---|---:|
| root active markdown | 3 |
| active `Docs` markdown | 175 |
| `Docs/DEPRECATED` markdown | 36 |
| `Docs/_Archive` markdown | 144 |
| `Docs/ARCHIVARIUS REPORTS/03_OBSOLETE` markdown | 26 |
| `Docs/Reports/DEPRECATED` markdown | 5 |
| total root/`Docs` markdown | 389 |

## Hard Evidence

Fresh commands run from `C:\hades\Hecton8`:

```text
dotnet build .\Hecton8.Core.csproj --no-restore
```

Initial result before restore:

- exit code: `1`
- reason: `NETSDK1004`, missing `Temp\obj\Hecton8.Core\project.assets.json`

Restore build:

```text
dotnet build .\Hecton8.Core.csproj
```

- exit code: `0`
- summary: `136 Warning(s)`, `0 Error(s)`
- elapsed: `00:01:24.05`

Post-restore no-restore rerun:

```text
dotnet build .\Hecton8.Core.csproj --no-restore
```

- exit code: `0`
- summary: `73 Warning(s)`, `0 Error(s)`
- elapsed: `00:00:23.95`

Supporting existing artifacts:

- `.codex-artifacts/dotnet-Hecton8.Core-2026-05-02-after-scatter-dispose-register-hardening.log`
- result: `Build succeeded`, `73 Warning(s)`, `0 Error(s)`
- `.codex-artifacts/dotnet-Hecton8.Core-2026-05-02-after-scatter-candidate-count-clamp.log`
- result: `Build succeeded`, `73 Warning(s)`, `0 Error(s)`

Conflicting/stale artifacts found:

- `unity-batch-smoke.log` ends with batchmode return code `0` after a `Tundra build success`.
- `unity-batch-autonomous-registry-sweep-rerun.log` contains an older `VehicleDockingModule.ResolveColliderRuntimeId` compile failure, then later records `ExitCode: 0` and `Tundra build success`.
- `.codex-artifacts/unity-batch-2026-05-02-autonomous-registry-sweep.log` contains an older `PowerGridManager` `ILateFrameTickable` compile failure.
- `.codex-artifacts/unity-batch-2026-05-02-final.log` exits with code `1` because Unity rejects `.codex-artifacts` as a directory name in that batch context.

Current compile truth for this pass is the fresh dotnet build sequence above. Warning count is build-state dependent; both successful commands report `0 Error(s)`. Unity Editor Play Mode, MCP console, GCMonitor, profiler, and memory-retention validation were not run.

## Source Count Recheck

Current source-count facts from filesystem reads:

| Metric | Current value |
|---|---:|
| `Assets/_Project` `.cs` files | 1087 |
| `Assets/_Project/Scripts` `.cs` files | 1047 |
| `Assets/_Project/Scripts` static line count | 571562 |
| average lines per script under `Assets/_Project/Scripts` | 545.90 |
| scripts directly under `Assets/_Project/Scripts` root | 317 |
| direct public interfaces in `GlobalRegistryContracts.cs` | 33 |

Largest current first-party script files by static line count:

| Lines | File |
|---:|---|
| 11340 | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` |
| 9389 | `Assets/_Project/Scripts/HectonPlayerMovement.cs` |
| 6599 | `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs` |
| 5798 | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` |
| 5708 | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` |
| 5471 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` |
| 5159 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` |
| 5012 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` |
| 4855 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` |
| 4726 | `Assets/_Project/Scripts/FaunaDirector.cs` |

DOTS / Entities boundary rechecked:

- `Packages/manifest.json` does not declare `com.unity.entities`.
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef` exists.
- The DOTS asmdef is define-gated and `autoReferenced: false`.
- Current source grep found `0` active `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem` references under `Assets/_Project/Scripts`.
- Current `World/Dots` source files are placeholder/fallback scaffolding, not live production ownership.

Static debt marker counts from current source grep:

| Marker | Hits |
|---|---:|
| `public static *Instance` | 133 |
| `ActiveRuntimeInstance` | 420 |
| `EnsureRuntimeInstance` | 86 |
| `DontDestroyOnLoad` | 66 |
| `.Complete(` | 6 |
| `StartCoroutine(` | 0 |
| `Camera.main` | 8 |
| `FindObjectOfType` | 3 |
| `Resources.Load` | 0 |
| `new Material(` | 57 |

Current `.Complete(` locations under `Assets/_Project/Scripts`:

- `ItemCatalog.cs`: dispatcher request completion callbacks only.
- `Optimization/AssetLifecycleGovernor.cs`: dispatcher request completion callbacks only.
- `World/DispatcherJobSwap.cs`: explicit `JobHandle.Complete()` inside the dispatcher swap helper.

The older report claim that `ProximityColliderSystem.Tick`, `SaveManager.Tick`, and `HectonFluidEngine.PostFixedTick` currently call `.Complete()` is stale by strict source grep. Remaining job risk is dispatcher-owned completion-window proof and runtime stall profiling, not those literal call sites.

## Link Integrity

Markdown link check over the active root/`Docs` surface found `3` missing markdown links before patching.

All three were in `Docs/Legacy_Backlog/Ð§Ñ‚Ð¾_Ð¸_ÐºÐ°Ðº_Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÑÐµÐ¼_â€”_Ð¶Ð¸Ð²Ð¾Ð¹_Ð¿Ð»Ð°Ð½.md`:

- two absolute `PROCEDURAL_WATER_PATTERN_REPORT.md` links pointed to a root file absent from the current workspace
- one absolute `PROCEDURAL_MATRIX_BIOME_CONTENT_REPORT.md` link pointed to a moved report now present under `Docs/_Archive/2026-04-16_Workspace_Cleanup/root/procedural_status_snapshots/`

Current handling:

- the matrix-biome content report link now points to its archive path
- the absent water-pattern report links are marked as historical references, not live links
- follow-up active markdown link check returns `0` missing markdown links
- full root/`Docs` markdown link check still finds `27` historical missing links inside `Docs/_Archive/**`; those files are preserved evidence dumps, not active project truth
- one deprecated redirect link in `Docs/DEPRECATED/Root_Legacy_And_Scan_Artifacts_2026-05-01/THIRD_PARTY_POISON.md` was corrected to the current architecture document path

## Documentation Changes Made

- At the May 2 boundary, `Docs/README.md` pointed to this sweep as the latest documentation evidence. Current authority has since moved to `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
- `Docs/Reports/README.md` now lists this sweep as a high-authority report.
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` now carries a May 2 addendum while retaining the stable file path.
- `Docs/2026-04-30_Codex_Full_Project_Forensic_Audit/06_CRITICAL_ACTION_QUEUE.md` now records the May 2 compile/documentation delta.
- `Docs/DOC_GOVERNANCE.md` and `Docs/ROOT_DOCS_REFERENCE.md` now recognize the May 2 authority/evidence boundary.
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md` count data was refreshed against the current filesystem.
- `Docs/Reports/2026-05-01_OBJECTIVE_PROJECT_CONCLUSION.md` source-count and largest-file data were refreshed against the current filesystem.
- `Docs/Legacy_Backlog/Ð§Ñ‚Ð¾_Ð¸_ÐºÐ°Ðº_Ð¸ÑÐ¿Ñ€Ð°Ð²Ð»ÑÐµÐ¼_â€”_Ð¶Ð¸Ð²Ð¾Ð¹_Ð¿Ð»Ð°Ð½.md` no longer contains broken absolute markdown links to absent root reports.

## Current Documentation Truth

- `Docs/README.md` is the entry point.
- `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` remains the current conceptual state anchor by stable path, updated through this pass.
- This file was the latest documentation sweep and build-evidence addendum on `2026-05-02`; it is superseded by `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md`.
- Older dated reports are evidence only after checking their latest delta sections.
- Archive/deprecated files are not active truth.

## Do Not Claim

- Do not claim Play Mode is stable.
- Do not claim the editor deadlock is fixed.
- Do not claim global zero-GC.
- Do not claim memory retention is flat.
- Do not claim MCP verified the current state.
- Do not claim Unity batchmode is clean from the conflicting old artifacts alone.
- Do not claim DOTS/Entities is production architecture.

## Regression Model

CPU: documentation-only edits; no runtime code changed by this pass.
GC: no hot-path code changed; measured `0 B/frame` proof is absent.
Memory: no assets, scenes, native containers, or runtime capacities changed.
Cadence: no tick, fixed tick, late-frame, or slow tick code changed.
Correctness: active docs now point to fresher compile evidence; runtime correctness remains unverified.

## Hot Path Impact

None from this pass. Documentation-only.

## Failure Modes

- Dirty source changes after this pass can invalidate the fresh dotnet build statement.
- Unity batchmode can still fail even if dotnet build succeeds.
- Scene/prefab wiring can contradict source-level documentation.
- Runtime GC and frame-time behavior can contradict static compile evidence.
- Old reports may still contain stale line-number claims.

STATUS: PENDING VERIFICATION
