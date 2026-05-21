# NATIVE ALLOCATION AUDIT

Date: 2026-04-29
Status: PENDING VERIFICATION
Scope: current changed first-party C# files under `Assets/_Project/Scripts`
Mandates followed: `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`, `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`, `ARCH_Project_Bootstrap_Sequence_Init_Safety.txt`, `STRM_Persistent_Object_Registry.txt`

## Audit Method

Changed-file scope came from `git diff --name-only --diff-filter=ACMR HEAD -- 'Assets/_Project/Scripts/**/*.cs'`.

The strict check applied here was narrower than the project mandate:

- find `Allocator.Persistent` native allocations in changed files
- verify whether the owning runtime path reaches `.Dispose()` from `OnDisable` or `OnDestroy`

## Hard Violation

### `Assets/_Project/Scripts/World/VoxelDynamicNavGridRuntime.cs`

Persistent array allocations:

- `VoxelDynamicNavGridRuntime.cs:1191` allocates `NativeArray<byte>`
- `VoxelDynamicNavGridRuntime.cs:1202` allocates `NativeArray<ushort>`

Observed disposal path:

- per-record dispose exists in `VoxelDynamicNavGridRuntime.cs:444-486`
- static teardown exists in `VoxelDynamicNavGridRuntime.cs:1083-1109`
- subsystem reset entry exists in `VoxelDynamicNavGridRuntime.cs:495-499`

Violation:

- the file has no `OnDisable()` and no `OnDestroy()`
- the persistent arrays are not disposed from either lifecycle method because the owner is a static runtime helper
- this fails the exact audit criterion requested for this pass

## Delegated / Non-Hard Findings

These changed files allocate persistent native arrays but do have a disposal owner reachable from runtime lifecycle or explicit reset. They are not hard failures under this report.

### `Assets/_Project/Scripts/Fauna/FaunaBrain.Compatibility.cs`

- `PredatorMemory.Initialize()` allocates a persistent `NativeArray<float4>` at `FaunaBrain.Compatibility.cs:288-297`
- `PredatorMemory.Dispose()` releases it at `FaunaBrain.Compatibility.cs:398-406`
- owner disposal is reached from `FaunaBrain.OnDestroy()` via `_utilityBrain.Dispose()` at `FaunaBrain.cs:257-268`

### `Assets/_Project/Scripts/Fauna/PredatorCognitionDomain.cs`

- shared static domain allocates many persistent native arrays at `PredatorCognitionDomain.cs:828-888`
- shared static teardown exists at `PredatorCognitionDomain.cs:723-826`
- subsystem reset calls that teardown at `PredatorCognitionDomain.cs:327-330`

This is not lifecycle-owned by a `MonoBehaviour`, so it misses the exact `OnDisable` / `OnDestroy` wording, but it does have a defined static reset path.

### Direct lifecycle pass examples

- `Assets/_Project/Scripts/World/EcosystemDirector.cs`
  - allocates at `981-995`
  - disposes through `DisposeRuntimeState()` called from `OnDisable()` at `635-643`
- `Assets/_Project/Scripts/World/FloraInteractionManager.cs`
  - allocates at `513-515` and `1501`
  - has `OnDisable()` / `OnDestroy()` at `538-557`
- `Assets/_Project/Scripts/World/HectonMapMagicVegetationBridge.cs`
  - large persistent surface
  - teardown fan-out is called from both `OnDisable()` and `OnDestroy()` at `1995-2041` and `2043-2085`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
  - allocates at `598-634`
  - has `OnDisable()` / `OnDestroy()` at `674-742`

## Adjacent Observation

`ThreadSafeCommandQueue.cs` owns a persistent `NativeQueue<EntityCommand>` at `126` and tears it down through static `Shutdown()` at `201-207`, called from `SystemDispatcher.ResetStaticState()` at `SystemDispatcher.cs:117-125`.

This is not a `NativeArray`, so it is outside the hard fail condition for this pass.

## Verdict

- strict `OnDisable` / `OnDestroy` audit result: `FAIL`
- evidence-backed hard violation count: `1`
- delegated or static reset owners present: `2`

## Regression Model

CPU: audit-only, no runtime mutation
GC: audit-only, no gameplay-path mutation
Memory: audit-only, no asset or scene mutation
Cadence: no runtime cadence change
Correctness: improved because static-reset ownership and lifecycle-owned disposal are no longer conflated

## Hot Path Impact

None. Markdown-only report.

## Failure Modes

- static native owners can survive scene churn longer than intended if subsystem reset is skipped
- delegated disposal can become invisible during future audits when allocation and owner teardown live in different files

## Why Kept

Kept because it distinguishes one hard criterion failure from several owner-delegated or static-reset cases.

STATUS: PENDING VERIFICATION
