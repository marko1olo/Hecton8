# COMPUTE FILE BURN ATTRIBUTION

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T03:45+04:00
Source: top-30 `.codex` rollout JSONL + `state_5.sqlite`
Method: weighted attribution by `apply_patch` file-target hits inside each thread

## Boundary

This is probabilistic file-level burn attribution.

Formula:
`file_weighted_tokens += thread_tokens * (file_patch_hits_in_thread / all_patch_file_hits_in_thread)`

This does not prove final LOC delta, correctness, or value. It shows where expensive threads visibly attempted to patch files.

## Aggregate

| Metric | Value |
|---|---:|
| Live SQLite all-thread tokens | 44,209,759,762 |
| Top-30 tokens | 9,492,793,103 |
| Unique patch targets in top-30 | 1,647 |
| Code target weighted tokens | 7,411,317,235 |
| Docs target weighted tokens | 1,143,348,625 |
| Asset target weighted tokens | 496,931,949 |
| Other target weighted tokens | 431,588,269 |
| Package target weighted tokens | 9,607,022 |
| Code target share | 78.073% |
| Docs target share | 12.044% |
| Asset target share | 5.235% |
| Other target share | 4.546% |
| Package target share | 0.101% |

Cost proxy uses the project-wide blended price: USD 0.665510 per 1M total tokens. It is not an invoice and not per-thread bill proof.

## Top File Burn Targets

| # | File | Class | Weighted tokens | Cost proxy | Patch hits | Threads | Current LOC | Dirty |
|---:|---|---|---:|---:|---:|---:|---:|---|
| 1 | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | code | 261,401,331 | USD 173.97 | 460 | 13 | 6,894 | No |
| 2 | `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | code | 193,185,153 | USD 128.57 | 324 | 12 | 3,472 | No |
| 3 | `Assets/_Project/Scripts/BaseModule.cs` | code | 176,763,826 | USD 117.64 | 290 | 11 | 5,459 | No |
| 4 | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | code | 158,398,777 | USD 105.42 | 248 | 10 | 6,712 | No |
| 5 | `Assets/_Project/Scripts/HectonPlayerMovement.cs` | code | 130,247,781 | USD 86.68 | 241 | 17 | 13,294 | No |
| 6 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | code | 128,655,987 | USD 85.62 | 160 | 12 | 7,877 | Yes |
| 7 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | code | 117,828,972 | USD 78.42 | 229 | 15 | 6,159 | No |
| 8 | `MASTER_RELEASE_WORK_PLAN.md` | docs | 116,894,969 | USD 77.79 | 207 | 3 | 2,333 | No |
| 9 | `Assets/_Project/Scripts/FaunaDirector.cs` | code | 114,753,410 | USD 76.37 | 186 | 12 | 5,077 | No |
| 10 | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | code | 112,636,093 | USD 74.96 | 196 | 13 | 7,246 | No |
| 11 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | code | 112,408,030 | USD 74.81 | 161 | 16 | 8,690 | No |
| 12 | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` | code | 111,137,400 | USD 73.96 | 149 | 10 | 7,297 | No |
| 13 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | code | 99,877,961 | USD 66.47 | 142 | 15 | 10,461 | No |
| 14 | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` | code | 94,961,546 | USD 63.20 | 148 | 14 | 7,300 | No |
| 15 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | code | 86,311,136 | USD 57.44 | 162 | 11 | 8,201 | No |
| 16 | `BUILD_PLAYTEST_ISSUES.md` | docs | 81,870,163 | USD 54.49 | 155 | 3 | 1,082 | No |
| 17 | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` | code | 78,048,481 | USD 51.94 | 144 | 8 | 11,907 | No |
| 18 | `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs` | code | 77,221,028 | USD 51.39 | 116 | 2 | 2,542 | No |
| 19 | `Assets/_Project/Scripts/PlayerInventory.cs` | code | 75,579,493 | USD 50.30 | 153 | 10 | 5,527 | No |
| 20 | `Assets/_Project/Scripts/Bootstrap/GameBootstrapper.cs` | code | 74,990,973 | USD 49.91 | 106 | 10 | 5,279 | No |
| 21 | `Assets/_Project/Scripts/AcousticZoneController.cs` | code | 70,500,917 | USD 46.92 | 114 | 9 | 3,636 | No |
| 22 | `Assets/_Project/Scripts/Core/SystemDispatcher.cs` | code | 60,992,400 | USD 40.59 | 86 | 14 | 3,570 | No |
| 23 | `VODOROSLI_TRANSFER_LEDGER.md` | docs | 60,799,827 | USD 40.46 | 91 | 1 | n/a | No |
| 24 | `FLORA_TRANSFER_MASTER_STATUS.md` | docs | 59,463,567 | USD 39.57 | 89 | 1 | n/a | No |
| 25 | `Assets/_Project/Art/Shaders/SargassumMicroFaunaBoids.compute` | assets | 55,821,413 | USD 37.15 | 95 | 3 | 2,094 | No |
| 26 | `Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs` | code | 54,087,459 | USD 36.00 | 94 | 7 | 2,431 | No |
| 27 | `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` | code | 52,302,377 | USD 34.81 | 84 | 3 | 4,967 | No |
| 28 | `Docs/Reports/2026-05-03_FOUNDATION_HARDENING_CONTINUATION.md` | docs | 51,741,688 | USD 34.43 | 104 | 5 | 1,479 | No |
| 29 | `Assets/_Project/Scripts/PhysicsApplySystem.cs` | code | 51,705,823 | USD 34.41 | 91 | 12 | 3,340 | No |
| 30 | `Assets/_Project/Scripts/HectonFabricatorUI.cs` | code | 51,479,020 | USD 34.26 | 99 | 9 | 1,731 | No |

## Interpretation

The highest weighted burn is not random. It clusters around fused, shared, or high-complexity surfaces:
- `SargassumMicroFaunaBoids.cs`
- `CrashTelemetryBuffer.cs`
- `BaseModule.cs`
- `FaunaBrain.cs`
- `HectonPlayerMovement.cs`
- `SpatialAudioManager.cs`
- `PersistentWorldRegistry.cs`
- `GlobalRegistry.cs`
- `HectonVoxelEngine.cs`
- `SaveBinaryStorage.cs`

Current live collision remains narrow: only `SpatialAudioManager.cs` is both a high burn target and dirty in the current working tree snapshot.

## Next Gate

Before more feature work lands on the top-10 burn files:
1. Read current diff.
2. Run compile after concurrent agents pause.
3. Bind failures to file and agent.
4. Record whether the file's final state has net compile/quality improvement.
