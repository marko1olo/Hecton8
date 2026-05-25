// ============================================================================
// HECTON-8 - BeaconRegistry.cs
// Fixed-capacity registry for active DeployableBeacon instances.
// ============================================================================

using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Static registry for active DeployableBeacon instances.
    /// Returns a stable caller-read array; consumers must respect Count.
    /// </summary>
    public static class BeaconRegistry
    {
        private const int MaxBeacons = 128;

        // COLD ALLOC: DeployableBeacon[128] — active beacon registry storage — owner: BeaconRegistry
        private static readonly DeployableBeacon[] _beacons = new DeployableBeacon[MaxBeacons];
        private static int _beaconCount;

        /// <summary>Number of active beacons.</summary>
        public static int Count => _beaconCount;

        /// <summary>Registers a beacon. Called by DeployableBeacon.OnEnable.</summary>
        public static void Register(DeployableBeacon beacon)
        {
            if (beacon == null || IndexOfBeacon(beacon) >= 0)
                return;

            if (!EnsureDenseCapacity(_beaconCount + 1))
            {
                LogCapacityExceeded();
                return;
            }

            _beacons[_beaconCount] = beacon;
            _beaconCount++;
        }

        /// <summary>Unregisters a beacon. Called by DeployableBeacon.OnDisable.</summary>
        public static void Unregister(DeployableBeacon beacon)
        {
            int index = IndexOfBeacon(beacon);
            if (index < 0)
                return;

            int lastIndex = _beaconCount - 1;
            _beacons[index] = _beacons[lastIndex];
            _beacons[lastIndex] = null;
            _beaconCount = lastIndex;
        }

        /// <summary>
        /// Gets the stable backing array of active beacons. Only the first Count entries are valid.
        /// </summary>
        public static DeployableBeacon[] GetAllBeacons()
        {
            return _beacons;
        }

        /// <summary>Gets beacon by ID. O(n) over bounded fixed storage.</summary>
        public static DeployableBeacon GetById(string beaconId)
        {
            if (string.IsNullOrEmpty(beaconId))
                return null;

            for (int i = 0; i < _beaconCount; i++)
            {
                DeployableBeacon beacon = _beacons[i];
                if (beacon != null &&
                    string.Equals(beacon.BeaconId, beaconId, global::System.StringComparison.Ordinal))
                {
                    return beacon;
                }
            }

            return null;
        }

        /// <summary>Finds the nearest beacon to a runtime-space position. Returns null if no beacons are registered.</summary>
        public static DeployableBeacon GetNearest(Vector3 position)
        {
            if (!TryResolveRuntimeAup(position, out AbsoluteUniversePosition originAup))
                return null;

            return GetNearest(in originAup);
        }

        /// <summary>Finds the nearest beacon to an absolute universe position.</summary>
        public static DeployableBeacon GetNearest(in AbsoluteUniversePosition originAup)
        {
            return TryGetNearest(in originAup, out DeployableBeacon nearest, out _)
                ? nearest
                : null;
        }

        /// <summary>Finds the nearest beacon and returns squared AUP distance.</summary>
        public static bool TryGetNearest(in AbsoluteUniversePosition originAup, out DeployableBeacon nearest, out double nearestDistanceSq)
        {
            if (_beaconCount == 0)
            {
                nearest = null;
                nearestDistanceSq = double.MaxValue;
                return false;
            }

            nearest = null;
            nearestDistanceSq = double.MaxValue;
            for (int i = 0; i < _beaconCount; i++)
            {
                DeployableBeacon beacon = _beacons[i];
                if (beacon == null)
                    continue;

                AbsoluteUniversePosition beaconAup = beacon.PositionAup;
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in beaconAup, in originAup);
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                nearest = beacon;
            }

            return nearest != null;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        /// <summary>Clears all registered beacons. Call when loading a new scene.</summary>
        public static void Clear()
        {
            for (int i = 0; i < _beaconCount; i++)
                _beacons[i] = null;

            _beaconCount = 0;
        }

        private static int IndexOfBeacon(DeployableBeacon beacon)
        {
            if (beacon == null)
                return -1;

            for (int i = 0; i < _beaconCount; i++)
            {
                if (ReferenceEquals(_beacons[i], beacon))
                    return i;
            }

            return -1;
        }

        private static bool EnsureDenseCapacity(int requiredCount)
        {
            return requiredCount <= MaxBeacons;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCapacityExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning("[BeaconRegistry] Fixed active beacon capacity exceeded.");
#endif
        }
    }
}
