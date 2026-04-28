# AGENT_04_CORE_LOG

Date: 2026-04-26
Status: PENDING VERIFICATION

Mandates followed:
- `ARCH_Project_Bootstrap_Sequence_Init_Safety`
- `ARCH_Global_Registry_ServiceLocator_DI_Init`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin`
- `STRM_Persistent_Object_Registry`
- `STRM_World_Streaming_Residency_Chunk_Management`
- `DATA_Save_Persistence_Binary_Delta_Checksum`

## Iteration 17 - Safe Mode Resuscitation

Objective:
- Recover Unity from Safe Mode.
- Restore `com.coplaydev.unity-mcp` package integrity.
- Clear compiler blockers after the package/cache damage from earlier assembly stripping.
- Reduce `ProcessInitializeOnLoadAttributes` cost by removing eager editor startup work.

Exact terminal command used to wipe caches:

```powershell
$projectRoot = 'C:\hades\Hecton8'
$targets = @(
    'C:\hades\Hecton8\Library',
    'C:\hades\Hecton8\obj',
    'C:\hades\Hecton8\Logs'
)

$unity = Get-Process Unity, UnityHub -ErrorAction SilentlyContinue
if ($unity) { $unity | Stop-Process -Force }

foreach ($target in $targets)
{
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $resolved = (Resolve-Path -LiteralPath $target).Path
    if ($resolved -notlike ($projectRoot + '*'))
    {
        throw "Refusing to delete out-of-root path: $resolved"
    }

    Remove-Item -LiteralPath $resolved -Recurse -Force
}
```

Observed deletion result:
- `Library => False`
- `obj => False`
- `Logs => False`

### MCP dependency repair

Audit result:
- `Packages/com.coplaydev.unity-mcp` does not currently contain `UnityEditor.TestTools` or `UnityEngine.TestTools` imports.
- The Safe Mode compiler blocker after cache repair was not `unity-mcp` source; it was a broken ShaderGraph package compile path plus one stray editor script in first-party code.

Restorations applied:
- Restored `Assets/_Project/Tests`.
- Restored `Assets/_Project/Scripts/World/Dots`.
- Removed the bad embedded `Packages/com.unity.collections` folder.
- Removed the bad `"com.unity.collections": "file:com.unity.collections"` override from `Packages/manifest.json`.

Compiler repair applied:
- Embedded `com.unity.shadergraph` into `Packages/com.unity.shadergraph`.
- Fixed the package compile failure by aliasing `GUID = UnityEngine.GUID` in:
  - `Packages/com.unity.shadergraph/Editor/Generation/Contexts/TargetSetupContext.cs`
  - `Packages/com.unity.shadergraph/Editor/Generation/Processors/ShaderSpliceUtil.cs`
  - `Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Targets/BuiltInCanvasSubTarget.cs`
- Deleted stray blocker:
  - `Assets/_Project/Editor/HectonIteration17BatchRepair.cs`

`unity-mcp` startup hardening applied:
- `Packages/com.coplaydev.unity-mcp/Editor/Migrations/LegacyServerSrcMigration.cs`
- `Packages/com.coplaydev.unity-mcp/Editor/Migrations/StdIoVersionMigration.cs`

Change:
- Both migrations are now session-once instead of re-running on every domain reload.

### Initialize-on-load purge

First-party:
- `Assets/_Project/Editor/HectonRenderPipelineValidator.cs`
  - removed automatic `[InitializeOnLoad]` bootstrap.
  - validation is now explicit menu/batch invocation only.
- `Assets/Bakery/ftLightmaps.cs`
  - batch mode now returns immediately from the static constructor.

Third-party lazy-load/session gating:
- `Assets/RealtimeCSG/RealtimeCSG/Plugins/Editor/Scripts/Control/Managers/UpdateLoop.cs`
  - moved full editor initialization out of the static constructor into deferred editor-idle init.
  - removed automatic scripting-define mutation during domain reload.
- `Assets/AmplifyImpostors/Plugins/Editor/AIPackageManagerHelper.cs`
  - session-once.
  - now reads the real persisted `Auto SRP` preference instead of the static default field during reload.
- `Assets/AmplifyImpostors/Plugins/Editor/AIStartScreen.cs`
  - session-once.
  - missing preference now defaults to no automatic popup.
- `Assets/AstarPathfindingProject/Editor/AstarUpdateChecker.cs`
  - session-once startup scheduling.
- `Assets/Candice AI for Games/Scripts/Editor/CandiceAutorun.cs`
  - session-once.
  - no startup window when no persisted opt-in exists.
- `Assets/Technie/PhysicsCreator/Updater/PhysicsCreatorUpdater.cs`
  - session-once orphan scan.
- `Assets/Plugins/Easy Save 3/Editor/ES3ScriptingDefineSymbols.cs`
  - session-once define sync.
- `Assets/Plugins/Editor/DarkTonic/MasterAudio/AudioScriptOrderManager.cs`
  - session-once execution-order scan.
- `Assets/GPUInstancer/Scripts/Editor/GPUInstancerDefines.cs`
  - session-once startup initialization.
- `Assets/Feel/NiceVibrations/Define/NiceVibrationsDefineSymbols.cs`
  - session-once define sync.

Additional local asmdef guard:
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef`
  - added package-gated define:
    - `HECTON8_HAS_ENTITIES_PACKAGE`
  - assembly is now explicitly tied to `com.unity.entities`.

### Verification evidence

Safe Mode / compile recovery:
- `.iter17c_unity_batch.log`
  - ended with `Exiting batchmode successfully now!`
  - ended with `Application will terminate with return code 0`
- `.iter17h_unity_batch.log`
  - ended with `Exiting batchmode successfully now!`
  - ended with `Application will terminate with return code 0`

Cold-start first reload after repair:
- `.iter17h_unity_batch.log`
  - `ProcessInitializeOnLoadAttributes (160ms)`
  - `ProcessInitializeOnLoadMethodAttributes (163ms)`

Cold-start compile reload after script compilation:
- `.iter17h_unity_batch.log`
  - `ProcessInitializeOnLoadAttributes (13167ms)`
  - `ProcessInitializeOnLoadMethodAttributes (1386ms)`

In-session live reload probe after one no-op editor-script comment edit:
- `.iter17_live_unity.log`
  - `Domain Reload Profiling: 14521ms`
  - `ProcessInitializeOnLoadAttributes (4341ms)`
  - `ProcessInitializeOnLoadMethodAttributes (924ms)`

Interpretation:
- Safe Mode/compiler failure is cleared.
- First cold editor startup still pays a heavy post-compile reload.
- The targeted `InitializeOnLoad` work was reduced materially on an in-session reload:
  - attributes: `13167ms -> 4341ms`
  - methods: `1386ms -> 924ms`
- Fast reload is not secured yet. Status remains `PENDING VERIFICATION`.

### Remaining blockers

Still present in current logs:
- skipped invalid assemblies, including:
  - `DocCodeExamples.dll`
  - `Hecton8.EditModeTests.dll`
  - `Hecton8.PlayModeTests.dll`
  - `Hecton8.World.Dots.dll`
  - `Unity.Collections.Tests.dll`
  - package test/doc assemblies under `Library/ScriptAssemblies`
- Crest warning:
  - `Assets/Crest/Crest/Scripts/LodData/RegisterLodDataInput.cs contains partial class of Unity.Object...`
- TMP shutdown exception on editor quit:
  - `TMP_FontAsset.m_AtlasTextures` unassigned
- MCP shutdown noise on quit:
  - `No process found listening on port 8088`

## Bootstrap Sequence

Entry vector:
- Runtime entry must resolve to `00_BOOTSTRAP`.
- `GameBootstrapper` now guards the entry vector in both `BeforeSceneLoad` and `AfterSceneLoad`.
- If play starts from any non-bootstrap scene, `TryRecoverEntryVector()` redirects to `00_BOOTSTRAP` before normal boot proceeds.

Ordered runtime initialization:
1. `GameBootstrapper.InitializeBootstrap()`
2. Core layer
   - `SystemDispatcher.EnsureRuntimeInstance()`
   - `RenderDispatcher.EnsureRuntimeInstance()`
   - `SceneInstantiationGate.EnsureRuntimeInstance()`
   - `SceneRuntimeService.InitializeService()`
   - `EquipmentInteractionHandler.InitializeService()`
3. Environment layer
   - `GlobalPhysicsStateManager.EnsureRuntimeInstance()`
   - `PhysicsApplySystem.InitializeService()`
   - `DebrisManager.InitializeService()`
   - `EnvironmentRuntimeContextService.InitializeService()`
4. Player layer
   - Input configuration validation
   - `InputDispatcher.InitializeService()`
   - `PlayerRuntimeContextService.InitializeService()`
   - `ContextualPhysicalIkRuntime.EnsureRuntimeInstance()`
5. UI layer
   - No central UI registry bootstrap owner exists yet. Scene-authored UI controllers remain the source of truth.

Lifecycle contract:
- `Awake()` is self-init only.
- External runtime registration is deferred to `OnEnable()` / `Start()` and now blocked when `Application.isPlaying == false`.

## Edit-Time Contamination Guard

Secured paths:
- `GlobalRegistry.RegisterUpdatable(...)`
- `GlobalRegistry.RegisterFixedTickable(...)`
- `GlobalRegistry.RegisterSlowTickable(...)`
- `SystemDispatcher.EnsureRuntimeInstance()`
- `PersistentWorldRegistry.TryRegisterTick()`
- `HectonFloatingOrigin.TryRegister()`

Rule now enforced:
- Editor-time `ExecuteAlways`, `OnValidate`, or scene-authoring callbacks cannot materialize `[SystemDispatcher]` or register first-party runtime lanes.
- Runtime buckets remain inert until play mode.

UI-specific containment:
- `HectonUIScaler.ResolveContentRoot(...)` no longer auto-creates the scaler hierarchy during editor-only resolution.
- `SuitHUDV4CanvasOverlay` no longer depends on editor-only APIs for default icon lookup inside the runtime assembly.

## System Cadence

Current dispatcher cadence:
- Fast tick: `SystemDispatcher.Update()` once per rendered frame.
- Fixed tick: `SystemDispatcher.FixedUpdate()` using Unity fixed timestep.
- Slow tick: `SystemDispatcher.RunSlowTick()` every `0.5s`.

Bootstrap lane gating:
- Core, environment, and UI lanes may continue during bootstrap.
- Player lane is gated until `BootstrapState.IsGameReady`.

Precision watchdog:
- `HectonFloatingOrigin.Tick(...)` now performs a 300-frame watchdog check.
- If anchor relative position exceeds `2048m` radius at the watchdog checkpoint, the system forces an origin shift.
- Normal shift threshold remains `_threshold` / `_thresholdSqr`.

## AUP Rules

Authoritative coordinate model:
- `AbsoluteUniversePosition = int64 grid + float32 local offset`
- Cell size: `5000m`
- Runtime world position is presentation-only.
- Persistent residency and save payloads derive from AUP, not scene searches.

Shift rules:
- Physics is paused during shift.
- Shift is atomic.
- Shader globals are refreshed after shift commit.
- Player/world runtime code must read committed offset through `HectonFloatingOrigin.CurrentTotalOffset`.

Watchdog and safe bounds:
- AUP precision watchdog runs every 300 frames.
- Forced shift radius for watchdog: `2048m`.
- Normal runtime shift trigger still uses configured `_threshold`.

## Persistent Residency / Dehydration

Owner:
- `PersistentWorldRegistry`

Data truth:
- Persistent dropped-item state lives in unmanaged record storage:
  - `NativeList<PersistentWorldItemRecord>`
  - `NativeArray<PoolSlotData>`
  - `NativeHashMap<ulong,int> _guidToPoolIndex`

Residency loop:
1. Resolve player AUP from runtime transform.
2. Determine whether hydration rescan is required by chunk change or AUP delta.
3. Sync hydrated records back into unmanaged state.
4. Queue records outside `160m` for dehydration.
5. Drain dehydration queue on the main thread.
6. Hydrate only records inside the active hydration window.

Dehydration behavior:
- Proxy GameObject is returned to `ObjectPoolManager` or deactivated.
- Rigidbody velocity is zeroed and kinematic state restored.
- Persistent record position and quantity remain in registry storage.
- No scene scans and no pool-wide O(N) GUID search are used.

## Save Decode Guard

Owner:
- `SaveBinaryStorage`

Hard bounds now enforced:
- Save file length must not exceed `HeaderSize + MaxCompressedPayloadBytes`.
- Compressed payload length must not exceed `MaxCompressedPayloadBytes`.
- Entity section offset must stay within the raw payload decoder budget.
- LZ4 block loop stays bounded by both compressed length and destination budget.
- Individual decoded blocks larger than `BlockSizeBytes` are rejected.

Failure mode:
- Corrupt headers fail fast before payload walk.
- Decoder returns failure instead of entering an unbounded parse loop.

## Verification State

Code-level verification completed:
- Assembly-target spam removed from live console:
  - `Unity.Properties.Internals.asmref`
  - Crest HDRP bridge asmrefs
- Runtime registration into dispatcher lanes is blocked in edit mode.
- Save decoder bounds are hardened.
- Core bootstrap entry guard executes earlier.

Unity runtime verification:
- PENDING VERIFICATION
- Play mode must be re-tested from the relaunched editor session to confirm the new `BeforeSceneLoad` bootstrap redirect clears the remaining play-entry stall.
