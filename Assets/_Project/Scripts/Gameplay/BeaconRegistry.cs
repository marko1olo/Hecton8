// ============================================================================
// HECTON-8 - BeaconRegistry.cs
// Fixed-capacity registry for active DeployableBeacon instances.
// ============================================================================

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

        // COLD ALLOC: DeployableBeacon[128] - active beacon registry storage - owner: BeaconRegistry
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
                return;

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

        /// <summary>Finds the nearest beacon to a position. Returns null if no beacons are registered.</summary>
        public static DeployableBeacon GetNearest(Vector3 position)
        {
            if (_beaconCount == 0)
                return null;

            DeployableBeacon nearest = null;
            float nearestDistSq = float.MaxValue;
            for (int i = 0; i < _beaconCount; i++)
            {
                DeployableBeacon beacon = _beacons[i];
                if (beacon == null)
                    continue;

                float distSq = (beacon.Position - position).sqrMagnitude;
                if (distSq >= nearestDistSq)
                    continue;

                nearestDistSq = distSq;
                nearest = beacon;
            }

            return nearest;
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
    }
}
