# 2026-05-03 Foundation Hardening Continuation

Status: PENDING VERIFICATION

## Scope

- Removed the remaining forced PhysX-bake completion path from `HectonWorldGenerator` runtime chunk retirement.
- Added async mesh-build yield watchdogs to `ProceduralWreckGenerator`.
- Reduced floating-origin stability watchdog from 50,000 frames to 1,200 frames.
- Kept `UserOptionsPersistence` owned by bootstrap/GlobalRegistry instead of self-persisting from the input assembly.

## Evidence

Fresh Unity batchmode log:

- File: `Temp/CodexArtifacts/unity-batch-2026-05-03-foundation-hardening-after-watchdogs.log`
- Result: `Exiting batchmode successfully now!`
- Tundra: `Tundra build success (51.07 seconds), 33 items updated, 1808 evaluated`
- Script compile: `CompileScripts: 52654.128ms`
- Mono reload: `Mono: successfully reloaded assembly`
- Strict compiler failure scan: `0` matches for `error CS`, `warning CS`, `Compiler error`, `Scripts have compiler errors`, `Tundra build failed`, `Compilation failed`

Fresh dotnet verification:

- Command: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:UseSharedCompilation=false`
- Result: `0 Error(s)`
- Residual warnings: `MSB3245 Unity.Cecil.Awesome` generated-project reference warnings. These are not C# warnings from modified files.

## Surgery Log

`HectonWorldGenerator` now hands canceled/scheduled PhysX bakes to a late-frame deferred teardown queue:

```csharp
if (!DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false))
    continue;

pending.Mesh.Clear();
DestroyDeferredObject(pending.Mesh);
DestroyDeferredObject(pending.Owner);
```

Runtime eviction/cancellation no longer blocks on active PhysX bake handles. Pending chunk renderers/colliders are disabled, then ownership is transferred to the deferred teardown driver until the job naturally completes.

`ProceduralWreckGenerator` mesh-build slicing now aborts after the watchdog ceiling instead of yielding indefinitely:

```csharp
if (!await YieldMeshBuildFrameAsync("merged mesh copy scheduling", meshYieldFrames++))
    return null;
```

`HectonFloatingOrigin` now reports shift-stability failure after 1,200 frames instead of 50,000 frames, keeping physics-pause failures diagnosable within a practical window.

## Regression Model

- CPU: deferred teardown removes streaming-retirement barriers, but still needs Play Mode eviction stress to prove no late-frame backlog.
- GC: no measured GCMonitor proof. Source-level changes use preallocated collections or timeout-only diagnostics.
- Memory: deferred meshes/owners live until their bake handle completes; leak risk requires rapid stream/unload soak testing.
- Correctness: chunk side effects are released before mesh object destruction; regression risk remains around orphaned chunk roots if Unity scene unloads before late-frame drain.
- Editor: Unity batch compile/import is clean for this pass; MCP runtime console was not available.

## Do Not Claim

- Do not claim Play Mode deadlock fixed.
- Do not claim zero GC.
- Do not claim MCP verified.
- Do not claim runtime memory retention solved.
