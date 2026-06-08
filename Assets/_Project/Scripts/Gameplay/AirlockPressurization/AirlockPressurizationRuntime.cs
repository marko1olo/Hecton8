// ============================================================================
// HECTON-8 - AirlockPressurizationRuntime.cs
// SHINOBU_338 dispatcher-facing Vault handles and job scheduling helpers.
// ============================================================================

using Hecton8.Atmosphere;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

using FluidCompartmentDTO = global::Hecton8.Core.Contracts.Physics.FluidCompartmentDTO;

namespace Hecton8.Gameplay.AirlockPressurization
{
    /// <summary>
    /// Generation-checked DataVault descriptors owned by the airlock pressurization route.
    /// </summary>
    public struct AirlockPressurizationVaultHandles
    {
        public VaultGenerationHandle<AirlockStateDTO> Airlocks;
        public VaultGenerationHandle<AirlockTuningDTO> Tunings;
        public VaultGenerationHandle<AirlockDoorPoseDTO> DoorPoses;
        public VaultGenerationHandle<AirlockExchangeIndexDTO> ExchangeIndices;
        public VaultGenerationHandle<AirlockEvaluationResultDTO> Results;
        public VaultGenerationHandle<BulkheadContainmentIntentDTO> BulkheadIntents;
        public VaultGenerationHandle<BubbleSpawnSignal> VfxSignals;
        public VaultGenerationHandle<MovementAcousticSignal> AcousticSignals;
        public VaultGenerationHandle<AirlockTelemetryEntry> Telemetry;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<AirlockHardwareProfileDTO> HardwareProfiles;
        public VaultGenerationHandle<AirlockDebugGizmoDTO> DebugGizmos;
        public VaultGenerationHandle<int> DumpRequested;

        /// <summary>
        /// Returns true when every required buffer descriptor has a non-zero generation.
        /// </summary>
        public bool IsCreated()
        {
            return IsHandleCreated(in Airlocks) &&
                   IsHandleCreated(in Tunings) &&
                   IsHandleCreated(in DoorPoses) &&
                   IsHandleCreated(in ExchangeIndices) &&
                   IsHandleCreated(in Results) &&
                   IsHandleCreated(in BulkheadIntents) &&
                   IsHandleCreated(in VfxSignals) &&
                   IsHandleCreated(in AcousticSignals) &&
                   IsHandleCreated(in Telemetry) &&
                   IsHandleCreated(in TelemetryCursor) &&
                   IsHandleCreated(in HardwareProfiles) &&
                   IsHandleCreated(in DebugGizmos) &&
                   IsHandleCreated(in DumpRequested);
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }
    }

    /// <summary>
    /// Transient NativeArray views resolved for one dispatcher scheduling window.
    /// </summary>
    public ref struct AirlockPressurizationVaultBuffers
    {
        public NativeArray<AirlockStateDTO> Airlocks;
        public NativeArray<AirlockTuningDTO> Tunings;
        public NativeArray<AirlockDoorPoseDTO> DoorPoses;
        public NativeArray<AirlockExchangeIndexDTO> ExchangeIndices;
        public NativeArray<AirlockEvaluationResultDTO> Results;
        public NativeArray<BulkheadContainmentIntentDTO> BulkheadIntents;
        public NativeArray<BubbleSpawnSignal> VfxSignals;
        public NativeArray<MovementAcousticSignal> AcousticSignals;
        public NativeArray<AirlockTelemetryEntry> Telemetry;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<AirlockHardwareProfileDTO> HardwareProfiles;
        public NativeArray<AirlockDebugGizmoDTO> DebugGizmos;
        public NativeArray<int> DumpRequested;

        /// <summary>
        /// Returns true when every required view was resolved for the current phase.
        /// </summary>
        public bool IsCreated()
        {
            return Airlocks.IsCreated &&
                   Tunings.IsCreated &&
                   DoorPoses.IsCreated &&
                   ExchangeIndices.IsCreated &&
                   Results.IsCreated &&
                   BulkheadIntents.IsCreated &&
                   VfxSignals.IsCreated &&
                   AcousticSignals.IsCreated &&
                   Telemetry.IsCreated &&
                   TelemetryCursor.IsCreated &&
                   HardwareProfiles.IsCreated &&
                   DebugGizmos.IsCreated &&
                   DumpRequested.IsCreated;
        }
    }

    public static partial class AirlockPressurizationVault
    {
        /// <summary>
        /// Acquires or reads all SHINOBU_338 Vault handles during cold owner setup.
        /// </summary>
        public static bool AcquireHandles(IDataVault vault, int requestedCapacity, out AirlockPressurizationVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (!vault.IsAllocationLocked && !Bootstrap(vault, requestedCapacity))
                return false;

            return ReadExistingHandles(vault, out handles) && handles.IsCreated();
        }

        /// <summary>
        /// Resolves handle descriptors into transient NativeArray views for the current phase.
        /// </summary>
        public static bool ResolveViews(
            IDataVault vault,
            in AirlockPressurizationVaultHandles handles,
            out AirlockPressurizationVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            if (!Open(vault, in handles.Airlocks, out buffers.Airlocks) ||
                !Open(vault, in handles.Tunings, out buffers.Tunings) ||
                !Open(vault, in handles.DoorPoses, out buffers.DoorPoses) ||
                !Open(vault, in handles.ExchangeIndices, out buffers.ExchangeIndices) ||
                !Open(vault, in handles.Results, out buffers.Results) ||
                !Open(vault, in handles.BulkheadIntents, out buffers.BulkheadIntents) ||
                !Open(vault, in handles.VfxSignals, out buffers.VfxSignals) ||
                !Open(vault, in handles.AcousticSignals, out buffers.AcousticSignals) ||
                !Open(vault, in handles.Telemetry, out buffers.Telemetry) ||
                !Open(vault, in handles.TelemetryCursor, out buffers.TelemetryCursor) ||
                !Open(vault, in handles.HardwareProfiles, out buffers.HardwareProfiles) ||
                !Open(vault, in handles.DebugGizmos, out buffers.DebugGizmos) ||
                !Open(vault, in handles.DumpRequested, out buffers.DumpRequested))
            {
                buffers = default;
                return false;
            }

            return buffers.IsCreated();
        }

        /// <summary>
        /// Advances the authority cadence accumulator without touching scene state.
        /// </summary>
        public static bool AdvanceCadence(
            ref AirlockPressurizationScheduleState state,
            float frameDeltaSeconds,
            float globalQualityWeight,
            uint frame,
            out float admittedDeltaSeconds,
            out float tickIntervalSeconds)
        {
            float quality = math.saturate(AirlockPressurizationMath.FiniteOr(globalQualityWeight, 0.5f));
            float frameDelta = math.max(0f, AirlockPressurizationMath.FiniteOr(frameDeltaSeconds, 0f));
            tickIntervalSeconds = AirlockPressurizationMath.ResolveAuthorityTickInterval();
            state.GlobalQualityWeight = quality;
            state.LastFrame = frame;
            state.LastTickIntervalSeconds = tickIntervalSeconds;
            state.TickAccumulatorSeconds = math.min(0.5f, math.max(0f, state.TickAccumulatorSeconds) + frameDelta);

            if (state.TickAccumulatorSeconds + 0.000001f < tickIntervalSeconds)
            {
                state.SkippedFrameCount++;
                admittedDeltaSeconds = 0f;
                return false;
            }

            admittedDeltaSeconds = state.TickAccumulatorSeconds;
            state.LastAdmittedDeltaSeconds = admittedDeltaSeconds;
            state.TickAccumulatorSeconds = 0f;
            state.ScheduledFrameCount++;
            return admittedDeltaSeconds > 0f;
        }

        /// <summary>
        /// Schedules evaluate, optional fluid/gas exchange, and telemetry jobs; completion remains dispatcher-owned.
        /// </summary>
        public static unsafe bool ScheduleSimulation(
            in AirlockPressurizationVaultBuffers buffers,
            NativeArray<FluidCompartmentDTO> fluidCompartments,
            NativeArray<AtmosphereCellDTO> atmosphereCells,
            int activeAirlockCount,
            uint frame,
            float admittedDeltaSeconds,
            float globalQualityWeight,
            uint solverWallMicroseconds,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!buffers.IsCreated())
                return false;

            int count = math.clamp(
                activeAirlockCount,
                0,
                math.min(
                    buffers.Airlocks.Length,
                    math.min(buffers.Results.Length, buffers.BulkheadIntents.Length)));
            if (count <= 0)
            {
                outputDependency = ScheduleTelemetry(in buffers, frame, solverWallMicroseconds, 0f, inputDependency);
                H8Memory.RegisterActiveJob(OwnerSystemId, outputDependency);
                return true;
            }

            float quality = math.saturate(AirlockPressurizationMath.FiniteOr(globalQualityWeight, 0.5f));
            float dt = math.max(0f, AirlockPressurizationMath.FiniteOr(admittedDeltaSeconds, 0f));
            EvaluateAirlockCyclesJob evaluateJob = new EvaluateAirlockCyclesJob
            {
                Airlocks = buffers.Airlocks,
                Tunings = buffers.Tunings,
                DoorPoses = buffers.DoorPoses,
                Results = buffers.Results,
                BulkheadIntents = buffers.BulkheadIntents,
                VfxSignals = buffers.VfxSignals,
                AcousticSignals = buffers.AcousticSignals,
                DebugGizmos = buffers.DebugGizmos,
                DeltaTime = dt,
                GlobalQualityWeight = quality,
                Frame = frame
            };
            JobHandle evaluateHandle = evaluateJob.Schedule(count, 32, inputDependency);

            JobHandle exchangeHandle = evaluateHandle;
            int exchangeCount = math.min(count, buffers.ExchangeIndices.Length);
            bool hasFluidCompartments = fluidCompartments.IsCreated;
            bool hasAtmosphereCells = atmosphereCells.IsCreated;
            if (exchangeCount > 0 && (hasFluidCompartments || hasAtmosphereCells))
            {
                IntegrateAirlockExchangeJob exchangeJob = new IntegrateAirlockExchangeJob
                {
                    Airlocks = (AirlockStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(buffers.Airlocks),
                    ExchangeIndices = (AirlockExchangeIndexDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffers.ExchangeIndices),
                    Tunings = (AirlockTuningDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffers.Tunings),
                    FluidCompartments = hasFluidCompartments
                        ? (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(fluidCompartments)
                        : null,
                    AtmosphereCells = hasAtmosphereCells
                        ? (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(atmosphereCells)
                        : null,
                    Results = (AirlockEvaluationResultDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(buffers.Results),
                    AirlockCount = count,
                    TuningCount = buffers.Tunings.Length,
                    FluidCompartmentCount = hasFluidCompartments ? fluidCompartments.Length : 0,
                    AtmosphereCellCount = hasAtmosphereCells ? atmosphereCells.Length : 0,
                    ResultCount = buffers.Results.Length,
                    ExchangeCount = exchangeCount,
                    DeltaTime = dt
                };
                exchangeHandle = exchangeJob.Schedule(evaluateHandle);
            }

            outputDependency = ScheduleTelemetry(
                in buffers,
                frame,
                solverWallMicroseconds,
                AirlockPressurizationMath.ResolveAuthorityTickInterval(),
                exchangeHandle);
            H8Memory.RegisterActiveJob(OwnerSystemId, outputDependency);
            return true;
        }

        /// <summary>
        /// Flushes already completed unmanaged output rows into SignalBus lanes and the fault dump file.
        /// </summary>
        public static bool FlushCompletedOutputs(
            AirlockPressurizationVaultBuffers buffers,
            int activeAirlockCount,
            bool dispatcherCompletionConfirmed)
        {
            if (!dispatcherCompletionConfirmed || !buffers.IsCreated())
                return false;

            int count = math.clamp(activeAirlockCount, 0, buffers.Airlocks.Length);
            uint bulkheadIntentFlushCounters = AirlockPressurizationIntentFlush.PushBulkheadIntents(buffers.BulkheadIntents, count);
            AirlockPressurizationIntentFlush.MergeFlushCountersIntoTelemetry(
                buffers.Telemetry,
                buffers.TelemetryCursor,
                bulkheadIntentFlushCounters);
            uint signalFlushCounters = AirlockPressurizationSignalFlush.PushFrameSignals(buffers.VfxSignals, buffers.AcousticSignals, count);
            AirlockPressurizationSignalFlush.MergeSignalFlushCountersIntoTelemetry(
                buffers.Telemetry,
                buffers.TelemetryCursor,
                signalFlushCounters);
            if (buffers.DumpRequested.Length <= 0 || buffers.DumpRequested[0] == 0)
                return true;

            if (AirlockTelemetryDumper.TryDump(buffers.Telemetry))
                buffers.DumpRequested[0] = 0;

            return true;
        }

        private static JobHandle ScheduleTelemetry(
            in AirlockPressurizationVaultBuffers buffers,
            uint frame,
            uint solverWallMicroseconds,
            float tickIntervalSeconds,
            JobHandle inputDependency)
        {
            RecordAirlockTelemetryJob telemetryJob = new RecordAirlockTelemetryJob
            {
                Airlocks = buffers.Airlocks,
                Results = buffers.Results,
                VfxSignals = buffers.VfxSignals,
                AcousticSignals = buffers.AcousticSignals,
                Telemetry = buffers.Telemetry,
                TelemetryCursor = buffers.TelemetryCursor,
                DumpRequested = buffers.DumpRequested,
                Frame = frame,
                SolverWallMicroseconds = solverWallMicroseconds,
                TickIntervalSeconds = tickIntervalSeconds
            };
            return telemetryJob.Schedule(inputDependency);
        }

        private static bool ReadExistingHandles(IDataVault vault, out AirlockPressurizationVaultHandles handles)
        {
            handles = default;
            return vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.AirlockStates, out handles.Airlocks) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.Tuning, out handles.Tunings) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.DoorPoses, out handles.DoorPoses) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.ExchangeIndices, out handles.ExchangeIndices) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.EvaluationResults, out handles.Results) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.BulkheadIntents, out handles.BulkheadIntents) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.VfxSignals, out handles.VfxSignals) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.AcousticSignals, out handles.AcousticSignals) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.TelemetryRing, out handles.Telemetry) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.TelemetryCursor, out handles.TelemetryCursor) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.HardwareProfiles, out handles.HardwareProfiles) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.DebugGizmos, out handles.DebugGizmos) &&
                   vault.TryGetGenerationHandle(AirlockPressurizationBufferIds.DumpRequested, out handles.DumpRequested);
        }

        private static bool Open<T>(IDataVault vault, in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }
    }
}
