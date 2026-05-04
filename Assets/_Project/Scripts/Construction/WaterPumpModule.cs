using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Power;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Water Pump Module")]
    public sealed class WaterPumpModule : MonoBehaviour, IPowerComponent, IPoolable
    {
        private const int InitialPumpCapacity = 16;

        [Header("Pump")]
        [SerializeField, Min(0f)] private float pumpRateM3PerSecond = 1.8f;
        [SerializeField, Min(0f)] private float powerDrawWatts = 2400f;
        [SerializeField, Range(0, 100)] private int powerPriority = 8;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private float _debugLastDrainBudgetM3;

        // COLD ALLOC: List<WaterPumpModule>[16] - active pump registry for CSR flood drainage - owner: WaterPumpModule
        private static readonly List<WaterPumpModule> s_activePumps = new List<WaterPumpModule>(InitialPumpCapacity);

        private BaseModule _hostModule;
        private bool _hasPower = true;
        private bool _registered;

        public float PowerRating => -math.max(0f, powerDrawWatts);
        public int PowerPriority => powerPriority;
        public bool HasPower => _hasPower;
        internal BaseModule HostModule => _hostModule;
        internal bool CanPump => isActiveAndEnabled && _hasPower && pumpRateM3PerSecond > 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activePumps.Clear();
        }

        private void Awake()
        {
            if (_hostModule == null)
                TryGetComponent(out _hostModule);
            if (_hostModule == null)
                _hostModule = GetComponentInParent<BaseModule>();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            Register();
        }

        public void OnDespawn()
        {
            Unregister();
            _hasPower = true;
            _debugHasPower = true;
            _debugLastDrainBudgetM3 = 0f;
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;
        }

        internal static int ActivePumpCount => s_activePumps.Count;

        internal static WaterPumpModule GetActivePump(int index)
        {
            return index >= 0 && index < s_activePumps.Count ? s_activePumps[index] : null;
        }

        internal float ResolveDrainBudgetM3(float deltaTime)
        {
            float budget = CalculatePumpDrainVolumeM3(pumpRateM3PerSecond, _hasPower ? 1f : 0f, deltaTime);
            _debugLastDrainBudgetM3 = budget;
            return budget;
        }

        internal static float CalculatePumpDrainVolumeM3(float rateM3PerSecond, float powerSupplyRatio, float deltaTime)
        {
            if (rateM3PerSecond <= 0f || powerSupplyRatio <= 0f || deltaTime <= 0f)
                return 0f;

            float volume = rateM3PerSecond * math.saturate(powerSupplyRatio) * deltaTime;
            return math.isfinite(volume) ? math.max(0f, volume) : 0f;
        }

        private void Register()
        {
            if (_registered)
                return;

            if (_hostModule == null)
                TryGetComponent(out _hostModule);

            s_activePumps.Add(this);
            _registered = true;
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            for (int i = s_activePumps.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_activePumps[i], this))
                    s_activePumps.RemoveAt(i);
            }

            _registered = false;
        }
    }
}
