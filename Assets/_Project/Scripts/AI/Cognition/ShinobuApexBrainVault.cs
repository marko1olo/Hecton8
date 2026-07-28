using System;
using System.IO;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    /// <summary>
    /// Vault buffer IDs reserved by SHINOBU_61 without mutating the shared BufferID enum.
    /// </summary>
    public static class ApexBrainVaultBufferIds
    {
        public const BufferID ApexState = BufferID.ShinobuApexBrainVault_ApexState;
        public const BufferID MockPlayerAup = BufferID.ShinobuApexBrainVault_MockPlayerAup;
        public const BufferID AcousticEchoTap = BufferID.ShinobuApexBrainVault_AcousticEchoTap;
        public const BufferID Tuning = BufferID.ShinobuApexBrainVault_Tuning;
        public const BufferID EmergencyStats = BufferID.ShinobuApexBrainVault_EmergencyStats;
        public const BufferID MockWorldSampler = BufferID.ShinobuApexBrainVault_MockWorldSampler;
        public const BufferID Output = BufferID.ShinobuApexBrainVault_Output;
        public const BufferID ProximitySignal = BufferID.ShinobuApexBrainVault_ProximitySignal;
        public const BufferID CombatDamageSignal = BufferID.ShinobuApexBrainVault_CombatDamageSignal;
        public const BufferID PanicSignal = BufferID.ShinobuApexBrainVault_PanicSignal;
        public const BufferID InfluenceNodes = BufferID.ShinobuApexBrainVault_InfluenceNodes;

        // NO RESERVED ID: SHINOBU_61 reserved exactly eleven BufferID members in
        // H8Memory.cs:2227-2237 (ShinobuApexBrainVault_ApexState 70609 .. _InfluenceNodes 70619).
        // TelemetryRing, TelemetryCursor, CsvScratch and AmbushNodeScratch were never given
        // reservations. They previously aliased BufferID.SystemDispatcherMasterPresentationSuppression
        // (70626), SystemDispatcherDomainFenceHandles (70627), SystemDispatcherFenceTelemetry (70628)
        // and SystemDispatcherFenceTelemetryCursor (70629) - the four buffers the master frame
        // dispatcher claims at SystemDispatcher.cs:2192-2221 with different element types
        // (DispatcherPresentationSuppressionDTO, JobHandle, DispatcherFenceTelemetryEntry, int).
        // Whichever side called EnsureGenerationHandle first won the row range and the other side's
        // GlobalDataVault.ValidateType stride/alignment/type-hash check failed. Reached from the
        // editor menu path AI/Cognition/Editor/LeviathanCortexTunerWindow.cs:106, that order is
        // reversed and the DISPATCHER loses its presentation-suppression buffer, its per-domain
        // JobHandle fence array, its fence telemetry ring and its fence telemetry cursor.
        // The aliases are removed. The four handles are therefore never acquired, which makes
        // ApexBrainVaultHandles.IsCreated() false and ApexBrainVault.TryAcquireHandles return false.
        // Do not re-point these at any existing BufferID member - reserve new members in
        // Assets/_Project/Scripts/Core/Memory/H8Memory.cs first.
    }

    /// <summary>
    /// Generation-checked DataVault handles for the apex brain.
    /// </summary>
    public struct ApexBrainVaultHandles
    {
        public VaultGenerationHandle<ApexStateDTO> States;
        public VaultGenerationHandle<MockPlayerAUP> MockTargets;
        public VaultGenerationHandle<ApexBrainAcousticEchoTap> AcousticTaps;
        public VaultGenerationHandle<ApexBrainTuning> Tuning;
        public VaultGenerationHandle<ApexEmergencyStats> EmergencyStats;
        public VaultGenerationHandle<MockWorldSampler> WorldSampler;
        public VaultGenerationHandle<ApexBrainOutputDTO> Outputs;
        public VaultGenerationHandle<ApexProximitySignal> ProximitySignals;
        public VaultGenerationHandle<MockCombatDamageSignal> CombatDamageSignals;
        public VaultGenerationHandle<ApexPanicSignal> PanicSignals;
        public VaultGenerationHandle<ApexInfluenceNode> InfluenceNodes;
        public VaultGenerationHandle<float3> AmbushNodeScratch;
        public VaultGenerationHandle<ApexTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
#if UNITY_EDITOR
        public VaultGenerationHandle<byte> CsvScratch;
#endif

        public bool IsCreated()
        {
            return IsHandleCreated(in States) &&
                   IsHandleCreated(in MockTargets) &&
                   IsHandleCreated(in AcousticTaps) &&
                   IsHandleCreated(in Tuning) &&
                   IsHandleCreated(in EmergencyStats) &&
                   IsHandleCreated(in WorldSampler) &&
                   IsHandleCreated(in Outputs) &&
                   IsHandleCreated(in ProximitySignals) &&
                   IsHandleCreated(in CombatDamageSignals) &&
                   IsHandleCreated(in PanicSignals) &&
                   IsHandleCreated(in InfluenceNodes) &&
                   IsHandleCreated(in AmbushNodeScratch) &&
                   IsHandleCreated(in TelemetryRing) &&
                   IsHandleCreated(in TelemetryCursor)
#if UNITY_EDITOR
                   && IsHandleCreated(in CsvScratch)
#endif
                   ;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }
    }

    /// <summary>
    /// Transient NativeArray views resolved from generation-checked handles.
    /// </summary>
    public ref struct ApexBrainVaultBuffers
    {
        public NativeArray<ApexStateDTO> States;
        public NativeArray<MockPlayerAUP> MockTargets;
        public NativeArray<ApexBrainAcousticEchoTap> AcousticTaps;
        public NativeArray<ApexBrainTuning> Tuning;
        public NativeArray<ApexEmergencyStats> EmergencyStats;
        public NativeArray<MockWorldSampler> WorldSampler;
        public NativeArray<ApexBrainOutputDTO> Outputs;
        public NativeArray<ApexProximitySignal> ProximitySignals;
        public NativeArray<MockCombatDamageSignal> CombatDamageSignals;
        public NativeArray<ApexPanicSignal> PanicSignals;
        public NativeArray<ApexInfluenceNode> InfluenceNodes;
        public NativeArray<float3> AmbushNodeScratch;
        public NativeArray<ApexTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
#if UNITY_EDITOR
        public NativeArray<byte> CsvScratch;
#endif

        public bool IsCreated()
        {
            return States.IsCreated &&
                   MockTargets.IsCreated &&
                   AcousticTaps.IsCreated &&
                   Tuning.IsCreated &&
                   EmergencyStats.IsCreated &&
                   WorldSampler.IsCreated &&
                   Outputs.IsCreated &&
                   ProximitySignals.IsCreated &&
                   CombatDamageSignals.IsCreated &&
                   PanicSignals.IsCreated &&
                   InfluenceNodes.IsCreated &&
                   AmbushNodeScratch.IsCreated &&
                   TelemetryRing.IsCreated &&
                   TelemetryCursor.IsCreated
#if UNITY_EDITOR
                   && CsvScratch.IsCreated
#endif
                   ;
        }
    }

    /// <summary>
    /// Cold bridge for DataVault hydration, CSV overrides, job creation, and black-box dumps.
    /// </summary>
    public static class ApexBrainVault
    {
        private const uint DumpMagic = 0x53484E61u;
        private const uint DumpEndianMarker = 0x01020304u;
        private const int DumpVersion = 1;
        private const string DumpFileName = "Dump_SHINOBU_61.bin";
        private const string LegacyDumpFileName = "Dump_LEVIATHAN_CORTEX.bin";
        private const string Agent1300DumpFileName = "Dump_1300_AICognition.bin";
        private static readonly ulong ApexStateMutationGuardMask =
            ApexVaultMutationGuardBit(ApexBrainVaultBufferIds.ApexState);
        private static readonly ulong ApexTuningMutationGuardMask =
            ApexVaultMutationGuardBit(ApexBrainVaultBufferIds.Tuning);
#if UNITY_EDITOR
        private const string CsvFileName = "apex_predator_stats.csv";
        private static readonly uint _aggressionMultiplierHash = HashAscii("aggression_multiplier");
        private static readonly uint _acousticSensitivityHash = HashAscii("acoustic_sensitivity");
        private static readonly uint _turnRateHash = HashAscii("turn_rate");
        private static readonly uint _stalkingDistanceHash = HashAscii("stalking_distance");
        private static readonly uint _leviathanSpeedHash = HashAscii("leviathan_speed");
        private static readonly uint _terrorRadiusHash = HashAscii("terror_radius");
        private static readonly uint _baseDamageMagnitudeHash = HashAscii("base_damage_magnitude");
        private static readonly uint _biomeAggressionHash = HashAscii("biome_aggression_multiplier");
        private static readonly uint _simulationTickDeltaHash = HashAscii("simulation_tick_delta");
        private static readonly uint _strikeDistanceHash = HashAscii("strike_distance");
        private static readonly uint _headOffsetMetersHash = HashAscii("head_offset_meters");
        private static readonly uint _midOffsetMetersHash = HashAscii("mid_offset_meters");
        private static readonly uint _tailOffsetMetersHash = HashAscii("tail_offset_meters");
        private static readonly uint _noiseAggroGainHash = HashAscii("noise_aggro_gain");
        private static readonly uint _staminaRecoveryPerSecondHash = HashAscii("stamina_recovery_per_second");
        private static readonly uint _staminaStrikeCostHash = HashAscii("stamina_strike_cost");
        private static readonly uint _sweetLieShadowGainHash = HashAscii("sweet_lie_shadow_gain");
        private static readonly uint _sweetLieViewDotThresholdHash = HashAscii("sweet_lie_view_dot_threshold");
        private static readonly uint _ambushNodeRadiusMetersHash = HashAscii("ambush_node_radius_meters");
        private static readonly uint _visualOverkillGainHash = HashAscii("visual_overkill_gain");
        private static readonly uint _biteHeadLocalOffsetHash = HashAscii("bite_head_local_offset");
        private static readonly uint _globalQualityHash = HashAscii("global_quality_weight");
#endif

        /// <summary>
        /// Acquires or recovers all SHINOBU_61 vault handles.
        /// </summary>
        public static bool TryAcquireHandles(IDataVault vault, out ApexBrainVaultHandles handles)
        {
            handles = default;
            if (vault == null)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!TryReadExistingHandles(vault, out handles))
                    return false;

                ApexBrainVaultBuffers lockedBuffers;
                return TryResolveViews(vault, ref handles, out lockedBuffers);
            }

            handles.States = vault.EnsureGenerationHandle<ApexStateDTO>(
                ApexBrainVaultBufferIds.ApexState,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.MockTargets = vault.EnsureGenerationHandle<MockPlayerAUP>(
                ApexBrainVaultBufferIds.MockPlayerAup,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.AcousticTaps = vault.EnsureGenerationHandle<ApexBrainAcousticEchoTap>(
                ApexBrainVaultBufferIds.AcousticEchoTap,
                ApexBrainConstants.MaxAcousticTaps,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.Tuning = vault.EnsureGenerationHandle<ApexBrainTuning>(
                ApexBrainVaultBufferIds.Tuning,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.EmergencyStats = vault.EnsureGenerationHandle<ApexEmergencyStats>(
                ApexBrainVaultBufferIds.EmergencyStats,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.WorldSampler = vault.EnsureGenerationHandle<MockWorldSampler>(
                ApexBrainVaultBufferIds.MockWorldSampler,
                1,
                SystemID.AICognition,
                NativeArrayOptions.ClearMemory);
            handles.Outputs = vault.EnsureGenerationHandle<ApexBrainOutputDTO>(
                ApexBrainVaultBufferIds.Output,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.ProximitySignals = vault.EnsureGenerationHandle<ApexProximitySignal>(
                ApexBrainVaultBufferIds.ProximitySignal,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.CombatDamageSignals = vault.EnsureGenerationHandle<MockCombatDamageSignal>(
                ApexBrainVaultBufferIds.CombatDamageSignal,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.PanicSignals = vault.EnsureGenerationHandle<ApexPanicSignal>(
                ApexBrainVaultBufferIds.PanicSignal,
                ApexBrainConstants.MaxLeviathans,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            handles.InfluenceNodes = vault.EnsureGenerationHandle<ApexInfluenceNode>(
                ApexBrainVaultBufferIds.InfluenceNodes,
                ApexBrainConstants.InfluenceNodeCapacity,
                SystemID.AICognition,
                NativeArrayOptions.UninitializedMemory);
            // AmbushNodeScratch, TelemetryRing, TelemetryCursor and CsvScratch are intentionally NOT
            // acquired: see the NO RESERVED ID note on ApexBrainVaultBufferIds above. Allocating them
            // meant claiming BufferID 70626-70629, which the master frame dispatcher owns at
            // SystemDispatcher.cs:2192-2221. Those four handles stay default, so the IsCreated() check
            // below fails and this vault cannot hydrate at all until real BufferID members are reserved
            // in Assets/_Project/Scripts/Core/Memory/H8Memory.cs.

            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers))
                return false;

            GenerateEmergencyMockApexStats(buffers);
            return true;
        }

        /// <summary>
        /// Resolves transient NativeArray views from handles.
        /// </summary>
        public static bool TryResolveViews(IDataVault vault, ref ApexBrainVaultHandles handles, out ApexBrainVaultBuffers buffers)
        {
            buffers = default;
            if (vault == null || !handles.IsCreated())
                return false;

            if (!TryOpenVaultView(vault, in handles.States, ApexBrainConstants.MaxLeviathans, out buffers.States) ||
                !TryOpenVaultView(vault, in handles.MockTargets, ApexBrainConstants.MaxLeviathans, out buffers.MockTargets) ||
                !TryOpenVaultView(vault, in handles.AcousticTaps, ApexBrainConstants.MaxAcousticTaps, out buffers.AcousticTaps) ||
                !TryOpenVaultView(vault, in handles.Tuning, 1, out buffers.Tuning) ||
                !TryOpenVaultView(vault, in handles.EmergencyStats, 1, out buffers.EmergencyStats) ||
                !TryOpenVaultView(vault, in handles.WorldSampler, 1, out buffers.WorldSampler) ||
                !TryOpenVaultView(vault, in handles.Outputs, ApexBrainConstants.MaxLeviathans, out buffers.Outputs) ||
                !TryOpenVaultView(vault, in handles.ProximitySignals, ApexBrainConstants.MaxLeviathans, out buffers.ProximitySignals) ||
                !TryOpenVaultView(vault, in handles.CombatDamageSignals, ApexBrainConstants.MaxLeviathans, out buffers.CombatDamageSignals) ||
                !TryOpenVaultView(vault, in handles.PanicSignals, ApexBrainConstants.MaxLeviathans, out buffers.PanicSignals) ||
                !TryOpenVaultView(vault, in handles.InfluenceNodes, ApexBrainConstants.InfluenceNodeCapacity, out buffers.InfluenceNodes) ||
                !TryOpenVaultView(vault, in handles.AmbushNodeScratch, ApexBrainConstants.InfluenceNodeCapacity, out buffers.AmbushNodeScratch) ||
                !TryOpenVaultView(vault, in handles.TelemetryRing, ApexBrainConstants.TelemetryCapacity, out buffers.TelemetryRing) ||
                !TryOpenVaultView(vault, in handles.TelemetryCursor, 1, out buffers.TelemetryCursor))
            {
                buffers = default;
                return false;
            }

#if UNITY_EDITOR
            if (!TryOpenVaultView(vault, in handles.CsvScratch, ApexBrainConstants.CsvScratchBytes, out buffers.CsvScratch))
            {
                buffers = default;
                return false;
            }
#endif

            return buffers.IsCreated();
        }

        /// <summary>
        /// Reads one ApexStateDTO from a phase-local vault view.
        /// </summary>
        public static bool TryReadState(IDataVault vault, ref ApexBrainVaultHandles handles, int index, out ApexStateDTO state)
        {
            state = default;
            if (vault == null ||
                handles.States.BufferID == 0u ||
                handles.States.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handles.States, out NativeArray<ApexStateDTO>.ReadOnly states) ||
                (uint)index >= (uint)states.Length)
            {
                return false;
            }

            state = states[index];
            return true;
        }

        /// <summary>
        /// Writes one ApexStateDTO through a phase-local vault view.
        /// </summary>
        public static bool TryWriteState(IDataVault vault, ref ApexBrainVaultHandles handles, int index, in ApexStateDTO state)
        {
            if (vault == null ||
                handles.States.BufferID != (uint)ApexBrainVaultBufferIds.ApexState ||
                handles.States.Generation == 0u ||
                !TryAcquireApexMutationGuard(vault, ApexStateMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryOpenVaultView(vault, in handles.States, ApexBrainConstants.MaxLeviathans, out NativeArray<ApexStateDTO> states))
                    return false;

                if (!states.IsCreated || (uint)index >= (uint)states.Length)
                    return false;

                states[index] = state;
                return true;
            }
            finally
            {
                ReleaseApexMutationGuard(vault, ApexStateMutationGuardMask);
            }
        }

        /// <summary>
        /// Resets one spawned apex slot.
        /// </summary>
        public static bool TryClearSpawnSlot(NativeArray<ApexStateDTO> states, int index)
        {
            if (!states.IsCreated || (uint)index >= (uint)states.Length)
                return false;

            states[index] = default;
            return true;
        }

        /// <summary>
        /// Builds the configured apex brain job and safe schedule length.
        /// </summary>
        public static bool TryCreateJob(
            in ApexBrainVaultBuffers buffers,
            uint frame,
            out ApexBrainJob job,
            out int scheduleLength)
        {
            job = default;
            scheduleLength = GetScheduleLength(in buffers);
            if (scheduleLength <= 0)
                return false;

            job.States = buffers.States;
            job.MockTargets = buffers.MockTargets;
            job.AcousticTaps = buffers.AcousticTaps;
            job.Tuning = buffers.Tuning;
            job.EmergencyStats = buffers.EmergencyStats;
            job.WorldSampler = buffers.WorldSampler;
            job.Outputs = buffers.Outputs;
            job.ProximitySignals = buffers.ProximitySignals;
            job.CombatDamageSignals = buffers.CombatDamageSignals;
            job.PanicSignals = buffers.PanicSignals;
            job.InfluenceNodes = buffers.InfluenceNodes;
            job.AmbushNodeScratch = buffers.AmbushNodeScratch;
            job.TelemetryRing = buffers.TelemetryRing;
            job.TargetCount = buffers.MockTargets.Length;
            job.AcousticTapCount = buffers.AcousticTaps.Length;
            job.Frame = frame;
            return true;
        }

        /// <summary>
        /// Creates a dependency-preserving schedule. Caller owns returned JobHandle and must not Complete mid-frame.
        /// </summary>
        public static bool TrySchedule(
            in ApexBrainVaultBuffers buffers,
            uint frame,
            JobHandle inputDependency,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!ShouldEvaluateFrame(in buffers, frame))
                return false;

            if (!TryCreateJob(in buffers, frame, out ApexBrainJob job, out int scheduleLength))
                return false;

            outputDependency = job.Schedule(scheduleLength, 1, inputDependency);
            return true;
        }

        /// <summary>
        /// Creates a dependency-preserving schedule with external bounded SignalBus MPSC writers attached by the owning core bridge.
        /// </summary>
        public static bool TryScheduleWithSignalWriters(
            in ApexBrainVaultBuffers buffers,
            uint frame,
            JobHandle inputDependency,
            global::Hecton8.Core.MpscSignalRingBuffer<ApexProximitySignal>.ParallelWriter proximityWriter,
            NativeArray<int> proximityWriterBudget,
            global::Hecton8.Core.MpscSignalRingBuffer<MockCombatDamageSignal>.ParallelWriter combatWriter,
            NativeArray<int> combatWriterBudget,
            global::Hecton8.Core.MpscSignalRingBuffer<ApexPanicSignal>.ParallelWriter panicWriter,
            NativeArray<int> panicWriterBudget,
            out JobHandle outputDependency)
        {
            outputDependency = inputDependency;
            if (!ShouldEvaluateFrame(in buffers, frame))
                return false;

            if (!TryCreateJob(in buffers, frame, out ApexBrainJob job, out int scheduleLength))
                return false;

            AttachSignalWriters(
                ref job,
                proximityWriter,
                proximityWriterBudget,
                combatWriter,
                combatWriterBudget,
                panicWriter,
                panicWriterBudget);
            outputDependency = job.Schedule(scheduleLength, 1, inputDependency);
            return true;
        }

        /// <summary>
        /// Continuous quality gate for scheduler owners: 5 Hz at survival quality, 60 Hz at full quality.
        /// </summary>
        public static bool ShouldEvaluateFrame(in ApexBrainVaultBuffers buffers, uint frame)
        {
            float quality = 1f;
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
                quality = ResolveSchedulingQuality(buffers.Tuning[0].GlobalQualityWeight);

            float qualityCurve = Smooth01(math.saturate((quality - ApexBrainConstants.MinimumQualityNodeHold) * math.rcp(1f - ApexBrainConstants.MinimumQualityNodeHold)));
            float updateHz = math.lerp(5f, 60f, qualityCurve);
            uint evaluationsPerWindow = (uint)math.clamp((int)math.round(updateHz), 5, 60);
            uint phase = (frame * evaluationsPerWindow) % 60u;
            return phase < evaluationsPerWindow;
        }

        /// <summary>
        /// Records the last written telemetry frame after the scheduled job is complete.
        /// </summary>
        public static bool TryRecordTelemetryHeartbeat(ApexBrainVaultBuffers buffers, uint frame)
        {
            if (!buffers.TelemetryCursor.IsCreated || buffers.TelemetryCursor.Length <= 0)
                return false;

            buffers.TelemetryCursor[0] = (int)(frame % ApexBrainConstants.TelemetryFrames);
            return true;
        }

        /// <summary>
        /// Records telemetry cursor and dumps black-box data immediately if the completed frame contains a fault row.
        /// </summary>
        public static bool TryRecordTelemetryHeartbeat(ApexBrainVaultBuffers buffers, uint frame, string projectRoot)
        {
            bool recorded = TryRecordTelemetryHeartbeat(buffers, frame);
            if (recorded && !string.IsNullOrEmpty(projectRoot))
                TryDumpBlackBoxOnFrameFault(in buffers, frame, projectRoot);
            return recorded;
        }

        /// <summary>
        /// Attaches external bounded MPSC writers without adding concrete sibling-domain callbacks.
        /// </summary>
        public static void AttachSignalWriters(
            ref ApexBrainJob job,
            global::Hecton8.Core.MpscSignalRingBuffer<ApexProximitySignal>.ParallelWriter proximityWriter,
            NativeArray<int> proximityWriterBudget,
            global::Hecton8.Core.MpscSignalRingBuffer<MockCombatDamageSignal>.ParallelWriter combatWriter,
            NativeArray<int> combatWriterBudget,
            global::Hecton8.Core.MpscSignalRingBuffer<ApexPanicSignal>.ParallelWriter panicWriter,
            NativeArray<int> panicWriterBudget)
        {
            job.ProximitySignalWriter = proximityWriter;
            job.ProximitySignalWriterBudget = proximityWriterBudget;
            job.CombatDamageSignalWriter = combatWriter;
            job.CombatDamageSignalWriterBudget = combatWriterBudget;
            job.PanicSignalWriter = panicWriter;
            job.PanicSignalWriterBudget = panicWriterBudget;
            job.EnableSignalQueueWrites = 1;
        }

        /// <summary>
        /// Reads current tuning from unmanaged vault memory.
        /// </summary>
        public static bool TryGetTuning(IDataVault vault, ref ApexBrainVaultHandles handles, out ApexBrainTuning tuning)
        {
            tuning = default;
            if (vault == null ||
                handles.Tuning.BufferID == 0u ||
                handles.Tuning.Generation == 0u ||
                !vault.TryReadOnlyHandle(in handles.Tuning, out NativeArray<ApexBrainTuning>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length <= 0)
            {
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        /// <summary>
        /// Writes current tuning to unmanaged vault memory.
        /// </summary>
        public static bool TrySetTuning(IDataVault vault, ref ApexBrainVaultHandles handles, in ApexBrainTuning tuning)
        {
            if (vault == null ||
                handles.Tuning.BufferID != (uint)ApexBrainVaultBufferIds.Tuning ||
                handles.Tuning.Generation == 0u ||
                !TryAcquireApexMutationGuard(vault, ApexTuningMutationGuardMask))
            {
                return false;
            }

            try
            {
                if (!TryOpenVaultView(vault, in handles.Tuning, 1, out NativeArray<ApexBrainTuning> tuningBuffer))
                    return false;

                if (!tuningBuffer.IsCreated || tuningBuffer.Length <= 0)
                    return false;

                tuningBuffer[0] = SanitizeTuning(in tuning);
                return true;
            }
            finally
            {
                ReleaseApexMutationGuard(vault, ApexTuningMutationGuardMask);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Loads apex_predator_stats.csv into vault scratch and applies zero-allocation key-hash parsing.
        /// </summary>
        public static bool TryLoadCsvOverrides(IDataVault vault, ref ApexBrainVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.CsvScratch.IsCreated ||
                buffers.CsvScratch.Length <= 0 ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = TryGetLastWriteTicks(path);
            int length = ReadFileIntoNativeScratch(path, buffers.CsvScratch);
            if (length <= 0)
                return false;

            ApexBrainTuning tuning = buffers.Tuning[0];
            bool changed = TryApplyCsvOverrides(buffers.CsvScratch, length, ref tuning);
            if (changed)
            {
                tuning.LastCsvHash = HashBytes(buffers.CsvScratch, length);
                tuning.CsvReloadVersion++;
                tuning.LastCsvWriteTicks = writeTicks;
                buffers.Tuning[0] = SanitizeTuning(in tuning);
            }

            return changed;
        }

        /// <summary>
        /// Timestamp-gated CSV polling for editor/runtime owners that need human tuning without recompilation.
        /// </summary>
        public static bool TryPollCsvOverrides(IDataVault vault, ref ApexBrainVaultHandles handles, string projectRoot)
        {
            if (!TryResolveViews(vault, ref handles, out ApexBrainVaultBuffers buffers) ||
                !buffers.Tuning.IsCreated ||
                buffers.Tuning.Length <= 0)
            {
                return false;
            }

            string path = ResolveCsvPath(projectRoot);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            ulong writeTicks = TryGetLastWriteTicks(path);
            if (writeTicks == 0UL || buffers.Tuning[0].LastCsvWriteTicks == writeTicks)
                return false;

            return TryLoadCsvOverrides(vault, ref handles, projectRoot);
        }

        /// <summary>
        /// Parses CSV bytes as key,value rows. The parser hashes ASCII keys and does not allocate.
        /// </summary>
        public static bool TryApplyCsvOverrides(NativeArray<byte> bytes, int length, ref ApexBrainTuning tuning)
        {
            if (!bytes.IsCreated || length <= 0)
                return false;

            bool changed = false;
            int limit = math.min(length, bytes.Length);
            int index = 0;
            while (index < limit)
            {
                SkipWhitespaceAndLineBreaks(bytes, limit, ref index);
                if (index >= limit)
                    break;

                if (bytes[index] == (byte)'#')
                {
                    SkipLine(bytes, limit, ref index);
                    continue;
                }

                uint keyHash = 2166136261u;
                int keyLength = 0;
                while (index < limit && bytes[index] != (byte)',' && bytes[index] != (byte)'=' && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                {
                    byte c = ToLowerAscii(bytes[index]);
                    if (c > (byte)' ')
                    {
                        keyHash = (keyHash ^ c) * 16777619u;
                        keyLength++;
                    }

                    index++;
                }

                if (index < limit && (bytes[index] == (byte)',' || bytes[index] == (byte)'='))
                    index++;

                if (keyLength > 0 && TryParseFloat(bytes, limit, ref index, out float value))
                    changed |= ApplyCsvValue(keyHash, value, ref tuning);

                SkipLine(bytes, limit, ref index);
            }

            return changed;
        }
#endif

        /// <summary>
        /// Confirms the in-memory telemetry ring is available on fault; disk dumps are forbidden in runtime.
        /// </summary>
        public static bool TryDumpBlackBox(in ApexBrainVaultBuffers buffers, string projectRoot)
        {
            _ = projectRoot;
            return buffers.TelemetryRing.IsCreated && buffers.TelemetryRing.Length > 0;
        }

        /// <summary>
        /// Scans one telemetry frame for faults and dumps the ring if needed.
        /// </summary>
        public static bool TryDumpBlackBoxOnFrameFault(in ApexBrainVaultBuffers buffers, uint frame, string projectRoot)
        {
            if (!buffers.TelemetryRing.IsCreated || buffers.TelemetryRing.Length <= 0)
                return false;

            int frameIndex = (int)(frame % ApexBrainConstants.TelemetryFrames);
            int start = frameIndex * ApexBrainConstants.MaxLeviathans;
            int end = math.min(start + ApexBrainConstants.MaxLeviathans, buffers.TelemetryRing.Length);
            for (int i = start; i < end; i++)
            {
                ApexTelemetryEntry entry = buffers.TelemetryRing[i];
                if (entry.Frame == frame && (entry.Flags & ApexBrainFlags.Fault) != 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Validates byte layouts required by the prompt.
        /// </summary>
        public static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<ApexStateDTO>() == 64 &&
                   UnsafeUtility.SizeOf<MockPlayerAUP>() == 128 &&
                   UnsafeUtility.SizeOf<ApexBrainAcousticEchoTap>() == 64 &&
                   UnsafeUtility.SizeOf<MockWorldSampler>() == 64 &&
                   UnsafeUtility.SizeOf<ApexBrainTuning>() == 128 &&
                   UnsafeUtility.SizeOf<ApexEmergencyStats>() == 64 &&
                   UnsafeUtility.SizeOf<ApexInfluenceNode>() == 64 &&
                   UnsafeUtility.SizeOf<ApexBrainOutputDTO>() == 192 &&
                   UnsafeUtility.SizeOf<ApexTelemetryEntry>() == 128 &&
                   UnsafeUtility.SizeOf<ApexProximitySignal>() == 64 &&
                   UnsafeUtility.SizeOf<MockCombatDamageSignal>() == 64 &&
                   UnsafeUtility.SizeOf<ApexPanicSignal>() == 64;
        }

        public static ApexBrainTuning BuildEmergencyMockTuning()
        {
            return ApexBrainDefaults.BuildEmergencyMockTuning();
        }

        public static ApexEmergencyStats BuildEmergencyMockStats()
        {
            return ApexBrainDefaults.BuildEmergencyMockStats();
        }

        public static MockWorldSampler BuildEmergencyMockWorldSampler()
        {
            return ApexBrainDefaults.BuildEmergencyMockWorldSampler();
        }

        private static bool TryReadExistingHandles(IDataVault vault, out ApexBrainVaultHandles handles)
        {
            handles = default;
            bool acquired =
                vault.TryGetGenerationHandle<ApexStateDTO>(ApexBrainVaultBufferIds.ApexState, out handles.States) &&
                vault.TryGetGenerationHandle<MockPlayerAUP>(ApexBrainVaultBufferIds.MockPlayerAup, out handles.MockTargets) &&
                vault.TryGetGenerationHandle<ApexBrainAcousticEchoTap>(ApexBrainVaultBufferIds.AcousticEchoTap, out handles.AcousticTaps) &&
                vault.TryGetGenerationHandle<ApexBrainTuning>(ApexBrainVaultBufferIds.Tuning, out handles.Tuning) &&
                vault.TryGetGenerationHandle<ApexEmergencyStats>(ApexBrainVaultBufferIds.EmergencyStats, out handles.EmergencyStats) &&
                vault.TryGetGenerationHandle<MockWorldSampler>(ApexBrainVaultBufferIds.MockWorldSampler, out handles.WorldSampler) &&
                vault.TryGetGenerationHandle<ApexBrainOutputDTO>(ApexBrainVaultBufferIds.Output, out handles.Outputs) &&
                vault.TryGetGenerationHandle<ApexProximitySignal>(ApexBrainVaultBufferIds.ProximitySignal, out handles.ProximitySignals) &&
                vault.TryGetGenerationHandle<MockCombatDamageSignal>(ApexBrainVaultBufferIds.CombatDamageSignal, out handles.CombatDamageSignals) &&
                vault.TryGetGenerationHandle<ApexPanicSignal>(ApexBrainVaultBufferIds.PanicSignal, out handles.PanicSignals) &&
                vault.TryGetGenerationHandle<ApexInfluenceNode>(ApexBrainVaultBufferIds.InfluenceNodes, out handles.InfluenceNodes);

            // AmbushNodeScratch, TelemetryRing, TelemetryCursor and CsvScratch have no reserved
            // BufferID member (see ApexBrainVaultBufferIds). Reading them by their old aliases probed
            // dispatcher-owned buffers 70626-70629, so those probes are removed and the handles stay
            // default. IsCreated() consequently reports false for every caller of this method.
            return acquired;
        }

        public static void ReleaseOwnedHandles(IDataVault vault, ref ApexBrainVaultHandles handles)
        {
            if (vault == null)
            {
                handles = default;
                return;
            }

            ReleaseVaultHandle(vault, ref handles.States);
            ReleaseVaultHandle(vault, ref handles.MockTargets);
            ReleaseVaultHandle(vault, ref handles.AcousticTaps);
            ReleaseVaultHandle(vault, ref handles.Tuning);
            ReleaseVaultHandle(vault, ref handles.EmergencyStats);
            ReleaseVaultHandle(vault, ref handles.WorldSampler);
            ReleaseVaultHandle(vault, ref handles.Outputs);
            ReleaseVaultHandle(vault, ref handles.ProximitySignals);
            ReleaseVaultHandle(vault, ref handles.CombatDamageSignals);
            ReleaseVaultHandle(vault, ref handles.PanicSignals);
            ReleaseVaultHandle(vault, ref handles.InfluenceNodes);
            ReleaseVaultHandle(vault, ref handles.AmbushNodeScratch);
            ReleaseVaultHandle(vault, ref handles.TelemetryRing);
            ReleaseVaultHandle(vault, ref handles.TelemetryCursor);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref handles.CsvScratch);
#endif
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool TryOpenVaultView<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   requiredLength >= 0 &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryAcquireApexMutationGuard(IDataVault vault, ulong guardMask)
        {
            return vault != null &&
                   guardMask != 0UL &&
                   !vault.IsCompactionFenceActive &&
                   vault.TryAcquireMutationGuard(guardMask);
        }

        private static void ReleaseApexMutationGuard(IDataVault vault, ulong guardMask)
        {
            if (guardMask != 0UL)
                vault?.ReleaseMutationGuard(guardMask);
        }

        private static ulong ApexVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static int GetScheduleLength(in ApexBrainVaultBuffers buffers)
        {
            if (!buffers.IsCreated())
                return 0;

            int length = math.min(buffers.States.Length, buffers.MockTargets.Length);
            length = math.min(length, buffers.Outputs.Length);
            length = math.min(length, buffers.ProximitySignals.Length);
            length = math.min(length, buffers.CombatDamageSignals.Length);
            length = math.min(length, buffers.PanicSignals.Length);
            length = math.min(length, buffers.AmbushNodeScratch.Length / ApexBrainConstants.MaxAmbushNodes);
            length = math.min(length, ApexBrainConstants.MaxLeviathans);
            return math.max(0, length);
        }

        private static void GenerateEmergencyMockApexStats(ApexBrainVaultBuffers buffers)
        {
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
            {
                ApexBrainTuning tuning = buffers.Tuning[0];
                if (!math.isfinite(tuning.LeviathanSpeed) || tuning.LeviathanSpeed <= 0f)
                {
                    ClearRuntimeRows(in buffers);
                    buffers.Tuning[0] = BuildEmergencyMockTuning();
                }
            }

            if (buffers.EmergencyStats.IsCreated && buffers.EmergencyStats.Length > 0)
                buffers.EmergencyStats[0] = BuildEmergencyMockStats();
            if (buffers.WorldSampler.IsCreated && buffers.WorldSampler.Length > 0)
                buffers.WorldSampler[0] = BuildEmergencyMockWorldSampler();
        }

        private static void ClearRuntimeRows(in ApexBrainVaultBuffers buffers)
        {
            MemClearArray(buffers.States);
            MemClearArray(buffers.MockTargets);
            MemClearArray(buffers.AcousticTaps);
            MemClearArray(buffers.Outputs);
            MemClearArray(buffers.ProximitySignals);
            MemClearArray(buffers.CombatDamageSignals);
            MemClearArray(buffers.PanicSignals);
            MemClearArray(buffers.InfluenceNodes);
            MemClearArray(buffers.AmbushNodeScratch);
            MemClearArray(buffers.TelemetryRing);
            MemClearArray(buffers.TelemetryCursor);
#if UNITY_EDITOR
            MemClearArray(buffers.CsvScratch);
#endif
        }

        private static void MemClearArray<T>(NativeArray<T> array)
            where T : unmanaged
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] = default;
        }

        private static ApexBrainTuning SanitizeTuning(in ApexBrainTuning input)
        {
            ApexBrainTuning fallback = BuildEmergencyMockTuning();
            ApexBrainTuning tuning = input;
            tuning.AggressionMultiplier = SanitizeRange(tuning.AggressionMultiplier, fallback.AggressionMultiplier, 0.01f, 8f);
            tuning.AcousticSensitivity = SanitizeRange(tuning.AcousticSensitivity, fallback.AcousticSensitivity, 0.01f, 8f);
            tuning.TurnRate = SanitizeRange(tuning.TurnRate, fallback.TurnRate, 0.01f, 4f);
            tuning.StalkingDistance = SanitizeRange(tuning.StalkingDistance, fallback.StalkingDistance, 8f, 600f);
            tuning.LeviathanSpeed = SanitizeRange(tuning.LeviathanSpeed, fallback.LeviathanSpeed, 1f, 120f);
            tuning.TerrorRadius = SanitizeRange(tuning.TerrorRadius, fallback.TerrorRadius, 16f, 1200f);
            tuning.BaseDamageMagnitude = SanitizeRange(tuning.BaseDamageMagnitude, fallback.BaseDamageMagnitude, 1f, 10000f);
            tuning.BiomeAggressionMultiplier = SanitizeRange(tuning.BiomeAggressionMultiplier, fallback.BiomeAggressionMultiplier, 1f, 8f);
            tuning.GlobalQualityWeight = math.saturate(math.select(fallback.GlobalQualityWeight, tuning.GlobalQualityWeight, math.isfinite(tuning.GlobalQualityWeight)));
            tuning.SimulationTickDelta = SanitizeRange(tuning.SimulationTickDelta, fallback.SimulationTickDelta, 1f / 120f, 0.25f);
            tuning.StrikeDistance = SanitizeRange(tuning.StrikeDistance, fallback.StrikeDistance, 4f, 240f);
            tuning.HeadOffsetMeters = SanitizeRange(tuning.HeadOffsetMeters, fallback.HeadOffsetMeters, 1f, 160f);
            tuning.MidOffsetMeters = SanitizeRange(tuning.MidOffsetMeters, fallback.MidOffsetMeters, 1f, 160f);
            tuning.TailOffsetMeters = SanitizeRange(tuning.TailOffsetMeters, fallback.TailOffsetMeters, 1f, 220f);
            tuning.NoiseAggroGain = SanitizeRange(tuning.NoiseAggroGain, fallback.NoiseAggroGain, 0f, 8f);
            tuning.StaminaRecoveryPerSecond = SanitizeRange(tuning.StaminaRecoveryPerSecond, fallback.StaminaRecoveryPerSecond, 0f, 4f);
            tuning.StaminaStrikeCost = math.saturate(math.select(fallback.StaminaStrikeCost, tuning.StaminaStrikeCost, math.isfinite(tuning.StaminaStrikeCost)));
            tuning.SweetLieShadowGain = SanitizeRange(tuning.SweetLieShadowGain, fallback.SweetLieShadowGain, 0f, 8f);
            tuning.SweetLieViewDotThreshold = math.saturate(math.select(fallback.SweetLieViewDotThreshold, tuning.SweetLieViewDotThreshold, math.isfinite(tuning.SweetLieViewDotThreshold)));
            tuning.AmbushNodeRadiusMeters = SanitizeRange(tuning.AmbushNodeRadiusMeters, fallback.AmbushNodeRadiusMeters, 2f, 512f);
            tuning.VisualOverkillGain = SanitizeRange(tuning.VisualOverkillGain, fallback.VisualOverkillGain, 0f, 8f);
            tuning.BiteHeadLocalOffset = SanitizeRange(tuning.BiteHeadLocalOffset, fallback.BiteHeadLocalOffset, 0f, 80f);
            if (tuning.PreferredBiomeHash == 0u)
                tuning.PreferredBiomeHash = fallback.PreferredBiomeHash;
            if (tuning.SourceHash == 0u)
                tuning.SourceHash = fallback.SourceHash;
            return tuning;
        }

#if UNITY_EDITOR
        private static bool ApplyCsvValue(uint keyHash, float value, ref ApexBrainTuning tuning)
        {
            if (!math.isfinite(value))
                return false;

            if (keyHash == _aggressionMultiplierHash)
                tuning.AggressionMultiplier = value;
            else if (keyHash == _acousticSensitivityHash)
                tuning.AcousticSensitivity = value;
            else if (keyHash == _turnRateHash)
                tuning.TurnRate = value;
            else if (keyHash == _stalkingDistanceHash)
                tuning.StalkingDistance = value;
            else if (keyHash == _leviathanSpeedHash)
                tuning.LeviathanSpeed = value;
            else if (keyHash == _terrorRadiusHash)
                tuning.TerrorRadius = value;
            else if (keyHash == _baseDamageMagnitudeHash)
                tuning.BaseDamageMagnitude = value;
            else if (keyHash == _biomeAggressionHash)
                tuning.BiomeAggressionMultiplier = value;
            else if (keyHash == _simulationTickDeltaHash)
                tuning.SimulationTickDelta = value;
            else if (keyHash == _strikeDistanceHash)
                tuning.StrikeDistance = value;
            else if (keyHash == _headOffsetMetersHash)
                tuning.HeadOffsetMeters = value;
            else if (keyHash == _midOffsetMetersHash)
                tuning.MidOffsetMeters = value;
            else if (keyHash == _tailOffsetMetersHash)
                tuning.TailOffsetMeters = value;
            else if (keyHash == _noiseAggroGainHash)
                tuning.NoiseAggroGain = value;
            else if (keyHash == _staminaRecoveryPerSecondHash)
                tuning.StaminaRecoveryPerSecond = value;
            else if (keyHash == _staminaStrikeCostHash)
                tuning.StaminaStrikeCost = math.saturate(value);
            else if (keyHash == _sweetLieShadowGainHash)
                tuning.SweetLieShadowGain = value;
            else if (keyHash == _sweetLieViewDotThresholdHash)
                tuning.SweetLieViewDotThreshold = math.saturate(value);
            else if (keyHash == _ambushNodeRadiusMetersHash)
                tuning.AmbushNodeRadiusMeters = value;
            else if (keyHash == _visualOverkillGainHash)
                tuning.VisualOverkillGain = value;
            else if (keyHash == _biteHeadLocalOffsetHash)
                tuning.BiteHeadLocalOffset = value;
            else if (keyHash == _globalQualityHash)
                tuning.GlobalQualityWeight = math.saturate(value);
            else
                return false;

            return true;
        }

        private static string ResolveCsvPath(string projectRoot)
        {
            try
            {
                string root = string.IsNullOrEmpty(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
                string sourceDataPath = Path.Combine(root, "Assets", "_SourceData", "AI", CsvFileName);
                if (File.Exists(sourceDataPath))
                    return sourceDataPath;

                string dataPath = Path.Combine(root, "Data", "AI", CsvFileName);
                if (File.Exists(dataPath))
                    return dataPath;

                string rootPath = Path.Combine(root, CsvFileName);
                if (File.Exists(rootPath))
                    return rootPath;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

            return null;
        }

        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int max = (int)math.min(scratch.Length, stream.Length);
                    if (max <= 0)
                        return 0;

                    unsafe
                    {
                        Span<byte> span = new Span<byte>(scratch.GetUnsafePtr(), max);
                        return stream.Read(span);
                    }
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (NotSupportedException)
            {
                return 0;
            }
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int limit, ref int index, out float value)
        {
            value = 0f;
            SkipSpaces(bytes, limit, ref index);
            if (index >= limit)
                return false;

            float sign = 1f;
            if (bytes[index] == (byte)'-' || bytes[index] == (byte)'+')
            {
                sign = bytes[index] == (byte)'-' ? -1f : 1f;
                index++;
            }

            float integer = 0f;
            int digitCount = 0;
            while (index < limit && IsDigit(bytes[index]))
            {
                integer = (integer * 10f) + (bytes[index] - (byte)'0');
                index++;
                digitCount++;
            }

            float fraction = 0f;
            float place = 0.1f;
            if (index < limit && bytes[index] == (byte)'.')
            {
                index++;
                while (index < limit && IsDigit(bytes[index]))
                {
                    fraction += (bytes[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                    digitCount++;
                }
            }

            value = (integer + fraction) * sign;
            return digitCount > 0 && math.isfinite(value);
        }

        private static void SkipWhitespaceAndLineBreaks(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] <= (byte)' ')
                index++;
        }

        private static void SkipSpaces(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && (bytes[index] == (byte)' ' || bytes[index] == (byte)'\t'))
                index++;
        }

        private static void SkipLine(NativeArray<byte> bytes, int limit, ref int index)
        {
            while (index < limit && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;
            while (index < limit && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static byte ToLowerAscii(byte c)
        {
            return c >= (byte)'A' && c <= (byte)'Z' ? (byte)(c + 32) : c;
        }

        private static bool IsDigit(byte c)
        {
            return c >= (byte)'0' && c <= (byte)'9';
        }

        private static uint HashAscii(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = (hash ^ ToLowerAscii((byte)value[i])) * 16777619u;
            return hash;
        }

        private static uint HashBytes(NativeArray<byte> bytes, int length)
        {
            uint hash = 2166136261u;
            int limit = math.min(length, bytes.IsCreated ? bytes.Length : 0);
            for (int i = 0; i < limit; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return hash;
        }

        private static ulong TryGetLastWriteTicks(string path)
        {
            try
            {
                return (ulong)File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch (IOException)
            {
                return 0UL;
            }
            catch (UnauthorizedAccessException)
            {
                return 0UL;
            }
            catch (ArgumentException)
            {
                return 0UL;
            }
            catch (NotSupportedException)
            {
                return 0UL;
            }
        }
#endif

        private static float ResolveSchedulingQuality(float value)
        {
            return math.saturate(math.select(1f, value, math.isfinite(value)));
        }

        private static float Smooth01(float value)
        {
            value = math.saturate(value);
            return value * value * (3f - (2f * value));
        }

        private static float SanitizeRange(float value, float fallback, float min, float max)
        {
            float selected = math.select(fallback, value, math.isfinite(value));
            return math.clamp(selected, min, math.max(min, max));
        }

        private static uint ToLittleEndianMarker(uint value)
        {
            return BitConverter.IsLittleEndian ? value : ReverseBytes(value);
        }

        private static uint ReverseBytes(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }
    }
}
