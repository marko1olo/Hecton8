using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Atmosphere
{
    public static class ShinobuStormPropagationConstants
    {
        public const int TelemetryFrameCount = 300;
        public const int StormPropagationStrideBytes = 32;
        public const int WriteSnapshotStrideBytes = 96;
        public const int TuningStrideBytes = 64;
        public const int TelemetryEntryStrideBytes = 64;
        public const int ImpactProfileStrideBytes = 32;
        public const int ImpactProfileCapacity = 16;
#if UNITY_EDITOR
        public const int CsvScratchBytes = 16 * 1024;
#endif
        public const int DumpScratchBytes = 32 + (TelemetryFrameCount * TelemetryEntryStrideBytes);
        public const float DefaultDecayConstant = 0.00185f;
        public const float DefaultTurbidityGain = 1.85f;
        public const float DefaultSurgeScale = 0.095f;
        public const float DefaultAcousticMufflingGain = 0.72f;
        public const float DefaultBiolumDeltaGain = 1.35f;
        public const float DefaultNoiseScale = 0.65f;
        public const float DefaultMaxDepthMeters = 6200f;
        public const float MinimumPublicationCadenceHz = 5f;
        public const float DefaultPublicationCadenceHz = 30f;
        public const float DefaultBaseFogDensity = 0.045f;
        public const float DefaultExtinctionCoefficient = 0.12f;
        public const float DefaultMaxTurbidityScalar = 3.2f;
        public const float DefaultFlowAdvectionGain = 2.25f;
        public const uint SourceHash = 0x53483234u; // SH24
        public const uint ProfileFallbackHash = 0x53504642u; // SPFB
        public const uint ProfileGaleHash = 0x264BE98Au;
        public const uint ProfileHurricaneHash = 0x9B45E804u;
        public const uint ProfileAbyssalHurricaneHash = 0x42174E62u;
        public const uint WeatherMaskStorm = 1u << 1;
        public const uint WeatherMaskThermocline = 1u << 3;
        public const uint WeatherMaskHalocline = 1u << 4;
        public const uint DumpMagic = 0x53504450u; // SPDP
        public const uint TelemetryFlagNonFinite = 1u;
        public const uint TelemetryFlagMockWeather = 2u;
        public const uint TelemetryFlagFogPublished = 4u;
        public const uint TelemetryFlagBiolumPublished = 8u;
        public const uint TelemetryFlagAudioPublished = 16u;
        public const uint TelemetryFlagFlowPublished = 32u;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuStormPropagationConstants.StormPropagationStrideBytes)]
    public struct StormPropagationDTO
    {
        [FieldOffset(0)] public float3 SurgeVector;
        [FieldOffset(12)] public float TurbidityScalar;
        [FieldOffset(16)] public float AcousticMuffling;
        [FieldOffset(20)] public float BioluminescenceStimulus;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuStormPropagationConstants.WriteSnapshotStrideBytes)]
    public struct StormPropagationWriteSnapshotDTO
    {
        [FieldOffset(0)] public StormPropagationDTO State;
        [FieldOffset(32)] public float4 FlowScalar;
        [FieldOffset(48)] public float4 AudioScalar;
        [FieldOffset(64)] public float4 BiolumScalar;
        [FieldOffset(80)] public float4 FogScalar;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuStormPropagationConstants.TuningStrideBytes)]
    public struct StormPropagationTuningDTO
    {
        [FieldOffset(0)] public float DecayConstant;
        [FieldOffset(4)] public float TurbidityGain;
        [FieldOffset(8)] public float SurgeScale;
        [FieldOffset(12)] public float AcousticMufflingGain;
        [FieldOffset(16)] public float BiolumDeltaGain;
        [FieldOffset(20)] public float NoiseScale;
        [FieldOffset(24)] public float MinimumLowPassHertz;
        [FieldOffset(28)] public float MaxDepthMeters;
        [FieldOffset(32)] public float4 FogBaseDensityExtinction;
        [FieldOffset(48)] public float GlobalQualityWeight;
        [FieldOffset(52)] public float PublicationCadenceHz;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint ProfileHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuStormPropagationConstants.ImpactProfileStrideBytes)]
    public struct StormDepthImpactProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float MinDepthMeters;
        [FieldOffset(8)] public float MaxDepthMeters;
        [FieldOffset(12)] public float DecayConstant;
        [FieldOffset(16)] public float TurbidityGain;
        [FieldOffset(20)] public float SurgeScale;
        [FieldOffset(24)] public float AcousticGain;
        [FieldOffset(28)] public float BiolumGain;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MockHurricaneStateDTO
    {
        [FieldOffset(0)] public float2 DirectionXZ;
        [FieldOffset(8)] public float WindSpeedMetersPerSecond;
        [FieldOffset(12)] public float StormIntensity01;
        [FieldOffset(16)] public float RainIntensity01;
        [FieldOffset(20)] public float SurfaceSurge01;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Seed;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuStormPropagationConstants.TelemetryEntryStrideBytes)]
    public struct StormPropagationTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float SurfaceIntensity01;
        [FieldOffset(12)] public float DepthMeters;
        [FieldOffset(16)] public float AttenuatedEnergy01;
        [FieldOffset(20)] public float TurbidityScalar;
        [FieldOffset(24)] public float AcousticMuffling01;
        [FieldOffset(28)] public float BiolumStimulus01;
        [FieldOffset(32)] public float3 SurgeVector;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float ScheduleToPublishMicroseconds;
        [FieldOffset(52)] public float PreviousSurfaceIntensity01;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public int NoiseOctaveCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct StormPropagationDumpHeader
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint ReasonFlags;
        [FieldOffset(8)] public int WriteCursor;
        [FieldOffset(12)] public int EntryCount;
        [FieldOffset(16)] public int EntryStrideBytes;
        [FieldOffset(20)] public uint SourceHash;
        [FieldOffset(24)] public uint StateHash;
        [FieldOffset(28)] public uint Reserved;
    }

    public static class ShinobuStormPropagationNative
    {
        public static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<StormPropagationDTO>() == ShinobuStormPropagationConstants.StormPropagationStrideBytes &&
                   OffsetOf<StormPropagationDTO>(nameof(StormPropagationDTO.SurgeVector)) == 0 &&
                   OffsetOf<StormPropagationDTO>(nameof(StormPropagationDTO.TurbidityScalar)) == 12 &&
                   OffsetOf<StormPropagationDTO>(nameof(StormPropagationDTO.AcousticMuffling)) == 16 &&
                   OffsetOf<StormPropagationDTO>(nameof(StormPropagationDTO.BioluminescenceStimulus)) == 20 &&
                   OffsetOf<StormPropagationDTO>(nameof(StormPropagationDTO._pad0)) == 24 &&
                   OffsetOf<StormPropagationDTO>(nameof(StormPropagationDTO._pad7)) == 31 &&
                   UnsafeUtility.SizeOf<StormPropagationWriteSnapshotDTO>() == ShinobuStormPropagationConstants.WriteSnapshotStrideBytes &&
                   OffsetOf<StormPropagationWriteSnapshotDTO>(nameof(StormPropagationWriteSnapshotDTO.State)) == 0 &&
                   OffsetOf<StormPropagationWriteSnapshotDTO>(nameof(StormPropagationWriteSnapshotDTO.FlowScalar)) == 32 &&
                   OffsetOf<StormPropagationWriteSnapshotDTO>(nameof(StormPropagationWriteSnapshotDTO.AudioScalar)) == 48 &&
                   OffsetOf<StormPropagationWriteSnapshotDTO>(nameof(StormPropagationWriteSnapshotDTO.BiolumScalar)) == 64 &&
                   OffsetOf<StormPropagationWriteSnapshotDTO>(nameof(StormPropagationWriteSnapshotDTO.FogScalar)) == 80 &&
                   UnsafeUtility.SizeOf<StormPropagationTuningDTO>() == ShinobuStormPropagationConstants.TuningStrideBytes &&
                   OffsetOf<StormPropagationTuningDTO>(nameof(StormPropagationTuningDTO.DecayConstant)) == 0 &&
                   OffsetOf<StormPropagationTuningDTO>(nameof(StormPropagationTuningDTO.FogBaseDensityExtinction)) == 32 &&
                   OffsetOf<StormPropagationTuningDTO>(nameof(StormPropagationTuningDTO.ProfileHash)) == 60 &&
                   UnsafeUtility.SizeOf<StormDepthImpactProfileDTO>() == ShinobuStormPropagationConstants.ImpactProfileStrideBytes &&
                   OffsetOf<StormDepthImpactProfileDTO>(nameof(StormDepthImpactProfileDTO.BiolumGain)) == 28 &&
                   UnsafeUtility.SizeOf<MockHurricaneStateDTO>() == 32 &&
                   OffsetOf<MockHurricaneStateDTO>(nameof(MockHurricaneStateDTO.DirectionXZ)) == 0 &&
                   OffsetOf<MockHurricaneStateDTO>(nameof(MockHurricaneStateDTO.Seed)) == 28 &&
                   UnsafeUtility.SizeOf<StormPropagationTelemetryEntry>() == ShinobuStormPropagationConstants.TelemetryEntryStrideBytes &&
                   OffsetOf<StormPropagationTelemetryEntry>(nameof(StormPropagationTelemetryEntry.SurgeVector)) == 32 &&
                   OffsetOf<StormPropagationTelemetryEntry>(nameof(StormPropagationTelemetryEntry.NoiseOctaveCount)) == 60 &&
                   UnsafeUtility.SizeOf<StormPropagationDumpHeader>() == 32 &&
                   OffsetOf<StormPropagationDumpHeader>(nameof(StormPropagationDumpHeader.StateHash)) == 24 &&
                   OffsetOf<StormPropagationDumpHeader>(nameof(StormPropagationDumpHeader.Reserved)) == 28;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T ElementAt<T>(NativeArray<T> values, int index)
            where T : unmanaged
        {
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(values);
            return ref UnsafeUtility.AsRef<T>(basePtr + (index * UnsafeUtility.SizeOf<T>()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T ReadElement<T>(NativeArray<T> values, int index)
            where T : unmanaged
        {
            byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(values);
            return UnsafeUtility.AsRef<T>(basePtr + (index * UnsafeUtility.SizeOf<T>()));
        }

        public static StormPropagationTuningDTO CreateDefaultTuning(float globalQualityWeight)
        {
            float quality = ShinobuStormPropagationMath.Sanitize01(globalQualityWeight);
            return new StormPropagationTuningDTO
            {
                DecayConstant = ShinobuStormPropagationConstants.DefaultDecayConstant,
                TurbidityGain = ShinobuStormPropagationConstants.DefaultTurbidityGain,
                SurgeScale = ShinobuStormPropagationConstants.DefaultSurgeScale,
                AcousticMufflingGain = ShinobuStormPropagationConstants.DefaultAcousticMufflingGain,
                BiolumDeltaGain = ShinobuStormPropagationConstants.DefaultBiolumDeltaGain,
                NoiseScale = ShinobuStormPropagationConstants.DefaultNoiseScale,
                MinimumLowPassHertz = 820f,
                MaxDepthMeters = ShinobuStormPropagationConstants.DefaultMaxDepthMeters,
                FogBaseDensityExtinction = new float4(
                    ShinobuStormPropagationConstants.DefaultBaseFogDensity,
                    ShinobuStormPropagationConstants.DefaultExtinctionCoefficient,
                    ShinobuStormPropagationConstants.DefaultMaxTurbidityScalar,
                    ShinobuStormPropagationConstants.DefaultFlowAdvectionGain),
                GlobalQualityWeight = quality,
                PublicationCadenceHz = ShinobuStormPropagationConstants.DefaultPublicationCadenceHz,
                Flags = 0u,
                ProfileHash = ShinobuStormPropagationConstants.ProfileFallbackHash
            };
        }

        public static StormPropagationTuningDTO SanitizeTuning(StormPropagationTuningDTO tuning, float fallbackQuality)
        {
            StormPropagationTuningDTO defaults = CreateDefaultTuning(fallbackQuality);
            if (!math.isfinite(tuning.DecayConstant) || tuning.DecayConstant <= 0f)
                return defaults;

            tuning.DecayConstant = math.max(0.000001f, tuning.DecayConstant);
            tuning.TurbidityGain = math.isfinite(tuning.TurbidityGain) ? math.max(0f, tuning.TurbidityGain) : defaults.TurbidityGain;
            tuning.SurgeScale = math.isfinite(tuning.SurgeScale) ? math.max(0f, tuning.SurgeScale) : defaults.SurgeScale;
            tuning.AcousticMufflingGain = math.isfinite(tuning.AcousticMufflingGain) ? math.max(0f, tuning.AcousticMufflingGain) : defaults.AcousticMufflingGain;
            tuning.BiolumDeltaGain = math.isfinite(tuning.BiolumDeltaGain) ? math.max(0f, tuning.BiolumDeltaGain) : defaults.BiolumDeltaGain;
            tuning.NoiseScale = math.isfinite(tuning.NoiseScale) ? math.max(0f, tuning.NoiseScale) : defaults.NoiseScale;
            tuning.MinimumLowPassHertz = math.isfinite(tuning.MinimumLowPassHertz) ? math.max(40f, tuning.MinimumLowPassHertz) : defaults.MinimumLowPassHertz;
            tuning.MaxDepthMeters = math.isfinite(tuning.MaxDepthMeters) ? math.max(1f, tuning.MaxDepthMeters) : defaults.MaxDepthMeters;

            float4 fog = tuning.FogBaseDensityExtinction;
            tuning.FogBaseDensityExtinction = math.all(math.isfinite(fog))
                ? new float4(math.max(0f, fog.x), math.max(0f, fog.y), math.max(1f, fog.z), math.max(0f, fog.w))
                : defaults.FogBaseDensityExtinction;

            tuning.GlobalQualityWeight = math.isfinite(tuning.GlobalQualityWeight)
                ? ShinobuStormPropagationMath.Sanitize01(tuning.GlobalQualityWeight)
                : ShinobuStormPropagationMath.Sanitize01(fallbackQuality);
            tuning.PublicationCadenceHz = math.isfinite(tuning.PublicationCadenceHz) && tuning.PublicationCadenceHz > 0.001f
                ? math.clamp(tuning.PublicationCadenceHz, ShinobuStormPropagationConstants.MinimumPublicationCadenceHz, 60f)
                : defaults.PublicationCadenceHz;
            if (tuning.ProfileHash == 0u)
                tuning.ProfileHash = defaults.ProfileHash;

            return tuning;
        }

        public static StormDepthImpactProfileDTO CreateFallbackProfile()
        {
            return new StormDepthImpactProfileDTO
            {
                ProfileHash = ShinobuStormPropagationConstants.ProfileFallbackHash,
                MinDepthMeters = 0f,
                MaxDepthMeters = 20000f,
                DecayConstant = ShinobuStormPropagationConstants.DefaultDecayConstant,
                TurbidityGain = ShinobuStormPropagationConstants.DefaultTurbidityGain,
                SurgeScale = ShinobuStormPropagationConstants.DefaultSurgeScale,
                AcousticGain = ShinobuStormPropagationConstants.DefaultAcousticMufflingGain,
                BiolumGain = ShinobuStormPropagationConstants.DefaultBiolumDeltaGain
            };
        }

        private static int OffsetOf<T>(string fieldName)
            where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public static class ShinobuStormPropagationMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float value)
        {
            float t = Sanitize01(value);
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveDepthMeters(double3 sampleAup, double3 seaLevelAup)
        {
            double delta = seaLevelAup.y - sampleAup.y;
            if (!math.isfinite(delta))
                return 0f;

            return math.max(0f, (float)delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Attenuate(float intensity01, float depthMeters, float decayConstant)
        {
            float intensity = Sanitize01(intensity01);
            float depth = math.max(0f, math.isfinite(depthMeters) ? depthMeters : 0f);
            float decay = math.max(0.000001f, math.isfinite(decayConstant) ? decayConstant : ShinobuStormPropagationConstants.DefaultDecayConstant);
            return math.saturate(intensity * MathLodApproximation.ApproxExpNegPade33Wide40(depth * decay));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveNoiseOctaveCount(float globalQualityWeight)
        {
            float q = Sanitize01(globalQualityWeight);
            return math.clamp(1 + (int)math.round(Smooth01(q) * 2f), 1, 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WrapRingIndex(int cursor, int length)
        {
            if (length <= 0)
                return 0;

            int wrapped = cursor % length;
            return wrapped < 0 ? wrapped + length : wrapped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AdvanceRingCursor(int cursor, int length)
        {
            if (length <= 0)
                return 0;

            if (cursor == int.MaxValue)
                return 0;

            return WrapRingIndex(cursor + 1, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PreviousRingIndex(int cursor, int length)
        {
            if (length <= 0)
                return 0;

            int wrapped = WrapRingIndex(cursor, length);
            return wrapped == 0 ? length - 1 : wrapped - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveWeatherProfileWeight(uint profileHash, uint stateMask, float stormIntensity01)
        {
            float storm = Sanitize01(stormIntensity01);
            float maskStorm = ((stateMask & ShinobuStormPropagationConstants.WeatherMaskStorm) != 0u)
                ? 1f
                : math.smoothstep(0.18f, 0.42f, storm);
            float barrier = ((stateMask & (ShinobuStormPropagationConstants.WeatherMaskThermocline | ShinobuStormPropagationConstants.WeatherMaskHalocline)) != 0u)
                ? 1f
                : 0f;
            float gale = maskStorm * (1f - math.smoothstep(0.54f, 0.78f, storm));
            float hurricane = maskStorm * math.smoothstep(0.46f, 0.74f, storm);
            float abyssalHurricane = hurricane * barrier * math.smoothstep(0.58f, 0.86f, storm);

            if (profileHash == ShinobuStormPropagationConstants.ProfileGaleHash)
                return gale;
            if (profileHash == ShinobuStormPropagationConstants.ProfileHurricaneHash)
                return hurricane * (1f - (abyssalHurricane * 0.35f));
            if (profileHash == ShinobuStormPropagationConstants.ProfileAbyssalHurricaneHash)
                return abyssalHurricane;
            if (profileHash == ShinobuStormPropagationConstants.ProfileFallbackHash)
                return 1f - maskStorm;

            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveContinuousNoise(float2 xz, float timeSeconds, float globalQualityWeight)
        {
            float q = Sanitize01(globalQualityWeight);
            float w0 = 1f;
            float w1 = math.smoothstep(0.3f, 0.7f, q);
            float w2 = math.smoothstep(0.7f, 1f, q);
            float n0 = Wave01(xz, timeSeconds, 0.031f, 1.0f);
            float sum = n0 * w0;
            float weight = w0;
            if (w1 > 0f)
            {
                sum += Wave01(xz.yx + 17.37f, timeSeconds, 0.071f, 1.7f) * w1 * 0.55f;
                weight += w1 * 0.55f;
            }

            if (w2 > 0f)
            {
                sum += Wave01(xz * 1.93f - 41.2f, timeSeconds, 0.139f, 2.4f) * w2 * 0.28f;
                weight += w2 * 0.28f;
            }

            return sum * math.rcp(math.max(0.0001f, weight));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint HashState(float depthMeters, float energy01, float turbidity, float acoustic, float biolum)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(depthMeters));
            hash = Mix(hash, math.asuint(energy01));
            hash = Mix(hash, math.asuint(turbidity));
            hash = Mix(hash, math.asuint(acoustic));
            hash = Mix(hash, math.asuint(biolum));
            return hash != 0u ? hash : 1u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Wave01(float2 xz, float timeSeconds, float frequency, float speed)
        {
            float phase = (xz.x * 12.9898f) + (xz.y * 78.233f) + (timeSeconds * speed);
            return MathLodApproximation.ApproxSinBhaskara(phase * frequency) * 0.5f + 0.5f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    #if UNITY_EDITOR
    public static unsafe class StormDepthImpactCsvParser
    {
        public static bool TryParse(
            ReadOnlySpan<byte> bytes,
            NativeArray<StormDepthImpactProfileDTO> profiles,
            out int profileCount,
            out uint fileHash)
        {
            profileCount = 0;
            fileHash = HashBytes(bytes);
            if (!profiles.IsCreated || profiles.Length <= 0)
                return false;

            int cursor = 0;
            while (cursor < bytes.Length && profileCount < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, cursor - lineStart);
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (IsIgnorableLine(line))
                    continue;

                int tokenCursor = 0;
                ReadOnlySpan<byte> id = ReadToken(line, ref tokenCursor);
                if (id.Length <= 0 || IsHeaderToken(id))
                    continue;

                if (!TryParseFloat(ReadToken(line, ref tokenCursor), out float minDepth) ||
                    !TryParseFloat(ReadToken(line, ref tokenCursor), out float maxDepth) ||
                    !TryParseFloat(ReadToken(line, ref tokenCursor), out float decay) ||
                    !TryParseFloat(ReadToken(line, ref tokenCursor), out float turbidity) ||
                    !TryParseFloat(ReadToken(line, ref tokenCursor), out float surge) ||
                    !TryParseFloat(ReadToken(line, ref tokenCursor), out float acoustic) ||
                    !TryParseFloat(ReadToken(line, ref tokenCursor), out float biolum))
                {
                    continue;
                }

                ref StormDepthImpactProfileDTO profile = ref ShinobuStormPropagationNative.ElementAt(profiles, profileCount);
                profile = new StormDepthImpactProfileDTO
                {
                    ProfileHash = HashAsciiLower(id),
                    MinDepthMeters = math.max(0f, minDepth),
                    MaxDepthMeters = math.max(minDepth, maxDepth),
                    DecayConstant = math.max(0.000001f, decay),
                    TurbidityGain = math.max(0f, turbidity),
                    SurgeScale = math.max(0f, surge),
                    AcousticGain = math.max(0f, acoustic),
                    BiolumGain = math.max(0f, biolum)
                };
                profileCount++;
            }

            return profileCount > 0;
        }

        public static uint HashAsciiLower(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash != 0u ? hash : 1u;
        }

        private static uint HashBytes(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ bytes[i]) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        private static bool IsIgnorableLine(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)' ' || b == (byte)'\t')
                    continue;
                return b == (byte)'#' || b == (byte)'/';
            }

            return true;
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> token)
        {
            if (token.Length <= 0)
                return true;

            return EqualsAsciiLower(token, "profile") ||
                   EqualsAsciiLower(token, "profile_hash") ||
                   EqualsAsciiLower(token, "id") ||
                   EqualsAsciiLower(token, "name");
        }

        private static bool EqualsAsciiLower(ReadOnlySpan<byte> token, string text)
        {
            if (token.Length != text.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                byte b = token[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (b != (byte)text[i])
                    return false;
            }

            return true;
        }

        private static ReadOnlySpan<byte> ReadToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && (line[cursor] == (byte)' ' || line[cursor] == (byte)'\t'))
                cursor++;

            int start = cursor;
            while (cursor < line.Length && line[cursor] != (byte)',' && line[cursor] != (byte)';')
                cursor++;

            int end = cursor;
            if (cursor < line.Length)
                cursor++;

            while (end > start && (line[end - 1] == (byte)' ' || line[end - 1] == (byte)'\t'))
                end--;

            return line.Slice(start, end - start);
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length <= 0)
                return false;

            int cursor = 0;
            float sign = 1f;
            if (token[cursor] == (byte)'-' || token[cursor] == (byte)'+')
            {
                sign = token[cursor] == (byte)'-' ? -1f : 1f;
                cursor++;
            }

            double whole = 0d;
            bool any = false;
            while (cursor < token.Length)
            {
                byte b = token[cursor];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                whole = (whole * 10d) + (b - (byte)'0');
                any = true;
                cursor++;
            }

            double fraction = 0d;
            double place = 0.1d;
            if (cursor < token.Length && token[cursor] == (byte)'.')
            {
                cursor++;
                while (cursor < token.Length)
                {
                    byte b = token[cursor];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    fraction += (b - (byte)'0') * place;
                    place *= 0.1d;
                    any = true;
                    cursor++;
                }
            }

            if (!any)
                return false;

            int exponent = 0;
            if (cursor < token.Length && (token[cursor] == (byte)'e' || token[cursor] == (byte)'E'))
            {
                cursor++;
                int expSign = 1;
                if (cursor < token.Length && (token[cursor] == (byte)'-' || token[cursor] == (byte)'+'))
                {
                    expSign = token[cursor] == (byte)'-' ? -1 : 1;
                    cursor++;
                }

                int expValue = 0;
                bool expAny = false;
                while (cursor < token.Length)
                {
                    byte b = token[cursor];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    expValue = (expValue * 10) + (b - (byte)'0');
                    expAny = true;
                    cursor++;
                }

                if (!expAny)
                    return false;

                exponent = expValue * expSign;
            }

            while (cursor < token.Length && (token[cursor] == (byte)' ' || token[cursor] == (byte)'\t'))
                cursor++;

            if (cursor < token.Length)
                return false;

            double result = (whole + fraction) * sign;
            if (exponent != 0)
                result *= Pow10Int(exponent);

            if (double.IsNaN(result) || double.IsInfinity(result))
                return false;

            value = (float)result;
            return math.isfinite(value);
        }

        private static double Pow10Int(int exponent)
        {
            int count = math.min(38, math.abs(exponent));
            double scale = 1.0d;
            for (int i = 0; i < count; i++)
                scale *= 10.0d;
            return exponent < 0 ? 1.0d / scale : scale;
        }
    }
    #endif
}

#if UNITY_EDITOR
namespace Hecton8.Atmosphere.EditorValidation
{
    [InitializeOnLoad]
    internal static class ShinobuStormPropagationLayoutValidator
    {
        static ShinobuStormPropagationLayoutValidator()
        {
            if (!ShinobuStormPropagationNative.ValidateLayouts())
                UnityEngine.Debug.LogError("SHINOBU_234 StormPropagationDTO layout validation failed.");
        }
    }
}
#endif
