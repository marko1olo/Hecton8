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
        float distanceSq = (worldCenter - observerPosition).sqrMagnitude;
        float thresholdSq = lodDistanceMeters * lodDistanceMeters;
        return distanceSq > thresholdSq ? 1 : 0;
    }

    internal static bool ResolveFixedPoolExhausted(int inUseCount, int capacity)
    {
        return capacity > 0 && inUseCount >= capacity;
    }
}
