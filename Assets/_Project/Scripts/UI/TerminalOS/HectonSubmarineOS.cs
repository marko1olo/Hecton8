using UnityEngine;
using Hecton8.Core;
using Hecton8.Gameplay.Atlas6Liability;
using Unity.Mathematics;

namespace Hecton8.UI.TerminalOS
{
    [DisallowMultipleComponent]
    public sealed class HectonSubmarineOS : MonoBehaviour, IUpdatable
    {
        private static readonly uint ThermalSheerUpdateHash = unchecked((uint)Animator.StringToHash("THERMAL_SHEER_UPDATE"));
        private static int s_signalDropCount;

        [SerializeField] private Atlas6CorporateLiabilityManager liabilityManager;

        [Header("UI Readouts")]
        public float DisplayedThermalSheer;
        public float DisplayedHullStress;

        private float actualThermalSheer;

        private bool _registeredTick;

        private void OnEnable()
        {
            if (Application.isPlaying)
                _registeredTick = Hecton8.Core.GlobalRegistry.TryRegisterUpdatable(this, Hecton8.Core.PriorityLayer.UI);
        }

        private void OnDisable()
        {
            if (_registeredTick)
            {
                Hecton8.Core.GlobalRegistry.UnregisterUpdatable(this, Hecton8.Core.PriorityLayer.UI);
                _registeredTick = false;
            }
        }

        private void Start()
        {
            TryBindLiabilityManager();
        }

        public void Tick(float deltaTime)
        {
            TryBindLiabilityManager();

            if (liabilityManager != null)
            {
                // Retrieve the smoothed lie (Varnek Protocol)
                var telemetry = liabilityManager.GetSubmarineOSReadout(actualThermalSheer);
                DisplayedThermalSheer = telemetry.ReportedSheer;
            }
            else
            {
                DisplayedThermalSheer = actualThermalSheer;
            }

            UpdateUIElements();
        }

        public void UpdateActualSensors(float realThermalSheer, float realHullStress)
        {
            actualThermalSheer = math.isfinite(realThermalSheer) ? math.saturate(realThermalSheer) : 0f;
            DisplayedHullStress = math.isfinite(realHullStress) ? math.max(0f, realHullStress) : 0f;
        }

        private void UpdateUIElements()
        {
            Hecton8.Core.Contracts.Signals.DiegeticHudSignal hudSignal = new Hecton8.Core.Contracts.Signals.DiegeticHudSignal
            {
                MessageHash = ThermalSheerUpdateHash,
                Priority = 1,
                Frame = (uint)Time.frameCount
            };
            Hecton8.Core.Contracts.Signals.SignalBus<Hecton8.Core.Contracts.Signals.DiegeticHudSignal>.TryPushTracked(in hudSignal, ref s_signalDropCount);
        }

        private void TryBindLiabilityManager()
        {
            if (liabilityManager == null)
                liabilityManager = Atlas6CorporateLiabilityManager.ActiveRuntimeInstance;
        }
    }
}
