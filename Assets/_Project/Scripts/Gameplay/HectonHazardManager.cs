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
        private const int MaxTrackedRadiationFacadeIds = 1024;
        private const float HazardIntensityHardCap = 1000f;

        // COLD ALLOC: int[1024] - untyped compatibility radiation source IDs - owner: HectonHazardManager
        private static readonly int[] _radiationFacadeIds = new int[MaxTrackedRadiationFacadeIds];
        private static int _radiationFacadeIdCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            System.Array.Clear(_radiationFacadeIds, 0, _radiationFacadeIds.Length);
            _radiationFacadeIdCount = 0;
        }

        internal HazardZoneManager ResolveOrAddZoneManager()
        {
            return ResolveZoneManager();
        }

        internal static HectonHazardManager EnsureRuntimeInstance()
        {
            EnvironmentRuntimeContextService environmentService = EnvironmentRuntimeContextService.EnsureRuntimeInstance();
            if (environmentService == null)
                return null;

            environmentService.InitializeService();

            if (!environmentService.TryGetComponent(out HectonHazardManager bridge))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
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
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition positionAup))
            {
                Unregister(id, type);
                return false;
            }

            return Register(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
        }

        internal static bool Register(int id, in AbsoluteUniversePosition positionAup, float intensity, float radius, HazardType type, float visorGlitchBias, HazardZoneProfile profile)
        {
            if (!IsFiniteAup(in positionAup))
            {
                Unregister(id, type);
                return false;
            }

            if (type == HazardType.Radiation)
            {
                if (!Application.isPlaying || id == 0)
                    return false;

                if (!IsValidRadiationFacadeSourceInput(intensity, radius))
                {
                    _ = UntrackRadiationFacadeId(id);
                    RadiationHazardGrid.UnregisterSource(id);
                    return false;
                }

                if (!TrackRadiationFacadeId(id, out bool addedFacadeId))
                {
                    RadiationHazardGrid.UnregisterSource(id);
                    return false;
                }

                if (addedFacadeId)
                {
                    HazardZoneManager existingZoneManager = TryResolveZoneManager();
                    if (existingZoneManager != null)
                        existingZoneManager.UnregisterZone(id);
                }

                RadiationHazardGrid.RegisterSource(id, in positionAup, intensity, radius);
                return true;
            }

            if (UntrackRadiationFacadeId(id))
                RadiationHazardGrid.UnregisterSource(id);

            HazardZoneManager zoneManager = ResolveZoneManager();
            if (zoneManager == null)
                return false;

            bool registered = zoneManager.RegisterZone(id, in positionAup, intensity, radius, type, visorGlitchBias, profile);
            if (!registered)
                zoneManager.UnregisterZone(id);

            return registered;
        }

        /// <summary>
        /// Removes a previously registered runtime hazard volume.
        /// </summary>
        public static void Unregister(int id)
        {
            if (id == 0)
                return;

            if (UntrackRadiationFacadeId(id))
                RadiationHazardGrid.UnregisterSource(id);

            HazardZoneManager zoneManager = TryResolveZoneManager();
            if (zoneManager != null)
                zoneManager.UnregisterZone(id);
        }

        public static void Unregister(int id, HazardType type)
        {
            if (id == 0)
                return;

            if (type == HazardType.Radiation)
            {
                _ = UntrackRadiationFacadeId(id);
                RadiationHazardGrid.UnregisterSource(id);
                return;
            }

            HazardZoneManager zoneManager = TryResolveZoneManager();
            if (zoneManager != null)
                zoneManager.UnregisterZone(id);
        }

        /// <summary>
        /// Returns the bounded summed hazard intensity at a runtime world-space point.
        /// </summary>
        public static float GetHazardIntensity(Vector3 runtimePoint, HazardType type)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePoint, out AbsoluteUniversePosition pointAup))
                return 0f;

            if (type == HazardType.Radiation)
                return RadiationHazardGrid.TrySampleRadiationIntensity01(in pointAup, out float radiation01) ? SanitizeHazardIntensity(radiation01) : 0f;

            HazardZoneManager zoneManager = TryResolveZoneManager();
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(in pointAup, type)
                : 0f;
        }

        /// <summary>
        /// Returns the bounded summed hazard intensity at an absolute-universe point without exposing the World AUP type to callers.
        /// </summary>
        public static float GetHazardIntensity(double3 absolutePoint, HazardType type)
        {
            if (!math.all(math.isfinite(absolutePoint)))
                return 0f;

            AbsoluteUniversePosition pointAup = AbsoluteUniversePosition.FromAbsolutePosition(absolutePoint);
            if (type == HazardType.Radiation)
                return RadiationHazardGrid.TrySampleRadiationIntensity01(in pointAup, out float radiation01) ? SanitizeHazardIntensity(radiation01) : 0f;

            HazardZoneManager zoneManager = TryResolveZoneManager();
            return zoneManager != null
                ? zoneManager.GetHazardIntensity(in pointAup, type)
                : 0f;
        }

        /// <summary>
        /// Returns the bounded summed hazard intensity at an absolute-universe point.
        /// </summary>
        public static float GetHazardIntensity(in AbsoluteUniversePosition pointAup, HazardType type)
        {
            if (!IsFiniteAup(in pointAup))
                return 0f;

            if (type == HazardType.Radiation)
                return RadiationHazardGrid.TrySampleRadiationIntensity01(in pointAup, out float radiation01) ? SanitizeHazardIntensity(radiation01) : 0f;

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
            if (environmentService == null)
                return GlobalRegistry.HazardZones;

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

        private static bool IsValidRadiationFacadeSourceInput(float intensity, float radius)
        {
            return math.isfinite(intensity) &&
                   intensity > 0f &&
                   math.isfinite(radius) &&
                   radius > 0f;
        }

        private static float SanitizeHazardIntensity(float intensity)
        {
            return math.isfinite(intensity) ? math.clamp(intensity, 0f, HazardIntensityHardCap) : 0f;
        }

        private static bool TrackRadiationFacadeId(int id, out bool added)
        {
            added = false;
            if (id == 0)
                return false;

            for (int i = 0; i < _radiationFacadeIdCount; i++)
            {
                if (_radiationFacadeIds[i] == id)
                    return true;
            }

            if (_radiationFacadeIdCount >= _radiationFacadeIds.Length)
                return false;

            _radiationFacadeIds[_radiationFacadeIdCount] = id;
            _radiationFacadeIdCount++;
            added = true;
            return true;
        }

        private static bool UntrackRadiationFacadeId(int id)
        {
            if (id == 0)
                return false;

            for (int i = 0; i < _radiationFacadeIdCount; i++)
            {
                if (_radiationFacadeIds[i] != id)
                    continue;

                _radiationFacadeIdCount--;
                _radiationFacadeIds[i] = _radiationFacadeIds[_radiationFacadeIdCount];
                _radiationFacadeIds[_radiationFacadeIdCount] = 0;
                return true;
            }

            return false;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePosition)
        {
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.isfinite(positionAup.LocalX) &&
                   math.isfinite(positionAup.LocalY) &&
                   math.isfinite(positionAup.LocalZ);
        }
    }
}
