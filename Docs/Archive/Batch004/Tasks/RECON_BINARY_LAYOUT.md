# RECON_BINARY_LAYOUT

Agent: BINARY_LAYOUT_SENTINEL
Status: PENDING VERIFICATION

## Scope

Command source: `rg` and PowerShell scans against:

- `Assets/_Project/Scripts/SaveSystem`
- `Assets/_Project/Scripts/Core`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`
- `Assets/_Project/Scripts/World/AbsoluteUniversePositionBlit.cs`

Requested path `Assets/_Project/Scripts/World/Persistent` does not exist in this workspace. Live persistent world DTOs are in `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs`.

## Structs Missing `[StructLayout]`

- `Assets/_Project/Scripts/SaveSystem/SteamCloudSaveConflictResolver.cs:137` - `SteamCloudSaveCandidate`
- `Assets/_Project/Scripts/SaveSystem/SteamCloudSaveConflictResolver.cs:149` - `SteamCloudSaveResolution`
- `Assets/_Project/Scripts/Core/BurstCallback.cs:12` - `BurstCallback`
- `Assets/_Project/Scripts/Core/BurstCallback.cs:33` - `BurstCallbackQueue`
- `Assets/_Project/Scripts/Core/BurstCallback.cs:204` - `ParallelEventWriter`
- `Assets/_Project/Scripts/Core/FixedCharBuffer.cs:10` - `FixedCharBuffer`
- `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:63` - `ImportanceScoringJob` - fixed in this pass.
- `Assets/_Project/Scripts/Core/FoveatedSimulationManager.cs:109` - `VisualInterpolationJob` - fixed in this pass.
- `Assets/_Project/Scripts/Core/GameStartContext.cs:60` - `GameStartContext`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:295` - `ForceOverrideToken`
- `Assets/_Project/Scripts/Core/GlobalRegistry.cs:357` - `RegistryReboundReferenceSlot`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:172` - `DamagePacket`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2242` - `HectonHardwareProfile`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2333` - `ServiceReboundEvent`
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs:2769` - `EcosystemSectorPopulationSample`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs:2008` - `SpscSignalRingBuffer`
- `Assets/_Project/Scripts/Core/GlobalSignals.cs:2936` - `CombatDamageSignalAupShiftTransformer`
- `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs:839` - `ArenaAllocation`
- `Assets/_Project/Scripts/Core/JobFenceManager.cs:11` - `JobFenceManager`
- `Assets/_Project/Scripts/Core/MemoryBudgetTracker.cs:12` - `BudgetRecord`
- `Assets/_Project/Scripts/Core/NativeArenaArray.cs:15` - `NativeArenaArray`
- `Assets/_Project/Scripts/Core/NativeQuery.cs:14` - `NativeQuery`
- `Assets/_Project/Scripts/Core/NativeQuery.cs:39` - `NativeSelectQuery`
- `Assets/_Project/Scripts/Core/NativeRingBuffer.cs:12` - `NativeRingBuffer`
- `Assets/_Project/Scripts/Core/PowerGridRuntimeService.cs:8` - `BatteryRuntimeSnapshot`
- `Assets/_Project/Scripts/Core/RenderSettingsLifecycleGuard.cs:17` - `RenderSettingsSnapshot`
- `Assets/_Project/Scripts/Core/StackQueue.cs:9` - `StackQueue`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:33` - `CriticalMemoryPressureEvent`
- `Assets/_Project/Scripts/Core/SystemDispatcher.cs:2746` - `RenderSettingsSnapshot`
- `Assets/_Project/Scripts/Core/ThreadSafeCommandQueue.cs:31` - `EntityCommand`
- `Assets/_Project/Scripts/Core/UnsafeArenaAllocator.cs:8` - `ArenaBlock`
- `Assets/_Project/Scripts/Core/Contracts/CoreContractsAssemblyMarker.cs:6` - `CoreContractsAssemblyMarker`
- `Assets/_Project/Scripts/Core/Data/InventoryCost.cs:6` - `InventoryCost`
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:214` - `ResourceNodeTombstoneRecord` - fixed in this pass.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:404` - `TombstoneDecayCollectJob` - fixed in this pass.
- `Assets/_Project/Scripts/World/PersistentWorldRegistry.cs:465` - `SectorOverrideWriteResult` - contains managed strings; left out of binary-blit annotations.

## Sequential Layouts Missing Explicit `Pack` Or `Size`

High-risk binary or job records fixed in this pass:

- `PersistentWorldRegistry.EntityDataRecord` - already `Pack = 16, Size = 64`; now marked `[BinaryBlittableSafe]`.
- `PersistentWorldRegistry.AbsoluteUniversePosition` - already explicit size 48; now marked `[BinaryBlittableSafe]`.
- `PersistentWorldRegistry.AbsoluteUniversePositionBlit128` - already explicit size 48; now marked `[BinaryBlittableSafe]`.
- `SaveDeltaCompression.StrictSaveFileHeader64` - already `Pack = 1, Size = 64`; now marked `[BinaryBlittableSafe]`.
- `SaveDeltaCompression.SaveChunkHeader32` - already `Pack = 1, Size = 32`; now marked `[BinaryBlittableSafe]`.
- `SaveDeltaCompression.SaveVoxelDeltaRun5` - added exact `Pack = 1, Size = 5`.
- `VRSomaticProvider.VRSomaticBlackBoxEntry` - forced to `Pack = 4, Size = 64`.
- `SubmarineStructuralGrid.DamageControlTelemetryEntry` - already `Pack = 4, Size = 32`.

Remaining offenders are not all safe to annotate blindly. Several are managed, private state, non-persistence snapshots, or broad core contracts requiring owner review.
