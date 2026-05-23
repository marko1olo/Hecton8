# BUILD_MEDIC Log

## 2026-05-22 Session Start

What was wrong: Broad build-health request arrived without a dedicated active batch XML id. Worktree is dirty and has many unrelated agent edits/deletions.
What was done: Read `AGENTS.md`, active domain file, current mandate registry, selected relevant mandates, checked CPU/dotnet/csc preflight, created isolated BUILD_MEDIC status/rationale/log files.
Cinematic Cheats used: none; compile/static repair only.
Exact Microseconds saved: no profiler proof; runtime savings not claimed.
Evidence: `Docs/Tasks/Status_BUILD_MEDIC.md`, `Docs/AgentLogs/Rationale_BUILD_MEDIC.md`.

## 2026-05-22 Core Build Pass

What was wrong: Unknown compile state for active dirty Core surface.
What was done: Ran guarded `dotnet build Hecton8.Core.csproj --no-restore -m:2 /nr:false -v:minimal`.
Cinematic Cheats used: none; compile/static proof only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_130944_Hecton8Core.log`, exit `0`, `0 Warning(s)`, `0 Error(s)`.

## 2026-05-22 Solution Warning Triage And First-Party Fix

What was wrong: `Hecton8.slnx` built with `136 Warning(s)` and `0 Error(s)`. Unique first-party warning sites were `InputManager` dead private flags and obsolete editor manager lookup.
What was done: Removed unused `_playerActionsSubscribed` / `_uiActionsSubscribed` fields and assignments; changed `ResidencyStreamingTunerWindow.ResolveManager()` to `FindAnyObjectByType<WorldChunkResidencyManager>()`.
Cinematic Cheats used: none; compile/static cleanup only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_131021_Hecton8Slnx.log`, `Docs/AgentLogs/WarningsUnique_BUILD_MEDIC_20260522_131021_Hecton8Slnx.txt`, `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_131414_TouchedTargets.log` with touched targets at `0 Warning(s)`, `0 Error(s)`.

## 2026-05-22 Active Solution Clean Pass

What was wrong: The active solution still needed proof after first-party warning edits.
What was done: Rebuilt `Hecton8.slnx` twice after edits, including after PlayMode test API repair.
Cinematic Cheats used: none; compile/static proof only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_131511_Hecton8Slnx_Final.log`, `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_132028_Hecton8Slnx_PostTestFix.log`; final result `0 Warning(s)`, `0 Error(s)`.

## 2026-05-22 PlayMode Test Target Repair

What was wrong: `Hecton8.PlayModeTests.csproj` was outside `Hecton8.slnx`; no-restore build had missing restore assets, then restore+build exposed stale test API calls in `H8StaticDataSanityTests`.
What was done: Replaced removed `StaticDataStore.GetRecord<T>()` calls with `FetchRecord<T>()`. Replaced removed `BabelDictionaryStore.GetUtf8()` calls with `TrackUtf8Lookup()` to preserve the existing missing-hash `ERROR` sentinel assertion.
Cinematic Cheats used: none; test compile repair only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/RestoreBuild_BUILD_MEDIC_20260522_131635_PlayModeTests.log`, `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_131744_PlayModeTests_AfterApiFix.log`; final result `0 Warning(s)`, `0 Error(s)`.

## 2026-05-22 Non-Solution Vendor Project Classification

What was wrong: `AIToASE.csproj`, `AmplifyShaderEditor.csproj`, `RealtimeCSG.csproj`, and `TechniePhysicsCreatorEditor.csproj` were not covered by `Hecton8.slnx`. Initial no-restore builds lacked `project.assets.json`; after restore, three vendor/generated csproj targets still referenced missing Unity `6000.3.10f1` source-generator DLLs.
What was done: Restored and built each target to separate first-party code defects from generated vendor/environment defects. `RealtimeCSG.csproj` built clean after restore. `AIToASE.csproj`, `AmplifyShaderEditor.csproj`, and `TechniePhysicsCreatorEditor.csproj` remain `[BLOCKED BY ENVIRONMENT / GENERATED VENDOR PROJECT]`.
Cinematic Cheats used: none; build graph classification only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_131835_NonSlnxVendorTargets.log`, `Docs/AgentLogs/RestoreBuild_BUILD_MEDIC_20260522_131922_NonSlnxVendorTargets.log`.

## 2026-05-22 Build Server Cleanup

What was wrong: Build medic session had started MSBuild and compiler servers.
What was done: Ran `dotnet build-server shutdown` after verification.
Cinematic Cheats used: none.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: command returned successful shutdown for MSBuild and VB/C# compiler servers.

## 2026-05-22 Full Project Continuation

What was wrong: User required the whole project to build without problems. Three generated csproj files still targeted Unity `6000.3.10f1` while project truth is `6000.4.1f1`; current csproj files also contained stale missing generated references. A full rebuild exposed first-party `Hecton8.Core` warnings and vendor/package warnings.
What was done: Synced stale generated Unity csproj files to `6000.4.1f1`, removed missing generated references from all csproj, fixed first-party warning sites, added scoped vendor/generated warning policy in `Directory.Build.targets`, restored tool/archive projects that lacked assets, and rebuilt the full matrix with `--no-restore`.
Cinematic Cheats used: none; build/static repair only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_133401_StaleUnityCsprojFix_Retry.log`, `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_135008_Slnx_WarningZeroRetry.log`, `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_135405_FullProject_FinalZeroWarnings_Retry.log`. Final matrix result: every target exit `0`, `0 Warning(s)`, `0 Error(s)`.

## 2026-05-22 Final Build Server Cleanup

What was wrong: Final full-project proof started MSBuild and compiler servers.
What was done: Ran `dotnet build-server shutdown`.
Cinematic Cheats used: none.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: command returned successful shutdown for MSBuild and VB/C# compiler servers.

## 2026-05-22 Unity Version Follow-Up

What was wrong: User noted the project is on a newer Unity version and old editor references should not be active.
What was done: Verified `ProjectSettings/ProjectVersion.txt` declares `6000.4.1f1`; targeted grep across active build config found no `6000.3.10f1`, `UNITY_6000_3_10`, or `UNITY_6000_3` references. The only residual `Unity.Cecil.Awesome` match is a `Directory.Build.targets` pruner condition for missing generated references.
Cinematic Cheats used: none; build/static audit only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: current project version `6000.4.1f1 (8535861f39e1)`; final build proof remains `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_135405_FullProject_FinalZeroWarnings_Retry.log`.

## 2026-05-22 Dirty Diff Safety Audit

What was wrong: `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` shows a large semantic diff relative to git baseline, beyond the scoped BUILD_MEDIC warning edit.
What was done: Inspected the diff and did not revert it because it overlaps parallel/user work. BUILD_MEDIC-owned change in that file remains the scoped `0649` warning pragma around `VaultDefragmentationJob`.
Cinematic Cheats used: none; ownership audit only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/Tasks/Status_BUILD_MEDIC.md`, `Docs/AgentLogs/Rationale_BUILD_MEDIC.md`.

## 2026-05-22 All Project Standalone Audit

What was wrong: Solution-level proof plus the outside-solution matrix was strong, but not identical to invoking every discovered `.csproj` as its own build target.
What was done: Enumerated `72` `.csproj` files, confirmed `63` are inside `Hecton8.slnx` and `9` are outside, then built all `72` individually with sequential `dotnet build <project> --no-restore -m:1 /nr:false -v:minimal`.
Cinematic Cheats used: none; compile/static proof only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_140332_AllCsprojIndividual.log`, `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_140332_AllCsprojIndividual.csv`. Result: `PROJECTS=72`, `NONCLEAN=0`, `TOTAL_WARNINGS=0`.

## 2026-05-22 Static Config Audit

What was wrong: Old Unity `6000.3.10f1` is still installed in Unity Hub, so active repo config needed separation from machine inventory.
What was done: Searched active non-doc/non-generated files for `6000.3.10f1`, `UNITY_6000_3_10`, and `UNITY_6000_3`; no matches. Re-ran project-relative literal `HintPath`/analyzer existence audit across all `72` `.csproj`; missing path count is `0`. Shut down MSBuild and compiler servers after the audit.
Cinematic Cheats used: none; build/static audit only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/Tasks/Status_BUILD_MEDIC.md`, `Docs/AgentLogs/Rationale_BUILD_MEDIC.md`.

## 2026-05-22 Unity Batchmode Compile Repair

What was wrong: Dotnet green was stale/incomplete evidence. Unity `6000.4.1f1` batchmode exposed Unity-only compile/import defects: editor API drift, generated csproj compile omissions, stale test contracts, generic explicit-layout `TypeLoadException`, false SignalBus layout validator pack failures, and invalid `OnDrawGizmos(SceneView)` script errors.
What was done: Updated current Unity API call sites and test contracts; fixed asmdef/project compile surfaces; changed generic vault handle layout from explicit to sequential declared-size structs; fixed `SignalPayloadLayoutValidator` to detect only source-declared `Pack`; renamed invalid SceneView gizmo callbacks while preserving subscriptions.
Cinematic Cheats used: none; editor/import compile repair only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `Docs/AgentLogs/Unity_BUILD_MEDIC_20260522_155956_BatchmodeCompile_AfterFix13.log`, `Docs/AgentLogs/Unity_BUILD_MEDIC_20260522_160926_BatchmodeCompile_AfterVaultLayoutFix.log`, `Docs/AgentLogs/Unity_BUILD_MEDIC_20260522_161528_BatchmodeCompile_AfterDrawGizmoSignalValidatorFix.log`. Final Unity parser result: `UNIQUE_ERRORS=0`, `UNIQUE_WARNINGS=0`, `PROBLEM_MARKERS=0`.

## 2026-05-22 Post-Unity Active Dotnet Matrix

What was wrong: Unity import can regenerate generated project files and restore assets, so the previous standalone dotnet matrix could no longer be the final proof.
What was done: Ran a recursive brute-force `88`-project pass to locate boundary debt; classified `14` nonclean projects as ignored `.codexbuild`/`.codex-build` scratch, ignored `Library/PackageCache` Unity.Sdk package projects, or archived `Docs/Archive` quarantine projects. Then ran restore+build over the active `72` `.csproj` boundary and restore+build over `Hecton8.slnx`.
Cinematic Cheats used: none; build graph verification only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: brute-force boundary artifact `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_161740_AllCsprojRestoreBuild_AfterUnityClean.log`; active matrix artifact `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_162435_Active72CsprojRestoreBuild_AfterUnityClean.log`; active matrix CSV `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_162435_Active72CsprojRestoreBuild_AfterUnityClean.csv`; solution artifact `Docs/AgentLogs/Build_BUILD_MEDIC_20260522_162853_Hecton8SlnxRestoreBuild_AfterUnityClean.log`. Final active result: `72/72` active projects clean, `NONCLEAN=0`, `0` warning/error lines; solution restore exit `0`, build exit `0`, `0 Warning(s)`, `0 Error(s)`.

## 2026-05-22 Final Active Config Recheck

What was wrong: User specifically challenged stale old-Unity references, and broad grep can falsely match expected `UNITY_6000_3_OR_NEWER` defines in a Unity `6000.4.1f1` project.
What was done: Re-ran exact active config scan for `6000.3.10f1`, `UNITY_6000_3_10`, and exact `UNITY_6000_3`; no matches. Re-ran active project-relative `<HintPath>` and `<Analyzer Include>` audit; `ACTIVE_PROJECTS=72`, `MISSING_REFERENCES=0`.
Cinematic Cheats used: none; static build-config audit only.
Exact Microseconds saved: no runtime profiler proof; no savings claimed.
Evidence: `ProjectSettings/ProjectVersion.txt` is `6000.4.1f1 (8535861f39e1)`; `Docs/Tasks/Status_BUILD_MEDIC.md`; `Docs/AgentLogs/Rationale_BUILD_MEDIC.md`.
