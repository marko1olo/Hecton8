# OMEGA PURGE SURGERY LOG

Date: 2026-04-30
Status: PENDING VERIFICATION

This log records enforcement work performed after `TOTAL_CODEBASE_AUDIT_V2.md`.
No Play Mode was launched.

## Completed Enforcement

| Area | Result |
|---|---|
| Burst contract normalization | Static scan: `128` IJob-like structs, `0` missing explicit `[BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. |
| Unauthorized Unity loops | Static scan: `0` `Update`/`FixedUpdate`/`LateUpdate` outside `SystemDispatcher` and `GameBootstrapper`. |
| Hot-path string/GC direct findings | Static scan: `0` direct `.ToString`, interpolation, or LINQ hits inside Tick-named methods. |
| SPSC queue review findings | `_impactEventReadIndex` and `_impactEventWriteIndex` direct reads were replaced with `Volatile.Read`. |
| LayerMask constructor finding | `SolarPanel` static constructor `LayerMask.NameToLayer("Water")` moved to `[RuntimeInitializeOnLoadMethod]`. |
| Compliance gate | Added editor reload validator at `Assets/_Project/Scripts/Editor/HectonComplianceValidator.cs`. |

## First 20 Normalized Job Structs

| File:Line from pre-fix audit | Struct | Action |
|---|---|---|
| `Assets/_Project/Scripts/CraftingSystem.cs:22` | `EvaluateRecipeAvailabilityJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/CraftingSystem.cs:51` | `BuildDeconstructionYieldJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/EncounterDirector.cs:759` | `EncounterDirectorJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/FlowFieldVisualizer.cs:506` | `FlowSamplingJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonFloatingOrigin.cs:26` | `OriginShiftTranslateJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonFloatingOrigin.cs:40` | `AupDriftCheckJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonFluidEngine.cs:1864` | `WaveQueryJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonFluidEngine.cs:1934` | `BuoyancyJob` | Inserted missing Burst attribute. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:428` | `VoxelDensityJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1486` | `VoxelColliderChunkClassifyJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1525` | `VoxelMCCountJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1587` | `VoxelMCExtractJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1740` | `VoxelWeldJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1865` | `VoxelNormalJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:1954` | `VoxelTerrainSeamSnapJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2011` | `VoxelSeamNormalBlendJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2128` | `VoxelShiftAwareProjectionJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2147` | `VoxelBiomeSampleJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2179` | `VoxelColorJob` | Replaced Burst attribute with explicit contract. |
| `Assets/_Project/Scripts/HectonVoxelEngine.cs:2268` | `VoxelSpawnPointJob` | Replaced Burst attribute with explicit contract. |

## Unauthorized Loop Migration

Migrated away from direct Unity loop methods:

| File | Previous loop | Replacement |
|---|---|---|
| `Core/ConnectionSplineBatchRenderer.cs` | `LateUpdate` | `ILateFrameTickable.LateFrameTick` registered via `GlobalRegistry`. |
| `Editor/GCSentinel.cs` | `LateUpdate` | `IUpdatable.Tick` registered via `GlobalRegistry`. |
| `GlobalPhysicsStateManager.cs` | `LateUpdate` | `ILateFrameTickable.LateFrameTick` registered via `GlobalRegistry`. |
| `Interaction/EquipmentInteractionHandler.cs` | `LateUpdate` | `ILateFrameTickable.LateFrameTick` registered via `GlobalRegistry`. |
| `ObserverRelativeCelestialBody.cs` | `LateUpdate` | Removed duplicate path; existing tick already calls `ApplyPlacement`. |
| `SkySystemFollowCamera.cs` | `LateUpdate` | `IUpdatable.Tick` registered via `GlobalRegistry`. |
| `TetherManager.cs` | `LateUpdate` | `ILateFrameTickable.LateFrameTick` registered via `GlobalRegistry`. |
| `UI/LocalizedTMPAutoSizer.cs` | `LateUpdate` | `IUpdatable.Tick` registered via `GlobalRegistry`. |
| `UI/LocalizedLayoutMirror.cs` | `LateUpdate` | `IUpdatable.Tick` registered via `GlobalRegistry`. |
| `UI/SuitHUDV4CanvasOverlay.cs` | `LateUpdate` | `ILateFrameTickable.LateFrameTick` registered via `GlobalRegistry`. |

## Blocked / Rejected Actions

### Core asmdef surgery

Rejected as a blind edit.

Facts:
- `Hecton8.Core.asmdef` sits at `Assets/_Project/Scripts/`, so it owns most runtime files by default.
- Static dependency scan found `1,879` UI/TMP/URP/Crest/MapMagic/GPUInstancer/third-party hits under this root assembly.
- Removing `UnityEngine.UI`, `Crest`, and `MapMagic` references now would create compile errors before bridge assemblies exist.

Required next step:
- Create staged bridge asmdefs for UI, URP/Visor, Crest, MapMagic, GPUInstancer, and world rendering owners.
- Move files by ownership or introduce nested asmdefs before removing references from `Hecton8.Core.asmdef`.

### GlobalRegistry ghost hunt

Not converted in this pass.

Facts:
- Static scan found `127` `public static ...Instance` matches under `Assets/_Project/Scripts`.
- This is a broad ownership/API migration, not a safe mechanical edit.
- Converting these blindly would change initialization order, save/load reachability, inspector wiring, and third-party bridge access.

Required next step:
- Build a registry migration queue by domain: bootstrap/core first, then gameplay managers, then UI/optimization, then world/third-party bridges.
- Convert one domain per pass with compile verification and scene readback.

### GlobalTelemetryBus memory-mapped rotation

Rejected as a blind runtime replacement.

Facts:
- Current `GlobalTelemetryBus` already uses an O(1) NativeArray ring and async binary export.
- Replacing it with OS memory-mapped files changes platform/AOT behavior and must be verified against Unity/IL2CPP targets.

Safe next step:
- Expand current NativeArray ring to a measured 5 MB event budget or add an editor/platform-gated memory-mapped backend behind an interface.

## Verification

| Check | Result |
|---|---|
| Burst static scan | PASS: `0` missing explicit flags. |
| Unauthorized loop static scan | PASS: `0` outside dispatcher/bootstrap. |
| Hot-path string static scan | PASS: `0` direct hits. |
| Local `dotnet build Hecton8.Core.csproj` | INCONCLUSIVE: generated csproj references missing `Unity.ShaderGraph.Editor` and stale deleted source paths. |
| Unity MCP refresh/console | PASS FOR EDITOR CONSOLE ONLY: initial refresh timed out waiting for readiness, then MCP recovered and `read_console` returned `0` error entries and `0` total entries. Play Mode was not launched. |

STATUS: PENDING VERIFICATION
