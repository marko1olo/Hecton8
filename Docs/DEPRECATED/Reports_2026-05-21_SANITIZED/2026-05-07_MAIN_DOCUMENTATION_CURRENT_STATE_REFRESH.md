<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# Main Documentation Current-State Refresh
Date: 2026-05-07
Status: PENDING VERIFICATION (BLOCKED BY MCP)
Scope: main documentation authority refresh after the final inquisition runtime patch, latest Unity MCP console retry, and build-master Core compile recheck

Mandates followed:

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `AGENTS.md`

## Current Truth Boundary

This report is the latest active documentation synchronization boundary.

It updates the main docs after the final runtime patch that changed `FaunaBrain.UpdateFaunaBiolumPresentation(...)` from `Mathf.Exp(...)` to a bounded rational presentation approximation.

It does not claim Play Mode proof, profiler proof, GCMonitor proof, scene/prefab proof, or player-build proof.

Earlier Unity MCP console readback was blocked:

```text
Unity session not ready for 'read_console' (ping not answered); please retry
```

Current build-master retry after this documentation refresh returned:

```text
Unity session not ready for 'read_console' (ping not answered); please retry
```

This is not console-clean evidence. It is not Play Mode, profiler, GCMonitor, scene/prefab, memory-retention, or player-build proof.

## Source Metrics

Raw script used:

```powershell
$root = Resolve-Path .\Assets\_Project
$scriptRoot = Resolve-Path .\Assets\_Project\Scripts
function Count-Cs($path) {
  $files = Get-ChildItem -Path $path -Recurse -File -Filter *.cs
  $lines = 0
  foreach ($file in $files) {
    $reader = [System.IO.StreamReader]::new($file.FullName)
    try { while ($null -ne $reader.ReadLine()) { $lines++ } }
    finally { $reader.Dispose() }
  }
  [pscustomobject]@{ Path = $path.Path; Files = $files.Count; Lines = $lines }
}
Count-Cs $root
Count-Cs $scriptRoot
```

Observed output:

| Path | Files | Physical lines |
|---|---:|---:|
| `Assets/_Project` | `1233` | `683064` |
| `Assets/_Project/Scripts` | `1192` | `667771` |

Additional orientation counts:

- direct scripts under `Assets/_Project/Scripts`: `337`
- `GlobalRegistryContracts.cs` direct public interfaces: `39`
- `Docs/**/*.md`: `443`
- active markdown excluding deprecated/archive/obsolete: `230`
- active non-report markdown: `163`
- direct `Docs/Reports/*.md`: `67`
- active JSON files excluding deprecated/archive/obsolete: `13`

## Documentation Files Updated

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ROOT_DOCS_REFERENCE.md`
- `Docs/QUALITY_GATES.md`
- `Docs/SYSTEMS_CONTRACTS.md`
- `Docs/HECTON8_GLOBAL_ARCHITECTURE_MAP.md`
- `Docs/ARCHIVARIUS REPORTS/01_GENERAL_INFO/PROJECT_ATLAS.md`
- `Docs/ARCHITECTURE/ZERO_GC_UI_PIPELINE.md`
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md`
- `Docs/Reports/README.md`
- `Docs/Reports/2026-05-07_PROJECT_ATLAS_SYNCHRONIZATION_PASS.md`
- `Docs/Reports/2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json`

## Hallucination Check

Current header scan found no active main/report document with:

```text
Header status equivalent to MCP console proof
Header status equivalent to OMEGA proof
Header status equivalent to ARCHIVE SYNCHRONIZED proof
```

Blocked-runtime reports must keep `PENDING VERIFICATION` or `PENDING FINAL UNITY PROOF`.

## Runtime Patch Reflected In Docs

`Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` now records:

- base-airlock pressure whistle as an audio presentation fake
- player slide-blocked speed scalar as squared-math telemetry simplification
- fauna biolum fade approximation as a visual-only `Mathf.Exp` replacement

These are source-present entries only. They still require profiler/runtime proof.

Source-count note:

- Source lines changed during this documentation refresh window while file counts remained stable.
- The counters above match the latest completed `System.IO.StreamReader.ReadLine()` pass before this report was closed.
- Treat earlier same-day line counts as superseded.

## Build-Master Compile Evidence

Latest build-master Core artifact:

- `CodexArtifacts/2026-05-07_BUILD_MASTER_CORE_BUILD.log`

Raw result:

```text
C:\hades\Hecton8\Assets\_Project\Scripts\HectonVoxelEngine.cs(4143,47): error CS0117: 'GlobalRegistry' does not contain a definition for 'PlayerRigidbody'
C:\hades\Hecton8\Assets\_Project\Scripts\HectonVoxelEngine.cs(4144,62): error CS0117: 'GlobalRegistry' does not contain a definition for 'PlayerMovement'

Build FAILED.
    55 Warning(s)
    2 Error(s)

Time Elapsed 00:02:41.78
```

Boundary:

- compile evidence is `Hecton8.Core.csproj` only
- command used `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /nr:false /p:UseSharedCompilation=false`
- previous no-dependencies and scoped successful build artifacts are superseded for whole Core compile state by this full dependency-graph result
- Unity console readback must not be used to override this local compile failure unless a newer Unity compile log proves the same source clean
- runtime behavior remains unproven until Play Mode/profiler/GCMonitor evidence exists

## Status Rule

Do not upgrade any active documentation status above `PENDING VERIFICATION` from this report.

Status: PENDING VERIFICATION (BLOCKED BY MCP)
