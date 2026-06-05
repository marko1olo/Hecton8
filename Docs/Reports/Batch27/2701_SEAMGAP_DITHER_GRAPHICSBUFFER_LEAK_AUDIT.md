# 2701 SeamGapDitherRenderer GraphicsBuffer Leak Audit

Status: STATIC VERIFIED for source/log audit. Runtime clearance is PENDING VERIFICATION.
Worker: Batch27 Worker 2701.
Mode: report-only. No Unity launch, Play Mode, dotnet build, process kill, asset import, or project source edit performed.

## Read Set

- `AGENTS.md`
- `quality.md`
- `performance.md`
- `rendering.md`
- `compute.md`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/OPT_Performance_Budgets_FrameTime_VRAM_Limits.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/REND_GPU_Sovereignty.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/CORE_Global_State_Reset_NonReload_Transitions.txt`
- `Docs/Reports/Batch26/BATCH26_SYNTHESIS_FOR_UNITY_OWNER.md`
- `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs` (`GraphicsBufferUploadUtility`)
- `Assets/_Project/Scenes/02_HECTON_WORLD.unity`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474.log`
- `Docs/AgentLogs/UnityEditor_visual_audit_restart_1474b.log`
- Current read-only Editor log: `C:/Users/danat/AppData/Local/Unity/Editor/Editor.log`

## Evidence

- Batch26 names `SeamGapDitherRenderer.EnsureBuffers()` as a runtime proof blocker at `SeamGapDitherRenderer.cs:455`, `456`, `466`, `467`, `476`, and `481`.
- `UnityEditor_visual_audit_restart_1474.log` contains no matching `SeamGapDitherRenderer` leak stack in the searched tokens.
- `UnityEditor_visual_audit_restart_1474b.log` contains repeated leak stacks during `Begin MonoManager ReloadAssembly`.
- `1474b` examples:
  - `SeamGapDitherRenderer.cs:455`: matrix buffer A via `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>()`.
  - `SeamGapDitherRenderer.cs:456`: matrix buffer B via `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Matrix4x4>()`.
  - `SeamGapDitherRenderer.cs:466`: color buffer A via `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>()`.
  - `SeamGapDitherRenderer.cs:467`: color buffer B via `GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>()`.
  - `SeamGapDitherRenderer.cs:476`: indirect args buffer A via direct `new GraphicsBuffer(...)`.
  - `SeamGapDitherRenderer.cs:481`: indirect args buffer B via direct `new GraphicsBuffer(...)`.
- The stacks flow through `SeamGapDitherRenderer.EnsureRenderingResourcesCold()` at line `388` and `Awake()` at line `118`.
- Current live Editor log searched as read-only shows only `Begin MonoManager ReloadAssembly` lines for the searched leak tokens, but this is not acceptance proof. No fresh reload/play-exit run was performed.

## Current Lifecycle Map

### Scene Owner

- `02_HECTON_WORLD.unity:47672-47713` owns one enabled `SeamGapDitherRenderer` component.
- Scene fields bind:
  - `seamRegistry`
  - `playerTransform`
  - `targetCamera`
  - `seamDitherMaterial`
  - `seamDitherQuadMesh`
  - `integrationDirector`
  - `maxInstances: 512`, clamped by source to `MaxMotesPerChunk` (`256`)
  - flora-root motes enabled
- No prefab wrapper or separate scene cleanup owner was found for this component.

### Managed Cold Resources

- `_stateScratch`: `List<ProceduralGeologySeamStateDTO>` allocated as an instance field at lines `75-76`. Managed, owner-local, no disposal route required.
- `_legacyRuntimeScratch`: `List<WorldGenerativeGeologySeamRuntime>` allocated as an instance field at lines `77-78`. Managed, owner-local, no disposal route required.
- `_argsUpload`: `GraphicsBuffer.IndirectDrawIndexedArgs[1]` allocated as an instance field at lines `79-80`. Managed array, no disposal route required.
- `_matrixUpload`: managed `Matrix4x4[]`, allocated/resized in `EnsureCpuCapacity()` lines `426-433`.
- `_colorUpload`: managed `Vector4[]`, allocated/resized in `EnsureCpuCapacity()` lines `435-439`.
- `_drawPropertyBlock`: `MaterialPropertyBlock`, allocated in `EnsureDrawPropertyBlockCold()` lines `391-397`, cleared during `ReleaseBuffers()` lines `836-837`.

### Mesh

- `_quadMesh` is assigned from serialized `seamDitherQuadMesh` at `EnsureQuadMesh()` lines `442-445`.
- `ReleaseQuadMesh()` lines `844-847` only nulls the runtime field.
- No `new Mesh()` path exists in this class. The mesh asset is not owned by this component.

### GraphicsBuffers

Owned GPU buffers:

- `_matrixBufferA` and `_matrixBufferB`: created at lines `455-456`, released at lines `826-827`.
- `_colorBufferA` and `_colorBufferB`: created at lines `466-467`, released at lines `828-829`.
- `_argsBufferA` and `_argsBufferB`: created at lines `476-485`, released at lines `830-831`.
- `_activeMatrixBuffer`, `_activeColorBuffer`, and `_activeArgsBuffer`: active references set during allocation or upload, nulled only by `ReleaseBuffers()` lines `832-834`.
- `_visualUploadBufferIndex`: reset in allocation branches and in `ReleaseBuffers()` line `835`.

Current release helper:

- `ReleaseBuffer(ref GraphicsBuffer buffer)` calls `buffer.Release()` and nulls the field at lines `849-855`.
- It does not call `IsValid()` before release.

### NativeArray Usage

- No persistent `NativeArray<T>` field exists in `SeamGapDitherRenderer`.
- `AppendFloraRootInstances()` borrows `NativeArray<Matrix4x4>` and `NativeArray<HectonVegetationInstanceData>` from `HectonMapMagicVegetationBridge.TryGetActiveUnderwaterNativePayload()` lines `655-661`.
- The borrowed arrays are method-scope only and are not stored.
- `GraphicsBufferUploadUtility.UploadArray()` maps `LockBufferForWrite` results into method-scope `NativeArray<T>` locals and unlocks in `finally` (`SystemDispatcher.cs:7469-7517`).
- No helper-level persistent native alias was found for this specific renderer path.

### Callbacks And Registry

- `TryRegister()` registers `IUpdatable` and `ILateFrameTickable` with `GlobalRegistry` only during play at lines `278-288`.
- `TryUnregister()` unregisters both at lines `290-303`.
- `TryRegisterHotSwapListener()` registers a GlobalRegistry hot-swap listener only during play at lines `356-362`.
- `TryUnregisterHotSwapListener()` unregisters it at lines `364-371`.
- `OnGlobalRegistryServiceReplaced()` updates cached service references and dispatcher registration flags at lines `305-329`.
- No static event handler, static buffer, or static instance list was found in `SeamGapDitherRenderer`.

### Unity Lifecycle

- `Awake()` lines `115-119`: resolves references and calls `EnsureRenderingResourcesCold()`.
- `OnEnable()` lines `121-127`: resolves references, calls `EnsureRenderingResourcesCold()`, registers hot-swap listener, registers dispatcher callbacks.
- `Start()` lines `129-134`: resolves references, calls `EnsureRenderingResourcesCold()`, registers dispatcher callbacks.
- `OnDisable()` lines `136-140`: unregisters dispatcher and hot-swap listener only. It does not release `GraphicsBuffer` resources.
- `OnDestroy()` lines `142-149`: unregisters, releases buffers, releases runtime material, nulls mesh field.

## Likely Leak Source

The leak source is `SeamGapDitherRenderer` owner lifecycle, not `GraphicsBufferUploadUtility`.

Reason:

- The leak stacks are all allocations inside `EnsureBuffers()` and direct callers are `EnsureRenderingResourcesCold()` and `Awake()`.
- `EnsureRenderingResourcesCold()` allocates buffers whenever `Application.isPlaying` is true at lines `381-389`.
- `OnDisable()` does not call `ReleaseBuffers()`.
- Unity assembly reload/play-exit paths reliably call `OnDisable`, but `OnDestroy` is not a sufficient sole teardown path for reload-disabled or reload-in-progress Editor transitions.
- `CORE_Global_State_Reset_NonReload_Transitions` explicitly forbids relying on `OnDestroy`, domain reload, or scene unload order as the only reset mechanism.
- The current class violates that lifecycle rule: owned `GraphicsBuffer` resources are released only in `OnDestroy`.

## Double-Buffer Stale Reference Finding

Current state:

- `ReleaseBuffers()` nulls all direct and active buffer fields.
- `EnsureBuffers()` releases direct A/B fields pair-by-pair, then recreates that pair and overwrites the matching active field.

Risk:

- If a pair is released/recreated due to capacity mismatch or invalidation, `_active*Buffer` can temporarily point at a released buffer until reassigned.
- If constructor failure or interrupted reload occurs inside `EnsureBuffers()`, active fields can remain stale.
- `AreRenderingResourcesResident()` checks `buffer != null` and `.count`, but not `GraphicsBuffer.IsValid()`. A non-null invalid wrapper can be treated as resident or throw while evaluating `.count`.

Conclusion:

- The stale active reference risk is secondary to the missing `OnDisable` release, but it should be fixed in the same source patch because the leak stack is already in the buffer lifecycle code.

## Invalid Phase Allocation Finding

`EnsureBuffers()` can allocate before dispatcher ownership is established because `Awake()` calls `EnsureRenderingResourcesCold()` and `EnsureRenderingResourcesCold()` calls `EnsureBuffers()` when `Application.isPlaying`.

This is not automatically wrong because GPU buffer allocation is a cold runtime path, but it creates a hard dependency on correct `OnDisable` teardown during Play exit, scene unload, and assembly reload. Current source does not meet that condition.

## Exact Fix Plan

Patch target: `Assets/_Project/Scripts/SeamGapDitherRenderer.cs`.

Do not patch `GraphicsBufferUploadUtility` for this leak. It is shared infrastructure and the inspected helper code unlocks mapped write buffers in `finally`.

Recommended source patch:

1. Add a small cleanup helper:

```csharp
private void ClearPendingVisualState()
{
    _pendingVisualDrawDirty = false;
    _pendingVisualInstanceCount = 0;
    _debugReady = false;
    _debugRenderedInstances = 0;
    _debugSourceSeams = 0;
    _debugDrawBounds = default;
}
```

2. Change `OnDisable()` from unregister-only to full renderer teardown:

```csharp
private void OnDisable()
{
    TryUnregister();
    TryUnregisterHotSwapListener();
    ClearPendingVisualState();
    ReleaseBuffers();
}
```

3. Keep `OnDestroy()` idempotent. Either leave the existing unregister/release calls or call the same teardown sequence explicitly. Do not remove `ReleaseBuffers()` from `OnDestroy`; it is safe after `OnDisable` because `ReleaseBuffers()` nulls fields.

4. Harden buffer validity checks:

```csharp
private static bool IsBufferReady(GraphicsBuffer buffer, int expectedCount)
{
    return buffer != null && buffer.IsValid() && buffer.count == expectedCount;
}
```

Use this helper in `AreRenderingResourcesResident()` for all six buffers and active buffers. For args buffers, expected count is `1`.

5. Rework `EnsureBuffers()` to avoid mixed old/new double-buffer state:

- Compute `requiredCapacity`.
- If all six direct buffers and all active buffers are valid for the required counts, return.
- Otherwise call `ReleaseBuffers()` once.
- Recreate all six buffers as one coherent set.
- Set `_activeMatrixBuffer = _matrixBufferA`, `_activeColorBuffer = _colorBufferA`, `_activeArgsBuffer = _argsBufferA`, and `_visualUploadBufferIndex = 0`.

6. Harden `ReleaseBuffer(ref GraphicsBuffer buffer)`:

```csharp
private static void ReleaseBuffer(ref GraphicsBuffer buffer)
{
    if (buffer == null)
        return;

    if (buffer.IsValid())
        buffer.Release();

    buffer = null;
}
```

7. Do not add editor-only static cleanup unless fresh proof shows `OnDisable` is not invoked. This class has no static resource registry today; adding one would broaden ownership surface for no current proof.

8. Do not fix by disabling the scene component as the primary path. That only hides seam/root-contact presentation and does not repair owner lifecycle.

## Risks

- Releasing buffers in `OnDisable()` means a later component re-enable performs a cold six-buffer reallocation. This is acceptable for lifecycle transitions, but not for any future per-frame quality toggle.
- If another system toggles this component as a quality mechanism, that system is wrong. Reduction must use continuous `GlobalQualityWeight`, distance/cadence/capacity scaling, not enable/disable thrash.
- Recreating all six buffers on any invalid/mismatched buffer costs one cold allocation burst but prevents mismatched double-buffer state and stale active references.
- Adding `IsValid()` can expose existing invalid wrappers earlier. That is correct behavior, but first runtime pass may reveal additional dirty lifecycle systems in the same proof window.
- Visual regression risk: if buffers are released while still registered for `LateFrameTick`, draw calls could disappear. The patch order must unregister before `ReleaseBuffers()`, as listed.

## Proof Required After Source Patch

Do not mark fixed from static source review. Required proof artifacts:

1. Fresh compile/import proof from Unity Editor after the source patch:
   - no C# compile errors;
   - no import loop;
   - no new source warnings tied to `SeamGapDitherRenderer`.

2. Fresh Editor reload log, same session, newer than the patch:
   - artifact path under `Docs/AgentLogs/`;
   - log tail must include a reload/play-exit window;
   - no `SeamGapDitherRenderer.EnsureBuffers` leak stack;
   - no `Persistent allocates` stack involving `UnityEngine.GraphicsBuffer:.ctor` from this renderer.

3. Play enter/exit proof:
   - enter `02_HECTON_WORLD`;
   - let renderer initialize;
   - exit Play Mode;
   - capture log tail after exit.

4. Suggested read-only validation commands after proof run:

```powershell
Select-String -LiteralPath "C:\Users\danat\AppData\Local\Unity\Editor\Editor.log" -Pattern "SeamGapDitherRenderer|Leak Detected|Persistent allocates|GraphicsBuffer:.ctor|EnsureBuffers" -Context 2,8
```

```powershell
Select-String -LiteralPath "C:\hades\Hecton8\Docs\AgentLogs\<fresh-log>.log" -Pattern "SeamGapDitherRenderer|Leak Detected|Persistent allocates|GraphicsBuffer:.ctor|EnsureBuffers" -Context 2,8
```

5. Runtime visual regression proof:
   - one same-session screenshot or capture from the seam/voxel route where seam dither/root motes are expected;
   - no missing-material error for `seamDitherMaterial`;
   - no blank indirect draw caused by released buffers after re-enable.

6. If acceptance needs profiler-level proof:
   - Memory Profiler or native leak detector snapshot after Play exit;
   - Frame Debugger/Rendering Stats only if draw behavior changes beyond lifecycle release.

## Low / Mid / High / Ultra Consequences

These are consequence labels. Any implementation must still consume continuous `GlobalQualityWeight`; no binary quality switch is acceptable.

Disabled renderer:

- Low: saves a tiny indirect draw/upload path, but exposes seam microgaps and flora-root contact gaps. Visual floor risk remains.
- Mid: terrain/voxel transitions lose polish in normal route play. Cheap but visibly worse.
- High: saved cost is wasted because high-tier should buy richer presentation, not remove seam camouflage.
- Ultra: unacceptable as a final state. Ultra should keep seam dither and add stronger near-field material/particle polish after leak proof.

Reduced renderer:

- Low: acceptable only if `maxInstances`, cadence, distance, and flora-root append density scale smoothly through `GlobalQualityWeight` while seams remain hidden.
- Mid: keep full gameplay truth and route readability; reduce only decorative density or upload cadence.
- High: spend headroom on more stable seam coverage and better biome tint variation.
- Ultra: increase sensory density and route polish without changing seam truth ownership or save/gameplay state.

Fixed renderer:

- Low: same intended visual output with clean lifecycle; no persistent `GraphicsBuffer` leak from this owner after proof.
- Mid: stable seam/root-contact presentation without reload pollution.
- High: keeps the renderer available for richer presentation because the memory owner is now deterministic.
- Ultra: enables higher presentation density later, but only after fresh profiler/render proof.

## Key Blockers

- Source fix not implemented by this report-only task.
- Fresh reload/play-exit proof is missing. Runtime status remains PENDING VERIFICATION.
- `1474b` contains other leak stacks outside this task scope (`WeatherEvents`, `WorldProceduralScatterDirector`, Crest). Even after the seam renderer patch, those can still block a clean proof window.
- Current live Editor log search did not show the seam leak tokens, but no fresh controlled reload/play-exit artifact was produced. Absence in a partial read-only search is not acceptance.
