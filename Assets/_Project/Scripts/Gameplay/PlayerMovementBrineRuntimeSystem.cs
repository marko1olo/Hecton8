using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using BrineLayerSample = Hecton8.Core.Contracts.BrineLayerSample;

namespace Hecton8.Gameplay
{
    internal static class PlayerMovementBrineRuntimeSystem
    {
        private static readonly uint _runtimeCostHash = unchecked((uint)LocHash.Compute("PLAYER_BRINE_RUNTIME_SYSTEM"));
        private static readonly double _stopwatchTicksToMilliseconds = 1000d / System.Diagnostics.Stopwatch.Frequency;

        internal static bool TrySampleBrineLayer(
            ResourceDistributionDirector director,
            Vector3 runtimePosition,
            bool dryInterior,
            float shiftOffsetY,
            out BrineLayerSample sample,
            out bool submerged)
        {
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            sample = default;
            submerged = false;
            bool resolved = false;
            if (!dryInterior &&
                director != null &&
                director.TrySampleBrineLayer(runtimePosition, out sample))
            {
                submerged = BrineLayerMath.IsRuntimeBelowAbsolutePlane(
                    runtimePosition.y,
                    sample.AbsoluteHeightY,
                    shiftOffsetY);
                resolved = true;
            }

            ReportWatchdogCost(startTimestamp);
            return resolved;
        }

        internal static float ResolveFogHardClip(float qualityWeight01)
        {
            float t = math.saturate(math.isfinite(qualityWeight01) ? qualityWeight01 : 1f);
            float smoothQuality = t * t * (3f - (2f * t));
            return math.lerp(1f, BrineLayerConstants.DefaultBrineFogHardClip, smoothQuality);
        }

        private static void ReportWatchdogCost(long startTimestamp)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks <= 0L)
                return;

            RuntimeWatchdog.ReportSubsystemCost(_runtimeCostHash, (float)(elapsedTicks * _stopwatchTicksToMilliseconds));
        }
    }
}
