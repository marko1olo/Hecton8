# 28 Stale Error Purge And Trust Sync

Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `PROJECT_LTS_Compatibility_Layer.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`

Scope:
- Purge stale compile-error truth around inventory genetics and resource density symbols.
- Synchronize Archivarius docset counts.
- Extend verification trust model evidence around smoke testers, verifier scripts, and crash telemetry.
- Runtime gameplay code remains read-only.

## 1. Stale Error Purge

The following old compile-blocker claims are no longer current truth:
- `InventoryDTO.itemGeneticsWords` missing.
- `MinimumDensity` missing.
- `MaximumDensity` missing.

Current source evidence:

| Symbol | Current proof |
|---|---|
| `dto.itemGeneticsWords` write path | `Assets/_Project/Scripts/PlayerInventory.cs:989` writes `_itemGeneticsWords[anchorIndex]` into `dto.itemGeneticsWords[cellIndex]` |
| `itemGeneticsWords` save backing field | `Assets/_Project/Scripts/SaveData.cs:373` defines `public uint[] itemGeneticsWords;` |
| `MinimumDensity` / `MaximumDensity` fields | `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs:123-124` define both fields |
| density assignment path | `Assets/_Project/Scripts/Scavenging/ResourceNodeTemplate.cs:549-550` assigns clamped density values |

Doc search result:
- Current forensic files `07` and `25` already state these symbols now exist.
- Regex search found no separate active markdown report still presenting these symbol-missing claims as current blockers.
- If an older dated report implies otherwise, this file supersedes it.

Hard rule:
- These symbol-missing claims are historical only.
- They must not be used as current compile blockers without a fresh Unity compile log proving regression.

## 2. Docset Count Synchronization

Filesystem snapshot: 2026-04-30.

| Bucket | Current count |
|---|---:|
| `01_GENERAL_INFO` markdown files | 22 |
| `02_ACTUAL_REPORTS` markdown files | 46 |
| `02_ACTUAL_REPORTS` CSV datasets | 1 |
| `02_ACTUAL_REPORTS` patch artifacts | 8 |
| indexed docs/datasets in `01` + `02` | 69 |
| physical non-meta files in `01` + `02` | 77 |

Correction:
- Requested `19` / `43` totals conflict with the current filesystem.
- Current true counts are `22` / `46`.
- `19` / `43` should be treated as stale unless files are deliberately moved or deleted in a separate cleanup.

Coverage matrix synchronization:
- `PLAYER_GAMEPLAY_CORE_MAP.md` is present in `01_GENERAL_INFO` and is already a primary player/gameplay read path.
- `CONSTRUCTION_RUNTIME_INTEGRATION_MAP.md` is present in `01_GENERAL_INFO` and is already a primary construction/runtime read path.
- `2026-04-29_SAVE_LOAD_RUNTIME_TRUTH.md` is present in `02_ACTUAL_REPORTS` and is already a primary save/load truth source.

## 3. Verification Trust Model Expansion

Formal tests:
- `Assets/_Project/Tests` contains 4 C# test files.
- Formal automated test coverage remains low.

Runtime smoke testers:
- `BarterRuntimeSmokeTester`
- `BuilderRuntimeSmokeTester`
- `FabricationRuntimeSmokeTester`
- `FieldToolRuntimeSmokeTester`
- `SaveSystemRuntimeSmokeTester`
- `ScanRuntimeSmokeTester`
- `ToolRuntimeSmokeTester`
- `ToolTrialRangeRuntimeSmokeTester`
- `UIRuntimeSmokeTester`
- `WorldGenerativeGeologyRuntimeSmokeTester`
- `ShellVerificationRuntimeSmokeTester`
- `WeakToolsRuntimeSmokeTester`

Verifier scripts:
- `StateRecoveryVerifier`
- `SceneTransitionVerifier`
- `PauseSystemVerifier`
- `PhysicalInteractionRuntimeVerifier`
- `MantaAcousticRuntimeVerifier`

Crash telemetry:
- `CrashTelemetryBuffer` is a real runtime owner implementing `ITickable`, `IUpdatable`, and `IFixedTickable`.
- It owns persistent `NativeArray<DebugLogEntry>` ring/export buffers and fixed binary export scratch.
- It supports NaN physics recovery reporting through `ReportNanPhysicsRecovery()`.

Verdict:
- Formal automated test maturity: low.
- Runtime smoke/verifier coverage maturity: medium-high.
- Telemetry/post-mortem maturity: high at source level.
- Overall verification maturity: medium, because runtime smoke and telemetry do not replace deterministic automated regression tests.

## 4. MCP Console Status

MCP console read:
- Tool: `read_console`
- Request: `types=["error"]`, `count=20`, `include_stacktrace=true`
- First attempt: failed because Unity session was not ready and ping was not answered.
- Retry result: `success=true`, message `Retrieved 0 log entries.`, data `[]`.

Therefore:
- Current MCP console error slice contains `0` error entries.
- This is console-readback proof only, not play-mode traversal, not build proof, and not profiler proof.
- Documentation edits do not compile into player scripts.

## 5. Regression Model

CPU:
- No runtime code changed.

GC:
- No runtime code changed.

Memory:
- No runtime code changed.

Cadence:
- Documentation truth layer improved; runtime cadence unchanged.

Correctness:
- Stale symbol-missing claims are explicitly invalidated.
- Folder counts are synchronized to the current filesystem instead of copied from stale prompt values.

## 6. Hard Conclusion

The stale compile-error claims about `itemGeneticsWords`, `MinimumDensity`, and `MaximumDensity` are purged from current truth.

The current docset counts are `22` and `46`, not `19` and `43`.

Verification posture remains mixed:
- formal tests are low volume
- runtime smoke/verifier layer is non-trivial
- crash telemetry is real
- current MCP console error slice returned `0` entries, but play-mode/build/profiler verification remains absent
