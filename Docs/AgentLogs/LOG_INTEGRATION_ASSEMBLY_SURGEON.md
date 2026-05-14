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
