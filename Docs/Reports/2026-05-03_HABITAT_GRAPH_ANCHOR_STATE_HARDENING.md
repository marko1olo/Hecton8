# 2026-05-03 Habitat Graph Anchor-State Hardening
Date: 2026-05-07

Status: PENDING VERIFICATION

## 2026-05-04 Supersession Note

This report is May 3 source/build evidence for the habitat graph anchor-state patch. Current global documentation/build/guard truth starts at `Docs/Reports/2026-05-04_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-04_FOUNDATION_GUARD_UNSAFE_COPY_AND_MENU_LOOP_REPAIR.md`: Core/Editor compile `0 Warning(s)` / `0 Error(s)`, DOTS restore build `1 Warning(s)` / `0 Error(s)`, PlayModeTests restore build `0 Warning(s)` / `0 Error(s)`, post-repair Core compile `0 Warning(s)` / `0 Error(s)`, and foundation guard scan exit `0`.

## Mandates Followed

- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `UI_Data_Streaming_ZeroGC_Optimization.txt`

## What Was Wrong

`HabitatGraphManager` used `_anchorReachability` for two incompatible jobs:

- authoritative anchor/isolated-state truth after `EvaluateAnchorReachability()`
- temporary BFS visited/depth scratch in flood center-of-mass traversal, fungal target lookup, and component power traversal

That means later graph publish paths could read traversal scratch as if it were anchor truth. In the rebuild sequence, `PublishComponentPowerState()` could mark every traversed component node as reachable before `PublishEmergencyLockdownState()` wrote `LogisticsNode.Reserved`. Result risk: isolated modules being published as anchored in the logistics graph reserved state.

## What I Did

Changed `Assets/_Project/Scripts/Construction/HabitatGraphManager.cs` only.

Added a persistent `_traversalVisited` `NativeArray<byte>` owned by `HabitatGraphManager` and allocated/disposed with the existing native graph buffers.

Moved scratch traversal writes from `_anchorReachability` to `_traversalVisited` in:

- `ApplyIslandFloodCenterOfMassShifts()`
- `TryResolveFungalMindTarget()`
- `PublishComponentPowerState()`

Kept `_anchorReachability` dedicated to anchor truth from `EvaluateAnchorReachability()` and consumers that intentionally publish anchored/isolated state.

Added a global siege-target owner token and subsystem-registration reset:

- `PublishSiegeTargetSnapshot()` now records the publishing `HabitatGraphManager` owner.
- `ClearSiegeTargetSnapshot()` now clears the static latest snapshot only when the clearing instance is still the publisher.
- `ResetStaticSiegeTargets()` clears static siege-target state on subsystem registration.

This prevents an older disposed/rebuilt graph manager from clearing a newer published siege-target snapshot by static side effect.

## Evidence

Fresh baseline before this patch:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `0 Error(s)`, `18 Warning(s)`
- Warning class: `MSB3245 Unity.Cecil.Awesome` generated-project reference warnings
- May 3 note: this warning class was superseded by generated-reference pruning in `Directory.Build.targets` and `HectonGeneratedProjectReferencePruner`; the May 3 warning-gate Core build reported `0 Error(s)` / `0 Warning(s)` with `-clp:ErrorsOnly`.

Fresh compile after this patch:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `0 Error(s)`, `1 Warning(s)`
- Warning class: `MSB3026` file-copy retry on `Temp\obj\Hecton8.Core\Hecton8.Core.dll`
- The warning is a build artifact file-lock retry, not a C# compiler warning from the changed file.

Latest follow-up full Core compile after generated-reference pruning:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `0 Error(s)`, `0 Warning(s)`
- The earlier `MSB3026` file-copy retry did not reproduce.

Latest strict Core warning gate after the current dirty-state UI compile blocker recheck:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false -clp:ErrorsOnly`
- Result: `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`
- Prior `DiegeticTooltipSystem` `CS0103` symbols did not reproduce in the serial rerun; source contains the referenced methods.

## Regression Model

- CPU: one additional linear clear of `_traversalVisited` replaces the previous clear of `_anchorReachability`; traversal count is unchanged. No measured CPU profile.
- GC: no managed allocations added to runtime paths. The added buffer is `Allocator.Persistent`.
- Memory: adds `nodeCapacity` bytes of persistent native memory per `HabitatGraphManager` instance. Default initial capacity is `64` bytes; growth follows existing graph-buffer capacity.
- Memory: adds one static owner reference for the latest siege-target snapshot. It is reset on subsystem registration and when the publishing manager clears its snapshot.
- Cadence: no tick registration, dispatcher, job scheduling, or service authority changed.
- Correctness: anchor truth is no longer overwritten by non-anchor graph traversals before later publish phases.
- Correctness: stale graph-manager disposal is less able to clear a newer static siege-target snapshot.

## Hot Path Impact

Hot-path allocation proof is source-level only. The modified paths still use preallocated native arrays and indexed loops.

Measured `0 B/frame` proof is absent.

## Failure Modes

- If another method later reuses `_anchorReachability` as scratch, the same class of bug can return.
- Runtime scene/prefab wiring can still publish wrong anchor state if module markers or anchor roles are wrong.
- This does not verify base gameplay, power brownout balance, emergency airlock lockdown behavior, or predator siege target consumption.
- Static siege-target publication is still not runtime-proven across construction manager replacement, scene unload, domain reload, and predator query timing.

## Do Not Claim

- Do not claim habitat/base gameplay is runtime-verified.
- Do not claim logistics anchor state is correct in scene without Play Mode and inspection.
- Do not claim zero GC.
- Do not claim memory retention is solved.

STATUS: PENDING VERIFICATION
