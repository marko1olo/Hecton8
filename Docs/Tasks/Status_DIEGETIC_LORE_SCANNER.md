# Status - DIEGETIC_LORE_SCANNER

Status: PENDING VERIFICATION
Domain: ECHELON 8 - PRESENTATION & UX
Prompt: Spatial Hashing Scanner UI
Task count: 15

Mandates read:
- UI_Data_Streaming_ZeroGC_Optimization.txt
- UI_Diegetic_Physical_Interfaces.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt
- OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Loop 0 - Intake

- [x] Extract XML prompt cover-to-cover | DOD: PowerShell raw regex extraction from Docs/Tasks/CURRENT_BATCH.md, not MCP truncation | Rejected: neighboring prompt context | Estimate: 1200 us
- [x] Read domain map and selected mandates | DOD: stable docs and task-matched mandate files read before code edits | Rejected: coding from task title only | Estimate: 9000 us
- [x] Task 1 - Singleton eradication: purge ScannerManager.Instance | DOD: `rg` scan found no `ScannerManager` or `.Instance` references in project scripts; no singleton dependency introduced | Rejected: creating a replacement manager singleton | Estimate: 1800 us
- [x] Task 2 - Signal migration: emit LoreFragmentScannedSignal(Hash) | DOD: added fixed-size `LoreFragmentScannedSignal` and publish path in archaeology completion | Rejected: chat/report-only event and managed C# events | Estimate: 3200 us
- [x] Task 3 - ASMDEF isolation: Hecton8.Tools.Scanner -> Contracts | DOD: created `Hecton8.Tools.Scanner.Contracts` asmdef and scanner lore title read model contract; implementation remains in existing monolithic assembly until Unity assembly regeneration | Rejected: moving `ScannerTool.cs` across assemblies mid-batch | Estimate: 4100 us
- [x] Task 4 - S.O.A. lore nodes from GlobalDataVault | DOD: `ScannableTarget` mirrors lore target AUPs and hashes into `BufferID.LoreEntityAUPs` / `LoreEntityHashes` DataVault buffers | Rejected: managed per-frame Physics query as target registry | Estimate: 7600 us
- [x] Task 5 - Frustum dot product in FastTick | DOD: scanner registers `IFastTickable` and runs a Burst `LoreCandidateDotProductJob` over DataVault SOA using camera AUP-relative vectors | Rejected: Unity `Update()` and direct forward `Physics.Raycast` | Estimate: 9400 us
- [x] Task 6 - Auto-aim fake by highest dot <15m | DOD: Burst candidate job chooses highest dot over lore nodes and clamps search to <=15m | Rejected: pixel-perfect raycast and screen-space collider picking | Estimate: 5200 us
- [x] Task 7 - One RaycastCommand occlusion check | DOD: selected lore node queues one dispatcher `RaycastCommand` toward the candidate and rejects earlier non-target obstruction | Rejected: raycast fan, per-frame raycast, and direct `Physics.Raycast` | Estimate: 4700 us
- [x] Task 8 - Progress accumulator while trigger held | DOD: active lore entity hash accumulates `_activeScientificEntityProgress` from held trigger delta and commits through archaeology runtime | Rejected: coroutine progress and managed event loops | Estimate: 2400 us
- [x] Task 9 - Span scrambling display on scanner RT | DOD: `ToolDiegeticDisplayController` writes scanner target text into the 256 RT TMP buffers via `Span<char>`/`SetCharArray`; active scanner summary also uses stackalloc span | Rejected: `.text` string assignment and heap-built decryption strings | Estimate: 8200 us
- [x] Task 10 - Unlock commit + Meta Campaign DAG | DOD: completion publishes `LoreFragmentScannedSignal`, `ScanCompleteSignal`, `BlueprintUnlockedSignal`, `HUDNotificationSignal`, and `ProgressionEventSignal` for MetaCampaign DAG consumption | Rejected: direct service call to campaign singleton | Estimate: 5100 us
- [ ] Task 11 - AUP shift safety | Justification pending | Alternatives pending | Estimate pending
- [ ] Task 12 - Math LOD Low Tier disables scrambling | Justification pending | Alternatives pending | Estimate pending
- [ ] Task 13 - Execution phase split: SIMULATION / VISUAL_SYNC | Justification pending | Alternatives pending | Estimate pending
- [ ] Task 14 - Zero-GC stringless Burst spatial loop | Justification pending | Alternatives pending | Estimate pending
- [ ] Task 15 - Omega compile check: Span<char> no boxing | Justification pending | Alternatives pending | Estimate pending

## Verification

- [ ] Compile/source validation - BLOCKED BY DEPENDENCY: generated `Hecton8.Core.csproj` fails on stale missing assemblies unrelated to scanner; Unity refresh timed out and console session is unavailable
- [ ] Console check - BLOCKED BY UNITY SESSION: MCP reports `no_unity_session` after refresh timeout
- [ ] Re-read prompt after core tasks
- [ ] Omega polish mandate after all tasks done or blocked
