using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [Flags]
    public enum ConstructionValidationFlags : uint
    {
        None = 0u,
        OccupiedGridCell = 1u << 0,
        TerrainIntersection = 1u << 1,
        PortMismatch = 1u << 2,
        StructuralWarning = 1u << 3,
        NonFiniteInput = 1u << 4,
        OutsideBounds = 1u << 5,
        GraphCapacity = 1u << 6,
        DisconnectedWing = 1u << 7
    }

    public static class ConstructionPortMask
    {
        public const uint None = 0u;
        public const uint PosX = 1u << 0;
        public const uint NegX = 1u << 1;
        public const uint PosY = 1u << 2;
        public const uint NegY = 1u << 3;
        public const uint PosZ = 1u << 4;
        public const uint NegZ = 1u << 5;
        public const uint AllCardinal = PosX | NegX | PosZ | NegZ;
        public const uint All = PosX | NegX | PosY | NegY | PosZ | NegZ;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ConstructionRequestDTO
    {
        [FieldOffset(0)] public double3 RootAUP;
        [FieldOffset(24)] public int3 GridPos;
        [FieldOffset(36)] public uint ModuleHash;
        [FieldOffset(40)] public uint Rotation;
        [FieldOffset(44)] public uint _pad0;
        [FieldOffset(48)] public uint _pad1;
        [FieldOffset(52)] public uint _pad2;
        [FieldOffset(56)] public uint _pad3;
        [FieldOffset(60)] public uint _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StructuralBoundsDTO
    {
        [FieldOffset(0)] public float3 CenterOffset;
        [FieldOffset(12)] public float ClearanceRadius;
        [FieldOffset(16)] public float3 Extents;
        [FieldOffset(28)] public uint BoundsHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ConstructionValidationSettingsDTO
    {
        [FieldOffset(0)] public float GridSizeMeters;
        [FieldOffset(4)] public float TerrainClearanceMargin;
        [FieldOffset(8)] public float GlobalQualityWeight;
        [FieldOffset(12)] public float MaxBaseBoundsMeters;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint CandidatePortMask;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BaseModuleOccupancyDTO
    {
        [FieldOffset(0)] public int3 GridPos;
        [FieldOffset(12)] public uint ModuleHash;
        [FieldOffset(16)] public uint PortMask;
        [FieldOffset(20)] public int NodeIndex;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ConstructionSipBudgetDTO
    {
        [FieldOffset(0)] public float TotalBaseSIP;
        [FieldOffset(4)] public float AddedSIPCost;
        [FieldOffset(8)] public float DepthPressure;
        [FieldOffset(12)] public float StructuralWarningRatio;
        [FieldOffset(16)] public uint BaseHash;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ConstructionValidationResultDTO
    {
        [FieldOffset(0)] public uint FailureFlags;
        [FieldOffset(4)] public float MinSdfDistance;
        [FieldOffset(8)] public int OccupiedCellHash;
        [FieldOffset(12)] public int ProbeCount;
        [FieldOffset(16)] public float ProjectedTotalSIP;
        [FieldOffset(20)] public float PressureRatio;
        [FieldOffset(24)] public byte IsValid;
        [FieldOffset(25)] public byte StructuralWarning;
        [FieldOffset(26)] public ushort _pad0;
        [FieldOffset(28)] public uint ResultHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockWorldSampler
    {
        [FieldOffset(0)] public double3 RootAUP;
        [FieldOffset(24)] public float PlaneLocalY;
        [FieldOffset(28)] public float RidgeAmplitudeMeters;
        [FieldOffset(32)] public float RidgeFrequency;
        [FieldOffset(36)] public uint Seed;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float HardBlockRadiusMeters;
        [FieldOffset(48)] public float3 HardBlockCenterLocal;
        [FieldOffset(60)] public uint _pad0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float SampleDistance(float3 localPosition)
        {
            if (!math.all(math.isfinite(localPosition)))
                return -1f;

            float ridge = RidgeAmplitudeMeters *
                          math.sin((localPosition.x + localPosition.z + (Seed & 1023u)) * math.max(RidgeFrequency, 0.0001f));
            float terrainDistance = localPosition.y - (PlaneLocalY + ridge);

            if (HardBlockRadiusMeters > 0.0001f)
            {
                float obstacleDistance = math.length(localPosition - HardBlockCenterLocal) - HardBlockRadiusMeters;
                terrainDistance = math.min(terrainDistance, obstacleDistance);
            }

            return terrainDistance;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ConstructionTelemetryEntry
    {
        [FieldOffset(0)] public double3 RootAUP;
        [FieldOffset(24)] public int3 GridPos;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint FailureFlags;
        [FieldOffset(44)] public float MinSdfDistance;
        [FieldOffset(48)] public float ValidationComputeTimeMs;
        [FieldOffset(52)] public uint BuildRequestsValidated;
        [FieldOffset(56)] public uint GraphSplices;
        [FieldOffset(60)] public uint ResultHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ConstructionGraphSignalDTO
    {
        [FieldOffset(0)] public int NodeIndex;
        [FieldOffset(4)] public int ParentNodeIndex;
        [FieldOffset(8)] public int2 Edge;
        [FieldOffset(16)] public uint ModuleHash;
        [FieldOffset(20)] public uint PortMask;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    public static unsafe class ModularBaseConstructionValidator
    {
        public const int TelemetryCapacity = 300;
        public const int BoundsOverrideCapacity = 512;
        public const int OccupancyHashTableCapacity = 4096;
        public const int EmergencyMockBoundsCount = 16;
        public const string DefaultDumpPath = @"C:\hades\Hecton8\Docs\AgentLogs\Dump_SHINOBU_228_ConstructionValidation.bin";
        public const uint OccupancyFlagOccupied = 1u;
        public const int ConstructionRequestSizeBytes = 64;
        public const int StructuralBoundsSizeBytes = 32;
        public const int ConstructionValidationSettingsSizeBytes = 32;
        public const int ConstructionValidationResultSizeBytes = 32;
        public const int BaseModuleOccupancySizeBytes = 32;
        public const int TelemetryEntrySizeBytes = 64;
        public const int ConstructionPreviewSignalSizeBytes = 128;
        public const int FloraExclusionSignalSizeBytes = 128;
        public const int BuilderGhostStateSizeBytes = 128;
        public const int BuilderGhostVisualSizeBytes = 64;
        public const int HolographyTelemetrySizeBytes = 64;
        public const int BuilderGhostIndirectArgsSizeBytes = 16;
        public const int TerrainProbeTruthCount = 9;

        private const float DefaultGridSizeMeters = 10f;
        private const float DefaultClearanceMarginMeters = 0.05f;
        private const float DefaultMaxBaseBoundsMeters = 250f;
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private static ConstructionValidationSettingsDTO s_TunerSettings = CreateDefaultSettings(1f);
        private static ConstructionValidationResultDTO s_LastValidationResult;
        private static ConstructionRequestDTO s_LastRequest;
        private static StructuralBoundsDTO s_LastBounds;
        private static MockWorldSampler s_LastSampler;
        private static bool s_TelemetryDumped;
        private static VaultGenerationHandle<ConstructionValidationSettingsDTO> s_TuningHandle;
        private static VaultGenerationHandle<ConstructionTelemetryEntry> s_TelemetryHandle;
        private static VaultGenerationHandle<StructuralBoundsDTO> s_BoundsOverrideHandle;
        private static VaultGenerationHandle<BaseModuleOccupancyDTO> s_OccupancyHandle;

        public static bool ValidateStructLayout()
        {
            return UnsafeUtility.SizeOf<ConstructionRequestDTO>() == ConstructionRequestSizeBytes &&
                   UnsafeUtility.SizeOf<StructuralBoundsDTO>() == StructuralBoundsSizeBytes &&
                   UnsafeUtility.SizeOf<ConstructionValidationSettingsDTO>() == ConstructionValidationSettingsSizeBytes &&
                   UnsafeUtility.SizeOf<ConstructionValidationResultDTO>() == ConstructionValidationResultSizeBytes &&
                   UnsafeUtility.SizeOf<BaseModuleOccupancyDTO>() == BaseModuleOccupancySizeBytes &&
                   UnsafeUtility.SizeOf<ConstructionTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<ConstructionPreviewSignal>() == ConstructionPreviewSignalSizeBytes &&
                   UnsafeUtility.SizeOf<FloraExclusionSignal>() == FloraExclusionSignalSizeBytes &&
                   UnsafeUtility.SizeOf<BuilderGhostStateDTO>() == BuilderGhostStateSizeBytes &&
                   UnsafeUtility.SizeOf<BuilderGhostVisualDTO>() == BuilderGhostVisualSizeBytes &&
                   UnsafeUtility.SizeOf<HolographyTelemetryEntry>() == HolographyTelemetrySizeBytes &&
                   UnsafeUtility.SizeOf<BuilderGhostIndirectArgsDTO>() == BuilderGhostIndirectArgsSizeBytes &&
                   ResolveOffset<ConstructionPreviewSignal>(nameof(ConstructionPreviewSignal.DearLieDampen)) == 96 &&
                   ResolveOffset<ConstructionPreviewSignal>(nameof(ConstructionPreviewSignal.GlobalQualityWeight)) == 100 &&
                   ResolveOffset<ConstructionPreviewSignal>(nameof(ConstructionPreviewSignal.DearLieWiggleSpeed)) == 104;
        }

        private static int ResolveOffset<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

        public static void InitializeVault(IDataVault vault)
        {
            if (vault == null)
                return;

            if (TryReadTunerSettingsFromVault(vault, out ConstructionValidationSettingsDTO tunerSettings))
                s_TunerSettings = tunerSettings;
            else
                WriteTunerSettingsToVault(vault);
            EnsureTelemetryRing(vault, out _);
            EnsureOccupancyHashTable(vault, out _);
            if (EnsureBoundsOverrideBuffer(vault, out NativeArray<StructuralBoundsDTO> boundsBuffer) &&
                boundsBuffer.Length > 0 &&
                boundsBuffer[0].BoundsHash == 0u)
            {
                GenerateEmergencyMockBounds(boundsBuffer, EmergencyMockBoundsCount, s_TunerSettings.GridSizeMeters);
            }
        }

        public static ConstructionValidationSettingsDTO CreateDefaultSettings(float globalQualityWeight)
        {
            ConstructionValidationSettingsDTO settings;
            settings.GridSizeMeters = DefaultGridSizeMeters;
            settings.TerrainClearanceMargin = DefaultClearanceMarginMeters;
            settings.GlobalQualityWeight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            settings.MaxBaseBoundsMeters = DefaultMaxBaseBoundsMeters;
            settings.Frame = 0u;
            settings.CandidatePortMask = ConstructionPortMask.AllCardinal;
            settings.Flags = 0u;
            settings._pad0 = 0u;
            return settings;
        }

        public static ConstructionValidationSettingsDTO GetTunerSettings()
        {
            return s_TunerSettings;
        }

        public static bool TryReadTunerSettingsFromVault(IDataVault vault, out ConstructionValidationSettingsDTO settings)
        {
            settings = default;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ConstructionValidationSettingsDTO>(BufferID.ConstructionBuilderTuning, out VaultGenerationHandle<ConstructionValidationSettingsDTO> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<ConstructionValidationSettingsDTO> buffer) ||
                !buffer.IsCreated ||
                buffer.Length <= 0)
                return false;

            ConstructionValidationSettingsDTO candidate = buffer[0];
            if (!math.isfinite(candidate.GridSizeMeters) ||
                !math.isfinite(candidate.TerrainClearanceMargin) ||
                !math.isfinite(candidate.GlobalQualityWeight) ||
                !math.isfinite(candidate.MaxBaseBoundsMeters))
                return false;

            settings = candidate;
            return true;
        }

        public static void WriteTunerSettingsToVault(IDataVault vault)
        {
            if (vault == null)
                return;

            if (!EnsureValidationBuffer(
                    vault,
                    BufferID.ConstructionBuilderTuning,
                    1,
                    NativeArrayOptions.UninitializedMemory,
                    ref s_TuningHandle,
                    out NativeArray<ConstructionValidationSettingsDTO> buffer))
                return;

            if (buffer.IsCreated && buffer.Length > 0)
                buffer[0] = s_TunerSettings;
        }

        public static void SetTunerSettings(float gridSnapSize, float maxBaseBounds, float terrainClearanceMargin)
        {
            s_TunerSettings.GridSizeMeters = SanitizePositive(gridSnapSize, DefaultGridSizeMeters);
            s_TunerSettings.MaxBaseBoundsMeters = math.clamp(
                math.isfinite(maxBaseBounds) ? maxBaseBounds : DefaultMaxBaseBoundsMeters,
                s_TunerSettings.GridSizeMeters,
                5000f);
            s_TunerSettings.TerrainClearanceMargin = math.clamp(
                math.isfinite(terrainClearanceMargin) ? terrainClearanceMargin : DefaultClearanceMarginMeters,
                0f,
                2f);
        }

        public static float ResolveGlobalQualityWeight()
        {
            float qualityWeight = Hecton8.Core.HomeostasisBrain.GlobalQualityWeight;
            if (!math.isfinite(qualityWeight))
                qualityWeight = 1f;

            return math.saturate(qualityWeight);
        }

        public static MockWorldSampler CreateMockWorldSampler(double3 rootAup, float planeLocalY, uint seed)
        {
            MockWorldSampler sampler;
            sampler.RootAUP = rootAup;
            sampler.PlaneLocalY = math.isfinite(planeLocalY) ? planeLocalY : 0f;
            sampler.RidgeAmplitudeMeters = 0f;
            sampler.RidgeFrequency = 0.03125f;
            sampler.Seed = seed;
            sampler.Flags = 0u;
            sampler.HardBlockRadiusMeters = 0f;
            sampler.HardBlockCenterLocal = float3.zero;
            sampler._pad0 = 0u;
            return sampler;
        }

        public static bool TryBuildRequestFromAup(
            double3 rootAup,
            double3 targetAup,
            uint moduleHash,
            uint rotation,
            float gridSizeMeters,
            out ConstructionRequestDTO request)
        {
            request = default;
            if (!IsFinite(rootAup) || !IsFinite(targetAup))
                return false;

            float gridSize = SanitizePositive(gridSizeMeters, DefaultGridSizeMeters);
            double3 localDouble = targetAup - rootAup;
            if (!IsSafeLocal(localDouble))
                return false;

            float3 local = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
            int3 grid = (int3)math.round(local / gridSize);
            request.RootAUP = rootAup;
            request.GridPos = grid;
            request.ModuleHash = moduleHash;
            request.Rotation = rotation & 3u;
            request._pad0 = 0u;
            request._pad1 = 0u;
            request._pad2 = 0u;
            request._pad3 = 0u;
            request._pad4 = 0u;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GridToLocal(in ConstructionRequestDTO request, float gridSizeMeters)
        {
            return (float3)request.GridPos * SanitizePositive(gridSizeMeters, DefaultGridSizeMeters);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveTerrainProbeCount()
        {
            return TerrainProbeTruthCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveTerrainProbeLocal(
            int probeIndex,
            in ConstructionRequestDTO request,
            in StructuralBoundsDTO bounds,
            in ConstructionValidationSettingsDTO settings)
        {
            float3 localCenter = GridToLocal(in request, settings.GridSizeMeters);
            return ResolveAabbProbe(probeIndex, localCenter, in bounds, request.Rotation);
        }

        public static StructuralBoundsDTO BuildBounds(float3 centerOffset, float3 size, uint boundsHash)
        {
            StructuralBoundsDTO bounds;
            bounds.CenterOffset = SanitizeFinite(centerOffset);
            bounds.ClearanceRadius = math.length(math.max(size, new float3(0.01f)) * 0.5f);
            bounds.Extents = math.max(SanitizeFinite(size) * 0.5f, new float3(0.005f));
            bounds.BoundsHash = boundsHash;
            return bounds;
        }

        public static int GenerateEmergencyMockBounds(
            NativeArray<StructuralBoundsDTO> boundsBuffer,
            int requestedCount,
            float gridSizeMeters)
        {
            if (!boundsBuffer.IsCreated || boundsBuffer.Length <= 0)
                return 0;

            int count = math.clamp(requestedCount, 1, math.min(boundsBuffer.Length, EmergencyMockBoundsCount));
            float grid = SanitizePositive(gridSizeMeters, DefaultGridSizeMeters);
            float3 size = new float3(grid * 0.92f, grid * 0.78f, grid * 0.92f);
            for (int i = 0; i < count; i++)
            {
                uint hash = FnvOffset ^ (uint)(i + 1) * FnvPrime;
                boundsBuffer[i] = BuildBounds(float3.zero, size, hash);
            }

            for (int i = count; i < boundsBuffer.Length; i++)
                boundsBuffer[i] = default;

            return count;
        }

        public static ConstructionValidationResultDTO ValidatePlacementNoOccupancy(
            in ConstructionRequestDTO request,
            in StructuralBoundsDTO bounds,
            in ConstructionValidationSettingsDTO settings,
            in MockWorldSampler sampler,
            in ConstructionSipBudgetDTO sipBudget)
        {
            ConstructionValidationResultDTO result;
            ValidatePlacementCore(
                in request,
                in bounds,
                in settings,
                in sampler,
                in sipBudget,
                false,
                default,
                out result);

            s_LastValidationResult = result;
            s_LastRequest = request;
            s_LastBounds = bounds;
            s_LastSampler = sampler;
            return result;
        }

        public static bool TryGetLastValidation(
            out ConstructionRequestDTO request,
            out StructuralBoundsDTO bounds,
            out MockWorldSampler sampler,
            out ConstructionValidationResultDTO result)
        {
            request = s_LastRequest;
            bounds = s_LastBounds;
            sampler = s_LastSampler;
            result = s_LastValidationResult;
            return result.ResultHash != 0u;
        }

        /// <summary>
        /// Applies post-validator failure evidence and refreshes validity plus telemetry hash in one place.
        /// </summary>
        public static void ApplyFailureFlags(
            ref ConstructionValidationResultDTO result,
            in ConstructionRequestDTO request,
            uint failureFlags,
            float minSdfDistance,
            int occupiedCellHash)
        {
            result.FailureFlags |= failureFlags;
            if (math.isfinite(minSdfDistance))
                result.MinSdfDistance = math.min(result.MinSdfDistance, minSdfDistance);
            if (occupiedCellHash != 0)
                result.OccupiedCellHash = occupiedCellHash;

            result.IsValid = (byte)((result.FailureFlags & ~((uint)ConstructionValidationFlags.StructuralWarning)) == 0u ? 1 : 0);
            result.StructuralWarning = (byte)((result.FailureFlags & (uint)ConstructionValidationFlags.StructuralWarning) != 0u ? 1 : 0);
            result.ResultHash = HashResult(in request, result.FailureFlags, result.MinSdfDistance, result.ProbeCount);
        }

        public static bool TryParseModuleBoundsCsv(
            ReadOnlySpan<byte> csv,
            NativeParallelHashMap<uint, StructuralBoundsDTO> boundsByHash)
        {
            if (!boundsByHash.IsCreated)
                return false;

            int lineStart = 0;
            bool parsedAny = false;
            for (int i = 0; i <= csv.Length; i++)
            {
                if (i < csv.Length && csv[i] != (byte)'\n')
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && csv[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                parsedAny |= TryParseBoundsLine(csv.Slice(lineStart, lineEnd - lineStart), boundsByHash);
                lineStart = i + 1;
            }

            return parsedAny;
        }

        public static bool TryParseModuleBoundsCsvToVault(
            ReadOnlySpan<byte> csv,
            IDataVault vault,
            out int writtenCount)
        {
            writtenCount = 0;
            if (!EnsureBoundsOverrideBuffer(vault, out NativeArray<StructuralBoundsDTO> boundsBuffer))
                return false;

            for (int i = 0; i < boundsBuffer.Length; i++)
                boundsBuffer[i] = default;

            int lineStart = 0;
            for (int i = 0; i <= csv.Length && writtenCount < boundsBuffer.Length; i++)
            {
                if (i < csv.Length && csv[i] != (byte)'\n')
                    continue;

                int lineEnd = i;
                if (lineEnd > lineStart && csv[lineEnd - 1] == (byte)'\r')
                    lineEnd--;

                if (TryParseBoundsLine(csv.Slice(lineStart, lineEnd - lineStart), out StructuralBoundsDTO bounds))
                    boundsBuffer[writtenCount++] = bounds;

                lineStart = i + 1;
            }

            return writtenCount > 0;
        }

        public static bool TryReadTelemetryRing(
            IDataVault vault,
            out NativeArray<ConstructionTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            if (vault == null ||
                !TryResolveCachedValidationBuffer(vault, in s_TelemetryHandle, 1, out telemetryRing))
                return false;

            return telemetryRing.IsCreated && telemetryRing.Length > 0;
        }

        public static bool EnsureTelemetryRing(
            IDataVault vault,
            out NativeArray<ConstructionTelemetryEntry> telemetryRing)
        {
            telemetryRing = default;
            if (vault == null)
                return false;

            if (!EnsureValidationBuffer(
                    vault,
                    BufferID.ConstructionBuilderTelemetry,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory,
                    ref s_TelemetryHandle,
                    out telemetryRing))
                return false;

            return telemetryRing.IsCreated && telemetryRing.Length > 0;
        }

        public static bool EnsureBoundsOverrideBuffer(
            IDataVault vault,
            out NativeArray<StructuralBoundsDTO> boundsBuffer)
        {
            boundsBuffer = default;
            if (vault == null)
                return false;

            if (!EnsureValidationBuffer(
                    vault,
                    BufferID.ConstructionBuilderBounds,
                    BoundsOverrideCapacity,
                    NativeArrayOptions.ClearMemory,
                    ref s_BoundsOverrideHandle,
                    out boundsBuffer))
                return false;

            return boundsBuffer.IsCreated && boundsBuffer.Length > 0;
        }

        public static bool TryReadOccupancyHashTable(
            IDataVault vault,
            out NativeArray<BaseModuleOccupancyDTO>.ReadOnly occupancyTable)
        {
            occupancyTable = default;
            if (vault == null ||
                !TryResolveCachedValidationBuffer(vault, in s_OccupancyHandle, 1, out NativeArray<BaseModuleOccupancyDTO> mutableOccupancyTable))
                return false;

            occupancyTable = mutableOccupancyTable.AsReadOnly();
            return mutableOccupancyTable.IsCreated && mutableOccupancyTable.Length > 0;
        }

        public static bool EnsureOccupancyHashTable(
            IDataVault vault,
            out NativeArray<BaseModuleOccupancyDTO> occupancyTable)
        {
            occupancyTable = default;
            if (vault == null)
                return false;

            if (!EnsureValidationBuffer(
                    vault,
                    BufferID.ConstructionBuilderOccupancy,
                    OccupancyHashTableCapacity,
                    NativeArrayOptions.ClearMemory,
                    ref s_OccupancyHandle,
                    out occupancyTable))
                return false;

            return occupancyTable.IsCreated && occupancyTable.Length > 0;
        }

        private static bool EnsureValidationBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryResolveCachedValidationBuffer(vault, in handle, requiredLength, out buffer))
                return true;

            if (vault == null)
                return false;

            handle = vault.GetGenerationHandle<T>(
                bufferId,
                math.max(1, requiredLength),
                SystemID.Construction,
                options);

            return TryResolveCachedValidationBuffer(vault, in handle, requiredLength, out buffer);
        }

        private static bool TryResolveCachedValidationBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= math.max(1, requiredLength);
        }

        public static bool TryInsertOccupancyCell(
            NativeArray<BaseModuleOccupancyDTO> occupancyTable,
            in BaseModuleOccupancyDTO entry,
            uint frame)
        {
            if (!occupancyTable.IsCreated || occupancyTable.Length <= 0)
                return false;

            int length = occupancyTable.Length;
            int frameStamp = unchecked((int)frame);
            int start = (int)((uint)HashGrid(entry.GridPos) % (uint)length);
            for (int probe = 0; probe < length; probe++)
            {
                int index = start + probe;
                if (index >= length)
                    index -= length;

                BaseModuleOccupancyDTO current = occupancyTable[index];
                bool stale = (current.Flags & OccupancyFlagOccupied) == 0u || current.NodeIndex != frameStamp;
                if (stale || math.all(current.GridPos == entry.GridPos))
                {
                    BaseModuleOccupancyDTO stored = entry;
                    stored.NodeIndex = frameStamp;
                    stored.Flags |= OccupancyFlagOccupied;
                    occupancyTable[index] = stored;
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindOccupiedCell(
            NativeArray<BaseModuleOccupancyDTO> occupancyTable,
            int3 gridPos,
            uint frame,
            out int occupiedCellHash)
        {
            occupiedCellHash = 0;
            if (!occupancyTable.IsCreated || occupancyTable.Length <= 0)
                return false;

            int length = occupancyTable.Length;
            int frameStamp = unchecked((int)frame);
            int start = (int)((uint)HashGrid(gridPos) % (uint)length);
            for (int probe = 0; probe < length; probe++)
            {
                int index = start + probe;
                if (index >= length)
                    index -= length;

                BaseModuleOccupancyDTO current = occupancyTable[index];
                if ((current.Flags & OccupancyFlagOccupied) == 0u || current.NodeIndex != frameStamp)
                    return false;

                if (!math.all(current.GridPos == gridPos))
                    continue;

                occupiedCellHash = HashGrid(gridPos);
                return true;
            }

            return false;
        }

        public static void WriteTelemetry(
            NativeArray<ConstructionTelemetryEntry> telemetryRing,
            uint frame,
            in ConstructionRequestDTO request,
            in ConstructionValidationResultDTO result,
            float validationComputeTimeMs,
            uint graphSplices)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return;

            int index = (int)(frame % (uint)math.min(telemetryRing.Length, TelemetryCapacity));
            ConstructionTelemetryEntry entry;
            entry.RootAUP = request.RootAUP;
            entry.GridPos = request.GridPos;
            entry.Frame = frame;
            entry.FailureFlags = result.FailureFlags;
            entry.MinSdfDistance = math.isfinite(result.MinSdfDistance) ? result.MinSdfDistance : -1f;
            entry.ValidationComputeTimeMs = math.isfinite(validationComputeTimeMs) ? validationComputeTimeMs : 0f;
            entry.BuildRequestsValidated = 1u;
            entry.GraphSplices = graphSplices;
            entry.ResultHash = result.ResultHash;
            telemetryRing[index] = entry;

            bool nonFinite =
                !IsFinite(request.RootAUP) ||
                !math.isfinite(result.MinSdfDistance) ||
                !math.isfinite(validationComputeTimeMs) ||
                (result.FailureFlags & (uint)ConstructionValidationFlags.NonFiniteInput) != 0u;
            if (nonFinite && !s_TelemetryDumped)
            {
                s_TelemetryDumped = true;
                DumpTelemetry(telemetryRing);
            }
        }

        public static void DumpTelemetry(NativeArray<ConstructionTelemetryEntry> telemetryRing, string absolutePath = DefaultDumpPath)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0 || string.IsNullOrWhiteSpace(absolutePath))
                return;

            void* ptr = telemetryRing.GetUnsafeReadOnlyPtr();
            int byteLength = telemetryRing.Length * UnsafeUtility.SizeOf<ConstructionTelemetryEntry>();

            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(new ReadOnlySpan<byte>(ptr, byteLength));
            }
        }

        internal static void ValidatePlacementCore(
            in ConstructionRequestDTO request,
            in StructuralBoundsDTO bounds,
            in ConstructionValidationSettingsDTO settings,
            in MockWorldSampler sampler,
            in ConstructionSipBudgetDTO sipBudget,
            bool hasOccupancy,
            NativeParallelMultiHashMap<int, BaseModuleOccupancyDTO> occupancy,
            out ConstructionValidationResultDTO result)
        {
            result = default;
            uint flags = 0u;
            int occupiedCellHash = 0;
            float minDistance = float.MaxValue;
            int probeCount = 0;

            if (!ValidateStructLayout() ||
                !IsFinite(request.RootAUP) ||
                !math.all(math.isfinite((float3)request.GridPos)) ||
                !math.all(math.isfinite(bounds.CenterOffset)) ||
                !math.all(math.isfinite(bounds.Extents)))
            {
                flags |= (uint)ConstructionValidationFlags.NonFiniteInput;
            }

            float gridSize = SanitizePositive(settings.GridSizeMeters, DefaultGridSizeMeters);
            float maxBounds = math.max(SanitizePositive(settings.MaxBaseBoundsMeters, DefaultMaxBaseBoundsMeters), gridSize);
            float3 localCenter = GridToLocal(in request, gridSize);
            if (math.any(math.abs(localCenter) > maxBounds))
                flags |= (uint)ConstructionValidationFlags.OutsideBounds;

            int cellHash = HashGrid(request.GridPos);
            if (hasOccupancy && occupancy.IsCreated)
            {
                NativeParallelMultiHashMapIterator<int> iterator;
                if (occupancy.TryGetFirstValue(cellHash, out BaseModuleOccupancyDTO _, out iterator))
                {
                    flags |= (uint)ConstructionValidationFlags.OccupiedGridCell;
                    occupiedCellHash = cellHash;
                }

                if (!HasAlignedNeighborPort(in request, in settings, occupancy))
                    flags |= (uint)ConstructionValidationFlags.PortMismatch;
            }

            float clearance = math.max(math.isfinite(settings.TerrainClearanceMargin) ? settings.TerrainClearanceMargin : DefaultClearanceMarginMeters, 0f);
            int probeBudget = TerrainProbeTruthCount;
            for (int i = 0; i < probeBudget; i++)
            {
                float3 probe = ResolveAabbProbe(i, localCenter, in bounds, request.Rotation);
                float distance = sampler.SampleDistance(probe);
                if (!math.isfinite(distance))
                {
                    flags |= (uint)ConstructionValidationFlags.NonFiniteInput;
                    distance = -1f;
                }

                minDistance = math.min(minDistance, distance);
                probeCount++;
                if (distance < clearance)
                    flags |= (uint)ConstructionValidationFlags.TerrainIntersection;
            }

            float projectedSip = SanitizeFinite(sipBudget.TotalBaseSIP, 0f) + SanitizeFinite(sipBudget.AddedSIPCost, 0f);
            float depthPressure = math.max(SanitizeFinite(sipBudget.DepthPressure, 0f), 0f);
            float pressureRatio = depthPressure * math.rcp(math.max(projectedSip, 0.0001f));
            float warningRatio = sipBudget.StructuralWarningRatio > 0.0001f ? sipBudget.StructuralWarningRatio : 1f;
            if (depthPressure > projectedSip * warningRatio)
                flags |= (uint)ConstructionValidationFlags.StructuralWarning;

            result.FailureFlags = flags;
            result.MinSdfDistance = minDistance == float.MaxValue ? 0f : minDistance;
            result.OccupiedCellHash = occupiedCellHash;
            result.ProbeCount = probeCount;
            result.ProjectedTotalSIP = projectedSip;
            result.PressureRatio = pressureRatio;
            result.IsValid = (byte)((flags & ~((uint)ConstructionValidationFlags.StructuralWarning)) == 0u ? 1 : 0);
            result.StructuralWarning = (byte)((flags & (uint)ConstructionValidationFlags.StructuralWarning) != 0u ? 1 : 0);
            result._pad0 = 0;
            result.ResultHash = HashResult(in request, flags, result.MinSdfDistance, probeCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int HashGrid(int3 grid)
        {
            unchecked
            {
                uint hash = FnvOffset;
                hash = (hash ^ (uint)grid.x) * FnvPrime;
                hash = (hash ^ (uint)grid.y) * FnvPrime;
                hash = (hash ^ (uint)grid.z) * FnvPrime;
                return (int)hash;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint OppositePort(uint port)
        {
            switch (port)
            {
                case ConstructionPortMask.PosX: return ConstructionPortMask.NegX;
                case ConstructionPortMask.NegX: return ConstructionPortMask.PosX;
                case ConstructionPortMask.PosY: return ConstructionPortMask.NegY;
                case ConstructionPortMask.NegY: return ConstructionPortMask.PosY;
                case ConstructionPortMask.PosZ: return ConstructionPortMask.NegZ;
                case ConstructionPortMask.NegZ: return ConstructionPortMask.PosZ;
                default: return ConstructionPortMask.None;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int3 DirectionForPort(uint port)
        {
            switch (port)
            {
                case ConstructionPortMask.PosX: return new int3(1, 0, 0);
                case ConstructionPortMask.NegX: return new int3(-1, 0, 0);
                case ConstructionPortMask.PosY: return new int3(0, 1, 0);
                case ConstructionPortMask.NegY: return new int3(0, -1, 0);
                case ConstructionPortMask.PosZ: return new int3(0, 0, 1);
                case ConstructionPortMask.NegZ: return new int3(0, 0, -1);
                default: return int3.zero;
            }
        }

        private static bool TryParseBoundsLine(
            ReadOnlySpan<byte> line,
            NativeParallelHashMap<uint, StructuralBoundsDTO> boundsByHash)
        {
            if (!TryParseBoundsLine(line, out StructuralBoundsDTO bounds))
                return false;

            if (boundsByHash.ContainsKey(bounds.BoundsHash))
                boundsByHash.Remove(bounds.BoundsHash);

            boundsByHash.TryAdd(bounds.BoundsHash, bounds);
            return true;
        }

        private static bool TryParseBoundsLine(
            ReadOnlySpan<byte> line,
            out StructuralBoundsDTO bounds)
        {
            bounds = default;
            if (line.Length <= 0 || line[0] == (byte)'#')
                return false;

            int cursor = 0;
            if (!TryParseHash(line, ref cursor, out uint moduleHash))
                return false;

            if (!TryParseFloat(line, ref cursor, out float cx) ||
                !TryParseFloat(line, ref cursor, out float cy) ||
                !TryParseFloat(line, ref cursor, out float cz) ||
                !TryParseFloat(line, ref cursor, out float sx) ||
                !TryParseFloat(line, ref cursor, out float sy) ||
                !TryParseFloat(line, ref cursor, out float sz))
                return false;

            bounds = BuildBounds(new float3(cx, cy, cz), new float3(sx, sy, sz), moduleHash);
            return true;
        }

        private static bool TryParseHash(ReadOnlySpan<byte> line, ref int cursor, out uint hash)
        {
            SkipWhitespace(line, ref cursor);
            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            if (cursor <= start)
            {
                hash = 0u;
                return false;
            }

            ReadOnlySpan<byte> token = line.Slice(start, cursor - start);
            hash = 0u;
            bool numeric = true;
            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b < (byte)'0' || b > (byte)'9')
                {
                    numeric = false;
                    break;
                }

                hash = hash * 10u + (uint)(b - (byte)'0');
            }

            if (!numeric)
            {
                hash = FnvOffset;
                for (int i = 0; i < token.Length; i++)
                    hash = (hash ^ token[i]) * FnvPrime;
            }

            cursor++;
            return hash != 0u;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            SkipWhitespace(line, ref cursor);
            if (cursor >= line.Length)
                return false;

            int sign = 1;
            if (line[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }

            double whole = 0d;
            bool any = false;
            while (cursor < line.Length && line[cursor] >= (byte)'0' && line[cursor] <= (byte)'9')
            {
                any = true;
                whole = (whole * 10d) + (line[cursor] - (byte)'0');
                cursor++;
            }

            double frac = 0d;
            double scale = 1d;
            if (cursor < line.Length && line[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < line.Length && line[cursor] >= (byte)'0' && line[cursor] <= (byte)'9')
                {
                    any = true;
                    frac = (frac * 10d) + (line[cursor] - (byte)'0');
                    scale *= 10d;
                    cursor++;
                }
            }

            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;

            value = (float)(sign * (whole + frac / scale));
            return any && math.isfinite(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SkipWhitespace(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && (line[cursor] == (byte)' ' || line[cursor] == (byte)'\t'))
                cursor++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool HasAlignedNeighborPort(
            in ConstructionRequestDTO request,
            in ConstructionValidationSettingsDTO settings,
            NativeParallelMultiHashMap<int, BaseModuleOccupancyDTO> occupancy)
        {
            uint candidateMask = settings.CandidatePortMask;
            if (candidateMask == 0u)
                return true;

            bool hasAnyNeighbor = false;
            bool hasAlignedPort = false;
            CheckPort(request.GridPos, candidateMask, ConstructionPortMask.PosX, occupancy, ref hasAnyNeighbor, ref hasAlignedPort);
            CheckPort(request.GridPos, candidateMask, ConstructionPortMask.NegX, occupancy, ref hasAnyNeighbor, ref hasAlignedPort);
            CheckPort(request.GridPos, candidateMask, ConstructionPortMask.PosY, occupancy, ref hasAnyNeighbor, ref hasAlignedPort);
            CheckPort(request.GridPos, candidateMask, ConstructionPortMask.NegY, occupancy, ref hasAnyNeighbor, ref hasAlignedPort);
            CheckPort(request.GridPos, candidateMask, ConstructionPortMask.PosZ, occupancy, ref hasAnyNeighbor, ref hasAlignedPort);
            CheckPort(request.GridPos, candidateMask, ConstructionPortMask.NegZ, occupancy, ref hasAnyNeighbor, ref hasAlignedPort);
            return !hasAnyNeighbor || hasAlignedPort;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CheckPort(
            int3 grid,
            uint candidateMask,
            uint port,
            NativeParallelMultiHashMap<int, BaseModuleOccupancyDTO> occupancy,
            ref bool hasAnyNeighbor,
            ref bool hasAlignedPort)
        {
            int3 neighborGrid = grid + DirectionForPort(port);
            int neighborHash = HashGrid(neighborGrid);
            NativeParallelMultiHashMapIterator<int> iterator;
            if (!occupancy.TryGetFirstValue(neighborHash, out BaseModuleOccupancyDTO neighbor, out iterator))
                return;

            hasAnyNeighbor = true;
            uint opposite = OppositePort(port);
            do
            {
                if ((candidateMask & port) != 0u && (neighbor.PortMask & opposite) != 0u)
                {
                    hasAlignedPort = true;
                    return;
                }
            }
            while (occupancy.TryGetNextValue(out neighbor, ref iterator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ResolveAabbProbe(int index, float3 localCenter, in StructuralBoundsDTO bounds, uint rotation)
        {
            float3 extents = math.max(bounds.Extents, new float3(0.005f));
            float3 center = localCenter + RotateYaw(bounds.CenterOffset, rotation);
            switch (index)
            {
                case 0: return center;
                case 1: return center + RotateYaw(new float3(-extents.x, -extents.y, -extents.z), rotation);
                case 2: return center + RotateYaw(new float3(extents.x, -extents.y, -extents.z), rotation);
                case 3: return center + RotateYaw(new float3(-extents.x, -extents.y, extents.z), rotation);
                case 4: return center + RotateYaw(new float3(extents.x, -extents.y, extents.z), rotation);
                case 5: return center + RotateYaw(new float3(-extents.x, extents.y, -extents.z), rotation);
                case 6: return center + RotateYaw(new float3(extents.x, extents.y, -extents.z), rotation);
                case 7: return center + RotateYaw(new float3(-extents.x, extents.y, extents.z), rotation);
                default: return center + RotateYaw(new float3(extents.x, extents.y, extents.z), rotation);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 RotateYaw(float3 value, uint rotation)
        {
            switch (rotation & 3u)
            {
                case 1u: return new float3(value.z, value.y, -value.x);
                case 2u: return new float3(-value.x, value.y, -value.z);
                case 3u: return new float3(-value.z, value.y, value.x);
                default: return value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsSafeLocal(double3 value)
        {
            return math.all(math.isfinite(value)) && math.all(math.abs(value) <= 100000d);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0.0001f ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SanitizeFinite(float3 value)
        {
            return math.select(float3.zero, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint HashResult(in ConstructionRequestDTO request, uint flags, float minDistance, int probeCount)
        {
            unchecked
            {
                uint hash = FnvOffset;
                hash = (hash ^ (uint)request.GridPos.x) * FnvPrime;
                hash = (hash ^ (uint)request.GridPos.y) * FnvPrime;
                hash = (hash ^ (uint)request.GridPos.z) * FnvPrime;
                hash = (hash ^ request.ModuleHash) * FnvPrime;
                hash = (hash ^ flags) * FnvPrime;
                hash = (hash ^ (uint)math.asint(minDistance)) * FnvPrime;
                hash = (hash ^ (uint)probeCount) * FnvPrime;
                return hash != 0u ? hash : 1u;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BurstGridValidationJob : IJob
    {
        [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, BaseModuleOccupancyDTO> Occupancy;
        public ConstructionRequestDTO Request;
        public StructuralBoundsDTO Bounds;
        public ConstructionValidationSettingsDTO Settings;
        public ConstructionSipBudgetDTO SipBudget;
        public MockWorldSampler WorldSampler;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ConstructionValidationResultDTO> Result;

        public void Execute()
        {
            if (!Result.IsCreated || Result.Length <= 0)
                return;

            ModularBaseConstructionValidator.ValidatePlacementCore(
                in Request,
                in Bounds,
                in Settings,
                in WorldSampler,
                in SipBudget,
                true,
                Occupancy,
                out ConstructionValidationResultDTO result);
            Result[0] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct LogisticsGraphSpliceJob : IJob
    {
        [ReadOnly, NoAlias] public NativeParallelMultiHashMap<int, BaseModuleOccupancyDTO> Occupancy;
        public ConstructionRequestDTO Request;
        public ConstructionValidationSettingsDTO Settings;
        public int NewNodeIndex;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int2> EdgeScratch;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> EdgeCount;

        public void Execute()
        {
            if (!EdgeScratch.IsCreated || !EdgeCount.IsCreated || EdgeCount.Length <= 0)
                return;

            int count = 0;
            TryWriteEdge(ConstructionPortMask.PosX, ref count);
            TryWriteEdge(ConstructionPortMask.NegX, ref count);
            TryWriteEdge(ConstructionPortMask.PosY, ref count);
            TryWriteEdge(ConstructionPortMask.NegY, ref count);
            TryWriteEdge(ConstructionPortMask.PosZ, ref count);
            TryWriteEdge(ConstructionPortMask.NegZ, ref count);
            EdgeCount[0] = count;
        }

        private void TryWriteEdge(uint port, ref int count)
        {
            if ((Settings.CandidatePortMask & port) == 0u || count >= EdgeScratch.Length)
                return;

            int3 neighborGrid = Request.GridPos + ModularBaseConstructionValidator.DirectionForPort(port);
            int hash = ModularBaseConstructionValidator.HashGrid(neighborGrid);
            NativeParallelMultiHashMapIterator<int> iterator;
            if (!Occupancy.TryGetFirstValue(hash, out BaseModuleOccupancyDTO neighbor, out iterator))
                return;

            uint opposite = ModularBaseConstructionValidator.OppositePort(port);
            do
            {
                if ((neighbor.PortMask & opposite) == 0u)
                    continue;

                EdgeScratch[count++] = new int2(NewNodeIndex, neighbor.NodeIndex);
                return;
            }
            while (Occupancy.TryGetNextValue(out neighbor, ref iterator));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct DeconstructionConnectivityJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<int2> DirectedEdges;
        public int NodeCount;
        public int RemovedNodeIndex;
        public int FoundationNodeIndex;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<byte> Visited;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<int> Queue;
        [NoAlias, NativeDisableParallelForRestriction] public NativeArray<ConstructionValidationResultDTO> Result;

        public void Execute()
        {
            if (!Visited.IsCreated ||
                !Queue.IsCreated ||
                !Result.IsCreated ||
                Result.Length <= 0 ||
                NodeCount <= 0 ||
                FoundationNodeIndex < 0 ||
                FoundationNodeIndex >= NodeCount)
            {
                WriteResult((uint)ConstructionValidationFlags.NonFiniteInput);
                return;
            }

            for (int i = 0; i < math.min(Visited.Length, NodeCount); i++)
                Visited[i] = 0;

            int head = 0;
            int tail = 0;
            if (FoundationNodeIndex != RemovedNodeIndex && tail < Queue.Length)
            {
                Queue[tail++] = FoundationNodeIndex;
                Visited[FoundationNodeIndex] = 1;
            }

            while (head < tail)
            {
                int node = Queue[head++];
                for (int i = 0; i < DirectedEdges.Length; i++)
                {
                    int2 edge = DirectedEdges[i];
                    int next = -1;
                    if (edge.x == node)
                        next = edge.y;
                    else if (edge.y == node)
                        next = edge.x;

                    if (next < 0 || next == RemovedNodeIndex || next >= NodeCount || Visited[next] != 0)
                        continue;

                    if (tail < Queue.Length)
                    {
                        Visited[next] = 1;
                        Queue[tail++] = next;
                    }
                }
            }

            for (int i = 0; i < NodeCount && i < Visited.Length; i++)
            {
                if (i == RemovedNodeIndex)
                    continue;

                if (Visited[i] == 0)
                {
                    WriteResult((uint)ConstructionValidationFlags.DisconnectedWing);
                    return;
                }
            }

            WriteResult(0u);
        }

        private void WriteResult(uint flags)
        {
            ConstructionValidationResultDTO result = default;
            result.FailureFlags = flags;
            result.MinSdfDistance = 0f;
            result.ProbeCount = 0;
            result.IsValid = (byte)(flags == 0u ? 1 : 0);
            result.ResultHash = flags == 0u ? 1u : flags;
            Result[0] = result;
        }
    }
}
