// HECTON-8 — HectonHazardManager.cs
// Compatibility bridge for runtime hazard registration and point queries.
// Owns the persistent hazard host and forwards work into HazardZoneManager.
// ============================================================================

using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Compatibility bridge for hazard registration and point queries.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5700)]
    public sealed class HectonHazardManager : MonoBehaviour
    {
        public static HectonHazardManager Instance { get; private set; }

        private HazardZoneManager _zoneManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveOrAddZoneManager();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        internal HazardZoneManager ResolveOrAddZoneManager()
        {
            if (_zoneManager == null)
            {
                if (!TryGetComponent(out _zoneManager))
                    _zoneManager = gameObject.AddComponent<HazardZoneManager>();
            }

            return _zoneManager;
        }

        internal static HectonHazardManager EnsureRuntimeInstance()
        {
            if (Instance != null)
                return Instance;

            if (!Application.isPlaying)
                return null;

            // COLD ALLOC: GameObject[1] — persistent runtime hazard host — owner: HectonHazardManager
            GameObject runtimeHost = new GameObject(nameof(HectonHazardManager));
            DontDestroyOnLoad(runtimeHost);
            return runtimeHost.AddComponent<HectonHazardManager>();
        }

        /// <summary>
        /// Registers or updates a runtime hazard volume.
        /// </summary>
        public static bool Register(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type)
        {
            HazardZoneManager zoneManager = HazardZoneManager.EnsureRuntimeInstance();
            return zoneManager != null && zoneManager.RegisterZone(id, runtimePosition, intensity, radius, type);
        }

        /// <summary>
        /// Removes a previously registered runtime hazard volume.
        /// </summary>
        public static void Unregister(int id)
        {
            HazardZoneManager zoneManager = Instance != null
                ? Instance.ResolveOrAddZoneManager()
                : null;
            if (zoneManager != null)
                zoneManager.UnregisterZone(id);
        }

        /// <summary>
        /// Returns the summed hazard intensity at a runtime world-space point.
        /// </summary>
        public static float GetHazardIntensity(Vector3 runtimePoint, HazardType type)
        {
            HazardZoneManager zoneManager = Instance != null
                ? Instance.ResolveOrAddZoneManager()
                : null;
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(runtimePoint, type)
                : 0f;
        }
    }
}
