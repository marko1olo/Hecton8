#if UNITY_EDITOR
using System;
using Hecton8.Celestial;
using Hecton8.Core;
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
        private const double KeplerSmokeTimeSeconds = 123456.789d;
        private const uint KeplerSmokeSeed = 0x00C0FFEEu;
        private const float KeplerPositionEpsilonSq = 0.000001f;

        [MenuItem("Hecton/Smoke/Celestial Sync 24h")]
        public static void RunFromMenu()
        {
            Run24HourFastForward();
            RunAnalyticalOrbitDeterminism();
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

        public static void RunAnalyticalOrbitDeterminism()
        {
            CelestialRuntimeSnapshot first = HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke(
                KeplerSmokeTimeSeconds,
                KeplerSmokeSeed);
            CelestialRuntimeSnapshot second = HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke(
                KeplerSmokeTimeSeconds,
                KeplerSmokeSeed);
            CelestialRuntimeSnapshot later = HectonCelestialEngine.EvaluateAnalyticalOrbitSnapshotForSmoke(
                KeplerSmokeTimeSeconds + 600d,
                KeplerSmokeSeed);

            AssertSameVector(first.GasGiantOffset, second.GasGiantOffset, "gas giant");
            AssertSameVector(first.Moon0Offset, second.Moon0Offset, "moon0");
            AssertSameVector(first.Moon1Offset, second.Moon1Offset, "moon1");
            AssertSameVector(first.TidePullVector, second.TidePullVector, "tide pull");

            if ((first.Flags & (uint)CelestialRuntimeFlags.Valid) == 0u)
                throw new InvalidOperationException("Analytical orbit snapshot missing Valid flag.");

            if (!IsFinite(first.TideHeightMeters) || math.abs(first.TideHeightMeters) > 8.0001f)
                throw new InvalidOperationException($"Analytical tide height out of bounds: {first.TideHeightMeters:0.0000}m.");

            float changedSq = math.lengthsq(first.Moon0Offset - later.Moon0Offset) +
                              math.lengthsq(first.Moon1Offset - later.Moon1Offset);
            if (changedSq <= KeplerPositionEpsilonSq)
                throw new InvalidOperationException("Analytical orbit did not advance across time.");
        }

        private static void AssertSameVector(float3 a, float3 b, string label)
        {
            float deltaSq = math.lengthsq(a - b);
            if (deltaSq > KeplerPositionEpsilonSq)
                throw new InvalidOperationException($"Analytical orbit nondeterminism in {label}: deltaSq={deltaSq:0.000000000}.");
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float3 ResolveUnitCircleDirection(float radians)
        {
            return math.normalize(new float3(math.cos(radians), math.sin(radians), 0f));
        }
    }
}
#endif
