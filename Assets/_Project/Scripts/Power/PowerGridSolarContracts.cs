using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    public static class SolarPowerGenerationConstants
    {
        public const int SolarPanelStateDtoSizeBytes = 32;
        public const int SolarConditionsDtoSizeBytes = 160;
        public const int SolarPanelOutputDtoSizeBytes = 32;
        public const int SolarTelemetryEntrySizeBytes = 64;
        public const int SolarBlackBoxDumpHeaderSizeBytes = 32;
        public const int SolarProfileDtoSizeBytes = 32;
        public const int SolarNodeInputCounterSizeBytes = 64;
        public const uint SolverBudgetMicroseconds = 200u;
        public const int TelemetryFrameCount = 300;
        public const int DefaultPanelCapacity = 512;
        public const int DefaultPowerNodeCapacity = 1024;
        public const int CsvScratchBytes = 16 * 1024;
        public const float DefaultSolarIrradianceWatts = 1361f;
        public const float DefaultWaterAttenuationCoefficient = 0.045f;
        public const float DefaultTurbidityMultiplier = 0.18f;
        public const float DefaultSdfRangeMeters = 64f;
        public const float DefaultSeaLevelY = 14.02f;
        public const float InjectedSourceCapacityFloorWatts = 4096f;
        public const uint RuntimeHash = 0x53333431u; // S341
        public const uint DumpMagic = 0x53343144u; // S41D
        public const uint DumpVersion = 1u;
        public const uint FlagMissingPowerVault = 1u << 0;
        public const uint FlagMissingSdfVault = 1u << 1;
        public const uint FlagNonFinite = 1u << 2;
        public const uint FlagSdfShadowed = 1u << 3;
        public const uint FlagMissingPowerNode = 1u << 4;
        public const uint FlagSolverOverBudget = 1u << 5;
        public const uint FlagMockConditions = 1u << 6;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_341.bin";

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSeaLevelDeltaMeters(float seaLevelDeltaMeters)
        {
            return math.isfinite(seaLevelDeltaMeters) &&
                   math.abs(seaLevelDeltaMeters) > 0.0001f &&
                   math.abs(seaLevelDeltaMeters) <= 1000f
                ? seaLevelDeltaMeters
                : DefaultSeaLevelY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double3 BuildSeaLevelAUP(double3 runtimeOriginAUP, float seaLevelDeltaMeters)
        {
            double3 origin = math.all(math.isfinite(runtimeOriginAUP)) ? runtimeOriginAUP : double3.zero;
            return origin + new double3(0.0, ResolveSeaLevelDeltaMeters(seaLevelDeltaMeters), 0.0);
        }
    }

    public static class SolarPowerBufferIds
    {
        public const BufferID PanelStates = (BufferID)73410;
        public const BufferID PanelOutputs = (BufferID)73411;
        public const BufferID PanelPowerNodeIndices = (BufferID)73412;
        public const BufferID NodeSolarInputMilliWatts = (BufferID)73413;
        public const BufferID Conditions = (BufferID)73414;
        public const BufferID TelemetryRing = (BufferID)73415;
        public const BufferID TelemetryCursor = (BufferID)73416;
        public const BufferID Profiles = (BufferID)73417;
        public const BufferID CsvScratch = (BufferID)73418;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarPanelStateDtoSizeBytes)]
    public struct SolarPanelStateDTO
    {
        [FieldOffset(0)] public double3 PanelAUP;
        [FieldOffset(24)] public float BaseEfficiencyScalar;
        [FieldOffset(28)] public uint PowerNodeHashID;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarConditionsDtoSizeBytes)]
    public struct SolarConditionsDTO
    {
        [FieldOffset(0)] public double3 SeaLevelAUP;
        [FieldOffset(24)] public double3 RuntimeOriginAUP;
        [FieldOffset(48)] public double3 VoxelSdfOriginAUP;
        [FieldOffset(72)] public float3 SunDirection;
        [FieldOffset(84)] public float WaterAttenuationCoefficient;
        [FieldOffset(88)] public float WaterTurbidity;
        [FieldOffset(92)] public float TurbidityMultiplier;
        [FieldOffset(96)] public float InitialIntensityWatts;
        [FieldOffset(100)] public float GlobalQualityWeight;
        [FieldOffset(104)] public float SimulationTimeSeconds;
        [FieldOffset(108)] public float DeltaTimeSeconds;
        [FieldOffset(112)] public int3 VoxelSdfDimensions;
        [FieldOffset(124)] public float3 VoxelSdfCellSize;
        [FieldOffset(136)] public float VoxelSdfRangeMeters;
        [FieldOffset(140)] public float BaseEfficiencyScalar;
        [FieldOffset(144)] private ulong _pad0;
        [FieldOffset(152)] private ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarPanelOutputDtoSizeBytes)]
    public struct SolarPanelOpticalOutputDTO
    {
        [FieldOffset(0)] public float GeneratedWatts;
        [FieldOffset(4)] public float IrradianceWatts;
        [FieldOffset(8)] public float OpticalDepthMeters;
        [FieldOffset(12)] public float AngleMultiplier;
        [FieldOffset(16)] public float ShadowMultiplier;
        [FieldOffset(20)] public float DepthMeters;
        [FieldOffset(24)] public uint PowerNodeHashID;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarNodeInputCounterSizeBytes)]
    public struct SolarNodeInputCounter64
    {
        [FieldOffset(0)] public int MilliWatts;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] private ulong _pad0;
        [FieldOffset(16)] private ulong _pad1;
        [FieldOffset(24)] private ulong _pad2;
        [FieldOffset(32)] private ulong _pad3;
        [FieldOffset(40)] private ulong _pad4;
        [FieldOffset(48)] private ulong _pad5;
        [FieldOffset(56)] private ulong _pad6;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarTelemetryEntrySizeBytes)]
    public struct SolarTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint ReasonFlags;
        [FieldOffset(12)] public int ActivePanelCount;
        [FieldOffset(16)] public int NodeCount;
        [FieldOffset(20)] public int SdfSampleCount;
        [FieldOffset(24)] public float TotalGeneratedWatts;
        [FieldOffset(28)] public float PeakPanelWatts;
        [FieldOffset(32)] public float AverageDepthMeters;
        [FieldOffset(36)] public float AverageOpticalDepth;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float SolarAngleMultiplier;
        [FieldOffset(48)] public float TurbidityScalar;
        [FieldOffset(52)] public uint SolverMicroseconds;
        [FieldOffset(56)] public int ShadowedPanelCount;
        [FieldOffset(60)] public int MissingNodeCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarBlackBoxDumpHeaderSizeBytes)]
    public struct SolarBlackBoxDumpHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint Version;
        [FieldOffset(8)] public uint ReasonFlags;
        [FieldOffset(12)] public uint EntryCount;
        [FieldOffset(16)] public uint EntryStrideBytes;
        [FieldOffset(20)] public uint FrameIndex;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = SolarPowerGenerationConstants.SolarProfileDtoSizeBytes)]
    public struct SolarProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float BaseEfficiencyScalar;
        [FieldOffset(8)] public float PanelAreaSquareMeters;
        [FieldOffset(12)] public float MaxOutputWatts;
        [FieldOffset(16)] public float HeatLossScalar;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    public struct SolarPowerVaultHandles
    {
        public VaultGenerationHandle<SolarPanelStateDTO> PanelStates;
        public VaultGenerationHandle<SolarPanelOpticalOutputDTO> PanelOutputs;
        public VaultGenerationHandle<int> PanelPowerNodeIndices;
        public VaultGenerationHandle<SolarNodeInputCounter64> NodeSolarInputMilliWatts;
        public VaultGenerationHandle<SolarConditionsDTO> Conditions;
        public VaultGenerationHandle<SolarTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<SolarProfileDTO> Profiles;
        public VaultGenerationHandle<byte> CsvScratch;
    }

#if UNITY_EDITOR
    public static class SolarPowerLayoutAudit
    {
        public static bool ValidateAllSolarLayouts()
        {
            return ValidatePanelStateLayout() &&
                   ValidateConditionsLayout() &&
                   ValidateOutputLayout() &&
                   ValidateNodeInputCounterLayout() &&
                   ValidateTelemetryLayout() &&
                   ValidateBlackBoxHeaderLayout() &&
                   ValidateProfileLayout();
        }

        public static bool ValidatePanelStateLayout()
        {
            return UnsafeUtility.SizeOf<SolarPanelStateDTO>() == SolarPowerGenerationConstants.SolarPanelStateDtoSizeBytes &&
                   OffsetOf<SolarPanelStateDTO>(nameof(SolarPanelStateDTO.PanelAUP)) == 0 &&
                   OffsetOf<SolarPanelStateDTO>(nameof(SolarPanelStateDTO.BaseEfficiencyScalar)) == 24 &&
                   OffsetOf<SolarPanelStateDTO>(nameof(SolarPanelStateDTO.PowerNodeHashID)) == 28;
        }

        public static bool ValidateConditionsLayout()
        {
            return UnsafeUtility.SizeOf<SolarConditionsDTO>() == SolarPowerGenerationConstants.SolarConditionsDtoSizeBytes &&
                   OffsetOf<SolarConditionsDTO>(nameof(SolarConditionsDTO.SeaLevelAUP)) == 0 &&
                   OffsetOf<SolarConditionsDTO>(nameof(SolarConditionsDTO.RuntimeOriginAUP)) == 24 &&
                   OffsetOf<SolarConditionsDTO>(nameof(SolarConditionsDTO.VoxelSdfOriginAUP)) == 48 &&
                   OffsetOf<SolarConditionsDTO>(nameof(SolarConditionsDTO.SunDirection)) == 72 &&
                   OffsetOf<SolarConditionsDTO>(nameof(SolarConditionsDTO.VoxelSdfRangeMeters)) == 136 &&
                   OffsetOf<SolarConditionsDTO>(nameof(SolarConditionsDTO.BaseEfficiencyScalar)) == 140;
        }

        public static bool ValidateOutputLayout()
        {
            return UnsafeUtility.SizeOf<SolarPanelOpticalOutputDTO>() == SolarPowerGenerationConstants.SolarPanelOutputDtoSizeBytes &&
                   OffsetOf<SolarPanelOpticalOutputDTO>(nameof(SolarPanelOpticalOutputDTO.GeneratedWatts)) == 0 &&
                   OffsetOf<SolarPanelOpticalOutputDTO>(nameof(SolarPanelOpticalOutputDTO.PowerNodeHashID)) == 24 &&
                   OffsetOf<SolarPanelOpticalOutputDTO>(nameof(SolarPanelOpticalOutputDTO.Flags)) == 28;
        }

        public static bool ValidateTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<SolarTelemetryEntry>() == SolarPowerGenerationConstants.SolarTelemetryEntrySizeBytes &&
                   OffsetOf<SolarTelemetryEntry>(nameof(SolarTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<SolarTelemetryEntry>(nameof(SolarTelemetryEntry.TotalGeneratedWatts)) == 24 &&
                   OffsetOf<SolarTelemetryEntry>(nameof(SolarTelemetryEntry.MissingNodeCount)) == 60;
        }

        public static bool ValidateNodeInputCounterLayout()
        {
            return UnsafeUtility.SizeOf<SolarNodeInputCounter64>() == SolarPowerGenerationConstants.SolarNodeInputCounterSizeBytes &&
                   OffsetOf<SolarNodeInputCounter64>(nameof(SolarNodeInputCounter64.MilliWatts)) == 0 &&
                   OffsetOf<SolarNodeInputCounter64>(nameof(SolarNodeInputCounter64.Flags)) == 4;
        }

        public static bool ValidateProfileLayout()
        {
            return UnsafeUtility.SizeOf<SolarProfileDTO>() == SolarPowerGenerationConstants.SolarProfileDtoSizeBytes &&
                   OffsetOf<SolarProfileDTO>(nameof(SolarProfileDTO.ProfileHash)) == 0 &&
                   OffsetOf<SolarProfileDTO>(nameof(SolarProfileDTO.Reserved1)) == 28;
        }

        public static bool ValidateBlackBoxHeaderLayout()
        {
            return UnsafeUtility.SizeOf<SolarBlackBoxDumpHeaderDTO>() == SolarPowerGenerationConstants.SolarBlackBoxDumpHeaderSizeBytes &&
                   OffsetOf<SolarBlackBoxDumpHeaderDTO>(nameof(SolarBlackBoxDumpHeaderDTO.Magic)) == 0 &&
                   OffsetOf<SolarBlackBoxDumpHeaderDTO>(nameof(SolarBlackBoxDumpHeaderDTO.ReasonFlags)) == 8 &&
                   OffsetOf<SolarBlackBoxDumpHeaderDTO>(nameof(SolarBlackBoxDumpHeaderDTO.FrameIndex)) == 20 &&
                   OffsetOf<SolarBlackBoxDumpHeaderDTO>(nameof(SolarBlackBoxDumpHeaderDTO.Reserved1)) == 28;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }
#endif

    public static class SolarPowerVaultRuntime
    {
        public static bool EnsureBuffers(
            IDataVault vault,
            int panelCapacity,
            int powerNodeCapacity,
            out SolarPowerVaultHandles handles)
        {
            handles = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            int safePanels = math.clamp(panelCapacity, 1, SolarPowerGenerationConstants.DefaultPanelCapacity);
            int safeNodes = math.max(1, powerNodeCapacity);
            handles.PanelStates = vault.EnsureGenerationHandle<SolarPanelStateDTO>(
                SolarPowerBufferIds.PanelStates,
                safePanels,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.PanelOutputs = vault.EnsureGenerationHandle<SolarPanelOpticalOutputDTO>(
                SolarPowerBufferIds.PanelOutputs,
                safePanels,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.PanelPowerNodeIndices = vault.EnsureGenerationHandle<int>(
                SolarPowerBufferIds.PanelPowerNodeIndices,
                safePanels,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.NodeSolarInputMilliWatts = vault.EnsureGenerationHandle<SolarNodeInputCounter64>(
                SolarPowerBufferIds.NodeSolarInputMilliWatts,
                safeNodes,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.Conditions = vault.EnsureGenerationHandle<SolarConditionsDTO>(
                SolarPowerBufferIds.Conditions,
                1,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.TelemetryRing = vault.EnsureGenerationHandle<SolarTelemetryEntry>(
                SolarPowerBufferIds.TelemetryRing,
                SolarPowerGenerationConstants.TelemetryFrameCount,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.TelemetryCursor = vault.EnsureGenerationHandle<int>(
                SolarPowerBufferIds.TelemetryCursor,
                1,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            handles.Profiles = vault.EnsureGenerationHandle<SolarProfileDTO>(
                SolarPowerBufferIds.Profiles,
                128,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);
            handles.CsvScratch = vault.EnsureGenerationHandle<byte>(
                SolarPowerBufferIds.CsvScratch,
                SolarPowerGenerationConstants.CsvScratchBytes,
                SystemID.Power,
                NativeArrayOptions.UninitializedMemory);

            return HasResolvedBuffer(vault, in handles.PanelStates, safePanels) &&
                   HasResolvedBuffer(vault, in handles.PanelOutputs, safePanels) &&
                   HasResolvedBuffer(vault, in handles.PanelPowerNodeIndices, safePanels) &&
                   HasResolvedBuffer(vault, in handles.NodeSolarInputMilliWatts, safeNodes) &&
                   HasResolvedBuffer(vault, in handles.Conditions, 1) &&
                   HasResolvedBuffer(vault, in handles.TelemetryRing, SolarPowerGenerationConstants.TelemetryFrameCount) &&
                   HasResolvedBuffer(vault, in handles.TelemetryCursor, 1) &&
                   HasResolvedBuffer(vault, in handles.Profiles, 128) &&
                   HasResolvedBuffer(vault, in handles.CsvScratch, SolarPowerGenerationConstants.CsvScratchBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasResolvedBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle, int requiredLength)
            where T : struct
        {
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateMockSolarConditionsJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SolarConditionsDTO> Conditions;
        public double3 RuntimeOriginAUP;
        public float SimulationTimeSeconds;
        public float GlobalQualityWeight;
        public float TideHeightMeters;

        public void Execute(int index)
        {
            if (!Conditions.IsCreated || index != 0)
                return;

            float time = math.isfinite(SimulationTimeSeconds) ? SimulationTimeSeconds : 0f;
            float phase = time * 0.000011574074f;
            float day01 = phase - math.floor(phase);
            float horizon = day01 * 2f - 1f;
            float height = math.saturate(1f - math.abs(horizon) * 2f);
            float3 rawSun = new float3(horizon * 0.31f, height, (1f - math.abs(horizon)) * 0.19f);
            float rawLengthSq = math.lengthsq(rawSun);
            float3 sunDirection = rawLengthSq > 0.0001f ? rawSun * math.rsqrt(rawLengthSq) : new float3(0f, 1f, 0f);
            if (!math.all(math.isfinite(sunDirection)) || math.lengthsq(sunDirection) <= 0.0001f)
                sunDirection = new float3(0f, 1f, 0f);

            SolarConditionsDTO conditions = Conditions[0];
            conditions.RuntimeOriginAUP = RuntimeOriginAUP;
            conditions.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(RuntimeOriginAUP, TideHeightMeters);
            conditions.SunDirection = sunDirection;
            conditions.WaterAttenuationCoefficient = SolarPowerGenerationConstants.DefaultWaterAttenuationCoefficient;
            conditions.WaterTurbidity = math.lerp(1f, 1.8f, math.saturate(1f - height));
            conditions.TurbidityMultiplier = SolarPowerGenerationConstants.DefaultTurbidityMultiplier;
            conditions.InitialIntensityWatts = SolarPowerGenerationConstants.DefaultSolarIrradianceWatts;
            conditions.GlobalQualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
            conditions.SimulationTimeSeconds = time;
            conditions.DeltaTimeSeconds = math.max(0f, conditions.DeltaTimeSeconds);
            Conditions[0] = conditions;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ClearSolarNodeInputJob : IJobParallelFor
    {
        [NoAlias] public NativeArray<SolarNodeInputCounter64> NodeSolarInputMilliWatts;
        public int NodeCount;

        public void Execute(int index)
        {
            if (!NodeSolarInputMilliWatts.IsCreated || (uint)index >= (uint)NodeSolarInputMilliWatts.Length || (uint)index >= (uint)NodeCount)
                return;

            NodeSolarInputMilliWatts[index] = default;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct ResolveSolarPowerNodeIndicesJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<SolarPanelStateDTO> PanelStates;
        [ReadOnly, NoAlias] public NativeArray<PowerNodeDTO> PowerNodes;
        [NoAlias] public NativeArray<int> PanelPowerNodeIndices;
        public int PanelCount;
        public int NodeCount;

        public void Execute(int index)
        {
            if (!PanelStates.IsCreated || !PanelPowerNodeIndices.IsCreated || (uint)index >= (uint)PanelPowerNodeIndices.Length)
                return;

            int resolved = -1;
            if ((uint)index < (uint)PanelCount && (uint)index < (uint)PanelStates.Length && PowerNodes.IsCreated)
            {
                uint targetHash = PanelStates[index].PowerNodeHashID;
                if (targetHash != 0u)
                {
                    int limit = math.min(NodeCount, PowerNodes.Length);
                    int cached = PanelPowerNodeIndices[index];
                    if ((uint)cached < (uint)limit && PowerNodes[cached].NodeHash == targetHash)
                    {
                        resolved = cached;
                    }
                    else
                    {
                        for (int nodeIndex = 0; nodeIndex < limit; nodeIndex++)
                        {
                            if (PowerNodes[nodeIndex].NodeHash != targetHash)
                                continue;

                            resolved = nodeIndex;
                            break;
                        }
                    }
                }
            }

            PanelPowerNodeIndices[index] = resolved;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateOpticalDepthJob : IJobParallelFor
    {
        // INVARIANT: SolarPanelStateDTO, SolarPanelOpticalOutputDTO, and
        // SolarNodeInputCounter64 are separate Vault lanes locked by the owner
        // from schedule until dispatcher finalization; no job receives aliases.
        // ALTERNATIVE REJECTED: NativeArray indexers here force conservative
        // alias assumptions around atomics and block the raw CSR counter path.
        // SAFETY: pointers never escape Execute and are null/range checked.
        [NoAlias, NativeDisableUnsafePtrRestriction] [ReadOnly] public SolarPanelStateDTO* PanelStatesPtr;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SolarPanelOpticalOutputDTO* OutputsPtr;
        [NoAlias, NativeDisableUnsafePtrRestriction] public SolarNodeInputCounter64* NodeSolarInputCountersPtr;
        [ReadOnly, NoAlias] public NativeArray<SolarConditionsDTO> Conditions;
        [ReadOnly, NoAlias] public NativeArray<int> PanelPowerNodeIndices;
        [ReadOnly, NoAlias] public NativeArray<byte>.ReadOnly VoxelSdfTexture3D;
        public int PanelCount;
        public int NodeCount;
        public uint InputFlags;

        public void Execute(int index)
        {
            if (PanelStatesPtr == null || OutputsPtr == null || (uint)index >= (uint)PanelCount)
                return;

            SolarConditionsDTO conditions = Conditions.IsCreated && Conditions.Length > 0 ? Conditions[0] : default;
            SolarPanelStateDTO panel = PanelStatesPtr[index];
            SolarPanelOpticalOutputDTO output = default;
            output.PowerNodeHashID = panel.PowerNodeHashID;
            output.Flags = InputFlags;

            if (panel.PowerNodeHashID == 0u || panel.BaseEfficiencyScalar <= 0f || !math.isfinite(panel.BaseEfficiencyScalar))
            {
                OutputsPtr[index] = output;
                return;
            }

            float quality = Sanitize01(conditions.GlobalQualityWeight, 1f);
            float3 sunDirection = NormalizeWithFallback(conditions.SunDirection, new float3(0f, 1f, 0f));
            float angleMultiplier = ResolveSolarAngle(sunDirection, quality);
            double3 panelSeaDelta = panel.PanelAUP - conditions.SeaLevelAUP;
            float depthMeters = math.max(0f, -(float)panelSeaDelta.y);
            float turbidity = math.max(0f, SanitizeFinite(conditions.WaterTurbidity, 1f));
            float attenuationCoefficient = math.max(0.000001f, SanitizeFinite(conditions.WaterAttenuationCoefficient, SolarPowerGenerationConstants.DefaultWaterAttenuationCoefficient));
            float turbidityMultiplier = math.max(0f, SanitizeFinite(conditions.TurbidityMultiplier, SolarPowerGenerationConstants.DefaultTurbidityMultiplier));
            float opticalDepth = depthMeters * attenuationCoefficient * (1f + turbidity * turbidityMultiplier);
            float attenuation = ResolveBeerLambert(opticalDepth, quality);
            float shadowMultiplier = angleMultiplier > 0.0001f
                ? ResolveShadowMultiplier(panel.PanelAUP, in conditions, sunDirection, quality, ref output.Flags)
                : 1f;
            float irradiance = math.max(0f, SanitizeFinite(conditions.InitialIntensityWatts, SolarPowerGenerationConstants.DefaultSolarIrradianceWatts)) *
                               attenuation *
                               angleMultiplier *
                               shadowMultiplier;
            float generatedWatts = irradiance * math.max(0f, panel.BaseEfficiencyScalar);
            if (!math.isfinite(generatedWatts) || !math.isfinite(opticalDepth))
            {
                generatedWatts = 0f;
                opticalDepth = 0f;
                output.Flags |= SolarPowerGenerationConstants.FlagNonFinite;
            }

            output.GeneratedWatts = math.max(0f, generatedWatts);
            output.IrradianceWatts = math.max(0f, irradiance);
            output.OpticalDepthMeters = math.max(0f, opticalDepth);
            output.AngleMultiplier = angleMultiplier;
            output.ShadowMultiplier = shadowMultiplier;
            output.DepthMeters = depthMeters;
            int resolvedNodeIndex = -1;
            if (PanelPowerNodeIndices.IsCreated && (uint)index < (uint)PanelPowerNodeIndices.Length)
                resolvedNodeIndex = PanelPowerNodeIndices[index];
            if ((uint)resolvedNodeIndex >= (uint)NodeCount)
                output.Flags |= SolarPowerGenerationConstants.FlagMissingPowerNode;
            OutputsPtr[index] = output;

            if (NodeSolarInputCountersPtr == null || (uint)resolvedNodeIndex >= (uint)NodeCount)
                return;

            int milliWatts = (int)math.clamp(math.round(output.GeneratedWatts * 1000f), 0f, 2147483000f);
            if (milliWatts <= 0)
                return;

            ref int target = ref UnsafeUtility.AsRef<int>(&NodeSolarInputCountersPtr[resolvedNodeIndex].MilliWatts);
            Interlocked.Add(ref target, milliWatts);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveShadowMultiplier(double3 panelAup, in SolarConditionsDTO conditions, float3 sunDirection, float quality, ref uint flags)
        {
            double3 runtimeDelta = panelAup - conditions.RuntimeOriginAUP;
            float3 panelRuntime = new float3((float)runtimeDelta.x, (float)runtimeDelta.y, (float)runtimeDelta.z);
            float sdfShadow = ResolveVoxelSdfShadow(panelAup, in conditions, sunDirection, quality, ref flags);
            float analyticShadow = ResolveAnalyticMountainShadow(panelRuntime, sunDirection, conditions.SimulationTimeSeconds, quality);
            float blendedShadow = math.min(sdfShadow, analyticShadow);
            if (blendedShadow < 0.999f)
                flags |= SolarPowerGenerationConstants.FlagSdfShadowed;
            return math.saturate(blendedShadow);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveVoxelSdfShadow(double3 panelAup, in SolarConditionsDTO conditions, float3 sunDirection, float quality, ref uint flags)
        {
            if (!IsVoxelSdfPayloadValid(in conditions))
            {
                flags |= SolarPowerGenerationConstants.FlagMissingSdfVault;
                return 1f;
            }

            double3 sdfLocalDouble = panelAup - conditions.VoxelSdfOriginAUP;
            float3 panelSdfLocal = new float3((float)sdfLocalDouble.x, (float)sdfLocalDouble.y, (float)sdfLocalDouble.z);
            float sampleBudget = math.lerp(1f, 9f, quality);
            int sampleCount = math.clamp((int)math.ceil(sampleBudget), 1, 9);
            float stepMeters = math.lerp(24f, 6f, quality);
            float occlusion = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float3 sampleLocal = panelSdfLocal + sunDirection * (stepMeters * i);
                float3 grid = sampleLocal / math.max(conditions.VoxelSdfCellSize, new float3(0.0001f));
                if (!IsInsideGrid(grid, conditions.VoxelSdfDimensions))
                    continue;

                float signed = SampleVoxelSignedTrilinear(grid, conditions.VoxelSdfDimensions, conditions.VoxelSdfRangeMeters, quality);
                if (!math.isfinite(signed))
                    continue;

                float solidOcclusion = 1f - Smooth01(-2.0f, 0.5f, signed);
                solidOcclusion *= math.saturate(sampleBudget - i);
                occlusion = math.max(occlusion, solidOcclusion);
            }

            float floor = math.lerp(0.22f, 0.04f, quality);
            return math.lerp(1f, floor, math.saturate(occlusion));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveAnalyticMountainShadow(float3 panelRuntime, float3 sunDirection, float time, float quality)
        {
            float2 xz = panelRuntime.xz + sunDirection.xz * math.lerp(40f, 140f, quality);
            float waveA = TriangleWave01(xz.x * 0.0041f + time * 0.00013f);
            float waveB = TriangleWave01(xz.y * 0.0037f - time * 0.00011f);
            float ridgeSignal = waveA + waveB - 1f;
            float ridgeHeight = math.lerp(18f, 64f, Smooth01(-0.35f, 0.8f, ridgeSignal));
            float sunHeight = math.max(0.001f, sunDirection.y) * math.lerp(180f, 520f, quality);
            float blocker = ridgeHeight - (panelRuntime.y + sunHeight);
            float occlusion = Smooth01(-16f, 12f, blocker);
            return math.lerp(1f, math.lerp(0.35f, 0.08f, quality), occlusion);
        }

        private float SampleVoxelSignedTrilinear(float3 grid, int3 dimensions, float rangeMeters, float quality)
        {
            int nearestIndex = ClampVoxelIndex(new int3((int)math.round(grid.x), (int)math.round(grid.y), (int)math.round(grid.z)), dimensions);
            float nearest = DecodeVoxelSigned(nearestIndex, rangeMeters);
            float trilinearBlend = Smooth01(0.12f, 0.35f, quality);
            if (trilinearBlend <= 0.0001f)
                return nearest;

            float3 clamped = math.clamp(grid, float3.zero, new float3(dimensions.x - 1.001f, dimensions.y - 1.001f, dimensions.z - 1.001f));
            float3 floorGrid = math.floor(clamped);
            int3 p0 = new int3((int)floorGrid.x, (int)floorGrid.y, (int)floorGrid.z);
            int3 p1 = math.min(p0 + new int3(1, 1, 1), dimensions - new int3(1));
            float3 f = math.saturate(clamped - floorGrid);
            float c000 = DecodeVoxelSigned(SdfIndex(p0.x, p0.y, p0.z, dimensions), rangeMeters);
            float c100 = DecodeVoxelSigned(SdfIndex(p1.x, p0.y, p0.z, dimensions), rangeMeters);
            float c010 = DecodeVoxelSigned(SdfIndex(p0.x, p1.y, p0.z, dimensions), rangeMeters);
            float c110 = DecodeVoxelSigned(SdfIndex(p1.x, p1.y, p0.z, dimensions), rangeMeters);
            float c001 = DecodeVoxelSigned(SdfIndex(p0.x, p0.y, p1.z, dimensions), rangeMeters);
            float c101 = DecodeVoxelSigned(SdfIndex(p1.x, p0.y, p1.z, dimensions), rangeMeters);
            float c011 = DecodeVoxelSigned(SdfIndex(p0.x, p1.y, p1.z, dimensions), rangeMeters);
            float c111 = DecodeVoxelSigned(SdfIndex(p1.x, p1.y, p1.z, dimensions), rangeMeters);
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            return math.lerp(nearest, math.lerp(c0, c1, f.z), trilinearBlend);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ClampVoxelIndex(int3 p, int3 dimensions)
        {
            int x = math.clamp(p.x, 0, dimensions.x - 1);
            int y = math.clamp(p.y, 0, dimensions.y - 1);
            int z = math.clamp(p.z, 0, dimensions.z - 1);
            return SdfIndex(x, y, z, dimensions);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DecodeVoxelSigned(int index, float rangeMeters)
        {
            if (!VoxelSdfTexture3D.IsCreated || (uint)index >= (uint)VoxelSdfTexture3D.Length)
                return -math.max(0.0001f, rangeMeters);

            return ((VoxelSdfTexture3D[index] * 0.0039215686274509803f) * 2.0f - 1.0f) * math.max(0.0001f, rangeMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int SdfIndex(int x, int y, int z, int3 dimensions)
        {
            return x + dimensions.x * (y + dimensions.y * z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsVoxelSdfPayloadValid(in SolarConditionsDTO conditions)
        {
            int3 dimensions = conditions.VoxelSdfDimensions;
            long expected = (long)dimensions.x * dimensions.y * dimensions.z;
            return VoxelSdfTexture3D.IsCreated &&
                   dimensions.x > 1 &&
                   dimensions.y > 1 &&
                   dimensions.z > 1 &&
                   expected > 0L &&
                   expected <= VoxelSdfTexture3D.Length &&
                   math.all(math.isfinite(conditions.VoxelSdfOriginAUP)) &&
                   math.all(math.isfinite(conditions.VoxelSdfCellSize)) &&
                   math.all(conditions.VoxelSdfCellSize > new float3(0.0001f)) &&
                   math.isfinite(conditions.VoxelSdfRangeMeters) &&
                   conditions.VoxelSdfRangeMeters > 0.0001f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInsideGrid(float3 grid, int3 dimensions)
        {
            return grid.x >= 0f && grid.y >= 0f && grid.z >= 0f &&
                   grid.x <= dimensions.x - 1f &&
                   grid.y <= dimensions.y - 1f &&
                   grid.z <= dimensions.z - 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveSolarAngle(float3 sunDirection, float quality)
        {
            float raw = math.max(0f, sunDirection.y);
            float lowTier = math.step(0.0001f, raw);
            float highTier = Smooth01(0.015f, 0.18f, raw);
            return math.lerp(lowTier, highTier, math.saturate(quality));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveBeerLambert(float opticalDepth, float quality)
        {
            float x = math.clamp(MathLodApproximation.FiniteOr(opticalDepth, 0f), 0f, 40f);
            float cheap = math.rcp(1f + x + 0.5f * x * x);
            float pade = MathLodApproximation.ApproxExpNegPade33Wide40(x);
            return math.saturate(MathLodApproximation.BlendByQuality(cheap, pade, quality, 0.30f, 0.85f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeWithFallback(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.all(math.isfinite(value)) || lengthSq <= 0.000001f)
                return fallback;
            return value * math.rsqrt(lengthSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float edge0, float edge1, float value)
        {
            float t = math.saturate((value - edge0) * math.rcp(math.max(0.000001f, edge1 - edge0)));
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWave01(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return 1f - math.abs(wrapped * 2f - 1f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ApplySolarPowerToCsrNodesJob : IJobParallelFor
    {
        // INVARIANT: PowerNodeDTO rows are locked as the CSR owner lane and
        // solar counters are read-only 64-byte rows from a distinct Vault lane.
        // ALTERNATIVE REJECTED: managed PowerGrid traversal would reintroduce
        // object events and transform the CSR solve into cache-miss fan-out.
        // SAFETY: the node pointer is range checked before UnsafeUtility.AsRef.
        [NoAlias, NativeDisableUnsafePtrRestriction] public PowerNodeDTO* NodesPtr;
        [ReadOnly, NoAlias] public NativeArray<SolarNodeInputCounter64> NodeSolarInputMilliWatts;
        public int NodeCount;
        public float DeltaTimeSeconds;

        public void Execute(int index)
        {
            if (NodesPtr == null || !NodeSolarInputMilliWatts.IsCreated || (uint)index >= (uint)NodeCount || (uint)index >= (uint)NodeSolarInputMilliWatts.Length)
                return;

            int milliWatts = NodeSolarInputMilliWatts[index].MilliWatts;
            if (milliWatts <= 0)
                return;

            float watts = milliWatts * 0.001f;
            ref PowerNodeDTO node = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + index);
            float existingCapacity = math.max(0f, math.isfinite(node.MaxCapacity) ? node.MaxCapacity : 0f);
            float capacity = math.max(SolarPowerGenerationConstants.InjectedSourceCapacityFloorWatts, math.max(existingCapacity, watts));
            float potential = math.saturate(watts * math.rcp(math.max(1f, capacity)));
            float dt = math.max(0f, math.isfinite(DeltaTimeSeconds) ? DeltaTimeSeconds : 0f);
            node.Flags |= PowerGridJacobiConstants.NodeFlagActive | PowerGridJacobiConstants.NodeFlagSource;
            node.MaxCapacity = capacity;
            node.Potential = math.max(math.saturate(math.isfinite(node.Potential) ? node.Potential : 0f), potential);
            node.CurrentStorage = math.clamp((math.isfinite(node.CurrentStorage) ? node.CurrentStorage : 0f) + watts * dt, 0f, capacity);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordSolarTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<SolarPanelOpticalOutputDTO> Outputs;
        [ReadOnly, NoAlias] public NativeArray<SolarConditionsDTO> Conditions;
        [NoAlias] public NativeArray<SolarTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        public uint FrameIndex;
        public int PanelCount;
        public int NodeCount;
        public int SdfSampleCount;
        public uint InputFlags;
        public uint SolverMicrosecondsEstimate;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length <= 0)
                return;

            SolarConditionsDTO conditions = Conditions.IsCreated && Conditions.Length > 0 ? Conditions[0] : default;
            int panelLimit = math.clamp(PanelCount, 0, Outputs.IsCreated ? Outputs.Length : 0);
            float totalWatts = 0f;
            float peakWatts = 0f;
            float depthSum = 0f;
            float opticalDepthSum = 0f;
            float angleSum = 0f;
            uint reasonFlags = InputFlags;
            uint stateHash = 2166136261u;
            int shadowed = 0;
            int missingNodes = 0;

            for (int i = 0; i < panelLimit; i++)
            {
                SolarPanelOpticalOutputDTO output = Outputs[i];
                float watts = SanitizePositive(output.GeneratedWatts, ref reasonFlags);
                float depth = SanitizePositive(output.DepthMeters, ref reasonFlags);
                float optical = SanitizePositive(output.OpticalDepthMeters, ref reasonFlags);
                totalWatts += watts;
                peakWatts = math.max(peakWatts, watts);
                depthSum += depth;
                opticalDepthSum += optical;
                angleSum += math.saturate(output.AngleMultiplier);
                if ((output.Flags & SolarPowerGenerationConstants.FlagSdfShadowed) != 0u)
                    shadowed++;
                if ((output.Flags & SolarPowerGenerationConstants.FlagMissingPowerNode) != 0u)
                    missingNodes++;

                stateHash = Mix(stateHash, output.PowerNodeHashID);
                stateHash = Mix(stateHash, math.asuint(watts));
                stateHash = Mix(stateHash, math.asuint(output.ShadowMultiplier));
            }

            if (SolverMicrosecondsEstimate > SolarPowerGenerationConstants.SolverBudgetMicroseconds)
                reasonFlags |= SolarPowerGenerationConstants.FlagSolverOverBudget;

            float invPanels = panelLimit > 0 ? math.rcp(panelLimit) : 0f;
            SolarTelemetryEntry entry = default;
            entry.FrameIndex = FrameIndex;
            entry.StateHash = stateHash;
            entry.ReasonFlags = reasonFlags;
            entry.ActivePanelCount = panelLimit;
            entry.NodeCount = math.max(0, NodeCount);
            entry.SdfSampleCount = math.max(0, SdfSampleCount);
            entry.TotalGeneratedWatts = totalWatts;
            entry.PeakPanelWatts = peakWatts;
            entry.AverageDepthMeters = depthSum * invPanels;
            entry.AverageOpticalDepth = opticalDepthSum * invPanels;
            entry.GlobalQualityWeight = math.saturate(math.isfinite(conditions.GlobalQualityWeight) ? conditions.GlobalQualityWeight : 1f);
            entry.SolarAngleMultiplier = angleSum * invPanels;
            entry.TurbidityScalar = math.max(0f, math.isfinite(conditions.WaterTurbidity) ? conditions.WaterTurbidity : 1f);
            entry.SolverMicroseconds = SolverMicrosecondsEstimate;
            entry.ShadowedPanelCount = shadowed;
            entry.MissingNodeCount = missingNodes;

            int index = ResolveWriteIndex();
            TelemetryRing[index] = entry;
        }

        private int ResolveWriteIndex()
        {
            if (!TelemetryCursor.IsCreated || TelemetryCursor.Length <= 0)
                return (int)(FrameIndex % (uint)TelemetryRing.Length);

            int cursor = math.max(0, TelemetryCursor[0]);
            int writeIndex = cursor % TelemetryRing.Length;
            TelemetryCursor[0] = cursor == int.MaxValue ? 0 : cursor + 1;
            return writeIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, ref uint reasonFlags)
        {
            if (math.isfinite(value))
                return math.max(0f, value);

            reasonFlags |= SolarPowerGenerationConstants.FlagNonFinite;
            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    public static unsafe class SolarPowerGenerationRuntime
    {
        private const SystemID OwnerSystem = SystemID.Power;
        private const uint CelestialReadBufferId = unchecked((uint)(int)BufferID.Shinobu345CelestialStateRead);
        private const uint EnvironmentStateBufferId = unchecked((uint)(int)BufferID.Shinobu345EnvironmentState);

        private static IDataVault s_vault;
        private static SolarPowerVaultHandles s_handles;
        private static VaultGenerationHandle<CelestialStateDTO> s_celestialStateReadHandle;
        private static VaultGenerationHandle<EnvironmentStateDTO> s_environmentStateHandle;
        private static JobHandle s_pendingHandle;
        private static IDataVault s_jobMutationGuardVault;
        private static IDataVault s_panelStateWriteVault;
        private static bool s_jobMutationGuardHeld;
        private static long s_scheduleTimestamp;
        private static uint s_frameIndex;
        private static uint s_completedOutputFrameIndex;
        private static int s_panelCapacity;
        private static int s_powerNodeCapacity;
        private static bool s_buffersReady;
        private static bool s_pending;
        private static bool s_hasCompletedOutput;
        private static bool s_blackBoxDumped;
        private static bool s_conditionsInitialized;
        private static SolarConditionsDTO s_offlineTuning = DefaultConditions();
        private static readonly ulong JobMutationGuardMask =
            SolarBufferGuardBit(SolarPowerBufferIds.PanelStates) |
            SolarBufferGuardBit(SolarPowerBufferIds.PanelOutputs) |
            SolarBufferGuardBit(SolarPowerBufferIds.PanelPowerNodeIndices) |
            SolarBufferGuardBit(SolarPowerBufferIds.NodeSolarInputMilliWatts) |
            SolarBufferGuardBit(SolarPowerBufferIds.Conditions) |
            SolarBufferGuardBit(SolarPowerBufferIds.TelemetryRing) |
            SolarBufferGuardBit(SolarPowerBufferIds.TelemetryCursor) |
            SolarBufferGuardBit(PowerGridBufferIds.Nodes) |
            SolarBufferGuardBit(BufferID.VoxelSdfPayloadDescriptor) |
            SolarBufferGuardBit(BufferID.VoxelSdfTexture3D);

        public static bool HasPendingJob => s_pending;

        public static void ResetForSubsystemRegistration()
        {
            if (s_pending)
                ForceCompletePendingJobInPostSimulationWindow();

            ReleasePanelStateWrite();
            UnlockJobBuffers();
            s_vault = null;
            s_handles = default;
            s_celestialStateReadHandle = default;
            s_environmentStateHandle = default;
            s_pendingHandle = default;
            s_jobMutationGuardVault = null;
            s_panelStateWriteVault = null;
            s_jobMutationGuardHeld = false;
            s_scheduleTimestamp = 0L;
            s_frameIndex = 0u;
            s_completedOutputFrameIndex = 0u;
            s_panelCapacity = 0;
            s_powerNodeCapacity = 0;
            s_buffersReady = false;
            s_pending = false;
            s_hasCompletedOutput = false;
            s_blackBoxDumped = false;
            s_conditionsInitialized = false;
            s_offlineTuning = DefaultConditions();
        }

        public static bool TryEnsure(int panelCapacity, int powerNodeCapacity)
        {
            IDataVault vault = BindDataVaultCold();
            if (vault == null)
                return false;

            int safePanels = math.clamp(panelCapacity, 1, SolarPowerGenerationConstants.DefaultPanelCapacity);
            int safeNodes = math.max(1, powerNodeCapacity);
            RefreshBorrowedCelestialHandles(vault);
            if (s_buffersReady && ReferenceEquals(s_vault, vault) && safePanels <= s_panelCapacity && safeNodes <= s_powerNodeCapacity)
                return true;

            if (!SolarPowerVaultRuntime.EnsureBuffers(vault, safePanels, safeNodes, out s_handles))
                return false;

            s_vault = vault;
            s_panelCapacity = safePanels;
            s_powerNodeCapacity = safeNodes;
            s_buffersReady = true;
            if (!s_conditionsInitialized)
            {
                SolarConditionsDTO defaults = SanitizeConditions(s_offlineTuning);
                if (!WriteConditionsRow(in defaults))
                    return false;
                s_conditionsInitialized = true;
            }

            return true;
        }

        public static bool TryPrepareCold(int panelCapacity, int powerNodeCapacity)
        {
            return TryEnsure(panelCapacity, powerNodeCapacity);
        }

        public static bool TryWritePanelState(int slot, in SolarPanelStateDTO state)
        {
            if (s_pending || !TryEnsure(SolarPowerGenerationConstants.DefaultPanelCapacity, SolarPowerGenerationConstants.DefaultPowerNodeCapacity))
                return false;

            IDataVault vault = s_vault;
            if ((uint)slot >= (uint)s_panelCapacity ||
                vault == null ||
                !vault.TryAcquireWriteLock(in s_handles.PanelStates, OwnerSystem, out NativeArray<SolarPanelStateDTO> states))
            {
                return false;
            }

            try
            {
                if (!states.IsCreated || (uint)slot >= (uint)states.Length)
                    return false;

                SolarPanelStateDTO* ptr = (SolarPanelStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states);
                UnsafeUtility.AsRef<SolarPanelStateDTO>(ptr + slot) = state;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_handles.PanelStates, OwnerSystem);
            }
        }

        public static bool TryAcquirePanelStateWrite(out NativeArray<SolarPanelStateDTO> states)
        {
            states = default;
            if (s_pending ||
                s_panelStateWriteVault != null ||
                !HasPreparedBuffers(SolarPowerGenerationConstants.DefaultPanelCapacity, SolarPowerGenerationConstants.DefaultPowerNodeCapacity))
            {
                return false;
            }

            IDataVault vault = s_vault;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in s_handles.PanelStates, OwnerSystem, out states))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (states.IsCreated && states.Length >= s_panelCapacity)
                {
                    s_panelStateWriteVault = vault;
                    keepLock = true;
                    return true;
                }

                states = default;
                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in s_handles.PanelStates, OwnerSystem);
                    states = default;
                }
            }
        }

        public static void ReleasePanelStateWrite()
        {
            IDataVault vault = s_panelStateWriteVault;
            s_panelStateWriteVault = null;
            if (vault != null && s_handles.PanelStates.BufferID != 0u)
                vault.ReleaseWriteLock(in s_handles.PanelStates, OwnerSystem);
        }

        public static bool TryClearPanelState(int slot)
        {
            SolarPanelStateDTO state = default;
            return TryWritePanelState(slot, in state);
        }

        public static bool TrySchedule(int panelCount, in SolarConditionsDTO requestedConditions, float deltaSeconds, bool forceMockConditions)
        {
            if (s_pending)
                return false;

            if (!HasPreparedBuffers(SolarPowerGenerationConstants.DefaultPanelCapacity, SolarPowerGenerationConstants.DefaultPowerNodeCapacity))
                return false;

            int activePanels = math.clamp(panelCount, 0, s_panelCapacity);
            UnlockJobBuffers();

            if (!TryAcquireJobMutationGuard())
                return false;

            bool scheduled = false;
            try
            {
                if (!ResolveCoreBuffers(
                        out NativeArray<SolarPanelStateDTO> states,
                        out NativeArray<SolarPanelOpticalOutputDTO> outputs,
                        out NativeArray<int> nodeIndices,
                        out NativeArray<SolarNodeInputCounter64> nodeSolarInput,
                        out NativeArray<SolarConditionsDTO> conditions,
                        out NativeArray<SolarTelemetryEntry> telemetry,
                        out NativeArray<int> telemetryCursor))
                {
                    return false;
                }

                bool hasPowerNodes = TryResolveExistingPowerNodes(out NativeArray<PowerNodeDTO> powerNodes);
                NativeArray<byte>.ReadOnly voxelSdf = default;
                SolarConditionsDTO conditionRow = SanitizeConditions(requestedConditions);
                uint inputFlags = 0u;
                if (!hasPowerNodes)
                    inputFlags |= SolarPowerGenerationConstants.FlagMissingPowerVault;
                if (!TryAcquireVoxelSdfPayload(ref conditionRow, out voxelSdf))
                    inputFlags |= SolarPowerGenerationConstants.FlagMissingSdfVault;
                if (forceMockConditions)
                    inputFlags |= SolarPowerGenerationConstants.FlagMockConditions;

                conditionRow.DeltaTimeSeconds = math.max(0f, deltaSeconds);

                SolarConditionsDTO* conditionPtr = (SolarConditionsDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(conditions);
                UnsafeUtility.AsRef<SolarConditionsDTO>(conditionPtr) = conditionRow;
                SolarPanelStateDTO* statesPtr = (SolarPanelStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
                SolarPanelOpticalOutputDTO* outputsPtr = (SolarPanelOpticalOutputDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(outputs);
                SolarNodeInputCounter64* solarInputPtr = nodeSolarInput.IsCreated && nodeSolarInput.Length > 0
                    ? (SolarNodeInputCounter64*)NativeArrayUnsafeUtility.GetUnsafePtr(nodeSolarInput)
                    : null;
                PowerNodeDTO* powerNodesPtr = hasPowerNodes && powerNodes.IsCreated && powerNodes.Length > 0
                    ? (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(powerNodes)
                    : null;
                int nodeCount = hasPowerNodes ? math.min(powerNodes.Length, nodeSolarInput.Length) : 0;
                JobHandle dependency = default;
                if (forceMockConditions)
                {
                    dependency = new GenerateMockSolarConditionsJob
                    {
                        Conditions = conditions,
                        RuntimeOriginAUP = conditionRow.RuntimeOriginAUP,
                        SimulationTimeSeconds = conditionRow.SimulationTimeSeconds,
                        GlobalQualityWeight = conditionRow.GlobalQualityWeight,
                        TideHeightMeters = (float)(conditionRow.SeaLevelAUP.y - conditionRow.RuntimeOriginAUP.y)
                    }.Schedule(1, 1, dependency);
                }

                dependency = new ClearSolarNodeInputJob
                {
                    NodeSolarInputMilliWatts = nodeSolarInput,
                    NodeCount = nodeCount
                }.Schedule(math.max(1, nodeCount), 64, dependency);

                dependency = new ResolveSolarPowerNodeIndicesJob
                {
                    PanelStates = states,
                    PowerNodes = powerNodes,
                    PanelPowerNodeIndices = nodeIndices,
                    PanelCount = activePanels,
                    NodeCount = nodeCount
                }.Schedule(math.max(1, activePanels), 64, dependency);

                int sdfSamples = activePanels * math.clamp((int)math.round(math.lerp(1f, 9f, conditionRow.GlobalQualityWeight)), 1, 9);
                dependency = new EvaluateOpticalDepthJob
                {
                    PanelStatesPtr = statesPtr,
                    OutputsPtr = outputsPtr,
                    NodeSolarInputCountersPtr = solarInputPtr,
                    Conditions = conditions,
                    PanelPowerNodeIndices = nodeIndices,
                    VoxelSdfTexture3D = voxelSdf,
                    PanelCount = activePanels,
                    NodeCount = nodeCount,
                    InputFlags = inputFlags
                }.Schedule(math.max(1, activePanels), 64, dependency);

                if (hasPowerNodes)
                {
                    dependency = new ApplySolarPowerToCsrNodesJob
                    {
                        NodesPtr = powerNodesPtr,
                        NodeSolarInputMilliWatts = nodeSolarInput,
                        NodeCount = nodeCount,
                        DeltaTimeSeconds = conditionRow.DeltaTimeSeconds
                    }.Schedule(math.max(1, nodeCount), 64, dependency);
                }

                s_pendingHandle = new RecordSolarTelemetryJob
                {
                    Outputs = outputs,
                    Conditions = conditions,
                    TelemetryRing = telemetry,
                    TelemetryCursor = telemetryCursor,
                    FrameIndex = s_frameIndex,
                    PanelCount = activePanels,
                    NodeCount = nodeCount,
                    SdfSampleCount = sdfSamples,
                    InputFlags = inputFlags,
                    SolverMicrosecondsEstimate = EstimateMicroseconds(activePanels, nodeCount, sdfSamples, conditionRow.GlobalQualityWeight)
                }.Schedule(dependency);
                H8Memory.RegisterActiveJob(OwnerSystem, s_pendingHandle);
                s_scheduleTimestamp = Stopwatch.GetTimestamp();
                s_pending = true;
                s_frameIndex++;
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                    UnlockJobBuffers();
            }
        }

        public static bool TryFinalize()
        {
            if (!s_pending)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref s_pendingHandle))
                return false;

            uint dumpFlags = 0u;
            bool shouldDump = false;
            try
            {
                s_pending = false;
                uint elapsed = ResolveElapsedMicroseconds(s_scheduleTimestamp);
                StampSolverWallTime(elapsed);
                SolarTelemetryEntry entry = ReadLatestTelemetry();
                s_completedOutputFrameIndex = entry.FrameIndex;
                s_hasCompletedOutput = true;
                dumpFlags = entry.ReasonFlags | (elapsed > SolarPowerGenerationConstants.SolverBudgetMicroseconds ? SolarPowerGenerationConstants.FlagSolverOverBudget : 0u);
                shouldDump = (entry.ReasonFlags & (SolarPowerGenerationConstants.FlagNonFinite | SolarPowerGenerationConstants.FlagSolverOverBudget)) != 0u ||
                             elapsed > SolarPowerGenerationConstants.SolverBudgetMicroseconds;
            }
            finally
            {
                UnlockJobBuffers();
            }

            if (shouldDump)
                DumpBlackBoxOnce(dumpFlags);

            return true;
        }

        public static bool TryReadOutput(int slot, out SolarPanelOpticalOutputDTO output)
        {
            output = default;
            if (!s_buffersReady ||
                s_pending ||
                (uint)slot >= (uint)s_panelCapacity ||
                s_vault == null ||
                !s_vault.TryReadOnlyHandle(in s_handles.PanelOutputs, out NativeArray<SolarPanelOpticalOutputDTO>.ReadOnly outputs) ||
                (uint)slot >= (uint)outputs.Length)
            {
                return false;
            }

            output = outputs[slot];
            return true;
        }

        public static bool TryReadOutputSnapshot(out NativeArray<SolarPanelOpticalOutputDTO>.ReadOnly outputs, out uint frameIndex)
        {
            outputs = default;
            frameIndex = 0u;
            if (!s_buffersReady ||
                s_pending ||
                !s_hasCompletedOutput ||
                s_vault == null ||
                !s_vault.TryReadOnlyHandle(in s_handles.PanelOutputs, out outputs) ||
                outputs.Length <= 0)
            {
                return false;
            }

            frameIndex = s_completedOutputFrameIndex;
            return true;
        }

        public static bool TryGetCompletedOutputFrameIndex(out uint frameIndex)
        {
            frameIndex = 0u;
            if (!s_buffersReady || s_pending || !s_hasCompletedOutput)
                return false;

            frameIndex = s_completedOutputFrameIndex;
            return true;
        }

        public static bool TryReadPanelState(int slot, out SolarPanelStateDTO state)
        {
            state = default;
            if (!s_buffersReady ||
                s_pending ||
                (uint)slot >= (uint)s_panelCapacity ||
                s_vault == null ||
                !s_vault.TryReadOnlyHandle(in s_handles.PanelStates, out NativeArray<SolarPanelStateDTO>.ReadOnly states) ||
                (uint)slot >= (uint)states.Length)
            {
                return false;
            }

            state = states[slot];
            return state.PowerNodeHashID != 0u;
        }

        public static bool TryReadLatestTelemetry(out SolarTelemetryEntry entry)
        {
            entry = default;
            if (!s_buffersReady || s_pending)
                return false;

            entry = ReadLatestTelemetry();
            return entry.FrameIndex != 0u || entry.StateHash != 0u || entry.ActivePanelCount != 0;
        }

        public static bool TryReadCelestialSnapshot(out CelestialRuntimeSnapshot snapshot)
        {
            snapshot = default;
            IDataVault vault = s_vault;
            if (!s_buffersReady ||
                vault == null ||
                s_celestialStateReadHandle.BufferID != CelestialReadBufferId ||
                !vault.TryReadOnlyHandle(in s_celestialStateReadHandle, out NativeArray<CelestialStateDTO>.ReadOnly celestialStates) ||
                celestialStates.Length <= 0)
            {
                return false;
            }

            CelestialStateDTO state = celestialStates[0];
            if (!TryNormalizeDouble3(state.SunDirection, out float3 sunDirection))
                return false;

            snapshot.SunDirection = sunDirection;
            snapshot.EclipseOcclusion01 = math.saturate(math.isfinite(state.EclipseShadowScalar01) ? state.EclipseShadowScalar01 : 0f);
            snapshot.Flags = (uint)CelestialRuntimeFlags.Valid;
            if (snapshot.EclipseOcclusion01 > 0.0001f)
                snapshot.Flags |= (uint)CelestialRuntimeFlags.EclipseActive;

            if (s_environmentStateHandle.BufferID == EnvironmentStateBufferId &&
                vault.TryReadOnlyHandle(in s_environmentStateHandle, out NativeArray<EnvironmentStateDTO>.ReadOnly environmentStates) &&
                environmentStates.Length > 0)
            {
                EnvironmentStateDTO environment = environmentStates[0];
                snapshot.AbsoluteUniverseTime = math.isfinite(environment.CurrentSimulationTime) ? environment.CurrentSimulationTime : 0d;
                snapshot.TideHeightMeters = math.isfinite(environment.GlobalTideLevel) ? environment.GlobalTideLevel : 0f;
                if (TryNormalizeDouble3(environment.TideVector, out float3 tidePull))
                    snapshot.TidePullVector = tidePull;
                snapshot.Sequence = environment.Sequence;
            }

            return true;
        }

        public static bool TryCopyTelemetry(SolarTelemetryEntry[] target, out int count)
        {
            count = 0;
            if (target == null || target.Length <= 0 || !s_buffersReady || s_pending || s_vault == null)
                return false;

            if (!s_vault.TryReadOnlyHandle(in s_handles.TelemetryRing, out NativeArray<SolarTelemetryEntry>.ReadOnly ring) ||
                !s_vault.TryReadOnlyHandle(in s_handles.TelemetryCursor, out NativeArray<int>.ReadOnly cursor) ||
                cursor.Length <= 0)
            {
                return false;
            }

            int capacity = math.min(ring.Length, SolarPowerGenerationConstants.TelemetryFrameCount);
            int available = math.min(capacity, math.max(0, cursor[0]));
            count = math.min(target.Length, available);
            for (int i = 0; i < count; i++)
            {
                int index = (cursor[0] - 1 - i) % capacity;
                if (index < 0)
                    index += capacity;
                target[i] = ring[index];
            }

            return true;
        }

        public static bool TryGetTuning(out SolarConditionsDTO tuning)
        {
            tuning = s_offlineTuning;
            if (!s_buffersReady || s_vault == null)
                return false;

            if (s_vault.TryReadOnlyHandle(in s_handles.Conditions, out NativeArray<SolarConditionsDTO>.ReadOnly conditions) &&
                conditions.Length > 0)
            {
                tuning = SanitizeConditions(conditions[0]);
                return true;
            }

            return false;
        }

        public static void SetTuning(in SolarConditionsDTO tuning)
        {
            SolarConditionsDTO sanitized = SanitizeConditions(tuning);
            s_offlineTuning = sanitized;
            if (!s_buffersReady || s_pending || s_vault == null)
                return;

            WriteConditionsRow(in sanitized);
        }

#if UNITY_EDITOR
        public static bool TryLoadProfilesFromCsv(ReadOnlySpan<byte> csvBytes, out int profileCount)
        {
            profileCount = 0;
            if (!TryEnsure(SolarPowerGenerationConstants.DefaultPanelCapacity, SolarPowerGenerationConstants.DefaultPowerNodeCapacity))
                return false;

            IDataVault vault = s_vault;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in s_handles.Profiles, OwnerSystem, out NativeArray<SolarProfileDTO> profiles))
            {
                return false;
            }

            try
            {
                if (!profiles.IsCreated)
                    return false;

                return SolarPanelProfileCsvParser.TryParseProfiles(csvBytes, profiles, out profileCount);
            }
            finally
            {
                vault.ReleaseWriteLock(in s_handles.Profiles, OwnerSystem);
            }
        }
#endif

        private static bool ResolveCoreBuffers(
            out NativeArray<SolarPanelStateDTO> states,
            out NativeArray<SolarPanelOpticalOutputDTO> outputs,
            out NativeArray<int> nodeIndices,
            out NativeArray<SolarNodeInputCounter64> nodeSolarInput,
            out NativeArray<SolarConditionsDTO> conditions,
            out NativeArray<SolarTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor)
        {
            states = default;
            outputs = default;
            nodeIndices = default;
            nodeSolarInput = default;
            conditions = default;
            telemetry = default;
            telemetryCursor = default;

            return s_vault != null &&
                   s_vault.TryResolveHandle(in s_handles.PanelStates, out states) &&
                   s_vault.TryResolveHandle(in s_handles.PanelOutputs, out outputs) &&
                   s_vault.TryResolveHandle(in s_handles.PanelPowerNodeIndices, out nodeIndices) &&
                   s_vault.TryResolveHandle(in s_handles.NodeSolarInputMilliWatts, out nodeSolarInput) &&
                   s_vault.TryResolveHandle(in s_handles.Conditions, out conditions) &&
                   s_vault.TryResolveHandle(in s_handles.TelemetryRing, out telemetry) &&
                   s_vault.TryResolveHandle(in s_handles.TelemetryCursor, out telemetryCursor) &&
                   states.IsCreated &&
                   outputs.IsCreated &&
                   nodeIndices.IsCreated &&
                   nodeSolarInput.IsCreated &&
                   conditions.IsCreated &&
                   telemetry.IsCreated &&
                   telemetryCursor.IsCreated;
        }

        private static bool TryResolveExistingPowerNodes(out NativeArray<PowerNodeDTO> powerNodes)
        {
            powerNodes = default;
            IDataVault vault = s_vault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<PowerNodeDTO>(PowerGridBufferIds.Nodes, out VaultGenerationHandle<PowerNodeDTO> handle) ||
                handle.BufferID != unchecked((uint)(int)PowerGridBufferIds.Nodes) ||
                !vault.TryResolveHandle(in handle, out powerNodes) ||
                !powerNodes.IsCreated ||
                powerNodes.Length <= 0)
            {
                return false;
            }

            return true;
        }

        private static void RefreshBorrowedCelestialHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            if (!vault.TryGetGenerationHandle<CelestialStateDTO>(BufferID.Shinobu345CelestialStateRead, out s_celestialStateReadHandle) ||
                s_celestialStateReadHandle.BufferID != CelestialReadBufferId)
            {
                s_celestialStateReadHandle = default;
            }

            if (!vault.TryGetGenerationHandle<EnvironmentStateDTO>(BufferID.Shinobu345EnvironmentState, out s_environmentStateHandle) ||
                s_environmentStateHandle.BufferID != EnvironmentStateBufferId)
            {
                s_environmentStateHandle = default;
            }
        }

        private static bool TryAcquireVoxelSdfPayload(ref SolarConditionsDTO conditions, out NativeArray<byte>.ReadOnly voxelSdf)
        {
            voxelSdf = default;
            IDataVault vault = s_jobMutationGuardVault ?? s_vault;
            if (vault == null || !s_jobMutationGuardHeld)
                return false;

            if (!vault.TryGetGenerationHandle<VoxelSdfPayloadDescriptorDTO>(BufferID.VoxelSdfPayloadDescriptor, out VaultGenerationHandle<VoxelSdfPayloadDescriptorDTO> descriptorHandle) ||
                descriptorHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfPayloadDescriptor) ||
                !vault.TryReadOnlyHandle(in descriptorHandle, out NativeArray<VoxelSdfPayloadDescriptorDTO>.ReadOnly descriptors) ||
                descriptors.Length <= 0)
            {
                return false;
            }

            VoxelSdfPayloadDescriptorDTO descriptor = descriptors[0];
            int3 dimensions = descriptor.GridDimensions;
            long expected = (long)dimensions.x * dimensions.y * dimensions.z;
            if (expected <= 0L ||
                expected > int.MaxValue ||
                descriptor.ByteCount != (int)expected ||
                descriptor.BufferId != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                (descriptor.Flags & VoxelSdfPayloadDescriptorDTO.FlagValid) == 0u)
            {
                return false;
            }

            if (!vault.TryGetGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, out VaultGenerationHandle<byte> sdfHandle) ||
                sdfHandle.BufferID != unchecked((uint)(int)BufferID.VoxelSdfTexture3D) ||
                sdfHandle.Generation == 0u ||
                sdfHandle.Generation != descriptor.BufferGeneration ||
                !vault.TryReadOnlyHandle(in sdfHandle, out voxelSdf) ||
                voxelSdf.Length < expected)
            {
                voxelSdf = default;
                return false;
            }

            conditions.VoxelSdfOriginAUP = conditions.RuntimeOriginAUP + new double3(descriptor.VolumeOrigin.x, descriptor.VolumeOrigin.y, descriptor.VolumeOrigin.z);
            conditions.VoxelSdfDimensions = dimensions;
            conditions.VoxelSdfCellSize = math.max(descriptor.VoxelCellSize, new float3(0.0001f));
            conditions.VoxelSdfRangeMeters = math.max(0.0001f, math.isfinite(descriptor.SdfRangeMeters) ? descriptor.SdfRangeMeters : SolarPowerGenerationConstants.DefaultSdfRangeMeters);
            return true;
        }

        private static bool TryAcquireJobMutationGuard()
        {
            IDataVault vault = s_vault;
            if (vault == null ||
                s_jobMutationGuardHeld ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(JobMutationGuardMask))
            {
                return false;
            }

            s_jobMutationGuardVault = vault;
            s_jobMutationGuardHeld = true;
            return true;
        }

        private static bool HasPreparedBuffers(int panelCapacity, int powerNodeCapacity)
        {
            int safePanels = math.clamp(panelCapacity, 1, SolarPowerGenerationConstants.DefaultPanelCapacity);
            int safeNodes = math.max(1, powerNodeCapacity);
            return s_buffersReady &&
                   s_vault != null &&
                   safePanels <= s_panelCapacity &&
                   safeNodes <= s_powerNodeCapacity;
        }

        private static void UnlockJobBuffers()
        {
            if (!s_jobMutationGuardHeld)
            {
                s_jobMutationGuardVault = null;
                return;
            }

            IDataVault vault = s_jobMutationGuardVault;
            s_jobMutationGuardVault = null;
            s_jobMutationGuardHeld = false;
            vault?.ReleaseMutationGuard(JobMutationGuardMask);
        }

        private static void ForceCompletePendingJobInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref s_pendingHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong SolarBufferGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private static SolarConditionsDTO DefaultConditions()
        {
            SolarConditionsDTO conditions = default;
            conditions.RuntimeOriginAUP = double3.zero;
            conditions.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(conditions.RuntimeOriginAUP, SolarPowerGenerationConstants.DefaultSeaLevelY);
            conditions.SunDirection = new float3(0f, 1f, 0f);
            conditions.WaterAttenuationCoefficient = SolarPowerGenerationConstants.DefaultWaterAttenuationCoefficient;
            conditions.WaterTurbidity = 1f;
            conditions.TurbidityMultiplier = SolarPowerGenerationConstants.DefaultTurbidityMultiplier;
            conditions.InitialIntensityWatts = SolarPowerGenerationConstants.DefaultSolarIrradianceWatts;
            conditions.GlobalQualityWeight = ResolveSolarQualityWeight();
            conditions.VoxelSdfCellSize = new float3(1f);
            conditions.VoxelSdfRangeMeters = SolarPowerGenerationConstants.DefaultSdfRangeMeters;
            return conditions;
        }

        private static float ResolveSolarQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            return MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, 1f);
        }

        private static SolarConditionsDTO SanitizeConditions(in SolarConditionsDTO source)
        {
            SolarConditionsDTO result = source;
            if (!math.all(math.isfinite(result.SunDirection)) || math.lengthsq(result.SunDirection) <= 0.000001f)
                result.SunDirection = new float3(0f, 1f, 0f);
            else
                result.SunDirection = math.normalize(result.SunDirection);
            if (!math.all(math.isfinite(result.RuntimeOriginAUP)))
                result.RuntimeOriginAUP = default;
            double seaLevelDeltaMeters = result.SeaLevelAUP.y - result.RuntimeOriginAUP.y;
            bool seaLevelAupDefault = math.lengthsq(result.SeaLevelAUP) <= 0.000001d;
            if (!math.all(math.isfinite(result.SeaLevelAUP)) ||
                seaLevelAupDefault ||
                !math.isfinite(seaLevelDeltaMeters) ||
                math.abs(seaLevelDeltaMeters) <= 0.0001d ||
                math.abs(seaLevelDeltaMeters) > 1000d)
            {
                result.SeaLevelAUP = SolarPowerGenerationConstants.BuildSeaLevelAUP(result.RuntimeOriginAUP, (float)seaLevelDeltaMeters);
            }
            if (!math.all(math.isfinite(result.VoxelSdfOriginAUP)))
                result.VoxelSdfOriginAUP = result.RuntimeOriginAUP;
            result.WaterAttenuationCoefficient = math.max(0.000001f, math.isfinite(result.WaterAttenuationCoefficient) ? result.WaterAttenuationCoefficient : SolarPowerGenerationConstants.DefaultWaterAttenuationCoefficient);
            result.WaterTurbidity = math.max(0f, math.isfinite(result.WaterTurbidity) ? result.WaterTurbidity : 1f);
            result.TurbidityMultiplier = math.max(0f, math.isfinite(result.TurbidityMultiplier) ? result.TurbidityMultiplier : SolarPowerGenerationConstants.DefaultTurbidityMultiplier);
            result.InitialIntensityWatts = math.max(0f, math.isfinite(result.InitialIntensityWatts) ? result.InitialIntensityWatts : SolarPowerGenerationConstants.DefaultSolarIrradianceWatts);
            result.GlobalQualityWeight = MathLodApproximation.SaturateFinite(result.GlobalQualityWeight, ResolveSolarQualityWeight());
            result.SimulationTimeSeconds = math.isfinite(result.SimulationTimeSeconds) ? result.SimulationTimeSeconds : 0f;
            result.DeltaTimeSeconds = math.max(0f, math.isfinite(result.DeltaTimeSeconds) ? result.DeltaTimeSeconds : 0f);
            result.BaseEfficiencyScalar = math.max(0f, math.isfinite(result.BaseEfficiencyScalar) ? result.BaseEfficiencyScalar : 0f);
            result.VoxelSdfCellSize = math.all(math.isfinite(result.VoxelSdfCellSize))
                ? math.max(result.VoxelSdfCellSize, new float3(0.0001f))
                : new float3(1f);
            result.VoxelSdfRangeMeters = math.max(0.0001f, math.isfinite(result.VoxelSdfRangeMeters) ? result.VoxelSdfRangeMeters : SolarPowerGenerationConstants.DefaultSdfRangeMeters);
            return result;
        }

        private static IDataVault BindDataVaultCold()
        {
            IDataVault vault = s_vault;
            if (vault != null)
                return vault;

            vault = GlobalRegistry.DataVault;
            if (vault != null)
                s_vault = vault;

            return vault;
        }

        private static bool WriteConditionsRow(in SolarConditionsDTO conditions)
        {
            IDataVault vault = s_vault;
            if (!s_buffersReady ||
                s_pending ||
                vault == null ||
                !vault.TryAcquireWriteLock(in s_handles.Conditions, OwnerSystem, out NativeArray<SolarConditionsDTO> rows))
            {
                return false;
            }

            try
            {
                if (!rows.IsCreated || rows.Length <= 0)
                    return false;

                SolarConditionsDTO* ptr = (SolarConditionsDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(rows);
                UnsafeUtility.AsRef<SolarConditionsDTO>(ptr) = conditions;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_handles.Conditions, OwnerSystem);
            }
        }

        private static uint EstimateMicroseconds(int panelCount, int nodeCount, int sdfSampleCount, float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            float estimate = 8f + panelCount * math.lerp(0.12f, 0.38f, q) + nodeCount * 0.02f + sdfSampleCount * math.lerp(0.03f, 0.11f, q);
            return (uint)math.clamp(math.round(estimate), 0f, 1000000f);
        }

        private static bool TryNormalizeDouble3(double3 value, out float3 normalized)
        {
            normalized = default;
            if (!math.all(math.isfinite(value)))
                return false;

            double lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.000000000001d)
                return false;

            double invLength = math.rsqrt(lengthSq);
            normalized = new float3(
                (float)(value.x * invLength),
                (float)(value.y * invLength),
                (float)(value.z * invLength));
            return math.all(math.isfinite(normalized)) && math.lengthsq(normalized) > 0.000001f;
        }

        private static uint ResolveElapsedMicroseconds(long startTimestamp)
        {
            if (startTimestamp <= 0L)
                return 0u;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks < 0L)
                elapsedTicks = 0L;

            long frequency = Stopwatch.Frequency > 0L ? Stopwatch.Frequency : 1L;
            long microseconds = (elapsedTicks * 1000000L) / frequency;
            return microseconds >= uint.MaxValue ? uint.MaxValue : (uint)microseconds;
        }

        private static void StampSolverWallTime(uint elapsedMicroseconds)
        {
            if (!s_buffersReady ||
                s_vault == null ||
                !s_vault.TryResolveHandle(in s_handles.TelemetryRing, out NativeArray<SolarTelemetryEntry> telemetry) ||
                !s_vault.TryResolveHandle(in s_handles.TelemetryCursor, out NativeArray<int> cursor) ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                cursor.Length <= 0 ||
                telemetry.Length <= 0)
            {
                return;
            }

            int capacity = math.min(telemetry.Length, SolarPowerGenerationConstants.TelemetryFrameCount);
            int index = (cursor[0] - 1) % capacity;
            if (index < 0)
                index += capacity;
            SolarTelemetryEntry entry = telemetry[index];
            entry.SolverMicroseconds = elapsedMicroseconds;
            if (elapsedMicroseconds > SolarPowerGenerationConstants.SolverBudgetMicroseconds)
                entry.ReasonFlags |= SolarPowerGenerationConstants.FlagSolverOverBudget;
            telemetry[index] = entry;
        }

        private static SolarTelemetryEntry ReadLatestTelemetry()
        {
            if (!s_buffersReady ||
                s_vault == null ||
                !s_vault.TryReadOnlyHandle(in s_handles.TelemetryRing, out NativeArray<SolarTelemetryEntry>.ReadOnly telemetry) ||
                !s_vault.TryReadOnlyHandle(in s_handles.TelemetryCursor, out NativeArray<int>.ReadOnly cursor) ||
                cursor.Length <= 0 ||
                telemetry.Length <= 0)
            {
                return default;
            }

            int capacity = math.min(telemetry.Length, SolarPowerGenerationConstants.TelemetryFrameCount);
            int index = (cursor[0] - 1) % capacity;
            if (index < 0)
                index += capacity;
            return telemetry[index];
        }

        private static void DumpBlackBoxOnce(uint reasonFlags)
        {
            if (s_blackBoxDumped ||
                s_vault == null ||
                !s_vault.TryReadOnlyHandle(in s_handles.TelemetryRing, out NativeArray<SolarTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated)
            {
                return;
            }

            NativeArray<byte> payload = default;
            try
            {
                int count = math.min(telemetry.Length, SolarPowerGenerationConstants.TelemetryFrameCount);
                int headerBytes = UnsafeUtility.SizeOf<SolarBlackBoxDumpHeaderDTO>();
                int entryBytes = UnsafeUtility.SizeOf<SolarTelemetryEntry>();
                int byteCount = headerBytes + count * entryBytes;
                SolarBlackBoxDumpHeaderDTO header = default;
                header.Magic = SolarPowerGenerationConstants.DumpMagic;
                header.Version = SolarPowerGenerationConstants.DumpVersion;
                header.ReasonFlags = reasonFlags;
                header.EntryCount = (uint)count;
                header.EntryStrideBytes = (uint)entryBytes;
                header.FrameIndex = s_frameIndex;

                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(SolarPowerGenerationRuntime),
                    "solarPowerBlackBoxPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(target, UnsafeUtility.AddressOf(ref header), headerBytes);
                void* telemetryPtr = telemetry.GetUnsafeReadOnlyPtr();
                UnsafeUtility.MemCpy(target + headerBytes, telemetryPtr, count * entryBytes);
                s_blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(
                    SolarPowerGenerationConstants.DumpRelativePath,
                    payload,
                    byteCount);
            }
            catch (Exception)
            {
                // Crash dump is diagnostic-only; runtime authority remains in Vault telemetry.
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(SolarPowerGenerationRuntime),
                    "solarPowerBlackBoxPayload");
            }
        }
    }

    #if UNITY_EDITOR
    public static class SolarPanelProfileCsvParser
    {
        public static bool TryParseProfiles(ReadOnlySpan<byte> csvBytes, NativeArray<SolarProfileDTO> profiles, out int profileCount)
        {
            profileCount = 0;
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int lineStart = 0;
            while (lineStart < csvBytes.Length && profileCount < profiles.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, lineEnd - lineStart);
                if (TryParseLine(line, out SolarProfileDTO profile))
                    profiles[profileCount++] = profile;

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return true;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out SolarProfileDTO profile)
        {
            profile = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> name = NextField(ref line);
            if (name.Length == 0 || IsHeader(name))
                return false;

            profile.ProfileHash = Fnva32(name);
            profile.BaseEfficiencyScalar = math.max(0f, ParseFloat(NextField(ref line)));
            profile.PanelAreaSquareMeters = math.max(0f, ParseFloat(NextField(ref line)));
            profile.MaxOutputWatts = math.max(0f, ParseFloat(NextField(ref line)));
            profile.HeatLossScalar = math.max(0f, ParseFloat(NextField(ref line)));
            profile.Flags = profile.MaxOutputWatts > 0f ? 1u : 0u;
            return true;
        }

        private static ReadOnlySpan<byte> NextField(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> last = Trim(line);
                line = ReadOnlySpan<byte>.Empty;
                return last;
            }

            ReadOnlySpan<byte> field = Trim(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            return field;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsSpace(value[start]))
                start++;
            while (end >= start && IsSpace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsHeader(ReadOnlySpan<byte> value)
        {
            return value.Length >= 4 &&
                   ToLower(value[0]) == (byte)'n' &&
                   ToLower(value[1]) == (byte)'a' &&
                   ToLower(value[2]) == (byte)'m' &&
                   ToLower(value[3]) == (byte)'e';
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static uint Fnva32(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ ToLower(bytes[i])) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static float ParseFloat(ReadOnlySpan<byte> value)
        {
            value = Trim(value);
            if (value.Length == 0)
                return 0f;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                result = (result * 10f) + (value[index] - (byte)'0');
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    result += (value[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                }
            }

            return math.isfinite(result) ? result * sign : 0f;
        }
    }
    #endif
}
