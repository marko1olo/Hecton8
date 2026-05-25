# LOG_X_003

## 2026-05-23 COMPILE_WALL_SMASHER_AND_DOMAIN_DECOUPLER

Status: PARTIAL PASS - CORE COMPILE VERIFIED / ARCHITECTURAL BLOCKERS REMAIN
Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / CHANGED_ASSEMBLY_COMPILE. No Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, or generated-project regeneration proof.

What was wrong:
- `Hecton8.Core.asmdef` is still a gravity well: 45 total refs, 32 first-party refs, 17 concrete sibling runtime refs.
- Static graph found 178 first-party asmdefs, 423 edges, 0 cycles, 116 runtime concrete sibling asmdef refs under `Assets/_Project`.
- Source using-boundary audit found 2,207 runtime cross-domain using violations under `Assets/_Project`.
- Core-owned gameplay files remain trapped in the root assembly. Selected blast radius is 98 assemblies for:
  - `Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime.cs`
  - `Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs`
  - `Assets/_Project/Scripts/Gameplay/HectonPlayerState.cs`
  - `Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs`
  - `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- Top contract-extraction candidates are real but unsafe for blind movement: `IDataVault`, `BufferID`, `VaultGenerationHandle`, `IGlobalRegistryHotSwapListener`, `ILateFrameTickable`, `IUpdatable`, `ISlowTickable`, `IPlayerRuntimeContext`.

What was done:
- Added `Tools/CompileWallX003Audit.py`.
- Corrected X_003 audit scope from `Assets/_Project/Scripts` to `Assets/_Project` so generated input/domain editor asmdefs are included.
- Generated:
  - `Docs/AgentLogs/CompileWall_X_003_Archaeology.json`
  - `Docs/AgentLogs/CompileWall_X_003_Archaeology.md`
  - `Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json`
- Ran existing gates:
  - `AssemblyDependencyAudit.py --fail-on-cycles`: PASS, 0 cycles.
  - `AssemblyDependencyAudit.py --fail-on-runtime-concrete-sibling-refs`: FAIL as required, currently 116 runtime concrete sibling refs under full-project scope.
- Patched `Assets/_Project/Scripts/Gameplay/EndingSystem.cs`:
  - `SlowTick()` no longer reads `GlobalRegistry.AtlasSignal`.
  - `SlowTick()` no longer reads `GlobalRegistry.Quest`.
  - Added cached `_atlasSignal` and `_questRuntime` fields.
  - Added `IGlobalRegistryHotSwapListener` refresh for `AtlasSignalRuntime` and `QuestRuntime`.
- Re-ran X_003 audit:
  - hot-path registry polling/search findings: 0.
  - remaining hot-path registry mutation notes: 2 self-unregister paths in existing code, not polling.
- Updated:
  - `Docs/Tasks/Status_X_003.md`
  - `Docs/AgentLogs/Rationale_X_003.md`
  - `Docs/ARCHITECTURE/GLOBAL_AUTHORITY_MIGRATION_LEDGER.md`

Cinematic Cheats used:
- None. This task touched compile topology, static audit tooling, and one registry-cache hot-path cleanup. No water/light/deformation simulation was introduced.

Exact microseconds saved:
- Measured runtime microseconds: 0 claimed. No profiler/GCMonitor proof was allowed or run.
- Static code-path reduction: 2 `GlobalRegistry` property reads removed from `EndingSystem.SlowTick()`.
- Compile-wall microseconds saved: 0 claimed. No runtime asmdef edge was severed because current source coupling would break compile.
- Static compile-wall baseline preserved for future proof: 98 affected assemblies for selected Core-owned gameplay files.

Verification:
- `python -m py_compile Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/CompileWallX003Audit.py`: PASS, report generated.
- `python Tools/AssemblyDependencyAudit.py --fail-on-cycles`: PASS.
- `python Tools/AssemblyDependencyAudit.py --fail-on-runtime-concrete-sibling-refs`: FAIL, expected and documented.
- Initial `dotnet build`: delayed by AGENTS.md rule because CPU was 65%, then 100%, and active `dotnet`/`csc.exe` processes existed.
- Later `dotnet build Hecton8.Core.csproj --no-restore`: PASS, 0 warnings, 0 errors, 00:02:03.32.

Residual risk:
- `EndingSystem.cs` compiles inside `Hecton8.Core.csproj`; Unity import and Play Mode remain unverified.
- Unauthorized sibling references remain non-zero.
- Contract extraction remains blocked until pure wrappers are designed for broad public APIs and concrete Unity/player facades.

## 2026-05-23 Compile Verification Addendum

What was wrong: X_003 status still carried compile as pending after the first guard blocked execution.

What was done: Rechecked CPU/process guard, then ran the minimal changed-assembly compile: `dotnet build Hecton8.Core.csproj --no-restore`.

Cinematic Cheats used: none. This is assembly verification, not simulation or presentation code.

Exact Microseconds saved: runtime 0. Compile proof cost was 00:02:03.32 wall time. Assembly debt remains 116 runtime concrete sibling refs under full-project scope; no false compile-wall saving claimed.

Result: `Hecton8.Core` compiled successfully with 0 warnings and 0 errors.

## 2026-05-23 Auto-Reference Cleanup Addendum

What was wrong: X_003 rerun found the current Batch 13 file no longer contains `<AGENT_PROMPT id="X_003">`, but existing X_003 audit artifacts still exposed two first-party `autoReferenced=true` Editor asmdefs and a false unresolved reference caused by the previous `Assets/_Project/Scripts` scope.

What was done: Set `autoReferenced=false` in:
- `Assets/_Project/Scripts/Lighting/Editor/Hecton8.Lighting.Editor.asmdef`
- `Assets/_Project/Scripts/Editor/InventoryRouting/Hecton8.InventoryRouting.Editor.asmdef`

Cinematic Cheats used: none. This is compile graph hygiene.

Exact Microseconds saved: runtime 0. Editor compile microseconds unmeasured. Static proof now reports `autoReferencedFalse=178`; hidden auto-reference count is 0.

Result: `python Tools/AssemblyDependencyAudit.py ...` passed with warnings and reported 0 cycles, 116 runtime concrete sibling refs, and all 178 first-party asmdefs explicitly referenced.

## 2026-05-23 Audit Scope Correction Addendum

What was wrong: `Hecton8.Input.Generated.asmdef` lives in `Assets/_Project/Input`, so the earlier `Assets/_Project/Scripts` graph undercounted assemblies and produced a false unresolved `Hecton8.Input.Generated` reference.

What was done: Reran `AssemblyDependencyAudit.py` with `--source-root Assets/_Project` and changed `Tools/CompileWallX003Audit.py` to use the same root.

Cinematic Cheats used: none. This is static compile topology evidence.

Exact Microseconds saved: runtime 0. Editor compile microseconds unmeasured. The metric got stricter: selected Core-owned files now show 98 affected assemblies and 92 direct inbound assemblies.

Result: full-project static graph now reports 178 asmdefs, 423 edges, 0 cycles, 0 unresolved first-party refs, `autoReferencedFalse=178`, and 116 runtime concrete sibling refs.

Verification limit: Editor/full build was not launched after asmdef flag changes because guard reported CPU 62% with an active `dotnet` process. Static JSON and graph gates are current; Unity import remains unverified.

## 2026-05-23 APEX Override Compile-Wall Addendum

What was wrong: Core was still carrying dead first-party refs, the cast audit was too narrow, and physics had one concrete service cast: `GlobalRegistry.Physics as PhysicsApplySystem` in `SeaglideHydrodynamicsRuntime`.

What was done:
- Removed zero-hit Core refs: `Hecton8.Bootstrap.Contracts`, `Hecton8.World.Contracts`, `Hecton8.Environment.Fluids.Contracts`, `Hecton8.Habitat.Deformation.Contracts`, `Hecton8.UI.Localization`.
- Converted seaglide force drain from concrete `PhysicsApplySystem` cache to `IPhysicsService`.
- Expanded `Tools/CompileWallX003Audit.py` to report runtime `as/is/GetComponent` concrete casts, source using domain edges, AI/Physics/UI/Audio critical using findings, and key-file blast radius.

Cinematic Cheats used: none. This is compile topology and service boundary work.

Exact Microseconds saved: runtime 0 claimed. Static compile graph reduction only: edges 423->418, Core refs 45->40, Core first-party refs 32->27, Core concrete sibling refs 17->16, total runtime concrete sibling refs 116->115.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: still `Hecton8.Core`, radius 98, reaches UI/audio. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`: `Hecton8.Physiology`, radius 2, reaches UI=false, audio=false. This one is isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs`: `Hecton8.AI.Cognition`, radius 99, reaches UI/audio through Core's live AI dependency. Not solved.

Verification:
- `python -m py_compile Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/CompileWallX003Audit.py`: PASS.
- `AssemblyDependencyAudit.py --source-root Assets/_Project --fail-on-cycles`: PASS, 0 cycles.
- `AssemblyDependencyAudit.py --source-root Assets/_Project --fail-on-runtime-concrete-sibling-refs`: FAIL as expected, 115 refs.
- CLI compile for latest changes: NOT RUN. Guard first reported CPU 100% and seven active `dotnet` processes; later CPU was 21% but seven `dotnet` processes were still active.

Residual risk:
- Latest asmdef and seaglide edits are STATIC_SOURCE only until compile guard opens.
- Runtime concrete cast debt remains 1,014 findings, concentrated in `Hecton8.Core`; AI/Physics/Physiology direct player concrete coupling count is 0.

## 2026-05-23 APEX Source-Domain DTO Addendum

What was wrong:
- The previous source using audit was too forgiving because it classified files by owning asmdef. Files under `Scripts/AI/*` without a local asmdef were treated as `Hecton8.Core`, masking source-domain coupling.
- `Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs` imported `Hecton8.Audio.Virtualization` only to consume `AcousticEchoTap`.

What was done:
- Moved the 144-byte unmanaged `AcousticEchoTap` transit DTO into `Assets/_Project/Scripts/Core/Contracts/HectonSignalLaneContract.cs`.
- Removed the audio-owned `AcousticEchoTap` copy from `AudioVirtualizationContracts.cs`.
- Removed `using Hecton8.Audio.Virtualization` from `AcousticEchoLocationRuntime.cs`.
- Changed `Tools/CompileWallX003Audit.py` so source-domain using/cast audits derive domain from `Assets/_Project/Scripts/<Domain>` path ownership while asmdef blast radius still uses actual assembly ownership.

Cinematic Cheats used: none. This was compile topology and DTO ownership work.

Exact Microseconds saved:
- Runtime: 0 claimed.
- Compile wall: no wall-clock saving claimed. The asmdef graph is still 418 edges and 115 runtime concrete sibling refs.
- Static source coupling: AssemblyDependencyAudit using-boundary violations reduced 2209->2208. X_003 source-domain audit now reports 470 cross-domain source edges, 3374 cross-domain `using` directives, and 0 critical AI/Physics/UI/Audio source imports.

Verification:
- `python -m py_compile Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`: PASS_WITH_WARNINGS, 0 cycles, 115 runtime concrete sibling refs, 2208 using-boundary violations.
- `rg` in `Assets/_Project/Scripts/AI`, `Physics`, and `Physiology`: no runtime AI->Audio, AI->Physics, Physics->AI, or AI/Physics/Physiology->UI imports; remaining hits are Editor/self-domain or Physics Vehicles automation self-domain.
- Latest `dotnet build Hecton8.Core.csproj --no-restore`: PASS, 0 warnings, 0 errors, 00:01:18.12.
- Coverage check: current generated root has only `Hecton8.Core.csproj`; it includes `HectonSignalLaneContract.cs` but not `AcousticEchoLocationRuntime.cs`, `AudioVirtualizationContracts.cs`, `SeaglideHydrodynamicsRuntime.cs`, or `PhysicsApplySystem.SeaglideQueue.cs`.

Residual risk:
- `Hecton8.Core` still references `Hecton8.Audio.Virtualization.Contracts` and `Hecton8.Audio.Virtualization` through live `GlobalRegistry` audio service APIs.
- `Physics/CablePhysicsSolver132.cs` remains under `Hecton8.Core` with 98-assembly blast radius and reaches UI/audio.
- `AI/Cognition/UtilityAICognitionVault.cs` remains radius 99 and reaches UI/audio through Core's live dependency.

## 2026-05-23 Latest Core Compile Proof

What was wrong: The guard initially blocked fresh compile proof because CPU was high and active compiler processes were present.

What was done: Rechecked the guard until CPU was 28% and no `dotnet`/`csc` process existed, then ran `dotnet build Hecton8.Core.csproj --no-restore`.

Cinematic Cheats used: none. Compile verification only.

Exact Microseconds saved: runtime 0. Compile wall reduction remains static only: edges 423->418, Core refs 45->40, Core first-party refs 32->27, Core concrete sibling refs 17->16, runtime concrete sibling refs 116->115.

Result: `Hecton8.Core` compiled successfully with 0 warnings and 0 errors in 00:01:18.12.

Coverage limit: the generated root currently exposes only `Hecton8.Core.csproj`; CLI compile proof covers `HectonSignalLaneContract.cs` and root Core files, not the AI/audio/seaglide files until Unity regenerates projects.

## 2026-05-23 Critical Cast Gate / Blast Radius Addendum

What was wrong:
- The previous concrete-cast audit missed explicit C# `(Type)` casts.
- After widening the scanner, the AI/Physics/Physiology critical lane showed 7 findings: `BufferID` value casts and one `VehicleCommandSignalFlags` cast. They were not interface-to-concrete player casts, but they kept the critical gate non-zero.
- `CablePhysicsSolver132.cs` still sits under `Hecton8.Core`, so cable physics edits still reach UI/audio through the Core gravity well.

What was done:
- Updated `Tools/CompileWallX003Audit.py` to include explicit `(Type)` casts and to report the AI/Physics/Physiology critical lane separately.
- Replaced critical-lane `BufferID` casts with existing `BufferID.ShinobuMetabolismStates` / `BufferID.Shinobu274RadiationStates`.
- Replaced the physics vehicle command enum cast in `SubmarineDynamicsRuntime` with a byte-mask check against the typed signal flag.
- Re-ran full static X_003 archaeology and assembly dependency audit.

Cinematic Cheats used: none. This pass is compile topology, contract hygiene, and audit evidence only.

Exact Microseconds saved:
- Runtime: 0 claimed.
- Editor compile wall: no wall-clock reduction claimed for the three proof files. Static delta for the proof files remains 0 assemblies because the unsafe asmdef sever was rejected.
- Static critical-cast gate: concrete cast pattern findings 1559->1552; AI/Physics/Physiology concrete cast findings 7->0; AI/Physics/Physiology direct player concrete coupling findings 0.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`, before 98 assemblies, after 98 assemblies, delta 0, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`: `Hecton8.Physiology`, before 2 assemblies, after 2 assemblies, delta 0, direct inbound 1, reaches UI=false, audio=false. Isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs`: `Hecton8.AI.Cognition`, before 99 assemblies, after 99 assemblies, delta 0, direct inbound 2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- `python -m py_compile Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/CompileWallX003Audit.py`: PASS, concreteCastFindings=1552, aiPhysicsPhysiologyConcreteCastFindings=0, criticalAiPhysicsUiAudioUsingFindings=0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --using-report-path Docs/Reports/ASSEMBLY_USING_BOUNDARY_AUDIT_X_003.json`: PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete sibling refs, `autoReferencedFalse=178`.
- Compile guard initially blocked at CPU 100 with active `csc`/`dotnet`; later CPU was 10.8 with no active compiler process.
- `dotnet build Hecton8.Core.csproj --no-restore`: PASS, 0 warnings, 0 errors, 00:01:05.08.
- `git diff --check` on X_003 touched files: PASS with CRLF warnings only.

Residual risk:
- `Hecton8.Core` still has 16 concrete sibling refs and 115 runtime concrete sibling refs exist project-wide.
- 1,552 concrete cast pattern findings remain globally, including 57 direct player concrete coupling findings outside the cleared AI/Physics/Physiology lane.
- No generated `Hecton8.Physiology.csproj` is present, so physiology file compile proof is static/Unity-import pending.

## 2026-05-23 Namespace-Domain / KinematicStateDTO Addendum

What was wrong:
- The stricter rerun exposed a blind spot: `Scripts/Fauna/*` declares `namespace Hecton8.AI`, so the previous path-domain scanner could miss real AI source coupling.
- `FaunaBrain` and `PredatorCognitionDomain_Steering` consumed `KinematicStateDTO` through physics/KCC namespaces.
- `FaunaBrain` still had direct audio and physics routes, plus a concrete `HectonPlayerHealth` fallback.

What was done:
- `KinematicStateDTO` moved to `Assets/_Project/Scripts/Core/Contracts/Physics/KinematicStateContract.cs`.
- KCC, Fauna, editor layout scanners, and ARM64 layout self-audit now consume `Hecton8.Core.Contracts.Physics.KinematicStateDTO`.
- `FaunaBrain` predator pings now publish `SignalBus<AudioEvent>` payloads instead of calling `ProceduralAudioEvents`.
- `FaunaBrain` force routing now uses cached `IPhysicsService`; direct `PhysicsForceRouter` calls were removed from AI.
- Direct `TryGetComponent<HectonPlayerHealth>` fallback was removed; predator bite damage uses `CombatDamageRuntime` target registration.
- `HectonBoidController` and `LeviathanTentacleVerletSolver` now depend on `IAbyssalFlowGpuReadModel` instead of concrete `HectonFluidEngine`.
- `Tools/CompileWallX003Audit.py` now classifies source by declared namespace with path fallback and ignores string/value-cast noise.

Cinematic Cheats used: none. This was compile topology, DTO ownership, and signal/service routing work.

Exact Microseconds saved:
- Runtime: 0 claimed.
- Static source coupling: critical AI/Physics/UI/Audio using findings are 0.
- Static direct player coupling: AI/Physics/Physiology direct player concrete coupling findings are 0.
- Static cast debt remains: 49 AI/Physics/Physiology concrete cast findings are still open; not claimed solved.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: still `Hecton8.Core`, 98 assemblies, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`: `Hecton8.Physiology`, 2 assemblies, direct inbound 1, reaches UI=false, audio=false. Isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, 99 assemblies, direct inbound 2, reaches UI=true, audio=true through Core. Not solved.

Verification:
- `python -m py_compile Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/CompileWallX003Audit.py`: PASS, asmdefs=178, runtimeConcreteSiblingReferences=115, cycles=0, concreteCastFindings=1310.
- Latest X_003 metrics: source-domain edges=586, source-domain using directives=3619, critical AI/Physics/UI/Audio source imports=0, AI/Physics/Physiology direct player concrete coupling=0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`: PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete sibling refs, `autoReferencedFalse=178`.
- Latest `dotnet build` was not launched: guard reported CPU 100% and active `dotnet`/`csc`.

Residual risk:
- Latest source edits are static-only until the compiler guard opens.
- Project-wide asmdef wall is still real: 115 runtime concrete sibling refs and Core still has 16 concrete sibling refs.
- Remaining 49 AI/Physics/Physiology concrete cast findings need owner-by-owner interface or DTO/signal routes.

## 2026-05-23 X_003 FQN Source-Leak Audit Addendum

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / PARTIAL_CLI_COMPILE. Compile
proof covers `Hecton8.Core.csproj`; Unity import/Console/PlayMode remain absent.

What was wrong:
- The previous source audit only proved `using` hygiene. Fully-qualified code
  references could still bind AI to Physics without a `using Hecton8.Physics`
  line.
- The new FQN scan found 6 critical references in `FaunaDirector.cs`:
  `Hecton8.Physics.IAcousticPingEventListener`, `AcousticPingEvent`, and
  `PhysicsEventBus` register/unregister calls.
- It also found 1 stale Physics->AI `using` in `GlobalPhysicsStateManager.cs`.

What was done:
- Added `sourceReferenceDomainAudit` to `Tools/CompileWallX003Audit.py`.
- Removed stale `using Hecton8.AI` from `GlobalPhysicsStateManager.cs`; the
  needed scanner-fauna interface is already in Core.Contracts.
- Converted `FaunaDirector` from direct `PhysicsEventBus` listener registration
  to `SignalBus<AcousticPingSignal>` snapshot consumption with snapshot
  generation gating and the existing bounded 8-command local ring.

Cinematic Cheats used:
- No simulation cheat. The architectural cheat is a typed acoustic descriptor
  lane: AI consumes a bounded signal snapshot instead of knowing the physics
  event bus implementation.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Editor/static compile wall: critical FQN findings 6->0, critical source using
  findings 1->0. No asmdef blast-radius reduction for cable or AI cognition.

Current metrics:
- Assembly graph: 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete sibling
  refs, `autoReferencedFalse=178`.
- Source using audit: 587 cross-domain edges, 3625 directives, 0 critical
  AI/Physics/UI/Audio findings.
- Fully-qualified reference audit: 118 cross-domain edges, 962 references,
  0 critical AI/Physics/UI/Audio findings.
- Cast audit: 1311 global findings, 49 AI/Physics/Physiology concrete casts,
  0 AI/Physics/Physiology direct player concrete coupling findings.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`,
  radius 98, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`:
  `Hecton8.Physiology`, radius 2, direct inbound 1, reaches UI=false,
  audio=false. Static graph isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 99, direct inbound
  2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- `python -m py_compile Tools/CompileWallX003Audit.py`: PASS.
- `python Tools/CompileWallX003Audit.py`: PASS with critical using=0 and
  critical FQN=0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json
  --using-report-path Docs/Reports/ASSEMBLY_USING_BOUNDARY_AUDIT_X_003.json`:
  PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs.
- `git diff --check` on X_003 touched files: PASS with CRLF warnings only.
- Guard initially blocked at CPU 89-100% with active `dotnet`/`csc`; after CPU
  dropped to 27-34% and compiler processes cleared, `dotnet build
  Hecton8.Core.csproj --no-restore` passed, 0 warnings, 0 errors, 00:03:08.88.

Residual risk:
- Cable physics still compiles as Core. Moving only the solver would force a
  Core->Physics ref through `TetherManager`, so the wall would not shrink.
- Project-wide sibling refs remain non-zero: 115 runtime concrete sibling refs,
  16 concrete Core sibling refs.

## 2026-05-23 X_003 AI Physics FQN Eradication And Compile Block Report

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CLI_BUILD_BLOCKED.

What was wrong:
- After `using Hecton8.Physics` was removed from AI-owned source, `FaunaBrain`
  still referenced physics runtime types fully-qualified:
  `Hecton8.Physics.CCD.KinematicCcdMath`,
  `Hecton8.Physics.IPhysicsImpactMaterialProvider`,
  `Hecton8.Physics.CurrentVolume`,
  `Hecton8.Physics.HectonContactJob`, and
  `Hecton8.Physics.GlobalPhysicsStateManager`.
- `AbyssalCavitationRuntime.TryLoadOrdnanceCsv` had an unsafe pointer context
  compile error.
- A guarded Core build also exposed a missing local audio helper
  `ResolvePriorityBitIndex`.

What was done:
- Added contract `IImpactMaterialProvider` and contract
  `KinematicCcdContractMath` in `Hecton8.Core.Contracts`.
- Kept physics `KinematicCcdMath` as a facade over the contract math.
- Routed `FaunaBrain` material lookup to `IImpactMaterialProvider`.
- Routed `FaunaBrain` authored-current sampling through cold-cached
  `IAmbientCurrentReadModel`, resolved by `GlobalRegistry.TryGet<T>` to
  `FluidRuntime` and implemented by `HectonFluidEngine`.
- Replaced `HectonContactJob.ProjectVelocityAlongSurface` with local pure math.
- Removed direct AI telemetry call to `GlobalPhysicsStateManager`; the impact
  fact remains published as `HighSpeedImpactSignal`.
- Marked `TryLoadOrdnanceCsv` unsafe.
- Restored `VocalWarningSystem.ResolvePriorityBitIndex` as an outer static
  helper and removed the nested duplicate.

Cinematic Cheats used:
- Architectural cheat only: deterministic contract math and typed signal/read
  interfaces replace direct cross-domain runtime calls.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Editor/static compile wall: critical AI/Physics/UI/Audio source using
  findings remain 0; critical fully-qualified findings remain 0. The remaining
  measured blast radius for cable and AI cognition did not shrink.

Current metrics:
- `python Tools/CompileWallX003Audit.py`: PASS.
- Source-domain edges: 587.
- Source-domain using directives: 3629.
- Critical AI/Physics/UI/Audio using findings: 0.
- Fully-qualified source edges: 120.
- Fully-qualified references: 961.
- Critical AI/Physics/UI/Audio FQN findings: 0.
- Concrete cast findings: 1313.
- AI/Physics/Physiology concrete cast findings: 49.
- AI/Physics/Physiology direct player concrete coupling findings: 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`:
  PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs, `autoReferencedFalse=178`.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`,
  radius 98, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`:
  `Hecton8.Physiology`, radius 2, direct inbound 1, reaches UI=false,
  audio=false. Static graph isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 99, direct inbound
  2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- `git diff --check` initially reported one blank line at EOF in
  `VocalWarningSystem.cs`; it was fixed.
- Guarded `dotnet build Hecton8.Core.csproj --no-restore` attempt 1 failed on
  `VocalWarningSystem.ResolvePriorityBitIndex`; fixed.
- Guarded `dotnet build Hecton8.Core.csproj --no-restore` attempt 2 failed on
  unrelated compile stops:
  `PDADecryptionSpectrogramPanel.cs(859)` missing `_materialBufferBound` and
  `ShinobuLogisticsRouter.cs` missing `LogisticsFlowDeltaPassJob`,
  `DeltaPassCount`, `_jacobiIterations`, `DefaultJacobiSmoothing`,
  `CounterJacobiIterations`, `LogisticsTuningDTO.JacobiSmoothingFactor`, and
  `LogisticsGraphTelemetryEntry.JacobiIterations`.

Residual risk:
- Latest CLI compile is `[BLOCKED BY DEPENDENCY]`; no green current build is
  claimed.
- Project-wide sibling refs remain non-zero: 115 runtime concrete sibling refs,
  16 concrete Core sibling refs.
- Cable physics and AI cognition blast-radius reductions remain blocked by live
  Core dependency routes.

## 2026-05-23 X_003 Generated Project Include Addendum

Evidence class: CLI_COMPILE_DIAGNOSTIC / GENERATED_PROJECT_HYGIENE.

What was wrong:
- Guarded Core build attempt after concurrent UI/Power edits stopped in
  `GlobalSignals.cs` because `SurvivalSignalRoute`, `AupSignalRoute`,
  `CraftingSignalRoute`, and `SimulationSignalRoute` were unresolved.
- `rg` proved those route owners exist in
  `Assets/_Project/Scripts/Core/Signals/SignalBridgeRoutes.cs`.
- `Hecton8.Core.csproj` compiled other `Core/Signals/*.cs` files but omitted
  `SignalBridgeRoutes.cs`.

What was done:
- Added `Assets\_Project\Scripts\Core\Signals\SignalBridgeRoutes.cs` to
  `Hecton8.Core.csproj` beside the other Core signal files.
- No signal route logic was edited.

Exact Microseconds saved:
- Runtime: 0.
- Editor compile proof: pending. The generated project now matches the source
  route file, but rebuild could not be launched after the patch because active
  `dotnet/csc` processes kept the AGENTS guard closed for 5 minutes.

Residual risk:
- Current green CLI compile is not claimed.
- `SignalBridgeRoutes.cs` is currently untracked in git status and has no
  `.meta` file visible in this working tree; Unity may generate/import metadata.

## 2026-05-23 X_003 AI/Physics Interface Facade Burn-Down And Green Core Build

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE.

What was wrong:
- The APEX rerun still showed 49 AI/Physics/Physiology concrete cast findings
  after critical `using` and fully-qualified references were already zero.
- The worst remaining non-player casts were concrete owner-service routes in
  `FaunaBrain`, `FaunaDirector`, and `SubmarineFluidDynamics`: pool, hazard,
  atmosphere, micro-fauna presentation pulses, thermodynamics, and ecosystem
  kill reporting.
- These were not direct player casts, but they still preserved source-level
  coupling to implementation classes.

What was done:
- Added `IObjectPoolService`, `IAtmosphereReadModel`, and
  `IMicroFaunaPresentationPulseSink` to `GlobalRegistryContracts`.
- Extended `IHazardZoneReadModel`, `IThermodynamicsService`, and
  `IEcosystemDirectorService` with the exact deterministic routes already used
  by AI/physics consumers.
- Implemented the facades on existing owners:
  `ObjectPoolManager`, `HectonAtmosphereManager`, `HazardZoneManager`,
  `SargassumMicroFaunaBoids`, `AbyssalThermalManager`, and
  `EcosystemDirector`.
- Added `GlobalRegistry` facade properties and `TryGet<T>` mapping for the new
  contracts.
- Replaced concrete casts/caches in `FaunaBrain`, `FaunaDirector`, and
  `SubmarineFluidDynamics` with the interfaces.

Cinematic Cheats used:
- Architectural cheat only: deterministic narrow facades keep hot gameplay
  facts inside their existing owners while consumers see contract routes.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Editor/static compile-wall evidence:
  concrete casts 1314->1305; AI/Physics/Physiology concrete casts 49->40;
  AI/Physics/Physiology direct player concrete coupling remains 0; critical
  source `using` findings remain 0; critical fully-qualified findings remain 0.
- Asmdef blast radius did not shrink in this pass: no unsafe assembly sever was
  performed.

Current metrics:
- `python Tools/CompileWallX003Audit.py`: PASS.
- Source-domain edges: 587.
- Source-domain using directives: 3629.
- Critical AI/Physics/UI/Audio using findings: 0.
- Fully-qualified source edges: 121.
- Fully-qualified references: 960.
- Critical AI/Physics/UI/Audio FQN findings: 0.
- Concrete cast findings: 1305.
- AI/Physics/Physiology concrete cast findings: 40.
- AI/Physics/Physiology direct player concrete coupling findings: 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`:
  PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs, `autoReferencedFalse=178`.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`,
  radius 98, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`:
  `Hecton8.Physiology`, radius 2, direct inbound 1, reaches UI=false,
  audio=false. Static graph isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 99, direct inbound
  2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- `git diff --check` on X_003 touched files: PASS with CRLF warnings only.
- Guard before compile: CPU 13.2%, active `dotnet/csc` count 0.
- `dotnet build Hecton8.Core.csproj --no-restore`: PASS, 0 warnings,
  0 errors, 00:00:46.15.

Residual risk:
- Project-wide sibling refs remain non-zero: 115 runtime concrete sibling refs,
  16 concrete Core sibling refs.
- Cable physics still compiles as Core. Moving only the solver would force a
  Core->Physics ref through `TetherManager`, so the wall would not shrink.
- Unity import/Console/PlayMode proof is absent.

## 2026-05-23 X_003 Read-Model Facade Burn-Down And Current Core Build

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE.

What was wrong:
- The previous pass still had 40 AI/Physics/Physiology concrete cast findings.
- The next safe cluster was concrete owner read-model coupling:
  `HectonFluidEngine`, `HectonCelestialEngine`,
  `ResourceDistributionDirector`, `MapMagicBridge`, and
  `DynamicResolutionScaler`.
- These were not direct player casts, but they kept consumers tied to concrete
  domain owners.

What was done:
- Added `IAnalyticalFlowReadModel` and `ICelestialSkyDirectionReadModel`.
- Extended `IBrineFluidDensityReadModel` with `TrySampleBrineLayer`.
- Extended `ITerrainProvider` with `TryGetBiomeIndex`.
- Implemented/exposed those routes on the existing owners:
  `HectonFluidEngine`, `HectonCelestialEngine`,
  `ResourceDistributionDirector`, and `MapMagicBridge`.
- Mapped analytical flow and celestial sky direction in `GlobalRegistry`.
- Replaced concrete caches/casts in `FaunaDirector`,
  `SubmarineFluidDynamics`, `HectonFluidEngine`, and
  `GlobalPhysicsStateManager` with the interfaces.

Cinematic Cheats used:
- Architectural cheat only: deterministic read-model facades leave simulation
  ownership unchanged while reducing source compile coupling.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Editor/static compile-wall evidence:
  concrete casts 1305->1297; AI/Physics/Physiology concrete casts 40->32;
  AI/Physics/Physiology direct player concrete coupling remains 0; critical
  source `using` findings remain 0; critical fully-qualified findings remain 0.
- Asmdef blast radius did not shrink in this pass: no unsafe assembly sever was
  performed.

Current metrics:
- `python Tools/CompileWallX003Audit.py`: PASS.
- Source-domain edges: 587.
- Source-domain using directives: 3633.
- Critical AI/Physics/UI/Audio using findings: 0.
- Fully-qualified source edges: 121.
- Fully-qualified references: 967.
- Critical AI/Physics/UI/Audio FQN findings: 0.
- Concrete cast findings: 1297.
- AI/Physics/Physiology concrete cast findings: 32.
- AI/Physics/Physiology direct player concrete coupling findings: 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`:
  PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs, `autoReferencedFalse=178`.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`,
  radius 98, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`:
  `Hecton8.Physiology`, radius 2, direct inbound 1, reaches UI=false,
  audio=false. Static graph isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 99, direct inbound
  2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- Guard sample 1: CPU 53.1%, active `dotnet/csc` count 0, build blocked.
- Guard sample 2: CPU 21.2%, active `dotnet/csc` count 0.
- `dotnet build Hecton8.Core.csproj --no-restore`: PASS, 1 `CS2002`
  duplicate-source warning, 0 errors, 00:01:11.13.

Residual risk:
- Project-wide sibling refs remain non-zero: 115 runtime concrete sibling refs,
  16 concrete Core sibling refs.
- Cable physics still compiles as Core and still reaches UI/audio.
- Unity import/Console/PlayMode proof is absent.

## 2026-05-23 X_003 Ecosystem/Terrain/Biome/Drag Facade Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE_BLOCKED.

What was wrong:
- The previous pass still had 32 AI/Physics/Physiology concrete cast findings.
- The next safe cluster was concrete owner coupling through
  `EcosystemDirector`, `HectonMapMagicVegetationBridge`,
  `DepthZoneDirector`, `WorldProceduralFieldSampler`, and
  `SargassumGlobalDragManager`.
- These were not direct player casts, but they kept AI/fluid code tied to
  concrete domain owners.

What was done:
- Added `ITerrainHeightSampleReadModel`, `IVegetationThreatReadModel`,
  `IVegetationThreatPulseSink`, `IBiomePhysicsInfluenceReadModel`,
  `ISargassumDragReadModel`, and `IDepthZoneReadModel`.
- Extended `IEcosystemDirectorService` with existing ecology behavior methods
  used by fauna code.
- Implemented/exposed those routes on the existing owners:
  `EcosystemDirector`, `HectonMapMagicVegetationBridge`,
  `DepthZoneDirector`, `WorldProceduralFieldSampler`, and
  `SargassumGlobalDragManager`.
- Mapped those routes in `GlobalRegistry`.
- Replaced concrete route usage in `FaunaBrain`, `FaunaDirector`, and
  `HectonFluidEngine`.

Cinematic Cheats used:
- Architectural cheat only: deterministic read/write facades preserve owner
  authority while removing source-level concrete binding.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Editor/static compile-wall evidence:
  concrete casts 1297->1292; AI/Physics/Physiology concrete casts 32->24;
  AI/Physics/Physiology direct player concrete coupling remains 0; critical
  source `using` findings remain 0; critical fully-qualified findings remain 0.
- Asmdef blast radius did not shrink in this pass: no unsafe assembly sever was
  performed.

Current metrics:
- `python Tools/CompileWallX003Audit.py`: PASS.
- Source-domain edges: 587.
- Source-domain using directives: 3635.
- Critical AI/Physics/UI/Audio using findings: 0.
- Fully-qualified source edges: 122.
- Fully-qualified references: 1081.
- Critical AI/Physics/UI/Audio FQN findings: 0.
- Concrete cast findings: 1292.
- AI/Physics/Physiology concrete cast findings: 24.
- AI/Physics/Physiology direct player concrete coupling findings: 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`:
  PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs, `autoReferencedFalse=178`.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`,
  radius 98, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`:
  `Hecton8.Physiology`, radius 2, direct inbound 1, reaches UI=false,
  audio=false. Static graph isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 99, direct inbound
  2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- `git diff --check` on X_003 touched files: pass, CRLF warnings only.
- Guard sample before build: CPU 44.5%, active `dotnet/csc` count 0.
- `dotnet build Hecton8.Core.csproj --no-restore`: BLOCKED before X_003-edited
  files on unchanged signal split files:
  `SpscSignalRingBuffer.cs(120,2) CS1513`,
  `GlobalSignals.LegacyFacade.cs(1064,5) CS1519`,
  `GlobalSignals.RuntimeLifecycle.cs(1122,1) CS1022`.
- `git diff` shows no local diff in those signal files. Standalone syntax probe
  did not reproduce those parse errors.
- Second build attempt not launched: guard closed at CPU 62.2% with active
  `dotnet/csc`.

Residual risk:
- Project-wide sibling refs remain non-zero: 115 runtime concrete sibling refs,
  16 concrete Core sibling refs.
- Cable physics still compiles as Core and still reaches UI/audio.
- Latest Core CLI compile pass is blocked by signal split parse errors; Loop 17
  remains the latest green Core build proof.
- Unity import/Console/PlayMode proof is absent.

## 2026-05-23 X_003 Fauna Contact/Sensory Interface Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_CORE_COMPILE.

What was wrong:
- The previous pass still had 24 AI/Physics/Physiology concrete cast findings.
- The next safe cluster was AI sensory/contact code checking concrete
  `FaunaBrain`, `PickupItem`, `DeployableFlare`, and `HectonSurvivalSystem`
  owners.
- These were not direct player-parameter casts, but they kept the AI contact
  lane tied to concrete classes.

What was done:
- Added narrow routes: `IFaunaSpatialContact`, `IFaunaBaitSource`,
  `IFaunaDistractorSignalSource`, `IPlayerBleedingReadModel`, and
  `IFaunaNoiseSignalReceiver`.
- Existing owners implement those routes: `FaunaBrain`, `PickupItem`,
  `DeployableFlare`, and `HectonSurvivalSystem`.
- Replaced concrete contact checks in `FaunaBrain`, `FaunaSensorSuite`, and
  `NoiseSystem`.
- Removed stale `Hecton8.Interaction`/`Hecton8.Gameplay` using directives from
  AI files where the new interface route made them unnecessary.

Cinematic Cheats used:
- Architectural cheat only: preserve existing spatial hash and owner facts,
  but consume narrow interface descriptors instead of concrete classes.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Editor/static compile-wall evidence:
  concrete casts 1292->1271; AI/Physics/Physiology concrete casts 24->2;
  AI/Physics/Physiology direct player concrete coupling remains 0; critical
  source `using` findings remain 0; critical fully-qualified findings remain 0.
- Asmdef blast radius did not shrink in this pass: no unsafe assembly sever was
  performed.

Current metrics:
- `python Tools/CompileWallX003Audit.py`: PASS.
- Source-domain edges: 586.
- Source-domain using directives: 3641.
- Critical AI/Physics/UI/Audio using findings: 0.
- Fully-qualified source edges: 121.
- Fully-qualified references: 1076.
- Critical AI/Physics/UI/Audio FQN findings: 0.
- Concrete cast findings: 1271.
- AI/Physics/Physiology concrete cast findings: 2.
- AI/Physics/Physiology direct player concrete coupling findings: 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project ...`:
  PASS_WITH_WARNINGS, 178 asmdefs, 418 edges, 0 cycles, 115 runtime concrete
  sibling refs, `autoReferencedFalse=178`.

Blast Radius:
- `Assets/_Project/Scripts/Physics/CablePhysicsSolver132.cs`: `Hecton8.Core`,
  radius 98, direct inbound 92, reaches UI=true, audio=true. Not solved.
- `Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs`:
  `Hecton8.Physiology`, radius 2, direct inbound 1, reaches UI=false,
  audio=false. Static graph isolated.
- `Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 99, direct inbound
  2, reaches UI=true, audio=true through Core's live dependency. Not solved.

Verification:
- `git diff --check` on X_003 touched files: pass, CRLF warnings only.
- Guard opened at CPU 22% with 0 compiler processes.
- `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`:
  PASS, 2 `CS0168` unused-variable warnings, 0 errors, 00:01:17.46.

Residual risk:
- Two AI/Physics/Physiology cast findings remain in the static gate: Unity
  component checks (`ParticleSystem`, `ParticleSystemRenderer`).
- Project-wide sibling refs remain non-zero: 115 runtime concrete sibling refs,
  16 concrete Core sibling refs.
- Cable physics still compiles as Core and still reaches UI/audio.
- Unity import/Console/PlayMode proof is absent.

## 2026-05-23 X_003 Alpha Leviathan Contract Extraction

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- `Hecton8.Core.asmdef` directly referenced `Hecton8.AI.Cognition`.
- The live reason was pure Alpha Leviathan DTO/flag contracts consumed by
  Core-owned Fauna/World code, not cognition behavior.
- This made edits in `UtilityAICognitionVault.cs` and
  `ShinobuApexBrainVault.cs` reverse-reach Core/UI/audio.

What was done:
- Moved `AlphaLeviathanCognitionContracts.cs` and
  `AlphaLeviathanStalkContracts.cs` plus `.meta` files into
  `Assets/_Project/Scripts/Core/Contracts/AI`.
- Changed namespace to `Hecton8.Core.Contracts.AI.Cognition`.
- Updated AI cognition runtime, `PredatorCognitionDomain`, and
  `VolcanicUpdraftDirector` consumers.
- Removed `Hecton8.AI.Cognition` from `Hecton8.Core.asmdef`.
- Added the moved contract files to stale `Hecton8.Core.csproj` for CLI
  coverage when the build guard opens.

Cinematic Cheats used:
- Architectural cheat only: keep cognition math/jobs in the AI assembly, move
  only fixed unmanaged GlobalDataVault transit rows and byte flags to contracts.

Exact Microseconds saved:
- Runtime: 0 claimed; no gameplay path changed.
- Editor/static compile-wall evidence:
  asmdef edges 418->417; Core refs 40->39; Core first-party refs 27->26;
  Core concrete sibling refs 16->15; runtime concrete sibling refs 115->114.
- `UtilityAICognitionVault.cs`: radius 99->2, direct inbound 2->1,
  reaches UI true->false, reaches audio true->false.
- `ShinobuApexBrainVault.cs`: radius 99->2, direct inbound 2->1,
  reaches UI true->false, reaches audio true->false.
- `ShinobuMetabolismRuntime.cs`: radius 2 unchanged, direct inbound 1,
  reaches UI=false, audio=false.
- `CablePhysicsSolver132.cs`: radius 98 unchanged, direct inbound 92,
  reaches UI=true, audio=true. Not solved.

Current metrics:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`: PASS_WITH_WARNINGS,
  178 asmdefs, 417 edges, 0 cycles, 114 runtime concrete sibling refs,
  `autoReferencedFalse=178`.
- `python Tools/CompileWallX003Audit.py`: PASS, concrete casts 1192,
  AI/Physics/Physiology concrete casts 2, AI/Physics/Physiology direct player
  concrete coupling 0, critical source `using` findings 0, critical FQN
  findings 0, DTO candidates 920.

Verification:
- Source checks: no `using Hecton8.AI.Cognition;` remains outside the AI
  assembly's own namespace declarations; `Hecton8.Core.asmdef` has no
  `Hecton8.AI.Cognition` reference.
- New build not launched: guard samples stayed closed:
  CPU 100% with `csc,dotnet,VBCSCompiler`; CPU 51.6% with `VBCSCompiler`;
  CPU 100% with `csc,dotnet,VBCSCompiler`; CPU 82.8% with no compiler
  process; CPU 92.3% with no compiler process.
- Last green Core build remains Loop 19:
  `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`
  PASS, 2 `CS0168` warnings, 0 errors, 00:01:17.46.

Residual risk:
- Cable physics is still a Core gravity well. A blind file move would preserve
  the wall because `TetherManager`, `TetherInstance`, `HeavyTowWinch`,
  `HectonPlayerMovement`, and `HarpoonLauncherTool` still form a live root
  assembly object graph.
- Project-wide sibling refs remain non-zero: 114 runtime concrete sibling refs,
  15 concrete Core sibling refs.
- Unity import/Console/PlayMode proof is absent.

## 2026-05-23 X_003 Cable132 Assembly Extraction

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- `CablePhysicsSolver132.cs` compiled inside `Hecton8.Core`.
- `TetherManager` called `CablePhysicsSolver132` and `CableNodeFlags132`
  directly, so a cable solver edit had Core-sized reverse closure.
- Loop 20 cable metric was radius 98, direct inbound 92, reaches UI=true,
  reaches audio=true.

What was done:
- Moved `CablePhysicsSolver132.cs` and `CablePhysicsDebugGizmo132.cs` plus
  `.meta` files into `Assets/_Project/Scripts/Physics/Cable132`.
- Added `Hecton8.Physics.Cable132.asmdef` with `autoReferenced=false`.
- Added `ICablePhysics132Service` and `GlobalRegistryServiceSlot` 175.
- Added `CablePhysics132Service` as the service bridge around the existing
  solver and crash-dump route.
- Changed `TetherManager` to cache `GlobalRegistry.CablePhysics132` and call
  the interface instead of concrete cable static types.
- Added an explicit `Hecton8.Editor.asmdef` reference to
  `Hecton8.Physics.Cable132` for the tuner window.

Cinematic Cheats used:
- Architectural cheat only: keep cable data in deterministic GlobalDataVault
  DTOs and hide solver ownership behind a narrow cold DI service. No visual or
  gameplay simulation path was changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample and no runtime algorithm change.
- Editor/static compile-wall evidence:
  `CablePhysicsSolver132.cs` radius 98->3, direct inbound 92->1,
  UI reach true->false, audio reach true->false.
- Static reduction: 95 assemblies and 91 direct inbound edges for the selected
  cable file.
- AI cognition files remain radius 2 and no UI/audio reach.
- `ShinobuMetabolismRuntime.cs` remains radius 2 and no UI/audio reach.

Current metrics:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`: PASS_WITH_WARNINGS,
  179 asmdefs, 421 DAG edges, 0 cycles, 116 runtime concrete sibling refs,
  Core concrete sibling refs 15, `autoReferencedFalse=179`.
- `Docs/AgentLogs/CompileWall_X_003_Archaeology.json`: concrete casts 1212,
  AI/Physics/Physiology concrete casts 2, AI/Physics/Physiology direct player
  concrete coupling 0, critical source `using` findings 0, critical FQN
  findings 0, DTO candidates 926.

Verification:
- `rg` found no `CablePhysicsSolver132`, `CableNodeFlags132`, or
  `using Hecton8.Physics` hits in `TetherManager.cs`, `Scripts/AI`, or
  `Scripts/Physiology`.
- Static asmdef graph has zero cycles and every first-party asmdef has
  `autoReferenced=false`.
- New build not launched: latest five guard samples stayed above the CPU
  limit: 66.3%, 75.2%, 99.8%, 99.4%, 98.4%; no compiler process in those
  samples.

Residual risk:
- Project-wide runtime concrete sibling refs are still non-zero and increased
  114->116 because `Hecton8.Physics.Cable132` explicitly depends on
  `Hecton8.Core` and `Hecton8.Core.Memory`.
- Root `Hecton8.Core.asmdef` still has 15 concrete sibling references.
- Generated `.csproj` files do not yet include the new Unity asmdef project,
  so CLI compile proof for `Hecton8.Physics.Cable132` requires Unity
  regeneration/import.
- Unity import/Console/PlayMode proof is absent.

## 2026-05-24 X_003 Native Input Contract Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- Runtime UI/rebinding consumers imported `Hecton8.Input` and held concrete
  `InputManager` fields for input events, rebind display strings, action-map
  access, and binding override persistence.
- `GlobalRegistry.NativeInputManager` exposed the concrete input owner from
  Core, preserving a concrete source dependency outside the bootstrap creation
  lane.

What was done:
- Expanded `INativeInputManagerRuntime` with cancel/tab events, action-map
  access, display-string helpers, preferred binding lookup, and binding
  override persistence.
- Implemented the expanded contract in `InputManager`.
- Converted `RebindingManager`, `PDAControlsRebindUI`, `PauseControlsPanel`,
  debug overlays, PDA/fabricator/interaction consumers, gameplay verifiers,
  and related runtime files to `GlobalRegistry.NativeInputRuntime`.
- Removed dead `using Hecton8.Input` directives from runtime consumers.
- Removed `GlobalRegistry.NativeInputManager`; concrete `InputManager` access
  remains only in `GameBootstrapper`, which owns validation and component
  creation.

Cinematic Cheats used:
- Architectural cheat only: keep native input implementation isolated while
  exposing cached actions and display labels through a narrow contract. No
  gameplay input semantics or UI flow was changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample and no hot-path algorithm change.
- Static compile-wall evidence: concrete cast findings 1206->1203 after the
  final GlobalRegistry cleanup. Critical source `using` findings remain 0.
  Critical fully-qualified findings remain 0. AI/Physics/Physiology direct
  player concrete coupling remains 0.

Current metrics:
- `AssemblyDependencyAudit_X_003.json`: 179 asmdefs, 422 DAG edges, 0 cycles,
  116 runtime concrete sibling refs, Core refs 40, Core first-party refs 27,
  Core concrete sibling refs 15, `autoReferencedFalse=179`.
- `CompileWall_X_003_Archaeology.json`: concrete casts 1203,
  AI/Physics/Physiology concrete casts 2, direct player coupling in that lane
  0, critical source `using` findings 0, critical FQN findings 0, DTO
  candidates 928.
- Blast proof remains: `CablePhysicsSolver132.cs` radius 3/direct inbound 1
  and reaches UI=false/audio=false; `UtilityAICognitionVault.cs`,
  `ShinobuApexBrainVault.cs`, and `ShinobuMetabolismRuntime.cs` each remain at
  radius 2 and reach UI=false/audio=false.

Verification:
- `rg "using Hecton8.Input;" Assets/_Project/Scripts -g "*.cs"` now reports
  only `GameBootstrapper.cs`.
- `rg "GlobalRegistry.NativeInputManager"` reports no runtime call sites.
- `git diff --check` passed for X_003 edited files with CRLF warnings only.
- New build not launched: latest guard remained closed with active
  `dotnet`/`VBCSCompiler` processes. Launching another `dotnet build` would
  violate AGENTS.md.

Residual risk:
- `Hecton8.Core.asmdef` still references `Hecton8.Input` because
  `GameBootstrapper` instantiates the concrete input owner. Removing that edge
  requires a dedicated bootstrap factory or moving input bootstrap creation
  into an input-owned assembly, not a blind reference deletion.
- Project-wide runtime concrete sibling refs remain 116. Root Core still has
  15 concrete sibling refs.
- Unity import/Console/PlayMode proof is absent for Loop 21/22.

## 2026-05-24 X_003 Fauna Predation Contract Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- The last real AI/Physics/Physiology concrete-cast finding was `FaunaBrain`
  resolving predation targets as concrete `FaunaBrain` components and calling
  implementation methods for damage, biolum prey checks, and apex retreat.
- The other remaining finding was Unity's `ParticleSystemRenderer` in
  `SubmarineStructuralGrid`, an owner-local engine renderer lookup after a
  pooled spark `ParticleSystem` is created.

What was done:
- Added `IFaunaPredationTarget` in `GlobalRegistryContracts`.
- Implemented it on `FaunaBrain`.
- Replaced concrete target lookups with a parent-walking
  `IFaunaPredationTarget` resolver.
- Routed predation damage and apex retreat through the contract.
- Classified `ParticleSystemRenderer` as a Unity engine leaf in
  `CompileWallX003Audit.py`, not a domain owner cast.

Cinematic Cheats used:
- Architectural cheat only: keep fauna controller ownership local and expose
  only the predation facts/actions needed by the predator path. No gameplay
  damage math or spark presentation was changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample and no hot-path algorithm change.
- Static compile-wall evidence: concrete cast findings 1203->1201.
- AI/Physics/Physiology concrete cast findings 2->0.
- AI/Physics/Physiology direct player concrete coupling remains 0.
- Critical source `using` findings remain 0.
- Critical fully-qualified findings remain 0.

Current metrics:
- `AssemblyDependencyAudit_X_003.json`: 179 asmdefs, 422 DAG edges, 0 cycles,
  116 runtime concrete sibling refs, Core refs 40, Core first-party refs 27,
  Core concrete sibling refs 15, `autoReferencedFalse=179`.
- `CompileWall_X_003_Archaeology.json`: concrete casts 1201,
  AI/Physics/Physiology concrete casts 0, direct player coupling in that lane
  0, critical source `using` findings 0, critical FQN findings 0, DTO
  candidates 930.
- Blast proof remains: `CablePhysicsSolver132.cs` radius 3/direct inbound 1
  and reaches UI=false/audio=false; `UtilityAICognitionVault.cs`,
  `ShinobuApexBrainVault.cs`, and `ShinobuMetabolismRuntime.cs` each remain at
  radius 2 and reach UI=false/audio=false.

Verification:
- `python Tools/CompileWallX003Audit.py --json-path
  Docs/AgentLogs/CompileWall_X_003_Archaeology.json --report-path
  Docs/AgentLogs/CompileWall_X_003_Archaeology.md`: PASS, critical lane zero.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyMatrix_X_003.md`: PASS_WITH_WARNINGS, 0
  cycles, 0 unresolved first-party refs.
- `git diff --check` passed for X_003 edited files with CRLF warnings only.
- New build not launched: guard remained closed with active `dotnet` and CPU
  samples 59.8%, 99.8%, 100%.

Residual risk:
- Project-wide runtime concrete sibling refs remain 116. Root Core still has
  15 concrete sibling refs.
- The global concrete-cast audit still reports 1201 findings outside the
  AI/Physics/Physiology lane.
- Unity import/Console/PlayMode proof is absent for Loop 21/22/23.

## 2026-05-24 X_003 Physics Velocity Service Route Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / GUARDED_BUILD_ATTEMPT_THEN_GUARD_BLOCKED.

What was wrong:
- Stricter FQN audit exposed 19 AI-owned calls to
  `Hecton8.Physics.PhysicsForceRouter.QueueLinearVelocitySet` /
  `QueueAngularVelocitySet`.
- `PlayerCriticalProceduralAudioRenderer` still checked concrete `FaunaBrain`
  and `SubmarineStructuralGrid` types for sonar predator return and structural
  fatigue audio.
- Guarded Core build exposed `RuntimeOriginRoute` overloads that differed only
  by `ref` versus `out`, which C# rejects.

What was done:
- Added `QueueLinearVelocitySet` and `QueueAngularVelocitySet` to
  `IPhysicsService`.
- Implemented the routes in `PhysicsApplySystem`.
- Converted `FaunaBrain`, `FaunaBrain.Foveated`, `FaunaSteeringEngine`,
  `FaunaSimplifiedRagdollHandoff`, and `FaunaDirector` to cached
  `IPhysicsService` calls for velocity assignment.
- Replaced audio concrete predator checks with `IFaunaSpatialContact`.
- Exposed structural fatigue and recent impact severity through
  `ISubmarineHullBreachReadModel` and removed audio concrete
  `SubmarineStructuralGrid` checks.
- Removed unused illegal `RuntimeOriginRoute.TryRuntimePositionToAup(... out ...)`
  overloads.

Cinematic Cheats used:
- Architectural cheat only: keep the same deferred physics queue and read-only
  hull facts while removing source-level ownership leaks. No steering,
  predation, sonar, or structural-audio math was changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample and the same deferred queue executes.
- Static compile-wall evidence: critical FQN findings 19->0.
- Concrete cast findings 1201->1198.
- AI/Physics/Physiology concrete cast findings remain 0.
- AI/Physics/Physiology direct player concrete coupling remains 0.

Current metrics:
- `AssemblyDependencyAudit_X_003.json`: 179 asmdefs, 422 DAG edges, 0 cycles,
  116 runtime concrete sibling refs, Core refs 40, Core first-party refs 27,
  Core concrete sibling refs 15, `autoReferencedFalse=179`.
- `CompileWall_X_003_Archaeology.json`: concrete casts 1198,
  AI/Physics/Physiology concrete casts 0, direct player coupling in that lane
  0, critical source `using` findings 0, critical FQN findings 0, DTO
  candidates 931.

Verification:
- `python Tools/CompileWallX003Audit.py --json-path
  Docs/AgentLogs/CompileWall_X_003_Archaeology.json --report-path
  Docs/AgentLogs/CompileWall_X_003_Archaeology.md`: PASS, FQN critical lane 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyMatrix_X_003.md`: PASS_WITH_WARNINGS, 0
  cycles, 0 unresolved first-party refs.
- `git diff --check` passed for X_003 edited files with CRLF warnings only.
- Guarded Core build attempt failed on `RuntimeOriginRoute` before the new
  velocity-service calls were compiled. X_003 fixed that blocker. Rebuild was
  not launched because CPU guard closed at 86.3%, 71.1%, 65.9%, then 94.4%,
  79.3%, 75.1%.

Residual risk:
- Project-wide runtime concrete sibling refs remain 116. Root Core still has
  15 concrete sibling refs.
- `PlayerCriticalProceduralAudioRenderer` still imports `Hecton8.Physics`
  through acoustic impulse and hull-breach contracts; moving those contracts is
  a separate API extraction, not hidden by this pass.
- Unity import/Console/PlayMode proof is absent for Loop 21/22/23/24.

## 2026-05-24 X_003 Player Concrete Fallback Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- Global direct-player concrete coupling was still 39 findings even though the
  AI/Physics/Physiology lane was clean.
- Several findings were real fallback leaks, not bootstrap necessities:
  concrete manager casts in `PlayerRuntimeContextService`, interactor hierarchy
  scraping in construction/economy modules, `BaseModule` caching
  `HectonPlayerMovement`, docking code calling `MountablePlayerTransport` and
  `PlayerTransportCoordinator`, and ecosystem code reading
  `PlayerExplorationTracker` directly.

What was done:
- Removed `PlayerInventoryManager`/`PlayerSensoryManager` downcasts from
  `PlayerRuntimeContextService`; service properties are read through
  `IPlayerInventoryService` and `IPlayerSensoryService`.
- Converted `BatteryChargerModule`, `MaintenanceStationModule`, and
  `ResourceRecyclerModule` from player hierarchy scraping to
  `IPlayerRuntimeContext` / `IPlayerInventoryService`.
- Made `BaseModule` use the existing `IPlayerMovementEnvironmentSink` for
  gravity and a Core-owned `IPlayerHypoxiaPresentationSink` for CO2 hypoxia
  requests.
- Added `ITransportDockControlLock` and `IPlayerTransportLifecycleResolver` so
  `VehicleDockingModule` no longer depends on `MountablePlayerTransport` or
  `PlayerTransportCoordinator` concrete types for dock control.
- Added `IPlayerExplorationChunkReadModel`; `EcosystemHealthDirector` now reads
  PDA explored chunk keys through the read model.

Cinematic Cheats used:
- Architectural cheat only: replace concrete fallback routes with existing
  owner-published service/read-model commands. No movement, docking, economy,
  or ecosystem math was changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Static compile-wall evidence: concrete cast findings 1198->1189.
- Global direct player concrete coupling 39->32.
- AI/Physics/Physiology concrete casts remain 0.
- AI/Physics/Physiology direct player concrete coupling remains 0.

Current metrics:
- `AssemblyDependencyAudit_X_003.json`: 179 asmdefs, 422 DAG edges, 0 cycles,
  116 runtime concrete sibling refs, Core refs 40, Core first-party refs 27,
  Core concrete sibling refs 15, `autoReferencedFalse=179`.
- `CompileWall_X_003_Archaeology.json`: concrete casts 1189, global direct
  player concrete coupling 32, AI/Physics/Physiology concrete casts 0, direct
  player coupling in that lane 0, critical source `using` findings 0, critical
  FQN findings 0, DTO candidates 930.

Verification:
- `python Tools/CompileWallX003Audit.py`: PASS, critical `using`/FQN lanes 0.
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`: PASS_WITH_WARNINGS, 0
  cycles, 0 unresolved first-party refs.
- `git diff --check` passed for X_003 edited files with CRLF warnings only.
- Guarded Core build was not launched: CPU dropped to 30.5%, 13.6%, 25.9%, but
  `VBCSCompiler` was active, so the project guard stayed closed.

Residual risk:
- Project-wide runtime concrete sibling refs remain 116. Root Core still has
  15 concrete sibling refs.
- Remaining global direct-player concrete findings include bootstrap owner
  creation/validation, `PlayerBuilder` concrete surface, and `GlobalRegistry`
  internal concrete slots. Those require separate builder/read-model and
  bootstrap split work, not analyzer suppression.
- Unity import/Console/PlayMode proof is absent for Loop 21/22/23/24/25.

## 2026-05-24 X_003 Interaction Inventory Tail Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- Remaining global player-concrete debt still included interaction fallback
  scene scraping and direct `GlobalRegistry.PlayerInventoryRuntime` reads.
- Bootstrap still recovered the input runtime with `NativeInputRuntime as
  InputManager`, and player context services still had `CurrentTool as
  PlayerBuilder` fallback paths.

What was done:
- `BioReactor` and `BatteryCharger` now read player inventory/tooling from
  cached `IPlayerRuntimeContext`, not interactor hierarchy searches.
- `HarvestableOutcrop`, `DestructibleOrganicManager`, `ScrapManager`,
  `PlayerActionController`, `ResourceNode`, `SuitUpgradeManager`,
  `LootMagnetSystem`, and `ModRuntimeState` now resolve inventory through
  `IPlayerInventoryService`.
- `PlayerRuntimeContextService` and `PlayerInventoryManager` no longer cast
  `CurrentTool` to `PlayerBuilder`; the builder is resolved from the player
  root only when the existing concrete builder surface is still required.
- `InputManager` and `PlayerInventoryManager` expose cold active-owner handles
  so bootstrap/service ensure paths do not downcast interface slots to concrete
  implementations.

Cinematic Cheats used:
- Architectural cheat only: replace fallback scene searches and interface-slot
  downcasts with existing owner-published service routes. No gameplay math was
  changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Static compile-wall evidence: concrete cast findings 1189->1173.
- Global direct player concrete coupling 32->15.
- AI/Physics/Physiology concrete casts remain 0.
- AI/Physics/Physiology direct player concrete coupling remains 0.

Current metrics:
- `AssemblyDependencyAudit_X_003.json`: 179 asmdefs, 422 DAG edges, 0 cycles,
  116 runtime concrete sibling refs, Core refs 40, Core first-party refs 27,
  Core concrete sibling refs 15, `autoReferencedFalse=179`.
- `CompileWall_X_003_Archaeology.json`: concrete casts 1173, global direct
  player concrete coupling 15, AI/Physics/Physiology concrete casts 0, direct
  player coupling in that lane 0, critical source `using` findings 0, critical
  FQN findings 0, DTO candidates 931.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`: PASS_WITH_WARNINGS, 0
  cycles, 0 unresolved first-party refs.
- `python Tools/CompileWallX003Audit.py`: PASS, critical `using`/FQN lanes 0.
- `git diff --check` passed for X_003 edited files with CRLF warnings only.
- Guarded Core build was not launched: CPU samples were 97.7%, 98.2%, 87.1%
  with active `csc`/`dotnet`.

Residual risk:
- Project-wide runtime concrete sibling refs remain 116. Root Core still has
  15 concrete sibling refs.
- Remaining player-concrete static hits are concentrated in player-internal
  component binding, bootstrap installers, save-runtime type dispatch, and the
  broader `PlayerBuilder` concrete API. Those need a dedicated builder/read
  model split and save/bootstrap migration, not analyzer suppression.
- Unity import/Console/PlayMode proof is absent for Loop 21/22/23/24/25/26.

## 2026-05-24 X_003 Save Inventory Commit Sink

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- `SaveManager` still type-tested `PlayerInventory` to notify mapped inventory
  write commits.

What was done:
- Added `IMappedInventoryWriteCommitSink` in Core contracts.
- `PlayerInventory` implements the sink.
- `SaveManager` now dispatches through `IMappedInventoryWriteCommitSink` from
  the existing `ISaveable` registry.

Cinematic Cheats used:
- Architectural cheat only: one narrow callback interface. No save format or
  inventory storage math changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Global direct player concrete coupling 15->14.
- Concrete cast findings stayed 1173 because the broad pattern gate still
  counts interface type tests.

Verification:
- `python Tools/CompileWallX003Audit.py`: PASS, concrete casts 1173, global
  direct player concrete coupling 14, AI/Physics/Physiology concrete casts 0,
  critical `using`/FQN lanes 0.
- `git diff --check` passed for the edited save/inventory files with CRLF
  warnings only.

Residual risk:
- Remaining runtime grep hits are player-internal kinematics self-binding and
  PDA cold installer component creation. Those are not cross-domain AI/Physics
  leaks, but they remain visible debt.

## 2026-05-24 X_003 Player Audio UI Contract Burn-Down

Evidence class: STATIC_SOURCE / CLI_STATIC_TOOL / BUILD_GUARD_BLOCKED.

What was wrong:
- `LifePodSeatStrapCoordinator` pinned the player through concrete
  `HectonPlayerMotor` and read `HectonPlayerMovement`.
- `SpatialAudioManager` kept concrete movement references for delayed trauma,
  listener AUP, and underwater density.
- Acoustic radar UI, `SignalBeacon`, and `RandomEventSystem` cached concrete
  `SpatialAudioManager`.

What was done:
- Added `IPlayerSeatLockMotorSink` and `GlobalRegistry.PlayerSeatLockMotor`;
  `HectonPlayerMotor` implements the sink.
- `LifePodSeatStrapCoordinator` now uses the seat-lock sink and player pose DTO.
- `SpatialAudioManager` uses `IPlayerMovementTraumaSink` and movement DTOs.
- Added `SpatialAudioImpactEmitterSample`,
  `ISpatialAudioImpactEmitterReadModel`,
  `ISpatialAudioListenerCaveReadModel`, and `IMeteorShowerAudioSink`.
- `AcousticRadarSphereRenderer`, `SonarHoloCompass`, `SignalBeacon`, and
  `RandomEventSystem` now consume those contracts instead of concrete
  `SpatialAudioManager` or `HectonPlayerMovement`.

Cinematic Cheats used:
- Architectural cheat only: presentation consumers receive fixed DTO samples
  and narrow sinks. No audio, physics, or player simulation math changed.

Exact Microseconds saved:
- Runtime: 0 claimed; no profiler sample.
- Static compile-wall evidence: concrete cast findings 1173->1160.
- Global direct player concrete coupling 14->12.
- AI/Physics/Physiology concrete casts remain 0.
- AI/Physics/Physiology direct player concrete coupling remains 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json --report-path
  Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`: PASS_WITH_WARNINGS, 179
  asmdefs, 422 DAG edges, 0 cycles, 116 runtime concrete sibling refs,
  `autoReferencedFalse=179`.
- `python Tools/CompileWallX003Audit.py`: PASS, concrete casts 1160, direct
  player concrete coupling 12, AI/Physics/Physiology concrete/direct-player 0,
  critical `using`/FQN lanes 0.
- `git diff --check` passed for edited files with CRLF warnings only.
- Build was not launched: CPU guard samples were 100%, 100%, 100%.

Residual risk:
- Runtime concrete sibling refs remain 116 and Core concrete sibling refs remain
  15.
- Remaining direct player grep hits are player-internal kinematics binding,
  PDA/editor bootstrap tooling, and broad `IPlayerRuntimeContext.PlayerMovement`
  consumers outside this slice.

## 2026-05-24 Loop 30 - Spatial Audio Concrete Surface Contraction

What was wrong:
- Runtime UI/world/gameplay systems still type-tested or cached concrete
  `SpatialAudioManager` for world-emitter samples, low-pass playback,
  eclipse/parasite modulation, SFX mixer routing, narrative radio bit-crush,
  inventory runaway explosions, flora harvest/spore playback, and weather
  thunder playback.
- The previous report said the critical AI/Physics/Physiology lane was clean,
  but runtime audio concrete fan-out was still wider than necessary.

What was done:
- Added narrow audio contracts:
  `SpatialAudioActiveEmitterSample`,
  `ISpatialAudioWorldEmitterReadModel`,
  `ISpatialAudioLowPassPlayback`,
  `ISpatialAudioEnvironmentModulationSink`,
  `ISpatialAudioSfxMixerRouteReadModel`,
  `ISpatialAudioNarrativeRadioSink`,
  `ISpatialAudioInventoryRunawaySink`,
  `ISpatialAudioHarvestPlaybackSink`,
  `ISpatialAudioWeatherPlaybackSink`.
- `SpatialAudioManager` implements those routes.
- Moved `AcousticZoneController`, `SpectrumSystem`,
  `PhysicalPanelButton`, `TraumaDispatcher`, `EclipseGameplaySystem`,
  `BaseModule`, `AudioLogSystem`, `PlayerThrusterAudio`,
  `PlayerInventory`, `DestructibleOrganicManager`,
  `HectonSurfaceWeatherDirector`, and `CelestialSyncSmokeTester`
  off concrete `SpatialAudioManager` runtime references.
- Updated `AdvancedAcousticsSmokeTester` string gates to assert the new
  interface routes.
- Updated `Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json`,
  `Docs/Tasks/Status_X_003.md`, and this rationale/log state.

Cinematic Cheats used:
- Architectural cheat only: presentation systems now see fixed samples and
  narrow owner routes. No DSP, physics, AI, or gameplay truth math changed.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Static source evidence: concrete cast findings 1160->1140.
- Global direct player concrete coupling 12->6.
- Runtime `SpatialAudioManager` concrete refs outside bootstrap/editor/audio
  owner code are now comments only.
- AI/Physics/Physiology concrete casts remain 0.
- AI/Physics/Physiology direct player concrete coupling remains 0.
- Critical source `using` findings remain 0.
- Critical fully-qualified source reference findings remain 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 179 asmdefs, 422 DAG edges, 0 cycles, 116 runtime
  concrete sibling refs, `autoReferencedFalse=179`.
- `python Tools/CompileWallX003Audit.py`: PASS, concrete casts 1140,
  direct player concrete coupling 6, AI/Physics/Physiology concrete/direct
  player 0, critical `using`/FQN lanes 0.
- `git diff --check` passed for edited files with CRLF warnings only.
- Build was not launched: CPU guard samples were 99.7%, 100%, 100% with
  active `csc`/`dotnet`.

Residual risk:
- Runtime concrete sibling refs remain 116 and Core concrete sibling refs
  remain 15.
- Remaining 6 direct player concrete findings are player-internal
  `PlayerKinematicsRuntime` component binding and cold PDA/progression
  installers. They are not AI/Physics/Physiology cross-domain leaks, but they
  remain documented debt.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 30.

## 2026-05-24 Loop 31 - Core Concrete Sibling Ref Amputation

What was wrong:
- `Hecton8.Core.asmdef` still referenced concrete sibling assemblies for
  AI migration, environment fluids, physics CCD, and physics determinism.
- Core consumers used those refs for pure constants/math or small jobs, not
  for authority ownership. That made the Core compile surface wider than the
  behavior required.

What was done:
- Moved `BrineLayerConstants` and `BrineLayerMath` to
  `Hecton8.Core.Contracts.Fluids`.
- Moved `FluidImpulseJob` to the Core-owned `Hecton8.Physics` source area.
- Moved `MacroSwarm` to the Core-owned `Hecton8.World.MacroSwarmTravelJob`.
- Added `DeterministicContractMath` and reused `KinematicCcdContractMath`
  from `Hecton8.Core.Contracts.Physics` for Core consumers.
- Removed `Hecton8.Core.asmdef` references to
  `Hecton8.AI.Ecology.Migration`, `Hecton8.Environment.Fluids`,
  `Hecton8.Physics.CCD`, and `Hecton8.Physics.Determinism`.
- Deleted the empty `Hecton8.AI.Ecology.Migration.asmdef`.
- Aligned stale `Hecton8.Core.csproj` includes/references with the moved
  files so the next guarded CLI build does not compile phantom refs.
- Updated `Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json`,
  `Docs/Tasks/Status_X_003.md`, and `Docs/AgentLogs/Rationale_X_003.md`.

Cinematic Cheats used:
- Architectural cheat only: pure primitive math and transit constants are
  owned by contracts; runtime simulation owners remain outside contracts.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Core concrete sibling refs: 15->11.
- Runtime concrete sibling refs: 116->112.
- First-party asmdefs: 179->178 after deleting empty migration asmdef.
- Critical source `using` findings remain 0.
- Critical fully-qualified source reference findings remain 0.
- AI/Physics/Physiology concrete casts remain 0.
- AI/Physics/Physiology direct player concrete coupling remains 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 178 asmdefs, 422 DAG edges, 0 cycles, 112 runtime
  concrete sibling refs, Core concrete sibling refs 11,
  `autoReferencedFalse=178`.
- `python Tools/CompileWallX003Audit.py`: PASS, concrete casts 1142,
  direct player concrete coupling 6, AI/Physics/Physiology concrete/direct
  player 0, critical `using`/FQN lanes 0.
- `git diff --check` passed for edited files with CRLF warnings only.
- Build was not launched: CPU guard samples were 100%, 100%, 99%; compiler
  process list was empty, but CPU guard alone was closed.

Residual risk:
- Runtime concrete sibling refs remain 112 and Core concrete sibling refs
  remain 11.
- Root `Hecton8.Core` files such as player movement, harpoon/seaglide, combat,
  health, and submarine OS still have 105-assembly reverse closure and still
  reach UI/audio because their folders are not split into real assemblies yet.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 31.

## 2026-05-24 Loop 32 - Echolocation Payload And Inventory Job Ref Cuts

What was wrong:
- `Hecton8.Core.asmdef` still referenced `Hecton8.Audio.Echolocation`
  for one 56-byte ray-hit DTO.
- `Hecton8.Core.asmdef` also referenced two one-file inventory job
  assemblies: `Hecton8.Inventory.Algorithms` and
  `Hecton8.Inventory.Corrosion`.
- Source gate exposed a stale `FaunaBrain -> Hecton8.Physics` import after
  the graph changes.

What was done:
- Promoted `AcousticEcholocationRayHit` to
  `Assets/_Project/Scripts/Core/Contracts/Audio`.
- Removed the duplicate ray-hit struct from
  `Audio/Echolocation/AcousticEcholocationRaymarch.cs`.
- Removed Core's asmdef reference to `Hecton8.Audio.Echolocation`.
- Moved `InventoryDefragJob` and `ItemSalinityCorrosionJob` under
  Core-owned `Assets/_Project/Scripts/Inventory`, preserving `.meta` files
  and namespaces.
- Deleted empty `Hecton8.Inventory.Algorithms` and
  `Hecton8.Inventory.Corrosion` asmdefs.
- Removed the stale `using Hecton8.Physics` from `FaunaBrain`.
- Updated `Hecton8.Core.csproj`, status, rationale, and JSON report.

Cinematic Cheats used:
- Architectural cheat only: fixed DTO/jobs moved to the owner that already
  consumes them. No runtime truth route changed.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Core concrete sibling refs: 11->8 during Loop 32, 15->8 across Loops
  31-32.
- Runtime concrete sibling refs: 112->110 during Loop 32, 116->110 across
  Loops 31-32.
- First-party asmdefs: 178->176 during Loop 32.
- Critical source `using` findings returned 1->0 after removing the stale
  `FaunaBrain` physics import.
- Critical fully-qualified source reference findings remain 0.
- AI/Physics/Physiology concrete casts remain 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 176 asmdefs, 424 DAG edges, 0 cycles, 110 runtime
  concrete sibling refs, Core concrete sibling refs 8,
  `autoReferencedFalse=176`.
- `python Tools/CompileWallX003Audit.py`: PASS, concrete casts 1146,
  direct player concrete coupling 6, AI/Physics/Physiology concrete/direct
  player 0, critical `using`/FQN lanes 0.
- `git diff --check` passed for edited files with CRLF warnings only.
- Build was not launched: CPU guard samples were 100%, 100%, 100%; compiler
  process list was empty, but CPU guard alone was closed.

Residual risk:
- Runtime concrete sibling refs remain 110 and Core concrete sibling refs
  remain 8: `World.Terrain`, `Audio.Propagation`, `Audio.Virtualization`,
  `Animation.IK`, `Cartography`, `Logistics`, `Logistics.Grid`, and `Input`.
- Root Core files still have a 111-assembly reverse closure for many gameplay
  files. Cable and metabolism remain isolated from UI/audio; root player and
  audio owner files are not.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 32.

## 2026-05-24 Loop 33 - Core Concrete Sibling Ref Zero And CLI Graph Scrub

What was wrong:
- `Hecton8.Core.asmdef` still carried eight concrete sibling refs:
  `Hecton8.Audio.Virtualization`, `Hecton8.Audio.Propagation`,
  `Hecton8.Animation.IK`, `Hecton8.Cartography`, `Hecton8.Logistics`,
  `Hecton8.Logistics.Grid`, `Hecton8.World.Terrain`, and `Hecton8.Input`.
- The CLI build graph still had stale moved paths and concrete DLL references
  in `Directory.Build.targets`, `Hecton8.Core.csproj`, and `Hecton8.slnx`.
- `Hecton8.Input.csproj` survived after `Hecton8.Input.asmdef` was removed,
  so solution builds could still compile a deleted assembly route.

What was done:
- Moved small Core-consumed files into Core-owned source folders with metas:
  audio virtualization jobs, audio propagation jobs, animation IK jobs,
  cartography grid job, logistics pipe jobs, WFC outpost translation job,
  world terrain seam job, and input runtime files.
- Deleted the now-empty/orphan concrete asmdefs and stale `Hecton8.Input.csproj`.
- Removed `Hecton8.Input.csproj` from `Hecton8.slnx`.
- Removed stale Core concrete DLL refs and stale moved source paths from
  `Directory.Build.targets` / `Hecton8.Core.csproj`.
- Re-ran full-project assembly audit and compile-wall artifact checks.

Cinematic Cheats used:
- Architectural cheat only: tiny helper/job implementation files were collapsed
  into the Core owner that already consumed them. No gameplay authority route
  was moved into contracts.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Core concrete sibling refs: 8->0 during Loop 33, 15->0 across Loops 31-33.
- Runtime concrete sibling refs: 110->100 during Loop 33, 116->100 across
  Loops 31-33.
- First-party asmdefs: 176->168 during Loop 33.
- Critical source `using` findings remain 0.
- Critical fully-qualified source reference findings remain 0.
- AI/Physics/Physiology concrete casts remain 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 168 asmdefs, 0 cycles, 100 runtime concrete sibling
  refs, Core concrete sibling refs 0, `autoReferencedFalse=168`,
  unresolved first-party refs 0.
- `Docs/Reports/COMPILE_WALL_X003_AUDIT.json`: 168 asmdefs, 100 runtime
  concrete sibling refs, 0 cycles, concrete casts 1147, direct player concrete
  coupling 6, AI/Physics/Physiology concrete/direct player 0, critical
  `using`/FQN lanes 0, hot path lookup findings 0.
- `git diff --check` passed for selected edited files with CRLF warnings only.
- Build was not launched: active `dotnet build Assembly-CSharp.csproj` and
  `csc.exe` were present, so the AGENTS.md guard was closed.

Residual risk:
- Runtime concrete sibling refs remain 100 outside Core; this is still not a
  project-wide green state.
- Root Core gameplay files still have a 111-assembly reverse closure and still
  reach UI/audio until player/combat/harpoon/seaglide/submarine owners are
  physically split or bridged.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 33.

## 2026-05-24 Loop 34 - Universal Input Payload Contract Promotion

What was wrong:
- `Hecton8.UI.VR` referenced `Hecton8.Input.Universal` only for the
  unmanaged `UniversalInputStateSignal` payload.
- After the long audit pass, `FaunaBrain` again contained a stale
  `using Hecton8.Physics`, which recreated an AI->Physics source import.

What was done:
- Moved `UniversalInputStateSignal.cs` and its `.meta` into
  `Assets/_Project/Scripts/Core/Contracts/Signals`.
- Changed the namespace to `Hecton8.Core.Contracts.Signals`.
- Removed the `Hecton8.Input.Universal` reference from `Hecton8.UI.VR.asmdef`.
- Deleted the empty `Hecton8.Input.Universal.asmdef` and `.meta`.
- Added the moved payload to `Hecton8.Core.csproj` and `Directory.Build.targets`.
- Removed the reintroduced `using Hecton8.Physics` from `FaunaBrain`.

Cinematic Cheats used:
- Architectural cheat only: one blittable payload moved to the shared contract
  route. No input implementation or UI behavior moved.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Runtime concrete sibling refs: 100->99 during Loop 34.
- First-party asmdefs: 168->167 during Loop 34.
- Critical source `using` findings: 1->0 after the `FaunaBrain` cleanup.
- Critical fully-qualified source reference findings remain 0.
- AI/Physics/Physiology concrete casts remain 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 167 asmdefs, 0 cycles, 99 runtime concrete sibling refs,
  Core concrete sibling refs 0, `autoReferencedFalse=167`, unresolved
  first-party refs 0.
- `python Tools/CompileWallX003Audit.py --json-path
  Docs/Reports/COMPILE_WALL_X003_AUDIT.json --report-path
  Docs/AgentLogs/CompileWallX003Audit.md`: 167 asmdefs, 99 runtime concrete
  sibling refs, 0 cycles, concrete casts 1153, direct player concrete coupling
  7, AI/Physics/Physiology concrete/direct player 0, critical `using`/FQN
  lanes 0.
- `git diff --check` passed for selected edited files with CRLF warnings only.
- Build was not launched: active `dotnet` and `csc.exe` were present.

Residual risk:
- Runtime concrete sibling refs remain 99, dominated by domain assemblies
  referencing `Hecton8.Core` and `Hecton8.Core.Memory`.
- Direct player concrete findings are now 7; current gate still proves 0 in
  the AI/Physics/Physiology lane, but the global player composition tail is
  not clean.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 34.

## 2026-05-24 Loop 35 - Unused Runtime Asmdef Edge Pruning

What was wrong:
- Runtime concrete sibling refs still included declared edges with no source
  usage.
- `Hecton8.UI.Diegetic` referenced `Hecton8.Core` even though the runtime
  assembly currently contains only `DiegeticAssemblyAnchor`.
- `Hecton8.Graphics.Caustics` referenced `Hecton8.Core.Memory` even though
  its only runtime source uses Core service interfaces, not Core.Memory.

What was done:
- Removed `Hecton8.Core` and `Hecton8.Core.Contracts` from
  `Hecton8.UI.Diegetic.asmdef`.
- Removed `Hecton8.Core.Memory` from
  `Hecton8.Graphics.Caustics.asmdef`.
- Re-ran assembly and compile-wall gates.

Cinematic Cheats used:
- None. This was dead-edge removal only.

Exact Microseconds saved:
- Runtime: 0 claimed.
- Runtime concrete sibling refs: 99->97 during Loop 35.
- Critical source `using` and FQN findings remain 0.
- AI/Physics/Physiology concrete casts remain 0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 167 asmdefs, 0 cycles, 97 runtime concrete sibling refs,
  Core concrete sibling refs 0, `autoReferencedFalse=167`.
- `python Tools/CompileWallX003Audit.py`: 167 asmdefs, 97 runtime concrete
  sibling refs, 0 cycles, concrete casts 1153, direct player concrete coupling
  7, AI/Physics/Physiology concrete/direct player 0, critical `using`/FQN
  lanes 0.
- `git diff --check` passed for selected edited files with CRLF warnings only.
- Build was not launched: CPU sample was 51%, above the AGENTS.md threshold.

Residual risk:
- Runtime concrete sibling refs remain 97, mainly `Hecton8.Core` /
  `Hecton8.Core.Memory` refs from real domain code.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 35.

## 2026-05-24 Loop 36 - Direct Player Cast Gate Closure And Runtime Edge Trim

What was wrong:
- `FaunaBrain` had reintroduced a physics determinism dependency for KCC
  velocity reads.
- Predator player impact fallbacks still knew concrete player movement.
- `Hecton8.World.Streaming`, loot contracts, and the SpaceEngine dev harness
  contributed avoidable runtime graph pressure.
- PDA/progression/narrative paths still had cold concrete player-owned
  component checks.
- `PlayerKinematicsRuntime` was the last global direct-player concrete cast
  source in the X_003 audit.

What was done:
- Replaced `FaunaBrain` physics determinism reads with the Core signal route
  and interface player force/trauma sinks.
- Removed unused refs from `Hecton8.World.Streaming` and
  `Hecton8.Gameplay.Loot.Contracts`; marked `Hecton8.Dev.SpaceEngine098`
  Editor-only.
- Added explicit direct-player rows to `CompileWallX003Audit.py`.
- Added/used `IPlayerExplorationChunkReadModel`,
  `IPlayerAchievementRegistryRuntime`, and `IPdaCartographyReadModel` for
  PDA/progression/narrative player-owned reads.
- Added `IPlayerKinematicsMovementRuntime` and
  `IPlayerKinematicsMotorSyncSink`; `PlayerKinematicsRuntime` now caches
  interfaces instead of `HectonPlayerMovement` / `HectonPlayerMotor`.

Cinematic Cheats used:
- None. This pass changed compile ownership and dependency routes only.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Runtime concrete sibling refs: 97->93 during Loop 36.
- Direct player concrete coupling: 7->0.
- AI/Physics/Physiology direct player coupling: 0->0.
- Critical source `using` findings: 0->0.
- Critical fully-qualified source reference findings: 0->0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 167 asmdefs, 0 cycles, 93 runtime concrete sibling refs,
  Core concrete sibling refs 0, `autoReferencedFalse=167`, unresolved
  first-party refs 0.
- `python Tools/CompileWallX003Audit.py --json-path
  Docs/Reports/COMPILE_WALL_X003_AUDIT.json --report-path
  Docs/AgentLogs/CompileWallX003Audit.md`: 167 asmdefs, 93 runtime concrete
  sibling refs, 0 cycles, concrete casts 1158, direct player concrete coupling
  0, AI/Physics/Physiology concrete/direct player 0, critical `using`/FQN
  lanes 0.
- `rg` against `FaunaBrain.cs` for `HectonPlayerMovement`,
  `using Hecton8.Physics`, `PhysicsDeterminismSignals`, and
  `Hecton8.Physics` returned no matches.
- `git diff --check` passed for selected edited files with CRLF warnings only.
- Build was not launched: CPU was 10.7%, but 7 `dotnet` processes were active.

Residual risk:
- Runtime concrete sibling refs remain 93, mainly domain assemblies that
  genuinely reference `Hecton8.Core` / `Hecton8.Core.Memory`.
- Root Core files still have large reverse closure; selected cable and
  metabolism files do not reach UI/audio.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 36.

## 2026-05-24 Loop 37 - Test Runtime Classification And Read-Model Cast Cuts

What was wrong:
- `Hecton8.PlayModeTests` was counted as product-runtime sibling debt even
  though its asmdef is gated by `TestAssemblies` and `UNITY_INCLUDE_TESTS`.
- Several runtime consumers cast registry services back to concrete owners for
  read-only atmosphere/fluid/vegetation data.
- `AmbientBiotaDirector` reopened the critical AI concrete-cast gate by
  casting `MapMagicVegetationRuntime` to `HectonMapMagicVegetationBridge`.

What was done:
- `AssemblyDependencyAudit.py` and `CompileWallX003Audit.py` now parse
  `optionalUnityReferences` and `defineConstraints`; Unity test asmdefs remain
  in the full graph but are excluded from product-runtime sibling debt.
- Expanded `IAtmosphereReadModel` and rerouted
  `AcousticZoneController`, `SkySystemFollowCamera`,
  `AcousticEcholocationTranslator`, `HabitatIntegrityManager`,
  `HectonSurvivalSystem`, `BaseModule`, `BiomeMatrixDirector`,
  `HabitatGraphManager`, and `FloraInteractionManager` away from concrete
  `HectonAtmosphereManager` reads.
- Added `IFluidSurfaceCurrentReadModel`, implemented it on
  `HectonFluidEngine`, exposed `GlobalRegistry.FluidSurfaceCurrent`, and
  changed `FloraInteractionManager` to use the interface for water/current
  shader inputs.
- Added `IAbyssalFlowVolumeReadModel.TrySampleAbyssalFlow()` and changed
  `AmbientBiotaDirector` to consume the interface instead of
  `HectonMapMagicVegetationBridge`.

Cinematic Cheats used:
- None. This pass changed compile ownership/read routes only.

Exact Microseconds saved:
- Runtime: 0 claimed; profiler proof absent.
- Product runtime concrete sibling refs: 93->91 from correcting test assembly
  classification.
- Concrete cast findings: 1158->1151.
- Direct player concrete coupling: 0->0.
- AI/Physics/Physiology concrete casts: 0->0 after the ambient biota fix.
- Critical source `using` findings: 0->0.
- Critical fully-qualified source reference findings: 0->0.

Verification:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project
  --json-path Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json
  --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`:
  PASS_WITH_WARNINGS, 167 asmdefs, 0 cycles, 91 product-runtime concrete
  sibling refs, Core concrete sibling refs 0, `autoReferencedFalse=167`,
  unresolved first-party refs 0.
- `python Tools/CompileWallX003Audit.py --json-path
  Docs/Reports/COMPILE_WALL_X003_AUDIT.json --report-path
  Docs/AgentLogs/CompileWallX003Audit.md`: 167 asmdefs, 91 product-runtime
  sibling refs, 0 cycles, concrete casts 1151, direct player concrete coupling
  0, AI/Physics/Physiology concrete/direct player 0, critical `using`/FQN
  lanes 0.
- `git diff --check` passed for edited files with CRLF warnings only.
- Build was not launched: CPU was 34.7%, but 7 `dotnet` processes were
  active.

Residual risk:
- Product-runtime concrete sibling refs remain 91, mainly real domain
  references to `Hecton8.Core` / `Hecton8.Core.Memory`.
- Root Core files still have large reverse closure; selected cable and
  metabolism files still do not reach UI/audio.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are
  absent for Loop 37.
## Loop 38 - Lore Runtime Contract Burn-Down

What was wrong -> Atlas/Quest/AudioLog/FirstHour/Localization consumers still knew concrete owner classes for read-only state or narrow owner notifications. Broad concrete cast debt stayed visible after Loop 37 even though critical AI/Physics/Physiology and direct-player lanes were already zero.

What was done -> Added/expanded `IAtlasSignalReadModel`, `IQuestSystem`, `IAudioLogRuntime`, `IFirstHourReadModel`, `IFirstHourRouteContactSink`, and `ILocalizationTextReadModel`; rerouted 38 gameplay/UI/world/runtime files to those interfaces. Corrected the first `IQuestSystem` signature after build failure by replacing leaked `QuestPhaseGateType` with `byte phaseGateCode`; enum interpretation stays inside `QuestManager`.

Cinematic Cheats used -> None. This pass was compile-wall/source-ownership decoupling, not simulation or presentation fakery.

Exact Microseconds saved -> 0 claimed. No runtime profiler sample was taken.

Evidence -> `CompileWallX003Audit.py`: critical source using 0, critical FQN 0, concrete casts 1108, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0. `AssemblyDependencyAudit.py`: 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferencedFalse=167`, Core concrete sibling refs 0, unresolved first-party refs 0. `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`: pass, 0 errors, 4 duplicate-source warnings, 00:00:29.47. Unity import/Console/PlayMode/player-build proof is not claimed.

## Loop 39 - ObjectPool Consumer Interface Burn-Down

What was wrong -> 26 runtime consumers still cached or cast `ObjectPoolManager` directly while only using pool-service operations. That preserved concrete owner knowledge outside the pool owner and inflated broad concrete-cast debt.

What was done -> Converted those consumers to `IObjectPoolService` fields, locals, hot-swap casts, and `GlobalRegistry.ObjectPoolService` reads. Expanded `IObjectPoolService` with existing owner methods required by current call sites: `WarmupPrefabAsync`, `CanDespawnWithoutDestroy`, and `TrimInactivePoolsForMemoryPressure`. `ObjectPoolManager.PoolItemMarker` checks were retained as marker-component identity tests.

Cinematic Cheats used -> None. This pass changed compile ownership/source routes only.

Exact Microseconds saved -> 0 claimed. No runtime profiler sample was taken.

Evidence -> `CompileWallX003Audit.py`: critical source using 0, critical FQN 0, concrete casts 1087, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0. `AssemblyDependencyAudit.py`: 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferencedFalse=167`, Core concrete sibling refs 0, unresolved first-party refs 0. `git diff --check` passed for edited files with CRLF warnings only. `dotnet build Hecton8.Core.csproj --no-restore /p:UseSharedCompilation=false`: pass, 0 errors, 4 duplicate-source warnings, 00:01:17.05. `MarauderOutpostGenerationService.cs` is still outside that generated Core project and needs Unity/import or regenerated domain project proof.
## 2026-05-24 - Loop 40 ObjectPool Compile-Wall Burn-Down

What was wrong:
- Broad concrete owner knowledge still existed in runtime consumers that only needed object-pool service operations.
- `BaseModule` held `ObjectPoolManager` and reached into `ObjectPoolManager.PoolItemMarker` for flooded-reef proxy reserve checks.
- Root Core source remains a compile nucleus; product runtime sibling refs are not zero.

What was done:
- Converted ObjectPool consumers to `IObjectPoolService` routes across 39 source files.
- Added `IObjectPoolService.TryGetAvailableCountForPooledInstance(GameObject, out int)` and implemented marker-based reserve reads inside `ObjectPoolManager`.
- Removed the final non-owner `BaseModule` concrete ObjectPool reserve route.
- Preserved owner/bootstrap/editor concrete ObjectPool routes; those are not consumer service dependencies.

Cinematic cheats / DOD patterns:
- Contract facade over owner internals instead of reflection, duplicate DTOs, or SignalBus request/response misuse.
- Cold GlobalRegistry service read, then cached interface use.
- No gameplay authority moved; pool owner remains the only owner of marker-to-pool reserve logic.

Evidence:
- `AssemblyDependencyAudit.py`: 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferencedFalse=167`, Core concrete sibling refs 0, unresolved first-party refs 0.
- `CompileWallX003Audit.py`: concrete cast findings 1084, critical source using 0, critical fully-qualified references 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0.
- Scoped `git diff --check` over the 39 X_003 source files passed with CRLF warnings only.
- Full-repo `git diff --check` is not clean because unrelated changed Unity `.meta` files contain trailing whitespace.
- Build guard closed: CPU 11.9%, 7 `dotnet` processes and `VBCSCompiler` active. No new build launched after the final BaseModule/ObjectPool contract extension.

Blast radius proof:
- `CablePhysicsSolver132.cs`: `Hecton8.Physics.Cable132`, radius 3, direct inbound 1, UI=false, audio=false; previous Core state was radius 98/direct inbound 92/UI=true/audio=true. Reduction: 95 assemblies, 91 direct inbound edges.
- `ShinobuMetabolismRuntime.cs`: `Hecton8.Physiology`, radius 2, direct inbound 1, UI=false, audio=false.
- `UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 2, direct inbound 1, UI=false, audio=false; previous Core state was radius 99/direct inbound 2/UI=true/audio=true. Reduction: 97 assemblies, 1 direct inbound edge.

Exact microseconds saved:
- Runtime: 0 claimed. This was compile-wall/source ownership work; no profiler proof.
- Editor compile wall: static reverse-closure reductions above. Wall-clock rebuild deltas are not claimed because Unity import timing was not available and AGENTS.md build guard blocked the final build.

## 2026-05-24 - Loop 41 Fluid/Acoustic/Weather Contract Slice

What was wrong:
- Non-owner runtime consumers still knew `HectonFluidEngine`, `HectonAnalyticalFlowField`, `GlobalRegistry.Fluid`, `AcousticZoneController`, `HectonSurfaceWeatherDirector`, or `FirstHourDirector` for read-only state or narrow owner commands.
- The residual asmdef graph stayed cycle-free, but the source-level wall still had concrete owner knowledge that would block future physical assembly splits.
- Exact wall-clock compile timing was not available because the AGENTS.md compile guard closed under active compiler load.

What was done:
- Added `Assets/_Project/Scripts/Core/Contracts/Fluids/FluidAnalyticalContracts.cs` with fixed-layout fluid payloads and deterministic analytical flow math.
- Expanded Core.Contracts / GlobalRegistry with fluid routes: `IAbyssalFlowGpuReadModel`, `IAnalyticalFlowReadModel`, `IFluidSurfaceCurrentReadModel`, `IFluidBubbleBurstSink`, `IBuoyancyObjectRegistry`, and `IFluidCurrentWriteSink`.
- Expanded Core.Contracts / GlobalRegistry with acoustic/weather routes: `IAcousticZoneReadModel`, `IAcousticZoneMadnessCueSink`, `ISurfaceWeatherReadModel`, and `SurfaceWeatherKindCodes`.
- Converted player kinematics/movement, fauna, boids, construction, visuals, weather, crash telemetry, biome, audio, localization, narrative, and world readability consumers to contract routes where the interaction was a read model or narrow sink.
- Preserved concrete owner/bootstrap/editor/tooling lanes where changing Unity serialized authoring or renderer-local owner integration would be a separate behavioral migration.

Cinematic cheats / DOD patterns:
- Contract read-models and fixed-layout DTOs instead of concrete owner casts.
- Cold GlobalRegistry service resolution with cached interfaces.
- Byte-coded cross-domain weather kind codes instead of leaking domain enums into Core.Contracts.
- No gameplay authority moved; fluid, weather, acoustic, and first-hour owners still own truth.

Evidence:
- `python Tools/AssemblyDependencyAudit.py --source-root Assets/_Project --json-path Docs/Reports/ASSEMBLY_DEPENDENCY_AUDIT_REPORT_X_003.json --report-path Docs/AgentLogs/AssemblyDependencyAudit_X_003.md`: PASS_WITH_WARNINGS, 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, Core concrete sibling refs 0, `autoReferencedFalse=167`, unresolved first-party refs 0.
- `python Tools/CompileWallX003Audit.py --json-path Docs/Reports/COMPILE_WALL_X003_AUDIT.json --report-path Docs/Reports/COMPILE_WALL_X003_AUDIT.md`: concrete casts 1065, critical source using 0, critical FQN 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0.
- Targeted source checks found no residual `HectonFluidEngine`, `GlobalRegistry.Fluid`, or direct concrete acoustic/weather/first-hour owner references in the converted consumer set.
- Scoped `git diff --check` over the X_003 source slice passed with CRLF warnings only.
- Build was not launched: latest guard sample was CPU 63.3% with active `dotnet`; an earlier Loop 41 sample was CPU 100% with active `csc` and `dotnet`.

Blast radius proof:
- `CablePhysicsSolver132.cs`: `Hecton8.Physics.Cable132`, radius 3, direct inbound 1, UI=false, audio=false; previous Core state was radius 98/direct inbound 92/UI=true/audio=true. Reduction: 95 assemblies, 91 direct inbound edges.
- `ShinobuMetabolismRuntime.cs`: `Hecton8.Physiology`, radius 2, direct inbound 1, UI=false, audio=false.
- `UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 2, direct inbound 1, UI=false, audio=false; previous Core state was radius 99/direct inbound 2/UI=true/audio=true. Reduction: 97 assemblies, 1 direct inbound edge.

Residual risk:
- Product-runtime concrete sibling refs remain 91. This is not green.
- Residual concrete fluid/acoustic references still exist in owner, bootstrap, editor, VFX, or tooling lanes. They were not hidden or counted as fixed.
- Last green Core build remains Loop 39. Loop 41 requires Unity/import or regenerated domain-project compile proof once the CPU/compiler guard opens.

Exact microseconds saved:
- Runtime: 0 claimed. No profiler sample was taken.
- Editor compile wall: static reverse-closure proof above; wall-clock rebuild deltas are not claimed.

## 2026-05-24 - Loop 42-43 Fluid GPU / Audio-World Read-Model Continuation

What was wrong:
- VFX and construction code still knew concrete fluid owner routes for GPU wake buffers, active maelstrom upload, and surface current strength.
- Audio/world consumers still knew concrete depth-zone, quest, localization, soundscape, environmental strain, and spatial-audio owners for narrow read or sink paths.
- The product-runtime graph is still not green: concrete sibling refs are currently 92 and Core.Contracts boundary violations are 119.

What was done:
- Expanded `IAbyssalFlowGpuReadModel` and `IFluidSurfaceCurrentReadModel`; moved `HectonMarineSnowRenderer`, `CarveDebrisComputeRenderer`, and `DroneFleetManager` to fluid read-model routes.
- Added/expanded contract routes for formatted localization text, quest depth context, soundscape tier, environmental strain, cave listener RT60, and fixed-layout binaural emitter telemetry.
- Converted `DepthZoneDirector`, `HectonMusicDirector`, `AcousticZoneController`, `DeepPsychosisController`, and `PlayerCriticalProceduralAudioRenderer` away from concrete non-owner routes.
- Made `SoundscapeSystem`, `EnvironmentalStrainManager`, and `SpatialAudioManager` implement their new read-model interfaces while keeping truth ownership inside their domains.

Cinematic cheats / DOD patterns:
- Narrow read-models and byte-coded boundary values instead of leaking domain enums or MonoBehaviours into Core.Contracts.
- Cold GlobalRegistry route with cached interface use.
- Fixed-layout telemetry DTO for spatial-audio emitter state; no managed event or reflection route.
- No gameplay authority moved; owners still publish/read their own truth.

Evidence:
- `AssemblyDependencyAudit.py`: 167 asmdefs, 393 edges, 0 cycles, 92 product-runtime concrete sibling refs, first-party `autoReferenced=false` 167/167, Core concrete sibling refs 0, unresolved first-party refs 0, Core.Contracts boundary violations 119.
- `CompileWallX003Audit.py`: concrete casts 1057, critical source using 0, critical FQN 0, hot-path lookup 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0.
- Scoped `git diff --check` over the touched source slice passed with CRLF warnings only.
- Build was not launched after this slice: latest guard sample was CPU 74.9% with an active `dotnet` process, so the AGENTS.md guard was closed. Last green Core build remains Loop 39.

Blast radius proof:
- `CablePhysicsSolver132.cs`: `Hecton8.Physics.Cable132`, radius 3, direct inbound 1, UI=false, audio=false; previous Core state was radius 98/direct inbound 92/UI=true/audio=true. Reduction: 95 assemblies and 91 direct inbound edges.
- `ShinobuMetabolismRuntime.cs`: `Hecton8.Physiology`, radius 2, direct inbound 1, UI=false, audio=false.
- `UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: `Hecton8.AI.Cognition`, radius 2, direct inbound 1, UI=false, audio=false; previous Core state was radius 99/direct inbound 2/UI=true/audio=true. Reduction: 97 assemblies and 1 direct inbound edge.

Residual risk:
- Product-runtime concrete sibling refs are 92, not zero.
- Broad concrete cast findings are 1057, not zero. The critical prompt lanes are zero; the whole project is not clean.
- `CarveDebrisComputeRenderer.cs` is outside the current generated Core csproj coverage.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are still absent for Loop 42-43.

Exact microseconds saved:
- Runtime: 0 claimed. No profiler sample was taken.
- Editor compile wall: static reverse-closure reductions above. Wall-clock rebuild deltas are not claimed because no Unity import/build was allowed by the guard.

## 2026-05-24 - Loop 44-45 Caustics Zero-Ref / RenderTexturePool Contract Slice

What was wrong:
- Disabled `Graphics.Caustics` compatibility shim still carried Core/Bootstrap/service interfaces and asmdef references to shared assemblies.
- `ToolDiegeticDisplayController` still imported `Hecton8.Optimization` and cached/cast concrete `RenderTexturePool` for a rent/return-only route.
- `Hecton8.Core.asmdef` had a reintroduced zero-hit `Hecton8.UI.Localization` concrete dependency.

What was done:
- Stripped `AnalyticalCausticsService` down to a disabled `MonoBehaviour` shim and removed every first-party/collections/math reference from `Hecton8.Graphics.Caustics.asmdef`.
- Added `IRenderTexturePoolService` in Core.Contracts, implemented it on `RenderTexturePool`, exposed `GlobalRegistry.RenderTexturePoolService`, and changed `ToolDiegeticDisplayController` to the interface route.
- Removed the zero-hit Core -> `Hecton8.UI.Localization` asmdef edge.
- Added the new contract to `Directory.Build.targets` for CLI Core coverage and removed the duplicate generated `.csproj` include after the first build warning exposed it.

Cinematic cheats / DOD patterns:
- Narrow contract route for synchronous rent/return instead of direct concrete owner knowledge.
- Dead shim decoupling instead of pretending a disabled service contributes runtime authority.
- Source-proofed asmdef pruning; no broad deletion of refs without `rg` evidence.

Evidence:
- `AssemblyDependencyAudit.py`: 167 asmdefs, 393 edges, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferenced=false` 167/167, Core refs 27, Core first-party refs 14, Core concrete sibling refs 0, Core.Contracts boundary violations 119, unresolved first-party refs 0.
- `CompileWallX003Audit.py`: runtime concrete sibling refs 91, concrete casts 1058, critical source using 0, critical FQN 0, hot-path lookup 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0.
- Direct greps: `NO_AI_PHYSICS_IMPORTS`; `NO_CRITICAL_UI_AUDIO_IMPORTS`.
- Scoped `git diff --check` passed with CRLF warnings only.
- Guarded Core build passed once: 0 errors, 5 warnings, 00:01:17.09. One warning was X_003's duplicate `RenderTexturePoolContracts.cs` include; duplicate removed. Rebuild retry was blocked by CPU 90.1%.

Blast radius proof:
- `CablePhysicsSolver132.cs`: radius 3, direct inbound 1, UI=false, audio=false; previous Core state radius 98/direct inbound 92/UI=true/audio=true. Reduction: 95 assemblies and 91 direct inbound edges.
- `ShinobuMetabolismRuntime.cs`: radius 2, direct inbound 1, UI=false, audio=false.
- `UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: radius 2, direct inbound 1, UI=false, audio=false; previous Core state radius 99/direct inbound 2/UI=true/audio=true. Reduction: 97 assemblies and 1 direct inbound edge.

Residual risk:
- Product-runtime concrete sibling refs remain 91. This is still not green.
- Broad concrete cast findings remain 1058. Critical lanes are zero; the whole project is not clean.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are still absent for this slice.

Exact microseconds saved:
- Runtime: 0 claimed. No profiler sample was taken.
- Editor compile wall: static graph/source ownership proof only; wall-clock Unity rebuild deltas are not claimed.

## 2026-05-24 - Loop 46 RenderTexture Lifecycle Contract Slice

What was wrong:
- RT lifecycle record/category types lived under `Hecton8.Optimization`, forcing non-owner consumers to know the optimization owner namespace.
- UI/PDA/visor/VRAM consumers still stored or cast concrete `RenderTexturePool` / `RenderTextureLifecycleTracker`.
- Editor diagnostics used concrete lifecycle/pool registry routes for read-only reporting.

What was done:
- Moved `RenderTextureAllocationRecord` and `RenderTextureOwnerCategory` into `Hecton8.Core.Contracts` with the existing `.meta` guid preserved.
- Added `IRenderTextureLifecycleService` and exposed `GlobalRegistry.RenderTextureLifecycleService`.
- Expanded `IRenderTexturePoolService` with existing owner methods/statistics: `PoolHitRate`, `TotalPooledCount`, `ClearAllPools`, `ReclaimPdaRenderTextures`.
- Converted PDA, vehicle cockpit UI, visor HUD, VRAM monitor/pressure, RT managers, visual smoke tester, and editor diagnostics to interface routes.

Cinematic cheats / DOD patterns:
- Contract route for synchronous lifecycle diagnostics instead of managed SignalBus request/response.
- Owner/self-registration kept concrete; consumers moved to interfaces.
- No runtime behavior rewrite, no fake reflection/object casts.

Evidence:
- `AssemblyDependencyAudit.py`: 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferenced=false` 167/167, Core concrete sibling refs 0, unresolved first-party refs 0.
- `CompileWallX003Audit.py`: concrete casts 1049, critical source using 0, critical FQN 0, hot-path lookup 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0.
- Concrete RT owner grep now only finds owner/bootstrap/self-registration routes for `GlobalRegistry.RenderTexturePool` and `GlobalRegistry.RenderTextureLifecycle`.
- Scoped `git diff --check` passed with CRLF warnings only.
- New build was blocked by AGENTS.md guard: latest samples were CPU 65% then 100% with active `dotnet`.

Exact microseconds saved:
- Runtime: 0 claimed. Calls forward to existing owners; no profiler sample.
- Editor compile wall: source-owner knowledge reduced by 9 concrete-cast findings. Asmdef sibling edge count unchanged at 91.

## 2026-05-24 - Loop 47 VRAM Fluid Decal Tool Durability Contract Slice

What was wrong:
- Bootstrap, content, dispatcher, asset lifecycle, voxel, asset dispatch, and vegetation paths still knew concrete `VRAMPressureMonitor`.
- Biome/logistics paths still knew concrete fluid decal and MapMagic terrain owners for presentation or surface-level reads.
- Tool durability consumers in maintenance, HUD, modular equipment, player tools, and PDA loadout still cached/cast concrete `ToolDurabilitySystem`.
- A zero-hit Core -> `Hecton8.UI.Localization` asmdef edge was reintroduced and had to be cut again.

What was done:
- Added `IVramPressureReadModel`, `IVramPressureSampleSink`, `IVramPressureMipBiasSink`, `IFluidDecalPresentationSink`, and `IToolDurabilityService`.
- Exposed the routes through `GlobalRegistry` and owner implementations.
- Converted 22 source/asmdef files to those contracts, including VRAM readers/sinks, fluid decal presentation calls, terrain reads, and tool durability service calls.
- Removed the reintroduced Core -> UI.Localization asmdef edge.

Cinematic cheats / DOD patterns:
- Narrow synchronous DTO/read-model/sink routes instead of concrete owner calls.
- Terrain and fluid presentation stay owner-owned; consumers do not import implementation classes.
- No reflection, no `object` hiding, no SignalBus request/response for immediate reads.

Evidence:
- `AssemblyDependencyAudit.py`: 167 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferenced=false` 167/167, Core concrete sibling refs 0, unresolved first-party refs 0, Core.Contracts boundary violations 119.
- `CompileWallX003Audit.py`: concrete casts 1009, critical source using 0, critical FQN 0, hot-path lookup 0, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0, AI/Physics/Physiology direct player coupling 0.
- `rg` over `Assets/_Project/Scripts/AI` found no `Hecton8.Physics` imports/references.
- `rg` for `Hecton8.UI.Localization` now only finds the UI localization asmdef and its marker namespace.
- Scoped `git diff --check` over the 22-file slice passed with CRLF warnings only.
- Guarded build was not launched: guard samples were CPU 65% with no compiler process, then CPU 100% with active `csc` and `dotnet`, above the AGENTS.md threshold.

Blast radius proof:
- `CablePhysicsSolver132.cs`: radius 3, direct inbound 1, UI=false, audio=false; previous Core state radius 98/direct inbound 92/UI=true/audio=true. Reduction: 95 assemblies and 91 direct inbound edges.
- `ShinobuMetabolismRuntime.cs`: radius 2, direct inbound 1, UI=false, audio=false.
- `UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: radius 2, direct inbound 1, UI=false, audio=false; previous Core state radius 99/direct inbound 2/UI=true/audio=true. Reduction: 97 assemblies and 1 direct inbound edge.

Residual risk:
- Product-runtime concrete sibling refs remain 91. This is not fully green.
- Broad concrete cast findings remain 1009. Critical lanes are zero; the whole project is not clean.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are still absent for this slice.

Exact microseconds saved:
- Runtime: 0 claimed. Calls forward to existing owners; no profiler sample.
- Editor compile wall: source-owner knowledge reduced by 40 broad concrete-cast findings in this slice. Asmdef sibling edge count unchanged at 91 after the Core UI edge was re-cut.

## 2026-05-24 - Loop 48 VRAM Budget ScanLog Fauna Terrain Contract Slice

What was wrong:
- `FaunaKinematicsRuntime` had reopened a concrete world-owner terrain route through `MapMagicBridge`.
- VRAM budget consumers still stored/cast concrete `VRAMMonitor` for scalar memory state, pressure state, and explicit sampling.
- Scan/analyzer/fabricator/PDA/logbook paths still knew concrete `ScanLogSystem` for read-only checks and archive commands.
- `WorldChunkResidencyManager` mixed concrete VRAM budget reads with addressable lifecycle owner calls.

What was done:
- Rerouted fauna terrain sampling through `ITerrainHeightSampleReadModel`.
- Added `IVramBudgetReadModel`, `IVramBudgetSampleSink`, byte-coded `VramPressureStateCodes`, `AssetPriorityTierCodes`, `IAssetLifecyclePressureSink`, and `IScanLogService`.
- Exposed the new routes through `GlobalRegistry` and owner implementations.
- Converted 21 source files in the VRAM budget, content dispatch, VFX, visor/profiler, scan-log, PDA/logbook, and world residency slice to stable contracts.
- Left addressable handle acquisition/release on concrete `AssetLifecycleGovernor` intentionally; that needs a separate ownership contract, not a pressure-sink shortcut.

Cinematic cheats / DOD patterns:
- Synchronous read-model DTO/byte-code routes instead of concrete owner imports.
- SignalBus was not used for request/response state reads.
- No reflection, no `object` hiding, no owner MonoBehaviours moved into Core.Contracts.

Evidence:
- `CompileWallX003Audit.py`: 167 asmdefs, 0 cycles, 91 runtime product concrete sibling refs, concrete casts 992, critical source using 0, critical FQN 0, hot-path lookup 0, hot-path registry mutation notes 2, direct player concrete coupling 0, AI/Physics/Physiology concrete casts 0.
- `AssemblyDependencyAudit.py` under `Assets/_Project/Scripts`: 162 asmdefs, 0 cycles, 91 product-runtime concrete sibling refs, `autoReferenced=false` 162/162, Core concrete sibling refs 0, unresolved first-party refs 0, Core.Contracts boundary violations 119.
- Scoped `git diff --check` over the Loop 48 source slice passed with CRLF warnings only.
- Build was not launched: latest guard sample was CPU 24.2% but 6 `dotnet` processes and `VBCSCompiler` were active.

Blast radius proof:
- `CablePhysicsSolver132.cs`: radius 3, direct inbound 1, UI=false, audio=false; previous Core state radius 98/direct inbound 92/UI=true/audio=true. Reduction: 95 assemblies and 91 direct inbound edges.
- `ShinobuMetabolismRuntime.cs`: radius 2, direct inbound 1, UI=false, audio=false.
- `UtilityAICognitionVault.cs` / `ShinobuApexBrainVault.cs`: radius 2, direct inbound 1, UI=false, audio=false; previous Core state radius 99/direct inbound 2/UI=true/audio=true. Reduction: 97 assemblies and 1 direct inbound edge.

Residual risk:
- Product-runtime concrete sibling refs remain 91. This is not fully green.
- Broad concrete cast findings remain 992. Critical lanes are zero; the whole project is not clean.
- `SaveManager.cs` still contains one concrete `VRAMMonitor` route because `apply_patch` rejected the file with an invalid UTF-8 sequence.
- `WorldChunkResidencyManager` still has concrete addressable lifecycle owner calls by design until handle ownership is formalized.
- Unity import, Console, PlayMode, profiler, GC, and player build proof are still absent for this slice.

Exact microseconds saved:
- Runtime: 0 claimed. No profiler sample was taken.
- Editor compile wall: source-owner knowledge reduced by 17 broad concrete-cast findings in this slice, 1009->992; since Loop 37, broad concrete-cast findings are 1151->992. Asmdef sibling edge count remains 91.
