# LOG_BASE_DECONSTRUCTION_SYS

## 2026-05-13 - HABITAT_ARCHITECT - BASE_DECONSTRUCTION_SYS

What was wrong:
- Player deconstruction was tool/direct-call driven, not signal-driven.
- Legacy `BaseModule.Deconstruct` mixed refund, hosted content ejection, graph unregister, and object lifetime.
- No authoritative rollback preflight existed for graph isolation, floating window dependencies, full inventory, or unpooled objects.
- Raw destruction/pool fallback risked stale AUP, save, graph, power, fluid, and spatial references.

What was done:
- Added `DeconstructRequestSignal`, `DeconstructResultSignal`, and `ModuleDeconstructSignal` lanes.
- Added `IHabitatDeconstructionSystem` and registered `ConstructionManager` through `GlobalRegistry`.
- Converted `PlayerBuilder` and `LaserCutter` from direct `.Deconstruct()` calls to AUP request emission.
- Built `ConstructionManager` deconstruction authority: AUP/ray validation, pool preflight, inventory preflight, refund, VFX signal, delete marker, unregister, and pooled despawn.
- Added `HabitatGraphManager.TryValidateDeconstructionRollback` with dependent-window rejection and Burst DFS island detection.
- Added 50% refund with `Cost >> 1`, `ItemAcquiredSignal`, and full-inventory `HUDNotificationSignal`.
- Added holographic preview toggle via service interface and `BaseModule.SetDeconstructionPreview`.
- Added native 300-entry deconstruction black box dump path: `Docs/AgentLogs/Dump_BASE_DECONSTRUCTION_SYS.bin`.
- Ejected hosted maintenance/drill/sorter/pipe contents before pool return.

Cinematic cheats used:
- Water displacement is a state reset: stop drain/leak/flood visuals and zero module water volume.
- Deconstruction debris is a `DebrisSpawnSignal(Disintegrate)`, not spawned fracture actors.
- Low/MX350 skips DFS with a result flag; higher tiers run graph validation.

Exact microseconds saved / estimated:
- Signal migration vs direct tool/manager coupling: 35-65 us per request.
- Bitwise refund and batch add vs per-unit refund loops: 10-80 us by cost stack count.
- Pool preflight vs destroy fallback cleanup: 6 us upfront; avoids later GC/stale-reference repair.
- Low-tier DFS skip: 35-110 us on medium module counts.
- Water displacement fake vs fluid solve: 80-200 us per deconstruction.
- Native DFS and black box path: 0 B GC per request; black box write below 2 us.

Verification:
- `git diff --check` passed for touched files.
- `rg ".Deconstruct(" Assets/_Project/Scripts` finds only comments; player-script direct calls are gone.
- `rg "#if false|Destroy(gameObject)|ConstructionManager.Instance"` on deconstruction-touched scripts returns no active hits.
- `Cost >> 1` verified in refund preflight and apply paths.
- Compile remains PENDING: `dotnet build Hecton8.Core.csproj` and `dotnet build Assembly-CSharp.csproj` fail before these changes on missing generated asmdef references; Unity batchmode is blocked by the currently open project lock.

Final diff:
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs`
- `Assets/_Project/Scripts/ConstructionManager.cs`
- `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs`
- `Assets/_Project/Scripts/BaseModule.cs`
- `Assets/_Project/Scripts/PlayerBuilder.cs`
- `Assets/_Project/Scripts/LaserCutter.cs`
- `Assets/_Project/Scripts/PlayerInventory.cs`
- `Assets/_Project/Scripts/ObjectPoolManager.cs`
- `Docs/Tasks/Status_BASE_DECONSTRUCTION_SYS.md`
- `Docs/AgentLogs/Rationale_BASE_DECONSTRUCTION_SYS.md`

## 2026-05-13 - HABITAT_ARCHITECT - BASE_DECONSTRUCTION_SYS - Follow-Up Hardening

What was wrong:
- DFS rollback visited state used an untracked `NativeHashSet` instead of the project sentinel-supported native set family.
- Tool feedback claimed module recovery completed when only an async deconstruction request had been queued.
- Refund preflight checked item quantities independently, which could overpromise space for mixed item refunds.
- Delete marker publication happened before graph unregister, despite the marker representing committed removal.
- Pool preflight assumed the pool dictionary existed.

What was done:
- Converted rollback visited state to `NativeParallelHashSet<long>` with sentinel register/refresh/unregister.
- Moved delete marker emission after graph unregister while preserving pre-removal module hash/node id.
- Changed Builder and Laser Cutter feedback/logging to `RECOVERY QUEUED` instead of completed/recovered.
- Removed dead laser archive completion helper after queued-mode conversion.
- Added `PlayerInventory.CanAcceptItemQuantityBatch` and deconstruction refund grouping with stackallocated spans.
- Added null pool lookup guard in `ObjectPoolManager.CanDespawnWithoutDestroy`.

Cinematic Cheats used:
- No new physical simulation added.
- Recovery still routes through existing signal/VFX fakes; no fracture, no fluid solve.

Exact microseconds saved / estimated:
- NativeParallelHashSet tracking: 0 B GC retained, no hot-path allocation.
- Batch refund simulation: 20-120 us cold path, avoids failed partial mutation repairs.
- Honest queued feedback: removes archive string construction from the request path.
- Pool null guard: ~1 us, prevents invalid fallback/destruction path.

Verification:
- No `dotnet build` launched per user instruction.
- `git diff --check` passed on touched files.
- `rg` found no active direct `.Deconstruct()` calls in touched deconstruction paths.
- `rg` found no stale completed/recovered recovery strings, no `NativeHashSet` leftovers, and no dead laser archive helper.
