using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace Hecton8.Visor
{
    /// <summary>
    /// Quest/OpenXR foveation bridge used by the graphics commander without Oculus-package dependencies.
    /// </summary>
    public static class QuestFoveationDriver
    {
        internal const float LevelLow = 0.35f;
        internal const float LevelMedium = 0.62f;
        internal const float LevelHigh = 0.85f;
        private const float MediumPressureThreshold = 0.35f;
        private const float HighPressureThreshold = 0.70f;
        private const float SecondsToMilliseconds = 1000f;

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        internal struct ApplyResult
        {
            [FieldOffset(0)] public float AppliedLevel01;
            [FieldOffset(4)] public float SampledGpuTimeMs;
            [FieldOffset(8)] public int DisplayCount;
            [FieldOffset(12)] public byte GpuTimeSampled;
            [FieldOffset(13)] public byte NonFiniteLevelDetected;
            [FieldOffset(14)] private ushort _padding;
            [FieldOffset(16)] private uint _reserved0;
            [FieldOffset(20)] private uint _reserved1;
        }

        public static bool WouldAbortCleanlyForUnsupported(bool xrActive, FoveatedRenderingCaps caps)
        {
            return !IsHardwareFoveationSupported(xrActive, caps);
        }

        public static byte ResolveMockTargetLevelCode(
            bool xrActive,
            FoveatedRenderingCaps caps,
            float globalQualityWeight01,
            float policyPressure01,
            bool lockedHighFoveation)
        {
            if (!IsHardwareFoveationSupported(xrActive, caps))
                return 0;

            if (lockedHighFoveation)
                return 3;

            float pressure = ResolveSurvivalPressure01(globalQualityWeight01, policyPressure01);
            if (pressure >= HighPressureThreshold)
                return 3;
            if (pressure >= MediumPressureThreshold)
                return 2;
            return 1;
        }

        public static float ResolveMockTargetLevel01(
            bool xrActive,
            FoveatedRenderingCaps caps,
            float globalQualityWeight01,
            float policyPressure01,
            bool lockedHighFoveation)
        {
            if (!IsHardwareFoveationSupported(xrActive, caps))
                return 0f;

            byte levelCode = ResolveMockTargetLevelCode(
                xrActive,
                caps,
                globalQualityWeight01,
                policyPressure01,
                lockedHighFoveation);
            return ResolveTargetLevel01(globalQualityWeight01, policyPressure01, levelCode, lockedHighFoveation, true);
        }

        internal static float ResolveQualityRelief01(float qualityWeight01, float policyPressure01, bool lockedHighFoveation)
        {
            if (lockedHighFoveation)
                return 0f;

            float quality = Sanitize01(qualityWeight01);
            float pressure = Smooth01(policyPressure01);
            return math.saturate(quality * (1f - pressure));
        }

        internal static float ResolveTargetLevel01(
            float globalQualityWeight01,
            float policyPressure01,
            byte requestedLevelCode,
            bool lockedHighFoveation,
            bool enforceFixedFoveationFloor)
        {
            if (lockedHighFoveation)
                return LevelHigh;

            float requestedFloor = ResolveLevel01(requestedLevelCode);
            if (!enforceFixedFoveationFloor)
            {
                float relief = ResolveQualityRelief01(globalQualityWeight01, policyPressure01, false);
                return math.lerp(requestedFloor, 0f, relief);
            }

            float survivalPressure = ResolveSurvivalPressure01(globalQualityWeight01, policyPressure01);
            float continuousTarget = math.lerp(LevelLow, LevelHigh, survivalPressure);
            return math.max(requestedFloor, continuousTarget);
        }

        internal static bool TryApplyUnityXrFoveation(
            List<XRDisplaySubsystem> displays,
            float targetLevel01,
            XRDisplaySubsystem.FoveatedRenderingFlags targetFlags,
            bool force,
            float previousAppliedLevel01,
            XRDisplaySubsystem.FoveatedRenderingFlags previousFlags,
            bool previousModeMatches,
            float applyEpsilon,
            out ApplyResult result)
        {
            result = default;
            if (displays == null)
                return false;

            float targetLevel = Sanitize01(targetLevel01);
            bool stateUnchanged = !force &&
                math.abs(previousAppliedLevel01 - targetLevel) <= applyEpsilon &&
                previousFlags == targetFlags &&
                previousModeMatches;

            displays.Clear();
            SubsystemManager.GetSubsystems(displays);

            for (int i = 0; i < displays.Count; i++)
            {
                XRDisplaySubsystem display = displays[i];
                if (display == null || !display.running)
                    continue;

                float displayLevel = display.foveatedRenderingLevel;
                bool displayLevelFinite = math.isfinite(displayLevel);
                if (!displayLevelFinite)
                    result.NonFiniteLevelDetected = 1;

                bool displayDrifted = display.foveatedRenderingFlags != targetFlags ||
                    !displayLevelFinite ||
                    math.abs(displayLevel - targetLevel) > applyEpsilon;

                if (!stateUnchanged || displayDrifted)
                {
                    display.foveatedRenderingFlags = targetFlags;
                    display.foveatedRenderingLevel = targetLevel;
                    displayLevel = display.foveatedRenderingLevel;
                    displayLevelFinite = math.isfinite(displayLevel);
                    if (!displayLevelFinite)
                        result.NonFiniteLevelDetected = 1;
                }

                if (!displayLevelFinite)
                    displayLevel = 0f;

                result.AppliedLevel01 = math.max(result.AppliedLevel01, displayLevel);
                if (display.TryGetAppGPUTimeLastFrame(out float gpuSeconds) &&
                    math.isfinite(gpuSeconds) &&
                    gpuSeconds >= 0f)
                {
                    float gpuMs = gpuSeconds * SecondsToMilliseconds;
                    if (math.isfinite(gpuMs))
                    {
                        result.SampledGpuTimeMs = math.max(result.SampledGpuTimeMs, gpuMs);
                        result.GpuTimeSampled = 1;
                    }
                }

                result.DisplayCount++;
            }

            return result.DisplayCount > 0 && result.AppliedLevel01 > applyEpsilon;
        }

        private static bool IsHardwareFoveationSupported(bool xrActive, FoveatedRenderingCaps caps)
        {
            return xrActive && caps != FoveatedRenderingCaps.None;
        }

        private static float ResolveSurvivalPressure01(float globalQualityWeight01, float policyPressure01)
        {
            float quality = Sanitize01(globalQualityWeight01);
            float pressure = Sanitize01(policyPressure01);
            return Smooth01(math.max(pressure, 1f - quality));
        }

        private static float ResolveLevel01(byte levelCode)
        {
            switch (levelCode)
            {
                case 3:
                    return LevelHigh;
                case 2:
                    return LevelMedium;
                case 1:
                    return LevelLow;
                default:
                    return 0f;
            }
        }

        private static float Smooth01(float value)
        {
            float t = Sanitize01(value);
            return t * t * (3f - 2f * t);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }
    }
}
