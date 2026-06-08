// ============================================================================
// HECTON-8 - AirlockPressurizationVault.cs
// SHINOBU_338 cold DataVault buffer staging helpers.
// ============================================================================

using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Gameplay.AirlockPressurization
{
    public static partial class AirlockPressurizationVault
    {
        public const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;

        public static bool Bootstrap(IDataVault vault, int requestedCapacity)
        {
            if (vault == null)
                return false;

            int capacity = math.clamp(requestedCapacity, 1, AirlockPressurizationConstants.MaxActiveAirlocks);
            vault.EnsureGenerationHandle<AirlockStateDTO>(
                AirlockPressurizationBufferIds.AirlockStates,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockTuningDTO>(
                AirlockPressurizationBufferIds.Tuning,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockDoorPoseDTO>(
                AirlockPressurizationBufferIds.DoorPoses,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockExchangeIndexDTO>(
                AirlockPressurizationBufferIds.ExchangeIndices,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockEvaluationResultDTO>(
                AirlockPressurizationBufferIds.EvaluationResults,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<BulkheadContainmentIntentDTO>(
                AirlockPressurizationBufferIds.BulkheadIntents,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<Hecton8.Core.Contracts.Signals.BubbleSpawnSignal>(
                AirlockPressurizationBufferIds.VfxSignals,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<Hecton8.Core.Contracts.Signals.MovementAcousticSignal>(
                AirlockPressurizationBufferIds.AcousticSignals,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockTelemetryEntry>(
                AirlockPressurizationBufferIds.TelemetryRing,
                AirlockPressurizationConstants.TelemetryFrameCount,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<int>(
                AirlockPressurizationBufferIds.TelemetryCursor,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            vault.EnsureGenerationHandle<AirlockHardwareProfileDTO>(
                AirlockPressurizationBufferIds.HardwareProfiles,
                AirlockPressurizationConstants.MaxHardwareProfiles,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<AirlockDebugGizmoDTO>(
                AirlockPressurizationBufferIds.DebugGizmos,
                capacity,
                OwnerSystemId,
                NativeArrayOptions.UninitializedMemory);
            vault.EnsureGenerationHandle<int>(
                AirlockPressurizationBufferIds.DumpRequested,
                1,
                OwnerSystemId,
                NativeArrayOptions.ClearMemory);
            return true;
        }

        public static bool TryReadAirlocks(IDataVault vault, out NativeArray<AirlockStateDTO>.ReadOnly airlocks)
        {
            return TryReadOnlyBuffer<AirlockStateDTO>(vault, AirlockPressurizationBufferIds.AirlockStates, out airlocks);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadAirlocks(IDataVault vault, out NativeArray<AirlockStateDTO> airlocks)
        {
            return TryReadLegacyMutableBuffer<AirlockStateDTO>(vault, AirlockPressurizationBufferIds.AirlockStates, out airlocks);
        }

        public static bool TryReadTelemetry(IDataVault vault, out NativeArray<AirlockTelemetryEntry>.ReadOnly telemetry)
        {
            return TryReadOnlyBuffer<AirlockTelemetryEntry>(vault, AirlockPressurizationBufferIds.TelemetryRing, out telemetry);
        }

        public static bool TryReadTelemetryCursor(IDataVault vault, out NativeArray<int>.ReadOnly cursor)
        {
            return TryReadOnlyBuffer<int>(vault, AirlockPressurizationBufferIds.TelemetryCursor, out cursor);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadTelemetry(IDataVault vault, out NativeArray<AirlockTelemetryEntry> telemetry)
        {
            return TryReadLegacyMutableBuffer<AirlockTelemetryEntry>(vault, AirlockPressurizationBufferIds.TelemetryRing, out telemetry);
        }

        public static bool TryReadTuning(IDataVault vault, out NativeArray<AirlockTuningDTO>.ReadOnly tuning)
        {
            return TryReadOnlyBuffer<AirlockTuningDTO>(vault, AirlockPressurizationBufferIds.Tuning, out tuning);
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadTuning(IDataVault vault, out NativeArray<AirlockTuningDTO> tuning)
        {
            return TryReadLegacyMutableBuffer<AirlockTuningDTO>(vault, AirlockPressurizationBufferIds.Tuning, out tuning);
        }

        public static bool TryReadDebugGizmos(IDataVault vault, out NativeArray<AirlockDebugGizmoDTO>.ReadOnly gizmos)
        {
            return TryReadOnlyBuffer<AirlockDebugGizmoDTO>(vault, AirlockPressurizationBufferIds.DebugGizmos, out gizmos);
        }

        public static bool TryWriteAirlockSnapshot(
            IDataVault vault,
            in AirlockPressurizationAuthoringSnapshot snapshot,
            out int slotIndex)
        {
            slotIndex = -1;
            if (!IsAuthoringSnapshotUsable(in snapshot) ||
                vault == null ||
                vault.ActiveBurstLockMask != 0u ||
                !AcquireHandles(vault, AirlockPressurizationConstants.MaxActiveAirlocks, out AirlockPressurizationVaultHandles handles))
            {
                return false;
            }

            bool airlocksLocked = false;
            bool tuningsLocked = false;
            bool doorPosesLocked = false;
            bool exchangeIndicesLocked = false;
            try
            {
                airlocksLocked = vault.TryAcquireWriteLock(in handles.Airlocks, OwnerSystemId, out NativeArray<AirlockStateDTO> airlocks);
                tuningsLocked = vault.TryAcquireWriteLock(in handles.Tunings, OwnerSystemId, out NativeArray<AirlockTuningDTO> tunings);
                doorPosesLocked = vault.TryAcquireWriteLock(in handles.DoorPoses, OwnerSystemId, out NativeArray<AirlockDoorPoseDTO> doorPoses);
                exchangeIndicesLocked = vault.TryAcquireWriteLock(in handles.ExchangeIndices, OwnerSystemId, out NativeArray<AirlockExchangeIndexDTO> exchangeIndices);
                if (!airlocksLocked ||
                    !tuningsLocked ||
                    !doorPosesLocked ||
                    !exchangeIndicesLocked ||
                    !airlocks.IsCreated ||
                    !tunings.IsCreated ||
                    !doorPoses.IsCreated ||
                    !exchangeIndices.IsCreated)
                {
                    return false;
                }

                slotIndex = ResolveSnapshotSlot(doorPoses, snapshot.EdgeHashID);
                if (slotIndex < 0 ||
                    slotIndex >= airlocks.Length ||
                    slotIndex >= tunings.Length ||
                    slotIndex >= doorPoses.Length ||
                    slotIndex >= exchangeIndices.Length)
                {
                    slotIndex = -1;
                    return false;
                }

                WriteSnapshotSlot(
                    airlocks,
                    tunings,
                    doorPoses,
                    exchangeIndices,
                    slotIndex,
                    in snapshot);
                return true;
            }
            finally
            {
                if (exchangeIndicesLocked)
                    vault.ReleaseWriteLock(in handles.ExchangeIndices, OwnerSystemId);
                if (doorPosesLocked)
                    vault.ReleaseWriteLock(in handles.DoorPoses, OwnerSystemId);
                if (tuningsLocked)
                    vault.ReleaseWriteLock(in handles.Tunings, OwnerSystemId);
                if (airlocksLocked)
                    vault.ReleaseWriteLock(in handles.Airlocks, OwnerSystemId);
            }
        }

        public static bool TryClearAirlockSnapshot(IDataVault vault, uint edgeHash)
        {
            if (vault == null ||
                edgeHash == 0u ||
                vault.ActiveBurstLockMask != 0u ||
                !ReadExistingHandles(vault, out AirlockPressurizationVaultHandles handles) ||
                !handles.IsCreated())
            {
                return false;
            }

            bool airlocksLocked = false;
            bool tuningsLocked = false;
            bool doorPosesLocked = false;
            bool exchangeIndicesLocked = false;
            try
            {
                airlocksLocked = vault.TryAcquireWriteLock(in handles.Airlocks, OwnerSystemId, out NativeArray<AirlockStateDTO> airlocks);
                tuningsLocked = vault.TryAcquireWriteLock(in handles.Tunings, OwnerSystemId, out NativeArray<AirlockTuningDTO> tunings);
                doorPosesLocked = vault.TryAcquireWriteLock(in handles.DoorPoses, OwnerSystemId, out NativeArray<AirlockDoorPoseDTO> doorPoses);
                exchangeIndicesLocked = vault.TryAcquireWriteLock(in handles.ExchangeIndices, OwnerSystemId, out NativeArray<AirlockExchangeIndexDTO> exchangeIndices);
                if (!airlocksLocked ||
                    !tuningsLocked ||
                    !doorPosesLocked ||
                    !exchangeIndicesLocked ||
                    !airlocks.IsCreated ||
                    !tunings.IsCreated ||
                    !doorPoses.IsCreated ||
                    !exchangeIndices.IsCreated)
                {
                    return false;
                }

                int slotIndex = FindExactSnapshotSlot(doorPoses, edgeHash);
                if (slotIndex < 0 ||
                    slotIndex >= airlocks.Length ||
                    slotIndex >= tunings.Length ||
                    slotIndex >= doorPoses.Length ||
                    slotIndex >= exchangeIndices.Length)
                {
                    return false;
                }

                airlocks[slotIndex] = default;
                tunings[slotIndex] = default;
                doorPoses[slotIndex] = default;
                exchangeIndices[slotIndex] = default;
                return true;
            }
            finally
            {
                if (exchangeIndicesLocked)
                    vault.ReleaseWriteLock(in handles.ExchangeIndices, OwnerSystemId);
                if (doorPosesLocked)
                    vault.ReleaseWriteLock(in handles.DoorPoses, OwnerSystemId);
                if (tuningsLocked)
                    vault.ReleaseWriteLock(in handles.Tunings, OwnerSystemId);
                if (airlocksLocked)
                    vault.ReleaseWriteLock(in handles.Airlocks, OwnerSystemId);
            }
        }

        [System.Obsolete("Use the NativeArray<T>.ReadOnly overload; legacy mutable wrapper retained for compatibility.", false)]
        public static bool TryReadDebugGizmos(IDataVault vault, out NativeArray<AirlockDebugGizmoDTO> gizmos)
        {
            return TryReadLegacyMutableBuffer<AirlockDebugGizmoDTO>(vault, AirlockPressurizationBufferIds.DebugGizmos, out gizmos);
        }

        private static bool TryReadOnlyBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryReadLegacyMutableBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool IsAuthoringSnapshotUsable(in AirlockPressurizationAuthoringSnapshot snapshot)
        {
            float normalLengthSq = math.lengthsq(snapshot.DoorNormal);
            return snapshot.EdgeHashID != 0u &&
                   snapshot.DoorAup.IsFinite() &&
                   math.all(math.isfinite(snapshot.DoorNormal)) &&
                   math.isfinite(normalLengthSq) &&
                   normalLengthSq > 0.0001f;
        }

        private static int ResolveSnapshotSlot(NativeArray<AirlockDoorPoseDTO> doorPoses, uint edgeHash)
        {
            int exact = FindExactSnapshotSlot(doorPoses, edgeHash);
            if (exact >= 0)
                return exact;

            if (!doorPoses.IsCreated || doorPoses.Length <= 0 || edgeHash == 0u)
                return -1;

            int capacity = math.min(doorPoses.Length, AirlockPressurizationConstants.MaxActiveAirlocks);
            int start = (int)(edgeHash % (uint)capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = (start + probe) % capacity;
                if (doorPoses[index].EdgeHashID == 0u)
                    return index;
            }

            return -1;
        }

        private static int FindExactSnapshotSlot(NativeArray<AirlockDoorPoseDTO> doorPoses, uint edgeHash)
        {
            if (!doorPoses.IsCreated || doorPoses.Length <= 0 || edgeHash == 0u)
                return -1;

            int capacity = math.min(doorPoses.Length, AirlockPressurizationConstants.MaxActiveAirlocks);
            int start = (int)(edgeHash % (uint)capacity);
            for (int probe = 0; probe < capacity; probe++)
            {
                int index = (start + probe) % capacity;
                if (doorPoses[index].EdgeHashID == edgeHash)
                    return index;
            }

            return -1;
        }

        private static void WriteSnapshotSlot(
            NativeArray<AirlockStateDTO> airlocks,
            NativeArray<AirlockTuningDTO> tunings,
            NativeArray<AirlockDoorPoseDTO> doorPoses,
            NativeArray<AirlockExchangeIndexDTO> exchangeIndices,
            int slotIndex,
            in AirlockPressurizationAuthoringSnapshot snapshot)
        {
            float maxWater = math.max(1f, AirlockPressurizationMath.FiniteOr(snapshot.MaxWaterVolumeLiters, AirlockPressurizationConstants.LitersPerCubicMeter));
            airlocks[slotIndex] = new AirlockStateDTO
            {
                InnerRoomHashID = snapshot.InnerRoomHashID,
                OuterRoomHashID = snapshot.OuterRoomHashID,
                CurrentWaterVolumeLiters = math.clamp(AirlockPressurizationMath.FiniteOr(snapshot.CurrentWaterVolumeLiters, 0f), 0f, maxWater),
                CurrentPressureATM = math.max(AirlockPressurizationConstants.MinimumPressureAtm, AirlockPressurizationMath.FiniteOr(snapshot.CurrentPressureAtm, AirlockPressurizationConstants.SurfacePressureAtm)),
                CycleStateFlags = snapshot.CycleStateFlags,
                CycleTimer = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.CycleTimer, 0f))
            };

            tunings[slotIndex] = new AirlockTuningDTO
            {
                PumpEvacuationSpeedLps = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.PumpEvacuationSpeedLps, AirlockPressurizationConstants.DefaultPumpEvacuationSpeedLps)),
                MaxWaterVolumeLiters = maxWater,
                ChamberVolumeLiters = math.max(maxWater + 1f, AirlockPressurizationMath.FiniteOr(snapshot.ChamberVolumeLiters, maxWater + 400f)),
                EqualizationCurveExponent = math.max(0.25f, AirlockPressurizationMath.FiniteOr(snapshot.EqualizationCurveExponent, AirlockPressurizationConstants.DefaultEqualizationCurveExponent)),
                PowerDrawWatts = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.PowerDrawWatts, AirlockPressurizationConstants.DefaultPowerDrawWatts)),
                AvailablePower01 = math.saturate(AirlockPressurizationMath.FiniteOr(snapshot.AvailablePower01, 1f)),
                ExternalDepthMeters = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.ExternalDepthMeters, 0f)),
                BreachAreaM2 = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.BreachAreaM2, AirlockPressurizationConstants.DefaultBreachAreaM2)),
                DischargeCoefficient = math.clamp(AirlockPressurizationMath.FiniteOr(snapshot.DischargeCoefficient, AirlockPressurizationConstants.DefaultDischargeCoefficient), 0.01f, 1f),
                GlobalQualityWeight = math.saturate(AirlockPressurizationMath.FiniteOr(snapshot.GlobalQualityWeight, AirlockPressurizationConstants.AuthoritativeQualityWeight)),
                PressureEqualizedAtm = math.max(0.001f, AirlockPressurizationMath.FiniteOr(snapshot.PressureEqualizedAtm, AirlockPressurizationConstants.PressureEqualizedAtm)),
                WaterEqualizedLiters = math.max(0.001f, AirlockPressurizationMath.FiniteOr(snapshot.WaterEqualizedLiters, AirlockPressurizationConstants.WaterEqualizedLiters)),
                ExternalPressureAtm = math.max(AirlockPressurizationConstants.MinimumPressureAtm, AirlockPressurizationMath.FiniteOr(snapshot.ExternalPressureAtm, AirlockPressurizationConstants.SurfacePressureAtm)),
                RoomPressureAtm = math.max(AirlockPressurizationConstants.MinimumPressureAtm, AirlockPressurizationMath.FiniteOr(snapshot.RoomPressureAtm, AirlockPressurizationConstants.SurfacePressureAtm)),
                Flags = 0u,
                Frame = snapshot.Frame
            };

            float normalLengthSq = math.lengthsq(snapshot.DoorNormal);
            doorPoses[slotIndex] = new AirlockDoorPoseDTO
            {
                DoorAup = snapshot.DoorAup,
                DoorNormal = snapshot.DoorNormal * math.rsqrt(math.max(normalLengthSq, 0.0001f)),
                WidthMeters = math.max(0.25f, AirlockPressurizationMath.FiniteOr(snapshot.WidthMeters, 2.6f)),
                HeightMeters = math.max(0.25f, AirlockPressurizationMath.FiniteOr(snapshot.HeightMeters, 3.2f)),
                DoorHashID = snapshot.DoorHashID,
                EdgeHashID = snapshot.EdgeHashID,
                Flags = AirlockDoorPoseFlags.Valid | AirlockDoorPoseFlags.OuterFaceSubmerged,
                ExternalDepthMeters = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.ExternalDepthMeters, 0f)),
                HeadMeters = math.max(0f, AirlockPressurizationMath.FiniteOr(snapshot.HeadMeters, 0f)),
                Frame = snapshot.Frame
            };

            exchangeIndices[slotIndex] = new AirlockExchangeIndexDTO
            {
                FluidCompartmentIndex = -1,
                AtmosphereCellIndex = -1,
                OwnerIndex = slotIndex,
                Flags = 0u
            };
        }
    }
}
