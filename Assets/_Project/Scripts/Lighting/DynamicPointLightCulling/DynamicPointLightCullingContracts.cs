using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.Lighting
{
    internal static class DynamicPointLightCullingLayout
    {
        public const int LightCullStateStrideBytes = 32;
        public const int SourceStrideBytes = 96;
        public const int GpuPayloadStrideBytes = 64;
        public const int SettingsStrideBytes = 128;
        public const int ProfileRuleStrideBytes = 32;
        public const int SourceManifestStrideBytes = 64;
        public const int TelemetryEntryStrideBytes = 64;
        public const int RuntimeCountersStrideBytes = 64;
        public const int SelfAuditStrideBytes = 64;
    }

    /// <summary>
    /// Vault buffer identifiers owned by 13KRA. Local cast constants preserve legacy IDs and avoid global enum churn during batch execution.
    /// </summary>
    public static class DynamicPointLightCullingVaultIds
    {
        public const BufferID Sources = BufferID.DynamicPointLightCullingContracts_Sources;
        public const BufferID States = BufferID.DynamicPointLightCullingContracts_States;
        public const BufferID Settings = BufferID.DynamicPointLightCullingContracts_Settings;
        public const BufferID GpuPayloadFront = BufferID.DynamicPointLightCullingContracts_GpuPayloadFront;
        public const BufferID GpuPayloadBack = BufferID.DynamicPointLightCullingContracts_GpuPayloadBack;
        public const BufferID TelemetryRing = BufferID.DynamicPointLightCullingContracts_TelemetryRing;
        public const BufferID TelemetryCursor = BufferID.DynamicPointLightCullingContracts_TelemetryCursor;
        public const BufferID ImportanceKeys = BufferID.DynamicPointLightCullingContracts_ImportanceKeys;
        public const BufferID ImportanceIndices = BufferID.DynamicPointLightCullingContracts_ImportanceIndices;
        public const BufferID SortScratchKeys = BufferID.DynamicPointLightCullingContracts_SortScratchKeys;
        public const BufferID SortScratchIndices = BufferID.DynamicPointLightCullingContracts_SortScratchIndices;
        public const BufferID CsvScratch = BufferID.DynamicPointLightCullingContracts_CsvScratch;
        public const BufferID ProfileRules = BufferID.DynamicPointLightCullingContracts_ProfileRules;
        public const BufferID MockSdfSamples = BufferID.DynamicPointLightCullingContracts_MockSdfSamples;
        public const BufferID DynamicProbeLights = BufferID.DynamicPointLightCullingContracts_DynamicProbeLights;
        public const BufferID RuntimeCounters = BufferID.DynamicPointLightCullingContracts_RuntimeCounters;
        public const BufferID FrustumPlanes = BufferID.DynamicPointLightCullingContracts_FrustumPlanes;
        public const BufferID SelfAudit = BufferID.DynamicPointLightCullingContracts_SelfAudit;
        public const BufferID SourceManifest = BufferID.DynamicPointLightCullingContracts_SourceManifest;
    }

    /// <summary>
    /// Flags written by the dynamic point-light culling jobs.
    /// </summary>
    public static class DynamicPointLightCullingFlags
    {
        public const uint Active = 1u << 0;
        public const uint Submitted = 1u << 1;
        public const uint CulledByFrustum = 1u << 2;
        public const uint CulledByDistance = 1u << 3;
        public const uint CulledBySdf = 1u << 4;
        public const uint NonFinite = 1u << 5;
        public const uint MockSource = 1u << 6;
        public const uint Spot = 1u << 7;
        public const uint PlayerCritical = 1u << 8;
        public const uint ProfileOverridden = 1u << 9;
        public const uint GpuDirty = 1u << 10;
        public const uint TimedOut = 1u << 11;
        public const uint ProbeBouncePublished = 1u << 12;
        public const uint LayoutAligned = 1u << 13;
        public const uint LayoutInvalid = 1u << 14;
    }

    /// <summary>
    /// Ownership flags for the source manifest. This keeps source-count authority in the Vault instead of a private mirror.
    /// </summary>
    public static class DynamicPointLightSourceManifestFlags
    {
        public const uint Committed = 1u << 0;
        public const uint MockGenerated = 1u << 1;
        public const uint ExternalWriter = 1u << 2;
    }

    /// <summary>
    /// Exact 32-byte culling result record. Offset contract is maintained by 13KRA; field layout and legacy BufferID values stay unchanged.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.LightCullStateStrideBytes)]
    public struct LightCullStateDTO
    {
        [FieldOffset(0)] public uint LightHash;
        [FieldOffset(4)] public float DistanceSq;
        [FieldOffset(8)] public float BaseIntensity;
        [FieldOffset(12)] public float ComputedIntensity;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public byte _pad0;
        [FieldOffset(21)] public byte _pad1;
        [FieldOffset(22)] public byte _pad2;
        [FieldOffset(23)] public byte _pad3;
        [FieldOffset(24)] public byte _pad4;
        [FieldOffset(25)] public byte _pad5;
        [FieldOffset(26)] public byte _pad6;
        [FieldOffset(27)] public byte _pad7;
        [FieldOffset(28)] public byte _pad8;
        [FieldOffset(29)] public byte _pad9;
        [FieldOffset(30)] public byte _pad10;
        [FieldOffset(31)] public byte _pad11;
    }

    /// <summary>
    /// Raw source record for mathematical point/spot light evaluation. No Unity Light object is represented here.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.SourceStrideBytes)]
    public struct DynamicPointLightSourceDTO
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public float3 Color;
        [FieldOffset(36)] public float RangeMeters;
        [FieldOffset(40)] public float BaseIntensity;
        [FieldOffset(44)] public float Priority;
        [FieldOffset(48)] public float3 Direction;
        [FieldOffset(60)] public float SpotCosine;
        [FieldOffset(64)] public uint LightHash;
        [FieldOffset(68)] public uint Flags;
        [FieldOffset(72)] public float FadeDistanceSq;
        [FieldOffset(76)] public uint ProfileHash;
        [FieldOffset(80)] public float ShadowPhase01;
        [FieldOffset(84)] public float BounceWeight;
        [FieldOffset(88)] public float ThermalFadeBias;
        [FieldOffset(92)] public uint _pad0;
    }

    /// <summary>
    /// Shader-facing payload. Packed in float4 lanes for StructuredBuffer access.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.GpuPayloadStrideBytes)]
    public struct DynamicPointLightGpuDTO
    {
        [FieldOffset(0)] public float4 PositionRange;
        [FieldOffset(16)] public float4 ColorIntensity;
        [FieldOffset(32)] public float4 DirectionSpot;
        [FieldOffset(48)] public uint LightHash;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] public float DistanceSq;
        [FieldOffset(60)] public float BounceIntensity;
    }

    /// <summary>
    /// Runtime culling settings stored in the Vault. Size is 128 bytes for predictable cache and audit output.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.SettingsStrideBytes)]
    public struct DynamicPointLightCullingSettingsDTO
    {
        [FieldOffset(0)] public double3 CameraAup;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float ThermalPressure01;
        [FieldOffset(32)] public float BaseFadeDistanceSq;
        [FieldOffset(36)] public float ImportanceWeight;
        [FieldOffset(40)] public float SdfOcclusionThreshold;
        [FieldOffset(44)] public int ActiveSourceCount;
        [FieldOffset(48)] public int MaxActiveLights;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public int SdfSampleCount;
        [FieldOffset(64)] public double3 SdfOriginAup;
        [FieldOffset(88)] public float SdfCellSizeMeters;
        [FieldOffset(92)] public int SdfGridResolution;
        [FieldOffset(96)] public float BounceGain;
        [FieldOffset(100)] public float NearFieldOverkillBoost;
        [FieldOffset(104)] public float ThermalFadeStrength;
        [FieldOffset(108)] public float MaxRangeMeters;
        [FieldOffset(112)] public float SubmitIntensityEpsilon;
        [FieldOffset(116)] public int FrustumPlaneCount;
        [FieldOffset(120)] public uint SettingsHash;
        [FieldOffset(124)] public uint _pad0;
    }

    /// <summary>
    /// Allocation-free profile rule parsed from light_culling_profiles.csv.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.ProfileRuleStrideBytes)]
    public struct DynamicPointLightProfileRuleDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float PriorityMultiplier;
        [FieldOffset(8)] public float FadeDistanceMultiplier;
        [FieldOffset(12)] public float IntensityMultiplier;
        [FieldOffset(16)] public float SdfBias;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong _pad0;
    }

    /// <summary>
    /// Vault-resident source-count manifest. Writers update this only after fully writing the source/state window.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.SourceManifestStrideBytes)]
    public struct DynamicPointLightSourceManifestDTO
    {
        [FieldOffset(0)] public int ActiveSourceCount;
        [FieldOffset(4)] public int SourceCapacity;
        [FieldOffset(8)] public uint WriterHash;
        [FieldOffset(12)] public uint SourceRevision;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint LastCommitFrame;
        [FieldOffset(24)] public int RejectedSourceCount;
        [FieldOffset(28)] public uint VaultGeneration;
        [FieldOffset(32)] public ulong _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    /// <summary>
    /// Frame aggregate consumed by editor tooling and black-box dump.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.TelemetryEntryStrideBytes)]
    public struct DynamicPointLightCullingTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public int TotalLights;
        [FieldOffset(8)] public int CulledLights;
        [FieldOffset(12)] public int SubmittedLights;
        [FieldOffset(16)] public float BurstCpuUs;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public float ThermalPressure01;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public int MaxActiveLights;
        [FieldOffset(40)] public float MaxDistanceSq;
        [FieldOffset(44)] public float AverageIntensity;
        [FieldOffset(48)] public ulong LastGpuUploadBytes;
        [FieldOffset(56)] public uint VaultGeneration;
        [FieldOffset(60)] public uint _pad0;
    }

    /// <summary>
    /// Single-element runtime counter block produced by the Burst payload builder.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.RuntimeCountersStrideBytes)]
    public struct DynamicPointLightRuntimeCountersDTO
    {
        [FieldOffset(0)] public int TotalLights;
        [FieldOffset(4)] public int VisibleLights;
        [FieldOffset(8)] public int CulledLights;
        [FieldOffset(12)] public int SubmittedLights;
        [FieldOffset(16)] public float AverageSubmittedIntensity;
        [FieldOffset(20)] public float MaxDistanceSq;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint StateHash;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public int MaxActiveLights;
        [FieldOffset(40)] public float QualityWeight;
        [FieldOffset(44)] public float ThermalPressure01;
        [FieldOffset(48)] public uint FirstSubmittedHash;
        [FieldOffset(52)] public uint LastSubmittedHash;
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
    }

    /// <summary>
    /// Static byte-layout audit record written to Vault for tools and final reports.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = DynamicPointLightCullingLayout.SelfAuditStrideBytes)]
    public struct DynamicPointLightSelfAuditDTO
    {
        [FieldOffset(0)] public int LightCullStateSize;
        [FieldOffset(4)] public int SourceSize;
        [FieldOffset(8)] public int GpuPayloadSize;
        [FieldOffset(12)] public int TelemetrySize;
        [FieldOffset(16)] public int SettingsSize;
        [FieldOffset(20)] public int ProfileRuleSize;
        [FieldOffset(24)] public int SourceBufferId;
        [FieldOffset(28)] public int StateBufferId;
        [FieldOffset(32)] public int GpuFrontBufferId;
        [FieldOffset(36)] public int GpuBackBufferId;
        [FieldOffset(40)] public int TelemetryBufferId;
        [FieldOffset(44)] public int MaxMockLights;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint SourceHash;
        [FieldOffset(56)] public int SourceManifestBufferId;
        [FieldOffset(60)] public int SourceManifestSize;
    }

    /// <summary>
    /// Math helpers shared by runtime, tests, and Burst jobs.
    /// </summary>
    public static class DynamicPointLightCullingMath
    {
        public const int DefaultMockLightCount = 5000;
        public const int TelemetryCapacity = 300;
        public const int MinimumActiveLights = 8;
        public const int MaximumActiveLights = 64;
        public const uint SourceHash = 0x53483135u; // SH15

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMaxActiveLights(float globalQualityWeight)
        {
            float quality = Sanitize01(globalQualityWeight, 1f);
            float curved = quality * quality * (3f - 2f * quality);
            float budget = curved;
            return math.clamp((int)math.round(math.lerp(MinimumActiveLights, MaximumActiveLights, budget)), MinimumActiveLights, MaximumActiveLights);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveMaxActiveLights(float globalQualityWeight, float thermalPressure01)
        {
            float quality = Sanitize01(globalQualityWeight, 1f);
            float thermal = Sanitize01(thermalPressure01, 0f);
            float thermalCurve = thermal * thermal * (3f - 2f * thermal);
            float weighted = math.saturate(quality * math.lerp(1f, 0.35f, thermalCurve));
            return ResolveMaxActiveLights(weighted);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.isfinite(value) ? value : fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Hash32(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FnvaByte(uint hash, byte value)
        {
            return (hash ^ value) * 16777619u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint BuildImportanceKey(float computedIntensity, float priority, float distanceSq, float importanceWeight)
        {
            float safeIntensity = math.max(0f, math.isfinite(computedIntensity) ? computedIntensity : 0f);
            float safePriority = math.max(0f, math.isfinite(priority) ? priority : 0f);
            float safeDistanceSq = math.max(0f, math.isfinite(distanceSq) ? distanceSq : 0f);
            float weighted = safeIntensity * math.max(0.0001f, safePriority) * math.max(0.0001f, importanceWeight);
            float score = weighted * math.rcp(1f + safeDistanceSq * 0.02f);
            uint quantized = (uint)math.clamp((int)math.round(score * 1048576f), 0, 0x7FFFFFFF);
            return 0xFFFFFFFFu - quantized;
        }
    }
}
