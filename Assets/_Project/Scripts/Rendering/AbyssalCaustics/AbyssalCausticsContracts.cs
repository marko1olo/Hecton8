using System.Runtime.InteropServices;
using AOT;
using Hecton8.Core;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Rendering
{
    public static class AbyssalCausticsConstants
    {
        public const int CBufferBytes = 64;
        public const int TuningBytes = 64;
        public const int TelemetryBytes = 64;
        public const int ProfileBytes = 32;
        public const int InputSnapshotBytes = 128;
        public const int ParameterCapacity = 2;
        public const int ActiveParameterIndex = 0;
        public const int PendingParameterIndex = 1;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 32;
        public const int CsvScratchBytes = 16384;
        public const float DefaultNoiseTileMeters = 96f;
        public const float DefaultMaxDepthMeters = 72f;
        public const uint StateHash = 0x53433233u; // SC23
        public const uint FaultNonFinite = 1u << 0;
        public const uint FaultLayout = 1u << 1;
        public const uint FaultDumpIo = 1u << 2;
        public const uint FaultConstantBufferUnavailable = 1u << 3;
        public const uint FaultBurstKernelUnavailable = 1u << 4;
        public const uint FlagMockLighting = 1u << 8;
        public const uint FlagWaveInputBound = 1u << 9;
        public const uint FlagWeatherSnapshotBound = 1u << 10;
        public const uint FlagProfileBound = 1u << 11;
        public const uint FlagInputSnapshot = 1u << 12;
        public const uint FlagCelestialLightBound = 1u << 13;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalCausticsConstants.CBufferBytes)]
    public struct CausticsParametersDTO
    {
        [FieldOffset(0)] public float4 ProjectionVectorAndScale;
        [FieldOffset(16)] public float4 NoiseAnimationSpeed;
        [FieldOffset(32)] public float4 IntensityAndDepthFalloff;
        [FieldOffset(48)] public float4 QualityAndColor;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalCausticsConstants.TuningBytes)]
    public struct CausticsTuningDTO
    {
        [FieldOffset(0)] public float4 ScaleFlowDepthIntensity;
        [FieldOffset(16)] public float4 DispersionSdfTileProfile;
        [FieldOffset(32)] public float4 ColorRgbWeatherPenalty;
        [FieldOffset(48)] public float4 Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalCausticsConstants.TelemetryBytes)]
    public struct CausticsTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint ActiveNoiseOctavesX1000;
        [FieldOffset(16)] public float SunIntensity;
        [FieldOffset(20)] public float ActiveNoiseOctaves;
        [FieldOffset(24)] public float MaxDepthMeters;
        [FieldOffset(28)] public float EstimatedGpuMicros;
        [FieldOffset(32)] public float4 ProjectionVectorAndScale;
        [FieldOffset(48)] public float4 NoiseAnimationSpeed;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalCausticsConstants.ProfileBytes)]
    public struct CausticsLightingProfileDTO
    {
        [FieldOffset(0)] public uint StateHash;
        [FieldOffset(4)] public float NoiseScale;
        [FieldOffset(8)] public float Intensity;
        [FieldOffset(12)] public float MaxDepthMeters;
        [FieldOffset(16)] public float FlowSpeed;
        [FieldOffset(20)] public float ChromaticDispersion;
        [FieldOffset(24)] public float SdfShadowStrength;
        [FieldOffset(28)] public float Reserved;
    }

    [StructLayout(LayoutKind.Explicit, Size = AbyssalCausticsConstants.InputSnapshotBytes)]
    public struct CausticsInputSnapshotDTO
    {
        [FieldOffset(0)] public CausticsTuningDTO Tuning;
        [FieldOffset(64)] public float4 WeatherStormWindPhaseQuality;
        [FieldOffset(80)] public float4 WaveHeightFrequencyReserved;
        [FieldOffset(96)] public float4 ProfileIntensityScaleDepthFlow;
        [FieldOffset(112)] public float2 ProfileChromaticSdf;
        [FieldOffset(120)] public uint Flags;
        [FieldOffset(124)] public uint Reserved;
    }

    public static class CausticsParametersLayoutValidator
    {
        public const int ProjectionVectorAndScaleOffset = 0;
        public const int NoiseAnimationSpeedOffset = 16;
        public const int IntensityAndDepthFalloffOffset = 32;
        public const int QualityAndColorOffset = 48;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<CausticsParametersDTO>() == AbyssalCausticsConstants.CBufferBytes &&
                   ProjectionVectorAndScaleOffset == 0 &&
                   NoiseAnimationSpeedOffset == 16 &&
                   IntensityAndDepthFalloffOffset == 32 &&
                   QualityAndColorOffset == 48 &&
                   UnsafeUtility.SizeOf<CausticsTuningDTO>() == AbyssalCausticsConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<CausticsTelemetryEntry>() == AbyssalCausticsConstants.TelemetryBytes &&
                   UnsafeUtility.SizeOf<CausticsLightingProfileDTO>() == AbyssalCausticsConstants.ProfileBytes &&
                   UnsafeUtility.SizeOf<CausticsInputSnapshotDTO>() == AbyssalCausticsConstants.InputSnapshotBytes;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct GenerateMockCausticLightingJob
    {
        // SAFETY: Parameters is the caustics-owned Vault lane for caustic CBuffer DTOs.
        // The runtime resolves a minimum capacity before invocation and this kernel writes
        // exactly one clamped element, so the unsafe pointer never crosses the lane bounds.
        [NoAlias] [NativeDisableUnsafePtrRestriction] public CausticsParametersDTO* Parameters;
        public int ParameterLength;
        public CausticsInputSnapshotDTO InputSnapshot;
        public double3 CameraAupLocalOffset;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public uint FrameIndex;
        public int OutputIndex;

        public void Execute()
        {
            if (Parameters == null || ParameterLength < 1)
                return;

            CausticsTuningDTO tuning = (InputSnapshot.Flags & AbyssalCausticsConstants.FlagInputSnapshot) != 0u
                ? InputSnapshot.Tuning
                : DefaultTuning();
            float quality = math.saturate(math.select(GlobalQualityWeight, 1f, !math.isfinite(GlobalQualityWeight)));
            float phase = TimeSeconds * (0.115f + quality * 0.085f);
            float3 lightDir = SafeNormalize(
                new float3(MathLodApproximation.ApproxSinBhaskara(phase) * 0.36f, -0.92f, MathLodApproximation.ApproxCosBhaskara(phase * 0.83f) * 0.24f),
                new float3(0f, -1f, 0f));
            float tileSize = math.max(8f, tuning.DispersionSdfTileProfile.z > 0.001f ? tuning.DispersionSdfTileProfile.z : AbyssalCausticsConstants.DefaultNoiseTileMeters);
            float3 wrappedAup = WrapAup(CameraAupLocalOffset, tileSize);
            float maxDepth = ResolveMaxDepth(tuning.ScaleFlowDepthIntensity.z, quality);
            float intensity = math.max(0f, tuning.ScaleFlowDepthIntensity.w) * math.lerp(0.30f, 1.0f, quality);
            float scale = math.max(0.01f, tuning.ScaleFlowDepthIntensity.x);
            float flow = math.max(0f, tuning.ScaleFlowDepthIntensity.y);

            int outputIndex = ClampOutputIndex(OutputIndex, ParameterLength);
            CausticsParametersDTO* ptr = Parameters + outputIndex;
            ref CausticsParametersDTO dto = ref UnsafeUtility.AsRef<CausticsParametersDTO>(ptr);
            dto.ProjectionVectorAndScale = new float4(lightDir, scale);
            dto.NoiseAnimationSpeed = new float4(wrappedAup.x, wrappedAup.z, TimeSeconds * flow, tuning.DispersionSdfTileProfile.x);
            dto.IntensityAndDepthFalloff = new float4(intensity, math.rcp(math.max(1f, maxDepth)), maxDepth, tuning.DispersionSdfTileProfile.y);
            dto.QualityAndColor = new float4(quality, tuning.ColorRgbWeatherPenalty.x, tuning.ColorRgbWeatherPenalty.y, tuning.ColorRgbWeatherPenalty.z);
        }

        internal static CausticsTuningDTO DefaultTuning()
        {
            CausticsTuningDTO tuning;
            tuning.ScaleFlowDepthIntensity = new float4(0.085f, 1.0f, AbyssalCausticsConstants.DefaultMaxDepthMeters, 0.34f);
            tuning.DispersionSdfTileProfile = new float4(0.16f, 0.86f, AbyssalCausticsConstants.DefaultNoiseTileMeters, 0f);
            tuning.ColorRgbWeatherPenalty = new float4(0.18f, 0.55f, 0.62f, 0.58f);
            tuning.Reserved = default;
            return tuning;
        }

        internal static float3 WrapAup(double3 cameraAupLocalOffset, float tileSize)
        {
            double safeTile = math.max(1.0, (double)tileSize);
            return new float3(
                (float)(cameraAupLocalOffset.x - math.floor(cameraAupLocalOffset.x / safeTile) * safeTile),
                (float)(cameraAupLocalOffset.y - math.floor(cameraAupLocalOffset.y / safeTile) * safeTile),
                (float)(cameraAupLocalOffset.z - math.floor(cameraAupLocalOffset.z / safeTile) * safeTile));
        }

        internal static float ResolveMaxDepth(float requestedMaxDepth, float quality)
        {
            float highDepth = math.max(8f, math.select(requestedMaxDepth, AbyssalCausticsConstants.DefaultMaxDepthMeters, !math.isfinite(requestedMaxDepth) || requestedMaxDepth <= 0f));
            return math.lerp(math.min(18f, highDepth), highDepth, math.saturate(quality * quality));
        }

        internal static float ResolveActiveOctaves(float quality)
        {
            float q = math.saturate(quality);
            float second = math.smoothstep(0.34f, 0.82f, q);
            float chroma = math.smoothstep(0.62f, 1.0f, q);
            return 1f + second + chroma * 0.35f;
        }

        internal static float3 SafeNormalize(float3 value, float3 fallback)
        {
            float lenSq = math.lengthsq(value);
            bool valid = math.isfinite(lenSq) && lenSq > 0.000001f;
            float invLen = math.rsqrt(math.max(lenSq, 0.000001f));
            return math.select(fallback, value * invLen, valid);
        }

        private static int ClampOutputIndex(int outputIndex, int parameterLength)
        {
            int maxIndex = parameterLength - 1;
            if (outputIndex < 0)
                return 0;
            return outputIndex > maxIndex ? maxIndex : outputIndex;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct CalculateCausticParametersJob
    {
        // SAFETY: Parameters, Telemetry, and TelemetryCursor are distinct Vault lanes
        // resolved to raw pointers and lengths immediately before this kernel is invoked.
        // Optional producer facts are pre-sanitized into InputSnapshot.
        //
        // SAFETY: The only raw pointer write targets Parameters[clamped OutputIndex].
        // Telemetry writes use one cursor modulo the fixed 300-entry ring; cursor lane is
        // one int. The kernel does not resize, release, or retain any memory view.
        //
        // SAFETY: [NoAlias] is valid because each pointer comes from a different
        // caustics-owned BufferID and optional external inputs are value snapshots.
        [NoAlias] [NativeDisableUnsafePtrRestriction] public CausticsParametersDTO* Parameters;
        public int ParameterLength;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public CausticsTelemetryEntry* Telemetry;
        public int TelemetryLength;
        [NoAlias] [NativeDisableUnsafePtrRestriction] public int* TelemetryCursor;
        public int TelemetryCursorLength;
        public CausticsInputSnapshotDTO InputSnapshot;
        public double3 CameraAupLocalOffset;
        public float TimeSeconds;
        public float GlobalQualityWeight;
        public uint FrameIndex;
        public int OutputIndex;

        public void Execute()
        {
            if (Parameters == null || ParameterLength < 1)
                return;

            CausticsInputSnapshotDTO snapshot = InputSnapshot;
            CausticsTuningDTO tuning = (snapshot.Flags & AbyssalCausticsConstants.FlagInputSnapshot) != 0u
                ? snapshot.Tuning
                : GenerateMockCausticLightingJob.DefaultTuning();
            float weatherStorm = math.saturate(snapshot.WeatherStormWindPhaseQuality.x);
            float windSpeed = math.max(0f, snapshot.WeatherStormWindPhaseQuality.y);
            float wavePhase = snapshot.WeatherStormWindPhaseQuality.z;
            float quality = math.saturate(math.select(snapshot.WeatherStormWindPhaseQuality.w, GlobalQualityWeight, !math.isfinite(snapshot.WeatherStormWindPhaseQuality.w)));
            float waveHeight = math.max(0.01f, snapshot.WaveHeightFrequencyReserved.x);
            float waveFrequency = math.max(0.02f, snapshot.WaveHeightFrequencyReserved.y);
            float profileIntensity = math.max(0f, snapshot.ProfileIntensityScaleDepthFlow.x);
            float profileScale = math.max(0.01f, snapshot.ProfileIntensityScaleDepthFlow.y);
            float profileDepth = math.max(0f, snapshot.ProfileIntensityScaleDepthFlow.z);
            float profileFlowMultiplier = math.max(0f, snapshot.ProfileIntensityScaleDepthFlow.w);
            float profileChromaticDispersion = math.saturate(snapshot.ProfileChromaticSdf.x);
            float profileSdfShadowStrength = math.saturate(snapshot.ProfileChromaticSdf.y);
            uint flags = snapshot.Flags & ~AbyssalCausticsConstants.FlagInputSnapshot;

            float tileSize = math.max(8f, tuning.DispersionSdfTileProfile.z > 0.001f ? tuning.DispersionSdfTileProfile.z : AbyssalCausticsConstants.DefaultNoiseTileMeters);
            float3 wrappedAup = GenerateMockCausticLightingJob.WrapAup(CameraAupLocalOffset, tileSize);
            float phase = TimeSeconds * (0.07f + windSpeed * 0.013f) + wavePhase * 0.17f;
            float3 sunDir = GenerateMockCausticLightingJob.SafeNormalize(
                new float3(
                    MathLodApproximation.ApproxSinBhaskara(phase) * (0.22f + waveHeight * 0.04f),
                    -math.lerp(0.98f, 0.72f, weatherStorm),
                    MathLodApproximation.ApproxCosBhaskara(phase * 0.71f) * (0.18f + waveFrequency * 0.01f)),
                new float3(0f, -1f, 0f));

            float baseScale = math.max(0.005f, tuning.ScaleFlowDepthIntensity.x) * profileScale;
            float flow = math.max(0f, tuning.ScaleFlowDepthIntensity.y) * profileFlowMultiplier * (0.55f + windSpeed * 0.12f + waveHeight * 0.28f);
            float requestedDepth = profileDepth > 0.001f ? profileDepth : tuning.ScaleFlowDepthIntensity.z;
            float maxDepth = GenerateMockCausticLightingJob.ResolveMaxDepth(requestedDepth, quality);
            float weatherFade = 1f - weatherStorm * math.saturate(tuning.ColorRgbWeatherPenalty.w);
            float intensity = math.max(0f, tuning.ScaleFlowDepthIntensity.w) * profileIntensity * weatherFade * math.lerp(0.32f, 1.0f, quality);
            float activeOctaves = GenerateMockCausticLightingJob.ResolveActiveOctaves(quality);

            int outputIndex = ClampOutputIndex(OutputIndex, ParameterLength);
            CausticsParametersDTO* ptr = Parameters + outputIndex;
            ref CausticsParametersDTO dto = ref UnsafeUtility.AsRef<CausticsParametersDTO>(ptr);
            dto.ProjectionVectorAndScale = new float4(sunDir, baseScale);
            dto.NoiseAnimationSpeed = new float4(wrappedAup.x, wrappedAup.z, TimeSeconds * flow, profileChromaticDispersion);
            dto.IntensityAndDepthFalloff = new float4(intensity, math.rcp(math.max(1f, maxDepth)), maxDepth, profileSdfShadowStrength);
            dto.QualityAndColor = new float4(quality, tuning.ColorRgbWeatherPenalty.x, tuning.ColorRgbWeatherPenalty.y, tuning.ColorRgbWeatherPenalty.z);

            bool finite = math.all(math.isfinite(dto.ProjectionVectorAndScale)) &&
                          math.all(math.isfinite(dto.NoiseAnimationSpeed)) &&
                          math.all(math.isfinite(dto.IntensityAndDepthFalloff)) &&
                          math.all(math.isfinite(dto.QualityAndColor));
            flags |= finite ? 0u : AbyssalCausticsConstants.FaultNonFinite;
            WriteTelemetry(in dto, flags, activeOctaves, maxDepth);
        }

        private void WriteTelemetry(in CausticsParametersDTO dto, uint flags, float activeOctaves, float maxDepth)
        {
            if (Telemetry == null || TelemetryLength <= 0)
                return;

            int cursor = 0;
            if (TelemetryCursor != null && TelemetryCursorLength > 0)
            {
                cursor = TelemetryCursor[0] % TelemetryLength;
                if (cursor < 0)
                    cursor += TelemetryLength;
            }

            CausticsTelemetryEntry entry;
            entry.FrameIndex = FrameIndex;
            entry.StateHash = ResolveStateHash(in dto, flags);
            entry.Flags = flags;
            entry.ActiveNoiseOctavesX1000 = (uint)math.round(activeOctaves * 1000f);
            entry.SunIntensity = dto.IntensityAndDepthFalloff.x;
            entry.ActiveNoiseOctaves = activeOctaves;
            entry.MaxDepthMeters = maxDepth;
            entry.EstimatedGpuMicros = EstimateGpuMicros(dto.QualityAndColor.x, activeOctaves, maxDepth);
            entry.ProjectionVectorAndScale = dto.ProjectionVectorAndScale;
            entry.NoiseAnimationSpeed = dto.NoiseAnimationSpeed;
            Telemetry[cursor] = entry;

            if (TelemetryCursor != null && TelemetryCursorLength > 0)
                TelemetryCursor[0] = (cursor + 1) % TelemetryLength;
        }

        private static int ClampOutputIndex(int outputIndex, int parameterLength)
        {
            int maxIndex = parameterLength - 1;
            if (outputIndex < 0)
                return 0;
            return outputIndex > maxIndex ? maxIndex : outputIndex;
        }

        internal static uint ResolveTelemetryStateHash(in CausticsParametersDTO dto, uint flags)
        {
            return ResolveStateHash(in dto, flags);
        }

        internal static float EstimateTelemetryGpuMicros(float quality, float activeOctaves, float maxDepth)
        {
            return EstimateGpuMicros(quality, activeOctaves, maxDepth);
        }

        private static uint ResolveStateHash(in CausticsParametersDTO dto, uint flags)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ AbyssalCausticsConstants.StateHash) * 16777619u;
                hash = (hash ^ flags) * 16777619u;
                hash = (hash ^ math.asuint(dto.QualityAndColor.x)) * 16777619u;
                hash = (hash ^ math.asuint(dto.IntensityAndDepthFalloff.x)) * 16777619u;
                hash = (hash ^ math.asuint(dto.IntensityAndDepthFalloff.z)) * 16777619u;
                return hash;
            }
        }

        private static float EstimateGpuMicros(float quality, float activeOctaves, float maxDepth)
        {
            float depthFactor = math.saturate(maxDepth * 0.0125f);
            return math.max(0f, (0.018f + activeOctaves * 0.021f + quality * 0.014f) * depthFactor * 1000f);
        }

        internal static uint ResolveProfileWeatherKey(uint weatherStateMask, float stormWeight)
        {
            const uint stormMask = (uint)WeatherState.Storm;
            const uint forcedStormMask = 1u << 7;
            if ((weatherStateMask & (stormMask | forcedStormMask)) != 0u || stormWeight >= 0.5f)
                return stormMask;

            if (weatherStateMask != 0u)
                return weatherStateMask;

            return (uint)WeatherState.Calm;
        }

        internal static bool ProfileMatches(uint profileKey, uint resolvedWeatherKey, uint rawWeatherStateMask)
        {
            if (profileKey == 0u)
                return false;

            if (profileKey == resolvedWeatherKey || profileKey == rawWeatherStateMask)
                return true;

            const uint knownWeatherMask =
                (uint)WeatherState.Calm |
                (uint)WeatherState.Storm |
                (uint)WeatherState.UpdraftActive |
                (uint)WeatherState.ThermoclineActive |
                (uint)WeatherState.HaloclineActive |
                (uint)WeatherState.BiolumeSurge;
            return (profileKey & ~knownWeatherMask) == 0u &&
                   (profileKey & rawWeatherStateMask) != 0u;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void GenerateMockCausticLightingKernelDelegate(GenerateMockCausticLightingJob* job);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate void CalculateCausticParametersKernelDelegate(CalculateCausticParametersJob* job);

    internal static unsafe class AbyssalCausticsBurstKernelEntrypoints
    {
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(GenerateMockCausticLightingKernelDelegate))]
        internal static void GenerateMockCausticLighting(GenerateMockCausticLightingJob* job)
        {
            if (job == null)
                return;

            ref GenerateMockCausticLightingJob jobRef = ref UnsafeUtility.AsRef<GenerateMockCausticLightingJob>(job);
            jobRef.Execute();
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        [MonoPInvokeCallback(typeof(CalculateCausticParametersKernelDelegate))]
        internal static void CalculateCausticParameters(CalculateCausticParametersJob* job)
        {
            if (job == null)
                return;

            ref CalculateCausticParametersJob jobRef = ref UnsafeUtility.AsRef<CalculateCausticParametersJob>(job);
            jobRef.Execute();
        }
    }
}
