using Hecton8.World;
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
        AbsoluteUniversePosition worldCenterAup = AbsoluteUniversePosition.FromRuntimePosition(worldCenter);
        AbsoluteUniversePosition observerAup = AbsoluteUniversePosition.FromRuntimePosition(observerPosition);
        double distanceSq = AbsoluteUniversePosition.DistanceSq(in worldCenterAup, in observerAup);
        double thresholdSq = (double)lodDistanceMeters * lodDistanceMeters;
        return distanceSq > thresholdSq ? 1 : 0;
    }

    internal static bool ResolveFixedPoolExhausted(int inUseCount, int capacity)
    {
        return capacity > 0 && inUseCount >= capacity;
    }
}
