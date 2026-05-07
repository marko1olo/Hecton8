# Final Inquisition Native Scanner
Date: 2026-05-07
Status: PENDING VERIFICATION (BLOCKED BY MCP)
Scope: final re-audit of the NativeLeakScanner editor tool, Awaitable debt boundary, and fallback compile evidence while Unity MCP is unavailable

## Mandates Followed

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `AGENTS.md`

## Confession

The flaw found in this pass was inside `Assets/_Project/Scripts/Editor/NativeLeakScanner.cs`.

Old scanner path used regex-backed source scanning:

- removed `using System.Text.RegularExpressions`
- removed static `Regex` fields for allocation/disposal/sentinel patterns
- removed `MatchCollection allocations = AllocationRegex.Matches(codeText)`

This was not a gameplay hot path, but it was still expensive honest logic inside the audit domain.

Current replacement:

- `RunScan()` calls `CountNativeAllocations(codeText)` at `NativeLeakScanner.cs:61`.
- `CountNativeAllocations(...)` is a manual `IndexOf("new", ...)` token scan at `NativeLeakScanner.cs:110-138`.
- native type matching is explicit token comparison at `NativeLeakScanner.cs:140-157`.
- disposal/helper checks are manual call scans at `NativeLeakScanner.cs:174-225`.
- whitespace/token helpers are at `NativeLeakScanner.cs:228-243`.

## 0.1 ms Budget Boundary

No runtime Burst job was added by this pass.

There is no valid profiler proof that the scanner path executes under `0.1ms`; it is an editor menu command, not gameplay runtime. Any claim that this scanner is under the runtime 0.1ms tick budget would be a guess and is rejected.

What can be stated:

- no gameplay `Tick`, `Update`, `FixedUpdate`, player movement, UI response, or combat loop was modified by this patch.
- `NativeLeakScanner.cs` is under `Assets/_Project/Scripts/Editor/`.
- the entire file is editor-gated by `#if UNITY_EDITOR` at `NativeLeakScanner.cs:1` and closes at `NativeLeakScanner.cs:554`.

## GC / Logging Boundary

The strings and logging inside `NativeLeakScanner.cs` are editor-only, not runtime.

Line evidence:

- file-level `#if UNITY_EDITOR`: `NativeLeakScanner.cs:1`
- `Debug.LogError(summary)`: `NativeLeakScanner.cs:29`
- `Debug.Log(summary)`: `NativeLeakScanner.cs:33`
- `StringBuilder.ToString()`: `NativeLeakScanner.cs:481`
- file-level `#endif`: `NativeLeakScanner.cs:554`

Cold allocations are explicitly documented:

- `List<Finding>(128)`: `NativeLeakScanner.cs:54`
- `text.ToCharArray()`: `NativeLeakScanner.cs:251`
- `new string(buffer)`: `NativeLeakScanner.cs:309`
- `StringBuilder(8192)`: `NativeLeakScanner.cs:451`

No claim is made that these are runtime zero-GC paths. They are editor-only audit allocations.

## Awaitable Mask

Direct `Awaitable.NextFrameAsync` body found:

- `Assets/_Project/Scripts/Core/InputDispatcher.cs:1261-1267`

Targeted scan found no `Awaitable.NextFrameAsync` / `AwaitableDebtMonitor.NextFrameAsync` hits in:

- `Assets/_Project/Scripts/HectonPlayerMovement.cs`
- `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs`
- `Assets/_Project/Scripts/UI/**/*.cs`

Project-wide hits still exist in bootstrap, scene setup, smoke testers, save/world hydration, voxel/wreck async work, object pool warmup, floating origin, and player spawner paths. This pass did not prove every existing await is harmless. It only proves the newly touched scanner code added none and targeted player movement/UI files have no hits.

## Cache Line / Data Layout Boundary

Scanner data layout is not runtime SIMD/Burst layout.

Line evidence:

- `Finding` is a managed struct with a `string` reference and scalar fields at `NativeLeakScanner.cs:529-538`.
- `ScanResult` is a managed struct with string references, counters, and `Finding[]` at `NativeLeakScanner.cs:540-550`.
- file enumeration iterates `string[] files` with an index loop at `NativeLeakScanner.cs:51-56`.

This is acceptable only because the tool is editor-only. It is not a model for runtime NativeArray/Burst data layout.

## AAA Cheat

The final expensive lie implemented here:

- replaced source-regex allocation detection with a bounded manual token scanner.
- avoided `Regex.Matches` / `MatchCollection` allocation pressure in the audit path.
- kept output counts stable after rerun: `207` allocation files, `1116` allocation hits, `3` strict same-file direct-dispose misses.

Updated static scanner artifact:

- `CodexArtifacts/native-leak-scanner-results.static.json`

## Fallback Build Evidence

Unity MCP was unavailable during earlier attempts:

- `read_console`: `Unity session not ready for 'read_console' (ping not answered)`
- `validate_script`: Unity plugin session disconnected while awaiting command result
- continuation retry after the live editor-log read again returned `Unity session not ready for 'read_console' (ping not answered)`.

Current addendum:

- `read_console(types=error,warning,count=20,format=detailed,include_stacktrace=true)` returned `success=true`, message `Retrieved 0 log entries.`
- final retry later returned `Unity session not ready for 'read_console' (ping not answered)`, so the MCP channel remains unstable.
- This is live MCP console readback for the requested console surface, but it is not Play Mode, profiler, GCMonitor, player-build, or user-supplied verification proof.

Fallback compile artifacts:

- `CodexArtifacts/2026-05-07_FINAL_INQUISITION_CORE_BUILD.log`
- `CodexArtifacts/2026-05-07_FINAL_INQUISITION_EDITOR_NODEPS_BUILD.log`
- `CodexArtifacts/2026-05-07_FINAL_INQUISITION_NATIVELEAKSCANNER_CSC.log`
- `CodexArtifacts/2026-05-07_CONTINUE_CORE_RECHECK_AFTER_EDITORLOG.log`
- `CodexArtifacts/2026-05-07_CONTINUE_CORE_FULLDEPS_BUILD.log`
- `CodexArtifacts/2026-05-07_CONTINUE_FINAL_CORE_NODEPS_RECHECK.log`
- `CodexArtifacts/2026-05-07_LIVE_EDITORLOG_COMPILE_SLICE.txt`
- `CodexArtifacts/2026-05-07_FINAL_CURRENT_CORE_NODEPS_BUILD.log`
- `CodexArtifacts/2026-05-07_FINAL_CURRENT_NATIVELEAKSCANNER_CSC.log`

Observed fallback results:

- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, exit code `0`.
- `dotnet build Hecton8.Editor.csproj --no-restore --no-dependencies`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, exit code `0`.
- manual Unity Mono compile of `NativeLeakScanner.cs`: exit code `0`.
- continuation `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, exit code `0`.
- continuation `dotnet build Hecton8.Core.csproj --no-restore`: `Build succeeded`, `48 Warning(s)`, `0 Error(s)`, exit code `0`; warnings are package/third-party surfaces and were not patched under the third-party integrity rule.
- final continuation `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, exit code `0`, after the latest observed source write.
- current recheck `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, exit code `0`, at `2026-05-07 19:08:26 +04:00`.
- current manual Unity Mono compile of `NativeLeakScanner.cs`: exit code `0`, at `2026-05-07 19:08:38 +04:00`.

Live Unity `Editor.log` boundary:

- Unity process is running and project lock exists, so separate batchmode on the same project would contend with the open editor.
- `Editor.log` still contains historical Unity compiler errors such as `PhysicalHandController.cs(233,17): EnsureSuitCollisionShell` and older missing-type/interface errors.
- Current disk source contradicts the `EnsureSuitCollisionShell` error: `PhysicalHandController.cs` contains `EnsureSuitCollisionShell()` at current line `500`, and `Hecton8.Core.csproj` includes that file.
- Fresh `dotnet build Hecton8.Core.csproj` compiles the current disk source with `0 Error(s)`.
- Current MCP console readback returned zero error/warning entries after earlier connection failures. It still does not prove Play Mode behavior, profiler budget, GC budget, scene wiring, or player-build health.

Important limitation:

- generated `.csproj` files have not been regenerated by Unity during this dead-MCP session.
- `NativeLeakScanner.cs` is not yet visible in the generated `Hecton8.Editor.csproj` text scan.
- manual Unity Mono compile is the direct compile evidence for the new file until Unity refreshes project files and console logs can be read.

Status: PENDING VERIFICATION (BLOCKED BY MCP)
