using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.PDA;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Slow-tick owner for deterministic daily fauna migration pressure that biases spawn density by biome and species.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6225)]
    [AddComponentMenu("Hecton8/Ecosystem/Migration Director")]
    public sealed class MigrationDirector : MonoBehaviour, ISlowTickable
    {
        private static MigrationDirector _instance;
        private const float DefaultMigrationDistanceMeters = 320f;
        private bool _registeredToTick;
        private int _currentDayIndex = 1;

        [Header("Temperature Migration")]
        [Tooltip("Optional authored temperature bands used to steer migration routes and herbivore relocation targets.")]
        [SerializeField] private EcosystemMigrationProfile migrationProfile;
        [Tooltip("Fallback route distance used when no authored temperature band matches the sampled water.")]
        [SerializeField, Min(1f)] private float fallbackMigrationDistanceMeters = DefaultMigrationDistanceMeters;
        [Tooltip("How strongly local water current bends the fallback route heading.")]
        [SerializeField, Range(0f, 1f)] private float fallbackCurrentAlignmentWeight = 0.55f;

        [Header("Diagnostics")]
        [SerializeField] private float _debugLastMigrationTemperatureCelsius = 15f;
        [SerializeField] private Vector3 _debugLastMigrationDirection = Vector3.forward;

        /// <summary>Active runtime owner while the gameplay scene is loaded.</summary>
        public static MigrationDirector Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            RefreshCurrentDay();
        }

        private void OnEnable()
        {
            TryRegisterToTickManager();
            RefreshCurrentDay();
        }

        private void Start()
        {
            TryRegisterToTickManager();
            RefreshCurrentDay();
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            if (_instance == this)
                _instance = null;
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            RefreshCurrentDay();
        }

        /// <summary>
        /// Resolves the current daily selection multiplier for one biome/archetype pair.
        /// </summary>
        public static float ResolveSelectionMultiplier(int biomeIndex, CreatureArchetypeData archetype)
        {
            return _instance != null ? _instance.ResolveSelectionMultiplierInternal(biomeIndex, archetype) : 1f;
        }

        internal static bool TryResolveMigrationTarget(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            return _instance != null && _instance.TryResolveMigrationTargetInternal(speciesId, origin, out target);
        }

        private float ResolveSelectionMultiplierInternal(int biomeIndex, CreatureArchetypeData archetype)
        {
            if (biomeIndex < 0 || archetype == null)
                return 1f;

            if (archetype.roleType != CreatureRoleType.Ambient &&
                archetype.roleType != CreatureRoleType.Territorial)
            {
                return 1f;
            }

            int worldSeed = FaunaGeneticsManager.Instance != null ? FaunaGeneticsManager.Instance.WorldSeed : 0;
            uint hash = Hash((uint)worldSeed ^ (uint)_currentDayIndex * 0x9E3779B9u ^ (uint)biomeIndex * 0x85EBCA6Bu ^ HashString(archetype.creatureId));
            if ((hash & 0x3u) != 0u)
                return 1f;

            bool abundanceWave = (hash & 0x10u) != 0u;
            float waveStrength = Hash01(hash ^ 0x7F4A7C15u);
            float dailyWave = abundanceWave
                ? Mathf.Lerp(1.12f, 1.6f, waveStrength)
                : Mathf.Lerp(0.58f, 0.92f, waveStrength);
            float currentBias = ResolveCurrentDrivenBias(biomeIndex, archetype, hash);
            return dailyWave * currentBias;
        }

        private void RefreshCurrentDay()
        {
            int dayIndex;
            float dayTimeHours;
            float playTimeSeconds;
            PDAClockUtility.CaptureStamp(out dayIndex, out dayTimeHours, out playTimeSeconds);
            _currentDayIndex = Mathf.Max(1, dayIndex);
        }

        private float ResolveCurrentDrivenBias(int biomeIndex, CreatureArchetypeData archetype, uint hash)
        {
            Vector3 probePosition = ResolveMigrationProbePosition(biomeIndex, hash);
            Vector3 currentVector = CurrentVolume.SampleCombinedCurrent(probePosition);
            currentVector.y = 0f;
            float sqrMagnitude = currentVector.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 1f;

            currentVector.Normalize();

            float preferredHeadingRadians = Hash01(hash ^ 0xB5297A4Du) * Mathf.PI * 2f;
            Vector3 preferredHeading = new Vector3(
                Mathf.Cos(preferredHeadingRadians),
                0f,
                Mathf.Sin(preferredHeadingRadians));
            float alignment01 = Mathf.InverseLerp(-1f, 1f, Vector3.Dot(currentVector, preferredHeading));

            float roleBlend = archetype.roleType == CreatureRoleType.Ambient ? 0.82f : 0.64f;
            float weightedAlignment = Mathf.Lerp(0.5f, alignment01, roleBlend);
            return Mathf.Lerp(0.7f, 1.45f, weightedAlignment);
        }

        private bool TryResolveMigrationTargetInternal(int speciesId, Vector3 origin, out Vector3 target)
        {
            target = origin;
            float sampledTemperature = ResolveWaterTemperature(origin);
            Vector3 currentVector = CurrentVolume.SampleCombinedCurrent(origin);
            currentVector.y = 0f;

            EcosystemMigrationProfile.TemperatureRoute route = default;
            bool hasRoute = migrationProfile != null && migrationProfile.TryResolveRoute(sampledTemperature, out route);
            float routeDistance = hasRoute ? Mathf.Max(1f, route.migrationDistanceMeters) : Mathf.Max(1f, fallbackMigrationDistanceMeters);
            float currentAlignmentWeight = hasRoute ? Mathf.Clamp01(route.currentAlignmentWeight) : Mathf.Clamp01(fallbackCurrentAlignmentWeight);
            float depthBiasMeters = hasRoute ? route.depthBiasMeters : 0f;

            uint seed = Hash((uint)speciesId ^ (uint)_currentDayIndex * 0x9E3779B9u ^ HashFloat3(origin));
            Vector3 preferredDirection = hasRoute
                ? ResolvePreferredDirection(route.preferredPlanarDirection, seed)
                : ResolvePreferredDirection(Vector2.zero, seed);
            Vector3 migrationDirection = BlendRouteWithCurrent(preferredDirection, currentVector, currentAlignmentWeight);

            _debugLastMigrationTemperatureCelsius = sampledTemperature;
            _debugLastMigrationDirection = migrationDirection;

            target = origin + (migrationDirection * routeDistance) + (Vector3.down * depthBiasMeters);
            return true;
        }

        private Vector3 ResolveMigrationProbePosition(int biomeIndex, uint hash)
        {
            float biomeOffset = biomeIndex * 173.31f;
            float dayOffset = _currentDayIndex * 41.7f;
            float x = Mathf.Sin(biomeOffset + dayOffset + Hash01(hash ^ 0x68E31DA4u) * 6.28318f) * 420f;
            float z = Mathf.Cos(biomeOffset * 0.5f + dayOffset + Hash01(hash ^ 0xC2B2AE35u) * 6.28318f) * 420f;
            float y = -Mathf.Lerp(24f, 220f, Hash01(hash ^ 0x9E3779B9u));
            return new Vector3(x, y, z);
        }

        private static Vector3 BlendRouteWithCurrent(Vector3 preferredDirection, Vector3 currentVector, float currentAlignmentWeight)
        {
            Vector3 normalizedCurrent = currentVector.sqrMagnitude > 0.0001f ? currentVector.normalized : Vector3.zero;
            Vector3 blended = normalizedCurrent == Vector3.zero
                ? preferredDirection
                : Vector3.Slerp(preferredDirection, normalizedCurrent, currentAlignmentWeight);
            blended.y = 0f;
            if (blended.sqrMagnitude <= 0.0001f)
                return preferredDirection;

            return blended.normalized;
        }

        private float ResolveWaterTemperature(Vector3 origin)
        {
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            return bridge != null ? bridge.GetWaterTemperature(origin) : 15f;
        }

        private static Vector3 ResolvePreferredDirection(Vector2 preferredPlanarDirection, uint seed)
        {
            Vector2 planarDirection = preferredPlanarDirection.sqrMagnitude > 0.0001f
                ? preferredPlanarDirection.normalized
                : new Vector2(
                    Mathf.Cos(Hash01(seed ^ 0x68E31DA4u) * Mathf.PI * 2f),
                    Mathf.Sin(Hash01(seed ^ 0xC2B2AE35u) * Mathf.PI * 2f));
            return new Vector3(planarDirection.x, 0f, planarDirection.y);
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTick = false;
        }

        private static uint HashString(string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                    return 0x811C9DC5u;

                uint hash = 0x811C9DC5u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint Hash(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static uint HashFloat3(Vector3 value)
        {
            unchecked
            {
                uint hash = 0x811C9DC5u;
                hash = Hash(hash ^ (uint)Mathf.RoundToInt(value.x * 10f));
                hash = Hash(hash ^ (uint)Mathf.RoundToInt(value.y * 10f));
                hash = Hash(hash ^ (uint)Mathf.RoundToInt(value.z * 10f));
                return hash;
            }
        }

        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
