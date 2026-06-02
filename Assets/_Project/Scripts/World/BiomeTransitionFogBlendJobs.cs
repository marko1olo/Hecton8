using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    public static class BiomeTransitionConstants
    {
        public const int MaxActiveBiomes = 64;
        public const int MaxBlendBiomes = 4;
        public const int TelemetryCapacity = 300;
        public const int CsvScratchBytes = 65536;
        public const int ShaderPayloadFloat4Count = 8;
        public const int ShaderPayloadStrideBytes = ShaderPayloadFloat4Count * 16;
        public const int BiomeLightingParametersStrideBytes = 64;
        public const int BiomeStateStrideBytes = 64;
        public const int BiomeCenterStrideBytes = 64;
        public const int CurrentAtmosphereStrideBytes = 128;
        public const int BlendMaskStrideBytes = 64;
        public const int InfluenceStrideBytes = 64;
        public const int AcousticStageStrideBytes = 64;
        public const int TelemetryStrideBytes = 64;
        public const int CounterStrideBytes = 64;
        public const int TuningStrideBytes = 64;
        public const int SectorSizeMeters = 1000;
        public const float MinRadiusMeters = 1f;
        public const float NaNEpsilon = 0.0001f;

        public const uint FlagFallbackMock = 1u << 0;
        public const uint FlagCsvLoaded = 1u << 1;
        public const uint FlagMissingBiomeCenters = 1u << 2;
        public const uint FlagInvalidInput = 1u << 3;
        public const uint FlagSignalEmitted = 1u << 4;
        public const uint FlagNonFiniteOutput = 1u << 5;
        public const uint FlagSectorCulled = 1u << 6;
        public const uint FlagNearestFallback = 1u << 7;
        public const uint FlagCadenceReused = 1u << 8;
    }

    public static class BiomeTransitionNativeLayout
    {
        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<BiomeStateDTO>() == BiomeTransitionConstants.BiomeStateStrideBytes &&
                   OffsetOf<BiomeStateDTO>(nameof(BiomeStateDTO.BiomeHash)) == 0 &&
                   OffsetOf<BiomeStateDTO>(nameof(BiomeStateDTO.FogColor)) == 16 &&
                   OffsetOf<BiomeStateDTO>(nameof(BiomeStateDTO.AbsorptionParams)) == 32 &&
                   OffsetOf<BiomeStateDTO>(nameof(BiomeStateDTO.AmbientAudioVolume)) == 48 &&
                   UnsafeUtility.SizeOf<BiomeCenterDTO>() == BiomeTransitionConstants.BiomeCenterStrideBytes &&
                   OffsetOf<BiomeCenterDTO>(nameof(BiomeCenterDTO.CenterAup)) == 0 &&
                   OffsetOf<BiomeCenterDTO>(nameof(BiomeCenterDTO.BiomeHash)) == 32 &&
                   OffsetOf<BiomeCenterDTO>(nameof(BiomeCenterDTO.StateIndex)) == 48 &&
                   UnsafeUtility.SizeOf<CurrentAtmosphereDTO>() == BiomeTransitionConstants.CurrentAtmosphereStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeBlendMaskDTO>() == BiomeTransitionConstants.BlendMaskStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeInfluenceDTO>() == BiomeTransitionConstants.InfluenceStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeAcousticStageDTO>() == BiomeTransitionConstants.AcousticStageStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeTransitionTelemetryEntry>() == BiomeTransitionConstants.TelemetryStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeTransitionCounterDTO>() == BiomeTransitionConstants.CounterStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeTransitionTuningDTO>() == BiomeTransitionConstants.TuningStrideBytes &&
                   UnsafeUtility.SizeOf<BiomeTransitionShaderPayloadCBufferDTO>() == BiomeTransitionConstants.ShaderPayloadStrideBytes &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.FogColor)) == 0 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.AbsorptionParams)) == 16 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.AudioParams)) == 32 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.NormalizedWeights)) == 48 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.BiomeHashes)) == 64 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.DitherParams)) == 80 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.FrameFlags)) == 96 &&
                   OffsetOf<BiomeTransitionShaderPayloadCBufferDTO>(nameof(BiomeTransitionShaderPayloadCBufferDTO.Reserved0)) == 112 &&
                   UnsafeUtility.SizeOf<BiomeLightingParametersDTO>() == BiomeTransitionConstants.BiomeLightingParametersStrideBytes &&
                   OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.PrimaryFogColor)) == 0 &&
                   OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.SecondaryFogColor)) == 16 &&
                   OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.FogDensity)) == 32 &&
                   OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.BlendFactor)) == 36 &&
                   OffsetOf<BiomeLightingParametersDTO>(nameof(BiomeLightingParametersDTO.LightShaftIntensity)) == 40;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            System.Reflection.FieldInfo field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct BiomeTransitionSample
    {
        [FieldOffset(0)] public byte FromBiomeId;
        [FieldOffset(1)] public byte ToBiomeId;
        [FieldOffset(2)] public byte Blend255;
        [FieldOffset(3)] public byte Flags;
        [FieldOffset(4)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct BiomeTransitionFogSource
    {
        [FieldOffset(0)] public float4 FogColor;
        [FieldOffset(16)] public float Density;
        [FieldOffset(20)] public float Turbidity;
        [FieldOffset(24)] public float Absorption;
        [FieldOffset(28)] public float FogAttenuationDistance;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeTransitionFogResult
    {
        [FieldOffset(0)] public BiomeTransitionSample Sample;
        [FieldOffset(8)] public uint DominantBiomeHash;
        [FieldOffset(12)] public uint SecondaryBiomeHash;
        [FieldOffset(16)] public float4 FogColor;
        [FieldOffset(32)] public float Density;
        [FieldOffset(36)] public float Turbidity;
        [FieldOffset(40)] public float Absorption;
        [FieldOffset(44)] public float FogAttenuationDistance;
        [FieldOffset(48)] public float AmbientAudioVolume;
        [FieldOffset(52)] public float NormalizedWeightSum;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.BiomeStateStrideBytes)]
    public struct BiomeStateDTO
    {
        [FieldOffset(0)] public uint BiomeHash;
        [FieldOffset(4)] public uint AuthoringIndex;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint _pad0;
        [FieldOffset(16)] public float4 FogColor;
        [FieldOffset(32)] public float4 AbsorptionParams;
        [FieldOffset(48)] public float AmbientAudioVolume;
        [FieldOffset(52)] public uint _pad1;
        [FieldOffset(56)] public uint _pad2;
        [FieldOffset(60)] public uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.BiomeCenterStrideBytes)]
    public struct BiomeCenterDTO
    {
        [FieldOffset(0)] public double3 CenterAup;
        [FieldOffset(24)] public float InnerRadiusMeters;
        [FieldOffset(28)] public float OuterRadiusMeters;
        [FieldOffset(32)] public uint BiomeHash;
        [FieldOffset(36)] public uint SectorHash;
        [FieldOffset(40)] public int SectorX;
        [FieldOffset(44)] public int SectorZ;
        [FieldOffset(48)] public int StateIndex;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.InfluenceStrideBytes)]
    public struct BiomeInfluenceDTO
    {
        [FieldOffset(0)] public uint4 BiomeHashes;
        [FieldOffset(16)] public float4 InfluenceWeights;
        [FieldOffset(32)] public float4 DistanceMeters;
        [FieldOffset(48)] public int4 StateIndices;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.CurrentAtmosphereStrideBytes)]
    public struct CurrentAtmosphereDTO
    {
        [FieldOffset(0)] public float4 FogColor;
        [FieldOffset(16)] public float4 AbsorptionParams;
        [FieldOffset(32)] public float4 AudioParams;
        [FieldOffset(48)] public float4 NormalizedWeights;
        [FieldOffset(64)] public BiomeInfluenceDTO Influence;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.BlendMaskStrideBytes)]
    public struct BiomeBlendMaskDTO
    {
        [FieldOffset(0)] public uint4 BiomeHashes;
        [FieldOffset(16)] public float4 Weights;
        [FieldOffset(32)] public float4 DitherParams;
        [FieldOffset(48)] public uint DominantBiomeHash;
        [FieldOffset(52)] public uint FrameIndex;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.ShaderPayloadStrideBytes)]
    public struct BiomeTransitionShaderPayloadCBufferDTO
    {
        [FieldOffset(0)] public float4 FogColor;
        [FieldOffset(16)] public float4 AbsorptionParams;
        [FieldOffset(32)] public float4 AudioParams;
        [FieldOffset(48)] public float4 NormalizedWeights;
        [FieldOffset(64)] public float4 BiomeHashes;
        [FieldOffset(80)] public float4 DitherParams;
        [FieldOffset(96)] public float4 FrameFlags;
        [FieldOffset(112)] public float4 Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.BiomeLightingParametersStrideBytes)]
    public struct BiomeLightingParametersDTO
    {
        [FieldOffset(0)] public float4 PrimaryFogColor;
        [FieldOffset(16)] public float4 SecondaryFogColor;
        [FieldOffset(32)] public float FogDensity;
        [FieldOffset(36)] public float BlendFactor;
        [FieldOffset(40)] public float LightShaftIntensity;
        [FieldOffset(44)] public float _pad0;
        [FieldOffset(48)] public float _pad1;
        [FieldOffset(52)] public float _pad2;
        [FieldOffset(56)] public float _pad3;
        [FieldOffset(60)] public float _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct BiomeAcousticStageDTO
    {
        [FieldOffset(0)] public float4 MixParams;
        [FieldOffset(16)] public uint DominantBiomeHash;
        [FieldOffset(20)] public uint PreviousBiomeHash;
        [FieldOffset(24)] public uint FrameIndex;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float4 Reserved0;
        [FieldOffset(48)] public float4 Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.TelemetryStrideBytes)]
    public struct BiomeTransitionTelemetryEntry
    {
        [FieldOffset(0)] public AbsoluteUniversePosition PlayerAup;
        [FieldOffset(48)] public uint DominantBiomeHash;
        [FieldOffset(52)] public int BlendedBiomeCount;
        [FieldOffset(56)] public float CpuMicroseconds;
        [FieldOffset(60)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.CounterStrideBytes)]
    public struct BiomeTransitionCounterDTO
    {
        [FieldOffset(0)] public int ActiveBiomeCount;
        [FieldOffset(4)] public int TelemetryCursor;
        [FieldOffset(8)] public uint PreviousDominantBiomeHash;
        [FieldOffset(12)] public uint CurrentDominantBiomeHash;
        [FieldOffset(16)] public int LastBlendCount;
        [FieldOffset(20)] public uint LastFlags;
        [FieldOffset(24)] public uint LastFrameIndex;
        [FieldOffset(28)] public float LastCpuMicroseconds;
        [FieldOffset(32)] public float LastQualityWeight;
        [FieldOffset(36)] public float LastWeightSum;
        [FieldOffset(40)] public uint LastStateHash;
        [FieldOffset(44)] public uint CsvVersion;
        [FieldOffset(48)] public float4 _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = BiomeTransitionConstants.TuningStrideBytes)]
    public struct BiomeTransitionTuningDTO
    {
        [FieldOffset(0)] public float RadiusScale;
        [FieldOffset(4)] public float HardwareQualityOverride;
        [FieldOffset(8)] public float LowCadenceHz;
        [FieldOffset(12)] public float UltraCadenceHz;
        [FieldOffset(16)] public float DitherStrength;
        [FieldOffset(20)] public float DebugDrawEnabled;
        [FieldOffset(24)] public float MockTraversalEnabled;
        [FieldOffset(28)] public float MaxCenterScanScale;
        [FieldOffset(32)] public float4 Reserved0;
        [FieldOffset(48)] public float4 Reserved1;
    }

    public static class BiomeTransitionMath
    {
        public static float Sanitize01(float value, float fallback = 1f)
        {
            return math.saturate(math.select(fallback, value, math.isfinite(value)));
        }

        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        public static double3 ToAbsoluteDouble3(in AbsoluteUniversePositionBlit128 position)
        {
            const double CellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                position.GridX * CellSizeMeters + position.Local.x,
                position.GridY * CellSizeMeters + position.Local.y,
                position.GridZ * CellSizeMeters + position.Local.z);
        }

        public static AbsoluteUniversePositionBlit128 ToBlit(double3 absolute)
        {
            const double CellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;
            long gridX = (long)math.floor(absolute.x / CellSizeMeters);
            long gridY = (long)math.floor(absolute.y / CellSizeMeters);
            long gridZ = (long)math.floor(absolute.z / CellSizeMeters);
            return new AbsoluteUniversePositionBlit128
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                Local = new float4(
                    (float)(absolute.x - gridX * CellSizeMeters),
                    (float)(absolute.y - gridY * CellSizeMeters),
                    (float)(absolute.z - gridZ * CellSizeMeters),
                    0f),
                Reserved = 0UL
            };
        }

        public static AbsoluteUniversePosition ToAup(in AbsoluteUniversePositionBlit128 blit)
        {
            return AbsoluteUniversePosition.FromAlignedBlit(in blit);
        }

        public static int2 ResolveSector(double3 absolute)
        {
            const double InvSectorSize = 1.0d / BiomeTransitionConstants.SectorSizeMeters;
            return new int2(
                (int)math.floor(absolute.x * InvSectorSize),
                (int)math.floor(absolute.z * InvSectorSize));
        }

        public static uint HashSector(int2 sector)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)sector.x) * 16777619u;
                hash = (hash ^ (uint)(sector.x >> 16)) * 16777619u;
                hash = (hash ^ (uint)sector.y) * 16777619u;
                hash = (hash ^ (uint)(sector.y >> 16)) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }

        public static uint HashLowerAscii(byte value, uint hash)
        {
            byte lower = value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
            return (hash ^ lower) * 16777619u;
        }

        public static uint HashState(in BiomeBlendMaskDTO mask, uint frameIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, mask.BiomeHashes.x);
                hash = Mix(hash, mask.BiomeHashes.y);
                hash = Mix(hash, mask.BiomeHashes.z);
                hash = Mix(hash, mask.BiomeHashes.w);
                hash = Mix(hash, (uint)math.asint(mask.Weights.x));
                hash = Mix(hash, (uint)math.asint(mask.Weights.y));
                hash = Mix(hash, (uint)math.asint(mask.Weights.z));
                hash = Mix(hash, (uint)math.asint(mask.Weights.w));
                return Mix(hash, frameIndex);
            }
        }

        private static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                hash = (hash ^ value) * 16777619u;
                return (hash ^ (value >> 16)) * 16777619u;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BiomeTransitionFogBlendJob : IJobParallelFor
    {
        [ReadOnly, NoAlias] public NativeArray<BiomeTransitionSample> Samples;
        [ReadOnly, NoAlias] public NativeArray<BiomeTransitionFogSource> FogSourcesByBiomeId;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> FromAup;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> ToAup;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> PlayerAup;
        [WriteOnly, NoAlias] public NativeArray<BiomeTransitionFogResult> Results;
        public float TransitionLengthMeters;

        public void Execute(int index)
        {
            BiomeTransitionSample sample = Samples[index];
            BiomeTransitionFogSource from = ResolveSource(sample.FromBiomeId);
            BiomeTransitionFogSource to = ResolveSource(sample.ToBiomeId);
            float blend = ResolveAupBlend(index, sample.Blend255 * (1f / 255f));
            float smoothBlend = BiomeTransitionMath.Smooth01(blend);

            Results[index] = new BiomeTransitionFogResult
            {
                Sample = new BiomeTransitionSample
                {
                    FromBiomeId = sample.FromBiomeId,
                    ToBiomeId = sample.ToBiomeId,
                    Blend255 = (byte)math.round(math.saturate(smoothBlend) * 255f),
                    Flags = sample.Flags
                },
                FogColor = math.lerp(from.FogColor, to.FogColor, smoothBlend),
                Density = math.lerp(from.Density, to.Density, smoothBlend),
                Turbidity = math.lerp(from.Turbidity, to.Turbidity, smoothBlend),
                Absorption = math.lerp(from.Absorption, to.Absorption, smoothBlend),
                FogAttenuationDistance = math.max(
                    0.001f,
                    math.lerp(from.FogAttenuationDistance, to.FogAttenuationDistance, smoothBlend)),
                NormalizedWeightSum = 1f
            };
        }

        private BiomeTransitionFogSource ResolveSource(byte biomeId)
        {
            if (!FogSourcesByBiomeId.IsCreated || FogSourcesByBiomeId.Length == 0)
                return default;

            int index = math.clamp((int)biomeId, 0, FogSourcesByBiomeId.Length - 1);
            return FogSourcesByBiomeId[index];
        }

        private float ResolveAupBlend(int index, float fallbackBlend)
        {
            if (!FromAup.IsCreated ||
                !ToAup.IsCreated ||
                !PlayerAup.IsCreated ||
                index >= FromAup.Length ||
                index >= ToAup.Length ||
                index >= PlayerAup.Length)
            {
                return math.saturate(fallbackBlend);
            }

            AbsoluteUniversePositionBlit128 fromAup = FromAup[index];
            AbsoluteUniversePositionBlit128 toAup = ToAup[index];
            AbsoluteUniversePositionBlit128 playerAup = PlayerAup[index];
            double3 from = BiomeTransitionMath.ToAbsoluteDouble3(in fromAup);
            double3 to = BiomeTransitionMath.ToAbsoluteDouble3(in toAup);
            double3 player = BiomeTransitionMath.ToAbsoluteDouble3(in playerAup);
            float3 segment = (float3)(to - from);
            float3 playerFrom = (float3)(player - from);
            float lengthSq = math.lengthsq(segment);
            if (lengthSq <= BiomeTransitionConstants.NaNEpsilon)
                return math.saturate(fallbackBlend);

            float projected = math.dot(playerFrom, segment) * math.rcp(math.max(lengthSq, BiomeTransitionConstants.NaNEpsilon));
            float segmentBlend = math.saturate(projected);
            float transitionLength = math.max(0.001f, TransitionLengthMeters);
            float transitionLengthSq = transitionLength * transitionLength;
            float halfWindow = math.saturate(transitionLengthSq * math.rcp(math.max(lengthSq, BiomeTransitionConstants.NaNEpsilon))) * 0.5f;
            float lower = math.max(0f, 0.5f - halfWindow);
            float upper = math.min(1f, 0.5f + halfWindow);
            float remapped = math.saturate((segmentBlend - lower) * math.rcp(math.max(0.001f, upper - lower)));
            return math.max(remapped, math.saturate(fallbackBlend));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BuildEmergencyMockBiomesJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<BiomeStateDTO> States;
        [WriteOnly, NoAlias] public NativeArray<BiomeCenterDTO> Centers;
        [NoAlias] public NativeArray<BiomeTransitionCounterDTO> Counters;
        public double3 OriginAup;
        public byte OnlyWhenCounterEmpty;

        public void Execute()
        {
            if (!States.IsCreated || !Centers.IsCreated || States.Length < 4 || Centers.Length < 4)
                return;

            if (OnlyWhenCounterEmpty != 0 &&
                Counters.IsCreated &&
                Counters.Length > 0 &&
                Counters[0].ActiveBiomeCount > 0)
            {
                return;
            }

            WriteBiome(0, 0xB55119E7u, OriginAup + new double3(-1200d, -25d, 0d), 320f, 1800f,
                new float4(0.07f, 0.18f, 0.19f, 1f),
                new float4(0.045f, 0.085f, 0.12f, 0.35f),
                0.45f);
            WriteBiome(1, 0x4B1D4C21u, OriginAup + new double3(450d, -70d, 550d), 420f, 2100f,
                new float4(0.035f, 0.12f, 0.10f, 1f),
                new float4(0.08f, 0.12f, 0.16f, 0.58f),
                0.62f);
            WriteBiome(2, 0xA8A7B055u, OriginAup + new double3(1500d, -900d, -450d), 520f, 2600f,
                new float4(0.006f, 0.014f, 0.022f, 1f),
                new float4(0.18f, 0.21f, 0.28f, 0.85f),
                0.78f);
            WriteBiome(3, 0x5F14A58Du, OriginAup + new double3(2850d, -1200d, 900d), 300f, 1900f,
                new float4(0.12f, 0.07f, 0.025f, 1f),
                new float4(0.25f, 0.18f, 0.08f, 0.92f),
                0.9f);

            if (Counters.IsCreated && Counters.Length > 0)
            {
                Counters[0] = new BiomeTransitionCounterDTO
                {
                    ActiveBiomeCount = 4,
                    LastFlags = BiomeTransitionConstants.FlagFallbackMock
                };
            }
        }

        private void WriteBiome(
            int index,
            uint hash,
            double3 center,
            float innerRadius,
            float outerRadius,
            float4 fogColor,
            float4 absorption,
            float ambientVolume)
        {
            States[index] = new BiomeStateDTO
            {
                BiomeHash = hash,
                AuthoringIndex = (uint)index,
                FogColor = fogColor,
                AbsorptionParams = absorption,
                AmbientAudioVolume = ambientVolume
            };

            int2 sector = BiomeTransitionMath.ResolveSector(center);
            Centers[index] = new BiomeCenterDTO
            {
                CenterAup = center,
                InnerRadiusMeters = math.max(0f, innerRadius),
                OuterRadiusMeters = math.max(innerRadius + 1f, outerRadius),
                BiomeHash = hash,
                SectorHash = BiomeTransitionMath.HashSector(sector),
                SectorX = sector.x,
                SectorZ = sector.y,
                StateIndex = index
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MockCameraTraversalJob : IJob
    {
        [WriteOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> OutputAup;
        public double3 StartAup;
        public double3 EndAup;
        public float Phase01;

        public void Execute()
        {
            if (!OutputAup.IsCreated || OutputAup.Length == 0)
                return;

            float t = BiomeTransitionMath.Smooth01(Phase01 - math.floor(Phase01));
            OutputAup[0] = BiomeTransitionMath.ToBlit(math.lerp(StartAup, EndAup, (double)t));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct EvaluateBiomeProximityJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<BiomeCenterDTO> Centers;
        [ReadOnly, NoAlias] public NativeArray<BiomeStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> MockPlayerAup;
        [WriteOnly, NoAlias] public NativeArray<BiomeInfluenceDTO> Influence;
        [NoAlias] public NativeArray<BiomeTransitionCounterDTO> Counters;
        [WriteOnly, NoAlias] public global::Hecton8.Core.MpscSignalRingBuffer<BiomeChangedSignal>.ParallelWriter BiomeChangedWriter;
        [NativeDisableParallelForRestriction] public NativeArray<int> BiomeChangedWriterBudget;
        public AbsoluteUniversePositionBlit128 PlayerAup;
        public float GlobalQualityWeight;
        public float RadiusScale;
        public float MaxCenterScanScale;
        public uint FrameIndex;
        public byte UseMockPlayerAup;

        public void Execute()
        {
            if (!Influence.IsCreated || Influence.Length == 0)
                return;

            BiomeInfluenceDTO influence = default;
            influence.DistanceMeters = new float4(float.MaxValue);
            influence.StateIndices = new int4(-1);

            uint flags = 0u;
            int activeCount = ResolveActiveCount();
            if (!Centers.IsCreated || activeCount <= 0)
                flags |= BiomeTransitionConstants.FlagMissingBiomeCenters;

            AbsoluteUniversePositionBlit128 resolvedPlayerAup = ResolvePlayerAup();
            double3 playerAbsolute = BiomeTransitionMath.ToAbsoluteDouble3(in resolvedPlayerAup);
            if (!math.all(math.isfinite(playerAbsolute)))
                flags |= BiomeTransitionConstants.FlagInvalidInput;

            float quality = BiomeTransitionMath.Sanitize01(GlobalQualityWeight);
            float qualityCurve = BiomeTransitionMath.Smooth01(quality);
            float scanScale = math.saturate(math.select(1f, MaxCenterScanScale, math.isfinite(MaxCenterScanScale)));
            float scaledActiveCount = math.lerp(1f, math.max(1f, activeCount * scanScale), qualityCurve);
            int scanCount = math.clamp((int)math.ceil(scaledActiveCount), 1, activeCount);
            int2 playerSector = BiomeTransitionMath.ResolveSector(playerAbsolute);
            int startIndex = ResolveStartIndex(activeCount, playerSector);
            float safeRadiusScale = math.max(0.0001f, math.select(1f, RadiusScale, math.isfinite(RadiusScale)));
            uint nearestHash = 0u;
            int nearestStateIndex = -1;
            float nearestDistance = float.MaxValue;

            for (int step = 0; step < scanCount; step++)
            {
                int i = startIndex + step;
                if (i >= activeCount)
                    i -= activeCount;

                BiomeCenterDTO center = Centers[i];
                if (center.BiomeHash == 0u)
                    continue;

                int stateIndex = ResolveStateIndex(in center);
                if (stateIndex < 0)
                {
                    flags |= BiomeTransitionConstants.FlagInvalidInput;
                    continue;
                }

                int dx = math.abs(center.SectorX - playerSector.x);
                int dz = math.abs(center.SectorZ - playerSector.y);
                float adjacentGate = math.step((float)math.max(dx, dz), 1.0001f);
                flags |= adjacentGate < 0.5f ? BiomeTransitionConstants.FlagSectorCulled : 0u;

                double3 localDeltaD = center.CenterAup - playerAbsolute;
                float3 localDelta = (float3)localDeltaD;
                float distanceSq = math.lengthsq(localDelta);
                if (!math.isfinite(distanceSq))
                    continue;

                distanceSq = math.max(distanceSq, BiomeTransitionConstants.NaNEpsilon);
                float distance = distanceSq * math.rsqrt(distanceSq);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestHash = center.BiomeHash;
                    nearestStateIndex = stateIndex;
                }

                float innerRadius = math.max(0f, center.InnerRadiusMeters * safeRadiusScale);
                float outerRadius = math.max(innerRadius + BiomeTransitionConstants.MinRadiusMeters, center.OuterRadiusMeters * safeRadiusScale);
                float band = math.max(BiomeTransitionConstants.NaNEpsilon, outerRadius - innerRadius);
                float falloff = math.saturate((outerRadius - distance) * math.rcp(band));
                float weight = BiomeTransitionMath.Smooth01(falloff) * adjacentGate;
                if (weight <= 0f || !math.isfinite(weight))
                    continue;

                InsertCandidate(ref influence, center.BiomeHash, stateIndex, weight, distance);
            }

            if (influence.BiomeHashes.x == 0u && nearestHash != 0u)
            {
                if (nearestStateIndex >= 0)
                {
                    InsertCandidate(ref influence, nearestHash, nearestStateIndex, 1f, nearestDistance);
                    flags |= BiomeTransitionConstants.FlagNearestFallback;
                }
                else
                {
                    flags |= BiomeTransitionConstants.FlagInvalidInput;
                }
            }

            Influence[0] = influence;
            uint dominant = influence.BiomeHashes.x;
            UpdateCounters(in influence, dominant, flags, quality, in resolvedPlayerAup);
        }

        private AbsoluteUniversePositionBlit128 ResolvePlayerAup()
        {
            if (UseMockPlayerAup == 0 || !MockPlayerAup.IsCreated || MockPlayerAup.Length == 0)
                return PlayerAup;

            return MockPlayerAup[0];
        }

        private int ResolveActiveCount()
        {
            int requested = Centers.IsCreated ? Centers.Length : 0;
            if (Counters.IsCreated && Counters.Length > 0 && Counters[0].ActiveBiomeCount > 0)
                requested = math.min(requested, Counters[0].ActiveBiomeCount);
            return math.clamp(requested, 0, Centers.IsCreated ? Centers.Length : 0);
        }

        private int ResolveStateIndex(in BiomeCenterDTO center)
        {
            if (!States.IsCreated || States.Length == 0)
                return -1;

            int stateIndex = center.StateIndex;
            if (stateIndex >= 0 && stateIndex < States.Length && States[stateIndex].BiomeHash == center.BiomeHash)
                return stateIndex;

            return FindStateIndex(center.BiomeHash);
        }

        private int ResolveStartIndex(int activeCount, int2 playerSector)
        {
            if (!Centers.IsCreated || activeCount <= 1)
                return 0;

            for (int i = 0; i < activeCount; i++)
            {
                BiomeCenterDTO center = Centers[i];
                int dx = math.abs(center.SectorX - playerSector.x);
                int dz = math.abs(center.SectorZ - playerSector.y);
                if (math.max(dx, dz) <= 1)
                    return i;
            }

            uint sectorHash = BiomeTransitionMath.HashSector(playerSector);
            return (int)(sectorHash % (uint)activeCount);
        }

        private int FindStateIndex(uint biomeHash)
        {
            if (!States.IsCreated)
                return -1;

            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].BiomeHash == biomeHash)
                    return i;
            }

            return -1;
        }

        private void UpdateCounters(
            in BiomeInfluenceDTO influence,
            uint dominant,
            uint flags,
            float quality,
            in AbsoluteUniversePositionBlit128 resolvedPlayerAup)
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            BiomeTransitionCounterDTO counter = Counters[0];
            uint previous = counter.CurrentDominantBiomeHash != 0u ? counter.CurrentDominantBiomeHash : counter.PreviousDominantBiomeHash;
            if (dominant != 0u && dominant != previous)
            {
                SignalBus<BiomeChangedSignal>.TryEnqueueBounded(BiomeChangedWriter, BiomeChangedWriterBudget, new BiomeChangedSignal
                {
                    PositionAup = BiomeTransitionMath.ToAup(in resolvedPlayerAup),
                    PreviousBiomeHash = previous,
                    CurrentBiomeHash = dominant,
                    PoiHash = 0u,
                    Frame = FrameIndex
                });
                flags |= BiomeTransitionConstants.FlagSignalEmitted;
            }

            counter.PreviousDominantBiomeHash = previous;
            counter.CurrentDominantBiomeHash = dominant;
            counter.LastFrameIndex = FrameIndex;
            counter.LastFlags = flags;
            counter.LastQualityWeight = quality;
            counter.LastBlendCount = ResolveBlendCount(quality);
            Counters[0] = counter;
        }

        private static int ResolveBlendCount(float quality)
        {
            float count = math.lerp(1f, 4f, BiomeTransitionMath.Smooth01(quality));
            return math.clamp((int)math.ceil(count - 0.0001f), 1, 4);
        }

        private static void InsertCandidate(ref BiomeInfluenceDTO influence, uint hash, int stateIndex, float weight, float distance)
        {
            if (weight > influence.InfluenceWeights.x)
            {
                influence.BiomeHashes.w = influence.BiomeHashes.z;
                influence.BiomeHashes.z = influence.BiomeHashes.y;
                influence.BiomeHashes.y = influence.BiomeHashes.x;
                influence.BiomeHashes.x = hash;
                influence.InfluenceWeights.w = influence.InfluenceWeights.z;
                influence.InfluenceWeights.z = influence.InfluenceWeights.y;
                influence.InfluenceWeights.y = influence.InfluenceWeights.x;
                influence.InfluenceWeights.x = weight;
                influence.DistanceMeters.w = influence.DistanceMeters.z;
                influence.DistanceMeters.z = influence.DistanceMeters.y;
                influence.DistanceMeters.y = influence.DistanceMeters.x;
                influence.DistanceMeters.x = distance;
                influence.StateIndices.w = influence.StateIndices.z;
                influence.StateIndices.z = influence.StateIndices.y;
                influence.StateIndices.y = influence.StateIndices.x;
                influence.StateIndices.x = stateIndex;
            }
            else if (weight > influence.InfluenceWeights.y)
            {
                influence.BiomeHashes.w = influence.BiomeHashes.z;
                influence.BiomeHashes.z = influence.BiomeHashes.y;
                influence.BiomeHashes.y = hash;
                influence.InfluenceWeights.w = influence.InfluenceWeights.z;
                influence.InfluenceWeights.z = influence.InfluenceWeights.y;
                influence.InfluenceWeights.y = weight;
                influence.DistanceMeters.w = influence.DistanceMeters.z;
                influence.DistanceMeters.z = influence.DistanceMeters.y;
                influence.DistanceMeters.y = distance;
                influence.StateIndices.w = influence.StateIndices.z;
                influence.StateIndices.z = influence.StateIndices.y;
                influence.StateIndices.y = stateIndex;
            }
            else if (weight > influence.InfluenceWeights.z)
            {
                influence.BiomeHashes.w = influence.BiomeHashes.z;
                influence.BiomeHashes.z = hash;
                influence.InfluenceWeights.w = influence.InfluenceWeights.z;
                influence.InfluenceWeights.z = weight;
                influence.DistanceMeters.w = influence.DistanceMeters.z;
                influence.DistanceMeters.z = distance;
                influence.StateIndices.w = influence.StateIndices.z;
                influence.StateIndices.z = stateIndex;
            }
            else if (weight > influence.InfluenceWeights.w)
            {
                influence.BiomeHashes.w = hash;
                influence.InfluenceWeights.w = weight;
                influence.DistanceMeters.w = distance;
                influence.StateIndices.w = stateIndex;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BlendAtmosphereJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<BiomeStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<BiomeInfluenceDTO> Influence;
        [WriteOnly, NoAlias] public NativeArray<CurrentAtmosphereDTO> CurrentAtmosphere;
        [WriteOnly, NoAlias] public NativeArray<BiomeBlendMaskDTO> BlendMask;
        [NoAlias] public NativeArray<BiomeTransitionCounterDTO> Counters;
        public float GlobalQualityWeight;
        public float DitherStrength;
        public uint FrameIndex;

        public void Execute()
        {
            if (!Influence.IsCreated || Influence.Length == 0 || !CurrentAtmosphere.IsCreated || CurrentAtmosphere.Length == 0)
                return;

            BiomeInfluenceDTO influence = Influence[0];
            float quality = BiomeTransitionMath.Sanitize01(GlobalQualityWeight);
            float maxBlendFloat = math.lerp(1f, 4f, BiomeTransitionMath.Smooth01(quality));
            float4 gates = math.saturate(maxBlendFloat - new float4(0f, 1f, 2f, 3f));
            float4 gatedWeights = influence.InfluenceWeights * gates;
            float weightSum = math.csum(gatedWeights);
            if (weightSum <= BiomeTransitionConstants.NaNEpsilon || !math.isfinite(weightSum))
            {
                gatedWeights = new float4(1f, 0f, 0f, 0f);
                weightSum = 1f;
            }

            float invWeightSum = math.rcp(math.max(weightSum, BiomeTransitionConstants.NaNEpsilon));
            float4 normalized = gatedWeights * invWeightSum;
            float4 fog = default;
            float4 absorption = default;
            float ambient = 0f;
            Accumulate(influence.StateIndices.x, influence.BiomeHashes.x, normalized.x, ref fog, ref absorption, ref ambient);
            Accumulate(influence.StateIndices.y, influence.BiomeHashes.y, normalized.y, ref fog, ref absorption, ref ambient);
            Accumulate(influence.StateIndices.z, influence.BiomeHashes.z, normalized.z, ref fog, ref absorption, ref ambient);
            Accumulate(influence.StateIndices.w, influence.BiomeHashes.w, normalized.w, ref fog, ref absorption, ref ambient);

            CurrentAtmosphereDTO current = new CurrentAtmosphereDTO
            {
                FogColor = SanitizeFloat4(fog, new float4(0.006f, 0.014f, 0.022f, 1f)),
                AbsorptionParams = SanitizeFloat4(absorption, new float4(0.18f, 0.21f, 0.28f, 0.85f)),
                AudioParams = new float4(math.saturate(ambient), weightSum, quality, maxBlendFloat),
                NormalizedWeights = normalized,
                Influence = influence
            };

            CurrentAtmosphere[0] = current;

            BiomeBlendMaskDTO mask = default;
            byte hasBlendMask = 0;
            if (BlendMask.IsCreated && BlendMask.Length > 0)
            {
                uint flags = Counters.IsCreated && Counters.Length > 0 ? Counters[0].LastFlags : 0u;
                mask = new BiomeBlendMaskDTO
                {
                    BiomeHashes = influence.BiomeHashes,
                    Weights = normalized,
                    DitherParams = new float4(
                        math.saturate(math.select(1f, DitherStrength, math.isfinite(DitherStrength))),
                        maxBlendFloat,
                        weightSum,
                        quality),
                    DominantBiomeHash = influence.BiomeHashes.x,
                    FrameIndex = FrameIndex,
                    Flags = flags
                };
                BlendMask[0] = mask;
                hasBlendMask = 1;
            }

            UpdateCounters(weightSum, normalized, influence.BiomeHashes.x, in mask, hasBlendMask);
        }

        private void Accumulate(int index, uint hash, float weight, ref float4 fog, ref float4 absorption, ref float ambient)
        {
            if (weight <= 0f || hash == 0u || !States.IsCreated || States.Length == 0)
                return;

            int resolved = index >= 0 && index < States.Length && States[index].BiomeHash == hash
                ? index
                : FindStateIndex(hash);
            if (resolved < 0)
                return;

            BiomeStateDTO state = States[resolved];
            fog += state.FogColor * weight;
            absorption += state.AbsorptionParams * weight;
            ambient += state.AmbientAudioVolume * weight;
        }

        private int FindStateIndex(uint hash)
        {
            for (int i = 0; i < States.Length; i++)
            {
                if (States[i].BiomeHash == hash)
                    return i;
            }

            return -1;
        }

        private void UpdateCounters(
            float weightSum,
            float4 normalized,
            uint dominant,
            in BiomeBlendMaskDTO mask,
            byte hasBlendMask)
        {
            if (!Counters.IsCreated || Counters.Length == 0)
                return;

            BiomeTransitionCounterDTO counter = Counters[0];
            counter.LastWeightSum = weightSum;
            counter.LastBlendCount = CountActiveWeights(normalized);
            counter.LastStateHash = hasBlendMask != 0
                ? BiomeTransitionMath.HashState(in mask, FrameIndex)
                : dominant;
            if (math.abs(math.csum(normalized) - 1f) > 0.001f)
                counter.LastFlags |= BiomeTransitionConstants.FlagNonFiniteOutput;
            Counters[0] = counter;
        }

        private static int CountActiveWeights(float4 weights)
        {
            return (int)math.csum(math.step(new float4(0.0001f), weights));
        }

        private static float4 SanitizeFloat4(float4 value, float4 fallback)
        {
            bool4 valid = math.isfinite(value);
            return math.select(fallback, value, valid);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct PublishAtmosphereDataJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<CurrentAtmosphereDTO> CurrentAtmosphere;
        [ReadOnly, NoAlias] public NativeArray<BiomeBlendMaskDTO> BlendMask;
        [WriteOnly, NoAlias] public NativeArray<float4> ShaderPayload;

        public unsafe void Execute()
        {
            if (!CurrentAtmosphere.IsCreated ||
                CurrentAtmosphere.Length == 0 ||
                !ShaderPayload.IsCreated ||
                ShaderPayload.Length < BiomeTransitionConstants.ShaderPayloadFloat4Count)
            {
                return;
            }

            CurrentAtmosphereDTO current = CurrentAtmosphere[0];
            BiomeBlendMaskDTO mask = BlendMask.IsCreated && BlendMask.Length > 0 ? BlendMask[0] : default;
            float4 slot0 = current.FogColor;
            float4 slot1 = current.AbsorptionParams;
            float4 slot2 = current.AudioParams;
            float4 slot3 = current.NormalizedWeights;
            float4 slot4 = new float4(
                math.asfloat(mask.BiomeHashes.x),
                math.asfloat(mask.BiomeHashes.y),
                math.asfloat(mask.BiomeHashes.z),
                math.asfloat(mask.BiomeHashes.w));
            float4 slot5 = mask.DitherParams;
            float4 slot6 = new float4(
                math.asfloat(mask.DominantBiomeHash),
                math.asfloat(mask.FrameIndex),
                math.asfloat(mask.Flags),
                0f);
            float4 slot7 = default;
            void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(ShaderPayload);
            int stride = UnsafeUtility.SizeOf<float4>();
            UnsafeUtility.MemCpy(dst, &slot0, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride, &slot1, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride * 2, &slot2, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride * 3, &slot3, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride * 4, &slot4, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride * 5, &slot5, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride * 6, &slot6, stride);
            UnsafeUtility.MemCpy((byte*)dst + stride * 7, &slot7, stride);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct StageAcousticParametersJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<CurrentAtmosphereDTO> CurrentAtmosphere;
        [ReadOnly, NoAlias] public NativeArray<BiomeBlendMaskDTO> BlendMask;
        [WriteOnly, NoAlias] public NativeArray<BiomeAcousticStageDTO> AcousticStage;
        [NoAlias] public NativeArray<BiomeTransitionCounterDTO> Counters;
        public uint FrameIndex;

        public void Execute()
        {
            if (!CurrentAtmosphere.IsCreated || CurrentAtmosphere.Length == 0 || !AcousticStage.IsCreated || AcousticStage.Length == 0)
                return;

            CurrentAtmosphereDTO current = CurrentAtmosphere[0];
            BiomeBlendMaskDTO mask = BlendMask.IsCreated && BlendMask.Length > 0 ? BlendMask[0] : default;
            uint previous = Counters.IsCreated && Counters.Length > 0 ? Counters[0].PreviousDominantBiomeHash : 0u;
            AcousticStage[0] = new BiomeAcousticStageDTO
            {
                MixParams = new float4(current.AudioParams.x, current.AudioParams.y, current.AudioParams.z, math.saturate(mask.DitherParams.y * 0.25f)),
                DominantBiomeHash = mask.DominantBiomeHash,
                PreviousBiomeHash = previous,
                FrameIndex = FrameIndex,
                Flags = mask.Flags
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct RecordBiomeTransitionTelemetryJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<BiomeBlendMaskDTO> BlendMask;
        [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> MockPlayerAup;
        [WriteOnly, NoAlias] public NativeArray<BiomeTransitionTelemetryEntry> TelemetryRing;
        [NoAlias] public NativeArray<BiomeTransitionCounterDTO> Counters;
        public AbsoluteUniversePositionBlit128 PlayerAup;
        public float CpuMicroseconds;
        public byte UseMockPlayerAup;

        public void Execute()
        {
            if (!TelemetryRing.IsCreated || TelemetryRing.Length == 0 || !Counters.IsCreated || Counters.Length == 0)
                return;

            BiomeTransitionCounterDTO counter = Counters[0];
            int cursor = math.clamp(counter.TelemetryCursor, 0, TelemetryRing.Length - 1);
            BiomeBlendMaskDTO mask = BlendMask.IsCreated && BlendMask.Length > 0 ? BlendMask[0] : default;
            uint stateHash = BiomeTransitionMath.HashState(in mask, counter.LastFrameIndex);
            AbsoluteUniversePositionBlit128 resolvedPlayerAup = UseMockPlayerAup != 0 && MockPlayerAup.IsCreated && MockPlayerAup.Length > 0
                ? MockPlayerAup[0]
                : PlayerAup;
            TelemetryRing[cursor] = new BiomeTransitionTelemetryEntry
            {
                PlayerAup = BiomeTransitionMath.ToAup(in resolvedPlayerAup),
                DominantBiomeHash = counter.CurrentDominantBiomeHash,
                BlendedBiomeCount = counter.LastBlendCount,
                CpuMicroseconds = math.max(0f, CpuMicroseconds),
                StateHash = stateHash
            };

            counter.TelemetryCursor = cursor + 1 >= TelemetryRing.Length ? 0 : cursor + 1;
            counter.LastCpuMicroseconds = math.max(0f, CpuMicroseconds);
            counter.LastStateHash = stateHash;
            Counters[0] = counter;
        }
    }

#if UNITY_EDITOR
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct BiomeAtmosphereCsvIngestJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<byte> CsvBytes;
        [WriteOnly, NoAlias] public NativeArray<BiomeStateDTO> States;
        [WriteOnly, NoAlias] public NativeArray<BiomeCenterDTO> Centers;
        [WriteOnly, NoAlias] public NativeArray<BiomeTransitionCounterDTO> Counters;
        public int ByteLength;

        public void Execute()
        {
            if (!CsvBytes.IsCreated || !States.IsCreated || !Centers.IsCreated)
                return;

            if (Counters.IsCreated && Counters.Length > 0)
                Counters[0] = default;

            int pos = 0;
            int length = math.clamp(ByteLength, 0, CsvBytes.Length);
            int written = 0;
            while (pos < length && written < States.Length && written < Centers.Length)
            {
                SkipLineWhitespace(ref pos, length);
                if (pos >= length)
                    break;

                byte first = CsvBytes[pos];
                if (first == (byte)'#' || first == (byte)'\n' || first == (byte)'\r')
                {
                    SkipLine(ref pos, length);
                    continue;
                }

                uint biomeHash = ReadNameHash(ref pos, length);
                bool valid = biomeHash != 0u;
                valid &= ReadFloatCell(ref pos, length, out float centerX);
                valid &= ReadFloatCell(ref pos, length, out float centerY);
                valid &= ReadFloatCell(ref pos, length, out float centerZ);
                valid &= ReadFloatCell(ref pos, length, out float innerRadius);
                valid &= ReadFloatCell(ref pos, length, out float outerRadius);
                valid &= ReadFloatCell(ref pos, length, out float fogR);
                valid &= ReadFloatCell(ref pos, length, out float fogG);
                valid &= ReadFloatCell(ref pos, length, out float fogB);
                valid &= ReadFloatCell(ref pos, length, out float fogA);
                valid &= ReadFloatCell(ref pos, length, out float absorptionX);
                valid &= ReadFloatCell(ref pos, length, out float absorptionY);
                valid &= ReadFloatCell(ref pos, length, out float absorptionZ);
                valid &= ReadFloatCell(ref pos, length, out float absorptionW);
                valid &= ReadFloatCell(ref pos, length, out float ambientVolume);
                SkipLine(ref pos, length);

                if (!valid)
                    continue;

                double3 center = new double3(centerX, centerY, centerZ);
                int2 sector = BiomeTransitionMath.ResolveSector(center);
                States[written] = new BiomeStateDTO
                {
                    BiomeHash = biomeHash,
                    AuthoringIndex = (uint)written,
                    Flags = BiomeTransitionConstants.FlagCsvLoaded,
                    FogColor = new float4(fogR, fogG, fogB, fogA),
                    AbsorptionParams = new float4(absorptionX, absorptionY, absorptionZ, absorptionW),
                    AmbientAudioVolume = math.saturate(ambientVolume)
                };
                Centers[written] = new BiomeCenterDTO
                {
                    CenterAup = center,
                    InnerRadiusMeters = math.max(0f, innerRadius),
                    OuterRadiusMeters = math.max(innerRadius + 1f, outerRadius),
                    BiomeHash = biomeHash,
                    SectorHash = BiomeTransitionMath.HashSector(sector),
                    SectorX = sector.x,
                    SectorZ = sector.y,
                    StateIndex = written
                };
                written++;
            }

            if (Counters.IsCreated && Counters.Length > 0 && written > 0)
            {
                Counters[0] = new BiomeTransitionCounterDTO
                {
                    ActiveBiomeCount = written,
                    LastFlags = BiomeTransitionConstants.FlagCsvLoaded,
                    CsvVersion = 1u
                };
            }
        }

        private uint ReadNameHash(ref int pos, int length)
        {
            uint hash = 2166136261u;
            bool hasBytes = false;
            while (pos < length)
            {
                byte b = CsvBytes[pos++];
                if (b == (byte)',')
                    break;

                if (b <= 32)
                    continue;

                hash = BiomeTransitionMath.HashLowerAscii(b, hash);
                hasBytes = true;
            }

            return hasBytes ? hash : 0u;
        }

        private bool ReadFloatCell(ref int pos, int length, out float value)
        {
            value = 0f;
            while (pos < length && CsvBytes[pos] <= 32 && CsvBytes[pos] != (byte)'\n' && CsvBytes[pos] != (byte)'\r')
                pos++;

            bool negative = false;
            if (pos < length && CsvBytes[pos] == (byte)'-')
            {
                negative = true;
                pos++;
            }

            double whole = 0d;
            bool hasDigit = false;
            while (pos < length)
            {
                byte b = CsvBytes[pos];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                whole = whole * 10d + (b - (byte)'0');
                hasDigit = true;
                pos++;
            }

            double fraction = 0d;
            double scale = 1d;
            if (pos < length && CsvBytes[pos] == (byte)'.')
            {
                pos++;
                while (pos < length)
                {
                    byte b = CsvBytes[pos];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    scale *= 10d;
                    fraction += (b - (byte)'0') / scale;
                    hasDigit = true;
                    pos++;
                }
            }

            while (pos < length)
            {
                byte b = CsvBytes[pos++];
                if (b == (byte)',' || b == (byte)'\n')
                    break;

                if (b == (byte)'\r')
                {
                    if (pos < length && CsvBytes[pos] == (byte)'\n')
                        pos++;
                    break;
                }
            }

            if (!hasDigit)
                return false;

            double parsed = whole + fraction;
            value = (float)(negative ? -parsed : parsed);
            return math.isfinite(value);
        }

        private void SkipLineWhitespace(ref int pos, int length)
        {
            while (pos < length)
            {
                byte b = CsvBytes[pos];
                if (b == (byte)' ' || b == (byte)'\t')
                {
                    pos++;
                    continue;
                }

                break;
            }
        }

        private void SkipLine(ref int pos, int length)
        {
            while (pos < length)
            {
                byte b = CsvBytes[pos++];
                if (b == (byte)'\n')
                    break;
            }
        }
    }
#endif
}
