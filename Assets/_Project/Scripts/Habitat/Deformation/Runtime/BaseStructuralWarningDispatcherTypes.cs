using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Habitat.Deformation
{
    public static class BaseStructuralWarningConstants
    {
        public const int MaxRawWarnings = StructuralIntegrityConstants.MaxNodeCapacity;
        public const int MaxGroupedWarnings = 64;
        public const int SectorTimerCapacity = 128;
        public const int TelemetryFrameCapacity = 300;
        public const int CounterBaseCapacity = 8;
        public const int CounterGroupCountsStart = 8;
        public const int CounterCapacity = CounterGroupCountsStart + MaxGroupedWarnings;
        public const int AlarmProfileCapacity = 16;
        public const int CsvScratchBytes = 16 * 1024;
        public const int SignalCapacity = 64;
        public const int MaxFrameSignals = 64;
        public const int LowTierFrameSignals = 8;
        public const uint SignalLaneHash = 0x42535744u; // BSWD
        public const uint DumpMagic = 0x42535744u; // BSWD
        public const uint DumpVersion = 1u;
        public const uint AgentHash = 0x53333339u; // S339

        public const float DefaultStressThreshold01 = 0.78f;
        public const float DefaultCooldownSeconds = 2.0f;
        public const float DefaultMinClusterRadiusMeters = 5.0f;
        public const float DefaultMaxClusterRadiusMeters = 100.0f;
        public const float DefaultRedAlertStress01 = 0.95f;
        public const float DefaultTelemetryBudgetMicroseconds = 200.0f;

        public const uint RawFlagActive = 1u << 0;
        public const uint RawFlagRedAlert = 1u << 1;
        public const uint RawFlagNonFinite = 1u << 31;

        public const uint GroupFlagRedAlert = 1u << 0;
        public const uint GroupFlagNonFinite = 1u << 1;
        public const uint GroupFlagThrottled = 1u << 2;
        public const uint GroupFlagHypoxiaPanicCandidate = 1u << 3;

        public const uint TelemetryFlagNonFinite = 1u << 0;
        public const uint TelemetryFlagOverBudgetEstimate = 1u << 1;
        public const uint TelemetryFlagGroupOverflow = 1u << 2;
        public const uint TelemetryFlagTimerOverflow = 1u << 3;
        public const uint TelemetryFlagSignalDrop = 1u << 4;

        public const int CounterRawCount = 0;
        public const int CounterGroupCount = 1;
        public const int CounterEmittedCount = 2;
        public const int CounterDroppedCount = 3;
        public const int CounterFaultFlags = 4;
        public const int CounterHighestStressBits = 5;
        public const int CounterEstimatedMicrosecondsBits = 6;
        public const int CounterRadiusMetersBits = 7;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RawWarningDTO
    {
        [FieldOffset(0)] public double3 WarningAUP;
        [FieldOffset(24)] public float Stress01;
        [FieldOffset(28)] public uint NodeHash;
        [FieldOffset(32)] public uint CriticalFlags;
        [FieldOffset(36)] public int ClusterIndex;
        [FieldOffset(40)] public int SourceIndex;
        [FieldOffset(44)] public uint Reserved0;
        [FieldOffset(48)] private ulong _padTail0;
        [FieldOffset(56)] private ulong _padTail1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct GroupedWarningDTO
    {
        [FieldOffset(0)] public double3 EpicenterAUP;
        [FieldOffset(24)] public float HighestStress01;
        [FieldOffset(28)] public uint CriticalFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BaseStructuralWarningTimerDTO
    {
        [FieldOffset(0)] public double3 SectorAUP;
        [FieldOffset(24)] public float LastWarningTimeSeconds;
        [FieldOffset(28)] public uint SectorHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseStructuralWarningTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int ActiveNodeCount;
        [FieldOffset(8)] public int RawWarningCount;
        [FieldOffset(12)] public int GroupedWarningCount;
        [FieldOffset(16)] public int EmittedWarningCount;
        [FieldOffset(20)] public int DroppedWarningCount;
        [FieldOffset(24)] public float HighestStress01;
        [FieldOffset(28)] public float ClusterRadiusMeters;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float EstimatedMicroseconds;
        [FieldOffset(40)] public uint FaultFlags;
        [FieldOffset(44)] public uint BaseHash;
        [FieldOffset(48)] public uint Sequence;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public uint LastSectorHash;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BaseStructuralWarningDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint EntrySize;
        [FieldOffset(16)] public int EntryCount;
        [FieldOffset(20)] public int Cursor;
        [FieldOffset(24)] public uint FaultFlags;
        [FieldOffset(28)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BaseStructuralWarningTuningDTO
    {
        [FieldOffset(0)] public float StressThreshold01;
        [FieldOffset(4)] public float CooldownSeconds;
        [FieldOffset(8)] public float MinClusterRadiusMeters;
        [FieldOffset(12)] public float MaxClusterRadiusMeters;
        [FieldOffset(16)] public float RedAlertStress01;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public float PanicStressScale;
        [FieldOffset(28)] public float AudioIntensityScale;
        [FieldOffset(32)] public float VisualIntensityScale;
        [FieldOffset(36)] public float TelemetryBudgetMicroseconds;
        [FieldOffset(40)] public uint BaseHash;
        [FieldOffset(44)] public int ActiveProfileCount;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint Reserved0;
        [FieldOffset(56)] private ulong _padTail0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BaseAlarmProfileDTO
    {
        [FieldOffset(0)] public uint FailureTypeHash;
        [FieldOffset(4)] public uint SoundProfileHash;
        [FieldOffset(8)] public float VisualIntensityScale;
        [FieldOffset(12)] public float AudioIntensityScale;
        [FieldOffset(16)] public float PanicScale;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _padTail0;
    }

    public static class BaseStructuralWarningLayout
    {
        public static bool Validate()
        {
            bool sizeValid =
                UnsafeUtility.SizeOf<RawWarningDTO>() == 64 &&
                UnsafeUtility.SizeOf<GroupedWarningDTO>() == 32 &&
                UnsafeUtility.SizeOf<BaseStructuralWarningTimerDTO>() == 32 &&
                UnsafeUtility.SizeOf<BaseStructuralWarningTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<BaseStructuralWarningDumpHeader>() == 32 &&
                UnsafeUtility.SizeOf<BaseStructuralWarningTuningDTO>() == 64 &&
                UnsafeUtility.SizeOf<BaseAlarmProfileDTO>() == 32 &&
                UnsafeUtility.SizeOf<BaseStructuralWarningSignal>() == 64;
#if UNITY_EDITOR
            return sizeValid &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.WarningAUP)) == 0 &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.Stress01)) == 24 &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.NodeHash)) == 28 &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.CriticalFlags)) == 32 &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.ClusterIndex)) == 36 &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.SourceIndex)) == 40 &&
                   Offset<RawWarningDTO>(nameof(RawWarningDTO.Reserved0)) == 44 &&
                   Offset<GroupedWarningDTO>(nameof(GroupedWarningDTO.EpicenterAUP)) == 0 &&
                   Offset<GroupedWarningDTO>(nameof(GroupedWarningDTO.HighestStress01)) == 24 &&
                   Offset<GroupedWarningDTO>(nameof(GroupedWarningDTO.CriticalFlags)) == 28 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.EpicenterAup)) == 0 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.BaseHash)) == 40 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.Frame)) == 44 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.HighestStress01)) == 48 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.AudioIntensity01)) == 52 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.PanicScalar01)) == 56 &&
                   Offset<BaseStructuralWarningSignal>(nameof(BaseStructuralWarningSignal.CriticalFlags)) == 60;
#else
            return sizeValid;
#endif
        }

#if UNITY_EDITOR
        private static int Offset<T>(string fieldName)
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct BaseStructuralWarningColdInitJob : IJob
    {
        [WriteOnly] [NoAlias] public NativeArray<RawWarningDTO> RawWarnings;
        [WriteOnly] [NoAlias] public NativeArray<GroupedWarningDTO> Groups;
        [WriteOnly] [NoAlias] public NativeArray<BaseStructuralWarningTimerDTO> Timers;
        [WriteOnly] [NoAlias] public NativeArray<int> Counters;
        [WriteOnly] [NoAlias] public NativeArray<BaseStructuralWarningTelemetryEntry> Telemetry;
        [WriteOnly] [NoAlias] public NativeArray<int> TelemetryCursor;

        public void Execute()
        {
            Clear(RawWarnings);
            Clear(Groups);
            if (Timers.IsCreated)
            {
                for (int i = 0; i < Timers.Length; i++)
                {
                    Timers[i] = new BaseStructuralWarningTimerDTO
                    {
                        SectorAUP = double3.zero,
                        LastWarningTimeSeconds = -1000000f,
                        SectorHash = 0u
                    };
                }
            }
            Clear(Counters);
            Clear(Telemetry);
            Clear(TelemetryCursor);
        }

        private static void Clear<T>(NativeArray<T> array) where T : unmanaged
        {
            if (!array.IsCreated || array.Length == 0)
                return;

            for (int i = 0; i < array.Length; i++)
                array[i] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateMockStressSpikeJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [NoAlias] public NativeArray<double3> NodeAups;
        public int SpikeCount;
        public int ActiveNodeCount;
        public double3 CenterAup;
        public float PeakStress01;
        public uint BaseHash;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount || index >= States.Length || index >= NodeAups.Length)
                return;

            int safeSpikeCount = math.clamp(SpikeCount, 1, ActiveNodeCount);
            int ring = math.max(1, (int)math.ceil(math.sqrt((float)safeSpikeCount)));
            int x = index % ring;
            int z = index / ring;
            bool inSpike = index < safeSpikeCount;
            double3 aup = CenterAup + new double3((x - ring * 0.5d) * 1.75d, 0d, z * 1.75d);
            float stress = inSpike ? math.saturate(PeakStress01 - (index * 0.0005f)) : 0.12f;
            uint nodeHash = math.hash(new uint3((uint)index + 1u, BaseHash, 0x53333339u));
            if (nodeHash == 0u)
                nodeHash = (uint)index + 1u;

            NodeAups[index] = aup;
            ref IntegrityStateDTO state = ref IntegrityStateDTO.AsRef(States, index);
            state.NodeHash = nodeHash;
            state.BaseStrength = math.max(1f, state.BaseStrength);
            state.CurrentStress = stress;
            state.AppliedPressure = math.max(0f, state.AppliedPressure);
            state.BucklingScalar = math.saturate(stress);
            state.Flags &= ~(StructuralIntegrityConstants.StateFlagWarn80Emitted |
                             StructuralIntegrityConstants.StateFlagWarn90Emitted |
                             StructuralIntegrityConstants.StateFlagLeakEmitted |
                             StructuralIntegrityConstants.StateFlagCollapsed |
                             StructuralIntegrityConstants.StateFlagNonFinite);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct EvaluateStructuralStressJob : IJobParallelFor
    {
        [ReadOnly] [NoAlias] public NativeArray<IntegrityStateDTO> States;
        [ReadOnly] [NoAlias] public NativeArray<double3> NodeAups;
        [WriteOnly] [NoAlias] public NativeArray<RawWarningDTO> RawWarnings;
        [ReadOnly] [NoAlias] public NativeArray<BaseStructuralWarningTuningDTO> WarningTuning;
        public int ActiveNodeCount;

        public void Execute(int index)
        {
            if ((uint)index >= (uint)ActiveNodeCount || index >= States.Length || index >= NodeAups.Length || index >= RawWarnings.Length)
                return;

            IntegrityStateDTO state = States[index];
            BaseStructuralWarningTuningDTO tuning = WarningTuning[0];
            float threshold = math.saturate(FiniteOr(tuning.StressThreshold01, BaseStructuralWarningConstants.DefaultStressThreshold01));
            float redAlertStress = math.saturate(FiniteOr(tuning.RedAlertStress01, BaseStructuralWarningConstants.DefaultRedAlertStress01));
            float stress = math.isfinite(state.CurrentStress) ? math.saturate(state.CurrentStress) : 1f;
            double3 aup = NodeAups[index];
            bool finiteAup = math.all(math.isfinite(aup));
            uint flags = 0u;
            if (!finiteAup || !math.isfinite(state.CurrentStress))
                flags |= BaseStructuralWarningConstants.RawFlagNonFinite;
            if (stress >= redAlertStress)
                flags |= BaseStructuralWarningConstants.RawFlagRedAlert;

            if (stress < threshold && (flags & BaseStructuralWarningConstants.RawFlagNonFinite) == 0u)
            {
                RawWarnings[index] = default;
                return;
            }

            RawWarnings[index] = new RawWarningDTO
            {
                WarningAUP = finiteAup ? aup : double3.zero,
                Stress01 = stress,
                NodeHash = state.NodeHash,
                CriticalFlags = flags | BaseStructuralWarningConstants.RawFlagActive,
                ClusterIndex = -1,
                SourceIndex = index,
                Reserved0 = 0u
            };
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct CoalesceWarningsJob : IJob
    {
        [NoAlias] public NativeArray<RawWarningDTO> RawWarnings;
        [NoAlias] public NativeArray<GroupedWarningDTO> Groups;
        [NoAlias] public NativeArray<int> Counters;
        [ReadOnly] [NoAlias] public NativeArray<BaseStructuralWarningTuningDTO> WarningTuning;
        public int ActiveNodeCount;
        public float GlobalQualityWeight;
        public float EstimatedMicroseconds;

        public void Execute()
        {
            if (!RawWarnings.IsCreated || !Groups.IsCreated || !Counters.IsCreated || Counters.Length < BaseStructuralWarningConstants.CounterCapacity ||
                !WarningTuning.IsCreated || WarningTuning.Length == 0)
            {
                return;
            }

            for (int i = 0; i < Counters.Length; i++)
                Counters[i] = 0;
            for (int i = 0; i < Groups.Length; i++)
                Groups[i] = default;

            BaseStructuralWarningTuningDTO tuning = WarningTuning[0];
            float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : tuning.GlobalQualityWeight);
            float minRadius = math.max(0.25f, FiniteOr(tuning.MinClusterRadiusMeters, BaseStructuralWarningConstants.DefaultMinClusterRadiusMeters));
            float maxRadius = math.max(minRadius, FiniteOr(tuning.MaxClusterRadiusMeters, BaseStructuralWarningConstants.DefaultMaxClusterRadiusMeters));
            float radius = math.lerp(minRadius, maxRadius, 1.0f - quality);
            double radiusSq = (double)radius * radius;
            int safeCount = math.clamp(ActiveNodeCount, 0, RawWarnings.Length);
            int rawCount = 0;
            int groupCount = 0;
            float highestStress = 0f;
            uint faultFlags = 0u;

            for (int i = 0; i < safeCount; i++)
            {
                RawWarningDTO raw = RawWarnings[i];
                if ((raw.CriticalFlags & BaseStructuralWarningConstants.RawFlagActive) == 0u)
                    continue;

                rawCount++;
                highestStress = math.max(highestStress, raw.Stress01);
                if ((raw.CriticalFlags & BaseStructuralWarningConstants.RawFlagNonFinite) != 0u)
                    faultFlags |= BaseStructuralWarningConstants.TelemetryFlagNonFinite;

                int selectedGroup = -1;
                for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
                {
                    int includedCount = math.max(1, Counters[BaseStructuralWarningConstants.CounterGroupCountsStart + groupIndex]);
                    GroupedWarningDTO existing = Groups[groupIndex];
                    double3 center = existing.EpicenterAUP / (double)includedCount;
                    double3 delta = raw.WarningAUP - center;
                    if (!math.all(math.isfinite(delta)))
                        continue;
                    if (math.dot(delta, delta) <= radiusSq)
                    {
                        selectedGroup = groupIndex;
                        break;
                    }
                }

                if (selectedGroup < 0)
                {
                    if (groupCount >= Groups.Length)
                    {
                        raw.ClusterIndex = -2;
                        RawWarnings[i] = raw;
                        faultFlags |= BaseStructuralWarningConstants.TelemetryFlagGroupOverflow;
                        continue;
                    }

                    selectedGroup = groupCount++;
                    Counters[BaseStructuralWarningConstants.CounterGroupCountsStart + selectedGroup] = 0;
                    Groups[selectedGroup] = default;
                }

                int countIndex = BaseStructuralWarningConstants.CounterGroupCountsStart + selectedGroup;
                Counters[countIndex] = Counters[countIndex] + 1;
                GroupedWarningDTO group = Groups[selectedGroup];
                group.EpicenterAUP += raw.WarningAUP;
                group.HighestStress01 = math.max(group.HighestStress01, raw.Stress01);
                if ((raw.CriticalFlags & BaseStructuralWarningConstants.RawFlagRedAlert) != 0u)
                    group.CriticalFlags |= BaseStructuralWarningConstants.GroupFlagRedAlert;
                if ((raw.CriticalFlags & BaseStructuralWarningConstants.RawFlagNonFinite) != 0u)
                    group.CriticalFlags |= BaseStructuralWarningConstants.GroupFlagNonFinite;
                Groups[selectedGroup] = group;

                raw.ClusterIndex = selectedGroup;
                RawWarnings[i] = raw;
            }

            float redAlertStress = math.saturate(FiniteOr(tuning.RedAlertStress01, BaseStructuralWarningConstants.DefaultRedAlertStress01));
            for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
            {
                GroupedWarningDTO group = Groups[groupIndex];
                int included = math.max(1, Counters[BaseStructuralWarningConstants.CounterGroupCountsStart + groupIndex]);
                double3 epicenter = group.EpicenterAUP / (double)included;
                if (!math.all(math.isfinite(epicenter)))
                {
                    epicenter = double3.zero;
                    group.CriticalFlags |= BaseStructuralWarningConstants.GroupFlagNonFinite;
                    faultFlags |= BaseStructuralWarningConstants.TelemetryFlagNonFinite;
                }

                if (group.HighestStress01 >= redAlertStress)
                    group.CriticalFlags |= BaseStructuralWarningConstants.GroupFlagRedAlert;
                if (group.HighestStress01 >= 0.82f)
                    group.CriticalFlags |= BaseStructuralWarningConstants.GroupFlagHypoxiaPanicCandidate;

                group.EpicenterAUP = epicenter;
                group.HighestStress01 = math.saturate(group.HighestStress01);
                Groups[groupIndex] = group;
            }

            if (EstimatedMicroseconds > FiniteOr(tuning.TelemetryBudgetMicroseconds, BaseStructuralWarningConstants.DefaultTelemetryBudgetMicroseconds))
                faultFlags |= BaseStructuralWarningConstants.TelemetryFlagOverBudgetEstimate;

            Counters[BaseStructuralWarningConstants.CounterRawCount] = rawCount;
            Counters[BaseStructuralWarningConstants.CounterGroupCount] = groupCount;
            Counters[BaseStructuralWarningConstants.CounterFaultFlags] = unchecked((int)faultFlags);
            Counters[BaseStructuralWarningConstants.CounterHighestStressBits] = math.asint(highestStress);
            Counters[BaseStructuralWarningConstants.CounterEstimatedMicrosecondsBits] = math.asint(EstimatedMicroseconds);
            Counters[BaseStructuralWarningConstants.CounterRadiusMetersBits] = math.asint(radius);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct RouteStructuralWarningsJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<GroupedWarningDTO> Groups;
        [NoAlias] public NativeArray<BaseStructuralWarningTimerDTO> Timers;
        [NoAlias] public NativeArray<int> Counters;
        [ReadOnly] [NoAlias] public NativeArray<BaseStructuralWarningTuningDTO> WarningTuning;
        [ReadOnly] [NoAlias] public NativeArray<BaseAlarmProfileDTO> Profiles;
        // SAFETY_JUSTIFICATION_PARAGRAPH_1:
        // SignalBus owns the BaseStructuralWarningSignal queue lifetime and this job receives only a producer-side
        // ParallelWriter. Unity safety cannot model that external lane ownership, so the safety bypass is scoped to
        // the queue facade, not to the Vault arrays.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_2:
        // A managed event bridge was rejected for GC and cross-domain coupling. A post-job main-thread scan was
        // rejected because it would duplicate the warning pass and risk same-frame readback pressure.
        //
        // SAFETY_JUSTIFICATION_PARAGRAPH_3:
        // The scheduler creates this writer after SignalBus initialization, chains the route job into the dispatcher
        // handle, and consumers flush/read only after that handle is fenced by the central phase dispatcher.
        [WriteOnly] [NoAlias] [NativeDisableContainerSafetyRestriction] public global::Hecton8.Core.MpscSignalRingBuffer<BaseStructuralWarningSignal>.ParallelWriter WarningSignals;
        [NativeDisableParallelForRestriction] public NativeArray<int> WarningSignalsBudget;
        public uint Frame;
        public float CurrentTimeSeconds;
        public float GlobalQualityWeight;

        public void Execute()
        {
            if (!Groups.IsCreated || !Timers.IsCreated || !Counters.IsCreated ||
                Counters.Length < BaseStructuralWarningConstants.CounterCapacity ||
                !WarningTuning.IsCreated || WarningTuning.Length == 0)
            {
                return;
            }

            BaseStructuralWarningTuningDTO tuning = WarningTuning[0];
            float cooldown = math.max(0.05f, FiniteOr(tuning.CooldownSeconds, BaseStructuralWarningConstants.DefaultCooldownSeconds));
            float quality = math.saturate(FiniteOr(GlobalQualityWeight, tuning.GlobalQualityWeight));
            float minRadius = math.max(0.25f, FiniteOr(tuning.MinClusterRadiusMeters, BaseStructuralWarningConstants.DefaultMinClusterRadiusMeters));
            float maxRadius = math.max(minRadius, FiniteOr(tuning.MaxClusterRadiusMeters, BaseStructuralWarningConstants.DefaultMaxClusterRadiusMeters));
            float qualityCurve = quality * quality * (3f - 2f * quality);
            int emissionLimit = math.clamp((int)math.round(math.lerp(4f, BaseStructuralWarningConstants.MaxFrameSignals, qualityCurve)), 1, BaseStructuralWarningConstants.MaxFrameSignals);
            double sectorCell = math.max(0.25d, math.lerp(minRadius, maxRadius, 1.0f - quality));
            int groupCount = math.clamp(Counters[BaseStructuralWarningConstants.CounterGroupCount], 0, Groups.Length);
            int emitted = 0;
            int dropped = 0;
            uint faultFlags = (uint)Counters[BaseStructuralWarningConstants.CounterFaultFlags];

            BaseAlarmProfileDTO profile = ResolveProfile(tuning);
            ulong visitedMask = 0UL;
            for (int pass = 0; pass < groupCount; pass++)
            {
                if (emitted >= emissionLimit)
                {
                    dropped += groupCount - pass;
                    break;
                }

                int selectedGroup = SelectNextGroupByStress(groupCount, visitedMask);
                if (selectedGroup < 0)
                    break;

                visitedMask |= 1UL << selectedGroup;
                GroupedWarningDTO group = Groups[selectedGroup];
                uint sectorHash = HashSector(group.EpicenterAUP, sectorCell);
                int timerIndex = FindOrAllocateTimer(sectorHash, group.EpicenterAUP, ref faultFlags);
                if (timerIndex < 0)
                {
                    dropped++;
                    continue;
                }

                BaseStructuralWarningTimerDTO timer = Timers[timerIndex];
                float last = timer.SectorHash == sectorHash ? timer.LastWarningTimeSeconds : -1000000f;
                if (CurrentTimeSeconds - last < cooldown)
                {
                    dropped++;
                    group.CriticalFlags |= BaseStructuralWarningConstants.GroupFlagThrottled;
                    continue;
                }

                timer.SectorAUP = group.EpicenterAUP;
                timer.LastWarningTimeSeconds = CurrentTimeSeconds;
                timer.SectorHash = sectorHash;
                Timers[timerIndex] = timer;

                BaseStructuralWarningSignal signal = new BaseStructuralWarningSignal
                {
                    EpicenterAup = BuildAcousticAup(group.EpicenterAUP),
                    BaseHash = tuning.BaseHash,
                    Frame = Frame,
                    HighestStress01 = math.saturate(group.HighestStress01),
                    AudioIntensity01 = math.saturate(group.HighestStress01 * math.max(0f, tuning.AudioIntensityScale) * math.max(0f, profile.AudioIntensityScale)),
                    PanicScalar01 = math.saturate((group.HighestStress01 - 0.65f) * 2.8571428f * math.max(0f, tuning.PanicStressScale) * math.max(0f, profile.PanicScale)),
                    CriticalFlags = ConvertFlags(group.CriticalFlags)
                };
                if (SignalBus<BaseStructuralWarningSignal>.TryEnqueueBounded(WarningSignals, WarningSignalsBudget, signal))
                {
                    emitted++;
                }
                else
                {
                    dropped++;
                    faultFlags |= BaseStructuralWarningConstants.TelemetryFlagSignalDrop;
                }
            }

            Counters[BaseStructuralWarningConstants.CounterEmittedCount] = emitted;
            Counters[BaseStructuralWarningConstants.CounterDroppedCount] = dropped;
            Counters[BaseStructuralWarningConstants.CounterFaultFlags] = unchecked((int)faultFlags);
        }

        private int SelectNextGroupByStress(int groupCount, ulong visitedMask)
        {
            int selected = -1;
            float selectedStress = -1f;
            for (int i = 0; i < groupCount; i++)
            {
                ulong bit = 1UL << i;
                if ((visitedMask & bit) != 0UL)
                    continue;

                GroupedWarningDTO group = Groups[i];
                float stress = math.saturate(FiniteOr(group.HighestStress01, 0f));
                if (stress > selectedStress)
                {
                    selectedStress = stress;
                    selected = i;
                }
            }

            return selected;
        }

        private BaseAlarmProfileDTO ResolveProfile(BaseStructuralWarningTuningDTO tuning)
        {
            BaseAlarmProfileDTO fallback = new BaseAlarmProfileDTO
            {
                FailureTypeHash = 0u,
                SoundProfileHash = 0u,
                VisualIntensityScale = 1f,
                AudioIntensityScale = 1f,
                PanicScale = 1f,
                Flags = 0u
            };

            if (!Profiles.IsCreated || Profiles.Length == 0)
                return fallback;

            int count = math.clamp(tuning.ActiveProfileCount, 0, Profiles.Length);
            for (int i = 0; i < count; i++)
            {
                BaseAlarmProfileDTO profile = Profiles[i];
                if (profile.FailureTypeHash != 0u)
                    return SanitizeProfile(profile);
            }

            return fallback;
        }

        private static BaseAlarmProfileDTO SanitizeProfile(BaseAlarmProfileDTO profile)
        {
            profile.VisualIntensityScale = math.max(0f, FiniteOr(profile.VisualIntensityScale, 1f));
            profile.AudioIntensityScale = math.max(0f, FiniteOr(profile.AudioIntensityScale, 1f));
            profile.PanicScale = math.max(0f, FiniteOr(profile.PanicScale, 1f));
            return profile;
        }

        private int FindOrAllocateTimer(uint sectorHash, double3 sectorAup, ref uint faultFlags)
        {
            int firstEmpty = -1;
            float oldestTime = float.MaxValue;
            int oldest = -1;
            for (int i = 0; i < Timers.Length; i++)
            {
                BaseStructuralWarningTimerDTO timer = Timers[i];
                if (timer.SectorHash == sectorHash)
                    return i;
                if (timer.SectorHash == 0u && firstEmpty < 0)
                    firstEmpty = i;
                if (timer.LastWarningTimeSeconds < oldestTime)
                {
                    oldestTime = timer.LastWarningTimeSeconds;
                    oldest = i;
                }
            }

            int selected = firstEmpty >= 0 ? firstEmpty : oldest;
            if (selected < 0)
            {
                faultFlags |= BaseStructuralWarningConstants.TelemetryFlagTimerOverflow;
                return -1;
            }

            if (firstEmpty < 0)
                faultFlags |= BaseStructuralWarningConstants.TelemetryFlagTimerOverflow;
            Timers[selected] = new BaseStructuralWarningTimerDTO
            {
                SectorAUP = sectorAup,
                LastWarningTimeSeconds = -1000000f,
                SectorHash = sectorHash
            };
            return selected;
        }

        private static uint ConvertFlags(uint groupFlags)
        {
            uint flags = 0u;
            if ((groupFlags & BaseStructuralWarningConstants.GroupFlagRedAlert) != 0u)
                flags |= BaseStructuralWarningSignal.FlagRedAlert;
            if ((groupFlags & BaseStructuralWarningConstants.GroupFlagNonFinite) != 0u)
                flags |= BaseStructuralWarningSignal.FlagNonFinite;
            if ((groupFlags & BaseStructuralWarningConstants.GroupFlagThrottled) != 0u)
                flags |= BaseStructuralWarningSignal.FlagThrottled;
            if ((groupFlags & BaseStructuralWarningConstants.GroupFlagHypoxiaPanicCandidate) != 0u)
                flags |= BaseStructuralWarningSignal.FlagHypoxiaPanicCandidate;
            return flags;
        }

        private static AcousticAup BuildAcousticAup(double3 absolute)
        {
            absolute = math.all(math.isfinite(absolute)) ? absolute : double3.zero;
            double cellSize = math.max(1d, (double)AcousticAup.CellSizeMeters);
            const double gridClamp = 1000000000d;
            long gridX = (long)math.clamp(math.floor(absolute.x / cellSize), -gridClamp, gridClamp);
            long gridY = (long)math.clamp(math.floor(absolute.y / cellSize), -gridClamp, gridClamp);
            long gridZ = (long)math.clamp(math.floor(absolute.z / cellSize), -gridClamp, gridClamp);
            return new AcousticAup(
                gridX,
                gridY,
                gridZ,
                new float3(
                    SafeSignalFloat(absolute.x - gridX * cellSize),
                    SafeSignalFloat(absolute.y - gridY * cellSize),
                    SafeSignalFloat(absolute.z - gridZ * cellSize)));
        }

        private static float SafeSignalFloat(double value)
        {
            const double signalClampMeters = 1000000d;
            double safe = math.isfinite(value) ? value : 0d;
            return (float)math.clamp(safe, -signalClampMeters, signalClampMeters);
        }

        private static uint HashSector(double3 aup, double cellSize)
        {
            if (!math.all(math.isfinite(aup)))
                return 1u;

            long sx = (long)math.floor(aup.x / cellSize);
            long sy = (long)math.floor(aup.y / cellSize);
            long sz = (long)math.floor(aup.z / cellSize);
            uint hash = 2166136261u;
            hash = HashLong(hash, sx);
            hash = HashLong(hash, sy);
            hash = HashLong(hash, sz);
            return hash == 0u ? 1u : hash;
        }

        private static uint HashLong(uint hash, long value)
        {
            unchecked
            {
                ulong raw = (ulong)value;
                hash = (hash ^ (uint)raw) * 16777619u;
                hash = (hash ^ (uint)(raw >> 32)) * 16777619u;
                return hash;
            }
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct WriteStructuralWarningTelemetryJob : IJob
    {
        [ReadOnly] [NoAlias] public NativeArray<int> Counters;
        [NoAlias] public NativeArray<BaseStructuralWarningTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint Frame;
        public int ActiveNodeCount;
        public float GlobalQualityWeight;
        public uint BaseHash;

        public void Execute()
        {
            if (!Counters.IsCreated || Counters.Length < BaseStructuralWarningConstants.CounterCapacity ||
                !Telemetry.IsCreated || Telemetry.Length == 0 ||
                !TelemetryCursor.IsCreated || TelemetryCursor.Length == 0)
            {
                return;
            }

            int capacity = math.min(Telemetry.Length, BaseStructuralWarningConstants.TelemetryFrameCapacity);
            int cursor = TelemetryCursor[0];
            if (cursor < 0)
                cursor = 0;
            cursor %= capacity;

            float highestStress = math.asfloat(Counters[BaseStructuralWarningConstants.CounterHighestStressBits]);
            float estimatedMicroseconds = math.asfloat(Counters[BaseStructuralWarningConstants.CounterEstimatedMicrosecondsBits]);
            float radiusMeters = math.asfloat(Counters[BaseStructuralWarningConstants.CounterRadiusMetersBits]);
            uint faultFlags = (uint)Counters[BaseStructuralWarningConstants.CounterFaultFlags];
            uint stateHash = 2166136261u;
            stateHash = Hash(stateHash, (uint)Counters[BaseStructuralWarningConstants.CounterRawCount]);
            stateHash = Hash(stateHash, (uint)Counters[BaseStructuralWarningConstants.CounterGroupCount]);
            stateHash = Hash(stateHash, math.asuint(highestStress));
            stateHash = Hash(stateHash, math.asuint(radiusMeters));

            Telemetry[cursor] = new BaseStructuralWarningTelemetryEntry
            {
                Frame = Frame,
                ActiveNodeCount = ActiveNodeCount,
                RawWarningCount = Counters[BaseStructuralWarningConstants.CounterRawCount],
                GroupedWarningCount = Counters[BaseStructuralWarningConstants.CounterGroupCount],
                EmittedWarningCount = Counters[BaseStructuralWarningConstants.CounterEmittedCount],
                DroppedWarningCount = Counters[BaseStructuralWarningConstants.CounterDroppedCount],
                HighestStress01 = highestStress,
                ClusterRadiusMeters = radiusMeters,
                GlobalQualityWeight = GlobalQualityWeight,
                EstimatedMicroseconds = estimatedMicroseconds,
                FaultFlags = faultFlags,
                BaseHash = BaseHash,
                Sequence = (uint)cursor,
                StateHash = stateHash,
                LastSectorHash = 0u,
                Reserved0 = 0u
            };
            TelemetryCursor[0] = (cursor + 1) % capacity;
        }

        private static uint Hash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    public sealed unsafe partial class StructuralIntegrityCalculatorRuntime
    {
        private const string BaseStructuralWarningDumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_339.bin";
        private const string BaseStructuralWarningDefaultCsvRelativePath = "Docs/Data/base_alarm_profiles.csv";
        private const int SolverLockBaseWarningRaw = 1 << 9;
        private const int SolverLockBaseWarningGroups = 1 << 10;
        private const int SolverLockBaseWarningTimers = 1 << 11;
        private const int SolverLockBaseWarningCounters = 1 << 12;
        private const int SolverLockBaseWarningTelemetry = 1 << 13;
        private const int SolverLockBaseWarningTelemetryCursor = 1 << 14;
        private const int SolverLockBaseWarningTuning = 1 << 15;
        private const int SolverLockBaseWarningProfiles = 1 << 16;
        private const int SolverLockBaseWarningCsvScratch = 1 << 17;
        private VaultGenerationHandle<RawWarningDTO> _baseWarningRawHandle;
        private VaultGenerationHandle<GroupedWarningDTO> _baseWarningGroupsHandle;
        private VaultGenerationHandle<BaseStructuralWarningTimerDTO> _baseWarningTimersHandle;
        private VaultGenerationHandle<int> _baseWarningCountersHandle;
        private VaultGenerationHandle<BaseStructuralWarningTelemetryEntry> _baseWarningTelemetryHandle;
        private VaultGenerationHandle<int> _baseWarningTelemetryCursorHandle;
        private VaultGenerationHandle<BaseStructuralWarningTuningDTO> _baseWarningTuningHandle;
        private VaultGenerationHandle<BaseAlarmProfileDTO> _baseWarningProfilesHandle;
        private VaultGenerationHandle<byte> _baseWarningCsvScratchHandle;
        private uint _lastBaseStructuralWarningDumpFrame;

        public bool TryGetBaseStructuralWarningTuning(out BaseStructuralWarningTuningDTO tuning)
        {
            tuning = default;
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            NativeArray<BaseStructuralWarningTuningDTO> tuningArray = ResolveVaultBuffer(in _baseWarningTuningHandle);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool SetBaseStructuralWarningTuning(in BaseStructuralWarningTuningDTO tuning)
        {
            if (_initialized == 0 || _jobScheduled != 0 || _dataVault == null)
                return false;

            if (!TryAcquireStructuralMutationGuard())
                return false;

            try
            {
                NativeArray<BaseStructuralWarningTuningDTO> tuningArray = ResolveVaultBuffer(in _baseWarningTuningHandle);
                if (!tuningArray.IsCreated || tuningArray.Length == 0)
                    return false;

                tuningArray[0] = SanitizeBaseStructuralWarningTuning(tuning);
                return true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        public bool TryGetBaseStructuralWarningTelemetry(out BaseStructuralWarningTelemetryEntry entry)
        {
            entry = default;
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            NativeArray<BaseStructuralWarningTelemetryEntry> telemetry = ResolveVaultBuffer(in _baseWarningTelemetryHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(in _baseWarningTelemetryCursorHandle);
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length == 0 || cursor.Length == 0)
                return false;

            int capacity = math.min(telemetry.Length, BaseStructuralWarningConstants.TelemetryFrameCapacity);
            int slot = cursor[0] - 1;
            if (slot < 0)
                slot += capacity;
            entry = telemetry[slot % capacity];
            return entry.Frame != 0u;
        }

        public bool TryGetGroupedStructuralWarning(int index, out GroupedWarningDTO group)
        {
            group = default;
            if (_initialized == 0 || _jobScheduled != 0 || index < 0)
                return false;

            NativeArray<GroupedWarningDTO> groups = ResolveVaultBuffer(in _baseWarningGroupsHandle);
            NativeArray<int> counters = ResolveVaultBuffer(in _baseWarningCountersHandle);
            if (!groups.IsCreated || !counters.IsCreated || counters.Length <= BaseStructuralWarningConstants.CounterGroupCount)
                return false;

            int count = math.clamp(counters[BaseStructuralWarningConstants.CounterGroupCount], 0, groups.Length);
            if (index >= count)
                return false;

            group = groups[index];
            return true;
        }

        public bool GenerateMockStructuralWarningSpike()
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            if (!TryPinSolverBuffers(false))
                return false;

            try
            {
                NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
                NativeArray<double3> aups = ResolveVaultBuffer(in _nodeAupsHandle);
                if (!states.IsCreated || !aups.IsCreated)
                    return false;

                int count = math.clamp(math.min(_activeNodeCount, 384), 1, math.min(states.Length, aups.Length));
                JobHandle handle = new GenerateMockStressSpikeJob
                {
                    States = states,
                    NodeAups = aups,
                    SpikeCount = math.min(count, 256),
                    ActiveNodeCount = count,
                    CenterAup = new double3(seaLevelAup.x, seaLevelAup.y - 120.0, seaLevelAup.z),
                    PeakStress01 = 0.985f,
                    BaseHash = StructuralIntegrityConstants.DefaultBaseHash
                }.Schedule(count, 64);
                H8Memory.RegisterActiveJob(SystemID.HullIntegrity, handle);
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                _activeNodeCount = math.max(_activeNodeCount, count);
                return true;
            }
            finally
            {
                UnlockSolverBuffers();
            }
        }

#if UNITY_EDITOR
        public bool TryLoadBaseAlarmProfilesCsv(string relativePath = null)
        {
            if (_initialized == 0 || _jobScheduled != 0 || _dataVault == null)
                return false;

            string path = ResolveProjectPath(string.IsNullOrEmpty(relativePath) ? BaseStructuralWarningDefaultCsvRelativePath : relativePath);
            if (!File.Exists(path))
                return WriteDefaultBaseAlarmProfile();

            FileInfo info = new FileInfo(path);
            if (info.Length <= 0L || info.Length > BaseStructuralWarningConstants.CsvScratchBytes)
                return WriteDefaultBaseAlarmProfile();

            if (!TryAcquireStructuralMutationGuard())
                return false;

            try
            {
                NativeArray<byte> scratch = ResolveVaultBuffer(in _baseWarningCsvScratchHandle);
                NativeArray<BaseAlarmProfileDTO> profiles = ResolveVaultBuffer(in _baseWarningProfilesHandle);
                if (!scratch.IsCreated || !profiles.IsCreated || profiles.Length == 0)
                    return false;

                for (int i = 0; i < profiles.Length; i++)
                    profiles[i] = default;

                int read;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 256, FileOptions.SequentialScan))
                {
                    read = stream.Read(new Span<byte>(scratchPtr, (int)info.Length));
                }

                int parsed = read > 0
                    ? ParseBaseAlarmProfileCsv(new ReadOnlySpan<byte>(scratchPtr, read), profiles)
                    : 0;
                if (parsed <= 0)
                {
                    profiles[0] = DefaultBaseAlarmProfile();
                    parsed = 1;
                }

                NativeArray<BaseStructuralWarningTuningDTO> tuningArray = ResolveVaultBuffer(in _baseWarningTuningHandle);
                if (tuningArray.IsCreated && tuningArray.Length > 0)
                {
                    BaseStructuralWarningTuningDTO tuning = tuningArray[0];
                    tuning.ActiveProfileCount = parsed;
                    tuningArray[0] = SanitizeBaseStructuralWarningTuning(tuning);
                }

                return true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }
#endif

        private void EnsureBaseStructuralWarningHandles()
        {
            _baseWarningRawHandle = _dataVault.EnsureGenerationHandle<RawWarningDTO>(
                BufferID.BaseStructuralWarningRawWarnings,
                BaseStructuralWarningConstants.MaxRawWarnings,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningGroupsHandle = _dataVault.EnsureGenerationHandle<GroupedWarningDTO>(
                BufferID.BaseStructuralWarningGroups,
                BaseStructuralWarningConstants.MaxGroupedWarnings,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningTimersHandle = _dataVault.EnsureGenerationHandle<BaseStructuralWarningTimerDTO>(
                BufferID.BaseStructuralWarningTimers,
                BaseStructuralWarningConstants.SectorTimerCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningCountersHandle = _dataVault.EnsureGenerationHandle<int>(
                BufferID.BaseStructuralWarningCounters,
                BaseStructuralWarningConstants.CounterCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningTelemetryHandle = _dataVault.EnsureGenerationHandle<BaseStructuralWarningTelemetryEntry>(
                BufferID.BaseStructuralWarningTelemetryRing,
                BaseStructuralWarningConstants.TelemetryFrameCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningTelemetryCursorHandle = _dataVault.EnsureGenerationHandle<int>(
                BufferID.BaseStructuralWarningTelemetryCursor,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningTuningHandle = _dataVault.EnsureGenerationHandle<BaseStructuralWarningTuningDTO>(
                BufferID.BaseStructuralWarningTuning,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningProfilesHandle = _dataVault.EnsureGenerationHandle<BaseAlarmProfileDTO>(
                BufferID.BaseStructuralWarningProfiles,
                BaseStructuralWarningConstants.AlarmProfileCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _baseWarningCsvScratchHandle = _dataVault.EnsureGenerationHandle<byte>(
                BufferID.BaseStructuralWarningCsvScratch,
                BaseStructuralWarningConstants.CsvScratchBytes,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
        }

        private bool HasRequiredBaseStructuralWarningBuffers()
        {
            return BaseStructuralWarningLayout.Validate() &&
                   TryResolveVaultBuffer(in _baseWarningRawHandle, out NativeArray<RawWarningDTO> raw) &&
                   raw.Length >= BaseStructuralWarningConstants.MaxRawWarnings &&
                   TryResolveVaultBuffer(in _baseWarningGroupsHandle, out NativeArray<GroupedWarningDTO> groups) &&
                   groups.Length >= BaseStructuralWarningConstants.MaxGroupedWarnings &&
                   TryResolveVaultBuffer(in _baseWarningTimersHandle, out NativeArray<BaseStructuralWarningTimerDTO> timers) &&
                   timers.Length >= BaseStructuralWarningConstants.SectorTimerCapacity &&
                   TryResolveVaultBuffer(in _baseWarningCountersHandle, out NativeArray<int> counters) &&
                   counters.Length >= BaseStructuralWarningConstants.CounterCapacity &&
                   TryResolveVaultBuffer(in _baseWarningTelemetryHandle, out NativeArray<BaseStructuralWarningTelemetryEntry> telemetry) &&
                   telemetry.Length >= BaseStructuralWarningConstants.TelemetryFrameCapacity &&
                   TryResolveVaultBuffer(in _baseWarningTelemetryCursorHandle, out NativeArray<int> telemetryCursor) &&
                   telemetryCursor.Length >= 1 &&
                   TryResolveVaultBuffer(in _baseWarningTuningHandle, out NativeArray<BaseStructuralWarningTuningDTO> tuning) &&
                   tuning.Length >= 1 &&
                   TryResolveVaultBuffer(in _baseWarningProfilesHandle, out NativeArray<BaseAlarmProfileDTO> profiles) &&
                   profiles.Length >= BaseStructuralWarningConstants.AlarmProfileCapacity &&
                   TryResolveVaultBuffer(in _baseWarningCsvScratchHandle, out NativeArray<byte> scratch) &&
                   scratch.Length >= BaseStructuralWarningConstants.CsvScratchBytes;
        }

        private void ReleaseBaseStructuralWarningVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _baseWarningRawHandle);
            ReleaseVaultHandle(vault, ref _baseWarningGroupsHandle);
            ReleaseVaultHandle(vault, ref _baseWarningTimersHandle);
            ReleaseVaultHandle(vault, ref _baseWarningCountersHandle);
            ReleaseVaultHandle(vault, ref _baseWarningTelemetryHandle);
            ReleaseVaultHandle(vault, ref _baseWarningTelemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _baseWarningTuningHandle);
            ReleaseVaultHandle(vault, ref _baseWarningProfilesHandle);
            ReleaseVaultHandle(vault, ref _baseWarningCsvScratchHandle);
        }

        private void ClearBaseStructuralWarningHandleState()
        {
            _baseWarningRawHandle = default;
            _baseWarningGroupsHandle = default;
            _baseWarningTimersHandle = default;
            _baseWarningCountersHandle = default;
            _baseWarningTelemetryHandle = default;
            _baseWarningTelemetryCursorHandle = default;
            _baseWarningTuningHandle = default;
            _baseWarningProfilesHandle = default;
            _baseWarningCsvScratchHandle = default;
        }

        private bool ClearBaseStructuralWarningBootBuffers()
        {
            int mask = 0;
            if (!TryAcquireStructuralMutationGuard())
                return false;

            if (!TryMarkBaseStructuralWarningBuffers(ref mask))
            {
                ReleaseStructuralMutationGuard();
                return false;
            }

            try
            {
                JobHandle clearHandle = new BaseStructuralWarningColdInitJob
                {
                    RawWarnings = ResolveVaultBuffer(in _baseWarningRawHandle),
                    Groups = ResolveVaultBuffer(in _baseWarningGroupsHandle),
                    Timers = ResolveVaultBuffer(in _baseWarningTimersHandle),
                    Counters = ResolveVaultBuffer(in _baseWarningCountersHandle),
                    Telemetry = ResolveVaultBuffer(in _baseWarningTelemetryHandle),
                    TelemetryCursor = ResolveVaultBuffer(in _baseWarningTelemetryCursorHandle)
                }.Schedule();
                H8Memory.RegisterActiveJob(SystemID.HullIntegrity, clearHandle);
                DispatcherJobFence.TryComplete(ref clearHandle, forceComplete: true);
                return true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        private bool WriteDefaultBaseStructuralWarningTuning()
        {
            if (_dataVault == null || !TryAcquireStructuralMutationGuard())
                return false;

            try
            {
                NativeArray<BaseStructuralWarningTuningDTO> tuning = ResolveVaultBuffer(in _baseWarningTuningHandle);
                NativeArray<BaseAlarmProfileDTO> profiles = ResolveVaultBuffer(in _baseWarningProfilesHandle);
                if (!tuning.IsCreated || tuning.Length == 0 || !profiles.IsCreated || profiles.Length == 0)
                    return false;

                tuning[0] = SanitizeBaseStructuralWarningTuning(new BaseStructuralWarningTuningDTO
                {
                    StressThreshold01 = BaseStructuralWarningConstants.DefaultStressThreshold01,
                    CooldownSeconds = BaseStructuralWarningConstants.DefaultCooldownSeconds,
                    MinClusterRadiusMeters = BaseStructuralWarningConstants.DefaultMinClusterRadiusMeters,
                    MaxClusterRadiusMeters = BaseStructuralWarningConstants.DefaultMaxClusterRadiusMeters,
                    RedAlertStress01 = BaseStructuralWarningConstants.DefaultRedAlertStress01,
                    GlobalQualityWeight = simulationQualityWeight,
                    PanicStressScale = 1f,
                    AudioIntensityScale = 1f,
                    VisualIntensityScale = 1f,
                    TelemetryBudgetMicroseconds = BaseStructuralWarningConstants.DefaultTelemetryBudgetMicroseconds,
                    BaseHash = StructuralIntegrityConstants.DefaultBaseHash,
                    ActiveProfileCount = 1,
                    Flags = 0u
                });
                profiles[0] = DefaultBaseAlarmProfile();
                for (int i = 1; i < profiles.Length; i++)
                    profiles[i] = default;
                return true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        private JobHandle ScheduleBaseStructuralWarningDispatcher(
            NativeArray<IntegrityStateDTO> states,
            NativeArray<double3> aups,
            NativeArray<StructuralTuningDTO> structuralTuning,
            int safeCount,
            float quality,
            int framesBetweenUpdates,
            JobHandle dependency)
        {
            NativeArray<RawWarningDTO> rawWarnings = ResolveVaultBuffer(in _baseWarningRawHandle);
            NativeArray<GroupedWarningDTO> groups = ResolveVaultBuffer(in _baseWarningGroupsHandle);
            NativeArray<BaseStructuralWarningTimerDTO> timers = ResolveVaultBuffer(in _baseWarningTimersHandle);
            NativeArray<int> counters = ResolveVaultBuffer(in _baseWarningCountersHandle);
            NativeArray<BaseStructuralWarningTelemetryEntry> telemetry = ResolveVaultBuffer(in _baseWarningTelemetryHandle);
            NativeArray<int> telemetryCursor = ResolveVaultBuffer(in _baseWarningTelemetryCursorHandle);
            NativeArray<BaseStructuralWarningTuningDTO> warningTuning = ResolveVaultBuffer(in _baseWarningTuningHandle);
            NativeArray<BaseAlarmProfileDTO> profiles = ResolveVaultBuffer(in _baseWarningProfilesHandle);
            if (!rawWarnings.IsCreated || !groups.IsCreated || !timers.IsCreated || !counters.IsCreated ||
                !telemetry.IsCreated || !telemetryCursor.IsCreated || !warningTuning.IsCreated || !profiles.IsCreated ||
                warningTuning.Length == 0)
            {
                return dependency;
            }

            BaseStructuralWarningTuningDTO current = SanitizeBaseStructuralWarningTuning(warningTuning[0]);
            current.GlobalQualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            warningTuning[0] = current;
            int batchSize = ResolveBatchSize(quality);
            float estimatedMicroseconds = EstimateBaseWarningMicroseconds(safeCount, framesBetweenUpdates, quality);

            JobHandle handle = new EvaluateStructuralStressJob
            {
                States = states,
                NodeAups = aups,
                RawWarnings = rawWarnings,
                WarningTuning = warningTuning,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize, dependency);

            handle = new CoalesceWarningsJob
            {
                RawWarnings = rawWarnings,
                Groups = groups,
                Counters = counters,
                WarningTuning = warningTuning,
                ActiveNodeCount = safeCount,
                GlobalQualityWeight = quality,
                EstimatedMicroseconds = estimatedMicroseconds
            }.Schedule(handle);

            handle = new RouteStructuralWarningsJob
            {
                Groups = groups,
                Timers = timers,
                Counters = counters,
                WarningTuning = warningTuning,
                Profiles = profiles,
                WarningSignals = SignalBus<BaseStructuralWarningSignal>.ParallelWriter,
                WarningSignalsBudget = SignalBus<BaseStructuralWarningSignal>.ParallelWriterBudget,
                Frame = _frame,
                CurrentTimeSeconds = _frame * HectonPhysicsContract.FixedDeltaTimeSeconds,
                GlobalQualityWeight = quality
            }.Schedule(handle);

            handle = new WriteStructuralWarningTelemetryJob
            {
                Counters = counters,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
                Frame = _frame,
                ActiveNodeCount = safeCount,
                GlobalQualityWeight = quality,
                BaseHash = StructuralIntegrityConstants.DefaultBaseHash
            }.Schedule(handle);

            return handle;
        }

        private bool TryMarkBaseStructuralWarningBuffers(ref int mask)
        {
            if (!TryMarkSolverBuffer(in _baseWarningRawHandle, BufferID.BaseStructuralWarningRawWarnings, SolverLockBaseWarningRaw, BaseStructuralWarningConstants.MaxRawWarnings, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningGroupsHandle, BufferID.BaseStructuralWarningGroups, SolverLockBaseWarningGroups, BaseStructuralWarningConstants.MaxGroupedWarnings, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningTimersHandle, BufferID.BaseStructuralWarningTimers, SolverLockBaseWarningTimers, BaseStructuralWarningConstants.SectorTimerCapacity, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningCountersHandle, BufferID.BaseStructuralWarningCounters, SolverLockBaseWarningCounters, BaseStructuralWarningConstants.CounterCapacity, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningTelemetryHandle, BufferID.BaseStructuralWarningTelemetryRing, SolverLockBaseWarningTelemetry, BaseStructuralWarningConstants.TelemetryFrameCapacity, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningTelemetryCursorHandle, BufferID.BaseStructuralWarningTelemetryCursor, SolverLockBaseWarningTelemetryCursor, 1, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningTuningHandle, BufferID.BaseStructuralWarningTuning, SolverLockBaseWarningTuning, 1, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningProfilesHandle, BufferID.BaseStructuralWarningProfiles, SolverLockBaseWarningProfiles, BaseStructuralWarningConstants.AlarmProfileCapacity, ref mask)) return false;
            if (!TryMarkSolverBuffer(in _baseWarningCsvScratchHandle, BufferID.BaseStructuralWarningCsvScratch, SolverLockBaseWarningCsvScratch, BaseStructuralWarningConstants.CsvScratchBytes, ref mask)) return false;
            return true;
        }

        private void DrawBaseStructuralWarningGizmos(double3 originAup)
        {
#if UNITY_EDITOR
            NativeArray<GroupedWarningDTO> groups = ResolveVaultBuffer(in _baseWarningGroupsHandle);
            NativeArray<RawWarningDTO> rawWarnings = ResolveVaultBuffer(in _baseWarningRawHandle);
            NativeArray<int> counters = ResolveVaultBuffer(in _baseWarningCountersHandle);
            if (!groups.IsCreated || !counters.IsCreated || counters.Length <= BaseStructuralWarningConstants.CounterGroupCount)
                return;

            int count = math.clamp(counters[BaseStructuralWarningConstants.CounterGroupCount], 0, groups.Length);
            int rawCount = rawWarnings.IsCreated ? math.clamp(_activeNodeCount, 0, rawWarnings.Length) : 0;
            int drawnLines = 0;
            for (int i = 0; i < count; i++)
            {
                GroupedWarningDTO group = groups[i];
                if (!TryBuildEditorRelativePosition(group.EpicenterAUP, originAup, out Vector3 position))
                    continue;

                float stress = math.saturate(group.HighestStress01);
                Gizmos.color = (group.CriticalFlags & BaseStructuralWarningConstants.GroupFlagRedAlert) != 0u
                    ? new Color(1f, 0.08f, 0.02f, 0.9f)
                    : new Color(1f, 0.72f, 0.08f, 0.75f);
                Gizmos.DrawWireSphere(position, math.lerp(0.8f, 3.0f, stress));

                if (!rawWarnings.IsCreated || drawnLines >= 256)
                    continue;

                Gizmos.color = new Color(1f, 0.05f, 0.02f, math.lerp(0.18f, 0.45f, stress));
                for (int rawIndex = 0; rawIndex < rawCount && drawnLines < 256; rawIndex++)
                {
                    RawWarningDTO raw = rawWarnings[rawIndex];
                    if (raw.ClusterIndex != i || (raw.CriticalFlags & BaseStructuralWarningConstants.RawFlagActive) == 0u)
                        continue;
                    if (!TryBuildEditorRelativePosition(raw.WarningAUP, originAup, out Vector3 rawPosition))
                        continue;

                    Gizmos.DrawLine(position, rawPosition);
                    drawnLines++;
                }
            }
#endif
        }

        private void DumpBaseStructuralWarningTelemetry(in BaseStructuralWarningTelemetryEntry faultEntry)
        {
            NativeArray<BaseStructuralWarningTelemetryEntry> telemetry = ResolveVaultBuffer(in _baseWarningTelemetryHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(in _baseWarningTelemetryCursorHandle);
            if (!telemetry.IsCreated)
                return;

            int cursorValue = cursor.IsCreated && cursor.Length > 0 ? cursor[0] : 0;
            BaseStructuralWarningDumpHeader header = new BaseStructuralWarningDumpHeader
            {
                Magic = BaseStructuralWarningConstants.DumpMagic,
                Version = BaseStructuralWarningConstants.DumpVersion,
                Frame = faultEntry.Frame,
                EntrySize = (uint)UnsafeUtility.SizeOf<BaseStructuralWarningTelemetryEntry>(),
                EntryCount = telemetry.Length,
                Cursor = cursorValue,
                FaultFlags = faultEntry.FaultFlags,
                StateHash = faultEntry.StateHash
            };

            int headerBytes = UnsafeUtility.SizeOf<BaseStructuralWarningDumpHeader>();
            int stride = UnsafeUtility.SizeOf<BaseStructuralWarningTelemetryEntry>();
            int entryBytes = telemetry.Length * stride;
            int totalBytes = headerBytes + entryBytes;
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                totalBytes,
                nameof(StructuralIntegrityCalculatorRuntime),
                "BaseStructuralWarningTelemetryDumpPayload");
            try
            {
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(target, &header, headerBytes);
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                UnsafeUtility.MemCpy(target + headerBytes, source, entryBytes);
                NativeFaultDumpWriter.TryWriteAll(BaseStructuralWarningDumpRelativePath, payload, totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(StructuralIntegrityCalculatorRuntime),
                    "BaseStructuralWarningTelemetryDumpPayload");
            }
        }

        private void AfterBaseStructuralWarningComplete()
        {
            NativeArray<BaseStructuralWarningTelemetryEntry> telemetry = ResolveVaultBuffer(in _baseWarningTelemetryHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(in _baseWarningTelemetryCursorHandle);
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length == 0 || cursor.Length == 0)
                return;

            int capacity = math.min(telemetry.Length, BaseStructuralWarningConstants.TelemetryFrameCapacity);
            int cursorValue = cursor[0];
            if (cursorValue < 0)
                cursorValue = 0;
            cursorValue %= capacity;
            int slot = cursorValue - 1;
            if (slot < 0)
                slot += capacity;

            BaseStructuralWarningTelemetryEntry entry = telemetry[slot];
            if ((entry.FaultFlags & (BaseStructuralWarningConstants.TelemetryFlagNonFinite | BaseStructuralWarningConstants.TelemetryFlagOverBudgetEstimate)) != 0u &&
                entry.Frame != _lastBaseStructuralWarningDumpFrame)
            {
                DumpBaseStructuralWarningTelemetry(in entry);
                _lastBaseStructuralWarningDumpFrame = entry.Frame;
            }
        }

        private static BaseStructuralWarningTuningDTO SanitizeBaseStructuralWarningTuning(in BaseStructuralWarningTuningDTO source)
        {
            BaseStructuralWarningTuningDTO tuning = source;
            tuning.StressThreshold01 = math.saturate(math.isfinite(tuning.StressThreshold01) ? tuning.StressThreshold01 : BaseStructuralWarningConstants.DefaultStressThreshold01);
            tuning.CooldownSeconds = math.max(0.05f, math.isfinite(tuning.CooldownSeconds) ? tuning.CooldownSeconds : BaseStructuralWarningConstants.DefaultCooldownSeconds);
            tuning.MinClusterRadiusMeters = math.max(0.25f, math.isfinite(tuning.MinClusterRadiusMeters) ? tuning.MinClusterRadiusMeters : BaseStructuralWarningConstants.DefaultMinClusterRadiusMeters);
            tuning.MaxClusterRadiusMeters = math.max(tuning.MinClusterRadiusMeters, math.isfinite(tuning.MaxClusterRadiusMeters) ? tuning.MaxClusterRadiusMeters : BaseStructuralWarningConstants.DefaultMaxClusterRadiusMeters);
            tuning.RedAlertStress01 = math.saturate(math.isfinite(tuning.RedAlertStress01) ? tuning.RedAlertStress01 : BaseStructuralWarningConstants.DefaultRedAlertStress01);
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            tuning.PanicStressScale = math.max(0f, math.isfinite(tuning.PanicStressScale) ? tuning.PanicStressScale : 1f);
            tuning.AudioIntensityScale = math.max(0f, math.isfinite(tuning.AudioIntensityScale) ? tuning.AudioIntensityScale : 1f);
            tuning.VisualIntensityScale = math.max(0f, math.isfinite(tuning.VisualIntensityScale) ? tuning.VisualIntensityScale : 1f);
            tuning.TelemetryBudgetMicroseconds = math.max(1f, math.isfinite(tuning.TelemetryBudgetMicroseconds) ? tuning.TelemetryBudgetMicroseconds : BaseStructuralWarningConstants.DefaultTelemetryBudgetMicroseconds);
            tuning.BaseHash = tuning.BaseHash == 0u ? StructuralIntegrityConstants.DefaultBaseHash : tuning.BaseHash;
            tuning.ActiveProfileCount = math.clamp(tuning.ActiveProfileCount, 0, BaseStructuralWarningConstants.AlarmProfileCapacity);
            return tuning;
        }

        private static float EstimateBaseWarningMicroseconds(int nodeCount, int framesBetweenUpdates, float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float raw = nodeCount * math.lerp(0.005f, 0.018f, q) + BaseStructuralWarningConstants.MaxGroupedWarnings * 0.12f + 5f;
            return math.max(1f, raw / math.max(1, framesBetweenUpdates));
        }

        private static BaseAlarmProfileDTO DefaultBaseAlarmProfile()
        {
            return new BaseAlarmProfileDTO
            {
                FailureTypeHash = 0x42535744u,
                SoundProfileHash = 0x5347524Fu,
                VisualIntensityScale = 1f,
                AudioIntensityScale = 1f,
                PanicScale = 1f,
                Flags = 0u
            };
        }

        private bool WriteDefaultBaseAlarmProfile()
        {
            if (_dataVault == null || !TryAcquireStructuralMutationGuard())
                return false;

            try
            {
                NativeArray<BaseAlarmProfileDTO> profiles = ResolveVaultBuffer(in _baseWarningProfilesHandle);
                if (!profiles.IsCreated || profiles.Length == 0)
                    return false;

                profiles[0] = DefaultBaseAlarmProfile();
                for (int i = 1; i < profiles.Length; i++)
                    profiles[i] = default;
                return true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

#if UNITY_EDITOR
        private static int ParseBaseAlarmProfileCsv(ReadOnlySpan<byte> bytes, NativeArray<BaseAlarmProfileDTO> profiles)
        {
            int length = math.min(bytes.Length, BaseStructuralWarningConstants.CsvScratchBytes);
            int cursor = 0;
            int row = 0;
            int written = 0;
            while (cursor < length && written < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < length && bytes[cursor] != (byte)'\n')
                    cursor++;
                int lineEnd = cursor;
                if (cursor < length && bytes[cursor] == (byte)'\n')
                    cursor++;
                row++;
                if (lineEnd > lineStart && bytes[lineEnd - 1] == (byte)'\r')
                    lineEnd--;
                if (lineEnd <= lineStart || bytes[lineStart] == (byte)'#')
                    continue;
                if (row == 1 && ContainsAlphaHeader(bytes, lineStart, lineEnd))
                    continue;

                int field = lineStart;
                uint failureHash = ReadUInt(bytes, ref field, lineEnd, 0x42535744u);
                uint soundHash = ReadUInt(bytes, ref field, lineEnd, 0x5347524Fu);
                float visual = ReadFloat(bytes, ref field, lineEnd, 1f);
                float audio = ReadFloat(bytes, ref field, lineEnd, 1f);
                float panic = ReadFloat(bytes, ref field, lineEnd, 1f);
                uint flags = ReadUInt(bytes, ref field, lineEnd, 0u);
                profiles[written++] = new BaseAlarmProfileDTO
                {
                    FailureTypeHash = failureHash,
                    SoundProfileHash = soundHash,
                    VisualIntensityScale = math.max(0f, visual),
                    AudioIntensityScale = math.max(0f, audio),
                    PanicScale = math.max(0f, panic),
                    Flags = flags
                };
            }

            return written;
        }

        private static bool ContainsAlphaHeader(ReadOnlySpan<byte> bytes, int start, int end)
        {
            return ContainsAsciiWord(bytes, start, end, "failure") ||
                   ContainsAsciiWord(bytes, start, end, "sound") ||
                   ContainsAsciiWord(bytes, start, end, "profile");
        }

        private static bool ContainsAsciiWord(ReadOnlySpan<byte> bytes, int start, int end, string word)
        {
            int length = word.Length;
            for (int i = start; i <= end - length; i++)
            {
                bool match = true;
                for (int j = 0; j < length; j++)
                {
                    byte c = bytes[i + j];
                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    if (c != (byte)word[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }

        private static uint ReadUInt(ReadOnlySpan<byte> bytes, ref int cursor, int lineEnd, uint fallback)
        {
            SkipFieldWhitespace(bytes, ref cursor, lineEnd);
            int tokenStart = cursor;
            while (cursor < lineEnd && bytes[cursor] != (byte)',')
                cursor++;
            int tokenEnd = cursor;
            if (cursor < lineEnd && bytes[cursor] == (byte)',')
                cursor++;
            while (tokenEnd > tokenStart && (bytes[tokenEnd - 1] == (byte)' ' || bytes[tokenEnd - 1] == (byte)'\t'))
                tokenEnd--;
            if (tokenEnd <= tokenStart)
                return fallback;

            if (tokenEnd - tokenStart > 2 && bytes[tokenStart] == (byte)'0' && (bytes[tokenStart + 1] == (byte)'x' || bytes[tokenStart + 1] == (byte)'X'))
                return ReadHexUInt(bytes, tokenStart + 2, tokenEnd, fallback);

            if (IsDecimal(bytes, tokenStart, tokenEnd))
                return ReadDecimalUInt(bytes, tokenStart, tokenEnd);

            uint hash = 2166136261u;
            for (int i = tokenStart; i < tokenEnd; i++)
            {
                byte c = bytes[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }

            return hash == 0u ? fallback : hash;
        }

        private static bool IsDecimal(ReadOnlySpan<byte> bytes, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
            }

            return end > start;
        }

        private static uint ReadDecimalUInt(ReadOnlySpan<byte> bytes, int start, int end)
        {
            uint value = 0u;
            for (int i = start; i < end; i++)
                value = value * 10u + (uint)(bytes[i] - (byte)'0');
            return value;
        }

        private static uint ReadHexUInt(ReadOnlySpan<byte> bytes, int start, int end, uint fallback)
        {
            uint value = 0u;
            bool any = false;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                uint nibble;
                if (c >= (byte)'0' && c <= (byte)'9')
                    nibble = (uint)(c - (byte)'0');
                else if (c >= (byte)'a' && c <= (byte)'f')
                    nibble = (uint)(c - (byte)'a' + 10);
                else if (c >= (byte)'A' && c <= (byte)'F')
                    nibble = (uint)(c - (byte)'A' + 10);
                else
                    return fallback;

                any = true;
                value = (value << 4) | nibble;
            }

            return any ? value : fallback;
        }

        private static float ReadFloat(ReadOnlySpan<byte> bytes, ref int cursor, int lineEnd, float fallback)
        {
            SkipFieldWhitespace(bytes, ref cursor, lineEnd);
            float sign = 1f;
            if (cursor < lineEnd && bytes[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }

            float value = 0f;
            float divisor = 1f;
            bool fraction = false;
            bool any = false;
            while (cursor < lineEnd && bytes[cursor] != (byte)',')
            {
                byte c = bytes[cursor++];
                if (c == (byte)'.')
                {
                    fraction = true;
                    continue;
                }

                if (c >= (byte)'0' && c <= (byte)'9')
                {
                    any = true;
                    value = value * 10f + (c - (byte)'0');
                    if (fraction)
                        divisor *= 10f;
                }
            }
            if (cursor < lineEnd && bytes[cursor] == (byte)',')
                cursor++;
            return any ? sign * value / math.max(1f, divisor) : fallback;
        }

        private static void SkipFieldWhitespace(ReadOnlySpan<byte> bytes, ref int cursor, int lineEnd)
        {
            while (cursor < lineEnd && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;
        }
#endif
    }
}
