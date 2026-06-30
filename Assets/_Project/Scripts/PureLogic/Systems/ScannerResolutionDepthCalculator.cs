using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    public static class ScannerResolutionDepthCalculator
    {
        public static float ComputeSqr(float sqrTargetDistance, float sqrMaxScanRange, float ambientNoiseLevel, float scannerPower)
        {
            if (float.IsNaN(sqrTargetDistance) || float.IsInfinity(sqrTargetDistance) ||
                float.IsNaN(sqrMaxScanRange) || float.IsInfinity(sqrMaxScanRange) ||

                float.IsNaN(ambientNoiseLevel) || float.IsInfinity(ambientNoiseLevel) ||
                float.IsNaN(scannerPower) || float.IsInfinity(scannerPower))
            {
                return 0f;
            }

            if (sqrMaxScanRange <= 0f) return 0f;

            float clampedDistanceSqr = Math.Max(0f, sqrTargetDistance);
            if (clampedDistanceSqr >= sqrMaxScanRange) return 0f;


            float clampedNoise = Math.Max(0f, ambientNoiseLevel);
            float clampedPower = Math.Max(0f, scannerPower);

            if (clampedPower == 0f && clampedNoise == 0f) return 0f;
            if (clampedPower == 0f) return 0f;

            float distanceFactor = 1f - (float)Math.Sqrt(clampedDistanceSqr / sqrMaxScanRange);

            float signalToNoise = clampedPower / (clampedPower + clampedNoise);

            return Math.Clamp(distanceFactor * signalToNoise, 0f, 1f);
        }
    }
}
