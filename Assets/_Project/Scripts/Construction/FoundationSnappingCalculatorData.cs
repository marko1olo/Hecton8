using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    public static class FoundationPylonFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint HitSdf = 1u << 1;
        public const uint ExtensionCulled = 1u << 2;
        public const uint OutOfSdfBounds = 1u << 3;
        public const uint NonFinite = 1u << 4;
        public const uint RollbackExcluded = 1u << 5;
        public const uint PresentationOnly = 1u << 6;
        public const uint MockSdfFallback = 1u << 7;
        public const uint RealVoxelSdf = 1u << 8;
        public const uint ApproximateSdf = 1u << 9;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PylonMatrixDTO
    {
        [FieldOffset(0)] public float4x4 LocalToWorld;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationPylonSurfaceDTO
    {
        [FieldOffset(0)] public float4 SurfaceNormalFlare;
        [FieldOffset(16)] public float4 AxisRadius;
        [FieldOffset(32)] public float4 HitLocalLength;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint ModuleHash;
        [FieldOffset(56)] public uint RayIndex;
        [FieldOffset(60)] public uint ResultHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationModuleAupDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public quaternion Rotation;
        [FieldOffset(40)] public float3 BoundsExtents;
        [FieldOffset(52)] public float GroundClearanceMeters;
        [FieldOffset(56)] public uint ModuleHash;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationSdfConfigDTO
    {
        [FieldOffset(0)] public double3 OriginAup;
        [FieldOffset(24)] public float VoxelSizeMeters;
        [FieldOffset(28)] public int SizeX;
        [FieldOffset(32)] public int SizeY;
        [FieldOffset(36)] public int SizeZ;
        [FieldOffset(40)] public float SdfRangeMeters;
        [FieldOffset(44)] public float IsoSurface;
        [FieldOffset(48)] public float MockSlopeX;
        [FieldOffset(52)] public float MockSlopeZ;
        [FieldOffset(56)] public float MockBaseY;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationTuningDTO
    {
        [FieldOffset(0)] public float MaxPylonLengthMeters;
        [FieldOffset(4)] public float SdfHitEpsilonMeters;
        [FieldOffset(8)] public float GlobalQualityWeight;
        [FieldOffset(12)] public float RadiusLowMeters;
        [FieldOffset(16)] public float RadiusUltraMeters;
        [FieldOffset(20)] public float ShaderFlareLow;
        [FieldOffset(24)] public float ShaderFlareUltra;
        [FieldOffset(28)] public float RayStartYOffsetMeters;
        [FieldOffset(32)] public int MinRaysPerModule;
        [FieldOffset(36)] public int MaxRaysPerModule;
        [FieldOffset(40)] public int MaxMarchStepsLow;
        [FieldOffset(44)] public int MaxMarchStepsUltra;
        [FieldOffset(48)] public float MaxMarchStepMeters;
        [FieldOffset(52)] public float GradientStepMeters;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationPylonFrameCounters
    {
        [FieldOffset(0)] public int ActivePylonCount;
        [FieldOffset(4)] public int SlotCount;
        [FieldOffset(8)] public int RaysCast;
        [FieldOffset(12)] public int HitCount;
        [FieldOffset(16)] public int CulledCount;
        [FieldOffset(20)] public float MaxResolvedLength;
        [FieldOffset(24)] public uint ResultHash;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] private byte _pad0;
        [FieldOffset(33)] private byte _pad1;
        [FieldOffset(34)] private byte _pad2;
        [FieldOffset(35)] private byte _pad3;
        [FieldOffset(36)] private byte _pad4;
        [FieldOffset(37)] private byte _pad5;
        [FieldOffset(38)] private byte _pad6;
        [FieldOffset(39)] private byte _pad7;
        [FieldOffset(40)] private byte _pad8;
        [FieldOffset(41)] private byte _pad9;
        [FieldOffset(42)] private byte _pad10;
        [FieldOffset(43)] private byte _pad11;
        [FieldOffset(44)] private byte _pad12;
        [FieldOffset(45)] private byte _pad13;
        [FieldOffset(46)] private byte _pad14;
        [FieldOffset(47)] private byte _pad15;
        [FieldOffset(48)] private byte _pad16;
        [FieldOffset(49)] private byte _pad17;
        [FieldOffset(50)] private byte _pad18;
        [FieldOffset(51)] private byte _pad19;
        [FieldOffset(52)] private byte _pad20;
        [FieldOffset(53)] private byte _pad21;
        [FieldOffset(54)] private byte _pad22;
        [FieldOffset(55)] private byte _pad23;
        [FieldOffset(56)] private byte _pad24;
        [FieldOffset(57)] private byte _pad25;
        [FieldOffset(58)] private byte _pad26;
        [FieldOffset(59)] private byte _pad27;
        [FieldOffset(60)] private byte _pad28;
        [FieldOffset(61)] private byte _pad29;
        [FieldOffset(62)] private byte _pad30;
        [FieldOffset(63)] private byte _pad31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationTelemetryEntry
    {
        [FieldOffset(0)] public double3 FirstModuleAup;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint SlotCount;
        [FieldOffset(32)] public uint RaysCast;
        [FieldOffset(36)] public uint HitCount;
        [FieldOffset(40)] public uint ActivePylonCount;
        [FieldOffset(44)] public float MaxResolvedLength;
        [FieldOffset(48)] public float SolverMicroseconds;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint ResultHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct FoundationPylonIndirectArgsDTO
    {
        [FieldOffset(0)] public uint VertexCountPerInstance;
        [FieldOffset(4)] public uint InstanceCount;
        [FieldOffset(8)] public uint StartVertex;
        [FieldOffset(12)] public uint StartInstance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct FoundationRayOriginDTO
    {
        [FieldOffset(0)] public uint ModuleHash;
        [FieldOffset(4)] public uint RayIndex;
        [FieldOffset(8)] public float3 NormalizedOffset;
        [FieldOffset(20)] public float RadiusMultiplier;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private byte _pad0;
        [FieldOffset(29)] private byte _pad1;
        [FieldOffset(30)] private byte _pad2;
        [FieldOffset(31)] private byte _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct FoundationProfileRangeDTO
    {
        [FieldOffset(0)] public uint ModuleHash;
        [FieldOffset(4)] public int StartIndex;
        [FieldOffset(8)] public int Count;
        [FieldOffset(12)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationDebugRayDTO
    {
        [FieldOffset(0)] public double3 OriginAup;
        [FieldOffset(24)] public double3 HitAup;
        [FieldOffset(48)] public float LengthMeters;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public uint ModuleHash;
        [FieldOffset(60)] public uint RayIndex;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct FoundationStructuralWarningSignal : ISignal
    {
        public const uint LaneHash = 0x46574E47u; // FWNG

        [FieldOffset(0)] public double3 ModuleAup;
        [FieldOffset(24)] public uint ModuleHash;
        [FieldOffset(28)] public uint WarningFlags;
        [FieldOffset(32)] public float RequestedLengthMeters;
        [FieldOffset(36)] public float MaxLengthMeters;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint ResultHash;
        [FieldOffset(48)] private byte _pad0;
        [FieldOffset(49)] private byte _pad1;
        [FieldOffset(50)] private byte _pad2;
        [FieldOffset(51)] private byte _pad3;
        [FieldOffset(52)] private byte _pad4;
        [FieldOffset(53)] private byte _pad5;
        [FieldOffset(54)] private byte _pad6;
        [FieldOffset(55)] private byte _pad7;
        [FieldOffset(56)] private byte _pad8;
        [FieldOffset(57)] private byte _pad9;
        [FieldOffset(58)] private byte _pad10;
        [FieldOffset(59)] private byte _pad11;
        [FieldOffset(60)] private byte _pad12;
        [FieldOffset(61)] private byte _pad13;
        [FieldOffset(62)] private byte _pad14;
        [FieldOffset(63)] private byte _pad15;
    }

    public ref struct FoundationSnappingVaultViews
    {
        public NativeArray<FoundationModuleAupDTO> Modules;
        public NativeArray<PylonMatrixDTO> PylonMatrices;
        public NativeArray<FoundationPylonSurfaceDTO> PylonSurfaces;
        public NativeArray<FoundationPylonFrameCounters> PerModuleCounters;
        public NativeArray<FoundationPylonFrameCounters> FrameCounters;
        public NativeArray<FoundationTelemetryEntry> Telemetry;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<FoundationTuningDTO> Tuning;
        public NativeArray<float> MockSdfDistances;
        public NativeArray<FoundationSdfConfigDTO> SdfConfig;
        public NativeArray<FoundationRayOriginDTO> RayOrigins;
        public NativeArray<FoundationProfileRangeDTO> ProfileRanges;
        public NativeArray<byte> CsvScratch;
        public NativeArray<FoundationDebugRayDTO> DebugRays;
        public NativeArray<FoundationPylonIndirectArgsDTO> IndirectArgs;
    }

    public static class FoundationSnappingCalculatorRuntime
    {
        public const int ModuleCapacity = ShinobuSocketConstructionRuntime.MockModuleCount;
        public const int MaxRaysPerModule = 4;
        public const int PylonCapacity = ModuleCapacity * MaxRaysPerModule;
        public const int TelemetryCapacity = 300;
        public const int MockSdfSizeX = 64;
        public const int MockSdfSizeY = 64;
        public const int MockSdfSizeZ = 64;
        public const int MockSdfSampleCount = MockSdfSizeX * MockSdfSizeY * MockSdfSizeZ;
        public const int ProfileCapacity = 256;
        public const int RayProfileCapacity = ProfileCapacity * MaxRaysPerModule;
        public const int CsvScratchCapacity = 32 * 1024;
        public const int ProceduralPylonVertexCount = 96;
        public const int PylonMatrixSizeBytes = 64;
        public const int PylonSurfaceSizeBytes = 64;
        public const int FoundationModuleSizeBytes = 64;
        public const int SdfConfigSizeBytes = 64;
        public const int FoundationTuningSizeBytes = 64;
        public const int FoundationFrameCounterSizeBytes = 64;
        public const int FoundationTelemetrySizeBytes = 64;
        public const int FoundationIndirectArgsSizeBytes = 16;
        public const int FoundationRayOriginSizeBytes = 32;
        public const int FoundationProfileRangeSizeBytes = 16;
        public const int FoundationDebugRaySizeBytes = 64;
        public const BufferID ModuleBufferId = BufferID.FoundationSnappingModules;
        public const BufferID PylonMatrixBufferId = BufferID.FoundationSnappingPylonMatrices;
        public const BufferID PylonSurfaceBufferId = BufferID.FoundationSnappingPylonSurfaces;
        public const BufferID PerModuleCounterBufferId = BufferID.FoundationSnappingPerModuleCounters;
        public const BufferID FrameCounterBufferId = BufferID.FoundationSnappingFrameCounters;
        public const BufferID TelemetryBufferId = BufferID.FoundationSnappingTelemetryRing;
        public const BufferID TelemetryCursorBufferId = BufferID.FoundationSnappingTelemetryCursor;
        public const BufferID TuningBufferId = BufferID.FoundationSnappingTuning;
        public const BufferID MockSdfDistanceBufferId = BufferID.FoundationSnappingMockSdfDistances;
        public const BufferID SdfConfigBufferId = BufferID.FoundationSnappingSdfConfig;
        public const BufferID RayOriginBufferId = BufferID.FoundationSnappingRayOrigins;
        public const BufferID ProfileRangeBufferId = BufferID.FoundationSnappingProfileRanges;
        public const BufferID CsvScratchBufferId = BufferID.FoundationSnappingCsvScratch;
        public const BufferID DebugRayBufferId = BufferID.FoundationSnappingDebugRays;
        public const BufferID IndirectArgsBufferId = BufferID.FoundationSnappingIndirectArgs;
        public const string DumpPath = "Docs/AgentLogs/Dump_1306_Construction_FoundationCalculator.bin";

        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private static FoundationTuningDTO s_Tuning = CreateDefaultTuning(1f);
        private static FoundationSdfConfigDTO s_SdfConfig = CreateDefaultMockSdfConfig(double3.zero);
        private static IDataVault s_BoundVault;
        private static bool s_TelemetryDumped;
        private static bool s_TelemetryCursorSeeded;
        private static uint s_TelemetryCursorSeedGeneration;
        private static int s_ProfileReadFenceDepth;
        private static int s_ProfileWriteFence;
        private static VaultGenerationHandle<FoundationModuleAupDTO> s_ModuleHandle;
        private static VaultGenerationHandle<PylonMatrixDTO> s_PylonMatrixHandle;
        private static VaultGenerationHandle<FoundationPylonSurfaceDTO> s_PylonSurfaceHandle;
        private static VaultGenerationHandle<FoundationPylonFrameCounters> s_PerModuleCounterHandle;
        private static VaultGenerationHandle<FoundationPylonFrameCounters> s_FrameCounterHandle;
        private static VaultGenerationHandle<FoundationTelemetryEntry> s_TelemetryHandle;
        private static VaultGenerationHandle<int> s_TelemetryCursorHandle;
        private static VaultGenerationHandle<FoundationTuningDTO> s_TuningHandle;
        private static VaultGenerationHandle<float> s_MockSdfDistanceHandle;
        private static VaultGenerationHandle<FoundationSdfConfigDTO> s_SdfConfigHandle;
        private static VaultGenerationHandle<FoundationRayOriginDTO> s_RayOriginHandle;
        private static VaultGenerationHandle<FoundationProfileRangeDTO> s_ProfileRangeHandle;
        private static VaultGenerationHandle<byte> s_CsvScratchHandle;
        private static VaultGenerationHandle<FoundationDebugRayDTO> s_DebugRayHandle;
        private static VaultGenerationHandle<FoundationPylonIndirectArgsDTO> s_IndirectArgsHandle;
        private static int s_ProfileCount;
        private static int s_RayOriginCount;

        public static bool ValidateStructLayout()
        {
            if (UnsafeUtility.SizeOf<PylonMatrixDTO>() != PylonMatrixSizeBytes ||
                UnsafeUtility.SizeOf<FoundationPylonSurfaceDTO>() != PylonSurfaceSizeBytes ||
                UnsafeUtility.SizeOf<FoundationModuleAupDTO>() != FoundationModuleSizeBytes ||
                UnsafeUtility.SizeOf<FoundationSdfConfigDTO>() != SdfConfigSizeBytes ||
                UnsafeUtility.SizeOf<FoundationTuningDTO>() != FoundationTuningSizeBytes ||
                UnsafeUtility.SizeOf<FoundationPylonFrameCounters>() != FoundationFrameCounterSizeBytes ||
                UnsafeUtility.SizeOf<FoundationTelemetryEntry>() != FoundationTelemetrySizeBytes ||
                UnsafeUtility.SizeOf<FoundationPylonIndirectArgsDTO>() != FoundationIndirectArgsSizeBytes ||
                UnsafeUtility.SizeOf<FoundationRayOriginDTO>() != FoundationRayOriginSizeBytes ||
                UnsafeUtility.SizeOf<FoundationProfileRangeDTO>() != FoundationProfileRangeSizeBytes ||
                UnsafeUtility.SizeOf<FoundationDebugRayDTO>() != FoundationDebugRaySizeBytes)
            {
                return false;
            }

#if UNITY_EDITOR
            return ResolveOffset<PylonMatrixDTO>(nameof(PylonMatrixDTO.LocalToWorld)) == 0 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.SurfaceNormalFlare)) == 0 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.AxisRadius)) == 16 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.HitLocalLength)) == 32 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.Flags)) == 48 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.ModuleHash)) == 52 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.RayIndex)) == 56 &&
                   ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.ResultHash)) == 60 &&
                   ResolveOffset<FoundationModuleAupDTO>(nameof(FoundationModuleAupDTO.CenterAup)) == 0 &&
                   ResolveOffset<FoundationModuleAupDTO>(nameof(FoundationModuleAupDTO.Rotation)) == 24 &&
                   ResolveOffset<FoundationModuleAupDTO>(nameof(FoundationModuleAupDTO.BoundsExtents)) == 40 &&
                   ResolveOffset<FoundationPylonFrameCounters>(nameof(FoundationPylonFrameCounters.ActivePylonCount)) == 0 &&
                   ResolveOffset<FoundationPylonFrameCounters>(nameof(FoundationPylonFrameCounters.Flags)) == 28;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        public static int ResolveOffset<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
#endif

        public static bool InitializeVault(IDataVault vault, double3 mockOriginAup)
        {
            if (vault == null)
                return false;

            ResetVaultDescriptorsIfOwnerChanged(vault);
            s_Tuning = SanitizeTuning(s_Tuning, ResolveGlobalQualityWeight());
            s_SdfConfig = SanitizeSdfConfig(CreateDefaultMockSdfConfig(mockOriginAup));
            s_ModuleHandle = EnsureVaultHandle(vault, ModuleBufferId, ModuleCapacity, ref s_ModuleHandle);
            s_PylonMatrixHandle = EnsureVaultHandle(vault, PylonMatrixBufferId, PylonCapacity, ref s_PylonMatrixHandle);
            s_PylonSurfaceHandle = EnsureVaultHandle(vault, PylonSurfaceBufferId, PylonCapacity, ref s_PylonSurfaceHandle);
            s_PerModuleCounterHandle = EnsureVaultHandle(vault, PerModuleCounterBufferId, ModuleCapacity, ref s_PerModuleCounterHandle);
            s_FrameCounterHandle = EnsureVaultHandle(vault, FrameCounterBufferId, 1, ref s_FrameCounterHandle);
            s_TelemetryHandle = EnsureVaultHandle(vault, TelemetryBufferId, TelemetryCapacity, ref s_TelemetryHandle);
            s_TelemetryCursorHandle = EnsureVaultHandle(vault, TelemetryCursorBufferId, 1, ref s_TelemetryCursorHandle);
            s_TuningHandle = EnsureVaultHandle(vault, TuningBufferId, 1, ref s_TuningHandle);
            s_MockSdfDistanceHandle = EnsureVaultHandle(vault, MockSdfDistanceBufferId, MockSdfSampleCount, ref s_MockSdfDistanceHandle);
            s_SdfConfigHandle = EnsureVaultHandle(vault, SdfConfigBufferId, 1, ref s_SdfConfigHandle);
            s_RayOriginHandle = EnsureVaultHandle(vault, RayOriginBufferId, RayProfileCapacity, ref s_RayOriginHandle);
            s_ProfileRangeHandle = EnsureVaultHandle(vault, ProfileRangeBufferId, ProfileCapacity, ref s_ProfileRangeHandle);
            s_CsvScratchHandle = EnsureVaultHandle(vault, CsvScratchBufferId, CsvScratchCapacity, ref s_CsvScratchHandle);
            s_DebugRayHandle = EnsureVaultHandle(vault, DebugRayBufferId, PylonCapacity, ref s_DebugRayHandle);
            s_IndirectArgsHandle = EnsureVaultHandle(vault, IndirectArgsBufferId, 1, ref s_IndirectArgsHandle);

            if (!TryAcquireWriteLane(vault, in s_TelemetryCursorHandle, 1, out NativeArray<int> cursor))
                return false;

            try
            {
                if (!s_TelemetryCursorSeeded || s_TelemetryCursorSeedGeneration != s_TelemetryCursorHandle.Generation)
                {
                    cursor[0] = 0;
                    s_TelemetryCursorSeeded = true;
                    s_TelemetryCursorSeedGeneration = s_TelemetryCursorHandle.Generation;
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in s_TelemetryCursorHandle, SystemID.Construction);
            }

            if (!TryAcquireWriteLane(vault, in s_TuningHandle, 1, out NativeArray<FoundationTuningDTO> tuning))
                return false;

            try
            {
                tuning[0] = s_Tuning;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_TuningHandle, SystemID.Construction);
            }

            if (!TryAcquireWriteLane(vault, in s_SdfConfigHandle, 1, out NativeArray<FoundationSdfConfigDTO> config))
                return false;

            try
            {
                config[0] = s_SdfConfig;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_SdfConfigHandle, SystemID.Construction);
            }

            return ValidateStructLayout();
        }

        public static void UnbindDataVault(IDataVault vault)
        {
            if (vault != null && ReferenceEquals(s_BoundVault, vault))
                ResetVaultDescriptorsIfOwnerChanged(null);
        }

        private static void ResetVaultDescriptorsIfOwnerChanged(IDataVault vault)
        {
            if (ReferenceEquals(s_BoundVault, vault))
                return;

            s_BoundVault = vault;
            s_TelemetryDumped = false;
            s_TelemetryCursorSeeded = false;
            s_TelemetryCursorSeedGeneration = 0u;
            s_ModuleHandle = default;
            s_PylonMatrixHandle = default;
            s_PylonSurfaceHandle = default;
            s_PerModuleCounterHandle = default;
            s_FrameCounterHandle = default;
            s_TelemetryHandle = default;
            s_TelemetryCursorHandle = default;
            s_TuningHandle = default;
            s_MockSdfDistanceHandle = default;
            s_SdfConfigHandle = default;
            s_RayOriginHandle = default;
            s_ProfileRangeHandle = default;
            s_CsvScratchHandle = default;
            s_DebugRayHandle = default;
            s_IndirectArgsHandle = default;
        }

        public static bool InitializeVault(IDataVault vault)
        {
            return InitializeVault(vault, HectonFloatingOrigin.CurrentTotalOffsetDouble);
        }

        public static bool TryReadVaultViews(IDataVault vault, out FoundationSnappingVaultViews views)
        {
            views = default;
            if (vault == null)
                return false;

            return vault.TryReadHandle(in s_ModuleHandle, out views.Modules) &&
                   vault.TryReadHandle(in s_PylonMatrixHandle, out views.PylonMatrices) &&
                   vault.TryReadHandle(in s_PylonSurfaceHandle, out views.PylonSurfaces) &&
                   vault.TryReadHandle(in s_PerModuleCounterHandle, out views.PerModuleCounters) &&
                   vault.TryReadHandle(in s_FrameCounterHandle, out views.FrameCounters) &&
                   vault.TryReadHandle(in s_TelemetryHandle, out views.Telemetry) &&
                   vault.TryReadHandle(in s_TelemetryCursorHandle, out views.TelemetryCursor) &&
                   vault.TryReadHandle(in s_TuningHandle, out views.Tuning) &&
                   vault.TryReadHandle(in s_MockSdfDistanceHandle, out views.MockSdfDistances) &&
                   vault.TryReadHandle(in s_SdfConfigHandle, out views.SdfConfig) &&
                   vault.TryReadHandle(in s_RayOriginHandle, out views.RayOrigins) &&
                   vault.TryReadHandle(in s_ProfileRangeHandle, out views.ProfileRanges) &&
                   vault.TryReadHandle(in s_CsvScratchHandle, out views.CsvScratch) &&
                   vault.TryReadHandle(in s_DebugRayHandle, out views.DebugRays) &&
                   vault.TryReadHandle(in s_IndirectArgsHandle, out views.IndirectArgs);
        }

        public static FoundationTuningDTO CreateDefaultTuning(float quality)
        {
            FoundationTuningDTO tuning;
            tuning.MaxPylonLengthMeters = 42f;
            tuning.SdfHitEpsilonMeters = 0.04f;
            tuning.GlobalQualityWeight = SanitizeQuality(quality);
            tuning.RadiusLowMeters = 0.18f;
            tuning.RadiusUltraMeters = 0.32f;
            tuning.ShaderFlareLow = 0.12f;
            tuning.ShaderFlareUltra = 0.65f;
            tuning.RayStartYOffsetMeters = 0.25f;
            tuning.MinRaysPerModule = 1;
            tuning.MaxRaysPerModule = MaxRaysPerModule;
            tuning.MaxMarchStepsLow = 1;
            tuning.MaxMarchStepsUltra = 96;
            tuning.MaxMarchStepMeters = 1.75f;
            tuning.GradientStepMeters = 0.35f;
            tuning.Frame = 0u;
            tuning.Flags = 0u;
            return tuning;
        }

        public static FoundationSdfConfigDTO CreateDefaultMockSdfConfig(double3 originAup)
        {
            FoundationSdfConfigDTO config;
            config.OriginAup = originAup - new double3(
                MockSdfSizeX * 0.5d,
                MockSdfSizeY * 0.45d,
                MockSdfSizeZ * 0.5d);
            config.VoxelSizeMeters = 1f;
            config.SizeX = MockSdfSizeX;
            config.SizeY = MockSdfSizeY;
            config.SizeZ = MockSdfSizeZ;
            config.SdfRangeMeters = 32f;
            config.IsoSurface = 0f;
            config.MockSlopeX = 0.08f;
            config.MockSlopeZ = -0.05f;
            config.MockBaseY = MockSdfSizeY * 0.24f;
            config.Flags = FoundationPylonFlags.MockSdfFallback;
            return config;
        }

        public static FoundationTuningDTO GetTuning()
        {
            return s_Tuning;
        }

        public static FoundationSdfConfigDTO GetSdfConfig()
        {
            return s_SdfConfig;
        }

        public static bool TryApplyEditorTuning(
            float maxLength,
            float radiusLow,
            float radiusUltra,
            float flareLow,
            float flareUltra,
            int maxStepsLow,
            int maxStepsUltra)
        {
            s_Tuning.MaxPylonLengthMeters = SanitizePositive(maxLength, 42f);
            s_Tuning.RadiusLowMeters = SanitizePositive(radiusLow, 0.18f);
            s_Tuning.RadiusUltraMeters = math.max(s_Tuning.RadiusLowMeters, SanitizePositive(radiusUltra, 0.32f));
            s_Tuning.ShaderFlareLow = math.clamp(math.isfinite(flareLow) ? flareLow : 0.12f, 0f, 4f);
            s_Tuning.ShaderFlareUltra = math.max(s_Tuning.ShaderFlareLow, math.clamp(math.isfinite(flareUltra) ? flareUltra : 0.65f, 0f, 4f));
            s_Tuning.MaxMarchStepsLow = math.clamp(maxStepsLow, 1, 512);
            s_Tuning.MaxMarchStepsUltra = math.max(s_Tuning.MaxMarchStepsLow, math.clamp(maxStepsUltra, 1, 512));
            s_Tuning.GlobalQualityWeight = ResolveGlobalQualityWeight();
            IDataVault vault = s_BoundVault;
            if (vault == null)
                return false;

            InitializeVault(vault);
            if (!TryAcquireWriteLane(vault, in s_TuningHandle, 1, out NativeArray<FoundationTuningDTO> tuning))
            {
                return false;
            }

            try
            {
                tuning[0] = s_Tuning;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_TuningHandle, SystemID.Construction);
            }
        }

        public static bool TryReadEditorState(
            IDataVault vault,
            out int activeCount,
            out int slotCount,
            out float quality,
            out float maxLength,
            out uint frame,
            out uint flags,
            out uint hash)
        {
            activeCount = 0;
            slotCount = 0;
            quality = 0f;
            maxLength = 0f;
            frame = 0u;
            flags = 0u;
            hash = 0u;
            if (vault == null)
                return false;

            if (!TryReadVaultViews(vault, out FoundationSnappingVaultViews views) ||
                !views.FrameCounters.IsCreated ||
                views.FrameCounters.Length <= 0 ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length <= 0)
            {
                return false;
            }

            FoundationPylonFrameCounters counters = views.FrameCounters[0];
            FoundationTuningDTO tuning = views.Tuning[0];
            activeCount = counters.ActivePylonCount;
            slotCount = counters.SlotCount;
            quality = tuning.GlobalQualityWeight;
            maxLength = counters.MaxResolvedLength;
            frame = tuning.Frame;
            flags = counters.Flags;
            hash = counters.ResultHash;
            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadProfilesFromCsvBytes(
            ReadOnlySpan<byte> csv,
            NativeArray<FoundationRayOriginDTO> rayOrigins,
            NativeArray<FoundationProfileRangeDTO> profileRanges,
            out int profileCount,
            out int rayCount)
        {
            if (!TryBeginProfileWriteFence())
            {
                profileCount = 0;
                rayCount = 0;
                return false;
            }

            try
            {
                return TryLoadProfilesFromCsvBytesUnlocked(
                    csv,
                    rayOrigins,
                    profileRanges,
                    out profileCount,
                    out rayCount);
            }
            finally
            {
                EndProfileWriteFence();
            }
        }

        private static bool TryLoadProfilesFromCsvBytesUnlocked(
            ReadOnlySpan<byte> csv,
            NativeArray<FoundationRayOriginDTO> rayOrigins,
            NativeArray<FoundationProfileRangeDTO> profileRanges,
            out int profileCount,
            out int rayCount)
        {
            profileCount = 0;
            rayCount = 0;
            if (!rayOrigins.IsCreated ||
                !profileRanges.IsCreated ||
                csv.Length <= 0)
            {
                return false;
            }

            int lineStart = 0;
            while (lineStart < csv.Length && profileCount < profileRanges.Length && rayCount < rayOrigins.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csv.Length && csv[lineEnd] != (byte)'\n' && csv[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = csv.Slice(lineStart, lineEnd - lineStart);
                lineStart = lineEnd + 1;
                while (lineStart < csv.Length && (csv[lineStart] == (byte)'\n' || csv[lineStart] == (byte)'\r'))
                    lineStart++;

                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                if (IsProfileCsvHeader(line))
                    continue;

                if (!TryParseProfileLine(line, out uint moduleHash, out uint rayIndex, out float3 offset, out float radiusMultiplier))
                    continue;

                int profileIndex = FindProfile(profileRanges, profileCount, moduleHash);
                if (profileIndex < 0)
                {
                    int slotStart = profileCount * MaxRaysPerModule;
                    if (profileCount >= profileRanges.Length ||
                        slotStart < 0 ||
                        slotStart + MaxRaysPerModule > rayOrigins.Length)
                    {
                        break;
                    }

                    profileIndex = profileCount++;
                    FoundationProfileRangeDTO range;
                    range.ModuleHash = moduleHash;
                    range.StartIndex = slotStart;
                    range.Count = MaxRaysPerModule;
                    range.Flags = FoundationPylonFlags.Active;
                    profileRanges[profileIndex] = range;
                    for (int raySlot = 0; raySlot < MaxRaysPerModule; raySlot++)
                    {
                        FoundationRayOriginDTO inactive = default;
                        inactive.ModuleHash = moduleHash;
                        inactive.RayIndex = (uint)raySlot;
                        inactive.NormalizedOffset = float3.zero;
                        inactive.RadiusMultiplier = 1f;
                        inactive.Flags = 0u;
                        rayOrigins[slotStart + raySlot] = inactive;
                    }

                    rayCount = math.max(rayCount, slotStart + MaxRaysPerModule);
                }

                FoundationProfileRangeDTO activeRange = profileRanges[profileIndex];
                int writeIndex = activeRange.StartIndex + (int)rayIndex;
                if (activeRange.StartIndex < 0 ||
                    activeRange.Count <= 0 ||
                    writeIndex < activeRange.StartIndex ||
                    writeIndex >= activeRange.StartIndex + activeRange.Count ||
                    writeIndex >= rayOrigins.Length)
                {
                    continue;
                }

                FoundationRayOriginDTO ray = default;
                ray.ModuleHash = moduleHash;
                ray.RayIndex = rayIndex;
                ray.NormalizedOffset = math.clamp(offset, new float3(-1f), new float3(1f));
                ray.RadiusMultiplier = SanitizePositive(radiusMultiplier, 1f);
                ray.Flags = FoundationPylonFlags.Active;
                rayOrigins[writeIndex] = ray;
                rayCount = math.max(rayCount, writeIndex + 1);
            }

            s_ProfileCount = profileCount;
            s_RayOriginCount = rayCount;
            return profileCount > 0 && rayCount > 0;
        }

        public static unsafe bool TryLoadProfilesFromCsvFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryBeginProfileWriteFence())
                return false;

            IDataVault vault = s_BoundVault;
            if (vault == null)
            {
                EndProfileWriteFence();
                return false;
            }

            InitializeVault(vault);
            int profileEditLockCount = 0;
            try
            {
                if (!TryBeginProfileEditLocks(vault, out profileEditLockCount))
                    return false;

                if (!TryReadVaultViews(vault, out FoundationSnappingVaultViews views) ||
                    !views.CsvScratch.IsCreated ||
                    views.CsvScratch.Length <= 0)
                {
                    return false;
                }

                // COLD ALLOC: FileStream[1] - designer CSV authoring bridge; bytes land in Vault scratch - owner: SHINOBU_252
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                long length = stream.Length;
                if (length <= 0L || length > views.CsvScratch.Length)
                    return false;

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(views.CsvScratch);
                Span<byte> scratch = new Span<byte>(scratchPtr, views.CsvScratch.Length);
                int totalRead = 0;
                int expected = (int)length;
                while (totalRead < expected)
                {
                    int read = stream.Read(scratch.Slice(totalRead, expected - totalRead));
                    if (read <= 0)
                        break;

                    totalRead += read;
                }

                if (totalRead <= 0)
                    return false;

                return TryLoadProfilesFromCsvBytesUnlocked(
                    scratch.Slice(0, totalRead),
                    views.RayOrigins,
                    views.ProfileRanges,
                    out _,
                    out _);
            }
            catch
            {
                return false;
            }
            finally
            {
                EndProfileEditLocks(vault, profileEditLockCount);
                EndProfileWriteFence();
            }
        }
#endif

        public static bool TryBeginProfileReadFence()
        {
            if (Volatile.Read(ref s_ProfileWriteFence) != 0)
                return false;

            while (true)
            {
                int observed = Volatile.Read(ref s_ProfileReadFenceDepth);
                if (observed < 0 ||
                    observed == int.MaxValue ||
                    Volatile.Read(ref s_ProfileWriteFence) != 0)
                {
                    return false;
                }

                if (Interlocked.CompareExchange(ref s_ProfileReadFenceDepth, observed + 1, observed) == observed)
                    break;
            }

            if (Volatile.Read(ref s_ProfileWriteFence) == 0)
                return true;

            EndProfileReadFence();
            return false;
        }

        public static void EndProfileReadFence()
        {
            while (true)
            {
                int observed = Volatile.Read(ref s_ProfileReadFenceDepth);
                if (observed <= 0)
                    return;

                if (Interlocked.CompareExchange(ref s_ProfileReadFenceDepth, observed - 1, observed) == observed)
                    return;
            }
        }

        public static bool HasActiveProfileReadFence()
        {
            return Volatile.Read(ref s_ProfileReadFenceDepth) > 0;
        }

        public static int GetLoadedProfileCount()
        {
            return math.clamp(s_ProfileCount, 0, ProfileCapacity);
        }

        public static int GetLoadedRayOriginCount()
        {
            return math.clamp(s_RayOriginCount, 0, RayProfileCapacity);
        }

        public static void WriteTelemetry(
            NativeArray<FoundationTelemetryEntry> telemetry,
            NativeArray<int> cursor,
            double3 firstModuleAup,
            uint frame,
            in FoundationPylonFrameCounters counters,
            float solverMicroseconds,
            float quality)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int writeIndex = 0;
            if (cursor.IsCreated && cursor.Length > 0)
            {
                uint next = (uint)cursor[0];
                uint length = (uint)telemetry.Length;
                writeIndex = (int)(next % length);
                cursor[0] = (int)((next + 1u) % length);
            }

            FoundationTelemetryEntry entry;
            entry.FirstModuleAup = firstModuleAup;
            entry.Frame = frame;
            entry.SlotCount = (uint)math.max(0, counters.SlotCount);
            entry.RaysCast = (uint)math.max(0, counters.RaysCast);
            entry.HitCount = (uint)math.max(0, counters.HitCount);
            entry.ActivePylonCount = (uint)math.max(0, counters.ActivePylonCount);
            entry.MaxResolvedLength = counters.MaxResolvedLength;
            entry.SolverMicroseconds = math.max(0f, math.isfinite(solverMicroseconds) ? solverMicroseconds : 0f);
            entry.GlobalQualityWeight = SanitizeQuality(quality);
            entry.Flags = counters.Flags;
            entry.ResultHash = counters.ResultHash;
            telemetry[writeIndex] = entry;
        }

        public static bool DumpTelemetry(NativeArray<FoundationTelemetryEntry> telemetry, string path = DumpPath, bool force = false)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            if (s_TelemetryDumped && !force)
                return true;

            try
            {
                string resolvedPath = ResolveDumpPath(path);
                if (string.IsNullOrEmpty(resolvedPath))
                    return false;

                string directory = Path.GetDirectoryName(resolvedPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using FileStream stream = new FileStream(resolvedPath, FileMode.Create, FileAccess.Write, FileShare.Read);
                unsafe
                {
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    int bytes = UnsafeUtility.SizeOf<FoundationTelemetryEntry>() * telemetry.Length;
                    stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
                }

                s_TelemetryDumped = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveDumpPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (Path.IsPathRooted(path))
                return path;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveRaysPerModule(FoundationTuningDTO tuning)
        {
            int min = math.clamp(tuning.MinRaysPerModule, 1, MaxRaysPerModule);
            int max = math.clamp(tuning.MaxRaysPerModule, min, MaxRaysPerModule);
            return math.clamp((int)math.floor(ResolveRayBudget(tuning) + 0.0001f), min, max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRayBudget(FoundationTuningDTO tuning)
        {
            int min = math.clamp(tuning.MinRaysPerModule, 1, MaxRaysPerModule);
            int max = math.clamp(tuning.MaxRaysPerModule, min, MaxRaysPerModule);
            return max;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMarchSteps(FoundationTuningDTO tuning)
        {
            int low = math.clamp(tuning.MaxMarchStepsLow, 1, 512);
            int high = math.max(low, math.clamp(tuning.MaxMarchStepsUltra, 1, 512));
            return high;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSdfInterpolationWeight(FoundationTuningDTO tuning)
        {
            return 1f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRadius(FoundationTuningDTO tuning)
        {
            float quality = SanitizeQuality(tuning.GlobalQualityWeight);
            float low = SanitizePositive(tuning.RadiusLowMeters, 0.18f);
            float high = math.max(low, SanitizePositive(tuning.RadiusUltraMeters, 0.32f));
            return math.lerp(low, high, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveShaderFlare(FoundationTuningDTO tuning)
        {
            float quality = SanitizeQuality(tuning.GlobalQualityWeight);
            float low = math.clamp(math.isfinite(tuning.ShaderFlareLow) ? tuning.ShaderFlareLow : 0.12f, 0f, 4f);
            float high = math.max(low, math.clamp(math.isfinite(tuning.ShaderFlareUltra) ? tuning.ShaderFlareUltra : 0.65f, 0f, 4f));
            return math.lerp(low, high, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveGlobalQualityWeight()
        {
            return SanitizeQuality(HomeostasisBrain.GlobalQualityWeight);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeQuality(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FoldHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= FnvPrime;
            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashFloat3(float3 value)
        {
            uint3 bits = math.asuint(value);
            uint hash = FnvOffset;
            hash = FoldHash(hash, bits.x);
            hash = FoldHash(hash, bits.y);
            return FoldHash(hash, bits.z);
        }

        public static FoundationTuningDTO SanitizeTuning(FoundationTuningDTO tuning, float quality)
        {
            tuning.GlobalQualityWeight = SanitizeQuality(quality);
            tuning.MaxPylonLengthMeters = SanitizePositive(tuning.MaxPylonLengthMeters, 42f);
            tuning.SdfHitEpsilonMeters = math.clamp(SanitizePositive(tuning.SdfHitEpsilonMeters, 0.04f), 0.001f, 1f);
            tuning.RadiusLowMeters = SanitizePositive(tuning.RadiusLowMeters, 0.18f);
            tuning.RadiusUltraMeters = math.max(tuning.RadiusLowMeters, SanitizePositive(tuning.RadiusUltraMeters, 0.32f));
            tuning.MinRaysPerModule = math.clamp(tuning.MinRaysPerModule, 1, MaxRaysPerModule);
            tuning.MaxRaysPerModule = math.clamp(tuning.MaxRaysPerModule, tuning.MinRaysPerModule, MaxRaysPerModule);
            tuning.MaxMarchStepsLow = math.clamp(tuning.MaxMarchStepsLow, 1, 512);
            tuning.MaxMarchStepsUltra = math.max(tuning.MaxMarchStepsLow, math.clamp(tuning.MaxMarchStepsUltra, 1, 512));
            tuning.MaxMarchStepMeters = SanitizePositive(tuning.MaxMarchStepMeters, 1.75f);
            tuning.GradientStepMeters = SanitizePositive(tuning.GradientStepMeters, 0.35f);
            return tuning;
        }

        public static FoundationSdfConfigDTO SanitizeSdfConfig(FoundationSdfConfigDTO config)
        {
            config.VoxelSizeMeters = SanitizePositive(config.VoxelSizeMeters, 1f);
            config.SizeX = math.max(2, config.SizeX);
            config.SizeY = math.max(2, config.SizeY);
            config.SizeZ = math.max(2, config.SizeZ);
            config.SdfRangeMeters = SanitizePositive(config.SdfRangeMeters, 32f);
            config.IsoSurface = math.isfinite(config.IsoSurface) ? config.IsoSurface : 0f;
            config.MockSlopeX = math.isfinite(config.MockSlopeX) ? config.MockSlopeX : 0.08f;
            config.MockSlopeZ = math.isfinite(config.MockSlopeZ) ? config.MockSlopeZ : -0.05f;
            config.MockBaseY = math.isfinite(config.MockBaseY) ? config.MockBaseY : config.SizeY * 0.24f;
            return config;
        }

        private static VaultGenerationHandle<T> EnsureVaultHandle<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (handle.BufferID != 0u &&
                vault.TryReadHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return handle;
            }

            return vault.EnsureGenerationHandle<T>(
                bufferId,
                math.max(1, requiredLength),
                SystemID.Construction,
                NativeArrayOptions.UninitializedMemory);
        }

        private static bool TryAcquireWriteLane<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                handle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Construction, out buffer))
            {
                return false;
            }

            if (buffer.IsCreated && buffer.Length >= requiredLength)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.Construction);
            buffer = default;
            return false;
        }

#if UNITY_EDITOR
        private static bool TryBeginProfileWriteFence()
        {
            if (Volatile.Read(ref s_ProfileReadFenceDepth) != 0 ||
                Interlocked.CompareExchange(ref s_ProfileWriteFence, 1, 0) != 0)
            {
                return false;
            }

            if (Volatile.Read(ref s_ProfileReadFenceDepth) == 0)
                return true;

            Interlocked.Exchange(ref s_ProfileWriteFence, 0);
            return false;
        }

        private static void EndProfileWriteFence()
        {
            Interlocked.Exchange(ref s_ProfileWriteFence, 0);
        }

        private static bool TryBeginProfileEditLocks(IDataVault vault, out int lockedCount)
        {
            lockedCount = 0;
            if (vault == null)
                return false;

            if (!vault.TryLockBuffer(RayOriginBufferId, SystemID.Construction))
                return false;
            lockedCount = 1;

            if (!vault.TryLockBuffer(ProfileRangeBufferId, SystemID.Construction))
            {
                EndProfileEditLocks(vault, lockedCount);
                lockedCount = 0;
                return false;
            }
            lockedCount = 2;

            if (!vault.TryLockBuffer(CsvScratchBufferId, SystemID.Construction))
            {
                EndProfileEditLocks(vault, lockedCount);
                lockedCount = 0;
                return false;
            }
            lockedCount = 3;

            return true;
        }

        private static void EndProfileEditLocks(IDataVault vault, int lockedCount)
        {
            if (vault == null || lockedCount <= 0)
                return;

            if (lockedCount >= 3)
                vault.TryUnlockBuffer(CsvScratchBufferId, SystemID.Construction);
            if (lockedCount >= 2)
                vault.TryUnlockBuffer(ProfileRangeBufferId, SystemID.Construction);
            if (lockedCount >= 1)
                vault.TryUnlockBuffer(RayOriginBufferId, SystemID.Construction);
        }
#endif

#if UNITY_EDITOR
        private static bool TryParseProfileLine(
            ReadOnlySpan<byte> line,
            out uint moduleHash,
            out uint rayIndex,
            out float3 offset,
            out float radiusMultiplier)
        {
            moduleHash = 0u;
            rayIndex = 0u;
            offset = float3.zero;
            radiusMultiplier = 1f;
            int cursor = 0;
            if (!TryParseTokenHash(line, ref cursor, out moduleHash))
                return false;
            if (!TryParseTokenUInt(line, ref cursor, out rayIndex))
                return false;
            if (!TryParseTokenFloat(line, ref cursor, out offset.x))
                return false;
            if (!TryParseTokenFloat(line, ref cursor, out offset.y))
                return false;
            if (!TryParseTokenFloat(line, ref cursor, out offset.z))
                return false;
            if (!TryParseTokenFloat(line, ref cursor, out radiusMultiplier))
                radiusMultiplier = 1f;

            rayIndex = (uint)math.min((int)rayIndex, MaxRaysPerModule - 1);
            return true;
        }

        private static bool TryParseTokenHash(ReadOnlySpan<byte> line, ref int cursor, out uint hash)
        {
            hash = FnvOffset;
            SkipWhitespace(line, ref cursor);
            int start = cursor;
            bool any = false;
            while (cursor < line.Length &&
                   line[cursor] != (byte)',' &&
                   line[cursor] != (byte)';' &&
                   line[cursor] != (byte)'\t' &&
                   line[cursor] != (byte)' ')
            {
                byte value = line[cursor++];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);
                hash ^= value;
                hash *= FnvPrime;
                any = true;
            }

            SkipDelimiter(line, ref cursor);
            return any && cursor > start;
        }

        private static bool TryParseTokenUInt(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            value = 0u;
            SkipWhitespace(line, ref cursor);
            bool any = false;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                value = value * 10u + (uint)(c - (byte)'0');
                cursor++;
                any = true;
            }

            SkipDelimiter(line, ref cursor);
            return any;
        }

        private static bool TryParseTokenFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            SkipWhitespace(line, ref cursor);
            float sign = 1f;
            if (cursor < line.Length && line[cursor] == (byte)'-')
            {
                sign = -1f;
                cursor++;
            }
            else if (cursor < line.Length && line[cursor] == (byte)'+')
            {
                cursor++;
            }

            double whole = 0d;
            bool any = false;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                whole = whole * 10d + c - (byte)'0';
                cursor++;
                any = true;
            }

            double frac = 0d;
            double place = 0.1d;
            if (cursor < line.Length && line[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < line.Length)
                {
                    byte c = line[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    frac += (c - (byte)'0') * place;
                    place *= 0.1d;
                    cursor++;
                    any = true;
                }
            }

            value = (float)((whole + frac) * sign);
            SkipDelimiter(line, ref cursor);
            return any && math.isfinite(value);
        }

        private static int FindProfile(NativeArray<FoundationProfileRangeDTO> profileRanges, int count, uint moduleHash)
        {
            int safeCount = math.min(count, profileRanges.IsCreated ? profileRanges.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                if (profileRanges[i].ModuleHash == moduleHash)
                    return i;
            }

            return -1;
        }

        private static bool IsProfileCsvHeader(ReadOnlySpan<byte> line)
        {
            int cursor = 0;
            SkipWhitespace(line, ref cursor);
            int start = cursor;
            while (cursor < line.Length &&
                   line[cursor] != (byte)',' &&
                   line[cursor] != (byte)';' &&
                   line[cursor] != (byte)'\t' &&
                   line[cursor] != (byte)' ')
            {
                cursor++;
            }

            ReadOnlySpan<byte> token = line.Slice(start, cursor - start);
            return AsciiEquals(token, "module") ||
                   AsciiEquals(token, "module_hash") ||
                   AsciiEquals(token, "modulehash");
        }

        private static bool AsciiEquals(ReadOnlySpan<byte> token, string value)
        {
            if (token.Length != value.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                byte actual = token[i];
                if (actual >= (byte)'A' && actual <= (byte)'Z')
                    actual = (byte)(actual + 32);
                if (actual != (byte)value[i])
                    return false;
            }

            return true;
        }

        private static void SkipDelimiter(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length &&
                   (line[cursor] == (byte)',' || line[cursor] == (byte)';' || line[cursor] == (byte)'\t' || line[cursor] == (byte)' '))
            {
                cursor++;
            }
        }

        private static void SkipWhitespace(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && line[cursor] == (byte)' ')
                cursor++;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0.0001f ? value : fallback;
        }
    }
}
