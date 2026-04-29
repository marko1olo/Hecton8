// HECTON-8 — HectonHazardManager.cs
// Compatibility bridge for runtime hazard registration and point queries.
// Owns the persistent hazard host and forwards work into HazardZoneManager.
// ============================================================================

using Hecton8.Core;
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
        internal HazardZoneManager ResolveOrAddZoneManager()
        {
            return ResolveZoneManager();
        }

        internal static HectonHazardManager EnsureRuntimeInstance()
        {
            EnvironmentRuntimeContextService environmentService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            environmentService.InitializeService();

            if (!environmentService.TryGetComponent(out HectonHazardManager bridge))
            {
                bridge = environmentService.gameObject.AddComponent<HectonHazardManager>(); // COLD ALLOC: HectonHazardManager[1] - compatibility bridge hosted by environment runtime context - owner: EnvironmentRuntimeContextService
            }

            return bridge;
        }

        /// <summary>
        /// Registers or updates a runtime hazard volume.
        /// </summary>
        public static bool Register(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias = 1f)
        {
            HazardZoneManager zoneManager = ResolveZoneManager();
            return zoneManager != null && zoneManager.RegisterZone(id, runtimePosition, intensity, radius, type, visorGlitchBias);
        }

        /// <summary>
        /// Removes a previously registered runtime hazard volume.
        /// </summary>
        public static void Unregister(int id)
        {
            HazardZoneManager zoneManager = ResolveZoneManager();
            if (zoneManager != null)
                zoneManager.UnregisterZone(id);
        }

        /// <summary>
        /// Returns the summed hazard intensity at a runtime world-space point.
        /// </summary>
        public static float GetHazardIntensity(Vector3 runtimePoint, HazardType type)
        {
            HazardZoneManager zoneManager = ResolveZoneManager();
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(runtimePoint, type)
                : 0f;
        }

        private static HazardZoneManager ResolveZoneManager()
        {
            IEnvironmentRuntimeContext environmentContext = GlobalRegistry.Environment;
            if (environmentContext != null && environmentContext.HazardZones != null)
                return environmentContext.HazardZones;

            EnvironmentRuntimeContextService environmentService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            environmentService.InitializeService();
            return environmentService.HazardZones;
        }
    }
}
