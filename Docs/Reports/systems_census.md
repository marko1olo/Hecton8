# HECTON-8 GAMEPLAY & SYSTEMS CENSUS REPORT
**STATUS**: SYSTEM AUDIT VERIFIED  
**DOCUMENT ID**: SYSTEMS_CENSUS.md  
**PROTOCOL**: Zero-Sycophancy, Facts-Only  
**ENVIRONMENT TARGET**: WINDOWS (COPPER WIRE PROOF LADDER)  

---

## 1. [MOVEMENT KERNEL AUDIT]

### Active Locomotion Engine Files
* Locomotion State Coordinator: [HectonPlayerMovement.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/HectonPlayerMovement.cs)
* Physics Bridge Component: [HectonPlayerMotor.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Gameplay/HectonPlayerMotor.cs)
* Speculative KCC Component: [HydrodynamicKccRuntime.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Physics/KCC/HydrodynamicKccRuntime.cs)

### Physical Movement Reality & Speculative KCC Verification
Locomotion in HECTON-8 utilizes a hybrid model with dual authority routes:
1. **Rigidbody Hybrid Mode (Standard Physics)**: Active when the custom KCC engine is inactive (`IsAuthorityRouteActive == false`). Standard forces are applied to the player's Unity `Rigidbody` component through the locomotion bridge:
   ```csharp
   // File: HectonPlayerMovement.cs
   _playerMotor?.ApplyForce(force);
   ```
2. **Kinematic Speculative KCC Mode (Burst-Optimized)**: Active when the `HydrodynamicKccRuntime` component is enabled and unmanaged `GlobalDataVault` buffers are successfully bound. Direct Rigidbody physics calculations are bypassed entirely. Position and sliding vector resolutions are simulated on parallel worker threads. The authority state check inside `HectonPlayerMotor.cs` determines this route:
   ```csharp
   public bool HydrodynamicKccOwnsCollisionAuthority => HydrodynamicKccOwnsCollision();
   private bool HydrodynamicKccOwnsCollision()
   {
       HydrodynamicKccRuntime runtime = _hydrodynamicKccRuntime;
       return runtime != null && runtime.IsAuthorityRouteActive;
   }
   ```
   Where KCC active status is defined as:
   ```csharp
   public bool IsAuthorityRouteActive => Application.isPlaying && isActiveAndEnabled && _dataVault != null;
   ```

### Math/Movement Vector Calculation (`SwimPhysics`)
The player's linear velocity and hydrodynamics drag coefficients are calculated inside `SwimPhysics` in `HectonPlayerMovement.cs`.

* **Signature**:
  ```csharp
  private void SwimPhysics(SuitData suit, float fixedDeltaTime, PlayerTransportPreset transportPreset)
  ```
* **Mathematical Kernel (First 20 Lines)**:
  ```csharp
  _velocity = ResolveAuthoritativeLinearVelocity(Vector3.zero);
  ApplyHeavyBrineSinkEffect();
  ApplyCriticalEncumbranceVerticalVelocityGate();

  float speedSq = _velocity.sqrMagnitude;
  bool isSurfaceSwim = _isSurfaceSwimming;
  bool hasSurfaceDiveIntent = isSurfaceSwim && HasCommittedSurfaceDive(transportPreset);
  float shoreSwimBlend = isSurfaceSwim ? _shoreBuoyancyBlend : 1f;
  float brineSwimSpeedMultiplier = _isInsideBrineLayer ? BrineLayerConstants.SwimSpeedMultiplier : 1f;
  float brineWaterDensityScale = _isInsideBrineLayer ? BrineLayerConstants.DensityMultiplier : 1f;

  // ─── Depth-based drag increase (v7.0) ───
  float effectiveDragCoeff = CalculateSwimEffectiveDragCoefficient(suit, isSurfaceSwim);

  _lastPlayerKinematicsDragCoefficient = effectiveDragCoeff;
  _lastPlayerKinematicsWaterDensityScale = brineWaterDensityScale;
  // Burst scalar water drag: presentation sells turbulence, authority stays replayable.
  ApplyBurstScalarWaterDrag(speedSq, effectiveDragCoeff, brineWaterDensityScale, fixedDeltaTime);

  // ─── Swim thrust ───
  float sargassumSpeedMultiplier = ResolveSargassumSpeedMultiplier();
  ```

### Execution Chain Check
The locomotion update sequence is driven by the `SystemDispatcher` phases and registers with `GlobalRegistry`:
1. **Startup Registration**:
   * `HectonPlayerMovement` registers its fixed update tick:
     ```csharp
     _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
     ```
   * `HydrodynamicKccRuntime` registers its simulation, resolution, and post-sync ticks:
     ```csharp
     _registeredFixedTick = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
     _registeredPostFixedTick = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
     _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
     ```
2. **FixedTick execution (SIMULATION phase)**:
   * `SystemDispatcher` calls `FixedTick` on `HectonPlayerMovement` to calculate linear speed, drag, and thrust force inputs.
   * `SystemDispatcher` calls `FixedTick` on `HydrodynamicKccRuntime` which pins unmanaged DataVault buffers and schedules parallel KCC simulation jobs:
     * `ClearKccInputBufferJob` (clears raw input states)
     * `SanitizeKccInputBufferJob` (bounds input commands to active sector)
     * `ApplyEnvironmentalForcesJob` (applies gravity, buoyancy, flow fields, and external drag)
     * `BuildSdfCollisionHitsJob` (checks speculative collision spheres against the SDF volume)
3. **PostFixedTick execution (POST_SIMULATION phase)**:
   * `SystemDispatcher` calls `PostFixedTick` on `HydrodynamicKccRuntime` to resolve collision overlap and update coordinates:
     * `EvaluateSlopeFrictionJob` (calculates slide multipliers on voxel surfaces)
     * `KinematicResolutionJob` (calculates final non-overlapping player position and pushes back from voxel walls)
     * `KinematicVisualSyncJob` (generates smoothed local coords for render interpolation)
     * `EmitWakeSignalsJob` (writes water turbulence wake packets directly to the unmanaged `SignalBus<WakeGeneratedSignal>`)
4. **Locomotion Pose Apply**:
   * During `PostFixedTick` in `HectonPlayerMotor.cs`, the visual pose is locked. It reads the final velocity from the determinism signal queues:
     ```csharp
     CoreDeterminismSignals.TryGetLatestKccVelocityVector(KccVelocityMotorMaxAgeFrames, out Vector3 kccVelocity);
     ```

---

## 2. [INVENTORY SOVEREIGNTY]

### Active Storage & Query Files
* Database Controller: [PlayerInventory.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/PlayerInventory.cs)
* Query Interface: [PlayerInventory_SoaQuery.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/PlayerInventory_SoaQuery.cs)
* Unmanaged Query Engine: [SoaInventoryQueryEngine.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Inventory/SoaInventoryQueryEngine.cs)
* Crafting validator: [CraftingSystem.FastFail.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/CraftingSystem.FastFail.cs)

### Storage Strategy & Unmanaged Vault Layout
Inventory slots are **not** heap-allocated objects in standard collections (`List<Item>`). All inventory slot data is stored in unmanaged `NativeArray<T>` lanes mapped inside the static `GlobalDataVault` heap. The `PlayerInventory` component manages these structures via `InventoryVaultLane<T>` wrappers:
```csharp
// File: PlayerInventory.cs
internal struct InventoryVaultLane<T> where T : struct
{
    private IDataVault _vault;
    private IDataVault _writeLockVault;
    private SystemID _owner;
    private int _expectedLength;
    private BufferID _expectedBufferId;

    public VaultGenerationHandle<T> Handle;
    // ...
}
```
Lanes initialized in `PlayerInventory` cover all physical properties of items in a flat structure:
```csharp
private InventoryVaultLane<uint> _itemHashes;
private InventoryVaultLane<ushort> _stackCounts;
private InventoryVaultLane<float> _itemCondition;
private InventoryVaultLane<float> _itemDurability;
private InventoryVaultLane<ushort> _craftLockedCounts;
private InventoryVaultLane<ushort> _anchorStateFlags;
private InventoryVaultLane<ushort> _itemStateFlags;
private InventoryVaultLane<byte> _itemGenetics;
private InventoryVaultLane<ushort> _qualityMilli;
private InventoryVaultLane<byte> _durabilities;
```

### Crafting Fast-Fail Validator
The Fast-Fail validator is fully active in [CraftingSystem.FastFail.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/CraftingSystem.FastFail.cs). It uses 64-bit coarse presence bitmasks to filter locked or missing ingredients in O(1) time before evaluating complex grid structures:
```csharp
// 1. Check blueprint unlock condition
if ((normalizedUnlockMask & recipe.BlueprintUnlockMask) == 0UL)
{
    failure = CraftingFastFailStatus.UnlockMissing;
    return false;
}

// 2. Coarse inventory presence validation
if ((CurrentInventoryMask & requirementMask) != requirementMask)
{
    failure = CraftingFastFailStatus.MaskMissing;
    return false;
}
```
Bitmask indices are mapped dynamically from unmanaged item hashes:
```csharp
public static ulong ResolveBit(uint itemHashId)
{
    return itemHashId == 0u ? 0UL : 1UL << (int)(itemHashId & 63);
}
```

### Save/Load Execution Chain
`PlayerInventory` implements `ISaveable` to participate in the boot/save sequence:
```csharp
public int SavePriority => 20;
public int LoadPriority => 20;
```
* **Save Process**:
  1. `SaveManager` calls `PopulateSaveData(SaveData data)` during serialization.
  2. The system checks if inventory status is dirty (`_isDirty`).
  3. It calls `RefreshInventoryShadowBufferFromRuntime()` to serialize the flat arrays to a compressed shadow byte buffer.
  4. It populates `data.inventory` DTO containing serialized slot arrays (`dto.itemHashIds`, `dto.stackCounts`).
* **Load Process**:
  1. `SaveManager` calls `LoadFromSaveData(SaveData data)` at load time.
  2. The system resets structural parameters (`_grid.Clear()`) and zeroes all native array lanes (`ClearNativeArray(...)`).
  3. It loops through `data.inventory.cellCount` and reconstructs placement details using:
     ```csharp
     _grid.PlaceAt(in descriptor, cellX, cellY);
     ```
  4. Flat native arrays are populated at `anchorIndex = AnchorIndex(cellX, cellY)` to restore items.

---

## 3. [SCATTER & INSTANCING REALITY]

### Active Files
* Renderer Coordinator: [GPUScatterDirector.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/GPUScatterDirector.cs)
* GPUI Submission Backend: [ScatterGPUIBackend.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/World/ScatterGPUIBackend.cs)
* Compute Culling Shader: [GpuScatterLodCull.compute](file:///C:/hades/Hecton8/Assets/_Project/Art/Shaders/GpuScatterLodCull.compute)
* Compute Distribution Shader: [Hecton_GpuScatter.compute](file:///C:/hades/Hecton8/Assets/_Project/Art/Shaders/Hecton_GpuScatter.compute)

### Seabed Detail & Ore Instancing Pipeline
HECTON-8 completely avoids spawning game objects or using standard `GameObject.Instantiate()` for decorative foliage, rocks, and mineable ore nodes. Everything is drawn via **GPU Instanced Indirect Rendering** using dynamic structured buffers:
```csharp
// File: GPUScatterDirector.cs / ScatterGPUIBackend.cs
UnityEngine.Graphics.RenderMeshIndirect(renderParams, scatterMesh, _argsBuffer, 1, 0);
```
Instance buffers are maintained on the GPU, and culling data is processed directly inside custom Compute shaders.

### Compute Shader Kernels & Threads
1. **`GpuScatterLodCull.compute`**: Handles frustum and distance culling on the GPU.
   * **Kernel**: `#pragma kernel ScatterCullJob`
   * **Thread Layout**: `[numthreads(64, 1, 1)]` (defined by `HECTON_SCATTER_THREADS_PER_GROUP 64`)
2. **`Hecton_GpuScatter.compute`**: Handles density calculation, terrain snapping, and compaction.
   * **Kernels**:
     * `#pragma kernel ClearScatterDensityBuffer`
     * `#pragma kernel GenerateScatterInstances`
     * `#pragma kernel CompactVisibleScatterInstances`
   * **Thread Layout**: `[numthreads(64, 1, 1)]` (defined by `HECTON_SCATTER_THREADS 64`)

### Drill Tools Interaction Mechanics
Drills resolve hits based on their category:
* **Handheld Tool ([SeafloorDrillTool.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/SeafloorDrillTool.cs))**: Uses standard Unity physics raycasts via the base `PlayerTool` wrapper (`RequestPrimarySurfaceHit`) on `HectonLayerMasks.FieldToolSurfaceLayerMask` and publishes a typed `InteractionSignal` to the hit collider:
  ```csharp
  interactionService.Publish(in signal, hit.collider);
  ```
* **Deployable Drill ([DeployableSdfDrillRuntime.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Gameplay/Mining/DeployableSdfDrillRuntime.cs))**: Bypasses Unity collider physics entirely to prevent raycast bottleneck issues. It queries the unmanaged voxel SDF model (`IVoxelSonarSdfReadModel`) using raymarching:
  ```csharp
  VoxelSonarSdfMath.TryResolveNearestSdfSurface(
      _voxelSdfReadModel,
      origin,
      direction,
      range,
      step,
      out VoxelSonarSdfRaycastHit hit)
  ```
  It then schedules direct grid modifications using `VoxelDeltaProcessor` to carve the voxel terrain.

---

## 4. [UI GC AUDIT]

### Active Files
* Suit HUD: [SuitHUDV4CanvasOverlay.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/UI/SuitHUDV4CanvasOverlay.cs)
* Formatting Ring: [LocNumericBuffer.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/LocNumericBuffer.cs)
* Formatter Core: [ZeroGCFormatter.cs](file:///C:/hades/Hecton8/Assets/_Project/Scripts/Core/ZeroGCFormatter.cs)

### HUD Formatting Allocation Pressure Audit
An audit of `SuitHUDV4CanvasOverlay.cs` verifies:
* **Zero GC Allocations**: There are **no** string updates, `string.Format`, interpolated string allocations (`$"..."`), or `.ToString()` operations on update paths.
* **Cached Layout Guards**: The script compares values (like `cachedTenths` or `cachedColor`) against previous values before rendering, skipping TMPro modifications entirely if no change is detected.

### Babel Protocol Zero-Allocation Formatting Proof
To update numerical values (oxygen levels, depths, battery charges) without GC allocations, the system formats localized templates directly into pre-allocated character buffers using `LocNumericBuffer` and updates TMPro using character spans:
```csharp
// File: SuitHUDV4CanvasOverlay.cs
SetLocalizedRtlState(label, rtl);
EnsureCharCapacity(ref displayBuffer, templateLength + 24);
FixedCharBuffer fixedBuffer = new FixedCharBuffer(displayBuffer);

// Formats number into character span without heap allocations
if (!fixedBuffer.TryWriteTemplateFloatTenths(templateBuffer.AsSpan(0, templateLength), roundedTenths, out int length))
    length = 0;

// Set TextMeshPro text directly via character array pointer
ApplyHudCharArray(label, fixedBuffer.Buffer, length);
```
Text assignment is bridged without string construction:
```csharp
private static void ApplyHudCharArray(TMP_Text label, char[] buffer, int length)
{
    if (label == null || buffer == null) return;
    int safeLength = math.clamp(length, 0, buffer.Length);
    label.SetCharArray(buffer, 0, safeLength); // Zero-alloc TMP bridge
}
```
Heap allocations are bypassed using pre-allocated ring buffers inside `LocNumericBuffer.cs`:
```csharp
// File: LocNumericBuffer.cs
private static readonly char[][] _stagingBufferRing = CreateStagingBufferRing();
private static int _stagingBufferCursor = -1;
```

---

## 5. [THE VERDICT]

* **MOVEMENT SYSTEM**: **PRODUCTION-READY**. Dual-locomotion setup is fully active. Character kinematics execute in the `SystemDispatcher` loop with speculative Burst-jobs solving KCC terrain collision.
* **INVENTORY SYSTEM**: **PRODUCTION-READY**. Flat unmanaged Structure-of-Arrays (SOA) arrays map directly to the `GlobalDataVault`. Fast-fail bitmask recipe checks are implemented in O(1). Save/load hooks execute via low-level byte-shadow serialization.
* **SCATTER & INSTANCING**: **PRODUCTION-READY**. Standard GameObject spawning is omitted. Seabed assets are instanced using unmanaged indirect draw calls (`RenderMeshIndirect`). Compute shaders handle culling on the GPU under `[numthreads(64, 1, 1)]`.
* **UI SYSTEM (BABEL)**: **PRODUCTION-READY**. String formatting overhead is eliminated. Pre-allocated static ring buffers and `ZeroGCFormatter` output character arrays directly to TextMeshPro via the `SetCharArray` API.
