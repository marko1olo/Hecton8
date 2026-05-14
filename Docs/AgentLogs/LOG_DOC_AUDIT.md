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

## R43 - 2026-05-14 - Root CLI Recheck / Active Docs Rebase

What was wrong:
- Active docs had been moved to the R41/R42 compile boundary, but another live recheck exposed more precise current hazards around generated output state.
- Earlier C# errors in fauna/audio were not stable current source blockers.
- `--no-restore` can fail before source compilation when `Temp\obj` restore assets are missing, referenced `Temp\bin\Debug` DLLs are missing, or shared `Temp\obj` output files are locked by concurrent agents.

What was done:
- Ran `dotnet build-server shutdown` before retrying shared-output evidence.
- Rebuilt Core with restore to recreate referenced package DLLs; result: `0 Warning(s)`, `0 Error(s)`.
- Restored all eight root projects serially after `NETSDK1004` on Editor restore assets.
- Rebuilt Editor with restore to recreate `Den.Tools.dll` and `MapMagic.dll`; result: `0 Error(s)`, vendor/package warnings only.
- Rechecked all eight root Hecton8 projects as individual no-restore commands with `-m:1 /nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -v:minimal -clp:Summary`.
- Individual final results: `Hecton8.Core.csproj`, `Hecton8.Editor.csproj`, `Hecton8.PlayModeTests.csproj`, `Hecton8.World.Contracts.csproj`, `Hecton8.World.Dots.csproj`, `Hecton8.Bootstrap.Contracts.csproj`, `Hecton8.Input.Generated.csproj`, and `Hecton8.Input.csproj` each returned `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, `LASTEXITCODE=0`.
- Updated `35` repeated active reference override lines plus top-level governance/index/static-map/report docs from latest-R41 wording to R43 wording.

Cinematic Cheats used:
- None. Evidence hygiene only.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: prevents false source edits caused by restore-state, missing temporary reference DLLs, or shared `Temp\obj` locks.

Current R43 boundary:
- Root Hecton8 CLI no-restore compile surface is clean after restore assets and referenced `Temp\bin\Debug` DLLs exist.
- Full restore graphs still carry vendor/package warnings.
- Unity MCP Console still fails at `127.0.0.1:8088/mcp`.
- No Unity Console, Play Mode, profiler, GCMonitor, player build, frame-time, memory, scene-wiring, save/load, or visual-quality proof is claimed.

## R44 - 2026-05-14 - R38 Current-Blocked Wording Cleanup

What was wrong:
- R43 had made the current external root `Hecton8*.csproj` no-restore CLI surface clean.
- Four active authority/index docs still contained R38 wording that read like the current full `Hecton8.Core` probe was blocked by unrelated active churn.
- That wording was historically true for R38, but stale as current status.

What was done:
- Re-read `AGENTS.md`, `Docs/Actual Domains of Project.txt`, `.agents-skills/README.md`, `QA_Evidence_Text_Filter_Audit.txt`, `PROJECT_LTS_Compatibility_Layer.txt`, `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, and `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`.
- Scanned active non-archive/non-deprecated docs, excluding dated report snapshots and DOC_AUDIT memory, for current-blocked Core wording.
- Updated `Docs/README.md`, `Docs/Reports/README.md`, `Docs/PROJECT_STATE_STATIC_XRAY.md`, and `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`.
- New wording preserves R38 as historical churn and states R43 superseded it with the clean external root CLI recheck.
- Re-ran stale-blocked and R41/latest scans; no active top-level stale hits remain outside DOC_AUDIT history.
- `git diff --check` on the edited docs is clean except Git LF-to-CRLF working-copy warnings.

Cinematic Cheats used:
- None. Documentation/evidence synchronization only.

Exact Microseconds saved:
- Runtime: 0 us/frame.
- Process: removes a current-status contradiction between R38 and R43 compile boundaries.
