#if UNITY_EDITOR
using System;
using Hecton8.Celestial;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only deterministic celestial sync smoke test.
    /// </summary>
    public static class CelestialSyncSmokeTester
    {
        private const int SecondsPerDay = 86400;
        private const int SamplesPerSecond = 10;
        private const int StepCount = SecondsPerDay * SamplesPerSecond;
        private const float MinimumEclipseDot = 0.999999f;
        private const float MinimumEclipseOverlap = 0.8f;

        [MenuItem("Hecton/Smoke/Celestial Sync 24h")]
        public static void RunFromMenu()
        {
            Run24HourFastForward();
            Debug.Log("[CelestialSyncSmokeTester] 24h sync smoke passed.");
        }

        public static void Run24HourFastForward()
        {
            float bestDot = -1f;
            float bestOverlap = 0f;
            int bestStep = 0;

            for (int step = 0; step <= StepCount; step++)
            {
                float normalizedDay = step / (float)StepCount;
                float orbitalRadians = normalizedDay * math.PI * 2f;
                float3 sunDirection = ResolveUnitCircleDirection(orbitalRadians);
                float3 eclipseOccluderDirection = ResolveUnitCircleDirection(orbitalRadians);
                float dot = math.dot(sunDirection, eclipseOccluderDirection);
                if (dot <= bestDot)
                    continue;

                float separationDegrees = math.degrees(math.acos(math.clamp(dot, -1f, 1f)));
                bestDot = dot;
                bestOverlap = HectonCelestialEngine.EvaluatePenumbraOverlapForSmoke(0.27f, 1.1f, separationDegrees);
                bestStep = step;
            }

            if (bestStep < 0 || bestDot < MinimumEclipseDot)
                throw new InvalidOperationException($"Celestial dot alignment failed: dot={bestDot:0.000000000} step={bestStep}.");

            if (bestOverlap < MinimumEclipseOverlap)
                throw new InvalidOperationException($"Celestial eclipse overlap failed: overlap={bestOverlap:0.000000} step={bestStep}.");
        }

        private static float3 ResolveUnitCircleDirection(float radians)
        {
            return math.normalize(new float3(math.cos(radians), math.sin(radians), 0f));
        }
    }
}
#endif
