using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.PDA;
using Hecton8.Physics;
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
        private bool _registeredToTick;
        private int _currentDayIndex = 1;

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

        private Vector3 ResolveMigrationProbePosition(int biomeIndex, uint hash)
        {
            float biomeOffset = biomeIndex * 173.31f;
            float dayOffset = _currentDayIndex * 41.7f;
            float x = Mathf.Sin(biomeOffset + dayOffset + Hash01(hash ^ 0x68E31DA4u) * 6.28318f) * 420f;
            float z = Mathf.Cos(biomeOffset * 0.5f + dayOffset + Hash01(hash ^ 0xC2B2AE35u) * 6.28318f) * 420f;
            float y = -Mathf.Lerp(24f, 220f, Hash01(hash ^ 0x9E3779B9u));
            return new Vector3(x, y, z);
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

        private static float Hash01(uint value)
        {
            return (Hash(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }
    }
}
