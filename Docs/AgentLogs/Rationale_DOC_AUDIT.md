# Rationale_DOC_AUDIT

Agent: DOC_AUDIT
Domain: Documentation / Project Reality Audit / Editor Validation Tripwires
Current continuation: R42
Date: 2026-05-14

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

Solution: perform a mechanical exact-string update across active non-archive/non-deprecated markdown, excluding dated report snapshots. The replacement keeps the missing May 11 artifact demotion, adds the May 14/R41 external root CLI compile surface (`0 Warning(s)` / `0 Error(s)` after restore assets exist), and explicitly preserves the non-claim boundary for Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, import, scene wiring, and visual quality.

Rejected Alternatives: editing old dated reports was rejected because reports are evidence snapshots. Leaving the May 13-only reference line was rejected because it is now incomplete for active reference docs. Claiming runtime health from R41 was rejected because Unity MCP Console remains unavailable and CLI compile does not prove runtime/editor state.

Scalability potential: Low/Middle/High/Ultra runtime tiers are unchanged. Process scalability improves because active reference docs now route readers to the same current compile/runtime boundary instead of forcing each agent to rediscover the May 13 -> R41 chain.

Hardware Impact: Runtime impact 0.000 ms/frame. Documentation-only work. No profiler, GCMonitor, Unity Console, Play Mode, Memory Profiler, player build, or scene-wiring proof was captured.
