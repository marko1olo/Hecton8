# LOG_DOC_AUDIT

Top = old. Bottom = new.

## R38 - 2026-05-13 - Active Memory Restart

What was wrong: Active `Docs/Tasks/Status_DOC_AUDIT.md`, `Docs/AgentLogs/Rationale_DOC_AUDIT.md`, and `Docs/AgentLogs/LOG_DOC_AUDIT.md` were absent after Batch005 archive movement, so continuation state could silently drift from disk-backed memory.

What was done: Recreated active DOC_AUDIT working files and linked prior history to `Docs/Archive/Batch005/`.

Cinematic Cheats used: none; documentation/state hygiene only.

Exact Microseconds saved: 0 runtime microseconds. Prevents human/integrator time loss from stale status location.

## R38 - 2026-05-13 - Pager Fault Accounting / WFC Persistence Contract

What was wrong:
- `H8BinaryWorldPager.RunWorkerLoop()` decremented pending write/read counters only after `ProcessWrite()` / `ProcessRead()` returned normally. Unexpected exceptions before inner IO catches could stop the worker with stale pending counters.
- `IAsyncPersistenceService` exposed WFC outpost persistence methods that `SaveManager` did not implement once current `Core.Contracts` source was used.
- R37 full-Core compile success was no longer current: the live worktree now blocks full `Hecton8.Core` on unrelated audio/scanner/submarine-buffer/arena/fluid/UI/fauna churn.

What was done:
- Added per-command pager worker wrappers that decrement pending counters in `finally`, record fault telemetry, fail-close the pager, zero exposed pending counters, and dump black-box telemetry on unexpected command faults.
- Implemented WFC outpost state persistence in `SaveManager` using DataVault `WfcOutpostGrid`, fixed native packed/restore scratch, existing `SaveBinaryPayloadCodec`, and `IMacroDatabaseService.MarkDirty` / `TryGetPayload`.
- Updated stable docs and the R38 X-Ray section to demote stale full-Core proof and list the current blocking files honestly.

Cinematic Cheats used:
- WFC mutable state persists as a compact bitmask payload, not a full simulated outpost object graph.
- Restore reads bounded MacroDB payload handles and unpacks only mutable flags back into the WFC grid.

Exact Microseconds saved:
- Pager normal path: 0 us/frame; only failure accounting changes.
- WFC unchanged snapshot skip: avoids one pager enqueue/write for repeated identical sector state.
- Full-Core proof correction: 0 runtime us; prevents stale build evidence from being treated as current.

## R39 - 2026-05-13 - Generated Project / Asmdef Drift Tripwire

What was wrong:
- Current `dotnet build Hecton8.Core.csproj --no-restore -v:minimal` fails on missing namespaces/types because the generated `Hecton8.Core.csproj` does not reflect the current `Hecton8.Core.asmdef` reference list.
- The source assemblies and asmdefs already exist for the first blocker class, so adding stubs would be false repair.

What was done:
- Added editor-only `CSPROJ001` validation in `HectonComplianceValidator`.
- The validator now compares `Assets/_Project/Scripts/Hecton8.Core.asmdef` against `Hecton8.Core.csproj` and reports missing generated references before agents treat external `dotnet build` failures as code-level evidence.
- Live check found `23` missing first-party generated references in `Hecton8.Core.csproj`.

Cinematic Cheats used:
- None. This is build-surface hygiene, not simulation/rendering.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: prevents duplicate namespace/type stubs for assemblies that already exist under asmdef authority.

## R40 - 2026-05-14 - Source-Backed MSBuild Bridge / Core CLI Compile Recovery

What was wrong:
- A non-destructive Unity batchmode project-refresh attempt did not regenerate stale root generated `.csproj` files.
- Bee response files and asmdefs showed newer source/reference truth than `Hecton8.Core.csproj` and `Hecton8.World.Contracts.csproj`.
- The stale external project surface reported false missing namespace/type errors for existing first-party systems, including logistics grid contracts and current Core-side source files.
- `Assets/_Project/Scripts/Core/PlayerLookTargetPromptCache.cs` existed as an empty comment while current `PlayerInteraction` and `DiegeticTooltipSystem` still required `PlayerLookTargetPromptCache`.

What was done:
- Added a `Directory.Build.targets` bridge for `Hecton8.Core` and `Hecton8.World.Contracts` instead of editing generated `.csproj` files.
- The bridge adds the missing current source files and existing first-party `Library/ScriptAssemblies` references required for controlled external CLI compile.
- Restored `PlayerLookTargetPromptCache` as a fixed, bounded prompt-text cache in namespace `Hecton8.Core`.
- Removed unused private `PrologueSplashdownSineSweepProbeJob` from `PlayerCriticalProceduralAudioRenderer`; the live splashdown path already uses `RenderPrologueSplashdownSample`.
- Verified controlled serial builds:
  - `dotnet build Hecton8.Core.csproj --no-restore -m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` -> `0 Warning(s)`, `0 Error(s)`.
  - `dotnet build Hecton8.World.Contracts.csproj --no-restore -m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary` -> `0 Warning(s)`, `0 Error(s)`.
- Rechecked Unity MCP Console after CLI verification; `read_console` still fails at `http://127.0.0.1:8088/mcp`, so no Unity Console claim is made.

Cinematic Cheats used:
- None in runtime simulation/rendering.
- Process cheat: source-backed MSBuild bridge keeps generated project drift from consuming engineering time while Unity project generation remains stale.
- UI data cheat: prompt text rides a hash-only signal and fixed cache instead of embedding variable strings into the signal lane.

Exact Microseconds saved:
- Runtime bridge cost: 0 us/frame.
- Prompt cache: bounded to 64 char copies per store/copy call; no managed hot-path allocation added by source inspection.
- Audio warning cleanup: 0 us/frame; dead private probe removal.
- Process: removes the false 128-error external Core wall and replaces it with controlled `0`-error CLI compile evidence.

## R41 - 2026-05-14 - Root Hecton8 Project Compile Sweep

What was wrong:
- R39/R40 left `Hecton8.Editor.csproj`, `Hecton8.PlayModeTests.csproj`, and `Hecton8.World.Dots.csproj` as older or partial evidence.
- Initial `--no-restore` attempts on those projects failed on missing `project.assets.json`, which is restore-state debt, not source compile failure.
- A brief parallel check of small Hecton8 projects was not acceptable as final evidence because Unity-generated projects share `Temp\obj`.

What was done:
- Rebuilt missing restore assets and dependencies serially.
- Re-ran final no-restore builds serially for every root `Hecton8*.csproj`.
- Updated stable documentation and report index to R41 so the current root-project CLI compile state replaces older R39/R40 partial wording without becoming a runtime claim.
- Re-ran the root sweep after docs updates. First no-restore attempt hit `NETSDK1004` on missing `Temp\obj\Hecton8.Core\project.assets.json`; serial restore/build recreated assets; final serial no-restore sweep again returned `0 Warning(s)`, `0 Error(s)` for all eight root Hecton8 projects.
- Current final no-restore compile surface:
  - `Hecton8.Core.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.Editor.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.PlayModeTests.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.World.Contracts.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.World.Dots.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.Bootstrap.Contracts.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.Input.Generated.csproj` -> `0 Warning(s)`, `0 Error(s)`.
  - `Hecton8.Input.csproj` -> `0 Warning(s)`, `0 Error(s)`.
- Unity MCP Console was rechecked and still fails at `http://127.0.0.1:8088/mcp`.
- Full restore graph still carries vendor/package warnings from URP/GPUInstancer/Crest/ShaderGraph and MapMagic/Den.Tools; those are not present in the final isolated root Hecton8 no-restore surface.
- Targeted stale-phrase scan found no remaining conflicting May 4/R40-only compile-status wording in updated authority files.
- `git diff --check` on the touched file set is clean except Git LF-to-CRLF working-copy warnings.

Cinematic Cheats used:
- None. This is external build-surface verification.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: removes stale "Editor blocked by missing Core DLL" and narrows current evidence to actual root Hecton8 CLI compile status.

## R42 - 2026-05-14 - Active Reference Docs R41 Override Propagation

What was wrong:
- Many active reference docs still carried the May 13 one-line override that only said the May 11 compile artifact was absent and runtime proof was pending.
- After R41 this was incomplete because current external root `Hecton8*.csproj` CLI compile proof exists, while runtime proof is still absent.

What was done:
- Scanned active non-archive/non-deprecated docs for stale May 13/Missing-May-11/R40-only compile-boundary wording.
- Mechanically updated `38` active markdown files, excluding dated report snapshots, to include the May 14/R41 boundary.
- Ran a second governance sweep and updated the remaining top-level authority/index surfaces, for `49` non-DOC_AUDIT-memory docs touched in R42.
- R42 stale-string scan now finds no targeted May 13-only / `until restored or replaced` / May 4-latest / R40-only compile-status phrases in active non-archive/non-deprecated docs, excluding dated report snapshots.
- `git diff --check` on the touched docs is clean except Git LF-to-CRLF working-copy warnings.
R42 boundary written:
  - May 11 artifact remains absent and stale.
  - Current root `Hecton8*.csproj` no-restore CLI compile surface is `0 Warning(s)` / `0 Error(s)` after restore assets exist.
  - Full restore graphs still carry vendor/package warnings.
  - Unity Console, Play Mode, profiler, GCMonitor, player build, scene wiring, and visual-quality proof remain absent.

Cinematic Cheats used:
- None. Documentation/evidence synchronization only.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: active docs now share the same current compile/runtime-proof boundary, reducing repeated rediscovery work by other agents.

## R45 - 2026-05-14 - Concurrent R43/R44 Wording Rebase

What was wrong:
- Active documentation drifted under concurrent edits after the R43 root compile recheck.
- Repeated reference docs still used the R41-only compile boundary and omitted the later R43 caveats around missing referenced `Temp\bin\Debug` DLLs, missing `Temp\obj` restore assets, and shared `Temp\obj` locks.
- Several authority surfaces still made R38's old full-Core-blocked state sound current instead of historical.
- DOC_AUDIT memory headers had to be brought back to the R45 continuation marker.

What was done:
- Re-read active DOC_AUDIT memory, project authority, relevant registry mandates, and Unity MCP workflow limits before editing.
- Updated `35` active repeated reference docs to the current R43 root compile boundary.
- Rebased top-level authority/index docs including `Docs/README.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/QUALITY_GATES.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ROOT_DOCS_REFERENCE.md`, `Docs/HECTON8_RUNTIME_EXECUTION_MASTER_PLAN.md`, architecture/report/Archivarius indexes, and related active reference surfaces.
- Preserved the hard evidence boundary: R43 is external CLI compile proof only, not Unity Console, Play Mode, profiler, GCMonitor, player build, memory, scene-wiring, or visual-quality proof.
- Targeted active-doc scans now find no remaining current R41/R38 compile-blocked stale strings outside excluded snapshots and DOC_AUDIT history.

Cinematic Cheats used:
- None. Documentation/evidence integrity pass only.
- Process cheat: exact stale-pattern scanning plus mechanical line replacement avoided hand-edited partial truth.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: prevents repeated false triage around old R38/R41 compile status and keeps future agents from treating CLI evidence as runtime proof.

## R46 - 2026-05-14 - Scatter Runtime Source Reality Recheck

What was wrong:
- Active scatter docs had stale and contradictory refactor-state claims.
- `SCATTER_REFACTOR_EXECUTION_PLAN.md` still said `ScatterHeuristicsUtility`, `ScatterRescueContext`, and `GetGridPlacements()` were absent, while current source shows partial implementations.
- Static x-ray file-size inventory for scatter/world runtime owners was stale.
- The DOTS boundary needed sharper wording because the DOTS asmdef exists but `com.unity.entities` is absent from `Packages/manifest.json` and the assembly is define-gated/auto-reference disabled.

What was done:
- Re-read streaming, persistence, flora instancing, and cinematic-fake mandates.
- Measured current source inventory:
  - `WorldProceduralScatterDirector.cs`: `539165` bytes / `11907` lines.
  - `WorldProceduralScatterWorkingMemory.cs`: `42878` bytes / `590` lines.
  - `WorldChunkResidencyManager.cs`: `187902` bytes / `4368` lines.
  - `HectonMapMagicVegetationBridge.cs`: `336953` bytes / `7041` lines.
  - `WorldProceduralFieldSampler.cs`: `263836` bytes / `5133` lines.
  - `PersistentWorldRegistry.cs`: `273872` bytes / `6159` lines.
- Updated `Docs/Scatter_Runtime/README.md`, `SCATTER_REFACTOR_EXECUTION_PLAN.md`, `SCATTER_REFACTORING_MANIFESTO_V2.md`, and `Docs/PROJECT_STATE_STATIC_XRAY.md`.
- Corrected the source boundary: `SamplingSnapshot`, `ScatterHeuristicsUtility`, `ScatterDiagnosticsTracker`, and `ScatterWorkingMemory` exist; `ScatterRescueContext` is private/director-partial and not the manifesto `ref struct`; `GetGridPlacements()` exists but returns bucketed placement lists; `ScatterSpawningService` was not found.
- Preserved runtime truth: no Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, player build, scene wiring, or visual proof was captured.

Cinematic Cheats used:
- None in runtime.
- Process cheat: source-symbol and file-inventory scans replaced stale refactor checklist assertions.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: prevents duplicate helper work and narrows the next real scatter refactor target to spawn/reconcile extraction plus runtime proof.

## R47 - 2026-05-15 - Current-Disk Core Build / H-Phi Boundary

What was wrong:
- The user corrected the active project context back to Hecton8, while root/stable docs still needed the newest current-disk proof because `/Docs/Tasks` and `/Docs/AgentLogs` can be archived.
- Same-day evidence conflicted: one new Core CLI artifact failed on stale generated-CLI visibility of `MacroDatabasePayloadFlags`, while current source and a later Core build passed.
- Earlier same-day H-Phi evidence included a MemoryAlignment floor failure, but the current full budget gate needed a fresh artifact before stable docs could supersede it.

What was done:
- Captured `Docs/AgentLogs/Build_DOC_AUDIT_CONTINUATION_20260515_183949_Hecton8Core.log`: `Hecton8.Core.csproj`, `EXIT=0`, `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Captured `Docs/AgentLogs/HPhi_DOC_AUDIT_CONTINUATION_20260515_184218_CurrentDiskBudgetGate.json`: `EXIT=0`, `MemoryAlignment=0.506309148`, `DataSovereignty=0.021306032`, `HPhiStaticRisk=0.000634446`, `DuplicateSignalNames=0`, `UnityUpdateMethods=0`, Core graph debt `25/10/14/8/6`.
- Promoted the later same-day integration artifacts as the latest observed boundary: `Docs/AgentLogs/Build_INTEGRATION_ASSEMBLY_SURGEON_20260515_190406_CurrentDisk25.log` (`EXIT=0`, `0 Warning(s)`, `0 Error(s)`) and `Docs/AgentLogs/HPhi_INTEGRATION_ASSEMBLY_SURGEON_20260515_185213_CurrentDiskBudgetGate10.json` (`EXIT=0`, `RuntimeHPhiRisk=0.000634446`, `MemoryAlignment=0.506309148`).
- Updated stable/root docs: `Docs/README.md`, `Docs/QUALITY_GATES.md`, `Docs/ARCHITECTURE/README.md`, `Docs/ARCHITECTURE/HECTON_PHI_STATIC_METRIC.md`, `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`, `Docs/SYSTEMS_CONTRACTS.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, `MASTER_RELEASE_WORK_PLAN.md`, and `BUILD_PLAYTEST_ISSUES.md`.
- Preserved the hard limitation: no Unity import, Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene wiring, save/load, or visual-quality proof was captured.

Cinematic Cheats used:
- None in runtime. Evidence promotion only.
- Process cheat: artifact-first promotion into root/stable docs prevents archived task/log drift from becoming project truth.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: avoids repeated false triage around stale `MacroDatabasePayloadFlags` and MemoryAlignment artifacts.
