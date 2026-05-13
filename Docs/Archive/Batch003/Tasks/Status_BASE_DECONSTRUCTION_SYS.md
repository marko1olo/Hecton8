# Status_BASE_DECONSTRUCTION_SYS

Prompt: BASE_DECONSTRUCTION_SYS
Role: HABITAT_ARCHITECT
Domain: HABITAT & VEHICLES
Status: PENDING VERIFICATION - COMPILE BLOCKED BY PROJECT ASSEMBLY GRAPH

## Mandates Selected
- ARCH_Global_Registry_ServiceLocator_DI_Init.txt
- OPT_Zero_GC_Policy_AllocFree_Mandate.txt
- OPT_Native_Memory_Collections_JobSystem_Protocol.txt
- MATH_Coordinate_Precision_AUP_FloatingOrigin.txt
- LOGI_Energy_Networks_Power_Grid_Graph_Flow.txt
- DATA_Inventory_Resources_Items_SOA_Layout.txt
- DATA_Save_Persistence_Binary_Delta_Checksum.txt
- DBG_Telemetry_Crash_Reporting_PostMortem.txt

## Loop 0 - Intake
- [x] Extract prompt | DOD: exact `<AGENT_PROMPT id="BASE_DECONSTRUCTION_SYS">` block extracted via PowerShell regex from CURRENT_BATCH.md | Alternative rejected: MCP/basic read because truncation risk | Estimate: 220 us
- [x] Domain check | DOD: matched task 56 in Echelon 6 HABITAT & VEHICLES from Actual Domains file | Alternative rejected: editing outside habitat without interface reason | Estimate: 90 us
- [x] Read selected mandates | DOD: loaded registry, zero-GC, native jobs, AUP, graph, inventory, save delta, telemetry mandates | Alternative rejected: coding from memory | Estimate: 510 us

## Loop 1 - Purge / Tasks 1-5
- [x] 1. SINGLETON ERADICATION | DOD: added `IHabitatDeconstructionSystem` and registered `ConstructionManager` through `GlobalRegistry`; no `ConstructionManager.Instance` introduced | Rejected: direct singleton lookup from tools | Estimate: 45 us/request
- [x] 2. SIGNAL MIGRATION | DOD: `PlayerBuilder`, `LaserCutter`, and legacy `BaseModule.Deconstruct` emit `DeconstructRequestSignal`; `ConstructionManager` emits `DeconstructResultSignal` | Rejected: direct `.Deconstruct()` tool calls | Estimate: 64 us/request
- [x] 3. ASMDEF ISOLATION | DOD: changes use existing core/contracts/registry interfaces and signal structs; no new asmdef dependency added | Rejected: new domain assembly churn during multi-agent work | Estimate: 0 us/runtime
- [x] 4. DEAD CODE HUNT | DOD: purged player-script `.Deconstruct()` calls and removed legacy active refund/despawn body from `BaseModule.Deconstruct` | Rejected: raw destruction or hidden fallback execution | Estimate: 35 us/recovery
- [x] 5. TARGET VALIDATION | DOD: request AUP is checked for finite 3m proximity and raycast ownership is validated with static `RaycastHit[4]` | Rejected: trusting collider instance IDs only | Estimate: 18-42 us

## Loop 2 - Graph / Tasks 6-7
- [x] 6. ADJACENCY COLLAPSE CHECK | DOD: `HabitatGraphManager` checks room connection dependencies and rejects window/observation modules left unsupported | Rejected: deleting then repairing graph after damage | Estimate: 12-30 us
- [x] 7. GRAPH ISOLATION CHECK | DOD: Burst DFS validates CSR connectivity after simulated node removal with persistent `NativeList` + `NativeParallelHashSet` | Rejected: managed recursive DFS / LINQ graph walk | Estimate: 35-110 us by module count

## Loop 3 - Refund / Tasks 8-9
- [x] 8. INVENTORY REFUND | DOD: reads `ModuleCatalog`/`ModuleMarker`, calculates `Mathf.Max(0, cost.amount) >> 1`, applies inventory refund, emits `ItemAcquiredSignal` | Rejected: `/ 2` and world-drop-first refund | Estimate: 20-80 us by cost count
- [x] 9. FULL FAILSAFE | DOD: preflights grouped refunds with `CanAcceptItemQuantityBatch`; rejects with `HUDNotificationSignal` before mutation | Rejected: partial refund then rollback guesswork | Estimate: 35-120 us by item footprint

## Loop 4 - Execution / Tasks 10-16
- [x] 10. HOLOGRAPHIC GHOST | DOD: target preview toggles through service interface and swaps shared material/optional ghost visual without material allocation | Rejected: per-frame material instantiation | Estimate: 8 us/toggle
- [x] 11. DECONSTRUCTION VFX | DOD: success publishes `DebrisSpawnSignal` with disintegrate kind before pool return | Rejected: instantiated particle prefab in rollback path | Estimate: 4 us
- [x] 12. UNREGISTER PIPELINES | DOD: success calls `UnregisterModule` before pool return, forcing habitat graph refresh and removal signals | Rejected: despawn-first stale graph pointers | Estimate: 45-90 us
- [x] 13. MMF DELTA OVERWRITE | DOD: success emits `ModuleDeconstructSignal` delete marker with AUP/module hash/node id/frame | Rejected: save scan guessing missing pooled object | Estimate: 5 us
- [x] 14. OBJECT POOL RETURN | DOD: `CanDespawnWithoutDestroy` preflight rejects unpooled objects; success uses `pool.Despawn` only | Rejected: `Destroy(gameObject)` fallback | Estimate: 6 us
- [x] 15. WATER DISPLACEMENT (DEAR LIE) | DOD: reset leak/drain/flood visuals and water volume on pool return instead of simulating displacement | Rejected: fluid solve during teardown | Estimate: 80-200 us saved
- [x] 16. POWER RECALCULATION | DOD: deconstruct signal carries force-cold-tick flag for downstream power/logistics invalidation | Rejected: direct cross-domain power graph mutation | Estimate: 0.1 ms avoided

## Loop 5 - AUP / Math LOD / Native
- [x] 17. ORIGIN SHIFT SYNC | DOD: all request/result/delete/VFX positions use `AbsoluteUniversePosition`; runtime validation converts AUP after origin shifts | Rejected: world-space-only module identity | Estimate: 10 us
- [x] 18. MATH LOD | DOD: Low/MX350/Unknown tiers skip DFS and emit skip flag while high tiers run rollback DFS | Rejected: same graph cost on toaster hardware | Estimate: 35-110 us saved low tier
- [x] 19. ZERO-GC DFS | DOD: DFS stack, visited set, result lane, and 300-entry black box are persistent native containers; no per-request managed collection allocation | Rejected: `Stack<T>`, `HashSet<T>`, recursive calls | Estimate: 0 B GC/request

## Verification
- [x] Re-read prompt after tasks | DOD: `Select-String` located `BASE_DECONSTRUCTION_SYS` at CURRENT_BATCH.md lines 644-685 after direct regex failed on extra attributes | Estimate: 210 us
- [x] Verify `Cost >> 1` | DOD: `ConstructionManager` refund preflight and apply paths both use `Mathf.Max(0, cost.amount) >> 1` | Estimate: 20 us
- [x] Compile attempt 1 | BLOCKED BY DEPENDENCY: `dotnet build Hecton8.Core.csproj` fails before this patch on missing generated asmdef references (`Hecton8.Core.Memory`, `Hecton8.Cartography`, `Hecton8.Physics.Determinism`, `IDataVault`, etc.) | Estimate: 53 s
- [x] Compile attempt 2 | BLOCKED BY DEPENDENCY: `dotnet build Assembly-CSharp.csproj` fails on the same `Hecton8.Core.csproj` dependency wall | Estimate: 96 s
- [x] Compile attempt 3 | BLOCKED BY DEPENDENCY: Unity batchmode cannot attach because the project is already open and locked by an interactive Unity process | Estimate: 3 s
- [x] Static source checks | DOD: `git diff --check` passes for touched files; `rg ".Deconstruct("` under Scripts finds only comments, no active tool calls | Estimate: 150 us
- [x] Final report appended | DOD: appended detailed completion report to `Docs/AgentLogs/LOG_BASE_DECONSTRUCTION_SYS.md` with wrong/done/cheats/microseconds sections | Estimate: 340 us

## Follow-Up Hardening Pass
- [x] Native DFS set compatibility | DOD: replaced rollback `NativeHashSet<long>` with project-standard `NativeParallelHashSet<long>` and registered/refreshed/unregistered it through `NativeMemorySentinel` | Rejected: untracked native set ownership | Estimate: 0 B GC/request
- [x] Authoritative transaction ordering | DOD: capture module hash/node id before mutation, unregister graph first, then emit `ModuleDeconstructSignal` delete marker before pooled despawn | Rejected: delete marker before graph removal | Estimate: 5 us
- [x] Honest tool feedback | DOD: Builder/Laser now report recovery queued, not completed, until authoritative rollback accepts through the deconstruction service | Rejected: optimistic completion logs | Estimate: 0 us runtime hot path
- [x] Batch inventory failsafe | DOD: added `PlayerInventory.CanAcceptItemQuantityBatch` and stack-allocated refund groups so mixed refund items are simulated in one shared grid pass | Rejected: independent per-item preflight that can overpromise capacity | Estimate: 0 B GC, 20-120 us cold path
- [x] Pool preflight guard | DOD: `CanDespawnWithoutDestroy` now rejects null pool lookup state instead of risking a null dictionary read | Rejected: assuming service warmup state | Estimate: 1 us
- [x] No dotnet build launched | DOD: followed user instruction; verification used `rg` and `git diff --check` only | Estimate: 0 s build time
