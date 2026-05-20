// HECTON-8 - HectonHazardManager.cs
// Compatibility bridge for runtime hazard registration and point queries.
// Owns the persistent hazard host and forwards work into HazardZoneManager.
// ============================================================================

using Hecton8.Core;
using Hecton8.World;
using Unity.Mathematics;
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
            return Register(id, runtimePosition, intensity, radius, type, visorGlitchBias, null);
        }

        /// <summary>
        /// Registers or updates a runtime hazard volume using absolute-universe coordinates.
        /// </summary>
        public static bool Register(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias = 1f)
        {
            return Register(id, in positionAup, intensity, radius, type, visorGlitchBias, null);
        }

        internal static bool Register(int id, Vector3 runtimePosition, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            return Register(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
        }

        internal static bool Register(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!IsFiniteAup(in positionAup))
                return false;

            HazardZoneManager zoneManager = ResolveZoneManager();
            return zoneManager != null && zoneManager.RegisterZone(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
        }

        /// <summary>
        /// Removes a previously registered runtime hazard volume.
        /// </summary>
        public static void Unregister(int id)
        {
            HazardZoneManager zoneManager = TryResolveZoneManager();
            if (zoneManager != null)
                zoneManager.UnregisterZone(id);
        }

        /// <summary>
        /// Returns the summed hazard intensity at a runtime world-space point.
        /// </summary>
        public static float GetHazardIntensity(Vector3 runtimePoint, HazardType type)
        {
            if (!IsFiniteRuntimePosition(runtimePoint))
                return 0f;

            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePoint);
            HazardZoneManager zoneManager = TryResolveZoneManager();
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(in pointAup, type)
                : 0f;
        }

        /// <summary>
        /// Returns the summed hazard intensity at an absolute-universe point without exposing the World AUP type to callers.
        /// </summary>
        public static float GetHazardIntensity(double3 absolutePoint, HazardType type)
        {
            if (!math.all(math.isfinite(absolutePoint)))
                return 0f;

            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromAbsolutePosition(absolutePoint);
            HazardZoneManager zoneManager = TryResolveZoneManager();
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(in pointAup, type)
                : 0f;
        }

        /// <summary>
        /// Returns the summed hazard intensity at an absolute-universe point.
        /// </summary>
        public static float GetHazardIntensity(in AbsoluteUniversePosition pointAup, HazardType type)
        {
            if (!IsFiniteAup(in pointAup))
                return 0f;

            HazardZoneManager zoneManager = TryResolveZoneManager();
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(in pointAup, type)
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

        private static HazardZoneManager TryResolveZoneManager()
        {
            IEnvironmentRuntimeContext environmentContext = GlobalRegistry.Environment;
            if (environmentContext != null && environmentContext.HazardZones != null)
                return environmentContext.HazardZones;

            return GlobalRegistry.HazardZones;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.isfinite(positionAup.LocalX) &&
                   math.isfinite(positionAup.LocalY) &&
                   math.isfinite(positionAup.LocalZ);
        }
    }
}
