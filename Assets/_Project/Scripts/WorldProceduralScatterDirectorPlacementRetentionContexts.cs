using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private readonly struct ScatterPlacementRegistrationContext
        {
            public readonly Dictionary<long, ScatterPlacement> DesiredPlacements;
            public readonly Dictionary<long, ScatterPlacement> RetainedPlacements;
            public readonly Dictionary<long, float> PlacementLastSeenTimes;
            public readonly float Now;

            public ScatterPlacementRegistrationContext(
                Dictionary<long, ScatterPlacement> desiredPlacements,
                Dictionary<long, ScatterPlacement> retainedPlacements,
                Dictionary<long, float> placementLastSeenTimes,
                float now)
            {
                DesiredPlacements = desiredPlacements;
                RetainedPlacements = retainedPlacements;
                PlacementLastSeenTimes = placementLastSeenTimes;
                Now = now;
            }
        }

        private readonly struct ScatterRetentionEvictionContext
        {
            public readonly Dictionary<long, ScatterPlacement> RetainedPlacements;
            public readonly Dictionary<long, float> PlacementLastSeenTimes;
            public readonly List<long> RemovalBuffer;
            public readonly float Now;
            public readonly float RemovalThresholdSeconds;

            public ScatterRetentionEvictionContext(
                Dictionary<long, ScatterPlacement> retainedPlacements,
                Dictionary<long, float> placementLastSeenTimes,
                List<long> removalBuffer,
                float now,
                float removalThresholdSeconds)
            {
                RetainedPlacements = retainedPlacements;
                PlacementLastSeenTimes = placementLastSeenTimes;
                RemovalBuffer = removalBuffer;
                Now = now;
                RemovalThresholdSeconds = removalThresholdSeconds;
            }
        }

        private readonly struct ScatterRetentionRestoreContext
        {
            public readonly Dictionary<long, ScatterPlacement> DesiredPlacements;
            public readonly Dictionary<long, ScatterPlacement> RetainedPlacements;
            public readonly Dictionary<long, float> PlacementLastSeenTimes;
            public readonly Vector3 ObserverPosition;
            public readonly float Now;
            public readonly float GraceSeconds;

            public ScatterRetentionRestoreContext(
                Dictionary<long, ScatterPlacement> desiredPlacements,
                Dictionary<long, ScatterPlacement> retainedPlacements,
                Dictionary<long, float> placementLastSeenTimes,
                Vector3 observerPosition,
                float now,
                float graceSeconds)
            {
                DesiredPlacements = desiredPlacements;
                RetainedPlacements = retainedPlacements;
                PlacementLastSeenTimes = placementLastSeenTimes;
                ObserverPosition = observerPosition;
                Now = now;
                GraceSeconds = graceSeconds;
            }
        }
    }
}
