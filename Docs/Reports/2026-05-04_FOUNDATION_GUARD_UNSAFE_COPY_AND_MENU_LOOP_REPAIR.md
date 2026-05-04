# Foundation Guard Unsafe Copy And Menu Loop Repair

Date: `2026-05-04`
Status: `PENDING VERIFICATION`
Scope: `CrashTelemetryBuffer`, `MainMenuController`, foundation guard scan

## Mandates Followed

- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`

## Defects Addressed

- `Tools/ReloadAudit/Scan-FoundationGuards.ps1` previously failed on raw `UnsafeUtility.MemCpy` outside `UnsafeMemoryCopyGuard` at `Assets/_Project/Scripts/CrashTelemetryBuffer.cs:903`.
- After the unsafe-copy fix, the same guard failed on `Assets/_Project/Scripts/MainMenuController.cs:152` because native `Update()` is not an approved runtime cadence owner.

## Changes

- Replaced the bootstrap safe-halt MMF dump raw copy with `UnsafeMemoryCopyGuard.TryMemCpy(...)`.
- Passed the exact remaining destination capacity from the MMF tail: `ExportScratchSizeBytes - BootstrapSafeHaltDumpOffsetBytes`.
- Removed the `MainMenuController.Update()` fallback that manually called `Tick(Time.unscaledDeltaTime)` when dispatcher registration was absent.
- `MainMenuController` now depends on its existing `GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI)` path for menu cadence.

This report certifies only the focused unsafe-copy guard and native-loop removal. The working tree already contains broader `MainMenuController` changes; those are not runtime-certified by this report.

## Verification

```text
rg -n "UnsafeUtility\.MemCpy" Assets/_Project/Scripts -g "*.cs"
```

- Result: only `Assets/_Project/Scripts/Core/UnsafeMemoryCopyGuard.cs` contains `UnsafeUtility.MemCpy`.

```text
.\Tools\ReloadAudit\Scan-FoundationGuards.ps1
```

- exit code: `0`
- regenerated: `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md`
- `UnsafeUtility.MemCpy outside guard`: `0`
- `Unauthorized Unity loop methods`: `0`
- `.Run(` sites: `0`
- hot-path `.Run(` review sites: `0`
- `.Complete(` text hits: `5`
- guarded dispatcher completion sites: `1`
- runtime Find API review hits outside Editor folder: `8`

```text
dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal
```

- result: `Build succeeded`
- summary: `0 Warning(s)`, `0 Error(s)`
- elapsed: `00:00:39.84`

```text
dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal
```

- result: `Build succeeded`
- summary: `0 Warning(s)`, `0 Error(s)`
- elapsed: `00:00:44.75`

Unity MCP readback after `refresh_unity(mode=if_dirty, scope=scripts, compile=request, wait_for_ready=true)`:

- editor state: ready for tools
- active scene: `Assets/_Project/Scenes/00_BOOTSTRAP.unity`
- Play Mode: not playing, not paused, not changing
- compilation: not compiling, no domain reload pending
- console errors: `0`
- console warnings: `0`

This is editor compile/console readback. It is not a gameplay route, player-build, profiler, or GC proof.

## 2026-05-04 Current Recheck

The current workspace was rechecked after a later generated guard report showed an unauthorized `MainMenuController.Update()` failure at an older source location. Current source readback shows no `MainMenuController.Update()` method; `OnLocalizationLanguageChanged` now occupies the former line range, and the rerun guard is clean.

- `.\Tools\ReloadAudit\Scan-FoundationGuards.ps1`: exit code `0`; regenerated `Docs/Reports/2026-05-03_FOUNDATION_GUARD_SCAN.md` at `2026-05-04 23:33:55`.
- Current guard inventory: `.Run(` sites `0`, hot-path `.Run(` review sites `0`, `.Complete(` text hits `5`, guarded dispatcher completion sites `1`, `UnsafeUtility.MemCpy outside guard` `0`, unauthorized Unity loop methods `0`, runtime Find API review hits `8`.
- `dotnet build .\Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:14.47`.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:minimal -clp:Summary`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`, elapsed `00:00:15.53`.
- Unity MCP current editor readback: Unity `6000.4.1f1`, active scene `Assets/_Project/Scenes/00_BOOTSTRAP.unity`, Play Mode off, compiling false, ready for tools, console error/warning entries `0`.

This recheck is still source/build/editor-console evidence only. It is not Play Mode, menu UX, player-build, profiler, GCMonitor, or memory-retention proof.

## Do Not Claim

- Do not claim Play Mode is stable. No Play Mode route was executed for this repair.
- Do not claim zero GC. No profiler or GCMonitor capture was taken.
- Do not claim the main menu UX was observed. The native `Update()` fallback was removed by source/guard reasoning only.
- Do not claim runtime Find API debt is gone. The current review inventory is `8`.
- Do not claim `.Complete(` review debt is gone. The current review inventory is `5`.

## Regression Risk

WARNING: Directly entering `01_MAIN_MENU` without a live dispatcher can leave menu transition/progress cadence inactive. Current bootstrap contract requires `00_BOOTSTRAP -> 01_MAIN_MENU`, and `MainMenuController` already routes invalid direct entry through `BootstrapRouteEnforcer`; runtime route proof is still absent.

STATUS: PENDING VERIFICATION
