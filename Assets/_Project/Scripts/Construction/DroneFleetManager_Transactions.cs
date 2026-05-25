using System;
using System.Buffers.Binary;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    internal static partial class DroneFleetManager
    {
        private const int DroneTransactionTelemetryCapacity = 300;
        private const int DroneTransactionMilliScale = 1000;
        private const float DroneTransactionMilliToUnits = 1f / DroneTransactionMilliScale;
        private const uint DroneRepairTaskTypeHash = 0x44525250u; // DRRP
        private const uint DroneMiningTaskTypeHash = 0x44524D4Eu; // DRMN
        private const uint DroneTransactionLayoutHash = 0x53333335u; // S335
        private const string DroneFleetShinobu335BlackBoxDumpPath = "Docs/AgentLogs/Dump_SHINOBU_335.bin";
        private const BufferID DroneFleetTransactionTasksBufferId = (BufferID)12873350;
        private const BufferID DroneFleetTransactionIntegrityBufferId = (BufferID)12873351;
        private const BufferID DroneFleetTransactionResultsBufferId = (BufferID)12873352;
        private const BufferID DroneFleetTransactionCountersBufferId = (BufferID)12873353;
        private const BufferID DroneFleetTransactionCommandConsumedBufferId = (BufferID)12873354;
        private const BufferID DroneFleetTransactionTelemetryBufferId = (BufferID)12873355;
        private const BufferID DroneFleetTransactionCommandsBufferId = (BufferID)12873356;
        private const BufferID DroneFleetTransactionAupSnapshotsBufferId = (BufferID)12873357;

        private static NativeArray<DroneTaskDTO> s_DroneTransactionTasks;
        private static NativeArray<DroneTransactionCommandDTO> s_DroneTransactionCommands;
        private static NativeArray<DroneTransactionAupSnapshotDTO> s_DroneTransactionAupSnapshots;
        private static NativeArray<DroneTransactionIntegrityDTO> s_DroneTransactionIntegrity;
        private static NativeArray<DroneTransactionResultDTO> s_DroneTransactionResults;
        private static NativeArray<DroneTransactionCounterDTO> s_DroneTransactionCounters;
        private static NativeArray<byte> s_DroneTransactionCommandConsumed;
        private static NativeArray<DroneTransactionTelemetryEntry> s_DroneTransactionTelemetry;
        private static VaultGenerationHandle<DroneTaskDTO> s_DroneTransactionTasksHandle;
        private static VaultGenerationHandle<DroneTransactionCommandDTO> s_DroneTransactionCommandsHandle;
        private static VaultGenerationHandle<DroneTransactionAupSnapshotDTO> s_DroneTransactionAupSnapshotsHandle;
        private static VaultGenerationHandle<DroneTransactionIntegrityDTO> s_DroneTransactionIntegrityHandle;
        private static VaultGenerationHandle<DroneTransactionResultDTO> s_DroneTransactionResultsHandle;
        private static VaultGenerationHandle<DroneTransactionCounterDTO> s_DroneTransactionCountersHandle;
        private static VaultGenerationHandle<byte> s_DroneTransactionCommandConsumedHandle;
        private static VaultGenerationHandle<DroneTransactionTelemetryEntry> s_DroneTransactionTelemetryHandle;
        private static bool s_DroneTransactionTasksVaultBacked;
        private static bool s_DroneTransactionCommandsVaultBacked;
        private static bool s_DroneTransactionAupSnapshotsVaultBacked;
        private static bool s_DroneTransactionIntegrityVaultBacked;
        private static bool s_DroneTransactionResultsVaultBacked;
        private static bool s_DroneTransactionCountersVaultBacked;
        private static bool s_DroneTransactionCommandConsumedVaultBacked;
        private static bool s_DroneTransactionTelemetryVaultBacked;
        private static JobHandle s_DroneTransactionJobHandle;
        private static bool s_DroneTransactionJobScheduled;
        private static bool s_DroneTransactionConsumedMaskCurrent;
        private static int s_DroneTransactionScheduledCommandCount;
        private static int s_DroneTransactionScheduledTransactionCount;
        private static uint s_DroneTransactionScheduledFrame;
        private static uint s_DroneTransactionLastTelemetryFrame;
        private static int s_DroneTransactionTelemetryCursor;
        private static int s_DroneTransactionDumpFrame;
        private static IDataVault s_DroneInventoryVault;
        private static InventorySoaVaultHandles s_DroneInventoryVaultHandles;
        private static bool s_DroneInventoryVaultHandlesBound;

        private static void AllocateDroneTransactionMemory()
        {
            ValidateDroneTransactionLayouts();
            s_DroneTransactionTasks = ResolveDroneTransactionVaultBuffer<DroneTaskDTO>(DroneFleetTransactionTasksBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionTasksHandle, out s_DroneTransactionTasksVaultBacked); // VAULT VIEW: NativeArray<DroneTaskDTO>[1536] - 32B atomic drone repair/mining task lane - owner: GlobalDataVault
            s_DroneTransactionCommands = ResolveDroneTransactionVaultBuffer<DroneTransactionCommandDTO>(DroneFleetTransactionCommandsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionCommandsHandle, out s_DroneTransactionCommandsVaultBacked); // VAULT VIEW: NativeArray<DroneTransactionCommandDTO>[1536] - command metadata snapshot to avoid service queue readback hazards - owner: GlobalDataVault
            s_DroneTransactionAupSnapshots = ResolveDroneTransactionVaultBuffer<DroneTransactionAupSnapshotDTO>(DroneFleetTransactionAupSnapshotsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionAupSnapshotsHandle, out s_DroneTransactionAupSnapshotsVaultBacked); // VAULT VIEW: NativeArray<DroneTransactionAupSnapshotDTO>[1536] - immutable AUP snapshots for transaction jobs - owner: GlobalDataVault
            s_DroneTransactionIntegrity = ResolveDroneTransactionVaultBuffer<DroneTransactionIntegrityDTO>(DroneFleetTransactionIntegrityBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionIntegrityHandle, out s_DroneTransactionIntegrityVaultBacked); // VAULT VIEW: NativeArray<DroneTransactionIntegrityDTO>[1536] - fixed-point Interlocked integrity staging - owner: GlobalDataVault
            s_DroneTransactionResults = ResolveDroneTransactionVaultBuffer<DroneTransactionResultDTO>(DroneFleetTransactionResultsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionResultsHandle, out s_DroneTransactionResultsVaultBacked); // VAULT VIEW: NativeArray<DroneTransactionResultDTO>[1536] - post-simulation transaction results - owner: GlobalDataVault
            s_DroneTransactionCounters = ResolveDroneTransactionVaultBuffer<DroneTransactionCounterDTO>(DroneFleetTransactionCountersBufferId, (int)DroneTransactionCounterSlot.Count, NativeArrayOptions.ClearMemory, ref s_DroneTransactionCountersHandle, out s_DroneTransactionCountersVaultBacked); // VAULT VIEW: NativeArray<DroneTransactionCounterDTO>[7] - 64B padded atomic counters - owner: GlobalDataVault
            s_DroneTransactionCommandConsumed = ResolveDroneTransactionVaultBuffer<byte>(DroneFleetTransactionCommandConsumedBufferId, DroneServiceCommandCapacity, NativeArrayOptions.ClearMemory, ref s_DroneTransactionCommandConsumedHandle, out s_DroneTransactionCommandConsumedVaultBacked); // VAULT VIEW: NativeArray<byte>[1536] - current-frame legacy service skip mask - owner: GlobalDataVault
            s_DroneTransactionTelemetry = ResolveDroneTransactionVaultBuffer<DroneTransactionTelemetryEntry>(DroneFleetTransactionTelemetryBufferId, DroneTransactionTelemetryCapacity, NativeArrayOptions.ClearMemory, ref s_DroneTransactionTelemetryHandle, out s_DroneTransactionTelemetryVaultBacked); // VAULT VIEW: NativeArray<DroneTransactionTelemetryEntry>[300] - SHINOBU_335 black box - owner: GlobalDataVault
            s_DroneTransactionLastTelemetryFrame = uint.MaxValue;
            TryBindDroneInventoryVaultHandles();
        }

        private static void ReleaseDroneTransactionMemory()
        {
            CompleteScheduledDroneServiceTransactionBatch(true);
            ReleaseDroneVaultBuffer(ref s_DroneTransactionTasks, ref s_DroneTransactionTasksHandle, ref s_DroneTransactionTasksVaultBacked, nameof(s_DroneTransactionTasks));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionCommands, ref s_DroneTransactionCommandsHandle, ref s_DroneTransactionCommandsVaultBacked, nameof(s_DroneTransactionCommands));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionAupSnapshots, ref s_DroneTransactionAupSnapshotsHandle, ref s_DroneTransactionAupSnapshotsVaultBacked, nameof(s_DroneTransactionAupSnapshots));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionIntegrity, ref s_DroneTransactionIntegrityHandle, ref s_DroneTransactionIntegrityVaultBacked, nameof(s_DroneTransactionIntegrity));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionResults, ref s_DroneTransactionResultsHandle, ref s_DroneTransactionResultsVaultBacked, nameof(s_DroneTransactionResults));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionCounters, ref s_DroneTransactionCountersHandle, ref s_DroneTransactionCountersVaultBacked, nameof(s_DroneTransactionCounters));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionCommandConsumed, ref s_DroneTransactionCommandConsumedHandle, ref s_DroneTransactionCommandConsumedVaultBacked, nameof(s_DroneTransactionCommandConsumed));
            ReleaseDroneVaultBuffer(ref s_DroneTransactionTelemetry, ref s_DroneTransactionTelemetryHandle, ref s_DroneTransactionTelemetryVaultBacked, nameof(s_DroneTransactionTelemetry));
            s_DroneInventoryVault = null;
            s_DroneInventoryVaultHandles = default;
            s_DroneInventoryVaultHandlesBound = false;
            s_DroneTransactionJobHandle = default;
            s_DroneTransactionJobScheduled = false;
            s_DroneTransactionConsumedMaskCurrent = false;
            s_DroneTransactionScheduledCommandCount = 0;
            s_DroneTransactionScheduledTransactionCount = 0;
            s_DroneTransactionScheduledFrame = 0u;
            s_DroneTransactionLastTelemetryFrame = uint.MaxValue;
            s_DroneTransactionTelemetryCursor = 0;
            s_DroneTransactionDumpFrame = 0;
        }

        private static void ValidateDroneTransactionLayouts()
        {
            if (UnsafeUtility.SizeOf<DroneTaskDTO>() == 32 &&
                UnsafeUtility.SizeOf<DroneTransactionCommandDTO>() == 64 &&
                UnsafeUtility.SizeOf<DroneTransactionAupSnapshotDTO>() == 64 &&
                UnsafeUtility.SizeOf<DroneTransactionIntegrityDTO>() == 32 &&
                UnsafeUtility.SizeOf<DroneTransactionResultDTO>() == 64 &&
                UnsafeUtility.SizeOf<DroneTransactionCounterDTO>() == 64 &&
                UnsafeUtility.SizeOf<DroneTransactionTelemetryEntry>() == 64)
            {
                return;
            }

            throw new InvalidOperationException("SHINOBU_335 drone transaction DTO ABI validation failed.");
        }

        private static NativeArray<T> ResolveDroneTransactionVaultBuffer<T>(
            BufferID bufferId,
            int length,
            NativeArrayOptions allocationNativeArrayOptions,
            ref VaultGenerationHandle<T> handle,
            out bool vaultBacked)
            where T : struct
        {
            vaultBacked = false;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || length <= 0)
                return default;

            if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out NativeArray<T> buffer))
            {
                vaultBacked = true;
                return buffer;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
            {
                handle = existingHandle;
                if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out buffer))
                {
                    vaultBacked = true;
                    return buffer;
                }
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                SystemID.Construction,
                allocationNativeArrayOptions);
            if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out buffer))
            {
                vaultBacked = true;
                return buffer;
            }

            handle = default;
            return default;
        }

        private static void ExecuteDroneServiceTransactionBatch(int commandCount)
        {
            if (commandCount <= 0 ||
                !s_DroneTransactionTasks.IsCreated ||
                !s_DroneTransactionCommands.IsCreated ||
                !s_DroneTransactionAupSnapshots.IsCreated ||
                !s_DroneTransactionIntegrity.IsCreated ||
                !s_DroneTransactionResults.IsCreated ||
                !s_DroneTransactionCounters.IsCreated ||
                !s_DroneTransactionCommandConsumed.IsCreated ||
                s_DroneTransactionJobScheduled)
            {
                return;
            }

            int safeCount = math.min(commandCount, math.min(DroneServiceCommandCapacity, s_DroneServiceCommands.Length));
            int transactionCount = PrepareDroneServiceTransactions(safeCount);
            if (transactionCount <= 0)
                return;

            ClearDroneTransactionCounters();
            EvaluateDroneTransactionsJob job = new EvaluateDroneTransactionsJob
            {
                Commands = s_DroneTransactionCommands,
                AupSnapshots = s_DroneTransactionAupSnapshots,
                Tasks = s_DroneTransactionTasks,
                Integrity = s_DroneTransactionIntegrity,
                Results = s_DroneTransactionResults,
                Counters = s_DroneTransactionCounters,
                TransactionCount = safeCount,
                RepairTaskHash = DroneRepairTaskTypeHash,
                MiningTaskHash = DroneMiningTaskTypeHash,
                GlobalQualityWeight = ResolveGlobalQualityWeight()
            };
            s_DroneTransactionJobHandle = job.Schedule(safeCount, DroneJobBatchSize);
            s_DroneTransactionJobScheduled = true;
            s_DroneTransactionConsumedMaskCurrent = true;
            s_DroneTransactionScheduledCommandCount = safeCount;
            s_DroneTransactionScheduledTransactionCount = transactionCount;
            s_DroneTransactionScheduledFrame = (uint)Mathf.Max(0, Time.frameCount);
        }

        private static bool CompleteScheduledDroneServiceTransactionBatch(bool force)
        {
            if (!s_DroneTransactionJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref s_DroneTransactionJobHandle, force))
                return false;

            s_DroneTransactionJobScheduled = false;
            TryResolveDroneInventoryTransactionBuffers(
                out InventorySoaVaultBuffers inventoryBuffers,
                out _);
            ApplyDroneTransactionResults(s_DroneTransactionScheduledCommandCount);
            RecordDroneTransactionTelemetry(
                s_DroneTransactionScheduledCommandCount,
                s_DroneTransactionScheduledTransactionCount,
                inventoryBuffers.ActiveSlotCount);
            s_DroneTransactionScheduledCommandCount = 0;
            s_DroneTransactionScheduledTransactionCount = 0;
            s_DroneTransactionScheduledFrame = 0u;
            return true;
        }

        private static void RecordDroneTransactionOwnerFrame(int commandCount)
        {
            if (s_DroneTransactionJobScheduled ||
                !s_DroneTransactionTelemetry.IsCreated ||
                !s_DroneTransactionCounters.IsCreated)
            {
                return;
            }

            uint frame = (uint)Mathf.Max(0, Time.frameCount);
            if (s_DroneTransactionLastTelemetryFrame == frame)
                return;

            ClearDroneTransactionCounters();
            NativeArray<int> activeSlotCount = default;
            if (TryResolveDroneInventoryTransactionBuffers(
                    out InventorySoaVaultBuffers inventoryBuffers,
                    out _))
            {
                activeSlotCount = inventoryBuffers.ActiveSlotCount;
            }

            RecordDroneTransactionTelemetry(
                Mathf.Max(0, commandCount),
                0,
                activeSlotCount);
        }

        private static int PrepareDroneServiceTransactions(int commandCount)
        {
            int prepared = 0;
            for (int i = 0; i < commandCount; i++)
            {
                s_DroneTransactionCommandConsumed[i] = 0;
                s_DroneTransactionCommands[i] = default;
                s_DroneTransactionAupSnapshots[i] = default;
                s_DroneTransactionTasks[i] = default;
                s_DroneTransactionIntegrity[i] = default;
                s_DroneTransactionResults[i] = default;
            }

            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                DroneServiceCommand command = s_DroneServiceCommands[commandIndex];
                int slot = command.Slot;
                if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds == null ||
                    s_DroneTaskKindsBySlot == null ||
                    s_DroneSlotDroneIds[slot] != command.DroneId ||
                    command.Kind != (byte)DroneServiceCommandKind.Repair)
                {
                    continue;
                }

                if (!s_DroneStates.IsCreated ||
                    (uint)slot >= (uint)s_DroneStates.Length)
                {
                    continue;
                }

                HeadlessDroneState currentDrone = s_DroneStates[slot];
                if (currentDrone.DroneId != command.DroneId ||
                    currentDrone.State != (byte)HeadlessDroneRuntimeState.Repair)
                {
                    continue;
                }

                DroneFleetTaskKind kind = s_DroneTaskKindsBySlot[slot];
                bool accepted = kind == DroneFleetTaskKind.MineNode
                    ? PrepareMiningTransaction(commandIndex, in command)
                    : kind == DroneFleetTaskKind.RepairModule && PrepareRepairTransaction(commandIndex, in command);

                if (!accepted)
                    continue;

                WriteDroneTransactionAupSnapshot(commandIndex, slot);
                s_DroneTransactionCommands[commandIndex] = new DroneTransactionCommandDTO
                {
                    Slot = command.Slot,
                    DroneId = command.DroneId,
                    CommandIndex = commandIndex,
                    DeltaTime = Mathf.Max(0f, command.DeltaTime),
                    TaskTypeHash = s_DroneTransactionTasks[commandIndex].TaskTypeHash,
                    TargetEntityHash = s_DroneTransactionTasks[commandIndex].TargetEntityHash,
                    Flags = DroneTransactionCommandDTO.FlagValid,
                    Frame = (uint)Mathf.Max(0, Time.frameCount),
                    Position = command.Position,
                    TargetPosition = command.TargetPosition,
                    StateHash = math.hash(new uint4(
                        (uint)Mathf.Max(0, command.Slot),
                        (uint)Mathf.Max(0, command.DroneId),
                        s_DroneTransactionTasks[commandIndex].TargetEntityHash,
                        s_DroneTransactionTasks[commandIndex].TaskTypeHash))
                };
                s_DroneTransactionCommandConsumed[commandIndex] = 1;
                prepared++;
            }

            return prepared;
        }

        private static void WriteDroneTransactionAupSnapshot(int commandIndex, int slot)
        {
            if (!s_DroneTransactionAupSnapshots.IsCreated ||
                (uint)commandIndex >= (uint)s_DroneTransactionAupSnapshots.Length)
            {
                return;
            }

            DroneTransactionAupSnapshotDTO snapshot = default;
            if (s_DroneStateDtos.IsCreated &&
                s_DroneTargetDtos.IsCreated &&
                s_DroneStates.IsCreated &&
                s_DroneTransactionTasks.IsCreated &&
                (uint)slot < (uint)s_DroneStateDtos.Length &&
                (uint)slot < (uint)s_DroneTargetDtos.Length &&
                (uint)slot < (uint)s_DroneStates.Length &&
                (uint)commandIndex < (uint)s_DroneTransactionTasks.Length)
            {
                DroneStateDTO state = s_DroneStateDtos[slot];
                DroneTargetDTO target = s_DroneTargetDtos[slot];
                HeadlessDroneState ownerState = s_DroneStates[slot];
                DroneTaskDTO task = s_DroneTransactionTasks[commandIndex];
                DroneFleetTaskKind expectedKind = ResolveDroneTransactionKind(task.TaskTypeHash);
                uint resolvedTargetHash = ResolveDroneSnapshotTargetHash(in target, expectedKind);
                bool targetAupZero = target.TargetAUP.x == 0d &&
                                     target.TargetAUP.y == 0d &&
                                     target.TargetAUP.z == 0d;
                bool finite = math.all(math.isfinite(state.CurrentAUP)) &&
                              math.all(math.isfinite(target.TargetAUP)) &&
                              math.isfinite(target.Radius);
                bool targetOwnsTask = expectedKind != DroneFleetTaskKind.None &&
                                      target.TaskKind == (uint)expectedKind &&
                                      s_DroneTaskKindsBySlot != null &&
                                      s_DroneTaskKindsBySlot[slot] == expectedKind;
                bool ownerOwnsTask = ownerState.DroneId > 0 &&
                                     ownerState.State == (byte)HeadlessDroneRuntimeState.Repair &&
                                     ownerState.TargetTaskIndex != EmptyTaskIndex &&
                                     ownerState.TargetTaskIndex == target.TaskIndex;
                bool targetMatchesTask = resolvedTargetHash != 0u &&
                                         resolvedTargetHash == task.TargetEntityHash;
                snapshot.CurrentAUP = state.CurrentAUP;
                snapshot.TargetAUP = target.TargetAUP;
                snapshot.Radius = math.max(0.1f, math.isfinite(target.Radius) ? target.Radius : 0f);
                snapshot.TargetEntityHash = resolvedTargetHash;
                snapshot.Flags = finite &&
                                 (target.Flags & 1u) != 0u &&
                                 !targetAupZero &&
                                 targetOwnsTask &&
                                 ownerOwnsTask &&
                                 targetMatchesTask
                    ? DroneTransactionAupSnapshotDTO.FlagValid
                    : 0u;
            }

            s_DroneTransactionAupSnapshots[commandIndex] = snapshot;
        }

        private static DroneFleetTaskKind ResolveDroneTransactionKind(uint taskTypeHash)
        {
            if (taskTypeHash == DroneMiningTaskTypeHash)
                return DroneFleetTaskKind.MineNode;

            if (taskTypeHash == DroneRepairTaskTypeHash)
                return DroneFleetTaskKind.RepairModule;

            return DroneFleetTaskKind.None;
        }

        private static uint ResolveDroneSnapshotTargetHash(in DroneTargetDTO target, DroneFleetTaskKind expectedKind)
        {
            if (expectedKind == DroneFleetTaskKind.RepairModule)
                return target.TargetModuleId > 0 ? (uint)target.TargetModuleId : 0u;

            if (expectedKind == DroneFleetTaskKind.MineNode)
            {
                int sourceId = target.TargetModuleId != 0
                    ? target.TargetModuleId
                    : unchecked((int)math.hash(target.LocalPosition));
                return (uint)Mathf.Max(1, sourceId);
            }

            return 0u;
        }

        private static bool PrepareRepairTransaction(int commandIndex, in DroneServiceCommand command)
        {
            int slot = command.Slot;
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
                return false;

            HeadlessDroneState drone = s_DroneStates[slot];
            if (drone.SolderUnits <= 0)
                return false;

            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            float currentIntegrity = Mathf.Clamp(target.CurrentIntegrity, 0f, recoverableIntegrity);
            if (currentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
            {
                s_DroneTransactionTasks[commandIndex] = new DroneTaskDTO
                {
                    TargetEntityHash = (uint)Mathf.Max(1, GetRuntimeId(target)),
                    TaskTypeHash = DroneRepairTaskTypeHash,
                    TaskProgress01 = 1f,
                    TaskEfficiencyScalar = 1f,
                    InventoryPayloadHash = 0u
                };
                s_DroneTransactionIntegrity[commandIndex] = new DroneTransactionIntegrityDTO
                {
                    TargetEntityHash = (uint)Mathf.Max(1, GetRuntimeId(target)),
                    CurrentIntegrityMilli = ToIntegrityMilli(currentIntegrity),
                    MaxRecoverableIntegrityMilli = ToIntegrityMilli(recoverableIntegrity),
                    RepairBudgetMilli = 0,
                    Flags = 1u,
                    CommandIndex = commandIndex,
                    Slot = slot
                };
                return true;
            }

            int targetHash = Mathf.Max(1, GetRuntimeId(target));
            int repairBudgetMilli = ToIntegrityMilli(Mathf.Max(0f, drone.RepairRatePerSecond * command.DeltaTime));
            if (repairBudgetMilli <= 0)
                return false;

            s_DroneTransactionTasks[commandIndex] = new DroneTaskDTO
            {
                TargetEntityHash = (uint)targetHash,
                TaskTypeHash = DroneRepairTaskTypeHash,
                TaskProgress01 = 0f,
                TaskEfficiencyScalar = 1f,
                InventoryPayloadHash = 0u
            };
            s_DroneTransactionIntegrity[commandIndex] = new DroneTransactionIntegrityDTO
            {
                TargetEntityHash = (uint)targetHash,
                CurrentIntegrityMilli = ToIntegrityMilli(currentIntegrity),
                MaxRecoverableIntegrityMilli = ToIntegrityMilli(recoverableIntegrity),
                RepairBudgetMilli = repairBudgetMilli,
                Flags = 1u,
                CommandIndex = commandIndex,
                Slot = slot
            };
            return true;
        }

        private static bool PrepareMiningTransaction(int commandIndex, in DroneServiceCommand command)
        {
            int slot = command.Slot;
            if ((uint)slot >= (uint)HeadlessDroneCapacity)
                return false;

            HeadlessDroneState drone = s_DroneStates[slot];
            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            DroneChassisSpecDTO chassis = ResolveLaunchDroneChassisSpec(DroneFleetTaskKind.MineNode, in tuning);
            float holdSeconds = Mathf.Max(0.01f, chassis.MiningHoldSeconds);
            float progress = Mathf.Clamp01(drone.RepairAccumulator / holdSeconds);
            int sourceId = drone.TargetModuleId != 0
                ? drone.TargetModuleId
                : unchecked((int)math.hash(drone.TargetPosition));
            uint targetHash = (uint)Mathf.Max(1, sourceId);
            s_DroneTransactionTasks[commandIndex] = new DroneTaskDTO
            {
                TargetEntityHash = targetHash,
                TaskTypeHash = DroneMiningTaskTypeHash,
                TaskProgress01 = progress,
                TaskEfficiencyScalar = math.rcp(holdSeconds),
                InventoryPayloadHash = unchecked((uint)DroneInventoryCopperHash)
            };
            s_DroneTransactionIntegrity[commandIndex] = new DroneTransactionIntegrityDTO
            {
                TargetEntityHash = targetHash,
                CurrentIntegrityMilli = 0,
                MaxRecoverableIntegrityMilli = 0,
                RepairBudgetMilli = 0,
                Flags = 2u,
                CommandIndex = commandIndex,
                Slot = slot
            };
            return true;
        }

        private static void ApplyDroneTransactionResults(int commandCount)
        {
            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                DroneTransactionResultDTO result = s_DroneTransactionResults[commandIndex];
                if (result.TaskTypeHash == 0u)
                    continue;

                int slot = result.Slot;
                if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds == null ||
                    s_DroneSlotDroneIds[slot] != result.DroneId)
                {
                    continue;
                }

                HeadlessDroneState drone = s_DroneStates[slot];
                if (drone.State != (byte)HeadlessDroneRuntimeState.Repair ||
                    s_DroneTaskKindsBySlot == null)
                {
                    continue;
                }

                DroneFleetTaskKind currentKind = s_DroneTaskKindsBySlot[slot];
                if (result.TaskTypeHash == DroneRepairTaskTypeHash &&
                    currentKind == DroneFleetTaskKind.RepairModule)
                {
                    ApplyRepairTransactionResult(slot, ref drone, in result);
                }
                else if (result.TaskTypeHash == DroneMiningTaskTypeHash &&
                         currentKind == DroneFleetTaskKind.MineNode)
                {
                    ApplyMiningTransactionResult(slot, ref drone, in result);
                }
                else
                {
                    continue;
                }

                s_DroneStates[slot] = drone;
                MirrorDroneSoA(slot, in drone);
            }
        }

        private static bool IsDroneServiceTransactionCommandConsumed(int commandIndex)
        {
            return s_DroneTransactionConsumedMaskCurrent &&
                   s_DroneTransactionCommandConsumed.IsCreated &&
                   (uint)commandIndex < (uint)s_DroneTransactionCommandConsumed.Length &&
                   s_DroneTransactionCommandConsumed[commandIndex] != 0;
        }

        private static bool ShouldDeferDroneServiceWhileTransactionPending(in DroneServiceCommand command)
        {
            if (!s_DroneTransactionJobScheduled ||
                command.Kind != (byte)DroneServiceCommandKind.Repair ||
                s_DroneSlotDroneIds == null)
            {
                return false;
            }

            int slot = command.Slot;
            if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                command.DroneId <= 0 ||
                s_DroneSlotDroneIds[slot] != command.DroneId)
            {
                return false;
            }

            DroneFleetTaskKind kind = s_DroneTaskKindsBySlot[slot];
            return kind == DroneFleetTaskKind.RepairModule ||
                   kind == DroneFleetTaskKind.MineNode;
        }

        private static void ApplyRepairTransactionResult(int slot, ref HeadlessDroneState drone, in DroneTransactionResultDTO result)
        {
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
            {
                ReturnDroneToHub(ref drone);
                return;
            }

            uint currentTargetHash = (uint)Mathf.Max(1, GetRuntimeId(target));
            if (currentTargetHash != result.TargetEntityHash)
                return;

            if ((result.Flags & DroneTransactionResultDTO.FlagNoop) != 0u)
            {
                if ((result.Flags & DroneTransactionResultDTO.FlagCompleted) != 0u)
                    ReturnDroneToHub(ref drone);
                return;
            }

            if ((result.Flags & DroneTransactionResultDTO.FlagRepairApplied) == 0u || result.RepairAppliedMilli <= 0)
                return;

            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            float currentIntegrity = Mathf.Clamp(target.CurrentIntegrity, 0f, recoverableIntegrity);
            float requestedRepair = result.RepairAppliedMilli * DroneTransactionMilliToUnits;
            float appliedRepair = Mathf.Min(requestedRepair, Mathf.Max(0f, recoverableIntegrity - currentIntegrity));
            if (appliedRepair <= 0f)
            {
                if (currentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
                    ReturnDroneToHub(ref drone);
                return;
            }

            if (target is Hecton8.Interaction.IRepairableModuleTarget repairableTarget)
                repairableTarget.ApplyRepair(appliedRepair);
            else
                target.CurrentIntegrity = currentIntegrity + appliedRepair;

            PublishHullRepairedByDrone(slot, in drone, target, appliedRepair);
            if ((result.Flags & DroneTransactionResultDTO.FlagVfxSpark) != 0u)
                DispatchRepairWeld(slot, in drone, target);

            ConsumeSolderByWork(ref drone, appliedRepair, SolderIntegrityUnitsPerBundle);
            if (target.CurrentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
                ReturnDroneToHub(ref drone);
        }

        private static void ApplyMiningTransactionResult(
            int slot,
            ref HeadlessDroneState drone,
            in DroneTransactionResultDTO result)
        {
            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            DroneChassisSpecDTO chassis = ResolveLaunchDroneChassisSpec(DroneFleetTaskKind.MineNode, in tuning);
            float holdSeconds = Mathf.Max(0.01f, chassis.MiningHoldSeconds);
            int sourceId = drone.TargetModuleId != 0
                ? drone.TargetModuleId
                : unchecked((int)math.hash(drone.TargetPosition));
            uint currentTargetHash = (uint)Mathf.Max(1, sourceId);
            if (currentTargetHash != result.TargetEntityHash)
                return;

            if ((result.Flags & DroneTransactionResultDTO.FlagInventoryAdded) == 0u)
            {
                float currentProgress = Mathf.Clamp01(drone.TransactionProgress);
                float resultProgress = Mathf.Clamp01(result.Progress01);
                if (resultProgress > currentProgress)
                {
                    drone.TransactionProgress = resultProgress;
                    drone.RepairAccumulator = resultProgress * holdSeconds;
                }
                return;
            }

            drone.TransactionProgress = Mathf.Clamp01(result.Progress01);
            drone.RepairAccumulator = drone.TransactionProgress * holdSeconds;

            int quantity = Mathf.Max(1, result.InventoryQuantityAdded);
            PublishDroneMiningItemAcquiredSignal(in drone, result.InventoryHash, quantity, sourceId);

            DroneFleetInventoryTransactionSignal transactionSignal = new DroneFleetInventoryTransactionSignal
            {
                DroneId = drone.DroneId,
                SourceId = sourceId,
                DestinationId = drone.HubGridId,
                ItemHash = unchecked((int)result.InventoryHash),
                Quantity = quantity,
                Position = drone.Position,
                Flags = 2u,
                Reserved0 = result.ActiveInventorySlots
            };
            SignalBus<DroneFleetInventoryTransactionSignal>.TryPush(in transactionSignal);
            drone.RepairAccumulator = 0f;
            drone.TransactionProgress = 1f;
            ReturnDroneToHub(ref drone);
        }

        private static void PublishDroneMiningItemAcquiredSignal(
            in HeadlessDroneState drone,
            uint itemHash,
            int quantity,
            int sourceId)
        {
            if (itemHash == 0u || quantity <= 0)
                return;

            AbsoluteUniversePosition positionAup = IsFiniteDouble3(drone.PositionAup)
                ? AbsoluteUniversePosition.FromAbsolutePosition(drone.PositionAup)
                : default;
            ItemAcquiredSignal signal = new ItemAcquiredSignal
            {
                PositionAup = positionAup,
                ItemHash = itemHash,
                OreHash = (uint)Mathf.Max(0, sourceId),
                Quantity = (ushort)Mathf.Clamp(quantity, 1, ushort.MaxValue),
                SourceKind = ItemAcquiredSignalSourceKinds.DroneMining,
                Flags = 0,
                Frame = (uint)Mathf.Max(0, Time.frameCount)
            };
            SignalBus<ItemAcquiredSignal>.TryPush(in signal);
        }

        private static int ResolveInventoryActiveSlotCount(NativeArray<int> activeSlotCount)
        {
            if (!activeSlotCount.IsCreated || activeSlotCount.Length <= 0)
                return 0;

            return Mathf.Max(0, activeSlotCount[0]);
        }

        private static void ClearDroneTransactionCounters()
        {
            if (!s_DroneTransactionCounters.IsCreated)
                return;

            for (int i = 0; i < s_DroneTransactionCounters.Length; i++)
                s_DroneTransactionCounters[i] = default;
        }

        private static bool TryBindDroneInventoryVaultHandles()
        {
            if (s_DroneInventoryVaultHandlesBound)
                return true;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null || !SoaInventoryQueryEngine.RuntimeLayoutValid())
                return false;

            InventorySoaVaultHandles handles = default;
            if (!TryBindDroneInventoryLane(vault, BufferID.ShinobuInventoryActiveSlotCount, 1, out handles.ActiveSlotCount))
                return false;

            s_DroneInventoryVaultHandles = handles;
            s_DroneInventoryVaultHandlesBound = s_DroneInventoryVaultHandles.ActiveSlotCount.Handle.Generation != 0u;
            s_DroneInventoryVault = s_DroneInventoryVaultHandlesBound ? vault : null;
            return s_DroneInventoryVaultHandlesBound;
        }

        private static bool TryBindDroneInventoryLane<T>(
            IDataVault vault,
            BufferID bufferId,
            int length,
            out InventorySoaVaultLane<T> lane)
            where T : struct
        {
            lane = default;
            if (vault == null ||
                length <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle))
            {
                return false;
            }

            uint expectedBufferId = unchecked((uint)(int)bufferId);
            if (handle.BufferID != expectedBufferId || handle.Generation == 0u)
                return false;

            lane = new InventorySoaVaultLane<T>
            {
                Handle = handle,
                ExpectedBufferID = expectedBufferId,
                Length = length
            };
            return true;
        }

        private static bool TryResolveDroneInventoryTransactionBuffers(
            out InventorySoaVaultBuffers buffers,
            out NativeArray<uint> quantities)
        {
            buffers = default;
            quantities = default;
            if (!s_DroneInventoryVaultHandlesBound ||
                s_DroneInventoryVault == null ||
                !SoaInventoryQueryEngine.RuntimeLayoutValid())
            {
                return false;
            }

            if (!s_DroneInventoryVault.TryReadHandle(in s_DroneInventoryVaultHandles.ActiveSlotCount.Handle, out NativeArray<int> activeSlotCount) ||
                !activeSlotCount.IsCreated)
            {
                return false;
            }

            buffers.ActiveSlotCount = activeSlotCount;
            return true;
        }

        private static int ToIntegrityMilli(float value)
        {
            if (!math.isfinite(value))
                return 0;

            return Mathf.Clamp(Mathf.RoundToInt(value * DroneTransactionMilliScale), 0, int.MaxValue);
        }

        private static void RecordDroneTransactionTelemetry(
            int commandCount,
            int transactionCount,
            NativeArray<int> activeSlotCount)
        {
            if (!s_DroneTransactionTelemetry.IsCreated || !s_DroneTransactionCounters.IsCreated)
                return;

            int index = s_DroneTransactionTelemetryCursor;
            if ((uint)index >= (uint)s_DroneTransactionTelemetry.Length)
                index = 0;

            int repairCount = ReadDroneTransactionCounter(DroneTransactionCounterSlot.RepairCount);
            int miningCount = ReadDroneTransactionCounter(DroneTransactionCounterSlot.MiningCount);
            int inventoryAdds = ReadDroneTransactionCounter(DroneTransactionCounterSlot.InventoryAdds);
            int conflicts = ReadDroneTransactionCounter(DroneTransactionCounterSlot.AtomicConflicts);
            int vfxSignals = ReadDroneTransactionCounter(DroneTransactionCounterSlot.VfxSignals);
            int faults = ReadDroneTransactionCounter(DroneTransactionCounterSlot.Faults);
            uint stateHash = ComputeDroneTransactionStateHash(commandCount);
            float quality = ResolveGlobalQualityWeight();
            uint telemetryFrame = s_DroneTransactionScheduledFrame != 0u
                ? s_DroneTransactionScheduledFrame
                : (uint)Mathf.Max(0, Time.frameCount);
            s_DroneTransactionTelemetry[index] = new DroneTransactionTelemetryEntry
            {
                Frame = telemetryFrame,
                StateHash = stateHash,
                TransactionCount = transactionCount,
                RepairCount = repairCount,
                MiningCount = miningCount,
                InventoryAdds = inventoryAdds,
                AtomicConflicts = conflicts,
                VfxSignals = vfxSignals,
                GlobalQualityWeight = quality,
                EstimatedMicroseconds = EstimateDroneTransactionMicroseconds(transactionCount, quality),
                FaultFlags = faults > 0 ? DroneTransactionResultDTO.FlagNaNFault : 0u,
                LastTargetHash = ResolveLastDroneTransactionTargetHash(commandCount),
                ActiveInventorySlots = ResolveInventoryActiveSlotCount(activeSlotCount),
                CommandCount = commandCount,
                LayoutHash = DroneTransactionLayoutHash
            };
            s_DroneTransactionTelemetryCursor = (index + 1) % s_DroneTransactionTelemetry.Length;
            s_DroneTransactionLastTelemetryFrame = telemetryFrame;
            if (faults > 0)
                DumpDroneTransactionBlackBoxOncePerFrame();
        }

        private static int ReadDroneTransactionCounter(DroneTransactionCounterSlot slot)
        {
            return s_DroneTransactionCounters.IsCreated && (uint)slot < (uint)s_DroneTransactionCounters.Length
                ? s_DroneTransactionCounters[(int)slot].Value
                : 0;
        }

        private static uint ComputeDroneTransactionStateHash(int commandCount)
        {
            uint hash = 2166136261u;
            int count = math.min(commandCount, s_DroneTransactionResults.IsCreated ? s_DroneTransactionResults.Length : 0);
            for (int i = 0; i < count; i++)
            {
                DroneTransactionResultDTO result = s_DroneTransactionResults[i];
                if (result.TaskTypeHash == 0u)
                    continue;

                hash = (hash ^ result.TargetEntityHash) * 16777619u;
                hash = (hash ^ result.TaskTypeHash) * 16777619u;
                hash = (hash ^ (uint)math.max(0, result.DroneId)) * 16777619u;
                hash = (hash ^ result.Flags) * 16777619u;
            }

            return hash;
        }

        private static uint ResolveLastDroneTransactionTargetHash(int commandCount)
        {
            int count = math.min(commandCount, s_DroneTransactionResults.IsCreated ? s_DroneTransactionResults.Length : 0);
            for (int i = count - 1; i >= 0; i--)
            {
                uint hash = s_DroneTransactionResults[i].TargetEntityHash;
                if (hash != 0u)
                    return hash;
            }

            return 0u;
        }

        private static float EstimateDroneTransactionMicroseconds(int transactionCount, float quality)
        {
            float safeQuality = math.saturate(math.isfinite(quality) ? quality : 0f);
            float baseCost = transactionCount * 0.045f;
            float visualCost = transactionCount * math.lerp(0.005f, 0.035f, safeQuality);
            return baseCost + visualCost;
        }

        internal static bool TryGetLatestDroneTransactionTelemetry(out DroneTransactionTelemetrySnapshot snapshot)
        {
            snapshot = default;
            if (!s_DroneTransactionTelemetry.IsCreated || s_DroneTransactionTelemetry.Length <= 0)
                return false;

            int cursor = s_DroneTransactionTelemetryCursor - 1;
            if (cursor < 0)
                cursor = s_DroneTransactionTelemetry.Length - 1;

            DroneTransactionTelemetryEntry entry = s_DroneTransactionTelemetry[cursor];
            if (entry.LayoutHash == 0u)
                return false;

            snapshot = ToDroneTransactionSnapshot(in entry);
            return true;
        }

        internal static int CopyDroneTransactionTelemetry(DroneTransactionTelemetrySnapshot[] buffer)
        {
            if (buffer == null || buffer.Length <= 0 || !s_DroneTransactionTelemetry.IsCreated)
                return 0;

            int capacity = math.min(buffer.Length, s_DroneTransactionTelemetry.Length);
            int count = 0;
            int cursor = s_DroneTransactionTelemetryCursor;
            for (int i = 0; i < s_DroneTransactionTelemetry.Length && count < capacity; i++)
            {
                int sourceIndex = (cursor + i) % s_DroneTransactionTelemetry.Length;
                DroneTransactionTelemetryEntry entry = s_DroneTransactionTelemetry[sourceIndex];
                if (entry.LayoutHash == 0u)
                    continue;

                buffer[count++] = ToDroneTransactionSnapshot(in entry);
            }

            return count;
        }

        internal static int CopyDroneTransactionDebugTasks(DroneTransactionDebugTask[] buffer)
        {
            if (buffer == null ||
                buffer.Length <= 0 ||
                s_DroneTransactionJobScheduled ||
                !s_DroneTransactionTasks.IsCreated ||
                !s_DroneTransactionCommands.IsCreated ||
                !s_DroneTransactionResults.IsCreated ||
                !s_DroneTransactionCommandConsumed.IsCreated ||
                s_DroneSlotDroneIds == null ||
                !s_DroneStates.IsCreated)
            {
                return 0;
            }

            int count = 0;
            int limit = math.min(s_DroneTransactionTasks.Length, s_DroneTransactionCommandConsumed.Length);
            for (int commandIndex = 0; commandIndex < limit && count < buffer.Length; commandIndex++)
            {
                if (s_DroneTransactionCommandConsumed[commandIndex] == 0)
                    continue;

                DroneTransactionResultDTO result = commandIndex < s_DroneTransactionResults.Length
                    ? s_DroneTransactionResults[commandIndex]
                    : default;
                DroneTransactionCommandDTO command = commandIndex < s_DroneTransactionCommands.Length
                    ? s_DroneTransactionCommands[commandIndex]
                    : default;
                DroneTaskDTO task = s_DroneTransactionTasks[commandIndex];
                int slot = result.Slot;
                if (slot < 0)
                    slot = command.Slot;
                if ((uint)slot >= (uint)HeadlessDroneCapacity || s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = s_DroneStates[slot];
                buffer[count++] = new DroneTransactionDebugTask
                {
                    Position = drone.Position,
                    Target = drone.TargetPosition,
                    Velocity = drone.Velocity,
                    TargetEntityHash = task.TargetEntityHash,
                    TaskTypeHash = task.TaskTypeHash,
                    Progress01 = result.TaskTypeHash != 0u ? result.Progress01 : task.TaskProgress01,
                    VfxIntensity01 = result.VfxIntensity01,
                    DroneId = drone.DroneId,
                    Slot = slot,
                    InventorySlot = result.InventorySlot,
                    InventoryHash = result.InventoryHash,
                    InventoryQuantityAdded = result.InventoryQuantityAdded,
                    Flags = result.Flags,
                    ActiveInventorySlots = result.ActiveInventorySlots,
                    AtomicConflicts = result.AtomicConflicts,
                    BatteryPercent = drone.BatteryPercent,
                    StateFlags = (uint)drone.State
                };
            }

            return count;
        }

        private static DroneTransactionTelemetrySnapshot ToDroneTransactionSnapshot(in DroneTransactionTelemetryEntry entry)
        {
            return new DroneTransactionTelemetrySnapshot
            {
                Frame = entry.Frame,
                StateHash = entry.StateHash,
                TransactionCount = entry.TransactionCount,
                RepairCount = entry.RepairCount,
                MiningCount = entry.MiningCount,
                InventoryAdds = entry.InventoryAdds,
                AtomicConflicts = entry.AtomicConflicts,
                VfxSignals = entry.VfxSignals,
                GlobalQualityWeight = entry.GlobalQualityWeight,
                EstimatedMicroseconds = entry.EstimatedMicroseconds,
                FaultFlags = entry.FaultFlags,
                LastTargetHash = entry.LastTargetHash,
                ActiveInventorySlots = entry.ActiveInventorySlots,
                CommandCount = entry.CommandCount,
                LayoutHash = entry.LayoutHash
            };
        }

        private static void DumpDroneTransactionBlackBoxOncePerFrame()
        {
            int frame = Time.frameCount;
            if (s_DroneTransactionDumpFrame == frame)
                return;

            s_DroneTransactionDumpFrame = frame;
            TryWriteDroneTransactionBlackBoxFile();
        }

        private static void TryWriteDroneTransactionBlackBoxFile()
        {
            if (!s_DroneTransactionTelemetry.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DroneFleetShinobu335BlackBoxDumpPath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> header = stackalloc byte[8];
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(0, 4), DroneTransactionTelemetryCapacity);
                BinaryPrimitives.WriteInt32LittleEndian(header.Slice(4, 4), s_DroneTransactionTelemetryCursor);
                stream.Write(header);
                unsafe
                {
                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(s_DroneTransactionTelemetry);
                    int byteCount = s_DroneTransactionTelemetry.Length * UnsafeUtility.SizeOf<DroneTransactionTelemetryEntry>();
                    stream.Write(new ReadOnlySpan<byte>(telemetryPtr, byteCount));
                }
            }
            catch (Exception)
            {
            }
        }
    }

    public static partial class DroneFleetAutomationFacade
    {
        public static bool TryGetLatestTransactionTelemetry(out DroneTransactionTelemetrySnapshot snapshot)
        {
            return DroneFleetManager.TryGetLatestDroneTransactionTelemetry(out snapshot);
        }

        public static int CopyTransactionTelemetry(DroneTransactionTelemetrySnapshot[] buffer)
        {
            return DroneFleetManager.CopyDroneTransactionTelemetry(buffer);
        }

        public static int CopyTransactionDebugTasks(DroneTransactionDebugTask[] buffer)
        {
            return DroneFleetManager.CopyDroneTransactionDebugTasks(buffer);
        }
    }
}
