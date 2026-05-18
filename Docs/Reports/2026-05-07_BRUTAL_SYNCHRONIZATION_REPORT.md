<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R22 Static Actuality Boundary

This document is active only where it agrees with `Docs/README.md`, `Docs/DOC_GOVERNANCE.md`, `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`, current source files, and fresh verification artifacts.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, platform run, campaign telemetry, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older `PASS` / `VERIFIED` labels inside this file are subordinate to the current authority spine above.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->
# 2026-05-07 Brutal Synchronization Report
Date: 2026-05-07
Status: PENDING VERIFICATION (BLOCKED BY MCP)
Scope: documentation synchronization pass across active `Docs` and source-backed project counts.

Mandates followed:

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## Hard Boundary

Requested terminal status `ARCHIVE SYNCHRONIZED VERIFIED` is rejected.
`AGENTS.md` requires status to remain `PENDING VERIFICATION` unless user-provided runtime logs prove the state.
This pass changed documentation only. No Unity Play Mode, profiler, console-clean compile, or runtime leak dump is claimed. The later build-master recheck currently reports a Core compile failure.

## Source Count Evidence

Raw count script:

```powershell
$scripts = @(rg --files 'Assets/_Project/Scripts' -g '*.cs')
$projectCs = @(rg --files 'Assets/_Project' -g '*.cs')
$getContentCount = 0
$measureLines = 0
foreach ($p in $scripts) {
  $content = Get-Content -LiteralPath $p
  $getContentCount += $content.Count
  $measureLines += ($content | Measure-Object -Line).Lines
}
$contracts = 'Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs'
$ifaceAll = (rg -n '^\s*public\s+interface\s+' $contracts | Measure-Object).Count
```

Observed values after the final stable file-count recheck:

- `Assets/_Project`: `1233` C# files.
- `Assets/_Project/Scripts`: `1192` C# files.
- `Assets/_Project/Scripts` by `System.IO.StreamReader.ReadLine()`: `667771` lines.
- `Assets/_Project/Scripts` by `Measure-Object -Line`: `573698` lines.
- `GlobalRegistryContracts.cs`: `39` public interfaces.

Boundary: source files were modified during the same May 7 documentation window. File count now reads `1233` / `1192`; latest script line count reads `667771` physical lines and `39` interfaces. This is still a timestamped source snapshot, not runtime or long-window stability proof. Source-count stability remains `PENDING VERIFICATION`.

## Documentation Count Evidence

Raw docs count script:

```powershell
$exclude = @(
  'Docs\_Archive\',
  'Docs\DEPRECATED\',
  'Docs\Reports\DEPRECATED\',
  'Docs\ARCHIVARIUS REPORTS\03_OBSOLETE\'
)
$docsAllMd = @(Get-ChildItem -LiteralPath 'Docs' -Recurse -File -Filter '*.md')
$docsActive = @($docsAllMd | Where-Object {
  $rel = Resolve-Path -LiteralPath $_.FullName -Relative
  $rel = $rel.TrimStart('.\')
  -not ($exclude | Where-Object { $rel.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) })
})
```

Observed after the final May 7 recheck:

- Physical markdown under `Docs`: `437`.
- Active markdown excluding archive/deprecated/obsolete: `224`.
- Active non-report markdown: `163`.
- Active report markdown: `61`.
- Non-meta files under `Docs`: `866`.
- Root markdown files: `5`.
- Root `.txt` files: `0`.
- Root `.log` files: `0`.
- Text/log evidence files under `Docs/DEPRECATED/External_And_Log_Bundles`: `151`.
- Full `Docs/**/*.md` header debt after archive/report metadata normalization: `0`.

## Objective Results

| Objective | Result |
|---|---|
| Active markdown `Date:` headers | Rechecked for `Date: 2026-05-07`; current active misses `0`. |
| Active markdown `Status:` headers | Rechecked for present status headers; current active misses `0`. Today reports containing `timed out` or `ping not answered` are forced to `PENDING VERIFICATION (BLOCKED BY MCP)`. |
| Stale compile claims | No active doc asserts the old absence of `itemGeneticsWords`, `MinimumDensity`, or `MaximumDensity`. Source contains all three symbols. |
| Deprecated flat redirects | No root-level `FLORA_SYSTEM_PLAN.md` or flat `Docs/FLORA_SYSTEM_PLAN.md` remains. Deprecated redirect stub is already under `Docs/DEPRECATED/Root_Redirect_Stubs_2026-05-01/`. Canonical `Docs/Flora_Pipeline/FLORA_SYSTEM_PLAN.md` is retained. |
| Atlas count sync | `PROJECT_ATLAS.md` updated to current physical file/line/interface counts. |
| JSON manifest | `Docs/Reports/2026-05-07_ACTIVE_DOCUMENTATION_MANIFEST.json` generated. |
| Cinematic cheat ledger | `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` added as an active architecture document for source-backed visual-fake decisions. |
| Conceptual system map | DOTS remains `Experimental Seam`; Physics remains `Active/Transitional`. |
| Event flow map | `EVENT_FLOW_MAP.md`, `EVENT_BUS_MAP.md`, and `SYSTEM_INTERCONNECT_MATRIX.md` state the five arteries: Core, Env, Player, Base, AI. |
| UI zero-GC doctrine | `ZERO_GC_UI_PIPELINE.md` now explicitly forbids `.ToString()` helper laundering and requires `Span<char>`/`TryFormat`/`TMP_Text.SetCharArray`. |
| Native collection lifecycle audit | `2026-05-07_NATIVE_COLLECTION_LEAK_AUDIT.md` and `2026-05-07_NATIVE_COLLECTION_LIFECYCLE_AUDIT.md` updated with sentinel usage, static detector review, and disposal-path disposition. Static audit remains `PENDING VERIFICATION`; runtime leak proof absent. |
| `Soon` / `TODO` in architecture docs | No hits in active architecture docs or authority maps after replacing the stale `soon enough` wording in the GlobalRegistry runtime matrix. |
| Cyrillic path/comment sweep | `NAMING_VIOLATIONS.md` remains the active ledger. No mass rename performed. |
| Hallucination check | No report with header `Status: MCP VERIFIED` plus blocked Unity-console text was found. Reports that reject MCP verification remain unchanged. |

## Stale Symbol Evidence

Source scan:

```powershell
rg -n 'itemGeneticsWords|MinimumDensity|MaximumDensity' 'Assets/_Project/Scripts' -g '*.cs'
```

Confirmed source hits:

- `Assets/_Project/Scripts/SaveData.cs`: `public byte[] itemGeneticsWords;`
- `Assets/_Project/Scripts/PlayerInventory.cs`: reads/writes `dto.itemGeneticsWords`.
- `Assets/_Project/Scripts/SaveBinaryPayloadCodec.cs`: serializes/deserializes `itemGeneticsWords`.
- `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs`: exposes `MinimumDensity` and `MaximumDensity`.

## Hallucination Pattern Evidence

Scan:

```powershell
rg -n -i 'MCP VERIFIED|Unity console was blocked|console was blocked' 'Docs/Reports' -g '*.md'
```

Hits found were rejection statements such as `NOT MCP VERIFIED` or "Do not claim MCP verified", not valid `Status: MCP VERIFIED` headers paired with blocked-console text.

## Diff Artifact

Complete docs diff is exported to:

- `CodexArtifacts/2026-05-07_BRUTAL_SYNCHRONIZATION_DOCS_DIFF.patch`

## Build Artifact Boundary

The latest opened build-master artifact `CodexArtifacts/2026-05-07_BUILD_MASTER_CORE_BUILD.log` reports:

- `Build FAILED.`
- `55 Warning(s)`
- `2 Error(s)`
- active blockers: `HectonVoxelEngine.cs(4143,47)` missing `GlobalRegistry.PlayerRigidbody`; `HectonVoxelEngine.cs(4144,62)` missing `GlobalRegistry.PlayerMovement`

Older successful Core logs are scoped evidence for older source states. They are not proof for the current source, and they are not Play Mode, GCMonitor, profiler, scene/prefab, memory-retention, or player-build proof.

## Verdict

Documentation was synchronized against the current static filesystem/source scan.
Runtime correctness remains unproven.

Status: PENDING VERIFICATION (BLOCKED BY MCP)
