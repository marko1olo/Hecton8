# COMPUTE THREAD ATTRIBUTION

Status: AUDIT COMPLETE
Snapshot: 2026-05-15T03:21:51+04:00
Source: `.codex/state_5.sqlite` + top-30 rollout JSONL files
Method: read-only parse of `custom_tool_call` / `function_call` payloads

## Boundary

This file attributes visible work traces. It does not prove final committed value.

Evidence strength:
- Strong: thread id, token count, model, rollout path, `apply_patch` file targets.
- Medium: patch churn lines, shell/build/test command mentions.
- Weak: title text and raw file mention frequency.
- Not proven: final LOC delta, compile result after the thread, H-Phi delta, actual user value.

Valid label remains `HIGH-BURN CANDIDATE` until a thread joins to final diff, meaningful LOC delta, compile/test result, and quality delta.

## Top-30 Aggregate

| Metric | Value |
|---|---:|
| Live SQLite all-thread tokens | 43,998,578,833 |
| Top-30 tokens | 9,492,793,103 |
| Top-30 share | 21.575% |
| Top-30 rollout events parsed | 490,220 |
| `apply_patch` calls | 14,015 |
| `shell_command` calls | 86,616 |
| Unique patch file targets | 1,647 |
| Patch churn added lines | 354,203 |
| Patch churn removed lines | 75,895 |
| Code patch target hits | 12,594 |
| Docs patch target hits | 2,187 |
| Asset patch target hits | 804 |
| Package patch target hits | 14 |
| Other patch target hits | 1,170 |

Patch churn is summed from JSONL patch payloads. It includes retries and superseded edits. It is not final repository diff.

## Tool Shape

| Tool | Calls in top-30 |
|---|---:|
| `shell_command` | 86,616 |
| `apply_patch` | 14,015 |
| `read_console` | 2,540 |
| `update_plan` | 1,532 |
| `read_mcp_resource` | 1,366 |
| `validate_script` | 1,337 |
| `refresh_unity` | 1,209 |
| `mcp__unityMCP__read_console` | 714 |
| `mcp__unityMCP__refresh_unity` | 518 |
| `execute_code` | 487 |

The expensive threads were not chat-only. They were heavy tool loops with large patch churn.

## Hot Patch Targets

| Rank | Patch target | Hits | Class |
|---:|---|---:|---|
| 1 | `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` | 460 | code |
| 2 | `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` | 324 | code |
| 3 | `Assets/_Project/Scripts/BaseModule.cs` | 290 | code |
| 4 | `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` | 248 | code |
| 5 | `Assets/_Project/Scripts/HectonPlayerMovement.cs` | 241 | code |
| 6 | `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` | 229 | code |
| 7 | `MASTER_RELEASE_WORK_PLAN.md` | 207 | docs |
| 8 | `Assets/_Project/Scripts/Core/GlobalRegistry.cs` | 196 | code |
| 9 | `Assets/_Project/Scripts/FaunaDirector.cs` | 186 | code |
| 10 | `Assets/_Project/Scripts/SaveBinaryStorage.cs` | 162 | code |
| 11 | `Assets/_Project/Scripts/HectonVoxelEngine.cs` | 161 | code |
| 12 | `Assets/_Project/Scripts/SpatialAudioManager.cs` | 160 | code |
| 13 | `BUILD_PLAYTEST_ISSUES.md` | 155 | docs |
| 14 | `Assets/_Project/Scripts/PlayerInventory.cs` | 153 | code |
| 15 | `Assets/_Project/Scripts/HectonUnderwaterVisuals.cs` | 149 | code |
| 16 | `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` | 148 | code |
| 17 | `Assets/_Project/Scripts/WorldProceduralScatterDirector.cs` | 144 | code |
| 18 | `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` | 142 | code |
| 19 | `Assets/_Project/Scripts/Editor/WorldProceduralSeaweedMeshBuilder.cs` | 116 | code |
| 20 | `Assets/_Project/Scripts/AcousticZoneController.cs` | 114 | code |

The hot file set overlaps the already identified fused-risk files. `HectonPlayerMovement.cs`, `WorldProceduralScatterDirector.cs`, `BaseModule.cs`, `GlobalRegistry.cs`, and `SaveBinaryStorage.cs` are not normal patch surfaces. They are repeated collision zones.

## Top-30 Attribution Rows

Column format:
`code/docs/assets/other` = patch target hit classes.
`build/test/diff/unity` = shell command mention counts.

| # | Thread | Tokens | Patches | Files | Classes | Churn | Shell | Build/test/diff/unity | Top patch targets |
|---:|---|---:|---:|---:|---|---:|---:|---|---|
| 1 | `019e1859-0e01-77b2-a8c6-b5586ccc5c8c` | 518,697,166 | 399 | 214 | 438/64/81/0 | +5,830/-2,128 | 4,482 | 853/207/32/797 | `HectonUnderwaterVisuals`, `HectonCelestial`, `LOG_UI_DIEGETIC_INPUT` |
| 2 | `019d6329-de82-74e2-83ca-450539a61cec` | 490,407,394 | 561 | 55 | 399/322/13/0 | +21,598/-2,520 | 2,382 | 530/127/2/173 | `WorldProceduralSeaweedMeshBuilder`, `VODOROSLI_TRANSFER_LEDGER`, `FLORA_TRANSFER_MASTER_STATUS` |
| 3 | `019dde7c-df90-7791-b4b4-d49c8450a9be` | 468,267,072 | 889 | 282 | 990/3/3/2 | +13,276/-3,721 | 3,947 | 656/173/98/362 | `GlobalRegistryContracts`, `GlobalRegistry`, `HectonPlayerMotor` |
| 4 | `019d67a6-6823-7b82-94f9-a3167b8e0286` | 429,064,399 | 733 | 89 | 486/309/6/13 | +15,703/-4,225 | 2,927 | 648/403/120/190 | `BUILD_PLAYTEST_ISSUES`, `MASTER_RELEASE_WORK_PLAN`, `WorldProceduralScatterDirector` |
| 5 | `019dcf19-407b-75f2-99e4-54d0217d9d14` | 408,633,638 | 1,010 | 215 | 17/98/0/1046 | +55,123/-2,949 | 2,627 | 291/17/0/101 | external `C:/dinaz/Revival-Project` paths dominate |
| 6 | `019dfc26-b869-7bf3-a254-de3f0a8111e9` | 349,084,791 | 489 | 50 | 475/16/3/3 | +9,749/-3,497 | 3,774 | 273/455/111/172 | `HectonVoxelEngine`, `Hazard`, `HectonAbyssalBasin` |
| 7 | `019def23-b6e4-7d72-9992-a10a17f0d7db` | 340,869,732 | 287 | 83 | 276/0/21/1 | +7,274/-1,329 | 3,254 | 968/289/9/777 | `SystemDiagnostics`, `FaunaSensor`, `GameBootstrapper` |
| 8 | `019dfd9c-337f-7842-81b5-e4b862462b87` | 333,924,928 | 500 | 82 | 494/24/0/0 | +6,839/-3,398 | 3,444 | 182/174/85/96 | `AudioLog`, `PDAE`, `Emergency` |
| 9 | `019dda15-a011-7a12-a62c-1bc748a269a3` | 310,515,372 | 357 | 144 | 329/32/66/2 | +9,355/-1,547 | 3,276 | 608/203/82/436 | `Construction`, `Resource`, `HectonWorldGenerator` |
| 10 | `019dda14-db04-74b0-91a0-e1088c40bc88` | 308,909,822 | 426 | 211 | 493/84/28/25 | +9,781/-1,596 | 2,826 | 377/73/60/110 | `Flora`, `Destruct`, `PersistentWorldRegistry` |
| 11 | `019dfd94-84ae-7b53-b689-cc893af60675` | 306,217,896 | 514 | 92 | 508/31/3/0 | +8,004/-3,200 | 3,597 | 519/125/127/147 | `HectonFabricator`, `PlayerInventory`, `LaserCutter` |
| 12 | `019dfd9d-e339-7601-9dca-e945563ed5ff` | 306,165,757 | 483 | 47 | 469/15/0/0 | +7,921/-2,248 | 3,288 | 225/16/127/104 | `FaunaBrain`, `HectonDirector`, `Sargassum` |
| 13 | `019def93-fcb8-7960-8196-412d6f9ef869` | 296,179,586 | 347 | 95 | 239/0/121/8 | +6,082/-1,583 | 2,918 | 492/286/45/318 | `HectonUnderwaterVisuals`, shaders, `HectonAtmosphere` |
| 14 | `019dd8e6-6149-7bb3-8900-cc0f69f9b12f` | 292,161,127 | 372 | 201 | 124/535/11/8 | +19,200/-2,697 | 3,236 | 337/163/83/239 | `PROJECT_ATLAS`, `FOUNDATION_HARDENING`, docs-heavy |
| 15 | `019dfc29-331e-7c21-b2ba-b6af81f9445d` | 290,450,970 | 432 | 28 | 342/34/65/0 | +4,588/-1,817 | 3,146 | 214/40/124/121 | `SargassumMicroFaunaBoids`, compute shader, `FaunaBrain` |
| 16 | `019dfd93-edaf-78d1-a75b-2786eb254071` | 288,545,985 | 333 | 40 | 338/0/0/0 | +3,687/-3,801 | 2,917 | 366/126/60/245 | `SpatialAudioManager`, `PlayerCriticalProceduralAudioRenderer`, `HectonVoxelEngine` |
| 17 | `019dfe4e-0a73-7eb1-a5ba-d201ae041c1c` | 283,175,153 | 505 | 113 | 552/0/0/2 | +7,042/-2,875 | 2,783 | 186/109/53/60 | `GlobalRegistry`, `GameBootstrapper`, `Input` |
| 18 | `019ddea2-fe00-7c62-b0c3-25b81e28794c` | 282,042,919 | 328 | 146 | 381/1/6/0 | +6,507/-1,598 | 2,393 | 485/84/8/352 | `HectonS`, `GameBootstrapper`, `PlayerPDA` |
| 19 | `019dcffb-783a-7690-b720-8ac0ceb29c3b` | 277,725,417 | 442 | 91 | 490/12/4/0 | +13,924/-1,951 | 2,332 | 159/38/12/110 | `HectonPlayerMovement`, `PhysicsApplySystem`, `SubmarineStructure` |
| 20 | `019d925a-0bf6-7c02-832f-bd2ef5cf13ca` | 275,724,279 | 367 | 79 | 340/37/4/1 | +10,356/-1,889 | 1,850 | 227/32/0/215 | `FaunaDirector`, `AcousticZoneController`, Subnautica gap audit docs |
| 21 | `019dfd50-3d75-7c21-a49c-d1369048a927` | 274,451,591 | 490 | 115 | 242/299/0/9 | +5,923/-3,864 | 2,965 | 891/226/32/106 | documentation reports and atlas docs |
| 22 | `019dda12-f963-7133-9e7e-65774a6601c2` | 274,181,378 | 377 | 271 | 598/24/53/25 | +9,942/-1,969 | 2,823 | 498/65/33/254 | `PlayerInventory`, `Fabricator`, `HectonWorldGenerator` |
| 23 | `019dfd9d-3d69-75b1-86ac-d6837fbe922c` | 272,789,160 | 381 | 61 | 335/35/26/3 | +5,739/-2,115 | 3,030 | 201/47/78/112 | `SuitHUDV4CanvasOverlay`, `VisorHUD`, `FakeRadar` |
| 24 | `019d9259-d4c0-7751-b30a-ba423b90929e` | 269,031,291 | 444 | 130 | 462/0/110/18 | +21,807/-2,929 | 2,141 | 172/18/0/101 | `Localization`, `PDADataLog`, UI/localization |
| 25 | `019dda14-b01c-7423-a02d-d7cd84914afb` | 262,370,401 | 438 | 75 | 434/18/3/0 | +10,251/-1,014 | 2,173 | 294/62/59/192 | `BaseModule`, construction systems |
| 26 | `019dd8d8-8d18-7fd2-8336-334fd3be0e14` | 261,225,332 | 304 | 399 | 615/133/45/0 | +6,236/-3,349 | 2,942 | 389/99/70/338 | `PDAInventory`, `HectonWorldGenerator`, project status docs |
| 27 | `019dabac-3c47-74f2-8150-6da883dd6b88` | 259,318,739 | 523 | 65 | 438/27/105/0 | +18,463/-1,085 | 1,580 | 115/17/25/50 | `SargassumMicroFaunaBoids`, `SargassumGlobalDragManager`, `AbyssalThermal` |
| 28 | `019def94-ec22-78d1-b0e3-1b61c192a31a` | 257,037,410 | 389 | 40 | 371/0/18/3 | +7,484/-2,944 | 2,444 | 436/297/39/143 | `BaseModule`, logistics power, construction |
| 29 | `019dfe4f-4f6f-7e40-8126-43f1b5a93a20` | 253,340,615 | 426 | 42 | 402/27/2/1 | +8,940/-1,700 | 3,026 | 289/30/113/109 | `CrashTelemetryBuffer`, `PhysicsApplySystem`, black box editor tools |
| 30 | `019dcffb-ddcd-78e1-8504-eef0baa9a02d` | 252,283,783 | 469 | 92 | 517/7/7/0 | +17,579/-4,357 | 2,093 | 137/90/8/73 | `PersistentWorldRegistry`, `SaveBinaryStorage`, `SaveManager` |

## Immediate Audit Queue

Do not inspect by rank alone. Inspect by collision risk:

1. `Assets/_Project/Scripts/World/SargassumMicroFaunaBoids.cs` - 460 patch hits.
2. `Assets/_Project/Scripts/CrashTelemetryBuffer.cs` - 324 patch hits.
3. `Assets/_Project/Scripts/BaseModule.cs` - 290 patch hits.
4. `Assets/_Project/Scripts/Fauna/FaunaBrain.cs` - 248 patch hits.
5. `Assets/_Project/Scripts/HectonPlayerMovement.cs` - 241 patch hits.
6. `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs` - 229 patch hits.
7. `Assets/_Project/Scripts/Core/GlobalRegistry.cs` - 196 patch hits.
8. `Assets/_Project/Scripts/SaveBinaryStorage.cs` - 162 patch hits.
9. `Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs` - 148 patch hits.
10. `Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs` - 142 patch hits.

These files deserve compile/history review before any more feature work lands on them.
