// ============================================================================
// HECTON-8 — BeaconRegistry.cs
// Registry for active DeployableBeacon instances.
//
// ARCHITECTURE:
//   • Static registry pattern (no singleton MonoBehaviour)
//   • Zero GC enumeration via pre-allocated array
//   • Thread-safe for main thread only
//
// USAGE:
//   • DeployableBeacon registers/unregisters in OnEnable/OnDisable
//   • HUD systems call GetAllBeacons() to enumerate for display
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Static registry for active DeployableBeacon instances.
    /// Allows HUD systems to enumerate all beacons for display.
    /// Zero GC: uses pre-allocated array for enumeration.
    /// </summary>
    public static class BeaconRegistry
    {
        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private static readonly List<DeployableBeacon> _beacons = new List<DeployableBeacon>(16);
        private static DeployableBeacon[] _beaconArray = new DeployableBeacon[16];
        private static int _beaconCount;
        private static bool _arrayDirty = true;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Number of active beacons.</summary>
        public static int Count => _beaconCount;

        /// <summary>Registers a beacon. Called by DeployableBeacon.OnEnable.</summary>
        public static void Register(DeployableBeacon beacon)
        {
            if (beacon == null || _beacons.Contains(beacon))
                return;

            _beacons.Add(beacon);
            _beaconCount = _beacons.Count;
            _arrayDirty = true;
        }

        /// <summary>Unregisters a beacon. Called by DeployableBeacon.OnDisable.</summary>
        public static void Unregister(DeployableBeacon beacon)
        {
            if (beacon == null)
                return;

            _beacons.Remove(beacon);
            _beaconCount = _beacons.Count;
            _arrayDirty = true;
        }

        /// <summary>
        /// Gets all active beacons as a read-only array.
        /// Zero GC: returns cached array, only rebuilds when dirty.
        /// </summary>
        /// <returns>Array of active beacons. Do NOT modify this array.</returns>
        public static DeployableBeacon[] GetAllBeacons()
        {
            if (_arrayDirty)
            {
                RebuildArray();
                _arrayDirty = false;
            }

            return _beaconArray;
        }

        /// <summary>
        /// Gets beacon by ID. O(n) search.
        /// </summary>
        public static DeployableBeacon GetById(string beaconId)
        {
            if (string.IsNullOrEmpty(beaconId))
                return null;

            for (int i = 0; i < _beacons.Count; i++)
            {
                DeployableBeacon beacon = _beacons[i];
                if (beacon != null && beacon.BeaconId == beaconId)
                    return beacon;
            }

            return null;
        }

        /// <summary>
        /// Finds the nearest beacon to a position.
        /// Returns null if no beacons registered.
        /// </summary>
        public static DeployableBeacon GetNearest(Vector3 position)
        {
            if (_beaconCount == 0)
                return null;

            DeployableBeacon nearest = null;
            float nearestDistSq = float.MaxValue;

            for (int i = 0; i < _beacons.Count; i++)
            {
                DeployableBeacon beacon = _beacons[i];
                if (beacon == null)
                    continue;

                float distSq = (beacon.Position - position).sqrMagnitude;
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearest = beacon;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Clears all registered beacons. Call when loading a new scene.
        /// </summary>
        public static void Clear()
        {
            _beacons.Clear();
            _beaconCount = 0;
            _arrayDirty = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private static void RebuildArray()
        {
            int count = _beacons.Count;

            // Resize array if needed
            if (_beaconArray == null || _beaconArray.Length < count)
            {
                _beaconArray = new DeployableBeacon[Mathf.Max(count, 16)];
            }

            // Copy to array
            for (int i = 0; i < count; i++)
            {
                _beaconArray[i] = _beacons[i];
            }

            // Null out remaining slots
            for (int i = count; i < _beaconArray.Length; i++)
            {
                _beaconArray[i] = null;
            }
        }
    }
}
