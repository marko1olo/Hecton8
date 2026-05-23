# BUILD_MEDIC Rationale

Date: 2026-05-22
Status: VERIFIED CLEAN UNITY IMPORT + ACTIVE DOTNET MATRIX

## Decision 01: Isolate Build Medic Identity

Problem: User requested broad dotnet build repair, but active `CURRENT_BATCH.md` has no supplied compile-medic XML id and existing SHINOBU status/log files belong to other agents.
Solution: Use `BUILD_MEDIC` for this cross-cutting compile-health session and keep all evidence in dedicated files.
Rejected Alternatives: Reusing `SHINOBU_202`, `SHINOBU_260`, or `SHINOBU_270` would pollute another agent's domain ledger; inventing a fake SHINOBU prompt would violate batch parsing.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; compile repair keeps the project analyzable across all tiers.
Hardware Impact: 0 us runtime on i3/MX350; prevents compile-server contention by honoring single build ownership.

## Decision 02: Build Scope Starts From Current Diagnostics

Problem: The workspace is dirty with many first-party modifications and many deleted archived logs; speculative edits could overwrite parallel agent work.
Solution: Run guarded `dotnet build --no-restore -m:2 /nr:false`, capture diagnostics, then edit only files directly implicated by current errors.
Rejected Alternatives: Full architecture cleanup, reverting dirty worktree, or treating archived green logs as current proof.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; reduces compile-wall depth before any runtime proof can exist.
Hardware Impact: 0 us runtime; build uses `-m:2` to avoid saturating i5-class CPU.

## Decision 03: Expand From Core To Solution After Core Green

Problem: `Hecton8.Core.csproj` is clean, but user requested broad build healing and compile failures can live in generated Unity/editor/third-party project surfaces.
Solution: Run the wider `Hecton8.slnx` target after confirming Core is not the first blocker.
Rejected Alternatives: Stopping after Core green would leave editor and assembly surfaces untested; jumping directly to random source scans would ignore actual compiler evidence.
Scalability potential: Low/Middle/High/Ultra runtime unaffected; wider compile proof increases integration confidence before runtime verification.
Hardware Impact: 0 us runtime; `-m:2` remains capped to reduce CPU contention on i3/MX350-class development machines.

## Decision 04: Fix Only First-Party Warnings In This Pass

Problem: `Hecton8.slnx` produced `136` warnings, but only `3` unique warning sites were first-party; the rest are Unity PackageCache or vendor packages.
Solution: Fix `InputManager` dead private state and `ResidencyStreamingTunerWindow` obsolete editor lookup. Leave vendor/package warnings as classified residual debt.
Rejected Alternatives: Editing `Crest`, `MapMagic`, `GPUInstancer`, `MeshBaker`, `Astar`, URP, or ShaderGraph without package-owner task risks vendor divergence and import regression; blanket `NoWarn` would hide real first-party warnings.
Scalability potential: Low/Middle/High/Ultra runtime unaffected by editor lookup; input manager state removal avoids dead state without adding runtime allocation.
Hardware Impact: 0 us runtime measured; no profiler claim. Compile warning surface reduced by `3` expected solution warnings.

## Decision 05: Repair PlayMode Test API Drift Without Reintroducing Old Read Paths

Problem: `Hecton8.PlayModeTests.csproj` was outside active `Hecton8.slnx`; after restore it failed because `H8StaticDataSanityTests` still called removed APIs `StaticDataStore.GetRecord<T>()` and `BabelDictionaryStore.GetUtf8()`.
Solution: Use current pure read APIs: `FetchRecord<T>()` for static records and `TrackUtf8Lookup()` where the test requires the missing-key `ERROR` sentinel.
Rejected Alternatives: Adding compatibility wrappers would enlarge runtime API surface for a test-only break; using `FetchUtf8()` would silently change the missing-key assertion from `ERROR` to empty string.
Scalability potential: Low/Middle/High/Ultra runtime behavior unchanged; test coverage now tracks the current single-owner static-data read route.
Hardware Impact: 0 us runtime on i3/MX350; no gameplay path changed and no allocation path added.

## Decision 06: Classify Non-Solution Vendor Project Failures As Environment/Generation Debt

Problem: `AIToASE.csproj`, `AmplifyShaderEditor.csproj`, and `TechniePhysicsCreatorEditor.csproj` fail after restore because generated project files point at missing Unity `6000.3.10f1` source-generator DLLs and Unity reference assemblies.
Solution: Record as `[BLOCKED BY ENVIRONMENT / GENERATED VENDOR PROJECT]` and do not edit vendor/generated project references in a first-party compile-medic pass.
Rejected Alternatives: Hardcoding paths to the current local Unity editor would create machine-specific project files; deleting source generator analyzers would mask package/editor integration defects; editing vendor packages without owner mandate risks import regressions.
Scalability potential: Low/Middle/High/Ultra runtime unchanged; keeping vendor project generation authoritative prevents build config drift across devices.
Hardware Impact: 0 us runtime. Development-machine impact is bounded by isolating the blocker instead of forcing repeated failed vendor builds.

## Decision 07: Final Verification Boundary

Problem: User asked for maximum project healing, but CLI `dotnet build` cannot prove Unity domain reload, scene import, Burst compilation, PlayMode execution, or runtime black-box telemetry.
Solution: Treat `Hecton8.slnx` and first-party `Hecton8.PlayModeTests.csproj` compile cleanliness as verified; explicitly leave Unity Editor runtime validation as pending.
Rejected Alternatives: Reporting runtime correctness from compile-only evidence would be false; launching broad Unity automation without a requested editor test target would risk stepping on parallel agents.
Scalability potential: Low/Middle/High/Ultra runtime claims are withheld until Unity profiler/playmode proof exists.
Hardware Impact: 0 us runtime claimed; compile artifacts only.

## Decision 08: Honor New CPU Build Permission

Problem: Earlier batch text forbade dotnet launches above 50% CPU, but user explicitly allowed dotnet at any CPU level during the continuation.
Solution: Drop CPU wait gate for this session while preserving one-dotnet/csc-at-a-time sequencing to avoid corrupt mixed compiler output.
Rejected Alternatives: Continuing to block on CPU would ignore the newest direct order; launching concurrent compiler sessions would make diagnostics ambiguous.
Scalability potential: Runtime unaffected; build feedback cadence improves on loaded developer machines.
Hardware Impact: 0 us runtime. Development impact: less idle wait; still avoids concurrent compiler contention.

## Decision 09: Sync Stale Unity Generated Project Files To 6000.4.1f1

Problem: `AIToASE.csproj`, `AmplifyShaderEditor.csproj`, and `TechniePhysicsCreatorEditor.csproj` carried `6000.3.10f1` Unity version, defines, analyzer paths, and hint paths while `ProjectSettings/ProjectVersion.txt` declares `6000.4.1f1`.
Solution: Mechanically sync generated project version/path/defines to `6000.4.1f1` and remove references to Unity modules absent from the local `6000.4.1f1` install.
Rejected Alternatives: Keeping old `6000.3.10f1` paths breaks source generators; installing/using the old editor contradicts project version; editing vendor source is unnecessary.
Scalability potential: Runtime unaffected; build graph now follows the single project-version owner.
Hardware Impact: 0 us runtime. Development impact: removes repeated failed vendor-target builds.

## Decision 10: Physically Prune Missing Generated References

Problem: Existing `Directory.Build.targets` and `HectonGeneratedProjectReferencePruner` already prune missing generated references at build/generation time, but current csproj files still contained stale `Library/PackageCache`, `Library/ScriptAssemblies`, and `Temp/bin/Debug` references.
Solution: Apply the same pruning doctrine to all current csproj files and verify static path audit returns no missing generated `HintPath` or analyzer entries.
Rejected Alternatives: Relying only on dynamic MSBuild pruning leaves stale text on disk; restoring removed packages such as `com.unity.entities` would change project dependencies.
Scalability potential: Runtime unaffected; reduces build graph noise across low/high developer machines.
Hardware Impact: 0 us runtime. Development impact: fewer reference-resolution probes.

## Decision 11: Treat First-Party Warnings Differently From Vendor Warnings

Problem: A full rebuild exposed first-party warnings in `Hecton8.Core` and vendor warnings in PackageCache/Asset Store projects.
Solution: Fix first-party logic: procedural music branch no longer compile-constant, laser layout validation no longer emits unreachable code, predator cognition job now receives chemical-grid state. For inert compatibility structs, use scoped `0649` suppression at the struct boundary.
Rejected Alternatives: Global first-party `NoWarn` would hide real defects; deleting compatibility data/jobs risks parallel-agent work; editing vendor packages would create package drift.
Scalability potential: Low/Middle/High/Ultra gameplay behavior preserved; predator chemical influence data path is now explicit.
Hardware Impact: 0 us runtime claimed. Predator job now reads intended chemical grid payload instead of default values, which is correctness rather than measured performance.

## Decision 12: Vendor Warning Policy Scope

Problem: Unity/Asset Store packages emitted obsolete API, unassigned field, Unity analyzer, and empty-source warnings under the newer editor.
Solution: Add scoped `NoWarn` only for named vendor/generated projects in `Directory.Build.targets`: `0618`, `0649`, `2008`, and `UNT0006`.
Rejected Alternatives: Editing PackageCache/Asset Store files is brittle; suppressing warnings globally would hide first-party regressions; leaving vendor warnings violates the user's zero-warning build requirement.
Scalability potential: Runtime unaffected; first-party warning surface remains visible.
Hardware Impact: 0 us runtime.

## Decision 13: Final Dotnet Boundary

Problem: User requested the whole project to build perfectly.
Solution: Prove `Hecton8.slnx`, all active non-slnx targets, tools, and deprecated smoke csproj with no restore and no warnings/errors.
Rejected Alternatives: Solution-only proof misses hidden csproj targets; restore-dependent proof hides stale asset state.
Scalability potential: Runtime not proven; build matrix is clean for current workstation state.
Hardware Impact: 0 us runtime; compile-only proof.

## Decision 14: Unity 6000.4.1f1 Is The Active Version Owner

Problem: User flagged that the project is on a newer Unity version and old Unity references should not remain.
Solution: Use `ProjectSettings/ProjectVersion.txt` as the owner (`6000.4.1f1`) and grep active build config for `6000.3.10f1`, `UNITY_6000_3_10`, and `UNITY_6000_3`. Stale generated project references had already been synchronized to `6000.4.1f1`; no active old-editor config remains. The `Unity.Cecil.Awesome` text is retained only as a generated-reference pruning condition.
Rejected Alternatives: Deleting historical log evidence would fake a cleaner repo history; removing the pruner condition would reduce protection against missing generated references.
Scalability potential: Runtime unaffected; build graph now follows the single Unity editor version owner.
Hardware Impact: 0 us runtime. Development impact: avoids source-generator path failures on machines that do not have `6000.3.10f1`.

## Decision 15: Do Not Revert Parallel `GlobalDataVault` Work

Problem: `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs` shows a large semantic diff relative to git baseline, far beyond BUILD_MEDIC's scoped warning suppression.
Solution: Keep the parallel/user changes intact and record that BUILD_MEDIC-owned intent in this file is only the scoped `0649` pragma around `VaultDefragmentationJob`.
Rejected Alternatives: `git checkout --` or manual baseline restoration would violate dirty-worktree ownership and could delete another agent's pointer-free vault migration.
Scalability potential: Runtime claims withheld; current compile proof remains valid for the combined workspace state.
Hardware Impact: 0 us runtime claimed. No performance assertion is made from this diff audit.

## Decision 16: Escalate From Solution Matrix To All-Project Matrix

Problem: User challenged whether the build proof was complete. `Hecton8.slnx` covers `63` of `72` discovered `.csproj`, and the previous final matrix covered the `9` outside-solution projects, but not every project as a standalone build invocation.
Solution: Build all `72` discovered `.csproj` individually, sequentially, with `--no-restore -m:1 /nr:false -v:minimal`, and record a CSV summary.
Rejected Alternatives: Trusting only solution aggregation could miss standalone project asset/reference drift; parallel dotnet builds would make diagnostics ambiguous.
Scalability potential: Runtime unaffected; compile graph confidence is now per-project instead of inferred.
Hardware Impact: 0 us runtime. Development impact: full standalone build matrix reports `NONCLEAN=0`, `TOTAL_WARNINGS=0`.

## Decision 17: Treat Installed Old Unity As Environment, Not Active Config

Problem: Unity Hub has both `6000.3.10f1` and `6000.4.1f1` installed, and stale generated csproj references had previously pointed at the old editor.
Solution: Verify active repo config, not installed editor inventory. Active non-doc/non-generated search for `6000.3.10f1`, `UNITY_6000_3_10`, and `UNITY_6000_3` returns no matches; project-relative literal `HintPath`/analyzer audit returns `0` missing paths.
Rejected Alternatives: Uninstalling `6000.3.10f1` is machine administration, not project repair; deleting historical docs/logs would hide evidence rather than fix active configuration.
Scalability potential: Runtime unaffected; project build truth now routes through Unity `6000.4.1f1`.
Hardware Impact: 0 us runtime. Development impact: avoids old-editor source-generator path failures without modifying Unity Hub installations.

## Decision 18: Dotnet Green Is Not Unity Import Proof

Problem: The dotnet matrix was clean, but Unity batchmode still exposed editor/import-only failures: API drift, missing generated project compile items, invalid `OnDrawGizmos(SceneView)` callbacks, and a generic explicit-layout `TypeLoadException`.
Solution: Promote Unity `6000.4.1f1` batchmode import/script compilation to the required proof after dotnet green and fix only failures visible in fresh Unity logs.
Rejected Alternatives: Reporting dotnet-only green as project-clean would violate evidence law; deleting editor utilities would hide script errors instead of fixing them.
Scalability potential: Low/Middle/High/Ultra runtime claims remain pending; editor/import health is now proven for the active editor.
Hardware Impact: 0 us runtime. Development impact: Unity import no longer stops on compiler/script markers.

## Decision 19: Use Current Unity 6000.4 APIs At Call Sites

Problem: Multiple editor/test files used stale signatures or APIs after the Unity/project code moved forward.
Solution: Update call sites to current APIs and types: current object-find overloads, current telemetry read-only array types, current quality-weight fields, current rollback/netcode DTOs, and writable local copies for `NativeArray<T>` indexer mutation.
Rejected Alternatives: Reintroducing obsolete wrappers would expand API surface for stale tests; broad refactors would collide with parallel agents.
Scalability potential: Runtime behavior unchanged except where tests now target current data contracts; no new hot-path allocation route was added.
Hardware Impact: 0 us runtime; compile/test surface repair only.

## Decision 20: Replace Generic Explicit Layout With Stable Sequential Handles

Problem: `VaultGenerationHandle<T>`, `VaultBufferHandle<T>`, and `VaultSliceHandle<T>` used generic explicit layout and Unity CLR threw `TypeLoadException` during import.
Solution: Convert the generic handle structs to sequential unmanaged layout with declared sizes so field order remains stable and Unity can load the generic structs.
Rejected Alternatives: Reverting broad parallel `GlobalDataVault` work would delete other ownership changes; removing the handles would break vault contracts; ignoring the exception would keep Unity import dirty.
Scalability potential: Low/Middle/High/Ultra runtime authority unchanged; handle layout remains small and fixed for cache use.
Hardware Impact: 0 us runtime measured. Static expectation: no additional heap allocation; no microsecond saving claimed.

## Decision 21: Keep Signal Layout Validator Active While Removing False Pack Positives

Problem: `SignalPayloadLayoutValidator` rejected many explicit-layout payloads because reflection reports a default `Pack` value even when source did not declare `Pack`.
Solution: Detect only source-declared `StructLayoutAttribute.Pack` via `CustomAttributeData` named arguments.
Rejected Alternatives: Disabling the validator would remove a real ARM64/layout gate; hardcoding allowlists would rot as payloads change.
Scalability potential: Low/Middle/High/Ultra layout law remains enforced; false import failures are removed.
Hardware Impact: 0 us runtime; editor validator correctness only.

## Decision 22: Rename SceneView Gizmo Hooks Instead Of Removing Tooling

Problem: Several editor windows had `OnDrawGizmos(SceneView)` methods. Unity treats `OnDrawGizmos` as a magic method and rejects parameters.
Solution: Rename these callbacks to non-magic names such as `DrawSceneGizmos(SceneView sceneView)` while preserving `SceneView.duringSceneGui` subscriptions.
Rejected Alternatives: Deleting gizmo hooks would reduce diagnostics; suppressing script errors is impossible because Unity rejects the signature.
Scalability potential: Runtime unaffected; editor diagnostics remain available.
Hardware Impact: 0 us runtime; editor-only script-error cleanup.

## Decision 23: Separate Active Project Proof From Ignored Scratch, Cache, And Archive Projects

Problem: A brute-force recursive `*.csproj` pass found `88` projects and `14` nonclean, but those failures came from ignored `.codexbuild`/`.codex-build` scratch trees, ignored `Library/PackageCache` Unity.Sdk package projects, and archived Crest quarantine generated projects under `Docs/Archive`.
Solution: Record the brute-force result as boundary evidence, then verify the active build boundary: `72` generated/active project files outside ignored cache/scratch and archive quarantine directories.
Rejected Alternatives: Editing Unity PackageCache generated `Unity.Sdk` projects or archived quarantine projects would create source/vendor drift; pretending the `88` pass was clean would be false.
Scalability potential: Runtime unaffected; active build truth is clearer for low/high developer machines.
Hardware Impact: 0 us runtime. Development impact: avoids chasing ignored scratch projects while still recording them.

## Decision 24: Final Verification Boundary After Unity Regeneration

Problem: Unity import can regenerate project/obj state after dotnet proof, so the previous clean matrix was stale.
Solution: After Unity batchmode green, run restore+build over the active `72` `.csproj` matrix, then restore+build `Hecton8.slnx`, then rerun active old-Unity and missing-reference audits.
Rejected Alternatives: No-restore-only proof after Unity cache mutation could hide missing `project.assets.json`; solution-only proof misses outside-solution targets.
Scalability potential: Runtime claims still require PlayMode/profiler/player proof; compile/import proof is clean for the current workstation.
Hardware Impact: 0 us runtime; compile/import artifacts only.
