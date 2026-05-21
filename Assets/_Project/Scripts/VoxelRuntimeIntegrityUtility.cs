using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

internal static class VoxelRuntimeIntegrityUtility
{
    internal static bool ResolveBackpressureState(
        int pendingCount,
        bool currentlyActive,
        int activationThreshold,
        int releaseThreshold)
    {
        bool nextActive = currentlyActive;
        if (!nextActive && pendingCount > activationThreshold)
            nextActive = true;
        else if (nextActive && pendingCount <= releaseThreshold)
            nextActive = false;

        return nextActive;
    }

    internal static int ResolveDistanceBasedLodLevel(
        Vector3 worldCenter,
        Vector3 observerPosition,
        float lodDistanceMeters)
    {
        if (!TryResolveAupFromRuntimeOrigin(worldCenter, out AbsoluteUniversePosition worldCenterAup) ||
            !TryResolveAupFromRuntimeOrigin(observerPosition, out AbsoluteUniversePosition observerAup))
        {
            return 1;
        }

        double distanceSq = AbsoluteUniversePosition.DistanceSq(in worldCenterAup, in observerAup);
        double thresholdSq = (double)lodDistanceMeters * lodDistanceMeters;
        return distanceSq > thresholdSq ? 1 : 0;
    }

    private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition absoluteAup)
    {
        absoluteAup = default;
        if (!float.IsFinite(runtimePosition.x) ||
            !float.IsFinite(runtimePosition.y) ||
            !float.IsFinite(runtimePosition.z))
        {
            return false;
        }

        AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
        if (!originAup.IsFinite())
            return false;

        absoluteAup = AbsoluteUniversePosition.OffsetMeters(
            in originAup,
            new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
        return absoluteAup.IsFinite();
    }

    internal static bool ResolveFixedPoolExhausted(int inUseCount, int capacity)
    {
        return capacity > 0 && inUseCount >= capacity;
    }
}
