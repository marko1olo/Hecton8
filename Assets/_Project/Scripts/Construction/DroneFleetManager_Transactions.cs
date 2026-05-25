using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
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
        private static int s_x001DroneFleetManagerTransactionsSignalPushDropCount;
        private const int DroneTransactionTelemetryCapacity = 300;
        private const int DroneTransactionMilliScale = 1000;
        private const float DroneTransactionMilliToUnits = 1f / DroneTransactionMilliScale;
        private const uint DroneRepairTaskTypeHash = 0x44525250u; // DRRP
        private const uint DroneMiningTaskTypeHash = 0x44524D4Eu; // DRMN
        private const uint DroneTransactionLayoutHash = 0x53333335u; // S335
        private const string DroneFleetTransactionBlackBoxDumpPath = "Docs/AgentLogs/Dump_1306_Construction.bin";
        private const BufferID DroneFleetTransactionTasksBufferId = (BufferID)72046;
        private const BufferID DroneFleetTransactionIntegrityBufferId = (BufferID)72047;
        private const BufferID DroneFleetTransactionResultsBufferId = (BufferID)72048;
        private const BufferID DroneFleetTransactionCountersBufferId = (BufferID)72049;
        private const BufferID DroneFleetTransactionCommandConsumedBufferId = (BufferID)72050;
        private const BufferID DroneFleetTransactionTelemetryBufferId = (BufferID)72051;
        private const BufferID DroneFleetTransactionCommandsBufferId = (BufferID)72052;
        private const BufferID DroneFleetTransactionAupSnapshotsBufferId = (BufferID)72053;

        private static VaultGenerationHandle<DroneTaskDTO> s_DroneTransactionTasksHandle;
        private static VaultGenerationHandle<DroneTransactionCommandDTO> s_DroneTransactionCommandsHandle;
        private static VaultGenerationHandle<DroneTransactionAupSnapshotDTO> s_DroneTransactionAupSnapshotsHandle;
        private static VaultGenerationHandle<DroneTransactionIntegrityDTO> s_DroneTransactionIntegrityHandle;
        private static VaultGenerationHandle<DroneTransactionResultDTO> s_DroneTransactionResultsHandle;
        private static VaultGenerationHandle<DroneTransactionCounterDTO> s_DroneTransactionCountersHandle;
        private static VaultGenerationHandle<byte> s_DroneTransactionCommandConsumedHandle;
        private static VaultGenerationHandle<DroneTransactionTelemetryEntry> s_DroneTransactionTelemetryHandle;
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
            if (!ValidateDroneTransactionLayouts())
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                return;
            }

            EnsureDroneTransactionVaultBuffer<DroneTaskDTO>(vault, DroneFleetTransactionTasksBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionTasksHandle);
            EnsureDroneTransactionVaultBuffer<DroneTransactionCommandDTO>(vault, DroneFleetTransactionCommandsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionCommandsHandle);
            EnsureDroneTransactionVaultBuffer<DroneTransactionAupSnapshotDTO>(vault, DroneFleetTransactionAupSnapshotsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionAupSnapshotsHandle);
            EnsureDroneTransactionVaultBuffer<DroneTransactionIntegrityDTO>(vault, DroneFleetTransactionIntegrityBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionIntegrityHandle);
            EnsureDroneTransactionVaultBuffer<DroneTransactionResultDTO>(vault, DroneFleetTransactionResultsBufferId, DroneServiceCommandCapacity, NativeArrayOptions.UninitializedMemory, ref s_DroneTransactionResultsHandle);
            EnsureDroneTransactionVaultBuffer<DroneTransactionCounterDTO>(vault, DroneFleetTransactionCountersBufferId, (int)DroneTransactionCounterSlot.Count, NativeArrayOptions.ClearMemory, ref s_DroneTransactionCountersHandle);
            EnsureDroneTransactionVaultBuffer<byte>(vault, DroneFleetTransactionCommandConsumedBufferId, DroneServiceCommandCapacity, NativeArrayOptions.ClearMemory, ref s_DroneTransactionCommandConsumedHandle);
            EnsureDroneTransactionVaultBuffer<DroneTransactionTelemetryEntry>(vault, DroneFleetTransactionTelemetryBufferId, DroneTransactionTelemetryCapacity, NativeArrayOptions.ClearMemory, ref s_DroneTransactionTelemetryHandle);
            if (!DroneTransactionHandlesValid())
                return;

            s_DroneTransactionLastTelemetryFrame = uint.MaxValue;
            TryBindDroneInventoryVaultHandles();
        }

        private static void ReleaseDroneTransactionMemory()
        {
            CompleteScheduledDroneServiceTransactionBatch(true);
            IDataVault vault = GlobalRegistry.DataVault;
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionTasksHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionCommandsHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionAupSnapshotsHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionIntegrityHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionResultsHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionCountersHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionCommandConsumedHandle);
            ReleaseDroneTransactionVaultHandle(vault, ref s_DroneTransactionTelemetryHandle);
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

        private static bool ValidateDroneTransactionLayouts()
        {
            return UnsafeUtility.SizeOf<DroneTaskDTO>() == 32 &&
                   UnsafeUtility.SizeOf<DroneTransactionCommandDTO>() == 64 &&
                   UnsafeUtility.SizeOf<DroneTransactionAupSnapshotDTO>() == 64 &&
                   UnsafeUtility.SizeOf<DroneTransactionIntegrityDTO>() == 32 &&
                   UnsafeUtility.SizeOf<DroneTransactionResultDTO>() == 64 &&
                   UnsafeUtility.SizeOf<DroneTransactionCounterDTO>() == 64 &&
                   UnsafeUtility.SizeOf<DroneTransactionTelemetryEntry>() == 64 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.TargetEntityHash)) == 0 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.CurrentIntegrityMilli)) == 4 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.MaxRecoverableIntegrityMilli)) == 8 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.RepairBudgetMilli)) == 12 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.Flags)) == 16 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.CommandIndex)) == 20 &&
                   OffsetOf<DroneTransactionIntegrityDTO>(nameof(DroneTransactionIntegrityDTO.Slot)) == 24 &&
                   OffsetOf<DroneTransactionIntegrityDTO>("_pad0") == 28 &&
                   OffsetOf<DroneTransactionIntegrityDTO>("_pad3") == 31 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.Slot)) == 0 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.DroneId)) == 4 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.TargetEntityHash)) == 8 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.TaskTypeHash)) == 12 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.PreviousIntegrityMilli)) == 16 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.NextIntegrityMilli)) == 20 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.RepairAppliedMilli)) == 24 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.InventorySlot)) == 28 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.InventoryHash)) == 32 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.InventoryQuantityAdded)) == 36 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.Progress01)) == 40 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.VfxIntensity01)) == 44 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.Flags)) == 48 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.ActiveInventorySlots)) == 52 &&
                   OffsetOf<DroneTransactionResultDTO>(nameof(DroneTransactionResultDTO.AtomicConflicts)) == 56 &&
                   OffsetOf<DroneTransactionResultDTO>("_pad0") == 60 &&
                   OffsetOf<DroneTransactionResultDTO>("_pad3") == 63 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.Slot)) == 0 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.DroneId)) == 4 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.CommandIndex)) == 8 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.DeltaTime)) == 12 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.TaskTypeHash)) == 16 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.TargetEntityHash)) == 20 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.Flags)) == 24 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.Frame)) == 28 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.Position)) == 32 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.TargetPosition)) == 44 &&
                   OffsetOf<DroneTransactionCommandDTO>(nameof(DroneTransactionCommandDTO.StateHash)) == 56 &&
                   OffsetOf<DroneTransactionCommandDTO>("_pad0") == 60 &&
                   OffsetOf<DroneTransactionCommandDTO>("_pad3") == 63 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>(nameof(DroneTransactionAupSnapshotDTO.CurrentAUP)) == 0 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>(nameof(DroneTransactionAupSnapshotDTO.TargetAUP)) == 24 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>(nameof(DroneTransactionAupSnapshotDTO.Radius)) == 48 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>(nameof(DroneTransactionAupSnapshotDTO.Flags)) == 52 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>(nameof(DroneTransactionAupSnapshotDTO.TargetEntityHash)) == 56 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>("_pad0") == 60 &&
                   OffsetOf<DroneTransactionAupSnapshotDTO>("_pad3") == 63 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.Frame)) == 0 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.StateHash)) == 4 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.TransactionCount)) == 8 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.RepairCount)) == 12 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.MiningCount)) == 16 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.InventoryAdds)) == 20 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.AtomicConflicts)) == 24 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.VfxSignals)) == 28 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.GlobalQualityWeight)) == 32 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.EstimatedMicroseconds)) == 36 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.FaultFlags)) == 40 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.LastTargetHash)) == 44 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.ActiveInventorySlots)) == 48 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.CommandCount)) == 52 &&
                   OffsetOf<DroneTransactionTelemetryEntry>(nameof(DroneTransactionTelemetryEntry.LayoutHash)) == 56 &&
                   OffsetOf<DroneTransactionTelemetryEntry>("_pad0") == 60 &&
                   OffsetOf<DroneTransactionTelemetryEntry>("_pad3") == 63 &&
                   OffsetOf<DroneTransactionCounterDTO>(nameof(DroneTransactionCounterDTO.Value)) == 0 &&
                   OffsetOf<DroneTransactionCounterDTO>("_pad0") == 4 &&
                   OffsetOf<DroneTransactionCounterDTO>("_pad59") == 63;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        private static bool DroneTransactionHandlesValid()
        {
            return DroneTransactionHandleValid(in s_DroneTransactionTasksHandle, DroneFleetTransactionTasksBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionCommandsHandle, DroneFleetTransactionCommandsBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionAupSnapshotsHandle, DroneFleetTransactionAupSnapshotsBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionIntegrityHandle, DroneFleetTransactionIntegrityBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionResultsHandle, DroneFleetTransactionResultsBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionCountersHandle, DroneFleetTransactionCountersBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionCommandConsumedHandle, DroneFleetTransactionCommandConsumedBufferId) &&
                   DroneTransactionHandleValid(in s_DroneTransactionTelemetryHandle, DroneFleetTransactionTelemetryBufferId);
        }

        private static bool DroneTransactionHandleValid<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   handle.Generation != 0u;
        }

        private static void EnsureDroneTransactionVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int length,
            NativeArrayOptions allocationNativeArrayOptions,
            ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault == null || length <= 0)
            {
                handle = default;
                return;
            }

            if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out _))
            {
                return;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
            {
                handle = existingHandle;
                if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out _))
                {
                    return;
                }
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                length,
                SystemID.Construction,
                allocationNativeArrayOptions);
            if (TryOpenDroneVaultBuffer(vault, in handle, bufferId, length, out _))
            {
                return;
            }

            handle = default;
        }

        private static void ReleaseDroneTransactionVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryAcquireDroneTransactionWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   requiredLength > 0 &&
                   DroneTransactionHandleValid(in handle, bufferId) &&
                   vault.TryAcquireWriteLock(in handle, SystemID.Construction, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadDroneTransactionBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   DroneTransactionHandleValid(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadDroneTransactionMutableBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   DroneTransactionHandleValid(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryAcquireDroneTransactionWriteBuffers(
            out IDataVault vault,
            out NativeArray<DroneTaskDTO> tasks,
            out NativeArray<DroneTransactionCommandDTO> commands,
            out NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots,
            out NativeArray<DroneTransactionIntegrityDTO> integrity,
            out NativeArray<DroneTransactionResultDTO> results,
            out NativeArray<DroneTransactionCounterDTO> counters,
            out NativeArray<byte> commandConsumed,
            out NativeArray<DroneTransactionTelemetryEntry> telemetry)
        {
            vault = GlobalRegistry.DataVault;
            tasks = default;
            commands = default;
            aupSnapshots = default;
            integrity = default;
            results = default;
            counters = default;
            commandConsumed = default;
            telemetry = default;
            if (vault == null || !DroneTransactionHandlesValid())
                return false;

            if (!TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionTasksHandle, DroneFleetTransactionTasksBufferId, DroneServiceCommandCapacity, out tasks) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionCommandsHandle, DroneFleetTransactionCommandsBufferId, DroneServiceCommandCapacity, out commands) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionAupSnapshotsHandle, DroneFleetTransactionAupSnapshotsBufferId, DroneServiceCommandCapacity, out aupSnapshots) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionIntegrityHandle, DroneFleetTransactionIntegrityBufferId, DroneServiceCommandCapacity, out integrity) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionResultsHandle, DroneFleetTransactionResultsBufferId, DroneServiceCommandCapacity, out results) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionCountersHandle, DroneFleetTransactionCountersBufferId, (int)DroneTransactionCounterSlot.Count, out counters) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionCommandConsumedHandle, DroneFleetTransactionCommandConsumedBufferId, DroneServiceCommandCapacity, out commandConsumed) ||
                !TryAcquireDroneTransactionWriteBuffer(vault, in s_DroneTransactionTelemetryHandle, DroneFleetTransactionTelemetryBufferId, DroneTransactionTelemetryCapacity, out telemetry))
            {
                ReleaseDroneTransactionWriteBuffers(
                    vault,
                    tasks,
                    commands,
                    aupSnapshots,
                    integrity,
                    results,
                    counters,
                    commandConsumed,
                    telemetry);
                tasks = default;
                commands = default;
                aupSnapshots = default;
                integrity = default;
                results = default;
                counters = default;
                commandConsumed = default;
                telemetry = default;
                return false;
            }

            return true;
        }

        private static void ReleaseDroneTransactionWriteBuffers(
            IDataVault vault,
            NativeArray<DroneTaskDTO> tasks,
            NativeArray<DroneTransactionCommandDTO> commands,
            NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots,
            NativeArray<DroneTransactionIntegrityDTO> integrity,
            NativeArray<DroneTransactionResultDTO> results,
            NativeArray<DroneTransactionCounterDTO> counters,
            NativeArray<byte> commandConsumed,
            NativeArray<DroneTransactionTelemetryEntry> telemetry)
        {
            if (vault == null)
                return;

            if (telemetry.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionTelemetryHandle, SystemID.Construction);
            if (commandConsumed.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionCommandConsumedHandle, SystemID.Construction);
            if (counters.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionCountersHandle, SystemID.Construction);
            if (results.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionResultsHandle, SystemID.Construction);
            if (integrity.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionIntegrityHandle, SystemID.Construction);
            if (aupSnapshots.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionAupSnapshotsHandle, SystemID.Construction);
            if (commands.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionCommandsHandle, SystemID.Construction);
            if (tasks.IsCreated)
                vault.ReleaseWriteLock(in s_DroneTransactionTasksHandle, SystemID.Construction);
        }

        private static void ExecuteDroneServiceTransactionBatch(
            int commandCount,
            NativeArray<DroneServiceCommand> serviceCommands)
        {
            if (commandCount <= 0 ||
                !serviceCommands.IsCreated ||
                s_DroneTransactionJobScheduled)
            {
                return;
            }

            if (!TryAcquireDroneTransactionWriteBuffers(
                    out IDataVault vault,
                    out NativeArray<DroneTaskDTO> tasks,
                    out NativeArray<DroneTransactionCommandDTO> commands,
                    out NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots,
                    out NativeArray<DroneTransactionIntegrityDTO> integrity,
                    out NativeArray<DroneTransactionResultDTO> results,
                    out NativeArray<DroneTransactionCounterDTO> counters,
                    out NativeArray<byte> commandConsumed,
                    out NativeArray<DroneTransactionTelemetryEntry> telemetry))
            {
                return;
            }

            int safeCount = math.min(commandCount, math.min(DroneServiceCommandCapacity, serviceCommands.Length));
            int transactionCount = 0;
            try
            {
                transactionCount = PrepareDroneServiceTransactions(
                    safeCount,
                    serviceCommands,
                    tasks,
                    commands,
                    aupSnapshots,
                    integrity,
                    results,
                    commandConsumed);
                if (transactionCount <= 0)
                    return;

                ClearDroneTransactionCounters(counters);
                EvaluateDroneTransactionsJob job = default;
                job.Commands = commands;
                job.AupSnapshots = aupSnapshots;
                job.Tasks = tasks;
                job.Integrity = integrity;
                job.Results = results;
                job.Counters = counters;
                job.TransactionCount = safeCount;
                job.RepairTaskHash = DroneRepairTaskTypeHash;
                job.MiningTaskHash = DroneMiningTaskTypeHash;
                job.GlobalQualityWeight = ResolveGlobalQualityWeight();
                s_DroneTransactionJobHandle = job.Schedule(safeCount, DroneJobBatchSize);
                s_DroneTransactionJobScheduled = true;
                s_DroneTransactionConsumedMaskCurrent = true;
                s_DroneTransactionScheduledCommandCount = safeCount;
                s_DroneTransactionScheduledTransactionCount = transactionCount;
                s_DroneTransactionScheduledFrame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            }
            finally
            {
                ReleaseDroneTransactionWriteBuffers(
                    vault,
                    tasks,
                    commands,
                    aupSnapshots,
                    integrity,
                    results,
                    counters,
                    commandConsumed,
                    telemetry);
            }
        }

        private static bool CompleteScheduledDroneServiceTransactionBatch(bool force)
        {
            if (!s_DroneTransactionJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref s_DroneTransactionJobHandle, force))
                return false;

            s_DroneTransactionJobScheduled = false;
            if (!TryAcquireDroneTransactionWriteBuffers(
                    out IDataVault vault,
                    out NativeArray<DroneTaskDTO> tasks,
                    out NativeArray<DroneTransactionCommandDTO> commands,
                    out NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots,
                    out NativeArray<DroneTransactionIntegrityDTO> integrity,
                    out NativeArray<DroneTransactionResultDTO> results,
                    out NativeArray<DroneTransactionCounterDTO> counters,
                    out NativeArray<byte> commandConsumed,
                    out NativeArray<DroneTransactionTelemetryEntry> telemetry))
            {
                s_DroneTransactionScheduledCommandCount = 0;
                s_DroneTransactionScheduledTransactionCount = 0;
                s_DroneTransactionScheduledFrame = 0u;
                return true;
            }

            TryResolveDroneInventoryTransactionBuffers(
                out InventorySoaVaultBuffers inventoryBuffers,
                out _);
            try
            {
                ApplyDroneTransactionResults(s_DroneTransactionScheduledCommandCount, results);
                RecordDroneTransactionTelemetry(
                    s_DroneTransactionScheduledCommandCount,
                    s_DroneTransactionScheduledTransactionCount,
                    inventoryBuffers.ActiveSlotCount,
                    results,
                    counters,
                    telemetry);
            }
            finally
            {
                ReleaseDroneTransactionWriteBuffers(
                    vault,
                    tasks,
                    commands,
                    aupSnapshots,
                    integrity,
                    results,
                    counters,
                    commandConsumed,
                    telemetry);
            }

            s_DroneTransactionScheduledCommandCount = 0;
            s_DroneTransactionScheduledTransactionCount = 0;
            s_DroneTransactionScheduledFrame = 0u;
            return true;
        }

        private static void RecordDroneTransactionOwnerFrame(int commandCount)
        {
            if (s_DroneTransactionJobScheduled)
            {
                return;
            }

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            if (s_DroneTransactionLastTelemetryFrame == frame)
                return;

            if (!TryAcquireDroneTransactionWriteBuffers(
                    out IDataVault vault,
                    out NativeArray<DroneTaskDTO> tasks,
                    out NativeArray<DroneTransactionCommandDTO> commands,
                    out NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots,
                    out NativeArray<DroneTransactionIntegrityDTO> integrity,
                    out NativeArray<DroneTransactionResultDTO> results,
                    out NativeArray<DroneTransactionCounterDTO> counters,
                    out NativeArray<byte> commandConsumed,
                    out NativeArray<DroneTransactionTelemetryEntry> telemetry))
            {
                return;
            }

            NativeArray<int> activeSlotCount = default;
            try
            {
                ClearDroneTransactionCounters(counters);
                if (TryResolveDroneInventoryTransactionBuffers(
                        out InventorySoaVaultBuffers inventoryBuffers,
                        out _))
                {
                    activeSlotCount = inventoryBuffers.ActiveSlotCount;
                }

                RecordDroneTransactionTelemetry(
                    Mathf.Max(0, commandCount),
                    0,
                    activeSlotCount,
                    results,
                    counters,
                    telemetry);
            }
            finally
            {
                ReleaseDroneTransactionWriteBuffers(
                    vault,
                    tasks,
                    commands,
                    aupSnapshots,
                    integrity,
                    results,
                    counters,
                    commandConsumed,
                    telemetry);
            }
        }

        private static int PrepareDroneServiceTransactions(
            int commandCount,
            NativeArray<DroneServiceCommand> serviceCommands,
            NativeArray<DroneTaskDTO> tasks,
            NativeArray<DroneTransactionCommandDTO> commands,
            NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots,
            NativeArray<DroneTransactionIntegrityDTO> integrity,
            NativeArray<DroneTransactionResultDTO> results,
            NativeArray<byte> commandConsumed)
        {
            int prepared = 0;
            for (int i = 0; i < commandCount; i++)
            {
                commandConsumed[i] = 0;
                commands[i] = default;
                aupSnapshots[i] = default;
                tasks[i] = default;
                integrity[i] = default;
                results[i] = default;
            }

            if (!TryOpenDroneCoreBuffers(
                    out NativeArray<HeadlessDroneState> droneStates,
                    out _,
                    out _,
                    out _) ||
                !TryOpenDroneMirrorBuffers(
                    out _,
                    out _,
                    out NativeArray<DroneStateDTO> droneStateDtos,
                    out NativeArray<DroneTargetDTO> droneTargetDtos))
            {
                return 0;
            }

            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                DroneServiceCommand command = serviceCommands[commandIndex];
                int slot = command.Slot;
                if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                    s_DroneSlotDroneIds == null ||
                    s_DroneTaskKindsBySlot == null ||
                    s_DroneSlotDroneIds[slot] != command.DroneId ||
                    command.Kind != (byte)DroneServiceCommandKind.Repair)
                {
                    continue;
                }

                if (!droneStates.IsCreated ||
                    (uint)slot >= (uint)droneStates.Length)
                {
                    continue;
                }

                HeadlessDroneState currentDrone = droneStates[slot];
                if (currentDrone.DroneId != command.DroneId ||
                    currentDrone.State != (byte)HeadlessDroneRuntimeState.Repair)
                {
                    continue;
                }

                DroneFleetTaskKind kind = s_DroneTaskKindsBySlot[slot];
                bool accepted = kind == DroneFleetTaskKind.MineNode
                    ? PrepareMiningTransaction(commandIndex, in command, droneStates, tasks, integrity)
                    : kind == DroneFleetTaskKind.RepairModule && PrepareRepairTransaction(commandIndex, in command, droneStates, tasks, integrity);

                if (!accepted)
                    continue;

                WriteDroneTransactionAupSnapshot(commandIndex, slot, droneStates, droneStateDtos, droneTargetDtos, tasks, aupSnapshots);
                DroneTaskDTO task = tasks[commandIndex];
                DroneTransactionCommandDTO transactionCommand = default;
                transactionCommand.Slot = command.Slot;
                transactionCommand.DroneId = command.DroneId;
                transactionCommand.CommandIndex = commandIndex;
                transactionCommand.DeltaTime = Mathf.Max(0f, command.DeltaTime);
                transactionCommand.TaskTypeHash = task.TaskTypeHash;
                transactionCommand.TargetEntityHash = task.TargetEntityHash;
                transactionCommand.Flags = DroneTransactionCommandDTO.FlagValid;
                transactionCommand.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
                transactionCommand.Position = command.Position;
                transactionCommand.TargetPosition = command.TargetPosition;
                transactionCommand.StateHash = math.hash(math.uint4(
                    (uint)Mathf.Max(0, command.Slot),
                    (uint)Mathf.Max(0, command.DroneId),
                    task.TargetEntityHash,
                    task.TaskTypeHash));
                commands[commandIndex] = transactionCommand;
                commandConsumed[commandIndex] = 1;
                prepared++;
            }

            return prepared;
        }

        private static void WriteDroneTransactionAupSnapshot(
            int commandIndex,
            int slot,
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<DroneStateDTO> droneStateDtos,
            NativeArray<DroneTargetDTO> droneTargetDtos,
            NativeArray<DroneTaskDTO> tasks,
            NativeArray<DroneTransactionAupSnapshotDTO> aupSnapshots)
        {
            if (!aupSnapshots.IsCreated ||
                (uint)commandIndex >= (uint)aupSnapshots.Length)
            {
                return;
            }

            DroneTransactionAupSnapshotDTO snapshot = default;
            if (droneStateDtos.IsCreated &&
                droneTargetDtos.IsCreated &&
                droneStates.IsCreated &&
                tasks.IsCreated &&
                (uint)slot < (uint)droneStateDtos.Length &&
                (uint)slot < (uint)droneTargetDtos.Length &&
                (uint)slot < (uint)droneStates.Length &&
                (uint)commandIndex < (uint)tasks.Length)
            {
                DroneStateDTO state = droneStateDtos[slot];
                DroneTargetDTO target = droneTargetDtos[slot];
                HeadlessDroneState ownerState = droneStates[slot];
                DroneTaskDTO task = tasks[commandIndex];
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

            aupSnapshots[commandIndex] = snapshot;
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

        private static bool PrepareRepairTransaction(
            int commandIndex,
            in DroneServiceCommand command,
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<DroneTaskDTO> tasks,
            NativeArray<DroneTransactionIntegrityDTO> integrity)
        {
            int slot = command.Slot;
            BaseModule target = s_TargetModulesByDroneSlot[slot];
            if (target == null)
                return false;

            HeadlessDroneState drone = droneStates[slot];
            if (drone.SolderUnits <= 0)
                return false;

            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            float currentIntegrity = Mathf.Clamp(target.CurrentIntegrity, 0f, recoverableIntegrity);
            if (currentIntegrity >= recoverableIntegrity && !target.IsFlooded && !target.HasCascadeFailure)
            {
                DroneTaskDTO completeTask = default;
                completeTask.TargetEntityHash = (uint)Mathf.Max(1, GetRuntimeId(target));
                completeTask.TaskTypeHash = DroneRepairTaskTypeHash;
                completeTask.TaskProgress01 = 1f;
                completeTask.TaskEfficiencyScalar = 1f;
                completeTask.InventoryPayloadHash = 0u;
                tasks[commandIndex] = completeTask;

                DroneTransactionIntegrityDTO completeIntegrity = default;
                completeIntegrity.TargetEntityHash = completeTask.TargetEntityHash;
                completeIntegrity.CurrentIntegrityMilli = ToIntegrityMilli(currentIntegrity);
                completeIntegrity.MaxRecoverableIntegrityMilli = ToIntegrityMilli(recoverableIntegrity);
                completeIntegrity.RepairBudgetMilli = 0;
                completeIntegrity.Flags = 1u;
                completeIntegrity.CommandIndex = commandIndex;
                completeIntegrity.Slot = slot;
                integrity[commandIndex] = completeIntegrity;
                return true;
            }

            int targetHash = Mathf.Max(1, GetRuntimeId(target));
            int repairBudgetMilli = ToIntegrityMilli(Mathf.Max(0f, drone.RepairRatePerSecond * command.DeltaTime));
            if (repairBudgetMilli <= 0)
                return false;

            DroneTaskDTO repairTask = default;
            repairTask.TargetEntityHash = (uint)targetHash;
            repairTask.TaskTypeHash = DroneRepairTaskTypeHash;
            repairTask.TaskProgress01 = 0f;
            repairTask.TaskEfficiencyScalar = 1f;
            repairTask.InventoryPayloadHash = 0u;
            tasks[commandIndex] = repairTask;

            DroneTransactionIntegrityDTO repairIntegrity = default;
            repairIntegrity.TargetEntityHash = (uint)targetHash;
            repairIntegrity.CurrentIntegrityMilli = ToIntegrityMilli(currentIntegrity);
            repairIntegrity.MaxRecoverableIntegrityMilli = ToIntegrityMilli(recoverableIntegrity);
            repairIntegrity.RepairBudgetMilli = repairBudgetMilli;
            repairIntegrity.Flags = 1u;
            repairIntegrity.CommandIndex = commandIndex;
            repairIntegrity.Slot = slot;
            integrity[commandIndex] = repairIntegrity;
            return true;
        }

        private static bool PrepareMiningTransaction(
            int commandIndex,
            in DroneServiceCommand command,
            NativeArray<HeadlessDroneState> droneStates,
            NativeArray<DroneTaskDTO> tasks,
            NativeArray<DroneTransactionIntegrityDTO> integrity)
        {
            int slot = command.Slot;
            if ((uint)slot >= (uint)HeadlessDroneCapacity)
                return false;

            HeadlessDroneState drone = droneStates[slot];
            DroneFleetTuningConstants tuning = ResolveDroneTuning();
            DroneChassisSpecDTO chassis = ResolveLaunchDroneChassisSpec(DroneFleetTaskKind.MineNode, in tuning);
            float holdSeconds = Mathf.Max(0.01f, chassis.MiningHoldSeconds);
            float progress = Mathf.Clamp01(drone.RepairAccumulator / holdSeconds);
            int sourceId = drone.TargetModuleId != 0
                ? drone.TargetModuleId
                : unchecked((int)math.hash(drone.TargetPosition));
            uint targetHash = (uint)Mathf.Max(1, sourceId);
            DroneTaskDTO miningTask = default;
            miningTask.TargetEntityHash = targetHash;
            miningTask.TaskTypeHash = DroneMiningTaskTypeHash;
            miningTask.TaskProgress01 = progress;
            miningTask.TaskEfficiencyScalar = math.rcp(holdSeconds);
            miningTask.InventoryPayloadHash = unchecked((uint)DroneInventoryCopperHash);
            tasks[commandIndex] = miningTask;

            DroneTransactionIntegrityDTO miningIntegrity = default;
            miningIntegrity.TargetEntityHash = targetHash;
            miningIntegrity.CurrentIntegrityMilli = 0;
            miningIntegrity.MaxRecoverableIntegrityMilli = 0;
            miningIntegrity.RepairBudgetMilli = 0;
            miningIntegrity.Flags = 2u;
            miningIntegrity.CommandIndex = commandIndex;
            miningIntegrity.Slot = slot;
            integrity[commandIndex] = miningIntegrity;
            return true;
        }

        private static void ApplyDroneTransactionResults(
            int commandCount,
            NativeArray<DroneTransactionResultDTO> results)
        {
            IDataVault stateVault = s_CachedDataVault;
            if (!TryAcquireDroneVaultWriteBuffer(
                    stateVault,
                    in s_DroneStatesHandle,
                    BufferID.ShinobuDroneFleetStates,
                    HeadlessDroneCapacity,
                    out NativeArray<HeadlessDroneState> droneStates))
            {
                return;
            }

            if (!TryAcquireDroneMirrorWriteBuffers(
                    out NativeArray<float3> positionsSoA,
                    out NativeArray<byte> stateBytes,
                    out NativeArray<DroneStateDTO> stateDtos,
                    out NativeArray<DroneTargetDTO> targetDtos,
                    out IDataVault mirrorVault))
            {
                stateVault.ReleaseWriteLock(in s_DroneStatesHandle, SystemID.Construction);
                return;
            }

            try
            {
                for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
                {
                    DroneTransactionResultDTO result = results[commandIndex];
                    if (result.TaskTypeHash == 0u)
                        continue;

                    int slot = result.Slot;
                    if ((uint)slot >= (uint)HeadlessDroneCapacity ||
                        s_DroneSlotDroneIds == null ||
                        s_DroneSlotDroneIds[slot] != result.DroneId ||
                        (uint)slot >= (uint)droneStates.Length)
                    {
                        continue;
                    }

                    HeadlessDroneState drone = droneStates[slot];
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

                    droneStates[slot] = drone;
                    MirrorDroneSoA(slot, in drone, positionsSoA, stateBytes, stateDtos, targetDtos);
                }
            }
            finally
            {
                ReleaseDroneMirrorWriteLocks(mirrorVault, 4);
                stateVault.ReleaseWriteLock(in s_DroneStatesHandle, SystemID.Construction);
            }
        }

        private static bool IsDroneServiceTransactionCommandConsumed(int commandIndex)
        {
            if (!s_DroneTransactionConsumedMaskCurrent ||
                !TryReadDroneTransactionBuffer(
                    in s_DroneTransactionCommandConsumedHandle,
                    DroneFleetTransactionCommandConsumedBufferId,
                    DroneServiceCommandCapacity,
                    out NativeArray<byte>.ReadOnly commandConsumed))
            {
                return false;
            }

            return (uint)commandIndex < (uint)commandConsumed.Length &&
                   commandConsumed[commandIndex] != 0;
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

            DroneFleetInventoryTransactionSignal transactionSignal = default;
            transactionSignal.DroneId = drone.DroneId;
            transactionSignal.SourceId = sourceId;
            transactionSignal.DestinationId = drone.HubGridId;
            transactionSignal.ItemHash = unchecked((int)result.InventoryHash);
            transactionSignal.Quantity = quantity;
            transactionSignal.Position = drone.Position;
            transactionSignal.Flags = 2u;
            transactionSignal.Reserved0 = result.ActiveInventorySlots;
            SignalBus<DroneFleetInventoryTransactionSignal>.TryPushTracked(in transactionSignal, ref s_x001DroneFleetManagerTransactionsSignalPushDropCount);
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
            ItemAcquiredSignal signal = default;
            signal.PositionAup = positionAup;
            signal.ItemHash = itemHash;
            signal.OreHash = (uint)Mathf.Max(0, sourceId);
            signal.Quantity = (ushort)Mathf.Clamp(quantity, 1, ushort.MaxValue);
            signal.SourceKind = ItemAcquiredSignalSourceKinds.DroneMining;
            signal.Flags = 0;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            SignalBus<ItemAcquiredSignal>.TryPushTracked(in signal, ref s_x001DroneFleetManagerTransactionsSignalPushDropCount);
        }

        private static int ResolveInventoryActiveSlotCount(NativeArray<int> activeSlotCount)
        {
            if (!activeSlotCount.IsCreated || activeSlotCount.Length <= 0)
                return 0;

            return Mathf.Max(0, activeSlotCount[0]);
        }

        private static void ClearDroneTransactionCounters(NativeArray<DroneTransactionCounterDTO> counters)
        {
            if (!counters.IsCreated)
                return;

            for (int i = 0; i < counters.Length; i++)
                counters[i] = default;
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

            lane = default;
            lane.Handle = handle;
            lane.ExpectedBufferID = expectedBufferId;
            lane.Length = length;
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
            NativeArray<int> activeSlotCount,
            NativeArray<DroneTransactionResultDTO> results,
            NativeArray<DroneTransactionCounterDTO> counters,
            NativeArray<DroneTransactionTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || !counters.IsCreated)
                return;

            int index = s_DroneTransactionTelemetryCursor;
            if ((uint)index >= (uint)telemetry.Length)
                index = 0;

            int repairCount = ReadDroneTransactionCounter(DroneTransactionCounterSlot.RepairCount, counters);
            int miningCount = ReadDroneTransactionCounter(DroneTransactionCounterSlot.MiningCount, counters);
            int inventoryAdds = ReadDroneTransactionCounter(DroneTransactionCounterSlot.InventoryAdds, counters);
            int conflicts = ReadDroneTransactionCounter(DroneTransactionCounterSlot.AtomicConflicts, counters);
            int vfxSignals = ReadDroneTransactionCounter(DroneTransactionCounterSlot.VfxSignals, counters);
            int faults = ReadDroneTransactionCounter(DroneTransactionCounterSlot.Faults, counters);
            uint stateHash = ComputeDroneTransactionStateHash(commandCount, results);
            float quality = ResolveGlobalQualityWeight();
            uint telemetryFrame = s_DroneTransactionScheduledFrame != 0u
                ? s_DroneTransactionScheduledFrame
                : Hecton8.Core.SystemDispatcher.CurrentFrameId;
            DroneTransactionTelemetryEntry entry = default;
            entry.Frame = telemetryFrame;
            entry.StateHash = stateHash;
            entry.TransactionCount = transactionCount;
            entry.RepairCount = repairCount;
            entry.MiningCount = miningCount;
            entry.InventoryAdds = inventoryAdds;
            entry.AtomicConflicts = conflicts;
            entry.VfxSignals = vfxSignals;
            entry.GlobalQualityWeight = quality;
            entry.EstimatedMicroseconds = EstimateDroneTransactionMicroseconds(transactionCount, quality);
            entry.FaultFlags = faults > 0 ? DroneTransactionResultDTO.FlagNaNFault : 0u;
            entry.LastTargetHash = ResolveLastDroneTransactionTargetHash(commandCount, results);
            entry.ActiveInventorySlots = ResolveInventoryActiveSlotCount(activeSlotCount);
            entry.CommandCount = commandCount;
            entry.LayoutHash = DroneTransactionLayoutHash;
            telemetry[index] = entry;
            s_DroneTransactionTelemetryCursor = (index + 1) % telemetry.Length;
            s_DroneTransactionLastTelemetryFrame = telemetryFrame;
            if (faults > 0)
                DumpDroneTransactionBlackBoxOncePerFrame();
        }

        private static int ReadDroneTransactionCounter(
            DroneTransactionCounterSlot slot,
            NativeArray<DroneTransactionCounterDTO> counters)
        {
            return counters.IsCreated && (uint)slot < (uint)counters.Length
                ? counters[(int)slot].Value
                : 0;
        }

        private static uint ComputeDroneTransactionStateHash(
            int commandCount,
            NativeArray<DroneTransactionResultDTO> results)
        {
            uint hash = 2166136261u;
            int count = math.min(commandCount, results.IsCreated ? results.Length : 0);
            for (int i = 0; i < count; i++)
            {
                DroneTransactionResultDTO result = results[i];
                if (result.TaskTypeHash == 0u)
                    continue;

                hash = (hash ^ result.TargetEntityHash) * 16777619u;
                hash = (hash ^ result.TaskTypeHash) * 16777619u;
                hash = (hash ^ (uint)math.max(0, result.DroneId)) * 16777619u;
                hash = (hash ^ result.Flags) * 16777619u;
            }

            return hash;
        }

        private static uint ResolveLastDroneTransactionTargetHash(
            int commandCount,
            NativeArray<DroneTransactionResultDTO> results)
        {
            int count = math.min(commandCount, results.IsCreated ? results.Length : 0);
            for (int i = count - 1; i >= 0; i--)
            {
                uint hash = results[i].TargetEntityHash;
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
            if (!TryReadDroneTransactionBuffer(
                    in s_DroneTransactionTelemetryHandle,
                    DroneFleetTransactionTelemetryBufferId,
                    DroneTransactionTelemetryCapacity,
                    out NativeArray<DroneTransactionTelemetryEntry>.ReadOnly telemetry) ||
                telemetry.Length <= 0)
            {
                return false;
            }

            int cursor = s_DroneTransactionTelemetryCursor - 1;
            if (cursor < 0)
                cursor = telemetry.Length - 1;

            DroneTransactionTelemetryEntry entry = telemetry[cursor];
            if (entry.LayoutHash == 0u)
                return false;

            snapshot = ToDroneTransactionSnapshot(in entry);
            return true;
        }

        internal static int CopyDroneTransactionTelemetry(DroneTransactionTelemetrySnapshot[] buffer)
        {
            if (buffer == null ||
                buffer.Length <= 0 ||
                !TryReadDroneTransactionBuffer(
                    in s_DroneTransactionTelemetryHandle,
                    DroneFleetTransactionTelemetryBufferId,
                    DroneTransactionTelemetryCapacity,
                    out NativeArray<DroneTransactionTelemetryEntry>.ReadOnly telemetry))
            {
                return 0;
            }

            int capacity = math.min(buffer.Length, telemetry.Length);
            int count = 0;
            int cursor = s_DroneTransactionTelemetryCursor;
            for (int i = 0; i < telemetry.Length && count < capacity; i++)
            {
                int sourceIndex = (cursor + i) % telemetry.Length;
                DroneTransactionTelemetryEntry entry = telemetry[sourceIndex];
                if (entry.LayoutHash == 0u)
                    continue;

                buffer[count++] = ToDroneTransactionSnapshot(in entry);
            }

            return count;
        }

        internal static int CopyDroneTransactionDebugTasks(DroneTransactionDebugTask[] buffer)
        {
            NativeArray<HeadlessDroneState> droneStates = default;
            NativeArray<DroneTaskDTO>.ReadOnly tasks = default;
            NativeArray<DroneTransactionCommandDTO>.ReadOnly commands = default;
            NativeArray<DroneTransactionResultDTO>.ReadOnly results = default;
            NativeArray<byte>.ReadOnly commandConsumed = default;
            if (buffer == null ||
                buffer.Length <= 0 ||
                s_DroneTransactionJobScheduled ||
                s_DroneSlotDroneIds == null ||
                !TryOpenDroneCoreBuffers(
                    out droneStates,
                    out _,
                    out _,
                    out _) ||
                !TryReadDroneTransactionBuffer(
                    in s_DroneTransactionTasksHandle,
                    DroneFleetTransactionTasksBufferId,
                    DroneServiceCommandCapacity,
                    out tasks) ||
                !TryReadDroneTransactionBuffer(
                    in s_DroneTransactionCommandsHandle,
                    DroneFleetTransactionCommandsBufferId,
                    DroneServiceCommandCapacity,
                    out commands) ||
                !TryReadDroneTransactionBuffer(
                    in s_DroneTransactionResultsHandle,
                    DroneFleetTransactionResultsBufferId,
                    DroneServiceCommandCapacity,
                    out results) ||
                !TryReadDroneTransactionBuffer(
                    in s_DroneTransactionCommandConsumedHandle,
                    DroneFleetTransactionCommandConsumedBufferId,
                    DroneServiceCommandCapacity,
                    out commandConsumed))
            {
                return 0;
            }

            int count = 0;
            int limit = math.min(tasks.Length, commandConsumed.Length);
            for (int commandIndex = 0; commandIndex < limit && count < buffer.Length; commandIndex++)
            {
                if (commandConsumed[commandIndex] == 0)
                    continue;

                DroneTransactionResultDTO result = commandIndex < results.Length
                    ? results[commandIndex]
                    : default;
                DroneTransactionCommandDTO command = commandIndex < commands.Length
                    ? commands[commandIndex]
                    : default;
                DroneTaskDTO task = tasks[commandIndex];
                int slot = result.Slot;
                if (slot < 0)
                    slot = command.Slot;
                if ((uint)slot >= (uint)HeadlessDroneCapacity || s_DroneSlotDroneIds[slot] <= 0)
                    continue;

                HeadlessDroneState drone = droneStates[slot];
                DroneTransactionDebugTask debugTask = default;
                debugTask.Position = drone.Position;
                debugTask.Target = drone.TargetPosition;
                debugTask.Velocity = drone.Velocity;
                debugTask.TargetEntityHash = task.TargetEntityHash;
                debugTask.TaskTypeHash = task.TaskTypeHash;
                debugTask.Progress01 = result.TaskTypeHash != 0u ? result.Progress01 : task.TaskProgress01;
                debugTask.VfxIntensity01 = result.VfxIntensity01;
                debugTask.DroneId = drone.DroneId;
                debugTask.Slot = slot;
                debugTask.InventorySlot = result.InventorySlot;
                debugTask.InventoryHash = result.InventoryHash;
                debugTask.InventoryQuantityAdded = result.InventoryQuantityAdded;
                debugTask.Flags = result.Flags;
                debugTask.ActiveInventorySlots = result.ActiveInventorySlots;
                debugTask.AtomicConflicts = result.AtomicConflicts;
                debugTask.BatteryPercent = drone.BatteryPercent;
                debugTask.StateFlags = (uint)drone.State;
                buffer[count++] = debugTask;
            }

            return count;
        }

        private static DroneTransactionTelemetrySnapshot ToDroneTransactionSnapshot(in DroneTransactionTelemetryEntry entry)
        {
            DroneTransactionTelemetrySnapshot snapshot = default;
            snapshot.Frame = entry.Frame;
            snapshot.StateHash = entry.StateHash;
            snapshot.TransactionCount = entry.TransactionCount;
            snapshot.RepairCount = entry.RepairCount;
            snapshot.MiningCount = entry.MiningCount;
            snapshot.InventoryAdds = entry.InventoryAdds;
            snapshot.AtomicConflicts = entry.AtomicConflicts;
            snapshot.VfxSignals = entry.VfxSignals;
            snapshot.GlobalQualityWeight = entry.GlobalQualityWeight;
            snapshot.EstimatedMicroseconds = entry.EstimatedMicroseconds;
            snapshot.FaultFlags = entry.FaultFlags;
            snapshot.LastTargetHash = entry.LastTargetHash;
            snapshot.ActiveInventorySlots = entry.ActiveInventorySlots;
            snapshot.CommandCount = entry.CommandCount;
            snapshot.LayoutHash = entry.LayoutHash;
            return snapshot;
        }

        private static void DumpDroneTransactionBlackBoxOncePerFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (s_DroneTransactionDumpFrame == frame)
                return;

            s_DroneTransactionDumpFrame = frame;
            TryWriteDroneTransactionBlackBoxFile();
        }

        private static void TryWriteDroneTransactionBlackBoxFile()
        {
            if (!TryReadDroneTransactionMutableBuffer(
                    in s_DroneTransactionTelemetryHandle,
                    DroneFleetTransactionTelemetryBufferId,
                    DroneTransactionTelemetryCapacity,
                    out NativeArray<DroneTransactionTelemetryEntry> telemetry))
            {
                return;
            }

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DroneFleetTransactionBlackBoxDumpPath);
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
                    void* telemetryPtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int byteCount = telemetry.Length * UnsafeUtility.SizeOf<DroneTransactionTelemetryEntry>();
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
