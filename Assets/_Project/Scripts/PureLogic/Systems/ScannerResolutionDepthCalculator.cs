using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    public static class ScannerResolutionDepthCalculator
    {
        public static float Compute(float targetDistance, float maxScanRange, float ambientNoiseLevel, float scannerPower)
        {
            if (float.IsNaN(targetDistance) || float.IsInfinity(targetDistance) ||
                float.IsNaN(maxScanRange) || float.IsInfinity(maxScanRange) ||
                float.IsNaN(ambientNoiseLevel) || float.IsInfinity(ambientNoiseLevel) ||
                float.IsNaN(scannerPower) || float.IsInfinity(scannerPower))
            {
                return 0f;
            }

            if (maxScanRange <= 0f) return 0f;

            float clampedDistance = Math.Max(0f, targetDistance);
            if (clampedDistance >= maxScanRange) return 0f;

            float clampedNoise = Math.Max(0f, ambientNoiseLevel);
            float clampedPower = Math.Max(0f, scannerPower);

            if (clampedPower == 0f && clampedNoise == 0f) return 0f;
            if (clampedPower == 0f) return 0f;

            float distanceFactor = 1f - (clampedDistance / maxScanRange);
            float signalToNoise = clampedPower / (clampedPower + clampedNoise);

            return Math.Clamp(distanceFactor * signalToNoise, 0f, 1f);
        }
    }
}
