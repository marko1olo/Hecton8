# 2026-05-11 Documentation Current Data Continuation
Date: 2026-05-11
Status: SUPERSEDED BY 2026-05-13 DOC_AUDIT X-RAY / PENDING VERIFICATION
Scope: current documentation counters, compile-only boundary, active-index correction after the visual-fake mandate audit, and stable-index promotion for `ARCHITECTURE`, `ARCHIVARIUS`, and `.agents-skills`

## 2026-05-13 Supersession Note

`Docs/Reports/2026-05-13_DOC_AUDIT_XRAY.md` supersedes this report for current counters and missing-artifact evidence. DOC_AUDIT R2 did not find `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt` or the matching raw log in the current filesystem. Treat the compile-success line and numeric counters below as historical report text until the artifacts are restored or replaced by a fresh build capture.

Mandates followed:

- `.agents-skills/OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/PHYS_Physics_Integrity_Determinism_ForceMode.txt`
- `Docs/DOC_GOVERNANCE.md`
- `AGENTS.md`

## Current Facts

The May 9 R186 build is no longer the latest local compile evidence. Newer May 10/May 11 artifacts exist, and May 11 C# writes occurred after `2026-05-11_HECTON8_INQUISITION_CORE_BUILD_R8.log`.

Fresh compile-only evidence for this continuation:

- artifact summary: `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.summary.txt`
- raw artifact: `CodexArtifacts/2026-05-11_DOCS_CONTINUATION_CORE_BUILD_R1.log`
- command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /m:1 /nr:false /p:UseSharedCompilation=false`
- result: `Build succeeded`
- warnings: `0`
- errors: `0`
- `DOTNET_EXIT_CODE=0`
- `CS_FILES_BEFORE=1306`
- `CS_FILES_AFTER=1306`
- `CS_WRITES_AFTER_START=0`
- `CS_WRITES_AFTER_END=0`

This is local `dotnet` compile evidence only. It is not Unity import, Unity Console, Play Mode, profiler, GCMonitor, player-build, frame-time, memory, scene-wiring, or visual-quality proof.

Official Unity release-page check on 2026-05-11:

- Unity `6000.4.6f1` page exists and reports release date `2026-05-05`.
- Local project pin remains Unity `6000.4.1f1` by older local project/MCP evidence.
- No Unity upgrade was performed. `PROJECT_LTS_Compatibility_Layer.txt` still requires a migration branch, compile dry-run, warning catalog, adapter review, CI/perf gates, and regression proof before changing the project pin.

## Current Counters

After this continuation report, the May 11 manifest, and the stable index promotion:

- `Docs/**/*.md`: `449`
- active markdown excluding `DEPRECATED/`, `Reports/DEPRECATED/`, `_Archive/`, and `ARCHIVARIUS REPORTS/03_OBSOLETE/`: `236`
- active non-report markdown: `166`
- direct `Docs/Reports/*.md`: `70`
- active JSON files under `Docs`: `15`
- `Docs` total files: `897`
- root `.md` files: `5`
- root `.txt`/`.log` files: `2`
- active markdown header debt: `0` missing `Date:`, `0` missing `Status:`

Current source-count orientation:

- `Assets/_Project/**/*.cs`: `1306`
- `Assets/_Project/**/*.cs` physical lines: `770577`
- `Assets/_Project/Scripts/**/*.cs`: `1262`
- `Assets/_Project/Scripts/**/*.cs` physical lines: `753858`
- direct `Assets/_Project/Scripts/*.cs`: `335`
- `GlobalRegistryContracts.cs` direct public interfaces: `40`
- line-count method: `System.IO.StreamReader.ReadLine`

These counts include the current dirty workspace state. They are orientation data, not runtime proof.

## Authority Correction

Stable documentation authority now starts with:

1. `AGENTS.md`
2. `.agents-skills/README.md`
3. task-relevant `.agents-skills/*`
4. `Docs/README.md`
5. `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
6. `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`
7. `Docs/SYSTEMS_CONTRACTS.md`
8. `Docs/QUALITY_GATES.md`
9. `Docs/ARCHITECTURE/README.md`
10. `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
11. `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/README.md`
12. `Docs/ARCHIVARIUS REPORTS/02_ACTUAL_REPORTS/README.md`

Current evidence/counter snapshots start with this report, `Docs/Reports/2026-05-11_ACTIVE_DOCUMENTATION_MANIFEST.json`, `Docs/Reports/2026-05-11_AGENTS_SKILLS_VISUAL_FAKE_AUDIT.md`, and then the May 8/R186 reports. Dated reports remain evidence. They are not the permanent project brain.

## Broad Active-Doc Override

This pass added a `2026-05-11 Current-State Override` block to `35` active long-lived docs that still opened with a May 4 boundary. The override points each file to the May 11 data continuation, May 11 manifest, May 11 visual-fake audit, and the May 11 compile-only artifact. The older May 4 local sections remain as historical local context where they do not conflict with newer reports.

## Non-Claims

- No Unity MCP run was executed in this continuation.
- No Unity Console proof was captured.
- No Play Mode proof was captured.
- No profiler or GCMonitor proof was captured.
- No player build was captured.
- No visual quality inspection was performed.

Status remains `PENDING FINAL UNITY PROOF`.
