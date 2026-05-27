#if UNITY_EDITOR
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Rendering.Editor
{
    internal static class AbyssalCausticsLayoutAudit
    {
        [MenuItem("HECTON-8/Rendering/Validate Abyssal Caustics Layout")]
        private static void ValidateMenu()
        {
            bool valid = ValidateAll();
            if (valid)
                Debug.Log("13KRA caustics DTO layout audit passed.");
            else
                Debug.LogError("13KRA caustics DTO layout audit failed.");
        }

        internal static bool ValidateAll()
        {
            bool valid = true;
            valid &= ValidateParameters();
            valid &= ValidateTuning();
            valid &= ValidateTelemetry();
            valid &= ValidateProfile();
            return valid;
        }

        private static bool ValidateParameters()
        {
            return UnsafeUtility.SizeOf<CausticsParametersDTO>() == AbyssalCausticsConstants.CBufferBytes &&
                   OffsetOf<CausticsParametersDTO>(nameof(CausticsParametersDTO.ProjectionVectorAndScale)) == 0 &&
                   OffsetOf<CausticsParametersDTO>(nameof(CausticsParametersDTO.NoiseAnimationSpeed)) == 16 &&
                   OffsetOf<CausticsParametersDTO>(nameof(CausticsParametersDTO.IntensityAndDepthFalloff)) == 32 &&
                   OffsetOf<CausticsParametersDTO>(nameof(CausticsParametersDTO.QualityAndColor)) == 48;
        }

        private static bool ValidateTuning()
        {
            return UnsafeUtility.SizeOf<CausticsTuningDTO>() == AbyssalCausticsConstants.TuningBytes &&
                   OffsetOf<CausticsTuningDTO>(nameof(CausticsTuningDTO.ScaleFlowDepthIntensity)) == 0 &&
                   OffsetOf<CausticsTuningDTO>(nameof(CausticsTuningDTO.DispersionSdfTileProfile)) == 16 &&
                   OffsetOf<CausticsTuningDTO>(nameof(CausticsTuningDTO.ColorRgbWeatherPenalty)) == 32 &&
                   OffsetOf<CausticsTuningDTO>(nameof(CausticsTuningDTO.Reserved)) == 48;
        }

        private static bool ValidateTelemetry()
        {
            return UnsafeUtility.SizeOf<CausticsTelemetryEntry>() == AbyssalCausticsConstants.TelemetryBytes &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.FrameIndex)) == 0 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.StateHash)) == 4 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.Flags)) == 8 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.ActiveNoiseOctavesX1000)) == 12 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.SunIntensity)) == 16 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.ActiveNoiseOctaves)) == 20 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.MaxDepthMeters)) == 24 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.EstimatedGpuMicros)) == 28 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.ProjectionVectorAndScale)) == 32 &&
                   OffsetOf<CausticsTelemetryEntry>(nameof(CausticsTelemetryEntry.NoiseAnimationSpeed)) == 48;
        }

        private static bool ValidateProfile()
        {
            return UnsafeUtility.SizeOf<CausticsLightingProfileDTO>() == AbyssalCausticsConstants.ProfileBytes &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.StateHash)) == 0 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.NoiseScale)) == 4 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.Intensity)) == 8 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.MaxDepthMeters)) == 12 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.FlowSpeed)) == 16 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.ChromaticDispersion)) == 20 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.SdfShadowStrength)) == 24 &&
                   OffsetOf<CausticsLightingProfileDTO>(nameof(CausticsLightingProfileDTO.Reserved)) == 28;
        }

        private static int OffsetOf<T>(string fieldName)
            where T : unmanaged
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }
}
#endif
