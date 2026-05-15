# LOG_INTEGRATION_ASSEMBLY_SURGEON

## 2026-05-15 - No-Dotnet H-Phi Continuation

What was wrong:
- `Directory.Build.props` lacked the Core-only project-reference isolation gate on disk during 2026-05-15 readback.
- `Docs/Tasks/Status_INTEGRATION_ASSEMBLY_SURGEON.md` and `Docs/AgentLogs/Rationale_INTEGRATION_ASSEMBLY_SURGEON.md` still described the older isolated-Core state and had no final log file.
- Generated `Hecton8.Core.csproj` still lists package/vendor project references: URP, GPUInstancer, Crest, WaveHarmonic, EasySave3, VolumetricLightBeam, input projects, and contracts.
- `Assets/_Project/Scripts/Hecton8.Core.asmdef` still references broad leaf/domain assemblies. This is H-Phi debt, but current Core-owned source still uses leaf types, so blind deletion would break the compile lane.

What was done:
- Hardened the source-backed `Directory.Build.props` gate for `Hecton8.Core`: default `BuildProjectReferences=false` and `BuildInParallel=false` unless `HectonBuildProjectReferences=true`.
- Rechecked `Directory.Build.targets`: current bridge surface has no `Hecton8.Animation.IK` reference.
- Rechecked current batch: `Docs/Tasks/CURRENT_BATCH.md` no longer contains `INTEGRATION_ASSEMBLY_SURGEON` or a polish tag, so no neighboring prompt was parsed.
- Audited Core asmdef references and recorded the remaining H-Phi debt instead of hiding it.
- Updated status and rationale with the no-dotnet evidence boundary.

Cinematic cheats used:
- Build-graph isolation instead of vendor/package source repair.
- No runtime simulation, rendering, physics, audio, NativeContainer, or gameplay path was changed.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh 2026-05-15 build-time savings: not claimed because the user forbade dotnet rebuilds.
- Historical artifact only: `Build_INTEGRATION_ASSEMBLY_SURGEON_05_RawCoreDefault.log` from 2026-05-14 reports 31,300,000 us, 0 warnings, 0 errors after the same Core default isolation pattern.

Verification:
- Evidence class for this continuation: STATIC_SOURCE and STATIC_DOC only.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run during this continuation.
- `Directory.Build.props` now contains the Core `BuildProjectReferences=false` and `BuildInParallel=false` defaults with `HectonBuildProjectReferences=true` opt-in.
- Owned-file static poison scan found no `foreach`, `string.Format`, interpolation, `.ToString(`, `math.sqrt`, `math.normalize`, managed collection construction, `Task.Run`, Addressables instantiate, or unload calls in `Directory.Build.props`.
- `git diff --check` on owned files reported no whitespace errors, only repository CRLF normalization warnings for the owned markdown files.

Residual risk:
- Fresh compile is PENDING VERIFICATION by user no-dotnet order.
- Unity Editor import, Unity Console, Play Mode, profiler, GCMonitor, and player build were not run.
- Full H-Phi asmdef cleanup requires staged contract extraction for `LeviathanTerrainIkJob`, `MacroSwarm`, `SoundEmissionSignal`, and other cross-domain DTOs before leaf references can be safely removed from `Hecton8.Core.asmdef`.

## 2026-05-15 - H-Phi Core Graph Audit Tooling

What was wrong:
- Core graph H-Phi debt was measurable only through ad hoc manual scans.
- Manual scans are brittle in this project because `Hecton8.Core.asmdef`, generated `.csproj` files, and `Directory.Build.props` can churn independently.

What was done:
- Extended `Tools/Architecture/HectonPhiAudit.ps1` with `-CoreGraphOnly`.
- The new mode classifies `Hecton8.Core.asmdef` references by CoreFamily, MathNative, Contract, LeafDomain, PackageOrUnity, and Other.
- The new mode classifies generated `Hecton8.Core.csproj` project references by ContractOrCore, FirstPartyLeaf, and PackageOrGenerated.
- The new mode reports whether `Directory.Build.props` has the Core `BuildProjectReferences=false` and `BuildInParallel=false` gate.

Cinematic cheats used:
- Static graph audit instead of compile or Unity import.
- No runtime code changed.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh build-time savings: not claimed because dotnet is forbidden in this continuation.
- Developer iteration value: static graph-only audit completed and avoids full source scan pressure when the Integrator only needs Core dependency debt.

Verification:
- Command run: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly`.
- Evidence class: STATIC_SOURCE.
- Result: Core build gate present; 46 Core asmdef refs; 28 Core asmdef H-Phi debt refs; 12 generated Core project refs; 10 generated Core project debt refs.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run.

Residual risk:
- This does not prove compilation or Unity import.
- It exposes dependency debt; it does not remove leaf references because DTO extraction needs a compile-enabled integration pass.

## 2026-05-15 - H-Phi Core Graph Budget Gate

What was wrong:
- Graph debt counts were visible but not enforceable. A future agent could add one more Core-to-leaf reference and the audit would still exit clean unless someone read the report.

What was done:
- Added `-RequireCoreBuildGate` to fail when the Core `BuildProjectReferences=false` / `BuildInParallel=false` guard is incomplete.
- Added `-MaxCoreAsmdefDebtReferences` to cap Core asmdef H-Phi debt.
- Added `-MaxGeneratedProjectDebtReferences` to cap generated Core project-reference H-Phi debt.
- Exercised the gate against the current static baseline: 28 Core asmdef debt refs and 10 generated project debt refs.
- Reworked the failure path to throw one aggregated budget exception instead of stopping on the first `Write-Error`.

Cinematic cheats used:
- No-regression budget gate instead of unsafe leaf-reference deletion.
- Static source audit instead of compile.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh build-time savings: not claimed.

Verification:
- Command run: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 28 -MaxGeneratedProjectDebtReferences 10`.
- Failure-path command run under catch: `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -MaxCoreAsmdefDebtReferences 0`.
- Evidence class: STATIC_SOURCE.
- Result: exit code 0; Core build gate present; debt counts at baseline.
- Failure-path result: `EXPECTED_BUDGET_FAIL_PATH_OK`.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run.

Residual risk:
- Budget values are baseline caps, not architectural approval. They prevent regression, but reducing the counts still requires staged contract extraction and fresh compile validation.

## 2026-05-15 - H-Phi Metric Documentation

What was wrong:
- H-Phi was implemented in `Tools/Architecture/HectonPhiAudit.ps1` and described across dated report addenda, but no stable architecture document defined the metric contract.
- Without a stable doc, future agents could confuse static H-Phi with compile, profiler, GC, player-build, or visual-quality proof.

What was done:
- Added `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`.
- Documented what H-Phi measures: coupling, tick discipline, DataVault/native ownership visibility, struct layout discipline, and Core graph debt.
- Documented exact formulas for `NarrowIntegration`, `RiskIntegration`, `ArchitecturalPurity`, `ArchitecturalPurityExpanded`, `DataSovereignty`, `MemoryAlignment`, `BinarySafeRatio`, `HPhiStaticNarrow`, and `HPhiStaticRisk`.
- Documented Core graph classification and debt rules for `Hecton8.Core.asmdef`, generated `Hecton8.Core.csproj`, and the `Directory.Build.props` Core build gate.
- Linked the metric contract from `Docs/ARCHITECTURE/README.md` and `Docs/README.md`.

Cinematic cheats used:
- Stable metric documentation instead of cross-domain code churn.
- No runtime simulation, rendering, physics, audio, NativeContainer, scene lookup, or gameplay path changed.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh build-time savings: not claimed.
- Documentation impact: prevents unsafe metric-chasing edits; no measured runtime delta.

Verification:
- Evidence class: STATIC_DOC plus STATIC_SOURCE for the referenced tool model.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run.

Residual risk:
- H-Phi remains static architecture evidence only.
- Runtime H-Phi quality, Unity Console, Play Mode, profiler, GCMonitor, player build, and visual quality remain pending until those evidence lanes are explicitly run.

## 2026-05-15 - H-Phi Documentation Verification

What was wrong:
- The new H-Phi metric contract needed static verification after indexing.

What was done:
- Ran `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 28 -MaxGeneratedProjectDebtReferences 10`.
- Ran an anchor scan for `HECTON_PHI_STATIC_METRIC`, `HPhiStaticNarrow`, `Core graph budget`, and static H-Phi evidence language.
- Ran `git diff --check` on the H-Phi tooling/docs/status/log file set.
- Ran an owned ASCII scan and separately located the pre-existing non-ASCII line in `Docs/README.md`.

Cinematic cheats used:
- Static verification only. No compile, Unity import, profiler, or runtime lane was touched.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh build-time savings: not claimed by no-dotnet order.

Verification:
- Core graph gate passed at 46 Core asmdef refs, 28 Core asmdef H-Phi debt refs, 12 generated Core project refs, and 10 generated project debt refs.
- Anchor scan found the stable doc link in both architecture and root docs indexes.
- `git diff --check` reported no whitespace errors; only LF/CRLF normalization warnings.
- Owned ASCII scan passed. `Docs/README.md` still contains pre-existing mojibake at line 17; this pass did not introduce it.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run.

Residual risk:
- Fresh compile remains pending by explicit user order.
- The doc does not reduce existing Core graph debt; it defines the metric and the evidence boundary for reducing it later.

## 2026-05-15 - Core Asmdef H-Phi Debt Prune

What was wrong:
- `Hecton8.Core.asmdef` still referenced three assemblies that static scans did not find in generated Core compile items: `Hecton8.Input.Generated`, `Hecton8.World.GPR`, and `Hecton8.SpaceEngine098Terrain`.
- Those references kept Core asmdef H-Phi debt at 28 even though the referenced leaf systems are owned by Input, World/GPR, and SpaceEngine/MapMagic paths.

What was done:
- Removed the three unused references from `Assets/_Project/Scripts/Hecton8.Core.asmdef`.
- Left `Hecton8.Input`, `Hecton8.World.Terrain`, and all references with live Core evidence untouched.
- Updated the stable H-Phi metric doc budget example from 28 to 25 asmdef debt refs.

Cinematic cheats used:
- Static compile-graph reduction instead of DTO migration or generated project edits.
- No runtime code, physics, rendering, audio, input, or gameplay path was changed.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh build-time savings: not claimed because no compile lane was run.
- Static H-Phi graph debt: Core asmdef debt refs reduced from 28 to 25.

Verification:
- Evidence class: STATIC_SOURCE.
- Generated Core compile-item filter returned no `SpaceEngine098`, `GroundPenetratingRadar`, `HectonInputActions`, `Assets\_Project\Input`, `World\GPR`, `World\SpaceEngine098`, or `Input\InputManager` entries.
- Type-name scan found no generated Core compile-item use of GPR or SpaceEngine098 public types.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10` passed.
- Result: 43 Core asmdef refs, 25 Core asmdef H-Phi debt refs, 12 generated Core project refs, 10 generated project debt refs.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run.

Residual risk:
- Fresh compile remains pending by explicit user order.
- Generated `Hecton8.Core.csproj` still has stale project references until Unity/project-generation evidence is refreshed.

## 2026-05-15 - Source-Backed Core Bridge Debt Gate

What was wrong:
- `Directory.Build.targets` still injected `Hecton8.World.GPR` and `Hecton8.SpaceEngine098Terrain` references into the Core bridge after the asmdef cleanup.
- `HectonPhiAudit.ps1` did not count source-backed bridge references, so bridge debt could bypass the visible Core graph budget.

What was done:
- Removed the two unused bridge reference blocks from `Directory.Build.targets`.
- Extended `Tools/Architecture/HectonPhiAudit.ps1` to report source-backed Core bridge refs and bridge H-Phi debt refs.
- Added `-MaxSourceBackedBridgeDebtReferences` to the same budget-gate path.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with bridge-debt definition and the new budget command.

Cinematic cheats used:
- Source-backed graph cleanup and static budget gate instead of generated `.csproj` editing or broad contract migration.

Exact microseconds saved:
- Runtime frame time: 0 us changed.
- Fresh build-time savings: not claimed because no compile lane was run.
- Static Core bridge debt after cleanup: 19 source-backed bridge refs, 8 bridge debt refs.

Verification:
- Evidence class: STATIC_SOURCE + STATIC_DOC.
- `Directory.Build.targets` scan found no remaining `Hecton8.World.GPR`, `Hecton8.SpaceEngine098Terrain`, or `Hecton8.Input.Generated` bridge references.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 8` passed.
- Expected bridge-budget failure path returned `EXPECTED_BRIDGE_BUDGET_FAIL_PATH_OK` under zero bridge-debt budget.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run.

Residual risk:
- Static bridge pruning still needs compile confirmation once the no-dotnet order is lifted.
- Generated project-reference debt remains at 10 until Unity project generation/compile evidence is refreshed.

Final static verification addendum:
- `Hecton8.Core.asmdef` JSON parse: OK.
- `Directory.Build.targets` XML parse: OK.
- Removed-reference scan: `REMOVED_CORE_BRIDGE_REFS_ABSENT`.
- Expected asmdef budget failure: `EXPECTED_ASMDEF_BUDGET_FAIL_PATH_OK`.
- Expected bridge budget failure: `EXPECTED_BRIDGE_BUDGET_FAIL_PATH_OK`.
- `git diff --check` on touched files: no whitespace errors.
- Owned ASCII scan: passed.

## 2026-05-15 - Fresh XML Compile Closure

Triage Record:
- Errors Fixed: 79 filtered compiler entries from `Fresh01` reduced to 0 in `Fresh12`.
- Files Moved: none.
- ASMDEFs Repaired: none in this closure; 152 asmdefs scanned into `AsmdefList_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh.txt`.
- Current Status: BUILD SUCCESSFUL / PLATINUM GRADE.

What was wrong:
- Core isolated build could not see current memory/save/source contracts and later hit generated project-output metadata leaks.
- Save master hash used the correct algorithm family but missed the Unity Collections namespace.
- `PDAShellChrome` called inventory signal binding/consumption methods that did not exist.
- A blank `Fresh10` build artifact produced exit `-1`; it was discarded as invalid evidence.

What was done:
- Re-ran the mandated Core build wall and fixed compile blockers in batches until `Hecton8.Core.dll` emitted.
- Verified generated project references are suppressed in the Core medic lane and Unity-built assemblies are resolved from `Library/ScriptAssemblies`.
- Added `Unity.Collections` import for `SaveMasterHashV10`.
- Added allocation-free PDA inventory signal binding and `SignalBus<InventoryChangedSignal>` consumption.
- Captured final proof in `Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh12.log`.

Cinematic Cheats used:
- Compile-graph surgery only. No runtime physical simulation was added.
- Source-backed DLL resolution was used instead of rebuilding every vendor/package project into `Temp/bin/Debug`.

Exact Microseconds saved:
- Runtime frame time saved: 0 us. This pass repaired compile connective tissue, not player-frame logic.
- Final build verification time: 105,610,000 us.
- Avoided vendor/project rebuild savings: not claimed; no controlled timing baseline was collected.

Verification:
- Command: `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`.
- Result: `Build succeeded. 0 Warning(s). 0 Error(s).`
- Output: `Temp/bin/Debug/Hecton8.Core.dll`.
- `git diff --check` on owned compile-fix files: no whitespace errors; only CRLF normalization warnings.

Residual risk:
- Other concurrent agents have unrelated working-tree edits. They were not reverted or claimed.
- This is CLI Core compile proof only; Unity editor import/playmode proof remains outside this task.

## 2026-05-15 - Post-Green H-Phi Static Hardening

What was wrong:
- Core asmdef debt pruning still depended on manual grep evidence instead of a repeatable candidate scan.
- `Directory.Build.targets` had a second Core lane for project-reference replacement refs. The old bridge budget of 8 only described compile-bridge debt and did not expose 12 replacement debt refs.
- The bridge counter included a `<Reference Remove=...>` node as a reference edge.
- `Hecton8.Input.Generated` had reappeared in the project-reference replacement lane even though Core source does not use `HectonInputActions`.

What was done:
- Added `-IncludeUnusedCoreReferenceScan` to `Tools/Architecture/HectonPhiAudit.ps1`.
- Added `BridgeLane` labels to bridge rows: `CoreCompileBridge` and `ProjectReferenceReplacement`.
- Corrected bridge counting to count only `Reference Include` edges.
- Added `-MaxSourceBackedCompileBridgeDebtReferences` and `-MaxProjectReferenceReplacementDebtReferences`.
- Removed the stale `Hecton8.Input.Generated` replacement reference from `Directory.Build.targets`.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with bridge lanes, the optional candidate scan, and the honest current budget command.

Cinematic Cheats used:
- Static graph surgery and no-regression gates only.
- No runtime physical simulation, gameplay loop, rendering path, input path, or package source was changed.

Exact Microseconds saved:
- Runtime frame time saved: 0 us.
- Build time saved: not claimed in this post-green pass because no dotnet command was run.
- Static debt changed: replacement bridge debt reduced by 1 (`Hecton8.Input.Generated` removed).
- Current graph baseline: 43 Core asmdef refs / 25 asmdef debt refs; 12 generated project refs / 10 generated-project debt refs; 31 source-backed bridge refs / 20 total bridge debt refs; 8 compile-bridge debt refs; 12 project-reference replacement debt refs.

Verification:
- Evidence class: STATIC_SOURCE + STATIC_DOC.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was run in this post-green pass.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -IncludeUnusedCoreReferenceScan -Summary -Json`: 1065 Core compile-surface files, 25 scanned asmdef debt refs, `CandidateCount=0`.
- Budget pass: `-MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 20 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 12`.
- Expected failures passed for asmdef 24, total bridge 19, compile-bridge 7, and replacement 11.
- `Directory.Build.targets` XML parse: OK.
- `Hecton8.Input.Generated` is absent from `Hecton8.Core.asmdef` and `Directory.Build.targets`.

Residual risk:
- This pass is not new compile proof. It was later superseded by `Fresh19` as current disk compile evidence.
- Remaining Core asmdef debt has static external hits; reducing it requires staged contract extraction and a compile-enabled lane.

## 2026-05-15 - Concurrent Edit Revalidation Addendum

Triage Record:
- Errors Fixed: current-disk wall from `Fresh14`/`Fresh18`/`Fresh17` reduced to 0 in `Fresh19`.
- Files Moved: none.
- ASMDEFs Repaired: none.
- Current Status: BUILD SUCCESSFUL / PLATINUM GRADE.

What was wrong:
- `Fresh12` was invalidated as final evidence by later concurrent source edits.
- Audio scalability listener work briefly produced a duplicate `OnScalabilityChanged`; the richer concurrent implementation had to be preserved.
- Save writer call sites passed bounded counts, but matching counted array writer overloads were missing.

What was done:
- Revalidated current disk state with fresh no-restore Core builds.
- Removed the duplicate thin audio scalability listener and kept the fuller implementation.
- Added no-allocation paired count clamp and counted custom-array writer overloads in `SaveBinaryPayloadCodec`.
- Captured final proof in `Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh19.log`.

Cinematic Cheats used:
- None. Compile-contract repair only.

Exact Microseconds saved:
- Runtime frame time saved: 0 us.
- Final build verification time: 81,710,000 us.

Verification:
- Command: `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`.
- Result: `Build succeeded. 0 Warning(s). 0 Error(s).`
- Output: `Temp/bin/Debug/Hecton8.Core.dll`.

## 2026-05-15 - Replacement Bridge H-Phi Debt Reduction

What was wrong:
- The Core project-reference replacement lane still carried four zero-hit references: `EasySave3`, `Unity.RenderPipelines.Core.Editor`, `Unity.ShaderGraph.Editor`, and `WaveHarmonic.Crest.Shared.Editor`.
- `HectonPhiAudit.ps1` had a PowerShell parse error in the `TopAupPrecisionRiskFiles` sort expression, blocking the static H-Phi gate.
- The stable H-Phi metric doc still needed the lowered bridge budget values after this prune.

What was done:
- Reconstructed the generated/source-backed Core compile surface: 1065 files.
- Confirmed zero exact static hits for the four removed replacement references.
- Removed those four reference blocks from `Directory.Build.targets`.
- Kept replacement refs with live source hits: `Crest`, `GPUInstancer`, `Hecton8.Input`, `ShapesRuntime`, `Unity.RenderPipelines.Universal.Runtime`, `VolumetricLightBeam`, `WaveHarmonic.Crest`, and `WaveHarmonic.Crest.Shared`.
- Fixed the PowerShell `Sort-Object -Property` syntax in `Tools/Architecture/HectonPhiAudit.ps1`.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with the lowered bridge budgets.

Cinematic Cheats used:
- Static graph debt surgery only.
- No runtime physical simulation, rendering algorithm, package source, or gameplay path changed.

Exact Microseconds saved:
- Runtime frame time saved: 0 us.
- Build time saved: not claimed because no dotnet command was run in this pass.
- Static graph impact: total source-backed bridge debt reduced from 20 to 16; project-reference replacement debt reduced from 12 to 8.

Verification:
- Evidence class: STATIC_SOURCE + STATIC_DOC.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` command was run.
- `Tools/Architecture/HectonPhiAudit.ps1 -CoreGraphOnly -Json -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 25 -MaxGeneratedProjectDebtReferences 10 -MaxSourceBackedBridgeDebtReferences 16 -MaxSourceBackedCompileBridgeDebtReferences 8 -MaxProjectReferenceReplacementDebtReferences 8` passed.
- Current counts: 43 Core asmdef refs, 25 asmdef debt refs, 12 generated project refs, 10 generated-project debt refs, 27 source-backed bridge refs, 16 total bridge debt refs, 8 compile-bridge debt refs, 8 replacement debt refs.
- `PS_PARSE_OK` for `HectonPhiAudit.ps1`.
- `Directory.Build.targets` XML parse: OK.
- Removed-reference scan: `REMOVED_DEBT_REFS_ABSENT`.
- Full static AUP gate: `AupPrecisionSafe=363`, `AupPrecisionRisk=0` under `-MaxAupPrecisionRisk 0`.
- Expected failure paths: `EXPECTED_TOTAL_BRIDGE_BUDGET_FAIL_PATH_OK` at total bridge budget 15 and `EXPECTED_REPLACEMENT_BRIDGE_BUDGET_FAIL_PATH_OK` at replacement budget 7.

Residual risk:
- This is not fresh compile proof. `Fresh19` remains the latest compile artifact on disk.
- Generated `Hecton8.Core.csproj` still lists stale project references until Unity regenerates project files or a compile-enabled lane validates generated-project cleanup.

## 2026-05-15 - Fresh25 Bottom Addendum

What was wrong:
- Earlier log sections are historical. The current compile authority is `Fresh25`, not Fresh19 and not any static-only pass.

What was done:
- Revalidated the current disk after concurrent edits.
- Accepted `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh25.log` as the latest compile artifact.
- Accepted `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_FinalBudgetPass2.json` as the latest static H-Phi artifact.

Cinematic Cheats used:
- No runtime simulation changes.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final build verification time: 117,770,000 us.
- Final H-Phi verification time: 296,100,000 us.

Verification:
- `Hecton8.Core.csproj --no-restore`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `EXIT=0`.
- H-Phi static gate: `EXIT=0`, `AupPrecisionRisk=0`, `DuplicateSignalNameCount=6`, `DuplicateSignalDeclarationCount=12`.
- `git diff --check`: exit 0 with CRLF normalization warnings only.

Current Status:
- BUILD SUCCESSFUL / FRESH25 CURRENT-DISK GREEN.

## 2026-05-15 - Fresh48 Bottom Authority Addendum

What was wrong:
- The previous bottom addendum was stale. Current authority is no longer Fresh25.
- Fresh47 was rejected as final compile proof because a C# source timestamp was newer than its build log.

What was done:
- Accepted `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh48.log` as the current compile artifact.
- Accepted `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh48.json` as the current static H-Phi artifact.

Cinematic Cheats used:
- No runtime simulation changes.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final build verification time: 34,270,000 us.
- Final H-Phi verification time: 55,700,000 us.

Verification:
- `Hecton8.Core.csproj --no-restore`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `EXIT=0`.
- H-Phi static gate: `EXIT=0`, `RuntimeHPhiRisk=0.000597081`, `GlobalRegistrySurface=5146`, `UnityUpdateMethods=0`, duplicate signal budget actual `0`, `AupPrecisionRisk=0`.
- Freshness: no C# source under `Assets/_Project/Scripts` was newer than the Fresh48 build or H-Phi artifacts at verification time.

Current Status:
- BUILD SUCCESSFUL / PLATINUM GRADE / FRESH48 CURRENT-DISK GREEN.

## 2026-05-15 - Fresh48 Current-Disk Closure

What was wrong:
- Earlier green compile/H-Phi artifacts were stale under parallel edits.
- `Fresh39` exposed a Voxel compile drift around unsafe cell-size usage.
- `Fresh46` compiled cleanly but the documented H-Phi floor failed by `0.000000116`; `Fresh47` then compiled cleanly but was rejected after Voxel source mtime proved newer than the build log.

What was done:
- Accepted the current Voxel record-bound validation on disk and recompiled `Hecton8.Core.csproj` from the current source as `Fresh48`.
- Replaced redundant audio dispatcher registration probes in `DeepPsychosisController` and `HectonMusicDirector` with the established `GlobalRegistry.TryRegisterUpdatable` and `TryRegisterSlowTickable` APIs.
- Preserved dispatcher readiness guards and hot-swap/cached-service behavior.
- Reran the full documented H-Phi gate without lowering any threshold.

Cinematic Cheats used:
- No runtime simulation or gameplay feature was added.
- Static coupling debt was reduced by removing real registry bucket probes instead of changing audio behavior.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Compile verification time: 34,270,000 us (`Fresh48`).
- H-Phi verification time: 55,700,000 us.
- Static graph impact: `GlobalRegistrySurface=5146`, `RuntimeHPhiRisk=0.000597081`, documented floor `0.000597000`.

Verification:
- `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh48.log`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `EXIT=0`.
- `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh48.exit.txt`: `EXIT=0`.
- H-Phi: `UnityUpdateMethods=0`, duplicate signal budget actual `0`, `AupPrecisionRisk=0`, `ArchitecturalPurity=1`.
- No C# source under `Assets/_Project/Scripts` was newer than the Fresh48 build or H-Phi artifacts at verification time.
- `git diff --check` on touched files returned exit 0 with CRLF normalization warnings only.

Errors Fixed:
- 1 current compile wall item closed in this continuation: Voxel cell-size/record-bound compile drift that appeared in `Fresh39`.
- 1 H-Phi gate failure closed: `RuntimeHPhiRisk` restored above the documented floor.

Files Moved:
- None.

ASMDEFs Repaired:
- None.

Current Status:
- BUILD SUCCESSFUL / PLATINUM GRADE / FRESH48 CURRENT-DISK GREEN.

## 2026-05-15 - Unity Loop Debt Zero H-Phi Correction

What was wrong:
- H-Phi still counted two Unity loop methods as runtime debt after gameplay loop debt was gone.
- Exact source scan showed both raw methods are the bounded `Core/SystemDispatcher.cs` Unity shell: `Update()` and `LateUpdate()`.
- The audit had no enforceable `-MaxUnityUpdateMethods 0` budget row, so future gameplay loops could regress without a dedicated gate.

What was done:
- Completed `Tools/Architecture/HectonPhiAudit.ps1` enforcement for `-MaxUnityUpdateMethods`.
- Kept raw transparency with `UnityUpdateMethodsRaw` and `UnityLoopShellMethods`, while `UnityUpdateMethods` now means non-exempt runtime debt.
- Added JSON budget reporting for the Unity update-method gate.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` to define the raw/shell/debt counters and the hard zero loop-debt gate.
- Tightened the documented full H-Phi risk floor from `0.000590000` to `0.000597000`.

Cinematic Cheats used:
- Static metric correction only.
- No physics, rendering, AI, gameplay, prefab, package, generated project, or runtime simulation path changed.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final tightened H-Phi verification time: 154,600,000 us.
- Static metric impact: `UnityUpdateMethods=0`, `UnityUpdateMethodsRaw=2`, `UnityLoopShellMethods=2`, `ArchitecturalPurity=1`, `RuntimeHPhiRisk=0.000597474`.

Verification:
- Evidence class: STATIC_SOURCE + STATIC_DOC.
- H-Phi artifact: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_UnityLoopDebtZero.json`.
- Full static regression gate: `EXIT=0`, `AupPrecisionRisk=0`, `DuplicateSignalNameCount=0`, `UnityUpdateMethods=0`, `RuntimeHPhiRisk=0.000597474`, Core asmdef debt `25`, total bridge debt `14`.
- Raw Unity loop scan: only `Assets/_Project/Scripts/Core/SystemDispatcher.cs:1663` and `:1835`.
- PowerShell parser: `PS_PARSE_OK`.
- Source-budget misuse path: `EXPECTED_UNITY_UPDATE_COREGRAPH_REJECT_OK`.
- `git diff --check` on touched script/doc files: exit 0 with CRLF normalization warnings only.

Residual risk:
- No `dotnet build`, `dotnet rebuild`, Unity import, Play Mode, profiler, GCMonitor, or player build was run in this pass.
- Fresh compile proof remains `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh27.log`.

Current Status:
- BUILD SUCCESSFUL / FRESH27 CURRENT-DISK GREEN.
- Latest static H-Phi gate: UNITY LOOP DEBT ZERO.

## 2026-05-15 - Duplicate Signal Name Zero Closure

What was wrong:
- H-Phi exposed six duplicate `*Signal` struct names across first-party source.
- Fresh26 proved the macro-database contract rename alone was unsafe for the CLI Core lane because the current `Hecton8.Core.Contracts` DLL still exposes `SectorHydratedSignal`.

What was done:
- Renamed world culling camera payloads to `InstanceCullingCameraPositionSignal` and `InstanceCullingCameraFrustumSignal`.
- Renamed the gameplay combat queue payload to `CombatDamageRequest`.
- Renamed the habitat callback damage payload to `HabitatDamageSignal`.
- Renamed the player interaction stress payload to `PlayerInteractionStressSignal`.
- Preserved the macro-database contract payload name for DLL compatibility and renamed the Core signal-bus payload to `MacroDatabaseSectorHydrationSignal`.
- Updated the H-Phi metric documentation with the duplicate-zero baseline and evidence boundary.

Cinematic Cheats used:
- No runtime simulation, rendering, physics, or visual algorithm changed.
- This was contract-name and compile-lane surgery only.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Fresh26 failed build wall read: 35,030,000 us.
- Fresh27 final build verification time: 117,430,000 us.
- Full H-Phi duplicate-zero verification time: 190,100,000 us.

Verification:
- CLI_COMPILE artifact: `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh27.log`.
- Command: `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`.
- Result: `Build succeeded. 0 Warning(s). 0 Error(s). EXIT=0`.
- H-Phi artifact: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_DuplicateZero.json`.
- H-Phi result: `EXIT=0`, `DuplicateSignalNameCount=0`, `AupPrecisionRisk=0`, Core asmdef debt `25`, generated-project debt `10`, total bridge debt `14`, compile-bridge debt `8`, replacement debt `6`.

Residual risk:
- Unity Editor import, Play Mode, profiler, GCMonitor, player build, and runtime visuals were not run.
- Public type renames were validated by CLI compile only. External branches or serialized references outside the current source tree still need Unity/project-wide owner review.

Current Status:
- BUILD SUCCESSFUL / FRESH27 CURRENT-DISK GREEN.

## 2026-05-15 - Fresh25 Compile And H-Phi Continuation

What was wrong:
- Current disk had moved past the old Fresh19/Fresh24 evidence because other agents continued editing C#.
- `SargassumMicroFaunaBoids.PublishPredatorKillDebris` was static while reading `_abyssalFluidDecals`, producing CS0120.
- `SpatialAudioManager` had hot-swap calls without the helper implementations, producing CS0103; then a concurrent duplicate helper set risked CS0111.
- `OculusFfrEnforcer` still had a private `Update()` fallback outside the dispatcher exception list.
- H-Phi did not expose duplicate `*Signal` struct-name debt as a budgetable metric.

What was done:
- Converted `PublishPredatorKillDebris` to an instance method, matching the existing instance caller and field ownership.
- Completed `SpatialAudioManager` cold service caching plus `GlobalRegistry` hot-swap listener registration/unregistration.
- Removed the duplicate short helper set from `SpatialAudioManager`.
- Moved `OculusFfrEnforcer` off its fallback `Update()` and onto `IGlobalRegistryHotSwapListener` dispatcher registration.
- Added duplicate signal-name scanning and `-MaxDuplicateSignalNames` to `Tools/Architecture/HectonPhiAudit.ps1`.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with the duplicate signal-name gate.

Cinematic Cheats used:
- No physical simulation or visual runtime algorithm changed.
- Compile surgery and static graph tooling only; public signal renames were rejected during the active batch.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final Core compile verification time: 117,770,000 us.
- Final H-Phi full-source budget verification time: 296,100,000 us.

Verification:
- CLI_COMPILE artifact: `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh25.log`.
- Command: `dotnet build Hecton8.Core.csproj --no-restore --disable-build-servers -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal`.
- Result: `Build succeeded. 0 Warning(s). 0 Error(s). EXIT=0`.
- H-Phi artifact: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_FinalBudgetPass2.json`.
- H-Phi result: `EXIT=0`, `AupPrecisionRisk=0`, `DuplicateSignalNameCount=6`, `DuplicateSignalDeclarationCount=12`, `UnityUpdateMethods=2`.
- `rg` scan: `OculusFfrEnforcer.cs` has no `private void Update()`; only `SystemDispatcher.Update()` remains in the bounded scan.
- `git diff --check`: exit 0; CRLF normalization warnings only.

Residual risk:
- Unity Editor import, Play Mode, profiler, GCMonitor, player build, and runtime visuals were not run.
- Duplicate signal-name debt is now visible and budgeted, not fully eliminated. Public contract renames require staged wrappers under the interface immutability rule.

## 2026-05-15 - Replacement Bridge Exact-Use Correction

What was wrong:
- The previous replacement-lane scan kept `ShapesRuntime` and `VolumetricLightBeam` from broad text hits.
- Exact package-surface usage did not exist in `Assets/_Project/Scripts`; the hits were generic words/comments or first-party custom volumetric renderer names.

What was done:
- Removed `ShapesRuntime` and `VolumetricLightBeam` from the Core project-reference replacement lane in `Directory.Build.targets`.
- Updated `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with the new Core graph budget command.
- Updated status/rationale with the stricter evidence boundary and new debt counts.

Cinematic Cheats used:
- Static graph debt surgery only.
- No runtime simulation, rendering algorithm, gameplay path, package source, prefab, or generated `.csproj` was edited.

Exact Microseconds saved:
- Runtime frame time saved: 0 us.
- Build time saved: not claimed because no dotnet command was run in this pass.
- Static graph impact: source-backed bridge refs reduced from 27 to 25, total bridge debt from 16 to 14, and project-reference replacement debt from 8 to 6.

Verification:
- Evidence class: STATIC_SOURCE + STATIC_DOC.
- No `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` command was run.
- Exact source scan: `NO_EXACT_SHAPES_OR_VLB_CORE_SOURCE_HITS`.
- Core graph budget pass: 43 Core asmdef refs, 25 asmdef debt refs, 12 generated project refs, 10 generated-project debt refs, 25 source-backed bridge refs, 14 total bridge debt refs, 8 compile-bridge debt refs, 6 replacement debt refs.
- Parse pass: `STATIC_PARSE_OK`.
- Removed-reference scan: `REMOVED_REPLACEMENT_REFS_ABSENT`.
- Full static regression gate: `AupPrecisionSafe=363`, `AupPrecisionRisk=0`, `FindObjectCalls=5`, `LegacyEventPublish=28`, Core asmdef debt `25`, generated-project debt `10`, total bridge debt `14`, compile-bridge debt `8`, replacement debt `6`.
- Expected failure paths: `EXPECTED_TOTAL_BRIDGE_BUDGET_FAIL_PATH_OK` at total bridge budget 13 and `EXPECTED_REPLACEMENT_BRIDGE_BUDGET_FAIL_PATH_OK` at replacement budget 5.

Residual risk:
- This is not fresh compile proof. `Fresh19` remains the latest compile artifact on disk.
- Generated `Hecton8.Core.csproj` still lists stale project references until Unity regenerates project files or a compile-enabled lane validates generated-project cleanup.

## 2026-05-15 - Fresh48 Final Bottom Addendum

What was wrong:
- Earlier bottom sections are historical. The current compile authority is `Fresh48`, not Fresh25 and not Fresh47.
- Fresh47 was rejected as final evidence because a C# source timestamp was newer than its build log.

What was done:
- Revalidated the current disk after timestamp invalidation.
- Accepted `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh48.log` as the latest compile artifact.
- Accepted `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh48.json` as the latest static H-Phi artifact.

Cinematic Cheats used:
- No runtime simulation changes.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final build verification time: 34,270,000 us.
- Final H-Phi verification time: 55,700,000 us.

Verification:
- `Hecton8.Core.csproj --no-restore`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `EXIT=0`.
- H-Phi static gate: `EXIT=0`, `RuntimeHPhiRisk=0.000597081`, `GlobalRegistrySurface=5146`, `UnityUpdateMethods=0`, duplicate signal budget actual `0`, `AupPrecisionRisk=0`.
- Freshness: no C# source under `Assets/_Project/Scripts` was newer than the Fresh48 build or H-Phi artifacts at verification time.
- `git diff --check`: exit 0 with CRLF normalization warnings only.

Current Status:
- BUILD SUCCESSFUL / PLATINUM GRADE / FRESH48 CURRENT-DISK GREEN.

## 2026-05-15 - Fresh50 Layout And Bootstrap Readback Closure

What was wrong:
- Fresh48 was green, but current disk still had two Core boundary marker structs without explicit layout metadata and a few bootstrap paths with avoidable duplicate `GlobalRegistry` readbacks.
- The inquisition report's binary-layout warning required classification, not blanket attributes on managed-reference structs.

What was done:
- Added `[StructLayout(LayoutKind.Sequential)]` to `MacroDatabaseSignalBridge` and `PersistenceAssemblyMarker`.
- Reduced safe bootstrap registry readbacks in fauna simulation registration, native input manager registration, and no-op audio fallback.
- Left side-effect-sensitive settings and beacon component paths untouched after confirming `Awake/OnEnable` can register services during `AddComponent`.

Cinematic Cheats used:
- Static architecture debt surgery only.
- No gameplay, rendering, physics, simulation, package, asmdef, prefab, or generated `.csproj` path was changed.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final build verification time: 26,670,000 us.
- Final H-Phi verification time: 39,700,000 us.

Verification:
- Build artifact: `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh50.log`.
- Build result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `EXIT=0`.
- H-Phi artifact: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh50.json`.
- H-Phi result: `EXIT=0`, `RuntimeHPhiRisk=0.000598725`, `GlobalRegistrySurface=5142`, `StructLayoutAttributes=957`, `MemoryAlignment=0.506081438`, `UnityUpdateMethods=0`, duplicate signal budget actual `0`, `AupPrecisionRisk=0`.
- Freshness: no C# source under `Assets/_Project/Scripts` was newer than the Fresh50 build or H-Phi artifacts at verification time.

## 2026-05-15 - Fresh64 Bootstrap Readiness And H-Phi Budget Closure

What was wrong:
- Fresh50 was no longer sufficient evidence after parallel C# edits landed.
- `GameBootstrapper` still had avoidable readiness-side `GlobalRegistry` self-read/coupling debt.
- The H-Phi doc command drifted away from the Fresh62 verified gate by missing `-MaxUnityUpdateMethods 0` and by retaining looser source counters.

What was done:
- Ran fresh serial Core compile passes without `dotnet rebuild`; final accepted compile is `Fresh64`.
- Reduced bootstrap dependency readiness coupling by resolving the dependency service once for heartbeat fallback and by using a node-readiness overload.
- Preserved lifecycle-sensitive bootstrap paths where `AddComponent` may register services during `Awake/OnEnable`.
- Updated the H-Phi documentation gate to the Fresh62 verified source budgets, including Unity loop debt zero and owner-blocked NativeArray ceilings.

Cinematic Cheats used:
- Compile/static architecture surgery only.
- No gameplay, physics, rendering, shader, prefab, package, generated `.csproj`, or leaf NativeArray ownership path was changed.

Exact Microseconds saved:
- Runtime frame time saved: 0 us measured.
- Final compile verification time: 115,650,249 us.
- Final H-Phi verification time: 138,599,793 us.
- Static coupling impact versus Fresh50: `GlobalRegistrySurface` 5142 -> 5086, `GetComponentCalls` 530 -> 405, `ManagedFormatSurface` 704 -> 681, `PrimaryManagedRuntimeRisk` 353 -> 330.

Verification:
- Compile artifact: `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh64.log`.
- Compile result: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `EXIT=0`.
- H-Phi artifact: `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_Fresh62.json`.
- H-Phi result: `EXIT=0`, `RuntimeHPhiRisk=0.000612591`, `DataSovereignty=0.021123844`, `MemoryAlignment=0.505783386`, `GlobalRegistrySurface=5086`, `GetComponentCalls=405`, `NativeArrayRefs=7090`, `OwnerBlockedNativeArrayRefs=6280`, `PrimaryOwnerBlockedNativeArrayRefs=5696`, `UnityUpdateMethods=0`, duplicate signal budget actual `0`, `AupPrecisionRisk=0`.
- Freshness: no C# source under `Assets/_Project/Scripts` was newer than the Fresh64 build log at verification time.
- Diff hygiene: `git diff --check` on touched code/docs/status/log paths returned exit 0 with no output.

Residual risk:
- No Unity import, scene play, GCMonitor session, profiler capture, or player build was run.
- NativeArray count movement is partly concurrent leaf-domain churn; this pass recorded the budget boundary instead of editing non-Integrator owners blindly.

Current Status:
- BUILD SUCCESSFUL / PLATINUM GRADE / FRESH64 CURRENT-DISK GREEN.
- `git diff --check`: exit 0 with CRLF normalization warnings only.

Residual risk:
- Unity Editor import, Play Mode, profiler, GCMonitor, player build, and runtime visuals were not run.
- Runtime microsecond savings are not claimed without profiler evidence.

Current Status:
- BUILD SUCCESSFUL / PLATINUM GRADE / FRESH50 CURRENT-DISK GREEN.
