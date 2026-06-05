using System;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Narrative.Prologue
{
    internal static class ReentrySequenceMetricValidator1603
    {
        private const int FuzzerFrameCount = 240;
        private const float FixedDeltaSeconds = 1f / 60f;

        internal static ReentrySequenceMetricResult Run()
        {
            ReentrySequenceMetricResult result = default;
            result.DtoLayoutValid = ToByte(ValidateDtoLayout());
            result.AcousticLayoutValid = ToByte(ValidateAcousticLayout());
            result.FlashImpulseValid = ToByte(ValidateFlashImpulse());
            result.ProgressMonotonic = 1;
            result.BoundsValid = 1;
            result.AblationBoundsValid = 1;

            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < FuzzerFrameCount; i++)
            {
                float progress01 = FuzzerFrameCount > 1 ? (float)i / (FuzzerFrameCount - 1) : 1f;
                float elapsed = progress01 * 30f;
                float heat01 = ResolveHeatCurve01(progress01);
                float trauma01 = ResolveTraumaCurve01(progress01, heat01, 0.5f);
                float opacity01 = ResolveOpacityCurve01(progress01, heat01);
                float plasmaIntensity01 = ResolvePlasmaIntensity01(heat01, opacity01);
                float ablationAmount01 = ResolveAblationAmount01(plasmaIntensity01, opacity01);
                float glassStress01 = ResolveGlassStress01(plasmaIntensity01, ablationAmount01, 0f, 0.5f);

                result.MaxHeat01 = math.max(result.MaxHeat01, heat01);
                result.MaxTrauma01 = math.max(result.MaxTrauma01, trauma01);
                result.MaxAblation01 = math.max(result.MaxAblation01, ablationAmount01);
                result.MaxGlassStress01 = math.max(result.MaxGlassStress01, glassStress01);
                result.ProgressMonotonic &= ToByte(progress01 >= result.LastProgress01);
                result.BoundsValid &= ToByte(IsUnit(heat01) && IsUnit(trauma01));
                result.AblationBoundsValid &= ToByte(IsUnit(opacity01) &&
                                                     IsUnit(plasmaIntensity01) &&
                                                     IsUnit(ablationAmount01) &&
                                                     IsUnit(glassStress01) &&
                                                     elapsed >= 0f);
                result.LastProgress01 = progress01;
            }

            long afterBytes = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBytes = afterBytes - beforeBytes;
            result.ManagedBytesAllocated = allocatedBytes > 0L ? allocatedBytes : 0L;
            result.ZeroGcScalarLoop = ToByte(result.ManagedBytesAllocated == 0L);
            result.Valid = ToByte(result.DtoLayoutValid != 0 &&
                                  result.AcousticLayoutValid != 0 &&
                                  result.FlashImpulseValid != 0 &&
                                  result.ProgressMonotonic != 0 &&
                                  result.BoundsValid != 0 &&
                                  result.AblationBoundsValid != 0 &&
                                  result.ZeroGcScalarLoop != 0 &&
                                  result.MaxHeat01 > 0.95f &&
                                  result.MaxTrauma01 > 0.05f &&
                                  result.MaxAblation01 > 0.55f &&
                                  result.MaxGlassStress01 > 0.25f);
            return result;
        }

        private static bool ValidateDtoLayout()
        {
            int dtoBytes = UnsafeUtility.SizeOf<ReentryStateDTO>();
            return dtoBytes == 32 &&
                   (dtoBytes & 7) == 0 &&
                   OffsetOf(nameof(ReentryStateDTO.ElapsedTime)) == 0 &&
                   OffsetOf(nameof(ReentryStateDTO.Progress01)) == 8 &&
                   OffsetOf(nameof(ReentryStateDTO.HeatIntensity)) == 12 &&
                   OffsetOf(nameof(ReentryStateDTO.TraumaScalar)) == 16 &&
                   OffsetOf(nameof(ReentryStateDTO.CurrentPhaseEnum)) == 20;
        }

        private static bool ValidateAcousticLayout()
        {
            int signalBytes = UnsafeUtility.SizeOf<ReentryAcousticStressSignal>();
            return signalBytes == 32 &&
                   (signalBytes & 7) == 0 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.Stress01)) == 0 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.Heat01)) == 4 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.UniverseVelocityMetersPerSecond)) == 8 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.LowPassCutoffHz)) == 12 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.LfeGain01)) == 16 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.GranularStress01)) == 20 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.Frame)) == 24 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.Sequence)) == 28 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.Flags)) == 30 &&
                   AcousticOffsetOf(nameof(ReentryAcousticStressSignal.Phase)) == 31;
        }

        private static bool ValidateFlashImpulse()
        {
            float flash01 = 1f;
            bool holdFirstFrame = true;
            float firstUpload01 = SimulateFlashVisualSync(ref flash01, ref holdFirstFrame, FixedDeltaSeconds, 3.4f, 1f);
            float secondUpload01 = SimulateFlashVisualSync(ref flash01, ref holdFirstFrame, FixedDeltaSeconds, 3.4f, 1f);
            return firstUpload01 == 1f &&
                   secondUpload01 < firstUpload01 &&
                   IsUnit(secondUpload01);
        }

        private static int OffsetOf(string fieldName)
        {
            return Marshal.OffsetOf<ReentryStateDTO>(fieldName).ToInt32();
        }

        private static int AcousticOffsetOf(string fieldName)
        {
            return Marshal.OffsetOf<ReentryAcousticStressSignal>(fieldName).ToInt32();
        }

        private static float ResolveHeatCurve01(float progress01)
        {
            float rise01 = SmoothStep01(math.saturate((progress01 - 0.18f) * 1.6129032f));
            float fall01 = 1f - SmoothStep01(math.saturate((progress01 - 0.88f) * 10f));
            return math.saturate(rise01 * fall01);
        }

        private static float ResolveTraumaCurve01(float progress01, float heat01, float globalQualityWeight01)
        {
            float maxQ01 = 1f - math.saturate(math.abs(progress01 - 0.8f) * 5f);
            float traumaBase01 = SmoothStep01(maxQ01) * math.saturate(heat01);
            float traumaScale01 = math.lerp(0.28f, 1f, math.saturate(globalQualityWeight01));
            return math.saturate(traumaBase01 * traumaScale01);
        }

        private static float ResolveOpacityCurve01(float progress01, float heat01)
        {
            float whiteout01 = SmoothStep01(math.saturate((progress01 - 0.62f) * 5f));
            return math.saturate(math.max(heat01, whiteout01));
        }

        private static float ResolvePlasmaIntensity01(float heat01, float opacity01)
        {
            return math.saturate(math.max(heat01, opacity01 * 0.72f));
        }

        private static float ResolveAblationAmount01(float plasmaIntensity01, float opacity01)
        {
            float plasmaSquared = plasmaIntensity01 * plasmaIntensity01;
            float opacityGain = math.lerp(0.48f, 1f, math.saturate(opacity01));
            return math.saturate(plasmaSquared * opacityGain);
        }

        private static float ResolveGlassStress01(float plasmaIntensity01, float ablationAmount01, float fullScreenFlash01, float globalQualityWeight01)
        {
            float qualityCurve = SmoothStep01(math.saturate(globalQualityWeight01));
            float qualityScaledStress = (plasmaIntensity01 * 0.45f + ablationAmount01 * 0.45f) *
                                        math.lerp(0.45f, 1f, qualityCurve);
            return math.saturate(qualityScaledStress + fullScreenFlash01 * 0.35f);
        }

        private static float SimulateFlashVisualSync(ref float flash01, ref bool holdFirstFrame, float deltaSeconds, float baseFadePerSecond, float globalQualityWeight01)
        {
            if (holdFirstFrame)
            {
                holdFirstFrame = false;
                return flash01;
            }

            float fadePerSecond = ResolveFlashFadePerSecond(baseFadePerSecond, globalQualityWeight01);
            flash01 = MoveTowards01(flash01, 0f, math.saturate(deltaSeconds) * fadePerSecond);
            return flash01;
        }

        private static float ResolveFlashFadePerSecond(float baseFadePerSecond, float globalQualityWeight01)
        {
            float qualityCurve = SmoothStep01(math.saturate(globalQualityWeight01));
            float safeBase = math.isfinite(baseFadePerSecond) && baseFadePerSecond > 0.25f ? baseFadePerSecond : 0.25f;
            return math.max(0.25f, safeBase * math.lerp(1.65f, 0.85f, qualityCurve));
        }

        private static float MoveTowards01(float current, float target, float maxDelta)
        {
            return math.saturate(current + math.clamp(target - current, -math.max(0f, maxDelta), math.max(0f, maxDelta)));
        }

        private static bool IsUnit(float value)
        {
            return math.isfinite(value) && value >= 0f && value <= 1f;
        }

        private static byte ToByte(bool value)
        {
            return value ? (byte)1 : (byte)0;
        }

        private static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    internal struct ReentrySequenceMetricResult
    {
        [FieldOffset(0)]
        public float LastProgress01;
        [FieldOffset(4)]
        public float MaxHeat01;
        [FieldOffset(8)]
        public float MaxTrauma01;
        [FieldOffset(12)]
        public float MaxAblation01;
        [FieldOffset(16)]
        public float MaxGlassStress01;
        [FieldOffset(24)]
        public long ManagedBytesAllocated;
        [FieldOffset(32)]
        public byte DtoLayoutValid;
        [FieldOffset(33)]
        public byte AcousticLayoutValid;
        [FieldOffset(34)]
        public byte FlashImpulseValid;
        [FieldOffset(35)]
        public byte ProgressMonotonic;
        [FieldOffset(36)]
        public byte BoundsValid;
        [FieldOffset(37)]
        public byte AblationBoundsValid;
        [FieldOffset(38)]
        public byte ZeroGcScalarLoop;
        [FieldOffset(39)]
        public byte Valid;
    }
}
