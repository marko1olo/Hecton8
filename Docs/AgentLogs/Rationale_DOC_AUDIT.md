# Rationale_DOC_AUDIT

Agent: DOC_AUDIT
Domain: Documentation / Project Reality Audit / Editor Validation Tripwires
Current continuation: R47
Date: 2026-05-15

Previous rationale history is archived under `Docs/Archive/Batch005/AgentLogs/Rationale_DOC_AUDIT.md`.

## Decision 038 - Pager Worker Fault Accounting Must Be Closed On The Failing Command

Problem: `H8BinaryWorldPager.RunWorkerLoop()` decremented pending write/read counters only after `ProcessWrite()` or `ProcessRead()` returned normally. The inner IO methods catch normal file corruption/IO cases, but unexpected exceptions before those guarded regions can still jump to the outer worker catch, stop the worker, and leave `_pendingWriteCount` or `_pendingReadCount` inflated. That makes diagnostics lie and can make later budget checks look saturated after a fault.

Solution: move per-command processing behind small accounting wrappers. Each dequeued command now decrements its pending counter in `finally`. Unexpected command-level faults record telemetry, mark the pager fail-closed, zero exposed pending queue counters, and request worker shutdown. This keeps the failure deterministic and makes post-fault telemetry honest.

Rejected Alternatives: leaving the outer worker catch alone was rejected because it reports a global fault after the damage is already done to counters. Retrying the same command was rejected because the command may reference invalid native memory or a disposed slot; predictable fail-closed behavior is safer than hiding memory corruption. Allocating managed diagnostic payloads was rejected because this is a pager subsystem and the black-box telemetry already exists.

Scalability potential: Low devices keep the same zero-frame-cost steady path and get clearer fallback state when disk/native state breaks. Middle/High/Ultra devices do not spend extra normal-frame budget; the saved debugging time goes into reliable page streaming instead of chasing false queue saturation.

Hardware Impact: Normal path is unchanged. Failure path adds only cold background bookkeeping. Estimated low-end i3/MX350 frame impact: 0.000 ms; background failure handling remains dominated by existing disk/black-box dump cost.

## Decision 039 - WFC Outpost Persistence Contract Cannot Stay Interface-Only

Problem: Current `IAsyncPersistenceService` declared `TryPersistWfcOutpostStateSnapshot` and `TryApplyWfcOutpostStateOverride`, while `SaveManager` did not implement them. After rebuilding current `Hecton8.Core.Contracts` from live source, this became the next real compile wall. Leaving it as documentation-only would preserve a fake persistence surface: WFC outpost mutable state would appear promised by the registry but have no service path.

Solution: keep the contract inside `SaveManager` and use the current MacroDB path now present in the live file. Mutable WFC cell flags live in DataVault `BufferID.WfcOutpostGrid`, are packed through `PackWfcOutpostMutableStateJob` into fixed `NativeArray<ulong>` scratch, encoded through `SaveBinaryPayloadCodec.TryWriteWfcOutpostBitmaskPayload`, deduplicated by one-sector packed hash, and committed via `IMacroDatabaseService.MarkDirty`. Restore reads `MacroDatabasePayloadHandle` through `TryGetPayload`, decodes into restore scratch, and unpacks mutable flags back into the DataVault grid.

Rejected Alternatives: empty stubs were rejected because they would satisfy the compiler while lying to gameplay systems. A direct `FileStream` path was rejected because MacroDB is now the current service authority for this WFC payload. A managed dictionary/cache was rejected because WFC state is small enough for fixed native scratch and the project requires zero-GC persistence paths.

Scalability potential: Low = WFC outpost mutable state persists as a compact bitmask instead of full grid payload. Middle = unchanged snapshots skip pager writes via packed hash. High/Ultra = richer outpost state can add bit planes later without changing the page transport contract.

Hardware Impact: Normal frame cost is 0 unless a WFC persistence caller invokes the service or WFC state signals are drained. Persist path loops `500` cells x `4` mutable bits into `32` ulong words, then writes a small MacroDB payload. Restore path decodes only the bounded payload returned by MacroDB. Estimated low-end i3/MX350 steady frame impact: 0.000 ms outside explicit WFC save/restore signal work.

## Decision 040 - Full Core Compile Result Must Be Demoted Under Active Churn

Problem: R37 local Bee/Roslyn `Hecton8.Core` probe returned `0`, but the current workspace changed underneath it. R38 rebuilt current contract/audio/world/AI/animation dependency refs and progressed past the persistence wall, then hit unrelated active errors in `SpatialAudioManager.cs`, `ScannerTool.cs`, `SubmarineAutoLevelBallastController.cs`, `HectonArenaAllocator.cs`, `HectonFluidEngine.cs`, `UI/SuitHUDV4CanvasOverlay.cs`, `UI/InteractionUI.cs`, and `FaunaBrain.cs`.

Solution: stable docs now say R37 full-Core success is stale for the current worktree. R38 claims only the probes that actually passed (`Hecton8.Core.Contracts`, `Hecton8.Core.Memory`, temporary audio virtualization, world contracts, AI cognition, and animation IK refs) and records full `Hecton8.Core` as blocked by unrelated active churn.

Rejected Alternatives: filtering out problem files was rejected as proof because those files are referenced by other Core files. Claiming the old R37 compile as current was rejected because it ignores active filesystem evidence. Chasing audio/fluid/UI repairs from DOC_AUDIT was rejected as cross-domain expansion after the persistence contract wall was closed.

Scalability potential: Accurate compile boundaries reduce integration time and prevent teams from spending performance work on a build state that no longer exists.

Hardware Impact: Documentation/probe correction only. Runtime cost: 0.000 ms.

## Decision 041 - Generated Project Drift Needs A Tripwire, Not Stubs

Problem: A fresh `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` fails before the previous R38 line-level blockers: the generated project surface is missing references for first-party assemblies that already exist in `Assets/_Project/Scripts/Hecton8.Core.asmdef`. The live diff is `23` missing generated references: `Hecton8.AI.Cognition`, `Hecton8.AI.Ecology.Migration`, `Hecton8.Animation.IK`, `Hecton8.Audio.Echolocation`, `Hecton8.Audio.Propagation`, `Hecton8.Audio.Virtualization`, `Hecton8.Audio.Virtualization.Contracts`, `Hecton8.Core.Bucketing`, `Hecton8.Core.Database`, `Hecton8.Core.Persistence.Paging`, `Hecton8.Core.Scheduling`, `Hecton8.Environment.Fluids`, `Hecton8.Environment.Fluids.Contracts`, `Hecton8.Inventory.Algorithms`, `Hecton8.Inventory.Corrosion`, `Hecton8.Inventory.Corrosion.Contracts`, `Hecton8.Physics.CCD`, `Hecton8.Physics.Tethers.Contracts`, `Hecton8.SpaceEngine098Terrain`, `Hecton8.UI.Diegetic.Contracts`, `Hecton8.Vehicles.Physics.Contracts`, `Hecton8.World.GPR`, and `Hecton8.World.Terrain`. Treating those as missing code would cause fake wrappers and duplicate contracts.

Solution: add an editor-only `CSPROJ001` validation path in `HectonComplianceValidator`. It reads the durable `Hecton8.Core.asmdef` reference list, checks the current generated `Hecton8.Core.csproj`, and reports missing generated references with explicit wording that Unity project files must be regenerated before external `dotnet build` output is used as source evidence.

Rejected Alternatives: editing `Hecton8.Core.csproj` directly was rejected because Unity-generated project files are not durable authority. Adding placeholder namespaces/types was rejected because the source assemblies already exist and the failure is project-model drift, not absent implementation. Claiming the current `dotnet build` error list as gameplay code breakage was rejected because the first failure class is generated reference omission.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. The scalability gain is process-level: agents stop spending CPU/architecture work on duplicate stubs and instead restore the correct Unity asmdef/project boundary before profiling or runtime validation.

Hardware Impact: Editor-only validation. Estimated low-end i3/MX350 frame impact: 0.000 ms. Build/CI impact is bounded string scanning of one asmdef and one generated csproj.

## Decision 042 - Generated Project Drift Needs A Source-Backed Bridge Until Unity Regenerates Correctly

Problem: Unity Bee's current response files contain source files and asmdef references that the root generated `.csproj` files do not expose. `Hecton8.Core.csproj` was missing first-party references already present in `Hecton8.Core.asmdef` and `Library/ScriptAssemblies`, while `Hecton8.World.Contracts.csproj` missed current contract files. A non-destructive Unity batchmode project-refresh attempt on `2026-05-14` did not regenerate the stale project files. Leaving the root projects stale means external `dotnet build` reports false namespace/type failures and tempts agents to create duplicate stubs.

Solution: add a source-backed compatibility bridge in `Directory.Build.targets`. It leaves generated `.csproj` files untouched, but augments the `Hecton8.Core` and `Hecton8.World.Contracts` MSBuild item graph with the current source files and existing `Library/ScriptAssemblies` first-party references needed by the stale generated project surface. The bridge is deliberately build-surface only. It does not change Unity project settings, packages, asmdefs, scene content, or runtime registration.

Rejected Alternatives: editing `Hecton8.Core.csproj` / `Hecton8.World.Contracts.csproj` directly was rejected because they are generated files and will drift again. Adding placeholder `Hecton8.Logistics.Grid` or prompt-cache stubs was rejected because the real source exists. Adding or changing Unity IDE/package generation settings was rejected because package/project-setting mutation is outside this request and forbidden without explicit permission. Leaving `PlayerLookTargetPromptCache.cs` as an empty comment was rejected because current call sites require the type and `GlobalSignals.cs` does not define it.

Scalability potential: Low = no runtime budget spent; external compile evidence stops misclassifying generated-project drift as gameplay-code failure. Middle = agents can repair true source errors faster because stale project-surface errors are removed. High/Ultra = richer source/binary layout validation can run in CI without burning human time on duplicate stubs. The restored prompt cache uses fixed slots: Low/Middle keep bounded 64-slot prompt staging; High/Ultra can spend visual/UI budget on richer diegetic prompts later without changing the signal payload shape.

Hardware Impact: `Directory.Build.targets` bridge runtime impact is 0.000 ms/frame. `PlayerLookTargetPromptCache` owns `64 * 4 + 64 * 1 + 4096 * 2 = 8512` bytes plus array headers as cold managed storage and performs at most 64 char copies per store/copy call. Estimated low-end i3/MX350 hot-frame impact is below profiler resolution, but no GCMonitor/profiler capture was run; evidence class is CLI_COMPILE plus STATIC_SOURCE only.

## Decision 043 - Remove Dead Audio Probe Instead Of Carrying A First-Party Warning

Problem: After the stale project surface was bridged and dependencies were rebuilt, the honest `Hecton8.Core` no-restore compile still emitted one first-party warning: `PlayerCriticalProceduralAudioRenderer.PrologueSplashdownSineSweepProbeJob.NormalizedTime` was never assigned. Source search showed the private Burst probe job had no schedule sites. The actual prologue splashdown audio path is `RenderPrologueSplashdownSample()`.

Solution: delete the unused private `PrologueSplashdownSineSweepProbeJob`. This removes dead compile surface and preserves the existing runtime sample path.

Rejected Alternatives: suppressing CS0649 was rejected because the field was not a serialized Unity field and the struct had no call sites. Assigning a dummy value was rejected because that would keep dead code alive and fake intent. Leaving the warning in docs was rejected because the warning was first-party and removable without changing public API.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. Removing dead source reduces compile noise and keeps audio warnings from hiding real DSP/render warnings later.

Hardware Impact: Runtime impact 0.000 ms/frame. No managed allocation change. No profiler capture was run; evidence class is CLI_COMPILE plus STATIC_SOURCE.

## Decision 044 - Root Hecton8 Project Sweep Must Be Serial And Evidence-Classed

Problem: After Core compile recovery, older docs still treated Editor and test project proof as blocked or historical. The first `--no-restore` attempts for Editor/PlayModeTests/World.Dots failed on missing `project.assets.json`, which is restore state, not C# source failure. Also, parallel `dotnet build` over Unity-generated projects can produce false lock noise because project outputs share `Temp\obj`.

Solution: run the root `Hecton8*.csproj` compile sweep serially. Use restore/build only to recreate missing MSBuild assets and referenced `Temp\bin\Debug` DLLs, then use serial `--no-restore -m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` as the final external compile surface. Current root Hecton8 projects now compile at `0 Warning(s)` / `0 Error(s)` under that surface: Core, Editor, PlayModeTests, World.Contracts, World.Dots, Bootstrap.Contracts, Input.Generated, and Input. A later sanity rerun proved the restore-state boundary can recur when `Temp\obj` is missing (`Hecton8.Core` hit `NETSDK1004`), but serial restore/build followed by the final no-restore sweep again produced `0 Warning(s)` / `0 Error(s)` for all eight root Hecton8 projects. Stable docs and report indexes were updated to state this exact boundary instead of leaving R40-only wording.

Rejected Alternatives: treating `NETSDK1004` as source failure was rejected because restore fixed it. Using parallel build output as final evidence was rejected because the docs already record false-lock risk under shared `Temp\obj`. Treating full restore-graph warnings from URP/GPUInstancer/Crest/ShaderGraph/MapMagic as first-party Hecton8 warnings was rejected because the final isolated root no-restore surface is the controlled evidence target. Claiming Play Mode or Unity Console health was rejected because MCP still fails at `127.0.0.1:8088/mcp`.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. Process scalability improves because agents can distinguish restore-state, generated-project drift, vendor warning noise, and first-party source failure before making runtime or architecture claims.

Hardware Impact: Runtime impact 0.000 ms/frame. Build-only work. No profiler, GCMonitor, Unity Console, Play Mode, Memory Profiler, player build, or scene-wiring proof was captured.

## Decision 045 - Active Reference Docs Need The R41 Boundary, Not Only The May 13 Missing-Artifact Warning

Problem: R41 made the current external root `Hecton8*.csproj` no-restore CLI compile surface clean after restore assets exist, but many active reference docs still carried the older May 13 one-line override: May 11 compile artifact absent and runtime proof pending. That line was not false, but it was incomplete after R41 and could make readers miss the current compile boundary or keep treating May 11 as the only build-evidence discussion.

Solution: perform a mechanical exact-string update across active non-archive/non-deprecated markdown, excluding dated report snapshots. The replacement keeps the missing May 11 artifact demotion, adds the May 14/R41 external root CLI compile surface (`0 Warning(s)` / `0 Error(s)` after restore assets exist), and explicitly preserves the non-claim boundary for Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality. A second governance sweep updated the remaining top-level authority/index surfaces, bringing R42 to `49` non-DOC_AUDIT-memory docs touched.

Rejected Alternatives: editing old dated reports was rejected because reports are evidence snapshots. Leaving the May 13-only reference line was rejected because it is now incomplete for active reference docs. Leaving top-level governance docs on May 13-only wording was rejected after the second stale-string scan found they still guided readers to an incomplete boundary. Claiming runtime health from R41 was rejected because Unity MCP Console remains unavailable and CLI compile does not prove runtime/editor state.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. Process scalability improves because active reference docs now route readers to the same current compile/runtime boundary instead of forcing each agent to rediscover the May 13 -> R41 chain.

Hardware Impact: Runtime impact 0.000 ms/frame. Documentation-only work. No profiler, GCMonitor, Unity Console, Play Mode, Memory Profiler, player build, or scene-wiring proof was captured.

## Decision 048 - Concurrent Docs Need A R43/R45 Rebase Instead Of Trusting Prior Reports

Problem: Concurrent documentation edits reintroduced stale R41/R38 current-state wording after the root compile surface had already been rechecked in R43. Some active docs again made the old full-Core-blocked R38 evidence sound current, while repeated reference docs carried the R41-only line without the later R43 `Temp\bin\Debug`, `Temp\obj`, and shared-lock caveats. The active DOC_AUDIT memory header also drifted behind the current continuation.

Solution: treat docs as mutable evidence, not as settled truth. Re-read the active status/rationale, project authority, registry mandates, and Unity MCP workflow boundary; scan active docs for exact stale patterns; then reapply the R43 boundary across repeated reference docs and top-level authority surfaces. The current statement is narrow: eight root `Hecton8*.csproj` projects have single-project no-restore CLI compile evidence at `0 Warning(s)` / `0 Error(s)` with `LASTEXITCODE=0` only after restore assets and referenced `Temp\bin\Debug` DLLs exist. Missing `Temp\obj` assets, missing referenced DLLs, and shared `Temp\obj` locks are evidence hazards, not gameplay/runtime proof.

Rejected Alternatives: trusting the previous R43/R44 report was rejected because the live files contradicted it. Editing dated report snapshots was rejected because snapshots must preserve historical evidence. Rerunning broad source fixes was rejected because this pass found documentation drift, not a new code defect. Claiming Unity runtime health from CLI compile was rejected because Unity MCP Console remains unavailable and no Play Mode, profiler, GCMonitor, player build, memory, scene-wiring, or visual-quality proof was captured.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. Process scalability improves because current docs now separate restore-state hazards, generated-project drift, vendor/package warnings, transient file locks, and actual source failures. Low-end and high-end device claims remain blocked until runtime captures exist.

Hardware Impact: Runtime impact 0.000 ms/frame. Documentation-only work. No code path, allocation path, frame-time path, or render path was changed.

## Decision 049 - Scatter Docs Must Distinguish Source-Present Refactor Pieces From Runtime Proof

Problem: Active scatter docs had stale or contradictory current-state claims. `SCATTER_REFACTOR_EXECUTION_PLAN.md` still said `ScatterHeuristicsUtility`, `ScatterRescueContext`, and `GetGridPlacements()` were absent even though current source has `ScatterHeuristicsUtility.cs`, a private director-partial `ScatterRescueContext`, and `WorldProceduralScatterDirector.GetGridPlacements()`. The same docs also carried old file-size/line-count inventory. That creates two bad outcomes: future agents may re-create existing helpers, or may mark the refactor complete when it is only partially source-present.

Solution: perform a source-backed R46 scatter reality pass. Measure the current file sizes/line counts with the filesystem, scan exact source symbols, and update the scatter docs plus static x-ray. The corrected boundary is narrow: `SamplingSnapshot.cs`, `ScatterHeuristicsUtility.cs`, `ScatterDiagnosticsTracker.cs`, and `WorldProceduralScatterWorkingMemory.cs` exist; `ScatterRescueContext` exists but is not the manifesto's standalone `ref struct`; `GetGridPlacements()` exists but returns bucketed placement lists; `ScatterSpawningService` was not found and spawn/reconcile remains director-owned. DOTS remains disabled/optional because `com.unity.entities` is absent from `Packages/manifest.json` and the DOTS asmdef is define-gated and auto-reference disabled.

Rejected Alternatives: deleting or rewriting the manifesto was rejected because it is still useful as a target shape. Marking checked boxes as runtime proof was rejected because no Unity scene, profiler, GCMonitor, Memory Profiler, player build, or visual validation was captured. Editing source was rejected in this pass because the defect found was documentation truth drift, not a scoped code bug.

Scalability potential: Low = agents stop duplicating helpers and keep MX350/GC claims pending until captures exist. Middle = refactor work can target the real remaining bottleneck, spawn/reconcile extraction, instead of already-existing heuristic/diagnostic files. High/Ultra = future visual-overkill scatter work can build on the same evidence boundary without faking DOTS readiness.

Hardware Impact: Runtime impact 0.000 ms/frame. Documentation-only work. No managed allocation, NativeCollection, GPU buffer, or frame-time path was changed.

## Decision 050 - May 15 Core Build / H-Phi Boundary Must Supersede Archived Task Noise

Problem: `/Docs/Tasks` and `/Docs/AgentLogs` are archive-prone evidence lanes, but active root/stable docs must still state the current project truth. The live Hecton8 worktree also had contradictory same-day evidence: an earlier Core CLI build failed on `SaveManager` referencing `MacroDatabasePayloadFlags` through stale generated-CLI contract visibility, while the current source and later build no longer failed. Earlier H-Phi artifacts also included a MemoryAlignment floor failure that the current full budget run superseded.

Solution: create fresh artifact-backed evidence first, then promote only the narrow truth into stable docs. DOC_AUDIT captured clean Core/H-Phi artifacts, detected live file-write races, reran both lanes, and promoted the latest observed current-disk boundary: `Docs/AgentLogs/Build_DOC_AUDIT_R47_20260515_194535_AfterQuestWaveCore.log` exits `0` with `Build succeeded`, `0 Warning(s)`, and `0 Error(s)`; `Docs/AgentLogs/HPhi_DOC_AUDIT_R47_20260515_194809_AfterQuestWaveBudgetGate.json` exits `0` with `MemoryAlignment=0.506309148`, `DataSovereignty=0.021306032`, `RuntimeHPhiRisk=0.000634555`, duplicate signal debt `0`, Unity loop debt `0`, and Core graph debt still at `25/10/14/8/6`. Stable/root docs now carry that boundary and still mark Unity runtime proof absent.

Rejected Alternatives: using chat memory was rejected because context is volatile. Treating the failed 18:35 build, 19:27 write-race build, or pre-Quest-wave artifacts as final was rejected because later current-disk builds passed. Hiding failed/race artifacts was rejected because they explain generated-CLI contract visibility and live-churn hazards. Promoting H-Phi static pass to runtime readiness was rejected because static text counters are not profiler, GCMonitor, Unity Console, Play Mode, player build, or visual proof.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. Process scalability improves because agents now see the current Core CLI and H-Phi boundary in root/stable docs even if `/Tasks` and `/AgentLogs` are archived. Low-end and high-end hardware claims remain blocked until runtime captures exist.

Hardware Impact: Runtime impact 0.000 ms/frame. Documentation/evidence synchronization only. No code path, allocation path, NativeCollection lifetime, GPU buffer, frame-time path, or visual path was changed by this DOC_AUDIT promotion.

## Decision 051 - Do Not Raise H-Phi Budget To Hide Post-WFC Registry Coupling

Problem: After the WFC/persistence wave, the latest DOC_AUDIT H-Phi attempt failed on `GlobalRegistrySurface=5076 > 5075`, and the latest integration build then briefly failed on a non-existent `ScalabilityTierBindingBridge` reference. Keeping the older R47 docs as "latest" would be false, and raising the registry budget would accept coupling drift instead of removing it.

Solution: first verify the current bridge source by rebuilding Core, then reduce actual coupling in `PlayerKinematicsRuntime`. The kinematics path now caches `GlobalRegistry.ScalabilityTier` once per `Time.frameCount` through `ResolveScalabilityTier()` and reuses that value for low/high-tier SDF, hand-probe, advection, roll-wave, and GPU-flow cadence decisions. This preserves rich `Mid` cadence behavior in `ResolveGpuFlowProbeFrameMask()` and avoids replacing it with the two-tier platform fallback.

Rejected Alternatives: raising `MaxGlobalRegistrySurface` from `5075` to `5076` was rejected because it hides technical debt. Replacing kinematics tier logic with `PlatformIntegrationBridge` was rejected because that bridge intentionally collapses `Mid` into Low/High two-tier profile bytes. Leaving the build-failure artifact unmentioned was rejected because it explains why R49 supersedes R47. Claiming runtime safety was rejected because no Unity Console, Play Mode, profiler, GCMonitor, player build, save/load, scene-wiring, or visual proof was captured.

Scalability potential: Low = one cached scalability read per rendered frame instead of repeated registry reads in kinematics, with Low/MX350 still using cheaper SDF/hand-probe cadence. Middle = `Mid` keeps its less aggressive GPU-flow probe mask. High/Ultra = high-tier roll uses the smoother sine approximation while saved coupling budget can be spent on visual overkill later without widening the registry surface.

Hardware Impact: Expected low-end i3/MX350 CPU gain is small but structurally correct: repeated tier registry reads in the kinematics path collapse to one cached read per `Time.frameCount`; H-Phi static counters improved to `GlobalRegistrySurface=5060/5075`, `ManagedFormatSurface=534/564`, and `PrimaryManagedRuntimeRisk=147/177`. Measured runtime frame-time gain is `PENDING VERIFICATION`; evidence class is `CLI_COMPILE` plus `STATIC_SOURCE_FULL_SCAN`.
