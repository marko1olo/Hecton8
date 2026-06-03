using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Rendering
{
    public static class BilateralDrsUpscalerConstants
    {
        public const int CBufferBytes = 32;
        public const int TuningBytes = 64;
        public const int TelemetryBytes = 64;
        public const int ProfileBytes = 32;
        public const int ParameterCapacity = 2;
        public const int ActiveParameterIndex = 0;
        public const int PendingParameterIndex = 1;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 32;
        public const int CsvScratchBytes = 16384;
        public const float QualityGateStart = 0.015f;
        public const float QualityGateEnd = 0.075f;
        public const float DefaultMinRadiusPixels = 0.5625f;
        public const float DefaultMaxRadiusPixels = 2.1875f;
        public const float DefaultDepthWeight = 168f;
        public const float DefaultColorWeight = 12f;
        public const uint StateHash = 0x42323336u; // B236
        public const uint UpscalerTypeHash = 0x42445253u; // BDRS
        public const uint FaultNonFinite = 1u << 0;
        public const uint FaultLayout = 1u << 1;
        public const uint FaultConstantBufferUnsupported = 1u << 2;
        public const uint FaultVaultUnavailable = 1u << 3;
        public const uint FlagMockState = 1u << 8;
        public const uint FlagScaleService = 1u << 9;
        public const uint FlagRenderDimensions = 1u << 10;
        public const uint FlagEditorOverride = 1u << 11;
        public const uint FlagDebugEdgeMask = 1u << 12;
    }

    /// <summary>
    /// GPU constant payload. ResolutionParams = lowX, lowY, highX, highY.
    /// FilterParams = depthWeight, colorWeight, packed radius+jitter, qualityScalar.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = BilateralDrsUpscalerConstants.CBufferBytes)]
    public struct UpscalerParamsDTO
    {
        [FieldOffset(0)] public float4 ResolutionParams;
        [FieldOffset(16)] public float4 FilterParams;
    }

    /// <summary>
    /// Cold tuning lane. Runtime reads one entry; editor/CSV write this outside the render pass.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = BilateralDrsUpscalerConstants.TuningBytes)]
    public struct UpscalerTuningDTO
    {
        [FieldOffset(0)] public float4 DepthColorRadiusSharpness;
        [FieldOffset(16)] public float4 EdgeThresholds;
        [FieldOffset(32)] public float4 ScaleQualityOverride;
        [FieldOffset(48)] public float4 DebugAndFlags;
    }

    [StructLayout(LayoutKind.Explicit, Size = BilateralDrsUpscalerConstants.TelemetryBytes)]
    public struct UpscalerTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public float CurrentRenderScale01;
        [FieldOffset(12)] public float TargetRenderScale01;
        [FieldOffset(16)] public float QualityScalar;
        [FieldOffset(20)] public float BilateralRadiusPixels;
        [FieldOffset(24)] public float DepthWeight;
        [FieldOffset(28)] public float EstimatedGpuMicros;
        [FieldOffset(32)] public float4 ResolutionParams;
        [FieldOffset(48)] public float4 FilterParams;
    }

    [StructLayout(LayoutKind.Explicit, Size = BilateralDrsUpscalerConstants.ProfileBytes)]
    public struct UpscalerProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float MinScale01;
        [FieldOffset(8)] public float MaxScale01;
        [FieldOffset(12)] public float QualityBias01;
        [FieldOffset(16)] public float4 FilterParams;
    }

    public static class UpscalerParamsLayoutValidator
    {
        public const int ResolutionParamsOffset = 0;
        public const int FilterParamsOffset = 16;

        public static bool Validate()
        {
            return UnsafeUtility.SizeOf<UpscalerParamsDTO>() == BilateralDrsUpscalerConstants.CBufferBytes &&
                   OffsetOf<UpscalerParamsDTO>(nameof(UpscalerParamsDTO.ResolutionParams)) == ResolutionParamsOffset &&
                   OffsetOf<UpscalerParamsDTO>(nameof(UpscalerParamsDTO.FilterParams)) == FilterParamsOffset &&
                   UnsafeUtility.SizeOf<UpscalerTuningDTO>() == BilateralDrsUpscalerConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<UpscalerTelemetryEntry>() == BilateralDrsUpscalerConstants.TelemetryBytes &&
                   UnsafeUtility.SizeOf<UpscalerProfileDTO>() == BilateralDrsUpscalerConstants.ProfileBytes &&
                   ResolutionParamsOffset == 0 &&
                   FilterParamsOffset == 16;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct CalculateUpscalerParamsJob : IJob
    {
        [WriteOnly] [NoAlias] public NativeArray<UpscalerParamsDTO> Parameters;
        [WriteOnly] [NoAlias] public NativeArray<UpscalerTelemetryEntry> Telemetry;
        [NoAlias] public NativeArray<int> TelemetryCursor;
        [ReadOnly] [NoAlias] public NativeArray<UpscalerTuningDTO>.ReadOnly Tuning;
        [ReadOnly] [NoAlias] public NativeArray<UpscalerProfileDTO>.ReadOnly Profiles;
        public ResolutionScaleState ScaleStateSnapshot;
        public DrsStateDTO MockStateSnapshot;
        public int SubmittedLowWidth;
        public int SubmittedLowHeight;
        public int SubmittedFullWidth;
        public int SubmittedFullHeight;
        public float SubmittedJitterX;
        public float SubmittedJitterY;
        public float FallbackQuality01;
        public uint FrameIndex;
        public int OutputIndex;
        public byte HasScaleState;
        public byte UseMockState;
        public UpscalerTelemetryEntry LastTelemetry;
        public byte HasLastTelemetry;
        public UpscalerParamsDTO LastParameters;
        public byte HasLastParameters;

        public void Execute()
        {
            HasLastTelemetry = 0;
            HasLastParameters = 0;

            UpscalerTuningDTO tuning = Tuning.IsCreated && Tuning.Length > 0
                ? SanitizeTuning(Tuning[0])
                : DefaultTuning();

            uint flags = 0u;
            float currentScale = 1f;
            float targetScale = 1f;
            float serviceQuality = Sanitize01(FallbackQuality01, 1f);
            if (UseMockState != 0)
            {
                DrsStateDTO mockState = MockStateSnapshot;
                currentScale = SanitizeScale(mockState.CurrentRenderScale, 0.5f);
                targetScale = SanitizeScale(mockState.TargetRenderScale, currentScale);
                flags |= BilateralDrsUpscalerConstants.FlagMockState;
            }
            else if (HasScaleState != 0)
            {
                currentScale = SanitizeScale(ScaleStateSnapshot.CurrentRenderScale01, 1f);
                targetScale = SanitizeScale(ScaleStateSnapshot.TargetRenderScale01, currentScale);
                serviceQuality = Sanitize01(ScaleStateSnapshot.GlobalQualityWeight01, serviceQuality);
                flags |= BilateralDrsUpscalerConstants.FlagScaleService;
            }

            float overrideScale = tuning.ScaleQualityOverride.x;
            if (math.isfinite(overrideScale) && overrideScale > 0.01f)
                currentScale = SanitizeScale(overrideScale, currentScale);

            float overrideQuality = tuning.ScaleQualityOverride.y;
            if (math.isfinite(overrideQuality) && overrideQuality >= 0f)
            {
                serviceQuality = math.saturate(overrideQuality);
                flags |= BilateralDrsUpscalerConstants.FlagEditorOverride;
            }
            flags |= tuning.DebugAndFlags.x > 0.5f ? BilateralDrsUpscalerConstants.FlagDebugEdgeMask : 0u;

            int fullWidth = math.max(1, SubmittedFullWidth);
            int fullHeight = math.max(1, SubmittedFullHeight);
            int lowWidth = SubmittedLowWidth > 0 ? SubmittedLowWidth : math.max(1, (int)math.round(fullWidth * currentScale));
            int lowHeight = SubmittedLowHeight > 0 ? SubmittedLowHeight : math.max(1, (int)math.round(fullHeight * currentScale));
            flags |= (SubmittedFullWidth > 0 && SubmittedFullHeight > 0) ? BilateralDrsUpscalerConstants.FlagRenderDimensions : 0u;

            float actualScale = math.saturate(math.min(lowWidth / (float)fullWidth, lowHeight / (float)fullHeight));
            float drop01 = math.saturate((1f - actualScale) * 1.6666666f);
            float quality = math.saturate(serviceQuality + tuning.ScaleQualityOverride.z);
            ApplyProfile(ref tuning, ref quality, actualScale);
            float depthWeight = math.max(1f, tuning.DepthColorRadiusSharpness.x) * math.lerp(0.72f, 1.35f, drop01);
            float colorWeight = math.max(0.001f, tuning.DepthColorRadiusSharpness.y);
            float minRadius = math.max(0.25f, tuning.DepthColorRadiusSharpness.z);
            float maxRadius = math.max(minRadius, tuning.DepthColorRadiusSharpness.w);
            float radius = math.lerp(minRadius, maxRadius, quality * quality);
            radius *= math.lerp(0.86f, 1.28f, drop01);
            radius = math.clamp(radius, 0.25f, 4f);
            float jitterScale = math.max(0f, tuning.EdgeThresholds.w);
            float packedRadiusJitter = PackRadiusJitter(radius, SubmittedJitterX * jitterScale, SubmittedJitterY * jitterScale);

            UpscalerParamsDTO dto;
            dto.ResolutionParams = new float4(lowWidth, lowHeight, fullWidth, fullHeight);
            dto.FilterParams = new float4(depthWeight, colorWeight, packedRadiusJitter, quality);

            bool finite = math.all(math.isfinite(dto.ResolutionParams)) &&
                          math.all(math.isfinite(dto.FilterParams)) &&
                          lowWidth > 0 &&
                          lowHeight > 0 &&
                          fullWidth > 0 &&
                          fullHeight > 0;
            flags |= finite ? 0u : BilateralDrsUpscalerConstants.FaultNonFinite;

            LastParameters = dto;
            HasLastParameters = 1;
            if (Parameters.IsCreated && Parameters.Length > 0)
                Parameters[ClampOutputIndex(OutputIndex, Parameters.Length)] = dto;

            UpscalerTelemetryEntry telemetry = BuildTelemetryEntry(in dto, flags, currentScale, targetScale, quality, radius, depthWeight, drop01, FrameIndex);
            LastTelemetry = telemetry;
            HasLastTelemetry = 1;
            WriteTelemetry(in telemetry);
        }

        public static UpscalerTuningDTO DefaultTuning()
        {
            UpscalerTuningDTO tuning;
            tuning.DepthColorRadiusSharpness = new float4(
                BilateralDrsUpscalerConstants.DefaultDepthWeight,
                BilateralDrsUpscalerConstants.DefaultColorWeight,
                BilateralDrsUpscalerConstants.DefaultMinRadiusPixels,
                BilateralDrsUpscalerConstants.DefaultMaxRadiusPixels);
            tuning.EdgeThresholds = new float4(0.0015f, 0.10f, 0.25f, 1f);
            tuning.ScaleQualityOverride = new float4(0f, -1f, 0f, 0f);
            tuning.DebugAndFlags = default;
            return tuning;
        }

        public static float PackRadiusJitter(float radius, float jitterX, float jitterY)
        {
            float radiusQ = math.round(math.clamp(radius, 0.25f, 4f) * 16f) * 0.0625f;
            float jxQ = math.round(math.saturate(jitterX * 0.5f + 0.5f) * 31f);
            float jyQ = math.round(math.saturate(jitterY * 0.5f + 0.5f) * 31f);
            return radiusQ + jxQ * 0.0009765625f + jyQ * 0.000030517578125f;
        }

        private void WriteTelemetry(in UpscalerTelemetryEntry entry)
        {
            if (!Telemetry.IsCreated || Telemetry.Length <= 0)
                return;

            int cursor = ResolveTelemetryCursor(TelemetryCursor, Telemetry.Length);
            Telemetry[cursor] = entry;

            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = (cursor + 1) % Telemetry.Length;
        }

        internal static UpscalerTelemetryEntry BuildTelemetryEntry(
            in UpscalerParamsDTO dto,
            uint flags,
            float currentScale,
            float targetScale,
            float quality,
            float radius,
            float depthWeight,
            float drop01,
            uint frameIndex)
        {
            UpscalerTelemetryEntry entry;
            entry.FrameIndex = frameIndex;
            entry.Flags = flags;
            entry.CurrentRenderScale01 = currentScale;
            entry.TargetRenderScale01 = targetScale;
            entry.QualityScalar = quality;
            entry.BilateralRadiusPixels = radius;
            entry.DepthWeight = depthWeight;
            entry.EstimatedGpuMicros = EstimateGpuMicros(drop01, quality, radius);
            entry.ResolutionParams = dto.ResolutionParams;
            entry.FilterParams = dto.FilterParams;
            return entry;
        }

        internal static int ResolveTelemetryCursor(NativeArray<int> telemetryCursor, int telemetryLength)
        {
            if (telemetryLength <= 0)
                return 0;

            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
            {
                int rawCursor = telemetryCursor[0];
                return rawCursor == int.MinValue ? 0 : math.abs(rawCursor) % telemetryLength;
            }

            return 0;
        }

        private static UpscalerTuningDTO SanitizeTuning(UpscalerTuningDTO tuning)
        {
            UpscalerTuningDTO fallback = DefaultTuning();
            tuning.DepthColorRadiusSharpness = math.select(fallback.DepthColorRadiusSharpness, tuning.DepthColorRadiusSharpness, math.isfinite(tuning.DepthColorRadiusSharpness));
            tuning.EdgeThresholds = math.select(fallback.EdgeThresholds, tuning.EdgeThresholds, math.isfinite(tuning.EdgeThresholds));
            tuning.ScaleQualityOverride = math.select(fallback.ScaleQualityOverride, tuning.ScaleQualityOverride, math.isfinite(tuning.ScaleQualityOverride));
            tuning.DepthColorRadiusSharpness.x = math.max(1f, tuning.DepthColorRadiusSharpness.x);
            tuning.DepthColorRadiusSharpness.y = math.max(0.001f, tuning.DepthColorRadiusSharpness.y);
            tuning.DepthColorRadiusSharpness.z = math.max(0.25f, tuning.DepthColorRadiusSharpness.z);
            tuning.DepthColorRadiusSharpness.w = math.max(tuning.DepthColorRadiusSharpness.z, tuning.DepthColorRadiusSharpness.w);
            tuning.EdgeThresholds.x = math.max(0.00001f, tuning.EdgeThresholds.x);
            tuning.EdgeThresholds.y = math.max(0.001f, tuning.EdgeThresholds.y);
            tuning.EdgeThresholds.z = math.saturate(tuning.EdgeThresholds.z);
            tuning.EdgeThresholds.w = math.saturate(tuning.EdgeThresholds.w);
            tuning.ScaleQualityOverride.z = math.clamp(tuning.ScaleQualityOverride.z, -1f, 1f);
            return tuning;
        }

        private void ApplyProfile(ref UpscalerTuningDTO tuning, ref float quality, float scale01)
        {
            if (!Profiles.IsCreated)
                return;

            for (int i = 0; i < Profiles.Length; i++)
            {
                UpscalerProfileDTO profile = Profiles[i];
                if (profile.ProfileHash == 0u)
                    continue;

                float minScale = math.saturate(math.select(profile.MinScale01, 0f, !math.isfinite(profile.MinScale01)));
                float maxScale = math.saturate(math.select(profile.MaxScale01, 1f, !math.isfinite(profile.MaxScale01)));
                if (scale01 < minScale || scale01 > math.max(minScale, maxScale))
                    continue;

                tuning.DepthColorRadiusSharpness.x = math.max(1f, profile.FilterParams.x);
                tuning.DepthColorRadiusSharpness.y = math.max(0.001f, profile.FilterParams.y);
                tuning.DepthColorRadiusSharpness.z = math.max(0.25f, profile.FilterParams.z);
                tuning.DepthColorRadiusSharpness.w = math.max(tuning.DepthColorRadiusSharpness.z, profile.FilterParams.w);
                quality = math.saturate(quality + math.select(profile.QualityBias01, 0f, !math.isfinite(profile.QualityBias01)));
                return;
            }
        }

        private static int ClampOutputIndex(int outputIndex, int parameterLength)
        {
            int maxIndex = parameterLength - 1;
            if (outputIndex < 0)
                return 0;
            return outputIndex > maxIndex ? maxIndex : outputIndex;
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.saturate(math.select(value, fallback, !math.isfinite(value)));
        }

        private static float SanitizeScale(float value, float fallback)
        {
            return math.clamp(math.select(value, fallback, !math.isfinite(value) || value <= 0f), 0.25f, 1f);
        }

        private static float EstimateGpuMicros(float drop01, float quality, float radius)
        {
            float edgeDensityGuess = math.lerp(0.11f, 0.24f, drop01);
            float tapFactor = math.lerp(5f, 13f, quality * quality);
            return 18f + edgeDensityGuess * tapFactor * radius * 3.25f;
        }
    }
}
