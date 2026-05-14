# Rationale_INTEGRATION_ASSEMBLY_SURGEON

Agent: INTEGRATION_ASSEMBLY_SURGEON
Domain: Unity Compilation Graph / Integrator
Status: BUILD SUCCESSFUL / POST-GREEN STATIC H-PHI DEBT REDUCED (Fresh19 retained; no dotnet command run in latest static pass)

## Decision 0 - Session Initialization

Problem: No existing status or rationale file for this assigned compile medic ID.
Solution: Created disk-backed checklist and rationale before touching compile code, using the batch prompt as primary directive and mandate registry as the technical filter.
Rejected Alternatives: Chat-only progress was rejected because the batch protocol makes disk state authoritative. Reading prior unrelated agents' logs as architectural input was rejected because the prompt says to ignore neighboring tasks unless they are compile artifacts.
Scalability potential: Low/Middle/High/Ultra are not runtime visual tiers for this initialization step; compile-boundary cleanup preserves later tier-specific runtime work by avoiding dependency cycles.
Hardware Impact: 0 us runtime change on i3/MX350. This is process state only.

## Decision 1 - Mandate Selection

Problem: Compile wall can tempt brute-force project-reference additions that create cycles.
Solution: Selected `ARCH_Global_Registry_ServiceLocator_DI_Init`, `PROJECT_LTS_Compatibility_Layer`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, and `QA_Evidence_Text_Filter_Audit` as the active mandate set.
Rejected Alternatives: Rendering/physics/audio domain mandates were rejected until diagnostics prove domain-specific runtime edits are required. Adding leaf assemblies to Core was rejected under H-PHI defense.
Scalability potential: Low tier benefits from strict contract isolation because it keeps Core compile/runtime surface smaller; Ultra tier remains free to add overkill visuals behind leaf systems without contaminating Core.
Hardware Impact: Expected runtime gain is indirect only: fewer hard references and smaller compile/runtime dependency surface. Estimated frame impact: 0 us until runtime code is touched.

## Decision 2 - Stale Build Wall Triage

Problem: First build reported `LeviathanTerrainIkJob.TailWhipDurationSeconds` and `PrologueSplashdownSineSweepProbeJob` missing, but current source already contained both symbols and line numbers had shifted.
Solution: Rebuilt before editing source. The second build cleared both errors, proving the first wall was stale relative to concurrent source churn.
Rejected Alternatives: Adding duplicate shim fields/jobs was rejected because it would create type noise around already-present symbols.
Scalability potential: Low/Middle/High/Ultra runtime tiers unchanged. Avoiding duplicate shims preserves single source of truth for later visual-tier logic.
Hardware Impact: 0 us runtime change. Compile-only decision.

## Decision 3 - Duplicate IK Assembly Surface

Problem: Core compile saw `LeviathanTerrainIkJobs.cs` as source while also resolving stale `Library/ScriptAssemblies/Hecton8.Animation.IK.dll`, producing CS0436 conflicts.
Solution: Re-read the current source-backed MSBuild bridge surface and confirmed `Directory.Build.targets` no longer contains a `Hecton8.Animation.IK` bridge reference. Existing CLI logs after that point show no CS0436 IK collisions.
Rejected Alternatives: Editing generated `Hecton8.Core.csproj` was rejected because Unity overwrites it. Patching the stale DLL was rejected because `Library/ScriptAssemblies` is generated output. Removing current source was rejected because it contained the newer field required by `FaunaKinematicsRuntime`.
Scalability potential: Low tier avoids ambiguous stale IK contracts; Ultra tier keeps current tail-whip duration field available to the high-fidelity IK path.
Hardware Impact: Expected runtime impact 0 us; compile graph cleanup only. Indirect editor iteration gain: avoids duplicate symbol resolution warnings in Core.

## Decision 4 - Vendor Warning Boundary

Problem: Raw project-reference build succeeds but reports 47 warnings from URP, GPUInstancer, Crest, ShaderGraph, and WaveHarmonic Crest projects.
Solution: Treated those as third-party/vendor warnings under the third-party integrity rule and captured a separate isolated Core compile with `-p:BuildProjectReferences=false`, which returned 0 warnings and 0 errors.
Rejected Alternatives: Editing package/vendor code was rejected by third-party asset integrity. Suppressing vendor warnings globally was rejected as false evidence. Reporting raw build as zero-warning was rejected by QA evidence law.
Scalability potential: Low tier remains protected from first-party Core compile errors; high/ultra visual packages still require separate vendor-maintenance work outside this emergency compile pass.
Hardware Impact: Runtime impact 0 us. Build evidence impact: first-party Core wall is green; vendor warning debt remains non-runtime compile-noise debt.

## Decision 5 - Core Medic ProjectReference Isolation Restored

Problem: On 2026-05-15 readback, `Directory.Build.props` did not contain the Core-only project-reference isolation gate, while `Hecton8.Core.csproj` still declares generated project references to URP, GPUInstancer, Crest, WaveHarmonic, EasySave3, VolumetricLightBeam, input projects, and contracts.
Solution: Hardened the source-backed `Directory.Build.props` property group: for `Hecton8.Core`, default `BuildProjectReferences=false` and `BuildInParallel=false` unless `HectonBuildProjectReferences=true` is explicitly supplied.
Rejected Alternatives: Editing `Hecton8.Core.csproj` was rejected because it is generated. Editing third-party package projects was rejected by third-party asset integrity. Running a fresh dotnet build was rejected because the user explicitly ordered no dotnet rebuilds.
Scalability potential: Low tier and weak developer machines avoid dragging third-party compile/warning noise into a Core medic pass; High/Ultra package-owner validation remains opt-in through `HectonBuildProjectReferences=true`.
Hardware Impact: Runtime impact 0 us. Build-time impact is a static expectation today; the existing artifact `Build_INTEGRATION_ASSEMBLY_SURGEON_05_RawCoreDefault.log` from 2026-05-14 measured 31,300,000 us for 0 warnings / 0 errors before this parallelism hardening line, but no fresh 2026-05-15 dotnet proof was generated by instruction.

## Decision 6 - H-Phi Asmdef Debt Boundary

Problem: Static asmdef audit shows `Assets/_Project/Scripts/Hecton8.Core.asmdef` references broad leaf/domain assemblies: Animation IK, Environment Fluids, World Terrain/GPR, AI Cognition, AI Ecology Migration, Physics, Vehicles, Audio Propagation/Virtualization/Echolocation, Logistics, Cartography, Input, URP, TextMeshPro, UI, and GPUInstancer. This is not a clean H-Phi contract surface.
Solution: Documented the debt and refused a blind removal pass. Current Core-owned source still directly uses leaf contracts/types, including `FaunaKinematicsRuntime` using `LeviathanTerrainIkJob`, `GlobalRegistryContracts` using `MacroSwarm`, and `GlobalRegistryContracts` using `SoundEmissionSignal`.
Rejected Alternatives: Removing asmdef references without moving DTOs/contracts was rejected because it would create a dependency compile wall. Moving `MacroSwarm`, `SoundEmissionSignal`, and IK job ownership during a no-build continuation was rejected as an unsafe cross-domain refactor.
Scalability potential: Low/Middle/High/Ultra runtime visuals unchanged. The clean path is staged contract extraction into lower contract assemblies, not emergency leaf-reference deletion.
Hardware Impact: Runtime impact 0 us. Static compile-graph risk is reduced only by the restored generated-project build gate; full asmdef purification remains pending a compile-enabled integration pass.

## Decision 7 - No-Dotnet Verification Boundary

Problem: User ordered continued work but explicitly forbade dotnet rebuilds. QA law forbids claiming fresh compile health from static scans.
Solution: Performed static-only validation: `Directory.Build.props` gate presence, generated Core project reference scan, Core asmdef reference scan, current batch tag scan, owned-file hot-path poison scan, and `git diff --check` on owned files.
Rejected Alternatives: Running `dotnet build`, `dotnet rebuild`, or `dotnet msbuild` was rejected by user instruction. Claiming Unity or fresh compile verification was rejected by evidence law.
Scalability potential: Static H-Phi work preserves build-lane determinism but does not prove player-facing runtime quality. Low and Ultra runtime tiers require later Unity/profiler evidence outside this no-dotnet pass.
Hardware Impact: Runtime impact 0 us. Fresh microsecond savings are not claimed; only the prior dated CLI artifact remains as historical evidence.

## Decision 8 - H-Phi Core Graph Audit Mode

Problem: H-Phi compile-graph debt was recorded manually in prose, which is too easy to stale out under parallel agent churn.
Solution: Extended `Tools/Architecture/HectonPhiAudit.ps1` with `-CoreGraphOnly`, a fast static mode that classifies `Hecton8.Core.asmdef` references, generated `Hecton8.Core.csproj` project references, and the `Directory.Build.props` Core build gate.
Rejected Alternatives: Creating a second standalone audit script was rejected because duplicate tooling fragments evidence. Running the full source H-Phi audit was rejected for this continuation because previous global H-Phi scans have timed out under repo load and the user requested continued careful work without build churn.
Scalability potential: Low-end development machines get a fast graph-only audit that avoids a full source scan; High/Ultra validation can still run the full H-Phi scan when time budget allows. Runtime visuals unchanged.
Hardware Impact: Runtime impact 0 us. Tooling impact: `-CoreGraphOnly` completed in 2026-05-15 static run and reported 46 Core asmdef refs, 28 Core asmdef H-Phi debt refs, 12 generated Core project refs, and 10 generated Core project debt refs. No fresh compile proof was generated.

## Decision 9 - Core Graph Budget Gate

Problem: A graph audit that only reports numbers can still let new H-Phi debt slip in unless humans compare the counts manually.
Solution: Added optional budget switches to `HectonPhiAudit.ps1`: `-RequireCoreBuildGate`, `-MaxCoreAsmdefDebtReferences`, and `-MaxGeneratedProjectDebtReferences`. The continuation validated the current baseline with `-CoreGraphOnly -RequireCoreBuildGate -MaxCoreAsmdefDebtReferences 28 -MaxGeneratedProjectDebtReferences 10`, then verified the expected failure path with a zero asmdef-debt budget.
Rejected Alternatives: Hard-failing on any existing debt was rejected because the current Core graph already has known debt and blind removal is unsafe. Baking the baseline into the script was rejected because the budget should be supplied by CI/task owner, not hidden inside tooling.
Scalability potential: Low-end developer machines can enforce no-regression H-Phi graph budgets with a fast static script. High/Ultra validation lanes can lower the budgets after staged contract extraction.
Hardware Impact: Runtime impact 0 us. Tooling impact is bounded to PowerShell text/XML/JSON reads; no Unity import or dotnet command is involved. Failure path now reports the aggregated budget violation text through one deterministic exception instead of stopping on the first `Write-Error`.

## Decision 10 - Fresh XML Compile Pass Re-Entry Paused By No-Dotnet Order

Problem: The previous session ended in static/no-dotnet mode, while the current in-chat XML explicitly orders a fresh `dotnet build Hecton8.Core.csproj --no-restore` wall read and contract surgery.
Solution: Preserve the fresh checklist as pending history, but keep it paused under the latest repeated user instruction forbidding dotnet rebuilds. The initial `Docs/Tasks/CURRENT_BATCH.md` extraction attempt did not expose this agent tag, so no fresh batch-file source overrides the current no-dotnet boundary.
Rejected Alternatives: Reusing old green logs was rejected by evidence law. Editing generated project files first was rejected because Unity regenerates them and the LTS mandate makes source-backed graph files authoritative.
Scalability potential: Low tier keeps Core compile diagnostics focused instead of dragging every third-party warning into the medic lane. Middle/High/Ultra validation can widen project references after first-party contract drift is removed.
Hardware Impact: Runtime impact 0 us. This is compile/evidence state only; no player-frame microsecond savings are claimed.

## Decision 11 - Stable H-Phi Metric Documentation

Problem: H-Phi had executable tooling and dated report addenda, but no stable architecture contract explaining what the metric means, how it is calculated, and why it matters. That made the metric vulnerable to vanity edits and fake evidence claims.
Solution: Added `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md` with the exact coefficient formulas, counter definitions, Core graph debt model, budget-gate command, valid/invalid improvement rules, and evidence-language boundary. Linked it from `Docs/ARCHITECTURE/README.md` and `Docs/README.md`.
Rejected Alternatives: Appending another paragraph to `Docs/Reports/HECTON_PHI_REPORT.md` was rejected because reports are snapshots, not durable policy. Adding comments only inside `Tools/Architecture/HectonPhiAudit.ps1` was rejected because agents need a stable documentation entry point before touching code.
Scalability potential: Low tier gets clearer rules for shedding and decoupling weak-device paths; Middle/High/Ultra tiers get a protected leaf-system boundary so visual overkill remains outside Core. The doc explicitly prevents treating static H-Phi as runtime proof.
Hardware Impact: Runtime impact 0 us on i3/MX350 and high-end machines. Documentation-only change. Expected engineering impact is reduced false-positive metric chasing and fewer unsafe Core-to-leaf dependency edits.

## Decision 12 - README Mojibake Boundary

Problem: The broad ASCII scan after documentation edits reported non-ASCII content in `Docs/README.md`.
Solution: Narrowed the scan to owned/new H-Phi documentation and state/log edits, then located the existing offending line: `Docs/README.md:17` contains historical mojibake for an old root filename. The H-Phi edit only changed the date and added an ASCII path entry.
Rejected Alternatives: Normalizing the unrelated mojibake line during this integrator pass was rejected as doc-history churn outside the H-Phi metric contract. Ignoring the scan without locating the line was rejected because it would leave ambiguity about whether the new doc introduced non-ASCII.
Scalability potential: Low/Middle/High/Ultra runtime tiers unchanged. The decision preserves focused documentation scope under concurrent agents.
Hardware Impact: Runtime impact 0 us. Tooling impact 0 us outside static text scan time.

## Decision 13 - Unused Core Asmdef Reference Prune

Problem: `Hecton8.Core.asmdef` still carried Core-to-leaf references that inflated H-Phi graph debt even when generated Core compile items did not use those assemblies.
Solution: Removed `Hecton8.Input.Generated`, `Hecton8.World.GPR`, and `Hecton8.SpaceEngine098Terrain` from `Hecton8.Core.asmdef`. Static evidence: generated Core compile-item filter had no `HectonInputActions`, `GroundPenetratingRadar`, `World/GPR`, `World/SpaceEngine098`, `Input/InputManager`, or SpaceEngine098 entries; type-name scan found no generated Core compile-item use of GPR or SpaceEngine098 public job/payload types. `HectonInputActions` use remains in `Assets/_Project/Scripts/Input/InputManager.cs`, owned by `Hecton8.Input`, not Core.
Rejected Alternatives: Removing references with live Core type hits was rejected. Editing generated `Hecton8.Core.csproj` was rejected because Unity overwrites it. Running dotnet compile was rejected by the current user no-dotnet order.
Scalability potential: Low tier keeps Core less coupled to input codegen, GPR runtime jobs, and SpaceEngine terrain kernels; High/Ultra tiers keep those leaf assemblies available through their own asmdefs instead of contaminating Core.
Hardware Impact: Runtime impact 0 us. Static graph debt changed from 28 to 25 Core asmdef debt refs. Fresh compile/runtime proof remains pending.

## Decision 14 - Source-Backed Core Bridge Debt Gate

Problem: `Directory.Build.targets` can inject Core references that are not visible in the generated `.csproj` or asmdef debt counts. After pruning asmdef references, the bridge still injected `Hecton8.World.GPR` and `Hecton8.SpaceEngine098Terrain`.
Solution: Removed the matching `Hecton8.World.GPR` and `Hecton8.SpaceEngine098Terrain` bridge reference blocks from `Directory.Build.targets`. Extended `Tools/Architecture/HectonPhiAudit.ps1` with `SourceBackedBridgeReferenceCount`, `SourceBackedBridgeDebtReferenceCount`, bridge debt rows, and `-MaxSourceBackedBridgeDebtReferences`.
Rejected Alternatives: Leaving bridge debt uncounted was rejected because it hides compile-graph coupling. Removing additional bridge references was rejected where live Core source evidence still exists or where the reference is a contract/Core-family dependency. Running dotnet compile was rejected by the current no-dotnet order.
Scalability potential: Low-end machines avoid two unnecessary bridge references in Core medic mode; High/Ultra lanes keep GPR and SpaceEngine terrain in their leaf/adapter assemblies. The audit now catches future bridge widening before it becomes compile-wall noise.
Hardware Impact: Runtime impact 0 us. Static bridge graph after cleanup: 19 source-backed Core bridge refs, 8 bridge debt refs. Fresh compile/runtime proof remains pending.

## Decision 15 - Fresh XML Compile Authority Restored

Problem: Disk history still contained a stale no-dotnet continuation, but the current in-chat XML ordered `dotnet build Hecton8.Core.csproj --no-restore` and made this agent final compile authority.
Solution: Treated the current XML as the active directive, preserved older state as history, and used file-backed logs for every compile pass. `Fresh01` captured 79 filtered compiler entries; `Fresh19` closed current disk state at 0 warnings and 0 errors.
Rejected Alternatives: Reusing old green logs was rejected by QA evidence law. Ignoring the current XML because older status said no-dotnet was rejected because the current user directive is newer and explicit.
Scalability potential: Low tier benefits from a smaller deterministic Core medic lane; Middle/High/Ultra package-heavy visual assemblies remain referenced through Unity-built outputs instead of being rebuilt as part of this emergency lane.
Hardware Impact: Runtime impact 0 us on i3/MX350 and high-end machines. Final compile verification cost measured in `Fresh19`: 81,710,000 us.

## Decision 16 - Source-Backed Project Output Translation

Problem: After restore, generated `ProjectReference` outputs pointed at missing `Temp\bin\Debug\*.dll` files and created CS0006 metadata failures. Removing all project references without replacement exposed real source dependencies on URP, GPUInstancer, Input, Bootstrap Contracts, Crest, Shapes, EasySave, VLB, and WaveHarmonic assemblies.
Solution: Use source-backed MSBuild policy: suppress generated project outputs for `Hecton8.Core` medic builds and resolve the same contracts from existing `Library\ScriptAssemblies` DLLs. This keeps Core compile evidence deterministic without editing Unity-generated `.csproj` files.
Rejected Alternatives: Building every vendor/package project was rejected because it reintroduces third-party warning noise and missing metadata churn. Stubbing URP/GPUInstancer/Input APIs was rejected as fake compilation. Editing `Hecton8.Core.csproj` was rejected because Unity regenerates it.
Scalability potential: Low-end developer machines avoid vendor rebuild fanout; Middle/High/Ultra visual packages remain available as prebuilt Unity assemblies and can still be validated in their own owner lanes.
Hardware Impact: Runtime impact 0 us. Editor compile impact is avoidance of generated project-output CS0006 churn; exact player-frame savings are 0 us because no runtime loop was changed.

## Decision 17 - Save Hash Contract Alignment

Problem: `SaveMasterHashV10` used the Unity Collections `xxHash3` API but only imported `Unity.Mathematics`, producing a missing symbol in the isolated Core build.
Solution: Import `Unity.Collections` and keep the established `xxHash3.Hash64` call shape used by `SaveBinaryStorage`.
Rejected Alternatives: Replacing the hash with ad hoc FNV, `HashCode`, or managed crypto was rejected because save identity must stay deterministic and allocation-free. Qualifying it as `Unity.Mathematics.xxHash3` was rejected after the compiler proved that namespace was wrong for the installed package.
Scalability potential: Low/Middle/High/Ultra save validation keeps the same deterministic hash path; no visual-tier behavior changes.
Hardware Impact: Runtime impact 0 us relative to intended hash path. The fix restores the existing native hash API; no new managed allocation path was introduced.

## Decision 18 - PDA Inventory Signal Bridge

Problem: `PDAShellChrome` called `RefreshInventorySignalBinding` and `ConsumeInventoryChangedSignals`, but the methods were absent. That broke compile and would have forced dirty UI polling if fixed lazily.
Solution: Added a fixed `SignalBus<InventoryChangedSignal>` frame-snapshot consumer keyed by the bound `PlayerInventory` entity hash, with `_lastInventorySignalRevision` suppressing duplicate work.
Rejected Alternatives: Polling inventory every late frame was rejected as managed/UI churn. Adding a new event contract was rejected because the global inventory signal lane already exists. Changing `SignalBus` was rejected because call-site repair was sufficient.
Scalability potential: Low tier gets cheap dirty-bit UI refresh; Middle/High/Ultra keep reactive PDA chrome without extra per-frame hierarchy search or inventory scans.
Hardware Impact: Expected low-end gain versus forced polling is small but real; exact measured runtime saving in this compile pass is 0 us because no profiler run was performed. The implementation is allocation-free and bounded by the current frame signal snapshot.

## Decision 19 - Final Evidence Boundary

Problem: The final status required 0 warnings and 0 errors while concurrent processes and an invalid blank `Fresh10` run created unreliable artifacts.
Solution: Re-ran with build servers disabled, one worker, no restore, and direct log capture. Accepted only `Fresh19` as current-disk proof after concurrent edits; it reports `Hecton8.Core -> Temp\bin\Debug\Hecton8.Core.dll`, `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
Rejected Alternatives: Treating blank logs or partial MSBuild diagnostic logs as final evidence was rejected. Running a broad Unity/player validation was rejected because the current objective was Core compile authority, not runtime QA.
Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged; compile evidence is now reproducible for the Core medic lane.
Hardware Impact: Runtime impact 0 us. Final compile verification time: 81,710,000 us. Exact runtime microseconds saved: 0 us.

## Decision 21 - Concurrent Edit Revalidation

Problem: After `Fresh12` succeeded, concurrent edits touched PDA/audio/save surfaces. Fresh verification then exposed a missing audio scalability listener, a duplicate listener during concurrent repair, and bounded save write helper drift.
Solution: Preserved the richer concurrent `PlayerCriticalProceduralAudioRenderer.OnScalabilityChanged` implementation, removed the duplicate thin method, added a no-params-allocation `ClampPairedCollectionCount`, and added counted custom-array write overloads that route through `WriteCustomArraySlice`.
Rejected Alternatives: Reverting concurrent edits was rejected. Changing read-side save format was rejected because existing readers already expect DTO counts plus array lengths. Using `params Array[]` for paired clamp was rejected because it would allocate on the save path.
Scalability potential: Low tier retains bounded save serialization and cached audio quality updates; Middle/High/Ultra retain richer audio scalability behavior from the concurrent implementation.
Hardware Impact: Runtime frame savings are not profiled and are reported as 0 us. The clamp/overloads avoid new managed allocations; final compile verification time was 81,710,000 us.

## Decision 22 - Optional Core Reference Candidate Scan

Problem: Core asmdef debt pruning had depended on one-off type/name scans. That is too fragile under parallel agent churn and can tempt blind deletion of leaf references.
Solution: Added `-IncludeUnusedCoreReferenceScan` to `HectonPhiAudit.ps1`. The scan maps each Core asmdef debt reference to its asmdef source, collects declared type names, strips comment noise, and searches the generated/source-backed Core compile surface outside the candidate's own source files. The post-green run scanned 1065 Core compile-surface files and 25 asmdef debt refs; `CandidateCount=0`.
Rejected Alternatives: Removing more Core asmdef references by name was rejected because every remaining leaf asmdef with source had external Core hits. Building a separate one-off script was rejected because the H-Phi audit must be the single evidence surface.
Scalability potential: Low-end machines get a static candidate-ordering pass without Unity import or dotnet; High/Ultra lanes can use the same scan before staged contract extraction. No runtime visual tier behavior changed.
Hardware Impact: Runtime impact 0 us. Tooling cost is static file IO/text regex only. It prevents false compile-wall edits rather than saving player-frame time.

## Decision 23 - Bridge Lane Split And Input Generated Prune

Problem: The current `Directory.Build.targets` contains two Core bridge lanes: compile-bridge references and project-reference replacement references used when generated project references are disabled. Counting them as one opaque number hid 12 replacement debt refs, while the old counter also counted a `<Reference Remove=...>` node as a dependency edge.
Solution: Tightened bridge counting to `Reference Include` nodes only, added `BridgeLane` labels (`CoreCompileBridge`, `ProjectReferenceReplacement`), and added lane-specific budget switches. Removed the stale `Hecton8.Input.Generated` replacement reference after static scans found `HectonInputActions` use only in `Hecton8.Input`, not Core. Current baseline: 31 bridge refs, 20 bridge debt refs, 18 compile-bridge refs with 8 debt refs, and 13 replacement refs with 12 debt refs.
Rejected Alternatives: Keeping the old total bridge budget of 8 was rejected because it ignored the replacement lane. Removing package or `Hecton8.Input` replacement refs was rejected because current Core source has live InputSystem/UI/package surfaces. Running dotnet was rejected by the current user instruction.
Scalability potential: Low tier gets a narrower replacement lane and honest no-regression gates; High/Ultra package-heavy visual systems remain isolated behind Unity-built assemblies rather than polluting Core ownership. Future contract extraction can lower compile-bridge and replacement budgets independently.
Hardware Impact: Runtime impact 0 us. Static graph impact: one replacement debt ref removed (`Hecton8.Input.Generated`), and four budget failure paths now catch regressions before compile time.

## Decision 24 - Replacement Bridge Zero-Hit Prune

Problem: The project-reference replacement lane still contained package/editor references after the previous bridge split. Some were only historical generated-project fallout, not Core source needs.
Solution: Reconstructed the generated/source-backed Core compile surface and scanned 1065 files for exact package/editor usage. Removed `EasySave3`, `Unity.RenderPipelines.Core.Editor`, `Unity.ShaderGraph.Editor`, and `WaveHarmonic.Crest.Shared.Editor` from `Directory.Build.targets` after zero hits. Kept `Crest`, `GPUInstancer`, `ShapesRuntime`, `VolumetricLightBeam`, `Unity.RenderPipelines.Universal.Runtime`, `WaveHarmonic.Crest`, `WaveHarmonic.Crest.Shared`, and `Hecton8.Input` because they had live Core source hits.
Rejected Alternatives: Editing generated `Hecton8.Core.csproj` was rejected because Unity regenerates it. Removing package refs with live source hits was rejected because it would create an unverified compile wall. Running dotnet was rejected under the current no-rebuild constraint.
Scalability potential: Low tier gets a narrower Core medic reference surface; High/Ultra package-heavy lanes remain available through only the assemblies Core actually references. Future package decoupling can target live-hit refs with contract extraction instead of blind deletion.
Hardware Impact: Runtime impact 0 us. Static graph impact: total bridge debt reduced from 20 to 16 and project-reference replacement debt reduced from 12 to 8. No runtime microsecond savings are claimed.

## Decision 25 - H-Phi Audit Syntax And AUP Gate Repair

Problem: `HectonPhiAudit.ps1` had an invalid multi-key `Sort-Object -Property` expression around `TopAupPrecisionRiskFiles`, so Core graph budget commands failed before reporting counts.
Solution: Repaired the PowerShell property-array syntax without removing the AUP precision budget feature. Full static summary with `-MaxAupPrecisionRisk 0` now returns `AupPrecisionSafe=363` and `AupPrecisionRisk=0`.
Rejected Alternatives: Reverting the AUP precision gate was rejected because it is useful H-Phi debt control. Treating the parse failure as a project build failure was rejected because no compile command was involved.
Scalability potential: Low-end validation gets a static precision-risk tripwire without Unity import or dotnet. High/Ultra validation can keep the same gate as a preflight before runtime/profiler proof.
Hardware Impact: Runtime impact 0 us. Tooling reliability improved; exact player-frame savings are 0 us.
